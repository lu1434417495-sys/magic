"""Parallel battle-sim evaluator for AI auto-tuning.

Given a *genome* (concrete values for a set of tunable parameters), this module:
  1. renders the genome into a BattleSimProfileDef .tres (override_patches),
  2. fans the scenario+profile out across N isolated `godot --headless` workers,
  3. parses every run's outcome and returns an aggregate Fitness.

Why processes, not threads / GPU: the battle is branchy, sequential, control-flow
heavy (turn-based AI), so the only useful parallelism axis is "many independent
battles". See docs and the memory note `parallel-battle-sim-tuning`.

ISOLATION (critical): this machine runs inside a VS Code snap that sets
XDG_DATA_HOME, and Godot resolves `user://` from XDG_DATA_HOME (NOT $HOME). Each
worker therefore gets its own HOME *and* XDG_{DATA,CONFIG,CACHE}_HOME, otherwise
all workers collide on the shared app_userdata (reports + import/shader caches),
which is what made naive multiprocessing "crash". Pre-warm `.godot/imported`
(one plain run) before launching a batch.

The battle is fully random / non-reproducible (TrueRandomSeedService ignores the
seed for combat rolls), so fitness is noisy: evaluate every genome over many runs
(>= ~20) and compare only aggregates, never single runs.
"""

from __future__ import annotations

import concurrent.futures
import glob
import json
import os
import shutil
import subprocess
import tempfile
from dataclasses import dataclass, field
from typing import Iterable, Mapping, Sequence

PROFILE_SCRIPT = "res://scripts/systems/battle/sim/BattleSimProfileDef.cs"
BALANCE_RUNNER = "res://tests/battle_runtime/simulation/run_battle_balance_simulation.cs"

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GODOT_BIN = os.environ.get("GODOT_BIN", "godot")


# --------------------------------------------------------------------------- #
# Central oracle sample store (cross-run accumulating dataset for the surrogate)
# --------------------------------------------------------------------------- #
# Each pipeline writes a per-run observations.jsonl into its own output dir (wiped
# per run). This is the COMPLEMENT: one append-only file that accumulates every
# genome evaluation across runs/sessions, so gpu_surrogate.train_surrogate() can be
# pointed here to learn from the full history. Schema is a superset of the per-run
# observations rows (genome + objective + fitness), so it is consumable as-is with
# target_key="objective"; we add ts/profile_id/faction/runs for provenance.
#
# Cross-process safe: many chunk subprocesses append concurrently, so each write
# takes an fcntl exclusive lock (intra-process threads also share _DATASET_LOCK).
# Disabled with BST_DATASET_DISABLE=1 (e.g. regression tests); path via BST_DATASET.
import fcntl  # noqa: E402
import threading  # noqa: E402
import time  # noqa: E402

from .objective import DEFAULT_MAX_ITERATIONS as _OBJ_MAX_ITER  # noqa: E402
from .objective import score_fitness as _score_fitness  # noqa: E402

DATASET_PATH = os.environ.get(
    "BST_DATASET", os.path.join(REPO_ROOT, "tools", "battle_sim_tuner", "dataset", "samples.jsonl")
)
_DATASET_LOCK = threading.Lock()


def _compact_runs(runs: Sequence[Mapping]) -> list:
    """Per-run ground truth, trimmed to what the surrogate needs."""
    out = []
    for r in runs or []:
        out.append(
            {
                "winner": (r.get("winner_faction_id") or "").strip(),
                "iterations": int(r.get("iterations", 0) or 0),
                "ended": bool(r.get("battle_ended", (r.get("winner_faction_id") or "") != "")),
            }
        )
    return out


def record_sample(
    genome: Mapping[str, float],
    specs: "Sequence[ParamSpec]",
    fit: "Fitness",
    *,
    scenario: str,
    win_faction: str,
    profile_id: str = "",
    extra: Mapping[str, object] | None = None,
) -> None:
    """Append one `(genome, scenario) -> fitness` sample to the central dataset.

    Row schema is a superset of run_gpu_*'s per-run observations (genome + objective +
    fitness), so gpu_surrogate.train_surrogate(DATASET_PATH, target_key="objective")
    works directly. Best-effort: never raise into the tuning loop on write failure.
    """
    if os.environ.get("BST_DATASET_DISABLE") == "1":
        return
    try:
        record = {
            "ts": time.time(),
            "scenario": scenario,
            "faction": win_faction,
            "profile_id": profile_id,
            "genome": {s.name: s.clamp(genome[s.name]) for s in specs if s.name in genome},
            # objective uses the shared DEFAULT_MAX_ITERATIONS (same convention as the
            # per-run observations); raw fitness is kept so a consumer can recompute.
            "objective": _score_fitness(fit, _OBJ_MAX_ITER),
            "fitness": {
                "score": fit.score,
                "win_rate": fit.win_rate,
                "loss_rate": fit.loss_rate,
                "stalemate_rate": fit.stalemate_rate,
                "avg_iterations": fit.avg_iterations,
                "net_kills": fit.net_kills,
                "own_deaths": fit.own_deaths,
                "enemy_deaths": fit.enemy_deaths,
                "own_damage": fit.own_damage,
                "enemy_damage": fit.enemy_damage,
                "n": fit.n,
            },
            "runs": _compact_runs(fit.runs),
        }
        if extra:
            record["extra"] = dict(extra)
        line = json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n"
        with _DATASET_LOCK:
            os.makedirs(os.path.dirname(DATASET_PATH), exist_ok=True)
            with open(DATASET_PATH, "a", encoding="utf-8") as fh:
                fcntl.flock(fh.fileno(), fcntl.LOCK_EX)
                try:
                    fh.write(line)
                    fh.flush()
                finally:
                    fcntl.flock(fh.fileno(), fcntl.LOCK_UN)
    except Exception:  # noqa: BLE001 - persistence must never break a tuning run
        pass


# --------------------------------------------------------------------------- #
# Parameters & genome rendering
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class ParamSpec:
    """One tunable axis: an override-patch template plus search bounds.

    `patch` is the override_patches entry WITHOUT "value", e.g.::

        {"target_type": "action", "brain_id": "mage_controller",
         "action_id": "mage_blink_escape",
         "path": "min_survival_margin_gain_to_escape"}
    """

    name: str
    patch: Mapping[str, object]
    lo: float
    hi: float
    is_int: bool = True

    def clamp(self, value: float):
        value = max(self.lo, min(self.hi, value))
        return int(round(value)) if self.is_int else float(value)


def _fmt_tres_value(value) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return repr(value)
    return f'"{value}"'


def render_profile_tres(
    profile_id: str, genome: Mapping[str, float], specs: Sequence[ParamSpec]
) -> str:
    """Render a genome into a BattleSimProfileDef .tres string."""
    entries = []
    for spec in specs:
        if spec.name not in genome:
            raise KeyError(f"genome is missing value for param '{spec.name}'")
        patch = {**dict(spec.patch), "value": spec.clamp(genome[spec.name])}
        body = ",\n".join(f'"{k}": {_fmt_tres_value(v)}' for k, v in patch.items())
        entries.append("{\n" + body + "\n}")
    patches = "Array[Dictionary]([" + ", ".join(entries) + "])"
    return (
        '[gd_resource type="Resource" format=3]\n\n'
        f'[ext_resource type="Script" path="{PROFILE_SCRIPT}" id="1_profile"]\n\n'
        "[resource]\n"
        'script = ExtResource("1_profile")\n'
        f'profile_id = &"{profile_id}"\n'
        f'display_name = "{profile_id}"\n'
        f"override_patches = {patches}\n"
    )


# --------------------------------------------------------------------------- #
# Fitness
# --------------------------------------------------------------------------- #
@dataclass
class Fitness:
    score: float          # scalar objective (higher is better)
    win_rate: float       # fraction of runs won by `win_faction`
    loss_rate: float      # fraction won by the opponent
    stalemate_rate: float # fraction that did not resolve (draw / hit max_iterations)
    avg_iterations: float
    n: int                # number of runs aggregated
    own_deaths: float = 0.0    # avg units of win_faction that died (low variance vs binary win)
    enemy_deaths: float = 0.0  # avg units of the opponent that died
    own_damage: float = 0.0    # avg damage dealt by win_faction
    enemy_damage: float = 0.0  # avg damage dealt by the opponent
    runs: list = field(default_factory=list, repr=False)

    @property
    def net_kills(self) -> float:
        """Continuous, low-variance proxy for 'closing out the fight'."""
        return self.enemy_deaths - self.own_deaths

    def __str__(self) -> str:
        return (
            f"score={self.score:+.3f} win={self.win_rate:.2f} loss={self.loss_rate:.2f} "
            f"stale={self.stalemate_rate:.2f} avg_iter={self.avg_iterations:.0f} "
            f"kills(e-o)={self.enemy_deaths:.1f}-{self.own_deaths:.1f}={self.net_kills:+.1f} n={self.n}"
        )


def score_runs(runs: Sequence[Mapping], win_faction: str, stalemate_penalty: float) -> Fitness:
    n = len(runs)
    if n == 0:
        return Fitness(-1.0, 0.0, 0.0, 1.0, 0.0, 0)
    wins = losses = stale = 0
    iters = 0
    own_d = enemy_d = own_dmg = enemy_dmg = 0
    for r in runs:
        iters += int(r.get("iterations", 0) or 0)
        winner = (r.get("winner_faction_id") or "").strip()
        # A non-empty winner means the battle resolved; `battle_ended` is only present in
        # some report shapes (e.g. the balance runner), so fall back to the winner.
        ended = bool(r.get("battle_ended", winner != ""))
        if not ended or not winner:
            stale += 1
        elif winner == win_faction:
            wins += 1
        else:
            losses += 1
        for fid, fd in (r.get("factions") or {}).items():
            if not isinstance(fd, dict):
                continue
            deaths = int(fd.get("death_count", 0) or 0)
            damage = int(fd.get("total_damage_done", 0) or 0)
            if str(fid) == win_faction:
                own_d += deaths
                own_dmg += damage
            else:
                enemy_d += deaths
                enemy_dmg += damage
    win_rate, loss_rate, stale_rate = wins / n, losses / n, stale / n
    # Win is good; a stalemate (e.g. the blink limit cycle) is a failure to close
    # out the fight and is penalised explicitly on top of the lost win.
    score = win_rate - stalemate_penalty * stale_rate
    return Fitness(
        score, win_rate, loss_rate, stale_rate, iters / n, n,
        own_deaths=own_d / n, enemy_deaths=enemy_d / n,
        own_damage=own_dmg / n, enemy_damage=enemy_dmg / n,
        runs=list(runs),
    )


# --------------------------------------------------------------------------- #
# Worker execution
# --------------------------------------------------------------------------- #
def _worker_reports(workdir: str) -> list:
    pattern = os.path.join(
        workdir, "data", "godot", "app_userdata", "*", "simulation_reports", "**", "*_report.json"
    )
    runs = []
    for path in glob.glob(pattern, recursive=True):
        with open(path) as fh:
            report = json.load(fh)
        for entry in report.get("profile_entries", []):
            runs.extend(entry.get("runs", []))
    return runs


def _run_one_worker(args) -> list:
    idx, scenario_res, profile_res, root, timeout = args
    workdir = tempfile.mkdtemp(prefix=f"bstuner_w{idx}_")
    try:
        env = dict(os.environ)
        for sub in ("data", "config", "cache"):
            os.makedirs(os.path.join(workdir, sub), exist_ok=True)
        env.update(
            HOME=workdir,
            XDG_DATA_HOME=os.path.join(workdir, "data"),
            XDG_CONFIG_HOME=os.path.join(workdir, "config"),
            XDG_CACHE_HOME=os.path.join(workdir, "cache"),
            TRACE_AI="false",
        )
        cmd = [
            GODOT_BIN, "--headless", "--audio-driver", "Dummy", "--path", root,
            "--script", BALANCE_RUNNER, "--", scenario_res, profile_res,
        ]
        with open(os.path.join(workdir, "out.log"), "w") as log:
            subprocess.run(cmd, env=env, cwd=root, stdout=log, stderr=subprocess.STDOUT,
                           timeout=timeout, check=False)
        return _worker_reports(workdir)
    finally:
        shutil.rmtree(workdir, ignore_errors=True)


def evaluate(
    genome: Mapping[str, float],
    specs: Sequence[ParamSpec],
    scenario_res: str,
    *,
    win_faction: str,
    workers: int = 8,
    profile_id: str = "candidate",
    stalemate_penalty: float = 0.5,
    timeout: float = 900.0,
    root: str = REPO_ROOT,
    record: bool = True,
) -> Fitness:
    """Render `genome`, run `scenario_res` across `workers` isolated processes, score it.

    Total run count = workers x (seeds in the scenario). The candidate profile is
    written once into the project (res://.tmp_tuner/) and shared by all workers.

    `record=False` skips the central-store append — used when a caller (e.g. the
    high-R gate) runs many waves of the same genome and records the merged result
    once instead of many low-n duplicate rows.
    """
    tmp_dir = os.path.join(root, ".tmp_tuner")
    os.makedirs(tmp_dir, exist_ok=True)
    profile_fname = f"{profile_id}.tres"
    with open(os.path.join(tmp_dir, profile_fname), "w") as fh:
        fh.write(render_profile_tres(profile_id, genome, specs))
    profile_res = f"res://.tmp_tuner/{profile_fname}"

    tasks = [(i, scenario_res, profile_res, root, timeout) for i in range(workers)]
    runs: list = []
    # Threads, not processes: each task only blocks on a `godot` subprocess (releasing
    # the GIL), so this is I/O-bound orchestration. A ProcessPoolExecutor would add a
    # useless Python-process layer and break on pickling / __main__ resolution.
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        for worker_runs in pool.map(_run_one_worker, tasks):
            runs.extend(worker_runs)
    fit = score_runs(runs, win_faction, stalemate_penalty)
    if record:
        record_sample(genome, specs, fit, scenario=scenario_res, win_faction=win_faction,
                      profile_id=profile_id)
    return fit


RUN_6V12 = "res://tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs"


def _run_6v12_worker(args) -> list:
    idx, profile_res, scenario_file, count, root, timeout = args
    workdir = tempfile.mkdtemp(prefix=f"bs6v12_w{idx}_")
    try:
        env = dict(os.environ)
        for sub in ("data", "config", "cache"):
            os.makedirs(os.path.join(workdir, sub), exist_ok=True)
        report_path = os.path.join(workdir, "report.json")
        env.update(
            HOME=workdir,
            XDG_DATA_HOME=os.path.join(workdir, "data"),
            XDG_CONFIG_HOME=os.path.join(workdir, "config"),
            XDG_CACHE_HOME=os.path.join(workdir, "cache"),
            TRACE_AI="false",
            PROGRESS="false",
            COUNT=str(count),
            AI_PROFILE_OVERRIDE_FILE=profile_res,
            OUTPUT_FILE=report_path,
        )
        if scenario_file:
            env["SCENARIO_FILE"] = scenario_file
        cmd = [
            GODOT_BIN, "--headless", "--audio-driver", "Dummy", "--path", root,
            "-s", RUN_6V12,
        ]
        with open(os.path.join(workdir, "out.log"), "w") as log:
            subprocess.run(cmd, env=env, cwd=root, stdout=log, stderr=subprocess.STDOUT,
                           timeout=timeout, check=False)
        if not os.path.exists(report_path):
            return []
        with open(report_path) as fh:
            return json.load(fh).get("runs", [])
    finally:
        shutil.rmtree(workdir, ignore_errors=True)


def evaluate_6v12(
    genome: Mapping[str, float],
    specs: Sequence[ParamSpec],
    *,
    win_faction: str,
    workers: int = 8,
    count_per_worker: int = 2,
    profile_id: str = "candidate",
    scenario_file: str = "",
    stalemate_penalty: float = 0.5,
    timeout: float = 1800.0,
    root: str = REPO_ROOT,
    record: bool = True,
) -> Fitness:
    """Evaluate a genome on the real 6v12 fixture via RunMixed6v12 (both sides AI).

    The genome should patch one faction's score profile (faction_ai_score_profile
    target_id=player|hostile); the other faction keeps the baseline weights, so
    win_rate is a genuine relative-strength signal. Total runs = workers x count.

    `record=False` skips the central-store append (used by the high-R gate, which
    pools many waves and records the merged result once).
    """
    tmp_dir = os.path.join(root, ".tmp_tuner")
    os.makedirs(tmp_dir, exist_ok=True)
    fname = f"{profile_id}.tres"
    with open(os.path.join(tmp_dir, fname), "w") as fh:
        fh.write(render_profile_tres(profile_id, genome, specs))
    profile_res = f"res://.tmp_tuner/{fname}"

    tasks = [
        (i, profile_res, scenario_file, count_per_worker, root, timeout)
        for i in range(workers)
    ]
    runs: list = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        for worker_runs in pool.map(_run_6v12_worker, tasks):
            runs.extend(worker_runs)
    fit = score_runs(runs, win_faction, stalemate_penalty)
    if record:
        record_sample(genome, specs, fit, scenario=scenario_file or RUN_6V12,
                      win_faction=win_faction, profile_id=profile_id)
    return fit


# Scenarios whose roster only BattleSimFormalCombatFixture.BuildRoster can build
# (the 6v12 / 2s1a family). The generic balance runner (used by `evaluate`) has no
# roster for these and spawns zero units -> a degenerate 25-idle-loop stalemate, so
# any genome eval on them must go through the 6v12 benchmark runner instead.
_FORMAL_FIXTURE_MARKERS = ("mixed_6v12", "two_archer", "mixed_2s1a")


def is_formal_fixture_scenario(scenario: str) -> bool:
    s = (scenario or "").lower()
    return any(m in s for m in _FORMAL_FIXTURE_MARKERS)


def evaluate_genome(
    genome: Mapping[str, float],
    specs: Sequence[ParamSpec],
    scenario: str,
    *,
    win_faction: str,
    workers: int = 8,
    count_per_worker: int = 2,
    profile_id: str = "candidate",
    stalemate_penalty: float = 0.5,
    timeout: float = 1800.0,
    root: str = REPO_ROOT,
    record: bool = True,
) -> Fitness:
    """Evaluate `genome` on `scenario`, auto-selecting the runner: the 6v12 benchmark
    runner for formal-fixture scenarios, the generic balance runner otherwise. This is
    what validate/gate callers should use so they never silently hit the empty-roster
    path. Total runs: formal -> workers*count_per_worker; balance -> workers*scenario_seeds."""
    if is_formal_fixture_scenario(scenario):
        return evaluate_6v12(
            genome, specs, win_faction=win_faction, workers=workers,
            count_per_worker=count_per_worker, profile_id=profile_id,
            scenario_file=scenario, stalemate_penalty=stalemate_penalty,
            timeout=timeout, root=root, record=record,
        )
    return evaluate(
        genome, specs, scenario, win_faction=win_faction, workers=workers,
        profile_id=profile_id, stalemate_penalty=stalemate_penalty,
        timeout=timeout, root=root, record=record,
    )


def evaluate_6v12_profile_res(
    profile_res: str,
    *,
    win_faction: str,
    workers: int = 8,
    count_per_worker: int = 2,
    scenario_file: str = "",
    stalemate_penalty: float = 0.5,
    timeout: float = 1800.0,
    root: str = REPO_ROOT,
) -> Fitness:
    """Evaluate an existing BattleSimProfileDef resource on the 6v12 runner."""
    tasks = [
        (i, profile_res, scenario_file, count_per_worker, root, timeout)
        for i in range(workers)
    ]
    runs: list = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        for worker_runs in pool.map(_run_6v12_worker, tasks):
            runs.extend(worker_runs)
    return score_runs(runs, win_faction, stalemate_penalty)


def evaluate_6v12_batch(
    genomes: Sequence[Mapping[str, float]],
    specs: Sequence[ParamSpec],
    *,
    win_faction: str,
    total_workers: int = 32,
    workers_per_candidate: int = 8,
    count_per_worker: int = 1,
    stalemate_penalty: float = 0.5,
    profile_prefix: str = "cand",
    scenario_file: str = "",
    timeout: float = 1800.0,
    root: str = REPO_ROOT,
) -> list[Fitness]:
    """Evaluate many genomes on 6v12 concurrently, capping total godot procs to
    ~total_workers (so a whole CMA generation runs across the machine at once).
    """
    outer = max(1, total_workers // max(1, workers_per_candidate))

    def _one(item):
        idx, genome = item
        return idx, evaluate_6v12(
            genome,
            specs,
            win_faction=win_faction,
            workers=workers_per_candidate,
            count_per_worker=count_per_worker,
            profile_id=f"{profile_prefix}_{idx}",
            scenario_file=scenario_file,
            stalemate_penalty=stalemate_penalty,
            timeout=timeout,
            root=root,
        )

    results: list = [None] * len(genomes)
    with concurrent.futures.ThreadPoolExecutor(max_workers=outer) as pool:
        for idx, fit in pool.map(_one, list(enumerate(genomes))):
            results[idx] = fit
    return results


def prewarm(root: str = REPO_ROOT, scenario_res: str | None = None) -> None:
    """Populate `.godot/imported` with a single non-parallel run before a batch."""
    cmd = [GODOT_BIN, "--headless", "--path", root, "--script", BALANCE_RUNNER]
    if scenario_res:
        cmd += ["--", scenario_res]
    subprocess.run(cmd, cwd=root, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)

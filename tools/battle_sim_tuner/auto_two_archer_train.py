"""Sample-count-driven active learning for the two-archer tuning scenario.

The loop grows the free search dimensions as the central dataset grows, while
using only real battle evaluations as the stopping signal.

Example:
    cd tools
    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.auto_two_archer_train \
        --target-win-rate 0.4 --rounds 12 --eval-top-k 4 --eval-workers 8
"""

from __future__ import annotations

import argparse
import json
import os
import random
import subprocess

from .evaluator import DATASET_PATH, REPO_ROOT, evaluate_6v12
from .export_score_profile import write_score_profile_tres
from .objective import DEFAULT_MAX_ITERATIONS
from .search_space import SCORE_DEFAULTS, score_weight_space

GPU_PYTHON_DEFAULT = "/home/luchaoli/venvs/cuda-op/bin/python"
SCENARIO = "res://data/configs/battle_sim/scenarios/mixed_6v12_two_archer.tres"
OBJECTIVE_WEIGHTS = {
    "win": 20.0,
    "loss": 8.0,
    "stalemate": 6.0,
    "net_kills": 0.25,
    "iterations": 0.1,
}

TWO_ARCHER_PARAM_ORDER = [
    # Outcome and target choice signals with direct gradient in the two-archer arena.
    "damage_weight",
    "lethal_target_weight",
    "lethal_threat_target_weight",
    "target_count_weight",
    "enemy_target_count_weight",
    "focus_fire_wounded_target_weight",
    "hit_rate_reliability_weight",
    "save_reliable_damage_weight",
    "height_weight",
    "terrain_weight",
    # Positioning and role-threat geometry.
    "position_base_score",
    "position_distance_step",
    "position_undershoot_penalty",
    "position_overshoot_penalty",
    "position_objective_weight",
    "safe_distance_adherence_weight",
    "role_threat_min_effective_range",
    "role_threat_distance_window",
    "role_threat_max_approach_distance",
    "role_threat_max_contact_range",
    "role_threat_in_range_score_step",
    "movement_cost_weight",
    # Action cost and multi-unit safety.
    "ap_cost_weight",
    "cooldown_weight",
    "stamina_cost_weight",
    "friendly_fire_damage_weight",
    "friendly_fire_target_weight",
    "friendly_control_target_weight",
    "friendly_lethal_target_weight",
    "overkill_damage_penalty_weight",
    "execute_target_hp_threshold_bp",
    "execute_bonus_weight",
    # Threat and survival signals.
    "survival_margin_gain_weight",
    "post_action_threat_damage_weight",
    "post_action_threat_count_weight",
    "lethal_survival_risk_penalty",
    "incoming_threat_relief_weight",
    "low_hp_urgency_threshold_bp",
    "low_hp_urgency_weight",
    "threat_healer_bias_basis_points",
    "threat_control_bias_basis_points",
    "threat_ranged_bias_basis_points",
    "threat_range_step_bias_basis_points",
    "threat_multiplier_cap_basis_points",
    "default_bucket_priority",
    # Secondary effects. Some are sparse here, but still combat-facing.
    "status_weight",
    "status_redundancy_penalty",
    "shield_absorbed_weight",
    "control_weight",
    "ground_control_weight",
    "chain_enemy_target_weight",
    # Resource reserve dimensions are deliberately later: they can easily create
    # no-action stalemates in this archer-heavy arena until enough samples exist.
    "stamina_reserve_floor_bp",
    "stamina_reserve_pressure_weight",
    "stamina_reserve_breach_penalty",
    "resource_conservation_weight",
    "mp_cost_weight",
    "mp_reserve_floor_bp",
    "mp_reserve_pressure_weight",
    "mp_reserve_breach_penalty",
    "aura_cost_weight",
    "aura_reserve_floor_bp",
    "aura_reserve_pressure_weight",
    "aura_reserve_breach_penalty",
    "heal_weight",
    # Meteor-specific fields are mostly irrelevant in the no-mage two-archer
    # roster, so keep them for late high-sample expansion only.
    "meteor_high_priority_threat_multiplier_bp",
    "meteor_high_priority_damage_hp_percent",
    "meteor_high_priority_target_priority_score",
    "meteor_top_threat_rank",
    "meteor_friendly_fire_soft_expected_hp_percent",
    "meteor_friendly_fire_hard_expected_hp_percent",
    "meteor_friendly_fire_hard_worst_case_hp_percent",
]


def _scenario_substr(scenario: str) -> str:
    return os.path.basename(scenario).split(".")[0] or scenario


def _has_full_fitness(row: dict) -> bool:
    fitness = row.get("fitness")
    if not isinstance(fitness, dict):
        return False
    required = ("win_rate", "loss_rate", "stalemate_rate", "avg_iterations", "net_kills", "n")
    return all(key in fitness for key in required)


def _fitness_n(row: dict) -> int:
    try:
        return int(row.get("fitness", {}).get("n", 0) or 0)
    except (TypeError, ValueError):
        return 0


def _is_wrong_runner_profile(row: dict) -> bool:
    profile_id = str(row.get("profile_id", "") or "")
    return profile_id.startswith("two_archer_auto_") or profile_id.startswith("two_archer_history_retest_")


def _target_objective(
    *,
    win_rate: float,
    loss_rate: float,
    stalemate_rate: float,
    avg_iterations: float,
    net_kills: float,
) -> float:
    """Training target for the 0.4-win-rate objective.

    The shared objective is useful for general score-profile tuning, but in this
    scenario it can over-reward "kills inside a stalemate". This target keeps
    win-rate dominant and treats stalemate as a real failure mode.
    """
    iteration_scale = max(float(DEFAULT_MAX_ITERATIONS), 1.0)
    return (
        OBJECTIVE_WEIGHTS["win"] * win_rate
        - OBJECTIVE_WEIGHTS["loss"] * loss_rate
        - OBJECTIVE_WEIGHTS["stalemate"] * stalemate_rate
        + OBJECTIVE_WEIGHTS["net_kills"] * net_kills
        - OBJECTIVE_WEIGHTS["iterations"] * avg_iterations / iteration_scale
    )


def _objective_from_fitness(fitness: dict) -> float:
    return _target_objective(
        win_rate=float(fitness["win_rate"]),
        loss_rate=float(fitness["loss_rate"]),
        stalemate_rate=float(fitness["stalemate_rate"]),
        avg_iterations=float(fitness["avg_iterations"]),
        net_kills=float(fitness["net_kills"]),
    )


def _objective_from_fit(fit) -> float:
    return _target_objective(
        win_rate=float(fit.win_rate),
        loss_rate=float(fit.loss_rate),
        stalemate_rate=float(fit.stalemate_rate),
        avg_iterations=float(fit.avg_iterations),
        net_kills=float(fit.net_kills),
    )


def _is_better_fit(fit, obj: float, *, best_win: float, best_obj: float) -> bool:
    if obj > best_obj + 1e-9:
        return True
    if abs(obj - best_obj) <= 1e-9 and fit.win_rate > best_win + 1e-9:
        return True
    return False


def _passes_target(
    fit,
    *,
    target_win_rate: float,
    target_max_loss_rate: float,
    target_max_stalemate_rate: float,
    min_n: int,
) -> bool:
    return (
        fit.n >= min_n
        and fit.win_rate >= target_win_rate
        and fit.loss_rate <= target_max_loss_rate
        and fit.stalemate_rate <= target_max_stalemate_rate
    )


def _count_rows(path: str, *, scenario: str, faction: str, min_n: int = 0) -> int:
    count = 0
    scenario_filter = _scenario_substr(scenario)
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if faction and row.get("faction") not in (None, faction):
                    continue
                if scenario_filter and scenario_filter not in str(row.get("scenario", "")):
                    continue
                if (
                    "genome" in row
                    and _has_full_fitness(row)
                    and _fitness_n(row) >= min_n
                    and not _is_wrong_runner_profile(row)
                ):
                    count += 1
    except FileNotFoundError:
        return 0
    return count


def _write_training_view(src: str, dst: str, *, scenario: str, faction: str, min_n: int = 0) -> int:
    scenario_filter = _scenario_substr(scenario)
    rows = 0
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(dst, "w", encoding="utf-8") as out:
        try:
            fh = open(src, encoding="utf-8")
        except FileNotFoundError:
            return 0
        with fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if faction and row.get("faction") not in (None, faction):
                    continue
                if scenario_filter and scenario_filter not in str(row.get("scenario", "")):
                    continue
                if (
                    "genome" not in row
                    or not _has_full_fitness(row)
                    or _fitness_n(row) < min_n
                    or _is_wrong_runner_profile(row)
                ):
                    continue
                record = {
                    "ts": row.get("ts"),
                    "scenario": row.get("scenario", scenario),
                    "faction": row.get("faction", faction),
                    "profile_id": row.get("profile_id", ""),
                    "genome": row["genome"],
                    "fitness": row["fitness"],
                    "objective": _objective_from_fitness(row["fitness"]),
                }
                out.write(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n")
                rows += 1
    return rows


def _complete_genome(genome: dict, specs) -> dict:
    return {spec.name: spec.clamp(genome.get(spec.name, SCORE_DEFAULTS[spec.name])) for spec in specs}


def _load_history_candidates(
    path: str,
    *,
    scenario: str,
    faction: str,
    specs,
    limit: int,
    min_n: int = 0,
    min_win_rate: float | None = None,
    max_loss_rate: float | None = None,
    max_stalemate_rate: float | None = None,
) -> list[dict]:
    if limit <= 0:
        return []
    scenario_filter = _scenario_substr(scenario)
    rows: list[dict] = []
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if faction and row.get("faction") not in (None, faction):
                    continue
                if scenario_filter and scenario_filter not in str(row.get("scenario", "")):
                    continue
                genome = row.get("genome")
                fitness = row.get("fitness")
                if not isinstance(genome, dict) or not isinstance(fitness, dict):
                    continue
                if not _has_full_fitness(row) or _fitness_n(row) < min_n or _is_wrong_runner_profile(row):
                    continue
                win_rate = float(fitness.get("win_rate", 0.0) or 0.0)
                loss_rate = float(fitness.get("loss_rate", 1.0) or 1.0)
                stalemate_rate = float(fitness.get("stalemate_rate", 1.0) or 1.0)
                if min_win_rate is not None and win_rate < min_win_rate:
                    continue
                if max_loss_rate is not None and loss_rate > max_loss_rate:
                    continue
                if max_stalemate_rate is not None and stalemate_rate > max_stalemate_rate:
                    continue
                rows.append(row)
    except FileNotFoundError:
        return []

    rows.sort(
        key=lambda row: (
            _objective_from_fitness(row["fitness"]),
            float(row.get("fitness", {}).get("win_rate", 0.0) or 0.0),
        ),
        reverse=True,
    )
    selected: list[dict] = []
    seen: set[tuple] = set()
    for row in rows:
        genome = _complete_genome(row["genome"], specs)
        key = _genome_key(genome, specs)
        if key in seen:
            continue
        seen.add(key)
        selected.append({"source_row": row, "genome": genome})
        if len(selected) >= limit:
            break
    return selected


def _free_param_names(rows: int, *, min_dims: int, max_dims: int, samples_per_dim: int, faction: str) -> list[str]:
    specs = score_weight_space(faction)
    spec_names = {spec.name for spec in specs}
    ordered_names = [name for name in TWO_ARCHER_PARAM_ORDER if name in spec_names]
    seen_order = set(ordered_names)
    ordered_names.extend(spec.name for spec in specs if spec.name not in seen_order)
    dim_cap = min(max_dims, len(specs))
    dims = max(min_dims, rows // max(1, samples_per_dim))
    dims = max(1, min(dim_cap, dims))
    return ordered_names[:dims]


def _mutated_fallback_entries(
    *,
    bases: list[dict],
    specs,
    free_names: list[str],
    limit: int,
    existing_keys: set[tuple],
    seed: int,
    radius_fraction: float,
    min_damage_weight: int,
    min_offense_pressure: float,
) -> list[dict]:
    if limit <= 0:
        return []
    spec_by_name = {spec.name: spec for spec in specs}
    rng = random.Random(seed)
    base_genomes = [_complete_genome(item["genome"], specs) for item in bases]
    if not base_genomes:
        base_genomes = [_complete_genome(dict(SCORE_DEFAULTS), specs)]

    entries: list[dict] = []
    attempts = 0
    max_attempts = max(64, limit * 64)
    while len(entries) < limit and attempts < max_attempts:
        attempts += 1
        base = dict(base_genomes[attempts % len(base_genomes)])
        genome = dict(base)
        for name in free_names:
            spec = spec_by_name.get(name)
            if spec is None:
                continue
            span = max(float(spec.hi - spec.lo), 1.0)
            delta = rng.gauss(0.0, radius_fraction * span)
            genome[name] = spec.clamp(float(genome.get(name, SCORE_DEFAULTS[name])) + delta)
        if not _passes_eval_guard(
            genome,
            min_damage_weight=min_damage_weight,
            min_offense_pressure=min_offense_pressure,
        ):
            continue
        key = _genome_key(genome, specs)
        if key in existing_keys:
            continue
        existing_keys.add(key)
        entries.append(
            {
                "acq": None,
                "pred_mean": None,
                "pred_std": None,
                "genome": genome,
                "source": "fallback_mutation",
            }
        )
    return entries


def _write_json(path: str, payload: dict) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2, sort_keys=True)


def _fmt_score(value) -> str:
    if value is None:
        return "n/a"
    return f"{float(value):+.3f}"


def _genome_key(genome: dict, specs) -> tuple:
    return tuple((spec.name, genome.get(spec.name)) for spec in specs)


def _offense_pressure(genome: dict) -> float:
    return (
        float(genome.get("damage_weight", 0))
        + float(genome.get("target_count_weight", 0)) / 8.0
        + float(genome.get("enemy_target_count_weight", 0)) / 8.0
        + float(genome.get("lethal_target_weight", 0)) / 200.0
        + float(genome.get("lethal_threat_target_weight", 0)) / 200.0
        + float(genome.get("focus_fire_wounded_target_weight", 0)) / 50.0
        + float(genome.get("execute_bonus_weight", 0)) / 250.0
    )


def _passes_eval_guard(genome: dict, *, min_damage_weight: int, min_offense_pressure: float) -> bool:
    if int(genome.get("damage_weight", 0) or 0) < min_damage_weight:
        return False
    return _offense_pressure(genome) >= min_offense_pressure


def _unique_ranked(
    ranked: list[dict],
    *,
    limit: int,
    specs,
    min_damage_weight: int,
    min_offense_pressure: float,
) -> tuple[list[dict], int]:
    selected: list[dict] = []
    seen: set[tuple] = set()
    guarded = 0
    for entry in ranked:
        genome = entry.get("genome")
        if not isinstance(genome, dict):
            continue
        if not _passes_eval_guard(
            genome,
            min_damage_weight=min_damage_weight,
            min_offense_pressure=min_offense_pressure,
        ):
            guarded += 1
            continue
        key = _genome_key(genome, specs)
        if key in seen:
            continue
        seen.add(key)
        selected.append(entry)
        if len(selected) >= limit:
            break
    return selected, guarded


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--scenario", default=SCENARIO)
    p.add_argument("--faction", default="player")
    p.add_argument("--dataset", default=DATASET_PATH)
    p.add_argument("--output-dir", default=os.path.join(REPO_ROOT, ".tmp_tuner", "two_archer_auto_train"))
    p.add_argument("--target-win-rate", type=float, default=0.4)
    p.add_argument("--target-max-loss-rate", type=float, default=1.0)
    p.add_argument("--target-max-stalemate-rate", type=float, default=1.0)
    p.add_argument("--rounds", type=int, default=12)
    p.add_argument(
        "--min-full-dim-rounds",
        type=int,
        default=0,
        help="Continue beyond --rounds until this many rounds have searched all score dimensions.",
    )
    p.add_argument("--eval-top-k", type=int, default=4)
    p.add_argument("--eval-workers", type=int, default=8)
    p.add_argument("--eval-count-per-worker", type=int, default=5)
    p.add_argument("--confirm-workers", type=int, default=16)
    p.add_argument("--confirm-count-per-worker", type=int, default=5)
    p.add_argument("--min-training-n", type=int, default=20)
    p.add_argument("--history-retest-top-k", type=int, default=4)
    p.add_argument("--eval-timeout", type=float, default=1200.0)
    p.add_argument("--min-dims", type=int, default=24)
    p.add_argument("--samples-per-dim", type=int, default=3)
    p.add_argument("--max-dims", type=int, default=10_000)
    p.add_argument("--gpu-python", default=GPU_PYTHON_DEFAULT)
    p.add_argument("--ensemble-size", type=int, default=8)
    p.add_argument("--kappa", type=float, default=1.0)
    p.add_argument("--cma-popsize", type=int, default=96)
    p.add_argument("--cma-generations", type=int, default=180)
    p.add_argument("--restarts", type=int, default=2)
    p.add_argument("--sigma0", type=float, default=0.05)
    p.add_argument("--polish-steps", type=int, default=180)
    p.add_argument("--min-damage-weight-for-eval", type=int, default=5)
    p.add_argument("--min-offense-pressure-for-eval", type=float, default=10.0)
    p.add_argument(
        "--ranked-multiplier",
        type=int,
        default=8,
        help="Ask gpu_search for eval_top_k * multiplier candidates, then exact-dedupe genomes.",
    )
    p.add_argument("--fallback-base-top-k", type=int, default=8)
    p.add_argument("--fallback-radius-fraction", type=float, default=0.10)
    p.add_argument("--force-fallback-only", action="store_true")
    p.add_argument("--objective-win-weight", type=float, default=OBJECTIVE_WEIGHTS["win"])
    p.add_argument("--objective-loss-weight", type=float, default=OBJECTIVE_WEIGHTS["loss"])
    p.add_argument("--objective-stalemate-weight", type=float, default=OBJECTIVE_WEIGHTS["stalemate"])
    p.add_argument("--objective-net-kills-weight", type=float, default=OBJECTIVE_WEIGHTS["net_kills"])
    p.add_argument("--objective-iteration-weight", type=float, default=OBJECTIVE_WEIGHTS["iterations"])
    args = p.parse_args()

    OBJECTIVE_WEIGHTS.update(
        {
            "win": args.objective_win_weight,
            "loss": args.objective_loss_weight,
            "stalemate": args.objective_stalemate_weight,
            "net_kills": args.objective_net_kills_weight,
            "iterations": args.objective_iteration_weight,
        }
    )

    specs = score_weight_space(args.faction)
    os.makedirs(args.output_dir, exist_ok=True)
    training_view = os.path.join(args.output_dir, "training_samples.full_fitness.jsonl")

    best_obj = float("-inf")
    best_win = float("-inf")
    best_genome: dict | None = None
    best_fit_payload: dict | None = None
    confirmed = False

    print(
        "two-archer auto train: "
        f"target_win={args.target_win_rate:.2f} "
        f"target_loss<={args.target_max_loss_rate:.2f} "
        f"target_stale<={args.target_max_stalemate_rate:.2f} "
        f"objective_weights={OBJECTIVE_WEIGHTS} "
        f"scenario={args.scenario} faction={args.faction}",
        flush=True,
    )

    history_candidates = _load_history_candidates(
        args.dataset,
        scenario=args.scenario,
        faction=args.faction,
        specs=specs,
        limit=max(0, args.history_retest_top_k),
        min_n=args.min_training_n,
    )
    if history_candidates:
        print(
            f"\n=== history retest: top {len(history_candidates)} previous high-win genomes "
            f"with {args.confirm_workers} workers ===",
            flush=True,
        )
    for idx, item in enumerate(history_candidates):
        source = item["source_row"]
        genome = item["genome"]
        src_fit = source.get("fitness", {})
        print(
            f"  history {idx}: source_win={float(src_fit.get('win_rate', 0.0) or 0.0):.3f} "
            f"source_n={int(src_fit.get('n', 0) or 0)}",
            flush=True,
        )
        fit = evaluate_6v12(
            genome,
            specs,
            win_faction=args.faction,
            workers=args.confirm_workers,
            count_per_worker=args.confirm_count_per_worker,
            profile_id=f"two_archer_6v12_history_retest_{idx}",
            scenario_file=args.scenario,
            timeout=args.eval_timeout,
        )
        obj = _objective_from_fit(fit)
        print(
            f"    retest: obj={obj:+.3f} win={fit.win_rate:.3f} "
            f"loss={fit.loss_rate:.3f} stale={fit.stalemate_rate:.3f} n={fit.n}",
            flush=True,
        )
        if _is_better_fit(fit, obj, best_win=best_win, best_obj=best_obj):
            best_obj = obj
            best_win = fit.win_rate
            best_genome = genome
            best_fit_payload = {
                "objective": obj,
                "win_rate": fit.win_rate,
                "loss_rate": fit.loss_rate,
                "stalemate_rate": fit.stalemate_rate,
                "avg_iterations": fit.avg_iterations,
                "net_kills": fit.net_kills,
                "n": fit.n,
                "source": "history_retest",
                "history_index": idx,
            }
            _write_json(os.path.join(args.output_dir, "best_genome.json"), best_genome)
            _write_json(os.path.join(args.output_dir, "best_real_fitness.json"), best_fit_payload)
            write_score_profile_tres(os.path.join(args.output_dir, "best_score_profile.tres"), best_genome)
        if _passes_target(
            fit,
            target_win_rate=args.target_win_rate,
            target_max_loss_rate=args.target_max_loss_rate,
            target_max_stalemate_rate=args.target_max_stalemate_rate,
            min_n=20,
        ):
            confirmed = True
            best_obj = obj
            best_win = fit.win_rate
            best_genome = genome
            best_fit_payload = {
                "objective": obj,
                "win_rate": fit.win_rate,
                "loss_rate": fit.loss_rate,
                "stalemate_rate": fit.stalemate_rate,
                "avg_iterations": fit.avg_iterations,
                "net_kills": fit.net_kills,
                "n": fit.n,
                "source": "history_retest",
                "history_index": idx,
                "confirmed": True,
            }
            _write_json(os.path.join(args.output_dir, "champion_genome.json"), best_genome)
            _write_json(os.path.join(args.output_dir, "champion_fitness.json"), best_fit_payload)
            write_score_profile_tres(os.path.join(args.output_dir, "champion_score_profile.tres"), best_genome)
            break

    full_dim_rounds = 0
    rnd = 0
    while not confirmed:
        if rnd >= max(1, args.rounds) and full_dim_rounds >= max(0, args.min_full_dim_rounds):
            break
        rows = _write_training_view(
            args.dataset,
            training_view,
            scenario=args.scenario,
            faction=args.faction,
            min_n=args.min_training_n,
        )
        free_names = _free_param_names(
            rows,
            min_dims=args.min_dims,
            max_dims=args.max_dims,
            samples_per_dim=args.samples_per_dim,
            faction=args.faction,
        )
        round_dir = os.path.join(args.output_dir, f"round_{rnd:02d}_rows_{rows}_dims_{len(free_names)}")
        os.makedirs(round_dir, exist_ok=True)
        base_path = ""
        if best_genome is not None:
            base_path = os.path.join(round_dir, "base_genome.json")
            _write_json(base_path, best_genome)

        print(
            f"\n=== round {rnd}: rows={rows} free_dims={len(free_names)}/{len(specs)} "
            f"eval_top_k={args.eval_top_k} full_dim_rounds={full_dim_rounds} ===",
            flush=True,
        )
        is_full_dim_round = len(free_names) >= len(specs)
        cmd = [
            args.gpu_python,
            "-m",
            "battle_sim_tuner.gpu_search",
            "--observations",
            training_view,
            "--scenario",
            _scenario_substr(args.scenario),
            "--faction",
            args.faction,
            "--free-params",
            ",".join(free_names),
            "--ensemble-size",
            str(args.ensemble_size),
            "--kappa",
            str(args.kappa),
            "--cma-popsize",
            str(args.cma_popsize),
            "--cma-generations",
            str(args.cma_generations),
            "--restarts",
            str(args.restarts),
            "--sigma0",
            str(args.sigma0),
            "--polish-steps",
            str(args.polish_steps),
            "--top-k",
            str(max(args.eval_top_k, args.eval_top_k * max(1, args.ranked_multiplier))),
            "--output-dir",
            round_dir,
        ]
        if base_path:
            cmd += ["--base", base_path]
        rc = subprocess.run(cmd, cwd=os.path.join(REPO_ROOT, "tools")).returncode
        ranked_path = os.path.join(round_dir, "ranked.json")
        if rc != 0 or not os.path.exists(ranked_path):
            raise SystemExit(f"gpu_search failed in round {rnd} (rc={rc})")

        with open(ranked_path, encoding="utf-8") as fh:
            ranked = json.load(fh)
        if args.force_fallback_only:
            candidates = []
            guarded = len(ranked)
        else:
            candidates, guarded = _unique_ranked(
                ranked,
                limit=args.eval_top_k,
                specs=specs,
                min_damage_weight=args.min_damage_weight_for_eval,
                min_offense_pressure=args.min_offense_pressure_for_eval,
            )
        if guarded:
            print(f"  eval guard skipped {guarded} low-offense ranked candidates", flush=True)
        if len(candidates) < args.eval_top_k:
            fallback_bases = _load_history_candidates(
                args.dataset,
                scenario=args.scenario,
                faction=args.faction,
                specs=specs,
                limit=max(0, args.fallback_base_top_k),
                min_n=args.min_training_n,
                min_win_rate=max(0.0, args.target_win_rate - 0.08),
                max_loss_rate=min(1.0, args.target_max_loss_rate + 0.05),
                max_stalemate_rate=min(1.0, args.target_max_stalemate_rate + 0.10),
            )
            if len(fallback_bases) < max(1, args.eval_top_k - len(candidates)):
                fallback_bases = _load_history_candidates(
                    args.dataset,
                    scenario=args.scenario,
                    faction=args.faction,
                    specs=specs,
                    limit=max(0, args.fallback_base_top_k),
                    min_n=args.min_training_n,
                )
            existing_keys = {_genome_key(dict(entry["genome"]), specs) for entry in candidates}
            fallback = _mutated_fallback_entries(
                bases=fallback_bases,
                specs=specs,
                free_names=free_names,
                limit=args.eval_top_k - len(candidates),
                existing_keys=existing_keys,
                seed=(rnd + 1) * 1009 + rows,
                radius_fraction=args.fallback_radius_fraction,
                min_damage_weight=args.min_damage_weight_for_eval,
                min_offense_pressure=args.min_offense_pressure_for_eval,
            )
            if fallback:
                print(f"  added {len(fallback)} fallback mutation candidates", flush=True)
                candidates.extend(fallback)
        if len(candidates) < args.eval_top_k:
            print(
                f"  warning: only {len(candidates)} unique genomes in ranked top {len(ranked)}",
                flush=True,
            )

        for idx, entry in enumerate(candidates):
            genome = dict(entry["genome"])
            fit = evaluate_6v12(
                genome,
                specs,
                win_faction=args.faction,
                workers=args.eval_workers,
                count_per_worker=args.eval_count_per_worker,
                profile_id=f"two_archer_6v12_auto_r{rnd}_c{idx}",
                scenario_file=args.scenario,
                timeout=args.eval_timeout,
            )
            obj = _objective_from_fit(fit)
            print(
                f"  cand {idx}: acq={_fmt_score(entry.get('acq'))} "
                f"pred_mean={_fmt_score(entry.get('pred_mean'))} pred_std={_fmt_score(entry.get('pred_std'))} "
                f"-> REAL obj={obj:+.3f} win={fit.win_rate:.3f} "
                f"loss={fit.loss_rate:.3f} stale={fit.stalemate_rate:.3f} n={fit.n}",
                flush=True,
            )
            if _is_better_fit(fit, obj, best_win=best_win, best_obj=best_obj):
                best_obj = obj
                best_win = fit.win_rate
                best_genome = genome
                best_fit_payload = {
                    "objective": obj,
                    "win_rate": fit.win_rate,
                    "loss_rate": fit.loss_rate,
                    "stalemate_rate": fit.stalemate_rate,
                    "avg_iterations": fit.avg_iterations,
                    "net_kills": fit.net_kills,
                    "n": fit.n,
                    "round": rnd,
                    "candidate_index": idx,
                    "free_dims": len(free_names),
                    "dataset_rows_before_round": rows,
                }
                _write_json(os.path.join(args.output_dir, "best_genome.json"), best_genome)
                _write_json(os.path.join(args.output_dir, "best_real_fitness.json"), best_fit_payload)
                write_score_profile_tres(os.path.join(args.output_dir, "best_score_profile.tres"), best_genome)

            if fit.win_rate >= args.target_win_rate and fit.n >= 20:
                print(
                    f"  target win crossed by candidate {idx}; confirming with {args.confirm_workers} workers.",
                    flush=True,
                )
                confirm = evaluate_6v12(
                    genome,
                    specs,
                    win_faction=args.faction,
                    workers=args.confirm_workers,
                    count_per_worker=args.confirm_count_per_worker,
                    profile_id=f"two_archer_6v12_auto_confirm_r{rnd}_c{idx}",
                    scenario_file=args.scenario,
                    timeout=args.eval_timeout,
                )
                confirm_obj = _objective_from_fit(confirm)
                print(
                    f"  confirm: obj={confirm_obj:+.3f} win={confirm.win_rate:.3f} "
                    f"loss={confirm.loss_rate:.3f} stale={confirm.stalemate_rate:.3f} n={confirm.n}",
                    flush=True,
                )
                if _passes_target(
                    confirm,
                    target_win_rate=args.target_win_rate,
                    target_max_loss_rate=args.target_max_loss_rate,
                    target_max_stalemate_rate=args.target_max_stalemate_rate,
                    min_n=20,
                ):
                    best_obj = confirm_obj
                    best_win = confirm.win_rate
                    best_genome = genome
                    best_fit_payload = {
                        "objective": confirm_obj,
                        "win_rate": confirm.win_rate,
                        "loss_rate": confirm.loss_rate,
                        "stalemate_rate": confirm.stalemate_rate,
                        "avg_iterations": confirm.avg_iterations,
                        "net_kills": confirm.net_kills,
                        "n": confirm.n,
                        "round": rnd,
                        "candidate_index": idx,
                        "free_dims": len(free_names),
                        "dataset_rows_before_round": rows,
                        "confirmed": True,
                    }
                    confirmed = True
                    _write_json(os.path.join(args.output_dir, "champion_genome.json"), best_genome)
                    _write_json(os.path.join(args.output_dir, "champion_fitness.json"), best_fit_payload)
                    write_score_profile_tres(
                        os.path.join(args.output_dir, "champion_score_profile.tres"),
                        best_genome,
                    )
                    break
        if confirmed:
            break
        if is_full_dim_round:
            full_dim_rounds += 1
        rnd += 1

    if best_genome is not None and not confirmed:
        _write_json(os.path.join(args.output_dir, "best_genome.json"), best_genome)
        if best_fit_payload is not None:
            _write_json(os.path.join(args.output_dir, "best_real_fitness.json"), best_fit_payload)
        write_score_profile_tres(os.path.join(args.output_dir, "best_score_profile.tres"), best_genome)

    rows_after = _count_rows(
        args.dataset,
        scenario=args.scenario,
        faction=args.faction,
        min_n=args.min_training_n,
    )
    print(
        f"\nfinished: confirmed={confirmed} best_win={best_win:.3f} best_obj={best_obj:+.3f} "
        f"dataset_rows={rows_after} full_dim_rounds={full_dim_rounds}",
        flush=True,
    )
    if confirmed:
        print(f"champion: {os.path.join(args.output_dir, 'champion_score_profile.tres')}", flush=True)
    elif best_genome is not None:
        print(f"best_so_far: {os.path.join(args.output_dir, 'best_score_profile.tres')}", flush=True)
    raise SystemExit(0 if confirmed else 2)


if __name__ == "__main__":
    main()

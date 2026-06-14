"""Run the evolved champion weights vs the shipped default weights on the REAL
6v12 baseline (the mage-containing mirror), to see what the two_archer-tuned
score profile does to the mage when a mage is actually present.

Both profiles patch faction_ai_score_profile target_id=player; the hostile side
keeps baseline weights, so win_rate is a genuine relative-strength signal.
"""

from __future__ import annotations

import concurrent.futures

from .evaluator import _run_6v12_worker, score_runs, REPO_ROOT

# The real, immutable 6v12 baseline (4 sword + 1 archer + 1 mage vs 6 sword + 6 archer).
SCENARIO = "res://data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres"
FACTION = "player"
MAX_ITER = 3000  # baseline iteration cap
WORKERS = 32
COUNT = 1
TIMEOUT = 1800.0


def run_profile(profile_res: str):
    tasks = [(i, profile_res, SCENARIO, COUNT, REPO_ROOT, TIMEOUT) for i in range(WORKERS)]
    runs = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=WORKERS) as pool:
        for worker_runs in pool.map(_run_6v12_worker, tasks):
            runs.extend(worker_runs)
    return score_runs(runs, FACTION, stalemate_penalty=0.5)


def objective(f) -> float:
    return f.net_kills + 2.0 * f.win_rate - f.avg_iterations / MAX_ITER


def main():
    print(f"真·6v12 基线(含法师) 对照: champion vs default, 各 {WORKERS} x {COUNT}\n", flush=True)
    base = run_profile("res://.tmp_tuner/p6_baseline.tres")
    print(f"  default(默认权重): {base}  obj={objective(base):+.3f}", flush=True)
    champ = run_profile("res://.tmp_tuner/p6_champion.tres")
    print(f"  champion(two_archer进化): {champ}  obj={objective(champ):+.3f}", flush=True)
    print(f"\n  Δwin = {champ.win_rate - base.win_rate:+.2f}   "
          f"Δnet_kills = {champ.net_kills - base.net_kills:+.1f}   "
          f"Δavg_iter = {champ.avg_iterations - base.avg_iterations:+.0f}   "
          f"Δobj = {objective(champ) - objective(base):+.3f}", flush=True)


if __name__ == "__main__":
    main()

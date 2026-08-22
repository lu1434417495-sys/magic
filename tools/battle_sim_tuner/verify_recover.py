"""Recover the lost final champion-vs-baseline numbers from the interrupted
tune_6v12 run, WITHOUT re-running the 8 generations.

Reuses the existing rendered profiles in res://.tmp_tuner/profiles/ (p6_champion.json,
p6_baseline.json) and runs each across N isolated godot workers on the same
two_archer scenario, then prints the same aggregates tune_6v12 would have.
"""

from __future__ import annotations

from .evaluator import evaluate_6v12_profile_json
from .objective import score_fitness

SCENARIO = "mixed_6v12_two_archer"
FACTION = "player"
MAX_ITER = 1500
WORKERS = 32
COUNT = 1
TIMEOUT = 1800.0


def run_profile(profile_json: str):
    return evaluate_6v12_profile_json(
        profile_json,
        win_faction=FACTION,
        workers=WORKERS,
        count_per_worker=COUNT,
        scenario_id=SCENARIO,
        stalemate_penalty=0.5,
        timeout=TIMEOUT,
    )


def objective(f) -> float:
    return score_fitness(f, MAX_ITER)


def main():
    print(f"恢复复核: champion vs baseline, 各 {WORKERS} workers x {COUNT}, scenario=two_archer\n", flush=True)
    base = run_profile("res://.tmp_tuner/profiles/p6_baseline.json")
    print(f"  baseline(默认权重): {base}  obj={objective(base):+.3f}", flush=True)
    champ = run_profile("res://.tmp_tuner/profiles/p6_champion.json")
    print(f"  champion(进化权重): {champ}  obj={objective(champ):+.3f}", flush=True)
    print(f"\n  Δobj = {objective(champ) - objective(base):+.3f}", flush=True)
    print(f"  Δwin = {champ.win_rate - base.win_rate:+.2f}   "
          f"Δnet_kills = {champ.net_kills - base.net_kills:+.1f}   "
          f"Δavg_iter = {champ.avg_iterations - base.avg_iterations:+.0f}", flush=True)


if __name__ == "__main__":
    main()

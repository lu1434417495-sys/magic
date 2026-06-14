"""Proper 35-dim CMA-ES tune of one faction's score profile on the real 6v12.

Uses the full machine: each CMA generation's candidates are evaluated concurrently
(evaluate_6v12_batch caps total godot procs ~total_workers). Starts from the shipped
weight defaults (already tuned) and searches their neighbourhood. Objective: win-rate
first, then win faster (lower avg_iter). Finishes with a high-run-count comparison of
the evolved champion vs the default-weight baseline so the delta is not noise.

Run: tools/battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.tune_6v12   (from tools/)
"""

from __future__ import annotations

import cma

from .evaluator import Fitness, evaluate_6v12, evaluate_6v12_batch
from .search_space import SCORE_DEFAULTS, score_weight_space

SCENARIO = "res://data/configs/battle_sim/scenarios/mixed_6v12_two_archer.tres"
FACTION = "player"           # tune the elite squad; the 12 hostiles keep baseline weights
MAX_ITER = 1500              # two-archer arena iteration cap (matches the .tres)
TOTAL_WORKERS = 32
WORKERS_PER_CANDIDATE = 8    # -> 4 candidates evaluated concurrently (4 x 8 = 32 procs)
POPSIZE = 8
GENERATIONS = 8
FINAL_RUNS_WORKERS = 32      # high-count re-eval for champion vs baseline
SIGMA0 = 0.2


def objective(f: Fitness) -> float:
    # Continuous, low-variance: net unit advantage (enemy deaths - own deaths) is the
    # main signal — far less noisy per run than a binary win on a stalemate-heavy arena.
    # Small bonuses nudge toward actually winning and resolving faster.
    return f.net_kills + 2.0 * f.win_rate - f.avg_iterations / MAX_ITER


def main():
    specs = score_weight_space(FACTION)
    los = [s.lo for s in specs]
    his = [s.hi for s in specs]
    x0 = [float(SCORE_DEFAULTS[s.name]) for s in specs]

    es = cma.CMAEvolutionStrategy(
        x0,
        SIGMA0,  # search around the tuned defaults
        {
            "bounds": [los, his],
            "CMA_stds": [s.hi - s.lo for s in specs],
            "popsize": POPSIZE,
            "maxiter": GENERATIONS,
            "seed": 0,
            "verbose": -9,
        },
    )

    print(
        f"6v12 正式 35维优化: 调 {FACTION} {len(specs)} 权重 vs 对手基线; "
        f"起点=shipped 默认; 目标=胜率+更快; {TOTAL_WORKERS} 线程并发\n",
        flush=True,
    )

    gen = 0
    while not es.stop():
        solutions = es.ask()
        genomes = [
            {s.name: s.clamp(v) for s, v in zip(specs, x)} for x in solutions
        ]
        fits = evaluate_6v12_batch(
            genomes,
            specs,
            win_faction=FACTION,
            total_workers=TOTAL_WORKERS,
            workers_per_candidate=WORKERS_PER_CANDIDATE,
            count_per_worker=1,
            profile_prefix=f"p6_{gen}",
            scenario_file=SCENARIO,
        )
        es.tell(solutions, [-objective(f) for f in fits])
        best = max(fits, key=objective)
        print(
            f"gen {gen}: gen-best win={best.win_rate:.2f} net_kills={best.net_kills:+.1f} "
            f"avg_iter={best.avg_iterations:.0f} obj={objective(best):+.3f} "
            f"| overall-best obj={-es.best.f:+.3f}",
            flush=True,
        )
        gen += 1

    champion = {s.name: s.clamp(v) for s, v in zip(specs, es.result.xbest)}
    baseline = dict(SCORE_DEFAULTS)

    print("\n高样本复核 (各 24 局):", flush=True)
    champ_fit = evaluate_6v12(
        champion, specs, win_faction=FACTION, workers=FINAL_RUNS_WORKERS,
        count_per_worker=1, profile_id="p6_champion", scenario_file=SCENARIO,
    )
    base_fit = evaluate_6v12(
        baseline, specs, win_faction=FACTION, workers=FINAL_RUNS_WORKERS,
        count_per_worker=1, profile_id="p6_baseline", scenario_file=SCENARIO,
    )
    print(f"  baseline(默认权重): {base_fit}", flush=True)
    print(f"  champion(进化权重): {champ_fit}", flush=True)
    print(f"  Δobj = {objective(champ_fit) - objective(base_fit):+.3f}", flush=True)

    changed = {
        s.name: (baseline[s.name], champion[s.name])
        for s in specs
        if champion[s.name] != baseline[s.name]
    }
    print(f"\n改动的权重 ({len(changed)}/{len(specs)}):", flush=True)
    for name, (b, c) in sorted(changed.items()):
        print(f"  {name}: {b} -> {c}", flush=True)


if __name__ == "__main__":
    main()

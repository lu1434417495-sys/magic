"""CMA-ES tune of one faction's score profile on the real 6v12.

Uses the full machine: each CMA generation's candidates are evaluated concurrently
(evaluate_6v12_batch caps total godot procs ~total_workers). Starts from the shipped
weight defaults (already tuned) and searches their neighbourhood. Objective: win-rate
first, then win faster (lower avg_iter). Finishes with a high-run-count comparison of
the evolved champion vs the default-weight baseline so the delta is not noise.

Run: /home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.tune_6v12   (from tools/)
"""

from __future__ import annotations

import argparse
import json
import os

from .evaluator import Fitness, REPO_ROOT, evaluate_6v12, evaluate_6v12_batch
from .export_score_profile import write_score_profile_tres
from .gpu_surrogate import require_cuda
from .objective import FORMULA as OBJECTIVE_FORMULA
from .objective import score_fitness
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
    return score_fitness(f, MAX_ITER)


def main():
    parser = argparse.ArgumentParser(
        description="Run mandatory-CUDA CMA-ES tuning for the mixed 6v12 score profile."
    )
    parser.parse_args()

    import cma

    device_name = require_cuda()
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
        f"6v12 正式 score-profile 优化: 调 {FACTION} {len(specs)} 个权重 vs 对手基线; "
        f"起点=shipped 默认; 目标=胜率/败率/僵局+净击杀; {TOTAL_WORKERS} 线程并发; "
        f"GPU={device_name}\n",
        flush=True,
    )
    output_dir = os.path.join(REPO_ROOT, ".tmp_tuner")
    os.makedirs(output_dir, exist_ok=True)
    observations_path = os.path.join(output_dir, "p6_observations.jsonl")

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
        with open(observations_path, "a", encoding="utf-8") as obs:
            for idx, (genome, fit) in enumerate(zip(genomes, fits)):
                record = {
                    "generation": gen,
                    "candidate_index": idx,
                    "scenario": SCENARIO,
                    "faction": FACTION,
                    "genome": genome,
                    "objective": objective(fit),
                    "fitness": {
                        "score": fit.score,
                        "win_rate": fit.win_rate,
                        "loss_rate": fit.loss_rate,
                        "stalemate_rate": fit.stalemate_rate,
                        "avg_iterations": fit.avg_iterations,
                        "own_deaths": fit.own_deaths,
                        "enemy_deaths": fit.enemy_deaths,
                        "own_damage": fit.own_damage,
                        "enemy_damage": fit.enemy_damage,
                        "n": fit.n,
                    },
                }
                obs.write(json.dumps(record, sort_keys=True) + "\n")
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

    result_path = os.path.join(output_dir, "p6_champion_result.json")
    score_profile_path = os.path.join(output_dir, "p6_champion_score_profile.tres")
    with open(result_path, "w", encoding="utf-8") as fh:
        json.dump(
            {
                "scenario": SCENARIO,
                "faction": FACTION,
                "objective": OBJECTIVE_FORMULA,
                "max_iterations": MAX_ITER,
                "observations_jsonl": observations_path,
                "champion": champion,
                "baseline": baseline,
                "changed": changed,
                "champion_fitness": {
                    "score": champ_fit.score,
                    "win_rate": champ_fit.win_rate,
                    "loss_rate": champ_fit.loss_rate,
                    "stalemate_rate": champ_fit.stalemate_rate,
                    "avg_iterations": champ_fit.avg_iterations,
                    "own_deaths": champ_fit.own_deaths,
                    "enemy_deaths": champ_fit.enemy_deaths,
                    "own_damage": champ_fit.own_damage,
                    "enemy_damage": champ_fit.enemy_damage,
                    "n": champ_fit.n,
                },
                "baseline_fitness": {
                    "score": base_fit.score,
                    "win_rate": base_fit.win_rate,
                    "loss_rate": base_fit.loss_rate,
                    "stalemate_rate": base_fit.stalemate_rate,
                    "avg_iterations": base_fit.avg_iterations,
                    "own_deaths": base_fit.own_deaths,
                    "enemy_deaths": base_fit.enemy_deaths,
                    "own_damage": base_fit.own_damage,
                    "enemy_damage": base_fit.enemy_damage,
                    "n": base_fit.n,
                },
            },
            fh,
            indent=2,
            sort_keys=True,
        )
    write_score_profile_tres(score_profile_path, champion)
    print(f"\n写出训练产物:", flush=True)
    print(f"  result_json: {result_path}", flush=True)
    print(f"  observations_jsonl: {observations_path}", flush=True)
    print(f"  score_profile_tres: {score_profile_path}", flush=True)


if __name__ == "__main__":
    main()

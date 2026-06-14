"""Run one real CPU-sim-to-GPU-surrogate bridge sample.

This is a short end-to-end proof run:
  1. evaluate a few real 6v12 BattleAiScoreProfile candidates through Godot,
  2. train the mandatory CUDA surrogate from those real observations,
  3. rank a large candidate pool on GPU,
  4. export the top-ranked BattleAiScoreProfile .tres.

It is intentionally small and noisy. Use it to validate the pipeline wiring, not
to make balance conclusions.
"""

from __future__ import annotations

import argparse
import json
import os
import random
from typing import Any, Mapping, Sequence

from .evaluator import Fitness, REPO_ROOT, evaluate_6v12_batch
from .export_score_profile import write_score_profile_tres
from .gpu_surrogate import rank_candidates, require_cuda, train_surrogate
from .objective import DEFAULT_MAX_ITERATIONS as MAX_ITER
from .objective import FORMULA as OBJECTIVE_FORMULA
from .objective import score_fitness
from .search_space import SCORE_DEFAULTS, score_weight_space

SCENARIO = "res://data/configs/battle_sim/scenarios/mixed_6v12_two_archer.tres"
FACTION = "player"


def objective(fitness: Fitness) -> float:
    return score_fitness(fitness, MAX_ITER)


def _baseline_genome(specs) -> dict[str, int]:
    return {spec.name: int(SCORE_DEFAULTS[spec.name]) for spec in specs}


def _sample_genomes(specs, count: int, seed: int) -> list[dict[str, int]]:
    rng = random.Random(seed)
    genomes = [_baseline_genome(specs)]
    while len(genomes) < count:
        genome = {}
        for spec in specs:
            default = float(SCORE_DEFAULTS[spec.name])
            span = spec.hi - spec.lo
            value = default + rng.uniform(-0.15 * span, 0.15 * span)
            genome[spec.name] = spec.clamp(value)
        genomes.append(genome)
    return genomes


def _fitness_payload(fitness: Fitness) -> dict[str, Any]:
    return {
        "score": fitness.score,
        "win_rate": fitness.win_rate,
        "loss_rate": fitness.loss_rate,
        "stalemate_rate": fitness.stalemate_rate,
        "avg_iterations": fitness.avg_iterations,
        "own_deaths": fitness.own_deaths,
        "enemy_deaths": fitness.enemy_deaths,
        "own_damage": fitness.own_damage,
        "enemy_damage": fitness.enemy_damage,
        "n": fitness.n,
    }


def _write_observations(
    path: str,
    *,
    genomes: Sequence[Mapping[str, int]],
    fits: Sequence[Fitness],
) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        for idx, (genome, fit) in enumerate(zip(genomes, fits)):
            record = {
                "sample": idx,
                "scenario": SCENARIO,
                "faction": FACTION,
                "genome": dict(genome),
                "objective": objective(fit),
                "fitness": _fitness_payload(fit),
            }
            fh.write(json.dumps(record, sort_keys=True) + "\n")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Run one real Godot observation batch, then train/rank on CUDA."
    )
    parser.add_argument("--candidates", type=int, default=4)
    parser.add_argument("--total-workers", type=int, default=4)
    parser.add_argument("--workers-per-candidate", type=int, default=1)
    parser.add_argument("--count-per-worker", type=int, default=1)
    parser.add_argument("--epochs", type=int, default=64)
    parser.add_argument("--rank-count", type=int, default=100000)
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument("--seed", type=int, default=13)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(REPO_ROOT, ".tmp_tuner", "gpu_bridge_real_sample"),
    )
    args = parser.parse_args()

    candidate_count = max(4, args.candidates)
    device_name = require_cuda()
    specs = score_weight_space(FACTION)
    genomes = _sample_genomes(specs, candidate_count, args.seed)
    os.makedirs(args.output_dir, exist_ok=True)

    print(
        "real-observation batch: "
        f"candidates={candidate_count} workers={args.total_workers} "
        f"workers_per_candidate={args.workers_per_candidate} "
        f"count_per_worker={args.count_per_worker} gpu={device_name}",
        flush=True,
    )
    fits = evaluate_6v12_batch(
        genomes,
        specs,
        win_faction=FACTION,
        total_workers=args.total_workers,
        workers_per_candidate=args.workers_per_candidate,
        count_per_worker=args.count_per_worker,
        profile_prefix="gpu_bridge_real_sample",
        scenario_file=SCENARIO,
    )

    observations_path = os.path.join(args.output_dir, "observations.jsonl")
    _write_observations(observations_path, genomes=genomes, fits=fits)
    for idx, fit in enumerate(fits):
        print(f"candidate {idx}: objective={objective(fit):+.3f} {fit}", flush=True)

    surrogate_dir = os.path.join(args.output_dir, "surrogate")
    train_result = train_surrogate(
        observations_path,
        output_dir=surrogate_dir,
        faction=FACTION,
        epochs=args.epochs,
        batch_size=max(1, min(32, candidate_count)),
        seed=args.seed,
    )
    ranked_path = os.path.join(args.output_dir, "ranked_candidates.json")
    ranked = rank_candidates(
        train_result.model_path,
        train_result.metadata_path,
        count=args.rank_count,
        top_k=args.top_k,
        output_json=ranked_path,
        seed=args.seed + 1,
    )
    top_profile_path = os.path.join(args.output_dir, "top_ranked_score_profile.tres")
    write_score_profile_tres(top_profile_path, ranked[0]["genome"])

    result_path = os.path.join(args.output_dir, "result.json")
    with open(result_path, "w", encoding="utf-8") as fh:
        json.dump(
            {
                "scenario": SCENARIO,
                "faction": FACTION,
                "gpu": train_result.device_name,
                "objective_formula": OBJECTIVE_FORMULA,
                "observations_jsonl": observations_path,
                "model": train_result.model_path,
                "metadata": train_result.metadata_path,
                "ranked_candidates": ranked_path,
                "top_ranked_score_profile": top_profile_path,
                "rank_count": args.rank_count,
                "top_predicted_objective": ranked[0]["predicted_objective"],
                "observed_candidates": [
                    {
                        "objective": objective(fit),
                        "fitness": _fitness_payload(fit),
                        "genome": dict(genome),
                    }
                    for genome, fit in zip(genomes, fits)
                ],
            },
            fh,
            indent=2,
            sort_keys=True,
        )

    print("cuda surrogate trained and ranked real observations", flush=True)
    print(f"  observations: {observations_path}", flush=True)
    print(f"  model: {train_result.model_path}", flush=True)
    print(f"  metadata: {train_result.metadata_path}", flush=True)
    print(f"  ranked: {ranked_path}", flush=True)
    print(f"  top_profile: {top_profile_path}", flush=True)
    print(f"  result: {result_path}", flush=True)
    print(f"  top_predicted_objective: {ranked[0]['predicted_objective']:+.6f}", flush=True)


if __name__ == "__main__":
    main()

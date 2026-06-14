"""Run the formal CPU-sim + GPU-surrogate + CPU-verification tuning loop."""

from __future__ import annotations

import argparse
import json
import os
from typing import Any

from .evaluator import REPO_ROOT, evaluate_6v12, evaluate_6v12_batch, score_runs
from .export_score_profile import write_score_profile_tres
from .gpu_surrogate import rank_candidates, require_cuda, train_surrogate
from .run_gpu_bridge_sample import (
    FACTION,
    SCENARIO,
    _baseline_genome,
    _fitness_payload,
    _sample_genomes,
    _write_observations,
    objective,
)
from .search_space import score_weight_space


def _verified_payload(
    *,
    label: str,
    genome: dict[str, int],
    fitness,
    predicted_objective: float | None = None,
    gpu_rank: int | None = None,
) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "label": label,
        "genome": genome,
        "objective": objective(fitness),
        "fitness": _fitness_payload(fitness),
    }
    if predicted_objective is not None:
        payload["predicted_objective"] = predicted_objective
    if gpu_rank is not None:
        payload["gpu_rank"] = gpu_rank
    return payload


def _top_up_verification_runs(
    *,
    fits,
    entries: list[dict[str, Any]],
    specs,
    target_runs: int,
    workers_per_attempt: int,
) -> list:
    topped_up = list(fits)
    for idx, fit in enumerate(topped_up):
        attempt = 0
        while fit.n < target_runs:
            missing = target_runs - fit.n
            workers = max(1, min(workers_per_attempt, missing))
            print(
                f"top-up verification {entries[idx]['label']}: "
                f"n={fit.n}/{target_runs}, extra_workers={workers}",
                flush=True,
            )
            extra = evaluate_6v12(
                entries[idx]["genome"],
                specs,
                win_faction=FACTION,
                workers=workers,
                count_per_worker=1,
                profile_id=f"gpu_tuning_formal_verify_topup_{idx}_{attempt}",
                scenario_file=SCENARIO,
            )
            if extra.n <= 0:
                raise RuntimeError(
                    f"Top-up verification for {entries[idx]['label']} produced no runs."
                )
            fit = score_runs([*fit.runs, *extra.runs], FACTION, 0.5)
            topped_up[idx] = fit
            attempt += 1
    return topped_up


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Run formal battle AI tuning: real Godot observations, CUDA surrogate "
            "ranking, then real Godot verification of the GPU top candidates."
        )
    )
    parser.add_argument("--observation-candidates", type=int, default=16)
    parser.add_argument("--observation-total-workers", type=int, default=16)
    parser.add_argument("--observation-workers-per-candidate", type=int, default=2)
    parser.add_argument("--observation-count-per-worker", type=int, default=2)
    parser.add_argument("--epochs", type=int, default=512)
    parser.add_argument("--rank-count", type=int, default=250000)
    parser.add_argument("--gpu-top-k", type=int, default=32)
    parser.add_argument("--verify-top-k", type=int, default=4)
    parser.add_argument("--verify-total-workers", type=int, default=20)
    parser.add_argument("--verify-workers-per-candidate", type=int, default=4)
    parser.add_argument("--verify-count-per-worker", type=int, default=5)
    parser.add_argument("--seed", type=int, default=101)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(REPO_ROOT, ".tmp_tuner", "gpu_tuning_formal"),
    )
    args = parser.parse_args()

    device_name = require_cuda()
    specs = score_weight_space(FACTION)
    observation_count = max(4, args.observation_candidates)
    verify_top_k = max(1, args.verify_top_k)
    gpu_top_k = max(verify_top_k, args.gpu_top_k)
    os.makedirs(args.output_dir, exist_ok=True)

    print(
        "formal observation phase: "
        f"scenario={SCENARIO} candidates={observation_count} "
        f"workers={args.observation_total_workers} "
        f"workers_per_candidate={args.observation_workers_per_candidate} "
        f"count_per_worker={args.observation_count_per_worker} "
        f"gpu={device_name}",
        flush=True,
    )
    genomes = _sample_genomes(specs, observation_count, args.seed)
    observation_fits = evaluate_6v12_batch(
        genomes,
        specs,
        win_faction=FACTION,
        total_workers=args.observation_total_workers,
        workers_per_candidate=args.observation_workers_per_candidate,
        count_per_worker=args.observation_count_per_worker,
        profile_prefix="gpu_tuning_formal_observe",
        scenario_file=SCENARIO,
    )

    observations_path = os.path.join(args.output_dir, "observations.jsonl")
    _write_observations(observations_path, genomes=genomes, fits=observation_fits)
    for idx, fit in enumerate(observation_fits):
        print(f"observation {idx}: objective={objective(fit):+.3f} {fit}", flush=True)

    surrogate_dir = os.path.join(args.output_dir, "surrogate")
    train_result = train_surrogate(
        observations_path,
        output_dir=surrogate_dir,
        faction=FACTION,
        epochs=args.epochs,
        batch_size=max(1, min(64, observation_count)),
        seed=args.seed,
    )
    ranked_path = os.path.join(args.output_dir, "gpu_ranked_candidates.json")
    ranked = rank_candidates(
        train_result.model_path,
        train_result.metadata_path,
        count=args.rank_count,
        top_k=gpu_top_k,
        output_json=ranked_path,
        seed=args.seed + 1,
    )
    print(
        "gpu ranking phase: "
        f"rank_count={args.rank_count} top_k={gpu_top_k} "
        f"top_predicted={ranked[0]['predicted_objective']:+.6f}",
        flush=True,
    )

    verify_entries = [
        {
            "label": "baseline",
            "genome": _baseline_genome(specs),
            "predicted_objective": None,
            "gpu_rank": None,
        }
    ]
    for rank_index, entry in enumerate(ranked[:verify_top_k]):
        verify_entries.append(
            {
                "label": f"gpu_top_{rank_index}",
                "genome": entry["genome"],
                "predicted_objective": float(entry["predicted_objective"]),
                "gpu_rank": rank_index,
            }
        )

    print(
        "formal verification phase: "
        f"profiles={len(verify_entries)} workers={args.verify_total_workers} "
        f"workers_per_candidate={args.verify_workers_per_candidate} "
        f"count_per_worker={args.verify_count_per_worker}",
        flush=True,
    )
    verify_fits = evaluate_6v12_batch(
        [entry["genome"] for entry in verify_entries],
        specs,
        win_faction=FACTION,
        total_workers=args.verify_total_workers,
        workers_per_candidate=args.verify_workers_per_candidate,
        count_per_worker=args.verify_count_per_worker,
        profile_prefix="gpu_tuning_formal_verify",
        scenario_file=SCENARIO,
    )
    target_verify_runs = args.verify_workers_per_candidate * args.verify_count_per_worker
    verify_fits = _top_up_verification_runs(
        fits=verify_fits,
        entries=verify_entries,
        specs=specs,
        target_runs=target_verify_runs,
        workers_per_attempt=args.verify_workers_per_candidate,
    )

    verified = [
        _verified_payload(
            label=str(entry["label"]),
            genome=dict(entry["genome"]),
            fitness=fit,
            predicted_objective=entry["predicted_objective"],
            gpu_rank=entry["gpu_rank"],
        )
        for entry, fit in zip(verify_entries, verify_fits)
    ]
    verified_sorted = sorted(verified, key=lambda item: item["objective"], reverse=True)
    verified_path = os.path.join(args.output_dir, "verified_candidates.json")
    with open(verified_path, "w", encoding="utf-8") as fh:
        json.dump(verified_sorted, fh, indent=2, sort_keys=True)

    best = verified_sorted[0]
    verified_best_profile = os.path.join(args.output_dir, "verified_best_score_profile.tres")
    write_score_profile_tres(verified_best_profile, best["genome"])
    for item in verified_sorted:
        fitness = item["fitness"]
        print(
            f"verified {item['label']}: objective={item['objective']:+.3f} "
            f"win={fitness['win_rate']:.2f} stale={fitness['stalemate_rate']:.2f} "
            f"avg_iter={fitness['avg_iterations']:.0f} "
            f"kills={fitness['enemy_deaths']:.1f}-{fitness['own_deaths']:.1f}",
            flush=True,
        )

    result_path = os.path.join(args.output_dir, "formal_result.json")
    with open(result_path, "w", encoding="utf-8") as fh:
        json.dump(
            {
                "scenario": SCENARIO,
                "faction": FACTION,
                "gpu": train_result.device_name,
                "observations_jsonl": observations_path,
                "observation_candidates": observation_count,
                "observation_runs_per_candidate": (
                    args.observation_workers_per_candidate
                    * args.observation_count_per_worker
                ),
                "model": train_result.model_path,
                "metadata": train_result.metadata_path,
                "ranked_candidates": ranked_path,
                "rank_count": args.rank_count,
                "gpu_top_k": gpu_top_k,
                "verified_candidates": verified_path,
                "verify_runs_per_candidate": (
                    args.verify_workers_per_candidate * args.verify_count_per_worker
                ),
                "verified_best_score_profile": verified_best_profile,
                "verified_best": best,
                "observed_candidates": [
                    {
                        "objective": objective(fit),
                        "fitness": _fitness_payload(fit),
                        "genome": dict(genome),
                    }
                    for genome, fit in zip(genomes, observation_fits)
                ],
            },
            fh,
            indent=2,
            sort_keys=True,
        )

    print("formal tuning finished", flush=True)
    print(f"  observations: {observations_path}", flush=True)
    print(f"  model: {train_result.model_path}", flush=True)
    print(f"  metadata: {train_result.metadata_path}", flush=True)
    print(f"  ranked: {ranked_path}", flush=True)
    print(f"  verified: {verified_path}", flush=True)
    print(f"  verified_best_profile: {verified_best_profile}", flush=True)
    print(f"  result: {result_path}", flush=True)
    print(f"  verified_best_label: {best['label']}", flush=True)
    print(f"  verified_best_objective: {best['objective']:+.6f}", flush=True)


if __name__ == "__main__":
    main()

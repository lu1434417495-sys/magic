"""Run the formal CPU-sim + GPU-surrogate + CPU-verification tuning loop."""

from __future__ import annotations

import argparse
import json
import os
from typing import Any

from .evaluator import (
    DATASET_PATH,
    REPO_ROOT,
    evaluate_6v12,
    evaluate_6v12_batch,
    evaluate_6v12_profile_res,
    score_runs,
)
from .export_score_profile import write_score_profile_tres
from .gpu_surrogate import rank_candidates, require_cuda, train_surrogate
from .objective import FORMULA as OBJECTIVE_FORMULA
from .run_gpu_bridge_sample import (
    FACTION,
    SCENARIO,
    _baseline_genome,
    _fitness_payload,
    _sample_genomes,
    _write_observations,
    objective,
)
from .search_space import resolve_drop_params, score_weight_space


def _central_store_summary(scenario: str, faction: str) -> str:
    """One-line convergence/growth view of the cross-run central store, filtered to
    this scenario+faction. The store is append-only (every record_sample is durable),
    so 'resume' is just re-running this script — completed samples are never lost."""
    if not os.path.exists(DATASET_PATH):
        return f"central store: (none yet at {DATASET_PATH})"
    rows = 0
    battles = 0
    best = None
    with open(DATASET_PATH, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except json.JSONDecodeError:
                continue
            if rec.get("faction") != faction:
                continue
            if scenario and scenario not in str(rec.get("scenario", "")):
                continue
            fit = rec.get("fitness", {})
            rows += 1
            battles += int(fit.get("n", 0) or 0)
            obj = fit.get("score")
            if obj is not None and (best is None or obj > best):
                best = obj
    best_str = f"{best:+.3f}" if best is not None else "n/a"
    return (f"central store [{faction} @ {scenario}]: "
            f"genomes={rows} battles={battles} best_objective={best_str}")


def _genome_key(genome: dict[str, int], specs) -> tuple[int, ...]:
    return tuple(int(genome.get(spec.name, 0)) for spec in specs)


def _unseen_ranked_genomes(
    ranked: list[dict[str, Any]],
    *,
    specs,
    seen: set[tuple[int, ...]],
    limit: int,
) -> list[dict[str, Any]]:
    selected = []
    for entry in ranked:
        genome = {spec.name: int(entry["genome"][spec.name]) for spec in specs}
        key = _genome_key(genome, specs)
        if key in seen:
            continue
        selected.append(
            {
                "genome": genome,
                "predicted_objective": float(entry["predicted_objective"]),
            }
        )
        if len(selected) >= limit:
            break
    return selected


def _verified_payload(
    *,
    label: str,
    genome: dict[str, int] | None = None,
    profile_res: str | None = None,
    fitness,
    predicted_objective: float | None = None,
    gpu_rank: int | None = None,
) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "label": label,
        "objective": objective(fitness),
        "fitness": _fitness_payload(fitness),
    }
    if genome is not None:
        payload["genome"] = genome
    if profile_res is not None:
        payload["profile_res"] = profile_res
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


def _top_up_profile_verification_runs(
    *,
    fit,
    entry: dict[str, Any],
    target_runs: int,
    workers_per_attempt: int,
) -> Any:
    attempt = 0
    while fit.n < target_runs:
        missing = target_runs - fit.n
        workers = max(1, min(workers_per_attempt, missing))
        print(
            f"top-up verification {entry['label']}: "
            f"n={fit.n}/{target_runs}, extra_workers={workers}",
            flush=True,
        )
        extra = evaluate_6v12_profile_res(
            entry["profile_res"],
            win_faction=FACTION,
            workers=workers,
            count_per_worker=1,
            scenario_file=SCENARIO,
        )
        if extra.n <= 0:
            raise RuntimeError(
                f"Top-up verification for {entry['label']} produced no runs."
            )
        fit = score_runs([*fit.runs, *extra.runs], FACTION, 0.5)
        attempt += 1
    return fit


def _path_to_res(path: str) -> str:
    if path.startswith("res://"):
        return path
    abs_path = os.path.abspath(os.path.join(REPO_ROOT, path))
    rel_path = os.path.relpath(abs_path, REPO_ROOT)
    if rel_path == os.pardir or rel_path.startswith(os.pardir + os.sep):
        raise ValueError(f"Profile path must be inside the project root: {path}")
    return "res://" + rel_path.replace(os.sep, "/")


def _parse_extra_profile(raw: str) -> dict[str, str]:
    if "=" in raw:
        label, path = raw.split("=", 1)
        label = label.strip()
        path = path.strip()
    else:
        path = raw.strip()
        label = os.path.splitext(os.path.basename(path))[0]
    if not label or not path:
        raise ValueError("--extra-profile expects label=path or path.")
    return {"label": label, "profile_res": _path_to_res(path)}


def main() -> None:
    global SCENARIO, FACTION
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
    parser.add_argument(
        "--observation-chunk-size",
        type=int,
        default=0,
        help="Number of observation candidates to evaluate before persisting observations.",
    )
    parser.add_argument("--epochs", type=int, default=512)
    parser.add_argument("--rank-count", type=int, default=250000)
    parser.add_argument("--rank-sampling", choices=["global", "local"], default="local")
    parser.add_argument("--rank-radius-fraction", type=float, default=0.15)
    parser.add_argument("--gpu-top-k", type=int, default=32)
    parser.add_argument("--active-learning-rounds", type=int, default=0)
    parser.add_argument("--active-learning-top-k", type=int, default=8)
    parser.add_argument("--active-learning-total-workers", type=int, default=32)
    parser.add_argument("--active-learning-workers-per-candidate", type=int, default=4)
    parser.add_argument("--active-learning-count-per-worker", type=int, default=3)
    parser.add_argument("--verify-top-k", type=int, default=4)
    parser.add_argument("--verify-total-workers", type=int, default=48)
    parser.add_argument("--verify-workers-per-candidate", type=int, default=12)
    parser.add_argument("--verify-count-per-worker", type=int, default=5)
    parser.add_argument("--min-verify-runs", type=int, default=60)
    parser.add_argument(
        "--extra-profile",
        action="append",
        default=[],
        help=(
            "Additional BattleSimProfileDef to verify, as label=res://path or "
            "label=repo/relative/path. May be passed more than once."
        ),
    )
    parser.add_argument("--seed", type=int, default=101)
    parser.add_argument(
        "--output-dir",
        default=os.path.join(REPO_ROOT, ".tmp_tuner", "gpu_tuning_formal"),
    )
    parser.add_argument(
        "--scenario",
        default=SCENARIO,
        help="res:// scenario to tune on (default: the module SCENARIO constant).",
    )
    parser.add_argument(
        "--faction",
        default=FACTION,
        help="faction whose score profile is tuned (default: the module FACTION constant).",
    )
    parser.add_argument(
        "--drop-params",
        default="",
        help="drop params from the search (named group e.g. 'mage', or comma list). "
        "Frozen params stay at shipped defaults. Use 'mage' for mage-free rosters "
        "like two_archer to skip the zero-gradient MP/meteor/chain dimensions.",
    )
    args = parser.parse_args()

    # Optional overrides rebind the module globals the helper evals read at call time;
    # defaults leave the shipped two_archer/player behaviour unchanged.
    SCENARIO = args.scenario
    FACTION = args.faction

    device_name = require_cuda()
    drop = resolve_drop_params(args.drop_params)
    specs = score_weight_space(FACTION, drop=drop)
    if drop:
        print(f"dropping {len(drop)} params from search: {sorted(drop)}", flush=True)
    print(f"[before] {_central_store_summary(SCENARIO, FACTION)}", flush=True)
    observation_count = max(4, args.observation_candidates)
    verify_top_k = max(1, args.verify_top_k)
    gpu_top_k = max(verify_top_k, args.gpu_top_k)
    target_verify_runs = max(
        args.min_verify_runs,
        args.verify_workers_per_candidate * args.verify_count_per_worker,
    )
    extra_profiles = [_parse_extra_profile(raw) for raw in args.extra_profile]
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
    observations_path = os.path.join(args.output_dir, "observations.jsonl")
    observation_chunk_size = (
        observation_count
        if args.observation_chunk_size <= 0
        else max(1, args.observation_chunk_size)
    )
    all_genomes = []
    all_fits = []
    for start in range(0, observation_count, observation_chunk_size):
        end = min(observation_count, start + observation_chunk_size)
        chunk_genomes = genomes[start:end]
        print(
            f"observation chunk {start // observation_chunk_size}: "
            f"candidates={start}-{end - 1}",
            flush=True,
        )
        chunk_fits = evaluate_6v12_batch(
            chunk_genomes,
            specs,
            win_faction=FACTION,
            total_workers=args.observation_total_workers,
            workers_per_candidate=args.observation_workers_per_candidate,
            count_per_worker=args.observation_count_per_worker,
            profile_prefix=f"gpu_tuning_formal_observe_{start}",
            scenario_file=SCENARIO,
        )
        all_genomes.extend(chunk_genomes)
        all_fits.extend(chunk_fits)
        _write_observations(observations_path, genomes=all_genomes, fits=all_fits)
        for offset, fit in enumerate(chunk_fits):
            idx = start + offset
            print(f"observation {idx}: objective={objective(fit):+.3f} {fit}", flush=True)
        print(
            f"observation progress: rows={len(all_genomes)}/{observation_count} "
            f"written={observations_path}",
            flush=True,
        )

    surrogate_dir = os.path.join(args.output_dir, "surrogate")
    train_result = train_surrogate(
        observations_path,
        output_dir=surrogate_dir,
        faction=FACTION,
        epochs=args.epochs,
        batch_size=max(1, min(64, len(all_genomes))),
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
        sample_mode=args.rank_sampling,
        radius_fraction=args.rank_radius_fraction,
    )
    print(
        "gpu ranking phase: "
        f"rank_count={args.rank_count} top_k={gpu_top_k} "
        f"sampling={args.rank_sampling} radius={args.rank_radius_fraction:.3f} "
        f"top_predicted={ranked[0]['predicted_objective']:+.6f}",
        flush=True,
    )
    active_round_summaries = []
    seen_genomes = {_genome_key(genome, specs) for genome in all_genomes}
    for round_index in range(max(0, args.active_learning_rounds)):
        active_entries = _unseen_ranked_genomes(
            ranked,
            specs=specs,
            seen=seen_genomes,
            limit=max(1, args.active_learning_top_k),
        )
        if not active_entries:
            print(f"active learning round {round_index}: no unseen ranked candidates", flush=True)
            break
        active_genomes = [dict(entry["genome"]) for entry in active_entries]
        print(
            f"active learning round {round_index}: "
            f"candidates={len(active_genomes)} workers={args.active_learning_total_workers} "
            f"workers_per_candidate={args.active_learning_workers_per_candidate} "
            f"count_per_worker={args.active_learning_count_per_worker}",
            flush=True,
        )
        active_fits = evaluate_6v12_batch(
            active_genomes,
            specs,
            win_faction=FACTION,
            total_workers=args.active_learning_total_workers,
            workers_per_candidate=args.active_learning_workers_per_candidate,
            count_per_worker=args.active_learning_count_per_worker,
            profile_prefix=f"gpu_tuning_formal_active_{round_index}",
            scenario_file=SCENARIO,
        )
        for idx, (entry, fit) in enumerate(zip(active_entries, active_fits)):
            print(
                f"active {round_index}.{idx}: "
                f"pred={entry['predicted_objective']:+.3f} "
                f"objective={objective(fit):+.3f} {fit}",
                flush=True,
            )
        all_genomes.extend(active_genomes)
        all_fits.extend(active_fits)
        seen_genomes.update(_genome_key(genome, specs) for genome in active_genomes)
        _write_observations(observations_path, genomes=all_genomes, fits=all_fits)
        active_round_summaries.append(
            {
                "round": round_index,
                "candidate_count": len(active_genomes),
                "runs_per_candidate": (
                    args.active_learning_workers_per_candidate
                    * args.active_learning_count_per_worker
                ),
                "candidates": [
                    {
                        "predicted_objective": entry["predicted_objective"],
                        "objective": objective(fit),
                        "fitness": _fitness_payload(fit),
                        "genome": dict(genome),
                    }
                    for entry, fit, genome in zip(active_entries, active_fits, active_genomes)
                ],
            }
        )
        train_result = train_surrogate(
            observations_path,
            output_dir=surrogate_dir,
            faction=FACTION,
            epochs=args.epochs,
            batch_size=max(1, min(64, len(all_genomes))),
            seed=args.seed + round_index + 1,
        )
        ranked = rank_candidates(
            train_result.model_path,
            train_result.metadata_path,
            count=args.rank_count,
            top_k=gpu_top_k,
            output_json=ranked_path,
            seed=args.seed + round_index + 2,
            sample_mode=args.rank_sampling,
            radius_fraction=args.rank_radius_fraction,
        )
        print(
            f"gpu ranking after active round {round_index}: "
            f"observations={len(all_genomes)} "
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
        f"profiles={len(verify_entries) + len(extra_profiles)} "
        f"workers={args.verify_total_workers} "
        f"workers_per_candidate={args.verify_workers_per_candidate} "
        f"count_per_worker={args.verify_count_per_worker} "
        f"target_runs={target_verify_runs}",
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
    for entry in extra_profiles:
        print(
            f"external verification {entry['label']}: "
            f"profile={entry['profile_res']}",
            flush=True,
        )
        fit = evaluate_6v12_profile_res(
            entry["profile_res"],
            win_faction=FACTION,
            workers=args.verify_workers_per_candidate,
            count_per_worker=args.verify_count_per_worker,
            scenario_file=SCENARIO,
        )
        fit = _top_up_profile_verification_runs(
            fit=fit,
            entry=entry,
            target_runs=target_verify_runs,
            workers_per_attempt=args.verify_workers_per_candidate,
        )
        verified.append(
            _verified_payload(
                label=entry["label"],
                profile_res=entry["profile_res"],
                fitness=fit,
            )
        )
    verified_sorted = sorted(verified, key=lambda item: item["objective"], reverse=True)
    verified_path = os.path.join(args.output_dir, "verified_candidates.json")
    with open(verified_path, "w", encoding="utf-8") as fh:
        json.dump(verified_sorted, fh, indent=2, sort_keys=True)

    best = verified_sorted[0]
    verified_best_profile = None
    if best.get("genome") is not None:
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
                "objective_formula": OBJECTIVE_FORMULA,
                "observations_jsonl": observations_path,
                "observation_candidates": observation_count,
                "observation_runs_per_candidate": (
                    args.observation_workers_per_candidate
                    * args.observation_count_per_worker
                ),
                "observation_chunk_size": observation_chunk_size,
                "total_observation_rows": len(all_genomes),
                "model": train_result.model_path,
                "metadata": train_result.metadata_path,
                "ranked_candidates": ranked_path,
                "rank_count": args.rank_count,
                "rank_sampling": args.rank_sampling,
                "rank_radius_fraction": args.rank_radius_fraction,
                "gpu_top_k": gpu_top_k,
                "active_learning_rounds": active_round_summaries,
                "verified_candidates": verified_path,
                "requested_verify_runs_per_candidate": (
                    args.verify_workers_per_candidate * args.verify_count_per_worker
                ),
                "min_verify_runs": args.min_verify_runs,
                "target_verify_runs": target_verify_runs,
                "extra_profiles": extra_profiles,
                "verified_best_score_profile": verified_best_profile,
                "verified_best": best,
                "observed_candidates": [
                    {
                        "objective": objective(fit),
                        "fitness": _fitness_payload(fit),
                        "genome": dict(genome),
                    }
                    for genome, fit in zip(all_genomes, all_fits)
                ],
            },
            fh,
            indent=2,
            sort_keys=True,
        )
    print(f"[after]  {_central_store_summary(SCENARIO, FACTION)}", flush=True)
    print(f"result: {result_path}", flush=True)

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

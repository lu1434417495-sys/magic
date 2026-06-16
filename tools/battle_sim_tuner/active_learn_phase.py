"""Active-learning loop for one phase (e.g. the 15-dim A+B params): accumulate while
converging.

Each round:
  1. gpu_search (subprocess, GPU): ensemble + pessimistic CMA/gradient over the free
     params -> ranked.json (top candidates).
  2. evaluate the top-k candidates in REAL battles (CPU) on the scenario. These land in
     the central store via evaluator.record_sample, so the dataset grows exactly where
     the search wants to look (active learning), not randomly.
  3. next round's gpu_search retrains on the grown store -> the surrogate sharpens near
     the optimum and ensemble pred_std there shrinks.

Stops when the best REAL objective stops improving for --patience rounds (converged) or
--rounds is hit.

Runs GPU (gpu_search) + CPU (real battles), so it competes with any other tuner on the
box — run it when the machine has headroom. Does NOT run on import.

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.active_learn_phase \
        --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
        --free-params phase1 --rounds 8 --eval-top-k 6 --eval-workers 8
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess

from .evaluator import DATASET_PATH, REPO_ROOT, evaluate
from .objective import DEFAULT_MAX_ITERATIONS, score_fitness
from .search_space import score_weight_space

GPU_PYTHON_DEFAULT = "/home/luchaoli/venvs/cuda-op/bin/python"


def _scenario_substr(scenario: str) -> str:
    base = os.path.basename(scenario).split(".")[0]
    return base or scenario


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--scenario", required=True, help="res:// scenario (real battles run on this)")
    p.add_argument("--faction", default="player")
    p.add_argument("--free-params", default="phase1")
    p.add_argument("--rounds", type=int, default=8)
    p.add_argument("--patience", type=int, default=2, help="stop if best real obj stalls this many rounds")
    p.add_argument("--eval-top-k", type=int, default=6, help="candidates to real-evaluate per round")
    p.add_argument("--eval-workers", type=int, default=8)
    p.add_argument("--eval-timeout", type=float, default=1200.0)
    p.add_argument("--dataset", default=DATASET_PATH)
    p.add_argument("--output-dir", default=os.path.join(REPO_ROOT, ".tmp_tuner", "active_learn_phase"))
    p.add_argument("--gpu-python", default=GPU_PYTHON_DEFAULT)
    # gpu_search knobs (passed through)
    p.add_argument("--ensemble-size", type=int, default=8)
    p.add_argument("--kappa", type=float, default=1.0)
    p.add_argument("--cma-popsize", type=int, default=128)
    p.add_argument("--cma-generations", type=int, default=300)
    p.add_argument("--restarts", type=int, default=3)
    args = p.parse_args()

    specs = score_weight_space(args.faction)
    scn_filter = _scenario_substr(args.scenario)
    os.makedirs(args.output_dir, exist_ok=True)

    best_real = float("-inf")
    best_genome = None
    stall = 0
    for rnd in range(args.rounds):
        round_dir = os.path.join(args.output_dir, f"round_{rnd}")
        # 1. GPU search proposes top candidates from the current central store.
        cmd = [
            args.gpu_python, "-m", "battle_sim_tuner.gpu_search",
            "--observations", args.dataset, "--scenario", scn_filter, "--faction", args.faction,
            "--free-params", args.free_params, "--ensemble-size", str(args.ensemble_size),
            "--kappa", str(args.kappa), "--cma-popsize", str(args.cma_popsize),
            "--cma-generations", str(args.cma_generations), "--restarts", str(args.restarts),
            "--top-k", str(args.eval_top_k), "--output-dir", round_dir,
        ]
        print(f"\n=== round {rnd}: gpu_search -> {round_dir} ===", flush=True)
        rc = subprocess.run(cmd, cwd=os.path.join(REPO_ROOT, "tools")).returncode
        ranked_path = os.path.join(round_dir, "ranked.json")
        if rc != 0 or not os.path.exists(ranked_path):
            print(f"  gpu_search failed (rc={rc}); stopping.", flush=True)
            break
        with open(ranked_path, encoding="utf-8") as fh:
            ranked = json.load(fh)

        # 2. Real-evaluate the proposed top-k (records into the central store).
        prev_best = best_real
        round_best = float("-inf")
        for i, entry in enumerate(ranked[: args.eval_top_k]):
            genome = entry["genome"]
            fit = evaluate(genome, specs, args.scenario, win_faction=args.faction,
                           workers=args.eval_workers, profile_id=f"al_r{rnd}_c{i}",
                           timeout=args.eval_timeout)
            real_obj = score_fitness(fit, DEFAULT_MAX_ITERATIONS)
            print(f"  cand {i}: pred_mean={entry.get('pred_mean'):+.3f} pred_std={entry.get('pred_std'):.3f}"
                  f" -> REAL obj={real_obj:+.3f} (win={fit.win_rate:.2f} stale={fit.stalemate_rate:.2f} n={fit.n})",
                  flush=True)
            round_best = max(round_best, real_obj)
            if real_obj > best_real:
                best_real, best_genome = real_obj, genome

        # 3. Convergence check on the REAL objective.
        print(f"  round {rnd}: round_best_real={round_best:+.3f}  global_best_real={best_real:+.3f}", flush=True)
        stall = 0 if best_real > prev_best + 1e-9 else stall + 1
        if stall >= args.patience:
            print(f"\nconverged: best real objective stalled {stall} round(s).", flush=True)
            break

    if best_genome is not None:
        from .export_score_profile import write_score_profile_tres
        champ = os.path.join(args.output_dir, "champion_score_profile.tres")
        write_score_profile_tres(champ, best_genome)
        with open(os.path.join(args.output_dir, "champion_genome.json"), "w", encoding="utf-8") as fh:
            json.dump(best_genome, fh, indent=2, sort_keys=True)
        print(f"\nbest REAL objective={best_real:+.3f}; exported {champ}")
        print("Gate it with promote_gate.py before adopting.")
    else:
        print("\nno candidate evaluated; nothing exported.")


if __name__ == "__main__":
    main()

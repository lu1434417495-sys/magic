"""Step-4 follow-up: GPU-rank a candidate pool against a trained surrogate and
export the top genome as a BattleAiScoreProfile .tres (the champion to promote).

Pairs with train_surrogate_from_central.py (which produced model+metadata). Requires
CUDA (rank_candidates calls require_cuda). Does NOT run on import.

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.rank_and_export \
        --model ../.tmp_tuner/surrogate_attrition/surrogate.pt \
        --metadata ../.tmp_tuner/surrogate_attrition/surrogate_meta.json \
        --count 250000 --top-k 16 \
        --output-dir ../.tmp_tuner/rank_attrition

The exported .tres is a candidate only — gate it with promote_gate.py (step 5)
before adopting it as a shipped profile.
"""

from __future__ import annotations

import argparse
import json
import os


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--model", required=True, help="trained surrogate .pt")
    p.add_argument("--metadata", required=True, help="surrogate metadata json (carries faction/bounds)")
    p.add_argument("--output-dir", required=True)
    p.add_argument("--count", type=int, default=250000, help="candidate pool size to rank on GPU")
    p.add_argument("--top-k", type=int, default=16)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--sample-mode", choices=["global", "local"], default="local")
    p.add_argument("--radius-fraction", type=float, default=0.15)
    p.add_argument("--center", default="", help="optional genome json to center a local search on")
    args = p.parse_args()

    center_genome = None
    if args.center:
        with open(args.center, encoding="utf-8") as fh:
            center_genome = json.load(fh)

    # Lazy import so --help works without CUDA/torch.
    from .export_score_profile import write_score_profile_tres
    from .gpu_surrogate import rank_candidates

    os.makedirs(args.output_dir, exist_ok=True)
    ranked_json = os.path.join(args.output_dir, "ranked.json")
    ranked = rank_candidates(
        args.model,
        args.metadata,
        count=args.count,
        top_k=args.top_k,
        output_json=ranked_json,
        seed=args.seed,
        sample_mode=args.sample_mode,
        radius_fraction=args.radius_fraction,
        center_genome=center_genome,
    )
    top = ranked[0]
    champion_tres = os.path.join(args.output_dir, "champion_score_profile.tres")
    write_score_profile_tres(champion_tres, top["genome"])

    print(f"ranked {args.count} candidates -> top {len(ranked)} ({ranked_json})")
    print(f"top predicted_objective = {top['predicted_objective']:+.4f}")
    print(f"exported champion profile: {champion_tres}")
    print("NEXT: gate it with promote_gate.py before adopting (surrogate scores are not ground truth).")


if __name__ == "__main__":
    main()

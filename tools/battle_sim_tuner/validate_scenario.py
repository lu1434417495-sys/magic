"""Step-1 follow-up (run AFTER the current GPU tuning run frees the cores).

Baseline-vs-baseline resolution / balance check for a tuning scenario: runs the
scenario with DEFAULT score weights on both factions over many seeds and reports
whether it actually RESOLVES (low stalemate) and is roughly even (gradient headroom).
A scenario that stalemates or is lopsided cannot tune the sustain/survival params.

This does NOT run on import. Invoke explicitly, e.g. from tools/:

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.validate_scenario \
        --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
        --workers 8

Total battles = workers x (#seeds in the scenario). Use workers>=7 for n>=20.
"""

from __future__ import annotations

import argparse

from .evaluator import evaluate_genome, is_formal_fixture_scenario
from .search_space import SCORE_DEFAULTS, score_weight_space

DEFAULT_SCENARIO = "res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres"


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--scenario", default=DEFAULT_SCENARIO)
    p.add_argument("--faction", default="player", help="faction scored as the win side")
    p.add_argument("--workers", type=int, default=8)
    p.add_argument("--timeout", type=float, default=900.0)
    p.add_argument("--max-stalemate", type=float, default=0.20,
                   help="verdict threshold: resolves if stalemate_rate <= this")
    p.add_argument("--even-band", type=float, default=0.15,
                   help="verdict: balanced if |win_rate-0.5| <= this")
    args = p.parse_args()

    specs = score_weight_space(args.faction)
    genome = dict(SCORE_DEFAULTS)  # baseline weights on both sides

    runner = "6v12-benchmark" if is_formal_fixture_scenario(args.scenario) else "balance"
    print(f"validating {args.scenario}  (faction={args.faction}, workers={args.workers}, "
          f"runner={runner})\n", flush=True)
    fit = evaluate_genome(
        genome, specs, args.scenario, win_faction=args.faction,
        workers=args.workers, count_per_worker=3, profile_id="baseline_validation",
        timeout=args.timeout,
    )
    ended_rate = 1.0 - fit.stalemate_rate
    print(f"  {fit}")
    print(f"  ended_rate={ended_rate:.2f}  win={fit.win_rate:.2f} loss={fit.loss_rate:.2f} "
          f"stale={fit.stalemate_rate:.2f}  net_kills={fit.net_kills:+.1f}  n={fit.n}")

    resolves = fit.stalemate_rate <= args.max_stalemate
    balanced = abs(fit.win_rate - 0.5) <= args.even_band
    print("\n  verdict:")
    print(f"    resolves (stalemate<= {args.max_stalemate:.2f}): {'YES' if resolves else 'NO'}")
    print(f"    balanced (|win-0.5|<= {args.even_band:.2f}):    {'YES' if balanced else 'NO'}")
    if fit.n < 20:
        print(f"    NOTE n={fit.n} < 20 -> directional only; raise --workers")
    if resolves and balanced:
        print("    => usable as a tuning arena for the A/B sustain params.")
    else:
        print("    => NOT yet usable; adjust roster (HP/AC/aggression/map) and re-validate.")


if __name__ == "__main__":
    main()

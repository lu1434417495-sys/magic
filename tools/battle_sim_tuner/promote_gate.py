"""Step-5 follow-up: real-battle promotion gate.

A surrogate-ranked champion is only a PREDICTION. Before adopting it, it must beat
the default weights in REAL battles on the tuning scenario, with no regression. This
runs candidate-vs-default at high sample count and prints a pass/fail verdict.

Does NOT run on import. Requires the scenario to be a resolving tuning arena (validate
it first with validate_scenario.py).

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.promote_gate \
        --candidate ../.tmp_tuner/rank_attrition/ranked.json \
        --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
        --workers 16

--candidate accepts either a ranked.json (top genome used) or a plain genome json.
"""

from __future__ import annotations

import argparse
import json

from .evaluator import evaluate
from .objective import DEFAULT_MAX_ITERATIONS, score_fitness
from .search_space import SCORE_DEFAULTS, score_weight_space

DEFAULT_SCENARIO = "res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres"


def _load_candidate_genome(path: str) -> dict:
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    if isinstance(data, list):  # ranked.json -> take the top entry's genome
        return dict(data[0]["genome"])
    if isinstance(data, dict) and "genome" in data:
        return dict(data["genome"])
    if isinstance(data, dict):  # already a bare genome dict
        return dict(data)
    raise SystemExit(f"unrecognised candidate file shape: {path}")


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--candidate", required=True, help="ranked.json or a genome json")
    p.add_argument("--scenario", default=DEFAULT_SCENARIO)
    p.add_argument("--faction", default="player")
    p.add_argument("--workers", type=int, default=16)
    p.add_argument("--timeout", type=float, default=1200.0)
    p.add_argument("--margin", type=float, default=0.1,
                   help="champion must beat default objective by at least this")
    args = p.parse_args()

    specs = score_weight_space(args.faction)
    default_genome = dict(SCORE_DEFAULTS)
    champ_genome = {**default_genome, **_load_candidate_genome(args.candidate)}

    print(f"promotion gate on {args.scenario} (faction={args.faction}, workers={args.workers})\n", flush=True)
    base = evaluate(default_genome, specs, args.scenario, win_faction=args.faction,
                    workers=args.workers, profile_id="gate_default", timeout=args.timeout)
    champ = evaluate(champ_genome, specs, args.scenario, win_faction=args.faction,
                     workers=args.workers, profile_id="gate_champion", timeout=args.timeout)

    base_obj = score_fitness(base, DEFAULT_MAX_ITERATIONS)
    champ_obj = score_fitness(champ, DEFAULT_MAX_ITERATIONS)
    print(f"  default : {base}  obj={base_obj:+.3f}")
    print(f"  champion: {champ}  obj={champ_obj:+.3f}")
    d_obj = champ_obj - base_obj
    print(f"\n  Δobj={d_obj:+.3f}  Δwin={champ.win_rate - base.win_rate:+.2f}  "
          f"Δloss={champ.loss_rate - base.loss_rate:+.2f}  Δstale={champ.stalemate_rate - base.stalemate_rate:+.2f}")

    improved = d_obj >= args.margin
    no_loss_regression = champ.loss_rate <= base.loss_rate + 0.05
    no_stale_regression = champ.stalemate_rate <= base.stalemate_rate + 0.05
    enough = min(base.n, champ.n) >= 20
    passed = improved and no_loss_regression and no_stale_regression and enough
    print("\n  gate:")
    print(f"    improved (Δobj>= {args.margin}):     {'YES' if improved else 'NO'}")
    print(f"    no loss regression:           {'YES' if no_loss_regression else 'NO'}")
    print(f"    no stalemate regression:      {'YES' if no_stale_regression else 'NO'}")
    print(f"    enough samples (n>=20):       {'YES' if enough else f'NO (n={min(base.n, champ.n)})'}")
    print(f"    => {'PROMOTE' if passed else 'REJECT (iterate / accumulate more samples)'}")
    raise SystemExit(0 if passed else 1)


if __name__ == "__main__":
    main()

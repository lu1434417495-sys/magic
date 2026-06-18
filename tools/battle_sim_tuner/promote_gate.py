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

from .evaluator import evaluate_genome, record_sample, score_runs
from .objective import DEFAULT_MAX_ITERATIONS, score_fitness
from .search_space import SCORE_DEFAULTS, score_weight_space

DEFAULT_SCENARIO = "res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres"


def _eval_high_r(genome, specs, scenario, *, faction, target_runs, workers, timeout, profile_id):
    """Run `genome` in waves of `workers` processes until >= target_runs battles are
    collected, then score the pooled runs once. Bounds live parallelism to `workers`
    while still reaching a high R for a trustworthy final estimate (per-genome SE
    ~ sqrt(28/R): R=200 -> ~0.37, vs the noisy R~24 used during search). The runner is
    auto-selected by scenario (formal fixtures -> 6v12 benchmark runner)."""
    acc: list = []
    max_waves = 64
    for wave in range(max_waves):
        if len(acc) >= target_runs:
            break
        fit = evaluate_genome(genome, specs, scenario, win_faction=faction, workers=workers,
                              profile_id=f"{profile_id}_w{wave}", timeout=timeout, record=False)
        acc.extend(fit.runs)
        print(f"    {profile_id}: {len(acc)}/{target_runs} runs", flush=True)
        if not fit.runs:  # a fully-failed wave: stop rather than spin
            break
    merged = score_runs(acc, faction, 0.5)
    # The pooled high-R result is the best ground-truth sample we have for this genome.
    record_sample(genome, specs, merged, scenario=scenario, win_faction=faction,
                  profile_id=profile_id)
    return merged


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
    p.add_argument("--workers", type=int, default=16,
                   help="concurrent godot processes per wave (bounds parallelism)")
    p.add_argument("--runs", type=int, default=200,
                   help="target battles per side for the final estimate (>=200 keeps "
                        "per-side SE ~0.37; collected in waves of --workers).")
    p.add_argument("--timeout", type=float, default=1200.0)
    p.add_argument("--margin", type=float, default=0.1,
                   help="champion must beat default objective by at least this")
    args = p.parse_args()

    specs = score_weight_space(args.faction)
    default_genome = dict(SCORE_DEFAULTS)
    champ_genome = {**default_genome, **_load_candidate_genome(args.candidate)}

    print(f"promotion gate on {args.scenario} (faction={args.faction}, "
          f"target_runs={args.runs}, workers/wave={args.workers})\n", flush=True)
    base = _eval_high_r(default_genome, specs, args.scenario, faction=args.faction,
                        target_runs=args.runs, workers=args.workers, timeout=args.timeout,
                        profile_id="gate_default")
    champ = _eval_high_r(champ_genome, specs, args.scenario, faction=args.faction,
                         target_runs=args.runs, workers=args.workers, timeout=args.timeout,
                         profile_id="gate_champion")

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
    min_n = max(20, int(args.runs * 0.75))  # most waves must have actually landed
    got_n = min(base.n, champ.n)
    enough = got_n >= min_n
    passed = improved and no_loss_regression and no_stale_regression and enough
    print("\n  gate:")
    print(f"    improved (Δobj>= {args.margin}):     {'YES' if improved else 'NO'}")
    print(f"    no loss regression:           {'YES' if no_loss_regression else 'NO'}")
    print(f"    no stalemate regression:      {'YES' if no_stale_regression else 'NO'}")
    print(f"    enough samples (n>={min_n}):      {'YES' if enough else f'NO (n={got_n})'}")
    print(f"    => {'PROMOTE' if passed else 'REJECT (iterate / accumulate more samples)'}")
    raise SystemExit(0 if passed else 1)


if __name__ == "__main__":
    main()

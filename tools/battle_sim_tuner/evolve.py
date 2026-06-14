"""(mu, lambda) evolution strategy over battle-AI tuning parameters.

Derivative-free, noise-tolerant search on top of `evaluator.evaluate`. The battle
is stochastic, so fitness is noisy: we keep the population small, evaluate each
candidate over many runs, and re-evaluate the incumbent mean every generation
instead of trusting a single lucky sample.

Per generation:
  1. sample `lam` children ~ N(mean, sigma) per dimension (+ the current mean),
  2. evaluate each via the parallel evaluator,
  3. set mean = centroid of the top `mu`,
  4. decay sigma.

This is the headline "self-evolution" loop: a population of AI score/behaviour
parameter vectors that compete in simulated battles, the fittest reproduce with
mutation, repeat. Promote the winning genome into the shipped defaults once it
also holds up on held-out scenarios.
"""

from __future__ import annotations

import random
import statistics
from dataclasses import dataclass, field
from typing import Callable, Mapping, Optional, Sequence

from .evaluator import Fitness, ParamSpec, evaluate


@dataclass
class GenerationResult:
    generation: int
    best_genome: dict
    best_fitness: Fitness
    mean_score: float
    mean_genome: dict


def _format_genome(genome: Mapping[str, float], specs: Sequence[ParamSpec]) -> str:
    return ", ".join(f"{s.name}={s.clamp(genome[s.name])}" for s in specs)


def evolve(
    specs: Sequence[ParamSpec],
    scenario_res: str,
    *,
    win_faction: str,
    generations: int = 6,
    mu: int = 2,
    lam: int = 5,
    workers: int = 8,
    sigma0: float = 0.35,
    sigma_decay: float = 0.82,
    stalemate_penalty: float = 0.5,
    rng_seed: int = 0,
    init: Optional[Mapping[str, float]] = None,
    objective: Optional[Callable[[Fitness], float]] = None,
    on_generation: Optional[Callable[[GenerationResult], None]] = None,
):
    """Run a (mu, lambda)-ES. Returns (best_genome, best_fitness, history).

    `objective(fitness) -> float` maps a Fitness to the scalar the ES maximises;
    defaults to `fitness.score` (win_rate minus stalemate penalty). Pass a custom
    one to optimise a different measured quantity (e.g. target battle length).
    """
    obj = objective or (lambda f: f.score)
    if mu < 1 or mu > lam:
        raise ValueError("require 1 <= mu <= lam")
    rng = random.Random(rng_seed)
    dims = list(specs)
    mean = {
        s.name: float(init[s.name]) if init and s.name in init else (s.lo + s.hi) / 2.0
        for s in dims
    }
    sigma = {s.name: sigma0 * (s.hi - s.lo) for s in dims}

    best_genome: Optional[dict] = None
    best_fitness: Optional[Fitness] = None
    best_obj = float("-inf")
    history: list[GenerationResult] = []

    for gen in range(generations):
        # The current mean is always re-evaluated (index 0) to fight noise lock-in.
        population = [dict(mean)]
        while len(population) < lam:
            population.append(
                {s.name: rng.gauss(mean[s.name], sigma[s.name]) for s in dims}
            )

        scored = []
        for idx, candidate in enumerate(population):
            fit = evaluate(
                candidate,
                dims,
                scenario_res,
                win_faction=win_faction,
                workers=workers,
                profile_id=f"g{gen}_c{idx}",
                stalemate_penalty=stalemate_penalty,
            )
            scored.append((obj(fit), candidate, fit))

        scored.sort(key=lambda e: e[0], reverse=True)
        if scored[0][0] > best_obj:
            best_obj = scored[0][0]
            best_genome, best_fitness = dict(scored[0][1]), scored[0][2]

        top = scored[: mu]
        mean = {
            s.name: float(s.clamp(statistics.fmean(c[s.name] for _, c, _ in top)))
            for s in dims
        }
        sigma = {k: v * sigma_decay for k, v in sigma.items()}

        result = GenerationResult(
            generation=gen,
            best_genome=dict(scored[0][1]),
            best_fitness=scored[0][2],
            mean_score=statistics.fmean(e[0] for e in scored),
            mean_genome=dict(mean),
        )
        history.append(result)
        if on_generation:
            on_generation(result)

    return best_genome, best_fitness, history


# --------------------------------------------------------------------------- #
# Demo: re-discover the mage blink-escape fix via evolution.
# --------------------------------------------------------------------------- #
def _demo():
    import argparse

    parser = argparse.ArgumentParser(description="Evolve the mage blink-escape threshold.")
    parser.add_argument("--scenario", required=True, help="res:// path to the tuning scenario")
    parser.add_argument("--win-faction", default="player")
    parser.add_argument("--generations", type=int, default=6)
    parser.add_argument("--lam", type=int, default=5)
    parser.add_argument("--mu", type=int, default=2)
    parser.add_argument("--workers", type=int, default=8)
    args = parser.parse_args()

    specs = [
        ParamSpec(
            "blink_gain",
            {
                "target_type": "action",
                "brain_id": "mage_controller",
                "action_id": "mage_blink_escape",
                "path": "min_survival_margin_gain_to_escape",
            },
            lo=-20,
            hi=20,
            is_int=True,
        )
    ]

    def report(r: GenerationResult):
        print(
            f"gen {r.generation}: best {r.best_fitness}  "
            f"[{_format_genome(r.best_genome, specs)}]  "
            f"mean_score={r.mean_score:+.3f}  next_mean=[{_format_genome(r.mean_genome, specs)}]"
        )

    best_genome, best_fitness, _ = evolve(
        specs,
        args.scenario,
        win_faction=args.win_faction,
        generations=args.generations,
        mu=args.mu,
        lam=args.lam,
        workers=args.workers,
        # Start from the BROKEN region (always-escape) so convergence to >=1 is meaningful.
        init={"blink_gain": -15},
        on_generation=report,
    )
    print(f"\nBEST: [{_format_genome(best_genome, specs)}]  {best_fitness}")


if __name__ == "__main__":
    _demo()

"""CMA-ES tuner with held-out validation (overfit guard).

Optimises a multi-dimensional AI-parameter genome with CMA-ES (pycma) over one or
more TRAINING scenarios, then checks the champion on a disjoint set of VALIDATION
scenarios that never enter the objective. A large train-minus-validation gap means
the genome overfit the training arena; the champion is selected by validation among
the CMA front-runners, not by training score alone.

Run (uses the project venv that has numpy + cma):
    tools/battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.tune
from the tools/ directory, or import `tune()` directly.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable, Mapping, Optional, Sequence

import cma

from .evaluator import Fitness, ParamSpec, evaluate


@dataclass
class Scenario:
    res: str          # res:// path to the scenario
    win_faction: str  # faction whose win_rate is the objective


@dataclass
class TuneResult:
    champion: dict
    train_score: float
    val_score: float
    champion_train_fit: list = field(default_factory=list)
    champion_val_fit: list = field(default_factory=list)
    history: list = field(default_factory=list)

    @property
    def overfit_gap(self) -> float:
        return self.train_score - self.val_score


def _genome_from_vector(specs, x) -> dict:
    return {s.name: s.clamp(v) for s, v in zip(specs, x)}


def mean_score(
    genome: Mapping[str, float],
    specs: Sequence[ParamSpec],
    scenarios: Sequence[Scenario],
    *,
    workers: int,
    profile_id: str,
) -> tuple[float, list[Fitness]]:
    """Mean fitness score of a genome across `scenarios` (averaged, equal weight)."""
    fits = [
        evaluate(
            genome,
            specs,
            sc.res,
            win_faction=sc.win_faction,
            workers=workers,
            profile_id=f"{profile_id}_{i}",
        )
        for i, sc in enumerate(scenarios)
    ]
    return sum(f.score for f in fits) / len(fits), fits


def tune(
    specs: Sequence[ParamSpec],
    train: Sequence[Scenario],
    val: Sequence[Scenario],
    *,
    generations: int = 5,
    popsize: int = 8,
    workers: int = 6,
    sigma0_frac: float = 0.25,
    init: Optional[Mapping[str, float]] = None,
    rng_seed: int = 0,
    on_generation: Optional[Callable[[int, dict, float], None]] = None,
) -> TuneResult:
    los = [s.lo for s in specs]
    his = [s.hi for s in specs]
    x0 = [
        float(init[s.name]) if init and s.name in init else (s.lo + s.hi) / 2.0
        for s in specs
    ]
    es = cma.CMAEvolutionStrategy(
        x0,
        sigma0_frac,
        {
            "bounds": [los, his],
            "CMA_stds": [s.hi - s.lo for s in specs],  # per-dim scale
            "popsize": popsize,
            "maxiter": generations,
            "seed": rng_seed,
            "verbose": -9,
        },
    )

    history = []
    gen = 0
    eval_counter = 0
    while not es.stop():
        solutions = es.ask()
        costs = []
        for x in solutions:
            genome = _genome_from_vector(specs, x)
            score, _ = mean_score(
                genome, specs, train, workers=workers, profile_id=f"cma{eval_counter}"
            )
            eval_counter += 1
            costs.append(-score)  # CMA-ES minimises
        es.tell(solutions, costs)

        gen_best_genome = _genome_from_vector(specs, es.best.x)
        gen_best_score = -es.best.f
        history.append((gen, gen_best_genome, gen_best_score))
        if on_generation:
            on_generation(gen, gen_best_genome, gen_best_score)
        gen += 1

    # Overfit guard: pick the champion by VALIDATION among the CMA front-runners
    # (best-ever point and the distribution mean), not by training score alone.
    candidates = {
        "xbest": _genome_from_vector(specs, es.result.xbest),
        "xmean": _genome_from_vector(specs, es.result.xfavorite),
    }
    best = None
    for tag, genome in candidates.items():
        v_score, v_fits = mean_score(genome, specs, val, workers=workers, profile_id=f"val_{tag}")
        if best is None or v_score > best[1]:
            t_score, t_fits = mean_score(
                genome, specs, train, workers=workers, profile_id=f"trn_{tag}"
            )
            best = (genome, v_score, t_score, t_fits, v_fits)

    champion, val_score, train_score, t_fits, v_fits = best
    return TuneResult(
        champion=champion,
        train_score=train_score,
        val_score=val_score,
        champion_train_fit=t_fits,
        champion_val_fit=v_fits,
        history=history,
    )


def _demo():
    from .search_space import MAGE_SPACE

    train = [Scenario("res://data/configs/battle_sim/scenarios/tuning_mage_kite.tres", "hostile")]
    val = [Scenario("res://data/configs/battle_sim/scenarios/tuning_mage_kite_val.tres", "hostile")]

    def report(gen, genome, score):
        shown = ", ".join(f"{s.name}={s.clamp(genome[s.name])}" for s in MAGE_SPACE)
        print(f"gen {gen}: best_train_score={score:+.3f}  [{shown}]", flush=True)

    # Start from the broken survival region (gains negative) at default distances.
    init = {
        "blink_gain": -15, "surv_gain": -15, "blink_safe": 4, "blink_bonus": 3,
        "surv_min": 4, "surv_max": 5, "fire_min": 4, "fire_max": 5,
    }
    print(f"CMA-ES 多维调参({len(MAGE_SPACE)}维),从坏区出发,留出验证防过拟合\n", flush=True)
    result = tune(
        MAGE_SPACE, train, val,
        generations=5, popsize=8, workers=6, init=init, on_generation=report,
    )
    shown = ", ".join(f"{s.name}={s.clamp(result.champion[s.name])}" for s in MAGE_SPACE)
    print(f"\n★ champion: [{shown}]")
    print(f"  train_score={result.train_score:+.3f}  val_score={result.val_score:+.3f}  "
          f"overfit_gap={result.overfit_gap:+.3f}")
    print(f"  train: {result.champion_train_fit[0]}")
    print(f"  val:   {result.champion_val_fit[0]}")


if __name__ == "__main__":
    _demo()

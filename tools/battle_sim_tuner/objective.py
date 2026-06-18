"""Shared battle AI tuning objective."""

from __future__ import annotations

from typing import Any

DEFAULT_MAX_ITERATIONS = 1500

FORMULA = (
    "6.0 * win_rate - 6.0 * loss_rate - 2.0 * stalemate_rate "
    "+ 0.5 * net_kills - 0.25 * avg_iterations / max_iterations"
)


def score_fitness(fitness: Any, max_iterations: int = DEFAULT_MAX_ITERATIONS) -> float:
    """Rank profiles by outcome first, then by combat advantage and speed."""
    iteration_scale = max(float(max_iterations), 1.0)
    return (
        6.0 * fitness.win_rate
        - 6.0 * fitness.loss_rate
        - 2.0 * fitness.stalemate_rate
        + 0.5 * fitness.net_kills
        - 0.25 * fitness.avg_iterations / iteration_scale
    )

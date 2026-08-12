"""Typed loader for the shared Battle AI score-weight tuning catalog."""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path


CATALOG_PATH = Path(__file__).with_name("score_weight_space.json")


@dataclass(frozen=True)
class ScoreWeightSpec:
    path: str
    minimum: int
    maximum: int


def load_score_weight_specs(
    catalog_path: Path = CATALOG_PATH,
) -> tuple[ScoreWeightSpec, ...]:
    payload = json.loads(catalog_path.read_text(encoding="utf-8"))
    if not isinstance(payload, list) or not payload:
        raise ValueError("score-weight catalog must be a non-empty JSON array")

    specs: list[ScoreWeightSpec] = []
    seen_paths: set[str] = set()
    for index, raw_spec in enumerate(payload):
        if not isinstance(raw_spec, dict):
            raise ValueError(f"score-weight catalog entry {index} must be an object")
        path = raw_spec.get("path")
        minimum = raw_spec.get("min")
        maximum = raw_spec.get("max")
        if not isinstance(path, str) or not path:
            raise ValueError(f"score-weight catalog entry {index} has an invalid path")
        if path in seen_paths:
            raise ValueError(f"score-weight catalog path {path!r} is duplicated")
        if (
            not isinstance(minimum, int)
            or isinstance(minimum, bool)
            or not isinstance(maximum, int)
            or isinstance(maximum, bool)
        ):
            raise ValueError(f"score-weight catalog path {path!r} must use integer bounds")
        if minimum > maximum:
            raise ValueError(
                f"score-weight catalog path {path!r} has min greater than max"
            )
        seen_paths.add(path)
        specs.append(ScoreWeightSpec(path, minimum, maximum))
    return tuple(specs)

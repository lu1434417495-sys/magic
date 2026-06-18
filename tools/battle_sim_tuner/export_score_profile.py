"""Export tuned BattleAiScoreProfile resources for game configuration.

Training/search code may work with a genome dictionary or a temporary
BattleSimProfileDef override profile. This module writes the durable artifact:
a standalone BattleAiScoreProfile .tres that can be committed under data/configs
and loaded by runtime wiring without requiring the trainer or a model.
"""

from __future__ import annotations

import argparse
import json
import os
from collections.abc import Mapping
from typing import Any

from .search_space import SCORE_PROFILE_DEFAULTS

SCORE_PROFILE_SCRIPT = "res://scripts/systems/battle/ai/BattleAiScoreProfile.cs"

_DICT_FIELDS = {"action_base_scores", "bucket_priorities"}
_STRING_NAME_FIELDS = {"meteor_friendly_fire_profile"}
_SCALAR_FIELDS = [
    key
    for key, value in SCORE_PROFILE_DEFAULTS.items()
    if key not in _DICT_FIELDS and not isinstance(value, Mapping)
]


def _fmt_scalar(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return repr(value)
    if isinstance(value, str):
        return f'"{value}"'
    raise TypeError(f"Unsupported score profile value: {value!r}")


def _fmt_string_name(value: Any) -> str:
    text = str(value or "")
    return f'&"{text}"'


def _fmt_string_name_int_dict(values: Mapping[str, Any]) -> str:
    if not values:
        return "{}"
    lines = ["{"]
    items = sorted((str(key), int(round(value))) for key, value in values.items())
    for index, (key, value) in enumerate(items):
        suffix = "," if index < len(items) - 1 else ""
        lines.append(f'&"{key}": {value}{suffix}')
    lines.append("}")
    return "\n".join(lines)


def normalize_score_profile_values(overrides: Mapping[str, Any] | None = None) -> dict[str, Any]:
    """Merge user overrides with shipped defaults and reject unknown fields."""
    values = {
        key: (dict(value) if isinstance(value, Mapping) else value)
        for key, value in SCORE_PROFILE_DEFAULTS.items()
    }
    if not overrides:
        return values

    unknown = sorted(set(overrides) - set(values))
    if unknown:
        raise KeyError(
            "Unknown BattleAiScoreProfile field(s): "
            + ", ".join(unknown)
            + ". Only score profile fields can be exported as a game config resource."
        )

    for key, value in overrides.items():
        if key in _DICT_FIELDS:
            if not isinstance(value, Mapping):
                raise TypeError(f"{key} must be an object/dictionary.")
            merged = dict(values[key])
            merged.update({str(k): int(round(v)) for k, v in value.items()})
            values[key] = merged
            continue
        if key in _STRING_NAME_FIELDS:
            values[key] = str(value)
            continue
        values[key] = int(round(value))
    return values


def render_score_profile_tres(overrides: Mapping[str, Any] | None = None) -> str:
    values = normalize_score_profile_values(overrides)
    lines = [
        '[gd_resource type="Resource" format=3]',
        "",
        f'[ext_resource type="Script" path="{SCORE_PROFILE_SCRIPT}" id="1_score"]',
        "",
        "[resource]",
        'script = ExtResource("1_score")',
    ]

    for key in _SCALAR_FIELDS:
        if key in _STRING_NAME_FIELDS:
            lines.append(f"{key} = {_fmt_string_name(values[key])}")
        else:
            lines.append(f"{key} = {_fmt_scalar(values[key])}")
    lines.append("action_base_scores = " + _fmt_string_name_int_dict(values["action_base_scores"]))
    lines.append("bucket_priorities = " + _fmt_string_name_int_dict(values["bucket_priorities"]))
    lines.append("")
    return "\n".join(lines)


def write_score_profile_tres(path: str, overrides: Mapping[str, Any] | None = None) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(render_score_profile_tres(overrides))


def _load_overrides(path: str) -> dict[str, Any]:
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    if isinstance(data, dict) and isinstance(data.get("champion"), dict):
        return data["champion"]
    if isinstance(data, dict) and isinstance(data.get("genome"), dict):
        return data["genome"]
    if isinstance(data, dict):
        return data
    raise TypeError("Expected JSON object, or an object with champion/genome.")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Export a tuned BattleAiScoreProfile .tres from a JSON genome."
    )
    parser.add_argument("--input-json", required=True, help="Genome JSON or tune result JSON.")
    parser.add_argument("--output", required=True, help="Output .tres path.")
    args = parser.parse_args()

    write_score_profile_tres(args.output, _load_overrides(args.input_json))
    print(f"wrote {args.output}")


if __name__ == "__main__":
    main()

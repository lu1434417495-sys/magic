"""Export tuned score weights as strict BattleSim profile JSON content.

Training/search code works with a genome dictionary. The durable artifact is a
code-owned ``battle_sim_profiles`` document that the production profile registry
can discover without loading a Godot Resource or retaining a source path.
"""

from __future__ import annotations

import argparse
import json
import os
from collections.abc import Mapping
from pathlib import Path
from typing import Any

_DICT_FIELDS = {"action_base_scores", "bucket_priorities"}
_STRING_NAME_FIELDS = {"meteor_friendly_fire_profile"}


def _score_profile_defaults() -> dict[str, Any]:
    baseline_path = (
        Path(__file__).resolve().parents[2]
        / "data"
        / "configs"
        / "json"
        / "battle_sim"
        / "profiles"
        / "baseline.json"
    )
    document = json.loads(baseline_path.read_text(encoding="utf-8"))
    return dict(document["entries"][0]["ai_score_profile"])


def normalize_score_profile_values(overrides: Mapping[str, Any] | None = None) -> dict[str, Any]:
    """Merge user overrides with shipped defaults and reject unknown fields."""
    defaults = _score_profile_defaults()
    values = {
        key: (dict(value) if isinstance(value, Mapping) else value)
        for key, value in defaults.items()
    }
    if not overrides:
        return values

    unknown = sorted(set(overrides) - set(values))
    if unknown:
        raise KeyError(
            "Unknown BattleAiScoreProfile field(s): "
            + ", ".join(unknown)
            + ". Only score profile fields can be exported as BattleSim profile JSON."
        )

    for key, value in overrides.items():
        if key in _DICT_FIELDS:
            if not isinstance(value, Mapping):
                raise TypeError(f"{key} must be an object/dictionary.")
            merged = dict(values[key])
            merged.update({str(k): int(round(v)) for k, v in value.items()})
            values[key] = merged
        elif key in _STRING_NAME_FIELDS:
            values[key] = str(value)
        elif isinstance(values[key], bool):
            values[key] = bool(value)
        elif isinstance(values[key], int):
            values[key] = int(round(value))
        else:
            values[key] = value
    return values


def render_score_profile_json(
    profile_id: str,
    overrides: Mapping[str, Any] | None = None,
) -> str:
    if not profile_id:
        raise ValueError("profile_id must not be empty.")
    document = {
        "schema": 1,
        "domain": "battle_sim_profiles",
        "family": "tuning",
        "templates": {},
        "entries": [
            {
                "profile_id": profile_id,
                "display_name": profile_id,
                "description": "BattleSim tuner candidate.",
                "ai_score_profile": normalize_score_profile_values(overrides),
                "override_patches": [],
            }
        ],
    }
    return json.dumps(document, ensure_ascii=False, indent=2) + "\n"


def write_score_profile_json(
    path: str,
    overrides: Mapping[str, Any] | None = None,
    *,
    profile_id: str | None = None,
) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    resolved_profile_id = profile_id or Path(path).stem
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(render_score_profile_json(resolved_profile_id, overrides))


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
        description="Export tuned weights as strict BattleSim profile JSON."
    )
    parser.add_argument("--input-json", required=True, help="Genome JSON or tune result JSON.")
    parser.add_argument("--output", required=True, help="Output .json path.")
    parser.add_argument("--profile-id", help="Profile ID; defaults to the output file stem.")
    args = parser.parse_args()

    write_score_profile_json(
        args.output,
        _load_overrides(args.input_json),
        profile_id=args.profile_id,
    )
    print(f"wrote {args.output}")


if __name__ == "__main__":
    main()

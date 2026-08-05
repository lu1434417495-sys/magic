#!/usr/bin/env python3
"""Inventory SkillDef resources and surface heuristic review candidates."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path


STRING_FIELD = re.compile(r'^([a-z_]+)\s*=\s*&?"([^"]*)"', re.MULTILINE)
INT_FIELD = re.compile(r"^([a-z_]+)\s*=\s*(-?\d+)\s*$", re.MULTILINE)
ARRAY_FIELD = re.compile(r"^([a-z_]+)\s*=\s*Array[^\n]*$", re.MULTILINE)
STRING_NAME = re.compile(r'&"([^"]+)"')
EFFECT_TYPE = re.compile(r'^effect_type\s*=\s*&"([^"]+)"', re.MULTILINE)
SECTION_RESOURCE = re.compile(r"^\[resource\]\s*$", re.MULTILINE)


def resource_body(text: str) -> str:
    matches = list(SECTION_RESOURCE.finditer(text))
    return text[matches[-1].end() :] if matches else text


def fields(text: str) -> tuple[dict[str, str], dict[str, int]]:
    body = resource_body(text)
    strings = {key: value for key, value in STRING_FIELD.findall(body)}
    ints = {key: int(value) for key, value in INT_FIELD.findall(body)}
    return strings, ints


def array_values(body: str, field: str) -> list[str]:
    for match in ARRAY_FIELD.finditer(body):
        if match.group(1) == field:
            return STRING_NAME.findall(match.group(0))
    return []


def inspect(path: Path) -> dict[str, object]:
    text = path.read_text(encoding="utf-8")
    body = resource_body(text)
    string_values, int_values = fields(text)
    skill_id = string_values.get("skill_id", "")
    max_level = int_values.get("max_level")
    non_core_max = int_values.get("non_core_max_level")
    growth_tier = string_values.get("growth_tier", "")
    learn_source = string_values.get("learn_source", "")
    has_level_overrides = (
        re.search(r"^level_overrides\s*=", text, re.MULTILINE) is not None
    )
    has_level_descriptions = (
        re.search(r"^level_description_configs\s*=", body, re.MULTILINE) is not None
    )
    candidates: list[str] = []

    if not skill_id:
        candidates.append("missing skill_id")
    if max_level is not None and max_level > 1:
        if not growth_tier:
            candidates.append("multi-level skill has no growth_tier")
        if not has_level_descriptions:
            candidates.append("multi-level skill has no level_description_configs")
        if not has_level_overrides:
            candidates.append("multi-level skill has no level_overrides")
    if (
        max_level is not None
        and non_core_max is not None
        and max_level > 1
        and max_level == non_core_max
    ):
        candidates.append("non_core_max_level equals max_level")

    return {
        "path": str(path),
        "skill_id": skill_id,
        "display_name": string_values.get("display_name", ""),
        "growth_tier": growth_tier,
        "learn_source": learn_source,
        "max_level": max_level,
        "non_core_max_level": non_core_max,
        "special_resolution_profile_id": string_values.get(
            "special_resolution_profile_id", ""
        ),
        "tags": array_values(body, "tags"),
        "effect_types": EFFECT_TYPE.findall(text),
        "has_level_overrides": has_level_overrides,
        "has_level_description_configs": has_level_descriptions,
        "candidates": candidates,
    }


def audit(repo_root: Path) -> dict[str, object]:
    skill_dir = repo_root / "data" / "configs" / "skills"
    if not skill_dir.is_dir():
        raise ValueError(f"skill directory not found: {skill_dir}")

    records = [inspect(path) for path in sorted(skill_dir.rglob("*.tres"))]
    ids: Counter[str] = Counter(
        str(record["skill_id"]) for record in records if record["skill_id"]
    )
    effect_types = Counter(
        effect for record in records for effect in record["effect_types"]
    )
    candidate_counts = Counter(
        candidate for record in records for candidate in record["candidates"]
    )
    return {
        "repo_root": str(repo_root.resolve()),
        "summary": {
            "resource_count": len(records),
            "unique_skill_id_count": len(ids),
            "duplicate_skill_ids": sorted(
                skill_id for skill_id, count in ids.items() if count > 1
            ),
            "candidate_counts": dict(sorted(candidate_counts.items())),
            "effect_type_counts": dict(sorted(effect_types.items())),
        },
        "skills": records,
        "notice": (
            "Heuristic static inventory only. Candidate flags are not validation "
            "errors or approved fixes; confirm with production source and tests."
        ),
    }


def run_self_test() -> int:
    sample = """[gd_resource type="Resource"]
[sub_resource type="Resource" id="effect"]
effect_type = &"damage"
[resource]
skill_id = &"sample"
display_name = "Sample"
max_level = 5
non_core_max_level = 3
growth_tier = &"basic"
tags = Array[StringName]([&"warrior", &"melee"])
level_overrides = {}
level_description_configs = {}
"""
    body = resource_body(sample)
    strings, ints = fields(sample)
    assert strings["skill_id"] == "sample"
    assert ints["max_level"] == 5
    assert array_values(body, "tags") == ["warrior", "melee"]
    assert EFFECT_TYPE.findall(sample) == ["damage"]
    print("self-test: PASS")
    return 0


def parse_args() -> argparse.Namespace:
    default_root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=default_root)
    parser.add_argument(
        "--summary-only",
        action="store_true",
        help="Print only repository facts and candidate counts.",
    )
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.self_test:
        return run_self_test()
    try:
        result = audit(args.repo_root)
    except (OSError, UnicodeError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    output = result
    if args.summary_only:
        output = {
            "repo_root": result["repo_root"],
            "summary": result["summary"],
            "notice": result["notice"],
        }
    json.dump(output, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

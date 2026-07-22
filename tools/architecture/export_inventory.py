#!/usr/bin/env python3
"""Export MAGICARCH100 SARIF results to deterministic review JSON."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any


INVENTORY_RULE = "MAGICARCH100"
ALLOWED_ERROR_RULE = "MAGICARCH001"
FATAL_ARCHITECTURE_RULES = {"MAGICARCH002", "MAGICARCH003", "MAGICARCH900"}
REQUIRED_PROPERTIES = {
    "SourceSymbol",
    "TargetSymbol",
    "SourceLayer",
    "TargetLayer",
    "SourcePath",
    "ReferenceKindsJson",
    "MatchedRuleIdsJson",
    "BaselinedRuleIdsJson",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("sarif", type=Path)
    parser.add_argument("output", type=Path)
    return parser.parse_args()


def parse_string_array(raw: Any, property_name: str) -> list[str]:
    if not isinstance(raw, str):
        raise ValueError(f"{property_name} must be a JSON array string")
    value = json.loads(raw)
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
        raise ValueError(f"{property_name} must contain only strings")
    canonical = sorted(set(value))
    if value != canonical:
        raise ValueError(f"{property_name} is not sorted and unique")
    return canonical


def get_custom_properties(result: dict[str, Any]) -> dict[str, Any]:
    properties = result.get("properties")
    if not isinstance(properties, dict):
        raise ValueError("inventory result has no properties object")
    custom = properties.get("customProperties")
    if not isinstance(custom, dict):
        raise ValueError("inventory result has no customProperties object")
    missing = sorted(REQUIRED_PROPERTIES.difference(custom))
    if missing:
        raise ValueError("inventory result is missing properties: " + ", ".join(missing))
    return custom


def get_location(result: dict[str, Any]) -> dict[str, Any]:
    locations = result.get("locations")
    if not isinstance(locations, list) or not locations:
        raise ValueError("inventory result has no evidence location")
    physical = locations[0].get("physicalLocation")
    if not isinstance(physical, dict):
        raise ValueError("inventory result has no physicalLocation")
    region = physical.get("region") or {}
    if not isinstance(region, dict):
        raise ValueError("inventory result region is invalid")
    return {
        "line": region.get("startLine", 0),
        "column": region.get("startColumn", 0),
    }


def main() -> int:
    args = parse_args()
    with args.sarif.open("r", encoding="utf-8-sig") as handle:
        sarif = json.load(handle)

    runs = sarif.get("runs")
    if not isinstance(runs, list) or not runs:
        raise ValueError("SARIF has no runs")

    inventory: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    fatal_results: list[str] = []
    for run in runs:
        for result in run.get("results", []):
            rule_id = result.get("ruleId", "")
            level = result.get("level", "")
            if rule_id in FATAL_ARCHITECTURE_RULES:
                fatal_results.append(rule_id)
                continue
            if level == "error" and rule_id not in {ALLOWED_ERROR_RULE, INVENTORY_RULE}:
                fatal_results.append(rule_id or "<missing ruleId>")
                continue
            if rule_id != INVENTORY_RULE:
                continue

            custom = get_custom_properties(result)
            key = (
                custom["SourceLayer"],
                custom["TargetLayer"],
                custom["SourceSymbol"],
                custom["TargetSymbol"],
            )
            entry = {
                "source_layer": key[0],
                "target_layer": key[1],
                "source_symbol": key[2],
                "target_symbol": key[3],
                "source_path": custom["SourcePath"],
                "reference_kinds": parse_string_array(
                    custom["ReferenceKindsJson"], "ReferenceKindsJson"
                ),
                "matched_rule_ids": parse_string_array(
                    custom["MatchedRuleIdsJson"], "MatchedRuleIdsJson"
                ),
                "baselined_rule_ids": parse_string_array(
                    custom["BaselinedRuleIdsJson"], "BaselinedRuleIdsJson"
                ),
                "evidence": get_location(result),
            }
            existing = inventory.get(key)
            if existing is not None and existing != entry:
                raise ValueError("conflicting inventory entries for " + " | ".join(key))
            inventory[key] = entry

    if fatal_results:
        counts = Counter(fatal_results)
        details = ", ".join(f"{rule}={count}" for rule, count in sorted(counts.items()))
        raise ValueError("inventory is invalid because fatal diagnostics exist: " + details)
    if not inventory:
        raise ValueError(
            "SARIF has no MAGICARCH100 inventory diagnostics; "
            "ensure layer_inventory_request.json was supplied to the analyzer"
        )

    entries = [inventory[key] for key in sorted(inventory)]
    pair_counts = Counter(
        f"{entry['source_layer']}->{entry['target_layer']}" for entry in entries
    )
    payload = {
        "schema_version": 1,
        "entry_count": len(entries),
        "layer_pair_counts": dict(sorted(pair_counts.items())),
        "entries": entries,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2, sort_keys=True)
        handle.write("\n")
    print(f"exported {len(entries)} cross-layer dependencies to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

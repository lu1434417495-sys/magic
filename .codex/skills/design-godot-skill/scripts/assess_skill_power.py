#!/usr/bin/env python3
"""Bounded arithmetic probes for skill design, not a second battle simulator.

Consumes explicit marginal effect facts. Never discovers skills, parses gameplay
JSON, derives hit/save odds, runs Godot, or declares a skill balanced.
"""
from __future__ import annotations

import argparse
from bisect import bisect_left, bisect_right
import hashlib
import json
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
BASELINE = Path(__file__).resolve().parents[1] / "assets/power-assessment/baseline.json"
RESOURCES = ("mp", "stamina", "aura")
METRICS = (
    "damage_dealt_hp", "damage_prevented_hp", "effective_healing_hp",
    "enemy_ap_denied", "ally_ap_gained", "safe_move_cells",
)


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def keys(value: dict, allowed: set[str], label: str) -> None:
    if not isinstance(value, dict):
        raise ValueError(f"{label}: expected object")
    unknown = value.keys() - allowed
    if unknown:
        raise ValueError(f"{label}: unsupported fields {sorted(unknown)}")


def number(value, label: str, minimum: float = 0, integer: bool = False):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{label}: expected finite number")
    if value < minimum or (integer and not isinstance(value, int)):
        raise ValueError(f"{label}: expected {'integer' if integer else 'number'} >= {minimum}")
    return value


def tu(value, label: str, step: int, minimum: int = 0) -> int:
    number(value, label, minimum, integer=True)
    if value % step:
        raise ValueError(f"{label}: must be a multiple of {step} TU")
    return value


def metrics(value: dict, label: str) -> dict:
    keys(value, set(METRICS), label)
    return {key: number(amount, f"{label}.{key}") for key, amount in value.items()}


def normalized_hash(path: Path) -> str:
    # Ignore checkout newline conversion; no source parsing or directory discovery.
    return hashlib.sha256(path.read_text(encoding="utf-8-sig").encode("utf-8")).hexdigest()


def stale_sources(baseline: dict, root: Path) -> list[str]:
    stale = []
    sources = baseline["source_sha256"]
    if not isinstance(sources, dict):
        raise ValueError("source_sha256: expected path/hash object")
    for relative, expected in sources.items():
        if not isinstance(relative, str) or not isinstance(expected, str) or len(expected) != 64:
            raise ValueError("source_sha256: expected relative path and SHA256 digest")
        path = (root / relative).resolve()
        if not path.is_relative_to(root.resolve()):
            raise ValueError("baseline source must stay inside repository")
        if not path.is_file() or normalized_hash(path) != expected:
            stale.append(relative)
    return stale


def validate_case(case: dict, baseline: dict) -> None:
    keys(case, {"id", "basis", "assumptions", "source_sha256", "duration_tu", "cooldown_tu", "costs",
                "scenario", "on_cast", "streams", "max_casts"}, "case")
    if not isinstance(case.get("id"), str) or not case["id"].strip():
        raise ValueError("case.id: nonempty string required")
    if case.get("basis") not in {"controlled_assumption", "canonical_preview", "measured"}:
        raise ValueError("case.basis: controlled_assumption/canonical_preview/measured required")
    if not isinstance(case.get("assumptions"), list) or not case["assumptions"]:
        raise ValueError("case.assumptions: record evidence and model limitations")
    if not isinstance(case.get("source_sha256"), dict):
        raise ValueError("case.source_sha256: explicit dependency hashes required (empty for assumptions)")
    if case["basis"] != "controlled_assumption" and not case["source_sha256"]:
        raise ValueError("canonical/measured facts require pinned evidence and dependency files")
    step = baseline["tu_step"]
    for name in ("duration_tu", "cooldown_tu"):
        tu(case[name], name, step)
    keys(case["costs"], {"ap", *RESOURCES}, "costs")
    number(case["costs"]["ap"], "costs.ap", integer=True)
    for resource in RESOURCES:
        number(case["costs"][resource], f"costs.{resource}", integer=True)
    if case.get("max_casts") is not None:
        number(case["max_casts"], "max_casts", 1, integer=True)
    scenario = case["scenario"]
    keys(scenario, {"horizon_tu", "caster_interval_tu", "ap_per_activation",
                    "resource_budget", "reference_action_damage_hp",
                    "alternative_damage_per_ap", "same_tu_order"}, "scenario")
    tu(scenario["horizon_tu"], "horizon_tu", step, step)
    tu(scenario["caster_interval_tu"], "caster_interval_tu", step, step)
    if scenario["horizon_tu"] > 100000:
        raise ValueError("horizon_tu: probe exceeds 100000 TU bound")
    number(scenario["ap_per_activation"], "ap_per_activation", 1, integer=True)
    number(scenario["reference_action_damage_hp"], "reference_action_damage_hp", 0.000001)
    number(scenario["alternative_damage_per_ap"], "alternative_damage_per_ap")
    if scenario["same_tu_order"] not in {"cast_first", "threat_first"}:
        raise ValueError("same_tu_order: specify cast_first or threat_first")
    keys(scenario["resource_budget"], set(RESOURCES), "resource_budget")
    for resource in RESOURCES:
        number(scenario["resource_budget"][resource], f"resource_budget.{resource}", integer=True)
    metrics(case["on_cast"], "on_cast")
    if not isinstance(case["streams"], list) or len(case["streams"]) > 100:
        raise ValueError("streams: bounded list required")
    for stream in case["streams"]:
        keys(stream, {"id", "count", "interval_tu", "first_event_tu", "marginal_per_event"}, "stream")
        number(stream["count"], "stream.count", 1, integer=True)
        tu(stream["interval_tu"], "stream.interval_tu", step, step)
        tu(stream["first_event_tu"], "stream.first_event_tu", step)
        metrics(stream["marginal_per_event"], "marginal_per_event")


def active_at(t: int, casts: list[int], duration: int, order: str) -> bool:
    index = (bisect_right(casts, t) if order == "cast_first" else bisect_left(casts, t)) - 1
    return index >= 0 and t < casts[index] + duration


def coverage(casts: list[int], duration: int, horizon: int) -> int:
    total = 0
    end = 0
    for start in casts:
        new_end = min(start + duration, horizon)
        total += max(0, new_end - max(start, end))
        end = max(end, new_end)
    return total


def effect_totals(case: dict, casts: list[int]) -> tuple[dict, list]:
    totals = {key: case["on_cast"].get(key, 0) * len(casts) for key in METRICS}
    scenario = case["scenario"]
    stream_results = []
    for stream in case["streams"]:
        events = range(stream["first_event_tu"], scenario["horizon_tu"], stream["interval_tu"])
        affected = sum(active_at(t, casts, case["duration_tu"], scenario["same_tu_order"])
                       for t in events) * stream["count"]
        for key, delta in stream["marginal_per_event"].items():
            totals[key] += affected * delta
        stream_results.append({"id": stream["id"], "affected_events": affected})
    return totals, stream_results


def evaluate(case: dict, baseline: dict) -> dict:
    validate_case(case, baseline)
    scenario, costs = case["scenario"], case["costs"]
    horizon, interval = scenario["horizon_tu"], scenario["caster_interval_tu"]
    # Policy: instant cast at first activation t=0, at most once per activation;
    # recast at earliest legal activation, even if buff is still active.
    cycle = max(1, math.ceil(case["cooldown_tu"] / interval)) * interval
    remaining = dict(scenario["resource_budget"])
    casts = []
    stop = "horizon"
    failed_at = None
    if costs["ap"] > scenario["ap_per_activation"]:
        stop = "insufficient_ap"
        failed_at = 0
    else:
        for t in range(0, horizon, cycle):
            if case.get("max_casts") and len(casts) >= case["max_casts"]:
                stop = "cast_limit"
                break
            exhausted = [r for r in RESOURCES if remaining[r] < costs[r]]
            if exhausted:
                stop = "insufficient_" + ",".join(exhausted)
                failed_at = t
                break
            casts.append(t)
            for resource in RESOURCES:
                remaining[resource] -= costs[resource]
    totals, streams = effect_totals(case, casts)
    single, single_streams = effect_totals(case, casts[:1])
    covered = coverage(casts, case["duration_tu"], horizon)
    spent = {r: scenario["resource_budget"][r] - remaining[r] for r in RESOURCES}
    spent["ap"] = len(casts) * costs["ap"]
    reference = scenario["reference_action_damage_hp"]
    return {
        "id": case["id"], "basis": case["basis"], "assumptions": case["assumptions"],
        "policy": "instant; at most one cast/activation; earliest legal recast; no regeneration",
        "same_tu_order": scenario["same_tu_order"],
        "actual_cycle_tu": cycle,
        "duration_in_reference_turns": case["duration_tu"] / baseline["reference_turn_tu"],
        "ideal_cycle_coverage": min(1, case["duration_tu"] / cycle),
        "gap_per_ideal_cycle_tu": max(0, cycle - case["duration_tu"]),
        "refresh_overlap_tu": max(0, case["duration_tu"] - cycle),
        "cast_times_tu": casts, "cast_count": len(casts), "stop_reason": stop,
        "first_failed_cast_tu": failed_at,
        "covered_tu": covered, "horizon_coverage": covered / horizon,
        "spent": spent, "resource_remaining": remaining,
        "single_cast_marginal": single, "single_cast_streams": single_streams,
        "horizon_marginal": totals, "streams": streams,
        "hp_lanes_in_reference_actions": {
            key: value / reference for key, value in totals.items() if key.endswith("_hp")
        },
        "metric_per_ap": {k: v / spent["ap"] if spent["ap"] else None for k, v in totals.items()},
        "metric_per_mp": {k: v / spent["mp"] if spent["mp"] else None for k, v in totals.items()},
        "foregone_alternative_damage_hp": spent["ap"] * scenario["alternative_damage_per_ap"],
        "balance_verdict": "requires_role_anchor_comparison",
    }


def run(packet: dict, baseline: dict, root: Path) -> dict:
    if not isinstance(packet, dict) or not isinstance(baseline, dict):
        raise ValueError("assessment and baseline must be JSON objects")
    if packet.get("schema_version") != 1 or baseline.get("schema_version") != 1:
        raise ValueError("only assessment schema_version 1 is supported")
    keys(packet, {"schema_version", "cases"}, "packet")
    stale = stale_sources(baseline, root)
    if stale:
        return {"status": "baseline_stale", "baseline_version": baseline["baseline_version"],
                "changed_sources": stale, "cases": []}
    if not isinstance(packet["cases"], list) or not 1 <= len(packet["cases"]) <= 100:
        raise ValueError("cases: expected 1..100 cases")
    for case in packet["cases"]:
        validate_case(case, baseline)
        stale = stale_sources(case, root)
        if stale:
            return {"status": "evidence_stale", "case_id": case["id"],
                    "changed_sources": stale, "cases": []}
    results = [evaluate(case, baseline) for case in packet["cases"]]
    if len({case["id"] for case in results}) != len(results):
        raise ValueError("case IDs must be unique")
    return {"status": "arithmetic_only", "baseline_version": baseline["baseline_version"],
            "calibration": baseline["calibration"], "cases": results}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--baseline", type=Path, default=BASELINE)
    parser.add_argument("--repo-root", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        report = run(read_json(args.input), read_json(args.baseline), args.repo_root)
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Assessment rejected: {error}\n")
    rendered = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    else:
        print(rendered, end="")
    return 2 if report["status"] in {"baseline_stale", "evidence_stale"} else 0


if __name__ == "__main__":
    raise SystemExit(main())

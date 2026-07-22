#!/usr/bin/env python3
"""Build a low-token analysis packet from a full battle simulation report."""

from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


FACTION_COUNTER_FIELDS = (
	"action_counts",
	"skill_attempt_counts",
	"skill_success_counts",
)


def parse_args() -> argparse.Namespace:
	parser = argparse.ArgumentParser(
		description=(
			"Create a compact analysis packet for battle simulation outputs. "
			"The packet is designed for human review and LLM handoff."
		)
	)
	parser.add_argument(
		"--report",
		required=True,
		help="Path to a full battle simulation report JSON.",
	)
	parser.add_argument(
		"--output-dir",
		default="",
		help=(
			"Directory for generated packet files. "
			"Defaults to a sibling folder next to the report."
		),
	)
	parser.add_argument(
		"--max-focus-traces",
		type=int,
		default=24,
		help="Maximum number of trace rows to export into focus_traces.jsonl.",
	)
	parser.add_argument(
		"--max-traces-per-profile",
		type=int,
		default=6,
		help="Maximum number of focus traces to keep per profile before global capping.",
	)
	parser.add_argument(
		"--top-skills",
		type=int,
		default=5,
		help="Maximum number of non-zero skill deltas to keep per comparison.",
	)
	parser.add_argument(
		"--top-actions",
		type=int,
		default=5,
		help="Maximum number of non-zero action deltas to keep per comparison.",
	)
	parser.add_argument(
		"--include-baseline-traces",
		action="store_true",
		help="Include baseline profile traces in focus_traces.jsonl for direct side-by-side comparison.",
	)
	return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
	return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
	path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def infer_godot_user_root(report_path: Path) -> Path | None:
	parts = report_path.resolve().parts
	for index, part in enumerate(parts):
		if part == "app_userdata" and index + 1 < len(parts):
			return Path(*parts[: index + 2])
	return None


def resolve_user_path(raw_path: str, report_path: Path) -> Path | None:
	if not raw_path:
		return None
	if raw_path.startswith("user://"):
		user_root = infer_godot_user_root(report_path)
		if user_root is None:
			return None
		relative_path = raw_path[len("user://") :].lstrip("/\\")
		return user_root / relative_path
	return Path(raw_path)


def normalize_profile_id(profile_entry: dict[str, Any]) -> str:
	profile = profile_entry.get("profile", {})
	return str(profile.get("profile_id", ""))


def as_int(value: Any, default: int = 0) -> int:
	try:
		return int(value)
	except (TypeError, ValueError):
		return default


def as_float(value: Any, default: float = 0.0) -> float:
	try:
		return float(value)
	except (TypeError, ValueError):
		return default


def infer_report_shape(report: dict[str, Any]) -> str:
	profile_entries = report.get("profile_entries", [])
	if isinstance(profile_entries, list) and profile_entries:
		return "profile_entries"
	if isinstance(report.get("runs", []), list):
		return "standalone_runs"
	return "unknown"


def build_effective_scenario(report: dict[str, Any], report_path: Path) -> dict[str, Any]:
	raw_scenario = report.get("scenario", {})
	if isinstance(raw_scenario, dict) and raw_scenario:
		scenario = dict(raw_scenario)
	else:
		scenario_id = str(report.get("scenario_id", "") or report.get("benchmark_id", "") or report_path.stem)
		scenario = {
			"scenario_id": scenario_id,
			"display_name": str(report.get("display_name", "")),
			"manual_policy": str(report.get("manual_policy", "")),
		}
	scenario["report_shape"] = infer_report_shape(report)
	if "requested_run_count" in report:
		scenario["requested_run_count"] = as_int(report.get("requested_run_count", 0))
	if "start_seed" in report:
		scenario["start_seed"] = report.get("start_seed")
	if "start_seed_source" in report:
		scenario["start_seed_source"] = report.get("start_seed_source")
	if "timeout_seconds" in report:
		scenario["timeout_seconds"] = as_int(report.get("timeout_seconds", 0))
	if "timed_out" in report:
		scenario["timed_out"] = bool(report.get("timed_out", False))
	return scenario


def count_total_runs(profile_entries: list[dict[str, Any]]) -> int:
	return sum(len(entry.get("runs", [])) for entry in profile_entries)


def count_total_traces(profile_entries: list[dict[str, Any]]) -> int:
	return sum(
		len(run.get("ai_turn_traces", []))
		for entry in profile_entries
		for run in entry.get("runs", [])
	)


def count_completed_traces(profile_entries: list[dict[str, Any]]) -> int:
	trace_count = 0
	for entry in profile_entries:
		runs = [run for run in entry.get("runs", []) if isinstance(run, dict)]
		if any(not is_completion_classification_known(run) for run in runs):
			continue
		trace_count += sum(len(run.get("ai_turn_traces", [])) for run in runs if is_completed_run(run))
	return trace_count


def infer_alive_count(metrics: dict[str, Any], faction_id: str) -> int:
	units = metrics.get("units", {})
	factions = metrics.get("factions", {})
	if not isinstance(units, dict) or not isinstance(factions, dict):
		return 0
	unit_count = 0
	for unit_entry in units.values():
		if isinstance(unit_entry, dict) and str(unit_entry.get("faction_id", "")) == faction_id:
			unit_count += 1
	faction_metrics = factions.get(faction_id, {})
	death_count = as_int(faction_metrics.get("death_count", 0)) if isinstance(faction_metrics, dict) else 0
	return max(unit_count - death_count, 0)


def normalize_run_for_packet(run: dict[str, Any], report: dict[str, Any], run_index: int) -> dict[str, Any]:
	normalized = dict(run)
	metrics = normalized.get("metrics", {})
	if not isinstance(metrics, dict):
		metrics = {}
	if ("units" not in metrics or not isinstance(metrics.get("units"), dict)) and isinstance(normalized.get("units", {}), dict):
		metrics["units"] = normalized.get("units", {})
	if ("factions" not in metrics or not isinstance(metrics.get("factions"), dict)) and isinstance(normalized.get("factions", {}), dict):
		metrics["factions"] = normalized.get("factions", {})
	normalized["metrics"] = metrics
	if "battle_ended" not in normalized:
		runs = report.get("runs", [])
		reported_ended_count = as_int(report.get("ended_count", -1), -1)
		winner_count = sum(
			1
			for candidate in runs
			if isinstance(candidate, dict) and bool(str(candidate.get("winner_faction_id", "")))
		) if isinstance(runs, list) else 0
		all_runs_completed = (
			isinstance(runs, list)
			and len(runs) > 0
			and reported_ended_count == len(runs)
		)
		has_winner = bool(str(normalized.get("winner_faction_id", "")))
		normalized["battle_ended"] = has_winner or all_runs_completed
		normalized["_packet_completion_known"] = (
			has_winner
			or all_runs_completed
			or (reported_ended_count >= 0 and reported_ended_count == winner_count)
		)
	else:
		normalized["_packet_completion_known"] = True
	if "ally_alive" not in normalized:
		normalized["ally_alive"] = infer_alive_count(metrics, "player")
	if "enemy_alive" not in normalized:
		normalized["enemy_alive"] = infer_alive_count(metrics, "hostile")
	if "run_index" not in normalized:
		normalized["run_index"] = run_index
	return normalized


def update_counter_from_skill_report(
	counter: Counter[str],
	skill_report: dict[str, Any],
	value_key: str,
) -> None:
	for skill_id, entry in skill_report.items():
		if not isinstance(entry, dict):
			continue
		value = as_int(entry.get(value_key, 0))
		if value:
			counter[str(skill_id)] += value


def build_trace_action_counts_by_faction(runs: list[dict[str, Any]]) -> dict[str, dict[str, int]]:
	counts: dict[str, Counter[str]] = defaultdict(Counter)
	for run in runs:
		for trace in run.get("ai_turn_traces", []):
			if not isinstance(trace, dict):
				continue
			faction_id = str(trace.get("faction_id", ""))
			action_id = str(trace.get("action_id", ""))
			if faction_id and action_id:
				counts[faction_id][action_id] += 1
	return {faction_id: dict(sorted(counter.items())) for faction_id, counter in sorted(counts.items())}


def build_trace_command_counts_by_faction(runs: list[dict[str, Any]]) -> dict[str, dict[str, int]]:
	counts: dict[str, Counter[str]] = defaultdict(Counter)
	for run in runs:
		for trace in run.get("ai_turn_traces", []):
			if not isinstance(trace, dict):
				continue
			faction_id = str(trace.get("faction_id", ""))
			command_type = ""
			command = trace.get("command", {})
			if isinstance(command, dict):
				command_type = str(command.get("command_type", "") or command.get("type", ""))
			score_input = trace.get("score_input", {})
			if not command_type and isinstance(score_input, dict):
				command_type = str(score_input.get("command_type", ""))
			if faction_id and command_type:
				counts[faction_id][command_type] += 1
	return {faction_id: dict(sorted(counter.items())) for faction_id, counter in sorted(counts.items())}


def merge_faction_metric_totals(runs: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
	numeric_totals: dict[str, dict[str, float]] = defaultdict(lambda: defaultdict(float))
	counter_totals: dict[str, dict[str, Counter[str]]] = defaultdict(
		lambda: {field: Counter() for field in FACTION_COUNTER_FIELDS}
	)
	faction_ids: set[str] = set()
	for run in runs:
		metrics = run.get("metrics", {})
		factions = metrics.get("factions", {}) if isinstance(metrics, dict) else {}
		if not isinstance(factions, dict):
			continue
		for faction_id, faction_entry in factions.items():
			if not isinstance(faction_entry, dict):
				continue
			faction_key = str(faction_id)
			faction_ids.add(faction_key)
			for key, value in faction_entry.items():
				key_text = str(key)
				if key_text in FACTION_COUNTER_FIELDS:
					if not isinstance(value, dict):
						continue
					for counter_key, counter_value in value.items():
						if isinstance(counter_value, bool):
							continue
						if isinstance(counter_value, (int, float)):
							counter_totals[faction_key][key_text][str(counter_key)] += counter_value
					continue
				if isinstance(value, bool):
					continue
				if isinstance(value, (int, float)):
					numeric_totals[faction_key][key_text] += value
	result: dict[str, dict[str, Any]] = {}
	for faction_id in sorted(faction_ids):
		faction_result: dict[str, Any] = {
			key: int(value) if float(value).is_integer() else value
			for key, value in sorted(numeric_totals[faction_id].items())
		}
		for field in FACTION_COUNTER_FIELDS:
			faction_result[field] = {
				key: int(value) if float(value).is_integer() else value
				for key, value in sorted(counter_totals[faction_id][field].items())
			}
		result[faction_id] = faction_result
	return result


def infer_unit_role(unit_id: str, display_name: str) -> str:
	label = f"{unit_id} {display_name}".lower()
	for role in ["mage", "archer", "sword", "wolf", "harrier", "beast", "warrior"]:
		if role in label:
			return role
	return "other"


def collect_unit_totals(report: dict[str, Any], runs: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
	per_unit_summary = report.get("per_unit_summary", {})
	if isinstance(per_unit_summary, dict) and per_unit_summary:
		return {
			str(unit_id): dict(unit_entry)
			for unit_id, unit_entry in per_unit_summary.items()
			if isinstance(unit_entry, dict)
		}
	unit_totals: dict[str, dict[str, Any]] = {}
	for run in runs:
		metrics = run.get("metrics", {})
		units = metrics.get("units", {}) if isinstance(metrics, dict) else {}
		if not isinstance(units, dict):
			continue
		for unit_id, unit_entry in units.items():
			if not isinstance(unit_entry, dict):
				continue
			unit_id_text = str(unit_id)
			total = unit_totals.setdefault(
				unit_id_text,
				{
					"display_name": str(unit_entry.get("display_name", "")),
					"faction_id": str(unit_entry.get("faction_id", "")),
					"runs": 0,
					"turn_count": 0,
					"total_damage_done": 0,
					"total_damage_taken": 0,
					"kill_count": 0,
					"death_count": 0,
				},
			)
			total["runs"] += 1
			for key in ["turn_count", "total_damage_done", "total_damage_taken", "kill_count", "death_count"]:
				total[key] += as_int(unit_entry.get(key, 0))
	return unit_totals


def build_unit_contribution_summary(
	report: dict[str, Any],
	runs: list[dict[str, Any]],
	faction_metric_totals: dict[str, dict[str, Any]],
	limit: int = 10,
) -> dict[str, Any]:
	unit_totals = collect_unit_totals(report, runs)
	role_totals: dict[tuple[str, str], dict[str, Any]] = {}
	unit_rows: list[dict[str, Any]] = []
	for unit_id, unit_entry in unit_totals.items():
		faction_id = str(unit_entry.get("faction_id", ""))
		display_name = str(unit_entry.get("display_name", ""))
		role = infer_unit_role(unit_id, display_name)
		row = {
			"unit_id": unit_id,
			"display_name": display_name,
			"faction_id": faction_id,
			"role": role,
			"damage_done": as_int(unit_entry.get("total_damage_done", 0)),
			"damage_taken": as_int(unit_entry.get("total_damage_taken", 0)),
			"kills": as_int(unit_entry.get("kill_count", 0)),
			"deaths": as_int(unit_entry.get("death_count", 0)),
			"turns": as_int(unit_entry.get("turn_count", 0)),
		}
		unit_rows.append(row)
		role_key = (faction_id, role)
		role_total = role_totals.setdefault(
			role_key,
			{
				"faction_id": faction_id,
				"role": role,
				"unit_count": 0,
				"damage_done": 0,
				"damage_taken": 0,
				"kills": 0,
				"deaths": 0,
				"turns": 0,
			},
		)
		role_total["unit_count"] += 1
		for key in ["damage_done", "damage_taken", "kills", "deaths", "turns"]:
			role_total[key] += row[key]
	role_rows = []
	for role_total in role_totals.values():
		faction_id = str(role_total.get("faction_id", ""))
		faction_damage = as_float(faction_metric_totals.get(faction_id, {}).get("total_damage_done", 0))
		damage_share = float(role_total.get("damage_done", 0)) / faction_damage if faction_damage > 0 else 0.0
		role_row = dict(role_total)
		role_row["damage_share"] = round(damage_share, 4)
		role_rows.append(role_row)
	return {
		"role_totals": sorted(role_rows, key=lambda row: (str(row.get("faction_id", "")), -as_int(row.get("damage_done", 0)), str(row.get("role", "")))),
		"top_damage_units": sorted(unit_rows, key=lambda row: (-as_int(row.get("damage_done", 0)), str(row.get("unit_id", ""))))[:limit],
		"top_damage_taken_units": sorted(unit_rows, key=lambda row: (-as_int(row.get("damage_taken", 0)), str(row.get("unit_id", ""))))[:limit],
	}


def build_standalone_summary(report: dict[str, Any], runs: list[dict[str, Any]]) -> dict[str, Any]:
	source_summary = {
		"profile_id": "standalone",
		"display_name": "Standalone report",
		"requested_run_count": as_int(report.get("requested_run_count", len(runs)), len(runs)),
		"reported_ended_count": as_int(report.get("ended_count", -1), -1),
		"timed_out": bool(report.get("timed_out", False)),
		"elapsed_seconds": as_float(report.get("elapsed_seconds", 0.0)),
	}
	return build_completed_only_summary(runs, source_summary, action_counts_by_faction=True)


def build_effective_profile_entries(report: dict[str, Any]) -> list[dict[str, Any]]:
	raw_profile_entries = report.get("profile_entries", [])
	if isinstance(raw_profile_entries, list) and raw_profile_entries:
		return [entry for entry in raw_profile_entries if isinstance(entry, dict)]
	raw_runs = report.get("runs", [])
	if not isinstance(raw_runs, list) or not raw_runs:
		return []
	runs = [
		normalize_run_for_packet(run, report, index)
		for index, run in enumerate(raw_runs)
		if isinstance(run, dict)
	]
	if not runs:
		return []
	return [
		{
			"profile": {
				"profile_id": "standalone",
				"display_name": "Standalone report",
				"description": "Synthetic profile entry derived from a top-level runs report.",
			},
			"summary": build_standalone_summary(report, runs),
			"runs": runs,
		}
	]


def sorted_counter_items(counter: Counter[str], limit: int | None = None) -> list[dict[str, Any]]:
	items = sorted(counter.items(), key=lambda item: (-item[1], item[0]))
	if limit is not None:
		items = items[:limit]
	return [{"id": key, "count": value} for key, value in items]


def sorted_delta_items(delta_map: dict[str, Any], limit: int) -> list[dict[str, Any]]:
	items: list[tuple[str, float]] = []
	for raw_key, raw_value in delta_map.items():
		value = float(raw_value)
		if value == 0:
			continue
		items.append((str(raw_key), value))
	items.sort(key=lambda item: (-abs(item[1]), item[0]))
	items = items[:limit]
	return [{"id": key, "delta": value} for key, value in items]


def build_run_action_counts(traces: list[dict[str, Any]]) -> Counter[str]:
	counter: Counter[str] = Counter()
	for trace in traces:
		action_id = str(trace.get("action_id", ""))
		if action_id:
			counter[action_id] += 1
	return counter


def build_run_skill_counts(metrics: dict[str, Any]) -> Counter[str]:
	counter: Counter[str] = Counter()
	for unit_entry in metrics.get("units", {}).values():
		if not isinstance(unit_entry, dict):
			continue
		skill_counts = unit_entry.get("skill_success_counts", {})
		if "skill_success_counts" not in unit_entry or not isinstance(skill_counts, dict):
			skill_counts = unit_entry.get("skill_successes", {})
		if not isinstance(skill_counts, dict):
			continue
		for skill_id, count in skill_counts.items():
			counter[str(skill_id)] += int(count)
	return counter


def build_run_skill_attempt_counts(metrics: dict[str, Any]) -> Counter[str]:
	counter: Counter[str] = Counter()
	for unit_entry in metrics.get("units", {}).values():
		if not isinstance(unit_entry, dict):
			continue
		skill_counts = unit_entry.get("skill_attempt_counts", {})
		if "skill_attempt_counts" not in unit_entry or not isinstance(skill_counts, dict):
			skill_counts = unit_entry.get("skill_attempts", {})
		if not isinstance(skill_counts, dict):
			continue
		for skill_id, count in skill_counts.items():
			counter[str(skill_id)] += int(count)
	return counter


def build_failure_counter(
	attempt_counts: Counter[str] | dict[str, Any],
	success_counts: Counter[str] | dict[str, Any],
) -> Counter[str]:
	failures: Counter[str] = Counter()
	keys = set(str(key) for key in attempt_counts.keys()) | set(str(key) for key in success_counts.keys())
	for skill_id in keys:
		attempts = int(attempt_counts.get(skill_id, 0))
		successes = int(success_counts.get(skill_id, 0))
		failure_count = max(attempts - successes, 0)
		if failure_count > 0:
			failures[skill_id] = failure_count
	return failures


def build_skill_counter_snapshot(
	success_counts: dict[str, Any],
	attempt_counts: dict[str, Any],
	limit: int = 5,
) -> dict[str, Any]:
	success_counter = Counter({str(key): int(value) for key, value in success_counts.items()})
	attempt_counter = Counter({str(key): int(value) for key, value in attempt_counts.items()})
	failure_counter = build_failure_counter(attempt_counter, success_counter)
	return {
		"success_totals": dict(sorted(success_counter.items())),
		"attempt_totals": dict(sorted(attempt_counter.items())),
		"failure_totals": dict(sorted(failure_counter.items())),
		"top_skill_successes": sorted_counter_items(success_counter, limit=limit),
		"top_skill_attempts": sorted_counter_items(attempt_counter, limit=limit),
		"top_skill_failures": sorted_counter_items(failure_counter, limit=limit),
	}


def build_profile_skill_counters(runs: list[dict[str, Any]], summary: dict[str, Any], limit: int = 5) -> dict[str, Any]:
	del runs
	success_counts = summary.get("skill_usage_totals", {})
	attempt_counts = summary.get("skill_attempt_totals", {})
	return build_skill_counter_snapshot(
		success_counts if isinstance(success_counts, dict) else {},
		attempt_counts if isinstance(attempt_counts, dict) else {},
		limit=limit,
	)


def build_run_digest(run: dict[str, Any]) -> dict[str, Any]:
	traces = run.get("ai_turn_traces", [])
	metrics = run.get("metrics", {})
	skill_success_counts = build_run_skill_counts(metrics)
	skill_attempt_counts = build_run_skill_attempt_counts(metrics)
	skill_failure_counts = build_failure_counter(skill_attempt_counts, skill_success_counts)
	return {
		"seed": int(run.get("seed", 0)),
		"battle_ended": bool(run.get("battle_ended", False)),
		"winner_faction_id": str(run.get("winner_faction_id", "")),
		"final_tu": int(run.get("final_tu", 0)),
		"iterations": int(run.get("iterations", 0)),
		"timeline_steps": int(run.get("timeline_steps", 0)),
		"idle_loops": int(run.get("idle_loops", 0)),
		"ally_alive": int(run.get("ally_alive", 0)),
		"enemy_alive": int(run.get("enemy_alive", 0)),
		"trace_count": len(traces),
		"top_action_choices": sorted_counter_items(build_run_action_counts(traces), limit=5),
		"top_skill_successes": sorted_counter_items(skill_success_counts, limit=5),
		"top_skill_attempts": sorted_counter_items(skill_attempt_counts, limit=5),
		"top_skill_failures": sorted_counter_items(skill_failure_counts, limit=5),
	}


def build_rate_dict(counts: dict[str, int], denominator: int) -> dict[str, float]:
	if denominator <= 0:
		return {}
	return {
		str(key): float(value) / float(denominator)
		for key, value in sorted(counts.items(), key=lambda item: item[0])
	}


def is_completed_run(run: dict[str, Any]) -> bool:
	return bool(run.get("battle_ended", False))


def is_completion_classification_known(run: dict[str, Any]) -> bool:
	return bool(run.get("_packet_completion_known", "battle_ended" in run))


def completed_numeric_mean(runs: list[dict[str, Any]], field: str) -> float | None:
	if not runs:
		return None
	values: list[float] = []
	for run in runs:
		value = run.get(field)
		if isinstance(value, bool) or not isinstance(value, (int, float)):
			return None
		values.append(float(value))
	return sum(values) / float(len(values))


def get_run_metric_map(run: dict[str, Any], field: str) -> dict[str, Any] | None:
	metrics = run.get("metrics", {})
	if not isinstance(metrics, dict):
		return None
	value = metrics.get(field)
	return value if isinstance(value, dict) else None


def has_complete_skill_metrics(run: dict[str, Any]) -> bool:
	units = get_run_metric_map(run, "units")
	if units is None:
		return False
	for unit_entry in units.values():
		if not isinstance(unit_entry, dict):
			return False
		attempts = unit_entry.get("skill_attempt_counts", unit_entry.get("skill_attempts"))
		successes = unit_entry.get("skill_success_counts", unit_entry.get("skill_successes"))
		if not isinstance(attempts, dict) or not isinstance(successes, dict):
			return False
	return True


def has_complete_faction_metrics(run: dict[str, Any]) -> bool:
	factions = get_run_metric_map(run, "factions")
	if not factions:
		return False
	for faction_entry in factions.values():
		if not isinstance(faction_entry, dict):
			return False
		if any(
			not isinstance(faction_entry.get(field), dict)
			for field in FACTION_COUNTER_FIELDS
		):
			return False
	return True


def build_flat_action_counts(runs: list[dict[str, Any]]) -> dict[str, int]:
	counter: Counter[str] = Counter()
	for run in runs:
		traces = run.get("ai_turn_traces", [])
		counter.update(build_run_action_counts(traces if isinstance(traces, list) else []))
	return dict(sorted(counter.items()))


def build_completed_only_summary(
	runs: list[dict[str, Any]],
	source_summary: dict[str, Any],
	*,
	action_counts_by_faction: bool = False,
) -> dict[str, Any]:
	completed_runs = [run for run in runs if is_completed_run(run)]
	completion_classification_complete = all(is_completion_classification_known(run) for run in runs)
	completion_unknown_run_count = sum(not is_completion_classification_known(run) for run in runs)
	reported_ended_count = as_int(source_summary.get("reported_ended_count", -1), -1)
	completed_run_count = (
		len(completed_runs)
		if completion_classification_complete or reported_ended_count < 0
		else min(max(reported_ended_count, 0), len(runs))
	)
	unfinished_run_count = len(runs) - completed_run_count
	aggregate_runs = completed_runs if completion_classification_complete else []
	result: dict[str, Any] = {
		"profile_id": str(source_summary.get("profile_id", "")),
		"display_name": str(source_summary.get("display_name", "")),
		"run_count": len(runs),
		"completed_run_count": completed_run_count,
		"reconstructable_completed_run_count": len(completed_runs),
		"unfinished_run_count": unfinished_run_count,
		"ended_count": completed_run_count,
		"completion_classification_complete": completion_classification_complete,
		"completion_unknown_run_count": completion_unknown_run_count,
		"normal_aggregate_scope": "completed_runs_only",
	}
	for field in ["description", "requested_run_count", "reported_ended_count", "timed_out", "elapsed_seconds"]:
		if field in source_summary:
			result[field] = source_summary[field]

	wins_by_faction: dict[str, int] = {}
	for run in aggregate_runs:
		winner_faction_id = str(run.get("winner_faction_id", "")) or "draw"
		wins_by_faction[winner_faction_id] = wins_by_faction.get(winner_faction_id, 0) + 1
	result["wins_by_faction"] = dict(sorted(wins_by_faction.items()))
	result["win_rate_by_faction"] = build_rate_dict(wins_by_faction, len(aggregate_runs))

	metric_sources: dict[str, str] = {
		"wins_by_faction": "per_run_completed" if completion_classification_complete else "unavailable",
		"win_rate_by_faction": "per_run_completed" if completion_classification_complete else "unavailable",
	}
	for run_field, summary_field in [
		("final_tu", "average_final_tu"),
		("iterations", "average_iterations"),
		("timeline_steps", "average_timeline_steps"),
	]:
		mean_value = completed_numeric_mean(aggregate_runs, run_field)
		result[summary_field] = mean_value
		metric_sources[summary_field] = "per_run_completed" if mean_value is not None else "unavailable"

	skill_metrics_available = bool(aggregate_runs) and all(has_complete_skill_metrics(run) for run in aggregate_runs)
	success_counter: Counter[str] = Counter()
	attempt_counter: Counter[str] = Counter()
	if skill_metrics_available:
		for run in aggregate_runs:
			metrics = run.get("metrics", {})
			success_counter.update(build_run_skill_counts(metrics))
			attempt_counter.update(build_run_skill_attempt_counts(metrics))
	result["skill_usage_totals"] = dict(sorted(success_counter.items())) if skill_metrics_available else {}
	result["skill_attempt_totals"] = dict(sorted(attempt_counter.items())) if skill_metrics_available else {}
	result["skill_failure_totals"] = (
		dict(sorted(build_failure_counter(attempt_counter, success_counter).items()))
		if skill_metrics_available
		else {}
	)
	for field in ["skill_usage_totals", "skill_attempt_totals", "skill_failure_totals"]:
		metric_sources[field] = "per_run_completed" if skill_metrics_available else "unavailable"

	traces_available = bool(aggregate_runs) and all(isinstance(run.get("ai_turn_traces"), list) for run in aggregate_runs)
	if traces_available:
		result["action_choice_counts"] = (
			build_trace_action_counts_by_faction(aggregate_runs)
			if action_counts_by_faction
			else build_flat_action_counts(aggregate_runs)
		)
		result["command_counts_by_faction"] = build_trace_command_counts_by_faction(aggregate_runs)
	else:
		result["action_choice_counts"] = {}
		result["command_counts_by_faction"] = {}
	metric_sources["action_choice_counts"] = "per_run_completed" if traces_available else "unavailable"
	metric_sources["command_counts_by_faction"] = "per_run_completed" if traces_available else "unavailable"

	faction_metrics_available = bool(aggregate_runs) and all(
		has_complete_faction_metrics(run) for run in aggregate_runs
	)
	faction_metric_totals = merge_faction_metric_totals(aggregate_runs) if faction_metrics_available else {}
	result["faction_metric_totals"] = faction_metric_totals
	metric_sources["faction_metric_totals"] = "per_run_completed" if faction_metrics_available else "unavailable"

	unit_metrics_available = bool(aggregate_runs) and all(
		get_run_metric_map(run, "units") is not None for run in aggregate_runs
	)
	result["unit_contribution_summary"] = (
		build_unit_contribution_summary({}, aggregate_runs, faction_metric_totals)
		if unit_metrics_available and faction_metrics_available
		else None
	)
	metric_sources["unit_contribution_summary"] = (
		"per_run_completed" if unit_metrics_available and faction_metrics_available else "unavailable"
	)
	result["completed_only_metric_sources"] = metric_sources
	return result


def build_profile_guardrails(
	entry: dict[str, Any],
	scenario: dict[str, Any],
	completed_only_summary: dict[str, Any],
) -> dict[str, Any]:
	runs = list(entry.get("runs", []))
	seeds = [int(run.get("seed", 0)) for run in runs]
	completed_run_count = as_int(completed_only_summary.get("completed_run_count", 0))
	unfinished_run_count = as_int(completed_only_summary.get("unfinished_run_count", 0))
	completion_unknown_run_count = as_int(completed_only_summary.get("completion_unknown_run_count", 0))
	completed_wins = completed_only_summary.get("wins_by_faction", {})
	if not isinstance(completed_wins, dict):
		completed_wins = {}
	metric_sources = completed_only_summary.get("completed_only_metric_sources", {})
	unavailable_metrics = sorted(
		str(field)
		for field, source in metric_sources.items()
		if str(source) == "unavailable"
	) if isinstance(metric_sources, dict) else []
	warnings: list[str] = []
	if str(scenario.get("manual_policy", "")) == "wait":
		warnings.append(
			"manual_policy=wait: manual-side units behave as stationary dummies, so this scenario is not suitable for validating AI against an intelligent player."
		)
	if completed_run_count < 20:
		warnings.append(
			"completed_sample_count_below_recommendation: use at least 20 independent completed runs per profile before treating small deltas as stable conclusions."
		)
	if unfinished_run_count > 0:
		warnings.append(
			"unfinished_runs_present: packet aggregates and comparisons exclude battle_ended=false runs; unfinished run details remain diagnostic only."
		)
	if completion_unknown_run_count > 0:
		warnings.append(
			"completion_classification_unknown: %d run(s) lack per-run battle_ended and cannot be assigned reliably from ended_count/winner alone; normal aggregates are unavailable."
			% completion_unknown_run_count
		)
	if unavailable_metrics:
		warnings.append(
			"completed_only_metrics_unavailable: %s could not be reconstructed reliably from completed per-run data."
			% ", ".join(unavailable_metrics)
		)
	return {
		"run_count": len(runs),
		"seed_count": len(set(seeds)),
		"seed_values": sorted(set(seeds)),
		"completed_run_count": completed_run_count,
		"unfinished_run_count": unfinished_run_count,
		"completion_unknown_run_count": completion_unknown_run_count,
		"completion_classification_complete": bool(completed_only_summary.get("completion_classification_complete", False)),
		"normal_aggregate_scope": "completed_runs_only",
		"unavailable_completed_only_metrics": unavailable_metrics,
		"completed_only_wins_by_faction": completed_wins,
		"completed_only_win_rate_by_faction": completed_only_summary.get("win_rate_by_faction", {}),
		"warnings": warnings,
	}


def build_profile_summaries(profile_entries: list[dict[str, Any]], scenario: dict[str, Any]) -> list[dict[str, Any]]:
	summaries: list[dict[str, Any]] = []
	for entry in profile_entries:
		runs = [run for run in entry.get("runs", []) if isinstance(run, dict)]
		raw_summary = entry.get("summary", {})
		source_summary = raw_summary if isinstance(raw_summary, dict) else {}
		profile = entry.get("profile", {})
		if isinstance(profile, dict):
			source_summary = dict(source_summary)
			source_summary.setdefault("profile_id", str(profile.get("profile_id", "")))
			source_summary.setdefault("display_name", str(profile.get("display_name", "")))
		summary = build_completed_only_summary(
			runs,
			source_summary,
			action_counts_by_faction=str(scenario.get("report_shape", "")) == "standalone_runs",
		)
		skill_counters = build_profile_skill_counters(
			runs,
			summary,
		)
		summaries.append(
			{
				"profile": entry.get("profile", {}),
				"summary": summary,
				"skill_counters": skill_counters,
				"run_digest": [build_run_digest(run) for run in runs],
				"guardrails": build_profile_guardrails(entry, scenario, summary),
			}
		)
	return summaries


def diff_numeric_dict(baseline: dict[str, Any], candidate: dict[str, Any]) -> dict[str, float]:
	keys = set(str(key) for key in baseline.keys()) | set(str(key) for key in candidate.keys())
	return {
		key: as_float(candidate.get(key, 0.0)) - as_float(baseline.get(key, 0.0))
		for key in sorted(keys)
	}


def summary_metric_available(summary: dict[str, Any], field: str) -> bool:
	sources = summary.get("completed_only_metric_sources", {})
	return isinstance(sources, dict) and str(sources.get(field, "unavailable")) != "unavailable"


def build_completed_only_comparisons(profile_summaries: list[dict[str, Any]]) -> list[dict[str, Any]]:
	if len(profile_summaries) < 2:
		return []
	baseline_entry = profile_summaries[0]
	baseline_profile = baseline_entry.get("profile", {})
	baseline_summary = baseline_entry.get("summary", {})
	if not isinstance(baseline_summary, dict):
		return []
	comparisons: list[dict[str, Any]] = []
	for candidate_entry in profile_summaries[1:]:
		candidate_profile = candidate_entry.get("profile", {})
		candidate_summary = candidate_entry.get("summary", {})
		if not isinstance(candidate_summary, dict):
			continue
		unavailable_metrics: list[str] = []

		def scalar_delta(field: str) -> float | None:
			if not summary_metric_available(baseline_summary, field) or not summary_metric_available(candidate_summary, field):
				unavailable_metrics.append(field)
				return None
			return as_float(candidate_summary.get(field, 0.0)) - as_float(baseline_summary.get(field, 0.0))

		def map_delta(field: str) -> dict[str, float]:
			if not summary_metric_available(baseline_summary, field) or not summary_metric_available(candidate_summary, field):
				unavailable_metrics.append(field)
				return {}
			baseline_values = baseline_summary.get(field, {})
			candidate_values = candidate_summary.get(field, {})
			if not isinstance(baseline_values, dict) or not isinstance(candidate_values, dict):
				unavailable_metrics.append(field)
				return {}
			return diff_numeric_dict(baseline_values, candidate_values)

		comparisons.append(
			{
				"baseline_profile_id": str(baseline_profile.get("profile_id", "")) if isinstance(baseline_profile, dict) else "",
				"candidate_profile_id": str(candidate_profile.get("profile_id", "")) if isinstance(candidate_profile, dict) else "",
				"comparison_method": "independent_completed_run_aggregates",
				"sample_scope": "completed_runs_only",
				"baseline_run_count": as_int(baseline_summary.get("run_count", 0)),
				"baseline_completed_run_count": as_int(baseline_summary.get("completed_run_count", 0)),
				"candidate_run_count": as_int(candidate_summary.get("run_count", 0)),
				"candidate_completed_run_count": as_int(candidate_summary.get("completed_run_count", 0)),
				"average_final_tu_delta": scalar_delta("average_final_tu"),
				"average_iterations_delta": scalar_delta("average_iterations"),
				"average_timeline_steps_delta": scalar_delta("average_timeline_steps"),
				"win_rate_delta": map_delta("win_rate_by_faction"),
				"skill_usage_delta": map_delta("skill_usage_totals"),
				"skill_attempt_delta": map_delta("skill_attempt_totals"),
				"skill_failure_delta": map_delta("skill_failure_totals"),
				"action_choice_delta": map_delta("action_choice_counts"),
				"unavailable_completed_only_metrics": sorted(set(unavailable_metrics)),
			}
		)
	return comparisons


def build_focus_hints(
	comparisons: list[dict[str, Any]],
	top_skills: int,
	top_actions: int,
) -> list[dict[str, Any]]:
	hints: list[dict[str, Any]] = []
	for comparison in comparisons:
		skill_usage_delta = comparison.get("skill_usage_delta", {})
		skill_failure_delta = comparison.get("skill_failure_delta", {})
		skill_attempt_delta = comparison.get("skill_attempt_delta", {})
		action_choice_delta = comparison.get("action_choice_delta", {})
		skill_deltas = sorted_delta_items(skill_usage_delta if isinstance(skill_usage_delta, dict) else {}, top_skills)
		skill_failure_deltas = sorted_delta_items(skill_failure_delta if isinstance(skill_failure_delta, dict) else {}, top_skills)
		skill_attempt_deltas = sorted_delta_items(skill_attempt_delta if isinstance(skill_attempt_delta, dict) else {}, top_skills)
		action_deltas = sorted_delta_items(action_choice_delta if isinstance(action_choice_delta, dict) else {}, top_actions)
		focus_skill_ids = {
			str(entry["id"]) for entry in skill_deltas + skill_failure_deltas + skill_attempt_deltas if entry.get("id", "")
		}
		hints.append(
			{
				"baseline_profile_id": str(comparison.get("baseline_profile_id", "")),
				"candidate_profile_id": str(comparison.get("candidate_profile_id", "")),
				"average_final_tu_delta": comparison.get("average_final_tu_delta"),
				"average_iterations_delta": comparison.get("average_iterations_delta"),
				"average_timeline_steps_delta": comparison.get("average_timeline_steps_delta"),
				"win_rate_delta": comparison.get("win_rate_delta", {}),
				"top_skill_deltas": skill_deltas,
				"top_skill_attempt_deltas": skill_attempt_deltas,
				"top_skill_failure_deltas": skill_failure_deltas,
				"top_action_deltas": action_deltas,
				"focus_skill_ids": sorted(focus_skill_ids),
				"focus_action_ids": [entry["id"] for entry in action_deltas],
				"comparison_method": str(comparison.get("comparison_method", "")),
				"unavailable_completed_only_metrics": comparison.get("unavailable_completed_only_metrics", []),
			}
		)
	return hints


def build_summary_packet(
	report_path: Path,
	trace_path: Path | None,
	report: dict[str, Any],
	scenario: dict[str, Any],
	profile_entries: list[dict[str, Any]],
	comparisons: list[dict[str, Any]],
	focus_hints: list[dict[str, Any]],
) -> dict[str, Any]:
	profile_summaries = build_profile_summaries(profile_entries, scenario)
	return {
		"source_files": {
			"report_json": str(report_path),
			"turn_trace_jsonl": str(trace_path) if trace_path is not None else "",
		},
		"analysis_guardrails": [
			"manual_policy=wait means manual-side units behave like dummies, so do not use this packet to claim AI performance against an intelligent player.",
			"Baseline comparisons always use profile_entries[0]. Prefer a profile_id prefix such as 00_baseline_* in scripted runs so ordering mistakes are obvious.",
			"Packet averages, wins, skill/action counts, faction metrics, unit contributions, comparisons, and focus traces are reconstructed from battle_ended=true runs only.",
			"Profile comparisons use independent completed-run aggregates, never seed pairing; combat outcomes are independently random even when terrain seeds match.",
			"If completed per-run data is insufficient, the affected metric is empty or null and listed in unavailable_completed_only_metrics instead of falling back to a possibly contaminated source aggregate.",
			"score_input.estimated_* fields are AI-side estimates, not actual realized combat output. Validate suspicious choices against faction_metric_totals and skill_success_counts.",
			"For small deltas, prefer at least 20 independent completed runs per profile before treating the difference as stable.",
			"top_candidates inside traces are truncated to the best 5 candidates per action, so dense target spaces may hide lower-ranked alternatives.",
		],
		"packet_notes": [
			"Read profile_summaries and comparisons first.",
			"Only read focus_traces.jsonl if the summary is not sufficient for diagnosis.",
			"Do not feed the original report.json and turn_traces.jsonl together unless full-fidelity review is required, because the report already contains embedded ai_turn_traces.",
		],
		"scenario": scenario,
		"report_shape": str(scenario.get("report_shape", infer_report_shape(report))),
		"generated_at_unix": int(report.get("generated_at_unix", 0)),
		"profile_count": len(profile_entries),
		"run_count": count_total_runs(profile_entries),
		"trace_count": count_total_traces(profile_entries),
		"completed_run_trace_count": count_completed_traces(profile_entries),
		"profile_summaries": profile_summaries,
		"comparisons": comparisons,
		"focus_hints": focus_hints,
	}


def build_trace_records(
	report: dict[str, Any],
	scenario: dict[str, Any],
	profile_entries: list[dict[str, Any]],
) -> dict[str, list[dict[str, Any]]]:
	scenario_id = str(scenario.get("scenario_id", ""))
	traces_by_profile: dict[str, list[dict[str, Any]]] = defaultdict(list)
	for entry in profile_entries:
		profile_id = normalize_profile_id(entry)
		runs = [run for run in entry.get("runs", []) if isinstance(run, dict)]
		if any(not is_completion_classification_known(run) for run in runs):
			continue
		for run in runs:
			if not isinstance(run, dict) or not is_completed_run(run):
				continue
			seed = int(run.get("seed", 0))
			for trace in run.get("ai_turn_traces", []):
				record = dict(trace)
				record["scenario_id"] = scenario_id
				record["profile_id"] = profile_id
				record["seed"] = seed
				traces_by_profile[profile_id].append(record)
	return traces_by_profile


def ordered_focus_profiles(
	focus_hints: list[dict[str, Any]],
	profile_entries: list[dict[str, Any]],
	include_baseline_traces: bool,
) -> list[str]:
	ordered: list[str] = []
	for hint in focus_hints:
		candidate_profile_id = str(hint.get("candidate_profile_id", ""))
		baseline_profile_id = str(hint.get("baseline_profile_id", ""))
		if candidate_profile_id and candidate_profile_id not in ordered:
			ordered.append(candidate_profile_id)
		if include_baseline_traces and baseline_profile_id and baseline_profile_id not in ordered:
			ordered.append(baseline_profile_id)
	if ordered:
		return ordered
	for entry in profile_entries:
		profile_id = normalize_profile_id(entry)
		if profile_id and profile_id not in ordered:
			ordered.append(profile_id)
	return ordered


def build_focus_lookup(
	focus_hints: list[dict[str, Any]],
	profile_entries: list[dict[str, Any]],
	include_baseline_traces: bool,
) -> dict[str, dict[str, set[str]]]:
	lookup: dict[str, dict[str, set[str]]] = {}
	for entry in profile_entries:
		profile_id = normalize_profile_id(entry)
		lookup[profile_id] = {"skills": set(), "actions": set()}
	for hint in focus_hints:
		target_profiles = [str(hint.get("candidate_profile_id", ""))]
		if include_baseline_traces:
			target_profiles.append(str(hint.get("baseline_profile_id", "")))
		for profile_id in target_profiles:
			if not profile_id:
				continue
			lookup.setdefault(profile_id, {"skills": set(), "actions": set()})
			lookup[profile_id]["skills"].update(str(value) for value in hint.get("focus_skill_ids", []))
			lookup[profile_id]["actions"].update(str(value) for value in hint.get("focus_action_ids", []))
	return lookup


def trace_sort_key(trace: dict[str, Any]) -> tuple[int, int, str, str]:
	return (
		int(trace.get("seed", 0)),
		int(trace.get("turn_started_tu", -1)),
		str(trace.get("unit_id", "")),
		str(trace.get("action_id", "")),
	)


def interleave_traces_by_seed(traces: list[dict[str, Any]]) -> list[dict[str, Any]]:
	buckets: dict[int, list[dict[str, Any]]] = defaultdict(list)
	for trace in sorted(traces, key=trace_sort_key):
		buckets[int(trace.get("seed", 0))].append(trace)
	ordered: list[dict[str, Any]] = []
	seed_values = sorted(buckets.keys())
	while True:
		added = False
		for seed in seed_values:
			if buckets[seed]:
				ordered.append(buckets[seed].pop(0))
				added = True
		if not added:
			break
	return ordered


def trace_identity(trace: dict[str, Any]) -> tuple[str, int, str, int, str]:
	return (
		str(trace.get("profile_id", "")),
		int(trace.get("seed", 0)),
		str(trace.get("unit_id", "")),
		int(trace.get("turn_started_tu", -1)),
		str(trace.get("action_id", "")),
	)


def copy_trace_with_reason(trace: dict[str, Any], reasons: list[str]) -> dict[str, Any]:
	record = dict(trace)
	record["packet_match_reasons"] = reasons
	return record


def select_focus_traces(
	profile_entries: list[dict[str, Any]],
	traces_by_profile: dict[str, list[dict[str, Any]]],
	focus_hints: list[dict[str, Any]],
	max_focus_traces: int,
	max_traces_per_profile: int,
	include_baseline_traces: bool,
) -> list[dict[str, Any]]:
	ordered_profiles = ordered_focus_profiles(focus_hints, profile_entries, include_baseline_traces)
	focus_lookup = build_focus_lookup(focus_hints, profile_entries, include_baseline_traces)
	selected: list[dict[str, Any]] = []
	seen: set[tuple[str, int, str, int, str]] = set()
	for profile_id in ordered_profiles:
		traces = sorted(traces_by_profile.get(profile_id, []), key=trace_sort_key)
		if not traces:
			continue
		action_ids = focus_lookup.get(profile_id, {}).get("actions", set())
		skill_ids = focus_lookup.get(profile_id, {}).get("skills", set())
		matching: list[dict[str, Any]] = []
		fallback: list[dict[str, Any]] = []
		for trace in traces:
			match_reasons: list[str] = []
			action_id = str(trace.get("action_id", ""))
			skill_id = str(trace.get("score_input", {}).get("skill_id", ""))
			if action_id and action_id in action_ids:
				match_reasons.append("action_delta")
			if skill_id and skill_id in skill_ids:
				match_reasons.append("skill_delta")
			if match_reasons:
				matching.append(copy_trace_with_reason(trace, match_reasons))
			else:
				fallback.append(copy_trace_with_reason(trace, ["profile_fill"]))
		profile_selected: list[dict[str, Any]] = []
		ordered_fallback = interleave_traces_by_seed(fallback) if not matching else fallback
		for trace in matching + ordered_fallback:
			identity = trace_identity(trace)
			if identity in seen:
				continue
			profile_selected.append(trace)
			seen.add(identity)
			if len(profile_selected) >= max_traces_per_profile:
				break
		selected.extend(profile_selected)
		if len(selected) >= max_focus_traces:
			break
	return selected[:max_focus_traces]


def build_focus_trace_stats(traces: list[dict[str, Any]]) -> list[dict[str, Any]]:
	per_profile: dict[str, Counter[str]] = defaultdict(Counter)
	seed_sets: dict[str, set[int]] = defaultdict(set)
	for trace in traces:
		profile_id = str(trace.get("profile_id", ""))
		if not profile_id:
			continue
		per_profile[profile_id][str(trace.get("action_id", ""))] += 1
		seed_sets[profile_id].add(int(trace.get("seed", 0)))
	stats: list[dict[str, Any]] = []
	for profile_id in sorted(per_profile.keys()):
		stats.append(
			{
				"profile_id": profile_id,
				"trace_count": sum(per_profile[profile_id].values()),
				"seed_count": len(seed_sets[profile_id]),
				"seeds": sorted(seed_sets[profile_id]),
				"top_action_choices": sorted_counter_items(per_profile[profile_id], limit=5),
			}
		)
	return stats


def format_delta_entries(entries: list[dict[str, Any]], value_key: str) -> list[str]:
	if not entries:
		return ["none"]
	lines: list[str] = []
	for entry in entries:
		value = entry.get(value_key, 0)
		lines.append(f"- {entry.get('id', '')}: {value}")
	return lines


def format_role_entries(entries: list[dict[str, Any]]) -> list[str]:
	if not entries:
		return ["none"]
	lines: list[str] = []
	for entry in entries[:8]:
		damage_share = float(entry.get("damage_share", 0.0)) * 100.0
		lines.append(
			"- %s/%s: damage=%s share=%.1f%% kills=%s deaths=%s taken=%s"
			% (
				entry.get("faction_id", ""),
				entry.get("role", ""),
				entry.get("damage_done", 0),
				damage_share,
				entry.get("kills", 0),
				entry.get("deaths", 0),
				entry.get("damage_taken", 0),
			)
		)
	return lines


def format_unit_entries(entries: list[dict[str, Any]]) -> list[str]:
	if not entries:
		return ["none"]
	lines: list[str] = []
	for entry in entries[:8]:
		lines.append(
			"- %s/%s: damage=%s kills=%s deaths=%s taken=%s"
			% (
				entry.get("faction_id", ""),
				entry.get("unit_id", ""),
				entry.get("damage_done", 0),
				entry.get("kills", 0),
				entry.get("deaths", 0),
				entry.get("damage_taken", 0),
			)
		)
	return lines


def build_analysis_brief(
	report_path: Path,
	trace_path: Path | None,
	summary_path: Path,
	focus_traces_path: Path,
	report: dict[str, Any],
	scenario: dict[str, Any],
	profile_entries: list[dict[str, Any]],
	focus_hints: list[dict[str, Any]],
	selected_traces: list[dict[str, Any]],
) -> str:
	profile_count = len(profile_entries)
	run_count = count_total_runs(profile_entries)
	trace_count = count_total_traces(profile_entries)
	focus_trace_stats = build_focus_trace_stats(selected_traces)
	profile_summaries = build_profile_summaries(profile_entries, scenario)
	lines: list[str] = [
		"# Battle Sim Analysis Packet",
		"",
		"## Source Files",
		f"- report_json: `{report_path}`",
		f"- original_turn_traces_jsonl: `{trace_path}`" if trace_path is not None else "- original_turn_traces_jsonl: not resolved from report",
		f"- summary_for_llm: `{summary_path}`",
		f"- focus_traces_jsonl: `{focus_traces_path}`",
		"",
		"## Scenario",
		f"- scenario_id: `{scenario.get('scenario_id', '')}`",
		f"- manual_policy: `{scenario.get('manual_policy', '')}`",
		f"- report_shape: `{scenario.get('report_shape', infer_report_shape(report))}`",
		f"- profile_count: `{profile_count}`",
		f"- run_count: `{run_count}`",
		f"- embedded_trace_count: `{trace_count}`",
		f"- completed_run_embedded_trace_count: `{count_completed_traces(profile_entries)}`",
		f"- exported_focus_trace_count: `{len(selected_traces)}`",
		"",
		"## Guardrails",
		"- `manual_policy=wait` means manual-side units act as dummies, not intelligent players.",
		"- Baseline comparisons always use `profile_entries[0]`. Prefer a `00_baseline_*` profile_id prefix in scripted runs.",
		"- Packet normal aggregates, comparisons, and focus traces already exclude `battle_ended=false` runs.",
		"- Profile comparisons use independent completed-run aggregates, never seed pairing.",
		"- Metrics that cannot be reconstructed from completed per-run data are marked unavailable instead of using source aggregates.",
		"- `score_input.estimated_*` fields are AI-side estimates, not realized combat output.",
		"- Treat small deltas cautiously when a profile has fewer than 20 completed runs.",
		"- `top_candidates` in traces are truncated to 5 entries per action.",
		"",
		"## Use Order",
		"1. Read `summary_for_llm.json` first.",
		"2. Read `analysis_brief.md` to decide what changed and which profile pair to inspect.",
		"3. Read `focus_traces.jsonl` only after the summary indicates a concrete anomaly worth tracing.",
		"4. Open the original `report.json` or full `turn_traces.jsonl` only if the focus packet is insufficient.",
		"",
		"## Comparison Highlights",
	]
	if not focus_hints:
		lines.extend(
			[
				"- No profile comparisons were present. Use `summary_for_llm.json` and then inspect the earliest focus traces.",
				"",
			]
		)
	else:
		for hint in focus_hints:
			lines.extend(
				[
					f"### `{hint.get('baseline_profile_id', '')}` -> `{hint.get('candidate_profile_id', '')}`",
					f"- comparison_method: `{hint.get('comparison_method', '')}`",
					f"- average_final_tu_delta: `{hint.get('average_final_tu_delta', 0.0)}`",
					f"- average_iterations_delta: `{hint.get('average_iterations_delta', 0.0)}`",
					f"- average_timeline_steps_delta: `{hint.get('average_timeline_steps_delta', 0.0)}`",
					f"- unavailable_completed_only_metrics: `{hint.get('unavailable_completed_only_metrics', [])}`",
					"- top_skill_deltas:",
					*format_delta_entries(hint.get("top_skill_deltas", []), "delta"),
					"- top_skill_attempt_deltas:",
					*format_delta_entries(hint.get("top_skill_attempt_deltas", []), "delta"),
					"- top_skill_failure_deltas:",
					*format_delta_entries(hint.get("top_skill_failure_deltas", []), "delta"),
					"- top_action_deltas:",
					*format_delta_entries(hint.get("top_action_deltas", []), "delta"),
					"",
				]
			)
	lines.append("## Profile Diagnostics")
	for summary_entry in profile_summaries:
		profile = summary_entry.get("profile", {})
		summary_data = summary_entry.get("summary", {})
		guardrails = summary_entry.get("guardrails", {})
		skill_counters = summary_entry.get("skill_counters", {})
		unit_contribution = summary_data.get("unit_contribution_summary", {}) if isinstance(summary_data, dict) else {}
		lines.extend(
			[
				f"### `{profile.get('profile_id', '')}`",
				f"- seed_count: `{guardrails.get('seed_count', 0)}`",
				f"- completed_run_count: `{guardrails.get('completed_run_count', 0)}`",
				f"- unfinished_run_count: `{guardrails.get('unfinished_run_count', 0)}`",
				f"- normal_aggregate_scope: `{guardrails.get('normal_aggregate_scope', '')}`",
				f"- unavailable_completed_only_metrics: `{guardrails.get('unavailable_completed_only_metrics', [])}`",
				f"- completed_only_win_rate_by_faction: `{guardrails.get('completed_only_win_rate_by_faction', {})}`",
				f"- faction_metric_totals: `{summary_data.get('faction_metric_totals', {}) if isinstance(summary_data, dict) else {}}`",
				"- role_damage_share:",
				*format_role_entries(unit_contribution.get("role_totals", []) if isinstance(unit_contribution, dict) else []),
				"- top_damage_units:",
				*format_unit_entries(unit_contribution.get("top_damage_units", []) if isinstance(unit_contribution, dict) else []),
				"- top_skill_successes:",
				*format_delta_entries(skill_counters.get("top_skill_successes", []), "count"),
				"- top_skill_attempts:",
				*format_delta_entries(skill_counters.get("top_skill_attempts", []), "count"),
				"- top_skill_failures:",
				*format_delta_entries(skill_counters.get("top_skill_failures", []), "count"),
			]
		)
		for warning in guardrails.get("warnings", []):
			lines.append(f"- warning: {warning}")
		lines.append("")
	lines.append("## Focus Trace Coverage")
	if not focus_trace_stats:
		lines.append("- No focus traces were exported.")
	else:
		for stat in focus_trace_stats:
			lines.extend(
				[
					f"### `{stat.get('profile_id', '')}`",
					f"- trace_count: `{stat.get('trace_count', 0)}`",
					f"- seeds: `{', '.join(str(seed) for seed in stat.get('seeds', []))}`",
					"- top_action_choices:",
					*format_delta_entries(stat.get("top_action_choices", []), "count"),
					"",
				]
			)
	lines.extend(
		[
			"## Recommended External-Model Prompt",
			"```text",
			"You are analyzing a compact battle simulation packet.",
			"",
			"Read summary_for_llm.json first.",
			"Use analysis_brief.md to identify the comparison pair and the likely failure axis.",
			"Open focus_traces.jsonl only if you need causal evidence for why the AI chose a certain action.",
			"",
			"Then answer:",
			"1. What are the primary behavioral changes between baseline and candidate profiles?",
			"2. Does the observed change look more like a skill-numbers issue, AI action-parameter issue, or AI scoring issue?",
			"3. Which 1-3 fields should be changed next, and why?",
			"4. Which output fields should be checked after the next simulation run to confirm the hypothesis?",
			"```",
			"",
		]
	)
	return "\n".join(lines)


def default_output_dir(report_path: Path) -> Path:
	return report_path.parent / f"{report_path.stem}_llm_packet"


def main() -> int:
	args = parse_args()
	report_path = Path(args.report).expanduser().resolve()
	report = load_json(report_path)
	scenario = build_effective_scenario(report, report_path)
	profile_entries = build_effective_profile_entries(report)
	trace_path = resolve_user_path(str(report.get("output_files", {}).get("turn_trace_jsonl", "")), report_path)
	output_dir = Path(args.output_dir).expanduser().resolve() if args.output_dir else default_output_dir(report_path)
	output_dir.mkdir(parents=True, exist_ok=True)

	completed_only_comparisons = build_completed_only_comparisons(
		build_profile_summaries(profile_entries, scenario)
	)
	focus_hints = build_focus_hints(
		completed_only_comparisons,
		top_skills=max(args.top_skills, 0),
		top_actions=max(args.top_actions, 0),
	)
	summary_packet = build_summary_packet(
		report_path,
		trace_path,
		report,
		scenario,
		profile_entries,
		completed_only_comparisons,
		focus_hints,
	)
	selected_traces = select_focus_traces(
		profile_entries,
		build_trace_records(report, scenario, profile_entries),
		focus_hints,
		max_focus_traces=max(args.max_focus_traces, 1),
		max_traces_per_profile=max(args.max_traces_per_profile, 1),
		include_baseline_traces=bool(args.include_baseline_traces),
	)

	summary_path = output_dir / "summary_for_llm.json"
	focus_traces_path = output_dir / "focus_traces.jsonl"
	analysis_brief_path = output_dir / "analysis_brief.md"

	write_json(summary_path, summary_packet)
	focus_traces_path.write_text(
		"\n".join(json.dumps(trace, ensure_ascii=False) for trace in selected_traces) + ("\n" if selected_traces else ""),
		encoding="utf-8",
	)
	analysis_brief_path.write_text(
		build_analysis_brief(
			report_path,
			trace_path,
			summary_path,
			focus_traces_path,
			report,
			scenario,
			profile_entries,
			focus_hints,
			selected_traces,
		),
		encoding="utf-8",
	)

	print(
		"[BattleSimPacket] summary_for_llm=%s focus_traces=%s analysis_brief=%s traces=%d"
		% (summary_path, focus_traces_path, analysis_brief_path, len(selected_traces))
	)
	return 0


if __name__ == "__main__":
	raise SystemExit(main())

#!/usr/bin/env python3
"""Build a low-token analysis packet from a full battle simulation report."""

from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


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
		all_runs_completed = (
			isinstance(runs, list)
			and len(runs) > 0
			and as_int(report.get("ended_count", -1), -1) == len(runs)
		)
		normalized["battle_ended"] = bool(str(normalized.get("winner_faction_id", ""))) or all_runs_completed
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
	totals: dict[str, dict[str, Any]] = defaultdict(lambda: defaultdict(float))
	for run in runs:
		metrics = run.get("metrics", {})
		factions = metrics.get("factions", {}) if isinstance(metrics, dict) else {}
		if not isinstance(factions, dict):
			continue
		for faction_id, faction_entry in factions.items():
			if not isinstance(faction_entry, dict):
				continue
			for key, value in faction_entry.items():
				if isinstance(value, bool):
					continue
				if isinstance(value, (int, float)):
					totals[str(faction_id)][str(key)] += value
	result: dict[str, dict[str, Any]] = {}
	for faction_id, faction_totals in sorted(totals.items()):
		result[faction_id] = {
			key: int(value) if float(value).is_integer() else value
			for key, value in sorted(faction_totals.items())
		}
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
	success_counter: Counter[str] = Counter()
	attempt_counter: Counter[str] = Counter()
	for run in runs:
		metrics = run.get("metrics", {})
		if not isinstance(metrics, dict):
			continue
		success_counter.update(build_run_skill_counts(metrics))
		attempt_counter.update(build_run_skill_attempt_counts(metrics))
	global_skill_report = report.get("global", {})
	if isinstance(global_skill_report, dict) and not success_counter:
		update_counter_from_skill_report(success_counter, global_skill_report, "successes")
	if isinstance(global_skill_report, dict) and not attempt_counter:
		update_counter_from_skill_report(attempt_counter, global_skill_report, "attempts")
	completed_runs = [run for run in runs if bool(run.get("battle_ended", False))]
	wins_by_faction: dict[str, int] = {}
	for run in completed_runs:
		winner_faction_id = str(run.get("winner_faction_id", ""))
		if winner_faction_id:
			wins_by_faction[winner_faction_id] = wins_by_faction.get(winner_faction_id, 0) + 1
	top_level_wins = report.get("win_rate", {})
	if isinstance(top_level_wins, dict) and top_level_wins:
		wins_by_faction = {str(key): as_int(value) for key, value in top_level_wins.items()}
	faction_metric_totals = merge_faction_metric_totals(runs)
	return {
		"profile_id": "standalone",
		"display_name": "Standalone report",
		"run_count": len(runs),
		"requested_run_count": as_int(report.get("requested_run_count", len(runs)), len(runs)),
		"completed_run_count": len(completed_runs),
		"ended_count": as_int(report.get("ended_count", len(completed_runs)), len(completed_runs)),
		"timed_out": bool(report.get("timed_out", False)),
		"elapsed_seconds": as_float(report.get("elapsed_seconds", 0.0)),
		"wins_by_faction": wins_by_faction,
		"win_rate_by_faction": build_rate_dict(wins_by_faction, max(len(completed_runs), 1)),
		"average_iterations": as_float(report.get("avg_iterations", 0.0)),
		"average_timeline_steps": as_float(report.get("avg_timeline_steps", 0.0)),
		"skill_usage_totals": dict(sorted(success_counter.items())),
		"skill_attempt_totals": dict(sorted(attempt_counter.items())),
		"action_choice_counts": build_trace_action_counts_by_faction(runs),
		"command_counts_by_faction": build_trace_command_counts_by_faction(runs),
		"faction_metric_totals": faction_metric_totals,
		"unit_contribution_summary": build_unit_contribution_summary(report, runs, faction_metric_totals),
	}


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
	success_counter: Counter[str] = Counter()
	attempt_counter: Counter[str] = Counter()
	for run in runs:
		metrics = run.get("metrics", {})
		if not isinstance(metrics, dict):
			continue
		success_counter.update(build_run_skill_counts(metrics))
		attempt_counter.update(build_run_skill_attempt_counts(metrics))
	if not success_counter and isinstance(summary.get("skill_usage_totals", {}), dict):
		success_counter.update({str(key): int(value) for key, value in summary.get("skill_usage_totals", {}).items()})
	if not attempt_counter and isinstance(summary.get("skill_attempt_totals", {}), dict):
		attempt_counter.update({str(key): int(value) for key, value in summary.get("skill_attempt_totals", {}).items()})
	return build_skill_counter_snapshot(dict(success_counter), dict(attempt_counter), limit=limit)


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


def build_profile_guardrails(entry: dict[str, Any], scenario: dict[str, Any]) -> dict[str, Any]:
	runs = list(entry.get("runs", []))
	seeds = [int(run.get("seed", 0)) for run in runs]
	completed_runs = [run for run in runs if bool(run.get("battle_ended", False))]
	unfinished_runs = [run for run in runs if not bool(run.get("battle_ended", False))]
	completed_wins: dict[str, int] = {}
	for run in completed_runs:
		winner_faction_id = str(run.get("winner_faction_id", ""))
		if not winner_faction_id:
			continue
		completed_wins[winner_faction_id] = int(completed_wins.get(winner_faction_id, 0)) + 1
	warnings: list[str] = []
	if str(scenario.get("manual_policy", "")) == "wait":
		warnings.append(
			"manual_policy=wait: manual-side units behave as stationary dummies, so this scenario is not suitable for validating AI against an intelligent player."
		)
	if len(runs) < 20:
		warnings.append(
			"seed_count_below_recommendation: use at least 20 seeds per profile before treating small deltas as stable conclusions."
		)
	if unfinished_runs:
		warnings.append(
			"unfinished_runs_present: exclude battle_ended=false runs from win-rate conclusions unless you are explicitly diagnosing stall behavior."
		)
	return {
		"run_count": len(runs),
		"seed_count": len(set(seeds)),
		"seed_values": sorted(set(seeds)),
		"completed_run_count": len(completed_runs),
		"unfinished_run_count": len(unfinished_runs),
		"completed_only_wins_by_faction": completed_wins,
		"completed_only_win_rate_by_faction": build_rate_dict(completed_wins, len(completed_runs)),
		"warnings": warnings,
	}


def build_profile_summaries(profile_entries: list[dict[str, Any]], scenario: dict[str, Any]) -> list[dict[str, Any]]:
	summaries: list[dict[str, Any]] = []
	for entry in profile_entries:
		runs = [run for run in entry.get("runs", []) if isinstance(run, dict)]
		summary = entry.get("summary", {})
		skill_counters = build_profile_skill_counters(
			runs,
			summary if isinstance(summary, dict) else {},
		)
		summaries.append(
			{
				"profile": entry.get("profile", {}),
				"summary": summary,
				"skill_counters": skill_counters,
				"run_digest": [build_run_digest(run) for run in runs],
				"guardrails": build_profile_guardrails(entry, scenario),
			}
		)
	return summaries


def build_focus_hints(
	comparisons: list[dict[str, Any]],
	top_skills: int,
	top_actions: int,
) -> list[dict[str, Any]]:
	hints: list[dict[str, Any]] = []
	for comparison in comparisons:
		skill_deltas = sorted_delta_items(comparison.get("skill_usage_delta", {}), top_skills)
		skill_failure_deltas = sorted_delta_items(comparison.get("skill_failure_delta", {}), top_skills)
		skill_attempt_deltas = sorted_delta_items(comparison.get("skill_attempt_delta", {}), top_skills)
		action_deltas = sorted_delta_items(comparison.get("action_choice_delta", {}), top_actions)
		focus_skill_ids = {
			str(entry["id"]) for entry in skill_deltas + skill_failure_deltas + skill_attempt_deltas if entry.get("id", "")
		}
		hints.append(
			{
				"baseline_profile_id": str(comparison.get("baseline_profile_id", "")),
				"candidate_profile_id": str(comparison.get("candidate_profile_id", "")),
				"average_final_tu_delta": float(comparison.get("average_final_tu_delta", 0.0)),
				"average_iterations_delta": float(comparison.get("average_iterations_delta", 0.0)),
				"average_timeline_steps_delta": float(comparison.get("average_timeline_steps_delta", 0.0)),
				"win_rate_delta": comparison.get("win_rate_delta", {}),
				"top_skill_deltas": skill_deltas,
				"top_skill_attempt_deltas": skill_attempt_deltas,
				"top_skill_failure_deltas": skill_failure_deltas,
				"top_action_deltas": action_deltas,
				"focus_skill_ids": sorted(focus_skill_ids),
				"focus_action_ids": [entry["id"] for entry in action_deltas],
			}
		)
	return hints


def build_summary_packet(
	report_path: Path,
	trace_path: Path | None,
	report: dict[str, Any],
	scenario: dict[str, Any],
	profile_entries: list[dict[str, Any]],
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
			"battle_ended=false runs should be filtered out before drawing win-rate conclusions; use completed_only_win_rate_by_faction when available.",
			"score_input.estimated_* fields are AI-side estimates, not actual realized combat output. Validate suspicious choices against faction_metric_totals and skill_success_counts.",
			"For small deltas, prefer at least 20 seeds per profile before treating the difference as stable.",
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
		"profile_summaries": profile_summaries,
		"comparisons": report.get("comparisons", []),
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
		for run in entry.get("runs", []):
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
		f"- exported_focus_trace_count: `{len(selected_traces)}`",
		"",
		"## Guardrails",
		"- `manual_policy=wait` means manual-side units act as dummies, not intelligent players.",
		"- Baseline comparisons always use `profile_entries[0]`. Prefer a `00_baseline_*` profile_id prefix in scripted runs.",
		"- Filter `battle_ended=false` runs before drawing win-rate conclusions.",
		"- `score_input.estimated_*` fields are AI-side estimates, not realized combat output.",
		"- Treat small deltas cautiously when a profile has fewer than 20 seeds.",
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
					f"- average_final_tu_delta: `{hint.get('average_final_tu_delta', 0.0)}`",
					f"- average_iterations_delta: `{hint.get('average_iterations_delta', 0.0)}`",
					f"- average_timeline_steps_delta: `{hint.get('average_timeline_steps_delta', 0.0)}`",
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

	focus_hints = build_focus_hints(
		list(report.get("comparisons", [])),
		top_skills=max(args.top_skills, 0),
		top_actions=max(args.top_actions, 0),
	)
	summary_packet = build_summary_packet(report_path, trace_path, report, scenario, profile_entries, focus_hints)
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

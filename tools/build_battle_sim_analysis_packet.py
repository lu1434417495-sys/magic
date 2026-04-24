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


def count_total_runs(profile_entries: list[dict[str, Any]]) -> int:
	return sum(len(entry.get("runs", [])) for entry in profile_entries)


def count_total_traces(profile_entries: list[dict[str, Any]]) -> int:
	return sum(
		len(run.get("ai_turn_traces", []))
		for entry in profile_entries
		for run in entry.get("runs", [])
	)


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
	profile_entries: list[dict[str, Any]],
	focus_hints: list[dict[str, Any]],
) -> dict[str, Any]:
	scenario = report.get("scenario", {})
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
	profile_entries: list[dict[str, Any]],
) -> dict[str, list[dict[str, Any]]]:
	scenario_id = str(report.get("scenario", {}).get("scenario_id", ""))
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
		for trace in matching + fallback:
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


def build_analysis_brief(
	report_path: Path,
	trace_path: Path | None,
	summary_path: Path,
	focus_traces_path: Path,
	report: dict[str, Any],
	profile_entries: list[dict[str, Any]],
	focus_hints: list[dict[str, Any]],
	selected_traces: list[dict[str, Any]],
) -> str:
	profile_count = len(profile_entries)
	run_count = count_total_runs(profile_entries)
	trace_count = count_total_traces(profile_entries)
	focus_trace_stats = build_focus_trace_stats(selected_traces)
	scenario = report.get("scenario", {})
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
		f"- scenario_id: `{report.get('scenario', {}).get('scenario_id', '')}`",
		f"- manual_policy: `{scenario.get('manual_policy', '')}`",
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
		guardrails = summary_entry.get("guardrails", {})
		skill_counters = summary_entry.get("skill_counters", {})
		lines.extend(
			[
				f"### `{profile.get('profile_id', '')}`",
				f"- seed_count: `{guardrails.get('seed_count', 0)}`",
				f"- completed_run_count: `{guardrails.get('completed_run_count', 0)}`",
				f"- unfinished_run_count: `{guardrails.get('unfinished_run_count', 0)}`",
				f"- completed_only_win_rate_by_faction: `{guardrails.get('completed_only_win_rate_by_faction', {})}`",
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
	profile_entries = list(report.get("profile_entries", []))
	trace_path = resolve_user_path(str(report.get("output_files", {}).get("turn_trace_jsonl", "")), report_path)
	output_dir = Path(args.output_dir).expanduser().resolve() if args.output_dir else default_output_dir(report_path)
	output_dir.mkdir(parents=True, exist_ok=True)

	focus_hints = build_focus_hints(
		list(report.get("comparisons", [])),
		top_skills=max(args.top_skills, 0),
		top_actions=max(args.top_actions, 0),
	)
	summary_packet = build_summary_packet(report_path, trace_path, report, profile_entries, focus_hints)
	selected_traces = select_focus_traces(
		profile_entries,
		build_trace_records(report, profile_entries),
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

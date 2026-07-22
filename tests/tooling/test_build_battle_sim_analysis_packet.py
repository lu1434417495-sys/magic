from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT_PATH = Path(__file__).resolve().parents[2] / "tools" / "build_battle_sim_analysis_packet.py"
SPEC = importlib.util.spec_from_file_location("build_battle_sim_analysis_packet_under_test", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(f"Unable to load battle sim packet builder from {SCRIPT_PATH}")
packet_builder = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = packet_builder
SPEC.loader.exec_module(packet_builder)


def build_run(
	*,
	seed: int,
	ended: bool,
	winner: str,
	iterations: int,
	timeline_steps: int,
	damage: int,
	skill_id: str,
	attempts: int,
	successes: int,
	action_id: str,
) -> dict[str, object]:
	return {
		"seed": seed,
		"battle_ended": ended,
		"winner_faction_id": winner,
		"final_tu": iterations * 2,
		"iterations": iterations,
		"timeline_steps": timeline_steps,
		"metrics": {
			"units": {
				"player_mage": {
					"display_name": "Player Mage",
					"faction_id": "player",
					"turn_count": 3,
					"total_damage_done": damage,
					"total_damage_taken": 1,
					"kill_count": 1,
					"death_count": 0,
					"skill_attempts": {skill_id: attempts},
					"skill_successes": {skill_id: successes},
				}
			},
			"factions": {
				"player": {
					"action_counts": {"skill": 1},
					"skill_attempt_counts": {skill_id: attempts},
					"skill_success_counts": {skill_id: successes},
					"total_damage_done": damage,
					"total_damage_taken": 1,
					"kill_count": 1,
					"death_count": 0,
				}
			},
		},
		"ai_turn_traces": [
			{
				"faction_id": "player",
				"action_id": action_id,
				"command": {"command_type": "skill"},
			}
		],
	}


def build_profile_entry(profile_id: str, completed: dict[str, object], unfinished: dict[str, object]) -> dict[str, object]:
	return {
		"profile": {"profile_id": profile_id, "display_name": profile_id},
		"summary": {
			"profile_id": profile_id,
			"display_name": profile_id,
			"run_count": 2,
			"average_iterations": 9999.0,
			"skill_usage_totals": {"polluted_skill": 9999},
			"action_choice_counts": {"polluted_action": 9999},
			"faction_metric_totals": {"player": {"total_damage_done": 9999}},
		},
		"runs": [completed, unfinished],
	}


class BattleSimAnalysisPacketCompletedRunTests(unittest.TestCase):
	def test_profile_summary_excludes_unfinished_run_from_every_normal_metric(self) -> None:
		completed = build_run(
			seed=101,
			ended=True,
			winner="player",
			iterations=10,
			timeline_steps=4,
			damage=50,
			skill_id="fireball",
			attempts=3,
			successes=2,
			action_id="cast_fireball",
		)
		unfinished = build_run(
			seed=102,
			ended=False,
			winner="",
			iterations=1000,
			timeline_steps=800,
			damage=9000,
			skill_id="unfinished_only_skill",
			attempts=900,
			successes=800,
			action_id="unfinished_only_action",
		)
		entry = build_profile_entry("baseline", completed, unfinished)

		profile_packet = packet_builder.build_profile_summaries(
			[entry],
			{"report_shape": "profile_entries", "manual_policy": ""},
		)[0]
		summary = profile_packet["summary"]

		self.assertEqual(2, summary["run_count"])
		self.assertEqual(1, summary["completed_run_count"])
		self.assertEqual(1, summary["unfinished_run_count"])
		self.assertEqual(10.0, summary["average_iterations"])
		self.assertEqual(4.0, summary["average_timeline_steps"])
		self.assertEqual({"fireball": 2}, summary["skill_usage_totals"])
		self.assertEqual({"fireball": 3}, summary["skill_attempt_totals"])
		self.assertEqual({"cast_fireball": 1}, summary["action_choice_counts"])
		self.assertEqual(50, summary["faction_metric_totals"]["player"]["total_damage_done"])
		self.assertEqual(
			{"skill": 1},
			summary["faction_metric_totals"]["player"]["action_counts"],
		)
		self.assertEqual(
			{"fireball": 3},
			summary["faction_metric_totals"]["player"]["skill_attempt_counts"],
		)
		self.assertEqual(
			{"fireball": 2},
			summary["faction_metric_totals"]["player"]["skill_success_counts"],
		)
		self.assertEqual(50, summary["unit_contribution_summary"]["top_damage_units"][0]["damage_done"])
		self.assertNotIn("unfinished_only_skill", summary["skill_usage_totals"])
		self.assertNotIn(
			"unfinished_only_skill",
			summary["faction_metric_totals"]["player"]["skill_attempt_counts"],
		)
		self.assertNotIn("unfinished_only_action", summary["action_choice_counts"])
		self.assertIn("unfinished_runs_present", " ".join(profile_packet["guardrails"]["warnings"]))

	def test_faction_counter_maps_sum_across_runs(self) -> None:
		first = build_run(
			seed=111,
			ended=True,
			winner="player",
			iterations=10,
			timeline_steps=4,
			damage=50,
			skill_id="fireball",
			attempts=2,
			successes=1,
			action_id="cast_fireball",
		)
		second = build_run(
			seed=112,
			ended=True,
			winner="player",
			iterations=12,
			timeline_steps=5,
			damage=60,
			skill_id="fireball",
			attempts=1,
			successes=1,
			action_id="cast_fireball",
		)
		first["metrics"]["factions"]["player"]["action_counts"] = {"skill": 2, "move": 1}
		second["metrics"]["factions"]["player"]["action_counts"] = {"skill": 3, "wait": 1}

		totals = packet_builder.merge_faction_metric_totals([first, second])

		self.assertEqual({"move": 1, "skill": 5, "wait": 1}, totals["player"]["action_counts"])
		self.assertEqual({"fireball": 3}, totals["player"]["skill_attempt_counts"])
		self.assertEqual({"fireball": 2}, totals["player"]["skill_success_counts"])

	def test_missing_faction_counter_map_is_unavailable_instead_of_zero(self) -> None:
		completed = build_run(
			seed=121,
			ended=True,
			winner="player",
			iterations=10,
			timeline_steps=4,
			damage=50,
			skill_id="fireball",
			attempts=2,
			successes=1,
			action_id="cast_fireball",
		)
		del completed["metrics"]["factions"]["player"]["skill_success_counts"]
		unfinished = build_run(
			seed=122,
			ended=False,
			winner="",
			iterations=1000,
			timeline_steps=900,
			damage=9000,
			skill_id="unfinished_only_skill",
			attempts=900,
			successes=900,
			action_id="unfinished_only_action",
		)

		profile_packet = packet_builder.build_profile_summaries(
			[build_profile_entry("baseline", completed, unfinished)],
			{"report_shape": "profile_entries", "manual_policy": ""},
		)[0]
		summary = profile_packet["summary"]

		self.assertEqual({}, summary["faction_metric_totals"])
		self.assertEqual(
			"unavailable",
			summary["completed_only_metric_sources"]["faction_metric_totals"],
		)
		self.assertIn(
			"faction_metric_totals",
			profile_packet["guardrails"]["unavailable_completed_only_metrics"],
		)

	def test_comparison_uses_independent_completed_aggregates_without_seed_matching(self) -> None:
		baseline = build_profile_entry(
			"00_baseline",
			build_run(
				seed=1,
				ended=True,
				winner="player",
				iterations=10,
				timeline_steps=4,
				damage=50,
				skill_id="fireball",
				attempts=3,
				successes=2,
				action_id="wait",
			),
			build_run(
				seed=2,
				ended=False,
				winner="",
				iterations=1000,
				timeline_steps=900,
				damage=9000,
				skill_id="corrupt",
				attempts=900,
				successes=900,
				action_id="corrupt",
			),
		)
		candidate = build_profile_entry(
			"candidate",
			build_run(
				seed=9,
				ended=True,
				winner="hostile",
				iterations=15,
				timeline_steps=7,
				damage=70,
				skill_id="fireball",
				attempts=5,
				successes=4,
				action_id="cast_fireball",
			),
			build_run(
				seed=10,
				ended=False,
				winner="",
				iterations=2000,
				timeline_steps=1800,
				damage=12000,
				skill_id="corrupt",
				attempts=1200,
				successes=1200,
				action_id="corrupt",
			),
		)
		profile_packets = packet_builder.build_profile_summaries(
			[baseline, candidate],
			{"report_shape": "profile_entries", "manual_policy": ""},
		)

		comparison = packet_builder.build_completed_only_comparisons(profile_packets)[0]

		self.assertEqual("independent_completed_run_aggregates", comparison["comparison_method"])
		self.assertEqual(5.0, comparison["average_iterations_delta"])
		self.assertEqual(2.0, comparison["skill_usage_delta"]["fireball"])
		self.assertEqual(-1.0, comparison["action_choice_delta"]["wait"])
		self.assertEqual(1.0, comparison["action_choice_delta"]["cast_fireball"])
		self.assertNotIn("corrupt", comparison["skill_usage_delta"])
		self.assertNotIn("corrupt", comparison["action_choice_delta"])

		trace_records = packet_builder.build_trace_records(
			{},
			{"scenario_id": "test"},
			[baseline, candidate],
		)
		self.assertEqual(["wait"], [trace["action_id"] for trace in trace_records["00_baseline"]])
		self.assertEqual(["cast_fireball"], [trace["action_id"] for trace in trace_records["candidate"]])

	def test_standalone_6v12_shape_recomputes_or_marks_unavailable(self) -> None:
		completed = build_run(
			seed=201,
			ended=True,
			winner="player",
			iterations=12,
			timeline_steps=5,
			damage=60,
			skill_id="charge",
			attempts=2,
			successes=1,
			action_id="charge_action",
		)
		unfinished = build_run(
			seed=202,
			ended=False,
			winner="",
			iterations=3000,
			timeline_steps=2500,
			damage=9999,
			skill_id="unfinished_skill",
			attempts=999,
			successes=999,
			action_id="unfinished_action",
		)
		for run in [completed, unfinished]:
			run.pop("battle_ended")
			run.pop("final_tu")
			metrics = run.pop("metrics")
			run["units"] = metrics["units"]
			run["factions"] = metrics["factions"]
		completed.pop("ai_turn_traces")
		report = {
			"scenario": {"scenario_id": "mixed_6v12", "manual_policy": "wait"},
			"requested_run_count": 2,
			"ended_count": 1,
			"avg_iterations": 1506.0,
			"avg_timeline_steps": 1252.5,
			"win_rate": {"player": 1, "draw": 1},
			"global": {"polluted": {"attempts": 1001, "successes": 1000}},
			"per_unit_summary": {"polluted": {"total_damage_done": 10059}},
			"runs": [completed, unfinished],
		}
		scenario = packet_builder.build_effective_scenario(report, Path("mixed_6v12.json"))
		entries = packet_builder.build_effective_profile_entries(report)
		profile_packet = packet_builder.build_profile_summaries(entries, scenario)[0]
		summary = profile_packet["summary"]

		self.assertEqual(12.0, summary["average_iterations"])
		self.assertEqual(5.0, summary["average_timeline_steps"])
		self.assertIsNone(summary["average_final_tu"])
		self.assertEqual({"player": 1}, summary["wins_by_faction"])
		self.assertEqual({"charge": 1}, summary["skill_usage_totals"])
		self.assertEqual(60, summary["faction_metric_totals"]["player"]["total_damage_done"])
		self.assertEqual(
			{"skill": 1},
			summary["faction_metric_totals"]["player"]["action_counts"],
		)
		self.assertEqual(
			{"charge": 2},
			summary["faction_metric_totals"]["player"]["skill_attempt_counts"],
		)
		self.assertEqual(
			{"charge": 1},
			summary["faction_metric_totals"]["player"]["skill_success_counts"],
		)
		self.assertEqual({}, summary["action_choice_counts"])
		self.assertEqual("unavailable", summary["completed_only_metric_sources"]["average_final_tu"])
		self.assertEqual("unavailable", summary["completed_only_metric_sources"]["action_choice_counts"])
		self.assertIn("action_choice_counts", profile_packet["guardrails"]["unavailable_completed_only_metrics"])
		self.assertEqual(2, len(profile_packet["run_digest"]))

	def test_missing_battle_ended_with_unassignable_draw_is_not_silently_inferred(self) -> None:
		runs = [
			build_run(
				seed=301 + index,
				ended=index == 0,
				winner="",
				iterations=20 + index,
				timeline_steps=8 + index,
				damage=30 + index,
				skill_id="draw_skill",
				attempts=1,
				successes=1,
				action_id="draw_action",
			)
			for index in range(2)
		]
		for run in runs:
			run.pop("battle_ended")
		report = {
			"scenario": {"scenario_id": "mixed_6v12", "manual_policy": "wait"},
			"requested_run_count": 2,
			"ended_count": 1,
			"runs": runs,
		}
		scenario = packet_builder.build_effective_scenario(report, Path("mixed_6v12.json"))
		entries = packet_builder.build_effective_profile_entries(report)
		profile_packet = packet_builder.build_profile_summaries(entries, scenario)[0]
		summary = profile_packet["summary"]

		self.assertFalse(summary["completion_classification_complete"])
		self.assertEqual(2, summary["completion_unknown_run_count"])
		self.assertEqual(1, summary["completed_run_count"])
		self.assertIsNone(summary["average_iterations"])
		self.assertEqual({}, summary["skill_usage_totals"])
		self.assertEqual({}, summary["faction_metric_totals"])
		warnings = " ".join(profile_packet["guardrails"]["warnings"])
		self.assertIn("completion_classification_unknown", warnings)
		self.assertEqual({}, packet_builder.build_trace_records({}, scenario, entries))

	def test_cli_packet_replaces_source_comparison_with_completed_only_comparison(self) -> None:
		baseline = build_profile_entry(
			"00_baseline",
			build_run(
				seed=401,
				ended=True,
				winner="player",
				iterations=10,
				timeline_steps=4,
				damage=50,
				skill_id="fireball",
				attempts=3,
				successes=2,
				action_id="wait",
			),
			build_run(
				seed=402,
				ended=False,
				winner="",
				iterations=1000,
				timeline_steps=900,
				damage=9000,
				skill_id="corrupt",
				attempts=900,
				successes=900,
				action_id="corrupt",
			),
		)
		candidate = build_profile_entry(
			"candidate",
			build_run(
				seed=499,
				ended=True,
				winner="hostile",
				iterations=20,
				timeline_steps=8,
				damage=80,
				skill_id="fireball",
				attempts=6,
				successes=5,
				action_id="cast_fireball",
			),
			build_run(
				seed=500,
				ended=False,
				winner="",
				iterations=2000,
				timeline_steps=1800,
				damage=12000,
				skill_id="corrupt",
				attempts=1200,
				successes=1200,
				action_id="corrupt",
			),
		)
		report = {
			"scenario": {"scenario_id": "comparison", "manual_policy": ""},
			"profile_entries": [baseline, candidate],
			"comparisons": [
				{
					"baseline_profile_id": "00_baseline",
					"candidate_profile_id": "candidate",
					"average_iterations_delta": 9999,
					"skill_usage_delta": {"corrupt": 9999},
				}
			],
		}
		with tempfile.TemporaryDirectory() as temp_dir:
			temp_path = Path(temp_dir)
			report_path = temp_path / "report.json"
			output_dir = temp_path / "packet"
			report_path.write_text(json.dumps(report), encoding="utf-8")
			args = argparse.Namespace(
				report=str(report_path),
				output_dir=str(output_dir),
				max_focus_traces=24,
				max_traces_per_profile=6,
				top_skills=5,
				top_actions=5,
				include_baseline_traces=True,
			)
			with mock.patch.object(packet_builder, "parse_args", return_value=args):
				self.assertEqual(0, packet_builder.main())

			summary_packet = json.loads((output_dir / "summary_for_llm.json").read_text(encoding="utf-8"))
			comparison = summary_packet["comparisons"][0]
			self.assertEqual("independent_completed_run_aggregates", comparison["comparison_method"])
			self.assertEqual(10.0, comparison["average_iterations_delta"])
			self.assertNotIn("corrupt", comparison["skill_usage_delta"])
			focus_rows = [
				json.loads(line)
				for line in (output_dir / "focus_traces.jsonl").read_text(encoding="utf-8").splitlines()
			]
			self.assertNotIn("corrupt", {row["action_id"] for row in focus_rows})


if __name__ == "__main__":
	unittest.main()

from __future__ import annotations

import copy
import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".codex/skills/design-godot-skill/scripts/assess_skill_power.py"
SPEC = importlib.util.spec_from_file_location("skill_power_assessment", SCRIPT)
assessment = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(assessment)


def case():
    return {
        "id": "test", "basis": "controlled_assumption", "source_sha256": {},
        "assumptions": ["Synthetic arithmetic fixture, not a balance oracle."],
        "duration_tu": 150, "cooldown_tu": 150,
        "costs": {"ap": 1, "mp": 45, "stamina": 0, "aura": 0},
        "scenario": {
            "horizon_tu": 320, "caster_interval_tu": 40, "ap_per_activation": 1,
            "resource_budget": {"mp": 90, "stamina": 0, "aura": 0},
            "reference_action_damage_hp": 10, "alternative_damage_per_ap": 10,
            "same_tu_order": "cast_first",
        },
        "on_cast": {},
        "streams": [{"id": "attack", "count": 1, "interval_tu": 30,
                     "first_event_tu": 30, "marginal_per_event": {"damage_prevented_hp": 2}}],
    }


class SkillPowerAssessmentTests(unittest.TestCase):
    def setUp(self):
        self.baseline = {"schema_version": 1, "baseline_version": "test",
                         "tu_step": 5, "reference_turn_tu": 40,
                         "source_sha256": {}, "calibration": "synthetic"}

    def evaluate(self, value):
        return assessment.evaluate(value, self.baseline)

    def test_cooldown_equal_to_duration_can_still_leave_a_gap(self):
        result = self.evaluate(case())
        self.assertEqual(result["cast_times_tu"], [0, 160])
        self.assertEqual(result["actual_cycle_tu"], 160)
        self.assertEqual(result["gap_per_ideal_cycle_tu"], 10)
        self.assertEqual(result["covered_tu"], 300)

    def test_refresh_uses_union_and_counts_each_event_once(self):
        value = case()
        value["duration_tu"] = 180
        result = self.evaluate(value)
        self.assertEqual(result["covered_tu"], 320)
        self.assertEqual(result["refresh_overlap_tu"], 20)
        self.assertEqual(result["streams"][0]["affected_events"], 10)
        self.assertEqual(result["horizon_marginal"]["damage_prevented_hp"], 20)

    def test_expiry_does_not_include_the_event_at_exact_end(self):
        value = case()
        value.update(duration_tu=60, max_casts=1)
        result = self.evaluate(value)
        self.assertEqual(result["single_cast_streams"][0]["affected_events"], 1)
        value["streams"][0]["first_event_tu"] = 5
        self.assertEqual(self.evaluate(value)["single_cast_streams"][0]["affected_events"], 2)

    def test_same_tu_order_changes_only_tied_events(self):
        value = case()
        value.update(duration_tu=40, cooldown_tu=40, max_casts=1)
        value["streams"][0].update(first_event_tu=0, interval_tu=40)
        self.assertEqual(self.evaluate(value)["streams"][0]["affected_events"], 1)
        value["scenario"]["same_tu_order"] = "threat_first"
        self.assertEqual(self.evaluate(value)["streams"][0]["affected_events"], 0)

    def test_budget_limits_coverage_even_when_timing_allows_refresh(self):
        value = case()
        value["duration_tu"] = 180
        value["scenario"]["resource_budget"]["mp"] = 45
        result = self.evaluate(value)
        self.assertEqual(result["cast_times_tu"], [0])
        self.assertEqual(result["stop_reason"], "insufficient_mp")
        self.assertEqual(result["first_failed_cast_tu"], 160)
        self.assertEqual(result["covered_tu"], 180)
        self.assertEqual(result["spent"]["mp"], 45)

    def test_ap_and_each_resource_gate_before_effects(self):
        for resource in ("ap", "stamina", "aura"):
            with self.subTest(resource=resource):
                value = case()
                value["costs"][resource] = 2
                result = self.evaluate(value)
                self.assertEqual(result["cast_count"], 0)
                self.assertEqual(result["horizon_marginal"]["damage_prevented_hp"], 0)

    def test_direct_effects_and_zero_cost_ratios_remain_separate(self):
        value = case()
        value.update(duration_tu=0, cooldown_tu=0, max_casts=2)
        value["costs"].update(ap=0, mp=0)
        value["on_cast"] = {"damage_dealt_hp": 10, "enemy_ap_denied": 1}
        result = self.evaluate(value)
        self.assertEqual(result["cast_times_tu"], [0, 40])
        self.assertEqual(result["horizon_marginal"]["damage_dealt_hp"], 20)
        self.assertEqual(result["horizon_marginal"]["enemy_ap_denied"], 2)
        self.assertIsNone(result["metric_per_mp"]["damage_dealt_hp"])
        self.assertNotIn("enemy_ap_denied", result["hp_lanes_in_reference_actions"])

    def test_unmodeled_casting_and_regeneration_are_rejected(self):
        for field in ("casting_time_tu", "mp_regen_per_tu"):
            with self.subTest(field=field):
                value = case()
                value[field] = 10
                with self.assertRaisesRegex(ValueError, "unsupported fields"):
                    self.evaluate(value)

    def test_invalid_quantization_and_nonfinite_metrics_rejected(self):
        value = case()
        value["duration_tu"] = 151
        with self.assertRaises(ValueError):
            self.evaluate(value)
        value = case()
        value["on_cast"]["damage_dealt_hp"] = float("nan")
        with self.assertRaises(ValueError):
            self.evaluate(value)

    def test_input_is_not_mutated(self):
        value = case()
        original = copy.deepcopy(value)
        self.evaluate(value)
        self.assertEqual(value, original)

    def test_changed_baseline_or_evidence_stops_calculation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "owner.cs"
            source.write_text("first\n", encoding="utf-8")
            baseline = copy.deepcopy(self.baseline)
            baseline["source_sha256"] = {"owner.cs": assessment.normalized_hash(source)}
            packet = {"schema_version": 1, "cases": [case()]}
            self.assertEqual(assessment.run(packet, baseline, root)["status"], "arithmetic_only")
            source.write_text("changed\n", encoding="utf-8")
            result = assessment.run(packet, baseline, root)
            self.assertEqual(result["status"], "baseline_stale")
            self.assertEqual(result["cases"], [])
            packet["cases"][0]["source_sha256"] = baseline["source_sha256"]
            result = assessment.run(packet, self.baseline, root)
            self.assertEqual(result["status"], "evidence_stale")

    def test_real_evidence_cannot_be_unpinned(self):
        value = case()
        value["basis"] = "canonical_preview"
        with self.assertRaisesRegex(ValueError, "pinned"):
            self.evaluate(value)


if __name__ == "__main__":
    unittest.main()

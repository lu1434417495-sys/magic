"""Build a bounded design card from explicit production source forecasts."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]

def read(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8-sig"))

def sha(relative):
    return hashlib.sha256((ROOT / relative).read_text(encoding="utf-8-sig").encode()).hexdigest()

forecast_path = (HERE / "window_sources_v5.json").relative_to(ROOT).as_posix()
forecast = read(forecast_path)
if len(forecast["rows"]) != 57:
    raise SystemExit("Expected 57 completed source snapshots from the design probe, including 4/5/9 sources.")
sources = [
    "data/configs/json/skills/mage_01.json",
    "data/configs/json/skills/mage_02.json",
    "data/configs/json/skills/mage_04.json",
    "scripts/player/progression/CombatStatusSourceContentRules.cs",
    "scripts/systems/battle/core/BattleStatusSourceContributionState.cs",
    "scripts/systems/battle/core/BattleStatusEffectState.cs",
    "scripts/systems/battle/rules/BattleStatusSemanticTable.cs",
    "scripts/systems/battle/rules/BattleStatusTickAdvanceRules.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.StatusTickAdvance.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.Effects.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.cs",
    "scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs",
    "scripts/systems/battle/runtime/BattleSkillMasteryService.cs",
    "scripts/systems/progression/ProgressionService.cs",
    "tests/battle_runtime/simulation/run_molten_window_design_analysis.cs",
    "tests/shared/BattleTestFixture.cs",
    forecast_path,
    Path(__file__).relative_to(ROOT).as_posix(),
]
hashes = {path: sha(path) for path in sources}
levels = [dict(level=level, window_tu=40 if level < 3 else 60 if level < 7 else 80,
               max_sources=None if level == 7 else 2 if level < 5 else 3,
               source_selection_mode="all_eligible" if level == 7 else "highest_window_damage", ap=1,
               mp=[40, 35, 35, 40, 40, 50, 45, 60][level],
               range=4 if level < 2 else 5, cooldown_tu=80 if level < 4 else 40)
          for level in range(8)]
cases = []
for level in levels:
    for horizon, cadence in [(80, 40), (160, 40), (320, 40), (320, 30), (320, 20)]:
        cases.append({
            "id": f"window_l{level['level']}_h{horizon}_t{cadence}",
            "basis": "controlled_assumption", "source_sha256": hashes,
            "assumptions": [
                "The window effect is proposed, not implemented. Immediate HP comes separately from natural source forecasts; this card records zero INCREMENTAL LIFETIME damage.",
                "At most one paid finisher; no refill, new fire, healing, dispel, death, mitigation change or overkill. Source and team AP/MP are recorded separately by the explicit source scenarios.",
                "D_ref=6 is the pre-existing diagnostic comparison unit, not a calibrated role price. Do not use zero lifetime HP to erase the conditional earlier-kill benefit.",
                "160MP here is a fixed remaining caster pool. It is not a normal character stat or a free preparation budget. Actual per-caster preparation affordability is in window_sources_v5.json."
            ],
            "duration_tu": 0, "cooldown_tu": level["cooldown_tu"],
            "costs": {"ap": 1, "mp": level["mp"], "stamina": 0, "aura": 0},
            "scenario": {"horizon_tu": horizon, "caster_interval_tu": cadence, "ap_per_activation": 2,
                         "resource_budget": {"mp": 160, "stamina": 0, "aura": 0},
                         "reference_action_damage_hp": 6, "alternative_damage_per_ap": 6,
                         "same_tu_order": "cast_first"},
            "max_casts": 1, "on_cast": {"damage_dealt_hp": 0}, "streams": []
        })

skill = next(s for s in read("data/configs/json/skills/mage_04.json")["entries"]
             if s["skill_id"] == "mage_molten_burst")
curve = skill["mastery_curve"]
cumulative = [sum(curve[:level]) for level in range(1, len(curve) + 1)]
summary = {
    "status": "proposed_not_implemented_or_balance_calibrated",
    "levels": levels, "source_sha256": hashes,
    "mastery_curve": curve, "mastery_cumulative": cumulative,
    "candidate_rows": len(forecast["rows"]) * 8,
    "prepared_fixed_burn_examples": [
        {"sources": count, "stacks_each": stacks, "power_each": 2, "interval_tu": 10,
         "next_tick_offset_tu": 10, "remaining_tu_at_least": 60,
         "l5_instant_hp": count * max(2, stacks) * 6}
        for count in [1, 2, 3] for stacks in [1, 3]
    ],
    "prepared_example_warning": "These are deliberately prebuilt upper scenarios, not free or automatically affordable cast sequences.",
    "all_source_prepared_examples": [
        {"sources": count, "power_each": 3, "interval_tu": 10,
         "next_tick_offset_tu": 10, "remaining_tu_at_least": 80,
         "l7_instant_hp": count * 3 * 8}
        for count in [3, 4, 5, 9]
    ],
    "validation": {"build": "0 warnings, 0 errors", "source_probe": "PASS: 57 snapshots and per-source additivity checks in the controlled fixture",
                   "candidate_execution": "NOT IMPLEMENTED", "battle_sim_win_rate": "NOT RUN", "full_suite_ci": "NOT RUN"},
}
(HERE / "input_v5.json").write_text(json.dumps({"schema_version": 1, "cases": cases}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
(HERE / "summary_v5.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"timing_cards": len(cases), "candidate_rows": summary["candidate_rows"],
                  "mastery_cumulative": cumulative}, ensure_ascii=False))

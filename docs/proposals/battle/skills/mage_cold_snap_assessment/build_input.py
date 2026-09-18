"""Explicit local author probes; does not mutate gameplay content or run BattleSim."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
LEVELS = [
    # level, MP, range, radius, base D6, cooldown TU
    (0,70,4,1,4,120), (1,65,4,1,4,120), (2,65,5,1,4,120),
    (3,65,5,1,6,120), (4,65,5,1,6,80), (5,65,5,2,6,80),
    (6,60,5,2,6,80), (7,60,5,2,8,80),
]
SOURCES = [
    "data/configs/json/skills/mage_01.json",
    "scripts/systems/battle/rules/BattleDamageBonusConditionRules.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.SaveBranch.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.DamageOutcome.cs",
    "scripts/systems/battle/rules/BattleDamageResolver.Preview.cs",
    "scripts/systems/battle/rules/BattleStatusSemanticTable.cs",
    "scripts/systems/battle/runtime/BattleSkillMasteryService.cs",
    "scripts/systems/progression/ProgressionService.cs",
    "scripts/systems/progression/SkillEffectiveMaxLevelRules.cs",
    str(Path(__file__).resolve().relative_to(ROOT)).replace("\\", "/"),
]
hashes = {p:hashlib.sha256((ROOT/p).read_text(encoding="utf-8-sig").encode()).hexdigest() for p in SOURCES}
cases = []
def case(name, row, targets=3, chilled=1, save_success=.5, horizon=320, budget=160, single=True):
    level,mp,range_value,radius,dice,cooldown = row
    expected = dice*3.5*(targets + .5*chilled)*(1-.5*save_success)
    value = {
        "id":name, "basis":"controlled_assumption", "source_sha256":hashes,
        "assumptions":[
            f"L{level}; range {range_value}; radius {radius}; {targets} actual enemies; {chilled} already have bone_chill at this cast.",
            f"Base {dice}D6; chilled targets only use proposed 150% pre-mitigation multiplier; save success probability {save_success}; success halves damage.",
            "Ignores integer rounding, resistance, mitigation, equipment, overkill, death and geometry. Fractional HP is author arithmetic, not engine expected damage.",
            "Existing bone_chill preparation is external to this single-cast probe. Its AP, MP, hit chance and status expiry must be assessed separately.",
            "Multi-cast probes contain no chilled target; they never assume a 60-TU debuff survives until recast or is freely reapplied.",
            "No slow, AP denial or healing suppression is added to this skill's payoff. Source skill's suppression remains separate.",
        ],
        "duration_tu":0, "cooldown_tu":cooldown,
        "costs":{"ap":2,"mp":mp,"stamina":0,"aura":0},
        "scenario":{"horizon_tu":horizon,"caster_interval_tu":40,"ap_per_activation":2,
                    "resource_budget":{"mp":budget,"stamina":0,"aura":0},
                    "reference_action_damage_hp":6,"alternative_damage_per_ap":6,"same_tu_order":"cast_first"},
        "on_cast":{"damage_dealt_hp":expected},"streams":[],
    }
    if single:
        value["max_casts"]=1
    cases.append(value)

for row in LEVELS:
    case(f"candidate_l{row[0]}_three_targets_one_chilled",row)
for level in [0,5,7]:
    case(f"candidate_l{level}_unprepared_repeat",LEVELS[level],chilled=0,single=False)
for name,n,k,q in [("solo_unprepared",1,0,.5),("solo_chilled",1,1,.5),
                    ("three_unprepared",3,0,.5),("three_chilled",3,3,.5),
                    ("all_save",3,1,1),("all_fail_save",3,1,0)]:
    case("candidate_l5_"+name,LEVELS[5],targets=n,chilled=k,save_success=q)
case("candidate_l0_short_unprepared",LEVELS[0],chilled=0,horizon=80,single=False)
case("candidate_l7_half_window_unprepared",LEVELS[7],chilled=0,horizon=160,single=False)
case("candidate_l0_insufficient_mp",LEVELS[0],chilled=0,budget=60)
for row in [(0,80,4,1,4,10),(5,80,5,1,7,5),(7,80,5,2,8,5)]:
    case(f"old_l{row[0]}_damage_only",row,chilled=0)
    cases[-1]["assumptions"].append("CURRENT production comparator; its separate slow effect is intentionally unpriced here, so damage-only comparison cannot prove net dominance.")

(HERE/"input.json").write_text(json.dumps({"schema_version":1,"cases":cases},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(f"Wrote {len(cases)} controlled cases")

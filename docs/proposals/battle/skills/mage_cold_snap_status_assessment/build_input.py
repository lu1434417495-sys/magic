"""Bounded cold-snap arithmetic probes; no skill discovery or battle simulation."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCES = [
    'data/configs/json/skills/mage_01.json',
    'scripts/systems/battle/rules/BattleStatusSemanticTable.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.DamageOutcome.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.SaveBranch.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.Preview.cs',
    'scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs',
    'scripts/systems/battle/runtime/BattleSkillMasteryService.cs',
    'scripts/systems/progression/ProgressionService.cs',
    'scripts/systems/battle/ai/BattleAiScoreService.Vulnerability.cs',
    Path(__file__).relative_to(ROOT).as_posix(),
]
hashes = {p: hashlib.sha256((ROOT/p).read_text(encoding='utf-8-sig').encode()).hexdigest() for p in SOURCES}
family = json.loads((ROOT/SOURCES[0]).read_text(encoding='utf-8-sig'))
skill = next(s for s in family['entries'] if s['skill_id'] == 'mage_cold_snap')
combat = skill['combat_profile']
cases = []

def case(level, label, *, follow=0, first=20, delta=5.25, repeat=False, budget=160, target_factor=3):
    values = dict(combat)
    values.update(combat['level_overrides'].get(str(level), {}))
    effect = next(e for e in combat['effect_defs'] if e['effect_type']=='damage'
                  and e.get('min_skill_level',0)<=level<=e.get('max_skill_level',7))
    cases.append({
        'id':f'l{level}_{label}', 'basis':'controlled_assumption', 'source_sha256':hashes,
        'assumptions':[
            f'Three actual surviving enemies; cold snap saves succeed with probability 0.5 and halve damage; D6 mean 3.5; combined damage factor for the three targets is {target_factor}.',
            'For vulnerability probes, one target already has two bone_chill stacks from two allied successful casts. Each L0 preparation separately costs 1 AP and 20 MP; total preparation 2 AP/40 MP is not charged to the cold-snap caster budget.',
            'A follow-up is one paid allied 2D6 cold spell with save-success probability 0.5. Ordinary-target marginal damage is 5.25; its own AP/MP is external and must be added to a team budget.',
            'New vulnerability never increases the triggering cold snap. Pre-existing vulnerability does. No consumption or renewal of bone_chill. No automatic repeat vulnerability preparation.',
            'Each stream count is the explicitly assumed number of independent allies/actions, not a skill target-count multiplier.',
            'Ignores integer rounding, fixed reduction, equipment, movement, death and overkill. Runtime first floors partial saves, then doubles vulnerability; exact integer cases are verified by run_post_save_vulnerability_regression, not this mean-value arithmetic.',
            'Repeat-cast probes are unprepared: no vulnerability follow-ups. This is arithmetic, not measured encounter pace or balance PASS.',
        ],
        'duration_tu':60,'cooldown_tu':values['cooldown_tu'],
        'costs':{'ap':2,'mp':values['mp_cost'],'stamina':0,'aura':0},
        'scenario':{'horizon_tu':320,'caster_interval_tu':40,'ap_per_activation':2,
            'resource_budget':{'mp':budget,'stamina':0,'aura':0},
            'reference_action_damage_hp':6,'alternative_damage_per_ap':6,'same_tu_order':'cast_first'},
        'on_cast':{'damage_dealt_hp':effect['dice_count']*3.5*target_factor*0.75},
        'streams':[] if follow==0 else [{'id':'paid_allied_cold_follow_up','count':follow,
            'first_event_tu':first,'interval_tu':320,'marginal_per_event':{'damage_dealt_hp':delta}}],
    })
    if not repeat:
        cases[-1]['max_casts']=1

for level in range(8):
    case(level,'no_follow_up')
for level in (0,5,7):
    case(level,'one_follow_up',follow=1)
    case(level,'two_follow_ups',follow=2)
    case(level,'unprepared_repeat',repeat=True)
case(5,'early_follow_up',follow=1,first=5)
case(5,'expired_follow_up',follow=1,first=60)
case(5,'resistant_target',follow=1,delta=2.625,target_factor=2.5)
case(5,'immune_target',follow=1,delta=0,target_factor=2)
case(5,'already_vulnerable',follow=1,delta=0,target_factor=4)
case(5,'two_sources_each_hit_60_percent',follow=1,delta=5.25*0.6*0.6)
case(0,'insufficient_mp',budget=60)
(HERE/'input.json').write_text(json.dumps({'schema_version':1,'cases':cases},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Wrote {len(cases)} bounded cases')

"""Fixed skill input card: retimed damage is not new lifetime damage.

Only reads the selected skill and pinned owners. No skill discovery, combat math,
or BattleSim. Immediate HP evidence comes from the production C# regression.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCES = [
    'data/configs/json/skills/mage_04.json',
    'scripts/systems/battle/rules/BattleStatusTickAdvanceRules.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.StatusTickAdvance.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.cs',
    'scripts/systems/battle/rules/BattleStatusSemanticTable.cs',
    'scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs',
    'scripts/systems/battle/runtime/BattleSkillMasteryService.cs',
    'scripts/systems/progression/ProgressionService.cs',
    'tests/battle_runtime/skills/run_mage_molten_burst_regression.cs',
    Path(__file__).relative_to(ROOT).as_posix(),
]

parser = argparse.ArgumentParser()
parser.add_argument('--runtime-log', required=True)
args = parser.parse_args()
log_path = ROOT / args.runtime_log
log = log_path.read_text(encoding='utf-8-sig')
if 'Mage molten burst regression: PASS' not in log or 'Failed: 0' not in log or 'regression: FAIL' in log:
    raise SystemExit('Require a completed passing focused runtime log, without failed attempts.')
probes = [dict(zip(('level','immediate_hp','lifetime_hp','baseline_hp'), map(int, match)))
          for match in re.findall(r'MOLTEN_PROBE level=(\d+) immediate_hp=(\d+) lifetime_hp=(\d+) baseline_hp=(\d+)', log)]
if len(probes) != 7:
    raise SystemExit('Expected seven mechanism/breakpoint runtime probes.')
hashes = {p: hashlib.sha256((ROOT/p).read_text(encoding='utf-8-sig').encode()).hexdigest() for p in SOURCES}
skill = next(s for s in json.loads((ROOT/SOURCES[0]).read_text(encoding='utf-8-sig'))['entries']
             if s['skill_id'] == 'mage_molten_burst')
profile = skill['combat_profile']
cases = []
def add(level, horizon=160, interval=40):
    effective = dict(profile)
    for key, override in sorted(profile['level_overrides'].items(), key=lambda item: int(item[0])):
        if int(key) <= level:
            effective.update(override)
    cases.append({
        'id':f'l{level}_h{horizon}_t{interval}', 'basis':'controlled_assumption', 'source_sha256':hashes,
        'assumptions':[
            'One paid cast, one already burning enemy; no regeneration, new fire application, expiry removal, death, overkill, changed mitigation, or movement.',
            'on_cast damage_dealt_hp=0 denotes INCREMENTAL LIFETIME damage relative to letting the same finite burns run. It is not zero immediate damage. Immediate HP and 80/160/320TU conservation are recorded separately by the production C# probe.',
            'Preparation is already sunk: C# fixture source A power2/80TU, B power3/60TU, both every10TU; no assertion that this synthetic pair is a normal player build. Source AP/MP must be included separately when comparing complete team combos.',
            'MP pool is fixed at160 for every level. D_ref=6 and alternative6HP/AP are comparison probes, not a calibrated skill-power price.',
            'Time-to-kill and saved enemy actions are conditional benefits, not assigned a made-up occurrence probability or added to lifetime damage.',
        ],
        'duration_tu':0,'cooldown_tu':effective['cooldown_tu'],
        'costs':{'ap':1,'mp':effective['mp_cost'],'stamina':0,'aura':0},
        'scenario':{'horizon_tu':horizon,'caster_interval_tu':interval,'ap_per_activation':2,
            'resource_budget':{'mp':160,'stamina':0,'aura':0},
            'reference_action_damage_hp':6,'alternative_damage_per_ap':6,'same_tu_order':'cast_first'},
        'max_casts':1,'on_cast':{'damage_dealt_hp':0},'streams':[],
    })

for level in range(8): add(level)
for level in (0,5,7):
    for horizon in (80,320): add(level,horizon)
    for interval in (20,30): add(level,interval=interval)
(HERE/'input_v3.json').write_text(json.dumps({'schema_version':1,'cases':cases},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(HERE/'runtime_v3.json').write_text(json.dumps({
    'kind':'focused_production_runtime_probe','test':'run_mage_molten_burst_regression',
    'source_sha256':hashes,'fixture':'A: power2/80TU; B: power3/60TU; both tick10TU; HP1000; no mitigation',
    'horizons_tu':[80,160,320],'probes':probes,
    'balance_calibration':'uncalibrated; behavior and bounded arithmetic only',
},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Wrote {len(cases)} arithmetic cards and {len(probes)} production runtime observations.')

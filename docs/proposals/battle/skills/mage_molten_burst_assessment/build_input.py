"""Bounded proposal probes. Does not discover skills or reproduce combat RNG."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCES = [
    'data/configs/json/skills/mage_04.json',
    'data/configs/json/skills/mage_01.json',
    'scripts/player/progression/SkillDefinition.cs',
    'scripts/systems/battle/rules/BattleStatusSemanticTable.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.DamageOutcome.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.SaveBranch.cs',
    'scripts/systems/battle/rules/BattleDamageResolver.Preview.cs',
    'scripts/systems/battle/core/BattleStatusEffectState.cs',
    'scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs',
    'scripts/systems/battle/runtime/BattleSkillMasteryService.cs',
    'scripts/systems/progression/ProgressionService.cs',
    Path(__file__).relative_to(ROOT).as_posix(),
]
hashes = {p: hashlib.sha256((ROOT / p).read_text(encoding='utf-8-sig').encode()).hexdigest() for p in SOURCES}
cases = []

def case(level, label, *, stacks=(0, 0, 0), interval=40, horizon=320,
         repeat=False, budget=160, old=False, save_success=0.5, prepared_probability=1.0):
    dice = (4 if level == 0 else 5 if level <= 2 else 6 if level <= 4 else 7 if level <= 6 else 8) if old else (4 if level < 3 else 6 if level < 7 else 8)
    mp = 80 if old or level == 0 else 75 if level < 6 else 70
    cooldown = (10 if level < 5 else 5) if old else (120 if level < 4 else 80)
    bonus = 0 if old else sum(min(max(s, 0), 3) for s in stacks) * prepared_probability
    cases.append({
        'id': f'{"old" if old else "proposal"}_l{level}_{label}',
        'basis': 'controlled_assumption', 'source_sha256': hashes,
        'assumptions': [
            f'Proposed target-status bonus dice are NOT implemented. Actual enemies: {len(stacks)}; valid burning stacks at this cast: {list(stacks)}.',
            'One D6 per valid target burning stack, cap three per target. Existing burning damage is excluded because this proposal neither consumes nor refreshes it.',
            f'D6 mean 3.5; save success probability {save_success}; partial save halves damage; ignores integer rounding, equipment, criticals, mitigation, death and overkill.',
            f'Preparation probability {prepared_probability} is an explicit assumption. Preparation AP/MP belongs to the source skill and is additional team expenditure.',
            'Every repeat-cast probe is unprepared; no burning is silently renewed between casts. Each action costs two AP and has zero casting time.',
            'Old-skill cases show only direct damage: its separately saved, source-scoped burning is excluded, so this comparison is not old/new total damage superiority.',
            'Area radius never supplies automatic extra targets; counted targets are held fixed.',
        ],
        'duration_tu': 0, 'cooldown_tu': cooldown,
        'costs': {'ap': 2, 'mp': mp, 'stamina': 0, 'aura': 0},
        'scenario': {'horizon_tu': horizon, 'caster_interval_tu': interval, 'ap_per_activation': 2,
            'resource_budget': {'mp': budget, 'stamina': 0, 'aura': 0},
            'reference_action_damage_hp': 6, 'alternative_damage_per_ap': 6, 'same_tu_order': 'cast_first'},
        'on_cast': {'damage_dealt_hp': (dice * len(stacks) + bonus) * 3.5 * (1 - save_success / 2)},
        'streams': [],
    })
    if not repeat:
        cases[-1]['max_casts'] = 1

for level in range(8):
    for stacks in (0, 1, 3):
        case(level, f'one_target_stacks_{stacks}', stacks=(stacks, 0, 0))
for level in (0, 5, 7):
    case(level, 'repeat_unprepared', repeat=True)
    case(level, 'old_direct_only', old=True)
case(5, 'all_targets_maximum', stacks=(3, 3, 3))
case(5, 'six_stacks_still_capped', stacks=(6, 0, 0))
case(5, 'one_target_only', stacks=(1,))
case(5, 'one_preparation_save_fails_half', stacks=(1, 0, 0), prepared_probability=0.5)
case(5, 'all_save_success', stacks=(3, 0, 0), save_success=1)
case(5, 'all_save_failure', stacks=(3, 0, 0), save_success=0)
case(5, '30tu_repeat', repeat=True, interval=30)
case(5, '20tu_repeat', repeat=True, interval=20)
case(5, '80tu_short', repeat=True, horizon=80)
case(0, 'cannot_afford', budget=70)
(HERE / 'input.json').write_text(json.dumps({'schema_version': 1, 'cases': cases}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Wrote {len(cases)} controlled arithmetic cases')

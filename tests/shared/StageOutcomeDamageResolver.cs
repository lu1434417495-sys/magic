using System.Collections.Generic;
using Godot;
using GArrayBool = Godot.Collections.Array<bool>;
using GArrayInt = Godot.Collections.Array<int>;
using GArrayStringName = Godot.Collections.Array<Godot.StringName>;
using GDictionary = Godot.Collections.Dictionary;

public partial class StageOutcomeDamageResolver : BattleDamageResolver
{
    public GArrayBool stage_successes = new();
    public GArrayInt stage_damage = new();
    public GArrayStringName target_ids_seen = new();
    public GArrayStringName dead_target_ids_seen = new();
    public GArrayInt hp_before_by_call = new();
    public int call_count = 0;

    internal override AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        return ResolveStageOutcome(target_unit, attack_check);
    }

    private AttackEffectResolutionResult ResolveStageOutcome(
        BattleUnitState target_unit,
        AttackCheckInput attack_check
    )
    {
        bool success = call_count < stage_successes.Count && stage_successes[call_count];
        int damage = call_count < stage_damage.Count ? stage_damage[call_count] : 0;
        call_count += 1;

        if (target_unit != null)
        {
            target_ids_seen.Add(target_unit.unit_id);
            hp_before_by_call.Add(target_unit.GetCurrentHp());
            if (!target_unit.IsAlive())
            {
                dead_target_ids_seen.Add(target_unit.unit_id);
            }
        }

        if (!success)
        {
            return AttackEffectResolutionResultReader.FinalizeTypedResult(
                new AttackEffectResolutionResult
                {
                    AttackSuccess = false,
                    AttackResolution = AttackResolutionKind.Miss,
                    HitRoll = 1,
                    Applied = false,
                    Damage = 0,
                    Healing = 0,
                    StatusEffectIds = new GArrayStringName(),
                    SourceStatusEffectIds = new GArrayStringName(),
                    AttackCheck = attack_check,
                }
            );
        }

        if (target_unit != null && damage > 0)
        {
            target_unit.SetCurrentHp(Mathf.Max(target_unit.GetCurrentHp() - damage, 0));
            if (target_unit.GetCurrentHp() <= 0)
            {
                target_unit.MarkDead();
            }
        }

        return AttackEffectResolutionResultReader.FinalizeTypedResult(
            new AttackEffectResolutionResult
            {
                AttackSuccess = true,
                AttackResolution = AttackResolutionKind.Hit,
                HitRoll = 10,
                Applied = true,
                Damage = damage,
                HpDamage = damage,
                Healing = 0,
                StatusEffectIds = new GArrayStringName(),
                SourceStatusEffectIds = new GArrayStringName(),
                AttackCheck = attack_check,
            }
        );
    }
}

using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleNineEchoFinalHammerResolver
{
    internal static readonly StringName SkillId = "warrior_nine_echo_final_hammer";

    private static readonly StringName StaggeredStatusId = "staggered";
    private static readonly StringName ProneStatusId = "prone";
    private static readonly StringName StunnedStatusId = "stunned";

    private const int StaggeredHitThreshold = 3;
    private const int ProneHitThreshold = 6;
    private const int TerminalHitThreshold = 9;
    private const int StaggeredDurationTu = 60;
    private const int ProneDurationTu = 50;
    private const int StunnedDurationTu = 30;
    private const int TerminalWeaponDiceMultiplier = 3;

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;

    private BattleRuntimeModule Runtime =>
        _runtimeRef != null
        && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime)
            ? runtime
            : null;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleSkillExecutionOrchestrator owner
    )
    {
        _runtimeRef =
            runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _owner = owner;
    }

    internal void DisposeRuntime()
    {
        _runtimeRef = null;
        _owner = null;
    }

    internal void ApplySuccessfulHitReward(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int successfulHitCount,
        BattleEventBatch batch
    )
    {
        if (
            activeUnit == null
            || targetUnit?.IsAlive() != true
            || skillDefinition?.SkillId != SkillId
        )
        {
            return;
        }

        switch (successfulHitCount)
        {
            case StaggeredHitThreshold:
                ApplyDebuff(
                    activeUnit,
                    targetUnit,
                    StaggeredStatusId,
                    StaggeredDurationTu,
                    batch,
                    $"{targetUnit.display_name} 连受三响，陷入踉跄。"
                );
                break;
            case ProneHitThreshold:
                if (HasKnockdownImmunity(targetUnit))
                {
                    batch?.AddLogLine(
                        $"{targetUnit.display_name} 抵抗了九响终槌的六响倒地。"
                    );
                    break;
                }
                ApplyDebuff(
                    activeUnit,
                    targetUnit,
                    ProneStatusId,
                    ProneDurationTu,
                    batch,
                    $"{targetUnit.display_name} 连受六响，被震倒在地。"
                );
                break;
            case TerminalHitThreshold:
                ApplyTerminalReward(
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    effectDefinitions,
                    batch
                );
                break;
        }
    }

    private void ApplyTerminalReward(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        ApplyDebuff(
            activeUnit,
            targetUnit,
            StunnedStatusId,
            StunnedDurationTu,
            batch,
            $"{targetUnit.display_name} 承受九响，陷入震晕。"
        );

        if (targetUnit?.IsAlive() != true || _owner == null)
        {
            return;
        }

        List<CombatEffectDefinition> terminalEffects = BuildTerminalDamageEffects(
            effectDefinitions
        );
        if (terminalEffects.Count == 0)
        {
            return;
        }

        batch?.AddLogLine(
            $"{activeUnit.display_name} 引爆九响终槌：追加攻击必中，并独立进行重击判定。"
        );
        _owner._apply_unit_skill_result(
            activeUnit,
            targetUnit,
            skillDefinition,
            castVariantDefinition,
            terminalEffects,
            batch,
            force_hit_allow_crit: true
        );
    }

    private void ApplyDebuff(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        StringName statusId,
        int durationTu,
        BattleEventBatch batch,
        string logLine
    )
    {
        BattleSpecialSkillResolver resolver = Runtime?._special_skill_resolver;
        if (resolver == null)
        {
            return;
        }

        bool terminalLock = statusId == StunnedStatusId;
        resolver.SetRuntimeStatusEffect(
            targetUnit,
            statusId,
            durationTu,
            activeUnit.unit_id,
            source_skill_id: SkillId,
            counts_as_debuff_override: true,
            counts_as_debuff: true,
            lock_counterattack: terminalLock,
            lock_guard: terminalLock,
            lock_dodge_bonus: terminalLock,
            lock_crit: terminalLock
        );
        Runtime?.MarkAppliedStatusesForTurnTiming(targetUnit, new[] { statusId });
        _owner?._append_changed_unit_id(batch, targetUnit.unit_id);
        batch?.AddLogLine(logLine);
    }

    private static bool HasKnockdownImmunity(BattleUnitState targetUnit)
    {
        foreach (
            BattleStatusEffectState status in
                targetUnit?.GetStatusEffectsTyped()
                ?? new List<BattleStatusEffectState>()
        )
        {
            if (status?.stacks > 0 && status.forced_move_immune)
            {
                return true;
            }
        }
        return false;
    }

    private static List<CombatEffectDefinition> BuildTerminalDamageEffects(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind != BattleEffectKind.Damage)
            {
                continue;
            }
            result.Add(
                effectDefinition.WithWeaponDiceMultiplier(
                    TerminalWeaponDiceMultiplier
                )
            );
        }
        return result;
    }
}

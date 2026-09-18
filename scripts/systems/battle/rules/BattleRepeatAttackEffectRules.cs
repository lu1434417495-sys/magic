using System;
using System.Collections.Generic;

/// <summary>
/// 连击（repeat_attack / fixed_repeat_attack）的逐段效果构建。执行、预览与 AI 评分共用这一份规则。
/// </summary>
internal static class BattleRepeatAttackEffectRules
{
    internal const int StageGuard = 32;

    internal readonly record struct RuntimeParameters(
        bool StopOnMiss,
        bool StopOnTargetDown,
        int FollowUpDamageMultiplierPercent
    )
    {
        public static RuntimeParameters FromEffect(CombatEffectDefinition effectDefinition)
        {
            return new RuntimeParameters(
                effectDefinition?.StopOnMiss ?? true,
                effectDefinition?.StopOnTargetDown ?? true,
                effectDefinition?.FollowUpDamageMultiplierPercent ?? 100
            );
        }

        public int GetStageDamagePercent(int stageIndex)
        {
            // 按段整数复合,除以 100 时向下截断(50% → 25% → 12%),符合
            // DnD 取整惯例且各段结果可精确复现。
            int percent = 100;
            for (int stage = 0; stage < stageIndex; stage++)
            {
                percent = percent * FollowUpDamageMultiplierPercent / 100;
            }
            return percent;
        }
    }

    internal static List<CombatEffectDefinition> CollectBaseEffects(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        var stagedEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && BattleTypedNames.IsUnitPayloadEffect(effectDefinition.EffectKind)
            )
            {
                stagedEffects.Add(effectDefinition);
            }
        }
        return stagedEffects;
    }

    internal static List<CombatEffectDefinition> BuildStageEffects(
        IEnumerable<CombatEffectDefinition> baseEffects,
        int damagePercent
    )
    {
        var stagedEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in
                baseEffects ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            CombatEffectDefinition stageEffect = effectDefinition;
            if (
                stageEffect.EffectKind == BattleEffectKind.Damage
                && damagePercent != 100
            )
            {
                // 整数百分比只在交给伤害管线的边界处转一次浮点。
                stageEffect = stageEffect.WithPreResistanceDamageMultiplier(
                    damagePercent / 100.0
                );
            }
            stagedEffects.Add(stageEffect);
        }
        return stagedEffects;
    }

    internal static List<CombatEffectDefinition> BuildPreviewEffects(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition repeatAttackEffect,
        int stageCount
    )
    {
        var result = new List<CombatEffectDefinition>();
        if (repeatAttackEffect == null || stageCount <= 0)
        {
            return result;
        }
        List<CombatEffectDefinition> baseEffects = CollectBaseEffects(effectDefinitions);
        RuntimeParameters parameters = RuntimeParameters.FromEffect(repeatAttackEffect);
        int normalizedStageCount = Math.Clamp(stageCount, 1, StageGuard);
        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            result.AddRange(
                BuildStageEffects(
                    baseEffects,
                    parameters.GetStageDamagePercent(stageIndex)
                )
            );
        }
        return result;
    }
}

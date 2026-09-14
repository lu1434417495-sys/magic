using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleRangedWeaponReactionPreviewData
{
    internal BattleRangedWeaponReactionPreviewData(
        int orbCount,
        int durationTu,
        int consumePerTrigger,
        int attackRollBonus,
        StringName attackDefenseMode,
        StringName damageTag,
        IReadOnlyList<StringName> triggerWeaponFamilies,
        bool triggerOnHit,
        bool triggerOnMiss,
        bool allowCritical,
        string summaryText
    )
    {
        OrbCount = Math.Max(orbCount, 0);
        DurationTu = Math.Max(durationTu, 0);
        ConsumePerTrigger = Math.Max(consumePerTrigger, 0);
        AttackRollBonus = attackRollBonus;
        AttackDefenseMode = ProgressionDataUtils.to_string_name(attackDefenseMode);
        DamageTag = ProgressionDataUtils.to_string_name(damageTag);
        TriggerWeaponFamilies = SkillDefinitionCollectionFreeze.List(
            triggerWeaponFamilies
        );
        TriggerOnHit = triggerOnHit;
        TriggerOnMiss = triggerOnMiss;
        AllowCritical = allowCritical;
        SummaryText = summaryText ?? "";
    }

    internal int OrbCount { get; }
    internal int DurationTu { get; }
    internal int ConsumePerTrigger { get; }
    internal int AttackRollBonus { get; }
    internal StringName AttackDefenseMode { get; }
    internal StringName DamageTag { get; }
    internal IReadOnlyList<StringName> TriggerWeaponFamilies { get; }
    internal bool TriggerOnHit { get; }
    internal bool TriggerOnMiss { get; }
    internal bool AllowCritical { get; }
    internal string SummaryText { get; }
}

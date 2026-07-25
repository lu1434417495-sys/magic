using System;
using System.Collections.Generic;
using Godot;

internal enum TemporalStatusReleaseKind
{
    Unknown = 0,
    NaturalExpire,
    Dispel,
    Death,
    Cleanup,
    BattleEnd,
    LeaveBattle,
}

// M2 temporal 状态族规则层：time_stasis / time_slow / time_reverberation。
// 设计来源：docs/discussions/casting_time_and_time_stasis.md（M2 Temporal 状态设计修正）。
// 只做规则计算与 typed 状态读写，不持有 runtime callback，不维护格锁。
internal static class BattleTemporalStatusService
{
    internal const int FullProgressRatePercent = 100;
    internal const int TimeSlowProgressRatePercent = 50;
    internal const int ReverberationDurationTu = 60;
    internal const int ReverberationTemporalSaveBonus = 5;

    internal static readonly StringName TemporalStatusTag =
        BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);

    private static Queue<int> _forcedTemporalProgressRollsForTests;

    internal static void SetForcedTemporalProgressRollsForTests(IEnumerable<int> rolls)
    {
        _forcedTemporalProgressRollsForTests = new Queue<int>();
        foreach (int roll in rolls ?? Array.Empty<int>())
            _forcedTemporalProgressRollsForTests.Enqueue(Math.Clamp(roll, 1, 20));
    }

    internal static void ClearForcedTemporalProgressRollsForTests()
    {
        _forcedTemporalProgressRollsForTests = null;
    }

    internal static bool HasTimeStasis(BattleUnitState unitState)
    {
        return unitState != null
            && unitState.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS);
    }

    internal static bool HasTimeSlow(BattleUnitState unitState)
    {
        return unitState != null
            && unitState.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_SLOW);
    }

    internal static bool HasTemporalCastBlock(BattleUnitState unitState)
    {
        return HasTimeStasis(unitState);
    }

    internal static int GetActionProgressRatePercent(BattleUnitState unitState)
    {
        if (HasTimeStasis(unitState))
        {
            return 0;
        }
        if (HasTimeSlow(unitState))
        {
            return TimeSlowProgressRatePercent;
        }
        return ResolveTemporalProgressModifierRatePercent(unitState, actionProgress: true)
            ?? FullProgressRatePercent;
    }

    internal static int GetCastProgressRatePercent(BattleUnitState unitState)
    {
        if (HasTimeStasis(unitState))
        {
            return 0;
        }
        if (HasTimeSlow(unitState))
        {
            return TimeSlowProgressRatePercent;
        }
        return ResolveTemporalProgressModifierRatePercent(unitState, actionProgress: false)
            ?? FullProgressRatePercent;
    }

    // runtime-only 余数累加：raw = base * rate + remainder；gain = raw / 100；remainder = raw % 100。
    internal static int ConsumeActionProgressGain(BattleUnitState unitState, int tuDelta)
    {
        if (unitState == null || tuDelta <= 0)
        {
            return 0;
        }
        int ratePercent = GetActionProgressRatePercent(unitState);
        if (ratePercent <= 0)
        {
            return 0;
        }
        return unitState.ConsumeActionProgressRateGainTyped(
            tuDelta,
            ratePercent
        );
    }

    private static int? ResolveTemporalProgressModifierRatePercent(
        BattleUnitState unitState,
        bool actionProgress
    )
    {
        BattleTemporalProgressModifierReadView selected =
            unitState?.GetSelectedTemporalProgressModifierTyped(
                actionProgress
            );
        if (selected == null)
            return null;

        int roll = RollTemporalProgressD20();
        int attributeModifier = unitState.attribute_snapshot?.GetValue(
            selected.AttributeModifierId
        ) ?? 0;
        bool success = roll + attributeModifier >= Math.Max(selected.SaveDc, 1);
        int ratePercent = success
            ? selected.SuccessRatePercent
            : selected.FailureRatePercent;
        return Math.Max(ratePercent, 0);
    }

    private static int RollTemporalProgressD20()
    {
        if (
            _forcedTemporalProgressRollsForTests != null
            && _forcedTemporalProgressRollsForTests.Count > 0
        )
        {
            return _forcedTemporalProgressRollsForTests.Dequeue();
        }
        return TrueRandomSeedService.RandiRange(1, 20);
    }

    internal static int ConsumeCastProgressGain(BattleUnitState unitState, int baseProgressDelta)
    {
        if (unitState == null || baseProgressDelta <= 0)
        {
            return 0;
        }
        int ratePercent = GetCastProgressRatePercent(unitState);
        if (ratePercent <= 0)
        {
            return 0;
        }
        int raw =
            baseProgressDelta * ratePercent
            + Math.Max(unitState.cast_progress_rate_remainder, 0);
        unitState.cast_progress_rate_remainder = raw % 100;
        return raw / 100;
    }

    internal static bool IsTemporalStatusId(StringName statusId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(statusId);
        return normalized == BattleStatusSemanticTable.STATUS_TIME_STASIS
            || normalized == BattleStatusSemanticTable.STATUS_TIME_SLOW
            || normalized == BattleStatusSemanticTable.STATUS_TIME_REVERBERATION;
    }

    internal static bool IsTemporalReleaseTargetStatusId(StringName statusId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(statusId);
        return normalized == BattleStatusSemanticTable.STATUS_TIME_STASIS
            || normalized == BattleStatusSemanticTable.STATUS_TIME_SLOW;
    }

    internal static bool IsTemporalReleaseEffect(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition != null
            && effectDefinition.EffectKind == BattleEffectKind.EraseStatus
            && HasEffectTag(effectDefinition, TemporalStatusTag)
            && IsTemporalReleaseTargetStatusId(effectDefinition.StatusId);
    }

    internal static bool IsTemporalReleaseSkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return false;
        }
        foreach (CombatEffectDefinition effectDefinition in combatProfile.EffectDefinitions)
        {
            if (IsTemporalReleaseEffect(effectDefinition))
            {
                return true;
            }
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant?.EffectDefinitions == null)
            {
                continue;
            }
            foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
            {
                if (IsTemporalReleaseEffect(effectDefinition))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal static bool CanTargetTimeStasis(BattleUnitState targetUnit, SkillDefinition skillDefinition)
    {
        if (!HasTimeStasis(targetUnit))
        {
            return true;
        }
        return IsTemporalReleaseSkill(skillDefinition);
    }

    private static bool HasEffectTag(CombatEffectDefinition effectDefinition, StringName expectedTag)
    {
        if (effectDefinition?.EffectTags == null || expectedTag == "")
        {
            return false;
        }
        foreach (StringName tag in effectDefinition.EffectTags)
        {
            if (tag == expectedTag)
            {
                return true;
            }
        }
        return false;
    }

    // elite / boss 不获得 time_stasis；失败结果降级为 time_slow。
    internal static StringName ApplyEliteBossStasisDowngrade(
        BattleUnitState targetUnit,
        StringName resolvedStatusId
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(resolvedStatusId);
        if (normalized != BattleStatusSemanticTable.STATUS_TIME_STASIS)
        {
            return normalized;
        }
        return BattleExecutionRules.IsEliteOrBossTarget(targetUnit)
            ? BattleStatusSemanticTable.STATUS_TIME_SLOW
            : normalized;
    }

    // temporal-only 解控效果：移除目标 temporal 状态；time_stasis 被解除时按 dispel 语义添加余波。
    internal static List<StringName> ApplyTemporalReleaseEffects(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        var removedStatusIds = new List<StringName>();
        if (targetUnit == null || !IsTemporalReleaseEffect(effectDefinition))
        {
            return removedStatusIds;
        }
        StringName releaseStatusId = ProgressionDataUtils.to_string_name(
            effectDefinition.StatusId
        );
        if (!targetUnit.HasStatusEffect(releaseStatusId))
        {
            return removedStatusIds;
        }
        targetUnit.EraseStatusEffect(releaseStatusId);
        removedStatusIds.Add(releaseStatusId);
        HandleTemporalStatusRemoved(
            targetUnit,
            releaseStatusId,
            TemporalStatusReleaseKind.Dispel,
            sourceUnit != null ? sourceUnit.unit_id : new StringName("")
        );
        return removedStatusIds;
    }

    // natural_expire / dispel 添加或刷新 time_reverberation；
    // death / cleanup / battle_end / leave_battle 不添加。
    internal static bool HandleTemporalStatusRemoved(
        BattleUnitState targetUnit,
        StringName removedStatusId,
        TemporalStatusReleaseKind releaseKind,
        StringName sourceUnitId = default
    )
    {
        if (targetUnit == null || !targetUnit.IsAlive())
        {
            return false;
        }
        StringName normalized = ProgressionDataUtils.to_string_name(removedStatusId);
        if (normalized != BattleStatusSemanticTable.STATUS_TIME_STASIS)
        {
            return false;
        }
        if (
            releaseKind != TemporalStatusReleaseKind.NaturalExpire
            && releaseKind != TemporalStatusReleaseKind.Dispel
        )
        {
            return false;
        }
        ApplyTimeReverberation(targetUnit, sourceUnitId);
        return true;
    }

    internal static void ApplyTimeReverberation(
        BattleUnitState targetUnit,
        StringName sourceUnitId = default
    )
    {
        if (targetUnit == null)
        {
            return;
        }
        BattleStatusEffectState existingEntry = targetUnit.GetStatusEffect(
            BattleStatusSemanticTable.STATUS_TIME_REVERBERATION
        );
        BattleStatusEffectState statusEntry = BattleStatusEffectState.CreateOrDuplicate(
            existingEntry
        );
        statusEntry.status_id = BattleStatusSemanticTable.STATUS_TIME_REVERBERATION;
        statusEntry.source_unit_id = ProgressionDataUtils.to_string_name(sourceUnitId);
        statusEntry.power = Math.Max(statusEntry.power, 1);
        statusEntry.stacks = 1;
        statusEntry.stack_behavior = BattleStatusSemanticTable.STACK_REFRESH;
        statusEntry.stack_limit = 1;
        statusEntry.duration = Math.Max(statusEntry.duration, ReverberationDurationTu);
        statusEntry.status_tags = new List<StringName> { TemporalStatusTag };
        statusEntry.save_bonus_by_tag = new Dictionary<StringName, int>
        {
            [TemporalStatusTag] = ReverberationTemporalSaveBonus,
        };
        targetUnit.SetStatusEffect(statusEntry);
    }
}

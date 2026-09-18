using System.Collections.Generic;
using Godot;

/// <summary>
/// 运行时推进状态时钟时需要改动战场状态的步骤：目标标记时钟与到期反应、体型恢复时的占格校验。
/// 不传 hooks 即为纯推演：只推进单位自身的护盾与状态时钟，不触碰 BattleState、网格或 batch。
/// </summary>
internal interface IBattleStatusDurationRuntimeHooks
{
    bool AdvanceTargetMarkDurations(BattleUnitState unit, int elapsedTu, BattleEventBatch batch);

    bool TryRestoreBodySizeCategory(
        BattleUnitState unit,
        StringName previousCategory,
        BattleEventBatch batch
    );

    void ResolveTargetMarkExpired(
        BattleUnitState unit,
        BattleStatusEffectState expiredStatus,
        BattleEventBatch batch
    );
}

internal static class BattleStatusDurationRules
{
    internal static bool AdvanceUnitProjection(BattleUnitState unit, int elapsedTu) =>
        Advance(unit, elapsedTu, null, null);

    internal static bool Advance(
        BattleUnitState unit,
        int elapsedTu,
        BattleEventBatch batch,
        IBattleStatusDurationRuntimeHooks hooks
    )
    {
        if (unit == null)
        {
            return false;
        }
        // Advance state that existed for the whole interval before expiry
        // reactions can apply a fresh shield at the interval boundary.
        bool changed = unit.AdvanceShieldDurationTyped(elapsedTu);
        changed |= hooks?.AdvanceTargetMarkDurations(unit, elapsedTu, batch) == true;
        var expiredStatusIds = new List<StringName>();
        var expiredStatusEntries = new Dictionary<StringName, BattleStatusEffectState>();
        foreach (BattleStatusEffectState statusEntry in unit.GetStatusEffectsTyped())
        {
            BattleStatusDurationAdvanceResult durationResult =
                BattleStatusSemanticTable.AdvanceTimelineDurationResult(statusEntry, elapsedTu);
            if (durationResult.Expired)
            {
                expiredStatusIds.Add(statusEntry.status_id);
                expiredStatusEntries[statusEntry.status_id] = statusEntry;
                changed = true;
                continue;
            }
            if (durationResult.Changed)
            {
                unit.SetStatusEffect(statusEntry);
                changed = true;
            }
        }
        foreach (StringName expiredStatusId in expiredStatusIds)
        {
            expiredStatusEntries.TryGetValue(
                expiredStatusId,
                out BattleStatusEffectState expiredStatusEntry
            );
            bool shouldEraseStatus = true;
            if (IsBodySizeCategoryOverrideStatus(expiredStatusEntry))
            {
                shouldEraseStatus = false;
                if (RestoreBodySizeCategoryOverrideIfNeeded(unit, expiredStatusEntry, batch, hooks))
                {
                    changed = true;
                    shouldEraseStatus = true;
                }
                else if (BodySizeAlreadyMatchesPrevious(unit, expiredStatusEntry))
                {
                    shouldEraseStatus = true;
                }
            }
            if (shouldEraseStatus)
            {
                hooks?.ResolveTargetMarkExpired(unit, expiredStatusEntry, batch);
                unit.EraseStatusEffect(expiredStatusId);
            }
        }
        return changed;
    }

    private static bool IsBodySizeCategoryOverrideStatus(BattleStatusEffectState statusEntry) =>
        statusEntry != null
        && BodySizeContentRules.IsValidBodySizeCategory(statusEntry.body_size_category_override);

    private static bool BodySizeAlreadyMatchesPrevious(
        BattleUnitState unit,
        BattleStatusEffectState statusEntry
    )
    {
        if (unit == null || statusEntry == null)
        {
            return false;
        }
        if (!BodySizeContentRules.IsValidBodySizeCategory(statusEntry.body_size_category_override))
        {
            return false;
        }
        StringName previousCategory = statusEntry.previous_body_size_category;
        return BodySizeContentRules.IsValidBodySizeCategory(previousCategory)
            && unit.GetBodySizeCategory() == previousCategory;
    }

    private static bool RestoreBodySizeCategoryOverrideIfNeeded(
        BattleUnitState unit,
        BattleStatusEffectState statusEntry,
        BattleEventBatch batch,
        IBattleStatusDurationRuntimeHooks hooks
    )
    {
        if (unit == null || statusEntry == null)
        {
            return false;
        }
        if (!BodySizeContentRules.IsValidBodySizeCategory(statusEntry.body_size_category_override))
        {
            return false;
        }
        StringName previousCategory = statusEntry.previous_body_size_category;
        if (!BodySizeContentRules.IsValidBodySizeCategory(previousCategory))
        {
            return false;
        }
        if (unit.GetBodySizeCategory() == previousCategory)
        {
            return false;
        }
        if (hooks != null)
        {
            return hooks.TryRestoreBodySizeCategory(unit, previousCategory, batch);
        }
        unit.SetBodySizeCategory(previousCategory);
        return true;
    }
}

using Godot;

internal static class TemporalStatusContentRules
{
    internal static readonly StringName TimeStasisStatusId = "time_stasis";
    internal static readonly StringName TimeSlowStatusId = "time_slow";
    internal static readonly StringName TimeReverberationStatusId = "time_reverberation";
    internal static readonly StringName TemporalStatusTag =
        BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);

    internal static bool IsTemporalStatusId(StringName statusId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(statusId);
        return normalized == TimeStasisStatusId
            || normalized == TimeSlowStatusId
            || normalized == TimeReverberationStatusId;
    }

    internal static bool IsTemporalReleaseTargetStatusId(StringName statusId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(statusId);
        return normalized == TimeStasisStatusId || normalized == TimeSlowStatusId;
    }

    internal static bool IsTemporalReleaseEffect(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition != null
            && effectDefinition.EffectKind == BattleEffectKind.EraseStatus
            && HasEffectTag(effectDefinition, TemporalStatusTag)
            && IsTemporalReleaseTargetStatusId(effectDefinition.StatusId);
    }

    private static bool HasEffectTag(
        CombatEffectDefinition effectDefinition,
        StringName expectedTag
    )
    {
        if (effectDefinition?.EffectTags == null || expectedTag == "")
            return false;
        foreach (StringName tag in effectDefinition.EffectTags)
        {
            if (tag == expectedTag)
                return true;
        }
        return false;
    }
}

using System.Collections.Generic;
using Godot;

internal enum LowLuckRelicItemKind
{
    Unknown = 0,
    ReverseFateAmulet,
    BlackStarWedge,
    BloodDebtShawl,
    DeadRoadLantern,
}

internal enum LowLuckRelicAttributeKind
{
    Unknown = 0,
    ReverseFateAmulet,
    BlackStarWedge,
    BloodDebtShawl,
    DeadRoadLantern,
}

internal enum LowLuckRelicStatusKind
{
    Unknown = 0,
    ReverseFateWeakened,
    BlackStarWedgeExposed,
}

internal enum LowLuckPathTagKind
{
    Unknown = 0,
    HiddenTrap,
    BlackMarket,
    BlackOmen,
    HiddenPath,
}

internal static class LowLuckRelicRules
{
    private static readonly StringName ItemReverseFateAmulet = "reverse_fate_amulet";
    private static readonly StringName ItemBlackStarWedge = "black_star_wedge";
    private static readonly StringName ItemBloodDebtShawl = "blood_debt_shawl";
    private static readonly StringName ItemDeadRoadLantern = "dead_road_lantern";

    private static readonly StringName AttrReverseFateAmulet = "low_luck_reverse_fate_amulet";
    private static readonly StringName AttrBlackStarWedge = "low_luck_black_star_wedge";
    private static readonly StringName AttrBloodDebtShawl = "low_luck_blood_debt_shawl";
    private static readonly StringName AttrDeadRoadLantern = "low_luck_dead_road_lantern";

    private static readonly StringName StatusReverseFateWeakened = "low_luck_reverse_fate_weakened";
    private static readonly StringName StatusBlackStarWedgeExposed =
        "low_luck_black_star_wedge_exposed";

    internal const string BattleFlagReverseFateUsed = "low_luck_reverse_fate_used";
    internal const string BattleFlagBlackStarWedgeUsed = "low_luck_black_star_wedge_used";

    internal const int ReverseFateDurationTu = 120;
    internal const double ReverseFateDamageMultiplier = 0.75;
    internal const int BlackStarWedgeGuardIgnoreFlat = 4;
    internal const int BlackStarWedgeExposedDurationTu = 60;
    internal const double BlackStarWedgeExposedIncomingDamageMultiplier = 1.25;
    internal const double BloodDebtLowHpThresholdRatio = 0.5;
    internal const double BloodDebtDamageMultiplier = 0.75;
    internal const double BloodDebtRecoveryMultiplier = 0.5;
    internal const int BloodDebtAllyDownApGain = 1;

    private static readonly StringName PathTagHiddenTrap = "hidden_trap";
    private static readonly StringName PathTagBlackMarket = "black_market";
    private static readonly StringName PathTagBlackOmen = "black_omen";
    private static readonly StringName PathTagHiddenPath = "hidden_path";

    internal static StringName ToStringName(LowLuckRelicItemKind kind) =>
        kind switch
        {
            LowLuckRelicItemKind.ReverseFateAmulet => ItemReverseFateAmulet,
            LowLuckRelicItemKind.BlackStarWedge => ItemBlackStarWedge,
            LowLuckRelicItemKind.BloodDebtShawl => ItemBloodDebtShawl,
            LowLuckRelicItemKind.DeadRoadLantern => ItemDeadRoadLantern,
            _ => "",
        };

    internal static StringName ToStringName(LowLuckRelicAttributeKind kind) =>
        kind switch
        {
            LowLuckRelicAttributeKind.ReverseFateAmulet => AttrReverseFateAmulet,
            LowLuckRelicAttributeKind.BlackStarWedge => AttrBlackStarWedge,
            LowLuckRelicAttributeKind.BloodDebtShawl => AttrBloodDebtShawl,
            LowLuckRelicAttributeKind.DeadRoadLantern => AttrDeadRoadLantern,
            _ => "",
        };

    internal static StringName ToStringName(LowLuckRelicStatusKind kind) =>
        kind switch
        {
            LowLuckRelicStatusKind.ReverseFateWeakened => StatusReverseFateWeakened,
            LowLuckRelicStatusKind.BlackStarWedgeExposed => StatusBlackStarWedgeExposed,
            _ => "",
        };

    internal static StringName ToStringName(LowLuckPathTagKind kind) =>
        kind switch
        {
            LowLuckPathTagKind.HiddenTrap => PathTagHiddenTrap,
            LowLuckPathTagKind.BlackMarket => PathTagBlackMarket,
            LowLuckPathTagKind.BlackOmen => PathTagBlackOmen,
            LowLuckPathTagKind.HiddenPath => PathTagHiddenPath,
            _ => "",
        };

    internal static LowLuckRelicItemKind ToItemKind(StringName itemId)
    {
        if (itemId == ItemReverseFateAmulet)
            return LowLuckRelicItemKind.ReverseFateAmulet;
        if (itemId == ItemBlackStarWedge)
            return LowLuckRelicItemKind.BlackStarWedge;
        if (itemId == ItemBloodDebtShawl)
            return LowLuckRelicItemKind.BloodDebtShawl;
        if (itemId == ItemDeadRoadLantern)
            return LowLuckRelicItemKind.DeadRoadLantern;
        return LowLuckRelicItemKind.Unknown;
    }

    internal static LowLuckRelicAttributeKind ToAttributeKind(StringName attributeId)
    {
        if (attributeId == AttrReverseFateAmulet)
            return LowLuckRelicAttributeKind.ReverseFateAmulet;
        if (attributeId == AttrBlackStarWedge)
            return LowLuckRelicAttributeKind.BlackStarWedge;
        if (attributeId == AttrBloodDebtShawl)
            return LowLuckRelicAttributeKind.BloodDebtShawl;
        if (attributeId == AttrDeadRoadLantern)
            return LowLuckRelicAttributeKind.DeadRoadLantern;
        return LowLuckRelicAttributeKind.Unknown;
    }

    internal static LowLuckRelicStatusKind ToStatusKind(StringName statusId)
    {
        if (statusId == StatusReverseFateWeakened)
            return LowLuckRelicStatusKind.ReverseFateWeakened;
        if (statusId == StatusBlackStarWedgeExposed)
            return LowLuckRelicStatusKind.BlackStarWedgeExposed;
        return LowLuckRelicStatusKind.Unknown;
    }

    internal static bool SnapshotHasFlag(AttributeSnapshot attributeSnapshot, StringName attributeId)
    {
        return attributeSnapshot != null && attributeId != "" && attributeSnapshot.GetValue(attributeId) > 0;
    }

    internal static bool UnitHasFlag(BattleUnitState unitState, StringName attributeId)
    {
        return unitState != null && SnapshotHasFlag(unitState.attribute_snapshot, attributeId);
    }

    internal static IReadOnlyList<StringName> NormalizePathTags(IEnumerable<StringName> pathTags)
    {
        var normalizedTags = new List<StringName>();
        if (pathTags == null)
            return normalizedTags;

        var seenTags = new HashSet<StringName>();
        foreach (StringName pathTag in pathTags)
        {
            if (pathTag == "" || !seenTags.Add(pathTag))
                continue;
            normalizedTags.Add(pathTag);
        }
        return normalizedTags;
    }

    internal static bool ShouldRevealHiddenPath(
        AttributeSnapshot attributeSnapshot,
        IEnumerable<StringName> pathTags
    )
    {
        if (
            !SnapshotHasFlag(
                attributeSnapshot,
                ToStringName(LowLuckRelicAttributeKind.DeadRoadLantern)
            )
        )
            return false;

        foreach (StringName pathTag in NormalizePathTags(pathTags))
        {
            if (ToPathTagKind(pathTag) != LowLuckPathTagKind.Unknown)
                return true;
        }
        return false;
    }

    internal static LowLuckPathTagKind ToPathTagKind(StringName pathTag)
    {
        if (pathTag == PathTagHiddenTrap)
            return LowLuckPathTagKind.HiddenTrap;
        if (pathTag == PathTagBlackMarket)
            return LowLuckPathTagKind.BlackMarket;
        if (pathTag == PathTagBlackOmen)
            return LowLuckPathTagKind.BlackOmen;
        if (pathTag == PathTagHiddenPath)
            return LowLuckPathTagKind.HiddenPath;
        return LowLuckPathTagKind.Unknown;
    }

    internal static bool MemberHasItem(PartyMemberState memberState, StringName itemId)
    {
        if (memberState == null || itemId == "" || memberState.equipment_state == null)
            return false;

        foreach (StringName slotId in memberState.equipment_state.GetEntrySlotIdsTyped())
        {
            if (memberState.equipment_state.GetEquippedItemId(slotId) == itemId)
                return true;
        }
        return false;
    }
}

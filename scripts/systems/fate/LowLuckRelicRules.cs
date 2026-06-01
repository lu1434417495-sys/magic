using System.Collections.Generic;
using Godot;

public static class LowLuckRelicRules
{
    public static readonly StringName ITEM_REVERSE_FATE_AMULET = "reverse_fate_amulet";
    public static readonly StringName ITEM_BLACK_STAR_WEDGE = "black_star_wedge";
    public static readonly StringName ITEM_BLOOD_DEBT_SHAWL = "blood_debt_shawl";
    public static readonly StringName ITEM_DEAD_ROAD_LANTERN = "dead_road_lantern";

    public static readonly StringName ATTR_REVERSE_FATE_AMULET = "low_luck_reverse_fate_amulet";
    public static readonly StringName ATTR_BLACK_STAR_WEDGE = "low_luck_black_star_wedge";
    public static readonly StringName ATTR_BLOOD_DEBT_SHAWL = "low_luck_blood_debt_shawl";
    public static readonly StringName ATTR_DEAD_ROAD_LANTERN = "low_luck_dead_road_lantern";

    public static readonly StringName STATUS_REVERSE_FATE_WEAKENED =
        "low_luck_reverse_fate_weakened";
    public static readonly StringName STATUS_BLACK_STAR_WEDGE_EXPOSED =
        "low_luck_black_star_wedge_exposed";

    public const string BATTLE_FLAG_REVERSE_FATE_USED = "low_luck_reverse_fate_used";
    public const string BATTLE_FLAG_BLACK_STAR_WEDGE_USED = "low_luck_black_star_wedge_used";

    public const int REVERSE_FATE_DURATION_TU = 120;
    public const double REVERSE_FATE_DAMAGE_MULTIPLIER = 0.75;
    public const int BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT = 4;
    public const int BLACK_STAR_WEDGE_EXPOSED_DURATION_TU = 60;
    public const double BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER = 1.25;
    public const double BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO = 0.5;
    public const double BLOOD_DEBT_DAMAGE_MULTIPLIER = 0.75;
    public const double BLOOD_DEBT_RECOVERY_MULTIPLIER = 0.5;
    public const int BLOOD_DEBT_ALLY_DOWN_AP_GAIN = 1;

    public static readonly StringName PATH_TAG_HIDDEN_TRAP = "hidden_trap";
    public static readonly StringName PATH_TAG_BLACK_MARKET = "black_market";
    public static readonly StringName PATH_TAG_BLACK_OMEN = "black_omen";
    public static readonly StringName PATH_TAG_HIDDEN_PATH = "hidden_path";

    public static readonly IReadOnlySet<StringName> VisiblePathTags = new HashSet<StringName>
    {
        PATH_TAG_HIDDEN_TRAP,
        PATH_TAG_BLACK_MARKET,
        PATH_TAG_BLACK_OMEN,
        PATH_TAG_HIDDEN_PATH,
    };

    public static bool SnapshotHasFlag(AttributeSnapshot attributeSnapshot, StringName attributeId)
    {
        return attributeSnapshot != null && attributeId != "" && attributeSnapshot.get_value(attributeId) > 0;
    }

    public static bool UnitHasFlag(BattleUnitState unitState, StringName attributeId)
    {
        return unitState != null && SnapshotHasFlag(unitState.attribute_snapshot, attributeId);
    }

    public static IReadOnlyList<StringName> NormalizePathTags(IEnumerable<StringName> pathTags)
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

    public static bool ShouldRevealHiddenPath(
        AttributeSnapshot attributeSnapshot,
        IEnumerable<StringName> pathTags
    )
    {
        if (!SnapshotHasFlag(attributeSnapshot, ATTR_DEAD_ROAD_LANTERN))
            return false;

        foreach (StringName pathTag in NormalizePathTags(pathTags))
        {
            if (VisiblePathTags.Contains(pathTag))
                return true;
        }
        return false;
    }

    public static bool MemberHasItem(PartyMemberState memberState, StringName itemId)
    {
        if (memberState == null || itemId == "" || memberState.equipment_state == null)
            return false;

        foreach (StringName slotId in memberState.equipment_state.get_entry_slot_ids())
        {
            if (memberState.equipment_state.get_equipped_item_id(slotId) == itemId)
                return true;
        }
        return false;
    }
}

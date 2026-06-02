using System.Collections.Generic;
using Godot;

public static class EquipmentRules
{
    private const string MainHandId = "main_hand";
    private const string OffHandId = "off_hand";
    private const string HeadId = "head";
    private const string BodyId = "body";
    private const string HandsId = "hands";
    private const string FeetId = "feet";
    private const string CloakId = "cloak";
    private const string NecklaceId = "necklace";
    private const string Ring1Id = "ring_1";
    private const string Ring2Id = "ring_2";
    private const string SpecialTrinketId = "special_trinket";
    private const string BadgeId = "badge";
    private static readonly string[] SlotOrder =
    {
        MainHandId,
        OffHandId,
        HeadId,
        BodyId,
        HandsId,
        FeetId,
        CloakId,
        NecklaceId,
        Ring1Id,
        Ring2Id,
        SpecialTrinketId,
        BadgeId,
    };
    private static readonly HashSet<string> ValidSlotIds =
        new()
        {
            "main_hand",
            "off_hand",
            "head",
            "body",
            "hands",
            "feet",
            "cloak",
            "necklace",
            "ring_1",
            "ring_2",
            "special_trinket",
            "badge",
        };
    private static readonly Dictionary<string, string> SlotLabels =
        new()
        {
            { "main_hand", "主手" },
            { "off_hand", "副手" },
            { "head", "头部" },
            { "body", "身躯" },
            { "hands", "手部" },
            { "feet", "脚部" },
            { "cloak", "披风" },
            { "necklace", "项链" },
            { "ring_1", "戒指一" },
            { "ring_2", "戒指二" },
            { "special_trinket", "特殊饰品" },
            { "badge", "徽章" },
        };

    public static StringName MAIN_HAND() => MainHandId;

    public static StringName OFF_HAND() => OffHandId;

    public static StringName HEAD() => HeadId;

    public static StringName BODY() => BodyId;

    public static StringName HANDS() => HandsId;

    public static StringName FEET() => FeetId;

    public static StringName CLOAK() => CloakId;

    public static StringName NECKLACE() => NecklaceId;

    public static StringName RING_1() => Ring1Id;

    public static StringName RING_2() => Ring2Id;

    public static StringName SPECIAL_TRINKET() => SpecialTrinketId;

    public static StringName BADGE() => BadgeId;

    public static Godot.Collections.Array<StringName> SLOT_ORDER()
    {
        return new Godot.Collections.Array<StringName>(GetAllSlotIdsTyped());
    }

    public static Godot.Collections.Array<StringName> get_all_slot_ids() => SLOT_ORDER();

    public static IReadOnlyList<StringName> GetAllSlotIdsTyped()
    {
        var result = new List<StringName>(SlotOrder.Length);
        foreach (string slotId in SlotOrder)
            result.Add(new StringName(slotId));
        return result;
    }

    public static bool is_valid_slot(StringName slot_id)
    {
        return ValidSlotIds.Contains(slot_id.ToString());
    }

    public static Godot.Collections.Array<StringName> normalize_slot_ids(
        Godot.Collections.Array<StringName> values
    )
    {
        return new Godot.Collections.Array<StringName>(NormalizeSlotIdsTyped(values));
    }

    public static Godot.Collections.Array<StringName> normalize_slot_ids(
        Godot.Collections.Array<string> values
    )
    {
        return new Godot.Collections.Array<StringName>(NormalizeSlotIdsTyped(values));
    }

    public static IReadOnlyList<StringName> NormalizeSlotIdsTyped(
        IEnumerable<StringName> values
    )
    {
        var normalized = new List<StringName>();
        var seen = new HashSet<string>();
        if (values == null)
            return normalized;
        foreach (StringName value in values)
            AddNormalizedSlotId(normalized, seen, value);
        return normalized;
    }

    public static IReadOnlyList<StringName> NormalizeSlotIdsTyped(
        IEnumerable<string> values
    )
    {
        var normalized = new List<StringName>();
        var seen = new HashSet<string>();
        if (values == null)
            return normalized;
        foreach (string value in values)
        {
            if (string.IsNullOrEmpty(value))
                continue;
            AddNormalizedSlotId(normalized, seen, new StringName(value));
        }
        return normalized;
    }

    private static void AddNormalizedSlotId(
        List<StringName> normalized,
        HashSet<string> seen,
        StringName raw
    )
    {
        string rawText = raw.ToString();
        if (!is_valid_slot(raw) || !seen.Add(rawText))
            return;
        normalized.Add(new StringName(rawText));
    }

    public static string get_slot_label(StringName slot_id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(slot_id);
        if (SlotLabels.TryGetValue(normalized.ToString(), out string label))
            return label;
        return slot_id.ToString();
    }
}

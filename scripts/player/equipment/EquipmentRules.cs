using Godot;

[GlobalClass]
public partial class EquipmentRules : RefCounted
{
    private static readonly StringName MainHandId = "main_hand";
    private static readonly StringName OffHandId = "off_hand";
    private static readonly StringName HeadId = "head";
    private static readonly StringName BodyId = "body";
    private static readonly StringName HandsId = "hands";
    private static readonly StringName FeetId = "feet";
    private static readonly StringName CloakId = "cloak";
    private static readonly StringName NecklaceId = "necklace";
    private static readonly StringName Ring1Id = "ring_1";
    private static readonly StringName Ring2Id = "ring_2";
    private static readonly StringName SpecialTrinketId = "special_trinket";
    private static readonly StringName BadgeId = "badge";

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
        return new Godot.Collections.Array<StringName>
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
    }

    public static Godot.Collections.Array<StringName> get_all_slot_ids() => new(SLOT_ORDER());

    public static bool is_valid_slot(StringName slot_id)
    {
        return SLOT_ORDER().Contains(slot_id);
    }

    public static Godot.Collections.Array<StringName> normalize_slot_ids(
        Godot.Collections.Array<StringName> values
    )
    {
        var normalized = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
            AddNormalizedSlotId(normalized, seen, value);
        return normalized;
    }

    public static Godot.Collections.Array<StringName> normalize_slot_ids(
        Godot.Collections.Array<string> values
    )
    {
        var normalized = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (string value in values ?? new Godot.Collections.Array<string>())
            AddNormalizedSlotId(normalized, seen, new StringName(value));
        return normalized;
    }

    private static void AddNormalizedSlotId(
        Godot.Collections.Array<StringName> normalized,
        Godot.Collections.Dictionary seen,
        StringName raw
    )
    {
        if (!is_valid_slot(raw) || seen.ContainsKey(raw))
            return;
        seen[raw] = true;
        normalized.Add(raw);
    }

    public static string get_slot_label(StringName slot_id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(slot_id);
        if (normalized == MainHandId)
            return "主手";
        if (normalized == OffHandId)
            return "副手";
        if (normalized == HeadId)
            return "头部";
        if (normalized == BodyId)
            return "身躯";
        if (normalized == HandsId)
            return "手部";
        if (normalized == FeetId)
            return "脚部";
        if (normalized == CloakId)
            return "披风";
        if (normalized == NecklaceId)
            return "项链";
        if (normalized == Ring1Id)
            return "戒指一";
        if (normalized == Ring2Id)
            return "戒指二";
        if (normalized == SpecialTrinketId)
            return "特殊饰品";
        if (normalized == BadgeId)
            return "徽章";
        return slot_id.ToString();
    }
}

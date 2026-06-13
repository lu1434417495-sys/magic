using System.Collections.Generic;
using Godot;

internal enum EquipmentSlotKind
{
    Unknown = 0,
    MainHand,
    OffHand,
    Head,
    Body,
    Hands,
    Feet,
    Cloak,
    Necklace,
    Ring1,
    Ring2,
    SpecialTrinket,
    Badge,
}

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
    private static readonly EquipmentSlotKind[] SlotOrder =
    {
        EquipmentSlotKind.MainHand,
        EquipmentSlotKind.OffHand,
        EquipmentSlotKind.Head,
        EquipmentSlotKind.Body,
        EquipmentSlotKind.Hands,
        EquipmentSlotKind.Feet,
        EquipmentSlotKind.Cloak,
        EquipmentSlotKind.Necklace,
        EquipmentSlotKind.Ring1,
        EquipmentSlotKind.Ring2,
        EquipmentSlotKind.SpecialTrinket,
        EquipmentSlotKind.Badge,
    };

    public static IReadOnlyList<StringName> GetAllSlotIdsTyped()
    {
        var result = new List<StringName>(SlotOrder.Length);
        foreach (EquipmentSlotKind slotKind in SlotOrder)
            result.Add(ToStringName(slotKind));
        return result;
    }

    public static bool IsValidSlot(StringName slot_id)
    {
        return ToSlotKind(slot_id) != EquipmentSlotKind.Unknown;
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
        EquipmentSlotKind slotKind = ToSlotKind(raw);
        StringName normalizedSlotId = ToStringName(slotKind);
        string normalizedText = normalizedSlotId.ToString();
        if (slotKind == EquipmentSlotKind.Unknown || !seen.Add(normalizedText))
            return;
        normalized.Add(normalizedSlotId);
    }

    public static string GetSlotLabel(StringName slot_id)
    {
        return ToSlotKind(ProgressionDataUtils.to_string_name(slot_id)) switch
        {
            EquipmentSlotKind.MainHand => "主手",
            EquipmentSlotKind.OffHand => "副手",
            EquipmentSlotKind.Head => "头部",
            EquipmentSlotKind.Body => "身躯",
            EquipmentSlotKind.Hands => "手部",
            EquipmentSlotKind.Feet => "脚部",
            EquipmentSlotKind.Cloak => "披风",
            EquipmentSlotKind.Necklace => "项链",
            EquipmentSlotKind.Ring1 => "戒指一",
            EquipmentSlotKind.Ring2 => "戒指二",
            EquipmentSlotKind.SpecialTrinket => "特殊饰品",
            EquipmentSlotKind.Badge => "徽章",
            _ => slot_id.ToString(),
        };
    }

    internal static EquipmentSlotKind ToSlotKind(StringName value)
    {
        return value.ToString() switch
        {
            MainHandId => EquipmentSlotKind.MainHand,
            OffHandId => EquipmentSlotKind.OffHand,
            HeadId => EquipmentSlotKind.Head,
            BodyId => EquipmentSlotKind.Body,
            HandsId => EquipmentSlotKind.Hands,
            FeetId => EquipmentSlotKind.Feet,
            CloakId => EquipmentSlotKind.Cloak,
            NecklaceId => EquipmentSlotKind.Necklace,
            Ring1Id => EquipmentSlotKind.Ring1,
            Ring2Id => EquipmentSlotKind.Ring2,
            SpecialTrinketId => EquipmentSlotKind.SpecialTrinket,
            BadgeId => EquipmentSlotKind.Badge,
            _ => EquipmentSlotKind.Unknown,
        };
    }

    internal static StringName ToStringName(EquipmentSlotKind kind)
    {
        return kind switch
        {
            EquipmentSlotKind.MainHand => MainHandId,
            EquipmentSlotKind.OffHand => OffHandId,
            EquipmentSlotKind.Head => HeadId,
            EquipmentSlotKind.Body => BodyId,
            EquipmentSlotKind.Hands => HandsId,
            EquipmentSlotKind.Feet => FeetId,
            EquipmentSlotKind.Cloak => CloakId,
            EquipmentSlotKind.Necklace => NecklaceId,
            EquipmentSlotKind.Ring1 => Ring1Id,
            EquipmentSlotKind.Ring2 => Ring2Id,
            EquipmentSlotKind.SpecialTrinket => SpecialTrinketId,
            EquipmentSlotKind.Badge => BadgeId,
            _ => "",
        };
    }
}

using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

public static class PartyMemberOptionUtils
{
    public static PartyState GetPartyState(GDictionary window_data)
    {
        if (window_data == null || !window_data.ContainsKey("party_state"))
            return null;
        return window_data["party_state"].AsGodotObject() as PartyState;
    }

    public static GDictionaryArray BuildMemberOptions(GDictionary window_data)
    {
        if (window_data != null && window_data.ContainsKey("member_options"))
            return _build_explicit_member_options(DictArray(window_data, "member_options"));

        PartyState partyState = GetPartyState(window_data);
        if (partyState == null)
            return new GDictionaryArray();

        var optionsFromParty = new GDictionaryArray();
        var seenIds = new GDictionary();
        foreach (StringName memberId in partyState.active_member_ids)
            _append_member_option(
                optionsFromParty,
                seenIds,
                partyState,
                ProgressionDataUtils.to_string_name(memberId),
                "上阵"
            );
        foreach (StringName memberId in partyState.reserve_member_ids)
            _append_member_option(
                optionsFromParty,
                seenIds,
                partyState,
                ProgressionDataUtils.to_string_name(memberId),
                "替补"
            );
        return optionsFromParty;
    }

    public static GDictionary BuildMemberVariantMap(GDictionaryArray options)
    {
        var memberMap = new GDictionary();
        if (options == null)
            return memberMap;

        foreach (GDictionary option in options)
        {
            if (string.IsNullOrEmpty(GetMemberVariantDisplayName(option)))
                continue;
            StringName memberId = DictStringName(option, "member_id");
            if (memberId != "")
                memberMap[memberId] = option;
        }
        return memberMap;
    }

    public static string BuildMemberVariantLabel(GDictionary member_option)
    {
        string displayName = GetMemberVariantDisplayName(member_option);
        if (string.IsNullOrEmpty(displayName))
            return "";

        string rosterRole = DictString(member_option, "roster_role", "");
        bool isLeader = DictBool(member_option, "is_leader", false);
        int currentHp = DictInt(member_option, "current_hp", 0);
        int currentMp = DictInt(member_option, "current_mp", 0);
        string prefix = isLeader ? "队长 · " : "";
        string roleSuffix = !string.IsNullOrEmpty(rosterRole) ? $" · {rosterRole}" : "";
        return $"{prefix}{displayName}{roleSuffix}  |  HP {currentHp}  MP {currentMp}";
    }

    public static StringName ResolveDefaultMemberId(
        GDictionary window_data,
        GDictionary member_variant_map,
        GDictionaryArray member_options
    )
    {
        StringName explicitDefault = DictStringName(window_data, "default_member_id");
        if (explicitDefault != "" && DictHas(member_variant_map, explicitDefault))
            return explicitDefault;

        StringName selectedMemberId = DictStringName(window_data, "selected_member_id");
        if (selectedMemberId != "" && DictHas(member_variant_map, selectedMemberId))
            return selectedMemberId;

        PartyState partyState = GetPartyState(window_data);
        if (partyState != null)
        {
            if (
                partyState.leader_member_id != ""
                && DictHas(member_variant_map, partyState.leader_member_id)
            )
                return partyState.leader_member_id;

            foreach (StringName rawMemberId in partyState.active_member_ids)
            {
                StringName memberId = ProgressionDataUtils.to_string_name(rawMemberId);
                if (memberId != "" && DictHas(member_variant_map, memberId))
                    return memberId;
            }

            foreach (StringName rawMemberId in partyState.reserve_member_ids)
            {
                StringName memberId = ProgressionDataUtils.to_string_name(rawMemberId);
                if (memberId != "" && DictHas(member_variant_map, memberId))
                    return memberId;
            }
        }

        if (member_options != null)
        {
            foreach (GDictionary memberOption in member_options)
            {
                StringName memberId = DictStringName(memberOption, "member_id");
                if (memberId != "")
                    return memberId;
            }
        }
        return "";
    }

    public static string GetMemberVariantDisplayName(GDictionary member_option)
    {
        if (!TryRead(member_option, "display_name", out Variant value))
            return "";
        string displayName = value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => "",
        };
        return displayName.StripEdges();
    }

    private static void _append_member_option(
        GDictionaryArray options,
        GDictionary seenIds,
        PartyState partyState,
        StringName memberId,
        string defaultRole
    )
    {
        if (memberId == "" || seenIds.ContainsKey(memberId))
            return;

        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null)
            return;

        string displayName = memberState.display_name.StripEdges();
        if (string.IsNullOrEmpty(displayName))
            return;

        seenIds[memberId] = true;
        options.Add(
            new GDictionary
            {
                ["member_id"] = memberId.ToString(),
                ["display_name"] = displayName,
                ["roster_role"] = defaultRole,
                ["is_leader"] = partyState.leader_member_id == memberId,
                ["current_hp"] = memberState.current_hp,
                ["current_mp"] = memberState.current_mp,
            }
        );
    }

    private static GDictionaryArray _build_explicit_member_options(GArray value)
    {
        var options = new GDictionaryArray();
        foreach (GDictionary optionData in ReadDictionaryItems(value))
        {
            var option = (GDictionary)optionData.Duplicate(true);
            string displayName = GetMemberVariantDisplayName(option);
            if (string.IsNullOrEmpty(displayName))
                continue;

            option["display_name"] = displayName;
            options.Add(option);
        }
        return options;
    }

    private static bool DictHas(GDictionary dict, StringName key)
    {
        return dict != null && dict.ContainsKey(key);
    }

    private static GArray DictArray(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Array)
            return new GArray();
        return value.AsGodotArray();
    }

    private static StringName DictStringName(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryRead(dict, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => defaultValue,
        };
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            return defaultValue;
        return value.AsBool();
    }

    private static int DictInt(GDictionary dict, string key, int defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Int)
            return defaultValue;
        return value.AsInt32();
    }

    private static GDictionaryArray ReadDictionaryItems(GArray items)
    {
        var result = new GDictionaryArray();
        if (items == null)
            return result;
        foreach (Variant item in items)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                return new GDictionaryArray();
            result.Add(item.AsGodotDictionary());
        }
        return result;
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        value = default;
        if (dict == null || !dict.ContainsKey(key))
            return false;
        value = dict[key];
        return value.VariantType != Variant.Type.Nil;
    }
}

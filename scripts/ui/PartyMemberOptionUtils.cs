using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class PartyMemberOptionUtils : RefCounted
{
    public static PartyState get_party_state(GDictionary window_data)
    {
        if (window_data == null || !window_data.ContainsKey("party_state"))
            return null;
        return window_data["party_state"].AsGodotObject() as PartyState;
    }

    public static GDictionaryArray build_member_options(GDictionary window_data)
    {
        if (window_data != null && window_data.ContainsKey("member_options"))
            return _build_explicit_member_options(DictArray(window_data, "member_options"));

        PartyState partyState = get_party_state(window_data);
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

    public static GDictionary build_member_variant_map(GDictionaryArray options)
    {
        var memberMap = new GDictionary();
        if (options == null)
            return memberMap;

        foreach (GDictionary option in options)
        {
            if (string.IsNullOrEmpty(get_member_variant_display_name(option)))
                continue;
            StringName memberId = DictStringName(option, "member_id");
            if (memberId != "")
                memberMap[memberId] = option;
        }
        return memberMap;
    }

    public static string build_member_variant_label(GDictionary member_option)
    {
        string displayName = get_member_variant_display_name(member_option);
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

    public static StringName resolve_default_member_id(
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

        PartyState partyState = get_party_state(window_data);
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

    public static string get_member_variant_display_name(GDictionary member_option)
    {
        if (member_option == null || !member_option.ContainsKey("display_name"))
            return "";
        return GdInterop.HasString(member_option, "display_name")
            ? GdInterop.GetString(member_option, "display_name").StripEdges()
            : "";
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

        PartyMemberState memberState = partyState.get_member_state(memberId);
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
        foreach (GDictionary optionData in GdInterop.ReadDictionaryItems(value))
        {
            var option = (GDictionary)optionData.Duplicate(true);
            string displayName = get_member_variant_display_name(option);
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
        if (dict == null || !dict.ContainsKey(key))
            return new GArray();
        return GdInterop.GetArray(dict, key);
    }

    private static StringName DictStringName(GDictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(dict[key]);
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsBool();
    }

    private static int DictInt(GDictionary dict, string key, int defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsInt32();
    }
}

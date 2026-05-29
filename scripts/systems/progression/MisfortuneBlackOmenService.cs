using Godot;

[GlobalClass]
public partial class MisfortuneBlackOmenService : RefCounted
{
    public static readonly StringName DOOM_MARKED_STAT_ID = "doom_marked";
    public static readonly StringName HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY =
        "cursed_relic_elite_or_boss_victory";
    public static readonly StringName HOOK_BOSS_CURSE_SURVIVAL_VICTORY =
        "boss_curse_survival_victory";
    public static readonly StringName HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH =
        "dead_road_lantern_black_omen_path";
    public static readonly Godot.Collections.Array<StringName> CURSED_RELIC_REQUIRED_TAGS = new()
    {
        "cursed",
        "relic",
    };

    public static StringName DOOM_MARKED_STAT_ID_VALUE() => DOOM_MARKED_STAT_ID;

    public static StringName HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY_VALUE() =>
        HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY;

    public static StringName HOOK_BOSS_CURSE_SURVIVAL_VICTORY_VALUE() =>
        HOOK_BOSS_CURSE_SURVIVAL_VICTORY;

    public static StringName HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH_VALUE() =>
        HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH;

    private GodotObject _character_gateway;
    private Godot.Collections.Dictionary _item_defs = new();

    public void setup(
        GodotObject characterGateway = null,
        Godot.Collections.Dictionary itemDefs = null
    )
    {
        _character_gateway = characterGateway;
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
    }

    public void dispose()
    {
        _character_gateway = null;
        _item_defs = new Godot.Collections.Dictionary();
    }

    public Godot.Collections.Dictionary try_run_hook(
        StringName hookId,
        Godot.Collections.Dictionary payload = null
    )
    {
        payload ??= new Godot.Collections.Dictionary();
        if (hookId == HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY)
            return _try_grant_cursed_relic_elite_or_boss_victory(payload);
        if (hookId == HOOK_BOSS_CURSE_SURVIVAL_VICTORY)
            return _try_grant_boss_curse_survival_victory(payload);
        if (hookId == HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH)
            return _try_grant_dead_road_lantern_black_omen_path(payload);
        var memberId = _resolve_member_id(payload);
        return _build_result(memberId, hookId, errorCode: "unknown_hook_id");
    }

    public Godot.Collections.Dictionary grant_doom_mark(
        StringName memberId,
        StringName sourceId,
        Godot.Collections.Dictionary sourceContext = null
    )
    {
        var result = _build_result(memberId, sourceId);
        if (memberId == "" || sourceId == "")
        {
            result["error_code"] = "invalid_request";
            return result;
        }
        var memberState = _get_member_state(memberId);
        if (
            memberState == null
            || memberState.progression == null
            || _get_unit_base_attributes(memberState) == null
        )
        {
            result["error_code"] = "member_not_found";
            return result;
        }
        result["ok"] = true;
        result["conditions_met"] = true;
        int currentValue = _get_doom_marked_value(memberState);
        result["doom_marked"] = currentValue;
        if (currentValue >= 1)
        {
            result["already_marked"] = true;
            return result;
        }
        _get_unit_base_attributes(memberState).Call("set_attribute_value", DOOM_MARKED_STAT_ID, 1);
        result["granted"] = true;
        result["doom_marked"] = 1;
        return result;
    }

    private Godot.Collections.Dictionary _try_grant_cursed_relic_elite_or_boss_victory(
        Godot.Collections.Dictionary payload
    )
    {
        var mid = _resolve_member_id(payload);
        var r = _build_result(mid, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY);
        var ms = _get_member_state(mid);
        if (mid == "")
        {
            r["error_code"] = "invalid_request";
            return r;
        }
        if (ms == null)
        {
            r["error_code"] = "member_not_found";
            return r;
        }
        r["ok"] = true;
        bool met =
            _is_payload_bool_true(payload, "encounter_won")
            && _is_payload_bool_true(payload, "defeated_elite_or_boss")
            && _has_cursed_relic(ms, payload);
        r["conditions_met"] = met;
        r["doom_marked"] = _get_doom_marked_value(ms);
        if (!met)
        {
            r["error_code"] = "conditions_not_met";
            return r;
        }
        return grant_doom_mark(mid, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY, payload);
    }

    private Godot.Collections.Dictionary _try_grant_boss_curse_survival_victory(
        Godot.Collections.Dictionary payload
    )
    {
        var mid = _resolve_member_id(payload);
        var r = _build_result(mid, HOOK_BOSS_CURSE_SURVIVAL_VICTORY);
        var ms = _get_member_state(mid);
        if (mid == "")
        {
            r["error_code"] = "invalid_request";
            return r;
        }
        if (ms == null)
        {
            r["error_code"] = "member_not_found";
            return r;
        }
        r["ok"] = true;
        bool met =
            _is_payload_bool_true(payload, "encounter_won")
            && _is_payload_bool_true(payload, "boss_encounter")
            && _is_payload_bool_true(payload, "member_survived")
            && _has_boss_curse(payload);
        r["conditions_met"] = met;
        r["doom_marked"] = _get_doom_marked_value(ms);
        if (!met)
        {
            r["error_code"] = "conditions_not_met";
            return r;
        }
        return grant_doom_mark(mid, HOOK_BOSS_CURSE_SURVIVAL_VICTORY, payload);
    }

    private Godot.Collections.Dictionary _try_grant_dead_road_lantern_black_omen_path(
        Godot.Collections.Dictionary payload
    )
    {
        var mid = _resolve_member_id(payload);
        var r = _build_result(mid, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH);
        var ms = _get_member_state(mid);
        if (mid == "")
        {
            r["error_code"] = "invalid_request";
            return r;
        }
        if (ms == null)
        {
            r["error_code"] = "member_not_found";
            return r;
        }
        r["ok"] = true;
        var pathTagsValue = payload.ContainsKey("path_tags")
            ? payload["path_tags"]
            : Variant.From(new Godot.Collections.Array());
        var pathTags = LowLuckRelicRules.normalize_path_tags(
            pathTagsValue.VariantType == Variant.Type.Array
                ? pathTagsValue.AsGodotArray()
                : new Godot.Collections.Array()
        );
        bool hasLantern = LowLuckRelicRules.member_has_item(
            _item_defs,
            ms,
            LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN
        );
        bool met = hasLantern && pathTags.Contains(LowLuckRelicRules.PATH_TAG_BLACK_OMEN);
        r["conditions_met"] = met;
        r["doom_marked"] = _get_doom_marked_value(ms);
        if (!met)
        {
            r["error_code"] = "conditions_not_met";
            return r;
        }
        return grant_doom_mark(mid, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH, payload);
    }

    private static StringName _resolve_member_id(Godot.Collections.Dictionary payload)
    {
        if (!payload.ContainsKey("member_id"))
            return new StringName("");
        var mv = payload["member_id"];
        if (mv.VariantType != Variant.Type.String && mv.VariantType != Variant.Type.StringName)
            return new StringName("");
        return ProgressionDataUtils.to_string_name(mv);
    }

    private static bool _is_payload_bool_true(Godot.Collections.Dictionary payload, string fn) =>
        payload.ContainsKey(fn)
        && payload[fn].VariantType == Variant.Type.Bool
        && payload[fn].AsBool();

    private bool _has_cursed_relic(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload
    )
    {
        if (payload.ContainsKey("has_cursed_relic"))
            return payload["has_cursed_relic"].VariantType == Variant.Type.Bool
                && payload["has_cursed_relic"].AsBool();
        if (memberState?.equipment_state == null || _item_defs.Count == 0)
            return false;
        foreach (var esId in memberState.equipment_state.get_entry_slot_ids())
        {
            var entry = memberState.equipment_state.get_entry(esId);
            var itemId = ProgressionDataUtils.to_string_name(entry?.Get("item_id") ?? "");
            if (entry == null || itemId == "")
                continue;
            var itemDef = _get_item_def(itemId);
            if (itemDef == null)
                continue;
            var itemTags = itemDef.get_tags();
            bool matched = true;
            foreach (var rt in CURSED_RELIC_REQUIRED_TAGS)
            {
                if (!itemTags.Contains(rt))
                {
                    matched = false;
                    break;
                }
            }
            if (matched)
                return true;
        }
        return false;
    }

    private static bool _has_boss_curse(Godot.Collections.Dictionary payload)
    {
        if (payload.ContainsKey("has_boss_curse"))
            return payload["has_boss_curse"].VariantType == Variant.Type.Bool
                && payload["has_boss_curse"].AsBool();
        var curseIds = ProgressionDataUtils.to_string_name_array(
            payload.ContainsKey("boss_curse_status_ids")
                ? payload["boss_curse_status_ids"]
                : new Godot.Collections.Array()
        );
        return curseIds.Count > 0;
    }

    private ItemDef _get_item_def(StringName itemId)
    {
        if (itemId == "")
            return null;
        foreach (var key in _item_defs.Keys)
        {
            if (key.VariantType == Variant.Type.StringName && key.AsStringName() == itemId)
                return _item_defs[key].AsGodotObject() as ItemDef;
        }
        return null;
    }

    private PartyMemberState _get_member_state(StringName memberId)
    {
        if (_character_gateway == null || memberId == "")
            return null;
        if (!_character_gateway.HasMethod("get_member_state"))
            return null;
        return _character_gateway.Call("get_member_state", memberId).AsGodotObject()
            as PartyMemberState;
    }

    private int _get_doom_marked_value(PartyMemberState memberState)
    {
        var uba = _get_unit_base_attributes(memberState);
        return uba?.Call("get_attribute_value", DOOM_MARKED_STAT_ID).AsInt32() ?? 0;
    }

    private static GodotObject _get_unit_base_attributes(PartyMemberState memberState) =>
        memberState?.progression?.Get("unit_base_attributes").AsGodotObject();

    private Godot.Collections.Dictionary _build_result(
        StringName memberId,
        StringName sourceId,
        string errorCode = ""
    )
    {
        int doomMarked = 0;
        var ms = _get_member_state(memberId);
        if (ms != null)
            doomMarked = _get_doom_marked_value(ms);
        return new Godot.Collections.Dictionary
        {
            { "ok", false },
            { "hook_id", (string)sourceId },
            { "member_id", (string)memberId },
            { "conditions_met", false },
            { "granted", false },
            { "already_marked", false },
            { "doom_marked", doomMarked },
            { "error_code", errorCode },
        };
    }
}

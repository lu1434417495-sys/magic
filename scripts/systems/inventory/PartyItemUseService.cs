using Godot;

[GlobalClass]
public partial class PartyItemUseService : RefCounted
{
    private static readonly Script PartyStateScript = GD.Load<Script>("res://scripts/player/progression/party_state.gd");

    private GodotObject _party_state = PartyStateScript.Call("new").AsGodotObject();
    private Godot.Collections.Dictionary _item_defs = new();
    private Godot.Collections.Dictionary _skill_defs = new();
    private GodotObject _warehouse_service;
    private GodotObject _character_management;

    public void setup(GodotObject partyState, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary skillDefs, GodotObject warehouseService, GodotObject characterManagement)
    {
        _party_state = partyState ?? PartyStateScript.Call("new").AsGodotObject();
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
        _skill_defs = skillDefs ?? new Godot.Collections.Dictionary();
        _warehouse_service = warehouseService;
        _character_management = characterManagement;
    }

    public Godot.Collections.Dictionary use_item(StringName itemId, StringName memberId, Godot.Collections.Dictionary options = null)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        var result = new Godot.Collections.Dictionary { { "success", false }, { "reason", "invalid_request" }, { "item_id", normalizedItemId }, { "member_id", normalizedMemberId }, { "skill_id", new StringName("") }, { "consumed_quantity", 0 }, { "needs_confirmation", false }, { "practice_replacement_preview", new Godot.Collections.Dictionary() } };
        if (normalizedItemId == "" || normalizedMemberId == "") return result;
        if (_party_state == null || _warehouse_service == null || _character_management == null) { result["reason"] = "service_unavailable"; return result; }

        var itemDef = _item_defs.ContainsKey(normalizedItemId) ? _item_defs[normalizedItemId].AsGodotObject() : null;
        if (itemDef == null) { result["reason"] = "missing_item_def"; return result; }
        if (!(bool)itemDef.Call("is_skill_book")) { result["reason"] = "item_not_usable"; return result; }

        var memberState = _party_state.Call("get_member_state", normalizedMemberId).AsGodotObject();
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null) { result["reason"] = "missing_member"; return result; }
        if (_warehouse_service.Call("count_item", normalizedItemId).AsInt32() <= 0) { result["reason"] = "missing_inventory"; return result; }

        var skillId = itemDef.Get("granted_skill_id").AsStringName();
        var skillDef = _skill_defs.ContainsKey(skillId) ? _skill_defs[skillId].AsGodotObject() : null;
        result["skill_id"] = skillId;
        if (skillDef == null) { result["reason"] = "missing_skill_def"; return result; }

        var practiceStatus = _get_practice_skill_learn_status(normalizedMemberId, skillId);
        var needsReplacement = practiceStatus.ContainsKey("needs_replacement") && (bool)practiceStatus["needs_replacement"];
        var confirmed = options != null && options.ContainsKey("confirm_practice_replacement") && (bool)options["confirm_practice_replacement"];
        if (needsReplacement && !confirmed)
        {
            result["reason"] = "practice_replacement_confirmation_required";
            result["needs_confirmation"] = true;
            result["practice_replacement_preview"] = practiceStatus;
            return result;
        }
        if (!(bool)_character_management.Call("learn_skill", normalizedMemberId, skillId, options ?? new Godot.Collections.Dictionary()))
        {
            result["reason"] = "learn_failed";
            return result;
        }

        var removeResult = _warehouse_service.Call("remove_item", normalizedItemId, 1).AsGodotDictionary();
        int removedQuantity = removeResult.ContainsKey("removed_quantity") ? removeResult["removed_quantity"].AsInt32() : 0;
        if (removedQuantity <= 0) { result["reason"] = "consume_failed"; return result; }

        result["success"] = true;
        result["reason"] = "ok";
        result["consumed_quantity"] = removedQuantity;
        return result;
    }

    private Godot.Collections.Dictionary _get_practice_skill_learn_status(StringName memberId, StringName skillId)
    {
        if (_character_management == null || !_character_management.HasMethod("get_practice_skill_learn_status"))
            return new Godot.Collections.Dictionary { { "is_practice_skill", false } };
        var status = _character_management.Call("get_practice_skill_learn_status", memberId, skillId);
        return status.VariantType == Variant.Type.Dictionary ? status.AsGodotDictionary() : new Godot.Collections.Dictionary { { "is_practice_skill", false } };
    }
}

using Godot;

[GlobalClass]
public partial class PartyItemUseService : RefCounted
{
    private PartyState _party_state = new();
    private Godot.Collections.Dictionary _item_defs = new();
    private Godot.Collections.Dictionary _skill_defs = new();
    private PartyWarehouseService _warehouse_service;
    private CharacterManagementModule _character_management;

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs,
        Godot.Collections.Dictionary skillDefs,
        PartyWarehouseService warehouseService,
        CharacterManagementModule characterManagement
    )
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
        _skill_defs = skillDefs ?? new Godot.Collections.Dictionary();
        _warehouse_service = warehouseService;
        _character_management = characterManagement;
    }

    public Godot.Collections.Dictionary use_item(StringName itemId, StringName memberId) =>
        use_item(itemId, memberId, new Godot.Collections.Dictionary());

    public Godot.Collections.Dictionary use_item(
        StringName itemId,
        StringName memberId,
        Godot.Collections.Dictionary options
    )
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        var result = new Godot.Collections.Dictionary
        {
            { "success", false },
            { "reason", new StringName("invalid_request") },
            { "item_id", normalizedItemId },
            { "member_id", normalizedMemberId },
            { "skill_id", new StringName("") },
            { "consumed_quantity", 0 },
            { "needs_confirmation", false },
            { "practice_replacement_preview", new Godot.Collections.Dictionary() },
        };

        if (normalizedItemId == "" || normalizedMemberId == "")
            return result;
        if (_party_state == null || _warehouse_service == null || _character_management == null)
            return _with_reason(result, "service_unavailable");

        var itemDef = get_item_def(normalizedItemId);
        if (itemDef == null)
            return _with_reason(result, "missing_item_def");
        if (!itemDef.is_skill_book())
            return _with_reason(result, "item_not_usable");

        var memberState = _party_state.get_member_state(normalizedMemberId);
        if (memberState == null || memberState.progression == null)
            return _with_reason(result, "missing_member");
        if (_warehouse_service.count_item(normalizedItemId) <= 0)
            return _with_reason(result, "missing_inventory");

        var skillId = itemDef.granted_skill_id;
        var skillDef = get_skill_def(skillId);
        result["skill_id"] = skillId;
        if (skillDef == null)
            return _with_reason(result, "missing_skill_def");

        options ??= new Godot.Collections.Dictionary();
        var practiceStatus = GetPracticeSkillLearnStatus(normalizedMemberId, skillId);
        bool needsReplacement = practiceStatus.NeedsReplacement;
        bool confirmed = HasConfirmedPracticeReplacement(options);
        if (needsReplacement && !confirmed)
        {
            result["reason"] = new StringName("practice_replacement_confirmation_required");
            result["needs_confirmation"] = true;
            result["practice_replacement_preview"] = practiceStatus.ToLearnedStatusDictionary();
            return result;
        }

        if (!_character_management.learn_skill(normalizedMemberId, skillId, options))
            return _with_reason(result, "learn_failed");

        var removeResult = _warehouse_service.remove_item(normalizedItemId, 1);
        int removedQuantity = removeResult.ContainsKey("removed_quantity")
            ? removeResult["removed_quantity"].AsInt32()
            : 0;
        if (removedQuantity <= 0)
            return _with_reason(result, "consume_failed");

        result["success"] = true;
        result["reason"] = new StringName("ok");
        result["consumed_quantity"] = removedQuantity;
        return result;
    }

    private ItemDef get_item_def(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (_item_defs.ContainsKey(normalizedItemId))
            return _item_defs[normalizedItemId].AsGodotObject() as ItemDef;
        var stringKey = normalizedItemId.ToString();
        return _item_defs.ContainsKey(stringKey)
            ? _item_defs[stringKey].AsGodotObject() as ItemDef
            : null;
    }

    private SkillDef get_skill_def(StringName skillId)
    {
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (_skill_defs.ContainsKey(normalizedSkillId))
            return _skill_defs[normalizedSkillId].AsGodotObject() as SkillDef;
        var stringKey = normalizedSkillId.ToString();
        return _skill_defs.ContainsKey(stringKey)
            ? _skill_defs[stringKey].AsGodotObject() as SkillDef
            : null;
    }

    private PracticeSkillLearnStatus GetPracticeSkillLearnStatus(
        StringName memberId,
        StringName skillId
    )
    {
        if (_character_management == null)
            return PracticeSkillLearnStatus.NonPractice();
        return _character_management.GetPracticeSkillLearnStatusTyped(memberId, skillId)
            ?? PracticeSkillLearnStatus.NonPractice();
    }

    private static Godot.Collections.Dictionary _with_reason(
        Godot.Collections.Dictionary result,
        string reason
    )
    {
        result["reason"] = new StringName(reason);
        return result;
    }

    private static bool HasConfirmedPracticeReplacement(Godot.Collections.Dictionary options)
    {
        if (options == null || !options.ContainsKey("confirm_practice_replacement"))
            return false;
        Variant value = options["confirm_practice_replacement"];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }
}

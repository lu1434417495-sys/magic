using System.Collections.Generic;
using Godot;

public class PartyItemUseService
{
    private PartyState _party_state = new();
    private Dictionary<StringName, ItemDef> _item_defs = new();
    private Dictionary<StringName, SkillDef> _skill_defs = new();
    private PartyWarehouseService _warehouse_service;
    private CharacterManagementModule _character_management;
    private bool _ownsPartyState = true;

    internal sealed class PartyItemUseOptions
    {
        public bool ConfirmPracticeReplacement { get; }

        public PartyItemUseOptions(bool confirmPracticeReplacement = false)
        {
            ConfirmPracticeReplacement = confirmPracticeReplacement;
        }

        public CharacterManagementModule.LearnSkillOptionsData ToLearnSkillOptions() =>
            new(ConfirmPracticeReplacement);
    }

    internal sealed class PartyItemUseResult
    {
        public bool Success { get; private set; }
        public StringName Reason { get; private set; }
        public StringName ItemId { get; }
        public StringName MemberId { get; }
        public StringName SkillId { get; private set; }
        public int ConsumedQuantity { get; private set; }
        public bool NeedsConfirmation { get; private set; }
        public PracticeSkillLearnStatus PracticeReplacementStatus { get; private set; }

        private PartyItemUseResult(StringName itemId, StringName memberId)
        {
            Success = false;
            Reason = "invalid_request";
            ItemId = itemId;
            MemberId = memberId;
            SkillId = "";
            ConsumedQuantity = 0;
            NeedsConfirmation = false;
            PracticeReplacementStatus = PracticeSkillLearnStatus.NonPractice();
        }

        public static PartyItemUseResult Create(StringName itemId, StringName memberId) =>
            new(itemId, memberId);

        public PartyItemUseResult WithReason(string reason)
        {
            Reason = new StringName(reason ?? "");
            return this;
        }

        public PartyItemUseResult WithSkill(StringName skillId)
        {
            SkillId = ProgressionDataUtils.to_string_name(skillId);
            return this;
        }

        public PartyItemUseResult WithConfirmationRequired(PracticeSkillLearnStatus status)
        {
            Reason = "practice_replacement_confirmation_required";
            NeedsConfirmation = true;
            PracticeReplacementStatus = status ?? PracticeSkillLearnStatus.NonPractice();
            return this;
        }

        public PartyItemUseResult WithSuccess(int consumedQuantity)
        {
            Success = true;
            Reason = "ok";
            ConsumedQuantity = Mathf.Max(consumedQuantity, 0);
            return this;
        }
    }

    public void Setup(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        PartyWarehouseService warehouseService,
        CharacterManagementModule characterManagement
    )
    {
        ReplacePartyState(partyState);
        _item_defs = itemDefs != null
            ? new Dictionary<StringName, ItemDef>(itemDefs)
            : new Dictionary<StringName, ItemDef>();
        _skill_defs = skillDefs != null
            ? new Dictionary<StringName, SkillDef>(skillDefs)
            : new Dictionary<StringName, SkillDef>();
        _warehouse_service = warehouseService;
        _character_management = characterManagement;
    }

    public void Dispose()
    {
        ReleaseOwnedPartyState();
        _item_defs.Clear();
        _skill_defs.Clear();
        _warehouse_service = null;
        _character_management = null;
    }

    private void ReplacePartyState(PartyState partyState)
    {
        ReleaseOwnedPartyState();
        if (partyState != null)
        {
            _party_state = partyState;
            _ownsPartyState = false;
            return;
        }
        _party_state = new PartyState();
        _ownsPartyState = true;
    }

    private void ReleaseOwnedPartyState()
    {
        if (_ownsPartyState)
            GodotRefCountedDisposer.DisposeIfValid(_party_state);
        _party_state = null;
        _ownsPartyState = false;
    }

    internal PartyItemUseResult UseItemTyped(
        StringName itemId,
        StringName memberId,
        PartyItemUseOptions options = null
    )
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        var result = PartyItemUseResult.Create(normalizedItemId, normalizedMemberId);

        if (normalizedItemId == "" || normalizedMemberId == "")
            return result;
        if (_party_state == null || _warehouse_service == null || _character_management == null)
            return result.WithReason("service_unavailable");

        if (!TryGetItemDef(normalizedItemId, out var itemDef))
            return result.WithReason("missing_item_def");
        if (!itemDef.IsSkillBook())
            return result.WithReason("item_not_usable");

        var memberState = _party_state.GetMemberState(normalizedMemberId);
        if (memberState == null || memberState.progression == null)
            return result.WithReason("missing_member");
        if (_warehouse_service.CountItem(normalizedItemId) <= 0)
            return result.WithReason("missing_inventory");

        var skillId = itemDef.granted_skill_id;
        result.WithSkill(skillId);
        if (!TryGetSkillDef(skillId, out _))
            return result.WithReason("missing_skill_def");

        options ??= new PartyItemUseOptions();
        var practiceStatus = GetPracticeSkillLearnStatus(normalizedMemberId, skillId);
        bool needsReplacement = practiceStatus.NeedsReplacement;
        bool confirmed = options.ConfirmPracticeReplacement;
        if (needsReplacement && !confirmed)
            return result.WithConfirmationRequired(practiceStatus);

        if (!_character_management.LearnSkillTyped(normalizedMemberId, skillId, options.ToLearnSkillOptions()))
            return result.WithReason("learn_failed");

        var removeResult = _warehouse_service.RemoveItemTyped(normalizedItemId, 1);
        if (removeResult.RemovedQuantity <= 0)
            return result.WithReason("consume_failed");

        return result.WithSuccess(removeResult.RemovedQuantity);
    }

    private bool TryGetItemDef(StringName itemId, out ItemDef itemDef)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId != "")
            return _item_defs.TryGetValue(normalizedItemId, out itemDef);
        itemDef = null;
        return false;
    }

    private bool TryGetSkillDef(StringName skillId, out SkillDef skillDef)
    {
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (normalizedSkillId != "")
            return _skill_defs.TryGetValue(normalizedSkillId, out skillDef);
        skillDef = null;
        return false;
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
}

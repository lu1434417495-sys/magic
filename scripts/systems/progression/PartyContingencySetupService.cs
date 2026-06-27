using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class PartyContingencySetupService
{
    private static readonly StringName ChargeMaterialItemId = "special_contingency_gem";
    private const int ChargeMaterialQuantity = 1;

    private PartyState _partyState = new();
    private PartyWarehouseService _warehouseService;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions =
        new Dictionary<StringName, SkillDefinition>();
    private Func<StringName, AttributeSnapshot> _attributeSnapshotProvider;
    private Func<bool> _battleMutationBlockedProvider;

    internal bool ForceSetupWriteFailureAfterWarehouseCommitForTests { get; set; }

    public void Setup(
        PartyState partyState,
        PartyWarehouseService warehouseService,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        Func<StringName, AttributeSnapshot> attributeSnapshotProvider,
        Func<bool> battleMutationBlockedProvider
    )
    {
        _partyState = partyState ?? new PartyState();
        _warehouseService = warehouseService;
        _skillDefinitions = skillDefinitions ?? new Dictionary<StringName, SkillDefinition>();
        _attributeSnapshotProvider = attributeSnapshotProvider;
        _battleMutationBlockedProvider = battleMutationBlockedProvider;
    }

    internal void SetBattleMutationGuardProvider(Func<bool> battleMutationBlockedProvider)
    {
        _battleMutationBlockedProvider = battleMutationBlockedProvider;
    }

    public ContingencySetupMutationResult SaveSetup(
        StringName memberId,
        ContingencyMatrixSetupState setup
    )
    {
        StringName normalizedMemberId = Normalize(memberId);
        if (setup == null)
            return Fail("invalid_setup", normalizedMemberId);
        if (IsBattleMutationBlocked())
            return Fail("battle_mutation_blocked", normalizedMemberId, setup.SetupId);
        PartyMemberState member = _partyState?.GetMemberState(normalizedMemberId);
        if (member == null)
            return Fail("member_not_found", normalizedMemberId, setup.SetupId);
        if (
            member.TryGetContingencySetupTyped(setup.SetupId, out ContingencyMatrixSetupState existing)
            && existing != null
            && existing.Charged
        )
            return Fail("setup_charged", normalizedMemberId, setup.SetupId);

        ContingencyMatrixSetupState uncharged = BuildSetupVariant(
            setup,
            charged: false,
            reservedMpMax: 0,
            MaterialCostsArray(Array.Empty<ContingencyMaterialCostState>())
        );
        if (uncharged == null)
            return Fail("invalid_setup", normalizedMemberId, setup.SetupId);

        PartyMemberState nextMember = ReplaceSetup(member, uncharged, appendIfMissing: true);
        if (!CandidatePassesContentValidation(nextMember))
            return Fail("invalid_setup", normalizedMemberId, setup.SetupId);

        _partyState.SetMemberState(nextMember);
        return SuccessFromSetup(normalizedMemberId, uncharged, member.current_mp);
    }

    public ContingencySetupMutationResult ChargeSetup(StringName memberId, GDictionary inlinePayload) =>
        Fail("inline_setup_payload_not_allowed", Normalize(memberId));

    public ContingencySetupMutationResult ChargeSetup(StringName memberId, StringName setupId)
    {
        StringName normalizedMemberId = Normalize(memberId);
        StringName normalizedSetupId = Normalize(setupId);
        if (IsBattleMutationBlocked())
            return Fail("battle_mutation_blocked", normalizedMemberId, normalizedSetupId);

        PartyMemberState member = _partyState?.GetMemberState(normalizedMemberId);
        if (member == null)
            return Fail("member_not_found", normalizedMemberId, normalizedSetupId);
        if (!member.TryGetContingencySetupTyped(normalizedSetupId, out ContingencyMatrixSetupState setup))
            return Fail("setup_not_found", normalizedMemberId, normalizedSetupId);
        if (setup.Charged)
            return Fail("setup_already_charged", normalizedMemberId, normalizedSetupId);
        if (member.GetChargedContingencySetupCount() > 0)
            return Fail("charged_setup_limit", normalizedMemberId, normalizedSetupId);

        List<ContingencyMaterialCostState> costs = BuildChargeCosts();
        int reservedMpMax = Mathf.Max(setup.MatrixLoad * 2, 1);
        ContingencyMatrixSetupState chargedCandidate = BuildSetupVariant(
            setup,
            charged: true,
            reservedMpMax,
            MaterialCostsArray(costs)
        );
        if (chargedCandidate == null)
            return Fail("invalid_setup", normalizedMemberId, normalizedSetupId);

        PartyMemberState candidateMember = ReplaceSetup(member, chargedCandidate, appendIfMissing: false);
        if (!CandidatePassesContentValidation(candidateMember))
            return Fail("content_validation_failed", normalizedMemberId, normalizedSetupId);

        List<WarehouseBatchQuantityEntry> withdraw = BuildWarehouseCostEntries(costs);
        WarehouseBatchSwapResult preview = _warehouseService?.PreviewBatchQuantitySwapTyped(
            withdraw,
            Array.Empty<WarehouseBatchQuantityEntry>()
        );
        if (preview == null || !preview.Allowed)
            return Fail("material_insufficient", normalizedMemberId, normalizedSetupId);

        WarehouseState warehouseSnapshot = _warehouseService.CaptureWarehouseStateForTransaction();
        PartyMemberState memberSnapshot = member.DuplicateState();
        int currentMpSnapshot = member.current_mp;
        try
        {
            WarehouseBatchSwapResult commit = _warehouseService.CommitBatchQuantitySwapTyped(
                withdraw,
                Array.Empty<WarehouseBatchQuantityEntry>()
            );
            if (commit == null || !commit.Allowed)
                return RollbackAndFail(
                    warehouseSnapshot,
                    memberSnapshot,
                    currentMpSnapshot,
                    "material_commit_failed",
                    normalizedMemberId,
                    normalizedSetupId
                );

            if (ForceSetupWriteFailureAfterWarehouseCommitForTests)
                return RollbackAndFail(
                    warehouseSnapshot,
                    memberSnapshot,
                    currentMpSnapshot,
                    "setup_write_failed",
                    normalizedMemberId,
                    normalizedSetupId
                );

            _partyState.SetMemberState(candidateMember);
            int effectiveMpMax = ClampMemberMpToEffectiveMax(normalizedMemberId);
            if (!PostChargeConditionsHold(normalizedMemberId, normalizedSetupId, effectiveMpMax))
                return RollbackAndFail(
                    warehouseSnapshot,
                    memberSnapshot,
                    currentMpSnapshot,
                    "postcondition_failed",
                    normalizedMemberId,
                    normalizedSetupId
                );
            return SuccessFromSetup(normalizedMemberId, chargedCandidate, effectiveMpMax);
        }
        catch
        {
            return RollbackAndFail(
                warehouseSnapshot,
                memberSnapshot,
                currentMpSnapshot,
                "charge_transaction_failed",
                normalizedMemberId,
                normalizedSetupId
            );
        }
    }

    public ContingencySetupMutationResult ClearCharge(StringName memberId, StringName setupId)
    {
        StringName normalizedMemberId = Normalize(memberId);
        StringName normalizedSetupId = Normalize(setupId);
        if (IsBattleMutationBlocked())
            return Fail("battle_mutation_blocked", normalizedMemberId, normalizedSetupId);

        PartyMemberState member = _partyState?.GetMemberState(normalizedMemberId);
        if (member == null)
            return Fail("member_not_found", normalizedMemberId, normalizedSetupId);
        if (!member.TryGetContingencySetupTyped(normalizedSetupId, out ContingencyMatrixSetupState setup))
            return Fail("setup_not_found", normalizedMemberId, normalizedSetupId);
        if (!setup.Charged)
            return Fail("setup_not_charged", normalizedMemberId, normalizedSetupId);

        ContingencyMatrixSetupState cleared = BuildSetupVariant(
            setup,
            charged: false,
            reservedMpMax: 0,
            MaterialCostsArray(Array.Empty<ContingencyMaterialCostState>())
        );
        if (cleared == null)
            return Fail("invalid_setup", normalizedMemberId, normalizedSetupId);

        PartyMemberState nextMember = ReplaceSetup(member, cleared, appendIfMissing: false);
        if (!CandidatePassesContentValidation(nextMember))
            return Fail("content_validation_failed", normalizedMemberId, normalizedSetupId);
        _partyState.SetMemberState(nextMember);
        int effectiveMpMax = GetEffectiveMpMax(normalizedMemberId);
        return SuccessFromSetup(normalizedMemberId, cleared, effectiveMpMax);
    }

    public ContingencySetupMutationResult BuildStatus(StringName memberId, StringName setupId)
    {
        StringName normalizedMemberId = Normalize(memberId);
        StringName normalizedSetupId = Normalize(setupId);
        PartyMemberState member = _partyState?.GetMemberState(normalizedMemberId);
        if (member == null)
            return Fail("member_not_found", normalizedMemberId, normalizedSetupId);
        if (!member.TryGetContingencySetupTyped(normalizedSetupId, out ContingencyMatrixSetupState setup))
            return Fail("setup_not_found", normalizedMemberId, normalizedSetupId);
        return SuccessFromSetup(normalizedMemberId, setup, GetEffectiveMpMax(normalizedMemberId));
    }

    private bool CandidatePassesContentValidation(PartyMemberState candidateMember)
    {
        PartyState candidateParty = _partyState?.DuplicateState() ?? new PartyState();
        candidateParty.SetMemberState(candidateMember);
        return ContingencyContentValidator
            .ValidateAllSetupsForSaveLoad(candidateParty, _skillDefinitions)
            .Count == 0;
    }

    private ContingencySetupMutationResult RollbackAndFail(
        WarehouseState warehouseSnapshot,
        PartyMemberState memberSnapshot,
        int currentMpSnapshot,
        string errorCode,
        StringName memberId,
        StringName setupId
    )
    {
        if (memberSnapshot != null)
        {
            memberSnapshot.SetCurrentMp(currentMpSnapshot);
            _partyState.SetMemberState(memberSnapshot);
        }
        _warehouseService?.RestoreWarehouseStateForTransaction(warehouseSnapshot);
        return Fail(errorCode, memberId, setupId);
    }

    private int ClampMemberMpToEffectiveMax(StringName memberId)
    {
        PartyMemberState member = _partyState?.GetMemberState(memberId);
        if (member == null)
            return 0;
        int effectiveMpMax = GetEffectiveMpMax(memberId);
        if (member.current_mp > effectiveMpMax)
        {
            PartyMemberState nextMember = member.DuplicateState();
            nextMember.SetCurrentMp(effectiveMpMax);
            _partyState.SetMemberState(nextMember);
        }
        return effectiveMpMax;
    }

    private int GetEffectiveMpMax(StringName memberId)
    {
        AttributeSnapshot snapshot = _attributeSnapshotProvider?.Invoke(memberId);
        return Mathf.Max(snapshot?.GetValue(AttributeService.MP_MAX) ?? 0, 0);
    }

    private bool PostChargeConditionsHold(StringName memberId, StringName setupId, int effectiveMpMax)
    {
        PartyMemberState member = _partyState?.GetMemberState(memberId);
        if (
            member == null
            || !member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup)
            || setup == null
            || !setup.Charged
            || member.current_mp > effectiveMpMax
        )
            return false;
        return HasExpectedChargeReceipt(setup);
    }

    private static bool HasExpectedChargeReceipt(ContingencyMatrixSetupState setup)
    {
        if (setup?.MaterialCosts == null || setup.MaterialCosts.Count != 1)
            return false;
        ContingencyMaterialCostState cost = setup.MaterialCosts[0];
        return cost != null
            && cost.ItemId == ChargeMaterialItemId
            && cost.Quantity == ChargeMaterialQuantity;
    }

    private static PartyMemberState ReplaceSetup(
        PartyMemberState member,
        ContingencyMatrixSetupState setup,
        bool appendIfMissing
    )
    {
        List<ContingencyMatrixSetupState> setups = new();
        bool replaced = false;
        foreach (ContingencyMatrixSetupState existing in member.GetContingencySetupsTyped())
        {
            if (existing != null && existing.SetupId == setup.SetupId)
            {
                setups.Add(setup.DuplicateState());
                replaced = true;
            }
            else if (existing != null)
            {
                setups.Add(existing.DuplicateState());
            }
        }
        if (!replaced && appendIfMissing)
            setups.Add(setup.DuplicateState());
        return member.WithContingencySetupsForMutation(setups);
    }

    private static ContingencyMatrixSetupState BuildSetupVariant(
        ContingencyMatrixSetupState setup,
        bool charged,
        int reservedMpMax,
        GArray materialCosts
    )
    {
        GDictionary payload = setup?.ToDictionary();
        if (payload == null)
            return null;
        payload["charged"] = charged;
        payload["reserved_mp_max"] = Mathf.Max(reservedMpMax, 0);
        payload["material_costs"] = materialCosts ?? new GArray();
        return ContingencyMatrixSetupState.FromDictionary(payload);
    }

    private static List<ContingencyMaterialCostState> BuildChargeCosts() =>
        new()
        {
            ContingencyMaterialCostState.Create(ChargeMaterialItemId, ChargeMaterialQuantity),
        };

    private static List<WarehouseBatchQuantityEntry> BuildWarehouseCostEntries(
        IReadOnlyList<ContingencyMaterialCostState> costs
    )
    {
        List<WarehouseBatchQuantityEntry> result = new();
        foreach (ContingencyMaterialCostState cost in costs ?? Array.Empty<ContingencyMaterialCostState>())
            if (cost != null)
                result.Add(new WarehouseBatchQuantityEntry(cost.ItemId, cost.Quantity));
        return result;
    }

    private static GArray MaterialCostsArray(IReadOnlyList<ContingencyMaterialCostState> costs)
    {
        GArray result = new();
        foreach (ContingencyMaterialCostState cost in costs ?? Array.Empty<ContingencyMaterialCostState>())
            if (cost != null)
                result.Add(cost.ToDictionary());
        return result;
    }

    private bool IsBattleMutationBlocked() => _battleMutationBlockedProvider?.Invoke() ?? false;

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);

    private static ContingencySetupMutationResult Fail(
        string errorCode,
        StringName memberId = default,
        StringName setupId = default
    ) =>
        ContingencySetupMutationResult.Failure(errorCode, memberId, setupId);

    private static ContingencySetupMutationResult SuccessFromSetup(
        StringName memberId,
        ContingencyMatrixSetupState setup,
        int effectiveMpMax
    ) =>
        ContingencySetupMutationResult.Success(
            memberId,
            setup?.SetupId ?? new StringName(""),
            setup?.Charged ?? false,
            setup?.ReservedMpMax ?? 0,
            effectiveMpMax,
            setup?.MaterialCosts
        );
}

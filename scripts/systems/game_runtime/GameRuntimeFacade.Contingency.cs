using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — contingency setup/charge runtime mutations.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{

    public ContingencySetupMutationResult GetLastContingencyCommandResultTyped() =>
        _last_contingency_command_result;

    internal ContingencySetupMutationResult CommandContingencyStatusTyped(StringName member_id)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(member_id);
        _last_contingency_command_result = BuildContingencyResultSnapshot(
            ContingencySetupMutationResult.Success(
                normalizedMemberId,
                "",
                false,
                0,
                GetContingencyEffectiveMpMax(normalizedMemberId)
            )
        );
        return _last_contingency_command_result;
    }

    internal ContingencySetupMutationResult SaveContingencySetupTemplateRuntimeTyped(
        StringName member_id,
        StringName setup_payload_name
    ) =>
        ExecuteContingencyTemplateSaveRuntimeMutation(
            member_id,
            setup_payload_name,
            "contingency_setup_save"
        );

    internal ContingencySetupMutationResult EditContingencySetupTemplateRuntimeTyped(
        StringName member_id,
        StringName setup_payload_name
    ) =>
        ExecuteContingencyTemplateSaveRuntimeMutation(
            member_id,
            setup_payload_name,
            "contingency_setup_edit"
        );

    internal ContingencySetupMutationResult ChargeContingencySetupRuntimeTyped(
        StringName member_id,
        StringName setup_id
    ) =>
        RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
            member_id,
            setup_id,
            "contingency_setup_charge",
                () =>
                    _character_management.ChargeContingencySetup(
                        member_id,
                        setup_id,
                        IsContingencyMutationBlocked
                    )
            )
        );

    internal ContingencySetupMutationResult ClearContingencyChargeRuntimeTyped(
        StringName member_id,
        StringName setup_id
    ) =>
        RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
            member_id,
            setup_id,
            "contingency_setup_clear",
                () =>
                    _character_management.ClearContingencyCharge(
                        member_id,
                        setup_id,
                        IsContingencyMutationBlocked
                    )
            )
        );

    private ContingencySetupMutationResult ExecuteContingencyTemplateSaveRuntimeMutation(
        StringName memberId,
        StringName setupPayloadName,
        StringName commitReason
    )
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        StringName normalizedPayloadName = ProgressionDataUtils.to_string_name(setupPayloadName);
        if (IsContingencyMutationBlocked())
            return RecordContingencyResult(
                ContingencySetupMutationResult.Failure(
                    "battle_mutation_blocked",
                    normalizedMemberId,
                    ResolveContingencyTemplateSetupId(normalizedPayloadName)
                )
            );

        ContingencyMatrixSetupState setup = BuildContingencySetupTemplate(
            normalizedMemberId,
            normalizedPayloadName,
            out string errorCode
        );
        if (setup == null)
            return RecordContingencyResult(
                ContingencySetupMutationResult.Failure(
                    errorCode,
                    normalizedMemberId,
                    ResolveContingencyTemplateSetupId(normalizedPayloadName)
                )
            );

        return RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
                normalizedMemberId,
                setup.SetupId,
                commitReason,
                () =>
                    _character_management.SaveContingencySetup(
                        normalizedMemberId,
                        setup,
                        IsContingencyMutationBlocked
                    )
            )
        );
    }

    private ContingencySetupMutationResult ExecuteContingencySetupRuntimeMutation(
        StringName memberId,
        StringName setupId,
        StringName commitReason,
        Func<ContingencySetupMutationResult> mutate
    )
    {
        if (_character_management == null || mutate == null)
            return ContingencySetupMutationResult.Failure(
                "runtime_unavailable",
                memberId,
                setupId
            );

        RuntimeTransaction transaction = new RuntimeTransaction().MarkPartyChanged();
        RuntimeTransactionRollbackState rollbackState = RuntimeTransactionRollbackState.Capture(
            this,
            transaction
        );
        ContingencySetupMutationResult mutationResult = mutate.Invoke();
        if (mutationResult == null)
            return ContingencySetupMutationResult.Failure("mutation_failed", memberId, setupId);
        if (!mutationResult.Ok)
            return mutationResult;

        _party_state = _character_management.GetPartyState();
        RuntimeCommitResult commitResult = CommitRuntimeTransaction(transaction, commitReason);
        if (commitResult.Ok)
            return mutationResult;

        transaction.Rollback(this, rollbackState);
        SyncPartyStateServices();
        return ContingencySetupMutationResult.Failure(
            "persistence_failure",
            mutationResult.MemberId,
            mutationResult.SetupId
        );
    }

    private ContingencySetupMutationResult RecordContingencyResult(
        ContingencySetupMutationResult result
    )
    {
        _last_contingency_command_result = BuildContingencyResultSnapshot(result);
        return _last_contingency_command_result;
    }

    private ContingencySetupMutationResult BuildContingencyResultSnapshot(
        ContingencySetupMutationResult result
    )
    {
        if (result == null)
            return ContingencySetupMutationResult.Failure("mutation_failed", "", "");

        StringName memberId = ProgressionDataUtils.to_string_name(result.MemberId);
        StringName setupId = ProgressionDataUtils.to_string_name(result.SetupId);
        bool charged = result.Charged;
        int reservedMpMax = result.ReservedMpMax;
        int effectiveMpMax = result.EffectiveMpMax;
        IReadOnlyList<ContingencyMaterialCostState> materialCosts = result.MaterialCosts;

        if (
            setupId != ""
            && _party_state?.GetMemberState(memberId) is PartyMemberState member
            && member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup)
            && setup != null
        )
        {
            charged = setup.Charged;
            reservedMpMax = setup.ReservedMpMax;
            materialCosts = setup.MaterialCosts;
            effectiveMpMax = GetContingencyEffectiveMpMax(memberId);
        }

        if (result.Ok)
            return ContingencySetupMutationResult.Success(
                memberId,
                setupId,
                charged,
                reservedMpMax,
                effectiveMpMax,
                materialCosts
            );

        return new ContingencySetupMutationResult
        {
            Ok = false,
            ErrorCode = result.ErrorCode,
            MemberId = memberId,
            SetupId = setupId,
            Charged = charged,
            ReservedMpMax = reservedMpMax,
            EffectiveMpMax = effectiveMpMax,
            MaterialCosts = materialCosts ?? Array.Empty<ContingencyMaterialCostState>(),
        };
    }

    private int GetContingencyEffectiveMpMax(StringName memberId)
    {
        if (_character_management == null)
            return 0;
        AttributeSnapshot snapshot = _character_management.GetMemberAttributeSnapshot(memberId);
        return Mathf.Max(snapshot?.GetValue(AttributeService.MP_MAX) ?? 0, 0);
    }

    private bool IsContingencyMutationBlocked() =>
        IsBattleActive() || (_game_session?.IsBattleSaveLocked() ?? false);

    private ContingencyMatrixSetupState BuildContingencySetupTemplate(
        StringName memberId,
        StringName payloadName,
        out string errorCode
    )
    {
        errorCode = "";
        ContingencySetupTemplateDef template = ResolveContingencySetupTemplateDef(payloadName);
        if (template == null)
        {
            errorCode = "unknown_setup_payload";
            return null;
        }

        PartyMemberState member = _party_state?.GetMemberState(memberId);
        UnitProgress progress = member?.progression;
        if (!TryGetLearnedSkillLevel(progress, template.source_skill_id, out int sourceLevel))
        {
            errorCode = "missing_required_skill";
            return null;
        }

        var castLevelsByStoredSkillId = new Dictionary<StringName, int>();
        foreach (
            ContingencyTemplateStoredSpellInfo storedSpell
            in ContingencyContentRules.GetTemplateStoredSpellsTyped(template)
        )
        {
            if (!TryGetLearnedSkillLevel(progress, storedSpell.StoredSkillId, out int storedLevel))
            {
                errorCode = "missing_required_skill";
                return null;
            }
            castLevelsByStoredSkillId[storedSpell.StoredSkillId] = Mathf.Max(
                Mathf.Min(storedLevel, storedSpell.MaxCastLevel),
                1
            );
        }

        GDictionary payload = ContingencyContentRules.BuildSetupPayloadFromTemplate(
            template,
            Mathf.Max(sourceLevel, 1),
            castLevelsByStoredSkillId
        );
        ContingencyMatrixSetupState setup = payload != null
            ? ContingencyMatrixSetupState.FromDictionary(payload)
            : null;
        if (setup == null)
            errorCode = "invalid_setup";
        return setup;
    }

    private ContingencySetupTemplateDef ResolveContingencySetupTemplateDef(StringName payloadName)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(payloadName);
        if (normalized == "")
            return null;
        IReadOnlyDictionary<StringName, ContingencySetupTemplateDef> templates =
            _game_session?.GetContingencySetupTemplatesTyped();
        return templates != null
            && templates.TryGetValue(normalized, out ContingencySetupTemplateDef template)
            ? template
            : null;
    }

    private StringName ResolveContingencyTemplateSetupId(StringName payloadName) =>
        ResolveContingencySetupTemplateDef(payloadName)?.template_id ?? new StringName("");

    private static bool TryGetLearnedSkillLevel(
        UnitProgress progress,
        StringName skillId,
        out int level
    )
    {
        level = 0;
        UnitSkillProgress skillProgress = progress?.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;
        level = Mathf.Max(skillProgress.skill_level, 1);
        return true;
    }
}

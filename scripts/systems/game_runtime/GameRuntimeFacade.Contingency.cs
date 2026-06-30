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
        RuntimeTransactionRollbackState rollbackState = RuntimeTransactionRollbackState.Capture(this);
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
        bool ownerTurnTemplate = payloadName == "owner_turn_mirror_self";
        bool hpTemplate = payloadName == "hp_mirror_self";
        if (!hpTemplate && !ownerTurnTemplate)
        {
            errorCode = "unknown_setup_payload";
            return null;
        }

        PartyMemberState member = _party_state?.GetMemberState(memberId);
        UnitProgress progress = member?.progression;
        if (
            !TryGetLearnedSkillLevel(progress, "mage_chain_contingency", out int chainLevel)
            || !TryGetLearnedSkillLevel(progress, "mage_mirror_image", out int mirrorLevel)
        )
        {
            errorCode = "missing_required_skill";
            return null;
        }

        GDictionary payload = new()
        {
            ["setup_id"] = payloadName.ToString(),
            ["display_name"] = ownerTurnTemplate ? "起手镜影" : "濒死镜影",
            ["enabled"] = true,
            ["charged"] = false,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = Mathf.Max(chainLevel, 1),
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = 0,
            ["material_costs"] = new GArray(),
            ["trigger"] = ownerTurnTemplate
                ? new GDictionary
                {
                    ["type"] = "owner_turn_started",
                    ["subject"] = "owner",
                    ["timing"] = "owner_turn_started",
                }
                : new GDictionary
                {
                    ["type"] = "hp_below_percent",
                    ["subject"] = "owner",
                    ["percent"] = 30,
                    ["crossing_only"] = true,
                    ["timing"] = "after_hp_changed",
                },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray
            {
                new GDictionary
                {
                    ["stored_skill_id"] = "mage_mirror_image",
                    ["cast_level"] = Mathf.Max(Mathf.Min(mirrorLevel, 2), 1),
                    ["order"] = 1,
                    ["target_resolver"] = new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };
        ContingencyMatrixSetupState setup = ContingencyMatrixSetupState.FromDictionary(payload);
        if (setup == null)
            errorCode = "invalid_setup";
        return setup;
    }

    private static StringName ResolveContingencyTemplateSetupId(StringName payloadName)
    {
        if (payloadName == "hp_mirror_self")
            return new StringName("hp_mirror_self");
        if (payloadName == "owner_turn_mirror_self")
            return new StringName("owner_turn_mirror_self");
        return new StringName("");
    }

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

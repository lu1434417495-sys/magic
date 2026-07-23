using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleContingencyBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleContingencyRuntimePort
{
    private int _sourceEventOrdinal;

    internal BattleContingencySystem GetContingencySystemTyped()
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._contingency_system;
    }

    internal StringName AllocateContingencySourceEventId(StringName prefix)
    {
        _sourceEventOrdinal += 1;
        string normalizedPrefix = ProgressionDataUtils.to_string_name(prefix).ToString();
        if (string.IsNullOrEmpty(normalizedPrefix))
            normalizedPrefix = "battle_fact";
        return new StringName($"{normalizedPrefix}:{_sourceEventOrdinal}");
    }

    internal void EmitContingencyHpAndStatusHooks(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int previousHp,
        IReadOnlyList<StringName> statusIds,
        StringName sourceEventId
    )
    {
        _runtime._ensure_sidecars_ready();
        if (targetUnit == null)
            return;
        BattleEffectOrigin origin = _runtime.CurrentEffectOriginForContingency;
        Vector2I sourceCell = sourceUnit?.coord ?? new Vector2I(-1, -1);
        Vector2I targetCell = targetUnit.coord;
        int maxHp = Math.Max(
            targetUnit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? targetUnit.current_hp,
            1
        );
        if (targetUnit.current_hp != previousHp)
        {
            _runtime._contingency_system.OnHookFact(
                ContingencyHookFact.HpChanged(
                    sourceEventId,
                    sourceUnit?.unit_id ?? "",
                    targetUnit.unit_id,
                    previousHp,
                    targetUnit.current_hp,
                    maxHp,
                    origin,
                    sourceCell,
                    targetCell
                )
            );
        }
        if (statusIds != null && statusIds.Count > 0)
        {
            _runtime._contingency_system.OnHookFact(
                ContingencyHookFact.StatusApplied(
                    sourceEventId,
                    sourceUnit?.unit_id ?? "",
                    targetUnit.unit_id,
                    statusIds,
                    origin,
                    sourceCell,
                    targetCell
                )
            );
        }
    }

    internal void EmitContingencySpellAffected(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> affectedUnitIds,
        StringName sourceEventId,
        IReadOnlyList<Vector2I> areaCells = null
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._contingency_system.OnHookFact(
            ContingencyHookFact.SpellAffected(
                sourceEventId,
                sourceUnit?.unit_id ?? "",
                targetUnit?.unit_id ?? "",
                affectedUnitIds ?? Array.Empty<StringName>(),
                _runtime.CurrentEffectOriginForContingency,
                sourceUnit?.coord ?? new Vector2I(-1, -1),
                targetUnit?.coord ?? new Vector2I(-1, -1),
                areaCells
            )
        );
    }

    internal void EmitContingencyPositionChanged(
        BattleUnitState unitState,
        Vector2I previousCoord,
        Vector2I currentCoord,
        StringName sourceEventId
    )
    {
        _runtime._ensure_sidecars_ready();
        if (unitState == null || previousCoord == currentCoord)
            return;
        _runtime._contingency_system.OnHookFact(
            ContingencyHookFact.PositionChanged(
                sourceEventId,
                unitState.unit_id,
                previousCoord,
                currentCoord,
                _runtime.CurrentEffectOriginForContingency
            )
        );
    }

    internal bool ExecuteAutoCast(AutoCastRequest request, BattleEventBatch batch)
    {
        _runtime._ensure_sidecars_ready();
        if (request?.IsValid != true || _runtime._state == null)
            return false;
        if (!IsContingencyAutoCastSourcePlayerLearned(request))
            return false;
        using IDisposable originScope = _runtime._metricsReportService.PushEffectOrigin(BattleEffectOrigin.AutoCast(request));
        return _runtime._skill_orchestrator.ExecuteAutoCast(request, batch ?? _runtime._new_batch());
    }

    internal bool IsContingencyAutoCastSourcePlayerLearned(AutoCastRequest request)
    {
        if (request == null || request.OwnerMemberId == "" || request.SourceSkillId == "")
            return false;
        PartyMemberState memberState = _runtime._characterGateway
            ?.GetPartyState()
            ?.GetMemberState(request.OwnerMemberId);
        UnitSkillProgress progress = memberState?.progression?.GetSkillProgress(
            request.SourceSkillId
        );
        return progress != null
            && progress.is_learned
            && progress.skill_level > 0
            && progress.GrantedSourceTypeKind == UnitSkillGrantSourceType.Player;
    }

    internal IReadOnlyList<ContingencyTargetResolutionResult> ResolveContingencyStoredSpellTargetsForRelease(
        ContingencyReleaseContext context,
        ContingencyFrozenTriggerFacts facts
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._contingency_system.ResolveStoredSpellTargetsForRelease(context, facts);
    }

    internal void OnBattleConfirmed(BattleEventBatch batch)
    {
        _runtime._ensure_sidecars_ready();
        ArgumentNullException.ThrowIfNull(batch);
        _runtime._contingency_system.OnBattleConfirmed(batch);
        _runtime._contingency_system.ExecuteQueuedReleaseContexts(
            ContingencyFrozenTriggerFacts.Empty,
            batch
        );
    }

    internal void OnOwnerTurnStarted(BattleUnitState ownerUnit, BattleEventBatch batch = null)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._contingency_system.OnOwnerTurnStarted(ownerUnit, batch);
    }

    internal int ExecuteQueuedContingencyReleases(
        ContingencyFrozenTriggerFacts facts,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._contingency_system.ExecuteQueuedReleaseContexts(
            facts ?? ContingencyFrozenTriggerFacts.Empty,
            batch
        );
    }

    internal int ExecuteNextSequentialContingencyAutoCastForOwner(
        BattleUnitState ownerUnit,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._contingency_system.ExecuteNextSequentialAutoCastForOwner(
            ownerUnit?.unit_id ?? "",
            batch
        );
    }

    internal void RefreshBattleUnitForContingencyOverlay(BattleUnitState unitState)
    {
        if (unitState == null)
            return;
        _runtime._ensure_sidecars_ready();
        _runtime._unit_factory.RefreshBattleUnit(unitState);
    }

    BattleState IBattleContingencyRuntimePort.GetBattleState() =>
        _runtime?.GetState();

    BattleGridService IBattleContingencyRuntimePort.GetGridService() =>
        _runtime?.GetGridService();

    SkillDefinition IBattleContingencyRuntimePort.GetSkillDefinition(StringName skillId) =>
        _runtime?.GetSkillDefinitionTyped(skillId);

    UnitSkillGrantSourceType IBattleContingencyRuntimePort.ResolveSourceSkillGrantSourceType(
        StringName memberId,
        StringName skillId
    )
    {
        UnitSkillProgress progress = _runtime
            ?.GetCharacterGatewayTyped()
            ?.GetPartyState()
            ?.GetMemberState(memberId)
            ?.progression
            ?.GetSkillProgress(skillId);
        return progress?.GrantedSourceTypeKind ?? UnitSkillGrantSourceType.Unknown;
    }

    StringName IBattleContingencyRuntimePort.AllocateSourceEventId(StringName prefix) =>
        _runtime != null ? AllocateContingencySourceEventId(prefix) : "";

    bool IBattleContingencyRuntimePort.ExecuteAutoCast(
        AutoCastRequest request,
        BattleEventBatch batch
    ) => _runtime != null && ExecuteAutoCast(request, batch);

    void IBattleContingencyRuntimePort.RefreshUnitOverlay(BattleUnitState unitState)
    {
        if (_runtime != null)
            RefreshBattleUnitForContingencyOverlay(unitState);
    }

    internal ContingencyConsumedCommitResult ValidateContingencyConsumedSetupsForBattleUnit(
        BattleUnitState unitState
    )
    {
        IReadOnlyList<StringName> consumedSetupIds =
            unitState?.GetConsumedContingencySetupIdsTyped() ?? Array.Empty<StringName>();
        if (consumedSetupIds.Count == 0)
            return ContingencyConsumedCommitResult.Success(
                unitState?.source_member_id ?? "",
                0
            );
        return _runtime._characterGateway.ValidateContingencyConsumedSetups(
            unitState.source_member_id,
            consumedSetupIds
        );
    }

    internal ContingencyConsumedCommitResult CommitContingencyConsumedSetupsForBattleUnit(
        BattleUnitState unitState
    )
    {
        IReadOnlyList<StringName> consumedSetupIds =
            unitState?.GetConsumedContingencySetupIdsTyped() ?? Array.Empty<StringName>();
        if (consumedSetupIds.Count == 0)
            return ContingencyConsumedCommitResult.Success(
                unitState?.source_member_id ?? "",
                0
            );
        return _runtime._characterGateway.CommitContingencyConsumedSetups(
            unitState.source_member_id,
            consumedSetupIds
        );
    }
}

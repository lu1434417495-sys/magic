using System;

internal sealed class BattleWeaponAttackOutcomeCommitter
    : BattleRuntimeModuleBorrower
{
    private readonly BattleEquipmentDurabilityResultProjector
        _durabilityResultProjector;

    internal BattleWeaponAttackOutcomeCommitter(
        BattleEquipmentDurabilityResultProjector durabilityResultProjector
    )
    {
        _durabilityResultProjector = durabilityResultProjector
            ?? throw new ArgumentNullException(nameof(durabilityResultProjector));
    }

    internal void CommitResolverSurface(
        BattleWeaponAttackResolverSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.MarkAppliedStatusesForTurnTiming(
            request.TargetUnit,
            request.Resolution.StatusEffectIds
        );
        _runtime._append_changed_unit_id(
            request.Batch,
            request.TargetUnit.unit_id
        );
        _runtime._append_changed_unit_coords(
            request.Batch,
            request.TargetUnit
        );
        _runtime.AppendResultSourceStatusEffects(
            request.Batch,
            request.SourceUnit,
            request.Resolution
        );
    }

    internal void CommitPostProducerHooks(
        BattleWeaponAttackPostProducerHookRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.EmitContingencyHpAndStatusHooks(
            request.SourceUnit,
            request.TargetUnit,
            request.PreviousTargetHp,
            request.AppliedStatusIds,
            request.SourceEventId
        );
    }

    internal void CommitUnappliedResultSurface(
        BattleWeaponAttackUnappliedResultSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.AppendResultReportEntry(
            request.Batch,
            request.Resolution
        );
    }

    internal void CommitAppliedResultSurface(
        BattleWeaponAttackAppliedResultSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime._report_formatter.AppendDamageResultLogLines(
            request.Batch,
            request.SubjectLabel,
            request.TargetDisplayLabel,
            request.Resolution
        );
        _durabilityResultProjector.Commit(
            request.TargetUnit,
            request.Resolution,
            request.Batch
        );
        _runtime.AppendResultReportEntry(
            request.Batch,
            request.Resolution
        );
    }

    internal void CommitTerminalOutcome(
        BattleWeaponAttackTerminalOutcomeRequest request
    )
    {
        RequireBoundAndActiveBatch(request);
        bool isEquipmentReaction =
            request.Kind
                == BattleWeaponAttackOutcomeKind.EquipmentReaction;
        bool recordsCombatContribution =
            request.Kind
                == BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack
            || request.Kind
                == BattleWeaponAttackOutcomeKind.Counterattack;
        if (!isEquipmentReaction && !recordsCombatContribution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Kind),
                request.Kind,
                null
            );
        }

        if (!request.TargetUnit.IsAlive())
        {
            _runtime.HandleUnitDefeatedByRuntimeEffect(
                request.TargetUnit,
                request.SourceUnit,
                request.Batch,
                $"{request.TargetUnit.display_name} 被击倒。",
                new BattleDefeatHandlingOptions(
                    recordEnemyDefeatedAchievement: true,
                    killProvenance: request.KillProvenance
                )
            );
        }

        if (isEquipmentReaction)
            return;

        int damage = request.Resolution.Damage;
        int healing = request.Resolution.Healing;
        bool causedDefeat = !request.TargetUnit.IsAlive();
        _runtime._record_effect_metrics(
            request.SourceUnit,
            request.TargetUnit,
            damage,
            healing,
            causedDefeat ? 1 : 0
        );
        _runtime._battle_rating_system.RecordContributionFromUnits(
            request.SourceUnit,
            request.TargetUnit,
            damage,
            healing,
            causedDefeat,
            request.Kind == BattleWeaponAttackOutcomeKind.Counterattack
                ? new Godot.StringName("counterattack")
                : new Godot.StringName("skill"),
            request.SkillId
        );
    }

    private void RequireStandardOrCounter(
        BattleWeaponAttackOutcomeRequest request
    )
    {
        RequireBoundAndActiveBatch(request);
        if (
            request.Kind
                != BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack
            && request.Kind
                != BattleWeaponAttackOutcomeKind.Counterattack
        )
        {
            throw new InvalidOperationException(
                $"{request.Kind} has no standard weapon result surface"
            );
        }
    }

    private void RequireBoundAndActiveBatch(
        BattleWeaponAttackOutcomeRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_runtime == null)
        {
            throw new InvalidOperationException(
                "weapon outcome committer is not bound"
            );
        }
        _runtime.RequireActiveReactionBatch(request.Batch);
    }
}

internal sealed class BattleEquipmentDurabilityResultProjector
    : BattleRuntimeModuleBorrower
{
    internal void Commit(
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    )
    {
        if (_runtime == null)
        {
            throw new InvalidOperationException(
                "durability projector is not bound"
            );
        }
        if (targetUnit == null || batch == null)
            return;

        bool destroyedAny = false;
        foreach (
            EquipmentDurabilityEventResult eventResult
                in result.EquipmentDurabilityEvents
                    ?? Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            string itemId = eventResult.ItemId ?? "";
            if (string.IsNullOrEmpty(itemId))
                itemId = "装备";
            if (
                eventResult.SaveResult.HasSave
                && eventResult.SaveResult.Success
            )
            {
                batch.AddLogLine(
                    $"{targetUnit.display_name} 的 {itemId} 抵抗了裂解术。"
                );
                continue;
            }
            int durabilityLoss = eventResult.DurabilityLoss;
            if (durabilityLoss <= 0)
                continue;
            if (eventResult.Destroyed)
            {
                destroyedAny = true;
                batch.AddLogLine(
                    $"{targetUnit.display_name} 的 {itemId} 被裂解为尘埃。"
                );
            }
            else
            {
                batch.AddLogLine(
                    $"{targetUnit.display_name} 的 {itemId} 被裂解，耐久 "
                    + $"{eventResult.DurabilityBefore} -> "
                    + $"{eventResult.DurabilityAfter}。"
                );
            }
        }
        if (!destroyedAny)
            return;
        _runtime._append_changed_unit_id(batch, targetUnit.unit_id);
        _runtime._append_changed_unit_coords(batch, targetUnit);
    }
}

using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleImmediateWeaponAttackService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleWeaponAttackOutcomeCommitter
        _weaponAttackOutcomeCommitter;
    private readonly IBattleCounterattackWeaponAttackDefinitionProvider
        _counterattackDefinitionProvider;

    internal BattleImmediateWeaponAttackService(
        BattleWeaponAttackOutcomeCommitter weaponAttackOutcomeCommitter,
        IBattleCounterattackWeaponAttackDefinitionProvider
            counterattackDefinitionProvider
    )
    {
        _weaponAttackOutcomeCommitter =
            weaponAttackOutcomeCommitter
            ?? throw new ArgumentNullException(
                nameof(weaponAttackOutcomeCommitter)
            );
        _counterattackDefinitionProvider =
            counterattackDefinitionProvider
            ?? throw new ArgumentNullException(
                nameof(counterattackDefinitionProvider)
            );
    }

    internal BattleImmediateWeaponAttackPlan PrepareCounterattack(
        BattleCounterattackImmediateWeaponAttackRequest request
    )
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        bool resolved = _counterattackDefinitionProvider.TryResolve(
            request.State,
            request.SourceUnit,
            request.Capability,
            out BattleImmediateWeaponAttackDefinition definition
        );
        BattleAttackDeliveryKind deliveryKind =
            resolved && definition != null
                ? BattleAttackDeliveryRules.Resolve(
                    definition.EffectDefinitions,
                    request.SourceUnit.GetWeaponProjectionReadViewTyped()
                )
                : BattleAttackDeliveryKind.Unknown;
        bool available =
            resolved
            && definition != null
            && (
                deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
                || deliveryKind == BattleAttackDeliveryKind.RangedWeapon
            );
        return new BattleCounterattackWeaponAttackPlan(
            request.State,
            request.SourceUnit,
            request.TargetUnit,
            available,
            available ? definition.SkillDefinition : null,
            available
                ? definition.EffectDefinitions
                : Array.Empty<CombatEffectDefinition>(),
            available
                ? deliveryKind
                : BattleAttackDeliveryKind.Unknown,
            available ? definition.StaminaCost : 0,
            request.Capability.AttackRollBonus,
            request.Capability.InstanceId,
            _runtime?._skill_mastery_service
                ?.ResolveWeaponTrainingSkillId(request.SourceUnit)
                ?? new StringName("")
        );
    }

    internal BattleImmediateWeaponAttackPlan PrepareEquipmentReaction(
        BattleEquipmentImmediateWeaponAttackRequest request
    )
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            request.SkillDefinition.CombatProfile?.EffectDefinitions
            ?? Array.Empty<CombatEffectDefinition>();
        if (!BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions))
        {
            throw new InvalidOperationException(
                "immediate_weapon_attack resolved non-weapon effects"
            );
        }
        BattleAttackDeliveryKind deliveryKind =
            BattleAttackDeliveryRules.Resolve(
                effectDefinitions,
                request.SourceUnit.GetWeaponProjectionReadViewTyped()
            );
        bool available =
            deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
            || deliveryKind == BattleAttackDeliveryKind.RangedWeapon;
        return new BattleEquipmentReactionWeaponAttackPlan(
            request.State,
            request.SourceUnit,
            request.TargetUnit,
            available,
            request.SkillDefinition,
            effectDefinitions,
            deliveryKind,
            new BattleImmediateWeaponAttackEquipmentAttribution(
                request.TraitId,
                request.BindingId,
                request.ActionId,
                request.SourceEquipmentInstanceId
            )
        );
    }

    internal BattleImmediateWeaponAttackAvailability Query(
        BattleImmediateWeaponAttackPlan plan
    )
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        BattleRuntimeModule runtime = _runtime
            ?? throw new InvalidOperationException(
                "immediate attack service is not bound"
            );
        if (!ReferenceEquals(plan.State, runtime.GetState()))
        {
            throw new InvalidOperationException(
                "immediate attack plan belongs to another battle"
            );
        }
        if (!plan.DefinitionAvailable)
        {
            return BattleImmediateWeaponAttackAvailability.Blocked(
                BattleImmediateWeaponAttackBlockReason.AttackUnavailable
            );
        }

        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
            plan.SourceUnit,
            plan.SkillDefinition
        );
        int currentDistance = runtime
            .GetGridService()
            .GetDistanceBetweenUnits(
                plan.SourceUnit,
                plan.TargetUnit
            );
        if (currentDistance > effectiveRange)
        {
            return BattleImmediateWeaponAttackAvailability.Blocked(
                BattleImmediateWeaponAttackBlockReason.OutOfReach,
                effectiveRange,
                currentDistance,
                plan.StaminaCost,
                plan.SourceUnit.GetCurrentStamina()
            );
        }

        BattleBarrierInteractionResult barrier =
            runtime._layered_barrier_service
                .PreviewSkillBarrierInteractionResult(
                    plan.SourceUnit,
                    plan.TargetUnit,
                    plan.SkillDefinition,
                    plan.EffectDefinitions
                );
        if (barrier.Blocked)
        {
            return BattleImmediateWeaponAttackAvailability.Blocked(
                BattleImmediateWeaponAttackBlockReason.BarrierBlocked,
                effectiveRange,
                currentDistance,
                plan.StaminaCost,
                plan.SourceUnit.GetCurrentStamina()
            );
        }
        int currentStamina = plan.SourceUnit.GetCurrentStamina();
        if (currentStamina < plan.StaminaCost)
        {
            return BattleImmediateWeaponAttackAvailability.Blocked(
                BattleImmediateWeaponAttackBlockReason.InsufficientStamina,
                effectiveRange,
                currentDistance,
                plan.StaminaCost,
                currentStamina
            );
        }
        return BattleImmediateWeaponAttackAvailability.Allowed(
            effectiveRange,
            currentDistance,
            plan.StaminaCost,
            currentStamina
        );
    }

    internal BattleImmediateWeaponAttackResult Execute(
        BattleImmediateWeaponAttackPlan plan,
        BattleEventBatch batch
    )
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        BattleRuntimeModule runtime = _runtime
            ?? throw new InvalidOperationException(
                "immediate attack service is not bound"
            );
        if (!ReferenceEquals(plan.State, runtime.GetState()))
        {
            throw new InvalidOperationException(
                "immediate attack plan belongs to another battle"
            );
        }
        if (!plan.DefinitionAvailable)
        {
            throw new InvalidOperationException(
                "unavailable immediate attack plan cannot execute"
            );
        }
        if (
            (
                plan is BattleCounterattackWeaponAttackPlan
                && plan.Mode
                    != BattleImmediateWeaponAttackMode.Counterattack
            )
            || (
                plan is BattleEquipmentReactionWeaponAttackPlan
                && plan.Mode
                    != BattleImmediateWeaponAttackMode.EquipmentReaction
            )
            || (
                plan is not BattleCounterattackWeaponAttackPlan
                and not BattleEquipmentReactionWeaponAttackPlan
            )
        )
        {
            throw new InvalidOperationException(
                "immediate attack plan type/mode mismatch"
            );
        }
        runtime.RequireActiveReactionBatch(batch);

        using BattleReactionBoundaryScope boundary =
            runtime.BeginReactionBoundary(batch);
        try
        {
            BattleImmediateWeaponAttackResult execution;
            using (
                BattleLogicalAttackScope logicalAttack =
                    runtime.BeginLogicalAttack(plan.DeliveryKind)
            )
            {
                try
                {
                    int previousTargetHp =
                        plan.TargetUnit.GetCurrentHp();
                    StringName sourceEventId =
                        plan.Mode
                            == BattleImmediateWeaponAttackMode.Counterattack
                            ? runtime.AllocateContingencySourceEventId(
                                "counterattack"
                            )
                            : new StringName("");
                    BattleAttackCheckPolicyService attackPolicy =
                        runtime.GetAttackCheckPolicyService()
                        ?? throw new InvalidOperationException(
                            "attack policy is not bound"
                        );
                    BattleDamageResolver damageResolver =
                        runtime._damage_resolver
                        ?? throw new InvalidOperationException(
                            "damage resolver is not bound"
                        );
                    BattleAttackCheckPolicyContext policyContext =
                        attackPolicy.BuildSkillDefinitionAttackContext(
                            plan.State,
                            plan.SourceUnit,
                            plan.TargetUnit,
                            plan.SkillDefinition,
                            new StringName("skill_attack_check"),
                            plan.TraceSource,
                            force_hit_no_crit: false
                        );
                    AttackCheckInput attackCheck =
                        attackPolicy.BuildAttackCheck(
                            policyContext,
                            plan.AttackRollBonus,
                            0
                        );
                    AttackEffectResolutionResult resolution =
                        damageResolver.ResolveAttackEffects(
                            plan.SourceUnit,
                            plan.TargetUnit,
                            plan.EffectDefinitions,
                            attackCheck,
                            new AttackContext
                            {
                                BattleState = plan.State,
                                SkillId =
                                    plan.SkillDefinition.SkillId,
                                EventBatch = batch,
                                Action = logicalAttack.Context,
                            }
                        );

                    execution =
                        plan.Mode switch
                        {
                            BattleImmediateWeaponAttackMode.Counterattack =>
                                CommitCounterattackOutcome(
                                    plan,
                                    resolution,
                                    previousTargetHp,
                                    sourceEventId,
                                    batch
                                ),
                            BattleImmediateWeaponAttackMode.EquipmentReaction =>
                                CommitEquipmentReactionOutcome(
                                    plan,
                                    resolution,
                                    batch
                                ),
                            _ => throw new ArgumentOutOfRangeException(
                                nameof(plan.Mode),
                                plan.Mode,
                                null
                            ),
                        };

                    logicalAttack.Complete();
                }
                catch
                {
                    runtime.AbortActiveReactionBoundary();
                    throw;
                }
            }
            boundary.Complete();
            return execution;
        }
        catch
        {
            runtime.AbortActiveReactionBoundary();
            throw;
        }
    }

    private BattleImmediateWeaponAttackResult
        CommitCounterattackOutcome(
            BattleImmediateWeaponAttackPlan plan,
            AttackEffectResolutionResult resolution,
            int previousTargetHp,
            StringName sourceEventId,
            BattleEventBatch batch
        )
    {
        if (
            plan is not BattleCounterattackWeaponAttackPlan
                counterattackPlan
        )
        {
            throw new InvalidOperationException(
                "counterattack outcome requires counterattack plan"
            );
        }
        const BattleWeaponAttackOutcomeKind kind =
            BattleWeaponAttackOutcomeKind.Counterattack;
        _weaponAttackOutcomeCommitter.CommitResolverSurface(
            new BattleWeaponAttackResolverSurfaceRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch
            )
        );

        var appliedStatusIds = new List<StringName>();
        foreach (
            StringName statusId in
                resolution.StatusEffectIds
        )
        {
            if (
                statusId != new StringName("")
                && !appliedStatusIds.Contains(statusId)
            )
            {
                appliedStatusIds.Add(statusId);
            }
        }
        _weaponAttackOutcomeCommitter.CommitPostProducerHooks(
            new BattleWeaponAttackPostProducerHookRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch,
                previousTargetHp,
                appliedStatusIds,
                sourceEventId
            )
        );

        if (!resolution.Applied)
        {
            _weaponAttackOutcomeCommitter
                .CommitUnappliedResultSurface(
                    new BattleWeaponAttackUnappliedResultSurfaceRequest(
                        kind,
                        plan.SourceUnit,
                        plan.TargetUnit,
                        resolution,
                        batch
                    )
                );
            return BattleImmediateWeaponAttackResult
                .ForCounterattack(resolution);
        }

        string sourceLabel =
            string.IsNullOrEmpty(
                plan.SourceUnit.display_name
            )
                ? "未知单位"
                : plan.SourceUnit.display_name;
        _weaponAttackOutcomeCommitter.CommitAppliedResultSurface(
            new BattleWeaponAttackAppliedResultSurfaceRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch,
                $"{sourceLabel} 发起反击",
                plan.TargetUnit.display_name ?? ""
            )
        );
        BattleKillProvenance killProvenance =
            BattleKillProvenance.FromWeaponAttackResult(
                plan.SourceUnit,
                resolution,
                kind,
                plan.SourceActionId
            );
        _weaponAttackOutcomeCommitter.CommitTerminalOutcome(
            new BattleWeaponAttackTerminalOutcomeRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch,
                plan.SkillDefinition.SkillId,
                killProvenance
            )
        );
        BattleSkillMasteryGrant weaponTrainingGrant =
            _runtime._skill_mastery_service
                ?.BuildCounterattackWeaponTrainingMasteryGrant(
                    plan.SourceUnit,
                    plan.TargetUnit,
                    counterattackPlan.WeaponTrainingSkillId,
                    resolution,
                    _runtime.GetSkillDefinitionIndexTyped()
                );
        _runtime.ApplySkillMasteryGrantTyped(
            plan.SourceUnit,
            weaponTrainingGrant,
            batch
        );
        return BattleImmediateWeaponAttackResult
            .ForCounterattack(resolution);
    }

    private BattleImmediateWeaponAttackResult
        CommitEquipmentReactionOutcome(
            BattleImmediateWeaponAttackPlan plan,
            AttackEffectResolutionResult resolution,
            BattleEventBatch batch
        )
    {
        bool countsTowardMaxAttacks =
            resolution.Applied || resolution.AttackSuccess;
        if (!countsTowardMaxAttacks)
        {
            return BattleImmediateWeaponAttackResult
                .ForEquipmentMiss(resolution);
        }

        if (
            plan is not BattleEquipmentReactionWeaponAttackPlan
                equipmentPlan
        )
        {
            throw new InvalidOperationException(
                "equipment mode requires equipment plan"
            );
        }
        BattleImmediateWeaponAttackEquipmentAttribution attribution =
            equipmentPlan.EquipmentAttribution;
        var summary =
            new BattleEquipmentAbilityImmediateWeaponAttackResult
            {
                BindingId = attribution.BindingId,
                ActionId = attribution.ActionId,
                TargetUnitId = plan.TargetUnit.unit_id,
                Applied = resolution.Applied,
                Damage = Math.Max(resolution.Damage, 0),
            };
        batch.AddChangedUnitId(plan.SourceUnit.unit_id);
        batch.AddChangedUnitId(plan.TargetUnit.unit_id);
        foreach (
            Vector2I coord in
                plan.TargetUnit.GetOccupiedCoordsTyped()
        )
        {
            batch.AddChangedCoord(coord);
        }
        batch.AddLogLine(
            $"{plan.SourceUnit.display_name} 借 {attribution.TraitId} "
            + $"追击 {plan.TargetUnit.display_name}。"
        );

        const BattleWeaponAttackOutcomeKind kind =
            BattleWeaponAttackOutcomeKind.EquipmentReaction;
        BattleKillProvenance fallback =
            BattleKillProvenance.ForWeaponAttack(
                kind,
                attribution.SourceEquipmentInstanceId,
                attribution.BindingId,
                attribution.ActionId
            );
        BattleKillProvenance killProvenance =
            BattleKillProvenance.FromWeaponAttackResult(
                plan.SourceUnit,
                resolution,
                kind,
                fallback
            );
        _weaponAttackOutcomeCommitter.CommitTerminalOutcome(
            new BattleWeaponAttackTerminalOutcomeRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch,
                plan.SkillDefinition.SkillId,
                killProvenance
            )
        );
        return BattleImmediateWeaponAttackResult.ForEquipment(
            resolution,
            summary
        );
    }
}

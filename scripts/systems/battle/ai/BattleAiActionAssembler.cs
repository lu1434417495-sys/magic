using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiActionAssembler
{
    private static readonly StringName CandidateRequestMode = "candidate_request";
    private static readonly StringName NoScreeningMode = "none";
    private readonly BattleAiSkillAffordanceClassifier _classifier = new();

    public BattleAiRuntimeActionPlan BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var plan = new BattleAiRuntimeActionPlan();
        try
        {
            if (unitState == null || brain == null)
                return plan;

            using BattleAiTraceSpan trace = new("build_unit_action_plan");
            skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
            plan.SetSource(unitState, brain);

            List<BattleAiSkillAffordanceRecord> skillRecords = ClassifyKnownActiveSkills(
                unitState,
                skillDefinitions
            );
            foreach (BattleAiSkillAffordanceRecord record in skillRecords)
                plan.SetSkillAffordanceRecordTyped(record);

            foreach (EnemyAiStateDefinition state in brain.StateOrder)
            {
                if (state == null)
                    continue;

                StringName stateId = state.StateId;
                IReadOnlyList<EnemyAiActionDefinition> authoredActions = state.Actions;
                plan.AddStateActions(stateId, authoredActions);

                foreach (EnemyAiGenerationSlotDefinition slot in SortGenerationSlots(state.GenerationSlots))
                {
                    if (slot == null)
                        continue;

                    foreach (BattleAiSkillAffordanceRecord record in skillRecords)
                    {
                        if (record?.is_generatable != true)
                            continue;

                        StringName skillId = record.skill_id;
                        SkillDefinition skillDefinition = GetSkillDefinition(
                            skillDefinitions,
                            skillId
                        );
                        if (skillDefinition == null)
                            continue;

                        foreach (StringName actionFamily in record.action_families)
                        {
                            if (
                                actionFamily == ""
                                || !slot.MatchesAffordance(record, actionFamily)
                                || IsGenerationSuppressed(
                                    plan,
                                    state,
                                    authoredActions,
                                    slot,
                                    skillId,
                                    actionFamily
                                )
                            )
                            {
                                continue;
                            }

                            EnemyAiActionDefinition generatedAction = BuildGeneratedAction(
                                unitState,
                                state,
                                authoredActions,
                                slot,
                                skillDefinition,
                                actionFamily
                            );
                            if (generatedAction == null)
                                continue;

                            string identityKey = BuildIdentityKey(
                                stateId,
                                slot.SlotId,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedAction(
                                stateId,
                                generatedAction,
                                slot.SlotId,
                                slot.SlotRole,
                                skillId,
                                actionFamily,
                                slot.StyleTemplateActionId,
                                identityKey
                            );
                        }
                    }
                }
            }
            return plan;
        }
        catch (Exception buildFailure)
        {
            try
            {
                plan.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Battle AI action-plan construction and cleanup both failed.",
                    buildFailure,
                    cleanupFailure
                );
            }
            throw;
        }
    }

    private List<BattleAiSkillAffordanceRecord> ClassifyKnownActiveSkills(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var records = new List<BattleAiSkillAffordanceRecord>();
        BattleSkillAvailabilityView availabilityView = new BattleSkillAvailabilityService(
            skillDefinitions
        ).BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unitState,
                Consumer = BattleSkillAvailabilityConsumer.AiPlanning,
                IncludeKnownSkills = true,
                IncludeEquipmentSkills = false,
                IncludeScopedAutoCast = false,
            }
        );
        foreach (BattleAvailableSkillEntry entry in availabilityView.SkillEntries)
        {
            StringName skillId = entry.EntryRef.SkillId;
            SkillDefinition definition =
                entry.SkillDefinition ?? GetSkillDefinition(skillDefinitions, skillId);
            if (skillId == "" || definition == null)
                continue;

            BattleAiSkillAffordanceRecord record = _classifier.ClassifySkill(
                definition,
                entry.SkillLevel
            );
            if (record.skill_id == "")
                record.skill_id = skillId;
            records.Add(record);
        }
        return records;
    }

    private static List<EnemyAiGenerationSlotDefinition> SortGenerationSlots(
        IReadOnlyList<EnemyAiGenerationSlotDefinition> slots
    )
    {
        var result = new List<EnemyAiGenerationSlotDefinition>();
        foreach (EnemyAiGenerationSlotDefinition slot in slots ?? Array.Empty<EnemyAiGenerationSlotDefinition>())
        {
            if (slot != null)
                result.Add(slot);
        }
        result.Sort(
            (left, right) =>
            {
                int orderComparison = left.Order.CompareTo(right.Order);
                return orderComparison != 0
                    ? orderComparison
                    : string.Compare(
                        left.SlotId.ToString(),
                        right.SlotId.ToString(),
                        StringComparison.Ordinal
                    );
            }
        );
        return result;
    }

    private static bool IsGenerationSuppressed(
        BattleAiRuntimeActionPlan plan,
        EnemyAiStateDefinition state,
        IReadOnlyList<EnemyAiActionDefinition> authoredActions,
        EnemyAiGenerationSlotDefinition slot,
        StringName skillId,
        StringName actionFamily
    )
    {
        if (slot.SuppressionPolicyKind == EnemyAiGenerationSuppressionPolicy.ManualOnly)
            return true;

        string identityKey = BuildIdentityKey(
            state.StateId,
            slot.SlotId,
            skillId,
            actionFamily
        );
        if (plan.HasActionIdentityKey(identityKey))
            return true;

        EnemyAiActionFamily family = ToActionFamily(actionFamily);
        foreach (EnemyAiActionDefinition authoredAction in authoredActions)
        {
            if (
                authoredAction != null
                && Contains(authoredAction.DeclaredSkillIds, skillId)
                && GetActionFamily(authoredAction) == family
            )
            {
                return true;
            }
        }
        return false;
    }

    private static EnemyAiActionDefinition BuildGeneratedAction(
        BattleUnitState unitState,
        EnemyAiStateDefinition state,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        EnemyAiGenerationSlotDefinition slot,
        SkillDefinition skillDefinition,
        StringName actionFamily
    )
    {
        EnemyAiActionFamily family = ToActionFamily(actionFamily);
        StringName actionId = BuildRuntimeActionId(
            state.StateId,
            slot.SlotId,
            skillDefinition.SkillId,
            actionFamily
        );
        StringName actionIntent = ResolveGeneratedActionIntent(
            slot,
            skillDefinition,
            family
        );

        EnemyAiActionDefinition action = family switch
        {
            EnemyAiActionFamily.UseUnitSkill => BuildUnitAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.UseGroundSkill => BuildGroundAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.UseMultiUnitSkill => BuildMultiUnitAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.UseRandomChainSkill => BuildRandomChainAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.UseCharge => BuildChargeAction(
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.UseChargePathAoe => BuildChargePathAoeAction(
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.MoveToRange => BuildMoveToRangeAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            EnemyAiActionFamily.MoveToMultiUnitSkillPosition => BuildMoveToMultiUnitAction(
                unitState,
                stateActions,
                skillDefinition,
                actionId,
                actionIntent
            ),
            _ => null,
        };
        return ApplySlotOverrides(action, slot, stateActions);
    }

    private static UseUnitSkillActionDefinition BuildUnitAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        DistanceStyle style = ResolveUnitDistanceStyle(
            unitState,
            stateActions,
            skillDefinition,
            EnemyAiActionFamily.UseUnitSkill,
            EnemyAiActionFamily.UseMultiUnitSkill,
            EnemyAiDistanceReference.TargetUnit
        );
        return new UseUnitSkillActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseUnitSkill),
            actionIntent,
            new[] { skillDefinition.SkillId },
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            1,
            0,
            false,
            style.Minimum,
            style.Maximum,
            EnemyAiDistanceReferences.ToStringName(style.Reference)
        );
    }

    private static UseGroundSkillActionDefinition BuildGroundAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        DistanceStyle style = ResolveGroundDistanceStyle(
            unitState,
            stateActions,
            skillDefinition
        );
        return new UseGroundSkillActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseGroundSkill),
            actionIntent,
            new[] { skillDefinition.SkillId },
            Math.Max(skillDefinition.CombatProfile?.MinTargetCount ?? 0, 1),
            false,
            false,
            1,
            0,
            0,
            false,
            0,
            0,
            style.Minimum,
            style.Maximum,
            EnemyAiDistanceReferences.ToStringName(style.Reference)
        );
    }

    private static UseMultiUnitSkillActionDefinition BuildMultiUnitAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        DistanceStyle style = ResolveUnitDistanceStyle(
            unitState,
            stateActions,
            skillDefinition,
            EnemyAiActionFamily.UseMultiUnitSkill,
            EnemyAiActionFamily.UseUnitSkill,
            EnemyAiDistanceReference.TargetUnit
        );
        return new UseMultiUnitSkillActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseMultiUnitSkill),
            actionIntent,
            new[] { skillDefinition.SkillId },
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            style.Minimum,
            style.Maximum,
            EnemyAiDistanceReferences.ToStringName(style.Reference),
            6,
            12
        );
    }

    private static MoveToMultiUnitSkillPositionActionDefinition BuildMoveToMultiUnitAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        DistanceStyle style = ResolveUnitDistanceStyle(
            unitState,
            stateActions,
            skillDefinition,
            EnemyAiActionFamily.UseMultiUnitSkill,
            EnemyAiActionFamily.UseUnitSkill,
            EnemyAiDistanceReference.TargetUnit
        );
        return new MoveToMultiUnitSkillPositionActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition
            ),
            actionIntent,
            new[] { skillDefinition.SkillId },
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            style.Minimum,
            style.Maximum,
            EnemyAiDistanceReferences.ToStringName(style.Reference),
            6,
            12,
            40
        );
    }

    private static UseRandomChainSkillActionDefinition BuildRandomChainAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        EnemyAiActionDefinition template =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseRandomChainSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill);
        DistanceStyle style;
        if (template != null)
        {
            EnemyAiDistanceReference reference = GetDistanceReference(template);
            style = new DistanceStyle(
                GetDesiredMinDistance(template),
                GetDesiredMaxDistance(template),
                reference is EnemyAiDistanceReference.CandidatePool
                    or EnemyAiDistanceReference.EnemyFrontline
                    ? reference
                    : EnemyAiDistanceReference.CandidatePool
            );
        }
        else
        {
            int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
                unitState,
                skillDefinition
            );
            int minimum = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
            style = new DistanceStyle(
                minimum,
                Math.Max(effectiveRange, minimum),
                EnemyAiDistanceReference.CandidatePool
            );
        }
        return new UseRandomChainSkillActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseUnitSkill),
            actionIntent,
            new[] { skillDefinition.SkillId },
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            style.Minimum,
            style.Maximum,
            EnemyAiDistanceReferences.ToStringName(style.Reference),
            1
        );
    }

    private static UseChargeActionDefinition BuildChargeAction(
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    ) =>
        new(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseCharge),
            actionIntent,
            skillDefinition.SkillId,
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            3
        );

    private static UseChargePathAoeActionDefinition BuildChargePathAoeAction(
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    ) =>
        new(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.UseCharge),
            actionIntent,
            new[] { skillDefinition.SkillId },
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            1,
            1,
            1
        );

    private static MoveToRangeActionDefinition BuildMoveToRangeAction(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        StringName actionId,
        StringName actionIntent
    )
    {
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        int minimum = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        return new MoveToRangeActionDefinition(
            actionId,
            ResolveGeneratedScoreBucketId(stateActions, EnemyAiActionFamily.MoveToRange),
            actionIntent,
            CandidateRequestMode,
            ResolveTargetSelector(stateActions, EnemyAiTargetSelectorRules.NearestEnemy),
            minimum,
            Math.Max(effectiveRange, minimum),
            new[] { skillDefinition.SkillId },
            NoScreeningMode,
            true,
            2,
            140,
            220,
            1000,
            4000,
            4,
            2,
            2,
            45
        );
    }

    private static DistanceStyle ResolveUnitDistanceStyle(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition,
        EnemyAiActionFamily primaryFamily,
        EnemyAiActionFamily secondaryFamily,
        EnemyAiDistanceReference fallbackReference
    )
    {
        EnemyAiActionDefinition template =
            FindActionByFamily(stateActions, primaryFamily)
            ?? FindActionByFamily(stateActions, secondaryFamily);
        if (template != null)
        {
            return new DistanceStyle(
                GetDesiredMinDistance(template),
                GetDesiredMaxDistance(template),
                GetDistanceReference(template)
            );
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        int minimum = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        return new DistanceStyle(
            minimum,
            Math.Max(effectiveRange, minimum),
            fallbackReference
        );
    }

    private static DistanceStyle ResolveGroundDistanceStyle(
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        SkillDefinition skillDefinition
    )
    {
        EnemyAiActionDefinition template = FindActionByFamily(
            stateActions,
            EnemyAiActionFamily.UseGroundSkill
        );
        if (template != null)
        {
            return new DistanceStyle(
                GetDesiredMinDistance(template),
                GetDesiredMaxDistance(template),
                GetDistanceReference(template)
            );
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        return new DistanceStyle(
            0,
            Math.Max(effectiveRange, 0),
            EnemyAiDistanceReference.TargetCoord
        );
    }

    private static EnemyAiActionDefinition ApplySlotOverrides(
        EnemyAiActionDefinition action,
        EnemyAiGenerationSlotDefinition slot,
        IReadOnlyList<EnemyAiActionDefinition> stateActions
    )
    {
        if (action == null || slot == null)
            return action;

        EnemyAiActionDefinition template = FindActionById(
            stateActions,
            slot.StyleTemplateActionId
        );
        StringName scoreBucketId = slot.ScoreBucketId != ""
            ? slot.ScoreBucketId
            : action.ScoreBucketId == "" && template != null
                ? template.ScoreBucketId
                : action.ScoreBucketId;

        StringName actionIntent = action.ActionIntent;
        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.SlotRole);
        if (BattleAiActionIntent.IsValid(slotIntent))
            actionIntent = slotIntent;
        else if (
            template != null
            && BattleAiActionIntent.IsValid(template.ActionIntent)
            && template.ActionIntent != BattleAiActionIntent.Positioning
        )
            actionIntent = template.ActionIntent;

        StringName targetSelector = GetTargetSelector(action);
        if (slot.TargetSelector != "")
            targetSelector = slot.TargetSelector;
        else if (template != null && GetTargetSelector(template) != "")
            targetSelector = GetTargetSelector(template);

        int desiredMinDistance = GetDesiredMinDistance(action, -1);
        int desiredMaxDistance = GetDesiredMaxDistance(action, -1);
        if (slot.DesiredMinDistance >= 0)
            desiredMinDistance = slot.DesiredMinDistance;
        if (slot.DesiredMaxDistance >= 0)
            desiredMaxDistance = slot.DesiredMaxDistance;

        EnemyAiDistanceReference distanceReference = GetDistanceReference(action);
        if (
            slot.DistanceReferenceKind is not EnemyAiDistanceReference.None
            and not EnemyAiDistanceReference.Unknown
        )
            distanceReference = slot.DistanceReferenceKind;
        if (action.Kind == EnemyAiActionKind.UseRandomChainSkill)
        {
            distanceReference = distanceReference is EnemyAiDistanceReference.CandidatePool
                or EnemyAiDistanceReference.EnemyFrontline
                ? distanceReference
                : EnemyAiDistanceReference.CandidatePool;
        }

        return CopyWithOverrides(
            action,
            scoreBucketId,
            actionIntent,
            targetSelector,
            desiredMinDistance,
            desiredMaxDistance,
            EnemyAiDistanceReferences.ToStringName(distanceReference)
        );
    }

    private static EnemyAiActionDefinition CopyWithOverrides(
        EnemyAiActionDefinition action,
        StringName scoreBucketId,
        StringName actionIntent,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference
    ) =>
        action switch
        {
            UseUnitSkillActionDefinition value => new UseUnitSkillActionDefinition(
                value.ActionId,
                scoreBucketId,
                actionIntent,
                value.SkillIds,
                targetSelector,
                value.MinimumEffectiveTargetCount,
                value.MaximumFriendlyFireTargetCount,
                value.AllowFriendlyLethal,
                desiredMinDistance,
                desiredMaxDistance,
                distanceReference
            ),
            UseGroundSkillActionDefinition value => new UseGroundSkillActionDefinition(
                value.ActionId,
                scoreBucketId,
                actionIntent,
                value.SkillIds,
                value.MinimumHitCount,
                value.AllowEmptyGroundControl,
                value.AllowGroundControlSupplementPartialHits,
                value.MinimumGroundControlScore,
                value.MinimumAllyThreatHitCount,
                value.MaximumFriendlyFireTargetCount,
                value.AllowFriendlyLethal,
                value.ThreatMinimumSafeDistance,
                value.ThreatSafeDistanceMargin,
                desiredMinDistance,
                desiredMaxDistance,
                distanceReference
            ),
            UseMultiUnitSkillActionDefinition value => new UseMultiUnitSkillActionDefinition(
                value.ActionId,
                scoreBucketId,
                actionIntent,
                value.SkillIds,
                targetSelector,
                desiredMinDistance,
                desiredMaxDistance,
                distanceReference,
                value.CandidatePoolLimit,
                value.CandidateGroupLimit
            ),
            MoveToMultiUnitSkillPositionActionDefinition value =>
                new MoveToMultiUnitSkillPositionActionDefinition(
                    value.ActionId,
                    scoreBucketId,
                    actionIntent,
                    value.SkillIds,
                    targetSelector,
                    desiredMinDistance,
                    desiredMaxDistance,
                    distanceReference,
                    value.CandidatePoolLimit,
                    value.CandidateGroupLimit,
                    value.TargetCountWeight
                ),
            UseRandomChainSkillActionDefinition value =>
                new UseRandomChainSkillActionDefinition(
                    value.ActionId,
                    scoreBucketId,
                    actionIntent,
                    value.SkillIds,
                    targetSelector,
                    desiredMinDistance,
                    desiredMaxDistance,
                    distanceReference,
                    value.MinimumCandidateCount
                ),
            UseChargeActionDefinition value => new UseChargeActionDefinition(
                value.ActionId,
                scoreBucketId,
                actionIntent,
                value.SkillId,
                targetSelector,
                value.MinimumChargeMoveDistance
            ),
            UseChargePathAoeActionDefinition value =>
                new UseChargePathAoeActionDefinition(
                    value.ActionId,
                    scoreBucketId,
                    actionIntent,
                    value.SkillIds,
                    targetSelector,
                    value.MinimumHitCount,
                    desiredMinDistance,
                    desiredMaxDistance
                ),
            MoveToRangeActionDefinition value => new MoveToRangeActionDefinition(
                value.ActionId,
                scoreBucketId,
                actionIntent,
                value.AiEvaluationMode,
                targetSelector,
                desiredMinDistance,
                desiredMaxDistance,
                value.RangeSkillIds,
                value.ScreeningMode,
                value.EnableAoeSetupPositioning,
                value.AoeSetupMinTargetCount,
                value.AoeSetupTargetCountWeight,
                value.AoeSetupImprovementWeight,
                value.AoeSetupFriendlyFirePenalty,
                value.ScreeningMinHpBasisPoints,
                value.ScreeningAllyMinAttackRange,
                value.ScreeningEnemyMaxContactRange,
                value.ScreeningThreatDistanceBuffer,
                value.ScreeningPathBonus
            ),
            _ => action,
        };

    private static StringName ResolveGeneratedScoreBucketId(
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        EnemyAiActionFamily preferredFamily
    )
    {
        EnemyAiActionDefinition preferred = FindActionByFamily(
            stateActions,
            preferredFamily
        );
        if (preferred != null && preferred.ScoreBucketId != "")
            return preferred.ScoreBucketId;
        foreach (EnemyAiActionDefinition action in stateActions)
        {
            if (
                action != null
                && action.ScoreBucketId != ""
                && action.DeclaredSkillIds.Count > 0
            )
                return action.ScoreBucketId;
        }
        return "";
    }

    private static StringName ResolveTargetSelector(
        IReadOnlyList<EnemyAiActionDefinition> stateActions,
        StringName fallback
    )
    {
        foreach (EnemyAiActionDefinition action in stateActions)
        {
            StringName selector = GetTargetSelector(action);
            if (selector != "")
                return selector;
        }
        return fallback;
    }

    private static StringName ResolveGeneratedActionIntent(
        EnemyAiGenerationSlotDefinition slot,
        SkillDefinition skillDefinition,
        EnemyAiActionFamily family
    )
    {
        if (slot.SlotRoleKind == EnemyAiGenerationSlotRole.Support)
            return BattleAiActionIntent.InferForSkill(skillDefinition);
        if (slot.SlotRoleKind == EnemyAiGenerationSlotRole.Engage)
            return BattleAiActionIntent.Offense;
        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.SlotRole);
        if (BattleAiActionIntent.IsValid(slotIntent))
            return slotIntent;
        return family switch
        {
            EnemyAiActionFamily.MoveToRange
            or EnemyAiActionFamily.MoveToMultiUnitSkillPosition =>
                BattleAiActionIntent.Positioning,
            EnemyAiActionFamily.UseCharge or EnemyAiActionFamily.UseChargePathAoe =>
                BattleAiActionIntent.Offense,
            _ => BattleAiActionIntent.InferForSkill(skillDefinition),
        };
    }

    private static EnemyAiActionDefinition FindActionById(
        IReadOnlyList<EnemyAiActionDefinition> actions,
        StringName actionId
    )
    {
        foreach (EnemyAiActionDefinition action in actions ?? Array.Empty<EnemyAiActionDefinition>())
        {
            if (action?.ActionId == actionId)
                return action;
        }
        return null;
    }

    private static EnemyAiActionDefinition FindActionByFamily(
        IReadOnlyList<EnemyAiActionDefinition> actions,
        EnemyAiActionFamily family
    )
    {
        foreach (EnemyAiActionDefinition action in actions ?? Array.Empty<EnemyAiActionDefinition>())
        {
            if (action != null && GetActionFamily(action) == family)
                return action;
        }
        return null;
    }

    private static EnemyAiActionFamily GetActionFamily(EnemyAiActionDefinition action) =>
        action?.Kind switch
        {
            EnemyAiActionKind.UseUnitSkill => EnemyAiActionFamily.UseUnitSkill,
            EnemyAiActionKind.UseGroundSkill => EnemyAiActionFamily.UseGroundSkill,
            EnemyAiActionKind.UseMultiUnitSkill => EnemyAiActionFamily.UseMultiUnitSkill,
            EnemyAiActionKind.MoveToMultiUnitSkillPosition =>
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition,
            EnemyAiActionKind.UseRandomChainSkill => EnemyAiActionFamily.UseRandomChainSkill,
            EnemyAiActionKind.UseCharge => EnemyAiActionFamily.UseCharge,
            EnemyAiActionKind.UseChargePathAoe => EnemyAiActionFamily.UseChargePathAoe,
            EnemyAiActionKind.MoveToRange => EnemyAiActionFamily.MoveToRange,
            _ => EnemyAiActionFamily.Unknown,
        };

    private static EnemyAiActionFamily ToActionFamily(StringName value) =>
        value.ToString() switch
        {
            "use_unit_skill" => EnemyAiActionFamily.UseUnitSkill,
            "use_ground_skill" => EnemyAiActionFamily.UseGroundSkill,
            "use_multi_unit_skill" => EnemyAiActionFamily.UseMultiUnitSkill,
            "use_random_chain_skill" => EnemyAiActionFamily.UseRandomChainSkill,
            "use_charge" => EnemyAiActionFamily.UseCharge,
            "use_charge_path_aoe" => EnemyAiActionFamily.UseChargePathAoe,
            "move_to_range" => EnemyAiActionFamily.MoveToRange,
            "move_to_multi_unit_skill_position" =>
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition,
            _ => EnemyAiActionFamily.Unknown,
        };

    private static StringName GetTargetSelector(EnemyAiActionDefinition action) =>
        action switch
        {
            UseUnitSkillActionDefinition value => value.TargetSelector,
            UseMultiUnitSkillActionDefinition value => value.TargetSelector,
            MoveToMultiUnitSkillPositionActionDefinition value => value.TargetSelector,
            UseRandomChainSkillActionDefinition value => value.TargetSelector,
            UseGroundRepositionSkillActionDefinition value => value.TargetSelector,
            RetreatActionDefinition value => value.TargetSelector,
            UseChargePathAoeActionDefinition value => value.TargetSelector,
            UseChargeActionDefinition value => value.TargetSelector,
            MoveToRangeActionDefinition value => value.TargetSelector,
            MoveToAdvantagePositionActionDefinition value => value.TargetSelector,
            _ => "",
        };

    private static int GetDesiredMinDistance(
        EnemyAiActionDefinition action,
        int fallback = 0
    ) =>
        action switch
        {
            UseUnitSkillActionDefinition value => value.DesiredMinDistance,
            UseGroundSkillActionDefinition value => value.DesiredMinDistance,
            UseMultiUnitSkillActionDefinition value => value.DesiredMinDistance,
            MoveToMultiUnitSkillPositionActionDefinition value => value.DesiredMinDistance,
            UseRandomChainSkillActionDefinition value => value.DesiredMinDistance,
            MoveToRangeActionDefinition value => value.DesiredMinDistance,
            UseChargePathAoeActionDefinition value => value.DesiredMinDistance,
            MoveToAdvantagePositionActionDefinition value => value.DesiredMinDistance,
            _ => fallback,
        };

    private static int GetDesiredMaxDistance(
        EnemyAiActionDefinition action,
        int fallback = 0
    ) =>
        action switch
        {
            UseUnitSkillActionDefinition value => value.DesiredMaxDistance,
            UseGroundSkillActionDefinition value => value.DesiredMaxDistance,
            UseMultiUnitSkillActionDefinition value => value.DesiredMaxDistance,
            MoveToMultiUnitSkillPositionActionDefinition value => value.DesiredMaxDistance,
            UseRandomChainSkillActionDefinition value => value.DesiredMaxDistance,
            MoveToRangeActionDefinition value => value.DesiredMaxDistance,
            UseChargePathAoeActionDefinition value => value.DesiredMaxDistance,
            MoveToAdvantagePositionActionDefinition value => value.DesiredMaxDistance,
            _ => fallback,
        };

    private static EnemyAiDistanceReference GetDistanceReference(
        EnemyAiActionDefinition action
    ) =>
        action switch
        {
            UseUnitSkillActionDefinition value =>
                EnemyAiDistanceReferences.ToKind(value.DistanceReference),
            UseGroundSkillActionDefinition value =>
                EnemyAiDistanceReferences.ToKind(value.DistanceReference),
            UseMultiUnitSkillActionDefinition value =>
                EnemyAiDistanceReferences.ToKind(value.DistanceReference),
            MoveToMultiUnitSkillPositionActionDefinition value =>
                EnemyAiDistanceReferences.ToKind(value.DistanceReference),
            UseRandomChainSkillActionDefinition value =>
                EnemyAiDistanceReferences.ToKind(value.DistanceReference),
            _ => EnemyAiDistanceReference.None,
        };

    private static StringName BuildRuntimeActionId(
        StringName stateId,
        StringName slotId,
        StringName skillId,
        StringName actionFamily
    ) => new($"auto_{stateId}_{slotId}_{skillId}_{actionFamily}");

    private static string BuildIdentityKey(
        StringName stateId,
        StringName slotId,
        StringName skillId,
        StringName actionFamily
    ) => $"{stateId}/{slotId}/{skillId}/{actionFamily}";

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    public bool IsOffensiveOrEnemySkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        if (combatProfile.SpecialResolutionProfileId == (StringName)"meteor_swarm")
            return true;
        if (BattleTargetTeamRules.IsEnemyFilter(combatProfile.TargetTeamFilter))
            return true;

        foreach (CombatEffectDefinition effect in combatProfile.EffectDefinitions)
        {
            if (IsOffensiveEffect(skillDefinition, effect))
                return true;
        }
        foreach (CombatCastVariantDefinition variant in combatProfile.CastVariants)
        {
            if (variant == null)
                continue;
            foreach (CombatEffectDefinition effect in variant.EffectDefinitions)
            {
                if (IsOffensiveEffect(skillDefinition, effect))
                    return true;
            }
        }
        return false;
    }

    private static bool IsOffensiveEffect(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effect
    )
    {
        if (effect == null)
            return false;
        StringName effectFilter = effect.EffectTargetTeamFilter;
        StringName skillFilter = skillDefinition?.CombatProfile?.TargetTeamFilter ?? "";
        if (BattleTargetTeamRules.IsEnemyFilter(effectFilter))
            return true;
        if (BattleTargetTeamRules.IsBeneficialFilter(effectFilter))
            return false;
        if (effectFilter == "" && BattleTargetTeamRules.IsBeneficialFilter(skillFilter))
            return false;
        if (
            effect.EffectKind is BattleEffectKind.Damage or BattleEffectKind.PathStepAoe
        )
            return !BattleTargetTeamRules.IsBeneficialFilter(skillFilter);
        if (
            effect.EffectKind
                is BattleEffectKind.Status
                    or BattleEffectKind.ApplyStatus
                    or BattleEffectKind.ForcedMove
        )
            return true;
        return effect.StatusId != "" || effect.SaveFailureStatusId != "";
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (skillDefinitions == null || skillId == "")
            return null;
        return skillDefinitions.TryGetValue(skillId, out SkillDefinition definition)
            ? definition
            : null;
    }

    private readonly record struct DistanceStyle(
        int Minimum,
        int Maximum,
        EnemyAiDistanceReference Reference
    );
}

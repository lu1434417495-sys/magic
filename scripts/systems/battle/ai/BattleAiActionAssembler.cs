using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiActionAssembler
{
    private readonly BattleAiSkillAffordanceClassifier _classifier = new();

    public BattleAiRuntimeActionPlan BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDef brain,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var plan = new BattleAiRuntimeActionPlan();
        try
        {
            if (unitState == null || brain == null)
            {
                return plan;
            }

        using BattleAiTraceSpan trace = new("build_unit_action_plan");
        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
        plan.SetSource(unitState, brain);
        List<BattleAiSkillAffordanceRecord> skillRecords = ClassifyKnownActiveSkills(
            unitState,
            skillDefinitions
        );
        foreach (BattleAiSkillAffordanceRecord record in skillRecords)
        {
            plan.SetSkillAffordanceRecordTyped(record);
        }

        foreach (EnemyAiStateDef stateDef in GetBrainStates(brain))
        {
            if (stateDef == null)
            {
                continue;
            }

            StringName stateId = stateDef.state_id;
            List<EnemyAiAction> authoredActions = GetActions(stateDef);
            List<EnemyAiAction> runtimeActions = CloneRuntimeActions(plan, authoredActions);
            plan.AddStateActions(stateId, runtimeActions);

            List<EnemyAiGenerationSlotDef> generationSlots = GetGenerationSlots(stateDef);
            foreach (EnemyAiGenerationSlotDef slot in generationSlots)
            {
                if (slot == null)
                {
                    continue;
                }

                foreach (BattleAiSkillAffordanceRecord record in skillRecords)
                {
                    if (!record.is_generatable)
                    {
                        continue;
                    }

                        StringName skillId = record.skill_id;
                    SkillDefinition skillDefinition = GetSkillDefinition(
                        skillDefinitions,
                        skillId
                    );
                    if (skillDefinition == null)
                    {
                        continue;
                    }

                    foreach (StringName actionFamily in record.action_families)
                    {
                        if (
                            actionFamily == ""
                            || !SlotMatchesAffordance(slot, record, actionFamily)
                        )
                        {
                            continue;
                        }
                        if (
                            IsGenerationSuppressed(
                                plan,
                                stateDef,
                                runtimeActions,
                                slot,
                                skillId,
                                actionFamily
                            )
                        )
                        {
                            continue;
                        }

                        EnemyAiActionFamily actionFamilyKind =
                            EnemyAiGenerationSlotDef.ToActionFamily(actionFamily);
                        if (actionFamilyKind == EnemyAiActionFamily.UseUnitSkill)
                        {
                            BattleAiUnitSkillActionSpec generatedUnitAction =
                                BuildUnitRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedUnitAction == null)
                            {
                                continue;
                            }
                            generatedUnitAction.ActionIntent = ResolveGeneratedActionIntent(
                                slot,
                                skillDefinition,
                                actionFamily
                            );
                            ApplySlotOverrides(generatedUnitAction, slot, runtimeActions);
                            generatedUnitAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedUnitIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedUseUnitSkillActionTyped(
                                stateId,
                                generatedUnitAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedUnitIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.MoveToRange)
                        {
                            BattleAiGeneratedMoveToRangeAction generatedMoveAction =
                                BuildMoveToRangeRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedMoveAction == null)
                            {
                                continue;
                            }
                            generatedMoveAction.ActionIntent = ResolveGeneratedActionIntent(
                                slot,
                                skillDefinition,
                                actionFamily
                            );
                            ApplySlotOverrides(generatedMoveAction, slot, runtimeActions);
                            generatedMoveAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedMoveIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedMoveToRangeActionTyped(
                                stateId,
                                generatedMoveAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedMoveIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.UseRandomChainSkill)
                        {
                            BattleAiRandomChainSkillActionSpec generatedRandomChainAction =
                                BuildRandomChainRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedRandomChainAction == null)
                            {
                                continue;
                            }
                            generatedRandomChainAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(generatedRandomChainAction, slot, runtimeActions);
                            generatedRandomChainAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedRandomChainIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedRandomChainSkillActionTyped(
                                stateId,
                                generatedRandomChainAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedRandomChainIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.UseMultiUnitSkill)
                        {
                            BattleAiMultiUnitSkillActionSpec generatedMultiUnitAction =
                                BuildMultiUnitRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedMultiUnitAction == null)
                            {
                                continue;
                            }
                            generatedMultiUnitAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(generatedMultiUnitAction, slot, runtimeActions);
                            generatedMultiUnitAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedMultiUnitIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedMultiUnitSkillActionTyped(
                                stateId,
                                generatedMultiUnitAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedMultiUnitIdentityKey
                            );
                            continue;
                        }

                        if (
                            actionFamilyKind
                            == EnemyAiActionFamily.MoveToMultiUnitSkillPosition
                        )
                        {
                            BattleAiMoveToMultiUnitSkillPositionActionSpec generatedMoveToMultiUnitAction =
                                BuildMoveToMultiUnitRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedMoveToMultiUnitAction == null)
                            {
                                continue;
                            }
                            generatedMoveToMultiUnitAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(
                                generatedMoveToMultiUnitAction,
                                slot,
                                runtimeActions
                            );
                            generatedMoveToMultiUnitAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedMoveToMultiUnitIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedMoveToMultiUnitSkillPositionActionTyped(
                                stateId,
                                generatedMoveToMultiUnitAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedMoveToMultiUnitIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.UseCharge)
                        {
                            BattleAiChargeActionSpec generatedChargeAction =
                                BuildChargeRuntimeAction(
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedChargeAction == null)
                            {
                                continue;
                            }
                            generatedChargeAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(generatedChargeAction, slot, runtimeActions);
                            generatedChargeAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedChargeIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedChargeActionTyped(
                                stateId,
                                generatedChargeAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedChargeIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.UseChargePathAoe)
                        {
                            BattleAiChargePathAoeActionSpec generatedChargePathAoeAction =
                                BuildChargePathAoeRuntimeAction(
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedChargePathAoeAction == null)
                            {
                                continue;
                            }
                            generatedChargePathAoeAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(
                                generatedChargePathAoeAction,
                                slot,
                                runtimeActions
                            );
                            generatedChargePathAoeAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedChargePathAoeIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedChargePathAoeActionTyped(
                                stateId,
                                generatedChargePathAoeAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedChargePathAoeIdentityKey
                            );
                            continue;
                        }

                        if (actionFamilyKind == EnemyAiActionFamily.UseGroundSkill)
                        {
                            BattleAiGroundSkillActionSpec generatedGroundAction =
                                BuildGroundRuntimeAction(
                                    unitState,
                                    stateDef,
                                    runtimeActions,
                                    skillDefinition
                                );
                            if (generatedGroundAction == null)
                            {
                                continue;
                            }
                            generatedGroundAction.ActionIntent =
                                ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily);
                            ApplySlotOverrides(generatedGroundAction, slot, runtimeActions);
                            generatedGroundAction.ActionId = BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            string generatedGroundIdentityKey = BuildIdentityKey(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            );
                            plan.AddGeneratedGroundSkillActionTyped(
                                stateId,
                                generatedGroundAction,
                                slot.slot_id,
                                slot.slot_role,
                                skillId,
                                actionFamily,
                                slot.style_template_action_id,
                                generatedGroundIdentityKey
                            );
                            continue;
                        }

                        EnemyAiAction generatedAction = BuildSkillActionForFamily(
                            plan,
                            unitState,
                            stateDef,
                            runtimeActions,
                            skillDefinition,
                            actionFamily
                        );
                        if (generatedAction == null)
                        {
                            continue;
                        }
                        generatedAction = plan.OwnRuntimeAction(
                            generatedAction,
                            $"generated_action:{stateId}:{slot.slot_id}:{skillId}:{actionFamily}"
                        );

                        SetActionIntent(
                            generatedAction,
                            ResolveGeneratedActionIntent(slot, skillDefinition, actionFamily)
                        );
                        ApplySlotOverrides(generatedAction, slot, runtimeActions);
                        SetActionId(
                            generatedAction,
                            BuildRuntimeActionId(
                                stateId,
                                slot.slot_id,
                                skillId,
                                actionFamily
                            )
                        );
                        string identityKey = BuildIdentityKey(
                            stateId,
                            slot.slot_id,
                            skillId,
                            actionFamily
                        );
                        plan.AddGeneratedActionTyped(
                            stateId,
                            generatedAction,
                            slot.slot_id,
                            slot.slot_role,
                            skillId,
                            actionFamily,
                            slot.style_template_action_id,
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

    private static IEnumerable<EnemyAiStateDef> GetBrainStates(EnemyAiBrainDef brain)
    {
        if (brain == null)
            return System.Array.Empty<EnemyAiStateDef>();
        Godot.Collections.Array<EnemyAiStateDef> states = brain.GetResolvedStates();
        return states != null ? states : System.Array.Empty<EnemyAiStateDef>();
    }

    private List<BattleAiSkillAffordanceRecord> ClassifyKnownActiveSkills(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var records = new List<BattleAiSkillAffordanceRecord>();
        BattleSkillAvailabilityService availabilityService = new(skillDefinitions);
        BattleSkillAvailabilityView availabilityView = availabilityService.BuildView(
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
            if (skillId == "")
            {
                continue;
            }
            SkillDefinition skillDefinition = entry.SkillDefinition ?? GetSkillDefinition(skillDefinitions, skillId);
            if (skillDefinition == null)
            {
                continue;
            }
            BattleAiSkillAffordanceRecord record = _classifier.ClassifySkill(
                skillDefinition,
                entry.SkillLevel
            );
            if (record.skill_id == "")
            {
                record.skill_id = skillId;
            }
            records.Add(record);
        }
        return records;
    }

    private static List<EnemyAiAction> GetActions(EnemyAiStateDef stateDef)
    {
        return stateDef?.GetTypedActions() ?? new List<EnemyAiAction>();
    }

    private static List<EnemyAiGenerationSlotDef> GetGenerationSlots(EnemyAiStateDef stateDef)
    {
        return SortGenerationSlots(
            stateDef?.GetTypedGenerationSlots() ?? new List<EnemyAiGenerationSlotDef>()
        );
    }

    private static List<EnemyAiGenerationSlotDef> SortGenerationSlots(
        List<EnemyAiGenerationSlotDef> slots
    )
    {
        slots ??= new List<EnemyAiGenerationSlotDef>();
        slots.Sort(
            (left, right) =>
            {
                int leftOrder = left.order;
                int rightOrder = right.order;
                if (leftOrder != rightOrder)
                {
                    return leftOrder.CompareTo(rightOrder);
                }
                return left.slot_id.ToString().CompareTo(right.slot_id.ToString());
            }
        );
        return slots;
    }

    private static List<EnemyAiAction> CloneRuntimeActions(
        BattleAiRuntimeActionPlan plan,
        List<EnemyAiAction> authoredActions
    )
    {
        var runtimeActions = new List<EnemyAiAction>();
        foreach (EnemyAiAction action in authoredActions ?? new List<EnemyAiAction>())
        {
            if (action == null)
            {
                continue;
            }
            if (plan == null)
            {
                LifecycleViolation.Report(
                    "BattleAiActionAssembler.CloneRuntimeActions requires a runtime action plan owner."
                );
            }
            runtimeActions.Add(action);
        }
        return runtimeActions;
    }

    private static bool SlotMatchesAffordance(
        EnemyAiGenerationSlotDef slot,
        BattleAiSkillAffordanceRecord record,
        StringName actionFamily
    )
    {
        if (
            slot == null
            || record == null
        )
        {
            return false;
        }
        return slot.MatchesAffordance(record, actionFamily);
    }

    private static EnemyAiAction BuildSkillActionForFamily(
        BattleAiRuntimeActionPlan plan,
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition,
        StringName actionFamily
    )
    {
        if (unitState == null || stateDef == null || skillDefinition?.CombatProfile == null)
        {
            return null;
        }
        return EnemyAiGenerationSlotDef.ToActionFamily(actionFamily) switch
        {
            _ => null,
        };
    }

    private static bool IsGenerationSuppressed(
        BattleAiRuntimeActionPlan plan,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        EnemyAiGenerationSlotDef slot,
        StringName skillId,
        StringName actionFamily
    )
    {
        if (slot.SuppressionPolicyKind == EnemyAiGenerationSuppressionPolicy.ManualOnly)
        {
            return true;
        }

        StringName stateId = stateDef.state_id;
        EnemyAiActionFamily actionFamilyKind = EnemyAiGenerationSlotDef.ToActionFamily(
            actionFamily
        );
        string identityKey = BuildIdentityKey(stateId, slot.slot_id, skillId, actionFamily);
        if (plan.HasActionIdentityKey(identityKey))
        {
            return true;
        }
        foreach (EnemyAiAction existingAction in plan.GetActions(stateId))
        {
            if (existingAction == null)
            {
                continue;
            }
            if (plan.HasActionIdentityKey(existingAction, identityKey))
            {
                return true;
            }
        }

        foreach (EnemyAiAction authoredAction in stateActions)
        {
            if (authoredAction == null)
            {
                continue;
            }
            if (!authoredAction.GetDeclaredSkillIds().Contains(skillId))
            {
                continue;
            }
            if (GetActionFamilyForAction(authoredAction) == actionFamilyKind)
            {
                return true;
            }
        }
        return false;
    }

    private static EnemyAiActionFamily GetActionFamilyForAction(EnemyAiAction action)
    {
        return action switch
        {
            UseUnitSkillAction => EnemyAiActionFamily.UseUnitSkill,
            UseGroundSkillAction => EnemyAiActionFamily.UseGroundSkill,
            MoveToMultiUnitSkillPositionAction =>
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition,
            UseMultiUnitSkillAction => EnemyAiActionFamily.UseMultiUnitSkill,
            UseRandomChainSkillAction => EnemyAiActionFamily.UseRandomChainSkill,
            UseChargePathAoeAction => EnemyAiActionFamily.UseChargePathAoe,
            UseChargeAction => EnemyAiActionFamily.UseCharge,
            MoveToRangeAction => EnemyAiActionFamily.MoveToRange,
            _ => EnemyAiActionFamily.Unknown,
        };
    }

    private static void ApplySlotOverrides(
        EnemyAiAction action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            SetScoreBucket(action, slotBucket);
        }
        else if (GetScoreBucket(action) == "" && templateAction != null)
        {
            SetScoreBucket(action, GetScoreBucket(templateAction));
        }
        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            SetActionIntent(action, slotIntent);
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            SetActionIntent(action, GetActionIntent(templateAction));
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            SetTargetSelectorIfSupported(action, slotSelector);
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                SetTargetSelectorIfSupported(action, templateSelector);
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            SetIntIfSupported(action, "desired_min_distance", minDistance);
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            SetIntIfSupported(action, "desired_max_distance", maxDistance);
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            SetDistanceReferenceIfSupported(action, distanceReference);
        }
    }

    private static void ApplySlotOverrides(
        BattleAiGeneratedMoveToRangeAction action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiUnitSkillActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            action.DistanceReferenceKind = distanceReference;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiRandomChainSkillActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            action.DistanceReferenceKind =
                distanceReference == EnemyAiDistanceReference.CandidatePool
                || distanceReference == EnemyAiDistanceReference.EnemyFrontline
                    ? distanceReference
                    : EnemyAiDistanceReference.CandidatePool;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiMultiUnitSkillActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            action.DistanceReferenceKind = distanceReference;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiMoveToMultiUnitSkillPositionActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            action.DistanceReferenceKind = distanceReference;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiChargeActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }
    }

    private static void ApplySlotOverrides(
        BattleAiChargePathAoeActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        StringName slotSelector = slot.target_selector;
        if (slotSelector != "")
        {
            action.TargetSelector = slotSelector;
        }
        else if (templateAction != null)
        {
            StringName templateSelector = GetTargetSelector(templateAction);
            if (templateSelector != "")
            {
                action.TargetSelector = templateSelector;
            }
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
    }

    private static void ApplySlotOverrides(
        BattleAiGroundSkillActionSpec action,
        EnemyAiGenerationSlotDef slot,
        IReadOnlyList<EnemyAiAction> stateActions
    )
    {
        if (action == null || slot == null)
        {
            return;
        }

        EnemyAiAction templateAction = FindActionById(
            stateActions,
            slot.style_template_action_id
        );
        StringName slotBucket = slot.score_bucket_id;
        if (slotBucket != "")
        {
            action.ScoreBucketId = slotBucket;
        }
        else if (action.ScoreBucketId == "" && templateAction != null)
        {
            action.ScoreBucketId = GetScoreBucket(templateAction);
        }

        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot.slot_role);
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            action.ActionIntent = slotIntent;
        }
        else if (
            templateAction != null
            && BattleAiActionIntent.IsValid(GetActionIntent(templateAction))
            && GetActionIntent(templateAction) != BattleAiActionIntent.Positioning
        )
        {
            action.ActionIntent = GetActionIntent(templateAction);
        }

        int minDistance = slot.desired_min_distance;
        if (minDistance >= 0)
        {
            action.DesiredMinDistance = minDistance;
        }
        int maxDistance = slot.desired_max_distance;
        if (maxDistance >= 0)
        {
            action.DesiredMaxDistance = maxDistance;
        }
        EnemyAiDistanceReference distanceReference = slot.DistanceReferenceKind;
        if (
            distanceReference != EnemyAiDistanceReference.None
            && distanceReference != EnemyAiDistanceReference.Unknown
        )
        {
            action.DistanceReferenceKind = distanceReference;
        }
    }

    private static EnemyAiAction FindActionById(
        IReadOnlyList<EnemyAiAction> stateActions,
        StringName actionId
    )
    {
        foreach (EnemyAiAction action in stateActions ?? System.Array.Empty<EnemyAiAction>())
        {
            if (action != null && GetActionId(action) == actionId)
            {
                return action;
            }
        }
        return null;
    }

    private static EnemyAiAction FindActionByFamily(
        IReadOnlyList<EnemyAiAction> stateActions,
        EnemyAiActionFamily actionFamily
    )
    {
        foreach (EnemyAiAction action in stateActions ?? System.Array.Empty<EnemyAiAction>())
        {
            if (action != null && GetActionFamilyForAction(action) == actionFamily)
            {
                return action;
            }
        }
        return null;
    }

    private static StringName BuildRuntimeActionId(
        StringName stateId,
        StringName slotId,
        StringName skillId,
        StringName actionFamily
    )
    {
        return new StringName($"auto_{stateId}_{slotId}_{skillId}_{actionFamily}");
    }

    private static string BuildIdentityKey(
        StringName stateId,
        StringName slotId,
        StringName skillId,
        StringName actionFamily
    )
    {
        return $"{stateId}/{slotId}/{skillId}/{actionFamily}";
    }

    private static StringName ResolveGeneratedActionIntent(
        EnemyAiGenerationSlotDef slot,
        SkillDefinition skillDefinition,
        StringName actionFamily
    )
    {
        EnemyAiGenerationSlotRole slotRole = slot?.SlotRoleKind ?? EnemyAiGenerationSlotRole.Unknown;
        if (slotRole == EnemyAiGenerationSlotRole.Support)
        {
            return BattleAiActionIntent.InferForSkill(skillDefinition);
        }
        if (slotRole == EnemyAiGenerationSlotRole.Engage)
        {
            return BattleAiActionIntent.Offense;
        }
        StringName slotIntent = BattleAiActionIntent.DefaultFromSlotRole(slot?.slot_role ?? "");
        if (BattleAiActionIntent.IsValid(slotIntent))
        {
            return slotIntent;
        }
        return EnemyAiGenerationSlotDef.ToActionFamily(actionFamily) switch
        {
            EnemyAiActionFamily.MoveToRange
            or EnemyAiActionFamily.MoveToMultiUnitSkillPosition =>
                BattleAiActionIntent.Positioning,
            EnemyAiActionFamily.UseCharge
            or EnemyAiActionFamily.UseChargePathAoe => BattleAiActionIntent.Offense,
            _ => BattleAiActionIntent.InferForSkill(skillDefinition),
        };
    }

    private static BattleAiChargePathAoeActionSpec BuildChargePathAoeRuntimeAction(
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        return new BattleAiChargePathAoeActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "path_aoe"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseCharge
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
            MinimumHitCount = 1,
            DesiredMinDistance = 1,
            DesiredMaxDistance = 1,
        };
    }

    private static BattleAiChargeActionSpec BuildChargeRuntimeAction(
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        return new BattleAiChargeActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "charge"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseCharge
            ),
            SkillId = skillDefinition.SkillId,
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            MinimumChargeMoveDistance = 3,
        };
    }

    private static BattleAiGroundSkillActionSpec BuildGroundRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        var action = new BattleAiGroundSkillActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "ground"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseGroundSkill
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
            MinimumHitCount = Mathf.Max(skillDefinition.CombatProfile?.MinTargetCount ?? 0, 1),
        };
        ApplyGroundDistanceStyle(action, unitState, stateActions, skillDefinition);
        return action;
    }

    private static BattleAiUnitSkillActionSpec BuildUnitRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        var action = new BattleAiUnitSkillActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "unit"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseUnitSkill
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
            MinimumEffectiveTargetCount = 1,
            MaximumFriendlyFireTargetCount = 0,
            AllowFriendlyLethal = false,
        };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDefinition);
        return action;
    }

    private static BattleAiMultiUnitSkillActionSpec BuildMultiUnitRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        var action = new BattleAiMultiUnitSkillActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "multi"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseMultiUnitSkill
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
        };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDefinition);
        return action;
    }

    private static BattleAiRandomChainSkillActionSpec BuildRandomChainRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        var action = new BattleAiRandomChainSkillActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "random_chain"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseUnitSkill
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
        };
        ApplyRandomChainDistanceStyle(action, unitState, stateActions, skillDefinition);
        return action;
    }

    private static BattleAiGeneratedMoveToRangeAction BuildMoveToRangeRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        int minDistance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        return new BattleAiGeneratedMoveToRangeAction
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "range_move"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.MoveToRange
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            RangeSkillIds = new List<StringName> { skillDefinition.SkillId },
            DesiredMinDistance = minDistance,
            DesiredMaxDistance = Math.Max(effectiveRange, minDistance),
        };
    }

    private static BattleAiMoveToMultiUnitSkillPositionActionSpec BuildMoveToMultiUnitRuntimeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        var action = new BattleAiMoveToMultiUnitSkillPositionActionSpec
        {
            ActionId = BuildActionId(
                stateDef.state_id,
                skillDefinition.SkillId,
                "multi_move"
            ),
            ScoreBucketId = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition
            ),
            TargetSelector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            SkillIds = new List<StringName> { skillDefinition.SkillId },
        };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDefinition);
        return action;
    }

    private static void ApplyUnitDistanceStyle(
        EnemyAiAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill);
        if (templateAction != null)
        {
            SetIntIfSupported(
                action,
                "desired_min_distance",
                GetDesiredMinDistance(templateAction)
            );
            SetIntIfSupported(
                action,
                "desired_max_distance",
                GetDesiredMaxDistance(templateAction)
            );
            SetDistanceReferenceIfSupported(
                action,
                GetDistanceReferenceKind(templateAction)
            );
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        SetIntIfSupported(
            action,
            "desired_min_distance",
            effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
        );
        SetIntIfSupported(
            action,
            "desired_max_distance",
            Math.Max(effectiveRange, effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0)
        );
        SetDistanceReferenceIfSupported(action, EnemyAiDistanceReference.TargetUnit);
    }

    private static void ApplyUnitDistanceStyle(
        BattleAiUnitSkillActionSpec action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        if (action == null)
        {
            return;
        }
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill);
        if (templateAction != null)
        {
            action.DesiredMinDistance = GetDesiredMinDistance(templateAction);
            action.DesiredMaxDistance = GetDesiredMaxDistance(templateAction);
            action.DistanceReferenceKind = GetDistanceReferenceKind(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        action.DesiredMinDistance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.DesiredMaxDistance = Math.Max(
            effectiveRange,
            effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
        );
        action.DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit;
    }

    private static void ApplyUnitDistanceStyle(
        BattleAiMultiUnitSkillActionSpec action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        if (action == null)
        {
            return;
        }
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill);
        if (templateAction != null)
        {
            action.DesiredMinDistance = GetDesiredMinDistance(templateAction);
            action.DesiredMaxDistance = GetDesiredMaxDistance(templateAction);
            action.DistanceReferenceKind = GetDistanceReferenceKind(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        action.DesiredMinDistance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.DesiredMaxDistance = Math.Max(
            effectiveRange,
            effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
        );
        action.DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit;
    }

    private static void ApplyUnitDistanceStyle(
        BattleAiMoveToMultiUnitSkillPositionActionSpec action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        if (action == null)
        {
            return;
        }
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill);
        if (templateAction != null)
        {
            action.DesiredMinDistance = GetDesiredMinDistance(templateAction);
            action.DesiredMaxDistance = GetDesiredMaxDistance(templateAction);
            action.DistanceReferenceKind = GetDistanceReferenceKind(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        action.DesiredMinDistance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.DesiredMaxDistance = Math.Max(
            effectiveRange,
            effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
        );
        action.DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit;
    }

    private static void ApplyRandomChainDistanceStyle(
        BattleAiRandomChainSkillActionSpec action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseRandomChainSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill);
        if (templateAction != null)
        {
            action.DesiredMinDistance = GetDesiredMinDistance(templateAction);
            action.DesiredMaxDistance = GetDesiredMaxDistance(templateAction);
            EnemyAiDistanceReference reference = GetDistanceReferenceKind(templateAction);
            action.DistanceReferenceKind =
                reference == EnemyAiDistanceReference.CandidatePool
                || reference == EnemyAiDistanceReference.EnemyFrontline
                    ? reference
                    : EnemyAiDistanceReference.CandidatePool;
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        action.DesiredMinDistance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.DesiredMaxDistance = Math.Max(effectiveRange, action.DesiredMinDistance);
        action.DistanceReferenceKind = EnemyAiDistanceReference.CandidatePool;
    }

    private static void ApplyGroundDistanceStyle(
        BattleAiGroundSkillActionSpec action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDefinition skillDefinition
    )
    {
        EnemyAiAction templateAction = FindActionByFamily(
            stateActions,
            EnemyAiActionFamily.UseGroundSkill
        );
        if (templateAction != null)
        {
            action.DesiredMinDistance = GetDesiredMinDistance(templateAction);
            action.DesiredMaxDistance = GetDesiredMaxDistance(templateAction);
            action.DistanceReferenceKind = GetDistanceReferenceKind(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDefinition
        );
        action.DesiredMinDistance = 0;
        action.DesiredMaxDistance = Math.Max(effectiveRange, 0);
        action.DistanceReferenceKind = EnemyAiDistanceReference.TargetCoord;
    }

    private static StringName ResolveGeneratedScoreBucketId(
        IReadOnlyList<EnemyAiAction> stateActions,
        EnemyAiActionFamily preferredFamily
    )
    {
        EnemyAiAction preferredAction = FindActionByFamily(stateActions, preferredFamily);
        if (preferredAction != null && GetScoreBucket(preferredAction) != "")
        {
            return GetScoreBucket(preferredAction);
        }
        foreach (EnemyAiAction action in stateActions)
        {
            if (action == null)
            {
                continue;
            }
            StringName bucketId = GetScoreBucket(action);
            if (bucketId != "" && action.GetDeclaredSkillIds().Count > 0)
            {
                return bucketId;
            }
        }
        return "";
    }

    private static StringName ResolveTargetSelector(
        IReadOnlyList<EnemyAiAction> stateActions,
        StringName fallback
    )
    {
        foreach (EnemyAiAction action in stateActions)
        {
            StringName selector = GetTargetSelector(action);
            if (selector != "")
            {
                return selector;
            }
        }
        return fallback;
    }

    private static StringName BuildActionId(
        StringName stateId,
        StringName skillId,
        StringName suffix
    )
    {
        return new StringName($"auto_{stateId}_{skillId}_{suffix}");
    }

    public bool IsOffensiveOrEnemySkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return false;
        }
        if (ProgressionDataUtils.to_string_name(combatProfile.SpecialResolutionProfileId) == "meteor_swarm")
        {
            return true;
        }
        StringName targetFilter = ProgressionDataUtils.to_string_name(combatProfile.TargetTeamFilter);
        if (BattleTargetTeamRules.IsEnemyFilter(targetFilter))
        {
            return true;
        }
        foreach (CombatEffectDefinition effectDefinition in combatProfile.EffectDefinitions)
        {
            if (IsOffensiveEffect(skillDefinition, effectDefinition))
            {
                return true;
            }
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant == null)
            {
                continue;
            }
            foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
            {
                if (IsOffensiveEffect(skillDefinition, effectDefinition))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsOffensiveEffect(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        StringName effectFilter = ProgressionDataUtils.to_string_name(effectDefinition.EffectTargetTeamFilter);
        StringName skillFilter = ProgressionDataUtils.to_string_name(
            skillDefinition?.CombatProfile?.TargetTeamFilter ?? ""
        );
        if (BattleTargetTeamRules.IsEnemyFilter(effectFilter))
        {
            return true;
        }
        if (BattleTargetTeamRules.IsBeneficialFilter(effectFilter))
        {
            return false;
        }
        if (effectFilter == "" && BattleTargetTeamRules.IsBeneficialFilter(skillFilter))
        {
            return false;
        }
        BattleEffectKind effectKind = effectDefinition.EffectKind;
        if (effectKind == BattleEffectKind.Damage || effectKind == BattleEffectKind.PathStepAoe)
        {
            return !BattleTargetTeamRules.IsBeneficialFilter(skillFilter);
        }
        if (
            effectKind == BattleEffectKind.Status
            || effectKind == BattleEffectKind.ApplyStatus
            || effectKind == BattleEffectKind.ForcedMove
        )
        {
            return true;
        }
        if (effectDefinition.StatusId != "" || effectDefinition.SaveFailureStatusId != "")
        {
            return true;
        }
        return false;
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (skillDefinitions == null || skillId == "")
        {
            return null;
        }
        return skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
        {
            return 0;
        }
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static StringName GetActionId(EnemyAiAction action)
    {
        return action != null ? ProgressionDataUtils.to_string_name(action.action_id) : "";
    }

    private static void SetActionId(EnemyAiAction action, StringName actionId)
    {
        if (action != null)
        {
            action.action_id = actionId;
        }
    }

    private static StringName GetScoreBucket(EnemyAiAction action)
    {
        return action != null ? ProgressionDataUtils.to_string_name(action.score_bucket_id) : "";
    }

    private static StringName GetActionIntent(EnemyAiAction action)
    {
        return action != null ? ProgressionDataUtils.to_string_name(action.action_intent) : "";
    }

    private static StringName GetTargetSelector(EnemyAiAction action)
    {
        return action switch
        {
            UseUnitSkillAction unitAction => unitAction.target_selector,
            UseMultiUnitSkillAction multiUnitAction => multiUnitAction.target_selector,
            UseRandomChainSkillAction chainAction => chainAction.target_selector,
            UseGroundRepositionSkillAction repositionAction => repositionAction.target_selector,
            RetreatAction retreatAction => retreatAction.target_selector,
            UseChargePathAoeAction chargePathAction => chargePathAction.target_selector,
            UseChargeAction chargeAction => chargeAction.target_selector,
            MoveToRangeAction moveAction => moveAction.target_selector,
            MoveToAdvantagePositionAction advantageAction => advantageAction.target_selector,
            _ => "",
        };
    }

    private static int GetDesiredMinDistance(EnemyAiAction action, int fallback = 0)
    {
        return action switch
        {
            UseUnitSkillAction unitAction => unitAction.desired_min_distance,
            UseGroundSkillAction groundAction => groundAction.desired_min_distance,
            UseMultiUnitSkillAction multiUnitAction => multiUnitAction.desired_min_distance,
            UseRandomChainSkillAction chainAction => chainAction.desired_min_distance,
            MoveToRangeAction moveAction => moveAction.desired_min_distance,
            UseChargePathAoeAction chargePathAction => chargePathAction.desired_min_distance,
            MoveToAdvantagePositionAction advantageAction => advantageAction.desired_min_distance,
            _ => fallback,
        };
    }

    private static int GetDesiredMaxDistance(EnemyAiAction action, int fallback = 0)
    {
        return action switch
        {
            UseUnitSkillAction unitAction => unitAction.desired_max_distance,
            UseGroundSkillAction groundAction => groundAction.desired_max_distance,
            UseMultiUnitSkillAction multiUnitAction => multiUnitAction.desired_max_distance,
            UseRandomChainSkillAction chainAction => chainAction.desired_max_distance,
            MoveToRangeAction moveAction => moveAction.desired_max_distance,
            UseChargePathAoeAction chargePathAction => chargePathAction.desired_max_distance,
            MoveToAdvantagePositionAction advantageAction => advantageAction.desired_max_distance,
            _ => fallback,
        };
    }

    private static EnemyAiDistanceReference GetDistanceReferenceKind(EnemyAiAction action)
    {
        return action switch
        {
            UseUnitSkillAction unitAction => unitAction.DistanceReferenceKind,
            UseGroundSkillAction groundAction => groundAction.DistanceReferenceKind,
            UseMultiUnitSkillAction multiUnitAction => multiUnitAction.DistanceReferenceKind,
            UseRandomChainSkillAction chainAction => chainAction.DistanceReferenceKind,
            _ => EnemyAiDistanceReference.None,
        };
    }

    private static void SetScoreBucket(EnemyAiAction action, StringName scoreBucketId)
    {
        if (action != null)
        {
            action.score_bucket_id = scoreBucketId;
        }
    }

    private static void SetActionIntent(EnemyAiAction action, StringName actionIntent)
    {
        if (action != null && BattleAiActionIntent.IsValid(actionIntent))
        {
            action.action_intent = actionIntent;
        }
    }

    private static void SetTargetSelectorIfSupported(EnemyAiAction action, StringName selector)
    {
        switch (action)
        {
            case UseUnitSkillAction unitAction:
                unitAction.target_selector = selector;
                break;
            case UseMultiUnitSkillAction multiUnitAction:
                multiUnitAction.target_selector = selector;
                break;
            case UseRandomChainSkillAction chainAction:
                chainAction.target_selector = selector;
                break;
            case UseGroundRepositionSkillAction repositionAction:
                repositionAction.target_selector = selector;
                break;
            case RetreatAction retreatAction:
                retreatAction.target_selector = selector;
                break;
            case UseChargePathAoeAction chargePathAction:
                chargePathAction.target_selector = selector;
                break;
            case UseChargeAction chargeAction:
                chargeAction.target_selector = selector;
                break;
            case MoveToRangeAction moveAction:
                moveAction.target_selector = selector;
                break;
            case MoveToAdvantagePositionAction advantageAction:
                advantageAction.target_selector = selector;
                break;
        }
    }

    private static void SetDistanceReferenceIfSupported(
        EnemyAiAction action,
        EnemyAiDistanceReference distanceReference
    )
    {
        switch (action)
        {
            case UseUnitSkillAction unitAction:
                unitAction.DistanceReferenceKind = distanceReference;
                break;
            case UseGroundSkillAction groundAction:
                groundAction.DistanceReferenceKind = distanceReference;
                break;
            case UseMultiUnitSkillAction multiUnitAction:
                multiUnitAction.DistanceReferenceKind = distanceReference;
                break;
            case UseRandomChainSkillAction chainAction:
                chainAction.DistanceReferenceKind = distanceReference;
                break;
        }
    }

    private static void SetIntIfSupported(EnemyAiAction action, string propertyName, int value)
    {
        switch (action)
        {
            case UseUnitSkillAction unitAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => unitAction.desired_min_distance = v,
                    v => unitAction.desired_max_distance = v
                );
                break;
            case UseGroundSkillAction groundAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => groundAction.desired_min_distance = v,
                    v => groundAction.desired_max_distance = v
                );
                break;
            case UseMultiUnitSkillAction multiUnitAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => multiUnitAction.desired_min_distance = v,
                    v => multiUnitAction.desired_max_distance = v
                );
                break;
            case UseRandomChainSkillAction chainAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => chainAction.desired_min_distance = v,
                    v => chainAction.desired_max_distance = v
                );
                break;
            case MoveToRangeAction moveAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => moveAction.desired_min_distance = v,
                    v => moveAction.desired_max_distance = v
                );
                break;
            case UseChargePathAoeAction chargePathAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => chargePathAction.desired_min_distance = v,
                    v => chargePathAction.desired_max_distance = v
                );
                break;
            case MoveToAdvantagePositionAction advantageAction:
                SetDistanceInt(
                    value,
                    propertyName,
                    v => advantageAction.desired_min_distance = v,
                    v => advantageAction.desired_max_distance = v
                );
                break;
        }
    }

    private static void SetDistanceInt(
        int value,
        string propertyName,
        Action<int> setMinDistance,
        Action<int> setMaxDistance
    )
    {
        if (propertyName == "desired_min_distance")
        {
            setMinDistance?.Invoke(value);
        }
        else if (propertyName == "desired_max_distance")
        {
            setMaxDistance?.Invoke(value);
        }
    }
}

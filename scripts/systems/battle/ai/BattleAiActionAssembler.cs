using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class BattleAiActionAssembler
{
    private readonly BattleAiSkillAffordanceClassifier _classifier = new();

    public BattleAiRuntimeActionPlan BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDef brain,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var plan = new BattleAiRuntimeActionPlan();
        if (unitState == null || brain == null)
        {
            return plan;
        }

        AiTraceRecorder.Enter("build_unit_action_plan");
        skillDefs ??= new Dictionary<StringName, SkillDef>();
        plan.SetSource(unitState, brain);
        List<BattleAiSkillAffordanceRecord> skillRecords = ClassifyKnownActiveSkills(
            unitState,
            skillDefs
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
                    SkillDef skillDef = GetSkillDef(skillDefs, skillId);
                    if (skillDef == null)
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

                        EnemyAiAction generatedAction = BuildSkillActionForFamily(
                            unitState,
                            stateDef,
                            runtimeActions,
                            skillDef,
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
                            ResolveGeneratedActionIntent(slot, skillDef, actionFamily)
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

        AiTraceRecorder.Exit("build_unit_action_plan");
        return plan;
    }

    private static Godot.Collections.Array<EnemyAiStateDef> GetBrainStates(EnemyAiBrainDef brain)
    {
        return brain?.GetResolvedStates() ?? new Godot.Collections.Array<EnemyAiStateDef>();
    }

    private List<BattleAiSkillAffordanceRecord> ClassifyKnownActiveSkills(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var records = new List<BattleAiSkillAffordanceRecord>();
        foreach (StringName rawSkillId in unitState.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
            {
                continue;
            }
            SkillDef skillDef = GetSkillDef(skillDefs, skillId);
            if (skillDef == null)
            {
                continue;
            }
            BattleAiSkillAffordanceRecord record = _classifier.ClassifySkill(
                skillDef,
                GetSkillLevel(unitState, skillId)
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
            EnemyAiAction runtimeAction = CloneAction(plan, action);
            EnableRuntimeActionDefaults(runtimeAction);
            runtimeActions.Add(runtimeAction);
        }
        return runtimeActions;
    }

    private static EnemyAiAction CloneAction(BattleAiRuntimeActionPlan plan, EnemyAiAction action)
    {
        if (action is Resource resource)
        {
            if (resource.Duplicate(true) is EnemyAiAction clone)
            {
                return plan != null
                    ? plan.OwnRuntimeAction(clone, $"clone_action:{action.action_id}")
                    : clone;
            }
        }
        return action;
    }

    private static void EnableRuntimeActionDefaults(EnemyAiAction action)
    {
        if (
            action is MoveToRangeAction moveToRange
            && moveToRange.CanUseGeneratedCandidateRequestMode()
        )
        {
            moveToRange.UseGeneratedCandidateRequestMode();
        }
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
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef,
        StringName actionFamily
    )
    {
        if (unitState == null || stateDef == null || skillDef?.combat_profile == null)
        {
            return null;
        }
        return EnemyAiGenerationSlotDef.ToActionFamily(actionFamily) switch
        {
            EnemyAiActionFamily.UseChargePathAoe => BuildChargePathAoeAction(
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.UseCharge => BuildChargeAction(stateDef, stateActions, skillDef),
            EnemyAiActionFamily.UseRandomChainSkill => BuildRandomChainAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.MoveToRange => BuildMoveToRangeAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.UseMultiUnitSkill => BuildMultiUnitAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.MoveToMultiUnitSkillPosition => BuildMoveToMultiUnitAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.UseGroundSkill => BuildGroundAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            EnemyAiActionFamily.UseUnitSkill => BuildUnitAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
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
        SkillDef skillDef,
        StringName actionFamily
    )
    {
        EnemyAiGenerationSlotRole slotRole = slot?.SlotRoleKind ?? EnemyAiGenerationSlotRole.Unknown;
        if (slotRole == EnemyAiGenerationSlotRole.Support)
        {
            return BattleAiActionIntent.InferForSkill(skillDef);
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
            _ => BattleAiActionIntent.InferForSkill(skillDef),
        };
    }

    private static UseChargePathAoeAction BuildChargePathAoeAction(
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new UseChargePathAoeAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "path_aoe"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseCharge
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            minimum_hit_count = 1,
            desired_min_distance = 1,
            desired_max_distance = 1,
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        return action;
    }

    private static UseChargeAction BuildChargeAction(
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        return new UseChargeAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "charge"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseCharge
            ),
            skill_id = skillDef.skill_id,
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            minimum_charge_move_distance = 3,
        };
    }

    private static UseGroundSkillAction BuildGroundAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new UseGroundSkillAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "ground"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseGroundSkill
            ),
            minimum_hit_count = Mathf.Max(
                (skillDef.combat_profile as CombatSkillDef)?.min_target_count ?? 0,
                1
            ),
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        ApplyGroundDistanceStyle(action, unitState, stateActions, skillDef);
        return action;
    }

    private static UseUnitSkillAction BuildUnitAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new UseUnitSkillAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "unit"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseUnitSkill
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDef);
        return action;
    }

    private static UseMultiUnitSkillAction BuildMultiUnitAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new UseMultiUnitSkillAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "multi"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseMultiUnitSkill
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDef);
        return action;
    }

    private static UseRandomChainSkillAction BuildRandomChainAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new UseRandomChainSkillAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "random_chain"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.UseUnitSkill
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        ApplyRandomChainDistanceStyle(action, unitState, stateActions, skillDef);
        return action;
    }

    private static MoveToRangeAction BuildMoveToRangeAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDef
        );
        var action = new MoveToRangeAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "range_move"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.MoveToRange
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
            range_skill_ids = new GStringNameArray { skillDef.skill_id },
            desired_min_distance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0,
            desired_max_distance = Math.Max(
                effectiveRange,
                effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
            ),
        };
        action.UseGeneratedCandidateRequestMode();
        return action;
    }

    private static MoveToMultiUnitSkillPositionAction BuildMoveToMultiUnitAction(
        BattleUnitState unitState,
        EnemyAiStateDef stateDef,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        var action = new MoveToMultiUnitSkillPositionAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "multi_move"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(
                stateActions,
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition
            ),
            target_selector = ResolveTargetSelector(
                stateActions,
                EnemyAiTargetSelectorRules.NearestEnemy
            ),
        };
        action.skill_ids = new GStringNameArray { skillDef.skill_id };
        ApplyUnitDistanceStyle(action, unitState, stateActions, skillDef);
        return action;
    }

    private static void ApplyUnitDistanceStyle(
        EnemyAiAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
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
            skillDef
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

    private static void ApplyRandomChainDistanceStyle(
        UseRandomChainSkillAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, EnemyAiActionFamily.UseRandomChainSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseUnitSkill)
            ?? FindActionByFamily(stateActions, EnemyAiActionFamily.UseMultiUnitSkill);
        if (templateAction != null)
        {
            action.desired_min_distance = GetDesiredMinDistance(templateAction);
            action.desired_max_distance = GetDesiredMaxDistance(templateAction);
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
            skillDef
        );
        action.desired_min_distance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.desired_max_distance = Math.Max(effectiveRange, action.desired_min_distance);
        action.DistanceReferenceKind = EnemyAiDistanceReference.CandidatePool;
    }

    private static void ApplyGroundDistanceStyle(
        UseGroundSkillAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        EnemyAiAction templateAction = FindActionByFamily(
            stateActions,
            EnemyAiActionFamily.UseGroundSkill
        );
        if (templateAction != null)
        {
            action.desired_min_distance = GetDesiredMinDistance(templateAction);
            action.desired_max_distance = GetDesiredMaxDistance(templateAction);
            action.DistanceReferenceKind = GetDistanceReferenceKind(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillDistanceContractRange(
            unitState,
            skillDef
        );
        action.desired_min_distance = 0;
        action.desired_max_distance = Math.Max(effectiveRange, 0);
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

    public bool IsOffensiveOrEnemySkill(SkillDef skillDef)
    {
        if (skillDef?.combat_profile is not CombatSkillDef combatProfile)
        {
            return false;
        }
        if (
            ProgressionDataUtils.to_string_name(combatProfile.special_resolution_profile_id)
            == "meteor_swarm"
        )
        {
            return true;
        }
        StringName targetFilter = ProgressionDataUtils.to_string_name(
            combatProfile.target_team_filter
        );
        if (BattleTargetTeamRules.IsEnemyFilter(targetFilter))
        {
            return true;
        }
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (IsOffensiveEffect(skillDef, effectDef))
            {
                return true;
            }
        }
        foreach (Resource optionResource in combatProfile.cast_variants)
        {
            if (optionResource is not CombatCastVariantDef castVariant)
            {
                continue;
            }
            foreach (Resource effectResource in castVariant.effect_defs)
            {
                if (IsOffensiveEffect(skillDef, effectResource as CombatEffectDef))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsOffensiveEffect(SkillDef skillDef, CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        StringName effectFilter = ProgressionDataUtils.to_string_name(
            effectDef.effect_target_team_filter
        );
        StringName skillFilter = skillDef?.combat_profile is CombatSkillDef combatProfile
            ? ProgressionDataUtils.to_string_name(combatProfile.target_team_filter)
            : "";
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
        BattleEffectKind effectKind = effectDef.EffectKind;
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
        if (effectDef.status_id != "" || effectDef.save_failure_status_id != "")
        {
            return true;
        }
        return false;
    }

    private static bool OptionHasEffect(CombatCastVariantDef castVariant, BattleEffectKind effectKind)
    {
        if (castVariant == null)
        {
            return false;
        }
        foreach (Resource effectResource in castVariant.effect_defs)
        {
            if (effectResource is CombatEffectDef effectDef && effectDef.EffectKind == effectKind)
            {
                return true;
            }
        }
        return false;
    }

    private static List<CombatCastVariantDef> GetUnlockedOptions(SkillDef skillDef, int skillLevel)
    {
        var options = new List<CombatCastVariantDef>();
        if (skillDef?.combat_profile == null)
        {
            return options;
        }
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(null, skillDef, skillLevel);
        foreach (CombatCastVariantDef option in effectiveProfile.UnlockedCastVariants)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }
        return options;
    }

    private static CombatCastVariantDef FindChargePathStepAoeValue(
        SkillDef skillDef,
        int skillLevel
    )
    {
        foreach (CombatCastVariantDef option in GetUnlockedOptions(skillDef, skillLevel))
        {
            if (
                OptionHasEffect(option, BattleEffectKind.Charge)
                && OptionHasEffect(option, BattleEffectKind.PathStepAoe)
            )
            {
                return option;
            }
        }
        return null;
    }

    private static CombatCastVariantDef FindChargeValue(SkillDef skillDef, int skillLevel)
    {
        foreach (CombatCastVariantDef option in GetUnlockedOptions(skillDef, skillLevel))
        {
            if (OptionHasEffect(option, BattleEffectKind.Charge))
            {
                return option;
            }
        }
        return null;
    }

    private static SkillDef GetSkillDef(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        StringName skillId
    )
    {
        if (skillDefs == null || skillId == "")
        {
            return null;
        }
        return skillDefs.TryGetValue(skillId, out SkillDef skillDef) ? skillDef : null;
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

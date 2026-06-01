using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class BattleAiActionAssembler : RefCounted
{
    private static readonly StringName PathStepAoeEffectType = "path_step_aoe";

    private readonly BattleAiSkillAffordanceClassifier _classifier = new();

    public BattleAiRuntimeActionPlan build_unit_action_plan(
        BattleUnitState unit_state,
        EnemyAiBrainDef brain,
        GDictionary skill_defs
    )
    {
        var plan = new BattleAiRuntimeActionPlan();
        if (unit_state == null || brain == null)
        {
            return plan;
        }

        skill_defs ??= new GDictionary();
        plan.set_source(unit_state, brain, skill_defs);
        List<BattleAiSkillAffordanceRecord> skillRecords = ClassifyKnownActiveSkills(unit_state, skill_defs);
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
            List<EnemyAiAction> runtimeActions = CloneRuntimeActions(authoredActions);
            plan.AddStateActionsTyped(stateId, runtimeActions);

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
                    SkillDef skillDef = GetSkillDef(skill_defs, skillId);
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
                            unit_state,
                            stateDef,
                            runtimeActions,
                            skillDef,
                            actionFamily
                        );
                        if (generatedAction == null)
                        {
                            continue;
                        }

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

    private static Godot.Collections.Array<EnemyAiStateDef> GetBrainStates(EnemyAiBrainDef brain)
    {
        return brain?.get_resolved_states() ?? new Godot.Collections.Array<EnemyAiStateDef>();
    }

    private List<BattleAiSkillAffordanceRecord> ClassifyKnownActiveSkills(
        BattleUnitState unitState,
        GDictionary skillDefs
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

    private static List<EnemyAiAction> CloneRuntimeActions(List<EnemyAiAction> authoredActions)
    {
        var runtimeActions = new List<EnemyAiAction>();
        foreach (EnemyAiAction action in authoredActions ?? new List<EnemyAiAction>())
        {
            if (action == null)
            {
                continue;
            }
            EnemyAiAction runtimeAction = CloneAction(action);
            EnableRuntimeActionDefaults(runtimeAction);
            runtimeActions.Add(runtimeAction);
        }
        return runtimeActions;
    }

    private static EnemyAiAction CloneAction(EnemyAiAction action)
    {
        if (action is Resource resource)
        {
            if (resource.Duplicate(true) is EnemyAiAction clone)
            {
                return clone;
            }
        }
        return action;
    }

    private static void EnableRuntimeActionDefaults(EnemyAiAction action)
    {
        if (
            action is MoveToRangeAction moveToRange
            && moveToRange.screening_mode == MoveToRangeAction.ScreeningNone
        )
        {
            moveToRange.ai_evaluation_mode = MoveToRangeAction.AiEvaluationCandidateRequest;
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
        return actionFamily.ToString() switch
        {
            "use_charge_path_aoe" => BuildChargePathAoeAction(stateDef, stateActions, skillDef),
            "use_charge" => BuildChargeAction(stateDef, stateActions, skillDef),
            "use_random_chain_skill" => BuildRandomChainAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            "move_to_range" => BuildMoveToRangeAction(unitState, stateDef, stateActions, skillDef),
            "use_multi_unit_skill" => BuildMultiUnitAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            "move_to_multi_unit_skill_position" => BuildMoveToMultiUnitAction(
                unitState,
                stateDef,
                stateActions,
                skillDef
            ),
            "use_ground_skill" => BuildGroundAction(unitState, stateDef, stateActions, skillDef),
            "use_unit_skill" => BuildUnitAction(unitState, stateDef, stateActions, skillDef),
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
        if (slot.suppression_policy == "manual_only")
        {
            return true;
        }

        StringName stateId = stateDef.state_id;
        string identityKey = BuildIdentityKey(stateId, slot.slot_id, skillId, actionFamily);
        foreach (EnemyAiAction existingAction in plan.GetTypedActions(stateId))
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
            if (!authoredAction.get_declared_skill_ids().Contains(skillId))
            {
                continue;
            }
            if (GetActionFamilyForAction(authoredAction) == actionFamily)
            {
                return true;
            }
        }
        return false;
    }

    private static StringName GetActionFamilyForAction(EnemyAiAction action)
    {
        return action switch
        {
            UseUnitSkillAction => "use_unit_skill",
            UseGroundSkillAction => "use_ground_skill",
            MoveToMultiUnitSkillPositionAction => "move_to_multi_unit_skill_position",
            UseMultiUnitSkillAction => "use_multi_unit_skill",
            UseRandomChainSkillAction => "use_random_chain_skill",
            UseChargePathAoeAction => "use_charge_path_aoe",
            UseChargeAction => "use_charge",
            MoveToRangeAction => "move_to_range",
            _ => "",
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
        StringName distanceReference = slot.distance_reference;
        if (distanceReference != "")
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
        StringName actionFamily
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_charge"),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_charge"),
            skill_id = skillDef.skill_id,
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_ground_skill"),
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_unit_skill"),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_multi_unit_skill"),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "use_unit_skill"),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
        int effectiveRange = BattleRangeService.get_effective_skill_distance_contract_range(
            unitState,
            skillDef
        );
        return new MoveToRangeAction
        {
            action_id = BuildActionId(
                stateDef.state_id,
                skillDef.skill_id,
                "range_move"
            ),
            score_bucket_id = ResolveGeneratedScoreBucketId(stateActions, "move_to_range"),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
            range_skill_ids = new GStringNameArray { skillDef.skill_id },
            desired_min_distance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0,
            desired_max_distance = Math.Max(
                effectiveRange,
                effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0
            ),
            ai_evaluation_mode = MoveToRangeAction.AiEvaluationCandidateRequest,
        };
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
                "move_to_multi_unit_skill_position"
            ),
            target_selector = ResolveTargetSelector(stateActions, "nearest_enemy"),
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
            FindActionByFamily(stateActions, "use_unit_skill")
            ?? FindActionByFamily(stateActions, "use_multi_unit_skill");
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
                GetDistanceReference(templateAction)
            );
            return;
        }
        int effectiveRange = BattleRangeService.get_effective_skill_distance_contract_range(
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
        SetDistanceReferenceIfSupported(action, "target_unit");
    }

    private static void ApplyRandomChainDistanceStyle(
        UseRandomChainSkillAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        EnemyAiAction templateAction =
            FindActionByFamily(stateActions, "use_random_chain_skill")
            ?? FindActionByFamily(stateActions, "use_unit_skill")
            ?? FindActionByFamily(stateActions, "use_multi_unit_skill");
        if (templateAction != null)
        {
            action.desired_min_distance = GetDesiredMinDistance(templateAction);
            action.desired_max_distance = GetDesiredMaxDistance(templateAction);
            StringName reference = GetDistanceReference(templateAction);
            action.distance_reference =
                reference == "candidate_pool" || reference == "enemy_frontline"
                    ? reference
                    : "candidate_pool";
            return;
        }
        int effectiveRange = BattleRangeService.get_effective_skill_distance_contract_range(
            unitState,
            skillDef
        );
        action.desired_min_distance = effectiveRange > 0 ? Math.Min(1, effectiveRange) : 0;
        action.desired_max_distance = Math.Max(effectiveRange, action.desired_min_distance);
        action.distance_reference = "candidate_pool";
    }

    private static void ApplyGroundDistanceStyle(
        UseGroundSkillAction action,
        BattleUnitState unitState,
        IReadOnlyList<EnemyAiAction> stateActions,
        SkillDef skillDef
    )
    {
        EnemyAiAction templateAction = FindActionByFamily(stateActions, "use_ground_skill");
        if (templateAction != null)
        {
            action.desired_min_distance = GetDesiredMinDistance(templateAction);
            action.desired_max_distance = GetDesiredMaxDistance(templateAction);
            action.distance_reference = GetDistanceReference(templateAction);
            return;
        }
        int effectiveRange = BattleRangeService.get_effective_skill_distance_contract_range(
            unitState,
            skillDef
        );
        action.desired_min_distance = 0;
        action.desired_max_distance = Math.Max(effectiveRange, 0);
        action.distance_reference = "target_coord";
    }

    private static StringName ResolveGeneratedScoreBucketId(
        IReadOnlyList<EnemyAiAction> stateActions,
        StringName preferredFamily
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
            if (bucketId != "" && action.get_declared_skill_ids().Count > 0)
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

    public bool _is_offensive_or_enemy_skill(SkillDef skill_def)
    {
        if (skill_def?.combat_profile is not CombatSkillDef combatProfile)
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
        if (BattleTargetTeamRules.is_enemy_filter(targetFilter))
        {
            return true;
        }
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (IsOffensiveEffect(skill_def, effectDef))
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
                if (IsOffensiveEffect(skill_def, effectResource as CombatEffectDef))
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
        if (BattleTargetTeamRules.is_enemy_filter(effectFilter))
        {
            return true;
        }
        if (BattleTargetTeamRules.is_beneficial_filter(effectFilter))
        {
            return false;
        }
        if (effectFilter == "" && BattleTargetTeamRules.is_beneficial_filter(skillFilter))
        {
            return false;
        }
        var effectKind = BattleTypedNames.ToEffectKind(effectDef.effect_type);
        if (effectKind == BattleEffectKind.Damage || effectKind == BattleEffectKind.PathStepAoe)
        {
            return !BattleTargetTeamRules.is_beneficial_filter(skillFilter);
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

    private static bool OptionHasEffect(CombatCastVariantDef castVariant, StringName effectType)
    {
        if (castVariant == null)
        {
            return false;
        }
        foreach (Resource effectResource in castVariant.effect_defs)
        {
            if (effectResource is CombatEffectDef effectDef && effectDef.effect_type == effectType)
            {
                return true;
            }
        }
        return false;
    }

    private static List<CombatCastVariantDef> GetUnlockedOptions(SkillDef skillDef, int skillLevel)
    {
        var options = new List<CombatCastVariantDef>();
        if (skillDef?.combat_profile is not CombatSkillDef combatProfile)
        {
            return options;
        }
        foreach (
            CombatCastVariantDef option in combatProfile.get_unlocked_cast_variants(skillLevel)
        )
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
                OptionHasEffect(option, "charge")
                && OptionHasEffect(option, PathStepAoeEffectType)
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
            if (OptionHasEffect(option, "charge"))
            {
                return option;
            }
        }
        return null;
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || skillId == "")
        {
            return null;
        }
        if (skillDefs.ContainsKey(skillId))
        {
            return skillDefs[skillId].As<SkillDef>();
        }
        string key = skillId.ToString();
        return skillDefs.ContainsKey(key) ? skillDefs[key].As<SkillDef>() : null;
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
        {
            return 0;
        }
        if (unitState.known_skill_level_map.ContainsKey(skillId))
        {
            return unitState.known_skill_level_map[skillId].AsInt32();
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
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

    private static StringName GetDistanceReference(EnemyAiAction action)
    {
        return action switch
        {
            UseUnitSkillAction unitAction => unitAction.distance_reference,
            UseGroundSkillAction groundAction => groundAction.distance_reference,
            UseMultiUnitSkillAction multiUnitAction => multiUnitAction.distance_reference,
            UseRandomChainSkillAction chainAction => chainAction.distance_reference,
            _ => "",
        };
    }

    private static void SetScoreBucket(EnemyAiAction action, StringName scoreBucketId)
    {
        if (action != null)
        {
            action.score_bucket_id = scoreBucketId;
        }
    }

    private static void SetTargetSelectorIfSupported(EnemyAiAction action, StringName selector)
    {
        if (action is UseUnitSkillAction unitAction)
        {
            unitAction.target_selector = selector;
        }
        else if (action is EnemyAiAction)
        {
            action.Set("target_selector", selector);
        }
    }

    private static void SetDistanceReferenceIfSupported(
        EnemyAiAction action,
        StringName distanceReference
    )
    {
        if (action is UseUnitSkillAction unitAction)
        {
            unitAction.distance_reference = distanceReference;
        }
        else if (
            action
            is UseGroundSkillAction
                or UseMultiUnitSkillAction
                or MoveToMultiUnitSkillPositionAction
                or UseRandomChainSkillAction
        )
        {
            action.Set("distance_reference", distanceReference);
        }
    }

    private static void SetIntIfSupported(EnemyAiAction action, string propertyName, int value)
    {
        if (action is UseUnitSkillAction unitAction)
        {
            if (propertyName == "desired_min_distance")
            {
                unitAction.desired_min_distance = value;
            }
            else if (propertyName == "desired_max_distance")
            {
                unitAction.desired_max_distance = value;
            }
            return;
        }
        if (
            action
            is UseGroundSkillAction
                or UseMultiUnitSkillAction
                or MoveToMultiUnitSkillPositionAction
                or UseRandomChainSkillAction
                or MoveToRangeAction
                or UseChargePathAoeAction
        )
        {
            action.Set(propertyName, value);
        }
    }
}

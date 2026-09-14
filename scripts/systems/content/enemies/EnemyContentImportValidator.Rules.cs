#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class EnemyContentImportValidator
{
    private static readonly HashSet<int> ValidHitDieSides = new() { 4, 6, 8, 10, 12, 20 };
    private static readonly HashSet<string> ValidActionIntents = new(StringComparer.Ordinal)
    {
        "offense",
        "control",
        "survival",
        "positioning",
        "escape",
        "wait",
    };
    private static readonly HashSet<string> ValidTargetSelectors = new(StringComparer.Ordinal)
    {
        "nearest_enemy",
        "lowest_hp_enemy",
        "nearest_role_threat_enemy",
        "nearest_ally",
        "lowest_hp_ally",
        "self",
    };
    private static readonly HashSet<string> EnemyFocusTargetSelectors = new(StringComparer.Ordinal)
    {
        "nearest_enemy",
        "lowest_hp_enemy",
        "nearest_role_threat_enemy",
    };
    private static readonly HashSet<string> ValidSlotRoles = new(StringComparer.Ordinal)
    {
        "offense", "control", "support", "positioning", "survival", "engage",
    };
    private static readonly HashSet<string> ValidAffordances = new(StringComparer.Ordinal)
    {
        "unit_hostile.damage", "unit_hostile.control", "ground_hostile.aoe",
        "ground_control", "terrain_control", "displacement_control", "charge_engage",
        "charge_path_aoe", "multi_unit", "random_chain", "special_ground", "ally_heal",
        "self_or_ally_buff", "reposition", "escape", "utility", "breaker",
    };
    private static readonly HashSet<string> ValidActionFamilies = new(StringComparer.Ordinal)
    {
        "use_unit_skill", "use_ground_skill", "use_multi_unit_skill",
        "use_random_chain_skill", "use_charge", "use_charge_path_aoe", "move_to_range",
        "move_to_multi_unit_skill_position",
    };
    private static readonly HashSet<string> ValidSuppressionPolicies = new(StringComparer.Ordinal)
    {
        "suppress_matching_family", "allow_companion", "manual_only",
    };
    private static readonly HashSet<string> ValidDistanceReferences = new(StringComparer.Ordinal)
    {
        "", "target_unit", "target_coord", "candidate_pool", "enemy_frontline",
    };
    private static readonly HashSet<string> ValidCognitionKinds = new(StringComparer.Ordinal)
    {
        "mindless", "instinctive", "sapient",
    };
    private static readonly HashSet<string> ValidTargetRanks = new(StringComparer.Ordinal)
    {
        "normal", "elite", "boss",
    };
    private static readonly HashSet<string> ValidSaveTags = new(StringComparer.Ordinal)
    {
        "sleep", "paralysis", "charm", "poison", "dragon_breath", "fireball",
        "chain_lightning", "equipment_disjunction", "magic", "illusion", "frightened",
        "dragon_frightful_presence", "execute", "temporal", "petrification", "antidote",
        "strength", "agility", "constitution", "perception", "intelligence", "willpower",
    };
    private static readonly HashSet<string> ValidDamageTags = new(StringComparer.Ordinal)
    {
        "physical_slash", "physical_pierce", "physical_blunt", "fire", "freeze",
        "lightning", "negative_energy", "force", "psychic", "radiant", "thunder",
        "magic", "acid", "poison",
    };
    private static readonly HashSet<string> ValidMitigationTiers = new(StringComparer.Ordinal)
    {
        "normal", "half", "double", "immune",
    };
    private static readonly HashSet<string> ValidEquipmentSlots = new(StringComparer.Ordinal)
    {
        "main_hand", "off_hand", "head", "body", "hands", "feet", "cloak", "necklace",
        "ring_1", "ring_2", "special_trinket", "badge",
    };
    private static readonly string[] BaseAttributeIds =
    {
        "strength",
        "agility",
        "constitution",
        "perception",
        "intelligence",
        "willpower",
    };

    private static void AppendBrainRuleDiagnostics(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyAiBrainImportModel import
    )
    {
        var declaredStateIds = new HashSet<string>(
            import.States.Select(value => value.StateId),
            StringComparer.Ordinal
        );
        if (
            !string.IsNullOrWhiteSpace(import.DefaultStateId)
            && !declaredStateIds.Contains(import.DefaultStateId)
        )
        {
            Add(
                diagnostics,
                context,
                EnemyContentImportRules.ReferenceMissing,
                $"default_state_id '{import.DefaultStateId}' is not declared by this brain.",
                "/default_state_id"
            );
        }

        for (int stateIndex = 0; stateIndex < import.States.Count; stateIndex += 1)
        {
            EnemyAiStateImportModel state = import.States[stateIndex];
            string statePointer = $"/states/{stateIndex}";
            if (string.IsNullOrWhiteSpace(state.StateId))
            {
                Add(
                    diagnostics,
                    context,
                    EnemyContentImportRules.IdRequired,
                    "Enemy AI state must declare state_id.",
                    $"{statePointer}/state_id"
                );
            }

            var actionIds = new HashSet<string>(
                state.Actions.Select(value => value.Payload.ActionId),
                StringComparer.Ordinal
            );
            for (int actionIndex = 0; actionIndex < state.Actions.Count; actionIndex += 1)
            {
                ValidateAction(
                    diagnostics,
                    context,
                    state.Actions[actionIndex],
                    $"{statePointer}/actions/{actionIndex}"
                );
            }
            for (int slotIndex = 0; slotIndex < state.GenerationSlots.Count; slotIndex += 1)
            {
                ValidateGenerationSlot(
                    diagnostics,
                    context,
                    state.GenerationSlots[slotIndex],
                    actionIds,
                    $"{statePointer}/generation_slots/{slotIndex}"
                );
            }
        }

        for (int ruleIndex = 0; ruleIndex < import.TransitionRules.Count; ruleIndex += 1)
        {
            ValidateTransitionRule(
                diagnostics,
                context,
                import.TransitionRules[ruleIndex],
                declaredStateIds,
                $"/transition_rules/{ruleIndex}"
            );
        }
    }

    private static void ValidateAction(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyAiActionImportModel action,
        string pointer
    )
    {
        EnemyAiActionPayloadJsonDto payload = action.Payload;
        if (string.IsNullOrWhiteSpace(payload.ActionId))
        {
            Add(
                diagnostics,
                context,
                EnemyContentImportRules.IdRequired,
                "Enemy AI action must declare action_id.",
                $"{pointer}/payload/action_id"
            );
        }
        if (!ValidActionIntents.Contains(payload.ActionIntent))
        {
            Unsupported(
                diagnostics,
                context,
                $"Unsupported action_intent '{payload.ActionIntent}'.",
                $"{pointer}/payload/action_intent"
            );
        }
        if (!ActionKindMatchesPayload(action.Kind, payload))
        {
            Unsupported(
                diagnostics,
                context,
                $"Action kind '{action.Kind}' does not match payload type '{payload.GetType().Name}'.",
                $"{pointer}/payload"
            );
            return;
        }

        ValidateDistinctIds(
            diagnostics,
            context,
            GetDeclaredSkillIds(payload),
            $"{pointer}/payload",
            "skill_id"
        );

        switch (payload)
        {
            case MoveToAdvantagePositionActionPayloadJsonDto value:
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                RequireNonNegative(diagnostics, context, value.MinimumSafeDistance, $"{pointer}/payload/minimum_safe_distance");
                RequireNonNegative(diagnostics, context, value.SafeDistanceMargin, $"{pointer}/payload/safe_distance_margin");
                RequireNonNegative(diagnostics, context, value.MinDistanceProgressWhenBeyondBand, $"{pointer}/payload/min_distance_progress_when_beyond_band");
                if (value.PositioningMode is not ("advantage" or "survival" or "high_ground"))
                    Unsupported(diagnostics, context, $"Unsupported positioning_mode '{value.PositioningMode}'.", $"{pointer}/payload/positioning_mode");
                break;
            case MoveToMultiUnitSkillPositionActionPayloadJsonDto value:
                ValidateMultiUnitAction(diagnostics, context, value, pointer);
                RequireNonNegative(diagnostics, context, value.TargetCountWeight, $"{pointer}/payload/target_count_weight");
                break;
            case MoveToRangeActionPayloadJsonDto value:
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                if (value.AiEvaluationMode is not ("inline_decide" or "candidate_request"))
                    Unsupported(diagnostics, context, $"Unsupported ai_evaluation_mode '{value.AiEvaluationMode}'.", $"{pointer}/payload/ai_evaluation_mode");
                if (value.ScreeningMode is not ("none" or "ranged_ally"))
                    Unsupported(diagnostics, context, $"Unsupported screening_mode '{value.ScreeningMode}'.", $"{pointer}/payload/screening_mode");
                if (value.AiEvaluationMode == "candidate_request" && value.ScreeningMode != "none")
                    Unsupported(diagnostics, context, "candidate_request only supports screening_mode 'none'.", $"{pointer}/payload/screening_mode");
                RequireRange(diagnostics, context, value.ScreeningMinHpBasisPoints, 0, 10000, $"{pointer}/payload/screening_min_hp_basis_points");
                RequireAtLeast(diagnostics, context, value.ScreeningAllyMinAttackRange, 1, $"{pointer}/payload/screening_ally_min_attack_range");
                RequireAtLeast(diagnostics, context, value.ScreeningEnemyMaxContactRange, 1, $"{pointer}/payload/screening_enemy_max_contact_range");
                RequireNonNegative(diagnostics, context, value.ScreeningThreatDistanceBuffer, $"{pointer}/payload/screening_threat_distance_buffer");
                RequireNonNegative(diagnostics, context, value.ScreeningPathBonus, $"{pointer}/payload/screening_path_bonus");
                RequireAtLeast(diagnostics, context, value.AoeSetupMinTargetCount, 1, $"{pointer}/payload/aoe_setup_min_target_count");
                RequireNonNegative(diagnostics, context, value.AoeSetupTargetCountWeight, $"{pointer}/payload/aoe_setup_target_count_weight");
                RequireNonNegative(diagnostics, context, value.AoeSetupImprovementWeight, $"{pointer}/payload/aoe_setup_improvement_weight");
                RequireNonNegative(diagnostics, context, value.AoeSetupFriendlyFirePenalty, $"{pointer}/payload/aoe_setup_friendly_fire_penalty");
                break;
            case RetreatActionPayloadJsonDto value:
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                RequireAtLeast(diagnostics, context, value.MinimumSafeDistance, 1, $"{pointer}/payload/minimum_safe_distance");
                RequireNonNegative(diagnostics, context, value.SafeDistanceMargin, $"{pointer}/payload/safe_distance_margin");
                break;
            case UseChargeActionPayloadJsonDto value:
                RequireId(diagnostics, context, value.SkillId, $"{pointer}/payload/skill_id", "skill_id");
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                RequireAtLeast(diagnostics, context, value.MinimumChargeMoveDistance, 1, $"{pointer}/payload/minimum_charge_move_distance");
                break;
            case UseChargePathAoeActionPayloadJsonDto value:
                RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                RequireAtLeast(diagnostics, context, value.MinimumHitCount, 1, $"{pointer}/payload/minimum_hit_count");
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                break;
            case UseGroundRepositionSkillActionPayloadJsonDto value:
                RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
                RequireEnemyFocusSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector");
                if (value.PositioningMode is not ("escape" or "high_ground"))
                    Unsupported(diagnostics, context, $"Unsupported positioning_mode '{value.PositioningMode}'.", $"{pointer}/payload/positioning_mode");
                RequireAtLeast(diagnostics, context, value.MinimumSafeDistance, 1, $"{pointer}/payload/minimum_safe_distance");
                RequireNonNegative(diagnostics, context, value.SafeDistanceMargin, $"{pointer}/payload/safe_distance_margin");
                RequireNonNegative(diagnostics, context, value.DesiredMaxDistanceBonus, $"{pointer}/payload/desired_max_distance_bonus");
                RequireNonNegative(diagnostics, context, value.HighGroundWeight, $"{pointer}/payload/high_ground_weight");
                break;
            case UseGroundSkillActionPayloadJsonDto value:
                RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
                RequireAtLeast(diagnostics, context, value.MinimumHitCount, 1, $"{pointer}/payload/minimum_hit_count");
                RequireAtLeast(diagnostics, context, value.MinimumGroundControlScore, 1, $"{pointer}/payload/minimum_ground_control_score");
                RequireNonNegative(diagnostics, context, value.MinimumAllyThreatHitCount, $"{pointer}/payload/minimum_ally_threat_hit_count");
                RequireNonNegative(diagnostics, context, value.MaximumFriendlyFireTargetCount, $"{pointer}/payload/maximum_friendly_fire_target_count");
                RequireNonNegative(diagnostics, context, value.ThreatMinimumSafeDistance, $"{pointer}/payload/threat_minimum_safe_distance");
                RequireNonNegative(diagnostics, context, value.ThreatSafeDistanceMargin, $"{pointer}/payload/threat_safe_distance_margin");
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                RequireDistanceReference(diagnostics, context, value.DistanceReference, pointer, "target_coord", "enemy_frontline");
                break;
            case UseMultiUnitSkillActionPayloadJsonDto value:
                ValidateMultiUnitAction(diagnostics, context, value, pointer);
                break;
            case UseRandomChainSkillActionPayloadJsonDto value:
                RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
                RequireSupportedSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector", allowEmpty: false);
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                RequireDistanceReference(diagnostics, context, value.DistanceReference, pointer, "candidate_pool", "enemy_frontline");
                RequireAtLeast(diagnostics, context, value.MinimumCandidateCount, 1, $"{pointer}/payload/minimum_candidate_count");
                break;
            case UseUnitSkillActionPayloadJsonDto value:
                RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
                RequireSupportedSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector", allowEmpty: false);
                RequireNonNegative(diagnostics, context, value.MinimumEffectiveTargetCount, $"{pointer}/payload/minimum_effective_target_count");
                RequireNonNegative(diagnostics, context, value.MaximumFriendlyFireTargetCount, $"{pointer}/payload/maximum_friendly_fire_target_count");
                ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
                RequireDistanceReference(diagnostics, context, value.DistanceReference, pointer, "target_unit", "enemy_frontline");
                break;
            case WaitActionPayloadJsonDto value:
                RequireAtLeast(diagnostics, context, value.ActiveRestActionBaseScore, -1000, $"{pointer}/payload/active_rest_action_base_score");
                RequireNonNegative(diagnostics, context, value.ActiveRestMinStaminaResidue, $"{pointer}/payload/active_rest_min_stamina_residue");
                break;
        }
    }

    private static void ValidateMultiUnitAction(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        UseMultiUnitSkillActionPayloadJsonDto value,
        string pointer
    )
    {
        RequireIds(diagnostics, context, value.SkillIds, $"{pointer}/payload/skill_ids", "skill_id");
        RequireSupportedSelector(diagnostics, context, value.TargetSelector, $"{pointer}/payload/target_selector", allowEmpty: false);
        ValidateDistanceBand(diagnostics, context, value.DesiredMinDistance, value.DesiredMaxDistance, pointer);
        RequireDistanceReference(diagnostics, context, value.DistanceReference, pointer, "target_unit", "enemy_frontline");
        RequireAtLeast(diagnostics, context, value.CandidatePoolLimit, 1, $"{pointer}/payload/candidate_pool_limit");
        RequireAtLeast(diagnostics, context, value.CandidateGroupLimit, 1, $"{pointer}/payload/candidate_group_limit");
    }

    private static void ValidateGenerationSlot(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyAiGenerationSlotJsonDto slot,
        IReadOnlySet<string> actionIds,
        string pointer
    )
    {
        RequireId(diagnostics, context, slot.SlotId, $"{pointer}/slot_id", "slot_id");
        if (!ValidSlotRoles.Contains(slot.SlotRole))
            Unsupported(diagnostics, context, $"Unsupported slot_role '{slot.SlotRole}'.", $"{pointer}/slot_role");
        RequireNonNegative(diagnostics, context, slot.Order, $"{pointer}/order");
        RequireIds(diagnostics, context, slot.AllowedAffordances, $"{pointer}/allowed_affordances", "allowed_affordance");
        foreach ((string value, int index) in slot.AllowedAffordances.Select((value, index) => (value, index)))
            if (!ValidAffordances.Contains(value))
                Unsupported(diagnostics, context, $"Unsupported allowed_affordance '{value}'.", $"{pointer}/allowed_affordances/{index}");
        RequireIds(diagnostics, context, slot.ActionFamilies, $"{pointer}/action_families", "action_family");
        foreach ((string value, int index) in slot.ActionFamilies.Select((value, index) => (value, index)))
            if (!ValidActionFamilies.Contains(value))
                Unsupported(diagnostics, context, $"Unsupported action_family '{value}'.", $"{pointer}/action_families/{index}");
        if (!string.IsNullOrWhiteSpace(slot.StyleTemplateActionId) && !actionIds.Contains(slot.StyleTemplateActionId))
            Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"style_template_action_id '{slot.StyleTemplateActionId}' is not declared in the same state.", $"{pointer}/style_template_action_id");
        RequireSupportedSelector(diagnostics, context, slot.TargetSelector, $"{pointer}/target_selector", allowEmpty: true);
        RequireAtLeast(diagnostics, context, slot.DesiredMinDistance, -1, $"{pointer}/desired_min_distance");
        RequireAtLeast(diagnostics, context, slot.DesiredMaxDistance, -1, $"{pointer}/desired_max_distance");
        if (slot.DesiredMinDistance >= 0 && slot.DesiredMaxDistance >= 0 && slot.DesiredMinDistance > slot.DesiredMaxDistance)
            RangeError(diagnostics, context, "desired_min_distance cannot exceed desired_max_distance.", $"{pointer}/desired_max_distance");
        if (!ValidDistanceReferences.Contains(slot.DistanceReference))
            Unsupported(diagnostics, context, $"Unsupported distance_reference '{slot.DistanceReference}'.", $"{pointer}/distance_reference");
        if (!ValidSuppressionPolicies.Contains(slot.SuppressionPolicy))
            Unsupported(diagnostics, context, $"Unsupported suppression_policy '{slot.SuppressionPolicy}'.", $"{pointer}/suppression_policy");
    }

    private static void ValidateTransitionRule(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyAiTransitionRuleJsonDto rule,
        IReadOnlySet<string> stateIds,
        string pointer
    )
    {
        RequireId(diagnostics, context, rule.RuleId, $"{pointer}/rule_id", "rule_id");
        RequireId(diagnostics, context, rule.TargetStateId, $"{pointer}/target_state_id", "target_state_id");
        if (!string.IsNullOrWhiteSpace(rule.TargetStateId) && !stateIds.Contains(rule.TargetStateId))
            Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"target_state_id '{rule.TargetStateId}' is not declared by this brain.", $"{pointer}/target_state_id");
        ValidateDistinctIds(diagnostics, context, rule.FromStateIds, $"{pointer}/from_state_ids", "from_state_id");
        for (int index = 0; index < rule.FromStateIds.Count; index += 1)
            if (!stateIds.Contains(rule.FromStateIds[index]))
                Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"from_state_id '{rule.FromStateIds[index]}' is not declared by this brain.", $"{pointer}/from_state_ids/{index}");
        if (rule.Conditions.Count == 0)
            Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, "Transition rule must declare at least one condition.", $"{pointer}/conditions");
        for (int index = 0; index < rule.Conditions.Count; index += 1)
            ValidateTransitionCondition(diagnostics, context, rule.Conditions[index], stateIds, $"{pointer}/conditions/{index}");
    }

    private static void ValidateTransitionCondition(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyAiTransitionConditionJsonDto condition,
        IReadOnlySet<string> stateIds,
        string pointer
    )
    {
        string predicate = condition.Predicate;
        if (
            predicate
                is not (
                    "always"
                    or "current_state_is"
                    or "self_hp_at_or_below_basis_points"
                    or "ally_hp_at_or_below_basis_points"
                    or "nearest_enemy_distance_at_or_below"
                    or "has_skill_affordance"
                )
        )
        {
            Unsupported(diagnostics, context, $"Unsupported transition predicate '{condition.Predicate}'.", $"{pointer}/predicate");
            return;
        }
        if (predicate == "current_state_is")
        {
            RequireIds(diagnostics, context, condition.StateIds, $"{pointer}/state_ids", "state_id");
            for (int index = 0; index < condition.StateIds.Count; index += 1)
                if (!stateIds.Contains(condition.StateIds[index]))
                    Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"state_id '{condition.StateIds[index]}' is not declared by this brain.", $"{pointer}/state_ids/{index}");
        }
        else if (predicate is "self_hp_at_or_below_basis_points" or "ally_hp_at_or_below_basis_points")
            RequireRange(diagnostics, context, condition.BasisPoints, 0, 10000, $"{pointer}/basis_points");
        else if (predicate == "nearest_enemy_distance_at_or_below")
            RequireNonNegative(diagnostics, context, condition.MaxDistance, $"{pointer}/max_distance");
        else if (predicate == "has_skill_affordance")
        {
            RequireIds(diagnostics, context, condition.Affordances, $"{pointer}/affordances", "affordance");
            foreach ((string value, int index) in condition.Affordances.Select((value, index) => (value, index)))
                if (!ValidAffordances.Contains(value))
                    Unsupported(diagnostics, context, $"Unsupported affordance '{value}'.", $"{pointer}/affordances/{index}");
        }
    }

    private static void AppendTemplateRuleDiagnostics(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EnemyTemplateJsonDto import
    )
    {
        if (import.BattleSpriteAssetId.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            Unsupported(diagnostics, context, "battle_sprite_asset_id must be a catalog ID, not a resource path.", "/battle_sprite_asset_id");
        RequireId(diagnostics, context, import.BrainId, "/brain_id", "brain_id");
        RequireAtLeast(diagnostics, context, import.BodySize, 1, "/body_size");
        if (!ValidHitDieSides.Contains(import.HitDieSides))
            Unsupported(diagnostics, context, $"Unsupported hit_die_sides '{import.HitDieSides}'.", "/hit_die_sides");
        if (!ValidCognitionKinds.Contains(import.CognitionKind))
            Unsupported(diagnostics, context, $"Unsupported cognition_kind '{import.CognitionKind}'.", "/cognition_kind");
        if (!ValidTargetRanks.Contains(import.TargetRank))
            Unsupported(diagnostics, context, $"Unsupported target_rank '{import.TargetRank}'.", "/target_rank");
        ValidateDistinctIds(diagnostics, context, import.Tags, "/tags", "tag");
        ValidateSaveTags(diagnostics, context, import.SaveAdvantageTags, "/save_advantage_tags");
        ValidateSaveTags(diagnostics, context, import.SaveDisadvantageTags, "/save_disadvantage_tags");
        ValidateSaveTags(diagnostics, context, import.SaveImmunityTags, "/save_immunity_tags");
        foreach ((string damageTag, string tier) in import.DamageResistances)
        {
            if (!ValidDamageTags.Contains(damageTag))
                Unsupported(diagnostics, context, $"Unsupported damage tag '{damageTag}'.", $"/damage_resistances/{damageTag}");
            if (!ValidMitigationTiers.Contains(tier))
                Unsupported(diagnostics, context, $"Unsupported mitigation tier '{tier}'.", $"/damage_resistances/{damageTag}");
        }
        foreach (string attributeId in BaseAttributeIds)
        {
            if (!import.BaseAttributeOverrides.TryGetValue(attributeId, out int value))
                Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"Missing base attribute '{attributeId}'.", $"/base_attribute_overrides/{attributeId}");
            else if (value <= 0)
                RangeError(diagnostics, context, $"Base attribute '{attributeId}' must be > 0.", $"/base_attribute_overrides/{attributeId}");
        }
        if (import.AttributeOverrides.ContainsKey("boss_target") || import.AttributeOverrides.ContainsKey("fortune_mark_target") || import.AttributeOverrides.ContainsKey("armor_class") || import.AttributeOverrides.ContainsKey("weapon_attack_range") || import.AttributeOverrides.ContainsKey("weapon_physical_damage_tag"))
            Unsupported(diagnostics, context, "attribute_overrides contains a field with a dedicated typed owner.", "/attribute_overrides");
        var declaredSkills = new HashSet<string>(import.SkillIds, StringComparer.Ordinal);
        foreach ((string skillId, int level) in import.SkillLevelMap)
        {
            if (!declaredSkills.Contains(skillId))
                Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"skill_level_map key '{skillId}' is not declared in skill_ids.", $"/skill_level_map/{skillId}");
            RequireAtLeast(diagnostics, context, level, 1, $"/skill_level_map/{skillId}");
        }
        bool isBeast = import.Tags.Contains("beast", StringComparer.Ordinal);
        if (isBeast)
        {
            RequireAtLeast(diagnostics, context, import.NaturalWeaponAttackRange, 1, "/natural_weapon_attack_range");
            if (!string.IsNullOrWhiteSpace(import.NaturalWeaponDamageTag) && !ValidDamageTags.Contains(import.NaturalWeaponDamageTag))
                Unsupported(diagnostics, context, $"Unsupported natural_weapon_damage_tag '{import.NaturalWeaponDamageTag}'.", "/natural_weapon_damage_tag");
        }
        else
            RequireId(diagnostics, context, import.AttackEquipmentItemId, "/attack_equipment_item_id", "attack_equipment_item_id");

        var equipmentSlots = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.BattleEquipmentEntries.Count; index += 1)
        {
            EnemyBattleEquipmentJsonDto entry = import.BattleEquipmentEntries[index];
            string pointer = $"/battle_equipment_entries/{index}";
            if (!ValidEquipmentSlots.Contains(entry.SlotId))
                Unsupported(diagnostics, context, $"Unsupported equipment slot '{entry.SlotId}'.", $"{pointer}/slot_id");
            else if (!equipmentSlots.Add(entry.SlotId))
                Add(diagnostics, context, EnemyContentImportRules.DuplicateId, $"Duplicate equipment slot '{entry.SlotId}'.", $"{pointer}/slot_id");
            RequireId(diagnostics, context, entry.ItemId, $"{pointer}/item_id", "item_id");
            RequireRange(diagnostics, context, entry.Rarity, 0, 4, $"{pointer}/rarity");
            if (entry.Rarity >= 0 && entry.Rarity <= 4 && !IsValidCurrentDurability(entry.CurrentDurability, entry.Rarity))
                RangeError(diagnostics, context, "current_durability is outside the rarity durability range.", $"{pointer}/current_durability");
        }
        for (int index = 0; index < import.DropEntries.Count; index += 1)
        {
            EnemyDropEntryJsonDto entry = import.DropEntries[index];
            string pointer = $"/drop_entries/{index}";
            RequireId(diagnostics, context, entry.DropEntryId, $"{pointer}/drop_entry_id", "drop_entry_id");
            if (entry.DropType is not ("item" or "random_equipment"))
                Unsupported(diagnostics, context, $"Unsupported drop_type '{entry.DropType}'.", $"{pointer}/drop_type");
            RequireId(diagnostics, context, entry.ItemId, $"{pointer}/item_id", "item_id");
            RequireAtLeast(diagnostics, context, entry.Quantity, 1, $"{pointer}/quantity");
        }
    }

    private static void AppendRosterRuleDiagnostics(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        EncounterRosterJsonDto import
    )
    {
        bool initialStageFound = false;
        for (int stageIndex = 0; stageIndex < import.Stages.Count; stageIndex += 1)
        {
            EncounterRosterStageJsonDto stage = import.Stages[stageIndex];
            string pointer = $"/stages/{stageIndex}";
            RequireNonNegative(diagnostics, context, stage.Stage, $"{pointer}/stage");
            if (stage.Stage == import.InitialStage)
                initialStageFound = true;
            if (stage.UnitEntries.Count == 0)
                Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, "Roster stage must declare at least one unit entry.", $"{pointer}/unit_entries");
            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            for (int unitIndex = 0; unitIndex < stage.UnitEntries.Count; unitIndex += 1)
            {
                EncounterRosterUnitJsonDto unit = stage.UnitEntries[unitIndex];
                string unitPointer = $"{pointer}/unit_entries/{unitIndex}";
                RequireId(diagnostics, context, unit.TemplateId, $"{unitPointer}/template_id", "template_id");
                RequireAtLeast(diagnostics, context, unit.Count, 1, $"{unitPointer}/count");
                if (!string.IsNullOrWhiteSpace(unit.ActorId))
                {
                    if (unit.Count != 1)
                        RangeError(diagnostics, context, "actor_id requires count == 1.", $"{unitPointer}/count");
                    if (!actorIds.Add(unit.ActorId))
                        Add(diagnostics, context, EnemyContentImportRules.DuplicateId, $"Duplicate actor_id '{unit.ActorId}' in the same stage.", $"{unitPointer}/actor_id");
                }
            }
        }
        if (import.Stages.Count > 0 && !initialStageFound)
            Add(diagnostics, context, EnemyContentImportRules.ReferenceMissing, $"initial_stage '{import.InitialStage}' is not declared.", "/initial_stage");
    }

    private static bool ActionKindMatchesPayload(string kind, EnemyAiActionPayloadJsonDto payload) =>
        (kind, payload) switch
        {
            ("move_to_advantage_position", MoveToAdvantagePositionActionPayloadJsonDto) => true,
            ("move_to_multi_unit_skill_position", MoveToMultiUnitSkillPositionActionPayloadJsonDto) => true,
            ("move_to_range", MoveToRangeActionPayloadJsonDto) => true,
            ("retreat", RetreatActionPayloadJsonDto) => true,
            ("use_charge", UseChargeActionPayloadJsonDto) => true,
            ("use_charge_path_aoe", UseChargePathAoeActionPayloadJsonDto) => true,
            ("use_ground_reposition_skill", UseGroundRepositionSkillActionPayloadJsonDto) => true,
            ("use_ground_skill", UseGroundSkillActionPayloadJsonDto) => true,
            ("use_multi_unit_skill", UseMultiUnitSkillActionPayloadJsonDto value) when value is not MoveToMultiUnitSkillPositionActionPayloadJsonDto => true,
            ("use_random_chain_skill", UseRandomChainSkillActionPayloadJsonDto) => true,
            ("use_unit_skill", UseUnitSkillActionPayloadJsonDto) => true,
            ("wait", WaitActionPayloadJsonDto) => true,
            _ => false,
        };

    private static IReadOnlyList<string> GetDeclaredSkillIds(EnemyAiActionPayloadJsonDto payload) =>
        payload switch
        {
            MoveToAdvantagePositionActionPayloadJsonDto value => value.RangeSkillIds,
            MoveToMultiUnitSkillPositionActionPayloadJsonDto value => value.SkillIds,
            MoveToRangeActionPayloadJsonDto value => value.RangeSkillIds,
            UseChargeActionPayloadJsonDto value => new[] { value.SkillId },
            UseChargePathAoeActionPayloadJsonDto value => value.SkillIds,
            UseGroundRepositionSkillActionPayloadJsonDto value => value.SkillIds,
            UseGroundSkillActionPayloadJsonDto value => value.SkillIds,
            UseMultiUnitSkillActionPayloadJsonDto value => value.SkillIds,
            UseRandomChainSkillActionPayloadJsonDto value => value.SkillIds,
            UseUnitSkillActionPayloadJsonDto value => value.SkillIds,
            _ => Array.Empty<string>(),
        };

    private static void ValidateSaveTags(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, IReadOnlyList<string> values, string pointer)
    {
        ValidateDistinctIds(diagnostics, context, values, pointer, "save tag");
        for (int index = 0; index < values.Count; index += 1)
            if (!ValidSaveTags.Contains(values[index]))
                Unsupported(diagnostics, context, $"Unsupported save tag '{values[index]}'.", $"{pointer}/{index}");
    }

    private static void RequireSupportedSelector(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value, string pointer, bool allowEmpty)
    {
        if (!(allowEmpty && value.Length == 0) && !ValidTargetSelectors.Contains(value))
            Unsupported(diagnostics, context, $"Unsupported target_selector '{value}'.", pointer);
    }

    private static void RequireEnemyFocusSelector(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value, string pointer)
    {
        if (!EnemyFocusTargetSelectors.Contains(value))
            Unsupported(diagnostics, context, $"Unsupported enemy-focus target_selector '{value}'.", pointer);
    }

    private static void RequireDistanceReference(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value, string pointer, params string[] allowed)
    {
        if (!allowed.Contains(value, StringComparer.Ordinal))
            Unsupported(diagnostics, context, $"Unsupported distance_reference '{value}' for this action kind.", $"{pointer}/payload/distance_reference");
    }

    private static bool IsValidCurrentDurability(int value, int rarity)
    {
        int maximum = rarity switch
        {
            1 => 84,
            2 => 120,
            3 => 160,
            4 => 200,
            _ => 56,
        };
        return value >= 1 && value <= maximum;
    }

    private static void ValidateDistanceBand(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, int minimum, int maximum, string actionPointer)
    {
        RequireNonNegative(diagnostics, context, minimum, $"{actionPointer}/payload/desired_min_distance");
        if (maximum < minimum)
            RangeError(diagnostics, context, "desired_max_distance must be >= desired_min_distance.", $"{actionPointer}/payload/desired_max_distance");
    }

    private static void RequireIds(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, IReadOnlyList<string> values, string pointer, string label)
    {
        if (values.Count == 0)
            Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, $"At least one {label} is required.", pointer);
        ValidateDistinctIds(diagnostics, context, values, pointer, label);
    }

    private static void ValidateDistinctIds(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, IEnumerable<string> values, string pointer, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(diagnostics, context, EnemyContentImportRules.IdRequired, $"{label} must not be empty.", $"{pointer}/{index}");
            else if (!seen.Add(value))
                Add(diagnostics, context, EnemyContentImportRules.DuplicateId, $"Duplicate {label} '{value}'.", $"{pointer}/{index}");
            index += 1;
        }
    }

    private static void RequireId(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value, string pointer, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(diagnostics, context, EnemyContentImportRules.IdRequired, $"{label} is required.", pointer);
    }

    private static void RequireNonNegative(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, int value, string pointer) =>
        RequireAtLeast(diagnostics, context, value, 0, pointer);

    private static void RequireAtLeast(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, int value, int minimum, string pointer)
    {
        if (value < minimum)
            RangeError(diagnostics, context, $"Value must be >= {minimum}; got {value}.", pointer);
    }

    private static void RequireRange(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, int value, int minimum, int maximum, string pointer)
    {
        if (value < minimum || value > maximum)
            RangeError(diagnostics, context, $"Value must be within [{minimum}, {maximum}]; got {value}.", pointer);
    }

    private static void Unsupported(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) =>
        Add(diagnostics, context, EnemyContentImportRules.ValueUnsupported, message, pointer);

    private static void RangeError(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) =>
        Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, message, pointer);
}

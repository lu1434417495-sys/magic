using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_ability_content_registry_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAuthoringAbiAttributesAndRuntimeDtoBoundary();
        TestBuiltInHandlerSpecsExposeStaticValidationMetadata();
        TestEmptyAndMinimalValidPacksBuildAndFindBindings();
        TestDependencyOrderedReplaceBinding();
        TestReplaceBindingRejectsUnrelatedBindingIdCollision();
        TestLifecycleSnapshotDoesNotRetainResourceMutations();
        TestFailedRebuildKeepsLastSuccessfulSnapshot();
        TestInvalidContentFailsFastWithStableCodesAndPaths();

        RequestTestExit(_test.Finish("Equipment ability content registry regression"));
    }

    private void TestAuthoringAbiAttributesAndRuntimeDtoBoundary()
    {
        var authoringAbi = new Dictionary<Type, string[]>
        {
            [typeof(EquipmentAbilityContentPackDef)] =
                new[] { "pack_id", "schema_version", "load_order", "dependencies", "bindings" },
            [typeof(EquipmentAbilityBindingDef)] =
                new[]
                {
                    "binding_id",
                    "trait_id",
                    "override_mode",
                    "replaces_binding_id",
                    "allowed_source_kinds",
                    "required_trait_categories",
                    "required_item_tags",
                    "supported_equipment_type_ids",
                    "state_schemas",
                    "reactions",
                    "granted_actions",
                    "weapon_profile_overlays",
                    "world_effects",
                },
            [typeof(EquipmentAbilityReactionDef)] =
                new[]
                {
                    "reaction_id",
                    "trigger",
                    "timing",
                    "priority",
                    "once_scope",
                    "requires_player_confirmation",
                    "condition_group",
                    "roll_gate",
                    "outcome_table",
                    "actions",
                },
            [typeof(EquipmentAbilityConditionGroupDef)] =
                new[] { "mode", "negate", "conditions", "groups" },
            [typeof(EquipmentAbilityConditionDef)] =
                new[] { "condition_id", "kind", "payload" },
            [typeof(HasStatusConditionPayloadDef)] = new[] { "subject", "status_id" },
            [typeof(CompareFactConditionPayloadDef)] = new[] { "left", "compare", "right" },
            [typeof(HasEquipmentTagConditionPayloadDef)] =
                new[] { "subject", "equipment_selector", "all_tags", "any_tags" },
            [typeof(EquipmentAbilityFactQueryDef)] =
                new[]
                {
                    "query_kind",
                    "fact_id",
                    "subject",
                    "status_id",
                    "aggregation",
                    "value_kind",
                    "bool_literal",
                    "int_literal",
                    "float_literal",
                    "string_name_literal",
                },
            [typeof(DiceExpressionDef)] = new[] { "terms", "flat_bonus", "preview_policy" },
            [typeof(DiceExpressionTermDef)] =
                new[]
                {
                    "dice_count",
                    "dice_sides",
                    "count_bonus_fact",
                    "count_bonus_multiplier",
                    "max_dice_count",
                },
            [typeof(EquipmentAbilityActionDef)] =
                new[] { "action_id", "kind", "payload", "condition_group", "roll_gate" },
            [typeof(AddDamageDiceActionPayloadDef)] =
                new[] { "target_selector", "dice", "damage_type", "damage_tags" },
            [typeof(ApplyStatusActionPayloadDef)] =
                new[]
                {
                    "target_selector",
                    "status_id",
                    "duration_turns",
                    "duration_tu",
                    "stack_delta",
                    "stack_behavior",
                    "stack_limit",
                    "display_label",
                    "attack_roll_penalty",
                    "source_bound_attack_roll_penalty",
                    "source_bound_attack_roll_penalty_min_stacks",
                    "forced_move_immune",
                    "counts_as_debuff_override",
                    "counts_as_debuff",
                    "undispellable",
                    "dispellable_magic",
                    "dispellable_harmful_magic",
                    "dispellable_beneficial_magic",
                    "lock_counterattack",
                    "lock_guard",
                    "lock_dodge_bonus",
                    "tick_interval_tu",
                    "timeline_damage_dice_count",
                    "timeline_damage_dice_sides",
                    "timeline_damage_flat_bonus",
                    "save_dc",
                    "save_ability",
                    "save_tag",
                    "apply_on_save_failure",
                },
            [typeof(ScheduleAreaEffectActionPayloadDef)] =
                new[]
                {
                    "anchor_selector",
                    "delay_tu",
                    "terrain_effect_id",
                    "area_pattern",
                    "area_value",
                    "lifetime_policy",
                    "effect_type",
                    "target_team_filter",
                    "stack_behavior",
                    "display_name",
                    "render_overlay_id",
                    "overlay_priority",
                    "contact_status_id",
                    "contact_status_duration_tu",
                    "contact_stack_behavior",
                    "contact_stack_limit",
                    "contact_status_display_label",
                    "contact_counts_as_debuff_override",
                    "contact_counts_as_debuff",
                    "contact_undispellable",
                    "contact_dispellable_magic",
                    "contact_dispellable_harmful_magic",
                    "contact_dispellable_beneficial_magic",
                    "contact_save_dc",
                    "contact_save_ability",
                    "contact_save_tag",
                    "contact_apply_on_save_failure",
                    "contact_tick_interval_tu",
                    "contact_timeline_damage_dice_count",
                    "contact_timeline_damage_dice_sides",
                    "contact_timeline_damage_flat_bonus",
                    "contact_blocked_by_trait_id",
                },
            [typeof(ApplyBattleTerrainEffectAfterCheckActionPayloadDef)] =
                new[]
                {
                    "anchor_selector",
                    "terrain_effect_id",
                    "move_cost_delta",
                    "target_team_filter",
                    "stack_behavior",
                    "display_name",
                    "render_overlay_id",
                    "overlay_priority",
                    "check_attribute_modifier_id",
                    "check_compare",
                    "check_threshold",
                    "natural_twenty_auto_success",
                    "natural_one_auto_failure",
                },
            [typeof(ModifyAbilityStateActionPayloadDef)] =
                new[]
                {
                    "target_selector",
                    "binding_id",
                    "state_key",
                    "operation",
                    "int_delta",
                },
            [typeof(MarkTargetActionPayloadDef)] =
                new[]
                {
                    "target_selector",
                    "state_key",
                    "stack_delta",
                    "remove_on_source_missing",
                    "remove_on_target_defeated",
                },
            [typeof(GrantSkillActionPayloadDef)] =
                new[] { "skill_id", "skill_level", "availability_state_key" },
            [typeof(SummonUnitsActionPayloadDef)] =
                new[]
                {
                    "anchor_selector",
                    "state_key",
                    "count_dice",
                    "max_living_units",
                    "duration_tu",
                    "spawn_radius",
                    "unit_id_prefix",
                    "unit_display_name",
                    "body_size_category",
                    "control_mode",
                    "ai_brain_id",
                    "ai_state_id",
                    "hp_max",
                    "armor_class",
                    "attack_bonus",
                    "base_attack_bonus",
                    "action_points",
                    "move_points",
                    "known_active_skill_ids",
                    "natural_weapon_profile_type_id",
                    "natural_weapon_damage_tag",
                    "natural_weapon_attack_range",
                    "natural_weapon_damage_dice",
                    "natural_weapon_family",
                    "creature_type_tags",
                    "movement_tags",
                },
            [typeof(EquipmentSlotWeightDef)] = new[] { "slot_id", "weight" },
            [typeof(EquipmentDurabilityDamageActionPayloadDef)] =
                new[]
                {
                    "target_selector",
                    "target_slots",
                    "slot_weights",
                    "required_item_tags",
                    "required_equipment_type_ids",
                    "durability_loss",
                    "save_tag",
                    "save_dc",
                    "require_attack_success",
                    "max_damaged_items",
                },
            [typeof(EquipmentAttackDefenseModifierDef)] =
                new[]
                {
                    "modifier_id",
                    "ignored_ac_components",
                    "ac_component_multipliers",
                    "lock_dodge_bonus",
                    "required_target_equipment_selector",
                    "required_target_item_tags",
                    "required_target_equipment_type_ids",
                    "cover_policy",
                    "projectile_obstacle_policy",
                    "trace_label",
                },
            [typeof(EquipmentAcComponentMultiplierDef)] =
                new[] { "ac_component_id", "multiplier_percent", "stack_mode" },
            [typeof(EquipmentWeaponProfileOverlayDef)] =
                new[]
                {
                    "overlay_id",
                    "priority",
                    "condition_group",
                    "require_equipped_weapon",
                    "required_weapon_families",
                    "required_weapon_type_ids",
                    "attack_range_delta",
                    "min_attack_range",
                    "max_attack_range",
                    "one_handed_dice_overlay",
                    "two_handed_dice_overlay",
                    "physical_damage_tag_override",
                    "grip_override",
                    "uses_two_hands_override",
                    "is_versatile_override",
                },
            [typeof(EquipmentWeaponDiceOverlayDef)] =
                new[]
                {
                    "mode",
                    "dice_count_delta",
                    "dice_sides_override",
                    "flat_bonus_delta",
                    "dice_override",
                },
            [typeof(EquipmentRollGateDef)] = new[] { "rng_stream", "roll", "compare", "threshold" },
            [typeof(EquipmentOutcomeTableDef)] = new[] { "table_id", "roll", "entries" },
            [typeof(EquipmentOutcomeEntryDef)] = new[] { "min_roll", "max_roll", "actions" },
            [typeof(EquipmentAbilityStateSchemaDef)] =
                new[]
                {
                    "state_key",
                    "owner_scope",
                    "value_kind",
                    "initial_int_value",
                    "max_int_value",
                    "reset_timing",
                    "persist_outside_battle",
                    "visible_to_ui",
                    "sync_source_state_key",
                    "sync_aggregation",
                    "sync_int_literal",
                },
            [typeof(EquipmentGrantedActionDef)] =
                new[]
                {
                    "granted_action_id",
                    "granted_kind",
                    "skill_id",
                    "skill_level",
                    "display_category",
                    "display_priority",
                    "availability_conditions",
                },
            [typeof(EquipmentWorldEffectDef)] =
                new[] { "world_effect_id", "trigger", "timing", "condition_group", "actions" },
        };

        foreach ((Type type, string[] memberNames) in authoringAbi)
        {
            _test.True(
                type.GetCustomAttribute<GlobalClassAttribute>() != null,
                $"{type.Name} should be a [GlobalClass] authoring Resource."
            );
            _test.True(
                typeof(Resource).IsAssignableFrom(type),
                $"{type.Name} should derive from Resource."
            );
            foreach (string memberName in memberNames)
            {
                MemberInfo member = FindPublicInstanceMember(type, memberName);
                _test.True(member != null, $"{type.Name}.{memberName} should exist.");
                if (member == null)
                    continue;
                _test.True(
                    member.GetCustomAttribute<ExportAttribute>() != null,
                    $"{type.Name}.{memberName} should be [Export]."
                );
            }
        }

        var runtimeTypes = new[]
        {
            typeof(EquipmentAbilityContentPackDefinition),
            typeof(EquipmentAbilityBindingDefinition),
            typeof(EquipmentAbilityReactionDefinition),
            typeof(EquipmentAbilityConditionDefinition),
            typeof(EquipmentConditionGroupDefinition),
            typeof(EquipmentAbilityActionDefinition),
            typeof(EquipmentGrantedActionDefinition),
            typeof(EquipmentWeaponProfileOverlayDefinition),
            typeof(EquipmentWorldEffectDefinition),
            typeof(EquipmentAbilityStateSchemaDefinition),
            typeof(EquipmentAbilityRegistryBuildResult),
            typeof(EquipmentAbilityContentValidationContext),
            typeof(EquipmentAbilityHandlerSpec),
        };

        foreach (Type type in runtimeTypes)
        {
            _test.True(
                type.GetCustomAttribute<GlobalClassAttribute>() == null,
                $"{type.Name} should not be a Godot [GlobalClass]."
            );
            _test.True(
                !typeof(Resource).IsAssignableFrom(type),
                $"{type.Name} should be a plain C# runtime type, not a Resource."
            );
            AssertRuntimeTypeHasNoResourceOrGodotDictionaryMembers(type);
        }
    }

    private void TestBuiltInHandlerSpecsExposeStaticValidationMetadata()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> conditionSpecs =
            registry.GetConditionHandlerSpecsTyped();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> actionSpecs =
            registry.GetActionHandlerSpecsTyped();

        AssertContainsKey(conditionSpecs, "has_status", "condition specs");
        AssertContainsKey(conditionSpecs, "compare_fact", "condition specs");
        AssertContainsKey(conditionSpecs, "has_equipment_tag", "condition specs");

        AssertContainsKey(actionSpecs, "add_damage_dice", "action specs");
        AssertContainsKey(actionSpecs, "apply_status", "action specs");
        AssertContainsKey(actionSpecs, "modify_ability_state", "action specs");
        AssertContainsKey(actionSpecs, "mark_target", "action specs");
        AssertContainsKey(actionSpecs, "grant_skill", "action specs");
        AssertContainsKey(actionSpecs, "equipment_durability_damage", "action specs");
        AssertContainsKey(actionSpecs, "apply_battle_terrain_effect_after_check", "action specs");
        _test.False(
            actionSpecs.ContainsKey("weapon_profile_overlay"),
            "weapon profile overlay should stay projection-only, not a normal action handler."
        );

        EquipmentAbilityHandlerSpec modifyState = actionSpecs["modify_ability_state"];
        _test.True(
            modifyState.StateAccess.Writes.Count > 0
                && modifyState.StateAccess.Writes[0].StateKeyMustBeDeclaredInBinding,
            "modify_ability_state should declare binding-local state writes."
        );
        _test.True(
            actionSpecs["mark_target"].StateAccess.Writes.Count > 0
                && actionSpecs["mark_target"].StateAccess.Writes[0].StateKeyMustBeDeclaredInBinding,
            "mark_target should declare binding-local state writes."
        );
        _test.True(
            actionSpecs["equipment_durability_damage"].SupportsConsumer(
                EquipmentAbilityConsumerKind.Preview
            ),
            "durability action spec should expose preview support metadata."
        );
        _test.False(
            actionSpecs["apply_battle_terrain_effect_after_check"].SupportsConsumer(
                EquipmentAbilityConsumerKind.Preview
            ),
            "terrain break after-check should stay execution-only because it mutates battle terrain."
        );
        _test.Eq(
            modifyState.StateAccess.Writes[0].StateKeyPayloadMemberName,
            "state_key",
            "state access metadata should identify the payload field carrying the binding state key."
        );

        IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> triggerSpecs =
            registry.GetTriggerTimingSpecsTyped();
        _test.True(
            triggerSpecs[EquipmentAbilityTriggerKind.OnHit].AllowedTimings.Contains(
                EquipmentAbilityTimingKind.AfterHit
            )
                && !triggerSpecs[EquipmentAbilityTriggerKind.OnHit].AllowedTimings.Contains(
                    EquipmentAbilityTimingKind.AfterBattle
                ),
            "on_hit trigger metadata should reject after_battle timing."
        );
        _test.True(
            triggerSpecs[EquipmentAbilityTriggerKind.OnBattleEnd].AllowedTimings.Contains(
                EquipmentAbilityTimingKind.AfterBattle
            )
                && !triggerSpecs[EquipmentAbilityTriggerKind.OnBattleEnd].AllowedTimings.Contains(
                    EquipmentAbilityTimingKind.AfterHit
                ),
            "on_battle_end trigger metadata should only allow after_battle timing."
        );
    }

    private void TestEmptyAndMinimalValidPacksBuildAndFindBindings()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityRegistryBuildResult emptyResult =
            registry.Rebuild(Array.Empty<EquipmentAbilityContentPackDef>(), BuildValidationContext());

        _test.True(emptyResult.Success, $"empty pack list should succeed: {FormatErrors(emptyResult.Errors)}");
        _test.Eq(registry.GetPackDefinitionsTyped().Count, 0, "empty rebuild should expose no packs.");
        _test.Eq(
            registry.GetBindingDefinitionsTyped().Count,
            0,
            "empty rebuild should expose no bindings."
        );

        EquipmentAbilityContentPackDef pack = BuildValidPack();
        EquipmentAbilityRegistryBuildResult result =
            registry.Rebuild(new[] { pack }, BuildValidationContext());

        _test.True(result.Success, $"minimal valid pack should build: {FormatErrors(result.Errors)}");
        _test.Eq(result.Revision, registry.GetRevision(), "build result should report registry revision.");
        _test.True(
            registry.GetPackDefinitionsTyped().ContainsKey("pack.core"),
            "registry should expose typed pack snapshot by pack id."
        );
        _test.True(
            registry.GetBindingDefinitionsTyped().ContainsKey("binding.weapon.flame"),
            "registry should expose typed binding snapshot by binding id."
        );

        IReadOnlyList<EquipmentAbilityBindingDefinition> matches = registry.FindBindings(
            "trait.weapon.flame",
            TraitSourceKind.EquipmentFixed,
            new HashSet<StringName> { "weapon_feat" },
            BuildSourceItem("test_blade", "blade", "weapon")
        );
        _test.Eq(matches.Count, 1, "FindBindings should match trait/source/category/item facts.");
        _test.Eq(
            matches[0].BindingId,
            new StringName("binding.weapon.flame"),
            "FindBindings should return the matching DTO binding."
        );

        IReadOnlyList<EquipmentAbilityBindingDefinition> wrongTag = registry.FindBindings(
            "trait.weapon.flame",
            TraitSourceKind.EquipmentFixed,
            new HashSet<StringName> { "weapon_feat" },
            BuildSourceItem("test_blade", "cloth", "weapon")
        );
        _test.Eq(wrongTag.Count, 0, "FindBindings should reject source items missing required tags.");
    }

    private void TestDependencyOrderedReplaceBinding()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityContentPackDef basePack = BuildValidPack("base_pack", "base.binding");
        EquipmentAbilityContentPackDef replacementPack =
            BuildValidPack("mod_pack", "mod.binding", loadOrder: 0);
        replacementPack.dependencies.Add("base_pack");
        replacementPack.bindings[0].override_mode = "replace_binding";
        replacementPack.bindings[0].replaces_binding_id = "base.binding";

        EquipmentAbilityRegistryBuildResult result =
            registry.Rebuild(new[] { replacementPack, basePack }, BuildValidationContext());

        _test.True(
            result.Success,
            $"dependency topo should load base before replacement: {FormatErrors(result.Errors)}"
        );
        _test.False(
            registry.GetBindingDefinitionsTyped().ContainsKey("base.binding"),
            "replace_binding should remove the replaced binding from the active typed snapshot."
        );
        _test.True(
            registry.GetBindingDefinitionsTyped().ContainsKey("mod.binding"),
            "replace_binding should publish the replacement binding."
        );
    }

    private void TestReplaceBindingRejectsUnrelatedBindingIdCollision()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityContentPackDef basePack = BuildValidPack("base_pack", "base.binding", loadOrder: 0);
        EquipmentAbilityContentPackDef otherPack =
            BuildValidPack("other_pack", "other.binding", loadOrder: 1);
        EquipmentAbilityContentPackDef replacementPack =
            BuildValidPack("mod_pack", "other.binding", loadOrder: 2);
        replacementPack.dependencies.Add("base_pack");
        replacementPack.dependencies.Add("other_pack");
        replacementPack.bindings[0].override_mode = "replace_binding";
        replacementPack.bindings[0].replaces_binding_id = "base.binding";

        EquipmentAbilityRegistryBuildResult result =
            registry.Rebuild(new[] { replacementPack, otherPack, basePack }, BuildValidationContext());

        _test.False(
            result.Success,
            "replace_binding should reject a replacement binding_id that collides with an unrelated loaded binding."
        );
        AssertErrorContains(
            result.Errors,
            "EQA_BINDING_REPLACE_ID_COLLISION",
            "other.binding"
        );
        _test.False(
            registry.GetBindingDefinitionsTyped().ContainsKey("other.binding"),
            "failed replacement rebuild should not publish a partially overwritten binding index."
        );
    }

    private void TestLifecycleSnapshotDoesNotRetainResourceMutations()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityContentPackDef pack = BuildValidPack();
        EquipmentAbilityRegistryBuildResult result =
            registry.Rebuild(new[] { pack }, BuildValidationContext());
        _test.True(result.Success, $"valid pack should build before mutation: {FormatErrors(result.Errors)}");

        EquipmentAbilityBindingDefinition snapshot =
            registry.GetBindingDefinitionsTyped()["binding.weapon.flame"];
        pack.pack_id = "mutated_pack";
        pack.dependencies.Add("unexpected_dependency");
        EquipmentAbilityBindingDef bindingResource = pack.bindings[0];
        bindingResource.binding_id = "mutated.binding";
        bindingResource.required_item_tags.Clear();
        ((AddDamageDiceActionPayloadDef)bindingResource.reactions[0].actions[0].payload)
            .damage_type = "mutated_damage";

        _test.True(
            registry.GetPackDefinitionsTyped().ContainsKey("pack.core"),
            "pack DTO snapshot should retain original pack id after Resource mutation."
        );
        _test.True(
            registry.GetBindingDefinitionsTyped().ContainsKey("binding.weapon.flame"),
            "binding DTO snapshot should retain original binding id after Resource mutation."
        );
        _test.True(
            snapshot.RequiredItemTags.Contains("blade"),
            "binding DTO should retain copied required item tags after Resource mutation."
        );
        _test.False(
            snapshot.RequiredItemTags is ISet<StringName>,
            "binding DTO required item tags should not expose a mutable set implementation."
        );
        _test.Eq(
            registry.FindBindings(
                "trait.weapon.flame",
                TraitSourceKind.EquipmentFixed,
                new HashSet<StringName> { "weapon_feat" },
                BuildSourceItem("test_blade", "blade", "weapon")
            ).Count,
            1,
            "registry lookup should keep using DTO snapshots, not mutated authoring Resources."
        );
    }

    private void TestFailedRebuildKeepsLastSuccessfulSnapshot()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityRegistryBuildResult validResult =
            registry.Rebuild(new[] { BuildValidPack() }, BuildValidationContext());
        _test.True(validResult.Success, $"valid pack should build: {FormatErrors(validResult.Errors)}");
        int successfulRevision = registry.GetRevision();

        EquipmentAbilityRegistryBuildResult failedResult =
            registry.Rebuild(new[] { BuildInvalidPack() }, BuildValidationContext());

        _test.False(failedResult.Success, "invalid rebuild should fail.");
        _test.True(
            failedResult.Revision > successfulRevision,
            "failed rebuild should still advance the registry build revision."
        );
        _test.True(
            registry.GetBindingDefinitionsTyped().ContainsKey("binding.weapon.flame"),
            "failed rebuild should preserve the previous successful binding snapshot."
        );
        _test.False(
            registry.GetBindingDefinitionsTyped().ContainsKey("bad.unknown_action"),
            "failed rebuild should not publish invalid partial bindings."
        );
        _test.Eq(
            registry.FindBindings(
                "trait.weapon.flame",
                TraitSourceKind.EquipmentFixed,
                new HashSet<StringName> { "weapon_feat" },
                BuildSourceItem("test_blade", "blade", "weapon")
            ).Count,
            1,
            "failed rebuild should keep lookup behavior on the last successful snapshot."
        );
    }

    private void TestInvalidContentFailsFastWithStableCodesAndPaths()
    {
        var registry = new EquipmentAbilityContentRegistry(new TestContentResourceLoader());
        EquipmentAbilityRegistryBuildResult result =
            registry.Rebuild(new[] { BuildInvalidPack() }, BuildValidationContext());

        _test.False(result.Success, "invalid pack should fail registry build.");
        _test.False(
            registry.GetBindingDefinitionsTyped().ContainsKey("bad.unknown_action"),
            "invalid bindings should not enter the active typed index."
        );

        AssertErrorContains(result.Errors, "EQA_REFERENCE_MISSING_TRAIT", "bad.missing_trait");
        AssertErrorContains(result.Errors, "EQA_TRIGGER_UNKNOWN_ID", "bad.unknown_trigger");
        AssertErrorContains(result.Errors, "EQA_TIMING_UNKNOWN_ID", "bad.unknown_timing");
        AssertErrorContains(result.Errors, "EQA_HANDLER_UNKNOWN_ID", "bad.unknown_condition");
        AssertErrorContains(result.Errors, "EQA_HANDLER_UNKNOWN_ID", "bad.unknown_action");
        AssertErrorContains(
            result.Errors,
            "EQA_HANDLER_PAYLOAD_TYPE_MISMATCH",
            "bad.payload_mismatch"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_ACTION_REQUIRED_FIELD_MISSING",
            "bad.missing_action_field"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_ACTION_REQUIRED_FIELD_MISSING",
            "bad.missing_terrain_check_field"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_COMPARE_OPERATOR_INVALID",
            "bad.invalid_terrain_check_compare"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_MOVE_COST_DELTA_INVALID",
            "bad.invalid_terrain_move_cost"
        );
        AssertErrorContains(result.Errors, "EQA_REFERENCE_UNKNOWN_STATUS", "bad.unknown_status");
        AssertErrorContains(result.Errors, "EQA_REFERENCE_UNKNOWN_SLOT", "bad.unknown_slot");
        AssertErrorContains(result.Errors, "EQA_SLOT_WEIGHT_INVALID", "bad.invalid_slot_weight");
        AssertErrorContains(
            result.Errors,
            "EQA_REFERENCE_UNKNOWN_SLOT",
            "bad.unknown_slot_weight"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_SLOT_WEIGHT_DUPLICATE",
            "bad.duplicate_slot_weight"
        );
        AssertErrorContains(result.Errors, "EQA_STATE_KEY_UNDECLARED", "bad.undeclared_state");
        AssertErrorContains(
            result.Errors,
            "EQA_STATE_SYNC_SOURCE_UNDECLARED",
            "bad.undeclared_sync_state"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_STATE_SYNC_INVALID",
            "bad.invalid_sync_divisor"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_STATE_PERSISTENT_OWNER_INVALID",
            "bad.invalid_persistent_state"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_GRANTED_SKILL_COMPOSITION_INVALID",
            "bad.invalid_grant"
        );
        AssertErrorContains(result.Errors, "EQA_REFERENCE_UNKNOWN_SKILL", "bad.invalid_grant");
        AssertErrorContains(
            result.Errors,
            "EQA_REACTION_CONFIRMATION_UNSUPPORTED",
            "bad.confirmation"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_BATTLE_END_MUTATION_UNSUPPORTED",
            "bad.battle_end"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_TRIGGER_TIMING_UNSUPPORTED",
            "bad.hit_after_battle"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_TRIGGER_TIMING_UNSUPPORTED",
            "bad.battle_end_after_hit"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_HANDLER_UNKNOWN_ID",
            "bad.outcome_unknown_action"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_HANDLER_UNKNOWN_ID",
            "bad.grant_availability_condition"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_HANDLER_UNKNOWN_ID",
            "bad.overlay_unknown_condition"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_TRIGGER_UNKNOWN_ID",
            "bad.world_unknown_trigger"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_HANDLER_UNKNOWN_ID",
            "bad.world_unknown_action"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_GRANTED_KIND_UNSUPPORTED",
            "bad.invalid_grant_kind"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_REFERENCE_UNKNOWN_SKILL",
            "bad.summon_unknown_skill"
        );
        AssertErrorContains(
            result.Errors,
            "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
            "bad.summon_unknown_damage"
        );
    }

    private static EquipmentAbilityContentPackDef BuildValidPack(
        StringName packId = default,
        StringName bindingId = default,
        int loadOrder = 10
    )
    {
        if (packId == default)
            packId = "pack.core";
        if (bindingId == default)
            bindingId = "binding.weapon.flame";

        EquipmentAbilityContentPackDef pack = new()
        {
            pack_id = packId,
            schema_version = 1,
            load_order = loadOrder,
        };
        EquipmentAbilityBindingDef binding = new()
        {
            binding_id = bindingId,
            trait_id = "trait.weapon.flame",
            override_mode = "add",
        };
        binding.allowed_source_kinds.Add("equipment_fixed");
        binding.required_trait_categories.Add("weapon_feat");
        binding.required_item_tags.Add("blade");
        binding.supported_equipment_type_ids.Add("weapon");
        binding.reactions.Add(
            new EquipmentAbilityReactionDef
            {
                reaction_id = "reaction.on_hit",
                trigger = "on_hit",
                timing = "after_hit",
                actions =
                {
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.fire_dice",
                        kind = "add_damage_dice",
                        payload = new AddDamageDiceActionPayloadDef
                        {
                            target_selector = "attack_target",
                            damage_type = "physical_slash",
                            dice = new DiceExpressionDef
                            {
                                terms =
                                {
                                    new DiceExpressionTermDef
                                    {
                                        dice_count = 1,
                                        dice_sides = 6,
                                    },
                                },
                            },
                        },
                    },
                },
            }
        );
        pack.bindings.Add(binding);
        return pack;
    }

    private static EquipmentAbilityContentPackDef BuildInvalidPack()
    {
        EquipmentAbilityContentPackDef pack = new()
        {
            pack_id = "bad_pack",
            schema_version = 1,
        };

        pack.bindings.Add(
            BuildBinding(
                bindingId: "bad.missing_trait",
                traitId: "missing_trait",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.valid",
                    trigger = "on_hit",
                    timing = "after_hit",
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_trigger",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.unknown_trigger",
                    trigger = "on_planet_align",
                    timing = "after_hit",
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_timing",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.unknown_timing",
                    trigger = "on_hit",
                    timing = "during_moonrise",
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_condition",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.unknown_condition",
                    trigger = "on_hit",
                    timing = "after_hit",
                    condition_group = new EquipmentAbilityConditionGroupDef
                    {
                        mode = "all",
                        conditions =
                        {
                            new EquipmentAbilityConditionDef
                            {
                                condition_id = "condition.unknown",
                                kind = "unknown_condition",
                            },
                        },
                    },
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_action",
                reaction: ReactionWithAction(
                    "reaction.unknown_action",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.unknown",
                        kind = "unknown_action",
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.payload_mismatch",
                reaction: ReactionWithAction(
                    "reaction.payload_mismatch",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.payload_mismatch",
                        kind = "apply_status",
                        payload = new AddDamageDiceActionPayloadDef(),
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.missing_action_field",
                reaction: ReactionWithAction(
                    "reaction.missing_action_field",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.missing_field",
                        kind = "add_damage_dice",
                        payload = new AddDamageDiceActionPayloadDef
                        {
                            target_selector = "attack_target",
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.missing_terrain_check_field",
                reaction: ReactionWithAction(
                    "reaction.missing_terrain_check_field",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.missing_terrain_check_field",
                        kind = "apply_battle_terrain_effect_after_check",
                        payload = new ApplyBattleTerrainEffectAfterCheckActionPayloadDef
                        {
                            anchor_selector = "attack_target",
                            terrain_effect_id = "broken_ground",
                            move_cost_delta = 1,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.invalid_terrain_check_compare",
                reaction: ReactionWithAction(
                    "reaction.invalid_terrain_check_compare",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.invalid_terrain_check_compare",
                        kind = "apply_battle_terrain_effect_after_check",
                        payload = new ApplyBattleTerrainEffectAfterCheckActionPayloadDef
                        {
                            anchor_selector = "attack_target",
                            terrain_effect_id = "broken_ground",
                            move_cost_delta = 1,
                            check_attribute_modifier_id = "strength_modifier",
                            check_compare = "nearly",
                            check_threshold = 22,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.invalid_terrain_move_cost",
                reaction: ReactionWithAction(
                    "reaction.invalid_terrain_move_cost",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.invalid_terrain_move_cost",
                        kind = "apply_battle_terrain_effect_after_check",
                        payload = new ApplyBattleTerrainEffectAfterCheckActionPayloadDef
                        {
                            anchor_selector = "attack_target",
                            terrain_effect_id = "broken_ground",
                            move_cost_delta = 0,
                            check_attribute_modifier_id = "strength_modifier",
                            check_compare = "gt",
                            check_threshold = 22,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_status",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.unknown_status",
                    trigger = "on_hit",
                    timing = "after_hit",
                    condition_group = new EquipmentAbilityConditionGroupDef
                    {
                        mode = "all",
                        conditions =
                        {
                            new EquipmentAbilityConditionDef
                            {
                                condition_id = "condition.status",
                                kind = "has_status",
                                payload = new HasStatusConditionPayloadDef
                                {
                                    subject = "target",
                                    status_id = "missing_status",
                                },
                            },
                        },
                    },
                    actions =
                    {
                        new EquipmentAbilityActionDef
                        {
                            action_id = "action.status",
                            kind = "apply_status",
                            payload = new ApplyStatusActionPayloadDef
                            {
                                target_selector = "attack_target",
                                status_id = "missing_status",
                                duration_turns = 1,
                                stack_delta = 1,
                            },
                        },
                    },
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_slot",
                reaction: ReactionWithAction(
                    "reaction.unknown_slot",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.durability",
                        kind = "equipment_durability_damage",
                        payload = new EquipmentDurabilityDamageActionPayloadDef
                        {
                            target_selector = "target_weapon",
                            target_slots = { "left_ear" },
                            durability_loss = 1,
                            max_damaged_items = 1,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.invalid_slot_weight",
                reaction: ReactionWithAction(
                    "reaction.invalid_slot_weight",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.durability",
                        kind = "equipment_durability_damage",
                        payload = new EquipmentDurabilityDamageActionPayloadDef
                        {
                            target_selector = "target_weapon",
                            slot_weights =
                            {
                                new EquipmentSlotWeightDef
                                {
                                    slot_id = "main_hand",
                                    weight = 0,
                                },
                            },
                            durability_loss = 1,
                            max_damaged_items = 1,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.unknown_slot_weight",
                reaction: ReactionWithAction(
                    "reaction.unknown_slot_weight",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.durability",
                        kind = "equipment_durability_damage",
                        payload = new EquipmentDurabilityDamageActionPayloadDef
                        {
                            target_selector = "target_weapon",
                            slot_weights =
                            {
                                new EquipmentSlotWeightDef
                                {
                                    slot_id = "left_ear",
                                    weight = 1,
                                },
                            },
                            durability_loss = 1,
                            max_damaged_items = 1,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.duplicate_slot_weight",
                reaction: ReactionWithAction(
                    "reaction.duplicate_slot_weight",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.durability",
                        kind = "equipment_durability_damage",
                        payload = new EquipmentDurabilityDamageActionPayloadDef
                        {
                            target_selector = "target_weapon",
                            slot_weights =
                            {
                                new EquipmentSlotWeightDef
                                {
                                    slot_id = "main_hand",
                                    weight = 1,
                                },
                                new EquipmentSlotWeightDef
                                {
                                    slot_id = "main_hand",
                                    weight = 2,
                                },
                            },
                            durability_loss = 1,
                            max_damaged_items = 1,
                        },
                    }
                )
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.undeclared_state",
                reaction: ReactionWithAction(
                    "reaction.undeclared_state",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.state",
                        kind = "modify_ability_state",
                        payload = new ModifyAbilityStateActionPayloadDef
                        {
                            target_selector = "self",
                            state_key = "missing_state",
                            operation = "add_int",
                            int_delta = 1,
                        },
                    }
                )
            )
        );

        EquipmentAbilityBindingDef badSyncState =
            BuildBinding(
                "bad.undeclared_sync_state",
                reaction: ReactionWithAction(
                    "reaction.undeclared_sync_state",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.sync_state",
                        kind = "modify_ability_state",
                        payload = new ModifyAbilityStateActionPayloadDef
                        {
                            target_selector = "self",
                            state_key = "declared_state",
                            operation = "add",
                            int_delta = 1,
                        },
                    }
                )
            );
        badSyncState.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "declared_state",
                owner_scope = "battle_state",
                value_kind = "int",
                reset_timing = "per_battle",
            }
        );
        badSyncState.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "tier",
                owner_scope = "battle_state",
                value_kind = "int",
                reset_timing = "per_battle",
                sync_source_state_key = "missing_sync_state",
                sync_aggregation = "floor_div",
                sync_int_literal = 10,
            }
        );
        pack.bindings.Add(badSyncState);

        EquipmentAbilityBindingDef badSyncDivisor =
            BuildBinding(
                "bad.invalid_sync_divisor",
                reaction: ReactionWithAction(
                    "reaction.invalid_sync_divisor",
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.sync_divisor",
                        kind = "modify_ability_state",
                        payload = new ModifyAbilityStateActionPayloadDef
                        {
                            target_selector = "self",
                            state_key = "counter",
                            operation = "add",
                            int_delta = 1,
                        },
                    }
                )
            );
        badSyncDivisor.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "counter",
                owner_scope = "battle_state",
                value_kind = "int",
                reset_timing = "per_battle",
            }
        );
        badSyncDivisor.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "tier",
                owner_scope = "battle_state",
                value_kind = "int",
                reset_timing = "per_battle",
                sync_source_state_key = "counter",
                sync_aggregation = "floor_div",
                sync_int_literal = 0,
            }
        );
        pack.bindings.Add(badSyncDivisor);

        EquipmentAbilityBindingDef badPersistent =
            BuildBinding(
                "bad.invalid_persistent_state",
                reaction: ReactionWithAction(
                    "reaction.valid_trace",
                    BuildValidAddDamageAction("action.valid")
                )
            );
        badPersistent.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "daily_use",
                owner_scope = "battle_state",
                value_kind = "int",
                reset_timing = "per_world_day",
                persist_outside_battle = true,
            }
        );
        pack.bindings.Add(badPersistent);

        EquipmentAbilityBindingDef badGrant =
            BuildBinding(
                "bad.invalid_grant",
                reaction: ReactionWithAction(
                    "reaction.valid_grant_probe",
                    BuildValidAddDamageAction("action.valid_grant_probe")
                )
            );
        badGrant.granted_actions.Add(
            new EquipmentGrantedActionDef
            {
                granted_action_id = "",
                granted_kind = "skill",
                skill_id = "missing_skill",
                skill_level = 1,
            }
        );
        pack.bindings.Add(badGrant);

        pack.bindings.Add(
            BuildBinding(
                "bad.confirmation",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.confirmation",
                    trigger = "on_hit",
                    timing = "after_hit",
                    requires_player_confirmation = true,
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.battle_end",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.battle_end",
                    trigger = "on_battle_end",
                    timing = "after_battle",
                    actions =
                    {
                        new EquipmentAbilityActionDef
                        {
                            action_id = "action.battle_end_status",
                            kind = "apply_status",
                            payload = new ApplyStatusActionPayloadDef
                            {
                                target_selector = "self",
                                status_id = "burning",
                                duration_turns = 1,
                                stack_delta = 1,
                            },
                        },
                    },
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.hit_after_battle",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.hit_after_battle",
                    trigger = "on_hit",
                    timing = "after_battle",
                }
            )
        );

        pack.bindings.Add(
            BuildBinding(
                "bad.battle_end_after_hit",
                reaction: new EquipmentAbilityReactionDef
                {
                    reaction_id = "reaction.battle_end_after_hit",
                    trigger = "on_battle_end",
                    timing = "after_hit",
                }
            )
        );

        EquipmentAbilityReactionDef outcomeUnknownAction = new()
        {
            reaction_id = "reaction.outcome_unknown_action",
            trigger = "on_hit",
            timing = "after_hit",
            outcome_table = new EquipmentOutcomeTableDef
            {
                table_id = "outcome.unknown_action",
                entries =
                {
                    new EquipmentOutcomeEntryDef
                    {
                        min_roll = 1,
                        max_roll = 1,
                        actions =
                        {
                            new EquipmentAbilityActionDef
                            {
                                action_id = "action.outcome_unknown",
                                kind = "unknown_action",
                            },
                        },
                    },
                },
            },
        };
        pack.bindings.Add(BuildBinding("bad.outcome_unknown_action", outcomeUnknownAction));

        EquipmentAbilityBindingDef badGrantAvailability =
            BuildBinding(
                "bad.grant_availability_condition",
                reaction: ReactionWithAction(
                    "reaction.valid_grant_availability",
                    BuildValidAddDamageAction("action.valid_grant_availability")
                )
            );
        badGrantAvailability.granted_actions.Add(
            new EquipmentGrantedActionDef
            {
                granted_action_id = "grant.bad_availability",
                granted_kind = "skill",
                skill_id = "known_skill",
                skill_level = 1,
                availability_conditions = new EquipmentAbilityConditionGroupDef
                {
                    conditions =
                    {
                        new EquipmentAbilityConditionDef
                        {
                            condition_id = "condition.bad_grant_availability",
                            kind = "unknown_condition",
                        },
                    },
                },
            }
        );
        pack.bindings.Add(badGrantAvailability);

        EquipmentAbilityBindingDef badOverlay =
            BuildBinding(
                "bad.overlay_unknown_condition",
                reaction: ReactionWithAction(
                    "reaction.valid_overlay_probe",
                    BuildValidAddDamageAction("action.valid_overlay_probe")
                )
            );
        badOverlay.weapon_profile_overlays.Add(
            new EquipmentWeaponProfileOverlayDef
            {
                overlay_id = "overlay.bad_condition",
                condition_group = new EquipmentAbilityConditionGroupDef
                {
                    conditions =
                    {
                        new EquipmentAbilityConditionDef
                        {
                            condition_id = "condition.bad_overlay",
                            kind = "unknown_condition",
                        },
                    },
                },
            }
        );
        pack.bindings.Add(badOverlay);

        EquipmentAbilityBindingDef badWorldTrigger =
            BuildBinding(
                "bad.world_unknown_trigger",
                reaction: ReactionWithAction(
                    "reaction.valid_world_trigger_probe",
                    BuildValidAddDamageAction("action.valid_world_trigger_probe")
                )
            );
        badWorldTrigger.world_effects.Add(
            new EquipmentWorldEffectDef
            {
                world_effect_id = "world.bad_trigger",
                trigger = "on_world_weather",
                timing = "after_hit",
            }
        );
        pack.bindings.Add(badWorldTrigger);

        EquipmentAbilityBindingDef badWorldAction =
            BuildBinding(
                "bad.world_unknown_action",
                reaction: ReactionWithAction(
                    "reaction.valid_world_action_probe",
                    BuildValidAddDamageAction("action.valid_world_action_probe")
                )
            );
        badWorldAction.world_effects.Add(
            new EquipmentWorldEffectDef
            {
                world_effect_id = "world.bad_action",
                trigger = "on_hit",
                timing = "after_hit",
                actions =
                {
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.world_unknown",
                        kind = "unknown_action",
                    },
                },
            }
        );
        pack.bindings.Add(badWorldAction);

        EquipmentAbilityBindingDef badGrantKind =
            BuildBinding(
                "bad.invalid_grant_kind",
                reaction: ReactionWithAction(
                    "reaction.valid_grant_kind_probe",
                    BuildValidAddDamageAction("action.valid_grant_kind_probe")
                )
            );
        badGrantKind.granted_actions.Add(
            new EquipmentGrantedActionDef
            {
                granted_action_id = "grant.invalid_kind",
                granted_kind = "spell_like_power",
                skill_id = "known_skill",
                skill_level = 1,
            }
        );
        pack.bindings.Add(badGrantKind);

        pack.bindings.Add(
            BuildSummonBinding(
                "bad.summon_unknown_skill",
                "action.summon_unknown_skill",
                "missing_summon_skill",
                "physical_slash"
            )
        );
        pack.bindings.Add(
            BuildSummonBinding(
                "bad.summon_unknown_damage",
                "action.summon_unknown_damage",
                "known_skill",
                "void_damage"
            )
        );

        return pack;
    }

    private static EquipmentAbilityBindingDef BuildBinding(
        StringName bindingId,
        EquipmentAbilityReactionDef reaction,
        StringName traitId = default
    )
    {
        if (traitId == default)
            traitId = "trait.weapon.flame";
        EquipmentAbilityBindingDef binding = new()
        {
            binding_id = bindingId,
            trait_id = traitId,
            override_mode = "add",
        };
        binding.allowed_source_kinds.Add("equipment_fixed");
        binding.required_trait_categories.Add("weapon_feat");
        binding.required_item_tags.Add("blade");
        binding.supported_equipment_type_ids.Add("weapon");
        binding.reactions.Add(reaction);
        return binding;
    }

    private static EquipmentAbilityReactionDef ReactionWithAction(
        StringName reactionId,
        EquipmentAbilityActionDef action
    )
    {
        EquipmentAbilityReactionDef reaction = new()
        {
            reaction_id = reactionId,
            trigger = "on_hit",
            timing = "after_hit",
        };
        reaction.actions.Add(action);
        return reaction;
    }

    private static EquipmentAbilityActionDef BuildValidAddDamageAction(StringName actionId) =>
        new()
        {
            action_id = actionId,
            kind = "add_damage_dice",
            payload = new AddDamageDiceActionPayloadDef
            {
                target_selector = "attack_target",
                damage_type = "physical_slash",
                dice = new DiceExpressionDef
                {
                    terms =
                    {
                        new DiceExpressionTermDef
                        {
                            dice_count = 1,
                            dice_sides = 4,
                        },
                    },
                },
            },
        };

    private static EquipmentAbilityBindingDef BuildSummonBinding(
        StringName bindingId,
        StringName actionId,
        StringName knownSkillId,
        StringName damageTag
    )
    {
        EquipmentAbilityBindingDef binding = BuildBinding(
            bindingId,
            ReactionWithAction(
                $"reaction.{bindingId}",
                BuildSummonAction(actionId, knownSkillId, damageTag)
            )
        );
        binding.state_schemas.Add(
            new EquipmentAbilityStateSchemaDef
            {
                state_key = "test_summon_state",
                owner_scope = "source_equipment",
                value_kind = "int",
                initial_int_value = 0,
                max_int_value = 1,
            }
        );
        return binding;
    }

    private static EquipmentAbilityActionDef BuildSummonAction(
        StringName actionId,
        StringName knownSkillId,
        StringName damageTag
    )
    {
        SummonUnitsActionPayloadDef payload = new()
        {
            anchor_selector = "self",
            state_key = "test_summon_state",
            count_dice = BuildDice(1, 1),
            max_living_units = 1,
            duration_tu = 60,
            spawn_radius = 1,
            unit_id_prefix = "test_summon",
            unit_display_name = "Test Summon",
            body_size_category = "small",
            control_mode = "ally_ai",
            hp_max = 1,
            armor_class = 10,
            natural_weapon_profile_type_id = "test_claws",
            natural_weapon_damage_tag = damageTag,
            natural_weapon_attack_range = 1,
            natural_weapon_damage_dice = BuildDice(1, 4),
        };
        payload.known_active_skill_ids.Add(knownSkillId);
        return new EquipmentAbilityActionDef
        {
            action_id = actionId,
            kind = "summon_units",
            payload = payload,
        };
    }

    private static DiceExpressionDef BuildDice(int diceCount, int diceSides) =>
        new()
        {
            terms =
            {
                new DiceExpressionTermDef
                {
                    dice_count = diceCount,
                    dice_sides = diceSides,
                },
            },
        };

    private static EquipmentAbilityContentValidationContext BuildValidationContext()
    {
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName> { "trait.weapon.flame" },
            KnownSkillIds = new HashSet<StringName> { "known_skill" },
            KnownItemIds = new HashSet<StringName> { "test_blade" },
            KnownStatusIds = new HashSet<StringName> { "burning" },
            KnownDamageTypes = new HashSet<StringName> { "physical_slash" },
            KnownEquipmentSlotIds = new HashSet<StringName> { "main_hand", "off_hand", "body" },
            KnownCreatureTypeTags = new HashSet<StringName> { "undead" },
            KnownBattleEnvironmentTags = new HashSet<StringName> { "night" },
        };
    }

    private static ItemDefinition BuildSourceItem(
        StringName itemId,
        StringName tag,
        StringName equipmentTypeId
    )
    {
        ItemDef item = new()
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            item_category = "equipment",
            equipment_type_id = equipmentTypeId,
        };
        item.tags.Add(tag);
        item.equipment_slot_ids.Add("main_hand");
        return item.ToDefinition();
    }

    private void AssertRuntimeTypeHasNoResourceOrGodotDictionaryMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        foreach (FieldInfo field in type.GetFields(flags))
            AssertRuntimeMemberType(type, field.Name, field.FieldType);
        foreach (PropertyInfo property in type.GetProperties(flags))
            AssertRuntimeMemberType(type, property.Name, property.PropertyType);
    }

    private void AssertRuntimeMemberType(Type owner, string memberName, Type memberType)
    {
        foreach (Type inspected in EnumerateRuntimeMemberTypeGraph(memberType))
        {
            _test.True(
                !typeof(Resource).IsAssignableFrom(inspected),
                $"{owner.Name}.{memberName} should not retain Resource type {inspected.FullName}."
            );
            _test.True(
                !IsGodotDictionaryType(inspected),
                $"{owner.Name}.{memberName} should not retain Godot.Collections.Dictionary type {inspected.FullName}."
            );
        }
    }

    private static IEnumerable<Type> EnumerateRuntimeMemberTypeGraph(Type root)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type type = pending.Pop();
            if (type == null || !seen.Add(type))
                continue;
            yield return type;

            if (type.HasElementType)
                pending.Push(type.GetElementType());
            foreach (Type argument in type.GetGenericArguments())
                pending.Push(argument);
        }
    }

    private static bool IsGodotDictionaryType(Type type)
    {
        return type.FullName != null
            && type.FullName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal);
    }

    private static MemberInfo FindPublicInstanceMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        MemberInfo[] members = type.GetMember(name, flags);
        return members.Length > 0 ? members[0] : null;
    }

    private void AssertContainsKey<T>(
        IReadOnlyDictionary<StringName, T> dictionary,
        StringName key,
        string label
    )
    {
        _test.True(dictionary.ContainsKey(key), $"{label} should contain {key}.");
    }

    private void AssertErrorContains(
        IReadOnlyList<string> errors,
        string code,
        string pathFragment
    )
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(code) && (error ?? "").Contains(pathFragment))
                return;
        }
        _test.Fail(
            $"Expected error containing code={code} path={pathFragment}. errors={FormatErrors(errors)}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors ?? Array.Empty<string>())
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }
}

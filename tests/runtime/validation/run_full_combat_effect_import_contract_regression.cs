#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public partial class run_full_combat_effect_import_contract_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestAllRegisteredKindsParseToTheirTypedPayload();
            TestFullEffectAndRecursiveNestedResourcesNormalize();
            TestRepeatPayloadPreservesTypedAbiAndFrozenMap();
            TestRawContractFailuresAreExactAndFailClosed();
            TestResourceDtoModelInventoriesAreExact();
            TestPayloadDtoContractsAreExact();
            TestImportGraphStaysPlainAndReadOnly();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected full combat effect import regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Full combat effect import contract regression"));
    }

    private void TestAllRegisteredKindsParseToTheirTypedPayload()
    {
        _test.Eq(SkillFullCombatEffectClosedSpec.SchemaBranches.Count, 28, "closed spec should register all 28 current effect kinds");
        foreach (ContentJsonSchemaClosedKindBranch branch in SkillFullCombatEffectClosedSpec.SchemaBranches)
        {
            ContentImportStageResult<SkillImportModel> result = Parse(
                $"kind_{branch.Kind}",
                Effect(branch.Kind, PayloadFor(branch.Kind))
            );
            _test.True(result.HasValue, $"registered kind {branch.Kind} should parse | {Format(result)}");
            if (!result.HasValue)
                continue;
            CombatEffectImportModel effect = result.Value.CombatProfile!.EffectDefs[0];
            _test.True(
                CombatEffectImportClosedSpec.IsPayloadCompatible(effect.Kind, effect.Payload),
                $"registered kind {branch.Kind} should publish only its matching payload model"
            );
        }

        ContentImportStageResult<SkillImportModel> repeatDefaults = Parse(
            "repeat_defaults",
            Effect("repeat_attack_until_fail", "{}")
        );
        _test.True(repeatDefaults.HasValue, "repeat payload should preserve legal CLR defaults");
        if (repeatDefaults.HasValue && repeatDefaults.Value.CombatProfile!.EffectDefs[0].Payload is RepeatAttackUntilFailEffectPayloadImportModel repeat)
        {
            _test.Eq(repeat.CostResource, CombatResourceImportKind.Aura, "parsed repeat resource default should be aura");
            _test.Eq(repeat.FollowUpCostMultiplier, 1.0, "parsed repeat multiplier default should be one");
            _test.False(repeat.SameTargetOnly, "parsed repeat same-target default should be false");
            _test.False(repeat.StopOnInsufficientResource, "parsed repeat stop default should be false");
        }
        ContentImportStageResult<SkillImportModel> onKillDefaults = Parse(
            "onkill_defaults",
            Effect("on_kill_gain_resources", "{}")
        );
        _test.True(onKillDefaults.HasValue, "on-kill payload should preserve legal CLR defaults");
        if (onKillDefaults.HasValue && onKillDefaults.Value.CombatProfile!.EffectDefs[0].Payload is OnKillGainResourcesEffectPayloadImportModel onKill)
            _test.Eq(onKill.GrantScope, CombatOnKillGrantScopeImportKind.None, "omitted grant scope should remain typed None");

        ContentImportStageResult<SkillImportModel> equipmentDefaults = Parse(
            "equipment_defaults",
            Effect("equipment_durability_damage", "{\"target_slots\":[\"head\"]}")
        );
        if (equipmentDefaults.HasValue && equipmentDefaults.Value.CombatProfile!.EffectDefs[0].Payload is EquipmentDurabilityDamageEffectPayloadImportModel equipment)
            _test.Eq(equipment.MaxDamagedItems, 1, "parsed equipment maximum default should be one");

        ContentImportStageResult<SkillImportModel> statusDefaults = Parse("status_defaults", Effect("status", "{}"));
        if (statusDefaults.HasValue && statusDefaults.Value.CombatProfile!.EffectDefs[0].Payload is StatusEffectPayloadImportModel status)
            _test.Eq(status.SourceSkillId.Value, "", "parsed status source ID default should stay empty");
        ContentImportStageResult<SkillImportModel> healDefaults = Parse("heal_defaults", Effect("heal", "{}"));
        if (healDefaults.HasValue && healDefaults.Value.CombatProfile!.EffectDefs[0].Payload is HealEffectPayloadImportModel heal)
            _test.False(heal.ConModHeal, "parsed heal con-mod default should stay false");
        ContentImportStageResult<SkillImportModel> dispelDefaults = Parse("dispel_defaults", Effect("dispel_magic", "{}"));
        if (dispelDefaults.HasValue && dispelDefaults.Value.CombatProfile!.EffectDefs[0].Payload is DispelMagicEffectPayloadImportModel dispel)
            _test.Eq(dispel.BreaksBarrierLayer.Value, "", "parsed dispel layer default should stay empty");
    }

    private void TestFullEffectAndRecursiveNestedResourcesNormalize()
    {
        string effect = "{" +
            "\"effect_type\":\"damage\",\"payload\":{}," +
            "\"tick_effect_type\":\"damage\",\"lifetime_policy\":\"battle\"," +
            "\"damage_tag\":\"fire\",\"damage_tags\":[\"force\"]," +
            "\"mitigation_bypass_tiers\":[\"half\"],\"damage_category\":\"spell\"," +
            "\"path_step_area_pattern\":\"cross\",\"effect_target_team_filter\":\"inherit\"," +
            "\"save_ability\":\"willpower\",\"save_tag\":\"magic\"," +
            "\"save_advantage_tags\":[\"illusion\"]," +
            "\"equipment_durability_slot_weights\":[{\"slot_id\":\"head\",\"weight\":2}]," +
            "\"extra_damage_segments\":[{\"damage_tag\":\"lightning\",\"power\":3}]," +
            "\"target_damage_multiplier_rules\":[{\"any_creature_type_tags\":[\"undead\"],\"multiplier_percent\":150}]," +
            "\"save_failure_status_outcomes\":[{\"outcome_id\":\"shock\",\"weight\":1," +
            "\"status_effect\":{" +
            "\"effect_type\":\"status\",\"payload\":{},\"status_id\":\"shocked\"}}]}";
        ContentImportStageResult<SkillImportModel> result = Parse("full", effect);
        _test.True(result.HasValue, $"full effect graph should parse | {Format(result)}");
        if (!result.HasValue)
            return;

        CombatEffectImportModel value = result.Value.CombatProfile!.EffectDefs[0];
        _test.Eq(value.TickEffectType, CombatTickEffectImportKind.Damage, "tick type should be typed");
        _test.Eq(value.LifetimePolicy, CombatEffectLifetimeImportKind.Battle, "lifetime should be typed");
        _test.Eq(value.DamageTag, DamageTagImportKind.Fire, "damage tag should be typed");
        _test.Eq(value.MitigationBypassTiers[0], DamageMitigationTierImportKind.Half, "mitigation tier should be typed");
        _test.Eq(value.PathStepAreaPattern, CombatPathStepAreaPatternImportKind.Cross, "path-step area should use its six-value domain");
        _test.Eq(value.EffectTargetTeamFilter, CombatEffectTargetTeamFilterImportKind.Inherit, "effect target filter should retain typed inherit");
        _test.Eq(value.SaveAbility, CombatSaveAbilityImportKind.Willpower, "save ability should be typed");
        _test.Eq(value.SaveTag, CombatSaveTagImportKind.Magic, "save tag should be typed");
        _test.Eq(value.EquipmentDurabilitySlotWeights[0].SlotId, CombatEquipmentSlotImportKind.Head, "nested slot should be typed");
        _test.Eq(value.ExtraDamageSegments[0].DamageTag, DamageTagImportKind.Lightning, "nested damage tag should be typed");
        _test.Eq(value.SaveFailureStatusOutcomes[0].StatusEffect.Kind, CombatEffectImportKind.Status, "weighted outcome should recursively normalize its effect");
        _test.Eq(value.SaveFailureStatusOutcomes[0].StatusEffect.StatusId.Value, "shocked", "recursive status effect fields should be retained");
    }

    private void TestRepeatPayloadPreservesTypedAbiAndFrozenMap()
    {
        string payload = "{" +
            "\"base_attack_bonus\":2,\"cost_resource\":\"mp\"," +
            "\"follow_up_cost_addition\":3,\"follow_up_cost_multiplier\":1.5," +
            "\"follow_up_attack_penalty\":-2," +
            "\"penalty_free_stages_by_level\":{\"0\":1,\"3\":2}," +
            "\"same_target_only\":true,\"follow_up_fixed_cost\":4," +
            "\"exponential_penalty\":true,\"stop_on_insufficient_resource\":false}";
        ContentImportStageResult<SkillImportModel> result = Parse("repeat", Effect("repeat_attack_until_fail", payload));
        _test.True(result.HasValue, $"repeat payload should parse | {Format(result)}");
        if (!result.HasValue || result.Value.CombatProfile!.EffectDefs[0].Payload is not RepeatAttackUntilFailEffectPayloadImportModel value)
            return;
        _test.Eq(value.CostResource, CombatResourceImportKind.Mp, "repeat resource should be typed");
        _test.Eq(value.FollowUpCostAddition, 3, "repeat additive cost should be retained");
        _test.Eq(value.FollowUpCostMultiplier, 1.5, "repeat multiplier should remain double");
        _test.False(value.StopOnInsufficientResource, "repeat stop flag should retain explicit false");
        _test.Eq(value.PenaltyFreeStagesByLevel[0], 1, "repeat level zero should be representable");
        _test.True(
            Throws<NotSupportedException>(() => ((IDictionary<int, int>)value.PenaltyFreeStagesByLevel).Add(9, 9)),
            "repeat level map should be read-only"
        );
    }

    private void TestRawContractFailuresAreExactAndFailClosed()
    {
        AssertFailure("unknown_kind", Effect("future_effect", "{}"), SkillJsonImportRules.UnknownEffectKind, "/entries/5/combat_profile/effect_defs/0/effect_type");
        AssertFailure("empty_extra", Effect("damage", "{\"raw\":1}"), SkillJsonImportRules.InvalidEffectPayload, "/entries/5/combat_profile/effect_defs/0/payload/raw");
        AssertFailure("equipment_required", Effect("equipment_durability_damage", "{}"), SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/payload/target_slots");
        foreach (string legacyAlias in new[] { "apply_status", "terrain", "height" })
        {
            AssertFailure($"legacy_{legacyAlias}", Effect(legacyAlias, "{}"), SkillJsonImportRules.UnknownEffectKind, "/entries/5/combat_profile/effect_defs/0/effect_type");
        }
        AssertFailure("nested_null", "{\"effect_type\":\"damage\",\"payload\":{},\"extra_damage_segments\":[{\"damage_tags\":null}]}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/extra_damage_segments/0/damage_tags");
        AssertFailure("nested_closed_empty", "{\"effect_type\":\"damage\",\"payload\":{},\"extra_damage_segments\":[{\"damage_tag\":\"\"}]}", SkillJsonImportRules.InvalidEffectPayload, "/entries/5/combat_profile/effect_defs/0/extra_damage_segments/0/damage_tag");
        AssertFailure("array_null", "{\"effect_type\":\"damage\",\"payload\":{},\"save_advantage_tags\":[null]}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/save_advantage_tags/0");
        AssertFailure("closed_empty", "{\"effect_type\":\"damage\",\"payload\":{},\"save_tag\":\"\"}", SkillJsonImportRules.InvalidEffectPayload, "/entries/5/combat_profile/effect_defs/0/save_tag");
        AssertFailure("closed_unknown", "{\"effect_type\":\"damage\",\"payload\":{},\"save_tag\":\"future_save\"}", SkillJsonImportRules.InvalidEffectPayload, "/entries/5/combat_profile/effect_defs/0/save_tag");
        AssertFailure("nested_missing_kind", "{\"effect_type\":\"damage\",\"payload\":{},\"save_failure_status_outcomes\":[{\"outcome_id\":\"x\",\"weight\":1,\"status_effect\":{\"payload\":{}}}]}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/save_failure_status_outcomes/0/status_effect/effect_type");
        AssertFailure("nested_missing_effect", "{\"effect_type\":\"damage\",\"payload\":{},\"save_failure_status_outcomes\":[{\"outcome_id\":\"x\",\"weight\":1}]}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/save_failure_status_outcomes/0/status_effect");
        AssertFailure("nested_null_effect", "{\"effect_type\":\"damage\",\"payload\":{},\"save_failure_status_outcomes\":[{\"outcome_id\":\"x\",\"weight\":1,\"status_effect\":null}]}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/save_failure_status_outcomes/0/status_effect");
        AssertFailure("payload_list_null", "{\"effect_type\":\"equipment_durability_damage\",\"payload\":{\"target_slots\":null}}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/payload/target_slots");
        AssertFailure("payload_list_item_null", "{\"effect_type\":\"equipment_durability_damage\",\"payload\":{\"target_slots\":[null]}}", SkillJsonImportRules.RequiredMember, "/entries/5/combat_profile/effect_defs/0/payload/target_slots/0");
        AssertFailure("duplicate_level", "{\"effect_type\":\"repeat_attack_until_fail\",\"payload\":{\"same_target_only\":true,\"stop_on_insufficient_resource\":true,\"penalty_free_stages_by_level\":{\"0\":1,\"0\":2}}}", SkillJsonImportRules.DuplicateLevelKey, "/entries/5/combat_profile/effect_defs/0/payload/penalty_free_stages_by_level/0");
        foreach (string noncanonicalLevel in new[] { "01", "+1", "-0" })
        {
            AssertFailure($"level_{noncanonicalLevel}", "{\"effect_type\":\"repeat_attack_until_fail\",\"payload\":{\"penalty_free_stages_by_level\":{\"" + noncanonicalLevel + "\":1}}}", SkillJsonImportRules.InvalidId, "/entries/5/combat_profile/effect_defs/0/payload/penalty_free_stages_by_level/" + noncanonicalLevel);
        }
        AssertFailure("wrong_multiplier", "{\"effect_type\":\"repeat_attack_until_fail\",\"payload\":{\"same_target_only\":true,\"stop_on_insufficient_resource\":true,\"follow_up_cost_multiplier\":\"two\"}}", SkillJsonImportRules.InvalidEffectPayload, "/entries/5/combat_profile/effect_defs/0/payload/follow_up_cost_multiplier");
    }

    private void TestImportGraphStaysPlainAndReadOnly()
    {
        _test.Eq(typeof(CombatEffectJsonDto).GetProperties().Length, 194, "effect DTO should represent every CombatEffectDef export exactly once");
        _test.Eq(typeof(CombatEffectImportModel).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length, 194, "plain effect model should represent every effect field and typed payload exactly once");
        _test.Eq(typeof(CombatEffectSlotWeightJsonDto).GetProperties().Length, 2, "slot-weight DTO inventory should stay exact");
        _test.Eq(typeof(CombatDamageSegmentJsonDto).GetProperties().Length, 10, "damage-segment DTO inventory should stay exact");
        _test.Eq(typeof(CombatTargetDamageMultiplierRuleJsonDto).GetProperties().Length, 4, "target-multiplier DTO inventory should stay exact");
        _test.Eq(typeof(CombatWeightedStatusOutcomeJsonDto).GetProperties().Length, 3, "weighted-status DTO inventory should stay exact");
        var roots = new[]
        {
            typeof(CombatEffectImportModel),
            typeof(CombatDamageSegmentImportModel),
            typeof(CombatWeightedStatusOutcomeImportModel),
            typeof(RepeatAttackUntilFailEffectPayloadImportModel),
        };
        foreach (Type type in roots)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string name = property.PropertyType.FullName ?? property.PropertyType.Name;
                _test.False(name.Contains("Godot", StringComparison.Ordinal), $"{type.Name}.{property.Name} must not expose Godot types");
                _test.False(property.PropertyType == typeof(object), $"{type.Name}.{property.Name} must not expose object");
                _test.False(property.PropertyType == typeof(JsonElement), $"{type.Name}.{property.Name} must not expose JsonElement");
            }
        }

        ContentImportStageResult<SkillImportModel> result = Parse("readonly", "{\"effect_type\":\"damage\",\"payload\":{},\"damage_tags\":[\"fire\"]}");
        if (!result.HasValue)
            return;
        IReadOnlyList<DamageTagImportKind> tags = result.Value.CombatProfile!.EffectDefs[0].DamageTags;
        _test.True(Throws<NotSupportedException>(() => ((IList<DamageTagImportKind>)tags).Add(DamageTagImportKind.Force)), "effect collections should be read-only");
    }

    private void TestResourceDtoModelInventoriesAreExact()
    {
        AssertProjectionInventory(typeof(CombatEffectDef), typeof(CombatEffectJsonDto), typeof(CombatEffectImportModel), "effect", new Dictionary<string, string>
        {
            ["effect_type"] = "Kind",
            ["params"] = "Payload",
        });
        AssertProjectionInventory(typeof(CombatEffectSlotWeightDef), typeof(CombatEffectSlotWeightJsonDto), typeof(CombatEffectSlotWeightImportModel), "slot weight");
        AssertProjectionInventory(typeof(CombatDamageSegmentDef), typeof(CombatDamageSegmentJsonDto), typeof(CombatDamageSegmentImportModel), "damage segment");
        AssertProjectionInventory(typeof(CombatTargetDamageMultiplierRuleDef), typeof(CombatTargetDamageMultiplierRuleJsonDto), typeof(CombatTargetDamageMultiplierRuleImportModel), "target multiplier");
        AssertProjectionInventory(typeof(CombatWeightedStatusOutcomeDef), typeof(CombatWeightedStatusOutcomeJsonDto), typeof(CombatWeightedStatusOutcomeImportModel), "weighted status");
    }

    private void TestPayloadDtoContractsAreExact()
    {
        AssertPayloadContract(typeof(StatusEffectPayloadJsonDto), new[]
        {
            Field("breaks_barrier_layer", typeof(string)),
            Field("source_skill_id", typeof(string)),
        });
        AssertPayloadContract(typeof(HealEffectPayloadJsonDto), new[]
        {
            Field("con_mod_heal", typeof(bool)),
        });
        AssertPayloadContract(typeof(EquipmentDurabilityDamageEffectPayloadJsonDto), new[]
        {
            Field("max_damaged_items", typeof(int)),
            Field("target_slots", typeof(IReadOnlyList<string>), required: true),
        });
        AssertPayloadContract(typeof(RepeatAttackUntilFailEffectPayloadJsonDto), new[]
        {
            Field("base_attack_bonus", typeof(int)),
            Field("cost_resource", typeof(string)),
            Field("follow_up_cost_addition", typeof(int)),
            Field("follow_up_cost_multiplier", typeof(double)),
            Field("follow_up_attack_penalty", typeof(int)),
            Field("penalty_free_stages_by_level", typeof(IReadOnlyDictionary<string, int>)),
            Field("same_target_only", typeof(bool)),
            Field("follow_up_fixed_cost", typeof(int)),
            Field("exponential_penalty", typeof(bool)),
            Field("stop_on_insufficient_resource", typeof(bool)),
        });
        AssertPayloadContract(typeof(LayeredBarrierEffectPayloadJsonDto), new[]
        {
            Field("area_pattern", typeof(string), required: true),
            Field("profile_id", typeof(string), required: true),
            Field("radius_cells", typeof(int), required: true),
            Field("save_dc", typeof(int), required: true),
        });
        AssertPayloadContract(typeof(GradedSaveExecuteEffectPayloadJsonDto), new[]
        {
            Field("critical_failure_damage_dice_count", typeof(int), true),
            Field("critical_failure_damage_dice_sides", typeof(int), true),
            Field("critical_failure_execute_threshold_max_hp_percent", typeof(int), true),
            Field("critical_failure_frightened_duration_tu", typeof(int), true),
            Field("critical_failure_stunned_duration_tu", typeof(int), true),
            Field("failure_damage_dice_count", typeof(int), true),
            Field("failure_damage_dice_sides", typeof(int), true),
            Field("failure_execute_threshold_fixed", typeof(int), true),
            Field("failure_execute_threshold_max_hp_percent", typeof(int), true),
            Field("failure_frightened_duration_tu", typeof(int), true),
            Field("failure_reaction_lock_duration_tu", typeof(int), true),
            Field("profile_id", typeof(string), true),
            Field("success_aftershock_duration_tu", typeof(int), true),
        });
        AssertPayloadContract(typeof(DispelMagicEffectPayloadJsonDto), new[]
        {
            Field("breaks_barrier_layer", typeof(string)),
        });
        AssertPayloadContract(typeof(OnKillGainResourcesEffectPayloadJsonDto), new[]
        {
            Field("grant_scope", typeof(string)),
            Field("require_target_defeated_by_same_skill", typeof(bool)),
            Field("stack_on_multiple_kills", typeof(bool)),
        });

        var equipmentDefaults = new EquipmentDurabilityDamageEffectPayloadJsonDto();
        _test.Eq(equipmentDefaults.MaxDamagedItems, 1, "equipment payload max-damaged default should match runtime");
        var repeatDefaults = new RepeatAttackUntilFailEffectPayloadJsonDto();
        _test.Eq(repeatDefaults.CostResource, "aura", "repeat resource default should match runtime");
        _test.Eq(repeatDefaults.FollowUpCostMultiplier, 1.0, "repeat multiplier default should match runtime");
        _test.False(repeatDefaults.SameTargetOnly, "repeat same-target default should remain false");
        _test.False(repeatDefaults.StopOnInsufficientResource, "repeat stop default should remain false");
        _test.False(new HealEffectPayloadJsonDto().ConModHeal, "heal payload default should remain false");
        _test.Eq(new StatusEffectPayloadJsonDto().SourceSkillId, "", "status source ID default should remain empty");
        _test.Eq(new DispelMagicEffectPayloadJsonDto().BreaksBarrierLayer, "", "dispel layer default should remain empty");
        _test.Eq(new OnKillGainResourcesEffectPayloadJsonDto().GrantScope, "", "on-kill grant scope default should remain empty");
    }

    private void AssertProjectionInventory(Type resourceType, Type dtoType, Type modelType, string label, IReadOnlyDictionary<string, string>? modelOverrides = null)
    {
        modelOverrides ??= new Dictionary<string, string>();
        string[] exports = resourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<ExportAttribute>() != null)
            .Select(property => property.Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedWire = exports.Select(value => value == "params" ? "payload" : value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualWire = dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "<missing>")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        _test.Eq(string.Join("|", actualWire), string.Join("|", expectedWire), $"{label} DTO wire inventory should exactly match Resource exports");

        string[] expectedModel = exports.Select(value => modelOverrides.TryGetValue(value, out string? mapped) ? mapped : SnakeToPascal(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualModel = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        _test.Eq(string.Join("|", actualModel), string.Join("|", expectedModel), $"{label} model inventory should exactly match Resource exports");
    }

    private void AssertPayloadContract(Type dtoType, IReadOnlyList<PayloadField> expected)
    {
        var actual = dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new PayloadField(
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "<missing>",
                property.PropertyType,
                property.GetCustomAttribute<JsonRequiredAttribute>() != null
            ))
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ToArray();
        PayloadField[] orderedExpected = expected.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        _test.Eq(
            string.Join("|", actual.Select(Describe)),
            string.Join("|", orderedExpected.Select(Describe)),
            $"{dtoType.Name} key/type/required inventory should be exact"
        );
    }

    private static PayloadField Field(string name, Type type, bool required = false) => new(name, type, required);
    private static string Describe(PayloadField field) => $"{field.Name}:{field.Type.FullName}:{field.Required}";
    private sealed record PayloadField(string Name, Type Type, bool Required);

    private static string SnakeToPascal(string value) =>
        string.Concat(value.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));

    private void AssertFailure(string id, string effect, string rule, string pointer)
    {
        ContentImportStageResult<SkillImportModel> result = Parse(id, effect);
        _test.False(result.HasValue, $"{id} should not publish a partial model");
        _test.Eq(result.Diagnostics.Count, 1, $"{id} should produce one diagnostic | {Format(result)}");
        if (result.Diagnostics.Count != 1)
            return;
        _test.Eq(result.Diagnostics[0].RuleId, rule, $"{id} rule should be stable");
        _test.Eq(result.Diagnostics[0].JsonPointer, pointer, $"{id} pointer should be exact");
    }

    private static ContentImportStageResult<SkillImportModel> Parse(string id, string effect) =>
        SkillJsonImportParser.Parse(
            new JsonContentEntryContext("skills", id, $"skill.json#{id}", "/entries/5"),
            "{\"skill_id\":\"test_skill\",\"display_name\":\"Test\",\"combat_profile\":{" +
                "\"skill_id\":\"test_skill\",\"effect_defs\":[" + effect + "]}}"
        );

    private static string Effect(string kind, string payload) =>
        $"{{\"effect_type\":\"{kind}\",\"payload\":{payload}}}";

    private static string PayloadFor(string kind) =>
        kind switch
        {
            "layered_barrier" => "{\"area_pattern\":\"diamond\",\"profile_id\":\"ward\",\"radius_cells\":1,\"save_dc\":10}",
            "equipment_durability_damage" => "{\"target_slots\":[\"head\"]}",
            "repeat_attack_until_fail" => "{\"same_target_only\":true,\"stop_on_insufficient_resource\":true}",
            "graded_save_execute" => "{\"critical_failure_damage_dice_count\":1,\"critical_failure_damage_dice_sides\":6,\"critical_failure_execute_threshold_max_hp_percent\":1,\"critical_failure_frightened_duration_tu\":1,\"critical_failure_stunned_duration_tu\":1,\"failure_damage_dice_count\":1,\"failure_damage_dice_sides\":6,\"failure_execute_threshold_fixed\":1,\"failure_execute_threshold_max_hp_percent\":1,\"failure_frightened_duration_tu\":1,\"failure_reaction_lock_duration_tu\":1,\"profile_id\":\"execute\",\"success_aftershock_duration_tu\":1}",
            "on_kill_gain_resources" => "{\"grant_scope\":\"current_turn\",\"require_target_defeated_by_same_skill\":true,\"stack_on_multiple_kills\":false}",
            _ => "{}",
        };

    private static string Format(ContentImportStageResult<SkillImportModel> result) =>
        string.Join("; ", result.Diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}"));

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}

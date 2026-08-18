#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public partial class run_skill_canonical_json_schema_regression : LifecycleTestSceneTree
{
    private delegate bool TryWireValue<T>(string? wire, out T value) where T : struct;
    private sealed record ObjectOracleCase(
        string Label,
        Type DtoType,
        Func<string, string> Wrap,
        string[] Path,
        IReadOnlyDictionary<string, string> BaselineOverrides,
        IReadOnlySet<string> PersistentBaselineKeys,
        IReadOnlyDictionary<string, FieldOracleOverride> FieldOverrides
    );
    private sealed record FieldOracleOverride(
        string ValueJson,
        string? BaselineObjectJson = null,
        string? ExpectedValueJson = null
    );
    private sealed record NonZeroDefaultResetCase(string WireName, string ResetJson);
    // Reviewed current canonical forms for minimal effects. Keeping these literals independent
    // prevents recursive effect getters from being self-certified by the writer under test.
    private const string CanonicalDamageEffectLiteral =
        "{\"effect_type\":\"damage\",\"payload\":{}}";
    private const string CanonicalHealEffectLiteral =
        "{\"effect_type\":\"heal\",\"payload\":{}}";
    private static readonly IReadOnlySet<string> ReviewedNestedNonZeroDefaultFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Key<ContingencyAutomationJsonDto>("min_contingency_skill_level"),
            Key<CombatWindupJsonDto>("stamina_cost_per_tier"),
            Key<CombatWindupJsonDto>("weapon_dice_per_tier"),
            Key<CombatDirectionalPiercingJsonDto>("successful_hit_decay_percent"),
            Key<CombatDirectionalPiercingJsonDto>("minimum_damage_percent"),
            Key<CombatDirectionalPiercingJsonDto>("stamina_flat_base"),
            Key<CombatDirectionalPiercingJsonDto>("stamina_range_square_coefficient"),
            Key<CombatDirectionalPiercingJsonDto>("stamina_strength_square_scale"),
            Key<CombatDirectionalPiercingJsonDto>("minimum_stamina_cost"),
            Key<CombatDirectionalPiercingJsonDto>("maximum_height_delta"),
            Key<CombatLineThroughAttackJsonDto>("maximum_weapon_range"),
            Key<CombatLineThroughAttackJsonDto>("intermediate_weapon_dice_multiplier"),
            Key<CombatLineThroughAttackJsonDto>("successful_intermediate_hit_bonus_weapon_dice"),
            Key<CombatLineThroughAttackJsonDto>("successful_intermediate_hit_attack_roll_bonus"),
            Key<CombatSpellReactionJsonDto>("base_save_dc"),
            Key<CombatSpellReactionJsonDto>("hp_damage_divisor"),
            Key<CombatSpellReactionJsonDto>("require_hp_damage"),
            Key<CombatSpellReactionJsonDto>("consume_on_trigger"),
            Key<CombatSpellReactionJsonDto>("expire_on_owner_turn_start"),
            Key<CombatRangedWeaponReactionJsonDto>("consume_status_stacks"),
            Key<CombatRangedWeaponReactionJsonDto>("trigger_on_hit"),
            Key<CombatRangedWeaponReactionJsonDto>("trigger_on_miss"),
            Key<CombatCastVariantJsonDto>("required_coord_count"),
            Key<CombatDamageSegmentJsonDto>("pre_resistance_damage_multiplier"),
            Key<CombatTargetDamageMultiplierRuleJsonDto>("multiplier_percent"),
            Key<CombatWeightedStatusOutcomeJsonDto>("weight"),
            Key<EquipmentDurabilityDamageEffectPayloadJsonDto>("max_damaged_items"),
            Key<RepeatAttackUntilFailEffectPayloadJsonDto>("follow_up_cost_multiplier"),
        };
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestTopLevelWireInventoriesAreExact();
            TestTopLevelNonZeroDefaultsPreserveOmissionAndExplicitReset();
            TestNestedWireInventoriesAreExact();
            TestNestedAndPayloadGetterDefaultOracles();
            TestEffectKindsMapToExpectedPayloadShapes();
            TestReverseWireDomainsAreClosedAndBijective();
            TestCanonicalNestedGraphAndPayloadsRoundTrip();
            TestOrderedMapsAndFloatingPointTokens();
            TestWindupCanonicalDefaults();
            TestWriterRejectsMismatchedEffectKindAndPayload();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected skill canonical JSON schema exception: {exception}");
        }
        RequestTestExit(_test.Finish("Skill canonical JSON schema regression"));
    }

    private void TestTopLevelNonZeroDefaultsPreserveOmissionAndExplicitReset()
    {
        NonZeroDefaultResetCase[] rootCases =
        {
            new("max_level", "0"),
            new("retain_source_skills_on_unlock", "false"),
        };
        NonZeroDefaultResetCase[] combatCases =
        {
            new("range_value", "0"),
            new("ap_cost", "0"),
            new("mastery_base_amount", "0"),
            new("fumble_protection_extra_mp_percent", "0"),
            new("min_target_count", "0"),
            new("max_target_count", "0"),
            new("mastery_low_hp_bonus_multiplier", "0"),
            new("mastery_low_hp_threshold_percent", "0"),
        };
        NonZeroDefaultResetCase[] effectCases =
        {
            new("max_skill_level", "0"),
            new("damage_ratio_percent", "0"),
            new("pre_resistance_damage_multiplier", "0.0"),
            new("weapon_dice_multiplier", "0"),
            new("path_step_radius", "0"),
            new("repeat_hit_status_power", "0"),
            new("prevent_repeat_target", "false"),
            new("stop_on_miss", "false"),
            new("stop_on_target_down", "false"),
            new("follow_up_damage_multiplier_percent", "0"),
            new("remove_harmful_from_allies", "false"),
            new("remove_beneficial_from_enemies", "false"),
            new("min_hp_after_damage", "0"),
            new("threshold_level_anchor", "0"),
            new("threshold_level_bonus_per_delta", "0"),
            new("threshold_max_hp_ratio_percent", "0"),
            new("threshold_cap_max_hp_ratio_percent", "0"),
            new("heal_multiplier_percent", "0"),
            new("shield_gain_multiplier_percent", "0"),
            new("attack_roll_penalty", "0"),
            new("secondary_hit_dc_base", "0"),
            new("debuff_count_threshold", "0"),
            new("base_heal", "0"),
            new("heal_per_level", "0"),
            new("con_mod_base", "0"),
            new("con_mod_per_2_levels", "0"),
            new("charge_trap_immunity_min_skill_level", "0"),
            new("jump_range_multiplier", "0"),
            new("upkeep_cost_multiplier", "0"),
        };

        JsonElement omittedRoot = ParseWriteAndNavigate(
            "top_level_root_defaults",
            "{\"skill_id\":\"top_level_root_defaults\",\"display_name\":\"Defaults\"}",
            Array.Empty<string>()
        );
        JsonElement omittedCombat = ParseWriteAndNavigate(
            "top_level_combat_defaults",
            "{\"skill_id\":\"top_level_combat_defaults\",\"display_name\":\"Defaults\",\"combat_profile\":{\"skill_id\":\"top_level_combat_defaults\"}}",
            new[] { "combat_profile" }
        );
        JsonElement omittedEffect = ParseWriteAndNavigate(
            "top_level_effect_defaults",
            "{\"skill_id\":\"top_level_effect_defaults\",\"display_name\":\"Defaults\",\"combat_profile\":{\"skill_id\":\"top_level_effect_defaults\",\"effect_defs\":[{\"effect_type\":\"damage\",\"payload\":{}}]}}",
            new[] { "combat_profile", "effect_defs", "0" }
        );
        AssertOmittedDefaults(omittedRoot, rootCases, "skill root");
        AssertOmittedDefaults(omittedCombat, combatCases, "combat profile");
        AssertOmittedDefaults(omittedEffect, effectCases, "combat effect");
        _test.Eq(
            string.Join("\n", GetObjectPropertyNames(omittedEffect)),
            "effect_type\npayload",
            "minimal effect canonical JSON should contain only its required discriminator and payload"
        );

        string rootResets = WriteObjectMembers(rootCases);
        string combatResets = WriteObjectMembers(combatCases);
        string effectResets = WriteObjectMembers(effectCases);
        string resetJson =
            "{\"skill_id\":\"top_level_resets\",\"display_name\":\"Resets\"," +
            rootResets + ",\"combat_profile\":{\"skill_id\":\"top_level_resets\"," +
            combatResets + ",\"effect_defs\":[{\"effect_type\":\"damage\",\"payload\":{}," +
            effectResets + "}]}}";
        JsonElement resetRoot = ParseWriteAndNavigate(
            "top_level_resets",
            resetJson,
            Array.Empty<string>()
        );
        JsonElement resetCombat = resetRoot.GetProperty("combat_profile");
        JsonElement resetEffect = resetCombat.GetProperty("effect_defs")[0];

        int testedCount = 0;
        testedCount += AssertExplicitResets(resetRoot, rootCases, "skill root");
        testedCount += AssertExplicitResets(resetCombat, combatCases, "combat profile");
        testedCount += AssertExplicitResets(resetEffect, effectCases, "combat effect");
        _test.Eq(testedCount, 39, "reviewed top-level nonzero/true default inventory should stay at 39");
    }

    private void AssertOmittedDefaults(
        JsonElement value,
        IReadOnlyList<NonZeroDefaultResetCase> cases,
        string label
    )
    {
        foreach (NonZeroDefaultResetCase item in cases)
        {
            _test.True(
                !value.TryGetProperty(item.WireName, out _),
                $"{label}.{item.WireName} omission should normalize to its canonical default without expanding JSON"
            );
        }
    }

    private int AssertExplicitResets(
        JsonElement value,
        IReadOnlyList<NonZeroDefaultResetCase> cases,
        string label
    )
    {
        int count = 0;
        foreach (NonZeroDefaultResetCase item in cases)
        {
            _test.True(
                value.TryGetProperty(item.WireName, out JsonElement actual),
                $"{label}.{item.WireName} explicit reset should be written"
            );
            if (value.TryGetProperty(item.WireName, out actual))
            {
                _test.Eq(
                    actual.GetRawText(),
                    item.ResetJson,
                    $"{label}.{item.WireName} explicit reset should remain exact"
                );
            }
            count += 1;
        }
        return count;
    }

    private static string WriteObjectMembers(
        IReadOnlyList<NonZeroDefaultResetCase> cases
    ) => string.Join(
        ",",
        cases.Select(static item => $"{JsonSerializer.Serialize(item.WireName)}:{item.ResetJson}")
    );

    private void TestTopLevelWireInventoriesAreExact()
    {
        AssertWireInventory(
            SkillCanonicalJsonSchema.EntrySchema,
            typeof(SkillJsonDto),
            31,
            "skill root"
        );
        AssertWireInventory(
            GetObjectSchema(SkillCanonicalJsonSchema.CombatValueSchema),
            typeof(CombatSkillJsonDto),
            76,
            "combat profile"
        );
        AssertWireInventory(
            GetObjectSchema(SkillCanonicalJsonSchema.EffectValueSchema),
            typeof(CombatEffectJsonDto),
            194,
            "combat effect"
        );
    }

    private void TestNestedWireInventoriesAreExact()
    {
        ContentCanonicalJsonObjectSchema<SkillImportModel> root = SkillCanonicalJsonSchema.EntrySchema;
        ContentCanonicalJsonObjectSchema<CombatSkillImportModel> combat =
            GetObjectSchema(SkillCanonicalJsonSchema.CombatValueSchema);
        ContentCanonicalJsonObjectSchema<CombatEffectImportModel> effect =
            GetObjectSchema(SkillCanonicalJsonSchema.EffectValueSchema);

        AssertNestedWireInventory<SkillImportModel, AttributeModifierImportModel>(root, "attribute_modifiers", typeof(AttributeModifierJsonDto), 6, "attribute modifier");
        AssertNestedWireInventory<SkillImportModel, ContingencyAutomationImportModel>(root, "contingency_automation_profile", typeof(ContingencyAutomationJsonDto), 8, "contingency automation");
        AssertNestedWireInventory<CombatSkillImportModel, CombatWindupImportModel>(combat, "windup_profile", typeof(CombatWindupJsonDto), 4, "combat windup");
        AssertNestedWireInventory<CombatSkillImportModel, CombatDirectionalPiercingImportModel>(combat, "directional_piercing_profile", typeof(CombatDirectionalPiercingJsonDto), 8, "directional piercing");
        AssertNestedWireInventory<CombatSkillImportModel, CombatApproachAttackImportModel>(combat, "approach_attack_profile", typeof(CombatApproachAttackJsonDto), 1, "approach attack");
        AssertNestedWireInventory<CombatSkillImportModel, CombatLineThroughAttackImportModel>(combat, "line_through_attack_profile", typeof(CombatLineThroughAttackJsonDto), 7, "line-through attack");
        AssertNestedWireInventory<CombatSkillImportModel, CombatSequentialLineHitImportModel>(combat, "sequential_line_hit_profile", typeof(CombatSequentialLineHitJsonDto), 3, "sequential line hit");
        AssertNestedWireInventory<CombatSkillImportModel, CombatSpellReactionImportModel>(combat, "spell_reaction_profile", typeof(CombatSpellReactionJsonDto), 13, "spell reaction");
        AssertNestedWireInventory<CombatSkillImportModel, CombatRangedWeaponReactionImportModel>(combat, "ranged_weapon_reaction_profile", typeof(CombatRangedWeaponReactionJsonDto), 9, "ranged reaction");
        ContentCanonicalJsonObjectSchema<CombatCastVariantImportModel> castVariant =
            GetNestedObjectSchema<CombatSkillImportModel, CombatCastVariantImportModel>(combat, "cast_variants");
        AssertWireInventory(castVariant, typeof(CombatCastVariantJsonDto), 11, "cast variant");
        AssertNestedWireInventory<CombatEffectImportModel, CombatEffectSlotWeightImportModel>(effect, "equipment_durability_slot_weights", typeof(CombatEffectSlotWeightJsonDto), 2, "effect slot weight");
        AssertNestedWireInventory<CombatEffectImportModel, CombatDamageSegmentImportModel>(effect, "extra_damage_segments", typeof(CombatDamageSegmentJsonDto), 10, "damage segment");
        AssertNestedWireInventory<CombatEffectImportModel, CombatTargetDamageMultiplierRuleImportModel>(effect, "target_damage_multiplier_rules", typeof(CombatTargetDamageMultiplierRuleJsonDto), 4, "target multiplier rule");
        AssertNestedWireInventory<CombatEffectImportModel, CombatWeightedStatusOutcomeImportModel>(effect, "save_failure_status_outcomes", typeof(CombatWeightedStatusOutcomeJsonDto), 3, "weighted status outcome");

        AssertNestedWireInventory<CombatSkillImportModel, CombatSkillLevelOverrideImportModel>(combat, "level_overrides", typeof(SkillLevelOverrideJsonDto), 19, "level override");
        AssertNestedWireInventory<CombatCastVariantImportModel, CombatCastVariantPayloadImportModel>(castVariant, "payload", typeof(CombatCastVariantPayloadJsonDto), 1, "cast payload");
        AssertEffectPayloadWireInventories(effect);
    }

    private void TestNestedAndPayloadGetterDefaultOracles()
    {
        int expectedFieldCount = 0;
        int testedFieldCount = 0;
        int testedNonZeroDefaultResetCount = 0;
        foreach (ObjectOracleCase oracle in BuildObjectOracleCases())
        {
            PropertyInfo[] properties = GetDtoWireProperties(oracle.DtoType);
            expectedFieldCount += properties.Length;
            string baselineObject = BuildDefaultBaselineObject(oracle.DtoType, oracle.BaselineOverrides);
            JsonElement baseline = ParseWriteAndNavigate(
                $"{oracle.Label}_baseline",
                oracle.Wrap(baselineObject),
                oracle.Path
            );
            _test.Eq(
                string.Join("\n", GetObjectPropertyNames(baseline)),
                string.Join("\n", oracle.PersistentBaselineKeys.OrderBy(static x => x, StringComparer.Ordinal)),
                $"{oracle.Label} optional defaults should be omitted and required baseline keys retained"
            );

            foreach (PropertyInfo property in properties)
            {
                string wireName = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
                FieldOracleOverride? fieldOverride = oracle.FieldOverrides.TryGetValue(wireName, out FieldOracleOverride? configured)
                    ? configured
                    : null;
                string valueJson = fieldOverride?.ValueJson
                    ?? CreateNonDefaultJsonValue(oracle.DtoType, property);
                string fieldBaselineObject = fieldOverride?.BaselineObjectJson ?? baselineObject;
                JsonElement fieldBaseline = fieldOverride?.BaselineObjectJson == null
                    ? baseline
                    : ParseWriteAndNavigate(
                        $"{oracle.Label}_{wireName}_baseline",
                        oracle.Wrap(fieldBaselineObject),
                        oracle.Path
                    );
                string mutatedObject = ReplaceObjectProperty(fieldBaselineObject, wireName, valueJson);
                JsonElement mutated = ParseWriteAndNavigate(
                    $"{oracle.Label}_{wireName}",
                    oracle.Wrap(mutatedObject),
                    oracle.Path
                );
                _test.True(
                    mutated.TryGetProperty(wireName, out JsonElement actualValue),
                    $"{oracle.Label}.{wireName} non-default value should be written"
                );
                string expectedValueJson = fieldOverride?.ExpectedValueJson ?? valueJson;
                using JsonDocument expectedDocument = JsonDocument.Parse(expectedValueJson);
                _test.True(
                    JsonValuesEqual(actualValue, expectedDocument.RootElement),
                    $"{oracle.Label}.{wireName} getter should preserve the exact JSON value; actual={actualValue.GetRawText()} expected={expectedValueJson}"
                );
                _test.Eq(
                    ObjectWithoutProperty(mutated, wireName),
                    ObjectWithoutProperty(fieldBaseline, wireName),
                    $"{oracle.Label}.{wireName} mutation should affect only its own canonical key"
                );
                if (ReviewedNestedNonZeroDefaultFields.Contains(Key(oracle.DtoType, wireName)))
                    testedNonZeroDefaultResetCount += 1;
                testedFieldCount += 1;
            }
        }
        _test.Eq(testedFieldCount, expectedFieldCount, "every nested and payload DTO field must execute a getter/default oracle");
        _test.Eq(testedFieldCount, 125, "reviewed nested and payload getter/default field inventory should stay at 125");
        _test.Eq(testedNonZeroDefaultResetCount, 28, "reviewed nested and payload nonzero/true defaults should all execute omission and explicit-reset oracles");
    }

    private static IReadOnlyList<ObjectOracleCase> BuildObjectOracleCases()
    {
        return Array.AsReadOnly(new[]
        {
            Case("attribute_modifier", typeof(AttributeModifierJsonDto),
                static value => WrapRoot($"\"attribute_modifiers\":[{value}]"),
                new[] { "attribute_modifiers", "0" }),
            Case("contingency", typeof(ContingencyAutomationJsonDto),
                static value => WrapRoot($"\"contingency_automation_profile\":{value}"),
                new[] { "contingency_automation_profile" }),
            CombatCase("windup", typeof(CombatWindupJsonDto), "windup_profile"),
            CombatCase("directional", typeof(CombatDirectionalPiercingJsonDto), "directional_piercing_profile"),
            CombatCase("approach", typeof(CombatApproachAttackJsonDto), "approach_attack_profile"),
            CombatCase("line_through", typeof(CombatLineThroughAttackJsonDto), "line_through_attack_profile"),
            CombatCase("sequential", typeof(CombatSequentialLineHitJsonDto), "sequential_line_hit_profile"),
            CombatCase("spell_reaction", typeof(CombatSpellReactionJsonDto), "spell_reaction_profile"),
            CombatCase("ranged_reaction", typeof(CombatRangedWeaponReactionJsonDto), "ranged_weapon_reaction_profile"),
            Case(
                "cast_variant",
                typeof(CombatCastVariantJsonDto),
                static value => WrapRoot($"\"combat_profile\":{{\"skill_id\":\"oracle\",\"cast_variants\":[{value}]}}"),
                new[] { "combat_profile", "cast_variants", "0" },
                Map(("variant_id", "\"variant\"")),
                Set("variant_id"),
                FieldMap(
                    ("effect_defs", new FieldOracleOverride(
                        $"[{Effect("damage", "{}")}]",
                        ExpectedValueJson: $"[{CanonicalDamageEffectLiteral}]"
                    )),
                    ("footprint_pattern", new FieldOracleOverride("\"line2\"")),
                    ("payload", new FieldOracleOverride(
                        "{\"square2_corner\":\"bottom_right\"}",
                        "{\"variant_id\":\"variant\",\"footprint_pattern\":\"square2\",\"required_coord_count\":2,\"payload\":{\"square2_corner\":\"top_left\"}}"
                    ))
                )
            ),
            EffectNestedCase("slot_weight", typeof(CombatEffectSlotWeightJsonDto), "equipment_durability_slot_weights",
                Map(("slot_id", "\"head\"")), Set("slot_id")),
            EffectNestedCase("damage_segment", typeof(CombatDamageSegmentJsonDto), "extra_damage_segments"),
            EffectNestedCase("target_multiplier", typeof(CombatTargetDamageMultiplierRuleJsonDto), "target_damage_multiplier_rules",
                fieldOverrides: FieldMap(("multiplier_percent", new FieldOracleOverride("99")))),
            EffectNestedCase("weighted_outcome", typeof(CombatWeightedStatusOutcomeJsonDto), "save_failure_status_outcomes",
                Map(("status_effect", Effect("status", "{}"))), Set("status_effect"),
                FieldMap(("status_effect", new FieldOracleOverride(
                    Effect("heal", "{}"),
                    ExpectedValueJson: CanonicalHealEffectLiteral
                )))),

            PayloadCase("payload_empty", typeof(EmptyCombatEffectPayloadJsonDto), "damage"),
            PayloadCase("payload_status", typeof(StatusEffectPayloadJsonDto), "status"),
            PayloadCase("payload_heal", typeof(HealEffectPayloadJsonDto), "heal"),
            PayloadCase("payload_equipment", typeof(EquipmentDurabilityDamageEffectPayloadJsonDto), "equipment_durability_damage",
                Map(("target_slots", "[\"head\"]")), Set("target_slots"),
                FieldMap(
                    ("max_damaged_items", new FieldOracleOverride("0")),
                    ("target_slots", new FieldOracleOverride("[\"body\"]"))
                )),
            PayloadCase(
                "payload_repeat",
                typeof(RepeatAttackUntilFailEffectPayloadJsonDto),
                "repeat_attack_until_fail",
                fieldOverrides: FieldMap(
                    ("follow_up_cost_multiplier", new FieldOracleOverride("0.0"))
                )
            ),
            PayloadCase("payload_barrier", typeof(LayeredBarrierEffectPayloadJsonDto), "layered_barrier",
                Map(("area_pattern", "\"radius\""), ("profile_id", "\"ward\""), ("radius_cells", "1"), ("save_dc", "10")),
                Set("area_pattern", "profile_id", "radius_cells", "save_dc")),
            PayloadCase("payload_graded", typeof(GradedSaveExecuteEffectPayloadJsonDto), "graded_save_execute",
                Map(
                    ("critical_failure_damage_dice_count", "1"), ("critical_failure_damage_dice_sides", "6"),
                    ("critical_failure_execute_threshold_max_hp_percent", "10"), ("critical_failure_frightened_duration_tu", "1"),
                    ("critical_failure_stunned_duration_tu", "1"), ("failure_damage_dice_count", "1"),
                    ("failure_damage_dice_sides", "6"), ("failure_execute_threshold_fixed", "1"),
                    ("failure_execute_threshold_max_hp_percent", "5"), ("failure_frightened_duration_tu", "1"),
                    ("failure_reaction_lock_duration_tu", "1"), ("profile_id", "\"execute_profile\""),
                    ("success_aftershock_duration_tu", "1")
                ),
                Set(
                    "critical_failure_damage_dice_count", "critical_failure_damage_dice_sides",
                    "critical_failure_execute_threshold_max_hp_percent", "critical_failure_frightened_duration_tu",
                    "critical_failure_stunned_duration_tu", "failure_damage_dice_count",
                    "failure_damage_dice_sides", "failure_execute_threshold_fixed",
                    "failure_execute_threshold_max_hp_percent", "failure_frightened_duration_tu",
                    "failure_reaction_lock_duration_tu", "profile_id", "success_aftershock_duration_tu"
                )
            ),
            PayloadCase("payload_dispel", typeof(DispelMagicEffectPayloadJsonDto), "dispel_magic"),
            PayloadCase("payload_on_kill", typeof(OnKillGainResourcesEffectPayloadJsonDto), "on_kill_gain_resources"),
        });
    }

    private static ObjectOracleCase Case(
        string label,
        Type dtoType,
        Func<string, string> wrap,
        string[] path,
        IReadOnlyDictionary<string, string>? baselineOverrides = null,
        IReadOnlySet<string>? persistentBaselineKeys = null,
        IReadOnlyDictionary<string, FieldOracleOverride>? fieldOverrides = null
    ) => new(
        label,
        dtoType,
        wrap,
        path,
        baselineOverrides ?? Map(),
        persistentBaselineKeys ?? Set(),
        fieldOverrides ?? FieldMap()
    );

    private static ObjectOracleCase CombatCase(string label, Type dtoType, string propertyName) =>
        Case(
            label,
            dtoType,
            value => WrapRoot($"\"combat_profile\":{{\"skill_id\":\"oracle\",\"{propertyName}\":{value}}}"),
            new[] { "combat_profile", propertyName }
        );

    private static ObjectOracleCase EffectNestedCase(
        string label,
        Type dtoType,
        string propertyName,
        IReadOnlyDictionary<string, string>? baselineOverrides = null,
        IReadOnlySet<string>? persistentBaselineKeys = null,
        IReadOnlyDictionary<string, FieldOracleOverride>? fieldOverrides = null
    ) => Case(
        label,
        dtoType,
        value => WrapRoot(
            $"\"combat_profile\":{{\"skill_id\":\"oracle\",\"effect_defs\":[{{\"effect_type\":\"damage\",\"payload\":{{}},\"{propertyName}\":[{value}]}}]}}"
        ),
        new[] { "combat_profile", "effect_defs", "0", propertyName, "0" },
        baselineOverrides,
        persistentBaselineKeys,
        fieldOverrides
    );

    private static ObjectOracleCase PayloadCase(
        string label,
        Type dtoType,
        string kind,
        IReadOnlyDictionary<string, string>? baselineOverrides = null,
        IReadOnlySet<string>? persistentBaselineKeys = null,
        IReadOnlyDictionary<string, FieldOracleOverride>? fieldOverrides = null
    ) => Case(
        label,
        dtoType,
        value => WrapRoot(
            $"\"combat_profile\":{{\"skill_id\":\"oracle\",\"effect_defs\":[{{\"effect_type\":\"{kind}\",\"payload\":{value}}}]}}"
        ),
        new[] { "combat_profile", "effect_defs", "0", "payload" },
        baselineOverrides,
        persistentBaselineKeys,
        fieldOverrides
    );

    private static string WrapRoot(string members) =>
        $"{{\"skill_id\":\"oracle\",\"display_name\":\"Oracle\",\"max_level\":100,{members}}}";

    private static Dictionary<string, string> Map(
        params (string Key, string Value)[] entries
    ) => entries.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.Ordinal);

    private static string Key<TDto>(string wireName) => Key(typeof(TDto), wireName);

    private static string Key(Type dtoType, string wireName) =>
        $"{dtoType.FullName}.{wireName}";

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static Dictionary<string, FieldOracleOverride> FieldMap(
        params (string Key, FieldOracleOverride Value)[] entries
    ) => entries.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.Ordinal);

    private static PropertyInfo[] GetDtoWireProperties(Type dtoType) =>
        dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetCustomAttribute<JsonPropertyNameAttribute>() != null)
            .OrderBy(
                static property => property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name,
                StringComparer.Ordinal
            )
            .ToArray();

    private static string BuildDefaultBaselineObject(
        Type dtoType,
        IReadOnlyDictionary<string, string> overrides
    )
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (PropertyInfo property in GetDtoWireProperties(dtoType))
        {
            string wireName = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
            if (overrides.TryGetValue(wireName, out string? configured))
            {
                values.Add(wireName, configured);
                continue;
            }
            if (property.GetCustomAttribute<JsonRequiredAttribute>() != null)
                throw new InvalidOperationException($"{dtoType.Name}.{wireName} requires an explicit oracle baseline.");
        }
        foreach (KeyValuePair<string, string> entry in overrides)
        {
            if (!values.ContainsKey(entry.Key))
                values.Add(entry.Key, entry.Value);
        }
        return WriteObjectJson(values);
    }

    private static bool ShouldIncludeDefaultBaselineValue(object? value)
    {
        if (value == null)
            return false;
        return value switch
        {
            string text => text.Length != 0,
            bool boolean => boolean,
            int integer => integer != 0,
            long integer => integer != 0,
            double number => number != 0.0,
            System.Collections.IEnumerable sequence => sequence.GetEnumerator().MoveNext(),
            _ => true,
        };
    }

    private static string CreateNonDefaultJsonValue(Type dtoType, PropertyInfo property)
    {
        object dto = Activator.CreateInstance(dtoType, nonPublic: true)
            ?? throw new InvalidOperationException($"Cannot construct {dtoType.Name} default DTO.");
        object? defaultValue = property.GetValue(dto);
        Type type = property.PropertyType;
        if (type == typeof(bool?))
            return "false";
        if (type == typeof(bool))
            return (defaultValue is true ? false : true) ? "true" : "false";
        if (type == typeof(int?))
            return "0";
        if (type == typeof(int))
            return ((defaultValue as int? ?? 0) + 41).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (type == typeof(long) || type == typeof(long?))
            return ((defaultValue as long? ?? 0L) + 41L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (type == typeof(double?))
            return "0.0";
        if (type == typeof(double))
            return ((defaultValue as double? ?? 0.0) + 1.25).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (type == typeof(string))
            return JsonSerializer.Serialize(ChooseNonDefaultString(property, defaultValue as string));
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            Type[] arguments = type.GetGenericArguments();
            if (arguments.Length == 1 && arguments[0] == typeof(int))
                return "[41]";
            if (arguments.Length == 1 && arguments[0] == typeof(string))
                return $"[{JsonSerializer.Serialize(ChooseNonDefaultString(property, null))}]";
            if (arguments.Length == 1 && arguments[0] == typeof(CombatEffectJsonDto))
                return $"[{Effect("damage", "{}")}]";
            if (arguments.Length == 2 && arguments[1] == typeof(int))
                return "{\"2\":3}";
            if (arguments.Length == 2 && arguments[1] == typeof(JsonElement))
                return "{\"oracle\":9223372036854775807}";
        }
        throw new InvalidOperationException(
            $"No automatic legal non-default JSON value for {dtoType.Name}.{property.Name} ({type})."
        );
    }

    private static string ChooseNonDefaultString(PropertyInfo property, string? defaultValue)
    {
        ContentJsonSchemaStableStringValuesAttribute? attribute =
            property.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>();
        if (attribute != null)
        {
            var provider = (IContentJsonSchemaStableStringValues)(
                Activator.CreateInstance(attribute.ProviderType, nonPublic: true)
                ?? throw new InvalidOperationException($"Cannot construct {attribute.ProviderType.Name}.")
            );
            string? candidate = provider.Values.FirstOrDefault(
                value => !string.Equals(value, defaultValue, StringComparison.Ordinal)
            );
            return candidate
                ?? throw new InvalidOperationException(
                    $"{property.DeclaringType!.Name}.{property.Name} has no non-default stable string value."
                );
        }
        string wireName = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
        return $"oracle_{wireName}";
    }

    private static string SerializeJsonValue(object value) =>
        JsonSerializer.Serialize(value, value.GetType());

    private static string WriteObjectJson(IEnumerable<KeyValuePair<string, string>> properties) =>
        "{" + string.Join(
            ",",
            properties.Select(static entry => $"{JsonSerializer.Serialize(entry.Key)}:{entry.Value}")
        ) + "}";

    private static string ReplaceObjectProperty(string objectJson, string propertyName, string valueJson)
    {
        using JsonDocument document = JsonDocument.Parse(objectJson);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                properties.Add(property.Name, property.Value.GetRawText());
        }
        properties.Add(propertyName, valueJson);
        return WriteObjectJson(properties);
    }

    private JsonElement ParseWriteAndNavigate(string id, string json, IReadOnlyList<string> path)
    {
        ContentImportStageResult<SkillImportModel> parsed = Parse(id, json);
        if (!parsed.HasValue)
        {
            _test.Fail($"{id} oracle must parse and cannot be skipped: {Format(parsed)}");
            throw new InvalidOperationException($"Getter/default oracle parse failed for {id}.");
        }
        string canonical = new ContentCanonicalJsonWriter().Write(
            parsed.Value,
            SkillCanonicalJsonSchema.EntrySchema,
            indented: false
        );
        using JsonDocument document = JsonDocument.Parse(canonical);
        JsonElement current = document.RootElement;
        foreach (string segment in path)
        {
            current = int.TryParse(segment, out int index)
                ? current[index]
                : current.GetProperty(segment);
        }
        return current.Clone();
    }

    private static string[] GetObjectPropertyNames(JsonElement value) =>
        value.EnumerateObject().Select(static x => x.Name)
            .OrderBy(static x => x, StringComparer.Ordinal).ToArray();

    private static string ObjectWithoutProperty(JsonElement value, string excludedProperty)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!string.Equals(property.Name, excludedProperty, StringComparison.Ordinal))
                properties.Add(property.Name, property.Value.GetRawText());
        }
        return WriteObjectJson(properties);
    }

    private static bool JsonValuesEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
            return false;
        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] leftProperties = left.EnumerateObject()
                    .OrderBy(static x => x.Name, StringComparer.Ordinal).ToArray();
                JsonProperty[] rightProperties = right.EnumerateObject()
                    .OrderBy(static x => x.Name, StringComparer.Ordinal).ToArray();
                if (leftProperties.Length != rightProperties.Length)
                    return false;
                for (int index = 0; index < leftProperties.Length; index += 1)
                {
                    if (
                        !string.Equals(leftProperties[index].Name, rightProperties[index].Name, StringComparison.Ordinal)
                        || !JsonValuesEqual(leftProperties[index].Value, rightProperties[index].Value)
                    )
                        return false;
                }
                return true;
            case JsonValueKind.Array:
                JsonElement.ArrayEnumerator leftItems = left.EnumerateArray();
                JsonElement.ArrayEnumerator rightItems = right.EnumerateArray();
                while (true)
                {
                    bool hasLeft = leftItems.MoveNext();
                    bool hasRight = rightItems.MoveNext();
                    if (hasLeft != hasRight)
                        return false;
                    if (!hasLeft)
                        return true;
                    if (!JsonValuesEqual(leftItems.Current, rightItems.Current))
                        return false;
                }
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return false;
        }
    }

    private void TestEffectKindsMapToExpectedPayloadShapes()
    {
        var expected = new Dictionary<CombatEffectImportKind, CombatEffectPayloadShape>
        {
            [CombatEffectImportKind.BodySizeCategoryOverride] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.ChainDamage] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.Charge] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.CleanseHarmful] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.Damage] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.DispelMagic] = CombatEffectPayloadShape.DispelMagic,
            [CombatEffectImportKind.EquipmentDurabilityDamage] = CombatEffectPayloadShape.EquipmentDurabilityDamage,
            [CombatEffectImportKind.EraseStatus] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.Execute] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.FixedRepeatAttack] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.ForcedMove] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.GradedSaveExecute] = CombatEffectPayloadShape.GradedSaveExecute,
            [CombatEffectImportKind.Heal] = CombatEffectPayloadShape.Heal,
            [CombatEffectImportKind.HealFatal] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.HeightDelta] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.LayeredBarrier] = CombatEffectPayloadShape.LayeredBarrier,
            [CombatEffectImportKind.OnKillGainResources] = CombatEffectPayloadShape.OnKillGainResources,
            [CombatEffectImportKind.PathStepAoe] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.PositionSwap] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.RepeatAttackUntilFail] = CombatEffectPayloadShape.RepeatAttackUntilFail,
            [CombatEffectImportKind.Shield] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.SourceRetreat] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.StaminaRestore] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.Status] = CombatEffectPayloadShape.Status,
            [CombatEffectImportKind.TerrainEffect] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.TerrainReplace] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.TerrainReplaceTo] = CombatEffectPayloadShape.Empty,
            [CombatEffectImportKind.VaultBehindTarget] = CombatEffectPayloadShape.Empty,
        };
        CombatEffectImportKind[] kinds = Enum.GetValues<CombatEffectImportKind>();
        _test.Eq(kinds.Length, 28, "effect kind inventory should stay reviewed at 28");
        _test.Eq(expected.Count, kinds.Length, "every effect kind should have an independent expected payload shape");
        foreach (CombatEffectImportKind kind in kinds)
        {
            _test.True(expected.TryGetValue(kind, out CombatEffectPayloadShape shape), $"effect kind {kind} should have a reviewed payload mapping");
            _test.Eq(SkillFullCombatEffectClosedSpec.GetPayloadShape(kind), shape, $"effect kind {kind} payload shape should stay exact");
        }
    }

    private void TestReverseWireDomainsAreClosedAndBijective()
    {
        AssertWireDomain<SkillImportType>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseSkillType);
        AssertWireDomain<SkillImportLearnSource>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseLearnSource);
        AssertWireDomain<CombatSkillImportTargetMode>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseTargetMode);
        AssertWireDomain<CombatSkillImportTargetTeamFilter>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseTargetTeamFilter);
        AssertWireDomain<CombatSkillImportRangePattern>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseRangePattern);
        AssertWireDomain<CombatSkillImportAreaPattern>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseAreaPattern);
        AssertWireDomain<PendingCastBindingModeKind>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParsePendingCastBindingMode);
        AssertWireDomain<CombatSkillLevelOverrideAttackResolutionMode>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseLevelOverrideAttackResolutionMode);
        AssertWireDomain<CombatSkillLevelOverrideAttackDefenseMode>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseLevelOverrideAttackDefenseMode);
        AssertWireDomain<CombatSkillLevelOverrideAreaPattern>(SkillJsonImportValueRules.GetWireValue, SkillJsonImportValueRules.TryParseLevelOverrideAreaPattern);

        AssertWireDomain<SkillImportUnlockMode>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryUnlockMode);
        AssertWireDomain<SkillImportCoreSkillTransitionMode>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryCoreTransition);
        AssertWireDomain<SkillImportProgressionTier>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryTier);
        AssertWireDomain<AttributeModifierImportMode>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryAttributeModifierMode);
        AssertWireDomain<CombatWeaponRangePolicyImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryWeaponRangePolicy);
        AssertWireDomain<CombatMasteryTriggerImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryMasteryTrigger);
        AssertWireDomain<CombatMasteryAmountImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryMasteryAmount);
        AssertWireDomain<CombatSpellFateImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TrySpellFate);
        AssertWireDomain<CombatSpellCriticalImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TrySpellCritical);
        AssertWireDomain<CombatBacklashImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryBacklash);
        AssertWireDomain<CombatAreaOriginImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryAreaOrigin);
        AssertWireDomain<CombatAreaDirectionImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryAreaDirection);
        AssertWireDomain<CombatProjectileImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryProjectile);
        AssertWireDomain<CombatBaseProjectileImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryBaseProjectile);
        AssertWireDomain<CombatTargetSelectionImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryTargetSelection);
        AssertWireDomain<CombatUnitTargetResolutionImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryUnitTargetResolution);
        AssertWireDomain<CombatSelectionOrderImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TrySelectionOrder);
        AssertWireDomain<CombatCastFootprintImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryFootprint);
        AssertWireDomain<CombatSaveAbilityImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TrySaveAbility);
        AssertWireDomain<DamageTagImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryDamageTag);
        AssertWireDomain<BattleTerrainImportKind>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TryTerrain);
        AssertWireDomain<CombatCastSquare2Corner>(SkillRootCombatImportValueRules.GetWireValue, SkillRootCombatImportValueRules.TrySquare2Corner);

        AssertWireDomain<CombatTickEffectImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryTickEffect);
        AssertWireDomain<CombatEffectLifetimeImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryLifetime);
        AssertWireDomain<CombatPathStepAreaPatternImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryPathStepAreaPattern);
        AssertWireDomain<DamageMitigationTierImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryMitigationTier);
        AssertWireDomain<DamageCategoryImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryDamageCategory);
        AssertWireDomain<ShieldAttributeModifierImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryShieldAttribute);
        AssertWireDomain<CombatEffectTargetTeamFilterImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryEffectTargetTeamFilter);
        AssertWireDomain<CombatEffectTargetOrderImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryTargetOrder);
        AssertWireDomain<CombatCognitionImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryCognition);
        AssertWireDomain<CombatTerrainContactImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryTerrainContact);
        AssertWireDomain<CombatBodySizeImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryBodySize);
        AssertWireDomain<CombatForcedMoveImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryForcedMove);
        AssertWireDomain<CombatStackBehaviorImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryStackBehavior);
        AssertWireDomain<CombatDamageBonusConditionImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryBonusCondition);
        AssertWireDomain<CombatEffectTriggerEventImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryTriggerEvent);
        AssertWireDomain<CombatEffectTriggerConditionImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryTriggerCondition);
        AssertWireDomain<CombatSaveDcModeImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TrySaveDcMode);
        AssertWireDomain<CombatSaveTagImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TrySaveTag);
        AssertWireDomain<CombatResourceImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryResource);
        AssertWireDomain<CombatStatusSourceSelectorImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryStatusSourceSelector);
        AssertWireDomain<CombatEquipmentSlotImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryEquipmentSlot);
        AssertWireDomain<CombatOnKillGrantScopeImportKind>(SkillCombatEffectValueRules.GetWireValue, SkillCombatEffectValueRules.TryGrantScope);
        AssertWireDomain<CombatEffectImportKind>(SkillFullCombatEffectClosedSpec.GetWireValue, SkillFullCombatEffectClosedSpec.TryParseKind);
    }

    private void TestCanonicalNestedGraphAndPayloadsRoundTrip()
    {
        string json = "{" +
            "\"skill_id\":\"writer_skill\",\"display_name\":\"Writer\",\"max_level\":10," +
            "\"attribute_modifiers\":[{\"attribute_id\":\"strength\",\"value\":1}]," +
            "\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{\"text\":\"v\"}}," +
            "\"combat_profile\":{" +
            "\"skill_id\":\"writer_skill\",\"windup_profile\":{},\"directional_piercing_profile\":{}," +
            "\"approach_attack_profile\":{},\"line_through_attack_profile\":{}," +
            "\"sequential_line_hit_profile\":{},\"spell_reaction_profile\":{}," +
            "\"ranged_weapon_reaction_profile\":{},\"level_overrides\":{\"2\":{\"ap_cost\":2}}," +
            "\"cast_variants\":[{\"variant_id\":\"square_cast\",\"footprint_pattern\":\"square2\",\"required_coord_count\":2,\"payload\":{\"square2_corner\":\"top_left\"}}]," +
            "\"effect_defs\":[" +
            Effect("status", "{\"breaks_barrier_layer\":\"ward\",\"source_skill_id\":\"writer_skill\"}") + "," +
            Effect("heal", "{\"con_mod_heal\":true}") + "," +
            Effect("equipment_durability_damage", "{\"max_damaged_items\":2,\"target_slots\":[\"head\"]}") + "," +
            Effect("repeat_attack_until_fail", "{\"follow_up_cost_multiplier\":2.0,\"penalty_free_stages_by_level\":{\"10\":2,\"2\":1}}") + "," +
            Effect("layered_barrier", "{\"area_pattern\":\"radius\",\"profile_id\":\"ward\",\"radius_cells\":1,\"save_dc\":10}") + "," +
            Effect("graded_save_execute", GradedPayload()) + "," +
            Effect("dispel_magic", "{\"breaks_barrier_layer\":\"ward\"}") + "," +
            Effect("on_kill_gain_resources", "{\"grant_scope\":\"current_turn\",\"stack_on_multiple_kills\":true}") + "," +
            "{\"effect_type\":\"damage\",\"payload\":{}," +
            "\"equipment_durability_slot_weights\":[{\"slot_id\":\"head\",\"weight\":2}]," +
            "\"extra_damage_segments\":[{\"damage_tag\":\"fire\",\"pre_resistance_damage_multiplier\":2.0}]," +
            "\"target_damage_multiplier_rules\":[{\"any_creature_type_tags\":[\"undead\"]}]," +
            "\"save_failure_status_outcomes\":[{\"outcome_id\":\"nested\",\"status_effect\":" + Effect("status", "{}") + "}]}" +
            "]}}";

        ContentImportStageResult<SkillImportModel> parsed = Parse("nested", json);
        _test.True(parsed.HasValue, "full nested graph should parse before writing: " + Format(parsed));
        if (!parsed.HasValue)
            return;

        var writer = new ContentCanonicalJsonWriter();
        string canonical = writer.Write(parsed.Value, SkillCanonicalJsonSchema.EntrySchema, indented: false);
        ContentImportStageResult<SkillImportModel> reparsed = Parse("nested_reparse", canonical);
        _test.True(reparsed.HasValue, "canonical full nested graph should reparse: " + Format(reparsed));
        if (!reparsed.HasValue)
            return;
        string second = writer.Write(reparsed.Value, SkillCanonicalJsonSchema.EntrySchema, indented: false);
        _test.Eq(second, canonical, "canonical skill writing should be byte-stable after parse/write");

        using JsonDocument document = JsonDocument.Parse(canonical);
        JsonElement effects = document.RootElement.GetProperty("combat_profile").GetProperty("effect_defs");
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement effect in effects.EnumerateArray())
            shapes.Add(effect.GetProperty("effect_type").GetString()!);
        _test.Eq(shapes.Count, 9, "fixture should exercise empty plus all eight typed payload shapes");
        JsonElement nested = effects[8].GetProperty("save_failure_status_outcomes")[0].GetProperty("status_effect");
        _test.Eq(nested.GetProperty("effect_type").GetString(), "status", "Deferred schema should write recursive weighted status effects");
    }

    private void TestOrderedMapsAndFloatingPointTokens()
    {
        string json = "{" +
            "\"skill_id\":\"map_skill\",\"display_name\":\"Map\",\"max_level\":10," +
            "\"level_description_configs\":{\"10\":{\"z\":\"1\",\"a\":\"2\"},\"2\":{}}," +
            "\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{\"z\":true,\"a\":[\"x\"],\"f\":1.5,\"i\":9223372036854775807}}," +
            "\"combat_profile\":{\"skill_id\":\"map_skill\",\"effect_defs\":[" +
            Effect("repeat_attack_until_fail", "{\"follow_up_cost_multiplier\":2.0,\"penalty_free_stages_by_level\":{\"10\":1,\"2\":2}}") + "]}}";
        ContentImportStageResult<SkillImportModel> parsed = Parse("maps", json);
        _test.True(parsed.HasValue, "ordered-map fixture should parse: " + Format(parsed));
        if (!parsed.HasValue)
            return;
        string canonical = new ContentCanonicalJsonWriter().Write(parsed.Value, SkillCanonicalJsonSchema.EntrySchema, indented: false);
        _test.True(canonical.IndexOf("\"2\":{}", StringComparison.Ordinal) < canonical.IndexOf("\"10\":{", StringComparison.Ordinal), "numeric level keys should use numeric order");
        _test.True(canonical.IndexOf("\"a\":[", StringComparison.Ordinal) < canonical.IndexOf("\"z\":true", StringComparison.Ordinal), "identifier map keys should use Ordinal order");
        _test.True(canonical.Contains("\"i\":9223372036854775807", StringComparison.Ordinal), "contingency Int64 branch should preserve the full primitive value");
        _test.True(canonical.Contains("\"follow_up_cost_multiplier\":2.0", StringComparison.Ordinal), "integral double must retain a floating-point JSON token");
    }

    private void TestWindupCanonicalDefaults()
    {
        string defaultJson = "{\"skill_id\":\"windup_default\",\"display_name\":\"Windup\",\"combat_profile\":{\"skill_id\":\"windup_default\",\"windup_profile\":{}}}";
        ContentImportStageResult<SkillImportModel> parsedDefault = Parse("windup_default", defaultJson);
        _test.True(parsedDefault.HasValue, "empty windup profile should parse: " + Format(parsedDefault));
        if (parsedDefault.HasValue)
        {
            string canonical = new ContentCanonicalJsonWriter().Write(parsedDefault.Value, SkillCanonicalJsonSchema.EntrySchema, indented: false);
            using JsonDocument document = JsonDocument.Parse(canonical);
            JsonElement windup = document.RootElement.GetProperty("combat_profile").GetProperty("windup_profile");
            _test.True(!windup.TryGetProperty("skill_level_tier_caps", out _), "canonical windup defaults should not expand skill_level_tier_caps");
            _test.True(!windup.TryGetProperty("base_weapon_dice_multipliers", out _), "canonical windup defaults should not expand base_weapon_dice_multipliers");
        }

        string customJson = "{\"skill_id\":\"windup_custom\",\"display_name\":\"Windup\",\"combat_profile\":{\"skill_id\":\"windup_custom\",\"windup_profile\":{\"skill_level_tier_caps\":[9],\"base_weapon_dice_multipliers\":[3]}}}";
        ContentImportStageResult<SkillImportModel> parsedCustom = Parse("windup_custom", customJson);
        _test.True(parsedCustom.HasValue, "non-default windup arrays should parse: " + Format(parsedCustom));
        if (parsedCustom.HasValue)
        {
            string canonical = new ContentCanonicalJsonWriter().Write(parsedCustom.Value, SkillCanonicalJsonSchema.EntrySchema, indented: false);
            _test.True(canonical.Contains("\"skill_level_tier_caps\":[9]", StringComparison.Ordinal), "non-default windup tier caps must be written");
            _test.True(canonical.Contains("\"base_weapon_dice_multipliers\":[3]", StringComparison.Ordinal), "non-default windup dice multipliers must be written");
        }
    }

    private void TestWriterRejectsMismatchedEffectKindAndPayload()
    {
        var effect = new CombatEffectImportModel(
            CombatEffectImportKind.Damage,
            EmptyCombatEffectPayloadImportModel.Instance
        );
        FieldInfo kind = typeof(CombatEffectImportModel).GetField(
            "<Kind>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        kind.SetValue(effect, CombatEffectImportKind.Status);
        var buffer = new ArrayBufferWriter<byte>();
        using var json = new Utf8JsonWriter(buffer);
        _test.True(
            Throws<JsonException>(() => SkillCanonicalJsonSchema.EffectValueSchema.Write(json, effect)),
            "writer must reject an effect kind/payload mismatch independently of model construction"
        );
    }

    private void AssertNestedWireInventory<TParent, TChild>(
        ContentCanonicalJsonObjectSchema<TParent> parent,
        string propertyName,
        Type dtoType,
        int expectedCount,
        string label
    ) => AssertWireInventory(
        GetNestedObjectSchema<TParent, TChild>(parent, propertyName),
        dtoType,
        expectedCount,
        label
    );

    private void AssertEffectPayloadWireInventories(
        ContentCanonicalJsonObjectSchema<CombatEffectImportModel> effect
    )
    {
        object union = GetPropertyValueSchema(effect, "payload");
        object cases = GetPrivateField(union, "_cases");
        var seen = new HashSet<CombatEffectPayloadShape>();
        foreach (object pair in (System.Collections.IEnumerable)cases)
        {
            Type pairType = pair.GetType();
            var shape = (CombatEffectPayloadShape)pairType.GetProperty("Key")!.GetValue(pair)!;
            object unionCase = pairType.GetProperty("Value")!.GetValue(pair)!;
            object objectSchema = FindObjectSchema(
                GetPrivateField(unionCase, "_valueSchema")
            );
            _test.True(seen.Add(shape), $"payload schema {shape} should be unique");
            switch (shape)
            {
                case CombatEffectPayloadShape.Empty:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<EmptyCombatEffectPayloadImportModel>)objectSchema, typeof(EmptyCombatEffectPayloadJsonDto), 0, "empty effect payload");
                    break;
                case CombatEffectPayloadShape.Status:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<StatusEffectPayloadImportModel>)objectSchema, typeof(StatusEffectPayloadJsonDto), 2, "status effect payload");
                    break;
                case CombatEffectPayloadShape.Heal:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<HealEffectPayloadImportModel>)objectSchema, typeof(HealEffectPayloadJsonDto), 1, "heal effect payload");
                    break;
                case CombatEffectPayloadShape.EquipmentDurabilityDamage:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<EquipmentDurabilityDamageEffectPayloadImportModel>)objectSchema, typeof(EquipmentDurabilityDamageEffectPayloadJsonDto), 2, "equipment effect payload");
                    break;
                case CombatEffectPayloadShape.RepeatAttackUntilFail:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<RepeatAttackUntilFailEffectPayloadImportModel>)objectSchema, typeof(RepeatAttackUntilFailEffectPayloadJsonDto), 10, "repeat effect payload");
                    break;
                case CombatEffectPayloadShape.LayeredBarrier:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<LayeredBarrierEffectPayloadImportModel>)objectSchema, typeof(LayeredBarrierEffectPayloadJsonDto), 4, "layered barrier payload");
                    break;
                case CombatEffectPayloadShape.GradedSaveExecute:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<GradedSaveExecuteEffectPayloadImportModel>)objectSchema, typeof(GradedSaveExecuteEffectPayloadJsonDto), 13, "graded-save payload");
                    break;
                case CombatEffectPayloadShape.DispelMagic:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<DispelMagicEffectPayloadImportModel>)objectSchema, typeof(DispelMagicEffectPayloadJsonDto), 1, "dispel payload");
                    break;
                case CombatEffectPayloadShape.OnKillGainResources:
                    AssertWireInventory((ContentCanonicalJsonObjectSchema<OnKillGainResourcesEffectPayloadImportModel>)objectSchema, typeof(OnKillGainResourcesEffectPayloadJsonDto), 3, "on-kill payload");
                    break;
                default:
                    _test.Fail($"Unexpected effect payload schema shape {shape}");
                    break;
            }
        }
        _test.Eq(seen.Count, 9, "all nine effect payload object schemas should be inventoried");
        foreach (CombatEffectPayloadShape shape in Enum.GetValues<CombatEffectPayloadShape>())
            _test.True(seen.Contains(shape), $"payload object schema {shape} should be present");
    }

    private void AssertWireInventory<T>(
        ContentCanonicalJsonObjectSchema<T> schema,
        Type dtoType,
        int expectedCount,
        string label
    )
    {
        string[] actual = GetSchemaPropertyNames(schema);
        string[] expected = dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(static name => name != null)
            .Select(static name => name!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        _test.Eq(actual.Length, expectedCount, $"{label} writer field count should stay exact");
        _test.Eq(expected.Length, expectedCount, $"{label} DTO field count should stay exact");
        _test.Eq(string.Join("\n", actual), string.Join("\n", expected), $"{label} writer fields should exactly match JsonPropertyName ABI");
    }

    private static ContentCanonicalJsonObjectSchema<TChild> GetNestedObjectSchema<TParent, TChild>(
        ContentCanonicalJsonObjectSchema<TParent> parent,
        string propertyName
    ) => (ContentCanonicalJsonObjectSchema<TChild>)FindObjectSchema(
        GetPropertyValueSchema(parent, propertyName)
    );

    private static object GetPropertyValueSchema<T>(
        ContentCanonicalJsonObjectSchema<T> schema,
        string propertyName
    )
    {
        object properties = typeof(ContentCanonicalJsonObjectSchema<T>).GetField(
            "_properties", BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(schema)!;
        foreach (object property in (System.Collections.IEnumerable)properties)
        {
            string name = (string)typeof(ContentCanonicalJsonProperty<T>).GetProperty(
                "JsonName", BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(property)!;
            if (string.Equals(name, propertyName, StringComparison.Ordinal))
                return GetPrivateField(property, "_valueSchema");
        }
        throw new InvalidOperationException($"Canonical property '{propertyName}' was not found.");
    }

    private static object FindObjectSchema(object valueSchema)
    {
        object current = valueSchema;
        for (int depth = 0; depth < 12; depth += 1)
        {
            FieldInfo? schema = current.GetType().GetField(
                "_schema", BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (schema != null)
                return schema.GetValue(current)!;
            FieldInfo? next = current.GetType().GetField(
                "_boundSchema", BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? current.GetType().GetField(
                "_elementSchema", BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? current.GetType().GetField(
                "_valueSchema", BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (next == null || next.GetValue(current) is not object nextValue)
                break;
            current = nextValue;
        }
        throw new InvalidOperationException($"No object schema is reachable from {valueSchema.GetType().Name}.");
    }

    private static object GetPrivateField(object owner, string fieldName) =>
        owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
        ?? throw new InvalidOperationException($"{owner.GetType().Name}.{fieldName} was not found.");

    private void AssertWireDomain<T>(Func<T, string> write, TryWireValue<T> parse)
        where T : struct, Enum
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (T value in Enum.GetValues<T>())
        {
            string wire = write(value);
            _test.True(seen.Add(wire), $"{typeof(T).Name} reverse wire '{wire}' should be unique");
            _test.True(parse(wire, out T reparsed) && EqualityComparer<T>.Default.Equals(value, reparsed), $"{typeof(T).Name}.{value} should reverse-round-trip");
        }
        T unknown = (T)Enum.ToObject(typeof(T), int.MaxValue);
        _test.True(Throws<ArgumentOutOfRangeException>(() => write(unknown)), $"{typeof(T).Name} unknown enum must fail closed");
    }

    private static ContentCanonicalJsonObjectSchema<T> GetObjectSchema<T>(
        ContentCanonicalJsonValueSchema<T> valueSchema
    )
    {
        object current = valueSchema;
        while (current.GetType().Name.StartsWith("ContentCanonicalJsonDeferredValueSchema", StringComparison.Ordinal))
        {
            current = current.GetType().GetField("_boundSchema", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(current)!;
        }
        object schema = current.GetType().GetField("_schema", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(current)!;
        return (ContentCanonicalJsonObjectSchema<T>)schema;
    }

    private static string[] GetSchemaPropertyNames<T>(ContentCanonicalJsonObjectSchema<T> schema)
    {
        object properties = typeof(ContentCanonicalJsonObjectSchema<T>).GetField(
            "_properties", BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(schema)!;
        var result = new List<string>();
        foreach (object property in (System.Collections.IEnumerable)properties)
        {
            result.Add((string)typeof(ContentCanonicalJsonProperty<T>).GetProperty(
                "JsonName", BindingFlags.Instance | BindingFlags.NonPublic
            )!.GetValue(property)!);
        }
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    private static string Effect(string kind, string payload) =>
        $"{{\"effect_type\":\"{kind}\",\"payload\":{payload}}}";

    private static string GradedPayload() =>
        "{\"critical_failure_damage_dice_count\":1,\"critical_failure_damage_dice_sides\":6," +
        "\"critical_failure_execute_threshold_max_hp_percent\":10,\"critical_failure_frightened_duration_tu\":1," +
        "\"critical_failure_stunned_duration_tu\":1,\"failure_damage_dice_count\":1," +
        "\"failure_damage_dice_sides\":6,\"failure_execute_threshold_fixed\":1," +
        "\"failure_execute_threshold_max_hp_percent\":5,\"failure_frightened_duration_tu\":1," +
        "\"failure_reaction_lock_duration_tu\":1,\"profile_id\":\"execute_profile\"," +
        "\"success_aftershock_duration_tu\":1}";

    private static ContentImportStageResult<SkillImportModel> Parse(string id, string json) =>
        SkillJsonImportParser.Parse(
            new JsonContentEntryContext("skills", id, $"canonical#{id}", "/entries/0"),
            json
        );

    private static string Format(ContentImportStageResult<SkillImportModel> result) =>
        string.Join("; ", result.Diagnostics.Select(static x => $"{x.RuleId}@{x.JsonPointer}"));

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try { action(); return false; }
        catch (TException) { return true; }
    }
}

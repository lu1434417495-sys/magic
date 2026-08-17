#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public partial class run_skill_json_import_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMinimalNonCombatSkillUsesCanonicalDefaults();
            TestPilotCombatSkillNormalizesToTypedModel();
            TestUnknownMemberIsRejectedAtExactPointer();
            TestNestedUnknownMemberIsRejectedAtExactPointer();
            TestRequiredMembersAreRejectedAtExactPointers();
            TestNumericRangesAreRejectedAtExactPointer();
            TestLevelOverrideContractCoversAllFieldsAndClosedValues();
            TestLevelOverrideKeyPresenceAndEmptyRules();
            TestLevelDescriptionConfigContract();
            TestSnakeCaseIdsAreRejectedAtExactPointers();
            TestUnknownBusinessStringIsRejectedAtExactPointer();
            TestEffectKindIsClosedAndFailurePublishesNoModel();
            TestImportModelsDefensivelyCopyCollections();
            TestImportModelsExposeOnlyPlainTypedClrState();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected skill JSON import contract regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Skill JSON import contract regression"));
    }

    private void TestMinimalNonCombatSkillUsesCanonicalDefaults()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "minimal",
            "{\"skill_id\":\"mage_inner_focus\",\"display_name\":\"Inner Focus\"}"
        );

        _test.True(
            result.HasValue,
            $"minimal non-combat skill should parse without a profile | {FormatDiagnostics(result)}"
        );
        _test.Eq(
            result.Diagnostics.Count,
            0,
            $"minimal non-combat skill should be diagnostic-free | {FormatDiagnostics(result)}"
        );
        if (!result.HasValue)
            return;

        _test.Eq(result.Value.SkillId.Value, "mage_inner_focus", "skill ID should normalize");
        _test.Eq(result.Value.Description, "", "omitted description should use Resource default");
        _test.Eq(result.Value.SkillType, SkillImportType.Active, "skill type default should be typed");
        _test.Eq(result.Value.MaxLevel, 1, "max_level default should match SkillDef");
        _test.Eq(result.Value.LearnSource, SkillImportLearnSource.Book, "learn source default should be typed");
        _test.Eq(result.Value.Tags.Count, 0, "omitted tags should normalize to an empty list");
        _test.Eq(
            result.Value.LevelDescriptionTemplate,
            "",
            "omitted level description template should use canonical empty text"
        );
        _test.Eq(
            result.Value.LevelDescriptionConfigs.Count,
            0,
            "omitted level description configs should use a canonical empty map"
        );
        _test.True(result.Value.CombatProfile == null, "combat_profile must remain optional");
    }

    private void TestPilotCombatSkillNormalizesToTypedModel()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "pilot",
            "{"
                + "\"skill_id\":\"mage_prismatic_red_ward\","
                + "\"display_name\":\"Prismatic Red Ward\","
                + "\"max_level\":5,"
                + "\"tags\":[\"mage\",\"magic_defense\"],"
                + "\"level_description_template\":\"Power {power}\","
                + "\"level_description_configs\":{\"3\":{\"z\":\"last\",\"a\":\"first\"},"
                + "\"1\":{\"power\":\"4\"}},"
                + "\"combat_profile\":{"
                + "\"skill_id\":\"mage_prismatic_red_ward\","
                + "\"target_team_filter\":\"self\",\"range_value\":0,"
                + "\"area_pattern\":\"self\",\"ap_cost\":2,\"mp_cost\":60,"
                + "\"cooldown_tu\":20,"
                + "\"effect_defs\":[{\"effect_type\":\"layered_barrier\","
                + "\"payload\":{\"area_pattern\":\"diamond\","
                + "\"profile_id\":\"prismatic_red_ward\","
                + "\"radius_cells\":1,\"save_dc\":16}}],"
                + "\"level_overrides\":{\"3\":{\"ap_cost\":0,\"mp_cost\":20,"
                + "\"stamina_cost\":6,\"mp_cost_per_target_slot\":1,"
                + "\"stamina_cost_per_target_slot\":2,\"aura_cost\":7,"
                + "\"cooldown_tu\":15,\"casting_time_tu\":10,"
                + "\"casting_maintenance_dc\":11,\"casting_spell_control_dc\":12,"
                + "\"pending_cast_binding_mode\":\"ground_bind\",\"attack_roll_bonus\":-2,"
                + "\"attack_resolution_mode\":\"force_hit_no_crit\","
                + "\"attack_defense_mode\":\"flat_footed\",\"area_value\":3,"
                + "\"range_value\":4,\"area_pattern\":\"front_arc\","
                + "\"max_target_count\":5,\"random_chain_attack_count\":6}}"
                + "}}"
        );

        _test.True(
            result.HasValue,
            $"pilot layered barrier skill should parse | {FormatDiagnostics(result)}"
        );
        _test.Eq(
            result.Diagnostics.Count,
            0,
            $"pilot skill should be diagnostic-free | {FormatDiagnostics(result)}"
        );
        if (!result.HasValue || result.Value.CombatProfile == null)
            return;

        CombatSkillImportModel combat = result.Value.CombatProfile;
        _test.Eq(
            result.Value.LevelDescriptionTemplate,
            "Power {power}",
            "level description template should remain canonical text"
        );
        _test.True(
            result.Value.LevelDescriptionConfigs.Keys.SequenceEqual(new[] { 1, 3 }),
            "level description config levels should be stable-sorted"
        );
        _test.True(
            result.Value.LevelDescriptionConfigs[3].Keys.SequenceEqual(new[] { "a", "z" }),
            "description variables should be Ordinal-sorted"
        );
        _test.Eq(combat.TargetMode, CombatSkillImportTargetMode.Unit, "target mode default should normalize");
        _test.Eq(combat.TargetTeamFilter, CombatSkillImportTargetTeamFilter.Self, "team filter should normalize");
        _test.Eq(combat.RangePattern, CombatSkillImportRangePattern.Single, "range pattern default should normalize");
        _test.Eq(combat.EffectDefs.Count, 1, "one effect should normalize");
        if (combat.EffectDefs.Count == 1)
        {
            CombatEffectImportModel effect = combat.EffectDefs[0];
            _test.Eq(effect.Kind, CombatEffectImportKind.LayeredBarrier, "effect kind should be typed");
            _test.Eq(effect.MinSkillLevel, 0, "omitted min level should use Resource default");
            _test.Eq(effect.MaxSkillLevel, -1, "omitted max level should use Resource default");
            _test.Eq(effect.Power, 0, "omitted power should use Resource default");
        }
        _test.True(combat.LevelOverrides.ContainsKey(3), "level override key should normalize to int");
        if (
            combat.LevelOverrides.TryGetValue(
                3,
                out CombatSkillLevelOverrideImportModel? levelOverride
            )
        )
        {
            _test.Eq(levelOverride.ApCost, 0, "explicit zero must retain field presence");
            _test.Eq(levelOverride.MpCost, 20, "mp override should remain typed");
            _test.Eq(levelOverride.StaminaCost, 6, "stamina override should remain typed");
            _test.Eq(levelOverride.MpCostPerTargetSlot, 1, "MP slot override should remain typed");
            _test.Eq(levelOverride.StaminaCostPerTargetSlot, 2, "stamina slot override should remain typed");
            _test.Eq(levelOverride.AuraCost, 7, "aura override should remain typed");
            _test.Eq(levelOverride.CooldownTu, 15, "cooldown override should remain typed");
            _test.Eq(levelOverride.CastingTimeTu, 10, "casting time override should remain typed");
            _test.Eq(levelOverride.CastingMaintenanceDc, 11, "maintenance DC should remain typed");
            _test.Eq(levelOverride.CastingSpellControlDc, 12, "spell control DC should remain typed");
            _test.Eq(levelOverride.PendingCastBindingMode, PendingCastBindingModeKind.GroundBind, "binding mode should be typed");
            _test.Eq(levelOverride.AttackRollBonus, -2, "signed attack bonus should remain typed");
            _test.Eq(levelOverride.AttackResolutionMode, CombatSkillLevelOverrideAttackResolutionMode.ForceHitNoCrit, "attack resolution should be typed");
            _test.Eq(levelOverride.AttackDefenseMode, CombatSkillLevelOverrideAttackDefenseMode.FlatFooted, "attack defense should be typed");
            _test.Eq(levelOverride.AreaValue, 3, "area value should remain typed");
            _test.Eq(levelOverride.RangeValue, 4, "range value should remain typed");
            _test.Eq(levelOverride.AreaPattern, CombatSkillLevelOverrideAreaPattern.FrontArc, "area pattern should be typed");
            _test.Eq(levelOverride.MaxTargetCount, 5, "target count should remain typed");
            _test.Eq(levelOverride.RandomChainAttackCount, 6, "chain count should remain typed");
        }
    }

    private void TestUnknownMemberIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "unknown",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\",\"unexpected\":true}"
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.InvalidDto,
            "/entries/7/unexpected",
            "unknown DTO member"
        );
    }

    private void TestRequiredMembersAreRejectedAtExactPointers()
    {
        ContentImportStageResult<SkillImportModel> missingRoot = Parse(
            "missing_root",
            "{\"display_name\":\"Focus\"}"
        );
        AssertSingleFailure(
            missingRoot,
            SkillJsonImportRules.RequiredMember,
            "/entries/7/skill_id",
            "missing root skill_id"
        );

        ContentImportStageResult<SkillImportModel> missingEffectKind = Parse(
            "missing_effect_kind",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"combat_profile\":{\"skill_id\":\"mage_focus\",\"effect_defs\":[{}]}}"
        );
        AssertSingleFailure(
            missingEffectKind,
            SkillJsonImportRules.RequiredMember,
            "/entries/7/combat_profile/effect_defs/0/effect_type",
            "missing effect discriminator"
        );

        ContentImportStageResult<SkillImportModel> missingCombatSkillId = Parse(
            "missing_combat_skill_id",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"combat_profile\":{}}"
        );
        AssertSingleFailure(
            missingCombatSkillId,
            SkillJsonImportRules.RequiredMember,
            "/entries/7/combat_profile/skill_id",
            "missing combat profile skill_id"
        );

        ContentImportStageResult<SkillImportModel> nullRequired = Parse(
            "null_required",
            "{\"skill_id\":null,\"display_name\":\"Focus\"}"
        );
        AssertSingleFailure(
            nullRequired,
            SkillJsonImportRules.RequiredMember,
            "/entries/7/skill_id",
            "null required skill_id"
        );
    }

    private void TestNestedUnknownMemberIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "nested_unknown",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                + "\"effect_defs\":[{\"effect_type\":\"layered_barrier\","
                + "\"payload\":{\"area_pattern\":\"diamond\","
                + "\"profile_id\":\"prismatic_red_ward\","
                + "\"radius_cells\":1,\"save_dc\":16},"
                + "\"unexpected\":true}]}}"
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.InvalidDto,
            "/entries/7/combat_profile/effect_defs/0/unexpected",
            "nested unknown effect member"
        );
    }

    private void TestNumericRangesAreRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "range",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"combat_profile\":{\"skill_id\":\"mage_focus\",\"mp_cost\":-1}}"
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.NumberOutOfRange,
            "/entries/7/combat_profile/mp_cost",
            "negative combat cost"
        );
    }

    private void TestLevelOverrideContractCoversAllFieldsAndClosedValues()
    {
        (string Field, string Value, string RuleId)[] unknownValues =
        {
            (
                "pending_cast_binding_mode",
                "future_bind",
                SkillJsonImportRules.UnknownPendingCastBindingMode
            ),
            (
                "attack_resolution_mode",
                "future_attack",
                SkillJsonImportRules.UnknownAttackResolutionMode
            ),
            (
                "attack_defense_mode",
                "future_defense",
                SkillJsonImportRules.UnknownAttackDefenseMode
            ),
            (
                "area_pattern",
                "spiral",
                SkillJsonImportRules.UnknownLevelOverrideAreaPattern
            ),
        };
        foreach ((string field, string value, string ruleId) in unknownValues)
        {
            ContentImportStageResult<SkillImportModel> result = Parse(
                $"unknown_{field}",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + $"\"level_overrides\":{{\"0\":{{\"{field}\":\"{value}\"}}}}}}}}"
            );
            AssertSingleFailure(
                result,
                ruleId,
                $"/entries/7/combat_profile/level_overrides/0/{field}",
                $"unknown level override {field}"
            );
        }

        string[] areaPatterns =
        {
            "single", "self", "diamond", "square", "radius", "cross", "line", "cone",
            "narrow_cone", "front_arc",
        };
        for (int index = 0; index < areaPatterns.Length; index += 1)
        {
            ContentImportStageResult<SkillImportModel> result = Parse(
                $"area_{areaPatterns[index]}",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + $"\"level_overrides\":{{\"0\":{{\"area_pattern\":\"{areaPatterns[index]}\"}}}}}}}}"
            );
            _test.True(
                result.HasValue,
                $"registered area pattern {areaPatterns[index]} should parse | {FormatDiagnostics(result)}"
            );
        }
    }

    private void TestLevelOverrideKeyPresenceAndEmptyRules()
    {
        AssertSingleFailure(
            Parse(
                "noncanonical_override_level",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"level_overrides\":{\"01\":{\"ap_cost\":1}}}}"
            ),
            SkillJsonImportRules.NumberOutOfRange,
            "/entries/7/combat_profile/level_overrides/01",
            "non-canonical override level"
        );
        AssertSingleFailure(
            Parse(
                "empty_override",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"level_overrides\":{\"0\":{}}}}"
            ),
            SkillJsonImportRules.EmptyLevelOverride,
            "/entries/7/combat_profile/level_overrides/0",
            "empty override"
        );
        AssertSingleFailure(
            Parse(
                "null_override_field",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"level_overrides\":{\"0\":{\"ap_cost\":null}}}}"
            ),
            SkillJsonImportRules.RequiredMember,
            "/entries/7/combat_profile/level_overrides/0/ap_cost",
            "null override field"
        );
        AssertSingleFailure(
            Parse(
                "duplicate_override_level",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"level_overrides\":{\"0\":{\"ap_cost\":1},\"0\":{\"mp_cost\":2}}}}"
            ),
            SkillJsonImportRules.DuplicateLevelKey,
            "/entries/7/combat_profile/level_overrides/0",
            "duplicate override level"
        );
        AssertSingleFailure(
            Parse(
                "invalid_positive_override",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"level_overrides\":{\"0\":{\"max_target_count\":0}}}}"
            ),
            SkillJsonImportRules.NumberOutOfRange,
            "/entries/7/combat_profile/level_overrides/0/max_target_count",
            "non-positive target count override"
        );
    }

    private void TestLevelDescriptionConfigContract()
    {
        (string EntryId, string Json, string RuleId, string Pointer)[] invalidCases =
        {
            (
                "description_level_type",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":\"bad\"}}",
                SkillJsonImportRules.InvalidDto,
                "/entries/7/level_description_configs/0"
            ),
            (
                "description_variable_type",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":{\"power\":4}}}",
                SkillJsonImportRules.InvalidDto,
                "/entries/7/level_description_configs/0/power"
            ),
            (
                "description_noncanonical_level",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"01\":{}}}",
                SkillJsonImportRules.NumberOutOfRange,
                "/entries/7/level_description_configs/01"
            ),
            (
                "description_level_out_of_range",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"max_level\":1,\"level_description_configs\":{\"2\":{}}}",
                SkillJsonImportRules.NumberOutOfRange,
                "/entries/7/level_description_configs/2"
            ),
            (
                "description_null_level",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":null}}",
                SkillJsonImportRules.RequiredMember,
                "/entries/7/level_description_configs/0"
            ),
            (
                "description_null_variable",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":{\"power\":null}}}",
                SkillJsonImportRules.RequiredMember,
                "/entries/7/level_description_configs/0/power"
            ),
            (
                "description_duplicate_level",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":{},\"0\":{}}}",
                SkillJsonImportRules.DuplicateLevelKey,
                "/entries/7/level_description_configs/0"
            ),
            (
                "description_duplicate_variable",
                "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"level_description_configs\":{\"0\":{\"power\":\"4\",\"power\":\"5\"}}}",
                SkillJsonImportRules.DuplicateDescriptionVariableKey,
                "/entries/7/level_description_configs/0/power"
            ),
        };
        foreach ((string entryId, string json, string ruleId, string pointer) in invalidCases)
            AssertSingleFailure(Parse(entryId, json), ruleId, pointer, entryId);
    }

    private void TestSnakeCaseIdsAreRejectedAtExactPointers()
    {
        ContentImportStageResult<SkillImportModel> rootResult = Parse(
            "bad_root_id",
            "{\"skill_id\":\"Mage-Focus\",\"display_name\":\"Focus\"}"
        );
        AssertSingleFailure(
            rootResult,
            SkillJsonImportRules.InvalidId,
            "/entries/7/skill_id",
            "non-snake-case root ID"
        );

        ContentImportStageResult<SkillImportModel> tagResult = Parse(
            "bad_tag_id",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"tags\":[\"magic/defense\"]}"
        );
        AssertSingleFailure(
            tagResult,
            SkillJsonImportRules.InvalidId,
            "/entries/7/tags/0",
            "non-snake-case tag ID"
        );
    }

    private void TestEffectKindIsClosedAndFailurePublishesNoModel()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "unknown_effect",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                + "\"effect_defs\":[{\"effect_type\":\"future_effect\","
                + "\"payload\":{}}]}}"
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.UnknownEffectKind,
            "/entries/7/combat_profile/effect_defs/0/effect_type",
            "unknown effect kind"
        );
        _test.False(result.HasValue, "closed-kind failure must not expose a partial model");
    }

    private void TestUnknownBusinessStringIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "unknown_skill_type",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"skill_type\":\"spell\"}"
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.UnknownSkillType,
            "/entries/7/skill_type",
            "unknown skill type"
        );
    }

    private void TestImportModelsDefensivelyCopyCollections()
    {
        SkillImportIdentifier.TryCreate("mage_copy", out SkillImportIdentifier skillId);
        SkillImportIdentifier.TryCreate("mage", out SkillImportIdentifier tag);
        SkillImportIdentifier.TryCreate(
            "prismatic_red_ward",
            out SkillImportIdentifier profileId
        );
        var effects = new List<CombatEffectImportModel>
        {
            new(
                CombatEffectImportKind.LayeredBarrier,
                0,
                -1,
                0,
                0,
                new LayeredBarrierEffectPayloadImportModel(
                    CombatSkillImportAreaPattern.Diamond,
                    profileId,
                    1,
                    16
                )
            ),
        };
        var overrides = new List<KeyValuePair<int, CombatSkillLevelOverrideImportModel>>
        {
            new(2, new CombatSkillLevelOverrideImportModel(cooldownTu: 10)),
        };
        var combat = new CombatSkillImportModel(
            skillId,
            CombatSkillImportTargetMode.Unit,
            CombatSkillImportTargetTeamFilter.Self,
            CombatSkillImportRangePattern.Single,
            0,
            CombatSkillImportAreaPattern.Self,
            2,
            10,
            20,
            effects,
            overrides
        );
        var tags = new List<SkillImportIdentifier> { tag };
        var descriptionValues = new Dictionary<string, string>
        {
            ["z"] = "last",
            ["a"] = "first",
        };
        var descriptionConfigs = new List<KeyValuePair<int, SkillDescriptionVariables>>
        {
            new(2, new SkillDescriptionVariables(descriptionValues)),
        };
        var skill = new SkillImportModel(
            skillId,
            "Copy",
            "",
            SkillImportType.Active,
            5,
            SkillImportLearnSource.Book,
            tags,
            "Value {a}",
            descriptionConfigs,
            combat
        );

        effects.Clear();
        overrides.Clear();
        tags.Clear();
        descriptionValues["a"] = "changed";
        descriptionConfigs.Clear();

        _test.Eq(combat.EffectDefs.Count, 1, "combat model must copy effect collection input");
        _test.Eq(combat.LevelOverrides.Count, 1, "combat model must copy override collection input");
        _test.Eq(skill.Tags.Count, 1, "skill model must copy tag collection input");
        _test.Eq(
            skill.LevelDescriptionConfigs.Count,
            1,
            "skill model must copy description level collection input"
        );
        _test.Eq(
            skill.LevelDescriptionConfigs[2]["a"],
            "first",
            "description variables must defensively copy caller-owned values"
        );
        _test.True(
            skill.LevelDescriptionConfigs[2].Keys.SequenceEqual(new[] { "a", "z" }),
            "description variable enumeration must be stable-sorted"
        );

        bool duplicateDescriptionVariableRejected = false;
        try
        {
            _ = new SkillDescriptionVariables(
                new[]
                {
                    new KeyValuePair<string, string>("power", "4"),
                    new KeyValuePair<string, string>("power", "5"),
                }
            );
        }
        catch (ArgumentException)
        {
            duplicateDescriptionVariableRejected = true;
        }
        _test.True(
            duplicateDescriptionVariableRejected,
            "SkillDescriptionVariables must reject duplicate keys instead of applying last-write-wins"
        );
    }

    private void TestImportModelsExposeOnlyPlainTypedClrState()
    {
        Type[] modelTypes =
        {
            typeof(SkillImportModel),
            typeof(CombatSkillImportModel),
            typeof(CombatEffectImportModel),
            typeof(LayeredBarrierEffectPayloadImportModel),
            typeof(CombatSkillLevelOverrideImportModel),
            typeof(SkillDescriptionVariables),
        };
        foreach (Type modelType in modelTypes)
        {
            foreach (
                PropertyInfo property in modelType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                _test.False(
                    ContainsForbiddenType(property.PropertyType),
                    $"{modelType.Name}.{property.Name} must remain plain typed CLR state"
                );
            }
        }
    }

    private static bool ContainsForbiddenType(Type type)
    {
        if (
            type == typeof(object)
            || type.FullName == "System.Text.Json.JsonElement"
            || type.FullName == "System.Text.Json.Nodes.JsonNode"
            || (type.Namespace?.StartsWith("Godot", StringComparison.Ordinal) ?? false)
        )
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsForbiddenType);
    }

    private void AssertSingleFailure(
        ContentImportStageResult<SkillImportModel> result,
        string ruleId,
        string pointer,
        string label
    )
    {
        _test.False(result.HasValue, $"{label} should not publish a model");
        _test.Eq(
            result.Diagnostics.Count,
            1,
            $"{label} should emit one diagnostic | {FormatDiagnostics(result)}"
        );
        if (result.Diagnostics.Count != 1)
            return;

        _test.Eq(result.Diagnostics[0].RuleId, ruleId, $"{label} rule ID should be stable");
        _test.Eq(result.Diagnostics[0].JsonPointer, pointer, $"{label} pointer should be exact");
        _test.True(
            result.Diagnostics[0].SourceLabel.StartsWith("skill.json#", StringComparison.Ordinal),
            $"{label} source label should retain entry provenance"
        );
    }

    private static ContentImportStageResult<SkillImportModel> Parse(string entryId, string json) =>
        SkillJsonImportParser.Parse(
            new JsonContentEntryContext("skills", entryId, $"skill.json#{entryId}", "/entries/7"),
            json
        );

    private static string FormatDiagnostics(ContentImportStageResult<SkillImportModel> result) =>
        string.Join(
            "; ",
            result.Diagnostics.Select(diagnostic => $"{diagnostic.RuleId}@{diagnostic.JsonPointer}")
        );
}

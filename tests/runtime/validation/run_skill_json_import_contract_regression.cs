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
            TestExpandedRootCombatContractsAreTypedAndClosed();
            TestExpandedDefaultsRejectExplicitNullAtExactPointers();
            TestCastVariantPayloadOmissionAndSquare2Contract();
            TestEffectKindIsClosedAndFailurePublishesNoModel();
            TestNestedEffectRawContractsAtExactPointers();
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
        _test.Eq(
            result.Value.IconId.Value,
            "",
            "omitted icon_id should retain the Resource empty sentinel"
        );
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

        ContentImportStageResult<SkillImportModel> nullOptionalCarrier = Parse(
            "null_max_level",
            "{\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                + "\"max_level\":null}"
        );
        AssertSingleFailure(
            nullOptionalCarrier,
            SkillJsonImportRules.RequiredMember,
            "/entries/7/max_level",
            "explicit null optional numeric carrier"
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

    private void TestNestedEffectRawContractsAtExactPointers()
    {
        const string layeredPayload =
            "\"payload\":{\"area_pattern\":\"diamond\",\"profile_id\":\"ward\","
            + "\"radius_cells\":1,\"save_dc\":12}";
        (string Label, string EffectArrayJson, string Pointer, string RuleId)[] cases =
        {
            (
                "passive explicit-null power",
                "\"passive_effect_defs\":[{\"effect_type\":\"layered_barrier\",\"power\":null," + layeredPayload + "}]",
                "/entries/7/combat_profile/passive_effect_defs/0/power",
                SkillJsonImportRules.RequiredMember
            ),
            (
                "passive missing effect type",
                "\"passive_effect_defs\":[{" + layeredPayload + "}]",
                "/entries/7/combat_profile/passive_effect_defs/0/effect_type",
                SkillJsonImportRules.RequiredMember
            ),
            (
                "passive missing payload",
                "\"passive_effect_defs\":[{\"effect_type\":\"layered_barrier\"}]",
                "/entries/7/combat_profile/passive_effect_defs/0/payload",
                SkillJsonImportRules.MissingEffectPayload
            ),
            (
                "cast explicit-null power",
                "\"cast_variants\":[{\"variant_id\":\"probe\",\"effect_defs\":[{\"effect_type\":\"layered_barrier\",\"power\":null," + layeredPayload + "}]}]",
                "/entries/7/combat_profile/cast_variants/0/effect_defs/0/power",
                SkillJsonImportRules.RequiredMember
            ),
            (
                "cast missing effect type",
                "\"cast_variants\":[{\"variant_id\":\"probe\",\"effect_defs\":[{" + layeredPayload + "}]}]",
                "/entries/7/combat_profile/cast_variants/0/effect_defs/0/effect_type",
                SkillJsonImportRules.RequiredMember
            ),
            (
                "cast missing payload",
                "\"cast_variants\":[{\"variant_id\":\"probe\",\"effect_defs\":[{\"effect_type\":\"layered_barrier\"}]}]",
                "/entries/7/combat_profile/cast_variants/0/effect_defs/0/payload",
                SkillJsonImportRules.MissingEffectPayload
            ),
        };

        foreach ((string label, string effectArrayJson, string pointer, string ruleId) in cases)
        {
            AssertSingleFailure(
                Parse(
                    $"nested_effect_{label.Replace('-', '_').Replace(' ', '_')}",
                    "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\","
                        + "\"combat_profile\":{\"skill_id\":\"mage_probe\"," + effectArrayJson + "}}"
                ),
                ruleId,
                pointer,
                label
            );
        }
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

    private void TestExpandedRootCombatContractsAreTypedAndClosed()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "expanded_contract",
            "{"
                + "\"skill_id\":\"mage_contract_probe\",\"display_name\":\"Probe\","
                + "\"unlock_mode\":\"composite_upgrade\","
                + "\"dynamic_max_level_stat_id\":\"profession_rank:mage\","
                + "\"core_skill_transition_mode\":\"replace_sources_with_result\","
                + "\"growth_tier\":\"advanced\",\"practice_tier\":\"intermediate\","
                + "\"attribute_modifiers\":[{\"attribute_id\":\"strength\",\"mode\":\"percent\"}],"
                + "\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{"
                + "\"enabled\":true,\"limit\":3,\"ratio\":1.5,"
                + "\"mode\":\"arc\",\"tags\":[\"fire\",\"cold\"]}},"
                + "\"combat_profile\":{\"skill_id\":\"mage_contract_probe\","
                + "\"weapon_range_policy\":\"configured\","
                + "\"mastery_trigger_mode\":\"damage_dealt\","
                + "\"mastery_amount_mode\":\"per_cast_hp_ratio\","
                + "\"projectile_kind\":\"magical\","
                + "\"target_selection_mode\":\"multi_unit\","
                + "\"unit_target_resolution_mode\":\"ordered_slots\","
                + "\"selection_order_mode\":\"manual\","
                + "\"ranged_weapon_reaction_profile\":{\"damage_tag\":\"force\"}}}"
        );
        _test.True(result.HasValue, $"expanded A/B contract should parse | {FormatDiagnostics(result)}");
        if (!result.HasValue || result.Value.CombatProfile == null)
            return;

        SkillImportModel root = result.Value;
        _test.Eq(root.UnlockMode, SkillImportUnlockMode.CompositeUpgrade, "unlock mode should be typed");
        _test.Eq(root.DynamicMaxLevelStatId.Value, "profession_rank:mage", "namespaced stat ID should remain typed and lossless");
        _test.Eq(root.CoreSkillTransitionMode, SkillImportCoreSkillTransitionMode.ReplaceSourcesWithResult, "core transition should be typed");
        _test.Eq(root.GrowthTier, SkillImportProgressionTier.Advanced, "growth tier should be typed");
        _test.Eq(root.PracticeTier, SkillImportProgressionTier.Intermediate, "practice tier should be typed");
        _test.Eq(root.AttributeModifiers[0].Mode, AttributeModifierImportMode.Percent, "modifier mode should be typed");
        IReadOnlyDictionary<SkillImportIdentifier, ContingencyParameterBindingImportValue> bindings =
            root.ContingencyAutomationProfile!.AllowedParameterBindings;
        _test.Eq(bindings.Count, 5, "parameter binding dictionary must retain every key/value");
        AssertBindingType<ContingencyBoolBindingImportValue>(bindings, "enabled");
        AssertBindingType<ContingencyIntBindingImportValue>(bindings, "limit");
        AssertBindingType<ContingencyFloatBindingImportValue>(bindings, "ratio");
        AssertBindingType<ContingencyStringBindingImportValue>(bindings, "mode");
        AssertBindingType<ContingencyStringListBindingImportValue>(bindings, "tags");

        CombatSkillImportModel combat = result.Value.CombatProfile;
        _test.Eq(combat.WeaponRangePolicy, CombatWeaponRangePolicyImportKind.Configured, "weapon range policy should be typed");
        _test.Eq(combat.MasteryTriggerMode, CombatMasteryTriggerImportKind.DamageDealt, "mastery trigger should be typed");
        _test.Eq(combat.MasteryAmountMode, CombatMasteryAmountImportKind.PerCastHpRatio, "mastery amount should be typed");
        _test.Eq(combat.ProjectileKind, CombatBaseProjectileImportKind.Magical, "projectile kind should be typed");
        _test.Eq(combat.TargetSelectionMode, CombatTargetSelectionImportKind.MultiUnit, "target selection should be typed");
        _test.Eq(combat.UnitTargetResolutionMode, CombatUnitTargetResolutionImportKind.OrderedSlots, "slot resolution should be typed");
        _test.Eq(combat.SelectionOrderMode, CombatSelectionOrderImportKind.Manual, "selection order should be typed");
        _test.Eq(combat.RangedWeaponReactionProfile?.DamageTag, DamageTagImportKind.Force, "reaction damage tag should be typed");

        AssertSingleFailure(
            Parse("bad_unlock", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"unlock_mode\":\"future\"}"),
            "skill.dto.unlock_mode.unknown",
            "/entries/7/unlock_mode",
            "unknown root business string"
        );
        AssertSingleFailure(
            Parse("bad_projectile", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"projectile_kind\":\"future\"}}"),
            "skill.dto.projectile_kind.unknown",
            "/entries/7/combat_profile/projectile_kind",
            "unknown combat business string"
        );
        AssertSingleFailure(
            Parse("empty_base_projectile", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"projectile_kind\":\"\"}}"),
            "skill.dto.projectile_kind.unknown",
            "/entries/7/combat_profile/projectile_kind",
            "base projectile inherit sentinel"
        );
        AssertSingleFailure(
            Parse("movement_target", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"target_selection_mode\":\"movement\"}}"),
            "skill.dto.target_selection_mode.unknown",
            "/entries/7/combat_profile/target_selection_mode",
            "unsupported movement target selection"
        );
        AssertSingleFailure(
            Parse("bad_reaction_damage", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"ranged_weapon_reaction_profile\":{\"damage_tag\":\"future\"}}}"),
            "skill.dto.ranged_weapon_reaction.damage_tag.unknown",
            "/entries/7/combat_profile/ranged_weapon_reaction_profile/damage_tag",
            "unknown reaction damage tag"
        );
        AssertSingleFailure(
            Parse("bad_binding", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{\"mode\":{}}}}"),
            "skill.dto.contingency.parameter_binding_value.invalid",
            "/entries/7/contingency_automation_profile/allowed_parameter_bindings/mode",
            "unsupported parameter binding value"
        );
        AssertSingleFailure(
            Parse("non_finite_binding", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{\"ratio\":1e400}}}"),
            "skill.dto.contingency.parameter_binding_value.invalid",
            "/entries/7/contingency_automation_profile/allowed_parameter_bindings/ratio",
            "non-finite parameter binding number"
        );
        AssertSingleFailure(
            Parse("bad_binding_key", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"contingency_automation_profile\":{\"allowed_parameter_bindings\":{\"bad/key\":true}}}"),
            SkillJsonImportRules.InvalidId,
            "/entries/7/contingency_automation_profile/allowed_parameter_bindings/bad~1key",
            "invalid escaped parameter binding key"
        );

        ContentImportStageResult<SkillImportModel> assetId = Parse(
            "asset_id",
            "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\","
                + "\"icon_id\":\"550e8400-e29b-41d4-a716-446655440000.asset\"}"
        );
        _test.True(assetId.HasValue, $"UUID/dot asset ID should parse | {FormatDiagnostics(assetId)}");
        if (assetId.HasValue)
        {
            _test.Eq(
                assetId.Value.IconId.Value,
                "550e8400-e29b-41d4-a716-446655440000.asset",
                "asset ID must retain its exact stable token"
            );
        }
        AssertSingleFailure(
            Parse("asset_path", "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"icon_id\":\"res://icons/probe.png\"}"),
            "skill.dto.asset_id.invalid",
            "/entries/7/icon_id",
            "path-like asset ID"
        );

        (string Label, string Json, string RuleId, string Pointer)[] explicitEmptyCases =
        {
            (
                "growth tier",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"growth_tier\":\"\"}",
                "skill.dto.growth_tier.unknown",
                "/entries/7/growth_tier"
            ),
            (
                "weapon range",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"weapon_range_policy\":\"\"}}",
                "skill.dto.weapon_range_policy.unknown",
                "/entries/7/combat_profile/weapon_range_policy"
            ),
            (
                "attack resolution",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"attack_resolution_mode\":\"\"}}",
                "skill.dto.attack_resolution_mode.unknown",
                "/entries/7/combat_profile/attack_resolution_mode"
            ),
            (
                "spell fate",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"spell_fate_mode\":\"\"}}",
                "skill.dto.spell_fate_mode.unknown",
                "/entries/7/combat_profile/spell_fate_mode"
            ),
            (
                "spell critical",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"spell_critical_mode\":\"\"}}",
                "skill.dto.spell_critical_mode.unknown",
                "/entries/7/combat_profile/spell_critical_mode"
            ),
            (
                "backlash",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"backlash_mode\":\"\"}}",
                "skill.dto.backlash_mode.unknown",
                "/entries/7/combat_profile/backlash_mode"
            ),
            (
                "backlash target filter",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"backlash_target_filter\":\"\"}}",
                "skill.dto.backlash_target_filter.unknown",
                "/entries/7/combat_profile/backlash_target_filter"
            ),
            (
                "projectile override",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"cast_variants\":[{\"variant_id\":\"probe\",\"projectile_kind_override\":\"\"}]}}",
                "skill.dto.cast_variant.projectile_kind_override.unknown",
                "/entries/7/combat_profile/cast_variants/0/projectile_kind_override"
            ),
        };
        foreach ((string label, string json, string ruleId, string pointer) in explicitEmptyCases)
            AssertSingleFailure(Parse($"explicit_empty_{label.Replace(' ', '_')}", json), ruleId, pointer, $"explicit empty {label}");
    }

    private void TestExpandedDefaultsRejectExplicitNullAtExactPointers()
    {
        (string Label, string Json, string Pointer)[] cases =
        {
            (
                "root collection",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"mastery_curve\":null}",
                "/entries/7/mastery_curve"
            ),
            (
                "attribute modifier",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"attribute_modifiers\":[{\"mode\":null}]}",
                "/entries/7/attribute_modifiers/0/mode"
            ),
            (
                "contingency profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"contingency_automation_profile\":{\"allowed_parameter_bindings\":null}}",
                "/entries/7/contingency_automation_profile/allowed_parameter_bindings"
            ),
            (
                "combat default",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"projectile_kind\":null}}",
                "/entries/7/combat_profile/projectile_kind"
            ),
            (
                "windup profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"windup_profile\":{\"skill_level_tier_caps\":null}}}",
                "/entries/7/combat_profile/windup_profile/skill_level_tier_caps"
            ),
            (
                "directional profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"directional_piercing_profile\":{\"base_damage_percent_curve\":null}}}",
                "/entries/7/combat_profile/directional_piercing_profile/base_damage_percent_curve"
            ),
            (
                "line-through profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"line_through_attack_profile\":{\"primary_weapon_dice_multiplier_curve\":null}}}",
                "/entries/7/combat_profile/line_through_attack_profile/primary_weapon_dice_multiplier_curve"
            ),
            (
                "sequential profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"sequential_line_hit_profile\":{\"continuation_range_curve\":null}}}",
                "/entries/7/combat_profile/sequential_line_hit_profile/continuation_range_curve"
            ),
            (
                "spell reaction profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"spell_reaction_profile\":{\"save_tag\":null}}}",
                "/entries/7/combat_profile/spell_reaction_profile/save_tag"
            ),
            (
                "ranged reaction profile",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"ranged_weapon_reaction_profile\":{\"damage_tag\":null}}}",
                "/entries/7/combat_profile/ranged_weapon_reaction_profile/damage_tag"
            ),
            (
                "cast variant",
                "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"cast_variants\":[{\"variant_id\":\"plain\",\"allowed_base_terrains\":null}]}}",
                "/entries/7/combat_profile/cast_variants/0/allowed_base_terrains"
            ),
        };

        foreach ((string label, string json, string pointer) in cases)
        {
            AssertSingleFailure(
                Parse($"explicit_null_{label.Replace('-', '_').Replace(' ', '_')}", json),
                SkillJsonImportRules.RequiredMember,
                pointer,
                $"explicit null {label}"
            );
        }

        ContentImportStageResult<SkillImportModel> nullableProfile = Parse(
            "nullable_profile",
            "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"windup_profile\":null}}"
        );
        _test.True(
            nullableProfile.HasValue,
            $"nullable profile carrier should continue to allow explicit null | {FormatDiagnostics(nullableProfile)}"
        );
    }

    private void TestCastVariantPayloadOmissionAndSquare2Contract()
    {
        string prefix = "{\"skill_id\":\"mage_probe\",\"display_name\":\"Probe\",\"combat_profile\":{\"skill_id\":\"mage_probe\",\"cast_variants\":[";
        ContentImportStageResult<SkillImportModel> result = Parse(
            "cast_payloads",
            prefix
                + "{\"variant_id\":\"plain\"},"
                + "{\"variant_id\":\"square\",\"footprint_pattern\":\"square2\","
                + "\"allowed_base_terrains\":[\"land\",\"deep_water\"],"
                + "\"payload\":{\"square2_corner\":\"top_left\"}}]}}"
        );
        _test.True(result.HasValue, $"empty and square2 cast payloads should parse | {FormatDiagnostics(result)}");
        if (result.HasValue && result.Value.CombatProfile != null)
        {
            IReadOnlyList<CombatCastVariantImportModel> variants = result.Value.CombatProfile.CastVariants;
            _test.Eq(variants.Count, 2, "both cast variants should normalize");
            _test.True(variants[0].Payload.Square2Corner == null, "omitted non-square payload should become typed empty payload");
            _test.Eq(variants[1].Payload.Square2Corner, CombatCastSquare2Corner.TopLeft, "square2 corner should remain typed");
            _test.True(
                variants[1].AllowedBaseTerrains.SequenceEqual(
                    new[] { BattleTerrainImportKind.Land, BattleTerrainImportKind.DeepWater }
                ),
                "cast terrain allowlist should be a typed closed collection"
            );
        }

        AssertSingleFailure(
            Parse(
                "missing_square_corner",
                prefix + "{\"variant_id\":\"square\",\"footprint_pattern\":\"square2\"}]}}"
            ),
            "skill.dto.cast_payload.square2_corner.required",
            "/entries/7/combat_profile/cast_variants/0/payload/square2_corner",
            "square2 variant without corner"
        );
        AssertSingleFailure(
            Parse(
                "null_cast_payload",
                prefix + "{\"variant_id\":\"plain\",\"payload\":null}]}}"
            ),
            SkillJsonImportRules.RequiredMember,
            "/entries/7/combat_profile/cast_variants/0/payload",
            "explicit-null cast payload"
        );
        AssertSingleFailure(
            Parse(
                "unknown_cast_payload_member",
                prefix + "{\"variant_id\":\"plain\",\"payload\":{\"future\":true}}]}}"
            ),
            SkillJsonImportRules.InvalidDto,
            "/entries/7/combat_profile/cast_variants/0/payload/future",
            "unknown cast payload member"
        );
        AssertSingleFailure(
            Parse(
                "null_square_corner",
                prefix + "{\"variant_id\":\"square\",\"footprint_pattern\":\"square2\",\"payload\":{\"square2_corner\":null}}]}}"
            ),
            SkillJsonImportRules.RequiredMember,
            "/entries/7/combat_profile/cast_variants/0/payload/square2_corner",
            "explicit-null square2 corner"
        );
        AssertSingleFailure(
            Parse(
                "bad_footprint",
                prefix + "{\"variant_id\":\"plain\",\"footprint_pattern\":\"future\"}]}}"
            ),
            "skill.dto.cast_variant.footprint_pattern.unknown",
            "/entries/7/combat_profile/cast_variants/0/footprint_pattern",
            "unknown cast footprint"
        );
        AssertSingleFailure(
            Parse(
                "bad_cast_terrain",
                prefix + "{\"variant_id\":\"plain\",\"allowed_base_terrains\":[\"lava\"]}]}}"
            ),
            "skill.dto.cast_variant.allowed_base_terrain.unknown",
            "/entries/7/combat_profile/cast_variants/0/allowed_base_terrains/0",
            "unknown cast terrain"
        );
    }

    private void AssertBindingType<T>(
        IReadOnlyDictionary<SkillImportIdentifier, ContingencyParameterBindingImportValue> values,
        string key
    ) where T : ContingencyParameterBindingImportValue
    {
        SkillImportIdentifier.TryCreate(key, out SkillImportIdentifier identifier);
        _test.True(values.TryGetValue(identifier, out ContingencyParameterBindingImportValue? value), $"binding {key} should exist");
        _test.True(value is T, $"binding {key} should normalize as {typeof(T).Name}");
    }

    private static SkillImportModel CreateSkillModel(
        SkillImportIdentifier skillId,
        IEnumerable<SkillImportIdentifier> tags,
        IEnumerable<KeyValuePair<int, SkillDescriptionVariables>> descriptionConfigs,
        CombatSkillImportModel? combatProfile
    ) => new(
        skillId: skillId,
        displayName: "Copy",
        description: "",
        skillType: SkillImportType.Active,
        maxLevel: 5,
        learnSource: SkillImportLearnSource.Book,
        tags: tags,
        levelDescriptionTemplate: "Value {a}",
        levelDescriptionConfigs: descriptionConfigs,
        combatProfile: combatProfile,
        iconId: SkillImportAssetId.FromResource(""),
        nonCoreMaxLevel: 0,
        dynamicMaxLevelStatId: default,
        dynamicMaxLevelBase: 0,
        dynamicMaxLevelPerStat: 0,
        masteryCurve: Array.Empty<int>(),
        learnRequirements: Array.Empty<SkillImportIdentifier>(),
        unlockMode: SkillImportUnlockMode.Standard,
        knowledgeRequirements: Array.Empty<SkillImportIdentifier>(),
        skillLevelRequirements: Array.Empty<KeyValuePair<SkillImportIdentifier, int>>(),
        attributeRequirements: Array.Empty<KeyValuePair<SkillImportIdentifier, int>>(),
        achievementRequirements: Array.Empty<SkillImportIdentifier>(),
        upgradeSourceSkillIds: Array.Empty<SkillImportIdentifier>(),
        retainSourceSkillsOnUnlock: true,
        coreSkillTransitionMode: SkillImportCoreSkillTransitionMode.Inherit,
        masterySources: Array.Empty<SkillImportIdentifier>(),
        growthTier: SkillImportProgressionTier.None,
        attributeGrowthProgress: Array.Empty<KeyValuePair<SkillImportIdentifier, int>>(),
        practiceTier: SkillImportProgressionTier.None,
        attributeModifiers: Array.Empty<AttributeModifierImportModel>(),
        contingencyAutomationProfile: null
    );

    private static CombatSkillImportModel CreateCombatModel(
        SkillImportIdentifier skillId,
        IEnumerable<CombatEffectImportModel> effects,
        IEnumerable<KeyValuePair<int, CombatSkillLevelOverrideImportModel>> overrides,
        IEnumerable<SkillImportStringName>? expandedTokens = null,
        IEnumerable<int>? expandedNumbers = null
    )
    {
        IEnumerable<SkillImportStringName> tokens =
            expandedTokens ?? Array.Empty<SkillImportStringName>();
        IEnumerable<int> numbers = expandedNumbers ?? Array.Empty<int>();
        return new CombatSkillImportModel(
            skillId: skillId,
            targetMode: CombatSkillImportTargetMode.Unit,
            targetTeamFilter: CombatSkillImportTargetTeamFilter.Self,
            rangePattern: CombatSkillImportRangePattern.Single,
            rangeValue: 0,
            areaPattern: CombatSkillImportAreaPattern.Self,
            apCost: 2,
            mpCost: 10,
            cooldownTu: 20,
            effectDefs: effects,
            levelOverrides: overrides,
            excludedTargetCreatureTypeTags: tokens,
            rangeMovePointCapacityMultiplier: 0,
            weaponRangePolicy: CombatWeaponRangePolicyImportKind.CurrentWeapon,
            areaValue: 0,
            requiresLos: false,
            groundEffectRequireFullArea: false,
            groundEffectRequireEmpty: false,
            groundEffectRequireTraversable: false,
            staminaCost: 0,
            mpCostPerTargetSlot: 0,
            staminaCostPerTargetSlot: 0,
            castingTimeTu: 0,
            castingMaintenanceDc: 0,
            castingSpellControlDc: 0,
            windupProfile: null,
            directionalPiercingProfile: null,
            approachAttackProfile: null,
            lineThroughAttackProfile: null,
            sequentialLineHitProfile: null,
            spellReactionProfile: null,
            rangedWeaponReactionProfile: null,
            pendingCastBindingMode: PendingCastBindingModeKind.SoftAnchor,
            attackRollBonus: 0,
            attackResolutionMode: CombatSkillLevelOverrideAttackResolutionMode.Auto,
            attackDefenseMode: CombatSkillLevelOverrideAttackDefenseMode.Normal,
            auraCost: 0,
            masteryTriggerMode: CombatMasteryTriggerImportKind.SkillDamageDiceMax,
            masteryAmountMode: CombatMasteryAmountImportKind.PerTargetRank,
            masteryBaseAmount: 1,
            spellFateMode: CombatSpellFateImportKind.None,
            spellCriticalMode: CombatSpellCriticalImportKind.None,
            spellCriticalMpRefundPercent: 0,
            fumbleProtectionCurve: numbers,
            fumbleProtectionExtraMpPercent: 100,
            backlashMode: CombatBacklashImportKind.None,
            backlashTargetFilter: null,
            backlashOffsetRadius: 0,
            areaOriginMode: CombatAreaOriginImportKind.Target,
            areaDirectionMode: CombatAreaDirectionImportKind.TargetVector,
            aiTags: tokens,
            deliveryCategories: tokens,
            attackRollBonusStatusId: default,
            attackRollBonusStatusStackDivisor: 0,
            projectileKind: CombatBaseProjectileImportKind.None,
            specialResolutionProfileId: default,
            targetSelectionMode: CombatTargetSelectionImportKind.SingleUnit,
            minTargetCount: 1,
            maxTargetCount: 1,
            allowRepeatTarget: false,
            unitTargetResolutionMode: CombatUnitTargetResolutionImportKind.Aggregate,
            maxHitsPerTarget: 0,
            randomChainAttackCount: 0,
            randomChainContinueOnMiss: false,
            selectionOrderMode: CombatSelectionOrderImportKind.Stable,
            passiveEffectDefs: Array.Empty<CombatEffectImportModel>(),
            castVariants: Array.Empty<CombatCastVariantImportModel>(),
            requiredWeaponFamilies: tokens,
            allowsNaturalWeapon: false,
            requiresHeavyWeapon: false,
            requiredWeaponTypeIds: tokens,
            excludedWeaponFamilies: tokens,
            excludedWeaponTypeIds: tokens,
            requiresEquippedShield: false,
            masteryLowHpBonusMultiplier: 1,
            masteryLowHpThresholdPercent: 50
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
        CombatSkillImportModel combat = CreateCombatModel(skillId, effects, overrides);
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
        SkillImportModel skill = CreateSkillModel(
            skillId,
            tags,
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

        SkillImportStringName.TryCreate("spell", out SkillImportStringName token);
        var numbers = new List<int> { 1 };
        var tokens = new List<SkillImportStringName> { token };
        var windup = new CombatWindupImportModel(6, 1, numbers, numbers);
        var directional = new CombatDirectionalPiercingImportModel(numbers, 20, 40, 32, 1, 100, 1, 1);
        var lineThrough = new CombatLineThroughAttackImportModel(2, 1, numbers, numbers, 1, 1, numbers);
        var sequential = new CombatSequentialLineHitImportModel(numbers, numbers, numbers);
        var spellReaction = new CombatSpellReactionImportModel(
            token, token, token, token, CombatSaveAbilityImportKind.Constitution, token,
            10, 2, numbers, numbers, true, true, true
        );
        var rangedReaction = new CombatRangedWeaponReactionImportModel(
            token, tokens, DamageTagImportKind.Force, CombatSkillLevelOverrideAttackDefenseMode.Touch,
            numbers, 1, true, true, false
        );
        var bindingList = new ContingencyStringListBindingImportValue(new[] { "fire" });
        CombatSkillImportModel expandedCombat = CreateCombatModel(
            skillId,
            Array.Empty<CombatEffectImportModel>(),
            Array.Empty<KeyValuePair<int, CombatSkillLevelOverrideImportModel>>(),
            tokens,
            numbers
        );
        numbers.Add(2);
        tokens.Add(token);

        _test.Eq(windup.SkillLevelTierCaps.Count, 1, "windup collections must be copied");
        _test.Eq(directional.BaseDamagePercentCurve.Count, 1, "directional collections must be copied");
        _test.Eq(lineThrough.PrimaryWeaponDiceMultiplierCurve.Count, 1, "line-through collections must be copied");
        _test.Eq(sequential.ContinuationRangeCurve.Count, 1, "sequential collections must be copied");
        _test.Eq(spellReaction.SaveDcBonusBySkillLevel.Count, 1, "spell reaction collections must be copied");
        _test.Eq(rangedReaction.TriggerWeaponFamilies.Count, 1, "ranged reaction collections must be copied");
        _test.Eq(expandedCombat.FumbleProtectionCurve.Count, 1, "combat int collections must be copied");
        _test.Eq(expandedCombat.AiTags.Count, 1, "combat token collections must be copied");
        _test.Eq(bindingList.Values.Count, 1, "binding list values must be copied");

        IReadOnlyDictionary<string, int> emptyMap = new SkillJsonDto().SkillLevelRequirements;
        var mutableView = (ICollection<KeyValuePair<string, int>>)emptyMap;
        _test.True(mutableView.IsReadOnly, "DTO empty map must expose a true read-only wrapper");
        bool emptyMapMutationRejected = false;
        try
        {
            mutableView.Add(new KeyValuePair<string, int>("poison", 1));
        }
        catch (NotSupportedException)
        {
            emptyMapMutationRejected = true;
        }
        _test.True(emptyMapMutationRejected, "DTO empty map mutation must be rejected");
    }

    private void TestImportModelsExposeOnlyPlainTypedClrState()
    {
        _test.Eq(
            typeof(SkillImportModel).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            ).Length,
            1,
            "root import model should expose one complete immutable construction contract"
        );
        _test.Eq(
            typeof(CombatSkillImportModel).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            ).Length,
            1,
            "combat import model should expose one complete immutable construction contract"
        );
        _test.True(
            typeof(SkillImportModel).GetProperty("RootDetails") == null,
            "root model must not retain a nullable details sidecar"
        );
        _test.True(
            typeof(CombatSkillImportModel).GetProperty("Details") == null,
            "combat model must not retain a nullable details sidecar"
        );

        Type[] modelTypes =
        {
            typeof(SkillImportModel),
            typeof(CombatSkillImportModel),
            typeof(CombatEffectImportModel),
            typeof(LayeredBarrierEffectPayloadImportModel),
            typeof(CombatSkillLevelOverrideImportModel),
            typeof(SkillDescriptionVariables),
            typeof(AttributeModifierImportModel),
            typeof(ContingencyAutomationImportModel),
            typeof(ContingencyBoolBindingImportValue),
            typeof(ContingencyIntBindingImportValue),
            typeof(ContingencyFloatBindingImportValue),
            typeof(ContingencyStringBindingImportValue),
            typeof(ContingencyStringListBindingImportValue),
            typeof(CombatWindupImportModel),
            typeof(CombatDirectionalPiercingImportModel),
            typeof(CombatApproachAttackImportModel),
            typeof(CombatLineThroughAttackImportModel),
            typeof(CombatSequentialLineHitImportModel),
            typeof(CombatSpellReactionImportModel),
            typeof(CombatRangedWeaponReactionImportModel),
            typeof(CombatCastVariantImportModel),
            typeof(CombatCastVariantPayloadImportModel),
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

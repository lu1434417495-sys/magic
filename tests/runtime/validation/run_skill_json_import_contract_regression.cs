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
                + "\"combat_profile\":{"
                + "\"skill_id\":\"mage_prismatic_red_ward\","
                + "\"target_team_filter\":\"self\",\"range_value\":0,"
                + "\"area_pattern\":\"self\",\"ap_cost\":2,\"mp_cost\":60,"
                + "\"cooldown_tu\":20,"
                + "\"effect_defs\":[{\"effect_type\":\"layered_barrier\","
                + "\"payload\":{\"area_pattern\":\"diamond\","
                + "\"profile_id\":\"prismatic_red_ward\","
                + "\"radius_cells\":1,\"save_dc\":16}}],"
                + "\"level_overrides\":{\"3\":{\"cooldown_tu\":15}}"
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
        if (combat.LevelOverrides.TryGetValue(3, out SkillLevelOverrideImportModel? levelOverride))
            _test.Eq(levelOverride.CooldownTu, 15, "level override should retain its typed value");
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
        var overrides = new List<KeyValuePair<int, SkillLevelOverrideImportModel>>
        {
            new(2, new SkillLevelOverrideImportModel(2, null, null, 10)),
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
        var skill = new SkillImportModel(
            skillId,
            "Copy",
            "",
            SkillImportType.Active,
            5,
            SkillImportLearnSource.Book,
            tags,
            combat
        );

        effects.Clear();
        overrides.Clear();
        tags.Clear();

        _test.Eq(combat.EffectDefs.Count, 1, "combat model must copy effect collection input");
        _test.Eq(combat.LevelOverrides.Count, 1, "combat model must copy override collection input");
        _test.Eq(skill.Tags.Count, 1, "skill model must copy tag collection input");
    }

    private void TestImportModelsExposeOnlyPlainTypedClrState()
    {
        Type[] modelTypes =
        {
            typeof(SkillImportModel),
            typeof(CombatSkillImportModel),
            typeof(CombatEffectImportModel),
            typeof(LayeredBarrierEffectPayloadImportModel),
            typeof(SkillLevelOverrideImportModel),
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

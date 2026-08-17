#nullable enable

using System;
using System.Linq;

public partial class run_layered_barrier_effect_payload_import_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestValidPayloadNormalizesToClosedTypedModel();
            TestUnknownKindFailsBeforePayloadSelection();
            TestMissingPayloadIsRejectedAtExactPointer();
            TestExtraPayloadMemberIsRejectedAtExactPointer();
            TestMissingRequiredPayloadMemberIsRejectedAtExactPointer();
            TestWrongPayloadFieldTypeIsRejectedAtExactPointer();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected layered barrier payload import regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Layered barrier effect payload import regression"));
    }

    private void TestValidPayloadNormalizesToClosedTypedModel()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "valid_payload",
            WrapEffect(
                "{\"effect_type\":\"layered_barrier\",\"min_skill_level\":3,"
                    + "\"max_skill_level\":4,\"duration_tu\":60,"
                    + "\"payload\":{\"area_pattern\":\"diamond\","
                    + "\"profile_id\":\"prismatic_violet_ward\","
                    + "\"radius_cells\":1,\"save_dc\":16}}"
            )
        );

        _test.True(
            result.HasValue,
            $"valid layered barrier payload should parse | {FormatDiagnostics(result)}"
        );
        _test.Eq(
            result.Diagnostics.Count,
            0,
            $"valid layered barrier payload should be diagnostic-free | {FormatDiagnostics(result)}"
        );
        if (!result.HasValue || result.Value.CombatProfile?.EffectDefs.Count != 1)
            return;

        CombatEffectImportModel effect = result.Value.CombatProfile.EffectDefs[0];
        _test.Eq(
            effect.Kind,
            CombatEffectImportKind.LayeredBarrier,
            "effect discriminator should normalize to the closed kind"
        );
        _test.Eq(effect.MinSkillLevel, 3, "effect minimum level should be retained");
        _test.Eq(effect.MaxSkillLevel, 4, "effect maximum level should be retained");
        _test.Eq(effect.Power, 0, "omitted layered barrier power should retain its default");
        _test.Eq(effect.DurationTu, 60, "effect duration should remain outside the payload");
        _test.True(
            effect.Payload is LayeredBarrierEffectPayloadImportModel,
            "closed kind must publish only its matching typed payload"
        );
        if (effect.Payload is not LayeredBarrierEffectPayloadImportModel payload)
            return;

        _test.Eq(
            payload.AreaPattern,
            CombatSkillImportAreaPattern.Diamond,
            "payload area pattern should be typed"
        );
        _test.Eq(
            payload.ProfileId.Value,
            "prismatic_violet_ward",
            "payload profile ID should remain canonical"
        );
        _test.Eq(payload.RadiusCells, 1, "payload radius should be retained");
        _test.Eq(payload.SaveDc, 16, "payload save DC should be retained");
    }

    private void TestUnknownKindFailsBeforePayloadSelection()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "unknown_kind",
            WrapEffect(
                "{\"effect_type\":\"future_barrier\","
                    + "\"payload\":{\"area_pattern\":\"diamond\","
                    + "\"profile_id\":\"prismatic_red_ward\","
                    + "\"radius_cells\":1,\"save_dc\":16}}"
            )
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.UnknownEffectKind,
            "/entries/3/combat_profile/effect_defs/0/effect_type",
            "unknown effect kind"
        );
    }

    private void TestMissingPayloadIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "missing_payload",
            WrapEffect("{\"effect_type\":\"layered_barrier\"}")
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.MissingEffectPayload,
            "/entries/3/combat_profile/effect_defs/0/payload",
            "missing layered barrier payload"
        );
    }

    private void TestExtraPayloadMemberIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "extra_payload_member",
            WrapEffect(
                "{\"effect_type\":\"layered_barrier\","
                    + "\"payload\":{\"area_pattern\":\"diamond\","
                    + "\"profile_id\":\"prismatic_red_ward\","
                    + "\"radius_cells\":1,\"save_dc\":16,\"raw\":true}}"
            )
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.InvalidEffectPayload,
            "/entries/3/combat_profile/effect_defs/0/payload/raw",
            "extra layered barrier payload member"
        );
    }

    private void TestMissingRequiredPayloadMemberIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "missing_profile_id",
            WrapEffect(
                "{\"effect_type\":\"layered_barrier\","
                    + "\"payload\":{\"area_pattern\":\"diamond\","
                    + "\"radius_cells\":1,\"save_dc\":16}}"
            )
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.RequiredMember,
            "/entries/3/combat_profile/effect_defs/0/payload/profile_id",
            "missing required layered barrier profile ID"
        );
    }

    private void TestWrongPayloadFieldTypeIsRejectedAtExactPointer()
    {
        ContentImportStageResult<SkillImportModel> result = Parse(
            "wrong_payload_type",
            WrapEffect(
                "{\"effect_type\":\"layered_barrier\","
                    + "\"payload\":{\"area_pattern\":\"diamond\","
                    + "\"profile_id\":\"prismatic_red_ward\","
                    + "\"radius_cells\":\"one\",\"save_dc\":16}}"
            )
        );

        AssertSingleFailure(
            result,
            SkillJsonImportRules.InvalidEffectPayload,
            "/entries/3/combat_profile/effect_defs/0/payload/radius_cells",
            "wrong layered barrier radius type"
        );
    }

    private void AssertSingleFailure(
        ContentImportStageResult<SkillImportModel> result,
        string ruleId,
        string pointer,
        string label
    )
    {
        _test.False(result.HasValue, $"{label} should not publish a partial model");
        _test.Eq(
            result.Diagnostics.Count,
            1,
            $"{label} should emit one diagnostic | {FormatDiagnostics(result)}"
        );
        if (result.Diagnostics.Count != 1)
            return;

        _test.Eq(result.Diagnostics[0].RuleId, ruleId, $"{label} rule ID should be stable");
        _test.Eq(result.Diagnostics[0].JsonPointer, pointer, $"{label} pointer should be exact");
    }

    private static ContentImportStageResult<SkillImportModel> Parse(string entryId, string json) =>
        SkillJsonImportParser.Parse(
            new JsonContentEntryContext("skills", entryId, $"skill.json#{entryId}", "/entries/3"),
            json
        );

    private static string WrapEffect(string effectJson) =>
        "{\"skill_id\":\"mage_prismatic_ward\",\"display_name\":\"Prismatic Ward\","
            + "\"max_level\":5,\"combat_profile\":{"
            + "\"skill_id\":\"mage_prismatic_ward\",\"effect_defs\":["
            + effectJson
            + "]}}";

    private static string FormatDiagnostics(ContentImportStageResult<SkillImportModel> result) =>
        string.Join(
            "; ",
            result.Diagnostics.Select(diagnostic => $"{diagnostic.RuleId}@{diagnostic.JsonPointer}")
        );
}

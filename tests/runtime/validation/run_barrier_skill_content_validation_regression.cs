using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_barrier_skill_content_validation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            using var loader = new TestContentResourceLoader();
            using var progression = new ProgressionContentRegistry(loader);
            using var barriers = new BarrierContentRegistry(loader);

            IReadOnlyDictionary<StringName, SkillDefinition> officialSkills =
                progression.GetSkillDefinitionsTyped();
            IReadOnlyDictionary<StringName, BarrierProfileDefinition> officialBarriers =
                barriers.GetProfileDefsTyped();

            TestOfficialCrossTableIsValid(officialSkills, officialBarriers);
            TestMissingLayeredBarrierProfileIsRejected(officialSkills, officialBarriers);
            TestMissingBreakerSkillIsRejected(officialSkills, officialBarriers);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Barrier skill content validation regression"));
    }

    private void TestOfficialCrossTableIsValid(
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barriers
    )
    {
        IReadOnlyList<string> errors = BarrierSkillContentValidator.Validate(skills, barriers);
        _test.Eq(errors.Count, 0, $"正式技能/屏障跨表引用应全部有效：{string.Join(" | ", errors)}");
    }

    private void TestMissingLayeredBarrierProfileIsRejected(
        IReadOnlyDictionary<StringName, SkillDefinition> officialSkills,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> officialBarriers
    )
    {
        const string missingProfileId = "missing_barrier_profile_probe";
        StringName skillId = "missing_barrier_profile_skill_probe";
        var skills = new Dictionary<StringName, SkillDefinition>(officialSkills)
        {
            [skillId] = BuildLayeredBarrierSkill(skillId, missingProfileId),
        };

        IReadOnlyList<string> errors = BarrierSkillContentValidator.Validate(
            skills,
            officialBarriers
        );
        _test.True(
            errors.Any(error =>
                error.Contains(skillId.ToString(), StringComparison.Ordinal)
                && error.Contains(missingProfileId, StringComparison.Ordinal)
                && error.Contains("missing barrier profile", StringComparison.Ordinal)
            ),
            "layered_barrier 的未知 profile_id 必须在 process snapshot seal 前失败。"
        );
    }

    private void TestMissingBreakerSkillIsRejected(
        IReadOnlyDictionary<StringName, SkillDefinition> officialSkills,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> officialBarriers
    )
    {
        StringName profileId = "missing_breaker_profile_probe";
        StringName missingSkillId = "missing_breaker_skill_probe";
        var barriers = new Dictionary<StringName, BarrierProfileDefinition>(officialBarriers)
        {
            [profileId] = new BarrierProfileDefinition(
                profileId,
                "Missing Breaker Probe",
                "fixed",
                "diamond",
                2,
                120,
                true,
                new[]
                {
                    new BarrierLayerDefinition(
                        "probe_layer",
                        "Probe Layer",
                        1,
                        Array.Empty<StringName>(),
                        new[] { missingSkillId },
                        Array.Empty<BarrierOutcomeDefinition>()
                    ),
                }
            ),
        };

        IReadOnlyList<string> errors = BarrierSkillContentValidator.Validate(
            officialSkills,
            barriers
        );
        _test.True(
            errors.Any(error =>
                error.Contains(profileId.ToString(), StringComparison.Ordinal)
                && error.Contains(missingSkillId.ToString(), StringComparison.Ordinal)
                && error.Contains("missing skill", StringComparison.Ordinal)
            ),
            "屏障层的未知 breaker_skill_id 必须在 process snapshot seal 前失败。"
        );
    }

    private static SkillDefinition BuildLayeredBarrierSkill(
        StringName skillId,
        StringName profileId
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            parameters: new Dictionary<string, object>
            {
                ["profile_id"] = profileId,
                ["radius_cells"] = 2L,
                ["area_pattern"] = new StringName("diamond"),
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Missing Barrier Profile Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "self"
            )
        );
    }
}

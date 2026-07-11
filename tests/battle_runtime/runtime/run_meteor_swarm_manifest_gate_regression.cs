using System;
using System.Collections.Generic;
using Godot;

public partial class run_meteor_swarm_manifest_gate_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        using var contentLoader = new TestContentResourceLoader();
        using var progressionRegistry = new ProgressionContentRegistry(contentLoader);
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions =
            progressionRegistry.GetSkillDefinitionsTyped();
        SkillDefinition meteorSkillDefinition = GetSkillDefinition(
            typedSkillDefinitions,
            "mage_meteor_swarm"
        );
        _test.True(meteorSkillDefinition != null, "陨星雨 DTO 应存在。");
        _test.True(
            meteorSkillDefinition?.CombatProfile != null,
            "陨星雨应声明 combat_profile。"
        );
        if (meteorSkillDefinition == null || meteorSkillDefinition.CombatProfile == null)
            return Finish();

        CombatSkillDefinition combatProfile = meteorSkillDefinition.CombatProfile;
        _test.Eq(
            combatProfile.SpecialResolutionProfileId.ToString(),
            "meteor_swarm",
            "陨星雨应切到 meteor_swarm special profile。"
        );
        _test.Eq(
            combatProfile.EffectDefinitions.Count,
            0,
            "陨星雨不应保留 executable effect_defs。"
        );
        _test.Eq(
            combatProfile.AreaPattern.ToString(),
            "radius",
            "陨星雨 shell 应保留 square-radius area metadata。"
        );
        _test.Eq(combatProfile.AreaValue, 3, "陨星雨 shell 的最外层应为 7x7。");

        using var registry = new BattleSpecialProfileRegistry(contentLoader);
        registry.Rebuild(typedSkillDefinitions);
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        Godot.Collections.Array<string> errors = registry.Validate();
        _test.Eq(
            FormatArray(typedErrors),
            FormatArray(errors),
            "battle special profile registry typed/public validation errors 应保持一致。"
        );
        _test.True(errors.Count == 0, $"正式 battle special profile manifest 应通过校验：{FormatArray(errors)}");
        IBattleSpecialProfileView profileView = registry.BuildRuntimeProfileView();
        _test.True(
            profileView.TryGetMeteorSwarmProfile(
                "meteor_swarm",
                out MeteorSwarmProfileData meteorProfileData
            ),
            "battle special profile typed view 应包含 meteor_swarm profile。"
        );
        _test.Eq(
            meteorProfileData?.profile_id.ToString() ?? "",
            "meteor_swarm",
            "typed view 应保留 hardcoded meteor_swarm profile id。"
        );
        _test.True(
            meteorProfileData?.impact_components.Count >= 4,
            "meteor_swarm typed view 应声明 impact components。"
        );
        _test.True(
            meteorProfileData?.terrain_profiles.Count >= 5,
            "meteor_swarm typed view 应声明 terrain profiles。"
        );

        Resource profileResource = contentLoader.LoadCanonical<MeteorSwarmProfile>(
            "res://data/configs/skill_special_profiles/profiles/meteor_swarm_profile.tres"
        );
        _test.True(profileResource != null, "registry authoring boundary 应持有已加载 profile_resource。");
        var meteorProfile = profileResource as MeteorSwarmProfile;
        _test.True(meteorProfile != null, "profile_resource 应为 MeteorSwarmProfile。");
        if (meteorProfile != null)
        {
            _test.True(
                meteorProfile.impact_components.Count >= 4,
                "meteor_swarm profile 应声明 typed impact components。"
            );
            _test.True(
                meteorProfile.terrain_profiles.Count >= 5,
                "meteor_swarm profile 应声明 typed terrain profiles。"
            );
        }

        TestManifestValidatorRejectsNonDefaultRequiredTests(typedSkillDefinitions, profileResource);
        TestManifestValidatorRejectsUnknownSaveProfile(profileResource);
        TestManifestValidatorRejectsDuplicateComponentId(profileResource);
        TestManifestValidatorRejectsComponentRingOutsideRadius(profileResource);
        TestManifestValidatorRejectsTerrainRingOutsideRadius(profileResource);
        TestRegistryUsesExactSkillDefinitionKeys(meteorSkillDefinition, contentLoader);
        TestGateAllowsValidManifest(meteorSkillDefinition, profileView);
        TestGateFailsClosedForInvalidManifest(meteorSkillDefinition);

        return Finish();
    }

    private void TestRegistryUsesExactSkillDefinitionKeys(
        SkillDefinition meteorSkillDefinition,
        IContentResourceLoader contentLoader
    )
    {
        var wrongKeySkillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [new StringName("wrong_meteor_swarm_key")] = meteorSkillDefinition,
        };
        using var registry = new BattleSpecialProfileRegistry(contentLoader);
        registry.Rebuild(wrongKeySkillDefinitions);

        _test.False(
            registry.BuildRuntimeProfileView().TryGetMeteorSwarmProfile(
                "meteor_swarm",
                out MeteorSwarmProfileData _
            ),
            "special profile registry 应把错误 key 的 skill_defs 判为无效输入。"
        );
    }

    private void TestManifestValidatorRejectsNonDefaultRequiredTests(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        Resource profileResource
    )
    {
        var manifest = new BattleSpecialProfileManifest
        {
            profile_id = "meteor_swarm",
            schema_version = 1,
            owning_skill_ids = new Godot.Collections.Array<Godot.StringName> { "mage_meteor_swarm" },
            runtime_resolver_id = "meteor_swarm",
            runtime_read_policy = "forbidden",
            profile_resource = profileResource,
            required_regression_tests = new Godot.Collections.Array<string>
            {
                "tests/battle_runtime/simulation/run_battle_simulation_regression.cs",
                "docs/discussions/meteor_swarm_impact_analysis.md",
            },
        };

        var validator = new BattleSpecialProfileManifestValidator();
        Godot.Collections.Array<string> errors = validator.ValidateManifest(
            manifest,
            skillDefinitions,
            ""
        );
        _test.True(errors.Count > 0, $"manifest validator 应拒绝 simulation/docs 等非默认回归入口：{FormatArray(errors)}");
    }

    private void TestManifestValidatorRejectsUnknownSaveProfile(Resource profileResource)
    {
        MeteorSwarmProfile profile = DuplicateProfile(profileResource, "save profile 负例前置");
        if (profile == null || profile.impact_components.Count == 0)
            return;

        profile.impact_components[0].save_profile_id = "legacy_dex_save";
        var validator = new BattleSpecialProfileManifestValidator();
        Godot.Collections.Array<string> errors = validator.ValidateMeteorSwarmProfile(profile, true);
        _test.True(errors.Count > 0, $"manifest validator 应拒绝未知 save_profile_id，避免运行时 fallback：{FormatArray(errors)}");
    }

    private void TestManifestValidatorRejectsDuplicateComponentId(Resource profileResource)
    {
        MeteorSwarmProfile profile = DuplicateProfile(profileResource, "duplicate component_id 负例前置");
        if (profile == null || profile.impact_components.Count < 2)
            return;

        profile.impact_components[1].component_id = profile.impact_components[0].component_id;
        var validator = new BattleSpecialProfileManifestValidator();
        Godot.Collections.Array<string> errors = validator.ValidateMeteorSwarmProfile(profile, true);
        _test.True(errors.Count > 0, $"manifest validator 应拒绝重复 impact component_id：{FormatArray(errors)}");
    }

    private void TestManifestValidatorRejectsComponentRingOutsideRadius(Resource profileResource)
    {
        MeteorSwarmProfile profile = DuplicateProfile(profileResource, "component ring 负例前置");
        if (profile == null || profile.impact_components.Count == 0)
            return;

        profile.impact_components[0].ring_max = 4;
        var validator = new BattleSpecialProfileManifestValidator();
        Godot.Collections.Array<string> errors = validator.ValidateMeteorSwarmProfile(profile, true);
        _test.True(errors.Count > 0, $"manifest validator 应拒绝越过 7x7 半径的 impact component ring：{FormatArray(errors)}");
    }

    private void TestManifestValidatorRejectsTerrainRingOutsideRadius(Resource profileResource)
    {
        MeteorSwarmProfile profile = DuplicateProfile(profileResource, "terrain ring 负例前置");
        if (profile == null || profile.terrain_profiles.Count == 0)
            return;

        Godot.Collections.Dictionary terrainProfile = profile.terrain_profiles[0].AsGodotDictionary().Duplicate(true);
        terrainProfile["ring_max"] = 4;
        profile.terrain_profiles[0] = terrainProfile;
        var validator = new BattleSpecialProfileManifestValidator();
        Godot.Collections.Array<string> errors = validator.ValidateMeteorSwarmProfile(profile, true);
        _test.True(errors.Count > 0, $"manifest validator 应拒绝越过 7x7 半径的 terrain profile ring：{FormatArray(errors)}");
    }

    private void TestGateAllowsValidManifest(
        SkillDefinition meteorSkillDefinition,
        IBattleSpecialProfileView profileView
    )
    {
        var gate = new BattleSpecialProfileGate();
        gate.Setup(profileView);
        BattleSpecialProfileGateResult allowedResult = gate.PreviewSkill(
            meteorSkillDefinition,
            new BattleCommand(),
            new BattleUnitState(),
            new BattleState()
        );
        _test.True(allowedResult.Allowed, "manifest gate 通过时应允许进入 meteor resolver。");
        _test.Eq(
            allowedResult.ProfileId.ToString(),
            "meteor_swarm",
            "manifest gate result 应暴露 typed profile id。"
        );
    }

    private void TestGateFailsClosedForInvalidManifest(SkillDefinition meteorSkillDefinition)
    {
        var invalidGate = new BattleSpecialProfileGate();
        invalidGate.Setup(BattleSpecialProfileRuntimeView.Empty);
        BattleSpecialProfileGateResult blockedResult = invalidGate.PreviewSkill(
            meteorSkillDefinition,
            new BattleCommand(),
            new BattleUnitState(),
            new BattleState()
        );
        _test.False(blockedResult.Allowed, "manifest gate 失败时应 fail closed。");
        _test.Eq(
            blockedResult.PlayerMessage,
            "该禁咒配置未通过校验，暂时无法施放。",
            "manifest gate fail closed 文案应稳定。"
        );
        _test.True(
            blockedResult.DebugDetails.ContainsKey("errors"),
            "manifest gate fail closed 应保留 typed debug details。"
        );
        Godot.Collections.Dictionary payload =
            BattleSpecialProfileGateResultProjection.Project(blockedResult);
        _test.Eq(
            GetString(payload, "player_message"),
            "该禁咒配置未通过校验，暂时无法施放。",
            "gate result projection 仅作为 Godot 边界投影。"
        );
    }

    private TestResult Finish() => _test.Finish("Meteor swarm manifest gate regression");

    private static string GetString(Godot.Collections.Dictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return source[key].ToString();
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (
            skillDefinitions == null
            || !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
        )
            return null;
        return skillDefinition;
    }

    private MeteorSwarmProfile DuplicateProfile(Resource profileResource, string preconditionLabel)
    {
        var profile = TestResourceOwnership.Own(
            (profileResource as MeteorSwarmProfile)?.Duplicate(true) as MeteorSwarmProfile,
            $"meteor_swarm_manifest_gate.duplicate_profile.{preconditionLabel}"
        );
        _test.True(profile != null, $"{preconditionLabel}：profile 应能 duplicate。");
        return profile;
    }

    private static string FormatArray(Godot.Collections.Array<string> values)
    {
        return values == null ? "[]" : string.Join(", ", values);
    }

    private static string FormatArray(IEnumerable<string> values)
    {
        if (values == null)
            return "";
        return string.Join(", ", values);
    }

}

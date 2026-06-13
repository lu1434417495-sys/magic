using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_meteor_swarm_manifest_gate_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestPlainCSharpGateContract();

        var progressionRegistry = new ProgressionContentRegistry();
        IReadOnlyDictionary<StringName, SkillDef> typedSkillDefs =
            progressionRegistry.GetSkillDefsTyped();
        SkillDef meteorSkill = GetSkill(typedSkillDefs, "mage_meteor_swarm");
        _test.True(meteorSkill != null, "陨星雨技能应存在。");
        _test.True(meteorSkill?.combat_profile != null, "陨星雨应声明 combat_profile。");
        if (meteorSkill == null || meteorSkill.combat_profile == null)
            return Finish();

        CombatSkillDef combatProfile = meteorSkill.combat_profile;
        _test.Eq(
            combatProfile.special_resolution_profile_id.ToString(),
            "meteor_swarm",
            "陨星雨应切到 meteor_swarm special profile。"
        );
        _test.Eq(
            combatProfile.effect_defs.Count,
            0,
            "陨星雨不应保留 executable effect_defs。"
        );
        _test.Eq(
            combatProfile.area_pattern.ToString(),
            "radius",
            "陨星雨 shell 应保留 square-radius area metadata。"
        );
        _test.Eq(combatProfile.area_value, 3, "陨星雨 shell 的最外层应为 7x7。");

        var registry = new BattleSpecialProfileRegistry();
        registry.Rebuild(typedSkillDefs);
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        Godot.Collections.Array<string> errors = registry.Validate();
        _test.Eq(
            FormatArray(typedErrors),
            FormatArray(errors),
            "battle special profile registry typed/public validation errors 应保持一致。"
        );
        _test.True(errors.Count == 0, $"正式 battle special profile manifest 应通过校验：{FormatArray(errors)}");
        Godot.Collections.Dictionary snapshot = registry.GetSnapshot();
        _test.True(GetBool(snapshot, "ok"), "battle special profile snapshot 应为 ok。");

        Godot.Collections.Dictionary profileIdBySkillId = GetDictionary(snapshot, "profile_id_by_skill_id");
        _test.Eq(
            GetString(profileIdBySkillId, "mage_meteor_swarm"),
            "meteor_swarm",
            "snapshot 应映射 mage_meteor_swarm -> meteor_swarm。"
        );
        Godot.Collections.Dictionary profiles = GetDictionary(snapshot, "profiles");
        _test.True(profiles.ContainsKey("meteor_swarm"), "snapshot 应包含 meteor_swarm profile。");
        Godot.Collections.Dictionary meteorProfileSnapshot = GetDictionary(profiles, "meteor_swarm");
        _test.Eq(
            GetString(meteorProfileSnapshot, "runtime_resolver_id"),
            "meteor_swarm",
            "runtime resolver id 必须走 hardcoded meteor_swarm。"
        );

        Resource profileResource = GetResource(meteorProfileSnapshot, "profile_resource");
        _test.True(profileResource != null, "snapshot 应携带已加载 profile_resource。");
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

        TestManifestValidatorRejectsNonDefaultRequiredTests(typedSkillDefs, profileResource);
        TestManifestValidatorRejectsUnknownSaveProfile(profileResource);
        TestManifestValidatorRejectsDuplicateComponentId(profileResource);
        TestManifestValidatorRejectsComponentRingOutsideRadius(profileResource);
        TestManifestValidatorRejectsTerrainRingOutsideRadius(profileResource);
        TestRegistryUsesExactSkillDefKeys(meteorSkill);
        TestGateAllowsValidManifest(meteorSkill, snapshot);
        TestGateFailsClosedForInvalidManifest(meteorSkill);

        return Finish();
    }

    private void TestPlainCSharpGateContract()
    {
        AssertPublicApiDoesNotExposeGodotPayload(
            typeof(BattleSpecialProfileGateResult),
            nameof(BattleSpecialProfileGateResult)
        );

        PropertyInfo debugDetails = typeof(BattleSpecialProfileGateResult).GetProperty(
            nameof(BattleSpecialProfileGateResult.DebugDetails)
        );
        _test.True(debugDetails != null, "Gate result 应暴露 DebugDetails typed property。");
        if (debugDetails != null)
        {
            _test.False(
                IsGodotPayloadType(debugDetails.PropertyType),
                "Gate result DebugDetails 不应是 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (MethodInfo method in typeof(BattleSpecialProfileGate).GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                 ))
        {
            if (method.Name == nameof(BattleSpecialProfileGate.Setup))
                continue;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.False(
                    IsGodotPayloadType(parameter.ParameterType),
                    $"{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private void TestRegistryUsesExactSkillDefKeys(SkillDef meteorSkill)
    {
        var wrongKeySkillDefs = new Dictionary<StringName, SkillDef>
        {
            [new StringName("wrong_meteor_swarm_key")] = meteorSkill,
        };
        var registry = new BattleSpecialProfileRegistry();
        registry.Rebuild(wrongKeySkillDefs);

        Godot.Collections.Dictionary snapshot = registry.GetSnapshot();
        Godot.Collections.Dictionary profileIdBySkillId = GetDictionary(
            snapshot,
            "profile_id_by_skill_id"
        );
        _test.Eq(
            GetString(profileIdBySkillId, "mage_meteor_swarm"),
            "",
            "special profile registry 不应从 value.skill_id 恢复错误 key 的 skill/profile 映射。"
        );
        _test.False(
            GetBool(snapshot, "ok"),
            "special profile registry 应把错误 key 的 skill_defs 判为无效输入。"
        );
    }

    private void TestManifestValidatorRejectsNonDefaultRequiredTests(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
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
            ProjectSkillDefs(skillDefs),
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

    private void TestGateAllowsValidManifest(SkillDef meteorSkill, Godot.Collections.Dictionary snapshot)
    {
        var gate = new BattleSpecialProfileGate();
        gate.Setup(snapshot);
        BattleSpecialProfileGateResult allowedResult = gate.PreviewSkill(
            meteorSkill,
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

    private void TestGateFailsClosedForInvalidManifest(SkillDef meteorSkill)
    {
        var invalidGate = new BattleSpecialProfileGate();
        invalidGate.Setup(
            new Godot.Collections.Dictionary
            {
                ["ok"] = false,
                ["errors"] = new Godot.Collections.Array<string> { "fixture error" },
                ["profiles"] = new Godot.Collections.Dictionary(),
                ["profile_id_by_skill_id"] = new Godot.Collections.Dictionary(),
            }
        );
        BattleSpecialProfileGateResult blockedResult = invalidGate.PreviewSkill(
            meteorSkill,
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
        Godot.Collections.Dictionary payload = blockedResult.ToDictionary();
        _test.Eq(
            GetString(payload, "player_message"),
            "该禁咒配置未通过校验，暂时无法施放。",
            "gate result ToDictionary 仅作为 Godot 边界投影。"
        );
    }

    private int Finish() => _test.Finish("Meteor swarm manifest gate regression");

    private static SkillDef GetSkill(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        StringName skillId
    )
    {
        if (skillDefs == null || !skillDefs.TryGetValue(skillId, out SkillDef skillDef))
            return null;
        return skillDef;
    }

    private MeteorSwarmProfile DuplicateProfile(Resource profileResource, string preconditionLabel)
    {
        var profile = (profileResource as MeteorSwarmProfile)?.Duplicate(true) as MeteorSwarmProfile;
        _test.True(profile != null, $"{preconditionLabel}：profile 应能 duplicate。");
        return profile;
    }

    private static Godot.Collections.Dictionary GetDictionary(Godot.Collections.Dictionary source, string key)
    {
        if (!TryGetValue(source, key, out Variant value))
            return new Godot.Collections.Dictionary();
        return value.AsGodotDictionary();
    }

    private static Resource GetResource(Godot.Collections.Dictionary source, string key)
    {
        if (!TryGetValue(source, key, out Variant value))
            return null;
        return value.AsGodotObject() as Resource;
    }

    private static bool GetBool(Godot.Collections.Dictionary source, string key)
    {
        return TryGetValue(source, key, out Variant value) && value.AsBool();
    }

    private static string GetString(Godot.Collections.Dictionary source, string key)
    {
        return TryGetValue(source, key, out Variant value) ? value.ToString() : "";
    }

    private static bool TryGetValue(Godot.Collections.Dictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null)
            return false;
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        var stringNameKey = new StringName(key);
        if (source.ContainsKey(stringNameKey))
        {
            value = source[stringNameKey];
            return true;
        }
        return false;
    }

    private void AssertPublicApiDoesNotExposeGodotPayload(Type type, string label)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            _test.False(
                IsGodotPayloadType(property.PropertyType),
                $"{label}.{property.Name} 不应公开 Godot Dictionary/Array/Variant 属性。"
            );
        }
        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (method.IsSpecialName)
                continue;
            _test.False(
                IsGodotPayloadType(method.ReturnType),
                $"{label}.{method.Name} 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.False(
                    IsGodotPayloadType(parameter.ParameterType),
                    $"{label}.{method.Name} 不应公开接收 Godot Dictionary/Array/Variant 参数 {parameter.Name}。"
                );
            }
        }
    }

    private static bool IsGodotPayloadType(Type type)
    {
        if (type == typeof(Variant) || type == typeof(Godot.Collections.Dictionary))
            return true;
        if (type.Namespace == "Godot.Collections")
            return true;
        if (typeof(Godot.Collections.Array).IsAssignableFrom(type))
            return true;
        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();
            if (
                genericType == typeof(Godot.Collections.Array<>)
                || genericType == typeof(Godot.Collections.Dictionary<,>)
            )
                return true;
        }
        return false;
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

    private static Godot.Collections.Dictionary ProjectSkillDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        Godot.Collections.Dictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillDef == null || skillId == "")
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_meteor_swarm_manifest_gate_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            GameSessionTestFactory.GetProcessSnapshot().Skills;
        SkillDefinition meteorSkill = skillDefinitions.GetValueOrDefault("mage_meteor_swarm");
        _test.True(meteorSkill?.CombatProfile != null, "Meteor swarm skill definition must exist.");
        if (meteorSkill?.CombatProfile == null)
        {
            RequestTestExit(_test.Finish("Meteor swarm manifest gate regression"));
            return;
        }

        _test.Eq(meteorSkill.CombatProfile.SpecialResolutionProfileId,
            new StringName("meteor_swarm"),
            "Meteor swarm must reference its special profile by ID.");
        _test.Eq(meteorSkill.CombatProfile.EffectDefinitions.Count, 0,
            "Special-profile skills must not retain executable effect_defs.");

        using var registry = new BattleSpecialProfileRegistry();
        registry.Rebuild(skillDefinitions);
        IReadOnlyList<string> errors = registry.ValidateTyped();
        _test.Eq(errors.Count, 0,
            $"Production JSON special-profile registry must validate: {string.Join(" | ", errors)}");
        _test.Eq(string.Join(" | ", registry.Validate()), string.Join(" | ", errors),
            "Typed and public registry diagnostics must stay equivalent.");

        IBattleSpecialProfileView profileView = registry.BuildRuntimeProfileView();
        _test.True(profileView.TryGetMeteorSwarmProfile(
                "meteor_swarm", out MeteorSwarmProfileData profile),
            "Runtime view must publish meteor_swarm by profile ID.");
        _test.Eq(profile?.profile_id ?? new StringName(""), new StringName("meteor_swarm"),
            "Projected profile ID must be stable.");
        _test.True(profile?.impact_components.Count >= 4,
            "Projected meteor profile must preserve impact components.");
        _test.True(profile?.terrain_profiles.Count >= 5,
            "Projected meteor profile must preserve terrain profiles.");

        TestPureImportValidation();
        TestRegistryUsesExactSkillDefinitionKeys(meteorSkill);
        TestGateAllowsValidManifest(meteorSkill, profileView);
        TestGateFailsClosedForMissingProfile(meteorSkill);
        RequestTestExit(_test.Finish("Meteor swarm manifest gate regression"));
    }

    private void TestPureImportValidation()
    {
        var reader = new GodotContentJsonSourceReader();
        ContentImportBatch<BattleSpecialProfileImportModel> batch =
            BattleSpecialProfileJsonAuthoringDomains
                .CreateProfileDescriptor(BattleSpecialProfileJsonDomains.ProfileDirectory, reader)
                .Import();
        _test.Eq(batch.Diagnostics.Count, 0,
            $"Canonical profile JSON must import: {FormatDiagnostics(batch.Diagnostics)}");
        _test.Eq(batch.Entries.Count, 1, "Canonical profile JSON must contain one profile.");
        if (batch.Entries.Count != 1)
            return;

        ContentImportEntry<BattleSpecialProfileImportModel> entry = batch.Entries[0];
        MeteorSwarmProfileImportModel source = entry.Import.MeteorSwarm;
        MeteorSwarmImpactComponentImportModel[] components = source.ImpactComponents.ToArray();
        MeteorSwarmTerrainProfileImportModel[] terrain = source.TerrainProfiles.ToArray();

        components[0] = components[0] with { SaveProfileId = "legacy_dex_save" };
        AssertSingleDiagnostic(
            entry.Context,
            entry.Import with { MeteorSwarm = source with { ImpactComponents = components } },
            BattleSpecialProfileJsonRules.ValueUnsupported,
            "/profile/payload/impact_components/0/save_profile_id",
            "Unknown save profile must fail in the pure import validator."
        );

        components = source.ImpactComponents.ToArray();
        components[1] = components[1] with { ComponentId = components[0].ComponentId };
        AssertSingleDiagnostic(
            entry.Context,
            entry.Import with { MeteorSwarm = source with { ImpactComponents = components } },
            BattleSpecialProfileJsonRules.DuplicateId,
            "/profile/payload/impact_components/1/component_id",
            "Duplicate component ID must fail in the pure import validator."
        );

        components = source.ImpactComponents.ToArray();
        components[0] = components[0] with { RingMax = source.Radius + 1 };
        AssertSingleDiagnostic(
            entry.Context,
            entry.Import with { MeteorSwarm = source with { ImpactComponents = components } },
            BattleSpecialProfileJsonRules.ValueOutOfRange,
            "/profile/payload/impact_components/0",
            "Component ring outside radius must fail in the pure import validator."
        );

        terrain[0] = terrain[0] with { RingMax = source.Radius + 1 };
        AssertSingleDiagnostic(
            entry.Context,
            entry.Import with { MeteorSwarm = source with { TerrainProfiles = terrain } },
            BattleSpecialProfileJsonRules.ValueOutOfRange,
            "/profile/payload/terrain_profiles/0",
            "Terrain ring outside radius must fail in the pure import validator."
        );
    }

    private void AssertSingleDiagnostic(
        JsonContentEntryContext context,
        BattleSpecialProfileImportModel import,
        string expectedRule,
        string expectedPointerSuffix,
        string label
    )
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics =
            BattleSpecialProfileImportValidator.ValidateProfile(context, import);
        _test.Eq(diagnostics.Count, 1,
            $"{label} diagnostics={FormatDiagnostics(diagnostics)}");
        if (diagnostics.Count != 1)
            return;
        _test.Eq(diagnostics[0].RuleId, expectedRule, label);
        _test.True(diagnostics[0].JsonPointer.EndsWith(expectedPointerSuffix,
                StringComparison.Ordinal),
            $"{label} pointer={diagnostics[0].JsonPointer}");
    }

    private void TestRegistryUsesExactSkillDefinitionKeys(SkillDefinition meteorSkill)
    {
        using var registry = new BattleSpecialProfileRegistry();
        registry.Rebuild(new Dictionary<StringName, SkillDefinition>
        {
            ["wrong_meteor_swarm_key"] = meteorSkill,
        });
        _test.True(registry.ValidateTyped().Count > 0,
            "Wrong skill-definition key must invalidate the manifest graph.");
        _test.False(registry.BuildRuntimeProfileView().TryGetMeteorSwarmProfile(
                "meteor_swarm", out _),
            "Invalid manifest graph must publish an empty runtime view.");
    }

    private void TestGateAllowsValidManifest(
        SkillDefinition meteorSkill,
        IBattleSpecialProfileView profileView
    )
    {
        var gate = new BattleSpecialProfileGate();
        gate.Setup(profileView);
        BattleSpecialProfileGateResult result = gate.PreviewSkill(
            meteorSkill, new BattleCommand(), new BattleUnitState(), new BattleState());
        _test.True(result.Allowed, "Valid JSON manifest must allow the meteor resolver.");
        _test.Eq(result.ProfileId, new StringName("meteor_swarm"),
            "Gate result must expose the typed profile ID.");
    }

    private void TestGateFailsClosedForMissingProfile(SkillDefinition meteorSkill)
    {
        var gate = new BattleSpecialProfileGate();
        gate.Setup(BattleSpecialProfileRuntimeView.Empty);
        BattleSpecialProfileGateResult result = gate.PreviewSkill(
            meteorSkill, new BattleCommand(), new BattleUnitState(), new BattleState());
        _test.False(result.Allowed, "Missing profile must fail closed.");
        _test.Eq(result.PlayerMessage, "该禁咒配置未通过校验，暂时无法施放。",
            "Fail-closed player message must stay stable.");
    }

    private static string FormatDiagnostics(IEnumerable<ContentJsonDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(value =>
            $"{value.RuleId}:{value.JsonPointer}:{value.Message}"));
}

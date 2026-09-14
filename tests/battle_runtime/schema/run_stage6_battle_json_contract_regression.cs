using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;

public partial class run_stage6_battle_json_contract_regression : LifecycleTestSceneTree
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        if (OS.GetCmdlineUserArgs().Contains("--write-schemas", StringComparer.Ordinal))
        {
            WriteSchemas();
            Quit(0);
            return;
        }
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        AssertSchemasAreByteExact();
        AssertEncounterClosedKinds();
        AssertBarrierIdProjection();
        AssertSpecialProfileIdProjection();
        RequestTestExit(_test.Finish("Stage 6 battle JSON contract regression"));
    }

    private void AssertSchemasAreByteExact()
    {
        var exporter = new ContentJsonSchemaExporter();
        foreach (ContentJsonSchemaDomainRegistration registration in Registrations())
        {
            string generated = exporter.Export(registration);
            string path = ProjectSettings.GlobalizePath(registration.TrackedSchemaPath);
            _test.True(File.Exists(path), $"Tracked schema must exist: {registration.DomainId}.");
            if (!File.Exists(path))
                continue;
            byte[] tracked = File.ReadAllBytes(path);
            _test.True(StrictUtf8.GetBytes(generated).SequenceEqual(tracked),
                $"Tracked schema must be byte-exact: {registration.DomainId}.");
        }
    }

    private void AssertEncounterClosedKinds()
    {
        var reader = new GodotContentJsonSourceReader();
        ContentImportBatch<BattleEncounterImportModel> batch =
            BattleEncounterJsonAuthoringDomains
                .CreateDescriptor(BattleEncounterJsonDomain.Directory, reader)
                .Import();
        _test.Eq(batch.Diagnostics.Count, 0,
            $"Battle encounters must import strictly: {FormatDiagnostics(batch.Diagnostics)}");
        _test.Eq(batch.Entries.Count, 11, "Battle encounter JSON entry parity must stay 11.");
        BattleObjectiveKind[] kinds = batch.Entries
            .Select(entry => entry.Import.Objective.Kind)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        _test.Eq(kinds.Length, 9, "Battle encounter JSON must exercise all nine objective kinds.");
        _test.False(kinds.Contains(BattleObjectiveKind.Unknown),
            "Closed objective projection must never publish Unknown.");
    }

    private void AssertBarrierIdProjection()
    {
        using var registry = new BarrierContentRegistry();
        _test.Eq(registry.ValidateTyped().Count, 0,
            $"Barrier JSON registry must validate: {string.Join(" | ", registry.ValidateTyped())}");
        IReadOnlyDictionary<StringName, BarrierLayerDefinition> layers =
            registry.GetLayerDefsTyped();
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profiles =
            registry.GetProfileDefsTyped();
        _test.Eq(layers.Count, 7, "Barrier layer definition parity must stay 7.");
        _test.Eq(profiles.Count, 8, "Barrier profile definition parity must stay 8.");
        _test.True(profiles.TryGetValue("prismatic_sphere", out BarrierProfileDefinition sphere),
            "Prismatic sphere must project by profile ID.");
        if (sphere == null)
            return;
        _test.True(sphere.Layers.All(layer =>
                layers.TryGetValue(layer.LayerId, out BarrierLayerDefinition canonical)
                && ReferenceEquals(layer, canonical)),
            "Barrier profiles must resolve canonical layer definitions by layer ID.");
    }

    private void AssertSpecialProfileIdProjection()
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skills =
            GameSessionTestFactory.GetProcessSnapshot().Skills;
        using var registry = new BattleSpecialProfileRegistry();
        registry.Rebuild(skills);
        _test.Eq(registry.ValidateTyped().Count, 0,
            $"Special-profile JSON registry must validate: {string.Join(" | ", registry.ValidateTyped())}");
        _test.Eq(registry.GetManifestsTyped().Count, 1,
            "Special-profile manifest definition parity must stay 1.");
        _test.True(registry.BuildRuntimeProfileView().TryGetMeteorSwarmProfile(
                "meteor_swarm", out MeteorSwarmProfileData profile),
            "Manifest/profile graph must resolve by profile ID.");
        _test.Eq(profile?.profile_id ?? new StringName(""), new StringName("meteor_swarm"),
            "Runtime profile must preserve profile ID without ResourcePath.");
    }

    private static IReadOnlyList<ContentJsonSchemaDomainRegistration> Registrations() =>
        BattleEncounterJsonAuthoringDomains.SchemaRegistrations
            .Concat(BarrierJsonAuthoringDomains.SchemaRegistrations)
            .Concat(BattleSpecialProfileJsonAuthoringDomains.SchemaRegistrations)
            .ToArray();

    private static void WriteSchemas()
    {
        var exporter = new ContentJsonSchemaExporter();
        foreach (ContentJsonSchemaDomainRegistration registration in Registrations())
        {
            string path = ProjectSettings.GlobalizePath(registration.TrackedSchemaPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, exporter.Export(registration), StrictUtf8);
        }
    }

    private static string FormatDiagnostics(IEnumerable<ContentJsonDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(value =>
            $"{value.RuleId}:{value.JsonPointer}:{value.Message}"));
}

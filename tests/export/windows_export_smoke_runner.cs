using System;
using System.Text.Json;
using Godot;

public partial class windows_export_smoke_runner : Node
{
    private const string ProbePath =
        "res://data/configs/engine_assets/export_smoke_probe.json";
    private const string MissingProbePath =
        "res://data/configs/engine_assets/export_smoke_missing.json";
    private const string ExpectedProbeId = "engine_asset_export_smoke_v1";

    private static readonly StringName TextureAssetId =
        "battle.terrain.marker_preview";
    private static readonly StringName SkillIconAssetId =
        "archer_aimed_shot";
    private static readonly StringName ItemIconAssetId =
        "ui.item.icon.default";
    private static readonly StringName SceneAssetId =
        "battle.board.prop_scene";
    private static readonly StringName ShaderAssetId =
        "ui.skill_icon.grayscale_shader";

    public override void _Ready()
    {
        string smokeCase = ReadSmokeCase();
        try
        {
            if (OS.HasFeature("editor"))
            {
                throw new InvalidOperationException(
                    "Windows export smoke must run from an export template, not the editor."
                );
            }

            AssertTestOnlyStartupOverrides();
            RunCase(smokeCase);
            ConsoleProcessOutput.WriteStandard(
                $"WINDOWS_EXPORT_SMOKE_PASS case={smokeCase}"
            );
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure(
                $"WINDOWS_EXPORT_SMOKE_FAIL case={smokeCase} "
                    + $"{exception.GetType().Name}: {exception.Message}"
            );
            GetTree().Quit(1);
        }
    }

    private void AssertTestOnlyStartupOverrides()
    {
        Node root = GetTree().Root;
        if (
            root.GetNodeOrNull<windows_export_smoke_noop_autoload>(
                "ApplicationLifetimeCoordinator"
            )
                == null
            || root.GetNodeOrNull<windows_export_smoke_noop_autoload>("GameSession")
                == null
        )
        {
            throw new InvalidOperationException(
                "Temporary exported-project startup overrides were not applied."
            );
        }
    }

    private static void RunCase(string smokeCase)
    {
        switch (smokeCase)
        {
            case "success":
                AssertProbePackaged(ProbePath);
                AssertProductionCatalogPackaged();
                AssertStageFourJsonPackaged();
                AssertWorldJsonCatalogPackaged();
                return;
            case "missing_file":
                AssertProbePackaged(MissingProbePath);
                throw new InvalidOperationException(
                    "Missing-file smoke case unexpectedly found its probe."
                );
            case "missing_entry":
                WithProductionCatalog(resolver =>
                    resolver.ResolveContentAssetBorrowed<PackedScene>(
                        "export_smoke.missing"
                    )
                );
                throw new InvalidOperationException(
                    "Missing-entry smoke case unexpectedly resolved an asset."
                );
            case "type_mismatch":
                WithProductionCatalog(resolver =>
                    resolver.ResolveContentAssetBorrowed<Texture2D>(SceneAssetId)
                );
                throw new InvalidOperationException(
                    "Type-mismatch smoke case unexpectedly resolved an asset."
                );
            default:
                throw new ArgumentException(
                    $"Unknown Windows export smoke case: {smokeCase}."
                );
        }
    }

    private static void AssertProbePackaged(string path)
    {
        using Godot.FileAccess file = Godot.FileAccess.Open(
            path,
            Godot.FileAccess.ModeFlags.Read
        );
        if (file == null)
        {
            throw new InvalidOperationException(
                $"Packaged JSON probe is unavailable through FileAccess: {path}."
            );
        }

        using JsonDocument document = JsonDocument.Parse(file.GetAsText(skipCr: false));
        JsonElement root = document.RootElement;
        if (
            !root.TryGetProperty("probe_id", out JsonElement probeId)
            || probeId.ValueKind != JsonValueKind.String
            || !string.Equals(
                probeId.GetString(),
                ExpectedProbeId,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                $"Packaged JSON probe has an unexpected probe_id: {path}."
            );
        }
    }

    private static void AssertProductionCatalogPackaged()
    {
        WithProductionCatalog(resolver =>
        {
            Texture2D texture = resolver.ResolveContentAssetBorrowed<Texture2D>(
                TextureAssetId
            );
            Texture2D skillIcon = resolver.ResolveContentAssetBorrowed<Texture2D>(
                SkillIconAssetId
            );
            Texture2D itemIcon = resolver.ResolveContentAssetBorrowed<Texture2D>(
                ItemIconAssetId
            );
            PackedScene scene = resolver.ResolveContentAssetBorrowed<PackedScene>(
                SceneAssetId
            );
            Shader shader = resolver.ResolveContentAssetBorrowed<Shader>(ShaderAssetId);
            if (
                texture == null
                || skillIcon == null
                || itemIcon == null
                || scene == null
                || shader == null
            )
            {
                throw new InvalidOperationException(
                    "Production catalog returned a missing typed borrowed asset."
                );
            }
            if (resolver.PublishedAssetCount != 30)
            {
                throw new InvalidOperationException(
                    "Production catalog published an unexpected asset count: "
                        + resolver.PublishedAssetCount
                        + "."
                );
            }
        });
    }

    private static void AssertStageFourJsonPackaged()
    {
        var reader = new GodotContentJsonSourceReader();
        AssertImportBatch(
            "items",
            129,
            ItemContentJsonAuthoringDomain.CreateImportDescriptor(
                ItemContentJsonAuthoringDomain.ProductionDirectory,
                reader
            ).Import()
        );
        AssertImportBatch(
            "traits",
            239,
            TraitContentJsonAuthoringDomain.CreateImportDescriptor(
                TraitContentJsonAuthoringDomain.ProductionDirectory,
                reader
            ).Import()
        );
        AssertImportBatch(
            "equipment_abilities",
            55,
            EquipmentAbilityContentJsonAuthoringDomain.CreateImportDescriptor(
                EquipmentAbilityContentJsonAuthoringDomain.ProductionDirectory,
                reader
            ).Import()
        );
        AssertImportBatch(
            "gear_sets",
            1,
            GearSetContentJsonAuthoringDomain.CreateImportDescriptor(
                GearSetContentJsonAuthoringDomain.ProductionDirectory,
                reader
            ).Import()
        );
        AssertImportBatch(
            "recipes",
            4,
            RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                RecipeContentJsonAuthoringDomain.ProductionDirectory,
                reader
            ).Import()
        );
    }

    private static void AssertImportBatch<TImport>(
        string domain,
        int expectedEntryCount,
        ContentImportBatch<TImport> batch
    )
    {
        if (batch.HasErrors || batch.Entries.Count != expectedEntryCount)
        {
            throw new InvalidOperationException(
                $"Packaged {domain} JSON import failed: entries={batch.Entries.Count}, diagnostics={batch.Diagnostics.Count}."
            );
        }
    }

    private static void AssertWorldJsonCatalogPackaged()
    {
        var registry = new WorldContentRegistry();
        registry.Rebuild();
        if (registry.GetValidationErrors().Count != 0)
        {
            throw new InvalidOperationException(
                "Packaged world JSON registry reported validation errors: "
                    + string.Join(" | ", registry.GetValidationErrors())
            );
        }

        var presets = registry.GetPresets();
        var generations = registry.GetGenerations();
        if (presets.Count != 5 || generations.Count != 6)
        {
            throw new InvalidOperationException(
                $"Packaged world JSON registry count mismatch: presets={presets.Count}, generations={generations.Count}."
            );
        }
        if (
            !presets.TryGetValue("ashen_intersection", out WorldPresetDefinition preset)
            || preset.GenerationId != "ashen_intersection"
            || !generations.TryGetValue(
                preset.GenerationId,
                out WorldGenerationDefinition rootGeneration
            )
            || rootGeneration.MountedSubmaps.Count != 1
            || rootGeneration.MountedSubmaps[0].WorldGenerationId != "ashen_wastes"
        )
        {
            throw new InvalidOperationException(
                "Packaged world preset/root/mounted generation stable-ID closure is invalid."
            );
        }
        if (
            !generations.TryGetValue("test", out WorldGenerationDefinition sharedGeneration)
            || sharedGeneration.SharedContentId != "main_world_defaults"
            || sharedGeneration.EffectiveSettlementLibrary.Count == 0
            || sharedGeneration.EffectiveFacilityLibrary.Count == 0
            || sharedGeneration.EffectiveWildSpawnRules.Count == 0
        )
        {
            throw new InvalidOperationException(
                "Packaged world_shared JSON did not project into the test generation."
            );
        }
    }

    private static void WithProductionCatalog(Action<EngineAssetResolver> action)
    {
        var resolver = new EngineAssetResolver();
        try
        {
            EngineAssetCatalogBootstrap.LoadAndPublish(resolver);
            action(resolver);
        }
        finally
        {
            resolver.Dispose();
        }
    }

    private static string ReadSmokeCase()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            const string prefix = "--case=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
                return argument[prefix.Length..];
        }

        return "success";
    }
}

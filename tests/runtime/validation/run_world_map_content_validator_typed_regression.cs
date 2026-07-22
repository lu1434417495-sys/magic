using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;

public partial class run_world_map_content_validator_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestOfficialWorldPresetsProjectAndValidateTyped();
        TestProjectedInvalidGenerationDefinitionReportsErrors();
        TestInjectedDefaultContentProjectsAndValidatesTyped();
        TestTypedGenerationValidationUsesExactCatalogIds();
        TestSiblingMountedSubmapsMayReuseConfigPath();
        TestWorldDefinitionsAreRecursiveReadOnlyAndCanonical();
        TestWorldDefinitionProjectionRejectsCyclesAndNullEntries();
        TestWorldDefinitionTypesContainNoGodotObjectGraph();

        RequestTestExit(_test.Finish("World map content validator typed regression"));
    }

    private void TestOfficialWorldPresetsProjectAndValidateTyped()
    {
        using TestContentResourceLoader loader = new();
        WorldMapContentValidator validator = new();
        ContentSnapshot contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();

        HashSet<StringName> battleEncounterIds = new(contentSnapshot.BattleEncounters.Keys);
        List<string> typedErrors = ValidateOfficialWorldPresets(
            loader,
            validator,
            battleEncounterIds
        );

        _test.Eq(typedErrors.Count, 0, $"正式 world preset typed boundary 不应报错: {FormatErrors(typedErrors)}");
    }

    private void TestProjectedInvalidGenerationDefinitionReportsErrors()
    {
        using TestContentResourceLoader loader = new();
        WorldMapContentValidator validator = new();
        ContentSnapshot contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();

        using WorldMapGenerationConfig config = BuildInvalidGenerationConfig();
        WorldGenerationDefinition definition = ProjectSyntheticGeneration(
            "res://synthetic/typed_invalid_world_generation_config.tres",
            config,
            loader
        );
        HashSet<StringName> battleEncounterIds = new(contentSnapshot.BattleEncounters.Keys);

        List<string> typedErrors = validator.ValidateGenerationConfigTyped(
            definition,
            "typed_invalid_world_generation_config",
            battleEncounterIds
        );

        _test.True(
            typedErrors.Count > 0,
            $"typed generation config 非法 fixture 应产生 validation 错误。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestInjectedDefaultContentProjectsAndValidatesTyped()
    {
        using TestContentResourceLoader loader = new();
        WorldMapContentValidator validator = new();
        ContentSnapshot contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();

        using WorldMapGenerationConfig config = new()
        {
            inject_default_main_world_content = true,
            world_size_in_chunks = new Vector2I(4, 4),
            chunk_size = new Vector2I(8, 8),
        };
        HashSet<StringName> battleEncounterIds = new(contentSnapshot.BattleEncounters.Keys);
        WorldGenerationDefinition definition = ProjectSyntheticGeneration(
            "res://synthetic/typed_default_injected_world_generation_config.tres",
            config,
            loader
        );

        List<string> typedErrors = validator.ValidateGenerationConfigTyped(
            definition,
            "typed_default_injected_world_generation_config",
            battleEncounterIds
        );

        _test.Eq(
            typedErrors.Count,
            0,
            $"default injected world content 的 typed generation 校验不应报错: {FormatErrors(typedErrors)}"
        );
    }

    private void TestTypedGenerationValidationUsesExactCatalogIds()
    {
        using TestContentResourceLoader loader = new();
        WorldMapContentValidator validator = new();

        using WorldMapGenerationConfig config = BuildCatalogBoundaryGenerationConfig(
            "string_name_battle_encounter"
        );
        WorldGenerationDefinition definition = ProjectSyntheticGeneration(
            "res://synthetic/string_name_catalog_boundary.tres",
            config,
            loader
        );

        List<string> unrelatedIdErrors = validator.ValidateGenerationConfigTyped(
            definition,
            "string_key_catalog_boundary",
            new HashSet<StringName> { "unrelated_battle_encounter" }
        );
        _test.True(
            unrelatedIdErrors.Count >= 1,
            $"typed generation config 校验应拒绝不匹配的 catalog id。 errors={FormatErrors(unrelatedIdErrors)}"
        );

        List<string> exactIdErrors = validator.ValidateGenerationConfigTyped(
            definition,
            "string_name_key_catalog_boundary",
            new HashSet<StringName> { "string_name_battle_encounter" }
        );
        _test.Eq(
            exactIdErrors.Count,
            0,
            $"typed generation config 校验应接受精确 StringName catalog id。 errors={FormatErrors(exactIdErrors)}"
        );
    }

    private void TestSiblingMountedSubmapsMayReuseConfigPath()
    {
        const string rootPath = "res://synthetic/sibling_duplicate_submap_path.tres";
        const string submapPath = "user://world_map_duplicate_submap_config_regression.tres";
        CleanupFile(submapPath);

        using WorldMapGenerationConfig childConfig = BuildMinimalGenerationConfig();
        _test.Eq(
            ResourceSaver.Save(childConfig, submapPath),
            Error.Ok,
            "应能写入 sibling submap 复用路径测试资源。"
        );

        using WorldMapGenerationConfig rootConfig = BuildMinimalGenerationConfig();
        rootConfig.mounted_submaps = new Godot.Collections.Array<Resource>
        {
            new MountedSubmapConfig
            {
                submap_id = "first_submap",
                generation_config_path = submapPath,
            },
            new MountedSubmapConfig
            {
                submap_id = "second_submap",
                generation_config_path = submapPath,
            },
        };

        using TestContentResourceLoader loader = new();
        WorldGenerationDefinition definition = ProjectSyntheticGeneration(
            rootPath,
            rootConfig,
            loader
        );
        WorldMapContentValidator validator = new();
        List<string> errors = validator.ValidateGenerationConfigTyped(
            definition,
            "sibling_duplicate_submap_path",
            new HashSet<StringName>()
        );
        _test.Eq(
            errors.Count,
            0,
            $"兄弟 submap 复用同一 generation_config_path 不应被误报递归: {FormatErrors(errors)}"
        );

        CleanupFile(submapPath);
    }

    private void TestWorldDefinitionsAreRecursiveReadOnlyAndCanonical()
    {
        const string rootPath = "res://synthetic/../synthetic/world_root.tres";
        const string childPath = "res://synthetic/world_child.tres";

        FacilityNpcConfig npc = new()
        {
            npc_id = "npc_keeper",
            display_name = "Keeper",
            service_type = "rest_service",
            interaction_script_id = "service_rest_full",
            local_slot_id = "core",
        };
        FacilityConfig facility = new()
        {
            facility_id = "inn",
            display_name = "Inn",
            category = "service",
            interaction_type = "rest_service",
            min_settlement_tier = 0,
            allowed_slot_tags = new Godot.Collections.Array<string> { "core" },
            bound_service_npcs = new Godot.Collections.Array<Resource> { npc },
        };
        FacilitySlotConfig slot = new()
        {
            slot_id = "core",
            slot_tag = "core",
            local_coord = Vector2I.Zero,
            required = true,
        };
        WeightedFacilityEntry optionalFacility = new() { facility_id = "inn", weight = 2 };
        SettlementConfig settlement = new()
        {
            settlement_id = "village",
            display_name = "Village",
            tier = (int)SettlementTierKind.Village,
            facility_slots = new Godot.Collections.Array<Resource> { slot },
            guaranteed_facility_ids = new Godot.Collections.Array<string> { "inn" },
            optional_facility_pool = new Godot.Collections.Array<Resource> { optionalFacility },
            max_optional_facilities = 1,
        };
        SettlementDistributionRule distribution = new()
        {
            settlement_id = "village",
            faction_id = "neutral",
            preferred_origin = Vector2I.Zero,
        };
        WildSpawnRule wildSpawn = new()
        {
            region_tag = "north",
            monster_name = "Wolf",
            encounter_profile_id = "synthetic_wolf_encounter",
            density_per_chunk = 1,
            min_distance_to_settlement = 0,
            vision_range = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I> { Vector2I.Zero },
        };
        WorldMapGenerationConfig child = new()
        {
            world_size_in_chunks = Vector2I.One,
            chunk_size = new Vector2I(4, 4),
        };
        MountedSubmapConfig firstSubmap = new()
        {
            submap_id = "child_a",
            display_name = "Child A",
            generation_config_path = childPath,
        };
        MountedSubmapConfig secondSubmap = new()
        {
            submap_id = "child_b",
            display_name = "Child B",
            generation_config_path = "res://synthetic/./world_child.tres",
        };
        WorldEventConfig worldEvent = new()
        {
            event_id = "enter_child",
            display_name = "Enter Child",
            world_coord = Vector2I.Zero,
            event_type = "enter_submap",
            target_submap_id = "child_a",
        };
        WorldMapGenerationConfig root = new()
        {
            world_size_in_chunks = new Vector2I(2, 2),
            chunk_size = new Vector2I(4, 4),
            inject_default_main_world_content = true,
            settlement_library = new Godot.Collections.Array<Resource> { settlement },
            facility_library = new Godot.Collections.Array<Resource> { facility },
            settlement_distribution = new Godot.Collections.Array<Resource> { distribution },
            wild_monster_distribution = new Godot.Collections.Array<Resource> { wildSpawn },
            mounted_submaps = new Godot.Collections.Array<Resource>
            {
                firstSubmap,
                secondSubmap,
            },
            world_events = new Godot.Collections.Array<Resource> { worldEvent },
        };

        TestResourceOwnership.Own(root, "world-definition-root");
        TestResourceOwnership.Own(child, "world-definition-child");
        using TestContentResourceLoader loader = new();
        loader.RegisterCanonical(childPath, child);
        AddDefaultResources(loader);

        WorldGenerationDefinition definition = root.ToDefinition(rootPath, loader);
        _test.Eq(
            definition.CanonicalPath,
            "res://synthetic/world_root.tres",
            "world definition should expose the shared canonical path normalization"
        );
        _test.Eq(definition.EffectiveSettlementLibrary.Count, 1, "local settlement should project");
        _test.Eq(definition.EffectiveFacilityLibrary.Count, 1, "local facility should project");
        _test.Eq(definition.EffectiveWildSpawnRules.Count, 1, "local wild rule should project");
        _test.Eq(definition.SettlementNamePools.Count, 5, "all five default name pools should project");
        _test.True(
            ReferenceEquals(
                definition.MountedSubmaps[0].Generation,
                definition.MountedSubmaps[1].Generation
            ),
            "sibling aliases should reuse one canonical projected child definition"
        );
        _test.Eq(
            definition.EffectiveFacilityLibrary[0].BoundServiceNpcs[0].DisplayName,
            "Keeper",
            "nested NPC fields should project"
        );

        npc.display_name = "Mutated";
        root.facility_library.Clear();
        _test.Eq(
            definition.EffectiveFacilityLibrary[0].BoundServiceNpcs[0].DisplayName,
            "Keeper",
            "definition should not alias nested authoring resources"
        );
        _test.Eq(
            definition.EffectiveFacilityLibrary.Count,
            1,
            "definition should not alias authoring arrays"
        );

        bool readOnlyRejected = false;
        try
        {
            ((IList<FacilityDefinition>)definition.EffectiveFacilityLibrary).Add(
                definition.EffectiveFacilityLibrary[0]
            );
        }
        catch (NotSupportedException)
        {
            readOnlyRejected = true;
        }
        _test.True(readOnlyRejected, "effective definition lists must reject mutation");

        WorldMapContentValidator validator = new();
        List<string> errors = validator.ValidateGenerationConfigTyped(
            definition,
            definition.CanonicalPath,
            new HashSet<StringName> { "synthetic_wolf_encounter" }
        );
        _test.Eq(
            errors.Count,
            0,
            $"projected definition graph should pass typed validation: {FormatErrors(errors)}"
        );
    }

    private void TestWorldDefinitionProjectionRejectsCyclesAndNullEntries()
    {
        const string rootPath = "res://synthetic/cycle_root.tres";
        const string childPath = "res://synthetic/cycle_child.tres";
        WorldMapGenerationConfig root = BuildMinimalGenerationConfig();
        WorldMapGenerationConfig child = BuildMinimalGenerationConfig();
        root.mounted_submaps.Add(
            new MountedSubmapConfig
            {
                submap_id = "child",
                generation_config_path = childPath,
            }
        );
        child.mounted_submaps.Add(
            new MountedSubmapConfig
            {
                submap_id = "root",
                generation_config_path = rootPath,
            }
        );
        TestResourceOwnership.Own(root, "world-cycle-root");
        TestResourceOwnership.Own(child, "world-cycle-child");
        using TestContentResourceLoader cycleLoader = new();
        cycleLoader.RegisterCanonical(rootPath, root);
        cycleLoader.RegisterCanonical(childPath, child);
        AssertInvalidData(
            () => root.ToDefinition(rootPath, cycleLoader),
            "cycle_root.tres -> res://synthetic/cycle_child.tres -> res://synthetic/cycle_root.tres",
            "recursive mounted world definitions must report the canonical path cycle"
        );

        WorldMapGenerationConfig nullNested = BuildMinimalGenerationConfig();
        nullNested.settlement_library.Add(null);
        TestResourceOwnership.Own(nullNested, "world-null-nested");
        using TestContentResourceLoader nullLoader = new();
        AssertInvalidData(
            () => nullNested.ToDefinition(
                "res://synthetic/null_nested.tres",
                nullLoader
            ),
            "settlement_library[0]",
            "null nested world resource must fail with its exact projection path"
        );
    }

    private void TestWorldDefinitionTypesContainNoGodotObjectGraph()
    {
        Type[] definitionTypes =
        {
            typeof(WorldGenerationDefinition),
            typeof(SettlementDefinition),
            typeof(SettlementDistributionDefinition),
            typeof(WeightedFacilityDefinition),
            typeof(FacilityDefinition),
            typeof(FacilityNpcDefinition),
            typeof(FacilitySlotDefinition),
            typeof(WildSpawnRuleDefinition),
            typeof(MountedSubmapDefinition),
            typeof(WorldEventDefinition),
            typeof(WorldMapSettlementBundleDefinition),
            typeof(WorldMapSettlementNamePoolDefinition),
            typeof(WorldMapWildSpawnBundleDefinition),
        };
        foreach (Type definitionType in definitionTypes)
        {
            foreach (
                PropertyInfo property in definitionType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public
                )
            )
            {
                foreach (Type inspected in EnumerateTypeGraph(property.PropertyType))
                {
                    _test.True(
                        !typeof(GodotObject).IsAssignableFrom(inspected),
                        $"{definitionType.Name}.{property.Name} must not retain GodotObject {inspected.FullName}."
                    );
                    _test.True(
                        inspected.FullName == null
                            || !inspected.FullName.StartsWith(
                                "Godot.Collections.",
                                StringComparison.Ordinal
                            ),
                        $"{definitionType.Name}.{property.Name} must not retain Godot collection {inspected.FullName}."
                    );
                }
            }
        }
    }

    private static List<string> ValidateOfficialWorldPresets(
        TestContentResourceLoader loader,
        WorldMapContentValidator validator,
        IReadOnlyCollection<StringName> battleEncounterIds
    )
    {
        var errors = new List<string>();
        foreach (WorldPresetRegistry.WorldPresetInfo preset in WorldPresetRegistry.ListPresetsTyped())
        {
            string canonicalPath = ContentPathCanonicalizer.Canonicalize(
                preset.GenerationConfigPath
            );
            WorldMapGenerationConfig source = loader.LoadCanonical<WorldMapGenerationConfig>(
                canonicalPath
            );
            WorldGenerationDefinition definition = source.ToDefinition(canonicalPath, loader);
            errors.AddRange(
                validator.ValidateGenerationConfigTyped(
                    definition,
                    canonicalPath,
                    battleEncounterIds
                )
            );
        }
        return errors;
    }

    private static WorldGenerationDefinition ProjectSyntheticGeneration(
        string resourcePath,
        WorldMapGenerationConfig source,
        TestContentResourceLoader loader
    )
    {
        loader.RegisterCanonical(resourcePath, source);
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        WorldMapGenerationConfig canonicalSource =
            loader.LoadCanonical<WorldMapGenerationConfig>(canonicalPath);
        return canonicalSource.ToDefinition(canonicalPath, loader);
    }

    private static WorldMapGenerationConfig BuildInvalidGenerationConfig()
    {
        WildSpawnRule missingWildRule = new()
        {
            region_tag = "invalid_wilds",
            encounter_profile_id = "missing_battle_encounter",
            density_per_chunk = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I> { new Vector2I(0, 0) },
        };

        return new WorldMapGenerationConfig
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            wild_monster_distribution = new Godot.Collections.Array<Resource> { missingWildRule },
        };
    }

    private static void AddDefaultResources(TestContentResourceLoader loader)
    {
        WorldMapSettlementBundle settlementBundle = TestResourceOwnership.Own(
            new WorldMapSettlementBundle(),
            "world-default-settlement-bundle"
        );
        WorldMapWildSpawnBundle wildSpawnBundle = TestResourceOwnership.Own(
            new WorldMapWildSpawnBundle(),
            "world-default-wild-spawn-bundle"
        );
        loader.RegisterCanonical(
            WorldGenerationDefinition.DefaultMainWorldSettlementBundlePath,
            settlementBundle
        );
        loader.RegisterCanonical(
            WorldGenerationDefinition.DefaultMainWorldWildSpawnBundlePath,
            wildSpawnBundle
        );
        string[] namePoolPaths =
        {
            WorldGenerationDefinition.DefaultMainWorldSettlementNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldTownNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldCityNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldCapitalNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldMetropolisNamePoolPath,
        };
        foreach (string path in namePoolPaths)
        {
            WorldMapSettlementNamePool namePool = TestResourceOwnership.Own(
                new WorldMapSettlementNamePool
                {
                    settlement_display_names = new Godot.Collections.Array<string>
                    {
                        $"Name {path}",
                    },
                },
                $"world-name-pool:{path}"
            );
            loader.RegisterCanonical(path, namePool);
        }
    }

    private void AssertInvalidData(Action action, string pathFragment, string message)
    {
        try
        {
            action();
            _test.Fail($"{message}: expected InvalidDataException.");
        }
        catch (InvalidDataException exception)
        {
            _test.True(
                exception.Message.Contains(pathFragment, StringComparison.Ordinal),
                $"{message}: expected '{pathFragment}', got '{exception.Message}'."
            );
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"{message}: expected InvalidDataException, got {exception.GetType().Name}."
            );
        }
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type type = pending.Pop();
            if (type == null || !seen.Add(type))
                continue;
            yield return type;
            if (type.HasElementType)
                pending.Push(type.GetElementType());
            foreach (Type argument in type.GetGenericArguments())
                pending.Push(argument);
        }
    }

    private static WorldMapGenerationConfig BuildCatalogBoundaryGenerationConfig(
        StringName battleEncounterId
    )
    {
        WildSpawnRule wildRule = new()
        {
            region_tag = "catalog_boundary",
            encounter_profile_id = battleEncounterId,
            density_per_chunk = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I> { new Vector2I(0, 0) },
        };
        return new WorldMapGenerationConfig
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            wild_monster_distribution = new Godot.Collections.Array<Resource> { wildRule },
        };
    }

    private static WorldMapGenerationConfig BuildMinimalGenerationConfig()
    {
        return new WorldMapGenerationConfig
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
        };
    }

    private static void CleanupFile(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
            return;
        string absolutePath = ProjectSettings.GlobalizePath(virtualPath);
        if (Godot.FileAccess.FileExists(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error ?? "");
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

}

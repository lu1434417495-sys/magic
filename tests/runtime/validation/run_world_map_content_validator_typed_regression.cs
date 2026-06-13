using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_world_map_content_validator_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialWorldPresetsTypedBoundaryMatchesPublicBoundary();
        TestGenerationConfigTypedBoundaryMatchesPublicBoundary();
        TestInjectedDefaultContentTypedBoundaryMatchesPublicBoundary();
        TestPublicGenerationConfigRejectsStringKeyCatalogIds();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("World map content validator typed regression"));
    }

    private void TestOfficialWorldPresetsTypedBoundaryMatchesPublicBoundary()
    {
        using EnemyContentRegistry enemyRegistry = new();
        using WorldMapContentValidator validator = new();

        HashSet<StringName> enemyTemplateIds = new(enemyRegistry.GetEnemyTemplatesTyped().Keys);
        HashSet<StringName> wildEncounterRosterIds = new(
            enemyRegistry.GetWildEncounterRostersTyped().Keys
        );
        List<string> typedErrors = validator.ValidateWorldPresetsTyped(
            enemyTemplateIds,
            wildEncounterRosterIds
        );
        GStringArray publicErrors = validator.ValidateWorldPresets(
            ProjectTemplates(enemyRegistry.GetEnemyTemplatesTyped()),
            ProjectRosters(enemyRegistry.GetWildEncounterRostersTyped())
        );

        _test.Eq(typedErrors.Count, 0, $"正式 world preset typed boundary 不应报错: {FormatErrors(typedErrors)}");
        _test.Eq(publicErrors.Count, 0, $"正式 world preset public boundary 不应报错: {FormatErrors(publicErrors)}");
    }

    private void TestGenerationConfigTypedBoundaryMatchesPublicBoundary()
    {
        using EnemyContentRegistry enemyRegistry = new();
        using WorldMapContentValidator validator = new();

        WorldMapGenerationConfig config = BuildInvalidGenerationConfig();
        GDictionary enemyTemplates = ProjectTemplates(enemyRegistry.GetEnemyTemplatesTyped());
        GDictionary wildEncounterRosters = ProjectRosters(enemyRegistry.GetWildEncounterRostersTyped());
        HashSet<StringName> enemyTemplateIds = new(enemyRegistry.GetEnemyTemplatesTyped().Keys);
        HashSet<StringName> wildEncounterRosterIds = new(
            enemyRegistry.GetWildEncounterRostersTyped().Keys
        );

        List<string> typedErrors = validator.ValidateGenerationConfigTyped(
            config,
            "typed_invalid_world_generation_config",
            enemyTemplateIds,
            wildEncounterRosterIds
        );
        GStringArray publicErrors = validator.ValidateGenerationConfig(
            config,
            "typed_invalid_world_generation_config",
            enemyTemplates,
            wildEncounterRosters
        );

        _test.True(
            typedErrors.Count > 0,
            $"typed generation config 非法 fixture 应产生 validation 错误。 errors={FormatErrors(typedErrors)}"
        );
        _test.Eq(
            typedErrors.Count,
            publicErrors.Count,
            "typed/public generation config 边界的错误数量应保持一致。"
        );
    }

    private void TestInjectedDefaultContentTypedBoundaryMatchesPublicBoundary()
    {
        using EnemyContentRegistry enemyRegistry = new();
        using WorldMapContentValidator validator = new();

        WorldMapGenerationConfig config = new()
        {
            inject_default_main_world_content = true,
            world_size_in_chunks = new Vector2I(4, 4),
            chunk_size = new Vector2I(8, 8),
        };
        HashSet<StringName> enemyTemplateIds = new(enemyRegistry.GetEnemyTemplatesTyped().Keys);
        HashSet<StringName> wildEncounterRosterIds = new(
            enemyRegistry.GetWildEncounterRostersTyped().Keys
        );

        List<string> typedErrors = validator.ValidateGenerationConfigTyped(
            config,
            "typed_default_injected_world_generation_config",
            enemyTemplateIds,
            wildEncounterRosterIds
        );
        GStringArray publicErrors = validator.ValidateGenerationConfig(
            config,
            "typed_default_injected_world_generation_config",
            ProjectTemplates(enemyRegistry.GetEnemyTemplatesTyped()),
            ProjectRosters(enemyRegistry.GetWildEncounterRostersTyped())
        );

        _test.Eq(
            typedErrors.Count,
            0,
            $"default injected world content 的 typed generation 校验不应报错: {FormatErrors(typedErrors)}"
        );
        _test.Eq(
            FormatErrors(typedErrors),
            FormatErrors(publicErrors),
            "default injected world content 的 typed/public generation 结果应保持一致。"
        );
    }

    private void TestPublicGenerationConfigRejectsStringKeyCatalogIds()
    {
        using WorldMapContentValidator validator = new();

        WorldMapGenerationConfig config = BuildCatalogBoundaryGenerationConfig(
            "string_key_enemy_template",
            "string_key_roster"
        );
        GDictionary enemyTemplates = new()
        {
            [new StringName("unrelated_enemy_template")] = new EnemyTemplateDef(),
            ["string_key_enemy_template"] = new EnemyTemplateDef(),
        };
        GDictionary wildEncounterRosters = new()
        {
            [new StringName("unrelated_roster")] = new WildEncounterRosterDef(),
            ["string_key_roster"] = new WildEncounterRosterDef(),
        };

        GStringArray stringKeyErrors = validator.ValidateGenerationConfig(
            config,
            "string_key_catalog_boundary",
            enemyTemplates,
            wildEncounterRosters
        );
        _test.True(
            stringKeyErrors.Count >= 2,
            $"public generation config 校验不应从 string-key catalog 恢复正式引用。 errors={FormatErrors(stringKeyErrors)}"
        );

        GStringArray stringNameKeyErrors = validator.ValidateGenerationConfig(
            config,
            "string_name_key_catalog_boundary",
            new GDictionary
            {
                [new StringName("string_key_enemy_template")] = new EnemyTemplateDef(),
            },
            new GDictionary
            {
                [new StringName("string_key_roster")] = new WildEncounterRosterDef(),
            }
        );
        _test.Eq(
            stringNameKeyErrors.Count,
            0,
            $"public generation config 校验应接受正式 StringName-key catalog。 errors={FormatErrors(stringNameKeyErrors)}"
        );
    }

    private static WorldMapGenerationConfig BuildInvalidGenerationConfig()
    {
        WildSpawnRule missingWildRule = new()
        {
            region_tag = "invalid_wilds",
            enemy_roster_template_id = "missing_enemy",
            encounter_profile_id = "missing_roster",
            density_per_chunk = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I> { new Vector2I(0, 0) },
        };

        MountedSubmapConfig declaredSubmap = new()
        {
            submap_id = "declared_submap",
            display_name = "Declared Submap",
            generation_config_path = "",
        };

        WorldEventConfig missingTargetEvent = new()
        {
            event_id = "missing_target_event",
            event_type = "enter_submap",
            target_submap_id = "never_declared_submap",
            world_coord = new Vector2I(0, 0),
        };

        return new WorldMapGenerationConfig
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            wild_monster_distribution = new Godot.Collections.Array<Resource> { missingWildRule },
            mounted_submaps = new Godot.Collections.Array<Resource> { declaredSubmap },
            world_events = new Godot.Collections.Array<Resource> { missingTargetEvent },
        };
    }

    private static WorldMapGenerationConfig BuildCatalogBoundaryGenerationConfig(
        StringName enemyTemplateId,
        StringName rosterId
    )
    {
        WildSpawnRule wildRule = new()
        {
            region_tag = "catalog_boundary",
            enemy_roster_template_id = enemyTemplateId,
            encounter_profile_id = rosterId,
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

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error ?? "");
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static GDictionary ProjectTemplates(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> templates
    )
    {
        GDictionary result = new();
        if (templates == null)
            return result;
        foreach ((StringName templateId, EnemyTemplateDef templateDef) in templates)
        {
            if (templateId == "" || templateDef == null)
                continue;
            result[templateId] = templateDef;
        }
        return result;
    }

    private static GDictionary ProjectRosters(
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> rosters
    )
    {
        GDictionary result = new();
        if (rosters == null)
            return result;
        foreach ((StringName rosterId, WildEncounterRosterDef rosterDef) in rosters)
        {
            if (rosterId == "" || rosterDef == null)
                continue;
            result[rosterId] = rosterDef;
        }
        return result;
    }

}

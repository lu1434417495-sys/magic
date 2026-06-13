using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_content_registry_typed_regression : SceneTree
{
    private const string OfficialSeedPath = "res://data/configs/enemies/enemy_content_seed.tres";
    private const string InvalidReferenceSeedPath =
        "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialTypedCatalogMatchesProjectedCatalog();
        TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Enemy content registry typed regression"));
    }

    private void TestOfficialTypedCatalogMatchesProjectedCatalog()
    {
        using EnemyContentRegistry registry = new();

        IReadOnlyDictionary<StringName, EnemyAiBrainDef> typedBrains = registry.GetEnemyAiBrainsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> typedTemplates = registry.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> typedRosters =
            registry.GetWildEncounterRostersTyped();
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GDictionary projectedBrains = ProjectBrains(typedBrains);
        GDictionary projectedTemplates = ProjectTemplates(typedTemplates);
        GDictionary projectedRosters = ProjectRosters(typedRosters);
        GStringArray errors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            errors.Count,
            "enemy registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(errors.Count, 0, $"正式 enemy content registry 不应报错: {FormatErrors(errors)}");
        _test.Eq(typedBrains.Count, 7, "正式 enemy brain typed catalog 应注册 7 个条目。");
        _test.Eq(typedTemplates.Count, 8, "正式 enemy template typed catalog 应注册 8 个条目。");
        _test.Eq(typedRosters.Count, 2, "正式 wild encounter roster typed catalog 应注册 2 个条目。");
        _test.Eq(
            projectedBrains.Count,
            typedBrains.Count,
            "brain 的 public Dictionary 投影数量应与 typed catalog 一致。"
        );
        _test.Eq(
            projectedTemplates.Count,
            typedTemplates.Count,
            "template 的 public Dictionary 投影数量应与 typed catalog 一致。"
        );
        _test.Eq(
            projectedRosters.Count,
            typedRosters.Count,
            "roster 的 public Dictionary 投影数量应与 typed catalog 一致。"
        );

        _test.True(
            typedBrains.ContainsKey("melee_aggressor"),
            "typed brain catalog 应保留 melee_aggressor。"
        );
        _test.True(
            typedTemplates.ContainsKey("wolf_raider"),
            "typed template catalog 应保留 wolf_raider。"
        );
        _test.True(
            typedRosters.ContainsKey("wolf_den"),
            "typed roster catalog 应保留 wolf_den。"
        );
    }

    private void TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed()
    {
        using EnemyContentRegistry registry = new();
        registry.ConfigureSeedResource(OfficialSeedPath, true, true);
        registry.ConfigureSeedResource(InvalidReferenceSeedPath, true, false);

        IReadOnlyDictionary<StringName, EnemyAiBrainDef> typedBrains = registry.GetEnemyAiBrainsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> typedTemplates = registry.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> typedRosters =
            registry.GetWildEncounterRostersTyped();
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray errors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            errors.Count,
            "invalid seed 下 enemy registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(typedBrains.Count, 0, "invalid roster fixture 不应残留上一轮官方 brain catalog。");
        _test.Eq(typedTemplates.Count, 0, "invalid roster fixture 不应残留上一轮官方 template catalog。");
        _test.Eq(typedRosters.Count, 1, "invalid roster fixture 应只注册自身 roster。");
        _test.True(
            typedRosters.ContainsKey("invalid_roster"),
            "invalid roster fixture 应保留 invalid_roster typed key。"
        );
        _test.True(
            errors.Count > 0,
            $"invalid roster fixture 应稳定报告缺失 template。 errors={FormatErrors(errors)}"
        );
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

    private static GDictionary ProjectBrains(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> brains
    )
    {
        GDictionary result = new();
        if (brains == null)
            return result;
        foreach ((StringName brainId, EnemyAiBrainDef brainDef) in brains)
        {
            if (brainId == "" || brainDef == null)
                continue;
            result[brainId] = brainDef;
        }
        return result;
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

using System.Collections.Generic;
using Godot;
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
        TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed();

        Quit(_test.Finish("Enemy content registry typed regression"));
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

}

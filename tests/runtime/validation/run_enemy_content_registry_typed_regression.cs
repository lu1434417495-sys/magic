using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_content_registry_typed_regression : LifecycleTestSceneTree
{
    private const string OfficialSeedPath = "res://data/configs/enemies/enemy_content_seed.tres";
    private const string InvalidReferenceSeedPath =
        "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestProcessSnapshotPublishesImmutablePlainEnemyDefinitions();
        TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed();

        RequestTestExit(_test.Finish("Enemy content registry typed regression"));
    }

    private void TestProcessSnapshotPublishesImmutablePlainEnemyDefinitions()
    {
        ProcessContentHost host = Root
            .GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator")
            .ContentHost;
        ContentSnapshot snapshot = host.GetSnapshot();

        _test.True(snapshot.EnemyTemplates.ContainsKey("wolf_raider"), "snapshot 应发布正式敌人模板定义。");
        _test.True(snapshot.EnemyBrains.ContainsKey("melee_aggressor"), "snapshot 应发布正式 AI brain 定义。");
        _test.True(snapshot.EncounterRosters.ContainsKey("wolf_den"), "snapshot 应发布正式 encounter roster 定义。");
        _test.Eq(snapshot.BattleSimProfiles.Count, 4, "snapshot 应一次投影四个正式 BattleSim profile。");

        EnemyTemplateDefinition template = snapshot.EnemyTemplates["wolf_raider"];
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry rawRegistry = new(loader);
        EnemyTemplateDef rawTemplate = rawRegistry.GetEnemyTemplatesTyped()["wolf_raider"];
        int rawTagCount = rawTemplate.tags.Count;
        int rawDropCount = rawTemplate.drop_entries.Count;
        EnemyTemplateDefinition repeatedProjection = rawTemplate.ToDefinition(snapshot.Items);
        _test.Eq(template.TemplateId, new StringName("wolf_raider"), "模板 key/value id 应保持一致。");
        _test.Eq(rawTemplate.tags.Count, rawTagCount, "definition projection 不得修改 source tags。");
        _test.Eq(rawTemplate.drop_entries.Count, rawDropCount, "definition projection 不得修改 source drops。");
        _test.Eq(repeatedProjection.TemplateId, template.TemplateId, "重复投影应保持字段稳定。");
        _test.Eq(
            template.BattleSpriteTexturePath,
            string.IsNullOrWhiteSpace(rawTemplate.battle_sprite_texture?.ResourcePath)
                ? ""
                : ContentPathCanonicalizer.Canonicalize(
                    rawTemplate.battle_sprite_texture.ResourcePath
                ),
            "texture wrapper 必须投影为资源路径。"
        );
        _test.True(
            template.Tags is not Godot.Collections.Array<StringName>,
            "模板列表必须是只读 CLR collection。"
        );
        _test.True(
            Throws<NotSupportedException>(() =>
                ((IDictionary<StringName, EnemyTemplateDefinition>)snapshot.EnemyTemplates).Add(
                    "forbidden",
                    template
                )
            ),
            "enemy snapshot dictionary 应拒绝修改。"
        );
        _test.True(
            Throws<NotSupportedException>(() =>
                ((IList<StringName>)template.Tags).Add("forbidden")
            ),
            "enemy template nested list 应拒绝修改。"
        );

        foreach (
            Type rootType in new[]
            {
                typeof(EnemyTemplateDefinition),
                typeof(EnemyAiBrainDefinition),
                typeof(WildEncounterRosterDefinition),
                typeof(BattleAiScoreProfileDefinition),
            }
        )
        {
            foreach (Type type in EnumerateTypeGraph(rootType))
            {
                _test.False(
                    typeof(GodotObject).IsAssignableFrom(type),
                    $"definition graph 不得包含 GodotObject: {rootType.Name} -> {type.FullName}"
                );
                _test.False(
                    type.Namespace?.StartsWith("Godot.Collections", StringComparison.Ordinal) == true,
                    $"definition graph 不得包含 Godot collection: {rootType.Name} -> {type.FullName}"
                );
                _test.False(
                    type == typeof(Variant),
                    $"definition graph 不得包含 Variant: {rootType.Name}"
                );
            }
        }
    }

    private void TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed()
    {
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry registry = new(loader);
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

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var pending = new Stack<Type>();
        var visited = new HashSet<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type current = pending.Pop();
            if (current == null || !visited.Add(current))
                continue;
            yield return current;
            if (current.IsArray)
                pending.Push(current.GetElementType());
            if (current.IsGenericType)
            {
                foreach (Type argument in current.GetGenericArguments())
                    pending.Push(argument);
            }
            foreach (
                PropertyInfo property in current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                if (property.DeclaringType == current)
                    pending.Push(property.PropertyType);
            }
        }
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

}

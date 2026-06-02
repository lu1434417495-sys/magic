using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_meteor_swarm_commit_payload_boundary_regression : SceneTree
{
    private readonly List<string> _failures = new();

    private static readonly string[] SpecialRuntimeFiles =
    {
        "scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs",
        "scripts/systems/battle/runtime/BattleSpecialProfileGate.cs",
        "scripts/systems/battle/runtime/BattleSpecialProfileCommitAdapter.cs",
        "scripts/systems/battle/core/meteor_swarm/MeteorSwarmProfile.cs",
        "scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetPlan.cs",
        "scripts/systems/battle/core/meteor_swarm/MeteorSwarmImpactComponent.cs",
        "scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetOutcome.cs",
        "scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs",
    };

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestCommitAdapterIsPlainTypedBoundary();
            TestCommonOutcomePayloadDictionaryBoundaryWasRemoved();
            TestSpecialRuntimeDoesNotReadLegacyEffectDefs();
            TestAdapterCommitsTypedMeteorResultAndCopiesReports();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Meteor swarm commit payload boundary regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Meteor swarm commit payload boundary regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestCommitAdapterIsPlainTypedBoundary()
    {
        Type adapterType = typeof(BattleSpecialProfileCommitAdapter);
        AssertTrue(adapterType.IsSealed, "BattleSpecialProfileCommitAdapter 应为 sealed plain C# service。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(adapterType),
            "BattleSpecialProfileCommitAdapter 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(adapterType, "GlobalClassAttribute"),
            "BattleSpecialProfileCommitAdapter 不应注册 GlobalClass。"
        );
        AssertTrue(
            adapterType.GetMethod("setup") == null
                && adapterType.GetMethod("dispose") == null
                && adapterType.GetMethod("commit_meteor_swarm_result") == null,
            "BattleSpecialProfileCommitAdapter 不应保留 GDScript-style snake_case API。"
        );
        AssertTrue(
            adapterType.GetMethod(nameof(BattleSpecialProfileCommitAdapter.Setup)) != null
                && adapterType.GetMethod(nameof(BattleSpecialProfileCommitAdapter.Dispose)) != null
                && adapterType.GetMethod(nameof(BattleSpecialProfileCommitAdapter.CommitMeteorSwarmResult)) != null,
            "BattleSpecialProfileCommitAdapter 应暴露 PascalCase typed API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(adapterType);
    }

    private void TestCommonOutcomePayloadDictionaryBoundaryWasRemoved()
    {
        AssertTrue(
            typeof(MeteorSwarmCommitResult).GetMethod("to_common_outcome_payload") == null,
            "MeteorSwarmCommitResult 不应保留 to_common_outcome_payload Dictionary 边界。"
        );
        foreach (string filePath in EnumerateSourceFiles("scripts"))
        {
            string text = ReadText(filePath);
            AssertFalse(
                text.Contains("to_common_outcome_payload(", StringComparison.Ordinal),
                $"正式代码不应再通过 to_common_outcome_payload 做 typed -> Dictionary -> typed 往返：{filePath}"
            );
        }
    }

    private void TestSpecialRuntimeDoesNotReadLegacyEffectDefs()
    {
        foreach (string filePath in SpecialRuntimeFiles)
        {
            string text = ReadText(filePath);
            AssertFalse(
                text.Contains(".effect_defs", StringComparison.Ordinal),
                $"Meteor special runtime/core 不得读取 legacy executable effect_defs：{filePath}"
            );
        }
    }

    private void TestAdapterCommitsTypedMeteorResultAndCopiesReports()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState caster = BuildUnit("meteor_commit_caster", "player");
        BattleUnitState target = BuildUnit("meteor_commit_target", "enemy");
        runtime._state = new BattleState
        {
            battle_id = "meteor_commit_payload_boundary_regression",
            map_size = new Vector2I(5, 5),
        };
        runtime._state.units[caster.unit_id] = caster;
        runtime._state.units[target.unit_id] = target;

        var committer = new BattleSkillOutcomeCommitter();
        committer.setup(runtime);
        var adapter = new BattleSpecialProfileCommitAdapter();
        adapter.Setup(committer);

        var result = new MeteorSwarmCommitResult
        {
            plan = new MeteorSwarmTargetPlan
            {
                source_unit_id = caster.unit_id,
                skill_id = "mage_meteor_swarm",
                final_anchor_coord = new Vector2I(2, 2),
                nominal_plan_signature = "nominal",
                final_plan_signature = "final",
            },
            total_damage = 12,
            total_healing = 1,
        };
        result.add_changed_unit_id(caster.unit_id);
        result.add_changed_coord(new Vector2I(2, 2));
        result.log_lines.Add("meteor commit log");
        var targetOutcome = new MeteorSwarmTargetOutcome
        {
            target_unit_id = target.unit_id,
            total_damage = 12,
            total_healing = 0,
            defeated = false,
        };
        targetOutcome.add_status_effect_id("meteor_concussed");
        result.target_outcomes.Add(targetOutcome);
        var reportEntry = new GDictionary
        {
            ["entry_type"] = "meteor_swarm_impact_summary",
            ["nested"] = new GDictionary { ["value"] = "original" },
        };
        result.report_entries.Add(reportEntry);

        var batch = new BattleEventBatch();
        AssertTrue(
            adapter.CommitMeteorSwarmResult(result, batch),
            "adapter 应从 typed MeteorSwarmCommitResult 提交 common outcome。"
        );
        AssertTrue(batch.changed_unit_ids.Contains(caster.unit_id), "提交应记录施法者变更。");
        AssertTrue(batch.changed_unit_ids.Contains(target.unit_id), "提交应记录目标变更。");
        AssertTrue(batch.changed_coords.Contains(new Vector2I(2, 2)), "提交应记录目标地格变更。");
        AssertTrue(batch.log_lines.Contains("meteor commit log"), "提交应追加 typed log line。");
        AssertEq(batch.report_entries.Count, 1, "提交应追加 report entry。");

        ((GDictionary)reportEntry["nested"])["value"] = "mutated";
        GDictionary committedEntry = batch.report_entries[0].AsGodotDictionary();
        GDictionary committedNested = committedEntry["nested"].AsGodotDictionary();
        AssertEq(
            committedNested["value"].AsString(),
            "original",
            "report entry 应在 adapter/common committer 边界 deep copy，不能被 result 后续修改污染。"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = factionId == "enemy" ? "ai" : "manual",
            current_hp = 30,
            is_alive = true,
        };
    }

    private static IEnumerable<string> EnumerateSourceFiles(string relativeRoot)
    {
        string rootPath = Path.Combine(ProjectSettings.GlobalizePath("res://"), relativeRoot);
        if (!Directory.Exists(rootPath))
        {
            yield break;
        }
        foreach (string filePath in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(filePath);
            if (extension == ".cs" || extension == ".gd")
            {
                yield return filePath.Replace('\\', '/');
            }
        }
    }

    private static string ReadText(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllText(filePath) : "";
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type)
    {
        foreach (
            MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
            )
        )
        {
            AssertFalse(
                IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{type.Name}.{method.Name} 不应返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant) || IsGodotCollectionType(type);

    private static bool IsGodotCollectionType(Type type)
    {
        if (type == null || type.IsGenericParameter)
        {
            return false;
        }
        if (type.Namespace == "Godot.Collections")
        {
            return type.Name.StartsWith("Dictionary", StringComparison.Ordinal)
                || type.Name.StartsWith("Array", StringComparison.Ordinal);
        }
        if (!type.IsGenericType)
        {
            return false;
        }
        foreach (Type genericArgument in type.GetGenericArguments())
        {
            if (IsGodotCollectionType(genericArgument))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAttributeNamed(Type type, string attributeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} Expected {expected}, got {actual}.");
        }
    }
}

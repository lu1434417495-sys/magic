using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;

public partial class run_barrier_architecture_contract_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        try
        {
            int exitCode = Run();
            Quit(exitCode);
        }
        catch (Exception ex)
        {
            GD.PushError($"Barrier architecture contract regression crashed: {ex}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestRequiredRuntimeBarrierFilesExist();
        TestBarrierRuntimeStateTypesArePlainTypedCSharp();
        TestRuntimeDoesNotLoadBarrierContentDirectly();
        TestRuntimeHasNoPrismaticSpecificRuleLiterals();
        TestRuntimeHasNoSkillIdTextCategoryGuessing();
        TestControlStatusNoLongerDependsOnBarrierService();

        if (_failures.Count == 0)
        {
            GD.Print("Barrier architecture contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Barrier architecture contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRequiredRuntimeBarrierFilesExist()
    {
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierService.cs",
            "BattleBarrierService must own barrier instances and interaction coordination."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleLayeredBarrierService.cs",
            "BattleLayeredBarrierService should remain the runtime-facing service type."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierGeometryService.cs",
            "BattleBarrierGeometryService must own footprint/line/area barrier geometry."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierOutcomeResolver.cs",
            "BattleBarrierOutcomeResolver must own whitelist outcome translation."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierInstanceState.cs",
            "Typed barrier instance state must replace anonymous barrier dictionaries."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierLayerState.cs",
            "Typed barrier layer state must replace anonymous layer dictionaries."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierOutcomeState.cs",
            "Typed barrier outcome state must replace anonymous outcome dictionaries."
        );
    }

    private void TestBarrierRuntimeStateTypesArePlainTypedCSharp()
    {
        AssertPlainCSharpType(typeof(BattleBarrierInstanceState), "BattleBarrierInstanceState");
        AssertPlainCSharpType(typeof(BattleBarrierLayerState), "BattleBarrierLayerState");
        AssertPlainCSharpType(typeof(BattleBarrierOutcomeState), "BattleBarrierOutcomeState");
        AssertPlainCSharpType(typeof(BattleBarrierOutcomeResolver), "BattleBarrierOutcomeResolver");
        AssertPlainCSharpType(typeof(BattleBarrierService), "BattleBarrierService");
        AssertPlainCSharpType(typeof(BattleLayeredBarrierService), "BattleLayeredBarrierService");

        foreach (Type type in new[]
        {
            typeof(BattleBarrierInstanceState),
            typeof(BattleBarrierLayerState),
            typeof(BattleBarrierOutcomeState),
        })
        {
            AssertNull(
                type.GetMethod("from_runtime_dict", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
                $"{type.Name} must not keep the old snake_case from_runtime_dict API."
            );
            AssertNull(
                type.GetMethod("to_runtime_dict", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
                $"{type.Name} must not keep the old snake_case to_runtime_dict API."
            );
            AssertNoGodotCollectionProperties(type);
        }

        AssertNull(
            typeof(BattleBarrierOutcomeResolver).GetMethod("ApplyPassageOutcomes"),
            "BattleBarrierOutcomeResolver must not expose a Dictionary wrapper for passage outcomes."
        );

        var outcome = new BattleBarrierOutcomeState { OutcomeType = "damage", Amount = 7 };
        var layer = new BattleBarrierLayerState
        {
            LayerId = "green",
            DisplayName = "Green Layer",
            SaveRollOverride = 9,
            HasSaveRollOverride = true,
        };
        layer.SetBlockedCategories(new[] { new StringName("poison") });
        layer.SetBreakerSkillIds(new[] { new StringName("mage_passwall") });
        layer.SetPassageOutcomes(new[] { outcome });

        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "barrier_1",
            ProfileId = "prismatic_sphere",
            AnchorCoord = new Vector2I(3, 4),
            RadiusCells = 2,
        };
        barrier.SetLayers(new[] { layer });

        AssertFalse(
            (object)barrier.Layers is Godot.Collections.Array,
            "BattleBarrierInstanceState.Layers must be a typed C# list view, not Godot Array."
        );
        AssertFalse(
            (object)layer.BlockedCategories is Godot.Collections.Array,
            "BattleBarrierLayerState.BlockedCategories must be a typed C# list view, not Godot Array."
        );
        AssertFalse(
            (object)layer.BreakerSkillIds is Godot.Collections.Array,
            "BattleBarrierLayerState.BreakerSkillIds must be a typed C# list view, not Godot Array."
        );
        AssertFalse(
            (object)layer.PassageOutcomes is Godot.Collections.Array,
            "BattleBarrierLayerState.PassageOutcomes must be a typed C# list view, not Godot Array."
        );

        BattleBarrierInstanceState roundTrip =
            BattleBarrierInstanceState.FromRuntimeDict(barrier.ToRuntimeDict());
        AssertEq(roundTrip.Layers.Count, 1, "Barrier runtime projection should preserve typed layers.");
        AssertEq(
            roundTrip.Layers[0].PassageOutcomes.Count,
            1,
            "Barrier runtime projection should preserve typed passage outcomes."
        );
        AssertEq(
            roundTrip.Layers[0].PassageOutcomes[0].Amount,
            7,
            "Barrier runtime projection should preserve outcome fields."
        );
    }

    private void TestRuntimeDoesNotLoadBarrierContentDirectly()
    {
        foreach (string sourcePath in CollectCodeFiles("res://scripts/systems/battle/runtime"))
        {
            string text = ReadText(sourcePath);
            if (text.Contains("data/configs/barriers", StringComparison.Ordinal))
            {
                _failures.Add(
                    $"{sourcePath} must not load barrier profile resources directly; profiles must come from the content registry."
                );
            }
        }
    }

    private void TestRuntimeHasNoPrismaticSpecificRuleLiterals()
    {
        string[] forbiddenTokens =
        {
            "PROFILE_PRISMATIC_SPHERE",
            "GREEN_INSTANT_DEATH_DAMAGE",
            "_build_prismatic_sphere_layers",
            "mage_cone_of_cold",
            "mage_gust_of_wind",
            "mage_spell_disjunction",
            "mage_passwall",
            "mage_arcane_missile",
            "mage_continual_light",
            "mage_dispel_magic",
        };
        foreach (string sourcePath in CollectCodeFiles("res://scripts/systems/battle/runtime"))
        {
            string text = ReadText(sourcePath);
            foreach (string token in forbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    _failures.Add(
                        $"{sourcePath} must not contain prismatic-specific runtime rule literal '{token}'."
                    );
                }
            }
        }
    }

    private void TestRuntimeHasNoSkillIdTextCategoryGuessing()
    {
        string[] forbiddenTokens =
        {
            "skill_id_text.contains",
            "skillIdText.Contains",
            ".contains(\"detect\")",
            ".Contains(\"detect\")",
            ".contains(\"breath\")",
            ".Contains(\"breath\")",
            "params.get(\"barrier_categories\"",
            "params.Get(\"barrier_categories\"",
            "params.get(&\"barrier_categories\"",
        };
        foreach (string sourcePath in Combine(
            CollectCodeFiles("res://scripts/systems/battle/runtime"),
            CollectCodeFiles("res://scripts/systems/battle/rules")
        ))
        {
            string text = ReadText(sourcePath);
            foreach (string token in forbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    _failures.Add(
                        $"{sourcePath} must not infer barrier/effect categories from skill id text or legacy params."
                    );
                }
            }
        }
    }

    private void TestControlStatusNoLongerDependsOnBarrierService()
    {
        string timelineText = ReadText(
            "res://scripts/systems/battle/runtime/BattleTimelineDriver.cs"
        );
        string runtimeText = ReadText(
            "res://scripts/systems/battle/runtime/BattleRuntimeModule.cs"
        );
        foreach (string forbidden in new[]
        {
            "resolve_control_status_turn_start",
            "is_unit_ai_controlled_for_turn",
            "clear_turn_ai_control",
        })
        {
            if (timelineText.Contains(forbidden, StringComparison.Ordinal))
            {
                _failures.Add(
                    $"BattleTimelineDriver must not call barrier service control-status method '{forbidden}'."
                );
            }
            if (runtimeText.Contains(forbidden, StringComparison.Ordinal))
            {
                _failures.Add(
                    $"BattleRuntimeModule must not call barrier service control-status method '{forbidden}'."
                );
            }
        }
    }

    private void AssertPlainCSharpType(Type type, string displayName)
    {
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(type),
            $"{displayName} must not inherit GodotObject/RefCounted."
        );
        AssertFalse(
            HasAttributeNamed(type, "GlobalClassAttribute"),
            $"{displayName} must not register as GlobalClass."
        );
    }

    private void AssertNoGodotCollectionProperties(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string propertyType = property.PropertyType.FullName ?? "";
            if (propertyType.Contains("Godot.Collections.", StringComparison.Ordinal))
            {
                _failures.Add(
                    $"{type.Name}.{property.Name} must not expose Godot collection state."
                );
            }
        }
    }

    private void AssertFileExists(string path, string message)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            _failures.Add(message);
        }
    }

    private static List<string> CollectCodeFiles(string rootPath)
    {
        var results = new List<string>();
        string root = ProjectSettings.GlobalizePath(rootPath);
        if (!Directory.Exists(root))
        {
            return results;
        }
        foreach (string filePath in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(filePath);
            if (extension != ".cs" && extension != ".gd")
            {
                continue;
            }
            results.Add("res://" + Path.GetRelativePath(ProjectSettings.GlobalizePath("res://"), filePath)
                .Replace('\\', '/'));
        }
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static IEnumerable<string> Combine(IEnumerable<string> first, IEnumerable<string> second)
    {
        foreach (string value in first)
        {
            yield return value;
        }
        foreach (string value in second)
        {
            yield return value;
        }
    }

    private static string ReadText(string path)
    {
        string globalPath = ProjectSettings.GlobalizePath(path);
        return File.Exists(globalPath) ? File.ReadAllText(globalPath) : "";
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
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
        AssertTrue(!condition, message);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}

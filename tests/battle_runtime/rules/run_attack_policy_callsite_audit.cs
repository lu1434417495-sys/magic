using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class run_attack_policy_callsite_audit : SceneTree
{
    private static readonly HashSet<string> AllowedAttackResolverFiles =
        new(StringComparer.Ordinal)
        {
            NormalizePath("res://scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs"),
            NormalizePath("res://scripts/systems/battle/rules/BattleHitResolver.cs"),
        };

    private static readonly Dictionary<string, RequiredFragment> RequiredPolicyCalleeFragments =
        new(StringComparer.Ordinal)
        {
            ["unit_execute_context"] = new(
                "res://scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs",
                "BuildAttackContext"
            ),
            ["unit_execute_check"] = new(
                "res://scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs",
                "BuildAttackCheck"
            ),
            ["unit_preview"] = new(
                "res://scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs",
                "BuildAttackPreview"
            ),
            ["ground_execute"] = new(
                "res://scripts/systems/battle/runtime/BattleGroundEffectService.cs",
                "BuildAttackCheck"
            ),
            ["repeat_execute_context"] = new(
                "res://scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs",
                "BuildRepeatAttackStageContext"
            ),
            ["repeat_execute_check"] = new(
                "res://scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs",
                "BuildFateAwareRepeatAttackStageHitCheck"
            ),
            ["charge_path_execute"] = new(
                "res://scripts/systems/battle/runtime/BattleChargeResolver.cs",
                "BuildAttackCheck"
            ),
            ["hud_preview"] = new(
                "res://scripts/systems/battle/presentation/BattleHudAdapter.cs",
                "_attackCheckPolicyService.BuildAttackPreview"
            ),
            ["ai_score_preview"] = new(
                "res://scripts/systems/battle/ai/BattleAiScoreService.cs",
                "PopulateSpecialProfileMetrics(scoreInput, context)"
            ),
        };

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        TestNoProductionDirectHitResolverAttackCalls();
        TestRequiredCallSitesRouteThroughPolicy();
        TestPolicyPublicContract();
        TestPolicyContextPublicContract();

        if (_failures.Count == 0)
        {
            GD.Print("Attack policy call-site audit: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Attack policy call-site audit: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestNoProductionDirectHitResolverAttackCalls()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        foreach (string filePath in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalizedPath = NormalizeFileSystemPath(filePath);
            if (AllowedAttackResolverFiles.Contains(normalizedPath))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (IsDirectHitResolverAttackCall(line))
                {
                    _failures.Add(
                        $"生产调用点不得绕过 BattleAttackCheckPolicyService：{normalizedPath}:{index + 1} {line.Trim()}"
                    );
                }
            }
        }
    }

    private void TestRequiredCallSitesRouteThroughPolicy()
    {
        foreach (KeyValuePair<string, RequiredFragment> entry in RequiredPolicyCalleeFragments)
        {
            string source = ReadText(entry.Value.Path);
            if (!source.Contains(entry.Value.Fragment, StringComparison.Ordinal))
            {
                _failures.Add($"attack policy audit 缺少必需调用面 {entry.Key}：{entry.Value.Fragment}");
            }
        }
    }

    private void TestPolicyPublicContract()
    {
        string source = ReadText("res://scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs");
        string[] forbiddenFragments =
        {
            "public partial class BattleAttackCheckPolicyService : RefCounted",
            "[GlobalClass]",
            "GSpecArray",
            "Godot.Collections.Array<BattleAttackRollModifierSpec>",
            "build_attack_check(",
            "build_attack_preview(",
            "build_repeat_attack_stage_context(",
            "build_fate_aware_repeat_attack_stage_hit_check(",
            "_resolve_stacked_specs",
        };
        foreach (string fragment in forbiddenFragments)
        {
            if (source.Contains(fragment, StringComparison.Ordinal))
            {
                _failures.Add($"BattleAttackCheckPolicyService 不应保留旧 public contract 片段：{fragment}");
            }
        }

        string[] requiredFragments =
        {
            "BuildAttackContext",
            "BuildAttackCheck",
            "BuildAttackPreview",
            "BuildRepeatAttackStageContext",
            "BuildFateAwareRepeatAttackStageHitCheck",
            "ResolveStackedSpecs",
            "IReadOnlyList<BattleAttackRollModifierSpec>",
            "List<BattleAttackRollModifierSpec>",
            "BattleRepeatAttackStageSpec",
        };
        foreach (string fragment in requiredFragments)
        {
            if (!source.Contains(fragment, StringComparison.Ordinal))
            {
                _failures.Add($"BattleAttackCheckPolicyService 缺少 typed C# public contract 片段：{fragment}");
            }
        }

        if (source.Contains("CombatEffectDef", StringComparison.Ordinal)
            || source.Contains("repeat_attack_effect", StringComparison.Ordinal))
        {
            _failures.Add("BattleAttackCheckPolicyService repeat API 不应接收 CombatEffectDef / repeat_attack_effect。");
        }
    }

    private void TestPolicyContextPublicContract()
    {
        string source = ReadText("res://scripts/systems/battle/core/BattleAttackCheckPolicyContext.cs");
        if (!source.Contains("public class BattleAttackCheckPolicyContext", StringComparison.Ordinal))
        {
            _failures.Add("BattleAttackCheckPolicyContext 应为 plain C# class。");
        }
        if (!source.Contains("public BattleUnitState target", StringComparison.Ordinal))
        {
            _failures.Add("BattleAttackCheckPolicyContext 应暴露文档约定的强类型 target 字段。");
        }
        if (source.Contains("target_unit", StringComparison.Ordinal))
        {
            _failures.Add("BattleAttackCheckPolicyContext 不应继续暴露 target_unit，避免新 API 又带回旧调用形状。");
        }
        if (source.Contains("RefCounted", StringComparison.Ordinal)
            || source.Contains("[GlobalClass]", StringComparison.Ordinal))
        {
            _failures.Add("BattleAttackCheckPolicyContext 不应保留 RefCounted / GlobalClass。");
        }
    }

    private static bool IsDirectHitResolverAttackCall(string line)
    {
        if (line.Contains("get_attack_check_policy_service()", StringComparison.Ordinal)
            || line.Contains("_attackCheckPolicyService.", StringComparison.Ordinal)
            || line.Contains("_attack_check_policy_service.", StringComparison.Ordinal)
            || line.Contains("attackPolicy.", StringComparison.Ordinal))
        {
            return false;
        }
        if (!line.Contains("hitResolver", StringComparison.Ordinal)
            && !line.Contains("_hitResolver", StringComparison.Ordinal)
            && !line.Contains("hit_resolver", StringComparison.Ordinal))
        {
            return false;
        }

        string[] forbiddenCalls =
        {
            ".build_skill_attack_check(",
            ".build_skill_attack_preview(",
            ".build_attack_check(",
            ".build_attack_preview(",
            ".build_repeat_attack_stage_hit_check(",
            ".build_fate_aware_repeat_attack_stage_hit_check(",
            ".build_repeat_attack_preview(",
        };
        foreach (string call in forbiddenCalls)
        {
            if (line.Contains(call, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string ReadText(string resourcePath)
    {
        string fullPath = ProjectSettings.GlobalizePath(resourcePath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
    }

    private static string NormalizeFileSystemPath(string filePath)
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://").TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        string relative = Path.GetRelativePath(projectRoot, filePath);
        return NormalizePath($"res://{relative}");
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private readonly struct RequiredFragment
    {
        public RequiredFragment(string path, string fragment)
        {
            Path = path;
            Fragment = fragment;
        }

        public string Path { get; }
        public string Fragment { get; }
    }
}

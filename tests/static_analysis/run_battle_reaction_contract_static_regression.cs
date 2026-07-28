using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;

public partial class
    run_battle_reaction_contract_static_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private static readonly IReadOnlyDictionary<
        string,
        IReadOnlySet<string>
    > DirectEntryManifest =
        new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.Ordinal
        )
        {
            ["_apply_ground_unit_effects_result"] = Paths(
                "tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs"
            ),
            ["handle_charge_skill_command_result"] = Paths(
                "tests/battle_runtime/ai/run_battle_ai_charge_path_aoe_behavior_regression.cs"
            ),
            ["ExecuteAutoCast"] = Paths(
                "tests/battle_runtime/runtime/run_prismatic_sphere_special_entry_regression.cs",
                "tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs"
            ),
            ["ResolvePendingCast"] = Paths(
                "tests/battle_runtime/runtime/run_prismatic_sphere_special_entry_regression.cs",
                "tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs"
            ),
            ["_handle_skill_command"] = Paths(
                "tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs"
            ),
            ["ApplyTimelineStep"] = Paths(
                "tests/battle_runtime/skills/run_time_stasis_regression.cs",
                "tests/battle_runtime/runtime/run_plague_tongue_weapon_ability_regression.cs",
                "tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs",
                "tests/battle_runtime/runtime/run_void_axe_weapon_ability_regression.cs"
            ),
            ["ActivateNextReadyUnit"] = Paths(
                "tests/battle_runtime/runtime/run_sands_time_weapon_ability_regression.cs",
                "tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs"
            ),
            ["EndActiveTurn"] = Paths(),
        };

    public override void _Initialize()
    {
        TestDirectEntryRootManifest();
        TestDamageResolverRequiredContextContract();
        TestProductionScopeFailurePreservationContract();
        TestBattleTuGranularityOwner();
        RequestTestExit(
            _test.Finish(
                "Battle reaction contract static regression"
            )
        );
    }

    private void TestProductionScopeFailurePreservationContract()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        string runtimeRoot = Path.Combine(
            repoRoot,
            "scripts",
            "systems",
            "battle",
            "runtime"
        );
        foreach (
            string filePath in Directory.EnumerateFiles(
                runtimeRoot,
                "*.cs",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            string source = StripCommentsAndStrings(
                File.ReadAllText(filePath)
            );
            int ownedScopeCount =
                CountOccurrences(
                    source,
                    "using BattleReactionBoundaryScope"
                )
                + CountOccurrences(
                    source,
                    "using BattleLogicalAttackScope"
                );
            if (ownedScopeCount == 0)
                continue;
            int abortCount = CountOccurrences(
                source,
                "AbortActiveReactionBoundary();"
            );
            string relative = Normalize(
                Path.GetRelativePath(repoRoot, filePath)
            );
            _test.True(
                abortCount >= ownedScopeCount,
                $"{relative} owns {ownedScopeCount} reaction/action scopes but only {abortCount} failure-preserving abort paths."
            );
        }
    }

    private void TestDirectEntryRootManifest()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        string testsRoot = Path.Combine(repoRoot, "tests");
        foreach (
            string filePath in Directory.EnumerateFiles(
                testsRoot,
                "*.cs",
                SearchOption.AllDirectories
            )
        )
        {
            string relative = Normalize(
                Path.GetRelativePath(repoRoot, filePath)
            );
            if (
                relative
                    == "tests/static_analysis/run_battle_reaction_contract_static_regression.cs"
                || relative
                    == "tests/shared/BattleReactionRootTestHelper.cs"
            )
            {
                continue;
            }

            string source = File.ReadAllText(filePath);
            string searchable = StripCommentsAndStrings(source);
            foreach (
                KeyValuePair<
                    string,
                    IReadOnlySet<string>
                > entry in DirectEntryManifest
            )
            {
                string token = "." + entry.Key + "(";
                if (
                    searchable.IndexOf(
                        token,
                        StringComparison.Ordinal
                    ) < 0
                )
                {
                    continue;
                }

                _test.True(
                    entry.Value.Contains(relative),
                    $"direct reaction entry {entry.Key} in {relative} must be added to the root manifest."
                );
                _test.True(
                    source.Contains(
                        "BattleReactionRootTestHelper.ExecuteInReactionRoot(",
                        StringComparison.Ordinal
                    ),
                    $"{relative} must wrap {entry.Key} in ExecuteInReactionRoot."
                );
                if (entry.Key == "ApplyTimelineStep")
                {
                    _test.True(
                        source.Contains(
                            "BattleEffectOrigin.Timeline(\"timeline_tick\")",
                            StringComparison.Ordinal
                        ),
                        $"{relative} timeline fixture must use the production timeline origin."
                    );
                }
                if (
                    entry.Key == "ActivateNextReadyUnit"
                    || entry.Key == "ResolvePendingCast"
                )
                {
                    _test.True(
                        source.Contains(
                            "BattleEffectOrigin.Timeline(\"ready_unit_activation\")",
                            StringComparison.Ordinal
                        ),
                        $"{relative} activation fixture must use the production activation origin."
                    );
                }
                if (entry.Key == "ExecuteAutoCast")
                {
                    _test.True(
                        source.Contains(
                            "BattleEffectOrigin.AutoCast(request)",
                            StringComparison.Ordinal
                        ),
                        $"{relative} auto-cast fixture must use the request-derived origin."
                    );
                }
                if (
                    entry.Key
                        == "_apply_ground_unit_effects_result"
                )
                {
                    foreach (
                        string required in new[]
                        {
                            "BeginLogicalAttack(",
                            "logicalAttack.Context",
                            "logicalAttack.Complete()",
                        }
                    )
                    {
                        _test.True(
                            source.Contains(
                                required,
                                StringComparison.Ordinal
                            ),
                            $"{relative} ground fixture is missing {required}."
                        );
                    }
                }
            }
        }
    }

    private void TestDamageResolverRequiredContextContract()
    {
        MethodInfo[] declarations =
            typeof(BattleDamageResolver)
                .GetMethods(
                    BindingFlags.Instance
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
                .Where(
                    method =>
                        method.Name == "ResolveAttackEffects"
                )
                .ToArray();
        _test.Eq(
            declarations.Length,
            1,
            "BattleDamageResolver must expose exactly one attack-effects declaration."
        );
        if (declarations.Length == 1)
        {
            ParameterInfo[] parameters =
                declarations[0].GetParameters();
            _test.Eq(
                parameters.Length,
                5,
                "ResolveAttackEffects must require five parameters."
            );
            if (parameters.Length == 5)
            {
                _test.Eq(
                    parameters[4].ParameterType,
                    typeof(AttackContext),
                    "the fifth parameter must be AttackContext."
                );
                _test.False(
                    parameters[4].IsOptional
                        || parameters[4].HasDefaultValue,
                    "AttackContext must not have a default."
                );
            }
        }

        string repoRoot = ProjectSettings.GlobalizePath("res://");
        var optionalContextPattern = new Regex(
            @"AttackContext\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*null",
            RegexOptions.CultureInvariant
        );
        foreach (
            string filePath in Directory.EnumerateFiles(
                Path.Combine(repoRoot, "tests"),
                "*.cs",
                SearchOption.AllDirectories
            )
        )
        {
            string relative = Normalize(
                Path.GetRelativePath(repoRoot, filePath)
            );
            _test.False(
                optionalContextPattern.IsMatch(
                    StripCommentsAndStrings(
                        File.ReadAllText(filePath)
                    )
                ),
                $"{relative} must not reintroduce an optional AttackContext override."
            );
        }
    }

    private void TestBattleTuGranularityOwner()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        string battleRoot = Path.Combine(
            repoRoot,
            "scripts",
            "systems",
            "battle"
        );
        var pattern = new Regex(
            @"const\s+int\s+(?:TuGranularity|TU_GRANULARITY)\s*=",
            RegexOptions.CultureInvariant
        );
        var matches = new List<string>();
        foreach (
            string filePath in Directory.EnumerateFiles(
                battleRoot,
                "*.cs",
                SearchOption.AllDirectories
            )
        )
        {
            if (
                pattern.IsMatch(
                    StripCommentsAndStrings(
                        File.ReadAllText(filePath)
                    )
                )
            )
            {
                matches.Add(
                    Normalize(
                        Path.GetRelativePath(
                            repoRoot,
                            filePath
                        )
                    )
                );
            }
        }
        _test.Eq(
            matches.Count,
            1,
            "battle runtime must have exactly one TU granularity constant."
        );
        if (matches.Count == 1)
        {
            _test.Eq(
                matches[0],
                "scripts/systems/battle/core/BattleTimelineState.cs",
                "BattleTimelineState must own TU granularity."
            );
        }
    }

    private static IReadOnlySet<string> Paths(
        params string[] values
    ) =>
        new HashSet<string>(
            values ?? Array.Empty<string>(),
            StringComparer.Ordinal
        );

    private static string Normalize(string path) =>
        (path ?? "").Replace('\\', '/');

    private static int CountOccurrences(
        string source,
        string token
    )
    {
        int count = 0;
        int cursor = 0;
        while (
            (cursor = source.IndexOf(
                token,
                cursor,
                StringComparison.Ordinal
            )) >= 0
        )
        {
            count++;
            cursor += token.Length;
        }
        return count;
    }

    private static string StripCommentsAndStrings(
        string source
    )
    {
        if (string.IsNullOrEmpty(source))
            return "";
        char[] output = source.ToCharArray();
        bool lineComment = false;
        bool blockComment = false;
        bool text = false;
        bool verbatim = false;
        char quote = '\0';
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next =
                index + 1 < source.Length
                    ? source[index + 1]
                    : '\0';
            if (lineComment)
            {
                if (current == '\n')
                    lineComment = false;
                else
                    output[index] = ' ';
                continue;
            }
            if (blockComment)
            {
                output[index] = current == '\n' ? '\n' : ' ';
                if (current == '*' && next == '/')
                {
                    output[index + 1] = ' ';
                    index++;
                    blockComment = false;
                }
                continue;
            }
            if (text)
            {
                output[index] = current == '\n' ? '\n' : ' ';
                if (
                    verbatim
                    && current == '"'
                    && next == '"'
                )
                {
                    output[index + 1] = ' ';
                    index++;
                    continue;
                }
                if (
                    current == quote
                    && (
                        verbatim
                        || index == 0
                        || source[index - 1] != '\\'
                    )
                )
                {
                    text = false;
                    verbatim = false;
                }
                continue;
            }
            if (current == '/' && next == '/')
            {
                output[index] = output[index + 1] = ' ';
                index++;
                lineComment = true;
                continue;
            }
            if (current == '/' && next == '*')
            {
                output[index] = output[index + 1] = ' ';
                index++;
                blockComment = true;
                continue;
            }
            if (
                current == '"'
                || current == '\''
                || (
                    current == '@'
                    && next == '"'
                )
            )
            {
                verbatim = current == '@';
                quote = current == '\'' ? '\'' : '"';
                output[index] = ' ';
                if (verbatim)
                {
                    output[index + 1] = ' ';
                    index++;
                }
                text = true;
            }
        }
        return new string(output);
    }
}

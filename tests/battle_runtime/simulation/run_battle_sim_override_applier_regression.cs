using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

public partial class run_battle_sim_override_applier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunDeferred);
    }

    private void RunDeferred()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        try
        {
            TestDeepBrainTransitionPatchStillWorks();
            TestAiScoreProfileNestedPatchWritesBackTypedProjection();
            TestTunerScoreScalarPathParity();
            TestUnknownPatchPathReportsError();
            TestFormalTransitionProfilesApplyWithoutErrors();
            TestFormalMistControllerActionPatchesAllMatchingStates();
            TestActionPatchWithStateIdOnlyUpdatesSpecifiedState();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        return _test.Finish("Battle sim override applier regression");
    }

    private void TestDeepBrainTransitionPatchStillWorks()
    {
        EnemyAiBrainDefinition brain = GameSessionTestFactory
            .GetProcessSnapshot()
            .EnemyBrains["ranged_suppressor"];
        BattleSimProfileDefinition profile = new(
            "transition_patch_probe",
            "Transition Patch Probe",
            "",
            BattleAiScoreProfileDefinition.Default,
            new[]
            {
                new BattleSimOverridePatchDefinition(
                    "brain",
                    "ranged_suppressor",
                    "",
                    "",
                    "transition_rules.0.conditions.0.basis_points",
                    6000
                ),
            }
        );

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition> { [brain.BrainId] = brain },
            profile
        );
        _test.True(
            result.Errors.Count == 0,
            $"合法 transition 深路径 patch 不应产生错误: {FormatErrors(result.Errors)}"
        );
        result.EnemyAiBrains.TryGetValue(
            "ranged_suppressor",
            out EnemyAiBrainDefinition patchedBrain
        );
        if (patchedBrain == null || patchedBrain.TransitionRules.Count == 0)
        {
            _test.Fail("override applier 应返回带 transition_rules 的 patched brain。");
            return;
        }
        EnemyAiTransitionRuleDefinition patchedRule = patchedBrain?.TransitionRules[0];
        EnemyAiTransitionConditionDefinition patchedCondition = patchedRule?.Conditions[0];
        _test.Eq(
            patchedCondition?.BasisPoints ?? -1,
            6000,
            "transition_rules.0.conditions.0.basis_points 应被 patch。"
        );
        if (brain == null || brain.TransitionRules.Count == 0)
        {
            _test.Fail("正式 ranged_suppressor brain 应带 transition_rules。");
            return;
        }
        EnemyAiTransitionRuleDefinition originalRule = brain.TransitionRules[0];
        EnemyAiTransitionConditionDefinition originalCondition = originalRule?.Conditions[0];
        _test.Eq(
            originalCondition?.BasisPoints ?? -1,
            3000,
            "override applier 应深拷贝 brain，不应改写原资源。"
        );
    }

    private void TestAiScoreProfileNestedPatchWritesBackTypedProjection()
    {
        BattleAiScoreProfileDefinition originalProfile =
            BattleAiScoreProfileDefinition.Default.WithActionBaseScores(
                new Dictionary<StringName, int>
                {
                    ["skill"] = 0,
                    ["move"] = 20,
                }
            ).WithBucketPriorities(
                new Dictionary<StringName, int> { ["mist_offense"] = 100 }
            );

        BattleSimProfileDefinition profile = new(
            "score_profile_patch_probe",
            "Score Profile Patch Probe",
            "",
            originalProfile,
            new[]
            {
                new BattleSimOverridePatchDefinition(
                    "ai_score_profile",
                    "",
                    "",
                    "",
                    "action_base_scores.move",
                    44
                ),
                new BattleSimOverridePatchDefinition(
                    "ai_score_profile",
                    "",
                    "",
                    "",
                    "movement_cost_weight",
                    7
                ),
                new BattleSimOverridePatchDefinition(
                    "ai_score_profile",
                    "",
                    "",
                    "",
                    "bucket_priorities.mist_offense",
                    333
                ),
            }
        );

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            profile
        );
        _test.True(
            result.Errors.Count == 0,
            $"ai_score_profile patch 不应产生错误: {FormatErrors(result.Errors)}"
        );

        BattleAiScoreProfileDefinition patchedProfile = result.AiScoreProfile;
        _test.True(patchedProfile != null, "override applier 应继续返回 typed ai_score_profile。");
        if (patchedProfile == null)
            return;

        _test.False(
            ReferenceEquals(patchedProfile, originalProfile),
            "override applier 应深拷贝 ai_score_profile，不应改写原资源实例。"
        );
        _test.Eq(
            patchedProfile.GetActionBaseScore("move"),
            44,
            "ai_score_profile.action_base_scores.move 深路径 patch 应写回 typed property。"
        );
        _test.Eq(
            patchedProfile.MovementCostWeight,
            7,
            "ai_score_profile 标量属性 patch 应继续生效。"
        );
        _test.Eq(
            patchedProfile.GetBucketPriority("mist_offense"),
            333,
            "ai_score_profile.bucket_priorities.* 深路径 patch 应继续生效。"
        );
        _test.Eq(
            originalProfile.GetActionBaseScore("move"),
            20,
            "原 ai_score_profile 资源不应被 override patch 改写。"
        );
    }

    private void TestTunerScoreScalarPathParity()
    {
        IReadOnlyList<string> tunerPaths = ReadTunerScorePaths();
        _test.Eq(tunerPaths.Count, 71, "tuner _SCORE_WEIGHTS 应保持 71 个标量路径。");
        _test.Eq(
            new HashSet<string>(tunerPaths, StringComparer.Ordinal).Count,
            71,
            "tuner 标量路径不得重复。"
        );

        var patches = new List<BattleSimOverridePatchDefinition>();
        for (int index = 0; index < tunerPaths.Count; index++)
        {
            string path = tunerPaths[index];
            int value = 100_000 + index;
            _test.True(
                BattleAiScoreProfileDefinition.Default.TryWithScalar(
                    path,
                    value,
                    out BattleAiScoreProfileDefinition patched
                ),
                $"BattleAiScoreProfileDefinition.TryWithScalar 应支持 tuner 路径 {path}。"
            );
            _test.False(
                ReferenceEquals(patched, BattleAiScoreProfileDefinition.Default),
                $"TryWithScalar({path}) 应返回新的 immutable definition。"
            );
            patches.Add(
                new BattleSimOverridePatchDefinition(
                    "ai_score_profile",
                    "",
                    "",
                    "",
                    path,
                    value
                )
            );
        }

        BattleSimOverrideApplyResult result = new BattleSimOverrideApplier().ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            new BattleSimProfileDefinition(
                "tuner_71_path_parity",
                "Tuner 71 Path Parity",
                "",
                BattleAiScoreProfileDefinition.Default,
                patches
            )
        );
        _test.Eq(
            result.Errors.Count,
            0,
            $"OverrideApplier 应接受 tuner 的全部 71 个标量路径: {FormatErrors(result.Errors)}"
        );
        _test.False(
            BattleAiScoreProfileDefinition.Default.TryWithScalar(
                "unsupported_tuner_scalar",
                1,
                out BattleAiScoreProfileDefinition unsupported
            ),
            "Definition 应拒绝不在显式 71-case 契约内的标量路径。"
        );
        _test.True(
            ReferenceEquals(unsupported, BattleAiScoreProfileDefinition.Default),
            "拒绝未知标量路径时应保留原 definition。"
        );
    }

    private void TestUnknownPatchPathReportsError()
    {
        EnemyAiBrainDefinition brain = GameSessionTestFactory
            .GetProcessSnapshot()
            .EnemyBrains["ranged_suppressor"];
        BattleSimProfileDefinition profile = new(
            "bad_path_probe",
            "Bad Path Probe",
            "",
            BattleAiScoreProfileDefinition.Default,
            new[]
            {
                new BattleSimOverridePatchDefinition(
                    "brain",
                    "ranged_suppressor",
                    "",
                    "",
                    "transition_rules.0.conditions.99.basis_points",
                    6000
                ),
            }
        );

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition> { [brain.BrainId] = brain },
            profile
        );
        IReadOnlyList<string> errors = result.Errors;
        _test.True(errors.Count > 0, "未知 transition 深路径必须报告 errors。");
    }

    private void TestFormalTransitionProfilesApplyWithoutErrors()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        Dictionary<StringName, EnemyAiBrainDefinition> brains = new()
        {
            ["ranged_controller"] = snapshot.EnemyBrains["ranged_controller"],
            ["ranged_suppressor"] = snapshot.EnemyBrains["ranged_suppressor"],
        };
        BattleSimOverrideApplier applier = new();

        foreach (
            StringName profileId in new[]
            {
                new StringName("mist_controller_aggressive"),
                new StringName("ranged_suppressor_cautious"),
            }
        )
        {
            BattleSimProfileDefinition profile = snapshot.BattleSimProfiles[profileId];
            BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
                new Dictionary<StringName, SkillDefinition>(),
                brains,
                profile
            );
            _test.True(
                result.Errors.Count == 0,
                $"{profileId} override patches 应全部可应用: {FormatErrors(result.Errors)}"
            );
        }
    }

    private void TestFormalMistControllerActionPatchesAllMatchingStates()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        EnemyAiBrainDefinition source = snapshot.EnemyBrains["ranged_controller"];
        BattleSimProfileDefinition profile = snapshot.BattleSimProfiles[
            "mist_controller_aggressive"
        ];
        BattleSimOverrideApplyResult result = new BattleSimOverrideApplier().ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition> { [source.BrainId] = source },
            profile
        );
        _test.Eq(
            result.Errors.Count,
            0,
            $"formal mist controller patches 应全部成功: {FormatErrors(result.Errors)}"
        );
        EnemyAiBrainDefinition patchedBrain = result.EnemyAiBrains[source.BrainId];
        _test.Eq(
            patchedBrain.DefaultStateId,
            new StringName("pressure"),
            "formal mist controller default state 应保持 pressure。"
        );

        foreach (StringName stateId in new[] { new StringName("engage"), new StringName("pressure") })
        {
            MoveToRangeActionDefinition move = FindAction<MoveToRangeActionDefinition>(
                patchedBrain,
                stateId,
                "mist_keep_range"
            );
            UseUnitSkillActionDefinition unit = FindAction<UseUnitSkillActionDefinition>(
                patchedBrain,
                stateId,
                "mist_ranged_single"
            );
            UseGroundSkillActionDefinition ground = FindAction<UseGroundSkillActionDefinition>(
                patchedBrain,
                stateId,
                "mist_aoe"
            );
            _test.Eq(move?.DesiredMinDistance ?? -1, 2, $"{stateId} move min patch");
            _test.Eq(move?.DesiredMaxDistance ?? -1, 3, $"{stateId} move max patch");
            _test.Eq(unit?.DesiredMaxDistance ?? -1, 5, $"{stateId} unit max patch");
            _test.Eq(ground?.MinimumHitCount ?? -1, 1, $"{stateId} ground hit patch");
        }
    }

    private void TestActionPatchWithStateIdOnlyUpdatesSpecifiedState()
    {
        EnemyAiBrainDefinition source = GameSessionTestFactory
            .GetProcessSnapshot()
            .EnemyBrains["ranged_controller"];
        BattleSimProfileDefinition profile = new(
            "state_scoped_action_patch",
            "State Scoped Action Patch",
            "",
            BattleAiScoreProfileDefinition.Default,
            new[]
            {
                new BattleSimOverridePatchDefinition(
                    "action",
                    source.BrainId,
                    "pressure",
                    "mist_ranged_single",
                    "desired_min_distance",
                    8
                ),
            }
        );
        BattleSimOverrideApplyResult result = new BattleSimOverrideApplier().ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition> { [source.BrainId] = source },
            profile
        );
        _test.Eq(
            result.Errors.Count,
            0,
            $"state-scoped action patch 应成功: {FormatErrors(result.Errors)}"
        );
        EnemyAiBrainDefinition patched = result.EnemyAiBrains[source.BrainId];
        _test.Eq(
            FindAction<UseUnitSkillActionDefinition>(
                patched,
                "pressure",
                "mist_ranged_single"
            )?.DesiredMinDistance ?? -1,
            8,
            "指定 state_id 时应更新 pressure action。"
        );
        _test.Eq(
            FindAction<UseUnitSkillActionDefinition>(
                patched,
                "engage",
                "mist_ranged_single"
            )?.DesiredMinDistance ?? -1,
            3,
            "指定 state_id 时不得更新 engage action。"
        );
    }

    private static IReadOnlyList<string> ReadTunerScorePaths()
    {
        string sourcePath = ProjectSettings.GlobalizePath(
            "res://tools/battle_sim_tuner/search_space.py"
        );
        string source = File.ReadAllText(sourcePath);
        int start = source.IndexOf("_SCORE_WEIGHTS = [", StringComparison.Ordinal);
        int end = source.IndexOf("SCORE_ACTION_BASE_DEFAULTS", start, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            throw new InvalidDataException("Unable to locate tuner _SCORE_WEIGHTS block.");

        string block = source[start..end];
        var paths = new List<string>();
        foreach (
            Match match in Regex.Matches(
                block,
                "\\(\\s*\"(?<path>[a-z0-9_]+)\"\\s*,",
                RegexOptions.CultureInvariant
            )
        )
        {
            paths.Add(match.Groups["path"].Value);
        }
        return paths;
    }

    private static TAction FindAction<TAction>(
        EnemyAiBrainDefinition brain,
        StringName stateId,
        StringName actionId
    )
        where TAction : EnemyAiActionDefinition
    {
        if (brain == null || !brain.TryGetState(stateId, out EnemyAiStateDefinition state))
            return null;
        foreach (EnemyAiActionDefinition action in state.Actions)
        {
            if (action.ActionId == actionId)
                return action as TAction;
        }
        return null;
    }

    private static string FormatErrors(IReadOnlyList<string> errors)
    {
        return string.Join(" | ", errors ?? System.Array.Empty<string>());
    }

}

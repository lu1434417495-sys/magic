using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_override_applier_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestProfileDefUsesTypedAiScoreProfileResource();
            TestTypedApplyResultUsesPlainCSharpBoundary();
            TestContentProviderUsesPlainTypedBoundary();
            TestDeepBrainTransitionPatchStillWorks();
            TestAiScoreProfileNestedPatchWritesBackTypedProjection();
            TestUnknownPatchPathReportsError();
            TestFormalTransitionProfilesApplyWithoutErrors();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        return _test.Finish("Battle sim override applier regression");
    }

    private void TestProfileDefUsesTypedAiScoreProfileResource()
    {
        _test.Eq(
            typeof(BattleSimProfileDef).GetField("ai_score_profile")?.FieldType,
            typeof(BattleAiScoreProfile),
            "BattleSimProfileDef.ai_score_profile 不应继续暴露 GodotObject。"
        );

        BattleSimProfileDef profile = ResourceLoader.Load<BattleSimProfileDef>(
            "res://data/configs/battle_sim/profiles/mist_controller_aggressive.tres"
        );
        _test.True(profile?.ai_score_profile != null, "正式 battle sim profile 应加载 typed ai_score_profile。");
        if (profile?.ai_score_profile == null)
            return;

        _test.Eq(
            profile.ai_score_profile.GetActionBaseScore("move"),
            32,
            "正式 profile 的 typed ai_score_profile 应保留 action_base_scores。"
        );
        _test.Eq(
            profile.ai_score_profile.GetBucketPriority("mist_offense"),
            105,
            "正式 profile 的 typed ai_score_profile 应保留 bucket_priorities。"
        );
        GDictionary profilePayload = profile.ToDictionary()["ai_score_profile"].AsGodotDictionary();
        _test.False(
            profilePayload.ContainsKey("move"),
            "BattleSimProfileDef.ToDictionary() 不应把 ai_score_profile 扁平化到根层。"
        );
        _test.Eq(
            profilePayload["action_base_scores"].AsGodotDictionary()[new StringName("move")]
                .AsInt32(),
            32,
            "BattleSimProfileDef.ToDictionary() 应通过 typed ai_score_profile 投影 profile payload。"
        );
    }

    private void TestTypedApplyResultUsesPlainCSharpBoundary()
    {
        Type resultType = typeof(BattleSimOverrideApplyResult);
        Type applierType = typeof(BattleSimOverrideApplier);
        _test.Eq(
            resultType.GetProperty("SkillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "BattleSimOverrideApplyResult.SkillDefs 应保持 typed skill index。"
        );
        _test.Eq(
            resultType.GetProperty("EnemyAiBrains", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, EnemyAiBrainDef>),
            "BattleSimOverrideApplyResult.EnemyAiBrains 应保持 typed brain index。"
        );
        _test.Eq(
            resultType.GetProperty("AiScoreProfile", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(BattleAiScoreProfile),
            "BattleSimOverrideApplyResult.AiScoreProfile 应保持 typed score profile。"
        );
        _test.Eq(
            applierType.GetMethod("ApplyProfileTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(BattleSimOverrideApplyResult),
            "BattleSimOverrideApplier 应向 C# caller 暴露 typed apply result。"
        );
        _test.True(
            applierType.GetMethod("apply_profile") == null,
            "BattleSimOverrideApplier 不应继续暴露 apply_profile GDictionary 入口。"
        );
    }

    private void TestContentProviderUsesPlainTypedBoundary()
    {
        Type providerType = typeof(BattleSimContentProvider);
        _test.Eq(
            providerType.GetMethod("GetSkillDefsTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "BattleSimContentProvider.GetSkillDefsTyped() 应保持 typed skill catalog。"
        );
        _test.Eq(
            providerType.GetMethod("GetEnemyTemplatesTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(IReadOnlyDictionary<StringName, EnemyTemplateDef>),
            "BattleSimContentProvider.GetEnemyTemplatesTyped() 应保持 typed enemy template catalog。"
        );
        _test.Eq(
            providerType.GetMethod("GetEnemyAiBrainsTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(IReadOnlyDictionary<StringName, EnemyAiBrainDef>),
            "BattleSimContentProvider.GetEnemyAiBrainsTyped() 应保持 typed enemy brain catalog。"
        );
        _test.True(
            providerType.GetMethod("get_skill_defs") == null
                && providerType.GetMethod("get_enemy_templates") == null
                && providerType.GetMethod("get_enemy_ai_brains") == null
                && providerType.GetMethod("dispose") == null,
            "BattleSimContentProvider 不应继续暴露 Godot dictionary / snake_case helper surface。"
        );
    }

    private void TestDeepBrainTransitionPatchStillWorks()
    {
        EnemyAiBrainDef brain = ResourceLoader.Load<EnemyAiBrainDef>(
            "res://data/configs/enemies/brains/ranged_suppressor.tres"
        );
        BattleSimProfileDef profile = new()
        {
            profile_id = "transition_patch_probe",
            override_patches = new GArray
            {
                new GDictionary
                {
                    ["target_type"] = "brain",
                    ["target_id"] = "ranged_suppressor",
                    ["path"] = "transition_rules.0.conditions.0.basis_points",
                    ["value"] = 6000,
                },
            },
        };

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain },
            profile
        );
        _test.True(
            result.Errors.Count == 0,
            $"合法 transition 深路径 patch 不应产生错误: {FormatErrors(result.Errors)}"
        );
        result.EnemyAiBrains.TryGetValue("ranged_suppressor", out EnemyAiBrainDef patchedBrain);
        if (patchedBrain == null || patchedBrain.transition_rules.Count == 0)
        {
            _test.Fail("override applier 应返回带 transition_rules 的 patched brain。");
            return;
        }
        EnemyAiTransitionRuleDef patchedRule = patchedBrain?.transition_rules[0];
        EnemyAiTransitionConditionDef patchedCondition =
            patchedRule?.GetTypedConditions()[0];
        _test.Eq(
            patchedCondition?.basis_points ?? -1,
            6000,
            "transition_rules.0.conditions.0.basis_points 应被 patch。"
        );
        if (brain == null || brain.transition_rules.Count == 0)
        {
            _test.Fail("正式 ranged_suppressor brain 应带 transition_rules。");
            return;
        }
        EnemyAiTransitionRuleDef originalRule = brain.transition_rules[0];
        EnemyAiTransitionConditionDef originalCondition =
            originalRule?.GetTypedConditions()[0];
        _test.Eq(
            originalCondition?.basis_points ?? -1,
            3000,
            "override applier 应深拷贝 brain，不应改写原资源。"
        );
    }

    private void TestAiScoreProfileNestedPatchWritesBackTypedProjection()
    {
        BattleAiScoreProfile originalProfile = new();
        originalProfile.action_base_scores = new GDictionary
        {
            [new StringName("skill")] = 0,
            [new StringName("move")] = 20,
        };

        BattleSimProfileDef profile = new()
        {
            profile_id = "score_profile_patch_probe",
            ai_score_profile = originalProfile,
            override_patches = new GArray
            {
                new GDictionary
                {
                    ["target_type"] = "ai_score_profile",
                    ["path"] = "action_base_scores.move",
                    ["value"] = 44,
                },
                new GDictionary
                {
                    ["target_type"] = "ai_score_profile",
                    ["path"] = "movement_cost_weight",
                    ["value"] = 7,
                },
            },
        };

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, EnemyAiBrainDef>(),
            profile
        );
        _test.True(
            result.Errors.Count == 0,
            $"ai_score_profile patch 不应产生错误: {FormatErrors(result.Errors)}"
        );

        BattleAiScoreProfile patchedProfile = result.AiScoreProfile;
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
            patchedProfile.movement_cost_weight,
            7,
            "ai_score_profile 标量属性 patch 应继续生效。"
        );
        _test.Eq(
            originalProfile.GetActionBaseScore("move"),
            20,
            "原 ai_score_profile 资源不应被 override patch 改写。"
        );
    }

    private void TestUnknownPatchPathReportsError()
    {
        EnemyAiBrainDef brain = ResourceLoader.Load<EnemyAiBrainDef>(
            "res://data/configs/enemies/brains/ranged_suppressor.tres"
        );
        BattleSimProfileDef profile = new()
        {
            profile_id = "bad_path_probe",
            override_patches = new GArray
            {
                new GDictionary
                {
                    ["target_type"] = "brain",
                    ["target_id"] = "ranged_suppressor",
                    ["path"] = "transition_rules.0.conditions.99.basis_points",
                    ["value"] = 6000,
                },
            },
        };

        BattleSimOverrideApplier applier = new();
        BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain },
            profile
        );
        IReadOnlyList<string> errors = result.Errors;
        _test.True(errors.Count > 0, "未知 transition 深路径必须报告 errors。");
    }

    private void TestFormalTransitionProfilesApplyWithoutErrors()
    {
        Dictionary<StringName, EnemyAiBrainDef> brains = new()
        {
            ["ranged_controller"] = ResourceLoader.Load<EnemyAiBrainDef>(
                "res://data/configs/enemies/brains/ranged_controller.tres"
            ),
            ["ranged_suppressor"] = ResourceLoader.Load<EnemyAiBrainDef>(
                "res://data/configs/enemies/brains/ranged_suppressor.tres"
            ),
        };
        BattleSimOverrideApplier applier = new();

        foreach (
            string profilePath in new[]
            {
                "res://data/configs/battle_sim/profiles/mist_controller_aggressive.tres",
                "res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres",
            }
        )
        {
            BattleSimProfileDef profile = ResourceLoader.Load<BattleSimProfileDef>(profilePath);
            BattleSimOverrideApplyResult result = applier.ApplyProfileTyped(
                new Dictionary<StringName, SkillDef>(),
                brains,
                profile
            );
            _test.True(
                result.Errors.Count == 0,
                $"{profilePath} override patches 应全部可应用: {FormatErrors(result.Errors)}"
            );
        }
    }

    private static string FormatErrors(IReadOnlyList<string> errors)
    {
        return string.Join(" | ", errors ?? System.Array.Empty<string>());
    }

}

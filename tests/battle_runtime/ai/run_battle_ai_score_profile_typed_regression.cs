using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_score_profile_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestScoreProfileUsesTypedBackingProjection();
        TestPreviewDamageResultUsesTypedEventBacking();

        Quit(_test.Finish("Battle AI score profile typed regression"));
    }

    private void TestScoreProfileUsesTypedBackingProjection()
    {
        _test.Eq(
            typeof(BattleAiScoreProfile)
                .GetProperty("ActionBaseScoresTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "BattleAiScoreProfile.action_base_scores 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreProfile)
                .GetProperty("BucketPrioritiesTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "BattleAiScoreProfile.bucket_priorities 业务态应保持 internal typed dictionary。"
        );

        BattleAiScoreProfile profile = new();
        profile.default_bucket_priority = 17;
        profile.action_base_scores = new GDictionary
        {
            [new StringName("skill")] = 9,
            [new StringName("move")] = 21,
            ["string_key_wait"] = -99,
        };
        profile.bucket_priorities = new GDictionary
        {
            [new StringName("mist_offense")] = 88,
            ["string_key_bucket"] = 77,
        };

        GDictionary actionProjection = profile.action_base_scores;
        actionProjection["retreat"] = 55;
        GDictionary bucketProjection = profile.bucket_priorities;
        bucketProjection["mist_control"] = 77;

        _test.Eq(
            profile.ActionBaseScoresTyped.Count,
            2,
            "BattleAiScoreProfile.action_base_scores typed backing 应只保留正式条目。"
        );
        _test.Eq(
            profile.GetActionBaseScore("move"),
            21,
            "BattleAiScoreProfile action score 查询应走 typed backing。"
        );
        _test.Eq(
            profile.GetActionBaseScore("retreat"),
            9,
            "缺失 action kind 应继续回退到 skill base score。"
        );
        _test.Eq(
            profile.BucketPrioritiesTyped.Count,
            1,
            "BattleAiScoreProfile.bucket_priorities typed backing 应只保留正式条目。"
        );
        _test.Eq(
            profile.GetBucketPriority("mist_offense"),
            88,
            "BattleAiScoreProfile bucket priority 查询应走 typed backing。"
        );
        _test.Eq(
            profile.GetBucketPriority("mist_control"),
            17,
            "缺失 bucket priority 应继续回退 default_bucket_priority。"
        );

        GDictionary payload = profile.ToDictionary();
        _test.Eq(
            payload["action_base_scores"].AsGodotDictionary()[new StringName("skill")].AsInt32(),
            9,
            "BattleAiScoreProfile.ToDictionary() 应从 typed backing 投影 action_base_scores。"
        );
        _test.Eq(
            payload["bucket_priorities"].AsGodotDictionary()[new StringName("mist_offense")].AsInt32(),
            88,
            "BattleAiScoreProfile.ToDictionary() 应从 typed backing 投影 bucket_priorities。"
        );
    }

    private void TestPreviewDamageResultUsesTypedEventBacking()
    {
        _test.Eq(
            typeof(BattleDamagePreviewResult).GetProperty("DamageEvents")?.PropertyType,
            typeof(IReadOnlyList<object>),
            "BattleDamagePreviewResult.DamageEvents 应保持 typed list backing。"
        );
        _test.Eq(
            typeof(BattleDamagePreviewResult).GetProperty("Diagnostics")?.PropertyType,
            typeof(IReadOnlyList<object>),
            "BattleDamagePreviewResult.Diagnostics 应保持 typed list backing。"
        );

        var damageEvent = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["target_id"] = new StringName("enemy_a"),
            ["damage"] = 7,
            ["tags"] = new List<object> { "fire" },
        };
        var diagnostic = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["code"] = "preview",
            ["ok"] = true,
        };
        BattleDamagePreviewResult result = BattleDamagePreviewResult.Create(
            applied: true,
            damageEvents: new[] { damageEvent },
            diagnostics: new[] { diagnostic }
        );

        damageEvent["damage"] = 99;
        ((List<object>)damageEvent["tags"]).Add("mutated");
        diagnostic["ok"] = false;

        _test.Eq(result.DamageEvents.Count, 1, "DamageEvents typed backing 应保留事件数量。");
        var storedEvent = result.DamageEvents[0] as IReadOnlyDictionary<string, object>;
        _test.True(storedEvent != null, "DamageEvents 应 deep copy 为 typed dictionary payload。");
        _test.Eq(storedEvent?["damage"], 7, "DamageEvents 应隔离调用方 dictionary mutation。");
        _test.True(
            storedEvent?["tags"] is IReadOnlyList<object> tags && tags.Count == 1,
            "DamageEvents 嵌套 list 应保持 typed copy。"
        );

        var storedDiagnostic = result.Diagnostics[0] as IReadOnlyDictionary<string, object>;
        _test.Eq(storedDiagnostic?["ok"], true, "Diagnostics 应隔离调用方 dictionary mutation。");

        GDictionary projected = result.ToDictionary();
        _test.Eq(
            projected["damage_events"].AsGodotArray().Count,
            1,
            "DamageEvents 应只在 Godot boundary 投影为 array。"
        );
        _test.Eq(
            projected["diagnostics"].AsGodotArray().Count,
            1,
            "Diagnostics 应只在 Godot boundary 投影为 array。"
        );
    }

}

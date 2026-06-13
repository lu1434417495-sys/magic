using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class AiServiceProbe
{
    private readonly BattleAiService _service = new();

    public GDictionary StatsChoose { get; private set; } = AiProbeStats.NewStats();
    public GDictionary StatsSkillInput { get; private set; } = AiProbeStats.NewStats();
    public GDictionary StatsActionInput { get; private set; } = AiProbeStats.NewStats();

    public void Setup(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null,
        BattleDamageResolver damageResolver = null
    )
    {
        _service.Setup(enemyAiBrains, damageResolver);
    }

    public void SetScoreProfile(BattleAiScoreProfile profile)
    {
        _service.SetScoreProfile(profile);
    }

    public BattleAiDecision ChooseCommand(BattleAiContext context)
    {
        AiTraceRecorder.Enter("choose_command");
        ulong start = Time.GetTicksUsec();
        BattleAiDecision result = _service.ChooseCommand(context);
        AiProbeStats.Record(StatsChoose, (long)(Time.GetTicksUsec() - start));
        AiTraceRecorder.Exit("choose_command");
        return result;
    }

    public BattleAiScoreInput BuildSkillScoreInput(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        AiTraceRecorder.Enter("build_skill_score_input");
        ulong start = Time.GetTicksUsec();
        BattleAiScoreInput result = _service
            .GetScoreService()
            .BuildSkillScoreInput(context, skillDef, command, preview, effectDefs, metadata);
        AiProbeStats.Record(StatsSkillInput, (long)(Time.GetTicksUsec() - start));
        AiTraceRecorder.Exit("build_skill_score_input");
        return result;
    }

    public BattleAiScoreInput BuildActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        AiTraceRecorder.Enter("build_action_score_input");
        ulong start = Time.GetTicksUsec();
        BattleAiScoreInput result = _service
            .GetScoreService()
            .BuildActionScoreInput(
                context,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            );
        AiProbeStats.Record(StatsActionInput, (long)(Time.GetTicksUsec() - start));
        AiTraceRecorder.Exit("build_action_score_input");
        return result;
    }

    public void ResetStats()
    {
        StatsChoose = AiProbeStats.NewStats();
        StatsSkillInput = AiProbeStats.NewStats();
        StatsActionInput = AiProbeStats.NewStats();
    }
}

internal static class AiProbeStats
{
    public static GDictionary NewStats() =>
        new()
        {
            ["call_count"] = 0,
            ["total_usec"] = 0L,
            ["max_usec"] = 0L,
            ["samples"] = System.Array.Empty<long>(),
        };

    public static void Record(GDictionary stats, long elapsedUsec)
    {
        if (stats == null)
            return;
        stats["call_count"] = DictInt(stats, "call_count") + 1;
        stats["total_usec"] = DictLong(stats, "total_usec") + elapsedUsec;
        stats["max_usec"] = Mathf.Max(DictLong(stats, "max_usec"), elapsedUsec);
        var samples =
            stats.ContainsKey("samples") && stats["samples"].VariantType == Variant.Type.PackedInt64Array
                ? new System.Collections.Generic.List<long>(stats["samples"].AsInt64Array())
                : new System.Collections.Generic.List<long>();
        samples.Add(elapsedUsec);
        stats["samples"] = samples.ToArray();
    }

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? (int)dict[key].AsInt64() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;
}

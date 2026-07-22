using System.Collections.Generic;
using Godot;

public sealed class BattleSimFactionMetricSummary
{
    public int UnitCount { get; set; }

    public int TurnCount { get; set; }

    public Dictionary<string, int> ActionCounts { get; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillAttemptCounts { get; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillSuccessCounts { get; } =
        new(System.StringComparer.Ordinal);

    public int SuccessfulSkillCount { get; set; }

    public int TotalDamageDone { get; set; }

    public int TotalHealingDone { get; set; }

    public int TotalDamageTaken { get; set; }

    public int TotalHealingReceived { get; set; }

    public int KillCount { get; set; }

    public int DeathCount { get; set; }

    internal void AccumulateFrom(Godot.Collections.Dictionary source)
    {
        UnitCount += ReadInt(source, "unit_count", 0);
        TurnCount += ReadInt(source, "turn_count", 0);
        MergeCounterMap(source, "action_counts", ActionCounts);
        MergeCounterMap(source, "skill_attempt_counts", SkillAttemptCounts);
        MergeCounterMap(source, "skill_success_counts", SkillSuccessCounts);
        SuccessfulSkillCount += ReadInt(source, "successful_skill_count", 0);
        TotalDamageDone += ReadInt(source, "total_damage_done", 0);
        TotalHealingDone += ReadInt(source, "total_healing_done", 0);
        TotalDamageTaken += ReadInt(source, "total_damage_taken", 0);
        TotalHealingReceived += ReadInt(source, "total_healing_received", 0);
        KillCount += ReadInt(source, "kill_count", 0);
        DeathCount += ReadInt(source, "death_count", 0);
    }

    private static void MergeCounterMap(
        Godot.Collections.Dictionary source,
        string key,
        Dictionary<string, int> target
    )
    {
        if (
            source == null
            || target == null
            || string.IsNullOrEmpty(key)
            || !source.ContainsKey(key)
            || source[key].VariantType != Variant.Type.Dictionary
        )
        {
            return;
        }

        using Godot.Collections.Dictionary counters = source[key].AsGodotDictionary();
        foreach (Variant rawKey in counters.Keys)
        {
            string counterKey = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (string.IsNullOrEmpty(counterKey))
                continue;
            Variant rawValue = counters[rawKey];
            if (rawValue.VariantType != Variant.Type.Int)
                continue;
            target[counterKey] =
                (target.TryGetValue(counterKey, out int current) ? current : 0)
                + rawValue.AsInt32();
        }
    }

    private static int ReadInt(Godot.Collections.Dictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;

        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }
}

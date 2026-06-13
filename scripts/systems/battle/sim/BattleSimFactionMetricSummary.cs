using Godot;

public sealed class BattleSimFactionMetricSummary
{
    public int UnitCount { get; set; }

    public int TurnCount { get; set; }

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
        SuccessfulSkillCount += ReadInt(source, "successful_skill_count", 0);
        TotalDamageDone += ReadInt(source, "total_damage_done", 0);
        TotalHealingDone += ReadInt(source, "total_healing_done", 0);
        TotalDamageTaken += ReadInt(source, "total_damage_taken", 0);
        TotalHealingReceived += ReadInt(source, "total_healing_received", 0);
        KillCount += ReadInt(source, "kill_count", 0);
        DeathCount += ReadInt(source, "death_count", 0);
    }

    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["unit_count"] = UnitCount,
            ["turn_count"] = TurnCount,
            ["successful_skill_count"] = SuccessfulSkillCount,
            ["total_damage_done"] = TotalDamageDone,
            ["total_healing_done"] = TotalHealingDone,
            ["total_damage_taken"] = TotalDamageTaken,
            ["total_healing_received"] = TotalHealingReceived,
            ["kill_count"] = KillCount,
            ["death_count"] = DeathCount,
        };

    private static int ReadInt(Godot.Collections.Dictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;

        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }
}

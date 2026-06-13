public sealed class BattleSimScenarioReport
{
    public BattleSimScenarioDef ScenarioDef { get; set; }

    public int GeneratedAtUnix { get; set; }

    public System.Collections.Generic.List<BattleSimProfileReportEntry> ProfileEntries { get; } = new();

    public System.Collections.Generic.List<BattleSimProfileComparison> Comparisons { get; } = new();

    public BattleSimOutputFiles OutputFiles { get; set; } = new();

    public string ScenarioId => ScenarioDef?.scenario_id.ToString() ?? "";

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var profileEntries = new Godot.Collections.Array();
        foreach (BattleSimProfileReportEntry entry in ProfileEntries)
            profileEntries.Add(entry?.ToDictionary() ?? new Godot.Collections.Dictionary());

        var comparisons = new Godot.Collections.Array();
        foreach (BattleSimProfileComparison entry in Comparisons)
            comparisons.Add(entry?.ToDictionary() ?? new Godot.Collections.Dictionary());

        return new Godot.Collections.Dictionary
        {
            ["scenario"] = ScenarioDef?.ToDictionary() ?? new Godot.Collections.Dictionary(),
            ["generated_at_unix"] = GeneratedAtUnix,
            ["profile_entries"] = profileEntries,
            ["comparisons"] = comparisons,
            ["output_files"] = OutputFiles?.ToDictionary() ?? new Godot.Collections.Dictionary(),
        };
    }
}

public sealed class BattleSimScenarioReport
{
    public BattleSimScenarioDef ScenarioDef { get; set; }

    public int GeneratedAtUnix { get; set; }

    public System.Collections.Generic.List<BattleSimProfileReportEntry> ProfileEntries { get; } = new();

    public System.Collections.Generic.List<BattleSimProfileComparison> Comparisons { get; } = new();

    public BattleSimOutputFiles OutputFiles { get; set; } = new();

    public string ScenarioId => ScenarioDef?.scenario_id.ToString() ?? "";

}

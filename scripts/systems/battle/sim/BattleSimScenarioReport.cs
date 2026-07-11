public sealed class BattleSimScenarioReport
{
    internal BattleSimScenarioDefinition Scenario { get; set; }

    public int GeneratedAtUnix { get; set; }

    public System.Collections.Generic.List<BattleSimProfileReportEntry> ProfileEntries { get; } = new();

    public System.Collections.Generic.List<BattleSimProfileComparison> Comparisons { get; } = new();

    public BattleSimOutputFiles OutputFiles { get; set; } = new();

    public string ScenarioId => Scenario?.ScenarioId.ToString() ?? "";

}

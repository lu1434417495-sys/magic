using System.Collections.Generic;

public sealed class BattleSimProfileReportEntry
{
    internal BattleSimProfileDefinition Profile { get; set; }

    public List<BattleSimRunReport> Runs { get; } = new();

    public BattleSimProfileSummary Summary { get; set; }

}

using System.Collections.Generic;

public sealed class BattleSimProfileReportEntry
{
    public BattleSimProfileDef Profile { get; set; }

    public List<BattleSimRunReport> Runs { get; } = new();

    public BattleSimProfileSummary Summary { get; set; }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var runsPayload = new Godot.Collections.Array();
        foreach (BattleSimRunReport run in Runs)
            runsPayload.Add(run?.ToDictionary() ?? new Godot.Collections.Dictionary());

        return new Godot.Collections.Dictionary
        {
            ["profile"] = Profile?.ToDict() ?? new Godot.Collections.Dictionary(),
            ["runs"] = runsPayload,
            ["summary"] = Summary?.ToDictionary() ?? new Godot.Collections.Dictionary(),
        };
    }
}

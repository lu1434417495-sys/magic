public sealed class BattleSimScenarioReport
{
    internal BattleSimScenarioDefinition Scenario { get; set; }

    public int GeneratedAtUnix { get; set; }

    public System.Collections.Generic.List<BattleSimProfileReportEntry> ProfileEntries { get; } = new();

    public System.Collections.Generic.List<BattleSimProfileComparison> Comparisons { get; } = new();

    public BattleSimOutputFiles OutputFiles { get; set; } = new();

    public string ScenarioId => Scenario?.ScenarioId.ToString() ?? "";

    public int RunCount => CountRuns(_ => true);

    public int CompletedRunCount =>
        CountRuns(IsCompletedRun);

    public int UnfinishedRunCount => RunCount - CompletedRunCount;

    public int StalledRunCount =>
        CountRuns(run => run?.TerminationKind == BattleSimTerminationKind.IdleStall);

    public int IterationBudgetExhaustedRunCount =>
        CountRuns(
            run =>
                run?.TerminationKind
                == BattleSimTerminationKind.IterationBudgetExhausted
        );

    public int InvalidRuntimeRunCount =>
        CountRuns(IsInvalidRuntimeRun);

    public bool HasUnfinishedRuns => UnfinishedRunCount > 0;

    public bool IsComplete => RunCount > 0 && !HasUnfinishedRuns;

    private static bool IsCompletedRun(BattleSimRunReport run) =>
        run?.IsCompletedSample == true;

    private static bool IsInvalidRuntimeRun(BattleSimRunReport run) =>
        run == null
        || run.TerminationKind == BattleSimTerminationKind.InvalidRuntime
        || (
            run.TerminationKind == BattleSimTerminationKind.BattleEnded
            && !run.HasFinalDecision
        );

    private int CountRuns(System.Func<BattleSimRunReport, bool> predicate)
    {
        int count = 0;
        foreach (BattleSimProfileReportEntry entry in ProfileEntries)
        {
            if (entry?.Runs == null)
                continue;
            foreach (BattleSimRunReport run in entry.Runs)
            {
                if (predicate(run))
                    count++;
            }
        }
        return count;
    }

}

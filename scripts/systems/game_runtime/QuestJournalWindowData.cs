using System;
using System.Collections.Generic;
using Godot;

internal enum QuestJournalStage { Active, Claimable, Completed, Failed }

internal sealed record QuestJournalObjectiveData(string Text, int Current, int Target);

internal sealed record QuestJournalEntryData(
    StringName QuestId, string Title, string Description, QuestJournalStage Stage,
    string Provider, string NextStep, IReadOnlyList<QuestJournalObjectiveData> Objectives,
    IReadOnlyList<string> Rewards
)
{
    internal string StateLabel => Stage switch
    {
        QuestJournalStage.Active => "进行中",
        QuestJournalStage.Claimable => "待交付",
        QuestJournalStage.Completed => "已完成",
        QuestJournalStage.Failed => "已失败",
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal sealed record QuestJournalWindowData(IReadOnlyList<QuestJournalEntryData> Entries)
{
    internal static readonly QuestJournalWindowData Empty = new(Array.Empty<QuestJournalEntryData>());
}

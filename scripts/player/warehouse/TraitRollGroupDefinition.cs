using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class TraitRollGroupDefinition
{
    public TraitRollGroupDefinition(
        StringName groupId,
        int rollCount,
        IReadOnlyList<TraitRollGroupEntryDefinition> entries
    )
    {
        GroupId = groupId;
        RollCount = rollCount;
        Entries = FreezeEntries(entries);
    }

    public StringName GroupId { get; }
    public int RollCount { get; }
    public IReadOnlyList<TraitRollGroupEntryDefinition> Entries { get; }

    internal static TraitRollGroupDefinition CopyOf(TraitRollGroupDefinition source)
    {
        if (source == null)
            return null;

        var entries = new List<TraitRollGroupEntryDefinition>(source.Entries.Count);
        foreach (TraitRollGroupEntryDefinition entry in source.Entries)
        {
            entries.Add(
                new TraitRollGroupEntryDefinition(
                    entry.TraitId,
                    entry.Weight,
                    entry.ExclusiveGroup
                )
            );
        }
        return new TraitRollGroupDefinition(source.GroupId, source.RollCount, entries);
    }

    private static IReadOnlyList<TraitRollGroupEntryDefinition> FreezeEntries(
        IReadOnlyList<TraitRollGroupEntryDefinition> entries
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        var copied = new List<TraitRollGroupEntryDefinition>(entries.Count);
        foreach (TraitRollGroupEntryDefinition entry in entries)
        {
            if (entry == null)
                throw new ArgumentException("Trait roll group entries must not contain null.", nameof(entries));
            copied.Add(entry);
        }
        return new ReadOnlyCollection<TraitRollGroupEntryDefinition>(copied);
    }
}

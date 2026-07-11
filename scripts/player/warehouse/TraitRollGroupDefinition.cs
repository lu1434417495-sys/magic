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

    internal static TraitRollGroupDefinition FromResource(TraitRollGroupDef source) =>
        FromResource(
            source,
            $"trait_roll_group.{WarehouseDefinitionProjection.PathId(source?.group_id ?? "")}"
        );

    internal static TraitRollGroupDefinition FromResource(
        TraitRollGroupDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var entries = new List<TraitRollGroupEntryDefinition>();
        int index = 0;
        foreach (
            TraitRollGroupEntryDef entry in WarehouseDefinitionProjection.RequireCollection(
                source.EntriesProjectionBorrowed,
                path + ".entries"
            )
        )
        {
            string entryPath = $"{path}.entries[{index}]";
            if (entry == null)
                throw WarehouseDefinitionProjection.Invalid(entryPath, "resource is null");
            entries.Add(TraitRollGroupEntryDefinition.FromResource(entry, entryPath));
            index++;
        }

        return new TraitRollGroupDefinition(source.group_id, source.roll_count, entries);
    }

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

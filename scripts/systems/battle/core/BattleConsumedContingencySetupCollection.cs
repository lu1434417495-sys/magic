using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleConsumedContingencySetupCollection
{
    private readonly List<StringName> _setupIds = new();

    internal void MarkConsumed(StringName setupId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(setupId);
        if (normalized == "" || _setupIds.Contains(normalized))
            return;
        _setupIds.Add(normalized);
    }

    internal bool Contains(StringName setupId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(setupId);
        return normalized != "" && _setupIds.Contains(normalized);
    }

    internal IReadOnlyList<StringName> GetIds() => new List<StringName>(_setupIds);

    internal void Replace(IEnumerable<StringName> setupIds)
    {
        _setupIds.Clear();
        foreach (StringName setupId in setupIds ?? Array.Empty<StringName>())
            MarkConsumed(setupId);
    }

    internal BattleConsumedContingencySetupCollection DuplicateState()
    {
        var duplicate = new BattleConsumedContingencySetupCollection();
        duplicate.Replace(_setupIds);
        return duplicate;
    }
}

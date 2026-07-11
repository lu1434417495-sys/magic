using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class BattleEventBatch : IDisposable
{
    private readonly List<StringName> _changedUnitIds = new();
    private readonly List<Vector2I> _changedCoords = new();
    private readonly List<string> _logLines = new();
    private readonly List<IReadOnlyDictionary<string, object>> _reportEntries = new();
    private readonly List<CharacterProgressionDelta> _progressionDeltas = new();
    private readonly ReadOnlyCollection<StringName> _changedUnitIdsView;
    private readonly ReadOnlyCollection<Vector2I> _changedCoordsView;
    private readonly ReadOnlyCollection<string> _logLinesView;

    public BattleEventBatch()
    {
        _changedUnitIdsView = _changedUnitIds.AsReadOnly();
        _changedCoordsView = _changedCoords.AsReadOnly();
        _logLinesView = _logLines.AsReadOnly();
    }

    public bool phase_changed { get; set; }
    public bool battle_ended { get; set; }
    public ReadOnlyCollection<StringName> changed_unit_ids => _changedUnitIdsView;
    public ReadOnlyCollection<Vector2I> changed_coords => _changedCoordsView;
    public ReadOnlyCollection<string> log_lines => _logLinesView;
    public ReadOnlyCollection<IReadOnlyDictionary<string, object>> report_entries =>
        BuildReportEntrySnapshots();
    public ReadOnlyCollection<CharacterProgressionDelta> progression_deltas =>
        BuildProgressionDeltaSnapshots();
    public bool modal_requested { get; set; }

    internal IReadOnlyList<StringName> ChangedUnitIdsTyped => _changedUnitIds;
    internal IReadOnlyList<Vector2I> ChangedCoordsTyped => _changedCoords;
    internal IReadOnlyList<string> LogLinesTyped => _logLines;
    internal IReadOnlyList<IReadOnlyDictionary<string, object>> ReportEntriesTyped =>
        BuildReportEntrySnapshots();
    internal IReadOnlyList<CharacterProgressionDelta> ProgressionDeltasTyped =>
        BuildProgressionDeltaSnapshots();

    public void Dispose() { }

    internal void SetChangedUnitIds(IEnumerable values)
    {
        _changedUnitIds.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(value);
            if (unitId != (StringName)"")
            {
                _changedUnitIds.Add(unitId);
            }
        }
    }

    internal void ClearChangedUnitIds()
    {
        _changedUnitIds.Clear();
    }

    internal void AddChangedUnitId(StringName unitId)
    {
        if (unitId == (StringName)"" || _changedUnitIds.Contains(unitId))
        {
            return;
        }
        _changedUnitIds.Add(unitId);
    }

    internal bool ContainsChangedUnitId(StringName unitId)
    {
        return unitId != (StringName)"" && _changedUnitIds.Contains(unitId);
    }

    internal void SetChangedCoords(IEnumerable values)
    {
        _changedCoords.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is Vector2I coord && !_changedCoords.Contains(coord))
            {
                _changedCoords.Add(coord);
            }
        }
    }

    internal void ClearChangedCoords()
    {
        _changedCoords.Clear();
    }

    internal void AddChangedCoord(Vector2I coord)
    {
        if (_changedCoords.Contains(coord))
        {
            return;
        }
        _changedCoords.Add(coord);
    }

    internal bool ContainsChangedCoord(Vector2I coord)
    {
        return _changedCoords.Contains(coord);
    }

    internal void SetLogLines(IEnumerable values)
    {
        _logLines.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            _logLines.Add(value?.ToString() ?? "");
        }
    }

    internal void ClearLogLines()
    {
        _logLines.Clear();
    }

    internal void AddLogLine(string value)
    {
        _logLines.Add(value ?? "");
    }

    internal void InsertLogLine(int index, string value)
    {
        _logLines.Insert(index, value ?? "");
    }

    internal bool ContainsLogLine(string value)
    {
        return _logLines.Contains(value ?? "");
    }

    internal void SetReportEntries(
        IEnumerable<IReadOnlyDictionary<string, object>> values
    )
    {
        _reportEntries.Clear();
        if (values == null)
        {
            return;
        }
        foreach (IReadOnlyDictionary<string, object> value in values)
            AddReportEntry(value);
    }

    internal void ClearReportEntries()
    {
        _reportEntries.Clear();
    }

    internal void AddReportEntry(IReadOnlyDictionary<string, object> reportEntry)
    {
        if (reportEntry == null || reportEntry.Count == 0)
        {
            return;
        }
        _reportEntries.Add(
            new ReadOnlyDictionary<string, object>(
                RuntimePlainPayload.CloneDictionary(reportEntry)
            )
        );
    }

    internal void SetProgressionDeltas(IEnumerable<CharacterProgressionDelta> values)
    {
        _progressionDeltas.Clear();
        if (values == null)
        {
            return;
        }
        foreach (CharacterProgressionDelta value in values)
            AddProgressionDelta(value);
    }

    internal void ClearProgressionDeltas()
    {
        _progressionDeltas.Clear();
    }

    internal void AddProgressionDelta(CharacterProgressionDelta delta)
    {
        if (delta == null)
        {
            return;
        }
        _progressionDeltas.Add(delta.DuplicateState());
    }

    private ReadOnlyCollection<CharacterProgressionDelta> BuildProgressionDeltaSnapshots()
    {
        var result = new List<CharacterProgressionDelta>(_progressionDeltas.Count);
        foreach (CharacterProgressionDelta delta in _progressionDeltas)
            result.Add(delta?.DuplicateState());
        return result.AsReadOnly();
    }

    private ReadOnlyCollection<IReadOnlyDictionary<string, object>> BuildReportEntrySnapshots()
    {
        var result = new List<IReadOnlyDictionary<string, object>>(_reportEntries.Count);
        foreach (IReadOnlyDictionary<string, object> entry in _reportEntries)
        {
            result.Add(
                new ReadOnlyDictionary<string, object>(
                    RuntimePlainPayload.CloneDictionary(entry)
                )
            );
        }
        return result.AsReadOnly();
    }

}

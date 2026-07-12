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
    private bool _phaseChanged;
    private bool _battleEnded;
    private bool _modalRequested;

    public BattleEventBatch()
    {
        _changedUnitIdsView = _changedUnitIds.AsReadOnly();
        _changedCoordsView = _changedCoords.AsReadOnly();
        _logLinesView = _logLines.AsReadOnly();
    }

    public bool phase_changed
    {
        get => _phaseChanged;
        set
        {
            _phaseChanged = value;
            SetFlag(BattleChangeFlags.Phase, value);
        }
    }
    public bool battle_ended
    {
        get => _battleEnded;
        set
        {
            _battleEnded = value;
            SetFlag(BattleChangeFlags.BattleLifecycle, value);
        }
    }
    public ReadOnlyCollection<StringName> changed_unit_ids => _changedUnitIdsView;
    public ReadOnlyCollection<Vector2I> changed_coords => _changedCoordsView;
    public ReadOnlyCollection<string> log_lines => _logLinesView;
    public ReadOnlyCollection<IReadOnlyDictionary<string, object>> report_entries =>
        BuildReportEntrySnapshots();
    public ReadOnlyCollection<CharacterProgressionDelta> progression_deltas =>
        BuildProgressionDeltaSnapshots();
    public bool modal_requested
    {
        get => _modalRequested;
        set
        {
            _modalRequested = value;
            SetFlag(BattleChangeFlags.Modal, value);
        }
    }

    internal IReadOnlyList<StringName> ChangedUnitIdsTyped => _changedUnitIds;
    internal IReadOnlyList<Vector2I> ChangedCoordsTyped => _changedCoords;
    internal IReadOnlyList<string> LogLinesTyped => _logLines;
    internal IReadOnlyList<IReadOnlyDictionary<string, object>> ReportEntriesTyped =>
        BuildReportEntrySnapshots();
    internal IReadOnlyList<CharacterProgressionDelta> ProgressionDeltasTyped =>
        BuildProgressionDeltaSnapshots();
    internal int ProgressionDeltaCount => _progressionDeltas.Count;
    internal BattleChangeFlags ChangeFlags { get; private set; }

    public void Dispose() { }

    internal void MarkChanged(BattleChangeFlags flags)
    {
        ChangeFlags |= flags;
    }

    internal void MarkUnitPlacementChanged()
    {
        MarkChanged(BattleChangeFlags.UnitPlacement);
    }

    internal void MarkCellStateChanged(Vector2I coord)
    {
        AddChangedCoord(coord);
        MarkChanged(BattleChangeFlags.CellState);
    }

    internal void MarkPropStateChanged(Vector2I coord)
    {
        AddChangedCoord(coord);
        MarkChanged(BattleChangeFlags.PropState);
    }

    internal void MarkTimelineChanged()
    {
        MarkChanged(BattleChangeFlags.Timeline);
    }

    internal void MergeFrom(BattleEventBatch source)
    {
        if (source == null || ReferenceEquals(source, this))
            return;

        MarkChanged(source.ChangeFlags);
        phase_changed |= source.phase_changed;
        battle_ended |= source.battle_ended;
        modal_requested |= source.modal_requested;
        foreach (StringName unitId in source._changedUnitIds)
            AddChangedUnitId(unitId);
        foreach (Vector2I coord in source._changedCoords)
            AddChangedCoord(coord);
        foreach (string logLine in source._logLines)
            AddLogLine(logLine);
        foreach (IReadOnlyDictionary<string, object> reportEntry in source._reportEntries)
            AddReportEntry(reportEntry);
        foreach (CharacterProgressionDelta progressionDelta in source._progressionDeltas)
            AddProgressionDelta(progressionDelta);
    }

    internal void SetChangedUnitIds(IEnumerable values)
    {
        _changedUnitIds.Clear();
        if (values == null)
        {
            SetFlag(BattleChangeFlags.UnitState, false);
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
        SetFlag(BattleChangeFlags.UnitState, _changedUnitIds.Count > 0);
    }

    internal void ClearChangedUnitIds()
    {
        _changedUnitIds.Clear();
        SetFlag(BattleChangeFlags.UnitState, false);
    }

    internal void AddChangedUnitId(StringName unitId)
    {
        if (unitId == (StringName)"" || _changedUnitIds.Contains(unitId))
        {
            return;
        }
        _changedUnitIds.Add(unitId);
        MarkChanged(BattleChangeFlags.UnitState);
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
            SetFlag(BattleChangeFlags.AffectedCoords, false);
            return;
        }
        foreach (object value in values)
        {
            if (value is Vector2I coord && !_changedCoords.Contains(coord))
            {
                _changedCoords.Add(coord);
            }
        }
        SetFlag(BattleChangeFlags.AffectedCoords, _changedCoords.Count > 0);
    }

    internal void ClearChangedCoords()
    {
        _changedCoords.Clear();
        SetFlag(BattleChangeFlags.AffectedCoords, false);
    }

    internal void AddChangedCoord(Vector2I coord)
    {
        if (_changedCoords.Contains(coord))
        {
            return;
        }
        _changedCoords.Add(coord);
        MarkChanged(BattleChangeFlags.AffectedCoords);
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
            SetFlag(BattleChangeFlags.Log, false);
            return;
        }
        foreach (object value in values)
        {
            _logLines.Add(value?.ToString() ?? "");
        }
        SetFlag(BattleChangeFlags.Log, _logLines.Count > 0);
    }

    internal void ClearLogLines()
    {
        _logLines.Clear();
        SetFlag(BattleChangeFlags.Log, false);
    }

    internal void AddLogLine(string value)
    {
        _logLines.Add(value ?? "");
        MarkChanged(BattleChangeFlags.Log);
    }

    internal void InsertLogLine(int index, string value)
    {
        _logLines.Insert(index, value ?? "");
        MarkChanged(BattleChangeFlags.Log);
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
            SetFlag(BattleChangeFlags.Report, false);
            return;
        }
        foreach (IReadOnlyDictionary<string, object> value in values)
            AddReportEntry(value);
    }

    internal void ClearReportEntries()
    {
        _reportEntries.Clear();
        SetFlag(BattleChangeFlags.Report, false);
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
        MarkChanged(BattleChangeFlags.Report);
    }

    internal void SetProgressionDeltas(IEnumerable<CharacterProgressionDelta> values)
    {
        _progressionDeltas.Clear();
        if (values == null)
        {
            SetFlag(BattleChangeFlags.Progression, false);
            return;
        }
        foreach (CharacterProgressionDelta value in values)
            AddProgressionDelta(value);
    }

    internal void ClearProgressionDeltas()
    {
        _progressionDeltas.Clear();
        SetFlag(BattleChangeFlags.Progression, false);
    }

    internal void AddProgressionDelta(CharacterProgressionDelta delta)
    {
        if (delta == null)
        {
            return;
        }
        _progressionDeltas.Add(delta.DuplicateState());
        MarkChanged(BattleChangeFlags.Progression);
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

    private void SetFlag(BattleChangeFlags flag, bool enabled)
    {
        if (enabled)
            ChangeFlags |= flag;
        else
            ChangeFlags &= ~flag;
    }

}

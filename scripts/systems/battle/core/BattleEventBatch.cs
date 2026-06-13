using Godot;
using System.Collections;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleEventBatch : RefCounted
{
    private readonly List<StringName> _changedUnitIds = new();
    private readonly List<Vector2I> _changedCoords = new();
    private readonly List<string> _logLines = new();
    private readonly List<GDictionary> _reportEntries = new();
    private readonly List<CharacterProgressionDelta> _progressionDeltas = new();

    public bool phase_changed { get; set; }
    public bool battle_ended { get; set; }
    public Godot.Collections.Array<StringName> changed_unit_ids
    {
        get => BuildChangedUnitIdsArray();
        set => SetChangedUnitIds(value);
    }
    public Godot.Collections.Array<Vector2I> changed_coords
    {
        get => BuildChangedCoordsArray();
        set => SetChangedCoords(value);
    }
    public Godot.Collections.Array<string> log_lines
    {
        get => BuildLogLinesArray();
        set => SetLogLines(value);
    }
    public GArray report_entries
    {
        get => BuildReportEntriesArray();
        set => SetReportEntries(value);
    }
    public GArray progression_deltas
    {
        get => BuildProgressionDeltasArray();
        set => SetProgressionDeltas(value);
    }
    public bool modal_requested { get; set; }

    internal IReadOnlyList<StringName> ChangedUnitIdsTyped => _changedUnitIds;
    internal IReadOnlyList<Vector2I> ChangedCoordsTyped => _changedCoords;
    internal IReadOnlyList<string> LogLinesTyped => _logLines;
    internal IReadOnlyList<GDictionary> ReportEntriesTyped => _reportEntries;
    internal IReadOnlyList<CharacterProgressionDelta> ProgressionDeltasTyped => _progressionDeltas;

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

    internal void SetReportEntries(IEnumerable values)
    {
        _reportEntries.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is GDictionary reportEntry)
            {
                AddReportEntry(reportEntry);
            }
        }
    }

    internal void ClearReportEntries()
    {
        _reportEntries.Clear();
    }

    internal void AddReportEntry(GDictionary reportEntry)
    {
        if (reportEntry == null || reportEntry.Count == 0)
        {
            return;
        }
        _reportEntries.Add((GDictionary)reportEntry.Duplicate(true));
    }

    internal void SetProgressionDeltas(IEnumerable values)
    {
        _progressionDeltas.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            if (value is CharacterProgressionDelta delta)
            {
                AddProgressionDelta(delta);
            }
        }
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
        _progressionDeltas.Add(delta);
    }

    private Godot.Collections.Array<string> BuildLogLinesArray()
    {
        var result = new Godot.Collections.Array<string>();
        foreach (string value in _logLines)
        {
            result.Add(value);
        }
        return result;
    }

    private Godot.Collections.Array<StringName> BuildChangedUnitIdsArray()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName unitId in _changedUnitIds)
        {
            result.Add(unitId);
        }
        return result;
    }

    private Godot.Collections.Array<Vector2I> BuildChangedCoordsArray()
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in _changedCoords)
        {
            result.Add(coord);
        }
        return result;
    }

    internal GArray BuildReportEntriesArray()
    {
        var result = new GArray();
        foreach (GDictionary reportEntry in _reportEntries)
        {
            result.Add((GDictionary)reportEntry.Duplicate(true));
        }
        return result;
    }

    internal GArray BuildProgressionDeltasArray()
    {
        var result = new GArray();
        foreach (CharacterProgressionDelta delta in _progressionDeltas)
        {
            result.Add(delta);
        }
        return result;
    }
}

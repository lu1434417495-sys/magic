using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class GameTextCommandResult : IDisposable
{
    private sealed class AssertionEntry
    {
        public bool ok;
        public string message = "";
        public string summary = "";
        public string actual = "";
        public string expected = "";

        internal GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["ok"] = ok,
                ["message"] = message,
                ["summary"] = summary,
                ["actual"] = actual,
                ["expected"] = expected,
            };
        }
    }

    private readonly System.Collections.Generic.List<AssertionEntry> _assertions = new();
    private readonly System.Collections.Generic.Dictionary<string, object> _snapshot =
        new(System.StringComparer.Ordinal);
    private readonly GodotProjectionPayloadOwner _projectionPayloads = new();
    private GDictionary _snapshotProjection;
    private Godot.Collections.Array<GDictionary> _assertionProjection;

    public string command_text = "";
    public bool ok = true;
    public GameRuntimeFacade.RuntimeCommandCode code = GameRuntimeFacade.RuntimeCommandCode.Ok;
    public bool skipped;
    public string message = "";
    public GDictionary snapshot =>
        _snapshotProjection ??= GodotTypedProjection.ProjectDictionary(_snapshot, _projectionPayloads);
    public string human_log = "";
    public string snapshot_text = "";
    public Godot.Collections.Array<GDictionary> assertions
    {
        get
        {
            if (_assertionProjection != null)
                return _assertionProjection;
            var projection = _projectionPayloads.Track(new Godot.Collections.Array<GDictionary>());
            foreach (AssertionEntry entry in _assertions)
                projection.Add(_projectionPayloads.Track(entry.ToDictionary()));
            _assertionProjection = projection;
            return _assertionProjection;
        }
    }

    internal void AddAssertion(
        bool ok,
        string message,
        string summary,
        string actual,
        string expected
    )
    {
        DisposeAssertionProjection();
        _assertions.Add(
            new AssertionEntry
            {
                ok = ok,
                message = message ?? "",
                summary = summary ?? "",
                actual = actual ?? "",
                expected = expected ?? "",
            }
        );
    }

    internal void SetSnapshot(System.Collections.Generic.IReadOnlyDictionary<string, object> snapshot)
    {
        DisposeSnapshotProjection();
        _snapshot.Clear();
        if (snapshot == null)
            return;
        foreach ((string key, object value) in CloneTypedDictionary(snapshot))
            _snapshot[key] = value;
    }

    internal System.Collections.Generic.IReadOnlyDictionary<string, object> SnapshotTyped =>
        _snapshot;

    public void Dispose()
    {
        DisposeProjections();
        _assertions.Clear();
        _snapshot.Clear();
    }

    public string Render()
    {
        var lines = new System.Collections.Generic.List<string>();
        if (skipped)
            lines.Add($"SKIP {command_text}");
        else
            lines.Add($"{(ok ? "OK" : "ERR")} {command_text}");
        if (message.Length > 0)
            lines.Add(message);
        foreach (AssertionEntry assertion in _assertions)
        {
            lines.Add(
                $"ASSERT {assertion.summary} | actual={assertion.actual} | expected={assertion.expected}"
            );
        }
        if (snapshot_text.Length > 0)
        {
            lines.Add("");
            lines.Add(snapshot_text);
        }
        return string.Join("\n", lines);
    }

    private static System.Collections.Generic.Dictionary<string, object> CloneTypedDictionary(
        System.Collections.Generic.IReadOnlyDictionary<string, object> source
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, object>(
            System.StringComparer.Ordinal
        );
        if (source == null)
            return result;
        foreach ((string key, object value) in source)
            result[key] = CloneTypedValue(value);
        return result;
    }

    private static System.Collections.Generic.List<object> CloneTypedArray(
        System.Collections.Generic.IReadOnlyList<object> source
    )
    {
        var result = new System.Collections.Generic.List<object>();
        if (source == null)
            return result;
        foreach (object value in source)
            result.Add(CloneTypedValue(value));
        return result;
    }

    private static object CloneTypedValue(object value)
    {
        if (value == null)
            return null;
        if (value is System.Collections.Generic.IReadOnlyDictionary<string, object> dictionaryValue)
            return CloneTypedDictionary(dictionaryValue);
        if (value is System.Collections.Generic.IReadOnlyList<object> listValue)
            return CloneTypedArray(listValue);
        return value;
    }

    private void DisposeSnapshotProjection()
    {
        if (_snapshotProjection == null)
            return;
        DisposeProjections();
        _snapshotProjection = null;
        _assertionProjection = null;
    }

    private void DisposeAssertionProjection()
    {
        if (_assertionProjection == null)
            return;
        DisposeProjections();
        _snapshotProjection = null;
        _assertionProjection = null;
    }

    private void DisposeProjections()
    {
        _projectionPayloads.Dispose();
        _snapshotProjection = null;
        _assertionProjection = null;
    }
}

using System;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
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

        internal IReadOnlyDictionary<string, object> BuildFacts()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ok"] = ok,
                ["message"] = message,
                ["summary"] = summary,
                ["actual"] = actual,
                ["expected"] = expected,
            };
        }
    }

    private readonly List<AssertionEntry> _assertions = new();
    private readonly List<IReadOnlyDictionary<string, object>> _assertionFacts = new();
    private readonly Dictionary<string, object> _snapshot = new(StringComparer.Ordinal);

    public string command_text = "";
    public bool ok = true;
    public GameRuntimeFacade.RuntimeCommandCode code = GameRuntimeFacade.RuntimeCommandCode.Ok;
    public bool skipped;
    public string message = "";
    public string human_log = "";
    public string snapshot_text = "";

    internal IReadOnlyDictionary<string, object> SnapshotTyped =>
        RuntimePlainPayload.CloneDictionary(_snapshot);

    internal IReadOnlyList<IReadOnlyDictionary<string, object>> AssertionFactsTyped
    {
        get
        {
            var result = new List<IReadOnlyDictionary<string, object>>(_assertionFacts.Count);
            foreach (IReadOnlyDictionary<string, object> facts in _assertionFacts)
                result.Add(RuntimePlainPayload.CloneDictionary(facts));
            return result.AsReadOnly();
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
        var entry = new AssertionEntry
        {
            ok = ok,
            message = message ?? "",
            summary = summary ?? "",
            actual = actual ?? "",
            expected = expected ?? "",
        };
        _assertions.Add(entry);
        _assertionFacts.Add(entry.BuildFacts());
    }

    internal void SetSnapshot(IReadOnlyDictionary<string, object> snapshot)
    {
        _snapshot.Clear();
        if (snapshot == null)
            return;
        foreach ((string key, object value) in RuntimePlainPayload.CloneDictionary(snapshot))
            _snapshot[key] = value;
    }

    internal GodotProjectionLease<GDictionary> BuildSnapshotLease() =>
        RuntimePlainPayload.ProjectDictionaryLease(
            SnapshotTyped,
            "game-text-command-result-snapshot",
            LifetimeDomain.Request,
            "GameTextCommandResult.snapshot"
        );

    internal GodotProjectionLease<GArray> BuildAssertionFactsLease()
    {
        var facts = new List<object>();
        foreach (IReadOnlyDictionary<string, object> assertion in AssertionFactsTyped)
            facts.Add(RuntimePlainPayload.CloneDictionary(assertion));
        return RuntimePlainPayload.ProjectArrayLease(
            facts,
            "game-text-command-result-assertions",
            LifetimeDomain.Request,
            "GameTextCommandResult.assertions"
        );
    }

    public void Dispose()
    {
        _assertions.Clear();
        _assertionFacts.Clear();
        _snapshot.Clear();
    }

    public string Render()
    {
        var lines = new List<string>();
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

}

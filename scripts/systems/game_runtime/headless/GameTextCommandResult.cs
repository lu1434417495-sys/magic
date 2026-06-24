using System;
using Godot;
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

    public string command_text = "";
    public bool ok = true;
    public GameRuntimeFacade.RuntimeCommandCode code = GameRuntimeFacade.RuntimeCommandCode.Ok;
    public bool skipped;
    public string message = "";
    public GDictionary snapshot => ProjectDictionary(_snapshot);
    public string human_log = "";
    public string snapshot_text = "";
    public Godot.Collections.Array<GDictionary> assertions
    {
        get
        {
            var projection = new Godot.Collections.Array<GDictionary>();
            foreach (AssertionEntry entry in _assertions)
                projection.Add(entry.ToDictionary());
            return projection;
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
        if (value is Variant variantValue)
            return CloneVariantValue(variantValue);
        if (value is GDictionary godotDictionaryValue)
            return CloneGodotDictionary(godotDictionaryValue);
        if (value is GArray arrayValue)
            return CloneGodotArray(arrayValue);
        if (value is System.Collections.Generic.IReadOnlyDictionary<string, object> dictionaryValue)
            return CloneTypedDictionary(dictionaryValue);
        if (value is System.Collections.Generic.IReadOnlyList<object> listValue)
            return CloneTypedArray(listValue);
        if (value is GodotObject godotObjectValue)
            return godotObjectValue.ToString() ?? "";
        return value;
    }

    private static System.Collections.Generic.Dictionary<string, object> CloneGodotDictionary(
        GDictionary source
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, object>(
            System.StringComparer.Ordinal
        );
        if (source == null)
            return result;
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (key.Length == 0)
                continue;
            result[key] = CloneTypedValue(source[rawKey]);
        }
        return result;
    }

    private static System.Collections.Generic.List<object> CloneGodotArray(GArray source)
    {
        var result = new System.Collections.Generic.List<object>();
        if (source == null)
            return result;
        foreach (object value in source)
            result.Add(CloneTypedValue(value));
        return result;
    }

    private static object CloneVariantValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.Vector2I => value.AsVector2I(),
            Variant.Type.Dictionary => CloneGodotDictionary(value.AsGodotDictionary()),
            Variant.Type.Array => CloneGodotArray(value.AsGodotArray()),
            Variant.Type.Object => value.ToString() ?? "",
            _ => value.ToString() ?? "",
        };
    }

    private static GDictionary ProjectDictionary(
        System.Collections.Generic.IReadOnlyDictionary<string, object> source
    )
    {
        var projection = new GDictionary();
        if (source == null)
            return projection;
        foreach ((string key, object value) in source)
            projection[key] = ProjectValue(value);
        return projection;
    }

    private static Godot.Collections.Array ProjectArray(
        System.Collections.Generic.IReadOnlyList<object> source
    )
    {
        var projection = new Godot.Collections.Array();
        if (source == null)
            return projection;
        foreach (object value in source)
            projection.Add(ProjectValue(value));
        return projection;
    }

    private static Variant ProjectValue(object value)
    {
        if (value == null)
            return default;
        if (value is System.Collections.Generic.IReadOnlyDictionary<string, object> dictionaryValue)
            return ProjectDictionary(dictionaryValue);
        if (value is System.Collections.Generic.IReadOnlyList<object> listValue)
            return ProjectArray(listValue);
        return value switch
        {
            Variant variantValue => variantValue,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vectorValue => vectorValue,
            GodotObject godotObjectValue => godotObjectValue.ToString() ?? "",
            _ => value.ToString() ?? "",
        };
    }
}

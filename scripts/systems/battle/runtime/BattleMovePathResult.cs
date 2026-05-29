using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class BattleMovePathResult
{
    public bool Allowed { get; init; }
    public int Cost { get; init; }
    public GVector2IArray Path { get; init; } = new();
    public string Message { get; init; } = "";

    public static BattleMovePathResult FromDictionary(GDictionary data)
    {
        data ??= new GDictionary();
        return new BattleMovePathResult
        {
            Allowed = GdInterop.GetBool(data, "allowed"),
            Cost = GdInterop.GetInt(data, "cost"),
            Path = ToVector2IArray(GdInterop.GetArray(data, "path")),
            Message = GdInterop.GetString(data, "message"),
        };
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["allowed"] = Allowed,
            ["cost"] = Cost,
            ["path"] = Path ?? new GVector2IArray(),
            ["message"] = Message,
        };
    }

    private static GVector2IArray ToVector2IArray(GArray source)
    {
        var result = new GVector2IArray();
        if (source == null)
        {
            return result;
        }
        foreach (Vector2I coord in source)
        {
            result.Add(coord);
        }
        return result;
    }
}

public sealed class BattleMovePathTreeResult
{
    public Dictionary<Vector2I, int> Costs { get; } = new();
    public Dictionary<Vector2I, Vector2I> Previous { get; } = new();
    public Dictionary<Vector2I, int> Steps { get; } = new();

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["costs"] = ToGodotDictionary(Costs),
            ["previous"] = ToGodotDictionary(Previous),
            ["steps"] = ToGodotDictionary(Steps),
        };
    }

    private static GDictionary ToGodotDictionary(Dictionary<Vector2I, int> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, int value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static GDictionary ToGodotDictionary(Dictionary<Vector2I, Vector2I> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, Vector2I value) in source)
        {
            result[key] = value;
        }
        return result;
    }
}

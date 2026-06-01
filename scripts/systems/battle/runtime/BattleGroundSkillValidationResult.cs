using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public readonly record struct BattleGroundSkillValidationResult(
    bool Allowed,
    string Message,
    IReadOnlyList<Vector2I> TargetCoords,
    bool HasPreviewCoords,
    IReadOnlyList<Vector2I> PreviewCoords,
    Vector2I Direction,
    int Distance,
    Vector2I ResolvedAnchorCoord
)
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    public static BattleGroundSkillValidationResult FromDictionary(GDictionary source)
    {
        if (source == null)
        {
            return Denied("地面技能目标无效。");
        }
        return new BattleGroundSkillValidationResult(
            ReadAllowedFlag(source),
            ReadString(source, "message", "地面技能目标无效。"),
            ReadVector2IList(source, "target_coords"),
            source.ContainsKey("preview_coords") || source.ContainsKey(new StringName("preview_coords")),
            ReadVector2IList(source, "preview_coords"),
            ReadVector2I(source, "direction", Vector2I.Zero),
            ReadInt(source, "distance", 0),
            ReadVector2I(source, "resolved_anchor_coord", InvalidCoord)
        );
    }

    public static BattleGroundSkillValidationResult Denied(
        string message,
        IReadOnlyList<Vector2I> targetCoords = null
    ) =>
        new(
            false,
            string.IsNullOrEmpty(message) ? "地面技能目标无效。" : message,
            targetCoords ?? System.Array.Empty<Vector2I>(),
            false,
            System.Array.Empty<Vector2I>(),
            Vector2I.Zero,
            0,
            InvalidCoord
        );

    public static BattleGroundSkillValidationResult AllowedResult(
        string message,
        IReadOnlyList<Vector2I> targetCoords,
        IReadOnlyList<Vector2I> previewCoords = null,
        Vector2I direction = default,
        int distance = 0,
        Vector2I resolvedAnchorCoord = default
    ) =>
        new(
            true,
            string.IsNullOrEmpty(message) ? "可施放。" : message,
            targetCoords ?? System.Array.Empty<Vector2I>(),
            previewCoords != null,
            previewCoords ?? System.Array.Empty<Vector2I>(),
            direction,
            distance,
            resolvedAnchorCoord == default ? InvalidCoord : resolvedAnchorCoord
        );

    public GVector2IArray TargetCoordsArray() => ToVector2IArray(TargetCoords);

    public GVector2IArray PreviewCoordsArray() => ToVector2IArray(PreviewCoords);

    public GDictionary ToDictionary()
    {
        var result = new GDictionary
        {
            ["allowed"] = Allowed,
            ["message"] = Message ?? "",
            ["target_coords"] = TargetCoordsArray(),
            ["resolved_anchor_coord"] = ResolvedAnchorCoord,
        };
        if (HasPreviewCoords)
        {
            result["preview_coords"] = PreviewCoordsArray();
        }
        if (Direction != Vector2I.Zero)
        {
            result["direction"] = Direction;
        }
        if (Distance > 0)
        {
            result["distance"] = Distance;
        }
        return result;
    }

    private static GVector2IArray ToVector2IArray(IReadOnlyList<Vector2I> coords)
    {
        var result = new GVector2IArray();
        if (coords == null)
        {
            return result;
        }
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static bool ReadAllowedFlag(GDictionary source)
    {
        if (!HasKey(source, "allowed"))
            return false;
        return source.ContainsKey("allowed")
            ? source["allowed"].AsBool()
            : source[new StringName("allowed")].AsBool();
    }

    private static string ReadString(GDictionary source, string key, string fallback)
    {
        if (!HasKey(source, key))
        {
            return fallback;
        }
        string result = source.ContainsKey(key)
            ? source[key].ToString()
            : source[new StringName(key)].ToString();
        return string.IsNullOrEmpty(result) ? fallback : result;
    }

    private static int ReadInt(GDictionary source, string key, int fallback)
    {
        if (!HasKey(source, key))
        {
            return fallback;
        }
        return source.ContainsKey(key)
            ? source[key].AsInt32()
            : source[new StringName(key)].AsInt32();
    }

    private static Vector2I ReadVector2I(GDictionary source, string key, Vector2I fallback)
    {
        if (!HasKey(source, key))
            return fallback;
        return source.ContainsKey(key)
            ? source[key].AsVector2I()
            : source[new StringName(key)].AsVector2I();
    }

    private static IReadOnlyList<Vector2I> ReadVector2IList(GDictionary source, string key)
    {
        if (!HasKey(source, key))
        {
            return System.Array.Empty<Vector2I>();
        }
        var result = new List<Vector2I>();
        var values = source.ContainsKey(key)
            ? source[key].AsGodotArray()
            : source[new StringName(key)].AsGodotArray();
        foreach (var entry in values)
        {
            result.Add(entry.AsVector2I());
        }
        return result;
    }

    private static bool HasKey(GDictionary source, string key)
    {
        if (source == null)
        {
            return false;
        }
        return source.ContainsKey(key) || source.ContainsKey(new StringName(key));
    }
}

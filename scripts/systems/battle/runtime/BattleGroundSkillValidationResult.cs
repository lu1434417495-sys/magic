using System.Collections.Generic;
using Godot;

internal readonly record struct BattleGroundSkillValidationResult(
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

    internal static BattleGroundSkillValidationResult FromDictionary(Godot.Collections.Dictionary source)
    {
        if (source == null)
        {
            return Denied("地面技能目标无效。");
        }
        return new BattleGroundSkillValidationResult(
            ReadAllowedFlag(source),
            ReadString(source, "message", "地面技能目标无效。"),
            ReadVector2IList(source, "target_coords"),
            source.ContainsKey("preview_coords"),
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

    private Godot.Collections.Array<Vector2I> ToTargetCoordsArray() => ToVector2IArray(TargetCoords);

    private Godot.Collections.Array<Vector2I> ToPreviewCoordsArray() => ToVector2IArray(PreviewCoords);

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var result = new Godot.Collections.Dictionary
        {
            ["allowed"] = Allowed,
            ["message"] = Message ?? "",
            ["target_coords"] = ToTargetCoordsArray(),
            ["resolved_anchor_coord"] = ResolvedAnchorCoord,
        };
        if (HasPreviewCoords)
        {
            result["preview_coords"] = ToPreviewCoordsArray();
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

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IReadOnlyList<Vector2I> coords
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
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

    private static bool ReadAllowedFlag(Godot.Collections.Dictionary source)
    {
        if (!HasKey(source, "allowed"))
            return false;
        return source["allowed"].AsBool();
    }

    private static string ReadString(Godot.Collections.Dictionary source, string key, string fallback)
    {
        if (!HasKey(source, key))
        {
            return fallback;
        }
        string result = source[key].ToString();
        return string.IsNullOrEmpty(result) ? fallback : result;
    }

    private static int ReadInt(Godot.Collections.Dictionary source, string key, int fallback)
    {
        if (!HasKey(source, key))
        {
            return fallback;
        }
        return source[key].AsInt32();
    }

    private static Vector2I ReadVector2I(
        Godot.Collections.Dictionary source,
        string key,
        Vector2I fallback
    )
    {
        if (!HasKey(source, key))
            return fallback;
        return source[key].AsVector2I();
    }

    private static IReadOnlyList<Vector2I> ReadVector2IList(
        Godot.Collections.Dictionary source,
        string key
    )
    {
        if (!HasKey(source, key))
        {
            return System.Array.Empty<Vector2I>();
        }
        var result = new List<Vector2I>();
        var values = source[key].AsGodotArray();
        foreach (var entry in values)
        {
            result.Add(entry.AsVector2I());
        }
        return result;
    }

    private static bool HasKey(Godot.Collections.Dictionary source, string key)
    {
        if (source == null)
        {
            return false;
        }
        return source.ContainsKey(key);
    }
}

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
        return TryReadVariant(source, "allowed", out Variant value)
            && value.VariantType == Variant.Type.Bool
            && value.AsBool();
    }

    private static string ReadString(GDictionary source, object key, string fallback)
    {
        if (!TryReadVariant(source, key, out Variant value))
        {
            return fallback;
        }
        string result = value.AsString();
        return string.IsNullOrEmpty(result) ? fallback : result;
    }

    private static int ReadInt(GDictionary source, object key, int fallback)
    {
        if (!TryReadVariant(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => Mathf.RoundToInt((float)value.AsDouble()),
            _ => fallback,
        };
    }

    private static Vector2I ReadVector2I(GDictionary source, object key, Vector2I fallback)
    {
        return TryReadVariant(source, key, out Variant value)
            && value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : fallback;
    }

    private static IReadOnlyList<Vector2I> ReadVector2IList(GDictionary source, object key)
    {
        if (!TryReadVariant(source, key, out Variant value) || value.VariantType != Variant.Type.Array)
        {
            return System.Array.Empty<Vector2I>();
        }
        var result = new List<Vector2I>();
        foreach (Variant entry in value.AsGodotArray())
        {
            if (entry.VariantType == Variant.Type.Vector2I)
            {
                result.Add(entry.AsVector2I());
            }
        }
        return result;
    }

    private static bool TryReadVariant(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }
}

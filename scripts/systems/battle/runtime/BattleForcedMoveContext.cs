using Godot;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleForcedMoveContext(Vector2I Direction)
{
    public static BattleForcedMoveContext Empty => new(Vector2I.Zero);

    public bool HasDirection => Direction != Vector2I.Zero;

    public static BattleForcedMoveContext FromDirection(Vector2I direction) =>
        new(NormalizeAxisDirection(direction));

    public GDictionary ToDictionary() =>
        HasDirection
            ? new GDictionary { ["direction"] = Direction }
            : new GDictionary();

    public static Vector2I NormalizeAxisDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }

        int absX = Mathf.Abs(direction.X);
        int absY = Mathf.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        if (absY > 0)
        {
            return new Vector2I(0, direction.Y > 0 ? 1 : -1);
        }
        return Vector2I.Zero;
    }
}

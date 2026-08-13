using Godot;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleForcedMoveContext(
    Vector2I Direction,
    Vector2I DestinationCoord,
    bool DestinationSelected
)
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    public BattleForcedMoveContext(Vector2I direction)
        : this(direction, InvalidCoord, false) { }

    public static BattleForcedMoveContext Empty => new(Vector2I.Zero, InvalidCoord, false);

    public bool HasDirection => Direction != Vector2I.Zero;

    public static BattleForcedMoveContext FromDirection(Vector2I direction) =>
        new(NormalizeAxisDirection(direction), InvalidCoord, false);

    public static BattleForcedMoveContext FromDestination(Vector2I destinationCoord) =>
        new(Vector2I.Zero, destinationCoord, true);

    public bool HasDestination => DestinationSelected && DestinationCoord != InvalidCoord;

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

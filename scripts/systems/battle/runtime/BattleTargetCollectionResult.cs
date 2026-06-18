using System.Collections.Generic;
using Godot;

internal sealed class BattleTargetCollectionResult
{
    private readonly IReadOnlyList<Vector2I> _targetCoords;

    private BattleTargetCollectionResult(bool handled, IEnumerable<Vector2I> targetCoords)
    {
        Handled = handled;
        _targetCoords = SortCoords(targetCoords).AsReadOnly();
    }

    public bool Handled { get; }

    public IReadOnlyList<Vector2I> TargetCoords => _targetCoords;

    internal static BattleTargetCollectionResult HandledResult(IEnumerable<Vector2I> targetCoords) =>
        new(true, targetCoords);

    internal static BattleTargetCollectionResult UnhandledResult(IEnumerable<Vector2I> targetCoords) =>
        new(false, targetCoords);

    private static List<Vector2I> SortCoords(IEnumerable<Vector2I> targetCoords)
    {
        var coords = new List<Vector2I>(targetCoords ?? System.Array.Empty<Vector2I>());
        coords.Sort(
            (left, right) =>
            {
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            }
        );
        return coords;
    }
}

using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleOccupiedCoordReadView
    : IReadOnlyList<Vector2I>
{
    private static readonly Vector2IList Empty = new();
    private readonly Vector2IList _values;

    internal BattleOccupiedCoordReadView(Vector2IList values)
    {
        _values = values;
    }

    private Vector2IList Values => _values ?? Empty;

    internal bool IsPresent => _values != null;

    public int Count => Values.Count;

    public Vector2I this[int index] => Values[index];

    internal bool Contains(Vector2I coord) => Values.Contains(coord);

    public List<Vector2I>.Enumerator GetEnumerator() =>
        Values.GetEnumerator();

    IEnumerator<Vector2I> IEnumerable<Vector2I>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly record struct BattleUnitGeometryReadView(
    bool OwnerPresent,
    Vector2I AnchorCoord,
    int BodySize,
    StringName BodySizeCategory,
    Vector2I FootprintSize,
    BattleOccupiedCoordReadView OccupiedCoords
)
{
    internal static BattleUnitGeometryReadView MissingOwner =>
        new(
            false,
            Vector2I.Zero,
            BattleUnitState.BodySizeMedium,
            new StringName("medium"),
            Vector2I.One,
            new BattleOccupiedCoordReadView(
                new Vector2IList { Vector2I.Zero }
            )
        );
}

internal readonly record struct BattleUnitGeometrySnapshot(
    bool OwnerPresent,
    Vector2I AnchorCoord,
    int BodySize,
    StringName BodySizeCategory,
    Vector2I FootprintSize,
    Vector2IList OccupiedCoords
)
{
    internal static BattleUnitGeometrySnapshot Present(
        Vector2I anchorCoord,
        int bodySize,
        StringName bodySizeCategory,
        Vector2I footprintSize,
        Vector2IList occupiedCoords
    ) =>
        new(
            true,
            anchorCoord,
            bodySize,
            bodySizeCategory,
            footprintSize,
            occupiedCoords
        );

    internal static BattleUnitGeometrySnapshot MissingOwner =>
        new(
            false,
            Vector2I.Zero,
            BattleUnitState.BodySizeMedium,
            new StringName("medium"),
            Vector2I.One,
            new Vector2IList { Vector2I.Zero }
        );
}

internal sealed class BattleUnitGeometryState
{
    private Vector2I _anchorCoord = Vector2I.Zero;
    private int _bodySize = BattleUnitState.BodySizeMedium;
    private StringName _bodySizeCategory = "medium";
    private Vector2I _footprintSize = Vector2I.One;
    private Vector2IList _occupiedCoords =
        BuildOccupiedCoords(Vector2I.Zero, Vector2I.One);

    internal BattleUnitGeometryReadView GetReadView() =>
        new(
            true,
            _anchorCoord,
            _bodySize,
            _bodySizeCategory,
            _footprintSize,
            new BattleOccupiedCoordReadView(_occupiedCoords)
        );

    internal Vector2I GetAnchorCoord() => _anchorCoord;

    internal int GetBodySize() => _bodySize;

    internal StringName GetBodySizeCategory() => _bodySizeCategory;

    internal Vector2I GetFootprintSize() => _footprintSize;

    internal BattleOccupiedCoordReadView GetOccupiedCoordsReadView() =>
        new(_occupiedCoords);

    internal IReadOnlyList<Vector2I> GetOccupiedCoordsDetached()
    {
        var result = new List<Vector2I>();
        if (_occupiedCoords != null)
        {
            foreach (Vector2I coord in _occupiedCoords)
                result.Add(coord);
        }
        return result;
    }

    internal bool OccupiesCoord(Vector2I targetCoord) =>
        _occupiedCoords?.Contains(targetCoord) == true;

    internal void SetAnchorCoord(Vector2I anchorCoord)
    {
        _anchorCoord = anchorCoord;
        RefreshFootprint();
    }

    internal bool SetBodySizeCategory(StringName category)
    {
        if (!IsValidBodySizeCategory(category))
            return false;

        _bodySizeCategory = category;
        _bodySize = GetBodySizeForCategory(category);
        RefreshFootprint();
        return true;
    }

    internal bool SetBodySizeProjection(int size)
    {
        if (!IsValidBodySize(size))
            return false;

        _bodySize = size;
        _bodySizeCategory = GetBodySizeCategoryForSize(size);
        RefreshFootprint();
        return true;
    }

    internal void RestoreBodyShapeProjection(
        StringName category,
        int size,
        Vector2I footprint,
        IEnumerable<Vector2I> occupiedCoords
    )
    {
        _bodySizeCategory = IsValidBodySizeCategory(category)
            ? category
            : GetBodySizeCategoryForSize(
                System.Math.Clamp(
                    size,
                    BattleUnitState.BodySizeSmall,
                    BattleUnitState.BodySizeBoss
                )
            );
        _bodySize = IsValidBodySize(size)
            ? size
            : GetBodySizeForCategory(_bodySizeCategory);
        _footprintSize = footprint;
        _occupiedCoords = DuplicateOccupiedCoords(
            occupiedCoords,
            preserveNull: false
        );
    }

    internal void NormalizeForOwnerWrite(StringName unitId)
    {
        EnsureBodySizeIdentityInvariant(unitId);
        RefreshFootprint();
    }

    internal void EnsureProjectionInvariant(StringName unitId)
    {
        EnsureBodySizeIdentityInvariant(unitId);
        Vector2I resolvedFootprint =
            GetFootprintSizeForBodySize(_bodySize);
        if (
            _footprintSize != resolvedFootprint
            || !OccupiedCoordsMatch(
                _anchorCoord,
                resolvedFootprint,
                _occupiedCoords
            )
        )
        {
            throw new System.InvalidOperationException(
                $"BattleUnitState footprint 投影不一致: unit_id='{unitId}', "
                + $"coord={_anchorCoord}, body_size={_bodySize}, "
                + $"footprint_size={_footprintSize}。 "
                + "请通过 owner 写入口修改坐标或体型。"
            );
        }
    }

    internal BattleUnitGeometrySnapshot CaptureRaw() =>
        BattleUnitGeometrySnapshot.Present(
            _anchorCoord,
            _bodySize,
            _bodySizeCategory,
            _footprintSize,
            DuplicateOccupiedCoords(
                _occupiedCoords,
                preserveNull: true
            )
        );

    internal void RestoreRaw(BattleUnitGeometrySnapshot snapshot)
    {
        _anchorCoord = snapshot.AnchorCoord;
        _bodySize = snapshot.BodySize;
        _bodySizeCategory = snapshot.BodySizeCategory;
        _footprintSize = snapshot.FootprintSize;
        _occupiedCoords = DuplicateOccupiedCoords(
            snapshot.OccupiedCoords,
            preserveNull: true
        );
    }

    internal BattleUnitGeometryState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitGeometryState FromRaw(
        BattleUnitGeometrySnapshot snapshot
    )
    {
        var result = new BattleUnitGeometryState();
        result.RestoreRaw(snapshot);
        return result;
    }

    internal static bool IsValidBodySizeCategory(
        StringName category
    )
    {
        string text = category?.ToString() ?? "";
        return text == "tiny"
            || text == "small"
            || text == "medium"
            || text == "large"
            || text == "huge"
            || text == "gargantuan"
            || text == "boss";
    }

    internal static bool IsValidBodySize(int size) =>
        size >= BattleUnitState.BodySizeSmall
        && size <= BattleUnitState.BodySizeBoss;

    internal static int GetBodySizeForCategory(
        StringName category
    )
    {
        return category?.ToString() switch
        {
            "tiny" => BattleUnitState.BodySizeTiny,
            "small" => BattleUnitState.BodySizeSmall,
            "large" => BattleUnitState.BodySizeLarge,
            "huge" => BattleUnitState.BodySizeHuge,
            "gargantuan" => BattleUnitState.BodySizeGargantuan,
            "boss" => BattleUnitState.BodySizeBoss,
            _ => BattleUnitState.BodySizeMedium,
        };
    }

    internal static StringName GetBodySizeCategoryForSize(
        int size
    )
    {
        return size switch
        {
            BattleUnitState.BodySizeMedium => "medium",
            BattleUnitState.BodySizeLarge => "large",
            BattleUnitState.BodySizeHuge => "huge",
            BattleUnitState.BodySizeGargantuan => "gargantuan",
            BattleUnitState.BodySizeBoss => "boss",
            _ => "small",
        };
    }

    internal static Vector2I GetFootprintSizeForBodySize(
        int size
    )
    {
        int normalizedSize = System.Math.Max(
            size,
            BattleUnitState.BodySizeSmall
        );
        return normalizedSize switch
        {
            BattleUnitState.BodySizeLarge => new Vector2I(2, 2),
            BattleUnitState.BodySizeHuge => new Vector2I(3, 3),
            BattleUnitState.BodySizeGargantuan => new Vector2I(4, 4),
            BattleUnitState.BodySizeBoss => new Vector2I(5, 5),
            _ => Vector2I.One,
        };
    }

    internal static Vector2IList BuildOccupiedCoords(
        Vector2I anchorCoord,
        Vector2I footprint
    )
    {
        Vector2IList result = new();
        for (int y = 0; y < footprint.Y; y++)
        {
            for (int x = 0; x < footprint.X; x++)
                result.Add(anchorCoord + new Vector2I(x, y));
        }
        return result;
    }

    internal static bool OccupiedCoordsMatch(
        Vector2I anchorCoord,
        Vector2I footprint,
        IReadOnlyList<Vector2I> occupiedCoords
    )
    {
        int width = System.Math.Max(footprint.X, 0);
        int height = System.Math.Max(footprint.Y, 0);
        if (
            occupiedCoords == null
            || occupiedCoords.Count != width * height
        )
        {
            return false;
        }

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (
                    occupiedCoords[index]
                    != anchorCoord + new Vector2I(x, y)
                )
                {
                    return false;
                }
                index += 1;
            }
        }
        return true;
    }

    private void RefreshFootprint()
    {
        Vector2I resolvedFootprint =
            GetFootprintSizeForBodySize(_bodySize);
        if (
            _footprintSize == resolvedFootprint
            && OccupiedCoordsMatch(
                _anchorCoord,
                resolvedFootprint,
                _occupiedCoords
            )
        )
        {
            return;
        }

        _footprintSize = resolvedFootprint;
        _occupiedCoords = BuildOccupiedCoords(
            _anchorCoord,
            resolvedFootprint
        );
    }

    private void EnsureBodySizeIdentityInvariant(
        StringName unitId
    )
    {
        if (
            IsValidBodySizeCategory(_bodySizeCategory)
            && IsValidBodySize(_bodySize)
            &&
            GetBodySizeForCategory(_bodySizeCategory)
            == _bodySize
        )
        {
            return;
        }

        throw new System.InvalidOperationException(
            "BattleUnitState body_size/body_size_category 不一致: "
            + $"unit_id='{unitId}', body_size={_bodySize}, "
            + $"body_size_category='{_bodySizeCategory}'。 "
            + "请检查数据构造路径是否绕过 SetBodySizeCategory()。"
        );
    }

    private static Vector2IList DuplicateOccupiedCoords(
        IEnumerable<Vector2I> source,
        bool preserveNull
    )
    {
        if (source == null)
            return preserveNull ? null : new Vector2IList();

        return new Vector2IList(source);
    }
}

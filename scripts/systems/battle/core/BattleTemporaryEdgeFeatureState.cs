using Godot;

internal sealed class BattleTemporaryEdgeFeatureState
{
    public Vector2I OriginCoord { get; init; } = Vector2I.Zero;
    public Vector2I Direction { get; init; } = Vector2I.Right;
    public StringName SourceUnitId { get; init; } = "";
    public StringName SourceEquipmentInstanceId { get; init; } = "";
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int CreatedAtTu { get; init; }
    public int ExpiresAtTu { get; init; }
    public int Sequence { get; init; }
    public BattleEdgeFeatureState Feature { get; init; }

    public bool IsValid =>
        Feature != null
        && BindingId != ""
        && ActionId != ""
        && (Direction == Vector2I.Right || Direction == Vector2I.Down)
        && ExpiresAtTu > CreatedAtTu;

    public StringName StateTag => Feature?.state_tag ?? new StringName("");

    public bool IsExpired(int currentTu) => currentTu >= ExpiresAtTu;

    public bool SameSource(BattleTemporaryEdgeFeatureState other) =>
        other != null
        && SourceUnitId == other.SourceUnitId
        && SourceEquipmentInstanceId == other.SourceEquipmentInstanceId
        && BindingId == other.BindingId
        && ActionId == other.ActionId
        && StateTag == other.StateTag;

    public bool SameEdge(BattleTemporaryEdgeFeatureState other) =>
        other != null && OriginCoord == other.OriginCoord && Direction == other.Direction;

    public BattleTemporaryEdgeFeatureState DuplicateState() =>
        new()
        {
            OriginCoord = OriginCoord,
            Direction = Direction,
            SourceUnitId = SourceUnitId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            BindingId = BindingId,
            ActionId = ActionId,
            CreatedAtTu = CreatedAtTu,
            ExpiresAtTu = ExpiresAtTu,
            Sequence = Sequence,
            Feature = Feature?.DuplicateFeature(),
        };

    internal static bool TryNormalizeEdge(
        Vector2I fromCoord,
        Vector2I toCoord,
        out Vector2I originCoord,
        out Vector2I direction
    )
    {
        Vector2I delta = toCoord - fromCoord;
        if (delta == Vector2I.Right)
        {
            originCoord = fromCoord;
            direction = Vector2I.Right;
            return true;
        }
        if (delta == Vector2I.Left)
        {
            originCoord = toCoord;
            direction = Vector2I.Right;
            return true;
        }
        if (delta == Vector2I.Down)
        {
            originCoord = fromCoord;
            direction = Vector2I.Down;
            return true;
        }
        if (delta == Vector2I.Up)
        {
            originCoord = toCoord;
            direction = Vector2I.Down;
            return true;
        }

        originCoord = Vector2I.Zero;
        direction = Vector2I.Zero;
        return false;
    }
}

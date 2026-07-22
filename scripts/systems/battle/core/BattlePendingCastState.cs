using System.Collections.Generic;
using Godot;

internal sealed class BattlePendingCastState
{
    private readonly List<StringName> _targetUnitIds = new();
    private readonly List<Vector2I> _targetCoords = new();

    internal StringName SourceUnitId { get; set; } = "";
    internal StringName SkillId { get; set; } = "";
    internal StringName VariantId { get; set; } = "";
    internal BattleTargetMode TargetMode { get; set; } = BattleTargetMode.Unit;
    internal PendingCastBindingModeKind BindingMode { get; set; } =
        PendingCastBindingModeKind.SoftAnchor;
    internal Vector2I StartedCoord { get; set; } = Vector2I.Zero;
    internal int StartedTu { get; set; }
    internal int BaseCastingTimeTu { get; set; }
    internal int RemainingCastProgress { get; set; }
    internal int LastMaintenanceCheckpointHp { get; set; }
    internal ulong CastSequence { get; set; }
    internal SkillCostTransaction CostTransaction { get; set; } = new();
    internal BattleSpellControlMetadata SpellControlMetadata { get; set; } = new();

    internal IReadOnlyList<StringName> TargetUnitIds => _targetUnitIds;
    internal IReadOnlyList<Vector2I> TargetCoords => _targetCoords;
    internal bool IsComplete => RemainingCastProgress <= 0;

    internal void SetTargetUnitIds(IEnumerable<StringName> targetUnitIds)
    {
        _targetUnitIds.Clear();
        if (targetUnitIds == null)
            return;
        foreach (StringName unitId in targetUnitIds)
        {
            if (!unitId.IsEmpty)
                _targetUnitIds.Add(unitId);
        }
    }

    internal void SetTargetCoords(IEnumerable<Vector2I> targetCoords)
    {
        _targetCoords.Clear();
        if (targetCoords == null)
            return;
        foreach (Vector2I coord in targetCoords)
            _targetCoords.Add(coord);
    }

    internal bool RemoveTargetUnitId(StringName unitId)
    {
        if (unitId.IsEmpty)
            return false;
        return _targetUnitIds.Remove(unitId);
    }

    internal BattlePendingCastState Clone()
    {
        BattlePendingCastState clone = new()
        {
            SourceUnitId = SourceUnitId,
            SkillId = SkillId,
            VariantId = VariantId,
            TargetMode = TargetMode,
            BindingMode = BindingMode,
            StartedCoord = StartedCoord,
            StartedTu = StartedTu,
            BaseCastingTimeTu = BaseCastingTimeTu,
            RemainingCastProgress = RemainingCastProgress,
            LastMaintenanceCheckpointHp = LastMaintenanceCheckpointHp,
            CastSequence = CastSequence,
            CostTransaction = CostTransaction?.Clone() ?? new SkillCostTransaction(),
            SpellControlMetadata = SpellControlMetadata,
        };
        clone.SetTargetUnitIds(_targetUnitIds);
        clone.SetTargetCoords(_targetCoords);
        return clone;
    }

    internal BattlePendingCastState DuplicateForMutationSnapshotExact()
    {
        BattlePendingCastState clone = new()
        {
            SourceUnitId = SourceUnitId,
            SkillId = SkillId,
            VariantId = VariantId,
            TargetMode = TargetMode,
            BindingMode = BindingMode,
            StartedCoord = StartedCoord,
            StartedTu = StartedTu,
            BaseCastingTimeTu = BaseCastingTimeTu,
            RemainingCastProgress = RemainingCastProgress,
            LastMaintenanceCheckpointHp = LastMaintenanceCheckpointHp,
            CastSequence = CastSequence,
            CostTransaction = CostTransaction?.Clone(),
            SpellControlMetadata = SpellControlMetadata is null
                ? null
                : SpellControlMetadata with { },
        };
        clone._targetUnitIds.AddRange(_targetUnitIds);
        clone._targetCoords.AddRange(_targetCoords);
        return clone;
    }
}

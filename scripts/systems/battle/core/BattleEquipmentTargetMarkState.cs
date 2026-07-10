using Godot;

internal sealed class BattleEquipmentTargetMarkState
{
    public StringName SourceUnitId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public StringName SourceEquipmentInstanceId { get; init; } = "";
    public StringName BindingId { get; init; } = "";
    public StringName StateKey { get; init; } = "";
    public int Stacks { get; init; }
    public int RemainingDurationTu { get; init; } = -1;
    public bool RemoveOnSourceMissing { get; init; }

    public bool IsValid =>
        SourceUnitId != ""
        && TargetUnitId != ""
        && BindingId != ""
        && StateKey != "";

    public bool IsSameSource(BattleEquipmentTargetMarkState other) =>
        other != null
        && SourceUnitId == other.SourceUnitId
        && SourceEquipmentInstanceId == other.SourceEquipmentInstanceId
        && BindingId == other.BindingId
        && StateKey == other.StateKey;

    public BattleEquipmentTargetMarkState DuplicateState() =>
        new()
        {
            SourceUnitId = SourceUnitId,
            TargetUnitId = TargetUnitId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            BindingId = BindingId,
            StateKey = StateKey,
            Stacks = Stacks,
            RemainingDurationTu = RemainingDurationTu,
            RemoveOnSourceMissing = RemoveOnSourceMissing,
        };

    public BattleEquipmentTargetMarkState WithRemainingDurationTu(int remainingDurationTu) =>
        new()
        {
            SourceUnitId = SourceUnitId,
            TargetUnitId = TargetUnitId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            BindingId = BindingId,
            StateKey = StateKey,
            Stacks = Stacks,
            RemainingDurationTu = remainingDurationTu,
            RemoveOnSourceMissing = RemoveOnSourceMissing,
        };
}

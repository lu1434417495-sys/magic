using Godot;

internal sealed class BattleTemporalProgressModifierState
{
    internal StringName ModifierId { get; init; } = "";
    internal StringName BindingId { get; init; } = "";
    internal StringName SourceEquipmentInstanceId { get; init; } = "";
    internal bool AppliesToActionProgress { get; init; }
    internal bool AppliesToCastProgress { get; init; }
    internal int SaveDc { get; init; }
    internal StringName AttributeModifierId { get; init; } = "";
    internal int SuccessRatePercent { get; init; }
    internal int FailureRatePercent { get; init; }
    internal string Label { get; init; } = "";

    internal BattleTemporalProgressModifierState DuplicateState()
    {
        return new BattleTemporalProgressModifierState
        {
            ModifierId = ModifierId,
            BindingId = BindingId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            AppliesToActionProgress = AppliesToActionProgress,
            AppliesToCastProgress = AppliesToCastProgress,
            SaveDc = SaveDc,
            AttributeModifierId = AttributeModifierId,
            SuccessRatePercent = SuccessRatePercent,
            FailureRatePercent = FailureRatePercent,
            Label = Label,
        };
    }
}

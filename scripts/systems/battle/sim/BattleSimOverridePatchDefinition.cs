using Godot;

internal sealed class BattleSimOverridePatchDefinition
{
    private readonly object _value;

    internal BattleSimOverridePatchDefinition(
        string targetType,
        StringName targetId,
        StringName stateId,
        StringName actionId,
        string path,
        object value
    )
    {
        TargetType = targetType ?? string.Empty;
        TargetId = targetId;
        StateId = stateId;
        ActionId = actionId;
        Path = path ?? string.Empty;
        _value = RuntimePlainPayload.CloneValue(value);
    }

    internal string TargetType { get; }
    internal StringName TargetId { get; }
    internal StringName StateId { get; }
    internal StringName ActionId { get; }
    internal string Path { get; }
    internal object Value => RuntimePlainPayload.CloneValue(_value);
}

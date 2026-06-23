using Godot;

internal sealed class ContingencyReleaseContext
{
    internal static ContingencyReleaseContext Empty { get; } = new();

    internal StringName InstanceId { get; init; } = "";
    internal StringName SetupId { get; init; } = "";
    internal StringName OwnerMemberId { get; init; } = "";
    internal StringName OwnerUnitId { get; init; } = "";
    internal StringName CasterUnitId { get; init; } = "";
    internal StringName TriggerType { get; init; } = "";
    internal StringName TriggeringUnitId { get; init; } = "";
    internal bool Suppressed { get; init; }

    internal bool IsValid =>
        InstanceId != "" && SetupId != "" && OwnerMemberId != "" && OwnerUnitId != "";
}

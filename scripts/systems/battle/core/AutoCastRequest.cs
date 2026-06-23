using Godot;

internal sealed class AutoCastRequest
{
    internal StringName CasterUnitId { get; init; } = "";
    internal StringName OwnerMemberId { get; init; } = "";
    internal StringName OwnerUnitId { get; init; } = "";
    internal StringName SetupId { get; init; } = "";
    internal StringName InstanceId { get; init; } = "";
    internal StringName StoredSkillId { get; init; } = "";
    internal int CastLevel { get; init; }
    internal ContingencyTargetResolutionResult TargetResolution { get; init; }
    internal ContingencyReleaseContext ReleaseContext { get; init; }
    internal ContingencyFrozenTriggerFacts FrozenFacts { get; init; } =
        ContingencyFrozenTriggerFacts.Empty;

    internal bool IsValid =>
        CasterUnitId != ""
        && OwnerMemberId != ""
        && OwnerUnitId != ""
        && SetupId != ""
        && InstanceId != ""
        && StoredSkillId != ""
        && CastLevel > 0
        && TargetResolution?.Ok == true
        && ReleaseContext?.IsValid == true;
}

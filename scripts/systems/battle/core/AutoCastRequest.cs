using Godot;

internal sealed class AutoCastRequest
{
    internal StringName CasterUnitId { get; init; } = "";
    internal StringName OwnerMemberId { get; init; } = "";
    internal StringName OwnerUnitId { get; init; } = "";
    internal StringName SetupId { get; init; } = "";
    internal StringName InstanceId { get; init; } = "";
    internal StringName SourceSkillId { get; init; } = "";
    internal int SourceSkillLevel { get; init; }
    internal UnitSkillGrantSourceType SourceSkillGrantSourceType { get; init; } =
        UnitSkillGrantSourceType.Unknown;
    internal StringName StoredSkillId { get; init; } = "";
    internal int CastLevel { get; init; }
    internal ContingencyTargetResolutionResult TargetResolution { get; init; }
    internal ContingencyReleaseContext ReleaseContext { get; init; }
    internal ContingencyFrozenTriggerFacts FrozenFacts { get; init; } =
        ContingencyFrozenTriggerFacts.Empty;
    internal StringName SkillEntryId => BattleSkillEntryIds.ScopedAutoCast(InstanceId, StoredSkillId);

    internal bool IsValid =>
        CasterUnitId != ""
        && OwnerMemberId != ""
        && OwnerUnitId != ""
        && SetupId != ""
        && InstanceId != ""
        && SourceSkillId != ""
        && SourceSkillLevel > 0
        && SourceSkillGrantSourceType == UnitSkillGrantSourceType.Player
        && StoredSkillId != ""
        && CastLevel > 0
        && SkillEntryId != ""
        && TargetResolution?.Ok == true
        && ReleaseContext?.IsValid == true;
}

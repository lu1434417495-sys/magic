using Godot;

public sealed class AgeStageResolution
{
    public readonly StringName StageId;
    public readonly StringName SourceType;
    public readonly StringName SourceId;

    public AgeStageResolution(
        StringName stageId,
        StringName sourceType,
        StringName sourceId
    )
    {
        StageId = stageId;
        SourceType = sourceType;
        SourceId = sourceId;
    }
}

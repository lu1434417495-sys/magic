using Godot;

public sealed class QuestFailureRequest
{
    public QuestFailureRequest(
        StringName questId,
        int worldStep,
        StringName reasonId,
        QuestProgressContext context = null
    )
    {
        QuestId = questId;
        WorldStep = worldStep;
        ReasonId = reasonId;
        Context = context?.DuplicateState() ?? QuestProgressContext.Empty();
    }

    public StringName QuestId { get; }

    public int WorldStep { get; }

    public StringName ReasonId { get; }

    public QuestProgressContext Context { get; }

    public bool IsValid => QuestId != "" && ReasonId != "" && WorldStep >= -1;
}

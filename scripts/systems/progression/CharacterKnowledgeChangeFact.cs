using Godot;

public sealed class CharacterKnowledgeChangeFact
{
    public StringName KnowledgeId { get; }
    public string KnowledgeLabel { get; }
    public string ReasonText { get; }

    public CharacterKnowledgeChangeFact(
        StringName knowledgeId,
        string knowledgeLabel,
        string reasonText
    )
    {
        KnowledgeId = knowledgeId;
        KnowledgeLabel = knowledgeLabel ?? "";
        ReasonText = reasonText ?? "";
    }

}

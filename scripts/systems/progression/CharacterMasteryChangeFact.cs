using Godot;

public sealed class CharacterMasteryChangeFact
{
    public StringName SkillId { get; }
    public string SkillName { get; }
    public int MasteryAmount { get; }
    public StringName SourceType { get; }
    public string SourceLabel { get; }
    public string ReasonText { get; }

    public CharacterMasteryChangeFact(
        StringName skillId,
        string skillName,
        int masteryAmount,
        StringName sourceType,
        string sourceLabel,
        string reasonText
    )
    {
        SkillId = skillId;
        SkillName = skillName ?? "";
        MasteryAmount = masteryAmount;
        SourceType = sourceType;
        SourceLabel = sourceLabel ?? "";
        ReasonText = reasonText ?? "";
    }

}

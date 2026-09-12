using System.Collections.Generic;
using Godot;

public sealed class AchievementDefinition
{
    public AchievementDefinition(
        StringName achievementId,
        string displayName,
        string description,
        StringName eventType,
        StringName subjectId,
        int threshold,
        IReadOnlyList<AchievementRewardDefinition> rewards
    )
    {
        AchievementId = achievementId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "AchievementDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "AchievementDefinition.Description"
        );
        EventType = eventType;
        SubjectId = subjectId;
        Threshold = threshold;
        Rewards = IdentityDefinitionProjection.FreezeList(
            rewards,
            "AchievementDefinition.Rewards"
        );
    }

    public StringName AchievementId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public StringName EventType { get; }
    public StringName SubjectId { get; }
    public int Threshold { get; }
    public IReadOnlyList<AchievementRewardDefinition> Rewards { get; }
}

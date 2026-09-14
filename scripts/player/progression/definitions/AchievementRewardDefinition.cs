using Godot;

public sealed class AchievementRewardDefinition
{
    public AchievementRewardDefinition(
        StringName rewardType,
        StringName targetId,
        string targetLabel,
        int amount,
        string reasonText
    )
    {
        RewardType = rewardType;
        TargetId = targetId;
        TargetLabel = IdentityDefinitionProjection.CopyString(
            targetLabel,
            "AchievementRewardDefinition.TargetLabel"
        );
        Amount = amount;
        ReasonText = IdentityDefinitionProjection.CopyString(
            reasonText,
            "AchievementRewardDefinition.ReasonText"
        );
    }

    public StringName RewardType { get; }
    public StringName TargetId { get; }
    public string TargetLabel { get; }
    public int Amount { get; }
    public string ReasonText { get; }
    internal PendingCharacterRewardEntryKind RewardKind =>
        PendingCharacterRewardContentRules.ToEntryKind(RewardType);
}

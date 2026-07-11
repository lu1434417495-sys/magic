using System.IO;
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

    internal static AchievementRewardDefinition FromSeed(
        AchievementRewardDef source,
        string path
    )
    {
        string rootPath = string.IsNullOrWhiteSpace(path) ? "$" : path;
        IdentityDefinitionProjection.RequireResource(
            source,
            rootPath,
            nameof(AchievementRewardDef)
        );
        PendingCharacterRewardEntryKind rewardKind = source.RewardKind;
        if (rewardKind == PendingCharacterRewardEntryKind.Unknown)
        {
            throw new InvalidDataException(
                $"Content value at '{rootPath}.reward_type' has unsupported achievement reward type '{source.reward_type}'."
            );
        }
        if (source.target_id == "")
        {
            throw new InvalidDataException(
                $"Content StringName at '{rootPath}.target_id' must not be empty."
            );
        }
        RequireString(source.target_label, $"{rootPath}.target_label");
        RequireString(source.reason_text, $"{rootPath}.reason_text");
        if (
            (
                rewardKind == PendingCharacterRewardEntryKind.AttributeDelta
                || rewardKind == PendingCharacterRewardEntryKind.SkillMastery
            )
            && source.amount <= 0
        )
        {
            throw new InvalidDataException(
                $"Content value at '{rootPath}.amount' must be positive for reward type '{source.reward_type}'."
            );
        }

        return new AchievementRewardDefinition(
            source.reward_type,
            source.target_id,
            source.target_label,
            source.amount,
            source.reason_text
        );
    }

    private static void RequireString(string value, string path)
    {
        if (value == null)
            throw new InvalidDataException($"Content string at '{path}' must not be null.");
    }
}

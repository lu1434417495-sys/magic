using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed record FaithRankRewardEntryDefinition(
    StringName EntryType,
    StringName TargetId,
    int Amount,
    string TargetLabel,
    string ReasonText
);

public sealed class FaithRankDefinition
{
    public FaithRankDefinition(
        int rankIndex,
        string rankName,
        int requiredGold,
        int requiredLevel,
        StringName requiredCustomStatId,
        int requiredCustomStatMinValue,
        StringName requiredAchievementId,
        IReadOnlyList<FaithRankRewardEntryDefinition> rewardEntries
    )
    {
        RankIndex = rankIndex;
        RankName = rankName
            ?? throw new InvalidDataException("FaithRankDefinition.RankName must not be null.");
        RequiredGold = requiredGold;
        RequiredLevel = requiredLevel;
        RequiredCustomStatId = requiredCustomStatId;
        RequiredCustomStatMinValue = requiredCustomStatMinValue;
        RequiredAchievementId = requiredAchievementId;
        RewardEntries = ProgressionDefinitionProjection.FreezeValues(
            rewardEntries,
            "FaithRankDefinition.RewardEntries"
        );
    }

    public int RankIndex { get; }
    public string RankName { get; }
    public int RequiredGold { get; }
    public int RequiredLevel { get; }
    public StringName RequiredCustomStatId { get; }
    public int RequiredCustomStatMinValue { get; }
    public StringName RequiredAchievementId { get; }
    public IReadOnlyList<FaithRankRewardEntryDefinition> RewardEntries { get; }

    public bool HasCustomStatRequirement() =>
        RequiredCustomStatId != "" && RequiredCustomStatMinValue > 0;

    public bool HasAchievementRequirement() => RequiredAchievementId != "";

}

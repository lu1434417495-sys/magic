using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

    internal static AchievementDefinition FromSeed(AchievementDef source, string path)
    {
        string rootPath = string.IsNullOrWhiteSpace(path) ? "$" : path;
        IdentityDefinitionProjection.RequireResource(source, rootPath, nameof(AchievementDef));
        if (source.achievement_id == "")
        {
            throw new InvalidDataException(
                $"Content StringName at '{rootPath}.achievement_id' must not be empty."
            );
        }
        if (source.event_type == "")
        {
            throw new InvalidDataException(
                $"Content StringName at '{rootPath}.event_type' must not be empty."
            );
        }
        if (source.threshold <= 0)
        {
            throw new InvalidDataException(
                $"Content value at '{rootPath}.threshold' must be positive."
            );
        }
        if (source.display_name == null)
            throw new InvalidDataException($"Content string at '{rootPath}.display_name' must not be null.");
        if (source.description == null)
            throw new InvalidDataException($"Content string at '{rootPath}.description' must not be null.");
        if (source.RewardsBorrowed == null)
        {
            throw new InvalidDataException(
                $"Content collection at '{rootPath}.rewards' must not be null."
            );
        }

        IReadOnlyList<AchievementRewardDef> rewardSeeds = source.RewardsBorrowed;
        var rewards = new List<AchievementRewardDefinition>(rewardSeeds.Count);
        for (int index = 0; index < rewardSeeds.Count; index++)
        {
            AchievementRewardDef rewardSeed = rewardSeeds[index];
            if (rewardSeed == null)
            {
                throw new InvalidDataException(
                    $"Content value at '{rootPath}.rewards[{index}]' must be a non-null AchievementRewardDef."
                );
            }
            rewards.Add(
                AchievementRewardDefinition.FromSeed(
                    rewardSeed,
                    $"{rootPath}.rewards[{index}]"
                )
            );
        }

        return new AchievementDefinition(
            source.achievement_id,
            source.display_name,
            source.description,
            source.event_type,
            source.subject_id,
            source.threshold,
            rewards.Count == 0
                ? System.Array.Empty<AchievementRewardDefinition>()
                : new ReadOnlyCollection<AchievementRewardDefinition>(rewards)
        );
    }
}

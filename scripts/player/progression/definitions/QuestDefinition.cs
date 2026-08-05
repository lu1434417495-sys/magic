using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal enum QuestAcceptRequirementKind
{
    Unknown = 0,
    QuestCompleted,
    QuestActive,
    QuestNotCompleted,
}

public sealed class QuestAcceptRequirementDefinition
{
    public QuestAcceptRequirementDefinition(StringName requirementType, StringName questId)
    {
        RequirementType = requirementType;
        QuestId = questId;
    }

    public StringName RequirementType { get; }
    public StringName QuestId { get; }
    internal QuestAcceptRequirementKind RequirementKind =>
        QuestDefinition.ToAcceptRequirementKind(RequirementType);
}

public sealed class QuestObjectiveDefinition
{
    public QuestObjectiveDefinition(
        StringName objectiveId,
        StringName objectiveType,
        StringName targetId,
        int targetValue,
        StringName encounterProfileId = default,
        string encounterDisplayName = "",
        int encounterGrowthStage = 0
    )
    {
        ObjectiveId = objectiveId;
        ObjectiveType = objectiveType;
        TargetId = targetId;
        TargetValue = targetValue;
        EncounterProfileId = encounterProfileId ?? new StringName("");
        EncounterDisplayName = IdentityDefinitionProjection.CopyString(
            encounterDisplayName,
            "QuestObjectiveDefinition.EncounterDisplayName"
        );
        EncounterGrowthStage = encounterGrowthStage;
    }

    public StringName ObjectiveId { get; }
    public StringName ObjectiveType { get; }
    public StringName TargetId { get; }
    public int TargetValue { get; }
    public StringName EncounterProfileId { get; }
    public string EncounterDisplayName { get; }
    public int EncounterGrowthStage { get; }
    internal QuestObjectiveKind ObjectiveKind => QuestDef.ToObjectiveKind(ObjectiveType);
}

public sealed class QuestPendingRewardEntryDefinition
{
    public QuestPendingRewardEntryDefinition(
        StringName entryType,
        StringName targetId,
        int amount
    )
    {
        EntryType = entryType;
        TargetId = targetId;
        Amount = amount;
    }

    public StringName EntryType { get; }
    public StringName TargetId { get; }
    public int Amount { get; }
    internal PendingCharacterRewardEntryKind EntryKind =>
        PendingCharacterRewardContentRules.ToEntryKind(EntryType);
}

public sealed class QuestRewardDefinition
{
    public QuestRewardDefinition(
        StringName rewardType,
        int goldAmount,
        StringName itemId,
        int itemQuantity,
        StringName pendingRewardMemberId,
        IReadOnlyList<QuestPendingRewardEntryDefinition> pendingRewardEntries
    )
    {
        RewardType = rewardType;
        GoldAmount = goldAmount;
        ItemId = itemId;
        ItemQuantity = itemQuantity;
        PendingRewardMemberId = pendingRewardMemberId;
        PendingRewardEntries = IdentityDefinitionProjection.FreezeList(
            pendingRewardEntries,
            "QuestRewardDefinition.PendingRewardEntries"
        );
    }

    public StringName RewardType { get; }
    public int GoldAmount { get; }
    public StringName ItemId { get; }
    public int ItemQuantity { get; }
    public StringName PendingRewardMemberId { get; }
    public IReadOnlyList<QuestPendingRewardEntryDefinition> PendingRewardEntries { get; }
    internal QuestRewardKind RewardKind => QuestDef.ToRewardKind(RewardType);
}

public sealed class QuestDefinition
{
    public QuestDefinition(
        StringName questId,
        string displayName,
        string description,
        StringName providerInteractionId,
        IReadOnlyList<StringName> tags,
        IReadOnlyList<QuestAcceptRequirementDefinition> acceptRequirements,
        IReadOnlyList<QuestObjectiveDefinition> objectives,
        IReadOnlyList<QuestRewardDefinition> rewards,
        bool isRepeatable,
        StringName providerKind,
        IReadOnlyList<StringName> listingChannels,
        string acceptDialogueText,
        string acceptFeedbackSuccess,
        string acceptFeedbackFailure,
        string acceptConfirmationText,
        int dangerTierOverride = 0,
        IReadOnlyList<StringName> listingSettlementIds = null,
        bool canRestartAfterFailure = false
    )
    {
        QuestId = questId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "QuestDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "QuestDefinition.Description"
        );
        ProviderInteractionId = providerInteractionId;
        Tags = IdentityDefinitionProjection.FreezeList(tags, "QuestDefinition.Tags");
        AcceptRequirements = IdentityDefinitionProjection.FreezeList(
            acceptRequirements,
            "QuestDefinition.AcceptRequirements"
        );
        Objectives = IdentityDefinitionProjection.FreezeList(
            objectives,
            "QuestDefinition.Objectives"
        );
        Rewards = IdentityDefinitionProjection.FreezeList(rewards, "QuestDefinition.Rewards");
        IsRepeatable = isRepeatable;
        ProviderKind = providerKind;
        ListingChannels = IdentityDefinitionProjection.FreezeList(
            listingChannels,
            "QuestDefinition.ListingChannels"
        );
        AcceptDialogueText = IdentityDefinitionProjection.CopyString(
            acceptDialogueText,
            "QuestDefinition.AcceptDialogueText"
        );
        AcceptFeedbackSuccess = IdentityDefinitionProjection.CopyString(
            acceptFeedbackSuccess,
            "QuestDefinition.AcceptFeedbackSuccess"
        );
        AcceptFeedbackFailure = IdentityDefinitionProjection.CopyString(
            acceptFeedbackFailure,
            "QuestDefinition.AcceptFeedbackFailure"
        );
        AcceptConfirmationText = IdentityDefinitionProjection.CopyString(
            acceptConfirmationText,
            "QuestDefinition.AcceptConfirmationText"
        );
        if (dangerTierOverride < 0 || dangerTierOverride > 5)
        {
            throw new InvalidDataException(
                $"QuestDefinition {questId} danger_tier_override must be within 0..5."
            );
        }
        DangerTierOverride = dangerTierOverride;
        ListingSettlementIds = IdentityDefinitionProjection.FreezeList(
            listingSettlementIds ?? System.Array.Empty<StringName>(),
            "QuestDefinition.ListingSettlementIds"
        );
        CanRestartAfterFailure = canRestartAfterFailure;
    }

    public StringName QuestId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public StringName ProviderInteractionId { get; }
    public IReadOnlyList<StringName> Tags { get; }
    public IReadOnlyList<QuestAcceptRequirementDefinition> AcceptRequirements { get; }
    public IReadOnlyList<QuestObjectiveDefinition> Objectives { get; }
    public IReadOnlyList<QuestRewardDefinition> Rewards { get; }
    public bool IsRepeatable { get; }
    public StringName ProviderKind { get; }
    public IReadOnlyList<StringName> ListingChannels { get; }
    public string AcceptDialogueText { get; }
    public string AcceptFeedbackSuccess { get; }
    public string AcceptFeedbackFailure { get; }
    public string AcceptConfirmationText { get; }
    public int DangerTierOverride { get; }
    public IReadOnlyList<StringName> ListingSettlementIds { get; }
    public bool CanRestartAfterFailure { get; }

    internal static QuestDefinition FromResource(QuestDef source, string path)
    {
        string rootPath = NormalizePath(path);
        IdentityDefinitionProjection.RequireResource(source, rootPath, nameof(QuestDef));
        RequireNonEmpty(source.quest_id, $"{rootPath}.quest_id");
        RequireNonBlankString(source.display_name, $"{rootPath}.display_name");
        RequireString(source.description, $"{rootPath}.description");
        RequireNonEmpty(
            source.provider_interaction_id,
            $"{rootPath}.provider_interaction_id"
        );
        RequireNonEmpty(source.provider_kind, $"{rootPath}.provider_kind");
        QuestFailurePolicyKind failurePolicy = QuestFailurePolicyRules.ToKind(
            source.failure_policy
        );
        if (failurePolicy == QuestFailurePolicyKind.Unknown)
        {
            throw new InvalidDataException(
                $"Content value at '{rootPath}.failure_policy' must be terminal or restartable."
            );
        }
        RequireString(source.accept_dialogue_text, $"{rootPath}.accept_dialogue_text");
        RequireString(
            source.accept_feedback_success,
            $"{rootPath}.accept_feedback_success"
        );
        RequireString(
            source.accept_feedback_failure,
            $"{rootPath}.accept_feedback_failure"
        );
        RequireString(
            source.accept_confirmation_text,
            $"{rootPath}.accept_confirmation_text"
        );

        IReadOnlyList<StringName> tags = IdentityDefinitionProjection.CopyStringNames(
            source.TagsBorrowed,
            $"{rootPath}.tags"
        );
        IReadOnlyList<StringName> listingChannels =
            IdentityDefinitionProjection.CopyStringNames(
                source.ListingChannelsBorrowed,
                $"{rootPath}.listing_channels"
            );
        if (listingChannels.Count == 0)
        {
            throw new InvalidDataException(
                $"Content collection at '{rootPath}.listing_channels' must not be empty."
            );
        }
        for (int index = 0; index < listingChannels.Count; index++)
            RequireNonEmpty(listingChannels[index], $"{rootPath}.listing_channels[{index}]");

        IReadOnlyList<StringName> listingSettlementIds =
            IdentityDefinitionProjection.CopyStringNames(
                source.ListingSettlementIdsBorrowed,
                $"{rootPath}.listing_settlement_ids"
            );
        for (int index = 0; index < listingSettlementIds.Count; index++)
            RequireNonEmpty(
                listingSettlementIds[index],
                $"{rootPath}.listing_settlement_ids[{index}]"
            );

        EnsureCollection(source.AcceptRequirementsBorrowed, $"{rootPath}.accept_requirements");
        EnsureCollection(source.ObjectiveDefsBorrowed, $"{rootPath}.objective_defs");
        EnsureCollection(source.RewardEntriesBorrowed, $"{rootPath}.reward_entries");

        IReadOnlyList<QuestAcceptRequirementDefinition> acceptRequirements =
            ProjectAcceptRequirements(
                source.GetAcceptRequirementEntriesTyped(),
                $"{rootPath}.accept_requirements"
            );
        IReadOnlyList<QuestObjectiveDefinition> objectives = ProjectObjectives(
            source.GetObjectiveEntriesTyped(),
            $"{rootPath}.objective_defs"
        );
        if (objectives.Count == 0)
        {
            throw new InvalidDataException(
                $"Content collection at '{rootPath}.objective_defs' must not be empty."
            );
        }
        IReadOnlyList<QuestRewardDefinition> rewards = ProjectRewards(
            source.GetRewardEntriesTyped(),
            $"{rootPath}.reward_entries"
        );

        return new QuestDefinition(
            source.quest_id,
            source.display_name,
            source.description,
            source.provider_interaction_id,
            tags,
            acceptRequirements,
            objectives,
            rewards,
            source.is_repeatable,
            source.provider_kind,
            listingChannels,
            source.accept_dialogue_text,
            source.accept_feedback_success,
            source.accept_feedback_failure,
            source.accept_confirmation_text,
            source.danger_tier_override,
            listingSettlementIds,
            failurePolicy == QuestFailurePolicyKind.Restartable
        );
    }

    internal static QuestAcceptRequirementKind ToAcceptRequirementKind(StringName value)
    {
        if (value == "quest_completed")
            return QuestAcceptRequirementKind.QuestCompleted;
        if (value == "quest_active")
            return QuestAcceptRequirementKind.QuestActive;
        if (value == "quest_not_completed")
            return QuestAcceptRequirementKind.QuestNotCompleted;
        return QuestAcceptRequirementKind.Unknown;
    }

    private static IReadOnlyList<QuestAcceptRequirementDefinition> ProjectAcceptRequirements(
        IReadOnlyList<QuestDef.AcceptRequirementEntryData> source,
        string path
    )
    {
        EnsureCollection(source, path);
        if (source.Count == 0)
            return System.Array.Empty<QuestAcceptRequirementDefinition>();
        var result = new List<QuestAcceptRequirementDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestDef.AcceptRequirementEntryData entry = source[index];
            if (entry == null)
                throw MissingNested(path, index, nameof(QuestDef.AcceptRequirementEntryData));
            string entryPath = $"{path}[{index}]";
            RequireNonEmpty(entry.RequirementType, $"{entryPath}.requirement_type");
            if (ToAcceptRequirementKind(entry.RequirementType) == QuestAcceptRequirementKind.Unknown)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.requirement_type' has unsupported quest requirement type '{entry.RequirementType}'."
                );
            }
            RequireNonEmpty(entry.QuestId, $"{entryPath}.quest_id");
            result.Add(
                new QuestAcceptRequirementDefinition(entry.RequirementType, entry.QuestId)
            );
        }
        return new ReadOnlyCollection<QuestAcceptRequirementDefinition>(result);
    }

    private static IReadOnlyList<QuestObjectiveDefinition> ProjectObjectives(
        IReadOnlyList<QuestDef.ObjectiveEntryData> source,
        string path
    )
    {
        EnsureCollection(source, path);
        if (source.Count == 0)
            return System.Array.Empty<QuestObjectiveDefinition>();
        var result = new List<QuestObjectiveDefinition>(source.Count);
        bool hasEncounterBinding = false;
        for (int index = 0; index < source.Count; index++)
        {
            QuestDef.ObjectiveEntryData entry = source[index];
            if (entry == null)
                throw MissingNested(path, index, nameof(QuestDef.ObjectiveEntryData));
            string entryPath = $"{path}[{index}]";
            RequireNonEmpty(entry.ObjectiveId, $"{entryPath}.objective_id");
            RequireNonEmpty(entry.ObjectiveType, $"{entryPath}.objective_type");
            QuestObjectiveKind objectiveKind = QuestDef.ToObjectiveKind(entry.ObjectiveType);
            if (objectiveKind == QuestObjectiveKind.Unknown)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.objective_type' has unsupported quest objective type '{entry.ObjectiveType}'."
                );
            }
            if (!entry.HasStrictTargetValue)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.target_value' must be Int."
                );
            }
            if (entry.TargetValue <= 0)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.target_value' must be positive."
                );
            }
            if (
                objectiveKind == QuestObjectiveKind.SubmitItem
                || objectiveKind == QuestObjectiveKind.DefeatEnemyInSingleBattle
                || objectiveKind == QuestObjectiveKind.SettlementAction
            )
            {
                RequireNonEmpty(entry.TargetId, $"{entryPath}.target_id");
            }
            bool hasEncounterProfile = entry.EncounterProfileId != "";
            bool hasEncounterDisplayName = !string.IsNullOrWhiteSpace(
                entry.EncounterDisplayName
            );
            if (
                entry.HasEncounterGrowthStage
                && !entry.HasStrictEncounterGrowthStage
            )
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.encounter_growth_stage' must be Int."
                );
            }
            if (
                entry.HasStrictEncounterGrowthStage
                && entry.EncounterGrowthStage < 0
            )
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.encounter_growth_stage' must be non-negative."
                );
            }
            if (hasEncounterProfile != hasEncounterDisplayName)
            {
                throw new InvalidDataException(
                    $"Content values at '{entryPath}.encounter_profile_id' and '{entryPath}.encounter_display_name' must be configured together."
                );
            }
            if (entry.HasEncounterGrowthStage && !hasEncounterProfile)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.encounter_growth_stage' requires an encounter binding."
                );
            }
            if (
                hasEncounterProfile
                && objectiveKind
                    is not QuestObjectiveKind.DefeatEnemy
                        and not QuestObjectiveKind.DefeatEnemyInSingleBattle
            )
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.encounter_profile_id' is only supported for enemy defeat objectives."
                );
            }
            if (hasEncounterProfile && hasEncounterBinding)
            {
                throw new InvalidDataException(
                    $"Content collection at '{path}' may contain at most one encounter binding."
                );
            }
            hasEncounterBinding |= hasEncounterProfile;
            result.Add(
                new QuestObjectiveDefinition(
                    entry.ObjectiveId,
                    entry.ObjectiveType,
                    entry.TargetId,
                    entry.TargetValue,
                    entry.EncounterProfileId,
                    entry.EncounterDisplayName,
                    entry.EncounterGrowthStage
                )
            );
        }
        return new ReadOnlyCollection<QuestObjectiveDefinition>(result);
    }

    private static IReadOnlyList<QuestRewardDefinition> ProjectRewards(
        IReadOnlyList<QuestDef.RewardEntryData> source,
        string path
    )
    {
        EnsureCollection(source, path);
        if (source.Count == 0)
            return System.Array.Empty<QuestRewardDefinition>();
        var result = new List<QuestRewardDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestDef.RewardEntryData entry = source[index];
            if (entry == null)
                throw MissingNested(path, index, nameof(QuestDef.RewardEntryData));
            string entryPath = $"{path}[{index}]";
            RequireNonEmpty(entry.RewardType, $"{entryPath}.reward_type");
            QuestRewardKind rewardKind = QuestDef.ToRewardKind(entry.RewardType);
            if (rewardKind == QuestRewardKind.Unknown)
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.reward_type' has unsupported quest reward type '{entry.RewardType}'."
                );
            }

            int goldAmount = 0;
            StringName itemId = "";
            int itemQuantity = 0;
            StringName pendingMemberId = "";
            IReadOnlyList<QuestPendingRewardEntryDefinition> pendingEntries =
                System.Array.Empty<QuestPendingRewardEntryDefinition>();
            if (rewardKind == QuestRewardKind.Gold)
            {
                if (!entry.HasStrictGoldAmount)
                    throw new InvalidDataException($"Content value at '{entryPath}.amount' must be Int.");
                if (entry.GoldAmount <= 0)
                    throw new InvalidDataException($"Content value at '{entryPath}.amount' must be positive.");
                goldAmount = entry.GoldAmount;
            }
            else if (rewardKind == QuestRewardKind.Item)
            {
                RequireNonEmpty(entry.ItemId, $"{entryPath}.item_id");
                if (!entry.HasStrictItemQuantity)
                    throw new InvalidDataException($"Content value at '{entryPath}.quantity' must be Int.");
                if (entry.ItemQuantity <= 0)
                    throw new InvalidDataException($"Content value at '{entryPath}.quantity' must be positive.");
                itemId = entry.ItemId;
                itemQuantity = entry.ItemQuantity;
            }
            else
            {
                RequireNonEmpty(entry.PendingRewardMemberId, $"{entryPath}.member_id");
                pendingMemberId = entry.PendingRewardMemberId;
                pendingEntries = ProjectPendingRewardEntries(
                    entry.PendingRewardEntries,
                    $"{entryPath}.entries"
                );
                if (pendingEntries.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Content collection at '{entryPath}.entries' must not be empty."
                    );
                }
            }

            result.Add(
                new QuestRewardDefinition(
                    entry.RewardType,
                    goldAmount,
                    itemId,
                    itemQuantity,
                    pendingMemberId,
                    pendingEntries
                )
            );
        }
        return new ReadOnlyCollection<QuestRewardDefinition>(result);
    }

    private static IReadOnlyList<QuestPendingRewardEntryDefinition> ProjectPendingRewardEntries(
        IReadOnlyList<QuestDef.PendingRewardEntryData> source,
        string path
    )
    {
        EnsureCollection(source, path);
        if (source.Count == 0)
            return System.Array.Empty<QuestPendingRewardEntryDefinition>();
        var result = new List<QuestPendingRewardEntryDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestDef.PendingRewardEntryData entry = source[index];
            string entryPath = $"{path}[{index}]";
            if (entry == null || !entry.IsDictionaryEntry)
                throw MissingNested(path, index, "Dictionary");
            RequireNonEmpty(entry.EntryType, $"{entryPath}.entry_type");
            if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entry.EntryType))
            {
                throw new InvalidDataException(
                    $"Content value at '{entryPath}.entry_type' has unsupported pending reward type '{entry.EntryType}'."
                );
            }
            RequireNonEmpty(entry.TargetId, $"{entryPath}.target_id");
            if (!entry.HasStrictAmount)
                throw new InvalidDataException($"Content value at '{entryPath}.amount' must be Int.");
            if (entry.Amount == 0)
                throw new InvalidDataException($"Content value at '{entryPath}.amount' must not be zero.");
            result.Add(
                new QuestPendingRewardEntryDefinition(
                    entry.EntryType,
                    entry.TargetId,
                    entry.Amount
                )
            );
        }
        return new ReadOnlyCollection<QuestPendingRewardEntryDefinition>(result);
    }

    private static void EnsureCollection(object source, string path)
    {
        if (source == null)
            throw new InvalidDataException($"Content collection at '{path}' must not be null.");
    }

    private static void RequireNonEmpty(StringName value, string path)
    {
        if (value == "")
            throw new InvalidDataException($"Content StringName at '{path}' must not be empty.");
    }

    private static string RequireNonBlankString(string value, string path)
    {
        RequireString(value, path);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Content string at '{path}' must not be blank.");
        return value;
    }

    private static string RequireString(string value, string path) =>
        value
        ?? throw new InvalidDataException($"Content string at '{path}' must not be null.");

    private static InvalidDataException MissingNested(
        string path,
        int index,
        string expectedType
    ) =>
        new(
            $"Content value at '{path}[{index}]' must be a non-null {expectedType}."
        );

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? "$" : path;
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal sealed class FaithContentRegistry
{
    private const string ConfigDirectory = "res://data/configs/faith";

    private readonly Dictionary<StringName, FaithDeityDefinition> _faithDeityDefs = new();
    private readonly List<string> _validationErrors = new();

    public void Rebuild()
    {
        LoadFromDirectory(ConfigDirectory);
    }

    internal void LoadFromDirectory(string directoryPath)
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();
        ScanDirectory(directoryPath);
        CollectValidationErrorsInto(_validationErrors);
    }

    internal IReadOnlyDictionary<StringName, FaithDeityDefinition> GetFaithDeityDefsTyped() =>
        new ReadOnlyDictionary<StringName, FaithDeityDefinition>(
            new Dictionary<StringName, FaithDeityDefinition>(_faithDeityDefs)
        );

    internal FaithDeityDefinition GetFaithDeityDef(StringName deityId) =>
        deityId != "" && _faithDeityDefs.TryGetValue(deityId, out FaithDeityDefinition definition)
            ? definition
            : null;

    internal IReadOnlyList<string> GetValidationErrors() =>
        new List<string>(_validationErrors);

    private void ScanDirectory(string directoryPath)
    {
        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"FaithService could not open {directoryPath}.");
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterDeityResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterDeityResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load faith config {resourcePath}.");
            return;
        }
        if (resource is not FaithDeityDef deityDef)
        {
            _validationErrors.Add(
                $"Faith config {resourcePath} failed to cast to FaithDeityDef."
            );
            return;
        }
        if (deityDef.deity_id == "")
        {
            _validationErrors.Add($"Faith config {resourcePath} is missing deity_id.");
            return;
        }
        if (_faithDeityDefs.ContainsKey(deityDef.deity_id))
        {
            _validationErrors.Add($"Duplicate faith deity_id registered: {deityDef.deity_id}");
            return;
        }

        GodotContentOwnership.RegisterBorrowedContent(deityDef, resourcePath);
        try
        {
            FaithDeityDefinition definition = FaithDeityDefinition.FromResource(
                deityDef,
                resourcePath
            );
            _faithDeityDefs.Add(definition.DeityId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validationErrors.Add(
                $"Faith config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private void CollectValidationErrorsInto(ICollection<string> errors)
    {
        var sortedIds = new List<string>();
        foreach (StringName deityId in _faithDeityDefs.Keys)
            sortedIds.Add(deityId.ToString());
        sortedIds.Sort();

        foreach (string deityIdText in sortedIds)
        {
            FaithDeityDefinition deityDef = GetFaithDeityDef(deityIdText);
            if (deityDef != null)
                AppendDeityValidationErrors(errors, deityDef);
        }
    }

    private static void AppendDeityValidationErrors(
        ICollection<string> errors,
        FaithDeityDefinition deityDef
    )
    {
        if (deityDef.DeityId == "")
            errors.Add("Faith deity config is missing deity_id.");
        if (deityDef.DisplayName.Length == 0)
            errors.Add($"Faith deity {deityDef.DeityId} is missing display_name.");
        if (deityDef.RankProgressStatId == "")
            errors.Add($"Faith deity {deityDef.DeityId} is missing rank_progress_stat_id.");
        if (deityDef.RankDefinitions.Count == 0)
            errors.Add($"Faith deity {deityDef.DeityId} must declare at least one rank_def.");

        var seenRanks = new HashSet<int>();
        foreach (FaithRankDefinition rankDef in deityDef.RankDefinitions)
        {
            if (rankDef == null)
            {
                errors.Add($"Faith deity {deityDef.DeityId} contains a null rank_def.");
                continue;
            }
            if (!seenRanks.Add(rankDef.RankIndex))
            {
                errors.Add(
                    $"Faith deity {deityDef.DeityId} declares duplicate rank {rankDef.RankIndex}."
                );
                continue;
            }

            AppendRankValidationErrors(errors, deityDef.DeityId, rankDef);
            if (
                deityDef.RankProgressStatId != ""
                && !HasRankProgressReward(rankDef, deityDef.RankProgressStatId)
            )
            {
                errors.Add(
                    $"Faith deity {deityDef.DeityId} rank {rankDef.RankIndex} is missing rank progress reward {deityDef.RankProgressStatId}."
                );
            }
        }

        int maxRank = deityDef.GetMaxRank();
        for (int expected = 1; expected <= maxRank; expected++)
        {
            if (!seenRanks.Contains(expected))
                errors.Add($"Faith deity {deityDef.DeityId} is missing rank {expected}.");
        }
    }

    private static void AppendRankValidationErrors(
        ICollection<string> errors,
        StringName deityId,
        FaithRankDefinition rankDef
    )
    {
        string prefix = $"Faith deity {deityId}: ";
        if (rankDef.RankIndex <= 0)
            errors.Add(prefix + "Faith rank must have rank_index >= 1.");
        if (string.IsNullOrEmpty(rankDef.RankName))
            errors.Add(prefix + $"Faith rank {rankDef.RankIndex} is missing rank_name.");
        if (rankDef.RequiredGold < 0)
        {
            errors.Add(
                prefix
                    + $"Faith rank {rankDef.RankIndex} uses negative required_gold {rankDef.RequiredGold}."
            );
        }
        if (rankDef.RequiredLevel < 0)
        {
            errors.Add(
                prefix
                    + $"Faith rank {rankDef.RankIndex} uses negative required_level {rankDef.RequiredLevel}."
            );
        }
        if (rankDef.HasCustomStatRequirement() && rankDef.HasAchievementRequirement())
        {
            errors.Add(
                prefix
                    + $"Faith rank {rankDef.RankIndex} should not mix custom stat and achievement placeholder gates."
            );
        }
        if (rankDef.RequiredCustomStatId == "" && rankDef.RequiredCustomStatMinValue != 0)
        {
            errors.Add(
                prefix
                    + $"Faith rank {rankDef.RankIndex} sets required_custom_stat_min_value without required_custom_stat_id."
            );
        }
        if (rankDef.RewardEntries.Count == 0)
        {
            errors.Add(
                prefix + $"Faith rank {rankDef.RankIndex} must define at least one reward entry."
            );
        }

        foreach (FaithRankRewardEntryDefinition rewardSpec in rankDef.RewardEntries)
        {
            if (
                rewardSpec == null
                || rewardSpec.EntryType == ""
                || rewardSpec.TargetId == ""
                || rewardSpec.Amount == 0
            )
            {
                errors.Add(
                    prefix
                        + $"Faith rank {rankDef.RankIndex} contains an invalid reward entry."
                );
                continue;
            }
            if (!PendingCharacterRewardContentRules.IsSupportedEntryType(rewardSpec.EntryType))
            {
                errors.Add(
                    prefix
                        + $"Faith rank {rankDef.RankIndex} contains unsupported reward entry_type {rewardSpec.EntryType}."
                );
                continue;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(rewardSpec.EntryType)
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                    rewardSpec.TargetId
                )
            )
            {
                errors.Add(
                    prefix
                        + $"Faith rank {rankDef.RankIndex} attribute_progress reward references unsupported attribute {rewardSpec.TargetId}."
                );
            }
        }
    }

    private static bool HasRankProgressReward(
        FaithRankDefinition rankDef,
        StringName rankProgressStatId
    )
    {
        if (rankDef == null || rankProgressStatId == "")
            return false;
        foreach (FaithRankRewardEntryDefinition entry in rankDef.RewardEntries)
        {
            if (
                entry != null
                && entry.EntryType == "attribute_delta"
                && entry.TargetId == rankProgressStatId
            )
            {
                return true;
            }
        }
        return false;
    }
}

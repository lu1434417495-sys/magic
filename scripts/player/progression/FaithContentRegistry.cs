using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class FaithContentRegistry
{
    private const string ConfigDirectory = ProfessionIdentityJsonDomains.FaithDirectory;

    private readonly Dictionary<StringName, FaithDeityDefinition> _faithDeityDefs = new();
    private readonly List<string> _validationErrors = new();
    private readonly IContentJsonSourceReader _jsonSourceReader;

    internal FaithContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal FaithContentRegistry(IContentJsonSourceReader jsonSourceReader)
    {
        _jsonSourceReader = jsonSourceReader
            ?? throw new System.ArgumentNullException(nameof(jsonSourceReader));
    }

    public void Rebuild()
    {
        LoadFromDirectory(ConfigDirectory);
    }

    internal void LoadFromDirectory(string directoryPath)
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();
        ImportDirectory(directoryPath);
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

    private void ImportDirectory(string directoryPath)
    {
        ContentImportBatch<FaithImportModel> batch = ProfessionIdentityJsonImport
            .CreateFaithDescriptor(directoryPath, _jsonSourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validationErrors.Add(ProfessionIdentityJsonImport.FormatDiagnostic(diagnostic));
        foreach (ContentImportEntry<FaithImportModel> entry in batch.Entries)
        {
            try
            {
                FaithDeityDefinition definition = ProfessionIdentityDefinitionProjector.Project(entry.Import);
                if (!_faithDeityDefs.TryAdd(definition.DeityId, definition))
                    _validationErrors.Add($"Duplicate faith deity_id registered: {definition.DeityId}");
            }
            catch (System.Exception exception)
                when (exception is System.IO.InvalidDataException
                    or System.InvalidOperationException)
            {
                _validationErrors.Add(
                    $"Faith JSON {entry.Context.SourceLabel} projection failed: {exception.GetType().Name}: {exception.Message}"
                );
            }
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

using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiCandidateRequest : RefCounted
{
    private static readonly StringName _familyMoveToRange = "move_to_range";

    public StringName FamilyMoveToRange => _familyMoveToRange;

    private static readonly HashSet<string> PathBudgetKeys = new()
    {
        "max_cost",
        "max_nodes",
        "max_destinations",
        "path_tree_min_destination_count",
        "include_origin",
        "prefer_progress",
    };

    private static readonly HashSet<string> MoveToRangeTacticalKeys = new()
    {
        "target_selector",
        "range_skill_ids",
        "position_objective_kind",
        "aoe_setup_enabled",
        "aoe_setup_min_target_count",
        "aoe_setup_target_count_weight",
        "aoe_setup_improvement_weight",
        "aoe_setup_friendly_fire_penalty",
    };

    private static readonly HashSet<string> MoveToRangeRuntimeKeys = new()
    {
        "configured_desired_min_distance",
        "configured_desired_max_distance",
        "effective_attack_range",
    };

    public StringName FamilyId = "";

    public StringName ActionId = "";

    public string ActionLabel = "";

    public StringName ActionIntent = "";

    public StringName ScoreBucketId = "";

    public StringName ActorUnitId = "";

    public StringName FocusTargetUnitId = "";

    public int DesiredMinDistance = 0;

    public int DesiredMaxDistance = 0;

    public int MaxCandidateCount = 0;

    private MoveToRangePathSearchBudget _pathSearchBudget = new();

    private MoveToRangeTacticalParams _tacticalParams = new();

    private MoveToRangeRuntimeMetadata _runtimeMetadata = new();

    private string _pathSearchBudgetError = "path_search_budget.max_cost must be int.";

    private string _tacticalParamsError = "";

    private string _runtimeMetadataError = "";

    internal MoveToRangePathSearchBudget ParsedPathSearchBudget { get; private set; } = new();

    internal MoveToRangeTacticalParams ParsedTacticalParams { get; private set; } = new();

    internal MoveToRangeRuntimeMetadata ParsedRuntimeMetadata { get; private set; } = new();

    public StringName family_id
    {
        get => FamilyId;
        set => FamilyId = value;
    }
    public StringName action_id
    {
        get => ActionId;
        set => ActionId = value;
    }
    public string action_label
    {
        get => ActionLabel;
        set => ActionLabel = value ?? "";
    }
    public StringName action_intent
    {
        get => ActionIntent;
        set => ActionIntent = value;
    }
    public StringName score_bucket_id
    {
        get => ScoreBucketId;
        set => ScoreBucketId = value;
    }
    public StringName actor_unit_id
    {
        get => ActorUnitId;
        set => ActorUnitId = value;
    }
    public StringName focus_target_unit_id
    {
        get => FocusTargetUnitId;
        set => FocusTargetUnitId = value;
    }
    public int desired_min_distance
    {
        get => DesiredMinDistance;
        set => DesiredMinDistance = value;
    }
    public int desired_max_distance
    {
        get => DesiredMaxDistance;
        set => DesiredMaxDistance = value;
    }
    public int max_candidate_count
    {
        get => MaxCandidateCount;
        set => MaxCandidateCount = value;
    }
    public GDictionary PathSearchBudget
    {
        get => _pathSearchBudget.ToDictionary();
        set
        {
            if (TryReadPathBudgetDictionary(value, out MoveToRangePathSearchBudget parsed, out string error))
            {
                _pathSearchBudget = parsed;
                _pathSearchBudgetError = "";
                return;
            }
            _pathSearchBudget = parsed ?? new MoveToRangePathSearchBudget();
            _pathSearchBudgetError = error;
        }
    }
    public GDictionary TacticalParams
    {
        get => _tacticalParams.ToDictionary();
        set
        {
            if (TryReadTacticalParamsDictionary(value, out MoveToRangeTacticalParams parsed, out string error))
            {
                _tacticalParams = parsed;
                _tacticalParamsError = "";
                return;
            }
            _tacticalParams = parsed ?? new MoveToRangeTacticalParams();
            _tacticalParamsError = error;
        }
    }
    public GDictionary RuntimeMetadata
    {
        get => _runtimeMetadata.ToDictionary();
        set
        {
            if (TryReadRuntimeMetadataDictionary(value, out MoveToRangeRuntimeMetadata parsed, out string error))
            {
                _runtimeMetadata = parsed;
                _runtimeMetadataError = "";
                return;
            }
            _runtimeMetadata = parsed ?? new MoveToRangeRuntimeMetadata();
            _runtimeMetadataError = error;
        }
    }
    public GDictionary path_search_budget
    {
        get => PathSearchBudget;
        set => PathSearchBudget = value;
    }
    public GDictionary tactical_params
    {
        get => TacticalParams;
        set => TacticalParams = value;
    }
    public GDictionary runtime_metadata
    {
        get => RuntimeMetadata;
        set => RuntimeMetadata = value;
    }

    internal void SetMoveToRangeSections(
        MoveToRangePathSearchBudget pathSearchBudget,
        MoveToRangeTacticalParams tacticalParams,
        MoveToRangeRuntimeMetadata runtimeMetadata
    )
    {
        _pathSearchBudget = pathSearchBudget?.Clone() ?? new MoveToRangePathSearchBudget();
        _tacticalParams = tacticalParams?.Clone() ?? new MoveToRangeTacticalParams();
        _runtimeMetadata = runtimeMetadata?.Clone() ?? new MoveToRangeRuntimeMetadata();
        _pathSearchBudgetError = "";
        _tacticalParamsError = "";
        _runtimeMetadataError = "";
    }

    public bool RequireValidPayload()
    {
        if (FamilyId != _familyMoveToRange)
            return Fail($"Unsupported candidate family_id {FamilyId}.");

        if (ActionId == "")
            return Fail("BattleAiCandidateRequest action_id must not be empty.");

        if (ActorUnitId == "")
            return Fail("BattleAiCandidateRequest actor_unit_id must not be empty.");

        if (ActionIntent == "" || !BattleAiActionIntent.is_valid(ActionIntent))
            return Fail($"BattleAiCandidateRequest action_intent is unsupported: {ActionIntent}.");

        if (
            DesiredMinDistance < 0
            || DesiredMaxDistance < 0
            || DesiredMinDistance > DesiredMaxDistance
        )
            return Fail("BattleAiCandidateRequest desired distance range is invalid.");

        if (MaxCandidateCount <= 0)
            return Fail("BattleAiCandidateRequest max_candidate_count must be > 0.");

        if (!TryParsePathBudget(out MoveToRangePathSearchBudget parsedPathBudget, out string error))
            return Fail(error);
        ParsedPathSearchBudget = parsedPathBudget;

        if (
            parsedPathBudget.MaxDestinations > 0
            && MaxCandidateCount > parsedPathBudget.MaxDestinations
        )
            return Fail("max_candidate_count must not exceed path_search_budget.max_destinations.");

        if (
            !TryParseMoveToRangeTacticalParams(
                out MoveToRangeTacticalParams parsedTacticalParams,
                out error
            )
        )
            return Fail(error);
        ParsedTacticalParams = parsedTacticalParams;

        if (!TryParseRuntimeMetadata(out MoveToRangeRuntimeMetadata parsedRuntimeMetadata, out error))
            return Fail(error);
        ParsedRuntimeMetadata = parsedRuntimeMetadata;

        return true;
    }

    internal bool TryGetMoveToRangeSections(
        out MoveToRangePathSearchBudget pathBudget,
        out MoveToRangeTacticalParams tacticalParams,
        out MoveToRangeRuntimeMetadata runtimeMetadata,
        out string error
    )
    {
        pathBudget = null;
        tacticalParams = null;
        runtimeMetadata = null;
        error = "";
        if (!TryParsePathBudget(out pathBudget, out error))
            return false;
        if (!TryParseMoveToRangeTacticalParams(out tacticalParams, out error))
            return false;
        if (!TryParseRuntimeMetadata(out runtimeMetadata, out error))
            return false;
        if (pathBudget.MaxDestinations > 0 && MaxCandidateCount > pathBudget.MaxDestinations)
        {
            error = "max_candidate_count must not exceed path_search_budget.max_destinations.";
            return false;
        }
        return true;
    }

    private bool TryParsePathBudget(
        out MoveToRangePathSearchBudget pathBudget,
        out string error
    )
    {
        pathBudget = _pathSearchBudget.Clone();
        error = _pathSearchBudgetError;
        if (!string.IsNullOrEmpty(error))
            return false;
        return ValidatePathBudget(pathBudget, out error);
    }

    private bool TryParseMoveToRangeTacticalParams(
        out MoveToRangeTacticalParams tacticalParams,
        out string error
    )
    {
        tacticalParams = _tacticalParams.Clone();
        error = _tacticalParamsError;
        if (!string.IsNullOrEmpty(error))
            return false;
        return ValidateTacticalParams(tacticalParams, out error);
    }

    private bool TryParseRuntimeMetadata(
        out MoveToRangeRuntimeMetadata runtimeMetadata,
        out string error
    )
    {
        runtimeMetadata = _runtimeMetadata.Clone();
        error = _runtimeMetadataError;
        return string.IsNullOrEmpty(error);
    }

    private static bool ValidatePathBudget(
        MoveToRangePathSearchBudget pathBudget,
        out string error
    )
    {
        error = "";
        if (pathBudget == null)
        {
            error = "path_search_budget must be set.";
            return false;
        }
        if (pathBudget.MaxCost < 0)
        {
            error = "path_search_budget.max_cost must be int >= 0.";
            return false;
        }
        if (pathBudget.MaxNodes < 0)
        {
            error = "path_search_budget.max_nodes must be int >= 0.";
            return false;
        }
        if (pathBudget.MaxDestinations < 0)
        {
            error = "path_search_budget.max_destinations must be int >= 0.";
            return false;
        }
        if (pathBudget.PathTreeMinDestinationCount < 0)
        {
            error = "path_search_budget.path_tree_min_destination_count must be int >= 0.";
            return false;
        }
        return true;
    }

    private static bool ValidateTacticalParams(
        MoveToRangeTacticalParams tacticalParams,
        out string error
    )
    {
        error = "";
        if (tacticalParams == null)
        {
            error = "tactical_params must be set.";
            return false;
        }
        if (tacticalParams.AoeSetupMinTargetCount < 0)
        {
            error = "tactical_params.aoe_setup_min_target_count must be int >= 0.";
            return false;
        }
        if (tacticalParams.AoeSetupTargetCountWeight < 0)
        {
            error = "tactical_params.aoe_setup_target_count_weight must be int >= 0.";
            return false;
        }
        if (tacticalParams.AoeSetupImprovementWeight < 0)
        {
            error = "tactical_params.aoe_setup_improvement_weight must be int >= 0.";
            return false;
        }
        if (tacticalParams.AoeSetupFriendlyFirePenalty < 0)
        {
            error = "tactical_params.aoe_setup_friendly_fire_penalty must be int >= 0.";
            return false;
        }
        return true;
    }

    private static bool TryReadPathBudgetDictionary(
        GDictionary source,
        out MoveToRangePathSearchBudget pathBudget,
        out string error
    )
    {
        pathBudget = new MoveToRangePathSearchBudget();
        error = "";
        if (!ValidateKnownKeys(source, PathBudgetKeys, "path_search_budget", out error))
            return false;
        if (!TryReadRequiredInt(source, "path_search_budget", "max_cost", out pathBudget.MaxCost, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "path_search_budget", "max_nodes", out pathBudget.MaxNodes, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "path_search_budget", "max_destinations", out pathBudget.MaxDestinations, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "path_search_budget", "path_tree_min_destination_count", out pathBudget.PathTreeMinDestinationCount, out error))
            return false;
        if (!TryReadOptionalBool(source, "path_search_budget", "include_origin", out pathBudget.IncludeOrigin, out error))
            return false;
        return TryReadOptionalBool(source, "path_search_budget", "prefer_progress", out pathBudget.PreferProgress, out error, true)
            && ValidatePathBudget(pathBudget, out error);
    }

    private static bool TryReadTacticalParamsDictionary(
        GDictionary source,
        out MoveToRangeTacticalParams tacticalParams,
        out string error
    )
    {
        tacticalParams = new MoveToRangeTacticalParams();
        error = "";
        if (!ValidateKnownKeys(source, MoveToRangeTacticalKeys, "tactical_params", out error))
            return false;
        if (!TryReadOptionalStringName(source, "tactical_params", "target_selector", out tacticalParams.TargetSelector, out error))
            return false;
        if (!TryReadOptionalStringName(source, "tactical_params", "position_objective_kind", out tacticalParams.PositionObjectiveKind, out error, "distance_band_progress"))
            return false;
        if (!TryReadOptionalStringNameList(source, "tactical_params", "range_skill_ids", tacticalParams.RangeSkillIds, out error))
            return false;
        if (!TryReadOptionalBool(source, "tactical_params", "aoe_setup_enabled", out tacticalParams.AoeSetupEnabled, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "tactical_params", "aoe_setup_min_target_count", out tacticalParams.AoeSetupMinTargetCount, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "tactical_params", "aoe_setup_target_count_weight", out tacticalParams.AoeSetupTargetCountWeight, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "tactical_params", "aoe_setup_improvement_weight", out tacticalParams.AoeSetupImprovementWeight, out error))
            return false;
        if (!TryReadOptionalNonNegativeInt(source, "tactical_params", "aoe_setup_friendly_fire_penalty", out tacticalParams.AoeSetupFriendlyFirePenalty, out error))
            return false;
        return ValidateTacticalParams(tacticalParams, out error);
    }

    private static bool TryReadRuntimeMetadataDictionary(
        GDictionary source,
        out MoveToRangeRuntimeMetadata runtimeMetadata,
        out string error
    )
    {
        runtimeMetadata = new MoveToRangeRuntimeMetadata();
        error = "";
        if (!ValidateKnownKeys(source, MoveToRangeRuntimeKeys, "runtime_metadata", out error))
            return false;
        if (!TryReadOptionalInt(source, "runtime_metadata", "configured_desired_min_distance", out runtimeMetadata.ConfiguredDesiredMinDistance, out error))
            return false;
        if (!TryReadOptionalInt(source, "runtime_metadata", "configured_desired_max_distance", out runtimeMetadata.ConfiguredDesiredMaxDistance, out error))
            return false;
        return TryReadOptionalInt(source, "runtime_metadata", "effective_attack_range", out runtimeMetadata.EffectiveAttackRange, out error);
    }

    private static bool ValidateKnownKeys(
        GDictionary source,
        HashSet<string> allowedKeys,
        string path,
        out string error
    )
    {
        error = "";
        if (source == null)
            return true;
        foreach (var rawKey in source.Keys)
        {
            string key = ReadDictionaryKey(rawKey);
            if (!string.IsNullOrEmpty(key) && allowedKeys.Contains(key))
                continue;
            error = $"Unsupported {path} key {key}.";
            return false;
        }
        return true;
    }

    private static bool TryReadRequiredInt(
        GDictionary source,
        string path,
        string key,
        out int value,
        out string error
    )
    {
        error = "";
        if (TryReadIntValue(source, key, out value))
            return true;
        value = 0;
        error = $"{path}.{key} must be int.";
        return false;
    }

    private static bool TryReadOptionalNonNegativeInt(
        GDictionary source,
        string path,
        string key,
        out int value,
        out string error,
        int defaultValue = 0
    )
    {
        if (!TryReadOptionalInt(source, path, key, out value, out error, defaultValue))
            return false;
        if (value >= 0)
            return true;
        error = $"{path}.{key} must be int >= 0.";
        return false;
    }

    private static bool TryReadOptionalInt(
        GDictionary source,
        string path,
        string key,
        out int value,
        out string error,
        int defaultValue = 0
    )
    {
        value = defaultValue;
        error = "";
        if (!HasDictionaryValue(source, key))
            return true;
        if (TryReadIntValue(source, key, out value))
            return true;
        error = $"{path}.{key} must be int.";
        return false;
    }

    private static bool TryReadOptionalBool(
        GDictionary source,
        string path,
        string key,
        out bool value,
        out string error,
        bool defaultValue = false
    )
    {
        value = defaultValue;
        error = "";
        if (!HasDictionaryValue(source, key))
            return true;
        if (TryReadBoolValue(source, key, out value))
            return true;
        error = $"{path}.{key} must be bool.";
        return false;
    }

    private static bool TryReadOptionalStringName(
        GDictionary source,
        string path,
        string key,
        out StringName value,
        out string error,
        StringName defaultValue = default
    )
    {
        value = defaultValue;
        error = "";
        if (!HasDictionaryValue(source, key))
            return true;
        StringName normalized = ReadStringNameValue(source, key);
        if (normalized != null && !string.IsNullOrEmpty(normalized.ToString()))
        {
            value = normalized;
            return true;
        }
        error = $"{path}.{key} must be StringName.";
        return false;
    }

    private static bool TryReadOptionalStringNameList(
        GDictionary source,
        string path,
        string key,
        List<StringName> target,
        out string error
    )
    {
        error = "";
        target?.Clear();
        if (!HasDictionaryValue(source, key))
            return true;
        if (!TryReadArrayValue(source, key, out GArray values))
        {
            error = $"{path}.{key} must be Array.";
            return false;
        }
        foreach (var item in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(item);
            if (normalized == null || string.IsNullOrEmpty(normalized.ToString()))
            {
                error = $"{path}.{key} elements must be StringName/String.";
                return false;
            }
            target?.Add(normalized);
        }
        return true;
    }

    private static bool HasDictionaryValue(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
            return false;
        if (source.ContainsKey(key))
            return source[key].ToString() != "<null>";
        StringName stringNameKey = key;
        if (source.ContainsKey(stringNameKey))
            return source[stringNameKey].ToString() != "<null>";
        return false;
    }

    private static string ReadDictionaryKey<T>(T rawKey)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(rawKey);
        if (normalized != null && !string.IsNullOrEmpty(normalized.ToString()))
            return normalized.ToString();
        string text = rawKey?.ToString() ?? "";
        return text == "<null>" ? "" : text;
    }

    private static bool TryReadIntValue(GDictionary source, string key, out int value)
    {
        value = 0;
        try
        {
            if (source.ContainsKey(key))
            {
                value = source[key].AsInt32();
                return true;
            }
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey].AsInt32();
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TryReadBoolValue(GDictionary source, string key, out bool value)
    {
        value = false;
        try
        {
            if (source.ContainsKey(key))
            {
                value = source[key].AsBool();
                return true;
            }
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey].AsBool();
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static StringName ReadStringNameValue(GDictionary source, string key)
    {
        try
        {
            if (source.ContainsKey(key))
                return ProgressionDataUtils.to_string_name(source[key]);
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
                return ProgressionDataUtils.to_string_name(source[stringNameKey]);
        }
        catch
        {
        }
        return "";
    }

    private static bool TryReadArrayValue(GDictionary source, string key, out GArray value)
    {
        value = null;
        try
        {
            if (source.ContainsKey(key))
            {
                value = source[key].AsGodotArray();
                return value != null;
            }
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey].AsGodotArray();
                return value != null;
            }
        }
        catch
        {
        }
        return false;
    }

    private bool Fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new GDictionary { ["source"] = "BattleAiCandidateRequest" }
        );
    }

}

internal sealed class MoveToRangePathSearchBudget
{
    public int MaxCost;
    public int MaxNodes;
    public int MaxDestinations;
    public int PathTreeMinDestinationCount;
    public bool IncludeOrigin;
    public bool PreferProgress = true;

    public MoveToRangePathSearchBudget Clone()
    {
        return new MoveToRangePathSearchBudget
        {
            MaxCost = MaxCost,
            MaxNodes = MaxNodes,
            MaxDestinations = MaxDestinations,
            PathTreeMinDestinationCount = PathTreeMinDestinationCount,
            IncludeOrigin = IncludeOrigin,
            PreferProgress = PreferProgress,
        };
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["max_cost"] = MaxCost,
            ["max_nodes"] = MaxNodes,
            ["max_destinations"] = MaxDestinations,
            ["path_tree_min_destination_count"] = PathTreeMinDestinationCount,
            ["include_origin"] = IncludeOrigin,
            ["prefer_progress"] = PreferProgress,
        };
    }
}

internal sealed class MoveToRangeTacticalParams
{
    public StringName TargetSelector = "";
    public List<StringName> RangeSkillIds = new();
    public StringName PositionObjectiveKind = "distance_band_progress";
    public bool AoeSetupEnabled;
    public int AoeSetupMinTargetCount = 2;
    public int AoeSetupTargetCountWeight = 140;
    public int AoeSetupImprovementWeight = 220;
    public int AoeSetupFriendlyFirePenalty = 1000;

    public MoveToRangeTacticalParams Clone()
    {
        return new MoveToRangeTacticalParams
        {
            TargetSelector = TargetSelector,
            RangeSkillIds = new List<StringName>(RangeSkillIds ?? new List<StringName>()),
            PositionObjectiveKind = PositionObjectiveKind,
            AoeSetupEnabled = AoeSetupEnabled,
            AoeSetupMinTargetCount = AoeSetupMinTargetCount,
            AoeSetupTargetCountWeight = AoeSetupTargetCountWeight,
            AoeSetupImprovementWeight = AoeSetupImprovementWeight,
            AoeSetupFriendlyFirePenalty = AoeSetupFriendlyFirePenalty,
        };
    }

    public GDictionary ToDictionary()
    {
        var rangeSkillIds = new GArray();
        foreach (StringName skillId in RangeSkillIds ?? new List<StringName>())
        {
            rangeSkillIds.Add(skillId);
        }
        return new GDictionary
        {
            ["target_selector"] = TargetSelector,
            ["range_skill_ids"] = rangeSkillIds,
            ["position_objective_kind"] = PositionObjectiveKind,
            ["aoe_setup_enabled"] = AoeSetupEnabled,
            ["aoe_setup_min_target_count"] = AoeSetupMinTargetCount,
            ["aoe_setup_target_count_weight"] = AoeSetupTargetCountWeight,
            ["aoe_setup_improvement_weight"] = AoeSetupImprovementWeight,
            ["aoe_setup_friendly_fire_penalty"] = AoeSetupFriendlyFirePenalty,
        };
    }
}

internal sealed class MoveToRangeRuntimeMetadata
{
    public int ConfiguredDesiredMinDistance;
    public int ConfiguredDesiredMaxDistance;
    public int EffectiveAttackRange;

    public MoveToRangeRuntimeMetadata Clone()
    {
        return new MoveToRangeRuntimeMetadata
        {
            ConfiguredDesiredMinDistance = ConfiguredDesiredMinDistance,
            ConfiguredDesiredMaxDistance = ConfiguredDesiredMaxDistance,
            EffectiveAttackRange = EffectiveAttackRange,
        };
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["configured_desired_min_distance"] = ConfiguredDesiredMinDistance,
            ["configured_desired_max_distance"] = ConfiguredDesiredMaxDistance,
            ["effective_attack_range"] = EffectiveAttackRange,
        };
    }
}

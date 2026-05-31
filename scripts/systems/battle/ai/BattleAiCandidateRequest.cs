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

    private CandidatePayloadSection _pathSearchBudget =
        CandidatePayloadSection.Empty("path_search_budget");

    private CandidatePayloadSection _tacticalParams =
        CandidatePayloadSection.Empty("tactical_params");

    private CandidatePayloadSection _runtimeMetadata =
        CandidatePayloadSection.Empty("runtime_metadata");

    private MoveToRangePathSearchBudget _typedPathSearchBudget;

    private MoveToRangeTacticalParams _typedTacticalParams;

    private MoveToRangeRuntimeMetadata _typedRuntimeMetadata;

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
        get => _typedPathSearchBudget?.ToDictionary() ?? _pathSearchBudget.ToDictionary();
        set
        {
            _typedPathSearchBudget = null;
            _pathSearchBudget = CandidatePayloadSection.FromDictionary(
                value,
                "path_search_budget"
            );
        }
    }
    public GDictionary TacticalParams
    {
        get => _typedTacticalParams?.ToDictionary() ?? _tacticalParams.ToDictionary();
        set
        {
            _typedTacticalParams = null;
            _tacticalParams = CandidatePayloadSection.FromDictionary(value, "tactical_params");
        }
    }
    public GDictionary RuntimeMetadata
    {
        get => _typedRuntimeMetadata?.ToDictionary() ?? _runtimeMetadata.ToDictionary();
        set
        {
            _typedRuntimeMetadata = null;
            _runtimeMetadata = CandidatePayloadSection.FromDictionary(value, "runtime_metadata");
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
        _typedPathSearchBudget = pathSearchBudget?.Clone() ?? new MoveToRangePathSearchBudget();
        _typedTacticalParams = tacticalParams?.Clone() ?? new MoveToRangeTacticalParams();
        _typedRuntimeMetadata = runtimeMetadata?.Clone() ?? new MoveToRangeRuntimeMetadata();
        _pathSearchBudget = CandidatePayloadSection.Empty("path_search_budget");
        _tacticalParams = CandidatePayloadSection.Empty("tactical_params");
        _runtimeMetadata = CandidatePayloadSection.Empty("runtime_metadata");
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

        foreach (CandidatePayloadSection payload in new[]
                 {
                     _pathSearchBudget,
                     _tacticalParams,
                     _runtimeMetadata,
                 })
            if (!ValidateNoForbiddenObject(payload))
                return false;

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
        error = "";
        if (_typedPathSearchBudget != null)
        {
            pathBudget = _typedPathSearchBudget.Clone();
            return ValidatePathBudget(pathBudget, out error);
        }

        pathBudget = new MoveToRangePathSearchBudget();
        foreach (CandidatePayloadField field in _pathSearchBudget.Fields)
        {
            if (!PathBudgetKeys.Contains(field.Key))
            {
                error = $"Unsupported path_search_budget key {field.Key}.";
                return false;
            }
        }

        if (!TryGetInt(_pathSearchBudget, "max_cost", out pathBudget.MaxCost))
        {
            error = "path_search_budget.max_cost must be int.";
            return false;
        }

        if (pathBudget.MaxCost < 0)
        {
            error = "path_search_budget.max_cost must be int >= 0.";
            return false;
        }

        if (
            !TryReadOptionalNonNegativeInt(
                _pathSearchBudget,
                "max_nodes",
                out pathBudget.MaxNodes,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _pathSearchBudget,
                "max_destinations",
                out pathBudget.MaxDestinations,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _pathSearchBudget,
                "path_tree_min_destination_count",
                out pathBudget.PathTreeMinDestinationCount,
                out error
            )
        )
            return false;

        if (
            !TryReadOptionalBool(
                _pathSearchBudget,
                "include_origin",
                out pathBudget.IncludeOrigin,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalBool(
                _pathSearchBudget,
                "prefer_progress",
                out pathBudget.PreferProgress,
                out error,
                true
            )
        )
            return false;

        return ValidatePathBudget(pathBudget, out error);
    }

    private bool TryParseMoveToRangeTacticalParams(
        out MoveToRangeTacticalParams tacticalParams,
        out string error
    )
    {
        error = "";
        if (_typedTacticalParams != null)
        {
            tacticalParams = _typedTacticalParams.Clone();
            return ValidateTacticalParams(tacticalParams, out error);
        }

        tacticalParams = new MoveToRangeTacticalParams();
        foreach (CandidatePayloadField field in _tacticalParams.Fields)
        {
            if (!MoveToRangeTacticalKeys.Contains(field.Key))
            {
                error = $"Unsupported tactical_params key {field.Key}.";
                return false;
            }
        }

        if (
            !TryReadOptionalStringName(
                _tacticalParams,
                "target_selector",
                out tacticalParams.TargetSelector,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalStringName(
                _tacticalParams,
                "position_objective_kind",
                out tacticalParams.PositionObjectiveKind,
                out error,
                "distance_band_progress"
            )
        )
            return false;
        if (
            !TryReadOptionalStringNameList(
                _tacticalParams,
                "range_skill_ids",
                tacticalParams.RangeSkillIds,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalBool(
                _tacticalParams,
                "aoe_setup_enabled",
                out tacticalParams.AoeSetupEnabled,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _tacticalParams,
                "aoe_setup_min_target_count",
                out tacticalParams.AoeSetupMinTargetCount,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _tacticalParams,
                "aoe_setup_target_count_weight",
                out tacticalParams.AoeSetupTargetCountWeight,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _tacticalParams,
                "aoe_setup_improvement_weight",
                out tacticalParams.AoeSetupImprovementWeight,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalNonNegativeInt(
                _tacticalParams,
                "aoe_setup_friendly_fire_penalty",
                out tacticalParams.AoeSetupFriendlyFirePenalty,
                out error
            )
        )
            return false;

        return ValidateTacticalParams(tacticalParams, out error);
    }

    private bool TryParseRuntimeMetadata(
        out MoveToRangeRuntimeMetadata runtimeMetadata,
        out string error
    )
    {
        error = "";
        if (_typedRuntimeMetadata != null)
        {
            runtimeMetadata = _typedRuntimeMetadata.Clone();
            return true;
        }

        runtimeMetadata = new MoveToRangeRuntimeMetadata();
        foreach (CandidatePayloadField field in _runtimeMetadata.Fields)
        {
            if (!MoveToRangeRuntimeKeys.Contains(field.Key))
            {
                error = $"Unsupported runtime_metadata key {field.Key}.";
                return false;
            }
        }

        if (
            !TryReadOptionalInt(
                _runtimeMetadata,
                "configured_desired_min_distance",
                out runtimeMetadata.ConfiguredDesiredMinDistance,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalInt(
                _runtimeMetadata,
                "configured_desired_max_distance",
                out runtimeMetadata.ConfiguredDesiredMaxDistance,
                out error
            )
        )
            return false;
        if (
            !TryReadOptionalInt(
                _runtimeMetadata,
                "effective_attack_range",
                out runtimeMetadata.EffectiveAttackRange,
                out error
            )
        )
            return false;
        return true;
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

    private static bool TryReadOptionalNonNegativeInt(
        CandidatePayloadSection source,
        string key,
        out int value,
        out string error
    )
    {
        if (!TryReadOptionalInt(source, key, out value, out error))
            return false;
        if (value < 0)
        {
            error = $"{source.Path}.{key} must be int >= 0.";
            return false;
        }
        return true;
    }

    private static bool TryReadOptionalInt(
        CandidatePayloadSection source,
        string key,
        out int value,
        out string error,
        int defaultValue = 0
    )
    {
        value = defaultValue;
        error = "";
        if (source == null || !source.TryGetValue(key, out CandidatePayloadValue rawValue))
            return true;
        if (!rawValue.TryGetInt(out value))
        {
            error = $"{source.Path}.{key} must be int.";
            return false;
        }
        return true;
    }

    private static bool TryReadOptionalBool(
        CandidatePayloadSection source,
        string key,
        out bool value,
        out string error,
        bool defaultValue = false
    )
    {
        value = defaultValue;
        error = "";
        if (source == null || !source.TryGetValue(key, out CandidatePayloadValue rawValue))
            return true;
        if (!rawValue.TryReadBool(out value))
        {
            error = $"{source.Path}.{key} must be bool.";
            return false;
        }
        return true;
    }

    private static bool TryReadOptionalStringName(
        CandidatePayloadSection source,
        string key,
        out StringName value,
        out string error,
        StringName defaultValue = default
    )
    {
        value = defaultValue;
        error = "";
        if (source == null || !source.TryGetValue(key, out CandidatePayloadValue rawValue))
            return true;
        if (!rawValue.TryGetStrictStringName(out value))
        {
            error = $"{source.Path}.{key} must be StringName.";
            return false;
        }
        return true;
    }

    private static bool TryReadOptionalStringNameList(
        CandidatePayloadSection source,
        string key,
        List<StringName> target,
        out string error
    )
    {
        error = "";
        target?.Clear();
        if (source == null || !source.TryGetValue(key, out CandidatePayloadValue rawValue))
            return true;
        if (!rawValue.IsArray)
        {
            error = $"{source.Path}.{key} must be Array.";
            return false;
        }
        if (!rawValue.TryGetStringNameList(target))
        {
            error = $"{source.Path}.{key} elements must be StringName/String.";
            return false;
        }
        return true;
    }

    private static bool TryGetInt(CandidatePayloadSection source, string key, out int value)
    {
        value = 0;
        if (source == null || !source.TryGetValue(key, out CandidatePayloadValue rawValue))
            return false;
        return rawValue.TryGetInt(out value);
    }

    private bool Fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new GDictionary { ["source"] = "BattleAiCandidateRequest" }
        );
    }

    private static bool ValidateNoForbiddenObject(CandidatePayloadSection payload)
    {
        string error = payload?.FindForbiddenObject("BattleAiCandidateRequest") ?? "";
        if (string.IsNullOrEmpty(error))
            return true;
        return BattleAiPayloadGuard.FailLoud(
            error,
            new GDictionary { ["context"] = "BattleAiCandidateRequest" }
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

internal sealed class CandidatePayloadSection
{
    private readonly List<CandidatePayloadField> _fields = new();

    private CandidatePayloadSection(string path)
    {
        Path = string.IsNullOrEmpty(path) ? "payload" : path;
    }

    public string Path { get; }

    public IReadOnlyList<CandidatePayloadField> Fields => _fields;

    public static CandidatePayloadSection Empty(string path) => new(path);

    public static CandidatePayloadSection FromDictionary(GDictionary source, string path)
    {
        var section = new CandidatePayloadSection(path);
        if (source == null)
            return section;
        foreach (var rawKey in source.Keys)
        {
            string key = ReadKey(rawKey);
            if (string.IsNullOrEmpty(key))
                continue;
            section._fields.Add(
                new CandidatePayloadField(
                    key,
                    CandidatePayloadValue.FromVariant(source[rawKey])
                )
            );
        }
        return section;
    }

    public bool TryGetValue(string key, out CandidatePayloadValue value)
    {
        foreach (CandidatePayloadField field in _fields)
        {
            if (field.Key == key)
            {
                value = field.Value;
                return true;
            }
        }
        value = CandidatePayloadValue.Nil();
        return false;
    }

    public GDictionary ToDictionary()
    {
        var result = new GDictionary();
        foreach (CandidatePayloadField field in _fields)
            result[field.Key] = field.Value.ToVariant();
        return result;
    }

    public string FindForbiddenObject(string rootPath)
    {
        foreach (CandidatePayloadField field in _fields)
        {
            string error = field.Value.FindForbiddenObject($"{rootPath}.{Path}.{field.Key}");
            if (!string.IsNullOrEmpty(error))
                return error;
        }
        return "";
    }

    private static string ReadKey(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.String => rawKey.AsString(),
            Variant.Type.StringName => rawKey.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => rawKey.ToString(),
        };
    }
}

internal readonly struct CandidatePayloadField
{
    public CandidatePayloadField(string key, CandidatePayloadValue value)
    {
        Key = key ?? "";
        Value = value ?? CandidatePayloadValue.Nil();
    }

    public string Key { get; }
    public CandidatePayloadValue Value { get; }
}

internal enum CandidatePayloadValueKind
{
    Nil,
    Bool,
    Int,
    StringName,
    Text,
    StringNameArray,
    Array,
    Dictionary,
    Object,
    Fallback,
}

internal sealed class CandidatePayloadValue
{
    private readonly CandidatePayloadValueKind _kind;
    private readonly bool _boolValue;
    private readonly int _intValue;
    private readonly StringName _stringNameValue;
    private readonly string _textValue;
    private readonly List<StringName> _stringNameArrayValue;
    private readonly GArray _arrayValue;
    private readonly GDictionary _dictionaryValue;
    private readonly GodotObject _objectValue;
    private readonly Variant _fallbackValue;

    private CandidatePayloadValue(
        CandidatePayloadValueKind kind,
        bool boolValue = false,
        int intValue = 0,
        StringName stringNameValue = default,
        string textValue = "",
        List<StringName> stringNameArrayValue = null,
        GArray arrayValue = null,
        GDictionary dictionaryValue = null,
        GodotObject objectValue = null,
        Variant fallbackValue = default
    )
    {
        _kind = kind;
        _boolValue = boolValue;
        _intValue = intValue;
        _stringNameValue = stringNameValue;
        _textValue = textValue ?? "";
        _stringNameArrayValue = stringNameArrayValue ?? new List<StringName>();
        _arrayValue = arrayValue?.Duplicate(true) ?? new GArray();
        _dictionaryValue = dictionaryValue?.Duplicate(true) ?? new GDictionary();
        _objectValue = objectValue;
        _fallbackValue = fallbackValue;
    }

    public bool IsArray =>
        _kind == CandidatePayloadValueKind.StringNameArray
        || _kind == CandidatePayloadValueKind.Array;

    public static CandidatePayloadValue Nil() => new(CandidatePayloadValueKind.Nil);

    public bool TryGetInt(out int value)
    {
        value = _intValue;
        return _kind == CandidatePayloadValueKind.Int;
    }

    public bool TryReadBool(out bool value)
    {
        value = _boolValue;
        return _kind == CandidatePayloadValueKind.Bool;
    }

    public bool TryGetStrictStringName(out StringName value)
    {
        value = _stringNameValue;
        return _kind == CandidatePayloadValueKind.StringName;
    }

    public bool TryGetStringNameList(List<StringName> target)
    {
        target?.Clear();
        if (_kind != CandidatePayloadValueKind.StringNameArray)
            return false;
        foreach (StringName value in _stringNameArrayValue)
            target?.Add(value);
        return true;
    }

    public Variant ToVariant()
    {
        return _kind switch
        {
            CandidatePayloadValueKind.Nil => default,
            CandidatePayloadValueKind.Bool => Variant.From(_boolValue),
            CandidatePayloadValueKind.Int => Variant.From(_intValue),
            CandidatePayloadValueKind.StringName => Variant.From(_stringNameValue),
            CandidatePayloadValueKind.Text => Variant.From(_textValue),
            CandidatePayloadValueKind.StringNameArray => Variant.From(ToStringNameArray()),
            CandidatePayloadValueKind.Array => Variant.From(_arrayValue.Duplicate(true)),
            CandidatePayloadValueKind.Dictionary => Variant.From(_dictionaryValue.Duplicate(true)),
            CandidatePayloadValueKind.Object => Variant.From(_objectValue),
            CandidatePayloadValueKind.Fallback => _fallbackValue,
            _ => default,
        };
    }

    public string FindForbiddenObject(string path)
    {
        return _kind switch
        {
            CandidatePayloadValueKind.Object => BattleAiPayloadGuard.FindForbiddenObject(
                _objectValue,
                path
            ),
            CandidatePayloadValueKind.Array => BattleAiPayloadGuard.FindForbiddenObject(
                _arrayValue,
                path
            ),
            CandidatePayloadValueKind.Dictionary => BattleAiPayloadGuard.FindForbiddenObject(
                _dictionaryValue,
                path
            ),
            CandidatePayloadValueKind.Fallback => BattleAiPayloadGuard.FindForbiddenObject(
                _fallbackValue,
                path
            ),
            _ => "",
        };
    }

    public static CandidatePayloadValue FromVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => Nil(),
            Variant.Type.Bool => new CandidatePayloadValue(
                CandidatePayloadValueKind.Bool,
                boolValue: value.AsBool()
            ),
            Variant.Type.Int => new CandidatePayloadValue(
                CandidatePayloadValueKind.Int,
                intValue: value.AsInt32()
            ),
            Variant.Type.StringName => new CandidatePayloadValue(
                CandidatePayloadValueKind.StringName,
                stringNameValue: value.AsStringName()
            ),
            Variant.Type.String => new CandidatePayloadValue(
                CandidatePayloadValueKind.Text,
                textValue: value.AsString()
            ),
            Variant.Type.Array => FromArray(value.AsGodotArray()),
            Variant.Type.Dictionary => new CandidatePayloadValue(
                CandidatePayloadValueKind.Dictionary,
                dictionaryValue: value.AsGodotDictionary()
            ),
            Variant.Type.Object => new CandidatePayloadValue(
                CandidatePayloadValueKind.Object,
                objectValue: value.AsGodotObject()
            ),
            _ => new CandidatePayloadValue(
                CandidatePayloadValueKind.Fallback,
                fallbackValue: value
            ),
        };
    }

    private static CandidatePayloadValue FromArray(GArray source)
    {
        var stringNames = new List<StringName>();
        bool allStringLike = true;
        foreach (var rawValue in source ?? new GArray())
        {
            if (
                rawValue.VariantType != Variant.Type.String
                && rawValue.VariantType != Variant.Type.StringName
            )
            {
                allStringLike = false;
                break;
            }
            stringNames.Add(ProgressionDataUtils.to_string_name(rawValue));
        }
        return allStringLike
            ? new CandidatePayloadValue(
                CandidatePayloadValueKind.StringNameArray,
                stringNameArrayValue: stringNames
            )
            : new CandidatePayloadValue(CandidatePayloadValueKind.Array, arrayValue: source);
    }

    private GArray ToStringNameArray()
    {
        var result = new GArray();
        foreach (StringName value in _stringNameArrayValue)
            result.Add(value);
        return result;
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

using System.Collections.Generic;
using Godot;

public sealed class BattleAiCandidateRequest
{
    public static readonly StringName FamilyMoveToRange = "move_to_range";

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

    private bool _hasMoveToRangeSections;

    private MoveToRangePathSearchBudget _pathSearchBudget = new();

    private MoveToRangeTacticalParams _tacticalParams = new();

    private MoveToRangeRuntimeMetadata _runtimeMetadata = new();

    internal MoveToRangePathSearchBudget ParsedPathSearchBudget { get; private set; } = new();

    internal MoveToRangeTacticalParams ParsedTacticalParams { get; private set; } = new();

    internal MoveToRangeRuntimeMetadata ParsedRuntimeMetadata { get; private set; } = new();

    internal void SetMoveToRangeSections(
        MoveToRangePathSearchBudget pathSearchBudget,
        MoveToRangeTacticalParams tacticalParams,
        MoveToRangeRuntimeMetadata runtimeMetadata
    )
    {
        _pathSearchBudget = pathSearchBudget?.Clone() ?? new MoveToRangePathSearchBudget();
        _tacticalParams = tacticalParams?.Clone() ?? new MoveToRangeTacticalParams();
        _runtimeMetadata = runtimeMetadata?.Clone() ?? new MoveToRangeRuntimeMetadata();
        _hasMoveToRangeSections = true;
    }

    public bool TryValidateCommon(out string error)
    {
        error = "";
        if (IsEmpty(FamilyId))
        {
            error = "BattleAiCandidateRequest family_id must not be empty.";
            return false;
        }
        if (IsEmpty(ActionId))
        {
            error = "BattleAiCandidateRequest action_id must not be empty.";
            return false;
        }
        if (IsEmpty(ActorUnitId))
        {
            error = "BattleAiCandidateRequest actor_unit_id must not be empty.";
            return false;
        }
        if (IsEmpty(ActionIntent) || !BattleAiActionIntent.IsValid(ActionIntent))
        {
            error = $"BattleAiCandidateRequest action_intent is unsupported: {ActionIntent}.";
            return false;
        }
        return true;
    }

    public bool TryValidateMoveToRange(out string error)
    {
        if (!TryValidateCommon(out error))
        {
            return false;
        }
        if (FamilyId != FamilyMoveToRange)
        {
            error = $"Unsupported candidate family_id {FamilyId}.";
            return false;
        }
        if (IsEmpty(FocusTargetUnitId))
        {
            error = "BattleAiCandidateRequest focus_target_unit_id must not be empty.";
            return false;
        }
        if (
            DesiredMinDistance < 0
            || DesiredMaxDistance < 0
            || DesiredMinDistance > DesiredMaxDistance
        )
        {
            error = "BattleAiCandidateRequest desired distance range is invalid.";
            return false;
        }
        if (MaxCandidateCount <= 0)
        {
            error = "BattleAiCandidateRequest max_candidate_count must be > 0.";
            return false;
        }
        if (
            !TryGetMoveToRangeSections(
                out MoveToRangePathSearchBudget pathBudget,
                out MoveToRangeTacticalParams tacticalParams,
                out MoveToRangeRuntimeMetadata runtimeMetadata,
                out error
            )
        )
        {
            return false;
        }
        ParsedPathSearchBudget = pathBudget;
        ParsedTacticalParams = tacticalParams;
        ParsedRuntimeMetadata = runtimeMetadata;
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
        if (!_hasMoveToRangeSections)
        {
            error = "BattleAiCandidateRequest move_to_range sections must be set.";
            return false;
        }

        pathBudget = _pathSearchBudget?.Clone() ?? new MoveToRangePathSearchBudget();
        tacticalParams = _tacticalParams?.Clone() ?? new MoveToRangeTacticalParams();
        runtimeMetadata = _runtimeMetadata?.Clone() ?? new MoveToRangeRuntimeMetadata();

        if (!ValidatePathBudget(pathBudget, out error))
        {
            return false;
        }
        if (!ValidateTacticalParams(tacticalParams, out error))
        {
            return false;
        }
        if (pathBudget.MaxDestinations > 0 && MaxCandidateCount > pathBudget.MaxDestinations)
        {
            error = "max_candidate_count must not exceed path_search_budget.max_destinations.";
            return false;
        }
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

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
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
}

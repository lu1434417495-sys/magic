using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRuntimeActionEntry
{
    internal EnemyAiAction ResourceAction { get; private init; }

    internal BattleAiGeneratedMoveToRangeAction GeneratedMoveToRange { get; private init; }

    internal BattleAiUnitSkillActionSpec GeneratedUseUnitSkill { get; private init; }

    internal BattleAiRandomChainSkillActionSpec GeneratedRandomChainSkill { get; private init; }

    internal BattleAiMultiUnitSkillActionSpec GeneratedMultiUnitSkill { get; private init; }

    internal BattleAiMoveToMultiUnitSkillPositionActionSpec GeneratedMoveToMultiUnitSkillPosition { get; private init; }

    internal BattleAiChargeActionSpec GeneratedCharge { get; private init; }

    internal BattleAiChargePathAoeActionSpec GeneratedChargePathAoe { get; private init; }

    internal BattleAiGroundSkillActionSpec GeneratedGroundSkill { get; private init; }

    internal BattleAiRuntimeActionPlan.RuntimeActionMetadata Metadata { get; private init; }

    internal bool HasResourceAction => ResourceAction != null;

    internal bool IsGeneratedMoveToRange => GeneratedMoveToRange != null;

    internal bool IsGeneratedUseUnitSkill => GeneratedUseUnitSkill != null;

    internal bool IsGeneratedRandomChainSkill => GeneratedRandomChainSkill != null;

    internal bool IsGeneratedMultiUnitSkill => GeneratedMultiUnitSkill != null;

    internal bool IsGeneratedMoveToMultiUnitSkillPosition =>
        GeneratedMoveToMultiUnitSkillPosition != null;

    internal bool IsGeneratedCharge => GeneratedCharge != null;

    internal bool IsGeneratedChargePathAoe => GeneratedChargePathAoe != null;

    internal bool IsGeneratedGroundSkill => GeneratedGroundSkill != null;

    internal StringName ActionId =>
        HasResourceAction
            ? ProgressionDataUtils.to_string_name(ResourceAction.action_id)
            : GeneratedMoveToRange?.ActionId
                ?? GeneratedUseUnitSkill?.ActionId
                ?? GeneratedRandomChainSkill?.ActionId
                ?? GeneratedMultiUnitSkill?.ActionId
                ?? GeneratedMoveToMultiUnitSkillPosition?.ActionId
                ?? GeneratedCharge?.ActionId
                ?? GeneratedChargePathAoe?.ActionId
                ?? GeneratedGroundSkill?.ActionId
                ?? new StringName("");

    internal StringName ScoreBucketId =>
        HasResourceAction
            ? ProgressionDataUtils.to_string_name(ResourceAction.score_bucket_id)
            : GeneratedMoveToRange?.ScoreBucketId
                ?? GeneratedUseUnitSkill?.ScoreBucketId
                ?? GeneratedRandomChainSkill?.ScoreBucketId
                ?? GeneratedMultiUnitSkill?.ScoreBucketId
                ?? GeneratedMoveToMultiUnitSkillPosition?.ScoreBucketId
                ?? GeneratedCharge?.ScoreBucketId
                ?? GeneratedChargePathAoe?.ScoreBucketId
                ?? GeneratedGroundSkill?.ScoreBucketId
                ?? new StringName("");

    internal static BattleAiRuntimeActionEntry FromResource(
        EnemyAiAction action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            ResourceAction = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedMoveToRange(
        BattleAiGeneratedMoveToRangeAction action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedMoveToRange = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedUseUnitSkill(
        BattleAiUnitSkillActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedUseUnitSkill = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedRandomChainSkill(
        BattleAiRandomChainSkillActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedRandomChainSkill = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedMultiUnitSkill(
        BattleAiMultiUnitSkillActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedMultiUnitSkill = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedMoveToMultiUnitSkillPosition(
        BattleAiMoveToMultiUnitSkillPositionActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedMoveToMultiUnitSkillPosition = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedCharge(
        BattleAiChargeActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedCharge = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedChargePathAoe(
        BattleAiChargePathAoeActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedChargePathAoe = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }

    internal static BattleAiRuntimeActionEntry FromGeneratedGroundSkill(
        BattleAiGroundSkillActionSpec action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        return new BattleAiRuntimeActionEntry
        {
            GeneratedGroundSkill = action,
            Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata(),
        };
    }
}

internal sealed class BattleAiUnitSkillActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal StringName TargetSelector = "nearest_enemy";
    internal int MinimumEffectiveTargetCount = 1;
    internal int MaximumFriendlyFireTargetCount = 0;
    internal bool AllowFriendlyLethal = false;
    internal int DesiredMinDistance = -1;
    internal int DesiredMaxDistance = -1;
    internal EnemyAiDistanceReference DistanceReferenceKind = EnemyAiDistanceReference.None;
    internal StringName DistanceReference =>
        EnemyAiDistanceReferences.ToStringName(DistanceReferenceKind);

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiUnitSkillActionSpec FromAction(UseUnitSkillAction action)
    {
        var result = new BattleAiUnitSkillActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.MinimumEffectiveTargetCount = action.minimum_effective_target_count;
        result.MaximumFriendlyFireTargetCount = action.maximum_friendly_fire_target_count;
        result.AllowFriendlyLethal = action.allow_friendly_lethal;
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.DistanceReferenceKind = action.DistanceReferenceKind;
        result.SkillIds = new List<StringName>();
        foreach (StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>())
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

internal sealed class BattleAiGeneratedMoveToRangeAction
{
    private const int DefaultMaxCandidateCount = 12;
    private const int DefaultAoeSetupMaxCandidateCount = 32;

    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = BattleAiActionIntent.Positioning;
    internal StringName TargetSelector = EnemyAiTargetSelectorRules.NearestEnemy;
    internal int DesiredMinDistance = 1;
    internal int DesiredMaxDistance = 1;
    internal List<StringName> RangeSkillIds = new();
    internal bool EnableAoeSetupPositioning = true;
    internal int AoeSetupMinTargetCount = 2;
    internal int AoeSetupTargetCountWeight = 140;
    internal int AoeSetupImprovementWeight = 220;
    internal int AoeSetupFriendlyFirePenalty = 1000;

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(RangeSkillIds ?? new List<StringName>());

    internal BattleAiCandidateRequest BuildCandidateRequest(BattleAiQueryService query)
    {
        if (query == null)
        {
            return null;
        }

        StringName actorId = query.GetActorId();
        BattleAiUnitSnapshot actorSnapshot = query.GetActorSnapshot();
        BattleAiUnitSnapshot focusTarget = ResolveFocusTarget(query, actorSnapshot);
        if (actorId == "" || actorSnapshot == null || focusTarget == null)
        {
            return null;
        }

        bool aoeSetupEnabled = HasGroundAoeSetupSkill(query);
        int effectiveAttackRange = ResolveEffectiveAttackRange(query, aoeSetupEnabled);
        int resolvedMinDistance = Mathf.Max(DesiredMinDistance, 0);
        int resolvedMaxDistance = DesiredMaxDistance;
        if (effectiveAttackRange >= 0)
        {
            resolvedMaxDistance = effectiveAttackRange;
        }
        resolvedMaxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance);

        int maxCandidateCount = ResolveMaxCandidateCount(actorSnapshot);
        if (aoeSetupEnabled)
        {
            maxCandidateCount = Mathf.Max(maxCandidateCount, DefaultAoeSetupMaxCandidateCount);
        }
        MoveToRangePathSearchBudget pathBudget = BuildPathBudget(
            query,
            actorId,
            maxCandidateCount
        );
        if (pathBudget.MaxDestinations > 0)
        {
            maxCandidateCount = Mathf.Min(maxCandidateCount, pathBudget.MaxDestinations);
        }

        var request = new BattleAiCandidateRequest
        {
            FamilyId = BattleAiCandidateRequest.FamilyMoveToRange,
            ActionId = ActionId,
            ActionLabel = ActionId.ToString(),
            ActionIntent = ActionIntent,
            ScoreBucketId = ScoreBucketId,
            ActorUnitId = actorId,
            FocusTargetUnitId = focusTarget.unit_id,
            DesiredMinDistance = resolvedMinDistance,
            DesiredMaxDistance = resolvedMaxDistance,
            MaxCandidateCount = Mathf.Max(maxCandidateCount, 1),
        };
        request.SetMoveToRangeSections(
            pathBudget,
            new MoveToRangeTacticalParams
            {
                TargetSelector = TargetSelector,
                RangeSkillIds = new List<StringName>(RangeSkillIds ?? new List<StringName>()),
                PositionObjectiveKind = "distance_band_progress",
                AoeSetupEnabled = aoeSetupEnabled,
                AoeSetupMinTargetCount = Mathf.Max(AoeSetupMinTargetCount, 1),
                AoeSetupTargetCountWeight = Mathf.Max(AoeSetupTargetCountWeight, 0),
                AoeSetupImprovementWeight = Mathf.Max(AoeSetupImprovementWeight, 0),
                AoeSetupFriendlyFirePenalty = Mathf.Max(AoeSetupFriendlyFirePenalty, 0),
            },
            new MoveToRangeRuntimeMetadata
            {
                ConfiguredDesiredMinDistance = DesiredMinDistance,
                ConfiguredDesiredMaxDistance = DesiredMaxDistance,
                EffectiveAttackRange = effectiveAttackRange,
            }
        );
        return request;
    }

    private bool HasGroundAoeSetupSkill(BattleAiQueryService query)
    {
        if (!EnableAoeSetupPositioning || query == null)
        {
            return false;
        }
        foreach (StringName skillId in RangeSkillIds ?? new List<StringName>())
        {
            if (skillId == "")
            {
                continue;
            }
            if (
                query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord record)
                && IsSetupSkill(record)
            )
            {
                return true;
            }
        }
        return false;
    }

    private int ResolveEffectiveAttackRange(BattleAiQueryService query, bool useGroundAoeCastRange)
    {
        int bestRange = -1;
        foreach (StringName skillId in RangeSkillIds ?? new List<StringName>())
        {
            if (skillId == "")
            {
                continue;
            }
            if (!query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord record))
            {
                continue;
            }
            if (record.target_mode == BattleTargetMode.Unknown && record.actor_effective_range < 0)
            {
                continue;
            }
            int range = useGroundAoeCastRange && IsGroundAoe(record)
                ? record.actor_effective_cast_range
                : record.actor_effective_range;
            bestRange = Mathf.Max(bestRange, range);
        }
        return bestRange;
    }

    private static bool IsSetupSkill(BattleAiQueryService.SkillRecord record) =>
        IsGroundAoe(record) || IsRandomChain(record);

    private static bool IsGroundAoe(BattleAiQueryService.SkillRecord record)
    {
        return record != null
            && record.target_mode == BattleTargetMode.Ground
            && record.area_value > 0
            && record.area_pattern != BattleAreaPattern.Unknown
            && record.area_pattern != BattleAreaPattern.Single
            && record.area_pattern != BattleAreaPattern.Self;
    }

    private static bool IsRandomChain(BattleAiQueryService.SkillRecord record)
    {
        return record != null
            && record.target_mode == BattleTargetMode.Unit
            && record.target_selection_mode == BattleTargetSelectionMode.RandomChain;
    }

    private static BattleAiUnitSnapshot ResolveFocusTarget(
        BattleAiQueryService query,
        BattleAiUnitSnapshot actorSnapshot
    )
    {
        IReadOnlyList<BattleAiUnitSnapshot> targetValues =
            query.GetLivingUnitSnapshotsTyped("enemy");
        if (targetValues.Count == 0)
        {
            return null;
        }
        var targets = new List<BattleAiUnitSnapshot>();
        foreach (BattleAiUnitSnapshot target in targetValues)
        {
            if (target != null)
            {
                targets.Add(target);
            }
        }
        targets.Sort(
            (left, right) =>
            {
                int leftDistance = DistanceFromActor(query, actorSnapshot, left);
                int rightDistance = DistanceFromActor(query, actorSnapshot, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                int leftHp = left.current_hp;
                int rightHp = right.current_hp;
                if (leftHp != rightHp)
                {
                    return leftHp.CompareTo(rightHp);
                }
                return left.unit_id.ToString().CompareTo(right.unit_id.ToString());
            }
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static int DistanceFromActor(
        BattleAiQueryService query,
        BattleAiUnitSnapshot actorSnapshot,
        BattleAiUnitSnapshot targetSnapshot
    )
    {
        if (query == null || actorSnapshot == null || targetSnapshot == null)
        {
            return int.MaxValue;
        }
        return query.DistanceFromAnchorToTarget(
            actorSnapshot.coord,
            actorSnapshot.footprint_size,
            targetSnapshot.unit_id
        );
    }

    private static int ResolveMaxCandidateCount(BattleAiUnitSnapshot actorSnapshot)
    {
        int movePoints = Mathf.Max(actorSnapshot?.current_move_points ?? 0, 0);
        return Mathf.Max(DefaultMaxCandidateCount, movePoints * 4);
    }

    private static MoveToRangePathSearchBudget BuildPathBudget(
        BattleAiQueryService query,
        StringName actorId,
        int maxCandidateCount
    )
    {
        BattleMovementQueryService movementQuery = query.GetMovementQueryService();
        if (movementQuery != null)
        {
            BattleMovementQueryService.MovementQueryOptions options =
                BattleMovementQueryService.MovementQueryOptions.ForPathSearchBudget(
                    maxCandidateCount,
                    includeOrigin: false,
                    preferProgress: true
                );
            if (
                movementQuery.TryBuildPathSearchBudgetTyped(
                    actorId,
                    options,
                    out BattleMovementQueryService.PathSearchBudgetSnapshot budget
                )
            )
            {
                return new MoveToRangePathSearchBudget
                {
                    MaxCost = budget.MaxCost,
                    MaxNodes = budget.MaxNodes,
                    MaxDestinations = budget.MaxDestinations,
                    PathTreeMinDestinationCount = budget.PathTreeMinDestinationCount,
                    IncludeOrigin = budget.IncludeOrigin,
                    PreferProgress = budget.PreferProgress,
                };
            }
        }
        return new MoveToRangePathSearchBudget
        {
            MaxCost = 0,
            MaxNodes = 0,
            MaxDestinations = maxCandidateCount,
            PathTreeMinDestinationCount = 0,
            IncludeOrigin = false,
            PreferProgress = true,
        };
    }
}

internal sealed class BattleAiRandomChainSkillActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal StringName TargetSelector = "nearest_enemy";
    internal int DesiredMinDistance = -1;
    internal int DesiredMaxDistance = -1;
    internal EnemyAiDistanceReference DistanceReferenceKind =
        EnemyAiDistanceReference.CandidatePool;
    internal int MinimumCandidateCount = 1;
    internal StringName DistanceReference =>
        EnemyAiDistanceReferences.ToStringName(DistanceReferenceKind);

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiRandomChainSkillActionSpec FromAction(
        UseRandomChainSkillAction action
    )
    {
        var result = new BattleAiRandomChainSkillActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.DistanceReferenceKind = action.DistanceReferenceKind;
        result.MinimumCandidateCount = action.minimum_candidate_count;
        result.SkillIds = new List<StringName>();
        foreach (
            StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

internal sealed class BattleAiMultiUnitSkillActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal StringName TargetSelector = "nearest_enemy";
    internal int DesiredMinDistance = -1;
    internal int DesiredMaxDistance = -1;
    internal EnemyAiDistanceReference DistanceReferenceKind = EnemyAiDistanceReference.None;
    internal int CandidatePoolLimit = 6;
    internal int CandidateGroupLimit = 12;
    internal StringName DistanceReference =>
        EnemyAiDistanceReferences.ToStringName(DistanceReferenceKind);

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiMultiUnitSkillActionSpec FromAction(UseMultiUnitSkillAction action)
    {
        var result = new BattleAiMultiUnitSkillActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.DistanceReferenceKind = action.DistanceReferenceKind;
        result.CandidatePoolLimit = action.candidate_pool_limit;
        result.CandidateGroupLimit = action.candidate_group_limit;
        result.SkillIds = new List<StringName>();
        foreach (
            StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

internal sealed class BattleAiMoveToMultiUnitSkillPositionActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal StringName TargetSelector = "nearest_enemy";
    internal int DesiredMinDistance = -1;
    internal int DesiredMaxDistance = -1;
    internal EnemyAiDistanceReference DistanceReferenceKind = EnemyAiDistanceReference.None;
    internal int CandidatePoolLimit = 6;
    internal int CandidateGroupLimit = 12;
    internal int TargetCountWeight = 40;
    internal StringName DistanceReference =>
        EnemyAiDistanceReferences.ToStringName(DistanceReferenceKind);

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiMoveToMultiUnitSkillPositionActionSpec FromAction(
        MoveToMultiUnitSkillPositionAction action
    )
    {
        var result = new BattleAiMoveToMultiUnitSkillPositionActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.DistanceReferenceKind = action.DistanceReferenceKind;
        result.CandidatePoolLimit = action.candidate_pool_limit;
        result.CandidateGroupLimit = action.candidate_group_limit;
        result.TargetCountWeight = action.target_count_weight;
        result.SkillIds = new List<StringName>();
        foreach (
            StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

internal sealed class BattleAiChargeActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal StringName SkillId = "charge";
    internal StringName TargetSelector = "nearest_enemy";
    internal int MinimumChargeMoveDistance = 3;

    internal IReadOnlyList<StringName> GetDeclaredSkillIds()
    {
        return SkillId != "" ? new List<StringName> { SkillId } : new List<StringName>();
    }

    internal static BattleAiChargeActionSpec FromAction(UseChargeAction action)
    {
        var result = new BattleAiChargeActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.SkillId = ProgressionDataUtils.to_string_name(action.skill_id);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.MinimumChargeMoveDistance = action.minimum_charge_move_distance;
        return result;
    }
}

internal sealed class BattleAiChargePathAoeActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal StringName TargetSelector = "nearest_enemy";
    internal int MinimumHitCount = 1;
    internal int DesiredMinDistance = 1;
    internal int DesiredMaxDistance = 1;

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiChargePathAoeActionSpec FromAction(UseChargePathAoeAction action)
    {
        var result = new BattleAiChargePathAoeActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.TargetSelector = ProgressionDataUtils.to_string_name(action.target_selector);
        result.MinimumHitCount = action.minimum_hit_count;
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.SkillIds = new List<StringName>();
        foreach (
            StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

internal sealed class BattleAiGroundSkillActionSpec
{
    internal StringName ActionId = "";
    internal StringName ScoreBucketId = "";
    internal StringName ActionIntent = "";
    internal List<StringName> SkillIds = new();
    internal int MinimumHitCount = 1;
    internal bool AllowEmptyGroundControl = false;
    internal bool AllowGroundControlSupplementPartialHits = false;
    internal int MinimumGroundControlScore = 1;
    internal int MinimumAllyThreatHitCount = 0;
    internal int MaximumFriendlyFireTargetCount = 0;
    internal bool AllowFriendlyLethal = false;
    internal int ThreatMinimumSafeDistance = 0;
    internal int ThreatSafeDistanceMargin = 0;
    internal int DesiredMinDistance = -1;
    internal int DesiredMaxDistance = -1;
    internal EnemyAiDistanceReference DistanceReferenceKind = EnemyAiDistanceReference.None;
    internal StringName DistanceReference =>
        EnemyAiDistanceReferences.ToStringName(DistanceReferenceKind);

    internal IReadOnlyList<StringName> GetDeclaredSkillIds() =>
        new List<StringName>(SkillIds ?? new List<StringName>());

    internal static BattleAiGroundSkillActionSpec FromAction(UseGroundSkillAction action)
    {
        var result = new BattleAiGroundSkillActionSpec();
        if (action == null)
        {
            return result;
        }
        result.ActionId = ProgressionDataUtils.to_string_name(action.action_id);
        result.ScoreBucketId = ProgressionDataUtils.to_string_name(action.score_bucket_id);
        result.ActionIntent = ProgressionDataUtils.to_string_name(action.action_intent);
        result.MinimumHitCount = action.minimum_hit_count;
        result.AllowEmptyGroundControl = action.allow_empty_ground_control;
        result.AllowGroundControlSupplementPartialHits =
            action.allow_ground_control_supplement_partial_hits;
        result.MinimumGroundControlScore = action.minimum_ground_control_score;
        result.MinimumAllyThreatHitCount = action.minimum_ally_threat_hit_count;
        result.MaximumFriendlyFireTargetCount = action.maximum_friendly_fire_target_count;
        result.AllowFriendlyLethal = action.allow_friendly_lethal;
        result.ThreatMinimumSafeDistance = action.threat_minimum_safe_distance;
        result.ThreatSafeDistanceMargin = action.threat_safe_distance_margin;
        result.DesiredMinDistance = action.desired_min_distance;
        result.DesiredMaxDistance = action.desired_max_distance;
        result.DistanceReferenceKind = action.DistanceReferenceKind;
        result.SkillIds = new List<StringName>();
        foreach (
            StringName skillId in action.skill_ids ?? new Godot.Collections.Array<StringName>()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId != "")
            {
                result.SkillIds.Add(normalizedSkillId);
            }
        }
        return result;
    }
}

using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MoveToRangeAction : EnemyAiAction
{
    public static readonly StringName ScreeningNone = "none";
    public static readonly StringName ScreeningRangedAlly = "ranged_ally";
    public static readonly StringName AiEvaluationLegacyDecide = "legacy_decide";
    public static readonly StringName AiEvaluationCandidateRequest = "candidate_request";
    private const int HpBasisPointsDenominator = 10000;
    private const int DefaultMaxCandidateCount = 12;
    private const int DefaultAoeSetupMaxCandidateCount = 32;
    private const int ScreeningPathUnreachableCost = int.MaxValue;
    private const int PathTreeFilterMinDestinations = 4;

    private sealed class ScreeningContext
    {
        public bool Enabled;
        public string Reason = "";
        public List<BattleUnitState> ProtectedAllies = new();
        public List<ScreeningThreatEntry> ThreatEntries = new();
        public Dictionary<Vector2I, ScreeningMetrics> AnchorMetricsCache = new();

        public static ScreeningContext Disabled(string reason = "")
        {
            return new ScreeningContext { Enabled = false, Reason = reason };
        }

        public GDictionary ToDictionary()
        {
            var threatEntries = new GArray();
            foreach (ScreeningThreatEntry entry in ThreatEntries)
            {
                if (entry != null)
                {
                    threatEntries.Add(entry.ToDictionary());
                }
            }

            var result = new GDictionary
            {
                ["enabled"] = Enabled,
                ["threat_entries"] = threatEntries,
                ["anchor_metrics_cache"] = new GDictionary(),
            };
            if (!string.IsNullOrEmpty(Reason))
            {
                result["reason"] = Reason;
            }
            return result;
        }
    }

    private sealed class ScreeningThreatEntry
    {
        public BattleUnitState ThreatUnit;
        public BattleUnitState ProtectedUnit;
        public int ContactRange;
        public int ThreatDistance;
        public int ThreatReach;
        public int BasePathCost;

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["threat_unit"] = ThreatUnit,
                ["protected_unit"] = ProtectedUnit,
                ["contact_range"] = ContactRange,
                ["threat_distance"] = ThreatDistance,
                ["threat_reach"] = ThreatReach,
                ["base_path_cost"] = BasePathCost,
            };
        }
    }

    private sealed class MoveDistanceContract
    {
        public int ConfiguredMinDistance;
        public int ConfiguredMaxDistance;
        public int DesiredMinDistance;
        public int DesiredMaxDistance;
        public int EffectiveAttackRange = -1;

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["configured_desired_min_distance"] = ConfiguredMinDistance,
                ["configured_desired_max_distance"] = ConfiguredMaxDistance,
                ["desired_min_distance"] = DesiredMinDistance,
                ["desired_max_distance"] = DesiredMaxDistance,
                ["effective_attack_range"] = EffectiveAttackRange,
            };
        }

        public static MoveDistanceContract FromDictionary(
            GDictionary source,
            int fallbackMin,
            int fallbackMax
        )
        {
            source ??= new GDictionary();
            int configuredMin = GdInterop.GetInt(
                source,
                "configured_desired_min_distance",
                fallbackMin
            );
            int configuredMax = GdInterop.GetInt(
                source,
                "configured_desired_max_distance",
                fallbackMax
            );
            int desiredMin = GdInterop.GetInt(source, "desired_min_distance", configuredMin);
            int desiredMax = GdInterop.GetInt(source, "desired_max_distance", configuredMax);
            return new MoveDistanceContract
            {
                ConfiguredMinDistance = configuredMin,
                ConfiguredMaxDistance = configuredMax,
                DesiredMinDistance = desiredMin,
                DesiredMaxDistance = desiredMax,
                EffectiveAttackRange = GdInterop.GetInt(source, "effective_attack_range", -1),
            };
        }
    }

    private sealed class MovePathResult
    {
        public bool Allowed;
        public int Cost;
        public Godot.Collections.Array<Vector2I> Path = new();

        public static MovePathResult FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            return new MovePathResult
            {
                Allowed = GdInterop.GetBool(source, "allowed", false),
                Cost = GdInterop.GetInt(source, "cost", 0),
                Path = ReadVector2IArray(source, "path"),
            };
        }
    }

    private sealed class MovePathTreeCosts
    {
        private readonly Dictionary<Vector2I, int> _costs = new();

        public bool IsEmpty => _costs.Count == 0;

        public bool TryGetCost(Vector2I coord, out int cost) =>
            _costs.TryGetValue(coord, out cost);

        public static MovePathTreeCosts FromPathTreeResult(BattleMovePathTreeResult source)
        {
            var result = new MovePathTreeCosts();
            if (source == null)
            {
                return result;
            }
            foreach ((Vector2I coord, int cost) in source.Costs)
            {
                result._costs[coord] = cost;
            }
            return result;
        }
    }

    private sealed class MovePathSearchBudget
    {
        public int MaxCost;
        public int MaxNodes;
        public int MaxDestinations;
        public int PathTreeMinDestinationCount;
        public bool IncludeOrigin;
        public bool PreferProgress;

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

        public static MovePathSearchBudget FromDictionary(
            GDictionary source,
            int fallbackMaxDestinations
        )
        {
            source ??= new GDictionary();
            return new MovePathSearchBudget
            {
                MaxCost = GdInterop.GetInt(source, "max_cost", 0),
                MaxNodes = GdInterop.GetInt(source, "max_nodes", 0),
                MaxDestinations = GdInterop.GetInt(
                    source,
                    "max_destinations",
                    fallbackMaxDestinations
                ),
                PathTreeMinDestinationCount = GdInterop.GetInt(
                    source,
                    "path_tree_min_destination_count",
                    0
                ),
                IncludeOrigin = GdInterop.GetBool(source, "include_origin", false),
                PreferProgress = GdInterop.GetBool(source, "prefer_progress", true),
            };
        }
    }

    private sealed class MoveSkillRecord
    {
        public StringName TargetMode = "";
        public StringName TargetSelectionMode = "";
        public StringName AreaPattern = "";
        public int AreaValue;
        public int ActorEffectiveRange = -1;
        public int ActorEffectiveCastRange = -1;

        public bool IsGroundAoe =>
            TargetMode == "ground"
            && AreaValue > 0
            && AreaPattern != ""
            && AreaPattern != "single"
            && AreaPattern != "self";

        public bool IsRandomChain => TargetMode == "unit" && TargetSelectionMode == "random_chain";

        public bool IsSetupSkill => IsGroundAoe || IsRandomChain;

        public static MoveSkillRecord FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            return new MoveSkillRecord
            {
                TargetMode = GdInterop.GetStringName(source, "target_mode"),
                TargetSelectionMode = GdInterop.GetStringName(source, "target_selection_mode"),
                AreaPattern = GdInterop.GetStringName(source, "area_pattern"),
                AreaValue = GdInterop.GetInt(source, "area_value", 0),
                ActorEffectiveRange = GdInterop.GetInt(source, "actor_effective_range", -1),
                ActorEffectiveCastRange = GdInterop.GetInt(
                    source,
                    "actor_effective_cast_range",
                    -1
                ),
            };
        }

        public static MoveSkillRecord FromSkillRecord(BattleAiQueryService.SkillRecord source)
        {
            if (source == null)
            {
                return new MoveSkillRecord();
            }
            return new MoveSkillRecord
            {
                TargetMode = source.target_mode,
                TargetSelectionMode = source.target_selection_mode,
                AreaPattern = source.area_pattern,
                AreaValue = source.area_value,
                ActorEffectiveRange = source.actor_effective_range,
                ActorEffectiveCastRange = source.actor_effective_cast_range,
            };
        }
    }

    private sealed class ScreeningMetrics
    {
        public int Bonus;
        public string ThreatUnitId = "";
        public string ProtectedUnitId = "";
        public int AnchorToThreat;
        public int AnchorToProtected;
        public int ThreatDistance;
        public int BasePathCost = ScreeningPathUnreachableCost;
        public int BlockedPathCost = ScreeningPathUnreachableCost;
        public int PathCostDelta;
        public bool HardBlock;
        public bool OnShortestPath;
        public bool KeepsContact;
        public bool CanCounterattack;
        public int Penalty;
        public int CurrentBonus;
        public int CandidateBonus;
        public int LostBonus;
        public int UncappedBonus;
        public bool DistanceBandCapped;

        public ScreeningMetrics Clone()
        {
            return (ScreeningMetrics)MemberwiseClone();
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary { ["bonus"] = Bonus };
            bool hasMatchedThreat =
                !string.IsNullOrEmpty(ThreatUnitId) || !string.IsNullOrEmpty(ProtectedUnitId);
            if (hasMatchedThreat)
            {
                result["threat_unit_id"] = ThreatUnitId;
                result["protected_unit_id"] = ProtectedUnitId;
                result["anchor_to_threat"] = AnchorToThreat;
                result["anchor_to_protected"] = AnchorToProtected;
                result["threat_distance"] = ThreatDistance;
                result["base_path_cost"] = BasePathCost;
                result["blocked_path_cost"] = BlockedPathCost;
                result["path_cost_delta"] = PathCostDelta;
                result["hard_block"] = HardBlock;
                result["on_shortest_path"] = OnShortestPath;
                result["keeps_contact"] = KeepsContact;
                result["can_counterattack"] = CanCounterattack;
            }
            if (Penalty != 0)
            {
                result["penalty"] = Penalty;
            }
            if (CurrentBonus != 0)
            {
                result["current_bonus"] = CurrentBonus;
            }
            if (CandidateBonus != 0)
            {
                result["candidate_bonus"] = CandidateBonus;
            }
            if (LostBonus != 0)
            {
                result["lost_bonus"] = LostBonus;
            }
            if (UncappedBonus != 0)
            {
                result["uncapped_bonus"] = UncappedBonus;
            }
            if (DistanceBandCapped)
            {
                result["distance_band_capped"] = true;
            }
            return result;
        }
    }

    public StringName SCREENING_NONE => ScreeningNone;
    public StringName SCREENING_RANGED_ALLY => ScreeningRangedAlly;
    public StringName AI_EVALUATION_LEGACY_DECIDE => AiEvaluationLegacyDecide;
    public StringName AI_EVALUATION_CANDIDATE_REQUEST => AiEvaluationCandidateRequest;

    [Export]
    public StringName ai_evaluation_mode { get; set; } = AiEvaluationLegacyDecide;

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = 1;

    [Export]
    public int desired_max_distance { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<StringName> range_skill_ids { get; set; } = new();

    [Export]
    public StringName screening_mode { get; set; } = ScreeningNone;

    [Export]
    public bool enable_aoe_setup_positioning { get; set; } = true;

    [Export]
    public int aoe_setup_min_target_count { get; set; } = 2;

    [Export]
    public int aoe_setup_target_count_weight { get; set; } = 140;

    [Export]
    public int aoe_setup_improvement_weight { get; set; } = 220;

    [Export]
    public int aoe_setup_friendly_fire_penalty { get; set; } = 1000;

    [Export]
    public int screening_min_hp_basis_points { get; set; } = 4000;

    [Export]
    public int screening_ally_min_attack_range { get; set; } = 4;

    [Export]
    public int screening_enemy_max_contact_range { get; set; } = 2;

    [Export]
    public int screening_threat_distance_buffer { get; set; } = 2;

    [Export]
    public int screening_path_bonus { get; set; } = 45;

    public override BattleAiDecision decide(BattleAiContext context)
    {
        if (uses_candidate_request())
        {
            return null;
        }
        AiTraceRecorder.enter("decide:move_to_range");
        BattleAiDecision result = DecideImpl(context);
        AiTraceRecorder.exit("decide:move_to_range");
        return result;
    }

    public override bool uses_candidate_request()
    {
        return ai_evaluation_mode == AiEvaluationCandidateRequest;
    }

    public GDictionary _build_screening_context(BattleAiContext context)
    {
        return BuildScreeningContext(context).ToDictionary();
    }

    public GDictionary _build_screening_metrics(
        BattleAiContext context,
        Vector2I anchor_coord,
        GDictionary screening_context
    )
    {
        ScreeningContext typedScreeningContext = DecodeScreeningContext(screening_context);
        return BuildScreeningMetrics(context, anchor_coord, typedScreeningContext, null).ToDictionary();
    }

    private ScreeningContext DecodeScreeningContext(GDictionary source)
    {
        if (source == null)
        {
            return ScreeningContext.Disabled();
        }

        var result = new ScreeningContext
        {
            Enabled = GdInterop.GetBool(source, "enabled", false),
            Reason = GdInterop.GetString(source, "reason", ""),
        };
        foreach (ScreeningThreatEntry threatEntry in ReadScreeningThreatEntries(source))
        {
            if (threatEntry.ThreatUnit == null || threatEntry.ProtectedUnit == null)
            {
                continue;
            }
            result.ThreatEntries.Add(threatEntry);
        }
        return result;
    }

    private static ScreeningThreatEntry DecodeScreeningThreatEntry(GDictionary source)
    {
        return new ScreeningThreatEntry
        {
            ThreatUnit = GdInterop.GetObject(source, "threat_unit") as BattleUnitState,
            ProtectedUnit = GdInterop.GetObject(source, "protected_unit") as BattleUnitState,
            ContactRange = GdInterop.GetInt(source, "contact_range", 1),
            ThreatDistance = GdInterop.GetInt(source, "threat_distance", 999999),
            ThreatReach = GdInterop.GetInt(source, "threat_reach", 0),
            BasePathCost = GdInterop.GetInt(
                source,
                "base_path_cost",
                ScreeningPathUnreachableCost
            ),
        };
    }

    public override BattleAiCandidateRequest build_candidate_request(BattleAiQueryService query)
    {
        if (query == null)
        {
            return null;
        }

        StringName actorId = query.get_actor_id();
        BattleAiUnitSnapshot actorSnapshot = query.get_actor_snapshot();
        BattleAiUnitSnapshot focusTarget = ResolveFocusTarget(query, actorSnapshot);
        if (actorId == "" || actorSnapshot == null || focusTarget == null)
        {
            return null;
        }

        bool aoeSetupEnabled = HasGroundAoeSetupSkill(query);
        int effectiveAttackRange = ResolveEffectiveAttackRange(query, aoeSetupEnabled);
        int resolvedMinDistance = Mathf.Max(desired_min_distance, 0);
        int resolvedMaxDistance = desired_max_distance;
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
        MovePathSearchBudget pathBudget = BuildPathBudget(query, actorId, maxCandidateCount);
        if (pathBudget.MaxDestinations > 0)
        {
            maxCandidateCount = Mathf.Min(maxCandidateCount, pathBudget.MaxDestinations);
        }

        return new BattleAiCandidateRequest
        {
            FamilyId = "move_to_range",
            ActionId = action_id,
            ActionLabel = action_id.ToString(),
            ActionIntent = action_intent,
            ScoreBucketId = score_bucket_id,
            ActorUnitId = actorId,
            FocusTargetUnitId = focusTarget.unit_id,
            DesiredMinDistance = resolvedMinDistance,
            DesiredMaxDistance = resolvedMaxDistance,
            MaxCandidateCount = Mathf.Max(maxCandidateCount, 1),
            PathSearchBudget = pathBudget.ToDictionary(),
            TacticalParams = new GDictionary
            {
                ["target_selector"] = target_selector,
                ["range_skill_ids"] = range_skill_ids.Duplicate(),
                ["position_objective_kind"] = new StringName("distance_band_progress"),
                ["aoe_setup_enabled"] = aoeSetupEnabled,
                ["aoe_setup_min_target_count"] = Mathf.Max(aoe_setup_min_target_count, 1),
                ["aoe_setup_target_count_weight"] = Mathf.Max(
                    aoe_setup_target_count_weight,
                    0
                ),
                ["aoe_setup_improvement_weight"] = Mathf.Max(aoe_setup_improvement_weight, 0),
                ["aoe_setup_friendly_fire_penalty"] = Mathf.Max(
                    aoe_setup_friendly_fire_penalty,
                    0
                ),
            },
            RuntimeMetadata = new GDictionary
            {
                ["configured_desired_min_distance"] = desired_min_distance,
                ["configured_desired_max_distance"] = desired_max_distance,
                ["effective_attack_range"] = effectiveAttackRange,
            },
        };
    }

    public override Godot.Collections.Array<string> validate_schema()
    {
        var errors = _collect_base_validation_errors();
        if (target_selector == "")
        {
            errors.Add($"MoveToRangeAction {action_id} is missing target_selector.");
        }
        _append_enemy_focus_target_selector_errors(errors, "MoveToRangeAction", target_selector);
        if (screening_mode != ScreeningNone && screening_mode != ScreeningRangedAlly)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_mode must be none or ranged_ally."
            );
        }
        if (desired_min_distance < 0)
        {
            errors.Add($"MoveToRangeAction {action_id} desired_min_distance must be >= 0.");
        }
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            screening_min_hp_basis_points < 0
            || screening_min_hp_basis_points > HpBasisPointsDenominator
        )
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_min_hp_basis_points must be between 0 and 10000."
            );
        }
        if (screening_ally_min_attack_range < 1)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_ally_min_attack_range must be >= 1."
            );
        }
        if (screening_enemy_max_contact_range < 1)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_enemy_max_contact_range must be >= 1."
            );
        }
        if (screening_threat_distance_buffer < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_threat_distance_buffer must be >= 0."
            );
        }
        if (screening_path_bonus < 0)
        {
            errors.Add($"MoveToRangeAction {action_id} screening_path_bonus must be >= 0.");
        }
        if (aoe_setup_min_target_count < 1)
        {
            errors.Add($"MoveToRangeAction {action_id} aoe_setup_min_target_count must be >= 1.");
        }
        if (aoe_setup_target_count_weight < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_target_count_weight must be >= 0."
            );
        }
        if (aoe_setup_improvement_weight < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_improvement_weight must be >= 0."
            );
        }
        if (aoe_setup_friendly_fire_penalty < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_friendly_fire_penalty must be >= 0."
            );
        }
        if (
            ai_evaluation_mode != AiEvaluationLegacyDecide
            && ai_evaluation_mode != AiEvaluationCandidateRequest
        )
        {
            errors.Add(
                $"MoveToRangeAction {action_id} ai_evaluation_mode must be legacy_decide or candidate_request."
            );
        }
        if (uses_candidate_request() && screening_mode != ScreeningNone)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} candidate_request mode does not support screening_mode {screening_mode}."
            );
        }
        return errors;
    }

    private BattleAiDecision DecideImpl(BattleAiContext context)
    {
        MoveDistanceContract distanceContract = ResolveMoveDistanceContract(context);
        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        var actionTrace = _begin_action_trace(
            context,
            new GDictionary
            {
                ["action_kind"] = "move_to_range",
                ["target_selector"] = target_selector.ToString(),
                ["desired_min_distance"] = resolvedMinDistance,
                ["desired_max_distance"] = resolvedMaxDistance,
                ["configured_desired_min_distance"] = desired_min_distance,
                ["configured_desired_max_distance"] = desired_max_distance,
                ["effective_attack_range"] = distanceContract.EffectiveAttackRange,
                ["range_skill_ids"] = range_skill_ids.Duplicate(),
                ["screening_mode"] = screening_mode.ToString(),
            }
        );

        List<BattleUnitState> targets = _sort_target_units_typed(context, "enemy", target_selector);
        if (targets.Count == 0)
        {
            _trace_add_block_reason(actionTrace, "no_valid_targets");
            _finalize_action_trace(context, actionTrace);
            return null;
        }

        var focusTarget = targets[0];
        var actor = GetContextUnit(context);
        if (actor == null || focusTarget == null)
        {
            _trace_add_block_reason(actionTrace, "missing_context");
            _finalize_action_trace(context, actionTrace);
            return null;
        }

        ScreeningContext screeningContext = BuildScreeningContext(context);
        BattleAiScoreInput currentScoreInput = _build_action_score_input(
            context,
            "move",
            action_id.ToString(),
            null,
            null,
            new GDictionary
            {
                ["position_target_unit_id"] = focusTarget.unit_id,
                ["position_anchor_coord"] = actor.coord,
                ["desired_min_distance"] = resolvedMinDistance,
                ["desired_max_distance"] = resolvedMaxDistance,
                ["position_objective_kind"] = new StringName("distance_band_progress"),
                ["move_cost"] = 0,
            }
        );
        ApplyScreeningScore(context, currentScoreInput, actor.coord, screeningContext);

        if (!screeningContext.Enabled)
        {
            BattleAiDecision pathProgressDecision = BuildPathProgressDecision(
                context,
                focusTarget,
                actionTrace,
                distanceContract
            );
            if (pathProgressDecision != null)
            {
                _finalize_action_trace(context, actionTrace, pathProgressDecision);
                return pathProgressDecision;
            }
        }

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = currentScoreInput;
        foreach (Vector2I neighbor in CollectReachableMoveCandidates(context))
        {
            _trace_count_increment(actionTrace, "evaluation_count", 1);
            BattleCommand command = _build_move_command(context, neighbor);
            BattlePreview preview = _build_fast_typed_move_preview(context, neighbor);
            if (preview?.allowed != true)
            {
                _trace_count_increment(actionTrace, "preview_reject_count", 1);
                continue;
            }

            BattleAiScoreInput scoreInput = _build_action_score_input(
                context,
                "move",
                action_id.ToString(),
                command,
                preview,
                new GDictionary
                {
                    ["position_target_unit_id"] = focusTarget.unit_id,
                    ["position_anchor_coord"] = neighbor,
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["position_objective_kind"] = new StringName("distance_band_progress"),
                }
            );
            ScreeningMetrics screeningMetrics = ApplyScreeningScore(
                context,
                scoreInput,
                neighbor,
                screeningContext
            );
            _trace_offer_candidate(
                actionTrace,
                _build_candidate_summary(
                    $"move_to_{neighbor.X}_{neighbor.Y}",
                    command,
                    scoreInput,
                    new GDictionary
                    {
                        ["predicted_distance"] = scoreInput is BattleAiScoreInput typed
                            ? typed.distance_to_primary_coord
                            : -1,
                        ["screening_bonus"] = screeningMetrics.Bonus,
                        ["screening_penalty"] = screeningMetrics.Penalty,
                        ["screening_threat_unit_id"] = screeningMetrics.ThreatUnitId,
                        ["screening_protected_unit_id"] = screeningMetrics.ProtectedUnitId,
                        ["screening_path_cost_delta"] = screeningMetrics.PathCostDelta,
                        ["screening_base_path_cost"] = screeningMetrics.BasePathCost,
                        ["screening_blocked_path_cost"] = screeningMetrics.BlockedPathCost,
                        ["screening_current_bonus"] = screeningMetrics.CurrentBonus,
                        ["screening_candidate_bonus"] = screeningMetrics.CandidateBonus,
                        ["screening_uncapped_bonus"] = screeningMetrics.UncappedBonus,
                        ["screening_on_shortest_path"] = screeningMetrics.OnShortestPath,
                        ["screening_keeps_contact"] = screeningMetrics.KeepsContact,
                        ["screening_can_counterattack"] = screeningMetrics.CanCounterattack,
                        ["screening_hard_block"] = screeningMetrics.HardBlock,
                        ["screening_distance_band_capped"] = screeningMetrics.DistanceBandCapped,
                    }
                )
            );

            if (!IsBetterMoveToRangeScoreInput(scoreInput, bestScoreInput))
            {
                continue;
            }

            bestScoreInput = scoreInput;
            int distance = scoreInput is BattleAiScoreInput moveScore
                ? moveScore.distance_to_primary_coord
                : -1;
            bestDecision = _create_scored_decision(
                command,
                scoreInput,
                $"{actor.display_name} 准备调整到距离 {focusTarget.display_name} {distance} 格（评分 {_score_total(scoreInput)}）。"
            );
        }

        if (bestDecision == null)
        {
            bestDecision = BuildPathProgressDecision(
                context,
                focusTarget,
                actionTrace,
                distanceContract
            );
        }
        _finalize_action_trace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    private Godot.Collections.Array<Vector2I> CollectReachableMoveCandidates(BattleAiContext context)
    {
        AiTraceRecorder.enter("_collect_reachable_move_candidates");
        Godot.Collections.Array<Vector2I> result = CollectReachableMoveCandidatesImpl(context);
        AiTraceRecorder.exit("_collect_reachable_move_candidates");
        return result;
    }

    private Godot.Collections.Array<Vector2I> CollectReachableMoveCandidatesImpl(
        BattleAiContext context
    )
    {
        var candidates = new Godot.Collections.Array<Vector2I>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return candidates;
        }

        var seen = new HashSet<Vector2I>();
        Vector2I origin = actor.coord;
        int maxMovePoints = Mathf.Max(actor.current_move_points, 0);
        var frontier = new Queue<(Vector2I Coord, int Cost)>();
        var bestCosts = new Dictionary<Vector2I, int> { [origin] = 0 };
        frontier.Enqueue((origin, 0));
        while (frontier.Count > 0)
        {
            (Vector2I currentCoord, int currentCost) = frontier.Dequeue();
            if (currentCost != bestCosts.GetValueOrDefault(currentCoord, int.MaxValue))
            {
                continue;
            }
            foreach (Vector2I neighbor in grid.get_neighbors_4(state, currentCoord))
            {
                if (!grid.can_unit_step_between_anchors(state, actor, currentCoord, neighbor))
                {
                    continue;
                }
                int nextCost = currentCost + GetMoveCost(context, actor, neighbor);
                if (nextCost > maxMovePoints)
                {
                    continue;
                }
                if (nextCost >= bestCosts.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    continue;
                }
                bestCosts[neighbor] = nextCost;
                frontier.Enqueue((neighbor, nextCost));
                if (seen.Add(neighbor))
                {
                    candidates.Add(neighbor);
                }
            }
        }

        var sorted = new List<Vector2I>();
        foreach (Vector2I candidate in candidates)
        {
            sorted.Add(candidate);
        }
        sorted.Sort(
            (left, right) =>
            {
                int leftDistance = grid.get_distance(origin, left);
                int rightDistance = grid.get_distance(origin, right);
                if (leftDistance == rightDistance)
                {
                    if (left.Y != right.Y)
                    {
                        return left.Y.CompareTo(right.Y);
                    }
                    return left.X.CompareTo(right.X);
                }
                return rightDistance.CompareTo(leftDistance);
            }
        );
        candidates.Clear();
        foreach (Vector2I candidate in sorted)
        {
            candidates.Add(candidate);
        }
        return candidates;
    }

    private ScreeningContext BuildScreeningContext(BattleAiContext context)
    {
        AiTraceRecorder.enter("_build_screening_context");
        ScreeningContext result = BuildScreeningContextImpl(context);
        AiTraceRecorder.exit("_build_screening_context");
        return result;
    }

    private ScreeningMetrics ApplyScreeningScore(
        BattleAiContext context,
        BattleAiScoreInput scoreInput,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        AiTraceRecorder.enter("_apply_screening_score");
        ScreeningMetrics result = ApplyScreeningScoreImpl(
            context,
            scoreInput,
            anchorCoord,
            screeningContext
        );
        AiTraceRecorder.exit("_apply_screening_score");
        return result;
    }

    private ScreeningMetrics ApplyScreeningScoreImpl(
        BattleAiContext context,
        BattleAiScoreInput scoreInput,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        ScreeningMetrics metrics = BuildScreeningMetrics(
            context,
            anchorCoord,
            screeningContext,
            scoreInput
        );
        if (scoreInput is BattleAiScoreInput typed)
        {
            typed.total_score += metrics.Bonus;
            typed.position_objective_score += metrics.Bonus;
        }
        return metrics;
    }

    private ScreeningContext BuildScreeningContextImpl(BattleAiContext context)
    {
        if (screening_mode != ScreeningRangedAlly)
        {
            return ScreeningContext.Disabled();
        }
        if (
            GetContextState(context) == null
            || GetContextUnit(context) == null
            || GetContextGrid(context) == null
        )
        {
            return ScreeningContext.Disabled();
        }
        if (
            _get_hp_basis_points(GetContextUnit(context))
            < Mathf.Max(screening_min_hp_basis_points, 0)
        )
        {
            return ScreeningContext.Disabled("low_hp");
        }

        List<BattleUnitState> protectedAllies = CollectScreeningProtectedAllies(context);
        if (protectedAllies.Count == 0)
        {
            return ScreeningContext.Disabled("no_protected_allies");
        }

        List<ScreeningThreatEntry> threatEntries = CollectScreeningThreatEntries(
            context,
            protectedAllies
        );
        if (threatEntries.Count == 0)
        {
            return ScreeningContext.Disabled("no_contact_threats");
        }

        return new ScreeningContext
        {
            Enabled = true,
            ProtectedAllies = protectedAllies,
            ThreatEntries = threatEntries,
        };
    }

    private List<BattleUnitState> CollectScreeningProtectedAllies(BattleAiContext context)
    {
        var allies = new List<BattleUnitState>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        if (state == null || actor == null)
        {
            return allies;
        }

        foreach (BattleUnitState allyUnit in state.GetUnitsTyped())
        {
            if (allyUnit == null || !allyUnit.is_alive)
            {
                continue;
            }
            if (allyUnit.unit_id == actor.unit_id || allyUnit.faction_id != actor.faction_id)
            {
                continue;
            }
            int allyAttackRange = _resolve_unit_effective_threat_range(context, allyUnit);
            if (allyAttackRange < Mathf.Max(screening_ally_min_attack_range, 1))
            {
                continue;
            }
            allies.Add(allyUnit);
        }
        return allies;
    }

    private List<ScreeningThreatEntry> CollectScreeningThreatEntries(
        BattleAiContext context,
        List<BattleUnitState> protectedAllies
    )
    {
        var threatEntries = new List<ScreeningThreatEntry>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        if (state == null || actor == null)
        {
            return threatEntries;
        }

        foreach (BattleUnitState threatUnit in state.GetUnitsTyped())
        {
            if (threatUnit == null || !threatUnit.is_alive)
            {
                continue;
            }
            if (threatUnit.faction_id == actor.faction_id)
            {
                continue;
            }

            int contactRange = ResolveScreeningUnitContactThreatRange(context, threatUnit);
            if (contactRange <= 0)
            {
                continue;
            }

            foreach (BattleUnitState protectedUnit in protectedAllies)
            {
                if (protectedUnit == null)
                {
                    continue;
                }
                int threatDistance = _distance_between_units(context, threatUnit, protectedUnit);
                int threatReach =
                    contactRange
                    + Mathf.Max(threatUnit.current_move_points, 0)
                    + Mathf.Max(screening_threat_distance_buffer, 0);
                if (threatDistance > threatReach)
                {
                    continue;
                }
                int basePathCost = ResolveScreeningThreatPathCost(
                    context,
                    threatUnit,
                    protectedUnit,
                    contactRange,
                    new Vector2I(-999999, -999999),
                    false
                );
                if (basePathCost >= ScreeningPathUnreachableCost)
                {
                    continue;
                }
                if (basePathCost > threatReach)
                {
                    continue;
                }
                threatEntries.Add(
                    new ScreeningThreatEntry
                    {
                        ThreatUnit = threatUnit,
                        ProtectedUnit = protectedUnit,
                        ContactRange = contactRange,
                        ThreatDistance = threatDistance,
                        ThreatReach = threatReach,
                        BasePathCost = basePathCost,
                    }
                );
            }
        }
        return threatEntries;
    }

    private int ResolveScreeningUnitContactThreatRange(
        BattleAiContext context,
        BattleUnitState threatUnit
    )
    {
        if (context == null || threatUnit == null)
        {
            return -1;
        }
        int bestRange = -1;
        GDictionary skillDefs = context.skill_defs;
        foreach (StringName rawSkillId in threatUnit.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
            {
                continue;
            }
            SkillDef skillDef = GdInterop.GetObject<SkillDef>(skillDefs, skillId);
            if (skillDef?.combat_profile is not CombatSkillDef combatProfile)
            {
                continue;
            }
            if (
                combatProfile.target_team_filter == "ally"
                || combatProfile.target_team_filter == "self"
            )
            {
                continue;
            }
            if (!_skill_has_tag(skillDef, "melee") && !_skill_has_tag(skillDef, "weapon"))
            {
                continue;
            }
            int effectiveRange = BattleRangeService.get_effective_skill_range(threatUnit, skillDef);
            if (effectiveRange > Mathf.Max(screening_enemy_max_contact_range, 1))
            {
                continue;
            }
            bestRange = Mathf.Max(bestRange, effectiveRange);
        }

        int weaponRange = BattleRangeService.get_weapon_attack_range(threatUnit);
        if (weaponRange > 0 && weaponRange <= Mathf.Max(screening_enemy_max_contact_range, 1))
        {
            bestRange = Mathf.Max(bestRange, weaponRange);
        }
        return bestRange;
    }

    private ScreeningMetrics BuildScreeningMetrics(
        BattleAiContext context,
        Vector2I anchorCoord,
        ScreeningContext screeningContext,
        BattleAiScoreInput scoreInput
    )
    {
        if (GetContextUnit(context) == null || screeningContext == null || !screeningContext.Enabled)
        {
            return new ScreeningMetrics();
        }

        ScreeningMetrics bestMetrics = new();
        ScreeningMetrics currentMetrics = BuildBestScreeningAnchorMetrics(
            context,
            GetContextUnit(context).coord,
            screeningContext
        );
        ScreeningMetrics candidateMetrics = BuildBestScreeningAnchorMetrics(
            context,
            anchorCoord,
            screeningContext
        );
        ApplyScreeningDistanceBandCap(candidateMetrics, scoreInput);

        int candidateBonus = candidateMetrics.Bonus;
        int currentBonus = currentMetrics.Bonus;
        if (candidateBonus <= currentBonus)
        {
            if (currentBonus > 0 && candidateBonus < currentBonus)
            {
                int penalty = Mathf.Min(
                    currentBonus - candidateBonus,
                    Mathf.Max(Mathf.Max(screening_path_bonus, 0) / 2, 1)
                );
                ScreeningMetrics penaltyMetrics = candidateMetrics.Clone();
                if (string.IsNullOrEmpty(penaltyMetrics.ThreatUnitId))
                {
                    penaltyMetrics.ThreatUnitId = currentMetrics.ThreatUnitId;
                }
                if (string.IsNullOrEmpty(penaltyMetrics.ProtectedUnitId))
                {
                    penaltyMetrics.ProtectedUnitId = currentMetrics.ProtectedUnitId;
                }
                penaltyMetrics.Bonus = -penalty;
                penaltyMetrics.Penalty = penalty;
                penaltyMetrics.CurrentBonus = currentBonus;
                penaltyMetrics.CandidateBonus = candidateBonus;
                penaltyMetrics.LostBonus = currentBonus - candidateBonus;
                return penaltyMetrics;
            }
            return bestMetrics;
        }

        bestMetrics = candidateMetrics.Clone();
        bestMetrics.Bonus = candidateBonus - currentBonus;
        bestMetrics.CurrentBonus = currentBonus;
        bestMetrics.CandidateBonus = candidateBonus;
        return bestMetrics;
    }

    private ScreeningMetrics BuildBestScreeningAnchorMetrics(
        BattleAiContext context,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        if (screeningContext == null)
        {
            return new ScreeningMetrics();
        }
        if (screeningContext.AnchorMetricsCache.TryGetValue(anchorCoord, out ScreeningMetrics cached))
        {
            return cached.Clone();
        }

        ScreeningMetrics bestMetrics = new();
        BattleUnitState actor = GetContextUnit(context);
        foreach (ScreeningThreatEntry entry in screeningContext.ThreatEntries)
        {
            BattleUnitState threatUnit = entry?.ThreatUnit;
            BattleUnitState protectedUnit = entry?.ProtectedUnit;
            if (actor == null || threatUnit == null || protectedUnit == null)
            {
                continue;
            }

            int anchorToThreat = _distance_from_anchor_to_unit(
                context,
                actor,
                anchorCoord,
                threatUnit
            );
            int anchorToProtected = _distance_from_anchor_to_unit(
                context,
                actor,
                anchorCoord,
                protectedUnit
            );
            bool onShortestPath = anchorToThreat + anchorToProtected == entry.ThreatDistance;
            bool keepsContact =
                anchorToThreat <= Mathf.Max(desired_max_distance, entry.ContactRange);
            int ownContactRange = ResolveScreeningUnitContactThreatRange(context, actor);
            if (ownContactRange <= 0)
            {
                ownContactRange = Mathf.Max(desired_max_distance, 1);
            }
            bool canCounterattack = anchorToThreat <= ownContactRange;
            int blockedPathCost = ResolveScreeningThreatPathCost(
                context,
                threatUnit,
                protectedUnit,
                entry.ContactRange,
                anchorCoord,
                true
            );
            int pathCostDelta = CalculateScreeningPathCostDelta(
                entry.BasePathCost,
                blockedPathCost,
                entry.ThreatReach
            );
            bool increasesPathCost = pathCostDelta > 0;
            bool hardBlock = blockedPathCost >= ScreeningPathUnreachableCost;
            bool canProjectPressure = keepsContact || canCounterattack;
            if (!increasesPathCost && !canProjectPressure)
            {
                continue;
            }

            int bonus = 0;
            if (increasesPathCost)
            {
                if (canProjectPressure)
                {
                    bonus += Mathf.Max(screening_path_bonus, 0);
                    if (pathCostDelta > 1)
                    {
                        bonus +=
                            Mathf.Min(pathCostDelta - 1, 2)
                            * (Mathf.Max(screening_path_bonus, 0) / 3);
                    }
                }
                else
                {
                    if (pathCostDelta < 2 && !hardBlock)
                    {
                        continue;
                    }
                    bonus += Mathf.Max(screening_path_bonus, 0) / 2;
                    if (hardBlock)
                    {
                        bonus += Mathf.Max(screening_path_bonus, 0) / 3;
                    }
                    else if (pathCostDelta > 2)
                    {
                        bonus +=
                            Mathf.Min(pathCostDelta - 2, 2)
                            * (Mathf.Max(screening_path_bonus, 0) / 6);
                    }
                }
            }
            if (keepsContact)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            else if (canCounterattack)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            if (onShortestPath && canProjectPressure && !increasesPathCost)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            if (bonus <= 0)
            {
                continue;
            }
            if (bonus < bestMetrics.Bonus)
            {
                continue;
            }
            if (bonus == bestMetrics.Bonus && pathCostDelta <= bestMetrics.PathCostDelta)
            {
                continue;
            }
            bestMetrics = new ScreeningMetrics
            {
                Bonus = bonus,
                ThreatUnitId = threatUnit.unit_id.ToString(),
                ProtectedUnitId = protectedUnit.unit_id.ToString(),
                AnchorToThreat = anchorToThreat,
                AnchorToProtected = anchorToProtected,
                ThreatDistance = entry.ThreatDistance,
                BasePathCost = entry.BasePathCost,
                BlockedPathCost = blockedPathCost,
                PathCostDelta = pathCostDelta,
                HardBlock = hardBlock,
                OnShortestPath = onShortestPath,
                KeepsContact = keepsContact,
                CanCounterattack = canCounterattack,
            };
        }

        screeningContext.AnchorMetricsCache[anchorCoord] = bestMetrics.Clone();
        return bestMetrics.Clone();
    }

    private void ApplyScreeningDistanceBandCap(ScreeningMetrics metrics, BattleAiScoreInput scoreInput)
    {
        if (metrics == null || scoreInput == null)
        {
            return;
        }
        if (metrics.HardBlock)
        {
            return;
        }
        if (GetScoreInputDistanceGap(scoreInput) <= 0)
        {
            return;
        }
        int bonus = metrics.Bonus;
        int cap = Mathf.Max(screening_path_bonus, 0) / 3;
        if (cap <= 0 || bonus <= cap)
        {
            return;
        }
        metrics.UncappedBonus = bonus;
        metrics.Bonus = cap;
        metrics.DistanceBandCapped = true;
    }

    private static int CalculateScreeningPathCostDelta(
        int basePathCost,
        int blockedPathCost,
        int threatReach
    )
    {
        if (basePathCost >= ScreeningPathUnreachableCost)
        {
            return 0;
        }
        if (blockedPathCost >= ScreeningPathUnreachableCost)
        {
            return Mathf.Max(threatReach - basePathCost + 1, 1);
        }
        return Mathf.Max(blockedPathCost - basePathCost, 0);
    }

    private int ResolveScreeningThreatPathCost(
        BattleAiContext context,
        BattleUnitState threatUnit,
        BattleUnitState protectedUnit,
        int contactRange,
        Vector2I blockerAnchor,
        bool useBlocker
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (
            state == null
            || actor == null
            || grid == null
            || threatUnit == null
            || protectedUnit == null
        )
        {
            return ScreeningPathUnreachableCost;
        }

        Godot.Collections.Array<Vector2I> blockerCoords = useBlocker
            ? grid.get_unit_target_coords(actor, blockerAnchor)
            : new Godot.Collections.Array<Vector2I>();
        Godot.Collections.Array<Vector2I> restoreCoords = new();
        actor.refresh_footprint();
        foreach (Vector2I coord in actor.occupied_coords)
        {
            restoreCoords.Add(coord);
        }
        foreach (Vector2I coord in blockerCoords)
        {
            restoreCoords.Add(coord);
        }

        Dictionary<Vector2I, StringName> occupantSnapshot = SnapshotScreeningOccupants(
            context,
            restoreCoords
        );
        try
        {
            grid.set_occupants(state, ToUntypedCoords(actor.occupied_coords), "");
            if (useBlocker)
            {
                grid.set_occupants(state, ToUntypedCoords(blockerCoords), actor.unit_id);
            }

            Godot.Collections.Array<Vector2I> destinations =
                CollectScreeningThreatContactDestinations(
                    context,
                    threatUnit,
                    protectedUnit,
                    contactRange
                );
            if (destinations.Count == 0)
            {
                return ScreeningPathUnreachableCost;
            }

            int bestCost = ScreeningPathUnreachableCost;
            int pathBudget = BuildPathSearchBudget(context);
            foreach (Vector2I destination in destinations)
            {
                GDictionary pathResult = grid.resolve_unit_move_path(
                    state,
                    threatUnit,
                    threatUnit.coord,
                    destination,
                    pathBudget,
                    BuildMoveCostProvider(context)
                );
                MovePathResult resolvedPath = MovePathResult.FromDictionary(pathResult);
                if (!resolvedPath.Allowed)
                {
                    continue;
                }
                bestCost = Mathf.Min(bestCost, resolvedPath.Cost);
            }
            return bestCost;
        }
        finally
        {
            RestoreScreeningOccupants(context, occupantSnapshot);
        }
    }

    private Godot.Collections.Array<Vector2I> CollectScreeningThreatContactDestinations(
        BattleAiContext context,
        BattleUnitState threatUnit,
        BattleUnitState protectedUnit,
        int contactRange
    )
    {
        var destinations = new Godot.Collections.Array<Vector2I>();
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null || threatUnit == null || protectedUnit == null)
        {
            return destinations;
        }

        int resolvedContactRange = Mathf.Max(contactRange, 1);
        var seen = new HashSet<Vector2I>();
        protectedUnit.refresh_footprint();
        foreach (Vector2I occupiedCoord in protectedUnit.occupied_coords)
        {
            for (
                int y = occupiedCoord.Y - resolvedContactRange;
                y <= occupiedCoord.Y + resolvedContactRange;
                y += 1
            )
            {
                for (
                    int x = occupiedCoord.X - resolvedContactRange;
                    x <= occupiedCoord.X + resolvedContactRange;
                    x += 1
                )
                {
                    Vector2I coord = new(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!grid.is_inside(state, coord))
                    {
                        continue;
                    }
                    int distance = _distance_from_anchor_to_unit(
                        context,
                        threatUnit,
                        coord,
                        protectedUnit
                    );
                    if (distance <= 0 || distance > resolvedContactRange)
                    {
                        continue;
                    }
                    if (
                        !grid.can_place_footprint(
                            state,
                            coord,
                            threatUnit.footprint_size,
                            threatUnit.unit_id,
                            threatUnit
                        )
                    )
                    {
                        continue;
                    }
                    destinations.Add(coord);
                }
            }
        }

        var sorted = new System.Collections.Generic.List<Vector2I>();
        foreach (Vector2I destination in destinations)
        {
            sorted.Add(destination);
        }
        sorted.Sort(
            (left, right) =>
            {
                int leftDistance = grid.get_distance(threatUnit.coord, left);
                int rightDistance = grid.get_distance(threatUnit.coord, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                if (left.Y != right.Y)
                {
                    return left.Y.CompareTo(right.Y);
                }
                return left.X.CompareTo(right.X);
            }
        );
        destinations.Clear();
        foreach (Vector2I destination in sorted)
        {
            destinations.Add(destination);
        }
        return destinations;
    }

    private Dictionary<Vector2I, StringName> SnapshotScreeningOccupants(
        BattleAiContext context,
        Godot.Collections.Array<Vector2I> coords
    )
    {
        var snapshot = new Dictionary<Vector2I, StringName>();
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null)
        {
            return snapshot;
        }
        foreach (Vector2I coord in coords)
        {
            if (snapshot.ContainsKey(coord))
            {
                continue;
            }
            BattleCellState cell = grid.get_cell(state, coord);
            if (cell == null)
            {
                continue;
            }
            snapshot[coord] = cell.occupant_unit_id;
        }
        return snapshot;
    }

    private void RestoreScreeningOccupants(
        BattleAiContext context,
        Dictionary<Vector2I, StringName> snapshot
    )
    {
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null || snapshot == null)
        {
            return;
        }
        foreach (KeyValuePair<Vector2I, StringName> entry in snapshot)
        {
            grid.set_occupant(state, entry.Key, entry.Value);
        }
    }

    private BattleAiDecision BuildPathProgressDecision(
        BattleAiContext context,
        BattleUnitState focusTarget,
        GDictionary actionTrace,
        MoveDistanceContract distanceContract
    )
    {
        AiTraceRecorder.enter("_build_path_progress_decision");
        BattleAiDecision result = BuildPathProgressDecisionImpl(
            context,
            focusTarget,
            actionTrace,
            distanceContract
        );
        AiTraceRecorder.exit("_build_path_progress_decision");
        return result;
    }

    private BattleAiDecision BuildPathProgressDecisionImpl(
        BattleAiContext context,
        BattleUnitState focusTarget,
        GDictionary actionTrace,
        MoveDistanceContract distanceContract
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return null;
        }
        if (focusTarget == null || actor.current_move_points <= 0)
        {
            return null;
        }

        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        int currentDistance = _distance_from_anchor_to_unit(
            context,
            actor,
            actor.coord,
            focusTarget
        );
        if (currentDistance >= resolvedMinDistance && currentDistance <= resolvedMaxDistance)
        {
            return null;
        }

        BattleAiDecision bestDecision = null;
        int bestPathCost = int.MaxValue;
        int bestPathLength = int.MaxValue;
        int pathSearchBudget = BuildPathSearchBudget(context);
        Godot.Collections.Array<Vector2I> destinations = CollectDistanceBandDestinations(
            context,
            focusTarget,
            distanceContract
        );
        MovePathTreeCosts pathTreeCosts = new();
        if (destinations.Count >= PathTreeFilterMinDestinations)
        {
            AiTraceRecorder.enter("grid_service.build_unit_move_path_tree");
            BattleMovePathTreeResult pathTree = grid.build_unit_move_path_tree_typed(
                state,
                actor,
                actor.coord,
                pathSearchBudget,
                BuildMoveCostProvider(context)
            );
            AiTraceRecorder.exit("grid_service.build_unit_move_path_tree");
            pathTreeCosts = MovePathTreeCosts.FromPathTreeResult(pathTree);
        }

        foreach (Vector2I destination in destinations)
        {
            if (!pathTreeCosts.IsEmpty)
            {
                if (!pathTreeCosts.TryGetCost(destination, out int knownPathCost))
                {
                    _trace_count_increment(actionTrace, "path_tree_unreachable_skip_count", 1);
                    continue;
                }
                if (bestDecision != null && knownPathCost > bestPathCost)
                {
                    _trace_count_increment(actionTrace, "path_tree_cost_skip_count", 1);
                    continue;
                }
            }

            AiTraceRecorder.enter("grid_service.resolve_unit_move_path");
            GDictionary pathResult = grid.resolve_unit_move_path(
                state,
                actor,
                actor.coord,
                destination,
                pathSearchBudget,
                BuildMoveCostProvider(context)
            );
            AiTraceRecorder.exit("grid_service.resolve_unit_move_path");
            MovePathResult resolvedPath = MovePathResult.FromDictionary(pathResult);
            if (!resolvedPath.Allowed)
            {
                continue;
            }

            Vector2I moveTarget = ResolveCurrentTurnPathTarget(context, resolvedPath.Path);
            if (moveTarget == actor.coord)
            {
                continue;
            }

            BattleCommand command = _build_move_command(context, moveTarget);
            BattlePreview preview = _build_fast_typed_move_preview(
                context,
                moveTarget,
                resolvedPath.Cost
            );
            if (preview?.allowed != true)
            {
                _trace_count_increment(actionTrace, "preview_reject_count", 1);
                continue;
            }

            int pathCost = resolvedPath.Cost;
            int pathLength = resolvedPath.Path.Count;
            BattleAiScoreInput scoreInput = _build_action_score_input(
                context,
                "move",
                action_id.ToString(),
                command,
                preview,
                new GDictionary
                {
                    ["position_target_unit_id"] = focusTarget.unit_id,
                    ["position_anchor_coord"] = moveTarget,
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["position_objective_kind"] = new StringName("distance_band_progress"),
                    ["action_base_score"] = 60,
                }
            );
            _trace_offer_candidate(
                actionTrace,
                _build_candidate_summary(
                    $"path_to_{destination.X}_{destination.Y}_via_{moveTarget.X}_{moveTarget.Y}",
                    command,
                    scoreInput,
                    new GDictionary
                    {
                        ["path_cost"] = pathCost,
                        ["path_length"] = pathLength,
                        ["path_destination"] = destination,
                    }
                )
            );

            if (bestDecision != null)
            {
                if (pathCost > bestPathCost)
                {
                    continue;
                }
                if (pathCost == bestPathCost && pathLength >= bestPathLength)
                {
                    continue;
                }
            }
            bestPathCost = pathCost;
            bestPathLength = pathLength;
            bestDecision = _create_scored_decision(
                command,
                scoreInput,
                $"{actor.display_name} 准备绕路逼近 {focusTarget.display_name}（路径成本 {pathCost}，评分 {_score_total(scoreInput)}）。"
            );
        }
        return bestDecision;
    }

    private Godot.Collections.Array<Vector2I> CollectDistanceBandDestinations(
        BattleAiContext context,
        BattleUnitState focusTarget,
        MoveDistanceContract distanceContract
    )
    {
        AiTraceRecorder.enter("_collect_distance_band_destinations");
        Godot.Collections.Array<Vector2I> result = CollectDistanceBandDestinationsImpl(
            context,
            focusTarget,
            distanceContract
        );
        AiTraceRecorder.exit("_collect_distance_band_destinations");
        return result;
    }

    private Godot.Collections.Array<Vector2I> CollectDistanceBandDestinationsImpl(
        BattleAiContext context,
        BattleUnitState focusTarget,
        MoveDistanceContract distanceContract
    )
    {
        var destinations = new Godot.Collections.Array<Vector2I>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null || focusTarget == null)
        {
            return destinations;
        }

        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        int maxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance);
        var seen = new HashSet<Vector2I>();
        focusTarget.refresh_footprint();
        foreach (Vector2I occupiedCoord in focusTarget.occupied_coords)
        {
            for (int y = occupiedCoord.Y - maxDistance; y <= occupiedCoord.Y + maxDistance; y++)
            {
                for (int x = occupiedCoord.X - maxDistance; x <= occupiedCoord.X + maxDistance; x++)
                {
                    Vector2I coord = new(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!grid.is_inside(state, coord))
                    {
                        continue;
                    }
                    int distance = _distance_from_anchor_to_unit(
                        context,
                        actor,
                        coord,
                        focusTarget
                    );
                    if (distance < resolvedMinDistance || distance > resolvedMaxDistance)
                    {
                        continue;
                    }
                    destinations.Add(coord);
                }
            }
        }

        var sorted = new List<Vector2I>();
        foreach (Vector2I destination in destinations)
        {
            sorted.Add(destination);
        }
        sorted.Sort(
            (left, right) =>
            {
                int leftDistance = grid.get_distance(actor.coord, left);
                int rightDistance = grid.get_distance(actor.coord, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                if (left.Y != right.Y)
                {
                    return left.Y.CompareTo(right.Y);
                }
                return left.X.CompareTo(right.X);
            }
        );
        destinations.Clear();
        foreach (Vector2I destination in sorted)
        {
            destinations.Add(destination);
        }
        return destinations;
    }

    private Vector2I ResolveCurrentTurnPathTarget(
        BattleAiContext context,
        Godot.Collections.Array<Vector2I> path
    )
    {
        AiTraceRecorder.enter("_resolve_current_turn_path_target");
        Vector2I result = ResolveCurrentTurnPathTargetImpl(context, path);
        AiTraceRecorder.exit("_resolve_current_turn_path_target");
        return result;
    }

    private Vector2I ResolveCurrentTurnPathTargetImpl(
        BattleAiContext context,
        Godot.Collections.Array<Vector2I> path
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return new Vector2I(-1, -1);
        }
        if (path == null || path.Count <= 1)
        {
            return actor.coord;
        }
        int spentCost = 0;
        int maxMovePoints = Mathf.Max(actor.current_move_points, 0);
        Vector2I bestCoord = actor.coord;
        for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
        {
            Vector2I nextCoord = path[pathIndex];
            int stepCost = GetMoveCost(context, actor, nextCoord);
            if (spentCost + stepCost > maxMovePoints)
            {
                break;
            }
            spentCost += stepCost;
            bestCoord = nextCoord;
        }
        return bestCoord;
    }

    private static bool IsBetterMoveToRangeScoreInput(
        BattleAiScoreInput candidate,
        BattleAiScoreInput bestCandidate
    )
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        int candidateGap = GetScoreInputDistanceGap(candidate);
        int bestGap = GetScoreInputDistanceGap(bestCandidate);
        if (candidateGap != bestGap)
        {
            if (candidateGap < 0)
            {
                return false;
            }
            if (bestGap < 0)
            {
                return true;
            }
            return candidateGap < bestGap;
        }
        return _is_better_skill_score_input(candidate, bestCandidate);
    }

    private static GArray ToUntypedCoords(Godot.Collections.Array<Vector2I> coords)
    {
        var result = new GArray();
        if (coords == null)
        {
            return result;
        }
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static int BuildPathSearchBudget(BattleAiContext context)
    {
        BattleState state = GetContextState(context);
        if (state == null)
        {
            return 32;
        }
        return Mathf.Max(state.map_size.X * state.map_size.Y, state.map_size.X + state.map_size.Y);
    }

    private static int GetScoreInputDistanceGap(BattleAiScoreInput scoreInput)
    {
        if (scoreInput is not BattleAiScoreInput typed)
        {
            return -1;
        }
        int distanceValue = typed.distance_to_primary_coord;
        int minDistance = typed.desired_min_distance;
        int maxDistance = typed.desired_max_distance;
        if (distanceValue < 0 || minDistance < 0 || maxDistance < minDistance)
        {
            return -1;
        }
        if (distanceValue < minDistance)
        {
            return minDistance - distanceValue;
        }
        if (distanceValue > maxDistance)
        {
            return distanceValue - maxDistance;
        }
        return 0;
    }

    private static BattleState GetContextState(BattleAiContext context)
    {
        return context?.state;
    }

    private static BattleUnitState GetContextUnit(BattleAiContext context)
    {
        return context?.unit_state;
    }

    private static BattleGridService GetContextGrid(BattleAiContext context)
    {
        return context?.grid_service;
    }

    private static int GetMoveCost(
        BattleAiContext context,
        BattleUnitState unitState,
        Vector2I targetCoord
    )
    {
        return context != null ? context.get_move_cost(unitState, targetCoord) : 1;
    }

    private static System.Func<BattleUnitState, Vector2I, int> BuildMoveCostProvider(
        BattleAiContext context
    )
    {
        return context != null
            ? (unitState, targetCoord) => context.get_move_cost(unitState, targetCoord)
            : null;
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
        var targets = new System.Collections.Generic.List<BattleAiUnitSnapshot>();
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
        return query.distance_from_anchor_to_target(
            actorSnapshot.coord,
            actorSnapshot.footprint_size,
            targetSnapshot.unit_id
        );
    }

    private MoveDistanceContract ResolveMoveDistanceContract(BattleAiContext context)
    {
        GDictionary rawDistanceContract = _resolve_desired_distance_contract(
            context,
            null,
            range_skill_ids
        );
        MoveDistanceContract distanceContract = MoveDistanceContract.FromDictionary(
            rawDistanceContract,
            desired_min_distance,
            desired_max_distance
        );
        int effectiveAttackRange = ResolveEffectiveAttackRange(context);
        if (effectiveAttackRange < 0)
        {
            return distanceContract;
        }

        int configuredMin = distanceContract.ConfiguredMinDistance;
        int configuredMax = distanceContract.ConfiguredMaxDistance;
        int resolvedMin = Mathf.Max(configuredMin, 0);
        int resolvedMax = configuredMax;
        if (effectiveAttackRange >= 0)
        {
            resolvedMax = effectiveAttackRange;
        }
        if (resolvedMax >= 0 && resolvedMin > resolvedMax)
        {
            resolvedMin = resolvedMax;
        }

        distanceContract.DesiredMinDistance = resolvedMin;
        distanceContract.DesiredMaxDistance = Mathf.Max(resolvedMax, resolvedMin);
        distanceContract.EffectiveAttackRange = effectiveAttackRange;
        return distanceContract;
    }

    private int ResolveEffectiveAttackRange(BattleAiContext context)
    {
        if (context?.unit_state == null)
        {
            return -1;
        }

        int bestGroundAoeCastRange = -1;
        int bestFallbackRange = -1;
        foreach (StringName skillId in _resolve_known_skill_ids(context, range_skill_ids))
        {
            if (skillId == "")
            {
                continue;
            }
            SkillDef skillDef = _get_skill_def(context, skillId);
            if (skillDef?.combat_profile == null)
            {
                continue;
            }
            if (_get_skill_cast_block_reason(context, skillDef).Length > 0)
            {
                continue;
            }
            if (enable_aoe_setup_positioning && IsGroundAoeSkill(context.unit_state, skillDef))
            {
                bestGroundAoeCastRange = Mathf.Max(
                    bestGroundAoeCastRange,
                    BattleRangeService.get_effective_skill_range(context.unit_state, skillDef)
                );
            }
            bestFallbackRange = Mathf.Max(
                bestFallbackRange,
                BattleRangeService.get_effective_skill_distance_contract_range(
                    context.unit_state,
                    skillDef
                )
            );
        }
        return bestGroundAoeCastRange >= 0 ? bestGroundAoeCastRange : bestFallbackRange;
    }

    private int ResolveEffectiveAttackRange(BattleAiQueryService query, bool useGroundAoeCastRange)
    {
        int bestRange = -1;
        foreach (StringName skillId in range_skill_ids)
        {
            if (skillId == "")
            {
                continue;
            }
            if (!query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord skillRecord))
            {
                continue;
            }
            MoveSkillRecord record = MoveSkillRecord.FromSkillRecord(skillRecord);
            if (record.TargetMode == "" && record.ActorEffectiveRange < 0)
            {
                continue;
            }
            int range = useGroundAoeCastRange && record.IsGroundAoe
                ? record.ActorEffectiveCastRange
                : record.ActorEffectiveRange;
            bestRange = Mathf.Max(bestRange, range);
        }
        return bestRange;
    }

    private bool HasGroundAoeSetupSkill(BattleAiQueryService query)
    {
        if (!enable_aoe_setup_positioning || query == null)
        {
            return false;
        }
        foreach (StringName skillId in range_skill_ids)
        {
            if (skillId == "")
            {
                continue;
            }
            if (
                query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord record)
                && MoveSkillRecord.FromSkillRecord(record).IsSetupSkill
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundAoeSkill(BattleUnitState actor, SkillDef skillDef)
    {
        if (actor == null || skillDef?.combat_profile == null)
        {
            return false;
        }
        CombatSkillDef combatProfile = skillDef.combat_profile;
        if (combatProfile.target_mode != "ground")
        {
            return false;
        }
        int skillLevel = actor.known_skill_level_map.ContainsKey(skillDef.skill_id)
            ? actor.known_skill_level_map[skillDef.skill_id].AsInt32()
            : actor.known_active_skill_ids.Contains(skillDef.skill_id)
                ? 1
                : 0;
        StringName areaPattern = combatProfile.get_effective_area_pattern(skillLevel);
        int areaValue = combatProfile.get_effective_area_value(skillLevel);
        return areaValue > 0 && areaPattern != "" && areaPattern != "single" && areaPattern != "self";
    }

    private static int ResolveMaxCandidateCount(BattleAiUnitSnapshot actorSnapshot)
    {
        int movePoints = Mathf.Max(actorSnapshot?.current_move_points ?? 0, 0);
        return Mathf.Max(DefaultMaxCandidateCount, movePoints * 4);
    }

    private static List<ScreeningThreatEntry> ReadScreeningThreatEntries(GDictionary source)
    {
        var result = new List<ScreeningThreatEntry>();
        foreach (GDictionary entryData in ReadDictionaryArray(source, "threat_entries"))
        {
            result.Add(DecodeScreeningThreatEntry(entryData));
        }
        return result;
    }

    private static List<GDictionary> ReadDictionaryArray(GDictionary source, string key)
    {
        var result = new List<GDictionary>();
        foreach (GDictionary value in GdInterop.ReadDictionaryItems(GdInterop.GetArray(source, key)))
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ReadVector2IArray(
        GDictionary source,
        string key
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in GdInterop.GetArray(source, key))
        {
            result.Add(coord);
        }
        return result;
    }

    private static MovePathSearchBudget BuildPathBudget(
        BattleAiQueryService query,
        StringName actorId,
        int maxCandidateCount
    )
    {
        BattleMovementQueryService movementQuery = query.get_movement_query_service();
        if (movementQuery != null)
        {
            GDictionary budget = movementQuery.build_path_search_budget(
                actorId,
                new GDictionary
                {
                    ["max_candidate_count"] = maxCandidateCount,
                    ["include_origin"] = false,
                    ["prefer_progress"] = true,
                }
            );
            if (budget.Count > 0)
            {
                return MovePathSearchBudget.FromDictionary(budget, maxCandidateCount);
            }
        }
        return new MovePathSearchBudget
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

using Godot;

public partial class BattleAiCandidateProbeTestAction : EnemyAiAction
{
    public int desired_min_distance { get; set; } = 1;
    public int desired_max_distance { get; set; } = 2;
    public bool inline_decide_called { get; set; }

    public BattleAiCandidateProbeTestAction()
    {
        action_id = "candidate_probe";
        score_bucket_id = "positioning";
        action_intent = "positioning";
    }

    public override bool UsesCandidateRequest() => true;

    internal override BattleAiCandidateRequest BuildCandidateRequest(BattleAiQueryService query)
    {
        var request = new BattleAiCandidateRequest
        {
            FamilyId = "move_to_range",
            ActionId = action_id,
            ActionLabel = "candidate probe",
            ActionIntent = action_intent,
            ScoreBucketId = score_bucket_id,
            ActorUnitId = query?.GetActorId() ?? "",
            FocusTargetUnitId = "hero",
            DesiredMinDistance = desired_min_distance,
            DesiredMaxDistance = desired_max_distance,
            MaxCandidateCount = 4,
        };
        request.SetMoveToRangeSections(
            new MoveToRangePathSearchBudget
            {
                MaxCost = 2,
                MaxDestinations = 4,
                PreferProgress = true,
            },
            new MoveToRangeTacticalParams
            {
                TargetSelector = "nearest_enemy",
                PositionObjectiveKind = "distance_band_progress",
            },
            new MoveToRangeRuntimeMetadata
            {
                ConfiguredDesiredMinDistance = desired_min_distance,
                ConfiguredDesiredMaxDistance = desired_max_distance,
                EffectiveAttackRange = -1,
            }
        );
        return request;
    }

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        inline_decide_called = true;
        return null;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        return new Godot.Collections.Array<string>();
    }
}

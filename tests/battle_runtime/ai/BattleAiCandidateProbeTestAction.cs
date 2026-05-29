using Godot;

public partial class BattleAiCandidateProbeTestAction : EnemyAiAction
{
    public int desired_min_distance { get; set; } = 1;
    public int desired_max_distance { get; set; } = 2;
    public bool legacy_decide_called { get; set; }

    public BattleAiCandidateProbeTestAction()
    {
        action_id = "candidate_probe";
        score_bucket_id = "positioning";
        action_intent = "positioning";
    }

    public override bool uses_candidate_request() => true;

    public override BattleAiCandidateRequest build_candidate_request(BattleAiQueryService query)
    {
        var request = new BattleAiCandidateRequest
        {
            FamilyId = "move_to_range",
            ActionId = action_id,
            ActionLabel = "candidate probe",
            ActionIntent = action_intent,
            ScoreBucketId = score_bucket_id,
            ActorUnitId = query?.get_actor_id() ?? "",
            FocusTargetUnitId = "hero",
            DesiredMinDistance = desired_min_distance,
            DesiredMaxDistance = desired_max_distance,
            MaxCandidateCount = 4,
            PathSearchBudget = new Godot.Collections.Dictionary
            {
                ["max_cost"] = 2,
                ["max_nodes"] = 0,
                ["max_destinations"] = 4,
                ["path_tree_min_destination_count"] = 0,
                ["include_origin"] = false,
                ["prefer_progress"] = true,
            },
            TacticalParams = new Godot.Collections.Dictionary
            {
                ["target_selector"] = new StringName("nearest_enemy"),
                ["range_skill_ids"] = new Godot.Collections.Array(),
                ["position_objective_kind"] = new StringName("distance_band_progress"),
            },
            RuntimeMetadata = new Godot.Collections.Dictionary
            {
                ["configured_desired_min_distance"] = desired_min_distance,
                ["configured_desired_max_distance"] = desired_max_distance,
                ["effective_attack_range"] = -1,
            },
        };
        return request;
    }

    public override BattleAiDecision decide(BattleAiContext context)
    {
        legacy_decide_called = true;
        return null;
    }

    public override Godot.Collections.Array<string> validate_schema()
    {
        return new Godot.Collections.Array<string>();
    }
}

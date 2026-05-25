using Godot;

[GlobalClass]
public partial class BadReturnCandidateEvaluator : BattleAiCandidateEvaluationService
{
    public string evaluate_bad_request(GodotObject _request, GodotObject _query)
    {
        return "not a decision";
    }
}

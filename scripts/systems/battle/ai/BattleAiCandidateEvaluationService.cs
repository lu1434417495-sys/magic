using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiCandidateEvaluationService : RefCounted
{
    private static readonly StringName FamilyMoveToRange = "move_to_range";

    private readonly Godot.Collections.Dictionary<StringName, Callable> _evaluators = new();

    public void setup(GodotObject score_service)
    {
        if (score_service == null)
        {
            GD.PushError("BattleAiCandidateEvaluationService.setup requires BattleAiScoreService.");
        }
    }

    public void register_evaluator(StringName family_id, Callable evaluator)
    {
        if (family_id == FamilyMoveToRange)
        {
            GD.PushError("Evaluator for built-in family move_to_range must not be overridden.");
            return;
        }
        if (_evaluators.ContainsKey(family_id))
        {
            GD.PushError($"Evaluator for family {family_id} is already registered.");
            return;
        }
        if (evaluator.Target == null || evaluator.Method == (StringName)"")
        {
            GD.PushError($"Evaluator for family {family_id} must be a valid Callable.");
            return;
        }
        _evaluators[family_id] = evaluator;
    }

    public Variant evaluate(GodotObject request, GodotObject query)
    {
        if (request == null)
        {
            GD.PushError("BattleAiCandidateEvaluationService.evaluate requires BattleAiCandidateRequest.");
            return default;
        }

        StringName familyId = ReadStringName(request, "FamilyId");
        if (_evaluators.TryGetValue(familyId, out Callable evaluator))
        {
            return evaluator.Call(request, query);
        }
        if (familyId == FamilyMoveToRange)
        {
            return evaluate_move_to_range_request(request, query);
        }

        GD.PushError($"Unsupported candidate family_id {familyId}.");
        return default;
    }

    public Variant evaluate_move_to_range_request(GodotObject _request, GodotObject _query)
    {
        return default;
    }

    public string _trim_reason(string value)
    {
        return value?.StripEdges() ?? "";
    }

    private static StringName ReadStringName(GodotObject source, string property)
    {
        Variant value = source.Get(property);
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(value.ToString()),
        };
    }
}

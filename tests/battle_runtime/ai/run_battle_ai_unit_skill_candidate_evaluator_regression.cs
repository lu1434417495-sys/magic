using System;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_unit_skill_candidate_evaluator_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestEvaluatorIsPlainCSharpHelper();
        TestEvaluatorUsesPascalCasePublicApi();

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI unit skill candidate evaluator regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI unit skill candidate evaluator regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestEvaluatorIsPlainCSharpHelper()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        AssertTrue(
            evaluatorType.IsSealed,
            "BattleAiUnitSkillCandidateEvaluator 应是 sealed helper。"
        );
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(evaluatorType),
            "BattleAiUnitSkillCandidateEvaluator 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            evaluatorType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiUnitSkillCandidateEvaluator 不应注册 GlobalClass。"
        );
    }

    private void TestEvaluatorUsesPascalCasePublicApi()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        MethodInfo evaluateMethod = evaluatorType.GetMethod(
            "Evaluate",
            BindingFlags.Public | BindingFlags.Instance
        );
        AssertTrue(evaluateMethod != null, "BattleAiUnitSkillCandidateEvaluator 应公开 Evaluate()。");
        if (evaluateMethod != null)
        {
            ParameterInfo[] parameters = evaluateMethod.GetParameters();
            AssertEq(
                evaluateMethod.ReturnType,
                typeof(BattleAiDecision),
                "Evaluate() 应返回 BattleAiDecision。"
            );
            AssertTrue(parameters.Length == 2, "Evaluate() 应只接收 action/context 两个参数。");
            if (parameters.Length == 2)
            {
                AssertEq(
                    parameters[0].ParameterType,
                    typeof(UseUnitSkillAction),
                    "Evaluate() 第一个参数应是 UseUnitSkillAction。"
                );
                AssertEq(
                    parameters[1].ParameterType,
                    typeof(BattleAiContext),
                    "Evaluate() 第二个参数应是 BattleAiContext。"
                );
            }
        }
        AssertTrue(
            evaluatorType.GetMethod("evaluate", BindingFlags.Public | BindingFlags.Instance) == null,
            "BattleAiUnitSkillCandidateEvaluator 不应保留 evaluate() 兼容别名。"
        );
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(Type actual, Type expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}

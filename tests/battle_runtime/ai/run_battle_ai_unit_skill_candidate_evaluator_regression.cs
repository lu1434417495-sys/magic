using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_unit_skill_candidate_evaluator_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestEvaluatorIsPlainCSharpHelper();
        TestEvaluatorUsesPascalCasePublicApi();
        TestEvaluatorTraceMetadataSurfaceUsesTypedDictionary();
        TestEvaluatorScoreMetadataSurfaceUsesTypedDictionary();

        Quit(_test.Finish("Battle AI unit skill candidate evaluator regression"));
    }

    private void TestEvaluatorIsPlainCSharpHelper()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        _test.True(
            evaluatorType.IsSealed,
            "BattleAiUnitSkillCandidateEvaluator 应是 sealed helper。"
        );
    }

    private void TestEvaluatorUsesPascalCasePublicApi()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        MethodInfo evaluateMethod = evaluatorType.GetMethod(
            "Evaluate",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        _test.True(evaluateMethod != null, "BattleAiUnitSkillCandidateEvaluator 应保留同程序集 Evaluate()。");
        if (evaluateMethod != null)
        {
            ParameterInfo[] parameters = evaluateMethod.GetParameters();
            _test.Eq(
                evaluateMethod.ReturnType,
                typeof(BattleAiDecision),
                "Evaluate() 应返回 BattleAiDecision。"
            );
            _test.True(parameters.Length == 2, "Evaluate() 应只接收 action/context 两个参数。");
            if (parameters.Length == 2)
            {
                _test.Eq(
                    parameters[0].ParameterType,
                    typeof(UseUnitSkillAction),
                    "Evaluate() 第一个参数应是 UseUnitSkillAction。"
                );
                _test.Eq(
                    parameters[1].ParameterType,
                    typeof(BattleAiContext),
                    "Evaluate() 第二个参数应是 BattleAiContext。"
                );
            }
        }
        _test.True(
            evaluatorType.GetMethod("evaluate", BindingFlags.Public | BindingFlags.Instance) == null,
            "BattleAiUnitSkillCandidateEvaluator 不应保留 evaluate() 兼容别名。"
        );
    }

    private void TestEvaluatorTraceMetadataSurfaceUsesTypedDictionary()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        MethodInfo beginActionTrace = evaluatorType.GetMethod(
            "BeginActionTrace",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        MethodInfo offerCandidate = evaluatorType.GetMethod(
            "OfferCandidate",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        MethodInfo buildCandidateExtra = evaluatorType.GetMethod(
            "BuildCandidateExtra",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        _test.True(
            beginActionTrace != null
                && beginActionTrace.GetParameters()[3].ParameterType
                    == typeof(IReadOnlyDictionary<string, object>),
            "BattleAiUnitSkillCandidateEvaluator.BeginActionTrace() trace metadata 应直接接收 typed dictionary。"
        );
        _test.True(
            offerCandidate != null
                && offerCandidate.GetParameters()[6].ParameterType
                    == typeof(IReadOnlyDictionary<string, object>),
            "BattleAiUnitSkillCandidateEvaluator.OfferCandidate() candidate metadata 应直接接收 typed dictionary。"
        );
        _test.True(
            buildCandidateExtra != null
                && buildCandidateExtra.ReturnType == typeof(Dictionary<string, object>),
            "BattleAiUnitSkillCandidateEvaluator.BuildCandidateExtra() 应返回 typed dictionary。"
        );
    }

    private void TestEvaluatorScoreMetadataSurfaceUsesTypedDictionary()
    {
        Type contextType = typeof(BattleAiContext);
        MethodInfo typedBuildSkillScoreInput = contextType.GetMethod(
            "BuildSkillScoreInputTyped",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        MethodInfo buildPositionMetadata = typeof(BattleAiTypedActionHelper).GetMethod(
            "BuildPositionMetadata",
            BindingFlags.Public | BindingFlags.Instance
        );

        _test.True(
            typedBuildSkillScoreInput != null
                && typedBuildSkillScoreInput.GetParameters()[4].ParameterType
                    == typeof(IReadOnlyDictionary<string, object>),
            "BattleAiContext.BuildSkillScoreInputTyped() score metadata 应直接接收 typed dictionary。"
        );
        _test.True(
            buildPositionMetadata != null
                && buildPositionMetadata.ReturnType == typeof(Dictionary<string, object>),
            "BattleAiTypedActionHelper.BuildPositionMetadata() 应返回 typed dictionary。"
        );
    }
}

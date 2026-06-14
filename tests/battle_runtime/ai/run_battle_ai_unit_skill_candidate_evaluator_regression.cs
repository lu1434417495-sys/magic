using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_unit_skill_candidate_evaluator_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestEvaluatorIsPlainCSharpHelper();

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

}

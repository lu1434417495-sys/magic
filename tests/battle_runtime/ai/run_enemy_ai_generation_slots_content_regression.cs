using System.Collections.Generic;
using Godot;

public partial class run_enemy_ai_generation_slots_content_regression : LifecycleTestSceneTree
{
    private static readonly StringName[] BrainIds =
    {
        "frontline_bulwark", "healer_controller", "mage_controller",
        "melee_aggressor", "ranged_archer", "ranged_controller", "ranged_suppressor",
    };
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        foreach (StringName brainId in BrainIds)
        {
            _test.True(snapshot.EnemyBrains.TryGetValue(brainId, out EnemyAiBrainDefinition brain), $"JSON brain {brainId} 应发布。");
            if (brain == null) continue;
            _test.True(brain.TransitionRules.Count > 0, $"JSON brain {brainId} 应保留 transition rules。");
            foreach (EnemyAiStateDefinition state in brain.StateOrder)
            {
                _test.True(state.Actions.Count > 0, $"JSON brain {brainId} state {state.StateId} 应保留 actions。");
                _test.True(state.GenerationSlots.Count > 0, $"JSON brain {brainId} state {state.StateId} 应保留 generation slots。");
            }
        }
        RequestTestExit(_test.Finish("Enemy AI JSON generation slots content regression"));
    }
}

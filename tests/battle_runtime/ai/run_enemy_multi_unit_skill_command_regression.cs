using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public partial class run_enemy_multi_unit_skill_command_regression : SceneTree
{
    public override void _Initialize()
    {
        var method = typeof(UseMultiUnitSkillAction).GetMethod(
            "_build_multi_unit_skill_command",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (method == null)
            throw new Exception("missing _build_multi_unit_skill_command");

        var source = new BattleUnitState { unit_id = "enemy_1", coord = new Vector2I(0, 0) };
        var targetA = new BattleUnitState { unit_id = "hero_1", coord = new Vector2I(2, 0) };
        var targetB = new BattleUnitState { unit_id = "hero_2", coord = new Vector2I(3, 0) };
        var context = new BattleAiContext { unit_state = source };
        var variant = new CombatCastVariantDef { variant_id = "multi" };
        var targets = new List<BattleUnitState> { targetA, targetB };

        var command = method.Invoke(null, new object[] { context, new StringName("enemy_chain"), variant, targets }) as BattleCommand;
        if (command == null)
            throw new Exception("command was null");
        if (command.TargetUnitIdsTyped.Count != 2)
            throw new Exception($"expected 2 target ids, got {command.TargetUnitIdsTyped.Count}");
        if (command.TargetUnitIdsTyped[0] != new StringName("hero_1") || command.TargetUnitIdsTyped[1] != new StringName("hero_2"))
            throw new Exception("target ids were not preserved in command backing list");

        GD.Print("PASS enemy multi-unit command target ids persist");
        Quit(0);
    }
}

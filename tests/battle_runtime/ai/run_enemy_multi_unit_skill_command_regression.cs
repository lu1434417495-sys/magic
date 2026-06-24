using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public partial class run_enemy_multi_unit_skill_command_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        new("enemy_multi_unit_skill_command", quarantineOnDrain: true);

    public override void _Initialize()
    {
        try
        {
            var method = typeof(UseMultiUnitSkillAction).GetMethod(
                "_build_multi_unit_skill_command",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (method == null)
                throw new Exception("missing _build_multi_unit_skill_command");

            var source = _runtimeScope.OwnWrapper(
                new BattleUnitState { unit_id = "enemy_1", coord = new Vector2I(0, 0) },
                "source"
            );
            var targetA = _runtimeScope.OwnWrapper(
                new BattleUnitState { unit_id = "hero_1", coord = new Vector2I(2, 0) },
                "target-a"
            );
            var targetB = _runtimeScope.OwnWrapper(
                new BattleUnitState { unit_id = "hero_2", coord = new Vector2I(3, 0) },
                "target-b"
            );
            var context = _runtimeScope.OwnValueGraph(
                new BattleAiContext { unit_state = source },
                "context"
            );
            var variant = _runtimeScope.OwnWrapper(
                new CombatCastVariantDef { variant_id = "multi" },
                "variant"
            );
            var targets = new List<BattleUnitState> { targetA, targetB };

            var command = method.Invoke(null, new object[] { context, new StringName("enemy_chain"), variant, targets }) as BattleCommand;
            _test.True(command != null, "command was null");
            if (command != null)
            {
                _runtimeScope.OwnValueGraph(command, "command");
                _test.Eq(command.TargetUnitIdsTyped.Count, 2, "expected 2 target ids");
                _test.Eq(
                    command.TargetUnitIdsTyped[0],
                    new StringName("hero_1"),
                    "first target id was preserved"
                );
                _test.Eq(
                    command.TargetUnitIdsTyped[1],
                    new StringName("hero_2"),
                    "second target id was preserved"
                );
            }
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            _runtimeScope.Close();
        }

        Quit(_test.Finish("enemy multi-unit command target ids persist"));
    }
}

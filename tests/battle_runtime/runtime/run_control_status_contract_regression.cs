using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_control_status_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPetrifiedSelfSaveFailureSkipsTurn();
        TestPetrifiedSelfSaveSuccessRemovesStatusAndAllowsAction();
        TestMadnessSelfSaveFailureReturnsAiOverridePolicy();
        TestMadnessSelfSaveSuccessRemovesStatusAndAllowsAction();
        RequestTestExit(_test.Finish("Control status contract regression"));
    }

    private void TestPetrifiedSelfSaveFailureSkipsTurn()
    {
        Fixture fixture = BuildTurnResolver();
        if (fixture.Resolver == null)
        {
            return;
        }
        AddControlStatus(
            fixture.Target,
            "petrified",
            selfSaveAbility: "constitution",
            selfSaveDc: 15,
            selfSaveRollOverride: 1,
            selfSaveTag: "constitution"
        );

        BattleTurnControlStatusResult result = fixture.Resolver.ResolveTurnControlStatusResult(
            fixture.Target,
            new BattleEventBatch()
        );
        _test.True(result.SkipTurn, "Petrified self-save failure must skip the current turn.");
        _test.False(result.AiControlled, "Petrified self-save failure must not request AI control.");
        _test.Eq(fixture.Target.GetCurrentAp(), 0, "Petrified self-save failure must clear AP.");
        _test.Eq(
            fixture.Target.GetCurrentMovePoints(),
            0,
            "Petrified self-save failure must clear movement."
        );
        _test.True(
            fixture.Target.HasStatusEffect("petrified"),
            "Petrified self-save failure must keep the status."
        );
    }

    private void TestPetrifiedSelfSaveSuccessRemovesStatusAndAllowsAction()
    {
        Fixture fixture = BuildTurnResolver();
        if (fixture.Resolver == null)
        {
            return;
        }
        AddControlStatus(
            fixture.Target,
            "petrified",
            selfSaveAbility: "constitution",
            selfSaveDc: 15,
            selfSaveRollOverride: 20,
            selfSaveTag: "constitution"
        );

        BattleTurnControlStatusResult result = fixture.Resolver.ResolveTurnControlStatusResult(
            fixture.Target,
            new BattleEventBatch()
        );
        _test.False(
            result.SkipTurn,
            "Petrified self-save success must allow the unit to act immediately."
        );
        _test.True(result.StatusRemoved, "Petrified self-save success must report status_removed.");
        _test.False(
            fixture.Target.HasStatusEffect("petrified"),
            "Petrified self-save success must remove the status."
        );
    }

    private void TestMadnessSelfSaveFailureReturnsAiOverridePolicy()
    {
        Fixture fixture = BuildTurnResolver();
        if (fixture.Resolver == null)
        {
            return;
        }
        AddControlStatus(
            fixture.Target,
            "madness",
            selfSaveAbility: "willpower",
            selfSaveDc: 15,
            selfSaveRollOverride: 1,
            selfSaveTag: "willpower"
        );

        BattleTurnControlStatusResult result = fixture.Resolver.ResolveTurnControlStatusResult(
            fixture.Target,
            new BattleEventBatch()
        );
        _test.False(result.SkipTurn, "Madness self-save failure must not skip the turn.");
        _test.True(result.AiControlled, "Madness self-save failure must request AI control.");
        _test.Eq(
            result.AiTargetPolicy,
            "any_unit",
            "Madness AI override must be allowed to target any unit."
        );
        _test.True(
            result.CleanupOnTurnEnd,
            "Madness AI override must request turn-end cleanup."
        );
        _test.True(
            fixture.Target.HasStatusEffect("madness"),
            "Madness self-save failure must keep the status."
        );
    }

    private void TestMadnessSelfSaveSuccessRemovesStatusAndAllowsAction()
    {
        Fixture fixture = BuildTurnResolver();
        if (fixture.Resolver == null)
        {
            return;
        }
        AddControlStatus(
            fixture.Target,
            "madness",
            selfSaveAbility: "willpower",
            selfSaveDc: 15,
            selfSaveRollOverride: 20,
            selfSaveTag: "willpower"
        );

        BattleTurnControlStatusResult result = fixture.Resolver.ResolveTurnControlStatusResult(
            fixture.Target,
            new BattleEventBatch()
        );
        _test.False(
            result.SkipTurn,
            "Madness self-save success must allow the unit to act immediately."
        );
        _test.False(
            result.AiControlled,
            "Madness self-save success must not request AI control."
        );
        _test.True(result.StatusRemoved, "Madness self-save success must report status_removed.");
        _test.False(
            fixture.Target.HasStatusEffect("madness"),
            "Madness self-save success must remove the status."
        );
    }

    private static Fixture BuildTurnResolver()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(4, 4));
        BattleUnitState target = BuildUnit("target", "Target", "enemy", new Vector2I(1, 1));
        state.SetUnit(target);
        state.enemy_unit_ids.Add(target.unit_id);
        runtime._grid_service.PlaceUnit(state, target, target.GetAnchorCoord(), true);
        runtime.SetupStateForTests(state);
        return new Fixture(runtime, runtime._skill_turn_resolver, target);
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "control_status_contract",
            phase = "unit_acting",
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 100,
            mp: 20,
            stamina: 20,
            ap: 2,
            movePoints: 4,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        return unit;
    }

    private static void AddControlStatus(
        BattleUnitState unitState,
        StringName statusId,
        StringName selfSaveAbility,
        int selfSaveDc,
        int selfSaveRollOverride,
        StringName selfSaveTag
    )
    {
        BattleStatusEffectState status = new()
        {
            status_id = statusId,
            source_unit_id = "source",
            power = 1,
            stacks = 1,
            duration = -1,
            self_save_ability = selfSaveAbility,
            self_save_dc = selfSaveDc,
            self_save_roll_override = selfSaveRollOverride,
            self_save_tag = selfSaveTag,
        };
        unitState.SetStatusEffect(status);
    }

    private readonly record struct Fixture(
        BattleRuntimeModule Runtime,
        BattleRuntimeSkillTurnResolver Resolver,
        BattleUnitState Target
    );
}

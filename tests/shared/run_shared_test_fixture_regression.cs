using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_shared_test_fixture_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHarnessRecordsFailures();
        TestStubRngRollsAreClampedAndCounted();
        TestLocalBattleFixtureBuildsStateAndUnits();
        TestFixedResolversUseInjectedRolls();
        Quit(_test.Finish("Shared test fixture regression"));
    }

    private void TestHarnessRecordsFailures()
    {
        var localHarness = new TestHarness();
        localHarness.Eq(1, 2, "local failure should be recorded");
        _test.Eq(localHarness.Failures.Count, 1, "TestHarness 应记录失败数量。");
        _test.True(localHarness.Failures.Count > 0, "TestHarness 应能报告存在失败。");
    }

    private void TestStubRngRollsAreClampedAndCounted()
    {
        var rng = new StubRng(new[] { 25, 4 });
        _test.Eq(rng.RandiRange(1, 20), 20, "StubRng 应 clamp 过大的注入掷骰。");
        _test.Eq(rng.RandiRange(1, 20), 4, "StubRng 应按顺序返回注入掷骰。");
        _test.Eq(rng.CallCount, 2, "StubRng 应记录调用次数。");
        _test.Eq(rng.RemainingCount(), 0, "StubRng 应暴露剩余 roll 数。");
    }

    private void TestLocalBattleFixtureBuildsStateAndUnits()
    {
        BattleState state = BuildState("shared_fixture_contract", new Vector2I(2, 1));
        BattleUnitState player = BuildUnit("hero", "player", Vector2I.Zero, currentAp: 3);
        BattleUnitState enemy = BuildUnit("enemy", "enemy", new Vector2I(1, 0));
        AddUnits(state, new[] { player }, new[] { enemy });

        _test.Eq(state.cells.Count, 2, "C# fixture 应按地图尺寸生成格子。");
        _test.Eq(state.active_unit_id, new StringName("hero"), "C# fixture 应默认首个友军为 active unit。");
        _test.Eq(player.current_ap, 3, "C# fixture 应应用 unit options。");
        _test.Eq(enemy.faction_id, new StringName("enemy"), "C# fixture enemy helper 应设置敌方阵营。");

        var runtime = new BattleRuntimeModule();
        runtime._state = state;
        _test.True(runtime.GetState() == state, "C# fixture 应能直接安装 runtime battle state。");
    }

    private void TestFixedResolversUseInjectedRolls()
    {
        var resolver = new FixedRollDamageResolver(new GArray { 2 }, new GArray { 20 });
        _test.True(resolver is BattleDamageResolver, "FixedRollDamageResolver 应继承 BattleDamageResolver。");

        BattleUnitState source = BuildUnit("source", "player", Vector2I.Zero);
        BattleUnitState target = BuildUnit("target", "enemy", Vector2I.Right);
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "physical_slash",
            power = 1,
            dice_count = 1,
            dice_sides = 6,
        };

        GDictionary result = resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary());
        _test.Eq(DictInt(result, "damage"), 3, "FixedRollDamageResolver 应使用注入 damage roll。");

        var hitResolver = new FixedHitResolver(17);
        AttackRollResult hit = hitResolver.RollAttackCheck(
            new BattleState(),
            new AttackCheckInput(requiredRoll: 10)
        );
        _test.True(hit.Success, "FixedHitResolver 应返回命中。");
        _test.Eq(hit.Roll, 17, "FixedHitResolver 应使用注入命中骰。");
    }

    private static BattleState BuildState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
            cells = BuildCells(mapSize),
        };
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
        return state;
    }

    private static GDictionary BuildCells(Vector2I mapSize)
    {
        var cells = new GDictionary();
        for (int y = 0; y < Mathf.Max(mapSize.Y, 0); y++)
        {
            for (int x = 0; x < Mathf.Max(mapSize.X, 0); x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = "land",
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                cells[coord] = cell;
            }
        }
        return cells;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentAp = 1
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_ap = currentAp,
            current_move_points = 2,
            current_hp = factionId == new StringName("enemy") ? 30 : 100,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue("hp_max", unit.current_hp);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void AddUnits(BattleState state, BattleUnitState[] allyUnits, BattleUnitState[] enemyUnits)
    {
        state.units = new GDictionary();
        state.ally_unit_ids = new Godot.Collections.Array<StringName>();
        state.enemy_unit_ids = new Godot.Collections.Array<StringName>();
        foreach (BattleUnitState unit in allyUnits)
        {
            state.units[unit.unit_id] = unit;
            state.ally_unit_ids.Add(unit.unit_id);
        }
        foreach (BattleUnitState unit in enemyUnits)
        {
            state.units[unit.unit_id] = unit;
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id = state.ally_unit_ids.Count > 0 ? state.ally_unit_ids[0] : new StringName("");
    }

    private static int DictInt(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : 0;
    }

    private sealed class StubRng
    {
        private readonly int[] _rolls;

        public StubRng(int[] rolls)
        {
            _rolls = rolls ?? Array.Empty<int>();
        }

        public int CallCount { get; private set; }

        public int RandiRange(int minValue, int maxValue)
        {
            int lower = Math.Min(minValue, maxValue);
            int upper = Math.Max(minValue, maxValue);
            int roll = CallCount < _rolls.Length ? _rolls[CallCount] : lower;
            CallCount += 1;
            return Math.Clamp(roll, lower, upper);
        }

        public int RemainingCount()
        {
            return Math.Max(_rolls.Length - CallCount, 0);
        }
    }
}

using System.Collections.Generic;
using Godot;

public partial class run_battle_state_disadvantage_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestAttackDisadvantageTriggersOnTwoAdjacentEnemies();
        TestReadViewDisadvantageNormalizesStaleCandidateFootprints();
        TestAttackDisadvantageTriggersOnLowHp();
        TestAttackDisadvantageTriggersOnStrongAttackDebuff();
        TestAttackDisadvantageTriggersOnFrightenedAndKeepsFearFamilyStatuses();
        TestAttackDisadvantageTriggersOnExplicitSceneTag();
        TestAttackDisadvantageDoesNotTriggerOnSingleAdjacentEnemy();
        TestAttackDisadvantageDoesNotTriggerOnWrongElementTarget();
        TestAttackDisadvantageDoesNotTriggerOnBadTacticalChoice();
        TestAttackDisadvantageDoesNotTriggerOnEconomicDelay();
        TestAttackDisadvantageDoesNotTriggerOnSoftDebuff();
        RequestTestExit(_test.Finish("Battle state disadvantage regression"));
    }

    private void TestReadViewDisadvantageNormalizesStaleCandidateFootprints()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("read_view_attacker", "player", new Vector2I(2, 2));
        BattleUnitState defender = BuildUnit("read_view_defender", "enemy", new Vector2I(8, 8));
        BattleUnitState sideEnemy = BuildUnit("read_view_side_enemy", "enemy", new Vector2I(9, 8));
        AddUnits(state, attacker, defender, sideEnemy);

        // Simulate an external state writer that changed anchors without rebuilding the
        // derived occupied-coord snapshot. The read-view route must preserve State-route parity.
        defender.coord = new Vector2I(3, 2);
        sideEnemy.coord = new Vector2I(2, 1);

        _test.True(
            state.IsAttackDisadvantage(
                (BattleUnitReadView)attacker,
                (BattleUnitReadView)defender
            ),
            "read-view 包夹判定应先归一化所有候选单位的 stale footprint。"
        );
    }

    private void TestAttackDisadvantageTriggersOnTwoAdjacentEnemies()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("flanked_attacker", "player", new Vector2I(2, 2));
        BattleUnitState defender = BuildUnit("flanked_defender", "enemy", new Vector2I(3, 2));
        BattleUnitState sideEnemy = BuildUnit("flanked_side_enemy", "enemy", new Vector2I(2, 1));
        AddUnits(state, attacker, defender, sideEnemy);
        _test.True(state.IsAttackDisadvantage(attacker, defender), "被 2 个相邻敌人包夹时应判定为 attack disadvantage。");
    }

    private void TestAttackDisadvantageTriggersOnLowHp()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("low_hp_attacker", "player", new Vector2I(1, 1), 9, 30);
        BattleUnitState defender = BuildUnit("low_hp_defender", "enemy", new Vector2I(3, 1));
        AddUnits(state, attacker, defender);
        _test.True(state.IsAttackDisadvantage(attacker, defender), "当前 HP <= 30% 时应判定为 attack disadvantage。");
    }

    private void TestAttackDisadvantageTriggersOnStrongAttackDebuff()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("debuffed_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("debuffed_defender", "enemy", new Vector2I(3, 1));
        SetStatus(attacker, "frozen", 15);
        AddUnits(state, attacker, defender);
        _test.True(state.IsAttackDisadvantage(attacker, defender), "强攻击型 debuff 应触发 attack disadvantage。");
    }

    private void TestAttackDisadvantageTriggersOnFrightenedAndKeepsFearFamilyStatuses()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("frightened_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("frightened_defender", "enemy", new Vector2I(3, 1));
        SetStatus(attacker, "frightened", 15);
        AddUnits(state, attacker, defender);
        _test.True(state.IsAttackDisadvantage(attacker, defender), "frightened 应触发 attack disadvantage。");

        _test.True(
            BattleState.IsStrongAttackDisadvantageStatusId("fear"),
            "fear 应继续保留为强攻击劣势状态。"
        );
        _test.True(
            BattleState.IsStrongAttackDisadvantageStatusId("feared"),
            "feared 应继续保留为强攻击劣势状态。"
        );
        _test.True(
            BattleState.IsStrongAttackDisadvantageStatusId("terrified"),
            "terrified 应继续保留为强攻击劣势状态。"
        );
    }

    private void TestAttackDisadvantageTriggersOnExplicitSceneTag()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("tagged_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("tagged_defender", "enemy", new Vector2I(3, 1));
        state.attack_disadvantage_tags = new Godot.Collections.Array<StringName> { "darkness" };
        AddUnits(state, attacker, defender);
        _test.True(state.IsAttackDisadvantage(attacker, defender), "场景显式 hardship 标签应触发 attack disadvantage。");
    }

    private void TestAttackDisadvantageDoesNotTriggerOnSingleAdjacentEnemy()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("single_adjacent_attacker", "player", new Vector2I(2, 2));
        BattleUnitState defender = BuildUnit("single_adjacent_defender", "enemy", new Vector2I(3, 2));
        AddUnits(state, attacker, defender);
        _test.False(state.IsAttackDisadvantage(attacker, defender), "只有 1 个相邻敌人时不应误判为被包夹。");
    }

    private void TestAttackDisadvantageDoesNotTriggerOnWrongElementTarget()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("wrong_element_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("wrong_element_defender", "enemy", new Vector2I(3, 1));
        SetStatus(defender, "prismatic_barrier", 15);
        AddUnits(state, attacker, defender);
        _test.False(state.IsAttackDisadvantage(attacker, defender), "主动打错元素或命中抗性目标不应触发 attack disadvantage。");
    }

    private void TestAttackDisadvantageDoesNotTriggerOnBadTacticalChoice()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("bad_choice_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("bad_choice_defender", "enemy", new Vector2I(3, 1));
        defender.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 100);
        SetStatus(defender, "dodge_bonus_up", 15);
        AddUnits(state, attacker, defender);
        _test.False(state.IsAttackDisadvantage(attacker, defender), "故意挑高闪避目标这种坏选择不应触发 attack disadvantage。");
    }

    private void TestAttackDisadvantageDoesNotTriggerOnEconomicDelay()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("economic_delay_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("economic_delay_defender", "enemy", new Vector2I(3, 1));
        attacker.current_ap = 0;
        attacker.current_aura = 0;
        attacker.current_stamina = 0;
        AddUnits(state, attacker, defender);
        _test.False(state.IsAttackDisadvantage(attacker, defender), "纯经济拖延或资源打空不应触发 attack disadvantage。");
    }

    private void TestAttackDisadvantageDoesNotTriggerOnSoftDebuff()
    {
        var state = new BattleState();
        BattleUnitState attacker = BuildUnit("soft_debuff_attacker", "player", new Vector2I(1, 1));
        BattleUnitState defender = BuildUnit("soft_debuff_defender", "enemy", new Vector2I(3, 1));
        SetStatus(attacker, "slow", 15);
        AddUnits(state, attacker, defender);
        _test.False(state.IsAttackDisadvantage(attacker, defender), "非强攻击型 debuff 不应触发 attack disadvantage。");
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentHp = 30,
        int maxHp = 30
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = currentHp,
            current_ap = 2,
            current_mp = 4,
            current_stamina = 4,
            current_aura = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        return unit;
    }

    private static void AddUnits(BattleState state, params BattleUnitState[] units)
    {
        foreach (BattleUnitState unit in units)
        {
            state.SetUnit(unit);
            if (unit.faction_id == new StringName("enemy"))
                state.enemy_unit_ids.Add(unit.unit_id);
            else
                state.ally_unit_ids.Add(unit.unit_id);
        }
    }

    private static void SetStatus(BattleUnitState unit, StringName statusId, int durationTu)
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            duration = durationTu,
            power = 1,
            stacks = 1,
        };
        unit.SetStatusEffect(status);
    }
}

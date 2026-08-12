using System;
using Godot;

public partial class run_unit_skill_exclude_source_living_target_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestOrdinaryHealRejectsExcludedSourceAndDeadAlly();
            TestOrdinaryHealWithoutExclusionStillAllowsSource();
            TestFatalHealStillAllowsDeadAlly();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(
            _test.Finish("Unit skill exclude-source and living-target regression")
        );
    }

    private void TestOrdinaryHealRejectsExcludedSourceAndDeadAlly()
    {
        using Fixture fixture = new("ordinary_heal_target_filters");
        SkillDefinition skill = BuildHealSkill(
            "ordinary_other_living_ally_heal",
            "heal",
            excludeSource: true
        );

        _test.False(
            fixture.Runtime.GetUnitSkillTargetAffordance(
                fixture.Source,
                fixture.Source,
                skill,
                require_ap: false
            ).Allowed,
            "正式unit target affordance必须在选择阶段拒绝exclude_source施法者。"
        );
        _test.True(
            fixture.Runtime.GetUnitSkillTargetAffordance(
                fixture.Source,
                fixture.LivingAlly,
                skill,
                require_ap: false
            ).Allowed,
            "相邻存活友方应通过普通治疗的正式target affordance。"
        );
        _test.False(
            fixture.Runtime.GetUnitSkillTargetAffordance(
                fixture.Source,
                fixture.DeadAlly,
                skill,
                require_ap: false
            ).Allowed,
            "普通heal不得因为治疗效果而获得复活目标权限。"
        );
    }

    private void TestOrdinaryHealWithoutExclusionStillAllowsSource()
    {
        using Fixture fixture = new("ordinary_self_heal_allowed");
        SkillDefinition skill = BuildHealSkill(
            "ordinary_self_heal",
            "heal",
            excludeSource: false
        );

        _test.True(
            fixture.Runtime.GetUnitSkillTargetAffordance(
                fixture.Source,
                fixture.Source,
                skill,
                require_ap: false
            ).Allowed,
            "通用修复不得把所有普通治疗都改成不能选择自身。"
        );
    }

    private void TestFatalHealStillAllowsDeadAlly()
    {
        using Fixture fixture = new("fatal_heal_dead_target_allowed");
        SkillDefinition skill = BuildHealSkill(
            "fatal_heal",
            "heal_fatal",
            excludeSource: true
        );

        _test.True(
            fixture.Runtime.GetUnitSkillTargetAffordance(
                fixture.Source,
                fixture.DeadAlly,
                skill,
                require_ap: false
            ).Allowed,
            "只有heal_fatal应保留选择死亡友方的正式权限。"
        );
    }

    private static SkillDefinition BuildHealSkill(
        StringName skillId,
        StringName effectType,
        bool excludeSource
    )
    {
        var resource = new CombatEffectDef
        {
            effect_type = effectType,
            effect_target_team_filter = "ally",
            power = 5,
            exclude_source = excludeSource,
        };
        CombatEffectDefinition effect = CombatEffectDefinition.FromResource(
            resource,
            "test://unit_skill_exclude_source_living_target"
        );
        resource.Dispose();
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "ally",
                rangeValue: 1,
                apCost: 1
            )
        );
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly BattleRuntimeModule Runtime = new();
        internal readonly BattleState State;
        internal readonly BattleUnitState Source;
        internal readonly BattleUnitState LivingAlly;
        internal readonly BattleUnitState DeadAlly;

        internal Fixture(string battleId)
        {
            Runtime.setup();
            State = new BattleState
            {
                battle_id = battleId,
                phase = "unit_acting",
                active_unit_id = "source",
                map_size = new Vector2I(3, 1),
                timeline = new BattleTimelineState(),
            };
            for (int x = 0; x < 3; x++)
            {
                Vector2I coord = new(x, 0);
                State.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
            State.RebuildCellColumns();
            Runtime.SetupStateForTests(State);

            LivingAlly = AddUnit("living_ally", new Vector2I(0, 0));
            Source = AddUnit("source", new Vector2I(1, 0));
            DeadAlly = AddUnit("dead_ally", new Vector2I(2, 0));
            DeadAlly.SetCurrentHp(0);
            DeadAlly.MarkDead();
        }

        private BattleUnitState AddUnit(StringName unitId, Vector2I coord)
        {
            var unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = "player",
            }.WithCombatResourcesForTest(hp: 20, ap: 2, isAlive: true);
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.HpMax),
                20
            );
            unit.SetAnchorCoord(coord);
            State.SetUnit(unit);
            State.ally_unit_ids.Add(unit.unit_id);
            if (!Runtime._grid_service.PlaceUnit(State, unit, coord, true))
                throw new InvalidOperationException($"Failed to place {unitId}.");
            return unit;
        }

        public void Dispose()
        {
            State.ClearUnits();
            State.ClearCells();
            Runtime.SetupStateForTests(null);
            Runtime.dispose();
        }
    }
}

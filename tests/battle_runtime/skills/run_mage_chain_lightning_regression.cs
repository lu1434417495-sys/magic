using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_mage_chain_lightning_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "mage_chain_lightning";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestNormalChainHitsEveryUnitInRadiusOnce(skill);
            TestWetPrimaryExpandsOnlyTheChainRadius(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage chain lightning regression"));
    }

    private void TestNormalChainHitsEveryUnitInRadiusOnce(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载正式链式闪击技能资源。");
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("chain_lightning_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("chain_lightning_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState secondaryEnemy = fixture.AddUnit(
            BuildUnit("chain_lightning_secondary_enemy", "enemy", new Vector2I(4, 1))
        );
        BattleUnitState secondaryAlly = fixture.AddUnit(
            BuildUnit("chain_lightning_secondary_ally", "player", new Vector2I(5, 2))
        );
        BattleUnitState outsideEnemy = fixture.AddUnit(
            BuildUnit("chain_lightning_outside_enemy", "enemy", new Vector2I(6, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        BattlePreview allyPrimaryPreview = fixture.Runtime.PreviewCommand(
            BuildCommand(caster, secondaryAlly)
        );
        _test.True(
            allyPrimaryPreview != null && !allyPrimaryPreview.allowed,
            "链式闪击首目标必须是敌人，不能直接锁定友军。"
        );

        int primaryHpBefore = primary.GetCurrentHp();
        int secondaryEnemyHpBefore = secondaryEnemy.GetCurrentHp();
        int secondaryAllyHpBefore = secondaryAlly.GetCurrentHp();
        int outsideEnemyHpBefore = outsideEnemy.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "敌方首目标在射程内时应允许施放链式闪击。");

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "链式闪击应通过正式技能命令完成结算。");
        _test.True(
            batch?.changed_unit_ids.Contains(secondaryEnemy.unit_id) == true,
            "正式技能结算应把受到连锁伤害的次要目标记入 changed_unit_ids。"
        );
        _test.Eq(primaryHpBefore - primary.GetCurrentHp(), 24, "0级首目标应承受一次4D6完整伤害。");
        _test.Eq(
            secondaryEnemyHpBefore - secondaryEnemy.GetCurrentHp(),
            24,
            "基础连锁范围内的每名次要敌人应承受一次相同的4D6伤害。"
        );
        _test.Eq(
            secondaryAllyHpBefore - secondaryAlly.GetCurrentHp(),
            24,
            "基础连锁范围内的友军也应承受一次相同的4D6伤害。"
        );
        _test.Eq(
            outsideEnemy.GetCurrentHp(),
            outsideEnemyHpBefore,
            "距离首目标2格的敌人不应被基础1格连锁命中。"
        );
        _test.True(primary.GetStatusEffect("shocked") != null, "首目标豁免失败后应获得感电。");
        _test.True(
            secondaryEnemy.GetStatusEffect("shocked") != null,
            "范围内次要敌人豁免失败后应获得感电。"
        );
        _test.True(
            secondaryAlly.GetStatusEffect("shocked") != null,
            "范围内友军豁免失败后也应获得感电。"
        );
        _test.True(outsideEnemy.GetStatusEffect("shocked") == null, "范围外敌人不应获得感电。");
        _test.Eq(caster.GetCurrentAp(), 1, "链式闪击成功施放后应消耗1 AP。");
        _test.Eq(caster.GetCurrentMp(), 120, "链式闪击成功施放后应消耗120法力。");
    }

    private void TestWetPrimaryExpandsOnlyTheChainRadius(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState radiusTwoEnemy = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_radius_two_enemy", "enemy", new Vector2I(5, 2))
        );
        BattleUnitState radiusTwoAlly = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_radius_two_ally", "player", new Vector2I(3, 4))
        );
        BattleUnitState radiusThreeEnemy = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_radius_three_enemy", "enemy", new Vector2I(6, 2))
        );
        BattleCellState primaryCell = fixture.State.GetCell(primary.GetAnchorCoord());
        _test.True(primaryCell != null, "湿地连锁回归应能取得首目标地格。");
        primaryCell?.terrain_effect_ids.Add("wet");
        PrepareCaster(caster);
        fixture.Activate(caster);

        int radiusTwoEnemyHpBefore = radiusTwoEnemy.GetCurrentHp();
        int radiusTwoAllyHpBefore = radiusTwoAlly.GetCurrentHp();
        int radiusThreeEnemyHpBefore = radiusThreeEnemy.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(BuildCommand(caster, primary));

        _test.True(batch != null, "湿地上的首目标应允许完成链式闪击结算。");
        _test.Eq(
            radiusTwoEnemyHpBefore - radiusTwoEnemy.GetCurrentHp(),
            24,
            "首目标位于湿地时，距离2格的敌人应承受一次完整连锁伤害。"
        );
        _test.Eq(
            radiusTwoAllyHpBefore - radiusTwoAlly.GetCurrentHp(),
            24,
            "首目标位于湿地时，距离2格的友军也应承受一次完整连锁伤害。"
        );
        _test.Eq(
            radiusThreeEnemy.GetCurrentHp(),
            radiusThreeEnemyHpBefore,
            "湿地只应把连锁范围扩大至2格，不能命中距离3格的敌人。"
        );
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/mage_chain_lightning.tres",
            "mage_chain_lightning_regression"
        );

    private static void PrepareCaster(BattleUnitState caster)
    {
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        caster.SetCurrentAp(2);
        caster.SetCurrentMp(240);
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target?.unit_id ?? new StringName(""),
            target_coord = target?.GetAnchorCoord() ?? new Vector2I(-1, -1),
        };
        if (target != null)
        {
            command.AddTargetUnitId(target.unit_id);
        }
        return command;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "mage_chain_lightning_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y += 1)
        {
            for (int x = 0; x < mapSize.X; x += 1)
            {
                Vector2I coord = new(x, y);
                BattleCellState cell = new()
                {
                    coord = coord,
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 200,
            mp: 240,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 200);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 240);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 16);
        unit.attribute_snapshot.SetValue("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.SetValue("faith_luck_bonus", 0);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<BattleUnitState> _units = new();
        private readonly TestHarness _test;

        internal Fixture(SkillDefinition skill, Vector2I mapSize, TestHarness test)
        {
            _test = test;
            Runtime = new BattleRuntimeModule();
            Runtime.setup(
                null,
                new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
            );
            Runtime.ConfigureDamageResolverForTests(
                new FixedFailedSaveDamageResolver(new GArray(), new GArray { 10 })
            );
            Runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            State = BuildState(mapSize);
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }

        internal BattleUnitState AddUnit(BattleUnitState unit)
        {
            State.SetUnit(unit);
            if (unit.faction_id == new StringName("player"))
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            _test.True(
                Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true),
                $"测试单位应能放入棋盘：{unit.unit_id}"
            );
            _units.Add(unit);
            return unit;
        }

        internal void Activate(BattleUnitState caster)
        {
            State.active_unit_id = caster.unit_id;
            Runtime.SetupStateForTests(State);
        }

        public void Dispose()
        {
            Runtime?.Dispose();
            foreach (BattleUnitState unit in _units)
            {
                BattleTestFixture.DisposeBattleUnit(unit);
            }
            BattleTestFixture.DisposeBattleState(State);
        }
    }
}

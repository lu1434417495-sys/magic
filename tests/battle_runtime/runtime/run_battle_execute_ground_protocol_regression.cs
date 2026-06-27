using System.Collections.Generic;
using Godot;

public partial class run_battle_execute_ground_protocol_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestGroundExecutePreviewDenied();
        TestGroundExecuteIssueDeniedBeforeCost();
        TestGroundExecuteDoesNotMutateDamageOrStatus();
        Quit(_test.Finish("Battle execute ground protocol regression"));
    }

    private void TestGroundExecutePreviewDenied()
    {
        SkillDefinition skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);

        using BattleTestFixture fixture = CreateFixture("ground_execute_preview", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "地面技能携带 execute 时 preview 应直接拒绝。");
        _test.Eq(target.current_hp, 10, "地面 execute preview 不应伤害目标。");
        _test.False(target.HasStatusEffect("soul_fracture"), "地面 execute preview 不应附加状态。");

        BattleTestFixture.DisposeBattlePreview(preview);
        GodotSharpCleanup.ClearRuntimeReferences(command);
    }

    private void TestGroundExecuteIssueDeniedBeforeCost()
    {
        SkillDefinition skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);
        source.current_ap = 2;
        source.current_mp = 20;
        source.SetCooldownTyped(skill.SkillId, 0);

        using BattleTestFixture fixture = CreateFixture("ground_execute_issue", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(source.current_ap, 2, "地面 execute 被拒绝时不应消耗 AP。");
        _test.Eq(source.current_mp, 20, "地面 execute 被拒绝时不应消耗 MP。");
        _test.Eq(source.GetCooldownTyped(skill.SkillId), 0, "地面 execute 被拒绝时不应进入冷却。");

        GodotSharpCleanup.DisposeBatch(batch);
        GodotSharpCleanup.ClearRuntimeReferences(command);
    }

    private void TestGroundExecuteDoesNotMutateDamageOrStatus()
    {
        SkillDefinition skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);

        using BattleTestFixture fixture = CreateFixture("ground_execute_no_mutation", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(target.is_alive, "地面 execute 被拒绝时不应杀死目标。");
        _test.Eq(target.current_hp, 10, "地面 execute 被拒绝时不应改变目标 HP。");
        _test.False(target.HasStatusEffect("soul_fracture"), "地面 execute 被拒绝时不应附加 soul fracture。");

        GodotSharpCleanup.DisposeBatch(batch);
        GodotSharpCleanup.ClearRuntimeReferences(command);
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(4, 2),
            new[] { source },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDefinition MakeGroundExecuteSkill(int apCost, int mpCost, int cooldownTu) =>
        TestSkillDefinitionProjection.BuildSkill(
            "test_ground_execute",
            "测试地面律令",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "test_ground_execute",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "execute",
                        effectTargetTeamFilter: "enemy",
                        saveDcMode: "static",
                        saveDc: 999,
                        saveAbility: "willpower",
                        saveTag: "execute",
                        damageTag: "negative_energy",
                        thresholdMaxHpRatioPercent: 20,
                        thresholdLevelAnchor: 17,
                        thresholdLevelBonusPerDelta: 5,
                        thresholdCapMaxHpRatioPercent: 50,
                        soulFractureDurationTu: 60,
                        healMultiplierPercent: 50,
                        shieldGainMultiplierPercent: 50
                    ),
                },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                rangeValue: 5,
                areaPattern: "single",
                areaValue: 0,
                apCost: apCost,
                mpCost: mpCost,
                cooldownTu: cooldownTu
            ),
            maxLevel: 20,
            nonCoreMaxLevel: 20
        );

    private static BattleUnitState MakeUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: currentHp
        );
        unit.current_mp = 20;
        unit.current_stamina = 20;
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue("willpower", 0);
        unit.UnlockCombatResource("mp");
        unit.known_active_skill_ids.Add("test_ground_execute");
        unit.known_skill_level_map[new StringName("test_ground_execute")] = 17;
        return unit;
    }

    private static BattleCommand MakeGroundCommand(
        BattleUnitState source,
        Vector2I targetCoord,
        SkillDefinition skill
    )
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = source.unit_id,
            skill_id = skill.SkillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }
}

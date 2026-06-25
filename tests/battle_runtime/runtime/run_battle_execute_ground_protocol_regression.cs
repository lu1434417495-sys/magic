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
        using SkillDef skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);

        using BattleTestFixture fixture = CreateFixture("ground_execute_preview", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "地面技能携带 execute 时 preview 应直接拒绝。");
        _test.Eq(target.current_hp, 10, "地面 execute preview 不应伤害目标。");
        _test.False(target.HasStatusEffect("soul_fracture"), "地面 execute preview 不应附加状态。");

        BattleTestFixture.DisposeBattlePreview(preview);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private void TestGroundExecuteIssueDeniedBeforeCost()
    {
        using SkillDef skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);
        source.current_ap = 2;
        source.current_mp = 20;
        source.SetCooldownTyped(skill.skill_id, 0);

        using BattleTestFixture fixture = CreateFixture("ground_execute_issue", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(source.current_ap, 2, "地面 execute 被拒绝时不应消耗 AP。");
        _test.Eq(source.current_mp, 20, "地面 execute 被拒绝时不应消耗 MP。");
        _test.Eq(source.GetCooldownTyped(skill.skill_id), 0, "地面 execute 被拒绝时不应进入冷却。");

        GodotSharpCleanup.DisposeGodotObject(batch);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private void TestGroundExecuteDoesNotMutateDamageOrStatus()
    {
        using SkillDef skill = MakeGroundExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("ground_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("ground_target", "enemy", new Vector2I(1, 0), 100, 10);

        using BattleTestFixture fixture = CreateFixture("ground_execute_no_mutation", skill, source, target);
        BattleCommand command = MakeGroundCommand(source, target.coord, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(target.is_alive, "地面 execute 被拒绝时不应杀死目标。");
        _test.Eq(target.current_hp, 10, "地面 execute 被拒绝时不应改变目标 HP。");
        _test.False(target.HasStatusEffect("soul_fracture"), "地面 execute 被拒绝时不应附加 soul fracture。");

        GodotSharpCleanup.DisposeGodotObject(batch);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDef skill,
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
            new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDef MakeGroundExecuteSkill(int apCost, int mpCost, int cooldownTu)
    {
        var skill = new SkillDef
        {
            skill_id = "test_ground_execute",
            display_name = "测试地面律令",
            max_level = 20,
            non_core_max_level = 20,
            combat_profile = new CombatSkillDef
            {
                target_mode = "ground",
                target_team_filter = "enemy",
                range_value = 5,
                area_pattern = "single",
                area_value = 0,
                ap_cost = apCost,
                mp_cost = mpCost,
                cooldown_tu = cooldownTu,
            },
        };
        skill.combat_profile.effect_defs.Add(new CombatEffectDef
        {
            effect_type = "execute",
            effect_target_team_filter = "enemy",
            save_dc_mode = "static",
            save_dc = 999,
            save_ability = "willpower",
            save_tag = "execute",
            damage_tag = "negative_energy",
            threshold_max_hp_ratio_percent = 20,
            threshold_level_anchor = 17,
            threshold_level_bonus_per_delta = 5,
            threshold_cap_max_hp_ratio_percent = 50,
            soul_fracture_duration_tu = 60,
            heal_multiplier_percent = 50,
            shield_gain_multiplier_percent = 50,
        });
        return TestResourceOwnership.Own(skill, "battle_execute_ground_protocol.ground_execute_skill");
    }

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
        SkillDef skill
    )
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = source.unit_id,
            skill_id = skill.skill_id,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }
}

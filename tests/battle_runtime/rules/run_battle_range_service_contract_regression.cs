using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_range_service_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBaseRangeHandlesNullSkill();
            TestRangeUsesWeaponProjectionAndStatusLayer();
            TestGroundAreaThreatRangeIncludesOuterEdge();

            RequestTestExit(_test.Finish("Battle range service contract regression"));
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure($"Battle range service contract regression crashed: {exception}");
            RequestTestExit(_test.Finish("Battle range service contract regression", 1));
        }
    }

    private void TestBaseRangeHandlesNullSkill()
    {
        BattleUnitState unit = BuildUnit("range_null_guard_unit");
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill("range_null_guard_skill");

        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(unit, (SkillDefinition)null),
            0,
            "ResolveBaseSkillRange 直接收到 null skillDefinition 时应返回 0。"
        );
        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(unit, skillDefinition),
            0,
            "ResolveBaseSkillRange 直接收到缺 combat_profile 的 skillDefinition 时应返回 0。"
        );
    }

    private void TestRangeUsesWeaponProjectionAndStatusLayer()
    {
        SkillDefinition skillDefinition = BuildDirectDamageSkill(
            "range_layer_contract",
            99,
            new[] { new StringName("archer"), new StringName("bow") }
        );

        BattleUnitState archer = BuildUnit("range_layer_archer");
        archer.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.WeaponAttackRange), 8);
        archer.SetNaturalWeaponProjection(
            "test_bow",
            "physical_pierce",
            2,
            new GDictionary(),
            ""
        );

        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(archer, skillDefinition),
            2,
            "有效射程应读取 BattleUnitState.weapon_attack_range，而不是 attribute_snapshot 或技能 range_value。"
        );

        archer.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "archer_range_up",
                source_unit_id = archer.unit_id,
                power = 1,
                stacks = 1,
                duration = 60,
            }
        );

        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(archer, skillDefinition),
            3,
            "状态提供的射程修正应只在有效射程读取层叠加。"
        );
        _test.Eq(
            archer.weapon_attack_range,
            2,
            "状态射程修正不应写回 BattleUnitState.weapon_attack_range 基础投影。"
        );
    }

    private void TestGroundAreaThreatRangeIncludesOuterEdge()
    {
        SkillDefinition skill = BuildGroundSkill(
            "ground_outer_reach_contract",
            "narrow_cone",
            5,
            new Dictionary<int, IReadOnlyDictionary<string, object>>
            {
                [7] = new Dictionary<string, object> { ["area_value"] = 6 },
            }
        );
        BattleUnitState caster = BuildUnit("ground_outer_reach_caster");
        caster.known_active_skill_ids = new GStringNameArray { skill.SkillId };
        caster.known_skill_level_map[skill.SkillId] = 7;

        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(caster, skill),
            1,
            "合法施法锚点距离仍应保持配置射程。"
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillThreatRange(caster, skill),
            7,
            "AI 战术威胁距离应计入地面范围技能的外缘覆盖。"
        );

        AssertGroundRanges(
            caster,
            "ground_cone_outer_reach_contract",
            "cone",
            3,
            7,
            7,
            "标准 cone"
        );
        AssertGroundRanges(
            caster,
            "ground_radius_outer_reach_contract",
            "radius",
            2,
            5,
            5,
            "radius/square"
        );
        AssertGroundRanges(
            caster,
            "ground_diamond_outer_reach_contract",
            "diamond",
            2,
            3,
            3,
            "diamond"
        );
        AssertGroundRanges(
            caster,
            "ground_line_outer_reach_contract",
            "line",
            2,
            3,
            3,
            "line"
        );
        AssertGroundRanges(
            caster,
            "ground_cross_outer_reach_contract",
            "cross",
            2,
            3,
            3,
            "cross"
        );
        AssertGroundRanges(
            caster,
            "ground_front_arc_outer_reach_contract",
            "front_arc",
            2,
            3,
            3,
            "front_arc"
        );
    }

    private void AssertGroundRanges(
        BattleUnitState caster,
        StringName skillId,
        StringName areaPattern,
        int areaValue,
        int expectedThreatRange,
        int expectedDistanceContractRange,
        string label
    )
    {
        SkillDefinition skill = BuildGroundSkill(skillId, areaPattern, areaValue);
        _test.Eq(
            BattleRangeService.GetEffectiveSkillThreatRange(caster, skill),
            expectedThreatRange,
            $"{label} 威胁距离应按实际外缘覆盖计算。"
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillDistanceContractRange(caster, skill),
            expectedDistanceContractRange,
            $"{label} 距离合同应按 AI 站位外缘合同计算。"
        );
    }

    private static SkillDefinition BuildDirectDamageSkill(
        StringName skillId,
        int rangeValue,
        IReadOnlyList<StringName> tags = null
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 1
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: rangeValue
            ),
            tags: tags
        );
    }

    private static SkillDefinition BuildGroundSkill(
        StringName skillId,
        StringName areaPattern,
        int areaValue,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelOverrides = null
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 1
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                rangeValue: 1,
                areaPattern: areaPattern,
                areaValue: areaValue,
                levelOverrides: levelOverrides
            )
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            coord = Vector2I.Zero,
            current_hp = 20,
            current_ap = 2,
            is_alive = true,
        };
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant)
        || IsGodotCollectionType(type);

    private static bool IsGodotCollectionType(Type type)
    {
        if (type == null || type.IsGenericParameter)
        {
            return false;
        }
        if (type.Namespace == "Godot.Collections")
        {
            return type.Name.StartsWith("Dictionary", StringComparison.Ordinal)
                || type.Name.StartsWith("Array", StringComparison.Ordinal);
        }
        if (!type.IsGenericType)
        {
            return false;
        }
        foreach (Type genericArgument in type.GetGenericArguments())
        {
            if (IsGodotCollectionType(genericArgument))
            {
                return true;
            }
        }
        return false;
    }

}

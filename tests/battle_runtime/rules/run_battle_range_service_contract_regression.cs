using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_range_service_contract_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        try
        {
            int exitCode = Run();
            Quit(exitCode);
        }
        catch (Exception exception)
        {
            GD.PushError($"Battle range service contract regression crashed: {exception}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestServiceTypeIsPlainStaticCSharp();
        TestBaseRangeHandlesNullSkill();
        TestRangeUsesWeaponProjectionAndStatusLayer();
        TestGroundAreaThreatRangeIncludesOuterEdge();

        if (_failures.Count == 0)
        {
            GD.Print("Battle range service contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle range service contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceTypeIsPlainStaticCSharp()
    {
        Type serviceType = typeof(BattleRangeService);
        AssertTrue(
            serviceType.IsAbstract && serviceType.IsSealed,
            "BattleRangeService 应为 plain static C# helper。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleRangeService 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(serviceType, "GlobalClassAttribute"),
            "BattleRangeService 不应注册 GlobalClass。"
        );
        foreach (string snakeName in new[]
        {
            "get_weapon_attack_range",
            "unit_has_melee_weapon",
            "unit_matches_required_weapon_families",
            "get_effective_skill_range",
            "get_effective_skill_threat_range",
            "get_effective_skill_distance_contract_range",
            "requires_current_melee_weapon",
            "is_weapon_range_skill",
            "resolve_base_skill_range",
            "is_ground_jump_skill",
            "is_ground_relocation_skill",
            "effect_uses_weapon_physical_damage_tag",
            "effect_requires_weapon",
        })
        {
            AssertNull(
                serviceType.GetMethod(snakeName),
                $"BattleRangeService 不应保留 {snakeName} snake_case API。"
            );
        }
        AssertPublicApiDoesNotExposeGodotCollections(serviceType);
    }

    private void TestBaseRangeHandlesNullSkill()
    {
        BattleUnitState unit = BuildUnit("range_null_guard_unit");
        SkillDef skill = BuildDirectDamageSkill("range_null_guard_skill", 1);
        skill.combat_profile = null;

        AssertEq(
            BattleRangeService.ResolveBaseSkillRange(unit, null),
            0,
            "ResolveBaseSkillRange 直接收到 null skillDef 时应返回 0。"
        );
        AssertEq(
            BattleRangeService.ResolveBaseSkillRange(unit, skill),
            0,
            "ResolveBaseSkillRange 直接收到缺 combat_profile 的 skillDef 时应返回 0。"
        );
    }

    private void TestRangeUsesWeaponProjectionAndStatusLayer()
    {
        SkillDef skill = BuildDirectDamageSkill("range_layer_contract", 1);
        skill.tags = new GStringNameArray { "archer", "bow" };
        skill.combat_profile.range_value = 99;

        BattleUnitState archer = BuildUnit("range_layer_archer");
        archer.attribute_snapshot.set_value(AttributeService.WEAPON_ATTACK_RANGE_ID(), 8);
        archer.set_natural_weapon_projection(
            "test_bow",
            "physical_pierce",
            2,
            new GDictionary(),
            ""
        );

        AssertEq(
            BattleRangeService.GetEffectiveSkillRange(archer, skill),
            2,
            "有效射程应读取 BattleUnitState.weapon_attack_range，而不是 attribute_snapshot 或技能 range_value。"
        );

        archer.set_status_effect(
            new BattleStatusEffectState
            {
                status_id = "archer_range_up",
                source_unit_id = archer.unit_id,
                power = 1,
                stacks = 1,
                duration = 60,
            }
        );

        AssertEq(
            BattleRangeService.GetEffectiveSkillRange(archer, skill),
            3,
            "状态提供的射程修正应只在有效射程读取层叠加。"
        );
        AssertEq(
            archer.weapon_attack_range,
            2,
            "状态射程修正不应写回 BattleUnitState.weapon_attack_range 基础投影。"
        );
    }

    private void TestGroundAreaThreatRangeIncludesOuterEdge()
    {
        SkillDef skill = BuildGroundSkill(
            "ground_outer_reach_contract",
            "narrow_cone",
            5
        );
        skill.combat_profile.level_overrides = new GDictionary
        {
            [7] = new GDictionary { ["area_value"] = 6 },
        };
        BattleUnitState caster = BuildUnit("ground_outer_reach_caster");
        caster.known_active_skill_ids = new GStringNameArray { skill.skill_id };
        caster.known_skill_level_map[skill.skill_id] = 7;

        AssertEq(
            BattleRangeService.GetEffectiveSkillRange(caster, skill),
            1,
            "合法施法锚点距离仍应保持配置射程。"
        );
        AssertEq(
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
        SkillDef skill = BuildGroundSkill(skillId, areaPattern, areaValue);
        AssertEq(
            BattleRangeService.GetEffectiveSkillThreatRange(caster, skill),
            expectedThreatRange,
            $"{label} 威胁距离应按实际外缘覆盖计算。"
        );
        AssertEq(
            BattleRangeService.GetEffectiveSkillDistanceContractRange(caster, skill),
            expectedDistanceContractRange,
            $"{label} 距离合同应按 AI 站位外缘合同计算。"
        );
    }

    private static SkillDef BuildDirectDamageSkill(StringName skillId, int rangeValue)
    {
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            power = 1,
        };
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_mode = "unit",
            target_team_filter = "enemy",
            range_value = rangeValue,
            effect_defs = new Godot.Collections.Array<CombatEffectDef> { effect },
        };
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = combatProfile,
        };
    }

    private static SkillDef BuildGroundSkill(
        StringName skillId,
        StringName areaPattern,
        int areaValue
    )
    {
        SkillDef skill = BuildDirectDamageSkill(skillId, 1);
        skill.combat_profile.target_mode = "ground";
        skill.combat_profile.target_team_filter = "enemy";
        skill.combat_profile.area_pattern = areaPattern;
        skill.combat_profile.area_value = areaValue;
        return skill;
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

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type)
    {
        foreach (
            MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
            )
        )
        {
            AssertFalse(
                IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{type.Name}.{method.Name} 不应返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
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

    private static bool HasAttributeNamed(Type type, string attributeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} Expected {expected}, got {actual}.");
        }
    }
}

using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_level_description_template_regression : SceneTree
{
    private const string BattleRecoverySkillPath =
        "res://data/configs/skills/warrior_battle_recovery.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestBasicSubstitution();
        TestConditionalPresent();
        TestConditionalAbsent();
        TestConditionalNumericZeroAbsent();
        TestGuardFullTemplate();
        TestWhirlwindTemplate();
        TestTauntTemplate();
        TestEmptyConfig();
        TestLevelDescriptionRequiresTemplateConfig();
        TestLevelDescriptionHidesZeroProfileDefaultsInOptionalBlocks();
        TestBattleRecoveryDescriptionDerivesDisplayDice();
        TestLevelDescriptionDerivesTypedEffectFields();
        TestLevelDescriptionIgnoresLockedCastVariantEffects();

        if (_failures.Count == 0)
        {
            GD.Print("Level description template regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Level description template regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestBasicSubstitution()
    {
        string result = SkillLevelDescriptionFormatter.render_template(
            "距离{dist}，伤害{dmg}",
            new GDictionary { ["dist"] = "3", ["dmg"] = "5" }
        );
        AssertEq(result, "距离3，伤害5", "基本变量替换");
    }

    private void TestConditionalPresent()
    {
        string result = SkillLevelDescriptionFormatter.render_template(
            "A{{?x}}，B{x}{{/x}}C",
            new GDictionary { ["x"] = "1" }
        );
        AssertEq(result, "A，B1C", "条件块存在时应保留内容");
    }

    private void TestConditionalAbsent()
    {
        string result = SkillLevelDescriptionFormatter.render_template(
            "A{{?x}}，B{x}{{/x}}C",
            new GDictionary()
        );
        AssertEq(result, "AC", "条件块不存在时应整段删除");
    }

    private void TestConditionalNumericZeroAbsent()
    {
        AssertEq(
            SkillLevelDescriptionFormatter.render_template(
                "A{{?x}}，B{x}{{/x}}C",
                new GDictionary { ["x"] = 0 }
            ),
            "AC",
            "条件块数值为 0 时应整段删除"
        );
        AssertEq(
            SkillLevelDescriptionFormatter.render_template(
                "A{{?x}}，B{x}{{/x}}C",
                new GDictionary { ["x"] = 0.0 }
            ),
            "AC",
            "条件块数值为 0.0 时应整段删除"
        );
    }

    private void TestGuardFullTemplate()
    {
        const string template =
            "guarding 物理伤害减{guard_power}{{?slow_power}}，移动力-{slow_power}{{/slow_power}}，持续{duration}TU，体力消耗{stamina}，冷却{cooldown}TU";

        string level0 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary
            {
                ["guard_power"] = "1",
                ["slow_power"] = "1",
                ["duration"] = "40",
                ["stamina"] = "50",
                ["cooldown"] = "120",
            }
        );
        AssertEq(
            level0,
            "guarding 物理伤害减1，移动力-1，持续40TU，体力消耗50，冷却120TU",
            "格挡 0 级完整描述"
        );

        string level4 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary
            {
                ["guard_power"] = "2",
                ["duration"] = "60",
                ["stamina"] = "35",
                ["cooldown"] = "100",
            }
        );
        AssertEq(
            level4,
            "guarding 物理伤害减2，持续60TU，体力消耗35，冷却100TU",
            "格挡 4 级（无 slow）完整描述"
        );
    }

    private void TestWhirlwindTemplate()
    {
        const string template =
            "冲锋距离{distance}，攻击检定{attack}，冷却{cooldown}TU。每进一步对周边敌人触发武器攻击{{?stagger}}；被连续命中超过3次的敌人陷入踉跄{{/stagger}}";

        string level0 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary { ["distance"] = "3", ["attack"] = "-4", ["cooldown"] = "120" }
        );
        AssertEq(
            level0,
            "冲锋距离3，攻击检定-4，冷却120TU。每进一步对周边敌人触发武器攻击",
            "旋风斩 0 级（无 stagger）"
        );

        string level9 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary
            {
                ["distance"] = "6",
                ["attack"] = "+1",
                ["cooldown"] = "90",
                ["stagger"] = "true",
            }
        );
        AssertEq(
            level9,
            "冲锋距离6，攻击检定+1，冷却90TU。每进一步对周边敌人触发武器攻击；被连续命中超过3次的敌人陷入踉跄",
            "旋风斩 9 级（有 stagger）"
        );
    }

    private void TestTauntTemplate()
    {
        const string template =
            "{{?range_desc}}{range_desc}{{/range_desc}}，使其攻击非来源单位时处于劣势，持续{duration}TU";

        string level0 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary { ["range_desc"] = "对身边相邻一格的敌人嘲讽", ["duration"] = "30" }
        );
        AssertEq(
            level0,
            "对身边相邻一格的敌人嘲讽，使其攻击非来源单位时处于劣势，持续30TU",
            "挑衅 0 级"
        );

        string level5 = SkillLevelDescriptionFormatter.render_template(
            template,
            new GDictionary { ["range_desc"] = "对身边相邻一格横向3格内的敌人嘲讽", ["duration"] = "90" }
        );
        AssertEq(
            level5,
            "对身边相邻一格横向3格内的敌人嘲讽，使其攻击非来源单位时处于劣势，持续90TU",
            "挑衅 5 级"
        );
    }

    private void TestEmptyConfig()
    {
        string result = SkillLevelDescriptionFormatter.render_template(
            "A{{?x}}B{{/x}}C",
            new GDictionary()
        );
        AssertEq(result, "AC", "空配置应删除所有条件块");
    }

    private void TestLevelDescriptionRequiresTemplateConfig()
    {
        SkillDef skillDef = new()
        {
            level_description_template = "模板{val}",
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["val"] = "新" },
                ["1"] = new GDictionary { ["val"] = "新" },
            },
        };

        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 0, new GDictionary()),
            "模板新",
            "正式模板配置应渲染等级描述"
        );
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 1, new GDictionary()),
            "模板新",
            "正式模板配置应渲染对应等级描述"
        );
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 2, new GDictionary()),
            "",
            "缺少当前等级正式配置时应返回空"
        );

        SkillDef missingTemplate = new()
        {
            level_description_configs = new GDictionary { ["0"] = new GDictionary { ["val"] = "新" } },
        };
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(missingTemplate, 0, new GDictionary()),
            "",
            "缺少正式模板时应返回空"
        );

        SkillDef missingConfig = new() { level_description_template = "模板{val}" };
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(missingConfig, 0, new GDictionary()),
            "",
            "缺少正式等级配置时应返回空"
        );

        SkillDef wrongConfigType = new()
        {
            level_description_template = "模板{val}",
            level_description_configs = new GDictionary { ["0"] = "旧格式描述" },
        };
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(wrongConfigType, 0, new GDictionary()),
            "",
            "等级配置不是字典时应返回空"
        );
    }

    private void TestLevelDescriptionHidesZeroProfileDefaultsInOptionalBlocks()
    {
        SkillDef skillDef = new()
        {
            level_description_template =
                "基础{{?attack_roll_bonus}}，攻击检定{attack_roll_bonus}{{/attack_roll_bonus}}{{?aura_cost}}，消耗{aura_cost}斗气{{/aura_cost}}",
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["marker"] = "configured" },
                ["1"] = new GDictionary { ["marker"] = "configured" },
            },
            combat_profile = new CombatSkillDef
            {
                attack_roll_bonus = 0,
                aura_cost = 0,
                level_overrides = new GDictionary
                {
                    [1] = new GDictionary { ["attack_roll_bonus"] = 2, ["aura_cost"] = 1 },
                },
            },
        };

        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 0, new GDictionary()),
            "基础",
            "formatter 不应让 profile 默认 0 撑开 optional 条件块。"
        );
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 1, new GDictionary()),
            "基础，攻击检定2，消耗1斗气",
            "formatter 仍应显示非 0 profile override。"
        );
    }

    private void TestBattleRecoveryDescriptionDerivesDisplayDice()
    {
        SkillDef skillDef = GD.Load<SkillDef>(BattleRecoverySkillPath);
        if (skillDef == null)
        {
            _failures.Add("战斗回复技能资源应能加载。");
            return;
        }

        string lowStatDescription = SkillLevelDescriptionFormatter.build_level_description(
            skillDef,
            5,
            new GDictionary { ["con_mod"] = -3, ["will_mod"] = -3 }
        );
        AssertEq(
            lowStatDescription,
            "恢复体力10D4，并恢复生命2D4。冷却120TU。",
            "战斗回复描述 formatter 应正确渲染低属性展示骰面和 Lv5 治疗骰。"
        );
    }

    private void TestLevelDescriptionDerivesTypedEffectFields()
    {
        SkillDef skillDef = new()
        {
            level_description_template =
                "造成{dmg}伤害（{damage_save_text}），{shocked_save_text}（{shocked_duration_tu}TU，强度{shocked_power}）。",
            level_description_configs = new GDictionary { ["0"] = new GDictionary { ["dmg"] = "4D6" } },
            combat_profile = new CombatSkillDef(),
        };

        CombatEffectDef damageEffect = new()
        {
            effect_type = "damage",
            save_ability = "agility",
            save_dc_mode = "caster_spell",
            save_partial_on_success = true,
        };
        skillDef.combat_profile.effect_defs.Add(damageEffect);

        CombatEffectDef statusEffect = new()
        {
            effect_type = "status",
            status_id = "shocked",
            power = 1,
            duration_tu = 60,
            save_ability = "constitution",
            save_dc_mode = "caster_spell",
        };
        skillDef.combat_profile.effect_defs.Add(statusEffect);

        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 0, new GDictionary()),
            "造成4D6伤害（敏捷豁免成功时伤害减半），体质豁免失败时附加感电（60TU，强度1）。",
            "等级描述 formatter 应从 typed effect fields 派生豁免、状态名、持续时间和强度。"
        );
    }

    private void TestLevelDescriptionIgnoresLockedCastVariantEffects()
    {
        SkillDef skillDef = new()
        {
            level_description_template = "基础{base}{{?locked_param}}，高阶{locked_param}{{/locked_param}}",
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["base"] = "可用" },
                ["3"] = new GDictionary { ["base"] = "可用" },
            },
            combat_profile = new CombatSkillDef(),
        };

        CombatCastVariantDef option = new()
        {
            variant_id = "advanced",
            min_skill_level = 3,
        };
        CombatEffectDef optionEffect = new()
        {
            effect_type = "damage",
            @params = new GDictionary { ["locked_param"] = "未锁" },
        };
        option.effect_defs.Add(optionEffect);
        skillDef.combat_profile.cast_variants.Add(option);

        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 0, new GDictionary()),
            "基础可用",
            "低等级描述不应合并未解锁施法形态的 effect params。"
        );
        AssertEq(
            SkillLevelDescriptionFormatter.build_level_description(skillDef, 3, new GDictionary()),
            "基础可用，高阶未锁",
            "达到施法形态等级后应合并该形态的 effect params。"
        );
    }

    private void AssertEq(string actual, string expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}

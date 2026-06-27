using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_level_description_template_regression : SceneTree
{
    private const string BattleRecoverySkillPath =
        "res://data/configs/skills/warrior_battle_recovery.tres";

    private readonly TestHarness _test = new();

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

        Quit(_test.Finish("Level description template regression"));
    }

    private void TestBasicSubstitution()
    {
        string result = SkillLevelDescriptionFormatter.RenderTemplate(
            "距离{dist}，伤害{dmg}",
            new GDictionary { ["dist"] = "3", ["dmg"] = "5" }
        );
        _test.Eq(result, "距离3，伤害5", "基本变量替换");
    }

    private void TestConditionalPresent()
    {
        string result = SkillLevelDescriptionFormatter.RenderTemplate(
            "A{{?x}}，B{x}{{/x}}C",
            new GDictionary { ["x"] = "1" }
        );
        _test.Eq(result, "A，B1C", "条件块存在时应保留内容");
    }

    private void TestConditionalAbsent()
    {
        string result = SkillLevelDescriptionFormatter.RenderTemplate(
            "A{{?x}}，B{x}{{/x}}C",
            new GDictionary()
        );
        _test.Eq(result, "AC", "条件块不存在时应整段删除");
    }

    private void TestConditionalNumericZeroAbsent()
    {
        _test.Eq(
            SkillLevelDescriptionFormatter.RenderTemplate(
                "A{{?x}}，B{x}{{/x}}C",
                new GDictionary { ["x"] = 0 }
            ),
            "AC",
            "条件块数值为 0 时应整段删除"
        );
        _test.Eq(
            SkillLevelDescriptionFormatter.RenderTemplate(
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

        string level0 = SkillLevelDescriptionFormatter.RenderTemplate(
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
        _test.Eq(
            level0,
            "guarding 物理伤害减1，移动力-1，持续40TU，体力消耗50，冷却120TU",
            "格挡 0 级完整描述"
        );

        string level4 = SkillLevelDescriptionFormatter.RenderTemplate(
            template,
            new GDictionary
            {
                ["guard_power"] = "2",
                ["duration"] = "60",
                ["stamina"] = "35",
                ["cooldown"] = "100",
            }
        );
        _test.Eq(
            level4,
            "guarding 物理伤害减2，持续60TU，体力消耗35，冷却100TU",
            "格挡 4 级（无 slow）完整描述"
        );
    }

    private void TestWhirlwindTemplate()
    {
        const string template =
            "冲锋距离{distance}，攻击检定{attack}，冷却{cooldown}TU。每进一步对周边敌人触发武器攻击{{?stagger}}；被连续命中超过3次的敌人陷入踉跄{{/stagger}}";

        string level0 = SkillLevelDescriptionFormatter.RenderTemplate(
            template,
            new GDictionary { ["distance"] = "3", ["attack"] = "-4", ["cooldown"] = "120" }
        );
        _test.Eq(
            level0,
            "冲锋距离3，攻击检定-4，冷却120TU。每进一步对周边敌人触发武器攻击",
            "旋风斩 0 级（无 stagger）"
        );

        string level9 = SkillLevelDescriptionFormatter.RenderTemplate(
            template,
            new GDictionary
            {
                ["distance"] = "6",
                ["attack"] = "+1",
                ["cooldown"] = "90",
                ["stagger"] = "true",
            }
        );
        _test.Eq(
            level9,
            "冲锋距离6，攻击检定+1，冷却90TU。每进一步对周边敌人触发武器攻击；被连续命中超过3次的敌人陷入踉跄",
            "旋风斩 9 级（有 stagger）"
        );
    }

    private void TestTauntTemplate()
    {
        const string template =
            "{{?range_desc}}{range_desc}{{/range_desc}}，使其攻击非来源单位时处于劣势，持续{duration}TU";

        string level0 = SkillLevelDescriptionFormatter.RenderTemplate(
            template,
            new GDictionary { ["range_desc"] = "对身边相邻一格的敌人嘲讽", ["duration"] = "30" }
        );
        _test.Eq(
            level0,
            "对身边相邻一格的敌人嘲讽，使其攻击非来源单位时处于劣势，持续30TU",
            "挑衅 0 级"
        );

        string level5 = SkillLevelDescriptionFormatter.RenderTemplate(
            template,
            new GDictionary { ["range_desc"] = "对身边相邻一格横向3格内的敌人嘲讽", ["duration"] = "90" }
        );
        _test.Eq(
            level5,
            "对身边相邻一格横向3格内的敌人嘲讽，使其攻击非来源单位时处于劣势，持续90TU",
            "挑衅 5 级"
        );
    }

    private void TestEmptyConfig()
    {
        string result = SkillLevelDescriptionFormatter.RenderTemplate(
            "A{{?x}}B{{/x}}C",
            new GDictionary()
        );
        _test.Eq(result, "AC", "空配置应删除所有条件块");
    }

    private void TestLevelDescriptionRequiresTemplateConfig()
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            "level_description_fixture",
            levelDescriptionTemplate: "模板{val}",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
            {
                [0] = new Dictionary<string, Variant> { ["val"] = "新" },
                [1] = new Dictionary<string, Variant> { ["val"] = "新" },
            }
        );

        _test.Eq(
            BuildLevelDescription(skillDefinition, 0, new GDictionary()),
            "模板新",
            "正式模板配置应渲染等级描述"
        );
        _test.Eq(
            BuildLevelDescription(skillDefinition, 1, new GDictionary()),
            "模板新",
            "正式模板配置应渲染对应等级描述"
        );
        _test.Eq(
            BuildLevelDescription(skillDefinition, 2, new GDictionary()),
            "",
            "缺少当前等级正式配置时应返回空"
        );

        SkillDefinition missingTemplate = TestSkillDefinitionProjection.BuildSkill(
            "missing_template_fixture",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
            {
                [0] = new Dictionary<string, Variant> { ["val"] = "新" },
            }
        );
        _test.Eq(
            BuildLevelDescription(missingTemplate, 0, new GDictionary()),
            "",
            "缺少正式模板时应返回空"
        );

        SkillDefinition missingConfig = TestSkillDefinitionProjection.BuildSkill(
            "missing_config_fixture",
            levelDescriptionTemplate: "模板{val}"
        );
        _test.Eq(
            BuildLevelDescription(missingConfig, 0, new GDictionary()),
            "",
            "缺少正式等级配置时应返回空"
        );

        SkillDef wrongConfigType = new()
        {
            level_description_template = "模板{val}",
            level_description_configs = new GDictionary { ["0"] = "旧格式描述" },
        };
        _test.Eq(
            BuildLevelDescriptionFromResource(wrongConfigType, 0, new GDictionary()),
            "",
            "等级配置不是字典时应返回空"
        );
    }

    private void TestLevelDescriptionHidesZeroProfileDefaultsInOptionalBlocks()
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            "zero_optional_profile_fixture",
            levelDescriptionTemplate:
                "基础{{?attack_roll_bonus}}，攻击检定{attack_roll_bonus}{{/attack_roll_bonus}}{{?aura_cost}}，消耗{aura_cost}斗气{{/aura_cost}}",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
            {
                [0] = new Dictionary<string, Variant> { ["marker"] = "configured" },
                [1] = new Dictionary<string, Variant> { ["marker"] = "configured" },
            },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "zero_optional_profile_fixture",
                attackRollBonus: 0,
                auraCost: 0,
                levelOverrides: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
                {
                    [1] = new Dictionary<string, Variant>
                    {
                        ["attack_roll_bonus"] = 2,
                        ["aura_cost"] = 1,
                    },
                }
            )
        );

        _test.Eq(
            BuildLevelDescription(skillDefinition, 0, new GDictionary()),
            "基础",
            "formatter 不应让 profile 默认 0 撑开 optional 条件块。"
        );
        _test.Eq(
            BuildLevelDescription(skillDefinition, 1, new GDictionary()),
            "基础，攻击检定2，消耗1斗气",
            "formatter 仍应显示非 0 profile override。"
        );
    }

    private void TestBattleRecoveryDescriptionDerivesDisplayDice()
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.LoadSkillDefinition(
            BattleRecoverySkillPath,
            $"level_description_template:{BattleRecoverySkillPath}"
        );
        if (skillDefinition == null)
        {
            _test.Fail("战斗回复技能资源应能加载。");
            return;
        }

        string lowStatDescription = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skillDefinition,
            5,
            new GDictionary { ["con_mod"] = -3, ["will_mod"] = -3 }
        );
        _test.Eq(
            lowStatDescription,
            "恢复体力10D4，并恢复生命2D4。冷却120TU。",
            "战斗回复描述 formatter 应正确渲染低属性展示骰面和 Lv5 治疗骰。"
        );
    }

    private void TestLevelDescriptionDerivesTypedEffectFields()
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            "typed_effect_description_fixture",
            levelDescriptionTemplate:
                "造成{dmg}伤害（{damage_save_text}），{shocked_save_text}（{shocked_duration_tu}TU，强度{shocked_power}）。",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
            {
                [0] = new Dictionary<string, Variant> { ["dmg"] = "4D6" },
            },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "typed_effect_description_fixture",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "damage",
                        saveAbility: "agility",
                        saveDcMode: "caster_spell",
                        savePartialOnSuccess: true
                    ),
                    TestSkillDefinitionProjection.BuildEffect(
                        "status",
                        statusId: "shocked",
                        power: 1,
                        durationTu: 60,
                        saveAbility: "constitution",
                        saveDcMode: "caster_spell"
                    ),
                }
            )
        );

        _test.Eq(
            BuildLevelDescription(skillDefinition, 0, new GDictionary()),
            "造成4D6伤害（敏捷豁免成功时伤害减半），体质豁免失败时附加感电（60TU，强度1）。",
            "等级描述 formatter 应从 typed effect fields 派生豁免、状态名、持续时间和强度。"
        );
    }

    private void TestLevelDescriptionIgnoresLockedCastVariantEffects()
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            "locked_cast_variant_description_fixture",
            levelDescriptionTemplate: "基础{base}{{?locked_param}}，高阶{locked_param}{{/locked_param}}",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>
            {
                [0] = new Dictionary<string, Variant> { ["base"] = "可用" },
                [3] = new Dictionary<string, Variant> { ["base"] = "可用" },
            },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "locked_cast_variant_description_fixture",
                castVariants: new[]
                {
                    TestSkillDefinitionProjection.BuildCastVariant(
                        "advanced",
                        3,
                        new[]
                        {
                            TestSkillDefinitionProjection.BuildEffect(
                                "damage",
                                parameters: new Dictionary<string, Variant>
                                {
                                    ["locked_param"] = "未锁",
                                }
                            ),
                        }
                    ),
                }
            )
        );

        _test.Eq(
            BuildLevelDescription(skillDefinition, 0, new GDictionary()),
            "基础可用",
            "低等级描述不应合并未解锁施法形态的 effect params。"
        );
        _test.Eq(
            BuildLevelDescription(skillDefinition, 3, new GDictionary()),
            "基础可用，高阶未锁",
            "达到施法形态等级后应合并该形态的 effect params。"
        );
    }

    private static string BuildLevelDescription(
        SkillDefinition skillDefinition,
        int level,
        GDictionary runtimeContext
    ) =>
        SkillLevelDescriptionFormatter.BuildLevelDescription(
            skillDefinition,
            level,
            runtimeContext
        );

    private static string BuildLevelDescriptionFromResource(
        SkillDef skillDef,
        int level,
        GDictionary runtimeContext
    ) =>
        SkillLevelDescriptionFormatter.BuildLevelDescription(
            SkillDefinition.FromResource(skillDef),
            level,
            runtimeContext
        );


}

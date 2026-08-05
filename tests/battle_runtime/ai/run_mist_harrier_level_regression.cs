using Godot;

public partial class run_mist_harrier_level_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        bool foundTemplate = snapshot.EnemyTemplates.TryGetValue(
            "mist_harrier",
            out EnemyTemplateDefinition template
        );

        _test.True(foundTemplate, "正式敌人内容应包含 mist_harrier。");
        if (foundTemplate && template != null)
        {
            _test.Eq(template.CreatureLevel, 5, "雾沼猎压者必须是 5 级怪物。");
            _test.Eq(template.HitDieSides, 8, "雾沼猎压者应继续使用 d8 生命骰。");
            AssertBaseAttribute(template, "strength", 10, "力量");
            AssertBaseAttribute(template, "agility", 17, "敏捷");
            AssertBaseAttribute(template, "constitution", 14, "体质");
            AssertBaseAttribute(template, "perception", 17, "感知");
            AssertBaseAttribute(template, "intelligence", 6, "智力");
            AssertBaseAttribute(template, "willpower", 11, "意志");
            _test.Eq(template.SkillIds.Count, 5, "雾沼猎压者必须显式配置5个技能。");
            AssertSkillDeclared(template, "archer_suppressive_fire", "压制射击");
            AssertSkillDeclared(template, "archer_pinning_shot", "钉射");
            AssertSkillDeclared(template, "archer_harrier_mark", "猎印追缉");
            AssertSkillDeclared(template, "archer_aimed_shot", "精准射击");
            AssertSkillDeclared(template, "basic_attack", "基础攻击");
            _test.Eq(
                template.SkillLevelMap.Count,
                0,
                "雾沼猎压者模板不应固化技能等级；等级必须在单位生成时随机。"
            );
            _test.False(
                template.AttributeOverrides.ContainsKey(new StringName("hp_max")),
                "雾沼猎压者不应以固定 hp_max 绕过等级生命公式。"
            );
            _test.Eq(
                template.DerivedHpMax,
                46,
                "5级、d8、体质14的雾沼猎压者应派生为46点生命。"
            );
            _test.Eq(
                template.DerivedAttackBonus,
                3,
                "远程攻击加值应由感知17派生为+3。"
            );
        }

        RequestTestExit(_test.Finish("Mist harrier level regression"));
    }

    private void AssertBaseAttribute(
        EnemyTemplateDefinition template,
        StringName attributeId,
        int expectedValue,
        string displayName
    )
    {
        bool found = template.BaseAttributeOverrides.TryGetValue(attributeId, out int actualValue);
        _test.True(found, $"雾沼猎压者必须显式配置{displayName}。");
        if (found)
            _test.Eq(actualValue, expectedValue, $"雾沼猎压者的{displayName}应为{expectedValue}。");
    }

    private void AssertSkillDeclared(
        EnemyTemplateDefinition template,
        StringName skillId,
        string displayName
    )
    {
        bool found = false;
        foreach (StringName declaredSkillId in template.SkillIds)
        {
            if (declaredSkillId != skillId)
                continue;
            found = true;
            break;
        }
        _test.True(found, $"雾沼猎压者必须配置{displayName}（{skillId}）。");
    }

}

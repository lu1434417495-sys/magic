using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_description_consistency_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestChainLightningDescriptionMatchesSaveEnabledEffects();

        if (_failures.Count == 0)
        {
            GD.Print("Skill description consistency regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Skill description consistency regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestChainLightningDescriptionMatchesSaveEnabledEffects()
    {
        ProgressionContentRegistry registry = new();
        SkillDef chainLightning = GetSkillDef(registry.get_skill_defs(), "mage_chain_lightning");
        AssertTrue(chainLightning != null, "链式闪击技能应存在。");
        if (chainLightning == null)
        {
            return;
        }

        string level0Description = SkillLevelDescriptionFormatter.build_level_description(
            chainLightning,
            0,
            new GDictionary()
        );
        AssertEq(
            level0Description,
            "射程5，造成4D6雷电伤害（敏捷豁免成功时伤害减半），并使目标进行体质豁免；失败则附加感电（60TU，强度1）。向范围内全部目标弹射全额伤害，不分敌我。湿地扩大弹射范围。消耗1AP/120法力，冷却60TU",
            "链式闪击 0 级描述应同时覆盖伤害敏捷豁免与感电体质豁免。"
        );

        string level7Description = SkillLevelDescriptionFormatter.build_level_description(
            chainLightning,
            7,
            new GDictionary()
        );
        AssertEq(
            level7Description,
            "射程5，造成8D6雷电伤害（敏捷豁免成功时伤害减半），并使目标进行体质豁免；失败则附加感电（60TU，强度1）。向范围内全部目标弹射全额伤害，不分敌我。湿地扩大弹射范围。消耗1AP/120法力，冷却60TU",
            "链式闪击 7 级描述应沿用 typed effect 字段并只替换等级伤害。"
        );
        AssertFalse(level0Description.Contains("shocked"), "链式闪击正式描述不应暴露英文状态 id。");
        AssertFalse(level7Description.Contains("shocked"), "链式闪击高等级正式描述不应暴露英文状态 id。");
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
        {
            return null;
        }
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool value, string message)
    {
        if (value)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(string actual, string expected, string message)
    {
        if (actual == expected)
        {
            return;
        }
        _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}

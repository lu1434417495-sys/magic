using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_target_team_rules_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRuleTypeIsPlainStaticCSharp();
        TestCanonicalFiltersMatchRelativeToSource();
        TestAliasAndUnknownFiltersFailClosed();
        TestEffectFilterEmptyInheritsSkillFilter();
        TestMadnessVariantOnlyRelaxesCanonicalTeamFilters();

        if (_failures.Count == 0)
        {
            GD.Print("Battle target team rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle target team rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRuleTypeIsPlainStaticCSharp()
    {
        Type ruleType = typeof(BattleTargetTeamRules);
        AssertTrue(ruleType.IsAbstract && ruleType.IsSealed, "目标队伍规则应是 plain static C# class。");
        AssertFalse(typeof(RefCounted).IsAssignableFrom(ruleType), "目标队伍规则不应继承 RefCounted。");
        AssertFalse(HasAttributeNamed(ruleType, "GlobalClassAttribute"), "目标队伍规则不应注册 GlobalClass。");
    }

    private void TestCanonicalFiltersMatchRelativeToSource()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");

        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "enemy"),
            "enemy 应命中不同阵营单位。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "enemy"),
            "enemy 不应命中同阵营单位。"
        );
        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "ally"),
            "ally 应命中同阵营单位。"
        );
        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, source, "self"),
            "self 应只命中来源单位。"
        );
        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "any"),
            "any 应命中敌方单位。"
        );
        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "any"),
            "any 应命中友方单位。"
        );
    }

    private void TestAliasAndUnknownFiltersFailClosed()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");

        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "hostile"),
            "hostile 是 faction_id，不应作为 target filter 命中。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "friendly"),
            "friendly 不应作为 target filter 命中。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "all"),
            "all 别名不应作为 target filter 命中。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "enmey"),
            "未知 target filter 应 fail closed。"
        );
    }

    private void TestEffectFilterEmptyInheritsSkillFilter()
    {
        var skillDef = new SkillDef
        {
            skill_id = "inherit_filter_skill",
            combat_profile = new CombatSkillDef
            {
                skill_id = "inherit_filter_skill",
                target_team_filter = "enemy",
            },
        };
        var inheritedEffect = new CombatEffectDef { effect_target_team_filter = "" };
        var allyEffect = new CombatEffectDef { effect_target_team_filter = "ally" };

        AssertStringNameEq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(skillDef, inheritedEffect),
            "enemy",
            "空 effect_target_team_filter 应继承 skill filter。"
        );
        AssertStringNameEq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(skillDef, allyEffect),
            "ally",
            "非空 effect_target_team_filter 应覆盖 skill filter。"
        );
        AssertStringNameEq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(null, inheritedEffect),
            "",
            "缺少 skill filter 时空 effect filter 不应隐藏回退成 any。"
        );
    }

    private void TestMadnessVariantOnlyRelaxesCanonicalTeamFilters()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");
        var options = new BattleTargetTeamRules.TargetFilterOptions(MadnessTargetAnyTeam: true);

        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "enemy", options),
            "madness_target_any_team 应允许 enemy/ally 队伍过滤命中任意非自身单位。"
        );
        AssertTrue(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "ally", options),
            "madness_target_any_team 应允许 ally 队伍过滤命中敌方单位。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, source, "enemy", options),
            "madness_target_any_team 不应允许命中自己。"
        );
        AssertFalse(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "hostile", options),
            "madness_target_any_team 不应复活 hostile 这类别名。"
        );
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            faction_id = factionId,
            is_alive = true,
        };
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
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
        AssertTrue(!condition, message);
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}

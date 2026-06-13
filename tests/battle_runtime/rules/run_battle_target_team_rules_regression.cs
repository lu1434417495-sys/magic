using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_target_team_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCanonicalFiltersMatchRelativeToSource();
        TestAliasAndUnknownFiltersFailClosed();
        TestEffectFilterEmptyInheritsSkillFilter();
        TestMadnessVariantOnlyRelaxesCanonicalTeamFilters();

        Quit(_test.Finish("Battle target team rules regression"));
    }

    private void TestCanonicalFiltersMatchRelativeToSource()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");

        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "enemy"),
            "enemy 应命中不同阵营单位。"
        );
        _test.False(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "enemy"),
            "enemy 不应命中同阵营单位。"
        );
        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "ally"),
            "ally 应命中同阵营单位。"
        );
        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, source, "self"),
            "self 应只命中来源单位。"
        );
        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "any"),
            "any 应命中敌方单位。"
        );
        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "any"),
            "any 应命中友方单位。"
        );
    }

    private void TestAliasAndUnknownFiltersFailClosed()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");

        _test.False(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "hostile"),
            "hostile 是 faction_id，不应作为 target filter 命中。"
        );
        _test.False(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "friendly"),
            "friendly 不应作为 target filter 命中。"
        );
        _test.False(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "all"),
            "all 别名不应作为 target filter 命中。"
        );
        _test.False(
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

        _test.Eq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(skillDef, inheritedEffect).ToString(),
            "enemy",
            "空 effect_target_team_filter 应继承 skill filter。"
        );
        _test.Eq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(skillDef, allyEffect),
            (StringName)"ally",
            "非空 effect_target_team_filter 应覆盖 skill filter。"
        );
        _test.Eq(
            BattleTargetTeamRules.ResolveEffectTargetFilter(null, inheritedEffect),
            (StringName)"",
            "缺少 skill filter 时空 effect filter 不应隐藏回退成 any。"
        );
    }

    private void TestMadnessVariantOnlyRelaxesCanonicalTeamFilters()
    {
        BattleUnitState source = MakeUnit("source", "player");
        BattleUnitState ally = MakeUnit("ally", "player");
        BattleUnitState enemy = MakeUnit("enemy", "hostile");
        var options = new BattleTargetTeamRules.TargetFilterOptions(MadnessTargetAnyTeam: true);

        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, ally, "enemy", options),
            "madness_target_any_team 应允许 enemy/ally 队伍过滤命中任意非自身单位。"
        );
        _test.True(
            BattleTargetTeamRules.IsUnitValidForFilter(source, enemy, "ally", options),
            "madness_target_any_team 应允许 ally 队伍过滤命中敌方单位。"
        );
        _test.False(
            BattleTargetTeamRules.IsUnitValidForFilter(source, source, "enemy", options),
            "madness_target_any_team 不应允许命中自己。"
        );
        _test.False(
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
}

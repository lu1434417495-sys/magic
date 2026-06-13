using Godot;

public partial class run_battle_contribution_event_builder_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDictionaryPayloadParsesTypedEvent();
        TestRelationFallsBackFromFactionIds();
        TestTypedEventProjectsToDictionary();
        Quit(_test.Finish("Battle contribution event builder regression"));
    }

    private void TestDictionaryPayloadParsesTypedEvent()
    {
        BattleContributionEvent contributionEvent = BattleContributionEventBuilder.FromDictionary(
            new Godot.Collections.Dictionary
            {
                ["source_unit_id"] = "caster",
                ["source_member_id"] = "member_caster",
                ["source_faction_id"] = "player",
                ["target_unit_id"] = "target",
                ["target_faction_id"] = "enemy",
                ["skill_id"] = "mage_fireball",
                ["relation"] = "ally",
                ["origin_kind"] = "terrain",
                ["hp_damage_applied"] = -5,
                ["hp_healing_applied"] = 12,
                ["caused_defeat"] = true,
            }
        );

        _test.Eq(contributionEvent.SourceUnitId, new StringName("caster"), "应解析 source unit id。");
        _test.Eq(contributionEvent.SourceMemberId, new StringName("member_caster"), "应解析 member id。");
        _test.Eq(contributionEvent.Relation, BattleContributionRelation.Ally, "显式 relation 应优先。");
        _test.Eq(
            contributionEvent.OriginKind,
            BattleContributionOriginKind.Terrain,
            "origin kind 应解析 terrain。"
        );
        _test.Eq(contributionEvent.HpDamageApplied, 0, "负数伤害应 clamp 到 0。");
        _test.Eq(contributionEvent.HpHealingApplied, 12, "治疗值应保留。");
        _test.True(contributionEvent.CausedDefeat, "击倒标记应保留。");
    }

    private void TestRelationFallsBackFromFactionIds()
    {
        BattleContributionEvent contributionEvent = BattleContributionEventBuilder.FromDictionary(
            new Godot.Collections.Dictionary
            {
                ["source_unit_id"] = "caster",
                ["source_faction_id"] = "player",
                ["target_unit_id"] = "target",
                ["target_faction_id"] = "enemy",
            }
        );

        _test.Eq(
            contributionEvent.Relation,
            BattleContributionRelation.Enemy,
            "relation 缺失时应按阵营回退。"
        );
    }

    private void TestTypedEventProjectsToDictionary()
    {
        var contributionEvent = new BattleContributionEvent
        {
            SourceUnitId = "caster",
            SourceMemberId = "member_caster",
            SourceFactionId = "player",
            TargetUnitId = "target",
            TargetFactionId = "enemy",
            SkillId = "mage_fireball",
            Relation = BattleContributionRelation.Enemy,
            OriginKind = BattleContributionOriginKind.Skill,
            HpDamageApplied = 17,
            HpHealingApplied = 0,
            CausedDefeat = true,
        };

        Godot.Collections.Dictionary payload = contributionEvent.ToDictionary();

        _test.Eq(
            payload["source_unit_id"].AsStringName(),
            new StringName("caster"),
            "投影应保留 source unit id。"
        );
        _test.Eq(
            payload["relation"].AsStringName(),
            new StringName("enemy"),
            "投影应使用 relation 字符串。"
        );
        _test.Eq(
            payload["origin_kind"].AsStringName(),
            new StringName("skill"),
            "投影应使用 origin kind 字符串。"
        );
        _test.Eq(payload["hp_damage_applied"].AsInt32(), 17, "投影应保留伤害值。");
        _test.True(payload["caused_defeat"].AsBool(), "投影应保留击倒标记。");
    }

}

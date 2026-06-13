using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_luck_getter_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestUnitBaseAttributesLuckGettersCoverBoundaries();
        TestPartyMemberStateLuckGettersDelegateAndStayNullSafe();
        TestFromDictMissingProgressionResourceSchemaIsRejected();
        Quit(_test.Finish("Luck getter regression"));
    }

    private void TestUnitBaseAttributesLuckGettersCoverBoundaries()
    {
        AssertLuckCase("默认值", BuildAttributes(), 0, 0, 0, 0, 0);
        AssertLuckCase("低端软封顶", BuildAttributes(-9, 0), -9, 0, -6, 0, -6);
        AssertLuckCase("faith 奇数向下取整", BuildAttributes(0, 1), 0, 1, 1, 0, 1);
        AssertLuckCase("高端软封顶", BuildAttributes(2, 5), 2, 5, 7, 4, 5);
        AssertLuckCase("战斗幸运分数上限", BuildAttributes(4, 4), 4, 4, 7, 4, 5);
        AssertLuckCase("负 faith 不参与战斗幸运加成", BuildAttributes(2, -3), 2, -3, -1, 2, -1);
    }

    private void TestPartyMemberStateLuckGettersDelegateAndStayNullSafe()
    {
        var memberState = new PartyMemberState();
        memberState.progression.unit_base_attributes = BuildAttributes(2, 5);
        AssertMemberLuckCase("PartyMemberState 读取封装", memberState, 2, 5, 7, 4, 5);

        memberState.progression = null;
        AssertMemberLuckCase("PartyMemberState 缺少 progression 时回退默认值", memberState, 0, 0, 0, 0, 0);
    }

    private void TestFromDictMissingProgressionResourceSchemaIsRejected()
    {
        UnitBaseAttributes attributes = UnitBaseAttributes.FromDictionary(
            new GDictionary
            {
                ["strength"] = 3,
                ["agility"] = 2,
                ["constitution"] = 3,
                ["perception"] = 2,
                ["intelligence"] = 1,
                ["willpower"] = 1,
                ["custom_stats"] = new GDictionary(),
            }
        );
        AssertLuckCase("UnitBaseAttributes.from_dict custom_stats 缺少 luck 键", attributes, 0, 0, 0, 0, 0);

        PartyMemberState memberState = PartyMemberState.FromDictionary(
            BuildMemberPayloadWithoutUnlockedCombatResourceIds()
        );
        _test.True(memberState == null, "缺少 unlocked_combat_resource_ids 的 UnitProgress shape 应直接拒绝。");
    }

    private static UnitBaseAttributes BuildAttributes(int hiddenLuckAtBirth = 0, int faithLuckBonus = 0)
    {
        var attributes = new UnitBaseAttributes();
        attributes.custom_stats["hidden_luck_at_birth"] = hiddenLuckAtBirth;
        attributes.custom_stats["faith_luck_bonus"] = faithLuckBonus;
        return attributes;
    }

    private void AssertLuckCase(
        string label,
        UnitBaseAttributes attributes,
        int expectedHiddenLuck,
        int expectedFaithLuck,
        int expectedEffectiveLuck,
        int expectedCombatLuckScore,
        int expectedDropLuck
    )
    {
        _test.True(attributes != null, $"{label}：UnitBaseAttributes 不应为 null。");
        if (attributes == null)
            return;
        _test.Eq(attributes.GetHiddenLuckAtBirth(), expectedHiddenLuck, $"{label}：hidden_luck_at_birth 读取结果错误。");
        _test.Eq(attributes.GetFaithLuckBonus(), expectedFaithLuck, $"{label}：faith_luck_bonus 读取结果错误。");
        _test.Eq(attributes.GetEffectiveLuck(), expectedEffectiveLuck, $"{label}：effective_luck 计算结果错误。");
        _test.Eq(attributes.GetCombatLuckScore(), expectedCombatLuckScore, $"{label}：combat_luck_score 计算结果错误。");
        _test.Eq(attributes.GetDropLuck(), expectedDropLuck, $"{label}：drop_luck 计算结果错误。");
    }

    private void AssertMemberLuckCase(
        string label,
        PartyMemberState memberState,
        int expectedHiddenLuck,
        int expectedFaithLuck,
        int expectedEffectiveLuck,
        int expectedCombatLuckScore,
        int expectedDropLuck
    )
    {
        _test.True(memberState != null, $"{label}：PartyMemberState 不应为 null。");
        if (memberState == null)
            return;
        _test.Eq(memberState.GetHiddenLuckAtBirth(), expectedHiddenLuck, $"{label}：hidden_luck_at_birth 读取结果错误。");
        _test.Eq(memberState.GetFaithLuckBonus(), expectedFaithLuck, $"{label}：faith_luck_bonus 读取结果错误。");
        _test.Eq(memberState.GetEffectiveLuck(), expectedEffectiveLuck, $"{label}：effective_luck 计算结果错误。");
        _test.Eq(memberState.GetCombatLuckScore(), expectedCombatLuckScore, $"{label}：combat_luck_score 计算结果错误。");
        _test.Eq(memberState.GetDropLuck(), expectedDropLuck, $"{label}：drop_luck 计算结果错误。");
    }

    private static GDictionary BuildMemberPayloadWithoutUnlockedCombatResourceIds() =>
        new()
        {
            ["member_id"] = "hero",
            ["display_name"] = "Hero",
            ["faction_id"] = "player",
            ["portrait_id"] = "",
            ["progression"] = new GDictionary
            {
                ["version"] = 1,
                ["unit_id"] = "hero",
                ["display_name"] = "Hero",
                ["character_level"] = 1,
                ["unit_base_attributes"] = new GDictionary
                {
                    ["strength"] = 3,
                    ["agility"] = 2,
                    ["constitution"] = 3,
                    ["perception"] = 2,
                    ["intelligence"] = 1,
                    ["willpower"] = 1,
                    ["custom_stats"] = new GDictionary(),
                },
                ["reputation_state"] = new GDictionary
                {
                    ["morality"] = 0,
                    ["custom_states"] = new GDictionary(),
                },
                ["skills"] = new GDictionary(),
                ["professions"] = new GDictionary(),
                ["known_knowledge_ids"] = new GArray(),
                ["active_core_skill_ids"] = new GArray(),
                ["attribute_growth_progress"] = new GDictionary(),
                ["achievement_progress"] = new GDictionary(),
                ["pending_profession_choices"] = new GArray(),
                ["blocked_relearn_skill_ids"] = new GArray(),
                ["merged_skill_source_map"] = new GDictionary(),
            },
            ["equipment_state"] = new GDictionary { ["equipped_slots"] = new GDictionary() },
            ["control_mode"] = "manual",
            ["current_hp"] = 18,
            ["current_mp"] = 6,
            ["is_dead"] = false,
            ["race_id"] = "human",
            ["subrace_id"] = "common_human",
            ["age_years"] = 24,
            ["birth_at_world_step"] = 0,
            ["age_profile_id"] = "human_age_profile",
            ["natural_age_stage_id"] = "adult",
            ["effective_age_stage_id"] = "adult",
            ["effective_age_stage_source_type"] = "",
            ["effective_age_stage_source_id"] = "",
            ["body_size"] = 1,
            ["body_size_category"] = "medium",
            ["versatility_pick"] = "",
            ["active_stage_advancement_modifier_ids"] = new GArray(),
            ["bloodline_id"] = "",
            ["bloodline_stage_id"] = "",
            ["ascension_id"] = "",
            ["ascension_stage_id"] = "",
            ["ascension_started_at_world_step"] = -1,
            ["original_race_id_before_ascension"] = "",
            ["biological_age_years"] = 24,
            ["astral_memory_years"] = 0,
        };
}


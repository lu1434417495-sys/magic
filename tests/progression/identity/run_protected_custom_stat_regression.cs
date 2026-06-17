using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_protected_custom_stat_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestNonWhitelistedSourcesCannotWriteHiddenLuckAtBirth();
        TestPendingRewardFlowRejectsProtectedHiddenLuckWrites();
        TestCharacterCreationAndExplicitStoryScriptsCanWriteHiddenLuckAtBirth();
        TestUnprotectedCustomStatsRemainWritable();
        Quit(_test.Finish("Protected custom stat regression"));
    }

    private void TestNonWhitelistedSourcesCannotWriteHiddenLuckAtBirth()
    {
        (string Label, StringName SourceType, StringName SourceId)[] cases =
        {
            ("成就奖励", "achievement", "battle_won_first"),
            ("普通 rank 奖励", "profession_rank_reward", "warrior_rank_2"),
            ("道具效果", "item_effect", "lucky_incense"),
        };

        foreach ((string label, StringName sourceType, StringName sourceId) in cases)
        {
            AttributeService service = BuildAttributeService(2);
            bool applied = service.ApplyPermanentAttributeChange(
                "hidden_luck_at_birth",
                3,
                new GDictionary
                {
                    ["source_type"] = sourceType,
                    ["source_id"] = sourceId,
                }
            );
            _test.False(applied, $"{label} 不应能写入 hidden_luck_at_birth。");
            _test.Eq(service.GetBaseValue("hidden_luck_at_birth"), 2, $"{label} 被拒绝后不应改写 hidden_luck_at_birth。");
        }
    }

    private void TestCharacterCreationAndExplicitStoryScriptsCanWriteHiddenLuckAtBirth()
    {
        AttributeService creationService = BuildAttributeService(1);
        bool creationApplied = creationService.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new GDictionary
            {
                ["source_type"] = new StringName("character_creation"),
                ["source_id"] = new StringName("birth_roll"),
            }
        );
        _test.True(creationApplied, "CharacterCreationService 来源应能写入 hidden_luck_at_birth。");
        _test.Eq(creationService.GetBaseValue("hidden_luck_at_birth"), 3, "CharacterCreationService 来源应真正累计 hidden_luck_at_birth。");

        AttributeService unmarkedStoryService = BuildAttributeService(1);
        bool unmarkedStoryApplied = unmarkedStoryService.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new GDictionary
            {
                ["source_type"] = new StringName("story_script"),
                ["source_id"] = new StringName("chapter_intro"),
            }
        );
        _test.False(unmarkedStoryApplied, "未显式标记的剧情脚本不应写入 hidden_luck_at_birth。");
        _test.Eq(unmarkedStoryService.GetBaseValue("hidden_luck_at_birth"), 1, "未显式标记的剧情脚本被拒绝后不应改写 hidden_luck_at_birth。");

        AttributeService markedStoryService = BuildAttributeService(1);
        bool markedStoryApplied = markedStoryService.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new GDictionary
            {
                ["source_type"] = new StringName("story_script"),
                ["source_id"] = new StringName("chapter_intro"),
                ["allow_protected_custom_stat_write"] = true,
            }
        );
        _test.True(markedStoryApplied, "显式标记的剧情脚本应能写入 hidden_luck_at_birth。");
        _test.Eq(markedStoryService.GetBaseValue("hidden_luck_at_birth"), 3, "显式标记的剧情脚本应真正累计 hidden_luck_at_birth。");
    }

    private void TestPendingRewardFlowRejectsProtectedHiddenLuckWrites()
    {
        var partyState = new PartyState();
        var memberState = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            progression = new UnitProgress
            {
                unit_id = "hero",
                display_name = "Hero",
            },
        };
        memberState.progression.unit_base_attributes.SetAttributeValue("hidden_luck_at_birth", 2);
        partyState.SetMemberState(memberState);

        var manager = new CharacterManagementModule();
        manager.setup(partyState, new GDictionary(), new GDictionary(), new GDictionary());

        PendingCharacterReward reward = manager.BuildPendingCharacterReward(
            "hero",
            "protected_hidden_luck_reward",
            "achievement",
            "battle_won_first",
            "首战成就",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.AttributeDelta,
                    target_id = "hidden_luck_at_birth",
                    amount = 3,
                    reason_text = "测试保护写入",
                },
            },
            "成就奖励"
        );
        _test.True(reward != null, "测试前置：应能构造 attribute_delta 奖励。");
        if (reward == null)
            return;

        CharacterProgressionDelta delta = manager.ApplyPendingCharacterReward(reward);
        _test.Eq(delta.AttributeChangesTyped.Count, 0, "受保护 custom stat 被拒绝时不应记录 attribute delta。");
        _test.Eq(memberState.progression.unit_base_attributes.GetAttributeValue("hidden_luck_at_birth"), 2, "受保护 custom stat 通过成就奖励链路写入时应保持原值。");
    }

    private void TestUnprotectedCustomStatsRemainWritable()
    {
        AttributeService service = BuildAttributeService(1);
        bool applied = service.ApplyPermanentAttributeChange(
            "storage_space",
            2,
            new GDictionary
            {
                ["source_type"] = new StringName("achievement"),
                ["source_id"] = new StringName("pack_master"),
            }
        );
        _test.True(applied, "未受保护的 custom stat 仍应允许通过正式写入点更新。");
        _test.Eq(service.GetBaseValue("storage_space"), 3, "未受保护的 custom stat 应正常累计。");
    }

    private static AttributeService BuildAttributeService(int hiddenLuckAtBirth, int storageSpace = 1)
    {
        var progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        progression.unit_base_attributes.SetAttributeValue("hidden_luck_at_birth", hiddenLuckAtBirth);
        progression.unit_base_attributes.SetAttributeValue("storage_space", storageSpace);

        var service = new AttributeService();
        service.Setup(progression);
        return service;
    }
}

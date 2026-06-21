using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_protected_custom_stat_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestAttributeServiceUsesTypedPermanentChangeSource();
        TestNonWhitelistedSourcesCannotWriteHiddenLuckAtBirth();
        TestPendingRewardFlowRejectsProtectedHiddenLuckWrites();
        TestCharacterCreationAndExplicitStoryScriptsCanWriteHiddenLuckAtBirth();
        TestUnprotectedCustomStatsRemainWritable();
        Quit(_test.Finish("Protected custom stat regression"));
    }

    private void TestAttributeServiceUsesTypedPermanentChangeSource()
    {
        var weakOverload = typeof(AttributeService).GetMethod(
            nameof(AttributeService.ApplyPermanentAttributeChange),
            new[] { typeof(StringName), typeof(int), typeof(GDictionary) }
        );
        _test.True(
            weakOverload == null,
            "AttributeService.ApplyPermanentAttributeChange 不应再暴露 GDictionary source_context 正式入口。"
        );

        AttributeService service = BuildAttributeService(1);
        bool applied = service.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new AttributePermanentChangeSource(
                AttributePermanentChangeSourceKind.CharacterCreation,
                "character_creation",
                true
            )
        );
        _test.True(applied, "typed character creation source 应允许写入受保护 custom stat。");
        _test.Eq(service.GetBaseValue("hidden_luck_at_birth"), 3, "typed source 应真正累计 hidden_luck_at_birth。");
    }

    private void TestNonWhitelistedSourcesCannotWriteHiddenLuckAtBirth()
    {
        (string Label, StringName SourceId)[] cases =
        {
            ("成就奖励", "battle_won_first"),
            ("普通 rank 奖励", "warrior_rank_2"),
            ("道具效果", "lucky_incense"),
        };

        foreach ((string label, StringName sourceId) in cases)
        {
            AttributeService service = BuildAttributeService(2);
            bool applied = service.ApplyPermanentAttributeChange(
                "hidden_luck_at_birth",
                3,
                new AttributePermanentChangeSource(
                    AttributePermanentChangeSourceKind.Unknown,
                    sourceId,
                    false
                )
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
            new AttributePermanentChangeSource(
                AttributePermanentChangeSourceKind.CharacterCreation,
                "birth_roll",
                true
            )
        );
        _test.True(creationApplied, "CharacterCreationService 来源应能写入 hidden_luck_at_birth。");
        _test.Eq(creationService.GetBaseValue("hidden_luck_at_birth"), 3, "CharacterCreationService 来源应真正累计 hidden_luck_at_birth。");

        AttributeService unmarkedStoryService = BuildAttributeService(1);
        bool unmarkedStoryApplied = unmarkedStoryService.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new AttributePermanentChangeSource(
                AttributePermanentChangeSourceKind.StoryScript,
                "chapter_intro",
                false
            )
        );
        _test.False(unmarkedStoryApplied, "未显式标记的剧情脚本不应写入 hidden_luck_at_birth。");
        _test.Eq(unmarkedStoryService.GetBaseValue("hidden_luck_at_birth"), 1, "未显式标记的剧情脚本被拒绝后不应改写 hidden_luck_at_birth。");

        AttributeService markedStoryService = BuildAttributeService(1);
        bool markedStoryApplied = markedStoryService.ApplyPermanentAttributeChange(
            "hidden_luck_at_birth",
            2,
            new AttributePermanentChangeSource(
                AttributePermanentChangeSourceKind.StoryScript,
                "chapter_intro",
                true
            )
        );
        _test.True(markedStoryApplied, "显式标记的剧情脚本应能写入 hidden_luck_at_birth。");
        _test.Eq(markedStoryService.GetBaseValue("hidden_luck_at_birth"), 3, "显式标记的剧情脚本应真正累计 hidden_luck_at_birth。");
    }

    private void TestPendingRewardFlowRejectsProtectedHiddenLuckWrites()
    {
        var partyState = new PartyState();
        PendingCharacterReward reward = null;
        PendingCharacterRewardEntry rewardEntry = null;
        var manager = new CharacterManagementModule();
        try
        {
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

            manager.setup(partyState, new GDictionary(), new GDictionary(), new GDictionary());

            reward = manager.BuildPendingCharacterReward(
                "hero",
                "protected_hidden_luck_reward",
                "achievement",
                "battle_won_first",
                "首战成就",
                new[]
                {
                    rewardEntry = new PendingCharacterRewardEntry
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
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(reward);
            GodotRefCountedDisposer.DisposeIfValid(rewardEntry);
            manager.Dispose();
            GodotRefCountedDisposer.DisposeIfValid(partyState);
        }
    }

    private void TestUnprotectedCustomStatsRemainWritable()
    {
        AttributeService service = BuildAttributeService(1);
        bool applied = service.ApplyPermanentAttributeChange(
            "storage_space",
            2,
            new AttributePermanentChangeSource(
                AttributePermanentChangeSourceKind.Unknown,
                "pack_master",
                false
            )
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

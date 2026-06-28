using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_quest_content_validator_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialQuestValidationTypedBoundary();
        TestMissingReferenceErrorsUseTypedBoundary();
        TestNpcProviderAcceptsNonServiceInteractionId();
        TestProviderKindValidationNegativeBoundary();
        TestListingChannelValidationNegativeBoundary();

        Quit(_test.Finish("Quest content validator typed regression"));
    }

    private void TestOfficialQuestValidationTypedBoundary()
    {
        using ProgressionContentRegistry progressionRegistry = new();
        using ItemContentRegistry itemRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();

        IReadOnlyDictionary<StringName, QuestDef> questDefs =
            progressionRegistry.GetQuestDefsTyped();
        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            progressionRegistry.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();
        IReadOnlyList<string> registrationErrors =
            progressionRegistry.GetQuestRegistrationErrorsTyped();

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            questDefs,
            itemDefs,
            skillDefinitions,
            enemyTemplates,
            registrationErrors
        );
        _test.Eq(
            typedErrors.Count,
            0,
            $"正式 quest typed boundary 不应报错: {FormatErrors(typedErrors)}"
        );
    }

    private void TestMissingReferenceErrorsUseTypedBoundary()
    {
        using ItemContentRegistry itemRegistry = new();
        using SkillContentRegistry skillRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();
        using QuestDef invalidQuest = BuildInvalidQuestDef();

        Dictionary<StringName, QuestDef> typedQuestDefs = new() { [invalidQuest.quest_id] = invalidQuest };
        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            skillRegistry.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();
        List<string> registrationErrors = new() { "typed registration error" };

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            itemDefs,
            skillDefinitions,
            enemyTemplates,
            registrationErrors
        );
        _test.True(
            typedErrors.Count >= 4,
            $"typed quest validator 应把缺失引用和 registration error 视为非法。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestNpcProviderAcceptsNonServiceInteractionId()
    {
        using ItemContentRegistry itemRegistry = new();
        using SkillContentRegistry skillRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();
        using QuestDef npcQuest = BuildValidQuestDef("npc_regression_quest");
        npcQuest.provider_kind = "npc";
        npcQuest.provider_interaction_id = "npc_blacksmith_hrothgar";
        npcQuest.listing_channels = new Godot.Collections.Array<StringName> { "npc_offer" };

        Dictionary<StringName, QuestDef> typedQuestDefs = new() { [npcQuest.quest_id] = npcQuest };
        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            skillRegistry.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            itemDefs,
            skillDefinitions,
            enemyTemplates
        );
        _test.Eq(
            typedErrors.Count,
            0,
            $"NPC provider quest 应通过 provider_interaction_id 白名单校验: {FormatErrors(typedErrors)}"
        );
    }

    private void TestProviderKindValidationNegativeBoundary()
    {
        // Unknown provider_kind.
        using QuestDef unknownKindQuest = BuildMinimalQuestDef(
            "unknown_provider_kind_quest",
            "unknown_kind",
            "service_contract_board"
        );
        List<string> unknownKindErrors = new();
        QuestContentValidator.AppendProviderKindErrors(unknownKindErrors, unknownKindQuest);
        _test.Eq(
            unknownKindErrors.Count,
            1,
            $"未知 provider_kind 应产生一条错误: {FormatErrors(unknownKindErrors)}"
        );
        _test.True(
            unknownKindErrors[0].Contains("未知 provider_kind"),
            $"未知 provider_kind 错误消息应包含提示。 actual={unknownKindErrors[0]}"
        );

        // Service provider_kind with mismatched provider_interaction_id.
        using QuestDef mismatchedServiceQuest = BuildMinimalQuestDef(
            "mismatched_service_quest",
            "service_contract_board",
            "service_bounty_registry"
        );
        List<string> mismatchedErrors = new();
        QuestContentValidator.AppendProviderKindErrors(mismatchedErrors, mismatchedServiceQuest);
        _test.Eq(
            mismatchedErrors.Count,
            1,
            $"service provider_kind 与 provider_interaction_id 不匹配时应产生一条错误: {FormatErrors(mismatchedErrors)}"
        );
        _test.True(
            mismatchedErrors[0].Contains("要求 provider_interaction_id"),
            $"不匹配错误消息应包含提示。 actual={mismatchedErrors[0]}"
        );

        // NPC provider_kind with empty provider_interaction_id.
        using QuestDef npcEmptyInteractionQuest = BuildMinimalQuestDef(
            "npc_empty_interaction_quest",
            "npc",
            ""
        );
        List<string> npcEmptyErrors = new();
        QuestContentValidator.AppendProviderKindErrors(npcEmptyErrors, npcEmptyInteractionQuest);
        _test.Eq(
            npcEmptyErrors.Count,
            1,
            $"npc provider_kind 空 provider_interaction_id 应产生一条错误: {FormatErrors(npcEmptyErrors)}"
        );
        _test.True(
            npcEmptyErrors[0].Contains("需要非空的 provider_interaction_id"),
            $"NPC 空 interaction id 错误消息应包含提示。 actual={npcEmptyErrors[0]}"
        );
    }

    private void TestListingChannelValidationNegativeBoundary()
    {
        // Empty listing_channels.
        using QuestDef emptyChannelsQuest = BuildMinimalQuestDef(
            "empty_channels_quest",
            "npc",
            "npc_blacksmith_hrothgar"
        );
        emptyChannelsQuest.listing_channels = new Godot.Collections.Array<StringName>();
        List<string> emptyChannelErrors = new();
        QuestContentValidator.AppendListingChannelErrors(emptyChannelErrors, emptyChannelsQuest);
        _test.Eq(
            emptyChannelErrors.Count,
            1,
            $"空 listing_channels 应产生一条错误: {FormatErrors(emptyChannelErrors)}"
        );
        _test.True(
            emptyChannelErrors[0].Contains("listing_channels 不能为空"),
            $"空 listing_channels 错误消息应包含提示。 actual={emptyChannelErrors[0]}"
        );

        // Unknown listing_channels.
        using QuestDef unknownChannelQuest = BuildMinimalQuestDef(
            "unknown_channel_quest",
            "npc",
            "npc_blacksmith_hrothgar"
        );
        unknownChannelQuest.listing_channels = new Godot.Collections.Array<StringName> { "tavern_board" };
        List<string> unknownChannelErrors = new();
        QuestContentValidator.AppendListingChannelErrors(unknownChannelErrors, unknownChannelQuest);
        _test.Eq(
            unknownChannelErrors.Count,
            1,
            $"未知 listing_channels 应产生一条错误: {FormatErrors(unknownChannelErrors)}"
        );
        _test.True(
            unknownChannelErrors[0].Contains("listing_channels 包含未知渠道"),
            $"未知 listing_channels 错误消息应包含提示。 actual={unknownChannelErrors[0]}"
        );
    }

    private static QuestDef BuildValidQuestDef(StringName questId)
    {
        return new QuestDef
        {
            quest_id = questId,
            display_name = "Valid Quest",
            description = "A valid quest.",
            provider_kind = "service_contract_board",
            provider_interaction_id = "service_contract_board",
            listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
            tags = new Godot.Collections.Array<StringName>(),
            accept_requirements = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["objective_id"] = "complete_once",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    ["target_id"] = "service:training",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold),
                    ["amount"] = 10,
                },
            },
            is_repeatable = false,
        };
    }

    private static QuestDef BuildMinimalQuestDef(
        StringName questId,
        StringName providerKind,
        StringName providerInteractionId
    )
    {
        return new QuestDef
        {
            quest_id = questId,
            display_name = "Minimal Quest",
            description = "A minimal quest for negative boundary tests.",
            provider_kind = providerKind,
            provider_interaction_id = providerInteractionId,
            listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
            tags = new Godot.Collections.Array<StringName>(),
            accept_requirements = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            objective_defs = new Godot.Collections.Array<GDictionary>(),
            reward_entries = new Godot.Collections.Array<GDictionary>(),
            is_repeatable = false,
        };
    }

    private static QuestDef BuildInvalidQuestDef()
    {
        return new QuestDef
        {
            quest_id = "typed_missing_reference_quest",
            display_name = "Typed Missing Reference Quest",
            description = "Regression quest for typed quest validation boundary.",
            provider_kind = "service_contract_board",
            provider_interaction_id = "service_contract_board",
            listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["objective_id"] = "deliver_missing_relic",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                    ["target_id"] = "missing_relic",
                    ["target_value"] = 1,
                },
                new GDictionary
                {
                    ["objective_id"] = "defeat_missing_enemy",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
                    ["target_id"] = "missing_enemy_template",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.PendingCharacterReward),
                    ["member_id"] = "hero",
                    ["entries"] = new Godot.Collections.Array
                    {
                        new GDictionary
                        {
                            ["entry_type"] = PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillUnlock),
                            ["target_id"] = "missing_skill_reward",
                            ["amount"] = 1,
                        },
                    },
                },
            },
        };
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error ?? "");
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

}

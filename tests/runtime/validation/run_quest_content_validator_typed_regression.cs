using System;
using System.Collections.Generic;
using Godot;

public partial class run_quest_content_validator_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _snapshot;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        _snapshot = GameSessionTestFactory.GetProcessSnapshot();
        TestOfficialQuestValidationTypedBoundary();
        TestUnboundObjectiveNormalizesEncounterProfileId();
        TestMissingReferenceErrorsUseTypedBoundary();
        TestDanglingEncounterProfileReferenceIsRejected();
        TestEncounterGrowthStageValidation();
        TestNpcProviderAcceptsNonServiceInteractionId();
        TestProviderKindValidationNegativeBoundary();
        TestListingChannelValidationNegativeBoundary();
        TestAcceptRequirementValidation();
        TestBountyDangerRatingValidation();
        TestListingSettlementValidation();

        RequestTestExit(_test.Finish("Quest content validator typed regression"));
    }

    private void TestOfficialQuestValidationTypedBoundary()
    {
        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            _snapshot.Quests,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates,
            Array.Empty<string>(),
            _snapshot.BattleEncounters,
            _snapshot.EncounterRosters
        );
        _test.Eq(
            typedErrors.Count,
            0,
            $"正式 quest typed boundary 不应报错: {FormatErrors(typedErrors)}"
        );

        bool hasFarmerPlea = _snapshot.Quests.TryGetValue(
            "folk_farmers_plea",
            out QuestDefinition farmerPlea
        );
        _test.True(hasFarmerPlea, "正式 quest snapshot 应发布《农夫的恳求》。");
        if (!hasFarmerPlea)
            return;
        _test.Eq(farmerPlea.Objectives.Count, 1, "《农夫的恳求》应只有一个正式战斗目标。");
        if (farmerPlea.Objectives.Count != 1)
            return;
        _test.Eq(
            farmerPlea.Objectives[0].EncounterProfileId,
            new StringName("wolf_wilds"),
            "《农夫的恳求》应复用正式共享 wolf_wilds 遭遇模板。"
        );
        _test.Eq(
            farmerPlea.Objectives[0].EncounterGrowthStage,
            1,
            "《农夫的恳求》应选择共享狼群 roster 的五狼阶段。"
        );
    }

    private void TestUnboundObjectiveNormalizesEncounterProfileId()
    {
        var objective = new QuestObjectiveDefinition(
            "complete_training",
            "settlement_action",
            "service:training",
            1
        );
        _test.True(
            objective.EncounterProfileId is not null,
            "未绑定接取遭遇的 objective 应把默认 StringName 归一化为空值对象。"
        );
        _test.Eq(
            objective.EncounterProfileId,
            new StringName(""),
            "未绑定接取遭遇的 objective 应公开空 encounter_profile_id。"
        );

        QuestDefinition quest = BuildQuestDefinition(
            "unbound_encounter_profile_quest",
            objectives: [objective]
        );
        Dictionary<StringName, QuestDefinition> questDefs = new()
        {
            [quest.QuestId] = quest,
        };
        List<string> errors = QuestContentValidator.ValidateTyped(
            questDefs,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates,
            Array.Empty<string>(),
            _snapshot.BattleEncounters,
            _snapshot.EncounterRosters
        );
        _test.Eq(
            errors.Count,
            0,
            $"未绑定 encounter 的 objective 不应进入 encounter 字典查询。 errors={FormatErrors(errors)}"
        );
    }

    private void TestMissingReferenceErrorsUseTypedBoundary()
    {
        QuestDefinition invalidQuest = BuildInvalidQuestDefinition();

        Dictionary<StringName, QuestDefinition> typedQuestDefs = new()
        {
            [invalidQuest.QuestId] = invalidQuest,
        };
        List<string> registrationErrors = new() { "typed registration error" };

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates,
            registrationErrors
        );
        _test.True(
            typedErrors.Count >= 4,
            $"typed quest validator 应把缺失引用和 registration error 视为非法。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestDanglingEncounterProfileReferenceIsRejected()
    {
        StringName validEnemyTemplateId = "";
        foreach (StringName enemyTemplateId in _snapshot.EnemyTemplates.Keys)
        {
            validEnemyTemplateId = enemyTemplateId;
            break;
        }
        _test.True(validEnemyTemplateId != "", "正式 enemy template 索引应至少包含一个条目。");

        QuestDefinition danglingEncounterQuest = BuildQuestDefinition(
            "dangling_encounter_profile_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "defeat_at_missing_encounter",
                    "defeat_enemy",
                    validEnemyTemplateId,
                    1,
                    "missing_quest_encounter_profile",
                    "Missing Encounter"
                ),
            ]
        );
        Dictionary<StringName, QuestDefinition> typedQuestDefs = new()
        {
            [danglingEncounterQuest.QuestId] = danglingEncounterQuest,
        };

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates,
            Array.Empty<string>(),
            _snapshot.BattleEncounters,
            _snapshot.EncounterRosters
        );
        _test.True(
            typedErrors.Contains(
                "Quest dangling_encounter_profile_quest objective defeat_at_missing_encounter references missing battle encounter missing_quest_encounter_profile."
            ),
            $"dangling encounter_profile_id 应报告缺失 encounter profile。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestEncounterGrowthStageValidation()
    {
        QuestDefinition negativeStageQuest = BuildQuestDefinition(
            "negative_encounter_growth_stage_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "defeat_wolves",
                    "defeat_enemy",
                    "wolf_pack",
                    1,
                    "wolf_wilds",
                    "Wolves",
                    -1
                ),
            ]
        );
        QuestDefinition unboundStageQuest = BuildQuestDefinition(
            "unbound_encounter_growth_stage_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "defeat_wolves",
                    "defeat_enemy",
                    "wolf_pack",
                    1,
                    "",
                    "",
                    1
                ),
            ]
        );
        QuestDefinition undeclaredStageQuest = BuildQuestDefinition(
            "undeclared_encounter_growth_stage_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "defeat_wolves",
                    "defeat_enemy_in_single_battle",
                    "wolf_pack",
                    1,
                    "wolf_wilds",
                    "Wolves",
                    99
                ),
            ]
        );
        QuestDefinition insufficientStageQuest = BuildQuestDefinition(
            "insufficient_encounter_growth_stage_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "defeat_wolves",
                    "defeat_enemy_in_single_battle",
                    "wolf_pack",
                    5,
                    "wolf_wilds",
                    "Wolves",
                    0
                ),
            ]
        );
        Dictionary<StringName, QuestDefinition> questDefs = new()
        {
            [negativeStageQuest.QuestId] = negativeStageQuest,
            [unboundStageQuest.QuestId] = unboundStageQuest,
            [undeclaredStageQuest.QuestId] = undeclaredStageQuest,
            [insufficientStageQuest.QuestId] = insufficientStageQuest,
        };

        List<string> errors = QuestContentValidator.ValidateTyped(
            questDefs,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates,
            Array.Empty<string>(),
            _snapshot.BattleEncounters,
            _snapshot.EncounterRosters
        );
        _test.True(
            errors.Contains(
                "Quest negative_encounter_growth_stage_quest: QuestDef negative_encounter_growth_stage_quest 的 objective defeat_wolves 的 encounter_growth_stage 不能为负数。"
            ),
            $"负 encounter_growth_stage 应被拒绝。 errors={FormatErrors(errors)}"
        );
        _test.True(
            errors.Contains(
                "Quest unbound_encounter_growth_stage_quest: QuestDef unbound_encounter_growth_stage_quest 的 objective defeat_wolves 只有绑定接取遭遇时才能配置 encounter_growth_stage。"
            ),
            $"未绑定 encounter 时不得单独配置 growth stage。 errors={FormatErrors(errors)}"
        );
        _test.True(
            errors.Contains(
                "Quest undeclared_encounter_growth_stage_quest objective defeat_wolves references undeclared growth stage 99 in encounter roster wolf_pack_skirmish."
            ),
            $"任务必须绑定 roster 中精确定义的 growth stage。 errors={FormatErrors(errors)}"
        );
        _test.True(
            errors.Contains(
                "Quest insufficient_encounter_growth_stage_quest objective defeat_wolves requires 5 wolf_pack in one battle, but encounter wolf_wilds roster wolf_pack_skirmish stage 0 provides 2."
            ),
            $"单场击败目标不得绑定敌人数不足的共享 roster stage。 errors={FormatErrors(errors)}"
        );
    }

    private void TestNpcProviderAcceptsNonServiceInteractionId()
    {
        QuestDefinition npcQuest = BuildQuestDefinition(
            "npc_regression_quest",
            providerKind: "npc",
            providerInteractionId: "npc_blacksmith_hrothgar",
            listingChannels: [new StringName("npc_offer")]
        );

        Dictionary<StringName, QuestDefinition> typedQuestDefs = new()
        {
            [npcQuest.QuestId] = npcQuest,
        };
        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            _snapshot.Items,
            _snapshot.Skills,
            _snapshot.EnemyTemplates
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
        QuestDefinition unknownKindQuest = BuildQuestDefinition(
            "unknown_provider_kind_quest",
            providerKind: "unknown_kind",
            providerInteractionId: "service_contract_board"
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
        QuestDefinition mismatchedServiceQuest = BuildQuestDefinition(
            "mismatched_service_quest",
            providerKind: "service_contract_board",
            providerInteractionId: "service_bounty_registry"
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
        QuestDefinition npcEmptyInteractionQuest = BuildQuestDefinition(
            "npc_empty_interaction_quest",
            providerKind: "npc",
            providerInteractionId: ""
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
        QuestDefinition emptyChannelsQuest = BuildQuestDefinition(
            "empty_channels_quest",
            providerKind: "npc",
            providerInteractionId: "npc_blacksmith_hrothgar",
            listingChannels: Array.Empty<StringName>()
        );
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
        QuestDefinition unknownChannelQuest = BuildQuestDefinition(
            "unknown_channel_quest",
            providerKind: "npc",
            providerInteractionId: "npc_blacksmith_hrothgar",
            listingChannels: [new StringName("tavern_board")]
        );
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

    private void TestAcceptRequirementValidation()
    {
        QuestDefinition validTargetQuest = BuildQuestDefinition(
            "accept_req_target",
            acceptRequirements:
            [
                new QuestAcceptRequirementDefinition(
                    "quest_completed",
                    "accept_req_prereq"
                ),
            ]
        );
        QuestDefinition validPrereqQuest = BuildQuestDefinition("accept_req_prereq");

        var questDefs = new Dictionary<StringName, QuestDefinition>
        {
            [validTargetQuest.QuestId] = validTargetQuest,
            [validPrereqQuest.QuestId] = validPrereqQuest,
        };

        List<string> validErrors = new();
        QuestContentValidator.AppendAcceptRequirementErrors(validErrors, validTargetQuest, questDefs);
        _test.Eq(validErrors.Count, 0, "有效的 accept_requirements 引用不应报错。");

        QuestDefinition unknownTypeQuest = BuildQuestDefinition(
            "unknown_req_type",
            acceptRequirements:
            [new QuestAcceptRequirementDefinition("gold_min", "accept_req_prereq")]
        );
        List<string> unknownTypeErrors = new();
        QuestContentValidator.AppendAcceptRequirementErrors(unknownTypeErrors, unknownTypeQuest, questDefs);
        _test.Eq(unknownTypeErrors.Count, 1, "不支持的 requirement_type 应产生一条错误。");
        _test.True(
            unknownTypeErrors[0].Contains("不支持的 requirement_type"),
            $"错误消息应提示不支持的类型。 actual={unknownTypeErrors[0]}"
        );

        QuestDefinition missingIdQuest = BuildQuestDefinition(
            "missing_req_id",
            acceptRequirements:
            [new QuestAcceptRequirementDefinition("quest_completed", "")]
        );
        List<string> missingIdErrors = new();
        QuestContentValidator.AppendAcceptRequirementErrors(missingIdErrors, missingIdQuest, questDefs);
        _test.Eq(missingIdErrors.Count, 1, "缺少 quest_id 的 requirement 应产生一条错误。");
        _test.True(
            missingIdErrors[0].Contains("缺少 quest_id"),
            $"错误消息应提示缺少 quest_id。 actual={missingIdErrors[0]}"
        );

        QuestDefinition danglingRefQuest = BuildQuestDefinition(
            "dangling_req_ref",
            acceptRequirements:
            [
                new QuestAcceptRequirementDefinition(
                    "quest_completed",
                    "non_existent_quest"
                ),
            ]
        );
        List<string> danglingErrors = new();
        QuestContentValidator.AppendAcceptRequirementErrors(danglingErrors, danglingRefQuest, questDefs);
        _test.Eq(danglingErrors.Count, 1, "引用不存在 quest_id 的 requirement 应产生一条错误。");
        _test.True(
            danglingErrors[0].Contains("不存在的 quest_id"),
            $"错误消息应提示引用不存在。 actual={danglingErrors[0]}"
        );
    }

    private void TestBountyDangerRatingValidation()
    {
        // Bounty-listed quest with a non-combat objective and no override cannot derive stars.
        QuestDefinition underivableBounty = BuildQuestDefinition(
            "underivable_bounty_quest",
            providerKind: "service_bounty_registry",
            providerInteractionId: "service_bounty_registry",
            listingChannels: [new StringName("bounty_registry")]
        );
        List<string> underivableErrors = new();
        QuestContentValidator.AppendDangerRatingErrors(
            underivableErrors,
            underivableBounty,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            underivableErrors.Count,
            1,
            $"无法推导危险度的悬赏任务应产生一条错误: {FormatErrors(underivableErrors)}"
        );
        _test.True(
            underivableErrors[0].Contains("无法推导危险度"),
            $"错误消息应提示无法推导危险度。 actual={underivableErrors[0]}"
        );

        // The same quest passes once the author sets danger_tier_override.
        QuestDefinition overriddenBounty = BuildQuestDefinition(
            "overridden_bounty_quest",
            providerKind: "service_bounty_registry",
            providerInteractionId: "service_bounty_registry",
            listingChannels: [new StringName("bounty_registry")],
            dangerTierOverride: 2
        );
        List<string> overriddenErrors = new();
        QuestContentValidator.AppendDangerRatingErrors(
            overriddenErrors,
            overriddenBounty,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            overriddenErrors.Count,
            0,
            $"带 override 的悬赏任务不应报危险度错误: {FormatErrors(overriddenErrors)}"
        );

        // Bounty-listed quests must not configure per-item accept confirmation.
        QuestDefinition confirmationBounty = BuildQuestDefinition(
            "confirmation_bounty_quest",
            providerKind: "service_bounty_registry",
            providerInteractionId: "service_bounty_registry",
            listingChannels: [new StringName("bounty_registry")],
            acceptConfirmationText: "确认接取这条悬赏吗？",
            dangerTierOverride: 1
        );
        List<string> confirmationErrors = new();
        QuestContentValidator.AppendDangerRatingErrors(
            confirmationErrors,
            confirmationBounty,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            confirmationErrors.Count,
            1,
            $"悬赏任务配置 accept_confirmation_text 应产生一条错误: {FormatErrors(confirmationErrors)}"
        );
        _test.True(
            confirmationErrors[0].Contains("accept_confirmation_text"),
            $"错误消息应提示禁用逐项确认。 actual={confirmationErrors[0]}"
        );

        // Non-bounty quests are exempt from both rules.
        QuestDefinition contractQuest = BuildQuestDefinition(
            "contract_confirmation_ok_quest",
            acceptConfirmationText: "确认接取这份契约吗？"
        );
        List<string> contractErrors = new();
        QuestContentValidator.AppendDangerRatingErrors(
            contractErrors,
            contractQuest,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            contractErrors.Count,
            0,
            $"非悬赏任务不应受危险度规则约束: {FormatErrors(contractErrors)}"
        );
    }

    private void TestListingSettlementValidation()
    {
        // Bounty-listed quests must bind at least one settlement.
        QuestDefinition unboundBounty = BuildQuestDefinition(
            "unbound_bounty_quest",
            providerKind: "service_bounty_registry",
            providerInteractionId: "service_bounty_registry",
            listingChannels: [new StringName("bounty_registry")]
        );
        List<string> unboundErrors = new();
        QuestContentValidator.AppendListingSettlementErrors(unboundErrors, unboundBounty);
        _test.Eq(
            unboundErrors.Count,
            1,
            $"未绑定据点的悬赏任务应产生一条错误: {FormatErrors(unboundErrors)}"
        );
        _test.True(
            unboundErrors[0].Contains("listing_settlement_ids"),
            $"错误消息应提示 listing_settlement_ids。 actual={unboundErrors[0]}"
        );

        // A bound bounty passes.
        QuestDefinition boundBounty = BuildQuestDefinition(
            "bound_bounty_quest",
            providerKind: "service_bounty_registry",
            providerInteractionId: "service_bounty_registry",
            listingChannels: [new StringName("bounty_registry")],
            listingSettlementIds: [new StringName("template_city")]
        );
        List<string> boundErrors = new();
        QuestContentValidator.AppendListingSettlementErrors(boundErrors, boundBounty);
        _test.Eq(
            boundErrors.Count,
            0,
            $"已绑定据点的悬赏任务不应报错: {FormatErrors(boundErrors)}"
        );

        // Non-bounty quests must not configure the field (no consumer yet).
        QuestDefinition boundContract = BuildQuestDefinition(
            "bound_contract_quest",
            listingSettlementIds: [new StringName("template_city")]
        );
        List<string> contractErrors = new();
        QuestContentValidator.AppendListingSettlementErrors(contractErrors, boundContract);
        _test.Eq(
            contractErrors.Count,
            1,
            $"非悬赏任务配置 listing_settlement_ids 应产生一条错误: {FormatErrors(contractErrors)}"
        );
        _test.True(
            contractErrors[0].Contains("仅悬赏板"),
            $"错误消息应提示字段仅悬赏板消费。 actual={contractErrors[0]}"
        );
    }

    private static QuestDefinition BuildQuestDefinition(
        StringName questId,
        string providerKind = null,
        string providerInteractionId = null,
        IReadOnlyList<StringName> listingChannels = null,
        IReadOnlyList<QuestAcceptRequirementDefinition> acceptRequirements = null,
        IReadOnlyList<QuestObjectiveDefinition> objectives = null,
        IReadOnlyList<QuestRewardDefinition> rewards = null,
        string acceptConfirmationText = "",
        int dangerTierOverride = 0,
        IReadOnlyList<StringName> listingSettlementIds = null
    )
    {
        IReadOnlyList<QuestObjectiveDefinition> resolvedObjectives = objectives
            ??
            [
                new QuestObjectiveDefinition(
                    "complete_once",
                    "settlement_action",
                    "service:training",
                    1
                ),
            ];
        IReadOnlyList<QuestRewardDefinition> resolvedRewards = rewards
            ??
            [
                new QuestRewardDefinition(
                    "gold",
                    10,
                    "",
                    0,
                    "",
                    Array.Empty<QuestPendingRewardEntryDefinition>()
                ),
            ];
        return new QuestDefinition(
            questId,
            "Valid Quest",
            "A valid quest.",
            new StringName(providerInteractionId ?? "service_contract_board"),
            Array.Empty<StringName>(),
            acceptRequirements ?? Array.Empty<QuestAcceptRequirementDefinition>(),
            resolvedObjectives,
            resolvedRewards,
            false,
            new StringName(providerKind ?? "service_contract_board"),
            listingChannels ?? [new StringName("contract_board")],
            "",
            "",
            "",
            acceptConfirmationText,
            dangerTierOverride,
            listingSettlementIds
        );
    }

    private static QuestDefinition BuildInvalidQuestDefinition() =>
        BuildQuestDefinition(
            "typed_missing_reference_quest",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "deliver_missing_relic",
                    "submit_item",
                    "missing_relic",
                    1
                ),
                new QuestObjectiveDefinition(
                    "defeat_missing_enemy",
                    "defeat_enemy",
                    "missing_enemy_template",
                    1
                ),
                new QuestObjectiveDefinition(
                    "defeat_missing_enemy_together",
                    "defeat_enemy_in_single_battle",
                    "missing_single_battle_enemy_template",
                    5
                ),
            ],
            rewards:
            [
                new QuestRewardDefinition(
                    "pending_character_reward",
                    0,
                    "",
                    0,
                    "hero",
                    [
                        new QuestPendingRewardEntryDefinition(
                            PendingCharacterRewardContentRules.ToStringName(
                                PendingCharacterRewardEntryKind.SkillUnlock
                            ),
                            "missing_skill_reward",
                            1
                        ),
                    ]
                ),
            ]
        );

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

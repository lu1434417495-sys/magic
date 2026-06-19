using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_quest_materializer_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSubmitItemObjectiveTracksProgressAndFailures();
        TestQuestRewardMaterializesGoldItemsAndOverflow();
        TestQuestRewardQueuesPendingCharacterReward();
        TestPendingCharacterRewardRejectsInvalidAttributeTarget();
        TestPendingCharacterRewardBoundaryAcceptsTypedRewards();
        TestAttributeProgressRewardConvertsAndAccumulatesWithTypedResult();
        TestLevelGrowthEvaluationServiceSetupUsesExactSkillDefKeys();
        TestActiveLevelTriggerSetAndClearUseTypedResult();
        TestActiveLevelTriggerAttributeGrowthUsesTypedEntries();
        TestActiveLevelTriggerAttributeGrowthRejectsInvalidEntries();
        TestSkillMasteryRewardAggregatesTypedEntries();
        TestStringKeyOnlyQuestRewardDefIsRejected();

        Quit(_test.Finish("Character management quest materializer regression"));
    }

    private void TestLevelGrowthEvaluationServiceSetupUsesExactSkillDefKeys()
    {
        PartyState party = BuildPartyWithMember("hero", 1);
        PartyMemberState member = party.GetMemberState("hero");
        SkillDef triggerSkill = new()
        {
            skill_id = "test_level_trigger_catalog_boundary",
            display_name = "Test level trigger catalog boundary",
            max_level = 1,
        };
        member.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = triggerSkill.skill_id,
                is_learned = true,
                is_core = true,
                skill_level = 1,
            }
        );
        member.progression.active_level_trigger_core_skill_id = triggerSkill.skill_id;

        LevelGrowthEvaluationService service = new();
        service.Setup(
            new Dictionary<StringName, SkillDef>
            {
                [new StringName("wrong_level_trigger_key")] = triggerSkill,
            }
        );
        _test.True(
            !service.IsActiveTriggerReadyForLevelUp(member),
            "LevelGrowthEvaluationService.Setup should not recover a skill def from value.skill_id when the dictionary key is wrong."
        );

        service.Setup(new Dictionary<StringName, SkillDef> { [triggerSkill.skill_id] = triggerSkill });
        _test.True(
            service.IsActiveTriggerReadyForLevelUp(member),
            "LevelGrowthEvaluationService.Setup should accept a typed skill def map keyed by skill id."
        );
    }

    private void TestSubmitItemObjectiveTracksProgressAndFailures()
    {
        PartyState party = BuildPartyWithMember("hero", 4);
        GDictionary itemDefs = BuildItemDefs();

        QuestDef submitQuest = BuildSubmitItemQuest(
            "contract_supply_delivery",
            "deliver_ore",
            "iron_ore",
            2
        );
        QuestDef shortageQuest = BuildSubmitItemQuest(
            "contract_supply_delivery_shortage",
            "deliver_ore",
            "iron_ore",
            2
        );
        QuestDef wrongItemQuest = BuildSubmitItemQuest(
            "contract_supply_delivery_wrong_item",
            "deliver_ore",
            "iron_ore",
            2
        );
        QuestDef missingTargetQuest = new()
        {
            quest_id = "contract_supply_delivery_missing_target",
            display_name = "Missing target",
        };
        missingTargetQuest.objective_defs.Add(
            new GDictionary
            {
                ["objective_id"] = "deliver_ore",
                ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                ["target_id"] = "iron_ore",
            }
        );

        CharacterManagementModule manager = BuildManager(
            party,
            itemDefs,
            new GDictionary
            {
                [submitQuest.quest_id] = submitQuest,
                [shortageQuest.quest_id] = shortageQuest,
                [wrongItemQuest.quest_id] = wrongItemQuest,
                [missingTargetQuest.quest_id] = missingTargetQuest,
            }
        );
        PartyWarehouseService warehouse = new();
        warehouse.Setup(party, BuildItemDefIndex(itemDefs));

        QuestState partialQuest = new() { quest_id = submitQuest.quest_id };
        partialQuest.MarkAccepted(3);
        partialQuest.RecordObjectiveProgress(
            "deliver_ore",
            1,
            2,
            new GDictionary { ["item_id"] = "iron_ore", ["submitted_quantity"] = 1 }
        );
        party.SetActiveQuestState(partialQuest);
        warehouse.AddItemTyped("iron_ore", 1);

        GDictionary partialResult = QuestCommandResultProjection.Project(
            manager.SubmitItemObjectiveTyped(submitQuest.quest_id, "deliver_ore", 4)
        );
        _test.True(ReadBool(partialResult, "ok"), "submit_item should complete a partially progressed objective.");
        _test.Eq(ReadInt(partialResult, "submitted_quantity"), 1, "submit_item should only withdraw the remaining quantity.");
        _test.True(!party.HasActiveQuest(submitQuest.quest_id), "successful submit_item should leave active quests.");
        _test.True(party.HasClaimableQuest(submitQuest.quest_id), "successful submit_item should enter claimable quests.");
        _test.Eq(warehouse.CountItem("iron_ore"), 0, "successful submit_item should consume the submitted item.");
        QuestState claimableSubmitQuest = party.GetClaimableQuestState(submitQuest.quest_id);
        _test.True(claimableSubmitQuest != null, "successful submit_item should preserve quest state.");
        if (claimableSubmitQuest != null)
        {
            _test.Eq(
                claimableSubmitQuest.GetObjectiveProgress("deliver_ore"),
                2,
                "successful submit_item should fill objective progress to target."
            );
            _test.Eq(
                ReadInt(claimableSubmitQuest.last_progress_context, "submitted_quantity"),
                1,
                "successful submit_item should record submitted quantity context."
            );
            _test.Eq(
                ReadString(claimableSubmitQuest.last_progress_context, "item_id"),
                "iron_ore",
                "successful submit_item should record submitted item context."
            );
            _test.Eq(
                claimableSubmitQuest.completed_at_world_step,
                4,
                "successful submit_item should record completion world step."
            );
        }

        QuestState shortageState = new() { quest_id = shortageQuest.quest_id };
        shortageState.MarkAccepted(5);
        party.SetActiveQuestState(shortageState);
        GDictionary shortageResult = QuestCommandResultProjection.Project(
            manager.SubmitItemObjectiveTyped(shortageQuest.quest_id, "deliver_ore", 6)
        );
        _test.True(!ReadBool(shortageResult, "ok"), "submit_item should fail when inventory is short.");
        _test.Eq(
            ReadString(shortageResult, "error_code"),
            "submit_item_missing_inventory",
            "submit_item shortage should return the formal shortage error."
        );
        _test.True(party.HasActiveQuest(shortageQuest.quest_id), "shortage should keep quest active.");
        _test.True(!party.HasClaimableQuest(shortageQuest.quest_id), "shortage should not mark quest claimable.");

        QuestState wrongItemState = new() { quest_id = wrongItemQuest.quest_id };
        wrongItemState.MarkAccepted(7);
        party.SetActiveQuestState(wrongItemState);
        warehouse.AddItemTyped("bronze_sword", 1);
        int bronzeBefore = warehouse.CountItem("bronze_sword");
        GDictionary wrongItemResult = QuestCommandResultProjection.Project(
            manager.SubmitItemObjectiveTyped(wrongItemQuest.quest_id, "deliver_ore", 8)
        );
        _test.True(!ReadBool(wrongItemResult, "ok"), "submit_item should fail when only the wrong item exists.");
        _test.Eq(
            ReadString(wrongItemResult, "error_code"),
            "submit_item_missing_inventory",
            "wrong item submit should still report missing target inventory."
        );
        _test.Eq(
            warehouse.CountItem("bronze_sword"),
            bronzeBefore,
            "wrong item submit should not consume other inventory."
        );

        QuestState missingTargetState = new() { quest_id = missingTargetQuest.quest_id };
        missingTargetState.MarkAccepted(9);
        party.SetActiveQuestState(missingTargetState);
        GDictionary missingTargetResult = QuestCommandResultProjection.Project(
            manager.SubmitItemObjectiveTyped(missingTargetQuest.quest_id, "deliver_ore", 10)
        );
        _test.True(!ReadBool(missingTargetResult, "ok"), "submit_item should reject objectives without target_value.");
        _test.Eq(
            ReadString(missingTargetResult, "error_code"),
            "invalid_submit_item_objective",
            "missing target_value should not default to one."
        );
        _test.True(
            party.HasActiveQuest(missingTargetQuest.quest_id),
            "missing target_value should keep quest active."
        );
    }

    private void TestQuestRewardMaterializesGoldItemsAndOverflow()
    {
        GDictionary itemDefs = BuildItemDefs();
        QuestDef rewardQuest = BuildRewardQuest(
            "contract_supply_receipt",
            "Supply receipt",
            new GDictionary { ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold), ["amount"] = 12 },
            new GDictionary
            {
                ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Item),
                ["item_id"] = "iron_ore",
                ["quantity"] = 2,
            }
        );
        QuestDef overflowQuest = BuildRewardQuest(
            "contract_reward_overflow",
            "Overflow",
            new GDictionary
            {
                ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Item),
                ["item_id"] = "iron_ore",
                ["quantity"] = 1,
            }
        );

        PartyState party = BuildPartyWithMember("porter", 4);
        CharacterManagementModule manager = BuildManager(
            party,
            itemDefs,
            new GDictionary
            {
                [rewardQuest.quest_id] = rewardQuest,
                [overflowQuest.quest_id] = overflowQuest,
            }
        );
        PartyWarehouseService warehouse = new();
        warehouse.Setup(party, BuildItemDefIndex(itemDefs));
        party.SetClaimableQuestState(BuildClaimableQuest("contract_supply_receipt", 4, 6));

        GDictionary claimResult = QuestCommandResultProjection.Project(
            manager.ClaimQuestRewardTyped("contract_supply_receipt", 8)
        );
        _test.True(ReadBool(claimResult, "ok"), "quest reward should claim successfully.");
        _test.Eq(ReadInt(claimResult, "gold_delta"), 12, "quest reward should expose gold delta.");
        _test.Eq(
            ExtractItemRewardQuantity(ReadArray(claimResult, "item_rewards"), "iron_ore"),
            2,
            "quest reward should expose materialized item rewards."
        );
        _test.Eq(warehouse.CountItem("iron_ore"), 2, "quest reward should deposit item rewards.");
        _test.Eq(party.GetGold(), 12, "quest reward should add gold to party state.");
        _test.True(!party.HasClaimableQuest("contract_supply_receipt"), "claimed quest should leave claimable quests.");
        _test.True(party.HasCompletedQuest("contract_supply_receipt"), "claimed quest should enter completed quests.");

        PartyState overflowParty = BuildPartyWithMember("porter", 1);
        PartyWarehouseService overflowWarehouse = new();
        overflowWarehouse.Setup(overflowParty, BuildItemDefIndex(itemDefs));
        overflowWarehouse.AddItemTyped("bronze_sword", 1);
        CharacterManagementModule overflowManager = BuildManager(
            overflowParty,
            itemDefs,
            new GDictionary { [overflowQuest.quest_id] = overflowQuest }
        );
        overflowParty.SetClaimableQuestState(BuildClaimableQuest("contract_reward_overflow", 5, 7));

        GDictionary overflowResult = QuestCommandResultProjection.Project(
            overflowManager.ClaimQuestRewardTyped("contract_reward_overflow", 9)
        );
        _test.True(!ReadBool(overflowResult, "ok"), "quest reward should fail when warehouse is full.");
        _test.Eq(
            ReadString(overflowResult, "error_code"),
            "reward_overflow",
            "warehouse overflow should map to reward_overflow."
        );
        _test.True(
            overflowParty.HasClaimableQuest("contract_reward_overflow"),
            "overflow should keep quest claimable."
        );
        _test.True(
            !overflowParty.HasCompletedQuest("contract_reward_overflow"),
            "overflow should not complete quest reward."
        );
        _test.Eq(
            overflowWarehouse.CountItem("bronze_sword"),
            1,
            "overflow should preserve existing warehouse item."
        );
    }

    private void TestQuestRewardQueuesPendingCharacterReward()
    {
        PartyState party = BuildPartyWithMember("hero", 4);
        QuestDef quest = BuildRewardQuest(
            "contract_growth_drill",
            "Growth drill",
            new GDictionary
            {
                ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.PendingCharacterReward),
                ["member_id"] = "hero",
                ["summary_text"] = "Growth reward.",
                ["entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["entry_type"] = "skill_unlock",
                        ["target_id"] = "charge",
                        ["target_label"] = "Charge",
                        ["amount"] = 1,
                    },
                    new GDictionary
                    {
                        ["entry_type"] = "skill_mastery",
                        ["target_id"] = "charge",
                        ["target_label"] = "Charge",
                        ["amount"] = 10,
                    },
                },
            }
        );
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary { [quest.quest_id] = quest }
        );
        party.SetClaimableQuestState(BuildClaimableQuest("contract_growth_drill", 6, 9));

        GDictionary claimResult = QuestCommandResultProjection.Project(
            manager.ClaimQuestRewardTyped("contract_growth_drill", 12)
        );
        _test.True(ReadBool(claimResult, "ok"), "pending character quest reward should claim.");
        _test.Eq(
            ReadArray(claimResult, "pending_character_rewards").Count,
            1,
            "claim result should expose materialized pending character reward."
        );
        _test.Eq(
            party.pending_character_rewards.Count,
            1,
            "pending character reward should enter party queue."
        );
        PendingCharacterReward queuedReward = party.GetNextPendingCharacterReward();
        _test.True(queuedReward != null, "queued pending character reward should be readable.");
        if (queuedReward != null)
        {
            _test.Eq(queuedReward.member_id, new StringName("hero"), "pending reward should preserve member id.");
            _test.Eq(queuedReward.source_id, quest.quest_id, "pending reward should default source id to quest id.");
            _test.Eq(queuedReward.source_label, "Growth drill", "pending reward should default source label to quest name.");
            _test.Eq(queuedReward.entries.Count, 2, "pending reward should preserve entries.");
        }
    }

    private void TestStringKeyOnlyQuestRewardDefIsRejected()
    {
        QuestDef quest = BuildRewardQuest(
            "contract_string_key_reward",
            "String key reward",
            new GDictionary { ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold), ["amount"] = 1 }
        );
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary { [quest.quest_id.ToString()] = quest }
        );
        party.SetClaimableQuestState(BuildClaimableQuest("contract_string_key_reward", 1, 2));

        GDictionary claimResult = QuestCommandResultProjection.Project(
            manager.ClaimQuestRewardTyped("contract_string_key_reward", 3)
        );
        _test.True(!ReadBool(claimResult, "ok"), "String-key-only quest def should be rejected.");
        _test.Eq(
            ReadString(claimResult, "error_code"),
            "quest_def_missing",
            "String-key-only quest def should not be accepted as formal quest data."
        );
    }

    private void TestPendingCharacterRewardRejectsInvalidAttributeTarget()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary()
        );

        PendingCharacterReward reward = manager.BuildPendingCharacterReward(
            "hero",
            "",
            "quest",
            "invalid_attribute_reward",
            "Invalid attribute reward",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.AttributeProgress,
                    target_id = "not_an_attribute",
                    amount = 1,
                },
            },
            "Invalid target should be rejected."
        );

        _test.True(
            reward == null,
            "pending character reward should reject attribute_progress entries with invalid targets."
        );
    }

    private void TestPendingCharacterRewardBoundaryAcceptsTypedRewards()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary()
        );

        PendingCharacterReward typedReward = manager.BuildPendingCharacterReward(
            "hero",
            "typed_reward",
            "quest",
            "typed_source",
            "Typed reward",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.AttributeDelta,
                    target_id = "strength",
                    target_label = "Strength",
                    amount = 1,
                },
            },
            "typed summary"
        );
        PendingCharacterReward masteryReward = manager.BuildPendingCharacterReward(
            "hero",
            "mastery_reward",
            "quest",
            "mastery_source",
            "Mastery reward",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = "charge",
                    target_label = "Charge",
                    amount = 2,
                },
            },
            "mastery summary"
        );

        _test.True(typedReward != null, "typed reward fixture should be valid.");
        _test.True(masteryReward != null, "mastery reward fixture should be valid.");
        if (typedReward == null || masteryReward == null)
            return;

        manager.EnqueuePendingCharacterRewardsTyped(
            new[]
            {
                typedReward,
                masteryReward,
            }
        );

        _test.Eq(
            party.pending_character_rewards.Count,
            2,
            "pending reward boundary should accept typed reward objects."
        );
        _test.Eq(
            party.pending_character_rewards[0].reward_id,
            new StringName("typed_reward"),
            "typed pending reward should preserve reward id."
        );
        _test.Eq(
            party.pending_character_rewards[1].reward_id,
            new StringName("mastery_reward"),
            "typed mastery pending reward should preserve reward id."
        );
    }

    private void TestAttributeProgressRewardConvertsAndAccumulatesWithTypedResult()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary()
        );
        PartyMemberState member = party.GetMemberState("hero");
        UnitBaseAttributes attributes = member.progression.unit_base_attributes;
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 19);
        member.progression.SetAttributeGrowthProgressAmount(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 90);

        PendingCharacterReward reward = manager.BuildPendingCharacterReward(
            "hero",
            "agility_progress_cap",
            "skill_core_max",
            "test_ultimate_skill",
            "Test ultimate skill",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.AttributeProgress,
                    target_id = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
                    amount = 240,
                    reason_text = "cap check",
                },
            },
            "Cap check"
        );

        _test.True(reward != null, "attribute progress reward should materialize.");
        if (reward == null)
            return;

        CharacterProgressionDelta delta = manager.ApplyPendingCharacterReward(reward);
        _test.Eq(
            attributes.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)),
            20,
            "attribute progress should convert until the base attribute cap."
        );
        _test.Eq(
            ReadGrowthProgress(member.progression, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)),
            230,
            "attribute progress should keep remaining progress after reaching the cap."
        );
        _test.Eq(
            delta.AttributeChangesTyped.Count,
            1,
            "attribute progress reward should expose one attribute change."
        );
        if (delta.AttributeChangesTyped.Count == 0)
            return;

        CharacterAttributeChangeFact change = delta.AttributeChangesTyped[0];
        _test.Eq(change.AttributeId, new StringName("agility"), "attribute change should keep the target id.");
        _test.Eq(change.ProgressDelta, 240, "attribute change should expose progress delta.");
        _test.Eq(change.ProgressBefore, 90, "attribute change should expose previous progress.");
        _test.Eq(change.ProgressAfter, 230, "attribute change should expose remaining progress.");
        _test.Eq(change.Delta, 1, "attribute change should expose converted attribute delta.");
        _test.Eq(change.AttributeBefore, 19, "attribute change should expose previous attribute.");
        _test.Eq(change.AttributeAfter, 20, "attribute change should expose final attribute.");
        _test.Eq(change.ReasonText, "cap check", "attribute change should preserve reason text.");
    }

    private void TestActiveLevelTriggerSetAndClearUseTypedResult()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.GetMemberState("hero");
        SkillDef triggerSkill = new()
        {
            skill_id = "test_set_clear_trigger",
            display_name = "Test set clear trigger",
            max_level = 1,
        };
        member.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = triggerSkill.skill_id,
                is_learned = true,
                is_core = true,
                skill_level = 1,
            }
        );

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary { [triggerSkill.skill_id] = triggerSkill },
            new GDictionary(),
            new GDictionary()
        );

        LevelGrowthTriggerResult setResult = manager.SetActiveLevelTriggerCoreSkillTyped(
            "hero",
            triggerSkill.skill_id
        );
        UnitSkillProgress triggerProgress = member.progression.GetSkillProgress(triggerSkill.skill_id);
        _test.True(setResult.Ok, "set active trigger should succeed.");
        _test.Eq(
            setResult.SkillId,
            triggerSkill.skill_id,
            "set active trigger should preserve skill id in boundary result."
        );
        _test.Eq(
            setResult.PreviousActive,
            new StringName(""),
            "set active trigger should expose the previous active skill id."
        );
        _test.Eq(
            member.progression.active_level_trigger_core_skill_id,
            triggerSkill.skill_id,
            "set active trigger should update progression state."
        );
        _test.True(
            triggerProgress != null && triggerProgress.is_level_trigger_active,
            "set active trigger should mark the skill active."
        );

        LevelGrowthTriggerResult clearResult = manager.ClearActiveLevelTriggerCoreSkillTyped("hero");
        triggerProgress = member.progression.GetSkillProgress(triggerSkill.skill_id);
        _test.True(clearResult.Ok, "clear active trigger should succeed.");
        _test.Eq(
            member.progression.active_level_trigger_core_skill_id,
            new StringName(""),
            "clear active trigger should remove active skill id."
        );
        _test.True(
            triggerProgress != null && !triggerProgress.is_level_trigger_active,
            "clear active trigger should clear the skill active flag."
        );

        LevelGrowthTriggerResult missingResult = manager.SetActiveLevelTriggerCoreSkillTyped(
            "hero",
            "missing_skill"
        );
        _test.True(!missingResult.Ok, "set active trigger should fail for missing skills.");
        _test.Eq(
            missingResult.Error,
            "skill_not_learned",
            "missing trigger failure should preserve boundary error code."
        );
    }

    private void TestActiveLevelTriggerAttributeGrowthUsesTypedEntries()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.GetMemberState("hero");
        member.progression.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 2);

        SkillDef triggerSkill = new()
        {
            skill_id = "test_growth_trigger",
            display_name = "Test growth trigger",
            max_level = 1,
        };
        triggerSkill.SetAttributeGrowthProgress(
            new Dictionary<StringName, int> { ["agility"] = 60 }
        );
        member.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = triggerSkill.skill_id,
                is_learned = true,
                is_core = true,
                skill_level = 1,
            }
        );
        member.progression.active_level_trigger_core_skill_id = triggerSkill.skill_id;

        ProfessionDef profession = new()
        {
            profession_id = "test_growth_profession",
            display_name = "Test growth profession",
            is_initial_profession = true,
            max_rank = 1,
            hit_die_sides = 1,
        };

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary { [triggerSkill.skill_id] = triggerSkill },
            new GDictionary { [profession.profession_id] = profession },
            new GDictionary()
        );

        CharacterProgressionDelta delta = manager.PromoteProfession(
            "hero",
            profession.profession_id,
            PromotionSelectionData.Empty
        );
        UnitSkillProgress triggerProgress = member.progression.GetSkillProgress(triggerSkill.skill_id);

        _test.Eq(delta.changed_profession_ids.Count, 1, "active trigger promotion should rank up.");
        _test.Eq(delta.AttributeChangesTyped.Count, 1, "active trigger should apply attribute growth directly.");
        _test.Eq(
            triggerSkill.AttributeGrowthProgressTyped.Count,
            1,
            "trigger skill should keep attribute growth entries in typed backing state."
        );
        _test.Eq(
            ReadGrowthProgress(member.progression, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)),
            60,
            "active trigger should write attribute growth progress."
        );
        _test.True(
            triggerProgress != null && triggerProgress.core_max_growth_claimed,
            "active trigger should mark growth claimed after applying progress."
        );
        _test.Eq(
            party.pending_character_rewards.Count,
            0,
            "active trigger should not queue a pending character reward."
        );
        if (delta.AttributeChangesTyped.Count == 0)
            return;
        CharacterAttributeChangeFact change = delta.AttributeChangesTyped[0];
        _test.Eq(change.AttributeId, new StringName("agility"), "active trigger change should keep target id.");
        _test.Eq(change.ProgressDelta, 60, "active trigger change should expose progress delta.");
        _test.Eq(change.ProgressAfter, 60, "active trigger change should expose final progress.");
        _test.Eq(change.Delta, 0, "active trigger should not convert below threshold.");
    }

    private void TestActiveLevelTriggerAttributeGrowthRejectsInvalidEntries()
    {
        var cases = new[]
        {
            new
            {
                Label = "StringName key",
                Growth = new GDictionary { [new StringName("agility")] = 60 },
            },
            new
            {
                Label = "unknown attribute key",
                Growth = new GDictionary { ["unknown_attribute"] = 60 },
            },
            new
            {
                Label = "non-int amount",
                Growth = new GDictionary { ["agility"] = "60" },
            },
            new
            {
                Label = "non-positive amount",
                Growth = new GDictionary { ["agility"] = 0 },
            },
        };

        foreach (var testCase in cases)
        {
            PartyState party = BuildPartyWithMember("hero", 2);
            PartyMemberState member = party.GetMemberState("hero");
            member.progression.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 2);

            SkillDef triggerSkill = new()
            {
                skill_id = new StringName($"test_invalid_growth_{testCase.Label.Replace(" ", "_")}"),
                display_name = testCase.Label,
                max_level = 1,
                growth_tier = "basic",
            };
            triggerSkill.attribute_growth_progress = testCase.Growth.Duplicate(true);
            member.progression.SetSkillProgress(
                new UnitSkillProgress
                {
                    skill_id = triggerSkill.skill_id,
                    is_learned = true,
                    is_core = true,
                    skill_level = 1,
                }
            );
            member.progression.active_level_trigger_core_skill_id = triggerSkill.skill_id;

            ProfessionDef profession = new()
            {
                profession_id = new StringName(
                    $"test_invalid_growth_profession_{testCase.Label.Replace(" ", "_")}"
                ),
                display_name = "Invalid growth profession",
                is_initial_profession = true,
                max_rank = 1,
                hit_die_sides = 1,
            };

            CharacterManagementModule manager = new();
            manager.setup(
                party,
                new GDictionary { [triggerSkill.skill_id] = triggerSkill },
                new GDictionary { [profession.profession_id] = profession },
                new GDictionary()
            );

            CharacterProgressionDelta delta = manager.PromoteProfession(
                "hero",
                profession.profession_id,
                PromotionSelectionData.Empty
            );
            UnitSkillProgress triggerProgress = member.progression.GetSkillProgress(triggerSkill.skill_id);

            _test.Eq(
                delta.changed_profession_ids.Count,
                1,
                $"{testCase.Label} should not block active trigger promotion itself."
            );
            _test.Eq(
                delta.AttributeChangesTyped.Count,
                0,
                $"{testCase.Label} should not produce attribute growth changes."
            );
            _test.Eq(
                ReadGrowthProgress(member.progression, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)),
                0,
                $"{testCase.Label} should not write agility growth progress."
            );
            _test.True(
                triggerProgress != null && !triggerProgress.core_max_growth_claimed,
                $"{testCase.Label} should not mark growth claimed."
            );
        }
    }

    private void TestSkillMasteryRewardAggregatesTypedEntries()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.GetMemberState("hero");
        member.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = "charge",
                is_learned = true,
                skill_level = 1,
            }
        );
        SkillDef charge = new()
        {
            skill_id = "charge",
            display_name = "Charge",
        };
        charge.mastery_sources = new Godot.Collections.Array<StringName> { "battle" };

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary { [charge.skill_id] = charge },
            new GDictionary(),
            new GDictionary(),
            BuildItemDefs(),
            new GDictionary()
        );

        PendingCharacterReward reward = manager.BuildPendingSkillMasteryReward(
            "hero",
            "battle",
            "Battle reward",
            new[]
            {
                new PendingCharacterRewardEntry
                {
                    target_id = "charge",
                    amount = 3,
                    reason_text = "first hit",
                },
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = "charge",
                    amount = 4,
                    mastery_source_type = "battle_rating",
                },
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = "charge",
                    amount = 99,
                    mastery_source_type = "training",
                },
            },
            "Battle mastery"
        );

        _test.True(reward != null, "skill mastery reward should materialize for learned skills.");
        if (reward == null)
            return;
        _test.Eq(reward.entries.Count, 1, "skill mastery reward should aggregate entries by skill.");
        PendingCharacterRewardEntry entry = reward.entries[0];
        _test.Eq(
            entry.entry_type,
            PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillMastery),
            "skill mastery reward should produce mastery entries."
        );
        _test.Eq(entry.target_id, new StringName("charge"), "skill mastery reward should preserve skill id.");
        _test.Eq(entry.amount, 7, "skill mastery reward should aggregate only allowed battle mastery.");
        _test.Eq(entry.reason_text, "first hit", "skill mastery reward should preserve first reason text.");
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        GDictionary itemDefs,
        GDictionary questDefs
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            itemDefs,
            questDefs
        );
        return manager;
    }

    private static PartyState BuildPartyWithMember(string memberId, int storageSpace)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
        };
        member.progression.unit_base_attributes.SetAttributeValue(
            PartyWarehouseService.StorageSpaceAttributeId,
            storageSpace
        );
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static GDictionary BuildItemDefs()
    {
        ItemDef ironOre = new()
        {
            item_id = "iron_ore",
            display_name = "Iron Ore",
            CategoryKind = ItemCategoryKind.Misc,
            is_stackable = true,
        };
        ItemDef bronzeSword = new()
        {
            item_id = "bronze_sword",
            display_name = "Bronze Sword",
            CategoryKind = ItemCategoryKind.Misc,
            is_stackable = true,
        };
        return new GDictionary
        {
            [ironOre.item_id] = ironOre,
            [bronzeSword.item_id] = bronzeSword,
        };
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static QuestDef BuildSubmitItemQuest(
        string questId,
        string objectiveId,
        string itemId,
        int targetValue
    )
    {
        QuestDef quest = new()
        {
            quest_id = questId,
            display_name = questId,
        };
        quest.objective_defs.Add(
            new GDictionary
            {
                ["objective_id"] = objectiveId,
                ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                ["target_id"] = itemId,
                ["target_value"] = targetValue,
            }
        );
        return quest;
    }

    private static QuestDef BuildRewardQuest(
        string questId,
        string displayName,
        params GDictionary[] rewards
    )
    {
        QuestDef quest = new()
        {
            quest_id = questId,
            display_name = displayName,
        };
        quest.objective_defs.Add(
            new GDictionary
            {
                ["objective_id"] = "done",
                ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                ["target_id"] = "service:contract",
                ["target_value"] = 1,
            }
        );
        foreach (GDictionary reward in rewards)
            quest.reward_entries.Add(reward);
        return quest;
    }

    private static QuestState BuildClaimableQuest(
        StringName questId,
        int acceptedStep,
        int completedStep
    )
    {
        QuestState questState = new() { quest_id = questId };
        questState.MarkAccepted(acceptedStep);
        questState.MarkCompleted(completedStep);
        return questState;
    }

    private static int ExtractItemRewardQuantity(GArray itemRewards, string itemId)
    {
        foreach (Variant rewardValue in itemRewards)
        {
            if (rewardValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary reward = rewardValue.AsGodotDictionary();
            if (ReadString(reward, "item_id") == itemId)
                return ReadInt(reward, "quantity");
        }
        return 0;
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GArray();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int ReadGrowthProgress(UnitProgress progress, StringName attributeId)
    {
        return progress != null
            && progress.TryGetAttributeGrowthProgressAmount(attributeId, out int amount)
            ? amount
            : 0;
    }


}

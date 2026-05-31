using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_quest_materializer_regression : SceneTree
{
    private readonly List<string> _failures = new();

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
        TestPendingCharacterRewardBoundaryAcceptsTypedAndDictionaryRewards();
        TestAttributeProgressRewardConvertsAndAccumulatesWithTypedResult();
        TestActiveLevelTriggerSetAndClearUseTypedResult();
        TestActiveLevelTriggerAttributeGrowthUsesTypedEntries();
        TestSkillMasteryRewardAggregatesTypedEntries();
        TestStringKeyOnlyQuestRewardDefIsRejected();

        if (_failures.Count == 0)
        {
            GD.Print("Character management quest materializer regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Character management quest materializer regression: FAIL ({_failures.Count})");
        Quit(1);
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
                ["objective_type"] = QuestDef.OBJECTIVE_SUBMIT_ITEM(),
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
        warehouse.setup(party, itemDefs);

        QuestState partialQuest = new() { quest_id = submitQuest.quest_id };
        partialQuest.mark_accepted(3);
        partialQuest.record_objective_progress(
            "deliver_ore",
            1,
            2,
            new GDictionary { ["item_id"] = "iron_ore", ["submitted_quantity"] = 1 }
        );
        party.set_active_quest_state(partialQuest);
        warehouse.add_item("iron_ore", 1);

        GDictionary partialResult = manager.submit_item_objective(
            submitQuest.quest_id,
            "deliver_ore",
            4
        );
        AssertTrue(ReadBool(partialResult, "ok"), "submit_item should complete a partially progressed objective.");
        AssertEq(ReadInt(partialResult, "submitted_quantity"), 1, "submit_item should only withdraw the remaining quantity.");
        AssertTrue(!party.has_active_quest(submitQuest.quest_id), "successful submit_item should leave active quests.");
        AssertTrue(party.has_claimable_quest(submitQuest.quest_id), "successful submit_item should enter claimable quests.");
        AssertEq(warehouse.count_item("iron_ore"), 0, "successful submit_item should consume the submitted item.");
        QuestState claimableSubmitQuest = party.get_claimable_quest_state(submitQuest.quest_id);
        AssertTrue(claimableSubmitQuest != null, "successful submit_item should preserve quest state.");
        if (claimableSubmitQuest != null)
        {
            AssertEq(
                claimableSubmitQuest.get_objective_progress("deliver_ore"),
                2,
                "successful submit_item should fill objective progress to target."
            );
            AssertEq(
                ReadInt(claimableSubmitQuest.last_progress_context, "submitted_quantity"),
                1,
                "successful submit_item should record submitted quantity context."
            );
            AssertEq(
                ReadString(claimableSubmitQuest.last_progress_context, "item_id"),
                "iron_ore",
                "successful submit_item should record submitted item context."
            );
            AssertEq(
                claimableSubmitQuest.completed_at_world_step,
                4,
                "successful submit_item should record completion world step."
            );
        }

        QuestState shortageState = new() { quest_id = shortageQuest.quest_id };
        shortageState.mark_accepted(5);
        party.set_active_quest_state(shortageState);
        GDictionary shortageResult = manager.submit_item_objective(
            shortageQuest.quest_id,
            "deliver_ore",
            6
        );
        AssertTrue(!ReadBool(shortageResult, "ok"), "submit_item should fail when inventory is short.");
        AssertEq(
            ReadString(shortageResult, "error_code"),
            "submit_item_missing_inventory",
            "submit_item shortage should return the formal shortage error."
        );
        AssertTrue(party.has_active_quest(shortageQuest.quest_id), "shortage should keep quest active.");
        AssertTrue(!party.has_claimable_quest(shortageQuest.quest_id), "shortage should not mark quest claimable.");

        QuestState wrongItemState = new() { quest_id = wrongItemQuest.quest_id };
        wrongItemState.mark_accepted(7);
        party.set_active_quest_state(wrongItemState);
        warehouse.add_item("bronze_sword", 1);
        int bronzeBefore = warehouse.count_item("bronze_sword");
        GDictionary wrongItemResult = manager.submit_item_objective(
            wrongItemQuest.quest_id,
            "deliver_ore",
            8
        );
        AssertTrue(!ReadBool(wrongItemResult, "ok"), "submit_item should fail when only the wrong item exists.");
        AssertEq(
            ReadString(wrongItemResult, "error_code"),
            "submit_item_missing_inventory",
            "wrong item submit should still report missing target inventory."
        );
        AssertEq(
            warehouse.count_item("bronze_sword"),
            bronzeBefore,
            "wrong item submit should not consume other inventory."
        );

        QuestState missingTargetState = new() { quest_id = missingTargetQuest.quest_id };
        missingTargetState.mark_accepted(9);
        party.set_active_quest_state(missingTargetState);
        GDictionary missingTargetResult = manager.submit_item_objective(
            missingTargetQuest.quest_id,
            "deliver_ore",
            10
        );
        AssertTrue(!ReadBool(missingTargetResult, "ok"), "submit_item should reject objectives without target_value.");
        AssertEq(
            ReadString(missingTargetResult, "error_code"),
            "invalid_submit_item_objective",
            "missing target_value should not default to one."
        );
        AssertTrue(
            party.has_active_quest(missingTargetQuest.quest_id),
            "missing target_value should keep quest active."
        );
    }

    private void TestQuestRewardMaterializesGoldItemsAndOverflow()
    {
        GDictionary itemDefs = BuildItemDefs();
        QuestDef rewardQuest = BuildRewardQuest(
            "contract_supply_receipt",
            "Supply receipt",
            new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 12 },
            new GDictionary
            {
                ["reward_type"] = QuestDef.REWARD_ITEM(),
                ["item_id"] = "iron_ore",
                ["quantity"] = 2,
            }
        );
        QuestDef overflowQuest = BuildRewardQuest(
            "contract_reward_overflow",
            "Overflow",
            new GDictionary
            {
                ["reward_type"] = QuestDef.REWARD_ITEM(),
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
        warehouse.setup(party, itemDefs);
        party.set_claimable_quest_state(BuildClaimableQuest("contract_supply_receipt", 4, 6));

        GDictionary claimResult = manager.claim_quest_reward("contract_supply_receipt", 8);
        AssertTrue(ReadBool(claimResult, "ok"), "quest reward should claim successfully.");
        AssertEq(ReadInt(claimResult, "gold_delta"), 12, "quest reward should expose gold delta.");
        AssertEq(
            ExtractItemRewardQuantity(ReadArray(claimResult, "item_rewards"), "iron_ore"),
            2,
            "quest reward should expose materialized item rewards."
        );
        AssertEq(warehouse.count_item("iron_ore"), 2, "quest reward should deposit item rewards.");
        AssertEq(party.get_gold(), 12, "quest reward should add gold to party state.");
        AssertTrue(!party.has_claimable_quest("contract_supply_receipt"), "claimed quest should leave claimable quests.");
        AssertTrue(party.has_completed_quest("contract_supply_receipt"), "claimed quest should enter completed quests.");

        PartyState overflowParty = BuildPartyWithMember("porter", 1);
        PartyWarehouseService overflowWarehouse = new();
        overflowWarehouse.setup(overflowParty, itemDefs);
        overflowWarehouse.add_item("bronze_sword", 1);
        CharacterManagementModule overflowManager = BuildManager(
            overflowParty,
            itemDefs,
            new GDictionary { [overflowQuest.quest_id] = overflowQuest }
        );
        overflowParty.set_claimable_quest_state(BuildClaimableQuest("contract_reward_overflow", 5, 7));

        GDictionary overflowResult = overflowManager.claim_quest_reward(
            "contract_reward_overflow",
            9
        );
        AssertTrue(!ReadBool(overflowResult, "ok"), "quest reward should fail when warehouse is full.");
        AssertEq(
            ReadString(overflowResult, "error_code"),
            "reward_overflow",
            "warehouse overflow should map to reward_overflow."
        );
        AssertTrue(
            overflowParty.has_claimable_quest("contract_reward_overflow"),
            "overflow should keep quest claimable."
        );
        AssertTrue(
            !overflowParty.has_completed_quest("contract_reward_overflow"),
            "overflow should not complete quest reward."
        );
        AssertEq(
            overflowWarehouse.count_item("bronze_sword"),
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
                ["reward_type"] = QuestDef.REWARD_PENDING_CHARACTER_REWARD(),
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
        party.set_claimable_quest_state(BuildClaimableQuest("contract_growth_drill", 6, 9));

        GDictionary claimResult = manager.claim_quest_reward("contract_growth_drill", 12);
        AssertTrue(ReadBool(claimResult, "ok"), "pending character quest reward should claim.");
        AssertEq(
            ReadArray(claimResult, "pending_character_rewards").Count,
            1,
            "claim result should expose materialized pending character reward."
        );
        AssertEq(
            party.pending_character_rewards.Count,
            1,
            "pending character reward should enter party queue."
        );
        PendingCharacterReward queuedReward = party.get_next_pending_character_reward();
        AssertTrue(queuedReward != null, "queued pending character reward should be readable.");
        if (queuedReward != null)
        {
            AssertEq(queuedReward.member_id, new StringName("hero"), "pending reward should preserve member id.");
            AssertEq(queuedReward.source_id, quest.quest_id, "pending reward should default source id to quest id.");
            AssertEq(queuedReward.source_label, "Growth drill", "pending reward should default source label to quest name.");
            AssertEq(queuedReward.entries.Count, 2, "pending reward should preserve entries.");
        }
    }

    private void TestStringKeyOnlyQuestRewardDefIsRejected()
    {
        QuestDef quest = BuildRewardQuest(
            "contract_string_key_reward",
            "String key reward",
            new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 1 }
        );
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary { [quest.quest_id.ToString()] = quest }
        );
        party.set_claimable_quest_state(BuildClaimableQuest("contract_string_key_reward", 1, 2));

        GDictionary claimResult = manager.claim_quest_reward("contract_string_key_reward", 3);
        AssertTrue(!ReadBool(claimResult, "ok"), "String-key-only quest def should be rejected.");
        AssertEq(
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

        PendingCharacterReward reward = manager.build_pending_character_reward(
            "hero",
            "",
            "quest",
            "invalid_attribute_reward",
            "Invalid attribute reward",
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = PendingCharacterRewardContentRules.ENTRY_ATTRIBUTE_PROGRESS,
                    ["target_id"] = "not_an_attribute",
                    ["amount"] = 1,
                },
            },
            "Invalid target should be rejected."
        );

        AssertTrue(
            reward == null,
            "pending character reward should reject attribute_progress entries with invalid targets."
        );
    }

    private void TestPendingCharacterRewardBoundaryAcceptsTypedAndDictionaryRewards()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        CharacterManagementModule manager = BuildManager(
            party,
            BuildItemDefs(),
            new GDictionary()
        );

        PendingCharacterReward typedReward = manager.build_pending_character_reward(
            "hero",
            "typed_reward",
            "quest",
            "typed_source",
            "Typed reward",
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "attribute_delta",
                    ["target_id"] = "strength",
                    ["target_label"] = "Strength",
                    ["amount"] = 1,
                },
            },
            "typed summary"
        );
        PendingCharacterReward dictionaryReward = manager.build_pending_character_reward(
            "hero",
            "dictionary_reward",
            "quest",
            "dictionary_source",
            "Dictionary reward",
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "skill_mastery",
                    ["target_id"] = "charge",
                    ["target_label"] = "Charge",
                    ["amount"] = 2,
                },
            },
            "dictionary summary"
        );

        AssertTrue(typedReward != null, "typed reward fixture should be valid.");
        AssertTrue(dictionaryReward != null, "dictionary reward fixture should be valid.");
        if (typedReward == null || dictionaryReward == null)
            return;

        manager.enqueue_pending_character_rewards(
            new GArray
            {
                typedReward,
                dictionaryReward.to_dict(),
            }
        );

        AssertEq(
            party.pending_character_rewards.Count,
            2,
            "pending reward boundary should accept typed objects and canonical dictionaries."
        );
        AssertEq(
            party.pending_character_rewards[0].reward_id,
            new StringName("typed_reward"),
            "typed pending reward should preserve reward id."
        );
        AssertEq(
            party.pending_character_rewards[1].reward_id,
            new StringName("dictionary_reward"),
            "dictionary pending reward should preserve reward id."
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
        PartyMemberState member = party.get_member_state("hero");
        UnitBaseAttributes attributes = member.progression.unit_base_attributes;
        attributes.set_attribute_value(UnitBaseAttributes.AGILITY(), 19);
        member.progression.attribute_growth_progress[UnitBaseAttributes.AGILITY()] = 90;

        PendingCharacterReward reward = manager.build_pending_character_reward(
            "hero",
            "agility_progress_cap",
            "skill_core_max",
            "test_ultimate_skill",
            "Test ultimate skill",
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = PendingCharacterRewardContentRules.ENTRY_ATTRIBUTE_PROGRESS,
                    ["target_id"] = UnitBaseAttributes.AGILITY(),
                    ["amount"] = 240,
                    ["reason_text"] = "cap check",
                },
            },
            "Cap check"
        );

        AssertTrue(reward != null, "attribute progress reward should materialize.");
        if (reward == null)
            return;

        CharacterProgressionDelta delta = manager.apply_pending_character_reward(reward);
        AssertEq(
            attributes.get_attribute_value(UnitBaseAttributes.AGILITY()),
            20,
            "attribute progress should convert until the base attribute cap."
        );
        AssertEq(
            ReadGrowthProgress(member.progression, UnitBaseAttributes.AGILITY()),
            230,
            "attribute progress should keep remaining progress after reaching the cap."
        );
        AssertEq(
            delta.attribute_changes.Count,
            1,
            "attribute progress reward should expose one attribute change."
        );
        if (delta.attribute_changes.Count == 0)
            return;

        GDictionary change = delta.attribute_changes[0];
        AssertEq(ReadString(change, "attribute_id"), "agility", "attribute change should keep the target id.");
        AssertEq(ReadInt(change, "progress_delta"), 240, "attribute change should expose progress delta.");
        AssertEq(ReadInt(change, "progress_before"), 90, "attribute change should expose previous progress.");
        AssertEq(ReadInt(change, "progress_after"), 230, "attribute change should expose remaining progress.");
        AssertEq(ReadInt(change, "delta"), 1, "attribute change should expose converted attribute delta.");
        AssertEq(ReadInt(change, "attribute_before"), 19, "attribute change should expose previous attribute.");
        AssertEq(ReadInt(change, "attribute_after"), 20, "attribute change should expose final attribute.");
        AssertEq(ReadString(change, "reason_text"), "cap check", "attribute change should preserve reason text.");
    }

    private void TestActiveLevelTriggerSetAndClearUseTypedResult()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.get_member_state("hero");
        SkillDef triggerSkill = new()
        {
            skill_id = "test_set_clear_trigger",
            display_name = "Test set clear trigger",
            max_level = 1,
        };
        member.progression.set_skill_progress(
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

        GDictionary setResult = manager.set_active_level_trigger_core_skill(
            "hero",
            triggerSkill.skill_id
        );
        UnitSkillProgress triggerProgress = member.progression.get_skill_progress(triggerSkill.skill_id);
        AssertTrue(ReadBool(setResult, "ok"), "set active trigger should succeed.");
        AssertEq(
            ReadString(setResult, "skill_id"),
            "test_set_clear_trigger",
            "set active trigger should preserve skill id in boundary result."
        );
        AssertEq(
            ReadString(setResult, "previous_active"),
            "",
            "set active trigger should expose the previous active skill id."
        );
        AssertEq(
            member.progression.active_level_trigger_core_skill_id,
            triggerSkill.skill_id,
            "set active trigger should update progression state."
        );
        AssertTrue(
            triggerProgress != null && triggerProgress.is_level_trigger_active,
            "set active trigger should mark the skill active."
        );

        GDictionary clearResult = manager.clear_active_level_trigger_core_skill("hero");
        triggerProgress = member.progression.get_skill_progress(triggerSkill.skill_id);
        AssertTrue(ReadBool(clearResult, "ok"), "clear active trigger should succeed.");
        AssertEq(
            member.progression.active_level_trigger_core_skill_id,
            new StringName(""),
            "clear active trigger should remove active skill id."
        );
        AssertTrue(
            triggerProgress != null && !triggerProgress.is_level_trigger_active,
            "clear active trigger should clear the skill active flag."
        );

        GDictionary missingResult = manager.set_active_level_trigger_core_skill(
            "hero",
            "missing_skill"
        );
        AssertTrue(!ReadBool(missingResult, "ok"), "set active trigger should fail for missing skills.");
        AssertEq(
            ReadString(missingResult, "error"),
            "skill_not_learned",
            "missing trigger failure should preserve boundary error code."
        );
    }

    private void TestActiveLevelTriggerAttributeGrowthUsesTypedEntries()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.get_member_state("hero");
        member.progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.AGILITY(), 2);

        SkillDef triggerSkill = new()
        {
            skill_id = "test_growth_trigger",
            display_name = "Test growth trigger",
            max_level = 1,
        };
        triggerSkill.attribute_growth_progress = new GDictionary
        {
            ["agility"] = 60,
        };
        member.progression.set_skill_progress(
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

        CharacterProgressionDelta delta = manager.promote_profession(
            "hero",
            profession.profession_id,
            new GDictionary { [ProgressionService.SELECTION_KEY_HP_ROLL_OVERRIDE_ID()] = 1 }
        );
        UnitSkillProgress triggerProgress = member.progression.get_skill_progress(triggerSkill.skill_id);

        AssertEq(delta.changed_profession_ids.Count, 1, "active trigger promotion should rank up.");
        AssertEq(delta.attribute_changes.Count, 1, "active trigger should apply attribute growth directly.");
        AssertEq(
            ReadGrowthProgress(member.progression, UnitBaseAttributes.AGILITY()),
            60,
            "active trigger should write attribute growth progress."
        );
        AssertTrue(
            triggerProgress != null && triggerProgress.core_max_growth_claimed,
            "active trigger should mark growth claimed after applying progress."
        );
        AssertEq(
            party.pending_character_rewards.Count,
            0,
            "active trigger should not queue a pending character reward."
        );
        if (delta.attribute_changes.Count == 0)
            return;
        GDictionary change = delta.attribute_changes[0];
        AssertEq(ReadString(change, "attribute_id"), "agility", "active trigger change should keep target id.");
        AssertEq(ReadInt(change, "progress_delta"), 60, "active trigger change should expose progress delta.");
        AssertEq(ReadInt(change, "progress_after"), 60, "active trigger change should expose final progress.");
        AssertEq(ReadInt(change, "delta"), 0, "active trigger should not convert below threshold.");
    }

    private void TestSkillMasteryRewardAggregatesTypedEntries()
    {
        PartyState party = BuildPartyWithMember("hero", 2);
        PartyMemberState member = party.get_member_state("hero");
        member.progression.set_skill_progress(
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
        charge.mastery_sources.Add("battle");

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary { [charge.skill_id] = charge },
            new GDictionary(),
            new GDictionary(),
            BuildItemDefs(),
            new GDictionary()
        );

        PendingCharacterReward reward = manager.build_pending_skill_mastery_reward(
            "hero",
            "battle",
            "Battle reward",
            new GArray
            {
                new GDictionary
                {
                    ["target_id"] = "charge",
                    ["amount"] = 3,
                    ["source_type"] = "battle",
                    ["reason_text"] = "first hit",
                },
                new GDictionary
                {
                    ["entry_type"] = PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY,
                    ["target_id"] = "charge",
                    ["amount"] = 4,
                    ["mastery_source_type"] = "battle_rating",
                },
                new GDictionary
                {
                    ["entry_type"] = PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY,
                    ["target_id"] = "charge",
                    ["amount"] = 99,
                    ["mastery_source_type"] = "training",
                },
            },
            "Battle mastery"
        );

        AssertTrue(reward != null, "skill mastery reward should materialize for learned skills.");
        if (reward == null)
            return;
        AssertEq(reward.entries.Count, 1, "skill mastery reward should aggregate entries by skill.");
        PendingCharacterRewardEntry entry = reward.entries[0];
        AssertEq(
            entry.entry_type,
            PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY,
            "skill mastery reward should produce mastery entries."
        );
        AssertEq(entry.target_id, new StringName("charge"), "skill mastery reward should preserve skill id.");
        AssertEq(entry.amount, 7, "skill mastery reward should aggregate only allowed battle mastery.");
        AssertEq(entry.reason_text, "first hit", "skill mastery reward should preserve first reason text.");
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
        member.progression.unit_base_attributes.set_attribute_value(
            PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(),
            storageSpace
        );
        party.set_member_state(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static GDictionary BuildItemDefs()
    {
        ItemDef ironOre = new()
        {
            item_id = "iron_ore",
            display_name = "Iron Ore",
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
            is_stackable = true,
        };
        ItemDef bronzeSword = new()
        {
            item_id = "bronze_sword",
            display_name = "Bronze Sword",
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
            is_stackable = true,
        };
        return new GDictionary
        {
            [ironOre.item_id] = ironOre,
            [bronzeSword.item_id] = bronzeSword,
        };
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
                ["objective_type"] = QuestDef.OBJECTIVE_SUBMIT_ITEM(),
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
                ["objective_type"] = QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
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
        questState.mark_accepted(acceptedStep);
        questState.mark_completed(completedStep);
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
        if (progress == null || !progress.attribute_growth_progress.ContainsKey(attributeId))
            return 0;
        Variant value = progress.attribute_growth_progress[attributeId];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }
}

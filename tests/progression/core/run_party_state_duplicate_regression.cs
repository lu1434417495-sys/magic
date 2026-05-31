using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_state_duplicate_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestDuplicateStateDeepCopiesBattleWritebackState();

        if (_failures.Count == 0)
        {
            GD.Print("Party state duplicate regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Party state duplicate regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestDuplicateStateDeepCopiesBattleWritebackState()
    {
        PartyState source = BuildPartyState();
        PartyState copy = source.duplicate_state();

        AssertTrue(copy != null && !ReferenceEquals(copy, source), "duplicate_state 应创建新的 PartyState。");
        AssertTrue(
            !ReferenceEquals(copy.warehouse_state, source.warehouse_state),
            "warehouse_state 不应共享引用。"
        );

        PartyMemberState sourceHero = source.get_member_state("hero");
        PartyMemberState copyHero = copy.get_member_state("hero");
        AssertTrue(copyHero != null && !ReferenceEquals(copyHero, sourceHero), "member_state 应深拷贝。");
        AssertTrue(
            !ReferenceEquals(copyHero.progression, sourceHero.progression),
            "progression 应深拷贝。"
        );
        AssertTrue(
            !ReferenceEquals(copyHero.equipment_state, sourceHero.equipment_state),
            "equipment_state 应深拷贝。"
        );

        copy.gold = 99;
        copy.warehouse_state.stacks[0].quantity = 1;
        copyHero.progression.get_skill_progress("slash").current_mastery = 77;
        copyHero.progression.unit_base_attributes.strength = 42;
        copyHero
            .progression
            .get_profession_progress("warrior")
            .promotion_history[0]
            .snapshot_unit_base_attributes["strength"] = 66;
        copyHero.progression.pending_profession_choices[0].target_rank_map["warrior"] = 5;
        copyHero
            .equipment_state
            .get_equipped_instance(EquipmentRules.MAIN_HAND())
            .current_durability = 2;
        copy.active_quests[0].objective_progress["kill"] = 9;
        copy.pending_character_rewards[0].entries[0].amount = 30;

        AssertEq(source.gold, 15, "修改 copy.gold 不应影响源队伍。");
        AssertEq(source.warehouse_state.stacks[0].quantity, 3, "修改 copy 仓库堆叠不应影响源队伍。");
        AssertEq(
            sourceHero.progression.get_skill_progress("slash").current_mastery,
            5,
            "修改 copy 技能进度不应影响源队伍。"
        );
        AssertEq(
            sourceHero.progression.unit_base_attributes.strength,
            8,
            "修改 copy 基础属性不应影响源队伍。"
        );
        AssertEq(
            sourceHero
                .progression
                .get_profession_progress("warrior")
                .promotion_history[0]
                .snapshot_unit_base_attributes["strength"]
                .AsInt32(),
            8,
            "修改 copy 晋升快照不应影响源队伍。"
        );
        AssertEq(
            sourceHero.progression.pending_profession_choices[0].target_rank_map["warrior"].AsInt32(),
            2,
            "修改 copy 待转职选项不应影响源队伍。"
        );
        AssertEq(
            sourceHero
                .equipment_state
                .get_equipped_instance(EquipmentRules.MAIN_HAND())
                .current_durability,
            7,
            "修改 copy 装备实例不应影响源队伍。"
        );
        AssertEq(
            source.active_quests[0].objective_progress["kill"].AsInt32(),
            1,
            "修改 copy 任务进度不应影响源队伍。"
        );
        AssertEq(
            source.pending_character_rewards[0].entries[0].amount,
            12,
            "修改 copy 待领奖励不应影响源队伍。"
        );
        AssertTrue(
            copyHero.progression.active_core_skill_ids.Contains("slash"),
            "duplicate_state 应保留 UnitProgress.to_dict 旧路径会同步的 active_core_skill_ids。"
        );
        AssertTrue(
            copyHero.progression.unlocked_combat_resource_ids.Contains(UnitProgress.COMBAT_RESOURCE_STAMINA()),
            "duplicate_state 应保留 UnitProgress.to_dict 旧路径会补齐的默认战斗资源。"
        );
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            gold = 15,
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            warehouse_state = new WarehouseState
            {
                stacks = new Godot.Collections.Array<WarehouseStackState>
                {
                    new() { item_id = "potion", quantity = 3 },
                },
                equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
                {
                    EquipmentInstanceState.create("spare_sword", "eq_000002"),
                },
            },
        };
        partyState.set_fate_run_flag("omen_seen", true);
        partyState.set_meta_flag("visited_town", true);
        partyState.set_member_state(BuildMemberState());
        partyState.active_quests.Add(
            new QuestState
            {
                quest_id = "hunt",
                status_id = QuestState.STATUS_ACTIVE,
                accepted_at_world_step = 1,
                objective_progress = new GDictionary { ["kill"] = 1 },
            }
        );
        partyState.pending_character_rewards.Add(BuildPendingReward());
        return partyState;
    }

    private static PartyMemberState BuildMemberState()
    {
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = BuildUnitProgress(),
            equipment_state = new EquipmentState(),
        };
        member.equipment_state.set_equipped_entry(
            EquipmentRules.MAIN_HAND(),
            "iron_sword",
            new GStringNameArray { EquipmentRules.MAIN_HAND() },
            new EquipmentInstanceState
            {
                instance_id = "eq_000001",
                item_id = "iron_sword",
                current_durability = 7,
            }
        );
        return member;
    }

    private static UnitProgress BuildUnitProgress()
    {
        UnitProgress progress = new()
        {
            unit_id = "hero",
            display_name = "Hero",
            character_level = 2,
            unit_base_attributes = new UnitBaseAttributes { strength = 8 },
            unlocked_combat_resource_ids = new GStringNameArray { UnitProgress.COMBAT_RESOURCE_HP() },
        };
        progress.set_skill_progress(
            new UnitSkillProgress
            {
                skill_id = "slash",
                is_learned = true,
                is_core = true,
                skill_level = 1,
                current_mastery = 5,
                total_mastery_earned = 5,
            }
        );
        UnitProfessionProgress professionProgress = new()
        {
            profession_id = "warrior",
            rank = 1,
        };
        professionProgress.add_promotion_record(
            new ProfessionPromotionRecord
            {
                new_rank = 1,
                consumed_skill_ids = new GStringNameArray { "slash" },
                qualifier_skill_ids = new GStringNameArray { "slash" },
                snapshot_unit_base_attributes = new GDictionary { ["strength"] = 8 },
                timestamp = 1,
            }
        );
        progress.set_profession_progress(professionProgress);
        progress.pending_profession_choices.Add(
            new PendingProfessionChoice
            {
                trigger_skill_ids = new GStringNameArray { "slash" },
                candidate_profession_ids = new GStringNameArray { "warrior" },
                target_rank_map = new GDictionary { ["warrior"] = 2 },
                required_qualifier_count = 1,
            }
        );
        return progress;
    }

    private static PendingCharacterReward BuildPendingReward()
    {
        PendingCharacterReward reward = new()
        {
            reward_id = "reward_1",
            member_id = "hero",
            member_name = "Hero",
            source_type = "quest",
            source_id = "hunt",
            summary_text = "Reward",
        };
        reward.entries.Add(
            new PendingCharacterRewardEntry
            {
                entry_type = PendingCharacterRewardEntry.SKILL_MASTERY_ENTRY_TYPE,
                target_id = "slash",
                target_label = "Slash",
                amount = 12,
            }
        );
        return reward;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
    }
}

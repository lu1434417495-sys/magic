using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_state_duplicate_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestDuplicateStateDeepCopiesBattleWritebackState();
        }
        finally
        {
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Party state duplicate regression"));
    }

    private void TestDuplicateStateDeepCopiesBattleWritebackState()
    {
        PartyState source = BuildPartyState();
        PartyState copy = null;
        try
        {
            copy = source.DuplicateState();

            _test.True(copy != null && !ReferenceEquals(copy, source), "duplicate_state 应创建新的 PartyState。");
            _test.True(
                !ReferenceEquals(copy.warehouse_state, source.warehouse_state),
                "warehouse_state 不应共享引用。"
            );

            PartyMemberState sourceHero = source.GetMemberState("hero");
            PartyMemberState copyHero = copy.GetMemberState("hero");
            _test.True(copyHero != null && !ReferenceEquals(copyHero, sourceHero), "member_state 应深拷贝。");
            _test.True(
                !ReferenceEquals(copyHero.progression, sourceHero.progression),
                "progression 应深拷贝。"
            );
            _test.True(
                !ReferenceEquals(copyHero.equipment_state, sourceHero.equipment_state),
                "equipment_state 应深拷贝。"
            );

            copy.gold = 99;
            copy.warehouse_state.stacks[0].quantity = 1;
            copyHero.progression.GetSkillProgress("slash").current_mastery = 77;
            copyHero.progression.unit_base_attributes.strength = 42;
            copyHero
                .progression
                .GetProfessionProgress("warrior")
                .promotion_history[0]
                .snapshot_unit_base_attributes.strength = 66;
            copyHero.progression.PendingProfessionChoicesTyped[0].SetTargetRank("warrior", 5);
            copyHero
                .equipment_state
                .GetEquippedInstance(EquipmentRules.ToStringName(EquipmentSlotKind.MainHand))
                .current_durability = 2;
            copyHero.trait_instances[0].SetIntRoll("amount", 7);
            copy.warehouse_state.equipment_instances[0].trait_instances[0].SetIntRoll("amount", 9);
            copy.active_quests[0].objective_progress["kill"] = 9;
            copy.pending_character_rewards[0].entries[0].amount = 30;

            _test.Eq(source.gold, 15, "修改 copy.gold 不应影响源队伍。");
            _test.Eq(source.warehouse_state.stacks[0].quantity, 3, "修改 copy 仓库堆叠不应影响源队伍。");
            _test.Eq(
                sourceHero.progression.GetSkillProgress("slash").current_mastery,
                5,
                "修改 copy 技能进度不应影响源队伍。"
            );
            _test.Eq(
                sourceHero.progression.unit_base_attributes.strength,
                8,
                "修改 copy 基础属性不应影响源队伍。"
            );
            _test.Eq(
                sourceHero
                    .progression
                    .GetProfessionProgress("warrior")
                    .promotion_history[0]
                    .snapshot_unit_base_attributes.strength,
                8,
                "修改 copy 晋升快照不应影响源队伍。"
            );
            _test.Eq(
                ReadTargetRank(sourceHero.progression.PendingProfessionChoicesTyped[0], "warrior"),
                2,
                "修改 copy 待转职选项不应影响源队伍。"
            );
            _test.Eq(
                sourceHero
                    .equipment_state
                    .GetEquippedInstance(EquipmentRules.ToStringName(EquipmentSlotKind.MainHand))
                    .current_durability,
                7,
                "修改 copy 装备实例不应影响源队伍。"
            );
            _test.Eq(
                sourceHero.trait_instances[0].GetIntRoll("amount", -1),
                2,
                "修改 copy 人物 trait_instances 不应影响源队伍。"
            );
            _test.Eq(
                source.warehouse_state.equipment_instances[0].trait_instances[0].GetIntRoll(
                    "amount",
                    -1
                ),
                4,
                "修改 copy 仓库装备 trait_instances 不应影响源队伍。"
            );
            _test.Eq(
                source.active_quests[0].objective_progress["kill"].AsInt32(),
                1,
                "修改 copy 任务进度不应影响源队伍。"
            );
            _test.Eq(
                source.pending_character_rewards[0].entries[0].amount,
                12,
                "修改 copy 待领奖励不应影响源队伍。"
            );
            _test.True(
                HasStringName(copyHero.progression.ActiveCoreSkillIdsTyped, "slash"),
                "duplicate_state 应保留 UnitProgress.to_dict 旧路径会同步的 active_core_skill_ids。"
            );
            _test.True(
                HasStringName(
                    copyHero.progression.UnlockedCombatResourceIdsTyped,
                    CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina)
                ),
                "duplicate_state 应保留 UnitProgress.to_dict 旧路径会补齐的默认战斗资源。"
            );
        }
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(copy);
            GodotRefCountedDisposer.DisposeIfValid(source);
        }
    }

    private static PartyState BuildPartyState()
    {
        EquipmentInstanceState spare = EquipmentInstanceState.CreateInstance(
            "spare_sword",
            "eq_000002"
        );
        spare.trait_instances.Add(
            TraitInstanceState.Create(
                "eq_000002_t01",
                "sharp_edge",
                TraitSourceKind.EquipmentRoll,
                "eq_000002",
                rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 4))
            )
        );
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
                    spare,
                },
            },
        };
        partyState.SetFateRunFlag("omen_seen", true);
        partyState.SetMetaFlag("visited_town", true);
        partyState.SetMemberState(BuildMemberState());
        partyState.active_quests.Add(
            new QuestState
            {
                quest_id = "hunt",
                status_id = QuestState.ToStringName(QuestStatusKind.Active),
                accepted_at_world_step = 1,
            }
        );
        partyState.active_quests[0].objective_progress.Set("kill", 1);
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
        member.equipment_state.SetEquippedEntry(
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            "iron_sword",
            new GStringNameArray { EquipmentRules.ToStringName(EquipmentSlotKind.MainHand) },
            new EquipmentInstanceState
            {
                instance_id = "eq_000001",
                item_id = "iron_sword",
                current_durability = 7,
            }
        );
        member.trait_instances.Add(
            TraitInstanceState.Create(
                "char_trait_001",
                "battle_hardened",
                TraitSourceKind.Character,
                "reward_intro",
                rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 2))
            )
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
            unlocked_combat_resource_ids = new GStringNameArray { CombatResourceIds.ToStringName(CombatResourceIdKind.Hp) },
        };
        progress.SetSkillProgress(
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
        professionProgress.AddPromotionRecord(
            new ProfessionPromotionRecord
            {
                new_rank = 1,
                consumed_skill_ids = new GStringNameArray { "slash" },
                qualifier_skill_ids = new GStringNameArray { "slash" },
                snapshot_unit_base_attributes = new UnitBaseAttributes { strength = 8 },
                timestamp = 1,
            }
        );
        progress.SetProfessionProgress(professionProgress);
        progress.AddPendingProfessionChoice(BuildPendingProfessionChoice());
        return progress;
    }

    private static PendingProfessionChoice BuildPendingProfessionChoice()
    {
        PendingProfessionChoice choice = new()
        {
            trigger_skill_ids = new GStringNameArray { "slash" },
            candidate_profession_ids = new GStringNameArray { "warrior" },
            required_qualifier_count = 1,
        };
        choice.SetTargetRank("warrior", 2);
        return choice;
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
                EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                target_id = "slash",
                target_label = "Slash",
                amount = 12,
            }
        );
        return reward;
    }



    private static int ReadTargetRank(PendingProfessionChoice choice, StringName professionId)
    {
        return choice != null && choice.TryGetTargetRank(professionId, out int targetRank)
            ? targetRank
            : 0;
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
            if (value == target)
                return true;
        return false;
    }
}

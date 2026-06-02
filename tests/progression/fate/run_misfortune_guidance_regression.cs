using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_misfortune_guidance_regression : SceneTree
{
    private static readonly StringName HeroId = "hero";
    private static readonly StringName MisfortuneDeityId = "misfortune_black_crown";
    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusDoomSentenceVerdict = "doom_sentence_verdict";
    private static readonly StringName GuidanceTrueId = "misfortune_guidance_true";
    private static readonly StringName GuidanceDevoutId = "misfortune_guidance_devout";
    private static readonly StringName GuidanceExaltedId = "misfortune_guidance_exalted";
    private static readonly StringName GuidanceBlessedId = "misfortune_guidance_blessed";
    private static readonly StringName ShadowHalberdId = "shadow_halberd";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestMisfortuneGuidanceUnlockChainFeedsRank2To5();
        TestForgeResultRejectsLegacyOkSuccessAlias();
        TestForgeResultRejectsStringKeyOnlyDarkEquipmentDef();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("Misfortune guidance regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Misfortune guidance regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMisfortuneGuidanceUnlockChainFeedsRank2To5()
    {
        using TestContext context = BuildContext();
        PartyState partyState = context.PartyState;
        CharacterManagementModule manager = context.Manager;
        MisfortuneGuidanceService guidance = context.Guidance;
        FaithService faith = context.Faith;
        BattleRuntimeModule battleRuntime = context.BattleRuntime;
        IReadOnlyDictionary<StringName, ItemDef> itemDefIndex = context.ItemDefIndex;
        if (
            partyState == null
            || manager == null
            || guidance == null
            || faith == null
            || battleRuntime == null
        )
        {
            AssertTrue(false, "Misfortune guidance regression 前置构建失败。");
            return;
        }

        FaithDevotionResult rank1Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(rank1Result.Success, "doom_marked 写入后应允许进入 Misfortune rank 1。");
        ApplyNextPendingReward(manager, partyState, 1);
        AssertEq(GetCustomStat(partyState, DoomAuthorityStatId), 1, "rank 1 结算后应写入 doom_authority=1。");

        FaithDevotionResult blockedRank2 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(!blockedRank2.Success, "guidance_true 未解锁前不应进入 rank 2。");
        AssertEq(
            blockedRank2.MissingAchievementId,
            GuidanceTrueId,
            "rank 2 应明确指出 guidance_true 缺失。"
        );

        List<StringName> trueUnlocks = guidance.HandleBattleResolution(
            BuildBattleStateWithDefeatedEnemy(
                "misfortune_true",
                StatusBlackStarBrandElite,
                false
            ),
            BuildBattleResolutionResult("misfortune_true")
        );
        AssertTrue(trueUnlocks.Contains(GuidanceTrueId), "doom_marked 后封印 elite 应解锁 guidance_true。");
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceTrueId), "campaign achievement 记录应保留 guidance_true。");
        AssertTrue(partyState.pending_character_rewards.Count == 0, "guidance 成就本身不应排入额外 reward 队列。");

        FaithDevotionResult rank2Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(rank2Result.Success, "guidance_true 达成后应允许进入 rank 2。");
        ApplyNextPendingReward(manager, partyState, 2);

        FaithDevotionResult blockedRank3 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(!blockedRank3.Success, "guidance_devout 未解锁前不应进入 rank 3。");
        AssertEq(
            blockedRank3.MissingAchievementId,
            GuidanceDevoutId,
            "rank 3 应明确指出 guidance_devout 缺失。"
        );

        RegisterMisfortuneReason(battleRuntime, "critical_fail");
        List<StringName> devoutUnlocks = guidance.HandleBattleResolution(
            BuildBattleStateWithDefeatedEnemy(
                "misfortune_devout",
                StatusCrownBreakBrokenHand,
                false
            ),
            BuildBattleResolutionResult("misfortune_devout")
        );
        AssertTrue(devoutUnlocks.Contains(GuidanceDevoutId), "大失败后再用封印链赢下 elite 应解锁 guidance_devout。");
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceDevoutId), "campaign achievement 记录应保留 guidance_devout。");

        FaithDevotionResult rank3Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(rank3Result.Success, "guidance_devout 达成后应允许进入 rank 3。");
        ApplyNextPendingReward(manager, partyState, 3);

        FaithDevotionResult blockedRank4 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(!blockedRank4.Success, "guidance_exalted 未解锁前不应进入 rank 4。");
        AssertEq(
            blockedRank4.MissingAchievementId,
            GuidanceExaltedId,
            "rank 4 应明确指出 guidance_exalted 缺失。"
        );

        battleRuntime.calamity_by_member_id[HeroId] = 2;
        List<StringName> exaltedBattleUnlocks = guidance.HandleBattleResolution(
            BuildBattleStateWithoutEnemies("misfortune_exalted"),
            BuildBattleResolutionResult(
                "misfortune_exalted",
                new GDictionary { ["converted_calamity_shards"] = 1 }
            )
        );
        AssertTrue(exaltedBattleUnlocks.Count == 0, "仅结算 calamity->shard 不应提前直接解锁 guidance_exalted。");
        List<StringName> exaltedUnlocks = guidance.HandleForgeResult(
            HeroId,
            BuildForgeGuidanceInput(ShadowHalberdId),
            itemDefIndex
        );
        AssertTrue(exaltedUnlocks.Contains(GuidanceExaltedId), "结算碎片后用固定材料打造黑暗装备应解锁 guidance_exalted。");
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceExaltedId), "campaign achievement 记录应保留 guidance_exalted。");

        FaithDevotionResult rank4Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(rank4Result.Success, "guidance_exalted 达成后应允许进入 rank 4。");
        ApplyNextPendingReward(manager, partyState, 4);

        FaithDevotionResult blockedRank5 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(!blockedRank5.Success, "guidance_blessed 未解锁前不应进入 rank 5。");
        AssertEq(
            blockedRank5.MissingAchievementId,
            GuidanceBlessedId,
            "rank 5 应明确指出 guidance_blessed 缺失。"
        );

        List<StringName> blessedUnlocks = guidance.HandleBattleResolution(
            BuildBattleStateWithDefeatedEnemy(
                "misfortune_blessed",
                StatusDoomSentenceVerdict,
                true
            ),
            BuildBattleResolutionResult("misfortune_blessed")
        );
        AssertTrue(blessedUnlocks.Contains(GuidanceBlessedId), "用 doom_sentence 终结 boss 应解锁 guidance_blessed。");
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceBlessedId), "campaign achievement 记录应保留 guidance_blessed。");

        FaithDevotionResult rank5Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        AssertTrue(rank5Result.Success, "guidance_blessed 达成后应允许进入 rank 5。");
        ApplyNextPendingReward(manager, partyState, 5);
        AssertEq(GetCustomStat(partyState, DoomAuthorityStatId), 5, "完整 guidance 链结算后 doom_authority 应到 rank 5。");
    }

    private void TestForgeResultRejectsLegacyOkSuccessAlias()
    {
        using TestContext context = BuildContext();
        PartyState partyState = context.PartyState;
        MisfortuneGuidanceService guidance = context.Guidance;
        BattleRuntimeModule battleRuntime = context.BattleRuntime;
        if (partyState == null || guidance == null || battleRuntime == null)
        {
            AssertTrue(false, "Misfortune legacy ok alias regression 前置构建失败。");
            return;
        }

        battleRuntime.calamity_by_member_id[HeroId] = 2;
        guidance.HandleBattleResolution(
            BuildBattleStateWithoutEnemies("misfortune_legacy_ok_alias"),
            BuildBattleResolutionResult(
                "misfortune_legacy_ok_alias",
                new GDictionary { ["converted_calamity_shards"] = 1 }
            )
        );
        GDictionary legacyResult = BuildForgeResult(ShadowHalberdId);
        legacyResult.Remove("success");
        legacyResult["ok"] = true;
        GStringNameArray legacyUnlocks = battleRuntime
            .get_fate_runtime()
            .handle_misfortune_forge_result(
                HeroId,
                legacyResult,
                context.ItemDefs
            );
        AssertTrue(legacyUnlocks.Count == 0, "forge result 只有 legacy ok=true 时不应解锁 guidance_exalted。");
        AssertTrue(
            !IsAchievementUnlocked(partyState, GuidanceExaltedId),
            "forge result 缺正式 success 字段时不应写入 guidance_exalted。"
        );
    }

    private void TestForgeResultRejectsStringKeyOnlyDarkEquipmentDef()
    {
        using TestContext context = BuildContext();
        PartyState partyState = context.PartyState;
        MisfortuneGuidanceService guidance = context.Guidance;
        BattleRuntimeModule battleRuntime = context.BattleRuntime;
        GDictionary itemDefs = context.ItemDefs;
        if (partyState == null || guidance == null || battleRuntime == null)
        {
            AssertTrue(false, "Misfortune String-key-only item_defs regression 前置构建失败。");
            return;
        }

        battleRuntime.calamity_by_member_id[HeroId] = 2;
        guidance.HandleBattleResolution(
            BuildBattleStateWithoutEnemies("misfortune_string_key_item_defs"),
            BuildBattleResolutionResult(
                "misfortune_string_key_item_defs",
                new GDictionary { ["converted_calamity_shards"] = 1 }
            )
        );
        ItemDef darkWeapon = itemDefs[ShadowHalberdId].As<ItemDef>();
        if (darkWeapon == null)
        {
            AssertTrue(false, "Misfortune String-key-only item_defs regression 前置：应存在正式 shadow_halberd。");
            return;
        }

        GDictionary stringKeyOnlyDefs = new()
        {
            [darkWeapon.item_id.ToString()] = darkWeapon,
        };
        GStringNameArray unlocks = battleRuntime
            .get_fate_runtime()
            .handle_misfortune_forge_result(
                HeroId,
                BuildForgeResult(ShadowHalberdId),
                stringKeyOnlyDefs
            );
        AssertTrue(unlocks.Count == 0, "forge result 只有 String key 的 dark equipment def 时不应解锁 guidance_exalted。");
        AssertTrue(
            !IsAchievementUnlocked(partyState, GuidanceExaltedId),
            "forge result 缺正式 StringName key 时不应写入 guidance_exalted。"
        );
    }

    private static TestContext BuildContext()
    {
        GDictionary itemDefs = BuildItemDefs();
        Dictionary<StringName, ItemDef> itemDefIndex = BuildItemDefIndex(itemDefs);
        PartyState partyState = new()
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
            active_member_ids = new GStringNameArray { HeroId },
        };
        partyState.set_gold(50000);
        partyState.set_member_state(BuildMemberState());

        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new ProgressionContentRegistry().get_achievement_defs(),
            itemDefs
        );

        BattleRuntimeModule battleRuntime = new();
        battleRuntime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        battleRuntime.get_fate_runtime().begin_battle(battleRuntime.calamity_by_member_id);

        MisfortuneGuidanceService guidance = new();
        guidance.Setup(manager, battleRuntime);

        FaithService faith = new();
        return new TestContext(
            partyState,
            manager,
            guidance,
            faith,
            battleRuntime,
            itemDefs,
            itemDefIndex
        );
    }

    private static GDictionary BuildItemDefs()
    {
        ItemDef darkWeapon = new()
        {
            item_id = ShadowHalberdId,
            display_name = "Shadow Halberd",
            item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
            equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
            equipment_slot_ids = new GStringArray { EquipmentRules.MAIN_HAND().ToString() },
            tags = new GStringNameArray { "dark", "misfortune" },
            crafting_groups = new GStringNameArray { "dark", "misfortune" },
        };

        ItemDef calamityShard = new()
        {
            item_id = BattleLootConstants.ITEM_CALAMITY_SHARD(),
            display_name = "灾厄碎片",
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
            is_stackable = true,
            max_stack = 99,
            tags = new GStringNameArray { "material", "misfortune" },
            crafting_groups = new GStringNameArray { "misfortune" },
        };

        return new GDictionary
        {
            [darkWeapon.item_id] = darkWeapon,
            [calamityShard.item_id] = calamityShard,
        };
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDef>();
        foreach (Variant key in itemDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ItemDef itemDef = itemDefs[key].As<ItemDef>();
            if (itemDef != null)
                result[key.AsStringName()] = itemDef;
        }
        return result;
    }

    private static PartyMemberState BuildMemberState()
    {
        PartyMemberState memberState = new()
        {
            member_id = HeroId,
            display_name = "Hero",
        };
        memberState.progression.unit_id = HeroId;
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 30;
        UnitProfessionProgress levelAnchor = new()
        {
            profession_id = "misfortune_guidance_level_anchor",
            rank = 30,
        };
        memberState.progression.set_profession_progress(levelAnchor);
        memberState
            .progression
            .unit_base_attributes
            .set_attribute_value(DoomMarkedStatId, 1);
        memberState
            .progression
            .unit_base_attributes
            .set_attribute_value(DoomAuthorityStatId, 0);
        return memberState;
    }

    private static BattleState BuildBattleStateWithDefeatedEnemy(
        StringName battleId,
        StringName statusId,
        bool isBoss
    )
    {
        BattleState battleState = new()
        {
            battle_id = battleId,
        };
        BattleUnitState heroUnit = new()
        {
            unit_id = "hero_unit",
            source_member_id = HeroId,
            faction_id = "player",
            display_name = "Hero",
            is_alive = true,
        };
        battleState.units[heroUnit.unit_id] = heroUnit;
        battleState.ally_unit_ids = new GStringNameArray { heroUnit.unit_id };

        BattleUnitState enemyUnit = new()
        {
            unit_id = "enemy_target",
            display_name = "Elite Target",
            faction_id = "enemy",
            is_alive = false,
            current_hp = 0,
        };
        enemyUnit.attribute_snapshot.set_value(FortuneMarkTargetStatId, 1);
        enemyUnit.attribute_snapshot.set_value(BossTargetStatId, isBoss ? 1 : 0);
        SetStatus(enemyUnit, statusId, heroUnit.unit_id);
        battleState.units[enemyUnit.unit_id] = enemyUnit;
        battleState.enemy_unit_ids = new GStringNameArray { enemyUnit.unit_id };
        return battleState;
    }

    private static BattleState BuildBattleStateWithoutEnemies(StringName battleId)
    {
        BattleState battleState = new()
        {
            battle_id = battleId,
        };
        BattleUnitState heroUnit = new()
        {
            unit_id = "hero_unit",
            source_member_id = HeroId,
            faction_id = "player",
            display_name = "Hero",
            is_alive = true,
        };
        battleState.units[heroUnit.unit_id] = heroUnit;
        battleState.ally_unit_ids = new GStringNameArray { heroUnit.unit_id };
        return battleState;
    }

    private static BattleResolutionResult BuildBattleResolutionResult(
        StringName battleId,
        GDictionary partyResourceCommit = null
    )
    {
        return new BattleResolutionResult
        {
            battle_id = battleId,
            winner_faction_id = "player",
            encounter_resolution = "player_victory",
            party_resource_commit =
                partyResourceCommit != null
                    ? (GDictionary)partyResourceCommit.Duplicate(true)
                    : new GDictionary(),
        };
    }

    private static GDictionary BuildForgeResult(StringName outputItemId)
    {
        return new GDictionary
        {
            ["success"] = true,
            ["inventory_delta"] = new GDictionary
            {
                ["recipe_id"] = "shadow_halberd_recipe",
                ["removed_entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["item_id"] = BattleLootConstants.ITEM_CALAMITY_SHARD().ToString(),
                        ["quantity"] = 1,
                    },
                },
                ["added_entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["item_id"] = outputItemId.ToString(),
                        ["quantity"] = 1,
                    },
                },
            },
            ["service_side_effects"] = new GDictionary
            {
                ["output_item_id"] = outputItemId.ToString(),
            },
        };
    }

    private static MisfortuneForgeGuidanceInput BuildForgeGuidanceInput(StringName outputItemId)
    {
        return new MisfortuneForgeGuidanceInput(
            true,
            new[]
            {
                new MisfortuneForgeGuidanceItemEntry(
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    1
                ),
            },
            new[] { new MisfortuneForgeGuidanceItemEntry(outputItemId, 1) },
            outputItemId
        );
    }

    private static void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        StringName sourceUnitId
    )
    {
        BattleStatusEffectState statusEntry = new()
        {
            status_id = statusId,
            source_unit_id = sourceUnitId,
            power = 1,
            stacks = 1,
            duration = 60,
        };
        unitState.set_status_effect(statusEntry);
    }

    private static void RegisterMisfortuneReason(
        BattleRuntimeModule battleRuntime,
        StringName reasonId
    )
    {
        BattleUnitState heroUnit = new()
        {
            unit_id = "misfortune_reason_hero",
            source_member_id = HeroId,
            faction_id = "player",
            display_name = "Hero",
            is_alive = true,
            current_hp = 60,
        };
        heroUnit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 60);
        battleRuntime
            .get_fate_runtime()
            .handle_misfortune_trigger(reasonId, new GDictionary { ["unit_state"] = heroUnit });
    }

    private void ApplyNextPendingReward(
        CharacterManagementModule manager,
        PartyState partyState,
        int expectedRank
    )
    {
        PendingCharacterReward pendingReward = partyState.get_next_pending_character_reward();
        AssertTrue(pendingReward != null, $"Misfortune rank {expectedRank} 应产生 pending reward。");
        if (pendingReward == null)
            return;
        manager.apply_pending_character_reward(pendingReward);
        AssertTrue(
            partyState.get_next_pending_character_reward() == null,
            $"Misfortune rank {expectedRank} 结算后应清空 pending reward。"
        );
    }

    private static bool IsAchievementUnlocked(PartyState partyState, StringName achievementId)
    {
        PartyMemberState memberState = partyState?.get_member_state(HeroId);
        if (memberState?.progression == null)
            return false;
        AchievementProgressState progressState =
            memberState.progression.get_achievement_progress_state(achievementId);
        return progressState != null && progressState.is_unlocked;
    }

    private static int GetCustomStat(PartyState partyState, StringName statId)
    {
        PartyMemberState memberState = partyState?.get_member_state(HeroId);
        if (memberState?.progression?.unit_base_attributes == null)
            return 0;
        return memberState.progression.unit_base_attributes.get_attribute_value(statId);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }

    private sealed class TestContext : System.IDisposable
    {
        public TestContext(
            PartyState partyState,
            CharacterManagementModule manager,
            MisfortuneGuidanceService guidance,
            FaithService faith,
            BattleRuntimeModule battleRuntime,
            GDictionary itemDefs,
            IReadOnlyDictionary<StringName, ItemDef> itemDefIndex
        )
        {
            PartyState = partyState;
            Manager = manager;
            Guidance = guidance;
            Faith = faith;
            BattleRuntime = battleRuntime;
            ItemDefs = itemDefs;
            ItemDefIndex = itemDefIndex;
        }

        public PartyState PartyState { get; }
        public CharacterManagementModule Manager { get; }
        public MisfortuneGuidanceService Guidance { get; }
        public FaithService Faith { get; }
        public BattleRuntimeModule BattleRuntime { get; }
        public GDictionary ItemDefs { get; }
        public IReadOnlyDictionary<StringName, ItemDef> ItemDefIndex { get; }

        public void Dispose()
        {
            Guidance?.Dispose();
            BattleRuntime?.dispose();
            Faith?.Dispose();
            Manager?.Dispose();
        }
    }
}

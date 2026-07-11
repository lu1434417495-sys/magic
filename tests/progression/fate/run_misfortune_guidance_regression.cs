using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_misfortune_guidance_regression : LifecycleTestSceneTree
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

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestMisfortuneGuidanceUnlockChainFeedsRank2To5();
        TestForgeResultRejectsStringKeyOnlyDarkEquipmentDef();

        return _test.Finish("Misfortune guidance regression");
    }

    private void TestMisfortuneGuidanceUnlockChainFeedsRank2To5()
    {
        using TestContext context = BuildContext();
        PartyState partyState = context.PartyState;
        CharacterManagementModule manager = context.Manager;
        MisfortuneGuidanceService guidance = context.Guidance;
        FaithService faith = context.Faith;
        BattleRuntimeModule battleRuntime = context.BattleRuntime;
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefIndex = context.ItemDefIndex;
        if (
            partyState == null
            || manager == null
            || guidance == null
            || faith == null
            || battleRuntime == null
        )
        {
            _test.True(false, "Misfortune guidance regression 前置构建失败。");
            return;
        }

        FaithDevotionResult rank1Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(rank1Result.Success, "doom_marked 写入后应允许进入 Misfortune rank 1。");
        ApplyNextPendingReward(manager, partyState, 1);
        _test.Eq(GetCustomStat(partyState, DoomAuthorityStatId), 1, "rank 1 结算后应写入 doom_authority=1。");

        FaithDevotionResult blockedRank2 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(!blockedRank2.Success, "guidance_true 未解锁前不应进入 rank 2。");
        _test.Eq(
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
        _test.True(trueUnlocks.Contains(GuidanceTrueId), "doom_marked 后封印 elite 应解锁 guidance_true。");
        _test.True(IsAchievementUnlocked(partyState, GuidanceTrueId), "campaign achievement 记录应保留 guidance_true。");
        _test.True(partyState.pending_character_rewards.Count == 0, "guidance 成就本身不应排入额外 reward 队列。");

        FaithDevotionResult rank2Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(rank2Result.Success, "guidance_true 达成后应允许进入 rank 2。");
        ApplyNextPendingReward(manager, partyState, 2);

        FaithDevotionResult blockedRank3 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(!blockedRank3.Success, "guidance_devout 未解锁前不应进入 rank 3。");
        _test.Eq(
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
        _test.True(devoutUnlocks.Contains(GuidanceDevoutId), "大失败后再用封印链赢下 elite 应解锁 guidance_devout。");
        _test.True(IsAchievementUnlocked(partyState, GuidanceDevoutId), "campaign achievement 记录应保留 guidance_devout。");

        FaithDevotionResult rank3Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(rank3Result.Success, "guidance_devout 达成后应允许进入 rank 3。");
        ApplyNextPendingReward(manager, partyState, 3);

        FaithDevotionResult blockedRank4 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(!blockedRank4.Success, "guidance_exalted 未解锁前不应进入 rank 4。");
        _test.Eq(
            blockedRank4.MissingAchievementId,
            GuidanceExaltedId,
            "rank 4 应明确指出 guidance_exalted 缺失。"
        );

        battleRuntime.calamity_by_member_id[HeroId] = 2;
        List<StringName> exaltedBattleUnlocks = guidance.HandleBattleResolution(
            BuildBattleStateWithoutEnemies("misfortune_exalted"),
            BuildBattleResolutionResult("misfortune_exalted", includeCalamityConversionShard: true)
        );
        _test.True(exaltedBattleUnlocks.Count == 0, "仅结算 calamity->shard 不应提前直接解锁 guidance_exalted。");
        List<StringName> exaltedUnlocks = guidance.HandleForgeResult(
            HeroId,
            BuildForgeGuidanceInput(ShadowHalberdId),
            itemDefIndex
        );
        _test.True(exaltedUnlocks.Contains(GuidanceExaltedId), "结算碎片后用固定材料打造黑暗装备应解锁 guidance_exalted。");
        _test.True(IsAchievementUnlocked(partyState, GuidanceExaltedId), "campaign achievement 记录应保留 guidance_exalted。");

        FaithDevotionResult rank4Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(rank4Result.Success, "guidance_exalted 达成后应允许进入 rank 4。");
        ApplyNextPendingReward(manager, partyState, 4);

        FaithDevotionResult blockedRank5 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(!blockedRank5.Success, "guidance_blessed 未解锁前不应进入 rank 5。");
        _test.Eq(
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
        _test.True(blessedUnlocks.Contains(GuidanceBlessedId), "用 doom_sentence 终结 boss 应解锁 guidance_blessed。");
        _test.True(IsAchievementUnlocked(partyState, GuidanceBlessedId), "campaign achievement 记录应保留 guidance_blessed。");

        FaithDevotionResult rank5Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.True(rank5Result.Success, "guidance_blessed 达成后应允许进入 rank 5。");
        ApplyNextPendingReward(manager, partyState, 5);
        _test.Eq(GetCustomStat(partyState, DoomAuthorityStatId), 5, "完整 guidance 链结算后 doom_authority 应到 rank 5。");
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
            _test.True(false, "Misfortune String-key-only item_defs regression 前置构建失败。");
            return;
        }

        battleRuntime.calamity_by_member_id[HeroId] = 2;
        guidance.HandleBattleResolution(
            BuildBattleStateWithoutEnemies("misfortune_string_key_item_defs"),
            BuildBattleResolutionResult(
                "misfortune_string_key_item_defs",
                includeCalamityConversionShard: true
            )
        );
        ItemDef darkWeapon = itemDefs[ShadowHalberdId].As<ItemDef>();
        if (darkWeapon == null)
        {
            _test.True(false, "Misfortune String-key-only item_defs regression 前置：应存在正式 shadow_halberd。");
            return;
        }

        GDictionary stringKeyOnlyDefs = new()
        {
            [darkWeapon.item_id.ToString()] = darkWeapon,
        };
        GStringNameArray unlocks = battleRuntime
            .GetFateRuntime()
            .HandleMisfortuneForgeResult(
                HeroId,
                BuildForgeServiceResult(ShadowHalberdId),
                BuildItemDefIndex(stringKeyOnlyDefs)
            );
        _test.True(unlocks.Count == 0, "forge result 只有 String key 的 dark equipment def 时不应解锁 guidance_exalted。");
        _test.True(
            !IsAchievementUnlocked(partyState, GuidanceExaltedId),
            "forge result 缺正式 StringName key 时不应写入 guidance_exalted。"
        );
    }

    private static TestContext BuildContext()
    {
        GDictionary itemDefs = BuildItemDefs();
        Dictionary<StringName, ItemDefinition> itemDefIndex = BuildItemDefIndex(itemDefs);
        PartyState partyState = new()
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
            active_member_ids = new GStringNameArray { HeroId },
        };
        partyState.SetGold(50000);
        partyState.SetMemberState(BuildMemberState());

        CharacterManagementModule manager = new();
        using ProgressionContentRegistry progressionRegistry = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            progressionRegistry.GetAchievementDefsTyped(),
            itemDefIndex,
            new Dictionary<StringName, QuestDefinition>(),
            null,
            new ProgressionIdentityCatalogData()
        );

        BattleRuntimeModule battleRuntime = new();
        battleRuntime.setup();
        battleRuntime.GetFateRuntime().BeginBattle(battleRuntime.calamity_by_member_id);

        MisfortuneGuidanceService guidance = new();
        guidance.Setup(manager, battleRuntime);

        FaithContentRegistry faithRegistry = new();
        faithRegistry.Rebuild();
        FaithService faith = new(faithRegistry.GetFaithDeityDefsTyped());
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
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new GStringArray
            {
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand).ToString(),
            },
            tags = new GStringNameArray { "dark", "misfortune" },
            crafting_groups = new GStringNameArray { "dark", "misfortune" },
        };

        ItemDef calamityShard = new()
        {
            item_id = BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
            display_name = "灾厄碎片",
            CategoryKind = ItemCategoryKind.Misc,
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

    private static Dictionary<StringName, ItemDefinition> BuildItemDefIndex(GDictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDefinition>();
        foreach (Variant key in itemDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ItemDef itemDef = itemDefs[key].As<ItemDef>();
            if (itemDef != null)
                result[key.AsStringName()] = itemDef.ToDefinition();
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
        memberState.progression.SetProfessionProgress(levelAnchor);
        memberState
            .progression
            .unit_base_attributes
            .SetAttributeValue(DoomMarkedStatId, 1);
        memberState
            .progression
            .unit_base_attributes
            .SetAttributeValue(DoomAuthorityStatId, 0);
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
        battleState.SetUnit(heroUnit);
        battleState.ally_unit_ids = new GStringNameArray { heroUnit.unit_id };

        BattleUnitState enemyUnit = new()
        {
            unit_id = "enemy_target",
            display_name = "Elite Target",
            faction_id = "enemy",
            is_alive = false,
            current_hp = 0,
        };
        enemyUnit.attribute_snapshot.SetValue(FortuneMarkTargetStatId, 1);
        enemyUnit.attribute_snapshot.SetValue(BossTargetStatId, isBoss ? 1 : 0);
        SetStatus(enemyUnit, statusId, heroUnit.unit_id);
        battleState.SetUnit(enemyUnit);
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
        battleState.SetUnit(heroUnit);
        battleState.ally_unit_ids = new GStringNameArray { heroUnit.unit_id };
        return battleState;
    }

    private static BattleResolutionResult BuildBattleResolutionResult(
        StringName battleId,
        bool includeCalamityConversionShard = false
    )
    {
        var result = new BattleResolutionResult
        {
            battle_id = battleId,
            winner_faction_id = "player",
            encounter_resolution = "player_victory",
        };
        if (includeCalamityConversionShard)
        {
            result.SetLootEntries(
                new[]
                {
                    BattleLootEntry.CreateItem(
                        BattleLootSourceKind.CalamityConversion,
                        BattleLootIds.ToStringName(BattleLootSourceIdKind.OrdinaryBattle),
                        "普通战未消耗 calamity 结算",
                        "calamity_conversion",
                        BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                        1
                    ),
                }
            );
        }
        return result;
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
                        ["item_id"] = BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard).ToString(),
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

    private static SettlementServiceResult BuildForgeServiceResult(StringName outputItemId)
    {
        GDictionary payload = BuildForgeResult(outputItemId);
        var result = new SettlementServiceResult
        {
            Success = true,
        };
        result.SetInventoryDelta(payload["inventory_delta"].AsGodotDictionary());
        result.SetServiceSideEffects(payload["service_side_effects"].AsGodotDictionary());
        return result;
    }

    private static MisfortuneForgeGuidanceInput BuildForgeGuidanceInput(StringName outputItemId)
    {
        return new MisfortuneForgeGuidanceInput(
            true,
            new[]
            {
                new MisfortuneForgeGuidanceItemEntry(
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
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
        unitState.SetStatusEffect(statusEntry);
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
        heroUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 60);
        battleRuntime
            .GetFateRuntime()
            .HandleMisfortuneTrigger(
                reasonId == new StringName("critical_fail")
                    ? MisfortuneTriggerRequest.CriticalFail(heroUnit)
                    : MisfortuneTriggerRequest.OrdinaryMiss(heroUnit)
            );
    }

    private void ApplyNextPendingReward(
        CharacterManagementModule manager,
        PartyState partyState,
        int expectedRank
    )
    {
        PendingCharacterReward pendingReward = partyState.GetNextPendingCharacterReward();
        _test.True(pendingReward != null, $"Misfortune rank {expectedRank} 应产生 pending reward。");
        if (pendingReward == null)
            return;
        manager.ApplyPendingCharacterReward(pendingReward);
        _test.True(
            partyState.GetNextPendingCharacterReward() == null,
            $"Misfortune rank {expectedRank} 结算后应清空 pending reward。"
        );
    }

    private static bool IsAchievementUnlocked(PartyState partyState, StringName achievementId)
    {
        PartyMemberState memberState = partyState?.GetMemberState(HeroId);
        if (memberState?.progression == null)
            return false;
        AchievementProgressState progressState =
            memberState.progression.GetAchievementProgressState(achievementId);
        return progressState != null && progressState.is_unlocked;
    }

    private static int GetCustomStat(PartyState partyState, StringName statId)
    {
        PartyMemberState memberState = partyState?.GetMemberState(HeroId);
        if (memberState?.progression?.unit_base_attributes == null)
            return 0;
        return memberState.progression.unit_base_attributes.GetAttributeValue(statId);
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
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefIndex
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
        public IReadOnlyDictionary<StringName, ItemDefinition> ItemDefIndex { get; }

        public void Dispose()
        {
            Guidance?.Dispose();
            BattleRuntime?.dispose();
            Faith?.Dispose();
            Manager?.Dispose();
        }
    }
}

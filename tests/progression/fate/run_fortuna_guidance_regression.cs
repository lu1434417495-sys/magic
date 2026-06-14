using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_fortuna_guidance_regression : SceneTree
{
    private static readonly StringName HeroId = "hero";
    private static readonly StringName FortunaDeityId = "fortuna";
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";
    private static readonly StringName GuidanceTrueId = "fortuna_guidance_true";
    private static readonly StringName GuidanceDevoutId = "fortuna_guidance_devout";
    private static readonly StringName GuidanceExaltedId = "fortuna_guidance_exalted";
    private static readonly StringName GuidanceBlessedId = "fortuna_guidance_blessed";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestFortunaGuidanceUnlockChainFeedsRank2To5();
        TestRuntimeChapterAdapterUsesFormalPermanentDeathField();

        GodotSharpCleanup.CollectPendingFinalizers();
        return _test.Finish("Fortuna guidance regression");
    }

    private void TestFortunaGuidanceUnlockChainFeedsRank2To5()
    {
        using TestContext context = BuildContext();
        PartyState partyState = context.PartyState;
        CharacterManagementModule manager = context.Manager;
        FortunaGuidanceService guidance = context.Guidance;
        FaithService faith = context.Faith;
        if (partyState == null || manager == null || guidance == null || faith == null)
        {
            _test.True(false, "Fortuna guidance regression 前置构建失败。");
            return;
        }

        guidance.HandleFateEvent(BuildDisadvantageCritInput("battle_mark"));
        _test.True(
            !IsAchievementUnlocked(partyState, GuidanceTrueId),
            "fortune_marked 前第一次事件不应顺带解锁 guidance_true。"
        );

        SetCustomStat(partyState, FortuneMarkedStatId, 1);
        FaithDevotionResult rank1Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(rank1Result.Success, "fortune_marked 写入后应允许进入 Fortuna rank 1。");
        ApplyNextPendingReward(manager, partyState, 1);
        _test.Eq(GetCustomStat(partyState, FaithLuckBonusStatId), 1, "rank 1 结算后应写入 faith_luck_bonus=1。");

        FaithDevotionResult blockedRank2 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(!blockedRank2.Success, "guidance_true 未解锁前不应进入 rank 2。");
        _test.Eq(
            blockedRank2.ErrorCode,
            "achievement_requirement_unmet",
            "rank 2 缺门票时应走 achievement gate。"
        );
        _test.Eq(
            blockedRank2.MissingAchievementId,
            GuidanceTrueId,
            "rank 2 应明确指出 guidance_true 缺失。"
        );

        guidance.HandleFateEvent(BuildDisadvantageCritInput("battle_true"));
        _test.True(IsAchievementUnlocked(partyState, GuidanceTrueId), "再次命中条件后应解锁 guidance_true。");
        _test.True(partyState.pending_character_rewards.Count == 0, "guidance 成就本身不应排入额外 reward 队列。");

        FaithDevotionResult rank2Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(rank2Result.Success, "guidance_true 达成后应允许进入 rank 2。");
        ApplyNextPendingReward(manager, partyState, 2);

        FaithDevotionResult blockedRank3 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(!blockedRank3.Success, "guidance_devout 未解锁前不应进入 rank 3。");
        _test.Eq(
            blockedRank3.MissingAchievementId,
            GuidanceDevoutId,
            "rank 3 应明确指出 guidance_devout 缺失。"
        );

        guidance.HandleFateEvent(BuildDevoutInput("battle_devout"));
        List<StringName> devoutUnlocks = guidance.HandleBattleResolution(
            BuildBattleState("battle_devout", true),
            BuildBattleResolutionResult("battle_devout")
        );
        _test.True(
            devoutUnlocks.Contains(GuidanceDevoutId),
            "低血+强 debuff 活下来并赢战后应解锁 guidance_devout。"
        );
        _test.True(IsAchievementUnlocked(partyState, GuidanceDevoutId), "campaign achievement 记录应保留 guidance_devout。");

        FaithDevotionResult rank3Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(rank3Result.Success, "guidance_devout 达成后应允许进入 rank 3。");
        ApplyNextPendingReward(manager, partyState, 3);

        FaithDevotionResult blockedRank4 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(!blockedRank4.Success, "guidance_exalted 未解锁前不应进入 rank 4。");
        _test.Eq(
            blockedRank4.MissingAchievementId,
            GuidanceExaltedId,
            "rank 4 应明确指出 guidance_exalted 缺失。"
        );

        guidance.HandleFateEvent(BuildExaltedInput("battle_exalted"));
        _test.True(IsAchievementUnlocked(partyState, GuidanceExaltedId), "高位威胁区大成功应解锁 guidance_exalted。");

        FaithDevotionResult rank4Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(rank4Result.Success, "guidance_exalted 达成后应允许进入 rank 4。");
        ApplyNextPendingReward(manager, partyState, 4);

        FaithDevotionResult blockedRank5 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(!blockedRank5.Success, "guidance_blessed 未解锁前不应进入 rank 5。");
        _test.Eq(
            blockedRank5.MissingAchievementId,
            GuidanceBlessedId,
            "rank 5 应明确指出 guidance_blessed 缺失。"
        );

        List<StringName> chapterUnlocks = guidance.HandleChapterCompleted(
            new FortunaChapterCompletionInput
            {
                MemberIds = new[] { HeroId },
                HadPermanentDeath = false,
            }
        );
        _test.True(
            chapterUnlocks.Contains(GuidanceBlessedId),
            "章节无永久死亡且出现过 Fortuna 事件时应解锁 guidance_blessed。"
        );
        _test.True(IsAchievementUnlocked(partyState, GuidanceBlessedId), "campaign achievement 记录应保留 guidance_blessed。");

        FaithDevotionResult rank5Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.True(rank5Result.Success, "guidance_blessed 达成后应允许进入 rank 5。");
        ApplyNextPendingReward(manager, partyState, 5);
        _test.Eq(GetCustomStat(partyState, FaithLuckBonusStatId), 5, "完整 guidance 链结算后 faith_luck_bonus 应到 rank 5。");
    }

    private void TestRuntimeChapterAdapterUsesFormalPermanentDeathField()
    {
        using TestContext context = BuildContext();
        SetCustomStat(context.PartyState, FortuneMarkedStatId, 1);
        SetCustomStat(context.PartyState, FaithLuckBonusStatId, 1);

        BattleFateEventBus bus = new();
        FateRuntimeModule fateRuntime = new();
        fateRuntime.Setup(context.Manager, bus);

        bus.dispatch("high_threat_critical_hit", BuildExaltedPayload("adapter_blocked"));
        GStringNameArray blockedUnlocks = fateRuntime.HandleFortunaChapterCompleted(
            new GDictionary
            {
                ["chapter_id"] = "chapter_blocked",
                ["member_ids"] = new GStringNameArray { HeroId },
                ["had_permanent_death"] = true,
            }
        );
        _test.True(blockedUnlocks.Count == 0, "正式 had_permanent_death=true 时 runtime adapter 不应解锁 blessed。");
        _test.True(
            !IsAchievementUnlocked(context.PartyState, GuidanceBlessedId),
            "正式永久死亡字段为 true 时不应写入 blessed achievement。"
        );

        bus.dispatch("high_threat_critical_hit", BuildExaltedPayload("adapter_allowed"));
        GStringNameArray allowedUnlocks = fateRuntime.HandleFortunaChapterCompleted(
            new GDictionary
            {
                ["chapter_id"] = "chapter_allowed",
                ["member_ids"] = new GStringNameArray { HeroId },
                ["had_permanent_death"] = false,
            }
        );
        _test.True(
            allowedUnlocks.Contains(GuidanceBlessedId),
            "正式 had_permanent_death=false 时 runtime adapter 应投影并解锁 blessed。"
        );

        fateRuntime.DisposeRuntime();
        bus.Dispose();
    }

    private static TestContext BuildContext()
    {
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
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, ProfessionDef>(),
            progressionRegistry.GetAchievementDefsTyped(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );

        FortunaGuidanceService guidance = new();
        guidance.Setup(manager);

        FaithService faith = new();
        return new TestContext(partyState, manager, guidance, faith);
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
            profession_id = "fortuna_guidance_level_anchor",
            rank = 30,
        };
        memberState.progression.SetProfessionProgress(levelAnchor);
        memberState.progression.unit_base_attributes.SetAttributeValue(FortuneMarkedStatId, 0);
        memberState.progression.unit_base_attributes.SetAttributeValue(FaithLuckBonusStatId, 0);
        return memberState;
    }

    private static FortunaGuidanceEventInput BuildDisadvantageCritInput(StringName battleId) =>
        new()
        {
            EventType = "critical_success_under_disadvantage",
            BattleId = battleId,
            AttackerMemberId = HeroId,
            DefenderIsEliteOrBoss = true,
            AttackerLowHpHardship = false,
            AttackerStrongAttackDebuffIds = Array.Empty<StringName>(),
        };

    private static FortunaGuidanceEventInput BuildDevoutInput(StringName battleId) =>
        new()
        {
            EventType = "hardship_survival",
            BattleId = battleId,
            AttackerMemberId = HeroId,
            DefenderIsEliteOrBoss = true,
            AttackerLowHpHardship = true,
            AttackerStrongAttackDebuffIds = new[] { new StringName("stunned") },
        };

    private static FortunaGuidanceEventInput BuildExaltedInput(StringName battleId) =>
        new()
        {
            EventType = "high_threat_critical_hit",
            BattleId = battleId,
            AttackerMemberId = HeroId,
            DefenderIsEliteOrBoss = true,
            AttackerLowHpHardship = false,
            AttackerStrongAttackDebuffIds = Array.Empty<StringName>(),
        };

    private static GDictionary BuildExaltedPayload(StringName battleId) =>
        new()
        {
            ["battle_id"] = battleId,
            ["attacker_id"] = "hero_unit",
            ["attacker_member_id"] = HeroId,
            ["attacker_low_hp_hardship"] = false,
            ["attacker_strong_attack_debuff_ids"] = new GStringNameArray(),
            ["defender_id"] = "elite_target_01",
            ["defender_member_id"] = "",
            ["defender_is_elite_or_boss"] = true,
            ["attack_resolution"] = "critical_hit",
            ["critical_source"] = "high_threat",
            ["is_disadvantage"] = false,
        };

    private static BattleState BuildBattleState(StringName battleId, bool isAlive)
    {
        BattleState battleState = new()
        {
            battle_id = battleId,
        };
        BattleUnitState unit = new()
        {
            unit_id = "hero_unit",
            source_member_id = HeroId,
            faction_id = "player",
            display_name = "Hero",
            is_alive = isAlive,
        };
        battleState.units[unit.unit_id] = unit;
        battleState.ally_unit_ids = new GStringNameArray { unit.unit_id };
        return battleState;
    }

    private static BattleResolutionResult BuildBattleResolutionResult(StringName battleId)
    {
        return new BattleResolutionResult
        {
            battle_id = battleId,
            winner_faction_id = "player",
            encounter_resolution = "player_victory",
        };
    }

    private void ApplyNextPendingReward(
        CharacterManagementModule manager,
        PartyState partyState,
        int expectedRank
    )
    {
        PendingCharacterReward pendingReward = partyState.GetNextPendingCharacterReward();
        _test.True(pendingReward != null, $"Fortuna rank {expectedRank} 应产生 pending reward。");
        if (pendingReward == null)
            return;
        manager.ApplyPendingCharacterReward(pendingReward);
        _test.True(
            partyState.GetNextPendingCharacterReward() == null,
            $"Fortuna rank {expectedRank} 结算后应清空 pending reward。"
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

    private static void SetCustomStat(PartyState partyState, StringName statId, int value)
    {
        PartyMemberState memberState = partyState?.GetMemberState(HeroId);
        memberState?.progression?.unit_base_attributes?.SetAttributeValue(statId, value);
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(
            PartyState partyState,
            CharacterManagementModule manager,
            FortunaGuidanceService guidance,
            FaithService faith
        )
        {
            PartyState = partyState;
            Manager = manager;
            Guidance = guidance;
            Faith = faith;
        }

        public PartyState PartyState { get; }
        public CharacterManagementModule Manager { get; }
        public FortunaGuidanceService Guidance { get; }
        public FaithService Faith { get; }

        public void Dispose()
        {
            Guidance?.Dispose();
            Faith?.Dispose();
            Manager?.Dispose();
        }
    }
}

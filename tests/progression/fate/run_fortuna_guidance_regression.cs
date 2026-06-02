using System;
using System.Collections.Generic;
using System.Reflection;
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

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestFortunaGuidanceUnlockChainFeedsRank2To5();
        TestRuntimeChapterAdapterUsesFormalPermanentDeathField();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("Fortuna guidance regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Fortuna guidance regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(FortunaGuidanceService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "FortunaGuidanceService 应是普通 C# service，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "FortunaGuidanceService 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("handle_chapter_completed") == null,
            "FortunaGuidanceService 不应保留 Godot Dictionary chapter API。"
        );
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
            AssertTrue(false, "Fortuna guidance regression 前置构建失败。");
            return;
        }

        guidance.HandleFateEvent(BuildDisadvantageCritInput("battle_mark"));
        AssertTrue(
            !IsAchievementUnlocked(partyState, GuidanceTrueId),
            "fortune_marked 前第一次事件不应顺带解锁 guidance_true。"
        );

        SetCustomStat(partyState, FortuneMarkedStatId, 1);
        FaithDevotionResult rank1Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(rank1Result.Success, "fortune_marked 写入后应允许进入 Fortuna rank 1。");
        ApplyNextPendingReward(manager, partyState, 1);
        AssertEq(GetCustomStat(partyState, FaithLuckBonusStatId), 1, "rank 1 结算后应写入 faith_luck_bonus=1。");

        FaithDevotionResult blockedRank2 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(!blockedRank2.Success, "guidance_true 未解锁前不应进入 rank 2。");
        AssertEq(
            blockedRank2.ErrorCode,
            "achievement_requirement_unmet",
            "rank 2 缺门票时应走 achievement gate。"
        );
        AssertEq(
            blockedRank2.MissingAchievementId,
            GuidanceTrueId,
            "rank 2 应明确指出 guidance_true 缺失。"
        );

        guidance.HandleFateEvent(BuildDisadvantageCritInput("battle_true"));
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceTrueId), "再次命中条件后应解锁 guidance_true。");
        AssertTrue(partyState.pending_character_rewards.Count == 0, "guidance 成就本身不应排入额外 reward 队列。");

        FaithDevotionResult rank2Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(rank2Result.Success, "guidance_true 达成后应允许进入 rank 2。");
        ApplyNextPendingReward(manager, partyState, 2);

        FaithDevotionResult blockedRank3 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(!blockedRank3.Success, "guidance_devout 未解锁前不应进入 rank 3。");
        AssertEq(
            blockedRank3.MissingAchievementId,
            GuidanceDevoutId,
            "rank 3 应明确指出 guidance_devout 缺失。"
        );

        guidance.HandleFateEvent(BuildDevoutInput("battle_devout"));
        List<StringName> devoutUnlocks = guidance.HandleBattleResolution(
            BuildBattleState("battle_devout", true),
            BuildBattleResolutionResult("battle_devout")
        );
        AssertTrue(
            devoutUnlocks.Contains(GuidanceDevoutId),
            "低血+强 debuff 活下来并赢战后应解锁 guidance_devout。"
        );
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceDevoutId), "campaign achievement 记录应保留 guidance_devout。");

        FaithDevotionResult rank3Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(rank3Result.Success, "guidance_devout 达成后应允许进入 rank 3。");
        ApplyNextPendingReward(manager, partyState, 3);

        FaithDevotionResult blockedRank4 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(!blockedRank4.Success, "guidance_exalted 未解锁前不应进入 rank 4。");
        AssertEq(
            blockedRank4.MissingAchievementId,
            GuidanceExaltedId,
            "rank 4 应明确指出 guidance_exalted 缺失。"
        );

        guidance.HandleFateEvent(BuildExaltedInput("battle_exalted"));
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceExaltedId), "高位威胁区大成功应解锁 guidance_exalted。");

        FaithDevotionResult rank4Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(rank4Result.Success, "guidance_exalted 达成后应允许进入 rank 4。");
        ApplyNextPendingReward(manager, partyState, 4);

        FaithDevotionResult blockedRank5 = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(!blockedRank5.Success, "guidance_blessed 未解锁前不应进入 rank 5。");
        AssertEq(
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
        AssertTrue(
            chapterUnlocks.Contains(GuidanceBlessedId),
            "章节无永久死亡且出现过 Fortuna 事件时应解锁 guidance_blessed。"
        );
        AssertTrue(IsAchievementUnlocked(partyState, GuidanceBlessedId), "campaign achievement 记录应保留 guidance_blessed。");

        FaithDevotionResult rank5Result = faith.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        AssertTrue(rank5Result.Success, "guidance_blessed 达成后应允许进入 rank 5。");
        ApplyNextPendingReward(manager, partyState, 5);
        AssertEq(GetCustomStat(partyState, FaithLuckBonusStatId), 5, "完整 guidance 链结算后 faith_luck_bonus 应到 rank 5。");
    }

    private void TestRuntimeChapterAdapterUsesFormalPermanentDeathField()
    {
        using TestContext context = BuildContext();
        SetCustomStat(context.PartyState, FortuneMarkedStatId, 1);
        SetCustomStat(context.PartyState, FaithLuckBonusStatId, 1);

        BattleFateEventBus bus = new();
        FateRuntimeModule fateRuntime = new();
        fateRuntime.setup(context.Manager, bus);

        bus.dispatch("high_threat_critical_hit", BuildExaltedPayload("adapter_blocked"));
        GStringNameArray blockedUnlocks = fateRuntime.handle_fortuna_chapter_completed(
            new GDictionary
            {
                ["chapter_id"] = "chapter_blocked",
                ["member_ids"] = new GStringNameArray { HeroId },
                ["had_permanent_death"] = true,
            }
        );
        AssertTrue(blockedUnlocks.Count == 0, "正式 had_permanent_death=true 时 runtime adapter 不应解锁 blessed。");
        AssertTrue(
            !IsAchievementUnlocked(context.PartyState, GuidanceBlessedId),
            "正式永久死亡字段为 true 时不应写入 blessed achievement。"
        );

        bus.dispatch("high_threat_critical_hit", BuildExaltedPayload("adapter_allowed"));
        GStringNameArray allowedUnlocks = fateRuntime.handle_fortuna_chapter_completed(
            new GDictionary
            {
                ["chapter_id"] = "chapter_allowed",
                ["member_ids"] = new GStringNameArray { HeroId },
                ["had_permanent_death"] = false,
            }
        );
        AssertTrue(
            allowedUnlocks.Contains(GuidanceBlessedId),
            "正式 had_permanent_death=false 时 runtime adapter 应投影并解锁 blessed。"
        );

        fateRuntime.dispose();
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
        partyState.set_gold(50000);
        partyState.set_member_state(BuildMemberState());

        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new ProgressionContentRegistry().get_achievement_defs()
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
        memberState.progression.set_profession_progress(levelAnchor);
        memberState.progression.unit_base_attributes.set_attribute_value(FortuneMarkedStatId, 0);
        memberState.progression.unit_base_attributes.set_attribute_value(FaithLuckBonusStatId, 0);
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
        PendingCharacterReward pendingReward = partyState.get_next_pending_character_reward();
        AssertTrue(pendingReward != null, $"Fortuna rank {expectedRank} 应产生 pending reward。");
        if (pendingReward == null)
            return;
        manager.apply_pending_character_reward(pendingReward);
        AssertTrue(
            partyState.get_next_pending_character_reward() == null,
            $"Fortuna rank {expectedRank} 结算后应清空 pending reward。"
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

    private static void SetCustomStat(PartyState partyState, StringName statId, int value)
    {
        PartyMemberState memberState = partyState?.get_member_state(HeroId);
        memberState?.progression?.unit_base_attributes?.set_attribute_value(statId, value);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
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

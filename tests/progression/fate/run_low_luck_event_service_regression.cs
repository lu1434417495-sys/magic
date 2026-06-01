using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_low_luck_event_service_regression : SceneTree
{
    private static readonly StringName HeroId = "hero";
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestBrokenBridgeSurvivalTriggersOncePerRun();
        TestLampWithoutWitnessTriggersOncePerRun();
        TestBorrowedRoadTriggersOncePerRun();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("Low luck event service regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Low luck event service regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(LowLuckEventService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "LowLuckEventService 应是普通 C# service，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "LowLuckEventService 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("handle_battle_resolution") == null,
            "LowLuckEventService 不应保留 GDScript snake_case battle result API。"
        );
        AssertTrue(
            serviceType.GetMethod("handle_settlement_action") == null,
            "LowLuckEventService 不应保留 GDScript snake_case settlement API。"
        );
        AssertTrue(
            serviceType.GetMethod("setup") == null,
            "LowLuckEventService 不应保留 GDScript snake_case setup API。"
        );
    }

    private void TestBrokenBridgeSurvivalTriggersOncePerRun()
    {
        using LowLuckContext context = BuildContext(-4);
        LowLuckEventService service = context.Service;
        PartyState partyState = context.PartyState;

        service.HandleFateEvent("hardship_survival", BuildHardshipPayload("bridge_battle_01", -4));
        LowLuckEventResult firstResult = service.HandleBattleResolution(
            BuildBattleInput("bridge_battle_01", memberAlive: true)
        );

        AssertTrue(
            ContainsStringName(firstResult.TriggeredEventIds, LowLuckEventService.EventBrokenBridgeSurvival),
            "低血 + 强 debuff 生还后应触发断桥生还。"
        );
        AssertEq(firstResult.LootEntries.Count, 1, "断桥生还应固定产出 1 条 loot entry。");
        if (firstResult.LootEntries.Count == 1)
        {
            LowLuckLootEntry lootEntry = firstResult.LootEntries[0];
            AssertEq(lootEntry.ItemId.ToString(), "calamity_shard", "断桥生还应走固定 calamity_shard。");
            AssertEq(lootEntry.DropSourceKind.ToString(), "low_luck_event", "断桥生还应走 fixed low_luck_event 路径。");
        }
        AssertTrue(
            partyState.has_meta_flag(BuildMemberFlagId(LowLuckEventService.EventBrokenBridgeSurvival)),
            "断桥生还命中后应写入 PartyState.meta_flags 去重。"
        );

        PartyState restoredPartyState = RoundTripPartyState(partyState);
        if (restoredPartyState == null)
            return;

        using LowLuckContext restoredContext = BuildContext(-4, restoredPartyState);
        restoredContext.Service.HandleFateEvent(
            "hardship_survival",
            BuildHardshipPayload("bridge_battle_02", -4)
        );
        LowLuckEventResult secondResult = restoredContext.Service.HandleBattleResolution(
            BuildBattleInput("bridge_battle_02", memberAlive: true)
        );
        AssertEq(secondResult.TriggeredEventIds.Count, 0, "同周目重复命中断桥生还时不应再次发奖。");
        AssertEq(secondResult.LootEntries.Count, 0, "去重后断桥生还不应重复生成固定 loot。");
    }

    private void TestLampWithoutWitnessTriggersOncePerRun()
    {
        using LowLuckContext context = BuildContext(-4);
        LowLuckEventService service = context.Service;
        PartyState partyState = context.PartyState;

        LowLuckEventResult firstResult = service.HandleSettlementAction(
            new LowLuckSettlementActionInput(
                "service:rest_full",
                "service_rest_full",
                "",
                "冷灯旅舍",
                "整备"
            )
        );
        AssertTrue(
            ContainsStringName(firstResult.TriggeredEventIds, LowLuckEventService.EventLampWithoutWitness),
            "旅舍休整遇到 low luck 角色时应触发灯下无人。"
        );
        AssertEq(firstResult.PendingCharacterRewards.Count, 1, "灯下无人应固定排入 1 条待领奖励。");
        AssertRewardEntryTarget(
            firstResult.PendingCharacterRewards,
            "low_luck_black_market_hint",
            "灯下无人应固定发放黑市知识占位。"
        );
        AssertTrue(
            partyState.has_meta_flag(BuildPartyFlagId(LowLuckEventService.EventLampWithoutWitness)),
            "灯下无人命中后应写入 PartyState.meta_flags 去重。"
        );

        PartyState restoredPartyState = RoundTripPartyState(partyState);
        if (restoredPartyState == null)
            return;

        using LowLuckContext restoredContext = BuildContext(-4, restoredPartyState);
        LowLuckEventResult secondResult = restoredContext.Service.HandleSettlementAction(
            new LowLuckSettlementActionInput(
                "service:rest_full",
                "service_rest_full",
                "",
                "冷灯旅舍",
                "整备"
            )
        );
        AssertEq(secondResult.TriggeredEventIds.Count, 0, "同周目重复进入休整场景时不应再次触发灯下无人。");
        AssertEq(secondResult.PendingCharacterRewards.Count, 0, "去重后灯下无人不应重复排入奖励。");
    }

    private void TestBorrowedRoadTriggersOncePerRun()
    {
        using LowLuckContext context = BuildContext(-5);
        LowLuckEventService service = context.Service;
        PartyState partyState = context.PartyState;

        service.HandleFateEvent("critical_fail", BuildCriticalFailPayload("borrowed_road_01", -5));
        LowLuckEventResult firstResult = service.HandleBattleResolution(
            BuildBattleInput("borrowed_road_01", memberAlive: true)
        );

        AssertTrue(
            ContainsStringName(firstResult.TriggeredEventIds, LowLuckEventService.EventBorrowedRoad),
            "low luck 角色大失败后仍赢下整场战斗时应触发死里借来的路。"
        );
        AssertEq(firstResult.PendingCharacterRewards.Count, 1, "死里借来的路应固定排入 1 条待领奖励。");
        AssertRewardEntryTarget(
            firstResult.PendingCharacterRewards,
            "low_luck_borrowed_road",
            "死里借来的路应固定发放借来的路占位知识。"
        );
        AssertTrue(
            partyState.has_meta_flag(BuildMemberFlagId(LowLuckEventService.EventBorrowedRoad)),
            "死里借来的路命中后应写入 PartyState.meta_flags 去重。"
        );

        PartyState restoredPartyState = RoundTripPartyState(partyState);
        if (restoredPartyState == null)
            return;

        using LowLuckContext restoredContext = BuildContext(-5, restoredPartyState);
        restoredContext.Service.HandleFateEvent(
            "critical_fail",
            BuildCriticalFailPayload("borrowed_road_02", -5)
        );
        LowLuckEventResult secondResult = restoredContext.Service.HandleBattleResolution(
            BuildBattleInput("borrowed_road_02", memberAlive: true)
        );
        AssertEq(secondResult.TriggeredEventIds.Count, 0, "同周目重复满足大失败获胜条件时不应再次触发死里借来的路。");
        AssertEq(secondResult.PendingCharacterRewards.Count, 0, "去重后死里借来的路不应重复排入奖励。");
    }

    private LowLuckContext BuildContext(int hiddenLuckAtBirth, PartyState partyState = null)
    {
        PartyState resolvedPartyState = partyState ?? BuildPartyState(hiddenLuckAtBirth);
        CharacterManagementModule manager = new();
        manager.setup(resolvedPartyState, new GDictionary(), new GDictionary(), new GDictionary());
        LowLuckEventService service = new();
        service.Setup(manager);
        return new LowLuckContext(resolvedPartyState, manager, service);
    }

    private static PartyState BuildPartyState(int hiddenLuckAtBirth)
    {
        PartyState partyState = new()
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
        };
        partyState.active_member_ids.Add(HeroId);
        partyState.set_member_state(BuildMemberState(hiddenLuckAtBirth));
        return partyState;
    }

    private static PartyMemberState BuildMemberState(int hiddenLuckAtBirth)
    {
        PartyMemberState memberState = new()
        {
            member_id = HeroId,
            display_name = "Hero",
        };
        memberState.progression.unit_id = HeroId;
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 12;
        memberState.progression.unit_base_attributes.set_attribute_value(
            "hidden_luck_at_birth",
            hiddenLuckAtBirth
        );
        return memberState;
    }

    private static LowLuckFateEventPayload BuildHardshipPayload(
        StringName battleId,
        int hiddenLuckAtBirth
    )
    {
        return new LowLuckFateEventPayload(
            battleId,
            HeroId,
            true,
            new[] { new StringName("stunned") },
            hiddenLuckAtBirth
        );
    }

    private static LowLuckFateEventPayload BuildCriticalFailPayload(
        StringName battleId,
        int hiddenLuckAtBirth
    )
    {
        return new LowLuckFateEventPayload(
            battleId,
            HeroId,
            false,
            null,
            hiddenLuckAtBirth
        );
    }

    private static LowLuckBattleResolutionInput BuildBattleInput(
        StringName battleId,
        bool memberAlive
    )
    {
        return new LowLuckBattleResolutionInput(
            battleId,
            true,
            new[]
            {
                new LowLuckBattleUnitSnapshot(
                    "hero_unit",
                    HeroId,
                    "player",
                    memberAlive,
                    false,
                    false
                ),
            }
        );
    }

    private PartyState RoundTripPartyState(PartyState partyState)
    {
        PartyState restored = PartyState.from_dict(partyState.to_dict());
        AssertTrue(restored != null, "PartyState 带 meta_flags 时应能完成 round-trip。");
        return restored;
    }

    private static StringName BuildMemberFlagId(StringName eventId) =>
        new($"{LowLuckEventService.MetaFlagPrefix}{eventId}:{HeroId}");

    private static StringName BuildPartyFlagId(StringName eventId) =>
        new($"{LowLuckEventService.MetaFlagPrefix}{eventId}");

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName target)
    {
        StringName normalizedTarget = ProgressionDataUtils.to_string_name(target);
        foreach (StringName value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == normalizedTarget)
                return true;
        }
        return false;
    }

    private void AssertRewardEntryTarget(
        IReadOnlyList<PendingCharacterReward> rewards,
        StringName expectedTargetId,
        string message
    )
    {
        if (rewards.Count == 0 || rewards[0] == null || rewards[0].entries.Count == 0)
        {
            _failures.Add(message);
            return;
        }
        PendingCharacterRewardEntry entry = rewards[0].entries[0];
        AssertEq(entry.target_id.ToString(), expectedTargetId.ToString(), message);
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

    private sealed class LowLuckContext : IDisposable
    {
        internal LowLuckContext(
            PartyState partyState,
            CharacterManagementModule manager,
            LowLuckEventService service
        )
        {
            PartyState = partyState;
            Manager = manager;
            Service = service;
        }

        internal PartyState PartyState { get; }
        internal CharacterManagementModule Manager { get; }
        internal LowLuckEventService Service { get; }

        public void Dispose()
        {
            Service?.Dispose();
            Manager?.Dispose();
        }
    }
}

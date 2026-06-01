using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_fortune_service_regression : SceneTree
{
    private static readonly StringName HeroId = "hero";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestGrantsFortuneMarkAfterConfirmationSuccess();
        TestFailedConfirmationDoesNotGrantMark();
        TestRepeatAttemptIsLockedBeforeRolling();
        TestRuntimeAdapterParsesFateBusPayload();

        if (_failures.Count == 0)
        {
            GD.Print("FortuneService regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"FortuneService regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(FortuneService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "FortuneService 应是 plain C# service，不应继承 GodotObject/RefCounted。"
        );
        AssertEq(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length,
            0,
            "FortuneService 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("try_grant_fortune_mark_from_payload") == null,
            "FortuneService 不应保留 Dictionary payload 入口。"
        );
        AssertTrue(
            serviceType.GetMethod("set_confirmation_rng_for_testing") == null,
            "FortuneService 不应保留旧 GDS rng test hook。"
        );
    }

    private void TestGrantsFortuneMarkAfterConfirmationSuccess()
    {
        ServiceContext context = BuildServiceContext();
        FixedRollSource rollSource = new(40, 40);
        FortuneService service = new(_ => rollSource);
        service.Setup(context.Manager);

        bool granted = service.TryGrantFortuneMark(BuildInput("battle_success", 40));

        AssertTrue(granted, "二次确认成功后应授予 fortune_marked。");
        AssertEq(GetFortuneMarkedValue(context.Manager, HeroId), 1, "fortune_marked 应写入 1。");
        AssertTrue(service.HasAttemptedFortuneMark(HeroId), "成功授予后应记录本周目已尝试。");
        AssertTrue(
            context.PartyState.has_fate_run_flag(FortuneService.BuildFortuneMarkAttemptFlagId(HeroId)),
            "PartyState.fate_run_flags 应保留对应角色的尝试锁。"
        );
        AssertEq(rollSource.CallCount, 2, "劣势确认应消耗两次确认骰。");
    }

    private void TestFailedConfirmationDoesNotGrantMark()
    {
        ServiceContext context = BuildServiceContext();
        FixedRollSource rollSource = new(1, 1);
        FortuneService service = new(_ => rollSource);
        service.Setup(context.Manager);

        bool granted = service.TryGrantFortuneMark(BuildInput("battle_confirm_fail", 40));

        AssertFalse(granted, "二次确认失败时不应授予 fortune_marked。");
        AssertEq(GetFortuneMarkedValue(context.Manager, HeroId), 0, "二次确认失败时 fortune_marked 应保持 0。");
        AssertTrue(service.HasAttemptedFortuneMark(HeroId), "二次确认失败后仍应保留 per-run 尝试锁。");
        AssertEq(rollSource.CallCount, 2, "失败确认仍应消耗两次确认骰。");
    }

    private void TestRepeatAttemptIsLockedBeforeRolling()
    {
        ServiceContext context = BuildServiceContext();
        FixedRollSource activeRollSource = new(1, 1);
        FortuneService service = new(_ => activeRollSource);
        service.Setup(context.Manager);

        service.TryGrantFortuneMark(BuildInput("battle_repeat_lock", 40));
        FixedRollSource blockedRollSource = new(40, 40);
        activeRollSource = blockedRollSource;
        bool secondGranted = service.TryGrantFortuneMark(BuildInput("battle_repeat_lock_second", 40));

        AssertFalse(secondGranted, "同一角色本周目第二次事件不应再次尝试授予。");
        AssertEq(GetFortuneMarkedValue(context.Manager, HeroId), 0, "重复尝试被锁后不应写入 fortune_marked。");
        AssertEq(blockedRollSource.CallCount, 0, "重复尝试被锁后不应再消耗二次确认骰。");
    }

    private void TestRuntimeAdapterParsesFateBusPayload()
    {
        ServiceContext context = BuildServiceContext();
        var bus = new BattleFateEventBus();
        var fateRuntime = new FateRuntimeModule();
        fateRuntime.setup(context.Manager, bus);

        bus.dispatch(
            FortuneService.CriticalSuccessUnderDisadvantageEventId,
            new GDictionary
            {
                ["battle_id"] = "runtime_adapter",
                ["attacker_id"] = "hero_unit",
                ["attacker_member_id"] = HeroId,
                ["defender_id"] = "normal_target",
                ["is_disadvantage"] = true,
                ["crit_gate_die"] = 1,
            }
        );

        AssertEq(GetFortuneMarkedValue(context.Manager, HeroId), 1, "runtime adapter 应从 fate bus payload 授予 mark。");
        AssertTrue(
            context.PartyState.has_fate_run_flag(FortuneService.BuildFortuneMarkAttemptFlagId(HeroId)),
            "runtime adapter 成功授予后也应写入尝试锁。"
        );
        fateRuntime.dispose();
    }

    private static FortuneMarkEventInput BuildInput(StringName battleId, int critGateDie)
    {
        return new FortuneMarkEventInput
        {
            BattleId = battleId,
            AttackerId = "hero_unit",
            AttackerMemberId = HeroId,
            DefenderId = "target_unit",
            CritGateDie = critGateDie,
            IsDisadvantage = true,
        };
    }

    private static ServiceContext BuildServiceContext()
    {
        var partyState = new PartyState
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
            active_member_ids = new GStringNameArray { HeroId },
        };
        partyState.set_member_state(BuildMemberState(HeroId, "Hero"));

        var manager = new CharacterManagementModule();
        manager.setup(partyState, new GDictionary(), new GDictionary(), new GDictionary());

        return new ServiceContext(partyState, manager);
    }

    private static PartyMemberState BuildMemberState(StringName memberId, string displayName)
    {
        var memberState = new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = displayName;
        memberState.progression.unit_base_attributes.set_attribute_value(
            FortuneService.FortuneMarkedStatId,
            0
        );
        return memberState;
    }

    private static int GetFortuneMarkedValue(
        CharacterManagementModule manager,
        StringName memberId
    )
    {
        PartyMemberState memberState = manager.get_member_state(memberId);
        return memberState?.progression?.unit_base_attributes?.get_attribute_value(
            FortuneService.FortuneMarkedStatId
        ) ?? 0;
    }

    private sealed class FixedRollSource : FateAttackFormula.IRollSource
    {
        private readonly Queue<int> _rolls;

        public int CallCount { get; private set; }

        public FixedRollSource(params int[] rolls)
        {
            _rolls = new Queue<int>(rolls ?? Array.Empty<int>());
        }

        public int RandiRange(int minValue, int maxValue)
        {
            int lower = Math.Min(minValue, maxValue);
            int upper = Math.Max(minValue, maxValue);
            CallCount++;
            if (_rolls.Count == 0)
                return lower;
            return Math.Clamp(_rolls.Dequeue(), lower, upper);
        }
    }

    private sealed class ServiceContext
    {
        public ServiceContext(PartyState partyState, CharacterManagementModule manager)
        {
            PartyState = partyState;
            Manager = manager;
        }

        public PartyState PartyState { get; }
        public CharacterManagementModule Manager { get; }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool value, string message)
    {
        if (value)
        {
            _failures.Add(message);
        }
    }
}

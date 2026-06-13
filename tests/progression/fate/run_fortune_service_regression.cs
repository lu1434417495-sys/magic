using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_fortune_service_regression : SceneTree
{
    private static readonly StringName HeroId = "hero";

    private readonly TestHarness _test = new();

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

        return _test.Finish("FortuneService regression");
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(FortuneService);
        _test.True(
            serviceType.GetMethod("try_grant_fortune_mark_from_payload") == null,
            "FortuneService 不应保留 Dictionary payload 入口。"
        );
        _test.True(
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

        _test.True(granted, "二次确认成功后应授予 fortune_marked。");
        _test.Eq(GetFortuneMarkedValue(context.Manager, HeroId), 1, "fortune_marked 应写入 1。");
        _test.True(service.HasAttemptedFortuneMark(HeroId), "成功授予后应记录本周目已尝试。");
        _test.True(
            context.PartyState.HasFateRunFlag(FortuneService.BuildFortuneMarkAttemptFlagId(HeroId)),
            "PartyState.fate_run_flags 应保留对应角色的尝试锁。"
        );
        _test.Eq(rollSource.CallCount, 2, "劣势确认应消耗两次确认骰。");
    }

    private void TestFailedConfirmationDoesNotGrantMark()
    {
        ServiceContext context = BuildServiceContext();
        FixedRollSource rollSource = new(1, 1);
        FortuneService service = new(_ => rollSource);
        service.Setup(context.Manager);

        bool granted = service.TryGrantFortuneMark(BuildInput("battle_confirm_fail", 40));

        _test.False(granted, "二次确认失败时不应授予 fortune_marked。");
        _test.Eq(GetFortuneMarkedValue(context.Manager, HeroId), 0, "二次确认失败时 fortune_marked 应保持 0。");
        _test.True(service.HasAttemptedFortuneMark(HeroId), "二次确认失败后仍应保留 per-run 尝试锁。");
        _test.Eq(rollSource.CallCount, 2, "失败确认仍应消耗两次确认骰。");
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

        _test.False(secondGranted, "同一角色本周目第二次事件不应再次尝试授予。");
        _test.Eq(GetFortuneMarkedValue(context.Manager, HeroId), 0, "重复尝试被锁后不应写入 fortune_marked。");
        _test.Eq(blockedRollSource.CallCount, 0, "重复尝试被锁后不应再消耗二次确认骰。");
    }

    private void TestRuntimeAdapterParsesFateBusPayload()
    {
        ServiceContext context = BuildServiceContext();
        var bus = new BattleFateEventBus();
        var fateRuntime = new FateRuntimeModule();
        fateRuntime.Setup(context.Manager, bus);

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

        _test.Eq(GetFortuneMarkedValue(context.Manager, HeroId), 1, "runtime adapter 应从 fate bus payload 授予 mark。");
        _test.True(
            context.PartyState.HasFateRunFlag(FortuneService.BuildFortuneMarkAttemptFlagId(HeroId)),
            "runtime adapter 成功授予后也应写入尝试锁。"
        );
        fateRuntime.DisposeRuntime();
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
        partyState.SetMemberState(BuildMemberState(HeroId, "Hero"));

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
        memberState.progression.unit_base_attributes.SetAttributeValue(
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
        PartyMemberState memberState = manager.GetMemberState(memberId);
        return memberState?.progression?.unit_base_attributes?.GetAttributeValue(
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

}

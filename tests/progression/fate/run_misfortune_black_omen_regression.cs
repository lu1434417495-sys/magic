using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_misfortune_black_omen_regression : LifecycleTestSceneTree
{
    private static readonly StringName HeroId = "hero";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCursedRelicEliteOrBossVictoryHookGrantsDoomMark();
        TestMissingTypedCursedRelicItemDefDoesNotGrant();
        TestCursedRelicExplicitFlagHookGrantsDoomMark();
        TestBossCurseSurvivalVictoryHookGrantsDoomMark();
        TestBossCurseExplicitFlagHookGrantsDoomMark();
        TestIncompleteTypedPayloadsDoNotMeetConditions();
        TestDeadRoadLanternBlackOmenPathHookGrantsDoomMark();
        TestAlreadyMarkedMemberIsNotGrantedAgain();

        RequestTestExit(_test.Finish("Misfortune black omen regression"));
    }

    private void TestCursedRelicEliteOrBossVictoryHookGrantsDoomMark()
    {
        Context context = BuildContextWithCursedRelic();
        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.CursedRelicEliteOrBossVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                DefeatedEliteOrBoss = true,
            }
        );

        _test.True(result.Ok, "诅咒遗物 hook 应完成受控评估。");
        _test.True(result.ConditionsMet, "携带诅咒遗物并击败 elite/boss 时应满足黑兆条件。");
        _test.True(result.Granted, "满足诅咒遗物黑兆条件时应直接写入 doom_marked。");
        _test.Eq(GetDoomMarkedValue(context.Manager), 1, "诅咒遗物黑兆 hook 应把 doom_marked 写成 1。");
        context.Dispose();
    }

    private void TestMissingTypedCursedRelicItemDefDoesNotGrant()
    {
        ItemDef cursedRelic = BuildItemDef(
            "cursed_black_crown_shard",
            new[] { new StringName("cursed"), new StringName("relic") },
            new[] { new StringName("necklace") }
        );
        Context context = BuildContext(new Dictionary<StringName, ItemDef>());
        context.MemberState.equipment_state.SetEquippedEntry(
            "necklace",
            cursedRelic.item_id,
            BuildSlotArray("necklace"),
            EquipmentInstanceState.CreateInstance(
                cursedRelic.item_id,
                "eq_black_omen_missing_typed_item_def"
            )
        );

        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.CursedRelicEliteOrBossVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                DefeatedEliteOrBoss = true,
            }
        );

        AssertRejectedWithoutDoomMark(
            result,
            context.Manager,
            "typed item_defs 未提供诅咒遗物定义时"
        );
        context.Dispose();
    }

    private void TestCursedRelicExplicitFlagHookGrantsDoomMark()
    {
        Context context = BuildContextWithoutRelic();
        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.CursedRelicEliteOrBossVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                DefeatedEliteOrBoss = true,
                HasCursedRelic = true,
            }
        );

        _test.True(result.Ok, "显式 cursed relic hook 应完成受控评估。");
        _test.True(result.ConditionsMet, "显式 HasCursedRelic=true 时应满足诅咒遗物黑兆条件。");
        _test.True(result.Granted, "显式 cursed relic 黑兆条件满足时应直接写入 doom_marked。");
        _test.Eq(GetDoomMarkedValue(context.Manager), 1, "显式 cursed relic hook 应把 doom_marked 写成 1。");
        context.Dispose();
    }

    private void TestBossCurseSurvivalVictoryHookGrantsDoomMark()
    {
        Context context = BuildContextWithoutRelic();
        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.BossCurseSurvivalVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                BossEncounter = true,
                MemberSurvived = true,
                BossCurseStatusIds = new[] { new StringName("black_star_brand") },
            }
        );

        _test.True(result.Ok, "boss curse hook 应完成受控评估。");
        _test.True(result.ConditionsMet, "boss 专属诅咒下存活获胜时应满足黑兆条件。");
        _test.True(result.Granted, "满足 boss curse 黑兆条件时应直接写入 doom_marked。");
        _test.Eq(GetDoomMarkedValue(context.Manager), 1, "boss curse 黑兆 hook 应把 doom_marked 写成 1。");
        context.Dispose();
    }

    private void TestBossCurseExplicitFlagHookGrantsDoomMark()
    {
        Context context = BuildContextWithoutRelic();
        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.BossCurseSurvivalVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                BossEncounter = true,
                MemberSurvived = true,
                HasBossCurse = true,
            }
        );

        _test.True(result.Ok, "显式 boss curse hook 应完成受控评估。");
        _test.True(result.ConditionsMet, "显式 HasBossCurse=true 时应满足 boss curse 黑兆条件。");
        _test.True(result.Granted, "显式 boss curse 黑兆条件满足时应直接写入 doom_marked。");
        _test.Eq(GetDoomMarkedValue(context.Manager), 1, "显式 boss curse hook 应把 doom_marked 写成 1。");
        context.Dispose();
    }

    private void TestIncompleteTypedPayloadsDoNotMeetConditions()
    {
        Context cursedContext = BuildContextWithCursedRelic();
        MisfortuneBlackOmenResult badCursedResult = cursedContext.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.CursedRelicEliteOrBossVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = false,
                DefeatedEliteOrBoss = true,
            }
        );
        AssertRejectedWithoutDoomMark(
            badCursedResult,
            cursedContext.Manager,
            "EncounterWon=false 时"
        );
        cursedContext.Dispose();

        Context bossContext = BuildContextWithoutRelic();
        MisfortuneBlackOmenResult badBossResult = bossContext.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.BossCurseSurvivalVictory),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                EncounterWon = true,
                BossEncounter = true,
                MemberSurvived = true,
            }
        );
        AssertRejectedWithoutDoomMark(
            badBossResult,
            bossContext.Manager,
            "BossCurseStatusIds 为空且无显式 HasBossCurse 时"
        );
        bossContext.Dispose();
    }

    private void TestDeadRoadLanternBlackOmenPathHookGrantsDoomMark()
    {
        Context context = BuildContextWithDeadRoadLantern();
        MisfortuneBlackOmenResult result = context.Service.TryRunHook(
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.DeadRoadLanternBlackOmenPath),
            new MisfortuneBlackOmenHookPayload
            {
                MemberId = HeroId,
                PathTags = new[] { LowLuckRelicRules.ToStringName(LowLuckPathTagKind.BlackOmen) },
            }
        );

        _test.True(result.Ok, "亡途灯笼黑兆 hook 应完成受控评估。");
        _test.True(result.ConditionsMet, "亡途灯笼遇到黑兆路径时应满足黑兆条件。");
        _test.True(result.Granted, "亡途灯笼代价应把佩戴者卷入黑兆。");
        _test.Eq(GetDoomMarkedValue(context.Manager), 1, "亡途灯笼黑兆 hook 应把 doom_marked 写成 1。");
        context.Dispose();
    }

    private void TestAlreadyMarkedMemberIsNotGrantedAgain()
    {
        Context context = BuildContextWithoutRelic();
        context.MemberState.progression.unit_base_attributes.SetAttributeValue(
            MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID,
            1
        );

        MisfortuneBlackOmenResult result = context.Service.GrantDoomMark(
            HeroId,
            MisfortuneBlackOmenService.ToStringName(MisfortuneBlackOmenHookKind.BossCurseSurvivalVictory)
        );

        _test.True(result.Ok, "已标记成员重复 grant 仍应完成受控评估。");
        _test.True(result.ConditionsMet, "已标记成员重复 grant 仍应表示条件已满足。");
        _test.True(result.AlreadyMarked, "已标记成员重复 grant 应返回 AlreadyMarked。");
        _test.False(result.Granted, "已标记成员重复 grant 不应再次报告 Granted。");
        _test.Eq(result.DoomMarked, 1, "已标记成员重复 grant 应保留 doom_marked=1。");
        context.Dispose();
    }

    private static Context BuildContextWithCursedRelic()
    {
        ItemDef cursedRelic = BuildItemDef(
            "cursed_black_crown_shard",
            new[] { new StringName("cursed"), new StringName("relic") },
            new[] { new StringName("necklace") }
        );
        Context context = BuildContext(
            new Dictionary<StringName, ItemDef> { [cursedRelic.item_id] = cursedRelic }
        );
        context.MemberState.equipment_state.SetEquippedEntry(
            "necklace",
            cursedRelic.item_id,
            BuildSlotArray("necklace"),
            EquipmentInstanceState.CreateInstance(cursedRelic.item_id, "eq_black_omen_cursed_relic")
        );
        return context;
    }

    private static Context BuildContextWithDeadRoadLantern()
    {
        ItemDef lantern = BuildItemDef(
            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern),
            Array.Empty<StringName>(),
            new[] { new StringName("special_trinket") }
        );
        Context context = BuildContext(
            new Dictionary<StringName, ItemDef> { [lantern.item_id] = lantern }
        );
        context.MemberState.equipment_state.SetEquippedEntry(
            "special_trinket",
            lantern.item_id,
            BuildSlotArray("special_trinket"),
            EquipmentInstanceState.CreateInstance(
                lantern.item_id,
                "eq_dead_road_lantern_black_omen"
            )
        );
        return context;
    }

    private static Context BuildContextWithoutRelic() => BuildContext(new Dictionary<StringName, ItemDef>());

    private static Context BuildContext(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        PartyState partyState = new()
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
        };
        partyState.active_member_ids.Add(HeroId);

        PartyMemberState memberState = new()
        {
            member_id = HeroId,
            display_name = "Hero",
        };
        memberState.progression.unit_id = HeroId;
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 20;
        memberState.progression.unit_base_attributes.SetAttributeValue(
            MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID,
            0
        );
        partyState.SetMemberState(memberState);

        CharacterManagementModule manager = new();
        manager.setup(partyState);

        MisfortuneBlackOmenService service = new();
        service.Setup(manager, itemDefs);

        return new Context(partyState, memberState, manager, service);
    }

    private static ItemDef BuildItemDef(
        StringName itemId,
        IEnumerable<StringName> tags,
        IEnumerable<StringName> slotIds
    )
    {
        ItemDef itemDef = new()
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            item_category = "equipment",
            is_stackable = false,
            max_stack = 1,
            equipment_type_id = "accessory",
        };
        foreach (StringName slotId in slotIds)
            itemDef.equipment_slot_ids.Add(slotId.ToString());
        foreach (StringName tag in tags)
            itemDef.tags.Add(tag);
        return itemDef;
    }

    private static GStringNameArray BuildSlotArray(StringName slotId) =>
        new() { slotId };

    private static int GetDoomMarkedValue(CharacterManagementModule manager)
    {
        PartyMemberState memberState = manager.GetMemberState(HeroId);
        if (memberState?.progression?.unit_base_attributes == null)
            return 0;
        return memberState.progression.unit_base_attributes.GetAttributeValue(
            MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID
        );
    }

    private void AssertRejectedWithoutDoomMark(
        MisfortuneBlackOmenResult result,
        CharacterManagementModule manager,
        string message
    )
    {
        _test.False(result.ConditionsMet, $"{message}不应满足黑兆条件。");
        _test.False(result.Granted, $"{message}不应写入 doom_marked。");
        _test.True(
            result.ErrorCode == "conditions_not_met" || result.ErrorCode == "invalid_request",
            $"{message}应返回 conditions_not_met 或 invalid_request。actual={result.ErrorCode}"
        );
        _test.Eq(GetDoomMarkedValue(manager), 0, $"{message}不应改变 doom_marked。");
    }

    private sealed class Context
    {
        internal Context(
            PartyState partyState,
            PartyMemberState memberState,
            CharacterManagementModule manager,
            MisfortuneBlackOmenService service
        )
        {
            PartyState = partyState;
            MemberState = memberState;
            Manager = manager;
            Service = service;
        }

        internal PartyState PartyState { get; }
        internal PartyMemberState MemberState { get; }
        internal CharacterManagementModule Manager { get; }
        internal MisfortuneBlackOmenService Service { get; }

        internal void Dispose()
        {
            Service.Dispose();
            Manager.Dispose();
            RuntimeStateLifecycle.MarkValueGraphFinalizerless(
                PartyState,
                "run_misfortune_black_omen_regression.Context"
            );
            RuntimeStateLifecycle.MarkValueGraphFinalizerless(
                MemberState,
                "run_misfortune_black_omen_regression.Context"
            );
        }
    }
}

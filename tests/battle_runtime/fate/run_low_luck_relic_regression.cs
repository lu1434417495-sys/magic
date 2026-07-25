using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_low_luck_relic_regression : LifecycleTestSceneTree
{
    private static readonly StringName HeroId = "hero";
    private static readonly StringName AllyId = "ally";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestItemResourcesSurfaceEquipmentFlags();
        TestFixedRewardPoolUsesLowLuckFixedLootPath();
        TestBlackStarWedgeFirstHitIgnoresGuardAndAppliesExposed();
        TestBloodDebtShawlLowHpReductionAllyDownApAndRecoveryPenalty();
        TestDeadRoadLanternRevealsHiddenPaths();

        RequestTestExit(_test.Finish("Low luck relic regression"));
    }

    private void TestItemResourcesSurfaceEquipmentFlags()
    {
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions =
            LoadLowLuckItemDefinitions();
        var cases = new[]
        {
            (LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.ReverseFateAmulet), LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.ReverseFateAmulet)),
            (LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BlackStarWedge), LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BlackStarWedge)),
            (LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl), LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl)),
            (LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern), LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.DeadRoadLantern)),
        };

        foreach ((StringName itemId, StringName attributeId) in cases)
        {
            _test.True(
                HasItemDefinition(itemDefinitions, itemId),
                $"正式内容快照应提供 {itemId}。"
            );
            if (!HasItemDefinition(itemDefinitions, itemId))
                continue;

            AttributeSnapshot snapshot = BuildEquippedMemberSnapshot(
                itemDefinitions,
                itemId
            );
            _test.True(
                snapshot != null && snapshot.GetValue(attributeId) > 0,
                $"{itemId} 装备后应把 {attributeId} 写入属性快照。"
            );
        }
    }

    private void TestFixedRewardPoolUsesLowLuckFixedLootPath()
    {
        using LowLuckContext reverseContext = BuildLowLuckContext(-5, includeAlly: true);
        reverseContext.Service.HandleFateEvent(
            "critical_fail",
            BuildCriticalFailPayload("reverse_reward_battle", -5)
        );
        LowLuckEventResult reverseResult = reverseContext.Service.HandleBattleResolution(
            BuildRewardBattleInput("reverse_reward_battle", heroAlive: true, enemyIsElite: true, allyDead: false)
        );
        AssertFixedLootEntry(
            reverseResult.LootEntries,
            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.ReverseFateAmulet),
            "逆命护符应只通过 low_luck fixed loot 路径发放。"
        );

        using LowLuckContext wedgeContext = BuildLowLuckContext(-5, includeAlly: true);
        wedgeContext.Service.HandleFateEvent(
            "hardship_survival",
            BuildHardshipPayload("wedge_reward_battle", -5)
        );
        LowLuckEventResult wedgeResult = wedgeContext.Service.HandleBattleResolution(
            BuildRewardBattleInput("wedge_reward_battle", heroAlive: true, enemyIsElite: true, allyDead: false)
        );
        AssertFixedLootEntry(
            wedgeResult.LootEntries,
            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BlackStarWedge),
            "黑星楔钉应只通过 low_luck fixed loot 路径发放。"
        );

        using LowLuckContext shawlContext = BuildLowLuckContext(-5, includeAlly: true);
        LowLuckEventResult shawlResult = shawlContext.Service.HandleBattleResolution(
            BuildRewardBattleInput("shawl_reward_battle", heroAlive: true, enemyIsElite: false, allyDead: true)
        );
        AssertFixedLootEntry(
            shawlResult.LootEntries,
            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl),
            "血债披肩应只通过 low_luck fixed loot 路径发放。"
        );

        using LowLuckContext lanternContext = BuildLowLuckContext(-5, includeAlly: true);
        lanternContext.Service.HandleFateEvent(
            "hardship_survival",
            BuildHardshipPayload("lantern_reward_battle", -5)
        );
        lanternContext.Service.HandleFateEvent(
            "critical_fail",
            BuildCriticalFailPayload("lantern_reward_battle", -5)
        );
        LowLuckEventResult lanternResult = lanternContext.Service.HandleBattleResolution(
            BuildRewardBattleInput("lantern_reward_battle", heroAlive: true, enemyIsElite: false, allyDead: false)
        );
        AssertFixedLootEntry(
            lanternResult.LootEntries,
            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern),
            "亡途灯笼应只通过 low_luck fixed loot 路径发放。"
        );
    }

    private void TestBlackStarWedgeFirstHitIgnoresGuardAndAppliesExposed()
    {
        FixedHitMaxDamageResolver resolver = new();
        BattleUnitState baselineSource = BuildBattleUnit("基准楔钉者", "player");
        BattleUnitState baselineTarget = BuildGuardedTarget("守住的敌人");
        using GodotProjectionLease<GDictionary> baselineDamageResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            baselineSource,
            baselineTarget,
            new[] { BuildDamageEffect(18) },
            DamageResolutionContext.Empty()
        ));
        GDictionary baselineDamageResult = baselineDamageResultLease.Value;
        int baselineDamage = DictInt(baselineDamageResult, "damage");

        BattleUnitState wedgeSource = BuildBattleUnit("楔钉者", "player");
        wedgeSource.attribute_snapshot.SetValue(LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BlackStarWedge), 1);
        BattleUnitState wedgeTarget = BuildGuardedTarget("守住的敌人");
        using GodotProjectionLease<GDictionary> wedgeResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            wedgeSource,
            wedgeTarget,
            new[] { BuildDamageEffect(18) },
            DamageResolutionContext.Empty()
        ));
        GDictionary wedgeResult = wedgeResultLease.Value;
        GDictionary wedgeEvent = ExtractFirstDamageEvent(wedgeResult);

        _test.True(
            DictInt(wedgeEvent, "guard_ignore_applied") > 0,
            $"黑星楔钉的首击应记录 guard_ignore_applied。 event={wedgeEvent}"
        );
        _test.True(
            DictInt(wedgeResult, "damage") > baselineDamage,
            $"黑星楔钉首击应比未装备时打出更高伤害。 baseline={baselineDamage} actual={DictInt(wedgeResult, "damage")}"
        );
        _test.True(
            wedgeSource.HasStatusEffect(LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.BlackStarWedgeExposed)),
            "黑星楔钉未击杀目标时应给佩戴者挂上 1 回合破绽。"
        );

        BattleUnitState enemyAttacker = BuildBattleUnit("报复者", "enemy");
        BattleUnitState normalTarget = BuildBattleUnit("普通持有者", "player");
        BattleUnitState exposedTarget = wedgeSource;
        using GodotProjectionLease<GDictionary> normalIncomingLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
            resolver.ResolveEffects(
                enemyAttacker,
                normalTarget,
                new[] { BuildDamageEffect(16) },
                DamageResolutionContext.Empty()
            )
        );
        int normalIncoming = DictInt(normalIncomingLease.Value, "damage");
        using GodotProjectionLease<GDictionary> exposedIncomingLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
            resolver.ResolveEffects(
                enemyAttacker,
                exposedTarget,
                new[] { BuildDamageEffect(16) },
                DamageResolutionContext.Empty()
            )
        );
        int exposedIncoming = DictInt(exposedIncomingLease.Value, "damage");
        _test.True(
            exposedIncoming > normalIncoming,
            $"黑星楔钉代价应让佩戴者承受更高的后续伤害。 normal={normalIncoming} exposed={exposedIncoming}"
        );
    }

    private void TestBloodDebtShawlLowHpReductionAllyDownApAndRecoveryPenalty()
    {
        FixedHitMaxDamageResolver resolver = new();
        BattleUnitState enemyAttacker = BuildBattleUnit("压迫者", "enemy");
        BattleUnitState baselineTarget = BuildBattleUnit("普通目标", "player", hpMax: 100, currentHp: 35);
        BattleUnitState shawlTarget = BuildBattleUnit("披肩目标", "player", hpMax: 100, currentHp: 35);
        shawlTarget.attribute_snapshot.SetValue(LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl), 1);
        using GodotProjectionLease<GDictionary> normalDamageLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
            resolver.ResolveEffects(
                enemyAttacker,
                baselineTarget,
                new[] { BuildDamageEffect(20) },
                DamageResolutionContext.Empty()
            )
        );
        int normalDamage = DictInt(normalDamageLease.Value, "damage");
        using GodotProjectionLease<GDictionary> reducedDamageLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
            resolver.ResolveEffects(
                enemyAttacker,
                shawlTarget,
                new[] { BuildDamageEffect(20) },
                DamageResolutionContext.Empty()
            )
        );
        int reducedDamage = DictInt(reducedDamageLease.Value, "damage");
        _test.True(
            reducedDamage < normalDamage,
            $"血债披肩在低血时应降低承伤。 normal={normalDamage} reduced={reducedDamage}"
        );

        BattleRuntimeModule runtime = new();
        runtime.setup();
        BattleState state = BuildRuntimeState("blood_debt_runtime");
        BattleUnitState wearer = BuildBattleUnit("披肩佩戴者", "player", hpMax: 100, currentHp: 80, sourceMemberId: HeroId);
        wearer.attribute_snapshot.SetValue(LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl), 1);
        wearer.SetCurrentAp(1);
        BattleUnitState fallenAlly = BuildBattleUnit("倒地队友", "player", hpMax: 100, currentHp: 0, sourceMemberId: AllyId);
        fallenAlly.MarkDead();
        BattleUnitState enemy = BuildBattleUnit("敌人", "enemy", hpMax: 100, currentHp: 0, sourceMemberId: "enemy");
        AddUnitToState(state, wearer);
        AddUnitToState(state, fallenAlly);
        AddUnitToState(state, enemy);
        state.ally_unit_ids = new GStringNameArray { wearer.unit_id, fallenAlly.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        runtime.SetupStateForTests(state);
        runtime.ClearDefeatedUnit(fallenAlly, new BattleEventBatch());
        _test.Eq(wearer.GetCurrentAp(), 2, "血债披肩应在队友倒地时返还 1 点行动点。");
        runtime.Dispose();

        AssertBloodDebtRecoveryPenalty();
    }

    private void TestDeadRoadLanternRevealsHiddenPaths()
    {
        AttributeSnapshot lanternSnapshot = new();
        lanternSnapshot.SetValue(LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.DeadRoadLantern), 1);
        _test.True(
            LowLuckRelicRules.ShouldRevealHiddenPath(
                lanternSnapshot,
                new[] { LowLuckRelicRules.ToStringName(LowLuckPathTagKind.HiddenTrap), LowLuckRelicRules.ToStringName(LowLuckPathTagKind.BlackOmen) }
            ),
            "亡途灯笼应把隐藏陷阱和黑兆路径都标记为可见。"
        );
    }

    private void AssertBloodDebtRecoveryPenalty()
    {
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions =
            LoadLowLuckItemDefinitions();
        PartyState plainParty = BuildRestoreParty(
            equipBloodDebt: false,
            itemDefinitions
        );
        PartyMemberState plainMember = plainParty.GetMemberState(HeroId);
        GameRuntimeFacade plainRuntime = BuildRestoreRuntime(
            plainParty,
            itemDefinitions
        );
        AttributeSnapshot plainSnapshot = plainRuntime.GetMemberAttributeSnapshot(HeroId);
        int plainHpMax = Math.Max(plainSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), plainMember.current_hp);
        int plainMpMax = Math.Max(plainSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)), plainMember.current_mp);
        GameRuntimeSettlementCommandHandler plainHandler = new();
        plainHandler.SetupRuntime(plainRuntime);
        plainHandler.RestorePartyResources(1.0f, true);

        PartyState shawlParty = BuildRestoreParty(
            equipBloodDebt: true,
            itemDefinitions
        );
        PartyMemberState shawlMember = shawlParty.GetMemberState(HeroId);
        GameRuntimeFacade shawlRuntime = BuildRestoreRuntime(
            shawlParty,
            itemDefinitions
        );
        AttributeSnapshot shawlSnapshot = shawlRuntime.GetMemberAttributeSnapshot(HeroId);
        int shawlHpMax = Math.Max(shawlSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), shawlMember.current_hp);
        int shawlMpMax = Math.Max(shawlSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)), shawlMember.current_mp);
        int oldHp = shawlMember.current_hp;
        int oldMp = shawlMember.current_mp;
        int expectedHp = Math.Min(
            oldHp + (int)Math.Ceiling(Math.Max(shawlHpMax - oldHp, 0) * LowLuckRelicRules.BloodDebtRecoveryMultiplier),
            shawlHpMax
        );
        int expectedMp = Math.Min(
            oldMp + (int)Math.Ceiling(Math.Max(shawlMpMax - oldMp, 0) * LowLuckRelicRules.BloodDebtRecoveryMultiplier),
            shawlMpMax
        );
        GameRuntimeSettlementCommandHandler shawlHandler = new();
        shawlHandler.SetupRuntime(shawlRuntime);
        shawlHandler.RestorePartyResources(1.0f, true);

        _test.Eq(plainMember.current_hp, plainHpMax, "未装备血债披肩时 full restore 应恢复到 HP 上限。");
        _test.Eq(plainMember.current_mp, plainMpMax, "未装备血债披肩时 full restore 应恢复到 MP 上限。");
        _test.Eq(shawlMember.current_hp, expectedHp, "血债披肩代价应把 full restore 的 HP 恢复量减半。");
        _test.Eq(shawlMember.current_mp, expectedMp, "血债披肩代价应把 full restore 的 MP 恢复量减半。");
        _test.True(
            shawlMember.current_hp < shawlHpMax || oldHp >= shawlHpMax,
            "血债披肩恢复后 HP 不应直接回满。"
        );
        _test.True(
            shawlMember.current_mp < shawlMpMax || oldMp >= shawlMpMax,
            "血债披肩恢复后 MP 不应直接回满。"
        );

        plainRuntime.Dispose();
        shawlRuntime.Dispose();
    }

    private static IReadOnlyDictionary<StringName, ItemDefinition> LoadLowLuckItemDefinitions()
        => GameSessionTestFactory.GetProcessSnapshot().Items;

    private static bool HasItemDefinition(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName itemId
    )
    {
        return itemDefinitions != null && itemDefinitions.ContainsKey(itemId);
    }

    private static AttributeSnapshot BuildEquippedMemberSnapshot(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName itemId
    )
    {
        PartyState partyState = BuildPartyShell();
        PartyMemberState memberState = BuildMemberState(HeroId, hiddenLuckAtBirth: -5);
        memberState.equipment_state.SetEquippedEntry(
            SlotForItem(itemId),
            itemId,
            BuildSlotArray(SlotForItem(itemId)),
            EquipmentInstanceState.CreateInstance(itemId, $"eq_snapshot_{itemId}")
        );
        partyState.SetMemberState(memberState);

        using CharacterManagementModule manager = new();
        manager.setup(partyState, item_defs: itemDefinitions);
        return manager.GetMemberAttributeSnapshot(HeroId);
    }

    private static LowLuckContext BuildLowLuckContext(int hiddenLuckAtBirth, bool includeAlly)
    {
        PartyState partyState = BuildPartyShell();
        if (includeAlly)
            partyState.active_member_ids.Add(AllyId);
        partyState.SetMemberState(BuildMemberState(HeroId, hiddenLuckAtBirth));
        if (includeAlly)
            partyState.SetMemberState(BuildMemberState(AllyId, 0));

        CharacterManagementModule manager = new();
        manager.setup(partyState);
        LowLuckEventService service = new();
        service.Setup(manager);
        return new LowLuckContext(partyState, manager, service);
    }

    private static PartyState BuildPartyShell()
    {
        PartyState partyState = new()
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
        };
        partyState.active_member_ids.Add(HeroId);
        return partyState;
    }

    private static PartyMemberState BuildMemberState(StringName memberId, int hiddenLuckAtBirth)
    {
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = memberId.ToString(),
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = memberId.ToString();
        memberState.progression.character_level = 10;
        memberState.progression.unit_base_attributes.SetAttributeValue(
            "hidden_luck_at_birth",
            hiddenLuckAtBirth
        );
        memberState.progression.unit_base_attributes.SetAttributeValue(
            AttributeService.ToStringName(AttributeIdKind.HpMax),
            100
        );
        memberState.progression.unit_base_attributes.SetAttributeValue(
            AttributeService.ToStringName(AttributeIdKind.MpMax),
            30
        );
        return memberState;
    }

    private static LowLuckBattleResolutionInput BuildRewardBattleInput(
        StringName battleId,
        bool heroAlive,
        bool enemyIsElite,
        bool allyDead
    )
    {
        List<LowLuckBattleUnitSnapshot> units = new()
        {
            new("hero_unit", HeroId, "player", heroAlive, false, false),
        };

        if (allyDead)
        {
            units.Add(new LowLuckBattleUnitSnapshot("ally_unit", AllyId, "player", false, false, false));
        }

        units.Add(new LowLuckBattleUnitSnapshot("enemy_unit", "enemy_member", "enemy", false, true, enemyIsElite));
        return new LowLuckBattleResolutionInput(battleId, true, units);
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
            new[] { new StringName("staggered") },
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

    private static BattleUnitState BuildBattleUnit(
        string displayName,
        StringName factionId,
        int hpMax = 100,
        int currentHp = 100,
        StringName sourceMemberId = default
    )
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = displayName,
            display_name = displayName,
            faction_id = factionId,
            source_member_id = sourceMemberId,
        }.WithCombatResourcesForTest(
            hp: currentHp,
            mp: 0,
            ap: 1,
            isAlive: currentHp > 0
        );
        unit.SetAnchorCoord(Vector2I.Zero);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 0);
        return unit;
    }

    private static BattleUnitState BuildGuardedTarget(string displayName)
    {
        BattleUnitState target = BuildBattleUnit(displayName, "enemy", hpMax: 120, currentHp: 120);
        BattleStatusEffectState guardStatus = new()
        {
            status_id = "test_guard",
            power = 1,
            stacks = 1,
            duration = 60,
            guard_block = 4,
        };
        target.SetStatusEffect(guardStatus);
        return target;
    }

    private static CombatEffectDefinition BuildDamageEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: power
        );

    private static BattleState BuildRuntimeState(StringName battleId)
    {
        BattleState state = new()
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = new Vector2I(3, 3),
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
            {
                Vector2I coord = new(x, y);
                BattleCellState cell = new()
                {
                    coord = coord,
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static void AddUnitToState(BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
    }

    private static PartyState BuildRestoreParty(
        bool equipBloodDebt,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        PartyState partyState = BuildPartyShell();
        PartyMemberState memberState = BuildMemberState(HeroId, hiddenLuckAtBirth: -5);
        memberState.current_hp = 20;
        memberState.current_mp = 10;
        if (equipBloodDebt)
        {
            memberState.equipment_state.SetEquippedEntry(
                SlotForItem(LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl)),
                LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl),
                BuildSlotArray(SlotForItem(LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl))),
                EquipmentInstanceState.CreateInstance(
                    LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl),
                    "eq_restore_blood_debt_shawl"
                )
            );
        }
        partyState.SetMemberState(memberState);
        return partyState;
    }

    private static GameRuntimeFacade BuildRestoreRuntime(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        GameRuntimeFacade runtime = new();
        runtime.SetPartyState(partyState);
        runtime._character_management.setup(
            partyState,
            item_defs: itemDefinitions
        );
        return runtime;
    }

    private static StringName SlotForItem(StringName itemId)
    {
        if (itemId == LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern))
            return "special_trinket";
        if (itemId == LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl))
            return "cloak";
        if (itemId == LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.ReverseFateAmulet))
            return "necklace";
        if (itemId == LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BlackStarWedge))
            return "badge";
        return "necklace";
    }

    private static GStringNameArray BuildSlotArray(StringName slotId) => new() { slotId };

    private static GDictionary ExtractFirstDamageEvent(GDictionary result)
    {
        GArray damageEvents = GetArray(result, "damage_events");
        if (damageEvents.Count == 0)
            return new GDictionary();
        Variant first = damageEvents[0];
        return first.VariantType == Variant.Type.Dictionary
            ? first.AsGodotDictionary()
            : new GDictionary();
    }

    private void AssertFixedLootEntry(
        IReadOnlyList<LowLuckLootEntry> lootEntries,
        StringName itemId,
        string message
    )
    {
        bool foundEntry = TryFindLootEntry(lootEntries, itemId, out LowLuckLootEntry lootEntry);
        _test.True(foundEntry, message);
        if (!foundEntry)
            return;
        _test.Eq(
            BattleLootIds.ToStringName(lootEntry.DropKind).ToString(),
            "item",
            $"{message} | fixed low luck 奖励必须是 drop_type=item。"
        );
        _test.Eq(
            BattleLootIds.ToStringName(lootEntry.DropSourceKind).ToString(),
            "low_luck_event",
            $"{message} | fixed low luck 奖励必须写成 drop_source_kind=low_luck_event。"
        );
    }

    private static bool TryFindLootEntry(
        IReadOnlyList<LowLuckLootEntry> lootEntries,
        StringName itemId,
        out LowLuckLootEntry foundEntry
    )
    {
        foundEntry = default;
        if (lootEntries == null)
            return false;
        foreach (LowLuckLootEntry lootEntry in lootEntries)
        {
            if (lootEntry.ItemId == itemId)
            {
                foundEntry = lootEntry;
                return true;
            }
        }
        return false;
    }

    private static GArray GetArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new GArray();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed) ? parsed : fallback,
            _ => fallback,
        };
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

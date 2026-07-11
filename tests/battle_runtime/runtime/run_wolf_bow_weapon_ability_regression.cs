using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_wolf_bow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName WolfBowItemId = "weapon_unique_bow_wolf_325";
    private static readonly StringName WolfPackTacticsTraitId =
        "weapon.bow.wolf.pack_tactics";
    private static readonly StringName WolfSpiritTraitId =
        "weapon.bow.wolf.spirit_summon";
    private static readonly StringName PackTacticsBindingId =
        "binding.weapon.bow.wolf.pack_tactics";
    private static readonly StringName WolfSpiritBindingId =
        "binding.weapon.bow.wolf.spirit_summon";
    private static readonly StringName WolfSpiritSkillId =
        "weapon_bow_wolf_spirit_summon";
    private static readonly StringName WolfSpiritGrantId =
        "grant.wolf_bow.spirit_summon.skill";
    private static readonly StringName WolfStateKey = "wolf_bow_ghost_wolf";
    private static readonly StringName TurnUseExhaustedReason =
        "equipment_skill_turn_use_exhausted";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestWolfBowProjectsContentAndGrantedSkill();
            TestWolfSpiritSummonUsageCreatesFullTemporaryBattleUnit();
            TestGhostWolfBasicAttackAndExpiryCleanup();
            TestPackTacticsUsesGenericNearbyAllyFact();
            RequestTestExit(_test.Finish("Wolf Bow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Wolf Bow weapon ability regression"));
        }
    }

    private void TestWolfBowProjectsContentAndGrantedSkill()
    {
        using WolfFixture fixture = WolfFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(WolfBowItemId), "真实物品内容应包含狼牙弓。");
        _test.True(fixture.TraitDefs.ContainsKey(WolfPackTacticsTraitId), "真实 trait 内容应包含狼群战术。");
        _test.True(fixture.TraitDefs.ContainsKey(WolfSpiritTraitId), "真实 trait 内容应包含狼灵召唤。");
        _test.True(fixture.Bindings.ContainsKey(PackTacticsBindingId), "真实装备能力内容应包含狼群战术 binding。");
        _test.True(fixture.Bindings.ContainsKey(WolfSpiritBindingId), "真实装备能力内容应包含狼灵召唤 binding。");
        _test.True(fixture.SkillDefs.ContainsKey(WolfSpiritSkillId), "真实技能内容应包含狼灵召唤装备技能。");

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longbow_wolf.tres"
        );
        _test.True(rawItem != null, "狼牙弓原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_longbow_base"), "狼牙弓应继承 longbow 模板。");
            _test.Eq(rawItem.base_price, 42000, "狼牙弓价格应为 42000。");
            _test.True(ContainsStringName(rawItem.tags, "wolf_bow"), "狼牙弓物品 tag 应包含 wolf_bow。");
            _test.True(rawItem.trait_ids.Contains(WolfPackTacticsTraitId), "狼牙弓应固定声明狼群战术 trait。");
            _test.True(rawItem.trait_ids.Contains(WolfSpiritTraitId), "狼牙弓应固定声明狼灵召唤 trait。");
            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "狼牙弓应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.attack_range, 10, "狼牙弓攻击距离应为 10。");
                _test.Eq(profile.damage_tag, new StringName("physical_pierce"), "狼牙弓基础伤害标签应为 physical_pierce。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 1, "狼牙弓双手伤害应为 1D8+2。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 8, "狼牙弓双手伤害应为 1D8+2。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 2, "狼牙弓双手伤害应为 1D8+2。");
                _test.True(profile.properties.Contains("two_handed"), "狼牙弓应声明 two_handed property。");
                _test.True(profile.properties.Contains("heavy"), "狼牙弓应声明 heavy property。");
            }
        }

        BattleUnitState equipped = fixture.BuildWolfUnit("projection");
        _test.Eq(equipped.weapon_item_id, WolfBowItemId, "狼牙弓装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longbow"), "狼牙弓应投影为 longbow。");
        _test.Eq(equipped.weapon_family, new StringName("bow"), "狼牙弓应保留 bow 家族。");
        _test.Eq(equipped.weapon_attack_range, 10, "狼牙弓攻击距离应投影为 10。");
        _test.True(equipped.weapon_uses_two_hands, "狼牙弓应占用双手。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "狼牙弓应造成穿刺物理伤害。");
        AssertUnitHasTraitAndAbilitySource(equipped, WolfPackTacticsTraitId, PackTacticsBindingId, "eq_wolf_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, WolfSpiritTraitId, WolfSpiritBindingId, "eq_wolf_projection");

        BattleState state = BuildState("wolf_skill_view", equipped, null, worldStep: 0, currentTu: 0);
        fixture.Runtime.SetupStateForTests(state);
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, equipped, state, 0);
        _test.True(TryFindSkillEntry(view, WolfSpiritSkillId, out BattleAvailableSkillEntry entry), "装备狼牙弓后 unit 应有狼灵召唤技能入口。");
        if (entry != null)
        {
            _test.True(entry.IsSelectable, "未使用前狼灵召唤应可选。");
            _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "狼灵召唤入口来源应是 equipment_skill。");
            _test.Eq(entry.EquipmentBindingId, WolfSpiritBindingId, "狼灵召唤入口应携带 binding id。");
            _test.Eq(entry.EquipmentGrantedActionId, WolfSpiritGrantId, "狼灵召唤入口应携带 grant id。");
            _test.Eq(entry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerWorldDay, "狼灵召唤应声明 per_world_day。");
            _test.Eq(entry.EquipmentMaxUsesPerPeriod, 1, "狼灵召唤每日应有 1 次。");
        }

        AssertWolfSkillConfig(fixture);
        AssertWolfAbilityPayloads(fixture);
    }

    private void TestWolfSpiritSummonUsageCreatesFullTemporaryBattleUnit()
    {
        using WolfFixture fixture = WolfFixture.Build(new GArray { 1 });
        BattleUnitState holder = fixture.BuildWolfUnit("summon");
        holder.SetAnchorCoord(new Vector2I(2, 2));
        BattleState state = BuildState("wolf_summon", holder, null, worldStep: 0, currentTu: 10);
        fixture.Runtime.SetupStateForTests(state);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0
        );
        BattleCommand command = BuildWolfSummonCommand(holder, holder, entry);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"狼灵召唤 preview 应允许。logs={JoinLogs(preview)}");

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        List<BattleUnitState> wolves = FindLivingWolves(state, holder);
        _test.Eq(wolves.Count, 1, "狼灵召唤应创建一只真实战斗单位。");
        BattleUnitState wolf = wolves.Count > 0 ? wolves[0] : null;
        _test.True(wolf != null, "应能找到召唤出的幽灵狼。");
        if (wolf == null)
            return;

        _test.True(state.GetUnit(wolf.unit_id) == wolf, "幽灵狼必须进入 BattleState unit store。");
        _test.True(state.ally_unit_ids.Contains(wolf.unit_id), "玩家持有者召唤的幽灵狼应进入友方列表。");
        _test.Eq(wolf.faction_id, holder.faction_id, "幽灵狼阵营应跟随来源单位。");
        _test.True(wolf.ai_blackboard?.summoned == true, "幽灵狼应标记 summoned。");
        _test.True(wolf.ai_blackboard?.temporary_unit == true, "幽灵狼应标记 temporary。");
        _test.Eq(wolf.ai_blackboard?.summon_source_unit_id ?? new StringName(""), holder.unit_id, "幽灵狼应记录来源单位。");
        _test.Eq(wolf.ai_blackboard?.summon_source_equipment_instance_id ?? new StringName(""), new StringName("eq_wolf_summon"), "幽灵狼应记录来源装备实例。");
        _test.Eq(wolf.ai_blackboard?.summon_binding_id ?? new StringName(""), WolfSpiritBindingId, "幽灵狼应记录来源 binding。");
        _test.Eq(wolf.ai_blackboard?.summon_state_key ?? new StringName(""), WolfStateKey, "幽灵狼应记录 summon state key。");
        _test.Eq(wolf.ai_blackboard?.summon_expires_at_tu ?? -1, 70, "幽灵狼应在当前 TU + 60 到期。");
        _test.True(ContainsStringName(wolf.known_active_skill_ids, WeaponAbilityCommandTestSupport.BasicAttackSkillId), "幽灵狼应拥有 basic_attack。");
        _test.Eq(wolf.weapon_profile_kind, new StringName("natural"), "幽灵狼应投影为 natural weapon。");
        _test.Eq(wolf.weapon_profile_type_id, new StringName("wolf_fang"), "幽灵狼天生武器 profile id 应稳定。");
        _test.Eq(wolf.weapon_one_handed_dice?.dice_count ?? 0, 1, "幽灵狼天生武器应为 1D6。");
        _test.Eq(wolf.weapon_one_handed_dice?.dice_sides ?? 0, 6, "幽灵狼天生武器应为 1D6。");
        _test.Eq(wolf.weapon_physical_damage_tag, new StringName("physical_pierce"), "幽灵狼天生武器应使用已有物理穿刺标签。");
        _test.True(ContainsStringName(wolf.creature_type_tags, "summoned"), "幽灵狼应带 summoned creature tag。");
        _test.True(ContainsStringName(wolf.creature_type_tags, "beast"), "幽灵狼应带 beast creature tag。");
        foreach (Vector2I coord in wolf.GetOccupiedCoordsTyped())
        {
            _test.Eq(state.GetCell(coord)?.occupant_unit_id ?? new StringName(""), wolf.unit_id, "幽灵狼应占用 grid cell。");
        }

        EquipmentInstanceState instance = FindEquippedInstance(holder, "eq_wolf_summon");
        _test.True(instance != null, "狼灵召唤测试应能找到装备实例。");
        if (instance != null)
        {
            _test.Eq(
                EquipmentAbilityUsageRuntime.GetUsedCount(
                    instance,
                    WolfSpiritGrantId,
                    EquipmentAbilityUsagePeriodKind.PerWorldDay,
                    WorldTimeSystem.StepToDay(0)
                ),
                1,
                "狼灵召唤第一次使用后应写入当前世界日使用次数。"
            );
        }

        BattleSkillAvailabilityView sameTurnView = BuildEquipmentSkillView(fixture, holder, state, 0);
        _test.True(TryFindSkillEntry(sameTurnView, WolfSpiritSkillId, out BattleAvailableSkillEntry sameTurnEntry), "同回合使用后狼灵召唤入口仍应可见。");
        _test.False(sameTurnEntry?.IsSelectable ?? true, "同一行动回合内狼灵召唤不能第二次使用。");
        _test.Eq(sameTurnEntry?.DisabledReason ?? new StringName(""), TurnUseExhaustedReason, "同一行动回合第 2 次应返回行动回合一次限制原因。");

        BattleSkillAccessResult sameTurnAccess = new BattleSkillAvailabilityService(
            fixture.SkillDefs,
            fixture.Bindings
        ).ValidateSkillEntryAccess(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.PreviewExecution,
                WorldStep = 0,
                BattleState = state,
            },
            entry.EntryRef.SkillEntryId,
            WolfSpiritSkillId
        );
        _test.Eq(sameTurnAccess.ErrorCode, TurnUseExhaustedReason, "同一行动回合第 2 次 access gate 应返回 equipment_skill_turn_use_exhausted。");

        holder.ResetPerTurnCharges();
        BattleSkillAvailabilityView exhaustedView = BuildEquipmentSkillView(fixture, holder, state, 0);
        _test.True(TryFindSkillEntry(exhaustedView, WolfSpiritSkillId, out BattleAvailableSkillEntry exhaustedEntry), "同日使用后狼灵召唤入口仍应可见。");
        _test.False(exhaustedEntry?.IsSelectable ?? true, "每日一次用尽后狼灵召唤应不可用。");
        _test.Eq(exhaustedEntry?.DisabledReason ?? new StringName(""), new StringName("equipment_skill_usage_exhausted"), "跨行动回合后同日第 2 次应返回每日次数耗尽。");

        BattleSkillAvailabilityView nextDayView = BuildEquipmentSkillView(fixture, holder, state, 15);
        _test.True(TryFindSkillEntry(nextDayView, WolfSpiritSkillId, out BattleAvailableSkillEntry nextDayEntry), "次日狼灵召唤入口应仍能解析。");
        _test.True(nextDayEntry?.IsSelectable == true, "跨世界日后狼灵召唤应恢复可用。");
    }

    private void TestGhostWolfBasicAttackAndExpiryCleanup()
    {
        using WolfFixture fixture = WolfFixture.Build(new GArray { 4 });
        BattleUnitState holder = fixture.BuildWolfUnit("wolf_attack");
        holder.SetAnchorCoord(new Vector2I(2, 2));
        BattleUnitState target = BuildEnemy("wolf_attack_target", new Vector2I(2, 0), hp: 20);
        BattleState state = BuildState("wolf_attack_and_expiry", holder, target, worldStep: 0, currentTu: 10);
        fixture.Runtime.SetupStateForTests(state);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(fixture, holder, state, 0);
        using BattleEventBatch summonBatch = fixture.Runtime.IssueCommand(
            BuildWolfSummonCommand(holder, holder, entry)
        );
        BattleUnitState wolf = FirstLivingWolf(state, holder);
        _test.True(wolf != null, "测试应先召唤幽灵狼。");
        if (wolf == null)
            return;
        target.SetAnchorCoord(new Vector2I(wolf.coord.X, Math.Max(wolf.coord.Y - 1, 0)));
        target.RefreshFootprint();
        wolf.RefreshFootprint();

        int hpBefore = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            wolf,
            target,
            "ghost_wolf_basic_attack",
            previewCommand: true
        );
        _test.Eq(
            hpBefore - target.current_hp,
            4,
            "固定骰 4 时，幽灵狼 basic_attack 应通过 1D6 天生武器造成真实物理伤害。"
        );

        state.timeline.current_tu = 69;
        _test.False(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveTurnEnd(
                new BattleEquipmentAbilityTurnEndContext
                {
                    SourceUnit = holder,
                    BattleState = state,
                }
            ),
            "60TU 到期前不应清理幽灵狼。"
        );
        _test.True(wolf.is_alive, "60TU 到期前幽灵狼应仍存活。");

        state.timeline.current_tu = 70;
        _test.True(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveTurnEnd(
                new BattleEquipmentAbilityTurnEndContext
                {
                    SourceUnit = holder,
                    BattleState = state,
                }
            ),
            "到达 expires TU 时应清理幽灵狼。"
        );
        _test.False(wolf.is_alive, "到期清理应使幽灵狼不再存活。");
        foreach (Vector2I coord in wolf.GetOccupiedCoordsTyped())
        {
            _test.True(
                state.GetCell(coord)?.occupant_unit_id != wolf.unit_id,
                "到期清理应释放幽灵狼占用的 grid cell。"
            );
        }
    }

    private void TestPackTacticsUsesGenericNearbyAllyFact()
    {
        using WolfFixture fixture = WolfFixture.Build();
        BattleUnitState lone = fixture.BuildWolfUnit("lone");
        lone.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState target = BuildEnemy("lone_target", new Vector2I(6, 1), hp: 30);
        BattleState loneState = BuildState("wolf_lone_tactics", lone, target, worldStep: 0, currentTu: 0);
        fixture.Runtime.SetupStateForTests(loneState);
        BattleAttackRollModifierBundle loneBundle = BuildAttackModifierBundle(
            fixture,
            loneState,
            lone,
            target
        );
        _test.True(
            HasModifier(loneBundle, PackTacticsBindingId, -2),
            "30ft 内没有友军时，狼群战术应通过 generic nearby_ally_count fact 提供 -2。"
        );

        BattleUnitState supported = fixture.BuildWolfUnit("supported");
        supported.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState supportedTarget = BuildEnemy("supported_target", new Vector2I(6, 1), hp: 30);
        BattleUnitState nearbyAlly = BuildAlly("nearby_pack_ally", new Vector2I(4, 1), hp: 30);
        BattleState supportedState = BuildState("wolf_supported_tactics", supported, supportedTarget, worldStep: 0, currentTu: 0);
        AddUnitToState(fixture.Runtime, supportedState, nearbyAlly, ally: true);
        fixture.Runtime.SetupStateForTests(supportedState);
        BattleAttackRollModifierBundle supportedBundle = BuildAttackModifierBundle(
            fixture,
            supportedState,
            supported,
            supportedTarget
        );
        _test.True(
            HasModifier(supportedBundle, PackTacticsBindingId, 3),
            "30ft 内有友军时，狼群战术应通过 generic nearby_ally_count fact 提供 +3。"
        );

        BattleUnitState distant = fixture.BuildWolfUnit("distant");
        distant.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState distantTarget = BuildEnemy("distant_target", new Vector2I(6, 1), hp: 30);
        BattleUnitState farAlly = BuildAlly("far_pack_ally", new Vector2I(8, 8), hp: 30);
        BattleState distantState = BuildState("wolf_distant_tactics", distant, distantTarget, worldStep: 0, currentTu: 0, mapSize: new Vector2I(10, 10));
        AddUnitToState(fixture.Runtime, distantState, farAlly, ally: true);
        fixture.Runtime.SetupStateForTests(distantState);
        BattleAttackRollModifierBundle distantBundle = BuildAttackModifierBundle(
            fixture,
            distantState,
            distant,
            distantTarget
        );
        _test.True(
            HasModifier(distantBundle, PackTacticsBindingId, -2),
            "超过 6 格的友军不应满足狼群战术。"
        );
    }

    private static void AssertWolfSkillConfig(WolfFixture fixture)
    {
        if (!fixture.SkillDefs.TryGetValue(WolfSpiritSkillId, out SkillDefinition skill))
            throw new InvalidOperationException("狼灵召唤 SkillDef 缺失。");
        CombatSkillDefinition combat = skill.CombatProfile;
        if (combat == null)
            throw new InvalidOperationException("狼灵召唤应有 combat_profile。");
        if (combat.TargetMode != "unit" || combat.TargetTeamFilter != "ally")
            throw new InvalidOperationException("狼灵召唤应选择一个盟友或自身作为召唤锚点。");
        if (combat.RangeValue != 6)
            throw new InvalidOperationException("狼灵召唤应可选择 6 格内锚点。");
        if (combat.ApCost != 1)
            throw new InvalidOperationException("狼灵召唤应消耗 1 AP。");
    }

    private static void AssertWolfAbilityPayloads(WolfFixture fixture)
    {
        if (!fixture.Bindings.TryGetValue(WolfSpiritBindingId, out EquipmentAbilityBindingDefinition summonBinding))
            throw new InvalidOperationException("狼灵召唤 binding 缺失。");
        if (summonBinding.GrantedActions.Count != 1)
            throw new InvalidOperationException("狼灵召唤 binding 应授予一个装备技能。");
        EquipmentGrantedActionDefinition grant = summonBinding.GrantedActions[0];
        if (grant.GrantedActionId != WolfSpiritGrantId || grant.SkillId != WolfSpiritSkillId)
            throw new InvalidOperationException("狼灵召唤 grant id/skill id 应稳定。");
        if (grant.UsagePeriodKind != EquipmentAbilityUsagePeriodKind.PerWorldDay || grant.MaxUsesPerPeriod != 1)
            throw new InvalidOperationException("狼灵召唤应配置为 per_world_day 每日 1 次。");

        EquipmentAbilityActionDefinition summonAction = summonBinding.Reactions?[0]?.Actions?[0];
        if (summonAction?.PayloadDefinition is not SummonUnitsActionPayloadDefinition payload)
            throw new InvalidOperationException("狼灵召唤 after_skill action 应投影为 summon_units payload。");
        if (payload.StateKey != WolfStateKey)
            throw new InvalidOperationException("幽灵狼 state key 应稳定。");
        if (payload.CountDice?.Terms?.Count != 1 || payload.CountDice.Terms[0].DiceCount != 1 || payload.CountDice.Terms[0].DiceSides != 1)
            throw new InvalidOperationException("狼灵召唤应固定创建 1 只幽灵狼。");
        if (payload.DurationTu != 60)
            throw new InvalidOperationException("幽灵狼持续时间必须是 60TU。");
        if (payload.UnitDisplayName != "幽灵狼")
            throw new InvalidOperationException("狼灵召唤应生成幽灵狼。");
        if (payload.HpMax <= 0 || payload.ArmorClass <= 0 || payload.ActionPoints <= 0)
            throw new InvalidOperationException("幽灵狼应拥有完整战斗属性。");

        if (!fixture.Bindings.TryGetValue(PackTacticsBindingId, out EquipmentAbilityBindingDefinition tacticsBinding))
            throw new InvalidOperationException("狼群战术 binding 缺失。");
        if (tacticsBinding.Reactions.Count != 2)
            throw new InvalidOperationException("狼群战术应有有友军/无友军两个 attack roll modifier 反应。");
    }

    private static BattleAttackRollModifierBundle BuildAttackModifierBundle(
        WolfFixture fixture,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("wolf_fixture_attack");
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        return attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                attackSkill,
                "skill_attack_check",
                "wolf_pack_tactics",
                force_hit_no_crit: false
            )
        );
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private static BattleCommand BuildWolfSummonCommand(
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry
    )
    {
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            WolfSpiritSkillId
        );
        command.target_coord = target?.coord ?? user?.coord ?? Vector2I.Zero;
        return command;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        WolfFixture fixture,
        BattleUnitState holder,
        BattleState state,
        int worldStep
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state, worldStep);
        if (!TryFindSkillEntry(view, WolfSpiritSkillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException("missing wolf spirit equipment skill entry.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"wolf spirit entry disabled: {entry.DisabledReason}");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        WolfFixture fixture,
        BattleUnitState unit,
        BattleState state,
        int worldStep
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = worldStep,
                BattleState = state,
            }
        );
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry result
    )
    {
        result = null;
        foreach (BattleAvailableSkillEntry entry in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        BattleUnitState target,
        int worldStep,
        int currentTu,
        Vector2I mapSize = default
    )
    {
        return WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            target,
            worldStep,
            currentTu,
            mapSize == default ? new Vector2I(9, 9) : mapSize
        );
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = BuildPlainUnit(unitId, coord, hp);
        unit.faction_id = "enemy";
        return unit;
    }

    private static BattleUnitState BuildAlly(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = BuildPlainUnit(unitId, coord, hp);
        unit.faction_id = "player";
        return unit;
    }

    private static BattleUnitState BuildPlainUnit(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            is_alive = true,
            current_hp = hp,
            current_ap = 2,
            current_stamina = 30,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 12);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool ally
    )
    {
        state.SetUnit(unit);
        if (ally)
        {
            if (!state.ally_unit_ids.Contains(unit.unit_id))
                state.ally_unit_ids.Add(unit.unit_id);
        }
        else if (!state.enemy_unit_ids.Contains(unit.unit_id))
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        if (!runtime._grid_service.PlaceUnit(state, unit, unit.coord, true))
            throw new InvalidOperationException($"unable to place unit {unit.unit_id} at {unit.coord}.");
    }

    private static BattleUnitState FirstLivingWolf(BattleState state, BattleUnitState holder)
    {
        List<BattleUnitState> wolves = FindLivingWolves(state, holder);
        return wolves.Count > 0 ? wolves[0] : null;
    }

    private static List<BattleUnitState> FindLivingWolves(
        BattleState state,
        BattleUnitState holder
    )
    {
        var result = new List<BattleUnitState>();
        IReadOnlyList<BattleUnitState> units = state != null
            ? state.GetUnitsTyped()
            : Array.Empty<BattleUnitState>();
        foreach (BattleUnitState unit in units)
        {
            if (
                unit != null
                && unit.is_alive
                && unit.ai_blackboard?.summoned == true
                && unit.ai_blackboard.summon_source_unit_id == (holder?.unit_id ?? new StringName(""))
                && unit.ai_blackboard.summon_binding_id == WolfSpiritBindingId
                && unit.ai_blackboard.summon_state_key == WolfStateKey
            )
            {
                result.Add(unit);
            }
        }
        return result;
    }

    private static EquipmentInstanceState FindEquippedInstance(
        BattleUnitState unit,
        StringName instanceId
    )
    {
        EquipmentState equipment = unit?.GetEquipmentView();
        if (equipment == null || instanceId == "")
            return null;
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry != null && entry.instance_id == instanceId)
                return entry.GetEquipmentInstance();
        }
        return null;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentEquipment)
            throw new InvalidOperationException($"{bindingId} should come from persistent equipment.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
            throw new InvalidOperationException($"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}.");
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new List<BattleEquipmentAbilitySourceState>())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private sealed class WolfFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private WolfFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = itemRegistry.GetItemDefsTyped();
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static WolfFixture Build(GArray damageRolls = null)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemRegistry.GetItemDefsTyped(),
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls ?? new GArray { 4 }));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new WolfFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildWolfUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                WolfBowItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    WolfBowItemId,
                    $"eq_wolf_{label}"
                )
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
            BattleUnitState unit = units[0];
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private static PartyState BuildPartyState(StringName memberId)
        {
            PartyState partyState = new();
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress(),
                equipment_state = new EquipmentState(),
            };
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }
}

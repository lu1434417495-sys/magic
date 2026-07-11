using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_eternity_edge_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_longsword_eternity_edge";
    private static readonly StringName EternalWoundTraitId =
        "weapon.sword.eternity_edge.eternal_wound";
    private static readonly StringName TimeTheftTraitId =
        "weapon.sword.eternity_edge.time_theft";
    private static readonly StringName AgingTraitId = "weapon.sword.eternity_edge.aging";
    private static readonly StringName EternalCostTraitId =
        "weapon.sword.eternity_edge.eternal_cost";
    private static readonly StringName TimeLoopTraitId =
        "weapon.sword.eternity_edge.time_loop";
    private static readonly StringName EternalWoundBindingId =
        "binding.weapon.sword.eternity_edge.eternal_wound";
    private static readonly StringName TimeTheftBindingId =
        "binding.weapon.sword.eternity_edge.time_theft";
    private static readonly StringName AgingBindingId =
        "binding.weapon.sword.eternity_edge.aging";
    private static readonly StringName EternalCostBindingId =
        "binding.weapon.sword.eternity_edge.eternal_cost";
    private static readonly StringName TimeLoopBindingId =
        "binding.weapon.sword.eternity_edge.time_loop";
    private static readonly StringName EternalWoundStatusId = "eternity_edge_eternal_wound";
    private static readonly StringName TimeDebtStatusId = "eternity_edge_time_debt";
    private static readonly StringName AgingStatusId = "eternity_edge_aging";
    private static readonly StringName TimeTheftTriggeredStateKey =
        "time_theft_triggered_current_turn";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestContentLoadsFiveConfiguredTraitsAndProjectsWeapon();
            TestEternalWoundTimeDebtAndTimeLoopUseRealWeaponDamagePath();
            TestTimeDebtStopsTheftAndTurnEndReducesDebtWithoutTheft();
            TestKillingBlowNetsOneTimeDebtRelief();
            RequestTestExit(_test.Finish("Eternity Edge weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Eternity Edge weapon ability regression"));
        }
    }

    private void TestContentLoadsFiveConfiguredTraitsAndProjectsWeapon()
    {
        using EternityEdgeFixture fixture = EternityEdgeFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含永恒之刃。");
        foreach (
            StringName traitId in new[]
            {
                EternalWoundTraitId,
                TimeTheftTraitId,
                AgingTraitId,
                EternalCostTraitId,
                TimeLoopTraitId,
            }
        )
        {
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"永恒之刃应包含 trait {traitId}。");
        }
        foreach (
            StringName bindingId in new[]
            {
                EternalWoundBindingId,
                TimeTheftBindingId,
                AgingBindingId,
                EternalCostBindingId,
                TimeLoopBindingId,
            }
        )
        {
            _test.True(fixture.Bindings.ContainsKey(bindingId), $"永恒之刃应包含 binding {bindingId}。");
        }

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_eternity_edge.tres"
        );
        _test.True(rawItem != null, "永恒之刃原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "永恒之刃 item_id 不应包含来源编号。");
            _test.Eq(rawItem.display_name, "永恒之刃", "永恒之刃显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_longsword_base"),
                "永恒之刃应继承 longsword 模板。"
            );
            _test.Eq(rawItem.base_price, 115000, "永恒之刃价格应为 115000。");
            _test.Eq(rawItem.trait_ids.Count, 5, "永恒之刃应固定 5 个特性。");
            foreach (
                StringName traitId in new[]
                {
                    EternalWoundTraitId,
                    TimeTheftTraitId,
                    AgingTraitId,
                    EternalCostTraitId,
                    TimeLoopTraitId,
                }
            )
            {
                _test.True(rawItem.trait_ids.Contains(traitId), $"永恒之刃 item 应声明 {traitId}。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildEternityEdgeUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "永恒之刃装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("longsword"),
            "永恒之刃应投影为 longsword。"
        );
        _test.Eq(equipped.weapon_family, new StringName("sword"), "永恒之刃应投影为 sword family。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_slash"),
            "永恒之刃基础伤害应为 slash。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "永恒之刃攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "永恒之刃应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "永恒之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "永恒之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "永恒之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "永恒之刃双手应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "永恒之刃双手应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "永恒之刃双手应为 1D10+3。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EternalWoundTraitId,
            EternalWoundBindingId,
            "eq_eternity_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            TimeTheftTraitId,
            TimeTheftBindingId,
            "eq_eternity_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            AgingTraitId,
            AgingBindingId,
            "eq_eternity_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EternalCostTraitId,
            EternalCostBindingId,
            "eq_eternity_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            TimeLoopTraitId,
            TimeLoopBindingId,
            "eq_eternity_edge_projection"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, EternalWoundBindingId, "apply_status"),
            "永恒伤口必须由 apply_status 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, TimeTheftBindingId, "heal"),
            "时间窃取必须由 heal 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, EternalCostBindingId, "consume_status_stacks"),
            "永恒代价的消债必须由 consume_status_stacks 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, TimeLoopBindingId, "add_damage_dice"),
            "时间闭环必须由 add_damage_dice 配置声明。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除永恒之刃后 weapon_item_id 应清空。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除永恒之刃后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除永恒之刃后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestEternalWoundTimeDebtAndTimeLoopUseRealWeaponDamagePath()
    {
        using EternityEdgeFixture fixture = EternityEdgeFixture.Build(
            new GArray { 4, 3, 4, 3, 4, 3, 4, 5, 5 }
        );
        BattleUnitState holder = fixture.BuildEternityEdgeUnit("loop");
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        holder.SetCurrentHp(40);
        BattleUnitState target = BuildTarget("eternity_loop_target", new Vector2I(1, 0), hp: 100);

        for (int hit = 1; hit <= 3; hit++)
        {
            IssueBasicAttackPreservingHolderHp(
                fixture.Runtime,
                holder,
                target,
                $"eternity_loop_setup_{hit}",
                previewCommand: false
            );
            EndHolderTurn(fixture.Runtime, holder);
        }

        _test.Eq(target.current_hp, 79, "前三次命中应各造成永恒之刃 1D8+3。");
        _test.Eq(holder.current_hp, 49, "前三次时间窃取应各恢复 1D8。");
        AssertStatusStacks(holder, TimeDebtStatusId, 3, -1, "三次时间窃取后应达到 3 层时间债。");
        BattleStatusEffectState aging = target.GetStatusEffect(AgingStatusId);
        _test.True(aging != null, "目标应获得老化。");
        if (aging != null)
        {
            _test.Eq(aging.stacks, 3, "三次命中后老化应达到 3 层。");
            _test.Eq(aging.duration, 30, "老化应明确持续 30TU。");
        }
        BattleStatusEffectState wound = target.GetStatusEffect(EternalWoundStatusId);
        _test.True(wound != null, "真实 HP 伤害后目标应获得永恒伤口。");
        if (wound != null)
        {
            _test.Eq(wound.duration, -1, "永恒伤口应无自然 TU 衰减。");
            _test.Eq(wound.heal_multiplier_percent ?? 100, 0, "永恒伤口应把常规治疗倍率压到 0%。");
        }
        int beforeHeal = target.current_hp;
        ResolveRegularHeal(holder, target, 10);
        _test.Eq(target.current_hp, beforeHeal, "永恒伤口应阻止常规治疗恢复 HP。");

        IssueBasicAttackPreservingHolderHp(
            fixture.Runtime,
            holder,
            target,
            "eternity_time_loop",
            previewCommand: false
        );
        _test.Eq(
            target.current_hp,
            62,
            "命中 3 层老化目标时应造成武器 1D8+3 与时间闭环 2D8 force。"
        );
        _test.Eq(holder.current_hp, 49, "3 层时间债时，本击不应再触发时间窃取治疗。");
        _test.False(holder.HasStatusEffect(TimeDebtStatusId), "时间闭环应清空持有者全部时间债。");
        AssertStatusStacks(target, AgingStatusId, 1, 30, "时间闭环应消耗旧 3 层老化，并由本击重新留下 1 层。");
    }

    private void TestTimeDebtStopsTheftAndTurnEndReducesDebtWithoutTheft()
    {
        using EternityEdgeFixture fixture = EternityEdgeFixture.Build(
            new GArray { 4, 2, 4, 2, 4, 2, 4 }
        );
        BattleUnitState holder = fixture.BuildEternityEdgeUnit("debt");
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        holder.SetCurrentHp(40);

        for (int hit = 1; hit <= 3; hit++)
        {
            BattleUnitState target = BuildTarget($"eternity_debt_target_{hit}", new Vector2I(1, 0), hp: 40);
            IssueBasicAttackPreservingHolderHp(
                fixture.Runtime,
                holder,
                target,
                $"eternity_debt_hit_{hit}",
                previewCommand: false
            );
            EndHolderTurn(fixture.Runtime, holder);
        }

        _test.Eq(holder.current_hp, 46, "前三次时间窃取应各恢复 2 点。");
        AssertStatusStacks(holder, TimeDebtStatusId, 3, -1, "时间债应达到 3 层。");

        BattleUnitState cappedTarget = BuildTarget("eternity_debt_capped_target", new Vector2I(1, 0), hp: 40);
        IssueBasicAttackPreservingHolderHp(
            fixture.Runtime,
            holder,
            cappedTarget,
            "eternity_debt_capped_hit",
            previewCommand: false
        );
        _test.Eq(holder.current_hp, 46, "3 层时间债时，时间窃取应暂停。");
        AssertStatusStacks(holder, TimeDebtStatusId, 3, -1, "暂停时不应继续增加时间债。");

        EndHolderTurn(fixture.Runtime, holder);
        AssertStatusStacks(holder, TimeDebtStatusId, 2, -1, "本回合未触发时间窃取时，回合末应消除 1 层时间债。");
    }

    private void TestKillingBlowNetsOneTimeDebtRelief()
    {
        using EternityEdgeFixture fixture = EternityEdgeFixture.Build(new GArray { 4, 3 });
        BattleUnitState holder = fixture.BuildEternityEdgeUnit("kill_relief");
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        holder.SetCurrentHp(40);
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = TimeDebtStatusId,
                stacks = 2,
                duration = -1,
                source_unit_id = holder.unit_id,
                stack_behavior = "add",
                stack_limit = 3,
                counts_as_debuff_override = true,
                counts_as_debuff = false,
                undispellable = true,
            }
        );
        BattleUnitState target = BuildTarget("eternity_kill_target", new Vector2I(1, 0), hp: 7);

        IssueBasicAttackPreservingHolderHp(
            fixture.Runtime,
            holder,
            target,
            "eternity_kill_relief",
            previewCommand: false
        );

        _test.False(target.is_alive, "这一击应击杀目标。");
        _test.Eq(holder.current_hp, 43, "击杀伤害仍应先触发时间窃取治疗。");
        AssertStatusStacks(
            holder,
            TimeDebtStatusId,
            1,
            -1,
            "击杀回流应抵消本击新增时间债，并额外净消 1 层旧时间债。"
        );
        _test.Eq(
            GetAbilityState(holder, TimeTheftBindingId, TimeTheftTriggeredStateKey),
            1,
            "击杀回流不应靠代码分支清除本回合触发标记，标记仍由回合末配置清理。"
        );
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedEquipmentInstanceId
    )
    {
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}");
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (
                source != null
                && source.AbilityIds?.Contains(bindingId) == true
                && source.SourceEquipmentInstanceId == expectedEquipmentInstanceId
            )
            {
                return;
            }
        }
        throw new InvalidOperationException($"unit missing equipment ability source {bindingId}");
    }

    private static bool BindingHasActionKind(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        StringName actionKind
    )
    {
        if (!bindings.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions)
            {
                if (action?.Kind == actionKind)
                    return true;
            }
        }
        return false;
    }

    private void AssertStatusStacks(
        BattleUnitState unit,
        StringName statusId,
        int expectedStacks,
        int expectedDuration,
        string message
    )
    {
        BattleStatusEffectState status = unit.GetStatusEffect(statusId);
        _test.True(status != null, message);
        if (status == null)
            return;
        _test.Eq(status.stacks, expectedStacks, $"{message} 层数不符。");
        _test.Eq(status.duration, expectedDuration, $"{message} 持续时间不符。");
    }

    private static void ResolveRegularHeal(BattleUnitState source, BattleUnitState target, int power)
    {
        var resolver = new BattleDamageResolver();
        CombatEffectDefinition healEffect = TestSkillDefinitionProjection.BuildEffect("heal", power: power);
        resolver.ResolveEffects(source, target, new[] { healEffect });
    }

    private static BattleEventBatch IssueBasicAttackPreservingHolderHp(
        BattleRuntimeModule runtime,
        BattleUnitState holder,
        BattleUnitState target,
        StringName battleId,
        bool previewCommand = true
    )
    {
        int holderHp = Math.Max(holder?.current_hp ?? 1, 1);
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        holder.SetCurrentHp(holderHp);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            target
        );
        runtime.SetupStateForTests(state);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            holder,
            target
        );
        if (previewCommand)
        {
            BattlePreview preview = runtime.PreviewCommand(command);
            if (preview?.allowed != true)
            {
                throw new InvalidOperationException(
                    $"basic_attack preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
                );
            }
        }
        return runtime.IssueCommand(command);
    }

    private static void EndHolderTurn(BattleRuntimeModule runtime, BattleUnitState holder)
    {
        BattleState state = runtime.GetState();
        if (state == null)
        {
            state = WeaponAbilityCommandTestSupport.BuildFlatState(
                "eternity_edge_turn_end",
                holder,
                null
            );
            runtime.SetupStateForTests(state);
        }
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        runtime.IssueCommand(
            new BattleCommand
            {
                CommandKind = BattleCommandKind.Wait,
                unit_id = holder.unit_id,
            }
        );
    }

    private static int GetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        if (key == "")
            return 0;
        if (unit.HasPerBattleChargeTyped(key))
            return unit.GetPerBattleChargeTyped(key, 0);
        return unit.GetPerTurnChargeTyped(key, 0);
    }

    private static StringName FindChargeKey(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        string suffix = $"|{stateKey}";
        foreach (StringName key in unit.GetPerBattleChargesTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        foreach (StringName key in unit.GetPerTurnChargesTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        foreach (StringName key in unit.GetPerTurnChargeLimitsTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        StringName sourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EquipmentDefId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        return sourceKey == ""
            ? new StringName("")
            : new StringName($"equipment_ability|state|{sourceKey}|{stateKey}");
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = hp,
            coord = coord,
            body_size = 1,
            body_size_category = "medium",
        };
        unit.SetCombatResources(hp, 0, 30, 0, 2, 2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hp, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.SetAnchorCoord(coord);
        unit.RefreshFootprint();
        return unit;
    }

    private sealed class EternityEdgeFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private EternityEdgeFixture(
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
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static EternityEdgeFixture Build(GArray damageRolls)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new EternityEdgeFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildEternityEdgeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_eternity_edge_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
            unit.SetCombatResources(80, 0, 30, 0, 2, 2);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            return units[0];
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

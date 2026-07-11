using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_spider_spear_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName SpiderItemId =
        "weapon_unique_polearm_spider_spear_136";
    private static readonly StringName WebBindingTraitId =
        "weapon.polearm.spider_spear.web_binding";
    private static readonly StringName WebBindingId =
        "binding.weapon.polearm.spider_spear.web_binding";
    private static readonly StringName WebBindingSkillId =
        "weapon_polearm_spider_spear_web_binding";
    private static readonly StringName WebBindingGrantId =
        "grant.spider_spear.web_binding.skill";
    private static readonly StringName RootedStatusId = "rooted";
    private static readonly StringName TurnUseExhaustedReason =
        "equipment_skill_turn_use_exhausted";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSpiderSpearContentProjectionAndSkillAvailability();
            TestSpiderWebBindingUsageLimitsAndFailedStrengthSaveRoot();
            TestSpiderWebBindingSuccessfulStrengthSaveDoesNotRoot();
            RequestTestExit(_test.Finish("Spider Spear weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Spider Spear weapon ability regression"));
        }
    }

    private void TestSpiderSpearContentProjectionAndSkillAvailability()
    {
        using SpiderFixture fixture = SpiderFixture.Build(saveRollOverride: null);
        _test.True(fixture.ItemDefs.ContainsKey(SpiderItemId), "真实物品内容应包含蛛矛。");
        _test.True(fixture.TraitDefs.ContainsKey(WebBindingTraitId), "真实 trait 内容应包含蛛丝束缚。");
        _test.True(
            fixture.Bindings.ContainsKey(WebBindingId),
            "真实装备能力内容应包含蛛矛蛛丝束缚 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(WebBindingSkillId),
            "真实技能内容应包含蛛矛蛛丝束缚装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(SpiderItemId))
            return;

        ItemDef rawSpider = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_spear_spider_spear.tres"
        );
        _test.True(rawSpider != null, "蛛矛原始资源应能加载。");
        if (rawSpider != null)
        {
            _test.Eq(
                rawSpider.base_item_id,
                new StringName("weapon_type_spear_base"),
                "蛛矛应继承 spear 模板。"
            );
            _test.Eq(rawSpider.display_name, "蛛矛", "蛛矛显示名应匹配设计。");
            _test.Eq(rawSpider.base_price, 35000, "蛛矛价格应为 35000。");
            _test.True(rawSpider.trait_ids.Contains(WebBindingTraitId), "物品应声明蛛丝束缚 trait。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildSpiderUnit("projection");
        _test.Eq(equipped.weapon_item_id, SpiderItemId, "蛛矛装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("spear"), "蛛矛应投影为 spear。");
        _test.Eq(equipped.weapon_family, new StringName("polearm"), "蛛矛应按设计投影为 polearm。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_pierce"),
            "蛛矛应是 physical_pierce。"
        );
        _test.Eq(equipped.weapon_attack_range, 2, "蛛矛攻击距离应为 2。");
        _test.True(equipped.weapon_is_versatile, "蛛矛应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "蛛矛单手应为 1D6+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 6, "蛛矛单手应为 1D6+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 1, "蛛矛单手应为 1D6+1。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "蛛矛双手应为 1D8+1。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 8, "蛛矛双手应为 1D8+1。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 1, "蛛矛双手应为 1D8+1。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            WebBindingTraitId,
            WebBindingId,
            "eq_spider_projection"
        );

        AssertWebBindingSkillConfig(fixture);

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, equipped, 0);
        _test.True(
            TryFindSkillEntry(view, WebBindingSkillId, out BattleAvailableSkillEntry entry),
            "装备蛛矛后 unit 应有蛛丝束缚技能入口。"
        );
        if (entry != null)
        {
            _test.Eq(
                entry.EntryRef.SourceKind,
                BattleSkillEntrySourceKind.EquipmentSkill,
                "蛛丝束缚技能入口来源应是 equipment_skill。"
            );
            _test.Eq(entry.EquipmentBindingId, WebBindingId, "蛛丝束缚入口应携带 binding id。");
            _test.Eq(
                entry.EquipmentGrantedActionId,
                WebBindingGrantId,
                "蛛丝束缚入口应携带 grant id。"
            );
            _test.True(entry.IsSelectable, "未使用前蛛丝束缚应可选。");
            _test.Eq(
                entry.EquipmentUsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                "蛛丝束缚应声明 per_world_day 使用周期。"
            );
            _test.Eq(entry.EquipmentMaxUsesPerPeriod, 3, "蛛丝束缚每世界日应有 3 次。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除蛛矛后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除蛛矛后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除蛛矛后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除蛛矛后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestSpiderWebBindingUsageLimitsAndFailedStrengthSaveRoot()
    {
        using SpiderFixture fixture = SpiderFixture.Build(saveRollOverride: 1);
        BattleUnitState holder = fixture.BuildSpiderUnit("web_binding");
        BattleUnitState target = BuildTarget(
            "spider_web_failed_target",
            new Vector2I(1, 0),
            strengthModifier: 0
        );
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "spider_web_binding_failure",
            holder,
            target,
            worldStep: 0
        );
        fixture.Runtime.SetupStateForTests(state);

        EquipmentInstanceState instance = FindEquippedInstance(holder, "eq_spider_web_binding");
        _test.True(instance != null, "蛛丝束缚测试应能找到装备实例。");
        if (instance == null)
            return;

        BattleAvailableSkillEntry firstEntry =
            FindRequiredEquipmentSkill(fixture, holder, state, 0);
        BattleEventBatch firstBatch =
            IssueWebBindingInCurrentState(fixture, holder, target, firstEntry, "first");
        _test.True(firstBatch != null, "第一次蛛丝束缚应返回 batch。");

        BattleStatusEffectState rooted = target.GetStatusEffect(RootedStatusId);
        _test.True(rooted != null, "STR DC14 豁免失败后应施加 rooted。");
        _test.Eq(rooted?.duration ?? -1, 60, "rooted 应持续 60TU。");
        _test.Eq(
            BattleStatusSemanticTable.GetAttackRollPenalty(rooted),
            2,
            "蛛丝束缚的 rooted 应附带攻击检定 -2（restrained 近似）。"
        );
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                WebBindingGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            1,
            "第一次蛛丝束缚后应写入当前世界日使用次数。"
        );

        holder.current_ap = 2;
        BattleSkillAvailabilityView sameTurnView = BuildEquipmentSkillView(
            fixture,
            holder,
            0,
            state
        );
        _test.True(
            TryFindSkillEntry(sameTurnView, WebBindingSkillId, out BattleAvailableSkillEntry sameTurnEntry),
            "同一行动回合内蛛丝束缚入口仍应可见。"
        );
        _test.False(sameTurnEntry?.IsSelectable ?? true, "同一行动回合第 2 次蛛丝束缚应不可用。");
        _test.Eq(
            sameTurnEntry?.DisabledReason ?? new StringName(""),
            TurnUseExhaustedReason,
            "同一行动回合第 2 次蛛丝束缚应返回 equipment_skill_turn_use_exhausted。"
        );

        for (int use = 2; use <= 3; use++)
        {
            holder.ResetPerTurnCharges();
            holder.current_ap = 2;
            ForceUnitActing(state, holder);
            BattleAvailableSkillEntry nextEntry =
                FindRequiredEquipmentSkill(fixture, holder, state, 0);
            IssueWebBindingInCurrentState(
                fixture,
                holder,
                target,
                nextEntry,
                $"use_{use}"
            );
        }

        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                WebBindingGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            3,
            "同一世界日 3 次蛛丝束缚后应记录用尽。"
        );
        holder.ResetPerTurnCharges();
        holder.current_ap = 2;
        ForceUnitActing(state, holder);
        BattleSkillAvailabilityView exhaustedView = BuildEquipmentSkillView(
            fixture,
            holder,
            0,
            state
        );
        _test.True(
            TryFindSkillEntry(exhaustedView, WebBindingSkillId, out BattleAvailableSkillEntry exhaustedEntry),
            "同日用尽后蛛丝束缚入口仍应存在。"
        );
        _test.False(exhaustedEntry?.IsSelectable ?? true, "第 4 次同日蛛丝束缚应不可用。");
        _test.Eq(
            exhaustedEntry?.DisabledReason ?? new StringName(""),
            new StringName("equipment_skill_usage_exhausted"),
            "第 4 次同日蛛丝束缚禁用原因应稳定。"
        );
    }

    private void TestSpiderWebBindingSuccessfulStrengthSaveDoesNotRoot()
    {
        using SpiderFixture fixture = SpiderFixture.Build(saveRollOverride: 20);
        BattleUnitState holder = fixture.BuildSpiderUnit("web_success");
        BattleUnitState target = BuildTarget(
            "spider_web_success_target",
            new Vector2I(1, 0),
            strengthModifier: 0
        );
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "spider_web_binding_success",
            holder,
            target,
            worldStep: 0
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(fixture, holder, state, 0);
        IssueWebBindingInCurrentState(fixture, holder, target, entry, "success");
        _test.False(target.HasStatusEffect(RootedStatusId), "STR DC14 豁免成功不应施加 rooted。");
    }

    private void AssertWebBindingSkillConfig(SpiderFixture fixture)
    {
        _test.True(
            fixture.SkillDefs.TryGetValue(WebBindingSkillId, out SkillDefinition skill),
            "蛛丝束缚应是 SkillDef，而不是 trait 自己承担主动动作。"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "蛛丝束缚技能应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("unit"), "蛛丝束缚应选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "蛛丝束缚应选择敌人。");
        _test.Eq(combat.RangeValue, 3, "蛛丝束缚射程应为 3。");
        _test.Eq(combat.ApCost, 1, "蛛丝束缚应消耗 1 AP。");
        _test.Eq(
            combat.AttackResolutionMode,
            new StringName("direct_effect"),
            "蛛丝束缚应走 direct_effect，以 STR save 决定状态。"
        );
        _test.Eq(combat.EffectDefinitions.Count, 1, "蛛丝束缚应只有一个 status effect。");
        if (combat.EffectDefinitions.Count == 0)
            return;
        CombatEffectDefinition status = combat.EffectDefinitions[0];
        _test.Eq(status.EffectType, new StringName("status"), "蛛丝束缚 effect 应是 status。");
        _test.Eq(status.StatusId, RootedStatusId, "蛛丝束缚失败应施加 rooted。");
        _test.Eq(status.DurationTu, 60, "蛛丝束缚 rooted 应持续 60TU。");
        _test.Eq(status.SaveDc, 14, "蛛丝束缚 STR save DC 应为 14。");
        _test.Eq(status.SaveAbility, new StringName("strength"), "蛛丝束缚应使用 strength save。");
        _test.Eq(status.SaveTag, new StringName("strength"), "蛛丝束缚 save_tag 应为 strength。");

        _test.True(
            fixture.Bindings.TryGetValue(WebBindingId, out EquipmentAbilityBindingDefinition binding),
            "蛛丝束缚 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "蛛丝束缚 binding 应授予一个装备技能入口。");
        if (binding.GrantedActions.Count == 0)
            return;
        EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
        _test.Eq(grant.SkillId, WebBindingSkillId, "蛛丝束缚 grant 应指向真实 SkillDef。");
        _test.Eq(grant.SkillLevel, 1, "蛛丝束缚 grant 等级应为 1。");
        _test.Eq(
            grant.UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "蛛丝束缚 grant 应声明 per_world_day。"
        );
        _test.Eq(grant.MaxUsesPerPeriod, 3, "蛛丝束缚 grant 每世界日 3 次。");
    }

    private BattleEventBatch IssueWebBindingInCurrentState(
        SpiderFixture fixture,
        BattleUnitState holder,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        string label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        ForceUnitActing(fixture.Runtime.GetState(), holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            WebBindingSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            int distance =
                fixture.Runtime.GetGridService()?.GetDistanceBetweenUnits(holder, target) ?? -1;
            BattleUnitSkillTargetAffordance affordance =
                fixture.Runtime._skill_orchestrator.GetUnitSkillTargetAffordance(
                    holder,
                    target,
                    fixture.SkillDefs[WebBindingSkillId]
                );
            throw new InvalidOperationException(
                $"{label} spider web binding preview blocked: {JoinLogs(preview?.LogLinesTyped)}"
                    + $" distance={distance} holder_coord={holder?.coord.ToString() ?? "<null>"}"
                    + $" target_coord={target?.coord.ToString() ?? "<null>"}"
                    + $" holder_ap={holder?.current_ap.ToString() ?? "<null>"}"
                    + $" holder_faction={holder?.faction_id.ToString() ?? "<null>"}"
                    + $" target_faction={target?.faction_id.ToString() ?? "<null>"}"
                    + $" affordance_allowed={affordance.Allowed}"
                    + $" affordance_reason={affordance.Reason}"
            );
        }
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException($"{label} spider web binding IssueCommand returned null.");
        return batch;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        SpiderFixture fixture,
        BattleUnitState unit,
        BattleState state,
        int worldStep
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(
            fixture,
            unit,
            worldStep,
            state
        );
        if (!TryFindSkillEntry(view, WebBindingSkillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException("missing spider spear equipment skill entry.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"spider spear equipment skill disabled: {entry.DisabledReason}");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        SpiderFixture fixture,
        BattleUnitState unit,
        int worldStep,
        BattleState state = null
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

    private static EquipmentInstanceState FindEquippedInstance(
        BattleUnitState unit,
        StringName instanceId
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(instanceId);
        EquipmentState equipment = unit?.GetEquipmentView();
        if (equipment == null || normalized == "")
            return null;
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry != null && entry.instance_id == normalized)
                return entry.GetEquipmentInstance();
        }
        return null;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        int strengthModifier
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 30,
            current_ap = 2,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.STRENGTH_MODIFIER, strengthModifier);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
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
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
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

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class SpiderFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private SpiderFixture(
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
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static SpiderFixture Build(int? saveRollOverride)
        {
            ItemContentRegistry itemRegistry = new(new TestContentResourceLoader());
            ProgressionContentRegistry progressionRegistry = new(new TestContentResourceLoader());
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
            BattleDamageResolver damageResolver = saveRollOverride.HasValue
                ? new FixedSaveRollDamageResolver(saveRollOverride.Value)
                : new FixedRollDamageResolver(new GArray());
            runtime.ConfigureDamageResolverForTests(damageResolver);
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new SpiderFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildSpiderUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                SpiderItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    SpiderItemId,
                    $"eq_spider_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
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

    private sealed partial class FixedSaveRollDamageResolver : FixedRollDamageResolver
    {
        private readonly int _saveRollOverride;

        internal FixedSaveRollDamageResolver(int saveRollOverride)
            : base(new GArray())
        {
            _saveRollOverride = Math.Clamp(saveRollOverride, 1, 20);
        }

        internal override AttackEffectResolutionResult ResolveEffects(
            BattleUnitState source_unit,
            BattleUnitState target_unit,
            IEnumerable<CombatEffectDefinition> effect_definitions,
            DamageResolutionContext damage_context
        )
        {
            GDictionary fixedContext =
                damage_context?.RawContext?.Duplicate(true) ?? new GDictionary();
            fixedContext["save_roll_override"] = _saveRollOverride;
            return base.ResolveEffects(
                source_unit,
                target_unit,
                effect_definitions,
                DamageResolutionContext.FromDictionary(fixedContext)
            );
        }
    }
}

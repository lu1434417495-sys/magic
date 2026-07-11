using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_sands_time_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName SandsTimeItemId =
        "weapon_unique_sands_time_480";
    private static readonly StringName AccelerationTraitId =
        "weapon.exotic.sands_time.time_acceleration";
    private static readonly StringName DecelerationTraitId =
        "weapon.exotic.sands_time.time_deceleration";
    private static readonly StringName DislocationTraitId =
        "weapon.exotic.sands_time.time_dislocation";
    private static readonly StringName AccelerationBindingId =
        "binding.weapon.exotic.sands_time.time_acceleration";
    private static readonly StringName DecelerationBindingId =
        "binding.weapon.exotic.sands_time.time_deceleration";
    private static readonly StringName DislocationBindingId =
        "binding.weapon.exotic.sands_time.time_dislocation";
    private static readonly StringName AccelerationSkillId =
        "weapon_exotic_sands_time_accelerate";
    private static readonly StringName DecelerationSkillId =
        "weapon_exotic_sands_time_decelerate";
    private static readonly StringName AccelerationGrantId =
        "grant.sands_time.time_acceleration.skill";
    private static readonly StringName DecelerationGrantId =
        "grant.sands_time.time_deceleration.skill";
    private static readonly StringName TemporalApStolenStatusId = "temporal_ap_stolen";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSandsTimeProjectsRealContent();
            TestAccelerationDoublesCurrentActionTurnBudgetOncePerBattle();
            TestDecelerationZerosTargetNextTurnApButLeavesMovement();
            TestDislocationRollsEveryActionAndCastProgressCalculation();
            RequestTestExit(_test.Finish("Sands of Time weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Sands of Time weapon ability regression"));
        }
    }

    private void TestSandsTimeProjectsRealContent()
    {
        using SandsTimeFixture fixture = SandsTimeFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(SandsTimeItemId), "真实物品内容应包含时间之沙。");
        _test.True(fixture.TraitDefs.ContainsKey(AccelerationTraitId), "真实 trait 内容应包含时间加速。");
        _test.True(fixture.TraitDefs.ContainsKey(DecelerationTraitId), "真实 trait 内容应包含时间减速。");
        _test.True(fixture.TraitDefs.ContainsKey(DislocationTraitId), "真实 trait 内容应包含时间错乱。");
        _test.True(fixture.Bindings.ContainsKey(AccelerationBindingId), "真实装备能力内容应包含时间加速 binding。");
        _test.True(fixture.Bindings.ContainsKey(DecelerationBindingId), "真实装备能力内容应包含时间减速 binding。");
        _test.True(fixture.Bindings.ContainsKey(DislocationBindingId), "真实装备能力内容应包含时间错乱 binding。");
        _test.True(fixture.SkillDefs.ContainsKey(AccelerationSkillId), "真实技能内容应包含时间加速装备技能。");
        _test.True(fixture.SkillDefs.ContainsKey(DecelerationSkillId), "真实技能内容应包含时间减速装备技能。");
        if (!fixture.ItemDefs.ContainsKey(SandsTimeItemId))
            return;

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_dagger_sands_time.tres"
        );
        _test.True(rawItem != null, "时间之沙原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "时间之沙", "时间之沙显示名应匹配设计。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_dagger_base"), "时间之沙应继承 dagger 模板。");
            _test.Eq(rawItem.base_price, 85000, "时间之沙价格应为 85000。");
            _test.True(rawItem.trait_ids.Contains(AccelerationTraitId), "时间之沙物品应声明时间加速 trait。");
            _test.True(rawItem.trait_ids.Contains(DecelerationTraitId), "时间之沙物品应声明时间减速 trait。");
            _test.True(rawItem.trait_ids.Contains(DislocationTraitId), "时间之沙物品应声明时间错乱 trait。");
        }

        BattleUnitState equipped = fixture.BuildSandsTimeUnit("projection");
        _test.Eq(equipped.weapon_item_id, SandsTimeItemId, "时间之沙装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("dagger"), "时间之沙应投影为 dagger。");
        _test.Eq(equipped.weapon_family, new StringName("exotic"), "时间之沙应投影为 exotic。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "时间之沙应投影为 physical_pierce。");
        _test.Eq(equipped.weapon_attack_range, 1, "时间之沙攻击范围应为 1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "时间之沙单手伤害应为 1D4+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 4, "时间之沙单手伤害应为 1D4+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "时间之沙单手伤害应为 1D4+2。");
        AssertUnitHasTraitAndAbilitySource(equipped, AccelerationTraitId, AccelerationBindingId, "eq_sands_time_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, DecelerationTraitId, DecelerationBindingId, "eq_sands_time_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, DislocationTraitId, DislocationBindingId, "eq_sands_time_projection");

        AssertActiveSkillAndBindingConfig(fixture, AccelerationSkillId, AccelerationBindingId, AccelerationGrantId, "ally");
        AssertActiveSkillAndBindingConfig(fixture, DecelerationSkillId, DecelerationBindingId, DecelerationGrantId, "enemy");
    }

    private void TestAccelerationDoublesCurrentActionTurnBudgetOncePerBattle()
    {
        using SandsTimeFixture fixture = SandsTimeFixture.Build();
        BattleUnitState holder = fixture.BuildSandsTimeUnit("accelerate");
        SetBaseActionPoints(holder, 2);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "sands_time_accelerate",
            holder,
            null,
            worldStep: 10
        );
        fixture.Runtime.SetupStateForTests(state);
        ForceUnitActing(state, holder);
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            AccelerationSkillId,
            expectSelectable: true
        );
        _test.Eq(entry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "时间加速应每场战斗 1 次。");
        _test.Eq(entry.EquipmentMaxUsesPerPeriod, 1, "时间加速应每场战斗 1 次。");

        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            holder,
            entry,
            AccelerationSkillId,
            "sands_time_acceleration"
        );
        _test.True(batch != null, "时间加速应返回 batch。");
        _test.Eq(holder.current_ap, 3, "时间加速应在支付 1 AP 后把本行动回合 AP 总量翻倍。");

        BattleAvailableSkillEntry sameTurnEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            AccelerationSkillId,
            expectSelectable: false
        );
        _test.Eq(
            sameTurnEntry.DisabledReason,
            new StringName("equipment_skill_turn_use_exhausted"),
            "时间加速同一行动回合第 2 次应被行动回合限制挡住。"
        );

        holder.ResetPerTurnCharges();
        BattleAvailableSkillEntry sameBattleEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            AccelerationSkillId,
            expectSelectable: false
        );
        _test.Eq(
            sameBattleEntry.DisabledReason,
            new StringName("equipment_skill_usage_exhausted"),
            "时间加速跨行动回合后仍应被每场战斗 1 次限制挡住。"
        );
    }

    private void TestDecelerationZerosTargetNextTurnApButLeavesMovement()
    {
        using SandsTimeFixture fixture = SandsTimeFixture.Build();
        BattleUnitState holder = fixture.BuildSandsTimeUnit("decelerate");
        BattleUnitState target = BuildTarget("decelerate_target", new Vector2I(1, 0));
        SetBaseActionPoints(holder, 2);
        SetBaseActionPoints(target, 2);
        target.current_ap = 2;
        target.current_move_points = 0;
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "sands_time_decelerate",
            holder,
            target,
            worldStep: 20
        );
        fixture.Runtime.SetupStateForTests(state);
        ForceUnitActing(state, holder);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            DecelerationSkillId,
            expectSelectable: true
        );
        _test.Eq(entry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "时间减速应每场战斗 1 次。");
        _test.Eq(entry.EquipmentMaxUsesPerPeriod, 1, "时间减速应每场战斗 1 次。");

        IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            target,
            entry,
            DecelerationSkillId,
            "sands_time_deceleration"
        );
        _test.True(target.HasStatusEffect(TemporalApStolenStatusId), "时间减速应标记目标下一行动回合 AP 归零。");

        state.active_unit_id = new StringName("");
        state.PhaseKind = BattlePhaseKind.TimelineRunning;
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        fixture.Runtime._timeline_driver.ActivateNextReadyUnit(new BattleEventBatch());

        _test.Eq(state.active_unit_id, target.unit_id, "目标应从 ready 队列进入行动回合。");
        _test.Eq(target.current_ap, 0, "时间减速应让目标下一行动回合 AP 归零。");
        _test.True(target.current_move_points > 0, "时间减速不应剥夺目标移动。");
        _test.False(target.HasStatusEffect(TemporalApStolenStatusId), "时间减速的下一回合 AP 标记应在生效后清除。");
    }

    private void TestDislocationRollsEveryActionAndCastProgressCalculation()
    {
        using SandsTimeFixture fixture = SandsTimeFixture.Build();
        BattleUnitState holder = fixture.BuildSandsTimeUnit("dislocation");
        holder.attribute_snapshot.SetValue(AttributeService.INTELLIGENCE_MODIFIER, 0);

        BattleTemporalStatusService.SetForcedTemporalProgressRollsForTests(new[] { 1, 20, 1, 20 });
        try
        {
            _test.Eq(
                BattleTemporalStatusService.ConsumeActionProgressGain(holder, 10),
                5,
                "时间错乱检定失败时行动进度应为 50%。"
            );
            _test.Eq(
                BattleTemporalStatusService.ConsumeActionProgressGain(holder, 10),
                20,
                "时间错乱检定成功时行动进度应为 200%。"
            );
            _test.Eq(
                BattleTemporalStatusService.ConsumeCastProgressGain(holder, 10),
                5,
                "时间错乱检定失败时读条进度应为 50%。"
            );
            _test.Eq(
                BattleTemporalStatusService.ConsumeCastProgressGain(holder, 10),
                20,
                "时间错乱检定成功时读条进度应为 200%。"
            );
            _test.False(holder.HasStatusEffect("time_slow"), "时间错乱不应建模为有持续时间的 time_slow。");
            _test.False(holder.HasStatusEffect("time_stasis"), "时间错乱不应建模为 time_stasis。");
        }
        finally
        {
            BattleTemporalStatusService.ClearForcedTemporalProgressRollsForTests();
        }
    }

    private void AssertActiveSkillAndBindingConfig(
        SandsTimeFixture fixture,
        StringName skillId,
        StringName bindingId,
        StringName grantId,
        StringName expectedTargetFilter
    )
    {
        _test.True(fixture.SkillDefs.TryGetValue(skillId, out SkillDefinition skill), $"{skillId} 应是 SkillDef。");
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, $"{skillId} 应有 combat_profile。");
        if (combat != null)
        {
            _test.Eq(combat.TargetMode, new StringName("unit"), $"{skillId} 应选择单位目标。");
            _test.Eq(combat.TargetTeamFilter, expectedTargetFilter, $"{skillId} 目标阵营过滤应匹配设计。");
            _test.Eq(combat.RangeValue, 6, $"{skillId} 应使用 30 尺时间作用范围。");
            _test.Eq(combat.ApCost, 1, $"{skillId} 应消耗 1 AP。");
            _test.Eq(combat.AttackResolutionMode, new StringName("direct_effect"), $"{skillId} 应走 direct_effect。");
        }

        _test.True(fixture.Bindings.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding), $"{bindingId} 应存在。");
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, $"{bindingId} 应授予一个装备技能入口。");
        if (binding.GrantedActions.Count == 0)
            return;
        EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
        _test.Eq(grant.GrantedActionId, grantId, $"{bindingId} grant id 应稳定。");
        _test.Eq(grant.SkillId, skillId, $"{bindingId} grant 应指向真实 SkillDef。");
        _test.Eq(grant.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, $"{bindingId} 应每场战斗 1 次。");
        _test.Eq(grant.MaxUsesPerPeriod, 1, $"{bindingId} 应每场战斗 1 次。");
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        SandsTimeFixture fixture,
        BattleUnitState unit,
        BattleState state,
        StringName skillId,
        bool expectSelectable
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(
            fixture,
            unit,
            state
        );
        if (!TryFindSkillEntry(view, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing sands time equipment skill entry {skillId}.");
        if (expectSelectable && !entry.IsSelectable)
            throw new InvalidOperationException($"{skillId} disabled: {entry.DisabledReason}");
        if (!expectSelectable && entry.IsSelectable)
            throw new InvalidOperationException($"{skillId} should be disabled.");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        SandsTimeFixture fixture,
        BattleUnitState unit,
        BattleState state
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
                WorldStep = 0,
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

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState holder,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        ForceUnitActing(runtime?.GetState(), holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{label} preview blocked: {JoinLogs(preview)}"
            );
        }
        BattleEventBatch batch = runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException($"{label} IssueCommand returned null.");
        return batch;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            control_mode = "manual",
            current_hp = 30,
            current_ap = 2,
            current_stamina = 30,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 8);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 8);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 12);
        unit.creature_type_tags.Add("humanoid");
        return unit;
    }

    private static void SetBaseActionPoints(BattleUnitState unit, int value)
    {
        unit?.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, Math.Max(value, 1));
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

    private static string JoinLogs(BattlePreview preview) =>
        preview == null ? "" : string.Join(" | ", preview.LogLinesTyped);

    private sealed class SandsTimeFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private SandsTimeFixture(
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

        internal static SandsTimeFixture Build()
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(new GArray()));
            return new SandsTimeFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildSandsTimeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                SandsTimeItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    SandsTimeItemId,
                    $"eq_sands_time_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
            unit.attribute_snapshot.SetValue(AttributeService.INTELLIGENCE_MODIFIER, 0);
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
}

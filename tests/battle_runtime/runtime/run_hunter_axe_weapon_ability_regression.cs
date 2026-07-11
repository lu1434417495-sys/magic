using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_hunter_axe_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName HunterAxeItemId = "weapon_unique_battleaxe_hunter_382";
    private static readonly StringName BeastSlayerTraitId = "weapon.axe.hunter.beast_slayer";
    private static readonly StringName HunterMarkTraitId = "weapon.axe.hunter.hunter_mark";
    private static readonly StringName BeastSlayerBindingId = "binding.weapon.axe.hunter.beast_slayer";
    private static readonly StringName HunterMarkBindingId = "binding.weapon.axe.hunter.hunter_mark";
    private static readonly StringName HunterMarkSkillId = "archer_hunter_mark";
    private static readonly StringName HunterMarkGrantId = "grant.hunter_axe.hunter_mark.skill";
    private static readonly StringName HunterMarkedStatusId = "hunter_marked";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestHunterAxeProjectsFixedLevelHunterMark();
            TestUnlearnedEquipmentGrantedHunterMarkDoesNotGrantMastery();
            TestLearnedHunterMarkUsesHigherLevelAndGrantsMasteryOnMarkedWeaponDamage();
            RequestTestExit(_test.Finish("Hunter Axe weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Hunter Axe weapon ability regression"));
        }
    }

    private void TestHunterAxeProjectsFixedLevelHunterMark()
    {
        using HunterAxeFixture fixture = HunterAxeFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(HunterAxeItemId), "真实物品内容应包含猎人之斧。");
        _test.True(fixture.TraitDefs.ContainsKey(BeastSlayerTraitId), "真实 trait 内容应包含野兽杀手。");
        _test.True(fixture.TraitDefs.ContainsKey(HunterMarkTraitId), "真实 trait 内容应包含猎人标记授予。");
        _test.True(fixture.Bindings.ContainsKey(BeastSlayerBindingId), "真实装备能力内容应包含野兽杀手 binding。");
        _test.True(fixture.Bindings.ContainsKey(HunterMarkBindingId), "真实装备能力内容应包含猎人标记 binding。");
        _test.True(fixture.SkillDefs.ContainsKey(HunterMarkSkillId), "猎人斧应复用真实猎人标记 SkillDef。");

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_hunter.tres"
        );
        _test.True(rawItem != null, "猎人之斧原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "猎人之斧", "猎人之斧显示名应匹配源设计。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_battleaxe_base"), "猎人之斧应继承 battleaxe 模板。");
            _test.Eq(rawItem.base_price, 35000, "猎人之斧价格应为 35000。");
            _test.True(rawItem.trait_ids.Contains(BeastSlayerTraitId), "猎人之斧应声明野兽杀手。");
            _test.True(rawItem.trait_ids.Contains(HunterMarkTraitId), "猎人之斧应声明猎人标记授予。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildHunterAxeUnit("projection");
        _test.Eq(equipped.weapon_item_id, HunterAxeItemId, "猎人之斧装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("battleaxe"), "猎人之斧应投影为 battleaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "猎人之斧应保留 axe family。");
        _test.Eq(equipped.weapon_attack_range, 1, "猎人之斧近战攻击距离应为 1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "猎人之斧单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "猎人之斧单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "猎人之斧单手应为 1D8+2。");
        AssertUnitHasTraitAndAbilitySource(equipped, BeastSlayerTraitId, BeastSlayerBindingId);
        AssertUnitHasTraitAndAbilitySource(equipped, HunterMarkTraitId, HunterMarkBindingId);
        _test.False(
            ContainsStringName(equipped.known_active_skill_ids, HunterMarkSkillId),
            "猎人之斧不应把猎人标记写进角色已学技能列表。"
        );

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, equipped);
        _test.True(
            TryFindSkillEntry(view, HunterMarkSkillId, out BattleAvailableSkillEntry entry),
            "装备猎人之斧后，unit 的可用技能应包含装备授予的猎人标记。"
        );
        if (entry != null)
        {
            _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "猎人标记来源应是 equipment_skill。");
            _test.Eq(entry.SkillLevel, 3, "猎人之斧授予的猎人标记应固定为 3 级。");
            _test.Eq(entry.EquipmentBindingId, HunterMarkBindingId, "猎人标记入口应携带 binding id。");
            _test.Eq(entry.EquipmentGrantedActionId, HunterMarkGrantId, "猎人标记入口应携带 grant id。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除猎人之斧后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除猎人之斧后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除猎人之斧后装备能力源应清空。");
    }

    private void TestUnlearnedEquipmentGrantedHunterMarkDoesNotGrantMastery()
    {
        using HunterAxeFixture fixture = HunterAxeFixture.Build();
        BattleUnitState holder = fixture.BuildHunterAxeUnit("unlearned_no_mastery");
        holder.current_ap = 2;
        holder.current_stamina = 80;
        holder.SetAnchorCoord(new Vector2I(0, 0));

        BattleUnitState quarry = BattleTestFixture.BuildUnit(
            "hunter_axe_quarry",
            "enemy",
            new Vector2I(1, 0),
            currentAp: 1,
            currentHp: 60
        );
        quarry.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);

        BattleState state = BattleTestFixture.BuildFlatState("hunter_axe_no_mastery", new Vector2I(4, 2));
        BattleTestFixture.InstallUnits(state, new[] { holder }, new[] { quarry });
        fixture.Runtime.SetupStateForTests(state);

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, HunterMarkSkillId, out BattleAvailableSkillEntry entry),
            "猎人之斧应提供可执行的装备猎人标记入口。"
        );
        if (entry == null)
        {
            BattleTestFixture.DisposeBattleState(state);
            return;
        }
        _test.Eq(entry.SkillLevel, 3, "未学习猎人标记时，猎人之斧入口应保持固定 3 级。");

        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_id = HunterMarkSkillId,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            target_unit_id = quarry.unit_id,
        };
        command.AddTargetUnitId(quarry.unit_id);

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(quarry.HasStatusEffect(HunterMarkedStatusId), "装备入口施放后目标应获得 hunter_marked。");
        BattleStatusEffectState mark = quarry.GetStatusEffect(HunterMarkedStatusId);
        _test.Eq(mark?.duration ?? -1, 80, "猎人之斧固定 3 级猎人标记应施加 80TU。");
        _test.True(
            fixture.GetHunterMarkProgress() == null,
            "未学习猎人标记时，装备入口施放不应创建角色技能进度。"
        );

        GodotSharpCleanup.DisposeBatch(batch);
        GodotSharpCleanup.ClearRuntimeReferences(command);
        BattleTestFixture.DisposeBattleState(state);
    }

    private void TestLearnedHunterMarkUsesHigherLevelAndGrantsMasteryOnMarkedWeaponDamage()
    {
        using HunterAxeFixture fixture = HunterAxeFixture.Build();
        UnitSkillProgress learnedProgress = fixture.SetLearnedHunterMark(
            level: 4,
            currentMastery: 10,
            isCore: true
        );
        BattleUnitState holder = fixture.BuildHunterAxeUnit("learned_mastery");
        holder.current_ap = 2;
        holder.current_stamina = 80;
        holder.SetAnchorCoord(new Vector2I(0, 0));

        BattleUnitState quarry = BattleTestFixture.BuildUnit(
            "hunter_axe_learned_quarry",
            "enemy",
            new Vector2I(1, 0),
            currentAp: 1,
            currentHp: 60
        );
        quarry.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);

        BattleState state = BattleTestFixture.BuildFlatState("hunter_axe_learned_mastery", new Vector2I(4, 2));
        BattleTestFixture.InstallUnits(state, new[] { holder }, new[] { quarry });
        fixture.Runtime.SetupStateForTests(state);

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, HunterMarkSkillId, out BattleAvailableSkillEntry entry),
            "已学习猎人标记时，猎人之斧仍应提供装备入口。"
        );
        if (entry == null)
        {
            BattleTestFixture.DisposeBattleState(state);
            return;
        }
        _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "已学习时使用的仍应是猎人之斧 equipment_skill 入口。");
        _test.Eq(entry.SkillLevel, 4, "已学习 4 级猎人标记时，装备入口应取已学等级而不是固定 3 级。");

        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_id = HunterMarkSkillId,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            target_unit_id = quarry.unit_id,
        };
        command.AddTargetUnitId(quarry.unit_id);

        int masteryBefore = learnedProgress.current_mastery;
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(quarry.HasStatusEffect(HunterMarkedStatusId), "已学习技能时，装备入口施放后目标应获得 hunter_marked。");
        BattleStatusEffectState mark = quarry.GetStatusEffect(HunterMarkedStatusId);
        _test.Eq(mark?.duration ?? -1, 100, "已学习 4 级时，装备入口应按 4 级猎人标记施加 100TU。");
        _test.Eq(learnedProgress.skill_level, 4, "装备入口施放不应直接改写已学技能等级。");
        _test.Eq(
            learnedProgress.current_mastery,
            masteryBefore,
            "猎人标记施放成功本身不应给猎人标记熟练度。"
        );

        holder.current_ap = 2;
        holder.current_stamina = 80;
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        BattleCommand attackCommand = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            holder,
            quarry
        );
        BattlePreview attackPreview = fixture.Runtime.PreviewCommand(attackCommand);
        _test.True(
            attackPreview?.allowed == true,
            $"标记后基础攻击应可执行：{string.Join(" | ", attackPreview?.LogLinesTyped ?? Array.Empty<string>())}"
        );
        BattleEventBatch attackBatch = fixture.Runtime.IssueCommand(attackCommand);

        _test.Eq(
            learnedProgress.current_mastery > masteryBefore,
            true,
            "已学习猎人标记时，标记后由施放者武器命中并触发追加伤害应给猎人标记增加熟练度。"
        );

        GodotSharpCleanup.DisposeBatch(attackBatch);
        GodotSharpCleanup.ClearRuntimeReferences(attackCommand);
        GodotSharpCleanup.DisposeBatch(batch);
        GodotSharpCleanup.ClearRuntimeReferences(command);
        BattleTestFixture.DisposeBattleState(state);
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        HunterAxeFixture fixture,
        BattleUnitState equipped,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityService service = new(
            fixture.SkillDefs,
            fixture.Bindings,
            fixture.ItemDefs
        );
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = equipped,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                BattleState = state,
            }
        );
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry entry
    )
    {
        foreach (BattleAvailableSkillEntry candidate in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (candidate != null && candidate.EntryRef.SkillId == skillId)
            {
                entry = candidate;
                return true;
            }
        }
        entry = null;
        return false;
    }

    private void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId
    )
    {
        _test.True(unit.effective_trait_ids.Contains(traitId), $"{traitId} 应投影到战斗单位。");
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return;
        }
        _test.Fail($"{bindingId} 应投影为装备能力源。");
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

    private sealed class HunterAxeFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private HunterAxeFixture(
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
        internal IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static HunterAxeFixture Build()
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
            return new HunterAxeFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal UnitSkillProgress SetLearnedHunterMark(
            int level,
            int currentMastery,
            bool isCore = false
        )
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            UnitProgress progression = member.progression as UnitProgress;
            UnitSkillProgress progress = new()
            {
                skill_id = HunterMarkSkillId,
                is_learned = true,
                skill_level = level,
                current_mastery = currentMastery,
                is_core = isCore,
                is_level_trigger_locked = level > 3,
                granted_source_type = "player",
            };
            progression?.SetSkillProgress(progress);
            return progress;
        }

        internal UnitSkillProgress GetHunterMarkProgress()
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            UnitProgress progression = member?.progression as UnitProgress;
            return progression?.GetSkillProgress(HunterMarkSkillId);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildHunterAxeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                HunterAxeItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(HunterAxeItemId, $"eq_hunter_axe_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.SetCombatResources(80, 0, 100, 0, 2, 2);
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 8);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 8);
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

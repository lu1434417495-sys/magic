using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_memoryeater_vine_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_rapier_memoryeater_vine";
    private static readonly StringName LifebloodLedgerTraitId =
        "weapon.rapier.memoryeater_vine.lifeblood_ledger";
    private static readonly StringName SymbioticSiphonTraitId =
        "weapon.rapier.memoryeater_vine.symbiotic_siphon";
    private static readonly StringName MemoryThornEdgeTraitId =
        "weapon.rapier.memoryeater_vine.memory_thorn_edge";
    private static readonly StringName StoryRootSnareTraitId =
        "weapon.rapier.memoryeater_vine.story_root_snare";
    private static readonly StringName MourningVineLungeTraitId =
        "weapon.rapier.memoryeater_vine.mourning_vine_lunge";
    private static readonly StringName BlackBloomAwakeningTraitId =
        "weapon.rapier.memoryeater_vine.black_bloom_awakening";
    private static readonly StringName LifebloodLedgerBindingId =
        "binding.weapon.rapier.memoryeater_vine.lifeblood_ledger";
    private static readonly StringName SymbioticSiphonBindingId =
        "binding.weapon.rapier.memoryeater_vine.symbiotic_siphon";
    private static readonly StringName MemoryThornEdgeBindingId =
        "binding.weapon.rapier.memoryeater_vine.memory_thorn_edge";
    private static readonly StringName StoryRootSnareBindingId =
        "binding.weapon.rapier.memoryeater_vine.story_root_snare";
    private static readonly StringName MourningVineLungeBindingId =
        "binding.weapon.rapier.memoryeater_vine.mourning_vine_lunge";
    private static readonly StringName BlackBloomAwakeningBindingId =
        "binding.weapon.rapier.memoryeater_vine.black_bloom_awakening";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        Run();
    }

    private void Run()
    {
        try
        {
            TestContentLoadsProjectsLegendaryNameAndSixTraits();
            TestLifebloodCounterIncrementsOnlyForThisWeaponAttackAgainstLivingKinds();
            TestLifebloodTierScalesDamageFromPersistentEquipmentConfig();
            TestImmediateWeaponAttackKillAlsoAddsLifeblood();
            RequestTestExit(_test.Finish("Memoryeater Vine weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Memoryeater Vine weapon ability regression"));
        }
    }

    private void TestContentLoadsProjectsLegendaryNameAndSixTraits()
    {
        using MemoryeaterFixture fixture = MemoryeaterFixture.Build(Array.Empty<int>());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含噬忆血蔓。");
        _test.True(fixture.TraitDefs.ContainsKey(LifebloodLedgerTraitId), "应包含生命簿血计 trait。");
        _test.True(fixture.TraitDefs.ContainsKey(SymbioticSiphonTraitId), "应包含共生虹吸 trait。");
        _test.True(fixture.TraitDefs.ContainsKey(MemoryThornEdgeTraitId), "应包含忆刺锋芽 trait。");
        _test.True(fixture.TraitDefs.ContainsKey(StoryRootSnareTraitId), "应包含故事根缚 trait。");
        _test.True(fixture.TraitDefs.ContainsKey(MourningVineLungeTraitId), "应包含哀藤追刺 trait。");
        _test.True(fixture.TraitDefs.ContainsKey(BlackBloomAwakeningTraitId), "应包含黑花将醒 trait。");
        _test.True(fixture.Bindings.ContainsKey(LifebloodLedgerBindingId), "应包含生命簿血计 binding。");
        _test.True(fixture.Bindings.ContainsKey(SymbioticSiphonBindingId), "应包含共生虹吸 binding。");
        _test.True(fixture.Bindings.ContainsKey(MemoryThornEdgeBindingId), "应包含忆刺锋芽 binding。");
        _test.True(fixture.Bindings.ContainsKey(StoryRootSnareBindingId), "应包含故事根缚 binding。");
        _test.True(fixture.Bindings.ContainsKey(MourningVineLungeBindingId), "应包含哀藤追刺 binding。");
        _test.True(fixture.Bindings.ContainsKey(BlackBloomAwakeningBindingId), "应包含黑花将醒 binding。");
        if (!fixture.ItemDefs.ContainsKey(ItemId))
            return;

        using TestContentResourceLoader contentLoader = new();
        ItemDef rawItem = contentLoader.LoadCanonical<ItemDef>(
            "res://data/configs/items/weapon_unique_rapier_memoryeater_vine.tres"
        );
        _test.True(rawItem != null, "噬忆血蔓原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "噬忆血蔓 item_id 不应包含来源编号。");
            _test.Eq(rawItem.display_name, "噬忆血蔓", "装备名应更具传奇性。");
            _test.True(ContainsText(rawItem.description, "生命故事"), "简介应跟随新名称与生命故事主题更新。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_rapier_base"), "噬忆血蔓应继承 rapier 模板。");
            _test.Eq(rawItem.base_price, 75000, "噬忆血蔓基础价格应保持 75000。");
            _test.Eq(rawItem.trait_ids.Count, 6, "噬忆血蔓应固定 6 个特性。");
            _test.True(ContainsStringName(rawItem.trait_ids, LifebloodLedgerTraitId), "应固定生命簿血计。");
            _test.True(ContainsStringName(rawItem.trait_ids, SymbioticSiphonTraitId), "应固定共生虹吸。");
            _test.True(ContainsStringName(rawItem.trait_ids, MemoryThornEdgeTraitId), "应固定忆刺锋芽。");
            _test.True(ContainsStringName(rawItem.trait_ids, StoryRootSnareTraitId), "应固定故事根缚。");
            _test.True(ContainsStringName(rawItem.trait_ids, MourningVineLungeTraitId), "应固定哀藤追刺。");
            _test.True(ContainsStringName(rawItem.trait_ids, BlackBloomAwakeningTraitId), "应固定黑花将醒。");
        }

        ModifyAbilityStateActionPayloadDefinition lifebloodAction =
            FindLifebloodModifyStatePayload(fixture.Bindings[LifebloodLedgerBindingId]);
        EquipmentAbilityStateSchemaDefinition tierSchema = FindSyncedStateSchema(
            fixture.Bindings[LifebloodLedgerBindingId],
            lifebloodAction?.StateKey ?? ""
        );
        _test.True(
            lifebloodAction?.StateKey != "",
            "生命簿血计必须把持久计数 state_key 声明在装备配置中。"
        );
        _test.True(
            tierSchema != null,
            "血阶同步关系必须声明在装备状态 schema 上，动作只写生命簿源状态。"
        );
        _test.Eq(
            tierSchema?.SyncAggregation ?? "",
            new StringName("floor_div"),
            "血阶应由生命簿按 floor_div 同步。"
        );
        _test.Eq(tierSchema?.SyncIntLiteral ?? 0, 10, "每 10 点生命簿应同步为 1 点血阶。");
        _test.True(
            IsPersistentStateKey(
                fixture.Bindings[LifebloodLedgerBindingId],
                tierSchema?.StateKey ?? ""
            ),
            "血阶 state_key 必须在装备配置中声明为持久状态。"
        );

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildMemoryeaterUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "噬忆血蔓装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("rapier"), "噬忆血蔓应投影为 rapier。");
        _test.Eq(equipped.weapon_family, new StringName("sword"), "噬忆血蔓武器族应为 sword。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "噬忆血蔓基础伤害应为 pierce。");
        _test.Eq(equipped.weapon_attack_range, 1, "噬忆血蔓攻击距离应为 1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "噬忆血蔓应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "噬忆血蔓应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "噬忆血蔓应为 1D8+3。");
        AssertUnitHasTraitAndAbilitySource(equipped, LifebloodLedgerTraitId, LifebloodLedgerBindingId, "eq_memoryeater_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, SymbioticSiphonTraitId, SymbioticSiphonBindingId, "eq_memoryeater_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, MemoryThornEdgeTraitId, MemoryThornEdgeBindingId, "eq_memoryeater_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, StoryRootSnareTraitId, StoryRootSnareBindingId, "eq_memoryeater_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, MourningVineLungeTraitId, MourningVineLungeBindingId, "eq_memoryeater_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, BlackBloomAwakeningTraitId, BlackBloomAwakeningBindingId, "eq_memoryeater_projection");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除噬忆血蔓后 weapon_item_id 应清空。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除后装备 trait 实例应回到装备前状态。"
        );
        BattleTestFixture.DisposeBattleUnit(equipped);
        BattleTestFixture.DisposeBattleUnit(baseline);
    }

    private void TestLifebloodCounterIncrementsOnlyForThisWeaponAttackAgainstLivingKinds()
    {
        using MemoryeaterFixture humanoidFixture = MemoryeaterFixture.Build(new[] { 8 });
        ModifyAbilityStateActionPayloadDefinition humanoidAction =
            FindLifebloodModifyStatePayload(humanoidFixture.Bindings[LifebloodLedgerBindingId]);
        BattleUnitState humanoidAttacker = humanoidFixture.BuildMemoryeaterUnit("counter_humanoid");
        EquipmentInstanceState humanoidInstance = FindWeaponInstance(humanoidAttacker);
        StringName humanoidTierStateKey = FindSyncedStateKey(
            humanoidFixture.Bindings[LifebloodLedgerBindingId],
            humanoidAction.StateKey
        );
        SetPersistentCounterValue(humanoidInstance, LifebloodLedgerBindingId, humanoidAction.StateKey, 9);
        SetPersistentCounterValue(humanoidInstance, LifebloodLedgerBindingId, humanoidTierStateKey, 0);
        BattleUnitState humanoid = BuildEnemy("lifeblood_humanoid", new Vector2I(1, 0), 8, "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            humanoidFixture.Runtime,
            humanoidAttacker,
            humanoid,
            "memoryeater_counter_humanoid",
            previewCommand: false
        );
        _test.False(humanoid.is_alive, "真实基础攻击应击杀 humanoid 目标。");
        _test.Eq(
            GetPersistentCounterValue(humanoidInstance, LifebloodLedgerBindingId, humanoidAction.StateKey),
            10L,
            "第 10 次合格击杀应保存生命簿为 10。"
        );
        _test.Eq(
            GetPersistentCounterValue(humanoidInstance, LifebloodLedgerBindingId, humanoidTierStateKey),
            1L,
            "第 10 次合格击杀应同步保存血阶为 1。"
        );

        using MemoryeaterFixture constructFixture = MemoryeaterFixture.Build(new[] { 8 });
        ModifyAbilityStateActionPayloadDefinition constructAction =
            FindLifebloodModifyStatePayload(constructFixture.Bindings[LifebloodLedgerBindingId]);
        StringName constructTierStateKey = FindSyncedStateKey(
            constructFixture.Bindings[LifebloodLedgerBindingId],
            constructAction.StateKey
        );
        BattleUnitState constructAttacker = constructFixture.BuildMemoryeaterUnit("counter_construct");
        EquipmentInstanceState constructInstance = FindWeaponInstance(constructAttacker);
        BattleUnitState construct = BuildEnemy("lifeblood_construct", new Vector2I(1, 0), 8, "construct");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            constructFixture.Runtime,
            constructAttacker,
            construct,
            "memoryeater_counter_construct",
            previewCommand: false
        );
        _test.False(construct.is_alive, "构装体场景应确实击杀目标。");
        _test.Eq(GetPersistentCounterValue(constructInstance, LifebloodLedgerBindingId, constructAction.StateKey), 0L, "构装体击杀不应增加生命簿。");
        _test.Eq(GetPersistentCounterValue(constructInstance, LifebloodLedgerBindingId, constructTierStateKey), 0L, "构装体击杀不应改变保存血阶。");

        using MemoryeaterFixture undeadFixture = MemoryeaterFixture.Build(new[] { 8 });
        ModifyAbilityStateActionPayloadDefinition undeadAction =
            FindLifebloodModifyStatePayload(undeadFixture.Bindings[LifebloodLedgerBindingId]);
        StringName undeadTierStateKey = FindSyncedStateKey(
            undeadFixture.Bindings[LifebloodLedgerBindingId],
            undeadAction.StateKey
        );
        BattleUnitState undeadAttacker = undeadFixture.BuildMemoryeaterUnit("counter_undead");
        EquipmentInstanceState undeadInstance = FindWeaponInstance(undeadAttacker);
        BattleUnitState undead = BuildEnemy("lifeblood_undead", new Vector2I(1, 0), 8, "undead");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            undeadFixture.Runtime,
            undeadAttacker,
            undead,
            "memoryeater_counter_undead",
            previewCommand: false
        );
        _test.False(undead.is_alive, "亡灵场景应确实击杀目标。");
        _test.Eq(GetPersistentCounterValue(undeadInstance, LifebloodLedgerBindingId, undeadAction.StateKey), 0L, "亡灵击杀不应增加生命簿。");
        _test.Eq(GetPersistentCounterValue(undeadInstance, LifebloodLedgerBindingId, undeadTierStateKey), 0L, "亡灵击杀不应改变保存血阶。");

        using MemoryeaterFixture directFixture = MemoryeaterFixture.Build(Array.Empty<int>());
        ModifyAbilityStateActionPayloadDefinition directAction =
            FindLifebloodModifyStatePayload(directFixture.Bindings[LifebloodLedgerBindingId]);
        StringName directTierStateKey = FindSyncedStateKey(
            directFixture.Bindings[LifebloodLedgerBindingId],
            directAction.StateKey
        );
        BattleUnitState directAttacker = directFixture.BuildMemoryeaterUnit("counter_direct");
        EquipmentInstanceState directInstance = FindWeaponInstance(directAttacker);
        SetPersistentCounterValue(directInstance, LifebloodLedgerBindingId, directAction.StateKey, 10);
        SetPersistentCounterValue(directInstance, LifebloodLedgerBindingId, directTierStateKey, 1);
        BattleUnitState directKill = BuildEnemy("lifeblood_direct", new Vector2I(1, 0), 0, "humanoid");
        directKill.is_alive = false;
        directFixture.Runtime._collect_defeated_unit_loot(directKill, directAttacker);
        _test.Eq(GetPersistentCounterValue(directInstance, LifebloodLedgerBindingId, directAction.StateKey), 10L, "没有攻击来源证明的击杀不应增加生命簿。");
        _test.Eq(GetPersistentCounterValue(directInstance, LifebloodLedgerBindingId, directTierStateKey), 1L, "没有攻击来源证明的击杀不应改变保存血阶。");
    }

    private void TestLifebloodTierScalesDamageFromPersistentEquipmentConfig()
    {
        using MemoryeaterFixture fixture = MemoryeaterFixture.Build(new[] { 4, 4, 4, 4, 4, 4, 4, 4 });
        ModifyAbilityStateActionPayloadDefinition lifebloodAction =
            FindLifebloodModifyStatePayload(fixture.Bindings[LifebloodLedgerBindingId]);
        StringName stateKey = lifebloodAction.StateKey;
        StringName tierStateKey = FindSyncedStateKey(
            fixture.Bindings[LifebloodLedgerBindingId],
            stateKey
        );

        BattleUnitState tierZeroAttacker = fixture.BuildMemoryeaterUnit("tier_zero");
        BattleUnitState tierZeroTarget = BuildEnemy("tier_zero_target", new Vector2I(1, 0), 100, "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            tierZeroAttacker,
            tierZeroTarget,
            "memoryeater_tier_zero",
            previewCommand: false
        );
        int tierZeroDamage = 100 - tierZeroTarget.current_hp;

        BattleUnitState unsyncedAttacker = fixture.BuildMemoryeaterUnit("tier_unsynced");
        SetPersistentCounterValue(FindWeaponInstance(unsyncedAttacker), LifebloodLedgerBindingId, stateKey, 30);
        SetPersistentCounterValue(FindWeaponInstance(unsyncedAttacker), LifebloodLedgerBindingId, tierStateKey, 0);
        BattleUnitState unsyncedTarget = BuildEnemy("tier_unsynced_target", new Vector2I(1, 0), 100, "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            unsyncedAttacker,
            unsyncedTarget,
            "memoryeater_tier_unsynced",
            previewCommand: false
        );
        int unsyncedDamage = 100 - unsyncedTarget.current_hp;

        BattleUnitState tierOneAttacker = fixture.BuildMemoryeaterUnit("tier_one");
        SetPersistentCounterValue(FindWeaponInstance(tierOneAttacker), LifebloodLedgerBindingId, stateKey, 10);
        SetPersistentCounterValue(FindWeaponInstance(tierOneAttacker), LifebloodLedgerBindingId, tierStateKey, 1);
        BattleUnitState tierOneTarget = BuildEnemy("tier_one_target", new Vector2I(1, 0), 100, "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            tierOneAttacker,
            tierOneTarget,
            "memoryeater_tier_one",
            previewCommand: false
        );
        int tierOneDamage = 100 - tierOneTarget.current_hp;

        BattleUnitState tierThreeAttacker = fixture.BuildMemoryeaterUnit("tier_three");
        SetPersistentCounterValue(FindWeaponInstance(tierThreeAttacker), LifebloodLedgerBindingId, stateKey, 30);
        SetPersistentCounterValue(FindWeaponInstance(tierThreeAttacker), LifebloodLedgerBindingId, tierStateKey, 3);
        BattleUnitState tierThreeTarget = BuildEnemy("tier_three_target", new Vector2I(1, 0), 100, "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            tierThreeAttacker,
            tierThreeTarget,
            "memoryeater_tier_three",
            previewCommand: false
        );
        int tierThreeDamage = 100 - tierThreeTarget.current_hp;

        _test.Eq(unsyncedDamage, tierZeroDamage, "只有生命簿=30 但保存血阶=0 时不应实时计算出追加伤害。");
        _test.True(tierOneDamage > tierZeroDamage, "保存血阶 1 应解锁并追加忆刺伤害。");
        _test.True(tierThreeDamage > tierOneDamage, "保存血阶 3 应继续放大追加骰，证明成长不是固定上限。");
    }

    private void TestImmediateWeaponAttackKillAlsoAddsLifeblood()
    {
        using MemoryeaterFixture fixture = MemoryeaterFixture.Build(new[] { 8, 8, 8, 8 });
        ModifyAbilityStateActionPayloadDefinition lifebloodAction =
            FindLifebloodModifyStatePayload(fixture.Bindings[LifebloodLedgerBindingId]);
        StringName stateKey = lifebloodAction.StateKey;
        StringName tierStateKey = FindSyncedStateKey(
            fixture.Bindings[LifebloodLedgerBindingId],
            stateKey
        );
        BattleUnitState attacker = fixture.BuildMemoryeaterUnit("lunge");
        EquipmentInstanceState instance = FindWeaponInstance(attacker);
        SetPersistentCounterValue(instance, LifebloodLedgerBindingId, stateKey, 59);
        SetPersistentCounterValue(instance, LifebloodLedgerBindingId, tierStateKey, 5);

        BattleUnitState firstDefeated = BuildEnemy("lunge_first", new Vector2I(1, 0), 0, "humanoid");
        firstDefeated.is_alive = false;
        BattleUnitState followupTarget = BuildEnemy("lunge_followup", new Vector2I(1, 1), 8, "humanoid");
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "memoryeater_lunge",
            attacker,
            firstDefeated,
            mapSize: new Vector2I(6, 6)
        );
        state.SetUnit(followupTarget);
        state.enemy_unit_ids.Add(followupTarget.unit_id);
        fixture.Runtime.SetupStateForTests(state);

        using BattleEventBatch batch = new();
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = attacker,
                DefeatedUnit = firstDefeated,
                BattleState = state,
                Batch = batch,
                KillProvenance = BattleKillProvenance.ForEquipmentAttack(
                    FindSource(attacker, LifebloodLedgerBindingId)?.SourceEquipmentInstanceId ?? "",
                    LifebloodLedgerBindingId,
                    "test.initial_weapon_attack"
                ),
            }
        );

        _test.False(followupTarget.is_alive, "血阶 5 解锁的哀藤追刺应以立即武器攻击击杀相邻敌人。");
        _test.Eq(
            GetPersistentCounterValue(instance, LifebloodLedgerBindingId, stateKey),
            61L,
            "初始击杀和血蔓触发的追刺击杀都应各自增加生命簿。"
        );
        _test.Eq(
            GetPersistentCounterValue(instance, LifebloodLedgerBindingId, tierStateKey),
            6L,
            "追刺链上的击杀也应让保存血阶跟着生命簿同步。"
        );
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedEquipmentInstanceId
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
        if (source.SourceEquipmentInstanceId != expectedEquipmentInstanceId)
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedEquipmentInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp, StringName creatureTag)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            enemy_template_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            control_mode = "ai",
            is_alive = hp > 0,
            current_hp = Math.Max(hp, 0),
            body_size = 1,
            body_size_category = "medium",
        };
        unit.SetCombatResources(Math.Max(hp, 1), 0, 30, 0, 2, 2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hp, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.creature_type_tags.Add(creatureTag);
        unit.SetAnchorCoord(coord);
        unit.RefreshFootprint();
        return unit;
    }

    private static EquipmentInstanceState FindWeaponInstance(BattleUnitState unit) =>
        unit?.GetEquipmentView()?.GetEquippedInstance("main_hand");

    private static ModifyAbilityStateActionPayloadDefinition FindLifebloodModifyStatePayload(
        EquipmentAbilityBindingDefinition binding
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition
                    is ModifyAbilityStateActionPayloadDefinition payload
                )
                    return payload;
            }
        }
        return null;
    }

    private static bool IsPersistentStateKey(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        foreach (
            EquipmentAbilityStateSchemaDefinition schema in binding?.StateSchemas
                ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>()
        )
        {
            if (schema?.StateKey == stateKey && schema?.ResetTiming == "persistent_counter")
                return true;
        }
        return false;
    }

    private static StringName FindSyncedStateKey(
        EquipmentAbilityBindingDefinition binding,
        StringName sourceStateKey
    ) => FindSyncedStateSchema(binding, sourceStateKey)?.StateKey ?? "";

    private static EquipmentAbilityStateSchemaDefinition FindSyncedStateSchema(
        EquipmentAbilityBindingDefinition binding,
        StringName sourceStateKey
    )
    {
        foreach (EquipmentAbilityStateSchemaDefinition schema in binding?.StateSchemas ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>())
        {
            if (schema?.SyncSourceStateKey == sourceStateKey)
                return schema;
        }
        return null;
    }

    private static long GetPersistentCounterValue(
        EquipmentInstanceState instance,
        StringName bindingId,
        StringName stateKey
    )
    {
        string counterId = BuildCounterId(bindingId, stateKey);
        foreach (EquipmentAbilityPersistentCounterState counter in instance?.ability_persistent_counters ?? new())
        {
            if (counter != null && counter.CounterId == counterId)
                return counter.Value;
        }
        return 0L;
    }

    private static void SetPersistentCounterValue(
        EquipmentInstanceState instance,
        StringName bindingId,
        StringName stateKey,
        long value
    )
    {
        string counterId = BuildCounterId(bindingId, stateKey);
        foreach (EquipmentAbilityPersistentCounterState counter in instance?.ability_persistent_counters ?? new())
        {
            if (counter != null && counter.CounterId == counterId)
            {
                counter.Value = Math.Max(value, 0L);
                return;
            }
        }
        instance?.ability_persistent_counters.Add(
            new EquipmentAbilityPersistentCounterState
            {
                CounterId = counterId,
                Value = Math.Max(value, 0L),
            }
        );
    }

    private static string BuildCounterId(StringName bindingId, StringName stateKey) =>
        $"{bindingId}:{stateKey}";

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static bool ContainsText(string value, string needle) =>
        !string.IsNullOrEmpty(value)
        && !string.IsNullOrEmpty(needle)
        && value.Contains(needle, StringComparison.Ordinal);

    private sealed class MemoryeaterFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private MemoryeaterFixture(
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime,
            ContentSnapshot snapshot
        )
        {
            _characterManagement = characterManagement;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = snapshot.Items;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static MemoryeaterFixture Build(IEnumerable<int> damageRolls)
        {
            CharacterManagementModule characterManagement = null;
            BattleRuntimeModule runtime = null;
            try
            {
                ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
                PartyState partyState = BuildPartyState("hero");
                characterManagement = new CharacterManagementModule();
                characterManagement.setup(
                    partyState,
                    snapshot.Skills,
                    snapshot.Professions,
                    snapshot.Achievements,
                    snapshot.Items,
                    snapshot.Quests,
                    snapshot.Traits,
                    null,
                    new ProgressionIdentityCatalogData()
                );

                runtime = new BattleRuntimeModule();
                runtime.setup(
                    characterManagement,
                    snapshot.Skills,
                    enemy_templates: new Dictionary<StringName, EnemyTemplateDefinition>(),
                    item_defs: snapshot.Items,
                    trait_defs: snapshot.Traits,
                    equipment_ability_bindings: snapshot.EquipmentAbilityBindings
                );
                using GArray damageRollPayload = new();
                foreach (int roll in damageRolls ?? new[] { 4, 4, 4, 4 })
                    damageRollPayload.Add(roll);
                BattleTestFixture.ConfigureDamageResolverForTests(
                    runtime,
                    new FixedRollDamageResolver(damageRollPayload)
                );
                BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
                return new MemoryeaterFixture(
                    characterManagement,
                    partyState,
                    runtime,
                    snapshot
                );
            }
            catch
            {
                BattleTestFixture.DisposeRuntime(runtime);
                characterManagement?.Dispose();
                throw;
            }
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildMemoryeaterUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new StringName[] { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_memoryeater_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
            unit.SetCombatResources(60, 0, 30, 0, 2, 2);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 60);
            return unit;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
            _characterManagement?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, null);
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

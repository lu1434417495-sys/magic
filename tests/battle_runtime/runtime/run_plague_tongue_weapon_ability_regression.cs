using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_plague_tongue_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName PlagueTongueItemId =
        "weapon_unique_axe_plague_tongue_099";
    private static readonly StringName PoisonTouchDamageTraitId =
        "weapon.axe.plague_tongue.poison_touch_damage";
    private static readonly StringName AxeFeverTraitId =
        "weapon.axe.plague_tongue.axe_fever";
    private static readonly StringName PlagueSpreadTraitId =
        "weapon.axe.plague_tongue.plague_spread";
    private static readonly StringName ImmuneCarrierTraitId =
        "weapon.axe.plague_tongue.immune_carrier";
    private static readonly StringName PoisonTouchDamageBindingId =
        "binding.weapon.axe.plague_tongue.poison_touch_damage";
    private static readonly StringName AxeFeverBindingId =
        "binding.weapon.axe.plague_tongue.axe_fever";
    private static readonly StringName PlagueSpreadBindingId =
        "binding.weapon.axe.plague_tongue.plague_spread";
    private static readonly StringName ImmuneCarrierBindingId =
        "binding.weapon.axe.plague_tongue.immune_carrier";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPlagueTongueProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestPoisonTouchAddsIndependentPoisonDamageOnHit();
            TestAxeFeverAppliesOnFailedAfterHitSaveAndSkipsOnSuccess();
            TestAxeFeverDealsOneD4TimelineDamageEverySixtyTu();
            TestPlagueSpreadCreatesBattleLifetimeCloudSixtyTuAfterHolderKill();
            TestPlagueCloudContactAppliesAxeFeverAndHonorsCarrierImmunity();
            RequestTestExit(_test.Finish("Plague Tongue weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Plague Tongue weapon ability regression"));
        }
    }

    private void TestPlagueTongueProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using PlagueTongueFixture fixture = PlagueTongueFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(PlagueTongueItemId), "真实物品内容应包含瘟疫之舌。");
        _test.True(
            fixture.TraitDefs.ContainsKey(PoisonTouchDamageTraitId),
            "真实 trait 内容应包含疫病之触毒素伤害。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(AxeFeverTraitId),
            "真实 trait 内容应包含斧刃热。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(PlagueSpreadTraitId),
            "真实 trait 内容应包含瘟疫传播。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(ImmuneCarrierTraitId),
            "真实 trait 内容应包含免疫携带者。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(PoisonTouchDamageBindingId),
            "真实装备能力内容应包含疫病之触毒素伤害 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(AxeFeverBindingId),
            "真实装备能力内容应包含斧刃热 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(PlagueSpreadBindingId),
            "真实装备能力内容应包含瘟疫传播 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ImmuneCarrierBindingId),
            "真实装备能力内容应包含免疫携带者 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(PlagueTongueItemId))
            return;

        ItemDef rawPlagueTongue = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_plague_tongue.tres"
        );
        _test.True(rawPlagueTongue != null, "瘟疫之舌原始资源应能加载。");
        if (rawPlagueTongue != null)
        {
            _test.Eq(
                rawPlagueTongue.base_item_id,
                new StringName("weapon_type_battleaxe_base"),
                "瘟疫之舌原始资源应声明继承 battleaxe 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildPlagueTongueUnit("projection");

        _test.Eq(
            equipped.weapon_item_id,
            PlagueTongueItemId,
            "瘟疫之舌装备后 unit 应保留真实 item_id。"
        );
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("battleaxe"),
            "瘟疫之舌应投影为 battleaxe。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "瘟疫之舌攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "瘟疫之舌应保留 versatile 属性。");
        _test.Eq(
            equipped.weapon_one_handed_dice?.dice_count ?? 0,
            1,
            "瘟疫之舌单手骰数量应为 1。"
        );
        _test.Eq(
            equipped.weapon_one_handed_dice?.dice_sides ?? 0,
            8,
            "瘟疫之舌单手骰面应为 D8。"
        );
        _test.Eq(
            equipped.weapon_one_handed_dice?.flat_bonus ?? 0,
            1,
            "瘟疫之舌单手骰固定加值应为 +1。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            PoisonTouchDamageTraitId,
            PoisonTouchDamageBindingId,
            "eq_plague_tongue_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            AxeFeverTraitId,
            AxeFeverBindingId,
            "eq_plague_tongue_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            PlagueSpreadTraitId,
            PlagueSpreadBindingId,
            "eq_plague_tongue_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ImmuneCarrierTraitId,
            ImmuneCarrierBindingId,
            "eq_plague_tongue_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除瘟疫之舌后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除瘟疫之舌后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.weapon_attack_range,
            baseline.weapon_attack_range,
            "移除瘟疫之舌后攻击距离应回到装备前状态。"
        );
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除瘟疫之舌后装备能力源应清空。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除瘟疫之舌后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestPoisonTouchAddsIndependentPoisonDamageOnHit()
    {
        using PlagueTongueFixture fixture = PlagueTongueFixture.Build(new GArray { 4, 6 });
        BattleUnitState attacker = fixture.BuildPlagueTongueUnit("poison_touch");
        BattleUnitState target = BuildTarget("poison_touch_target", new Vector2I(1, 0));
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "plague_tongue_poison_touch",
            previewCommand: false
        );
        int poisonTouchDamage = 100 - target.current_hp;

        using PlagueTongueFixture plainFixture = PlagueTongueFixture.Build(new GArray { 4, 6 });
        BattleUnitState plainAttacker = plainFixture.BuildPlagueTongueUnit("poison_touch_plain");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildTarget("poison_touch_plain_target", new Vector2I(1, 0));
        plainTarget.current_hp = 100;
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        plainTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "plague_tongue_poison_touch_plain",
            previewCommand: false
        );
        int plainWeaponDamage = 100 - plainTarget.current_hp;
        _test.True(
            poisonTouchDamage > plainWeaponDamage,
            "瘟疫之舌真实基础攻击应比同一武器移除装备能力源后造成更多 HP 伤害。"
        );
    }

    private void TestAxeFeverAppliesOnFailedAfterHitSaveAndSkipsOnSuccess()
    {
        using PlagueTongueFixture failFixture = PlagueTongueFixture.Build(
            new GArray(),
            afterHitSaveRollOverride: 1
        );
        _test.False(
            BattleStatusSemanticTable.HasSemantic("axe_fever"),
            "斧刃热状态语义应由瘟疫之舌装备配置提供，不应硬编码在全局状态表。"
        );
        BattleUnitState attacker = failFixture.BuildPlagueTongueUnit("axe_fever");
        BattleUnitState failedTarget = BuildTarget("axe_fever_failed", new Vector2I(1, 0));
        failedTarget.current_hp = 100;
        failedTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        failedTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            failFixture.Runtime,
            attacker,
            failedTarget,
            "plague_tongue_axe_fever_failed"
        );

        BattleStatusEffectState fever = failedTarget.GetStatusEffect("axe_fever");
        _test.True(fever != null, "斧刃热应在命中后 DC14 体质/毒素豁免失败时施加。");
        _test.Eq(fever?.duration ?? -1, 300, "斧刃热持续时间应为 300TU。");
        _test.Eq(fever?.stack_behavior ?? new StringName(""), new StringName("refresh"), "斧刃热应由配置声明刷新叠层。");
        _test.Eq(fever?.stack_limit ?? 0, 1, "斧刃热应由配置声明最多 1 层。");
        _test.Eq(fever?.display_label ?? "", "斧刃热", "斧刃热显示名应来自装备配置。");
        _test.True(fever?.counts_as_debuff_override == true, "斧刃热应由配置显式声明 debuff 归类。");
        _test.True(fever?.counts_as_debuff == true, "斧刃热应计为 debuff。");
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(fever),
            "斧刃热应由配置声明为可驱散 harmful magic。"
        );
        _test.Eq(fever?.source_unit_id ?? new StringName(""), attacker.unit_id, "斧刃热来源应记录持有者。");

        using PlagueTongueFixture successFixture = PlagueTongueFixture.Build(
            new GArray(),
            afterHitSaveRollOverride: 20
        );
        BattleUnitState successAttacker = successFixture.BuildPlagueTongueUnit("axe_fever_success");
        BattleUnitState successTarget = BuildTarget("axe_fever_success", new Vector2I(1, 0));
        successTarget.current_hp = 100;
        successTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        successTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            successFixture.Runtime,
            successAttacker,
            successTarget,
            "plague_tongue_axe_fever_success"
        );
        _test.False(
            successTarget.HasStatusEffect("axe_fever"),
            "斧刃热在体质/毒素豁免成功时不应施加。"
        );
    }

    private void TestAxeFeverDealsOneD4TimelineDamageEverySixtyTu()
    {
        using PlagueTongueFixture fixture = PlagueTongueFixture.Build(
            new GArray { 4 },
            afterHitSaveRollOverride: 1
        );
        BattleUnitState attacker = fixture.BuildPlagueTongueUnit("axe_fever_tick");
        BattleUnitState target = BuildTarget("axe_fever_tick_target", new Vector2I(1, 0));
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "plague_tongue_axe_fever_tick"
        );
        int hpAfterHit = target.current_hp;

        BattleStatusEffectState fever = target.GetStatusEffect("axe_fever");
        _test.True(fever != null, "斧刃热应先被施加，才能结算周期伤害。");
        _test.Eq(fever?.tick_interval_tu ?? 0, 60, "斧刃热每 60TU 触发一次周期伤害。");
        _test.Eq(fever?.timeline_damage_dice_count ?? 0, 1, "斧刃热周期伤害应记录 1D4。");
        _test.Eq(fever?.timeline_damage_dice_sides ?? 0, 4, "斧刃热周期伤害骰面应为 D4。");

        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 60);

        _test.Eq(target.current_hp, hpAfterHit - 4, "固定周期伤害骰 4 时，斧刃热首跳应损失 4 HP。");
        _test.True(target.HasStatusEffect("axe_fever"), "首跳后斧刃热未到 300TU，不应移除。");
    }

    private void TestPlagueSpreadCreatesBattleLifetimeCloudSixtyTuAfterHolderKill()
    {
        using PlagueTongueFixture fixture = PlagueTongueFixture.Build(new GArray());
        BattleUnitState killer = fixture.BuildPlagueTongueUnit("plague_spread");
        killer.SetAnchorCoord(new Vector2I(1, 2));
        BattleUnitState defeated = BuildDefeatedEnemyUnit(
            "plague_spread_victim",
            new Vector2I(2, 2)
        );
        BattleState state = BuildFlatState("plague_tongue_spread", new Vector2I(5, 5));
        state.SetUnit(killer);
        state.SetUnit(defeated);
        state.ally_unit_ids.Add(killer.unit_id);
        state.enemy_unit_ids.Add(defeated.unit_id);
        fixture.Runtime.SetupStateForTests(state);

        fixture.Runtime._collect_defeated_unit_loot(defeated, killer);

        _test.Eq(
            CountTerrainEffects(state, "plague_cloud"),
            0,
            "击杀收集当下不应立刻生成瘟疫云。"
        );
        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 55);
        _test.Eq(
            CountTerrainEffects(state, "plague_cloud"),
            0,
            "瘟疫传播固定延迟 60TU；55TU 时不应生成。"
        );
        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 5);

        _test.Eq(
            CountTerrainEffects(state, "plague_cloud"),
            5,
            "瘟疫云应在尸体格 diamond 半径 1 内生成 5 个 terrain effect。"
        );
        BattleTerrainEffectState centerCloud =
            FindTerrainEffect(state, new Vector2I(2, 2), "plague_cloud");
        _test.True(centerCloud != null, "尸体所在格应生成 plague_cloud。");
        _test.Eq(centerCloud?.lifetime_policy ?? new StringName(""), new StringName("battle"), "plague_cloud 应为 battle lifetime。");
        _test.Eq(centerCloud?.effect_type ?? new StringName(""), new StringName("none"), "plague_cloud 本身不应按周期 tick 结算。");

        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 300);
        _test.Eq(
            CountTerrainEffects(state, "plague_cloud"),
            5,
            "battle lifetime 的 plague_cloud 推进时间后仍应存在。"
        );
    }

    private void TestPlagueCloudContactAppliesAxeFeverAndHonorsCarrierImmunity()
    {
        using PlagueTongueFixture fixture = PlagueTongueFixture.Build(new GArray());
        BattleUnitState killer = fixture.BuildPlagueTongueUnit("plague_cloud_contact");
        killer.SetAnchorCoord(new Vector2I(1, 2));
        BattleUnitState defeated = BuildDefeatedEnemyUnit(
            "plague_cloud_contact_victim",
            new Vector2I(2, 2)
        );
        BattleUnitState failedTarget = BuildTarget(
            "plague_cloud_contact_failed",
            new Vector2I(2, 2)
        );
        BattleUnitState successTarget = BuildTarget(
            "plague_cloud_contact_success",
            new Vector2I(2, 1)
        );
        BattleState state = BuildFlatState("plague_tongue_cloud_contact", new Vector2I(5, 5));
        state.SetUnit(killer);
        state.SetUnit(defeated);
        state.SetUnit(failedTarget);
        state.SetUnit(successTarget);
        state.ally_unit_ids.Add(killer.unit_id);
        state.enemy_unit_ids.Add(defeated.unit_id);
        state.enemy_unit_ids.Add(failedTarget.unit_id);
        state.enemy_unit_ids.Add(successTarget.unit_id);
        fixture.Runtime.SetupStateForTests(state);

        fixture.Runtime._collect_defeated_unit_loot(defeated, killer);
        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 60);
        _test.Eq(
            CountTerrainEffects(state, "plague_cloud"),
            5,
            "接触感染测试需要先生成完整 plague_cloud。"
        );

        BattleEventBatch failedBatch = new();
        fixture.Runtime._terrain_effect_system.ApplyContactEffectsForUnit(
            failedTarget,
            BattleSaveContext.WithSaveRollOverride(1),
            failedBatch
        );
        BattleStatusEffectState fever = failedTarget.GetStatusEffect("axe_fever");
        _test.True(fever != null, "进入 plague_cloud 且 DC13 体质/毒素豁免失败时应感染斧刃热。");
        _test.Eq(fever?.duration ?? -1, 300, "瘟疫云感染的斧刃热持续 300TU。");
        _test.Eq(fever?.stack_behavior ?? new StringName(""), new StringName("refresh"), "瘟疫云感染的斧刃热应刷新叠层。");
        _test.Eq(fever?.stack_limit ?? 0, 1, "瘟疫云感染的斧刃热最多 1 层。");
        _test.Eq(fever?.display_label ?? "", "斧刃热", "瘟疫云感染的斧刃热显示名应来自接触状态配置。");
        _test.True(fever?.counts_as_debuff == true, "瘟疫云感染的斧刃热应计为 debuff。");
        _test.Eq(fever?.tick_interval_tu ?? 0, 60, "瘟疫云感染的斧刃热每 60TU 触发。");
        _test.Eq(fever?.timeline_damage_dice_count ?? 0, 1, "瘟疫云感染的斧刃热周期伤害应为 1D4。");
        _test.Eq(fever?.timeline_damage_dice_sides ?? 0, 4, "瘟疫云感染的斧刃热周期伤害骰面应为 D4。");
        _test.Eq(fever?.source_unit_id ?? new StringName(""), killer.unit_id, "瘟疫云感染来源应追溯到持有者。");

        fixture.Runtime._terrain_effect_system.ApplyContactEffectsForUnit(
            successTarget,
            BattleSaveContext.WithSaveRollOverride(20),
            new BattleEventBatch()
        );
        _test.False(
            successTarget.HasStatusEffect("axe_fever"),
            "进入 plague_cloud 但 DC13 体质/毒素豁免成功时不应感染。"
        );

        fixture.Runtime._terrain_effect_system.ApplyContactEffectsForUnit(
            killer,
            BattleSaveContext.WithSaveRollOverride(1),
            new BattleEventBatch()
        );
        _test.False(
            killer.HasStatusEffect("axe_fever"),
            "瘟疫之舌持有者应因免疫携带者 trait 免疫该武器传播的疾病。"
        );
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
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 30,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleUnitState BuildDefeatedEnemyUnit(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            enemy_template_id = "plague_spread_template",
            display_name = unitId.ToString(),
            faction_id = "hostile",
            control_mode = "ai",
            is_alive = false,
        };
        unit.SetAnchorCoord(coord);
        unit.creature_type_tags.Add("humanoid");
        return unit;
    }

    private static BattleState BuildFlatState(StringName battleId, Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
        }
        return state;
    }

    private static int CountTerrainEffects(BattleState state, StringName effectId)
    {
        int count = 0;
        if (state == null)
            return count;
        foreach (BattleState.BattleCellEntry entry in state.CellEntries())
        {
            foreach (BattleTerrainEffectState effect in entry.Cell?.timed_terrain_effects ?? new List<BattleTerrainEffectState>())
            {
                if (effect?.effect_id == effectId)
                    count++;
            }
        }
        return count;
    }

    private static BattleTerrainEffectState FindTerrainEffect(
        BattleState state,
        Vector2I coord,
        StringName effectId
    )
    {
        if (state == null || !state.TryGetCellTyped(coord, out BattleCellState cell))
            return null;
        foreach (BattleTerrainEffectState effect in cell.timed_terrain_effects)
        {
            if (effect?.effect_id == effectId)
                return effect;
        }
        return null;
    }

    private sealed class PlagueTongueFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private PlagueTongueFixture(
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

        internal static PlagueTongueFixture Build(
            GArray damageRolls,
            int? afterHitSaveRollOverride = null
        )
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
            BattleDamageResolver damageResolver = afterHitSaveRollOverride.HasValue
                ? new FixedAfterHitSaveRollDamageResolver(
                    damageRolls,
                    afterHitSaveRollOverride.Value
                )
                : new FixedRollDamageResolver(damageRolls);
            runtime.ConfigureDamageResolverForTests(damageResolver);
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new PlagueTongueFixture(
                itemRegistry,
                progressionRegistry,
                partyState,
                runtime
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildPlagueTongueUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                PlagueTongueItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    PlagueTongueItemId,
                    $"eq_plague_tongue_{label}"
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

    private sealed partial class FixedAfterHitSaveRollDamageResolver : FixedRollDamageResolver
    {
        private readonly int _afterHitSaveRollOverride;

        internal FixedAfterHitSaveRollDamageResolver(GArray damageRolls, int afterHitSaveRollOverride)
            : base(damageRolls)
        {
            _afterHitSaveRollOverride = Math.Clamp(afterHitSaveRollOverride, 1, 20);
        }

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState source_unit,
            BattleUnitState target_unit,
            IEnumerable<CombatEffectDefinition> effect_definitions,
            AttackCheckInput attack_check,
            AttackContext attack_context = null
        )
        {
            attack_context ??= new AttackContext();
            attack_context.AddSaveRollOverride(_afterHitSaveRollOverride);
            return base.ResolveAttackEffects(
                source_unit,
                target_unit,
                effect_definitions,
                attack_check,
                attack_context
            );
        }
    }
}

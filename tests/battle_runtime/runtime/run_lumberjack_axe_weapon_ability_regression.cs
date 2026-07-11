using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_lumberjack_axe_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_battleaxe_lumberjack_383";
    private static readonly StringName ChoppingRhythmTraitId =
        "weapon.axe.lumberjack.chopping_rhythm";
    private static readonly StringName PlantSlayerTraitId =
        "weapon.axe.lumberjack.plant_slayer";
    private static readonly StringName FellingMomentumTraitId =
        "weapon.axe.lumberjack.felling_momentum";
    private static readonly StringName ChoppingRhythmBindingId =
        "binding.weapon.axe.lumberjack.chopping_rhythm";
    private static readonly StringName PlantSlayerBindingId =
        "binding.weapon.axe.lumberjack.plant_slayer";
    private static readonly StringName FellingMomentumBindingId =
        "binding.weapon.axe.lumberjack.felling_momentum";
    private static readonly StringName ChopNotchStatusId = "lumberjack_chop_notch";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRealContentProjectsAndClearsOnUnequip();
            TestFirstHitMarksAndFollowUpGetsAccuracyAndDamage();
            TestPlantSlayerStacksWithFollowUpDamage();
            TestMissAndWrongSourceDoNotBorrowNotch();
            TestFirstHitKillDoesNotMarkOrRefundAp();
            TestRealMarkedKillCommandRefundsApButNotStamina();
            TestMarkedKillsRestoreApWithoutPerTurnLimitAndRespectCap();
            RequestTestExit(_test.Finish("Lumberjack axe weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Lumberjack axe weapon ability regression"));
        }
    }

    private void TestRealContentProjectsAndClearsOnUnequip()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(new GArray(), hitRoll: 10);
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含伐木工之斧。");
        _test.True(fixture.TraitDefs.ContainsKey(ChoppingRhythmTraitId), "真实 trait 内容应包含顺纹连斩。");
        _test.True(fixture.TraitDefs.ContainsKey(PlantSlayerTraitId), "真实 trait 内容应包含植物杀手。");
        _test.True(fixture.TraitDefs.ContainsKey(FellingMomentumTraitId), "真实 trait 内容应包含倒木回势。");
        _test.True(fixture.Bindings.ContainsKey(ChoppingRhythmBindingId), "真实装备能力内容应包含顺纹连斩 binding。");
        _test.True(fixture.Bindings.ContainsKey(PlantSlayerBindingId), "真实装备能力内容应包含植物杀手 binding。");
        _test.True(fixture.Bindings.ContainsKey(FellingMomentumBindingId), "真实装备能力内容应包含倒木回势 binding。");

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_lumberjack.tres"
        );
        _test.True(rawItem != null, "伐木工之斧原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "伐木工之斧", "显示名应匹配设计。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_battleaxe_base"), "应继承 battleaxe 模板。");
            _test.Eq(rawItem.base_price, 38000, "基础价格应为 38000。");
            _test.Eq(rawItem.buy_price, 38000, "购买价格应为 38000。");
            _test.Eq(rawItem.sell_price, 19000, "出售价格应为 19000。");
            _test.True(rawItem.trait_ids.Contains(ChoppingRhythmTraitId), "物品应声明顺纹连斩。");
            _test.True(rawItem.trait_ids.Contains(PlantSlayerTraitId), "物品应声明植物杀手。");
            _test.True(rawItem.trait_ids.Contains(FellingMomentumTraitId), "物品应声明倒木回势。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildLumberjackUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "装备后应保留真实 item id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("battleaxe"), "应投影为 battleaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "应投影为 axe family。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "应造成挥砍伤害。");
        _test.Eq(equipped.weapon_attack_range, 1, "攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "单手应为 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "双手应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "双手应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "双手应为 1D10+2。");
        AssertUnitHasTraitAndAbilitySource(equipped, ChoppingRhythmTraitId, ChoppingRhythmBindingId, "eq_lumberjack_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, PlantSlayerTraitId, PlantSlayerBindingId, "eq_lumberjack_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, FellingMomentumTraitId, FellingMomentumBindingId, "eq_lumberjack_projection");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "卸装后 weapon item id 应清空。");
        _test.Eq(equipped.weapon_profile_type_id, baseline.weapon_profile_type_id, "卸装后应恢复基础武器投影。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "卸装后装备能力源应清空。");
        _test.False(equipped.effective_trait_ids.Contains(ChoppingRhythmTraitId), "卸装后顺纹连斩不应残留。");
        _test.False(equipped.effective_trait_ids.Contains(PlantSlayerTraitId), "卸装后植物杀手不应残留。");
        _test.False(equipped.effective_trait_ids.Contains(FellingMomentumTraitId), "卸装后倒木回势不应残留。");
    }

    private void TestFirstHitMarksAndFollowUpGetsAccuracyAndDamage()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(new GArray { 4, 4, 3 }, hitRoll: 10);
        BattleUnitState holder = fixture.BuildLumberjackUnit("rhythm");
        BattleUnitState target = BuildEnemy("rhythm_target", new Vector2I(1, 0), hp: 100, "humanoid");

        int hpBefore = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "lumberjack_rhythm_first",
            previewCommand: false
        );
        _test.Eq(hpBefore - target.current_hp, 6, "第一击应只造成 1D8+2 基础伤害。");
        AssertNotch(target, holder.unit_id, 60, "第一击后应留下单层 60TU 劈痕。");

        BattleAttackRollModifierBundle markedBundle = BuildBasicAttackModifierBundle(
            fixture,
            holder,
            target,
            "lumberjack_marked_accuracy"
        );
        _test.Eq(markedBundle.GetEffectiveModifierDelta(), 1, "攻击自身劈痕目标应获得 +1 命中。");
        _test.True(HasModifier(markedBundle, ChoppingRhythmBindingId, 1), "命中明细应显示顺纹连斩 +1。");

        hpBefore = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "lumberjack_rhythm_follow_up",
            previewCommand: false
        );
        _test.Eq(hpBefore - target.current_hp, 9, "后续命中应造成 1D8+2 与额外 1D4。");
        AssertNotch(target, holder.unit_id, 60, "后续命中应刷新单层 60TU 劈痕。");

        holder.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(holder);
        BattleAttackRollModifierBundle unequippedBundle = BuildBasicAttackModifierBundle(
            fixture,
            holder,
            target,
            "lumberjack_unequipped_accuracy"
        );
        _test.Eq(unequippedBundle.GetEffectiveModifierDelta(), 0, "卸装后残留劈痕不能继续提供命中加值。");
    }

    private void TestPlantSlayerStacksWithFollowUpDamage()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(
            new GArray { 4, 3, 4, 4, 2, 3, 4 },
            hitRoll: 10
        );
        BattleUnitState holder = fixture.BuildLumberjackUnit("plant");
        BattleUnitState plant = BuildEnemy("plant_target", new Vector2I(1, 0), hp: 100, "plant");

        int hpBefore = plant.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            plant,
            "lumberjack_plant_first",
            previewCommand: false
        );
        _test.Eq(hpBefore - plant.current_hp, 13, "植物目标第一击应造成基础 6 与植物杀手 2D6=7。");

        hpBefore = plant.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            plant,
            "lumberjack_plant_follow_up",
            previewCommand: false
        );
        _test.Eq(hpBefore - plant.current_hp, 15, "植物后续击应同时结算基础 6、顺纹 1D4=2 与植物杀手 2D6=7。");
    }

    private void TestMissAndWrongSourceDoNotBorrowNotch()
    {
        using LumberjackFixture missFixture = LumberjackFixture.Build(new GArray(), hitRoll: 1);
        missFixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        BattleUnitState missHolder = missFixture.BuildLumberjackUnit("miss");
        BattleUnitState missTarget = BuildEnemy("miss_target", new Vector2I(1, 0), hp: 40, "humanoid");
        SetNotch(missTarget, missHolder.unit_id, durationTu: 20);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            missFixture.Runtime,
            missHolder,
            missTarget,
            "lumberjack_miss",
            previewCommand: false
        );
        _test.Eq(missTarget.current_hp, 40, "自然 1 未命中不应造成伤害。");
        AssertNotch(missTarget, missHolder.unit_id, 20, "未命中不应刷新已有劈痕。");

        using LumberjackFixture sourceFixture = LumberjackFixture.Build(new GArray { 4 }, hitRoll: 10);
        BattleUnitState holder = sourceFixture.BuildLumberjackUnit("takeover");
        BattleUnitState target = BuildEnemy("takeover_target", new Vector2I(1, 0), hp: 40, "humanoid");
        SetNotch(target, "other_holder", durationTu: 60);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState("lumberjack_wrong_source", holder, target);
        sourceFixture.Runtime.SetupStateForTests(state);
        BattleAttackRollModifierBundle wrongSourceBundle = BuildBasicAttackModifierBundle(
            sourceFixture,
            holder,
            target,
            "lumberjack_wrong_source_accuracy"
        );
        _test.Eq(wrongSourceBundle.GetEffectiveModifierDelta(), 0, "不能借用其他持有者留下的劈痕命中加值。");

        int hpBefore = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            sourceFixture.Runtime,
            holder,
            target,
            "lumberjack_takeover",
            previewCommand: false
        );
        _test.Eq(hpBefore - target.current_hp, 6, "接管来源的首次命中不应提前获得 1D4。");
        AssertNotch(target, holder.unit_id, 60, "存活目标的劈痕来源应由本次命中的持有者接管。");
    }

    private void TestFirstHitKillDoesNotMarkOrRefundAp()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(new GArray { 4 }, hitRoll: 10);
        BattleUnitState holder = fixture.BuildLumberjackUnit("first_kill");
        holder.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        holder.SetCurrentAp(2);
        BattleUnitState target = BuildEnemy("first_kill_target", new Vector2I(1, 0), hp: 6, "humanoid");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "lumberjack_first_hit_kill",
            previewCommand: false
        );
        _test.False(target.is_alive, "基础 6 点伤害应直接击杀 6 HP 目标。");
        _test.False(target.HasStatusEffect(ChopNotchStatusId), "第一击直接击杀不能给死者新留劈痕。");
        _test.Eq(holder.current_ap, 1, "第一击直接击杀不应返还已支付的 1 AP。");
    }

    private void TestMarkedKillsRestoreApWithoutPerTurnLimitAndRespectCap()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(new GArray(), hitRoll: 10);
        BattleUnitState holder = fixture.BuildLumberjackUnit("ap_chain");
        holder.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        holder.SetCurrentAp(0);
        holder.SetCurrentStamina(30);
        BattleUnitState anchor = BuildEnemy("ap_anchor", new Vector2I(1, 0), hp: 1, "humanoid");
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState("lumberjack_ap_chain", holder, anchor);
        fixture.Runtime.SetupStateForTests(state);
        StringName equipmentInstanceId = holder.GetEquipmentView().GetEquippedInstanceId("main_hand");
        BattleKillProvenance matchingProvenance = BattleKillProvenance.ForEquipmentAttack(
            equipmentInstanceId,
            "",
            "basic_attack"
        );

        BattleUnitState first = BuildDefeatedMarkedTarget("ap_first", holder.unit_id);
        ResolveOnKill(fixture, holder, first, state, matchingProvenance);
        _test.Eq(holder.current_ap, 1, "第一个合格击杀应恢复 1 AP。");

        BattleUnitState second = BuildDefeatedMarkedTarget("ap_second", holder.unit_id);
        ResolveOnKill(fixture, holder, second, state, matchingProvenance);
        _test.Eq(holder.current_ap, 2, "同回合第二个合格击杀仍应恢复 AP，不设每回合一次限制。");

        BattleUnitState third = BuildDefeatedMarkedTarget("ap_third", holder.unit_id);
        ResolveOnKill(fixture, holder, third, state, matchingProvenance);
        _test.Eq(holder.current_ap, 2, "达到正常 AP 上限后不能继续堆高。");
        _test.Eq(holder.current_stamina, 30, "倒木回势只恢复 AP，不恢复体力。");

        holder.SetCurrentAp(0);
        BattleUnitState wrongSource = BuildDefeatedMarkedTarget("ap_wrong_source", "other_holder");
        ResolveOnKill(fixture, holder, wrongSource, state, matchingProvenance);
        _test.Eq(holder.current_ap, 0, "其他持有者留下的劈痕不能触发 AP 恢复。");

        BattleUnitState wrongEquipment = BuildDefeatedMarkedTarget("ap_wrong_equipment", holder.unit_id);
        ResolveOnKill(
            fixture,
            holder,
            wrongEquipment,
            state,
            BattleKillProvenance.ForEquipmentAttack("other_equipment", "", "basic_attack")
        );
        _test.Eq(holder.current_ap, 0, "其他装备实例造成的击杀不能触发 AP 恢复。");

        BattleUnitState nonAttack = BuildDefeatedMarkedTarget("ap_non_attack", holder.unit_id);
        ResolveOnKill(fixture, holder, nonAttack, state, BattleKillProvenance.None);
        _test.Eq(holder.current_ap, 0, "非攻击击杀不能触发 AP 恢复。");
    }

    private void TestRealMarkedKillCommandRefundsApButNotStamina()
    {
        using LumberjackFixture fixture = LumberjackFixture.Build(new GArray { 4, 1 }, hitRoll: 10);
        BattleUnitState holder = fixture.BuildLumberjackUnit("real_kill");
        holder.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        holder.SetCurrentAp(2);
        holder.SetCurrentStamina(30);
        BattleUnitState target = BuildEnemy("real_kill_target", new Vector2I(1, 0), hp: 7, "humanoid");
        SetNotch(target, holder.unit_id, durationTu: 60);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "lumberjack_real_marked_kill",
            previewCommand: false
        );

        _test.False(target.is_alive, "已有自身劈痕的 7 HP 目标应被基础 6 与顺纹 1D4=1 击杀。");
        _test.Eq(holder.current_ap, 2, "真实命令支付 1 AP 后，合格击杀应通过 on-kill 分发恢复 1 AP。");
        _test.Eq(holder.current_stamina, 22, "真实命令应保留基础攻击的 8 点体力消耗。");
    }

    private static void ResolveOnKill(
        LumberjackFixture fixture,
        BattleUnitState holder,
        BattleUnitState target,
        BattleState state,
        BattleKillProvenance provenance
    )
    {
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = holder,
                DefeatedUnit = target,
                BattleState = state,
                Batch = new BattleEventBatch(),
                KillProvenance = provenance,
            }
        );
    }

    private static BattleAttackRollModifierBundle BuildBasicAttackModifierBundle(
        LumberjackFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName contextId
    )
    {
        BattleState state = fixture.Runtime.GetState();
        SkillDefinition basicAttack = fixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId];
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        return attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                basicAttack,
                "skill_attack_check",
                contextId,
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
        foreach (BattleAttackRollModifierSpec modifier in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (modifier?.source_id == sourceId && modifier.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private void AssertNotch(
        BattleUnitState target,
        StringName expectedSource,
        int expectedDuration,
        string message
    )
    {
        BattleStatusEffectState notch = target?.GetStatusEffect(ChopNotchStatusId);
        _test.True(notch != null, message);
        if (notch == null)
            return;
        _test.Eq(notch.stacks, 1, $"{message} 层数应固定为 1。");
        _test.Eq(notch.stack_limit, 1, $"{message} 层数上限应为 1。");
        _test.Eq(notch.duration, expectedDuration, $"{message} 持续时间不符。");
        _test.Eq(notch.source_unit_id, expectedSource, $"{message} 来源不符。");
    }

    private static void SetNotch(BattleUnitState target, StringName sourceUnitId, int durationTu)
    {
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ChopNotchStatusId,
                display_label = "劈痕",
                source_unit_id = sourceUnitId,
                power = 1,
                stacks = 1,
                duration = durationTu,
                stack_behavior = "refresh",
                stack_limit = 1,
                counts_as_debuff_override = true,
                counts_as_debuff = true,
                undispellable = true,
            }
        );
    }

    private static BattleUnitState BuildDefeatedMarkedTarget(StringName unitId, StringName sourceUnitId)
    {
        BattleUnitState target = BuildEnemy(unitId, new Vector2I(1, 0), hp: 1, "humanoid");
        SetNotch(target, sourceUnitId, durationTu: 60);
        target.MarkDead();
        return target;
    }

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        Vector2I coord,
        int hp,
        StringName creatureType
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = hp,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        unit.creature_type_tags.Add(creatureType);
        return unit;
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

    private sealed class LumberjackFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private LumberjackFixture(
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

        internal static LumberjackFixture Build(GArray damageRolls, int hitRoll)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(hitRoll));
            return new LumberjackFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildLumberjackUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_lumberjack_{label}")
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
}

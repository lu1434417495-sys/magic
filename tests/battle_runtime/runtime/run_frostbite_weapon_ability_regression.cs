using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_frostbite_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName FrostbiteItemId = "weapon_unique_axe_frostbite_097";
    private static readonly StringName FrostTouchTraitId =
        "weapon.axe.frostbite.frost_touch";
    private static readonly StringName IceboundPathTraitId =
        "weapon.axe.frostbite.icebound_path";
    private static readonly StringName PolarAdaptationTraitId =
        "weapon.axe.frostbite.polar_adaptation";
    private static readonly StringName FrostTouchBindingId =
        "binding.weapon.axe.frostbite.frost_touch";
    private static readonly StringName IceboundPathBindingId =
        "binding.weapon.axe.frostbite.icebound_path";
    private static readonly StringName IceboundPathSkillId =
        "weapon_axe_frostbite_icebound_path";
    private static readonly StringName IceboundPathGrantId =
        "grant.frostbite.icebound_path.skill";
    private static readonly StringName ChillCountStatusId = "frostbite_chill_count";
    private static readonly StringName SlowStatusId = "slow";
    private static readonly StringName FreezeDamageTag = "freeze";

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
            TestFrostbiteProjectsRealContentAndClearsOnUnequip();
            TestFrostTouchAddsColdDamageAndThirdSameTargetHitSlows();
            TestIceboundPathFreezesAdjacentWaterAndConsumesStaminaCooldown();
            RequestTestExit(_test.Finish("Frostbite weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Frostbite weapon ability regression"));
        }
    }

    private void TestFrostbiteProjectsRealContentAndClearsOnUnequip()
    {
        using FrostbiteFixture fixture = FrostbiteFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(FrostbiteItemId), "真实物品内容应包含霜咬。");
        _test.True(fixture.TraitDefs.ContainsKey(FrostTouchTraitId), "真实 trait 内容应包含霜冻之触。");
        _test.True(fixture.TraitDefs.ContainsKey(IceboundPathTraitId), "真实 trait 内容应包含冰封之路。");
        _test.True(
            fixture.TraitDefs.ContainsKey(PolarAdaptationTraitId),
            "真实 trait 内容应包含极地适应。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(FrostTouchBindingId),
            "真实装备能力内容应包含霜冻之触 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(IceboundPathBindingId),
            "真实装备能力内容应包含冰封之路 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(IceboundPathSkillId),
            "冰封之路应落成真实 SkillDef，而不是 trait 文本。"
        );
        _test.Eq(
            BattleTerrainRules.NormalizeTerrainId("ice"),
            new StringName("ice"),
            "冰封之路需要通用 ice 地形 id。"
        );
        _test.Eq(BattleTerrainRules.GetDisplayName("ice"), "冰层", "ice 地形应显示为冰层。");

        using TestContentResourceLoader contentLoader = new();
        ItemDef rawItem = contentLoader.LoadCanonical<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_frostbite.tres"
        );
        _test.True(rawItem != null, "霜咬原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "霜咬", "霜咬显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_battleaxe_base"),
                "霜咬应继承 battleaxe 模板。"
            );
            _test.Eq(rawItem.base_price, 40000, "霜咬基础价格应为 40000。");
            _test.True(rawItem.trait_ids.Contains(FrostTouchTraitId), "霜咬物品应声明霜冻之触。");
            _test.True(rawItem.trait_ids.Contains(IceboundPathTraitId), "霜咬物品应声明冰封之路。");
            _test.True(rawItem.trait_ids.Contains(PolarAdaptationTraitId), "霜咬物品应声明极地适应。");
            _test.False(
                ContainsText(rawItem.description, "温暖") || ContainsText(rawItem.description, "-2"),
                "玩家说明不应包含已否掉的温暖环境攻击惩罚。"
            );
        }

        if (fixture.SkillDefs.TryGetValue(IceboundPathSkillId, out SkillDefinition icebound))
        {
            AssertIceboundPathSkillDefinition(icebound, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleUnitState equipped = fixture.BuildFrostbiteUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, FrostbiteItemId, "霜咬装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("battleaxe"), "霜咬应投影为 battleaxe。");
        _test.Eq(equippedWeapon.Family, new StringName("axe"), "霜咬应投影为 axe family。");
        _test.Eq(equippedWeapon.PhysicalDamageTag, new StringName("physical_slash"), "霜咬应为挥砍伤害。");
        _test.Eq(equippedWeapon.AttackRange, 1, "霜咬攻击距离应为 1。");
        _test.True(equippedWeapon.IsVersatile, "霜咬应保留 versatile 投影。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "霜咬单手应为 1D8+1。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 8, "霜咬单手应为 1D8+1。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 1, "霜咬单手应为 1D8+1。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceCount, 1, "霜咬双手应为 1D10+1。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceSides, 10, "霜咬双手应为 1D10+1。");
        _test.Eq(equippedWeapon.TwoHandedDice.FlatBonus, 1, "霜咬双手应为 1D10+1。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FrostTouchTraitId,
            FrostTouchBindingId,
            "eq_frostbite_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            IceboundPathTraitId,
            IceboundPathBindingId,
            "eq_frostbite_projection"
        );
        _test.True(
            equipped.HasEffectiveTrait(PolarAdaptationTraitId),
            "装备霜咬应投影极地适应 trait。"
        );
        _test.Eq(
            GetDamageMitigation(equipped, FreezeDamageTag),
            new StringName("immune"),
            "极地适应应通过 trait 被动投影寒冷免疫。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        equippedWeapon = equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, new StringName(""), "移除霜咬后 weapon_item_id 应清空。");
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除霜咬后武器 profile 应回到装备前状态。"
        );
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除霜咬后装备能力源应清空。"
        );
        _test.False(equipped.HasEffectiveTrait(FrostTouchTraitId), "移除霜咬后霜冻之触不应残留。");
        _test.False(equipped.HasEffectiveTrait(IceboundPathTraitId), "移除霜咬后冰封之路不应残留。");
        _test.False(equipped.HasEffectiveTrait(PolarAdaptationTraitId), "移除霜咬后极地适应不应残留。");
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除霜咬后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestFrostTouchAddsColdDamageAndThirdSameTargetHitSlows()
    {
        using FrostbiteFixture fixture = FrostbiteFixture.Build(new GArray { 4, 3, 4, 3, 4, 3, 4, 3 });
        BattleUnitState attacker = fixture.BuildFrostbiteUnit("frost_touch");
        BattleUnitState target = BuildEnemy("frost_touch_target", new Vector2I(1, 0), hp: 120);

        int previousHp = target.GetCurrentHp();
        for (int hit = 1; hit <= 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"frostbite_same_target_hit_{hit}",
                previewCommand: false
            );
            int damage = previousHp - target.GetCurrentHp();
            previousHp = target.GetCurrentHp();
            _test.Eq(damage, 8, $"第 {hit} 次命中应造成武器挥砍与 1D6 寒冷伤害。");
            BattleStatusEffectState chill = target.GetStatusEffect(ChillCountStatusId);
            _test.True(chill != null, $"第 {hit} 次命中应记录寒霜命中计数。");
            if (chill != null)
            {
                _test.Eq(chill.stacks, hit, $"第 {hit} 次命中同一目标后计数应为 {hit}。");
                _test.Eq(chill.duration, 60, "霜咬寒霜计数应持续 60TU 并随命中刷新。");
                _test.Eq(chill.source_unit_id, attacker.unit_id, "霜咬寒霜计数应记录持有者来源。");
            }
            if (hit < 3)
            {
                _test.False(target.HasStatusEffect(SlowStatusId), $"第 {hit} 次命中不应施加 slow。");
            }
        }

        BattleStatusEffectState slow = target.GetStatusEffect(SlowStatusId);
        _test.True(slow != null, "连续三次命中同一目标后应施加 slow。");
        if (slow != null)
        {
            _test.Eq(slow.duration, 60, "霜咬 slow 应持续 60TU。");
            _test.Eq(slow.source_unit_id, attacker.unit_id, "霜咬 slow 应记录持有者来源。");
            _test.Eq(slow.move_point_capacity_delta, -1, "霜咬 slow 应明确将移动力上限降低 1。");
            _test.Eq(target.GetMovePointCapacity(), 1, "普通 2 点移动力目标被霜咬 slow 后上限应为 1。");
        }

        BattleUnitState otherTarget = BuildEnemy("frost_touch_other_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            otherTarget,
            "frostbite_other_target_first_hit",
            previewCommand: false
        );
        _test.Eq(
            otherTarget.GetStatusEffect(ChillCountStatusId)?.stacks ?? 0,
            1,
            "换目标后的第一次命中应只给新目标 1 层寒霜计数。"
        );
        _test.False(otherTarget.HasStatusEffect(SlowStatusId), "新目标第一次命中不应继承旧目标 slow。");
    }

    private void TestIceboundPathFreezesAdjacentWaterAndConsumesStaminaCooldown()
    {
        using FrostbiteFixture fixture = FrostbiteFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildFrostbiteUnit("icebound_path");
        holder.SetCombatResources(80, 0, 100, 0, 0, 2);
        holder.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        BattleUnitState enemy = BuildEnemy("icebound_dummy_enemy", new Vector2I(4, 4), hp: 30);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "frostbite_icebound_path",
            holder,
            enemy,
            mapSize: new Vector2I(5, 5)
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleGridService grid = fixture.Runtime.GetGridService();
        Vector2I adjacentWater = new(1, 0);
        Vector2I adjacentLand = new(0, 1);
        Vector2I farWater = new(2, 0);
        grid.SetBaseTerrain(state, adjacentWater, "water");
        grid.SetBaseTerrain(state, farWater, "water");

        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, IceboundPathSkillId, out BattleAvailableSkillEntry entry),
            "装备霜咬后，unit 的可用技能应包含冰封之路。"
        );
        if (entry == null)
            return;
        _test.True(entry.IsSelectable, "体力充足且未冷却时冰封之路应可选。");
        _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "冰封之路来源应是 equipment_skill。");
        _test.Eq(entry.EquipmentBindingId, IceboundPathBindingId, "冰封之路入口应携带 binding id。");
        _test.Eq(entry.EquipmentGrantedActionId, IceboundPathGrantId, "冰封之路入口应携带 grant id。");

        SkillDefinition skill = fixture.SkillDefs[IceboundPathSkillId];
        CombatCastVariantDefinition variant =
            fixture.Runtime._skill_resolution_rules.ResolveGroundCastVariantDefinition(
                skill,
                holder,
                ""
            );
        BattleGroundSkillValidationResult landValidation =
            fixture.Runtime.ValidateGroundSkillCommandResultTyped(
                holder,
                skill,
                variant,
                BuildGroundSkillCommand(holder, entry, adjacentLand)
            );
        _test.False(landValidation.Allowed, "冰封之路不应允许选择非水域地格。");
        BattleGroundSkillValidationResult farValidation =
            fixture.Runtime.ValidateGroundSkillCommandResultTyped(
                holder,
                skill,
                variant,
                BuildGroundSkillCommand(holder, entry, farWater)
            );
        _test.False(farValidation.Allowed, "冰封之路不应允许选择不相邻水域。");

        BattleCommand command = BuildGroundSkillCommand(holder, entry, adjacentWater);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"冰封之路选择相邻水域时 preview 应允许。logs={JoinLogs(preview?.LogLinesTyped)}"
        );
        int staminaBefore = holder.GetCurrentStamina();
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "冰封之路 IssueCommand 应返回事件 batch。");
        _test.Eq(
            grid.GetCellBaseTerrainId(state, adjacentWater),
            new StringName("ice"),
            "冰封之路应将相邻 1 格水域改成冰层。"
        );
        _test.Eq(holder.GetCurrentStamina(), staminaBefore - 60, "冰封之路应消耗 60 体力。");
        _test.Eq(holder.GetCooldownTyped(IceboundPathSkillId), 120, "冰封之路应设置 120TU 冷却表示一回合一次。");

        BattlePreview sameTurnPreview = fixture.Runtime.PreviewCommand(command);
        _test.False(sameTurnPreview?.allowed == true, "同一回合冷却中不应再次使用冰封之路。");
    }

    private void AssertIceboundPathSkillDefinition(
        SkillDefinition skill,
        FrostbiteFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "冰封之路技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "冰封之路应选择地面格。");
        _test.Eq(combat.RangeValue, 1, "冰封之路只能选择相邻 1 格。");
        _test.Eq(combat.ApCost, 0, "冰封之路不应额外消耗 AP，只消耗体力。");
        _test.Eq(combat.StaminaCost, 60, "冰封之路体力消耗应为 60。");
        _test.Eq(combat.CooldownTu, 120, "冰封之路应使用 120TU 冷却表达一回合一次。");
        _test.Eq(combat.CastVariants.Count, 1, "冰封之路应只有一个单格水域形态。");
        if (combat.CastVariants.Count == 0)
            return;
        CombatCastVariantDefinition variant = combat.CastVariants[0];
        _test.Eq(variant.FootprintPattern, new StringName("single"), "冰封之路应只选 1 格。");
        _test.Eq(variant.RequiredCoordCount, 1, "冰封之路应只选 1 格。");
        _test.True(ContainsStringName(variant.AllowedBaseTerrains, "water"), "冰封之路应允许 water。");
        _test.True(ContainsStringName(variant.AllowedBaseTerrains, "shallow_water"), "冰封之路应允许 shallow_water。");
        _test.True(ContainsStringName(variant.AllowedBaseTerrains, "flowing_water"), "冰封之路应允许 flowing_water。");
        _test.True(ContainsStringName(variant.AllowedBaseTerrains, "deep_water"), "冰封之路应允许 deep_water。");
        _test.Eq(variant.EffectDefinitions.Count, 1, "冰封之路应有一个地形替换 effect。");
        if (variant.EffectDefinitions.Count > 0)
        {
            CombatEffectDefinition effect = variant.EffectDefinitions[0];
            _test.Eq(effect.EffectType, new StringName("terrain_replace"), "冰封之路 effect 应是 terrain_replace。");
            _test.Eq(effect.TerrainReplaceTo, new StringName("ice"), "冰封之路应替换为 ice。");
        }
        _test.True(
            fixture.Bindings.TryGetValue(IceboundPathBindingId, out EquipmentAbilityBindingDefinition binding),
            "冰封之路 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "冰封之路 binding 应授予一个装备技能入口。");
            if (binding.GrantedActions.Count > 0)
            {
                EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
                _test.Eq(grant.SkillId, IceboundPathSkillId, "冰封之路 grant 应指向真实 SkillDef。");
                _test.Eq(grant.GrantedActionId, IceboundPathGrantId, "冰封之路 grant id 应稳定。");
                _test.Eq(grant.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.None, "冰封之路使用次数由技能冷却承担。");
            }
        }
    }

    private static BattleCommand BuildGroundSkillCommand(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry,
        Vector2I coord
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unit?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = entry?.EntryRef.SkillId ?? new StringName(""),
            target_coord = coord,
        };

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        FrostbiteFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
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

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: hp,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        unit.AddCreatureTypeTagTyped("humanoid");
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
        if (!unit.HasEffectiveTrait(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
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

    private static BattleEquipmentAbilitySourceReadView FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source
            in unit?.GetEquipmentAbilitySourcesReadViewTyped()
                ?? new BattleEquipmentAbilitySourceListReadView(
                    null
                )
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static StringName GetDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit == null)
            return "";
        if (unit.TryGetDamageResistanceTyped(damageTag, out StringName value))
            return ProgressionDataUtils.to_string_name(value);
        return "";
    }

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
        && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class FrostbiteFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private FrostbiteFixture(
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
            SkillDefs = snapshot.Skills;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static FrostbiteFixture Build(GArray damageRolls)
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
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

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                snapshot.Skills,
                item_defs: snapshot.Items,
                trait_defs: snapshot.Traits,
                equipment_ability_bindings: snapshot.EquipmentAbilityBindings
            );
            BattleTestFixture.ConfigureDamageResolverForTests(
                runtime,
                new FixedRollDamageResolver(damageRolls)
            );
            BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
            return new FrostbiteFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildFrostbiteUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                FrostbiteItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(FrostbiteItemId, $"eq_frostbite_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
            _characterManagement?.Dispose();
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

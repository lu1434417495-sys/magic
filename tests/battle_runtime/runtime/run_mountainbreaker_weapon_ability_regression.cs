using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_mountainbreaker_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_greataxe_mountainbreaker";
    private static readonly StringName BladeTrait = "weapon.axe.mountainbreaker.mountain_splitting_blade";
    private static readonly StringName GripTrait = "weapon.axe.mountainbreaker.titan_grip";
    private static readonly StringName CollapseTrait = "weapon.axe.mountainbreaker.mountain_collapse";
    private static readonly StringName FollowupTrait = "weapon.axe.mountainbreaker.faultline_followup";
    private static readonly StringName AnchorTrait = "weapon.axe.mountainbreaker.leyline_anchor";
    private static readonly StringName CollapseBinding = "binding.weapon.axe.mountainbreaker.mountain_collapse";
    private static readonly StringName FollowupBinding = "binding.weapon.axe.mountainbreaker.faultline_followup";
    private static readonly StringName AnchorBinding = "binding.weapon.axe.mountainbreaker.leyline_anchor";
    private static readonly StringName BrokenGroundEffectId = "mountainbreaker_broken_ground";
    private static readonly StringName Strength = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength);
    private static readonly StringName StrengthModifier = AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier);
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
            TestContentProjectionAndStrengthRequirement();
            TestCollapseFollowupAndLeylineAnchor();
            TestLeylineAnchorTriggersFromRealWeaponDamage();
            RequestTestExit(_test.Finish("Mountainbreaker weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Mountainbreaker weapon ability regression"));
        }
    }

    private void TestContentProjectionAndStrengthRequirement()
    {
        using Fixture fixture = Fixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含裂山者。");
        foreach (StringName traitId in new[] { BladeTrait, GripTrait, CollapseTrait, FollowupTrait, AnchorTrait })
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"裂山者应包含 trait {traitId}。");
        foreach (StringName bindingId in new[] { CollapseBinding, FollowupBinding, AnchorBinding })
            _test.True(fixture.Bindings.ContainsKey(bindingId), $"裂山者应包含 binding {bindingId}。");

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_mountainbreaker.tres"
        );
        _test.True(rawItem != null, "裂山者原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "裂山者 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "裂山者", "裂山者应使用新名称。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_greataxe_base"), "裂山者应继承 greataxe。");
            _test.Eq(rawItem.base_price, 65000, "裂山者价格应为 65000。");
            _test.Eq(rawItem.trait_ids.Count, 5, "裂山者应有且只有 5 个特性。");
            foreach (StringName traitId in new[] { BladeTrait, GripTrait, CollapseTrait, FollowupTrait, AnchorTrait })
                _test.True(rawItem.trait_ids.Contains(traitId), $"裂山者 item 应声明 {traitId}。");

            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "裂山者应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.family, new StringName("axe"), "裂山者 family 应为 axe。");
                _test.Eq(profile.range_type, new StringName("melee"), "裂山者应为 melee。");
                _test.Eq(profile.damage_tag, new StringName("physical_slash"), "裂山者应为斩击。");
                _test.Eq(profile.attack_range, 2, "裂山者应保留 reach 攻击距离 2。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 2, "裂山者应为 2D8+3。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 8, "裂山者应为 2D8+3。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 3, "裂山者应为 2D8+3。");
                _test.True(Contains(profile.GetPropertiesTyped(), "two_handed"), "裂山者应声明 two_handed。");
                _test.True(Contains(profile.GetPropertiesTyped(), "heavy"), "裂山者应声明 heavy。");
                _test.True(Contains(profile.GetPropertiesTyped(), "reach"), "裂山者应声明 reach。");
            }

            EquipmentRequirement requirement = rawItem.equip_requirement as EquipmentRequirement;
            _test.True(HasAttributeRequirement(requirement, Strength, 20), "泰坦之握应要求 strength >= 20。");
        }

        AssertStrengthRequirement(fixture.ItemDefs, 19, false);
        AssertStrengthRequirement(fixture.ItemDefs, 20, true);

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon(20);
        BattleUnitState equipped = fixture.BuildMountainbreakerUnit(20, "projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "裂山者装备后 unit 应保留 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("greataxe"), "裂山者应投影为 greataxe。");
        _test.Eq(equipped.weapon_attack_range, 2, "裂山者投影攻击距离应为 2。");
        _test.True(equipped.weapon_uses_two_hands, "裂山者应占用双手。");
        foreach (StringName traitId in new[] { BladeTrait, GripTrait, CollapseTrait, FollowupTrait, AnchorTrait })
            _test.True(equipped.effective_trait_ids.Contains(traitId), $"装备后应投影 {traitId}。");
        AssertAbilitySource(equipped, CollapseTrait, CollapseBinding);
        AssertAbilitySource(equipped, FollowupTrait, FollowupBinding);
        AssertAbilitySource(equipped, AnchorTrait, AnchorBinding);

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除裂山者后 weapon_item_id 应清空。");
        _test.Eq(equipped.weapon_profile_type_id, baseline.weapon_profile_type_id, "移除后 profile 应恢复。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除后装备能力源应清空。");
    }

    private void TestCollapseFollowupAndLeylineAnchor()
    {
        using Fixture fixture = Fixture.Build();
        BattleUnitState attacker = fixture.BuildMountainbreakerUnit(20, "runtime");
        BattleUnitState target = BuildTarget("target", new Vector2I(1, 0));

        BattleEquipmentAbilityAfterHitResult first = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "mountainbreaker_first",
            weaponHpDamage: 1,
            strengthModifier: 3,
            abilityCheckRoll: 19,
            saveRollOverride: 1
        );
        BattleStatusEffectState prone = target.GetStatusEffect("prone");
        _test.True(prone != null, "山崩击真实武器 HP 伤害后 STR DC16 失败应倒地。");
        _test.Eq(prone?.duration ?? -1, 50, "山崩击 prone 必须持续 50TU。");
        _test.False(first.HasBonusDamageDice(FollowupBinding, 1, 8), "目标刚被山崩击击倒时，断层追击不应提前触发。");
        _test.True(first.HasRoll(AnchorBinding, 22, false), "地脉定锚 19+3=22 不高于22，应失败。");
        _test.Eq(CountBrokenGround(fixture.Runtime.GetState(), target), 0, "失败不应破坏地形。");

        BattleEquipmentAbilityAfterHitResult second = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "mountainbreaker_second",
            weaponHpDamage: 1,
            strengthModifier: 4,
            abilityCheckRoll: 19,
            saveRollOverride: 20
        );
        _test.True(second.HasBonusDamageDice(FollowupBinding, 1, 8), "攻击已倒地目标应追加 1D8 physical_blunt。");
        _test.True(second.HasRoll(AnchorBinding, 23, true), "地脉定锚 19+4=23 高于22，应成功。");
        _test.Eq(CountBrokenGround(fixture.Runtime.GetState(), target), 1, "成功应破坏目标 anchor 格一次。");
        BattleTerrainEffectState effect = GetBrokenGround(fixture.Runtime.GetState(), target);
        _test.Eq(effect?.lifetime_policy ?? new StringName(""), new StringName("battle"), "破碎地形应为 battle lifetime。");
        _test.Eq(effect?.remaining_tu ?? -1, 0, "破碎地形没有持续时间 TU。");
        _test.Eq(effect?.move_cost_delta ?? 0, 1, "破碎地形移动消耗应 +1。");
        _test.Eq(fixture.Runtime._terrain_effect_system.GetMoveCostDeltaForUnitTarget(target, target.coord), 1, "移动成本 delta 应生效。");

        ResolveAfterHit(
            fixture,
            attacker,
            target,
            "mountainbreaker_same_cell",
            weaponHpDamage: 1,
            strengthModifier: 20,
            abilityCheckRoll: 20,
            saveRollOverride: 20
        );
        _test.Eq(CountBrokenGround(fixture.Runtime.GetState(), target), 1, "同一格最多破坏一次。");
        _test.Eq(fixture.Runtime._terrain_effect_system.GetMoveCostDeltaForUnitTarget(target, target.coord), 1, "重复成功不应继续提高移动消耗。");

        fixture.Runtime.GetState().timeline.current_tu = 1000;
        fixture.Runtime._terrain_effect_system.ProcessTimedTerrainEffects(new BattleEventBatch());
        _test.Eq(CountBrokenGround(fixture.Runtime.GetState(), target), 1, "推进 1000TU 后 battle lifetime 地形仍保留。");

        BattleUnitState nat20Target = BuildTarget("nat20_target", new Vector2I(2, 0));
        fixture.Runtime.GetState().SetUnit(nat20Target);
        SetUnitOccupants(fixture.Runtime.GetState(), nat20Target);
        ResolveAfterHitInCurrentState(
            fixture,
            attacker,
            nat20Target,
            weaponHpDamage: 1,
            strengthModifier: -10,
            abilityCheckRoll: 20,
            saveRollOverride: 20
        );
        _test.Eq(CountBrokenGround(fixture.Runtime.GetState(), nat20Target), 1, "自然 20 应自动成功。");
    }

    private void TestLeylineAnchorTriggersFromRealWeaponDamage()
    {
        using Fixture fixture = Fixture.Build(new GArray { 4, 4 });
        BattleUnitState attacker = fixture.BuildMountainbreakerUnit(20, "real_attack");
        BattleUnitState target = BuildTarget("real_attack_target", new Vector2I(1, 0));
        attacker.attribute_snapshot.SetValue(StrengthModifier, 4);
        fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ConfigureAbilityCheckRollValuesForTests(new[] { 19 });

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "mountainbreaker_real_attack",
            previewCommand: false
        );

        _test.True(target.current_hp < 100, "真实 basic_attack 应先造成武器 HP 伤害。");
        _test.Eq(
            CountBrokenGround(fixture.Runtime.GetState(), target),
            1,
            "地脉定锚应通过真实 BattleDamageResolver after-hit 链获得 weapon HP damage 并破坏地形。"
        );
        BattleTerrainEffectState effect = GetBrokenGround(fixture.Runtime.GetState(), target);
        _test.Eq(effect?.lifetime_policy ?? new StringName(""), new StringName("battle"), "真实攻击生成的破碎地形应为 battle lifetime。");
        _test.Eq(effect?.remaining_tu ?? -1, 0, "真实攻击生成的破碎地形没有 TU 持续时间。");
        _test.Eq(effect?.move_cost_delta ?? 0, 1, "真实攻击生成的破碎地形移动消耗应 +1。");
    }

    private static BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        Fixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int weaponHpDamage,
        int strengthModifier,
        int abilityCheckRoll,
        int saveRollOverride
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target,
            mapSize: new Vector2I(5, 2)
        );
        fixture.Runtime.SetupStateForTests(state);
        return ResolveAfterHitInCurrentState(
            fixture,
            attacker,
            target,
            weaponHpDamage,
            strengthModifier,
            abilityCheckRoll,
            saveRollOverride
        );
    }

    private static BattleEquipmentAbilityAfterHitResult ResolveAfterHitInCurrentState(
        Fixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        int weaponHpDamage,
        int strengthModifier,
        int abilityCheckRoll,
        int saveRollOverride
    )
    {
        attacker.attribute_snapshot.SetValue(StrengthModifier, strengthModifier);
        target.attribute_snapshot.SetValue(Strength, 10);
        target.attribute_snapshot.SetValue(StrengthModifier, 0);
        BattleEquipmentAbilityRuntimeService service = fixture.Runtime.GetEquipmentAbilityRuntimeService();
        service.ConfigureAbilityCheckRollValuesForTests(new[] { abilityCheckRoll });
        return service.ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = fixture.Runtime.GetState(),
                AttackSucceeded = true,
                WeaponHpDamage = weaponHpDamage,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static bool HasAttributeRequirement(EquipmentRequirement requirement, StringName attributeId, int minValue)
    {
        if (requirement == null)
            return false;
        foreach (EquipmentAttributeRequirementDef entry in requirement.attribute_requirements)
            if (entry?.attribute_id == attributeId && entry.min_value == minValue)
                return true;
        return false;
    }

    private static void AssertStrengthRequirement(IReadOnlyDictionary<StringName, ItemDefinition> itemDefs, int strength, bool shouldSucceed)
    {
        PartyState party = BuildPartyState($"hero_{strength}", strength);
        PartyWarehouseService warehouse = new();
        warehouse.Setup(party, itemDefs);
        PartyEquipmentService equipment = new();
        equipment.Setup(party, itemDefs, warehouse);
        warehouse.AddItemTyped(ItemId, 1);
        PartyEquipmentService.EquipmentActionResult result = equipment.EquipItemTyped($"hero_{strength}", ItemId);
        if (shouldSucceed)
            _ = result.Success ? true : throw new InvalidOperationException($"strength {strength} should equip裂山者: {result.ErrorCode}");
        else if (result.Success || result.ErrorCode != "attribute_too_low")
            throw new InvalidOperationException($"strength {strength} should fail attribute_too_low, actual success={result.Success} error={result.ErrorCode}");
    }

    private static void AssertAbilitySource(BattleUnitState unit, StringName traitId, StringName bindingId)
    {
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"missing trait {traitId}");
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return;
        throw new InvalidOperationException($"missing ability source {bindingId}");
    }

    private static bool Contains(IEnumerable<StringName> values, StringName needle)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == needle)
                return true;
        return false;
    }

    private static int CountBrokenGround(BattleState state, BattleUnitState unit)
    {
        int count = 0;
        BattleCellState cell = GetCell(state, unit);
        foreach (BattleTerrainEffectState effect in cell?.timed_terrain_effects ?? new List<BattleTerrainEffectState>())
            if (effect?.effect_id == BrokenGroundEffectId)
                count++;
        return count;
    }

    private static BattleTerrainEffectState GetBrokenGround(BattleState state, BattleUnitState unit)
    {
        BattleCellState cell = GetCell(state, unit);
        foreach (BattleTerrainEffectState effect in cell?.timed_terrain_effects ?? new List<BattleTerrainEffectState>())
            if (effect?.effect_id == BrokenGroundEffectId)
                return effect;
        return null;
    }

    private static BattleCellState GetCell(BattleState state, BattleUnitState unit) =>
        state != null && unit != null && state.ContainsCell(unit.coord) ? state.GetCell(unit.coord) : null;

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            coord = coord,
            is_alive = true,
            current_hp = 100,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(Strength, 10);
        unit.attribute_snapshot.SetValue(StrengthModifier, 0);
        unit.RefreshFootprint();
        return unit;
    }

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
    }

    private static PartyState BuildPartyState(StringName memberId, int strength)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress(),
            equipment_state = new EquipmentState(),
            current_hp = 50,
        };
        member.progression.unit_id = memberId;
        member.progression.display_name = member.display_name;
        member.progression.unit_base_attributes = BuildAttributes(strength);
        party.SetMemberState(member);
        party.active_member_ids = new GStringNameArray { memberId };
        party.leader_member_id = memberId;
        party.main_character_member_id = memberId;
        return party;
    }

    private static UnitBaseAttributes BuildAttributes(int strength)
    {
        UnitBaseAttributes attributes = new();
        attributes.SetAttributeValue(Strength, strength);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower), 10);
        attributes.custom_stats[PartyWarehouseService.StorageSpaceAttributeId] = 20;
        return attributes;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterManagementModule _management;
        private readonly PartyState _party;
        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        private Fixture(
            CharacterManagementModule management,
            PartyState party,
            BattleRuntimeModule runtime,
            ContentSnapshot snapshot
        )
        {
            _management = management;
            _party = party;
            Runtime = runtime;
            ItemDefs = snapshot.Items;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal static Fixture Build(GArray damageRolls = null)
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState party = BuildPartyState("hero", 20);
            CharacterManagementModule management = new();
            management.setup(
                party,
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
                management,
                snapshot.Skills,
                item_defs: snapshot.Items,
                trait_defs: snapshot.Traits,
                equipment_ability_bindings: snapshot.EquipmentAbilityBindings
            );
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls ?? new GArray { 4, 4, 4 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new Fixture(management, party, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(int strength)
        {
            PartyMemberState member = _party.GetMemberState("hero");
            member.progression.unit_base_attributes = BuildAttributes(strength);
            member.equipment_state = new EquipmentState();
            return BuildUnit(strength);
        }

        internal BattleUnitState BuildMountainbreakerUnit(int strength, string label)
        {
            PartyMemberState member = _party.GetMemberState("hero");
            member.progression.unit_base_attributes = BuildAttributes(strength);
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_mountainbreaker_{label}")
            );
            return BuildUnit(strength);
        }

        private BattleUnitState BuildUnit(int strength)
        {
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(_party, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException("mountainbreaker fixture should build one ally.");
            BattleUnitState unit = units[0];
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(Strength, strength);
            unit.attribute_snapshot.SetValue(StrengthModifier, AttributeSnapshot.CalculateScoreModifier(strength));
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 50);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _management?.Dispose();
        }
    }
}

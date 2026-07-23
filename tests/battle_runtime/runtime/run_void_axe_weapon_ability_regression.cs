using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_void_axe_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_greataxe_void";
    private static readonly StringName BoundaryCutTraitId = "weapon.axe.void.boundary_cut";
    private static readonly StringName RiftSealTraitId = "weapon.axe.void.rift_seal";
    private static readonly StringName OldWoundTraitId = "weapon.axe.void.old_wound";
    private static readonly StringName ThreefoldLimitTraitId = "weapon.axe.void.threefold_limit";
    private static readonly StringName DissipationTraitId = "weapon.axe.void.dissipation";
    private static readonly StringName BoundaryCutBindingId = "binding.weapon.axe.void.boundary_cut";
    private static readonly StringName RiftStateTag = "void_rift";
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
            TestVoidAxeProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestBoundaryCutCreatesBlockingRiftOnRealWeaponHitAndExpiresAfter80Tu();
            TestBoundaryCutKeepsOnlyThreeActiveRifts();
            RequestTestExit(_test.Finish("Void axe weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Void axe weapon ability regression"));
        }
    }

    private void TestVoidAxeProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using Fixture fixture = Fixture.Build(new GArray { 6 });
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含虚空之斧。");
        foreach (StringName traitId in TraitIds())
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"虚空之斧应包含 trait {traitId}。");
        _test.True(
            fixture.Bindings.ContainsKey(BoundaryCutBindingId),
            "虚空之斧应包含断界切口 binding。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, BoundaryCutBindingId, "apply_edge_feature"),
            "断界切口必须由通用 apply_edge_feature action 配置声明。"
        );

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_void.tres"
        );
        _test.True(rawItem != null, "虚空之斧原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "虚空之斧 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "虚空之斧", "虚空之斧显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "虚空之斧应继承 greataxe 模板。"
            );
            _test.Eq(rawItem.base_price, 85000, "虚空之斧价格应为 85000。");
            _test.Eq(rawItem.trait_ids.Count, 5, "虚空之斧应有且只有 5 个特性。");
            foreach (StringName traitId in TraitIds())
                _test.True(rawItem.trait_ids.Contains(traitId), $"虚空之斧 item 应声明 {traitId}。");

            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "虚空之斧应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.family, new StringName("axe"), "虚空之斧 family 应为 axe。");
                _test.Eq(profile.range_type, new StringName("melee"), "虚空之斧应为 melee。");
                _test.Eq(profile.damage_tag, new StringName("physical_slash"), "虚空之斧应为斩击。");
                _test.Eq(profile.attack_range, 1, "虚空之斧攻击距离应为 1。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 1, "虚空之斧应为 1D12+3。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 12, "虚空之斧应为 1D12+3。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 3, "虚空之斧应为 1D12+3。");
                _test.True(Contains(profile.GetPropertiesTyped(), "two_handed"), "虚空之斧应声明 two_handed。");
                _test.True(Contains(profile.GetPropertiesTyped(), "heavy"), "虚空之斧应声明 heavy。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildVoidAxeUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "虚空之斧装备后 unit 应保留 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("greataxe"), "虚空之斧应投影为 greataxe。");
        _test.Eq(equipped.weapon_attack_range, 1, "虚空之斧投影攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "虚空之斧应占用双手。");
        foreach (StringName traitId in TraitIds())
            _test.True(equipped.effective_trait_ids.Contains(traitId), $"装备后应投影 {traitId}。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            BoundaryCutTraitId,
            BoundaryCutBindingId,
            "eq_void_axe_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除虚空之斧后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除虚空之斧后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除虚空之斧后装备能力源应清空。");
    }

    private void TestBoundaryCutCreatesBlockingRiftOnRealWeaponHitAndExpiresAfter80Tu()
    {
        using Fixture fixture = Fixture.Build(new GArray { 6 });
        BattleUnitState attacker = fixture.BuildVoidAxeUnit("real_hit");
        BattleUnitState target = BuildTarget("void_real_hit_target", new Vector2I(1, 0), hp: 100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "void_axe_real_hit",
            previewCommand: false
        );

        BattleState state = fixture.Runtime.GetState();
        AssertRiftEdge(state, fixture.Runtime.GetGridService(), Vector2I.Zero, new Vector2I(1, 0), true);
        _test.False(
            fixture.Runtime.GetGridService().CanTraverse(state, Vector2I.Zero, new Vector2I(1, 0)),
            "断界裂隙生成后，相邻两格之间应不能跨边移动。"
        );

        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 40);
        attacker.ResetPerTurnCharges();
        ResolveAfterHit(
            fixture.Runtime.GetEquipmentAbilityRuntimeService(),
            state,
            attacker,
            target
        );
        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 40);
        AssertRiftEdge(state, fixture.Runtime.GetGridService(), Vector2I.Zero, new Vector2I(1, 0), true);
        fixture.Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), 40);

        AssertRiftEdge(state, fixture.Runtime.GetGridService(), Vector2I.Zero, new Vector2I(1, 0), false);
    }

    private void TestBoundaryCutKeepsOnlyThreeActiveRifts()
    {
        using Fixture fixture = Fixture.Build(new GArray { 6 });
        BattleUnitState attacker = fixture.BuildVoidAxeUnit("cap");
        attacker.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState east = BuildTarget("void_east", new Vector2I(2, 1), hp: 100);
        BattleUnitState south = BuildTarget("void_south", new Vector2I(1, 2), hp: 100);
        BattleUnitState west = BuildTarget("void_west", new Vector2I(0, 1), hp: 100);
        BattleUnitState north = BuildTarget("void_north", new Vector2I(1, 0), hp: 100);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "void_axe_cap",
            attacker,
            east,
            mapSize: new Vector2I(4, 4)
        );
        AddUnitToState(state, south);
        AddUnitToState(state, west);
        AddUnitToState(state, north);
        fixture.Runtime.SetupStateForTests(state);
        BattleEquipmentAbilityRuntimeService service = fixture.Runtime.GetEquipmentAbilityRuntimeService();

        ResolveAfterHit(service, state, attacker, east);
        attacker.ResetPerTurnCharges();
        ResolveAfterHit(service, state, attacker, south);
        attacker.ResetPerTurnCharges();
        ResolveAfterHit(service, state, attacker, west);
        attacker.ResetPerTurnCharges();
        ResolveAfterHit(service, state, attacker, north);

        BattleGridService grid = fixture.Runtime.GetGridService();
        AssertRiftEdge(state, grid, new Vector2I(1, 1), new Vector2I(2, 1), false);
        AssertRiftEdge(state, grid, new Vector2I(1, 1), new Vector2I(1, 2), true);
        AssertRiftEdge(state, grid, new Vector2I(1, 1), new Vector2I(0, 1), true);
        AssertRiftEdge(state, grid, new Vector2I(1, 1), new Vector2I(1, 0), true);
    }

    private static void ResolveAfterHit(
        BattleEquipmentAbilityRuntimeService service,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        service.ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                AttackSucceeded = true,
                WeaponHpDamage = 1,
            }
        );
    }

    private static void AssertRiftEdge(
        BattleState state,
        BattleGridService grid,
        Vector2I from,
        Vector2I to,
        bool expectedPresent
    )
    {
        BattleEdgeFaceState edge = grid?.GetEdgeFace(state, from, to);
        bool present =
            edge != null
            && edge.feature_state_tag == RiftStateTag
            && edge.BlocksMove()
            && edge.BlocksOccupancy()
            && !edge.feature_blocks_los;
        if (present != expectedPresent)
        {
            throw new InvalidOperationException(
                $"rift edge {from}->{to} expected={expectedPresent} actual={present}"
            );
        }
    }

    private static bool BindingHasActionKind(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        StringName actionKind
    )
    {
        if (bindings == null || !bindings.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                if (action?.Kind == actionKind)
                    return true;
        return false;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        string instanceId
    )
    {
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"missing trait {traitId}");
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (
                source?.SourceEquipmentInstanceId == instanceId
                && source.AbilityIds?.Contains(bindingId) == true
            )
            {
                return;
            }
        }
        throw new InvalidOperationException($"missing ability source {bindingId}");
    }

    private static IReadOnlyList<StringName> TraitIds() =>
        new[]
        {
            BoundaryCutTraitId,
            RiftSealTraitId,
            OldWoundTraitId,
            ThreefoldLimitTraitId,
            DissipationTraitId,
        };

    private static bool Contains(IEnumerable<StringName> values, StringName needle)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == needle)
                return true;
        return false;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            coord = coord,
            is_alive = true,
            current_hp = hp,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.RefreshFootprint();
        return unit;
    }

    private static void AddUnitToState(BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        state.enemy_unit_ids.Add(unit.unit_id);
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
    }

    private static PartyState BuildPartyState(StringName memberId)
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
        member.progression.unit_base_attributes = BuildAttributes();
        party.SetMemberState(member);
        party.active_member_ids = new GStringNameArray { memberId };
        party.leader_member_id = memberId;
        party.main_character_member_id = memberId;
        return party;
    }

    private static UnitBaseAttributes BuildAttributes()
    {
        UnitBaseAttributes attributes = new();
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 18);
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

        internal static Fixture Build(GArray damageRolls)
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState party = BuildPartyState("hero");
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new Fixture(management, party, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _party.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildUnit(label);
        }

        internal BattleUnitState BuildVoidAxeUnit(string label)
        {
            PartyMemberState member = _party.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_void_axe_{label}")
            );
            return BuildUnit(label);
        }

        private BattleUnitState BuildUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(_party, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"void axe fixture should build one ally: {label}");
            BattleUnitState unit = units[0];
            unit.SetAnchorCoord(Vector2I.Zero);
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

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_flame_morningstar_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName FlameItemId = "weapon_unique_morningstar_flame_208";
    private static readonly StringName FlameStrikeTraitId =
        "weapon.morningstar.flame.flame_strike";
    private static readonly StringName FireImmunityTraitId =
        "weapon.morningstar.flame.fire_immunity";
    private static readonly StringName FlameStrikeBindingId =
        "binding.weapon.morningstar.flame.flame_strike";

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
            TestFlameContentLoadsAndProjectsWeaponProfileAndFireImmunity();
            TestFlameStrikeAddsFireDamageOnRealWeaponHit();
            TestFlameStrikeRollGateControlsBurning();
            RequestTestExit(_test.Finish("Flame Morningstar weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Flame Morningstar weapon ability regression"));
        }
    }

    private void TestFlameContentLoadsAndProjectsWeaponProfileAndFireImmunity()
    {
        using FlameFixture fixture = FlameFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(FlameItemId), "真实物品内容应包含火焰晨星。");
        _test.True(
            fixture.TraitDefs.ContainsKey(FlameStrikeTraitId),
            "真实 trait 内容应包含火焰打击。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(FireImmunityTraitId),
            "真实 trait 内容应包含火焰之友/火焰免疫。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(FlameStrikeBindingId),
            "真实装备能力内容应包含火焰打击 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(FlameItemId))
            return;

        ItemDef rawFlame = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_morningstar_flame.tres"
        );
        _test.True(rawFlame != null, "火焰晨星原始资源应能加载。");
        if (rawFlame != null)
        {
            _test.Eq(rawFlame.display_name, "火焰晨星", "火焰晨星显示名应来自设计源。");
            _test.Eq(
                rawFlame.base_item_id,
                new StringName("weapon_type_morningstar_base"),
                "火焰晨星应继承 morningstar 模板。"
            );
            _test.Eq(rawFlame.base_price, 48000, "火焰晨星基础价格应为 48000。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildFlameUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, FlameItemId, "火焰晨星装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("morningstar"), "火焰晨星应投影为 morningstar。");
        _test.Eq(equippedWeapon.Family, new StringName("mace"), "火焰晨星应保留 mace 家族。");
        _test.Eq(
            equippedWeapon.PhysicalDamageTag,
            new StringName("physical_pierce"),
            "火焰晨星基础伤害标签应为 physical_pierce。"
        );
        _test.Eq(equippedWeapon.AttackRange, 1, "火焰晨星攻击距离应为 1。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "火焰晨星单手应为 1D8+2。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 8, "火焰晨星单手应为 1D8+2。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 2, "火焰晨星单手应为 1D8+2。");
        _test.True(equippedWeapon.IsVersatile, "火焰晨星应保留 versatile 属性。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FlameStrikeTraitId,
            FlameStrikeBindingId,
            "eq_flame_projection"
        );
        _test.True(
            equipped.HasEffectiveTrait(FireImmunityTraitId),
            "火焰免疫 trait 应作为固定装备 trait 投影到战斗单位。"
        );
        _test.Eq(
            GetDamageMitigation(equipped, "fire"),
            new StringName("immune"),
            "火焰免疫 trait 应投影 fire damage immune。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        equippedWeapon = equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, new StringName(""), "移除火焰晨星后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除火焰晨星后装备能力源应清空。"
        );
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除火焰晨星后装备 trait 实例应回到装备前状态。"
        );
        _test.False(
            equipped.HasEffectiveTrait(FireImmunityTraitId),
            "移除火焰晨星后火焰免疫 trait 不应残留。"
        );
        _test.False(
            HasDamageMitigation(equipped, "fire"),
            "移除火焰晨星后 fire damage immune 不应残留。"
        );
    }

    private void TestFlameStrikeAddsFireDamageOnRealWeaponHit()
    {
        using FlameFixture fixture = FlameFixture.Build(new GArray { 4, 2 });
        BattleUnitState attacker = fixture.BuildFlameUnit("fire_damage");
        BattleUnitState target = BuildTarget("fire_damage_target", new Vector2I(1, 0));
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 10 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "flame_morningstar_fire_damage",
            previewCommand: false
        );
        int flameDamage = 100 - target.GetCurrentHp();

        using FlameFixture plainFixture = FlameFixture.Build(new GArray { 4, 2 });
        BattleUnitState plainAttacker = plainFixture.BuildFlameUnit("plain_damage");
        plainAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainTarget = BuildTarget("plain_damage_target", new Vector2I(1, 0));
        plainTarget.SetCurrentHp(100);
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "flame_morningstar_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.GetCurrentHp();

        _test.Eq(plainDamage, 6, "固定骰 4 时，火焰晨星基础武器伤害应为 1D8+2。");
        _test.Eq(
            flameDamage,
            8,
            "火焰打击应在真实命中后额外造成 1D6 fire，且不吞掉武器伤害。"
        );
    }

    private void TestFlameStrikeRollGateControlsBurning()
    {
        using FlameFixture failFixture = FlameFixture.Build(new GArray { 4, 2 });
        BattleUnitState failAttacker = failFixture.BuildFlameUnit("burn_fail");
        BattleUnitState failTarget = BuildTarget("burn_fail_target", new Vector2I(1, 0));
        failTarget.SetCurrentHp(100);
        failTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        failFixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 10 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            failFixture.Runtime,
            failAttacker,
            failTarget,
            "flame_morningstar_burn_fail",
            previewCommand: false
        );
        _test.False(
            failTarget.HasStatusEffect("burning"),
            "火焰晨星 roll_gate 失败（1D20=10）时不应施加 burning。"
        );

        using FlameFixture successFixture = FlameFixture.Build(new GArray { 4, 2 });
        BattleUnitState successAttacker = successFixture.BuildFlameUnit("burn_success");
        BattleUnitState successTarget = BuildTarget("burn_success_target", new Vector2I(1, 0));
        successTarget.SetCurrentHp(100);
        successTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        successFixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 11 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            successFixture.Runtime,
            successAttacker,
            successTarget,
            "flame_morningstar_burn_success",
            previewCommand: false
        );
        BattleStatusEffectState burning = successTarget.GetStatusEffect("burning");
        _test.True(
            burning != null,
            "火焰晨星 roll_gate 成功（1D20=11）时应施加 burning。"
        );
        _test.Eq(burning?.duration ?? -1, 60, "火焰晨星 burning 应持续 60 TU。");
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: 30,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
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

    private static bool HasDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        return unit != null
            && unit.HasDamageResistanceTyped(damageTag);
    }

    private static StringName GetDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit == null)
            return "";
        if (unit.TryGetDamageResistanceTyped(damageTag, out StringName value))
            return ProgressionDataUtils.to_string_name(value);
        return "";
    }

    private sealed class FlameFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private FlameFixture(
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

        internal static FlameFixture Build(GArray damageRolls)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new FlameFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildFlameUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                FlameItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    FlameItemId,
                    $"eq_flame_{label}"
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

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_dragonbone_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName DragonboneItemId = "weapon_unique_axe_dragonbone_096";
    private static readonly StringName DragonFlameTraitId =
        "weapon.axe.dragonbone.dragon_flame";
    private static readonly StringName DragonHateTraitId =
        "weapon.axe.dragonbone.dragon_hate";
    private static readonly StringName DragonFlameBindingId =
        "binding.weapon.axe.dragonbone.dragon_flame";
    private static readonly StringName DragonHateBindingId =
        "binding.weapon.axe.dragonbone.dragon_hate";

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
            TestDragonboneProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestDragonFlameTriggersOncePerHolderTurnAsFireDamage();
            TestDragonHateAppliesToEveryDragonHitAfterDragonFlameIsConsumed();
            RequestTestExit(_test.Finish("Dragonbone weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Dragonbone weapon ability regression"));
        }
    }

    private void TestDragonboneProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using DragonboneFixture fixture = DragonboneFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(DragonboneItemId), "真实物品内容应包含龙骨斧。");
        _test.True(
            fixture.TraitDefs.ContainsKey(DragonFlameTraitId),
            "真实 trait 内容应包含龙焰。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(DragonHateTraitId),
            "真实 trait 内容应包含龙族之恨。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DragonFlameBindingId),
            "真实装备能力内容应包含龙焰 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DragonHateBindingId),
            "真实装备能力内容应包含龙族之恨 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(DragonboneItemId))
            return;

        ItemDef rawDragonbone = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_dragonbone.tres"
        );
        _test.True(rawDragonbone != null, "龙骨斧原始资源应能加载。");
        if (rawDragonbone != null)
        {
            _test.Eq(
                rawDragonbone.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "龙骨斧原始资源应声明继承 greataxe 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleUnitState equipped = fixture.BuildDragonboneUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(equippedWeapon.ItemId, DragonboneItemId, "龙骨斧装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            new StringName("greataxe"),
            "龙骨斧应投影为 greataxe。"
        );
        _test.Eq(equippedWeapon.AttackRange, 1, "龙骨斧攻击距离应为 1。");
        _test.True(equippedWeapon.UsesTwoHands, "龙骨斧应占用双手。");
        _test.Eq(
            equippedWeapon.TwoHandedDice.DiceCount,
            1,
            "龙骨斧双手骰数量应为 1。"
        );
        _test.Eq(
            equippedWeapon.TwoHandedDice.DiceSides,
            12,
            "龙骨斧双手骰面应为 12。"
        );
        _test.Eq(
            equippedWeapon.TwoHandedDice.FlatBonus,
            3,
            "龙骨斧双手骰固定加值应为 +3。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DragonFlameTraitId,
            DragonFlameBindingId,
            "eq_dragonbone_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DragonHateTraitId,
            DragonHateBindingId,
            "eq_dragonbone_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        equippedWeapon = equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, new StringName(""), "移除龙骨斧后 weapon_item_id 应清空。");
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除龙骨斧后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equippedWeapon.PhysicalDamageTag,
            baselineWeapon.PhysicalDamageTag,
            "移除龙骨斧后 weapon_physical_damage_tag 应回到装备前状态。"
        );
        _test.Eq(
            equippedWeapon.AttackRange,
            baselineWeapon.AttackRange,
            "移除龙骨斧后攻击距离应回到装备前状态。"
        );
        _test.Eq(
            equippedWeapon.CurrentGrip,
            baselineWeapon.CurrentGrip,
            "移除龙骨斧后当前握持应回到装备前状态。"
        );
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除龙骨斧后装备能力源应清空。"
        );
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除龙骨斧后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestDragonFlameTriggersOncePerHolderTurnAsFireDamage()
    {
        using DragonboneFixture fixture = DragonboneFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildDragonboneUnit("dragon_flame");
        BattleUnitState target = BuildTarget(
            "dragon_flame_humanoid",
            new Vector2I(1, 0),
            "humanoid"
        );
        target.SetCurrentHp(120);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);

        int beforeFirst = target.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "dragonbone_flame_first",
            previewCommand: false
        );
        int firstDamage = beforeFirst - target.GetCurrentHp();

        int beforeSecond = target.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "dragonbone_flame_second",
            previewCommand: false
        );
        int secondDamage = beforeSecond - target.GetCurrentHp();

        _test.True(
            firstDamage > secondDamage,
            "龙骨斧真实基础攻击第一次命中应比同回合第二次多出龙焰伤害。"
        );

        attacker.ResetPerTurnCharges();
        int beforeNextTurn = target.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "dragonbone_flame_next_turn",
            previewCommand: false
        );
        int nextTurnDamage = beforeNextTurn - target.GetCurrentHp();
        _test.True(
            nextTurnDamage > secondDamage,
            "持有者 per-turn charge 重置后，真实基础攻击应再次获得龙焰伤害。"
        );
    }

    private void TestDragonHateAppliesToEveryDragonHitAfterDragonFlameIsConsumed()
    {
        using DragonboneFixture fixture = DragonboneFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildDragonboneUnit("dragon_hate");
        BattleUnitState warmupTarget = BuildTarget(
            "warmup_humanoid",
            new Vector2I(1, 0),
            "humanoid"
        );
        BattleUnitState dragonTarget = BuildTarget("dragon_target", new Vector2I(1, 0), "dragon");
        dragonTarget.SetCurrentHp(120);
        dragonTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            warmupTarget,
            "dragonbone_hate_warmup",
            previewCommand: false
        );

        int beforeFirstDragon = dragonTarget.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            dragonTarget,
            "dragonbone_hate_first_dragon",
            previewCommand: false
        );
        int firstDragonDamage = beforeFirstDragon - dragonTarget.GetCurrentHp();

        int beforeSecondDragon = dragonTarget.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            dragonTarget,
            "dragonbone_hate_second_dragon",
            previewCommand: false
        );
        int secondDragonDamage = beforeSecondDragon - dragonTarget.GetCurrentHp();

        using DragonboneFixture plainFixture = DragonboneFixture.Build(new GArray());
        BattleUnitState plainAttacker = plainFixture.BuildDragonboneUnit("dragon_hate_plain");
        plainAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainDragon = BuildTarget("plain_dragon_target", new Vector2I(1, 0), "dragon");
        plainDragon.SetCurrentHp(120);
        plainDragon.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainDragon,
            "dragonbone_hate_plain_dragon",
            previewCommand: false
        );
        int plainDragonDamage = 120 - plainDragon.GetCurrentHp();

        _test.True(
            firstDragonDamage > plainDragonDamage,
            "龙焰被 warmup 消耗后，真实基础攻击命中 dragon 仍应因龙族之恨高于同武器无装备能力伤害。"
        );
        _test.True(
            secondDragonDamage > plainDragonDamage,
            "龙族之恨应在同回合后续真实基础攻击命中 dragon 时继续生效。"
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
            in unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, StringName tag)
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
        unit.AddCreatureTypeTagTyped(tag);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class DragonboneFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private DragonboneFixture(
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

        internal static DragonboneFixture Build(GArray damageRolls)
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
            return new DragonboneFixture(
                characterManagement,
                partyState,
                runtime,
                snapshot
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildDragonboneUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                DragonboneItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    DragonboneItemId,
                    $"eq_dragonbone_{label}"
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

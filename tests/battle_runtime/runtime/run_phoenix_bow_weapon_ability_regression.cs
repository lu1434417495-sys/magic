using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_phoenix_bow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName PhoenixItemId = "weapon_unique_bow_phoenix_330";
    private static readonly StringName FireArrowTraitId = "weapon.bow.phoenix.fire_arrow";
    private static readonly StringName FireArrowBindingId =
        "binding.weapon.bow.phoenix.fire_arrow";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPhoenixBowContentLoadsAndProjects();
            TestPhoenixFireArrowAddsFireDamageOnRealWeaponHit();
            TestPhoenixFireArrowRollGateControlsBurning();
            RequestTestExit(_test.Finish("Phoenix Bow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Phoenix Bow weapon ability regression"));
        }
    }

    private void TestPhoenixBowContentLoadsAndProjects()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(PhoenixItemId), "真实物品内容应包含凤凰之弓。");
        _test.True(
            fixture.TraitDefs.ContainsKey(FireArrowTraitId),
            "真实 trait 内容应包含火焰箭。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(FireArrowBindingId),
            "真实装备能力内容应包含火焰箭 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(PhoenixItemId))
            return;

        ItemDef rawPhoenix = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longbow_phoenix.tres"
        );
        _test.True(rawPhoenix != null, "凤凰之弓原始资源应能加载。");
        if (rawPhoenix != null)
        {
            _test.Eq(rawPhoenix.display_name, "凤凰之弓", "凤凰之弓显示名应来自设计源。");
            _test.Eq(
                rawPhoenix.base_item_id,
                new StringName("weapon_type_longbow_base"),
                "凤凰之弓应继承 longbow 模板。"
            );
            _test.Eq(rawPhoenix.base_price, 75000, "凤凰之弓基础价格应为 75000。");
            _test.True(rawPhoenix.tags.Contains(new StringName("phoenix_bow")), "凤凰之弓应声明 phoenix_bow item tag。");
            _test.True(rawPhoenix.trait_ids.Contains(FireArrowTraitId), "凤凰之弓应固定声明火焰箭 trait。");
            WeaponProfileDef rawProfile = rawPhoenix.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "凤凰之弓应声明武器 profile override。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.attack_range, 10, "凤凰之弓攻击距离应覆盖为 10。");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "凤凰之弓应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 8, "凤凰之弓应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "凤凰之弓应为 1D8+2。");
                _test.True(rawProfile.properties.Contains(new StringName("heavy")), "凤凰之弓应添加 heavy 属性。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildPhoenixUnit("projection");
        _test.Eq(equipped.weapon_item_id, PhoenixItemId, "凤凰之弓装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longbow"), "凤凰之弓应投影为 longbow。");
        _test.Eq(equipped.weapon_family, new StringName("bow"), "凤凰之弓应保留 bow 家族。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_pierce"),
            "凤凰之弓基础伤害标签应为 physical_pierce。"
        );
        _test.Eq(equipped.weapon_attack_range, 10, "凤凰之弓攻击距离应为 10。");
        _test.True(equipped.weapon_uses_two_hands, "凤凰之弓应占用双手。");
        _test.Eq(equipped.weapon_current_grip, new StringName("two_handed"), "凤凰之弓应使用双手握法。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "凤凰之弓应为 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 8, "凤凰之弓应为 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "凤凰之弓应为 1D8+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FireArrowTraitId,
            FireArrowBindingId,
            "eq_phoenix_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除凤凰之弓后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除凤凰之弓后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除凤凰之弓后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除凤凰之弓后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestPhoenixFireArrowAddsFireDamageOnRealWeaponHit()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState attacker = fixture.BuildPhoenixUnit("fire_damage");
        BattleUnitState target = BuildTarget("fire_damage_target", new Vector2I(1, 0));
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 10 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "phoenix_bow_fire_damage",
            previewCommand: false
        );
        int phoenixDamage = 100 - target.current_hp;

        using PhoenixFixture plainFixture = PhoenixFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState plainAttacker = plainFixture.BuildPhoenixUnit("plain_damage");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildTarget("plain_damage_target", new Vector2I(1, 0));
        plainTarget.current_hp = 100;
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "phoenix_bow_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.current_hp;

        _test.Eq(plainDamage, 6, "固定骰 4 时，凤凰之弓基础武器伤害应为 1D8+2。");
        _test.Eq(
            phoenixDamage,
            14,
            "火焰箭应在真实命中后额外造成 2D6 fire，且不吞掉武器伤害。"
        );
    }

    private void TestPhoenixFireArrowRollGateControlsBurning()
    {
        using PhoenixFixture failFixture = PhoenixFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState failAttacker = failFixture.BuildPhoenixUnit("burn_fail");
        BattleUnitState failTarget = BuildTarget("burn_fail_target", new Vector2I(1, 0));
        failTarget.current_hp = 100;
        failTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        failFixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 10 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            failFixture.Runtime,
            failAttacker,
            failTarget,
            "phoenix_bow_burn_fail",
            previewCommand: false
        );
        _test.False(
            failTarget.HasStatusEffect("burning"),
            "凤凰之弓 roll_gate 未通过（1D20=10）时不应施加 burning。"
        );

        using PhoenixFixture successFixture = PhoenixFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState successAttacker = successFixture.BuildPhoenixUnit("burn_success");
        BattleUnitState successTarget = BuildTarget("burn_success_target", new Vector2I(1, 0));
        successTarget.current_hp = 100;
        successTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        successFixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 11 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            successFixture.Runtime,
            successAttacker,
            successTarget,
            "phoenix_bow_burn_success",
            previewCommand: false
        );
        BattleStatusEffectState burning = successTarget.GetStatusEffect("burning");
        _test.True(
            burning != null,
            "凤凰之弓 roll_gate 通过（1D20=11）时应施加 burning。"
        );
        _test.Eq(burning?.duration ?? -1, 60, "凤凰之弓 burning 应持续 60 TU。");
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

    private sealed class PhoenixFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private PhoenixFixture(
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
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static PhoenixFixture Build(GArray damageRolls)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new PhoenixFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildPhoenixUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                PhoenixItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    PhoenixItemId,
                    $"eq_phoenix_{label}"
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
}

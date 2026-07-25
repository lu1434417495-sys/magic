using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_thunder_halberd_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ThunderHalberdItemId =
        "weapon_unique_polearm_thunder_halberd_137";
    private static readonly StringName ThunderSlashTraitId =
        "weapon.polearm.thunder_halberd.thunder_slash";
    private static readonly StringName ThunderSlashBindingId =
        "binding.weapon.polearm.thunder_halberd.thunder_slash";
    private static readonly StringName StunnedStatusId = "stunned";

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
            TestThunderHalberdContentLoadsAndProjects();
            TestThunderSlashAddsThunderDamageOnRealWeaponHit();
            TestNonCriticalHitDoesNotStun();
            TestCriticalFailedConSaveAppliesStunnedForSixtyTu();
            TestCriticalSuccessfulConSaveDoesNotStun();
            RequestTestExit(_test.Finish("Thunder Halberd weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Thunder Halberd weapon ability regression"));
        }
    }

    private void TestThunderHalberdContentLoadsAndProjects()
    {
        using ThunderHalberdFixture fixture = ThunderHalberdFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ThunderHalberdItemId), "真实物品内容应包含雷霆之戟。");
        _test.True(fixture.TraitDefs.ContainsKey(ThunderSlashTraitId), "真实 trait 内容应包含雷鸣斩。");
        _test.True(
            fixture.Bindings.ContainsKey(ThunderSlashBindingId),
            "真实装备能力内容应包含雷鸣斩 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(ThunderHalberdItemId))
            return;

        ItemDef rawHalberd = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_halberd_thunder_halberd.tres"
        );
        _test.True(rawHalberd != null, "雷霆之戟原始资源应能加载。");
        if (rawHalberd != null)
        {
            _test.Eq(rawHalberd.display_name, "雷霆之戟", "雷霆之戟显示名应来自设计源。");
            _test.Eq(
                rawHalberd.base_item_id,
                new StringName("weapon_type_halberd_base"),
                "雷霆之戟应继承 halberd 模板。"
            );
            _test.Eq(rawHalberd.base_price, 52000, "雷霆之戟基础价格应为 52000。");
            WeaponProfileDef rawProfile = rawHalberd.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "雷霆之戟应声明武器 profile override。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.training_group, new StringName("martial"), "雷霆之戟训练组应为 martial。");
                _test.Eq(rawProfile.range_type, new StringName("melee"), "雷霆之戟应为 melee。");
                _test.Eq(rawProfile.family, new StringName("polearm"), "雷霆之戟应属于 polearm。");
                _test.Eq(rawProfile.damage_tag, new StringName("physical_slash"), "雷霆之戟应为 slashing 伤害。");
                _test.Eq(rawProfile.attack_range, 2, "雷霆之戟攻击距离应为 2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "雷霆之戟双手应为 1D10+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 10, "雷霆之戟双手应为 1D10+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "雷霆之戟双手应为 1D10+2。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "two_handed"),
                    "雷霆之戟应声明 two_handed 属性。"
                );
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "heavy"),
                    "雷霆之戟应声明 heavy 属性。"
                );
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "reach"),
                    "雷霆之戟应声明 reach 属性。"
                );
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildThunderHalberdUnit("projection");
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, ThunderHalberdItemId, "雷霆之戟装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("halberd"), "雷霆之戟应投影为 halberd。");
        _test.Eq(equippedWeapon.Family, new StringName("polearm"), "雷霆之戟应投影为 polearm。");
        _test.Eq(
            equippedWeapon.PhysicalDamageTag,
            new StringName("physical_slash"),
            "雷霆之戟基础伤害标签应为 physical_slash。"
        );
        _test.Eq(equippedWeapon.AttackRange, 2, "雷霆之戟攻击距离应为 2。");
        _test.True(equippedWeapon.UsesTwoHands, "雷霆之戟应占用双手。");
        _test.False(equippedWeapon.IsVersatile, "雷霆之戟不应是 versatile。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceCount, 1, "雷霆之戟双手应为 1D10+2。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceSides, 10, "雷霆之戟双手应为 1D10+2。");
        _test.Eq(equippedWeapon.TwoHandedDice.FlatBonus, 2, "雷霆之戟双手应为 1D10+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ThunderSlashTraitId,
            ThunderSlashBindingId,
            "eq_thunder_halberd_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleWeaponProjectionValues removedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(removedWeapon.ItemId, new StringName(""), "移除雷霆之戟后 weapon_item_id 应清空。");
        _test.Eq(
            removedWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除雷霆之戟后武器 profile 应回到装备前状态。"
        );
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除雷霆之戟后装备能力源应清空。"
        );
        _test.False(
            equipped.HasEffectiveTrait(ThunderSlashTraitId),
            "移除雷霆之戟后雷鸣斩 trait 不应残留。"
        );
    }

    private void TestThunderSlashAddsThunderDamageOnRealWeaponHit()
    {
        using ThunderHalberdFixture fixture = ThunderHalberdFixture.Build(new GArray { 4, 3 });
        BattleUnitState attacker = fixture.BuildThunderHalberdUnit("thunder_damage");
        BattleUnitState target = BuildTarget("thunder_damage_target", new Vector2I(2, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "thunder_halberd_damage",
            previewCommand: false
        );
        int thunderDamage = 100 - target.GetCurrentHp();

        using ThunderHalberdFixture plainFixture = ThunderHalberdFixture.Build(new GArray { 4, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildThunderHalberdUnit("plain_damage");
        plainAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainTarget = BuildTarget("plain_damage_target", new Vector2I(2, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "thunder_halberd_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.GetCurrentHp();

        _test.Eq(plainDamage, 6, "固定骰 4 时，雷霆之戟基础武器伤害应为 1D10+2。");
        _test.Eq(
            thunderDamage,
            9,
            "雷鸣斩应在真实武器命中后额外造成 1D6 thunder，且不吞掉基础武器伤害。"
        );
    }

    private void TestNonCriticalHitDoesNotStun()
    {
        using ThunderHalberdFixture fixture = ThunderHalberdFixture.Build(new GArray { 4, 3 });
        BattleUnitState attacker = fixture.BuildThunderHalberdUnit("noncritical");
        BattleUnitState target = BuildTarget("noncritical_target", new Vector2I(2, 0), hp: 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "thunder_halberd_noncritical",
            previewCommand: false
        );

        _test.False(
            target.HasStatusEffect(StunnedStatusId),
            "非暴击真实命中不应施加 stunned，即使目标 CON 豁免很低。"
        );
    }

    private void TestCriticalFailedConSaveAppliesStunnedForSixtyTu()
    {
        using ThunderHalberdFixture fixture = ThunderHalberdFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildThunderHalberdUnit("critical_failed_save");
        BattleUnitState target = BuildTarget("critical_failed_save_target", new Vector2I(2, 0), hp: 100);

        ResolveThunderSlashAfterHit(
            fixture,
            attacker,
            target,
            "thunder_halberd_critical_failed_save",
            criticalHit: true,
            saveRollOverride: 1
        );

        BattleStatusEffectState stunned = target.GetStatusEffect(StunnedStatusId);
        _test.True(stunned != null, "暴击且 CON DC14 豁免失败时应施加 stunned。");
        _test.Eq(stunned?.duration ?? -1, 60, "雷霆之戟 stunned 应持续 60 TU。");
        _test.Eq(stunned?.source_unit_id ?? new StringName(""), attacker.unit_id, "stunned 应记录雷霆之戟持有者来源。");
    }

    private void TestCriticalSuccessfulConSaveDoesNotStun()
    {
        using ThunderHalberdFixture fixture = ThunderHalberdFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildThunderHalberdUnit("critical_success_save");
        BattleUnitState target = BuildTarget("critical_success_save_target", new Vector2I(2, 0), hp: 100);

        ResolveThunderSlashAfterHit(
            fixture,
            attacker,
            target,
            "thunder_halberd_critical_success_save",
            criticalHit: true,
            saveRollOverride: 20
        );

        _test.False(
            target.HasStatusEffect(StunnedStatusId),
            "暴击但 CON DC14 豁免成功时不应施加 stunned。"
        );
    }

    private static void ResolveThunderSlashAfterHit(
        ThunderHalberdFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        bool criticalHit,
        int saveRollOverride
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                AttackSucceeded = true,
                CriticalHit = criticalHit,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, int hp)
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
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
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
        if (unit == null)
            return null;
        foreach (
            BattleEquipmentAbilitySourceReadView source in
            unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private sealed class ThunderHalberdFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private ThunderHalberdFixture(
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

        internal static ThunderHalberdFixture Build(GArray damageRolls)
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
            return new ThunderHalberdFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildThunderHalberdUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ThunderHalberdItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    ThunderHalberdItemId,
                    $"eq_thunder_halberd_{label}"
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

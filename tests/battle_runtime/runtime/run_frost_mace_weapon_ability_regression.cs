using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_frost_mace_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName FrostMaceItemId = "weapon_unique_mace_frost_207";
    private static readonly StringName FrozenStrikeTraitId = "weapon.mace.frost.frozen_strike";
    private static readonly StringName SealPowerTraitId = "weapon.mace.frost.seal_power";
    private static readonly StringName PolarAdaptationTraitId =
        "weapon.mace.frost.polar_adaptation";
    private static readonly StringName FrozenStrikeBindingId =
        "binding.weapon.mace.frost.frozen_strike";
    private static readonly StringName SealPowerBindingId =
        "binding.weapon.mace.frost.seal_power";
    private static readonly StringName ChillCountBindingId =
        "binding.weapon.mace.frost.chill_count";
    private static readonly StringName PolarAdaptationBindingId =
        "binding.weapon.mace.frost.polar_adaptation";
    private static readonly StringName ChillCountStatusId = "frost_mace_chill_count";
    private static readonly StringName SlowStatusId = "slow";

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
            TestFrostMaceProjectsRealContentAndClearsOnUnequip();
            TestFrozenStrikeAddsColdDamageOnNormalTarget();
            TestSealPowerAddsExtraColdDamageAgainstUndeadAndFiend();
            TestThirdHitOnSameTargetAppliesSlowWithoutSharingCounterToOtherTarget();
            RequestTestExit(_test.Finish("Frost Mace weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Frost Mace weapon ability regression"));
        }
    }

    private void TestFrostMaceProjectsRealContentAndClearsOnUnequip()
    {
        using FrostMaceFixture fixture = FrostMaceFixture.Build(new GArray());

        _test.True(fixture.ItemDefs.ContainsKey(FrostMaceItemId), "真实物品内容应包含冰霜锤。");
        _test.True(fixture.TraitDefs.ContainsKey(FrozenStrikeTraitId), "真实 trait 内容应包含冰冻打击。");
        _test.True(fixture.TraitDefs.ContainsKey(SealPowerTraitId), "真实 trait 内容应包含封印之力。");
        _test.False(
            fixture.TraitDefs.ContainsKey(PolarAdaptationTraitId),
            "极地适应是未落地设计条目，不应注册占位 trait。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(FrozenStrikeBindingId),
            "真实装备能力内容应包含冰冻打击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(SealPowerBindingId),
            "真实装备能力内容应包含封印之力 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ChillCountBindingId),
            "真实装备能力内容应包含寒霜计数 binding。"
        );
        _test.False(
            fixture.Bindings.ContainsKey(PolarAdaptationBindingId),
            "极地适应不应注册占位 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(FrostMaceItemId))
            return;

        ItemDef rawFrostMace = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_mace_frost.tres"
        );
        _test.True(rawFrostMace != null, "冰霜锤原始资源应能加载。");
        if (rawFrostMace != null)
        {
            _test.Eq(rawFrostMace.display_name, "冰霜锤", "冰霜锤显示名应匹配设计。");
            _test.Eq(
                rawFrostMace.base_item_id,
                new StringName("weapon_type_mace_base"),
                "冰霜锤应继承 mace 模板。"
            );
            _test.Eq(rawFrostMace.base_price, 42000, "冰霜锤基础价格应为 42000。");
            WeaponProfileDef rawProfile = rawFrostMace.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "冰霜锤应声明武器 profile override。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.training_group, new StringName("simple"), "冰霜锤训练组应为 simple。");
                _test.Eq(rawProfile.range_type, new StringName("melee"), "冰霜锤应为 melee。");
                _test.Eq(rawProfile.damage_tag, new StringName("physical_blunt"), "冰霜锤应为钝击。");
                _test.Eq(rawProfile.attack_range, 1, "冰霜锤攻击距离应为 1。");
                _test.Eq(rawProfile.one_handed_dice?.dice_count ?? 0, 1, "冰霜锤单手应为 1D6+2。");
                _test.Eq(rawProfile.one_handed_dice?.dice_sides ?? 0, 6, "冰霜锤单手应为 1D6+2。");
                _test.Eq(rawProfile.one_handed_dice?.flat_bonus ?? 0, 2, "冰霜锤单手应为 1D6+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "冰霜锤双手应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 8, "冰霜锤双手应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "冰霜锤双手应为 1D8+2。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "versatile"),
                    "冰霜锤应声明 versatile 属性。"
                );
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildFrostMaceUnit("projection");
        _test.Eq(equipped.weapon_item_id, FrostMaceItemId, "冰霜锤装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("mace"), "冰霜锤应投影为 mace。");
        _test.Eq(equipped.weapon_family, new StringName("mace"), "冰霜锤应保留 mace 家族。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_blunt"), "冰霜锤应造成钝击。");
        _test.Eq(equipped.weapon_attack_range, 1, "冰霜锤攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "冰霜锤应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "冰霜锤单手应为 1D6+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 6, "冰霜锤单手应为 1D6+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "冰霜锤单手应为 1D6+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FrozenStrikeTraitId,
            FrozenStrikeBindingId,
            "eq_frost_mace_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            SealPowerTraitId,
            SealPowerBindingId,
            "eq_frost_mace_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FrozenStrikeTraitId,
            ChillCountBindingId,
            "eq_frost_mace_projection"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(PolarAdaptationTraitId),
            "装备冰霜锤不应投影极地适应占位 trait。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除冰霜锤后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除冰霜锤后武器 profile 应回到装备前状态。"
        );
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除冰霜锤后装备能力源应清空。"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(FrozenStrikeTraitId),
            "移除冰霜锤后冰冻打击 trait 不应残留。"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(SealPowerTraitId),
            "移除冰霜锤后封印之力 trait 不应残留。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除冰霜锤后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestFrozenStrikeAddsColdDamageOnNormalTarget()
    {
        using FrostMaceFixture fixture = FrostMaceFixture.Build(new GArray { 4, 3 });
        BattleUnitState attacker = fixture.BuildFrostMaceUnit("normal_damage");
        BattleUnitState target = BuildTarget("normal_target", new Vector2I(1, 0), "humanoid", hp: 100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "frost_mace_normal_damage",
            previewCommand: false
        );
        int frostDamage = 100 - target.current_hp;

        using FrostMaceFixture plainFixture = FrostMaceFixture.Build(new GArray { 4, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildFrostMaceUnit("normal_plain");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildTarget(
            "normal_plain_target",
            new Vector2I(1, 0),
            "humanoid",
            hp: 100
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "frost_mace_normal_plain",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.current_hp;

        _test.Eq(plainDamage, 6, "固定骰 4 时，冰霜锤基础武器伤害应为 1D6+2。");
        _test.Eq(
            frostDamage,
            9,
            "普通目标命中后应追加 1D6 cold，且不吞掉基础武器伤害。"
        );
    }

    private void TestSealPowerAddsExtraColdDamageAgainstUndeadAndFiend()
    {
        AssertSealPowerDamageForCreatureTag("undead", "frost_mace_undead_damage");
        AssertSealPowerDamageForCreatureTag("fiend", "frost_mace_fiend_damage");
    }

    private void AssertSealPowerDamageForCreatureTag(StringName creatureTag, StringName battleId)
    {
        using FrostMaceFixture fixture = FrostMaceFixture.Build(new GArray { 4, 3, 5, 6 });
        BattleUnitState attacker = fixture.BuildFrostMaceUnit($"{creatureTag}_damage");
        BattleUnitState target = BuildTarget(
            $"{creatureTag}_target",
            new Vector2I(1, 0),
            creatureTag,
            hp: 100
        );

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            battleId,
            previewCommand: false
        );

        _test.Eq(
            100 - target.current_hp,
            20,
            $"{creatureTag} 目标命中后应造成基础 1D6+2、1D6 cold 与额外 2D6 cold。"
        );
    }

    private void TestThirdHitOnSameTargetAppliesSlowWithoutSharingCounterToOtherTarget()
    {
        using FrostMaceFixture fixture = FrostMaceFixture.Build(new GArray { 4, 3, 4, 3, 4, 3, 4, 3 });
        BattleUnitState attacker = fixture.BuildFrostMaceUnit("slow");
        BattleUnitState target = BuildTarget("slow_target", new Vector2I(1, 0), "humanoid", hp: 120);

        for (int hit = 1; hit <= 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"frost_mace_same_target_hit_{hit}",
                previewCommand: false
            );
            BattleStatusEffectState chill = target.GetStatusEffect(ChillCountStatusId);
            _test.True(chill != null, $"第 {hit} 次命中应记录寒霜命中计数。");
            if (chill != null)
            {
                _test.Eq(chill.stacks, hit, $"第 {hit} 次命中同一目标后计数应为 {hit}。");
                _test.Eq(chill.stack_limit, 3, "寒霜计数上限应为 3。");
                _test.Eq(chill.source_unit_id, attacker.unit_id, "寒霜计数应记录持有者来源。");
            }
            if (hit < 3)
            {
                _test.False(target.HasStatusEffect(SlowStatusId), $"第 {hit} 次命中不应施加 slow。");
            }
        }

        BattleStatusEffectState slow = target.GetStatusEffect(SlowStatusId);
        _test.True(slow != null, "同一目标第 3 次命中后应施加 slow。");
        if (slow != null)
        {
            _test.Eq(slow.duration, 60, "冰霜锤 slow 应持续 60 TU。");
            _test.Eq(slow.source_unit_id, attacker.unit_id, "冰霜锤 slow 应记录持有者来源。");
            _test.Eq(
                BattleStatusSemanticTable.GetMoveCostDelta(slow),
                1,
                "现有 slow 语义应用移动成本 +1 近似 movement halved。"
            );
        }

        BattleUnitState otherTarget = BuildTarget(
            "fresh_other_target",
            new Vector2I(1, 0),
            "humanoid",
            hp: 100
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            otherTarget,
            "frost_mace_other_target_first_hit",
            previewCommand: false
        );
        BattleStatusEffectState otherChill = otherTarget.GetStatusEffect(ChillCountStatusId);
        _test.True(otherChill != null, "换目标后的第一次命中应给新目标自己的寒霜计数。");
        _test.Eq(otherChill?.stacks ?? 0, 1, "换目标不应把旧目标计数直接套给新目标。");
        _test.False(otherTarget.HasStatusEffect(SlowStatusId), "新目标第一次命中不应继承旧目标 slow。");
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

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName creatureTag,
        int hp
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
        if (creatureTag != "")
            unit.creature_type_tags.Add(creatureTag);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private sealed class FrostMaceFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private FrostMaceFixture(
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

        internal static FrostMaceFixture Build(GArray damageRolls)
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
            return new FrostMaceFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildFrostMaceUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                FrostMaceItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(FrostMaceItemId, $"eq_frost_mace_{label}")
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

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_scorpion_bow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ScorpionItemId = "weapon_unique_bow_scorpion_339";
    private static readonly StringName ScorpionArrowTraitId =
        "weapon.bow.scorpion.scorpion_arrow";
    private static readonly StringName PoisonImmunityTraitId =
        "weapon.bow.scorpion.poison_immunity";
    private static readonly StringName ScorpionArrowBindingId =
        "binding.weapon.bow.scorpion.scorpion_arrow";
    private static readonly StringName PoisonBloodTraitId =
        "weapon.bow.scorpion.poison_blood";
    private static readonly StringName PoisonBloodBindingId =
        "binding.weapon.bow.scorpion.poison_blood";
    private static readonly StringName PoisonedStatusId = "poisoned";

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
            TestScorpionContentLoadsAndProjectsWeaponAndPoisonImmunity();
            TestScorpionArrowAddsPoisonDamageOnRealWeaponHit();
            TestScorpionArrowParalyzesOnFailedConSaveAndSkipsOnSuccess();
            TestPoisonBloodPoisonsAdjacentAttackerOnFailedSave();
            RequestTestExit(_test.Finish("Scorpion Bow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Scorpion Bow weapon ability regression"));
        }
    }

    private void TestScorpionContentLoadsAndProjectsWeaponAndPoisonImmunity()
    {
        using ScorpionFixture fixture = ScorpionFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ScorpionItemId), "真实物品内容应包含蝎子之弓。");
        _test.True(
            fixture.TraitDefs.ContainsKey(ScorpionArrowTraitId),
            "真实 trait 内容应包含蝎毒箭。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(PoisonImmunityTraitId),
            "真实 trait 内容应包含毒素免疫。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ScorpionArrowBindingId),
            "真实装备能力内容应包含蝎毒箭 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(ScorpionItemId))
            return;

        ItemDef rawScorpion = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_shortbow_scorpion.tres"
        );
        _test.True(rawScorpion != null, "蝎子之弓原始资源应能加载。");
        if (rawScorpion != null)
        {
            _test.Eq(rawScorpion.display_name, "蝎子之弓", "蝎子之弓显示名应来自设计源。");
            _test.Eq(
                rawScorpion.base_item_id,
                new StringName("weapon_type_shortbow_base"),
                "蝎子之弓应继承 shortbow 模板。"
            );
            _test.Eq(rawScorpion.base_price, 38000, "蝎子之弓基础价格应为 38000。");
            WeaponProfileDef rawProfile = rawScorpion.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "蝎子之弓应声明武器 profile 覆写。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.training_group, new StringName("martial"), "蝎子之弓训练组应为 martial。");
                _test.Eq(rawProfile.attack_range, 6, "蝎子之弓攻击距离应为 6。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        _test.False(
            HasDamageMitigation(baseline, "poison"),
            "未装备蝎子之弓时不应拥有 poison damage immune。"
        );
        _test.False(
            baseline.HasSaveImmunityTag("poison"),
            "未装备蝎子之弓时不应拥有 poison save immunity。"
        );

        BattleUnitState equipped = fixture.BuildScorpionUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, ScorpionItemId, "蝎子之弓装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("shortbow"), "蝎子之弓应投影为 shortbow。");
        _test.Eq(equippedWeapon.Family, new StringName("bow"), "蝎子之弓应保留 bow 家族。");
        _test.Eq(
            equippedWeapon.PhysicalDamageTag,
            new StringName("physical_pierce"),
            "蝎子之弓基础伤害标签应为 physical_pierce。"
        );
        _test.Eq(equippedWeapon.AttackRange, 6, "蝎子之弓攻击距离应为 6。");
        _test.True(equippedWeapon.UsesTwoHands, "蝎子之弓应保留 two_handed 投影。");
        _test.False(equippedWeapon.IsVersatile, "蝎子之弓不应投影为 versatile。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceCount, 1, "蝎子之弓应为 1D6+2。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceSides, 6, "蝎子之弓应为 1D6+2。");
        _test.Eq(equippedWeapon.TwoHandedDice.FlatBonus, 2, "蝎子之弓应为 1D6+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ScorpionArrowTraitId,
            ScorpionArrowBindingId,
            "eq_scorpion_projection"
        );
        _test.True(
            equipped.HasEffectiveTrait(PoisonImmunityTraitId),
            "毒素免疫 trait 应作为固定装备 trait 投影到战斗单位。"
        );
        _test.Eq(
            GetDamageMitigation(equipped, "poison"),
            new StringName("immune"),
            "毒素免疫 trait 应投影 poison damage immune。"
        );
        _test.True(
            equipped.HasSaveImmunityTag("poison"),
            "毒素免疫 trait 应投影 poison save immunity。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleWeaponProjectionValues removedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(removedWeapon.ItemId, new StringName(""), "移除蝎子之弓后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除蝎子之弓后装备能力源应清空。"
        );
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除蝎子之弓后装备 trait 实例应回到装备前状态。"
        );
        _test.False(
            HasDamageMitigation(equipped, "poison"),
            "移除蝎子之弓后 poison damage immune 不应残留。"
        );
        _test.False(
            equipped.HasSaveImmunityTag("poison"),
            "移除蝎子之弓后 poison save immunity 不应残留。"
        );
    }

    private void TestScorpionArrowAddsPoisonDamageOnRealWeaponHit()
    {
        using ScorpionFixture fixture = ScorpionFixture.Build(new GArray { 4, 8 });
        BattleUnitState attacker = fixture.BuildScorpionUnit("poison_damage");
        BattleUnitState target = BuildTarget("poison_damage_target", new Vector2I(1, 0));
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "scorpion_bow_poison_damage",
            previewCommand: false
        );
        int scorpionDamage = 100 - target.GetCurrentHp();

        using ScorpionFixture plainFixture = ScorpionFixture.Build(new GArray { 4, 8 });
        BattleUnitState plainAttacker = plainFixture.BuildScorpionUnit("plain_damage");
        plainAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainTarget = BuildTarget("plain_damage_target", new Vector2I(1, 0));
        plainTarget.SetCurrentHp(100);
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        plainTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "scorpion_bow_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.GetCurrentHp();

        _test.Eq(plainDamage, 6, "固定骰 4 时，蝎子之弓基础武器伤害应为 1D6+2。");
        _test.Eq(
            scorpionDamage,
            14,
            "蝎毒箭应在真实命中后额外造成 1D8 poison，且不吞掉武器伤害。"
        );
    }

    private void TestScorpionArrowParalyzesOnFailedConSaveAndSkipsOnSuccess()
    {
        using ScorpionFixture fixture = ScorpionFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildScorpionUnit("paralyze");

        BattleUnitState failedTarget = BuildTarget("paralyze_failed", new Vector2I(1, 0));
        BattleState failedState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "scorpion_bow_paralyze_failed",
            attacker,
            failedTarget
        );
        fixture.Runtime.SetupStateForTests(failedState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = failedTarget,
                BattleState = failedState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );

        BattleStatusEffectState paralyzed = failedTarget.GetStatusEffect("paralyzed");
        _test.True(paralyzed != null, "蝎毒箭应在 DC15 constitution/poison 豁免失败后施加 paralyzed。");
        _test.Eq(paralyzed?.duration ?? -1, 60, "paralyzed 应持续 60 TU。");

        BattleUnitState successTarget = BuildTarget("paralyze_success", new Vector2I(1, 0));
        BattleState successState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "scorpion_bow_paralyze_success",
            attacker,
            successTarget
        );
        fixture.Runtime.SetupStateForTests(successState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = successTarget,
                BattleState = successState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(20),
            }
        );

        _test.False(
            successTarget.HasStatusEffect("paralyzed"),
            "蝎毒箭在 DC15 constitution/poison 豁免成功时不应施加 paralyzed。"
        );
    }

    private void TestPoisonBloodPoisonsAdjacentAttackerOnFailedSave()
    {
        using ScorpionFixture fixture = ScorpionFixture.Build(new GArray());
        _test.True(
            fixture.TraitDefs.ContainsKey(PoisonBloodTraitId),
            "真实 trait 内容应包含毒血。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(PoisonBloodBindingId),
            "真实装备能力内容应包含毒血 binding。"
        );

        BattleUnitState holder = fixture.BuildScorpionUnit("poison_blood");
        AssertUnitHasTraitAndAbilitySource(
            holder,
            PoisonBloodTraitId,
            PoisonBloodBindingId,
            "eq_scorpion_poison_blood"
        );

        BattleUnitState meleeAttacker = BuildTarget("poison_blood_melee", new Vector2I(1, 0));
        BattleState meleeState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "scorpion_poison_blood_melee",
            holder,
            meleeAttacker
        );
        fixture.Runtime.SetupStateForTests(meleeState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = holder,
                TargetUnit = meleeAttacker,
                BattleState = meleeState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );
        BattleStatusEffectState poisoned = meleeAttacker.GetStatusEffect(PoisonedStatusId);
        _test.True(poisoned != null, "相邻攻击者命中持有者且 DC14 豁免失败后应中毒。");
        if (poisoned != null)
        {
            _test.Eq(poisoned.duration, 60, "中毒应持续 60 TU。");
            _test.Eq(poisoned.source_unit_id, holder.unit_id, "中毒应记录持有者来源。");
            _test.Eq(
                BattleStatusSemanticTable.GetAttackRollPenalty(poisoned),
                2,
                "中毒应使攻击检定 -2。"
            );
        }

        BattleUnitState luckyAttacker = BuildTarget("poison_blood_lucky", new Vector2I(1, 0));
        BattleState luckyState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "scorpion_poison_blood_lucky",
            holder,
            luckyAttacker
        );
        fixture.Runtime.SetupStateForTests(luckyState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = holder,
                TargetUnit = luckyAttacker,
                BattleState = luckyState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(20),
            }
        );
        _test.False(
            luckyAttacker.HasStatusEffect(PoisonedStatusId),
            "DC14 豁免成功的攻击者不应中毒。"
        );

        BattleUnitState rangedAttacker = BuildTarget("poison_blood_ranged", new Vector2I(3, 0));
        BattleState rangedState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "scorpion_poison_blood_ranged",
            holder,
            rangedAttacker
        );
        fixture.Runtime.SetupStateForTests(rangedState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = holder,
                TargetUnit = rangedAttacker,
                BattleState = rangedState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );
        _test.False(
            rangedAttacker.HasStatusEffect(PoisonedStatusId),
            "距离超过 1 格的攻击者不应被毒血触及（血液接触限相邻）。"
        );
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
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
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

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
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

    private sealed class ScorpionFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private ScorpionFixture(
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

        internal static ScorpionFixture Build(GArray damageRolls)
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
            return new ScorpionFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildScorpionUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ScorpionItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    ScorpionItemId,
                    $"eq_scorpion_{label}"
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

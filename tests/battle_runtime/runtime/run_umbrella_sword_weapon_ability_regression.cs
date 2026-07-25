using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_umbrella_sword_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName UmbrellaItemId = "weapon_unique_exotic_umbrella_232";
    private static readonly StringName RainScreenTraitId =
        "weapon.exotic.umbrella.rain_screen";
    private static readonly StringName GuardTraitId = "weapon.exotic.umbrella.guard";
    private static readonly StringName RainAdvantageTraitId =
        "weapon.exotic.umbrella.rain_advantage";
    private static readonly StringName RainScreenBindingId =
        "binding.weapon.exotic.umbrella.rain_screen";
    private static readonly StringName GuardBindingId =
        "binding.weapon.exotic.umbrella.guard";
    private static readonly StringName RainAdvantageBindingId =
        "binding.weapon.exotic.umbrella.rain_advantage";

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
            TestUmbrellaContentLoadsAndProjectsWeaponTraitsAndRangeType();
            TestRainScreenReducesFireColdDamageThroughRealDamageResolver();
            TestRainAdvantageAndGuardUseEnvironmentAndRangedWeaponConfig();
            RequestTestExit(_test.Finish("Umbrella Sword weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Umbrella Sword weapon ability regression"));
        }
    }

    private void TestUmbrellaContentLoadsAndProjectsWeaponTraitsAndRangeType()
    {
        using UmbrellaFixture fixture = UmbrellaFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(UmbrellaItemId), "真实物品内容应包含伞剑。");
        _test.True(fixture.TraitDefs.ContainsKey(RainScreenTraitId), "真实 trait 应包含雨幕伞面。");
        _test.True(fixture.TraitDefs.ContainsKey(GuardTraitId), "真实 trait 应包含格挡。");
        _test.True(fixture.TraitDefs.ContainsKey(RainAdvantageTraitId), "真实 trait 应包含雨天优势。");
        _test.True(fixture.Bindings.ContainsKey(RainScreenBindingId), "真实装备能力内容应包含雨幕伞面 binding。");
        _test.True(fixture.Bindings.ContainsKey(GuardBindingId), "真实装备能力内容应包含格挡 binding。");
        _test.True(fixture.Bindings.ContainsKey(RainAdvantageBindingId), "真实装备能力内容应包含雨天优势 binding。");
        if (!fixture.ItemDefs.ContainsKey(UmbrellaItemId))
            return;

        ItemDef rawUmbrella = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_rapier_umbrella.tres"
        );
        GodotContentOwnership.RegisterBorrowedContent(
            rawUmbrella,
            "umbrella_sword_weapon_ability_regression:raw_item"
        );
        _test.True(rawUmbrella != null, "伞剑原始资源应能加载。");
        if (rawUmbrella != null)
        {
            _test.Eq(rawUmbrella.display_name, "伞剑", "伞剑显示名应来自设计源。");
            _test.Eq(rawUmbrella.base_item_id, new StringName("weapon_type_rapier_base"), "伞剑应继承 rapier 模板。");
            _test.Eq(rawUmbrella.base_price, 35000, "伞剑基础价格应为 35000。");
            WeaponProfileDef rawProfile = rawUmbrella.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "伞剑应声明武器 profile 覆写。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.training_group, new StringName("martial"), "伞剑训练组应为 martial。");
                _test.Eq(rawProfile.range_type, new StringName("melee"), "伞剑自身应是 melee weapon。");
                _test.Eq(rawProfile.attack_range, 1, "伞剑攻击距离应为 1。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildUmbrellaUnit("projection");
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(equippedWeapon.ItemId, UmbrellaItemId, "伞剑装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("rapier"), "伞剑应投影为 rapier。");
        _test.Eq(equippedWeapon.RangeType, new StringName("melee"), "伞剑应投影 weapon range_type。");
        _test.Eq(equippedWeapon.Family, new StringName("exotic"), "伞剑应保留 exotic 家族。");
        _test.Eq(equippedWeapon.AttackRange, 1, "伞剑攻击距离应为 1。");
        _test.Eq(equippedWeapon.PhysicalDamageTag, new StringName("physical_pierce"), "伞剑基础伤害标签应为 physical_pierce。");
        _test.False(equippedWeapon.UsesTwoHands, "伞剑应是单手武器。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "伞剑应为 1D8+1。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 8, "伞剑应为 1D8+1。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 1, "伞剑应为 1D8+1。");
        AssertUnitHasTraitAndAbilitySource(equipped, RainScreenTraitId, RainScreenBindingId, "eq_umbrella_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, GuardTraitId, GuardBindingId, "eq_umbrella_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, RainAdvantageTraitId, RainAdvantageBindingId, "eq_umbrella_projection");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleWeaponProjectionValues removedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(removedWeapon.ItemId, new StringName(""), "移除伞剑后 weapon_item_id 应清空。");
        _test.Eq(removedWeapon.ProfileTypeId, baselineWeapon.ProfileTypeId, "移除伞剑后 weapon profile 应回到装备前状态。");
        _test.Eq(removedWeapon.RangeType, baselineWeapon.RangeType, "移除伞剑后 weapon range_type 应回到装备前状态。");
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除伞剑后装备能力源应清空。"
        );
        _test.Eq(equipped.GetEffectiveTraitInstanceCountTyped(), baseline.GetEffectiveTraitInstanceCountTyped(), "移除伞剑后装备 trait 实例应回到装备前状态。");
    }

    private void TestRainScreenReducesFireColdDamageThroughRealDamageResolver()
    {
        using UmbrellaFixture fixture = UmbrellaFixture.Build();

        BattleUnitState clearSource = BuildAttacker("clear_fire_source", new Vector2I(1, 0), "enemy");
        BattleUnitState clearTarget = fixture.BuildUmbrellaUnit("clear_fire");
        GDictionary clearEvent;
        int clearDamage = ResolveDamage(
            fixture,
            clearSource,
            clearTarget,
            "umbrella_clear_fire",
            new GStringNameArray(),
            "fire",
            out clearEvent
        );
        _test.Eq(clearDamage, 8, "雨幕伞面非雨天应使 fire 10 点伤害减为 8。");
        _test.Eq(DictInt(clearEvent, "fixed_mitigation_total", -1), 2, "非雨天固定减免应为 2。");
        _test.True(FixedSourcesInclude(clearEvent, RainScreenBindingId), "雨幕伞面应进入固定减免来源。");

        BattleUnitState rainSource = BuildAttacker("rain_fire_source", new Vector2I(1, 0), "enemy");
        BattleUnitState rainTarget = fixture.BuildUmbrellaUnit("rain_fire");
        GDictionary rainEvent;
        int rainDamage = ResolveDamage(
            fixture,
            rainSource,
            rainTarget,
            "umbrella_rain_fire",
            new GStringNameArray { "rain" },
            "fire",
            out rainEvent
        );
        _test.Eq(rainDamage, 6, "雨天时雨幕伞面应使 fire 10 点伤害减为 6。");
        _test.Eq(DictInt(rainEvent, "fixed_mitigation_total", -1), 4, "雨天固定减免应为 4。");

        BattleUnitState wetSource = BuildAttacker("wet_cold_source", new Vector2I(1, 0), "enemy");
        BattleUnitState wetTarget = fixture.BuildUmbrellaUnit("wet_cold");
        GDictionary wetEvent;
        int wetDamage = ResolveDamage(
            fixture,
            wetSource,
            wetTarget,
            "umbrella_wet_cold",
            new GStringNameArray { "wet" },
            "freeze",
            out wetEvent
        );
        _test.Eq(wetDamage, 6, "潮湿环境时雨幕伞面应同样使 freeze 10 点伤害减为 6。");
        _test.Eq(DictInt(wetEvent, "fixed_mitigation_total", -1), 4, "潮湿环境固定减免应为 4。");

        BattleUnitState forceSource = BuildAttacker("force_source", new Vector2I(1, 0), "enemy");
        BattleUnitState forceTarget = fixture.BuildUmbrellaUnit("force_target");
        GDictionary forceEvent;
        int forceDamage = ResolveDamage(
            fixture,
            forceSource,
            forceTarget,
            "umbrella_force",
            new GStringNameArray { "rain" },
            "force",
            out forceEvent
        );
        _test.Eq(forceDamage, 10, "雨幕伞面不应减免非 fire/freeze 伤害。");
        _test.Eq(DictInt(forceEvent, "fixed_mitigation_total", -1), 0, "非匹配伤害类型固定减免应为 0。");
    }

    private void TestRainAdvantageAndGuardUseEnvironmentAndRangedWeaponConfig()
    {
        using UmbrellaFixture fixture = UmbrellaFixture.Build();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();

        BattleUnitState umbrellaAttacker = fixture.BuildUmbrellaUnit("rain_advantage");
        BattleUnitState adjacentTarget = BuildTarget("rain_advantage_target", new Vector2I(1, 0));
        BattleState rainState = BuildStateWithEnvironmentTags(
            "umbrella_rain_advantage",
            umbrellaAttacker,
            adjacentTarget,
            new GStringNameArray { "rain" }
        );
        fixture.Runtime.SetupStateForTests(rainState);
        BattleAttackRollModifierBundle rainBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                rainState,
                umbrellaAttacker,
                adjacentTarget,
                attackSkill,
                "skill_attack_check",
                "umbrella_rain_advantage",
                force_hit_no_crit: false
            )
        );
        _test.Eq(rainBundle.GetEffectiveModifierDelta(), 2, "雨天优势应在 rain/wet 环境给伞剑攻击 +2。");
        _test.True(HasModifier(rainBundle, RainAdvantageBindingId, 2), "雨天优势 +2 应进入 modifier breakdown。");

        BattleUnitState clearUmbrellaAttacker = fixture.BuildUmbrellaUnit("clear_advantage");
        BattleUnitState clearAdjacentTarget = BuildTarget("clear_advantage_target", new Vector2I(1, 0));
        BattleState clearState = BuildStateWithEnvironmentTags(
            "umbrella_clear_advantage",
            clearUmbrellaAttacker,
            clearAdjacentTarget,
            new GStringNameArray()
        );
        fixture.Runtime.SetupStateForTests(clearState);
        BattleAttackRollModifierBundle clearBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                clearState,
                clearUmbrellaAttacker,
                clearAdjacentTarget,
                attackSkill,
                "skill_attack_check",
                "umbrella_clear_advantage",
                force_hit_no_crit: false
            )
        );
        _test.Eq(clearBundle.GetEffectiveModifierDelta(), 0, "非 rain/wet 环境不应获得雨天优势 +2。");

        BattleUnitState holder = fixture.BuildUmbrellaUnit("guard");
        BattleUnitState rangedAttacker = BuildAttacker("ranged_attacker", new Vector2I(3, 0), "enemy");
        ApplyTestWeaponProjection(rangedAttacker, "ranged", 6);
        BattleState rangedState = BuildStateWithEnvironmentTags(
            "umbrella_guard_ranged",
            rangedAttacker,
            holder,
            new GStringNameArray()
        );
        fixture.Runtime.SetupStateForTests(rangedState);
        BattleAttackRollModifierBundle rangedGuardBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                rangedState,
                rangedAttacker,
                holder,
                attackSkill,
                "skill_attack_check",
                "umbrella_guard_ranged",
                force_hit_no_crit: false
            )
        );
        _test.Eq(rangedGuardBundle.GetEffectiveModifierDelta(), -2, "格挡应只在 ranged 攻击持有者时等价为攻击者 -2。");
        _test.True(HasModifier(rangedGuardBundle, GuardBindingId, -2), "格挡 -2 应进入 modifier breakdown。");

        BattleUnitState meleeHolder = fixture.BuildUmbrellaUnit("guard_melee");
        BattleUnitState meleeAttacker = BuildAttacker("melee_attacker", new Vector2I(1, 0), "enemy");
        ApplyTestWeaponProjection(meleeAttacker, "melee", 1);
        BattleState meleeState = BuildStateWithEnvironmentTags(
            "umbrella_guard_melee",
            meleeAttacker,
            meleeHolder,
            new GStringNameArray()
        );
        fixture.Runtime.SetupStateForTests(meleeState);
        BattleAttackRollModifierBundle meleeGuardBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                meleeState,
                meleeAttacker,
                meleeHolder,
                attackSkill,
                "skill_attack_check",
                "umbrella_guard_melee",
                force_hit_no_crit: false
            )
        );
        _test.Eq(meleeGuardBundle.GetEffectiveModifierDelta(), 0, "melee 攻击不应触发伞面格挡远程修正。");
    }

    private static int ResolveDamage(
        UmbrellaFixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        StringName battleId,
        GStringNameArray environmentTags,
        StringName damageTag,
        out GDictionary firstDamageEvent
    )
    {
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleState state = BuildStateWithEnvironmentTags(
            battleId,
            source,
            target,
            environmentTags
        );
        fixture.Runtime.SetupStateForTests(state);
        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
                fixture.Runtime.GetDamageResolver().ResolveEffects(
                source,
                target,
                new[] { MakeDamageEffect(damageTag, 10) },
                DamageResolutionContext.Empty()
            )
        );
        GDictionary result = resultLease.Value;
        firstDamageEvent = FirstDamageEvent(result);
        return DictInt(result, "damage", -1);
    }

    private static CombatEffectDefinition MakeDamageEffect(StringName damageTag, int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag,
            power: power
        );

    private static BattleState BuildStateWithEnvironmentTags(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        GStringNameArray environmentTags
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target,
            mapSize: new Vector2I(6, 6)
        );
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["global_environment_tags"] = environmentTags ?? new GStringNameArray() }
            )
        );
        return state;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord) =>
        BuildAttacker(unitId, coord, "enemy");

    private static BattleUnitState BuildAttacker(StringName unitId, Vector2I coord, StringName factionId)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: 100,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
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

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private static bool FixedSourcesInclude(GDictionary damageEvent, StringName expected)
    {
        if (damageEvent == null || !damageEvent.ContainsKey("fixed_mitigation_sources"))
            return false;
        foreach (Variant value in damageEvent["fixed_mitigation_sources"].AsGodotArray())
        {
            if (
                value.VariantType == Variant.Type.Dictionary
                && value.AsGodotDictionary().ContainsKey("status_id")
                && value.AsGodotDictionary()["status_id"].AsString() == expected.ToString()
            )
            {
                return true;
            }
            if (value.AsString() == expected.ToString())
                return true;
        }
        return false;
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        if (result == null || !result.ContainsKey("damage_events"))
            return new GDictionary();
        GArray events = result["damage_events"].AsGodotArray();
        return events.Count > 0 ? events[0].AsGodotDictionary() : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static void ApplyTestWeaponProjection(
        BattleUnitState unit,
        StringName rangeType,
        int attackRange
    )
    {
        bool usesTwoHands = rangeType == "ranged";
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = usesTwoHands ? "equipped" : "unarmed",
                weapon_item_id = usesTwoHands ? "umbrella_test_bow" : "",
                weapon_profile_type_id = usesTwoHands ? "longbow" : "unarmed",
                weapon_range_type = rangeType,
                weapon_family = usesTwoHands ? "bow" : "unarmed",
                weapon_current_grip = usesTwoHands ? "two_handed" : "one_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = usesTwoHands
                    ? new WeaponDice()
                    : new WeaponDice { dice_count = 1, dice_sides = 4 },
                weapon_two_handed_dice = usesTwoHands
                    ? new WeaponDice { dice_count = 1, dice_sides = 8 }
                    : new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = usesTwoHands,
                weapon_physical_damage_tag = usesTwoHands
                    ? "physical_pierce"
                    : "physical_blunt",
            }
        );
    }

    private sealed class UmbrellaFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private UmbrellaFixture(
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

        internal static UmbrellaFixture Build()
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
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new UmbrellaFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildUmbrellaUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                UmbrellaItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    UmbrellaItemId,
                    $"eq_umbrella_{label}"
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

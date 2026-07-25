using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_titanbow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName TitanbowItemId = "weapon_unique_bow_titanbow_173";
    private static readonly StringName LongbowItemId = "ash_longbow";
    private static readonly StringName BehemothSlayerTraitId =
        "weapon.bow.titanbow.behemoth_slayer";
    private static readonly StringName ScalePiercerTraitId =
        "weapon.bow.titanbow.scale_piercer";
    private static readonly StringName StrengthRequirementTraitId =
        "weapon.bow.titanbow.strength_requirement";
    private static readonly StringName BehemothSlayerBindingId =
        "binding.weapon.bow.titanbow.behemoth_slayer";
    private static readonly StringName ScalePiercerBindingId =
        "binding.weapon.bow.titanbow.scale_piercer";
    private static readonly StringName StrengthRequirementBindingId =
        "binding.weapon.bow.titanbow.strength_requirement";
    private static readonly StringName NaturalArmorAcBonus = "natural_armor_ac_bonus";

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
            TestTitanbowContentProjectsTraitsAndAbilitySources();
            TestTitanbowAddsDamageDiceAgainstLargeOrLargerTargetsOnly();
            TestTitanbowStrengthRequirementAppliesAttackCheckPenaltyFromAbilityConfig();
            TestTitanbowScalePiercerIgnoresOnlyNaturalArmorForThisAttackCheck();
            RequestTestExit(_test.Finish("Titanbow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Titanbow weapon ability regression"));
        }
    }

    private void TestTitanbowContentProjectsTraitsAndAbilitySources()
    {
        using TitanbowFixture fixture = TitanbowFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(TitanbowItemId), "真实物品内容应包含泰坦之弓。");
        _test.True(
            fixture.TraitDefs.ContainsKey(BehemothSlayerTraitId),
            "真实 trait 内容应包含巨兽杀手。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(ScalePiercerTraitId),
            "真实 trait 内容应包含穿透鳞甲。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StrengthRequirementTraitId),
            "真实 trait 内容应包含力量需求。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(BehemothSlayerBindingId),
            "真实装备能力内容应包含巨兽杀手 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ScalePiercerBindingId),
            "真实装备能力内容应包含穿透鳞甲 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StrengthRequirementBindingId),
            "真实装备能力内容应包含力量需求 binding。"
        );
        _test.True(
            ContainsAcComponent(NaturalArmorAcBonus),
            "natural_armor_ac_bonus 应是正式 AC component。"
        );
        if (!fixture.ItemDefs.ContainsKey(TitanbowItemId))
            return;

        ItemDef rawTitanbow = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longbow_titanbow.tres"
        );
        _test.True(rawTitanbow != null, "泰坦之弓原始资源应能加载。");
        if (rawTitanbow != null)
        {
            _test.Eq(
                rawTitanbow.base_item_id,
                new StringName("weapon_type_longbow_base"),
                "泰坦之弓原始资源应声明继承 longbow 模板。"
            );
            _test.Eq(rawTitanbow.trait_ids.Count, 3, "泰坦之弓应固定声明三个 weapon trait。");
            _test.True(
                rawTitanbow.trait_ids.Contains(BehemothSlayerTraitId),
                "泰坦之弓应固定声明巨兽杀手 trait。"
            );
            _test.True(
                rawTitanbow.trait_ids.Contains(ScalePiercerTraitId),
                "泰坦之弓应固定声明穿透鳞甲 trait。"
            );
            _test.True(
                rawTitanbow.trait_ids.Contains(StrengthRequirementTraitId),
                "泰坦之弓应固定声明力量需求 trait。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildTitanbowUnit("projection", strength: 18);
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(equippedWeapon.ItemId, TitanbowItemId, "泰坦之弓装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            new StringName("longbow"),
            "泰坦之弓应投影为 longbow。"
        );
        _test.Eq(equippedWeapon.AttackRange, 15, "泰坦之弓攻击距离应为 15。");
        _test.True(equippedWeapon.UsesTwoHands, "泰坦之弓应占用双手。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceCount, 1, "泰坦之弓应为 1D8+4。");
        _test.Eq(equippedWeapon.TwoHandedDice.DiceSides, 8, "泰坦之弓应为 1D8+4。");
        _test.Eq(equippedWeapon.TwoHandedDice.FlatBonus, 4, "泰坦之弓应为 1D8+4。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            BehemothSlayerTraitId,
            BehemothSlayerBindingId,
            "eq_titanbow_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ScalePiercerTraitId,
            ScalePiercerBindingId,
            "eq_titanbow_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StrengthRequirementTraitId,
            StrengthRequirementBindingId,
            "eq_titanbow_projection"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, ScalePiercerBindingId, "attack_defense_modifier"),
            "穿透鳞甲必须由 attack_defense_modifier action 配置声明。"
        );
        _test.True(
            BindingHasAttackRollPenalty(fixture.Bindings, StrengthRequirementBindingId, -4),
            "力量需求必须由装备能力 attack_roll_bonus=-4 条件配置声明。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleWeaponProjectionValues removedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(removedWeapon.ItemId, new StringName(""), "移除泰坦之弓后 weapon_item_id 应清空。");
        _test.Eq(
            removedWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除泰坦之弓后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除泰坦之弓后装备能力源应清空。"
        );
    }

    private void TestTitanbowAddsDamageDiceAgainstLargeOrLargerTargetsOnly()
    {
        using TitanbowFixture largeFixture = TitanbowFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState largeAttacker = largeFixture.BuildTitanbowUnit("damage_large", strength: 18);
        BattleUnitState largeTarget = BuildTarget(
            "large_target",
            new Vector2I(1, 0),
            bodySize: 3,
            bodySizeCategory: "large"
        );
        largeTarget.SetCurrentHp(100);
        largeTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            largeFixture.Runtime,
            largeAttacker,
            largeTarget,
            "titanbow_damage_large",
            previewCommand: false
        );
        _test.Eq(
            100 - largeTarget.GetCurrentHp(),
            16,
            "泰坦之弓真实基础攻击命中 Large+ 目标时应造成 1D8+4 加巨兽杀手 2D8。"
        );

        using TitanbowFixture mediumFixture = TitanbowFixture.Build(new GArray { 4, 3, 5 });
        BattleUnitState mediumAttacker = mediumFixture.BuildTitanbowUnit("damage_medium", strength: 18);
        BattleUnitState mediumTarget = BuildTarget(
            "medium_target",
            new Vector2I(1, 0),
            bodySize: 2,
            bodySizeCategory: "medium"
        );
        mediumTarget.SetCurrentHp(100);
        mediumTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            mediumFixture.Runtime,
            mediumAttacker,
            mediumTarget,
            "titanbow_damage_medium",
            previewCommand: false
        );
        _test.Eq(
            100 - mediumTarget.GetCurrentHp(),
            8,
            "泰坦之弓真实基础攻击命中 Medium 目标时只应造成武器 1D8+4。"
        );
    }

    private void TestTitanbowStrengthRequirementAppliesAttackCheckPenaltyFromAbilityConfig()
    {
        using TitanbowFixture fixture = TitanbowFixture.Build();
        BattleUnitState weak = fixture.BuildTitanbowUnit("strength_17", strength: 17);
        BattleUnitState strong = fixture.BuildTitanbowUnit("strength_18", strength: 18);
        BattleUnitState target = BuildTarget("strength_target", new Vector2I(1, 0));
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();

        AttackCheckInput weakCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                weak,
                target,
                attackSkill,
                "skill_attack_check",
                "titanbow_strength_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput strongCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                strong,
                target,
                attackSkill,
                "skill_attack_check",
                "titanbow_strength_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );

        _test.Eq(weakCheck.SituationalAttackPenalty, 4, "力量 17 使用泰坦之弓应有 -4 攻击检定惩罚。");
        _test.Eq(strongCheck.SituationalAttackPenalty, 0, "力量 18 使用泰坦之弓不应有攻击检定惩罚。");
        _test.Eq(
            weakCheck.RequiredRoll,
            strongCheck.RequiredRoll + 4,
            "力量不足惩罚应真实进入本次 AttackCheckInput。"
        );
    }

    private void TestTitanbowScalePiercerIgnoresOnlyNaturalArmorForThisAttackCheck()
    {
        using TitanbowFixture fixture = TitanbowFixture.Build();
        BattleUnitState titanbow = fixture.BuildTitanbowUnit("scale_piercer", strength: 18);
        BattleUnitState ordinaryLongbow = fixture.BuildLongbowUnit("ordinary_longbow", strength: 18);
        BattleUnitState target = BuildTargetWithAcComponents("scaled_target", new Vector2I(1, 0));
        int originalArmorClass = target.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS);
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();

        AttackCheckInput titanbowCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                titanbow,
                target,
                attackSkill,
                "skill_attack_check",
                "titanbow_scale_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput ordinaryCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                ordinaryLongbow,
                target,
                attackSkill,
                "skill_attack_check",
                "ordinary_longbow_scale_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );

        _test.Eq(ordinaryCheck.TargetArmorClass, 20, "普通长弓不应忽略目标自然护甲。");
        _test.Eq(
            titanbowCheck.TargetArmorClass,
            16,
            "泰坦之弓本次攻击检查只应忽略 natural_armor_ac_bonus=4。"
        );
        _test.Eq(
            target.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS),
            originalArmorClass,
            "穿透鳞甲不应改写目标永久 armor_class snapshot。"
        );
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        int bodySize = 2,
        StringName bodySizeCategory = default
    )
    {
        StringName resolvedBodySizeCategory =
            bodySizeCategory == default || bodySizeCategory == ""
                ? new StringName("medium")
                : bodySizeCategory;
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: 30,
            isAlive: true
        );
        if (
            !unit.SetBodySizeProjection(bodySize)
            || unit.GetBodySizeCategory() != resolvedBodySizeCategory
        )
        {
            throw new InvalidOperationException(
                $"测试目标体型参数不一致: body_size={bodySize}, "
                + $"body_size_category='{resolvedBodySizeCategory}'。"
            );
        }
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleUnitState BuildTargetWithAcComponents(StringName unitId, Vector2I coord)
    {
        BattleUnitState target = BuildTarget(unitId, coord);
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 20);
        target.attribute_snapshot.SetValue(NaturalArmorAcBonus, 4);
        target.attribute_snapshot.SetValue(AttributeContentRules.ArmorAcBonus, 3);
        target.attribute_snapshot.SetValue(AttributeContentRules.ShieldAcBonus, 2);
        target.attribute_snapshot.SetValue(AttributeContentRules.DodgeBonus, 1);
        target.attribute_snapshot.SetValue(AttributeContentRules.DeflectionBonus, 2);
        return target;
    }

    private static bool ContainsAcComponent(StringName componentId)
    {
        return AttributeContentRules.IsArmorClassComponentAttributeId(componentId);
    }

    private static bool BindingHasActionKind(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        StringName actionKind
    )
    {
        if (bindings == null || !bindings.TryGetValue(bindingId, out var binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.Kind == actionKind)
                    return true;
            }
        }
        return false;
    }

    private static bool BindingHasAttackRollPenalty(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        int expectedBonus
    )
    {
        if (bindings == null || !bindings.TryGetValue(bindingId, out var binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.Kind == "attack_roll_bonus"
                    && action.PayloadDefinition is AttackRollBonusActionPayloadDefinition payload
                    && payload.Bonus == expectedBonus
                )
                {
                    return true;
                }
            }
        }
        return false;
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
            BattleEquipmentAbilitySourceReadView source in
            unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private sealed class TitanbowFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private TitanbowFixture(
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

        internal static TitanbowFixture Build(GArray damageRolls = null)
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
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls ?? new GArray { 4, 3, 5 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new TitanbowFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildTitanbowUnit(string label, int strength)
        {
            return BuildEquippedUnit(label, TitanbowItemId, $"eq_titanbow_{label}", strength);
        }

        internal BattleUnitState BuildLongbowUnit(string label, int strength)
        {
            return BuildEquippedUnit(label, LongbowItemId, $"eq_longbow_{label}", strength);
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _characterManagement?.Dispose();
        }

        private BattleUnitState BuildEquippedUnit(
            string label,
            StringName itemId,
            StringName instanceId,
            int strength
        )
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                itemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(itemId, instanceId)
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue("strength", strength);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
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

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_bonecrusher_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName BonecrusherItemId =
        "weapon_unique_axe_bonecrusher_088";
    private static readonly StringName RaiderGreataxeItemId = "raider_greataxe";
    private static readonly StringName ArmorCrushingTraitId =
        "weapon.axe.bonecrusher.armor_crushing_blow";
    private static readonly StringName BoneShatterTraitId =
        "weapon.axe.bonecrusher.bone_shatter";
    private static readonly StringName AftershockFractureTraitId =
        "weapon.axe.bonecrusher.aftershock_fracture";
    private static readonly StringName ArmorCrushingBindingId =
        "binding.weapon.axe.bonecrusher.armor_crushing_blow";
    private static readonly StringName BoneShatterBindingId =
        "binding.weapon.axe.bonecrusher.bone_shatter";
    private static readonly StringName AftershockFractureBindingId =
        "binding.weapon.axe.bonecrusher.aftershock_fracture";
    private static readonly StringName FracturedDefenseStatusId =
        "bonecrusher_fractured_defense";
    private static readonly StringName TimeSlowStatusId = "time_slow";
    private static readonly StringName NaturalArmorAcBonus = "natural_armor_ac_bonus";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBonecrusherProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestArmorCrushingBlowHalvesArmorShieldAndNaturalArmorForThisAttackOnly();
            TestBoneShatterAddsBluntDamageAgainstUndeadAndConstructsOnly();
            TestAftershockFractureStacksDamageAndConsumesStacksAtThree();
            TestAftershockFractureAppliesActionProgressSlowWhenTargetHasNoAp();
            RequestTestExit(_test.Finish("Bonecrusher weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Bonecrusher weapon ability regression"));
        }
    }

    private void TestBonecrusherProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using BonecrusherFixture fixture = BonecrusherFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(BonecrusherItemId), "真实物品内容应包含碎骨者。");
        _test.True(
            fixture.TraitDefs.ContainsKey(ArmorCrushingTraitId),
            "真实 trait 内容应包含碎甲重击。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(BoneShatterTraitId),
            "真实 trait 内容应包含骨骼粉碎。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(AftershockFractureTraitId),
            "真实 trait 内容应包含余震破防。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ArmorCrushingBindingId),
            "真实装备能力内容应包含碎甲重击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(BoneShatterBindingId),
            "真实装备能力内容应包含骨骼粉碎 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(AftershockFractureBindingId),
            "真实装备能力内容应包含余震破防 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(BonecrusherItemId))
            return;

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_bonecrusher.tres"
        );
        _test.True(rawItem != null, "碎骨者原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "碎骨者", "碎骨者显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "碎骨者应继承 greataxe 模板。"
            );
            _test.Eq(rawItem.base_price, 48000, "碎骨者价格应为 48000。");
            _test.True(
                rawItem.trait_ids.Contains(ArmorCrushingTraitId),
                "碎骨者物品应声明碎甲重击 trait。"
            );
            _test.True(
                rawItem.trait_ids.Contains(BoneShatterTraitId),
                "碎骨者物品应声明骨骼粉碎 trait。"
            );
            _test.True(
                rawItem.trait_ids.Contains(AftershockFractureTraitId),
                "碎骨者物品应声明余震破防 trait。"
            );
            _test.False(
                TextContainsEnglishCreatureLabels(rawItem.description),
                "碎骨者玩家说明不应直接露出 undead/construct 英文标签。"
            );
        }

        AssertTraitDescriptionIsPlayerFacingChinese(fixture, BoneShatterTraitId);

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildBonecrusherUnit("projection");

        _test.Eq(equipped.weapon_item_id, BonecrusherItemId, "碎骨者装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("greataxe"), "碎骨者应投影为 greataxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "碎骨者应投影为 axe family。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_blunt"),
            "碎骨者应投影为钝击伤害标签。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "碎骨者攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "碎骨者应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "碎骨者应为 1D12+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 12, "碎骨者应为 1D12+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "碎骨者应为 1D12+3。");

        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ArmorCrushingTraitId,
            ArmorCrushingBindingId,
            "eq_bonecrusher_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            BoneShatterTraitId,
            BoneShatterBindingId,
            "eq_bonecrusher_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            AftershockFractureTraitId,
            AftershockFractureBindingId,
            "eq_bonecrusher_projection"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, ArmorCrushingBindingId, "attack_defense_modifier"),
            "碎甲重击必须由 attack_defense_modifier action 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, BoneShatterBindingId, "add_damage_dice"),
            "骨骼粉碎必须由 add_damage_dice action 配置声明。"
        );
        _test.True(
            BindingHasModifyActionMode(
                fixture.Bindings,
                AftershockFractureBindingId,
                "subtract_current_action_points"
            ),
            "骨裂震荡扣 AP 必须由通用 modify_action_points mode 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, AftershockFractureBindingId, "consume_status_stacks"),
            "骨裂震荡触发后必须由 consume_status_stacks action 清除裂防层数。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除碎骨者后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除碎骨者后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除碎骨者后装备能力源应清空。");
    }

    private void TestArmorCrushingBlowHalvesArmorShieldAndNaturalArmorForThisAttackOnly()
    {
        using BonecrusherFixture fixture = BonecrusherFixture.Build();
        BattleUnitState bonecrusher = fixture.BuildBonecrusherUnit("armor_crushing");
        BattleUnitState ordinary = fixture.BuildRaiderGreataxeUnit("ordinary_greataxe");
        BattleUnitState target = BuildTargetWithAcComponents("armored_target", new Vector2I(1, 0));
        int originalArmorClass = target.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS);
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();

        AttackCheckInput bonecrusherCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                bonecrusher,
                target,
                attackSkill,
                "skill_attack_check",
                "bonecrusher_armor_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput ordinaryCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                ordinary,
                target,
                attackSkill,
                "skill_attack_check",
                "ordinary_greataxe_armor_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );

        _test.Eq(ordinaryCheck.TargetArmorClass, 20, "普通巨斧不应调整目标 AC。");
        _test.Eq(
            bonecrusherCheck.TargetArmorClass,
            15,
            "碎甲重击应把 armor/shield/natural armor AC 组件按 50% 计算。"
        );
        _test.Eq(
            target.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS),
            originalArmorClass,
            "碎甲重击不应改写目标永久 armor_class snapshot。"
        );
    }

    private void TestBoneShatterAddsBluntDamageAgainstUndeadAndConstructsOnly()
    {
        using BonecrusherFixture undeadFixture = BonecrusherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState undeadAttacker = undeadFixture.BuildBonecrusherUnit("damage_undead");
        BattleUnitState undead = BuildTarget("undead_target", new Vector2I(1, 0), "undead", hp: 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            undeadFixture.Runtime,
            undeadAttacker,
            undead,
            "bonecrusher_damage_undead",
            previewCommand: false
        );
        _test.Eq(
            120 - undead.current_hp,
            13,
            "碎骨者命中亡灵时应造成武器 1D12+3 加骨骼粉碎 2D8。"
        );

        using BonecrusherFixture constructFixture = BonecrusherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState constructAttacker = constructFixture.BuildBonecrusherUnit("damage_construct");
        BattleUnitState construct = BuildTarget("construct_target", new Vector2I(1, 0), "construct", hp: 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            constructFixture.Runtime,
            constructAttacker,
            construct,
            "bonecrusher_damage_construct",
            previewCommand: false
        );
        _test.Eq(
            120 - construct.current_hp,
            13,
            "碎骨者命中构造体时应造成武器 1D12+3 加骨骼粉碎 2D8。"
        );

        using BonecrusherFixture humanoidFixture = BonecrusherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState humanoidAttacker = humanoidFixture.BuildBonecrusherUnit("damage_humanoid");
        BattleUnitState humanoid = BuildTarget("humanoid_target", new Vector2I(1, 0), "humanoid", hp: 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            humanoidFixture.Runtime,
            humanoidAttacker,
            humanoid,
            "bonecrusher_damage_humanoid",
            previewCommand: false
        );
        _test.Eq(
            120 - humanoid.current_hp,
            8,
            "碎骨者命中普通人形目标时只应造成武器 1D12+3。"
        );
    }

    private void TestAftershockFractureStacksDamageAndConsumesStacksAtThree()
    {
        using BonecrusherFixture fixture = BonecrusherFixture.Build(BuildFixedRolls(20, 3));
        BattleUnitState attacker = fixture.BuildBonecrusherUnit("fracture_ap");
        BattleUnitState target = BuildTarget("fracture_target", new Vector2I(1, 0), "humanoid", hp: 200);
        target.current_ap = 1;

        int[] expectedDamage = { 6, 8, 10 };
        int[] expectedStacksAfterHit = { 1, 2, 0 };
        int previousHp = target.current_hp;
        for (int hit = 0; hit < expectedDamage.Length; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"bonecrusher_fracture_hit_{hit + 1}",
                previewCommand: false
            );
            int damage = previousHp - target.current_hp;
            previousHp = target.current_hp;
            _test.Eq(
                damage,
                expectedDamage[hit],
                $"第 {hit + 1} 次命中应按命中前裂防层数追加伤害。"
            );
            _test.Eq(
                target.GetStatusEffect(FracturedDefenseStatusId)?.stacks ?? 0,
                expectedStacksAfterHit[hit],
                $"第 {hit + 1} 次命中后裂防层数应匹配。"
            );
        }

        _test.Eq(target.current_ap, 0, "裂防达到 3 层时，若目标有 AP，应扣除 1 AP。");
        _test.False(
            target.HasStatusEffect(TimeSlowStatusId),
            "目标有 AP 可扣时，骨裂震荡不应额外施加行动进度减速。"
        );
    }

    private void TestAftershockFractureAppliesActionProgressSlowWhenTargetHasNoAp()
    {
        using BonecrusherFixture fixture = BonecrusherFixture.Build(BuildFixedRolls(20, 3));
        BattleUnitState attacker = fixture.BuildBonecrusherUnit("fracture_slow");
        BattleUnitState target = BuildTarget("fracture_slow_target", new Vector2I(1, 0), "humanoid", hp: 200);
        target.current_ap = 0;

        for (int hit = 0; hit < 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"bonecrusher_fracture_slow_hit_{hit + 1}",
                previewCommand: false
            );
        }

        BattleStatusEffectState slow = target.GetStatusEffect(TimeSlowStatusId);
        _test.True(slow != null, "裂防达到 3 层且目标无 AP 时应施加行动进度减速。");
        if (slow != null)
        {
            _test.Eq(slow.duration, 60, "骨裂震荡行动进度减速应持续 60TU。");
            _test.Eq(slow.display_label, "迟滞", "骨裂震荡减速显示名应使用中文。");
            _test.Eq(
                BattleTemporalStatusService.ConsumeActionProgressGain(target, 10),
                5,
                "time_slow 语义应让行动进度获得率降为 50%。"
            );
        }
        _test.Eq(
            target.GetStatusEffect(FracturedDefenseStatusId)?.stacks ?? 0,
            0,
            "骨裂震荡触发后应清除裂防层数。"
        );
        _test.Eq(target.current_ap, 0, "目标无 AP 时骨裂震荡不应产生负 AP。");
    }

    private static GArray BuildFixedRolls(int count, int value)
    {
        GArray rolls = new();
        for (int index = 0; index < count; index++)
            rolls.Add(value);
        return rolls;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName creatureTag,
        int hp = 30
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
        unit.creature_type_tags.Add(creatureTag);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleUnitState BuildTargetWithAcComponents(StringName unitId, Vector2I coord)
    {
        BattleUnitState target = BuildTarget(unitId, coord, "humanoid");
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 20);
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_AC_BONUS, 4);
        target.attribute_snapshot.SetValue(AttributeService.SHIELD_AC_BONUS, 2);
        target.attribute_snapshot.SetValue(NaturalArmorAcBonus, 4);
        target.attribute_snapshot.SetValue(AttributeService.DODGE_BONUS, 1);
        target.attribute_snapshot.SetValue(AttributeService.DEFLECTION_BONUS, 2);
        return target;
    }

    private static void AssertTraitDescriptionIsPlayerFacingChinese(
        BonecrusherFixture fixture,
        StringName traitId
    )
    {
        if (
            fixture == null
            || !fixture.TraitDefs.TryGetValue(traitId, out TraitDefinition trait)
        )
            return;
        if (TextContainsEnglishCreatureLabels(trait.Description))
        {
            throw new InvalidOperationException(
                $"{traitId} description exposes internal English creature labels."
            );
        }
    }

    private static bool TextContainsEnglishCreatureLabels(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return value.IndexOf("undead", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("construct", StringComparison.OrdinalIgnoreCase) >= 0;
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

    private static bool BindingHasModifyActionMode(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        StringName mode
    )
    {
        if (bindings == null || !bindings.TryGetValue(bindingId, out var binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.Kind == "modify_action_points"
                    && action.PayloadDefinition is ModifyActionPointsActionPayloadDefinition payload
                    && payload.Mode == mode
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

    private sealed class BonecrusherFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private BonecrusherFixture(
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
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static BonecrusherFixture Build(GArray damageRolls = null)
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
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls ?? new GArray { 3, 4 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new BonecrusherFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildBonecrusherUnit(string label)
        {
            return BuildEquippedUnit(label, BonecrusherItemId, $"eq_bonecrusher_{label}");
        }

        internal BattleUnitState BuildRaiderGreataxeUnit(string label)
        {
            return BuildEquippedUnit(label, RaiderGreataxeItemId, $"eq_raider_greataxe_{label}");
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private BattleUnitState BuildEquippedUnit(
            string label,
            StringName itemId,
            StringName instanceId
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

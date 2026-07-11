using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_thunderfang_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ThunderfangItemId =
        "weapon_unique_axe_thunderfang_086";
    private static readonly StringName ThunderSlashTraitId =
        "weapon.axe.thunderfang.thunder_slash";
    private static readonly StringName ThorsHammeringTraitId =
        "weapon.axe.thunderfang.thors_hammering";
    private static readonly StringName StormConductorTraitId =
        "weapon.axe.thunderfang.storm_conductor";
    private static readonly StringName ThunderSlashBindingId =
        "binding.weapon.axe.thunderfang.thunder_slash";
    private static readonly StringName ThorsHammeringBindingId =
        "binding.weapon.axe.thunderfang.thors_hammering";
    private static readonly StringName StormConductorBindingId =
        "binding.weapon.axe.thunderfang.storm_conductor";
    private static readonly StringName StunnedStatusId = "stunned";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestThunderfangContentLoadsAndProjects();
            TestThunderSlashAddsThunderDamageOnRealWeaponHit();
            TestCriticalFailedConSaveAppliesStunnedForSixtyTu();
            TestCriticalSuccessfulConSaveDoesNotStun();
            TestThorsHammeringUsesStormEnvironmentForAttackBonusAndMaximumDamage();
            TestStormConductorReflectsLightningOnlyInStormAgainstMeleeHit();
            RequestTestExit(_test.Finish("Thunderfang weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Thunderfang weapon ability regression"));
        }
    }

    private void TestThunderfangContentLoadsAndProjects()
    {
        using ThunderfangFixture fixture = ThunderfangFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ThunderfangItemId), "真实物品内容应包含雷霆之牙。");
        _test.True(fixture.TraitDefs.ContainsKey(ThunderSlashTraitId), "真实 trait 内容应包含雷鸣斩。");
        _test.True(fixture.TraitDefs.ContainsKey(ThorsHammeringTraitId), "真实 trait 内容应包含托尔的锤打。");
        _test.True(fixture.TraitDefs.ContainsKey(StormConductorTraitId), "真实 trait 内容应包含风暴导体。");
        _test.True(
            fixture.Bindings.ContainsKey(ThunderSlashBindingId),
            "真实装备能力内容应包含雷鸣斩 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ThorsHammeringBindingId),
            "真实装备能力内容应包含托尔的锤打 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StormConductorBindingId),
            "真实装备能力内容应包含风暴导体 binding。"
        );

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_thunderfang.tres"
        );
        _test.True(rawItem != null, "雷霆之牙原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "雷霆之牙", "雷霆之牙显示名应来自设计源。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "雷霆之牙应继承 greataxe 模板。"
            );
            _test.Eq(rawItem.base_price, 58000, "雷霆之牙基础价格应为 58000。");
            _test.True(rawItem.trait_ids.Contains(ThunderSlashTraitId), "雷霆之牙应固定声明雷鸣斩 trait。");
            _test.True(rawItem.trait_ids.Contains(ThorsHammeringTraitId), "雷霆之牙应固定声明托尔的锤打 trait。");
            _test.True(rawItem.trait_ids.Contains(StormConductorTraitId), "雷霆之牙应固定声明风暴导体 trait。");
            WeaponProfileDef rawProfile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "雷霆之牙应声明武器 profile override。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.training_group, new StringName("martial"), "雷霆之牙训练组应为 martial。");
                _test.Eq(rawProfile.range_type, new StringName("melee"), "雷霆之牙应为 melee。");
                _test.Eq(rawProfile.family, new StringName("axe"), "雷霆之牙应属于 axe。");
                _test.Eq(rawProfile.damage_tag, new StringName("physical_slash"), "雷霆之牙应为 slashing 伤害。");
                _test.Eq(rawProfile.attack_range, 1, "雷霆之牙攻击距离应为 1。");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "雷霆之牙双手应为 1D12+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 12, "雷霆之牙双手应为 1D12+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "雷霆之牙双手应为 1D12+2。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "two_handed"),
                    "雷霆之牙应声明 two_handed 属性。"
                );
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "heavy"),
                    "雷霆之牙应声明 heavy 属性。"
                );
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildThunderfangUnit("projection");
        _test.Eq(equipped.weapon_item_id, ThunderfangItemId, "雷霆之牙装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("greataxe"), "雷霆之牙应投影为 greataxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "雷霆之牙应投影为 axe。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_slash"),
            "雷霆之牙基础伤害标签应为 physical_slash。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "雷霆之牙攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "雷霆之牙应占用双手。");
        _test.False(equipped.weapon_is_versatile, "雷霆之牙不应是 versatile。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "雷霆之牙双手应为 1D12+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 12, "雷霆之牙双手应为 1D12+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "雷霆之牙双手应为 1D12+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ThunderSlashTraitId,
            ThunderSlashBindingId,
            "eq_thunderfang_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ThorsHammeringTraitId,
            ThorsHammeringBindingId,
            "eq_thunderfang_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StormConductorTraitId,
            StormConductorBindingId,
            "eq_thunderfang_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除雷霆之牙后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除雷霆之牙后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除雷霆之牙后装备能力源应清空。");
        _test.False(
            equipped.effective_trait_ids.Contains(ThunderSlashTraitId),
            "移除雷霆之牙后雷鸣斩 trait 不应残留。"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(ThorsHammeringTraitId),
            "移除雷霆之牙后托尔的锤打 trait 不应残留。"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(StormConductorTraitId),
            "移除雷霆之牙后风暴导体 trait 不应残留。"
        );
    }

    private void TestThunderSlashAddsThunderDamageOnRealWeaponHit()
    {
        using ThunderfangFixture fixture = ThunderfangFixture.Build(new GArray { 4, 3 });
        BattleUnitState attacker = fixture.BuildThunderfangUnit("thunder_damage");
        BattleUnitState target = BuildTarget("thunder_damage_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "thunderfang_damage",
            previewCommand: false
        );
        int thunderDamage = 100 - target.current_hp;

        using ThunderfangFixture plainFixture = ThunderfangFixture.Build(new GArray { 4, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildThunderfangUnit("plain_damage");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildTarget("plain_damage_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "thunderfang_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.current_hp;

        _test.Eq(plainDamage, 6, "固定骰 4 时，雷霆之牙基础武器伤害应为 1D12+2。");
        _test.Eq(
            thunderDamage,
            9,
            "雷鸣斩应在真实武器命中后额外造成 1D6 thunder，且不吞掉基础武器伤害。"
        );
    }

    private void TestCriticalFailedConSaveAppliesStunnedForSixtyTu()
    {
        using ThunderfangFixture fixture = ThunderfangFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildThunderfangUnit("critical_failed_save");
        BattleUnitState target = BuildTarget("critical_failed_save_target", new Vector2I(1, 0), hp: 100);

        ResolveThunderSlashAfterHit(
            fixture,
            attacker,
            target,
            "thunderfang_critical_failed_save",
            criticalHit: true,
            saveRollOverride: 1
        );

        BattleStatusEffectState stunned = target.GetStatusEffect(StunnedStatusId);
        _test.True(stunned != null, "暴击且 CON DC14 豁免失败时应施加 stunned。");
        _test.Eq(stunned?.duration ?? -1, 60, "雷霆之牙 stunned 应持续 60 TU。");
        _test.Eq(stunned?.source_unit_id ?? new StringName(""), attacker.unit_id, "stunned 应记录雷霆之牙持有者来源。");
    }

    private void TestCriticalSuccessfulConSaveDoesNotStun()
    {
        using ThunderfangFixture fixture = ThunderfangFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildThunderfangUnit("critical_success_save");
        BattleUnitState target = BuildTarget("critical_success_save_target", new Vector2I(1, 0), hp: 100);

        ResolveThunderSlashAfterHit(
            fixture,
            attacker,
            target,
            "thunderfang_critical_success_save",
            criticalHit: true,
            saveRollOverride: 20
        );

        _test.False(
            target.HasStatusEffect(StunnedStatusId),
            "暴击但 CON DC14 豁免成功时不应施加 stunned。"
        );
    }

    private void TestThorsHammeringUsesStormEnvironmentForAttackBonusAndMaximumDamage()
    {
        using ThunderfangFixture clearFixture = ThunderfangFixture.Build(new GArray { 1, 1 });
        BattleUnitState clearAttacker = clearFixture.BuildThunderfangUnit("clear_hammering");
        BattleUnitState clearTarget = BuildTarget("clear_hammering_target", new Vector2I(1, 0), hp: 100);
        BattleState clearState = BuildStateWithEnvironmentTags(
            "thunderfang_clear_hammering",
            clearAttacker,
            clearTarget,
            new GStringNameArray()
        );
        IssueBasicAttackInState(clearFixture.Runtime, clearState, clearAttacker, clearTarget);
        int clearDamage = 100 - clearTarget.current_hp;

        using ThunderfangFixture stormFixture = ThunderfangFixture.Build(new GArray { 1, 1 });
        BattleUnitState stormAttacker = stormFixture.BuildThunderfangUnit("storm_hammering");
        BattleUnitState stormTarget = BuildTarget("storm_hammering_target", new Vector2I(1, 0), hp: 100);
        BattleState stormState = BuildStateWithEnvironmentTags(
            "thunderfang_storm_hammering",
            stormAttacker,
            stormTarget,
            new GStringNameArray { "storm" }
        );
        IssueBasicAttackInState(stormFixture.Runtime, stormState, stormAttacker, stormTarget);
        int stormDamage = 100 - stormTarget.current_hp;

        _test.Eq(clearDamage, 4, "无 storm 时固定骰 1 应造成 1D12+2 加 1D6 thunder。");
        _test.Eq(stormDamage, 20, "storm 中托尔的锤打应让本次雷霆之牙伤害取最大值。");

        BattleAttackCheckPolicyService attackPolicy = stormFixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle stormBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                stormState,
                stormAttacker,
                stormTarget,
                attackSkill,
                "skill_attack_check",
                "thunderfang_storm_attack_bonus",
                force_hit_no_crit: false
            )
        );
        _test.Eq(stormBundle.GetEffectiveModifierDelta(), 2, "storm 中托尔的锤打应提供攻击检定 +2。");
        _test.True(
            HasModifier(stormBundle, ThorsHammeringBindingId, 2),
            "托尔的锤打 +2 应进入 modifier breakdown。"
        );

        StringName clearMode = clearFixture.Runtime.GetEquipmentAbilityRuntimeService()
            .ResolveDamageRollModeOverride(
                new BattleEquipmentAbilityDamageRollModeContext
                {
                    SourceUnit = clearAttacker,
                    TargetUnit = clearTarget,
                    BattleState = clearState,
                    CurrentRollMode = "random",
                    AttackSucceeded = true,
                }
            );
        StringName stormMode = stormFixture.Runtime.GetEquipmentAbilityRuntimeService()
            .ResolveDamageRollModeOverride(
                new BattleEquipmentAbilityDamageRollModeContext
                {
                    SourceUnit = stormAttacker,
                    TargetUnit = stormTarget,
                    BattleState = stormState,
                    CurrentRollMode = "random",
                    AttackSucceeded = true,
                }
            );
        _test.Eq(clearMode, new StringName("random"), "无 storm 时不应改写伤害骰模式。");
        _test.Eq(stormMode, new StringName("maximum"), "storm 中应把伤害骰模式改写为 maximum。");
    }

    private void TestStormConductorReflectsLightningOnlyInStormAgainstMeleeHit()
    {
        using ThunderfangFixture fixture = ThunderfangFixture.Build(new GArray { 5 });
        BattleUnitState holder = fixture.BuildThunderfangUnit("storm_conductor");
        BattleUnitState meleeAttacker = BuildTarget("storm_conductor_melee", new Vector2I(1, 0), hp: 50);
        meleeAttacker.weapon_range_type = "melee";
        BattleState stormState = BuildStateWithEnvironmentTags(
            "thunderfang_storm_conductor",
            holder,
            meleeAttacker,
            new GStringNameArray { "storm" }
        );
        fixture.Runtime.SetupStateForTests(stormState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = holder,
                TargetUnit = meleeAttacker,
                BattleState = stormState,
                AttackSucceeded = true,
            }
        );
        _test.Eq(
            meleeAttacker.current_hp,
            44,
            "storm 中近战命中持有者时，攻击者应受到风暴导体 1D6 lightning；托尔的锤打会将该伤害骰最大化。"
        );

        using ThunderfangFixture clearFixture = ThunderfangFixture.Build(new GArray { 5 });
        BattleUnitState clearHolder = clearFixture.BuildThunderfangUnit("clear_conductor");
        BattleUnitState clearMeleeAttacker = BuildTarget("clear_conductor_melee", new Vector2I(1, 0), hp: 50);
        clearMeleeAttacker.weapon_range_type = "melee";
        BattleState clearState = BuildStateWithEnvironmentTags(
            "thunderfang_clear_conductor",
            clearHolder,
            clearMeleeAttacker,
            new GStringNameArray()
        );
        clearFixture.Runtime.SetupStateForTests(clearState);
        clearFixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = clearHolder,
                TargetUnit = clearMeleeAttacker,
                BattleState = clearState,
                AttackSucceeded = true,
            }
        );
        _test.Eq(clearMeleeAttacker.current_hp, 50, "无 storm 时风暴导体不应反伤。");

        using ThunderfangFixture rangedFixture = ThunderfangFixture.Build(new GArray { 5 });
        BattleUnitState rangedHolder = rangedFixture.BuildThunderfangUnit("ranged_conductor");
        BattleUnitState rangedAttacker = BuildTarget("ranged_conductor_attacker", new Vector2I(1, 0), hp: 50);
        rangedAttacker.weapon_range_type = "ranged";
        BattleState rangedStormState = BuildStateWithEnvironmentTags(
            "thunderfang_ranged_conductor",
            rangedHolder,
            rangedAttacker,
            new GStringNameArray { "storm" }
        );
        rangedFixture.Runtime.SetupStateForTests(rangedStormState);
        rangedFixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveHitReceived(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = rangedHolder,
                TargetUnit = rangedAttacker,
                BattleState = rangedStormState,
                AttackSucceeded = true,
            }
        );
        _test.Eq(rangedAttacker.current_hp, 50, "storm 中非 melee 攻击命中时风暴导体不应反伤。");
    }

    private static void ResolveThunderSlashAfterHit(
        ThunderfangFixture fixture,
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

    private static void IssueBasicAttackInState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        runtime.SetupStateForTests(state);
        runtime.IssueCommand(WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(attacker, target));
    }

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

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = hp,
            weapon_range_type = "melee",
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int modifierDelta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (
                spec.source_domain == "equipment_ability"
                && spec.source_id == sourceId
                && spec.modifier_delta == modifierDelta
            )
            {
                return true;
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private sealed class ThunderfangFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ThunderfangFixture(
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

        internal static ThunderfangFixture Build(GArray damageRolls)
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
            return new ThunderfangFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildThunderfangUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ThunderfangItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    ThunderfangItemId,
                    $"eq_thunderfang_{label}"
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

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_shieldbreaker_guard_breaker_regression : LifecycleTestSceneTree
{
    private static readonly StringName ShieldbreakerItemId =
        "weapon_unique_axe_shieldbreaker_098";
    private static readonly StringName GuardBreakerBindingId =
        "binding.weapon.axe.shieldbreaker.guard_breaker";
    private static readonly StringName SiegeAxeBindingId =
        "binding.weapon.axe.shieldbreaker.siege_axe";
    private static readonly StringName CommonShieldId = "fixture_common_wooden_shield";
    private static readonly StringName UncommonShieldId = "fixture_uncommon_warded_shield";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGuardBreakerSemanticsAreDeclaredByConfig();
            TestGuardBreakerAttackModifierAppliesOnlyToShieldedTargets();
            TestGuardBreakerDestroysCommonShieldOnlyOnHitAndSuccessfulRollGate();
            TestGuardBreakerDoesNotDestroyMagicalShield();
            TestSiegeAxeAddsDamageDiceAgainstConstructs();
            RequestTestExit(_test.Finish("Shieldbreaker guard breaker regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Shieldbreaker guard breaker regression"));
        }
    }

    private void TestGuardBreakerSemanticsAreDeclaredByConfig()
    {
        using ShieldbreakerFixture fixture = ShieldbreakerFixture.Build();
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings =
            fixture.Runtime.GetEquipmentAbilityBindingIndexTyped();
        _test.True(
            bindings.TryGetValue(GuardBreakerBindingId, out EquipmentAbilityBindingDefinition binding),
            "破盾者 binding 应来自装备能力配置。"
        );
        if (binding == null)
            return;

        _test.True(
            HasAttackRollBonusAction(binding, bonus: 3),
            "破盾者 +3 attack roll bonus 必须由配置 action 声明，不能依赖 C# 专用分支。"
        );

        EquipmentDurabilityDamageActionPayloadDefinition durabilityPayload =
            FindDurabilityPayload(binding);
        _test.True(durabilityPayload != null, "破盾者应由配置声明装备耐久 damage action。");
        if (durabilityPayload == null)
            return;

        System.Reflection.PropertyInfo maxTargetRarityProperty =
            typeof(EquipmentDurabilityDamageActionPayloadDefinition).GetProperty(
                "MaxTargetRarity"
            );
        _test.True(
            maxTargetRarityProperty != null,
            "装备耐久 damage payload 必须提供 MaxTargetRarity 类型化字段表达非魔法盾限制。"
        );
        if (maxTargetRarityProperty == null)
            return;
        _test.Eq(
            (int)maxTargetRarityProperty.GetValue(durabilityPayload),
            (int)EquipmentInstanceState.RarityTier.COMMON,
            "破盾者应通过配置限制只粉碎 common 盾牌。"
        );
    }

    private void TestGuardBreakerAttackModifierAppliesOnlyToShieldedTargets()
    {
        using ShieldbreakerFixture fixture = ShieldbreakerFixture.Build();
        BattleUnitState attacker = fixture.BuildShieldbreakerUnit("attack_modifier");
        BattleUnitState shieldedTarget = BuildTarget(
            "shielded_target",
            new Vector2I(1, 0),
            shieldItemId: CommonShieldId,
            shieldInstanceId: "eq_common_shield_modifier",
            rarity: (int)EquipmentInstanceState.RarityTier.COMMON,
            durability: 12
        );
        BattleUnitState unshieldedTarget = BuildTarget("unshielded_target", new Vector2I(1, 0));

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle shieldedBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                shieldedTarget,
                attackSkill,
                "skill_attack_check",
                "shieldbreaker_test",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle unshieldedBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                unshieldedTarget,
                attackSkill,
                "skill_attack_check",
                "shieldbreaker_test",
                force_hit_no_crit: false
            )
        );

        _test.Eq(
            shieldedBundle.TotalBonus,
            3,
            "破盾者对持盾目标应提供 +3 attack roll bonus。"
        );
        _test.True(
            HasModifier(shieldedBundle, GuardBreakerBindingId, 3),
            "破盾者 +3 应在 modifier breakdown 中标明装备能力来源。"
        );
        _test.Eq(
            unshieldedBundle.TotalBonus,
            0,
            "破盾者不应对无盾目标提供 attack roll bonus。"
        );

        AttackCheckInput shieldedCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                shieldedTarget,
                attackSkill,
                "skill_attack_check",
                "shieldbreaker_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput unshieldedCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                unshieldedTarget,
                attackSkill,
                "skill_attack_check",
                "shieldbreaker_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        _test.Eq(
            shieldedCheck.SituationalAttackBonus,
            3,
            "破盾者 +3 应进入实际 AttackCheckInput。"
        );
        _test.Eq(
            shieldedCheck.RequiredRoll,
            unshieldedCheck.RequiredRoll - 3,
            "破盾者应让命中所需点数降低 3。"
        );
    }

    private void TestGuardBreakerDestroysCommonShieldOnlyOnHitAndSuccessfulRollGate()
    {
        using ShieldbreakerFixture fixture = ShieldbreakerFixture.Build();
        BattleUnitState attacker = fixture.BuildShieldbreakerUnit("break_success");
        BattleUnitState target = BuildTarget(
            "common_shield_target",
            new Vector2I(1, 0),
            shieldItemId: CommonShieldId,
            shieldInstanceId: "eq_common_shield_break",
            rarity: (int)EquipmentInstanceState.RarityTier.COMMON,
            durability: 12
        );

        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 6 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "shieldbreaker_guard_break_success",
            previewCommand: false
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedItemId("off_hand"),
            new StringName(""),
            "破盾者成功触发后应真实移除目标 off_hand 盾牌。"
        );

        BattleUnitState failedRollTarget = BuildTarget(
            "failed_roll_target",
            new Vector2I(1, 0),
            shieldItemId: CommonShieldId,
            shieldInstanceId: "eq_common_shield_failed_roll",
            rarity: (int)EquipmentInstanceState.RarityTier.COMMON,
            durability: 12
        );
        failedRollTarget.current_hp = 100;
        failedRollTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 7 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            failedRollTarget,
            "shieldbreaker_guard_break_failed_roll",
            previewCommand: false
        );
        _test.Eq(
            failedRollTarget.GetEquipmentView().GetEquippedItemId("off_hand"),
            CommonShieldId,
            "破盾者 roll gate 失败时不应移除盾牌。"
        );
        _test.Eq(
            failedRollTarget.GetEquipmentView().GetEquippedInstance("off_hand").current_durability,
            12,
            "破盾者 roll gate 失败时不应扣盾牌耐久。"
        );

        BattleUnitState missedTarget = BuildTarget(
            "missed_target",
            new Vector2I(1, 0),
            shieldItemId: CommonShieldId,
            shieldInstanceId: "eq_common_shield_miss",
            rarity: (int)EquipmentInstanceState.RarityTier.COMMON,
            durability: 12
        );
        missedTarget.current_hp = 100;
        missedTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 6 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            missedTarget,
            "shieldbreaker_guard_break_miss",
            previewCommand: false
        );
        _test.Eq(
            missedTarget.GetEquipmentView().GetEquippedItemId("off_hand"),
            CommonShieldId,
            "攻击未命中时不应移除盾牌。"
        );
    }

    private void TestGuardBreakerDoesNotDestroyMagicalShield()
    {
        using ShieldbreakerFixture fixture = ShieldbreakerFixture.Build();
        BattleUnitState attacker = fixture.BuildShieldbreakerUnit("magical_shield");
        BattleUnitState target = BuildTarget(
            "magical_shield_target",
            new Vector2I(1, 0),
            shieldItemId: UncommonShieldId,
            shieldInstanceId: "eq_uncommon_shield",
            rarity: (int)EquipmentInstanceState.RarityTier.UNCOMMON,
            durability: 24
        );

        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 6 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "shieldbreaker_guard_break_magical",
            previewCommand: false
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedItemId("off_hand"),
            UncommonShieldId,
            "魔法盾不应被移除。"
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("off_hand").current_durability,
            24,
            "魔法盾不应扣耐久。"
        );
    }

    private void TestSiegeAxeAddsDamageDiceAgainstConstructs()
    {
        using ShieldbreakerFixture fixture = ShieldbreakerFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildShieldbreakerUnit("siege_axe");
        BattleUnitState construct = BuildTarget("construct_target", new Vector2I(1, 0));
        construct.creature_type_tags.Add("construct");
        BattleUnitState humanoid = BuildTarget("humanoid_target", new Vector2I(1, 0));
        humanoid.creature_type_tags.Add("humanoid");
        construct.current_hp = 120;
        construct.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        humanoid.current_hp = 120;
        humanoid.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            construct,
            "shieldbreaker_siege_construct",
            previewCommand: false
        );
        int constructDamage = 120 - construct.current_hp;

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            humanoid,
            "shieldbreaker_siege_humanoid",
            previewCommand: false
        );
        int humanoidDamage = 120 - humanoid.current_hp;

        _test.True(
            constructDamage > humanoidDamage,
            "攻城之斧真实基础攻击命中 construct 应比命中 humanoid 造成更多 HP 伤害。"
        );
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int modifierDelta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle.Breakdown)
        {
            if (spec.source_domain == "equipment_ability"
                && spec.source_id == sourceId
                && spec.modifier_delta == modifierDelta)
                return true;
        }
        return false;
    }

    private static bool HasAttackRollBonusAction(
        EquipmentAbilityBindingDefinition binding,
        int bonus
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.Kind == "attack_roll_bonus"
                    && action.PayloadDefinition is AttackRollBonusActionPayloadDefinition payload
                    && payload.Bonus == bonus
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static EquipmentDurabilityDamageActionPayloadDefinition FindDurabilityPayload(
        EquipmentAbilityBindingDefinition binding
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.Kind == "equipment_durability_damage"
                    && action.PayloadDefinition is EquipmentDurabilityDamageActionPayloadDefinition payload
                )
                {
                    return payload;
                }
            }
        }
        return null;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName shieldItemId = default,
        StringName shieldInstanceId = default,
        int rarity = 0,
        int durability = 0
    )
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
        EquipmentState equipment = new();
        if (shieldItemId != default && shieldItemId != "")
        {
            EquipmentInstanceState instance =
                EquipmentInstanceState.CreateInstance(shieldItemId, shieldInstanceId);
            instance.rarity = rarity;
            instance.current_durability = durability;
            equipment.SetEquippedEntry(
                "off_hand",
                shieldItemId,
                new GStringNameArray { "off_hand" },
                instance
            );
        }
        unit.SetEquipmentView(equipment);
        return unit;
    }

    private sealed class ShieldbreakerFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ShieldbreakerFixture(
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
        }

        internal BattleRuntimeModule Runtime { get; }

        internal static ShieldbreakerFixture Build(GArray damageRolls = null)
        {
            ItemContentRegistry itemRegistry = new(new TestContentResourceLoader());
            ProgressionContentRegistry progressionRegistry = new(new TestContentResourceLoader());
            Dictionary<StringName, ItemDefinition> itemDefs = new(
                itemRegistry.GetItemDefsTyped()
            );
            itemDefs[CommonShieldId] = BuildShieldItem(CommonShieldId);
            itemDefs[UncommonShieldId] = BuildShieldItem(UncommonShieldId);

            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemDefs,
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemDefs,
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls ?? new GArray { 3, 4 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new ShieldbreakerFixture(
                itemRegistry,
                progressionRegistry,
                partyState,
                runtime
            );
        }

        internal BattleUnitState BuildShieldbreakerUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ShieldbreakerItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    ShieldbreakerItemId,
                    $"eq_shieldbreaker_{label}"
                )
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            BattleUnitState unit = units[0];
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

        private static ItemDefinition BuildShieldItem(StringName itemId)
        {
            ItemDef itemResource = TestResourceOwnership.Own(
                new ItemDef
                {
                    item_id = itemId,
                    display_name = itemId.ToString(),
                    item_category = "equipment",
                    equipment_type_id = "armor",
                    equipment_slot_ids = new Godot.Collections.Array<string> { "off_hand" },
                    is_stackable = false,
                    max_stack = 1,
                    tags = new GStringNameArray { "shield" },
                },
                $"ShieldbreakerGuardBreaker.BuildShieldItem.{itemId}"
            );
            return itemResource.ToDefinition();
        }
    }
}

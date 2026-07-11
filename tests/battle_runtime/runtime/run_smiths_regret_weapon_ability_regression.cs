using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_smiths_regret_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_longsword_smiths_regret";
    private static readonly StringName FlawedBeautyTraitId =
        "weapon.sword.smiths_regret.flawed_beauty";
    private static readonly StringName ImperfectResonanceTraitId =
        "weapon.sword.smiths_regret.imperfect_resonance";
    private static readonly StringName ElementalOverloadTraitId =
        "weapon.sword.smiths_regret.elemental_overload";
    private static readonly StringName MoradinForgivenessTraitId =
        "weapon.sword.smiths_regret.moradin_forgiveness";
    private static readonly StringName FlawedBeautyBindingId =
        "binding.weapon.sword.smiths_regret.flawed_beauty";
    private static readonly StringName MoradinForgivenessBindingId =
        "binding.weapon.sword.smiths_regret.moradin_forgiveness";
    private static readonly StringName GraceStatusId = "smiths_regret_moradin_grace";
    private static readonly StringName ColdEchoStatusId = "smiths_regret_flaw_cold";
    private static readonly StringName FireEchoStatusId = "smiths_regret_flaw_fire";
    private static readonly StringName BurningStatusId = "burning";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestContentLoadsAndProjectsFiveFeatures();
            TestFlawedBeautyOutcomeTableControlsFireFlaw();
            TestElementalOverloadRequiresDifferentRecentFlaw();
            TestMoradinGraceAppliesToNextWeaponAttackAndConsumesOnMiss();
            RequestTestExit(_test.Finish("Smith's Regret weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Smith's Regret weapon ability regression"));
        }
    }

    private void TestContentLoadsAndProjectsFiveFeatures()
    {
        using SmithFixture fixture = SmithFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含铁匠的悔恨。");
        _test.True(
            fixture.TraitDefs.ContainsKey(FlawedBeautyTraitId),
            "真实 trait 内容应包含缺陷之美。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(ImperfectResonanceTraitId),
            "真实 trait 内容应包含不完美共鸣。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(ElementalOverloadTraitId),
            "真实 trait 内容应包含元素超载。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(MoradinForgivenessTraitId),
            "真实 trait 内容应包含摩拉丁的谅解。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(FlawedBeautyBindingId),
            "真实装备能力内容应包含缺陷之美 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(MoradinForgivenessBindingId),
            "真实装备能力内容应包含摩拉丁的谅解 binding。"
        );

        ItemDef raw = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_smiths_regret.tres"
        );
        _test.True(raw != null, "铁匠的悔恨原始资源应能加载。");
        if (raw != null)
        {
            _test.Eq(raw.item_id, ItemId, "铁匠的悔恨 item_id 应使用无数字 longsword 文件名。");
            _test.Eq(raw.display_name, "铁匠的悔恨", "铁匠的悔恨显示名应来自设计源。");
            _test.Eq(
                raw.base_item_id,
                new StringName("weapon_type_longsword_base"),
                "铁匠的悔恨应继承 longsword 模板。"
            );
            _test.Eq(raw.base_price, 52000, "铁匠的悔恨基础价格应为 52000。");
            _test.Eq(raw.trait_ids.Count, 4, "物品应显式挂载 4 个 trait，其中不完美共鸣与元素超载由缺陷之美配置落地。");
        }

        BattleUnitState equipped = fixture.BuildSmithUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("longsword"),
            "铁匠的悔恨应投影为 longsword。"
        );
        _test.Eq(
            equipped.weapon_one_handed_dice?.dice_count ?? 0,
            1,
            "铁匠的悔恨单手应为 1D8+2。"
        );
        _test.Eq(
            equipped.weapon_one_handed_dice?.dice_sides ?? 0,
            8,
            "铁匠的悔恨单手应为 1D8+2。"
        );
        _test.Eq(
            equipped.weapon_one_handed_dice?.flat_bonus ?? 0,
            2,
            "铁匠的悔恨单手应为 1D8+2。"
        );
        _test.True(equipped.weapon_is_versatile, "铁匠的悔恨应保留 versatile 属性。");

        EquipmentAbilityBindingDefinition flawed = fixture.Bindings[FlawedBeautyBindingId];
        EquipmentAbilityReactionDefinition reaction = flawed.Reactions[0];
        _test.True(reaction.RollGate?.Roll?.Terms?.Count == 1, "缺陷之美应配置 D100 触发门槛。");
        _test.Eq(reaction.RollGate?.Compare ?? new StringName(""), new StringName("lte"), "缺陷之美应在 D100 <= 20 时触发。");
        _test.Eq(reaction.RollGate?.Threshold ?? 0, 20, "缺陷之美触发门槛应为 20%。");
        _test.True(reaction.OutcomeTable != null, "缺陷之美应通过 outcome_table 配置 D4 缺陷分支。");
        _test.Eq(reaction.OutcomeTable?.Entries?.Count ?? 0, 4, "缺陷之美 outcome_table 应包含 4 个 D4 分支。");

        EquipmentAbilityBindingDefinition grace = fixture.Bindings[MoradinForgivenessBindingId];
        _test.True(
            HasReaction(grace, EquipmentAbilityTriggerKind.OnAttackCheck, EquipmentAbilityTimingKind.AfterAttackCheck),
            "摩拉丁的谅解必须声明攻击检定提交后清除状态。"
        );
        _test.True(
            AttackBonusRequiresWeaponDamage(grace),
            "摩拉丁的谅解 +2 必须限制在本武器攻击检定上。"
        );
    }

    private void TestFlawedBeautyOutcomeTableControlsFireFlaw()
    {
        using SmithFixture fixture = SmithFixture.Build(new GArray { 4, 2 });
        BattleUnitState attacker = fixture.BuildSmithUnit("fire");
        BattleUnitState target = BuildTarget("smith_fire_target", new Vector2I(1, 0), hp: 100);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 20, 2 }
        );

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "smiths_regret_fire",
            previewCommand: false
        );

        _test.Eq(100 - target.current_hp, 8, "火焰缺陷应造成基础 1D8+2 加 1D6 fire。");
        BattleStatusEffectState burning = target.GetStatusEffect(BurningStatusId);
        _test.True(burning != null, "D4=2 应施加 burning。");
        _test.Eq(burning?.duration ?? -1, 60, "burning 应持续 60 TU。");
        _test.Eq(burning?.tick_interval_tu ?? 0, 10, "burning tick 应每 10 TU 触发。");
        BattleStatusEffectState echo = target.GetStatusEffect(FireEchoStatusId);
        _test.True(echo != null, "火焰缺陷应留下 120 TU 共鸣标记。");
        _test.Eq(echo?.duration ?? -1, 120, "火焰共鸣标记应持续 120 TU。");
        _test.Eq(
            attacker.GetStatusEffect(GraceStatusId)?.duration ?? -1,
            60,
            "缺陷触发后应写入 60 TU 的摩拉丁 grace 状态。"
        );
    }

    private void TestElementalOverloadRequiresDifferentRecentFlaw()
    {
        using SmithFixture fixture = SmithFixture.Build(new GArray { 4, 2, 4, 2, 3, 3 });
        BattleUnitState attacker = fixture.BuildSmithUnit("overload");
        BattleUnitState target = BuildTarget("smith_overload_target", new Vector2I(1, 0), hp: 100);

        fixture.Runtime.GetEquipmentAbilityRuntimeService().ConfigureRollGateValuesForTests(
            new[] { 20, 1, 20, 2 }
        );
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "smiths_regret_overload_first",
            previewCommand: false
        );
        _test.True(target.HasStatusEffect(ColdEchoStatusId), "D4=1 应留下寒冷共鸣标记。");
        int afterCold = target.current_hp;

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "smiths_regret_overload_second",
            previewCommand: false
        );

        _test.Eq(
            afterCold - target.current_hp,
            14,
            "已有不同缺陷标记时，火焰缺陷应追加 2D6 force 超载伤害。"
        );
    }

    private void TestMoradinGraceAppliesToNextWeaponAttackAndConsumesOnMiss()
    {
        using SmithFixture fixture = SmithFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildSmithUnit("grace");
        BattleUnitState target = BuildTarget("smith_grace_target", new Vector2I(1, 0), hp: 100);
        ApplyGraceStatus(attacker);

        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "smiths_regret_grace",
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        SkillDefinition attackSkill = fixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId];
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        BattleAttackRollModifierBundle bundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                attackSkill,
                "skill_attack_check",
                "smiths_regret_grace",
                force_hit_no_crit: false
            )
        );
        _test.True(
            HasModifier(bundle, MoradinForgivenessBindingId, 2),
            "grace 状态存在时，下一次本武器攻击检定应获得 +2。"
        );
        _test.Eq(
            attacker.GetStatusEffect(GraceStatusId)?.stacks ?? 0,
            1,
            "只构建攻击预览/检定包不应消耗 grace。"
        );

        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "smiths_regret_grace_miss",
            previewCommand: false
        );
        _test.Eq(
            attacker.GetStatusEffect(GraceStatusId)?.stacks ?? 0,
            0,
            "真实本武器攻击即使命中失败，也应消耗 grace。"
        );
    }

    private static bool HasReaction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityTriggerKind trigger,
        EquipmentAbilityTimingKind timing
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
            if (reaction?.Trigger == trigger && reaction.Timing == timing)
                return true;
        return false;
    }

    private static bool AttackBonusRequiresWeaponDamage(EquipmentAbilityBindingDefinition binding)
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is AttackRollBonusActionPayloadDefinition payload
                    && payload.RequireWeaponDamage
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName bindingId,
        int modifier
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec?.source_id == bindingId && spec.modifier_delta == modifier)
                return true;
        }
        return false;
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
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static void ApplyGraceStatus(BattleUnitState unit)
    {
        BattleStatusEffectState status = BattleStatusSemanticTable.MergeStatus(
            BattleRuntimeEffectDefinitions.Status(
                GraceStatusId,
                1,
                60,
                stackBehavior: "refresh",
                stackLimit: 1,
                displayName: "摩拉丁的谅解"
            ),
            unit.unit_id,
            unit.GetStatusEffect(GraceStatusId),
            GraceStatusId
        );
        unit.SetStatusEffect(status);
    }

    private sealed class SmithFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private SmithFixture(
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
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static SmithFixture Build(GArray damageRolls)
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
            return new SmithFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildSmithUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_smiths_regret_{label}")
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

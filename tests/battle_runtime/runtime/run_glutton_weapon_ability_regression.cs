using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_glutton_weapon_ability_regression : SceneTree
{
    private static readonly StringName GluttonItemId = "weapon_unique_axe_glutton_090";
    private static readonly StringName SatedTraitId = "weapon.axe.glutton.sated";
    private static readonly StringName UnsatisfiedTraitId = "weapon.axe.glutton.unsatisfied";
    private static readonly StringName DevouringChopTraitId =
        "weapon.axe.glutton.devouring_chop";
    private static readonly StringName SatedBindingId = "binding.weapon.axe.glutton.sated";
    private static readonly StringName UnsatisfiedBindingId =
        "binding.weapon.axe.glutton.unsatisfied";
    private static readonly StringName DevouringChopBindingId =
        "binding.weapon.axe.glutton.devouring_chop";
    private static readonly StringName HungerStatusId = "glutton_hunger";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGluttonProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestUnsatisfiedAddsHungerAndDevouringChopConsumesItForDamage();
            TestSatedHealsForHalfActualHpDamageOnWeaponKill();
            Quit(_test.Finish("Glutton weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(_test.Finish("Glutton weapon ability regression"));
        }
    }

    private void TestGluttonProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using GluttonFixture fixture = GluttonFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(GluttonItemId), "真实物品内容应包含贪食者。");
        _test.True(fixture.TraitDefs.ContainsKey(SatedTraitId), "真实 trait 内容应包含饱食。");
        _test.True(
            fixture.TraitDefs.ContainsKey(UnsatisfiedTraitId),
            "真实 trait 内容应包含永不满足。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(DevouringChopTraitId),
            "真实 trait 内容应包含吞食斩。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(SatedBindingId),
            "真实装备能力内容应包含饱食 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(UnsatisfiedBindingId),
            "真实装备能力内容应包含永不满足 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DevouringChopBindingId),
            "真实装备能力内容应包含吞食斩 binding。"
        );

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_glutton.tres"
        );
        _test.True(rawItem != null, "贪食者原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "贪食者", "贪食者显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "贪食者应继承 greataxe 模板。"
            );
            _test.Eq(rawItem.base_price, 42000, "贪食者价格应为 42000。");
            _test.True(rawItem.trait_ids.Contains(SatedTraitId), "贪食者物品应声明饱食。");
            _test.True(rawItem.trait_ids.Contains(UnsatisfiedTraitId), "贪食者物品应声明永不满足。");
            _test.True(rawItem.trait_ids.Contains(DevouringChopTraitId), "贪食者物品应声明吞食斩。");
            _test.False(
                ContainsText(rawItem.description, "长休")
                    || ContainsText(rawItem.description, "三日")
                    || ContainsText(rawItem.description, "necrotic"),
                "玩家说明不应包含已延后的长休、三日或英文负能量反噬文本。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildGluttonUnit("projection");
        _test.Eq(equipped.weapon_item_id, GluttonItemId, "贪食者装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("greataxe"),
            "贪食者应投影为 greataxe。"
        );
        _test.Eq(equipped.weapon_family, new StringName("axe"), "贪食者应投影为 axe family。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_slash"),
            "贪食者应为斩击伤害。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "贪食者攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "贪食者应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "贪食者应为 1D12+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 12, "贪食者应为 1D12+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "贪食者应为 1D12+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            SatedTraitId,
            SatedBindingId,
            "eq_glutton_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            UnsatisfiedTraitId,
            UnsatisfiedBindingId,
            "eq_glutton_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DevouringChopTraitId,
            DevouringChopBindingId,
            "eq_glutton_projection"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, SatedBindingId, "heal_from_fact"),
            "饱食必须由通用 heal_from_fact action 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, UnsatisfiedBindingId, "apply_status"),
            "永不满足必须由 apply_status action 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, DevouringChopBindingId, "add_damage_dice"),
            "吞食斩必须由 add_damage_dice action 配置声明。"
        );
        _test.True(
            BindingHasActionKind(fixture.Bindings, DevouringChopBindingId, "consume_status_stacks"),
            "吞食斩造成伤害后必须清除已消耗的饥饿层数。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除贪食者后 weapon_item_id 应清空。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除贪食者后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除贪食者后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestUnsatisfiedAddsHungerAndDevouringChopConsumesItForDamage()
    {
        using GluttonFixture fixture = GluttonFixture.Build(new GArray { 10, 10, 4 });
        BattleUnitState attacker = fixture.BuildGluttonUnit("hunger");
        BattleUnitState firstTarget = BuildEnemy("glutton_first_target", new Vector2I(1, 0), hp: 50);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            firstTarget,
            "glutton_hunger_first",
            previewCommand: false
        );
        _test.Eq(38, firstTarget.current_hp, "第一次未击杀命中应只造成贪食者武器 1D12+2。");
        BattleStatusEffectState hunger = attacker.GetStatusEffect(HungerStatusId);
        _test.True(hunger != null, "未击杀且造成 HP 伤害后，持有者应获得饥饿。");
        if (hunger != null)
        {
            _test.Eq(hunger.stacks, 1, "第一次未击杀应获得 1 层饥饿。");
            _test.Eq(hunger.duration, 120, "饥饿应持续 120TU。");
            _test.Eq(hunger.source_unit_id, attacker.unit_id, "饥饿应记录持有者来源。");
        }

        BattleUnitState secondTarget = BuildEnemy("glutton_second_target", new Vector2I(1, 0), hp: 50);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            secondTarget,
            "glutton_devouring_second",
            previewCommand: false
        );
        _test.Eq(
            34,
            secondTarget.current_hp,
            "带 1 层饥饿的下一次命中应造成武器 1D12+2 与吞食斩 1D6。"
        );
        hunger = attacker.GetStatusEffect(HungerStatusId);
        _test.True(hunger != null, "吞食斩消耗后，未击杀命中应重新获得 1 层饥饿。");
        if (hunger != null)
        {
            _test.Eq(hunger.stacks, 1, "吞食斩应先消耗旧饥饿，再由未击杀命中刷新为 1 层。");
            _test.Eq(hunger.duration, 120, "刷新后的饥饿仍应持续 120TU。");
        }
    }

    private void TestSatedHealsForHalfActualHpDamageOnWeaponKill()
    {
        using GluttonFixture fixture = GluttonFixture.Build(new GArray { 10 });
        BattleUnitState attacker = fixture.BuildGluttonUnit("sated");
        attacker.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        attacker.current_hp = 40;
        BattleUnitState target = BuildEnemy("glutton_kill_target", new Vector2I(1, 0), hp: 12);

        IssueBasicAttackWithAttackerHp(
            fixture.Runtime,
            attacker,
            target,
            "glutton_sated_kill",
            attackerHp: 40
        );

        _test.False(target.is_alive, "贪食者这一击应击杀目标。");
        _test.Eq(
            46,
            attacker.current_hp,
            "饱食应按本次实际 HP 伤害 12 的 50% 向下取整，治疗 6 点。"
        );
        _test.False(
            attacker.HasStatusEffect(HungerStatusId),
            "击杀触发饱食时不应再按未击杀路径获得饥饿。"
        );
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedEquipmentInstanceId
    )
    {
        if (!unit.effective_trait_ids.Contains(traitId))
        {
            throw new InvalidOperationException($"unit missing trait {traitId}");
        }
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (
                source != null
                && source.AbilityIds?.Contains(bindingId) == true
                && source.SourceEquipmentInstanceId == expectedEquipmentInstanceId
            )
            {
                return;
            }
        }
        throw new InvalidOperationException($"unit missing equipment ability source {bindingId}");
    }

    private static bool BindingHasActionKind(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId,
        StringName actionKind
    )
    {
        if (!bindings.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions)
            {
                if (action?.Kind == actionKind)
                    return true;
            }
        }
        return false;
    }

    private static bool ContainsText(string value, string needle) =>
        !string.IsNullOrEmpty(value)
        && !string.IsNullOrEmpty(needle)
        && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = hp,
            coord = coord,
            body_size = 1,
            body_size_category = "medium",
        };
        unit.SetCombatResources(hp, 0, 30, 0, 2, 2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.SetAnchorCoord(coord);
        unit.RefreshFootprint();
        return unit;
    }

    private static void IssueBasicAttackWithAttackerHp(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int attackerHp
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        attacker.SetCurrentHp(Math.Max(attackerHp, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target
        );
        runtime.SetupStateForTests(state);
        runtime.IssueCommand(WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(attacker, target));
    }

    private sealed class GluttonFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private GluttonFixture(
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

        internal static GluttonFixture Build(GArray damageRolls = null)
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
                new FixedRollDamageResolver(damageRolls ?? new GArray { 10, 4 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new GluttonFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildGluttonUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                GluttonItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(GluttonItemId, $"eq_glutton_{label}")
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

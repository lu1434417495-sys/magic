using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_butcher_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ButcherItemId = "weapon_unique_axe_butcher_094";
    private static readonly StringName SlaughterArtTraitId = "weapon.axe.butcher.slaughter_art";
    private static readonly StringName CutTheJointTraitId = "weapon.axe.butcher.cut_the_joint";
    private static readonly StringName BloodDiscomfortTraitId =
        "weapon.axe.butcher.blood_discomfort";
    private static readonly StringName SlaughterArtBindingId =
        "binding.weapon.axe.butcher.slaughter_art";
    private static readonly StringName CutTheJointBindingId =
        "binding.weapon.axe.butcher.cut_the_joint";
    private static readonly StringName BloodDiscomfortBindingId =
        "binding.weapon.axe.butcher.blood_discomfort";
    private static readonly StringName NauseatedStatusId = "nauseated";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestButcherProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestButcherAddsDamageDiceAgainstBeastAndAnimal();
            TestCutTheJointAddsAttackRollBonusAgainstWoundedTargets();
            TestSlaughterArtDoublesOnlyHolderKillLootAtKillCollection();
            TestBloodDiscomfortAppliesOnlyToHolderAfterHumanoidKillFailedSave();
            RequestTestExit(_test.Finish("Butcher weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Butcher weapon ability regression"));
        }
    }

    private void TestButcherProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using ButcherFixture fixture = ButcherFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(ButcherItemId), "真实物品内容应包含屠夫。");
        _test.True(
            fixture.TraitDefs.ContainsKey(SlaughterArtTraitId),
            "真实 trait 内容应包含屠宰艺术。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(CutTheJointTraitId),
            "真实 trait 内容应包含庖丁解牛。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(BloodDiscomfortTraitId),
            "真实 trait 内容应包含血的不适。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(SlaughterArtBindingId),
            "真实装备能力内容应包含屠宰艺术 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(CutTheJointBindingId),
            "真实装备能力内容应包含庖丁解牛 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(BloodDiscomfortBindingId),
            "真实装备能力内容应包含血的不适 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(ButcherItemId))
            return;

        ItemDef rawButcher = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_butcher.tres"
        );
        _test.True(rawButcher != null, "屠夫原始资源应能加载。");
        if (rawButcher != null)
        {
            _test.Eq(
                rawButcher.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "屠夫原始资源应声明继承 greataxe 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildButcherUnit("projection");

        _test.Eq(equipped.weapon_item_id, ButcherItemId, "屠夫装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("greataxe"),
            "屠夫应投影为 greataxe。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "屠夫攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "屠夫应占用双手。");
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_count ?? 0,
            1,
            "屠夫双手骰数量应为 1。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_sides ?? 0,
            12,
            "屠夫双手骰面应为 12。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.flat_bonus ?? 0,
            2,
            "屠夫双手骰固定加值应为 +2。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            SlaughterArtTraitId,
            SlaughterArtBindingId,
            "eq_butcher_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CutTheJointTraitId,
            CutTheJointBindingId,
            "eq_butcher_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            BloodDiscomfortTraitId,
            BloodDiscomfortBindingId,
            "eq_butcher_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除屠夫后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除屠夫后装备能力源应清空。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除屠夫后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestButcherAddsDamageDiceAgainstBeastAndAnimal()
    {
        using ButcherFixture beastFixture = ButcherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState beastAttacker = beastFixture.BuildButcherUnit("damage_beast");
        BattleUnitState beast = BuildTarget("beast_target", new Vector2I(1, 0), "beast");
        beast.current_hp = 100;
        beast.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            beastFixture.Runtime,
            beastAttacker,
            beast,
            "butcher_damage_beast",
            previewCommand: false
        );
        _test.Eq(
            100 - beast.current_hp,
            12,
            "屠夫真实基础攻击命中 beast 时应造成武器 1D12+2 加屠宰艺术 2D6。"
        );

        using ButcherFixture animalFixture = ButcherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState animalAttacker = animalFixture.BuildButcherUnit("damage_animal");
        BattleUnitState animal = BuildTarget("animal_target", new Vector2I(1, 0), "animal");
        animal.current_hp = 100;
        animal.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            animalFixture.Runtime,
            animalAttacker,
            animal,
            "butcher_damage_animal",
            previewCommand: false
        );
        _test.Eq(
            100 - animal.current_hp,
            12,
            "屠夫真实基础攻击命中 animal 时应同样追加屠宰艺术 2D6。"
        );

        using ButcherFixture humanoidFixture = ButcherFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState humanoidAttacker = humanoidFixture.BuildButcherUnit("damage_humanoid");
        BattleUnitState humanoid = BuildTarget("humanoid_target", new Vector2I(1, 0), "humanoid");
        humanoid.current_hp = 100;
        humanoid.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            humanoidFixture.Runtime,
            humanoidAttacker,
            humanoid,
            "butcher_damage_humanoid",
            previewCommand: false
        );
        _test.Eq(
            100 - humanoid.current_hp,
            7,
            "屠夫真实基础攻击命中 humanoid 时只应造成武器 1D12+2，不应追加屠宰艺术。"
        );
    }

    private void TestCutTheJointAddsAttackRollBonusAgainstWoundedTargets()
    {
        using ButcherFixture fixture = ButcherFixture.Build();
        BattleUnitState attacker = fixture.BuildButcherUnit("attack_bonus");
        BattleUnitState wounded = BuildTarget("wounded_target", new Vector2I(1, 0), "humanoid");
        wounded.attribute_snapshot.SetValue(AttributeService.HP_MAX, 40);
        wounded.current_hp = 19;
        BattleUnitState healthy = BuildTarget("healthy_target", new Vector2I(1, 0), "humanoid");
        healthy.attribute_snapshot.SetValue(AttributeService.HP_MAX, 40);
        healthy.current_hp = 20;

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");

        BattleAttackRollModifierBundle woundedBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                wounded,
                attackSkill,
                "skill_attack_check",
                "butcher_test",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle healthyBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                healthy,
                attackSkill,
                "skill_attack_check",
                "butcher_test",
                force_hit_no_crit: false
            )
        );

        _test.Eq(woundedBundle.TotalBonus, 2, "庖丁解牛应对 HP 低于 50% 目标提供 +2。");
        _test.True(
            HasModifier(woundedBundle, CutTheJointBindingId, 2),
            "庖丁解牛 +2 应在 modifier breakdown 中标明装备能力来源。"
        );
        _test.Eq(healthyBundle.TotalBonus, 0, "HP 等于 50% 的目标不应触发庖丁解牛。");
    }

    private void TestSlaughterArtDoublesOnlyHolderKillLootAtKillCollection()
    {
        using ButcherFixture fixture = ButcherFixture.Build();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleUnitState butcherKiller = fixture.BuildButcherUnit("loot_holder");
        BattleUnitState plainKiller = BuildPlainKiller("plain_killer");
        BattleUnitState butcherKill = BuildDefeatedEnemyUnit(
            "butcher_kill_beast",
            "butcher_loot_beast",
            "beast"
        );
        BattleUnitState plainKill = BuildDefeatedEnemyUnit(
            "plain_kill_beast",
            "plain_loot_beast",
            "beast"
        );

        runtime._collect_defeated_unit_loot(butcherKill, butcherKiller);
        runtime._collect_defeated_unit_loot(plainKill, plainKiller);

        _test.Eq(
            CountLootQuantity(runtime._active_loot_entries, "beast_hide", "butcher_kill_beast"),
            4,
            "Butcher 持有者击杀 beast 时，该单位掉落应在击杀收集阶段翻倍。"
        );
        _test.Eq(
            CountLootQuantity(runtime._active_loot_entries, "beast_hide", "plain_kill_beast"),
            2,
            "非 Butcher 持有者击杀 beast 时，不应被战斗结算统一翻倍。"
        );

        BattleState state = new()
        {
            battle_id = "butcher_loot_resolution",
            winner_faction_id = "player",
        };
        runtime.SetupStateForTests(state);
        BattleResolutionResult resolution = runtime._build_battle_resolution_result();
        _test.Eq(
            CountLootQuantity(resolution.loot_entries, "beast_hide", "butcher_kill_beast"),
            4,
            "战斗结算应只汇总已收集的 Butcher 翻倍结果，不二次计算。"
        );
        _test.Eq(
            CountLootQuantity(resolution.loot_entries, "beast_hide", "plain_kill_beast"),
            2,
            "战斗结算不应把非持有者击杀结果统一翻倍。"
        );
    }

    private void TestBloodDiscomfortAppliesOnlyToHolderAfterHumanoidKillFailedSave()
    {
        using ButcherFixture fixture = ButcherFixture.Build();
        _test.False(
            BattleStatusSemanticTable.HasSemantic(NauseatedStatusId),
            "屠夫恶心状态语义应由装备配置提供，不应硬编码在全局状态表。"
        );
        BattleEquipmentAbilityRuntimeService abilityRuntime =
            fixture.Runtime.GetEquipmentAbilityRuntimeService();
        BattleUnitState butcherFail = fixture.BuildButcherUnit("blood_fail");
        PrimeWillSave(butcherFail);
        BattleUnitState butcherSuccess = fixture.BuildButcherUnit("blood_success");
        PrimeWillSave(butcherSuccess);
        BattleUnitState butcherBeast = fixture.BuildButcherUnit("blood_beast");
        PrimeWillSave(butcherBeast);
        BattleUnitState plainKiller = BuildPlainKiller("plain_blood");
        PrimeWillSave(plainKiller);
        BattleUnitState humanoidKill = BuildDefeatedEnemyUnit(
            "blood_humanoid",
            "butcher_loot_humanoid",
            "humanoid"
        );
        BattleUnitState beastKill = BuildDefeatedEnemyUnit(
            "blood_beast",
            "butcher_loot_beast",
            "beast"
        );

        abilityRuntime.ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = butcherFail,
                DefeatedUnit = humanoidKill,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );
        BattleStatusEffectState nausea = butcherFail.GetStatusEffect(NauseatedStatusId);
        _test.True(nausea != null, "Butcher 持有者击杀 humanoid 且意志豁免失败时应获得恶心。");
        _test.Eq(nausea?.duration ?? -1, 60, "血的不适恶心持续时间应为 60TU。");
        _test.Eq(nausea?.stack_behavior ?? new StringName(""), new StringName("refresh"), "恶心应由配置声明刷新叠层。");
        _test.Eq(nausea?.stack_limit ?? 0, 1, "恶心应由配置声明最多 1 层。");
        _test.Eq(nausea?.display_label ?? "", "恶心", "恶心显示名应来自屠夫装备配置。");
        _test.True(nausea?.counts_as_debuff == true, "恶心应由配置声明为 debuff。");
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(nausea),
            "恶心应由配置声明为可驱散 harmful magic。"
        );
        _test.Eq(
            BattleStatusSemanticTable.GetAttackRollPenalty(nausea),
            2,
            "恶心应通过正式状态语义造成攻击检定 -2。"
        );

        abilityRuntime.ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = butcherSuccess,
                DefeatedUnit = humanoidKill,
                SaveContext = BattleSaveContext.WithSaveRollOverride(20),
            }
        );
        _test.False(
            butcherSuccess.HasStatusEffect(NauseatedStatusId),
            "意志豁免成功时不应获得恶心。"
        );

        abilityRuntime.ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = butcherBeast,
                DefeatedUnit = beastKill,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );
        _test.False(
            butcherBeast.HasStatusEffect(NauseatedStatusId),
            "击杀非 humanoid 不应触发血的不适。"
        );

        abilityRuntime.ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = plainKiller,
                DefeatedUnit = humanoidKill,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );
        _test.False(
            plainKiller.HasStatusEffect(NauseatedStatusId),
            "非 Butcher 持有者击杀 humanoid 不应触发血的不适。"
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
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, StringName tag)
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
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.creature_type_tags.Add(tag);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleUnitState BuildPlainKiller(StringName unitId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            is_alive = true,
        };
    }

    private static void PrimeWillSave(BattleUnitState unit)
    {
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
    }

    private static BattleUnitState BuildDefeatedEnemyUnit(
        StringName unitId,
        StringName templateId,
        StringName creatureTag
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            enemy_template_id = templateId,
            display_name = unitId.ToString(),
            faction_id = "hostile",
            control_mode = "ai",
            is_alive = false,
        };
        unit.creature_type_tags.Add(creatureTag);
        return unit;
    }

    private static int CountLootQuantity(
        IEnumerable<BattleLootEntry> lootEntries,
        StringName itemId,
        StringName sourceId
    )
    {
        int total = 0;
        foreach (BattleLootEntry entry in lootEntries ?? Array.Empty<BattleLootEntry>())
        {
            if (
                entry != null
                && entry.DropKind == BattleLootDropKind.Item
                && entry.ItemId == itemId
                && entry.SourceId == sourceId
            )
            {
                total += entry.Quantity;
            }
        }
        return total;
    }

    private sealed class ButcherFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ButcherFixture(
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

        internal static ButcherFixture Build(GArray damageRolls = null)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            Dictionary<StringName, EnemyTemplateDef> enemyTemplates = new()
            {
                ["butcher_loot_beast"] = BuildEnemyTemplate("butcher_loot_beast"),
                ["plain_loot_beast"] = BuildEnemyTemplate("plain_loot_beast"),
                ["butcher_loot_humanoid"] = BuildEnemyTemplate("butcher_loot_humanoid"),
            };
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
                enemy_templates: enemyTemplates,
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls ?? new GArray { 3, 4 })
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new ButcherFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildButcherUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ButcherItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(ButcherItemId, $"eq_butcher_{label}")
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

        private static EnemyTemplateDef BuildEnemyTemplate(StringName templateId)
        {
            EnemyTemplateDef template = new()
            {
                template_id = templateId,
                display_name = templateId.ToString(),
            };
            template.drop_entries.Add(new DropEntryDef
            {
                drop_entry_id = "hide_bundle",
                drop_type = "item",
                item_id = "beast_hide",
                quantity = 2,
            });
            return template;
        }
    }
}

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_courage_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_longsword_courage";
    private static readonly StringName FearlessTraitId = "weapon.sword.courage.fearless";
    private static readonly StringName InspireTraitId = "weapon.sword.courage.inspire";
    private static readonly StringName CourageChargeTraitId = "weapon.sword.courage.courage_charge";
    private static readonly StringName LonelyCowardiceTraitId = "weapon.sword.courage.lonely_cowardice";
    private static readonly StringName FearlessStatusId = "courage_fearless";
    private static readonly StringName InspiredStatusId = "courage_inspired";
    private static readonly StringName FrightenedStatusId = "frightened";
    private static readonly StringName InspireSkillId = "weapon_sword_courage_inspire";
    private static readonly StringName InspireGrantId = "grant.courage.inspire.skill";
    private static readonly StringName InspireBindingId = "binding.weapon.sword.courage.inspire";
    private static readonly StringName CourageChargeBindingId =
        "binding.weapon.sword.courage.courage_charge";
    private static readonly StringName LonelyCowardiceBindingId =
        "binding.weapon.sword.courage.lonely_cowardice";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestContentLoadsAndFearlessProjectsWhileEquipped();
            TestInspireGrantsOneShotAttackAndSaveBonusForSixtyTu();
            TestCourageChargeAndLonelyCowardiceUseNearbyAllies();
            RequestTestExit(_test.Finish("Courage weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Courage weapon ability regression"));
        }
    }

    private void TestContentLoadsAndFearlessProjectsWhileEquipped()
    {
        using CourageFixture fixture = CourageFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含勇气之刃。");
        foreach (
            StringName traitId in new[]
            {
                FearlessTraitId,
                InspireTraitId,
                CourageChargeTraitId,
                LonelyCowardiceTraitId,
            }
        )
        {
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"勇气之刃应包含 trait {traitId}。");
        }
        _test.True(fixture.Bindings.ContainsKey(InspireBindingId), "鼓舞应通过装备能力授予技能。");
        _test.True(
            fixture.Bindings.ContainsKey(CourageChargeBindingId),
            "勇气冲锋应有装备能力 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(LonelyCowardiceBindingId),
            "孤独的懦弱应有装备能力 binding。"
        );
        _test.True(fixture.SkillDefs.ContainsKey(InspireSkillId), "鼓舞应落成真实 SkillDef。");

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_courage.tres"
        );
        _test.True(rawItem != null, "勇气之刃原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "勇气之刃 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "勇气之刃", "勇气之刃显示名应匹配设计源。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_longsword_base"),
                "勇气之刃应继承 longsword 模板。"
            );
            _test.Eq(rawItem.base_price, 72000, "勇气之刃价格应为 72000。");
            _test.Eq(rawItem.trait_ids.Count, 4, "勇气之刃应显式挂载 4 个特性。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildCourageUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "勇气之刃应投影为 longsword。");
        _test.Eq(equipped.weapon_attack_range, 1, "勇气之刃攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "勇气之刃应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "勇气之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "勇气之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "勇气之刃单手应为 1D8+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "勇气之刃双手应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "勇气之刃双手应为 1D10+3。");

        AssertUnitHasTrait(equipped, FearlessTraitId);
        AssertUnitHasTraitAndAbilitySource(equipped, InspireTraitId, InspireBindingId, "eq_courage_projection");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CourageChargeTraitId,
            CourageChargeBindingId,
            "eq_courage_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            LonelyCowardiceTraitId,
            LonelyCowardiceBindingId,
            "eq_courage_projection"
        );

        BattleStatusEffectState fearless = equipped.GetStatusEffect(FearlessStatusId);
        _test.True(fearless != null, "无畏应通过 trait passive 投射持久在线状态。");
        _test.False(
            BattleStatusSemanticTable.HasSemantic(FearlessStatusId),
            "无畏专属状态不应写入通用状态语义表。"
        );
        if (fearless != null)
        {
            _test.Eq(fearless.duration, -1, "无畏是装备期间被动，不应有自然 TU 衰减。");
            _test.True(fearless.undispellable, "装备被动免疫状态不应被驱散。");
            _test.True(
                fearless.save_immunity_tags.Contains("frightened"),
                "无畏应声明 frightened save immunity。"
            );
        }
        _test.True(BattleSaveResolver.IsImmune(equipped, "frightened"), "无畏应让持有者免疫 fear/frightened。");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除勇气之刃后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除勇气之刃后武器 profile 应回到装备前状态。"
        );
        _test.False(equipped.HasStatusEffect(FearlessStatusId), "移除勇气之刃后无畏被动状态应移除。");
        _test.False(BattleSaveResolver.IsImmune(equipped, "frightened"), "移除勇气之刃后不应保留 fear 免疫。");
    }

    private void TestInspireGrantsOneShotAttackAndSaveBonusForSixtyTu()
    {
        using CourageFixture fixture = CourageFixture.Build(new GArray());
        if (!fixture.SkillDefs.ContainsKey(InspireSkillId))
        {
            _test.Fail("鼓舞 SkillDef 缺失，无法验证主动技能。");
            return;
        }

        BattleUnitState holder = fixture.BuildCourageUnit("inspire");
        BattleUnitState ally = BuildAlly("courage_inspired_ally", new Vector2I(0, 2));
        BattleUnitState enemy = BuildEnemy("courage_inspired_enemy", new Vector2I(1, 2), hp: 100);
        BattleState state = BuildState("courage_inspire", holder, enemy, ally);
        fixture.Runtime.SetupStateForTests(state);
        holder.SetCombatResources(80, 0, 60, 0, 2, 2);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(fixture, holder, InspireSkillId, state);
        _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "鼓舞来源应是 equipment_skill。");
        _test.Eq(entry.EquipmentGrantedActionId, InspireGrantId, "鼓舞 grant id 应稳定。");
        _test.True(entry.IsSelectable, "鼓舞装备技能应可选。");

        SkillDefinition inspire = fixture.SkillDefs[InspireSkillId];
        _test.Eq(inspire.CombatProfile?.RangeValue ?? -1, 6, "鼓舞 30 尺应落成 6 格射程。");
        _test.Eq(inspire.CombatProfile?.ApCost ?? -1, 0, "bonus action 映射为 0AP。");
        _test.Eq(inspire.CombatProfile?.CooldownTu ?? -1, 60, "鼓舞冷却必须是 60TU。");

        BattleCommand command = BuildUnitSkillCommand(holder, ally, entry);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"鼓舞应允许选择 6 格内盟友。logs={JoinLogs(preview)}");
        int apBefore = holder.current_ap;
        fixture.Runtime.IssueCommand(command);
        _test.Eq(holder.current_ap, apBefore, "鼓舞为 0AP，不应消耗 AP。");
        _test.Eq(holder.GetCooldownTyped(InspireSkillId), 60, "鼓舞使用后应设置 60TU 冷却。");

        BattleStatusEffectState inspired = ally.GetStatusEffect(InspiredStatusId);
        _test.True(inspired != null, "鼓舞应给盟友施加 courage_inspired。");
        _test.False(
            BattleStatusSemanticTable.HasSemantic(InspiredStatusId),
            "鼓舞专属状态不应写入通用状态语义表。"
        );
        if (inspired != null)
        {
            _test.Eq(inspired.duration, 60, "鼓舞状态应持续 60TU。");
            _test.Eq(inspired.save_bonus, 2, "鼓舞应给下一次豁免 +2。");
            _test.True(
                BattleStatusSemanticTable.IsDispellableBeneficialStatusEntry(inspired),
                "鼓舞应由 SkillDef typed 字段声明为可驱散增益。"
            );
        }

        WeaponAbilityCommandTestSupport.PrimeBasicAttack(ally);
        SkillDefinition basicAttack = fixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId];
        AttackCheckInput inspiredCheck = BuildAttackCheck(fixture, state, ally, enemy, basicAttack);
        BattleUnitState plainAlly = BuildAlly("courage_plain_ally", ally.coord);
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(plainAlly);
        AttackCheckInput plainCheck = BuildAttackCheck(fixture, state, plainAlly, enemy, basicAttack);
        _test.Eq(
            inspiredCheck.RequiredRoll,
            plainCheck.RequiredRoll - 2,
            "courage_inspired 应让下一次攻击检定获得 +2。"
        );

        IssueBasicAttackInCurrentState(fixture.Runtime, ally, enemy);
        _test.False(ally.HasStatusEffect(InspiredStatusId), "真实攻击检定提交后应消耗 courage_inspired。");

        using CourageFixture saveFixture = CourageFixture.Build(new GArray());
        BattleUnitState saveHolder = saveFixture.BuildCourageUnit("inspire_save");
        BattleUnitState saveAlly = BuildAlly("courage_save_ally", new Vector2I(0, 2));
        BattleUnitState saveEnemy = BuildEnemy("courage_save_enemy", new Vector2I(1, 2), hp: 100);
        BattleState saveState = BuildState("courage_inspire_save", saveHolder, saveEnemy, saveAlly);
        saveFixture.Runtime.SetupStateForTests(saveState);
        BattleAvailableSkillEntry saveEntry = FindRequiredEquipmentSkill(
            saveFixture,
            saveHolder,
            InspireSkillId,
            saveState
        );
        saveFixture.Runtime.IssueCommand(BuildUnitSkillCommand(saveHolder, saveAlly, saveEntry));
        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            saveHolder,
            saveAlly,
            BattleRuntimeEffectDefinitions.StaticSave(14, "willpower", "frightened"),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(saveResult.Bonus, 2, "courage_inspired 应让下一次豁免获得 +2。");
        _test.False(saveAlly.HasStatusEffect(InspiredStatusId), "真实豁免检定提交后应消耗 courage_inspired。");
    }

    private void TestCourageChargeAndLonelyCowardiceUseNearbyAllies()
    {
        using CourageFixture fixture = CourageFixture.Build(new GArray { 4, 3, 3, 4 });
        BattleUnitState holder = fixture.BuildCourageUnit("charge");
        BattleUnitState target = BuildEnemy("courage_frightened_target", new Vector2I(1, 0), hp: 100);
        target.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = FrightenedStatusId,
            source_unit_id = holder.unit_id,
            duration = 60,
            power = 1,
            stacks = 1,
        });
        BattleUnitState ally = BuildAlly("courage_nearby_ally", new Vector2I(0, 2));
        BattleState state = BuildState("courage_charge", holder, target, ally);
        fixture.Runtime.SetupStateForTests(state);

        int damage = IssueBasicAttackInCurrentState(fixture.Runtime, holder, target);
        _test.Eq(damage, 13, "有 6 格内盟友且目标 frightened 时，应造成 1D8+3 加 2D6 physical_slash。");

        using CourageFixture lonelyFixture = CourageFixture.Build(new GArray());
        BattleUnitState lonelyHolder = lonelyFixture.BuildCourageUnit("lonely");
        BattleUnitState lonelyTarget = BuildEnemy("courage_lonely_target", new Vector2I(1, 0), hp: 100);
        BattleState lonelyState = BuildState("courage_lonely", lonelyHolder, lonelyTarget);
        lonelyFixture.Runtime.SetupStateForTests(lonelyState);
        SkillDefinition basicAttack = lonelyFixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId];
        BattleAttackRollModifierBundle lonelyBundle = BuildModifierBundle(
            lonelyFixture,
            lonelyState,
            lonelyHolder,
            lonelyTarget,
            basicAttack
        );
        _test.Eq(lonelyBundle.GetEffectiveModifierDelta(), -2, "6 格内无盟友时攻击检定应 -2。");
        _test.True(
            HasModifier(lonelyBundle, LonelyCowardiceBindingId, -2),
            "孤独的懦弱 -2 应进入装备来源 modifier breakdown。"
        );

        BattleUnitState nearAlly = BuildAlly("courage_lonely_near_ally", new Vector2I(0, 2));
        AddUnitToState(lonelyFixture.Runtime, lonelyState, nearAlly);
        BattleAttackRollModifierBundle withAllyBundle = BuildModifierBundle(
            lonelyFixture,
            lonelyState,
            lonelyHolder,
            lonelyTarget,
            basicAttack
        );
        _test.Eq(withAllyBundle.GetEffectiveModifierDelta(), 0, "6 格内有盟友时孤独的懦弱不应生效。");
    }

    private static AttackCheckInput BuildAttackCheck(
        CourageFixture fixture,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target,
        SkillDefinition skill
    )
    {
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        return attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                skill,
                "skill_attack_check",
                "courage_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
    }

    private static BattleAttackRollModifierBundle BuildModifierBundle(
        CourageFixture fixture,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target,
        SkillDefinition skill
    )
    {
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        return attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                skill,
                "skill_attack_check",
                "courage_test",
                force_hit_no_crit: false
            )
        );
    }

    private static BattleCommand BuildUnitSkillCommand(
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry
    )
    {
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = user?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = entry?.EntryRef.SkillId ?? new StringName(""),
            target_unit_id = target?.unit_id ?? new StringName(""),
            target_coord = target?.coord ?? new Vector2I(-1, -1),
        };
        if (target != null)
            command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        CourageFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(
            fixture.SkillDefs,
            fixture.Bindings,
            fixture.ItemDefs
        );
        BattleSkillAvailabilityView view = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                BattleState = state,
                WorldStep = 0,
            }
        );
        foreach (BattleAvailableSkillEntry entry in view.SkillEntries)
        {
            if (entry?.EntryRef.SkillId == skillId)
                return entry;
        }
        throw new InvalidOperationException($"missing equipment skill {skillId}.");
    }

    private static int IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        BattleState state = runtime.GetState();
        if (state != null)
            state.active_unit_id = attacker.unit_id;
        int before = target.current_hp;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            attacker,
            target
        );
        runtime.IssueCommand(command);
        return Math.Max(before - target.current_hp, 0);
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        BattleUnitState target,
        params BattleUnitState[] extraUnits
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(8, 8),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.timeline.current_tu = 0;
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(new GDictionary())
        );
        for (int x = 0; x < state.map_size.X; x++)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            {
                BattleCellState cell = new();
                cell.SetCoord(new Vector2I(x, y));
                state.SetCell(cell);
            }
        }
        AddUnitToState(null, state, holder);
        AddUnitToState(null, state, target);
        foreach (BattleUnitState unit in extraUnits ?? Array.Empty<BattleUnitState>())
            AddUnitToState(null, state, unit);
        state.active_unit_id = holder?.unit_id ?? new StringName("");
        return state;
    }

    private static void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        if (unit.faction_id == "ally" || unit.faction_id == "player")
        {
            if (!state.ally_unit_ids.Contains(unit.unit_id))
                state.ally_unit_ids.Add(unit.unit_id);
        }
        else if (!state.enemy_unit_ids.Contains(unit.unit_id))
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            BattleCellState cell = state.GetCell(coord);
            cell?.SetOccupant(unit.unit_id);
        }
    }

    private static BattleUnitState BuildAlly(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = BuildUnit(unitId, coord, "ally", 80);
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(unit);
        return unit;
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = BuildUnit(unitId, coord, "enemy", hp);
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(unit);
        return unit;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, StringName faction, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = faction,
            is_alive = true,
            current_hp = hp,
            coord = coord,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 18);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue("willpower", 14);
        unit.attribute_snapshot.SetValue("willpower_modifier", 2);
        unit.body_size_category = "medium";
        unit.RefreshFootprint();
        return unit;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (
            BattleAttackRollModifierSpec spec in bundle?.Breakdown
                ?? Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (spec?.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private void AssertUnitHasTrait(BattleUnitState unit, StringName traitId)
    {
        _test.True(unit.effective_trait_ids.Contains(traitId), $"unit 应投影 trait {traitId}。");
    }

    private void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        string equipmentInstanceId
    )
    {
        AssertUnitHasTrait(unit, traitId);
        foreach (
            BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources
                ?? new List<BattleEquipmentAbilitySourceState>()
        )
        {
            if (
                source.AbilityIds.Contains(bindingId)
                && source.SourceEquipmentInstanceId == equipmentInstanceId
            )
            {
                return;
            }
        }
        _test.Fail($"unit 应投影装备能力 {bindingId}，来源装备 {equipmentInstanceId}。");
    }

    private static string JoinLogs(BattlePreview preview) =>
        preview == null ? "" : string.Join(" | ", preview.LogLinesTyped);

    private sealed class CourageFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private CourageFixture(
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
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static CourageFixture Build(GArray damageRolls)
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
            return new CourageFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildCourageUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_courage_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
            BattleUnitState unit = units[0];
            unit.faction_id = "ally";
            unit.coord = new Vector2I(0, 0);
            unit.RefreshFootprint();
            WeaponAbilityCommandTestSupport.PrimeBasicAttack(unit);
            return unit;
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} baseline should build exactly one ally unit.");
            BattleUnitState unit = units[0];
            unit.faction_id = "ally";
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
            return partyState;
        }
    }
}

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_twilight_edge_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName TwilightEdgeItemId =
        "weapon_unique_sword_twilight_edge_005";
    private static readonly StringName TwilightStepTraitId =
        "weapon.sword.twilight_edge.twilight_step";
    private static readonly StringName DayNightBalanceTraitId =
        "weapon.sword.twilight_edge.day_night_balance";
    private static readonly StringName TwilightCutTraitId =
        "weapon.sword.twilight_edge.twilight_cut";
    private static readonly StringName TwilightGuardTraitId =
        "weapon.sword.twilight_edge.twilight_guard";
    private static readonly StringName TwilightStepSkillId =
        "weapon_sword_twilight_edge_twilight_step";
    private static readonly StringName TwilightStepGrantId =
        "grant.twilight_edge.twilight_step.skill";
    private static readonly StringName TwilightStepBindingId =
        "binding.weapon.sword.twilight_edge.twilight_step";
    private static readonly StringName DayNightBalanceBindingId =
        "binding.weapon.sword.twilight_edge.day_night_balance";
    private static readonly StringName TwilightCutBindingId =
        "binding.weapon.sword.twilight_edge.twilight_cut";
    private static readonly StringName TwilightGuardBindingId =
        "binding.weapon.sword.twilight_edge.twilight_guard";

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
            TestTwilightEdgeProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestTwilightStepIsGrantSkillBlockedUntilBattleTu70();
            TestTwilightStepCommitEnablesCutOnlyForCurrentActionTurn();
            TestDayNightBalanceAndGuardUseEnvironmentConfig();
            RequestTestExit(_test.Finish("Twilight Edge weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Twilight Edge weapon ability regression"));
        }
    }

    private void TestTwilightEdgeProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using TwilightEdgeFixture fixture = TwilightEdgeFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(TwilightEdgeItemId), "真实物品内容应包含暮光之刃。");
        _test.True(fixture.TraitDefs.ContainsKey(TwilightStepTraitId), "真实 trait 应包含暮影步。");
        _test.True(
            fixture.TraitDefs.ContainsKey(DayNightBalanceTraitId),
            "真实 trait 应包含昼夜平衡。"
        );
        _test.True(fixture.TraitDefs.ContainsKey(TwilightCutTraitId), "真实 trait 应包含暮光切割。");
        _test.True(fixture.TraitDefs.ContainsKey(TwilightGuardTraitId), "真实 trait 应包含暮光守护。");
        _test.True(
            fixture.Bindings.ContainsKey(TwilightStepBindingId),
            "真实装备能力内容应包含暮影步 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DayNightBalanceBindingId),
            "真实装备能力内容应包含昼夜平衡 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(TwilightCutBindingId),
            "真实装备能力内容应包含暮光切割 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(TwilightGuardBindingId),
            "真实装备能力内容应包含暮光守护 binding。"
        );
        _test.True(fixture.SkillDefs.ContainsKey(TwilightStepSkillId), "真实技能内容应包含暮影步。");
        if (!fixture.ItemDefs.ContainsKey(TwilightEdgeItemId))
            return;

        ItemDef rawTwilightEdge = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_scimitar_twilight_edge.tres"
        );
        _test.True(rawTwilightEdge != null, "暮光之刃原始资源应能加载。");
        if (rawTwilightEdge != null)
        {
            _test.Eq(
                rawTwilightEdge.base_item_id,
                new StringName("weapon_type_scimitar_base"),
                "暮光之刃原始资源应声明继承 scimitar 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildTwilightEdgeUnit("projection");

        _test.Eq(equipped.weapon_item_id, TwilightEdgeItemId, "暮光之刃装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("scimitar"),
            "暮光之刃应投影为 scimitar。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "暮光之刃攻击距离应为 1。");
        _test.False(equipped.weapon_uses_two_hands, "暮光之刃应是单手武器。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "暮光之刃应是 1D6。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 6, "暮光之刃应是 1D6。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "暮光之刃应有 +3 固定伤害。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            TwilightStepTraitId,
            TwilightStepBindingId,
            "eq_twilight_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DayNightBalanceTraitId,
            DayNightBalanceBindingId,
            "eq_twilight_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            TwilightCutTraitId,
            TwilightCutBindingId,
            "eq_twilight_edge_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            TwilightGuardTraitId,
            TwilightGuardBindingId,
            "eq_twilight_edge_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除暮光之刃后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除暮光之刃后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.weapon_attack_range,
            baseline.weapon_attack_range,
            "移除暮光之刃后攻击距离应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除暮光之刃后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除暮光之刃后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestTwilightStepIsGrantSkillBlockedUntilBattleTu70()
    {
        using TwilightEdgeFixture fixture = TwilightEdgeFixture.Build(new GArray());
        _test.True(
            fixture.SkillDefs.TryGetValue(TwilightStepSkillId, out SkillDefinition stepSkill),
            "暮影步应是装备授予 SkillDef。"
        );
        if (stepSkill == null)
            return;

        CombatSkillDefinition combat = stepSkill.CombatProfile;
        _test.True(combat != null, "暮影步应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "暮影步应选择地面格。");
        _test.Eq(combat.TargetTeamFilter, new StringName("ally"), "暮影步应按友方位移技能口径处理。");
        _test.Eq(combat.RangeValue, 3, "暮影步 15 尺应落成 3 格。");
        _test.Eq(combat.ApCost, 1, "暮影步必须消耗 1AP。");
        _test.Eq(combat.CooldownTu, 40, "暮影步冷却应为 40TU。");
        _test.True(HasForcedMoveBlink(combat, 3), "暮影步应通过 forced_move blink 位移 3 格。");

        _test.True(
            fixture.Bindings.TryGetValue(TwilightStepBindingId, out EquipmentAbilityBindingDefinition binding),
            "暮影步 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "暮影步 binding 应授予一个装备技能入口。");
        EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
        _test.Eq(grant.SkillId, TwilightStepSkillId, "暮影步 grant 应指向真实 SkillDef。");
        _test.Eq(grant.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.None, "暮影步次数不由装备 usage 限制。");
        _test.True(grant.AvailabilityConditions != null, "暮影步 70TU 门槛必须由 grant availability 配置。");

        BattleUnitState equipped = fixture.BuildTwilightEdgeUnit("step_gate");
        BattleUnitState target = BuildTarget("step_gate_target", new Vector2I(1, 0));
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);

        BattleState earlyState = BuildState("twilight_step_early", equipped, target, 2, 65);
        BattleSkillAvailabilityView earlyView = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = equipped,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 2,
                BattleState = earlyState,
            }
        );
        _test.True(
            TryFindSkillEntry(earlyView, TwilightStepSkillId, out BattleAvailableSkillEntry earlyEntry),
            "70TU 前暮影步入口仍应存在，供 UI 展示禁用原因。"
        );
        _test.False(earlyEntry.IsSelectable, "战斗时间 65TU 时暮影步不应可选。");
        _test.Eq(
            earlyEntry.DisabledReason,
            new StringName("equipment_skill_availability_blocked"),
            "70TU 前暮影步禁用原因应稳定。"
        );

        BattleState readyState = BuildState("twilight_step_ready", equipped, target, 2, 70);
        BattleSkillAvailabilityView readyView = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = equipped,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 2,
                BattleState = readyState,
            }
        );
        _test.True(
            TryFindSkillEntry(readyView, TwilightStepSkillId, out BattleAvailableSkillEntry readyEntry),
            "70TU 时暮影步入口应能解析。"
        );
        _test.True(readyEntry.IsSelectable, "战斗时间 70TU 后暮影步应可选。");
        _test.Eq(
            readyEntry.EntryRef.SourceKind,
            BattleSkillEntrySourceKind.EquipmentSkill,
            "暮影步入口来源应是 equipment_skill。"
        );
        _test.Eq(readyEntry.EquipmentGrantedActionId, TwilightStepGrantId, "暮影步入口应保留 grant id。");
    }

    private void TestTwilightStepCommitEnablesCutOnlyForCurrentActionTurn()
    {
        using TwilightEdgeFixture fixture = TwilightEdgeFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildTwilightEdgeUnit("cut");
        BattleUnitState target = BuildTarget("cut_target", new Vector2I(1, 0));
        target.current_hp = 160;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 160);
        BattleState state = BuildState("twilight_cut", attacker, target, 2, 70);
        fixture.Runtime.SetupStateForTests(state);

        int beforeStepHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "twilight_cut_before_step",
            worldStep: 2,
            currentTu: 70,
            previewCommand: false
        );
        int beforeStepDamage = beforeStepHp - target.current_hp;

        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        BattleSkillAvailabilityView view = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = attacker,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 2,
                BattleState = state,
            }
        );
        _test.True(
            TryFindSkillEntry(view, TwilightStepSkillId, out BattleAvailableSkillEntry stepEntry),
            "暮影步应能从 unit 的装备技能入口查到。"
        );
        BattleCommand stepCommand = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = attacker.unit_id,
            skill_entry_id = stepEntry.EntryRef.SkillEntryId,
            skill_id = TwilightStepSkillId,
            target_coord = new Vector2I(2, 0),
        };
        _test.True(
            fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(attacker, stepCommand),
            "暮影步成功使用后应提交装备授予技能触发，即使它没有装备 usage 次数。"
        );

        int afterStepHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "twilight_cut_after_step",
            worldStep: 2,
            currentTu: 70,
            previewCommand: false
        );
        int afterStepDamage = afterStepHp - target.current_hp;
        _test.True(
            afterStepDamage > beforeStepDamage,
            "暮影步后的首次真实基础攻击应追加暮光切割 psychic 伤害。"
        );

        int sameTurnSecondHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "twilight_cut_same_turn_second",
            worldStep: 2,
            currentTu: 70,
            previewCommand: false
        );
        int sameTurnSecondDamage = sameTurnSecondHp - target.current_hp;
        _test.Eq(
            sameTurnSecondDamage,
            beforeStepDamage,
            "同一行动轮次内第二次真实基础攻击不应再次触发暮光切割。"
        );

        attacker.ResetPerTurnCharges();
        int nextTurnHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "twilight_cut_next_turn",
            worldStep: 2,
            currentTu: 110,
            previewCommand: false
        );
        int nextTurnDamage = nextTurnHp - target.current_hp;
        _test.Eq(
            nextTurnDamage,
            beforeStepDamage,
            "回合重置后，暮影步给的暮光切割当前轮标记应消失。"
        );
    }

    private void TestDayNightBalanceAndGuardUseEnvironmentConfig()
    {
        using TwilightEdgeFixture fixture = TwilightEdgeFixture.Build(new GArray());
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");

        BattleUnitState dayAttacker = fixture.BuildTwilightEdgeUnit("day_balance");
        BattleUnitState dayTarget = BuildTarget("day_target", new Vector2I(1, 0));
        BattleState dayState = BuildState("twilight_day", dayAttacker, dayTarget, 2, 70);
        fixture.Runtime.SetupStateForTests(dayState);
        dayTarget.current_hp = 100;
        dayTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            dayAttacker,
            dayTarget,
            "twilight_day_balance",
            worldStep: 2,
            currentTu: 70,
            previewCommand: false
        );
        int dayDamage = 100 - dayTarget.current_hp;

        BattleUnitState nightAttacker = fixture.BuildTwilightEdgeUnit("night_balance");
        BattleUnitState nightTarget = BuildTarget("night_target", new Vector2I(1, 0));
        BattleState nightState = BuildStateWithEnvironmentTags(
            "twilight_night",
            nightAttacker,
            nightTarget,
            new GStringNameArray { "night" },
            70
        );
        fixture.Runtime.SetupStateForTests(nightState);
        nightTarget.current_hp = 100;
        nightTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            nightAttacker,
            nightTarget,
            "twilight_night_balance",
            worldStep: 12,
            currentTu: 70,
            previewCommand: false
        );
        int nightDamage = 100 - nightTarget.current_hp;
        _test.True(dayDamage > 0, "白天真实基础攻击应造成伤害。");
        _test.True(
            nightDamage > dayDamage,
            "夜间昼夜平衡真实基础攻击应比白天多 1 点 force 伤害。"
        );

        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        AttackCheckInput nightAttackCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                nightState,
                nightAttacker,
                nightTarget,
                attackSkill,
                "skill_attack_check",
                "twilight_edge_night_advantage",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput nightResolved = fixture.Runtime.GetHitResolver().BuildFateAwareAttackCheckPreview(
            nightState,
            nightAttacker,
            nightTarget,
            nightAttackCheck
        );
        _test.True(nightResolved.IsAdvantage, "夜间昼夜平衡应提供真正 attack advantage。");

        BattleAttackRollModifierBundle attacksHolderAtNight =
            attackPolicy.BuildModifierBundle(
                attackPolicy.BuildSkillDefinitionAttackContext(
                    nightState,
                    nightTarget,
                    nightAttacker,
                    attackSkill,
                    "skill_attack_check",
                    "twilight_edge_guard_night",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(
            attacksHolderAtNight.GetEffectiveModifierDelta(),
            -1,
            "攻击夜间持有暮光之刃的目标时，暮光守护应等价为攻击者 -1。"
        );
        _test.True(
            HasModifier(attacksHolderAtNight, TwilightGuardBindingId, -1),
            "夜间暮光守护 -1 应进入 modifier breakdown。"
        );

        fixture.Runtime.SetupStateForTests(dayState);
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        BattleSkillAvailabilityView dayStepView = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = dayAttacker,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 2,
                BattleState = dayState,
            }
        );
        _test.True(
            TryFindSkillEntry(dayStepView, TwilightStepSkillId, out BattleAvailableSkillEntry dayStepEntry),
            "白天暮影步入口应存在。"
        );
        BattleCommand dayStepCommand = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = dayAttacker.unit_id,
            skill_entry_id = dayStepEntry.EntryRef.SkillEntryId,
            skill_id = TwilightStepSkillId,
            target_coord = new Vector2I(2, 0),
        };
        _test.True(
            fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(dayAttacker, dayStepCommand),
            "白天暮影步成功使用后应设置暮光守护的当前轮暴露状态。"
        );
        BattleAttackRollModifierBundle attacksHolderAfterDayStep =
            attackPolicy.BuildModifierBundle(
                attackPolicy.BuildSkillDefinitionAttackContext(
                    dayState,
                    dayTarget,
                    dayAttacker,
                    attackSkill,
                    "skill_attack_check",
                    "twilight_edge_guard_day_step",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(
            attacksHolderAfterDayStep.GetEffectiveModifierDelta(),
            1,
            "白天使用暮影步后，本行动轮攻击持有者应获得 +1，表达 AC -1。"
        );
        dayAttacker.ResetPerTurnCharges();
        BattleAttackRollModifierBundle attacksHolderAfterReset =
            attackPolicy.BuildModifierBundle(
                attackPolicy.BuildSkillDefinitionAttackContext(
                    dayState,
                    dayTarget,
                    dayAttacker,
                    attackSkill,
                    "skill_attack_check",
                    "twilight_edge_guard_day_reset",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(
            attacksHolderAfterReset.GetEffectiveModifierDelta(),
            0,
            "回合重置后，白天暮影步暴露状态应消失。"
        );
    }

    private static bool HasForcedMoveBlink(CombatSkillDefinition combat, int distance)
    {
        foreach (CombatEffectDefinition effect in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect.EffectType == "forced_move"
                && effect.ForcedMoveMode == "blink"
                && effect.ForcedMoveDistance == distance
            )
            {
                return true;
            }
        }
        return false;
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

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry result
    )
    {
        result = null;
        foreach (BattleAvailableSkillEntry entry in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        int worldStep,
        int currentTu
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(5, 5),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = attacker.unit_id;
        state.timeline.current_tu = currentTu;
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = worldStep }
            )
        );
        AddPlainCells(state);
        state.SetUnit(attacker);
        state.SetUnit(target);
        SetUnitOccupants(state, attacker);
        SetUnitOccupants(state, target);
        state.ally_unit_ids.Add(attacker.unit_id);
        state.enemy_unit_ids.Add(target.unit_id);
        return state;
    }

    private static BattleState BuildStateWithEnvironmentTags(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        GStringNameArray tags,
        int currentTu
    )
    {
        BattleState state = BuildState(battleId, attacker, target, 2, currentTu);
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["global_environment_tags"] = tags }
            )
        );
        return state;
    }

    private static void AddPlainCells(BattleState state)
    {
        if (state == null)
            return;
        for (int x = 0; x < state.map_size.X; x++)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            {
                BattleCellState cell = new();
                cell.SetCoord(new Vector2I(x, y));
                state.SetCell(cell);
            }
        }
    }

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            BattleCellState cell = state.GetCell(coord);
            cell?.SetOccupant(unit.unit_id);
        }
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
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

    private sealed class TwilightEdgeFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private TwilightEdgeFixture(
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
            SkillDefs = snapshot.Skills;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static TwilightEdgeFixture Build(GArray damageRolls)
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
            return new TwilightEdgeFixture(
                characterManagement,
                partyState,
                runtime,
                snapshot
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildTwilightEdgeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                TwilightEdgeItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    TwilightEdgeItemId,
                    $"eq_twilight_edge_{label}"
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

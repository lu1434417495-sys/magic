using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_cowardice_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_shortsword_cowardice";
    private static readonly StringName GapBackstabTraitId =
        "weapon.sword.cowardice.gap_backstab";
    private static readonly StringName FrontalFragilityTraitId =
        "weapon.sword.cowardice.frontal_fragility";
    private static readonly StringName FleeingInstinctTraitId =
        "weapon.sword.cowardice.fleeing_instinct";
    private static readonly StringName CowardlyCounterTraitId =
        "weapon.sword.cowardice.cowardly_counter";
    private static readonly StringName GapBackstabBindingId =
        "binding.weapon.sword.cowardice.gap_backstab";
    private static readonly StringName FrontalFragilityBindingId =
        "binding.weapon.sword.cowardice.frontal_fragility";
    private static readonly StringName FleeingInstinctBindingId =
        "binding.weapon.sword.cowardice.fleeing_instinct";
    private static readonly StringName CowardlyCounterBindingId =
        "binding.weapon.sword.cowardice.cowardly_counter";
    private static readonly StringName ScurrySkillId = "weapon_sword_cowardice_scurry";
    private static readonly StringName ScurryGrantId = "grant.cowardice.scurry.skill";
    private static readonly StringName CounterStatusId = "cowardice_counter_ready";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestCowardiceProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestGapBackstabAndFrontalFragilityUseTargetSupport();
            TestScurryRequiresLowHpAndGrantsSixtyTuCounterAdvantage();
            RequestTestExit(_test.Finish("Cowardice weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Cowardice weapon ability regression"));
        }
    }

    private void TestCowardiceProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using CowardiceFixture fixture = CowardiceFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含懦弱之刃。");
        foreach (
            StringName traitId in new[]
            {
                GapBackstabTraitId,
                FrontalFragilityTraitId,
                FleeingInstinctTraitId,
                CowardlyCounterTraitId,
            }
        )
        {
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"懦弱之刃应包含 trait {traitId}。");
        }
        foreach (
            StringName bindingId in new[]
            {
                GapBackstabBindingId,
                FrontalFragilityBindingId,
                FleeingInstinctBindingId,
                CowardlyCounterBindingId,
            }
        )
        {
            _test.True(fixture.Bindings.ContainsKey(bindingId), $"懦弱之刃应包含 binding {bindingId}。");
        }
        _test.True(fixture.SkillDefs.ContainsKey(ScurrySkillId), "逃窜应落成真实 SkillDef。");

        using TestContentResourceLoader loader = new();
        ItemDef rawItem = loader.LoadCanonical<ItemDef>(
            "res://data/configs/items/weapon_unique_shortsword_cowardice.tres"
        );
        _test.True(rawItem != null, "懦弱之刃原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "懦弱之刃 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "懦弱之刃", "懦弱之刃显示名应匹配设计源。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_shortsword_base"),
                "懦弱之刃应继承 shortsword 模板。"
            );
            _test.Eq(rawItem.base_price, 48000, "懦弱之刃价格应为 48000。");
            _test.Eq(rawItem.trait_ids.Count, 4, "懦弱之刃应显式挂载 4 个特性。");
            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "懦弱之刃应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.one_handed_dice?.dice_count ?? 0, 1, "懦弱之刃应为 1D6+3。");
                _test.Eq(profile.one_handed_dice?.dice_sides ?? 0, 6, "懦弱之刃应为 1D6+3。");
                _test.Eq(profile.one_handed_dice?.flat_bonus ?? 0, 3, "懦弱之刃应为 1D6+3。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "finesse"), "懦弱之刃应保留 finesse。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "light"), "懦弱之刃应保留 light。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildCowardiceUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "懦弱之刃装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("shortsword"), "懦弱之刃应投影为 shortsword。");
        _test.Eq(equipped.weapon_family, new StringName("sword"), "懦弱之刃应投影为 sword family。");
        _test.Eq(equipped.weapon_attack_range, 1, "懦弱之刃攻击距离应为 1。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "懦弱之刃应为穿刺伤害。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "懦弱之刃单手应为 1D6+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 6, "懦弱之刃单手应为 1D6+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "懦弱之刃单手应为 1D6+3。");
        AssertUnitHasTraitAndAbilitySource(equipped, GapBackstabTraitId, GapBackstabBindingId, "eq_cowardice_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, FrontalFragilityTraitId, FrontalFragilityBindingId, "eq_cowardice_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, FleeingInstinctTraitId, FleeingInstinctBindingId, "eq_cowardice_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, CowardlyCounterTraitId, CowardlyCounterBindingId, "eq_cowardice_projection");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除懦弱之刃后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除懦弱之刃后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除懦弱之刃后装备能力源应清空。");
    }

    private void TestGapBackstabAndFrontalFragilityUseTargetSupport()
    {
        using CowardiceFixture isolatedFixture = CowardiceFixture.Build(new GArray { 4, 3, 3 });
        BattleUnitState isolatedAttacker = isolatedFixture.BuildCowardiceUnit("isolated");
        BattleUnitState isolatedTarget = BuildEnemy("cowardice_isolated_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            isolatedFixture.Runtime,
            isolatedAttacker,
            isolatedTarget,
            "cowardice_gap_backstab_isolated",
            previewCommand: false
        );
        _test.Eq(
            100 - isolatedTarget.current_hp,
            13,
            "目标 6 格内无其友军时，缝隙背刺应造成 1D6+3 加 2D6 physical_pierce。"
        );

        using CowardiceFixture supportedFixture = CowardiceFixture.Build(new GArray { 4, 3, 3 });
        BattleUnitState supportedAttacker = supportedFixture.BuildCowardiceUnit("supported");
        BattleUnitState supportedTarget = BuildEnemy("cowardice_supported_target", new Vector2I(1, 0), hp: 100);
        BattleUnitState targetSupport = BuildEnemy("cowardice_target_support", new Vector2I(2, 0), hp: 100);
        BattleState supportedState = BuildState(
            "cowardice_supported_target",
            supportedAttacker,
            supportedTarget,
            targetSupport
        );
        supportedFixture.Runtime.SetupStateForTests(supportedState);

        SkillDefinition basicAttack = supportedFixture.SkillDefs[
            WeaponAbilityCommandTestSupport.BasicAttackSkillId
        ];
        BattleAttackRollModifierBundle fragileBundle = BuildModifierBundle(
            supportedFixture,
            supportedState,
            supportedAttacker,
            supportedTarget,
            basicAttack
        );
        _test.Eq(fragileBundle.GetEffectiveModifierDelta(), -3, "目标有 6 格内友军时，正面脆弱应让攻击检定 -3。");
        _test.True(
            HasModifier(fragileBundle, FrontalFragilityBindingId, -3),
            "正面脆弱 -3 应进入装备来源 modifier breakdown。"
        );

        int beforeSupportedHp = supportedTarget.current_hp;
        IssueBasicAttackInCurrentState(supportedFixture.Runtime, supportedAttacker, supportedTarget);
        _test.Eq(
            beforeSupportedHp - supportedTarget.current_hp,
            7,
            "目标有友军支援时，缝隙背刺不应追加 2D6。"
        );

        supportedAttacker.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = CounterStatusId,
            source_unit_id = supportedAttacker.unit_id,
            power = 1,
            stacks = 1,
            duration = 60,
            attack_roll_advantage = true,
            consume_on_next_attack_check = true,
        });
        BattleAttackRollModifierBundle counterBundle = BuildModifierBundle(
            supportedFixture,
            supportedState,
            supportedAttacker,
            supportedTarget,
            basicAttack
        );
        _test.Eq(counterBundle.GetEffectiveModifierDelta(), 0, "怯懦反击预备存在时，正面脆弱不应生效。");
        AttackCheckInput counterCheck = BuildAttackCheck(
            supportedFixture,
            supportedState,
            supportedAttacker,
            supportedTarget,
            basicAttack
        );
        AttackCheckInput counterResolved =
            supportedFixture.Runtime.GetHitResolver().BuildFateAwareAttackCheckPreview(
                supportedState,
                supportedAttacker,
                supportedTarget,
                counterCheck
            );
        _test.True(counterResolved.IsAdvantage, "怯懦反击预备应提供真正 attack advantage。");
    }

    private void TestScurryRequiresLowHpAndGrantsSixtyTuCounterAdvantage()
    {
        using CowardiceFixture fixture = CowardiceFixture.Build(new GArray { 4 });
        BattleUnitState holder = fixture.BuildCowardiceUnit("scurry");
        BattleUnitState enemy = BuildEnemy("cowardice_scurry_enemy", new Vector2I(3, 0), hp: 100);
        BattleState state = BuildState("cowardice_scurry", holder, enemy);
        fixture.Runtime.SetupStateForTests(state);
        holder.SetCombatResources(80, 0, 60, 0, 2, 2);
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);

        BattleAvailableSkillEntry fullHpEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            ScurrySkillId,
            state
        );
        _test.False(fullHpEntry.IsSelectable, "HP 高于 50% 时逃窜入口应存在但不可选。");
        _test.Eq(
            fullHpEntry.DisabledReason,
            new StringName("equipment_skill_availability_blocked"),
            "逃窜高血量禁用应走通用 availability blocked reason。"
        );

        holder.current_hp = 40;
        BattleAvailableSkillEntry readyEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            ScurrySkillId,
            state
        );
        _test.True(readyEntry.IsSelectable, "HP 等于 50% 时逃窜应可用。");
        _test.Eq(readyEntry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "逃窜来源应是 equipment_skill。");
        _test.Eq(readyEntry.EquipmentBindingId, FleeingInstinctBindingId, "逃窜入口应携带逃跑本能 binding id。");
        _test.Eq(readyEntry.EquipmentGrantedActionId, ScurryGrantId, "逃窜 grant id 应稳定。");
        AssertScurrySkillDefinition(fixture);

        BattleCommand command = BuildSelfSkillCommand(holder, readyEntry);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"逃窜低血量时应允许使用。logs={JoinLogs(preview)}");
        int apBefore = holder.current_ap;
        fixture.Runtime.IssueCommand(command);
        _test.Eq(holder.current_ap, apBefore, "逃窜为 0AP，不应消耗 AP。");
        _test.Eq(holder.GetCooldownTyped(ScurrySkillId), 60, "逃窜使用后应设置 60TU 冷却。");

        BattleStatusEffectState counter = holder.GetStatusEffect(CounterStatusId);
        _test.True(counter != null, "逃窜应给自己施加怯懦反击预备。");
        if (counter != null)
        {
            _test.Eq(counter.duration, 60, "怯懦反击预备应持续 60TU。");
            _test.True(counter.attack_roll_advantage, "怯懦反击预备应通过 typed 字段提供 attack advantage。");
            _test.True(counter.consume_on_next_attack_check, "怯懦反击预备应在下一次真实攻击检定后消耗。");
        }

        SkillDefinition basicAttack = fixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId];
        AttackCheckInput attackCheck = BuildAttackCheck(fixture, state, holder, enemy, basicAttack);
        AttackCheckInput resolved =
            fixture.Runtime.GetHitResolver().BuildFateAwareAttackCheckPreview(
                state,
                holder,
                enemy,
                attackCheck
            );
        _test.True(resolved.IsAdvantage, "逃窜后的下一次攻击检定应获得真正 advantage。");

        MoveUnitAdjacentTo(state, enemy, holder);
        BattleCommand attackCommand = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(holder, enemy);
        BattlePreview attackPreview = fixture.Runtime.PreviewCommand(attackCommand);
        _test.True(attackPreview?.allowed == true, $"反击预备后的普通攻击预览应允许。logs={JoinLogs(attackPreview)}");
        _test.True(holder.HasStatusEffect(CounterStatusId), "预览攻击不应消耗怯懦反击预备。");
        fixture.Runtime.IssueCommand(attackCommand);
        _test.False(holder.HasStatusEffect(CounterStatusId), "真实攻击检定提交后应消耗怯懦反击预备。");
    }

    private void AssertScurrySkillDefinition(CowardiceFixture fixture)
    {
        _test.True(fixture.SkillDefs.TryGetValue(ScurrySkillId, out SkillDefinition skill), "逃窜应是 SkillDef。");
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "逃窜应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("unit"), "逃窜应选择自身单位。");
        _test.Eq(combat.TargetTeamFilter, new StringName("self"), "逃窜目标过滤应为 self。");
        _test.Eq(combat.TargetSelectionMode, new StringName("self"), "逃窜应固定选择自己。");
        _test.Eq(combat.RangeValue, 0, "逃窜 self target 射程应为 0。");
        _test.Eq(combat.ApCost, 0, "逃窜应为 0AP。");
        _test.Eq(combat.CooldownTu, 60, "逃窜冷却必须是 60TU。");
        _test.True(ContainsStringName(combat.RequiredWeaponFamilies, "sword"), "逃窜应要求 sword family。");
        _test.True(HasForcedMoveEvasive(combat, 2), "逃窜应通过 forced_move evasive 位移 2 格。");
        _test.True(HasCounterStatus(combat), "逃窜应施加 60TU 怯懦反击预备状态。");

        _test.True(
            fixture.Bindings.TryGetValue(
                FleeingInstinctBindingId,
                out EquipmentAbilityBindingDefinition binding
            ),
            "逃跑本能 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "逃跑本能 binding 应授予一个装备技能入口。");
        EquipmentGrantedActionDefinition grant =
            binding.GrantedActions.Count > 0 ? binding.GrantedActions[0] : null;
        _test.Eq(grant?.SkillId ?? new StringName(""), ScurrySkillId, "逃跑本能 grant 应指向逃窜 SkillDef。");
        _test.Eq(grant?.GrantedActionId ?? new StringName(""), ScurryGrantId, "逃跑本能 grant id 应稳定。");
        _test.True(grant?.AvailabilityConditions != null, "逃窜 50% HP 门槛必须由 grant availability 配置。");
    }

    private static AttackCheckInput BuildAttackCheck(
        CowardiceFixture fixture,
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
                "cowardice_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
    }

    private static BattleAttackRollModifierBundle BuildModifierBundle(
        CowardiceFixture fixture,
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
                "cowardice_test",
                force_hit_no_crit: false
            )
        );
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        CowardiceFixture fixture,
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

    private static BattleCommand BuildSelfSkillCommand(
        BattleUnitState user,
        BattleAvailableSkillEntry entry
    )
    {
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = user?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = entry?.EntryRef.SkillId ?? new StringName(""),
            target_unit_id = user?.unit_id ?? new StringName(""),
            target_coord = user?.coord ?? new Vector2I(-1, -1),
        };
        if (user != null)
            command.AddTargetUnitId(user.unit_id);
        return command;
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
        AddUnitToState(state, holder);
        AddUnitToState(state, target);
        foreach (BattleUnitState unit in extraUnits ?? Array.Empty<BattleUnitState>())
            AddUnitToState(state, unit);
        state.active_unit_id = holder?.unit_id ?? new StringName("");
        return state;
    }

    private static void AddUnitToState(BattleState state, BattleUnitState unit)
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

    private static void MoveUnitAdjacentTo(
        BattleState state,
        BattleUnitState unit,
        BattleUnitState anchor
    )
    {
        if (state == null || unit == null || anchor == null)
            return;
        foreach (Vector2I coord in unit.occupied_coords)
            state.GetCell(coord)?.ClearOccupant();

        Vector2I destination = anchor.coord.X < state.map_size.X - 1
            ? anchor.coord + Vector2I.Right
            : anchor.coord + Vector2I.Left;
        unit.coord = destination;
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
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

    private static bool HasForcedMoveEvasive(CombatSkillDefinition combat, int distance)
    {
        foreach (CombatEffectDefinition effect in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectType == "forced_move"
                && effect.ForcedMoveMode == "evasive"
                && effect.ForcedMoveDistance == distance)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasCounterStatus(CombatSkillDefinition combat)
    {
        foreach (CombatEffectDefinition effect in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect?.EffectType == "status"
                && effect.StatusId == CounterStatusId
                && effect.DurationTu == 60
                && effect.AttackRollAdvantage
                && effect.ConsumeOnNextAttackCheck
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        string equipmentInstanceId
    )
    {
        _test.True(unit.effective_trait_ids.Contains(traitId), $"unit 应投影 trait {traitId}。");
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

    private sealed class CowardiceFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private CowardiceFixture(
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
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static CowardiceFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new(new TestContentResourceLoader());
            ProgressionContentRegistry progressionRegistry = new(new TestContentResourceLoader());
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
            BattleTestFixture.ConfigureDamageResolverForTests(
                runtime,
                new FixedRollDamageResolver(damageRolls)
            );
            BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
            return new CowardiceFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildCowardiceUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new StringName[] { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_cowardice_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, null);
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
                Runtime._unit_factory.BuildAllyUnits(_partyState, null);
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} baseline should build exactly one ally unit.");
            BattleUnitState unit = units[0];
            unit.faction_id = "ally";
            return unit;
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
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

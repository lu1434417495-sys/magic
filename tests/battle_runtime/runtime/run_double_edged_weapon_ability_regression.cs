using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_double_edged_weapon_ability_regression : SceneTree
{
    private static readonly StringName DoubleEdgedItemId =
        "weapon_unique_sword_double_edged_263";
    private static readonly StringName TwinAttackTraitId =
        "weapon.sword.double_edged.twin_attack";
    private static readonly StringName NoGuardTraitId =
        "weapon.sword.double_edged.no_guard";
    private static readonly StringName ParadoxDanceTraitId =
        "weapon.sword.double_edged.paradox_dance";
    private static readonly StringName SingleEdgeTraitId =
        "weapon.sword.double_edged.single_edge";
    private static readonly StringName TwinAttackBindingId =
        "binding.weapon.sword.double_edged.twin_attack";
    private static readonly StringName NoGuardBindingId =
        "binding.weapon.sword.double_edged.no_guard";
    private static readonly StringName ParadoxDanceBindingId =
        "binding.weapon.sword.double_edged.paradox_dance";
    private static readonly StringName SingleEdgeBindingId =
        "binding.weapon.sword.double_edged.single_edge";
    private static readonly StringName TwinAttackSkillId =
        "weapon_sword_double_edged_twin_attack";
    private static readonly StringName SingleEdgeSkillId =
        "weapon_sword_double_edged_single_edge";
    private static readonly StringName TwinAttackGrantId =
        "grant.double_edged.twin_attack.skill";
    private static readonly StringName SingleEdgeGrantId =
        "grant.double_edged.single_edge.skill";
    private static readonly StringName TurnUseExhaustedReason =
        "equipment_skill_turn_use_exhausted";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestDoubleEdgedProjectsRealContentAndSkillShapes();
            TestTwinAttackHitsTwoTargetsPaysFixedRecoilAndHealsOnDoubleKill();
            TestSingleEdgeAttackUsesWeaponDamagePlusTwoD6WithoutRecoil();
            TestEquipmentGrantedWeaponSkillsAreOnceEachPerActionTurn();
            Quit(_test.Finish("Double-Edged weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(_test.Finish("Double-Edged weapon ability regression"));
        }
    }

    private void TestDoubleEdgedProjectsRealContentAndSkillShapes()
    {
        using DoubleEdgedFixture fixture = DoubleEdgedFixture.Build(new FixedRollDamageResolver());

        _test.True(fixture.ItemDefs.ContainsKey(DoubleEdgedItemId), "双面刃 item 应加载。");
        _test.True(fixture.TraitDefs.ContainsKey(TwinAttackTraitId), "双刃攻击 trait 应加载。");
        _test.True(fixture.TraitDefs.ContainsKey(NoGuardTraitId), "无防御 trait 应加载。");
        _test.True(fixture.TraitDefs.ContainsKey(ParadoxDanceTraitId), "悖论之舞 trait 应加载。");
        _test.True(fixture.TraitDefs.ContainsKey(SingleEdgeTraitId), "单刃姿态 trait 应加载。");
        _test.True(fixture.Bindings.ContainsKey(TwinAttackBindingId), "双刃攻击 binding 应加载。");
        _test.True(fixture.Bindings.ContainsKey(NoGuardBindingId), "无防御 binding 应加载。");
        _test.True(fixture.Bindings.ContainsKey(ParadoxDanceBindingId), "悖论之舞 binding 应加载。");
        _test.True(fixture.Bindings.ContainsKey(SingleEdgeBindingId), "单刃姿态 binding 应加载。");
        _test.True(fixture.SkillDefs.ContainsKey(TwinAttackSkillId), "双刃攻击技能应加载。");
        _test.True(fixture.SkillDefs.ContainsKey(SingleEdgeSkillId), "单刃斩技能应加载。");
        if (!fixture.ItemDefs.ContainsKey(DoubleEdgedItemId))
            return;

        ItemDef raw = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_sword_double_edged_263.tres"
        );
        _test.True(raw != null, "双面刃原始资源应能加载。");
        if (raw != null)
        {
            _test.Eq(raw.base_item_id, new StringName("weapon_type_longsword_base"), "双面刃应继承 longsword 模板。");
            _test.Eq(raw.base_price, 65000, "双面刃价格应落成 65000。");
            _test.True(ContainsStringName(raw.tags, "double_edged"), "双面刃物品 tag 应包含 double_edged。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildDoubleEdgedUnit("projection");
        int baselineAc = baseline.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS);
        int equippedAc = equipped.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS);

        _test.Eq(equipped.weapon_item_id, DoubleEdgedItemId, "装备后 unit 应保留双面刃 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "双面刃应投影为 longsword。");
        _test.Eq(equipped.weapon_attack_range, 1, "双面刃攻击范围应为 1。");
        _test.True(equipped.weapon_is_versatile, "双面刃应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "双面刃单手骰应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "双面刃单手骰应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "双面刃单手骰固定加值应为 +3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "双面刃双手骰应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "双面刃双手骰应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "双面刃双手骰固定加值应为 +3。");
        _test.Eq(equippedAc, baselineAc - 2, "无防御应在装备时让持有者 AC -2。");
        AssertUnitHasTraitAndAbilitySource(equipped, TwinAttackTraitId, TwinAttackBindingId, "eq_double_edged_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, NoGuardTraitId, NoGuardBindingId, "eq_double_edged_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, ParadoxDanceTraitId, ParadoxDanceBindingId, "eq_double_edged_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, SingleEdgeTraitId, SingleEdgeBindingId, "eq_double_edged_projection");
        AssertTwinAttackSkillShape(fixture);
        AssertSingleEdgeSkillShape(fixture);

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除双面刃后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS),
            baselineAc,
            "移除双面刃后 AC -2 应消失。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除双面刃后装备能力源应清空。");
    }

    private void TestTwinAttackHitsTwoTargetsPaysFixedRecoilAndHealsOnDoubleKill()
    {
        using DoubleEdgedFixture fixture = DoubleEdgedFixture.Build(
            new FixedRollDamageResolver(new GArray { 4, 3, 3, 3, 4, 3, 3, 3, 4, 5 })
        );
        BattleUnitState holder = fixture.BuildDoubleEdgedUnit("twin");
        holder.current_hp = 50;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 60);
        BattleUnitState first = BuildEnemy("twin_first", new Vector2I(1, 0), hp: 10);
        BattleUnitState second = BuildEnemy("twin_second", new Vector2I(0, 1), hp: 10);
        BattleState state = BuildState("double_edged_twin", holder, first, second);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(fixture, holder, TwinAttackSkillId, state);
        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            entry,
            TwinAttackSkillId,
            first,
            second
        );

        _test.True(batch != null, "双刃攻击命令应返回 batch。");
        _test.False(first.is_alive, "双刃攻击应命中并击杀第一个低 HP 目标。");
        _test.False(second.is_alive, "双刃攻击应命中并击杀第二个低 HP 目标。");
        _test.Eq(holder.current_hp, 51, "双杀时应先承受固定 8HP 反伤，再由悖论之舞治疗 2D8。");
        _test.Eq(holder.current_stamina, 36, "双刃攻击应消耗 24 体力。");
        _test.Eq(holder.current_ap, 1, "双刃攻击应消耗 1AP。");
    }

    private void TestSingleEdgeAttackUsesWeaponDamagePlusTwoD6WithoutRecoil()
    {
        using DoubleEdgedFixture fixture = DoubleEdgedFixture.Build(
            new FixedRollDamageResolver(new GArray { 4, 3, 3 })
        );
        BattleUnitState holder = fixture.BuildDoubleEdgedUnit("single");
        holder.current_hp = 40;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 60);
        BattleUnitState target = BuildEnemy("single_target", new Vector2I(1, 0), hp: 100);
        BattleState state = BuildState("double_edged_single", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(fixture, holder, SingleEdgeSkillId, state);
        int targetHpBefore = target.current_hp;
        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            entry,
            SingleEdgeSkillId,
            target
        );

        _test.True(batch != null, "单刃斩命令应返回 batch。");
        _test.Eq(targetHpBefore - target.current_hp, 13, "单刃斩应造成 1D8+3 加 2D6 physical_slash。");
        _test.Eq(holder.current_hp, 40, "单刃斩不应造成反伤。");
        _test.Eq(holder.current_stamina, 48, "单刃斩应消耗 12 体力。");
        _test.Eq(holder.current_ap, 1, "单刃斩应消耗 1AP。");
    }

    private void TestEquipmentGrantedWeaponSkillsAreOnceEachPerActionTurn()
    {
        using DoubleEdgedFixture fixture = DoubleEdgedFixture.Build(
            new FixedRollDamageResolver(new GArray { 4, 3, 3 })
        );
        BattleUnitState holder = fixture.BuildDoubleEdgedUnit("turn_once");
        BattleUnitState target = BuildEnemy("turn_once_target", new Vector2I(1, 0), hp: 100);
        BattleState state = BuildState("double_edged_turn_once", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry singleEntry =
            FindRequiredEquipmentSkill(fixture, holder, SingleEdgeSkillId, state);
        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            singleEntry,
            SingleEdgeSkillId,
            target
        );
        _test.True(batch != null, "第一次单刃斩应成功。");

        holder.current_ap = 2;
        holder.current_stamina = 60;
        BattleSkillAvailabilityView sameTurnView = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(sameTurnView, SingleEdgeSkillId, out BattleAvailableSkillEntry sameTurnSingle),
            "同回合使用后，单刃斩入口仍应可见。"
        );
        _test.False(sameTurnSingle.IsSelectable, "同一行动回合内，同一个装备技能不能第二次使用。");
        _test.Eq(sameTurnSingle.DisabledReason, TurnUseExhaustedReason, "同回合禁用原因应标识为装备技能行动回合已用。");
        _test.True(
            TryFindSkillEntry(sameTurnView, TwinAttackSkillId, out BattleAvailableSkillEntry sameTurnTwin),
            "双刃攻击入口仍应可见。"
        );
        _test.True(sameTurnTwin.IsSelectable, "使用单刃斩不应消耗双刃攻击自己的行动回合使用次数。");

        BattleCommand blockedCommand = BuildUnitSkillCommand(
            holder,
            sameTurnSingle,
            SingleEdgeSkillId,
            target
        );
        BattlePreview blockedPreview = fixture.Runtime.PreviewCommand(blockedCommand);
        _test.False(blockedPreview?.allowed == true, "同一行动回合第二次预览同装备技能应被阻止。");

        holder.ResetPerTurnCharges();
        holder.current_ap = 2;
        holder.current_stamina = 60;
        BattleSkillAvailabilityView nextTurnView = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(nextTurnView, SingleEdgeSkillId, out BattleAvailableSkillEntry nextTurnSingle),
            "下一行动回合单刃斩入口应仍存在。"
        );
        _test.True(nextTurnSingle.IsSelectable, "下一行动回合应恢复单刃斩可用性。");
    }

    private void AssertTwinAttackSkillShape(DoubleEdgedFixture fixture)
    {
        _test.True(fixture.SkillDefs.TryGetValue(TwinAttackSkillId, out SkillDefinition twinSkill), "双刃攻击技能应投影。");
        CombatSkillDefinition combat = twinSkill?.CombatProfile;
        _test.Eq(combat?.TargetTeamFilter ?? new StringName(""), new StringName("enemy"), "双刃攻击应只选敌方。");
        _test.Eq(combat?.TargetSelectionMode ?? new StringName(""), new StringName("multi_unit"), "双刃攻击应选择两个单位目标。");
        _test.Eq(combat?.RangeValue ?? 0, 1, "双刃攻击应使用武器范围 1。");
        _test.Eq(combat?.MinTargetCount ?? 0, 2, "双刃攻击至少两个目标。");
        _test.Eq(combat?.MaxTargetCount ?? 0, 2, "双刃攻击最多两个目标。");
        _test.False(combat?.AllowRepeatTarget == true, "双刃攻击两个目标不能重复。");
        _test.Eq(combat?.ApCost ?? 0, 1, "双刃攻击消耗 1AP。");
        _test.Eq(combat?.StaminaCost ?? 0, 24, "双刃攻击消耗 24 体力。");
        CombatEffectDefinition effect = FirstEffect(twinSkill);
        _test.Eq(effect?.EffectKind ?? BattleEffectKind.Unknown, BattleEffectKind.Damage, "双刃攻击应造成伤害。");
        _test.True(effect?.AddWeaponDice == true, "双刃攻击应加入武器骰。");
        _test.True(effect?.RequiresWeapon == true, "双刃攻击应要求武器。");
        _test.True(effect?.UseWeaponPhysicalDamageTag == true, "双刃攻击应使用武器物理伤害标签。");
        _test.True(effect?.ResolveAsWeaponAttack == true, "双刃攻击应走武器攻击命中检定。");
        _test.Eq(effect?.DiceCount ?? 0, 3, "双刃攻击应追加 3D6。");
        _test.Eq(effect?.DiceSides ?? 0, 6, "双刃攻击应追加 3D6。");

        _test.True(fixture.Bindings.TryGetValue(TwinAttackBindingId, out EquipmentAbilityBindingDefinition binding), "双刃攻击 binding 应投影。");
        _test.True(HasGrantedSkill(binding, TwinAttackGrantId, TwinAttackSkillId), "双刃攻击 binding 应授予对应主动技。");
    }

    private void AssertSingleEdgeSkillShape(DoubleEdgedFixture fixture)
    {
        _test.True(fixture.SkillDefs.TryGetValue(SingleEdgeSkillId, out SkillDefinition singleSkill), "单刃斩技能应投影。");
        CombatSkillDefinition combat = singleSkill?.CombatProfile;
        _test.Eq(combat?.TargetTeamFilter ?? new StringName(""), new StringName("enemy"), "单刃斩应只选敌方。");
        _test.Eq(combat?.TargetSelectionMode ?? new StringName(""), new StringName("single_unit"), "单刃斩应选择一个单位目标。");
        _test.Eq(combat?.RangeValue ?? 0, 1, "单刃斩应使用武器范围 1。");
        _test.Eq(combat?.ApCost ?? 0, 1, "单刃斩消耗 1AP。");
        _test.Eq(combat?.StaminaCost ?? 0, 12, "单刃斩消耗 12 体力。");
        CombatEffectDefinition effect = FirstEffect(singleSkill);
        _test.Eq(effect?.EffectKind ?? BattleEffectKind.Unknown, BattleEffectKind.Damage, "单刃斩应造成伤害。");
        _test.True(effect?.AddWeaponDice == true, "单刃斩应加入武器骰。");
        _test.True(effect?.RequiresWeapon == true, "单刃斩应要求武器。");
        _test.True(effect?.UseWeaponPhysicalDamageTag == true, "单刃斩应使用武器物理伤害标签。");
        _test.True(effect?.ResolveAsWeaponAttack == true, "单刃斩应走武器攻击命中检定。");
        _test.Eq(effect?.DiceCount ?? 0, 2, "单刃斩应追加 2D6。");
        _test.Eq(effect?.DiceSides ?? 0, 6, "单刃斩应追加 2D6。");

        _test.True(fixture.Bindings.TryGetValue(SingleEdgeBindingId, out EquipmentAbilityBindingDefinition binding), "单刃姿态 binding 应投影。");
        _test.True(HasGrantedSkill(binding, SingleEdgeGrantId, SingleEdgeSkillId), "单刃姿态 binding 应授予对应主动技。");
    }

    private static CombatEffectDefinition FirstEffect(SkillDefinition skill)
    {
        IReadOnlyList<CombatEffectDefinition> effects =
            skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        return effects.Count > 0 ? effects[0] : null;
    }

    private static bool HasGrantedSkill(
        EquipmentAbilityBindingDefinition binding,
        StringName grantId,
        StringName skillId
    )
    {
        foreach (EquipmentGrantedActionDefinition grant in binding?.GrantedActions ?? Array.Empty<EquipmentGrantedActionDefinition>())
        {
            if (grant?.GrantedActionId == grantId && grant.SkillId == skillId)
                return true;
        }
        return false;
    }

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        params BattleUnitState[] targets
    )
    {
        int currentHp = Math.Max(user?.current_hp ?? 0, 0);
        WeaponAbilityCommandTestSupport.PrimeActionResources(user, ap: 2);
        if (user != null)
            user.SetCurrentHp(currentHp);
        user.current_stamina = 60;
        user.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 60);
        BattleCommand command = BuildUnitSkillCommand(user, entry, skillId, targets);
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"skill preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleCommand BuildUnitSkillCommand(
        BattleUnitState user,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        params BattleUnitState[] targets
    )
    {
        BattleUnitState primary = targets != null && targets.Length > 0 ? targets[0] : null;
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = user?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = skillId,
            target_unit_id = primary?.unit_id ?? new StringName(""),
            target_coord = primary?.coord ?? new Vector2I(-1, -1),
        };
        foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
        {
            if (target != null)
                command.AddTargetUnitId(target.unit_id);
        }
        return command;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        DoubleEdgedFixture fixture,
        BattleUnitState holder,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityService availabilityService =
            new(fixture.SkillDefs, fixture.Bindings);
        return availabilityService.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeEquipmentSkills = true,
                IncludeKnownSkills = false,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 0,
                BattleState = state,
            }
        );
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        DoubleEdgedFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityView availability =
            BuildEquipmentSkillAvailability(fixture, holder, state);
        if (!TryFindSkillEntry(availability, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        return entry;
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

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        params BattleUnitState[] units
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(6, 6),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(new GDictionary { ["world_step"] = 0 })
        );
        AddPlainCells(state);
        AddUnitToState(state, holder, holder.faction_id);
        state.ally_unit_ids.Add(holder.unit_id);
        foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
        {
            AddUnitToState(state, unit, holder.faction_id);
            if (unit.faction_id == holder.faction_id)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
        }
        return state;
    }

    private static void AddUnitToState(BattleState state, BattleUnitState unit, StringName allyFactionId)
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        SetUnitOccupants(state, unit);
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

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp = 30) =>
        BuildUnit(unitId, "enemy", coord, hp, hp);

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp,
        int hpMax
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = hp > 0,
            current_hp = Math.Max(hp, 0),
            current_ap = 2,
            current_stamina = 60,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hpMax, 1));
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 60);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private sealed class DoubleEdgedFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private DoubleEdgedFixture(
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
        internal IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static DoubleEdgedFixture Build(BattleDamageResolver damageResolver)
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
            if (damageResolver != null)
                runtime.ConfigureDamageResolverForTests(damageResolver);
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new DoubleEdgedFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildDoubleEdgedUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                DoubleEdgedItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(DoubleEdgedItemId, $"eq_double_edged_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 60);
            unit.current_stamina = 60;
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

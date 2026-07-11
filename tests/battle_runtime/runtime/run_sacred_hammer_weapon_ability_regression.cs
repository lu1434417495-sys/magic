using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_sacred_hammer_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName SacredItemId =
        "weapon_unique_warhammer_sacred_408";
    private static readonly StringName RadiantStrikeTraitId =
        "weapon.warhammer.sacred.radiant_strike";
    private static readonly StringName UndeadJudgmentTraitId =
        "weapon.warhammer.sacred.undead_judgment";
    private static readonly StringName SacredHealTraitId =
        "weapon.warhammer.sacred.heal";
    private static readonly StringName FaithCostTraitId =
        "weapon.warhammer.sacred.faith_cost";
    private static readonly StringName RadiantStrikeBindingId =
        "binding.weapon.warhammer.sacred.radiant_strike";
    private static readonly StringName UndeadJudgmentBindingId =
        "binding.weapon.warhammer.sacred.undead_judgment";
    private static readonly StringName SacredHealBindingId =
        "binding.weapon.warhammer.sacred.heal";
    private static readonly StringName FaithCostBindingId =
        "binding.weapon.warhammer.sacred.faith_cost";
    private static readonly StringName SacredHealSkillId =
        "weapon_warhammer_sacred_heal";
    private static readonly StringName SacredHealGrantId =
        "grant.sacred_hammer.heal.skill";
    private static readonly StringName TurnUseExhaustedReason =
        "equipment_skill_turn_use_exhausted";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSacredHammerProjectsRealContentAndClearsOnUnequip();
            TestSacredHammerAddsRadiantDamageOnWeaponHit();
            TestSacredHammerAddsExtraRadiantDamageAgainstUndead();
            TestSacredHealSkillShapeUsageAndHealing();
            RequestTestExit(_test.Finish("Sacred Hammer weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Sacred Hammer weapon ability regression"));
        }
    }

    private void TestSacredHammerProjectsRealContentAndClearsOnUnequip()
    {
        using SacredFixture fixture = SacredFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(SacredItemId), "真实物品内容应包含神圣之锤。");
        _test.True(fixture.TraitDefs.ContainsKey(RadiantStrikeTraitId), "真实 trait 内容应包含神圣辉击。");
        _test.True(fixture.TraitDefs.ContainsKey(UndeadJudgmentTraitId), "真实 trait 内容应包含不死审判。");
        _test.True(fixture.TraitDefs.ContainsKey(SacredHealTraitId), "真实 trait 内容应包含神圣治疗。");
        _test.False(
            fixture.TraitDefs.ContainsKey(FaithCostTraitId),
            "信仰的代价应只保留 source trace，不再注册占位 trait。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(RadiantStrikeBindingId),
            "真实装备能力内容应包含神圣辉击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(UndeadJudgmentBindingId),
            "真实装备能力内容应包含不死审判 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(SacredHealBindingId),
            "真实装备能力内容应包含神圣治疗 binding。"
        );
        _test.False(
            fixture.Bindings.ContainsKey(FaithCostBindingId),
            "信仰的代价不应再注册占位 binding，deferred 条目统一走 trace-only。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(SacredHealSkillId),
            "真实技能内容应包含神圣之锤治疗装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(SacredItemId))
            return;

        ItemDef rawSacred = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_warhammer_sacred.tres"
        );
        _test.True(rawSacred != null, "神圣之锤原始资源应能加载。");
        if (rawSacred != null)
        {
            _test.Eq(
                rawSacred.base_item_id,
                new StringName("weapon_type_warhammer_base"),
                "神圣之锤应继承 warhammer 模板。"
            );
            _test.Eq(rawSacred.display_name, "神圣之锤", "神圣之锤显示名应匹配设计。");
            _test.Eq(rawSacred.base_price, 65000, "神圣之锤价格应为 65000。");
            _test.True(rawSacred.trait_ids.Contains(RadiantStrikeTraitId), "物品应声明神圣辉击 trait。");
            _test.True(rawSacred.trait_ids.Contains(UndeadJudgmentTraitId), "物品应声明不死审判 trait。");
            _test.True(rawSacred.trait_ids.Contains(SacredHealTraitId), "物品应声明神圣治疗 trait。");
            _test.False(rawSacred.trait_ids.Contains(FaithCostTraitId), "物品不应再声明信仰代价占位 trait。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildSacredUnit("projection");
        _test.Eq(equipped.weapon_item_id, SacredItemId, "神圣之锤装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("warhammer"), "神圣之锤应投影为 warhammer。");
        _test.Eq(equipped.weapon_family, new StringName("hammer"), "神圣之锤应保留 hammer 家族。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_blunt"), "神圣之锤应是 blunt 物理伤害。");
        _test.Eq(equipped.weapon_attack_range, 1, "神圣之锤攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "神圣之锤应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "神圣之锤单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "神圣之锤单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "神圣之锤单手应为 1D8+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            RadiantStrikeTraitId,
            RadiantStrikeBindingId,
            "eq_sacred_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            UndeadJudgmentTraitId,
            UndeadJudgmentBindingId,
            "eq_sacred_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            SacredHealTraitId,
            SacredHealBindingId,
            "eq_sacred_projection"
        );
        _test.False(
            equipped.effective_trait_ids.Contains(FaithCostTraitId),
            "装备神圣之锤不应投影信仰的代价占位 trait。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除神圣之锤后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除神圣之锤后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除神圣之锤后装备能力源应清空。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除神圣之锤后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestSacredHammerAddsRadiantDamageOnWeaponHit()
    {
        using SacredFixture guard = SacredFixture.Build(new GArray());
        if (!guard.ItemDefs.ContainsKey(SacredItemId))
            return;

        int plainDamage = MeasureBasicAttackDamage(
            new[] { new StringName("humanoid") },
            stripAbilitySources: true,
            new GArray { 4, 3 }
        );
        int sacredDamage = MeasureBasicAttackDamage(
            new[] { new StringName("humanoid") },
            stripAbilitySources: false,
            new GArray { 4, 3 }
        );

        _test.Eq(plainDamage, 6, "固定骰 4 时，神圣之锤基础武器伤害应为 1D8+2。");
        _test.Eq(
            sacredDamage,
            9,
            "普通目标命中后应追加 1D8 radiant，且不吞掉基础武器伤害。"
        );
    }

    private void TestSacredHammerAddsExtraRadiantDamageAgainstUndead()
    {
        using SacredFixture guard = SacredFixture.Build(new GArray());
        if (!guard.ItemDefs.ContainsKey(SacredItemId))
            return;

        AssertUndeadBonusDiceCanBeCollected();

        using SacredFixture plainFixture = SacredFixture.Build(new GArray { 4, 3, 1, 2, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildSacredUnit("undead_plain");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildEnemy(
            "sacred_undead_plain_target",
            new Vector2I(1, 0),
            100,
            new[] { new StringName("undead") }
        );
        BattleState plainState = BuildState("sacred_undead_plain_attack", plainAttacker, plainTarget, worldStep: 0);
        plainFixture.Runtime.SetupStateForTests(plainState);
        IssueBasicAttackInCurrentState(plainFixture.Runtime, plainAttacker, plainTarget, "sacred_undead_plain_attack");
        int plainDamage = 100 - plainTarget.current_hp;

        using SacredFixture fixture = SacredFixture.Build(new GArray { 4, 3, 1, 2, 3 });
        BattleUnitState attacker = fixture.BuildSacredUnit("undead_damage");
        BattleUnitState target = BuildEnemy(
            "sacred_undead_target",
            new Vector2I(1, 0),
            100,
            new[] { new StringName("undead") }
        );
        BattleState state = BuildState("sacred_undead_attack", attacker, target, worldStep: 0);
        fixture.Runtime.SetupStateForTests(state);
        BattleEventBatch undeadBatch =
            IssueBasicAttackInCurrentState(fixture.Runtime, attacker, target, "sacred_undead_attack");
        int undeadDamage = 100 - target.current_hp;

        _test.Eq(plainDamage, 6, "undead 基准伤害仍应只有 1D8+2 武器伤害。");
        _test.Eq(
            undeadDamage,
            15,
            $"命中 undead 时应造成武器 1D8+2、通用 1D8 radiant 和额外 3D6 radiant。{DescribeBatch(undeadBatch)}"
        );
    }

    private void AssertUndeadBonusDiceCanBeCollected()
    {
        using SacredFixture fixture = SacredFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildSacredUnit("undead_bonus_probe");
        BattleUnitState target = BuildEnemy(
            "undead_bonus_probe_target",
            new Vector2I(1, 0),
            100,
            new[] { new StringName("undead") }
        );
        BattleState state = BuildState("sacred_undead_bonus_probe", attacker, target, worldStep: 0);
        fixture.Runtime.SetupStateForTests(state);
        BattleUnitState stateAttacker = state.GetUnit(attacker.unit_id);
        BattleUnitState stateTarget = state.GetUnit(target.unit_id);

        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> bonusDice =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = stateAttacker,
                    TargetUnit = stateTarget,
                    BattleState = state,
                    AttackSucceeded = true,
                    CriticalHit = false,
                }
            );

        _test.Eq(bonusDice.Count, 2, "undead 命中上下文应收集通用 1D8 与 undead 3D6 两组 radiant bonus dice。");
        _test.True(
            ContainsBonusDice(bonusDice, RadiantStrikeBindingId, 1, 8, "radiant"),
            "通用 radiant strike 应贡献 1D8 radiant。"
        );
        _test.True(
            ContainsBonusDice(bonusDice, UndeadJudgmentBindingId, 3, 6, "radiant"),
            "undead judgment 应贡献 3D6 radiant。"
        );
    }

    private void TestSacredHealSkillShapeUsageAndHealing()
    {
        using SacredFixture fixture = SacredFixture.Build(new GArray { 4, 5, 4, 5, 4, 5 });
        if (!fixture.ItemDefs.ContainsKey(SacredItemId))
            return;

        AssertSacredHealSkillConfig(fixture);

        BattleUnitState holder = fixture.BuildSacredUnit("heal");
        BattleUnitState ally = BuildAlly("sacred_heal_ally", new Vector2I(1, 0), hp: 10, hpMax: 50);
        BattleState state = BuildState("sacred_heal", holder, ally, worldStep: 0);
        BattleUnitState sentinel = BuildEnemy(
            "sacred_heal_sentinel",
            new Vector2I(5, 5),
            hp: 10,
            Array.Empty<StringName>()
        );
        state.SetUnit(sentinel);
        SetUnitOccupants(state, sentinel);
        state.enemy_unit_ids.Add(sentinel.unit_id);
        fixture.Runtime.SetupStateForTests(state);
        BattleAvailableSkillEntry entry =
            FindRequiredEquipmentSkill(fixture, holder, SacredHealSkillId, state, 0);

        _test.Eq(
            entry.EntryRef.SourceKind,
            BattleSkillEntrySourceKind.EquipmentSkill,
            "神圣治疗技能入口来源应是 equipment_skill。"
        );
        _test.Eq(entry.EquipmentBindingId, SacredHealBindingId, "神圣治疗入口应携带 binding id。");
        _test.Eq(entry.EquipmentGrantedActionId, SacredHealGrantId, "神圣治疗入口应携带 grant id。");
        _test.True(entry.IsSelectable, "未使用前神圣治疗应可选。");
        _test.Eq(
            entry.EquipmentUsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "神圣治疗应声明 per_world_day 使用周期。"
        );
        _test.Eq(entry.EquipmentMaxUsesPerPeriod, 3, "神圣治疗每日应有 3 次。");

        BattleEventBatch firstBatch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            ally,
            entry,
            SacredHealSkillId
        );
        _test.True(firstBatch != null, "第一次神圣治疗应返回 batch。");
        _test.Eq(ally.current_hp, 19, "固定骰 4、5 时，神圣治疗应恢复 2D8 HP。");

        EquipmentInstanceState instance = FindEquippedInstance(holder, "eq_sacred_heal");
        _test.True(instance != null, "神圣治疗测试应能找到装备实例。");
        if (instance != null)
        {
            _test.Eq(
                EquipmentAbilityUsageRuntime.GetUsedCount(
                    instance,
                    SacredHealGrantId,
                    EquipmentAbilityUsagePeriodKind.PerWorldDay,
                    WorldTimeSystem.StepToDay(0)
                ),
                1,
                "神圣治疗第一次施放后应写入当前世界日使用次数。"
            );
        }

        holder.current_ap = 2;
        BattleSkillAvailabilityView sameTurnView = BuildEquipmentSkillView(
            fixture,
            holder,
            state,
            0
        );
        _test.True(
            TryFindSkillEntry(sameTurnView, SacredHealSkillId, out BattleAvailableSkillEntry sameTurnEntry),
            "同回合使用后，神圣治疗入口仍应可见。"
        );
        _test.False(sameTurnEntry?.IsSelectable ?? true, "同一行动回合内同一个装备技能不能第二次使用。");
        _test.Eq(
            sameTurnEntry?.DisabledReason ?? new StringName(""),
            TurnUseExhaustedReason,
            "同回合禁用原因应来自现有装备技能行动回合一次限制。"
        );

        for (int use = 2; use <= 3; use++)
        {
            holder.ResetPerTurnCharges();
            holder.current_ap = 2;
            ally.current_hp = 10;
            ForceUnitActing(state, holder);
            BattleAvailableSkillEntry nextEntry =
                FindRequiredEquipmentSkill(fixture, holder, SacredHealSkillId, state, 0);
            IssueUnitSkillInCurrentState(
                fixture.Runtime,
                holder,
                ally,
                nextEntry,
                SacredHealSkillId
            );
        }

        holder.ResetPerTurnCharges();
        holder.current_ap = 2;
        ForceUnitActing(state, holder);
        BattleSkillAvailabilityView exhaustedView = BuildEquipmentSkillView(
            fixture,
            holder,
            state,
            0
        );
        _test.True(
            TryFindSkillEntry(exhaustedView, SacredHealSkillId, out BattleAvailableSkillEntry exhaustedEntry),
            "同日用尽后神圣治疗入口仍应存在。"
        );
        _test.False(exhaustedEntry?.IsSelectable ?? true, "第 4 次同日神圣治疗应不可用。");
        _test.Eq(
            exhaustedEntry?.DisabledReason ?? new StringName(""),
            new StringName("equipment_skill_usage_exhausted"),
            "第 4 次同日神圣治疗禁用原因应稳定。"
        );

        BattleSkillAvailabilityView nextDayView = BuildEquipmentSkillView(
            fixture,
            holder,
            state,
            15
        );
        _test.True(
            TryFindSkillEntry(nextDayView, SacredHealSkillId, out BattleAvailableSkillEntry nextDayEntry),
            "次日神圣治疗入口应仍能解析。"
        );
        _test.True(nextDayEntry?.IsSelectable == true, "跨世界日后神圣治疗应恢复可用。");
    }

    private static int MeasureBasicAttackDamage(
        IReadOnlyList<StringName> targetTags,
        bool stripAbilitySources,
        GArray rolls
    )
    {
        using SacredFixture fixture = SacredFixture.Build(rolls);
        StringName tagLabel =
            targetTags != null && targetTags.Count > 0 ? targetTags[0] : new StringName("none");
        BattleUnitState attacker = fixture.BuildSacredUnit(
            stripAbilitySources ? $"plain_attack_{tagLabel}" : $"sacred_attack_{tagLabel}"
        );
        if (stripAbilitySources)
            attacker.equipment_ability_sources.Clear();
        BattleUnitState target = BuildEnemy(
            $"sacred_attack_target_{tagLabel}",
            new Vector2I(1, 0),
            100,
            targetTags
        );
        StringName battleId = stripAbilitySources
            ? $"sacred_plain_attack_{tagLabel}"
            : $"sacred_ability_attack_{tagLabel}";
        BattleState state = BuildState(battleId, attacker, target, worldStep: 0);
        fixture.Runtime.SetupStateForTests(state);
        IssueBasicAttackInCurrentState(
            fixture.Runtime,
            attacker,
            target,
            battleId
        );
        return 100 - target.current_hp;
    }

    private void AssertSacredHealSkillConfig(SacredFixture fixture)
    {
        _test.True(
            fixture.SkillDefs.TryGetValue(SacredHealSkillId, out SkillDefinition healSkill),
            "神圣治疗应是 SkillDef，而不是 trait 自己承担主动动作。"
        );
        CombatSkillDefinition combat = healSkill?.CombatProfile;
        _test.True(combat != null, "神圣治疗技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "神圣治疗应选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("ally"), "神圣治疗应选择盟友。");
        _test.Eq(combat.RangeValue, 1, "神圣治疗应是触碰/近战范围。");
        _test.Eq(combat.ApCost, 1, "神圣治疗应消耗 1 AP。");
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "神圣治疗应走 direct_effect，不做命中检定。"
        );
        _test.Eq(combat.EffectDefinitions.Count, 1, "神圣治疗应只有一个 heal effect。");
        CombatEffectDefinition heal = combat.EffectDefinitions.Count > 0
            ? combat.EffectDefinitions[0]
            : null;
        _test.Eq(heal?.EffectKind ?? BattleEffectKind.Unknown, BattleEffectKind.Heal, "神圣治疗 effect 应是 heal。");
        _test.Eq(heal?.DiceCount ?? 0, 2, "神圣治疗应掷 2 个骰。");
        _test.Eq(heal?.DiceSides ?? 0, 8, "神圣治疗应掷 D8。");
        _test.Eq(heal?.DiceBonus ?? -1, 0, "神圣治疗不应有固定治疗加值。");

        _test.True(
            fixture.Bindings.TryGetValue(SacredHealBindingId, out EquipmentAbilityBindingDefinition binding),
            "神圣治疗 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "神圣治疗 binding 应授予一个装备技能入口。");
        if (binding.GrantedActions.Count == 0)
            return;
        EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
        _test.Eq(grant.SkillId, SacredHealSkillId, "神圣治疗 grant 应指向真实 SkillDef。");
        _test.Eq(grant.SkillLevel, 1, "神圣治疗 grant 等级应为 1。");
        _test.Eq(
            grant.UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "神圣治疗 grant 应声明 per_world_day。"
        );
        _test.Eq(grant.MaxUsesPerPeriod, 3, "神圣治疗 grant 每世界日 3 次。");
    }

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(user);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            BattleState state = runtime?.GetState();
            BattleUnitState runtimeUser = null;
            BattleUnitState runtimeTarget = null;
            state?.TryGetUnitTyped(user?.unit_id ?? new StringName(""), out runtimeUser);
            state?.TryGetUnitTyped(target?.unit_id ?? new StringName(""), out runtimeTarget);
            IEnumerable<StringName> turnKeys = runtimeUser != null
                ? runtimeUser.GetPerTurnChargesTyped().Keys
                : Array.Empty<StringName>();
            throw new InvalidOperationException(
                $"skill preview blocked: allowed={preview?.allowed} "
                    + $"phase={state?.PhaseKind.ToString() ?? "<null>"} active={state?.active_unit_id.ToString() ?? "<null>"} "
                    + $"entry_selectable={entry?.IsSelectable.ToString() ?? "<null>"} entry_disabled={entry?.DisabledReason.ToString() ?? "<null>"} "
                    + $"user_ap={runtimeUser?.current_ap.ToString() ?? "<null>"} user_turn_keys={string.Join(",", turnKeys)} "
                    + $"target_alive={runtimeTarget?.is_alive.ToString() ?? "<null>"} target_hp={runtimeTarget?.current_hp.ToString() ?? "<null>"} "
                    + $"target_coord={runtimeTarget?.coord.ToString() ?? "<null>"} command_target={command.target_unit_id} "
                    + $"target_ids={string.Join(",", preview?.TargetUnitIdsTyped ?? Array.Empty<StringName>())} logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleEventBatch IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        ForceUnitActing(runtime?.GetState(), attacker);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(attacker, target);
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{label} basic_attack preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        BattleEventBatch batch = runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException($"{label} IssueCommand returned null.");
        return batch;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        SacredFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state,
        int worldStep
    )
    {
        BattleSkillAvailabilityView availability =
            BuildEquipmentSkillView(fixture, holder, state, worldStep);
        if (!TryFindSkillEntry(availability, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        SacredFixture fixture,
        BattleUnitState unit,
        BattleState state,
        int worldStep
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = worldStep,
                BattleState = state,
            }
        );
    }

    private static bool ContainsBonusDice(
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> values,
        StringName bindingId,
        int diceCount,
        int diceSides,
        StringName damageType
    )
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult value in values ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>())
        {
            if (
                value != null
                && value.BindingId == bindingId
                && value.DiceCount == diceCount
                && value.DiceSides == diceSides
                && value.DamageType == damageType
            )
            {
                return true;
            }
        }
        return false;
    }

    private static string DescribeBatch(BattleEventBatch batch)
    {
        if (batch == null)
            return " batch=<null>";
        return
            $" logs=[{string.Join(" | ", batch.LogLinesTyped ?? Array.Empty<string>())}] reports=[{string.Join(" | ", DescribeReports(batch.ReportEntriesTyped))}]";
    }

    private static IEnumerable<string> DescribeReports(
        IEnumerable<IReadOnlyDictionary<string, object>> entries
    )
    {
        foreach (
            IReadOnlyDictionary<string, object> entry in
            entries ?? Array.Empty<IReadOnlyDictionary<string, object>>()
        )
            yield return entry?.ToString() ?? "";
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

    private static EquipmentInstanceState FindEquippedInstance(
        BattleUnitState unit,
        StringName instanceId
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(instanceId);
        EquipmentState equipment = unit?.GetEquipmentView();
        if (equipment == null || normalized == "")
            return null;
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry != null && entry.instance_id == normalized)
                return entry.GetEquipmentInstance();
        }
        return null;
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

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        BattleUnitState unit,
        int worldStep
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
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = worldStep }
            )
        );
        AddPlainCells(state);
        state.SetUnit(holder);
        SetUnitOccupants(state, holder);
        state.ally_unit_ids.Add(holder.unit_id);
        if (unit != null)
        {
            state.SetUnit(unit);
            SetUnitOccupants(state, unit);
            if (unit.faction_id == holder.faction_id)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
        }
        return state;
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
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

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        Vector2I coord,
        int hp,
        IReadOnlyList<StringName> tags
    ) => BuildUnit(unitId, "enemy", coord, hp, hp, tags);

    private static BattleUnitState BuildAlly(
        StringName unitId,
        Vector2I coord,
        int hp,
        int hpMax
    ) => BuildUnit(unitId, "player", coord, hp, hpMax, Array.Empty<StringName>());

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp,
        int hpMax,
        IReadOnlyList<StringName> tags
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
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hpMax, 1));
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        foreach (StringName tag in tags ?? Array.Empty<StringName>())
        {
            if (tag != "")
                unit.creature_type_tags.Add(tag);
        }
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class SacredFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private SacredFixture(
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

        internal static SacredFixture Build(GArray damageRolls)
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
            return new SacredFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildSacredUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                SacredItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    SacredItemId,
                    $"eq_sacred_{label}"
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

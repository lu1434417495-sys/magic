using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_viper_morningstar_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ViperItemId = "weapon_unique_morningstar_viper_206";
    private static readonly StringName VenomStrikeTraitId =
        "weapon.morningstar.viper.venom_strike";
    private static readonly StringName VenomInjectionTraitId =
        "weapon.morningstar.viper.venom_injection";
    private static readonly StringName PoisonImmunityTraitId =
        "weapon.morningstar.viper.poison_immunity";
    private static readonly StringName VenomStrikeBindingId =
        "binding.weapon.morningstar.viper.venom_strike";
    private static readonly StringName VenomInjectionBindingId =
        "binding.weapon.morningstar.viper.venom_injection";
    private static readonly StringName VenomInjectionSkillId =
        "weapon_morningstar_viper_venom_injection";
    private static readonly StringName VenomInjectionGrantId =
        "grant.viper_morningstar.venom_injection.skill";
    private static readonly StringName VenomPrimedStateKey = "venom_primed";

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
            TestViperProjectsRealContentAndPassivePoisonImmunity();
            TestVenomStrikeAddsPoisonAndParalyzesOnFailedPoisonSave();
            TestPoisonSaveImmunityBlocksParalysis();
            TestVenomInjectionUsageAndPrimedDamageLifecycle();
            RequestTestExit(_test.Finish("Viper Morningstar weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Viper Morningstar weapon ability regression"));
        }
    }

    private void TestViperProjectsRealContentAndPassivePoisonImmunity()
    {
        using ViperFixture fixture = ViperFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ViperItemId), "真实物品内容应包含毒蛇晨星。");
        _test.True(fixture.TraitDefs.ContainsKey(VenomStrikeTraitId), "真实 trait 内容应包含毒蛇打击。");
        _test.True(fixture.TraitDefs.ContainsKey(VenomInjectionTraitId), "真实 trait 内容应包含毒液注入。");
        _test.True(fixture.TraitDefs.ContainsKey(PoisonImmunityTraitId), "真实 trait 内容应包含毒抗/毒免。");
        _test.True(
            fixture.Bindings.ContainsKey(VenomStrikeBindingId),
            "真实装备能力内容应包含毒蛇打击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(VenomInjectionBindingId),
            "真实装备能力内容应包含毒液注入 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(VenomInjectionSkillId),
            "真实技能内容应包含毒液注入装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(ViperItemId))
            return;

        ItemDef rawViper = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_morningstar_viper.tres"
        );
        _test.True(rawViper != null, "毒蛇晨星原始资源应能加载。");
        if (rawViper != null)
        {
            _test.Eq(
                rawViper.base_item_id,
                new StringName("weapon_type_morningstar_base"),
                "毒蛇晨星应继承 morningstar 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildViperUnit("projection");
        _test.Eq(equipped.weapon_item_id, ViperItemId, "毒蛇晨星装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("morningstar"), "毒蛇晨星应投影为 morningstar。");
        _test.Eq(equipped.weapon_family, new StringName("mace"), "毒蛇晨星应保留 mace 家族。");
        _test.Eq(equipped.weapon_attack_range, 1, "毒蛇晨星攻击距离应为 1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "毒蛇晨星单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "毒蛇晨星单手应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "毒蛇晨星单手应为 1D8+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            VenomStrikeTraitId,
            VenomStrikeBindingId,
            "eq_viper_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            VenomInjectionTraitId,
            VenomInjectionBindingId,
            "eq_viper_projection"
        );
        _test.True(
            equipped.effective_trait_ids.Contains(PoisonImmunityTraitId),
            "毒抗/毒免 trait 应作为固定装备 trait 投影到战斗单位。"
        );
        _test.Eq(
            GetDamageMitigation(equipped, "poison"),
            new StringName("immune"),
            "毒抗/毒免 trait 应投影 poison damage immune。"
        );
        _test.True(
            ContainsStringName(equipped.save_immunity_tags, "poison"),
            "毒抗/毒免 trait 应投影 poison save immunity。"
        );
        _test.True(
            ContainsStringName(equipped.save_immunity_tags, "antidote"),
            "毒抗/毒免 trait 应投影 antidote immunity。"
        );

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, equipped, 0);
        _test.True(
            TryFindSkillEntry(view, VenomInjectionSkillId, out BattleAvailableSkillEntry entry),
            "装备毒蛇晨星后 unit 应有毒液注入技能入口。"
        );
        if (entry != null)
        {
            _test.True(entry.IsSelectable, "未使用前毒液注入应可选。");
            _test.Eq(
                entry.EquipmentUsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                "毒液注入技能入口应携带 per_world_day 使用周期。"
            );
            _test.Eq(entry.EquipmentMaxUsesPerPeriod, 3, "毒液注入每天应有 3 次。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除毒蛇晨星后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除毒蛇晨星后装备能力源应清空。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除毒蛇晨星后装备 trait 实例应回到装备前状态。"
        );
        _test.False(
            HasDamageMitigation(equipped, "poison"),
            "移除毒蛇晨星后 poison damage immune 不应残留。"
        );
        _test.False(
            ContainsStringName(equipped.save_immunity_tags, "poison"),
            "移除毒蛇晨星后 poison save immunity 不应残留。"
        );
        _test.False(
            ContainsStringName(equipped.save_immunity_tags, "antidote"),
            "移除毒蛇晨星后 antidote immunity 不应残留。"
        );
    }

    private void TestVenomStrikeAddsPoisonAndParalyzesOnFailedPoisonSave()
    {
        // --- 毒液加成：真实命中后额外 +1D6 poison，不涉及豁免（确定性）。 ---
        using ViperFixture fixture = ViperFixture.Build(new GArray { 4, 2 });
        BattleUnitState attacker = fixture.BuildViperUnit("venom_strike");
        BattleUnitState damageTarget = BuildTarget("venom_strike_target", new Vector2I(1, 0));
        damageTarget.current_hp = 100;
        damageTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            damageTarget,
            "viper_venom_strike",
            previewCommand: false
        );
        int venomStrikeDamage = 100 - damageTarget.current_hp;

        using ViperFixture plainFixture = ViperFixture.Build(new GArray { 4, 2 });
        BattleUnitState plainAttacker = plainFixture.BuildViperUnit("venom_strike_plain");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildTarget("venom_strike_plain_target", new Vector2I(1, 0));
        plainTarget.current_hp = 100;
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "viper_venom_strike_plain",
            previewCommand: false
        );
        int plainWeaponDamage = 100 - plainTarget.current_hp;

        _test.Eq(plainWeaponDamage, 6, "固定骰 4 时，毒蛇晨星基础武器伤害应为 1D8+2。");
        _test.Eq(
            venomStrikeDamage,
            8,
            "毒蛇打击应在真实命中后额外造成 1D6 poison，且不吞掉武器伤害。"
        );

        // --- 麻痹：注入固定豁免骰（nat 1）强制 DC15 poison 豁免失败，避免依赖 RNG d20
        //     （nat 20 恒成功会让断言约 5% 概率偶发失败）。装备命中后反应经装备能力服务
        //     直接解析，与 giants_heel 注入 SaveContext 的做法一致。 ---
        BattleUnitState paralyzeTarget = BuildTarget("venom_strike_paralyze_target", new Vector2I(1, 0));
        paralyzeTarget.current_hp = 100;
        paralyzeTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        paralyzeTarget.SetPendingCast(
            new BattlePendingCastState
            {
                SkillId = "fixture_pending",
                StartedCoord = paralyzeTarget.coord,
                RemainingCastProgress = 1000,
            }
        );
        BattleState paralyzeState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "viper_venom_strike_paralyze",
            attacker,
            paralyzeTarget
        );
        fixture.Runtime.SetupStateForTests(paralyzeState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = paralyzeTarget,
                BattleState = paralyzeState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );

        BattleStatusEffectState paralyzed = paralyzeTarget.GetStatusEffect("paralyzed");
        _test.True(paralyzed != null, "毒蛇打击应在 DC15 constitution/poison 豁免失败后施加 paralyzed。");
        _test.Eq(paralyzed?.duration ?? -1, 60, "paralyzed 一回合应使用战斗 TU 持续时间，不写世界时间。");
        _test.True(
            BattleStatusSemanticTable.BlocksPendingCast("paralyzed"),
            "paralyzed 应接入通用 pending cast 阻断语义。"
        );
        _test.True(
            fixture.Runtime._skill_turn_resolver.IsCastInterruptedByStatus(paralyzeTarget),
            "命中后施加 paralyzed 应中断目标 pending cast。"
        );
        _test.True(
            fixture.Runtime._skill_turn_resolver.IsMovementBlocked(paralyzeTarget),
            "paralyzed 应阻止移动。"
        );
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(paralyzeTarget);
        _test.True(
            BattleSkillCastBlockReasonKinds.IsBlocked(
                fixture.Runtime.GetSkillCastBlockReason(
                    paralyzeTarget,
                    fixture.SkillDefs[WeaponAbilityCommandTestSupport.BasicAttackSkillId]
                )
            ),
            "paralyzed 应阻止主动技能/行动。"
        );
    }

    private void TestPoisonSaveImmunityBlocksParalysis()
    {
        using ViperFixture fixture = ViperFixture.Build(new GArray { 4, 2 });
        BattleUnitState attacker = fixture.BuildViperUnit("poison_immunity_target");
        BattleUnitState target = BuildTarget("poison_immune_target", new Vector2I(1, 0));
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);
        target.save_immunity_tags.Add("poison");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "viper_poison_immune_target",
            previewCommand: false
        );

        _test.False(
            target.HasStatusEffect("paralyzed"),
            "目标拥有 poison save immunity 时不应被毒蛇打击施加 paralyzed。"
        );
    }

    private void TestVenomInjectionUsageAndPrimedDamageLifecycle()
    {
        using ViperFixture fixture = ViperFixture.Build(new GArray { 4, 4, 2, 3 });
        BattleUnitState holder = fixture.BuildViperUnit("venom_injection");
        EquipmentInstanceState instance = FindEquippedInstance(holder, "eq_viper_venom_injection");
        _test.True(instance != null, "毒液注入测试应能找到装备实例。");
        if (instance == null)
            return;

        for (int use = 1; use <= 3; use++)
        {
            if (use > 1)
                holder.ResetPerTurnCharges();
            IssueVenomInjection(fixture, holder, $"viper_venom_injection_use_{use}", 0);
            _test.Eq(
                GetAbilityState(holder, VenomInjectionBindingId, VenomPrimedStateKey),
                1,
                $"第 {use} 次毒液注入后 venom_primed 应置为 1。"
            );
            if (use == 1)
            {
                BattleSkillAvailabilityView sameTurnView = BuildEquipmentSkillView(fixture, holder, 0);
                _test.True(
                    TryFindSkillEntry(sameTurnView, VenomInjectionSkillId, out BattleAvailableSkillEntry sameTurnEntry),
                    "同一行动回合内毒液注入入口仍应存在。"
                );
                _test.False(sameTurnEntry?.IsSelectable ?? true, "同一行动回合内毒液注入不能第二次使用。");
                _test.Eq(
                    sameTurnEntry?.DisabledReason ?? new StringName(""),
                    new StringName("equipment_skill_turn_use_exhausted"),
                    "同一行动回合第 2 次毒液注入应返回行动回合一次限制原因。"
                );
            }
        }
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                VenomInjectionGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            3,
            "毒液注入同一世界日应记录 3 次使用。"
        );
        StringName primedChargeKey = FindChargeKey(
            holder,
            VenomInjectionBindingId,
            VenomPrimedStateKey
        );
        _test.True(primedChargeKey != "", "毒液注入应声明并初始化 venom_primed 状态 key。");

        holder.ResetPerTurnCharges();
        BattleSkillAvailabilityView exhaustedView = BuildEquipmentSkillView(fixture, holder, 0);
        _test.True(
            TryFindSkillEntry(exhaustedView, VenomInjectionSkillId, out BattleAvailableSkillEntry exhaustedEntry),
            "同日用尽后毒液注入入口仍应存在。"
        );
        _test.False(exhaustedEntry?.IsSelectable ?? true, "第 4 次同日毒液注入应不可用。");
        _test.Eq(
            exhaustedEntry?.DisabledReason ?? new StringName(""),
            new StringName("equipment_skill_usage_exhausted"),
            "第 4 次同日毒液注入禁用原因应稳定。"
        );

        holder.ResetPerTurnCharges();
        BattleSkillAvailabilityView nextDayView = BuildEquipmentSkillView(fixture, holder, 15);
        _test.True(
            TryFindSkillEntry(nextDayView, VenomInjectionSkillId, out BattleAvailableSkillEntry nextDayEntry),
            "次日毒液注入入口应仍能解析。"
        );
        _test.True(nextDayEntry?.IsSelectable == true, "跨世界日后毒液注入应恢复可用。");

        BattleUnitState missTarget = BuildTarget("venom_injection_miss_target", new Vector2I(1, 0));
        missTarget.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 999);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            missTarget,
            "viper_venom_injection_miss",
            previewCommand: false
        );
        _test.Eq(
            primedChargeKey == "" ? 0 : holder.GetPerBattleChargeTyped(primedChargeKey, 0),
            1,
            "未命中不应清除 venom_primed。"
        );

        BattleUnitState plainTarget = BuildTarget("venom_injection_plain_target", new Vector2I(1, 0));
        plainTarget.current_hp = 100;
        plainTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        holder.equipment_ability_sources.Clear();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            plainTarget,
            "viper_venom_injection_non_viper_damage",
            previewCommand: false
        );
        _test.Eq(
            primedChargeKey == "" ? 0 : holder.GetPerBattleChargeTyped(primedChargeKey, 0),
            1,
            "非本武器装备能力伤害不应清除 venom_primed。"
        );

        fixture.Runtime._unit_factory.RefreshBattleUnit(holder);
        BattleUnitState primedTarget = BuildTarget("venom_injection_primed_target", new Vector2I(1, 0));
        primedTarget.current_hp = 100;
        primedTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        primedTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            primedTarget,
            "viper_venom_injection_primed_hit",
            previewCommand: false
        );
        _test.Eq(
            100 - primedTarget.current_hp,
            11,
            "venom_primed 命中后总毒伤应为 2D6：基础 +1D6 再额外 +1D6，不应变成 3D6。"
        );
        _test.Eq(
            GetAbilityState(holder, VenomInjectionBindingId, VenomPrimedStateKey),
            0,
            "下一次本武器成功命中并造成武器伤害后应清除 venom_primed。"
        );
    }

    private static void IssueVenomInjection(
        ViperFixture fixture,
        BattleUnitState holder,
        StringName battleId,
        int worldStep
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, worldStep);
        if (!TryFindSkillEntry(view, VenomInjectionSkillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException("missing venom injection equipment skill entry.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"venom injection entry disabled: {entry.DisabledReason}");

        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            null,
            worldStep: worldStep
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            skill_id = VenomInjectionSkillId,
            target_unit_id = holder.unit_id,
            target_coord = holder.coord,
        };
        command.AddTargetUnitId(holder.unit_id);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
            throw new InvalidOperationException($"venom injection preview blocked: {JoinLogs(preview?.LogLinesTyped)}");
        if (!fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(holder, command))
            throw new InvalidOperationException("venom injection equipment usage commit returned false.");
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        ViperFixture fixture,
        BattleUnitState unit,
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
                BattleState = WeaponAbilityCommandTestSupport.BuildFlatState(
                    "viper_skill_view",
                    unit,
                    null,
                    worldStep: worldStep
                ),
            }
        );
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

    private static int GetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        return key == "" ? 0 : unit.GetPerBattleChargeTyped(key, 0);
    }

    private static StringName FindChargeKey(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        string suffix = $"|{stateKey}";
        foreach (StringName key in unit.GetPerBattleChargesTyped().Keys)
        {
            if (key.ToString().EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        StringName sourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EquipmentDefId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        return sourceKey == ""
            ? new StringName("")
            : new StringName($"equipment_ability|state|{sourceKey}|{stateKey}");
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
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
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
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new List<BattleEquipmentAbilitySourceState>())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static bool HasDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit?.damage_resistances == null)
            return false;
        return unit.damage_resistances.ContainsKey(damageTag.ToString())
            || unit.damage_resistances.ContainsKey(damageTag);
    }

    private static StringName GetDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit?.damage_resistances == null)
            return "";
        if (unit.damage_resistances.TryGetValue(damageTag, out StringName value))
            return ProgressionDataUtils.to_string_name(value);
        if (unit.damage_resistances.TryGetValue(new StringName(damageTag.ToString()), out value))
            return ProgressionDataUtils.to_string_name(value);
        return "";
    }

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class ViperFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private ViperFixture(
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

        internal static ViperFixture Build(GArray damageRolls)
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
            return new ViperFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildViperUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ViperItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    ViperItemId,
                    $"eq_viper_{label}"
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

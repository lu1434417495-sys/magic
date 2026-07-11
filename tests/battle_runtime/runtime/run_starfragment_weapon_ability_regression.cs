using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_starfragment_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName StarfragmentItemId =
        "weapon_unique_axe_starfragment_100";
    private static readonly StringName StardustTouchTraitId =
        "weapon.axe.starfragment.stardust_touch";
    private static readonly StringName StarburstTraitId =
        "weapon.axe.starfragment.starburst";
    private static readonly StringName CosmicDreadTraitId =
        "weapon.axe.starfragment.cosmic_dread";
    private static readonly StringName StarburstSkillId =
        "weapon_axe_starfragment_starburst";
    private static readonly StringName StarburstGrantId =
        "grant.starfragment.starburst.skill";
    private static readonly StringName StardustTouchBindingId =
        "binding.weapon.axe.starfragment.stardust_touch";
    private static readonly StringName StarburstBindingId =
        "binding.weapon.axe.starfragment.starburst";
    private static readonly StringName CosmicDreadBindingId =
        "binding.weapon.axe.starfragment.cosmic_dread";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEnvironmentSnapshotDerivesNightFromWorldStepAndHonorsExplicitTags();
            TestStarfragmentProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestStarburstIsProjectedAsEquipmentGrantedSkillWithRealCombatConfig();
            TestStardustTouchAddsForceDamageOnlyAtNight();
            RequestTestExit(_test.Finish("Starfragment weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Starfragment weapon ability regression"));
        }
    }

    private void TestEnvironmentSnapshotDerivesNightFromWorldStepAndHonorsExplicitTags()
    {
        BattleEnvironmentSnapshot explicitNight =
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["global_environment_tags"] = new GStringNameArray { "night" } }
            );
        _test.True(explicitNight.HasGlobalTag("night"), "显式 night tag 应进入 battle 环境 snapshot。");

        BattleEnvironmentSnapshot derivedNight =
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = 12 }
            );
        _test.True(derivedNight.HasGlobalTag("night"), "world_step 日内 12 应推导为夜间。");

        BattleEnvironmentSnapshot derivedDay =
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = 2 }
            );
        _test.False(derivedDay.HasGlobalTag("night"), "world_step 日内 2 不应推导为夜间。");

        BattleEnvironmentSnapshot explicitTagsOverrideDerivedNight =
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary
                {
                    ["world_step"] = 12,
                    ["global_environment_tags"] = new GStringNameArray { "outdoors" },
                }
            );
        _test.False(
            explicitTagsOverrideDerivedNight.HasGlobalTag("night"),
            "显式 global_environment_tags 存在时应覆盖 world_step 自动推导。"
        );
        _test.True(
            explicitTagsOverrideDerivedNight.HasGlobalTag("outdoors"),
            "显式非 night tag 不应被丢弃。"
        );

        BattleState state = new();
        state.ReplaceEnvironmentSnapshot(explicitNight);
        _test.True(
            state.GetEnvironmentSnapshot().HasGlobalTag("night"),
            "BattleState 应持有当前战斗的环境 snapshot。"
        );
    }

    private void TestStarfragmentProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using StarfragmentFixture fixture = StarfragmentFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(StarfragmentItemId), "真实物品内容应包含星辰碎片。");
        _test.True(
            fixture.TraitDefs.ContainsKey(StardustTouchTraitId),
            "真实 trait 内容应包含星尘之触。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StarburstTraitId),
            "真实 trait 内容应包含星爆。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(CosmicDreadTraitId),
            "真实 trait 内容应包含宇宙恐惧。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StardustTouchBindingId),
            "真实装备能力内容应包含星尘之触 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StarburstBindingId),
            "真实装备能力内容应包含星爆 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(CosmicDreadBindingId),
            "真实装备能力内容应包含宇宙恐惧 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(StarburstSkillId),
            "真实技能内容应包含星爆装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(StarfragmentItemId))
            return;

        ItemDef rawStarfragment = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_starfragment.tres"
        );
        _test.True(rawStarfragment != null, "星辰碎片原始资源应能加载。");
        if (rawStarfragment != null)
        {
            _test.Eq(
                rawStarfragment.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "星辰碎片原始资源应声明继承 greataxe 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildStarfragmentUnit("projection");

        _test.Eq(equipped.weapon_item_id, StarfragmentItemId, "星辰碎片装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("greataxe"),
            "星辰碎片应投影为 greataxe。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "星辰碎片攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "星辰碎片应占用双手。");
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_count ?? 0,
            1,
            "星辰碎片双手骰数量应为 1。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_sides ?? 0,
            12,
            "星辰碎片双手骰面应为 D12。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.flat_bonus ?? 0,
            2,
            "星辰碎片双手骰固定加值应为 +2。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StardustTouchTraitId,
            StardustTouchBindingId,
            "eq_starfragment_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StarburstTraitId,
            StarburstBindingId,
            "eq_starfragment_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CosmicDreadTraitId,
            CosmicDreadBindingId,
            "eq_starfragment_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除星辰碎片后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除星辰碎片后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.weapon_attack_range,
            baseline.weapon_attack_range,
            "移除星辰碎片后攻击距离应回到装备前状态。"
        );
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "移除星辰碎片后装备能力源应清空。"
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除星辰碎片后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestStarburstIsProjectedAsEquipmentGrantedSkillWithRealCombatConfig()
    {
        using StarfragmentFixture fixture = StarfragmentFixture.Build(new GArray());
        _test.True(
            fixture.SkillDefs.TryGetValue(StarburstSkillId, out SkillDefinition starburst),
            "星爆应是 SkillDef，而不是 trait 自己承担主动动作。"
        );
        if (starburst == null)
            return;
        CombatSkillDefinition combat = starburst.CombatProfile;
        _test.True(combat != null, "星爆技能应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("ground"), "星爆应使用地面目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "星爆应只影响敌方目标。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "星爆应使用当前系统 radius 范围。");
        _test.Eq(combat.AreaValue, 2, "星爆 10 尺半径应落成当前系统半径 2 格。");
        _test.Eq(combat.ApCost, 1, "星爆应消耗 1 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "星爆应只有一个伤害 effect。");

        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.EffectType, new StringName("damage"), "星爆 effect 应是 damage。");
        _test.Eq(damage.DamageTag, new StringName("force"), "星爆伤害标签应是 force。");
        _test.Eq(damage.DiceCount, 2, "星爆伤害应是 2D8。");
        _test.Eq(damage.DiceSides, 8, "星爆伤害应是 2D8。");
        _test.Eq(damage.SaveDc, 14, "星爆豁免 DC 应是 14。");
        _test.Eq(damage.SaveAbility, new StringName("agility"), "星爆敏捷豁免应落成 agility。");
        _test.Eq(damage.SaveTag, new StringName("magic"), "星爆豁免标签应使用当前魔法豁免 tag。");
        _test.True(damage.SavePartialOnSuccess, "星爆豁免成功应减半。");

        _test.True(
            fixture.Bindings.TryGetValue(StarburstBindingId, out EquipmentAbilityBindingDefinition binding),
            "星爆 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "星爆 binding 应授予一个装备技能入口。");
        if (binding.GrantedActions.Count > 0)
        {
            EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
            _test.Eq(grant.SkillId, StarburstSkillId, "星爆 grant 应指向真实 SkillDef。");
            _test.Eq(grant.SkillLevel, 1, "星爆 grant 等级应为 1。");
            _test.Eq(
                grant.UsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                "星爆 grant 应声明 per_world_day 使用周期。"
            );
            _test.Eq(grant.MaxUsesPerPeriod, 1, "星爆 grant 应限制每世界日 1 次。");
        }

        BattleUnitState equipped = fixture.BuildStarfragmentUnit("starburst_skill");
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        BattleSkillAvailabilityView view = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = equipped,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 12,
            }
        );
        _test.True(
            TryFindSkillEntry(view, StarburstSkillId, out BattleAvailableSkillEntry entry),
            "装备星辰碎片后，unit 的可用技能应包含装备授予的星爆。"
        );
        if (entry != null)
        {
            _test.Eq(
                entry.EntryRef.SourceKind,
                BattleSkillEntrySourceKind.EquipmentSkill,
                "星爆技能入口来源应是 equipment_skill。"
            );
            _test.Eq(entry.SkillLevel, 1, "星爆装备技能等级应为 1。");
            _test.True(entry.IsSelectable, "未使用前星爆同日入口应可选。");
            _test.Eq(
                entry.EquipmentBindingId,
                StarburstBindingId,
                "星爆技能入口应携带 binding id。"
            );
            _test.Eq(
                entry.EquipmentGrantedActionId,
                StarburstGrantId,
                "星爆技能入口应携带 grant id。"
            );
            _test.Eq(
                entry.EquipmentUsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                "星爆技能入口应携带 per_world_day 使用周期。"
            );
            _test.Eq(entry.EquipmentMaxUsesPerPeriod, 1, "星爆技能入口应携带每日次数上限。");

            BattleUnitState target = BuildTarget("starburst_usage_target", new Vector2I(1, 0));
            fixture.Runtime.SetupStateForTests(
                BuildState("starfragment_starburst_usage", equipped, target, 12)
            );
            BattleCommand command = new()
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = equipped.unit_id,
                skill_entry_id = entry.EntryRef.SkillEntryId,
                skill_id = StarburstSkillId,
                target_coord = new Vector2I(1, 0),
            };
            _test.True(
                fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(equipped, command),
                "星爆实际成功后应能提交装备技能每日次数。"
            );
            EquipmentInstanceState instance = FindEquippedInstance(
                equipped,
                "eq_starfragment_starburst_skill"
            );
            _test.True(instance != null, "星爆测试应能找到装备实例。");
            if (instance != null)
            {
                _test.Eq(
                    EquipmentAbilityUsageRuntime.GetUsedCount(
                        instance,
                        StarburstGrantId,
                        EquipmentAbilityUsagePeriodKind.PerWorldDay,
                        WorldTimeSystem.StepToDay(12)
                    ),
                    1,
                    "星爆提交后应写入当前世界日使用次数。"
                );
            }

            equipped.current_ap = 2;
            BattleSkillAvailabilityView sameTurnView = service.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = equipped,
                    IncludeKnownSkills = false,
                    IncludeEquipmentSkills = true,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 12,
                }
            );
            _test.True(
                TryFindSkillEntry(sameTurnView, StarburstSkillId, out BattleAvailableSkillEntry sameTurnEntry),
                "同一行动回合使用后星爆入口仍应存在，供 UI 展示禁用原因。"
            );
            if (sameTurnEntry != null)
            {
                _test.False(sameTurnEntry.IsSelectable, "同一行动回合内星爆不能第二次使用。");
                _test.Eq(
                    sameTurnEntry.DisabledReason,
                    new StringName("equipment_skill_turn_use_exhausted"),
                    "同一行动回合内星爆禁用原因应来自装备技能行动回合一次限制。"
                );
            }

            equipped.ResetPerTurnCharges();
            equipped.current_ap = 2;
            BattleSkillAvailabilityView exhaustedView = service.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = equipped,
                    IncludeKnownSkills = false,
                    IncludeEquipmentSkills = true,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 12,
                }
            );
            _test.True(
                TryFindSkillEntry(exhaustedView, StarburstSkillId, out BattleAvailableSkillEntry exhaustedEntry),
                "跨行动回合后同日用尽的星爆入口仍应存在，供 UI 展示禁用原因。"
            );
            if (exhaustedEntry != null)
            {
                _test.False(exhaustedEntry.IsSelectable, "同日使用 1 次后星爆应禁用。");
                _test.Eq(
                    exhaustedEntry.DisabledReason,
                    new StringName("equipment_skill_usage_exhausted"),
                    $"同日星爆禁用原因应稳定。 charges={SummarizePerTurnCharges(equipped)} limits={SummarizePerTurnChargeLimits(equipped)} entry_source={exhaustedEntry.EntryRef.SourceEquipmentInstanceId} entry_effective={exhaustedEntry.EntryRef.SourceEquipmentEffectiveInstanceKey}"
                );
            }

            equipped.ResetPerTurnCharges();
            equipped.current_ap = 2;
            BattleSkillAvailabilityView nextDayView = service.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = equipped,
                    IncludeKnownSkills = false,
                    IncludeEquipmentSkills = true,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 27,
                }
            );
            _test.True(
                TryFindSkillEntry(nextDayView, StarburstSkillId, out BattleAvailableSkillEntry nextDayEntry),
                "次日星爆入口应仍能解析。"
            );
            if (nextDayEntry != null)
            {
                _test.True(
                    nextDayEntry.IsSelectable,
                    $"次日星爆应恢复可选。 disabled={nextDayEntry.DisabledReason} charges={SummarizePerTurnCharges(equipped)} limits={SummarizePerTurnChargeLimits(equipped)}"
                );
            }

            BattleUnitState issueCaster = fixture.BuildStarfragmentUnit("starburst_issue");
            issueCaster.SetCombatResources(40, 10, 0, 0, 1, 0);
            BattleUnitState issueTarget = BuildTarget("starburst_issue_target", new Vector2I(2, 0));
            BattleSkillAvailabilityView issueView = service.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = issueCaster,
                    IncludeKnownSkills = false,
                    IncludeEquipmentSkills = true,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 12,
                }
            );
            _test.True(
                TryFindSkillEntry(issueView, StarburstSkillId, out BattleAvailableSkillEntry issueEntry),
                "实际施放前应能解析星爆装备技能入口。"
            );
            if (issueEntry != null)
            {
                fixture.Runtime.SetupStateForTests(
                    BuildState("starfragment_starburst_issue", issueCaster, issueTarget, 12)
                );
                int targetHpBefore = issueTarget.current_hp;
                BattleCommand issueCommand = new()
                {
                    CommandKind = BattleCommandKind.Skill,
                    unit_id = issueCaster.unit_id,
                    skill_entry_id = issueEntry.EntryRef.SkillEntryId,
                    skill_id = StarburstSkillId,
                    target_coord = new Vector2I(2, 0),
                };
                BattlePreview issuePreview = fixture.Runtime.PreviewCommand(issueCommand);
                _test.True(
                    issuePreview.allowed,
                    $"星爆实际施放前 preview 应允许执行。logs={JoinLogLines(issuePreview.LogLinesTyped)}"
                );
                CombatCastVariantDefinition issueGroundVariant =
                    fixture.Runtime._skill_resolution_rules.ResolveGroundCastVariantDefinition(
                        starburst,
                        issueCaster,
                        ""
                    );
                BattleGroundSkillValidationResult issueValidation =
                    fixture.Runtime.ValidateGroundSkillCommandResultTyped(
                        issueCaster,
                        starburst,
                        issueGroundVariant,
                        issueCommand
                    );
                _test.True(
                    issueValidation.Allowed,
                    $"星爆实际施放前 ground validation 应允许执行。message={issueValidation.Message}"
                );
                BattleEventBatch issueBatch = fixture.Runtime.IssueCommand(issueCommand);
                _test.True(issueBatch != null, "星爆 IssueCommand 应返回事件 batch。");
                _test.True(
                    issueTarget.current_hp < targetHpBefore,
                    $"星爆实际 IssueCommand 应对范围内敌人造成伤害。logs={JoinLogLines(issueBatch?.LogLinesTyped)}"
                );
                EquipmentInstanceState issueInstance = FindEquippedInstance(
                    issueCaster,
                    "eq_starfragment_starburst_issue"
                );
                _test.True(issueInstance != null, "实际施放后应能找到星爆装备实例。");
                if (issueInstance != null)
                {
                    _test.Eq(
                        EquipmentAbilityUsageRuntime.GetUsedCount(
                            issueInstance,
                            StarburstGrantId,
                            EquipmentAbilityUsagePeriodKind.PerWorldDay,
                            WorldTimeSystem.StepToDay(12)
                        ),
                        1,
                        "星爆实际 IssueCommand 成功后应扣装备每日次数。"
                    );
                }
            }
        }
    }

    private void TestStardustTouchAddsForceDamageOnlyAtNight()
    {
        using StarfragmentFixture fixture = StarfragmentFixture.Build(new GArray());
        BattleUnitState dayAttacker = fixture.BuildStarfragmentUnit("stardust_day");
        BattleUnitState dayTarget = BuildTarget("stardust_day_target", new Vector2I(1, 0));
        dayTarget.current_hp = 100;
        dayTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            dayAttacker,
            dayTarget,
            "starfragment_stardust_day",
            worldStep: 2,
            previewCommand: false
        );
        int dayDamage = 100 - dayTarget.current_hp;

        BattleUnitState nightAttacker = fixture.BuildStarfragmentUnit("stardust_night");
        BattleUnitState nightTarget = BuildTarget("stardust_night_target", new Vector2I(1, 0));
        nightTarget.current_hp = 100;
        nightTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            nightAttacker,
            nightTarget,
            "starfragment_stardust_night",
            worldStep: 12,
            previewCommand: false
        );
        int nightDamage = 100 - nightTarget.current_hp;

        _test.True(dayDamage > 0, "非夜间真实基础攻击应造成武器伤害。");
        _test.True(
            nightDamage > dayDamage,
            "夜间真实基础攻击应因星尘之触追加 force 伤害。"
        );
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        int worldStep
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(5, 5),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = attacker.unit_id;
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

    private static BattleState BuildStateWithEnvironmentTags(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        GStringNameArray tags
    )
    {
        BattleState state = BuildState(battleId, attacker, target, 2);
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["global_environment_tags"] = tags }
            )
        );
        return state;
    }

    private static string JoinLogLines(IEnumerable<string> values)
    {
        if (values == null)
            return "";
        return string.Join(" | ", values);
    }

    private static string SummarizePerTurnCharges(BattleUnitState unit)
    {
        var entries = new List<string>();
        foreach ((StringName key, int value) in unit?.GetPerTurnChargesTyped() ?? new Dictionary<StringName, int>())
        {
            entries.Add($"{key}:{value}");
        }
        entries.Sort(StringComparer.Ordinal);
        return entries.Count == 0 ? "<none>" : string.Join(",", entries);
    }

    private static string SummarizePerTurnChargeLimits(BattleUnitState unit)
    {
        var entries = new List<string>();
        foreach ((StringName key, int value) in unit?.GetPerTurnChargeLimitsTyped() ?? new Dictionary<StringName, int>())
        {
            entries.Add($"{key}:{value}");
        }
        entries.Sort(StringComparer.Ordinal);
        return entries.Count == 0 ? "<none>" : string.Join(",", entries);
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
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
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
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class StarfragmentFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private StarfragmentFixture(
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

        internal static StarfragmentFixture Build(GArray damageRolls)
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
            return new StarfragmentFixture(
                itemRegistry,
                progressionRegistry,
                partyState,
                runtime
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildStarfragmentUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                StarfragmentItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    StarfragmentItemId,
                    $"eq_starfragment_{label}"
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

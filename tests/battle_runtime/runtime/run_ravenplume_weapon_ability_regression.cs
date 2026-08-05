using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_ravenplume_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName RavenplumeItemId =
        "weapon_unique_sword_ravenplume_017";
    private static readonly StringName CrowSummonTraitId =
        "weapon.sword.ravenplume.crow_summon";
    private static readonly StringName RavenCoverTraitId =
        "weapon.sword.ravenplume.raven_cover";
    private static readonly StringName CrowFeastTraitId =
        "weapon.sword.ravenplume.crow_feast";
    private static readonly StringName CrowClamorTraitId =
        "weapon.sword.ravenplume.crow_clamor";
    private static readonly StringName CrowFeastSkillId =
        "weapon_sword_ravenplume_crow_feast";
    private static readonly StringName CrowFeastGrantId =
        "grant.ravenplume.crow_feast.skill";
    private static readonly StringName CrowSummonBindingId =
        "binding.weapon.sword.ravenplume.crow_summon";
    private static readonly StringName RavenCoverBindingId =
        "binding.weapon.sword.ravenplume.raven_cover";
    private static readonly StringName CrowFeastBindingId =
        "binding.weapon.sword.ravenplume.crow_feast";
    private static readonly StringName CrowClamorBindingId =
        "binding.weapon.sword.ravenplume.crow_clamor";
    private static readonly StringName CrowStateKey = "ravenplume_crows";
    private static readonly StringName UndeadConversionBlockedStatusId =
        "undead_conversion_blocked";

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
            TestRavenplumeProjectsRealContentAndTypedPayloads();
            TestRavenplumeSummonsBattleUnitsAndCapsAtTwelve();
            TestRavenplumeCrowModifiersAndFeastUseLivingCrowCount();
            RequestTestExit(_test.Finish("Ravenplume weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Ravenplume weapon ability regression"));
        }
    }

    private void TestRavenplumeProjectsRealContentAndTypedPayloads()
    {
        using RavenplumeFixture fixture = RavenplumeFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(RavenplumeItemId), "真实物品内容应包含鸦羽。");
        _test.True(fixture.TraitDefs.ContainsKey(CrowSummonTraitId), "真实 trait 内容应包含鸦群召唤。");
        _test.True(fixture.TraitDefs.ContainsKey(RavenCoverTraitId), "真实 trait 内容应包含鸦羽遮蔽。");
        _test.True(fixture.TraitDefs.ContainsKey(CrowFeastTraitId), "真实 trait 内容应包含群鸦之宴。");
        _test.True(fixture.TraitDefs.ContainsKey(CrowClamorTraitId), "真实 trait 内容应包含群鸦喧嚣。");
        _test.True(fixture.Bindings.ContainsKey(CrowSummonBindingId), "真实装备能力内容应包含鸦群召唤 binding。");
        _test.True(fixture.Bindings.ContainsKey(RavenCoverBindingId), "真实装备能力内容应包含鸦羽遮蔽 binding。");
        _test.True(fixture.Bindings.ContainsKey(CrowFeastBindingId), "真实装备能力内容应包含群鸦之宴 binding。");
        _test.True(fixture.Bindings.ContainsKey(CrowClamorBindingId), "真实装备能力内容应包含群鸦喧嚣 binding。");
        _test.True(fixture.SkillDefs.ContainsKey(CrowFeastSkillId), "真实技能内容应包含群鸦之宴装备技能。");

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_shortsword_ravenplume.tres"
        );
        _test.True(rawItem != null, "鸦羽原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_shortsword_base"), "鸦羽应继承 shortsword 模板。");
            _test.Eq(rawItem.base_price, 62000, "鸦羽价格应落成 62000。");
            _test.True(ContainsStringName(rawItem.tags, "ravenplume"), "鸦羽物品 tag 应包含 ravenplume。");
        }

        BattleUnitState equipped = fixture.BuildRavenplumeUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, RavenplumeItemId, "鸦羽装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("shortsword"), "鸦羽应投影为 shortsword。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "鸦羽单手伤害应为 1D6+2。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 6, "鸦羽单手伤害应为 1D6+2。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 2, "鸦羽单手伤害应为 1D6+2。");
        AssertUnitHasTraitAndAbilitySource(equipped, CrowSummonTraitId, CrowSummonBindingId, "eq_ravenplume_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, RavenCoverTraitId, RavenCoverBindingId, "eq_ravenplume_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, CrowFeastTraitId, CrowFeastBindingId, "eq_ravenplume_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, CrowClamorTraitId, CrowClamorBindingId, "eq_ravenplume_projection");

        AssertCrowSummonPayload(fixture.Bindings[CrowSummonBindingId]);
        AssertRavenCoverPayload(fixture.Bindings[RavenCoverBindingId]);
        AssertCrowFeastPayload(fixture.Bindings[CrowFeastBindingId]);
        AssertCrowClamorPayload(fixture.Bindings[CrowClamorBindingId]);
    }

    private void TestRavenplumeSummonsBattleUnitsAndCapsAtTwelve()
    {
        using RavenplumeFixture fixture = RavenplumeFixture.Build();
        BattleUnitState holder = fixture.BuildRavenplumeUnit("summon");
        holder.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState defeated = BuildTarget("ravenplume_defeated", new Vector2I(3, 3));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "ravenplume_summon",
            holder,
            defeated,
            mapSize: new Vector2I(9, 9)
        );
        fixture.Runtime.SetupStateForTests(state);
        defeated.SetCurrentHp(0);
        defeated.MarkDead();

        using BattleEventBatch batch = new();
        fixture.Runtime.HandleUnitDefeatedByRuntimeEffect(
            defeated,
            holder,
            batch,
            "ravenplume defeated.",
            new BattleDefeatHandlingOptions(
                recordEnemyDefeatedAchievement: false
            )
        );
        for (int index = 0; index < 11; index++)
        {
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
                new BattleEquipmentAbilityOnKillContext
                {
                    SourceUnit = holder,
                    DefeatedUnit = defeated,
                    BattleState = state,
                    Batch = batch,
                }
            );
        }

        List<BattleUnitState> crows = FindLivingCrows(state, holder);
        _test.Eq(crows.Count, 12, "鸦群召唤应生成真实战斗单位，并按同一来源上限 12 截断。");
        BattleUnitState crow = crows.Count > 0 ? crows[0] : null;
        _test.True(crow != null, "至少应能找到一个召唤乌鸦单位。");
        if (crow == null)
            return;
        _test.True(crow.ai_blackboard?.summoned == true, "乌鸦应标记为 summoned。");
        _test.Eq(crow.ai_blackboard?.summon_source_unit_id ?? new StringName(""), holder.unit_id, "乌鸦应记录召唤来源单位。");
        _test.Eq(crow.ai_blackboard?.summon_binding_id ?? new StringName(""), CrowSummonBindingId, "乌鸦应记录召唤来源 binding。");
        _test.Eq(crow.ai_blackboard?.summon_state_key ?? new StringName(""), CrowStateKey, "乌鸦应记录召唤 state key。");
        _test.True(state.GetUnit(crow.unit_id) == crow, "乌鸦必须进入 BattleState unit store，而不是只写计数。");
        _test.True(state.ally_unit_ids.Contains(crow.unit_id), "玩家持有者召唤的乌鸦应进入友方单位列表。");
        _test.Eq(crow.GetCurrentHp(), 1, "乌鸦当前 HP 应为 1。");
        _test.Eq(crow.attribute_snapshot.GetValue(AttributeService.HP_MAX), 1, "乌鸦最大 HP 应为 1。");
        _test.Eq(crow.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS), 12, "乌鸦 AC 应为 12。");
        _test.Eq(
            crow.GetBaseCognitionKindTyped(),
            BattleCognitionKind.Instinctive,
            "召唤乌鸦应由 payload 数据驱动为野兽心智。"
        );
        _test.True(crow.HasCreatureTypeTag("familiar"), "乌鸦应保留 familiar 标签。");
        _test.True(ContainsStringName(batch.ChangedUnitIdsTyped, crow.unit_id), "召唤应把新增乌鸦写入 changed unit。");
    }

    private void TestRavenplumeCrowModifiersAndFeastUseLivingCrowCount()
    {
        using RavenplumeFixture fixture = RavenplumeFixture.Build(new GArray { 6, 6, 6, 6 });
        BattleUnitState holder = fixture.BuildRavenplumeUnit("feast");
        holder.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState defeated = BuildTarget("ravenplume_feast_defeated", new Vector2I(3, 3));
        BattleUnitState feastTarget = BuildTarget("ravenplume_feast_target", new Vector2I(2, 1));
        feastTarget.SetCurrentHp(1);
        feastTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 1);
        BattleUnitState enemyAttacker = BuildTarget("ravenplume_cover_attacker", new Vector2I(7, 5));

        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "ravenplume_feast",
            holder,
            defeated,
            mapSize: new Vector2I(9, 9)
        );
        AddUnitToState(fixture.Runtime, state, feastTarget);
        AddUnitToState(fixture.Runtime, state, enemyAttacker);
        fixture.Runtime.SetupStateForTests(state);
        defeated.MarkDead();
        fixture.Runtime._grid_service.ClearUnitOccupancy(state, defeated);

        using BattleEventBatch summonBatch = new();
        for (int index = 0; index < 12; index++)
        {
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
                new BattleEquipmentAbilityOnKillContext
                {
                    SourceUnit = holder,
                    DefeatedUnit = defeated,
                    BattleState = state,
                    Batch = summonBatch,
                }
            );
        }

        List<BattleUnitState> crows = FindLivingCrows(state, holder);
        _test.Eq(crows.Count, 12, "修正和群鸦之宴 fixture 应先拥有 12 只存活乌鸦。");
        PlaceCrowCluster(fixture.Runtime, state, crows, enemyAttacker.GetAnchorCoord());

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("ravenplume_fixture_attack");
        BattleAttackRollModifierBundle coverBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                enemyAttacker,
                holder,
                attackSkill,
                "skill_attack_check",
                "ravenplume_cover",
                force_hit_no_crit: false
            )
        );
        _test.True(
            HasModifier(coverBundle, RavenCoverBindingId, -4),
            "附近 4 只以上存活乌鸦应让敌人攻击检定受到鸦羽遮蔽 -4，上限不继续增加。"
        );

        BattleAttackRollModifierBundle clamorBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                feastTarget,
                attackSkill,
                "skill_attack_check",
                "ravenplume_clamor",
                force_hit_no_crit: false
            )
        );
        _test.True(
            HasModifier(clamorBundle, CrowClamorBindingId, 2),
            "存活乌鸦不少于 6 只时，持有者攻击应获得群鸦喧嚣 +2。"
        );

        BattleSkillAvailabilityView readyView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            state
        );
        _test.True(
            TryFindSkillEntry(readyView, CrowFeastSkillId, out BattleAvailableSkillEntry feastEntry),
            "拥有 4 只以上乌鸦时应显示群鸦之宴装备技能。"
        );
        _test.True(feastEntry?.IsSelectable == true, "拥有 4 只以上乌鸦时群鸦之宴应可选择。");
        int crowsBeforeFeast = CountLivingCrows(state, holder);
        BattleEventBatch feastBatch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            feastTarget,
            feastEntry,
            CrowFeastSkillId,
            "ravenplume_crow_feast"
        );
        _test.True(
            feastTarget.GetCurrentHp() <= 0 || !feastTarget.IsAlive(),
            $"群鸦之宴应造成 4D6 necrotic 伤害并击杀 1HP 目标。 logs={JoinLogs(feastBatch)}"
        );
        _test.Eq(
            CountLivingCrows(state, holder),
            crowsBeforeFeast - 4,
            "群鸦之宴应消耗同一来源的 4 只存活乌鸦。"
        );
        _test.True(
            feastTarget.HasStatusEffect(UndeadConversionBlockedStatusId),
            "群鸦之宴击杀目标后应写入不可转化为亡灵的状态标记。"
        );

        foreach (BattleUnitState crow in FindLivingCrows(state, holder))
            fixture.Runtime.RemoveSummonedUnitFromBattle(crow, null);
        BattleSkillAvailabilityView emptyView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            state
        );
        _test.True(
            TryFindSkillEntry(emptyView, CrowFeastSkillId, out BattleAvailableSkillEntry emptyEntry),
            "乌鸦不足时群鸦之宴入口仍应保留给 UI 展示。"
        );
        _test.False(emptyEntry?.IsSelectable == true, "乌鸦不足 4 只时群鸦之宴不应可用。");
    }

    private static void AssertCrowSummonPayload(EquipmentAbilityBindingDefinition binding)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        if (action?.PayloadDefinition is not SummonUnitsActionPayloadDefinition payload)
            throw new InvalidOperationException("鸦群召唤 action 应投影为 summon_units payload。");
        if (payload.CountDice?.Terms?.Count != 1)
            throw new InvalidOperationException("鸦群召唤应声明 2D6 数量骰。");
        if (payload.CountDice.Terms[0].DiceCount != 2 || payload.CountDice.Terms[0].DiceSides != 6)
            throw new InvalidOperationException("鸦群召唤应是 2D6。");
        if (payload.MaxLivingUnits != 12)
            throw new InvalidOperationException("鸦群召唤上限应为 12。");
        if (payload.DurationTu != 60)
            throw new InvalidOperationException("鸦群召唤持续时间应为 60 TU。");
        if (payload.UnitDisplayName != "乌鸦")
            throw new InvalidOperationException("鸦群召唤应生成乌鸦单位。");
        if (payload.CognitionKind != BattleCognitionKind.Instinctive)
            throw new InvalidOperationException("鸦群召唤 payload 应声明野兽心智。");
        if (payload.HpMax != 1 || payload.ArmorClass != 12)
            throw new InvalidOperationException("乌鸦单位应为 AC12 HP1。");
    }

    private static void AssertRavenCoverPayload(EquipmentAbilityBindingDefinition binding)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        if (action?.PayloadDefinition is not SummonedUnitAttackRollModifierActionPayloadDefinition payload)
            throw new InvalidOperationException("鸦羽遮蔽 action 应投影为 summoned_unit_attack_roll_modifier payload。");
        if (payload.StateKey != CrowStateKey || payload.SourceBindingId != CrowSummonBindingId)
            throw new InvalidOperationException("鸦羽遮蔽应只读取鸦群召唤产生的乌鸦。");
        if (payload.BonusPerUnit != -1 || payload.MaxAbsoluteBonus != 4)
            throw new InvalidOperationException("鸦羽遮蔽应为每只 -1，最多 -4。");
    }

    private static void AssertCrowFeastPayload(EquipmentAbilityBindingDefinition binding)
    {
        if (binding?.GrantedActions?.Count != 1)
            throw new InvalidOperationException("群鸦之宴 binding 应授予一个装备技能。");
        EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
        if (grant.GrantedActionId != CrowFeastGrantId || grant.SkillId != CrowFeastSkillId)
            throw new InvalidOperationException("群鸦之宴 grant id/skill id 应稳定。");
        IReadOnlyList<EquipmentAbilityActionDefinition> actions =
            binding.Reactions?[0]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
        if (actions.Count != 2)
            throw new InvalidOperationException("群鸦之宴 after_skill 应包含消耗乌鸦和击杀标记两个 action。");
        if (actions[0]?.PayloadDefinition is not ConsumeSummonedUnitsActionPayloadDefinition consume)
            throw new InvalidOperationException("群鸦之宴第一个 action 应投影为 consume_summoned_units payload。");
        if (consume.Count != 4 || consume.StateKey != CrowStateKey)
            throw new InvalidOperationException("群鸦之宴应消耗 4 只同一 state 的乌鸦。");
        if (actions[1]?.PayloadDefinition is not ApplyStatusActionPayloadDefinition status)
            throw new InvalidOperationException("群鸦之宴第二个 action 应投影为 apply_status payload。");
        if (status.StatusId != UndeadConversionBlockedStatusId)
            throw new InvalidOperationException("群鸦之宴击杀标记 status id 应稳定。");
    }

    private static void AssertCrowClamorPayload(EquipmentAbilityBindingDefinition binding)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        if (action?.PayloadDefinition is not AttackRollBonusActionPayloadDefinition payload)
            throw new InvalidOperationException("群鸦喧嚣 action 应投影为 attack_roll_bonus payload。");
        if (payload.Bonus != 2)
            throw new InvalidOperationException("群鸦喧嚣应提供 +2 攻击检定。");
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 30,
            stamina: 30,
            ap: 2,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 8);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 8);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 12);
        unit.AddCreatureTypeTagTyped("humanoid");
        return unit;
    }

    private static void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        if (runtime == null || state == null || unit == null)
            return;
        state.SetUnit(unit);
        if (unit.faction_id == "player")
        {
            if (!state.ally_unit_ids.Contains(unit.unit_id))
                state.ally_unit_ids.Add(unit.unit_id);
        }
        else if (!state.enemy_unit_ids.Contains(unit.unit_id))
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        if (!runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true))
            throw new InvalidOperationException($"unable to place unit {unit.unit_id} at {unit.GetAnchorCoord()}.");
    }

    private static void PlaceCrowCluster(
        BattleRuntimeModule runtime,
        BattleState state,
        IReadOnlyList<BattleUnitState> crows,
        Vector2I center
    )
    {
        Vector2I[] coords =
        {
            center + new Vector2I(-1, 0),
            center + new Vector2I(1, 0),
            center + new Vector2I(0, -1),
            center + new Vector2I(0, 1),
        };
        for (int index = 0; index < coords.Length && index < (crows?.Count ?? 0); index++)
        {
            BattleUnitState crow = crows[index];
            runtime._grid_service.ClearUnitOccupancy(state, crow);
            if (!runtime._grid_service.PlaceUnit(state, crow, coords[index], true))
                throw new InvalidOperationException($"unable to place crow {crow.unit_id} at {coords[index]}.");
        }
    }

    private static List<BattleUnitState> FindLivingCrows(
        BattleState state,
        BattleUnitState holder
    )
    {
        var result = new List<BattleUnitState>();
        IReadOnlyList<BattleUnitState> units = Array.Empty<BattleUnitState>();
        if (state != null)
            units = state.GetUnitsTyped();
        foreach (BattleUnitState unit in units)
        {
            if (IsLivingCrow(unit, holder))
                result.Add(unit);
        }
        return result;
    }

    private static int CountLivingCrows(BattleState state, BattleUnitState holder) =>
        FindLivingCrows(state, holder).Count;

    private static bool IsLivingCrow(BattleUnitState unit, BattleUnitState holder) =>
        unit != null
        && unit.IsAlive()
        && unit.ai_blackboard?.summoned == true
        && unit.ai_blackboard.summon_source_unit_id == (holder?.unit_id ?? new StringName(""))
        && unit.ai_blackboard.summon_binding_id == CrowSummonBindingId
        && unit.ai_blackboard.summon_state_key == CrowStateKey;

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        RavenplumeFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
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

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(user);
        BattleState state = runtime?.GetState();
        if (state != null)
        {
            state.PhaseKind = BattlePhaseKind.UnitActing;
            state.active_unit_id = user.unit_id;
        }
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            BattleUnitSkillTargetAffordance affordance =
                runtime.GetUnitSkillTargetAffordance(
                    user,
                    target,
                    entry?.SkillDefinition,
                    require_ap: true
                );
            int distance = runtime.GetGridService().GetDistanceBetweenUnits(user, target);
            bool targetInState = state?.GetUnit(target?.unit_id ?? new StringName("")) != null;
            throw new InvalidOperationException(
                $"{label} unit skill preview blocked: {JoinLogs(preview)} | affordance={affordance.Allowed}/{affordance.Reason} distance={distance} range={entry?.SkillDefinition?.CombatProfile?.RangeValue ?? -1} ap={user?.GetCurrentAp() ?? -1} target_alive={target?.IsAlive()} target_in_state={targetInState} crows={CountLivingCrows(state, user)}"
            );
        }
        return runtime.IssueCommand(command);
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

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.HasEffectiveTrait(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
    }

    private static BattleEquipmentAbilitySourceReadView FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source in
            unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        if (values == null)
            return false;
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static string JoinLogs(BattleEventBatch batch) =>
        string.Join(" | ", batch?.LogLinesTyped ?? Array.Empty<string>());

    private sealed class RavenplumeFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private RavenplumeFixture(
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

        internal static RavenplumeFixture Build(GArray fixedDamageRolls = null)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(fixedDamageRolls ?? new GArray()));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new RavenplumeFixture(
                characterManagement,
                partyState,
                runtime,
                snapshot
            );
        }

        internal BattleUnitState BuildRavenplumeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                RavenplumeItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    RavenplumeItemId,
                    $"eq_ravenplume_{label}"
                )
            );
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

        public void Dispose()
        {
            Runtime?.dispose();
            _characterManagement?.Dispose();
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

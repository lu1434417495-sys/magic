using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_gorgon_crossbow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName GorgonCrossbowItemId =
        "weapon_unique_crossbow_gorgon_329";
    private static readonly StringName PetrifyingGazeTraitId =
        "weapon.crossbow.gorgon.petrifying_gaze";
    private static readonly StringName CompletePetrificationTraitId =
        "weapon.crossbow.gorgon.complete_petrification";
    private static readonly StringName StatueShatterTraitId =
        "weapon.crossbow.gorgon.statue_shatter";
    private static readonly StringName PetrifyingGazeBindingId =
        "binding.weapon.crossbow.gorgon.petrifying_gaze";
    private static readonly StringName CompletePetrificationBindingId =
        "binding.weapon.crossbow.gorgon.complete_petrification";
    private static readonly StringName StatueShatterBindingId =
        "binding.weapon.crossbow.gorgon.statue_shatter";
    private static readonly StringName PetrificationCountStatusId =
        "gorgon_crossbow_petrification_count";
    private static readonly StringName SlowStatusId = "slow";
    private static readonly StringName ParalyzedStatusId = "paralyzed";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGorgonCrossbowContentLoadsProjectsAndClearsOnUnequip();
            TestPetrifyingGazeAppliesSlowForSixtyTuOnlyOnFailedConSave();
            TestThirdHitConsumesCountAndParalyzesOnlyOnFailedConSave();
            TestStatueShatterAddsBludgeonDamageAgainstSlowedOrParalyzedTargets();
            TestStatueShatterKillSpreadAppliesShortSlowToAdjacentEnemiesOnly();
            TestStatueShatterKillSpreadHonorsConSaveSuccess();
            RequestTestExit(_test.Finish("Gorgon Crossbow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Gorgon Crossbow weapon ability regression"));
        }
    }

    private void TestGorgonCrossbowContentLoadsProjectsAndClearsOnUnequip()
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray());
        _test.True(
            fixture.ItemDefs.ContainsKey(GorgonCrossbowItemId),
            "真实物品内容应包含蛇发女妖之弩。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(PetrifyingGazeTraitId),
            "真实 trait 内容应包含石化凝视。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(CompletePetrificationTraitId),
            "真实 trait 内容应包含完全石化。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StatueShatterTraitId),
            "真实 trait 内容应包含石像崩裂。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(PetrifyingGazeBindingId),
            "真实装备能力内容应包含石化凝视 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(CompletePetrificationBindingId),
            "真实装备能力内容应包含完全石化 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StatueShatterBindingId),
            "真实装备能力内容应包含石像崩裂 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(GorgonCrossbowItemId))
            return;

        ItemDef rawGorgon = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_heavy_crossbow_gorgon.tres"
        );
        _test.True(rawGorgon != null, "蛇发女妖之弩原始资源应能加载。");
        if (rawGorgon != null)
        {
            _test.Eq(rawGorgon.display_name, "蛇发女妖之弩", "显示名应来自设计源。");
            _test.Eq(
                rawGorgon.base_item_id,
                new StringName("weapon_type_heavy_crossbow_base"),
                "蛇发女妖之弩应继承 heavy crossbow 模板。"
            );
            _test.Eq(rawGorgon.base_price, 58000, "蛇发女妖之弩基础价格应为 58000。");
            WeaponProfileDef profile = rawGorgon.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "蛇发女妖之弩应声明武器 profile override。");
            if (profile != null)
            {
                _test.Eq(profile.weapon_type_id, new StringName("heavy_crossbow"), "武器类型应为 heavy_crossbow。");
                _test.Eq(profile.training_group, new StringName("martial"), "训练组应为 martial。");
                _test.Eq(profile.range_type, new StringName("ranged"), "射程类型应为 ranged。");
                _test.Eq(profile.family, new StringName("crossbow"), "武器家族应为 crossbow。");
                _test.Eq(profile.damage_tag, new StringName("physical_pierce"), "基础伤害应为穿刺。");
                _test.Eq(profile.attack_range, 10, "攻击距离应为 10。");
                _test.True(profile.one_handed_dice == null, "重弩不应声明单手伤害。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 1, "重弩伤害应为 1D10+2。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 10, "重弩伤害应为 1D10+2。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 2, "重弩伤害应为 1D10+2。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "two_handed"), "应声明 two_handed。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "heavy"), "应声明 heavy。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "loading"), "应声明 loading。");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildGorgonUnit("projection");
        _test.Eq(equipped.weapon_item_id, GorgonCrossbowItemId, "装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("heavy_crossbow"), "应投影为 heavy_crossbow。");
        _test.Eq(equipped.weapon_family, new StringName("crossbow"), "应投影 crossbow 家族。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "应投影穿刺伤害。");
        _test.Eq(equipped.weapon_attack_range, 10, "应投影 10 格攻击距离。");
        _test.True(equipped.weapon_uses_two_hands, "蛇发女妖之弩应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "投影伤害应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "投影伤害应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "投影伤害应为 1D10+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            PetrifyingGazeTraitId,
            PetrifyingGazeBindingId,
            "eq_gorgon_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CompletePetrificationTraitId,
            CompletePetrificationBindingId,
            "eq_gorgon_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StatueShatterTraitId,
            StatueShatterBindingId,
            "eq_gorgon_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除蛇发女妖之弩后 weapon_item_id 应清空。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestPetrifyingGazeAppliesSlowForSixtyTuOnlyOnFailedConSave()
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildGorgonUnit("gaze");

        BattleUnitState failedTarget = BuildTarget("gaze_failed", new Vector2I(2, 0), hp: 100);
        BattleEquipmentAbilityAfterHitResult failedResult = ResolveAfterHit(
            fixture,
            attacker,
            failedTarget,
            "gorgon_gaze_failed",
            saveRollOverride: 1
        );
        BattleStatusEffectState slow = failedTarget.GetStatusEffect(SlowStatusId);
        _test.True(slow != null, "DC16 CON/petrification 豁免失败应施加部分石化 slow。");
        _test.Eq(slow?.duration ?? -1, 60, "部分石化 slow 应持续 60 TU。");
        _test.Eq(slow?.display_label ?? "", "部分石化", "slow 显示名应表现为部分石化。");
        _test.Eq(slow?.source_unit_id ?? new StringName(""), attacker.unit_id, "部分石化应记录持有者来源。");
        _test.Eq(
            BattleStatusSemanticTable.GetMoveCostDelta(slow),
            1,
            "部分石化应沿用 slow 的移动成本 +1 语义。"
        );
        BattleEquipmentAbilityStatusActionResult slowResult = FindStatusResult(failedResult, SlowStatusId);
        _test.True(slowResult?.Applied == true, "失败豁免应报告 slow 已施加。");
        _test.Eq(slowResult?.SaveResult.Dc ?? 0, 16, "石化凝视 DC 应为 16。");
        _test.Eq(
            slowResult?.SaveResult.Ability ?? new StringName(""),
            new StringName("constitution"),
            "石化凝视应使用 CON 豁免。"
        );

        BattleUnitState successTarget = BuildTarget("gaze_success", new Vector2I(2, 0), hp: 100);
        BattleEquipmentAbilityAfterHitResult successResult = ResolveAfterHit(
            fixture,
            attacker,
            successTarget,
            "gorgon_gaze_success",
            saveRollOverride: 20
        );
        _test.False(successTarget.HasStatusEffect(SlowStatusId), "DC16 CON/petrification 豁免成功时不应 slow。");
        BattleEquipmentAbilityStatusActionResult skippedSlow = FindStatusResult(successResult, SlowStatusId);
        _test.True(skippedSlow != null, "成功豁免也应产生状态门控结果。");
        _test.False(skippedSlow?.Applied ?? true, "成功豁免应报告状态未施加。");
    }

    private void TestThirdHitConsumesCountAndParalyzesOnlyOnFailedConSave()
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildGorgonUnit("third_hit");
        BattleUnitState target = BuildTarget("third_hit_target", new Vector2I(2, 0), hp: 100);

        for (int hit = 1; hit <= 2; hit++)
        {
            ResolveAfterHit(
                fixture,
                attacker,
                target,
                $"gorgon_same_target_hit_{hit}",
                saveRollOverride: 20
            );
            BattleStatusEffectState counter = target.GetStatusEffect(PetrificationCountStatusId);
            _test.True(counter != null, $"第 {hit} 次命中应记录石化积累。");
            _test.Eq(counter?.stacks ?? 0, hit, $"第 {hit} 次命中后石化积累应为 {hit}。");
            _test.False(target.HasStatusEffect(ParalyzedStatusId), $"第 {hit} 次命中不应触发完全石化。");
        }

        BattleEquipmentAbilityAfterHitResult thirdResult = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "gorgon_same_target_hit_3",
            saveRollOverride: 1
        );
        _test.False(
            target.HasStatusEffect(PetrificationCountStatusId),
            "第三次命中触发完全石化检定后应消耗 3 层石化积累。"
        );
        BattleStatusEffectState paralyzed = target.GetStatusEffect(ParalyzedStatusId);
        _test.True(paralyzed != null, "第三次命中且 DC18 CON 豁免失败时应 paralyzed。");
        _test.Eq(paralyzed?.duration ?? -1, 60, "完全石化 paralyzed 应持续 60 TU。");
        BattleEquipmentAbilityStatusActionResult paralyzeResult = FindStatusResult(
            thirdResult,
            ParalyzedStatusId
        );
        _test.True(paralyzeResult?.Applied == true, "失败豁免应报告 paralyzed 已施加。");
        _test.Eq(paralyzeResult?.SaveResult.Dc ?? 0, 18, "完全石化 DC 应为 18。");

        BattleUnitState successTarget = BuildTarget("third_hit_success", new Vector2I(2, 0), hp: 100);
        for (int hit = 1; hit <= 3; hit++)
        {
            ResolveAfterHit(
                fixture,
                attacker,
                successTarget,
                $"gorgon_success_target_hit_{hit}",
                saveRollOverride: 20
            );
        }
        _test.False(successTarget.HasStatusEffect(ParalyzedStatusId), "第三次命中但 DC18 豁免成功时不应 paralyzed。");
        _test.False(
            successTarget.HasStatusEffect(PetrificationCountStatusId),
            "第三次命中的 DC18 豁免成功也应消耗 3 层石化积累。"
        );
    }

    private void TestStatueShatterAddsBludgeonDamageAgainstSlowedOrParalyzedTargets()
    {
        AssertStatueShatterDamageForStatus(SlowStatusId, "gorgon_shatter_slow");
        AssertStatueShatterDamageForStatus(ParalyzedStatusId, "gorgon_shatter_paralyzed");

        using GorgonFixture fixture = GorgonFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState attacker = fixture.BuildGorgonUnit("shatter_clean");
        BattleUnitState cleanTarget = BuildTarget("shatter_clean_target", new Vector2I(2, 0), hp: 100);
        cleanTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            cleanTarget,
            "gorgon_shatter_clean",
            previewCommand: false
        );
        _test.Eq(100 - cleanTarget.current_hp, 7, "未被 slow/paralyzed 的目标只应承受 1D10+2 基础伤害。");
    }

    private void AssertStatueShatterDamageForStatus(StringName statusId, StringName battleId)
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray { 5, 2, 3 });
        BattleUnitState attacker = fixture.BuildGorgonUnit($"{battleId}_attacker");
        BattleUnitState target = BuildTarget($"{battleId}_target", new Vector2I(2, 0), hp: 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        SetSimpleStatus(target, statusId, 60, attacker.unit_id, statusId == SlowStatusId ? "部分石化" : "");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            battleId,
            previewCommand: false
        );
        _test.Eq(
            100 - target.current_hp,
            12,
            $"{statusId} 目标应承受基础 1D10+2 加石像崩裂 2D6 bludgeon。"
        );
    }

    private void TestStatueShatterKillSpreadAppliesShortSlowToAdjacentEnemiesOnly()
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray());
        BattleUnitState killer = fixture.BuildGorgonUnit("spread");
        killer.SetAnchorCoord(Vector2I.Zero);

        BattleUnitState defeated = BuildTarget("spread_defeated", new Vector2I(2, 2), hp: 0);
        defeated.is_alive = false;
        SetSimpleStatus(defeated, SlowStatusId, 60, killer.unit_id, "部分石化");

        BattleUnitState adjacentFail = BuildTarget("spread_adjacent_fail", new Vector2I(2, 3), hp: 30);
        adjacentFail.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);
        BattleUnitState adjacentLongSlow = BuildTarget("spread_adjacent_long_slow", new Vector2I(1, 2), hp: 30);
        adjacentLongSlow.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);
        SetSimpleStatus(adjacentLongSlow, SlowStatusId, 60, "other_source", "部分石化");
        BattleUnitState nonAdjacent = BuildTarget("spread_non_adjacent", new Vector2I(4, 4), hp: 30);
        nonAdjacent.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);
        BattleUnitState ally = BuildAlly("spread_ally", new Vector2I(2, 1));
        ally.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, -100);

        BattleState state = BuildFlatState("gorgon_shatter_spread", new Vector2I(6, 6));
        foreach (BattleUnitState unit in new[] { killer, defeated, adjacentFail, adjacentLongSlow, nonAdjacent, ally })
            state.SetUnit(unit);
        state.ally_unit_ids.Add(killer.unit_id);
        state.ally_unit_ids.Add(ally.unit_id);
        state.enemy_unit_ids.Add(defeated.unit_id);
        state.enemy_unit_ids.Add(adjacentFail.unit_id);
        state.enemy_unit_ids.Add(adjacentLongSlow.unit_id);
        state.enemy_unit_ids.Add(nonAdjacent.unit_id);
        fixture.Runtime.SetupStateForTests(state);

        BattleEquipmentAbilityOnKillResult result =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
                new BattleEquipmentAbilityOnKillContext
                {
                    SourceUnit = killer,
                    DefeatedUnit = defeated,
                    BattleState = state,
                    SaveContext = BattleSaveContext.WithSaveRollOverrides(new[] { 1, 1 }),
                }
            );

        BattleStatusEffectState failedSlow = adjacentFail.GetStatusEffect(SlowStatusId);
        _test.True(
            failedSlow != null,
            $"击杀石化目标后，相邻敌人豁免失败应获得短部分石化。{DescribeStatusResults(result)}"
        );
        _test.Eq(
            failedSlow?.duration ?? -1,
            30,
            $"石像崩裂扩散 slow 应只持续 30 TU。{DescribeStatusResults(result)}"
        );
        _test.Eq(
            failedSlow?.display_label ?? "",
            "部分石化",
            $"扩散 slow 显示名应为部分石化。{DescribeStatusResults(result)}"
        );
        _test.Eq(
            adjacentLongSlow.GetStatusEffect(SlowStatusId)?.duration ?? -1,
            60,
            "扩散的 30 TU slow 不应缩短目标身上已有的更长 slow。"
        );
        _test.False(nonAdjacent.HasStatusEffect(SlowStatusId), "非相邻敌人不应被石像崩裂扩散影响。");
        _test.False(ally.HasStatusEffect(SlowStatusId), "相邻友军不应被石像崩裂扩散影响。");
        _test.True(
            CountStatusResults(result, SlowStatusId) >= 2,
            "扩散应对相邻敌人分别产生状态门控结果，方便回归豁免和应用状态。"
        );
    }

    private void TestStatueShatterKillSpreadHonorsConSaveSuccess()
    {
        using GorgonFixture fixture = GorgonFixture.Build(new GArray());
        BattleUnitState killer = fixture.BuildGorgonUnit("spread_save_success");
        killer.SetAnchorCoord(Vector2I.Zero);

        BattleUnitState defeated = BuildTarget("spread_success_defeated", new Vector2I(2, 2), hp: 0);
        defeated.is_alive = false;
        SetSimpleStatus(defeated, SlowStatusId, 60, killer.unit_id, "部分石化");

        BattleUnitState adjacentSuccess = BuildTarget("spread_adjacent_success", new Vector2I(3, 2), hp: 30);
        adjacentSuccess.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);

        BattleState state = BuildFlatState("gorgon_shatter_spread_success", new Vector2I(6, 6));
        foreach (BattleUnitState unit in new[] { killer, defeated, adjacentSuccess })
            state.SetUnit(unit);
        state.ally_unit_ids.Add(killer.unit_id);
        state.enemy_unit_ids.Add(defeated.unit_id);
        state.enemy_unit_ids.Add(adjacentSuccess.unit_id);
        fixture.Runtime.SetupStateForTests(state);

        BattleEquipmentAbilityOnKillResult result =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
                new BattleEquipmentAbilityOnKillContext
                {
                    SourceUnit = killer,
                    DefeatedUnit = defeated,
                    BattleState = state,
                    SaveContext = BattleSaveContext.WithSaveRollOverride(20),
                }
            );

        _test.False(
            adjacentSuccess.HasStatusEffect(SlowStatusId),
            $"相邻敌人 DC16 CON 豁免成功时不应获得 slow。{DescribeStatusResults(result)}"
        );
        BattleEquipmentAbilityStatusActionResult statusResult =
            FindStatusResult(result, SlowStatusId, adjacentSuccess.unit_id);
        _test.True(
            statusResult != null && !statusResult.Applied && statusResult.SaveResult.Success,
            $"成功豁免应记录未施加的状态结果。{DescribeStatusResults(result)}"
        );
    }

    private static BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        GorgonFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int saveRollOverride
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        return fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static BattleEquipmentAbilityStatusActionResult FindStatusResult(
        BattleEquipmentAbilityAfterHitResult result,
        StringName statusId
    )
    {
        foreach (BattleEquipmentAbilityStatusActionResult statusResult in result?.StatusResults ?? Array.Empty<BattleEquipmentAbilityStatusActionResult>())
        {
            if (statusResult?.StatusId == statusId)
                return statusResult;
        }
        return null;
    }

    private static BattleEquipmentAbilityStatusActionResult FindStatusResult(
        BattleEquipmentAbilityOnKillResult result,
        StringName statusId,
        StringName targetUnitId
    )
    {
        foreach (
            BattleEquipmentAbilityStatusActionResult statusResult in result?.StatusResults
                ?? Array.Empty<BattleEquipmentAbilityStatusActionResult>()
        )
        {
            if (statusResult?.StatusId == statusId && statusResult.TargetUnitId == targetUnitId)
                return statusResult;
        }
        return null;
    }

    private static int CountStatusResults(
        BattleEquipmentAbilityOnKillResult result,
        StringName statusId
    )
    {
        int count = 0;
        foreach (BattleEquipmentAbilityStatusActionResult statusResult in result?.StatusResults ?? Array.Empty<BattleEquipmentAbilityStatusActionResult>())
        {
            if (statusResult?.StatusId == statusId)
                count++;
        }
        return count;
    }

    private static string DescribeStatusResults(BattleEquipmentAbilityOnKillResult result)
    {
        var entries = new List<string>();
        foreach (
            BattleEquipmentAbilityStatusActionResult statusResult in result?.StatusResults
                ?? Array.Empty<BattleEquipmentAbilityStatusActionResult>()
        )
        {
            BattleSaveResult save = statusResult.SaveResult;
            entries.Add(
                $"{statusResult.TargetUnitId}:{statusResult.StatusId}"
                    + $" applied={statusResult.Applied}"
                    + $" roll={save.NaturalRoll}"
                    + $" total={save.RollTotal}"
                    + $" dc={save.Dc}"
                    + $" success={save.Success}"
            );
        }
        return $" status_results=[{string.Join(" | ", entries)}]";
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = hp > 0,
            current_hp = hp,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hp, 30));
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleUnitState BuildAlly(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = BuildTarget(unitId, coord, hp: 30);
        unit.faction_id = "player";
        unit.is_alive = true;
        return unit;
    }

    private static BattleState BuildFlatState(StringName battleId, Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
        }
        return state;
    }

    private static void SetSimpleStatus(
        BattleUnitState unit,
        StringName statusId,
        int durationTu,
        StringName sourceUnitId,
        string displayLabel
    )
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = statusId,
                source_unit_id = sourceUnitId,
                duration = durationTu,
                stacks = 1,
                power = 1,
                display_label = displayLabel,
                counts_as_debuff_override = true,
                counts_as_debuff = true,
            }
        );
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private sealed class GorgonFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private GorgonFixture(
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

        internal static GorgonFixture Build(GArray damageRolls)
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
            return new GorgonFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildGorgonUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                GorgonCrossbowItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(GorgonCrossbowItemId, $"eq_gorgon_{label}")
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

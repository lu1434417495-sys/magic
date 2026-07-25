using System.Collections.Generic;
using Godot;

// M2 temporal 状态族语义回归：time_stasis 冻结个人时间线、time_slow 余数累加、
// time_reverberation 释放规则与 typed 状态构造边界。
public partial class run_temporal_status_semantics_regression : LifecycleTestSceneTree
{
    private static readonly StringName TemporalTag =
        BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStasisFreezesPersonalTimeline();
        TestStasisFreezesCooldownProgress();
        TestReadyUnitActivationConsumesCooldownElapsedTu();
        TestTimeSlowAccumulatesProgressWithRemainder();
        TestActionClockConsumesEveryCrossingWithoutDuplicateReady();
        TestStasisBlocksMovementFailClosed();
        TestStasisUnitSkipsReadyQueueActivation();
        TestStatusConstructionImportsTemporalTypedFields();
        TestReleaseReasonsControlReverberation();
        RequestTestExit(_test.Finish("Temporal status semantics regression"));
    }

    private void TestStasisFreezesPersonalTimeline()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState stasisUnit = fixture.AddUnit("stasis_unit", "enemy", new Vector2I(1, 1));
        BattleUnitState controlUnit = fixture.AddUnit("control_unit", "enemy", new Vector2I(2, 2));
        foreach (BattleUnitState unit in new[] { stasisUnit, controlUnit })
        {
            unit.SetCurrentStamina(10);
            unit.SetStatusEffect(BuildBurningStatus());
            unit.SetStatusEffect(BuildTimedBuffStatus("attack_up", 30));
            unit.ReplaceShieldStateTyped(
                5,
                5,
                20,
                "temporal_ward",
                unit.unit_id,
                "temporal_ward_skill"
            );
        }
        ApplyTimeStasis(stasisUnit, 15);

        fixture.Step(5);

        _test.Eq(controlUnit.GetCurrentHp(), 98, "对照单位应照常吃到 burning tick。");
        _test.Eq(stasisUnit.GetCurrentHp(), 100, "静滞单位不应结算普通 DOT tick。");
        _test.Eq(
            controlUnit.GetActionProgressTyped(),
            5,
            "对照单位应推进 action progress。"
        );
        _test.Eq(
            stasisUnit.GetActionProgressTyped(),
            0,
            "静滞单位不应推进 action progress。"
        );
        _test.True(
            controlUnit.GetCurrentStamina() > 10,
            "对照单位应推进 stamina recovery。"
        );
        _test.Eq(stasisUnit.GetCurrentStamina(), 10, "静滞单位不应推进 stamina recovery。");
        _test.Eq(
            GetStatusDuration(controlUnit, "attack_up"),
            25,
            "对照单位普通状态 duration 应按时间减少。"
        );
        _test.Eq(
            GetStatusDuration(stasisUnit, "attack_up"),
            30,
            "静滞单位的其他状态 duration 不应减少。"
        );
        _test.Eq(
            GetStatusDuration(stasisUnit, "burning"),
            30,
            "静滞单位的 DOT duration 不应减少。"
        );
        _test.Eq(
            controlUnit.GetShieldStateTyped().Duration,
            15,
            "对照单位 shield duration 应按战场 TU 减少。"
        );
        _test.Eq(
            stasisUnit.GetShieldStateTyped().Duration,
            20,
            "静滞单位 shield duration 不应减少。"
        );
        _test.Eq(
            GetStatusDuration(stasisUnit, BattleStatusSemanticTable.STATUS_TIME_STASIS),
            10,
            "time_stasis 自身 duration 应按战场时间减少。"
        );

        fixture.Step(5);
        fixture.Step(5);

        _test.Eq(
            controlUnit.GetShieldStateTyped().Duration,
            5,
            "对照单位 shield duration 应持续随每个 timeline step 减少。"
        );
        _test.Eq(
            stasisUnit.GetShieldStateTyped().Duration,
            20,
            "time_stasis 在 step 内到期时，该 step 仍应冻结 shield duration。"
        );
        _test.False(
            stasisUnit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "time_stasis 到期后应自然消散。"
        );
        BattleStatusEffectState reverberation = stasisUnit.GetStatusEffect(
            BattleStatusSemanticTable.STATUS_TIME_REVERBERATION
        );
        _test.True(reverberation != null, "自然到期应添加 time_reverberation。");
        if (reverberation != null)
        {
            _test.True(reverberation.HasStatusTag(TemporalTag), "余波应携带 temporal status tag。");
            _test.Eq(
                reverberation.save_bonus_by_tag.GetValueOrDefault(TemporalTag, 0),
                BattleTemporalStatusService.ReverberationTemporalSaveBonus,
                "余波应携带 temporal per-tag save bonus。"
            );
        }
        _test.Eq(stasisUnit.GetCurrentHp(), 100, "静滞期间不应累积任何 DOT 伤害。");
        _test.Eq(
            stasisUnit.GetActionProgressTyped(),
            0,
            "静滞期间 action progress 应保持冻结。"
        );

        fixture.Step(5);

        _test.Eq(
            controlUnit.GetShieldStateTyped(),
            new BattleUnitShieldSnapshot(0, 0, -1, "", "", ""),
            "shield duration 到期时应原子清空 HP、duration、family 与 source metadata。"
        );
        _test.Eq(
            stasisUnit.GetShieldStateTyped().Duration,
            15,
            "time_stasis 解除后的下一 step 应恢复 shield duration 推进。"
        );
        _test.Eq(stasisUnit.GetCurrentHp(), 98, "静滞解除后 DOT tick 应恢复结算。");
        _test.Eq(
            stasisUnit.GetActionProgressTyped(),
            5,
            "静滞解除后 action progress 应恢复推进。"
        );
    }

    private void TestStasisFreezesCooldownProgress()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState unit = fixture.AddUnit("cooldown_unit", "enemy", new Vector2I(1, 1));
        unit.SetCooldownsTyped(new Dictionary<StringName, int> { ["skill_a"] = 30 });
        unit.SetCooldownAnchorTuTyped(0);

        fixture.Step(5);
        ApplyTimeStasis(unit, 10);
        fixture.Step(5);
        fixture.Step(5);

        _test.False(
            unit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "静滞应在 10 TU 后自然到期。"
        );
        _test.Eq(
            unit.GetCooldownsTyped().GetValueOrDefault(new StringName("skill_a"), 0),
            30,
            "静滞期间冷却不应被消费。"
        );
        _test.Eq(unit.GetCooldownAnchorTuTyped(), 10, "冻结 anchor 应只吸收静滞时段。");

        fixture.Runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(unit);
        _test.Eq(
            unit.GetCooldownsTyped().GetValueOrDefault(new StringName("skill_a"), 0),
            25,
            "静滞前已流逝的 5 TU 应在解除后被惰性消费，静滞时段不计入。"
        );
    }

    private void TestReadyUnitActivationConsumesCooldownElapsedTu()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState unit = fixture.AddUnit(
            "ready_cooldown_unit",
            "enemy",
            new Vector2I(1, 1)
        );
        unit.SetCooldownsTyped(new Dictionary<StringName, int> { ["skill_a"] = 30 });
        unit.SetCooldownAnchorTuTyped(0);
        fixture.State.timeline.current_tu = 10;
        fixture.State.timeline.ready_unit_ids.Add(unit.unit_id);

        fixture.Runtime._timeline_driver.ActivateNextReadyUnit(new BattleEventBatch());

        _test.Eq(
            fixture.State.active_unit_id,
            unit.unit_id,
            "ready unit 应通过真实时间轴激活路径进入行动窗口。"
        );
        _test.Eq(
            unit.GetCooldownsTyped().GetValueOrDefault(new StringName("skill_a"), 0),
            20,
            "激活行动窗口时应消费自上次回合锚点起流逝的 10 TU 冷却。"
        );
        _test.Eq(
            unit.GetCooldownAnchorTuTyped(),
            10,
            "真实激活路径应把回合锚点推进到当前 10 TU。"
        );
    }

    private void TestTimeSlowAccumulatesProgressWithRemainder()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState slowUnit = fixture.AddUnit("slow_unit", "enemy", new Vector2I(1, 1));
        BattleUnitState controlUnit = fixture.AddUnit("fast_unit", "enemy", new Vector2I(2, 2));
        ApplyTimeSlow(slowUnit, 600);

        fixture.Step(5);
        _test.Eq(
            slowUnit.GetActionProgressTyped(),
            2,
            "50% 减速下单个 5 TU tick 应只得 2 进度。"
        );
        _test.Eq(
            slowUnit.GetActionProgressRateRemainderTyped(),
            50,
            "50% 减速下单个 5 TU tick 应携带 50 余数。"
        );

        for (int step = 0; step < 9; step++)
        {
            fixture.Step(5);
        }

        _test.Eq(
            slowUnit.GetActionProgressTyped(),
            25,
            "10 个 5 TU tick 在 50% 减速下总进度应为 25，而不是逐 tick 截断。"
        );
        _test.Eq(
            slowUnit.GetActionProgressRateRemainderTyped(),
            0,
            "整除节点余数应归零。"
        );
        _test.Eq(
            controlUnit.GetActionProgressTyped(),
            50,
            "全速单位 10 个 tick 应得 50 进度。"
        );
    }

    private void TestActionClockConsumesEveryCrossingWithoutDuplicateReady()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState unit = fixture.AddUnit(
            "multi_crossing_unit",
            "enemy",
            new Vector2I(1, 1)
        );
        unit.SetActionThresholdTyped(100);
        unit.SetActionProgressTyped(250);
        fixture.State.timeline.ready_unit_ids.Add(unit.unit_id);

        fixture.Runtime._timeline_driver.CollectTimelineReadyUnits(
            new BattleEventBatch(),
            5
        );

        _test.Eq(
            unit.GetActionProgressTyped(),
            55,
            "已有 ready id 仍应扣除本 tick 跨越的全部 action thresholds。"
        );
        _test.Eq(
            fixture.State.timeline.ready_unit_ids.Count,
            1,
            "同一单位跨越多个 action thresholds 时 ready queue 不应重复插入。"
        );
    }

    private void TestStasisBlocksMovementFailClosed()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState stasisUnit = fixture.AddUnit("stasis_mover", "enemy", new Vector2I(1, 1));
        BattleUnitState sourceUnit = fixture.AddUnit("pusher", "player", new Vector2I(1, 2));
        ApplyTimeStasis(stasisUnit, 30);

        _test.True(
            fixture.Runtime._skill_turn_resolver.IsMovementBlocked(stasisUnit),
            "静滞单位应被主动移动校验拦截。"
        );
        _test.False(
            fixture.Runtime._grid_service.MoveUnit(
                fixture.State,
                stasisUnit,
                new Vector2I(3, 3)
            ),
            "静滞单位经 BattleGridService.MoveUnit 的位移应 fail closed。"
        );
        _test.Eq(stasisUnit.GetAnchorCoord(), new Vector2I(1, 1), "静滞单位坐标不应变化。");

        CombatEffectDefinition knockback = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "knockback",
            forcedMoveDistance: 2
        );
        int movedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            sourceUnit,
            stasisUnit,
            knockback,
            new BattleEventBatch(),
            default
        );
        _test.Eq(movedSteps, 0, "静滞单位的 forced movement 应 fail closed。");
        _test.Eq(stasisUnit.GetAnchorCoord(), new Vector2I(1, 1), "forced movement 后静滞单位坐标不应变化。");
    }

    private void TestStasisUnitSkipsReadyQueueActivation()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState stasisUnit = fixture.AddUnit("stasis_ready", "enemy", new Vector2I(1, 1));
        ApplyTimeStasis(stasisUnit, 30);
        stasisUnit.SetActionProgressTyped(0);
        fixture.State.timeline.ready_unit_ids.Add(stasisUnit.unit_id);

        fixture.Runtime._timeline_driver.ActivateNextReadyUnit(new BattleEventBatch());

        _test.Eq(
            fixture.State.active_unit_id,
            new StringName(""),
            "静滞单位不应被激活进入行动窗口。"
        );
        _test.Eq(
            stasisUnit.GetCurrentAp(),
            2,
            "静滞单位不应触发 turn start AP 重置。"
        );

        // ready 收集路径同样应跳过静滞单位。
        stasisUnit.SetActionProgressTyped(115);
        fixture.Step(5);
        _test.False(
            fixture.State.timeline.ready_unit_ids.Contains(stasisUnit.unit_id),
            "静滞单位不应加入 ready 队列。"
        );
        _test.Eq(
            stasisUnit.GetActionProgressTyped(),
            115,
            "静滞单位的 action progress 应保持冻结。"
        );
    }

    private void TestStatusConstructionImportsTemporalTypedFields()
    {
        CombatEffectDefinition effectDef = TestSkillDefinitionProjection.BuildEffect(
            "status",
            statusId: BattleStatusSemanticTable.STATUS_TIME_REVERBERATION,
            power: 1,
            durationTu: 60,
            effectTags: new[] { TemporalTag },
            parameters: new Dictionary<string, object>
            {
                ["save_bonus_by_tag"] = new Dictionary<string, object>
                {
                    [TemporalTag.ToString()] = 4,
                },
            }
        );
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            effectDef,
            "source_unit"
        );
        _test.True(statusEntry != null, "temporal 状态构造应成功。");
        if (statusEntry == null)
        {
            return;
        }
        _test.True(
            statusEntry.HasStatusTag(TemporalTag),
            "effect_tags 应在状态构造边界投成 typed status_tags。"
        );
        _test.Eq(
            statusEntry.save_bonus_by_tag.GetValueOrDefault(TemporalTag, 0),
            4,
            "params.save_bonus_by_tag 应在状态构造边界投成 typed save_bonus_by_tag。"
        );
        _test.False(
            statusEntry.ParamsSnapshotPlain.ContainsKey("save_bonus_by_tag"),
            "save_bonus_by_tag formal key 不应滞留在 owner 内部 @params。"
        );

        // Restricted content graphs canonicalize nested dictionary keys to strings.
        CombatEffectDefinition stringKeyDef = TestSkillDefinitionProjection.BuildEffect(
            "status",
            statusId: "string_key_probe",
            power: 1,
            durationTu: 60,
            parameters: new Dictionary<string, object>
            {
                ["save_bonus_by_tag"] = new Dictionary<string, object>
                {
                    ["temporal"] = 4,
                },
            }
        );
        BattleStatusEffectState stringKeyEntry = BattleStatusSemanticTable.MergeStatus(
            stringKeyDef,
            "source_unit"
        );
        _test.True(stringKeyEntry != null, "string-key 探针状态构造应成功。");
        _test.Eq(
            stringKeyEntry?.save_bonus_by_tag.GetValueOrDefault(TemporalTag, 0) ?? -1,
            4,
            "save_bonus_by_tag 的 canonical string key 应恢复进 typed map。"
        );
    }

    private void TestReleaseReasonsControlReverberation()
    {
        foreach (
            TemporalStatusReleaseKind blockedKind in new[]
            {
                TemporalStatusReleaseKind.Death,
                TemporalStatusReleaseKind.Cleanup,
                TemporalStatusReleaseKind.BattleEnd,
                TemporalStatusReleaseKind.LeaveBattle,
            }
        )
        {
            BattleUnitState unit = BuildBareUnit($"release_{blockedKind}");
            bool added = BattleTemporalStatusService.HandleTemporalStatusRemoved(
                unit,
                BattleStatusSemanticTable.STATUS_TIME_STASIS,
                blockedKind
            );
            _test.False(added, $"{blockedKind} 不应添加 time_reverberation。");
            _test.False(
                unit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
                $"{blockedKind} 后单位不应带 time_reverberation。"
            );
        }

        foreach (
            TemporalStatusReleaseKind allowedKind in new[]
            {
                TemporalStatusReleaseKind.NaturalExpire,
                TemporalStatusReleaseKind.Dispel,
            }
        )
        {
            BattleUnitState unit = BuildBareUnit($"release_{allowedKind}");
            bool added = BattleTemporalStatusService.HandleTemporalStatusRemoved(
                unit,
                BattleStatusSemanticTable.STATUS_TIME_STASIS,
                allowedKind
            );
            _test.True(added, $"{allowedKind} 应添加 time_reverberation。");
            _test.True(
                unit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
                $"{allowedKind} 后单位应带 time_reverberation。"
            );
        }

        BattleUnitState deadUnit = BuildBareUnit("release_dead");
        deadUnit.MarkDead();
        _test.False(
            BattleTemporalStatusService.HandleTemporalStatusRemoved(
                deadUnit,
                BattleStatusSemanticTable.STATUS_TIME_STASIS,
                TemporalStatusReleaseKind.NaturalExpire
            ),
            "已倒下单位不应获得 time_reverberation。"
        );

        BattleUnitState slowUnit = BuildBareUnit("release_slow");
        _test.False(
            BattleTemporalStatusService.HandleTemporalStatusRemoved(
                slowUnit,
                BattleStatusSemanticTable.STATUS_TIME_SLOW,
                TemporalStatusReleaseKind.NaturalExpire
            ),
            "time_slow 消散不应添加 time_reverberation。"
        );
    }

    private static void ApplyTimeStasis(BattleUnitState unit, int durationTu)
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
                source_unit_id = "temporal_caster",
                power = 1,
                stacks = 1,
                duration = durationTu,
                status_tags = new List<StringName> { TemporalTag },
            }
        );
    }

    private static void ApplyTimeSlow(BattleUnitState unit, int durationTu)
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_SLOW,
                source_unit_id = "temporal_caster",
                power = 1,
                stacks = 1,
                duration = durationTu,
                status_tags = new List<StringName> { TemporalTag },
            }
        );
    }

    private static BattleStatusEffectState BuildBurningStatus()
    {
        return new BattleStatusEffectState
        {
            status_id = "burning",
            source_unit_id = "burn_source",
            power = 2,
            stacks = 1,
            duration = 30,
            tick_interval_tu = 5,
            next_tick_at_tu = 5,
        };
    }

    private static BattleStatusEffectState BuildTimedBuffStatus(StringName statusId, int durationTu)
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = "buff_source",
            power = 1,
            stacks = 1,
            duration = durationTu,
        };
    }

    private static int GetStatusDuration(BattleUnitState unit, StringName statusId)
    {
        return unit.GetStatusEffect(statusId)?.duration ?? -1;
    }

    private static Fixture BuildFixture()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(6, 6));
        runtime.SetupStateForTests(state);
        return new Fixture(runtime, state);
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "temporal_status_semantics",
            phase = "timeline_running",
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildBareUnit(StringName unitId)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 100,
            isAlive: true
        );
        return unit;
    }

    private readonly record struct Fixture(BattleRuntimeModule Runtime, BattleState State)
    {
        internal BattleUnitState AddUnit(StringName unitId, StringName factionId, Vector2I coord)
        {
            BattleUnitState unit = new BattleUnitState()
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
                control_mode = "manual",
            }.WithCombatResourcesForTest(
                hp: 100,
                mp: 20,
                stamina: 20,
                ap: 2,
                movePoints: 4,
                isAlive: true
            );
            unit.SetAnchorCoord(coord);
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.HpMax),
                100
            );
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 20);
            unit.attribute_snapshot.SetValue("constitution", 10);
            unit.attribute_snapshot.SetValue("willpower", 10);
            unit.attribute_snapshot.SetValue("constitution_modifier", 0);
            unit.attribute_snapshot.SetValue("willpower_modifier", 0);
            State.SetUnit(unit);
            if (factionId == new StringName("player"))
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true);
            return unit;
        }

        internal void Step(int tuDelta)
        {
            Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), tuDelta);
        }
    }
}

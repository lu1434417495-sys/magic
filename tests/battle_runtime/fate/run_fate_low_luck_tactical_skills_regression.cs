using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_fate_low_luck_tactical_skills_regression : SceneTree
{
    private static readonly StringName HERO_ID = "hero";
    private static readonly StringName MISSTEP_TO_SCHEME_SKILL_ID = "misstep_to_scheme";
    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName DOOM_SHIFT_SKILL_ID = "doom_shift";
    private static readonly StringName BLACK_CROWN_SEAL_SKILL_ID = "black_crown_seal";
    private static readonly StringName BLOOD_TITHE_VARIANT_ID = "blood_tithe";
    private static readonly StringName GUARD_TITHE_VARIANT_ID = "guard_tithe";
    private static readonly StringName ACTION_TITHE_VARIANT_ID = "action_tithe";
    private static readonly StringName COUNTERATTACK_LOCK_VARIANT_ID = "counterattack_lock";
    private static readonly StringName CRIT_LOCK_VARIANT_ID = "crit_lock";
    private static readonly StringName STATUS_GUARDING = "guarding";
    private static readonly StringName STATUS_STAGGERED = "staggered";
    private static readonly StringName STATUS_MARKED = "marked";
    private static readonly StringName STATUS_BLACK_CROWN_SEAL_COUNTERATTACK = "black_crown_seal_counterattack";
    private static readonly StringName STATUS_BLACK_CROWN_SEAL_CRIT = "black_crown_seal_crit";
    private static readonly StringName FORTUNE_MARK_TARGET_STAT_ID = "fortune_mark_target";
    private static readonly StringName BOSS_TARGET_STAT_ID = "boss_target";
    private const int BLACK_CONTRACT_PUSH_HP_COST = 10;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMisstepToSchemeGrantsBonusCalamityWithoutDuplicateCriticalFailEvents();
        TestBlackContractPushOptionsPayTheirSelectedCostAndForceHitWithoutCrit();
        TestDoomShiftMarksSelfAndSwapsWithNearbyAlly();
        TestBlackCrownSealIsBossOnlyOncePerBattleAndAppliesBothLockOptions();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("FATE_25 regression"));
    }

    private void TestMisstepToSchemeGrantsBonusCalamityWithoutDuplicateCriticalFailEvents()
    {
        BattleRuntimeModule runtime = null;
        BattleState state = null;
        BattleUnitState hero = null;
        LowLuckContext lowLuckContext = null;
        BattleFateEventBus fateEventBus = null;
        BattleFateEventBus.EventDispatchedEventHandler eventCallback = null;
        try
        {
            runtime = BuildRuntime();
            state = BuildSkillTestState("fate_25_misstep", new Vector2I(5, 4));
            hero = BuildUnit("misstep_hero", "倒霉先锋", "player", new Vector2I(1, 1), 1, HERO_ID);
            hero.known_skill_level_map[MISSTEP_TO_SCHEME_SKILL_ID] = 1;
            AddUnit(runtime, state, hero);
            state.ally_unit_ids = new GStringNameArray { hero.unit_id };
            state.active_unit_id = hero.unit_id;
            runtime.SetupStateForTests(state);
            BeginRuntimeBattle(runtime);

            lowLuckContext = BuildLowLuckContext(-5);
            LowLuckEventService lowLuckService = lowLuckContext.Service;
            if (lowLuckService == null)
            {
                _test.True(false, "失手成筹前置失败：LowLuckEventService 未初始化。");
                return;
            }

            fateEventBus = runtime.GetFateEventBus();
            var seenEvents = new GStringNameArray();
            eventCallback = (eventType, _payload) =>
            {
                seenEvents.Add(eventType);
            };
            fateEventBus.EventDispatched += eventCallback;
            GDictionary criticalFailPayload = BuildCriticalFailPayload(state.battle_id, HERO_ID, hero.unit_id, -5);
            fateEventBus.dispatch("critical_fail", criticalFailPayload);
            lowLuckService.HandleFateEvent(
                "critical_fail",
                BuildLowLuckCriticalFailPayload(state.battle_id, HERO_ID, -5)
            );

            _test.Eq(runtime.GetMemberCalamity(HERO_ID), 2, "失手成筹应让首次大失败额外获得 1 点 calamity。");
            _test.Eq(SeenEventCount(seenEvents, "critical_fail"), 1, "失手成筹不应额外重复派发 critical_fail 事件。");

            LowLuckEventResult lowLuckResult = lowLuckService.HandleBattleResolution(
                BuildLowLuckBattleResolutionInput(state, BuildBattleResolutionResult(state.battle_id))
            );
            _test.True(
                ContainsStringName(lowLuckResult.TriggeredEventIds, "borrowed_road"),
                "失手成筹不应冲掉 Borrowed Road 的大失败计数。"
            );
        }
        finally
        {
            if (fateEventBus != null && eventCallback != null)
                fateEventBus.EventDispatched -= eventCallback;
            lowLuckContext?.Dispose();
            BattleTestFixture.DisposeBattleFixture(runtime, state, hero);
        }
    }

    private void TestBlackContractPushOptionsPayTheirSelectedCostAndForceHitWithoutCrit()
    {
        var bloodCase = IssueBlackContractPushCase(BLOOD_TITHE_VARIANT_ID);
        AssertForceHitPreview(
            (BattlePreview)bloodCase["preview"],
            "黑契推进·血契 preview 应按必定命中暴露给指令与 AI 评分。"
        );
        AssertForcedHitNoCrit(
            bloodCase.GetValueOrDefault("simulated_result", new GDictionary()).AsGodotDictionary(),
            "黑契推进·血契应改为必定命中且不会暴击。"
        );
        _test.True(
            ((BattleUnitState)bloodCase["enemy"]).current_hp < 60,
            "黑契推进·血契命中后应对目标造成伤害。"
        );
        _test.Eq(
            ((BattleUnitState)bloodCase["caster"]).current_hp,
            28 - BLACK_CONTRACT_PUSH_HP_COST,
            "黑契推进·血契应先扣除固定生命代价。"
        );
        DisposeBlackContractCase(bloodCase);

        var guardCase = IssueBlackContractPushCase(GUARD_TITHE_VARIANT_ID);
        AssertForceHitPreview(
            (BattlePreview)guardCase["preview"],
            "黑契推进·护契 preview 应按必定命中暴露给指令与 AI 评分。"
        );
        AssertForcedHitNoCrit(
            guardCase.GetValueOrDefault("simulated_result", new GDictionary()).AsGodotDictionary(),
            "黑契推进·护契应改为必定命中且不会暴击。"
        );
        _test.True(
            !((BattleUnitState)guardCase["caster"]).HasStatusEffect(STATUS_GUARDING),
            "黑契推进·护契成功后应移除施法者的 Guard。"
        );
        _test.True(
            ((BattleUnitState)guardCase["enemy"]).current_hp < 60,
            "黑契推进·护契命中后应对目标造成伤害。"
        );
        DisposeBlackContractCase(guardCase);

        var actionCase = IssueBlackContractPushCase(ACTION_TITHE_VARIANT_ID);
        AssertForceHitPreview(
            (BattlePreview)actionCase["preview"],
            "黑契推进·行契 preview 应按必定命中暴露给指令与 AI 评分。"
        );
        BattleRuntimeModule actionRuntime = (BattleRuntimeModule)actionCase["runtime"];
        BattleUnitState actionCaster = (BattleUnitState)actionCase["caster"];
        AssertForcedHitNoCrit(
            actionCase.GetValueOrDefault("simulated_result", new GDictionary()).AsGodotDictionary(),
            "黑契推进·行契应改为必定命中且不会暴击。"
        );
        _test.True(
            actionCaster.HasStatusEffect(STATUS_STAGGERED),
            "黑契推进·行契成功后应为自己挂上 staggered。"
        );
        actionCaster.current_ap = 2;
        BattleEventBatch turnStartBatch = new BattleEventBatch();
        actionRuntime._apply_turn_start_statuses_result(actionCaster, turnStartBatch);
        _test.Eq(actionCaster.current_ap, 1, "黑契推进·行契应让施法者下一回合少 1 点行动点。");
        _test.True(
            ((BattleUnitState)actionCase["enemy"]).current_hp < 60,
            "黑契推进·行契命中后应对目标造成伤害。"
        );
        GodotSharpCleanup.DisposeGodotObject(turnStartBatch);
        DisposeBlackContractCase(actionCase);
    }

    private void TestDoomShiftMarksSelfAndSwapsWithNearbyAlly()
    {
        BattleRuntimeModule runtime = null;
        BattleState state = null;
        BattleUnitState caster = null;
        BattleUnitState ally = null;
        BattleCommand illegalCommand = null;
        BattlePreview illegalPreview = null;
        BattleCommand issueCommand = null;
        BattleEventBatch issueBatch = null;
        try
        {
            runtime = BuildRuntime();
            state = BuildSkillTestState("fate_25_doom_shift", new Vector2I(6, 4));
            caster = BuildUnit("doom_shift_caster", "断命者", "player", new Vector2I(1, 1), 1, HERO_ID);
            caster.known_active_skill_ids = new GStringNameArray { DOOM_SHIFT_SKILL_ID };
            caster.known_skill_level_map[DOOM_SHIFT_SKILL_ID] = 1;
            ally = BuildUnit("doom_shift_ally", "护卫", "player", new Vector2I(3, 1), 1, "ally");
            AddUnit(runtime, state, caster);
            AddUnit(runtime, state, ally);
            state.ally_unit_ids = new GStringNameArray { caster.unit_id, ally.unit_id };
            state.active_unit_id = caster.unit_id;
            runtime.SetupStateForTests(state);
            BeginRuntimeBattle(runtime);

            illegalCommand = BuildUnitSkillCommand(caster.unit_id, DOOM_SHIFT_SKILL_ID, caster);
            illegalPreview = runtime.PreviewCommand(illegalCommand);
            _test.True(
                illegalPreview != null && !illegalPreview.allowed,
                "断命换位不应允许以自己为目标。"
            );

            Vector2I originCoord = caster.coord;
            Vector2I allyCoord = ally.coord;
            issueCommand = BuildUnitSkillCommand(caster.unit_id, DOOM_SHIFT_SKILL_ID, ally);
            issueBatch = runtime.IssueCommand(issueCommand);
            _test.True(caster.HasStatusEffect(STATUS_MARKED), "断命换位成功后应给施法者写入 marked。");
            _test.Eq(caster.coord, allyCoord, "断命换位应把施法者送到队友原位置。");
            _test.Eq(ally.coord, originCoord, "断命换位应把队友换到施法者原位置。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state, illegalCommand, illegalPreview, issueCommand, issueBatch, caster, ally);
        }
    }

    private void TestBlackCrownSealIsBossOnlyOncePerBattleAndAppliesBothLockOptions()
    {
        var counterCase = BuildBlackCrownSealCase("fate_25_black_crown_counter");
        BattleRuntimeModule counterRuntime = counterCase.Runtime;
        BattleUnitState counterCaster = counterCase.Caster;
        BattleUnitState boss = counterCase.Boss;
        BattleUnitState elite = counterCase.Elite;
        SkillDef skillDef = GetSkill(counterRuntime.GetSkillDefIndexTyped(), BLACK_CROWN_SEAL_SKILL_ID);

        BattleCommand illegalCommand = BuildUnitSkillCommand(counterCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, elite, COUNTERATTACK_LOCK_VARIANT_ID);
        BattlePreview illegalPreview = counterRuntime.PreviewCommand(illegalCommand);
        _test.True(
            illegalPreview != null && !illegalPreview.allowed,
            "黑冠封印应拒绝非 boss 的 elite 目标。"
        );

        BattleCommand counterCommand = BuildUnitSkillCommand(counterCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, boss, COUNTERATTACK_LOCK_VARIANT_ID);
        BattleEventBatch counterBatch = counterRuntime.IssueCommand(counterCommand);
        _test.True(
            boss.HasStatusEffect(STATUS_BLACK_CROWN_SEAL_COUNTERATTACK),
            "黑冠封印·禁反击成功后应写入对应状态。"
        );
        _test.True(counterRuntime.IsUnitCounterattackLocked(boss), "黑冠封印·禁反击应封锁 boss 的反击。");
        counterCaster.current_ap = 1;
        _test.Eq(
            BattleSkillCastBlockReasonKinds.IsBlocked(counterRuntime.GetSkillCastBlockReason(counterCaster, skillDef)),
            true,
            "黑冠封印成功后应立刻进入每战 1 次的封锁状态并提供反馈。"
        );
        DisposeBlackCrownSealCase(counterCase, illegalCommand, illegalPreview, counterCommand, counterBatch);

        var critCase = BuildBlackCrownSealCase("fate_25_black_crown_crit");
        BattleRuntimeModule critRuntime = critCase.Runtime;
        BattleUnitState critCaster = critCase.Caster;
        BattleUnitState critBoss = critCase.Boss;
        BattleCommand critCommand = BuildUnitSkillCommand(critCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, critBoss, CRIT_LOCK_VARIANT_ID);
        BattleEventBatch critBatch = critRuntime.IssueCommand(critCommand);
        _test.True(
            critBoss.HasStatusEffect(STATUS_BLACK_CROWN_SEAL_CRIT),
            "黑冠封印·禁暴击成功后应写入对应状态。"
        );
        DisposeBlackCrownSealCase(critCase, critCommand, critBatch);
    }

    private GDictionary IssueBlackContractPushCase(StringName variantId)
    {
        BattleRuntimeModule runtime = BuildRuntime();
        SkillDef skillDef = GetSkill(runtime.GetSkillDefIndexTyped(), BLACK_CONTRACT_PUSH_SKILL_ID);
        CombatCastVariantDef castVariant = skillDef?.combat_profile?.GetCastVariant(variantId);
        BattleState state = BuildSkillTestState($"black_contract_{variantId}", new Vector2I(6, 4));
        BattleUnitState caster = BuildUnit("contract_caster", "契约战士", "player", new Vector2I(1, 1), 1, HERO_ID);
        caster.current_hp = 28;
        caster.known_active_skill_ids = new GStringNameArray { BLACK_CONTRACT_PUSH_SKILL_ID };
        caster.known_skill_level_map[BLACK_CONTRACT_PUSH_SKILL_ID] = 1;
        if (variantId == GUARD_TITHE_VARIANT_ID)
            SetStatus(caster, STATUS_GUARDING, 60);
        BattleUnitState enemy = BuildUnit("contract_target", "高闪避敌人", "enemy", new Vector2I(2, 1), 1);
        enemy.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 999);
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, enemy);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        BeginRuntimeBattle(runtime);

        BattleCommand previewCommand = BuildUnitSkillCommand(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variantId);
        BattlePreview preview = runtime.PreviewCommand(previewCommand);
        _test.True(preview != null && preview.allowed, $"黑契推进 {variantId} 前置：目标应可预览。");

        var simulatedTypedResult = runtime._skill_orchestrator.ResolveUnitSkillEffectResult(
            caster,
            enemy,
            skillDef,
            runtime._skill_resolution_rules.CollectUnitSkillEffectDefs(skillDef, castVariant)
        );
        var simulatedResult = AttackEffectResolutionResultReader.BuildGodotPayload(
            simulatedTypedResult.Result
        );
        if (simulatedTypedResult.CustomLogLines.Count != 0)
        {
            var customLogLines = new GArray();
            foreach (string line in simulatedTypedResult.CustomLogLines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    customLogLines.Add(line);
                }
            }
            if (customLogLines.Count != 0)
            {
                simulatedResult["custom_log_lines"] = customLogLines;
            }
        }
        BattleCommand issueCommand = BuildUnitSkillCommand(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variantId);
        BattleEventBatch batch = runtime.IssueCommand(issueCommand);
        return new GDictionary
        {
            ["runtime"] = runtime,
            ["state"] = state,
            ["caster"] = caster,
            ["enemy"] = enemy,
            ["batch"] = batch,
            ["preview"] = preview,
            ["preview_command"] = previewCommand,
            ["issue_command"] = issueCommand,
            ["simulated_result"] = simulatedResult,
        };
    }

    private BlackCrownSealCase BuildBlackCrownSealCase(StringName battleId)
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState(battleId, new Vector2I(7, 4));
        BattleUnitState caster = BuildUnit("seal_caster", "黑冕使徒", "player", new Vector2I(1, 1), 1, HERO_ID);
        caster.known_active_skill_ids = new GStringNameArray { BLACK_CROWN_SEAL_SKILL_ID };
        caster.known_skill_level_map[BLACK_CROWN_SEAL_SKILL_ID] = 1;
        BattleUnitState boss = BuildUnit("seal_boss", "章末 Boss", "enemy", new Vector2I(2, 1), 1, "", false, true);
        BattleUnitState elite = BuildUnit("seal_elite", "精英敌人", "enemy", new Vector2I(3, 1), 1, "", true, false);
        BattleUnitState allyTarget = BuildUnit("seal_ally", "受击队友", "player", new Vector2I(1, 2), 1, "ally");
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, boss);
        AddUnit(runtime, state, elite);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { boss.unit_id, elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        BeginRuntimeBattle(runtime);
        return new BlackCrownSealCase
        {
            Runtime = runtime,
            State = state,
            Caster = caster,
            Boss = boss,
            Elite = elite,
            AllyTarget = allyTarget,
        };
    }

    private void AssertForcedHitNoCrit(GDictionary result, string message)
    {
        _test.True(
            result.GetValueOrDefault("attack_success", false).AsBool()
                && result.GetValueOrDefault("crit_locked", false).AsBool()
                && !result.GetValueOrDefault("critical_hit", false).AsBool(),
            $"{message} result={result}"
        );
    }

    private void AssertForceHitPreview(BattlePreview preview, string message)
    {
        AttackPreviewData hitPreview = preview?.hit_preview;
        _test.True(hitPreview != null && !hitPreview.IsEmpty, $"{message} preview={preview}");
        _test.Eq(hitPreview.HitRatePercent, 100, $"{message} hit_rate_percent 应为 100。");
        _test.Eq(hitPreview.SuccessRatePercent, 100, $"{message} success_rate_percent 应为 100。");
        _test.True(
            hitPreview.StageSuccessRates.Count == 1 && hitPreview.StageSuccessRates[0] == 100,
            $"{message} stage_success_rates 应为 [100]。"
        );
        _test.True(hitPreview.ForceHitNoCrit, $"{message} 应标记 force_hit_no_crit。");
        _test.True(hitPreview.CritLocked, $"{message} 应标记 crit_locked。");
    }

    private BattleRuntimeModule BuildRuntime()
    {
        var registry = new ProgressionContentRegistry();
        var runtime = new BattleRuntimeModule();
        try
        {
            runtime.setup(
                null,
                registry.GetSkillDefsTyped(),
                new Dictionary<StringName, EnemyTemplateDef>(),
                new Dictionary<StringName, EnemyAiBrainDef>()
            );
            BattleTestFixture.ConfigureDamageResolverForTests(runtime, new DeterministicBattleDamageResolver());
            BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
        }
        finally
        {
            GodotSharpCleanup.DisposeGodotObject(registry);
        }
        return runtime;
    }

    private static void DisposeBlackContractCase(GDictionary testCase)
    {
        if (testCase == null)
            return;
        BattleTestFixture.DisposeBattleFixture(
            testCase.GetValueOrDefault("runtime", default).AsGodotObject() as BattleRuntimeModule,
            testCase.GetValueOrDefault("state", default).AsGodotObject() as BattleState,
            testCase.GetValueOrDefault("preview_command", default).AsGodotObject(),
            testCase.GetValueOrDefault("issue_command", default).AsGodotObject(),
            testCase.GetValueOrDefault("preview", default).AsGodotObject(),
            testCase.GetValueOrDefault("batch", default).AsGodotObject(),
            testCase.GetValueOrDefault("caster", default).AsGodotObject(),
            testCase.GetValueOrDefault("enemy", default).AsGodotObject()
        );
    }

    private static void DisposeBlackCrownSealCase(
        BlackCrownSealCase testCase,
        params GodotObject[] ownedObjects
    )
    {
        if (testCase == null)
            return;
        var owned = new List<GodotObject>();
        if (ownedObjects != null)
            owned.AddRange(ownedObjects);
        owned.Add(testCase.Caster);
        owned.Add(testCase.Boss);
        owned.Add(testCase.Elite);
        owned.Add(testCase.AllyTarget);
        BattleTestFixture.DisposeBattleFixture(testCase.Runtime, testCase.State, owned.ToArray());
    }

    private void BeginRuntimeBattle(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.calamity_by_member_id.Clear();
        runtime.GetFateRuntime().BeginBattle(runtime.calamity_by_member_id);
    }

    private BattleState BuildSkillTestState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState();
        state.battle_id = battleId;
        state.phase = "unit_acting";
        state.map_size = mapSize;
        state.timeline = new BattleTimelineState();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, BuildCell(coord));
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land);
        cell.base_height = 4;
        cell.height_offset = 0;
        cell.RecalculateRuntimeValues();
        return cell;
    }

    private BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int currentAp,
        StringName sourceMemberId = default,
        bool isElite = false,
        bool isBoss = false
    )
    {
        var unit = new BattleUnitState();
        unit.unit_id = unitId;
        unit.source_member_id = sourceMemberId;
        unit.display_name = displayName;
        unit.faction_id = factionId;
        unit.control_mode = "manual";
        unit.current_ap = currentAp;
        unit.current_hp = 60;
        unit.current_mp = 120;
        unit.current_stamina = 4;
        unit.current_aura = 0;
        unit.is_alive = true;
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 4);
        unit.attribute_snapshot.SetValue("action_points", Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 6);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.SetValue("faith_luck_bonus", 0);
        unit.attribute_snapshot.SetValue(FORTUNE_MARK_TARGET_STAT_ID, isBoss ? 2 : (isElite ? 1 : 0));
        unit.attribute_snapshot.SetValue(BOSS_TARGET_STAT_ID, isBoss ? 1 : 0);
        return unit;
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        int durationTu,
        StringName sourceUnitId = default,
        int power = 1,
        GDictionary @params = null
    )
    {
        if (unitState == null || statusId == default)
            return;
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = sourceUnitId;
        statusEntry.power = Mathf.Max(power, 1);
        statusEntry.stacks = 1;
        statusEntry.duration = durationTu;
        statusEntry.@params = @params != null ? (GDictionary)@params.Duplicate(true) : new GDictionary();
        unitState.SetStatusEffect(statusEntry);
    }

    private BattleCommand BuildUnitSkillCommand(
        StringName unitId,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName variantId = default
    )
    {
        var command = new BattleCommand();
        command.command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill);
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.skill_variant_id = variantId;
        command.target_unit_id = targetUnit?.unit_id ?? default;
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private LowLuckContext BuildLowLuckContext(int hiddenLuckAtBirth)
    {
        var partyState = new PartyState();
        partyState.leader_member_id = HERO_ID;
        partyState.main_character_member_id = HERO_ID;
        partyState.active_member_ids = new GStringNameArray { HERO_ID };
        partyState.SetMemberState(BuildMemberState(hiddenLuckAtBirth));
        var manager = new CharacterManagementModule();
        manager.setup(partyState, new GDictionary(), new GDictionary(), new GDictionary());
        var service = new LowLuckEventService();
        service.Setup(manager);
        return new LowLuckContext
        {
            PartyState = partyState,
            Manager = manager,
            Service = service,
        };
    }

    private PartyMemberState BuildMemberState(int hiddenLuckAtBirth)
    {
        var memberState = new PartyMemberState();
        memberState.member_id = HERO_ID;
        memberState.display_name = "Hero";
        memberState.progression.unit_id = HERO_ID;
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 12;
        memberState.progression.unit_base_attributes.SetAttributeValue("hidden_luck_at_birth", hiddenLuckAtBirth);
        return memberState;
    }

    private GDictionary BuildCriticalFailPayload(StringName battleId, StringName memberId, StringName attackerId, int hiddenLuckAtBirth)
    {
        return new GDictionary
        {
            ["battle_id"] = battleId,
            ["attacker_id"] = attackerId,
            ["attacker_member_id"] = memberId,
            ["luck_snapshot"] = new GDictionary
            {
                ["hidden_luck_at_birth"] = hiddenLuckAtBirth,
            },
        };
    }

    private LowLuckFateEventPayload BuildLowLuckCriticalFailPayload(
        StringName battleId,
        StringName memberId,
        int hiddenLuckAtBirth
    )
    {
        return new LowLuckFateEventPayload(
            battleId,
            memberId,
            false,
            null,
            hiddenLuckAtBirth
        );
    }

    private BattleResolutionResult BuildBattleResolutionResult(StringName battleId)
    {
        var result = new BattleResolutionResult();
        result.battle_id = battleId;
        result.winner_faction_id = "player";
        return result;
    }

    private LowLuckBattleResolutionInput BuildLowLuckBattleResolutionInput(
        BattleState state,
        BattleResolutionResult result
    )
    {
        var units = new List<LowLuckBattleUnitSnapshot>();
        if (state != null)
        {
            bool hasEnemyUnitIds = state.enemy_unit_ids != null && state.enemy_unit_ids.Count > 0;
            foreach (object unitValue in state.Units())
            {
                BattleUnitState unit = ReadBattleUnitState(unitValue);
                if (unit == null)
                    continue;
                bool isEnemy = hasEnemyUnitIds
                    ? ContainsStringName(state.enemy_unit_ids, unit.unit_id)
                    : unit.faction_id != "player";
                bool isEliteOrBoss =
                    unit.attribute_snapshot != null
                    && unit.attribute_snapshot.GetValue(FORTUNE_MARK_TARGET_STAT_ID) > 0;
                units.Add(
                    new LowLuckBattleUnitSnapshot(
                        unit.unit_id,
                        unit.source_member_id,
                        unit.faction_id,
                        unit.is_alive,
                        isEnemy,
                        isEliteOrBoss
                    )
                );
            }
        }
        StringName battleId = result != null && result.battle_id != ""
            ? result.battle_id
            : state?.battle_id ?? "";
        bool playerWon = result != null && result.winner_faction_id == "player";
        return new LowLuckBattleResolutionInput(battleId, playerWon, units);
    }

    private static BattleUnitState ReadBattleUnitState(object unitValue)
    {
        if (unitValue is BattleUnitState unit)
            return unit;
        if (unitValue is Variant variant && variant.VariantType == Variant.Type.Object)
            return variant.AsGodotObject() as BattleUnitState;
        return null;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName target)
    {
        StringName normalizedTarget = ProgressionDataUtils.to_string_name(target);
        if (values == null || normalizedTarget == "")
            return false;
        foreach (StringName value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == normalizedTarget)
                return true;
        }
        return false;
    }

    private int SeenEventCount(GStringNameArray seenEvents, StringName eventType)
    {
        int count = 0;
        foreach (StringName seenEvent in seenEvents)
            if (seenEvent == eventType)
                count++;
        return count;
    }

    private static SkillDef GetSkill(IReadOnlyDictionary<StringName, SkillDef> skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.TryGetValue(skillId, out SkillDef skillDef))
            return null;
        return skillDef;
    }

    private sealed class BlackCrownSealCase
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Caster;
        public BattleUnitState Boss;
        public BattleUnitState Elite;
        public BattleUnitState AllyTarget;
    }

    private sealed class LowLuckContext
    {
        public PartyState PartyState;
        public CharacterManagementModule Manager;
        public LowLuckEventService Service;

        public void Dispose()
        {
            Service?.Dispose();
            GodotSharpCleanup.DisposeGodotObject(Manager);
            GodotSharpCleanup.DisposeGodotObject(PartyState);
            Service = null;
            Manager = null;
            PartyState = null;
        }
    }
}

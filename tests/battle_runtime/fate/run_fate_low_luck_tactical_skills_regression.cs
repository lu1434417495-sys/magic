using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
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

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestMisstepToSchemeGrantsBonusCalamityWithoutDuplicateCriticalFailEvents();
        TestBlackContractPushOptionsPayTheirSelectedCostAndForceHitWithoutCrit();
        TestDoomShiftMarksSelfAndSwapsWithNearbyAlly();
        TestBlackCrownSealIsBossOnlyOncePerBattleAndAppliesBothLockOptions();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("FATE_25 regression: PASS");
            return 0;
        }
        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"FATE_25 regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMisstepToSchemeGrantsBonusCalamityWithoutDuplicateCriticalFailEvents()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("fate_25_misstep", new Vector2I(5, 4));
        BattleUnitState hero = BuildUnit("misstep_hero", "倒霉先锋", "player", new Vector2I(1, 1), 1, HERO_ID);
        hero.known_skill_level_map[MISSTEP_TO_SCHEME_SKILL_ID] = 1;
        AddUnit(runtime, state, hero);
        state.ally_unit_ids = new GStringNameArray { hero.unit_id };
        state.active_unit_id = hero.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        var lowLuckContext = BuildLowLuckContext(-5, runtime.get_fate_event_bus());
        LowLuckEventService lowLuckService = lowLuckContext.Service;
        if (lowLuckService == null)
        {
            AssertTrue(false, "失手成筹前置失败：LowLuckEventService 未初始化。");
            return;
        }

        BattleFateEventBus fateEventBus = runtime.get_fate_event_bus();
        var seenEvents = new GStringNameArray();
        BattleFateEventBus.EventDispatchedEventHandler eventCallback = (eventType, _payload) =>
        {
            seenEvents.Add(eventType);
        };
        fateEventBus.EventDispatched += eventCallback;
        fateEventBus.dispatch(
            "critical_fail",
            BuildCriticalFailPayload(state.battle_id, HERO_ID, hero.unit_id, -5)
        );

        AssertEq(runtime.get_member_calamity(HERO_ID), 2, "失手成筹应让首次大失败额外获得 1 点 calamity。");
        AssertEq(SeenEventCount(seenEvents, "critical_fail"), 1, "失手成筹不应额外重复派发 critical_fail 事件。");

        var lowLuckResult = lowLuckService.handle_battle_resolution(state, BuildBattleResolutionResult(state.battle_id));
        GArray triggeredEventIds = lowLuckResult.GetValueOrDefault("triggered_event_ids", new GArray()).AsGodotArray();
        AssertTrue(
            triggeredEventIds.Contains("borrowed_road"),
            "失手成筹不应冲掉 Borrowed Road 的大失败计数。"
        );
        if (fateEventBus != null)
            fateEventBus.EventDispatched -= eventCallback;
        lowLuckService.dispose();
        lowLuckContext.Dispose();
        runtime.dispose();
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
        AssertTrue(
            ((BattleUnitState)bloodCase["enemy"]).current_hp < 60,
            "黑契推进·血契命中后应对目标造成伤害。"
        );
        AssertEq(
            ((BattleUnitState)bloodCase["caster"]).current_hp,
            28 - BLACK_CONTRACT_PUSH_HP_COST,
            "黑契推进·血契应先扣除固定生命代价。"
        );
        AssertLogContains(
            ((BattleEventBatch)bloodCase["batch"]).log_lines,
            "必定命中，且不会触发暴击",
            "黑契推进·血契应在 battle log 中回显强制命中语义。"
        );
        ((BattleRuntimeModule)bloodCase["runtime"]).dispose();

        var guardCase = IssueBlackContractPushCase(GUARD_TITHE_VARIANT_ID);
        AssertForceHitPreview(
            (BattlePreview)guardCase["preview"],
            "黑契推进·护契 preview 应按必定命中暴露给指令与 AI 评分。"
        );
        AssertForcedHitNoCrit(
            guardCase.GetValueOrDefault("simulated_result", new GDictionary()).AsGodotDictionary(),
            "黑契推进·护契应改为必定命中且不会暴击。"
        );
        AssertTrue(
            !((BattleUnitState)guardCase["caster"]).has_status_effect(STATUS_GUARDING),
            "黑契推进·护契成功后应移除施法者的 Guard。"
        );
        AssertTrue(
            ((BattleUnitState)guardCase["enemy"]).current_hp < 60,
            "黑契推进·护契命中后应对目标造成伤害。"
        );
        ((BattleRuntimeModule)guardCase["runtime"]).dispose();

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
        AssertTrue(
            actionCaster.has_status_effect(STATUS_STAGGERED),
            "黑契推进·行契成功后应为自己挂上 staggered。"
        );
        actionCaster.current_ap = 2;
        actionRuntime._apply_turn_start_statuses(actionCaster, new BattleEventBatch());
        AssertEq(actionCaster.current_ap, 1, "黑契推进·行契应让施法者下一回合少 1 点行动点。");
        AssertTrue(
            ((BattleUnitState)actionCase["enemy"]).current_hp < 60,
            "黑契推进·行契命中后应对目标造成伤害。"
        );
        actionRuntime.dispose();
    }

    private void TestDoomShiftMarksSelfAndSwapsWithNearbyAlly()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("fate_25_doom_shift", new Vector2I(6, 4));
        BattleUnitState caster = BuildUnit("doom_shift_caster", "断命者", "player", new Vector2I(1, 1), 1, HERO_ID);
        caster.known_active_skill_ids = new GStringNameArray { DOOM_SHIFT_SKILL_ID };
        caster.known_skill_level_map[DOOM_SHIFT_SKILL_ID] = 1;
        BattleUnitState ally = BuildUnit("doom_shift_ally", "护卫", "player", new Vector2I(3, 1), 1, "ally");
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, ally);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, ally.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        BattlePreview illegalPreview = runtime.preview_command(BuildUnitSkillCommand(caster.unit_id, DOOM_SHIFT_SKILL_ID, caster));
        AssertTrue(
            illegalPreview != null && !illegalPreview.allowed,
            "断命换位不应允许以自己为目标。"
        );

        Vector2I originCoord = caster.coord;
        Vector2I allyCoord = ally.coord;
        BattleEventBatch batch = runtime.issue_command(BuildUnitSkillCommand(caster.unit_id, DOOM_SHIFT_SKILL_ID, ally));
        AssertTrue(caster.has_status_effect(STATUS_MARKED), "断命换位成功后应给施法者写入 marked。");
        AssertEq(caster.coord, allyCoord, "断命换位应把施法者送到队友原位置。");
        AssertEq(ally.coord, originCoord, "断命换位应把队友换到施法者原位置。");
        AssertLogContains(batch.log_lines, "交换位置", "断命换位应在 battle log 中说明换位结果。");
        runtime.dispose();
    }

    private void TestBlackCrownSealIsBossOnlyOncePerBattleAndAppliesBothLockOptions()
    {
        var counterCase = BuildBlackCrownSealCase("fate_25_black_crown_counter");
        BattleRuntimeModule counterRuntime = counterCase.Runtime;
        BattleUnitState counterCaster = counterCase.Caster;
        BattleUnitState boss = counterCase.Boss;
        BattleUnitState elite = counterCase.Elite;
        SkillDef skillDef = GetSkill(counterRuntime.get_skill_defs(), BLACK_CROWN_SEAL_SKILL_ID);

        BattlePreview illegalPreview = counterRuntime.preview_command(
            BuildUnitSkillCommand(counterCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, elite, COUNTERATTACK_LOCK_VARIANT_ID)
        );
        AssertTrue(
            illegalPreview != null && !illegalPreview.allowed,
            "黑冠封印应拒绝非 boss 的 elite 目标。"
        );

        counterRuntime.issue_command(
            BuildUnitSkillCommand(counterCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, boss, COUNTERATTACK_LOCK_VARIANT_ID)
        );
        AssertTrue(
            boss.has_status_effect(STATUS_BLACK_CROWN_SEAL_COUNTERATTACK),
            "黑冠封印·禁反击成功后应写入对应状态。"
        );
        AssertTrue(counterRuntime.is_unit_counterattack_locked(boss), "黑冠封印·禁反击应封锁 boss 的反击。");
        counterCaster.current_ap = 1;
        AssertEq(
            counterRuntime.get_skill_cast_block_reason(counterCaster, skillDef),
            "黑冠封印每战只能施放 1 次。",
            "黑冠封印成功后应立刻进入每战 1 次的封锁状态。"
        );
        counterRuntime.dispose();

        var critCase = BuildBlackCrownSealCase("fate_25_black_crown_crit");
        BattleRuntimeModule critRuntime = critCase.Runtime;
        BattleUnitState critCaster = critCase.Caster;
        BattleUnitState critBoss = critCase.Boss;
        critRuntime.issue_command(
            BuildUnitSkillCommand(critCaster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, critBoss, CRIT_LOCK_VARIANT_ID)
        );
        AssertTrue(
            critBoss.has_status_effect(STATUS_BLACK_CROWN_SEAL_CRIT),
            "黑冠封印·禁暴击成功后应写入对应状态。"
        );
        critRuntime.dispose();
    }

    private GDictionary IssueBlackContractPushCase(StringName variantId)
    {
        BattleRuntimeModule runtime = BuildRuntime();
        SkillDef skillDef = GetSkill(runtime.get_skill_defs(), BLACK_CONTRACT_PUSH_SKILL_ID);
        CombatCastVariantDef castVariant = skillDef?.combat_profile?.get_cast_variant(variantId);
        BattleState state = BuildSkillTestState($"black_contract_{variantId}", new Vector2I(6, 4));
        BattleUnitState caster = BuildUnit("contract_caster", "契约战士", "player", new Vector2I(1, 1), 1, HERO_ID);
        caster.current_hp = 28;
        caster.known_active_skill_ids = new GStringNameArray { BLACK_CONTRACT_PUSH_SKILL_ID };
        caster.known_skill_level_map[BLACK_CONTRACT_PUSH_SKILL_ID] = 1;
        if (variantId == GUARD_TITHE_VARIANT_ID)
            SetStatus(caster, STATUS_GUARDING, 60);
        BattleUnitState enemy = BuildUnit("contract_target", "高闪避敌人", "enemy", new Vector2I(2, 1), 1);
        enemy.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 999);
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, enemy);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        BattlePreview preview = runtime.preview_command(BuildUnitSkillCommand(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variantId));
        AssertTrue(preview != null && preview.allowed, $"黑契推进 {variantId} 前置：目标应可预览。");

        var simulatedResult = runtime._resolve_unit_skill_effect_result(
            caster,
            enemy,
            skillDef,
            runtime._collect_unit_skill_effect_defs(skillDef, castVariant)
        );
        BattleEventBatch batch = runtime.issue_command(BuildUnitSkillCommand(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variantId));
        return new GDictionary
        {
            ["runtime"] = runtime,
            ["caster"] = caster,
            ["enemy"] = enemy,
            ["batch"] = batch,
            ["preview"] = preview,
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
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        return new BlackCrownSealCase
        {
            Runtime = runtime,
            Caster = caster,
            Boss = boss,
            Elite = elite,
            AllyTarget = allyTarget,
        };
    }

    private void AssertForcedHitNoCrit(GDictionary result, string message)
    {
        AssertTrue(
            result.GetValueOrDefault("attack_success", false).AsBool()
                && result.GetValueOrDefault("crit_locked", false).AsBool()
                && !result.GetValueOrDefault("critical_hit", false).AsBool(),
            $"{message} result={result}"
        );
    }

    private void AssertForceHitPreview(BattlePreview preview, string message)
    {
        AttackPreviewData hitPreview = preview?.hit_preview;
        AssertTrue(hitPreview != null && !hitPreview.IsEmpty, $"{message} preview={preview}");
        AssertEq(hitPreview.HitRatePercent, 100, $"{message} hit_rate_percent 应为 100。");
        AssertEq(hitPreview.SuccessRatePercent, 100, $"{message} success_rate_percent 应为 100。");
        AssertTrue(
            hitPreview.StageSuccessRates.Count == 1 && hitPreview.StageSuccessRates[0] == 100,
            $"{message} stage_success_rates 应为 [100]。"
        );
        AssertTrue(hitPreview.ForceHitNoCrit, $"{message} 应标记 force_hit_no_crit。");
        AssertTrue(
            (hitPreview.SummaryText ?? "").Contains("必定命中")
                && (hitPreview.SummaryText ?? "").Contains("禁暴击"),
            $"{message} 文案应说明必定命中且禁暴击。"
        );
    }

    private BattleRuntimeModule BuildRuntime()
    {
        var registry = new ProgressionContentRegistry();
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, registry.get_skill_defs(), new GDictionary(), new GDictionary());
        runtime.configure_damage_resolver_for_tests(new DeterministicBattleDamageResolver());
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        return runtime;
    }

    private void BeginRuntimeBattle(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.calamity_by_member_id.Clear();
        runtime.get_fate_runtime().begin_battle(runtime.calamity_by_member_id);
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
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.base_terrain = BattleCellState.TERRAIN_LAND();
        cell.base_height = 4;
        cell.height_offset = 0;
        cell.recalculate_runtime_values();
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
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 120);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value("action_points", Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 12);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 6);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.attribute_snapshot.set_value("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.set_value("faith_luck_bonus", 0);
        unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, isBoss ? 2 : (isElite ? 1 : 0));
        unit.attribute_snapshot.set_value(BOSS_TARGET_STAT_ID, isBoss ? 1 : 0);
        return unit;
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
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
        unitState.set_status_effect(statusEntry);
    }

    private BattleCommand BuildUnitSkillCommand(
        StringName unitId,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName variantId = default
    )
    {
        var command = new BattleCommand();
        command.command_type = BattleCommand.TYPE_SKILL();
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.skill_variant_id = variantId;
        command.target_unit_id = targetUnit?.unit_id ?? default;
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private LowLuckContext BuildLowLuckContext(int hiddenLuckAtBirth, BattleFateEventBus fateEventBus)
    {
        var partyState = new PartyState();
        partyState.leader_member_id = HERO_ID;
        partyState.main_character_member_id = HERO_ID;
        partyState.active_member_ids = new GStringNameArray { HERO_ID };
        partyState.set_member_state(BuildMemberState(hiddenLuckAtBirth));
        var manager = new CharacterManagementModule();
        manager.setup(partyState, new GDictionary(), new GDictionary(), new GDictionary());
        var service = new LowLuckEventService();
        service.setup(manager, fateEventBus);
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
        memberState.progression.unit_base_attributes.set_attribute_value("hidden_luck_at_birth", hiddenLuckAtBirth);
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

    private BattleResolutionResult BuildBattleResolutionResult(StringName battleId)
    {
        var result = new BattleResolutionResult();
        result.battle_id = battleId;
        result.winner_faction_id = "player";
        return result;
    }

    private int SeenEventCount(GStringNameArray seenEvents, StringName eventType)
    {
        int count = 0;
        foreach (StringName seenEvent in seenEvents)
            if (seenEvent == eventType)
                count++;
        return count;
    }

    private void AssertLogContains(GStringArray lines, string needle, string message)
    {
        foreach (string line in lines)
        {
            if (line.Contains(needle))
                return;
        }
        _failures.Add($"{message} log={lines}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
            return null;
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private sealed class BlackCrownSealCase
    {
        public BattleRuntimeModule Runtime;
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
            Service?.dispose();
        }
    }
}

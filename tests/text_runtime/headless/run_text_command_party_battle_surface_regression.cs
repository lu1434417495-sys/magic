using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_party_battle_surface_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPartyEquipAndBattleCommandsUseTypedRuntimeBoundary();

        RequestTestExit(_test.Finish("Text command party/battle surface regression"));
    }

    private void TestPartyEquipAndBattleCommandsUseTypedRuntimeBoundary()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new test"), "game new test 应成功。");

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            GameSession gameSession = session?.GetGameSessionTyped();
            _test.True(runtime != null, "party/battle 文本回归应拿到 typed runtime。");
            _test.True(gameSession != null, "party/battle 文本回归应拿到 typed game session。");
            if (runtime == null || gameSession == null)
                return;

            AssertCommandOk(
                runner.ExecuteLine("warehouse add bronze_sword 1"),
                "warehouse add bronze_sword 应成功。"
            );
            string bronzeSwordInstanceId = FindWarehouseEquipmentInstanceId(
                gameSession.GetPartyState(),
                "bronze_sword"
            );
            _test.True(
                !string.IsNullOrEmpty(bronzeSwordInstanceId),
                "party/battle 文本回归应能从 typed 仓库状态拿到 bronze_sword instance_id。"
            );
            AssertCommandOk(
                runner.ExecuteLine(
                    $"party equip player_sword_01 bronze_sword instance_id={bronzeSwordInstanceId}"
                ),
                "party equip 带 instance_id 应成功。"
            );
            _test.Eq(
                gameSession
                    .GetPartyState()
                    ?.GetMemberState("player_sword_01")
                    ?.equipment_state
                    ?.GetEquippedItemId("main_hand") ?? "",
                new StringName("bronze_sword"),
                "party equip 后主手应装备 bronze_sword。"
            );
            _test.Eq(
                (
                    gameSession
                        .GetPartyState()
                        ?.GetMemberState("player_sword_01")
                        ?.equipment_state
                        ?.GetEquippedInstanceId("main_hand") ?? ""
                ).ToString(),
                bronzeSwordInstanceId,
                "party equip 带 instance_id 时应写入指定实例。"
            );

            AssertCommandOk(
                runner.ExecuteLine("party unequip player_sword_01 main_hand"),
                "party unequip 应成功。"
            );
            _test.Eq(
                gameSession
                    .GetPartyState()
                    ?.GetMemberState("player_sword_01")
                    ?.equipment_state
                    ?.GetEquippedItemId("main_hand") ?? "",
                new StringName(),
                "party unequip 后主手应清空。"
            );
            _test.Eq(
                runtime.GetPartyWarehouseService()?.CountItem("bronze_sword") ?? -1,
                1,
                "party unequip 后 bronze_sword 应返回仓库。"
            );

            AssertCommandOk(
                runner.ExecuteLine("battle start settlement"),
                "battle start settlement 应成功。"
            );
            AdvanceUntilBattleActive(runner);
            using GodotProjectionLease<GDictionary> battleSnapshotLease =
                session.BuildSnapshotLease();
            GDictionary battleSnapshot = Dict(battleSnapshotLease.Value, "battle");
            _test.True(Bool(battleSnapshot, "active"), "battle start 后应进入 active battle。");
            _test.True(
                Bool(battleSnapshot, "start_confirm_visible"),
                "battle start 后应出现开始战斗确认。"
            );

            AssertCommandOk(
                runner.ExecuteLine("battle confirm"),
                "battle confirm 应成功。"
            );
            Dictionary<StringName, (int CurrentHp, int MaxHp)> manualUnitHp =
                PrimeManualUnitSurvival(runtime);
            AdvanceToManualBattleTurn(runner);
            RestoreManualUnitHp(runtime, manualUnitHp);

            PrimeActiveManualSkillBlocker(runtime, 0, 0);
            GameTextCommandResult skillBlockedResult = runner.ExecuteLine("battle skill 1");
            _test.True(!skillBlockedResult.skipped, "体力不足的 skill 命令必须实际进入 runtime。");
            _test.False(skillBlockedResult.ok, "体力不足时 battle skill 1 应失败。");
            _test.Eq(
                skillBlockedResult.code,
                RuntimeCommandCode.InvalidState,
                "battle skill blocker 应返回 InvalidState code。"
            );
            _test.Eq(
                runtime.GetSelectedBattleSkillId(),
                new StringName(),
                "skill blocker 失败后不应保留 selected skill。"
            );

            PrimeActiveManualMultiVariantSkill(runtime);
            AssertCommandOk(runner.ExecuteLine("battle skill 1"), "battle skill 1 应选中多形态技能。");
            _test.Eq(
                runtime.GetSelectedBattleSkillId(),
                new StringName("mage_delayed_fireball"),
                "多形态前置应选中正式 mage_delayed_fireball 定义。"
            );
            StringName initialVariantId = runtime.GetSelectedBattleSkillVariantId();
            _test.True(initialVariantId != "", "选中多形态技能后应有默认 variant。");
            AssertCommandOk(runner.ExecuteLine("battle option next"), "battle option next 应成功。");
            _test.True(
                runtime.GetSelectedBattleSkillVariantId() != ""
                    && runtime.GetSelectedBattleSkillVariantId() != initialVariantId,
                "battle option next 应把已选 variant 切换到另一个已解锁形态。"
            );

            const string NonSelfTargetUnitId = "non_self_target_unit";
            PrimeActiveManualSkillBlocker(runtime, 2, 0);
            GameTextCommandResult targetBlockedResult = runner.ExecuteLine(
                $"battle equip main_hand bronze_sword target_unit_id={NonSelfTargetUnitId}"
            );
            _test.True(!targetBlockedResult.skipped, "battle equip 负例必须实际进入 runtime。");
            _test.False(
                targetBlockedResult.ok,
                "指定其他目标时 battle equip 应失败。"
            );
            _test.Eq(
                targetBlockedResult.code,
                RuntimeCommandCode.InvalidState,
                "battle equip target_unit_id self-only 失败应返回 InvalidState code。"
            );

            Vector2I activeCoord;
            using (GodotProjectionLease<GDictionary> activeCoordSnapshotLease = session.BuildSnapshotLease())
                activeCoord = FindActiveUnitCoord(activeCoordSnapshotLease.Value);
            _test.True(activeCoord != new Vector2I(-1, -1), "应能找到当前行动单位坐标。");
            if (activeCoord != new Vector2I(-1, -1))
            {
                AssertCommandOk(
                    runner.ExecuteLine($"battle inspect {activeCoord.X} {activeCoord.Y}"),
                    "battle inspect 应成功。"
                );
                _test.Eq(
                    runtime.GetActiveModalId(),
                    "character_info",
                    "battle inspect 后应打开 character_info modal。"
                );
                using (GodotProjectionLease<GDictionary> inspectSnapshotLease =
                    session.BuildSnapshotLease())
                {
                    GDictionary characterInfo = Dict(
                        inspectSnapshotLease.Value,
                        "character_info"
                    );
                    _test.True(
                        Bool(characterInfo, "visible"),
                        "battle inspect snapshot 应显示 character_info。"
                    );
                    _test.Eq(
                        DictString(characterInfo, "source", ""),
                        "battle",
                        "battle inspect snapshot 应保留 battle source。"
                    );
                    _test.True(
                        !string.IsNullOrEmpty(
                            DictString(characterInfo, "display_name", "")
                        ),
                        "battle inspect snapshot 应包含人物显示名。"
                    );
                    _test.True(
                        DictArray(characterInfo, "sections").Count > 0,
                        "battle inspect snapshot 应包含 typed context 投影的 sections。"
                    );
                }
                AssertCommandOk(runner.ExecuteLine("close"), "close 应能关闭 battle inspect modal。");
                using (GodotProjectionLease<GDictionary> closeSnapshotLease =
                    session.BuildSnapshotLease())
                {
                    _test.False(
                        Bool(Dict(closeSnapshotLease.Value, "character_info"), "visible"),
                        "close 后 character_info snapshot 应不可见。"
                    );
                }
            }

            Vector2I moveTarget = FindReachableMoveTarget(runtime, activeCoord);
            _test.True(moveTarget != new Vector2I(-1, -1), "应能找到一个可达 battle move 目标。");
            if (moveTarget != new Vector2I(-1, -1))
            {
                BattleUnitState movingUnit = runtime.GetBattleState()?.GetUnit(
                    runtime.GetBattleState().active_unit_id
                );
                int movePointsBefore = movingUnit?.GetCurrentMovePoints() ?? -1;
                AssertCommandOk(
                    runner.ExecuteLine($"battle move {moveTarget.X} {moveTarget.Y}"),
                    "battle move <x> <y> 应成功。"
                );
                _test.Eq(
                    movingUnit?.GetAnchorCoord() ?? new Vector2I(-1, -1),
                    moveTarget,
                    "battle move 应真正更新行动单位的 anchor coord。"
                );
                _test.True(
                    movingUnit != null
                        && movePointsBefore > movingUnit.GetCurrentMovePoints(),
                    "battle move 应按路径消耗正式移动力。"
                );
            }

            StringName waitingUnitId = runtime.GetBattleState()?.active_unit_id ?? "";
            _test.True(waitingUnitId != "", "battle wait 前应仍有手动行动单位。");
            AssertCommandOk(runner.ExecuteLine("battle wait"), "battle wait 应成功。");
            _test.Eq(
                runtime.GetBattleState()?.active_unit_id ?? "",
                new StringName(),
                "battle wait 应真正结束当前回合并交回 timeline。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static void AdvanceUntilBattleActive(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            HeadlessGameTestSession session = runner.GetSession();
            if (session == null)
                break;
            bool battleActive;
            using (GodotProjectionLease<GDictionary> snapshotLease = session.BuildSnapshotLease())
                battleActive = Bool(Dict(snapshotLease.Value, "battle"), "active");
            if (battleActive)
                return;
            runner.ExecuteLine("battle tick 1");
        }
    }

    private void AdvanceToManualBattleTurn(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            HeadlessGameTestSession session = runner.GetSession();
            if (session == null)
                break;
            bool battleActive;
            bool manualTurn;
            using (GodotProjectionLease<GDictionary> snapshotLease = session.BuildSnapshotLease())
            {
                GDictionary battleSnapshot = Dict(snapshotLease.Value, "battle");
                battleActive = Bool(battleSnapshot, "active");
                string activeUnitId = DictString(battleSnapshot, "active_unit_id", "");
                GDictionary activeUnit = FindBattleUnit(battleSnapshot, activeUnitId);
                manualTurn = DictString(activeUnit, "control_mode", "") == "manual";
            }
            if (!battleActive)
                break;
            if (manualTurn)
                return;
            AssertCommandOk(runner.ExecuteLine("battle tick 1"), "推进到手动回合的 battle tick 应成功。");
        }
        _test.Fail("文本 party/battle surface 回归未能进入手动单位回合。");
    }

    private static Dictionary<StringName, (int CurrentHp, int MaxHp)> PrimeManualUnitSurvival(
        GameRuntimeFacade runtime
    )
    {
        var snapshots = new Dictionary<StringName, (int CurrentHp, int MaxHp)>();
        foreach (BattleUnitState unit in runtime?.GetBattleState()?.GetUnitsTyped() ?? new List<BattleUnitState>())
        {
            if (unit?.control_mode != "manual" || unit.attribute_snapshot == null)
                continue;
            snapshots[unit.unit_id] = (
                unit.GetCurrentHp(),
                unit.attribute_snapshot.GetValue("hp_max")
            );
            unit.attribute_snapshot.SetValue("hp_max", 100);
            unit.SetCurrentHp(100);
        }
        return snapshots;
    }

    private static void RestoreManualUnitHp(
        GameRuntimeFacade runtime,
        IReadOnlyDictionary<StringName, (int CurrentHp, int MaxHp)> snapshots
    )
    {
        foreach ((StringName unitId, (int currentHp, int maxHp)) in snapshots)
        {
            BattleUnitState unit = runtime?.GetBattleState()?.GetUnit(unitId);
            if (unit?.attribute_snapshot == null)
                continue;
            unit.attribute_snapshot.SetValue("hp_max", maxHp);
            unit.SetCurrentHp(currentHp);
        }
    }

    private static void PrimeActiveManualSkillBlocker(
        GameRuntimeFacade runtime,
        int currentStamina,
        int cooldown
    )
    {
        BattleState battleState = runtime?.GetBattleState();
        if (battleState == null || battleState.IsEmpty() || battleState.active_unit_id == "")
            return;
        BattleUnitState activeUnit = battleState.ContainsUnit(battleState.active_unit_id)
            ? battleState.GetUnit(battleState.active_unit_id)
            : null;
        if (activeUnit == null)
            return;
        activeUnit.SetKnownActiveSkillIds(new[] { new StringName("archer_long_draw") });
        activeUnit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["archer_long_draw"] = 1,
            }
        );
        activeUnit.SetCurrentAp(2);
        activeUnit.SetCurrentStamina(currentStamina);
        activeUnit.SetCooldownsTyped(null);
        if (cooldown > 0)
            activeUnit.SetCooldownTyped("archer_long_draw", cooldown);
        if (activeUnit.attribute_snapshot != null)
        {
            activeUnit.attribute_snapshot.SetValue("action_points", 2);
            activeUnit.attribute_snapshot.SetValue("stamina_max", Mathf.Max(currentStamina, 2));
        }
        runtime.CommandBattleClearSkillTyped();
        runtime.RefreshBattleSelectionState();
    }

    private static void PrimeActiveManualMultiVariantSkill(GameRuntimeFacade runtime)
    {
        BattleState battleState = runtime?.GetBattleState();
        if (battleState == null || battleState.IsEmpty() || battleState.active_unit_id == "")
            return;
        BattleUnitState activeUnit = battleState.ContainsUnit(battleState.active_unit_id)
            ? battleState.GetUnit(battleState.active_unit_id)
            : null;
        if (activeUnit == null)
            return;
        activeUnit.SetKnownActiveSkillIds(new[] { new StringName("mage_delayed_fireball") });
        activeUnit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["mage_delayed_fireball"] = 1,
            }
        );
        activeUnit.SetCurrentAp(3);
        activeUnit.SetCurrentMp(100);
        activeUnit.SetCurrentStamina(50);
        activeUnit.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        activeUnit.SetCooldownsTyped(null);
        if (activeUnit.attribute_snapshot != null)
        {
            activeUnit.attribute_snapshot.SetValue("action_points", 3);
            activeUnit.attribute_snapshot.SetValue("mp_max", 100);
            activeUnit.attribute_snapshot.SetValue("stamina_max", 50);
        }
        runtime.CommandBattleClearSkillTyped();
        runtime.RefreshBattleSelectionState();
    }

    private static Vector2I FindReachableMoveTarget(GameRuntimeFacade runtime, Vector2I activeCoord)
    {
        if (runtime == null)
            return new Vector2I(-1, -1);
        foreach (Vector2I coord in runtime.GetBattleMovementReachableCoords())
        {
            if (coord != activeCoord)
                return coord;
        }
        return new Vector2I(-1, -1);
    }

    private static string FindWarehouseEquipmentInstanceId(PartyState partyState, StringName itemId)
    {
        if (partyState?.warehouse_state == null)
            return "";
        foreach (EquipmentInstanceState instance in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance != null && instance.item_id == itemId)
                return instance.instance_id.ToString();
        }
        return "";
    }

    private static Vector2I FindActiveUnitCoord(GDictionary snapshot)
    {
        GDictionary battle = Dict(snapshot, "battle");
        string activeUnitId = DictString(battle, "active_unit_id", "");
        GDictionary activeUnit = FindBattleUnit(battle, activeUnitId);
        if (activeUnit.Count == 0)
            return new Vector2I(-1, -1);
        GDictionary coord = Dict(activeUnit, "coord");
        return new Vector2I(DictInt(coord, "x", -1), DictInt(coord, "y", -1));
    }

    private static GDictionary FindBattleUnit(GDictionary battleSnapshot, string unitId)
    {
        GArray units = DictArray(battleSnapshot, "units");
        foreach (Variant unitValue in units)
        {
            if (unitValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary unit = unitValue.AsGodotDictionary();
            if (DictString(unit, "unit_id", "") == unitId)
                return unit;
        }
        return new GDictionary();
    }

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static GArray DictArray(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();

    private static bool Bool(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].AsBool();

    private static int DictInt(GDictionary dictionary, string key, int fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(
            result != null && !result.skipped && result.ok,
            $"{message} skipped={result?.skipped} message={result?.message}"
        );
    }
}

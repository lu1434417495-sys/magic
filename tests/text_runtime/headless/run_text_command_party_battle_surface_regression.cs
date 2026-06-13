using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_party_battle_surface_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPartyEquipAndBattleCommandsUseTypedRuntimeBoundary();

        Quit(_test.Finish("Text command party/battle surface regression"));
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
            GDictionary battleSnapshot = Dict(session.BuildSnapshot(), "battle");
            _test.True(Bool(battleSnapshot, "active"), "battle start 后应进入 active battle。");
            _test.True(
                Bool(battleSnapshot, "start_confirm_visible"),
                "battle start 后应出现开始战斗确认。"
            );

            AssertCommandOk(
                runner.ExecuteLine("battle confirm"),
                "battle confirm 应成功。"
            );
            AdvanceToManualBattleTurn(runner);

            PrimeActiveManualSkillBlocker(runtime, 0, 0);
            GameTextCommandResult skillBlockedResult = runner.ExecuteLine("battle skill 1");
            _test.False(skillBlockedResult.ok, "体力不足时 battle skill 1 应失败。");
            _test.Eq(
                skillBlockedResult.code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "battle skill blocker 应返回 InvalidState code。"
            );
            _test.Eq(
                runtime.GetSelectedBattleSkillId(),
                new StringName(),
                "skill blocker 失败后不应保留 selected skill。"
            );

            AssertCommandOk(runner.ExecuteLine("battle clear"), "battle clear 应成功。");
            AssertCommandOk(runner.ExecuteLine("battle option next"), "battle option next 应成功。");

            const string NonSelfTargetUnitId = "non_self_target_unit";
            PrimeActiveManualSkillBlocker(runtime, 2, 0);
            GameTextCommandResult targetBlockedResult = runner.ExecuteLine(
                $"battle equip main_hand bronze_sword target_unit_id={NonSelfTargetUnitId}"
            );
            _test.False(
                targetBlockedResult.ok,
                "指定其他目标时 battle equip 应失败。"
            );
            _test.Eq(
                targetBlockedResult.code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "battle equip target_unit_id self-only 失败应返回 InvalidState code。"
            );

            Vector2I activeCoord = FindActiveUnitCoord(session.BuildSnapshot());
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
                AssertCommandOk(runner.ExecuteLine("close"), "close 应能关闭 battle inspect modal。");
            }

            Vector2I moveTarget = FindReachableMoveTarget(runtime, activeCoord);
            _test.True(moveTarget != new Vector2I(-1, -1), "应能找到一个可达 battle move 目标。");
            if (moveTarget != new Vector2I(-1, -1))
            {
                AssertCommandOk(
                    runner.ExecuteLine($"battle move {moveTarget.X} {moveTarget.Y}"),
                    "battle move <x> <y> 应成功。"
                );
            }

            AssertCommandOk(runner.ExecuteLine("battle wait"), "battle wait 应成功。");
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
            GDictionary battleSnapshot = Dict(runner.GetSession()?.BuildSnapshot(), "battle");
            if (Bool(battleSnapshot, "active"))
                return;
            runner.ExecuteLine("battle tick 1");
        }
    }

    private void AdvanceToManualBattleTurn(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            GDictionary battleSnapshot = Dict(runner.GetSession()?.BuildSnapshot(), "battle");
            if (!Bool(battleSnapshot, "active"))
                break;
            string activeUnitId = DictString(battleSnapshot, "active_unit_id", "");
            GDictionary activeUnit = FindBattleUnit(battleSnapshot, activeUnitId);
            if (DictString(activeUnit, "control_mode", "") == "manual")
                return;
            AssertCommandOk(runner.ExecuteLine("battle tick 1"), "推进到手动回合的 battle tick 应成功。");
        }
        _test.Fail("文本 party/battle surface 回归未能进入手动单位回合。");
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
        BattleUnitState activeUnit = battleState.units.ContainsKey(battleState.active_unit_id)
            ? battleState.units[battleState.active_unit_id].AsGodotObject() as BattleUnitState
            : null;
        if (activeUnit == null)
            return;
        activeUnit.known_active_skill_ids = new() { "archer_long_draw" };
        activeUnit.known_skill_level_map.Clear();
        activeUnit.known_skill_level_map["archer_long_draw"] = 1;
        activeUnit.current_ap = 2;
        activeUnit.current_stamina = currentStamina;
        activeUnit.cooldowns.Clear();
        if (cooldown > 0)
            activeUnit.cooldowns["archer_long_draw"] = cooldown;
        if (activeUnit.attribute_snapshot != null)
        {
            activeUnit.attribute_snapshot.SetValue("action_points", 2);
            activeUnit.attribute_snapshot.SetValue("stamina_max", Mathf.Max(currentStamina, 2));
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
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}

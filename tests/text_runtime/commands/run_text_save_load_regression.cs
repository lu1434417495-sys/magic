using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_save_load_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private GameTextCommandRunner _runner;

    public override void _Initialize()
    {
        TestResult exitCode = null;
        try
        {
            _runner = new GameTextCommandRunner();
            _runner.initialize();

            TestGameLoadReopensNewlyCreatedSave(_runner);
            TestGameLoadReopensForgePersistedSave(_runner);
            _runner.Dispose(true);
            _runner = null;

            exitCode = _test.Finish("Text save/load regression");
        }
        finally
        {
            if (_runner != null)
            {
                _runner.Dispose(true);
                _runner = null;
            }
            RequestTestExit(exitCode ?? _test.Finish("Text save/load regression", 1));
        }
    }

    private void TestGameLoadReopensNewlyCreatedSave(GameTextCommandRunner runner)
    {
        using GameTextCommandResult createResult = runner.ExecuteLine(
            "game new ashen_intersection"
        );
        _test.True(createResult.ok, "game new ashen_intersection 应创建成功。");
        if (!createResult.ok)
            return;

        string saveId = runner.GetSession().GetGameSession().GetActiveSaveId();
        _test.True(!string.IsNullOrEmpty(saveId), "新建世界后应能读取 active save id。");
        _test.True(SaveSlotsIncludeId(runner, saveId), "新建世界后 save slots 应包含 active save id。");
        AssertUngeneratedSubmapPlaceholder(
            runner,
            "ashen_ashlands",
            "新建 ashen_intersection 后应保留未生成子地图空占位。"
        );

        using GameTextCommandResult loadResult = runner.ExecuteLine($"game load {saveId}");
        _test.True(loadResult.ok, "game load 刚创建的 save_id 应成功。");
        _test.Eq(
            runner.GetSession().GetGameSession().GetActiveSaveId(),
            saveId,
            "重新载入后 active save id 应保持不变。"
        );
        AssertUngeneratedSubmapPlaceholder(
            runner,
            "ashen_ashlands",
            "重新载入后未生成子地图占位仍应是空 world_data。"
        );
    }

    private void TestGameLoadReopensForgePersistedSave(GameTextCommandRunner runner)
    {
        using GameTextCommandResult createResult = runner.ExecuteLine(
            "game new ashen_intersection"
        );
        _test.True(createResult.ok, "forge save/load 回归前置：应能创建 ashen_intersection 世界。");
        if (!createResult.ok)
            return;

        string saveId = runner.GetSession().GetGameSession().GetActiveSaveId();
        _test.True(!string.IsNullOrEmpty(saveId), "forge save/load 回归前置：应能读取 active save id。");

        AssertCommandOk(runner, "warehouse add bronze_sword 1");
        AssertCommandOk(runner, "warehouse add iron_ore 3");
        AssertCommandOk(runner, "world open");
        AssertCommandOk(runner, "settlement action service:repair_gear");
        AssertCommandOk(
            runner,
            "settlement action service:repair_gear submission_source=forge recipe_id=forge_smith_iron_greatsword"
        );
        _test.Eq(
            CountRuntimeWarehouseItem(runner, "iron_greatsword"),
            1,
            "锻造完成后，当前 runtime 仓库应立即包含铁制大剑。"
        );
        _test.Eq(
            CountPartyStateItem(runner.GetSession().GetGameSession().GetPartyState(), "iron_greatsword"),
            1,
            "锻造完成并持久化后，GameSession 内部 PartyState 应包含铁制大剑。"
        );

        _test.True(SaveSlotsIncludeId(runner, saveId), "forge 持久化后 save slots 应继续包含原始 save id。");
        using GameTextCommandResult loadResult = runner.ExecuteLine($"game load {saveId}");
        _test.True(loadResult.ok, "game load forge 持久化后的 save_id 应成功。");
        _test.Eq(
            CountPartyStateItem(runner.GetSession().GetGameSession().GetPartyState(), "iron_greatsword"),
            1,
            "重新载入后，GameSession 内部 PartyState 应包含铁制大剑。"
        );
        _test.Eq(
            CountWarehouseItem(loadResult.SnapshotTyped, "iron_greatsword"),
            1,
            "重新载入后应保留通用 forge 产出的铁制大剑。"
        );
    }

    private static bool SaveSlotsIncludeId(GameTextCommandRunner runner, string saveId)
    {
        if (runner == null || string.IsNullOrEmpty(saveId))
            return false;
        foreach (
            Dictionary<string, object> slot in runner
                .GetSession()
                .GetGameSession()
                .ListSaveSlotsPlain()
        )
        {
            if (TypedString(slot, "save_id") == saveId)
                return true;
        }
        return false;
    }

    private static int CountWarehouseItem(IReadOnlyDictionary<string, object> snapshot, string itemId)
    {
        IReadOnlyDictionary<string, object> warehouseSnapshot = TypedDict(snapshot, "warehouse");
        IReadOnlyDictionary<string, object> windowData = TypedDict(warehouseSnapshot, "window_data");
        foreach (object entryValue in TypedArray(windowData, "entries"))
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (TypedString(entry, "item_id") == itemId)
                return entry.ContainsKey("quantity")
                    ? TypedInt(entry, "quantity")
                    : TypedInt(entry, "total_quantity");
        }
        return 0;
    }

    private static int CountRuntimeWarehouseItem(GameTextCommandRunner runner, string itemId)
    {
        GameRuntimeFacade runtime = runner?.GetSession()?.GetRuntimeFacade();
        if (runtime == null)
            return 0;
        GDictionary windowData = runtime.GetWarehouseWindowData();
        foreach (Variant entryValue in ArrayValue(windowData, "entries"))
        {
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "item_id") == itemId)
                return entry.ContainsKey("quantity")
                    ? entry["quantity"].AsInt32()
                    : DictInt(entry, "total_quantity");
        }
        return 0;
    }

    private static int CountPartyStateItem(PartyState partyState, string itemId)
    {
        if (partyState?.warehouse_state == null)
            return 0;
        int count = 0;
        foreach (WarehouseStackState stack in partyState.warehouse_state.GetNonEmptyStacksTyped())
        {
            if (stack != null && stack.item_id.ToString() == itemId)
                count += Mathf.Max(stack.quantity, 0);
        }
        // 装备类物品按仓库规则只能存进 equipment_instances，不会出现在 stacks。
        foreach (
            EquipmentInstanceState instance
            in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped()
        )
        {
            if (instance != null && instance.item_id.ToString() == itemId)
                count += 1;
        }
        return count;
    }

    private void AssertUngeneratedSubmapPlaceholder(
        GameTextCommandRunner runner,
        string submapId,
        string message
    )
    {
        GameSession gameSession = runner.GetSession().GetGameSession();
        _test.True(gameSession != null, $"{message} | GameSession 应可访问。");
        if (gameSession == null)
            return;
        using GodotProjectionLease<GDictionary> worldDataLease = gameSession.GetWorldDataLease();
        GDictionary worldData = worldDataLease.Value;
        _test.True(worldData.ContainsKey("mounted_submaps"), $"{message} | mounted_submaps 应为 Dictionary。");
        GDictionary submaps = Dict(worldData, "mounted_submaps");
        GDictionary submap = Dict(submaps, submapId);
        _test.True(submap.Count > 0, $"{message} | 应存在子地图 {submapId}。");
        _test.False(DictBool(submap, "is_generated", true), $"{message} | 子地图不应被标记为已生成。");
        _test.True(submap.ContainsKey("world_data"), $"{message} | 子地图 world_data 占位应为 Dictionary。");
        GDictionary submapWorldData = Dict(submap, "world_data");
        _test.True(submapWorldData.Count == 0, $"{message} | 未生成子地图 world_data 必须保持空字典。");
    }

    private void AssertCommandOk(GameTextCommandRunner runner, string commandText)
    {
        using GameTextCommandResult result = runner.ExecuteLine(commandText);
        _test.True(result.ok, $"命令失败：{commandText} | {result.message}");
    }

    private static IReadOnlyDictionary<string, object> TypedDict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyDictionary<string, object> typedDictionary
            ? typedDictionary
            : new Dictionary<string, object>();
    }

    private static IReadOnlyList<object> TypedArray(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> typedArray
            ? typedArray
            : System.Array.Empty<object>();
    }

    private static int TypedInt(IReadOnlyDictionary<string, object> dictionary, string key)
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return 0;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => 0,
        };
    }

    private static string TypedString(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return "";
        return rawValue switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => "",
        };
    }

    private static GArray ArrayValue(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();
    }

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsBool() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : 0;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : "";
    }
}

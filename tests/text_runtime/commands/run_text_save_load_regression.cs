using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_save_load_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestGameLoadReopensNewlyCreatedSave();
        TestGameLoadReopensForgePersistedSave();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Text save/load regression"));
    }

    private void TestGameLoadReopensNewlyCreatedSave()
    {
        var runner = new GameTextCommandRunner();
        try
        {
            runner.initialize();

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
        finally
        {
            runner.Dispose(true);
        }
    }

    private void TestGameLoadReopensForgePersistedSave()
    {
        var runner = new GameTextCommandRunner();
        try
        {
            runner.initialize();

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
        finally
        {
            runner.Dispose(true);
        }
    }

    private static bool SaveSlotsIncludeId(GameTextCommandRunner runner, string saveId)
    {
        if (runner == null || string.IsNullOrEmpty(saveId))
            return false;
        IReadOnlyDictionary<string, object> sessionSnapshot = Dict(
            runner.GetSession().BuildSnapshotTyped(),
            "session"
        );
        foreach (object slotValue in ArrayValue(sessionSnapshot, "save_slots"))
        {
            if (
                slotValue is IReadOnlyDictionary<string, object> slot
                && DictString(slot, "save_id") == saveId
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int CountWarehouseItem(IReadOnlyDictionary<string, object> snapshot, string itemId)
    {
        IReadOnlyDictionary<string, object> warehouseSnapshot = Dict(snapshot, "warehouse");
        IReadOnlyDictionary<string, object> windowData = Dict(warehouseSnapshot, "window_data");
        foreach (object entryValue in ArrayValue(windowData, "entries"))
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (DictString(entry, "item_id") == itemId)
                return DictInt(entry, "quantity", DictInt(entry, "total_quantity"));
        }
        return 0;
    }

    private static int CountRuntimeWarehouseItem(GameTextCommandRunner runner, string itemId)
    {
        GameRuntimeFacade runtime = runner?.GetSession()?.GetRuntimeFacade();
        if (runtime == null)
            return 0;
        return CountWarehouseItem(runner.GetSession().BuildSnapshotTyped(), itemId);
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
        GDictionary worldData = gameSession.GetWorldData();
        _test.True(
            TryReadDictionary(worldData, "mounted_submaps", out GDictionary submaps),
            $"{message} | mounted_submaps 应为 Dictionary。"
        );
        if (submaps == null)
            return;
        try
        {
            _test.True(
                TryReadDictionary(submaps, submapId, out GDictionary submap),
                $"{message} | 应存在子地图 {submapId}。"
            );
            if (submap == null)
                return;
            try
            {
                _test.False(
                    DictBool(submap, "is_generated", true),
                    $"{message} | 子地图不应被标记为已生成。"
                );
                _test.True(
                    TryReadDictionary(submap, "world_data", out GDictionary submapWorldData),
                    $"{message} | 子地图 world_data 占位应为 Dictionary。"
                );
                if (submapWorldData == null)
                    return;
                try
                {
                    _test.True(
                        submapWorldData.Count == 0,
                        $"{message} | 未生成子地图 world_data 必须保持空字典。"
                    );
                }
                finally
                {
                    submapWorldData.Dispose();
                }
            }
            finally
            {
                submap.Dispose();
            }
        }
        finally
        {
            submaps.Dispose();
        }
    }

    private void AssertCommandOk(GameTextCommandRunner runner, string commandText)
    {
        using GameTextCommandResult result = runner.ExecuteLine(commandText);
        _test.True(result.ok, $"命令失败：{commandText} | {result.message}");
    }

    private static IReadOnlyList<object> ArrayValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> values
            ? values
            : Array.Empty<object>();
    }

    private static IReadOnlyDictionary<string, object> Dict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyDictionary<string, object> nested
            ? nested
            : EmptyDictionary();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (!TryReadVariant(dictionary, key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    private static int DictInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback = 0
    )
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => fallback,
        };
    }

    private static string DictString(IReadOnlyDictionary<string, object> dictionary, string key)
    {
        return dictionary != null && dictionary.TryGetValue(key, out object rawValue)
            ? ValueString(rawValue)
            : "";
    }

    private static bool TryReadDictionary(
        GDictionary dictionary,
        string key,
        out GDictionary value
    )
    {
        value = null;
        if (!TryReadVariant(dictionary, key, out Variant rawValue))
            return false;
        try
        {
            if (rawValue.VariantType != Variant.Type.Dictionary)
                return false;
            value = rawValue.AsGodotDictionary();
            return value != null;
        }
        finally
        {
            rawValue.Dispose();
        }
    }

    private static bool TryReadVariant(GDictionary dictionary, string key, out Variant value)
    {
        value = default;
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return false;
        value = dictionary[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private static string ValueString(object rawValue)
    {
        return rawValue switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => rawValue?.ToString() ?? "",
        };
    }

    private static IReadOnlyDictionary<string, object> EmptyDictionary() =>
        new Dictionary<string, object>(StringComparer.Ordinal);
}

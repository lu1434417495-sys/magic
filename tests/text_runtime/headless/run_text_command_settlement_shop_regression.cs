using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_settlement_shop_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTextCommandsUseTypedSettlementAndShopBoundary();

        Quit(_test.Finish("Text command settlement/shop regression"));
    }

    private void TestTextCommandsUseTypedSettlementAndShopBoundary()
    {
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandExecuteSettlementActionTyped", BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandExecuteSettlementActionTyped() 应暴露 typed runtime result。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandShopBuyTyped", BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandShopBuyTyped() 应暴露 typed runtime result。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandShopSellTyped", BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandShopSellTyped() 应暴露 typed runtime result。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandStagecoachTravelTyped", BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandStagecoachTravelTyped() 应暴露 typed runtime result。"
        );

        MethodInfo parseNamedArgsTyped = typeof(GameTextCommandRunner).GetMethod(
            "ParseNamedArgsTyped",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseNamedArgs = typeof(GameTextCommandRunner).GetMethod(
            "ParseNamedArgs",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        _test.Eq(
            parseNamedArgsTyped?.ReturnType,
            typeof(Dictionary<string, object>),
            "GameTextCommandRunner settlement named-arg helper 应先停留在 typed Dictionary。"
        );
        _test.True(
            parseNamedArgs == null,
            "GameTextCommandRunner 不应继续保留 GDictionary ParseNamedArgs helper。"
        );
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new test"), "game new test 应成功。");

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            _test.True(runtime != null, "settlement/shop 文本回归应拿到 typed runtime。");
            if (runtime == null)
                return;

            InjectShopService(runtime);

            GameTextCommandResult worldOpenResult = runner.ExecuteLine("world open");
            AssertCommandOk(worldOpenResult, "world open 应成功。");
            _test.Eq(
                SnapshotString(worldOpenResult.snapshot, "modal", "id"),
                "settlement",
                "world open 后应进入 settlement modal。"
            );

            GameTextCommandResult invalidActionResult = runner.ExecuteLine("settlement action service:missing");
            _test.False(invalidActionResult.ok, "未开放的 settlement action 应失败。");
            _test.Eq(
                invalidActionResult.code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "未开放的 settlement action 应返回 InvalidState code。"
            );

            GameTextCommandResult spoofedShopResult = runner.ExecuteLine(
                "settlement action service:basic_supply interaction_script_id=service_research facility_name=伪造图书馆 npc_name=伪造导师 service_type=研究"
            );
            AssertCommandOk(spoofedShopResult, "带命名参数的 settlement action service:basic_supply 应成功。");
            _test.Eq(
                SnapshotString(spoofedShopResult.snapshot, "modal", "id"),
                "shop",
                "伪造 interaction_script_id 时文本命令仍应按真实服务入口落到 shop modal。"
            );

            GameTextCommandResult closeResult = runner.ExecuteLine("close");
            AssertCommandOk(closeResult, "close 应能关闭 spoofed shop modal。");

            GameTextCommandResult openShopResult = runner.ExecuteLine("settlement action service:basic_supply");
            AssertCommandOk(openShopResult, "settlement action service:basic_supply 应成功。");
            _test.Eq(
                SnapshotString(openShopResult.snapshot, "modal", "id"),
                "shop",
                "basic_supply 应切到 shop modal。"
            );

            GameTextCommandResult buyResult = runner.ExecuteLine("shop buy healing_herb");
            AssertCommandOk(buyResult, "shop buy healing_herb 应成功。");
            _test.Eq(
                CountWarehouseItem(buyResult.snapshot, "healing_herb"),
                1,
                "shop buy 后共享仓库应出现 healing_herb。"
            );

            GameTextCommandResult sellResult = runner.ExecuteLine("shop sell healing_herb");
            AssertCommandOk(sellResult, "shop sell healing_herb 应成功。");
            _test.Eq(
                CountWarehouseItem(sellResult.snapshot, "healing_herb"),
                0,
                "shop sell 后共享仓库中的 healing_herb 应被正式扣除。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static void InjectShopService(GameRuntimeFacade runtime)
    {
        if (runtime == null)
            return;
        GDictionary selectedSettlement = runtime.GetSelectedSettlement();
        string settlementId = selectedSettlement != null && selectedSettlement.ContainsKey("settlement_id")
            ? selectedSettlement["settlement_id"].AsString()
            : "";
        if (string.IsNullOrEmpty(settlementId))
            return;

        GDictionary worldData = runtime.GetWorldData();
        if (worldData == null || !worldData.ContainsKey("settlements"))
            return;

        GArray settlements = worldData["settlements"].AsGodotArray();
        for (int index = 0; index < settlements.Count; index++)
        {
            if (settlements[index].VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary settlementData = settlements[index].AsGodotDictionary();
            string candidateSettlementId = settlementData.ContainsKey("settlement_id")
                ? settlementData["settlement_id"].AsString()
                : "";
            if (!string.Equals(candidateSettlementId, settlementId, System.StringComparison.Ordinal))
                continue;

            GArray availableServices = settlementData.ContainsKey("available_services")
                && settlements[index].AsGodotDictionary()["available_services"].VariantType == Variant.Type.Array
                ? settlementData["available_services"].AsGodotArray().Duplicate(true)
                : new GArray();
            UpsertSettlementService(
                availableServices,
                new GDictionary
                {
                    ["action_id"] = "service:basic_supply",
                    ["facility_id"] = "basic_supply",
                    ["facility_template_id"] = "basic_supply",
                    ["facility_name"] = "补给铺",
                    ["npc_id"] = "npc_trader",
                    ["npc_template_id"] = "npc_trader",
                    ["npc_name"] = "行商",
                    ["service_type"] = "补给",
                    ["interaction_script_id"] = "service_basic_supply",
                    ["settlement_id"] = settlementId,
                }
            );
            settlementData["available_services"] = availableServices;
            settlements[index] = settlementData;
            break;
        }
        worldData["settlements"] = settlements;
    }

    private static void UpsertSettlementService(GArray serviceOptions, GDictionary serviceData)
    {
        string actionId = serviceData["action_id"].AsString();
        for (int index = 0; index < serviceOptions.Count; index++)
        {
            if (serviceOptions[index].VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary existing = serviceOptions[index].AsGodotDictionary();
            string existingActionId = existing.ContainsKey("action_id")
                ? existing["action_id"].AsString()
                : "";
            if (!string.Equals(existingActionId, actionId, System.StringComparison.Ordinal))
                continue;
            serviceOptions[index] = serviceData.Duplicate(true);
            return;
        }
        serviceOptions.Add(serviceData.Duplicate(true));
    }

    private static string SnapshotString(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictString(Dict(snapshot, topLevelKey), nestedKey, "");

    private static int CountWarehouseItem(GDictionary snapshot, string itemId)
    {
        GDictionary warehouse = Dict(snapshot, "warehouse");
        GDictionary windowData = Dict(warehouse, "window_data");
        GArray entries = windowData != null && windowData.ContainsKey("entries")
            ? windowData["entries"].AsGodotArray()
            : new GArray();
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "item_id", "") != itemId)
                continue;
            return entry.ContainsKey("quantity")
                ? entry["quantity"].AsInt32()
                : entry.ContainsKey("total_quantity")
                    ? entry["total_quantity"].AsInt32()
                    : 0;
        }
        return 0;
    }

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}

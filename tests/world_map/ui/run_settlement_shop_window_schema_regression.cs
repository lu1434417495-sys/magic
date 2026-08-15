using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_settlement_shop_window_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene SettlementWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/settlement_window.tscn"
    );
    private static readonly PackedScene ShopWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/shop_window.tscn"
    );

    public override async void _Initialize()
    {
        try
        {
            await TestSettlementWindowRendersTypedOverviewData();
            await TestSettlementWindowRendersCountryIdWhenPresent();
            await TestSettlementWindowHidesOnInvalidOverviewData();
            await TestSettlementWindowAppliesMemberAvailability();
            await TestSettlementWindowSubmitsStableServiceIds();
            await TestShopWindowRendersTypedServiceWindowData();
            await TestShopWindowHidesOnInvalidWindowData();
            await TestShopWindowSubmitsStableShopIds();
            await TestStagecoachWindowSubmitsStableDestinationId();
            await TestShopWindowConfirmationFlow();
            await TestForgeWindowRaisesTypedCSharpEvent();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Settlement/shop window schema regression"));
        }
    }

    private async Task<SettlementWindow> CreateSettlementWindow()
    {
        var window = SettlementWindowScene.Instantiate<SettlementWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return window;
    }

    private async Task<ShopWindow> CreateShopWindow()
    {
        var window = ShopWindowScene.Instantiate<ShopWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return window;
    }

    private async Task DisposeWindow(Node window)
    {
        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestSettlementWindowRendersTypedOverviewData()
    {
        SettlementWindow window = await CreateSettlementWindow();
        window.ShowSettlement(MakeSettlementOverviewData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "SettlementWindow 应接受含空 country_id 的 typed overview data。");
        _test.Eq(window.title_label.Text, "灰石镇", "SettlementWindow 应渲染 typed display_name。");
        _test.Eq(window.services_container.GetChildCount(), 1, "SettlementWindow 应渲染一条正式 service entry。");
        _test.True(
            window.meta_label.Text.Contains("占地 2x2"),
            "SettlementWindow 应渲染 typed footprint size。"
        );
        _test.False(
            window.meta_label.Text.Contains("国家"),
            "空 country_id 不应渲染国家段。"
        );
        _test.True(
            window.facilities_label.Text.Contains("仓库 [storage]"),
            "SettlementWindow 应渲染 typed facility 条目。"
        );
        _test.True(
            window.resident_label.Text.Contains("仓库管理员"),
            "SettlementWindow 应渲染 typed 驻留 NPC。"
        );
        _test.True(
            window.member_state_label.Text.Contains("主角"),
            "SettlementWindow 应按 typed default_member_id 选中成员。"
        );
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRendersCountryIdWhenPresent()
    {
        SettlementWindow window = await CreateSettlementWindow();
        window.ShowSettlement(MakeSettlementOverviewData(countryId: "spring_republic"));
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "SettlementWindow 应接受非空 country_id。");
        _test.True(
            window.meta_label.Text.Contains("国家 spring_republic"),
            "SettlementWindow 的元信息应暴露据点 country_id。"
        );
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowHidesOnInvalidOverviewData()
    {
        SettlementWindow window = await CreateSettlementWindow();

        window.ShowSettlement(MakeSettlementOverviewData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(window.Visible, "测试前置：合法 overview data 应打开窗口。");

        window.ShowSettlement(null);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "null overview data 应关闭据点窗口。");
        _test.Eq(window.services_container.GetChildCount(), 0, "null overview data 不应保留 service 按钮。");

        window.ShowSettlement(SettlementOverviewWindowData.Empty);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "缺少 settlement_id / display_name 的 overview data 应关闭据点窗口。");
        _test.Eq(window.services_container.GetChildCount(), 0, "非法 overview data 不应渲染 service 按钮。");

        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowAppliesMemberAvailability()
    {
        SettlementWindow window = await CreateSettlementWindow();
        window.ShowSettlement(
            MakeSettlementOverviewData(
                service: MakeServiceEntry(
                    memberAvailability: new[]
                    {
                        new KeyValuePair<StringName, SettlementMemberAvailabilityData>(
                            "mage",
                            new SettlementMemberAvailabilityData(true, "")
                        ),
                    }
                )
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "带 member availability 的 typed service 应正常渲染。");
        _test.True(
            window.service_state_label.Text.Contains("当前成员不可用"),
            "当前成员不在 member availability 索引内时应按不可用渲染。"
        );
        _test.Eq(
            window.services_container.GetChildCount(),
            1,
            "member availability 不匹配不应移除 service 按钮。"
        );
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowSubmitsStableServiceIds()
    {
        SettlementWindow window = await CreateSettlementWindow();
        string capturedSettlementId = null;
        string capturedActionId = null;
        string capturedMemberId = null;
        int capturedQuantity = -1;
        string capturedSource = null;
        window.action_requested += (settlementId, _, actionId, memberId, quantity, source) =>
        {
            capturedSettlementId = settlementId;
            capturedActionId = actionId;
            capturedMemberId = memberId;
            capturedQuantity = quantity;
            capturedSource = source;
        };

        window.ShowSettlement(MakeSettlementOverviewData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        (window.services_container.GetChild(0) as Button).EmitSignal(
            BaseButton.SignalName.Pressed
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.Eq(capturedSettlementId, "graystone_town_01", "服务提交应携带据点 id。");
        _test.Eq(capturedActionId, "service:warehouse", "服务提交应携带稳定 action id。");
        _test.Eq(capturedMemberId, "hero", "服务提交应携带当前选中成员。");
        _test.Eq(capturedQuantity, 0, "据点总览提交不带数量。");
        _test.Eq(
            capturedSource,
            SettlementSubmissionSources.ToPayloadValue(SettlementSubmissionSource.Settlement),
            "服务提交应标记 settlement 来源。"
        );
        await DisposeWindow(window);
    }

    private async Task TestShopWindowRendersTypedServiceWindowData()
    {
        ShopWindow window = await CreateShopWindow();
        window.ShowShop(MakeShopWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "ShopWindow 应接受 typed service window data。");
        _test.Eq(window.entry_list.ItemCount, 1, "ShopWindow 应渲染一条正式 shop entry。");
        _test.Eq(window.title_label.Text, "灰石镇补给", "ShopWindow 应渲染 typed 标题。");
        _test.True(
            window.meta_label.Text.Contains("补给稳定"),
            "ShopWindow 元信息应附带 typed state_summary_text。"
        );
        _test.Eq(window.confirm_button.Text, "购买", "ShopWindow 应渲染 typed confirm label。");
        _test.True(
            window.details_label.Text.Contains("治疗药水"),
            "ShopWindow 应渲染默认选中条目的详情。"
        );
        await DisposeWindow(window);
    }

    private async Task TestShopWindowHidesOnInvalidWindowData()
    {
        ShopWindow window = await CreateShopWindow();

        window.ShowShop(MakeShopWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(window.Visible, "测试前置：合法 window data 应打开 ShopWindow。");

        window.ShowShop(null);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "null window data 应关闭 ShopWindow。");
        _test.Eq(window.entry_list.ItemCount, 0, "null window data 不应保留 shop entry。");

        window.ShowShop(SettlementServiceWindowData.Empty);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "缺少 settlement/action/panel 的 window data 应关闭 ShopWindow。");

        window.ShowStagecoach(MakeShopWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(
            window.Visible,
            "ShowStagecoach 收到非 stagecoach panel kind 时应关闭窗口。"
        );

        await DisposeWindow(window);
    }

    private async Task TestShopWindowSubmitsStableShopIds()
    {
        ShopWindow window = await CreateShopWindow();
        SettlementShopActionRequest capturedRequest = default;
        bool requested = false;
        window.ShopActionRequested += request =>
        {
            requested = true;
            capturedRequest = request;
        };

        window.ShowShop(MakeShopWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        window.confirm_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(requested, "shop 条目提交应触发强类型 C# 事件。");
        _test.Eq(
            capturedRequest.Action.SettlementId.ToString(),
            "graystone_town_01",
            "shop 请求应保留据点 id。"
        );
        _test.Eq(
            capturedRequest.Action.ActionId.ToString(),
            "shop:trade",
            "shop 请求应保留稳定 action id。"
        );
        _test.Eq(
            capturedRequest.ActionKind,
            SettlementShopActionKind.Buy,
            "买入条目应提交 Buy 语义。"
        );
        _test.Eq(capturedRequest.ItemId.ToString(), "healing_herb", "shop 请求应保留物品 id。");
        _test.Eq(capturedRequest.InstanceId.ToString(), "", "堆叠商品不应携带实例 id。");
        _test.Eq(capturedRequest.Quantity, 1, "shop 请求应提交单件数量。");
        _test.False(window.Visible, "提交 shop 请求后应隐藏窗口。");

        await DisposeWindow(window);
    }

    private async Task TestStagecoachWindowSubmitsStableDestinationId()
    {
        ShopWindow window = await CreateShopWindow();
        SettlementStagecoachActionRequest capturedRequest = default;
        bool requested = false;
        window.StagecoachActionRequested += request =>
        {
            requested = true;
            capturedRequest = request;
        };

        window.ShowStagecoach(MakeStagecoachWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(window.Visible, "typed stagecoach window data 应打开窗口。");
        window.confirm_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(requested, "驿站路线提交应触发强类型 C# 事件。");
        _test.Eq(
            capturedRequest.TargetSettlementId.ToString(),
            "north_outpost",
            "驿站请求应保留目标据点 id。"
        );
        _test.False(window.Visible, "提交驿站请求后应隐藏窗口。");

        await DisposeWindow(window);
    }

    private static SettlementServiceEntryData MakeServiceEntry(
        IEnumerable<KeyValuePair<StringName, SettlementMemberAvailabilityData>> memberAvailability =
            null
    ) =>
        new(
            "service:warehouse",
            "facility_warehouse",
            "仓库",
            "npc_warehouse_keeper",
            "仓库管理员",
            "warehouse",
            "service_warehouse",
            "免费",
            "状态：可用",
            "仓库 · 仓库管理员 · warehouse",
            true,
            "",
            SettlementPanelKind.None,
            memberAvailability
        );

    private static SettlementOverviewWindowData MakeSettlementOverviewData(
        string countryId = "",
        SettlementServiceEntryData service = null
    ) =>
        new(
            "graystone_town_01",
            "灰石镇",
            "城镇",
            "graystone",
            countryId,
            "欢迎来到灰石镇。",
            "补给稳定",
            new Vector2I(2, 2),
            "hero",
            new[]
            {
                new SettlementMemberOptionData("hero", "主角", "上阵", true, 18, 6),
            },
            new[]
            {
                new SettlementFacilityEntryData(
                    "facility_warehouse",
                    "仓库",
                    "storage",
                    "warehouse"
                ),
            },
            new[]
            {
                new SettlementResidentEntryData(
                    "npc_warehouse_keeper",
                    "仓库管理员",
                    "warehouse",
                    "仓库"
                ),
            },
            new[] { service ?? MakeServiceEntry() }
        );

    private async Task TestShopWindowConfirmationFlow()
    {
        ShopWindow window = await CreateShopWindow();
        SettlementContractBoardActionRequest capturedRequest = default;
        bool actionRequested = false;
        window.ContractActionRequested += request =>
        {
            actionRequested = true;
            capturedRequest = request;
        };

        window.ShowShop(MakeConfirmationContractWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "带 pending_confirmation 的 payload 应正常显示 ShopWindow。");
        _test.Eq(window.confirm_button.Text, "确认", "确认面板应把确认按钮文案改为 确认。");
        _test.Eq(window.cancel_button.Text, "返回", "确认面板应把取消按钮文案改为 返回。");
        _test.True(
            window.details_label.Text.Contains("确认要接取这个契约吗？"),
            "确认面板应显示 pending_confirmation_text。"
        );

        window.confirm_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(actionRequested, "确认面板下再次点击确认应发射 typed 契约请求。");
        _test.Eq(
            capturedRequest.Action.SettlementId.ToString(),
            "graystone_town_01",
            "typed 契约请求应保留 settlement id。"
        );
        _test.Eq(
            capturedRequest.Action.ActionId.ToString(),
            "service:contract_board",
            "typed 契约请求应保留 action id。"
        );
        _test.Eq(
            capturedRequest.QuestId.ToString(),
            "contract_confirmation_quest",
            "typed 契约请求应保留稳定 quest id。"
        );
        _test.True(capturedRequest.ConfirmAccept, "确认后的请求应带 confirm_accept。");
        _test.False(window.Visible, "确认接取后应隐藏窗口。");

        actionRequested = false;
        capturedRequest = default;
        window.ShowShop(MakeConfirmationContractWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        window.cancel_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(actionRequested, "确认面板下点击取消不应发射 typed 契约请求。");
        _test.True(window.Visible, "确认面板下点击取消应保持窗口可见。");
        _test.Eq(window.confirm_button.Text, "确认操作", "取消后确认按钮应恢复原始文案。");
        _test.Eq(window.cancel_button.Text, "返回据点", "取消后取消按钮应恢复原始文案。");
        _test.False(
            window.details_label.Text.Contains("确认要接取这个契约吗？"),
            "取消后详情文本应恢复条目详情。"
        );

        await DisposeWindow(window);
    }

    private async Task TestForgeWindowRaisesTypedCSharpEvent()
    {
        ShopWindow window = await CreateShopWindow();
        ForgeActionRequest capturedRequest = default;
        bool typedEventRaised = false;
        bool shopEventRaised = false;
        window.ForgeActionRequested += request =>
        {
            typedEventRaised = true;
            capturedRequest = request;
        };
        window.ShopActionRequested += _ => shopEventRaised = true;

        window.ShowShop(MakeForgeWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "正式 forge payload 应正常显示 ShopWindow。");
        _test.False(window.confirm_button.Disabled, "可用配方应允许提交。");

        window.confirm_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(typedEventRaised, "forge 提交应触发强类型 C# 事件。");
        _test.False(shopEventRaised, "forge 提交不应触发商店条目事件。");
        _test.Eq(
            capturedRequest.SettlementId.ToString(),
            "graystone_town_01",
            "typed forge request 应保留 settlement id。"
        );
        _test.Eq(
            capturedRequest.ServiceId.ToString(),
            "service:repair_gear",
            "typed forge request 应保留 service id。"
        );
        _test.Eq(
            capturedRequest.ActionId.ToString(),
            "service:repair_gear",
            "typed forge request 应保留 action id。"
        );
        _test.Eq(
            capturedRequest.MemberId.ToString(),
            "mage",
            "typed forge request 应保留当前成员。"
        );
        _test.Eq(
            capturedRequest.RecipeId.ToString(),
            "forge_militia_axe",
            "typed forge request 应保留 recipe id。"
        );
        _test.False(window.Visible, "提交 forge request 后应隐藏窗口。");

        await DisposeWindow(window);
    }

    private static SettlementServiceWindowData MakeConfirmationContractWindowData() =>
        new(
            "graystone_town_01",
            "service:contract_board",
            SettlementPanelKind.ContractBoard,
            "灰石镇任务板",
            "任务板 · 值守人员 · 契约",
            "可接契约清单",
            "契约稳定",
            new SettlementServiceWindowLabelsData(
                "确认操作",
                "返回据点",
                "可选契约",
                "任务板概况",
                "契约状态",
                "契约奖励",
                "契约说明",
                "执行成员",
                "状态：暂无契约",
                "奖励：无",
                "当前没有可查看契约。"
            ),
            false,
            "service_contract_board",
            "",
            "",
            "",
            "",
            "",
            new[]
            {
                new SettlementServiceWindowEntryData(
                    "contract_confirmation_quest",
                    "确认契约",
                    "目标：护送商队",
                    "需要玩家确认后才会接取。",
                    "状态：可接取",
                    "奖励：80 金",
                    true,
                    "",
                    new SettlementContractSelectionData("contract_confirmation_quest")
                ),
            },
            null,
            "",
            "",
            new SettlementServiceConfirmationData(
                "contract_confirmation_quest",
                "确认要接取这个契约吗？",
                SettlementSubmissionSource.ContractBoard
            )
        );

    private static SettlementServiceWindowData MakeForgeWindowData() =>
        new(
            "graystone_town_01",
            "service:repair_gear",
            SettlementPanelKind.Forge,
            "灰石镇工坊",
            "工坊：铁匠铺",
            "可选配方",
            "",
            new SettlementServiceWindowLabelsData(
                "锻造",
                "返回",
                "可选配方",
                "工坊概况",
                "配方状态",
                "材料消耗",
                "配方说明",
                "工坊成员",
                "状态：暂无配方",
                "材料：暂无配方",
                "当前没有可用配方。"
            ),
            false,
            "service_smith_forge",
            "",
            "",
            "",
            "",
            "",
            new[]
            {
                new SettlementServiceWindowEntryData(
                    "recipe:forge_militia_axe",
                    "民兵手斧",
                    "铁矿石 + 硬木板 -> 民兵手斧",
                    "锻造一把民兵手斧。",
                    "状态：可锻造",
                    "材料：铁矿石、硬木板",
                    true,
                    "",
                    new SettlementForgeSelectionData("forge_militia_axe")
                ),
            },
            new[] { new SettlementMemberOptionData("mage", "法师", "队员", false, 18, 12) },
            "mage",
            "mage",
            null
        );

    private static SettlementServiceWindowData MakeStagecoachWindowData() =>
        new(
            "graystone_town_01",
            "stagecoach:travel",
            SettlementPanelKind.Stagecoach,
            "灰石镇 · 驿站路线",
            "驿站：灰石镇  |  金币：120",
            "持有金币：120",
            "选择一个已访问据点并支付路费后即可启程。",
            new SettlementServiceWindowLabelsData(
                "确认出发",
                "返回据点",
                "可选路线",
                "行程概况",
                "行程状态",
                "行程费用",
                "行程说明",
                "出发成员",
                "状态：暂无路线",
                "费用：暂无路线",
                "当前没有可用路线。"
            ),
            true,
            "service_stagecoach",
            "",
            "",
            "",
            "",
            "",
            new[]
            {
                new SettlementServiceWindowEntryData(
                    "travel:north_outpost",
                    "北境哨站",
                    "哨站",
                    "哨站 ",
                    "状态：可出发",
                    "路费 12 金",
                    true,
                    "",
                    new SettlementStagecoachSelectionData("north_outpost")
                ),
            },
            new[] { new SettlementMemberOptionData("hero", "主角", "上阵", true, 18, 6) },
            "hero",
            "hero",
            null
        );

    private static SettlementServiceWindowData MakeShopWindowData() =>
        new(
            "graystone_town_01",
            "shop:trade",
            SettlementPanelKind.Shop,
            "灰石镇补给",
            "据点补给",
            "基础补给清单",
            "补给稳定",
            new SettlementServiceWindowLabelsData(
                "购买",
                "关闭",
                "商品",
                "摘要",
                "状态",
                "成本",
                "详情",
                "成员",
                "暂无状态",
                "暂无成本",
                "暂无详情"
            ),
            false,
            "service_basic_supply",
            "",
            "",
            "",
            "",
            "",
            new[]
            {
                new SettlementServiceWindowEntryData(
                    "buy:healing_herb",
                    "治疗药水",
                    "恢复少量生命",
                    "常见旅行补给。",
                    "现货",
                    "25 金",
                    true,
                    "",
                    new SettlementShopSelectionData(
                        SettlementShopActionKind.Buy,
                        "healing_herb",
                        "",
                        3
                    )
                ),
            },
            null,
            "",
            "",
            null
        );
}

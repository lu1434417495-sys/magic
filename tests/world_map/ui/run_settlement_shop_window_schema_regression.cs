using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
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
        await TestSettlementWindowAcceptsFormalStringKeys();
        await TestSettlementWindowRendersCountryIdWhenPresent();
        await TestSettlementWindowRejectsMissingCountryId();
        await TestSettlementWindowRejectsUnknownServiceFields();
        await TestSettlementWindowRejectsStringNameTopLevelFields();
        await TestSettlementWindowRejectsStringNameServiceFields();
        await TestSettlementWindowRejectsStringNameMemberOptionFields();
        await TestSettlementWindowRejectsUnknownPanelKind();
        await TestShopWindowAcceptsFormalStringKeys();
        await TestShopWindowRejectsStringNameTopLevelFields();
        await TestShopWindowRejectsStringNameEntryFields();
        await TestShopWindowRejectsStringNameMemberOptionFields();
        await TestShopWindowConfirmationFlow();
        await TestForgeWindowRaisesTypedCSharpEvent();
        RequestTestExit(_test.Finish("Settlement/shop window schema regression"));
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

    private async Task TestSettlementWindowAcceptsFormalStringKeys()
    {
        SettlementWindow window = await CreateSettlementWindow();
        window.ShowSettlement(MakeSettlementPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "SettlementWindow 应接受含空 country_id 的 formal string-key payload。");
        _test.Eq(window.services_container.GetChildCount(), 1, "SettlementWindow 应渲染一条正式 service entry。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsMissingCountryId()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        payload.Remove("country_id");
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow 应拒绝缺少 country_id 的 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "缺少 country_id 时不应继续渲染 settlement 服务。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRendersCountryIdWhenPresent()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        payload["country_id"] = "spring_republic";
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "SettlementWindow 应接受非空 country_id。");
        _test.True(
            window.meta_label.Text.Contains("国家 spring_republic"),
            "SettlementWindow 的元信息应暴露据点 country_id。"
        );
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsUnknownServiceFields()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        FirstDictionary(payload, "available_services")["window_kind"] = "shop";
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow service entry 含非正式字段时应拒绝整份 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "非正式 service 字段不应被忽略后继续渲染。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsStringNameTopLevelFields()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        payload["display_name"] = new StringName("灰石镇");
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow 顶层 string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "StringName-valued 顶层字段不应局部渲染 service。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsStringNameServiceFields()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        FirstDictionary(payload, "available_services")["facility_name"] = new StringName("仓库");
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow service string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "StringName-valued service 不应被跳过后继续渲染。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsStringNameMemberOptionFields()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        payload["member_options"] = new Godot.Collections.Array<GDictionary>
        {
            new()
            {
                ["member_id"] = new StringName("hero"),
                ["display_name"] = "主角",
                ["roster_role"] = "队长",
                ["is_leader"] = true,
                ["current_hp"] = 18,
                ["current_mp"] = 6,
            },
        };
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow member option id 为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "非法 member option 不应被忽略后继续渲染。");
        await DisposeWindow(window);
    }

    private async Task TestSettlementWindowRejectsUnknownPanelKind()
    {
        SettlementWindow window = await CreateSettlementWindow();
        GDictionary payload = MakeSettlementPayload();
        FirstDictionary(payload, "available_services")["panel_kind"] = "legacy_shop";
        window.ShowSettlement(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "SettlementWindow service panel_kind 不在正式枚举中时应拒绝整份 payload。");
        _test.Eq(window.services_container.GetChildCount(), 0, "未知 panel_kind 不应被保留到 service payload。");
        await DisposeWindow(window);
    }

    private async Task TestShopWindowAcceptsFormalStringKeys()
    {
        ShopWindow window = await CreateShopWindow();
        window.ShowShop(MakeShopPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "ShopWindow 应继续接受 formal string-key payload。");
        _test.Eq(window.entry_list.ItemCount, 1, "ShopWindow 应渲染一条正式 shop entry。");
        await DisposeWindow(window);
    }

    private async Task TestShopWindowRejectsStringNameTopLevelFields()
    {
        ShopWindow window = await CreateShopWindow();
        GDictionary payload = MakeShopPayload();
        payload["title"] = new StringName("灰石镇补给");
        window.ShowShop(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "ShopWindow 顶层 string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.entry_list.ItemCount, 0, "StringName-valued 顶层字段不应局部渲染 shop entry。");
        await DisposeWindow(window);
    }

    private async Task TestShopWindowRejectsStringNameEntryFields()
    {
        ShopWindow window = await CreateShopWindow();
        GDictionary payload = MakeShopPayload();
        FirstDictionary(payload, "entries")["display_name"] = new StringName("治疗药水");
        window.ShowShop(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "ShopWindow entry string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.entry_list.ItemCount, 0, "StringName-valued entry 不应被跳过后继续渲染。");
        await DisposeWindow(window);
    }

    private async Task TestShopWindowRejectsStringNameMemberOptionFields()
    {
        ShopWindow window = await CreateShopWindow();
        GDictionary payload = MakeShopPayload();
        payload["show_member_selector"] = true;
        payload["member_options"] = new Godot.Collections.Array<GDictionary>
        {
            new()
            {
                ["member_id"] = new StringName("hero"),
                ["display_name"] = "主角",
                ["roster_role"] = "队长",
                ["is_leader"] = true,
                ["current_hp"] = 18,
                ["current_mp"] = 6,
            },
        };
        window.ShowShop(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "ShopWindow member option id 为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.entry_list.ItemCount, 0, "非法 member option 不应被忽略后继续渲染。");
        await DisposeWindow(window);
    }

    private static GDictionary FirstDictionary(GDictionary payload, string key) =>
        ((Godot.Collections.Array<GDictionary>)payload[key])[0];

    private static GDictionary MakeSettlementPayload() =>
        new()
        {
            ["settlement_id"] = "graystone_town_01",
            ["display_name"] = "灰石镇",
            ["tier_name"] = "城镇",
            ["faction_id"] = "graystone",
            ["country_id"] = "",
            ["feedback_text"] = "欢迎来到灰石镇。",
            ["state_summary_text"] = "补给稳定",
            ["footprint_size"] = new Vector2I(2, 2),
            ["available_services"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["action_id"] = "service:warehouse",
                    ["facility_name"] = "仓库",
                    ["npc_name"] = "仓库管理员",
                    ["service_type"] = "warehouse",
                    ["interaction_script_id"] = "service_warehouse",
                    ["cost_label"] = "免费",
                    ["state_label"] = "可用",
                    ["summary_text"] = "打开共享仓库。",
                    ["is_enabled"] = true,
                    ["disabled_reason"] = "",
                },
            },
            ["facilities"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["display_name"] = "仓库",
                    ["slot_tag"] = "storage",
                    ["interaction_type"] = "warehouse",
                },
            },
            ["service_npcs"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["display_name"] = "仓库管理员",
                    ["service_type"] = "warehouse",
                    ["facility_name"] = "仓库",
                },
            },
        };

    private async Task TestShopWindowConfirmationFlow()
    {
        ShopWindow window = await CreateShopWindow();
        GDictionary capturedPayload = null;
        bool actionRequested = false;
        string capturedSettlementId = null;
        string capturedActionId = null;
        window.action_requested += (settlement_id, action_id, payload) =>
        {
            actionRequested = true;
            capturedSettlementId = settlement_id;
            capturedActionId = action_id;
            capturedPayload = payload;
        };

        window.ShowShop(MakeConfirmationShopPayload());
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

        _test.True(actionRequested, "确认面板下再次点击确认应发射 action_requested 信号。");
        _test.Eq(capturedSettlementId, "graystone_town_01", "payload 应保留 settlement_id。");
        _test.Eq(capturedActionId, "service:basic_supply", "payload 应保留 action_id。");
        _test.True(capturedPayload.ContainsKey("confirm_accept"), "确认后的 payload 应包含 confirm_accept。");
        _test.True((bool)capturedPayload["confirm_accept"], "confirm_accept 应为 true。");
        _test.False(window.Visible, "确认接取后应隐藏窗口。");

        actionRequested = false;
        capturedPayload = null;
        capturedSettlementId = null;
        capturedActionId = null;
        window.ShowShop(MakeConfirmationShopPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        window.cancel_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(actionRequested, "确认面板下点击取消不应发射 action_requested。");
        _test.True(window.Visible, "确认面板下点击取消应保持窗口可见。");
        _test.Eq(window.confirm_button.Text, "购买", "取消后确认按钮应恢复原始文案。");
        _test.Eq(window.cancel_button.Text, "关闭", "取消后取消按钮应恢复原始文案。");
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
        bool legacySignalRaised = false;
        window.ForgeActionRequested += request =>
        {
            typedEventRaised = true;
            capturedRequest = request;
        };
        window.action_requested += (_, _, _) => legacySignalRaised = true;

        window.ShowShop(MakeForgePayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "正式 forge payload 应正常显示 ShopWindow。");
        _test.False(window.confirm_button.Disabled, "可用配方应允许提交。");

        window.confirm_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(typedEventRaised, "forge 提交应触发强类型 C# 事件。");
        _test.False(legacySignalRaised, "forge 提交不应再触发 Dictionary action_requested signal。");
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

    private static GDictionary MakeConfirmationShopPayload()
    {
        GDictionary payload = MakeShopPayload().Duplicate(true);
        payload["pending_confirmation_quest_id"] = "contract_confirmation_quest";
        payload["pending_confirmation_text"] = "确认要接取这个契约吗？";
        payload["pending_confirmation_source"] = "contract_board";
        return payload;
    }

    private static GDictionary MakeForgePayload()
    {
        GDictionary payload = MakeShopPayload().Duplicate(true);
        payload["action_id"] = "service:repair_gear";
        payload["panel_kind"] = "forge";
        payload["interaction_script_id"] = "service_smith_forge";
        payload["default_member_id"] = "mage";
        payload["selected_member_id"] = "mage";
        payload["member_options"] = new Godot.Collections.Array<GDictionary>
        {
            new()
            {
                ["member_id"] = "mage",
                ["display_name"] = "法师",
                ["roster_role"] = "队员",
                ["is_leader"] = false,
                ["current_hp"] = 18,
                ["current_mp"] = 12,
            },
        };
        payload["entries"] = new Godot.Collections.Array<GDictionary>
        {
            new()
            {
                ["entry_id"] = "recipe:forge_militia_axe",
                ["recipe_id"] = "forge_militia_axe",
                ["display_name"] = "民兵手斧",
                ["summary_text"] = "铁矿石 + 硬木板 -> 民兵手斧",
                ["details_text"] = "锻造一把民兵手斧。",
                ["state_label"] = "状态：可锻造",
                ["cost_label"] = "材料：铁矿石、硬木板",
                ["is_enabled"] = true,
                ["disabled_reason"] = "",
            },
        };
        return payload;
    }

    private static GDictionary MakeShopPayload() =>
        new()
        {
            ["settlement_id"] = "graystone_town_01",
            ["action_id"] = "service:basic_supply",
            ["panel_kind"] = "shop",
            ["title"] = "灰石镇补给",
            ["meta"] = "据点补给",
            ["summary_text"] = "基础补给清单",
            ["confirm_label"] = "购买",
            ["cancel_label"] = "关闭",
            ["entry_title"] = "商品",
            ["summary_title"] = "摘要",
            ["state_title"] = "状态",
            ["cost_title"] = "成本",
            ["details_title"] = "详情",
            ["member_title"] = "成员",
            ["empty_state_label"] = "暂无状态",
            ["empty_cost_label"] = "暂无成本",
            ["empty_details_text"] = "暂无详情",
            ["state_summary_text"] = "补给稳定",
            ["show_member_selector"] = false,
            ["entries"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["entry_id"] = "buy_potion",
                    ["display_name"] = "治疗药水",
                    ["summary_text"] = "恢复少量生命",
                    ["details_text"] = "常见旅行补给。",
                    ["state_label"] = "现货",
                    ["cost_label"] = "25 金",
                    ["is_enabled"] = true,
                    ["disabled_reason"] = "",
                },
            },
        };
}

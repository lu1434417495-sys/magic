using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_settlement_shop_window_schema_regression : SceneTree
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
        await TestSettlementWindowRejectsUnknownServiceFields();
        await TestSettlementWindowRejectsStringNameTopLevelFields();
        await TestSettlementWindowRejectsStringNameServiceFields();
        await TestSettlementWindowRejectsStringNameMemberOptionFields();
        await TestSettlementWindowRejectsUnknownPanelKind();
        await TestShopWindowAcceptsFormalStringKeys();
        await TestShopWindowRejectsStringNameTopLevelFields();
        await TestShopWindowRejectsStringNameEntryFields();
        await TestShopWindowRejectsStringNameMemberOptionFields();
        Quit(_test.Finish("Settlement/shop window schema regression"));
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

        _test.True(window.Visible, "SettlementWindow 应继续接受 formal string-key payload。");
        _test.Eq(window.services_container.GetChildCount(), 1, "SettlementWindow 应渲染一条正式 service entry。");
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

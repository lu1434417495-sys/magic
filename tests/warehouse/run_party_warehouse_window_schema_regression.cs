using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_party_warehouse_window_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene WindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/party_warehouse_window.tscn"
    );

    public override async void _Initialize()
    {
        try
        {
            await TestPartyWarehouseWindowRendersFormalWindowData();
            await TestPartyWarehouseWindowUsesInstanceOnlyDiscardForEquipment();
            await TestPartyWarehouseWindowKeepsDetailsPlainText();
            await TestPartyWarehouseWindowToleratesInvalidIconPath();
            await TestPartyWarehouseWindowHidesOnUnavailableWindowData();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Party warehouse window schema regression"));
        }
    }

    private async Task<PartyWarehouseWindow> CreateWindow()
    {
        var window = WindowScene.Instantiate<PartyWarehouseWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return window;
    }

    private async Task DisposeWindow(Node window)
    {
        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestPartyWarehouseWindowRendersFormalWindowData()
    {
        PartyWarehouseWindow window = await CreateWindow();
        StringName discardedAllItemId = "";
        window.discard_all_requested += itemId => discardedAllItemId = itemId;
        window.ShowWarehouse(MakeWarehouseWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "共享仓库窗口应在 ShowWarehouse 后保持可见。");
        _test.Eq(window.title_label.Text, "共享仓库", "typed window data 的标题应渲染。");
        _test.Eq(window.summary_label.Text, "已用 1/12 格", "typed window data 的容量摘要应渲染。");
        _test.Eq(window.status_label.Text, "可用", "typed window data 的状态文案应渲染。");
        _test.Eq(window.stack_list.ItemCount, 1, "typed entries 应渲染一条仓库条目。");
        _test.Eq(window.target_member_selector.GetItemCount(), 1, "typed target_members 应渲染一个目标角色。");
        _test.Eq(window.discard_one_button.Text, "丢弃 1 件", "堆叠条目应提供按数量丢弃一件的操作。");
        _test.True(window.discard_all_button.Visible, "堆叠条目应显示丢弃全部同类操作。");
        _test.False(window.discard_all_button.Disabled, "有选中的堆叠条目时应允许丢弃全部同类。");

        window.discard_all_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.Eq(
            discardedAllItemId,
            new StringName("potion"),
            "堆叠条目的 discard-all 信号只应提交 item_id。"
        );
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowUsesInstanceOnlyDiscardForEquipment()
    {
        PartyWarehouseWindow window = await CreateWindow();
        StringName discardedItemId = "";
        StringName discardedInstanceId = "";
        bool discardAllRequested = false;
        window.discard_one_requested += (itemId, instanceId) =>
        {
            discardedItemId = itemId;
            discardedInstanceId = instanceId;
        };
        window.discard_all_requested += _ => discardAllRequested = true;

        window.ShowWarehouse(MakeEquipmentWarehouseWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.Eq(window.discard_one_button.Text, "丢弃此装备", "装备条目应明确按当前实例丢弃。");
        _test.False(window.discard_one_button.Disabled, "有选中的装备实例时应允许丢弃此装备。");
        _test.False(window.discard_all_button.Visible, "装备条目不应显示丢弃全部同类操作。");
        _test.True(window.discard_all_button.Disabled, "装备条目的 discard-all 控件应保持禁用。");
        _test.True(
            window.details_label.Text.Contains("装备实例条目"),
            "装备详情应明确当前条目代表一个独立实例。"
        );
        _test.True(
            window.details_label.Text.Contains("耐久：80"),
            "装备条目应展示实例耐久。"
        );

        window.discard_all_button.EmitSignal(BaseButton.SignalName.Pressed);
        window.discard_one_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(discardAllRequested, "即使外部触发隐藏按钮，装备条目也不应发出 discard-all 信号。");
        _test.Eq(discardedItemId, new StringName("bronze_sword"), "丢弃装备应提交装备 item_id。");
        _test.Eq(
            discardedInstanceId,
            new StringName("eq_warehouse_bronze_sword_001"),
            "丢弃装备应提交被选中的唯一 instance_id。"
        );
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowKeepsDetailsPlainText()
    {
        PartyWarehouseWindow window = await CreateWindow();
        window.ShowWarehouse(
            MakeWarehouseWindowData(
                MakeStackEntry(displayName: "[b]治疗药水[/b]", description: "[url]不要解释为链接[/url]")
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "仓库窗口应接受 typed window data。");
        _test.False(window.details_label.BbcodeEnabled, "仓库详情必须保持纯文本模式，内容字段不应被解释为 BBCode。");
        _test.True(
            window.details_label.Text.Contains("[b]治疗药水[/b]")
                && window.details_label.Text.Contains("[url]不要解释为链接[/url]"),
            "仓库详情应保留原始方括号文本，而不是按 BBCode 渲染或吞掉。"
        );
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowToleratesInvalidIconPath()
    {
        PartyWarehouseWindow window = await CreateWindow();
        window.ShowWarehouse(
            MakeWarehouseWindowData(
                MakeStackEntry(icon: "res://missing/warehouse/not_a_texture.png")
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "坏 icon 路径不应导致仓库窗口拒绝整份 typed window data。");
        _test.True(window.item_icon.Texture == null, "坏 icon 路径应降级为空贴图，而不是保留脏贴图。");
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowHidesOnUnavailableWindowData()
    {
        PartyWarehouseWindow window = await CreateWindow();

        window.ShowWarehouse(MakeWarehouseWindowData());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(window.Visible, "测试前置：可用 window data 应打开窗口。");

        window.ShowWarehouse(null);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "null window data 应关闭共享仓库窗口。");
        _test.Eq(window.stack_list.ItemCount, 0, "null window data 不应保留仓库条目。");

        window.ShowWarehouse(
            new WarehouseWindowData("共享仓库", "", "", "", WarehouseWindowSnapshot.Empty)
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.False(window.Visible, "不可用 snapshot 应关闭共享仓库窗口。");
        _test.Eq(
            window.target_member_selector.GetItemCount(),
            0,
            "不可用 snapshot 不应保留目标角色选项。"
        );

        await DisposeWindow(window);
    }

    private static WarehouseInventoryEntrySnapshot MakeStackEntry(
        string displayName = "治疗药水",
        string description = "恢复生命。",
        string icon = ""
    ) =>
        new()
        {
            ItemId = "potion",
            DisplayName = displayName,
            Description = description,
            Icon = icon,
            Quantity = 3,
            TotalQuantity = 3,
            IsStackable = true,
            StackLimit = 20,
            ItemCategory = "consumable",
            IsSkillBook = false,
            GrantedSkillId = "",
            GrantedSkillName = "",
            StorageMode = "stack",
            InstanceId = "",
            Rarity = 1,
            CurrentDurability = 0,
            HasEquipmentInstance = false,
        };

    private static WarehouseInventoryEntrySnapshot MakeEquipmentEntry() =>
        new()
        {
            ItemId = "bronze_sword",
            DisplayName = "青铜短剑",
            Description = "一把独立记录属性的短剑。",
            Icon = "",
            Quantity = 1,
            TotalQuantity = 2,
            IsStackable = false,
            StackLimit = 1,
            ItemCategory = "equipment",
            IsSkillBook = false,
            GrantedSkillId = "",
            GrantedSkillName = "",
            StorageMode = "instance",
            InstanceId = "eq_warehouse_bronze_sword_001",
            Rarity = 2,
            CurrentDurability = 80,
            HasEquipmentInstance = true,
        };

    private static WarehouseWindowData MakeWarehouseWindowData(
        WarehouseInventoryEntrySnapshot entry = null
    ) =>
        new(
            "共享仓库",
            "共享背包按堆栈占格，不计算重量。",
            "已用 1/12 格",
            "可用",
            new WarehouseWindowSnapshot(
                true,
                12,
                1,
                11,
                false,
                "队伍管理",
                "hero",
                new List<WarehouseTargetMemberSnapshot>
                {
                    new("hero", "主角", "leader"),
                },
                new List<WarehouseInventoryEntrySnapshot> { entry ?? MakeStackEntry() }
            )
        );

    private static WarehouseWindowData MakeEquipmentWarehouseWindowData() =>
        MakeWarehouseWindowData(MakeEquipmentEntry());
}

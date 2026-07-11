using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_party_warehouse_window_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene WindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/party_warehouse_window.tscn"
    );

    public override async void _Initialize()
    {
        await TestPartyWarehouseWindowRendersFormalWindowPayload();
        await TestPartyWarehouseWindowRejectsStringNameTopLevelFields();
        await TestPartyWarehouseWindowRejectsStringNameEntryFields();
        await TestPartyWarehouseWindowRejectsStringNameTargetMemberFields();
        RequestTestExit(_test.Finish("Party warehouse window schema regression"));
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

    private async Task TestPartyWarehouseWindowRendersFormalWindowPayload()
    {
        PartyWarehouseWindow window = await CreateWindow();
        window.ShowWarehouse(MakeWarehousePayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(window.Visible, "共享仓库窗口应在 ShowWarehouse 后保持可见。");
        _test.Eq(window.stack_list.ItemCount, 1, "formal entries 应渲染一条仓库条目。");
        _test.Eq(window.target_member_selector.GetItemCount(), 1, "formal target_members 应渲染一个目标角色。");
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowRejectsStringNameTopLevelFields()
    {
        PartyWarehouseWindow window = await CreateWindow();
        GDictionary payload = MakeWarehousePayload();
        payload["title"] = new StringName("共享仓库");
        window.ShowWarehouse(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "仓库窗口顶层 string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.stack_list.ItemCount, 0, "StringName-valued 顶层字段不应局部渲染仓库条目。");
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowRejectsStringNameEntryFields()
    {
        PartyWarehouseWindow window = await CreateWindow();
        GDictionary payload = MakeWarehousePayload();
        ((Godot.Collections.Array<GDictionary>)payload["entries"])[0]["display_name"] = new StringName("治疗药水");
        window.ShowWarehouse(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "仓库 entry string 字段为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.stack_list.ItemCount, 0, "StringName-valued entry 不应被跳过后继续渲染。");
        await DisposeWindow(window);
    }

    private async Task TestPartyWarehouseWindowRejectsStringNameTargetMemberFields()
    {
        PartyWarehouseWindow window = await CreateWindow();
        GDictionary payload = MakeWarehousePayload();
        ((Godot.Collections.Array<GDictionary>)payload["target_members"])[0]["member_id"] = new StringName("hero");
        window.ShowWarehouse(payload);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(window.Visible, "仓库 target member id 为 StringName 时应拒绝整份 payload。");
        _test.Eq(window.target_member_selector.GetItemCount(), 0, "非法 target member 不应被忽略后继续渲染。");
        await DisposeWindow(window);
    }

    private static GDictionary MakeWarehousePayload() =>
        new()
        {
            ["title"] = "共享仓库",
            ["meta"] = "共享背包按堆栈占格，不计算重量。",
            ["summary_text"] = "已用 1/12 格",
            ["status_text"] = "可用",
            ["default_target_member_id"] = "hero",
            ["entries"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["item_id"] = "potion",
                    ["instance_id"] = "",
                    ["display_name"] = "治疗药水",
                    ["description"] = "恢复生命。",
                    ["quantity"] = 3,
                    ["total_quantity"] = 3,
                    ["is_stackable"] = true,
                    ["stack_limit"] = 20,
                    ["item_category"] = "consumable",
                    ["granted_skill_id"] = "",
                    ["storage_mode"] = "stack",
                    ["icon"] = "",
                    ["rarity"] = 1,
                    ["current_durability"] = 0,
                    ["is_skill_book"] = false,
                    ["granted_skill_name"] = "",
                },
            },
            ["target_members"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["member_id"] = "hero",
                    ["display_name"] = "主角",
                },
            },
        };
}

using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_promotion_choice_window_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene WindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/promotion_choice_window.tscn"
    );

    public override async void _Initialize()
    {
        await TestPromotionChoiceWindowAcceptsFormalStringPayload();
        await TestPromotionChoiceWindowSubmitPreservesMemberId();
        await TestPromotionChoiceWindowEscapesBbcodeContent();
        await TestPromotionChoiceWindowRejectsStringNameStringFields();
        Quit(_test.Finish("Promotion choice window schema regression"));
    }

    private async Task<PromotionChoiceWindow> CreateWindow()
    {
        var window = WindowScene.Instantiate<PromotionChoiceWindow>();
        Root.AddChild(window);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return window;
    }

    private async Task DisposeWindow(Node window)
    {
        window.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestPromotionChoiceWindowAcceptsFormalStringPayload()
    {
        PromotionChoiceWindow window = await CreateWindow();
        window.ShowPromotion(MakeFormalPromotionPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var choiceCards = window.GetNode<HBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/ChoiceCards"
        );
        _test.True(window.Visible, "PromotionChoiceWindow 应继续接受 formal string payload。");
        _test.Eq(choiceCards.GetChildCount(), 1, "PromotionChoiceWindow 应渲染一条正式晋升选项。");
        await DisposeWindow(window);
    }

    private async Task TestPromotionChoiceWindowSubmitPreservesMemberId()
    {
        PromotionChoiceWindow window = await CreateWindow();
        StringName submittedMemberId = "";
        StringName submittedProfessionId = "";
        GDictionary submittedSelection = null;
        window.choice_submitted += (memberId, professionId, selection) =>
        {
            submittedMemberId = memberId;
            submittedProfessionId = professionId;
            submittedSelection = selection;
        };

        window.ShowPromotion(MakeFormalPromotionPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var confirmButton = window.GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton"
        );
        confirmButton.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.Eq(
            submittedMemberId,
            new StringName("hero"),
            "PromotionChoiceWindow 确认后应提交原始 member_id，而不是 HideWindow 清空后的空值。"
        );
        _test.Eq(
            submittedProfessionId,
            new StringName("warrior"),
            "PromotionChoiceWindow 确认后应提交被选中的 profession_id。"
        );
        _test.True(submittedSelection != null, "PromotionChoiceWindow 确认后应提交 selection payload。");
        _test.False(window.Visible, "PromotionChoiceWindow 确认后应关闭窗口。");
        await DisposeWindow(window);
    }

    private async Task TestPromotionChoiceWindowEscapesBbcodeContent()
    {
        PromotionChoiceWindow window = await CreateWindow();
        window.ShowPromotion(MakeBbcodePromotionPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var detailsLabel = window.GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel"
        );
        string detailText = detailsLabel.Text;
        _test.True(
            detailText.Contains("[lb]b]伪装战士[lb]/b]"),
            "PromotionChoiceWindow 应转义职业名中的 BBCode 标签。"
        );
        _test.True(
            detailText.Contains("[lb]color=red]危险说明[lb]/color]"),
            "PromotionChoiceWindow 应转义描述中的 BBCode 标签。"
        );
        _test.True(
            detailText.Contains("[lb]url=https://example.invalid]不要点击[lb]/url]"),
            "PromotionChoiceWindow 应转义说明中的 BBCode 标签。"
        );
        _test.False(
            detailText.Contains("[b]伪装战士[/b]"),
            "PromotionChoiceWindow 不应让内容字段注入原始 BBCode。"
        );
        await DisposeWindow(window);
    }

    private async Task TestPromotionChoiceWindowRejectsStringNameStringFields()
    {
        PromotionChoiceWindow window = await CreateWindow();
        window.ShowPromotion(MakeStringNamePromotionPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var choiceCards = window.GetNode<HBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/ChoiceCards"
        );
        _test.False(window.Visible, "PromotionChoiceWindow 应拒绝 StringName-valued prompt。");
        _test.Eq(choiceCards.GetChildCount(), 0, "StringName-valued prompt 不应局部渲染晋升选项。");
        await DisposeWindow(window);
    }

    private static GDictionary MakeFormalPromotionPayload() =>
        new()
        {
            ["member_id"] = "hero",
            ["member_name"] = "主角",
            ["choices"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["profession_id"] = "warrior",
                    ["display_name"] = "战士",
                    ["summary"] = "Rank 1",
                    ["description"] = "获得更稳定的前排姿态。",
                    ["granted_skill_ids"] = new Godot.Collections.Array<string> { "slash", "guard" },
                    ["selection_hint"] = "确认后将在战斗中立即生效。",
                    ["selection"] = new GDictionary(),
                },
            },
        };

    private static GDictionary MakeBbcodePromotionPayload() =>
        new()
        {
            ["member_id"] = "hero",
            ["member_name"] = "主角",
            ["choices"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["profession_id"] = "warrior",
                    ["display_name"] = "[b]伪装战士[/b]",
                    ["summary"] = "Rank 1",
                    ["description"] = "[color=red]危险说明[/color]",
                    ["granted_skill_ids"] = new Godot.Collections.Array<string> { "[wave]slash[/wave]" },
                    ["selection_hint"] = "[url=https://example.invalid]不要点击[/url]",
                    ["selection"] = new GDictionary(),
                },
            },
        };

    private static GDictionary MakeStringNamePromotionPayload() =>
        new()
        {
            ["member_id"] = new StringName("hero"),
            ["member_name"] = new StringName("主角"),
            ["choices"] = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["profession_id"] = new StringName("warrior"),
                    ["display_name"] = new StringName("字符串战士"),
                    ["summary"] = new StringName("Rank 1"),
                    ["description"] = new StringName("StringName 技能摘要"),
                    ["granted_skill_ids"] = new Godot.Collections.Array<StringName> { "slash" },
                    ["selection_hint"] = new StringName("StringName hint"),
                    ["selection"] = new GDictionary(),
                },
            },
        };
}

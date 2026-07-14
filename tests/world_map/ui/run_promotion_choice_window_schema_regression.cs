using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_promotion_choice_window_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene WindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/promotion_choice_window.tscn"
    );

    public override async void _Initialize()
    {
        await TestPromotionChoiceWindowAcceptsFormalStringPayload();
        await TestPromotionChoiceWindowRendersBbcodeShapedContentLiterally();
        await TestPromotionChoiceWindowSubmitPreservesMemberId();
        await TestPromotionChoiceWindowRejectsStringNameStringFields();
        RequestTestExit(_test.Finish("Promotion choice window schema regression"));
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
        bool submittedSelectionWasValid = false;
        string submittedSelectionMode = "";
        window.choice_submitted += (memberId, professionId, selection) =>
        {
            submittedMemberId = memberId;
            submittedProfessionId = professionId;
            submittedSelectionWasValid =
                selection != null
                && selection.ContainsKey("mode")
                && selection["mode"].VariantType == Variant.Type.String;
            submittedSelectionMode = submittedSelectionWasValid
                ? selection["mode"].AsString()
                : "";
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
        _test.True(
            submittedSelectionWasValid,
            "PromotionChoiceWindow 确认后应同步提交 selection payload。"
        );
        _test.Eq(
            submittedSelectionMode,
            "frontline",
            "PromotionChoiceWindow 应保持 selection payload 的字段语义。"
        );
        _test.False(window.Visible, "PromotionChoiceWindow 确认后应关闭窗口。");
        await DisposeWindow(window);
    }

    private async Task TestPromotionChoiceWindowRendersBbcodeShapedContentLiterally()
    {
        PromotionChoiceWindow window = await CreateWindow();
        window.ShowPromotion(MakeBbcodeShapedPromotionPayload());
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        var detailsLabel = window.GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel"
        );
        _test.True(
            detailsLabel.BbcodeEnabled,
            "晋升详情应继续启用受控 BBCode 样式，而不是通过关闭格式规避动态文本解析。"
        );
        string parsedText = detailsLabel.GetParsedText();
        _test.True(
            parsedText.Contains("[b]守卫[/b]"),
            "晋升名称中的 BBCode 形态文本应按字面显示。"
        );
        _test.True(
            parsedText.Contains("[color=red]仍是描述[/color]"),
            "晋升描述中的 BBCode 形态文本应按字面显示。"
        );
        _test.True(
            parsedText.Contains("[url=confirm]确认[/url]"),
            "晋升提示中的 BBCode 形态文本应按字面显示。"
        );
        _test.True(
            parsedText.Contains("slash[b]"),
            "授予技能文本中的方括号内容也不应被当作 BBCode。"
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

    private static IReadOnlyDictionary<string, object> MakeFormalPromotionPayload() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = "hero",
            ["member_name"] = "主角",
            ["choices"] = new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["profession_id"] = "warrior",
                    ["display_name"] = "战士",
                    ["summary"] = "Rank 1",
                    ["description"] = "获得更稳定的前排姿态。",
                    ["granted_skill_ids"] = new List<object> { "slash", "guard" },
                    ["selection_hint"] = "确认后将在战斗中立即生效。",
                    ["selection"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["mode"] = "frontline",
                    },
                },
            },
        };

    private static IReadOnlyDictionary<string, object> MakeStringNamePromotionPayload() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = new StringName("hero"),
            ["member_name"] = new StringName("主角"),
            ["choices"] = new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["profession_id"] = new StringName("warrior"),
                    ["display_name"] = new StringName("字符串战士"),
                    ["summary"] = new StringName("Rank 1"),
                    ["description"] = new StringName("StringName 技能摘要"),
                    ["granted_skill_ids"] = new List<object>
                    {
                        new StringName("slash"),
                    },
                    ["selection_hint"] = new StringName("StringName hint"),
                    ["selection"] = new Dictionary<string, object>(StringComparer.Ordinal),
                },
            },
        };

    private static IReadOnlyDictionary<string, object> MakeBbcodeShapedPromotionPayload() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = "hero",
            ["member_name"] = "主角",
            ["choices"] = new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["profession_id"] = "guardian",
                    ["display_name"] = "[b]守卫[/b]",
                    ["summary"] = "Rank 1",
                    ["description"] = "[color=red]仍是描述[/color]",
                    ["granted_skill_ids"] = new List<object> { "slash[b]" },
                    ["selection_hint"] = "[url=confirm]确认[/url]",
                    ["selection"] = new Dictionary<string, object>(StringComparer.Ordinal),
                },
            },
        };
}

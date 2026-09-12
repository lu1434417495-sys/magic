using System;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_creation_window_payload_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene CharacterCreationScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_creation_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            await TestConfirmationPayloadIncludesRolledAttributesAndIdentity();
            await TestIdentityCardsPopulate();
        }
        catch (Exception ex)
        {
            _test.Fail(ex.ToString());
        }

        RequestTestExit(_test.Finish("Character creation window payload regression"));
    }

    private async Task TestConfirmationPayloadIncludesRolledAttributesAndIdentity()
    {
        CharacterCreationWindow window = CharacterCreationScene.Instantiate<CharacterCreationWindow>();
        Root.AddChild(window);
        await ToSignal(this, SignalName.ProcessFrame);

        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(gameSession != null, "建卡窗口回归应能取得 canonical GameSession。");
        GameContentCatalog contentCatalog = gameSession?.GetContentCatalogTyped();
        _test.True(contentCatalog != null, "建卡窗口回归应注入当前 process content catalog。");
        if (contentCatalog != null)
            window.SetContentCatalog(contentCatalog);

        GDictionary capturedPayload = null;
        window.character_confirmed += payload => capturedPayload = payload;

        window.ShowWindow();
        _test.True(window.name_phase.IsVisibleInTree(), "创建初始阶段应只展示姓名输入。");
        AssertUnrevealedContentHidden(window, "属性", "种族", "亚种", "年龄", "身份");
        window.name_input.Text = "Payload Probe";
        window._on_name_confirmed();
        AssertUnrevealedContentHidden(window, "种族", "亚种", "年龄", "身份");
        window._enter_race_phase();
        window._enter_age_phase();
        window._enter_identity_options_phase();
        window._on_confirm_pressed();

        _test.True(capturedPayload != null, "建卡确认应发出 payload。");
        _test.Eq(DictString(capturedPayload, "display_name"), "Payload Probe", "建卡 payload 应保留输入角色名。");
        foreach (string attributeId in new[]
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        })
        {
            _test.True(
                DictInt(capturedPayload, attributeId) >= 4,
                $"建卡 payload 应包含有效属性：{attributeId}。"
            );
        }
        _test.True(DictStringName(capturedPayload, "race_id") != "", "建卡 payload 应包含 race_id。");
        _test.True(DictStringName(capturedPayload, "subrace_id") != "", "建卡 payload 应包含 subrace_id。");
        _test.True(DictStringName(capturedPayload, "natural_age_stage_id") != "", "建卡 payload 应包含 age stage。");

        window.QueueFree();
        await ToSignal(this, SignalName.ProcessFrame);
    }

    private async Task TestIdentityCardsPopulate()
    {
        CharacterCreationWindow window = CharacterCreationScene.Instantiate<CharacterCreationWindow>();
        Root.AddChild(window);
        await ToSignal(this, SignalName.ProcessFrame);

        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        GameContentCatalog contentCatalog = gameSession?.GetContentCatalogTyped();
        if (contentCatalog != null)
            window.SetContentCatalog(contentCatalog);

        window.ShowWindow();
        window.name_input.Text = "Card Probe";
        window._on_name_confirmed();
        window._enter_race_phase();
        _test.True(
            window.race_card_flow.GetChildCount() > 0,
            "进入种族阶段后应渲染至少一张种族卡片。"
        );
        for (int i = 0; i < 5; i++) await ToSignal(this, SignalName.ProcessFrame);
        Control panel = window.GetNode<Control>("CenterContainer/Panel");
        _test.True(panel.GetGlobalRect().Position.Y >= 0 && panel.GetGlobalRect().End.Y <= window.Size.Y + 1,
            "完整种族列表不应把建卡弹窗撑出屏幕。");
        _test.True(window.race_next_button.GetGlobalRect().End.Y <= window.Size.Y,
            "种族页下一步按钮必须始终留在屏幕内。");
        var scroll = window.race_phase.GetNode<ScrollContainer>("BodyScroll");
        _test.True(scroll.Size.Y > 100, "种族卡片区应获得可滚动的有效高度。");

        var input = new E2eInputDriver(this, new E2eWait(this));
        var firstChoice = window.race_card_flow.GetChild<PanelContainer>(0).GetNode<Button>("Select");
        StringName originalRace = window._selected_race_id;
        await input.ClickAsync(firstChoice);
        StringName clickedRace = window._selected_race_id;
        _test.True(clickedRace != originalRace, "点击种族选项应穿过展示文字并切换正式身份选择。");
        await input.ClickAsync(window.race_card_flow.GetChild<PanelContainer>(0).GetNode<Button>("Select"));
        _test.Eq(window._selected_race_id, clickedRace, "再次点击已选种族不应取消选择。");

        var secondChoice = window.race_card_flow.GetChild<PanelContainer>(1).GetNode<Button>("Select");
        secondChoice.GrabFocus();
        await input.TapKeyAsync(Key.Space);
        _test.True(window._selected_race_id != clickedRace, "键盘确认应能切换种族选项。");
        _test.True(window._build_selected_identity_payload().Count > 0,
            "鼠标和键盘选择后仍应生成有效的创建身份 payload。");

        window._enter_age_phase();
        _test.True(
            window.age_stage_card_flow.GetChildCount() > 0,
            "进入年龄阶段后应渲染至少一张阶段卡片。"
        );

        int cancelledCount = 0;
        window.cancelled += () => cancelledCount++;
        for (int i = 0; i < 3; i++) await ToSignal(this, SignalName.ProcessFrame);
        await input.ClickAsync(window.age_cancel_button);
        _test.Eq(cancelledCount, 1, "当前阶段的放弃创建应发出一次取消信号。");
        _test.False(window.Visible, "放弃创建后应关闭创建界面。");

        window.QueueFree();
        await ToSignal(this, SignalName.ProcessFrame);
    }

    private void AssertUnrevealedContentHidden(CharacterCreationWindow window, params string[] hiddenTopics)
    {
        foreach (Node node in window.FindChildren("*", "Control", true, false))
        {
            if (node is not Control control || !control.IsVisibleInTree())
                continue;
            string text = control switch { Label label => label.Text, Button button => button.Text, _ => "" };
            _test.False(text.Contains("上一步") || text.Contains("下一步"),
                "创建界面不应显示向导式上一步 / 下一步导航。");
            foreach (string topic in hiddenTopics)
                _test.False(text.Contains(topic), $"确认当前阶段之前不能预告后续内容：{topic}。");
        }
    }

    private static string DictString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static int DictInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            _ => 0,
        };
    }
}

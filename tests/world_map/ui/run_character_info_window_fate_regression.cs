using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_character_info_window_fate_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        try
        {
            await TestCharacterInfoWindowRendersFateSectionHappyPath();
            await TestCharacterInfoWindowRendersUpperBoundFateHints();
            await TestCharacterInfoWindowHidesOnEmptyContext();
            await TestCharacterInfoWindowSkipsDuplicateFateSection();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("CharacterInfoWindow fate regression"));
        }
    }

    private async Task<CharacterInfoWindow> CreateWindow()
    {
        var window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ProcessFrames(1);
        return window;
    }

    private async Task TestCharacterInfoWindowRendersFateSectionHappyPath()
    {
        CharacterInfoWindow window = await CreateWindow();
        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle,
                "黑冠见证者",
                "战斗单位  |  玩家前排",
                "战斗单位",
                BaseSections(),
                unitId: "unit_1",
                fate: new GameRuntimeCharacterInfoFate(
                    hiddenLuckAtBirth: 7,
                    faithLuckBonus: -13,
                    fortuneMarked: 1,
                    doomMarked: 1,
                    doomAuthority: 4
                )
            )
        );
        await ProcessFrames(1);

        _test.True(window.Visible, "完整 typed context 应打开人物信息窗。");
        _test.Eq(window.title_label.Text, "黑冠见证者", "typed context 的 display_name 应直接渲染。");
        _test.Eq(window.sections_container.GetChildCount(), 2, "显式 sections + fate 应渲染为两个段落。");
        List<string> renderedTexts = CollectLabelTexts(window.sections_container);
        AssertHas(renderedTexts, "命运", "typed fate 应追加命运段落标题。");
        AssertHas(renderedTexts, "生来暗运：", "应渲染生来暗运标签。");
        AssertHas(renderedTexts, "+7", "应按原值显示 HiddenLuckAtBirth=+7。");
        AssertHas(renderedTexts, "信仰赐运：", "应渲染信仰赐运标签。");
        AssertHas(renderedTexts, "-13", "应渲染 FaithLuckBonus。");
        AssertHas(renderedTexts, "有效运势：", "应渲染有效运势标签。");
        AssertHas(renderedTexts, "-6", "EffectiveLuck 应由 typed owner 截断到 -6 下限。");
        AssertHas(renderedTexts, "1（已获福印）", "应渲染 FortuneMarked。");
        AssertHas(renderedTexts, "1（已见黑兆）", "应渲染 DoomMarked。");
        AssertHas(renderedTexts, "厄权：", "DoomAuthority > 0 时应显示厄权标签。");
        AssertHas(renderedTexts, "4 级", "DoomAuthority > 0 时应显示厄权值。");
        AssertHas(
            renderedTexts,
            "生来暗运已处于极端正运档，界面会按原值保留该刻印。",
            "HiddenLuckAtBirth=+7 时应给出极端正运提示。"
        );
        AssertHas(
            renderedTexts,
            "有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。",
            "EffectiveLuck=-6 时应给出下限提示。"
        );

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowRendersUpperBoundFateHints()
    {
        CharacterInfoWindow window = await CreateWindow();
        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle,
                "福印旅人",
                "战斗单位",
                "战斗单位",
                BaseSections(),
                fate: new GameRuntimeCharacterInfoFate(
                    hiddenLuckAtBirth: -6,
                    faithLuckBonus: 13,
                    fortuneMarked: 0,
                    doomMarked: 0,
                    doomAuthority: 0
                )
            )
        );
        await ProcessFrames(1);

        List<string> renderedTexts = CollectLabelTexts(window.sections_container);
        AssertHas(renderedTexts, "+7", "EffectiveLuck 应由 typed owner 截断到 +7 上限。");
        AssertHas(renderedTexts, "0（未获福印）", "FortuneMarked=0 应显示未获福印。");
        AssertHas(renderedTexts, "0（未见黑兆）", "DoomMarked=0 应显示未见黑兆。");
        _test.False(
            renderedTexts.Contains("厄权："),
            "DoomAuthority=0 时不应显示厄权条目。"
        );
        AssertHas(
            renderedTexts,
            "生来暗运已压到最深坏运档，这类角色更容易撞进命运事件的极端分支。",
            "HiddenLuckAtBirth=-6 时应给出最深坏运提示。"
        );
        AssertHas(
            renderedTexts,
            "有效运势已到 +7 上限：高位大成功威胁区会吃满，但随机掉落仍只按 +5 结算。",
            "EffectiveLuck=+7 时应给出上限提示。"
        );

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowHidesOnEmptyContext()
    {
        CharacterInfoWindow window = await CreateWindow();

        window.ShowCharacter(null);
        await ProcessFrames(1);
        _test.False(window.Visible, "null context 应关闭人物信息窗。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "null context 不应渲染任何 section。");

        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.World,
                "无段落旅人",
                "世界 NPC",
                "可见提示单位",
                System.Array.Empty<GameRuntimeCharacterInfoSection>()
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "无 section 的 context 应关闭人物信息窗。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "无 section 时不应渲染默认 section。");

        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.World,
                "   ",
                "世界 NPC",
                "可见提示单位",
                BaseSections()
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "空白 display_name 的 context 应关闭人物信息窗。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "空白 display_name 不应继续渲染 section。");

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowSkipsDuplicateFateSection()
    {
        CharacterInfoWindow window = await CreateWindow();
        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle,
                "双命运旅人",
                "战斗单位",
                "战斗单位",
                new[]
                {
                    new GameRuntimeCharacterInfoSection(
                        "命运",
                        new[] { GameRuntimeCharacterInfoEntry.Pair("生来暗运", "+1") }
                    ),
                },
                fate: new GameRuntimeCharacterInfoFate(1, 0, 0, 0, 0)
            )
        );
        await ProcessFrames(1);

        _test.True(window.Visible, "已自带命运段落的 context 仍应正常展示。");
        _test.Eq(
            window.sections_container.GetChildCount(),
            1,
            "context 已含命运段落时不应再追加第二个命运段落。"
        );

        await DisposeNode(window);
    }

    private static IReadOnlyList<GameRuntimeCharacterInfoSection> BaseSections()
    {
        return new[]
        {
            new GameRuntimeCharacterInfoSection(
                "基础概览",
                new[] { GameRuntimeCharacterInfoEntry.Pair("职业", "见厄者") }
            ),
        };
    }

    private void AssertHas(List<string> texts, string expected, string message)
    {
        _test.True(texts.Contains(expected), message);
    }

    private static List<string> CollectLabelTexts(Node node)
    {
        var texts = new List<string>();
        if (node is Label label)
            texts.Add(label.Text);
        foreach (Node child in node.GetChildren())
            texts.AddRange(CollectLabelTexts(child));
        return texts;
    }

    private async Task DisposeNode(Node node)
    {
        node.QueueFree();
        await ProcessFrames(1);
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}

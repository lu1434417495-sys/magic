using System.Threading.Tasks;
using Godot;

public partial class run_character_info_payload_schema_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        try
        {
            await TestRuntimeCharacterInfoContextRendersRuntimeIdentityFields();
            await TestEquipmentTooltipEntryRendersAsHoverTooltip();
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Character info payload schema regression"));
        }
    }

    private async Task TestRuntimeCharacterInfoContextRendersRuntimeIdentityFields()
    {
        CharacterInfoWindow window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ToSignal(this, SignalName.ProcessFrame);

        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle,
                "Hero",
                "战斗单位",
                "玩家",
                new[]
                {
                    new GameRuntimeCharacterInfoSection(
                        "基础概览",
                        new[] { GameRuntimeCharacterInfoEntry.Pair("职业", "战士") }
                    ),
                },
                unitId: "unit_1",
                memberId: "hero"
            )
        );
        await ToSignal(this, SignalName.ProcessFrame);

        _test.True(window.Visible, "带 source/unit_id/member_id 的 typed context 应被窗口接受。");
        _test.Eq(window.title_label.Text, "Hero", "typed context 的 display_name 应渲染。");
        _test.Eq(window.meta_label.Text, "战斗单位", "typed context 的 meta_label 应渲染。");
        _test.Eq(window.status_label.Text, "玩家", "typed context 的 status_label 应渲染。");
        _test.True(window.status_block.Visible, "非空 status_label 应展开状态块。");
        _test.Eq(window.sections_container.GetChildCount(), 1, "typed context 的 sections 应渲染。");

        window.QueueFree();
        await ToSignal(this, SignalName.ProcessFrame);
    }

    private async Task TestEquipmentTooltipEntryRendersAsHoverTooltip()
    {
        CharacterInfoWindow window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ToSignal(this, SignalName.ProcessFrame);

        const string tooltipText = "【屠龙】对 dragon 额外3D6火焰。";
        window.ShowCharacter(
            new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle,
                "Hero",
                "战斗单位",
                "玩家",
                new[]
                {
                    new GameRuntimeCharacterInfoSection(
                        "装备",
                        new[]
                        {
                            GameRuntimeCharacterInfoEntry.Pair(
                                "主手",
                                "龙骨断剑 ⓘ",
                                tooltipText
                            ),
                        }
                    ),
                },
                unitId: "unit_1"
            )
        );
        await ToSignal(this, SignalName.ProcessFrame);

        _test.Eq(
            window.sections_container.GetChildCount(),
            1,
            "带 tooltip 的装备条目所在 section 应被渲染。"
        );
        _test.True(
            FindTooltipText(window.sections_container, tooltipText),
            "装备详情应作为鼠标悬停 tooltip 挂到渲染节点上，而不是内联铺开。"
        );

        window.QueueFree();
        await ToSignal(this, SignalName.ProcessFrame);
    }

    private static bool FindTooltipText(Node node, string expected)
    {
        if (node is Control control && control.TooltipText == expected)
            return true;
        foreach (Node child in node.GetChildren())
        {
            if (FindTooltipText(child, expected))
                return true;
        }
        return false;
    }
}

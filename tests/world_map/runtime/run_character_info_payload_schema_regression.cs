using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_info_payload_schema_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestRuntimeCharacterInfoPayloadAllowsRuntimeIdentityKeys();
        await TestEquipmentTooltipEntryRendersAsHoverTooltip();
        RequestTestExit(_test.Finish("Character info payload schema regression"));
    }

    private async Task TestRuntimeCharacterInfoPayloadAllowsRuntimeIdentityKeys()
    {
        CharacterInfoWindow window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ToSignal(this, SignalName.ProcessFrame);

        window.ShowCharacter(
            new GDictionary
            {
                ["source"] = "battle",
                ["unit_id"] = "unit_1",
                ["member_id"] = "hero",
                ["display_name"] = "Hero",
                ["meta_label"] = "战斗单位",
                ["status_label"] = "玩家",
                ["sections"] = new GArray
                {
                    new GDictionary
                    {
                        ["title"] = "基础概览",
                        ["entries"] = new GArray
                        {
                            new GDictionary { ["label"] = "职业", ["value"] = "战士" },
                        },
                    },
                },
            }
        );
        await ToSignal(this, SignalName.ProcessFrame);

        _test.True(window.Visible, "runtime character info payload with source/unit_id/member_id should be accepted.");
        _test.Eq(window.title_label.Text, "Hero", "runtime payload display_name should render.");
        _test.Eq(window.sections_container.GetChildCount(), 1, "runtime payload sections should render.");

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
            new GDictionary
            {
                ["source"] = "battle",
                ["unit_id"] = "unit_1",
                ["display_name"] = "Hero",
                ["meta_label"] = "战斗单位",
                ["status_label"] = "玩家",
                ["sections"] = new GArray
                {
                    new GDictionary
                    {
                        ["title"] = "装备",
                        ["entries"] = new GArray
                        {
                            new GDictionary
                            {
                                ["label"] = "主手",
                                ["value"] = "龙骨断剑 ⓘ",
                                ["tooltip"] = tooltipText,
                            },
                        },
                    },
                },
            }
        );
        await ToSignal(this, SignalName.ProcessFrame);

        _test.Eq(
            window.sections_container.GetChildCount(),
            1,
            "带 tooltip 的装备条目所在 section 应被接受并渲染，不应被严格解析丢弃。"
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

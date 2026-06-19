using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_info_payload_schema_regression : SceneTree
{
    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestRuntimeCharacterInfoPayloadAllowsRuntimeIdentityKeys();
        Quit(_test.Finish("Character info payload schema regression"));
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
}

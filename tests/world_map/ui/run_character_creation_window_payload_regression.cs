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

    public override async void _Initialize()
    {
        try
        {
            await TestConfirmationPayloadIncludesRolledAttributesAndIdentity();
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

        GDictionary capturedPayload = null;
        window.character_confirmed += payload => capturedPayload = payload;

        window.ShowWindow();
        window.name_input.Text = "Payload Probe";
        window._on_name_confirmed();
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

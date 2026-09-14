using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public static class SelectionCardBuilder
{
    private static readonly Color CardBgNormal = new(0.04f, 0.05f, 0.06f, 0.3f);
    private static readonly Color CardBgSelected = new(0.22f, 0.17f, 0.09f, 0.42f);
    private static readonly Color CardBorderNormal = new(0.56f, 0.46f, 0.31f, 0.3f);
    private static readonly Color CardBorderSelected = new(0.77f, 0.63f, 0.40f, 1.0f);
    private static readonly Color ColorTitle = new(0.86f, 0.74f, 0.52f, 1.0f);
    private static readonly Color ColorSummary = new(0.91f, 0.88f, 0.81f, 0.92f);
    private static readonly Color ColorChipHeader = new(0.69f, 0.68f, 0.62f, 0.85f);
    private static readonly Color ColorChip = new(0.87f, 0.84f, 0.77f, 1.0f);

    public static StyleBoxFlat MakeStyle(bool selected)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected ? CardBgSelected : CardBgNormal,
            BorderWidthLeft = selected ? 2 : 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 1,
            BorderColor = selected ? CardBorderSelected : CardBorderNormal,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
        };
        return style;
    }

    public static PanelContainer BuildCard(GDictionary spec)
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        card.AddThemeStyleboxOverride("panel", MakeStyle(false));

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        card.AddChild(margin);

        var vbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        string title = DictString(spec, "title", "");
        string summary = DictString(spec, "summary", "");
        string chipHeader = DictString(spec, "chip_header", "");
        var chipStrings = ExtractChipStrings(spec, "chips");

        vbox.AddChild(MakeLabel(title, ColorTitle, 24, false));

        if (!string.IsNullOrEmpty(summary))
            vbox.AddChild(MakeLabel(summary, ColorSummary, 14, true));

        var spacer = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        vbox.AddChild(spacer);

        if (chipStrings.Count > 0)
        {
            if (!string.IsNullOrEmpty(chipHeader))
                vbox.AddChild(MakeLabel(chipHeader, ColorChipHeader, 12, false));
            vbox.AddChild(MakeLabel(string.Join("  ·  ", chipStrings), ColorChip, 14, true));
        }

        return card;
    }

    private static Label MakeLabel(string text, Color color, int fontSize, bool autowrap)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Text = text };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (autowrap)
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static List<string> ExtractChipStrings(GDictionary spec, string key)
    {
        var results = new List<string>();
        if (spec == null || !spec.ContainsKey(key))
            return results;
        var chipsValue = spec[key];
        if (chipsValue.VariantType == Variant.Type.Array)
        {
            using GArray chips = chipsValue.AsGodotArray();
            foreach (var chip in chips)
                results.Add(chip.AsString());
        }
        else if (chipsValue.VariantType == Variant.Type.PackedStringArray)
        {
            foreach (string chip in chipsValue.AsStringArray())
                results.Add(chip);
        }
        return results;
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }
}

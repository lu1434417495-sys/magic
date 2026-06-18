using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class SelectionCardBuilder
{
    private static readonly Color CardBgNormal = new(0.10f, 0.13f, 0.20f, 0.94f);
    private static readonly Color CardBgSelected = new(0.18f, 0.16f, 0.10f, 0.98f);
    private static readonly Color CardBorderNormal = new(0.40f, 0.50f, 0.66f, 0.7f);
    private static readonly Color CardBorderSelected = new(0.95f, 0.78f, 0.32f, 1.0f);
    private static readonly Color ColorTitle = new(0.98f, 0.86f, 0.46f, 1.0f);
    private static readonly Color ColorSummary = new(0.85f, 0.92f, 1.0f, 0.92f);
    private static readonly Color ColorChipHeader = new(0.756f, 0.835f, 0.957f, 0.85f);
    private static readonly Color ColorChip = new(0.95f, 0.95f, 0.95f, 1.0f);

    public static StyleBoxFlat MakeStyle(bool selected)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected ? CardBgSelected : CardBgNormal,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = selected ? CardBorderSelected : CardBorderNormal,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomRight = 14,
            CornerRadiusBottomLeft = 14,
        };
        if (selected)
        {
            style.ShadowColor = new Color(0.95f, 0.78f, 0.32f, 0.35f);
            style.ShadowSize = 8;
        }
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
            foreach (var chip in chipsValue.AsGodotArray())
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

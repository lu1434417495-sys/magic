using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class CharacterInfoWindow : Control
{
    [Signal]
    public delegate void closedEventHandler();

    private const string FateSectionTitle = "命运";
    private static readonly string[] TopLevelKeys =
    {
        "display_name",
        "meta_label",
        "status_label",
        "sections",
        "fate",
    };
    private static readonly string[] SectionKeys = { "title", "entries" };
    private static readonly string[] PairEntryKeys = { "label", "value" };
    private static readonly string[] TextEntryKeys = { "text" };
    private static readonly string[] FateKeys =
    {
        "hidden_luck_at_birth",
        "faith_luck_bonus",
        "effective_luck",
        "fortune_marked",
        "doom_marked",
        "doom_authority",
        "has_misfortune",
    };

    public ColorRect shade;
    public Label title_label;
    public Label meta_label;
    public ScrollContainer sections_scroll;
    public VBoxContainer sections_container;
    public VBoxContainer status_block;
    public Label status_label;
    public Button close_button;

    private enum EntryKind
    {
        Pair,
        Text,
    }

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        meta_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        sections_scroll = GetNode<ScrollContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll"
        );
        sections_container = GetNode<VBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll/SectionsContainer"
        );
        status_block = GetNode<VBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock"
        );
        status_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock/StatusLabel"
        );
        close_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
        );

        HideWindow();
        shade.GuiInput += _on_shade_gui_input;
        close_button.Pressed += _close_window;
    }

    public void ShowCharacter(GDictionary window_data)
    {
        CharacterInfoPayload payload = CharacterInfoPayload.From(window_data);
        if (payload == null)
        {
            HideWindow();
            return;
        }

        Visible = true;
        title_label.Text = payload.DisplayName;
        meta_label.Text = payload.MetaLabel.StripEdges();
        meta_label.Visible = !string.IsNullOrEmpty(meta_label.Text);
        _rebuild_sections(payload.Sections);
        status_label.Text = payload.StatusLabel;
        status_block.Visible = !string.IsNullOrEmpty(payload.StatusLabel);
    }

    public void HideWindow()
    {
        Visible = false;
        title_label.Text = "人物信息";
        meta_label.Text = "";
        meta_label.Visible = false;
        _clear_sections();
        status_label.Text = "";
        status_block.Visible = false;
    }

    private void _close_window()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;
        if (
            mouseEvent.ButtonIndex != MouseButton.Left
            && mouseEvent.ButtonIndex != MouseButton.Right
        )
            return;
        _close_window();
    }

    private void _rebuild_sections(List<CharacterInfoSection> sections)
    {
        _clear_sections();
        foreach (CharacterInfoSection section in sections)
            sections_container.AddChild(_build_section_panel(section));
        if (sections_scroll != null)
            sections_scroll.SetDeferred("scroll_vertical", 0);
    }

    private void _clear_sections()
    {
        if (sections_container == null)
            return;
        foreach (Node child in sections_container.GetChildren())
            child.Free();
    }

    private PanelContainer _build_section_panel(CharacterInfoSection section)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", _create_section_panel_stylebox());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        if (!string.IsNullOrEmpty(section.Title))
        {
            var sectionTitle = new Label { Text = section.Title };
            sectionTitle.AddThemeColorOverride(
                "font_color",
                new Color(0.972549f, 0.815686f, 0.427451f, 1.0f)
            );
            sectionTitle.AddThemeFontSizeOverride("font_size", 18);
            content.AddChild(sectionTitle);
        }

        foreach (CharacterInfoEntry entry in section.Entries)
        {
            content.AddChild(
                entry.Kind == EntryKind.Pair
                    ? _build_pair_entry(entry)
                    : _build_text_entry(entry.Text)
            );
        }

        return panel;
    }

    private Control _build_pair_entry(CharacterInfoEntry entry)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);

        var labelNode = new Label
        {
            CustomMinimumSize = new Vector2(108.0f, 0.0f),
            Text = $"{entry.Label}：",
        };
        labelNode.AddThemeColorOverride(
            "font_color",
            new Color(0.635294f, 0.713726f, 0.85098f, 1.0f)
        );
        row.AddChild(labelNode);

        var valueNode = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = entry.Value,
        };
        valueNode.AddThemeColorOverride("font_color", new Color(0.960784f, 0.976471f, 1.0f, 1.0f));
        row.AddChild(valueNode);

        return row;
    }

    private Label _build_text_entry(string text)
    {
        var textLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = text };
        textLabel.AddThemeColorOverride(
            "font_color",
            new Color(0.901961f, 0.933333f, 0.980392f, 1.0f)
        );
        return textLabel;
    }

    private static StyleBoxFlat _create_section_panel_stylebox()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.109804f, 0.145098f, 0.235294f, 0.88f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.278431f, 0.384314f, 0.584314f, 0.9f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomRight = 14,
            CornerRadiusBottomLeft = 14,
        };
    }

    private sealed class CharacterInfoPayload
    {
        public string DisplayName { get; private init; } = "";
        public string MetaLabel { get; private init; } = "";
        public string StatusLabel { get; private init; } = "";
        public List<CharacterInfoSection> Sections { get; private set; } = new();

        public static CharacterInfoPayload From(GDictionary data)
        {
            if (data == null)
                return null;
            CharacterInfoPayload payload = NormalizeTopLevelPayload(data);
            if (payload == null)
                return null;
            List<CharacterInfoSection> sections = NormalizeSections(data);
            if (sections.Count == 0)
                return null;
            payload.Sections = sections;
            return payload;
        }

        private static CharacterInfoPayload NormalizeTopLevelPayload(GDictionary data)
        {
            if (!HasOnlyKnownKeys(data, TopLevelKeys))
                return null;
            foreach (string fieldName in new[] { "display_name", "meta_label", "status_label" })
            {
                if (!HasString(data, fieldName))
                    return null;
            }

            string displayName = data["display_name"].AsString().StripEdges();
            if (string.IsNullOrEmpty(displayName))
                return null;

            return new CharacterInfoPayload
            {
                DisplayName = displayName,
                MetaLabel = data["meta_label"].AsString(),
                StatusLabel = data["status_label"].AsString(),
            };
        }

        private static List<CharacterInfoSection> NormalizeSections(GDictionary data)
        {
            if (!data.ContainsKey("sections"))
                return new List<CharacterInfoSection>();
            List<CharacterInfoSection> sections = NormalizeExplicitSections(
                ReadArray(data, "sections")
            );
            if (sections.Count == 0)
                return sections;
            if (!data.ContainsKey("fate"))
                return sections;

            CharacterInfoSection fateSection = BuildFateSection(ReadDictionary(data, "fate"), sections);
            if (fateSection == null)
                return new List<CharacterInfoSection>();
            sections.Add(fateSection);
            return sections;
        }

        private static List<CharacterInfoSection> NormalizeExplicitSections(GArray rawSections)
        {
            var sections = new List<CharacterInfoSection>();
            if (rawSections.Count == 0)
                return sections;

            foreach (var sectionValue in rawSections)
            {
                if (!sectionValue.TryAsDictionary(out GDictionary sectionData))
                    return new List<CharacterInfoSection>();
                if (!HasExactKeys(sectionData, SectionKeys) || !HasString(sectionData, "title"))
                    return new List<CharacterInfoSection>();
                string titleText = sectionData["title"].AsString().StripEdges();
                if (string.IsNullOrEmpty(titleText))
                    return new List<CharacterInfoSection>();
                if (
                    !sectionData.ContainsKey("entries")
                    || !HasArray(sectionData, "entries")
                )
                    return new List<CharacterInfoSection>();
                List<CharacterInfoEntry> entries = NormalizeSectionEntries(
                    ReadArray(sectionData, "entries")
                );
                if (entries.Count == 0)
                    return new List<CharacterInfoSection>();
                sections.Add(new CharacterInfoSection(titleText, entries));
            }

            return sections;
        }

        private static List<CharacterInfoEntry> NormalizeSectionEntries(
            Godot.Collections.Array entryOptions
        )
        {
            var entries = new List<CharacterInfoEntry>();
            if (entryOptions.Count == 0)
                return entries;
            foreach (var entryValue in entryOptions)
            {
                if (!entryValue.TryAsDictionary(out GDictionary entryData))
                    return new List<CharacterInfoEntry>();
                CharacterInfoEntry entry = NormalizeEntry(entryData);
                if (entry == null)
                    return new List<CharacterInfoEntry>();
                entries.Add(entry);
            }
            return entries;
        }

        private static CharacterInfoEntry NormalizeEntry(GDictionary entry)
        {
            if (HasExactKeys(entry, PairEntryKeys))
            {
                if (!HasString(entry, "label") || !HasString(entry, "value"))
                    return null;
                string labelText = entry["label"].AsString().StripEdges();
                if (string.IsNullOrEmpty(labelText))
                    return null;
                return CharacterInfoEntry.Pair(labelText, entry["value"].AsString());
            }
            if (HasExactKeys(entry, TextEntryKeys))
            {
                if (!HasString(entry, "text"))
                    return null;
                string textValue = entry["text"].AsString().StripEdges();
                if (string.IsNullOrEmpty(textValue))
                    return null;
                return CharacterInfoEntry.TextEntry(textValue);
            }
            return null;
        }

        private static CharacterInfoSection BuildFateSection(
            GDictionary fateData,
            List<CharacterInfoSection> existingSections
        )
        {
            if (HasSectionTitle(existingSections, FateSectionTitle))
                return null;

            if (!HasExactKeys(fateData, FateKeys))
                return null;
            foreach (
                string fieldName in new[]
                {
                    "hidden_luck_at_birth",
                    "faith_luck_bonus",
                    "effective_luck",
                    "fortune_marked",
                    "doom_marked",
                    "doom_authority",
                }
            )
            {
                if (!HasInt(fateData, fieldName))
                    return null;
            }
            if (!HasBool(fateData, "has_misfortune"))
                return null;

            int hiddenLuckAtBirth = ReadInt(fateData, "hidden_luck_at_birth");
            int faithLuckBonus = ReadInt(fateData, "faith_luck_bonus");
            int effectiveLuck = ReadInt(fateData, "effective_luck");
            int fortuneMarked = ReadInt(fateData, "fortune_marked");
            int doomMarked = ReadInt(fateData, "doom_marked");
            int doomAuthority = ReadInt(fateData, "doom_authority");
            bool hasMisfortune = ReadBool(fateData, "has_misfortune");
            int expectedEffectiveLuck = Mathf.Clamp(
                hiddenLuckAtBirth + faithLuckBonus,
                UnitBaseAttributes.EffectiveLuckMin,
                UnitBaseAttributes.EffectiveLuckMax
            );

            if (effectiveLuck != expectedEffectiveLuck)
                return null;
            if (fortuneMarked < 0 || doomMarked < 0 || doomAuthority < 0)
                return null;
            if (hasMisfortune != (doomAuthority > 0))
                return null;

            var entries = new List<CharacterInfoEntry>
            {
                CharacterInfoEntry.Pair("生来暗运", FormatSignedNumber(hiddenLuckAtBirth)),
                CharacterInfoEntry.Pair("信仰赐运", FormatSignedNumber(faithLuckBonus)),
                CharacterInfoEntry.Pair("有效运势", FormatSignedNumber(effectiveLuck)),
                CharacterInfoEntry.Pair(
                    "Fortuna 标记",
                    FormatFateMarkValue(fortuneMarked, "已获福印", "未获福印")
                ),
                CharacterInfoEntry.Pair(
                    "Misfortune 黑兆",
                    FormatFateMarkValue(doomMarked, "已见黑兆", "未见黑兆")
                ),
            };
            if (hasMisfortune)
                entries.Add(CharacterInfoEntry.Pair("厄权", $"{doomAuthority} 级"));
            foreach (string hintText in BuildFateHintTexts(hiddenLuckAtBirth, effectiveLuck))
                entries.Add(CharacterInfoEntry.TextEntry(hintText));

            return new CharacterInfoSection(FateSectionTitle, entries);
        }

        private static bool HasExactKeys(GDictionary data, IReadOnlyList<string> expectedKeys)
        {
            if (data.Count != expectedKeys.Count)
                return false;
            return HasOnlyKnownKeys(data, expectedKeys);
        }

        private static bool HasOnlyKnownKeys(GDictionary data, IReadOnlyList<string> expectedKeys)
        {
            foreach (Variant keyValue in data.Keys)
            {
                string key = keyValue.ToString();
                bool found = false;
                foreach (string expectedKey in expectedKeys)
                {
                    if (key == expectedKey)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static bool HasSectionTitle(List<CharacterInfoSection> sections, string titleText)
        {
            foreach (CharacterInfoSection section in sections)
            {
                if (section.Title.StripEdges() == titleText)
                    return true;
            }
            return false;
        }

        private static bool HasString(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.String;
        }

        private static GArray ReadArray(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array
                ? value.AsGodotArray()
                : new GArray();
        }

        private static GDictionary ReadDictionary(GDictionary data, string key)
        {
            return
                TryRead(data, key, out Variant value)
                && value.VariantType == Variant.Type.Dictionary
                ? value.AsGodotDictionary()
                : new GDictionary();
        }

        private static bool HasInt(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Int;
        }

        private static bool HasBool(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Bool;
        }

        private static bool HasArray(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array;
        }

        private static int ReadInt(GDictionary data, string key)
        {
            return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Int
                ? value.AsInt32()
                : 0;
        }

        private static bool ReadBool(GDictionary data, string key)
        {
            return
                TryRead(data, key, out Variant value)
                && value.VariantType == Variant.Type.Bool
                && value.AsBool();
        }

        private static bool TryRead(GDictionary data, string key, out Variant value)
        {
            if (data == null || string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            if (data.ContainsKey(key))
            {
                value = data[key];
                return true;
            }
            value = default;
            return false;
        }

        private static string FormatSignedNumber(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static string FormatFateMarkValue(int value, string markedText, string unmarkedText)
        {
            return $"{value}（{(value > 0 ? markedText : unmarkedText)}）";
        }

        private static List<string> BuildFateHintTexts(int hiddenLuckAtBirth, int effectiveLuck)
        {
            var hints = new List<string>();
            if (hiddenLuckAtBirth >= UnitBaseAttributes.EffectiveLuckMax)
                hints.Add("生来暗运已处于极端正运档，界面会按原值保留该刻印。");
            else if (hiddenLuckAtBirth <= UnitBaseAttributes.EffectiveLuckMin)
                hints.Add("生来暗运已压到最深坏运档，这类角色更容易撞进命运事件的极端分支。");

            if (effectiveLuck >= UnitBaseAttributes.EffectiveLuckMax)
                hints.Add(
                    "有效运势已到 +7 上限：高位大成功威胁区会吃满，但随机掉落仍只按 +5 结算。"
                );
            else if (effectiveLuck <= UnitBaseAttributes.EffectiveLuckMin)
                hints.Add(
                    "有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。"
                );
            return hints;
        }
    }

    private sealed class CharacterInfoSection
    {
        public string Title { get; }
        public List<CharacterInfoEntry> Entries { get; }

        public CharacterInfoSection(string title, List<CharacterInfoEntry> entries)
        {
            Title = title;
            Entries = entries;
        }
    }

    private sealed class CharacterInfoEntry
    {
        public EntryKind Kind { get; private init; }
        public string Label { get; private init; } = "";
        public string Value { get; private init; } = "";
        public string Text { get; private init; } = "";

        public static CharacterInfoEntry Pair(string label, string value)
        {
            return new CharacterInfoEntry
            {
                Kind = EntryKind.Pair,
                Label = label,
                Value = value,
            };
        }

        public static CharacterInfoEntry TextEntry(string text)
        {
            return new CharacterInfoEntry { Kind = EntryKind.Text, Text = text };
        }
    }
}

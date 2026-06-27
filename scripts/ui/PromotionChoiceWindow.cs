using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class PromotionChoiceWindow : Control
{
    private static readonly string[] PromptKeys =
    {
        "member_id",
        "member_name",
        "choices",
    };
    private static readonly string[] ChoiceKeys =
    {
        "profession_id",
        "display_name",
        "summary",
        "description",
        "granted_skill_ids",
        "selection_hint",
        "selection",
    };

    [Signal]
    public delegate void choice_submittedEventHandler(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    );

    [Signal]
    public delegate void cancelledEventHandler();

    private ColorRect _shade;
    private Label _titleLabel;
    private Label _metaLabel;
    private HBoxContainer _choiceCards;
    private RichTextLabel _detailsLabel;
    private Button _confirmButton;
    private Button _cancelButton;

    private StringName _memberId = "";
    private string _memberName = "";
    private readonly List<PromotionChoiceEntry> _choices = new();
    private int _selectedIndex = -1;
    private readonly List<PanelContainer> _cards = new();
    private StyleBoxFlat _cardStyleNormal;
    private StyleBoxFlat _cardStyleSelected;

    public override void _Ready()
    {
        _shade = GetNode<ColorRect>("Shade");
        _titleLabel = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        _metaLabel = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        _choiceCards = GetNode<HBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/ChoiceCards"
        );
        _detailsLabel = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel"
        );
        _confirmButton = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton"
        );
        _cancelButton = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/CancelButton"
        );

        _cardStyleNormal = SelectionCardBuilder.MakeStyle(false);
        _cardStyleSelected = SelectionCardBuilder.MakeStyle(true);
        HideWindow();
        _shade.GuiInput += _on_shade_gui_input;
        _confirmButton.Pressed += _on_confirm_button_pressed;
        _cancelButton.Pressed += _on_cancel_button_pressed;
    }

    public void ShowPromotion(GDictionary prompt_data)
    {
        prompt_data ??= new GDictionary();
        if (!IsValidPrompt(prompt_data))
        {
            HideWindow();
            return;
        }
        _memberId = DictStringName(prompt_data, "member_id");
        _memberName = DictString(prompt_data, "member_name", "");
        _choices.Clear();

        foreach (GDictionary choice in ReadDictionaryItems(DictArray(prompt_data, "choices")))
        {
            PromotionChoiceEntry entry = PromotionChoiceEntry.From(choice);
            if (entry != null)
                _choices.Add(entry);
        }

        Visible = true;
        _titleLabel.Text = "职业晋升";
        _metaLabel.Text = $"{_memberName} 触发了新的职业晋升选择。";
        _rebuild_choice_cards();
        _select_choice(_choices.Count > 0 ? 0 : -1);
    }

    public void HideWindow()
    {
        Visible = false;
        _memberId = "";
        _memberName = "";
        _choices.Clear();
        _selectedIndex = -1;
        _clear_cards();
        if (_detailsLabel != null)
            _detailsLabel.Text = "";
        if (_confirmButton != null)
            _confirmButton.Disabled = true;
    }

    private void _clear_cards()
    {
        if (_choiceCards == null)
            return;
        foreach (PanelContainer card in _cards)
        {
            if (GodotObject.IsInstanceValid(card))
                card.QueueFree();
        }
        _cards.Clear();
    }

    private void _rebuild_choice_cards()
    {
        _clear_cards();
        for (int index = 0; index < _choices.Count; index++)
        {
            PanelContainer card = _create_card(index, _choices[index]);
            _choiceCards.AddChild(card);
            _cards.Add(card);
        }
    }

    private PanelContainer _create_card(int index, PromotionChoiceEntry choice)
    {
        var skillStrings = new GArray();
        foreach (StringName skillId in choice.GrantedSkillIds)
            skillStrings.Add(skillId.ToString());

        PanelContainer card = SelectionCardBuilder.BuildCard(
            new GDictionary
            {
                ["title"] = choice.DisplayName,
                ["summary"] = choice.Summary,
                ["chip_header"] = skillStrings.Count > 0 ? "授予技能" : "",
                ["chips"] = skillStrings,
            }
        );
        card.GuiInput += @event => _on_card_gui_input(@event, index);
        return card;
    }

    private void _select_choice(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (!GodotObject.IsInstanceValid(_cards[i]))
                continue;
            _cards[i]
                .AddThemeStyleboxOverride(
                    "panel",
                    i == index ? _cardStyleSelected : _cardStyleNormal
                );
        }
        _refresh_details();
    }

    private void _refresh_details()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _choices.Count)
        {
            _detailsLabel.Text = "[i]当前没有可用晋升项。[/i]";
            _confirmButton.Disabled = true;
            return;
        }

        PromotionChoiceEntry choiceData = _choices[_selectedIndex];

        var skillNames = new List<string>();
        foreach (StringName skillId in choiceData.GrantedSkillIds)
            skillNames.Add(skillId.ToString());
        string skillsText = skillNames.Count > 0 ? string.Join(", ", skillNames) : "暂无";

        _detailsLabel.Text = string.Join(
            "\n",
            new[]
            {
                $"[color=#fadc6f][b]{choiceData.DisplayName}[/b][/color]",
                "",
                !string.IsNullOrEmpty(choiceData.Description)
                    ? choiceData.Description
                    : "[i]暂无描述[/i]",
                "",
                $"[color=#a3c1ee]授予技能：[/color]{skillsText}",
                $"[color=#a3c1ee]说明：[/color][i]{choiceData.SelectionHint}[/i]",
            }
        );
        _confirmButton.Disabled = false;
    }

    private void _on_card_gui_input(InputEvent @event, int index)
    {
        if (@event is not InputEventMouseButton mouseEvent)
            return;
        if (!mouseEvent.Pressed || mouseEvent.ButtonIndex != MouseButton.Left)
            return;
        _select_choice(index);
    }

    private void _on_confirm_button_pressed()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _choices.Count)
            return;

        PromotionChoiceEntry choiceData = _choices[_selectedIndex];
        StringName memberId = _memberId;
        StringName professionId = choiceData.ProfessionId;
        GDictionary selection = RuntimePlainPayload.ProjectDictionary(
            choiceData.Selection,
            "PromotionChoiceWindow.choice.selection"
        );
        HideWindow();
        EmitSignal(SignalName.choice_submitted, memberId, professionId, selection);
    }

    private void _on_cancel_button_pressed()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.cancelled);
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent)
            return;
        if (!mouseEvent.Pressed)
            return;
        if (
            mouseEvent.ButtonIndex != MouseButton.Left
            && mouseEvent.ButtonIndex != MouseButton.Right
        )
            return;
        _on_cancel_button_pressed();
    }

    private static GArray DictArray(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Array)
            return new GArray();
        return value.AsGodotArray();
    }

    private static StringName DictStringName(GDictionary dict, string key)
    {
        if (!TryReadString(dict, key, out string value))
            return "";
        return new StringName(value);
    }

    private static Godot.Collections.Array<StringName> DictStringNameArray(
        GDictionary dict,
        string key
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var value in DictArray(dict, key))
        {
            if (value.VariantType != Variant.Type.String)
                continue;
            string rawId = value.AsString();
            if (string.IsNullOrEmpty(rawId))
                continue;
            result.Add(new StringName(rawId));
        }
        return result;
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryReadString(dict, key, out string value))
            return defaultValue;
        return value;
    }

    private sealed class PromotionChoiceEntry
    {
        public StringName ProfessionId { get; private init; } = "";
        public string DisplayName { get; private init; } = "";
        public string Summary { get; private init; } = "";
        public string Description { get; private init; } = "";
        public IReadOnlyList<StringName> GrantedSkillIds { get; private init; } =
            Array.Empty<StringName>();
        public string SelectionHint { get; private init; } = "";
        public Dictionary<string, object> Selection { get; private init; } =
            new(StringComparer.Ordinal);

        public static PromotionChoiceEntry From(GDictionary data)
        {
            if (!IsValidChoice(data))
                return null;

            var grantedSkillIds = new List<StringName>();
            foreach (StringName skillId in DictStringNameArray(data, "granted_skill_ids"))
                grantedSkillIds.Add(skillId);

            GDictionary selectionPayload = DictDictionary(data, "selection");
            return new PromotionChoiceEntry
            {
                ProfessionId = DictStringName(data, "profession_id"),
                DisplayName = DictString(data, "display_name", ""),
                Summary = DictString(data, "summary", ""),
                Description = DictString(data, "description", ""),
                GrantedSkillIds = grantedSkillIds,
                SelectionHint = DictString(data, "selection_hint", ""),
                Selection = RuntimePlainPayload.NormalizeDictionary(
                    selectionPayload,
                    "PromotionChoiceWindow.choice.selection"
                ),
            };
        }
    }

    private static bool IsValidPrompt(GDictionary data)
    {
        if (
            data == null
            || !HasOnlyKnownKeys(data, PromptKeys)
            || !HasNonEmptyString(data, "member_id")
            || !HasNonEmptyString(data, "member_name")
            || !HasArray(data, "choices")
        )
            return false;
        GArray choices = DictArray(data, "choices");
        if (choices.Count == 0)
            return false;
        foreach (Variant choiceValue in choices)
        {
            if (choiceValue.VariantType != Variant.Type.Dictionary)
                return false;
            if (!IsValidChoice(choiceValue.AsGodotDictionary()))
                return false;
        }
        return true;
    }

    private static bool IsValidChoice(GDictionary data)
    {
        if (
            data == null
            || !HasOnlyKnownKeys(data, ChoiceKeys)
            || !HasNonEmptyString(data, "profession_id")
            || !HasNonEmptyString(data, "display_name")
            || !HasString(data, "summary")
            || !HasString(data, "description")
            || !HasArray(data, "granted_skill_ids")
            || !HasNonEmptyString(data, "selection_hint")
            || !HasDictionary(data, "selection")
        )
            return false;
        foreach (Variant skillId in DictArray(data, "granted_skill_ids"))
        {
            if (skillId.VariantType != Variant.Type.String)
                return false;
            if (string.IsNullOrEmpty(skillId.AsString()))
                return false;
        }
        return true;
    }

    private static bool HasOnlyKnownKeys(GDictionary data, IReadOnlyList<string> expectedKeys)
    {
        if (data == null)
            return false;
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

    private static bool HasArray(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array;
    }

    private static bool HasDictionary(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary;
    }

    private static GDictionary DictDictionary(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Dictionary)
            return new GDictionary();
        return value.AsGodotDictionary();
    }

    private static bool HasString(GDictionary dict, string key)
    {
        return TryReadString(dict, key, out _);
    }

    private static bool HasNonEmptyString(GDictionary dict, string key)
    {
        return TryReadString(dict, key, out string value) && !string.IsNullOrEmpty(value);
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray items)
    {
        if (items == null)
            yield break;
        foreach (Variant item in items)
        {
            if (item.VariantType == Variant.Type.Dictionary)
                yield return item.AsGodotDictionary();
        }
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        value = default;
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return false;
        value = dict[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private static bool TryReadString(GDictionary dict, string key, out string value)
    {
        value = "";
        if (!TryRead(dict, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.String)
            return false;
        value = rawValue.AsString();
        return true;
    }
}

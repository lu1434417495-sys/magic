using System;
using System.Collections.Generic;
using Godot;
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

    public void ShowPromotion(IReadOnlyDictionary<string, object> prompt_data)
    {
        prompt_data ??= new Dictionary<string, object>(StringComparer.Ordinal);
        if (!IsValidPrompt(prompt_data))
        {
            HideWindow();
            return;
        }
        _memberId = DictStringName(prompt_data, "member_id");
        _memberName = DictString(prompt_data, "member_name", "");
        _choices.Clear();

        foreach (
            IReadOnlyDictionary<string, object> choice in ReadDictionaryItems(
                DictArray(prompt_data, "choices")
            )
        )
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
        var skillStrings = new List<object>();
        foreach (StringName skillId in choice.GrantedSkillIds)
            skillStrings.Add(skillId.ToString());

        var cardPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["title"] = choice.DisplayName,
            ["summary"] = choice.Summary,
            ["chip_header"] = skillStrings.Count > 0 ? "授予技能" : "",
            ["chips"] = skillStrings,
        };
        using GodotProjectionLease<GDictionary> cardLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                cardPayload,
                "PromotionChoiceWindow.choice.card",
                LifetimeDomain.Request,
                "PromotionChoiceWindow.choice.card"
            );
        PanelContainer card = SelectionCardBuilder.BuildCard(cardLease.Value);
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
        using GodotProjectionLease<GDictionary> selectionLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                choiceData.Selection,
                "PromotionChoiceWindow.choice.selection",
                LifetimeDomain.Request,
                "PromotionChoiceWindow.choice.selection"
            );
        HideWindow();
        EmitSignal(
            SignalName.choice_submitted,
            memberId,
            professionId,
            selectionLease.Value
        );
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

    private static IReadOnlyList<object> DictArray(
        IReadOnlyDictionary<string, object> dict,
        string key
    )
    {
        if (!TryRead(dict, key, out object value) || value is not IReadOnlyList<object> items)
            return Array.Empty<object>();
        return items;
    }

    private static StringName DictStringName(
        IReadOnlyDictionary<string, object> dict,
        string key
    )
    {
        if (!TryReadString(dict, key, out string value))
            return "";
        return new StringName(value);
    }

    private static IReadOnlyList<StringName> DictStringNameArray(
        IReadOnlyDictionary<string, object> dict,
        string key
    )
    {
        var result = new List<StringName>();
        foreach (object value in DictArray(dict, key))
        {
            if (value is not string rawId)
                continue;
            if (string.IsNullOrEmpty(rawId))
                continue;
            result.Add(new StringName(rawId));
        }
        return result;
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> dict,
        string key,
        string defaultValue
    )
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

        public static PromotionChoiceEntry From(IReadOnlyDictionary<string, object> data)
        {
            if (!IsValidChoice(data))
                return null;

            var grantedSkillIds = new List<StringName>();
            foreach (StringName skillId in DictStringNameArray(data, "granted_skill_ids"))
                grantedSkillIds.Add(skillId);

            IReadOnlyDictionary<string, object> selectionPayload = DictDictionary(
                data,
                "selection"
            );
            return new PromotionChoiceEntry
            {
                ProfessionId = DictStringName(data, "profession_id"),
                DisplayName = DictString(data, "display_name", ""),
                Summary = DictString(data, "summary", ""),
                Description = DictString(data, "description", ""),
                GrantedSkillIds = grantedSkillIds,
                SelectionHint = DictString(data, "selection_hint", ""),
                Selection = RuntimePlainPayload.CloneDictionary(selectionPayload),
            };
        }
    }

    private static bool IsValidPrompt(IReadOnlyDictionary<string, object> data)
    {
        if (
            data == null
            || !HasOnlyKnownKeys(data, PromptKeys)
            || !HasNonEmptyString(data, "member_id")
            || !HasNonEmptyString(data, "member_name")
            || !HasArray(data, "choices")
        )
            return false;
        IReadOnlyList<object> choices = DictArray(data, "choices");
        if (choices.Count == 0)
            return false;
        foreach (object choiceValue in choices)
        {
            if (
                choiceValue is not IReadOnlyDictionary<string, object> choice
                || !IsValidChoice(choice)
            )
                return false;
        }
        return true;
    }

    private static bool IsValidChoice(IReadOnlyDictionary<string, object> data)
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
        foreach (object skillId in DictArray(data, "granted_skill_ids"))
        {
            if (skillId is not string skillIdText || string.IsNullOrEmpty(skillIdText))
                return false;
        }
        return true;
    }

    private static bool HasOnlyKnownKeys(
        IReadOnlyDictionary<string, object> data,
        IReadOnlyList<string> expectedKeys
    )
    {
        if (data == null)
            return false;
        foreach (string key in data.Keys)
        {
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

    private static bool HasArray(IReadOnlyDictionary<string, object> dict, string key)
    {
        return TryRead(dict, key, out object value) && value is IReadOnlyList<object>;
    }

    private static bool HasDictionary(IReadOnlyDictionary<string, object> dict, string key)
    {
        return TryRead(dict, key, out object value)
            && value is IReadOnlyDictionary<string, object>;
    }

    private static IReadOnlyDictionary<string, object> DictDictionary(
        IReadOnlyDictionary<string, object> dict,
        string key
    )
    {
        if (
            !TryRead(dict, key, out object value)
            || value is not IReadOnlyDictionary<string, object> dictionary
        )
            return new Dictionary<string, object>(StringComparer.Ordinal);
        return dictionary;
    }

    private static bool HasString(IReadOnlyDictionary<string, object> dict, string key)
    {
        return TryReadString(dict, key, out _);
    }

    private static bool HasNonEmptyString(
        IReadOnlyDictionary<string, object> dict,
        string key
    )
    {
        return TryReadString(dict, key, out string value) && !string.IsNullOrEmpty(value);
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> ReadDictionaryItems(
        IReadOnlyList<object> items
    )
    {
        if (items == null)
            yield break;
        foreach (object item in items)
        {
            if (item is IReadOnlyDictionary<string, object> dictionary)
                yield return dictionary;
        }
    }

    private static bool TryRead(
        IReadOnlyDictionary<string, object> dict,
        string key,
        out object value
    )
    {
        value = null;
        if (dict == null || string.IsNullOrEmpty(key) || !dict.TryGetValue(key, out value))
            return false;
        return value != null;
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object> dict,
        string key,
        out string value
    )
    {
        value = "";
        if (!TryRead(dict, key, out object rawValue) || rawValue is not string text)
            return false;
        value = text;
        return true;
    }
}

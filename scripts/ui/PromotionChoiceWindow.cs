using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class PromotionChoiceWindow : Control
{
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
    private readonly List<GDictionary> _choices = new();
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

        _cardStyleNormal = SelectionCardBuilder.make_style(false);
        _cardStyleSelected = SelectionCardBuilder.make_style(true);
        hide_window();
        _shade.GuiInput += _on_shade_gui_input;
        _confirmButton.Pressed += _on_confirm_button_pressed;
        _cancelButton.Pressed += _on_cancel_button_pressed;
    }

    public void show_promotion(GDictionary prompt_data)
    {
        prompt_data ??= new GDictionary();
        _memberId = DictStringName(prompt_data, "member_id");
        _memberName = DictString(prompt_data, "member_name", "成员");
        _choices.Clear();

        foreach (GDictionary choice in GdInterop.ReadDictionaryItems(DictArray(prompt_data, "choices")))
        {
            _choices.Add((GDictionary)choice.Duplicate(true));
        }

        Visible = true;
        _titleLabel.Text = "职业晋升";
        _metaLabel.Text = $"{_memberName} 触发了新的职业晋升选择。";
        _rebuild_choice_cards();
        _select_choice(_choices.Count > 0 ? 0 : -1);
    }

    public void hide_window()
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

    private PanelContainer _create_card(int index, GDictionary choice)
    {
        StringName professionId = DictStringName(choice, "profession_id");
        string displayName = DictString(choice, "display_name", professionId.ToString());
        string summary = DictString(choice, "summary", "");
        Godot.Collections.Array<StringName> grantedSkillIds = DictStringNameArray(
            choice,
            "granted_skill_ids"
        );

        var skillStrings = new GArray();
        foreach (StringName skillId in grantedSkillIds)
            skillStrings.Add(skillId.ToString());

        PanelContainer card = SelectionCardBuilder.build_card(
            new GDictionary
            {
                ["title"] = displayName,
                ["summary"] = summary,
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

        GDictionary choiceData = _choices[_selectedIndex];
        string displayName = DictString(choiceData, "display_name", "");
        string description = DictString(choiceData, "description", "");
        Godot.Collections.Array<StringName> grantedSkillIds = DictStringNameArray(
            choiceData,
            "granted_skill_ids"
        );
        string selectionHint = DictString(
            choiceData,
            "selection_hint",
            "确认后将在战斗中立即生效。"
        );

        var skillNames = new List<string>();
        foreach (StringName skillId in grantedSkillIds)
            skillNames.Add(skillId.ToString());
        string skillsText = skillNames.Count > 0 ? string.Join(", ", skillNames) : "暂无";

        _detailsLabel.Text = string.Join(
            "\n",
            new[]
            {
                $"[color=#fadc6f][b]{displayName}[/b][/color]",
                "",
                !string.IsNullOrEmpty(description) ? description : "[i]暂无描述[/i]",
                "",
                $"[color=#a3c1ee]授予技能：[/color]{skillsText}",
                $"[color=#a3c1ee]说明：[/color][i]{selectionHint}[/i]",
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

        GDictionary choiceData = _choices[_selectedIndex];
        StringName professionId = DictStringName(choiceData, "profession_id");
        GDictionary selection = DictDictionaryDuplicate(choiceData, "selection");
        hide_window();
        EmitSignal(SignalName.choice_submitted, _memberId, professionId, selection);
    }

    private void _on_cancel_button_pressed()
    {
        if (!Visible)
            return;
        hide_window();
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
        if (dict == null || !dict.ContainsKey(key))
            return new GArray();
        return GdInterop.GetArray(dict, key);
    }

    private static GDictionary DictDictionaryDuplicate(GDictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return new GDictionary();
        GDictionary value = GdInterop.GetDictionary(dict, key);
        return value.Count > 0 ? (GDictionary)value.Duplicate(true) : new GDictionary();
    }

    private static StringName DictStringName(GDictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(dict[key]);
    }

    private static Godot.Collections.Array<StringName> DictStringNameArray(
        GDictionary dict,
        string key
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var value in DictArray(dict, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != (StringName)"")
                result.Add(normalized);
        }
        return result;
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }
}

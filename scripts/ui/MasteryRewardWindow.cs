using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class MasteryRewardWindow : Control
{
    [Signal]
    public delegate void confirmedEventHandler();

    private ColorRect _shade;
    private Label _titleLabel;
    private Label _metaLabel;
    private RichTextLabel _detailsLabel;
    private Button _confirmButton;

    private PendingCharacterReward _reward;

    public override void _Ready()
    {
        _shade = GetNode<ColorRect>("Shade");
        _titleLabel = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        _metaLabel = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        _detailsLabel = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel"
        );
        _confirmButton = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton"
        );

        HideWindow();
        _shade.GuiInput += _on_shade_gui_input;
        _confirmButton.Pressed += _on_confirm_button_pressed;
    }

    public void ShowReward(PendingCharacterReward reward)
    {
        ShowReward(reward, 1);
    }

    public void ShowReward(PendingCharacterReward reward, int remaining_count)
    {
        _reward = reward;
        Visible = true;

        string memberName = _read_reward_text("member_name", "成员");
        string sourceLabel = _read_reward_text("source_label", "角色奖励");
        string summaryText = _read_reward_text("summary_text", "");

        _titleLabel.Text = "角色奖励";
        _metaLabel.Text =
            $"{memberName} · {sourceLabel}{(remaining_count > 1 ? $" · 待处理 {remaining_count} 项" : "")}";
        _detailsLabel.Text = _build_details_text(memberName, sourceLabel, summaryText);
        _confirmButton.Disabled = false;
    }

    public void HideWindow()
    {
        Visible = false;
        _reward = null;
        if (_detailsLabel != null)
            _detailsLabel.Text = "";
        if (_confirmButton != null)
            _confirmButton.Disabled = true;
    }

    private string _build_details_text(string memberName, string sourceLabel, string summaryText)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(summaryText))
        {
            lines.Add(summaryText);
            lines.Add("");
        }

        lines.Add($"成员：{memberName}");
        lines.Add($"来源：{sourceLabel}");
        lines.Add("");
        lines.Add("奖励内容：");

        foreach (PendingCharacterRewardEntry entry in _get_reward_entries())
        {
            if (entry == null)
                continue;
            lines.Add(_build_entry_line(entry));
        }

        lines.Add("");
        lines.Add("确认后将立即把本批奖励结算到角色成长中。");
        return string.Join("\n", lines);
    }

    private string _build_entry_line(PendingCharacterRewardEntry entry)
    {
        string entryType = entry?.entry_type.ToString() ?? "";
        string targetLabel = _read_entry_target_label(entry);
        int amount = entry?.amount ?? 0;
        string reasonText = entry?.reason_text ?? "";
        string line = entryType switch
        {
            "knowledge_unlock" => $"- 解锁知识：{targetLabel}",
            "skill_unlock" => $"- 解锁技能：{targetLabel}",
            "skill_mastery" => $"- {targetLabel} 技能熟练度增长 {amount}",
            "attribute_delta" =>
                $"- {targetLabel} {(amount > 0 ? $"+{amount}" : amount.ToString())}",
            _ => $"- {targetLabel} × {amount}",
        };

        if (!string.IsNullOrEmpty(reasonText))
            line += $" · {reasonText}";
        return line;
    }

    private static string _read_entry_target_label(PendingCharacterRewardEntry entry)
    {
        if (entry == null)
            return "未知条目";
        if (!string.IsNullOrEmpty(entry.target_label))
            return entry.target_label;
        if (entry.target_id != "")
            return entry.target_id.ToString();
        return "未知条目";
    }

    private IEnumerable<PendingCharacterRewardEntry> _get_reward_entries()
    {
        return _reward?.entries ?? new List<PendingCharacterRewardEntry>();
    }

    private string _read_reward_text(string fieldName, string defaultValue)
    {
        if (_reward == null)
            return defaultValue;

        string value = fieldName switch
        {
            "member_name" => _reward.member_name,
            "source_label" => _reward.source_label,
            "summary_text" => _reward.summary_text,
            _ => "",
        };
        return !string.IsNullOrEmpty(value) ? value : defaultValue;
    }

    private void _on_confirm_button_pressed()
    {
        if (!Visible)
            return;
        EmitSignal(SignalName.confirmed);
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
        AcceptEvent();
    }
}

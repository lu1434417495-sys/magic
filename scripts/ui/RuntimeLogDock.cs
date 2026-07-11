using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class RuntimeLogDock : PanelContainer
{
    [Signal]
    public delegate void panel_layout_changedEventHandler();

    private static readonly Color TextPrimary = new(0.92f, 0.86f, 0.74f, 1.0f);
    private static readonly Color TextMuted = new(0.78f, 0.68f, 0.52f, 0.9f);
    private static readonly Color PanelFill = new(0.047f, 0.031f, 0.016f, 0.6f);
    private static readonly Color PanelBorder = new(0.706f, 0.588f, 0.353f, 0.7f);

    private const int PanelCornerRadius = 4;
    private const int PanelBorderWidth = 1;
    private const float LockedPanelWidth = 412.0f;
    private const float DesignPanelHeight = 600.0f;
    private const float CollapsedPanelHeight = 56.0f;
    private const int DesignMarginLeft = 14;
    private const int DesignMarginTop = 12;
    private const int DesignMarginRight = 14;
    private const int DesignMarginBottom = 12;
    private const int DesignLayoutSeparation = 6;
    private const int DesignHeaderSeparation = 6;
    private const int DesignTitleFontSize = 18;
    private const int DesignMetaFontSize = 11;
    private const int DesignLogFontSize = 18;
    private const int DesignButtonFontSize = 12;
    private const int DesignLogLineSeparation = 4;
    private const string CollapseButtonTextExpanded = "—";
    private const string CollapseButtonTextCollapsed = "▢";
    private const string WorldLogTitle = "运行日志";
    private const string BattleLogTitle = "战斗日志";
    private const string WorldLogEmptyText = "等待世界运行日志。";
    private const string BattleLogEmptyText = "等待战斗开始后显示完整战斗日志。";

    private static readonly float[] OpacityLevels = { 1.0f, 0.6f, 0.2f };

    public MarginContainer margin;
    public VBoxContainer layout;
    public HBoxContainer header_row;
    public Label title_label;
    public Label meta_label;
    public RichTextLabel log_output;
    public Button opacity_button;
    public Button collapse_button;

    private string _feed_source_id = "";
    private int _feed_entry_count;
    private string _feed_last_entry_key = "";
    private bool _is_collapsed;
    private int _opacity_level_index;

    private readonly record struct DisplayLogEntry(string Key, string Text);

    public override void _Ready()
    {
        margin = GetNode<MarginContainer>("Margin");
        layout = GetNode<VBoxContainer>("Margin/Layout");
        header_row = GetNode<HBoxContainer>("Margin/Layout/HeaderRow");
        title_label = GetNode<Label>("%TitleLabel");
        meta_label = GetNode<Label>("%MetaLabel");
        log_output = GetNode<RichTextLabel>("%LogOutput");
        opacity_button = GetNode<Button>("%OpacityButton");
        collapse_button = GetNode<Button>("%CollapseButton");

        _apply_static_skin();
        collapse_button.Pressed += _toggle_collapsed;
        opacity_button.Pressed += _cycle_opacity;
        ShowWorldLogs(new Dictionary<string, object>(StringComparer.Ordinal), "", "");
    }

    public bool IsCollapsed()
    {
        return _is_collapsed;
    }

    public float GetCollapsedHeight()
    {
        return CollapsedPanelHeight;
    }

    public float GetPreferredHeight(float available_height, float min_height)
    {
        return _is_collapsed ? CollapsedPanelHeight : Mathf.Max(available_height, min_height);
    }

    private void _toggle_collapsed()
    {
        _is_collapsed = !_is_collapsed;
        meta_label.Visible = !_is_collapsed;
        log_output.Visible = !_is_collapsed;
        collapse_button.Text = _is_collapsed
            ? CollapseButtonTextCollapsed
            : CollapseButtonTextExpanded;
        EmitSignal(SignalName.panel_layout_changed);
    }

    private void _cycle_opacity()
    {
        _opacity_level_index = (_opacity_level_index + 1) % OpacityLevels.Length;
        float level = OpacityLevels[_opacity_level_index];
        Modulate = new Color(Modulate.R, Modulate.G, Modulate.B, level);
        opacity_button.Text = $"{Mathf.RoundToInt(level * 100.0f)}%";
    }

    public void ShowWorldLogs(
        IReadOnlyDictionary<string, object> log_snapshot,
        string active_map_display_name = "",
        string status_text = ""
    )
    {
        log_snapshot ??= new Dictionary<string, object>(StringComparer.Ordinal);
        List<DisplayLogEntry> displayEntries = _build_runtime_log_entries(
            PlainList(log_snapshot, "entries")
        );
        string virtualPath = PlainString(log_snapshot, "virtual_path", "");
        string sourceId = $"runtime:{virtualPath}";
        string scopeText = !string.IsNullOrEmpty(active_map_display_name)
            ? active_map_display_name
            : "世界地图";
        int entryCount = PlainInt(log_snapshot, "entry_count", displayEntries.Count);
        int bufferLimit = Mathf.Max(
            PlainInt(log_snapshot, "buffer_limit", displayEntries.Count),
            1
        );
        string metaText = $"{scopeText}  ·  最近 {entryCount}/{bufferLimit} 条";

        _sync_entries(sourceId, WorldLogTitle, metaText, WorldLogEmptyText, displayEntries);
        meta_label.TooltipText = _build_runtime_log_meta_tooltip(
            scopeText,
            status_text,
            virtualPath,
            entryCount,
            bufferLimit
        );
    }

    public void ShowBattleLogs(BattleState battle_state)
    {
        if (battle_state == null)
        {
            _sync_entries(
                "",
                BattleLogTitle,
                _build_default_battle_meta_text(),
                BattleLogEmptyText,
                new List<DisplayLogEntry>()
            );
            meta_label.TooltipText = "";
            return;
        }

        List<DisplayLogEntry> displayEntries = _build_battle_log_entries(battle_state.log_entries);
        string sourceId = $"battle:{battle_state.battle_id}";
        string metaText =
            $"当前 {battle_state.GetLogBudgetSummaryText()}  ·  上限 {BattleState.LogEntryLimit} 条 / {BattleState.LogTextByteLimit / (1024 * 1024)} MiB";

        _sync_entries(sourceId, BattleLogTitle, metaText, BattleLogEmptyText, displayEntries);
        meta_label.TooltipText =
            $"battle_id={battle_state.battle_id}\nphase={battle_state.phase}\nlog_entries={battle_state.log_entries.Count}\ntext_budget={battle_state.GetLogTextByteSize()} bytes";
    }

    public void ClearLogs()
    {
        _sync_entries(
            "",
            WorldLogTitle,
            "等待运行时。",
            WorldLogEmptyText,
            new List<DisplayLogEntry>()
        );
        meta_label.TooltipText = "";
    }

    public Vector2 GetDesignPanelSize()
    {
        return new Vector2(LockedPanelWidth, DesignPanelHeight);
    }

    public void ApplyLayoutScale(float layout_scale)
    {
        float safeScale = Mathf.Max(layout_scale, 0.25f);
        if (margin != null)
        {
            margin.AddThemeConstantOverride(
                "margin_left",
                Mathf.RoundToInt(DesignMarginLeft * safeScale)
            );
            margin.AddThemeConstantOverride(
                "margin_top",
                Mathf.RoundToInt(DesignMarginTop * safeScale)
            );
            margin.AddThemeConstantOverride(
                "margin_right",
                Mathf.RoundToInt(DesignMarginRight * safeScale)
            );
            margin.AddThemeConstantOverride(
                "margin_bottom",
                Mathf.RoundToInt(DesignMarginBottom * safeScale)
            );
        }
        if (layout != null)
            layout.AddThemeConstantOverride(
                "separation",
                Mathf.Max(Mathf.RoundToInt(DesignLayoutSeparation * safeScale), 1)
            );
        if (header_row != null)
            header_row.AddThemeConstantOverride(
                "separation",
                Mathf.Max(Mathf.RoundToInt(DesignHeaderSeparation * safeScale), 1)
            );
        if (title_label != null)
            title_label.AddThemeFontSizeOverride(
                "font_size",
                Mathf.Max(Mathf.RoundToInt(DesignTitleFontSize * safeScale), 10)
            );
        if (meta_label != null)
            meta_label.AddThemeFontSizeOverride(
                "font_size",
                Mathf.Max(Mathf.RoundToInt(DesignMetaFontSize * safeScale), 8)
            );
        if (log_output != null)
        {
            log_output.AddThemeFontSizeOverride(
                "normal_font_size",
                Mathf.Max(Mathf.RoundToInt(DesignLogFontSize * safeScale), 10)
            );
            log_output.AddThemeConstantOverride(
                "line_separation",
                Mathf.Max(Mathf.RoundToInt(DesignLogLineSeparation * safeScale), 1)
            );
        }

        int buttonFontSize = Mathf.Max(Mathf.RoundToInt(DesignButtonFontSize * safeScale), 9);
        if (opacity_button != null)
            opacity_button.AddThemeFontSizeOverride("font_size", buttonFontSize);
        if (collapse_button != null)
            collapse_button.AddThemeFontSizeOverride("font_size", buttonFontSize);
    }

    private static List<DisplayLogEntry> _build_runtime_log_entries(
        IReadOnlyList<object> entries
    )
    {
        var displayEntries = new List<DisplayLogEntry>();
        foreach (IReadOnlyDictionary<string, object> entry in ReadPlainDictionaryItems(entries))
        {
            string message = PlainString(entry, "message", "").StripEdges();
            if (string.IsNullOrEmpty(message))
                continue;
            int seq = PlainInt(entry, "seq", 0);
            string eventId = PlainString(entry, "event_id", "");
            string domain = PlainString(entry, "domain", "runtime").ToUpper(
                System.Globalization.CultureInfo.GetCultureInfo("")
            );
            string level = PlainString(entry, "level", "info").ToUpper(
                System.Globalization.CultureInfo.GetCultureInfo("")
            );
            string timeText = _shorten_time_text(PlainString(entry, "time_text", ""));

            displayEntries.Add(
                new DisplayLogEntry(
                    $"{seq}:{eventId}:{message}",
                    $"[{timeText}][{domain}/{level}] {message}"
                )
            );
        }

        return displayEntries;
    }

    private static List<DisplayLogEntry> _build_battle_log_entries(
        Godot.Collections.Array<string> logEntries
    )
    {
        var displayEntries = new List<DisplayLogEntry>();
        if (logEntries == null)
            return displayEntries;

        for (int index = 0; index < logEntries.Count; index++)
        {
            string message = (logEntries[index] ?? "").StripEdges();
            if (string.IsNullOrEmpty(message))
                continue;
            displayEntries.Add(new DisplayLogEntry($"{index}:{message}", message));
        }

        return displayEntries;
    }

    private void _sync_entries(
        string source_id,
        string title_text,
        string meta_text,
        string empty_text,
        List<DisplayLogEntry> display_entries
    )
    {
        title_label.Text = title_text;
        meta_label.Text = meta_text;
        int entryCount = display_entries.Count;
        string lastEntryKey = _get_last_entry_key(display_entries);
        bool changed = false;

        if (source_id != _feed_source_id)
        {
            _reset_feed(source_id);
            _rebuild_feed(display_entries, empty_text);
            changed = true;
        }
        else if (entryCount < _feed_entry_count)
        {
            _rebuild_feed(display_entries, empty_text);
            changed = true;
        }
        else if (entryCount == _feed_entry_count && lastEntryKey != _feed_last_entry_key)
        {
            _rebuild_feed(display_entries, empty_text);
            changed = true;
        }
        else if (entryCount > _feed_entry_count)
        {
            for (int index = _feed_entry_count; index < entryCount; index++)
                _append_line(display_entries[index].Text);
            _feed_entry_count = entryCount;
            _feed_last_entry_key = lastEntryKey;
            changed = true;
        }

        // 用户要求日志框始终展示最新信息。旧的 follow-tail 判定靠滚动条位置推断,
        // 内容一超出可视区就恒判为"不跟随",新条目堆在折叠线以下、视图停在顶部。
        // 改为:只要内容有变化就滚到底,确保最新一条始终可见。
        if (changed && entryCount > 0)
            CallDeferred(MethodName._scroll_to_bottom);
    }

    private void _reset_feed(string source_id = "")
    {
        _feed_source_id = source_id;
        _feed_entry_count = 0;
        _feed_last_entry_key = "";
        log_output?.Clear();
    }

    private void _rebuild_feed(List<DisplayLogEntry> display_entries, string empty_text)
    {
        if (log_output == null)
            return;

        log_output.Clear();
        if (display_entries.Count == 0)
        {
            if (!string.IsNullOrEmpty(empty_text))
                log_output.AddText(empty_text);
            _feed_entry_count = 0;
            _feed_last_entry_key = "";
            return;
        }

        foreach (DisplayLogEntry entry in display_entries)
            _append_line(entry.Text);
        _feed_entry_count = display_entries.Count;
        _feed_last_entry_key = _get_last_entry_key(display_entries);
    }

    private void _append_line(string text)
    {
        if (log_output == null || string.IsNullOrEmpty(text))
            return;
        if (string.IsNullOrEmpty(log_output.GetParsedText()))
        {
            log_output.AddText(text);
            return;
        }
        log_output.Newline();
        log_output.AddText(text);
    }

    private static string _get_last_entry_key(List<DisplayLogEntry> display_entries)
    {
        if (display_entries.Count == 0)
            return "";
        return display_entries[^1].Key;
    }

    private static string _shorten_time_text(string time_text)
    {
        if (!string.IsNullOrEmpty(time_text) && time_text.Length >= 19)
            return time_text.Substring(11);
        return !string.IsNullOrEmpty(time_text) ? time_text : "--:--:--";
    }

    private static string _build_runtime_log_meta_tooltip(
        string scope_text,
        string status_text,
        string virtual_path,
        int entry_count,
        int buffer_limit
    )
    {
        var lines = new List<string>
        {
            $"scope={scope_text}",
            $"entries={entry_count}/{buffer_limit}",
        };
        if (!string.IsNullOrEmpty(status_text))
            lines.Add($"status={status_text}");
        if (!string.IsNullOrEmpty(virtual_path))
            lines.Add($"log={virtual_path}");
        return string.Join("\n", lines);
    }

    private static string _build_default_battle_meta_text()
    {
        return $"上限 {BattleState.LogEntryLimit} 条 / {BattleState.LogTextByteLimit / (1024 * 1024)} MiB";
    }

    private void _scroll_to_bottom()
    {
        if (log_output == null)
            return;
        log_output.ScrollToLine(Mathf.Max(log_output.GetLineCount() - 1, 0));
    }

    private void _apply_static_skin()
    {
        AddThemeStyleboxOverride("panel", _build_log_panel_style());
        title_label.AddThemeColorOverride("font_color", TextPrimary);
        meta_label.AddThemeColorOverride("font_color", TextMuted);
        opacity_button.AddThemeColorOverride("font_color", TextPrimary);
        collapse_button.AddThemeColorOverride("font_color", TextPrimary);
        log_output.FitContent = false;
        log_output.AddThemeColorOverride("default_color", TextPrimary);
        log_output.ScrollActive = true;
        collapse_button.Text = CollapseButtonTextExpanded;
        opacity_button.Text = $"{Mathf.RoundToInt(OpacityLevels[0] * 100.0f)}%";
        ApplyLayoutScale(1.0f);
    }

    private static StyleBoxFlat _build_log_panel_style()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelFill,
            BorderColor = PanelBorder,
            BorderWidthLeft = PanelBorderWidth,
            BorderWidthRight = PanelBorderWidth,
            BorderWidthTop = PanelBorderWidth,
            BorderWidthBottom = PanelBorderWidth,
            CornerRadiusTopLeft = PanelCornerRadius,
            CornerRadiusTopRight = PanelCornerRadius,
            CornerRadiusBottomLeft = PanelCornerRadius,
            CornerRadiusBottomRight = PanelCornerRadius,
            AntiAliasing = true,
        };
    }

    private static IReadOnlyList<object> PlainList(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return TryReadPlain(values, key, out object value)
            && value is IReadOnlyList<object> items
                ? items
                : Array.Empty<object>();
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string defaultValue
    )
    {
        return TryReadPlain(values, key, out object value) && value is string text
            ? text
            : defaultValue;
    }

    private static int PlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int defaultValue
    )
    {
        return TryReadPlain(values, key, out object value) && value is int number
            ? number
            : defaultValue;
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> ReadPlainDictionaryItems(
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

    private static bool TryReadPlain(
        IReadOnlyDictionary<string, object> values,
        string key,
        out object value
    )
    {
        value = null;
        return values != null
            && values.TryGetValue(key, out value)
            && value != null;
    }
}

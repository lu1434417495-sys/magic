using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using System;

[GlobalClass]
public partial class LoginScreen : Control
{
    public StringName PENDING_START_TYPE_PRESET = "preset";
    public StringName TEST_PRESET_ID = "test";
    public StringName DEFAULT_START_PRESET_ID = "small";

    [Export(PropertyHint.File, "*.tscn")]
    public string start_scene_path = "";

    public Button start_button;
    public Button test_button;
    public Button load_button;
    public Button settings_button;
    public Label status_label;
    public WorldPresetPickerWindow world_preset_picker_window;
    public SaveListWindow save_list_window;
    public DisplaySettingsWindow display_settings_window;
    public CharacterCreationWindow character_creation_window;

    public bool _is_transitioning;
    public DisplaySettingsService _display_settings_service;
    public DisplaySettingsService.DisplaySettings _display_settings = new(
        DisplaySettingsService.DefaultWindowedResolution,
        false
    );
    public StringName _pending_start_type = "";
    public StringName _pending_preset_id = "";

    public override void _Ready()
    {
        _display_settings_service = new DisplaySettingsService();
        _display_settings = _display_settings_service.LoadAndApply(GetWindow());

        start_button = GetNode<Button>("%StartButton");
        test_button = GetNode<Button>("%TestButton");
        load_button = GetNode<Button>("%LoadButton");
        settings_button = GetNode<Button>("%SettingsButton");
        status_label = GetNode<Label>("%StatusLabel");
        world_preset_picker_window = GetNode<WorldPresetPickerWindow>("WorldPresetPickerWindow");
        save_list_window = GetNode<SaveListWindow>("SaveListWindow");
        display_settings_window = GetNode<DisplaySettingsWindow>("DisplaySettingsWindow");
        character_creation_window = GetNode<CharacterCreationWindow>("CharacterCreationWindow");

        start_button.Pressed += _on_start_button_pressed;
        test_button.Pressed += _on_test_button_pressed;
        load_button.Pressed += _on_load_button_pressed;
        settings_button.Pressed += _on_settings_button_pressed;
        world_preset_picker_window.preset_confirmed += _on_world_preset_confirmed;
        world_preset_picker_window.cancelled += _on_world_preset_picker_cancelled;
        save_list_window.save_load_requested += _on_save_load_requested;
        save_list_window.closed += _on_save_list_closed;
        display_settings_window.settings_apply_requested += _on_display_settings_apply_requested;
        display_settings_window.cancelled += _on_display_settings_cancelled;
        display_settings_window.ConfigureOptions(
            _display_settings_service.ListResolutionOptions()
        );
        character_creation_window.character_confirmed += _on_character_creation_confirmed;
        character_creation_window.cancelled += _on_character_creation_cancelled;
        _configure_character_creation_window();
        start_button.GrabFocus();
        _show_idle_status();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_is_transitioning || _is_modal_open())
            return;
        if (@event is not InputEventKey keyEvent)
            return;
        if (!keyEvent.Pressed || keyEvent.Echo)
            return;
        if (
            keyEvent.Keycode != Key.Enter
            && keyEvent.Keycode != Key.KpEnter
            && keyEvent.Keycode != Key.Space
        )
            return;

        Viewport viewport = GetViewport();
        viewport?.SetInputAsHandled();
        _open_start_game_picker();
    }

    public void _on_start_button_pressed()
    {
        _open_start_game_picker();
    }

    public void _on_test_button_pressed()
    {
        if (_is_transitioning)
            return;
        if (!_validate_start_scene_path())
            return;
        _open_character_creation_for(PENDING_START_TYPE_PRESET, TEST_PRESET_ID);
    }

    public void _on_load_button_pressed()
    {
        if (_is_transitioning)
            return;
        if (!_validate_start_scene_path())
            return;

        GameSession gameSession = _get_game_session();
        if (gameSession == null)
        {
            _show_error("未找到 GameSession。");
            return;
        }

        GDictionaryArray saveSlots = gameSession.ListSaveSlots();
        save_list_window.ShowWindow(ToUntypedArray(saveSlots));
        status_label.Text =
            saveSlots.Count == 0
                ? "当前没有可加载的存档。可以先点击“进入游戏”或“测试地图”创建新存档。"
                : "请选择一个已有存档继续加载游戏。";
    }

    public void _on_settings_button_pressed()
    {
        if (_is_transitioning)
            return;
        display_settings_window.ShowWindow(_display_settings);
        status_label.Text = "请选择游戏分辨率，并按需切换全屏模式。";
    }

    public void _open_start_game_picker()
    {
        if (_is_transitioning)
            return;
        if (!_validate_start_scene_path())
            return;

        var presets = new GDictionaryArray();
        foreach (WorldPresetRegistry.WorldPresetInfo presetData in WorldPresetRegistry.ListPresetsTyped())
        {
            if (presetData.PresetId == TEST_PRESET_ID)
                continue;
            presets.Add(presetData.ToDictionary());
        }

        if (presets.Count == 0)
        {
            _show_error("当前没有可用的正式世界预设。");
            return;
        }

        world_preset_picker_window.ShowWindow(ToUntypedArray(presets), DEFAULT_START_PRESET_ID);
        status_label.Text = "请选择正式世界类型。";
    }

    public void _on_world_preset_confirmed(StringName preset_id)
    {
        _open_character_creation_for(PENDING_START_TYPE_PRESET, preset_id);
    }

    public void _on_world_preset_picker_cancelled()
    {
        _show_idle_status();
    }

    public void _open_character_creation_for(StringName start_type, StringName preset_id)
    {
        if (!_can_open_character_creation())
        {
            _pending_start_type = "";
            _pending_preset_id = "";
            return;
        }
        _pending_start_type = start_type;
        _pending_preset_id = preset_id;
        _configure_character_creation_window();
        character_creation_window.ShowWindow();
        status_label.Text = "请输入主角姓名并掷出六项属性。";
    }

    public bool _can_open_character_creation()
    {
        GameSession gameSession = _get_game_session();
        if (gameSession == null)
        {
            _show_error("未找到 GameSession。");
            return false;
        }

        GDictionary snapshot = gameSession.RefreshContentValidationSnapshot();
        if (!DictBool(snapshot, "ok", false) || !gameSession.IsContentValidationOk())
        {
            _show_error("内容校验失败，无法开始建卡。请查看日志并修正配置。");
            return false;
        }
        return true;
    }

    public void _on_character_creation_confirmed(GDictionary payload)
    {
        StringName startType = _pending_start_type;
        StringName presetId = _pending_preset_id;
        _pending_start_type = "";
        _pending_preset_id = "";

        if (startType == PENDING_START_TYPE_PRESET)
            _start_preset(presetId, payload);
        else
            _show_idle_status();
    }

    public void _on_character_creation_cancelled()
    {
        _pending_start_type = "";
        _pending_preset_id = "";
        _show_idle_status();
    }

    public void _on_save_load_requested(string save_id)
    {
        if (_is_transitioning)
            return;

        _set_transition_state(true);
        status_label.Text = "正在加载存档并进入游戏...";

        GameSession gameSession = _get_game_session();
        if (gameSession == null)
        {
            _set_transition_state(false);
            _show_error("未找到 GameSession。");
            return;
        }

        Error loadError = (Error)gameSession.LoadSave(save_id);
        if (loadError != Error.Ok)
        {
            _set_transition_state(false);
            _show_error(
                loadError == Error.InvalidData
                    ? "该存档数据不完整或版本不匹配，当前版本无法加载。"
                    : "加载存档失败，请检查存档数据。"
            );
            GameLog.Error($"Failed to load save slot {save_id}. Error code: {loadError}", "ui.save.load_failed", "ui");
            return;
        }

        _change_to_start_scene();
    }

    public void _on_save_list_closed()
    {
        _show_idle_status();
    }

    public void _on_display_settings_apply_requested(Vector2I resolution, bool fullscreen)
    {
        var requestedSettings = new DisplaySettingsService.DisplaySettings(resolution, fullscreen);
        _display_settings = _display_settings_service.ApplySettings(requestedSettings, GetWindow());
        Error saveError = _display_settings_service.SaveSettings(_display_settings);
        status_label.Text =
            saveError == Error.Ok
                ? $"显示设置已应用：{_display_settings_service.DescribeSettings(_display_settings)}。"
                : "显示设置已应用，但本地保存失败。";
        start_button.GrabFocus();
    }

    public void _on_display_settings_cancelled()
    {
        start_button.GrabFocus();
        _show_idle_status();
    }

    public void _start_preset(StringName preset_id, GDictionary character_creation_payload = null)
    {
        character_creation_payload ??= new GDictionary();
        if (_is_transitioning)
            return;
        if (!_validate_start_scene_path())
            return;

        if (!WorldPresetRegistry.TryGetPresetTyped(preset_id, out var presetData))
        {
            _show_error("未找到对应的世界预设。");
            return;
        }

        string generationConfigPath = presetData.GenerationConfigPath;
        if (string.IsNullOrEmpty(generationConfigPath))
        {
            _show_error("世界预设缺少生成配置路径。");
            return;
        }

        _set_transition_state(true);
        string presetName = string.IsNullOrEmpty(presetData.DisplayName)
            ? "世界"
            : presetData.DisplayName;
        status_label.Text = $"正在创建 {presetName} 存档并进入游戏...";

        Error sessionError = CreateSaveForPreset(preset_id, character_creation_payload);
        if (sessionError != Error.Ok)
        {
            _set_transition_state(false);
            _show_error("世界创建失败，请检查持久化目录或配置。");
            GameLog.Error(
                $"Failed to create save for preset {preset_id}. Error code: {sessionError}.",
                "ui.save.create_failed",
                "ui"
            );
            return;
        }

        _change_to_start_scene();
    }

    public int _create_save_for_preset(StringName preset_id)
    {
        return (int)CreateSaveForPreset(preset_id, new GDictionary());
    }

    private Error CreateSaveForPreset(StringName preset_id, GDictionary character_creation_payload)
    {
        character_creation_payload ??= new GDictionary();
        if (!WorldPresetRegistry.TryGetPresetTyped(preset_id, out var presetData))
            return Error.DoesNotExist;

        string generationConfigPath = presetData.GenerationConfigPath;
        if (string.IsNullOrEmpty(generationConfigPath))
            return Error.InvalidData;

        GameSession gameSession = _get_game_session();
        if (gameSession == null)
            return Error.Unconfigured;

        return (Error)
            gameSession.CreateNewSave(
                generationConfigPath,
                preset_id,
                string.IsNullOrEmpty(presetData.DisplayName) ? "世界" : presetData.DisplayName,
                character_creation_payload
            );
    }

    public void _change_to_start_scene()
    {
        Error changeError = GetTree().ChangeSceneToFile(start_scene_path);
        if (changeError == Error.Ok)
            return;

        _set_transition_state(false);
        _show_error("进入游戏失败，请检查场景路径。");
        GameLog.Error($"Failed to change scene to {start_scene_path}. Error code: {changeError}.", "ui.scene.change_failed", "ui");
    }

    public void _set_transition_state(bool in_progress)
    {
        _is_transitioning = in_progress;
        start_button.Disabled = in_progress;
        test_button.Disabled = in_progress;
        load_button.Disabled = in_progress;
        settings_button.Disabled = in_progress;
    }

    public bool _validate_start_scene_path()
    {
        if (string.IsNullOrEmpty(start_scene_path))
        {
            _show_error("未配置开始场景。");
            return false;
        }
        return true;
    }

    public bool _is_modal_open()
    {
        return world_preset_picker_window.Visible
            || save_list_window.Visible
            || display_settings_window.Visible
            || character_creation_window.Visible;
    }

    public void _show_idle_status()
    {
        status_label.Text =
            "点击“进入游戏”创建正式世界，点击“加载存档”继续已有进度，或点击“测试地图”创建测试世界。";
    }

    public void _show_error(string message)
    {
        status_label.Text = message;
    }

    public void _configure_character_creation_window()
    {
        GameSession gameSession = _get_game_session();
        if (gameSession == null)
            return;
        character_creation_window.SetProgressionContentRegistry(
            gameSession.GetProgressionContentRegistry()
        );
    }

    public GameSession _get_game_session()
    {
        SceneTree tree = GetTree();
        if (tree?.Root == null)
            return null;
        return tree.Root.GetNodeOrNull<GameSession>("GameSession");
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            return defaultValue;
        return value.AsBool();
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        value = default;
        if (dict == null || !dict.ContainsKey(key))
            return false;
        value = dict[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private static Godot.Collections.Array ToUntypedArray(GDictionaryArray items)
    {
        var result = new Godot.Collections.Array();
        if (items == null)
            return result;
        foreach (GDictionary item in items)
            result.Add(item);
        return result;
    }
}

using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class GameSession : Node
{
    private const string SaveDirectory = "user://saves";
    private const string SaveIndexPath = "user://saves/index.dat";
    private const int SaveVersion = 7;
    private const int SaveIndexVersion = 3;
    private const int MaxActiveMemberCount = 4;
    private static readonly int SaveFileCompressionMode = (int)FileAccess.CompressionMode.Zstd;

    private static readonly string[] ContentValidationDomainOrder =
    {
        "progression",
        "battle_special_profile",
        "item",
        "recipe",
        "enemy",
        "world",
        "quest",
    };

    private static readonly StringName RandomStartSkillTierBasic = "basic";
    private static readonly StringName RandomStartSkillTierIntermediate = "intermediate";
    private static readonly StringName RandomStartSkillTierAdvanced = "advanced";
    private static readonly StringName RandomStartSkillTierUltimate = "ultimate";

    private static readonly GDictionary RandomStartSkillLevelByTier = new()
    {
        [RandomStartSkillTierBasic] = 3,
        [RandomStartSkillTierIntermediate] = 2,
        [RandomStartSkillTierAdvanced] = 1,
        [RandomStartSkillTierUltimate] = 0,
    };

    private static readonly string[] RandomStartSkillKeywordsUltimate = { "终极", "大招" };
    private static readonly string[] RandomStartSkillKeywordsAdvanced =
    {
        "高阶",
        "招牌",
        "大型召唤",
    };
    private static readonly string[] RandomStartSkillKeywordsIntermediate = { "中段", "中后期" };
    private static readonly string[] RandomStartSkillKeywordsBasic =
    {
        "基础",
        "低耗",
        "起手",
        "最小保障",
    };

    private static readonly StringName WorldEquipmentInstanceSerialKey =
        "next_equipment_instance_serial";
    private static readonly StringName SaveDirtyScopeWorldData = "world_data";
    private static readonly StringName SaveDirtyScopePlayerCoord = "player_coord";
    private static readonly StringName SaveDirtyScopePlayerFactionId = "player_faction_id";
    private static readonly StringName SaveDirtyScopePartyState = "party_state";
    private static readonly StringName SaveDirtyScopePostDecodeRepair = "post_decode_repair";
    private static readonly StringName SaveDirtyScopeBattleLockedSave = "battle_locked_save";

    private static readonly StringName StartingMeleeWeaponItemId = "steel_longsword";
    private static readonly StringName StartingArcherWeaponItemId = "ash_shortbow";
    private static readonly StringName StartingCrossbowWeaponItemId = "militia_light_crossbow";
    private static readonly StringName StartingMageWeaponItemId = "oak_quarterstaff";
    private static readonly StringName StartingPriestWeaponItemId = "watchman_mace";

    public string _active_save_id = "";
    public string _active_save_path = "";
    public GDictionary _active_save_meta = new();
    public string _generation_config_path = "";
    public WorldMapGenerationConfig _generation_config;
    public GDictionary _world_data = new();
    public Vector2I _player_coord = Vector2I.Zero;
    public string _player_faction_id = "player";
    public PartyState _party_state = new();
    public bool _has_active_world;
    public bool _battle_save_lock_enabled;
    public bool _battle_save_dirty;
    public bool _runtime_save_dirty;
    public GStringNameArray _runtime_save_dirty_scopes = new();
    public int _last_save_error = (int)Error.Ok;
    public StringName _last_save_error_reason = "";
    public bool _post_decode_save_pending;
    public GStringNameArray _post_decode_save_reasons = new();

    public ProgressionContentRegistry _progression_content_registry = new();
    public ItemContentRegistry _item_content_registry = new();
    public RecipeContentRegistry _recipe_content_registry = new();
    public EnemyContentRegistry _enemy_content_registry = new();
    public BattleSpecialProfileRegistry _battle_special_profile_registry = new();
    public SkillBookItemFactory _skill_book_item_factory = new();

    public GDictionary _skill_defs = new();
    public GDictionary _profession_defs = new();
    public GDictionary _achievement_defs = new();
    public GDictionary _quest_defs = new();
    public GDictionary _item_defs = new();
    public GDictionary _recipe_defs = new();
    public GDictionary _enemy_templates = new();
    public GDictionary _enemy_ai_brains = new();
    public GDictionary _wild_encounter_rosters = new();
    public GDictionary _content_validation_snapshot = new();

    public SaveSerializer _save_serializer = new();
    public GameLogService _log_service = new();
    public WorldMapContentValidator _world_content_validator = new();

    public GDictionaryArray _save_index_entries_cache = new();
    public bool _save_index_cache_valid;
    public GDictionary _save_index_cache_signature = new();

    public bool fail_payload_write;

    public GameSession()
    {
        _save_serializer.setup(
            SaveVersion,
            SaveIndexVersion,
            MaxActiveMemberCount
        );

        _refresh_progression_content();
        _refresh_battle_special_profiles();
        _refresh_item_content();
        _refresh_recipe_content();
        _refresh_enemy_content();
        _refresh_content_validation_snapshot();
        _report_content_validation_errors();

        GameLog.AddSink(new GameSessionLogSink(this));
    }

    public int ensure_world_ready(string generation_config_path)
    {
        int contentValidationError = _require_content_validation_for_runtime("ensure_world_ready");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;
        if (_has_active_world && _generation_config_path == generation_config_path)
            return (int)Error.Ok;
        if (_try_load_game_state(generation_config_path))
            return (int)Error.Ok;
        return start_new_game(generation_config_path);
    }

    public int start_new_game(string generation_config_path)
    {
        string presetName = WorldPresetRegistry.get_fallback_preset_name(generation_config_path);
        return create_new_save(generation_config_path, "", presetName, new GDictionary());
    }

    public int create_new_save(string generation_config_path)
    {
        return create_new_save(generation_config_path, "", "", new GDictionary());
    }

    public int create_new_save(string generation_config_path, StringName preset_id)
    {
        return create_new_save(generation_config_path, preset_id, "", new GDictionary());
    }

    public int create_new_save(
        string generation_config_path,
        StringName preset_id,
        string preset_name
    )
    {
        return create_new_save(generation_config_path, preset_id, preset_name, new GDictionary());
    }

    public int create_new_save(
        string generation_config_path,
        StringName preset_id,
        string preset_name,
        GDictionary character_creation_payload
    )
    {
        character_creation_payload ??= new GDictionary();
        int contentValidationError = _require_content_validation_for_runtime("create_new_save");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;

        GDictionary previousRuntimeState = _capture_runtime_state();
        if (string.IsNullOrEmpty(generation_config_path))
        {
            throw new InvalidOperationException(
                "GameSession requires a generation config path."
            );
        }

        WorldMapGenerationConfig generationConfig = _load_generation_config(generation_config_path);
        if (generationConfig == null)
            return (int)Error.CantOpen;

        int prepareError = _prepare_new_world(generation_config_path, generationConfig);
        if (prepareError != (int)Error.Ok)
        {
            _restore_runtime_state(previousRuntimeState);
            return prepareError;
        }

        int characterCreationError = _apply_character_creation_payload_to_main_character(
            character_creation_payload
        );
        if (characterCreationError != (int)Error.Ok)
        {
            _restore_runtime_state(previousRuntimeState);
            return characterCreationError;
        }

        int timestamp = (int)Time.GetUnixTimeFromSystem();
        string saveId = _generate_unique_save_id(timestamp);
        if (string.IsNullOrEmpty(saveId))
        {
            _restore_runtime_state(previousRuntimeState);
            throw new InvalidOperationException(
                "GameSession failed to allocate a unique save id."
            );
        }

        _active_save_id = saveId;
        _active_save_path = _build_save_file_path(saveId);
        string resolvedPresetName = string.IsNullOrEmpty(preset_name)
            ? WorldPresetRegistry.get_fallback_preset_name(generation_config_path)
            : preset_name;
        _active_save_meta = _build_save_meta(
            saveId,
            saveId,
            generation_config_path,
            preset_id,
            resolvedPresetName,
            generationConfig.get_world_size_cells(),
            timestamp,
            timestamp
        );
        _rotate_log_session();

        int persistError = _persist_game_state();
        if (persistError == (int)Error.Ok)
        {
            _log_session_info(
                "session.save.create.ok",
                "已创建新存档。",
                Json.Stringify(new GDictionary
                {
                    ["save_id"] = _active_save_id,
                    ["generation_config_path"] = generation_config_path,
                    ["preset_id"] = preset_id.ToString(),
                    ["preset_name"] = preset_name,
                })
            );
        }
        else
        {
            _restore_runtime_state(previousRuntimeState);
        }
        return persistError;
    }

    public GDictionaryArray list_save_slots() => _load_save_index_entries();

    public GDictionaryArray peek_save_slots() => _peek_save_index_entries_read_only();

    public int load_save(string save_id)
    {
        if (!_save_serializer.is_valid_save_id_token(save_id))
            return (int)Error.InvalidParameter;
        int contentValidationError = _require_content_validation_for_runtime("load_save");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;

        GDictionary saveMeta = _get_save_meta_by_id(save_id);
        if (saveMeta.Count == 0)
        {
            throw new InvalidOperationException(
                $"GameSession could not find save slot {save_id}."
            );
        }

        string savePath = _build_save_file_path(save_id);
        GDictionary readResult = _read_save_payload(savePath);
        int readError = GetInt(readResult, "error", (int)Error.CantOpen);
        if (readError != (int)Error.Ok)
            return readError;

        if (!TryRead(readResult, "payload", out var payloadValue)
            || payloadValue.VariantType != Variant.Type.Dictionary)
        {
            throw new InvalidOperationException(
                $"GameSession loaded an invalid payload from {savePath}."
            );
        }

        GDictionary payload = payloadValue.AsGodotDictionary();
        if (!payload.ContainsKey("generation_config_path"))
        {
            throw new InvalidOperationException(
                $"Save slot {save_id} is missing generation_config_path."
            );
        }

        string generationConfigPath = GetString(payload, "generation_config_path").StripEdges();
        if (string.IsNullOrEmpty(generationConfigPath))
        {
            throw new InvalidOperationException(
                $"Save slot {save_id} is missing generation_config_path."
            );
        }

        WorldMapGenerationConfig generationConfig = _load_generation_config(generationConfigPath);
        if (generationConfig == null)
            return (int)Error.CantOpen;

        GDictionary previousRuntimeState = _capture_runtime_state();
        int loadError = _load_current_payload(
            payload,
            generationConfigPath,
            generationConfig,
            saveMeta
        );
        if (loadError == (int)Error.Ok)
        {
            _rotate_log_session();
            _log_session_info(
                "session.save.load.ok",
                "已加载存档。",
                Json.Stringify(new GDictionary
                {
                    ["save_id"] = save_id,
                    ["save_path"] = savePath,
                    ["generation_config_path"] = generationConfigPath,
                })
            );
        }
        else
        {
            _restore_runtime_state(previousRuntimeState);
        }
        return loadError;
    }

    public bool has_active_world() => _has_active_world;

    public string get_active_save_id() => _active_save_id;

    public string get_active_save_path() => _active_save_path;

    public GDictionary get_active_save_meta() => _active_save_meta.Duplicate(true);

    public GameLogService get_log_service() => _log_service;

    public GArray get_recent_logs() => get_recent_logs(50);

    public GArray get_recent_logs(int limit = 50) =>
        _log_service != null ? _log_service.get_recent_entries(limit) : new GArray();

    public GDictionary get_log_snapshot() => get_log_snapshot(50);

    public GDictionary get_log_snapshot(int limit = 50) =>
        _log_service != null ? _log_service.build_snapshot(limit) : new GDictionary();

    public string get_active_log_file_path() =>
        _log_service != null ? _log_service.get_log_path() : "";

    public string allocate_unique_save_id() => allocate_unique_save_id("save");

    public string allocate_unique_save_id(string prefix = "save") =>
        _generate_unique_save_id((int)Time.GetUnixTimeFromSystem(), prefix);

    public GDictionary get_content_validation_snapshot() =>
        _content_validation_snapshot.Duplicate(true);

    public GDictionary refresh_content_validation_snapshot()
    {
        _refresh_content_validation_snapshot();
        return get_content_validation_snapshot();
    }

    public bool is_content_validation_ok() => ReadExactBool(_content_validation_snapshot, "ok", false);

    public GDictionary log_event(
        string level,
        string domain,
        string event_id,
        string message,
        string context = ""
    )
    {
        return _log_service != null
            ? _log_service.append_entry(
                level,
                domain,
                event_id,
                message,
                context
            )
            : new GDictionary();
    }

    public GDictionary log_event(string level, string domain, string event_id, string message)
    {
        return log_event(level, domain, event_id, message, "");
    }

    public WorldMapGenerationConfig get_generation_config() => _generation_config;

    public string get_generation_config_path() => _generation_config_path;

    public GDictionary get_world_data() => _world_data;

    public StringName allocate_equipment_instance_id()
    {
        if (_world_data == null || !_world_data.ContainsKey(WorldEquipmentInstanceSerialKey))
            return "";
        GDictionary usedIds = _collect_persistent_equipment_instance_ids();
        int serial = GetInt(_world_data, WorldEquipmentInstanceSerialKey, 0);
        if (serial < 1)
            return "";
        while (true)
        {
            StringName candidate = EquipmentInstanceState.format_instance_id(serial);
            serial += 1;
            _world_data[WorldEquipmentInstanceSerialKey] = serial;
            if (!usedIds.ContainsKey(candidate.ToString()))
            {
                _mark_runtime_state_dirty(SaveDirtyScopeWorldData);
                return candidate;
            }
        }
    }

    public int set_world_data(GDictionary world_data)
    {
        GDictionary normalizedWorldData = _normalize_world_data(world_data ?? new GDictionary());
        if (normalizedWorldData.Count == 0)
            return (int)Error.InvalidData;
        _world_data = normalizedWorldData;
        _mark_runtime_state_dirty(SaveDirtyScopeWorldData);
        return (int)Error.Ok;
    }

    public Vector2I get_player_coord() => _player_coord;

    public int set_player_coord(Vector2I coord)
    {
        _player_coord = coord;
        _mark_runtime_state_dirty(SaveDirtyScopePlayerCoord);
        return (int)Error.Ok;
    }

    public string get_player_faction_id() => _player_faction_id;

    public int set_player_faction_id(string faction_id)
    {
        _player_faction_id = faction_id;
        _mark_runtime_state_dirty(SaveDirtyScopePlayerFactionId);
        return (int)Error.Ok;
    }

    public PartyState get_party_state() => _party_state;

    public int set_party_state(PartyState party_state)
    {
        _party_state = _normalize_party_state(party_state);
        _mark_runtime_state_dirty(SaveDirtyScopePartyState);
        return (int)Error.Ok;
    }

    public void set_battle_save_lock(bool enabled) => _battle_save_lock_enabled = enabled;

    public bool is_battle_save_locked() => _battle_save_lock_enabled;

    public bool has_pending_save() =>
        _runtime_save_dirty || _battle_save_dirty || _post_decode_save_pending;

    public void discard_pending_save()
    {
        _battle_save_dirty = false;
        _runtime_save_dirty = false;
        _runtime_save_dirty_scopes.Clear();
        _post_decode_save_pending = false;
        _post_decode_save_reasons.Clear();
    }

    public GDictionary get_save_status()
    {
        return new GDictionary
        {
            ["has_pending_save"] = has_pending_save(),
            ["dirty_scopes"] = _runtime_save_dirty_scopes.Duplicate(),
            ["battle_save_locked"] = _battle_save_lock_enabled,
            ["last_error"] = _last_save_error,
            ["last_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = _post_decode_save_reasons.Duplicate(),
        };
    }

    public void _mark_runtime_state_dirty(StringName scope)
    {
        _runtime_save_dirty = true;
        if (scope == "" || _runtime_save_dirty_scopes.Contains(scope))
            return;
        _runtime_save_dirty_scopes.Add(scope);
    }

    public void _clear_runtime_save_dirty()
    {
        _battle_save_dirty = false;
        _runtime_save_dirty = false;
        _runtime_save_dirty_scopes.Clear();
        _post_decode_save_pending = false;
        _post_decode_save_reasons.Clear();
    }

    public void _record_save_error(int error_code, StringName reason)
    {
        _last_save_error = error_code;
        _last_save_error_reason = reason;
    }

    public void _clear_last_save_error()
    {
        _last_save_error = (int)Error.Ok;
        _last_save_error_reason = "";
    }

    public void queue_post_decode_save(StringName reason)
    {
        _post_decode_save_pending = true;
        _mark_runtime_state_dirty(SaveDirtyScopePostDecodeRepair);
        if (reason == "" || _post_decode_save_reasons.Contains(reason))
            return;
        _post_decode_save_reasons.Add(reason);
    }

    public PartyMemberState get_party_member_state(StringName member_id)
    {
        return _party_state?.get_member_state(member_id);
    }

    public PartyMemberState get_leader_member_state()
    {
        return _party_state?.get_member_state(_party_state.leader_member_id);
    }

    public GDictionary _collect_persistent_equipment_instance_ids()
    {
        GDictionary usedIds = new();
        if (_party_state == null)
            return usedIds;
        _collect_warehouse_equipment_instance_ids(_party_state.warehouse_state, usedIds);
        foreach (var memberValue in _party_state.member_states.Values)
        {
            PartyMemberState memberState = memberValue.AsGodotObject() as PartyMemberState;
            EquipmentState equipmentState = memberState?.equipment_state;
            if (equipmentState == null)
                continue;
            foreach (StringName entrySlotId in equipmentState.get_entry_slot_ids())
            {
                StringName instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.get_equipped_instance_id(entrySlotId)
                );
                if (instanceId == "")
                    continue;
                usedIds[instanceId.ToString()] = true;
            }
        }
        return usedIds;
    }

    public void _collect_warehouse_equipment_instance_ids(
        WarehouseState warehouse_state,
        GDictionary used_ids
    )
    {
        if (warehouse_state == null || used_ids == null)
            return;
        foreach (EquipmentInstanceState instance in warehouse_state.get_non_empty_instances())
        {
            if (instance == null)
                continue;
            StringName instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
            if (instanceId == "")
                continue;
            used_ids[instanceId.ToString()] = true;
        }
    }

    public ProgressionContentRegistry get_progression_content_registry() =>
        _progression_content_registry;

    public GDictionary get_progression_content_bundle()
    {
        return _progression_content_registry == null
            ? new GDictionary()
            : _duplicate_content_bundle(_progression_content_registry.get_bundle());
    }

    public GDictionary get_skill_defs() => _skill_defs.Duplicate();

    public GDictionary get_battle_special_profile_registry_snapshot() =>
        _battle_special_profile_registry != null
            ? _battle_special_profile_registry.get_snapshot()
            : new GDictionary();

    public GDictionary get_profession_defs() => _profession_defs.Duplicate();

    public GDictionary get_achievement_defs() => _achievement_defs.Duplicate();

    public GDictionary get_quest_defs() => _quest_defs.Duplicate();

    public GDictionary get_item_defs() => _item_defs.Duplicate();

    public GDictionary get_recipe_defs() => _recipe_defs.Duplicate();

    public GDictionary get_enemy_templates() => _enemy_templates.Duplicate();

    public GDictionary get_enemy_ai_brains() => _enemy_ai_brains.Duplicate();

    public GDictionary get_wild_encounter_rosters() => _wild_encounter_rosters.Duplicate();

    public int install_test_content_def(
        StringName domain_id,
        StringName content_key,
        Resource content_def
    ) => InstallTestContentDef(domain_id, content_key, content_def);

    public int install_test_content_def_string_key(
        StringName domain_id,
        string content_key,
        Resource content_def
    ) => InstallTestContentDefStringKey(domain_id, content_key, content_def);

    private int InstallTestContentDef(
        StringName domain_id,
        StringName content_key,
        Resource content_def
    )
    {
        if (content_def == null)
            return (int)Error.InvalidParameter;
        if (content_key.ToString().Length == 0)
            return (int)Error.InvalidParameter;
        if (!TryGetTestContentRegistry(domain_id, out var registry, out var refreshBattleSpecialProfiles))
            return (int)Error.InvalidParameter;

        registry[content_key] = content_def;
        if (refreshBattleSpecialProfiles)
            _refresh_battle_special_profiles();
        return (int)Error.Ok;
    }

    private int InstallTestContentDefStringKey(
        StringName domain_id,
        string content_key,
        Resource content_def
    )
    {
        if (content_def == null)
            return (int)Error.InvalidParameter;
        if (string.IsNullOrEmpty(content_key))
            return (int)Error.InvalidParameter;
        if (!TryGetTestContentRegistry(domain_id, out var registry, out var refreshBattleSpecialProfiles))
            return (int)Error.InvalidParameter;

        registry[content_key] = content_def;
        if (refreshBattleSpecialProfiles)
            _refresh_battle_special_profiles();
        return (int)Error.Ok;
    }

    private bool TryGetTestContentRegistry(
        StringName domain_id,
        out GDictionary registry,
        out bool refreshBattleSpecialProfiles
    )
    {
        refreshBattleSpecialProfiles = false;
        switch (domain_id.ToString())
        {
            case "skill":
                registry = _skill_defs;
                refreshBattleSpecialProfiles = true;
                return true;
            case "profession":
                registry = _profession_defs;
                return true;
            case "achievement":
                registry = _achievement_defs;
                return true;
            case "quest":
                registry = _quest_defs;
                return true;
            case "item":
                registry = _item_defs;
                return true;
            case "recipe":
                registry = _recipe_defs;
                return true;
            case "enemy_template":
                registry = _enemy_templates;
                return true;
            case "enemy_ai_brain":
                registry = _enemy_ai_brains;
                return true;
            case "wild_encounter_roster":
                registry = _wild_encounter_rosters;
                return true;
            default:
                registry = new GDictionary();
                return false;
        }
    }

    public int save_world_state() => save_game_state();

    public int save_game_state()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            _battle_save_dirty = true;
            _mark_runtime_state_dirty(SaveDirtyScopeBattleLockedSave);
            return (int)Error.Ok;
        }
        return commit_runtime_state("save_game_state");
    }

    public int commit_runtime_state()
    {
        return commit_runtime_state("runtime");
    }

    public int commit_runtime_state(StringName reason)
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            _record_save_error((int)Error.Busy, reason);
            return (int)Error.Busy;
        }

        int persistError = _persist_game_state();
        if (persistError != (int)Error.Ok)
        {
            _record_save_error(persistError, reason);
            return persistError;
        }

        _clear_runtime_save_dirty();
        _clear_last_save_error();
        return (int)Error.Ok;
    }

    public int flush_game_state()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            _record_save_error((int)Error.Busy, "flush_game_state");
            return (int)Error.Busy;
        }
        if (!has_pending_save())
            return (int)Error.Ok;
        return commit_runtime_state("flush_game_state");
    }

    public int clear_persisted_world() => clear_persisted_game();

    public int clear_persisted_game()
    {
        _reset_runtime_state();
        _invalidate_save_index_cache();
        int removeError = _remove_directory_recursive(SaveDirectory);
        if (removeError != (int)Error.Ok)
            return removeError;
        _log_session_info("session.save.clear.ok", "已清理存档目录。");
        return (int)Error.Ok;
    }

    public void reset_runtime_cache() => _reset_runtime_state();

    public void unload_active_world()
    {
        if (!_has_active_world)
            return;
        if (has_pending_save())
        {
            if (_battle_save_lock_enabled)
            {
                _record_save_error((int)Error.Busy, "unload_active_world");
                throw new InvalidOperationException(
                    "GameSession cannot unload active world while battle save lock is enabled."
                );
            }
            int unloadSaveError = commit_runtime_state("unload_active_world");
            if (unloadSaveError != (int)Error.Ok)
            {
                throw new InvalidOperationException(
                    "GameSession failed to commit pending save before unloading active world."
                );
            }
        }
        string unloadedSaveId = _active_save_id;
        _reset_runtime_state();
        _rotate_log_session();
        _log_session_info(
            "session.runtime.unload.ok",
            "已卸载当前运行中世界。",
            Json.Stringify(new GDictionary { ["save_id"] = unloadedSaveId })
        );
    }

    public bool _try_load_game_state(string generation_config_path)
    {
        if (string.IsNullOrEmpty(generation_config_path))
            return false;

        bool attemptedCandidate = false;
        foreach (GDictionary saveMeta in _load_save_index_entries())
        {
            if (GetString(saveMeta, "generation_config_path") != generation_config_path)
                continue;
            attemptedCandidate = true;
            string candidateSaveId = GetString(saveMeta, "save_id");
            if (load_save(candidateSaveId) == (int)Error.Ok)
                return true;
            _log_session_info(
                "session.save.autoload.skip_bad_candidate",
                $"自动载入跳过坏存档 {candidateSaveId}。",
                Json.Stringify(new GDictionary
                {
                    ["save_id"] = candidateSaveId,
                    ["generation_config_path"] = generation_config_path,
                })
            );
        }
        return attemptedCandidate ? false : false;
    }

    public int _prepare_new_world(
        string generation_config_path,
        WorldMapGenerationConfig generation_config
    )
    {
        if (generation_config == null)
            return (int)Error.InvalidParameter;

        var gridSystem = new WorldMapGridSystem();
        gridSystem.setup(generation_config.world_size_in_chunks, generation_config.chunk_size);

        var spawnSystem = new WorldMapSpawnSystem();
        GDictionary worldData = spawnSystem.build_world(generation_config, gridSystem);

        _generation_config_path = generation_config_path;
        _generation_config = generation_config;
        _world_data = _normalize_world_data(worldData);
        _player_coord = GetVector2I(
            worldData,
            "player_start_coord",
            generation_config.player_start_coord
        );
        _player_faction_id = "player";
        _party_state = _create_default_party_state();
        _refresh_party_body_sizes_from_identity(_party_state);
        _backfill_racial_granted_skills(_party_state);
        _has_active_world = true;
        _battle_save_lock_enabled = false;
        _clear_runtime_save_dirty();
        _clear_last_save_error();
        return (int)Error.Ok;
    }

    public int _persist_game_state()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (string.IsNullOrEmpty(_active_save_id) || string.IsNullOrEmpty(_active_save_path))
        {
            throw new InvalidOperationException(
                "GameSession has world state but no active save slot."
            );
        }

        int ensureDirError = _ensure_save_directory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        int now = (int)Time.GetUnixTimeFromSystem();
        string displayName = GetString(_active_save_meta, "display_name", _active_save_id);
        _active_save_meta = _build_save_meta(
            _active_save_id,
            displayName,
            _generation_config_path,
            new StringName(GetString(_active_save_meta, "world_preset_id")),
            GetString(_active_save_meta, "world_preset_name"),
            _generation_config != null ? _generation_config.get_world_size_cells() : Vector2I.Zero,
            GetInt(_active_save_meta, "created_at_unix_time", now),
            now
        );

        int payloadWriteError = _write_save_payload_atomically(
            _active_save_path,
            _build_save_payload(now)
        );
        if (payloadWriteError != (int)Error.Ok)
            return payloadWriteError;

        int indexError = _write_save_index(
            _upsert_save_meta(_load_save_index_entries(), _active_save_meta)
        );
        if (indexError != (int)Error.Ok)
            return indexError;

        _battle_save_dirty = false;
        return (int)Error.Ok;
    }

    public int _load_current_payload(
        GDictionary payload,
        string generation_config_path,
        WorldMapGenerationConfig generation_config,
        GDictionary save_meta
    )
    {
        GDictionary decodeResult = _save_serializer.decode_payload(
            payload,
            generation_config_path,
            generation_config,
            save_meta
        );
        int decodeError = GetInt(decodeResult, "error", (int)Error.InvalidData);
        if (decodeError != (int)Error.Ok)
            return decodeError;

        PartyState decodedPartyState =
            (ReadGodotObject(decodeResult, "party_state") ?? new PartyState()) as PartyState
            ?? new PartyState();
        int identityError = _validate_decoded_party_identity_for_save(
            decodedPartyState,
            GetString(decodeResult, "active_save_id"),
            "load_save"
        );
        if (identityError != (int)Error.Ok)
            return identityError;

        _reset_runtime_state();
        _active_save_id = GetString(decodeResult, "active_save_id");
        _active_save_path = _build_save_file_path(_active_save_id);
        _active_save_meta = GetDictionary(decodeResult, "active_save_meta").Duplicate(true);
        _generation_config_path = GetString(
            decodeResult,
            "generation_config_path",
            generation_config_path
        );
        _generation_config =
            (ReadGodotObject(decodeResult, "generation_config") ?? generation_config)
                as WorldMapGenerationConfig
            ?? generation_config;
        _world_data = GetDictionary(decodeResult, "world_data").Duplicate(true);
        _player_coord = GetVector2I(decodeResult, "player_coord", Vector2I.Zero);
        _player_faction_id = GetString(decodeResult, "player_faction_id", "player");
        _party_state = decodedPartyState;
        _has_active_world = true;

        bool bodySizeChanged = _refresh_party_body_sizes_from_identity(_party_state);
        bool racialGrantsChanged = false;
        racialGrantsChanged = _revoke_orphan_racial_skills(_party_state) || racialGrantsChanged;
        racialGrantsChanged = _backfill_racial_granted_skills(_party_state) || racialGrantsChanged;
        if (bodySizeChanged)
            queue_post_decode_save("identity_body_size");
        if (racialGrantsChanged)
            queue_post_decode_save("racial_granted_skills");
        return (int)Error.Ok;
    }

    public int _flush_post_decode_save()
    {
        return !_post_decode_save_pending
            ? (int)Error.Ok
            : commit_runtime_state("post_decode_repair");
    }

    public bool _refresh_party_body_sizes_from_identity(PartyState party_state)
    {
        if (party_state == null)
            return false;
        bool changed = false;
        foreach (var memberValue in party_state.member_states.Values)
        {
            changed =
                _refresh_member_body_size_from_identity(
                    memberValue.AsGodotObject() as PartyMemberState
                ) || changed;
        }
        return changed;
    }

    public bool _backfill_racial_granted_skills(PartyState party_state)
    {
        return RacialSkillGrantService.backfill_party(
            party_state,
            get_progression_content_bundle(),
            _skill_defs,
            _profession_defs
        );
    }

    public bool _revoke_orphan_racial_skills(PartyState party_state)
    {
        return RacialSkillGrantService.revoke_orphan_party(
            party_state,
            get_progression_content_bundle(),
            _skill_defs,
            _profession_defs
        );
    }

    public GDictionary _build_save_payload(int saved_at_unix_time)
    {
        return _save_serializer.build_save_payload(
            _active_save_id,
            _generation_config_path,
            _active_save_meta,
            _world_data,
            _player_coord,
            _player_faction_id,
            _party_state,
            saved_at_unix_time
        );
    }

    public GDictionary _build_world_state_payload()
    {
        return _save_serializer.build_world_state_payload(
            _world_data,
            _player_coord,
            _player_faction_id
        );
    }

    public GDictionary _build_meta_payload(int saved_at_unix_time)
    {
        return _save_serializer.build_meta_payload(saved_at_unix_time);
    }

    public GDictionary _build_save_meta(
        string save_id,
        string display_name,
        string generation_config_path,
        StringName preset_id,
        string preset_name,
        Vector2I world_size_cells,
        int created_at_unix_time,
        int updated_at_unix_time
    )
    {
        return _save_serializer.build_save_meta(
            save_id,
            display_name,
            generation_config_path,
            preset_id,
            preset_name,
            world_size_cells,
            created_at_unix_time,
            updated_at_unix_time
        );
    }

    public string _generate_unique_save_id(int timestamp, string prefix = "save")
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        GDictionary existingSaveIds = new();
        foreach (GDictionary entry in _load_save_index_entries())
        {
            existingSaveIds[GetString(entry, "save_id")] = true;
        }

        GDictionary datetime = Time.GetDatetimeDictFromUnixTime(timestamp);
        string normalizedPrefix = (prefix ?? "").StripEdges().Replace(" ", "_");
        if (string.IsNullOrEmpty(normalizedPrefix))
            normalizedPrefix = "save";
        string idPrefix = string.Format(
            "{0}_{1:D4}{2:D2}{3:D2}_{4:D2}{5:D2}{6:D2}",
            normalizedPrefix,
            GetInt(datetime, "year", 1970),
            GetInt(datetime, "month", 1),
            GetInt(datetime, "day", 1),
            GetInt(datetime, "hour", 0),
            GetInt(datetime, "minute", 0),
            GetInt(datetime, "second", 0)
        );

        for (int attempt = 0; attempt < 128; attempt++)
        {
            string saveId = $"{idPrefix}_{rng.RandiRange(0, 999999):D6}";
            if (
                !existingSaveIds.ContainsKey(saveId)
                && !FileAccess.FileExists(_build_save_file_path(saveId))
            )
                return saveId;
        }
        return "";
    }

    public WorldMapGenerationConfig _load_generation_config(string generation_config_path)
    {
        var generationConfig = ResourceLoader.Load<WorldMapGenerationConfig>(
            generation_config_path
        );
        if (generationConfig == null)
        {
            _push_session_error(
                "session.config.load_failed",
                $"GameSession failed to load config from {generation_config_path}",
                Json.Stringify(new GDictionary { ["generation_config_path"] = generation_config_path })
            );
            return null;
        }
        return generationConfig;
    }

    public GDictionary _read_save_payload(string save_path, bool emit_errors = true)
    {
        int recoveryError = FileIOCoordinator.recover_replace_target(
            save_path,
            SaveFileCompressionMode,
            "session.save.read",
            "save",
            _push_session_error
        );
        if (recoveryError != (int)Error.Ok && recoveryError != (int)Error.DoesNotExist)
            return new GDictionary { ["error"] = recoveryError };
        if (!FileAccess.FileExists(save_path))
        {
            if (emit_errors)
            {
                throw new InvalidOperationException(
                    $"GameSession could not find persisted save {save_path}."
                );
            }
            return new GDictionary { ["error"] = (int)Error.DoesNotExist };
        }

        using FileAccess saveFile = FileAccess.OpenCompressed(
            save_path,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)SaveFileCompressionMode
        );
        if (saveFile == null)
        {
            Error openError = FileAccess.GetOpenError();
            if (emit_errors)
            {
                throw new InvalidOperationException(
                    $"Failed to open persisted save {save_path}. Error: {(int)openError}"
                );
            }
            return new GDictionary { ["error"] = (int)openError };
        }

        int saveSize = (int)saveFile.GetLength();
        if (saveSize < 8)
        {
            saveFile.Close();
            return new GDictionary { ["error"] = (int)Error.InvalidData };
        }

        var rawPayload = saveFile.GetVar(false);
        saveFile.Close();
        if (rawPayload.VariantType != Variant.Type.Dictionary)
            return new GDictionary { ["error"] = (int)Error.InvalidData };

        return new GDictionary { ["error"] = (int)Error.Ok, ["payload"] = rawPayload };
    }

    public int _ensure_save_directory()
    {
        return (int)
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory));
    }

    public string _build_save_file_path(string save_id)
    {
        if (!_save_serializer.is_valid_save_id_token(save_id))
            return "";
        return $"{SaveDirectory}/{save_id}.dat";
    }

    private int _write_compressed_variant_atomically(
        string virtual_path,
        GDictionary payload,
        string error_event_prefix,
        string label
    )
    {
        return FileIOCoordinator.write_compressed_variant_atomically(
            virtual_path,
            payload,
            SaveFileCompressionMode,
            error_event_prefix,
            label,
            _push_session_error
        );
    }

    public int _write_save_payload_atomically(string save_path, GDictionary payload)
    {
        var failValue = Get("fail_payload_write");
        if (
            fail_payload_write
            || (failValue.VariantType == Variant.Type.Bool && failValue.AsBool())
        )
            return (int)Error.CantCreate;
        return _write_compressed_variant_atomically(
            save_path,
            payload,
            "session.save.persist",
            "save"
        );
    }

    public int _replace_file_atomically(
        string source_path,
        string target_path,
        string error_event_prefix,
        string label
    )
    {
        return FileIOCoordinator.replace_file_atomically(
            source_path,
            target_path,
            error_event_prefix,
            label,
            _push_session_error
        );
    }

    public int _rename_file(string from_virtual_path, string to_virtual_path)
    {
        return FileIOCoordinator.rename_file(from_virtual_path, to_virtual_path);
    }

    public int _remove_file_if_exists(string virtual_path)
    {
        return FileIOCoordinator.remove_file_if_exists(virtual_path);
    }

    public GDictionaryArray _load_save_index_entries()
    {
        if (_is_save_index_cache_current())
            return _duplicate_save_index_entries(_save_index_entries_cache);

        bool shouldRewriteIndex = false;
        GArray rawEntries = new();
        int indexRecoveryError = FileIOCoordinator.recover_replace_target(
            SaveIndexPath,
            SaveFileCompressionMode,
            "session.save.index",
            "save index",
            _push_session_error
        );
        if (indexRecoveryError != (int)Error.Ok && indexRecoveryError != (int)Error.DoesNotExist)
            shouldRewriteIndex = true;
        if (!FileAccess.FileExists(SaveIndexPath))
        {
            shouldRewriteIndex = true;
        }
        else
        {
            using FileAccess indexFile = FileAccess.OpenCompressed(
                SaveIndexPath,
                FileAccess.ModeFlags.Read,
                (FileAccess.CompressionMode)SaveFileCompressionMode
            );
            if (indexFile == null)
            {
                shouldRewriteIndex = true;
            }
            else
            {
                bool hasIndexPayload = TryReadSaveIndexPayload(indexFile, out GDictionary rawPayloadDict);
                indexFile.Close();
                if (hasIndexPayload)
                {
                    TryRead(rawPayloadDict, "version", out var indexVersionValue);
                    TryRead(rawPayloadDict, "saves", out var savesValue);
                    if (
                        !_is_save_index_integer_value(indexVersionValue)
                        || indexVersionValue.AsInt32() != SaveIndexVersion
                        || savesValue.VariantType != Variant.Type.Array
                    )
                    {
                        shouldRewriteIndex = true;
                    }
                    else
                    {
                        rawEntries = savesValue.AsGodotArray();
                    }
                }
                else
                {
                    shouldRewriteIndex = true;
                }
            }
        }

        GDictionaryArray entries = _normalize_save_index_entries(rawEntries);
        GDictionaryArray rebuiltEntries = _rebuild_save_index_entries_from_save_files();
        GDictionaryArray mergedEntries = _merge_save_index_entries(entries, rebuiltEntries);
        if (shouldRewriteIndex || !_save_index_entries_match(entries, mergedEntries))
            _write_save_index(mergedEntries);
        else
            _set_save_index_cache(mergedEntries);
        return _duplicate_save_index_entries(mergedEntries);
    }

    public GDictionaryArray _peek_save_index_entries_read_only()
    {
        if (_is_save_index_cache_current())
            return _duplicate_save_index_entries(_save_index_entries_cache);
        if (!FileAccess.FileExists(SaveIndexPath))
            return new GDictionaryArray();

        using FileAccess indexFile = FileAccess.OpenCompressed(
            SaveIndexPath,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)SaveFileCompressionMode
        );
        if (indexFile == null)
            return new GDictionaryArray();
        bool hasIndexPayload = TryReadSaveIndexPayload(indexFile, out GDictionary rawPayloadDict);
        indexFile.Close();
        if (!hasIndexPayload)
            return new GDictionaryArray();

        TryRead(rawPayloadDict, "version", out var indexVersionValue);
        TryRead(rawPayloadDict, "saves", out var savesValue);
        if (
            !_is_save_index_integer_value(indexVersionValue)
            || indexVersionValue.AsInt32() != SaveIndexVersion
            || savesValue.VariantType != Variant.Type.Array
        )
        {
            return new GDictionaryArray();
        }

        GDictionaryArray entries = _normalize_save_index_entries(savesValue.AsGodotArray());
        _set_save_index_cache(entries);
        return _duplicate_save_index_entries(entries);
    }

    public int _write_save_index(GDictionaryArray entries)
    {
        int ensureDirError = _ensure_save_directory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        GDictionaryArray normalizedEntries = _normalize_save_index_entries(ToUntypedArray(entries));
        int writeError = _write_compressed_variant_atomically(
            SaveIndexPath,
            _build_save_index_payload(normalizedEntries),
            "session.save.index",
            "save index"
        );
        _set_save_index_cache(normalizedEntries);
        if (writeError != (int)Error.Ok)
            return (int)Error.Ok;
        return (int)Error.Ok;
    }

    private bool TryReadSaveIndexPayload(FileAccess index_file, out GDictionary payload)
    {
        GDictionary rawPayload = _save_serializer.read_save_index_payload(index_file);
        if (rawPayload != null)
        {
            payload = rawPayload;
            return true;
        }
        payload = new GDictionary();
        return false;
    }

    public bool _is_save_index_cache_current()
    {
        if (!_save_index_cache_valid)
            return false;
        GDictionary currentSignature = _get_save_index_file_signature();
        return ReadExactBool(_save_index_cache_signature, "exists") == ReadExactBool(currentSignature, "exists")
            && GetInt(_save_index_cache_signature, "modified_time", -1)
                == GetInt(currentSignature, "modified_time", -1)
            && GetInt(_save_index_cache_signature, "size", -1)
                == GetInt(currentSignature, "size", -1);
    }

    public void _set_save_index_cache(GDictionaryArray entries)
    {
        _save_index_entries_cache = _duplicate_save_index_entries(entries);
        _save_index_cache_valid = true;
        _save_index_cache_signature = _get_save_index_file_signature();
    }

    public void _invalidate_save_index_cache()
    {
        _save_index_entries_cache.Clear();
        _save_index_cache_valid = false;
        _save_index_cache_signature = new GDictionary();
    }

    public GDictionary _get_save_index_file_signature()
    {
        if (!FileAccess.FileExists(SaveIndexPath))
        {
            return new GDictionary
            {
                ["exists"] = false,
                ["modified_time"] = -1,
                ["size"] = -1,
            };
        }

        int size = -1;
        using FileAccess indexFile = FileAccess.Open(SaveIndexPath, FileAccess.ModeFlags.Read);
        if (indexFile != null)
        {
            size = (int)indexFile.GetLength();
            indexFile.Close();
        }
        return new GDictionary
        {
            ["exists"] = true,
            ["modified_time"] = (int)FileAccess.GetModifiedTime(SaveIndexPath),
            ["size"] = size,
        };
    }

    public GDictionaryArray _duplicate_save_index_entries(GDictionaryArray entries)
    {
        GDictionaryArray duplicatedEntries = new();
        if (entries == null)
            return duplicatedEntries;
        foreach (GDictionary entry in entries)
            duplicatedEntries.Add(entry?.Duplicate(true) ?? new GDictionary());
        return duplicatedEntries;
    }

    public GDictionary _duplicate_content_bundle(GDictionary bundle)
    {
        GDictionary duplicatedBundle = new();
        if (bundle == null)
            return duplicatedBundle;
        foreach (var key in bundle.Keys)
        {
            var value = bundle[key];
            if (value.VariantType == Variant.Type.Dictionary)
                duplicatedBundle[key] = value.AsGodotDictionary().Duplicate();
            else if (value.VariantType == Variant.Type.Array)
                duplicatedBundle[key] = value.AsGodotArray().Duplicate();
            else
                duplicatedBundle[key] = value;
        }
        return duplicatedBundle;
    }

    public bool _save_index_entries_match(
        GDictionaryArray left_entries,
        GDictionaryArray right_entries
    )
    {
        if (
            left_entries == null
            || right_entries == null
            || left_entries.Count != right_entries.Count
        )
            return false;
        for (int index = 0; index < left_entries.Count; index++)
        {
            if (!_save_index_entry_matches(left_entries[index], right_entries[index]))
                return false;
        }
        return true;
    }

    public bool _save_index_entry_matches(GDictionary left_entry, GDictionary right_entry)
    {
        string[] keys =
        {
            "save_id",
            "display_name",
            "world_preset_id",
            "world_preset_name",
            "generation_config_path",
            "world_size_cells",
            "created_at_unix_time",
            "updated_at_unix_time",
        };
        foreach (string key in keys)
        {
            TryRead(left_entry, key, out var leftVal);
            TryRead(right_entry, key, out var rightVal);
            if (!VariantEquals(leftVal, rightVal))
                return false;
        }
        return true;
    }

    public GDictionaryArray _normalize_save_index_entries(GArray raw_entries)
    {
        GDictionaryArray entries = new();
        if (raw_entries == null)
            return entries;
        foreach (GDictionary rawEntry in ReadDictionaryItems(raw_entries))
        {
            GDictionary entry = _normalize_save_meta(
                _deserialize_save_index_entry(rawEntry)
            );
            if (entry.Count == 0)
                continue;
            if (!FileAccess.FileExists(_build_save_file_path(GetString(entry, "save_id"))))
                continue;
            entries.Add(entry);
        }
        SortSaveMetaNewestFirst(entries);
        return entries;
    }

    public GDictionaryArray _serialize_save_index_entries(GDictionaryArray entries)
    {
        return _save_serializer.serialize_save_index_entries(entries);
    }

    public GDictionary _build_save_index_payload(GDictionaryArray entries)
    {
        return _save_serializer.build_save_index_payload(entries);
    }

    public GDictionary _deserialize_save_index_entry(GDictionary raw_entry)
    {
        return _save_serializer.deserialize_save_index_entry(raw_entry);
    }

    private bool _is_save_index_integer_value(Variant value)
    {
        return value.VariantType == Variant.Type.Int
            && _save_serializer.IsSaveIndexIntegerValue(value.AsInt32());
    }

    public GDictionaryArray _rebuild_save_index_entries_from_save_files()
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SaveDirectory)))
            return new GDictionaryArray();

        DirAccess saveDir = DirAccess.Open(SaveDirectory);
        if (saveDir == null)
            return new GDictionaryArray();

        GDictionary rebuiltById = new();
        Error listError = saveDir.ListDirBegin();
        if (listError != Error.Ok)
        {
            throw new InvalidOperationException(
                $"Failed to list save directory {SaveDirectory} for index rebuild. Error: {(int)listError}"
            );
        }

        while (true)
        {
            string fileName = saveDir.GetNext();
            if (string.IsNullOrEmpty(fileName))
                break;
            if (fileName == "." || fileName == ".." || saveDir.CurrentIsDir())
                continue;
            if (!fileName.EndsWith(".dat") || fileName == "index.dat")
                continue;
            string candidateSaveId = fileName[..^4];
            if (!_save_serializer.is_valid_save_id_token(candidateSaveId))
                continue;
            string savePath = $"{SaveDirectory}/{fileName}";
            GDictionary readResult = _read_save_payload(savePath, false);
            if (GetInt(readResult, "error", (int)Error.InvalidData) != (int)Error.Ok)
                continue;
            if (!TryRead(readResult, "payload", out var payloadValue)
                || payloadValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary payload = payloadValue.AsGodotDictionary();
            GDictionary saveMeta = _extract_save_meta_from_payload(payload);
            if (saveMeta.Count == 0)
                continue;
            string generationConfigPath = GetString(saveMeta, "generation_config_path");
            WorldMapGenerationConfig generationConfig = _load_generation_config(
                generationConfigPath
            );
            if (generationConfig == null)
                continue;
            GDictionary decodeResult = _save_serializer.decode_payload(
                payload,
                generationConfigPath,
                generationConfig,
                saveMeta
            );
            if (GetInt(decodeResult, "error", (int)Error.InvalidData) != (int)Error.Ok)
                continue;
            try
            {
                if (
                    _validate_decoded_party_identity_for_save(
                        ReadGodotObject(decodeResult, "party_state") as PartyState,
                        GetString(saveMeta, "save_id"),
                        "index_rebuild"
                    ) != (int)Error.Ok
                )
                {
                    continue;
                }
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            rebuiltById[GetString(saveMeta, "save_id")] = saveMeta;
        }
        saveDir.ListDirEnd();

        GDictionaryArray rebuiltEntries = new();
        foreach (var saveMetaValue in rebuiltById.Values)
        {
            if (TryUnboxToDictionary(saveMetaValue, out GDictionary saveMeta))
                rebuiltEntries.Add(saveMeta);
        }
        SortSaveMetaNewestFirst(rebuiltEntries);
        return rebuiltEntries;
    }

    public GDictionaryArray _merge_save_index_entries(
        GDictionaryArray primary_entries,
        GDictionaryArray fallback_entries
    )
    {
        return _save_serializer.merge_save_index_entries(primary_entries, fallback_entries);
    }

    public GDictionary _extract_save_meta_from_payload(GDictionary payload)
    {
        return _save_serializer.extract_save_meta_from_payload(payload);
    }

    public int _validate_decoded_party_identity_for_save(
        PartyState party_state,
        string save_id,
        StringName context
    )
    {
        GStringArray identityErrors = IdentityPayloadValidator.validate_party_identity_for_content_source(
            party_state,
            _progression_content_registry
        );
        if (identityErrors.Count == 0)
            return (int)Error.Ok;
        throw new InvalidOperationException(
            $"Save slot {save_id} has invalid party identity payload: {string.Join(", ", identityErrors)}"
        );
    }

    public GDictionaryArray _upsert_save_meta(GDictionaryArray entries, GDictionary save_meta)
    {
        return _save_serializer.upsert_save_meta(entries, save_meta);
    }

    public GDictionary _get_save_meta_by_id(string save_id)
    {
        foreach (GDictionary entry in _load_save_index_entries())
        {
            if (GetString(entry, "save_id") == save_id)
                return entry;
        }
        return new GDictionary();
    }

    public GDictionary _find_most_recent_save_by_config(string generation_config_path)
    {
        foreach (GDictionary entry in _load_save_index_entries())
        {
            if (GetString(entry, "generation_config_path") == generation_config_path)
                return entry;
        }
        return new GDictionary();
    }

    public GDictionary _normalize_save_meta(GDictionary raw_meta)
    {
        return _save_serializer.normalize_save_meta(raw_meta ?? new GDictionary());
    }

    public bool _sort_save_meta_newest_first(GDictionary a, GDictionary b)
    {
        return _save_serializer.sort_save_meta_newest_first(a, b);
    }

    public int _remove_directory_recursive(string virtual_path)
    {
        return FileIOCoordinator.remove_directory_recursive(
            virtual_path,
            _push_session_error
        );
    }

    public int _apply_character_creation_payload_to_main_character(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return (int)Error.Ok;
        if (_party_state == null)
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without party state."
            );
        }

        StringName mainMemberId = _party_state.get_resolved_main_character_member_id();
        if (mainMemberId == "")
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without a resolvable main character."
            );
        }

        PartyMemberState memberState = _party_state.get_member_state(mainMemberId);
        UnitProgress progression = memberState?.progression as UnitProgress;
        if (memberState == null || progression == null || progression.unit_base_attributes == null)
        {
            throw new InvalidOperationException(
                $"GameSession cannot apply character creation payload because main character {mainMemberId} is incomplete."
            );
        }

        if (
            !CharacterCreationService.apply_character_creation_payload_to_member_for_content_source(
                memberState,
                payload,
                _progression_content_registry,
                new GDictionary
                {
                    [CharacterCreationService.CREATION_OPTION_BAKE_REROLL_LUCK()] = true,
                }
            )
        )
        {
            throw new InvalidOperationException(
                $"GameSession rejected invalid character creation payload for main character {mainMemberId}."
            );
        }

        _revoke_orphan_racial_skills(_party_state);
        _backfill_racial_granted_skills(_party_state);
        return (int)Error.Ok;
    }

    public void _apply_character_creation_identity_payload(
        PartyMemberState member_state,
        GDictionary payload
    )
    {
        if (member_state == null || payload == null)
            return;
        member_state.race_id = _read_payload_string_name(
            payload,
            "race_id",
            member_state.race_id,
            false
        );
        member_state.subrace_id = _read_payload_string_name(
            payload,
            "subrace_id",
            member_state.subrace_id,
            false
        );
        member_state.age_years = _read_payload_nonnegative_int(
            payload,
            "age_years",
            member_state.age_years
        );
        member_state.birth_at_world_step = _read_payload_nonnegative_int(
            payload,
            "birth_at_world_step",
            member_state.birth_at_world_step
        );
        member_state.age_profile_id = _read_payload_string_name(
            payload,
            "age_profile_id",
            member_state.age_profile_id,
            false
        );
        member_state.natural_age_stage_id = _read_payload_string_name(
            payload,
            "natural_age_stage_id",
            member_state.natural_age_stage_id,
            false
        );
        member_state.effective_age_stage_id = _read_payload_string_name(
            payload,
            "effective_age_stage_id",
            member_state.effective_age_stage_id,
            false
        );
        member_state.effective_age_stage_source_type = _read_payload_string_name(
            payload,
            "effective_age_stage_source_type",
            member_state.effective_age_stage_source_type,
            true
        );
        member_state.effective_age_stage_source_id = _read_payload_string_name(
            payload,
            "effective_age_stage_source_id",
            member_state.effective_age_stage_source_id,
            true
        );
        member_state.body_size = Mathf.Max(
            _read_payload_nonnegative_int(payload, "body_size", member_state.body_size),
            1
        );
        member_state.body_size_category = _read_payload_string_name(
            payload,
            "body_size_category",
            member_state.body_size_category,
            false
        );
        member_state.versatility_pick = _read_payload_string_name(
            payload,
            "versatility_pick",
            member_state.versatility_pick,
            true
        );
        if (
            payload.ContainsKey("active_stage_advancement_modifier_ids")
            && HasArray(payload, "active_stage_advancement_modifier_ids")
        )
            member_state.active_stage_advancement_modifier_ids =
                ProgressionDataUtils.to_string_name_array(
                    payload["active_stage_advancement_modifier_ids"]
                );
        member_state.bloodline_id = _read_payload_string_name(
            payload,
            "bloodline_id",
            member_state.bloodline_id,
            true
        );
        member_state.bloodline_stage_id = _read_payload_string_name(
            payload,
            "bloodline_stage_id",
            member_state.bloodline_stage_id,
            true
        );
        member_state.ascension_id = _read_payload_string_name(
            payload,
            "ascension_id",
            member_state.ascension_id,
            true
        );
        member_state.ascension_stage_id = _read_payload_string_name(
            payload,
            "ascension_stage_id",
            member_state.ascension_stage_id,
            true
        );
        if (
            payload.ContainsKey("ascension_started_at_world_step")
            && HasInt(payload, "ascension_started_at_world_step")
        )
            member_state.ascension_started_at_world_step = Mathf.Max(
                payload["ascension_started_at_world_step"].AsInt32(),
                -1
            );
        member_state.original_race_id_before_ascension = _read_payload_string_name(
            payload,
            "original_race_id_before_ascension",
            member_state.original_race_id_before_ascension,
            true
        );
        member_state.biological_age_years = _read_payload_nonnegative_int(
            payload,
            "biological_age_years",
            member_state.biological_age_years
        );
        member_state.astral_memory_years = _read_payload_nonnegative_int(
            payload,
            "astral_memory_years",
            member_state.astral_memory_years
        );
        _refresh_member_body_size_from_identity(member_state);
    }

    public void _apply_initial_hp_formula(PartyMemberState member_state)
    {
        if (member_state?.progression is not UnitProgress progression)
            return;
        UnitBaseAttributes attributes = progression.unit_base_attributes;
        if (attributes == null)
            return;
        int constitution = attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION());
        int initialHpMax = CharacterCreationService.calculate_initial_hp_max(constitution);
        attributes.set_attribute_value(AttributeService.HP_MAX_ID(), initialHpMax);
        member_state.current_hp = initialHpMax;
    }

    public bool _refresh_member_body_size_from_identity(PartyMemberState member_state)
    {
        StringName category = _resolve_body_size_category_for_member(member_state);
        if (category == "")
            return false;
        int resolvedBodySize = BodySizeRules.get_body_size_for_category(category);
        if (
            member_state.body_size_category == category
            && member_state.body_size == resolvedBodySize
        )
            return false;
        member_state.body_size_category = category;
        member_state.body_size = resolvedBodySize;
        return true;
    }

    public StringName _resolve_body_size_category_for_member(PartyMemberState member_state)
    {
        if (member_state == null || _progression_content_registry == null)
            return "";
        AscensionStageDef ascensionStageDef = GetObject<AscensionStageDef>(
            _progression_content_registry.get_ascension_stage_defs(),
            member_state.ascension_stage_id
        );
        if (
            ascensionStageDef != null
            && ascensionStageDef.body_size_category_override != ""
            && BodySizeRules.is_valid_body_size_category(
                ascensionStageDef.body_size_category_override
            )
        )
        {
            return ascensionStageDef.body_size_category_override;
        }
        SubraceDef subraceDef = GetObject<SubraceDef>(
            _progression_content_registry.get_subrace_defs(),
            member_state.subrace_id
        );
        if (
            subraceDef != null
            && subraceDef.body_size_category_override != ""
            && BodySizeRules.is_valid_body_size_category(subraceDef.body_size_category_override)
        )
        {
            return subraceDef.body_size_category_override;
        }
        RaceDef raceDef = GetObject<RaceDef>(
            _progression_content_registry.get_race_defs(),
            member_state.race_id
        );
        if (
            raceDef != null
            && BodySizeRules.is_valid_body_size_category(raceDef.body_size_category)
        )
            return raceDef.body_size_category;
        return "";
    }

    public StringName _read_payload_string_name(
        GDictionary payload,
        string field_name,
        StringName fallback,
        bool allow_empty
    )
    {
        if (payload == null || !payload.ContainsKey(field_name))
            return fallback;
        if (!HasString(payload, field_name))
            return fallback;
        StringName parsed = GetStringName(payload, field_name);
        if (parsed == "" && !allow_empty)
            return fallback;
        return parsed;
    }

    public int _read_payload_nonnegative_int(GDictionary payload, string field_name, int fallback)
    {
        if (
            payload == null
            || !payload.ContainsKey(field_name)
            || !HasInt(payload, field_name)
        )
            return fallback;
        return Mathf.Max(payload[field_name].AsInt32(), 0);
    }

    public PartyState _create_default_party_state()
    {
        var partyState = new PartyState();
        partyState.gold = 180;

        PartyMemberState swordMember = _build_default_member_state(
            "player_sword_01",
            "剑士",
            "warrior_heavy_strike",
            "portrait_sword",
            0,
            4,
            2,
            3,
            1,
            1,
            1,
            12
        );

        partyState.set_member_state(swordMember);
        partyState.leader_member_id = "player_sword_01";
        partyState.main_character_member_id = "player_sword_01";
        partyState.active_member_ids = ProgressionDataUtils.to_string_name_array(
            new GArray { "player_sword_01" }
        );
        partyState.reserve_member_ids = ProgressionDataUtils.to_string_name_array(new GArray());
        return partyState;
    }

    public PartyMemberState _build_default_member_state(
        StringName member_id,
        string display_name,
        StringName starting_skill_id,
        StringName portrait_id,
        int current_mp,
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower,
        int storage_space = 0
    )
    {
        var memberState = new PartyMemberState
        {
            member_id = member_id,
            display_name = display_name,
            faction_id = "player",
            portrait_id = portrait_id,
            control_mode = "manual",
            current_mp = current_mp,
            body_size = 2,
            race_id = "human",
            subrace_id = "common_human",
            age_years = 24,
            birth_at_world_step = 0,
            age_profile_id = "human_age_profile",
            natural_age_stage_id = "adult",
            effective_age_stage_id = "adult",
            effective_age_stage_source_type = "",
            effective_age_stage_source_id = "",
            body_size_category = "medium",
            versatility_pick = "",
            active_stage_advancement_modifier_ids = new GStringNameArray(),
            bloodline_id = "",
            bloodline_stage_id = "",
            ascension_id = "",
            ascension_stage_id = "",
            ascension_started_at_world_step = -1,
            original_race_id_before_ascension = "",
            biological_age_years = 24,
            astral_memory_years = 0,
        };

        var progression = new UnitProgress
        {
            unit_id = member_id,
            display_name = display_name,
            character_level = 0,
        };

        var unitBaseAttributes = new UnitBaseAttributes
        {
            strength = strength,
            agility = agility,
            constitution = constitution,
            perception = perception,
            intelligence = intelligence,
            willpower = willpower,
        };
        int initialHpMax = CharacterCreationService.calculate_initial_hp_max(constitution);
        unitBaseAttributes.custom_stats["hp_max"] = initialHpMax;
        unitBaseAttributes.custom_stats["mp_max"] = current_mp;
        unitBaseAttributes.custom_stats["storage_space"] = Mathf.Max(storage_space, 0);
        memberState.current_hp = initialHpMax;
        progression.unit_base_attributes = unitBaseAttributes;

        var starterSkill = new UnitSkillProgress
        {
            skill_id = starting_skill_id,
            is_learned = true,
            is_core = true,
            assigned_profession_id = "warrior",
            granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PROFESSION(),
            granted_source_id = "warrior",
        };
        progression.set_skill_progress(starterSkill);

        var warriorProgress = new UnitProfessionProgress
        {
            profession_id = "warrior",
            rank = 0,
            is_active = false,
        };
        warriorProgress.add_core_skill(starting_skill_id);
        progression.set_profession_progress(warriorProgress);
        SkillDef randomStartingSkillDef = _grant_random_starting_book_skill(progression);
        _refresh_progression_runtime_state(progression);

        memberState.progression = progression;
        _equip_starting_weapon_for_skill(memberState, randomStartingSkillDef);
        return memberState;
    }

    public SkillDef _grant_random_starting_book_skill(UnitProgress progression)
    {
        if (progression == null || _skill_defs.Count == 0)
            return null;

        GStringNameArray eligibleSkillIds = new();
        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skill_defs))
        {
            StringName skillId = new(skillKey);
            SkillDef skillDef = GetObject<SkillDef>(_skill_defs, skillId);
            if (!_is_random_start_book_skill_candidate(skillDef, progression))
                continue;
            eligibleSkillIds.Add(skillId);
        }

        if (eligibleSkillIds.Count == 0)
            return null;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        StringName selectedSkillId = eligibleSkillIds[
            (int)rng.RandiRange(0, eligibleSkillIds.Count - 1)
        ];
        SkillDef selectedSkillDef = GetObject<SkillDef>(_skill_defs, selectedSkillId);
        if (selectedSkillDef == null)
            return null;

        UnitSkillProgress skillProgress = progression.get_skill_progress(selectedSkillId);
        if (skillProgress == null)
        {
            skillProgress = new UnitSkillProgress();
            skillProgress.skill_id = selectedSkillId;
        }

        skillProgress.is_learned = true;
        skillProgress.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PLAYER();
        skillProgress.granted_source_id = "";
        skillProgress.skill_level = _resolve_random_start_skill_initial_level(selectedSkillDef);
        skillProgress.current_mastery = 0;
        skillProgress.total_mastery_earned = 0;
        progression.set_skill_progress(skillProgress);
        return selectedSkillDef;
    }

    public void _equip_starting_weapon_for_skill(PartyMemberState member_state, SkillDef skill_def)
    {
        if (member_state?.equipment_state == null)
            return;
        StringName itemId = _resolve_starting_weapon_item_id_for_skill(skill_def);
        if (itemId == "")
            return;
        ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
        if (itemDef == null || !itemDef.is_weapon())
            return;
        StringName instanceId = allocate_equipment_instance_id();
        if (instanceId == "")
            return;
        EquipmentInstanceState equipmentInstance = EquipmentInstanceState.create_instance(
            itemId,
            instanceId
        );
        GStringNameArray occupiedSlots = itemDef.get_final_occupied_slot_ids(
            EquipmentRules.MAIN_HAND()
        );
        member_state.equipment_state.set_equipped_entry(
            EquipmentRules.MAIN_HAND(),
            itemId,
            occupiedSlots,
            equipmentInstance
        );
    }

    public StringName _resolve_starting_weapon_item_id_for_skill(SkillDef skill_def)
    {
        GStringNameArray candidates = new();
        if (
            _skill_matches_starting_weapon_type(
                skill_def,
                new GStringNameArray { "crossbow" },
                new GStringArray { "crossbow" }
            )
        )
            candidates.Add(StartingCrossbowWeaponItemId);
        if (
            _skill_matches_starting_weapon_type(
                skill_def,
                new GStringNameArray { "archer", "bow" },
                new GStringArray { "archer_" }
            )
        )
            candidates.Add(StartingArcherWeaponItemId);
        if (
            _skill_matches_starting_weapon_type(
                skill_def,
                new GStringNameArray { "mage", "magic", "spell" },
                new GStringArray { "mage_" }
            )
        )
            candidates.Add(StartingMageWeaponItemId);
        if (
            _skill_matches_starting_weapon_type(
                skill_def,
                new GStringNameArray { "priest", "faith", "heal" },
                new GStringArray { "priest_", "saint_" }
            )
        )
            candidates.Add(StartingPriestWeaponItemId);
        if (
            _skill_matches_starting_weapon_type(
                skill_def,
                new GStringNameArray { "warrior", "melee", "shield" },
                new GStringArray { "warrior_" }
            )
        )
            candidates.Add(StartingMeleeWeaponItemId);
        candidates.Add(StartingMeleeWeaponItemId);
        return _first_valid_starting_weapon_item_id(candidates);
    }

    public bool _skill_matches_starting_weapon_type(
        SkillDef skill_def,
        GStringNameArray tag_ids,
        GStringArray skill_id_prefixes
    )
    {
        if (skill_def == null)
            return false;
        foreach (StringName tagId in tag_ids)
        {
            if (skill_def.tags.Contains(tagId))
                return true;
        }
        string skillIdText = skill_def.skill_id.ToString();
        foreach (string prefix in skill_id_prefixes)
        {
            if (skillIdText.StartsWith(prefix))
                return true;
        }
        return false;
    }

    public StringName _first_valid_starting_weapon_item_id(GStringNameArray candidates)
    {
        foreach (StringName itemId in candidates)
        {
            if (itemId == "")
                continue;
            ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
            if (itemDef != null && itemDef.is_weapon())
                return itemId;
        }
        return "";
    }

    public void _refresh_progression_runtime_state(UnitProgress progression)
    {
        if (progression == null)
            return;
        var progressionService = new ProgressionService();
        progressionService.setup(progression, _skill_defs, _profession_defs);
        progressionService.refresh_runtime_state();
    }

    public bool _is_random_start_book_skill_candidate(SkillDef skill_def, UnitProgress progression)
    {
        if (skill_def == null || skill_def.skill_id == "")
            return false;
        if (skill_def.learn_source != "book")
            return false;
        if (skill_def.unlock_mode == "composite_upgrade")
            return false;
        if (
            skill_def.learn_requirements.Count > 0
            || skill_def.knowledge_requirements.Count > 0
            || skill_def.skill_level_requirements.Count > 0
            || skill_def.attribute_requirements.Count > 0
            || skill_def.achievement_requirements.Count > 0
        )
        {
            return false;
        }
        UnitSkillProgress learnedProgress = progression?.get_skill_progress(skill_def.skill_id);
        return learnedProgress == null || !learnedProgress.is_learned;
    }

    public int _resolve_random_start_skill_initial_level(SkillDef skill_def)
    {
        return _resolve_random_start_skill_initial_level(skill_def, null);
    }

    public int _resolve_random_start_skill_initial_level(
        SkillDef skill_def,
        UnitProgress progression
    )
    {
        if (skill_def == null)
            return 0;
        int mappedLevel = GetInt(
            RandomStartSkillLevelByTier,
            _resolve_random_start_skill_tier(skill_def),
            0
        );
        int maxInitialLevel = skill_def.max_level >= 0 ? Mathf.Max(skill_def.max_level, 0) : 999;
        if (progression != null && skill_def.dynamic_max_level_stat_id != "")
        {
            int effectiveMax = SkillEffectiveMaxLevelRules.get_effective_max_level(
                skill_def,
                null,
                progression
            );
            if (effectiveMax > 0)
                maxInitialLevel = Mathf.Min(maxInitialLevel, effectiveMax);
        }
        if (skill_def.non_core_max_level > 0)
            maxInitialLevel = Mathf.Min(maxInitialLevel, skill_def.non_core_max_level);
        return Mathf.Clamp(mappedLevel, 0, maxInitialLevel);
    }

    public StringName _resolve_random_start_skill_tier(SkillDef skill_def)
    {
        if (skill_def == null)
            return RandomStartSkillTierBasic;

        string description = skill_def.description ?? "";
        if (_description_contains_any_keyword(description, RandomStartSkillKeywordsUltimate))
            return RandomStartSkillTierUltimate;
        if (_description_contains_any_keyword(description, RandomStartSkillKeywordsAdvanced))
            return RandomStartSkillTierAdvanced;
        if (_description_contains_any_keyword(description, RandomStartSkillKeywordsIntermediate))
            return RandomStartSkillTierIntermediate;
        if (_description_contains_any_keyword(description, RandomStartSkillKeywordsBasic))
            return RandomStartSkillTierBasic;

        int tierScore = _build_random_start_skill_tier_score(skill_def);
        if (tierScore >= 14)
            return RandomStartSkillTierUltimate;
        if (tierScore >= 9)
            return RandomStartSkillTierAdvanced;
        if (tierScore >= 6)
            return RandomStartSkillTierIntermediate;
        return RandomStartSkillTierBasic;
    }

    public bool _description_contains_any_keyword(string description, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if ((description ?? "").Contains(keyword))
                return true;
        }
        return false;
    }

    public int _build_random_start_skill_tier_score(SkillDef skill_def)
    {
        if (skill_def == null || skill_def.combat_profile == null)
            return 0;

        CombatSkillDef combatProfile = skill_def.combat_profile;
        int score = 0;
        score += combatProfile.ap_cost * 2;
        score += combatProfile.mp_cost;
        score += combatProfile.stamina_cost;
        score += combatProfile.aura_cost * 2;
        score += Mathf.Max(combatProfile.cooldown_tu / 5 - 1, 0);
        if (BattleTypedNames.ToTargetMode(combatProfile.target_mode) == BattleTargetMode.Ground)
            score += 1;
        var areaPattern = BattleTypedNames.ToAreaPattern(combatProfile.area_pattern);
        if (areaPattern != BattleAreaPattern.Unknown && areaPattern != BattleAreaPattern.Single)
            score += 1;
        if (skill_def.tags.Contains("aoe"))
            score += 1;
        if (skill_def.tags.Contains("finisher"))
            score += 2;
        if (skill_def.unlock_mode == "composite_upgrade")
            score += 2;
        return score;
    }

    public PartyState _normalize_party_state(PartyState party_state)
    {
        return _save_serializer.normalize_party_state(party_state) ?? new PartyState();
    }

    public GDictionary _normalize_world_data(GDictionary world_data)
    {
        return _save_serializer.normalize_world_data(world_data ?? new GDictionary());
    }

    public GDictionary _serialize_world_data(GDictionary world_data)
    {
        return _save_serializer.serialize_world_data(world_data ?? new GDictionary());
    }

    public void _rotate_log_session()
    {
        _log_service?.start_new_session();
    }

    public GDictionary _capture_runtime_state()
    {
        return new GDictionary
        {
            ["active_save_id"] = _active_save_id,
            ["active_save_path"] = _active_save_path,
            ["active_save_meta"] = _active_save_meta.Duplicate(true),
            ["generation_config_path"] = _generation_config_path,
            ["generation_config"] = _generation_config,
            ["world_data"] = _world_data.Duplicate(true),
            ["player_coord"] = _player_coord,
            ["player_faction_id"] = _player_faction_id,
            ["party_state"] = _party_state,
            ["has_active_world"] = _has_active_world,
            ["battle_save_lock_enabled"] = _battle_save_lock_enabled,
            ["battle_save_dirty"] = _battle_save_dirty,
            ["runtime_save_dirty"] = _runtime_save_dirty,
            ["runtime_save_dirty_scopes"] = _runtime_save_dirty_scopes.Duplicate(),
            ["last_save_error"] = _last_save_error,
            ["last_save_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = _post_decode_save_reasons.Duplicate(),
        };
    }

    public void _restore_runtime_state(GDictionary state)
    {
        _active_save_id = GetString(state, "active_save_id");
        _active_save_path = GetString(state, "active_save_path");
        _active_save_meta = GetDictionary(state, "active_save_meta").Duplicate(true);
        _generation_config_path = GetString(state, "generation_config_path");
        _generation_config =
            ReadGodotObject(state, "generation_config") as WorldMapGenerationConfig;
        _world_data = GetDictionary(state, "world_data").Duplicate(true);
        _player_coord = GetVector2I(state, "player_coord", Vector2I.Zero);
        _player_faction_id = GetString(state, "player_faction_id", "player");
        _party_state =
            (ReadGodotObject(state, "party_state") ?? new PartyState()) as PartyState
            ?? new PartyState();
        _has_active_world = ReadExactBool(state, "has_active_world", false);
        _battle_save_lock_enabled = ReadExactBool(state, "battle_save_lock_enabled", false);
        _battle_save_dirty = ReadExactBool(state, "battle_save_dirty", false);
        _runtime_save_dirty = ReadExactBool(state, "runtime_save_dirty", false);
        _runtime_save_dirty_scopes = ProgressionDataUtils.to_string_name_array(
            GetArray(state, "runtime_save_dirty_scopes")
        );
        _last_save_error = GetInt(state, "last_save_error", (int)Error.Ok);
        _last_save_error_reason = ProgressionDataUtils.to_string_name(
            GetString(state, "last_save_error_reason")
        );
        _post_decode_save_pending = ReadExactBool(state, "post_decode_save_pending", false);
        _post_decode_save_reasons = ProgressionDataUtils.to_string_name_array(
            GetArray(state, "post_decode_save_reasons")
        );
    }

    public void _reset_runtime_state()
    {
        _active_save_id = "";
        _active_save_path = "";
        _active_save_meta = new GDictionary();
        _generation_config_path = "";
        _generation_config = null;
        _world_data = new GDictionary();
        _player_coord = Vector2I.Zero;
        _player_faction_id = "player";
        _party_state = new PartyState();
        _has_active_world = false;
        _battle_save_lock_enabled = false;
        _battle_save_dirty = false;
        _runtime_save_dirty = false;
        _runtime_save_dirty_scopes.Clear();
        _last_save_error = (int)Error.Ok;
        _last_save_error_reason = "";
        _post_decode_save_pending = false;
        _post_decode_save_reasons.Clear();
    }

    public void _refresh_progression_content()
    {
        if (_progression_content_registry == null)
            return;
        _skill_defs = _progression_content_registry.get_skill_defs();
        _profession_defs = _progression_content_registry.get_profession_defs();
        _achievement_defs = _progression_content_registry.get_achievement_defs();
        _quest_defs = _progression_content_registry.get_quest_defs();
    }

    public void _refresh_battle_special_profiles()
    {
        if (_battle_special_profile_registry == null)
            return;
        _battle_special_profile_registry.rebuild(_skill_defs);
    }

    public void _refresh_item_content()
    {
        if (_item_content_registry == null)
            return;
        _item_defs = _item_content_registry.get_item_defs().Duplicate();
        if (_skill_book_item_factory != null)
        {
            GDictionary generatedSkillBookDefs = _skill_book_item_factory.build_generated_item_defs(
                _skill_defs,
                _item_defs
            );
            foreach (var itemId in generatedSkillBookDefs.Keys)
                _item_defs[itemId] = generatedSkillBookDefs[itemId];
        }
    }

    public void _refresh_recipe_content()
    {
        if (_recipe_content_registry == null)
            return;
        _recipe_content_registry.setup(_item_defs);
        _recipe_defs = _recipe_content_registry.get_recipe_defs().Duplicate();
    }

    public void _refresh_enemy_content()
    {
        if (_enemy_content_registry == null)
            return;
        _enemy_templates = _enemy_content_registry.get_enemy_templates();
        _enemy_ai_brains = _enemy_content_registry.get_enemy_ai_brains();
        _wild_encounter_rosters = _enemy_content_registry.get_wild_encounter_rosters();
    }

    public void _refresh_content_validation_snapshot()
    {
        _refresh_battle_special_profiles();
        GDictionary domainSnapshots = new()
        {
            ["progression"] = _build_content_validation_domain_snapshot(
                _progression_content_registry
            ),
            ["battle_special_profile"] = _build_content_validation_domain_snapshot(
                _battle_special_profile_registry
            ),
            ["item"] = _build_item_content_validation_domain_snapshot(),
            ["recipe"] = _build_content_validation_domain_snapshot(_recipe_content_registry),
            ["enemy"] = _build_content_validation_domain_snapshot(_enemy_content_registry),
            ["world"] = _build_world_content_validation_domain_snapshot(),
            ["quest"] = _build_quest_content_validation_domain_snapshot(),
        };
        int errorCount = 0;
        foreach (string domainId in ContentValidationDomainOrder)
            errorCount += GetInt(GetDictionary(domainSnapshots, domainId), "error_count", 0);
        _content_validation_snapshot = new GDictionary
        {
            ["ok"] = errorCount == 0,
            ["error_count"] = errorCount,
            ["domain_order"] = BuildDomainOrderArray(),
            ["domains"] = domainSnapshots,
        };
    }

    public GDictionary _build_content_validation_domain_snapshot(IValidatableRegistry registry)
    {
        GStringArray errors = new();
        if (registry != null)
        {
            foreach (var validationError in registry.validate())
                errors.Add(validationError);
        }
        return new GDictionary
        {
            ["ok"] = errors.Count == 0,
            ["error_count"] = errors.Count,
            ["errors"] = errors,
        };
    }

    public GDictionary _build_world_content_validation_domain_snapshot()
    {
        GStringArray errors = new();
        if (_world_content_validator != null)
        {
            foreach (
                string validationError in _world_content_validator.validate_world_presets(
                    _enemy_templates,
                    _wild_encounter_rosters
                )
            )
                errors.Add(validationError);
        }
        return new GDictionary
        {
            ["ok"] = errors.Count == 0,
            ["error_count"] = errors.Count,
            ["errors"] = errors,
        };
    }

    public GDictionary _build_item_content_validation_domain_snapshot()
    {
        GStringArray errors = new();
        if (_item_content_registry != null)
        {
            foreach (var validationError in _item_content_registry.validate())
                errors.Add(validationError);
        }
        if (
            _item_defs != null
            && _skill_defs != null
            && _item_defs.Count > 0
            && _skill_defs.Count > 0
        )
        {
            foreach (
                string skillBookError in SkillBookItemContentValidator.validate(
                    _item_defs,
                    _skill_defs
                )
            )
                errors.Add(skillBookError);
        }
        return new GDictionary
        {
            ["ok"] = errors.Count == 0,
            ["error_count"] = errors.Count,
            ["errors"] = errors,
        };
    }

    public GDictionary _build_quest_content_validation_domain_snapshot()
    {
        GStringArray errors = new();
        GStringArray registrationErrors = new();
        if (_progression_content_registry != null)
        {
            foreach (
                string registrationError in _progression_content_registry.get_quest_registration_errors()
            )
                registrationErrors.Add(registrationError);
        }
        foreach (
            string validationError in QuestContentValidator.validate(
                _quest_defs,
                _item_defs,
                _skill_defs,
                _enemy_templates,
                registrationErrors
            )
        )
        {
            errors.Add(validationError);
        }
        return new GDictionary
        {
            ["ok"] = errors.Count == 0,
            ["error_count"] = errors.Count,
            ["errors"] = errors,
        };
    }

    public int _require_content_validation_for_runtime(StringName operation_id)
    {
        _refresh_content_validation_snapshot();
        if (is_content_validation_ok())
            return (int)Error.Ok;
        int errorCount = GetInt(_content_validation_snapshot, "error_count", 0);
        _push_session_error(
            "session.content.validation_blocked",
            "GameSession blocked formal runtime entry because content validation failed.",
            Json.Stringify(new GDictionary
            {
                ["operation_id"] = operation_id.ToString(),
                ["error_count"] = errorCount,
            })
        );
        return (int)Error.InvalidData;
    }

    public void _report_content_validation_errors()
    {
        GDictionary domains = GetDictionary(_content_validation_snapshot, "domains");
        foreach (string domainId in ContentValidationDomainOrder)
        {
            GDictionary domainSnapshot = GetDictionary(domains, domainId);
            GArray errorsArray = GetArray(domainSnapshot, "errors");
            foreach (var validationErrorValue in errorsArray)
                _report_content_validation_error(domainId, validationErrorValue.AsString());
        }
    }

    public void _report_content_validation_error(string domain_id, string validation_error)
    {
        switch (domain_id)
        {
            case "progression":
                _push_session_error(
                    "session.content.progression_validation_failed",
                    $"Progression content error: {validation_error}"
                );
                break;
            case "battle_special_profile":
                _push_session_error(
                    "session.content.battle_special_profile_validation_failed",
                    $"Battle special profile content error: {validation_error}"
                );
                break;
            case "item":
                _push_session_error(
                    "session.content.item_validation_failed",
                    $"Item content error: {validation_error}"
                );
                break;
            case "recipe":
                _push_session_error(
                    "session.content.recipe_validation_failed",
                    $"Recipe content error: {validation_error}"
                );
                break;
            case "enemy":
                _push_session_error(
                    "session.content.enemy_validation_failed",
                    $"Enemy content error: {validation_error}"
                );
                break;
            case "world":
                _push_session_error(
                    "session.content.world_validation_failed",
                    $"World content error: {validation_error}"
                );
                break;
            case "quest":
                _push_session_error(
                    "session.content.quest_validation_failed",
                    $"Quest content error: {validation_error}"
                );
                break;
        }
    }

    public void _log_session_info(string event_id, string message)
    {
        log_event("info", "session", event_id, message, "");
    }

    public void _log_session_info(string event_id, string message, string context)
    {
        GameLog.Info(message, event_id, "session", context);
    }

    public void _push_session_error(string event_id, string message)
    {
        _push_session_error(event_id, message, "");
    }

    public void _push_session_error(string event_id, string message, string context)
    {
        GameLog.Error(message, event_id, "session", context);
    }

    private static GDictionary GetDictionary(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static string GetString(GDictionary dictionary, object key, string fallback = "")
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName GetStringName(
        GDictionary dictionary,
        object key,
        StringName fallback = default
    )
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback ?? new StringName("");
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? new StringName(""),
        };
    }

    private static int GetInt(GDictionary dictionary, object key, int fallback = 0)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadExactBool(GDictionary dictionary, object key, bool fallback = false)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Vector2I GetVector2I(GDictionary dictionary, object key, Vector2I fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static T GetObject<T>(GDictionary dictionary, object key)
        where T : GodotObject
    {
        return ReadGodotObject(dictionary, key) as T;
    }

    private static bool TryRead(GDictionary dictionary, object key, out Variant value)
    {
        if (dictionary == null || key == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return true;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool HasArray(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Array;

    private static bool HasInt(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Int;

    private static bool HasString(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value)
        && (
            value.VariantType == Variant.Type.String
            || value.VariantType == Variant.Type.StringName
        );

    private static bool TryUnboxToDictionary(Variant value, out GDictionary dictionary)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            dictionary = value.AsGodotDictionary();
            return true;
        }
        dictionary = default;
        return false;
    }

    private static GodotObject ReadGodotObject(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() : null;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        foreach (Variant value in values ?? new GArray())
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static GArray ToUntypedArray(GDictionaryArray entries)
    {
        GArray raw = new();
        if (entries == null)
            return raw;
        foreach (GDictionary entry in entries)
            raw.Add(entry);
        return raw;
    }

    private static GArray BuildDomainOrderArray()
    {
        GArray order = new();
        foreach (string domainId in ContentValidationDomainOrder)
            order.Add(domainId);
        return order;
    }

    private void SortSaveMetaNewestFirst(GDictionaryArray entries)
    {
        var list = new List<GDictionary>();
        foreach (GDictionary entry in entries)
            list.Add(entry);
        list.Sort(
            (a, b) =>
            {
                if (_sort_save_meta_newest_first(a, b))
                    return -1;
                if (_sort_save_meta_newest_first(b, a))
                    return 1;
                return 0;
            }
        );
        entries.Clear();
        foreach (GDictionary entry in list)
            entries.Add(entry);
    }

    private static Variant ToVariant(object value)
    {
        return value switch
        {
            null => default,
            Variant variantValue => variantValue,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vectorValue => vectorValue,
            GDictionary dictionaryValue => dictionaryValue,
            GArray arrayValue => arrayValue,
            GodotObject objectValue => objectValue,
            _ => value.ToString(),
        };
    }

    private static bool VariantEquals(object leftValue, object rightValue)
    {
        var left = ToVariant(leftValue);
        var right = ToVariant(rightValue);
        if (left.VariantType != right.VariantType)
            return false;
        return left.Obj == right.Obj;
    }
}

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

    private sealed class ContentValidationDomainSnapshotData
    {
        public List<string> Errors { get; } = new();

        public bool Ok => Errors.Count == 0;

        public int ErrorCount => Errors.Count;

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["ok"] = Ok,
                ["error_count"] = ErrorCount,
                ["errors"] = ToGodotStringArray(Errors),
            };
        }
    }

    private sealed class ContentValidationSnapshotData
    {
        public Dictionary<string, ContentValidationDomainSnapshotData> Domains { get; } =
            new(StringComparer.Ordinal);

        public int ErrorCount
        {
            get
            {
                int errorCount = 0;
                foreach (string domainId in ContentValidationDomainOrder)
                {
                    if (Domains.TryGetValue(domainId, out ContentValidationDomainSnapshotData domain))
                        errorCount += domain?.ErrorCount ?? 0;
                }
                return errorCount;
            }
        }

        public bool Ok => ErrorCount == 0;

        public IEnumerable<string> EnumerateDomainErrors(string domainId)
        {
            if (!Domains.TryGetValue(domainId, out ContentValidationDomainSnapshotData domain))
                return Array.Empty<string>();
            if (domain == null)
                return Array.Empty<string>();
            return domain.Errors;
        }

        public GDictionary ToDictionary()
        {
            GDictionary domainSnapshots = new();
            foreach (string domainId in ContentValidationDomainOrder)
            {
                domainSnapshots[domainId] = Domains.TryGetValue(
                    domainId,
                    out ContentValidationDomainSnapshotData domain
                )
                    ? domain?.ToDictionary() ?? new GDictionary()
                    : new GDictionary();
            }
            return new GDictionary
            {
                ["ok"] = Ok,
                ["error_count"] = ErrorCount,
                ["domain_order"] = BuildDomainOrderArray(),
                ["domains"] = domainSnapshots,
            };
        }
    }

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

    internal string _active_save_id = "";
    internal string _active_save_path = "";
    internal GDictionary _active_save_meta = new();
    internal string _generation_config_path = "";
    internal WorldMapGenerationConfig _generation_config;
    internal GDictionary _world_data = new();
    internal Vector2I _player_coord = Vector2I.Zero;
    internal string _player_faction_id = "player";
    internal PartyState _party_state = new();
    internal bool _has_active_world;
    internal bool _battle_save_lock_enabled;
    internal bool _battle_save_dirty;
    internal bool _runtime_save_dirty;
    internal GStringNameArray _runtime_save_dirty_scopes = new();
    internal int _last_save_error = (int)Error.Ok;
    internal StringName _last_save_error_reason = "";
    internal bool _post_decode_save_pending;
    internal GStringNameArray _post_decode_save_reasons = new();

    public ProgressionContentRegistry _progression_content_registry = new();
    public ItemContentRegistry _item_content_registry = new();
    public RecipeContentRegistry _recipe_content_registry = new();
    public EnemyContentRegistry _enemy_content_registry = new();
    internal BattleSpecialProfileRegistry _battle_special_profile_registry = new();
    internal GameRoot _game_root = new();

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
    private ContentValidationSnapshotData _contentValidationSnapshotData = new();
    private Dictionary<StringName, QuestDef> _questDefIndex = new();

    public SaveSerializer _save_serializer = new();
    private GameLogService _log_service = new();
    public WorldMapContentValidator _world_content_validator = new();

    public GDictionaryArray _save_index_entries_cache = new();
    public bool _save_index_cache_valid;
    public GDictionary _save_index_cache_signature = new();

    public bool fail_payload_write;

    public GameSession()
    {
        EnsureGameRoot();
        _save_serializer.Setup(
            SaveVersion,
            SaveIndexVersion,
            MaxActiveMemberCount
        );

        RefreshProgressionContent();
        RefreshBattleSpecialProfiles();
        RefreshItemContent();
        RefreshRecipeContent();
        RefreshEnemyContent();
        RefreshContentValidationSnapshot();
        ReportContentValidationErrors();

        GameLog.AddSink(new GameSessionLogSink(this));
    }

    internal void DisposeOwnedRuntimeResources()
    {
        DisposeOwned(_game_root, root => root.DisposeOwnedRuntimeResources());
        _game_root = null;
        DisposeOwned(_progression_content_registry, registry => registry.Dispose());
        DisposeOwned(_item_content_registry, registry => registry.Dispose());
        DisposeOwned(_recipe_content_registry, registry => registry.Dispose());
        DisposeOwned(_enemy_content_registry, _ => { });
        DisposeOwned(_battle_special_profile_registry, _ => { });
        DisposeOwned(_save_serializer, _ => { });
        DisposeOwned(_world_content_validator, _ => { });
    }

    public int EnsureWorldReady(string generation_config_path)
    {
        int contentValidationError = RequireContentValidationForRuntime("ensure_world_ready");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;
        if (_has_active_world && _generation_config_path == generation_config_path)
            return (int)Error.Ok;
        if (TryLoadGameState(generation_config_path))
            return (int)Error.Ok;
        return StartNewGame(generation_config_path);
    }

    private static void DisposeOwned<T>(T owned, Action<T> cleanup)
        where T : GodotObject
    {
        if (owned == null || !GodotObject.IsInstanceValid(owned))
            return;
        GC.SuppressFinalize(owned);
        cleanup?.Invoke(owned);
        if (GodotObject.IsInstanceValid(owned))
            owned.Dispose();
    }

    private GameRoot EnsureGameRoot()
    {
        if (_game_root == null || !GodotObject.IsInstanceValid(_game_root))
        {
            _game_root = new GameRoot();
        }
        _game_root.BindSession(this);
        return _game_root;
    }

    public int StartNewGame(string generation_config_path)
    {
        string presetName = WorldPresetRegistry.GetFallbackPresetName(generation_config_path);
        return CreateNewSave(generation_config_path, "", presetName, new GDictionary());
    }

    public int CreateNewSave(string generation_config_path)
    {
        return CreateNewSave(generation_config_path, "", "", new GDictionary());
    }

    public int CreateNewSave(string generation_config_path, StringName preset_id)
    {
        return CreateNewSave(generation_config_path, preset_id, "", new GDictionary());
    }

    public int CreateNewSave(
        string generation_config_path,
        StringName preset_id,
        string preset_name
    )
    {
        return CreateNewSave(generation_config_path, preset_id, preset_name, new GDictionary());
    }

    public int CreateNewSave(
        string generation_config_path,
        StringName preset_id,
        string preset_name,
        GDictionary character_creation_payload
    )
    {
        character_creation_payload ??= new GDictionary();
        int contentValidationError = RequireContentValidationForRuntime("create_new_save");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;

        GDictionary previousRuntimeState = CaptureRuntimeState();
        if (string.IsNullOrEmpty(generation_config_path))
        {
            throw new InvalidOperationException(
                "GameSession requires a generation config path."
            );
        }

        WorldMapGenerationConfig generationConfig = LoadGenerationConfig(generation_config_path);
        if (generationConfig == null)
            return (int)Error.CantOpen;

        int prepareError = PrepareNewWorld(generation_config_path, generationConfig);
        if (prepareError != (int)Error.Ok)
        {
            RestoreRuntimeState(previousRuntimeState);
            return prepareError;
        }

        int characterCreationError = ApplyCharacterCreationPayloadToMainCharacter(
            character_creation_payload
        );
        if (characterCreationError != (int)Error.Ok)
        {
            RestoreRuntimeState(previousRuntimeState);
            return characterCreationError;
        }

        int timestamp = (int)Time.GetUnixTimeFromSystem();
        string saveId = GenerateUniqueSaveId(timestamp);
        if (string.IsNullOrEmpty(saveId))
        {
            RestoreRuntimeState(previousRuntimeState);
            throw new InvalidOperationException(
                "GameSession failed to allocate a unique save id."
            );
        }

        _active_save_id = saveId;
        _active_save_path = BuildSaveFilePath(saveId);
        string resolvedPresetName = string.IsNullOrEmpty(preset_name)
            ? WorldPresetRegistry.GetFallbackPresetName(generation_config_path)
            : preset_name;
        _active_save_meta = BuildSaveMeta(
            saveId,
            saveId,
            generation_config_path,
            preset_id,
            resolvedPresetName,
            generationConfig.GetWorldSizeCells(),
            timestamp,
            timestamp
        );
        RotateLogSession();

        int persistError = PersistGameState();
        if (persistError == (int)Error.Ok)
        {
            LogSessionInfo(
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
            RestoreRuntimeState(previousRuntimeState);
        }
        return persistError;
    }

    public GDictionaryArray ListSaveSlots() => LoadSaveIndexEntries();

    public GDictionaryArray PeekSaveSlots() => PeekSaveIndexEntriesReadOnly();

    public int LoadSave(string save_id)
    {
        if (!_save_serializer.IsValidSaveIdToken(save_id))
            return (int)Error.InvalidParameter;
        int contentValidationError = RequireContentValidationForRuntime("load_save");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;

        GDictionary saveMeta = GetSaveMetaById(save_id);
        if (saveMeta.Count == 0)
        {
            throw new InvalidOperationException(
                $"GameSession could not find save slot {save_id}."
            );
        }

        string savePath = BuildSaveFilePath(save_id);
        GDictionary readResult = ReadSavePayload(savePath);
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

        GDictionary payload = _save_serializer.RestoreMinimizedSavePayloadStrings(
            payloadValue.AsGodotDictionary()
        );
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

        WorldMapGenerationConfig generationConfig = LoadGenerationConfig(generationConfigPath);
        if (generationConfig == null)
            return (int)Error.CantOpen;

        GDictionary previousRuntimeState = CaptureRuntimeState();
        int loadError = LoadCurrentPayload(
            payload,
            generationConfigPath,
            generationConfig,
            saveMeta
        );
        if (loadError == (int)Error.Ok)
        {
            RotateLogSession();
            LogSessionInfo(
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
            RestoreRuntimeState(previousRuntimeState);
        }
        return loadError;
    }

    public bool HasActiveWorld() => _has_active_world;

    public string GetActiveSaveId() => _active_save_id;

    public string GetActiveSavePath() => _active_save_path;

    public GDictionary GetActiveSaveMeta() => _active_save_meta.Duplicate(true);

    internal GameLogService GetLogService() => _log_service;

    public GArray GetRecentLogs() => GetRecentLogs(50);

    public GArray GetRecentLogs(int limit = 50) =>
        _log_service != null ? _log_service.GetRecentEntries(limit) : new GArray();

    public GDictionary GetLogSnapshot() => GetLogSnapshot(50);

    public GDictionary GetLogSnapshot(int limit = 50) =>
        _log_service != null ? _log_service.BuildSnapshot(limit) : new GDictionary();

    public string GetActiveLogFilePath() =>
        _log_service != null ? _log_service.GetLogPath() : "";

    public string AllocateUniqueSaveId() => AllocateUniqueSaveId("save");

    public string AllocateUniqueSaveId(string prefix = "save") =>
        GenerateUniqueSaveId((int)Time.GetUnixTimeFromSystem(), prefix);

    public GDictionary GetContentValidationSnapshot() =>
        _content_validation_snapshot.Duplicate(true);

    public GDictionary RefreshContentValidationSnapshot()
    {
        RefreshContentValidationSnapshotState();
        return GetContentValidationSnapshot();
    }

    public bool IsContentValidationOk() => _contentValidationSnapshotData?.Ok ?? false;

    public GDictionary LogEvent(
        string level,
        string domain,
        string event_id,
        string message,
        string context = ""
    )
    {
        return _log_service != null
            ? _log_service.AppendEntry(
                level,
                domain,
                event_id,
                message,
                context
            )
            : new GDictionary();
    }

    public GDictionary LogEvent(string level, string domain, string event_id, string message)
    {
        return LogEvent(level, domain, event_id, message, "");
    }

    public WorldMapGenerationConfig GetGenerationConfig() => _generation_config;

    public string GetGenerationConfigPath() => _generation_config_path;

    public GDictionary GetWorldData() => _world_data;

    public StringName AllocateEquipmentInstanceId()
    {
        if (_world_data == null || !_world_data.ContainsKey(WorldEquipmentInstanceSerialKey))
            return "";
        GDictionary usedIds = CollectPersistentEquipmentInstanceIds();
        int serial = GetInt(_world_data, WorldEquipmentInstanceSerialKey, 0);
        if (serial < 1)
            return "";
        while (true)
        {
            StringName candidate = EquipmentInstanceState.FormatInstanceId(serial);
            serial += 1;
            _world_data[WorldEquipmentInstanceSerialKey] = serial;
            if (!usedIds.ContainsKey(candidate.ToString()))
            {
                MarkRuntimeStateDirty(SaveDirtyScopeWorldData);
                return candidate;
            }
        }
    }

    public int SetWorldData(GDictionary world_data)
    {
        GDictionary normalizedWorldData = NormalizeWorldData(world_data ?? new GDictionary());
        if (normalizedWorldData.Count == 0)
            return (int)Error.InvalidData;
        _world_data = normalizedWorldData;
        MarkRuntimeStateDirty(SaveDirtyScopeWorldData);
        return (int)Error.Ok;
    }

    public Vector2I GetPlayerCoord() => _player_coord;

    public int SetPlayerCoord(Vector2I coord)
    {
        _player_coord = coord;
        MarkRuntimeStateDirty(SaveDirtyScopePlayerCoord);
        return (int)Error.Ok;
    }

    public string GetPlayerFactionId() => _player_faction_id;

    public int SetPlayerFactionId(string faction_id)
    {
        _player_faction_id = faction_id;
        MarkRuntimeStateDirty(SaveDirtyScopePlayerFactionId);
        return (int)Error.Ok;
    }

    public PartyState GetPartyState() => _party_state;

    public int SetPartyState(PartyState party_state)
    {
        _party_state = NormalizePartyState(party_state);
        MarkRuntimeStateDirty(SaveDirtyScopePartyState);
        return (int)Error.Ok;
    }

    public void SetBattleSaveLock(bool enabled) => _battle_save_lock_enabled = enabled;

    public bool IsBattleSaveLocked() => _battle_save_lock_enabled;

    public bool HasPendingSave() =>
        _runtime_save_dirty || _battle_save_dirty || _post_decode_save_pending;

    public void DiscardPendingSave()
    {
        _battle_save_dirty = false;
        _runtime_save_dirty = false;
        _runtime_save_dirty_scopes.Clear();
        _post_decode_save_pending = false;
        _post_decode_save_reasons.Clear();
    }

    public GDictionary GetSaveStatus()
    {
        return new GDictionary
        {
            ["has_pending_save"] = HasPendingSave(),
            ["dirty_scopes"] = _runtime_save_dirty_scopes.Duplicate(),
            ["battle_save_locked"] = _battle_save_lock_enabled,
            ["last_error"] = _last_save_error,
            ["last_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = _post_decode_save_reasons.Duplicate(),
        };
    }

    private void MarkRuntimeStateDirty(StringName scope)
    {
        _runtime_save_dirty = true;
        if (scope == "" || _runtime_save_dirty_scopes.Contains(scope))
            return;
        _runtime_save_dirty_scopes.Add(scope);
    }

    private void ClearRuntimeSaveDirty()
    {
        _battle_save_dirty = false;
        _runtime_save_dirty = false;
        _runtime_save_dirty_scopes.Clear();
        _post_decode_save_pending = false;
        _post_decode_save_reasons.Clear();
    }

    private void RecordSaveError(int error_code, StringName reason)
    {
        _last_save_error = error_code;
        _last_save_error_reason = reason;
    }

    private void ClearLastSaveError()
    {
        _last_save_error = (int)Error.Ok;
        _last_save_error_reason = "";
    }

    public void QueuePostDecodeSave(StringName reason)
    {
        _post_decode_save_pending = true;
        MarkRuntimeStateDirty(SaveDirtyScopePostDecodeRepair);
        if (reason == "" || _post_decode_save_reasons.Contains(reason))
            return;
        _post_decode_save_reasons.Add(reason);
    }

    public PartyMemberState GetPartyMemberState(StringName member_id)
    {
        return _party_state?.GetMemberState(member_id);
    }

    public PartyMemberState GetLeaderMemberState()
    {
        return _party_state?.GetMemberState(_party_state.leader_member_id);
    }

    private GDictionary CollectPersistentEquipmentInstanceIds()
    {
        GDictionary usedIds = new();
        if (_party_state == null)
            return usedIds;
        CollectWarehouseEquipmentInstanceIds(_party_state.warehouse_state, usedIds);
        foreach (var memberValue in _party_state.member_states.Values)
        {
            PartyMemberState memberState = memberValue.AsGodotObject() as PartyMemberState;
            EquipmentState equipmentState = memberState?.equipment_state;
            if (equipmentState == null)
                continue;
            foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            {
                StringName instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.GetEquippedInstanceId(entrySlotId)
                );
                if (instanceId == "")
                    continue;
                usedIds[instanceId.ToString()] = true;
            }
        }
        return usedIds;
    }

    private void CollectWarehouseEquipmentInstanceIds(
        WarehouseState warehouse_state,
        GDictionary used_ids
    )
    {
        if (warehouse_state == null || used_ids == null)
            return;
        foreach (EquipmentInstanceState instance in warehouse_state.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance == null)
                continue;
            StringName instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
            if (instanceId == "")
                continue;
            used_ids[instanceId.ToString()] = true;
        }
    }

    public ProgressionContentRegistry GetProgressionContentRegistry() =>
        _progression_content_registry;

    public GameRoot GetGameRootTyped() => EnsureGameRoot();

    public GameContentCatalog GetContentCatalogTyped() =>
        EnsureGameRoot().GetContentCatalogTyped();

    public ProgressionIdentityCatalogData GetProgressionIdentityCatalogTyped()
    {
        return _progression_content_registry?.GetIdentityCatalogTyped()
            ?? new ProgressionIdentityCatalogData();
    }

    public IReadOnlyDictionary<StringName, SkillDef> GetSkillDefsTyped()
    {
        return BuildSkillDefIndex(_skill_defs);
    }

    public GDictionary GetBattleSpecialProfileRegistrySnapshot() =>
        _battle_special_profile_registry != null
            ? _battle_special_profile_registry.GetSnapshot()
            : new GDictionary();

    public IReadOnlyDictionary<StringName, ProfessionDef> GetProfessionDefsTyped()
    {
        return BuildProfessionDefIndex(_profession_defs);
    }

    public IReadOnlyDictionary<StringName, AchievementDef> GetAchievementDefsTyped()
    {
        return BuildAchievementDefIndex(_achievement_defs);
    }

    public QuestDef GetQuestDef(StringName quest_id)
    {
        if (quest_id == "" || _quest_defs == null)
            return null;
        Dictionary<StringName, QuestDef> questDefs = BuildQuestDefIndex(_quest_defs);
        return questDefs.TryGetValue(quest_id, out QuestDef questDef) ? questDef : null;
    }

    public IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped()
    {
        return BuildQuestDefIndex(_quest_defs);
    }

    public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped()
    {
        return BuildItemDefIndex(_item_defs);
    }

    public IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped()
    {
        return BuildRecipeDefIndex(_recipe_defs);
    }

    public IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplatesTyped()
    {
        return BuildEnemyTemplateIndex(_enemy_templates);
    }

    public IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainsTyped()
    {
        return BuildEnemyAiBrainIndex(_enemy_ai_brains);
    }

    public IReadOnlyDictionary<StringName, WildEncounterRosterDef> GetWildEncounterRostersTyped()
    {
        return BuildWildEncounterRosterIndex(_wild_encounter_rosters);
    }

    public int InstallTestContentDef(
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
        if (domain_id == "quest")
            _questDefIndex = BuildQuestDefIndex(_quest_defs);
        if (refreshBattleSpecialProfiles)
            RefreshBattleSpecialProfiles();
        RefreshContentCatalog();
        return (int)Error.Ok;
    }

    public int InstallTestContentDefStringKey(
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
        if (domain_id == "quest")
            _questDefIndex = BuildQuestDefIndex(_quest_defs);
        if (refreshBattleSpecialProfiles)
            RefreshBattleSpecialProfiles();
        RefreshContentCatalog();
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

    public int SaveWorldState() => SaveGameState();

    public int SaveGameState()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            _battle_save_dirty = true;
            MarkRuntimeStateDirty(SaveDirtyScopeBattleLockedSave);
            return (int)Error.Ok;
        }
        return CommitRuntimeState("save_game_state");
    }

    public int CommitRuntimeState()
    {
        return CommitRuntimeState("runtime");
    }

    public int CommitRuntimeState(StringName reason)
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            RecordSaveError((int)Error.Busy, reason);
            return (int)Error.Busy;
        }

        int persistError = PersistGameState();
        if (persistError != (int)Error.Ok)
        {
            RecordSaveError(persistError, reason);
            return persistError;
        }

        ClearRuntimeSaveDirty();
        ClearLastSaveError();
        return (int)Error.Ok;
    }

    public int FlushGameState()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (_battle_save_lock_enabled)
        {
            RecordSaveError((int)Error.Busy, "flush_game_state");
            return (int)Error.Busy;
        }
        if (!HasPendingSave())
            return (int)Error.Ok;
        return CommitRuntimeState("flush_game_state");
    }

    public int ClearPersistedWorld() => ClearPersistedGame();

    public int ClearPersistedGame()
    {
        ResetRuntimeState();
        InvalidateSaveIndexCache();
        int removeError = RemoveDirectoryRecursive(SaveDirectory);
        if (removeError != (int)Error.Ok)
            return removeError;
        LogSessionInfo("session.save.clear.ok", "已清理存档目录。");
        return (int)Error.Ok;
    }

    public void ResetRuntimeCache() => ResetRuntimeState();

    public void UnloadActiveWorld()
    {
        if (!_has_active_world)
            return;
        if (HasPendingSave())
        {
            if (_battle_save_lock_enabled)
            {
                RecordSaveError((int)Error.Busy, "unload_active_world");
                throw new InvalidOperationException(
                    "GameSession cannot unload active world while battle save lock is enabled."
                );
            }
            int unloadSaveError = CommitRuntimeState("unload_active_world");
            if (unloadSaveError != (int)Error.Ok)
            {
                throw new InvalidOperationException(
                    "GameSession failed to commit pending save before unloading active world."
                );
            }
        }
        string unloadedSaveId = _active_save_id;
        ResetRuntimeState();
        RotateLogSession();
        LogSessionInfo(
            "session.runtime.unload.ok",
            "已卸载当前运行中世界。",
            Json.Stringify(new GDictionary { ["save_id"] = unloadedSaveId })
        );
    }

    private bool TryLoadGameState(string generation_config_path)
    {
        if (string.IsNullOrEmpty(generation_config_path))
            return false;

        bool attemptedCandidate = false;
        foreach (GDictionary saveMeta in LoadSaveIndexEntries())
        {
            if (GetString(saveMeta, "generation_config_path") != generation_config_path)
                continue;
            attemptedCandidate = true;
            string candidateSaveId = GetString(saveMeta, "save_id");
            if (LoadSave(candidateSaveId) == (int)Error.Ok)
                return true;
            LogSessionInfo(
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

    private int PrepareNewWorld(
        string generation_config_path,
        WorldMapGenerationConfig generation_config
    )
    {
        if (generation_config == null)
            return (int)Error.InvalidParameter;

        var gridSystem = new WorldMapGridSystem();
        gridSystem.Setup(generation_config.world_size_in_chunks, generation_config.chunk_size);

        var spawnSystem = new WorldMapSpawnSystem();
        WorldMapSpawnSystem.WorldBuildData worldBuild = spawnSystem.BuildWorldTyped(
            generation_config,
            gridSystem
        );
        GDictionary worldData = worldBuild.ToDictionary();

        _generation_config_path = generation_config_path;
        _generation_config = generation_config;
        _world_data = NormalizeWorldData(worldData);
        _player_coord = worldBuild.PlayerStartCoord;
        _player_faction_id = "player";
        _party_state = CreateDefaultPartyState();
        RefreshPartyBodySizesFromIdentity(_party_state);
        BackfillRacialGrantedSkills(_party_state);
        _has_active_world = true;
        _battle_save_lock_enabled = false;
        ClearRuntimeSaveDirty();
        ClearLastSaveError();
        return (int)Error.Ok;
    }

    private int PersistGameState()
    {
        if (!_has_active_world)
            return (int)Error.Unconfigured;
        if (string.IsNullOrEmpty(_active_save_id) || string.IsNullOrEmpty(_active_save_path))
        {
            throw new InvalidOperationException(
                "GameSession has world state but no active save slot."
            );
        }

        int ensureDirError = EnsureSaveDirectory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        int now = (int)Time.GetUnixTimeFromSystem();
        string displayName = GetString(_active_save_meta, "display_name", _active_save_id);
        _active_save_meta = BuildSaveMeta(
            _active_save_id,
            displayName,
            _generation_config_path,
            new StringName(GetString(_active_save_meta, "world_preset_id")),
            GetString(_active_save_meta, "world_preset_name"),
            _generation_config != null ? _generation_config.GetWorldSizeCells() : Vector2I.Zero,
            GetInt(_active_save_meta, "created_at_unix_time", now),
            now
        );

        int payloadWriteError = WriteSavePayloadAtomically(
            _active_save_path,
            BuildSavePayload(now)
        );
        if (payloadWriteError != (int)Error.Ok)
            return payloadWriteError;

        int indexError = WriteSaveIndex(
            UpsertSaveMeta(LoadSaveIndexEntries(), _active_save_meta)
        );
        if (indexError != (int)Error.Ok)
            return indexError;

        _battle_save_dirty = false;
        return (int)Error.Ok;
    }

    private int LoadCurrentPayload(
        GDictionary payload,
        string generation_config_path,
        WorldMapGenerationConfig generation_config,
        GDictionary save_meta
    )
    {
        GDictionary decodeResult = _save_serializer.DecodePayload(
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
        int identityError = ValidateDecodedPartyIdentityForSave(
            decodedPartyState,
            GetString(decodeResult, "active_save_id"),
            "load_save"
        );
        if (identityError != (int)Error.Ok)
            return identityError;

        ResetRuntimeState();
        _active_save_id = GetString(decodeResult, "active_save_id");
        _active_save_path = BuildSaveFilePath(_active_save_id);
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

        bool bodySizeChanged = RefreshPartyBodySizesFromIdentity(_party_state);
        bool racialGrantsChanged = false;
        racialGrantsChanged = RevokeOrphanRacialSkills(_party_state) || racialGrantsChanged;
        racialGrantsChanged = BackfillRacialGrantedSkills(_party_state) || racialGrantsChanged;
        if (bodySizeChanged)
            QueuePostDecodeSave("identity_body_size");
        if (racialGrantsChanged)
            QueuePostDecodeSave("racial_granted_skills");
        return (int)Error.Ok;
    }

    private int FlushPostDecodeSave()
    {
        return !_post_decode_save_pending
            ? (int)Error.Ok
            : CommitRuntimeState("post_decode_repair");
    }

    private bool RefreshPartyBodySizesFromIdentity(PartyState party_state)
    {
        if (party_state == null)
            return false;
        bool changed = false;
        foreach (var memberValue in party_state.member_states.Values)
        {
            changed =
                RefreshMemberBodySizeFromIdentity(
                    memberValue.AsGodotObject() as PartyMemberState
                ) || changed;
        }
        return changed;
    }

    private bool BackfillRacialGrantedSkills(PartyState party_state)
    {
        return RacialSkillGrantService.BackfillParty(
            party_state,
            GetProgressionIdentityCatalogTyped(),
            GetSkillDefsTyped(),
            GetProfessionDefsTyped()
        );
    }

    private bool RevokeOrphanRacialSkills(PartyState party_state)
    {
        return RacialSkillGrantService.RevokeOrphanParty(
            party_state,
            GetProgressionIdentityCatalogTyped(),
            GetSkillDefsTyped(),
            GetProfessionDefsTyped()
        );
    }

    private GDictionary BuildSavePayload(int saved_at_unix_time)
    {
        return _save_serializer.BuildSavePayload(
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

    private GDictionary BuildWorldStatePayload()
    {
        return _save_serializer.BuildWorldStatePayload(
            _world_data,
            _player_coord,
            _player_faction_id
        );
    }

    private GDictionary BuildMetaPayload(int saved_at_unix_time)
    {
        return _save_serializer.BuildMetaPayload(saved_at_unix_time);
    }

    public GDictionary BuildSaveMeta(
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
        return _save_serializer.BuildSaveMeta(
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

    private string GenerateUniqueSaveId(int timestamp, string prefix = "save")
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        GDictionary existingSaveIds = new();
        foreach (GDictionary entry in LoadSaveIndexEntries())
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
                && !FileAccess.FileExists(BuildSaveFilePath(saveId))
            )
                return saveId;
        }
        return "";
    }

    private WorldMapGenerationConfig LoadGenerationConfig(string generation_config_path)
    {
        WorldMapGenerationConfig generationConfig = null;
        try
        {
            generationConfig = ResourceLoader.Load<WorldMapGenerationConfig>(
                generation_config_path
            );
        }
        catch (Exception ex)
        {
            PushSessionError(
                "session.config.load_failed",
                $"GameSession failed to load config from {generation_config_path}",
                Json.Stringify(
                    new GDictionary
                    {
                        ["generation_config_path"] = generation_config_path,
                        ["exception_type"] = ex.GetType().Name,
                    }
                )
            );
            return null;
        }
        if (generationConfig == null)
        {
            PushSessionError(
                "session.config.load_failed",
                $"GameSession failed to load config from {generation_config_path}",
                Json.Stringify(new GDictionary { ["generation_config_path"] = generation_config_path })
            );
            return null;
        }
        return generationConfig;
    }

    public GDictionary ReadSavePayload(string save_path, bool emit_errors = true)
    {
        int recoveryError = FileIOCoordinator.RecoverReplaceTarget(
            save_path,
            SaveFileCompressionMode,
            "session.save.read",
            "save",
            PushSessionError
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

    public int EnsureSaveDirectory()
    {
        return (int)
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory));
    }

    public string BuildSaveFilePath(string save_id)
    {
        if (!_save_serializer.IsValidSaveIdToken(save_id))
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
        return FileIOCoordinator.WriteCompressedVariantAtomically(
            virtual_path,
            payload,
            SaveFileCompressionMode,
            error_event_prefix,
            label,
            PushSessionError
        );
    }

    private int WriteSavePayloadAtomically(string save_path, GDictionary payload)
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

    public int ReplaceFileAtomically(
        string source_path,
        string target_path,
        string error_event_prefix,
        string label
    )
    {
        return FileIOCoordinator.ReplaceFileAtomically(
            source_path,
            target_path,
            error_event_prefix,
            label,
            PushSessionError
        );
    }

    public int RenameFile(string from_virtual_path, string to_virtual_path)
    {
        return FileIOCoordinator.RenameFile(from_virtual_path, to_virtual_path);
    }

    public int RemoveFileIfExists(string virtual_path)
    {
        return FileIOCoordinator.RemoveFileIfExists(virtual_path);
    }

    public GDictionaryArray LoadSaveIndexEntries()
    {
        if (IsSaveIndexCacheCurrent())
            return DuplicateSaveIndexEntries(_save_index_entries_cache);

        bool shouldRewriteIndex = false;
        GArray rawEntries = new();
        int indexRecoveryError = FileIOCoordinator.RecoverReplaceTarget(
            SaveIndexPath,
            SaveFileCompressionMode,
            "session.save.index",
            "save index",
            PushSessionError
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

        GDictionaryArray entries = NormalizeSaveIndexEntries(rawEntries);
        GDictionaryArray rebuiltEntries = RebuildSaveIndexEntriesFromSaveFiles();
        GDictionaryArray mergedEntries = MergeSaveIndexEntries(entries, rebuiltEntries);
        if (shouldRewriteIndex || !SaveIndexEntriesMatch(entries, mergedEntries))
            WriteSaveIndex(mergedEntries);
        else
            SetSaveIndexCache(mergedEntries);
        return DuplicateSaveIndexEntries(mergedEntries);
    }

    public GDictionaryArray PeekSaveIndexEntriesReadOnly()
    {
        if (IsSaveIndexCacheCurrent())
            return DuplicateSaveIndexEntries(_save_index_entries_cache);
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

        GDictionaryArray entries = NormalizeSaveIndexEntries(savesValue.AsGodotArray());
        SetSaveIndexCache(entries);
        return DuplicateSaveIndexEntries(entries);
    }

    public int WriteSaveIndex(GDictionaryArray entries)
    {
        int ensureDirError = EnsureSaveDirectory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        GDictionaryArray normalizedEntries = NormalizeSaveIndexEntries(ToUntypedArray(entries));
        int writeError = _write_compressed_variant_atomically(
            SaveIndexPath,
            BuildSaveIndexPayload(normalizedEntries),
            "session.save.index",
            "save index"
        );
        SetSaveIndexCache(normalizedEntries);
        if (writeError != (int)Error.Ok)
            return (int)Error.Ok;
        return (int)Error.Ok;
    }

    private bool TryReadSaveIndexPayload(FileAccess index_file, out GDictionary payload)
    {
        GDictionary rawPayload = _save_serializer.ReadSaveIndexPayload(index_file);
        if (rawPayload != null)
        {
            payload = rawPayload;
            return true;
        }
        payload = new GDictionary();
        return false;
    }

    private bool IsSaveIndexCacheCurrent()
    {
        if (!_save_index_cache_valid)
            return false;
        GDictionary currentSignature = GetSaveIndexFileSignature();
        return ReadExactBool(_save_index_cache_signature, "exists") == ReadExactBool(currentSignature, "exists")
            && GetInt(_save_index_cache_signature, "modified_time", -1)
                == GetInt(currentSignature, "modified_time", -1)
            && GetInt(_save_index_cache_signature, "size", -1)
                == GetInt(currentSignature, "size", -1);
    }

    private void SetSaveIndexCache(GDictionaryArray entries)
    {
        _save_index_entries_cache = DuplicateSaveIndexEntries(entries);
        _save_index_cache_valid = true;
        _save_index_cache_signature = GetSaveIndexFileSignature();
    }

    private void InvalidateSaveIndexCache()
    {
        _save_index_entries_cache.Clear();
        _save_index_cache_valid = false;
        _save_index_cache_signature = new GDictionary();
    }

    private GDictionary GetSaveIndexFileSignature()
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

    private GDictionaryArray DuplicateSaveIndexEntries(GDictionaryArray entries)
    {
        GDictionaryArray duplicatedEntries = new();
        if (entries == null)
            return duplicatedEntries;
        foreach (GDictionary entry in entries)
            duplicatedEntries.Add(entry?.Duplicate(true) ?? new GDictionary());
        return duplicatedEntries;
    }

    private bool SaveIndexEntriesMatch(
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
            if (!SaveIndexEntryMatches(left_entries[index], right_entries[index]))
                return false;
        }
        return true;
    }

    private bool SaveIndexEntryMatches(GDictionary left_entry, GDictionary right_entry)
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

    private GDictionaryArray NormalizeSaveIndexEntries(GArray raw_entries)
    {
        GDictionaryArray entries = new();
        if (raw_entries == null)
            return entries;
        foreach (GDictionary rawEntry in ReadDictionaryItems(raw_entries))
        {
            GDictionary entry = NormalizeSaveMeta(
                DeserializeSaveIndexEntry(rawEntry)
            );
            if (entry.Count == 0)
                continue;
            if (!FileAccess.FileExists(BuildSaveFilePath(GetString(entry, "save_id"))))
                continue;
            entries.Add(entry);
        }
        SortSaveMetaNewestFirst(entries);
        return entries;
    }

    public GDictionaryArray SerializeSaveIndexEntries(GDictionaryArray entries)
    {
        return _save_serializer.SerializeSaveIndexEntries(entries);
    }

    public GDictionary BuildSaveIndexPayload(GDictionaryArray entries)
    {
        return _save_serializer.BuildSaveIndexPayload(entries);
    }

    public GDictionary DeserializeSaveIndexEntry(GDictionary raw_entry)
    {
        return _save_serializer.DeserializeSaveIndexEntry(raw_entry);
    }

    private bool _is_save_index_integer_value(Variant value)
    {
        return value.VariantType == Variant.Type.Int
            && _save_serializer.IsSaveIndexIntegerValue(value.AsInt32());
    }

    private GDictionaryArray RebuildSaveIndexEntriesFromSaveFiles()
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
            if (!_save_serializer.IsValidSaveIdToken(candidateSaveId))
                continue;
            string savePath = $"{SaveDirectory}/{fileName}";
            GDictionary readResult = ReadSavePayload(savePath, false);
            if (GetInt(readResult, "error", (int)Error.InvalidData) != (int)Error.Ok)
                continue;
            if (!TryRead(readResult, "payload", out var payloadValue)
                || payloadValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary payload = payloadValue.AsGodotDictionary();
            GDictionary saveMeta = ExtractSaveMetaFromPayload(payload);
            if (saveMeta.Count == 0)
                continue;
            string generationConfigPath = GetString(saveMeta, "generation_config_path");
            WorldMapGenerationConfig generationConfig = LoadGenerationConfig(
                generationConfigPath
            );
            if (generationConfig == null)
                continue;
            GDictionary decodeResult = _save_serializer.DecodePayload(
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
                    ValidateDecodedPartyIdentityForSave(
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

    public GDictionaryArray MergeSaveIndexEntries(
        GDictionaryArray primary_entries,
        GDictionaryArray fallback_entries
    )
    {
        return _save_serializer.MergeSaveIndexEntries(primary_entries, fallback_entries);
    }

    public GDictionary ExtractSaveMetaFromPayload(GDictionary payload)
    {
        return _save_serializer.ExtractSaveMetaFromPayload(payload);
    }

    private int ValidateDecodedPartyIdentityForSave(
        PartyState party_state,
        string save_id,
        StringName context
    )
    {
        IReadOnlyList<string> identityErrors = IdentityPayloadValidator.ValidatePartyIdentityForContentSource(
            party_state,
            GetProgressionIdentityCatalogTyped()
        );
        if (identityErrors.Count == 0)
            return (int)Error.Ok;
        GArray errorPayload = new();
        foreach (string identityError in identityErrors)
            errorPayload.Add(identityError);
        PushSessionError(
            "session.save.identity_invalid",
            $"Save slot {save_id} has invalid party identity payload.",
            Json.Stringify(
                new GDictionary
                {
                    ["save_id"] = save_id,
                    ["context"] = context,
                    ["errors"] = errorPayload,
                }
            )
        );
        return (int)Error.InvalidData;
    }

    public GDictionaryArray UpsertSaveMeta(GDictionaryArray entries, GDictionary save_meta)
    {
        return _save_serializer.UpsertSaveMeta(entries, save_meta);
    }

    private GDictionary GetSaveMetaById(string save_id)
    {
        foreach (GDictionary entry in LoadSaveIndexEntries())
        {
            if (GetString(entry, "save_id") == save_id)
                return entry;
        }
        return new GDictionary();
    }

    private GDictionary FindMostRecentSaveByConfig(string generation_config_path)
    {
        foreach (GDictionary entry in LoadSaveIndexEntries())
        {
            if (GetString(entry, "generation_config_path") == generation_config_path)
                return entry;
        }
        return new GDictionary();
    }

    public GDictionary NormalizeSaveMeta(GDictionary raw_meta)
    {
        return _save_serializer.NormalizeSaveMeta(raw_meta ?? new GDictionary());
    }

    private bool SortSaveMetaNewestFirst(GDictionary a, GDictionary b)
    {
        return _save_serializer.SortSaveMetaNewestFirst(a, b);
    }

    public int RemoveDirectoryRecursive(string virtual_path)
    {
        return FileIOCoordinator.RemoveDirectoryRecursive(
            virtual_path,
            PushSessionError
        );
    }

    private int ApplyCharacterCreationPayloadToMainCharacter(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return (int)Error.Ok;
        if (_party_state == null)
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without party state."
            );
        }

        StringName mainMemberId = _party_state.GetResolvedMainCharacterMemberId();
        if (mainMemberId == "")
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without a resolvable main character."
            );
        }

        PartyMemberState memberState = _party_state.GetMemberState(mainMemberId);
        UnitProgress progression = memberState?.progression as UnitProgress;
        if (memberState == null || progression == null || progression.unit_base_attributes == null)
        {
            throw new InvalidOperationException(
                $"GameSession cannot apply character creation payload because main character {mainMemberId} is incomplete."
            );
        }

        if (
            !CharacterCreationService.ApplyCharacterCreationPayloadToMemberForContentSource(
                memberState,
                payload,
                _progression_content_registry,
                new CharacterCreationOptions(bakeRerollLuck: true)
            )
        )
        {
            PushSessionError(
                "session.character_creation.invalid_payload",
                $"GameSession rejected invalid character creation payload for main character {mainMemberId}.",
                Json.Stringify(new GDictionary { ["member_id"] = mainMemberId })
            );
            return (int)Error.InvalidData;
        }

        RevokeOrphanRacialSkills(_party_state);
        BackfillRacialGrantedSkills(_party_state);
        return (int)Error.Ok;
    }

    private void ApplyCharacterCreationIdentityPayload(
        PartyMemberState member_state,
        GDictionary payload
    )
    {
        if (member_state == null || payload == null)
            return;
        member_state.race_id = ReadPayloadStringName(
            payload,
            "race_id",
            member_state.race_id,
            false
        );
        member_state.subrace_id = ReadPayloadStringName(
            payload,
            "subrace_id",
            member_state.subrace_id,
            false
        );
        member_state.age_years = ReadPayloadNonnegativeInt(
            payload,
            "age_years",
            member_state.age_years
        );
        member_state.birth_at_world_step = ReadPayloadNonnegativeInt(
            payload,
            "birth_at_world_step",
            member_state.birth_at_world_step
        );
        member_state.age_profile_id = ReadPayloadStringName(
            payload,
            "age_profile_id",
            member_state.age_profile_id,
            false
        );
        member_state.natural_age_stage_id = ReadPayloadStringName(
            payload,
            "natural_age_stage_id",
            member_state.natural_age_stage_id,
            false
        );
        member_state.effective_age_stage_id = ReadPayloadStringName(
            payload,
            "effective_age_stage_id",
            member_state.effective_age_stage_id,
            false
        );
        member_state.effective_age_stage_source_type = ReadPayloadStringName(
            payload,
            "effective_age_stage_source_type",
            member_state.effective_age_stage_source_type,
            true
        );
        member_state.effective_age_stage_source_id = ReadPayloadStringName(
            payload,
            "effective_age_stage_source_id",
            member_state.effective_age_stage_source_id,
            true
        );
        member_state.body_size = Mathf.Max(
            ReadPayloadNonnegativeInt(payload, "body_size", member_state.body_size),
            1
        );
        member_state.body_size_category = ReadPayloadStringName(
            payload,
            "body_size_category",
            member_state.body_size_category,
            false
        );
        member_state.versatility_pick = ReadPayloadStringName(
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
        member_state.bloodline_id = ReadPayloadStringName(
            payload,
            "bloodline_id",
            member_state.bloodline_id,
            true
        );
        member_state.bloodline_stage_id = ReadPayloadStringName(
            payload,
            "bloodline_stage_id",
            member_state.bloodline_stage_id,
            true
        );
        member_state.ascension_id = ReadPayloadStringName(
            payload,
            "ascension_id",
            member_state.ascension_id,
            true
        );
        member_state.ascension_stage_id = ReadPayloadStringName(
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
        member_state.original_race_id_before_ascension = ReadPayloadStringName(
            payload,
            "original_race_id_before_ascension",
            member_state.original_race_id_before_ascension,
            true
        );
        member_state.biological_age_years = ReadPayloadNonnegativeInt(
            payload,
            "biological_age_years",
            member_state.biological_age_years
        );
        member_state.astral_memory_years = ReadPayloadNonnegativeInt(
            payload,
            "astral_memory_years",
            member_state.astral_memory_years
        );
        RefreshMemberBodySizeFromIdentity(member_state);
    }

    private void ApplyInitialHpFormula(PartyMemberState member_state)
    {
        if (member_state?.progression is not UnitProgress progression)
            return;
        UnitBaseAttributes attributes = progression.unit_base_attributes;
        if (attributes == null)
            return;
        int constitution = attributes.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int initialHpMax = CharacterCreationService.CalculateInitialHpMax(constitution);
        attributes.SetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax), initialHpMax);
        member_state.current_hp = initialHpMax;
    }

    private bool RefreshMemberBodySizeFromIdentity(PartyMemberState member_state)
    {
        StringName category = ResolveBodySizeCategoryForMember(member_state);
        if (category == "")
            return false;
        int resolvedBodySize = BodySizeContentRules.GetBodySizeForCategory(category);
        if (
            member_state.body_size_category == category
            && member_state.body_size == resolvedBodySize
        )
            return false;
        member_state.body_size_category = category;
        member_state.body_size = resolvedBodySize;
        return true;
    }

    private StringName ResolveBodySizeCategoryForMember(PartyMemberState member_state)
    {
        if (member_state == null || _progression_content_registry == null)
            return "";
        IReadOnlyDictionary<StringName, AscensionStageDef> ascensionStageDefs =
            _progression_content_registry.GetAscensionStageDefsTyped();
        ascensionStageDefs.TryGetValue(
            member_state.ascension_stage_id,
            out AscensionStageDef ascensionStageDef
        );
        if (
            ascensionStageDef != null
            && ascensionStageDef.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(
                ascensionStageDef.body_size_category_override
            )
        )
        {
            return ascensionStageDef.body_size_category_override;
        }
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs =
            _progression_content_registry.GetSubraceDefsTyped();
        subraceDefs.TryGetValue(member_state.subrace_id, out SubraceDef subraceDef);
        if (
            subraceDef != null
            && subraceDef.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(subraceDef.body_size_category_override)
        )
        {
            return subraceDef.body_size_category_override;
        }
        IReadOnlyDictionary<StringName, RaceDef> raceDefs =
            _progression_content_registry.GetRaceDefsTyped();
        raceDefs.TryGetValue(member_state.race_id, out RaceDef raceDef);
        if (
            raceDef != null
            && BodySizeContentRules.IsValidBodySizeCategory(raceDef.body_size_category)
        )
            return raceDef.body_size_category;
        return "";
    }

    private StringName ReadPayloadStringName(
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

    private int ReadPayloadNonnegativeInt(GDictionary payload, string field_name, int fallback)
    {
        if (
            payload == null
            || !payload.ContainsKey(field_name)
            || !HasInt(payload, field_name)
        )
            return fallback;
        return Mathf.Max(payload[field_name].AsInt32(), 0);
    }

    private PartyState CreateDefaultPartyState()
    {
        var partyState = new PartyState();
        partyState.gold = 180;

        PartyMemberState swordMember = BuildDefaultMemberState(
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

        partyState.SetMemberState(swordMember);
        partyState.leader_member_id = "player_sword_01";
        partyState.main_character_member_id = "player_sword_01";
        partyState.active_member_ids = ProgressionDataUtils.to_string_name_array(
            new GArray { "player_sword_01" }
        );
        partyState.reserve_member_ids = ProgressionDataUtils.to_string_name_array(new GArray());
        return partyState;
    }

    private PartyMemberState BuildDefaultMemberState(
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
        int initialHpMax = CharacterCreationService.CalculateInitialHpMax(constitution);
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
            granted_source_type = UnitSkillProgress.ToStringName(
                UnitSkillGrantSourceType.Profession
            ),
            granted_source_id = "warrior",
        };
        progression.SetSkillProgress(starterSkill);

        var warriorProgress = new UnitProfessionProgress
        {
            profession_id = "warrior",
            rank = 0,
            is_active = false,
        };
        warriorProgress.AddCoreSkill(starting_skill_id);
        progression.SetProfessionProgress(warriorProgress);
        SkillDef randomStartingSkillDef = GrantRandomStartingBookSkill(progression);
        RefreshProgressionRuntimeState(progression);

        memberState.progression = progression;
        EquipStartingWeaponForSkill(memberState, randomStartingSkillDef);
        return memberState;
    }

    private SkillDef GrantRandomStartingBookSkill(UnitProgress progression)
    {
        if (progression == null || _skill_defs.Count == 0)
            return null;

        GStringNameArray eligibleSkillIds = new();
        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skill_defs))
        {
            StringName skillId = new(skillKey);
            SkillDef skillDef = GetObject<SkillDef>(_skill_defs, skillId);
            if (!IsRandomStartBookSkillCandidate(skillDef, progression))
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

        UnitSkillProgress skillProgress = progression.GetSkillProgress(selectedSkillId);
        if (skillProgress == null)
        {
            skillProgress = new UnitSkillProgress();
            skillProgress.skill_id = selectedSkillId;
        }

        skillProgress.is_learned = true;
        skillProgress.granted_source_type = UnitSkillProgress.ToStringName(
            UnitSkillGrantSourceType.Player
        );
        skillProgress.granted_source_id = "";
        skillProgress.skill_level = ResolveRandomStartSkillInitialLevel(selectedSkillDef);
        skillProgress.current_mastery = 0;
        skillProgress.total_mastery_earned = 0;
        progression.SetSkillProgress(skillProgress);
        return selectedSkillDef;
    }

    private void EquipStartingWeaponForSkill(PartyMemberState member_state, SkillDef skill_def)
    {
        if (member_state?.equipment_state == null)
            return;
        StringName itemId = ResolveStartingWeaponItemIdForSkill(skill_def);
        if (itemId == "")
            return;
        ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
        if (itemDef == null || !itemDef.IsWeapon())
            return;
        StringName instanceId = AllocateEquipmentInstanceId();
        if (instanceId == "")
            return;
        EquipmentInstanceState equipmentInstance = EquipmentInstanceState.CreateInstance(
            itemId,
            instanceId
        );
        IReadOnlyList<StringName> occupiedSlots = itemDef.GetFinalOccupiedSlotIdsTyped(
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand)
        );
        member_state.equipment_state.SetEquippedEntry(
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            itemId,
            occupiedSlots,
            equipmentInstance
        );
    }

    private StringName ResolveStartingWeaponItemIdForSkill(SkillDef skill_def)
    {
        GStringNameArray candidates = new();
        if (
            SkillMatchesStartingWeaponType(
                skill_def,
                new GStringNameArray { "crossbow" },
                new GStringArray { "crossbow" }
            )
        )
            candidates.Add(StartingCrossbowWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skill_def,
                new GStringNameArray { "archer", "bow" },
                new GStringArray { "archer_" }
            )
        )
            candidates.Add(StartingArcherWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skill_def,
                new GStringNameArray { "mage", "magic", "spell" },
                new GStringArray { "mage_" }
            )
        )
            candidates.Add(StartingMageWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skill_def,
                new GStringNameArray { "priest", "faith", "heal" },
                new GStringArray { "priest_", "saint_" }
            )
        )
            candidates.Add(StartingPriestWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skill_def,
                new GStringNameArray { "warrior", "melee", "shield" },
                new GStringArray { "warrior_" }
            )
        )
            candidates.Add(StartingMeleeWeaponItemId);
        candidates.Add(StartingMeleeWeaponItemId);
        return FirstValidStartingWeaponItemId(candidates);
    }

    private bool SkillMatchesStartingWeaponType(
        SkillDef skill_def,
        GStringNameArray tag_ids,
        GStringArray skill_id_prefixes
    )
    {
        if (skill_def == null)
            return false;
        foreach (StringName tagId in tag_ids)
        {
            if (skill_def.HasTag(tagId))
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

    private StringName FirstValidStartingWeaponItemId(GStringNameArray candidates)
    {
        foreach (StringName itemId in candidates)
        {
            if (itemId == "")
                continue;
            ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
            if (itemDef != null && itemDef.IsWeapon())
                return itemId;
        }
        return "";
    }

    private void RefreshProgressionRuntimeState(UnitProgress progression)
    {
        if (progression == null)
            return;
        var progressionService = new ProgressionService();
        progressionService.Setup(
            progression,
            BuildSkillDefIndex(_skill_defs),
            BuildProfessionDefIndex(_profession_defs)
        );
        progressionService.RefreshRuntimeState();
    }

    private bool IsRandomStartBookSkillCandidate(SkillDef skill_def, UnitProgress progression)
    {
        if (skill_def == null || skill_def.skill_id == "")
            return false;
        if (skill_def.LearnSourceKind != SkillLearnSourceKind.Book)
            return false;
        if (skill_def.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return false;
        if (
            skill_def.LearnRequirementsTyped.Count > 0
            || skill_def.KnowledgeRequirementsTyped.Count > 0
            || skill_def.SkillLevelRequirementEntriesTyped.Count > 0
            || skill_def.AttributeRequirementEntriesTyped.Count > 0
            || skill_def.AchievementRequirementsTyped.Count > 0
        )
        {
            return false;
        }
        UnitSkillProgress learnedProgress = progression?.GetSkillProgress(skill_def.skill_id);
        return learnedProgress == null || !learnedProgress.is_learned;
    }

    public int ResolveRandomStartSkillInitialLevel(SkillDef skill_def)
    {
        return ResolveRandomStartSkillInitialLevel(skill_def, null);
    }

    private int ResolveRandomStartSkillInitialLevel(
        SkillDef skill_def,
        UnitProgress progression
    )
    {
        if (skill_def == null)
            return 0;
        int mappedLevel = GetInt(
            RandomStartSkillLevelByTier,
            ResolveRandomStartSkillTier(skill_def),
            0
        );
        int maxInitialLevel = skill_def.max_level >= 0 ? Mathf.Max(skill_def.max_level, 0) : 999;
        if (progression != null && skill_def.dynamic_max_level_stat_id != "")
        {
            int effectiveMax = SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
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

    private StringName ResolveRandomStartSkillTier(SkillDef skill_def)
    {
        if (skill_def == null)
            return RandomStartSkillTierBasic;

        string description = skill_def.description ?? "";
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsUltimate))
            return RandomStartSkillTierUltimate;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsAdvanced))
            return RandomStartSkillTierAdvanced;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsIntermediate))
            return RandomStartSkillTierIntermediate;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsBasic))
            return RandomStartSkillTierBasic;

        int tierScore = BuildRandomStartSkillTierScore(skill_def);
        if (tierScore >= 14)
            return RandomStartSkillTierUltimate;
        if (tierScore >= 9)
            return RandomStartSkillTierAdvanced;
        if (tierScore >= 6)
            return RandomStartSkillTierIntermediate;
        return RandomStartSkillTierBasic;
    }

    private bool DescriptionContainsAnyKeyword(string description, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if ((description ?? "").Contains(keyword))
                return true;
        }
        return false;
    }

    private int BuildRandomStartSkillTierScore(SkillDef skill_def)
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
        if (skill_def.HasTag("aoe"))
            score += 1;
        if (skill_def.HasTag("finisher"))
            score += 2;
        if (skill_def.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            score += 2;
        return score;
    }

    private PartyState NormalizePartyState(PartyState party_state)
    {
        return _save_serializer.NormalizePartyState(party_state) ?? new PartyState();
    }

    private GDictionary NormalizeWorldData(GDictionary world_data)
    {
        return _save_serializer.NormalizeWorldData(world_data ?? new GDictionary());
    }

    private GDictionary SerializeWorldData(GDictionary world_data)
    {
        return _save_serializer.SerializeWorldData(world_data ?? new GDictionary());
    }

    private void RotateLogSession()
    {
        _log_service?.StartNewSession();
    }

    public GDictionary CaptureRuntimeState()
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

    public void RestoreRuntimeState(GDictionary state)
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

    private void ResetRuntimeState()
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

    private void RefreshProgressionContent()
    {
        if (_progression_content_registry == null)
            return;
        _skill_defs = ProjectResourceDictionary(_progression_content_registry.GetSkillDefsTyped());
        _profession_defs = ProjectResourceDictionary(_progression_content_registry.GetProfessionDefsTyped());
        _achievement_defs = ProjectResourceDictionary(_progression_content_registry.GetAchievementDefsTyped());
        _quest_defs = ProjectResourceDictionary(_progression_content_registry.GetQuestDefsTyped());
        _questDefIndex = BuildQuestDefIndex(_quest_defs);
    }

    private void RefreshBattleSpecialProfiles()
    {
        if (_battle_special_profile_registry == null)
            return;
        _battle_special_profile_registry.Rebuild(BuildSkillDefIndex(_skill_defs));
    }

    private void RefreshItemContent()
    {
        if (_item_content_registry == null)
            return;
        Dictionary<StringName, SkillDef> skillDefs = BuildSkillDefIndex(_skill_defs);
        Dictionary<StringName, ItemDef> itemDefs = new(_item_content_registry.GetItemDefsTyped());
        foreach (
            var entry in SkillBookItemFactory.BuildGeneratedItemDefs(skillDefs, itemDefs)
        )
        {
            itemDefs[entry.Key] = entry.Value;
        }
        _item_defs = ProjectResourceDictionary(itemDefs);
    }

    private void RefreshRecipeContent()
    {
        if (_recipe_content_registry == null)
            return;
        _recipe_content_registry.Setup(GetItemDefsTyped());
        _recipe_defs = ProjectResourceDictionary(_recipe_content_registry.GetRecipeDefsTyped());
    }

    private void RefreshEnemyContent()
    {
        if (_enemy_content_registry == null)
            return;
        _enemy_templates = ProjectResourceDictionary(_enemy_content_registry.GetEnemyTemplatesTyped());
        _enemy_ai_brains = ProjectResourceDictionary(_enemy_content_registry.GetEnemyAiBrainsTyped());
        _wild_encounter_rosters = ProjectResourceDictionary(
            _enemy_content_registry.GetWildEncounterRostersTyped()
        );
    }

    // 把 session 当前的正式内容缓存推进到 GameContentCatalog 自己的 typed 快照里。
    // 在任何会改变 progression / item / recipe / enemy / battle special profile 内容的刷新后调用。
    private void RefreshContentCatalog()
    {
        EnsureGameRoot().GetContentCatalogTyped().Rebuild(this);
    }

    // 回归测试用的显式刷新入口：让测试在直接改动 session 内容缓存后手动重建 catalog 快照，
    // 以验证 catalog getter 不是 session getter 的 live 转发。不要为此暴露 public Godot API。
    internal void RefreshContentCatalogForTests()
    {
        RefreshContentCatalog();
    }

    private void RefreshContentValidationSnapshotState()
    {
        RefreshBattleSpecialProfiles();
        var snapshot = new ContentValidationSnapshotData();
        snapshot.Domains["progression"] = BuildContentValidationDomainSnapshot(
            _progression_content_registry
        );
        snapshot.Domains["battle_special_profile"] = BuildContentValidationDomainSnapshotFromErrors(
            _battle_special_profile_registry?.ValidateTyped()
        );
        snapshot.Domains["item"] = BuildItemContentValidationDomainSnapshot();
        snapshot.Domains["recipe"] = BuildContentValidationDomainSnapshotFromErrors(
            _recipe_content_registry?.ValidateTyped()
        );
        snapshot.Domains["enemy"] = BuildContentValidationDomainSnapshotFromErrors(
            _enemy_content_registry?.ValidateTyped()
        );
        snapshot.Domains["world"] = BuildWorldContentValidationDomainSnapshot();
        snapshot.Domains["quest"] = BuildQuestContentValidationDomainSnapshot();
        _contentValidationSnapshotData = snapshot;
        _content_validation_snapshot = snapshot.ToDictionary();
        // 验证刷新会重建 battle special profile registry，需要把新的 snapshot 推进 catalog，
        // 否则运行期再次走验证门时 catalog 的 battle special profile 视图会落后。
        RefreshContentCatalog();
    }

    private static ContentValidationDomainSnapshotData BuildContentValidationDomainSnapshot(
        IValidatableRegistry registry
    )
    {
        return BuildContentValidationDomainSnapshotFromErrors(registry?.ValidateTyped());
    }

    private ContentValidationDomainSnapshotData BuildWorldContentValidationDomainSnapshot()
    {
        if (_world_content_validator == null)
            return new ContentValidationDomainSnapshotData();
        return BuildContentValidationDomainSnapshotFromErrors(
            _world_content_validator.ValidateWorldPresets(_enemy_templates, _wild_encounter_rosters)
        );
    }

    private ContentValidationDomainSnapshotData BuildItemContentValidationDomainSnapshot()
    {
        var errors = new List<string>();
        AppendErrors(errors, _item_content_registry?.ValidateTyped());
        if (
            _item_defs != null
            && _skill_defs != null
            && _item_defs.Count > 0
            && _skill_defs.Count > 0
        )
        {
            Dictionary<StringName, ItemDef> itemDefs = BuildItemDefIndex(_item_defs);
            Dictionary<StringName, SkillDef> skillDefs = BuildSkillDefIndex(_skill_defs);
            AppendErrors(errors, SkillBookItemContentValidator.Validate(itemDefs, skillDefs));
        }
        return BuildContentValidationDomainSnapshotFromErrors(errors);
    }

    private ContentValidationDomainSnapshotData BuildQuestContentValidationDomainSnapshot()
    {
        var registrationErrors = new List<string>();
        if (_progression_content_registry != null)
        {
            AppendErrors(
                registrationErrors,
                _progression_content_registry.GetQuestRegistrationErrorsTyped()
            );
        }
        Dictionary<StringName, QuestDef> questDefs = BuildQuestDefIndex(_quest_defs);
        Dictionary<StringName, ItemDef> itemDefs = BuildItemDefIndex(_item_defs);
        Dictionary<StringName, SkillDef> skillDefs = BuildSkillDefIndex(_skill_defs);
        Dictionary<StringName, EnemyTemplateDef> enemyTemplates = BuildEnemyTemplateIndex(
            _enemy_templates
        );
        return BuildContentValidationDomainSnapshotFromErrors(
            QuestContentValidator.ValidateTyped(
                questDefs,
                itemDefs,
                skillDefs,
                enemyTemplates,
                registrationErrors
            )
        );
    }

    private static Dictionary<StringName, QuestDef> BuildQuestDefIndex(GDictionary questDefs)
    {
        var result = new Dictionary<StringName, QuestDef>();
        if (questDefs == null)
            return result;
        foreach (var key in questDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            QuestDef questDef = questDefs[key].AsGodotObject() as QuestDef;
            if (questDef == null || questDef.quest_id == "")
                continue;
            result[key.AsStringName()] = questDef;
        }
        return result;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDef>();
        if (itemDefs == null)
            return result;
        foreach (Variant key in itemDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ItemDef itemDef = itemDefs[key].AsGodotObject() as ItemDef;
            if (itemDef == null || itemDef.item_id == "")
                continue;
            result[key.AsStringName()] = itemDef;
        }
        return result;
    }

    private static Dictionary<StringName, RecipeDef> BuildRecipeDefIndex(GDictionary recipeDefs)
    {
        var result = new Dictionary<StringName, RecipeDef>();
        if (recipeDefs == null)
            return result;
        foreach (Variant key in recipeDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            RecipeDef recipeDef = recipeDefs[key].AsGodotObject() as RecipeDef;
            if (recipeDef == null || recipeDef.recipe_id == "")
                continue;
            result[key.AsStringName()] = recipeDef;
        }
        return result;
    }

    private static Dictionary<StringName, EnemyTemplateDef> BuildEnemyTemplateIndex(
        GDictionary enemyTemplates
    )
    {
        var result = new Dictionary<StringName, EnemyTemplateDef>();
        if (enemyTemplates == null)
            return result;
        foreach (Variant key in enemyTemplates.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            EnemyTemplateDef enemyTemplate = enemyTemplates[key].AsGodotObject() as EnemyTemplateDef;
            if (enemyTemplate == null || enemyTemplate.template_id == "")
                continue;
            result[key.AsStringName()] = enemyTemplate;
        }
        return result;
    }

    private static Dictionary<StringName, EnemyAiBrainDef> BuildEnemyAiBrainIndex(
        GDictionary enemyAiBrains
    )
    {
        var result = new Dictionary<StringName, EnemyAiBrainDef>();
        if (enemyAiBrains == null)
            return result;
        foreach (Variant key in enemyAiBrains.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            EnemyAiBrainDef enemyAiBrain = enemyAiBrains[key].AsGodotObject() as EnemyAiBrainDef;
            if (enemyAiBrain == null || enemyAiBrain.brain_id == "")
                continue;
            result[key.AsStringName()] = enemyAiBrain;
        }
        return result;
    }

    private static Dictionary<StringName, WildEncounterRosterDef> BuildWildEncounterRosterIndex(
        GDictionary wildEncounterRosters
    )
    {
        var result = new Dictionary<StringName, WildEncounterRosterDef>();
        if (wildEncounterRosters == null)
            return result;
        foreach (Variant key in wildEncounterRosters.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            WildEncounterRosterDef roster = wildEncounterRosters[key].AsGodotObject()
                as WildEncounterRosterDef;
            if (roster == null || roster.profile_id == "")
                continue;
            result[key.AsStringName()] = roster;
        }
        return result;
    }

    private static Dictionary<StringName, SkillDef> BuildSkillDefIndex(GDictionary skillDefs)
    {
        var result = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
            return result;
        foreach (Variant key in skillDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            SkillDef skillDef = skillDefs[key].AsGodotObject() as SkillDef;
            if (skillDef == null || skillDef.skill_id == "")
                continue;
            result[key.AsStringName()] = skillDef;
        }
        return result;
    }

    private static GDictionary ProjectResourceDictionary<T>(IReadOnlyDictionary<StringName, T> values)
        where T : GodotObject
    {
        var result = new GDictionary();
        if (values == null)
            return result;
        foreach ((StringName id, T value) in values)
        {
            if (id == default || id == (StringName)"" || value == null)
                continue;
            result[id] = value;
        }
        return result;
    }

    private static Dictionary<StringName, ProfessionDef> BuildProfessionDefIndex(
        GDictionary professionDefs
    )
    {
        var result = new Dictionary<StringName, ProfessionDef>();
        if (professionDefs == null)
            return result;
        foreach (Variant key in professionDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            ProfessionDef professionDef = professionDefs[key].AsGodotObject() as ProfessionDef;
            if (professionDef == null || professionDef.profession_id == "")
                continue;
            result[key.AsStringName()] = professionDef;
        }
        return result;
    }

    private static Dictionary<StringName, AchievementDef> BuildAchievementDefIndex(
        GDictionary achievementDefs
    )
    {
        var result = new Dictionary<StringName, AchievementDef>();
        if (achievementDefs == null)
            return result;
        foreach (Variant key in achievementDefs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
                continue;
            AchievementDef achievementDef = achievementDefs[key].AsGodotObject()
                as AchievementDef;
            if (achievementDef == null || achievementDef.achievement_id == "")
                continue;
            result[key.AsStringName()] = achievementDef;
        }
        return result;
    }

    private static ContentValidationDomainSnapshotData BuildContentValidationDomainSnapshotFromErrors(
        IEnumerable<string> errors
    )
    {
        var snapshot = new ContentValidationDomainSnapshotData();
        AppendErrors(snapshot.Errors, errors);
        return snapshot;
    }

    private int RequireContentValidationForRuntime(StringName operation_id)
    {
        RefreshContentValidationSnapshotState();
        if (IsContentValidationOk())
            return (int)Error.Ok;
        int errorCount = _contentValidationSnapshotData?.ErrorCount ?? 0;
        PushSessionError(
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

    private void ReportContentValidationErrors()
    {
        foreach (string domainId in ContentValidationDomainOrder)
        {
            foreach (
                string validationError in _contentValidationSnapshotData.EnumerateDomainErrors(domainId)
            )
                ReportContentValidationError(domainId, validationError);
        }
    }

    private void ReportContentValidationError(string domain_id, string validation_error)
    {
        switch (domain_id)
        {
            case "progression":
                PushSessionError(
                    "session.content.progression_validation_failed",
                    $"Progression content error: {validation_error}"
                );
                break;
            case "battle_special_profile":
                PushSessionError(
                    "session.content.battle_special_profile_validation_failed",
                    $"Battle special profile content error: {validation_error}"
                );
                break;
            case "item":
                PushSessionError(
                    "session.content.item_validation_failed",
                    $"Item content error: {validation_error}"
                );
                break;
            case "recipe":
                PushSessionError(
                    "session.content.recipe_validation_failed",
                    $"Recipe content error: {validation_error}"
                );
                break;
            case "enemy":
                PushSessionError(
                    "session.content.enemy_validation_failed",
                    $"Enemy content error: {validation_error}"
                );
                break;
            case "world":
                PushSessionError(
                    "session.content.world_validation_failed",
                    $"World content error: {validation_error}"
                );
                break;
            case "quest":
                PushSessionError(
                    "session.content.quest_validation_failed",
                    $"Quest content error: {validation_error}"
                );
                break;
        }
    }

    private void LogSessionInfo(string event_id, string message)
    {
        LogEvent("info", "session", event_id, message, "");
    }

    private void LogSessionInfo(string event_id, string message, string context)
    {
        GameLog.Info(message, event_id, "session", context);
    }

    private void PushSessionError(string event_id, string message)
    {
        PushSessionError(event_id, message, "");
    }

    private void PushSessionError(string event_id, string message, string context)
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
        value = default;
        return false;
    }

    private static bool HasArray(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Array;

    private static bool HasInt(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Int;

    private static bool HasString(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value)
        && value.VariantType == Variant.Type.String;

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

    private static GStringArray ToGodotStringArray(IEnumerable<string> values)
    {
        GStringArray result = new();
        AppendErrors(result, values);
        return result;
    }

    private static void AppendErrors(ICollection<string> target, IEnumerable<string> source)
    {
        if (target == null || source == null)
            return;
        foreach (string value in source)
            target.Add(value ?? "");
    }

    private void SortSaveMetaNewestFirst(GDictionaryArray entries)
    {
        var list = new List<GDictionary>();
        foreach (GDictionary entry in entries)
            list.Add(entry);
        list.Sort(
            (a, b) =>
            {
                if (SortSaveMetaNewestFirst(a, b))
                    return -1;
                if (SortSaveMetaNewestFirst(b, a))
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

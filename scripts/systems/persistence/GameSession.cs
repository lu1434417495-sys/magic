using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    private const int SaveVersion = 11;
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

    private static readonly Dictionary<StringName, int> RandomStartSkillLevelByTier = new()
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

    private readonly struct SaveIndexFileSignature
    {
        public static SaveIndexFileSignature Missing => new(false, -1, -1, "");

        public SaveIndexFileSignature(
            bool exists,
            int modifiedTime,
            int size,
            string fingerprint
        )
        {
            Exists = exists;
            ModifiedTime = modifiedTime;
            Size = size;
            Fingerprint = fingerprint ?? "";
        }

        public bool Exists { get; }
        public int ModifiedTime { get; }
        public int Size { get; }
        public string Fingerprint { get; }

        public bool Matches(SaveIndexFileSignature other) =>
            Exists == other.Exists
            && ModifiedTime == other.ModifiedTime
            && Size == other.Size
            && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
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
    private readonly Dictionary<string, object> _activeSaveMeta = new(StringComparer.Ordinal);
    internal string _generation_config_path = "";
    internal WorldMapGenerationConfig _generation_config;
    private readonly Dictionary<string, object> _worldData = new(StringComparer.Ordinal);
    internal Vector2I _player_coord = Vector2I.Zero;
    internal string _player_faction_id = "player";
    internal PartyState _party_state = new();
    internal bool _has_active_world;
    internal bool _battle_save_lock_enabled;
    internal bool _battle_save_dirty;
    internal bool _runtime_save_dirty;
    internal StringNameList _runtime_save_dirty_scopes = new();
    internal int _last_save_error = (int)Error.Ok;
    internal StringName _last_save_error_reason = "";
    private StringName _pending_load_error_reason = "";
    internal bool _post_decode_save_pending;
    internal StringNameList _post_decode_save_reasons = new();

    public ProgressionContentRegistry _progression_content_registry = new();
    public ItemContentRegistry _item_content_registry = new();
    public RecipeContentRegistry _recipe_content_registry = new();
    public EnemyContentRegistry _enemy_content_registry = new();
    internal BattleSpecialProfileRegistry _battle_special_profile_registry = new();
    internal GameRoot _game_root = new();

    public GDictionary _profession_defs = new();
    public GDictionary _achievement_defs = new();
    public GDictionary _quest_defs = new();
    public GDictionary _item_defs = new();
    public GDictionary _recipe_defs = new();
    public GDictionary _enemy_templates = new();
    public GDictionary _enemy_ai_brains = new();
    public GDictionary _wild_encounter_rosters = new();
    private ContentValidationSnapshotData _contentValidationSnapshotData = new();
    private Dictionary<StringName, SkillDefinition> _skillDefinitionIndex = new();
    private Dictionary<StringName, ProfessionDef> _professionDefIndex = new();
    private Dictionary<StringName, AchievementDef> _achievementDefIndex = new();
    private Dictionary<StringName, QuestDef> _questDefIndex = new();
    private Dictionary<StringName, ItemDef> _itemDefIndex = new();
    private Dictionary<StringName, RecipeDef> _recipeDefIndex = new();
    private Dictionary<StringName, EnemyTemplateDef> _enemyTemplateIndex = new();
    private Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrainIndex = new();
    private Dictionary<StringName, WildEncounterRosterDef> _wildEncounterRosterIndex = new();

    public SaveSerializer _save_serializer = new();
    private SaveRepository _save_repository;
    private GameLogService _log_service = new();
    public WorldMapContentValidator _world_content_validator = new();
    private IGameLogSink _log_sink;
    private bool _disposed;

    private List<Dictionary<string, object>> _saveIndexEntriesCache = new();
    private bool _saveIndexCacheValid;
    private SaveIndexFileSignature _saveIndexCacheSignature = SaveIndexFileSignature.Missing;

    public bool fail_payload_write;

    public GameSession()
    {
        EnsureGameRoot();
        _save_serializer.Setup(
            SaveVersion,
            SaveIndexVersion,
            MaxActiveMemberCount
        );
        _save_repository = BuildSaveRepository();

        RefreshProgressionContent();
        RefreshBattleSpecialProfiles();
        RefreshItemContent();
        RefreshRecipeContent();
        RefreshEnemyContent();
        RefreshContentValidationSnapshot();
        ReportContentValidationErrors();

        _log_sink = new GameSessionLogSink(this);
        GameLog.AddSink(_log_sink);
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        bool sessionInTree = IsSessionInTree();
        GC.SuppressFinalize(this);
        DisposeManagedSession(sessionInTree);
        if (GodotObject.IsInstanceValid(this))
        {
            if (sessionInTree)
                Free();
            else
                base.Dispose();
        }
    }

    public override void _ExitTree()
    {
        DisposeManagedSession(suppressContentFinalizers: true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeManagedSession(IsSessionInTree());
        }
        base.Dispose(disposing);
    }

    private void DisposeManagedSession(bool suppressContentFinalizers = false)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        DisposePartyStateGraph(_party_state);
        _party_state = null;
        DisposeOwnedRuntimeResources(suppressContentFinalizers);
        _log_service = null;
        if (_log_sink != null)
        {
            GameLog.RemoveSink(_log_sink);
            _log_sink = null;
        }
    }

    internal void DisposeOwnedRuntimeResources(bool suppressContentFinalizers = false)
    {
        HashSet<GodotObject> shutdownSuppressionVisited = suppressContentFinalizers
            ? new HashSet<GodotObject>()
            : null;
        if (shutdownSuppressionVisited != null)
        {
            SuppressOwnedContentFinalizerGraphsForShutdown(shutdownSuppressionVisited);
        }
        _game_root?.Dispose();
        _game_root = null;
        ClearSessionGodotObjectReferences(shutdownSuppressionVisited);
        _progression_content_registry?.Dispose();
        _progression_content_registry = null;
        _item_content_registry?.Dispose();
        _item_content_registry = null;
        _recipe_content_registry?.Dispose();
        _recipe_content_registry = null;
        _enemy_content_registry?.Dispose();
        _enemy_content_registry = null;
        _battle_special_profile_registry?.Dispose();
        _battle_special_profile_registry = null;
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

    private GameRoot EnsureGameRoot()
    {
        if (_game_root == null)
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
        ReplaceActiveSaveMetaPayload(BuildSaveMeta(
            saveId,
            saveId,
            generation_config_path,
            preset_id,
            resolvedPresetName,
            generationConfig.GetWorldSizeCells(),
            timestamp,
            timestamp
        ));
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
            DisposeCapturedPartyState(previousRuntimeState);
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
        _pending_load_error_reason = "";
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
            DisposeCapturedPartyState(previousRuntimeState);
        }
        else
        {
            StringName loadErrorReason =
                _pending_load_error_reason != "" ? _pending_load_error_reason : "load_save";
            RestoreRuntimeState(previousRuntimeState);
            RecordSaveError(loadError, loadErrorReason);
        }
        _pending_load_error_reason = "";
        return loadError;
    }

    public bool HasActiveWorld() => _has_active_world;

    public string GetActiveSaveId() => _active_save_id;

    public string GetActiveSavePath() => _active_save_path;

    public GDictionary GetActiveSaveMeta() => ActiveSaveMetaPayload().Duplicate(true);

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
        MarkRuntimePayload(
            _contentValidationSnapshotData.ToDictionary(),
            "GameSession.GetContentValidationSnapshot"
        );

    public GDictionary RefreshContentValidationSnapshot()
    {
        RefreshContentValidationSnapshotState();
        return GetContentValidationSnapshot();
    }

    internal GDictionary GetQuestDefsSnapshotForTests() =>
        RegisterContentProjectionWrapper(
            _quest_defs != null
                ? (GDictionary)_quest_defs.Duplicate(true)
                : new GDictionary(),
            "GameSession.GetQuestDefsSnapshotForTests"
        );

    internal void ReplaceQuestDefsForTests(GDictionary questDefs)
    {
        _quest_defs = RegisterContentProjectionWrapper(
            questDefs != null ? (GDictionary)questDefs.Duplicate(true) : new GDictionary(),
            "GameSession.ReplaceQuestDefsForTests"
        );
        _questDefIndex = BuildQuestDefIndex(_quest_defs);
        RefreshContentCatalog();
    }

    internal ItemContentRegistry GetItemContentRegistryForTests() => _item_content_registry;

    internal void ReplaceItemDefsForTests(GDictionary itemDefs)
    {
        _itemDefIndex = BuildItemDefIndex(itemDefs);
        _item_defs = RegisterContentProjectionWrapper(
            itemDefs != null ? (GDictionary)itemDefs.Duplicate(true) : new GDictionary(),
            "GameSession.ReplaceItemDefsForTests"
        );
        RefreshRecipeContent();
        RefreshContentCatalog();
    }

    internal void SetItemContentRegistryForTests(ItemContentRegistry registry)
    {
        _item_content_registry = registry ?? new ItemContentRegistry();
        RefreshItemContent();
        RefreshRecipeContent();
        RefreshContentCatalog();
    }

    internal WorldMapContentValidator GetWorldContentValidatorForTests() =>
        _world_content_validator;

    internal void SetWorldContentValidatorForTests(WorldMapContentValidator validator) =>
        _world_content_validator = validator ?? new WorldMapContentValidator();

    internal void ConfigureRuntimeWorldForTests(
        string saveId,
        string generationConfigPath,
        GDictionary worldData,
        PartyState partyState,
        GDictionary questDefs = null,
        string saveKind = "runtime_test",
        string displayName = "Runtime Test",
        Vector2I? mapSize = null
    )
    {
        int now = (int)Time.GetUnixTimeFromSystem();
        _active_save_id = saveId ?? "";
        _active_save_path = BuildSaveFilePath(_active_save_id);
        _generation_config_path = generationConfigPath ?? "";
        _generation_config = ResourceLoader.Load<WorldMapGenerationConfig>(_generation_config_path);
        RegisterStaticContentOwnership(_generation_config);
        ReplaceWorldDataPayload(worldData ?? new GDictionary());
        _player_coord = Vector2I.Zero;
        _player_faction_id = "player";
        PartyState previousPartyState = _party_state;
        _party_state = partyState ?? new PartyState();
        DisposePartyStateGraph(previousPartyState, _party_state);
        if (questDefs != null)
        {
            _quest_defs = questDefs;
            _questDefIndex = BuildQuestDefIndex(_quest_defs);
        }
        _has_active_world = true;
        _battle_save_lock_enabled = false;
        ReplaceActiveSaveMetaPayload(BuildSaveMeta(
            _active_save_id,
            _active_save_id,
            _generation_config_path,
            saveKind,
            displayName,
            mapSize ?? new Vector2I(8, 8),
            now,
            now
        ));
        DiscardPendingSave();
        RefreshContentCatalog();
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

    internal void ReplaceWorldDataPayloadForRuntimeRestore(GDictionary worldData)
    {
        ReplaceWorldDataPayload(worldData ?? new GDictionary());
    }

    private GDictionary ActiveSaveMetaPayload() =>
        RuntimePlainPayload.ProjectDictionary(_activeSaveMeta, "GameSession.active_save_meta");

    private void ReplaceActiveSaveMetaPayload(GDictionary payload)
    {
        ReplacePlainPayload(
            _activeSaveMeta,
            payload ?? new GDictionary(),
            "GameSession.active_save_meta"
        );
    }

    private void ClearActiveSaveMetaPayload() => _activeSaveMeta.Clear();

    private GDictionary WorldDataPayload() =>
        RuntimePlainPayload.ProjectDictionary(_worldData, "GameSession.world_data");

    private void ReplaceWorldDataPayload(GDictionary payload)
    {
        ReplacePlainPayload(_worldData, payload ?? new GDictionary(), "GameSession.world_data");
    }

    private void ClearWorldDataPayload() => _worldData.Clear();

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        GDictionary payload,
        string ownerPath
    )
    {
        target.Clear();
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(payload, ownerPath);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            target[entry.Key] = entry.Value;
        }
    }

    public GDictionary GetWorldData() => WorldDataPayload();

    public StringName AllocateEquipmentInstanceId()
    {
        GDictionary worldData = WorldDataPayload();
        if (worldData == null || !worldData.ContainsKey(WorldEquipmentInstanceSerialKey))
            return "";
        GDictionary usedIds = CollectPersistentEquipmentInstanceIds();
        int serial = GetInt(worldData, WorldEquipmentInstanceSerialKey, 0);
        if (serial < 1)
            return "";
        while (true)
        {
            StringName candidate = EquipmentInstanceState.FormatInstanceId(serial);
            serial += 1;
            worldData[WorldEquipmentInstanceSerialKey] = serial;
            if (!usedIds.ContainsKey(candidate.ToString()))
            {
                ReplaceWorldDataPayload(worldData);
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
        ReplaceWorldDataPayload(normalizedWorldData);
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
        if (ReferenceEquals(_party_state, party_state))
        {
            _party_state ??= new PartyState();
            MarkRuntimeStateDirty(SaveDirtyScopePartyState);
            return (int)Error.Ok;
        }

        PartyState previousPartyState = _party_state;
        _party_state = NormalizePartyState(party_state);
        DisposePartyStateGraph(previousPartyState, _party_state);
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
            ["dirty_scopes"] = _runtime_save_dirty_scopes.ToGodotArray(),
            ["battle_save_locked"] = _battle_save_lock_enabled,
            ["last_error"] = _last_save_error,
            ["last_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = _post_decode_save_reasons.ToGodotArray(),
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
        foreach (PartyMemberState memberState in _party_state.GetMemberStates())
        {
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

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        if (_skillDefinitionIndex.Count > 0 || _progression_content_registry == null)
            return new Dictionary<StringName, SkillDefinition>(_skillDefinitionIndex);
        return _progression_content_registry.GetSkillDefinitionsTyped();
    }

    public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped()
    {
        return _progression_content_registry?.GetTraitDefsTyped()
            ?? new Dictionary<StringName, TraitDef>();
    }

    public EquipmentAbilityRegistryBuildResult GetEquipmentAbilityLastBuildResultTyped()
    {
        return _progression_content_registry?.GetEquipmentAbilityLastBuildResultTyped()
            ?? new EquipmentAbilityRegistryBuildResult
            {
                Success = true,
                Revision = 0,
                Errors = Array.Empty<string>(),
            };
    }

    public int GetEquipmentAbilityContentRevision()
    {
        return _progression_content_registry?.GetEquipmentAbilityContentRevision() ?? 0;
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetEquipmentAbilityPackDefinitionsTyped()
    {
        return _progression_content_registry?.GetEquipmentAbilityPackDefinitionsTyped()
            ?? new Dictionary<StringName, EquipmentAbilityContentPackDefinition>();
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindingDefinitionsTyped()
    {
        return _progression_content_registry?.GetEquipmentAbilityBindingDefinitionsTyped()
            ?? new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
    }

    public GDictionary GetBattleSpecialProfileRegistrySnapshot() =>
        _battle_special_profile_registry != null
            ? _battle_special_profile_registry.GetSnapshot()
            : new GDictionary();

    public GDictionary GetBattleSpecialProfileRegistryRuntimeSnapshot() =>
        _battle_special_profile_registry != null
            ? _battle_special_profile_registry.GetRuntimeSnapshotPayload()
            : new GDictionary();

    internal IBattleSpecialProfileView GetBattleSpecialProfileRuntimeView() =>
        _battle_special_profile_registry != null
            ? _battle_special_profile_registry.BuildRuntimeProfileView()
            : BattleSpecialProfileRuntimeView.Empty;

    public IReadOnlyDictionary<StringName, ProfessionDef> GetProfessionDefsTyped()
    {
        return new Dictionary<StringName, ProfessionDef>(_professionDefIndex);
    }

    public IReadOnlyDictionary<StringName, AchievementDef> GetAchievementDefsTyped()
    {
        return new Dictionary<StringName, AchievementDef>(_achievementDefIndex);
    }

    public QuestDef GetQuestDef(StringName quest_id)
    {
        if (quest_id == "")
            return null;
        return _questDefIndex.TryGetValue(quest_id, out QuestDef questDef) ? questDef : null;
    }

    public IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped()
    {
        return new Dictionary<StringName, QuestDef>(_questDefIndex);
    }

    public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped()
    {
        return new Dictionary<StringName, ItemDef>(_itemDefIndex);
    }

    public IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped()
    {
        return new Dictionary<StringName, RecipeDef>(_recipeDefIndex);
    }

    public IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplatesTyped()
    {
        return new Dictionary<StringName, EnemyTemplateDef>(_enemyTemplateIndex);
    }

    public IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainsTyped()
    {
        return new Dictionary<StringName, EnemyAiBrainDef>(_enemyAiBrainIndex);
    }

    public IReadOnlyDictionary<StringName, WildEncounterRosterDef> GetWildEncounterRostersTyped()
    {
        return new Dictionary<StringName, WildEncounterRosterDef>(_wildEncounterRosterIndex);
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

        RegisterStaticContentOwnership(content_def);
        registry[content_key] = content_def;
        RebuildTypedContentIndexForDomain(domain_id);
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

        RegisterStaticContentOwnership(content_def);
        registry[content_key] = content_def;
        RebuildTypedContentIndexForDomain(domain_id);
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
                registry = new GDictionary();
                return false;
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

    private void RebuildTypedContentIndexForDomain(StringName domain_id)
    {
        switch (domain_id.ToString())
        {
            case "profession":
                _professionDefIndex = BuildProfessionDefIndex(_profession_defs);
                break;
            case "achievement":
                _achievementDefIndex = BuildAchievementDefIndex(_achievement_defs);
                break;
            case "quest":
                _questDefIndex = BuildQuestDefIndex(_quest_defs);
                break;
            case "item":
                _itemDefIndex = BuildItemDefIndex(_item_defs);
                break;
            case "recipe":
                _recipeDefIndex = BuildRecipeDefIndex(_recipe_defs);
                break;
            case "enemy_template":
                _enemyTemplateIndex = BuildEnemyTemplateIndex(_enemy_templates);
                break;
            case "enemy_ai_brain":
                _enemyAiBrainIndex = BuildEnemyAiBrainIndex(_enemy_ai_brains);
                break;
            case "wild_encounter_roster":
                _wildEncounterRosterIndex = BuildWildEncounterRosterIndex(
                    _wild_encounter_rosters
                );
                break;
        }
    }

    internal void SetSkillDefinitionForTests(
        StringName skillId,
        SkillDefinition skillDefinition
    )
    {
        if (skillId == "" || skillDefinition == null)
            return;
        _skillDefinitionIndex[skillId] = skillDefinition;
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
        ResetRuntimeState(dispose_current_party_state: false);
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
        GDictionary worldData = WorldMapSpawnProjection.Project(worldBuild);

        _generation_config_path = generation_config_path;
        _generation_config = generation_config;
        RegisterStaticContentOwnership(_generation_config);
        ReplaceWorldDataPayload(NormalizeWorldData(worldData));
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
        GDictionary activeSaveMeta = ActiveSaveMetaPayload();
        string displayName = GetString(activeSaveMeta, "display_name", _active_save_id);
        ReplaceActiveSaveMetaPayload(BuildSaveMeta(
            _active_save_id,
            displayName,
            _generation_config_path,
            new StringName(GetString(activeSaveMeta, "world_preset_id")),
            GetString(activeSaveMeta, "world_preset_name"),
            _generation_config != null ? _generation_config.GetWorldSizeCells() : Vector2I.Zero,
            GetInt(activeSaveMeta, "created_at_unix_time", now),
            now
        ));

        int payloadWriteError = WriteSavePayloadAtomically(
            _active_save_path,
            BuildSavePayload(now)
        );
        if (payloadWriteError != (int)Error.Ok)
            return payloadWriteError;

        int indexError = WriteSaveIndex(
            UpsertSaveMeta(LoadSaveIndexEntries(), ActiveSaveMetaPayload())
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
            PartyState.TryReadPartyPayload(decodeResult["party_state"], out PartyState parsedPartyState)
                ? parsedPartyState
                : new PartyState();
        int identityError = ValidateDecodedPartyIdentityForSave(
            decodedPartyState,
            GetString(decodeResult, "active_save_id"),
            "load_save"
        );
        if (identityError != (int)Error.Ok)
            return identityError;
        IReadOnlyList<string> contingencyContentErrors =
            ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
                decodedPartyState,
                GetContentCatalogTyped()
            );
        if (contingencyContentErrors.Count > 0)
        {
            _pending_load_error_reason = "contingency_content_validation";
            PushSessionError(
                "session.save.load.contingency_content_invalid",
                "存档中的连锁应急术配置引用了非法技能内容。",
                Json.Stringify(new GDictionary
                {
                    ["error_count"] = contingencyContentErrors.Count,
                    ["first_error"] = contingencyContentErrors[0],
                })
            );
            return (int)Error.InvalidData;
        }

        ResetRuntimeState();
        _active_save_id = GetString(decodeResult, "active_save_id");
        _active_save_path = BuildSaveFilePath(_active_save_id);
        ReplaceActiveSaveMetaPayload(GetDictionary(decodeResult, "active_save_meta").Duplicate(true));
        _generation_config_path = GetString(
            decodeResult,
            "generation_config_path",
            generation_config_path
        );
        _generation_config =
            (ReadGodotObject(decodeResult, "generation_config") ?? generation_config)
                as WorldMapGenerationConfig
            ?? generation_config;
        RegisterStaticContentOwnership(_generation_config);
        ReplaceWorldDataPayload(GetDictionary(decodeResult, "world_data").Duplicate(true));
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
        foreach (PartyMemberState memberState in party_state.GetMemberStates())
        {
            changed =
                RefreshMemberBodySizeFromIdentity(memberState) || changed;
        }
        return changed;
    }

    private bool BackfillRacialGrantedSkills(PartyState party_state)
    {
        return RacialSkillGrantService.BackfillParty(
            party_state,
            GetProgressionIdentityCatalogTyped(),
            GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            GetProfessionDefsTyped()
        );
    }

    private bool RevokeOrphanRacialSkills(PartyState party_state)
    {
        return RacialSkillGrantService.RevokeOrphanParty(
            party_state,
            GetProgressionIdentityCatalogTyped(),
            GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            GetProfessionDefsTyped()
        );
    }

    private GDictionary BuildSavePayload(int saved_at_unix_time)
    {
        return _save_serializer.BuildSavePayload(
            _active_save_id,
            _generation_config_path,
            ActiveSaveMetaPayload(),
            WorldDataPayload(),
            _player_coord,
            _player_faction_id,
            _party_state,
            saved_at_unix_time
        );
    }

    private GDictionary BuildWorldStatePayload()
    {
        return _save_serializer.BuildWorldStatePayload(
            WorldDataPayload(),
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
            string saveId = $"{idPrefix}_{TrueRandomSeedService.RandiRange(0, 999999):D6}";
            if (
                !existingSaveIds.ContainsKey(saveId)
                && !FileAccess.FileExists(BuildSaveFilePath(saveId))
            )
            {
                return saveId;
            }
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
        GodotContentOwnership.RegisterBorrowedContent(
            generationConfig,
            $"GameSession.LoadGenerationConfig:{generation_config_path}"
        );
        return generationConfig;
    }

    public GDictionary ReadSavePayload(string save_path, bool emit_errors = true)
    {
        return EnsureSaveRepository().ReadSavePayload(save_path, emit_errors);
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
            ["active_save_meta"] = ActiveSaveMetaPayload().Duplicate(true),
            ["generation_config_path"] = _generation_config_path,
            ["world_data"] = WorldDataPayload().Duplicate(true),
            ["player_coord"] = _player_coord,
            ["player_faction_id"] = _player_faction_id,
            ["party_state"] = _party_state?.ToDictionary() ?? new GDictionary(),
            ["has_active_world"] = _has_active_world,
            ["battle_save_lock_enabled"] = _battle_save_lock_enabled,
            ["battle_save_dirty"] = _battle_save_dirty,
            ["runtime_save_dirty"] = _runtime_save_dirty,
            ["runtime_save_dirty_scopes"] = _runtime_save_dirty_scopes.ToGodotArray(),
            ["last_save_error"] = _last_save_error,
            ["last_save_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = _post_decode_save_reasons.ToGodotArray(),
        };
    }

    public void RestoreRuntimeState(GDictionary state)
    {
        PartyState previousPartyState = _party_state;
        _active_save_id = GetString(state, "active_save_id");
        _active_save_path = GetString(state, "active_save_path");
        ReplaceActiveSaveMetaPayload(GetDictionary(state, "active_save_meta").Duplicate(true));
        _generation_config_path = GetString(state, "generation_config_path");
        _generation_config = string.IsNullOrEmpty(_generation_config_path)
            ? null
            : LoadGenerationConfig(_generation_config_path);
        ReplaceWorldDataPayload(GetDictionary(state, "world_data").Duplicate(true));
        _player_coord = GetVector2I(state, "player_coord", Vector2I.Zero);
        _player_faction_id = GetString(state, "player_faction_id", "player");
        _party_state =
            PartyState.TryReadPartyPayload(state["party_state"], out PartyState restoredPartyState)
                ? restoredPartyState
                : new PartyState();
        DisposePartyStateGraph(previousPartyState, _party_state);
        _has_active_world = ReadExactBool(state, "has_active_world", false);
        _battle_save_lock_enabled = ReadExactBool(state, "battle_save_lock_enabled", false);
        _battle_save_dirty = ReadExactBool(state, "battle_save_dirty", false);
        _runtime_save_dirty = ReadExactBool(state, "runtime_save_dirty", false);
        _runtime_save_dirty_scopes = new StringNameList(
            ProgressionDataUtils.to_string_name_array(
            GetArray(state, "runtime_save_dirty_scopes")
            )
        );
        _last_save_error = GetInt(state, "last_save_error", (int)Error.Ok);
        _last_save_error_reason = ProgressionDataUtils.to_string_name(
            GetString(state, "last_save_error_reason")
        );
        _post_decode_save_pending = ReadExactBool(state, "post_decode_save_pending", false);
        _post_decode_save_reasons = new StringNameList(
            ProgressionDataUtils.to_string_name_array(
            GetArray(state, "post_decode_save_reasons")
            )
        );
    }

    private void ResetRuntimeState(bool dispose_current_party_state = true)
    {
        PartyState previousPartyState = _party_state;
        _active_save_id = "";
        _active_save_path = "";
        ClearActiveSaveMetaPayload();
        _generation_config_path = "";
        _generation_config = null;
        ClearWorldDataPayload();
        _player_coord = Vector2I.Zero;
        _player_faction_id = "player";
        _party_state = new PartyState();
        if (dispose_current_party_state)
            DisposePartyStateGraph(previousPartyState, _party_state);
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

    private SaveRepository EnsureSaveRepository()
    {
        _save_repository ??= BuildSaveRepository();
        return _save_repository;
    }

    private SaveRepository BuildSaveRepository() =>
        new(
            _save_serializer,
            SaveDirectory,
            SaveFileCompressionMode,
            PushSessionError,
            ShouldFailPayloadWrite
        );

    private bool ShouldFailPayloadWrite()
    {
        var failValue = Get("fail_payload_write");
        return fail_payload_write
            || (failValue.VariantType == Variant.Type.Bool && failValue.AsBool());
    }

}

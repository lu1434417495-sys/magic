using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class GameSession : Node, IApplicationShutdownParticipant
{
    private const string ApplicationShutdownParticipantId = "game-session";
    private const int ApplicationShutdownOrder = 0;
    private const string SaveDirectory = "user://saves";
    private const string SaveIndexPath = "user://saves/index.dat";
    private const int SaveVersion = 12;
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

        public Dictionary<string, object> BuildSnapshotPlain()
        {
            var errors = new List<object>();
            foreach (string error in Errors)
                errors.Add(error);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ok"] = Ok,
                ["error_count"] = ErrorCount,
                ["errors"] = errors,
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

        public Dictionary<string, object> BuildSnapshotPlain()
        {
            var domainSnapshots = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string domainId in ContentValidationDomainOrder)
            {
                domainSnapshots[domainId] = Domains.TryGetValue(
                    domainId,
                    out ContentValidationDomainSnapshotData domain
                )
                    ? domain?.BuildSnapshotPlain()
                        ?? new Dictionary<string, object>(StringComparer.Ordinal)
                    : new Dictionary<string, object>(StringComparer.Ordinal);
            }
            var domainOrder = new List<object>();
            foreach (string domainId in ContentValidationDomainOrder)
                domainOrder.Add(domainId);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ok"] = Ok,
                ["error_count"] = ErrorCount,
                ["domain_order"] = domainOrder,
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
    internal WorldGenerationDefinition _generation_definition;
    private string _bound_generation_definition_path = "";
    private WorldGenerationDefinition _bound_generation_definition;
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

    private ContentSnapshot _contentSnapshot;
    private ProcessContentHost _contentBorrowerHost;
    private string _contentBorrowerId = "";
    internal GameRoot _game_root = new();
    private ContentValidationSnapshotData _contentValidationSnapshotData = new();

    public SaveSerializer _save_serializer = new();
    private SaveRepository _save_repository;
    private GameLogService _log_service = new();
    private IGameLogSink _log_sink;
    private bool _disposed;
    private bool _contentBindingEstablished;
    private bool _ownedRuntimeResourcesDisposed;
    private ApplicationLifetimeCoordinator _applicationLifetimeCoordinator;

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

        _log_sink = new GameSessionLogSink(this);
        GameLog.AddSink(_log_sink);
    }

    internal void BindContent(ContentSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_contentBindingEstablished)
            throw new InvalidOperationException("GameSession content is already bound.");

        GameRoot gameRoot = EnsureGameRoot();
        try
        {
            gameRoot.BindSnapshot(this, snapshot);
            _contentSnapshot = snapshot;
            _contentBindingEstablished = true;
            RefreshContentValidationSnapshotState();
            ReportContentValidationErrors();
        }
        catch
        {
            ClearContentBindingForRetry(snapshot);
            throw;
        }
    }

    internal void BindContentBorrower(ProcessContentHost host, string borrowerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(host);
        if (string.IsNullOrWhiteSpace(borrowerId))
            throw new ArgumentException("Content borrower ID is required.", nameof(borrowerId));
        if (_contentSnapshot == null || !ReferenceEquals(host.GetSnapshot(), _contentSnapshot))
        {
            throw new InvalidOperationException(
                "GameSession may only release the process host snapshot it currently borrows."
            );
        }
        if (_contentBorrowerHost != null || _contentBorrowerId.Length != 0)
            throw new InvalidOperationException("GameSession content borrower is already bound.");

        _contentBorrowerHost = host;
        _contentBorrowerId = borrowerId;
    }

    internal void RollBackFailedContentAttachment(
        ContentSnapshot snapshot,
        ProcessContentHost host,
        string borrowerId,
        ApplicationLifetimeCoordinator coordinator
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(coordinator);

        if (ReferenceEquals(_applicationLifetimeCoordinator, coordinator))
            _applicationLifetimeCoordinator = null;
        if (
            ReferenceEquals(_contentBorrowerHost, host)
            && string.Equals(_contentBorrowerId, borrowerId, StringComparison.Ordinal)
        )
        {
            _contentBorrowerHost = null;
            _contentBorrowerId = "";
        }
        ClearContentBindingForRetry(snapshot);
    }

    private void ClearContentBindingForRetry(ContentSnapshot expectedSnapshot)
    {
        if (!ReferenceEquals(_contentSnapshot, expectedSnapshot))
            return;

        _game_root?.ClearSnapshotBindingForRetry();
        _contentSnapshot = null;
        _contentBindingEstablished = false;
        _contentValidationSnapshotData = new ContentValidationSnapshotData();
    }

    internal bool IsClosed => _disposed;

    internal void BindApplicationLifetimeCoordinator(
        ApplicationLifetimeCoordinator coordinator
    )
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (
            _applicationLifetimeCoordinator != null
            && !ReferenceEquals(_applicationLifetimeCoordinator, coordinator)
        )
        {
            throw new InvalidOperationException(
                "GameSession is already attached to another lifetime coordinator."
            );
        }
        _applicationLifetimeCoordinator = coordinator;
    }

    internal long GetContentSnapshotEpoch() => RequireContentSnapshot().Epoch;

    private ContentSnapshot RequireContentSnapshot() =>
        _contentSnapshot
        ?? throw new InvalidOperationException(
            "GameSession content must be explicitly bound before runtime use."
        );

    private void ReleaseContentBorrower()
    {
        ProcessContentHost host = _contentBorrowerHost;
        string borrowerId = _contentBorrowerId;
        _contentBorrowerHost = null;
        _contentBorrowerId = "";
        if (host == null || borrowerId.Length == 0)
            return;
        host.UnregisterSnapshotBorrower(borrowerId);
    }

    string IApplicationShutdownParticipant.ShutdownParticipantId =>
        ApplicationShutdownParticipantId;

    ApplicationShutdownParticipantStage IApplicationShutdownParticipant.ShutdownStage =>
        ApplicationShutdownParticipantStage.Session;

    int IApplicationShutdownParticipant.ShutdownOrder => ApplicationShutdownOrder;

    ValueTask IApplicationShutdownParticipant.CloseForApplicationShutdownAsync(
        ShutdownReport report
    )
    {
        CloseNormal();
        return ValueTask.CompletedTask;
    }

    public override void _Ready()
    {
        SceneTree tree = GetTree();
        if (!ReferenceEquals(tree.Root.GetNodeOrNull<GameSession>("GameSession"), this))
            return;

        _applicationLifetimeCoordinator = tree.Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        if (_applicationLifetimeCoordinator == null)
        {
            throw new InvalidOperationException(
                "The canonical GameSession owner requires ApplicationLifetimeCoordinator."
            );
        }

        if (!_applicationLifetimeCoordinator.CanAttachSession)
            return;

        _applicationLifetimeCoordinator.AttachSession(this);
    }

    public new void Dispose()
    {
        bool sessionInTree = IsSessionInTree();
        CloseNormal();
        if (GodotObject.IsInstanceValid(this))
        {
            if (sessionInTree)
                Free();
            else
                base.Dispose();
        }
    }

    public override void _ExitTree() => CloseNormal();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseNormal();
        }
        base.Dispose(disposing);
    }

    internal void CloseNormal()
    {
        if (_disposed)
        {
            UnregisterApplicationShutdownParticipant();
            ReleaseContentBorrower();
            return;
        }
        _disposed = true;
        UnregisterApplicationShutdownParticipant();
        DisposePartyStateGraph(_party_state);
        _party_state = null;
        DisposeOwnedRuntimeResources();
        _log_service = null;
        RemoveLogSink();
    }

    private void UnregisterApplicationShutdownParticipant()
    {
        ApplicationLifetimeCoordinator coordinator = _applicationLifetimeCoordinator;
        _applicationLifetimeCoordinator = null;
        if (coordinator == null || !GodotObject.IsInstanceValid(coordinator))
            return;

        coordinator.UnregisterParticipant(this);
        coordinator.NotifySessionClosed(this);
    }

    private void RemoveLogSink()
    {
        if (_log_sink != null)
        {
            GameLog.RemoveSink(_log_sink);
            _log_sink = null;
        }
    }

    internal void DisposeOwnedRuntimeResources()
    {
        if (_ownedRuntimeResourcesDisposed)
            return;
        _ownedRuntimeResourcesDisposed = true;
        _game_root?.Dispose();
        _game_root = null;
        ClearSessionGodotObjectReferences();
        _contentSnapshot = null;
        ReleaseContentBorrower();
    }

    public int EnsureWorldReady(string generation_config_path)
    {
        generation_config_path = ContentPathCanonicalizer.Canonicalize(generation_config_path);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownedRuntimeResourcesDisposed)
        {
            throw new ObjectDisposedException(
                nameof(GameRoot),
                "GameSession owned runtime resources have already been released."
            );
        }
        if (_game_root == null)
        {
            _game_root = new GameRoot();
        }
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

        if (string.IsNullOrEmpty(generation_config_path))
        {
            throw new InvalidOperationException(
                "GameSession requires a generation config path."
            );
        }
        generation_config_path = ContentPathCanonicalizer.Canonicalize(generation_config_path);

        WorldGenerationDefinition generationDefinition = ResolveBoundGenerationDefinition(
            generation_config_path
        );
        if (generationDefinition == null)
            return (int)Error.CantOpen;

        using GodotProjectionLease<GDictionary> previousRuntimeStateLease =
            CaptureRuntimeStateLease();
        GDictionary previousRuntimeState = previousRuntimeStateLease.Value;
        WorldGenerationDefinition previousGenerationDefinition = _generation_definition;

        int prepareError = PrepareNewWorld(generation_config_path, generationDefinition);
        if (prepareError != (int)Error.Ok)
        {
            RestoreRuntimeState(previousRuntimeState, previousGenerationDefinition);
            return prepareError;
        }

        int characterCreationError = ApplyCharacterCreationPayloadToMainCharacter(
            character_creation_payload
        );
        if (characterCreationError != (int)Error.Ok)
        {
            RestoreRuntimeState(previousRuntimeState, previousGenerationDefinition);
            return characterCreationError;
        }

        int timestamp = (int)Time.GetUnixTimeFromSystem();
        string saveId = GenerateUniqueSaveId(timestamp);
        if (string.IsNullOrEmpty(saveId))
        {
            RestoreRuntimeState(previousRuntimeState, previousGenerationDefinition);
            throw new InvalidOperationException(
                "GameSession failed to allocate a unique save id."
            );
        }

        _active_save_id = saveId;
        _active_save_path = BuildSaveFilePath(saveId);
        string resolvedPresetName = string.IsNullOrEmpty(preset_name)
            ? WorldPresetRegistry.GetFallbackPresetName(generation_config_path)
            : preset_name;
        ReplaceActiveSaveMetaPlain(BuildSaveMetaPlain(
            saveId,
            saveId,
            generation_config_path,
            preset_id,
            resolvedPresetName,
            generationDefinition.GetWorldSizeCells(),
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
            RestoreRuntimeState(previousRuntimeState, previousGenerationDefinition);
        }
        return persistError;
    }

    internal List<Dictionary<string, object>> ListSaveSlotsPlain() =>
        LoadSaveIndexEntriesPlain();

    internal List<Dictionary<string, object>> PeekSaveSlotsPlain() =>
        PeekSaveIndexEntriesPlain();

    public int LoadSave(string save_id)
    {
        if (!_save_serializer.IsValidSaveIdToken(save_id))
            return (int)Error.InvalidParameter;
        int contentValidationError = RequireContentValidationForRuntime("load_save");
        if (contentValidationError != (int)Error.Ok)
            return contentValidationError;

        Dictionary<string, object> saveMeta = GetSaveMetaByIdPlain(save_id);
        if (saveMeta.Count == 0)
        {
            throw new InvalidOperationException(
                $"GameSession could not find save slot {save_id}."
            );
        }

        string savePath = BuildSaveFilePath(save_id);
        int readError = ReadSavePayload(
            savePath,
            out Dictionary<string, object> plainPayload
        );
        if (readError != (int)Error.Ok)
            return readError;

        if (plainPayload.Count == 0)
        {
            throw new InvalidOperationException(
                $"GameSession loaded an invalid payload from {savePath}."
            );
        }
        if (
            !plainPayload.TryGetValue("generation_config_path", out object generationPathValue)
            || generationPathValue is not string generationConfigPath
        )
        {
            throw new InvalidOperationException(
                $"Save slot {save_id} is missing generation_config_path."
            );
        }

        generationConfigPath = generationConfigPath.Trim();
        if (string.IsNullOrEmpty(generationConfigPath))
        {
            throw new InvalidOperationException(
                $"Save slot {save_id} is missing generation_config_path."
            );
        }

        WorldGenerationDefinition generationDefinition = ResolveBoundGenerationDefinition(
            generationConfigPath
        );
        if (generationDefinition == null)
            return (int)Error.CantOpen;

        using GodotProjectionLease<GDictionary> previousRuntimeStateLease =
            CaptureRuntimeStateLease();
        GDictionary previousRuntimeState = previousRuntimeStateLease.Value;
        WorldGenerationDefinition previousGenerationDefinition = _generation_definition;
        _pending_load_error_reason = "";
        int loadError = LoadCurrentPayload(
            plainPayload,
            generationConfigPath,
            generationDefinition,
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
            RestoreRuntimeState(previousRuntimeState, previousGenerationDefinition);
            RecordSaveError(loadError, loadErrorReason);
        }
        _pending_load_error_reason = "";
        return loadError;
    }

    public bool HasActiveWorld() => _has_active_world;

    public string GetActiveSaveId() => _active_save_id;

    public string GetActiveSavePath() => _active_save_path;

    internal Dictionary<string, object> CaptureActiveSaveMetaPlain() =>
        RuntimePlainPayload.CloneDictionary(_activeSaveMeta);

    internal Dictionary<string, object> CaptureWorldDataPlain() =>
        RuntimePlainPayload.CloneDictionary(_worldData);

    internal GameLogService GetLogService() => _log_service;

    internal IReadOnlyDictionary<string, object> GetLogSnapshotPlain(int limit = 50) =>
        _log_service != null
            ? _log_service.BuildSnapshotPlain(limit)
            : new Dictionary<string, object>(StringComparer.Ordinal);

    public string GetActiveLogFilePath() =>
        _log_service != null ? _log_service.GetLogPath() : "";

    public string AllocateUniqueSaveId() => AllocateUniqueSaveId("save");

    public string AllocateUniqueSaveId(string prefix = "save") =>
        GenerateUniqueSaveId((int)Time.GetUnixTimeFromSystem(), prefix);

    internal Dictionary<string, object> GetContentValidationSnapshot() =>
        _contentValidationSnapshotData.BuildSnapshotPlain();

    internal Dictionary<string, object> RefreshContentValidationSnapshot()
    {
        RefreshContentValidationSnapshotState();
        return GetContentValidationSnapshot();
    }

    internal void ConfigureRuntimeWorldForTests(
        string saveId,
        string generationConfigPath,
        GDictionary worldData,
        PartyState partyState,
        string saveKind = "runtime_test",
        string displayName = "Runtime Test",
        Vector2I? mapSize = null,
        WorldGenerationDefinition generationDefinition = null
    )
    {
        int now = (int)Time.GetUnixTimeFromSystem();
        _active_save_id = saveId ?? "";
        _active_save_path = BuildSaveFilePath(_active_save_id);
        if (generationDefinition != null)
            BindGenerationDefinition(generationConfigPath, generationDefinition);
        WorldGenerationDefinition resolvedGenerationDefinition =
            ResolveBoundGenerationDefinition(generationConfigPath);
        if (resolvedGenerationDefinition == null)
            throw new InvalidOperationException(
                "Runtime world tests require the bound snapshot to contain the requested WorldGenerationDefinition."
            );
        _generation_config_path = ContentPathCanonicalizer.Canonicalize(generationConfigPath);
        _generation_definition = resolvedGenerationDefinition;
        ReplaceWorldDataPayload(worldData ?? new GDictionary());
        _player_coord = Vector2I.Zero;
        _player_faction_id = "player";
        PartyState previousPartyState = _party_state;
        _party_state = partyState ?? new PartyState();
        DisposePartyStateGraph(previousPartyState, _party_state);
        _has_active_world = true;
        _battle_save_lock_enabled = false;
        ReplaceActiveSaveMetaPlain(BuildSaveMetaPlain(
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
    }

    public bool IsContentValidationOk() =>
        _contentSnapshot != null && (_contentValidationSnapshotData?.Ok ?? false);

    public void LogEvent(
        string level,
        string domain,
        string event_id,
        string message,
        string context = ""
    )
    {
        _log_service?.AppendEntry(level, domain, event_id, message, context);
    }

    public void LogEvent(string level, string domain, string event_id, string message)
    {
        LogEvent(level, domain, event_id, message, "");
    }

    internal void BindGenerationDefinition(
        string canonicalPath,
        WorldGenerationDefinition definition
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        string normalizedPath = ContentPathCanonicalizer.Canonicalize(canonicalPath);
        string definitionPath = ContentPathCanonicalizer.Canonicalize(definition.CanonicalPath);
        if (!string.Equals(normalizedPath, definitionPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"World generation definition path mismatch: requested {normalizedPath}, definition {definitionPath}."
            );
        }
        _bound_generation_definition_path = normalizedPath;
        _bound_generation_definition = definition;
    }

    public WorldGenerationDefinition GetGenerationDefinition() => _generation_definition;

    public string GetGenerationConfigPath() => _generation_config_path;

    internal void ReplaceWorldDataPayloadForRuntimeRestore(GDictionary worldData)
    {
        ReplaceWorldDataPayload(worldData ?? new GDictionary());
    }

    private GodotProjectionLease<GDictionary> ActiveSaveMetaPayloadLease() =>
        RuntimePlainPayload.ProjectDictionaryLease(
            _activeSaveMeta,
            "GameSession.active_save_meta",
            LifetimeDomain.Request,
            "GameSession.active_save_meta"
        );

    private void ReplaceActiveSaveMetaPayload(GDictionary payload)
    {
        ReplacePlainPayload(
            _activeSaveMeta,
            payload ?? new GDictionary(),
            "GameSession.active_save_meta"
        );
    }

    private void ReplaceActiveSaveMetaPlain(
        IReadOnlyDictionary<string, object> payload
    )
    {
        ReplacePlainPayload(_activeSaveMeta, payload);
    }

    private void ClearActiveSaveMetaPayload() => _activeSaveMeta.Clear();

    private GodotProjectionLease<GDictionary> WorldDataPayloadLease() =>
        RuntimePlainPayload.ProjectDictionaryLease(
            _worldData,
            "GameSession.world_data",
            LifetimeDomain.Request,
            "GameSession.world_data"
        );

    private void ReplaceWorldDataPayload(GDictionary payload)
    {
        ReplacePlainPayload(_worldData, payload ?? new GDictionary(), "GameSession.world_data");
    }

    private void ReplaceWorldDataPlain(IReadOnlyDictionary<string, object> payload)
    {
        ReplacePlainPayload(_worldData, payload);
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

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> payload
    )
    {
        target.Clear();
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(payload);
        foreach (KeyValuePair<string, object> entry in cloned)
            target[entry.Key] = entry.Value;
    }

    internal GodotProjectionLease<GDictionary> GetWorldDataLease() =>
        WorldDataPayloadLease();

    internal IReadOnlyDictionary<string, object> GetWorldDataSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_worldData);

    public StringName AllocateEquipmentInstanceId()
    {
        if (!_worldData.TryGetValue(WorldEquipmentInstanceSerialKey, out object rawSerial))
            return "";
        GDictionary usedIds = CollectPersistentEquipmentInstanceIds();
        int serial = rawSerial switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => 0,
        };
        if (serial < 1)
            return "";
        while (true)
        {
            StringName candidate = EquipmentInstanceState.FormatInstanceId(serial);
            serial += 1;
            _worldData[WorldEquipmentInstanceSerialKey] = serial;
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

    internal GodotProjectionLease<GDictionary> GetSaveStatusLease()
    {
        var dirtyScopes = new List<object>();
        foreach (StringName scope in _runtime_save_dirty_scopes)
            dirtyScopes.Add(scope);
        var postDecodeReasons = new List<object>();
        foreach (StringName reason in _post_decode_save_reasons)
            postDecodeReasons.Add(reason);
        return RuntimePlainPayload.ProjectDictionaryLease(
            new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["has_pending_save"] = HasPendingSave(),
            ["dirty_scopes"] = dirtyScopes,
            ["battle_save_locked"] = _battle_save_lock_enabled,
            ["last_error"] = _last_save_error,
            ["last_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = postDecodeReasons,
        },
            "game-session-save-status",
            LifetimeDomain.Request,
            "GameSession.GetSaveStatusLease"
        );
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

    public GameRoot GetGameRootTyped() => EnsureGameRoot();

    public GameContentCatalog GetContentCatalogTyped() =>
        EnsureGameRoot().GetContentCatalogTyped();

    public ProgressionIdentityCatalogData GetProgressionIdentityCatalogTyped()
    {
        return RequireContentSnapshot().IdentityCatalog;
    }

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        RequireContentSnapshot().Skills;

    public IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefsTyped() =>
        RequireContentSnapshot().Traits;

    public int GetEquipmentAbilityContentRevision() =>
        checked((int)RequireContentSnapshot().Epoch);

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetEquipmentAbilityPackDefinitionsTyped()
    {
        return RequireContentSnapshot().EquipmentAbilityPacks;
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindingDefinitionsTyped()
    {
        return RequireContentSnapshot().EquipmentAbilityBindings;
    }

    internal IBattleSpecialProfileView GetBattleSpecialProfileView() =>
        RequireContentSnapshot().BattleSpecialProfiles;

    public IReadOnlyDictionary<StringName, ProfessionDefinition> GetProfessionDefsTyped() =>
        RequireContentSnapshot().Professions;

    public IReadOnlyDictionary<StringName, AchievementDefinition> GetAchievementDefsTyped() =>
        RequireContentSnapshot().Achievements;

    public QuestDefinition GetQuestDef(StringName quest_id)
    {
        if (quest_id == "")
            return null;
        return RequireContentSnapshot().Quests.TryGetValue(
            quest_id,
            out QuestDefinition questDefinition
        )
            ? questDefinition
            : null;
    }

    public IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped() =>
        RequireContentSnapshot().Quests;

    public IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> GetContingencySetupTemplatesTyped()
    {
        return RequireContentSnapshot().ContingencyTemplates;
    }

    public IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileDefinitionsTyped()
    {
        return RequireContentSnapshot().BarrierProfiles;
    }

    public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped()
    {
        return RequireContentSnapshot().Items;
    }

    public IReadOnlyDictionary<StringName, RecipeDefinition> GetRecipeDefsTyped()
    {
        return RequireContentSnapshot().Recipes;
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplateDefinitions()
    {
        return RequireContentSnapshot().EnemyTemplates;
    }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> GetEnemyAiBrainDefinitions()
    {
        return RequireContentSnapshot().EnemyBrains;
    }

    internal IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> GetEncounterRosterDefinitions()
    {
        return RequireContentSnapshot().EncounterRosters;
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
        foreach (Dictionary<string, object> saveMeta in LoadSaveIndexEntriesPlain())
        {
            if (
                !string.Equals(
                    ReadPlainString(saveMeta, "generation_config_path"),
                    generation_config_path,
                    StringComparison.Ordinal
                )
            )
                continue;
            attemptedCandidate = true;
            string candidateSaveId = ReadPlainString(saveMeta, "save_id");
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
        WorldGenerationDefinition generation_definition
    )
    {
        if (generation_definition == null)
            return (int)Error.InvalidParameter;

        var gridSystem = new WorldMapGridSystem();
        gridSystem.Setup(
            generation_definition.WorldSizeInChunks,
            generation_definition.ChunkSize
        );

        var spawnSystem = new WorldMapSpawnSystem();
        WorldMapSpawnSystem.WorldBuildData worldBuild = spawnSystem.BuildWorldTyped(
            generation_definition,
            gridSystem
        );
        GDictionary worldData = WorldMapSpawnProjection.Project(worldBuild);

        _generation_config_path = ContentPathCanonicalizer.Canonicalize(generation_config_path);
        _generation_definition = generation_definition;
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
        string displayName = ReadPlainString(
            _activeSaveMeta,
            "display_name",
            _active_save_id
        );
        ReplaceActiveSaveMetaPlain(BuildSaveMetaPlain(
            _active_save_id,
            displayName,
            _generation_config_path,
            new StringName(ReadPlainString(_activeSaveMeta, "world_preset_id")),
            ReadPlainString(_activeSaveMeta, "world_preset_name"),
            _generation_definition != null
                ? _generation_definition.GetWorldSizeCells()
                : Vector2I.Zero,
            ReadPlainInt(_activeSaveMeta, "created_at_unix_time", now),
            now
        ));

        using GodotProjectionLease<GDictionary> payload = BuildSavePayloadLease(now);
        int payloadWriteError = WriteSavePayloadAtomically(_active_save_path, payload);
        if (payloadWriteError != (int)Error.Ok)
            return payloadWriteError;

        int indexError = WriteSaveIndexPlain(
            UpsertSaveMetaPlain(LoadSaveIndexEntriesPlain(), _activeSaveMeta)
        );
        if (indexError != (int)Error.Ok)
            return indexError;

        _battle_save_dirty = false;
        return (int)Error.Ok;
    }

    private int LoadCurrentPayload(
        IReadOnlyDictionary<string, object> payload,
        string generation_config_path,
        WorldGenerationDefinition generation_definition,
        IReadOnlyDictionary<string, object> save_meta
    )
    {
        if (
            !_save_serializer.TryDecodePayload(
                payload,
                generation_config_path,
                save_meta,
                out SaveDecodeResult decodeResult
            )
        )
            return decodeResult.Error;

        PartyState decodedPartyState = decodeResult.PartyState;
        int identityError = ValidateDecodedPartyIdentityForSave(
            decodedPartyState,
            decodeResult.ActiveSaveId,
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
        _active_save_id = decodeResult.ActiveSaveId;
        _active_save_path = BuildSaveFilePath(_active_save_id);
        ReplaceActiveSaveMetaPlain(decodeResult.ActiveSaveMeta);
        _generation_config_path = decodeResult.GenerationConfigPath;
        _generation_definition = generation_definition;
        ReplaceWorldDataPlain(decodeResult.WorldData);
        _player_coord = decodeResult.PlayerCoord;
        _player_faction_id = decodeResult.PlayerFactionId;
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

    private GodotProjectionLease<GDictionary> BuildSavePayloadLease(
        int saved_at_unix_time
    )
    {
        return _save_serializer.BuildSavePayloadLease(
            _active_save_id,
            _generation_config_path,
            _activeSaveMeta,
            _worldData,
            _player_coord,
            _player_faction_id,
            _party_state,
            saved_at_unix_time
        );
    }

    private Dictionary<string, object> BuildSaveMetaPlain(
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
        return _save_serializer.BuildSaveMetaPlain(
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
        HashSet<string> existingSaveIds = new(StringComparer.Ordinal);
        foreach (Dictionary<string, object> entry in LoadSaveIndexEntriesPlain())
            existingSaveIds.Add(ReadPlainString(entry, "save_id"));

        using NativeLeaseScope datetimeScope = new(
            "save-id-datetime",
            LifetimeDomain.Request
        );
        GDictionary datetime = datetimeScope.Own(
            Time.GetDatetimeDictFromUnixTime(timestamp),
            "Time.GetDatetimeDictFromUnixTime"
        );
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
                !existingSaveIds.Contains(saveId)
                && !FileAccess.FileExists(BuildSaveFilePath(saveId))
            )
            {
                return saveId;
            }
        }
        return "";
    }

    private WorldGenerationDefinition ResolveBoundGenerationDefinition(
        string generationConfigPath
    )
    {
        string canonicalPath;
        try
        {
            canonicalPath = ContentPathCanonicalizer.Canonicalize(generationConfigPath);
        }
        catch (ArgumentException exception)
        {
            PushSessionError(
                "session.config.definition_unavailable",
                $"GameSession rejected generation config path {generationConfigPath}.",
                Json.Stringify(
                    new GDictionary
                    {
                        ["generation_config_path"] = generationConfigPath ?? "",
                        ["exception_type"] = exception.GetType().Name,
                    }
                )
            );
            return null;
        }
        ContentSnapshot snapshot = _contentSnapshot;
        if (
            snapshot == null
            || !snapshot.WorldGenerations.TryGetValue(
                canonicalPath,
                out WorldGenerationDefinition definition
            )
            || definition == null
        )
        {
            PushSessionError(
                "session.config.definition_unavailable",
                $"GameSession snapshot has no world generation definition for {canonicalPath}.",
                Json.Stringify(new GDictionary { ["generation_config_path"] = canonicalPath })
            );
            return null;
        }
        BindGenerationDefinition(canonicalPath, definition);
        return definition;
    }

    public int ReadSavePayload(
        string save_path,
        out Dictionary<string, object> payload,
        bool emit_errors = true
    )
    {
        return EnsureSaveRepository().ReadSavePayload(save_path, out payload, emit_errors);
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

    internal Dictionary<string, object> CaptureRuntimeStateSnapshotPlain()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["active_save_id"] = _active_save_id,
            ["active_save_path"] = _active_save_path,
            ["active_save_meta"] = RuntimePlainPayload.CloneDictionary(_activeSaveMeta),
            ["generation_config_path"] = _generation_config_path,
            ["world_data"] = RuntimePlainPayload.CloneDictionary(_worldData),
            ["player_coord"] = _player_coord,
            ["player_faction_id"] = _player_faction_id,
            ["party_state"] =
                _party_state?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["has_active_world"] = _has_active_world,
            ["battle_save_lock_enabled"] = _battle_save_lock_enabled,
            ["battle_save_dirty"] = _battle_save_dirty,
            ["runtime_save_dirty"] = _runtime_save_dirty,
            ["runtime_save_dirty_scopes"] = BuildStringListPlain(_runtime_save_dirty_scopes),
            ["last_save_error"] = _last_save_error,
            ["last_save_error_reason"] = _last_save_error_reason,
            ["post_decode_save_pending"] = _post_decode_save_pending,
            ["post_decode_save_reasons"] = BuildStringListPlain(_post_decode_save_reasons),
        };
    }

    internal GodotProjectionLease<GDictionary> CaptureRuntimeStateLease() =>
        RuntimePlainPayload.ProjectDictionaryLease(
            CaptureRuntimeStateSnapshotPlain(),
            "GameSession.CaptureRuntimeState",
            LifetimeDomain.Request,
            "GameSession.CaptureRuntimeState"
        );

    private static List<object> BuildStringListPlain(IEnumerable<StringName> values)
    {
        var result = new List<object>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    public void RestoreRuntimeState(GDictionary state)
    {
        RestoreRuntimeState(state, _generation_definition);
    }

    private void RestoreRuntimeState(
        GDictionary state,
        WorldGenerationDefinition generationDefinition
    )
    {
        PartyState previousPartyState = _party_state;
        _active_save_id = GetString(state, "active_save_id");
        _active_save_path = GetString(state, "active_save_path");
        ReplaceActiveSaveMetaPayload(GetDictionary(state, "active_save_meta").Duplicate(true));
        string restoredGenerationPath = GetString(state, "generation_config_path");
        _generation_config_path = restoredGenerationPath;
        _generation_definition = DefinitionMatchesPath(
            generationDefinition,
            restoredGenerationPath
        )
            ? generationDefinition
            : null;
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

    private static bool DefinitionMatchesPath(
        WorldGenerationDefinition definition,
        string generationConfigPath
    )
    {
        if (definition == null || string.IsNullOrWhiteSpace(generationConfigPath))
            return false;
        try
        {
            return string.Equals(
                ContentPathCanonicalizer.Canonicalize(definition.CanonicalPath),
                ContentPathCanonicalizer.Canonicalize(generationConfigPath),
                StringComparison.Ordinal
            );
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void ResetRuntimeState(bool dispose_current_party_state = true)
    {
        PartyState previousPartyState = _party_state;
        _active_save_id = "";
        _active_save_path = "";
        ClearActiveSaveMetaPayload();
        _generation_config_path = "";
        _generation_definition = null;
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

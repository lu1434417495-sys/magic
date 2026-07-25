using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_save_projection_lease_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";
    private const string SaveIndexPath = "user://saves/index.dat";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            _test.Eq(
                (Error)gameSession.ClearPersistedGame(),
                Error.Ok,
                "save projection lease 回归前应能清理旧存档。"
            );
            _test.Eq(
                (Error)gameSession.CreateNewSave(TestWorldConfig),
                Error.Ok,
                "save projection lease 回归应能创建初始存档。"
            );

            LifecycleAuditSnapshot baseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            AssertStrictPlainPayloadContracts(baseline);
            AssertSuccessfulCommitReturnsToBaseline(gameSession, baseline);
            AssertInjectedFailureReturnsToBaseline(gameSession, baseline);
            AssertExceptionAndClosedAccessReturnToBaseline(gameSession, baseline);
            AssertIndexLeaseFiltersInvalidEntries(gameSession, baseline);
            AssertCurrentSaveShapes(gameSession, baseline);
        }
        finally
        {
            gameSession.fail_payload_write = false;
            gameSession.ClearPersistedGame();
            gameSession.Dispose();
        }

        RequestTestExit(_test.Finish("Save projection lease regression"));
    }

    private void AssertSuccessfulCommitReturnsToBaseline(
        GameSession gameSession,
        LifecycleAuditSnapshot baseline
    )
    {
        _test.Eq(
            (Error)gameSession.SetPlayerCoord(gameSession.GetPlayerCoord() + Vector2I.Right),
            Error.Ok,
            "成功提交前应能更新玩家坐标。"
        );
        _test.Eq(
            (Error)gameSession.CommitRuntimeState("save_projection_success"),
            Error.Ok,
            "成功 save transaction 应完成写入。"
        );
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "成功写入"
        );
    }

    private void AssertInjectedFailureReturnsToBaseline(
        GameSession gameSession,
        LifecycleAuditSnapshot baseline
    )
    {
        _test.Eq(
            (Error)gameSession.SetPlayerCoord(gameSession.GetPlayerCoord() + Vector2I.Down),
            Error.Ok,
            "失败提交前应能 stage 玩家坐标。"
        );
        gameSession.fail_payload_write = true;
        _test.Eq(
            (Error)gameSession.CommitRuntimeState("save_projection_failure"),
            Error.CantCreate,
            "注入 payload 写失败应返回 CantCreate。"
        );
        gameSession.fail_payload_write = false;
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "注入写失败"
        );
    }

    private void AssertExceptionAndClosedAccessReturnToBaseline(
        GameSession gameSession,
        LifecycleAuditSnapshot baseline
    )
    {
        try
        {
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildPayloadLease(gameSession);
            LifecycleAuditSnapshot active =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "save payload 构建期间应只有一个 request projection lease。"
            );
            _test.Eq(
                active.ActiveOwnerCount - baseline.ActiveOwnerCount,
                CountContainers(payloadLease.Value),
                "save payload lease 应精确拥有 root 与每个 nested container。"
            );
            _test.Eq(
                active.ActiveScopeCount,
                baseline.ActiveScopeCount,
                "save payload projection 不应额外登记 native scope。"
            );
            _test.Eq(
                active.ActiveContentBorrowerCount,
                baseline.ActiveContentBorrowerCount,
                "save payload projection 不应登记 content borrower。"
            );
            throw new InvalidOperationException("save projection exception probe");
        }
        catch (InvalidOperationException exception)
        {
            _test.Eq(
                exception.Message,
                "save projection exception probe",
                "exception probe 应进入预期 finally/dispose 路径。"
            );
        }
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "异常退出"
        );

        GodotProjectionLease<GDictionary> closedLease = BuildPayloadLease(gameSession);
        closedLease.Dispose();
        _test.True(
            Throws<ObjectDisposedException>(() => _ = closedLease.Value),
            "关闭后的 save payload lease.Value 应抛 ObjectDisposedException。"
        );
        int beforeReadError = gameSession.ReadSavePayload(
            gameSession.GetActiveSavePath(),
            out Dictionary<string, object> beforePayload
        );
        _test.Eq((Error)beforeReadError, Error.Ok, "closed lease 写探针前 target 应可读。");
        _test.True(
            Throws<ObjectDisposedException>(
                () =>
                    FileIOCoordinator.WriteCompressedVariantAtomically(
                        gameSession.GetActiveSavePath(),
                        closedLease,
                        (int)FileAccess.CompressionMode.Zstd,
                        "test.save.closed_lease",
                        "closed lease save"
                    )
            ),
            "closed lease 写入应保留 ObjectDisposedException。"
        );
        _test.False(
            FileAccess.FileExists($"{gameSession.GetActiveSavePath()}.tmp"),
            "closed lease 写入异常后不得留下半写 tmp 文件。"
        );
        int afterReadError = gameSession.ReadSavePayload(
            gameSession.GetActiveSavePath(),
            out Dictionary<string, object> afterPayload
        );
        _test.Eq((Error)afterReadError, Error.Ok, "closed lease 写入异常后 target 应保持可读。");
        _test.Eq(
            PlainString(afterPayload, "save_id"),
            PlainString(beforePayload, "save_id"),
            "closed lease 写入异常不得替换正式 target。"
        );
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "关闭后访问"
        );
    }

    private void AssertStrictPlainPayloadContracts(LifecycleAuditSnapshot baseline)
    {
        var unsupportedPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["nested"] = new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["value"] = 1,
                },
            },
            ["unsupported"] = new object(),
        };
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                {
                    using GodotProjectionLease<GDictionary> rejected =
                        RuntimePlainPayload.ProjectDictionaryLease(
                            unsupportedPayload,
                            "save-unsupported-value",
                            LifetimeDomain.Request,
                            "run_save_projection_lease_regression.unsupported"
                        );
                }
            ),
            "plain save projection 遇到未知类型时应抛错，而不是字符串化。"
        );
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "未知类型投影失败"
        );

        using (var invalidTopLevel = new GDictionary { [1] = "invalid" })
        using (Variant invalidTopLevelVariant = Variant.From(invalidTopLevel))
        {
            _test.False(
                RuntimePlainPayload.TryRestoreSaveVariantDictionary(
                    invalidTopLevelVariant,
                    "invalid-top-level-key",
                    out _
                ),
                "顶层非字符串 key 必须使 save payload 还原失败。"
            );
        }

        using (var invalidNested = new GDictionary { [""] = "invalid" })
        using (var invalidNestedRoot = new GDictionary { ["nested"] = invalidNested })
        using (Variant invalidNestedVariant = Variant.From(invalidNestedRoot))
        {
            _test.False(
                RuntimePlainPayload.TryRestoreSaveVariantDictionary(
                    invalidNestedVariant,
                    "invalid-nested-key",
                    out _
                ),
                "嵌套空 key 必须使 save payload 还原失败。"
            );
        }

        var mutableChild = new List<object> { "before" };
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["child"] = mutableChild,
            }
        );
        mutableChild[0] = "after";
        _test.Eq(
            ((IReadOnlyList<object>)cloned["child"])[0]?.ToString(),
            "before",
            "plain clone 不得保留 mutable list alias。"
        );

        using (var nativeArray = new GArray())
        {
            _test.True(
                Throws<InvalidOperationException>(
                    () =>
                        RuntimePlainPayload.CloneDictionary(
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["native"] = nativeArray,
                            }
                        )
                ),
                "plain clone 必须拒绝 Godot collection wrapper。"
            );
        }
    }

    private void AssertIndexLeaseFiltersInvalidEntries(
        GameSession gameSession,
        LifecycleAuditSnapshot baseline
    )
    {
        Dictionary<string, object> valid = gameSession.CaptureActiveSaveMetaPlain();
        Dictionary<string, object> invalid = RuntimePlainPayload.CloneDictionary(valid);
        invalid.Remove("display_name");
        var entries = new List<Dictionary<string, object>> { valid, invalid };

        using (
            GodotProjectionLease<GDictionary> indexLease =
                gameSession._save_serializer.BuildSaveIndexPayloadLease(entries)
        )
        {
            LifecycleAuditSnapshot active =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "save index projection 应打开一个 request lease。"
            );
            _test.Eq(
                active.ActiveOwnerCount,
                baseline.ActiveOwnerCount + 3,
                "save index projection 应拥有 root、saves Array 与唯一有效 meta Dictionary。"
            );

            Dictionary<string, object> projected = RuntimePlainPayload.RestoreSaveDictionary(
                indexLease.Value,
                "save-index-lease-regression"
            );
            _test.True(
                HasExactKeys(projected, "version", "saves"),
                "save index 顶层 key shape 应保持 version+saves。"
            );
            _test.Eq(
                PlainInt(projected, "version", -1),
                SaveSchemaVersions.SaveIndexVersion,
                "save index lease version 应保持当前版本。"
            );
            IReadOnlyList<object> projectedEntries = PlainList(projected, "saves");
            _test.Eq(projectedEntries.Count, 1, "save index lease 应过滤缺字段的 invalid entry。");
            IReadOnlyDictionary<string, object> projectedMeta =
                projectedEntries.Count > 0
                && projectedEntries[0] is IReadOnlyDictionary<string, object> dictionary
                    ? dictionary
                    : new Dictionary<string, object>(StringComparer.Ordinal);
            _test.True(
                HasExactKeys(
                    projectedMeta,
                    "save_id",
                    "display_name",
                    "world_preset_id",
                    "world_preset_name",
                    "generation_config_path",
                    "world_size_cells",
                    "created_at_unix_time",
                    "updated_at_unix_time"
                ),
                "有效 save meta 的八字段 shape 应保持不变。"
            );
        }

        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "index projection"
        );
    }

    private void AssertCurrentSaveShapes(
        GameSession gameSession,
        LifecycleAuditSnapshot baseline
    )
    {
        int readError = gameSession.ReadSavePayload(
            gameSession.GetActiveSavePath(),
            out Dictionary<string, object> payload
        );
        _test.Eq((Error)readError, Error.Ok, "当前 save payload 应能读回 plain graph。");
        _test.True(
            HasExactKeys(
                payload,
                "version",
                "save_id",
                "generation_config_path",
                "world_state",
                "party_state",
                "meta",
                "save_slot_meta"
            ),
            "save v16 顶层 key shape 应保持不变。"
        );
        _test.Eq(PlainInt(payload, "version", -1), 17, "save version 应保持 17。");
        IReadOnlyDictionary<string, object> worldState = PlainDictionary(
            payload,
            "world_state"
        );
        _test.True(
            HasExactKeys(worldState, "world_data", "player_coord", "player_faction_id"),
            "world_state key shape 应保持不变。"
        );
        IReadOnlyDictionary<string, object> partyState = PlainDictionary(
            payload,
            "party_state"
        );
        _test.Eq(PlainInt(partyState, "version", -1), 8, "PartyState version 应保持 8。");

        {
            using NativeLeaseScope indexFileScope = new(
                "save-projection-index-read",
                LifetimeDomain.Request
            );
            FileAccess openedIndexFile = FileAccess.OpenCompressed(
                SaveIndexPath,
                FileAccess.ModeFlags.Read,
                FileAccess.CompressionMode.Zstd
            );
            _test.True(openedIndexFile != null, "save index 应能按 Zstd 打开。");
            if (openedIndexFile != null)
            {
                FileAccess indexFile = indexFileScope.Own(
                    openedIndexFile,
                    $"open:{SaveIndexPath}"
                );
                try
                {
                    _test.True(
                        gameSession._save_serializer.TryReadSaveIndexPayloadPlain(
                            indexFile,
                            out Dictionary<string, object> indexPayload
                        ),
                        "save index 应能立即还原为 plain graph。"
                    );
                    _test.Eq(
                        PlainInt(indexPayload, "version", -1),
                        SaveSchemaVersions.SaveIndexVersion,
                        "save index version 应保持当前版本。"
                    );
                    _test.True(
                        indexPayload.TryGetValue("saves", out object savesValue)
                            && savesValue is IReadOnlyList<object>,
                        "save index saves 应保持 Array shape。"
                    );
                    indexFile.Close();
                }
                finally
                {
                    indexFile.Close();
                }
            }
        }

        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "shape/read transaction"
        );
    }

    private static GodotProjectionLease<GDictionary> BuildPayloadLease(
        GameSession gameSession
    )
    {
        return gameSession._save_serializer.BuildSavePayloadLease(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.CaptureActiveSaveMetaPlain(),
            gameSession.CaptureWorldDataPlain(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            gameSession.GetPartyState(),
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private void AssertAuditBaseline(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label} owner 应回到 baseline。");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label} lease 应回到 baseline。");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label} scope 应回到 baseline。");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label} borrower 应回到 baseline。"
        );
    }

    private static bool HasExactKeys(
        IReadOnlyDictionary<string, object> values,
        params string[] keys
    )
    {
        if (values == null || values.Count != keys.Length)
            return false;
        foreach (string key in keys)
        {
            if (!values.ContainsKey(key))
                return false;
        }
        return true;
    }

    private static IReadOnlyDictionary<string, object> PlainDictionary(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is IReadOnlyDictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static int PlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int fallback
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return fallback;
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue =>
                (int)longValue,
            _ => fallback,
        };
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string fallback = ""
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is string text
            ? text
            : fallback ?? "";
    }

    private static IReadOnlyList<object> PlainList(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> list
            ? list
            : Array.Empty<object>();
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static int CountContainers(GDictionary dictionary)
    {
        int count = 1;
        foreach (Variant key in dictionary.Keys)
        {
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }

    private static int CountContainers(GArray array)
    {
        int count = 1;
        for (int index = 0; index < array.Count; index++)
        {
            Variant value = array[index];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }
}

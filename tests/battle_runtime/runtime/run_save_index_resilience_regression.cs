using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

public partial class run_save_index_resilience_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private const string SaveDirectory = "user://saves";
    private const string SaveIndexPath = "user://saves/index.dat";
    private const int SaveIndexVersion = 3;
    private const FileAccess.CompressionMode SaveCompressionMode = FileAccess.CompressionMode.Zstd;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCorruptSaveIndexRecoversCleanly();
        TestSaveIndexSchemaRejectsOldEntryShapes();
        Quit(_test.Finish("Save index resilience regression"));
    }

    private void TestCorruptSaveIndexRecoversCleanly()
    {
        var gameSession = new GameSession();
        _test.Eq((Error)gameSession.ClearPersistedGame(), Error.Ok, "测试前应能清理旧存档目录。");
        _test.Eq(
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory)),
            Error.Ok,
            "测试前应能创建 save 目录。"
        );

        using (FileAccess corruptFile = FileAccess.Open(SaveIndexPath, FileAccess.ModeFlags.Write))
        {
            _test.True(corruptFile != null, "应能写入损坏的 save index 夹具。");
            corruptFile?.StoreBuffer(new byte[] { 0xCC, 0x80, 0x01, 0x02 });
        }

        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "损坏的 save index 不应阻止创建新存档。");
        GDictionaryArray saveSlots = gameSession.ListSaveSlots();
        _test.True(saveSlots.Count > 0, "损坏索引恢复后，应能重新列出至少一个存档槽。");
        if (saveSlots.Count > 0)
            _test.Eq(DictString(saveSlots[0], "save_id"), gameSession.GetActiveSaveId(), "恢复后的索引应包含当前新创建的存档。");

        // 清理前必须释放 index 文件句柄，Windows 上打开中的文件会阻止目录删除。
        using (
            FileAccess restoredIndexFile = FileAccess.OpenCompressed(
                SaveIndexPath,
                FileAccess.ModeFlags.Read,
                SaveCompressionMode
            )
        )
        {
            _test.True(restoredIndexFile != null, "恢复后应能按压缩 Variant 格式重新打开 save index。");
            if (restoredIndexFile != null)
                _test.True(restoredIndexFile.GetVar(false).VariantType == Variant.Type.Dictionary, "恢复后的 save index 应写成 Godot Variant 二进制 Dictionary。");
        }

        _test.Eq((Error)gameSession.ClearPersistedGame(), Error.Ok, "测试结束后应能清理 save 目录。");
        gameSession.Dispose();
    }

    private void TestSaveIndexSchemaRejectsOldEntryShapes()
    {
        var gameSession = new GameSession();
        _test.Eq((Error)gameSession.ClearPersistedGame(), Error.Ok, "save index schema 回归前应能清理旧存档目录。");
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "save index schema 回归应能创建测试存档。");
        if (createError != Error.Ok)
        {
            gameSession.Dispose();
            return;
        }

        SaveSerializer serializer = gameSession._save_serializer;
        var metaEntries = new GDictionaryArray { gameSession.GetActiveSaveMeta() };
        GDictionaryArray serializedEntries = serializer.SerializeSaveIndexEntries(metaEntries);
        _test.Eq(serializedEntries.Count, 1, "当前 save meta 应能序列化为一个 index entry。");
        if (serializedEntries.Count == 0)
        {
            Cleanup(gameSession);
            return;
        }

        GDictionary validEntry = serializedEntries[0];
        _test.True(serializer.DeserializeSaveIndexEntry(validEntry).Count > 0, "完整 save index entry 应能反序列化。");
        GDictionary stringTimestampEntry = (GDictionary)validEntry.Duplicate(true);
        stringTimestampEntry["updated_at_unix_time"] = validEntry["updated_at_unix_time"].AsString();
        _test.True(serializer.DeserializeSaveIndexEntry(stringTimestampEntry).Count == 0, "save index entry 不应接受字符串时间戳。");

        using (FileAccess oldShapeFile = FileAccess.Open(SaveIndexPath, FileAccess.ModeFlags.Write))
        {
            _test.True(oldShapeFile != null, "应能写入旧 top-level Array save index 夹具。");
            oldShapeFile?.StoreString(Json.Stringify(serializedEntries));
        }
        _test.True(gameSession.ListSaveSlots().Count > 0, "旧 top-level Array index 被拒绝后，应能从正式 save payload 重建列表。");
        AssertSaveIndexFileUsesCurrentSchema("旧 top-level Array index 被拒绝后");

        using (FileAccess stringVersionFile = FileAccess.OpenCompressed(
                   SaveIndexPath,
                   FileAccess.ModeFlags.Write,
                   SaveCompressionMode
               ))
        {
            _test.True(stringVersionFile != null, "应能写入字符串 version 的 save index 夹具。");
            stringVersionFile?.StoreVar(
                serializer.MinimizeSavePayloadStrings(
                    new GDictionary { ["version"] = SaveIndexVersion.ToString(), ["saves"] = serializedEntries }
                ),
                false
            );
        }
        _test.True(gameSession.ListSaveSlots().Count > 0, "字符串 version index 被拒绝后，应能从正式 save payload 重建列表。");
        AssertSaveIndexFileUsesCurrentSchema("字符串 version index 被拒绝后");

        Cleanup(gameSession);
    }

    private void AssertSaveIndexFileUsesCurrentSchema(string label)
    {
        using FileAccess file = FileAccess.OpenCompressed(
            SaveIndexPath,
            FileAccess.ModeFlags.Read,
            SaveCompressionMode
        );
        _test.True(file != null, $"{label} 应能重新打开 current schema save index。");
        if (file == null)
            return;
        Variant raw = file.GetVar(false);
        _test.True(raw.VariantType == Variant.Type.Dictionary, $"{label} save index 应是 Dictionary。");
        GDictionary index = raw.AsGodotDictionary();
        _test.Eq(DictInt(index, "version", -1), SaveIndexVersion, $"{label} save index version 应保持当前版本。");
        _test.True(index.ContainsKey("saves"), $"{label} save index 应包含 saves。");
    }

    private static void Cleanup(GameSession gameSession)
    {
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : "";
    }
}

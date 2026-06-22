using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_save_payload_string_minimization_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private const string SaveDirectory = "user://saves";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSavePayloadMinimizesIdentityStrings();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Save payload string minimization regression"));
    }

    private void TestSavePayloadMinimizesIdentityStrings()
    {
        GameSession gameSession = new();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "字符串最小化回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
            {
                return;
            }

            SaveSerializer serializer = gameSession._save_serializer;
            _test.True(serializer != null, "字符串最小化回归需要已初始化的 SaveSerializer。");
            if (serializer == null)
            {
                return;
            }

            GDictionary payload = serializer.BuildSavePayload(
                gameSession.GetActiveSaveId(),
                gameSession.GetGenerationConfigPath(),
                gameSession.GetActiveSaveMeta(),
                gameSession.GetWorldData(),
                gameSession.GetPlayerCoord(),
                gameSession.GetPlayerFactionId(),
                gameSession.GetPartyState(),
                (int)Time.GetUnixTimeFromSystem()
            );
            try
            {
                using Variant payloadVariant = Variant.From(payload);
                AssertNoStringOptions(payloadVariant, "payload", "正式 save payload 不应保留 TYPE_STRING key 或 value。");
                AssertBinaryDictionaryFile(
                    $"{SaveDirectory}/{gameSession.GetActiveSaveId()}.dat",
                    "正式 slot save 文件"
                );

                AssertType(DictGet(payload, "save_id"), Variant.Type.StringName, "save_id 在正式 payload 中应保存为 StringName。");
                AssertType(DictGet(payload, "generation_config_path"), Variant.Type.StringName, "generation_config_path 在正式 payload 中应保存为 StringName。");
                GDictionary saveMeta = DictDictionary(payload, "save_slot_meta");
                AssertType(DictGet(saveMeta, "display_name"), Variant.Type.StringName, "save_slot_meta.display_name 在正式 payload 中应保存为 StringName。");

                GDictionary worldState = DictDictionary(payload, "world_state");
                AssertType(DictGet(worldState, "player_faction_id"), Variant.Type.StringName, "world_state.player_faction_id 应保存为 StringName。");
                GDictionary worldData = DictDictionary(worldState, "world_data");
                AssertType(DictGet(worldData, "active_submap_id"), Variant.Type.StringName, "world_data.active_submap_id 应保存为 StringName。");

                GArray settlements = DictArray(worldData, "settlements");
                _test.True(settlements.Count > 0, "测试世界应至少生成一个据点用于检查 world_data 字符串最小化。");
                if (settlements.Count > 0)
                {
                    GDictionary settlement = DictAt(settlements, 0);
                    AssertType(DictGet(settlement, "settlement_id"), Variant.Type.StringName, "settlement_id 应保存为 StringName。");
                    AssertType(DictGet(settlement, "faction_id"), Variant.Type.StringName, "settlement faction_id 应保存为 StringName。");
                    AssertType(DictGet(settlement, "display_name"), Variant.Type.StringName, "settlement display_name 在正式 payload 中应保存为 StringName。");

                    GArray facilities = DictArray(settlement, "facilities");
                    if (facilities.Count > 0)
                    {
                        GDictionary facility = DictAt(facilities, 0);
                        AssertType(DictGet(facility, "facility_id"), Variant.Type.StringName, "facility_id 应保存为 StringName。");
                        AssertType(DictGet(facility, "slot_tag"), Variant.Type.StringName, "facility slot_tag 应保存为 StringName。");
                        AssertType(DictGet(facility, "display_name"), Variant.Type.StringName, "facility display_name 在正式 payload 中应保存为 StringName。");
                    }

                    GArray services = DictArray(settlement, "available_services");
                    if (services.Count > 0)
                    {
                        GDictionary service = DictAt(services, 0);
                        AssertType(DictGet(service, "action_id"), Variant.Type.StringName, "service action_id 应保存为 StringName。");
                        AssertType(DictGet(service, "interaction_script_id"), Variant.Type.StringName, "service interaction_script_id 应保存为 StringName。");
                        AssertType(DictGet(service, "service_type"), Variant.Type.StringName, "service_type 在正式 payload 中应保存为 StringName。");
                    }
                }

                GArray encounters = DictArray(worldData, "encounter_anchors");
                _test.True(encounters.Count > 0, "测试世界应至少生成一个 encounter_anchor 用于检查 ID 字段。");
                if (encounters.Count > 0)
                {
                    GDictionary encounter = DictAt(encounters, 0);
                    AssertType(DictGet(encounter, "entity_id"), Variant.Type.StringName, "encounter entity_id 应保存为 StringName。");
                    AssertType(DictGet(encounter, "encounter_kind"), Variant.Type.StringName, "encounter_kind 应保存为 StringName。");
                    AssertType(DictGet(encounter, "display_name"), Variant.Type.StringName, "encounter display_name 在正式 payload 中应保存为 StringName。");
                }

                GDictionary partyPayload = DictDictionary(payload, "party_state");
                AssertType(DictGet(partyPayload, "leader_member_id"), Variant.Type.StringName, "party leader_member_id 应保存为 StringName。");
                AssertArrayItemType(DictGet(partyPayload, "active_member_ids"), Variant.Type.StringName, "active_member_ids 元素应保存为 StringName。");
                GDictionary memberStates = DictDictionary(partyPayload, "member_states");
                AssertDictionaryKeysType(
                    Variant.From(memberStates),
                    Variant.Type.StringName,
                    "member_states 的成员 ID key 应保存为 StringName。"
                );
                StringName mainMemberId = gameSession.GetPartyState().main_character_member_id;
                GDictionary memberPayload = DictDictionary(memberStates, mainMemberId.ToString());
                _test.True(memberPayload.Count > 0, "应能用 StringName 成员 ID 读取成员 payload。");
                if (memberPayload.Count > 0)
                {
                    AssertType(DictGet(memberPayload, "member_id"), Variant.Type.StringName, "member_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "display_name"), Variant.Type.StringName, "member display_name 在正式 payload 中应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "race_id"), Variant.Type.StringName, "member race_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "subrace_id"), Variant.Type.StringName, "member subrace_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "age_profile_id"), Variant.Type.StringName, "member age_profile_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "natural_age_stage_id"), Variant.Type.StringName, "member natural_age_stage_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "effective_age_stage_id"), Variant.Type.StringName, "member effective_age_stage_id 应保存为 StringName。");
                    AssertType(DictGet(memberPayload, "body_size_category"), Variant.Type.StringName, "member body_size_category 应保存为 StringName。");

                    GDictionary progression = DictDictionary(memberPayload, "progression");
                    AssertType(DictGet(progression, "unit_id"), Variant.Type.StringName, "progression unit_id 应保存为 StringName。");
                    AssertArrayItemType(DictGet(progression, "active_core_skill_ids"), Variant.Type.StringName, "active_core_skill_ids 元素应保存为 StringName。");
                    AssertDictionaryKeysType(DictGet(progression, "skills"), Variant.Type.StringName, "skills 的 skill_id key 应保存为 StringName。");
                    GDictionary skillPayloads = DictDictionary(progression, "skills");
                    foreach (Variant skillPayloadOption in skillPayloads.Values)
                    {
                        try
                        {
                            if (skillPayloadOption.VariantType != Variant.Type.Dictionary)
                            {
                                continue;
                            }
                            GDictionary skillPayload = skillPayloadOption.AsGodotDictionary();
                            GC.SuppressFinalize(skillPayload);
                            AssertType(DictGet(skillPayload, "granted_source_type"), Variant.Type.StringName, "skill granted_source_type 应保存为 StringName。");
                            AssertType(DictGet(skillPayload, "granted_source_id"), Variant.Type.StringName, "skill granted_source_id 应保存为 StringName。");
                            break;
                        }
                        finally
                        {
                            skillPayloadOption.Dispose();
                        }
                    }
                }

                GDictionary decodeResult = serializer.DecodePayload(
                    payload,
                    gameSession.GetGenerationConfigPath(),
                    gameSession.GetGenerationConfig(),
                    gameSession.GetActiveSaveMeta()
                );
                try
                {
                    _test.Eq(DictError(decodeResult, "error", Error.InvalidData), Error.Ok, "StringName 化后的 save payload 应继续能被 SaveSerializer 解码。");
                    AssertType(DictGet(decodeResult, "active_save_id"), Variant.Type.String, "解码后 active_save_id 应恢复为运行时 String。");
                    GDictionary decodedMeta = DictDictionary(decodeResult, "active_save_meta");
                    AssertType(DictGet(decodedMeta, "display_name"), Variant.Type.String, "解码后 save meta display_name 应恢复为运行时 String。");
                }
                finally
                {
                    DisposeOwnedDictionary(decodeResult);
                }

                GDictionary runtimeWorldData = gameSession.GetWorldData();
                using Variant activeSubmapIdValue = DictGet(worldData, "active_submap_id");
                runtimeWorldData["active_submap_id"] = activeSubmapIdValue;
                GDictionary normalizedWorldData = serializer.NormalizeWorldData(runtimeWorldData);
                try
                {
                    _test.True(
                        normalizedWorldData.Count == 0,
                        "Runtime world_data should reject StringName active_submap_id."
                    );
                }
                finally
                {
                    DisposeOwnedDictionary(normalizedWorldData);
                }

                GDictionary runtimeSaveMeta = gameSession.GetActiveSaveMeta();
                using Variant saveDisplayNameValue = DictGet(saveMeta, "display_name");
                runtimeSaveMeta["display_name"] = saveDisplayNameValue;
                GDictionary normalizedSaveMeta = serializer.NormalizeSaveMeta(runtimeSaveMeta);
                try
                {
                    _test.True(
                        normalizedSaveMeta.Count == 0,
                        "Runtime save meta should reject StringName display_name."
                    );
                }
                finally
                {
                    DisposeOwnedDictionary(normalizedSaveMeta);
                }
            }
            finally
            {
                DisposeOwnedDictionary(payload);
            }
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void AssertType(Variant value, Variant.Type expectedType, string message)
    {
        if (value.VariantType != expectedType)
        {
            _test.Fail($"{message} | actual_type={value.VariantType} expected_type={expectedType} value={value}");
        }
        value.Dispose();
    }

    private void AssertArrayItemType(Variant values, Variant.Type expectedType, string message)
    {
        try
        {
            if (values.VariantType != Variant.Type.Array)
            {
                _test.Fail($"{message} | actual container type={values.VariantType}");
                return;
            }
            GArray array = values.AsGodotArray();
            try
            {
                foreach (Variant item in array)
                {
                    try
                    {
                        if (item.VariantType == expectedType)
                        {
                            continue;
                        }
                        _test.Fail($"{message} | bad item type={item.VariantType} value={item}");
                        return;
                    }
                    finally
                    {
                        item.Dispose();
                    }
                }
            }
            finally
            {
                GodotCollectionDisposer.DisposeWrapperOnly(array);
            }
        }
        finally
        {
            values.Dispose();
        }
    }

    private void AssertDictionaryKeysType(Variant values, Variant.Type expectedType, string message)
    {
        try
        {
            if (values.VariantType != Variant.Type.Dictionary)
            {
                _test.Fail($"{message} | actual container type={values.VariantType}");
                return;
            }
            GDictionary dictionary = values.AsGodotDictionary();
            try
            {
                foreach (Variant key in dictionary.Keys)
                {
                    try
                    {
                        if (key.VariantType == expectedType)
                        {
                            continue;
                        }
                        _test.Fail($"{message} | bad key type={key.VariantType} key={key}");
                        return;
                    }
                    finally
                    {
                        key.Dispose();
                    }
                }
            }
            finally
            {
                GodotCollectionDisposer.DisposeWrapperOnly(dictionary);
            }
        }
        finally
        {
            values.Dispose();
        }
    }

    private void AssertNoStringOptions(Variant value, string rootPath, string message)
    {
        List<string> stringPaths = new();
        CollectStringVariantPaths(value, rootPath, stringPaths);
        if (stringPaths.Count == 0)
        {
            return;
        }

        int previewCount = Math.Min(stringPaths.Count, 8);
        string preview = string.Join(", ", stringPaths.GetRange(0, previewCount));
        _test.Fail($"{message} | count={stringPaths.Count} examples={preview}");
    }

    private static void CollectStringVariantPaths(Variant value, string path, List<string> stringPaths)
    {
        switch (value.VariantType)
        {
            case Variant.Type.String:
            case Variant.Type.PackedStringArray:
                stringPaths.Add(path);
                return;
            case Variant.Type.Dictionary:
            {
                GDictionary values = value.AsGodotDictionary();
                try
                {
                    foreach (Variant rawKey in values.Keys)
                    {
                        try
                        {
                            string keyLabel = rawKey.ToString();
                            if (rawKey.VariantType == Variant.Type.String)
                            {
                                stringPaths.Add($"{path}.<key:{keyLabel}>");
                            }
                            using Variant childValue = values[rawKey];
                            CollectStringVariantPaths(childValue, $"{path}.{keyLabel}", stringPaths);
                        }
                        finally
                        {
                            rawKey.Dispose();
                        }
                    }
                }
                finally
                {
                    GodotCollectionDisposer.DisposeWrapperOnly(values);
                }
                return;
            }
            case Variant.Type.Array:
            {
                GArray valuesArray = value.AsGodotArray();
                try
                {
                    for (int index = 0; index < valuesArray.Count; index++)
                    {
                        using Variant childValue = valuesArray[index];
                        CollectStringVariantPaths(childValue, $"{path}[{index}]", stringPaths);
                    }
                }
                finally
                {
                    GodotCollectionDisposer.DisposeWrapperOnly(valuesArray);
                }
                return;
            }
        }
    }

    private void AssertBinaryDictionaryFile(string path, string context)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        _test.True(file != null, $"{context} 应能打开：{path}");
        if (file == null)
        {
            return;
        }

        byte[] rawBytes = file.GetBuffer((long)file.GetLength());
        _test.True(rawBytes.Length > 0, $"{context} 不应为空。");
        _test.False(LooksLikeJsonText(rawBytes), $"{context} 不应是 JSON 文本。");

        using FileAccess compressedFile = FileAccess.OpenCompressed(
            path,
            FileAccess.ModeFlags.Read,
            FileAccess.CompressionMode.Zstd
        );
        _test.True(compressedFile != null, $"{context} 应能以 ZSTD 压缩格式打开。");
        if (compressedFile == null)
        {
            return;
        }
        using Variant payloadOption = compressedFile.GetVar(false);
        _test.True(payloadOption.VariantType == Variant.Type.Dictionary, $"{context} 应能以压缩 Godot Variant Dictionary 读回。");
    }

    private static bool LooksLikeJsonText(byte[] rawBytes)
    {
        foreach (byte byteValue in rawBytes)
        {
            int byteInt = byteValue;
            if (byteInt == 9 || byteInt == 10 || byteInt == 13 || byteInt == 32)
            {
                continue;
            }
            return byteInt == 123 || byteInt == 91;
        }
        return false;
    }

    private static Variant DictGet(GDictionary dictionary, string key)
    {
        if (dictionary == null)
        {
            return default;
        }
        foreach (Variant rawKey in dictionary.Keys)
        {
            try
            {
                if (rawKey.ToString() == key)
                {
                    return dictionary[rawKey];
                }
            }
            finally
            {
                rawKey.Dispose();
            }
        }
        return default;
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        using Variant value = DictGet(dictionary, key);
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return EmptyOwnedDictionary();
        }
        GDictionary result = value.AsGodotDictionary();
        GC.SuppressFinalize(result);
        return result;
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        using Variant value = DictGet(dictionary, key);
        if (value.VariantType != Variant.Type.Array)
        {
            return EmptyOwnedArray();
        }
        GArray result = value.AsGodotArray();
        GC.SuppressFinalize(result);
        return result;
    }

    private static GDictionary DictAt(GArray array, int index)
    {
        using Variant value = array[index];
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return EmptyOwnedDictionary();
        }
        GDictionary result = value.AsGodotDictionary();
        GC.SuppressFinalize(result);
        return result;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        using Variant value = DictGet(dictionary, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Error DictError(GDictionary dictionary, string key, Error fallback)
    {
        return (Error)DictInt(dictionary, key, (int)fallback);
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static GDictionary EmptyOwnedDictionary()
    {
        GDictionary result = new();
        GC.SuppressFinalize(result);
        return result;
    }

    private static GArray EmptyOwnedArray()
    {
        GArray result = new();
        GC.SuppressFinalize(result);
        return result;
    }

    private static void DisposeOwnedDictionary(GDictionary dictionary)
    {
        GodotCollectionDisposer.DisposeOwnedPayloadTree(dictionary);
    }
}

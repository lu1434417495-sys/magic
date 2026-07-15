using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_save_payload_string_minimization_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private const string SaveDirectory = "user://saves";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestSavePayloadMinimizesIdentityStrings();

        RequestTestExit(_test.Finish("Save payload string minimization regression"));
    }

    private void TestSavePayloadMinimizesIdentityStrings()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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

            using GodotProjectionLease<GDictionary> payloadLease =
                serializer.BuildSavePayloadLease(
                gameSession.GetActiveSaveId(),
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                gameSession.CaptureWorldDataPlain(),
                gameSession.GetPlayerCoord(),
                gameSession.GetPlayerFactionId(),
                gameSession.GetPartyState(),
                (int)Time.GetUnixTimeFromSystem()
            );
            GDictionary payload = payloadLease.Value;
            AssertNoStringOptions(Variant.From(payload), "payload", "正式 save payload 不应保留 TYPE_STRING key 或 value。");
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
                GDictionary settlement = settlements[0].AsGodotDictionary();
                AssertType(DictGet(settlement, "settlement_id"), Variant.Type.StringName, "settlement_id 应保存为 StringName。");
                AssertType(DictGet(settlement, "faction_id"), Variant.Type.StringName, "settlement faction_id 应保存为 StringName。");
                AssertType(DictGet(settlement, "display_name"), Variant.Type.StringName, "settlement display_name 在正式 payload 中应保存为 StringName。");

                GArray facilities = DictArray(settlement, "facilities");
                if (facilities.Count > 0)
                {
                    GDictionary facility = facilities[0].AsGodotDictionary();
                    AssertType(DictGet(facility, "facility_id"), Variant.Type.StringName, "facility_id 应保存为 StringName。");
                    AssertType(DictGet(facility, "slot_tag"), Variant.Type.StringName, "facility slot_tag 应保存为 StringName。");
                    AssertType(DictGet(facility, "display_name"), Variant.Type.StringName, "facility display_name 在正式 payload 中应保存为 StringName。");
                }

                GArray services = DictArray(settlement, "available_services");
                if (services.Count > 0)
                {
                    GDictionary service = services[0].AsGodotDictionary();
                    AssertType(DictGet(service, "action_id"), Variant.Type.StringName, "service action_id 应保存为 StringName。");
                    AssertType(DictGet(service, "interaction_script_id"), Variant.Type.StringName, "service interaction_script_id 应保存为 StringName。");
                    AssertType(DictGet(service, "service_type"), Variant.Type.StringName, "service_type 在正式 payload 中应保存为 StringName。");
                }
            }

            GArray encounters = DictArray(worldData, "encounter_anchors");
            _test.True(encounters.Count > 0, "测试世界应至少生成一个 encounter_anchor 用于检查 ID 字段。");
            if (encounters.Count > 0)
            {
                GDictionary encounter = encounters[0].AsGodotDictionary();
                AssertType(DictGet(encounter, "entity_id"), Variant.Type.StringName, "encounter entity_id 应保存为 StringName。");
                AssertType(DictGet(encounter, "encounter_kind"), Variant.Type.StringName, "encounter_kind 应保存为 StringName。");
                AssertType(DictGet(encounter, "display_name"), Variant.Type.StringName, "encounter display_name 在正式 payload 中应保存为 StringName。");
            }

            GDictionary partyPayload = DictDictionary(payload, "party_state");
            AssertType(DictGet(partyPayload, "leader_member_id"), Variant.Type.StringName, "party leader_member_id 应保存为 StringName。");
            AssertArrayItemType(DictGet(partyPayload, "active_member_ids"), Variant.Type.StringName, "active_member_ids 元素应保存为 StringName。");
            GDictionary memberStates = DictDictionary(partyPayload, "member_states");
            AssertDictionaryKeysType(Variant.From(memberStates), Variant.Type.StringName, "member_states 的成员 ID key 应保存为 StringName。");
            StringName mainMemberId = gameSession.GetPartyState().main_character_member_id;
            GDictionary memberPayload = memberStates.ContainsKey(mainMemberId)
                ? memberStates[mainMemberId].AsGodotDictionary()
                : new GDictionary();
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
                    if (skillPayloadOption.VariantType != Variant.Type.Dictionary)
                    {
                        continue;
                    }
                    GDictionary skillPayload = skillPayloadOption.AsGodotDictionary();
                    AssertType(DictGet(skillPayload, "granted_source_type"), Variant.Type.StringName, "skill granted_source_type 应保存为 StringName。");
                    AssertType(DictGet(skillPayload, "granted_source_id"), Variant.Type.StringName, "skill granted_source_id 应保存为 StringName。");
                    break;
                }
            }

            Dictionary<string, object> payloadPlain = RuntimePlainPayload.RestoreSaveDictionary(
                payload,
                "save-string-minimization.payload"
            );
            bool decoded = serializer.TryDecodePayload(
                payloadPlain,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(decoded, "StringName 化后的 save payload 应继续能被 SaveSerializer 解码。");
            _test.Eq((Error)decodeResult.Error, Error.Ok, "成功解码应返回 Ok。");
            _test.Eq(
                decodeResult.ActiveSaveId,
                gameSession.GetActiveSaveId(),
                "解码后 active_save_id 应恢复为运行时 String。"
            );
            _test.True(
                decodeResult.ActiveSaveMeta.TryGetValue("display_name", out object displayName)
                    && displayName is string,
                "解码后 save meta display_name 应恢复为运行时 String。"
            );

            using GodotProjectionLease<GDictionary> runtimeWorldDataLease =
                gameSession.GetWorldDataLease();
            GDictionary runtimeWorldData = runtimeWorldDataLease.Value;
            runtimeWorldData["active_submap_id"] = new StringName("");
            _test.True(
                !serializer.TryNormalizeWorldDataPlain(runtimeWorldData, out _),
                "Runtime world_data should reject StringName active_submap_id."
            );

            Dictionary<string, object> runtimeSaveMeta =
                gameSession.CaptureActiveSaveMetaPlain();
            runtimeSaveMeta["display_name"] = new StringName("bad_runtime_meta");
            _test.True(
                !serializer.TryNormalizeSaveMetaPlain(runtimeSaveMeta, out _),
                "Runtime save meta should reject StringName display_name."
            );
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
    }

    private void AssertArrayItemType(Variant values, Variant.Type expectedType, string message)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            _test.Fail($"{message} | actual container type={values.VariantType}");
            return;
        }
        foreach (Variant item in values.AsGodotArray())
        {
            if (item.VariantType == expectedType)
            {
                continue;
            }
            _test.Fail($"{message} | bad item type={item.VariantType} value={item}");
            return;
        }
    }

    private void AssertDictionaryKeysType(Variant values, Variant.Type expectedType, string message)
    {
        if (values.VariantType != Variant.Type.Dictionary)
        {
            _test.Fail($"{message} | actual container type={values.VariantType}");
            return;
        }
        foreach (Variant key in values.AsGodotDictionary().Keys)
        {
            if (key.VariantType == expectedType)
            {
                continue;
            }
            _test.Fail($"{message} | bad key type={key.VariantType} key={key}");
            return;
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
                foreach (Variant rawKey in values.Keys)
                {
                    string keyLabel = rawKey.ToString();
                    if (rawKey.VariantType == Variant.Type.String)
                    {
                        stringPaths.Add($"{path}.<key:{keyLabel}>");
                    }
                    CollectStringVariantPaths(values[rawKey], $"{path}.{keyLabel}", stringPaths);
                }
                return;
            }
            case Variant.Type.Array:
            {
                GArray valuesArray = value.AsGodotArray();
                for (int index = 0; index < valuesArray.Count; index++)
                {
                    CollectStringVariantPaths(valuesArray[index], $"{path}[{index}]", stringPaths);
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
        Variant payloadOption = compressedFile.GetVar(false);
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
        StringName stringNameKey = new(key);
        if (dictionary.ContainsKey(stringNameKey))
        {
            return dictionary[stringNameKey];
        }
        return dictionary.ContainsKey(key) ? dictionary[key] : default;
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        Variant value = DictGet(dictionary, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        Variant value = DictGet(dictionary, key);
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        Variant value = DictGet(dictionary, key);
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
        gameSession.Free();
    }
}

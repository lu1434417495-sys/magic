using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_party_state_fate_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPartyStateFateFieldsRoundTrip();
        TestPartyStateFromDictMissingFateFieldsIsRejected();
        TestPartyStateFromDictRejectsBadFateFlagSchema();

        RequestTestExit(_test.Finish("PartyState fate regression"));
    }

    private void TestPartyStateFateFieldsRoundTrip()
    {
        PartyState partyState = BuildPartyState();
        PartyState restored = null;
        try
        {
            partyState.SetFateRunFlag("fortuna_guidance_blessed", true);
            partyState.SetFateRunFlag("doom_gate_opened", false);

            _test.True(
                partyState.HasFateRunFlag("fortuna_guidance_blessed"),
                "写接口应保留 true 的命运周目标记。"
            );
            _test.True(
                !partyState.GetFateRunFlag("doom_gate_opened", true),
                "写接口应保留 false 的命运周目标记。"
            );

            using GodotProjectionLease<GDictionary> payloadLease =
                partyState.ToDictionaryLease("PartyStateFate.RoundTrip");
            GDictionary payload = payloadLease.Value;
            _test.True(
                !payload.ContainsKey("party_drop_luck_source_member_id"),
                "掉落承担者字段已废弃，不应继续写入 PartyState 存档。"
            );
            GDictionary payloadFlags = payload["fate_run_flags"].AsGodotDictionary();
            _test.True(payloadFlags.Count > 0, "序列化结果应暴露 fate_run_flags 字典。");
            _test.True(
                payloadFlags.ContainsKey("fortuna_guidance_blessed"),
                "fate_run_flags 应使用稳定字符串键写入存档。"
            );
            _test.True(
                payloadFlags.ContainsKey("doom_gate_opened"),
                "false 的命运周目标记也应稳定写入存档。"
            );
            _test.True(
                payloadFlags["fortuna_guidance_blessed"].AsBool(),
                "true 的命运周目标记不应在序列化时丢失。"
            );
            _test.True(
                !payloadFlags["doom_gate_opened"].AsBool(),
                "false 的命运周目标记不应在序列化时漂移。"
            );

            restored = PartyState.FromDictionary(payload);
            _test.True(restored != null, "带 fate 字段的 PartyState 应能完成 round-trip。");
            if (restored == null)
            {
                return;
            }

            _test.True(
                restored.HasFateRunFlag("fortuna_guidance_blessed"),
                "round-trip 后应保留 true 的命运周目标记。"
            );
            _test.True(
                !restored.GetFateRunFlag("doom_gate_opened", true),
                "round-trip 后应保留 false 的命运周目标记。"
            );
        }
        finally
        {
            DisposePartyState(restored);
            DisposePartyState(partyState);
        }
    }

    private void TestPartyStateFromDictMissingFateFieldsIsRejected()
    {
        using GodotProjectionLease<GDictionary> missingFatePayloadLease =
            BuildPartyPayloadLease("PartyStateFate.MissingFate");
        GDictionary missingFatePayload = missingFatePayloadLease.Value;
        missingFatePayload.Remove("fate_run_flags");
        _test.True(
            PartyState.FromDictionary(missingFatePayload) == null,
            "缺少 fate_run_flags 的 PartyState shape 应直接拒绝。"
        );

        using GodotProjectionLease<GDictionary> missingMetaPayloadLease =
            BuildPartyPayloadLease("PartyStateFate.MissingMeta");
        GDictionary missingMetaPayload = missingMetaPayloadLease.Value;
        missingMetaPayload.Remove("meta_flags");
        _test.True(
            PartyState.FromDictionary(missingMetaPayload) == null,
            "缺少 meta_flags 的 PartyState shape 应直接拒绝。"
        );

        using GodotProjectionLease<GDictionary> invalidFatePayloadLease =
            BuildPartyPayloadLease("PartyStateFate.InvalidFate");
        GDictionary invalidFatePayload = invalidFatePayloadLease.Value;
        invalidFatePayload["fate_run_flags"] = Variant.From(new GArray());
        _test.True(
            PartyState.FromDictionary(invalidFatePayload) == null,
            "fate_run_flags 类型错误的 PartyState shape 应直接拒绝。"
        );
    }

    private void TestPartyStateFromDictRejectsBadFateFlagSchema()
    {
        foreach (string fieldName in new[] { "fate_run_flags", "meta_flags" })
        {
            foreach (
                GDictionary invalidFlags in new[]
                {
                    BuildEmptyFlagKeyPayload(),
                    BuildNonBoolFlagPayload(),
                    BuildDuplicateNormalizedFlagPayload(),
                }
            )
            {
                using GodotProjectionLease<GDictionary> payloadLease =
                    BuildPartyPayloadLease($"PartyStateFate.InvalidFlag.{fieldName}");
                GDictionary payload = payloadLease.Value;
                payload[fieldName] = Variant.From(invalidFlags);
                _test.True(
                    PartyState.FromDictionary(payload) == null,
                    $"{fieldName} 内空 key、重复归一化 key 或非 bool value 应直接拒绝。"
                );
            }
        }
    }

    private static GDictionary BuildEmptyFlagKeyPayload()
    {
        GDictionary flags = new();
        flags[Variant.From("")] = Variant.From(true);
        return flags;
    }

    private static GDictionary BuildNonBoolFlagPayload()
    {
        GDictionary flags = new();
        flags[Variant.From("chapter_gate")] = Variant.From(1);
        return flags;
    }

    private static GDictionary BuildDuplicateNormalizedFlagPayload()
    {
        GDictionary flags = new();
        flags[Variant.From("1")] = Variant.From(true);
        flags[Variant.From(1)] = Variant.From(false);
        return flags;
    }

    private static GodotProjectionLease<GDictionary> BuildPartyPayloadLease(string ownerId)
    {
        PartyState partyState = BuildPartyState();
        try
        {
            return partyState.ToDictionaryLease(ownerId);
        }
        finally
        {
            DisposePartyState(partyState);
        }
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
        };
        partyState.active_member_ids.Add("hero");
        partyState.SetMemberState(BuildPartyMemberState("hero", "Hero"));
        return partyState;
    }

    private static PartyMemberState BuildPartyMemberState(StringName memberId, string displayName)
    {
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = displayName,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = displayName;
        return memberState;
    }

    private static void DisposePartyState(PartyState partyState)
    {
        if (partyState == null)
        {
            return;
        }

    }

}

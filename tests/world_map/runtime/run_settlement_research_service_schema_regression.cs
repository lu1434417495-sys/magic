using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_research_service_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestValidPayloadSucceeds();
        TestRejectsMissingFacilityName();
        TestRejectsNonStringNpcName();
        TestRejectsMissingSettlementDisplayName();
        TestRejectsCandidateMissingTargetLabel();
        TestRejectsCandidateMissingResearchId();

        return _test.Finish("Settlement research service schema regression");
    }

    private void TestValidPayloadSucceeds()
    {
        PartyState party = BuildParty();
        var service = new SettlementResearchService();
        GDictionary result = service
            .ExecuteTyped(ValidSettlement(), ValidPayload(), party)
            .ToDictionary();

        _test.True(DictBool(result, "success", false), "正式 payload 应成功执行 research 服务。");
        _test.Eq(party.GetGold(), 50, "正式 research 成功后应扣除 200 金。");
        _test.True(DictBool(result, "persist_party_state", false), "正式 research 成功后应要求持久化队伍状态。");
        _test.Eq(DictInt(result, "gold_delta", 0), -200, "正式 research 成功结果应记录 gold_delta。");
        GArray rewards = DictArray(result, "pending_character_rewards");
        _test.Eq(rewards.Count, 1, "正式 research 成功后应返回一条 pending_character_rewards。");
        GDictionary reward = rewards.Count > 0 && rewards[0].VariantType == Variant.Type.Dictionary
            ? rewards[0].AsGodotDictionary()
            : new GDictionary();
        _test.Eq(DictString(reward, "source_id", ""), "research_field_manual", "research 奖励应使用显式 research_id。");
        _test.Eq(DictString(reward, "source_label", ""), "大图书官·研究", "research 奖励应使用正式来源标签。");
        GDictionary entry = GetFirstRewardEntry(reward);
        _test.Eq(DictString(entry, "entry_type", ""), "knowledge_unlock", "research 奖励 entry_type 应来自显式候选条目。");
        _test.Eq(DictString(entry, "target_id", ""), "field_manual", "research 奖励 target_id 应来自显式候选条目。");
        _test.Eq(DictString(entry, "target_label", ""), "野外手册", "research 奖励 target_label 不应由 target_id 回填。");
        _test.Eq(DictString(entry, "reason_text", ""), "研究员整理出一份可长期翻阅的野外手册抄本。", "research 奖励 reason_text 不应由 source_label 回填。");
    }

    private void TestRejectsMissingFacilityName()
    {
        GDictionary payload = ValidPayload();
        payload.Remove("facility_name");
        AssertRejectsWithoutSideEffect(
            "缺 facility_name 的 payload 应被拒绝且不扣金币。",
            ValidSettlement(),
            payload
        );
    }

    private void TestRejectsNonStringNpcName()
    {
        GDictionary payload = ValidPayload();
        payload["npc_name"] = 12;
        AssertRejectsWithoutSideEffect(
            "非 String npc_name 的 payload 应被拒绝且不扣金币。",
            ValidSettlement(),
            payload
        );
    }

    private void TestRejectsMissingSettlementDisplayName()
    {
        GDictionary settlement = ValidSettlement();
        settlement.Remove("display_name");
        AssertRejectsWithoutSideEffect(
            "缺 settlement.display_name 时应被拒绝且不扣金币。",
            settlement,
            ValidPayload()
        );
    }

    private void TestRejectsCandidateMissingTargetLabel()
    {
        GDictionary candidate = ValidResearchCandidate();
        candidate.Remove("target_label");
        var service = new TestSettlementResearchCatalogOverrideService();
        service.setup(new GArray { candidate });
        AssertRejectsWithoutSideEffect(
            "候选缺 target_label 时应被拒绝且不扣金币。",
            ValidSettlement(),
            ValidPayload(),
            service
        );
    }

    private void TestRejectsCandidateMissingResearchId()
    {
        GDictionary candidate = ValidResearchCandidate();
        candidate.Remove("research_id");
        var service = new TestSettlementResearchCatalogOverrideService();
        service.setup(new GArray { candidate });
        AssertRejectsWithoutSideEffect(
            "候选缺 research_id 时应被拒绝且不扣金币。",
            ValidSettlement(),
            ValidPayload(),
            service
        );
    }

    private void AssertRejectsWithoutSideEffect(
        string message,
        GDictionary settlement,
        GDictionary payload,
        SettlementResearchService service = null)
    {
        PartyState party = BuildParty();
        SettlementResearchService researchService = service ?? new SettlementResearchService();
        GDictionary result = researchService
            .ExecuteTyped(settlement, payload, party)
            .ToDictionary();
        _test.False(DictBool(result, "success", true), message);
        _test.Eq(party.GetGold(), 250, $"{message} 金币不应变化。");
        GArray resultRewards = DictArray(result, "pending_character_rewards");
        _test.Eq(resultRewards.Count, 0, $"{message} 失败结果不应包含 pending_character_rewards。");
        _test.Eq(party.pending_character_rewards.Count, 0, $"{message} party_state 不应写入 pending_character_rewards。");
    }

    private static PartyState BuildParty()
    {
        var party = new PartyState();
        party.SetGold(250);
        party.leader_member_id = "hero";
        party.active_member_ids = new GStringNameArray { "hero" };
        party.SetMemberState(new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
        });
        return party;
    }

    private static GDictionary ValidSettlement()
    {
        return new GDictionary
        {
            ["settlement_id"] = "graystone_town_01",
            ["display_name"] = "灰石镇",
        };
    }

    private static GDictionary ValidPayload()
    {
        return new GDictionary
        {
            ["facility_name"] = "大图书馆",
            ["npc_name"] = "大图书官",
            ["service_type"] = "研究",
        };
    }

    private static GDictionary ValidResearchCandidate()
    {
        return new GDictionary
        {
            ["research_id"] = "research_field_manual",
            ["entry_type"] = "knowledge_unlock",
            ["target_id"] = "field_manual",
            ["target_label"] = "野外手册",
            ["reason_text"] = "研究员整理出一份可长期翻阅的野外手册抄本。",
        };
    }

    private static GDictionary GetFirstRewardEntry(GDictionary rewardData)
    {
        GArray entries = DictArray(rewardData, "entries");
        if (entries.Count == 0 || entries[0].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return (GDictionary)entries[0].AsGodotDictionary().Duplicate(true);
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }
}

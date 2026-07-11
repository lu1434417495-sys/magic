using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_research_typed_catalog_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestUnknownRewardKindFailsCatalogValidation();
        RequestTestExit(_test.Finish("Settlement research typed catalog regression"));
    }

    private void TestUnknownRewardKindFailsCatalogValidation()
    {
        GDictionary candidate = ValidResearchCandidate();
        candidate["entry_type"] = "mystery_unlock";
        var service = new TestSettlementResearchCatalogOverrideService();
        service.setup(new GArray { candidate });

        GDictionary result = SettlementServiceResultProjection.Project(
            service.ExecuteTyped(ValidSettlement(), ValidPayload(), BuildParty())
        );

        _test.False(DictBool(result, "success", true), "未知 research reward kind 应失败。");
        _test.True(
            DictString(result, "message", "").Contains("entry_type"),
            "未知 research reward kind 应作为 catalog schema error 返回，而不是静默跳过。"
        );
    }

    private static PartyState BuildParty()
    {
        var party = new PartyState();
        party.SetGold(250);
        party.leader_member_id = "hero";
        party.active_member_ids = new GStringNameArray { "hero" };
        party.SetMemberState(
            new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
            }
        );
        return party;
    }

    private static GDictionary ValidSettlement() =>
        new()
        {
            ["settlement_id"] = "graystone_town_01",
            ["display_name"] = "灰石镇",
        };

    private static GDictionary ValidPayload() =>
        new()
        {
            ["facility_name"] = "大图书馆",
            ["npc_name"] = "大图书官",
            ["service_type"] = "研究",
        };

    private static GDictionary ValidResearchCandidate() =>
        new()
        {
            ["research_id"] = "research_field_manual",
            ["entry_type"] = "knowledge_unlock",
            ["target_id"] = "field_manual",
            ["target_label"] = "野外手册",
            ["reason_text"] = "研究员整理出一份可长期翻阅的野外手册抄本。",
        };

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }
}

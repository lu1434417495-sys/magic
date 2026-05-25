using Godot;

[GlobalClass]
public partial class SettlementResearchService : RefCounted
{
    private const string RESEARCH_INTERACTION_ID = "service_research";
    private const int RESEARCH_GOLD_COST = 200;
    private static readonly StringName RESEARCH_SOURCE_TYPE = "npc_teach";
    private static readonly Godot.Collections.Array<string> REQUIRED_SERVICE_PAYLOAD_STRING_FIELDS = new() { "facility_name", "npc_name", "service_type" };
    private static readonly Godot.Collections.Array<string> REQUIRED_RESEARCH_CANDIDATE_STRING_FIELDS = new() { "research_id", "entry_type", "target_id", "target_label", "reason_text" };
    private static readonly Godot.Collections.Array<Godot.Collections.Dictionary> RESEARCH_REWARD_CATALOG = new() { new Godot.Collections.Dictionary{ {"research_id","research_field_manual"},{"entry_type","knowledge_unlock"},{"target_id","field_manual"},{"target_label","野外手册"},{"reason_text","研究员整理出一份可长期翻阅的野外手册抄本。"} }, new Godot.Collections.Dictionary{ {"research_id","research_guard_break"},{"entry_type","skill_unlock"},{"target_id","warrior_guard_break"},{"target_label","裂甲斩"},{"reason_text","研究记录补全了裂甲斩的动作拆解。"} } };

    public bool is_supported_interaction(string interactionScriptId) => interactionScriptId.StripEdges() == RESEARCH_INTERACTION_ID;

    public Godot.Collections.Dictionary build_service_metadata(GodotObject partyState, Godot.Collections.Dictionary payload = null)
    {
        payload ??= new Godot.Collections.Dictionary();
        bool canAfford = partyState != null && (bool)partyState.Call("can_afford", RESEARCH_GOLD_COST);
        string catalogError = _validate_research_catalog_schema();
        var memberAvailability = _build_member_research_availability(partyState, canAfford, catalogError);
        var requestedMemberId = ProgressionDataUtils.to_string_name(payload.ContainsKey("member_id") ? payload["member_id"] : "");
        bool hasAvailable = false; string memberDisabledReason = "";
        if (requestedMemberId != "") { var sa = memberAvailability.ContainsKey((string)requestedMemberId) ? memberAvailability[(string)requestedMemberId].AsGodotDictionary() : new Godot.Collections.Dictionary(); hasAvailable = sa.ContainsKey("has_available_research") && sa["has_available_research"].AsBool(); memberDisabledReason = sa.ContainsKey("disabled_reason") ? sa["disabled_reason"].AsString() : "暂无可研究内容"; }
        else foreach (var av in memberAvailability.Values) { if (av.VariantType == Variant.Type.Dictionary && av.AsGodotDictionary().ContainsKey("has_available_research") && av.AsGodotDictionary()["has_available_research"].AsBool()) { hasAvailable = true; break; } }
        bool isEnabled = canAfford && hasAvailable; string disabledReason = "";
        if (catalogError.Length > 0) disabledReason = "研究配置无效"; else if (!canAfford) disabledReason = "金币不足"; else if (!hasAvailable) disabledReason = memberDisabledReason.Length > 0 ? memberDisabledReason : "暂无可研究内容";
        return new Godot.Collections.Dictionary { {"cost_label",$"{RESEARCH_GOLD_COST} 金"},{"is_enabled",isEnabled},{"disabled_reason",disabledReason},{"member_availability",memberAvailability} };
    }

    public Godot.Collections.Dictionary execute(Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, GodotObject partyState, GodotObject characterManagement, GodotObject warehouseService)
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (string fn in REQUIRED_SERVICE_PAYLOAD_STRING_FIELDS) { if (!payload.ContainsKey(fn) || payload[fn].VariantType != Variant.Type.String || payload[fn].AsString().StripEdges().Length == 0) errors.Add($"缺少 {fn}"); }
        var memberId = ProgressionDataUtils.to_string_name(payload.ContainsKey("member_id") ? payload["member_id"] : "");
        var researchId = payload.ContainsKey("research_id") ? payload["research_id"].AsString().StripEdges() : "";
        if (memberId == "") errors.Add("缺少 member_id"); if (researchId.Length == 0) errors.Add("缺少 research_id");
        if (errors.Count > 0) return new Godot.Collections.Dictionary { {"ok",false},{"errors",errors} };
        if (partyState == null || !(bool)partyState.Call("can_afford", RESEARCH_GOLD_COST)) { errors.Add("金币不足"); return new Godot.Collections.Dictionary { {"ok",false},{"errors",errors} }; }
        var candidate = _find_catalog_entry(researchId); if (candidate == null) { errors.Add($"未知的研究项目: {researchId}"); return new Godot.Collections.Dictionary { {"ok",false},{"errors",errors} }; }
        var entryType = ProgressionDataUtils.to_string_name(candidate.ContainsKey("entry_type") ? candidate["entry_type"] : "");
        var targetId = ProgressionDataUtils.to_string_name(candidate.ContainsKey("target_id") ? candidate["target_id"] : "");
        var targetLabel = candidate.ContainsKey("target_label") ? candidate["target_label"].AsString() : "";
        var reasonText = candidate.ContainsKey("reason_text") ? candidate["reason_text"].AsString() : "";
        if (!PendingCharacterRewardContentRules.is_supported_entry_type(entryType) || targetId == "") { errors.Add("研究奖励配置无效"); return new Godot.Collections.Dictionary { {"ok",false},{"errors",errors} }; }
        partyState.Call("spend_gold", RESEARCH_GOLD_COST);
        var rewardEntry = new Godot.Collections.Dictionary { {"entry_type",entryType},{"target_id",targetId},{"target_label",targetLabel},{"amount",1},{"reason_text",reasonText} };
        return new Godot.Collections.Dictionary { {"ok",true},{"research_id",researchId},{"reward_entry",rewardEntry},{"source_type",RESEARCH_SOURCE_TYPE},{"source_id",researchId} };
    }

    private Godot.Collections.Dictionary _build_member_research_availability(GodotObject partyState, bool canAfford, string catalogError) { var r = new Godot.Collections.Dictionary(); if (partyState == null) return r; var memberStates = partyState.Get("member_states").AsGodotDictionary(); foreach (var kv in memberStates) { string mid = kv.Key.AsString(); var memberState = kv.Value.AsGodotObject(); var memberR = new Godot.Collections.Dictionary { {"member_id",mid},{"has_available_research",false},{"disabled_reason",""} }; if (!canAfford) memberR["disabled_reason"] = "金币不足"; else if (catalogError.Length > 0) memberR["disabled_reason"] = "研究配置无效"; else { bool hasAny = false; foreach (var cat in RESEARCH_REWARD_CATALOG) { string etype = cat.ContainsKey("entry_type") ? cat["entry_type"].AsString() : ""; string tid = cat.ContainsKey("target_id") ? cat["target_id"].AsString() : ""; if (etype.Length > 0 && tid.Length > 0 && _can_member_research(memberState, etype, tid)) { hasAny = true; break; } } memberR["has_available_research"] = hasAny; if (!hasAny) memberR["disabled_reason"] = "暂无可研究内容"; } r[mid] = memberR; } return r; }

    private static bool _can_member_research(GodotObject memberState, string entryType, string targetId) { if (memberState == null || memberState.Get("progression").AsGodotObject() == null) return false; var prog = memberState.Get("progression").AsGodotObject(); if (entryType == "knowledge_unlock") { var known = prog.Get("known_knowledge_ids").AsGodotArray(); if (known.Contains(new StringName(targetId))) return false; } else if (entryType == "skill_unlock") { var skills = prog.Get("skills").AsGodotDictionary(); var sid = new StringName(targetId); if (skills.ContainsKey(sid)) { var sp = skills[sid].AsGodotObject(); if (sp != null && sp.Get("is_learned").AsBool()) return false; } } return true; }

    private static string _validate_research_catalog_schema() { foreach (var entry in RESEARCH_REWARD_CATALOG) { foreach (string fn in REQUIRED_RESEARCH_CANDIDATE_STRING_FIELDS) { if (!entry.ContainsKey(fn) || entry[fn].VariantType != Variant.Type.String || entry[fn].AsString().StripEdges().Length == 0) return $"研究目录缺少 {fn}"; } if (!PendingCharacterRewardContentRules.is_supported_entry_type(entry["entry_type"])) return $"不支持的 entry_type: {entry["entry_type"]}"; } return ""; }

    private static Godot.Collections.Dictionary _find_catalog_entry(string researchId) { foreach (var entry in RESEARCH_REWARD_CATALOG) { if (entry.ContainsKey("research_id") && entry["research_id"].AsString() == researchId) return entry; } return null; }
}


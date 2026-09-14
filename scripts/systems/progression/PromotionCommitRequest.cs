using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>A complete immutable selection. Missing values never mean "pick something for me".</summary>
public sealed class PromotionCommitRequest
{
    private static readonly string[] Fields = { "growth_trigger_skill_id", "target_rank",
        "selected_assigned_core_skill_ids", "selected_qualifier_skill_ids", "prompt_id" };

    public StringName GrowthTriggerSkillId { get; }
    public int TargetRank { get; }
    public IReadOnlyList<StringName> AssignedCoreSkillIds { get; }
    public IReadOnlyList<StringName> QualifierSkillIds { get; }
    public string PromptId { get; }
    public bool IsWellFormed { get; }

    public PromotionCommitRequest(StringName growthTriggerSkillId, int targetRank,
        IEnumerable<StringName> assignedCoreSkillIds, IEnumerable<StringName> qualifierSkillIds,
        string promptId = "")
    {
        GrowthTriggerSkillId = growthTriggerSkillId ?? "";
        TargetRank = targetRank;
        PromptId = promptId ?? "";
        var assigned = assignedCoreSkillIds?.ToArray();
        var qualifiers = qualifierSkillIds?.ToArray();
        IsWellFormed = GrowthTriggerSkillId != "" && targetRank > 0
            && ValidIds(assigned) && ValidIds(qualifiers);
        AssignedCoreSkillIds = Array.AsReadOnly(assigned ?? Array.Empty<StringName>());
        QualifierSkillIds = Array.AsReadOnly(qualifiers ?? Array.Empty<StringName>());
    }

    public PromotionCommitRequest WithPromptId(string promptId) =>
        new(GrowthTriggerSkillId, TargetRank, AssignedCoreSkillIds, QualifierSkillIds, promptId);

    public bool IncludesSkill(StringName id) => AssignedCoreSkillIds.Contains(id) || QualifierSkillIds.Contains(id);

    public bool SelectionEquals(PromotionCommitRequest other) => other != null
        && PromptId == other.PromptId && HasSameSkills(other);

    public bool HasSameSkills(PromotionCommitRequest other) => other != null
        && GrowthTriggerSkillId == other.GrowthTriggerSkillId && TargetRank == other.TargetRank
        && AssignedCoreSkillIds.SequenceEqual(other.AssignedCoreSkillIds)
        && QualifierSkillIds.SequenceEqual(other.QualifierSkillIds);

    public Dictionary<string, object> ToPlainPayload() => new(StringComparer.Ordinal)
    {
        [Fields[0]] = GrowthTriggerSkillId.ToString(),
        [Fields[1]] = TargetRank,
        [Fields[2]] = AssignedCoreSkillIds.Select(id => (object)id.ToString()).ToList(),
        [Fields[3]] = QualifierSkillIds.Select(id => (object)id.ToString()).ToList(),
        [Fields[4]] = PromptId,
    };

    public GDictionary ToDictionary()
    {
        using var lease = RuntimePlainPayload.ProjectDictionaryLease(ToPlainPayload(),
            "PromotionCommitRequest.payload", LifetimeDomain.Request, "PromotionCommitRequest.payload");
        return lease.Value.Duplicate(true);
    }

    public static PromotionCommitRequest FromPlainPayload(IReadOnlyDictionary<string, object> payload)
    {
        if (payload == null || payload.Count != Fields.Length || Fields.Any(key => !payload.ContainsKey(key))
            || payload[Fields[0]] is not string trigger || payload[Fields[4]] is not string prompt
            || !TryInt(payload[Fields[1]], out int rank)
            || !TryIds(payload[Fields[2]], out var assigned) || !TryIds(payload[Fields[3]], out var qualifiers))
            return null;
        var request = new PromotionCommitRequest(trigger, rank, assigned, qualifiers, prompt);
        return request.IsWellFormed ? request : null;
    }

    public static PromotionCommitRequest FromPayload(GDictionary payload)
    {
        if (payload == null || payload.Count != Fields.Length || Fields.Any(key => !payload.ContainsKey(key)))
            return null;
        if (!StringValue(payload[Fields[0]]) || !StringValue(payload[Fields[4]])
            || payload[Fields[1]].VariantType != Variant.Type.Int
            || payload[Fields[1]].AsInt64() > int.MaxValue || payload[Fields[1]].AsInt64() <= 0
            || payload[Fields[2]].VariantType != Variant.Type.Array || payload[Fields[3]].VariantType != Variant.Type.Array)
            return null;
        using var assignedValues = payload[Fields[2]].AsGodotArray();
        using var qualifierValues = payload[Fields[3]].AsGodotArray();
        var assigned = ReadIds(assignedValues);
        var qualifiers = ReadIds(qualifierValues);
        var request = new PromotionCommitRequest(payload[Fields[0]].AsString(), payload[Fields[1]].AsInt32(),
            assigned, qualifiers, payload[Fields[4]].AsString());
        return request.IsWellFormed ? request : null;
    }

    private static bool ValidIds(StringName[] ids) => ids != null && ids.All(id => id != null && id != "")
        && ids.Distinct().Count() == ids.Length;
    private static bool StringValue(Variant value) => value.VariantType is Variant.Type.String or Variant.Type.StringName;
    private static List<StringName> ReadIds(Godot.Collections.Array values)
    {
        List<StringName> ids = new();
        foreach (Variant value in values)
        {
            if (!StringValue(value)) return null;
            ids.Add(value.AsString());
        }
        return ids;
    }
    private static bool TryInt(object value, out int result)
    {
        result = 0;
        if (value is int i) { result = i; return true; }
        if (value is long l && l > 0 && l <= int.MaxValue) { result = (int)l; return true; }
        return false;
    }
    private static bool TryIds(object value, out IReadOnlyList<StringName> ids)
    {
        ids = null;
        if (value is not IEnumerable<object> values) return false;
        List<StringName> result = new();
        foreach (object item in values)
        {
            if (item is not string text) return false;
            result.Add(text);
        }
        ids = result;
        return true;
    }
}

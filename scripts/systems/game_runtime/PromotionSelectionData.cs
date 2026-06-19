using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class PromotionSelectionData
{
    internal const string QualifierSkillIdsKey = "selected_qualifier_skill_ids";
    internal const string AssignedCoreSkillIdsKey = "selected_assigned_core_skill_ids";
    internal const string TriggerSkillIdsKey = "trigger_skill_ids";

    public static PromotionSelectionData Empty { get; } = new();

    public IReadOnlyList<StringName> AssignedCoreSkillIds { get; }
    public IReadOnlyList<StringName> QualifierSkillIds { get; }
    public IReadOnlyList<StringName> TriggerSkillIds { get; }
    public bool HasAssignedCoreSkillIds { get; }
    public bool HasQualifierSkillIds { get; }
    public bool HasTriggerSkillIds { get; }

    public PromotionSelectionData(
        IEnumerable assignedCoreSkillIds = null,
        IEnumerable qualifierSkillIds = null,
        IEnumerable triggerSkillIds = null,
        bool hasAssignedCoreSkillIds = false,
        bool hasQualifierSkillIds = false,
        bool hasTriggerSkillIds = false
    )
    {
        AssignedCoreSkillIds = Normalize(assignedCoreSkillIds);
        QualifierSkillIds = Normalize(qualifierSkillIds);
        TriggerSkillIds = Normalize(triggerSkillIds);
        HasAssignedCoreSkillIds = hasAssignedCoreSkillIds || assignedCoreSkillIds != null;
        HasQualifierSkillIds = hasQualifierSkillIds || qualifierSkillIds != null;
        HasTriggerSkillIds = hasTriggerSkillIds || triggerSkillIds != null;
    }

    public static PromotionSelectionData FromPayload(GDictionary payload)
    {
        payload ??= new GDictionary();
        bool hasAssigned = HasPayloadKey(payload, AssignedCoreSkillIdsKey);
        bool hasQualifier = HasPayloadKey(payload, QualifierSkillIdsKey);
        bool hasTrigger = HasPayloadKey(payload, TriggerSkillIdsKey);
        return new PromotionSelectionData(
            ReadPayloadArray(payload, AssignedCoreSkillIdsKey),
            ReadPayloadArray(payload, QualifierSkillIdsKey),
            ReadPayloadArray(payload, TriggerSkillIdsKey),
            hasAssigned,
            hasQualifier,
            hasTrigger
        );
    }

    public bool IncludesSkill(StringName skillId)
    {
        return skillId != ""
            && (
                AssignedCoreSkillIds.Contains(skillId)
                || QualifierSkillIds.Contains(skillId)
                || TriggerSkillIds.Contains(skillId)
            );
    }

    public bool SelectionEquals(PromotionSelectionData other)
    {
        other ??= Empty;
        return HasAssignedCoreSkillIds == other.HasAssignedCoreSkillIds
            && HasQualifierSkillIds == other.HasQualifierSkillIds
            && HasTriggerSkillIds == other.HasTriggerSkillIds
            && AssignedCoreSkillIds.SequenceEqual(other.AssignedCoreSkillIds)
            && QualifierSkillIds.SequenceEqual(other.QualifierSkillIds)
            && TriggerSkillIds.SequenceEqual(other.TriggerSkillIds);
    }

    public GDictionary ToPayloadProjection()
    {
        GDictionary payload = new();
        if (HasAssignedCoreSkillIds)
            payload[AssignedCoreSkillIdsKey] = BuildStringNameArray(AssignedCoreSkillIds);
        if (HasQualifierSkillIds)
            payload[QualifierSkillIdsKey] = BuildStringNameArray(QualifierSkillIds);
        if (HasTriggerSkillIds)
            payload[TriggerSkillIdsKey] = BuildStringNameArray(TriggerSkillIds);
        return payload;
    }

    internal static GStringNameArray BuildStringNameArray(IEnumerable<StringName> values)
    {
        GStringNameArray result = new();
        if (values == null)
            return result;
        foreach (StringName value in values)
            if (value != "")
                result.Add(value);
        return result;
    }

    private static IReadOnlyList<StringName> Normalize(IEnumerable values)
    {
        if (values == null)
            return System.Array.Empty<StringName>();

        List<StringName> result = new();
        HashSet<StringName> seen = new();
        foreach (object rawValue in values)
        {
            StringName value = ProgressionDataUtils.to_string_name(rawValue);
            if (value == "" || !seen.Add(value))
                continue;
            result.Add(value);
        }
        return result.ToArray();
    }

    private static IEnumerable ReadPayloadArray(GDictionary payload, string key)
    {
        if (!TryReadPayloadValue(payload, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : null;
    }

    private static bool HasPayloadKey(GDictionary payload, string key)
    {
        if (payload == null)
            return false;
        return payload.ContainsKey(key) || payload.ContainsKey(new StringName(key));
    }

    private static bool TryReadPayloadValue(GDictionary payload, string key, out Variant value)
    {
        value = default;
        if (payload == null)
            return false;
        if (payload.ContainsKey(key))
        {
            value = payload[key];
            return true;
        }
        StringName stringNameKey = new(key);
        if (payload.ContainsKey(stringNameKey))
        {
            value = payload[stringNameKey];
            return true;
        }
        return false;
    }
}

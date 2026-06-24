using System.Collections;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public class PendingProfessionChoice
{
    private readonly List<StringName> _triggerSkillIds = new();
    private readonly List<StringName> _candidateProfessionIds = new();
    private readonly Dictionary<StringName, int> _targetRankMap = new();
    private readonly List<StringName> _qualifierSkillPoolIds = new();
    private readonly List<StringName> _assignableSkillCandidateIds = new();

    public GStringNameArray trigger_skill_ids
    {
        get => BuildStringNameArray(_triggerSkillIds);
        set => SetTriggerSkillIds(value);
    }

    public GStringNameArray candidate_profession_ids
    {
        get => BuildStringNameArray(_candidateProfessionIds);
        set => SetCandidateProfessionIds(value);
    }

    public GDictionary target_rank_map
    {
        get => BuildTargetRankMap();
        set => SetTargetRankMap(value);
    }

    public GStringNameArray qualifier_skill_pool_ids
    {
        get => BuildStringNameArray(_qualifierSkillPoolIds);
        set => SetQualifierSkillPoolIds(value);
    }

    public GStringNameArray assignable_skill_candidate_ids
    {
        get => BuildStringNameArray(_assignableSkillCandidateIds);
        set => SetAssignableSkillCandidateIds(value);
    }

    public int required_qualifier_count { get; set; }
    public int required_assigned_core_count { get; set; }

    internal IReadOnlyList<StringName> TriggerSkillIdsTyped => _triggerSkillIds;
    internal IReadOnlyList<StringName> CandidateProfessionIdsTyped => _candidateProfessionIds;
    internal IReadOnlyDictionary<StringName, int> TargetRankMapTyped => _targetRankMap;
    internal IReadOnlyList<StringName> QualifierSkillPoolIdsTyped => _qualifierSkillPoolIds;
    internal IReadOnlyList<StringName> AssignableSkillCandidateIdsTyped =>
        _assignableSkillCandidateIds;

    public void SetTriggerSkillIds(IEnumerable values) => SetUniqueStringNames(_triggerSkillIds, values);

    public void AddTriggerSkillId(StringName skillId) => AddUniqueStringName(_triggerSkillIds, skillId);

    public void SetCandidateProfessionIds(IEnumerable values) =>
        SetUniqueStringNames(_candidateProfessionIds, values);

    public void AddCandidateProfessionId(StringName professionId) =>
        AddUniqueStringName(_candidateProfessionIds, professionId);

    public void SetQualifierSkillPoolIds(IEnumerable values) =>
        SetUniqueStringNames(_qualifierSkillPoolIds, values);

    public void AddQualifierSkillPoolId(StringName skillId) =>
        AddUniqueStringName(_qualifierSkillPoolIds, skillId);

    public void SetAssignableSkillCandidateIds(IEnumerable values) =>
        SetUniqueStringNames(_assignableSkillCandidateIds, values);

    public void AddAssignableSkillCandidateId(StringName skillId) =>
        AddUniqueStringName(_assignableSkillCandidateIds, skillId);

    public void SetTargetRank(StringName professionId, int targetRank)
    {
        if (professionId == "" || targetRank < 0)
            return;
        _targetRankMap[professionId] = targetRank;
    }

    private void SetTargetRankMap(GDictionary values)
    {
        _targetRankMap.Clear();
        if (values == null)
            return;
        foreach (var entry in ParseStringNameIntDictionary(values))
            _targetRankMap[entry.Key] = entry.Value;
    }

    public bool TryGetTargetRank(StringName professionId, out int targetRank)
    {
        if (professionId != "")
            return _targetRankMap.TryGetValue(professionId, out targetRank);
        targetRank = 0;
        return false;
    }

    public PendingProfessionChoice DuplicateState()
    {
        var copy = new PendingProfessionChoice
        {
            required_qualifier_count = required_qualifier_count,
            required_assigned_core_count = required_assigned_core_count,
        };
        copy.SetTriggerSkillIds(_triggerSkillIds);
        copy.SetCandidateProfessionIds(_candidateProfessionIds);
        copy.SetQualifierSkillPoolIds(_qualifierSkillPoolIds);
        copy.SetAssignableSkillCandidateIds(_assignableSkillCandidateIds);
        foreach (var entry in _targetRankMap)
            copy.SetTargetRank(entry.Key, entry.Value);
        return copy;
    }

    public GDictionary ToDictionary() =>
        new()
        {
            ["trigger_skill_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                _triggerSkillIds
            ),
            ["candidate_profession_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                _candidateProfessionIds
            ),
            ["target_rank_map"] = ProgressionDataUtils.string_name_int_map_to_string_dict(
                _targetRankMap
            ),
            ["qualifier_skill_pool_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                _qualifierSkillPoolIds
            ),
            ["assignable_skill_candidate_ids"] =
                ProgressionDataUtils.string_name_array_to_string_array(
                    _assignableSkillCandidateIds
                ),
            ["required_qualifier_count"] = required_qualifier_count,
            ["required_assigned_core_count"] = required_assigned_core_count,
        };

    public static PendingProfessionChoice FromDictionary(GDictionary data)
    {
        if (
            !HasExactFields(
                data,
                new[]
                {
                    "trigger_skill_ids",
                    "candidate_profession_ids",
                    "target_rank_map",
                    "qualifier_skill_pool_ids",
                    "assignable_skill_candidate_ids",
                    "required_qualifier_count",
                    "required_assigned_core_count",
                }
            )
        )
            return null;
        if (data["trigger_skill_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["candidate_profession_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["target_rank_map"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["qualifier_skill_pool_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["assignable_skill_candidate_ids"].VariantType != Variant.Type.Array)
            return null;
        var triggerSkillIds = ParseUniqueStringNameList(data["trigger_skill_ids"].AsGodotArray());
        if (triggerSkillIds == null)
            return null;
        var candidateProfessionIds = ParseUniqueStringNameList(
            data["candidate_profession_ids"].AsGodotArray()
        );
        if (candidateProfessionIds == null)
            return null;
        var targetRankMap = ParseStringNameIntDictionary(data["target_rank_map"].AsGodotDictionary());
        if (targetRankMap == null)
            return null;
        var qualifierSkillPoolIds = ParseUniqueStringNameList(
            data["qualifier_skill_pool_ids"].AsGodotArray()
        );
        if (qualifierSkillPoolIds == null)
            return null;
        var assignableSkillCandidateIds = ParseUniqueStringNameList(
            data["assignable_skill_candidate_ids"].AsGodotArray()
        );
        if (assignableSkillCandidateIds == null)
            return null;
        if (
            data["required_qualifier_count"].VariantType != Variant.Type.Int
            || data["required_qualifier_count"].AsInt32() < 0
        )
            return null;
        if (
            data["required_assigned_core_count"].VariantType != Variant.Type.Int
            || data["required_assigned_core_count"].AsInt32() < 0
        )
            return null;

        var result = new PendingProfessionChoice
        {
            required_qualifier_count = data["required_qualifier_count"].AsInt32(),
            required_assigned_core_count = data["required_assigned_core_count"].AsInt32(),
        };
        result.SetTriggerSkillIds(triggerSkillIds);
        result.SetCandidateProfessionIds(candidateProfessionIds);
        foreach (var entry in targetRankMap)
            result.SetTargetRank(entry.Key, entry.Value);
        result.SetQualifierSkillPoolIds(qualifierSkillPoolIds);
        result.SetAssignableSkillCandidateIds(assignableSkillCandidateIds);
        return result;
    }

    private static bool HasExactFields(GDictionary data, IReadOnlyList<string> fields)
    {
        if (data == null || data.Count != fields.Count)
            return false;
        foreach (string fieldName in fields)
            if (!data.ContainsKey(fieldName))
                return false;
        return true;
    }

    private static void SetUniqueStringNames(List<StringName> target, IEnumerable values)
    {
        target.Clear();
        if (values == null)
            return;
        foreach (object value in values)
            AddUniqueStringName(target, ProgressionDataUtils.to_string_name(value));
    }

    private static void AddUniqueStringName(List<StringName> target, StringName value)
    {
        if (value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private static GStringNameArray BuildStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private GDictionary BuildTargetRankMap()
    {
        var result = new GDictionary();
        foreach (var entry in _targetRankMap)
            result[entry.Key] = entry.Value;
        return result;
    }

    private static List<StringName> ParseUniqueStringNameList(Godot.Collections.Array values)
    {
        var results = new List<StringName>();
        var seen = new HashSet<StringName>();
        foreach (var rawValue in values)
        {
            if (!TryParseRequiredStringName(rawValue, out StringName parsed) || !seen.Add(parsed))
                return null;
            results.Add(parsed);
        }
        return results;
    }

    private static Dictionary<StringName, int> ParseStringNameIntDictionary(GDictionary values)
    {
        var parsedValues = new Dictionary<StringName, int>();
        var seen = new HashSet<StringName>();
        foreach (var rawKey in values.Keys)
        {
            if (!TryParseRequiredStringName(rawKey, out StringName parsedKey) || !seen.Add(parsedKey))
                return null;
            var rawValue = values[rawKey];
            if (rawValue.VariantType != Variant.Type.Int || rawValue.AsInt32() < 0)
                return null;
            parsedValues[parsedKey] = rawValue.AsInt32();
        }
        return parsedValues;
    }

    private static bool TryParseRequiredStringName(object rawValue, out StringName parsed)
    {
        parsed = default;
        if (rawValue is Variant value)
        {
            var vt = value.VariantType;
            if (vt != Variant.Type.String && vt != Variant.Type.StringName)
                return false;
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return false;
        }
        parsed = ProgressionDataUtils.to_string_name(rawValue);
        return parsed != "";
    }
}

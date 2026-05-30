using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiBlackboard : RefCounted
{
    public StringName last_brain_id = "";
    public StringName last_state_id = "";
    public StringName last_action_id = "";
    public StringName last_reason_text = "";
    public StringName last_transition_previous_state_id = "";
    public StringName last_transition_state_id = "";
    public StringName last_transition_rule_id = "";
    public StringName last_transition_reason = "";

    public int turn_started_tu = 0;
    public int turn_decision_count = 0;

    public bool madness_target_any_team = false;
    public bool low_luck_reverse_fate_used = false;
    public bool low_luck_black_star_wedge_used = false;
    public bool meteor_protected_ally = false;
    public bool protected_ally = false;
    public bool summoned = false;
    public bool temporary_unit = false;
    public StringName summon_source_unit_id = "";

    private bool _hasTurnStartedTu;
    private bool _hasTurnDecisionCount;

    public static implicit operator GDictionary(BattleAiBlackboard blackboard) =>
        blackboard?.ToDictionary() ?? new GDictionary();

    public static implicit operator BattleAiBlackboard(GDictionary data) => FromDictionary(data);

    public bool ContainsKey(object key) => HasKey(NormalizeKey(key));

    public bool has(object key) => ContainsKey(key);

    public bool Remove(object key)
    {
        string normalizedKey = NormalizeKey(key);
        if (!HasKey(normalizedKey))
            return false;
        ClearKey(normalizedKey);
        return true;
    }

    public Variant GetValueOrDefault(object key, object fallback)
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? this[normalizedKey] : ObjectToVariant(fallback);
    }

    public Variant get(object key, Variant fallback = default)
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? this[normalizedKey] : fallback;
    }

    public Variant this[object key]
    {
        get => ReadValue(NormalizeKey(key));
        set => WriteValue(NormalizeKey(key), value);
    }

    public GDictionary Duplicate(bool deep = false) => ToDictionary();

    public GDictionary ToDictionary()
    {
        var result = new GDictionary();
        AddStringName(result, "last_brain_id", last_brain_id);
        AddStringName(result, "last_state_id", last_state_id);
        AddStringName(result, "last_action_id", last_action_id);
        AddStringName(result, "last_reason_text", last_reason_text);
        AddStringName(
            result,
            "last_transition_previous_state_id",
            last_transition_previous_state_id
        );
        AddStringName(result, "last_transition_state_id", last_transition_state_id);
        AddStringName(result, "last_transition_rule_id", last_transition_rule_id);
        AddStringName(result, "last_transition_reason", last_transition_reason);
        if (_hasTurnStartedTu)
            result["turn_started_tu"] = turn_started_tu;
        if (_hasTurnDecisionCount)
            result["turn_decision_count"] = turn_decision_count;
        AddBool(result, "madness_target_any_team", madness_target_any_team);
        AddBool(result, "low_luck_reverse_fate_used", low_luck_reverse_fate_used);
        AddBool(result, "low_luck_black_star_wedge_used", low_luck_black_star_wedge_used);
        AddBool(result, "meteor_protected_ally", meteor_protected_ally);
        AddBool(result, "protected_ally", protected_ally);
        AddBool(result, "summoned", summoned);
        AddBool(result, "temporary_unit", temporary_unit);
        AddStringName(result, "summon_source_unit_id", summon_source_unit_id);
        return result;
    }

    public static BattleAiBlackboard FromDictionary(GDictionary data)
    {
        var blackboard = new BattleAiBlackboard();
        if (data == null)
            return blackboard;
        foreach (object key in data.Keys)
        {
            string normalizedKey = NormalizeKey(key);
            blackboard[normalizedKey] = ObjectToVariant(data[normalizedKey]);
        }
        return blackboard;
    }

    private static void AddBool(GDictionary result, string key, bool value)
    {
        if (value)
            result[key] = value;
    }

    private static void AddStringName(GDictionary result, string key, StringName value)
    {
        if (value != "")
            result[key] = value;
    }

    private static string NormalizeKey(object key)
    {
        return key switch
        {
            null => "",
            StringName stringNameKey => stringNameKey.ToString(),
            Variant variantKey => variantKey.AsString(),
            _ => key.ToString() ?? "",
        };
    }

    private static Variant ObjectToVariant(object value) =>
        value switch
        {
            null => default,
            Variant variantValue => variantValue,
            bool boolValue => Variant.From(boolValue),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            string stringValue => Variant.From(stringValue),
            StringName stringNameValue => Variant.From(stringNameValue),
            Vector2I vector2IValue => Variant.From(vector2IValue),
            _ => Variant.From(value.ToString() ?? ""),
        };

    private bool HasKey(string key) =>
        key switch
        {
            "last_brain_id" => last_brain_id != "",
            "last_state_id" => last_state_id != "",
            "last_action_id" => last_action_id != "",
            "last_reason_text" => last_reason_text != "",
            "last_transition_previous_state_id" => last_transition_previous_state_id != "",
            "last_transition_state_id" => last_transition_state_id != "",
            "last_transition_rule_id" => last_transition_rule_id != "",
            "last_transition_reason" => last_transition_reason != "",
            "turn_started_tu" => _hasTurnStartedTu,
            "turn_decision_count" => _hasTurnDecisionCount,
            "madness_target_any_team" => madness_target_any_team,
            "low_luck_reverse_fate_used" => low_luck_reverse_fate_used,
            "low_luck_black_star_wedge_used" => low_luck_black_star_wedge_used,
            "meteor_protected_ally" => meteor_protected_ally,
            "protected_ally" => protected_ally,
            "summoned" => summoned,
            "temporary_unit" => temporary_unit,
            "summon_source_unit_id" => summon_source_unit_id != "",
            _ => false,
        };

    private Variant ReadValue(string key) =>
        key switch
        {
            "last_brain_id" => Variant.From(last_brain_id),
            "last_state_id" => Variant.From(last_state_id),
            "last_action_id" => Variant.From(last_action_id),
            "last_reason_text" => Variant.From(last_reason_text),
            "last_transition_previous_state_id" => Variant.From(last_transition_previous_state_id),
            "last_transition_state_id" => Variant.From(last_transition_state_id),
            "last_transition_rule_id" => Variant.From(last_transition_rule_id),
            "last_transition_reason" => Variant.From(last_transition_reason),
            "turn_started_tu" => Variant.From(turn_started_tu),
            "turn_decision_count" => Variant.From(turn_decision_count),
            "madness_target_any_team" => Variant.From(madness_target_any_team),
            "low_luck_reverse_fate_used" => Variant.From(low_luck_reverse_fate_used),
            "low_luck_black_star_wedge_used" => Variant.From(low_luck_black_star_wedge_used),
            "meteor_protected_ally" => Variant.From(meteor_protected_ally),
            "protected_ally" => Variant.From(protected_ally),
            "summoned" => Variant.From(summoned),
            "temporary_unit" => Variant.From(temporary_unit),
            "summon_source_unit_id" => Variant.From(summon_source_unit_id),
            _ => default,
        };

    private void WriteValue(string key, Variant value)
    {
        switch (key)
        {
            case "last_brain_id":
                last_brain_id = value.AsStringName();
                break;
            case "last_state_id":
                last_state_id = value.AsStringName();
                break;
            case "last_action_id":
                last_action_id = value.AsStringName();
                break;
            case "last_reason_text":
                last_reason_text = value.AsStringName();
                break;
            case "last_transition_previous_state_id":
                last_transition_previous_state_id = value.AsStringName();
                break;
            case "last_transition_state_id":
                last_transition_state_id = value.AsStringName();
                break;
            case "last_transition_rule_id":
                last_transition_rule_id = value.AsStringName();
                break;
            case "last_transition_reason":
                last_transition_reason = value.AsStringName();
                break;
            case "turn_started_tu":
                turn_started_tu = value.AsInt32();
                _hasTurnStartedTu = true;
                break;
            case "turn_decision_count":
                turn_decision_count = value.AsInt32();
                _hasTurnDecisionCount = true;
                break;
            case "madness_target_any_team":
                madness_target_any_team = value.AsBool();
                break;
            case "low_luck_reverse_fate_used":
                low_luck_reverse_fate_used = value.AsBool();
                break;
            case "low_luck_black_star_wedge_used":
                low_luck_black_star_wedge_used = value.AsBool();
                break;
            case "meteor_protected_ally":
                meteor_protected_ally = value.AsBool();
                break;
            case "protected_ally":
                protected_ally = value.AsBool();
                break;
            case "summoned":
                summoned = value.AsBool();
                break;
            case "temporary_unit":
                temporary_unit = value.AsBool();
                break;
            case "summon_source_unit_id":
                summon_source_unit_id = value.AsStringName();
                break;
        }
    }

    private void ClearKey(string key)
    {
        switch (key)
        {
            case "turn_started_tu":
                turn_started_tu = 0;
                _hasTurnStartedTu = false;
                break;
            case "turn_decision_count":
                turn_decision_count = 0;
                _hasTurnDecisionCount = false;
                break;
            case "madness_target_any_team":
                madness_target_any_team = false;
                break;
            case "low_luck_reverse_fate_used":
                low_luck_reverse_fate_used = false;
                break;
            case "low_luck_black_star_wedge_used":
                low_luck_black_star_wedge_used = false;
                break;
            case "meteor_protected_ally":
                meteor_protected_ally = false;
                break;
            case "protected_ally":
                protected_ally = false;
                break;
            case "summoned":
                summoned = false;
                break;
            case "temporary_unit":
                temporary_unit = false;
                break;
            case "summon_source_unit_id":
                summon_source_unit_id = "";
                break;
        }
    }
}

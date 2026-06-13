using Godot;

internal partial class BattleAiBlackboard : RefCounted
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

    public bool madness_ai_control = false;
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

    public bool ContainsKey(string key) => HasKey(NormalizeKey(key));

    public bool ContainsKey(StringName key) => HasKey(NormalizeKey(key));

    public bool has(string key) => ContainsKey(key);

    public bool has(StringName key) => ContainsKey(key);

    public bool Remove(string key)
    {
        string normalizedKey = NormalizeKey(key);
        if (!HasKey(normalizedKey))
            return false;
        ClearKey(normalizedKey);
        return true;
    }

    public bool Remove(StringName key) => Remove(NormalizeKey(key));

    public string get(string key, string fallback = "")
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? ReadTextValue(normalizedKey) : fallback;
    }

    public StringName GetStringName(string key, StringName fallback = default)
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? ReadStringNameValue(normalizedKey) : fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? ReadIntValue(normalizedKey) : fallback;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        string normalizedKey = NormalizeKey(key);
        return HasKey(normalizedKey) ? ReadBoolValue(normalizedKey) : fallback;
    }

    public void SetStringName(string key, StringName value)
    {
        WriteStringNameValue(NormalizeKey(key), value);
    }

    public void SetText(string key, string value)
    {
        WriteStringNameValue(NormalizeKey(key), new StringName(value ?? ""));
    }

    public void SetInt(string key, int value)
    {
        WriteIntValue(NormalizeKey(key), value);
    }

    public void SetBool(string key, bool value)
    {
        WriteBoolValue(NormalizeKey(key), value);
    }

    private static string NormalizeKey(string key) => key ?? "";

    private static string NormalizeKey(StringName key) => key.ToString();

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
            "madness_ai_control" => madness_ai_control,
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

    private string ReadTextValue(string key) =>
        key switch
        {
            "last_brain_id" => last_brain_id.ToString(),
            "last_state_id" => last_state_id.ToString(),
            "last_action_id" => last_action_id.ToString(),
            "last_reason_text" => last_reason_text.ToString(),
            "last_transition_previous_state_id" => last_transition_previous_state_id.ToString(),
            "last_transition_state_id" => last_transition_state_id.ToString(),
            "last_transition_rule_id" => last_transition_rule_id.ToString(),
            "last_transition_reason" => last_transition_reason.ToString(),
            "turn_started_tu" => turn_started_tu.ToString(),
            "turn_decision_count" => turn_decision_count.ToString(),
            "madness_ai_control" => madness_ai_control.ToString(),
            "madness_target_any_team" => madness_target_any_team.ToString(),
            "low_luck_reverse_fate_used" => low_luck_reverse_fate_used.ToString(),
            "low_luck_black_star_wedge_used" => low_luck_black_star_wedge_used.ToString(),
            "meteor_protected_ally" => meteor_protected_ally.ToString(),
            "protected_ally" => protected_ally.ToString(),
            "summoned" => summoned.ToString(),
            "temporary_unit" => temporary_unit.ToString(),
            "summon_source_unit_id" => summon_source_unit_id.ToString(),
            _ => "",
        };

    private StringName ReadStringNameValue(string key) => new(ReadTextValue(key));

    private int ReadIntValue(string key) =>
        key switch
        {
            "turn_started_tu" => turn_started_tu,
            "turn_decision_count" => turn_decision_count,
            _ => 0,
        };

    private bool ReadBoolValue(string key) =>
        key switch
        {
            "madness_ai_control" => madness_ai_control,
            "madness_target_any_team" => madness_target_any_team,
            "low_luck_reverse_fate_used" => low_luck_reverse_fate_used,
            "low_luck_black_star_wedge_used" => low_luck_black_star_wedge_used,
            "meteor_protected_ally" => meteor_protected_ally,
            "protected_ally" => protected_ally,
            "summoned" => summoned,
            "temporary_unit" => temporary_unit,
            _ => false,
        };

    private void WriteStringNameValue(string key, StringName value)
    {
        switch (key)
        {
            case "last_brain_id":
                last_brain_id = value;
                break;
            case "last_state_id":
                last_state_id = value;
                break;
            case "last_action_id":
                last_action_id = value;
                break;
            case "last_reason_text":
                last_reason_text = value;
                break;
            case "last_transition_previous_state_id":
                last_transition_previous_state_id = value;
                break;
            case "last_transition_state_id":
                last_transition_state_id = value;
                break;
            case "last_transition_rule_id":
                last_transition_rule_id = value;
                break;
            case "last_transition_reason":
                last_transition_reason = value;
                break;
            case "summon_source_unit_id":
                summon_source_unit_id = value;
                break;
        }
    }

    private void WriteIntValue(string key, int value)
    {
        switch (key)
        {
            case "turn_started_tu":
                turn_started_tu = value;
                _hasTurnStartedTu = true;
                break;
            case "turn_decision_count":
                turn_decision_count = value;
                _hasTurnDecisionCount = true;
                break;
        }
    }

    private void WriteBoolValue(string key, bool value)
    {
        switch (key)
        {
            case "madness_ai_control":
                madness_ai_control = value;
                break;
            case "madness_target_any_team":
                madness_target_any_team = value;
                break;
            case "low_luck_reverse_fate_used":
                low_luck_reverse_fate_used = value;
                break;
            case "low_luck_black_star_wedge_used":
                low_luck_black_star_wedge_used = value;
                break;
            case "meteor_protected_ally":
                meteor_protected_ally = value;
                break;
            case "protected_ally":
                protected_ally = value;
                break;
            case "summoned":
                summoned = value;
                break;
            case "temporary_unit":
                temporary_unit = value;
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
            case "madness_ai_control":
                madness_ai_control = false;
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

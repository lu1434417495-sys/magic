using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiDecisionCommitter : RefCounted
{
    public void attach_state_patch(BattleAiDecision decision)
    {
        if (decision == null)
            return;
        decision.state_patch = build_state_patch(decision);
    }

    public GDictionary build_state_patch(BattleAiDecision decision)
    {
        if (decision == null)
            return new GDictionary();
        var blackboard_set = new GDictionary
        {
            ["last_brain_id"] = decision.brain_id.ToString(),
            ["last_state_id"] = decision.state_id.ToString(),
            ["last_action_id"] = decision.action_id.ToString(),
            ["last_reason_text"] = decision.reason_text,
        };
        if (decision.transition != null && decision.transition.Count > 0)
        {
            blackboard_set["last_transition_previous_state_id"] = decision.transition._get("previous_state_id", "").ToString();
            blackboard_set["last_transition_state_id"] = decision.transition._get("state_id", "").ToString();
            blackboard_set["last_transition_rule_id"] = decision.transition._get("rule_id", "").ToString();
            blackboard_set["last_transition_reason"] = decision.transition._get("reason", "").ToString();
        }
        var patch = new GDictionary
        {
            ["blackboard_set"] = blackboard_set,
            ["blackboard_increment"] = new GDictionary
            {
                ["turn_decision_count"] = 1,
            },
        };
        if (decision.brain_id != "")
            patch["ai_brain_id"] = decision.brain_id;
        if (decision.state_id != "")
            patch["ai_state_id"] = decision.state_id;
        return patch;
    }

    public void commit(BattleUnitState unit_state, BattleAiDecision decision)
    {
        if (unit_state == null || decision == null)
            return;
        var patch = decision.state_patch != null && decision.state_patch.Count > 0
            ? decision.state_patch
            : build_state_patch(decision);
        if (!validate_state_patch(patch))
            return;
        if (patch.ContainsKey("ai_brain_id"))
            unit_state.ai_brain_id = ProgressionDataUtils.to_string_name(_get(patch,"ai_brain_id", ""));
        if (patch.ContainsKey("ai_state_id"))
            unit_state.ai_state_id = ProgressionDataUtils.to_string_name(patch._get("ai_state_id", ""));
        var blackboard_set = patch._get("blackboard_set", new GDictionary());
        if (blackboard_set.VariantType == Variant.Type.Dictionary)
        {
            var set_dict = blackboard_set.AsGodotDictionary();
            foreach (var key in set_dict.Keys)
            {
                unit_state.ai_blackboard[key.ToString()] = set_dict[key];
            }
        }
        var blackboard_increment = patch._get("blackboard_increment", new GDictionary());
        if (blackboard_increment.VariantType == Variant.Type.Dictionary)
        {
            var inc_dict = blackboard_increment.AsGodotDictionary();
            foreach (var key in inc_dict.Keys)
            {
                var key_string = key.ToString();
                unit_state.ai_blackboard[key_string] = unit_state.ai_blackboard._get(key_string, 0).AsInt32() + inc_dict[key].AsInt32();
            }
        }
    }

    public bool validate_state_patch(GDictionary patch)
    {
        if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(patch, "BattleAiDecision.state_patch"))
            return false;
        foreach (var key in patch.Keys)
        {
            if (!_is_allowed_state_patch_key(key.ToString()))
                return _fail("BattleAiDecision.state_patch contains unsupported key " + key.ToString());
        }
        if (patch.ContainsKey("ai_brain_id") && !_is_stringish(patch._get("ai_brain_id")))
            return _fail("state_patch.ai_brain_id must be StringName/String.");
        if (patch.ContainsKey("ai_state_id") && !_is_stringish(patch._get("ai_state_id")))
            return _fail("state_patch.ai_state_id must be StringName/String.");
        var blackboard_set = patch._get("blackboard_set", new GDictionary());
        if (blackboard_set.VariantType == Variant.Type.Dictionary)
        {
            var set_dict = blackboard_set.AsGodotDictionary();
            foreach (var key in set_dict.Keys)
            {
                if (!_is_allowed_blackboard_set_key(key.ToString()))
                    return _fail("state_patch.blackboard_set contains unsupported key " + key.ToString());
                if (!_is_stringish(set_dict[key]))
                    return _fail("state_patch.blackboard_set." + key.ToString() + " must be StringName/String.");
            }
        }
        else if (patch.ContainsKey("blackboard_set"))
        {
            return _fail("state_patch.blackboard_set must be Dictionary.");
        }
        var blackboard_increment = patch._get("blackboard_increment", new GDictionary());
        if (blackboard_increment.VariantType == Variant.Type.Dictionary)
        {
            var inc_dict = blackboard_increment.AsGodotDictionary();
            foreach (var key in inc_dict.Keys)
            {
                if (!_is_allowed_blackboard_increment_key(key.ToString()))
                    return _fail("state_patch.blackboard_increment contains unsupported key " + key.ToString());
                if (inc_dict[key].VariantType != Variant.Type.Int)
                    return _fail("state_patch.blackboard_increment." + key.ToString() + " must be int.");
            }
        }
        else if (patch.ContainsKey("blackboard_increment"))
        {
            return _fail("state_patch.blackboard_increment must be Dictionary.");
        }
        return true;
    }

    private static bool _is_stringish(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static bool _is_allowed_state_patch_key(string key)
    {
        return key == "ai_brain_id" || key == "ai_state_id" || key == "blackboard_set" || key == "blackboard_increment";
    }

    private static bool _is_allowed_blackboard_set_key(string key)
    {
        return key == "last_brain_id"
            || key == "last_state_id"
            || key == "last_action_id"
            || key == "last_reason_text"
            || key == "last_transition_previous_state_id"
            || key == "last_transition_state_id"
            || key == "last_transition_rule_id"
            || key == "last_transition_reason";
    }

    private static bool _is_allowed_blackboard_increment_key(string key)
    {
        return key == "turn_decision_count";
    }

    private static Variant _get(GDictionary dict, string key, Variant fallback = default)
    {
        return dict != null && dict.ContainsKey(key) ? dict[key] : fallback;
    }

    private static bool _fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(message, new GDictionary { ["source"] = "BattleAiDecisionCommitter" });
    }
}


using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiDecisionCommitter : RefCounted
{
    public void attach_state_patch(BattleAiDecision decision)
    {
        AttachStatePatch(decision);
    }

    public GDictionary build_state_patch(BattleAiDecision decision)
    {
        return BuildStatePatchDictionary(decision);
    }

    public static GDictionary BuildStatePatchDictionary(BattleAiDecision decision)
    {
        return BuildTypedStatePatch(decision).ToDictionary();
    }

    internal static DecisionStatePatch BuildTypedStatePatch(BattleAiDecision decision)
    {
        return DecisionStatePatch.FromDecision(decision);
    }

    internal static void AttachStatePatch(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return;
        }
        DecisionStatePatch patch = BuildTypedStatePatch(decision);
        decision.TypedStatePatch = patch;
        decision.state_patch = patch.ToDictionary();
    }

    public void commit(BattleUnitState unit_state, BattleAiDecision decision)
    {
        if (unit_state == null || decision == null)
            return;
        DecisionStatePatch patch = decision.TypedStatePatch;
        if (patch == null && decision.state_patch != null && decision.state_patch.Count > 0)
        {
            if (!DecisionStatePatch.TryFromDictionary(decision.state_patch, out patch, true))
            {
                return;
            }
        }
        else if (patch == null)
        {
            patch = DecisionStatePatch.FromDecision(decision);
        }
        if (patch == null)
        {
            return;
        }
        patch.ApplyTo(unit_state);
    }

    public bool validate_state_patch(GDictionary patch)
    {
        return DecisionStatePatch.TryFromDictionary(patch, out _, true);
    }

    internal sealed class DecisionStatePatch
    {
        private bool _hasBrainId;
        private StringName _brainId = "";
        private bool _hasStateId;
        private StringName _stateId = "";
        private bool _hasLastBrainId;
        private string _lastBrainId = "";
        private bool _hasLastStateId;
        private string _lastStateId = "";
        private bool _hasLastActionId;
        private string _lastActionId = "";
        private bool _hasLastReasonText;
        private string _lastReasonText = "";
        private bool _hasLastTransitionPreviousStateId;
        private string _lastTransitionPreviousStateId = "";
        private bool _hasLastTransitionStateId;
        private string _lastTransitionStateId = "";
        private bool _hasLastTransitionRuleId;
        private string _lastTransitionRuleId = "";
        private bool _hasLastTransitionReason;
        private string _lastTransitionReason = "";
        private bool _hasTurnDecisionCountIncrement;
        private int _turnDecisionCountIncrement;

        public static DecisionStatePatch FromDecision(BattleAiDecision decision)
        {
            DecisionStatePatch patch = new();
            if (decision == null)
            {
                return patch;
            }
            patch.SetBlackboardText("last_brain_id", decision.brain_id.ToString());
            patch.SetBlackboardText("last_state_id", decision.state_id.ToString());
            patch.SetBlackboardText("last_action_id", decision.action_id.ToString());
            patch.SetBlackboardText("last_reason_text", decision.reason_text);

            BattleAiStateResolver.TransitionResult transition = decision.TypedTransition;
            if (transition != null)
            {
                patch.SetBlackboardText(
                    "last_transition_previous_state_id",
                    transition.PreviousStateId.ToString()
                );
                patch.SetBlackboardText(
                    "last_transition_state_id",
                    transition.StateId.ToString()
                );
                patch.SetBlackboardText(
                    "last_transition_rule_id",
                    transition.RuleId.ToString()
                );
                patch.SetBlackboardText(
                    "last_transition_reason",
                    transition.Reason.ToString()
                );
            }
            else
            {
                GDictionary transitionDictionary = decision.transition;
                if (transitionDictionary != null && transitionDictionary.Count > 0)
                {
                    patch.SetBlackboardText(
                        "last_transition_previous_state_id",
                        GetStringLikeOrEmpty(transitionDictionary, "previous_state_id")
                    );
                    patch.SetBlackboardText(
                        "last_transition_state_id",
                        GetStringLikeOrEmpty(transitionDictionary, "state_id")
                    );
                    patch.SetBlackboardText(
                        "last_transition_rule_id",
                        GetStringLikeOrEmpty(transitionDictionary, "rule_id")
                    );
                    patch.SetBlackboardText(
                        "last_transition_reason",
                        GetStringLikeOrEmpty(transitionDictionary, "reason")
                    );
                }
            }

            patch._hasTurnDecisionCountIncrement = true;
            patch._turnDecisionCountIncrement = 1;
            if (decision.brain_id != "")
            {
                patch._hasBrainId = true;
                patch._brainId = decision.brain_id;
            }
            if (decision.state_id != "")
            {
                patch._hasStateId = true;
                patch._stateId = decision.state_id;
            }
            return patch;
        }

        public static bool TryFromDictionary(
            GDictionary source,
            out DecisionStatePatch patch,
            bool failLoud
        )
        {
            patch = null;
            if (source == null)
            {
                return Fail(failLoud, "BattleAiDecision.state_patch must be Dictionary.");
            }
            if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(source, "BattleAiDecision.state_patch"))
            {
                return false;
            }

            DecisionStatePatch parsed = new();
            foreach (string key in ReadDictionaryKeys(source))
            {
                if (!IsAllowedStatePatchKey(key))
                {
                    return Fail(
                        failLoud,
                        "BattleAiDecision.state_patch contains unsupported key " + key
                    );
                }
            }

            if (TryGetDictionaryValue(source, "ai_brain_id", out Variant rawBrainId))
            {
                if (!TryAsStringName(rawBrainId, out parsed._brainId))
                {
                    return Fail(failLoud, "state_patch.ai_brain_id must be StringName/String.");
                }
                parsed._hasBrainId = true;
            }
            if (TryGetDictionaryValue(source, "ai_state_id", out Variant rawStateId))
            {
                if (!TryAsStringName(rawStateId, out parsed._stateId))
                {
                    return Fail(failLoud, "state_patch.ai_state_id must be StringName/String.");
                }
                parsed._hasStateId = true;
            }
            if (TryGetDictionaryValue(source, "blackboard_set", out Variant rawBlackboardSet))
            {
                if (!TryAsDictionary(rawBlackboardSet, out GDictionary setDictionary))
                {
                    return Fail(failLoud, "state_patch.blackboard_set must be Dictionary.");
                }
                if (!parsed.ParseBlackboardSet(setDictionary, failLoud))
                {
                    return false;
                }
            }
            if (TryGetDictionaryValue(source, "blackboard_increment", out Variant rawIncrement))
            {
                if (!TryAsDictionary(rawIncrement, out GDictionary incrementDictionary))
                {
                    return Fail(failLoud, "state_patch.blackboard_increment must be Dictionary.");
                }
                if (!parsed.ParseBlackboardIncrement(incrementDictionary, failLoud))
                {
                    return false;
                }
            }

            patch = parsed;
            return true;
        }

        public GDictionary ToDictionary()
        {
            GDictionary patch = new();
            GDictionary blackboardSet = new();
            AddBlackboardText(blackboardSet, "last_brain_id", _hasLastBrainId, _lastBrainId);
            AddBlackboardText(blackboardSet, "last_state_id", _hasLastStateId, _lastStateId);
            AddBlackboardText(blackboardSet, "last_action_id", _hasLastActionId, _lastActionId);
            AddBlackboardText(
                blackboardSet,
                "last_reason_text",
                _hasLastReasonText,
                _lastReasonText
            );
            AddBlackboardText(
                blackboardSet,
                "last_transition_previous_state_id",
                _hasLastTransitionPreviousStateId,
                _lastTransitionPreviousStateId
            );
            AddBlackboardText(
                blackboardSet,
                "last_transition_state_id",
                _hasLastTransitionStateId,
                _lastTransitionStateId
            );
            AddBlackboardText(
                blackboardSet,
                "last_transition_rule_id",
                _hasLastTransitionRuleId,
                _lastTransitionRuleId
            );
            AddBlackboardText(
                blackboardSet,
                "last_transition_reason",
                _hasLastTransitionReason,
                _lastTransitionReason
            );
            if (blackboardSet.Count > 0)
            {
                patch["blackboard_set"] = blackboardSet;
            }
            if (_hasTurnDecisionCountIncrement)
            {
                patch["blackboard_increment"] = new GDictionary
                {
                    ["turn_decision_count"] = _turnDecisionCountIncrement,
                };
            }
            if (_hasBrainId)
            {
                patch["ai_brain_id"] = _brainId;
            }
            if (_hasStateId)
            {
                patch["ai_state_id"] = _stateId;
            }
            return patch;
        }

        private static void AddBlackboardText(
            GDictionary blackboardSet,
            string key,
            bool hasValue,
            string value
        )
        {
            if (!hasValue || blackboardSet == null)
                return;
            blackboardSet[key] = value ?? "";
        }

        public void ApplyTo(BattleUnitState unitState)
        {
            if (unitState == null)
            {
                return;
            }
            if (_hasBrainId)
            {
                unitState.ai_brain_id = _brainId;
            }
            if (_hasStateId)
            {
                unitState.ai_state_id = _stateId;
            }
            if (_hasLastBrainId)
                unitState.ai_blackboard.last_brain_id = new StringName(_lastBrainId);
            if (_hasLastStateId)
                unitState.ai_blackboard.last_state_id = new StringName(_lastStateId);
            if (_hasLastActionId)
                unitState.ai_blackboard.last_action_id = new StringName(_lastActionId);
            if (_hasLastReasonText)
                unitState.ai_blackboard.last_reason_text = new StringName(_lastReasonText);
            if (_hasLastTransitionPreviousStateId)
                unitState.ai_blackboard.last_transition_previous_state_id = new StringName(_lastTransitionPreviousStateId);
            if (_hasLastTransitionStateId)
                unitState.ai_blackboard.last_transition_state_id = new StringName(_lastTransitionStateId);
            if (_hasLastTransitionRuleId)
                unitState.ai_blackboard.last_transition_rule_id = new StringName(_lastTransitionRuleId);
            if (_hasLastTransitionReason)
                unitState.ai_blackboard.last_transition_reason = new StringName(_lastTransitionReason);
            if (_hasTurnDecisionCountIncrement)
            {
                unitState.ai_blackboard.turn_decision_count += _turnDecisionCountIncrement;
            }
        }

        private bool ParseBlackboardSet(GDictionary setDictionary, bool failLoud)
        {
            if (
                !TryReadTextPatchEntries(
                    setDictionary,
                    "state_patch.blackboard_set",
                    IsAllowedBlackboardSetKey,
                    out List<TextPatchEntry> entries,
                    out string error
                )
            )
            {
                return Fail(failLoud, error);
            }
            foreach (TextPatchEntry entry in entries)
            {
                SetBlackboardText(entry.Key, entry.Value);
            }
            return true;
        }

        private bool ParseBlackboardIncrement(GDictionary incrementDictionary, bool failLoud)
        {
            if (
                !TryReadIntPatchEntries(
                    incrementDictionary,
                    "state_patch.blackboard_increment",
                    IsAllowedBlackboardIncrementKey,
                    out List<IntPatchEntry> entries,
                    out string error
                )
            )
            {
                return Fail(failLoud, error);
            }
            foreach (IntPatchEntry entry in entries)
            {
                _hasTurnDecisionCountIncrement = true;
                _turnDecisionCountIncrement = entry.Value;
            }
            return true;
        }

        private void SetBlackboardText(string key, string value)
        {
            switch (key)
            {
                case "last_brain_id":
                    _hasLastBrainId = true;
                    _lastBrainId = value ?? "";
                    break;
                case "last_state_id":
                    _hasLastStateId = true;
                    _lastStateId = value ?? "";
                    break;
                case "last_action_id":
                    _hasLastActionId = true;
                    _lastActionId = value ?? "";
                    break;
                case "last_reason_text":
                    _hasLastReasonText = true;
                    _lastReasonText = value ?? "";
                    break;
                case "last_transition_previous_state_id":
                    _hasLastTransitionPreviousStateId = true;
                    _lastTransitionPreviousStateId = value ?? "";
                    break;
                case "last_transition_state_id":
                    _hasLastTransitionStateId = true;
                    _lastTransitionStateId = value ?? "";
                    break;
                case "last_transition_rule_id":
                    _hasLastTransitionRuleId = true;
                    _lastTransitionRuleId = value ?? "";
                    break;
                case "last_transition_reason":
                    _hasLastTransitionReason = true;
                    _lastTransitionReason = value ?? "";
                    break;
            }
        }

    }

    private static bool _fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new GDictionary { ["source"] = "BattleAiDecisionCommitter" }
        );
    }

    private readonly struct TextPatchEntry
    {
        public TextPatchEntry(string key, string value)
        {
            Key = key ?? "";
            Value = value ?? "";
        }

        public string Key { get; }

        public string Value { get; }
    }

    private readonly struct IntPatchEntry
    {
        public IntPatchEntry(string key, int value)
        {
            Key = key ?? "";
            Value = value;
        }

        public string Key { get; }

        public int Value { get; }
    }

    private static List<string> ReadDictionaryKeys(GDictionary source)
    {
        var result = new List<string>();
        if (source == null)
        {
            return result;
        }
        foreach (var rawKey in source.Keys)
        {
            result.Add(ReadKeyText(rawKey));
        }
        return result;
    }

    private static bool TryReadTextPatchEntries(
        GDictionary source,
        string path,
        System.Func<string, bool> isAllowedKey,
        out List<TextPatchEntry> entries,
        out string error
    )
    {
        entries = new List<TextPatchEntry>();
        error = "";
        if (source == null)
        {
            return true;
        }
        foreach (var rawKey in source.Keys)
        {
            string key = ReadKeyText(rawKey);
            if (isAllowedKey == null || !isAllowedKey(key))
            {
                error = $"{path} contains unsupported key {key}";
                return false;
            }
            if (
                !TryGetDictionaryValue(source, rawKey, out Variant rawValue)
                || !TryAsString(rawValue, out string value)
            )
            {
                error = $"{path}.{key} must be StringName/String.";
                return false;
            }
            entries.Add(new TextPatchEntry(key, value));
        }
        return true;
    }

    private static bool TryReadIntPatchEntries(
        GDictionary source,
        string path,
        System.Func<string, bool> isAllowedKey,
        out List<IntPatchEntry> entries,
        out string error
    )
    {
        entries = new List<IntPatchEntry>();
        error = "";
        if (source == null)
        {
            return true;
        }
        foreach (var rawKey in source.Keys)
        {
            string key = ReadKeyText(rawKey);
            if (isAllowedKey == null || !isAllowedKey(key))
            {
                error = $"{path} contains unsupported key {key}";
                return false;
            }
            if (
                !TryGetDictionaryValue(source, rawKey, out Variant rawValue)
                || !TryAsInt(rawValue, out int value)
            )
            {
                error = $"{path}.{key} must be int.";
                return false;
            }
            entries.Add(new IntPatchEntry(key, value));
        }
        return true;
    }

    private static bool IsAllowedStatePatchKey(string key)
    {
        return key == "ai_brain_id"
            || key == "ai_state_id"
            || key == "blackboard_set"
            || key == "blackboard_increment";
    }

    private static bool IsAllowedBlackboardSetKey(string key)
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

    private static bool IsAllowedBlackboardIncrementKey(string key)
    {
        return key == "turn_decision_count";
    }

    private static bool Fail(bool failLoud, string message)
    {
        return failLoud ? _fail(message) : false;
    }

    private static string GetStringLikeOrEmpty(GDictionary dictionary, string key)
    {
        return TryGetDictionaryValue(dictionary, key, out Variant rawValue)
            && TryAsString(rawValue, out string value)
                ? value
                : "";
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, string key, out Variant value)
    {
        if (dictionary == null || key == null)
        {
            value = default;
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        StringName stringNameKey = new(key);
        if (dictionary.ContainsKey(stringNameKey))
        {
            value = dictionary[stringNameKey];
            return true;
        }
        value = default;
        return false;
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, Variant key, out Variant value)
    {
        if (dictionary == null)
        {
            value = default;
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        value = default;
        return false;
    }

    private static bool TryAsDictionary(Variant rawValue, out GDictionary value)
    {
        if (rawValue.VariantType == Variant.Type.Dictionary)
        {
            value = rawValue.AsGodotDictionary();
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsStringName(Variant rawValue, out StringName value)
    {
        if (TryAsString(rawValue, out string text))
        {
            value = new StringName(text);
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsString(Variant rawValue, out string value)
    {
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = rawValue.AsString();
            return true;
        }
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName().ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsInt(Variant rawValue, out int value)
    {
        if (rawValue.VariantType == Variant.Type.Int)
        {
            value = rawValue.AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static string ReadKeyText(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.String => rawKey.AsString(),
            Variant.Type.StringName => rawKey.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => rawKey.ToString(),
        };
    }
}

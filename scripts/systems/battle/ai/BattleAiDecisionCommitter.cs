using Godot;

public static class BattleAiDecisionCommitter
{
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
        decision.StatePatch = patch;
    }

    public static void Commit(BattleUnitState unitState, BattleAiDecision decision)
    {
        if (unitState == null || decision == null)
            return;
        DecisionStatePatch patch = decision.StatePatch;
        if (patch == null)
        {
            patch = DecisionStatePatch.FromDecision(decision);
            decision.StatePatch = patch;
        }
        patch.ApplyTo(unitState);
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

            BattleAiStateResolver.TransitionResult transition = decision.Transition;
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
                unitState.ai_blackboard.set_int(
                    "turn_decision_count",
                    unitState.ai_blackboard.turn_decision_count + _turnDecisionCountIncrement
                );
            }
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

}

using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiWaitActionEvaluator
{
    private const int TuGranularity = 5;
    private const int StaminaRecoveryProgressBase = 11;
    private const int StaminaRecoveryProgressDenominator = 10;
    private const int StaminaRestingRecoveryMultiplier = 2;

    private readonly BattleAiTypedActionHelper _helper = new();

    private sealed class ActiveRestProfile
    {
        internal bool Active;
        internal bool WillRest;
        internal int CurrentStamina;
        internal int ProjectedRestStamina;
        internal int DesiredStamina;
        internal int StaminaMax;
    }

    internal BattleAiDecision Evaluate(WaitActionDefinition action, BattleAiContext context)
    {
        if (action == null || context?.unit_state == null)
            return null;

        AiTraceRecorder.Enter("decide:wait");
        try
        {
            return EvaluateImpl(action, context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:wait");
        }
    }

    private BattleAiDecision EvaluateImpl(WaitActionDefinition action, BattleAiContext context)
    {
        ActiveRestProfile rest = BuildActiveRestProfile(action, context);
        AiActionTrace trace = context?.trace_enabled == true
            ? EnemyAiActionHelper.BeginActionTrace(
                action.ActionId,
                action.ScoreBucketId,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "wait",
                    ["active_rest"] = rest.Active,
                    ["will_rest"] = rest.WillRest,
                    ["current_stamina"] = rest.CurrentStamina,
                    ["projected_rest_stamina"] = rest.ProjectedRestStamina,
                    ["desired_stamina"] = rest.DesiredStamina,
                }
            )
            : null;
        BattleCommand command = EnemyAiActionHelper.BuildWaitCommand(context);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["position_objective_kind"] = "none",
        };
        if (rest.Active)
        {
            metadata["action_base_score"] = action.ActiveRestActionBaseScore;
            metadata["active_rest"] = true;
        }
        BattleAiScoreInput scoreInput = context.BuildActionScoreInputTyped(
            "wait",
            action.ActionId.ToString(),
            action.ScoreBucketId,
            command,
            null,
            metadata
        );
        BattleUnitState unit = context.unit_state;
        string reason = $"{unit.display_name} 没有更优动作，选择待机。";
        if (rest.Active)
        {
            reason =
                $"{unit.display_name} 体力不足，选择主动休息以恢复到 {rest.ProjectedRestStamina}/{rest.StaminaMax}。";
        }
        else if (rest.WillRest)
        {
            reason = $"{unit.display_name} 没有更优动作，选择休息恢复体力。";
        }
        BattleAiDecision decision = EnemyAiActionHelper.CreateScoredDecision(
            action.ActionId,
            action.ScoreBucketId,
            command,
            scoreInput,
            reason
        );
        if (trace != null)
        {
            EnemyAiActionHelper.TraceOfferCandidate(
                trace,
                EnemyAiActionHelper.BuildCandidateSummary("wait", command, scoreInput)
            );
        }
        EnemyAiActionHelper.FinalizeActionTrace(context, trace, decision);
        return decision;
    }

    private ActiveRestProfile BuildActiveRestProfile(
        WaitActionDefinition action,
        BattleAiContext context
    )
    {
        var profile = new ActiveRestProfile();
        BattleUnitState unit = context?.unit_state;
        if (unit == null)
            return profile;

        int staminaMax = GetUnitStaminaMax(unit);
        int currentStamina = Mathf.Max(unit.GetCurrentStamina(), 0);
        profile.CurrentStamina = currentStamina;
        profile.StaminaMax = staminaMax;
        profile.WillRest = WillWaitTriggerRest(unit, currentStamina, staminaMax);
        if (
            staminaMax <= 0
            || currentStamina >= staminaMax
            || unit.HasTakenActionThisTurnTyped()
            || HasAffordableLegalHostileSkill(context)
        )
        {
            profile.ProjectedRestStamina = currentStamina;
            return profile;
        }

        int desiredStamina = ResolveDesiredRestStamina(action, context);
        profile.DesiredStamina = desiredStamina;
        if (desiredStamina <= 0 || currentStamina >= desiredStamina)
        {
            profile.ProjectedRestStamina = currentStamina;
            return profile;
        }
        int projectedStamina = Mathf.Min(
            currentStamina
                + EstimateRestingRecovery(unit, ResolveActionThresholdTu(unit)),
            staminaMax
        );
        profile.ProjectedRestStamina = projectedStamina;
        profile.Active = projectedStamina >= desiredStamina;
        return profile;
    }

    private static bool WillWaitTriggerRest(BattleUnitState unit, int stamina, int staminaMax) =>
        unit != null
        && !unit.HasTakenActionThisTurnTyped()
        && staminaMax > 0
        && stamina < staminaMax;

    private bool HasAffordableLegalHostileSkill(BattleAiContext context)
    {
        if (context?.unit_state == null)
            return false;
        foreach (
            BattleAvailableSkillEntry entry in _helper.ResolveAvailableSkillEntries(
                context,
                Array.Empty<StringName>()
            )
        )
        {
            SkillDefinition skill = _helper.GetSkillDefinition(context, entry);
            if (
                skill?.CombatProfile == null
                || !BattleAiTypedActionHelper.IsHostileThreatSkill(skill)
                || BattleSkillCastBlockReasonKinds.IsBlocked(
                    _helper.GetSkillCastBlockReason(context, skill)
                )
            )
            {
                continue;
            }
            if (HasLegalUnitSkillTarget(context, entry, skill))
                return true;
        }
        return false;
    }

    private bool HasLegalUnitSkillTarget(
        BattleAiContext context,
        BattleAvailableSkillEntry entry,
        SkillDefinition skill
    )
    {
        if (entry == null || skill?.CombatProfile?.TargetModeKind != BattleTargetMode.Unit)
            return false;
        foreach (BattleUnitState target in _helper.SortTargetUnits(context, "enemy", "nearest_enemy"))
        {
            BattleCommand command = _helper.BuildUnitSkillCommand(context, entry, target, "");
            BattlePreview preview = BattleAiUnitSkillCandidateEvaluator.BuildFastUnitSkillPreview(
                context,
                skill,
                command,
                target,
                out _
            );
            if (preview?.allowed == true)
                return true;
        }
        return false;
    }

    private int ResolveDesiredRestStamina(WaitActionDefinition action, BattleAiContext context)
    {
        int desiredCost = GetSkillStaminaCost(context, "basic_attack");
        foreach (
            BattleAvailableSkillEntry entry in _helper.ResolveAvailableSkillEntries(
                context,
                Array.Empty<StringName>()
            )
        )
        {
            SkillDefinition skill = _helper.GetSkillDefinition(context, entry);
            if (skill?.CombatProfile == null || !BattleAiTypedActionHelper.IsHostileThreatSkill(skill))
                continue;
            int skillCost = GetSkillStaminaCost(context, entry, skill);
            if (skillCost <= 0)
                continue;
            desiredCost = desiredCost <= 0 ? skillCost : Mathf.Min(desiredCost, skillCost);
        }
        return desiredCost <= 0 ? 0 : desiredCost + action.ActiveRestMinStaminaResidue;
    }

    private int GetSkillStaminaCost(BattleAiContext context, StringName skillId)
    {
        SkillDefinition skill = _helper.GetSkillDefinition(context, skillId);
        if (skill?.CombatProfile == null)
            return 0;
        int skillLevel = context?.unit_state != null
            ? GetSkillLevel(context.unit_state, skillId)
            : 1;
        SkillEffectiveCombatDefinition effective =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillId, Mathf.Max(skillLevel, 1))
            ?? SkillEffectiveCombatDefinition.BuildUncached(skill, Mathf.Max(skillLevel, 1));
        return Mathf.Max(effective.ResourceCosts.StaminaCost, 0);
    }

    private static int GetSkillStaminaCost(
        BattleAiContext context,
        BattleAvailableSkillEntry entry,
        SkillDefinition skill
    )
    {
        if (entry == null || skill?.CombatProfile == null)
            return 0;
        int skillLevel = Mathf.Max(entry.SkillLevel, 1);
        SkillEffectiveCombatDefinition effective =
            context?.skill_catalog?.GetEffectiveCombatDefinition(
                entry.EntryRef.SkillId,
                skillLevel
            ) ?? SkillEffectiveCombatDefinition.BuildUncached(skill, skillLevel);
        return Mathf.Max(effective.ResourceCosts.StaminaCost, 0);
    }

    private static int EstimateRestingRecovery(BattleUnitState unit, int tuDelta)
    {
        if (unit == null || tuDelta <= 0)
            return 0;
        int tickCount = Mathf.Max(tuDelta / TuGranularity, 0);
        if (tickCount <= 0)
            return 0;
        int progressPerTick = StaminaRecoveryProgressBase + GetUnitConstitution(unit);
        progressPerTick = ApplyStaminaRecoveryPercentBonus(unit, progressPerTick);
        progressPerTick *= StaminaRestingRecoveryMultiplier;
        int progress = Mathf.Max(unit.GetStaminaRecoveryProgressTyped(), 0);
        int recovered = 0;
        for (int index = 0; index < tickCount; index++)
        {
            progress += progressPerTick;
            recovered += progress / StaminaRecoveryProgressDenominator;
            progress %= StaminaRecoveryProgressDenominator;
        }
        return recovered;
    }

    private static int ResolveActionThresholdTu(BattleUnitState unit) =>
        unit != null ? Mathf.Max(unit.GetActionThresholdTyped(), 1) : 30;

    private static int GetUnitConstitution(BattleUnitState unit) =>
        unit?.attribute_snapshot != null
            ? Mathf.Max(
                unit.attribute_snapshot.GetValue(
                    UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)
                ),
                0
            )
            : 0;

    private static int GetUnitStaminaMax(BattleUnitState unit) =>
        unit?.attribute_snapshot != null
            ? Mathf.Max(
                unit.attribute_snapshot.GetValue(
                    AttributeService.ToStringName(AttributeIdKind.StaminaMax)
                ),
                0
            )
            : 0;

    private static int ApplyStaminaRecoveryPercentBonus(BattleUnitState unit, int baseProgress)
    {
        if (unit?.attribute_snapshot == null)
            return baseProgress;
        int percentBonus = Mathf.Max(
            unit.attribute_snapshot.GetValue(
                AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus)
            ),
            0
        );
        return percentBonus <= 0
            ? baseProgress
            : Mathf.FloorToInt(baseProgress * (100f + percentBonus) / 100f);
    }

    private static int GetSkillLevel(BattleUnitState unit, StringName skillId)
    {
        if (unit == null || skillId == "")
            return 0;
        int knownLevel = unit.GetKnownSkillLevelTyped(skillId);
        return knownLevel > 0
            ? knownLevel
            : unit.KnowsActiveSkill(skillId)
                ? 1
                : 0;
    }
}

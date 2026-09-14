using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum PromotionFailureKind
{
    None,
    InvalidRequest,
    AlreadyUsed,
    NotEligible,
    SelectionChanged,
    InvalidHistory,
}

internal sealed class PreparedPromotion
{
    public bool Ok => Failure == PromotionFailureKind.None;
    public PromotionFailureKind Failure { get; }
    public UnitProgress Candidate { get; }
    public IReadOnlyList<AttributeGrowthResult> AttributeChanges { get; }

    public PreparedPromotion(PromotionFailureKind failure, UnitProgress candidate = null,
        IReadOnlyList<AttributeGrowthResult> changes = null)
    {
        Failure = failure;
        Candidate = candidate;
        AttributeChanges = changes ?? Array.Empty<AttributeGrowthResult>();
    }
}

public sealed partial class ProgressionService
{
    private bool CanApplyPromotionSelection(StringName professionId, PromotionCommitRequest request)
    {
        if (!request.IsWellFormed || !request.IncludesSkill(request.GrowthTriggerSkillId)) return false;
        var trigger = _unit_progress.GetSkillProgress(request.GrowthTriggerSkillId);
        if (trigger == null || (trigger.assigned_profession_id != "" && trigger.assigned_profession_id != professionId))
            return false;
        HashSet<StringName> professionCores = new(GetProfessionProgress(professionId)?.core_skill_ids ?? new StringNameList());
        foreach (StringName id in request.AssignedCoreSkillIds)
        {
            var skill = _unit_progress.GetSkillProgress(id);
            if (skill == null || (!skill.is_core && id != request.GrowthTriggerSkillId)
                || (skill.assigned_profession_id != "" && skill.assigned_profession_id != professionId))
                return false;
            professionCores.Add(id);
        }
        int coreCount = 0;
        foreach (StringName id in _unit_progress.GetSortedSkillIdsTyped())
        {
            var skill = _unit_progress.GetSkillProgress(id);
            if (skill.is_learned && (skill.is_core || id == request.GrowthTriggerSkillId)) coreCount++;
        }
        return professionCores.Count <= request.TargetRank && coreCount <= _unit_progress.character_level + 1;
    }

    public bool CanPromoteProfession(StringName professionId)
    {
        foreach (PendingProfessionChoice offer in BuildPendingProfessionChoices())
            if (offer.CandidateProfessionIdsTyped.Contains(professionId)) return true;
        return false;
    }

    internal PreparedPromotion PreparePromotion(StringName professionId, PromotionCommitRequest request)
    {
        if (_unit_progress == null || request?.IsWellFormed != true)
            return new(PromotionFailureKind.InvalidRequest);
        if (!_unit_progress.HasValidPromotionHistory())
            return new(PromotionFailureKind.InvalidHistory);
        if (_unit_progress.HasUsedGrowthTrigger(request.GrowthTriggerSkillId))
            return new(PromotionFailureKind.AlreadyUsed);
        SkillDefinition triggerDefinition = GetSkillDefinition(request.GrowthTriggerSkillId);
        if (!PromotionEligibilityRules.IsReadyTrigger(triggerDefinition,
                _unit_progress.GetSkillProgress(request.GrowthTriggerSkillId), _unit_progress)
            || !_rule_service.EvaluatePromotionPrerequisites(professionId, request.TargetRank))
            return new(PromotionFailureKind.NotEligible);

        PromotionCommitRequest resolved = ResolvePromotionSelection(professionId, request.TargetRank,
            request.TargetRank == 1, new PromotionSelectionDraft(request.AssignedCoreSkillIds, request.QualifierSkillIds),
            request.GrowthTriggerSkillId);
        if (resolved == null || !request.HasSameSkills(resolved) || !request.IncludesSkill(request.GrowthTriggerSkillId))
            return new(PromotionFailureKind.SelectionChanged);
        foreach (var growth in triggerDefinition.AttributeGrowthProgress)
            if (!AttributeGrowthService.IsValidAttributeId(growth.Key) || growth.Value <= 0)
                return new(PromotionFailureKind.InvalidRequest);

        UnitProgress candidate = _unit_progress.DuplicateState();
        var staged = new ProgressionService();
        staged.SetupDefinitions(candidate, _skill_definitions, _profession_defs);
        UnitSkillProgress trigger = candidate.GetSkillProgress(request.GrowthTriggerSkillId);
        trigger.is_core = true;
        candidate.SetSkillProgress(trigger);
        UnitProfessionProgress profession = candidate.GetProfessionProgress(professionId);
        if (profession == null)
        {
            profession = new UnitProfessionProgress { profession_id = professionId };
            candidate.SetProfessionProgress(profession);
        }
        foreach (StringName skillId in request.AssignedCoreSkillIds)
            if (!staged._assignment_service.AssignCoreSkillToProfession(skillId, professionId))
                return new(PromotionFailureKind.SelectionChanged);
        candidate.SyncActiveCoreSkillIds();
        if (profession.core_skill_ids.Count > request.TargetRank
            || candidate.ActiveCoreSkillIdsTyped.Count > candidate.character_level + 1)
            return new(PromotionFailureKind.NotEligible);

        var record = new ProfessionPromotionRecord
        {
            new_rank = request.TargetRank,
            growth_trigger_skill_id = request.GrowthTriggerSkillId,
            growth_trigger_level = trigger.skill_level,
            consumed_skill_ids = new StringNameList(request.AssignedCoreSkillIds),
            qualifier_skill_ids = new StringNameList(request.QualifierSkillIds),
            snapshot_unit_base_attributes = candidate.unit_base_attributes.DuplicateState(),
            timestamp = (int)Time.GetUnixTimeFromSystem(),
        };
        if (!candidate.TryAppendPromotionRecord(professionId, record))
            return new(PromotionFailureKind.InvalidHistory);

        // All rejectable selection checks precede randomness and growth application.
        ProfessionDefinition definition = GetProfessionDef(professionId);
        staged.ApplyProfessionHitPointGain(definition);
        staged.GrantProfessionSkills(definition, profession, request.TargetRank);
        var growthService = new AttributeGrowthService();
        growthService.Setup(candidate);
        List<AttributeGrowthResult> changes = new();
        foreach (var growth in triggerDefinition.AttributeGrowthProgress)
            changes.Add(growthService.ApplyAttributeProgressTyped(growth.Key, growth.Value,
                $"{triggerDefinition.DisplayName} 晋升成长"));
        staged.RefreshRuntimeState();
        if (!candidate.HasValidPromotionHistory())
            throw new InvalidOperationException("Prepared promotion violated its history invariant.");
        return new(PromotionFailureKind.None, candidate, changes.AsReadOnly());
    }
}

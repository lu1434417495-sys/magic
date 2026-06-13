using System.Collections.Generic;
using Godot;

public sealed class ProfessionAssignmentService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();
    private readonly Dictionary<StringName, ProfessionDef> _professionDefs = new();

    public void Setup(
        UnitProgress unit_progress,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs,
        IReadOnlyDictionary<StringName, ProfessionDef> profession_defs
    )
    {
        _unit_progress = unit_progress;
        _skillDefs.Clear();
        _professionDefs.Clear();

        if (skill_defs != null)
        {
            foreach (KeyValuePair<StringName, SkillDef> pair in skill_defs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _skillDefs[pair.Key] = pair.Value;
            }
        }

        if (profession_defs != null)
        {
            foreach (KeyValuePair<StringName, ProfessionDef> pair in profession_defs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _professionDefs[pair.Key] = pair.Value;
            }
        }
    }

    public bool CanAssignCoreSkillToProfession(StringName skill_id, StringName profession_id)
    {
        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        SkillDef skillDef = GetSkillDef(skill_id);
        if (skillProgress == null || professionProgress == null || skillDef == null)
            return false;
        if (!skillProgress.is_learned)
            return false;
        if (!skillProgress.is_core)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDef,
                skillProgress,
                _unit_progress
            )
        )
            return false;

        StringName assignedProfessionId = skillProgress.assigned_profession_id;
        if (assignedProfessionId != "" && assignedProfessionId != profession_id)
            return false;
        return true;
    }

    public bool AssignCoreSkillToProfession(StringName skill_id, StringName profession_id)
    {
        if (!CanAssignCoreSkillToProfession(skill_id, profession_id))
            return false;

        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        RemoveSkillFromAllProfessions(skill_id, profession_id);
        skillProgress.assigned_profession_id = profession_id;
        professionProgress.AddCoreSkill(skill_id);
        _unit_progress.SyncActiveCoreSkillIds();
        return true;
    }

    public bool RemoveCoreSkillFromProfession(StringName skill_id, StringName profession_id)
    {
        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        if (professionProgress == null)
            return false;

        if (!professionProgress.core_skill_ids.Contains(skill_id))
            return false;

        professionProgress.RemoveCoreSkill(skill_id);
        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        if (skillProgress != null && skillProgress.assigned_profession_id == profession_id)
            skillProgress.ClearProfessionAssignment();

        _unit_progress.SyncActiveCoreSkillIds();
        return true;
    }

    public bool CanPromoteNonCoreToCore(StringName skill_id, StringName profession_id)
    {
        if (_unit_progress == null)
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        ProfessionDef professionDef = GetProfessionDef(profession_id);
        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        SkillDef skillDef = GetSkillDef(skill_id);
        if (
            professionProgress == null
            || professionDef == null
            || skillProgress == null
            || skillDef == null
        )
            return false;

        int currentCharacterLevel = GetEffectiveCharacterLevel();
        _unit_progress.SyncActiveCoreSkillIds();
        if (_unit_progress.ActiveCoreSkillIdsTyped.Count >= currentCharacterLevel)
            return false;
        if (
            professionProgress.core_skill_ids.Count >= professionProgress.rank
        )
            return false;
        if (professionProgress.rank <= 0)
            return false;
        if (!skillProgress.is_learned)
            return false;
        if (skillProgress.is_core)
            return false;
        if (skillProgress.assigned_profession_id != "")
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDef,
                skillProgress,
                _unit_progress
            )
        )
            return false;

        List<StringName> acceptedTags = GetProfessionAcceptedTags(professionDef);
        if (acceptedTags.Count == 0)
            return false;

        foreach (StringName rawTag in skillDef.TagsTyped)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawTag);
            if (acceptedTags.Contains(tag))
                return true;
        }
        return false;
    }

    public bool PromoteNonCoreToCore(StringName skill_id, StringName profession_id)
    {
        if (!CanPromoteNonCoreToCore(skill_id, profession_id))
            return false;

        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        if (skillProgress == null)
            return false;

        skillProgress.is_core = true;
        skillProgress.assigned_profession_id = profession_id;

        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        professionProgress.AddCoreSkill(skill_id);
        _unit_progress.SyncActiveCoreSkillIds();
        return true;
    }

    public IReadOnlyList<StringName> GetProfessionCoreSkillIds(StringName profession_id)
    {
        UnitProfessionProgress professionProgress = GetProfessionProgress(profession_id);
        if (professionProgress == null)
            return new List<StringName>();

        List<StringName> result = new();
        foreach (var rawSkillId in professionProgress.core_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId != "")
                result.Add(skillId);
        }
        return result;
    }

    public StringName GetSkillAssignedProfession(StringName skill_id)
    {
        UnitSkillProgress skillProgress = GetSkillProgress(skill_id);
        return skillProgress == null
            ? new StringName("")
            : skillProgress.assigned_profession_id;
    }

    private UnitSkillProgress GetSkillProgress(StringName skillId)
    {
        return _unit_progress?.GetSkillProgress(skillId);
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        return _unit_progress?.GetProfessionProgress(professionId);
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skillDefs.TryGetValue(skillId, out SkillDef skillDef) ? skillDef : null;
    }

    private ProfessionDef GetProfessionDef(StringName professionId)
    {
        return _professionDefs.TryGetValue(professionId, out ProfessionDef professionDef)
            ? professionDef
            : null;
    }

    private void RemoveSkillFromAllProfessions(
        StringName skillId,
        StringName exceptProfessionId = default
    )
    {
        if (_unit_progress == null)
            return;

        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            if (exceptProfessionId != "" && professionId == exceptProfessionId)
                continue;

            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            professionProgress?.RemoveCoreSkill(skillId);
        }
    }

    private static List<StringName> GetProfessionAcceptedTags(
        ProfessionDef professionDef
    )
    {
        List<StringName> acceptedTags = new();
        HashSet<StringName> seenTags = new();
        if (professionDef == null)
            return acceptedTags;

        if (professionDef.unlock_requirement != null)
            AppendTagRules(
                acceptedTags,
                seenTags,
                professionDef.unlock_requirement.required_tag_rules
            );

        foreach (ProfessionRankRequirement rankRequirement in professionDef.rank_requirements)
        {
            if (rankRequirement != null)
                AppendTagRules(acceptedTags, seenTags, rankRequirement.required_tag_rules);
        }
        return acceptedTags;
    }

    private static void AppendTagRules(
        List<StringName> acceptedTags,
        HashSet<StringName> seenTags,
        IEnumerable<TagRequirement> tagRules
    )
    {
        if (tagRules == null)
            return;

        foreach (TagRequirement tagRule in tagRules)
        {
            if (
                tagRule == null
                || tagRule.tag == ""
                || !seenTags.Add(tagRule.tag)
            )
                continue;
            acceptedTags.Add(tagRule.tag);
        }
    }

    private int GetEffectiveCharacterLevel()
    {
        if (_unit_progress == null)
            return 0;

        int rankTotal = 0;
        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            if (professionProgress != null)
                rankTotal += professionProgress.rank;
        }
        return rankTotal;
    }
}

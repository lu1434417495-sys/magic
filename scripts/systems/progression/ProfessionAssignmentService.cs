using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class ProfessionAssignmentService : RefCounted
{
    private GodotObject _unit_progress;
    private GDictionary _skill_defs = new();
    private GDictionary _profession_defs = new();

    public void setup(GodotObject unit_progress, GDictionary skill_defs, GDictionary profession_defs)
    {
        _unit_progress = unit_progress;
        _skill_defs = IndexSkillDefs(skill_defs);
        _profession_defs = IndexProfessionDefs(profession_defs);
    }

    public bool can_assign_core_skill_to_profession(StringName skill_id, StringName profession_id)
    {
        GodotObject skillProgress = GetSkillProgress(skill_id);
        GodotObject professionProgress = GetProfessionProgress(profession_id);
        SkillDef skillDef = GetSkillDef(skill_id);
        if (skillProgress == null || professionProgress == null || skillDef == null)
            return false;
        if (!GdInterop.GetBool(skillProgress, "is_learned"))
            return false;
        if (!GdInterop.GetBool(skillProgress, "is_core"))
            return false;
        if (
            !SkillEffectiveMaxLevelRules.is_at_effective_max_level(
                skillDef,
                skillProgress as UnitSkillProgress,
                _unit_progress
            )
        )
            return false;

        StringName assignedProfessionId = GdInterop.GetStringName(
            skillProgress,
            "assigned_profession_id"
        );
        if (!GdInterop.IsEmpty(assignedProfessionId) && assignedProfessionId != profession_id)
            return false;
        return true;
    }

    public bool assign_core_skill_to_profession(StringName skill_id, StringName profession_id)
    {
        if (!can_assign_core_skill_to_profession(skill_id, profession_id))
            return false;

        GodotObject skillProgress = GetSkillProgress(skill_id);
        GodotObject professionProgress = GetProfessionProgress(profession_id);
        RemoveSkillFromAllProfessions(skill_id, profession_id);
        skillProgress.Set("assigned_profession_id", profession_id);
        professionProgress.Call("add_core_skill", skill_id);
        _unit_progress.Call("sync_active_core_skill_ids");
        return true;
    }

    public bool remove_core_skill_from_profession(StringName skill_id, StringName profession_id)
    {
        GodotObject professionProgress = GetProfessionProgress(profession_id);
        if (professionProgress == null)
            return false;

        GArray coreSkillIds = GdInterop.GetArray(professionProgress, "core_skill_ids");
        if (!coreSkillIds.Contains(skill_id))
            return false;

        professionProgress.Call("remove_core_skill", skill_id);
        GodotObject skillProgress = GetSkillProgress(skill_id);
        if (
            skillProgress != null
            && GdInterop.GetStringName(skillProgress, "assigned_profession_id") == profession_id
        )
            skillProgress.Call("clear_profession_assignment");

        _unit_progress.Call("sync_active_core_skill_ids");
        return true;
    }

    public bool can_promote_non_core_to_core(StringName skill_id, StringName profession_id)
    {
        if (_unit_progress == null)
            return false;

        GodotObject professionProgress = GetProfessionProgress(profession_id);
        ProfessionDef professionDef = GetProfessionDef(profession_id);
        GodotObject skillProgress = GetSkillProgress(skill_id);
        SkillDef skillDef = GetSkillDef(skill_id);
        if (
            professionProgress == null
            || professionDef == null
            || skillProgress == null
            || skillDef == null
        )
            return false;

        int currentCharacterLevel = GetEffectiveCharacterLevel();
        _unit_progress.Call("sync_active_core_skill_ids");
        if (
            GdInterop.GetArray(_unit_progress, "active_core_skill_ids").Count
            >= currentCharacterLevel
        )
            return false;
        if (
            GdInterop.GetArray(professionProgress, "core_skill_ids").Count
            >= GdInterop.GetInt(professionProgress, "rank")
        )
            return false;
        if (GdInterop.GetInt(professionProgress, "rank") <= 0)
            return false;
        if (!GdInterop.GetBool(skillProgress, "is_learned"))
            return false;
        if (GdInterop.GetBool(skillProgress, "is_core"))
            return false;
        if (!GdInterop.IsEmpty(GdInterop.GetStringName(skillProgress, "assigned_profession_id")))
            return false;
        if (
            !SkillEffectiveMaxLevelRules.is_at_effective_max_level(
                skillDef,
                skillProgress as UnitSkillProgress,
                _unit_progress
            )
        )
            return false;

        Godot.Collections.Array<StringName> acceptedTags = GetProfessionAcceptedTags(professionDef);
        if (acceptedTags.Count == 0)
            return false;

        foreach (var rawTag in skillDef.tags)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawTag);
            if (acceptedTags.Contains(tag))
                return true;
        }
        return false;
    }

    public bool promote_non_core_to_core(StringName skill_id, StringName profession_id)
    {
        if (!can_promote_non_core_to_core(skill_id, profession_id))
            return false;

        GodotObject skillProgress = GetSkillProgress(skill_id);
        if (skillProgress == null)
            return false;

        skillProgress.Set("is_core", true);
        skillProgress.Set("assigned_profession_id", profession_id);

        GodotObject professionProgress = GetProfessionProgress(profession_id);
        professionProgress.Call("add_core_skill", skill_id);
        _unit_progress.Call("sync_active_core_skill_ids");
        return true;
    }

    public Godot.Collections.Array<StringName> get_profession_core_skills(StringName profession_id)
    {
        GodotObject professionProgress = GetProfessionProgress(profession_id);
        if (professionProgress == null)
            return new Godot.Collections.Array<StringName>();

        Godot.Collections.Array<StringName> result = new();
        foreach (var rawSkillId in GdInterop.GetArray(professionProgress, "core_skill_ids"))
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (!GdInterop.IsEmpty(skillId))
                result.Add(skillId);
        }
        return result;
    }

    public StringName get_skill_assigned_profession(StringName skill_id)
    {
        GodotObject skillProgress = GetSkillProgress(skill_id);
        return skillProgress == null
            ? new StringName("")
            : GdInterop.GetStringName(skillProgress, "assigned_profession_id");
    }

    private static GDictionary IndexSkillDefs(GDictionary skillDefs)
    {
        GDictionary indexedDefs = new();
        if (skillDefs == null)
            return indexedDefs;

        foreach (var key in skillDefs.Keys)
        {
            if (skillDefs[key].AsGodotObject() is not SkillDef skillDef)
                continue;
            StringName indexedId = !GdInterop.IsEmpty(skillDef.skill_id)
                ? skillDef.skill_id
                : ProgressionDataUtils.to_string_name(key);
            indexedDefs[indexedId] = skillDef;
        }
        return indexedDefs;
    }

    private static GDictionary IndexProfessionDefs(GDictionary professionDefs)
    {
        GDictionary indexedDefs = new();
        if (professionDefs == null)
            return indexedDefs;

        foreach (var key in professionDefs.Keys)
        {
            if (professionDefs[key].AsGodotObject() is not ProfessionDef professionDef)
                continue;
            StringName indexedId = !GdInterop.IsEmpty(professionDef.profession_id)
                ? professionDef.profession_id
                : ProgressionDataUtils.to_string_name(key);
            indexedDefs[indexedId] = professionDef;
        }
        return indexedDefs;
    }

    private GodotObject GetSkillProgress(StringName skillId)
    {
        if (_unit_progress == null)
            return null;
        var result = _unit_progress.Call("get_skill_progress", skillId);
        return result.VariantType == Variant.Type.Object ? result.AsGodotObject() : null;
    }

    private GodotObject GetProfessionProgress(StringName professionId)
    {
        if (_unit_progress == null)
            return null;
        var result = _unit_progress.Call("get_profession_progress", professionId);
        return result.VariantType == Variant.Type.Object ? result.AsGodotObject() : null;
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skill_defs.ContainsKey(skillId)
            ? _skill_defs[skillId].AsGodotObject() as SkillDef
            : null;
    }

    private ProfessionDef GetProfessionDef(StringName professionId)
    {
        return _profession_defs.ContainsKey(professionId)
            ? _profession_defs[professionId].AsGodotObject() as ProfessionDef
            : null;
    }

    private void RemoveSkillFromAllProfessions(
        StringName skillId,
        StringName exceptProfessionId = default
    )
    {
        if (_unit_progress == null)
            return;

        GDictionary professions = GdInterop.GetDictionary(_unit_progress, "professions");
        foreach (var professionKey in professions.Keys)
        {
            StringName professionId = ProgressionDataUtils.to_string_name(professionKey);
            if (!GdInterop.IsEmpty(exceptProfessionId) && professionId == exceptProfessionId)
                continue;

            GodotObject professionProgress = GetProfessionProgress(professionId);
            professionProgress?.Call("remove_core_skill", skillId);
        }
    }

    private static Godot.Collections.Array<StringName> GetProfessionAcceptedTags(
        ProfessionDef professionDef
    )
    {
        Godot.Collections.Array<StringName> acceptedTags = new();
        GDictionary seenTags = new();
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
        Godot.Collections.Array<StringName> acceptedTags,
        GDictionary seenTags,
        Godot.Collections.Array<TagRequirement> tagRules
    )
    {
        foreach (TagRequirement tagRule in tagRules)
        {
            if (
                tagRule == null
                || GdInterop.IsEmpty(tagRule.tag)
                || seenTags.ContainsKey(tagRule.tag)
            )
                continue;
            seenTags[tagRule.tag] = true;
            acceptedTags.Add(tagRule.tag);
        }
    }

    private int GetEffectiveCharacterLevel()
    {
        if (_unit_progress == null)
            return 0;

        int rankTotal = 0;
        foreach (
            Variant rawProfessionProgress in GdInterop
                .GetDictionary(_unit_progress, "professions")
                .Values
        )
        {
            GodotObject professionProgress =
                rawProfessionProgress.VariantType == Variant.Type.Object
                    ? rawProfessionProgress.AsGodotObject()
                    : null;
            if (professionProgress != null)
                rankTotal += GdInterop.GetInt(professionProgress, "rank");
        }
        return rankTotal;
    }
}

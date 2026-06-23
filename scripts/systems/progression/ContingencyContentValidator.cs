using System.Collections.Generic;
using Godot;

public static class ContingencyContentValidator
{
    private static readonly HashSet<StringName> ForbiddenAutomationTags = new()
    {
        "contingency",
        "meta_spell",
        "contingency_forbidden",
    };

    public static IReadOnlyList<string> ValidateAllSetupsForSaveLoad(
        PartyState partyState,
        GameContentCatalog catalog
    )
    {
        return ValidateAllSetupsForSaveLoad(partyState, catalog?.GetSkillDefsTyped());
    }

    public static IReadOnlyList<string> ValidateAllSetupsForSaveLoad(
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        List<string> errors = new();
        if (partyState == null)
        {
            errors.Add("party_state: contingency_content_validation.party_state_missing");
            return errors;
        }
        if (skillDefs == null)
        {
            errors.Add("skill_catalog: contingency_content_validation.skill_catalog_missing");
            return errors;
        }

        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            if (memberState == null)
                continue;
            ValidateMemberSetups(errors, memberState, skillDefs);
        }
        return errors;
    }

    private static void ValidateMemberSetups(
        List<string> errors,
        PartyMemberState memberState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        IReadOnlyList<ContingencyMatrixSetupState> setups =
            memberState.GetContingencySetupsTyped();
        for (int setupIndex = 0; setupIndex < setups.Count; setupIndex++)
        {
            ContingencyMatrixSetupState setup = setups[setupIndex];
            if (setup == null)
                continue;
            string setupPath =
                $"party_state.member_states.{memberState.member_id}.contingency_matrix_setups[{setupIndex}]";
            ValidateSetup(errors, memberState, setup, setupPath, skillDefs);
        }
    }

    private static void ValidateSetup(
        List<string> errors,
        PartyMemberState memberState,
        ContingencyMatrixSetupState setup,
        string setupPath,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        if (!skillDefs.TryGetValue(setup.SourceSkillId, out SkillDef sourceSkill))
        {
            errors.Add($"{setupPath}.source_skill_id: source_skill_missing:{setup.SourceSkillId}");
            return;
        }
        if (!sourceSkill.HasTag("contingency") || !sourceSkill.HasTag("meta_spell"))
            errors.Add($"{setupPath}.source_skill_id: source_skill_not_chain_contingency:{setup.SourceSkillId}");
        if (!MemberKnowsSkillAtAnyLevel(memberState, setup.SourceSkillId))
            errors.Add($"{setupPath}.source_skill_id: source_skill_not_known:{setup.SourceSkillId}");

        IReadOnlyList<ContingencyStoredSpellEntryState> storedSpells = setup.StoredSpells;
        for (int spellIndex = 0; spellIndex < storedSpells.Count; spellIndex++)
        {
            ContingencyStoredSpellEntryState storedSpell = storedSpells[spellIndex];
            if (storedSpell == null)
                continue;
            ValidateStoredSpell(
                errors,
                memberState,
                setup,
                storedSpell,
                $"{setupPath}.stored_spells[{spellIndex}]",
                skillDefs
            );
        }
    }

    private static void ValidateStoredSpell(
        List<string> errors,
        PartyMemberState memberState,
        ContingencyMatrixSetupState setup,
        ContingencyStoredSpellEntryState storedSpell,
        string spellPath,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        if (!skillDefs.TryGetValue(storedSpell.StoredSkillId, out SkillDef skillDef))
        {
            errors.Add($"{spellPath}.stored_skill_id: stored_skill_missing:{storedSpell.StoredSkillId}");
            return;
        }
        if (!MemberKnowsSkillAtAnyLevel(memberState, storedSpell.StoredSkillId))
            errors.Add($"{spellPath}.stored_skill_id: stored_skill_not_known:{storedSpell.StoredSkillId}");

        ContingencyAutomationDef automation = skillDef.contingency_automation_profile;
        if (automation == null)
        {
            errors.Add($"{spellPath}.stored_skill_id: automation_profile_missing:{storedSpell.StoredSkillId}");
            return;
        }

        StringName forbiddenTag = FirstForbiddenTag(skillDef, automation);
        if (forbiddenTag != "")
        {
            errors.Add($"{spellPath}.stored_skill_id: forbidden_tag:{forbiddenTag}");
            return;
        }

        if (!automation.can_be_stored_in_contingency)
        {
            errors.Add($"{spellPath}.stored_skill_id: not_storable:{storedSpell.StoredSkillId}");
            return;
        }
        if (automation.min_contingency_skill_level <= 0)
        {
            errors.Add($"{spellPath}.stored_skill_id: invalid_min_contingency_skill_level");
            return;
        }
        if (setup.SourceSkillLevel < automation.min_contingency_skill_level)
        {
            errors.Add(
                $"{spellPath}.stored_skill_id: source_skill_level_too_low:{setup.SourceSkillLevel}<{automation.min_contingency_skill_level}"
            );
            return;
        }
        StringName resolverType = storedSpell.TargetResolver?.Type ?? "";
        if (!automation.AllowsTargetResolver(resolverType))
        {
            errors.Add($"{spellPath}.target_resolver.type: target_resolver_not_allowed:{resolverType}");
            return;
        }
        foreach (Variant rawKey in storedSpell.ParameterBindings.Keys)
        {
            if (!TryAsStringName(rawKey, out StringName bindingKey)
                || !automation.AllowsParameterBinding(bindingKey))
            {
                errors.Add($"{spellPath}.parameter_bindings: unsupported_parameter_binding:{rawKey}");
                return;
            }
        }
    }

    private static StringName FirstForbiddenTag(
        SkillDef skillDef,
        ContingencyAutomationDef automation
    )
    {
        foreach (StringName tag in skillDef.TagsTyped)
            if (ForbiddenAutomationTags.Contains(tag))
                return tag;
        if (automation?.tags != null)
        {
            foreach (StringName tag in automation.tags)
                if (ForbiddenAutomationTags.Contains(tag))
                    return tag;
        }
        return "";
    }

    private static bool MemberKnowsSkillAtAnyLevel(PartyMemberState memberState, StringName skillId)
    {
        UnitSkillProgress progress = memberState?.progression?.GetSkillProgress(skillId);
        return progress != null && progress.is_learned && progress.skill_level > 0;
    }

    private static bool TryAsStringName(Variant rawValue, out StringName value)
    {
        value = "";
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = rawValue.AsString();
            return value != "";
        }
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName();
            return value != "";
        }
        return false;
    }
}

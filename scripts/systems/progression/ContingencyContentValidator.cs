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
        return ValidateAllSetupsForSaveLoad(partyState, catalog?.GetSkillDefinitionsTyped());
    }

    public static IReadOnlyList<string> ValidateAllSetupsForSaveLoad(
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        List<string> errors = new();
        if (partyState == null)
        {
            errors.Add("party_state: contingency_content_validation.party_state_missing");
            return errors;
        }
        if (skillDefinitions == null)
        {
            errors.Add("skill_catalog: contingency_content_validation.skill_catalog_missing");
            return errors;
        }

        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            if (memberState == null)
                continue;
            ValidateMemberSetups(errors, memberState, skillDefinitions);
        }
        return errors;
    }

    private static void ValidateMemberSetups(
        List<string> errors,
        PartyMemberState memberState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
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
            ValidateSetup(errors, memberState, setup, setupPath, skillDefinitions);
        }
    }

    private static void ValidateSetup(
        List<string> errors,
        PartyMemberState memberState,
        ContingencyMatrixSetupState setup,
        string setupPath,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (!skillDefinitions.TryGetValue(setup.SourceSkillId, out SkillDefinition sourceSkill))
        {
            errors.Add($"{setupPath}.source_skill_id: source_skill_missing:{setup.SourceSkillId}");
            return;
        }
        if (!sourceSkill.HasTag("contingency") || !sourceSkill.HasTag("meta_spell"))
            errors.Add($"{setupPath}.source_skill_id: source_skill_not_chain_contingency:{setup.SourceSkillId}");
        UnitSkillProgress sourceProgress = GetKnownSkillProgress(memberState, setup.SourceSkillId);
        if (sourceProgress == null)
            errors.Add($"{setupPath}.source_skill_id: source_skill_not_known:{setup.SourceSkillId}");
        else if (sourceProgress.GrantedSourceTypeKind != UnitSkillGrantSourceType.Player)
            errors.Add(
                $"{setupPath}.source_skill_id: source_skill_not_player_learned:{setup.SourceSkillId}"
            );
        else if (setup.SourceSkillLevel > sourceProgress.skill_level)
            errors.Add(
                $"{setupPath}.source_skill_level: source_skill_level_exceeds_known_level:{setup.SourceSkillLevel}>{sourceProgress.skill_level}"
            );

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
                skillDefinitions
            );
        }
    }

    private static void ValidateStoredSpell(
        List<string> errors,
        PartyMemberState memberState,
        ContingencyMatrixSetupState setup,
        ContingencyStoredSpellEntryState storedSpell,
        string spellPath,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (!skillDefinitions.TryGetValue(storedSpell.StoredSkillId, out SkillDefinition skillDefinition))
        {
            errors.Add($"{spellPath}.stored_skill_id: stored_skill_missing:{storedSpell.StoredSkillId}");
            return;
        }
        UnitSkillProgress storedProgress = GetKnownSkillProgress(memberState, storedSpell.StoredSkillId);
        if (storedProgress == null)
            errors.Add($"{spellPath}.stored_skill_id: stored_skill_not_known:{storedSpell.StoredSkillId}");
        else if (storedSpell.CastLevel > storedProgress.skill_level)
            errors.Add(
                $"{spellPath}.cast_level: stored_skill_cast_level_exceeds_known_level:{storedSpell.CastLevel}>{storedProgress.skill_level}"
            );

        ContingencyAutomationDefinition automation = skillDefinition.ContingencyAutomationProfile;
        if (automation == null)
        {
            errors.Add($"{spellPath}.stored_skill_id: automation_profile_missing:{storedSpell.StoredSkillId}");
            return;
        }

        StringName forbiddenTag = FirstForbiddenTag(skillDefinition, automation);
        if (forbiddenTag != "")
        {
            errors.Add($"{spellPath}.stored_skill_id: forbidden_tag:{forbiddenTag}");
            return;
        }

        if (!automation.CanBeStoredInContingency)
        {
            errors.Add($"{spellPath}.stored_skill_id: not_storable:{storedSpell.StoredSkillId}");
            return;
        }
        if (automation.MinContingencySkillLevel <= 0)
        {
            errors.Add($"{spellPath}.stored_skill_id: invalid_min_contingency_skill_level");
            return;
        }
        if (setup.SourceSkillLevel < automation.MinContingencySkillLevel)
        {
            errors.Add(
                $"{spellPath}.stored_skill_id: source_skill_level_too_low:{setup.SourceSkillLevel}<{automation.MinContingencySkillLevel}"
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
        SkillDefinition skillDefinition,
        ContingencyAutomationDefinition automation
    )
    {
        foreach (StringName tag in skillDefinition.Tags)
            if (ForbiddenAutomationTags.Contains(tag))
                return tag;
        if (automation?.Tags != null)
        {
            foreach (StringName tag in automation.Tags)
                if (ForbiddenAutomationTags.Contains(tag))
                    return tag;
        }
        return "";
    }

    private static UnitSkillProgress GetKnownSkillProgress(
        PartyMemberState memberState,
        StringName skillId
    )
    {
        UnitSkillProgress progress = memberState?.progression?.GetSkillProgress(skillId);
        if (progress == null || !progress.is_learned || progress.skill_level <= 0)
            return null;
        return progress;
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

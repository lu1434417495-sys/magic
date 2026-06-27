using System;
using System.Collections.Generic;
using Godot;

public static class RacialSkillGrantService
{
    private sealed class RacialGrantEntry
    {
        internal readonly RacialGrantedSkill Grant;
        internal readonly StringName SourceType;
        internal readonly StringName SourceId;

        internal RacialGrantEntry(
            RacialGrantedSkill grant,
            StringName sourceType,
            StringName sourceId
        )
        {
            Grant = grant;
            SourceType = sourceType;
            SourceId = sourceId;
        }
    }

    public static bool BackfillParty(
        PartyState partyState,
        ProgressionIdentityCatalogData identityCatalog,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (partyState == null)
            return false;
        bool changed = false;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
            changed =
                BackfillMember(
                    memberState,
                    identityCatalog,
                    skillDefinitions,
                    professionDefs,
                    progressionServiceFactory
                ) || changed;
        return changed;
    }

    public static bool RevokeOrphanParty(
        PartyState partyState,
        ProgressionIdentityCatalogData identityCatalog,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (partyState == null)
            return false;
        bool changed = false;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
            changed =
                RevokeOrphanMember(
                    memberState,
                    identityCatalog,
                    skillDefinitions,
                    professionDefs,
                    progressionServiceFactory
                ) || changed;
        return changed;
    }

    public static bool BackfillMember(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData identityCatalog,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (memberState?.progression == null)
            return false;
        List<RacialGrantEntry> grantEntries = CollectMemberRacialGrantEntries(
            memberState,
            identityCatalog
        );
        if (grantEntries.Count == 0)
            return false;
        var ps = _build_progression_service(
            memberState.progression,
            skillDefinitions,
            professionDefs,
            progressionServiceFactory
        );
        bool changed = false;
        foreach (RacialGrantEntry grantEntry in grantEntries)
        {
            if (
                ps.GrantRacialSkill(
                    grantEntry.Grant,
                    grantEntry.SourceType,
                    grantEntry.SourceId
                )
            )
                changed = true;
        }
        return changed;
    }

    public static bool RevokeOrphanMember(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData identityCatalog,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (memberState?.progression == null)
            return false;
        HashSet<string> activeGrantLookup = CollectActiveIdentityGrantLookup(
            memberState,
            identityCatalog
        );
        List<StringName> skillIdsToRemove = new();
        foreach (var skId in memberState.progression.GetSortedSkillIdsTyped())
        {
            var sp = memberState.progression.GetSkillProgress(skId);
            if (sp == null)
                continue;
            var st = ProgressionDataUtils.to_string_name(sp.granted_source_type);
            if (!IsRacialGrantedSourceType(st))
                continue;
            var si = ProgressionDataUtils.to_string_name(sp.granted_source_id);
            if (activeGrantLookup.Contains(IdentityGrantKey(st, si, skId)))
                continue;
            if (sp.profession_granted_by != "")
                continue;
            skillIdsToRemove.Add(skId);
        }
        if (skillIdsToRemove.Count == 0)
            return false;
        foreach (var skId in skillIdsToRemove)
            memberState.progression.RemoveSkillProgress(skId);
        var ps = _build_progression_service(
            memberState.progression,
            skillDefinitions,
            professionDefs,
            progressionServiceFactory
        );
        ps.RefreshRuntimeState();
        return true;
    }

    private static List<RacialGrantEntry> CollectMemberRacialGrantEntries(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData identityCatalog
    )
    {
        List<RacialGrantEntry> entries = new();
        if (memberState == null)
            return entries;
        identityCatalog ??= new ProgressionIdentityCatalogData();
        RaceDef rd = Lookup(identityCatalog.RaceDefs, memberState.race_id);
        if (rd != null)
            _append(entries, rd.racial_granted_skills, "race", memberState.race_id);
        SubraceDef srd = Lookup(identityCatalog.SubraceDefs, memberState.subrace_id);
        if (srd != null)
            _append(entries, srd.racial_granted_skills, "subrace", memberState.subrace_id);
        if (memberState.bloodline_id != "")
        {
            BloodlineDef bld = Lookup(identityCatalog.BloodlineDefs, memberState.bloodline_id);
            if (bld != null)
                _append(entries, bld.racial_granted_skills, "bloodline", memberState.bloodline_id);
        }
        if (memberState.bloodline_stage_id != "")
        {
            BloodlineStageDef blsd = Lookup(
                identityCatalog.BloodlineStageDefs,
                memberState.bloodline_stage_id
            );
            if (blsd != null)
                _append(
                    entries,
                    blsd.racial_granted_skills,
                    "bloodline",
                    memberState.bloodline_stage_id
                );
        }
        if (memberState.ascension_id != "")
        {
            AscensionDef ad = Lookup(identityCatalog.AscensionDefs, memberState.ascension_id);
            if (ad != null)
                _append(entries, ad.racial_granted_skills, "ascension", memberState.ascension_id);
        }
        if (memberState.ascension_stage_id != "")
        {
            AscensionStageDef asd = Lookup(
                identityCatalog.AscensionStageDefs,
                memberState.ascension_stage_id
            );
            if (asd != null)
                _append(
                    entries,
                    asd.racial_granted_skills,
                    "ascension",
                    memberState.ascension_stage_id
                );
        }
        return entries;
    }

    private static void _append(
        List<RacialGrantEntry> entries,
        IEnumerable<RacialGrantedSkill> grantedSkills,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (sourceId == "" || grantedSkills == null)
            return;
        foreach (RacialGrantedSkill grant in grantedSkills)
        {
            if (grant == null)
                continue;
            entries.Add(new RacialGrantEntry(grant, sourceType, sourceId));
        }
    }

    private static HashSet<string> CollectActiveIdentityGrantLookup(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData identityCatalog
    )
    {
        HashSet<string> lookup = new(StringComparer.Ordinal);
        if (memberState == null)
            return lookup;
        foreach (RacialGrantEntry grantEntry in CollectMemberRacialGrantEntries(memberState, identityCatalog))
        {
            RacialGrantedSkill grant = grantEntry.Grant;
            if (grant == null || grant.skill_id == "")
                continue;
            if (grantEntry.SourceType == "" || grantEntry.SourceId == "")
                continue;
            lookup.Add(
                IdentityGrantKey(grantEntry.SourceType, grantEntry.SourceId, grant.skill_id)
            );
        }
        return lookup;
    }

    public static string IdentityGrantKey(
        StringName sourceType,
        StringName sourceId,
        StringName skillId
    ) => $"{(string)sourceType}:{(string)sourceId}:{(string)skillId}";

    public static bool IsRacialGrantedSourceType(StringName sourceType) =>
        UnitSkillProgress.ToGrantSourceType(sourceType)
            is UnitSkillGrantSourceType.Race
                or UnitSkillGrantSourceType.Subrace
                or UnitSkillGrantSourceType.Ascension
                or UnitSkillGrantSourceType.Bloodline;

    private static T Lookup<T>(IReadOnlyDictionary<StringName, T> defs, StringName entryId)
        where T : class =>
        entryId != "" && defs != null && defs.TryGetValue(entryId, out T entry) ? entry : null;

    private static ProgressionService _build_progression_service(
        UnitProgress progressionState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        Func<UnitProgress, ProgressionService> factory
    )
    {
        if (factory != null)
            return factory.Invoke(progressionState);
        var ps = new ProgressionService();
        ps.SetupDefinitions(progressionState, skillDefinitions, professionDefs);
        return ps;
    }

}

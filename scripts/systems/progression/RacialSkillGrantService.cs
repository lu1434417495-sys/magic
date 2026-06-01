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

    public static bool backfill_party(
        PartyState partyState,
        Godot.Collections.Dictionary contentBundle,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (partyState == null)
            return false;
        bool changed = false;
        foreach (PartyMemberState memberState in partyState.get_member_states())
            changed =
                backfill_member(
                    memberState,
                    contentBundle,
                    skillDefs,
                    professionDefs,
                    progressionServiceFactory
                ) || changed;
        return changed;
    }

    public static bool revoke_orphan_party(
        PartyState partyState,
        Godot.Collections.Dictionary contentBundle,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (partyState == null)
            return false;
        bool changed = false;
        foreach (PartyMemberState memberState in partyState.get_member_states())
            changed =
                revoke_orphan_member(
                    memberState,
                    contentBundle,
                    skillDefs,
                    professionDefs,
                    progressionServiceFactory
                ) || changed;
        return changed;
    }

    public static bool backfill_member(
        PartyMemberState memberState,
        Godot.Collections.Dictionary contentBundle,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (memberState?.progression == null)
            return false;
        List<RacialGrantEntry> grantEntries = CollectMemberRacialGrantEntries(
            memberState,
            contentBundle
        );
        if (grantEntries.Count == 0)
            return false;
        var ps = _build_progression_service(
            memberState.progression,
            skillDefs,
            professionDefs,
            progressionServiceFactory
        );
        bool changed = false;
        foreach (RacialGrantEntry grantEntry in grantEntries)
        {
            if (
                ps.grant_racial_skill(
                    grantEntry.Grant,
                    grantEntry.SourceType,
                    grantEntry.SourceId
                )
            )
                changed = true;
        }
        return changed;
    }

    public static bool revoke_orphan_member(
        PartyMemberState memberState,
        Godot.Collections.Dictionary contentBundle,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs,
        Func<UnitProgress, ProgressionService> progressionServiceFactory = null
    )
    {
        if (memberState?.progression == null)
            return false;
        HashSet<string> activeGrantLookup = CollectActiveIdentityGrantLookup(
            memberState,
            contentBundle
        );
        List<StringName> skillIdsToRemove = new();
        foreach (
            var sk in ProgressionDataUtils.sorted_string_keys(
                memberState.progression.skills
            )
        )
        {
            var skId = new StringName(sk);
            var sp = memberState.progression.get_skill_progress(skId);
            if (sp == null)
                continue;
            var st = ProgressionDataUtils.to_string_name(sp.granted_source_type);
            if (!is_racial_granted_source_type(st))
                continue;
            var si = ProgressionDataUtils.to_string_name(sp.granted_source_id);
            if (activeGrantLookup.Contains(identity_grant_key(st, si, skId)))
                continue;
            if (sp.profession_granted_by != "")
                continue;
            skillIdsToRemove.Add(skId);
        }
        if (skillIdsToRemove.Count == 0)
            return false;
        foreach (var skId in skillIdsToRemove)
            memberState.progression.remove_skill_progress(skId);
        var ps = _build_progression_service(
            memberState.progression,
            skillDefs,
            professionDefs,
            progressionServiceFactory
        );
        ps.refresh_runtime_state();
        return true;
    }

    private static List<RacialGrantEntry> CollectMemberRacialGrantEntries(
        PartyMemberState memberState,
        Godot.Collections.Dictionary contentBundle
    )
    {
        List<RacialGrantEntry> entries = new();
        if (memberState == null)
            return entries;
        RaceDef rd =
            _get_content_def<RaceDef>(contentBundle, "race_defs", "race", memberState.race_id);
        if (rd != null)
            _append(entries, rd.racial_granted_skills, "race", memberState.race_id);
        SubraceDef srd = _get_content_def<SubraceDef>(
            contentBundle,
            "subrace_defs",
            "subrace",
            memberState.subrace_id
        );
        if (srd != null)
            _append(entries, srd.racial_granted_skills, "subrace", memberState.subrace_id);
        if (memberState.bloodline_id != "")
        {
            BloodlineDef bld =
                _get_content_def<BloodlineDef>(
                    contentBundle,
                    "bloodline_defs",
                    "bloodline",
                    memberState.bloodline_id
                );
            if (bld != null)
                _append(entries, bld.racial_granted_skills, "bloodline", memberState.bloodline_id);
        }
        if (memberState.bloodline_stage_id != "")
        {
            BloodlineStageDef blsd =
                _get_content_def<BloodlineStageDef>(
                    contentBundle,
                    "bloodline_stage_defs",
                    "bloodline_stage",
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
            AscensionDef ad =
                _get_content_def<AscensionDef>(
                    contentBundle,
                    "ascension_defs",
                    "ascension",
                    memberState.ascension_id
                );
            if (ad != null)
                _append(entries, ad.racial_granted_skills, "ascension", memberState.ascension_id);
        }
        if (memberState.ascension_stage_id != "")
        {
            AscensionStageDef asd =
                _get_content_def<AscensionStageDef>(
                    contentBundle,
                    "ascension_stage_defs",
                    "ascension_stage",
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
        Godot.Collections.Dictionary contentBundle
    )
    {
        HashSet<string> lookup = new(StringComparer.Ordinal);
        if (memberState == null)
            return lookup;
        foreach (RacialGrantEntry grantEntry in CollectMemberRacialGrantEntries(memberState, contentBundle))
        {
            RacialGrantedSkill grant = grantEntry.Grant;
            if (grant == null || grant.skill_id == "")
                continue;
            if (grantEntry.SourceType == "" || grantEntry.SourceId == "")
                continue;
            lookup.Add(
                identity_grant_key(grantEntry.SourceType, grantEntry.SourceId, grant.skill_id)
            );
        }
        return lookup;
    }

    public static string identity_grant_key(
        StringName sourceType,
        StringName sourceId,
        StringName skillId
    ) => $"{(string)sourceType}:{(string)sourceId}:{(string)skillId}";

    public static bool is_racial_granted_source_type(StringName sourceType) =>
        sourceType == "race"
        || sourceType == "subrace"
        || sourceType == "ascension"
        || sourceType == "bloodline";

    private static T _get_content_def<T>(
        Godot.Collections.Dictionary contentBundle,
        string primaryBucket,
        string aliasBucket,
        StringName entryId
    ) where T : class
    {
        if (entryId == "")
            return null;
        var bucket = _get_content_bucket(contentBundle, primaryBucket, aliasBucket);
        return bucket.ContainsKey(entryId) ? bucket[entryId].AsGodotObject() as T : null;
    }

    private static Godot.Collections.Dictionary _get_content_bucket(
        Godot.Collections.Dictionary contentBundle,
        string primaryBucket,
        string aliasBucket
    )
    {
        if (contentBundle.ContainsKey(primaryBucket))
        {
            var bv = contentBundle[primaryBucket];
            if (bv.VariantType == Variant.Type.Dictionary)
                return bv.AsGodotDictionary();
        }
        if (contentBundle.ContainsKey(aliasBucket))
        {
            var bv = contentBundle[aliasBucket];
            if (bv.VariantType == Variant.Type.Dictionary)
                return bv.AsGodotDictionary();
        }
        return new Godot.Collections.Dictionary();
    }

    private static ProgressionService _build_progression_service(
        UnitProgress progressionState,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs,
        Func<UnitProgress, ProgressionService> factory
    )
    {
        if (factory != null)
            return factory.Invoke(progressionState);
        var ps = new ProgressionService();
        ps.setup(progressionState, skillDefs, professionDefs);
        return ps;
    }
}

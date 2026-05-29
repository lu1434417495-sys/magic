using System;
using Godot;

[GlobalClass]
public partial class RacialSkillGrantService : RefCounted
{
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
        foreach (var msv in partyState.member_states.Values)
            changed =
                backfill_member(
                    msv.AsGodotObject() as PartyMemberState,
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
        foreach (var msv in partyState.member_states.Values)
            changed =
                revoke_orphan_member(
                    msv.AsGodotObject() as PartyMemberState,
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
        var grantEntries = collect_member_racial_grant_entries(memberState, contentBundle);
        if (grantEntries.Count == 0)
            return false;
        var ps = _build_progression_service(
            memberState.progression,
            skillDefs,
            professionDefs,
            progressionServiceFactory
        );
        bool changed = false;
        foreach (var ge in grantEntries)
        {
            var grant = ge["grant"].AsGodotObject() as RacialGrantedSkill;
            var st = ProgressionDataUtils.to_string_name(ge["source_type"]);
            var si = ProgressionDataUtils.to_string_name(ge["source_id"]);
            if (ps.grant_racial_skill(grant, st, si))
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
        var activeGrantLookup = collect_active_identity_grant_lookup(memberState, contentBundle);
        var skillIdsToRemove = new Godot.Collections.Array<StringName>();
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
            if (activeGrantLookup.ContainsKey(identity_grant_key(st, si, skId)))
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

    public static Godot.Collections.Array<Godot.Collections.Dictionary> collect_member_racial_grant_entries(
        PartyMemberState memberState,
        Godot.Collections.Dictionary contentBundle
    )
    {
        var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (memberState == null)
            return entries;
        var rd =
            _get_content_def(contentBundle, "race_defs", "race", memberState.race_id) as RaceDef;
        if (rd != null)
            _append(entries, rd.racial_granted_skills, "race", memberState.race_id);
        var srd =
            _get_content_def(contentBundle, "subrace_defs", "subrace", memberState.subrace_id)
            as SubraceDef;
        if (srd != null)
            _append(entries, srd.racial_granted_skills, "subrace", memberState.subrace_id);
        if (memberState.bloodline_id != "")
        {
            var bld =
                _get_content_def(
                    contentBundle,
                    "bloodline_defs",
                    "bloodline",
                    memberState.bloodline_id
                ) as BloodlineDef;
            if (bld != null)
                _append(entries, bld.racial_granted_skills, "bloodline", memberState.bloodline_id);
        }
        if (memberState.bloodline_stage_id != "")
        {
            var blsd =
                _get_content_def(
                    contentBundle,
                    "bloodline_stage_defs",
                    "bloodline_stage",
                    memberState.bloodline_stage_id
                ) as BloodlineStageDef;
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
            var ad =
                _get_content_def(
                    contentBundle,
                    "ascension_defs",
                    "ascension",
                    memberState.ascension_id
                ) as AscensionDef;
            if (ad != null)
                _append(entries, ad.racial_granted_skills, "ascension", memberState.ascension_id);
        }
        if (memberState.ascension_stage_id != "")
        {
            var asd =
                _get_content_def(
                    contentBundle,
                    "ascension_stage_defs",
                    "ascension_stage",
                    memberState.ascension_stage_id
                ) as AscensionStageDef;
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
        Godot.Collections.Array<Godot.Collections.Dictionary> entries,
        object grantedSkills,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (sourceId == "")
            return;
        Godot.Collections.Array values = null;
        if (grantedSkills is Variant variantSkills && variantSkills.VariantType == Variant.Type.Array)
        {
            values = variantSkills.AsGodotArray();
        }
        else if (grantedSkills is Godot.Collections.Array arraySkills)
        {
            values = arraySkills;
        }
        if (values == null)
            return;
        foreach (var g in values)
        {
            if (g.VariantType == Variant.Type.Nil)
                continue;
            entries.Add(
                new Godot.Collections.Dictionary
                {
                    { "grant", g },
                    { "source_type", sourceType },
                    { "source_id", sourceId },
                }
            );
        }
    }

    public static Godot.Collections.Dictionary collect_active_identity_grant_lookup(
        PartyMemberState memberState,
        Godot.Collections.Dictionary contentBundle
    )
    {
        var l = new Godot.Collections.Dictionary();
        if (memberState == null)
            return l;
        foreach (var ge in collect_member_racial_grant_entries(memberState, contentBundle))
        {
            var g = ge["grant"].AsGodotObject() as RacialGrantedSkill;
            if (g == null || g.skill_id == "")
                continue;
            var st = ProgressionDataUtils.to_string_name(ge["source_type"]);
            var si = ProgressionDataUtils.to_string_name(ge["source_id"]);
            if (st == "" || si == "")
                continue;
            l[identity_grant_key(st, si, g.skill_id)] = true;
        }
        return l;
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

    private static GodotObject _get_content_def(
        Godot.Collections.Dictionary contentBundle,
        string primaryBucket,
        string aliasBucket,
        StringName entryId
    )
    {
        if (entryId == "")
            return null;
        var bucket = _get_content_bucket(contentBundle, primaryBucket, aliasBucket);
        return bucket.ContainsKey(entryId) ? bucket[entryId].AsGodotObject() : null;
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

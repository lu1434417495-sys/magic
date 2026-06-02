using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class IdentityPayloadValidator
{
    public static IReadOnlyList<string> ValidatePartyIdentityForContentSource(
        PartyState partyState,
        ProgressionContentRegistry contentSource
    )
    {
        return ValidatePartyIdentity(partyState, IdentityContentSource.FromRegistry(contentSource));
    }

    internal static IReadOnlyList<string> ValidatePartyIdentity(
        PartyState partyState,
        GDictionary contentSource
    )
    {
        return ValidatePartyIdentity(partyState, IdentityContentSource.FromDictionary(contentSource));
    }

    private static List<string> ValidatePartyIdentity(
        PartyState partyState,
        IdentityContentSource contentSource
    )
    {
        var errors = new List<string>();
        if (partyState == null)
        {
            errors.Add("party identity payload is null");
            return errors;
        }
        if (partyState.member_states == null)
        {
            errors.Add("party identity payload has no member_states dictionary");
            return errors;
        }

        foreach (PartyMemberState memberState in partyState.get_member_states())
        {
            foreach (string error in ValidateMemberIdentity(memberState, contentSource))
                errors.Add(error);
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidateMemberIdentityForContentSource(
        PartyMemberState memberState,
        ProgressionContentRegistry contentSource
    )
    {
        return ValidateMemberIdentity(memberState, IdentityContentSource.FromRegistry(contentSource));
    }

    internal static IReadOnlyList<string> ValidateMemberIdentity(
        PartyMemberState memberState,
        GDictionary contentSource
    )
    {
        return ValidateMemberIdentity(memberState, IdentityContentSource.FromDictionary(contentSource));
    }

    private static List<string> ValidateMemberIdentity(
        PartyMemberState memberState,
        IdentityContentSource contentSource
    )
    {
        var errors = new List<string>();
        if (memberState == null)
        {
            errors.Add("member identity payload is null");
            return errors;
        }
        if (!contentSource.HasSource)
        {
            errors.Add(
                $"member {MemberLabel(memberState)} identity validation requires content source"
            );
            return errors;
        }

        string label = MemberLabel(memberState);
        var raceId = memberState.race_id;
        var subraceId = memberState.subrace_id;
        var bloodlineId = memberState.bloodline_id;
        var bloodlineStageId = memberState.bloodline_stage_id;
        var ascensionId = memberState.ascension_id;
        var ascensionStageId = memberState.ascension_stage_id;

        var raceDef = ValidateRace(errors, label, raceId, contentSource);
        var subraceDef = ValidateSubrace(errors, label, subraceId, contentSource);
        ValidateRaceSubracePair(errors, label, raceId, subraceId, raceDef, subraceDef);
        ValidateBloodlinePair(errors, label, bloodlineId, bloodlineStageId, contentSource);
        ValidateAscensionPair(
            errors,
            label,
            raceId,
            subraceId,
            bloodlineId,
            ascensionId,
            ascensionStageId,
            contentSource
        );
        return errors;
    }

    public static StringName ResolveBodySizeCategoryForContentSource(
        PartyMemberState memberState,
        ProgressionContentRegistry contentSource
    )
    {
        return ResolveBodySizeCategoryForMember(
            memberState,
            IdentityContentSource.FromRegistry(contentSource)
        );
    }

    internal static StringName ResolveBodySizeCategoryForMember(
        PartyMemberState memberState,
        GDictionary contentSource
    )
    {
        return ResolveBodySizeCategoryForMember(
            memberState,
            IdentityContentSource.FromDictionary(contentSource)
        );
    }

    private static StringName ResolveBodySizeCategoryForMember(
        PartyMemberState memberState,
        IdentityContentSource contentSource
    )
    {
        if (memberState == null)
            return "";

        var ascensionStageId = memberState.ascension_stage_id;
        if (ascensionStageId != "")
        {
            var ascensionStageDef = contentSource.GetAscensionStageDef(ascensionStageId);
            StringName ascensionBodySize =
                ascensionStageDef != null
                    ? ascensionStageDef.body_size_category_override
                    : new StringName("");
            if (
                ascensionBodySize != ""
                && BodySizeContentRules.IsValidBodySizeCategory(ascensionBodySize)
            )
                return ascensionBodySize;
        }

        var subraceId = memberState.subrace_id;
        var subraceDef = contentSource.GetSubraceDef(subraceId);
        StringName subraceBodySize =
            subraceDef != null ? subraceDef.body_size_category_override : new StringName("");
        if (
            subraceBodySize != ""
            && BodySizeContentRules.IsValidBodySizeCategory(subraceBodySize)
        )
            return subraceBodySize;

        var raceId = memberState.race_id;
        var raceDef = contentSource.GetRaceDef(raceId);
        StringName raceBodySize =
            raceDef != null ? raceDef.body_size_category : new StringName("");
        if (raceBodySize != "" && BodySizeContentRules.IsValidBodySizeCategory(raceBodySize))
            return raceBodySize;
        return "";
    }

    public static bool RefreshMemberBodySizeFromContentSource(
        PartyMemberState memberState,
        ProgressionContentRegistry contentSource
    )
    {
        return RefreshMemberBodySizeFromIdentity(
            memberState,
            IdentityContentSource.FromRegistry(contentSource)
        );
    }

    internal static bool RefreshMemberBodySizeFromIdentity(
        PartyMemberState memberState,
        GDictionary contentSource
    )
    {
        return RefreshMemberBodySizeFromIdentity(
            memberState,
            IdentityContentSource.FromDictionary(contentSource)
        );
    }

    private static bool RefreshMemberBodySizeFromIdentity(
        PartyMemberState memberState,
        IdentityContentSource contentSource
    )
    {
        var category = ResolveBodySizeCategoryForMember(memberState, contentSource);
        if (category == "")
            return false;

        memberState.body_size_category = category;
        memberState.body_size = BodySizeContentRules.GetBodySizeForCategory(category);
        return true;
    }

    private static RaceDef ValidateRace(
        List<string> errors,
        string label,
        StringName raceId,
        IdentityContentSource contentSource
    )
    {
        if (raceId == "")
        {
            errors.Add($"member {label} must have race_id");
            return default;
        }
        var raceDef = contentSource.GetRaceDef(raceId);
        if (raceDef == null)
            errors.Add($"member {label} references missing race {(string)raceId}");
        return raceDef;
    }

    private static SubraceDef ValidateSubrace(
        List<string> errors,
        string label,
        StringName subraceId,
        IdentityContentSource contentSource
    )
    {
        if (subraceId == "")
        {
            errors.Add($"member {label} must have subrace_id");
            return default;
        }
        var subraceDef = contentSource.GetSubraceDef(subraceId);
        if (subraceDef == null)
            errors.Add($"member {label} references missing subrace {(string)subraceId}");
        return subraceDef;
    }

    private static void ValidateRaceSubracePair(
        List<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        RaceDef raceDef,
        SubraceDef subraceDef
    )
    {
        if (raceDef == null || subraceDef == null || raceId == "" || subraceId == "")
            return;

        var parentRaceId = subraceDef.parent_race_id;
        if (parentRaceId != raceId)
            errors.Add(
                $"member {label} subrace {(string)subraceId} parent_race_id must be {(string)raceId}, got {(string)parentRaceId}"
            );

        if (!ContainsId(raceDef.subrace_ids, subraceId))
            errors.Add(
                $"member {label} race {(string)raceId} must list subrace {(string)subraceId} in subrace_ids"
            );
    }

    private static void ValidateBloodlinePair(
        List<string> errors,
        string label,
        StringName bloodlineId,
        StringName bloodlineStageId,
        IdentityContentSource contentSource
    )
    {
        if (bloodlineId == "" && bloodlineStageId == "")
            return;
        if (bloodlineId == "" || bloodlineStageId == "")
        {
            errors.Add(
                $"member {label} bloodline_id and bloodline_stage_id must both be empty or both be set"
            );
            return;
        }

        var bloodlineDef = contentSource.GetBloodlineDef(bloodlineId);
        var stageDef = contentSource.GetBloodlineStageDef(bloodlineStageId);
        if (bloodlineDef == null)
            errors.Add($"member {label} references missing bloodline {(string)bloodlineId}");
        if (stageDef == null)
            errors.Add(
                $"member {label} references missing bloodline stage {(string)bloodlineStageId}"
            );
        if (bloodlineDef == null || stageDef == null)
            return;

        var declaredBloodlineId = bloodlineDef.bloodline_id;
        var declaredStageId = stageDef.stage_id;
        var stageParentBloodlineId = stageDef.bloodline_id;
        if (
            declaredBloodlineId != bloodlineId
            || declaredStageId != bloodlineStageId
            || stageParentBloodlineId != bloodlineId
            || !ContainsId(bloodlineDef.stage_ids, bloodlineStageId)
        )
            errors.Add(
                $"member {label} bloodline_stage_id {(string)bloodlineStageId} does not belong to bloodline {(string)bloodlineId}"
            );
    }

    private static void ValidateAscensionPair(
        List<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        StringName ascensionStageId,
        IdentityContentSource contentSource
    )
    {
        if (ascensionId == "" && ascensionStageId == "")
            return;
        if (ascensionId == "" || ascensionStageId == "")
        {
            errors.Add(
                $"member {label} ascension_id and ascension_stage_id must both be empty or both be set"
            );
            return;
        }

        var ascensionDef = contentSource.GetAscensionDef(ascensionId);
        var stageDef = contentSource.GetAscensionStageDef(ascensionStageId);
        if (ascensionDef == null)
            errors.Add($"member {label} references missing ascension {(string)ascensionId}");
        if (stageDef == null)
            errors.Add(
                $"member {label} references missing ascension stage {(string)ascensionStageId}"
            );
        if (ascensionDef == null || stageDef == null)
            return;

        var declaredAscensionId = ascensionDef.ascension_id;
        var declaredStageId = stageDef.stage_id;
        var stageParentAscensionId = stageDef.ascension_id;
        if (
            declaredAscensionId != ascensionId
            || declaredStageId != ascensionStageId
            || stageParentAscensionId != ascensionId
            || !ContainsId(ascensionDef.stage_ids, ascensionStageId)
        )
            errors.Add(
                $"member {label} ascension_stage_id {(string)ascensionStageId} does not belong to ascension {(string)ascensionId}"
            );

        ValidateAscensionAllowedIdentity(
            errors,
            label,
            raceId,
            subraceId,
            bloodlineId,
            ascensionId,
            ascensionDef
        );
    }

    private static void ValidateAscensionAllowedIdentity(
        List<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        AscensionDef ascensionDef
    )
    {
        if (
            ascensionDef.allowed_race_ids != null
            && ascensionDef.allowed_race_ids.Count > 0
            && !ascensionDef.allowed_race_ids.Contains(raceId)
        )
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow race {(string)raceId}"
            );

        if (
            ascensionDef.allowed_subrace_ids != null
            && ascensionDef.allowed_subrace_ids.Count > 0
            && !ascensionDef.allowed_subrace_ids.Contains(subraceId)
        )
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow subrace {(string)subraceId}"
            );

        if (
            ascensionDef.allowed_bloodline_ids != null
            && ascensionDef.allowed_bloodline_ids.Count > 0
            && !ascensionDef.allowed_bloodline_ids.Contains(bloodlineId)
        )
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow bloodline {(string)bloodlineId}"
            );
    }

    private static bool ContainsId(
        Godot.Collections.Array<StringName> values,
        StringName expected
    )
    {
        return values != null && values.Contains(expected);
    }

    private static string MemberLabel(PartyMemberState memberState)
    {
        return memberState != null && memberState.member_id != ""
            ? (string)memberState.member_id
            : "<unknown>";
    }

    private sealed class IdentityContentSource
    {
        private static readonly IdentityContentSource NoSource = new(false);

        private readonly IReadOnlyDictionary<StringName, RaceDef> _raceDefs;
        private readonly IReadOnlyDictionary<StringName, SubraceDef> _subraceDefs;
        private readonly IReadOnlyDictionary<StringName, BloodlineDef> _bloodlineDefs;
        private readonly IReadOnlyDictionary<StringName, BloodlineStageDef> _bloodlineStageDefs;
        private readonly IReadOnlyDictionary<StringName, AscensionDef> _ascensionDefs;
        private readonly IReadOnlyDictionary<StringName, AscensionStageDef> _ascensionStageDefs;

        private IdentityContentSource(bool hasSource)
        {
            HasSource = hasSource;
            _raceDefs = new Dictionary<StringName, RaceDef>();
            _subraceDefs = new Dictionary<StringName, SubraceDef>();
            _bloodlineDefs = new Dictionary<StringName, BloodlineDef>();
            _bloodlineStageDefs = new Dictionary<StringName, BloodlineStageDef>();
            _ascensionDefs = new Dictionary<StringName, AscensionDef>();
            _ascensionStageDefs = new Dictionary<StringName, AscensionStageDef>();
        }

        private IdentityContentSource(
            IReadOnlyDictionary<StringName, RaceDef> raceDefs,
            IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
            IReadOnlyDictionary<StringName, BloodlineDef> bloodlineDefs,
            IReadOnlyDictionary<StringName, BloodlineStageDef> bloodlineStageDefs,
            IReadOnlyDictionary<StringName, AscensionDef> ascensionDefs,
            IReadOnlyDictionary<StringName, AscensionStageDef> ascensionStageDefs
        )
        {
            HasSource = true;
            _raceDefs = raceDefs ?? new Dictionary<StringName, RaceDef>();
            _subraceDefs = subraceDefs ?? new Dictionary<StringName, SubraceDef>();
            _bloodlineDefs = bloodlineDefs ?? new Dictionary<StringName, BloodlineDef>();
            _bloodlineStageDefs =
                bloodlineStageDefs ?? new Dictionary<StringName, BloodlineStageDef>();
            _ascensionDefs = ascensionDefs ?? new Dictionary<StringName, AscensionDef>();
            _ascensionStageDefs =
                ascensionStageDefs ?? new Dictionary<StringName, AscensionStageDef>();
        }

        internal bool HasSource { get; }

        internal static IdentityContentSource FromDictionary(GDictionary contentSource)
        {
            if (contentSource == null)
                return NoSource;
            return new IdentityContentSource(
                ProgressionContentBundleAdapter.ReadDefMap<RaceDef>(
                    contentSource,
                    "race_defs",
                    "race"
                ),
                ProgressionContentBundleAdapter.ReadDefMap<SubraceDef>(
                    contentSource,
                    "subrace_defs",
                    "subrace"
                ),
                ProgressionContentBundleAdapter.ReadDefMap<BloodlineDef>(
                    contentSource,
                    "bloodline_defs",
                    "bloodline"
                ),
                ProgressionContentBundleAdapter.ReadDefMap<BloodlineStageDef>(
                    contentSource,
                    "bloodline_stage_defs",
                    "bloodline_stage"
                ),
                ProgressionContentBundleAdapter.ReadDefMap<AscensionDef>(
                    contentSource,
                    "ascension_defs",
                    "ascension"
                ),
                ProgressionContentBundleAdapter.ReadDefMap<AscensionStageDef>(
                    contentSource,
                    "ascension_stage_defs",
                    "ascension_stage"
                )
            );
        }

        internal static IdentityContentSource FromRegistry(ProgressionContentRegistry registry)
        {
            if (registry == null)
                return NoSource;
            return new IdentityContentSource(
                ReadRegistryBucket<RaceDef>(registry.get_race_defs()),
                ReadRegistryBucket<SubraceDef>(registry.get_subrace_defs()),
                ReadRegistryBucket<BloodlineDef>(registry.get_bloodline_defs()),
                ReadRegistryBucket<BloodlineStageDef>(registry.get_bloodline_stage_defs()),
                ReadRegistryBucket<AscensionDef>(registry.get_ascension_defs()),
                ReadRegistryBucket<AscensionStageDef>(registry.get_ascension_stage_defs())
            );
        }

        internal RaceDef GetRaceDef(StringName defId) => Lookup(_raceDefs, defId);

        internal SubraceDef GetSubraceDef(StringName defId) => Lookup(_subraceDefs, defId);

        internal BloodlineDef GetBloodlineDef(StringName defId) => Lookup(_bloodlineDefs, defId);

        internal BloodlineStageDef GetBloodlineStageDef(StringName defId) =>
            Lookup(_bloodlineStageDefs, defId);

        internal AscensionDef GetAscensionDef(StringName defId) => Lookup(_ascensionDefs, defId);

        internal AscensionStageDef GetAscensionStageDef(StringName defId) =>
            Lookup(_ascensionStageDefs, defId);

        private static Dictionary<StringName, T> ReadRegistryBucket<T>(GDictionary bucket)
            where T : class
        {
            if (bucket == null)
                return new Dictionary<StringName, T>();
            return ProgressionContentBundleAdapter.ReadDefMap<T>(
                new GDictionary { ["defs"] = bucket },
                "defs",
                "defs"
            );
        }

        private static T Lookup<T>(IReadOnlyDictionary<StringName, T> map, StringName defId)
            where T : class
        {
            if (map == null || defId == "")
                return null;
            return map.TryGetValue(defId, out T value) ? value : null;
        }
    }
}

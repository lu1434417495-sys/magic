using System.Collections.Generic;
using Godot;

public static class IdentityPayloadValidator
{
    public static IReadOnlyList<string> ValidatePartyIdentityForContentSource(
        PartyState partyState,
        ProgressionContentRegistry contentSource
    ) =>
        ValidatePartyIdentityForContentSource(
            partyState,
            contentSource?.GetIdentityCatalogTyped()
        );

    public static IReadOnlyList<string> ValidatePartyIdentityForContentSource(
        PartyState partyState,
        ProgressionIdentityCatalogData contentSource
    ) => ValidatePartyIdentity(partyState, contentSource);

    private static List<string> ValidatePartyIdentity(
        PartyState partyState,
        ProgressionIdentityCatalogData contentSource
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

        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            foreach (string error in ValidateMemberIdentityTyped(memberState, contentSource))
                errors.Add(error);
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidateMemberIdentityForContentSource(
        PartyMemberState memberState,
        ProgressionContentRegistry contentSource
    ) =>
        ValidateMemberIdentityForContentSource(
            memberState,
            contentSource?.GetIdentityCatalogTyped()
        );

    public static IReadOnlyList<string> ValidateMemberIdentityForContentSource(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => ValidateMemberIdentityTyped(memberState, contentSource);

    internal static IReadOnlyList<string> ValidateMemberIdentityTyped(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => ValidateMemberIdentityCore(memberState, contentSource);

    private static List<string> ValidateMemberIdentityCore(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    )
    {
        var errors = new List<string>();
        if (memberState == null)
        {
            errors.Add("member identity payload is null");
            return errors;
        }
        if (!HasContentSource(contentSource))
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
    ) =>
        ResolveBodySizeCategoryForContentSource(
            memberState,
            contentSource?.GetIdentityCatalogTyped()
        );

    public static StringName ResolveBodySizeCategoryForContentSource(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => ResolveBodySizeCategoryForMemberTyped(memberState, contentSource);

    internal static StringName ResolveBodySizeCategoryForMemberTyped(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => ResolveBodySizeCategoryForMemberCore(memberState, contentSource);

    private static StringName ResolveBodySizeCategoryForMemberCore(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    )
    {
        if (memberState == null)
            return "";

        var ascensionStageId = memberState.ascension_stage_id;
        if (ascensionStageId != "")
        {
            var ascensionStageDef = Lookup(contentSource?.AscensionStageDefs, ascensionStageId);
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
        var subraceDef = Lookup(contentSource?.SubraceDefs, subraceId);
        StringName subraceBodySize =
            subraceDef != null ? subraceDef.body_size_category_override : new StringName("");
        if (
            subraceBodySize != ""
            && BodySizeContentRules.IsValidBodySizeCategory(subraceBodySize)
        )
            return subraceBodySize;

        var raceId = memberState.race_id;
        var raceDef = Lookup(contentSource?.RaceDefs, raceId);
        StringName raceBodySize =
            raceDef != null ? raceDef.body_size_category : new StringName("");
        if (raceBodySize != "" && BodySizeContentRules.IsValidBodySizeCategory(raceBodySize))
            return raceBodySize;
        return "";
    }

    public static bool RefreshMemberBodySizeFromContentSource(
        PartyMemberState memberState,
        ProgressionContentRegistry contentSource
    ) =>
        RefreshMemberBodySizeFromContentSource(
            memberState,
            contentSource?.GetIdentityCatalogTyped()
        );

    public static bool RefreshMemberBodySizeFromContentSource(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => RefreshMemberBodySizeFromIdentityTyped(memberState, contentSource);

    internal static bool RefreshMemberBodySizeFromIdentityTyped(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    ) => RefreshMemberBodySizeFromIdentityCore(memberState, contentSource);

    private static bool RefreshMemberBodySizeFromIdentityCore(
        PartyMemberState memberState,
        ProgressionIdentityCatalogData contentSource
    )
    {
        var category = ResolveBodySizeCategoryForMemberCore(memberState, contentSource);
        if (category == "")
            return false;

        return memberState.SetBodySizeCategory(category);
    }

    private static RaceDef ValidateRace(
        List<string> errors,
        string label,
        StringName raceId,
        ProgressionIdentityCatalogData contentSource
    )
    {
        if (raceId == "")
        {
            errors.Add($"member {label} must have race_id");
            return default;
        }
        var raceDef = Lookup(contentSource?.RaceDefs, raceId);
        if (raceDef == null)
            errors.Add($"member {label} references missing race {(string)raceId}");
        return raceDef;
    }

    private static SubraceDef ValidateSubrace(
        List<string> errors,
        string label,
        StringName subraceId,
        ProgressionIdentityCatalogData contentSource
    )
    {
        if (subraceId == "")
        {
            errors.Add($"member {label} must have subrace_id");
            return default;
        }
        var subraceDef = Lookup(contentSource?.SubraceDefs, subraceId);
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
        ProgressionIdentityCatalogData contentSource
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

        var bloodlineDef = Lookup(contentSource?.BloodlineDefs, bloodlineId);
        var stageDef = Lookup(contentSource?.BloodlineStageDefs, bloodlineStageId);
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
        ProgressionIdentityCatalogData contentSource
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

        var ascensionDef = Lookup(contentSource?.AscensionDefs, ascensionId);
        var stageDef = Lookup(contentSource?.AscensionStageDefs, ascensionStageId);
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

    private static bool HasContentSource(ProgressionIdentityCatalogData contentSource)
    {
        return contentSource != null;
    }

    private static T Lookup<T>(IReadOnlyDictionary<StringName, T> map, StringName defId)
        where T : class
    {
        if (map == null || defId == "")
            return null;
        return map.TryGetValue(defId, out T value) ? value : null;
    }
}

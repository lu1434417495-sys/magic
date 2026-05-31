using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class IdentityPayloadValidator : RefCounted
{
    public static Godot.Collections.Array<string> validate_party_identity_for_content_source(
        PartyState party_state,
        ProgressionContentRegistry content_source
    )
    {
        return ValidatePartyIdentity(party_state, IdentityContentSourceRef.FromRegistry(content_source));
    }

    public static Godot.Collections.Array<string> validate_party_identity(
        PartyState party_state,
        GDictionary content_source
    )
    {
        return ValidatePartyIdentity(
            party_state,
            IdentityContentSourceRef.FromDictionary(content_source)
        );
    }

    private static Godot.Collections.Array<string> ValidatePartyIdentity(
        PartyState partyState,
        IdentityContentSourceRef contentSource
    )
    {
        var errors = new Godot.Collections.Array<string>();
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

        foreach (var memberStateValue in partyState.member_states.Values)
        {
            var memberState = memberStateValue.AsGodotObject() as PartyMemberState;
            foreach (string error in ValidateMemberIdentity(memberState, contentSource))
                errors.Add(error);
        }
        return errors;
    }

    public static Godot.Collections.Array<string> validate_member_identity_for_content_source(
        PartyMemberState member_state,
        ProgressionContentRegistry content_source
    )
    {
        return ValidateMemberIdentity(
            member_state,
            IdentityContentSourceRef.FromRegistry(content_source)
        );
    }

    public static Godot.Collections.Array<string> validate_member_identity(
        PartyMemberState member_state,
        GDictionary content_source
    )
    {
        return ValidateMemberIdentity(
            member_state,
            IdentityContentSourceRef.FromDictionary(content_source)
        );
    }

    private static Godot.Collections.Array<string> ValidateMemberIdentity(
        PartyMemberState memberState,
        IdentityContentSourceRef contentSource
    )
    {
        var errors = new Godot.Collections.Array<string>();
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

    public static StringName resolve_body_size_category_for_content_source(
        PartyMemberState member_state,
        ProgressionContentRegistry content_source
    )
    {
        return ResolveBodySizeCategoryForMember(
            member_state,
            IdentityContentSourceRef.FromRegistry(content_source)
        );
    }

    public static StringName resolve_body_size_category_for_member(
        PartyMemberState member_state,
        GDictionary content_source
    )
    {
        return ResolveBodySizeCategoryForMember(
            member_state,
            IdentityContentSourceRef.FromDictionary(content_source)
        );
    }

    private static StringName ResolveBodySizeCategoryForMember(
        PartyMemberState memberState,
        IdentityContentSourceRef contentSource
    )
    {
        if (memberState == null)
            return "";

        var ascensionStageId = memberState.ascension_stage_id;
        if (ascensionStageId != "")
        {
            var ascensionStageDef = contentSource.GetContentDef<AscensionStageDef>(
                "get_ascension_stage_defs",
                "ascension_stage_defs",
                "ascension_stage",
                ascensionStageId
            );
            StringName ascensionBodySize =
                ascensionStageDef != null
                    ? ascensionStageDef.body_size_category_override
                    : new StringName("");
            if (
                ascensionBodySize != ""
                && BodySizeRules.is_valid_body_size_category(ascensionBodySize)
            )
                return ascensionBodySize;
        }

        var subraceId = memberState.subrace_id;
        var subraceDef = contentSource.GetContentDef<SubraceDef>(
            "get_subrace_defs",
            "subrace_defs",
            "subrace",
            subraceId
        );
        StringName subraceBodySize =
            subraceDef != null ? subraceDef.body_size_category_override : new StringName("");
        if (subraceBodySize != "" && BodySizeRules.is_valid_body_size_category(subraceBodySize))
            return subraceBodySize;

        var raceId = memberState.race_id;
        var raceDef = contentSource.GetContentDef<RaceDef>(
            "get_race_defs",
            "race_defs",
            "race",
            raceId
        );
        StringName raceBodySize =
            raceDef != null ? raceDef.body_size_category : new StringName("");
        if (raceBodySize != "" && BodySizeRules.is_valid_body_size_category(raceBodySize))
            return raceBodySize;
        return "";
    }

    public static bool refresh_member_body_size_from_content_source(
        PartyMemberState member_state,
        ProgressionContentRegistry content_source
    )
    {
        return RefreshMemberBodySizeFromIdentity(
            member_state,
            IdentityContentSourceRef.FromRegistry(content_source)
        );
    }

    public static bool refresh_member_body_size_from_identity(
        PartyMemberState member_state,
        GDictionary content_source
    )
    {
        return RefreshMemberBodySizeFromIdentity(
            member_state,
            IdentityContentSourceRef.FromDictionary(content_source)
        );
    }

    private static bool RefreshMemberBodySizeFromIdentity(
        PartyMemberState memberState,
        IdentityContentSourceRef contentSource
    )
    {
        var category = ResolveBodySizeCategoryForMember(memberState, contentSource);
        if (category == "")
            return false;

        memberState.body_size_category = category;
        memberState.body_size = BodySizeRules.get_body_size_for_category(category);
        return true;
    }

    private static RaceDef ValidateRace(
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        IdentityContentSourceRef contentSource
    )
    {
        if (raceId == "")
        {
            errors.Add($"member {label} must have race_id");
            return default;
        }
        var raceDef = contentSource.GetContentDef<RaceDef>(
            "get_race_defs",
            "race_defs",
            "race",
            raceId
        );
        if (raceDef == null)
            errors.Add($"member {label} references missing race {(string)raceId}");
        return raceDef;
    }

    private static SubraceDef ValidateSubrace(
        Godot.Collections.Array<string> errors,
        string label,
        StringName subraceId,
        IdentityContentSourceRef contentSource
    )
    {
        if (subraceId == "")
        {
            errors.Add($"member {label} must have subrace_id");
            return default;
        }
        var subraceDef = contentSource.GetContentDef<SubraceDef>(
            "get_subrace_defs",
            "subrace_defs",
            "subrace",
            subraceId
        );
        if (subraceDef == null)
            errors.Add($"member {label} references missing subrace {(string)subraceId}");
        return subraceDef;
    }

    private static void ValidateRaceSubracePair(
        Godot.Collections.Array<string> errors,
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

        var raceSubraceIds = raceDef.subrace_ids ?? new Godot.Collections.Array<StringName>();
        if (!raceSubraceIds.Contains(subraceId))
            errors.Add(
                $"member {label} race {(string)raceId} must list subrace {(string)subraceId} in subrace_ids"
            );
    }

    private static void ValidateBloodlinePair(
        Godot.Collections.Array<string> errors,
        string label,
        StringName bloodlineId,
        StringName bloodlineStageId,
        IdentityContentSourceRef contentSource
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

        var bloodlineDef = contentSource.GetContentDef<BloodlineDef>(
            "get_bloodline_defs",
            "bloodline_defs",
            "bloodline",
            bloodlineId
        );
        var stageDef = contentSource.GetContentDef<BloodlineStageDef>(
            "get_bloodline_stage_defs",
            "bloodline_stage_defs",
            "bloodline_stage",
            bloodlineStageId
        );
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
        var bloodlineStageIds =
            bloodlineDef.stage_ids ?? new Godot.Collections.Array<StringName>();
        if (
            declaredBloodlineId != bloodlineId
            || declaredStageId != bloodlineStageId
            || stageParentBloodlineId != bloodlineId
            || !bloodlineStageIds.Contains(bloodlineStageId)
        )
            errors.Add(
                $"member {label} bloodline_stage_id {(string)bloodlineStageId} does not belong to bloodline {(string)bloodlineId}"
            );
    }

    private static void ValidateAscensionPair(
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        StringName ascensionStageId,
        IdentityContentSourceRef contentSource
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

        var ascensionDef = contentSource.GetContentDef<AscensionDef>(
            "get_ascension_defs",
            "ascension_defs",
            "ascension",
            ascensionId
        );
        var stageDef = contentSource.GetContentDef<AscensionStageDef>(
            "get_ascension_stage_defs",
            "ascension_stage_defs",
            "ascension_stage",
            ascensionStageId
        );
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
        var ascensionStageIds =
            ascensionDef.stage_ids ?? new Godot.Collections.Array<StringName>();
        if (
            declaredAscensionId != ascensionId
            || declaredStageId != ascensionStageId
            || stageParentAscensionId != ascensionId
            || !ascensionStageIds.Contains(ascensionStageId)
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
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        AscensionDef ascensionDef
    )
    {
        var allowedRaceIds =
            ascensionDef.allowed_race_ids ?? new Godot.Collections.Array<StringName>();
        if (allowedRaceIds.Count > 0 && !allowedRaceIds.Contains(raceId))
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow race {(string)raceId}"
            );

        var allowedSubraceIds =
            ascensionDef.allowed_subrace_ids ?? new Godot.Collections.Array<StringName>();
        if (allowedSubraceIds.Count > 0 && !allowedSubraceIds.Contains(subraceId))
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow subrace {(string)subraceId}"
            );

        var allowedBloodlineIds =
            ascensionDef.allowed_bloodline_ids ?? new Godot.Collections.Array<StringName>();
        if (allowedBloodlineIds.Count > 0 && !allowedBloodlineIds.Contains(bloodlineId))
            errors.Add(
                $"member {label} ascension {(string)ascensionId} does not allow bloodline {(string)bloodlineId}"
            );
    }

    private static string MemberLabel(PartyMemberState memberState)
    {
        return memberState != null && memberState.member_id != ""
            ? (string)memberState.member_id
            : "<unknown>";
    }

    private readonly struct IdentityContentSourceRef
    {
        private readonly GDictionary _dictionary;
        private readonly ProgressionContentRegistry _registry;

        private IdentityContentSourceRef(GDictionary dictionary, ProgressionContentRegistry registry)
        {
            _dictionary = dictionary;
            _registry = registry;
        }

        internal bool HasSource => _dictionary != null || _registry != null;

        internal static IdentityContentSourceRef FromDictionary(GDictionary dictionary)
        {
            return dictionary != null ? new IdentityContentSourceRef(dictionary, null) : new();
        }

        internal static IdentityContentSourceRef FromRegistry(ProgressionContentRegistry registry)
        {
            return registry != null ? new IdentityContentSourceRef(null, registry) : new();
        }

        internal T GetContentDef<T>(
            string methodName,
            string primaryBucketName,
            string aliasBucketName,
            StringName defId
        ) where T : class
        {
            if (defId == "")
                return null;
            return LookupBucketEntry<T>(
                GetContentBucket(methodName, primaryBucketName, aliasBucketName),
                defId
            );
        }

        private Godot.Collections.Dictionary GetContentBucket(
            string methodName,
            string primaryBucketName,
            string aliasBucketName
        )
        {
            if (_registry != null)
            {
                return methodName switch
                {
                    "get_race_defs" => _registry.get_race_defs(),
                    "get_subrace_defs" => _registry.get_subrace_defs(),
                    "get_bloodline_defs" => _registry.get_bloodline_defs(),
                    "get_bloodline_stage_defs" => _registry.get_bloodline_stage_defs(),
                    "get_ascension_defs" => _registry.get_ascension_defs(),
                    "get_ascension_stage_defs" => _registry.get_ascension_stage_defs(),
                    _ => new Godot.Collections.Dictionary(),
                };
            }
            if (_dictionary != null)
            {
                if (_dictionary.ContainsKey(primaryBucketName))
                {
                    var primaryBucket = _dictionary[primaryBucketName];
                    if (primaryBucket.VariantType == Variant.Type.Dictionary)
                        return primaryBucket.AsGodotDictionary();
                }
                if (_dictionary.ContainsKey(aliasBucketName))
                {
                    var aliasBucket = _dictionary[aliasBucketName];
                    if (aliasBucket.VariantType == Variant.Type.Dictionary)
                        return aliasBucket.AsGodotDictionary();
                }
            }
            return new Godot.Collections.Dictionary();
        }

        private static T LookupBucketEntry<T>(
            Godot.Collections.Dictionary bucket,
            StringName defId
        ) where T : class
        {
            if (bucket == null || defId == "")
                return null;
            if (bucket.ContainsKey(defId))
            {
                T stringNameValue = bucket[defId].AsGodotObject() as T;
                if (stringNameValue != null)
                    return stringNameValue;
            }
            string textId = (string)defId;
            if (bucket.ContainsKey(textId))
            {
                T stringValue = bucket[textId].AsGodotObject() as T;
                if (stringValue != null)
                    return stringValue;
            }
            return null;
        }
    }
}

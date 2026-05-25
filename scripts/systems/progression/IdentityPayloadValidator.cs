using Godot;

[GlobalClass]
public partial class IdentityPayloadValidator : RefCounted
{
    public static Godot.Collections.Array<string> validate_party_identity(Variant party_state, Variant content_source)
    {
        var errors = new Godot.Collections.Array<string>();
        if (party_state.VariantType == Variant.Type.Nil)
        {
            errors.Add("party identity payload is null");
            return errors;
        }

        var memberStatesVariant = ReadProperty(party_state, "member_states");
        if (memberStatesVariant.VariantType != Variant.Type.Dictionary)
        {
            errors.Add("party identity payload has no member_states dictionary");
            return errors;
        }

        foreach (var memberState in memberStatesVariant.AsGodotDictionary().Values)
        {
            foreach (string error in validate_member_identity(memberState, content_source))
                errors.Add(error);
        }
        return errors;
    }

    public static Godot.Collections.Array<string> validate_member_identity(Variant member_state, Variant content_source)
    {
        var errors = new Godot.Collections.Array<string>();
        if (member_state.VariantType == Variant.Type.Nil)
        {
            errors.Add("member identity payload is null");
            return errors;
        }
        if (content_source.VariantType == Variant.Type.Nil)
        {
            errors.Add($"member {MemberLabel(member_state)} identity validation requires content source");
            return errors;
        }

        string label = MemberLabel(member_state);
        var raceId = MemberStringName(member_state, "race_id");
        var subraceId = MemberStringName(member_state, "subrace_id");
        var bloodlineId = MemberStringName(member_state, "bloodline_id");
        var bloodlineStageId = MemberStringName(member_state, "bloodline_stage_id");
        var ascensionId = MemberStringName(member_state, "ascension_id");
        var ascensionStageId = MemberStringName(member_state, "ascension_stage_id");

        var raceDef = ValidateRace(errors, label, raceId, content_source);
        var subraceDef = ValidateSubrace(errors, label, subraceId, content_source);
        ValidateRaceSubracePair(errors, label, raceId, subraceId, raceDef, subraceDef);
        ValidateBloodlinePair(errors, label, bloodlineId, bloodlineStageId, content_source);
        ValidateAscensionPair(errors, label, raceId, subraceId, bloodlineId, ascensionId, ascensionStageId, content_source);
        return errors;
    }

    public static StringName resolve_body_size_category_for_member(Variant member_state, Variant content_source)
    {
        if (member_state.VariantType == Variant.Type.Nil)
            return "";

        var ascensionStageId = MemberStringName(member_state, "ascension_stage_id");
        if (ascensionStageId != "")
        {
            var ascensionStageDef = GetContentDef(content_source, "get_ascension_stage_defs", "ascension_stage_defs", "ascension_stage", ascensionStageId);
            var ascensionBodySize = DefStringName(ascensionStageDef, "body_size_category_override");
            if (ascensionBodySize != "" && BodySizeRules.is_valid_body_size_category(ascensionBodySize))
                return ascensionBodySize;
        }

        var subraceId = MemberStringName(member_state, "subrace_id");
        var subraceDef = GetContentDef(content_source, "get_subrace_defs", "subrace_defs", "subrace", subraceId);
        var subraceBodySize = DefStringName(subraceDef, "body_size_category_override");
        if (subraceBodySize != "" && BodySizeRules.is_valid_body_size_category(subraceBodySize))
            return subraceBodySize;

        var raceId = MemberStringName(member_state, "race_id");
        var raceDef = GetContentDef(content_source, "get_race_defs", "race_defs", "race", raceId);
        var raceBodySize = DefStringName(raceDef, "body_size_category");
        if (raceBodySize != "" && BodySizeRules.is_valid_body_size_category(raceBodySize))
            return raceBodySize;
        return "";
    }

    public static bool refresh_member_body_size_from_identity(Variant member_state, Variant content_source)
    {
        var category = resolve_body_size_category_for_member(member_state, content_source);
        if (category == "")
            return false;

        WriteProperty(member_state, "body_size_category", category);
        WriteProperty(member_state, "body_size", BodySizeRules.get_body_size_for_category(category));
        return true;
    }

    private static Variant ValidateRace(Godot.Collections.Array<string> errors, string label, StringName raceId, Variant contentSource)
    {
        if (raceId == "")
        {
            errors.Add($"member {label} must have race_id");
            return default;
        }
        var raceDef = GetContentDef(contentSource, "get_race_defs", "race_defs", "race", raceId);
        if (raceDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing race {(string)raceId}");
        return raceDef;
    }

    private static Variant ValidateSubrace(Godot.Collections.Array<string> errors, string label, StringName subraceId, Variant contentSource)
    {
        if (subraceId == "")
        {
            errors.Add($"member {label} must have subrace_id");
            return default;
        }
        var subraceDef = GetContentDef(contentSource, "get_subrace_defs", "subrace_defs", "subrace", subraceId);
        if (subraceDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing subrace {(string)subraceId}");
        return subraceDef;
    }

    private static void ValidateRaceSubracePair(
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        Variant raceDef,
        Variant subraceDef
    )
    {
        if (raceDef.VariantType == Variant.Type.Nil || subraceDef.VariantType == Variant.Type.Nil || raceId == "" || subraceId == "")
            return;

        var parentRaceId = DefStringName(subraceDef, "parent_race_id");
        if (parentRaceId != raceId)
            errors.Add($"member {label} subrace {(string)subraceId} parent_race_id must be {(string)raceId}, got {(string)parentRaceId}");

        var raceSubraceIds = DefStringNameArray(raceDef, "subrace_ids");
        if (!raceSubraceIds.Contains(subraceId))
            errors.Add($"member {label} race {(string)raceId} must list subrace {(string)subraceId} in subrace_ids");
    }

    private static void ValidateBloodlinePair(
        Godot.Collections.Array<string> errors,
        string label,
        StringName bloodlineId,
        StringName bloodlineStageId,
        Variant contentSource
    )
    {
        if (bloodlineId == "" && bloodlineStageId == "")
            return;
        if (bloodlineId == "" || bloodlineStageId == "")
        {
            errors.Add($"member {label} bloodline_id and bloodline_stage_id must both be empty or both be set");
            return;
        }

        var bloodlineDef = GetContentDef(contentSource, "get_bloodline_defs", "bloodline_defs", "bloodline", bloodlineId);
        var stageDef = GetContentDef(contentSource, "get_bloodline_stage_defs", "bloodline_stage_defs", "bloodline_stage", bloodlineStageId);
        if (bloodlineDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing bloodline {(string)bloodlineId}");
        if (stageDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing bloodline stage {(string)bloodlineStageId}");
        if (bloodlineDef.VariantType == Variant.Type.Nil || stageDef.VariantType == Variant.Type.Nil)
            return;

        var declaredBloodlineId = DefStringName(bloodlineDef, "bloodline_id");
        var declaredStageId = DefStringName(stageDef, "stage_id");
        var stageParentBloodlineId = DefStringName(stageDef, "bloodline_id");
        var bloodlineStageIds = DefStringNameArray(bloodlineDef, "stage_ids");
        if (declaredBloodlineId != bloodlineId || declaredStageId != bloodlineStageId || stageParentBloodlineId != bloodlineId || !bloodlineStageIds.Contains(bloodlineStageId))
            errors.Add($"member {label} bloodline_stage_id {(string)bloodlineStageId} does not belong to bloodline {(string)bloodlineId}");
    }

    private static void ValidateAscensionPair(
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        StringName ascensionStageId,
        Variant contentSource
    )
    {
        if (ascensionId == "" && ascensionStageId == "")
            return;
        if (ascensionId == "" || ascensionStageId == "")
        {
            errors.Add($"member {label} ascension_id and ascension_stage_id must both be empty or both be set");
            return;
        }

        var ascensionDef = GetContentDef(contentSource, "get_ascension_defs", "ascension_defs", "ascension", ascensionId);
        var stageDef = GetContentDef(contentSource, "get_ascension_stage_defs", "ascension_stage_defs", "ascension_stage", ascensionStageId);
        if (ascensionDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing ascension {(string)ascensionId}");
        if (stageDef.VariantType == Variant.Type.Nil)
            errors.Add($"member {label} references missing ascension stage {(string)ascensionStageId}");
        if (ascensionDef.VariantType == Variant.Type.Nil || stageDef.VariantType == Variant.Type.Nil)
            return;

        var declaredAscensionId = DefStringName(ascensionDef, "ascension_id");
        var declaredStageId = DefStringName(stageDef, "stage_id");
        var stageParentAscensionId = DefStringName(stageDef, "ascension_id");
        var ascensionStageIds = DefStringNameArray(ascensionDef, "stage_ids");
        if (declaredAscensionId != ascensionId || declaredStageId != ascensionStageId || stageParentAscensionId != ascensionId || !ascensionStageIds.Contains(ascensionStageId))
            errors.Add($"member {label} ascension_stage_id {(string)ascensionStageId} does not belong to ascension {(string)ascensionId}");

        ValidateAscensionAllowedIdentity(errors, label, raceId, subraceId, bloodlineId, ascensionId, ascensionDef);
    }

    private static void ValidateAscensionAllowedIdentity(
        Godot.Collections.Array<string> errors,
        string label,
        StringName raceId,
        StringName subraceId,
        StringName bloodlineId,
        StringName ascensionId,
        Variant ascensionDef
    )
    {
        var allowedRaceIds = DefStringNameArray(ascensionDef, "allowed_race_ids");
        if (allowedRaceIds.Count > 0 && !allowedRaceIds.Contains(raceId))
            errors.Add($"member {label} ascension {(string)ascensionId} does not allow race {(string)raceId}");

        var allowedSubraceIds = DefStringNameArray(ascensionDef, "allowed_subrace_ids");
        if (allowedSubraceIds.Count > 0 && !allowedSubraceIds.Contains(subraceId))
            errors.Add($"member {label} ascension {(string)ascensionId} does not allow subrace {(string)subraceId}");

        var allowedBloodlineIds = DefStringNameArray(ascensionDef, "allowed_bloodline_ids");
        if (allowedBloodlineIds.Count > 0 && !allowedBloodlineIds.Contains(bloodlineId))
            errors.Add($"member {label} ascension {(string)ascensionId} does not allow bloodline {(string)bloodlineId}");
    }

    private static Variant GetContentDef(Variant contentSource, string methodName, string primaryBucketName, string aliasBucketName, StringName defId)
    {
        if (contentSource.VariantType == Variant.Type.Nil || defId == "")
            return default;
        var bucket = GetContentBucket(contentSource, methodName, primaryBucketName, aliasBucketName);
        return LookupBucketEntry(bucket, defId);
    }

    private static Godot.Collections.Dictionary GetContentBucket(Variant contentSource, string methodName, string primaryBucketName, string aliasBucketName)
    {
        if (contentSource.VariantType == Variant.Type.Dictionary)
        {
            var dict = contentSource.AsGodotDictionary();
            if (TryGetDictionaryValue(dict, primaryBucketName, out var primaryBucket) && primaryBucket.VariantType == Variant.Type.Dictionary)
                return primaryBucket.AsGodotDictionary();
            if (TryGetDictionaryValue(dict, aliasBucketName, out var aliasBucket) && aliasBucket.VariantType == Variant.Type.Dictionary)
                return aliasBucket.AsGodotDictionary();
        }
        if (contentSource.VariantType == Variant.Type.Object)
        {
            var obj = contentSource.AsGodotObject();
            if (obj != null && obj.HasMethod(methodName))
            {
                var methodBucket = obj.Call(methodName);
                if (methodBucket.VariantType == Variant.Type.Dictionary)
                    return methodBucket.AsGodotDictionary();
            }
        }
        return new Godot.Collections.Dictionary();
    }

    private static Variant LookupBucketEntry(Godot.Collections.Dictionary bucket, StringName defId)
    {
        if (bucket.ContainsKey(defId))
            return bucket[defId];
        string textId = (string)defId;
        if (bucket.ContainsKey(textId))
            return bucket[textId];
        return default;
    }

    private static string MemberLabel(Variant memberState)
    {
        var memberId = MemberStringName(memberState, "member_id");
        return memberId != "" ? (string)memberId : "<unknown>";
    }

    private static StringName MemberStringName(Variant memberState, string propertyName)
    {
        return ToStringName(ReadProperty(memberState, propertyName));
    }

    private static StringName DefStringName(Variant def, string propertyName)
    {
        return ToStringName(ReadProperty(def, propertyName));
    }

    private static Godot.Collections.Array<StringName> DefStringNameArray(Variant def, string propertyName)
    {
        var result = new Godot.Collections.Array<StringName>();
        var value = ReadProperty(def, propertyName);
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (var item in value.AsGodotArray())
        {
            var parsed = ToStringName(item);
            if (parsed != "")
                result.Add(parsed);
        }
        return result;
    }

    private static Variant ReadProperty(Variant source, string propertyName)
    {
        if (source.VariantType == Variant.Type.Nil)
            return default;
        if (source.VariantType == Variant.Type.Dictionary)
        {
            var dict = source.AsGodotDictionary();
            if (TryGetDictionaryValue(dict, propertyName, out var stringValue))
                return stringValue;
            if (TryGetDictionaryValue(dict, new StringName(propertyName), out var stringNameValue))
                return stringNameValue;
            return default;
        }
        if (source.VariantType == Variant.Type.Object)
        {
            var obj = source.AsGodotObject();
            return obj != null ? obj.Get(propertyName) : default;
        }
        return default;
    }

    private static void WriteProperty(Variant source, string propertyName, Variant value)
    {
        if (source.VariantType == Variant.Type.Dictionary)
        {
            source.AsGodotDictionary()[propertyName] = value;
            return;
        }
        if (source.VariantType == Variant.Type.Object)
            source.AsGodotObject()?.Set(propertyName, value);
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName("")
        };
    }

    private static bool TryGetDictionaryValue(Godot.Collections.Dictionary dict, Variant key, out Variant value)
    {
        if (dict.ContainsKey(key))
        {
            value = dict[key];
            return true;
        }
        value = default;
        return false;
    }
}

using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class CharacterCreationIdentityOptionService : RefCounted
{
    public static Godot.Collections.Array<StringName> collect_creation_race_ids(Variant content_source)
    {
        var ids = new Godot.Collections.Array<StringName>();
        var raceDefs = GetContentBucket(content_source, "get_race_defs", "race_defs", "race");
        foreach (var raceId in SortedBucketIds(raceDefs))
        {
            if (collect_subrace_ids_for_race(content_source, raceId).Count > 0)
                ids.Add(raceId);
        }
        return ids;
    }

    public static Godot.Collections.Array<StringName> collect_subrace_ids_for_race(Variant content_source, StringName race_id)
    {
        var ids = new Godot.Collections.Array<StringName>();
        if (race_id == "")
            return ids;

        var raceDef = GetContentDef(content_source, "get_race_defs", "race_defs", "race", race_id);
        if (raceDef.VariantType == Variant.Type.Nil)
            return ids;

        var subraceDefs = GetContentBucket(content_source, "get_subrace_defs", "subrace_defs", "subrace");
        foreach (var subraceId in DefStringNameArray(raceDef, "subrace_ids"))
        {
            if (subraceId == "" || ids.Contains(subraceId))
                continue;
            var subraceDef = LookupBucketEntry(subraceDefs, subraceId);
            if (subraceDef.VariantType == Variant.Type.Nil)
                continue;
            if (DefStringName(subraceDef, "parent_race_id") != race_id)
                continue;
            if (!is_valid_creation_race_subrace_pair(content_source, race_id, subraceId))
                continue;
            ids.Add(subraceId);
        }
        return SortStringNames(ids);
    }

    public static StringName choose_race_id(Variant content_source, StringName current_id, StringName default_id)
    {
        var candidates = collect_creation_race_ids(content_source);
        if (current_id != "" && candidates.Contains(current_id))
            return current_id;
        if (default_id != "" && candidates.Contains(default_id))
            return default_id;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static StringName choose_subrace_id(Variant content_source, StringName race_id, StringName current_id)
    {
        var candidates = collect_subrace_ids_for_race(content_source, race_id);
        if (current_id != "" && candidates.Contains(current_id))
            return current_id;

        var raceDef = GetContentDef(content_source, "get_race_defs", "race_defs", "race", race_id);
        var defaultSubraceId = DefStringName(raceDef, "default_subrace_id");
        if (defaultSubraceId != "" && candidates.Contains(defaultSubraceId))
            return defaultSubraceId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static bool is_valid_creation_race_subrace_pair(Variant content_source, StringName race_id, StringName subrace_id)
    {
        if (content_source.VariantType == Variant.Type.Nil || race_id == "" || subrace_id == "")
            return false;

        var memberState = new PartyMemberState
        {
            member_id = "character_creation_candidate",
            race_id = race_id,
            subrace_id = subrace_id,
            bloodline_id = "",
            bloodline_stage_id = "",
            ascension_id = "",
            ascension_stage_id = "",
        };
        return IdentityPayloadValidator.validate_member_identity(memberState, content_source).Count == 0;
    }

    private static Godot.Collections.Array<StringName> SortedBucketIds(Godot.Collections.Dictionary bucket)
    {
        var ids = new Godot.Collections.Array<StringName>();
        foreach (var key in bucket.Keys)
        {
            var id = new StringName(key.AsString());
            if (id != "" && !ids.Contains(id))
                ids.Add(id);
        }
        return SortStringNames(ids);
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
        var textId = (string)defId;
        if (bucket.ContainsKey(textId))
            return bucket[textId];
        return default;
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

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName("")
        };
    }

    private static Godot.Collections.Array<StringName> SortStringNames(Godot.Collections.Array<StringName> ids)
    {
        var values = new List<StringName>();
        foreach (var id in ids)
            values.Add(id);
        values.Sort((left, right) => string.CompareOrdinal((string)left, (string)right));

        var result = new Godot.Collections.Array<StringName>();
        foreach (var id in values)
            result.Add(id);
        return result;
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

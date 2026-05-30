using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class CharacterCreationIdentityOptionService : RefCounted
{
    public static Godot.Collections.Array<StringName> collect_creation_race_ids(
        ProgressionContentRegistry content_source
    )
    {
        var ids = new Godot.Collections.Array<StringName>();
        var raceDefs = content_source != null ? content_source.get_race_defs() : new Godot.Collections.Dictionary();
        foreach (var raceId in SortedBucketIds(raceDefs))
        {
            if (collect_subrace_ids_for_race(content_source, raceId).Count > 0)
                ids.Add(raceId);
        }
        return ids;
    }

    public static Godot.Collections.Array<StringName> collect_subrace_ids_for_race(
        ProgressionContentRegistry content_source,
        StringName race_id
    )
    {
        var ids = new Godot.Collections.Array<StringName>();
        if (race_id == "")
            return ids;

        var raceDef = LookupBucketEntry(content_source.get_race_defs(), race_id);
        if (raceDef == null)
            return ids;

        var subraceDefs = content_source.get_subrace_defs();
        foreach (var subraceId in DefStringNameArray(raceDef, "subrace_ids"))
        {
            if (subraceId == "" || ids.Contains(subraceId))
                continue;
            var subraceDef = LookupBucketEntry(subraceDefs, subraceId);
            if (subraceDef == null)
                continue;
            if (DefStringName(subraceDef, "parent_race_id") != race_id)
                continue;
            if (!is_valid_creation_race_subrace_pair(content_source, race_id, subraceId))
                continue;
            ids.Add(subraceId);
        }
        return SortStringNames(ids);
    }

    public static StringName choose_race_id(
        ProgressionContentRegistry content_source,
        StringName current_id,
        StringName default_id
    )
    {
        var candidates = collect_creation_race_ids(content_source);
        if (current_id != "" && candidates.Contains(current_id))
            return current_id;
        if (default_id != "" && candidates.Contains(default_id))
            return default_id;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static StringName choose_subrace_id(
        ProgressionContentRegistry content_source,
        StringName race_id,
        StringName current_id
    )
    {
        var candidates = collect_subrace_ids_for_race(content_source, race_id);
        if (current_id != "" && candidates.Contains(current_id))
            return current_id;

        var raceDef = LookupBucketEntry(content_source.get_race_defs(), race_id);
        var defaultSubraceId = DefStringName(raceDef, "default_subrace_id");
        if (defaultSubraceId != "" && candidates.Contains(defaultSubraceId))
            return defaultSubraceId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static bool is_valid_creation_race_subrace_pair(
        ProgressionContentRegistry content_source,
        StringName race_id,
        StringName subrace_id
    )
    {
        if (content_source == null || race_id == "" || subrace_id == "")
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
        return IdentityPayloadValidator
                .validate_member_identity_for_content_source(memberState, content_source)
                .Count
            == 0;
    }

    private static Godot.Collections.Array<StringName> SortedBucketIds(
        Godot.Collections.Dictionary bucket
    )
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



    private static GodotObject LookupBucketEntry(Godot.Collections.Dictionary bucket, StringName defId)
    {
        if (bucket == null || defId == "")
            return null;
        if (TryGetObject(bucket, defId, out var stringNameValue))
            return stringNameValue;
        var textId = (string)defId;
        if (TryGetObject(bucket, textId, out var stringValue))
            return stringValue;
        return null;
    }

    private static StringName DefStringName(GodotObject def, string propertyName)
    {
        if (def == null)
            return new StringName("");
        var value = def.Get(propertyName);
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static Godot.Collections.Array<StringName> DefStringNameArray(
        GodotObject def,
        string propertyName
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (def == null)
            return result;
        var value = def.Get(propertyName);
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (var item in value.AsGodotArray())
        {
            var parsed = item.VariantType switch
            {
                Variant.Type.StringName => item.AsStringName(),
                Variant.Type.String => new StringName(item.AsString()),
                _ => new StringName(""),
            };
            if (parsed != "")
                result.Add(parsed);
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> SortStringNames(
        Godot.Collections.Array<StringName> ids
    )
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

    private static bool TryGetObject(
        Godot.Collections.Dictionary dict,
        StringName key,
        out GodotObject value
    )
    {
        if (dict.ContainsKey(key))
        {
            value = dict[key].AsGodotObject();
            return value != null;
        }
        value = null;
        return false;
    }

    private static bool TryGetObject(
        Godot.Collections.Dictionary dict,
        string key,
        out GodotObject value
    )
    {
        if (dict.ContainsKey(key))
        {
            value = dict[key].AsGodotObject();
            return value != null;
        }
        value = null;
        return false;
    }
}

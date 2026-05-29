using Godot;
using GDictionary = Godot.Collections.Dictionary;
using System;

[GlobalClass]
public partial class BattleSimOverrideApplier : RefCounted
{
    public GDictionary apply_profile(
        GDictionary skill_defs,
        GDictionary enemy_ai_brains,
        BattleSimProfileDef profile
    )
    {
        var cloned_skill_defs = _duplicate_resource_dict(skill_defs);

        var cloned_enemy_ai_brains = _duplicate_resource_dict(enemy_ai_brains);

        GodotObject ai_score_profile =
            profile?.ai_score_profile != null
                ? profile.ai_score_profile.Call("duplicate", true).AsGodotObject()
                : new BattleAiScoreProfile();

        var errors = new Godot.Collections.Array<string>();

        if (profile != null)
        {
            foreach (var patch_entry in profile.override_patches)
            {
                if (patch_entry.VariantType != Variant.Type.Dictionary)
                {
                    errors.Add(
                        "Battle sim profile "
                            + profile.profile_id
                            + " contains a non-Dictionary override patch."
                    );

                    continue;
                }

                errors.AddRange(
                    _apply_patch_entry(
                        cloned_skill_defs,
                        cloned_enemy_ai_brains,
                        ai_score_profile,
                        patch_entry.AsGodotDictionary()
                    )
                );
            }
        }

        foreach (var error in errors)
            GameLog.Error(error, "battlesim.override.failed", "battlesim");

        return new GDictionary
        {
            ["skill_defs"] = cloned_skill_defs,

            ["enemy_ai_brains"] = cloned_enemy_ai_brains,

            ["ai_score_profile"] = ai_score_profile,

            ["errors"] = errors,
        };
    }

    private GDictionary _duplicate_resource_dict(GDictionary source)
    {
        var duplicated = new GDictionary();

        foreach (var key in source.Keys)
        {
            var value = source[key];

            var res = value.AsGodotObject() as Resource;

            duplicated[key] = res != null ? res.Duplicate(true) : value;
        }

        return duplicated;
    }

    private Godot.Collections.Array<string> _apply_patch_entry(
        GDictionary skill_defs,
        GDictionary enemy_ai_brains,
        GodotObject ai_score_profile,
        GDictionary patch_entry
    )
    {
        var errors = new Godot.Collections.Array<string>();

        var target_type = patch_entry.GetValueOrDefault("target_type", "").ToString();

        var path = patch_entry.GetValueOrDefault("path", "").ToString();

        var value = patch_entry.GetValueOrDefault("value");

        if (string.IsNullOrEmpty(path))
            return new Godot.Collections.Array<string>
            {
                "Battle sim override patch for target_type=" + target_type + " is missing path.",
            };

        switch (target_type)
        {
            case "skill":
            {
                var skill_id = ProgressionDataUtils.to_string_name(
                    patch_entry.GetValueOrDefault("target_id", "")
                );

                if (!skill_defs.ContainsKey(skill_id))
                    return new Godot.Collections.Array<string>
                    {
                        "Battle sim override patch target skill "
                            + skill_id
                            + " was not found for path "
                            + path
                            + ".",
                    };

                var error = _set_value_by_path(skill_defs[skill_id], path, value);

                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);

                break;
            }

            case "brain":
            {
                var brain_id = ProgressionDataUtils.to_string_name(
                    patch_entry.GetValueOrDefault("target_id", "")
                );

                if (!enemy_ai_brains.ContainsKey(brain_id))
                    return new Godot.Collections.Array<string>
                    {
                        "Battle sim override patch target brain "
                            + brain_id
                            + " was not found for path "
                            + path
                            + ".",
                    };

                var error = _set_value_by_path(enemy_ai_brains[brain_id], path, value);

                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);

                break;
            }

            case "action":
            {
                var action_resource = _resolve_action_resource(enemy_ai_brains, patch_entry);

                if (action_resource == null)
                    return new Godot.Collections.Array<string>
                    {
                        "Battle sim override patch target action was not found for path "
                            + path
                            + ": "
                            + patch_entry.ToString(),
                    };

                var error = _set_value_by_path(action_resource, path, value);

                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);

                break;
            }

            case "ai_score_profile":
            {
                var error = _set_value_by_path(ai_score_profile, path, value);

                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);

                break;
            }

            default:

                errors.Add(
                    "Battle sim override patch uses unsupported target_type "
                        + target_type
                        + " for path "
                        + path
                        + "."
                );

                break;
        }

        return errors;
    }

    private GodotObject _resolve_action_resource(GDictionary enemy_ai_brains, GDictionary patch_entry)
    {
        var brain_id = ProgressionDataUtils.to_string_name(
            patch_entry.GetValueOrDefault("brain_id", patch_entry.GetValueOrDefault("target_id", ""))
        );

        if (brain_id == "" || !enemy_ai_brains.ContainsKey(brain_id))
            return null;

        var brain = enemy_ai_brains[brain_id].AsGodotObject();

        var state_id = ProgressionDataUtils.to_string_name(patch_entry.GetValueOrDefault("state_id", ""));

        var action_id = ProgressionDataUtils.to_string_name(patch_entry.GetValueOrDefault("action_id", ""));

        var states = _get_brain_states(brain);
        foreach (var state_def in states)
        {
            var state_obj = state_def.AsGodotObject();

            if (state_obj == null)
                continue;

            if (state_id != "" && state_obj.Get("state_id").AsStringName() != state_id)
                continue;

            var actions = state_obj.Call("get_actions").AsGodotArray<Variant>();

            foreach (var action_resource in actions)
            {
                var action_obj = action_resource.AsGodotObject();

                if (action_obj == null)
                    continue;

                if (action_id == "" || action_obj.Get("action_id").AsStringName() == action_id)
                    return action_obj;
            }
        }

        return null;
    }

    private static Godot.Collections.Array _get_brain_states(GodotObject brain)
    {
        var result = new Godot.Collections.Array();

        if (brain == null)
            return result;

        if (brain is EnemyAiBrainDef typedBrain)
        {
            foreach (var state in typedBrain.get_resolved_states())
            {
                if (state != null)
                    result.Add(state);
            }

            return result;
        }

        var rawStates = brain.Get("states");

        if (rawStates.VariantType == Variant.Type.Dictionary)
        {
            foreach (var state in rawStates.AsGodotDictionary().Values)
                result.Add(state);

            return result;
        }

        if (rawStates.VariantType == Variant.Type.Array)
            return rawStates.AsGodotArray();

        return result;
    }

    private string _set_value_by_path(object target, string path, object value)
    {
        var segments = path.Split(".", System.StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return "Battle sim override patch has empty path.";

        return _set_value_recursive(target, segments, 0, value, path);
    }

    private string _set_value_recursive(
        object raw_target,
        string[] segments,
        int index,
        object value,
        string full_path
    )
    {
        var target = GdInterop.GetValueOrDefault(null, "", raw_target);
        if (target.VariantType == Variant.Type.Nil || index >= segments.Length)
            return "Battle sim override path "
                + full_path
                + " could not be applied: null target at segment "
                + index
                + ".";

        var segment = segments[index];

        var is_last = index == segments.Length - 1;

        if (target.VariantType == Variant.Type.Array)
        {
            var arr = target.AsGodotArray<Variant>();

            if (!int.TryParse(segment, out var array_index))
                return "Battle sim override path "
                    + full_path
                    + " expected an array index at segment "
                    + segment
                    + ".";

            if (array_index < 0 || array_index >= arr.Count)
                return "Battle sim override path "
                    + full_path
                    + " has out-of-range array index "
                    + array_index
                    + " at segment "
                    + segment
                    + ".";

            if (is_last)
            {
                arr[array_index] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    _coerce_value(arr[array_index], value)
                );

                return "";
            }

            return _set_value_recursive(arr[array_index], segments, index + 1, value, full_path);
        }

        if (target.VariantType == Variant.Type.Dictionary)
        {
            var dict = target.AsGodotDictionary();

            var resolved_key = _resolve_dictionary_key(dict, segment);

            if (resolved_key == null)
                return "Battle sim override path "
                    + full_path
                    + " references missing dictionary key "
                    + segment
                    + ".";
            var resolved_key_variant = GdInterop.GetValueOrDefault(null, "", resolved_key);

            if (is_last)
            {
                dict[resolved_key_variant] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    _coerce_value(dict.GetValueOrDefault(resolved_key_variant), value)
                );

                return "";
            }

            return _set_value_recursive(
                dict.GetValueOrDefault(resolved_key_variant),
                segments,
                index + 1,
                value,
                full_path
            );
        }

        var obj = target.AsGodotObject();

        if (obj == null || !_object_has_property(obj, segment))
            return "Battle sim override path "
                + full_path
                + " references missing property "
                + segment
                + " on "
                + target.VariantType
                + ".";

        if (is_last)
        {
            var current_value = obj.Get(segment);

            obj.Set(
                segment,
                GdInterop.GetValueOrDefault(null, "", _coerce_value(current_value, value))
            );

            return "";
        }

        return _set_value_recursive(obj.Get(segment), segments, index + 1, value, full_path);
    }

    private object _resolve_dictionary_key(GDictionary target, string segment)
    {
        if (target.ContainsKey(segment))
            return segment;

        var string_name_key = new StringName(segment);

        if (target.ContainsKey(string_name_key))
            return string_name_key;

        return null;
    }

    private bool _object_has_property(GodotObject target, string property_name)
    {
        if (target == null)
            return false;

        foreach (Godot.Collections.Dictionary property_info in target.GetPropertyList())
        {
            if (property_info.GetValueOrDefault("name", "").ToString() == property_name)
                return true;
        }

        return false;
    }

    private object _coerce_value(object raw_current_value, object raw_value)
    {
        var current_value = GdInterop.GetValueOrDefault(null, "", raw_current_value);
        var value = GdInterop.GetValueOrDefault(null, "", raw_value);

        if (current_value.VariantType == Variant.Type.StringName)
            return ProgressionDataUtils.to_string_name(value);

        if (current_value.VariantType == Variant.Type.Vector2I)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                return value;

            if (value.VariantType == Variant.Type.Dictionary)
            {
                var dict = value.AsGodotDictionary();

                return new Vector2I((int)dict.GetValueOrDefault("x", 0), (int)dict.GetValueOrDefault("y", 0));
            }
        }

        if (current_value.VariantType == Variant.Type.Int)
            return (int)value;

        if (current_value.VariantType == Variant.Type.Float)
            return (float)value;

        if (current_value.VariantType == Variant.Type.Bool)
            return (bool)value;

        return value;
    }
}

using Godot;
using GDictionary = Godot.Collections.Dictionary;
using System;
using System.Collections.Generic;

public sealed class BattleSimOverrideApplier
{
    internal BattleSimOverrideApplyResult ApplyProfileTyped(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        BattleSimProfileDef profile
    )
    {
        var patchedSkillDefinitions = CloneSkillDefinitions(skillDefinitions);
        var clonedEnemyAiBrains = DuplicateResourceIndex(enemyAiBrains);
        BattleAiScoreProfile aiScoreProfile =
            DuplicateDerivedResource(
                profile?.ai_score_profile,
                "baseline_ai_score_profile",
                "BattleSimOverrideApplier.ApplyProfileTyped"
            ) ?? new BattleAiScoreProfile();
        GodotContentOwnership.RegisterDerivedContent(
            aiScoreProfile,
            "battle_sim_override:baseline_ai_score_profile",
            "BattleSimOverrideApplier.ApplyProfileTyped.default_profile"
        );
        GDictionary clonedEnemyAiBrainsProjection = ProjectResourceIndex(clonedEnemyAiBrains);
        var factionProfiles = new Dictionary<StringName, BattleAiScoreProfile>();
        var errors = new List<string>();

        if (profile != null)
        {
            foreach (Variant patchEntry in profile.override_patches)
            {
                if (patchEntry.VariantType != Variant.Type.Dictionary)
                {
                    errors.Add(
                        "Battle sim profile "
                            + profile.profile_id
                            + " contains a non-Dictionary override patch."
                    );
                    continue;
                }

                errors.AddRange(
                    ApplyPatchEntryTyped(
                        patchedSkillDefinitions,
                        clonedEnemyAiBrainsProjection,
                        aiScoreProfile,
                        factionProfiles,
                        patchEntry.AsGodotDictionary()
                    )
                );
            }
        }

        foreach (string error in errors)
            GameLog.Error(error, "battlesim.override.failed", "battlesim");

        return new BattleSimOverrideApplyResult(
            patchedSkillDefinitions,
            clonedEnemyAiBrains,
            aiScoreProfile,
            factionProfiles,
            errors
        );
    }

    private static Dictionary<StringName, SkillDefinition> CloneSkillDefinitions(
        IReadOnlyDictionary<StringName, SkillDefinition> source
    )
    {
        return source != null
            ? new Dictionary<StringName, SkillDefinition>(source)
            : new Dictionary<StringName, SkillDefinition>();
    }

    private static Dictionary<StringName, TResource> DuplicateResourceIndex<TResource>(
        IReadOnlyDictionary<StringName, TResource> source
    )
        where TResource : Resource
    {
        var duplicated = new Dictionary<StringName, TResource>();
        if (source == null)
            return duplicated;
        foreach ((StringName id, TResource resource) in source)
        {
            if (id == "" || resource == null)
                continue;
            duplicated[id] =
                DuplicateDerivedResource(
                    resource,
                    $"{typeof(TResource).Name}:{id}",
                    "BattleSimOverrideApplier.DuplicateResourceIndex"
                ) ?? resource;
        }
        return duplicated;
    }

    private static GDictionary ProjectResourceIndex<TResource>(
        IReadOnlyDictionary<StringName, TResource> values
    )
        where TResource : Resource
    {
        var projected = new GDictionary();
        if (values == null)
            return projected;
        foreach ((StringName id, TResource resource) in values)
        {
            if (id == "" || resource == null)
                continue;
            projected[id] = resource;
        }
        return projected;
    }

    private static TResource DuplicateDerivedResource<TResource>(
        TResource resource,
        string derivedKey,
        string reason
    )
        where TResource : Resource
    {
        TResource clone = resource?.Duplicate(true) as TResource;
        if (clone == null)
            return null;
        GodotContentOwnership.RegisterDerivedContent(
            clone,
            $"battle_sim_override:{derivedKey}",
            reason
        );
        return clone;
    }

    private List<string> ApplyPatchEntryTyped(
        Dictionary<StringName, SkillDefinition> skill_definitions,
        GDictionary enemy_ai_brains,
        BattleAiScoreProfile ai_score_profile,
        Dictionary<StringName, BattleAiScoreProfile> faction_profiles,
        GDictionary patch_entry
    )
    {
        var errors = new List<string>();

        string target_type = ReadString(patch_entry, "target_type");

        string path = ReadString(patch_entry, "path");

        Variant value = ReadValue(patch_entry, "value");

        if (string.IsNullOrEmpty(path))
            return new List<string>
            {
                "Battle sim override patch for target_type=" + target_type + " is missing path.",
            };

        switch (target_type)
        {
            case "skill":
            {
                StringName skill_id = ReadStringName(patch_entry, "target_id");

                if (
                    !skill_definitions.TryGetValue(
                        skill_id,
                        out SkillDefinition skillDefinition
                    )
                    || skillDefinition == null
                )
                    return new List<string>
                    {
                        "Battle sim override patch target skill "
                            + skill_id
                            + " was not found for path "
                            + path
                            + ".",
                    };

                var error = ApplySkillDefinitionPatch(
                    skill_definitions,
                    skill_id,
                    skillDefinition,
                    path,
                    value
                );

                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);

                break;
            }

            case "brain":
            {
                StringName brain_id = ReadStringName(patch_entry, "target_id");

                if (!enemy_ai_brains.ContainsKey(brain_id))
                    return new List<string>
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
                    return new List<string>
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

            case "faction_ai_score_profile":
            {
                StringName faction_id = ReadStringName(patch_entry, "target_id");
                if (faction_id == "")
                {
                    errors.Add(
                        "Battle sim faction_ai_score_profile patch is missing target_id for path "
                            + path
                            + "."
                    );
                    break;
                }

                // Per-faction profile is cloned from the (already global-patched) baseline
                // on first touch, so unspecified weights stay at baseline.
                if (
                    !faction_profiles.TryGetValue(
                        faction_id,
                        out BattleAiScoreProfile faction_profile
                    )
                )
                {
                    faction_profile =
                        DuplicateDerivedResource(
                            ai_score_profile,
                            $"faction_ai_score_profile:{faction_id}",
                            "BattleSimOverrideApplier.faction_ai_score_profile"
                        ) ?? new BattleAiScoreProfile();
                    GodotContentOwnership.RegisterDerivedContent(
                        faction_profile,
                        $"battle_sim_override:faction_ai_score_profile:{faction_id}",
                        "BattleSimOverrideApplier.faction_ai_score_profile.default_profile"
                    );
                    faction_profiles[faction_id] = faction_profile;
                }

                var error = _set_value_by_path(faction_profile, path, value);
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

    private static string ApplySkillDefinitionPatch(
        Dictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId,
        SkillDefinition skillDefinition,
        string path,
        Variant value
    )
    {
        if (path != "combat_profile.stamina_cost")
        {
            return "Battle sim override skill patch path "
                + path
                + " is unsupported; supported skill patch paths: combat_profile.stamina_cost.";
        }
        if (skillDefinition.CombatProfile == null)
        {
            return "Battle sim override patch target skill "
                + skillId
                + " has no combat_profile for path "
                + path
                + ".";
        }
        if (value.VariantType != Variant.Type.Int)
        {
            return "Battle sim override skill patch "
                + skillId
                + "."
                + path
                + " requires an int value.";
        }

        skillDefinitions[skillId] = skillDefinition.WithCombatProfile(
            skillDefinition.CombatProfile.WithStaminaCost(value.AsInt32())
        );
        return "";
    }

    private EnemyAiAction _resolve_action_resource(GDictionary enemy_ai_brains, GDictionary patch_entry)
    {
        StringName brain_id = ReadStringName(
            patch_entry,
            "brain_id",
            ReadStringName(patch_entry, "target_id")
        );

        if (brain_id == "" || !enemy_ai_brains.ContainsKey(brain_id))
            return null;

        EnemyAiBrainDef brain = enemy_ai_brains[brain_id].AsGodotObject() as EnemyAiBrainDef;
        if (brain == null)
            return null;

        StringName state_id = ReadStringName(patch_entry, "state_id");

        StringName action_id = ReadStringName(patch_entry, "action_id");

        foreach (EnemyAiStateDef state_obj in GetBrainStates(brain))
        {
            if (state_obj == null)
                continue;

            if (state_id != "" && state_obj.state_id != state_id)
                continue;

            foreach (EnemyAiAction action_obj in state_obj.GetTypedActions())
            {
                if (action_obj == null)
                    continue;

                if (action_id == "" || action_obj.action_id == action_id)
                    return action_obj;
            }
        }

        return null;
    }

    private static IEnumerable<EnemyAiStateDef> GetBrainStates(EnemyAiBrainDef brain)
    {
        if (brain == null)
            yield break;
        foreach (EnemyAiStateDef state in brain.GetResolvedStates())
        {
            if (state != null)
                yield return state;
        }
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
        var target = ToVariant(raw_target);
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
                arr[array_index] = ToVariant(_coerce_value(arr[array_index], value));

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
            var resolved_key_variant = ToVariant(resolved_key);

            if (is_last)
            {
                dict[resolved_key_variant] = ToVariant(_coerce_value(dict[resolved_key_variant], value));

                return "";
            }

            return _set_value_recursive(
                dict[resolved_key_variant],
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
                ToVariant(_coerce_value(current_value, value))
            );

            return "";
        }

        Variant childValue = obj.Get(segment);
        string error = _set_value_recursive(childValue, segments, index + 1, value, full_path);
        if (
            string.IsNullOrEmpty(error)
            && (childValue.VariantType == Variant.Type.Dictionary
                || childValue.VariantType == Variant.Type.Array)
        )
        {
            obj.Set(segment, childValue);
        }
        return error;
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
            if (ReadString(property_info, "name") == property_name)
                return true;
        }

        return false;
    }

    private object _coerce_value(object raw_current_value, object raw_value)
    {
        var current_value = ToVariant(raw_current_value);
        var value = ToVariant(raw_value);

        if (current_value.VariantType == Variant.Type.StringName)
            return ProgressionDataUtils.to_string_name(value);

        if (current_value.VariantType == Variant.Type.Vector2I)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                return value;

            if (value.VariantType == Variant.Type.Dictionary)
            {
                var dict = value.AsGodotDictionary();

                return new Vector2I(ReadInt(dict, "x", 0), ReadInt(dict, "y", 0));
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

    private static Variant ToVariant(object value) =>
        value switch
        {
            Variant variant => variant,
            string text => text,
            StringName stringName => stringName,
            int intValue => intValue,
            long longValue => longValue,
            bool boolValue => boolValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            Godot.Collections.Array array => array,
            GDictionary dictionary => dictionary,
            GodotObject godotObject => godotObject,
            _ => default,
        };

    private static Variant ReadValue(GDictionary source, string key, Variant fallback = default)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key];
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        Variant value = ReadValue(source, key, Variant.From(fallback));
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        Variant value = ReadValue(source, key, Variant.From(fallback));
        return ProgressionDataUtils.to_string_name(value);
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        Variant value = ReadValue(source, key, Variant.From(fallback));
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }
}

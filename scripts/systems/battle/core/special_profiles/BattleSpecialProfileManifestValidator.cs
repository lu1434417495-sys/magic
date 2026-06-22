using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleSpecialProfileManifestValidator
{
    private static readonly StringName METEOR_SWARM_PROFILE_ID = "meteor_swarm";

    private static readonly StringName METEOR_SWARM_RESOLVER_ID = "meteor_swarm";

    private static readonly StringName RUNTIME_READ_POLICY_FORBIDDEN = "forbidden";

    private static readonly string[] FORBIDDEN_FALLBACK_FIELDS =
    {
        "active_fallbacks",
        "fallbacks",
        "legacy_bridge",
    };

    private static readonly HashSet<StringName> ALLOWED_METEOR_SAVE_PROFILE_IDS = new()
    {
        "",
        "meteor_dex_half",
    };

    private static readonly HashSet<string> ALLOWED_TERRAIN_PROFILE_KEYS = new(
        StringComparer.Ordinal
    )
    {
        "terrain_profile_id",
        "ring_min",
        "ring_max",
        "move_cost_delta",
        "move_cost_stack_key",
        "move_cost_stack_mode",
        "lifetime_policy",
        "duration_tu",
        "tick_interval_tu",
        "tick_effect_type",
        "accuracy_modifier_spec",
        "render_overlay_id",
        "overlay_priority",
    };

    private static readonly string[] REQUIRED_TERRAIN_PROFILE_KEYS =
    {
        "terrain_profile_id",
        "ring_min",
        "ring_max",
        "move_cost_delta",
        "lifetime_policy",
        "duration_tu",
        "tick_interval_tu",
        "tick_effect_type",
        "render_overlay_id",
    };

    private static readonly HashSet<string> ALLOWED_ACCURACY_MODIFIER_KEYS = new(
        StringComparer.Ordinal
    )
    {
        "source_domain",
        "label",
        "modifier_delta",
        "stack_key",
        "stack_mode",
        "roll_kind_filter",
        "endpoint_mode",
        "distance_min_exclusive",
        "distance_max_inclusive",
        "target_team_filter",
        "footprint_mode",
        "applies_to",
    };

    public Godot.Collections.Array<string> ValidateManifest(
        Resource manifest,
        Godot.Collections.Dictionary skillDefs,
        string asOfDate = ""
    )
    {
        var errors = new Godot.Collections.Array<string>();

        if (manifest is not BattleSpecialProfileManifest manifestDef)
        {
            errors.Add(
                "Battle special profile manifest failed to cast to BattleSpecialProfileManifest."
            );
            return errors;
        }

        _append_forbidden_fallback_errors(errors, manifestDef);

        var pid = manifestDef.profile_id;
        var sv = manifestDef.schema_version;

        if (pid == "")
            errors.Add("Battle special profile manifest is missing profile_id.");

        if (sv != 1)
            errors.Add($"Battle special profile {pid} uses unsupported schema_version {sv}.");

        if (manifestDef.runtime_read_policy != RUNTIME_READ_POLICY_FORBIDDEN)
            errors.Add($"Battle special profile {pid} must use runtime_read_policy forbidden.");

        var rid = manifestDef.runtime_resolver_id;

        if (rid == "")
            errors.Add($"Battle special profile {pid} is missing runtime_resolver_id.");

        var osids = manifestDef.owning_skill_ids;

        if (osids.Count == 0)
            errors.Add($"Battle special profile {pid} must declare at least one owning_skill_id.");

        if (pid == METEOR_SWARM_PROFILE_ID)
        {
            if (rid != METEOR_SWARM_RESOLVER_ID)
                errors.Add(
                    "Battle special profile meteor_swarm must use runtime_resolver_id meteor_swarm."
                );
            var mp = manifestDef.profile_resource as MeteorSwarmProfile;
            if (mp == null)
                errors.Add(
                    "Battle special profile meteor_swarm profile_resource must be MeteorSwarmProfile."
                );
            else
                foreach (var e in ValidateMeteorSwarmProfile(mp, true))
                    errors.Add(e);
        }
        else if (manifestDef.profile_resource is MeteorSwarmProfile)
            errors.Add($"Battle special profile {pid} cannot use MeteorSwarmProfile.");

        foreach (var skIdV in osids)
        {
            var skId = new StringName(skIdV.ToString());
            if (skId == "")
            {
                errors.Add($"Battle special profile {pid} declares an empty owning_skill_id.");
                continue;
            }
            if (!skillDefs.ContainsKey(skId))
            {
                errors.Add($"Battle special profile {pid} references missing owning skill {skId}.");
                continue;
            }
            var sd = skillDefs[skId].As<SkillDef>();
            if (sd == null || sd.combat_profile == null)
            {
                errors.Add(
                    $"Battle special profile {pid} owning skill {skId} is missing combat_profile."
                );
                continue;
            }
            if (sd.combat_profile.special_resolution_profile_id != pid)
                errors.Add(
                    $"Battle special profile {pid} owning skill {skId} must set matching special_resolution_profile_id."
                );
            _append_special_skill_effect_surface_errors(errors, skId, sd);
        }

        foreach (var tp in manifestDef.required_regression_tests)
        {
            string tps = tp;
            if (tps.StripEdges().Length == 0)
            {
                errors.Add(
                    $"Battle special profile {pid} declares an empty required_regression_tests path."
                );
                continue;
            }
            if (!_resource_file_exists(tps))
                errors.Add(
                    $"Battle special profile {pid} required regression test path does not exist: {tps}."
                );
            if (!_is_default_regression_suite_member(tps))
                errors.Add(
                    $"Battle special profile {pid} required regression test must be a default regression suite member: {tps}."
                );
        }

        return errors;
    }

    public Godot.Collections.Array<string> ValidateMeteorSwarmProfile(
        MeteorSwarmProfile profile,
        bool requireRuntimeData = false
    )
    {
        var errors = new Godot.Collections.Array<string>();

        if (profile == null)
        {
            errors.Add("MeteorSwarmProfile is required.");
            return errors;
        }

        int radius = profile.radius;

        if (profile.coverage_shape_id != "square_7x7")
            errors.Add("MeteorSwarmProfile.coverage_shape_id must be square_7x7.");

        if (radius != 3)
            errors.Add("MeteorSwarmProfile.radius must be 3.");

        if (profile.friendly_fire_soft_expected_hp_percent < 0)
            errors.Add("MeteorSwarmProfile.friendly_fire_soft_expected_hp_percent must be >= 0.");

        if (profile.friendly_fire_hard_expected_hp_percent < 0)
            errors.Add("MeteorSwarmProfile.friendly_fire_hard_expected_hp_percent must be >= 0.");

        if (profile.friendly_fire_hard_worst_case_hp_percent < 0)
            errors.Add("MeteorSwarmProfile.friendly_fire_hard_worst_case_hp_percent must be >= 0.");

        if (
            profile.friendly_fire_hard_expected_hp_percent
            < profile.friendly_fire_soft_expected_hp_percent
        )
            errors.Add(
                "MeteorSwarmProfile.friendly_fire_hard_expected_hp_percent must be >= soft threshold."
            );

        if (
            profile.friendly_fire_hard_worst_case_hp_percent
            < profile.friendly_fire_hard_expected_hp_percent
        )
            errors.Add(
                "MeteorSwarmProfile.friendly_fire_hard_worst_case_hp_percent must be >= hard expected threshold."
            );

        if (requireRuntimeData && profile.impact_components.Count == 0)
            errors.Add(
                "MeteorSwarmProfile.impact_components must be non-empty for runtime resolution."
            );

        if (requireRuntimeData && profile.terrain_profiles.Count == 0)
            errors.Add(
                "MeteorSwarmProfile.terrain_profiles must be non-empty for runtime resolution."
            );

        var seenCompIds = new HashSet<StringName>();

        for (int i = 0; i < profile.impact_components.Count; i++)
        {
            var c = profile.impact_components[i];
            if (c != null && c.component_id != "")
            {
                if (!seenCompIds.Add(c.component_id))
                    errors.Add(
                        $"MeteorSwarmProfile.impact_components[{i}].component_id is duplicated: {c.component_id}."
                    );
            }
            _append_impact_component_errors(errors, profile.impact_components[i], i, radius);
        }

        for (int i = 0; i < profile.terrain_profiles.Count; i++)
        {
            var terrainProfile = profile.terrain_profiles[i];
            _append_terrain_profile_errors(errors, terrainProfile.AsGodotDictionary(), i, radius);
        }
        return errors;
    }

    private void _append_impact_component_errors(
        Godot.Collections.Array<string> errors,
        Resource cr,
        int idx,
        int radius
    )
    {
        var c = cr as MeteorSwarmImpactComponent;
        if (c == null)
        {
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}] must be MeteorSwarmImpactComponent."
            );
            return;
        }

        if (c.component_id == "")
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}].component_id must not be empty."
            );

        if (c.role_label == "")
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}].role_label must not be empty."
            );

        if (c.damage_tag == "")
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}].damage_tag must not be empty."
            );

        if (c.base_power < 0)
            errors.Add($"MeteorSwarmProfile.impact_components[{idx}].base_power must be >= 0.");

        if (c.dice_count < 0)
            errors.Add($"MeteorSwarmProfile.impact_components[{idx}].dice_count must be >= 0.");

        if (c.dice_sides < 0)
            errors.Add($"MeteorSwarmProfile.impact_components[{idx}].dice_sides must be >= 0.");

        if (c.dice_count <= 0 && c.base_power <= 0)
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}] must declare dice or base_power."
            );

        if (c.dice_count > 0 && c.dice_sides <= 0)
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}].dice_sides must be > 0 when dice_count > 0."
            );

        if (c.ring_min < 0 || c.ring_max < c.ring_min || c.ring_max > radius)
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}] ring range is invalid or outside radius."
            );

        if (c.mastery_weight < 0.0)
            errors.Add($"MeteorSwarmProfile.impact_components[{idx}].mastery_weight must be >= 0.");

        if (!ALLOWED_METEOR_SAVE_PROFILE_IDS.Contains(c.save_profile_id))
            errors.Add(
                $"MeteorSwarmProfile.impact_components[{idx}].save_profile_id is unsupported: {c.save_profile_id}."
            );
    }

    private static void _append_special_skill_effect_surface_errors(
        Godot.Collections.Array<string> errors,
        StringName skId,
        SkillDef sd
    )
    {
        var cp = sd.combat_profile;
        if (cp == null)
            return;
        if (cp.effect_defs.Count > 0)
            errors.Add(
                $"Battle special profile owning skill {skId} must not declare executable combat_profile.effect_defs."
            );
        for (int i = 0; i < cp.cast_variants.Count; i++)
        {
            var cv = cp.cast_variants[i];
            if (cv != null && cv.effect_defs.Count > 0)
                errors.Add(
                    $"Battle special profile owning skill {skId} must not declare executable cast_variants[{i}].effect_defs."
                );
        }
    }

    private static void _append_forbidden_fallback_errors(
        Godot.Collections.Array<string> errors,
        BattleSpecialProfileManifest manifest
    )
    {
        var pid = manifest.profile_id;
        foreach (string pn in FORBIDDEN_FALLBACK_FIELDS)
        {
            bool found = false;
            foreach (var pi in manifest.GetPropertyList())
            {
                Godot.Collections.Dictionary propertyInfo = (Godot.Collections.Dictionary)pi;
                if (
                    propertyInfo.ContainsKey("name")
                    && (string)propertyInfo["name"] == pn
                )
                {
                    found = true;
                    break;
                }
            }
            if (found)
                errors.Add($"Battle special profile {pid} declares forbidden fallback field {pn}.");
        }
    }

    private static void _append_terrain_profile_errors(
        Godot.Collections.Array<string> errors,
        Godot.Collections.Dictionary pe,
        int idx,
        int radius
    )
    {
        foreach (var kv in pe.Keys)
        {
            string k = kv.AsString();
            if (k == "accuracy_modifer_spec")
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}] uses misspelled accuracy_modifer_spec."
                );
            else if (!ALLOWED_TERRAIN_PROFILE_KEYS.Contains(k))
                errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}] uses unsupported key {k}.");
        }
        foreach (string rk in REQUIRED_TERRAIN_PROFILE_KEYS)
            if (!pe.ContainsKey(rk))
                errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}] is missing {rk}.");
        StringName tpid = ReadStringName(pe, "terrain_profile_id");
        if (tpid == "")
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].terrain_profile_id must be String/StringName."
            );
        bool hasRingMin = TryReadInt(pe, "ring_min", out int ri);
        bool hasRingMax = TryReadInt(pe, "ring_max", out int ra);
        if (!hasRingMin)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].ring_min must be int.");
        if (!hasRingMax)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].ring_max must be int.");
        if (hasRingMin && hasRingMax)
        {
            if (ri < 0 || ra < ri || ra > radius)
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}] ring range is invalid or outside radius."
                );
        }
        if (!TryReadInt(pe, "move_cost_delta", out _))
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].move_cost_delta must be int.");
        var lp = ReadStringName(pe, "lifetime_policy");
        if (CombatEffectDef.ToLifetimePolicy(lp) == CombatEffectLifetimePolicy.Unknown)
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].lifetime_policy must be battle or timed."
            );
        if (!TryReadInt(pe, "duration_tu", out _))
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].duration_tu must be int.");
        if (!TryReadInt(pe, "tick_interval_tu", out _))
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].tick_interval_tu must be int.");
        var roi = ReadStringName(pe, "render_overlay_id");
        if (roi == "")
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].render_overlay_id must be a non-empty String/StringName."
            );
        if (TryGetValue(pe, "accuracy_modifier_spec", out object accSpec))
        {
            if (!TryAsDictionary(accSpec, out Godot.Collections.Dictionary accuracySpec))
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec must be Dictionary."
                );
            else
                _append_accuracy_modifier_spec_errors(errors, accuracySpec, idx);
        }
    }

    private static void _append_accuracy_modifier_spec_errors(
        Godot.Collections.Array<string> errors,
        Godot.Collections.Dictionary spec,
        int idx
    )
    {
        foreach (var kv in spec.Keys)
        {
            string k = kv.AsString();
            if (!ALLOWED_ACCURACY_MODIFIER_KEYS.Contains(k))
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec uses unsupported key {k}."
                );
        }
        if (
            !spec.ContainsKey("modifier_delta")
        )
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec is missing modifier_delta."
            );
        else
        {
            var md = spec["modifier_delta"];
            if (!TryAsInt(md, out _))
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.modifier_delta must be int."
                );
        }
        StringName ttf = ReadStringName(spec, "target_team_filter", "any");
        if (ttf == "")
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.target_team_filter must be String/StringName."
            );
        else if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(ttf))
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.target_team_filter is unsupported: {ttf}."
            );
    }

    private static bool TryGetValue(
        Godot.Collections.Dictionary source,
        string key,
        out object value
    )
    {
        value = null;
        if (source == null || string.IsNullOrEmpty(key))
            return false;
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (!TryGetValue(source, key, out object value))
            return fallback ?? "";
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized == "" ? fallback ?? "" : normalized;
    }

    private static bool TryReadInt(
        Godot.Collections.Dictionary source,
        string key,
        out int value
    )
    {
        value = 0;
        return TryGetValue(source, key, out object rawValue) && TryAsInt(rawValue, out value);
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsInt32();
            return true;
        }
        catch
        {
            return int.TryParse(rawValue?.ToString() ?? "", out value);
        }
    }

    private static bool TryAsDictionary(
        object rawValue,
        out Godot.Collections.Dictionary value
    )
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return true;
        }
        catch
        {
            value = rawValue as Godot.Collections.Dictionary;
            return value != null;
        }
    }

    private static bool _resource_file_exists(string path)
    {
        if (path.StartsWith("res://") || path.StartsWith("user://"))
            return FileAccess.FileExists(path);
        return FileAccess.FileExists($"res://{path}");
    }

    private static bool _is_default_regression_suite_member(string path)
    {
        var n = path.Replace("\\", "/").StripEdges();
        var lower = n.ToLower();
        if (!lower.StartsWith("tests/"))
            return false;
        if (
            lower.Contains("/tools/")
            || lower.Contains("/simulation/")
            || lower.Contains("/benchmarks/")
        )
            return false;
        if (
            lower.EndsWith("benchmark.cs")
            || lower.EndsWith("analysis.cs")
        )
            return false;
        string fileName = lower.Contains("/") ? lower.Substring(lower.LastIndexOf('/') + 1) : lower;
        return fileName.StartsWith("run_") && lower.EndsWith(".cs");
    }

}

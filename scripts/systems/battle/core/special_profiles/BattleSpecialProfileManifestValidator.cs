using Godot;
using VT = Godot.Variant.Type;

[GlobalClass]
public partial class BattleSpecialProfileManifestValidator : RefCounted
{
    private static readonly StringName METEOR_SWARM_PROFILE_ID = "meteor_swarm";

    private static readonly StringName METEOR_SWARM_RESOLVER_ID = "meteor_swarm";

    private static readonly StringName RUNTIME_READ_POLICY_FORBIDDEN = "forbidden";

    private static readonly Godot.Collections.Array<string> FORBIDDEN_FALLBACK_FIELDS = new()
    {
        "active_fallbacks",
        "fallbacks",
        "legacy_bridge",
    };

    private static readonly Godot.Collections.Dictionary ALLOWED_METEOR_SAVE_PROFILE_IDS = new()
    {
        { "", true },
        { "meteor_dex_half", true },
    };

    private static readonly Godot.Collections.Dictionary ALLOWED_TERRAIN_PROFILE_KEYS = new()
    {
        { "terrain_profile_id", true },
        { "ring_min", true },
        { "ring_max", true },
        { "move_cost_delta", true },
        { "move_cost_stack_key", true },
        { "move_cost_stack_mode", true },
        { "lifetime_policy", true },
        { "duration_tu", true },
        { "tick_interval_tu", true },
        { "tick_effect_type", true },
        { "accuracy_modifier_spec", true },
        { "render_overlay_id", true },
        { "overlay_priority", true },
    };

    private static readonly Godot.Collections.Array<string> REQUIRED_TERRAIN_PROFILE_KEYS = new()
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

    private static readonly Godot.Collections.Dictionary ALLOWED_ACCURACY_MODIFIER_KEYS = new()
    {
        { "source_domain", true },
        { "label", true },
        { "modifier_delta", true },
        { "stack_key", true },
        { "stack_mode", true },
        { "roll_kind_filter", true },
        { "endpoint_mode", true },
        { "distance_min_exclusive", true },
        { "distance_max_inclusive", true },
        { "target_team_filter", true },
        { "footprint_mode", true },
        { "applies_to", true },
    };

    public Godot.Collections.Array<string> validate_manifest(
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
                foreach (var e in validate_meteor_swarm_profile(mp, true))
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
            var sd = skillDefs[skId].AsGodotObject() as SkillDef;
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

    public Godot.Collections.Array<string> validate_meteor_swarm_profile(
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

        var seenCompIds = new Godot.Collections.Dictionary();

        for (int i = 0; i < profile.impact_components.Count; i++)
        {
            var c = profile.impact_components[i];
            if (c != null && c.component_id != "")
            {
                if (seenCompIds.ContainsKey(c.component_id))
                    errors.Add(
                        $"MeteorSwarmProfile.impact_components[{i}].component_id is duplicated: {c.component_id}."
                    );
                else
                    seenCompIds[c.component_id] = i;
            }
            _append_impact_component_errors(errors, profile.impact_components[i], i, radius);
        }

        for (int i = 0; i < profile.terrain_profiles.Count; i++)
        {
            var terrainProfile = profile.terrain_profiles[i];
            if (terrainProfile.VariantType != Variant.Type.Dictionary)
            {
                errors.Add($"MeteorSwarmProfile.terrain_profiles[{i}] must be Dictionary.");
                continue;
            }
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

        if (!ALLOWED_METEOR_SAVE_PROFILE_IDS.ContainsKey(c.save_profile_id))
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
                if (
                    pi.AsGodotDictionary().ContainsKey("name")
                    && (string)pi.AsGodotDictionary()["name"] == pn
                )
                {
                    found = true;
                    break;
                }
            }
            if (found)
                errors.Add($"Battle special profile {pid} declares forbidden fallback field {pn}.");
        }
        string rp = manifest.ResourcePath;
        if (string.IsNullOrEmpty(rp))
            return;
        using var file = FileAccess.Open(rp, FileAccess.ModeFlags.Read);
        if (file == null)
            return;
        string text = file.GetAsText();
        foreach (string fn in FORBIDDEN_FALLBACK_FIELDS)
            if (text.Contains(fn))
                errors.Add(
                    $"Battle special profile {pid} resource text contains forbidden fallback field {fn}."
                );
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
            else if (!ALLOWED_TERRAIN_PROFILE_KEYS.ContainsKey(k))
                errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}] uses unsupported key {k}.");
        }
        foreach (string rk in REQUIRED_TERRAIN_PROFILE_KEYS)
            if (!pe.ContainsKey(rk) && !pe.ContainsKey(new StringName(rk)))
                errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}] is missing {rk}.");
        var tpid = pe.ContainsKey("terrain_profile_id")
            ? pe["terrain_profile_id"]
            : (
                pe.ContainsKey(new StringName("terrain_profile_id"))
                    ? pe[new StringName("terrain_profile_id")]
                    : Variant.From("")
            );
        if (
            (tpid.VariantType != Variant.Type.String && tpid.VariantType != Variant.Type.StringName)
            || tpid.AsString().Length == 0
        )
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].terrain_profile_id must be String/StringName."
            );
        var rmin = pe.ContainsKey("ring_min")
            ? pe["ring_min"]
            : (
                pe.ContainsKey(new StringName("ring_min"))
                    ? pe[new StringName("ring_min")]
                    : Variant.From(0)
            );
        var rmax = pe.ContainsKey("ring_max")
            ? pe["ring_max"]
            : (
                pe.ContainsKey(new StringName("ring_max"))
                    ? pe[new StringName("ring_max")]
                    : Variant.From(0)
            );
        if (rmin.VariantType != Variant.Type.Int)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].ring_min must be int.");
        if (rmax.VariantType != Variant.Type.Int)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].ring_max must be int.");
        if (rmin.VariantType == Variant.Type.Int && rmax.VariantType == Variant.Type.Int)
        {
            int ri = rmin.AsInt32();
            int ra = rmax.AsInt32();
            if (ri < 0 || ra < ri || ra > radius)
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}] ring range is invalid or outside radius."
                );
        }
        var mcd = pe.ContainsKey("move_cost_delta")
            ? pe["move_cost_delta"]
            : (
                pe.ContainsKey(new StringName("move_cost_delta"))
                    ? pe[new StringName("move_cost_delta")]
                    : Variant.From(0)
            );
        if (mcd.VariantType != Variant.Type.Int)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].move_cost_delta must be int.");
        var lpValue = pe.ContainsKey("lifetime_policy")
            ? pe["lifetime_policy"]
            : (
                pe.ContainsKey(new StringName("lifetime_policy"))
                    ? pe[new StringName("lifetime_policy")]
                    : Variant.From("")
            );
        var lp =
            lpValue.VariantType == Variant.Type.StringName
                ? lpValue.AsStringName()
                : (
                    lpValue.VariantType == Variant.Type.String
                        ? new StringName(lpValue.AsString())
                        : new StringName("")
                );
        if (lp != "battle" && lp != "timed")
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].lifetime_policy must be battle or timed."
            );
        var dtu = pe.ContainsKey("duration_tu")
            ? pe["duration_tu"]
            : (
                pe.ContainsKey(new StringName("duration_tu"))
                    ? pe[new StringName("duration_tu")]
                    : Variant.From(0)
            );
        if (dtu.VariantType != Variant.Type.Int)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].duration_tu must be int.");
        var tiu = pe.ContainsKey("tick_interval_tu")
            ? pe["tick_interval_tu"]
            : (
                pe.ContainsKey(new StringName("tick_interval_tu"))
                    ? pe[new StringName("tick_interval_tu")]
                    : Variant.From(0)
            );
        if (tiu.VariantType != Variant.Type.Int)
            errors.Add($"MeteorSwarmProfile.terrain_profiles[{idx}].tick_interval_tu must be int.");
        var roi = pe.ContainsKey("render_overlay_id")
            ? pe["render_overlay_id"]
            : (
                pe.ContainsKey(new StringName("render_overlay_id"))
                    ? pe[new StringName("render_overlay_id")]
                    : Variant.From("")
            );
        if (
            (roi.VariantType != Variant.Type.String && roi.VariantType != Variant.Type.StringName)
            || roi.AsString().Length == 0
        )
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].render_overlay_id must be a non-empty String/StringName."
            );
        var accSpec = pe.ContainsKey("accuracy_modifier_spec")
            ? pe["accuracy_modifier_spec"]
            : (
                pe.ContainsKey(new StringName("accuracy_modifier_spec"))
                    ? pe[new StringName("accuracy_modifier_spec")]
                    : default(Variant)
            );
        if (accSpec.VariantType != Variant.Type.Nil)
        {
            if (accSpec.VariantType != Variant.Type.Dictionary)
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec must be Dictionary."
                );
            else
                _append_accuracy_modifier_spec_errors(errors, accSpec.AsGodotDictionary(), idx);
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
            if (!ALLOWED_ACCURACY_MODIFIER_KEYS.ContainsKey(k))
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec uses unsupported key {k}."
                );
        }
        if (
            !spec.ContainsKey("modifier_delta")
            && !spec.ContainsKey(new StringName("modifier_delta"))
        )
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec is missing modifier_delta."
            );
        else
        {
            var md = spec.ContainsKey("modifier_delta")
                ? spec["modifier_delta"]
                : spec[new StringName("modifier_delta")];
            if (md.VariantType != Variant.Type.Int)
                errors.Add(
                    $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.modifier_delta must be int."
                );
        }
        var ttf = spec.ContainsKey("target_team_filter")
            ? spec["target_team_filter"]
            : (
                spec.ContainsKey(new StringName("target_team_filter"))
                    ? spec[new StringName("target_team_filter")]
                    : Variant.From("any")
            );
        if (ttf.VariantType != Variant.Type.String && ttf.VariantType != Variant.Type.StringName)
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.target_team_filter must be String/StringName."
            );
        else if (!CombatTargetTeamContentRules.is_valid_skill_target_team_filter(
            ttf.VariantType == Variant.Type.StringName
                ? ttf.AsStringName()
                : new StringName(ttf.AsString())
        ))
            errors.Add(
                $"MeteorSwarmProfile.terrain_profiles[{idx}].accuracy_modifier_spec.target_team_filter is unsupported: {ttf}."
            );
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
        if (lower.EndsWith("benchmark.gd") || lower.EndsWith("analysis.gd"))
            return false;
        string fileName = lower.Contains("/") ? lower.Substring(lower.LastIndexOf('/') + 1) : lower;
        return fileName.StartsWith("run_") && lower.EndsWith(".gd");
    }

}

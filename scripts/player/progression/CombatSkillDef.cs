using Godot;

[GlobalClass]
public partial class CombatSkillDef : Resource
{
    [Export]
    public StringName skill_id { get; set; } = "";

    [Export]
    public StringName target_mode { get; set; } = "unit";

    [Export]
    public StringName target_team_filter { get; set; } = "enemy";

    [Export]
    public StringName range_pattern { get; set; } = "single";

    [Export]
    public int range_value { get; set; } = 1;

    [Export]
    public StringName area_pattern { get; set; } = "single";

    [Export]
    public int area_value { get; set; }

    [Export]
    public bool requires_los { get; set; }

    [Export]
    public int ap_cost { get; set; } = 1;

    [Export]
    public int mp_cost { get; set; }

    [Export]
    public int stamina_cost { get; set; }

    [Export]
    public int cooldown_tu { get; set; }

    [Export]
    public int attack_roll_bonus { get; set; }

    [Export]
    public int aura_cost { get; set; }

    private Godot.Collections.Dictionary _level_overrides = new();

    [Export]
    public Godot.Collections.Dictionary level_overrides
    {
        get => _level_overrides;
        set =>
            _level_overrides = _normalize_level_overrides(
                value ?? new Godot.Collections.Dictionary()
            );
    }

    [Export]
    public StringName mastery_trigger_mode { get; set; } = "skill_damage_dice_max";

    [Export]
    public StringName mastery_amount_mode { get; set; } = "per_target_rank";

    [Export]
    public StringName spell_fate_mode { get; set; } = "";

    [Export]
    public StringName spell_critical_mode { get; set; } = "";

    private int _spell_critical_mp_refund_percent;

    [Export]
    public int spell_critical_mp_refund_percent
    {
        get => Mathf.Clamp(_spell_critical_mp_refund_percent, 0, 100);
        set => _spell_critical_mp_refund_percent = value;
    }

    [Export]
    public int[] fumble_protection_curve { get; set; } = System.Array.Empty<int>();

    private int _fumble_protection_extra_mp_percent = 100;

    [Export]
    public int fumble_protection_extra_mp_percent
    {
        get => Mathf.Max(_fumble_protection_extra_mp_percent, 0);
        set => _fumble_protection_extra_mp_percent = value;
    }

    [Export]
    public StringName backlash_mode { get; set; } = "";

    [Export]
    public StringName backlash_target_filter { get; set; } = "";

    [Export]
    public int backlash_offset_radius { get; set; }

    [Export]
    public StringName area_origin_mode { get; set; } = "target";

    [Export]
    public StringName area_direction_mode { get; set; } = "target_vector";

    [Export]
    public Godot.Collections.Array<StringName> ai_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> delivery_categories { get; set; } = new();

    [Export]
    public StringName special_resolution_profile_id { get; set; } = "";

    [Export]
    public StringName target_selection_mode { get; set; } = "single_unit";

    [Export]
    public int min_target_count { get; set; } = 1;

    [Export]
    public int max_target_count { get; set; } = 1;

    [Export]
    public bool allow_repeat_target { get; set; }

    [Export]
    public int max_hits_per_target { get; set; }

    [Export]
    public StringName selection_order_mode { get; set; } = "stable";

    [Export]
    public Godot.Collections.Array<CombatEffectDef> effect_defs { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CombatEffectDef> passive_effect_defs { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CombatCastVariantDef> cast_variants { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> required_weapon_families { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> excluded_weapon_families { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> excluded_weapon_type_ids { get; set; } = new();

    [Export]
    public bool requires_equipped_shield { get; set; }

    [Export]
    public int mastery_low_hp_bonus_multiplier { get; set; } = 1;

    [Export]
    public int mastery_low_hp_threshold_percent { get; set; } = 50;

    public CombatCastVariantDef get_cast_variant(StringName variantId)
    {
        if (variantId == "")
            return null;
        foreach (var cv in cast_variants)
        {
            if (cv != null && cv.variant_id == variantId)
                return cv;
        }
        return null;
    }

    public Godot.Collections.Array<CombatCastVariantDef> get_unlocked_cast_variants(int skillLevel)
    {
        var r = new Godot.Collections.Array<CombatCastVariantDef>();
        foreach (var cv in cast_variants)
        {
            if (cv != null && skillLevel >= cv.min_skill_level)
                r.Add((CombatCastVariantDef)cv.Duplicate(true));
        }
        return r;
    }

    public Godot.Collections.Dictionary get_effective_resource_costs(int skillLevel)
    {
        var costs = new Godot.Collections.Dictionary
        {
            { "ap_cost", ap_cost },
            { "mp_cost", mp_cost },
            { "stamina_cost", stamina_cost },
            { "aura_cost", aura_cost },
            { "cooldown_tu", cooldown_tu },
        };

        var ov = get_level_override(skillLevel);

        foreach (var key in costs.Keys)
        {
            var ks = key.AsString();
            if (ov.ContainsKey(ks))
                costs[ks] = ov[ks].AsInt32();
        }

        return costs;
    }

    public Godot.Collections.Dictionary get_level_override(int skillLevel)
    {
        var eligible = new System.Collections.Generic.List<(
            int level,
            Godot.Collections.Dictionary data
        )>();

        foreach (var lk in _level_overrides.Keys)
        {
            if (lk.VariantType != Variant.Type.Int)
                continue;

            int ol = lk.AsInt32();

            if (ol < 0 || ol > skillLevel)
                continue;

            var od = _level_overrides[lk];
            if (od.VariantType != Variant.Type.Dictionary)
                continue;

            eligible.Add((ol, od.AsGodotDictionary()));
        }

        eligible.Sort((a, b) => a.level.CompareTo(b.level));

        var merged = new Godot.Collections.Dictionary();

        foreach (var (_, data) in eligible)
        {
            foreach (var k in data.Keys)
                merged[k] = data[k];
        }

        return merged;
    }

    public int get_effective_attack_roll_bonus(int sl)
    {
        var o = get_level_override(sl);
        return o.ContainsKey("attack_roll_bonus")
            ? o["attack_roll_bonus"].AsInt32()
            : attack_roll_bonus;
    }

    public StringName get_effective_area_pattern(int sl)
    {
        var o = get_level_override(sl);
        return o.ContainsKey("area_pattern")
            ? ProgressionDataUtils.to_string_name(o["area_pattern"])
            : area_pattern;
    }

    public int get_effective_area_value(int sl)
    {
        var o = get_level_override(sl);
        return o.ContainsKey("area_value") ? o["area_value"].AsInt32() : area_value;
    }

    public int get_effective_range_value(int sl)
    {
        var o = get_level_override(sl);
        return o.ContainsKey("range_value") ? o["range_value"].AsInt32() : range_value;
    }

    public int get_effective_max_target_count(int sl)
    {
        var o = get_level_override(sl);
        return o.ContainsKey("max_target_count")
            ? o["max_target_count"].AsInt32()
            : max_target_count;
    }

    public bool has_spell_fate_control() => spell_fate_mode == "control_roll";

    public int get_fumble_protection_limit(int sl)
    {
        if (fumble_protection_curve.Length == 0)
            return 0;
        int idx = Mathf.Clamp(sl, 0, fumble_protection_curve.Length - 1);
        return Mathf.Max(fumble_protection_curve[idx], 0);
    }

    public bool uses_ground_anchor_drift_backlash() => backlash_mode == "ground_anchor_drift";

    private Godot.Collections.Dictionary _normalize_level_overrides(
        Godot.Collections.Dictionary raw
    )
    {
        var normalized = new Godot.Collections.Dictionary();

        foreach (var lk in raw.Keys)
        {
            var nk = lk;
            if (lk.VariantType == Variant.Type.Float)
            {
                double f = lk.AsDouble();
                if (Mathf.IsEqualApprox((float)f, (float)System.Math.Floor(f)))
                    nk = Variant.From((int)f);
            }
            normalized[nk] = raw[lk];
        }

        return normalized;
    }
}

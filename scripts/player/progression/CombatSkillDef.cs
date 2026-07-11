using System.Collections.Generic;
using Godot;

internal enum CombatSpellFateMode
{
    Unknown = 0,
    None,
    ControlRoll,
}

internal enum CombatSpellCriticalMode
{
    Unknown = 0,
    None,
    MpRefund,
}

internal enum CombatSkillBacklashMode
{
    Unknown = 0,
    None,
    GroundAnchorDrift,
}

internal enum CombatSkillAttackResolutionMode
{
    Unknown = 0,
    Auto,
    DirectEffect,
    FateAttack,
    ForceHitNoCrit,
}

internal enum CombatAreaOriginMode
{
    Unknown = 0,
    Target,
    Caster,
    AnchorCoord,
}

internal enum CombatAreaDirectionMode
{
    Unknown = 0,
    TargetVector,
    CasterFacing,
}

[GlobalClass]
public partial class CombatSkillDef : Resource
{
    private static readonly StringName SpellFateControlRoll = "control_roll";
    private static readonly StringName SpellCriticalMpRefund = "mp_refund";
    private static readonly StringName BacklashGroundAnchorDrift = "ground_anchor_drift";
    private static readonly StringName AttackResolutionModeAuto = "auto";
    private static readonly StringName AttackResolutionModeDirectEffect = "direct_effect";
    private static readonly StringName AttackResolutionModeFateAttack = "fate_attack";
    private static readonly StringName AttackResolutionModeForceHitNoCrit = "force_hit_no_crit";
    private static readonly StringName AreaOriginTarget = "target";
    private static readonly StringName AreaOriginCaster = "caster";
    private static readonly StringName AreaOriginAnchorCoord = "anchor_coord";
    private static readonly StringName AreaDirectionTargetVector = "target_vector";
    private static readonly StringName AreaDirectionCasterFacing = "caster_facing";

    [Export]
    public StringName skill_id { get; set; } = "";

    [Export]
    public StringName target_mode { get; set; } = "unit";
    internal BattleTargetMode TargetModeKind
    {
        get => BattleTypedNames.ToTargetMode(target_mode);
        set => target_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName target_team_filter { get; set; } = "enemy";
    internal BattleTargetFilter TargetFilterKind
    {
        get => BattleTypedNames.ToTargetFilter(target_team_filter);
        set => target_team_filter = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName range_pattern { get; set; } = "single";

    [Export]
    public int range_value { get; set; } = 1;

    [Export]
    public StringName weapon_range_policy { get; set; } = "";

    [Export]
    public StringName area_pattern { get; set; } = "single";
    internal BattleAreaPattern AreaPatternKind => BattleTypedNames.ToAreaPattern(area_pattern);

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
    public int casting_time_tu { get; set; }

    [Export]
    public int casting_maintenance_dc { get; set; }

    [Export]
    public int casting_spell_control_dc { get; set; }

    [Export]
    public StringName pending_cast_binding_mode { get; set; } = "soft_anchor";

    internal PendingCastBindingModeKind PendingCastBindingModeKind
    {
        get => BattleTypedNames.ToPendingCastBindingMode(pending_cast_binding_mode);
        set => pending_cast_binding_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public int attack_roll_bonus { get; set; }

    [Export]
    public StringName attack_resolution_mode { get; set; } = "";
    internal CombatSkillAttackResolutionMode AttackResolutionModeKind
    {
        get => ToAttackResolutionMode(attack_resolution_mode);
        set => attack_resolution_mode = ToStringName(value);
    }

    [Export]
    public int aura_cost { get; set; }

    private Godot.Collections.Dictionary _level_overrides = new();
    private readonly Dictionary<int, Godot.Collections.Dictionary> _mergedLevelOverrideCache =
        new();

    [Export]
    public Godot.Collections.Dictionary level_overrides
    {
        get => _level_overrides;
        set
        {
            _level_overrides = _normalize_level_overrides(
                value ?? new Godot.Collections.Dictionary()
            );
            _mergedLevelOverrideCache.Clear();
        }
    }

    [Export]
    public StringName mastery_trigger_mode { get; set; } = "skill_damage_dice_max";
    internal CombatSkillMasteryTriggerMode MasteryTriggerModeKind
    {
        get => BattleTypedNames.ToCombatSkillMasteryTriggerMode(mastery_trigger_mode);
        set => mastery_trigger_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName mastery_amount_mode { get; set; } = "per_target_rank";
    internal CombatSkillMasteryAmountMode MasteryAmountModeKind
    {
        get => BattleTypedNames.ToCombatSkillMasteryAmountMode(mastery_amount_mode);
        set => mastery_amount_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName spell_fate_mode { get; set; } = "";
    internal CombatSpellFateMode SpellFateModeKind
    {
        get => ToSpellFateMode(spell_fate_mode);
        set => spell_fate_mode = ToStringName(value);
    }

    [Export]
    public StringName spell_critical_mode { get; set; } = "";
    internal CombatSpellCriticalMode SpellCriticalModeKind
    {
        get => ToSpellCriticalMode(spell_critical_mode);
        set => spell_critical_mode = ToStringName(value);
    }

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
    internal CombatSkillBacklashMode BacklashModeKind
    {
        get => ToBacklashMode(backlash_mode);
        set => backlash_mode = ToStringName(value);
    }

    [Export]
    public StringName backlash_target_filter { get; set; } = "";

    [Export]
    public int backlash_offset_radius { get; set; }

    [Export]
    public StringName area_origin_mode { get; set; } = "target";
    internal CombatAreaOriginMode AreaOriginModeKind
    {
        get => ToAreaOriginMode(area_origin_mode);
        set => area_origin_mode = ToStringName(value);
    }

    [Export]
    public StringName area_direction_mode { get; set; } = "target_vector";
    internal CombatAreaDirectionMode AreaDirectionModeKind
    {
        get => ToAreaDirectionMode(area_direction_mode);
        set => area_direction_mode = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> ai_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> delivery_categories { get; set; } = new();

    [Export]
    public StringName special_resolution_profile_id { get; set; } = "";

    [Export]
    public StringName target_selection_mode { get; set; } = "single_unit";

    internal BattleTargetSelectionMode TargetSelectionModeKind
    {
        get => BattleTypedNames.ToTargetSelectionMode(target_selection_mode);
        set => target_selection_mode = BattleTypedNames.ToStringName(value);
    }

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

    internal BattleTargetSelectionOrderMode SelectionOrderModeKind
    {
        get => BattleTypedNames.ToTargetSelectionOrderMode(selection_order_mode);
        set => selection_order_mode = BattleTypedNames.ToStringName(value);
    }

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

    public CombatCastVariantDef GetCastVariant(StringName variantId)
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

    public Godot.Collections.Array<CombatCastVariantDef> GetUnlockedCastVariants(int skillLevel)
    {
        var r = new Godot.Collections.Array<CombatCastVariantDef>();
        foreach (var cv in cast_variants)
        {
            if (cv != null && skillLevel >= cv.min_skill_level)
            {
                CombatCastVariantDef variant = (CombatCastVariantDef)cv.Duplicate(true);
                r.Add(variant);
            }
        }
        return r;
    }

    public CombatSkillResourceCosts GetEffectiveResourceCostValues(int skillLevel)
    {
        var ov = GetCachedLevelOverride(skillLevel);

        return new CombatSkillResourceCosts(
            TryReadResourceCostOverride(ov, "ap_cost", out int effectiveApCost)
                ? effectiveApCost
                : ap_cost,
            TryReadResourceCostOverride(ov, "mp_cost", out int effectiveMpCost)
                ? effectiveMpCost
                : mp_cost,
            TryReadResourceCostOverride(ov, "stamina_cost", out int effectiveStaminaCost)
                ? effectiveStaminaCost
                : stamina_cost,
            TryReadResourceCostOverride(ov, "aura_cost", out int effectiveAuraCost)
                ? effectiveAuraCost
                : aura_cost,
            TryReadResourceCostOverride(ov, "cooldown_tu", out int effectiveCooldownTu)
                ? effectiveCooldownTu
                : cooldown_tu
        );
    }

    public Godot.Collections.Dictionary GetLevelOverride(int skillLevel)
    {
        Godot.Collections.Dictionary duplicate = DuplicateLevelOverride(
            GetCachedLevelOverride(skillLevel)
        );
        return duplicate;
    }

    private Godot.Collections.Dictionary GetCachedLevelOverride(int skillLevel)
    {
        if (_mergedLevelOverrideCache.TryGetValue(skillLevel, out var cached))
        {
            return cached;
        }

        Godot.Collections.Dictionary merged = BuildLevelOverride(skillLevel);
        _mergedLevelOverrideCache[skillLevel] = merged;
        return merged;
    }

    private Godot.Collections.Dictionary BuildLevelOverride(int skillLevel)
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

    private static Godot.Collections.Dictionary DuplicateLevelOverride(
        Godot.Collections.Dictionary source
    )
    {
        var duplicate = new Godot.Collections.Dictionary();
        if (source == null)
            return duplicate;
        foreach (var key in source.Keys)
            duplicate[key] = source[key];
        return duplicate;
    }

    private static bool TryReadResourceCostOverride(
        Godot.Collections.Dictionary overrides,
        string key,
        out int value
    )
    {
        if (overrides != null && overrides.ContainsKey(key))
        {
            Variant rawValue = overrides[key];
            if (rawValue.VariantType == Variant.Type.Int)
            {
                value = rawValue.AsInt32();
                return true;
            }
            if (rawValue.VariantType == Variant.Type.Float)
            {
                value = (int)rawValue.AsDouble();
                return true;
            }
        }
        value = 0;
        return false;
    }

    public int GetEffectiveAttackRollBonus(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("attack_roll_bonus")
            ? o["attack_roll_bonus"].AsInt32()
            : attack_roll_bonus;
    }

    public int GetEffectiveCastingTimeTu(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return TryReadResourceCostOverride(o, "casting_time_tu", out int value)
            ? value
            : casting_time_tu;
    }

    public int GetEffectiveCastingMaintenanceDc(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return TryReadResourceCostOverride(o, "casting_maintenance_dc", out int value)
            ? value
            : casting_maintenance_dc;
    }

    public int GetEffectiveCastingSpellControlDc(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return TryReadResourceCostOverride(o, "casting_spell_control_dc", out int value)
            ? value
            : casting_spell_control_dc;
    }

    public PendingCastBindingModeKind GetEffectivePendingCastBindingMode(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("pending_cast_binding_mode")
            ? BattleTypedNames.ToPendingCastBindingMode(
                ProgressionDataUtils.to_string_name(o["pending_cast_binding_mode"])
            )
            : PendingCastBindingModeKind;
    }

    public StringName GetEffectiveAreaPattern(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("area_pattern")
            ? ProgressionDataUtils.to_string_name(o["area_pattern"])
            : area_pattern;
    }

    public int GetEffectiveAreaValue(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("area_value") ? o["area_value"].AsInt32() : area_value;
    }

    public int GetEffectiveRangeValue(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("range_value") ? o["range_value"].AsInt32() : range_value;
    }

    public int GetEffectiveMaxTargetCount(int sl)
    {
        var o = GetCachedLevelOverride(sl);
        return o.ContainsKey("max_target_count")
            ? o["max_target_count"].AsInt32()
            : max_target_count;
    }

    public bool HasCastingTime(int sl) => GetEffectiveCastingTimeTu(sl) > 0;

    public bool HasSpellFateControl() => SpellFateModeKind == CombatSpellFateMode.ControlRoll;

    public int GetFumbleProtectionLimit(int sl)
    {
        if (fumble_protection_curve.Length == 0)
            return 0;
        int idx = Mathf.Clamp(sl, 0, fumble_protection_curve.Length - 1);
        return Mathf.Max(fumble_protection_curve[idx], 0);
    }

    public bool UsesGroundAnchorDriftBacklash() =>
        BacklashModeKind == CombatSkillBacklashMode.GroundAnchorDrift;

    internal static CombatSpellFateMode ToSpellFateMode(StringName value)
    {
        if (value == "")
            return CombatSpellFateMode.None;
        if (value == SpellFateControlRoll)
            return CombatSpellFateMode.ControlRoll;
        return CombatSpellFateMode.Unknown;
    }

    internal static CombatSpellCriticalMode ToSpellCriticalMode(StringName value)
    {
        if (value == "")
            return CombatSpellCriticalMode.None;
        if (value == SpellCriticalMpRefund)
            return CombatSpellCriticalMode.MpRefund;
        return CombatSpellCriticalMode.Unknown;
    }

    internal static CombatSkillBacklashMode ToBacklashMode(StringName value)
    {
        if (value == "")
            return CombatSkillBacklashMode.None;
        if (value == BacklashGroundAnchorDrift)
            return CombatSkillBacklashMode.GroundAnchorDrift;
        return CombatSkillBacklashMode.Unknown;
    }

    internal static CombatSkillAttackResolutionMode ToAttackResolutionMode(StringName value)
    {
        if (value == "" || value == AttackResolutionModeAuto)
            return CombatSkillAttackResolutionMode.Auto;
        if (value == AttackResolutionModeDirectEffect)
            return CombatSkillAttackResolutionMode.DirectEffect;
        if (value == AttackResolutionModeFateAttack)
            return CombatSkillAttackResolutionMode.FateAttack;
        if (value == AttackResolutionModeForceHitNoCrit)
            return CombatSkillAttackResolutionMode.ForceHitNoCrit;
        return CombatSkillAttackResolutionMode.Unknown;
    }

    internal static CombatAreaOriginMode ToAreaOriginMode(StringName value)
    {
        if (value == AreaOriginTarget)
            return CombatAreaOriginMode.Target;
        if (value == AreaOriginCaster)
            return CombatAreaOriginMode.Caster;
        if (value == AreaOriginAnchorCoord)
            return CombatAreaOriginMode.AnchorCoord;
        return CombatAreaOriginMode.Unknown;
    }

    internal static CombatAreaDirectionMode ToAreaDirectionMode(StringName value)
    {
        if (value == AreaDirectionTargetVector)
            return CombatAreaDirectionMode.TargetVector;
        if (value == AreaDirectionCasterFacing)
            return CombatAreaDirectionMode.CasterFacing;
        return CombatAreaDirectionMode.Unknown;
    }

    internal static StringName ToStringName(CombatSpellFateMode mode)
    {
        return mode switch
        {
            CombatSpellFateMode.None => "",
            CombatSpellFateMode.ControlRoll => SpellFateControlRoll,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatSpellCriticalMode mode)
    {
        return mode switch
        {
            CombatSpellCriticalMode.None => "",
            CombatSpellCriticalMode.MpRefund => SpellCriticalMpRefund,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatSkillBacklashMode mode)
    {
        return mode switch
        {
            CombatSkillBacklashMode.None => "",
            CombatSkillBacklashMode.GroundAnchorDrift => BacklashGroundAnchorDrift,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatSkillAttackResolutionMode mode)
    {
        return mode switch
        {
            CombatSkillAttackResolutionMode.Auto => "",
            CombatSkillAttackResolutionMode.DirectEffect => AttackResolutionModeDirectEffect,
            CombatSkillAttackResolutionMode.FateAttack => AttackResolutionModeFateAttack,
            CombatSkillAttackResolutionMode.ForceHitNoCrit => AttackResolutionModeForceHitNoCrit,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatAreaOriginMode mode)
    {
        return mode switch
        {
            CombatAreaOriginMode.Target => AreaOriginTarget,
            CombatAreaOriginMode.Caster => AreaOriginCaster,
            CombatAreaOriginMode.AnchorCoord => AreaOriginAnchorCoord,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatAreaDirectionMode mode)
    {
        return mode switch
        {
            CombatAreaDirectionMode.TargetVector => AreaDirectionTargetVector,
            CombatAreaDirectionMode.CasterFacing => AreaDirectionCasterFacing,
            _ => "",
        };
    }

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

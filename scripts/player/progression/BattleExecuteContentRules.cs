using Godot;

[GlobalClass]
public partial class BattleExecuteContentRules : RefCounted
{
    private static readonly StringName EffectTypeExecute = "execute";
    private static readonly StringName SaveTagExecute = BattleSaveContentRules.SAVE_TAG_EXECUTE;
    private static readonly StringName DamageTagNegativeEnergy = "negative_energy";

    private const string ParamThresholdMaxHpRatioPercent = "threshold_max_hp_ratio_percent";
    private const string ParamSoulFractureDurationTu = "soul_fracture_duration_tu";
    private const string ParamHealMultiplierPercent = "heal_multiplier_percent";
    private const string ParamShieldGainMultiplierPercent = "shield_gain_multiplier_percent";

    public static StringName EFFECT_TYPE_EXECUTE() => EffectTypeExecute;

    public static StringName SAVE_TAG_EXECUTE() => SaveTagExecute;

    public static StringName DAMAGE_TAG_NEGATIVE_ENERGY() => DamageTagNegativeEnergy;

    public static string PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT() => ParamThresholdMaxHpRatioPercent;

    public static string PARAM_SOUL_FRACTURE_DURATION_TU() => ParamSoulFractureDurationTu;

    public static string PARAM_HEAL_MULTIPLIER_PERCENT() => ParamHealMultiplierPercent;

    public static string PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT() => ParamShieldGainMultiplierPercent;

    public static readonly Godot.Collections.Dictionary REQUIRED_PARAM_TYPES = new()
    {
        { ParamThresholdMaxHpRatioPercent, (long)Variant.Type.Int },
        { ParamSoulFractureDurationTu, (long)Variant.Type.Int },
        { ParamHealMultiplierPercent, (long)Variant.Type.Int },
        { ParamShieldGainMultiplierPercent, (long)Variant.Type.Int },
    };
}

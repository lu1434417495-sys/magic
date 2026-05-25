using Godot;

[GlobalClass]
public partial class BattleExecuteContentRules : RefCounted
{
    public static readonly StringName EFFECT_TYPE_EXECUTE = "execute";
    public static readonly StringName SAVE_TAG_EXECUTE = BattleSaveContentRules.SAVE_TAG_EXECUTE;
    public static readonly StringName DAMAGE_TAG_NEGATIVE_ENERGY = "negative_energy";

    public const string PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT = "threshold_max_hp_ratio_percent";
    public const string PARAM_SOUL_FRACTURE_DURATION_TU = "soul_fracture_duration_tu";
    public const string PARAM_HEAL_MULTIPLIER_PERCENT = "heal_multiplier_percent";
    public const string PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT = "shield_gain_multiplier_percent";

    public static readonly Godot.Collections.Dictionary REQUIRED_PARAM_TYPES = new()
    {
        { PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT, (long)Variant.Type.Int },
        { PARAM_SOUL_FRACTURE_DURATION_TU, (long)Variant.Type.Int },
        { PARAM_HEAL_MULTIPLIER_PERCENT, (long)Variant.Type.Int },
        { PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT, (long)Variant.Type.Int },
    };
}

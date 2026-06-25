using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleStatusEffectParams
{
    private readonly RuntimePayloadStore _residualSavePayload = new();

    public GDictionary ResidualSavePayload => _residualSavePayload.ProjectPayload();
    public double? IncomingDamageMultiplier { get; private init; }
    public double? OutgoingDamageMultiplier { get; private init; }
    public StringName SourceProfileId { get; private init; } = "";
    public StringName SourceLayerId { get; private init; } = "";
    public StringName SourceSkillId { get; private init; } = "";
    public StringName StackBehavior { get; private init; } = "";
    public int StackLimit { get; private init; }
    public int? HealMultiplierPercent { get; private init; }
    public int? ShieldGainMultiplierPercent { get; private init; }
    public int AttackRollPenalty { get; private init; } = -1;
    public bool Undispellable { get; private init; }
    public bool DispellableMagic { get; private init; }
    public bool DispellableHarmfulMagic { get; private init; }
    public bool DispellableBeneficialMagic { get; private init; }
    public StringName DamageTag { get; private init; } = "";
    public List<StringName> DamageTags { get; private init; } = new();
    public StringName DamageCategory { get; private init; } = "";
    public StringName MitigationTier { get; private init; } = "";
    public StringName DrBypassTag { get; private init; } = "";
    public StringName BodySizeCategoryOverride { get; private init; } = "";
    public StringName PreviousBodySizeCategory { get; private init; } = "";
    public int SelfSaveDc { get; private init; }
    public StringName SelfSaveAbility { get; private init; } = "";
    public StringName SelfSaveTag { get; private init; } = "";
    public int? SelfSaveRollOverride { get; private init; }
    public int? SourceSkillLevel { get; private init; }
    public int SaveBonus { get; private init; }
    public int ControlSaveBonus { get; private init; }
    public int PassiveReduction { get; private init; }
    public int ContentDr { get; private init; }
    public int GuardBlock { get; private init; }
    public int RangeBonus { get; private init; }
    public List<StringName> SaveAdvantageTags { get; private init; } = new();
    public List<StringName> SaveDisadvantageTags { get; private init; } = new();
    public List<StringName> SaveImmunityTags { get; private init; } = new();
    public List<StringName> SaveTags { get; private init; } = new();
    public List<StringName> StatusTags { get; private init; } = new();
    public Dictionary<StringName, int> SaveBonusByTag { get; private init; } = new();

    public static BattleStatusEffectParams FromDictionary(GDictionary parameters)
    {
        parameters ??= new GDictionary();
        var result = new BattleStatusEffectParams
        {
            IncomingDamageMultiplier = ReadOptionalDoubleParam(parameters, "incoming_damage_multiplier"),
            OutgoingDamageMultiplier = ReadOptionalDoubleParam(parameters, "outgoing_damage_multiplier"),
            SourceProfileId = ReadOptionalStringNameParam(parameters, "source"),
            SourceLayerId = ReadOptionalStringNameParam(parameters, "layer_id"),
            SourceSkillId = ReadOptionalStringNameParam(parameters, "source_skill_id"),
            StackBehavior = ReadOptionalStringNameParam(parameters, "stack_behavior"),
            StackLimit = ReadOptionalIntParam(parameters, "stack_limit") ?? 0,
            HealMultiplierPercent = ReadOptionalIntParam(parameters, "heal_multiplier_percent"),
            ShieldGainMultiplierPercent = ReadOptionalIntParam(
                parameters,
                "shield_gain_multiplier_percent"
            ),
            AttackRollPenalty = ReadOptionalIntParam(parameters, "attack_roll_penalty") ?? -1,
            Undispellable = ReadOptionalBoolParam(parameters, "undispellable"),
            DispellableMagic = ReadOptionalBoolParam(parameters, "dispellable_magic"),
            DispellableHarmfulMagic = ReadOptionalBoolParam(parameters, "dispellable_harmful_magic"),
            DispellableBeneficialMagic = ReadOptionalBoolParam(
                parameters,
                "dispellable_beneficial_magic"
            ),
            DamageTag = ReadOptionalStringNameParam(parameters, "damage_tag"),
            DamageTags = ReadStringNameListParam(parameters, "damage_tags"),
            DamageCategory = ReadOptionalStringNameParam(parameters, "damage_category"),
            MitigationTier = ReadOptionalStringNameParam(parameters, "mitigation_tier"),
            DrBypassTag = ReadOptionalStringNameParam(parameters, "dr_bypass_tag"),
            BodySizeCategoryOverride = ReadOptionalStringNameParam(
                parameters,
                "body_size_category_override"
            ),
            PreviousBodySizeCategory = ReadOptionalStringNameParam(
                parameters,
                "previous_body_size_category"
            ),
            SelfSaveDc = ReadOptionalIntParam(parameters, "self_save_dc") ?? 0,
            SelfSaveAbility = ReadOptionalStringNameParam(parameters, "self_save_ability"),
            SelfSaveTag = ReadOptionalStringNameParam(parameters, "self_save_tag"),
            SelfSaveRollOverride = ReadOptionalIntParam(parameters, "self_save_roll_override"),
            SourceSkillLevel = ReadOptionalIntParam(parameters, "skill_level"),
            SaveBonus = ReadOptionalIntParam(parameters, "save_bonus") ?? 0,
            ControlSaveBonus = ReadOptionalIntParam(parameters, "control_save_bonus") ?? 0,
            PassiveReduction = ReadOptionalIntParam(parameters, "passive_reduction") ?? 0,
            ContentDr = ReadOptionalIntParam(parameters, "content_dr") ?? 0,
            GuardBlock = ReadOptionalIntParam(parameters, "guard_block") ?? 0,
            RangeBonus = ReadOptionalIntParam(parameters, "range_bonus") ?? 0,
            SaveAdvantageTags = ReadStringNameListParam(parameters, "save_advantage_tags"),
            SaveDisadvantageTags = ReadStringNameListParam(parameters, "save_disadvantage_tags"),
            SaveImmunityTags = ReadStringNameListParam(parameters, "save_immunity_tags"),
            SaveTags = ReadStringNameListParam(parameters, "save_tags"),
            StatusTags = ReadStringNameListParam(parameters, "status_tags"),
            SaveBonusByTag = ReadStringNameIntMapParam(parameters, "save_bonus_by_tag"),
        };
        result._residualSavePayload.ReplaceWithPayload(BattleStatusEffectState.CopyResidualParams(parameters));
        return result;
    }

    public void ApplyTo(BattleStatusEffectState status, bool overwriteExisting = false)
    {
        if (status == null)
            return;

        if (overwriteExisting || !status.incoming_damage_multiplier.HasValue)
            status.incoming_damage_multiplier = IncomingDamageMultiplier;
        if (overwriteExisting || !status.outgoing_damage_multiplier.HasValue)
            status.outgoing_damage_multiplier = OutgoingDamageMultiplier;
        if (overwriteExisting || status.source_profile_id == "")
            status.source_profile_id = SourceProfileId;
        if (overwriteExisting || status.source_layer_id == "")
            status.source_layer_id = SourceLayerId;
        if (overwriteExisting || status.source_skill_id == "")
            status.source_skill_id = SourceSkillId;
        if (overwriteExisting || status.stack_behavior == "")
            status.stack_behavior = StackBehavior;
        if ((overwriteExisting || status.stack_limit <= 0) && StackLimit > 0)
            status.stack_limit = StackLimit;
        if (overwriteExisting || !status.heal_multiplier_percent.HasValue)
            status.heal_multiplier_percent = HealMultiplierPercent;
        if (overwriteExisting || !status.shield_gain_multiplier_percent.HasValue)
            status.shield_gain_multiplier_percent = ShieldGainMultiplierPercent;
        if ((overwriteExisting || status.attack_roll_penalty < 0) && AttackRollPenalty >= 0)
            status.attack_roll_penalty = AttackRollPenalty;
        if (overwriteExisting || !status.undispellable)
            status.undispellable = Undispellable;
        if (overwriteExisting || !status.dispellable_magic)
            status.dispellable_magic = DispellableMagic;
        if (overwriteExisting || !status.dispellable_harmful_magic)
            status.dispellable_harmful_magic = DispellableHarmfulMagic;
        if (overwriteExisting || !status.dispellable_beneficial_magic)
            status.dispellable_beneficial_magic = DispellableBeneficialMagic;
        if (overwriteExisting || status.damage_tag == "")
            status.damage_tag = DamageTag;
        if ((overwriteExisting || status.damage_tags.Count == 0) && DamageTags.Count > 0)
            status.damage_tags = new List<StringName>(DamageTags);
        if (overwriteExisting || status.damage_category == "")
            status.damage_category = DamageCategory;
        if (overwriteExisting || status.mitigation_tier == "")
            status.mitigation_tier = MitigationTier;
        if (overwriteExisting || status.dr_bypass_tag == "")
            status.dr_bypass_tag = DrBypassTag;
        if (overwriteExisting || status.body_size_category_override == "")
            status.body_size_category_override = BodySizeCategoryOverride;
        if (overwriteExisting || status.previous_body_size_category == "")
            status.previous_body_size_category = PreviousBodySizeCategory;
        if ((overwriteExisting || status.self_save_dc <= 0) && SelfSaveDc > 0)
            status.self_save_dc = SelfSaveDc;
        if (overwriteExisting || status.self_save_ability == "")
            status.self_save_ability = SelfSaveAbility;
        if (overwriteExisting || status.self_save_tag == "")
            status.self_save_tag = SelfSaveTag;
        if (overwriteExisting || !status.self_save_roll_override.HasValue)
            status.self_save_roll_override = SelfSaveRollOverride;
        if (overwriteExisting || !status.source_skill_level.HasValue)
            status.source_skill_level = SourceSkillLevel;
        if ((overwriteExisting || status.save_bonus == 0) && SaveBonus != 0)
            status.save_bonus = SaveBonus;
        if ((overwriteExisting || status.control_save_bonus == 0) && ControlSaveBonus != 0)
            status.control_save_bonus = ControlSaveBonus;
        if ((overwriteExisting || status.passive_reduction == 0) && PassiveReduction != 0)
            status.passive_reduction = PassiveReduction;
        if ((overwriteExisting || status.content_dr == 0) && ContentDr != 0)
            status.content_dr = ContentDr;
        if ((overwriteExisting || status.guard_block == 0) && GuardBlock != 0)
            status.guard_block = GuardBlock;
        if ((overwriteExisting || status.range_bonus == 0) && RangeBonus != 0)
            status.range_bonus = RangeBonus;
        if ((overwriteExisting || status.save_advantage_tags.Count == 0) && SaveAdvantageTags.Count > 0)
            status.save_advantage_tags = new List<StringName>(SaveAdvantageTags);
        if ((overwriteExisting || status.save_disadvantage_tags.Count == 0) && SaveDisadvantageTags.Count > 0)
            status.save_disadvantage_tags = new List<StringName>(SaveDisadvantageTags);
        if ((overwriteExisting || status.save_immunity_tags.Count == 0) && SaveImmunityTags.Count > 0)
            status.save_immunity_tags = new List<StringName>(SaveImmunityTags);
        if ((overwriteExisting || status.save_tags.Count == 0) && SaveTags.Count > 0)
            status.save_tags = new List<StringName>(SaveTags);
        if ((overwriteExisting || status.status_tags.Count == 0) && StatusTags.Count > 0)
            status.status_tags = new List<StringName>(StatusTags);
        if ((overwriteExisting || status.save_bonus_by_tag.Count == 0) && SaveBonusByTag.Count > 0)
            status.save_bonus_by_tag = new Dictionary<StringName, int>(SaveBonusByTag);
    }

    private static int? ReadOptionalIntParam(GDictionary parameters, string key)
    {
        if (parameters.ContainsKey(key) && parameters[key].VariantType == Variant.Type.Int)
            return parameters[key].AsInt32();
        return null;
    }

    private static double? ReadOptionalDoubleParam(GDictionary parameters, string key)
    {
        if (!parameters.ContainsKey(key))
            return null;
        Variant value = parameters[key];
        return value.VariantType switch
        {
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.Int => value.AsInt32(),
            _ => null,
        };
    }

    private static bool ReadOptionalBoolParam(GDictionary parameters, string key) =>
        parameters.ContainsKey(key)
        && parameters[key].VariantType == Variant.Type.Bool
        && parameters[key].AsBool();

    private static StringName ReadOptionalStringNameParam(GDictionary parameters, string key)
    {
        if (
            !parameters.ContainsKey(key)
            || (
                parameters[key].VariantType != Variant.Type.String
                && parameters[key].VariantType != Variant.Type.StringName
            )
        )
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(parameters[key]);
    }

    private static List<StringName> ReadStringNameListParam(GDictionary parameters, string key)
    {
        if (!parameters.ContainsKey(key) || parameters[key].VariantType != Variant.Type.Array)
            return new List<StringName>();
        var result = new List<StringName>();
        foreach (StringName value in ProgressionDataUtils.to_string_name_array(parameters[key].AsGodotArray()))
        {
            if (value != "")
                result.Add(value);
        }
        return result;
    }

    private static Dictionary<StringName, int> ReadStringNameIntMapParam(
        GDictionary parameters,
        string key
    )
    {
        var result = new Dictionary<StringName, int>();
        if (!parameters.ContainsKey(key) || parameters[key].VariantType != Variant.Type.Dictionary)
            return result;

        GDictionary map = parameters[key].AsGodotDictionary();
        foreach (Variant rawKey in map.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName tag = rawKey.AsStringName();
            Variant rawValue = map[rawKey];
            if (tag == "" || rawValue.VariantType != Variant.Type.Int)
                continue;
            result[tag] = rawValue.AsInt32();
        }
        return result;
    }
}

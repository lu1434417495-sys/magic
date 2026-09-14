#nullable enable

using System;
using System.Collections.Generic;

/// <summary>
/// Pure CLR wire vocabulary shared by production and standalone offline validation.
/// Runtime StringName mappings are projections of these authored values, never validator inputs.
/// </summary>
internal static class TraitImportValueRules
{
    internal const string PassiveTrigger = "passive";
    internal const string HighestRollStackPolicy = "highest_roll";
    internal const string IdentitySource = "identity";
    internal const string CharacterSource = "character";
    internal const string EquipmentFixedSource = "equipment_fixed";
    internal const string EquipmentRollSource = "equipment_roll";
    internal const string RollInt = "int";
    internal const string RollStringName = "string_name";
    internal const string RollBool = "bool";

    private static readonly HashSet<string> EffectTypes = Set(
        "attribute_modifier", "darkvision", "superior_darkvision", "fey_ancestry", "brave",
        "halfling_luck", "savage_attacks", "relentless_endurance", "gnome_cunning",
        "dwarven_resilience", "duergar_resilience", "human_versatility", "small_body",
        "fleet_of_foot", "dragon_breath", "racial_spell_grant", "damage_resistance",
        "save_advantage", "civil_militia", "keen_senses", "trance", "elven_weapon_training",
        "drow_weapon_training", "dwarven_combat_training", "shield_dwarf_armor_training",
        "dwarven_toughness", "menacing", "halfling_nimbleness", "naturally_stealthy",
        "mask_of_the_wild", "stonecunning", "forest_gnome_magic", "deep_gnome_camouflage",
        "artificers_lore", "duergar_magic", "githyanki_martial_prodigy", "astral_knowledge",
        "githyanki_psionics", "infernal_legacy", "asmodeus_legacy", "mephistopheles_legacy",
        "zariel_legacy", "drow_magic", "draconic_ancestry", "equipment_ability"
    );

    private static readonly HashSet<string> TriggerTypes = Set(
        PassiveTrigger, "on_natural_one", "on_crit", "on_fatal_damage", "on_battle_start", "on_turn_start"
    );

    private static readonly HashSet<string> DispatchPairs = Set(
        "halfling_luck\ton_natural_one",
        "savage_attacks\ton_crit",
        "relentless_endurance\ton_fatal_damage"
    );

    private static readonly HashSet<string> StackPolicies = Set(
        "unique_by_trait", HighestRollStackPolicy, "additive", "stack_by_instance"
    );

    private static readonly HashSet<string> SourceKinds = Set(
        IdentitySource, CharacterSource, EquipmentFixedSource, EquipmentRollSource, "gear_set_threshold"
    );

    private static readonly HashSet<string> ChargeScopes = Set("none", "per_turn", "per_battle");
    private static readonly HashSet<string> ChargeResetTimings = Set("none", "battle_start", "turn_start");
    private static readonly HashSet<string> RollValueTypes = Set(RollInt, RollStringName, RollBool);
    private static readonly HashSet<string> ModifierModes = Set("flat", "percent");

    private static readonly HashSet<string> BaseAttributes = Set(
        "strength", "agility", "constitution", "perception", "intelligence", "willpower"
    );

    private static readonly HashSet<string> AllowedAttributeIds = Set(
        "strength", "agility", "constitution", "perception", "intelligence", "willpower",
        "strength_modifier", "agility_modifier", "constitution_modifier", "perception_modifier",
        "intelligence_modifier", "willpower_modifier", "hp_max", "character_hp_max_percent_bonus",
        "mp_max", "stamina_max", "stamina_recovery_percent_bonus", "aura_max", "action_points",
        "action_threshold", "armor_class", "armor_ac_bonus", "shield_ac_bonus", "dodge_bonus",
        "deflection_bonus", "armor_max_dex_bonus"
    );

    private static readonly HashSet<string> SaveTags = Set(
        "sleep", "paralysis", "charm", "poison", "dragon_breath", "fireball", "chain_lightning",
        "equipment_disjunction", "magic", "illusion", "frightened", "execute", "temporal",
        "petrification", "antidote", "strength", "agility", "constitution", "perception",
        "intelligence", "willpower", "dragon_frightful_presence"
    );

    private static readonly HashSet<string> DamageTags = Set(
        "acid", "fire", "force", "freeze", "lightning", "magic", "negative_energy",
        "physical_blunt", "physical_pierce", "physical_slash", "poison", "psychic", "radiant", "thunder"
    );

    private static readonly HashSet<string> MitigationTiers = Set("double", "half", "immune", "normal");

    internal static bool IsEffectType(string value) => EffectTypes.Contains(value ?? "");
    internal static bool IsTriggerType(string value) => TriggerTypes.Contains(value ?? "");
    internal static bool HasDispatch(string effect, string trigger) => DispatchPairs.Contains($"{effect}\t{trigger}");
    internal static bool IsStackPolicy(string value) => StackPolicies.Contains(value ?? "");
    internal static bool IsSourceKind(string value) => SourceKinds.Contains(value ?? "");
    internal static bool IsChargeScope(string value) => ChargeScopes.Contains(value ?? "");
    internal static bool IsChargeResetTiming(string value) => ChargeResetTimings.Contains(value ?? "");
    internal static bool IsRollValueType(string value) => RollValueTypes.Contains(value ?? "");
    internal static bool IsModifierMode(string value) => ModifierModes.Contains(value ?? "");
    internal static bool IsBaseAttribute(string value) => BaseAttributes.Contains(value ?? "");
    internal static bool IsAllowedAttributeId(string value) => AllowedAttributeIds.Contains(value ?? "");
    internal static bool IsSaveTag(string value) => SaveTags.Contains(value ?? "");
    internal static bool IsDamageTag(string value) => DamageTags.Contains(value ?? "");
    internal static bool IsMitigationTier(string value) => MitigationTiers.Contains(value ?? "");

    internal static bool IsInstanceSource(string value) =>
        value == CharacterSource || value == EquipmentRollSource;

    internal static bool IsFixedSource(string value) =>
        value == IdentitySource || value == EquipmentFixedSource;

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}

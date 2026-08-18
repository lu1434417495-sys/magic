#nullable enable

using System;
using System.Collections.Generic;

internal enum CombatTickEffectImportKind { None, Damage, MovementCost, Status }
internal enum CombatEffectLifetimeImportKind { Timed, Battle }
internal enum CombatPathStepAreaPatternImportKind { Single, Self, Diamond, Square, Radius, Cross }
internal enum DamageMitigationTierImportKind { Normal, Half, Double, Immune }
internal enum DamageCategoryImportKind { Physical, Spell, Magic, Energy }
internal enum ShieldAttributeModifierImportKind
{
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
}
internal enum CombatEffectTargetTeamFilterImportKind { Inherit, Self, Ally, Enemy, Any }
internal enum CombatEffectTargetOrderImportKind { LowestHpPercentThenUnitId }
internal enum CombatCognitionImportKind { Mindless, Instinctive, Sapient }
internal enum CombatTerrainContactImportKind { InterruptMovementOnFailedSave }
internal enum CombatBodySizeImportKind { Tiny, Small, Medium, Large, Huge, Gargantuan, Boss }
internal enum CombatForcedMoveImportKind
{
    Jump,
    Blink,
    WindPush,
    Evasive,
    Retreat,
    Knockback,
    Reposition,
    GrappleAscent,
    AirbornePull,
}
internal enum CombatStackBehaviorImportKind { Refresh, Add, Stack }
internal enum CombatDamageBonusConditionImportKind
{
    TargetLowHp,
    TargetDebuffCount,
    TargetCreatureType,
    TargetHardControlled,
    TargetHasShield,
}
internal enum CombatEffectTriggerEventImportKind
{
    AttackHit,
    CriticalHit,
    OrdinaryHit,
    SecondaryHit,
    ForcedMoveApplied,
}
internal enum CombatEffectTriggerConditionImportKind { BattleStart, OnFatalDamage }
internal enum CombatSaveDcModeImportKind { Static, CasterSpell }
internal enum CombatSaveTagImportKind
{
    Sleep,
    Paralysis,
    Charm,
    Poison,
    DragonBreath,
    Fireball,
    ChainLightning,
    EquipmentDisjunction,
    Magic,
    Illusion,
    Frightened,
    Execute,
    Temporal,
    Petrification,
    Antidote,
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
}
internal enum CombatResourceImportKind { Ap, Aura, Mp, Stamina }
internal enum CombatStatusSourceSelectorImportKind { Source }
internal enum CombatEquipmentSlotImportKind
{
    MainHand,
    OffHand,
    Head,
    Body,
    Hands,
    Feet,
    Cloak,
    Necklace,
    Ring1,
    Ring2,
    SpecialTrinket,
    Badge,
}
internal enum CombatOnKillGrantScopeImportKind { None, CurrentTurn }

internal static class SkillCombatEffectValueRules
{
    internal static bool TryTickEffect(string? value, out CombatTickEffectImportKind result) =>
        value switch
        {
            "" or "none" => Set(CombatTickEffectImportKind.None, out result),
            "damage" => Set(CombatTickEffectImportKind.Damage, out result),
            "movement_cost" => Set(CombatTickEffectImportKind.MovementCost, out result),
            "status" => Set(CombatTickEffectImportKind.Status, out result),
            _ => Fail(out result),
        };

    internal static bool TryLifetime(string? value, out CombatEffectLifetimeImportKind result) =>
        value switch
        {
            "timed" => Set(CombatEffectLifetimeImportKind.Timed, out result),
            "battle" => Set(CombatEffectLifetimeImportKind.Battle, out result),
            _ => Fail(out result),
        };

    internal static bool TryPathStepAreaPattern(
        string? value,
        out CombatPathStepAreaPatternImportKind result
    ) =>
        value switch
        {
            "single" => Set(CombatPathStepAreaPatternImportKind.Single, out result),
            "self" => Set(CombatPathStepAreaPatternImportKind.Self, out result),
            "diamond" => Set(CombatPathStepAreaPatternImportKind.Diamond, out result),
            "square" => Set(CombatPathStepAreaPatternImportKind.Square, out result),
            "radius" => Set(CombatPathStepAreaPatternImportKind.Radius, out result),
            "cross" => Set(CombatPathStepAreaPatternImportKind.Cross, out result),
            _ => Fail(out result),
        };

    internal static bool TryMitigationTier(string? value, out DamageMitigationTierImportKind result) =>
        value switch
        {
            "normal" => Set(DamageMitigationTierImportKind.Normal, out result),
            "half" => Set(DamageMitigationTierImportKind.Half, out result),
            "double" => Set(DamageMitigationTierImportKind.Double, out result),
            "immune" => Set(DamageMitigationTierImportKind.Immune, out result),
            _ => Fail(out result),
        };

    internal static bool TryDamageCategory(string? value, out DamageCategoryImportKind result) =>
        value switch
        {
            "physical" => Set(DamageCategoryImportKind.Physical, out result),
            "spell" => Set(DamageCategoryImportKind.Spell, out result),
            "magic" => Set(DamageCategoryImportKind.Magic, out result),
            "energy" => Set(DamageCategoryImportKind.Energy, out result),
            _ => Fail(out result),
        };

    internal static bool TryShieldAttribute(string? value, out ShieldAttributeModifierImportKind result) =>
        value switch
        {
            "strength_modifier" => Set(ShieldAttributeModifierImportKind.Strength, out result),
            "agility_modifier" => Set(ShieldAttributeModifierImportKind.Agility, out result),
            "constitution_modifier" => Set(ShieldAttributeModifierImportKind.Constitution, out result),
            "perception_modifier" => Set(ShieldAttributeModifierImportKind.Perception, out result),
            "intelligence_modifier" => Set(ShieldAttributeModifierImportKind.Intelligence, out result),
            "willpower_modifier" => Set(ShieldAttributeModifierImportKind.Willpower, out result),
            _ => Fail(out result),
        };

    internal static bool TryTargetOrder(string? value, out CombatEffectTargetOrderImportKind result) =>
        value == "lowest_hp_percent_then_unit_id"
            ? Set(CombatEffectTargetOrderImportKind.LowestHpPercentThenUnitId, out result)
            : Fail(out result);

    internal static bool TryEffectTargetTeamFilter(
        string? value,
        out CombatEffectTargetTeamFilterImportKind result
    ) =>
        value switch
        {
            "inherit" => Set(CombatEffectTargetTeamFilterImportKind.Inherit, out result),
            "self" => Set(CombatEffectTargetTeamFilterImportKind.Self, out result),
            "ally" => Set(CombatEffectTargetTeamFilterImportKind.Ally, out result),
            "enemy" => Set(CombatEffectTargetTeamFilterImportKind.Enemy, out result),
            "any" => Set(CombatEffectTargetTeamFilterImportKind.Any, out result),
            _ => Fail(out result),
        };

    internal static bool TryCognition(string? value, out CombatCognitionImportKind result) =>
        value switch
        {
            "mindless" => Set(CombatCognitionImportKind.Mindless, out result),
            "instinctive" => Set(CombatCognitionImportKind.Instinctive, out result),
            "sapient" => Set(CombatCognitionImportKind.Sapient, out result),
            _ => Fail(out result),
        };

    internal static bool TryTerrainContact(string? value, out CombatTerrainContactImportKind result) =>
        value == "interrupt_movement_on_failed_save"
            ? Set(CombatTerrainContactImportKind.InterruptMovementOnFailedSave, out result)
            : Fail(out result);

    internal static bool TryBodySize(string? value, out CombatBodySizeImportKind result) =>
        value switch
        {
            "tiny" => Set(CombatBodySizeImportKind.Tiny, out result),
            "small" => Set(CombatBodySizeImportKind.Small, out result),
            "medium" => Set(CombatBodySizeImportKind.Medium, out result),
            "large" => Set(CombatBodySizeImportKind.Large, out result),
            "huge" => Set(CombatBodySizeImportKind.Huge, out result),
            "gargantuan" => Set(CombatBodySizeImportKind.Gargantuan, out result),
            "boss" => Set(CombatBodySizeImportKind.Boss, out result),
            _ => Fail(out result),
        };

    internal static bool TryForcedMove(string? value, out CombatForcedMoveImportKind result) =>
        value switch
        {
            "jump" => Set(CombatForcedMoveImportKind.Jump, out result),
            "blink" => Set(CombatForcedMoveImportKind.Blink, out result),
            "wind_push" => Set(CombatForcedMoveImportKind.WindPush, out result),
            "evasive" => Set(CombatForcedMoveImportKind.Evasive, out result),
            "retreat" => Set(CombatForcedMoveImportKind.Retreat, out result),
            "knockback" => Set(CombatForcedMoveImportKind.Knockback, out result),
            "reposition" => Set(CombatForcedMoveImportKind.Reposition, out result),
            "grapple_ascent" => Set(CombatForcedMoveImportKind.GrappleAscent, out result),
            "airborne_pull" => Set(CombatForcedMoveImportKind.AirbornePull, out result),
            _ => Fail(out result),
        };

    internal static bool TryStackBehavior(string? value, out CombatStackBehaviorImportKind result) =>
        value switch
        {
            "refresh" => Set(CombatStackBehaviorImportKind.Refresh, out result),
            "add" => Set(CombatStackBehaviorImportKind.Add, out result),
            "stack" => Set(CombatStackBehaviorImportKind.Stack, out result),
            _ => Fail(out result),
        };

    internal static bool TryBonusCondition(string? value, out CombatDamageBonusConditionImportKind result) =>
        value switch
        {
            "target_low_hp" => Set(CombatDamageBonusConditionImportKind.TargetLowHp, out result),
            "target_debuff_count" => Set(CombatDamageBonusConditionImportKind.TargetDebuffCount, out result),
            "target_creature_type" => Set(CombatDamageBonusConditionImportKind.TargetCreatureType, out result),
            "target_hard_controlled" => Set(CombatDamageBonusConditionImportKind.TargetHardControlled, out result),
            "target_has_shield" => Set(CombatDamageBonusConditionImportKind.TargetHasShield, out result),
            _ => Fail(out result),
        };

    internal static bool TryTriggerEvent(string? value, out CombatEffectTriggerEventImportKind result) =>
        value switch
        {
            "attack_hit" => Set(CombatEffectTriggerEventImportKind.AttackHit, out result),
            "critical_hit" => Set(CombatEffectTriggerEventImportKind.CriticalHit, out result),
            "ordinary_hit" => Set(CombatEffectTriggerEventImportKind.OrdinaryHit, out result),
            "secondary_hit" => Set(CombatEffectTriggerEventImportKind.SecondaryHit, out result),
            "forced_move_applied" => Set(CombatEffectTriggerEventImportKind.ForcedMoveApplied, out result),
            _ => Fail(out result),
        };

    internal static bool TryTriggerCondition(string? value, out CombatEffectTriggerConditionImportKind result) =>
        value switch
        {
            "battle_start" => Set(CombatEffectTriggerConditionImportKind.BattleStart, out result),
            "on_fatal_damage" => Set(CombatEffectTriggerConditionImportKind.OnFatalDamage, out result),
            _ => Fail(out result),
        };

    internal static bool TrySaveDcMode(string? value, out CombatSaveDcModeImportKind result) =>
        value switch
        {
            "static" => Set(CombatSaveDcModeImportKind.Static, out result),
            "caster_spell" => Set(CombatSaveDcModeImportKind.CasterSpell, out result),
            _ => Fail(out result),
        };

    internal static bool TrySaveTag(string? value, out CombatSaveTagImportKind result) =>
        TryWireEnum(value, SaveTags, out result);

    internal static bool TryResource(string? value, out CombatResourceImportKind result) =>
        value switch
        {
            "ap" => Set(CombatResourceImportKind.Ap, out result),
            "aura" => Set(CombatResourceImportKind.Aura, out result),
            "mp" => Set(CombatResourceImportKind.Mp, out result),
            "stamina" => Set(CombatResourceImportKind.Stamina, out result),
            _ => Fail(out result),
        };

    internal static bool TryStatusSourceSelector(string? value, out CombatStatusSourceSelectorImportKind result) =>
        value == "source"
            ? Set(CombatStatusSourceSelectorImportKind.Source, out result)
            : Fail(out result);

    internal static bool TryEquipmentSlot(string? value, out CombatEquipmentSlotImportKind result) =>
        TryWireEnum(value, EquipmentSlots, out result);

    internal static bool TryGrantScope(string? value, out CombatOnKillGrantScopeImportKind result) =>
        value switch
        {
            "" => Set(CombatOnKillGrantScopeImportKind.None, out result),
            "current_turn" => Set(CombatOnKillGrantScopeImportKind.CurrentTurn, out result),
            _ => Fail(out result),
        };

    private static readonly IReadOnlyDictionary<string, CombatSaveTagImportKind> SaveTags =
        new Dictionary<string, CombatSaveTagImportKind>(StringComparer.Ordinal)
        {
            ["sleep"] = CombatSaveTagImportKind.Sleep,
            ["paralysis"] = CombatSaveTagImportKind.Paralysis,
            ["charm"] = CombatSaveTagImportKind.Charm,
            ["poison"] = CombatSaveTagImportKind.Poison,
            ["dragon_breath"] = CombatSaveTagImportKind.DragonBreath,
            ["fireball"] = CombatSaveTagImportKind.Fireball,
            ["chain_lightning"] = CombatSaveTagImportKind.ChainLightning,
            ["equipment_disjunction"] = CombatSaveTagImportKind.EquipmentDisjunction,
            ["magic"] = CombatSaveTagImportKind.Magic,
            ["illusion"] = CombatSaveTagImportKind.Illusion,
            ["frightened"] = CombatSaveTagImportKind.Frightened,
            ["execute"] = CombatSaveTagImportKind.Execute,
            ["temporal"] = CombatSaveTagImportKind.Temporal,
            ["petrification"] = CombatSaveTagImportKind.Petrification,
            ["antidote"] = CombatSaveTagImportKind.Antidote,
            ["strength"] = CombatSaveTagImportKind.Strength,
            ["agility"] = CombatSaveTagImportKind.Agility,
            ["constitution"] = CombatSaveTagImportKind.Constitution,
            ["perception"] = CombatSaveTagImportKind.Perception,
            ["intelligence"] = CombatSaveTagImportKind.Intelligence,
            ["willpower"] = CombatSaveTagImportKind.Willpower,
        };

    private static readonly IReadOnlyDictionary<string, CombatEquipmentSlotImportKind> EquipmentSlots =
        new Dictionary<string, CombatEquipmentSlotImportKind>(StringComparer.Ordinal)
        {
            ["main_hand"] = CombatEquipmentSlotImportKind.MainHand,
            ["off_hand"] = CombatEquipmentSlotImportKind.OffHand,
            ["head"] = CombatEquipmentSlotImportKind.Head,
            ["body"] = CombatEquipmentSlotImportKind.Body,
            ["hands"] = CombatEquipmentSlotImportKind.Hands,
            ["feet"] = CombatEquipmentSlotImportKind.Feet,
            ["cloak"] = CombatEquipmentSlotImportKind.Cloak,
            ["necklace"] = CombatEquipmentSlotImportKind.Necklace,
            ["ring_1"] = CombatEquipmentSlotImportKind.Ring1,
            ["ring_2"] = CombatEquipmentSlotImportKind.Ring2,
            ["special_trinket"] = CombatEquipmentSlotImportKind.SpecialTrinket,
            ["badge"] = CombatEquipmentSlotImportKind.Badge,
        };

    private static bool TryWireEnum<T>(
        string? value,
        IReadOnlyDictionary<string, T> values,
        out T result
    ) where T : struct
    {
        if (value != null && values.TryGetValue(value, out result))
            return true;
        result = default;
        return false;
    }

    private static bool Set<T>(T value, out T result) where T : struct
    {
        result = value;
        return true;
    }

    private static bool Fail<T>(out T result) where T : struct
    {
        result = default;
        return false;
    }
}

internal abstract class CombatEffectSchemaValues : IContentJsonSchemaStableStringValues
{
    protected CombatEffectSchemaValues(params string[] values) => Values = Array.AsReadOnly(values);
    public IReadOnlyList<string> Values { get; }
}

internal sealed class CombatTickEffectSchemaValues : CombatEffectSchemaValues { internal CombatTickEffectSchemaValues() : base("none", "damage", "movement_cost", "status") { } }
internal sealed class CombatEffectLifetimeSchemaValues : CombatEffectSchemaValues { internal CombatEffectLifetimeSchemaValues() : base("timed", "battle") { } }
internal sealed class CombatPathStepAreaPatternSchemaValues : CombatEffectSchemaValues { internal CombatPathStepAreaPatternSchemaValues() : base("single", "self", "diamond", "square", "radius", "cross") { } }
internal sealed class DamageMitigationTierSchemaValues : CombatEffectSchemaValues { internal DamageMitigationTierSchemaValues() : base("normal", "half", "double", "immune") { } }
internal sealed class DamageCategorySchemaValues : CombatEffectSchemaValues { internal DamageCategorySchemaValues() : base("physical", "spell", "magic", "energy") { } }
internal sealed class ShieldAttributeModifierSchemaValues : CombatEffectSchemaValues { internal ShieldAttributeModifierSchemaValues() : base("strength_modifier", "agility_modifier", "constitution_modifier", "perception_modifier", "intelligence_modifier", "willpower_modifier") { } }
internal sealed class CombatEffectTargetTeamFilterSchemaValues : CombatEffectSchemaValues { internal CombatEffectTargetTeamFilterSchemaValues() : base("inherit", "self", "ally", "enemy", "any") { } }
internal sealed class CombatEffectTargetOrderSchemaValues : CombatEffectSchemaValues { internal CombatEffectTargetOrderSchemaValues() : base("lowest_hp_percent_then_unit_id") { } }
internal sealed class CombatCognitionSchemaValues : CombatEffectSchemaValues { internal CombatCognitionSchemaValues() : base("mindless", "instinctive", "sapient") { } }
internal sealed class CombatTerrainContactSchemaValues : CombatEffectSchemaValues { internal CombatTerrainContactSchemaValues() : base("interrupt_movement_on_failed_save") { } }
internal sealed class CombatBodySizeSchemaValues : CombatEffectSchemaValues { internal CombatBodySizeSchemaValues() : base("tiny", "small", "medium", "large", "huge", "gargantuan", "boss") { } }
internal sealed class CombatForcedMoveSchemaValues : CombatEffectSchemaValues { internal CombatForcedMoveSchemaValues() : base("jump", "blink", "wind_push", "evasive", "retreat", "knockback", "reposition", "grapple_ascent", "airborne_pull") { } }
internal sealed class CombatStackBehaviorSchemaValues : CombatEffectSchemaValues { internal CombatStackBehaviorSchemaValues() : base("refresh", "add", "stack") { } }
internal sealed class CombatDamageBonusConditionSchemaValues : CombatEffectSchemaValues { internal CombatDamageBonusConditionSchemaValues() : base("target_low_hp", "target_debuff_count", "target_creature_type", "target_hard_controlled", "target_has_shield") { } }
internal sealed class CombatEffectTriggerEventSchemaValues : CombatEffectSchemaValues { internal CombatEffectTriggerEventSchemaValues() : base("attack_hit", "critical_hit", "ordinary_hit", "secondary_hit", "forced_move_applied") { } }
internal sealed class CombatEffectTriggerConditionSchemaValues : CombatEffectSchemaValues { internal CombatEffectTriggerConditionSchemaValues() : base("battle_start", "on_fatal_damage") { } }
internal sealed class CombatSaveDcModeSchemaValues : CombatEffectSchemaValues { internal CombatSaveDcModeSchemaValues() : base("static", "caster_spell") { } }
internal sealed class CombatSaveTagSchemaValues : CombatEffectSchemaValues { internal CombatSaveTagSchemaValues() : base("sleep", "paralysis", "charm", "poison", "dragon_breath", "fireball", "chain_lightning", "equipment_disjunction", "magic", "illusion", "frightened", "execute", "temporal", "petrification", "antidote", "strength", "agility", "constitution", "perception", "intelligence", "willpower") { } }
internal sealed class CombatResourceSchemaValues : CombatEffectSchemaValues { internal CombatResourceSchemaValues() : base("ap", "aura", "mp", "stamina") { } }
internal sealed class CombatStatusSourceSelectorSchemaValues : CombatEffectSchemaValues { internal CombatStatusSourceSelectorSchemaValues() : base("source") { } }
internal sealed class CombatEquipmentSlotSchemaValues : CombatEffectSchemaValues { internal CombatEquipmentSlotSchemaValues() : base("main_hand", "off_hand", "head", "body", "hands", "feet", "cloak", "necklace", "ring_1", "ring_2", "special_trinket", "badge") { } }
internal sealed class CombatOnKillGrantScopeSchemaValues : CombatEffectSchemaValues { internal CombatOnKillGrantScopeSchemaValues() : base("current_turn") { } }

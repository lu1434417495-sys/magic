#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed record EquipmentAbilityPayloadKindSpec(
    string Kind,
    Type JsonDtoType,
    Type ImportModelType
);

internal static class EquipmentAbilityPayloadKindCatalog
{
    internal static IReadOnlyDictionary<string, EquipmentAbilityPayloadKindSpec> Conditions { get; } =
        ReadOnly(new[]
        {
            Spec<HasStatusConditionPayloadJsonDto, HasStatusConditionPayloadImportModel>("has_status"),
            Spec<CompareFactConditionPayloadJsonDto, CompareFactConditionPayloadImportModel>("compare_fact"),
            Spec<HasEquipmentTagConditionPayloadJsonDto, HasEquipmentTagConditionPayloadImportModel>("has_equipment_tag"),
        });

    internal static IReadOnlyDictionary<string, EquipmentAbilityPayloadKindSpec> Actions { get; } =
        ReadOnly(new[]
        {
            Spec<AddDamageDiceActionPayloadJsonDto, AddDamageDiceActionPayloadImportModel>("add_damage_dice"),
            Spec<ImmediateWeaponAttackActionPayloadJsonDto, ImmediateWeaponAttackActionPayloadImportModel>("immediate_weapon_attack"),
            Spec<DealDamageActionPayloadJsonDto, DealDamageActionPayloadImportModel>("deal_damage"),
            Spec<HealActionPayloadJsonDto, HealActionPayloadImportModel>("heal"),
            Spec<HealFromFactActionPayloadJsonDto, HealFromFactActionPayloadImportModel>("heal_from_fact"),
            Spec<AttackRollBonusActionPayloadJsonDto, AttackRollBonusActionPayloadImportModel>("attack_roll_bonus"),
            Spec<AttackRollAdvantageActionPayloadJsonDto, AttackRollAdvantageActionPayloadImportModel>("attack_roll_advantage"),
            Spec<CriticalHitOverrideActionPayloadJsonDto, CriticalHitOverrideActionPayloadImportModel>("critical_hit_override"),
            Spec<EquipmentAttackDefenseModifierJsonDto, EquipmentAttackDefenseModifierImportModel>("attack_defense_modifier"),
            Spec<DamageRollModeOverrideActionPayloadJsonDto, DamageRollModeOverrideActionPayloadImportModel>("damage_roll_mode_override"),
            Spec<DamageReductionActionPayloadJsonDto, DamageReductionActionPayloadImportModel>("damage_reduction"),
            Spec<LootQuantityMultiplierActionPayloadJsonDto, LootQuantityMultiplierActionPayloadImportModel>("loot_quantity_multiplier"),
            Spec<ApplyStatusActionPayloadJsonDto, ApplyStatusActionPayloadImportModel>("apply_status"),
            Spec<ModifyActionPointsActionPayloadJsonDto, ModifyActionPointsActionPayloadImportModel>("modify_action_points"),
            Spec<ScheduleAreaEffectActionPayloadJsonDto, ScheduleAreaEffectActionPayloadImportModel>("schedule_area_effect"),
            Spec<ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto, ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel>("apply_battle_terrain_effect_after_check"),
            Spec<ApplyEdgeFeatureActionPayloadJsonDto, ApplyEdgeFeatureActionPayloadImportModel>("apply_edge_feature"),
            Spec<ModifyAbilityStateActionPayloadJsonDto, ModifyAbilityStateActionPayloadImportModel>("modify_ability_state"),
            Spec<MarkTargetActionPayloadJsonDto, MarkTargetActionPayloadImportModel>("mark_target"),
            Spec<ClearStatusActionPayloadJsonDto, ClearStatusActionPayloadImportModel>("clear_status"),
            Spec<TriggerSkillActionPayloadJsonDto, TriggerSkillActionPayloadImportModel>("trigger_skill"),
            Spec<SummonUnitsActionPayloadJsonDto, SummonUnitsActionPayloadImportModel>("summon_units"),
            Spec<ConsumeSummonedUnitsActionPayloadJsonDto, ConsumeSummonedUnitsActionPayloadImportModel>("consume_summoned_units"),
            Spec<ConsumeStatusStacksActionPayloadJsonDto, ConsumeStatusStacksActionPayloadImportModel>("consume_status_stacks"),
            Spec<SummonedUnitAttackRollModifierActionPayloadJsonDto, SummonedUnitAttackRollModifierActionPayloadImportModel>("summoned_unit_attack_roll_modifier"),
            Spec<EquipmentDurabilityDamageActionPayloadJsonDto, EquipmentDurabilityDamageActionPayloadImportModel>("equipment_durability_damage"),
        });

    internal static IReadOnlyList<ContentJsonSchemaClosedKindBranch> ConditionSchemaBranches { get; } =
        BuildSchemaBranches(Conditions);
    internal static IReadOnlyList<ContentJsonSchemaClosedKindBranch> ActionSchemaBranches { get; } =
        BuildSchemaBranches(Actions);

    internal static bool TryGetCondition(string? kind, out EquipmentAbilityPayloadKindSpec spec) =>
        Conditions.TryGetValue(kind ?? "", out spec!);
    internal static bool TryGetAction(string? kind, out EquipmentAbilityPayloadKindSpec spec) =>
        Actions.TryGetValue(kind ?? "", out spec!);

    private static EquipmentAbilityPayloadKindSpec Spec<TDto, TImport>(string kind) =>
        new(kind, typeof(TDto), typeof(TImport));

    private static IReadOnlyDictionary<string, EquipmentAbilityPayloadKindSpec> ReadOnly(
        IEnumerable<EquipmentAbilityPayloadKindSpec> specs
    )
    {
        var values = new Dictionary<string, EquipmentAbilityPayloadKindSpec>(StringComparer.Ordinal);
        foreach (EquipmentAbilityPayloadKindSpec spec in specs)
            values.Add(spec.Kind, spec);
        return new ReadOnlyDictionary<string, EquipmentAbilityPayloadKindSpec>(values);
    }

    private static IReadOnlyList<ContentJsonSchemaClosedKindBranch> BuildSchemaBranches(
        IReadOnlyDictionary<string, EquipmentAbilityPayloadKindSpec> values
    )
    {
        var result = new List<ContentJsonSchemaClosedKindBranch>();
        foreach (EquipmentAbilityPayloadKindSpec spec in values.Values)
            result.Add(new ContentJsonSchemaClosedKindBranch(spec.Kind, spec.JsonDtoType));
        return result.AsReadOnly();
    }
}

internal sealed class EquipmentAbilityConditionClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches =>
        EquipmentAbilityPayloadKindCatalog.ConditionSchemaBranches;
}

internal sealed class EquipmentAbilityActionClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches =>
        EquipmentAbilityPayloadKindCatalog.ActionSchemaBranches;
}

#nullable enable

using System;
using System.Collections.Generic;

internal enum CombatEffectPayloadShape
{
    Empty,
    Status,
    Heal,
    EquipmentDurabilityDamage,
    RepeatAttackUntilFail,
    LayeredBarrier,
    GradedSaveExecute,
    DispelMagic,
    OnKillGainResources,
}

internal static class SkillFullCombatEffectClosedSpec
{
    private sealed record Entry(
        string WireValue,
        CombatEffectImportKind Kind,
        CombatEffectPayloadShape PayloadShape,
        Type PayloadDtoType
    );

    private static readonly IReadOnlyList<Entry> Entries = Array.AsReadOnly(
        new[]
        {
            Empty("body_size_category_override", CombatEffectImportKind.BodySizeCategoryOverride),
            Empty("chain_damage", CombatEffectImportKind.ChainDamage),
            Empty("charge", CombatEffectImportKind.Charge),
            Empty("cleanse_harmful", CombatEffectImportKind.CleanseHarmful),
            Empty("damage", CombatEffectImportKind.Damage),
            Typed("dispel_magic", CombatEffectImportKind.DispelMagic, CombatEffectPayloadShape.DispelMagic, typeof(DispelMagicEffectPayloadJsonDto)),
            Typed("equipment_durability_damage", CombatEffectImportKind.EquipmentDurabilityDamage, CombatEffectPayloadShape.EquipmentDurabilityDamage, typeof(EquipmentDurabilityDamageEffectPayloadJsonDto)),
            Empty("erase_status", CombatEffectImportKind.EraseStatus),
            Empty("execute", CombatEffectImportKind.Execute),
            Empty("fixed_repeat_attack", CombatEffectImportKind.FixedRepeatAttack),
            Empty("forced_move", CombatEffectImportKind.ForcedMove),
            Typed("graded_save_execute", CombatEffectImportKind.GradedSaveExecute, CombatEffectPayloadShape.GradedSaveExecute, typeof(GradedSaveExecuteEffectPayloadJsonDto)),
            Typed("heal", CombatEffectImportKind.Heal, CombatEffectPayloadShape.Heal, typeof(HealEffectPayloadJsonDto)),
            Empty("heal_fatal", CombatEffectImportKind.HealFatal),
            Empty("height_delta", CombatEffectImportKind.HeightDelta),
            Typed(CombatEffectImportClosedSpec.LayeredBarrierKindValue, CombatEffectImportKind.LayeredBarrier, CombatEffectPayloadShape.LayeredBarrier, typeof(LayeredBarrierEffectPayloadJsonDto)),
            Typed("on_kill_gain_resources", CombatEffectImportKind.OnKillGainResources, CombatEffectPayloadShape.OnKillGainResources, typeof(OnKillGainResourcesEffectPayloadJsonDto)),
            Empty("path_step_aoe", CombatEffectImportKind.PathStepAoe),
            Empty("position_swap", CombatEffectImportKind.PositionSwap),
            Typed("repeat_attack_until_fail", CombatEffectImportKind.RepeatAttackUntilFail, CombatEffectPayloadShape.RepeatAttackUntilFail, typeof(RepeatAttackUntilFailEffectPayloadJsonDto)),
            Empty("shield", CombatEffectImportKind.Shield),
            Empty("source_retreat", CombatEffectImportKind.SourceRetreat),
            Empty("stamina_restore", CombatEffectImportKind.StaminaRestore),
            Typed("status", CombatEffectImportKind.Status, CombatEffectPayloadShape.Status, typeof(StatusEffectPayloadJsonDto)),
            Empty("terrain_effect", CombatEffectImportKind.TerrainEffect),
            Empty("terrain_replace", CombatEffectImportKind.TerrainReplace),
            Empty("terrain_replace_to", CombatEffectImportKind.TerrainReplaceTo),
            Empty("vault_behind_target", CombatEffectImportKind.VaultBehindTarget),
        }
    );

    internal static IReadOnlyList<ContentJsonSchemaClosedKindBranch> SchemaBranches { get; } =
        BuildSchemaBranches();

    internal static bool TryParseKind(string? value, out CombatEffectImportKind result)
    {
        foreach (Entry entry in Entries)
        {
            if (!string.Equals(value, entry.WireValue, StringComparison.Ordinal))
                continue;
            result = entry.Kind;
            return true;
        }
        result = default;
        return false;
    }

    internal static CombatEffectPayloadShape GetPayloadShape(CombatEffectImportKind kind)
    {
        foreach (Entry entry in Entries)
        {
            if (entry.Kind == kind)
                return entry.PayloadShape;
        }
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered effect kind.");
    }

    internal static string GetWireValue(CombatEffectImportKind kind)
    {
        foreach (Entry entry in Entries)
        {
            if (entry.Kind == kind)
                return entry.WireValue;
        }
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered effect kind.");
    }

    internal static bool IsPayloadCompatible(
        CombatEffectImportKind kind,
        ICombatEffectPayloadImportModel payload
    ) =>
        GetPayloadShape(kind) switch
        {
            CombatEffectPayloadShape.Empty => payload is EmptyCombatEffectPayloadImportModel,
            CombatEffectPayloadShape.Status => payload is StatusEffectPayloadImportModel,
            CombatEffectPayloadShape.Heal => payload is HealEffectPayloadImportModel,
            CombatEffectPayloadShape.EquipmentDurabilityDamage => payload is EquipmentDurabilityDamageEffectPayloadImportModel,
            CombatEffectPayloadShape.RepeatAttackUntilFail => payload is RepeatAttackUntilFailEffectPayloadImportModel,
            CombatEffectPayloadShape.LayeredBarrier => payload is LayeredBarrierEffectPayloadImportModel,
            CombatEffectPayloadShape.GradedSaveExecute => payload is GradedSaveExecuteEffectPayloadImportModel,
            CombatEffectPayloadShape.DispelMagic => payload is DispelMagicEffectPayloadImportModel,
            CombatEffectPayloadShape.OnKillGainResources => payload is OnKillGainResourcesEffectPayloadImportModel,
            _ => false,
        };

    private static Entry Empty(string wireValue, CombatEffectImportKind kind) =>
        Typed(wireValue, kind, CombatEffectPayloadShape.Empty, typeof(EmptyCombatEffectPayloadJsonDto));

    private static Entry Typed(
        string wireValue,
        CombatEffectImportKind kind,
        CombatEffectPayloadShape payloadShape,
        Type payloadDtoType
    ) => new(wireValue, kind, payloadShape, payloadDtoType);

    private static IReadOnlyList<ContentJsonSchemaClosedKindBranch> BuildSchemaBranches()
    {
        var result = new ContentJsonSchemaClosedKindBranch[Entries.Count];
        for (int index = 0; index < Entries.Count; index += 1)
        {
            Entry entry = Entries[index];
            result[index] = new ContentJsonSchemaClosedKindBranch(
                entry.WireValue,
                entry.PayloadDtoType
            );
        }
        return Array.AsReadOnly(result);
    }
}

using Godot;
using System.Collections.Generic;

internal enum CombatResourceIdKind
{
    Unknown = 0,
    Hp,
    Stamina,
    Mp,
    Aura,
}

internal static class CombatResourceIds
{
    private static readonly StringName HpId = "hp";
    private static readonly StringName StaminaId = "stamina";
    private static readonly StringName MpId = "mp";
    private static readonly StringName AuraId = "aura";

    internal static IReadOnlyList<StringName> DefaultUnlocked { get; } =
        new List<StringName> { HpId, StaminaId };

    internal static CombatResourceIdKind ToResourceKind(StringName value)
    {
        if (value == HpId)
            return CombatResourceIdKind.Hp;
        if (value == StaminaId)
            return CombatResourceIdKind.Stamina;
        if (value == MpId)
            return CombatResourceIdKind.Mp;
        if (value == AuraId)
            return CombatResourceIdKind.Aura;
        return CombatResourceIdKind.Unknown;
    }

    internal static StringName ToStringName(CombatResourceIdKind kind)
    {
        return kind switch
        {
            CombatResourceIdKind.Hp => HpId,
            CombatResourceIdKind.Stamina => StaminaId,
            CombatResourceIdKind.Mp => MpId,
            CombatResourceIdKind.Aura => AuraId,
            _ => "",
        };
    }
}

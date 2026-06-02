using Godot;

public enum CombatResourceKind
{
    None = 0,
    Ap,
    Aura,
    Mp,
    Stamina,
}

internal static class CombatResourceKindUtils
{
    internal static CombatResourceKind FromStringName(
        StringName value,
        CombatResourceKind fallback = CombatResourceKind.None
    )
    {
        if (value == "ap")
            return CombatResourceKind.Ap;
        if (value == "aura")
            return CombatResourceKind.Aura;
        if (value == "mp")
            return CombatResourceKind.Mp;
        if (value == "stamina")
            return CombatResourceKind.Stamina;
        return fallback;
    }

    internal static StringName ToStringName(CombatResourceKind value)
    {
        return value switch
        {
            CombatResourceKind.Ap => new StringName("ap"),
            CombatResourceKind.Aura => new StringName("aura"),
            CombatResourceKind.Mp => new StringName("mp"),
            CombatResourceKind.Stamina => new StringName("stamina"),
            _ => new StringName(""),
        };
    }

    internal static string ToLabel(CombatResourceKind value)
    {
        return value switch
        {
            CombatResourceKind.Ap => "AP",
            CombatResourceKind.Aura => "斗气",
            CombatResourceKind.Mp => "法力",
            CombatResourceKind.Stamina => "体力",
            _ => "",
        };
    }

    internal static string ToAbbr(CombatResourceKind value)
    {
        return value switch
        {
            CombatResourceKind.Ap => "AP",
            CombatResourceKind.Aura => "AU",
            CombatResourceKind.Mp => "MP",
            CombatResourceKind.Stamina => "ST",
            _ => "",
        };
    }

    internal static string ToEffectiveCostField(CombatResourceKind value)
    {
        return value switch
        {
            CombatResourceKind.Ap => "ap_cost",
            CombatResourceKind.Aura => "aura_cost",
            CombatResourceKind.Mp => "mp_cost",
            CombatResourceKind.Stamina => "stamina_cost",
            _ => "",
        };
    }

    internal static string ToCurrentUnitField(CombatResourceKind value)
    {
        return value switch
        {
            CombatResourceKind.Ap => "current_ap",
            CombatResourceKind.Aura => "current_aura",
            CombatResourceKind.Mp => "current_mp",
            CombatResourceKind.Stamina => "current_stamina",
            _ => "",
        };
    }
}

using System.Collections.Generic;
using Godot;

internal static class CombatEffectCategoryContentRules
{
    internal static readonly StringName Spell = "spell";
    internal static readonly StringName Projectile = "projectile";
    internal static readonly StringName NonmagicalProjectile = "nonmagical_projectile";
    internal static readonly StringName MagicalProjectile = "magical_projectile";
    internal static readonly StringName Poison = "poison";
    internal static readonly StringName Petrification = "petrification";
    internal static readonly StringName BreathWeapon = "breath_weapon";
    internal static readonly StringName MentalAttack = "mental_attack";
    internal static readonly StringName Psychic = "psychic";
    internal static readonly StringName ForceEffect = "force_effect";
    internal static readonly StringName Antimagic = "antimagic";

    internal static IReadOnlyList<StringName> RequiredDeliveryCategories(
        bool isMageMagic,
        bool isDragonBreath
    )
    {
        var required = new List<StringName>();
        if (isMageMagic)
            required.Add(Spell);

        if (isDragonBreath)
            required.Add(BreathWeapon);

        return required;
    }

    internal static bool IsDerivedProjectileCategory(StringName value)
    {
        return value == Projectile
            || value == NonmagicalProjectile
            || value == MagicalProjectile;
    }

    internal static bool IsRemovedProjectileCategory(StringName value)
    {
        return value == "nonmagical_missile" || value == "magical_missile";
    }

    internal static IReadOnlyList<StringName> RequiredEffectCategories(
        StringName damageTag,
        StringName saveTag,
        BattleEffectKind effectKind
    )
    {
        var required = new List<StringName>();
        if (damageTag == "force")
            required.Add(ForceEffect);
        else if (damageTag == "psychic")
            required.Add(Psychic);
        else if (damageTag == "poison")
            required.Add(Poison);

        if (saveTag == "petrification")
            required.Add(Petrification);
        if (saveTag == "illusion" || saveTag == "charm" || saveTag == "sleep")
        {
            required.Add(MentalAttack);
        }

        if (effectKind == BattleEffectKind.DispelMagic)
            required.Add(Antimagic);

        return required;
    }
}

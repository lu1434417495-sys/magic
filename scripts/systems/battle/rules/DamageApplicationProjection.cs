using System;
using Godot;

internal readonly record struct DamageApplicationProjection(
    int ResolvedDamage,
    int ShieldAbsorbed,
    int ShieldDrain,
    bool ShieldBroken,
    int HpDamage,
    int HpBefore,
    int ProjectedHp,
    int ShieldHpBefore,
    int ProjectedShieldHp,
    bool WouldBeFatalBeforeDeathPrevention
)
{
    internal static DamageApplicationProjection Project(
        BattleUnitState targetUnit,
        DamageApplicationInput damageInput
    )
    {
        int resolvedDamage = Math.Max(damageInput.ResolvedDamage, 0);
        int hpBefore = Math.Max(targetUnit?.current_hp ?? 0, 0);
        int shieldBefore = 0;
        if (targetUnit != null && !damageInput.BypassShield)
        {
            int shieldMax = Math.Max(targetUnit.shield_max_hp, 0);
            bool hasShield =
                targetUnit.current_shield_hp > 0 && shieldMax > 0 && targetUnit.shield_duration > 0;
            shieldBefore = hasShield ? Math.Clamp(targetUnit.current_shield_hp, 0, shieldMax) : 0;
        }

        double shieldEfficiency = damageInput.ShieldAbsorptionPercent / 100.0;
        int shieldAbsorbed = 0;
        int shieldDrain = 0;
        if (!damageInput.BypassShield && shieldBefore > 0 && shieldEfficiency > 0.0)
        {
            int shieldCapacity = (int)Math.Ceiling(shieldBefore * shieldEfficiency);
            shieldAbsorbed = Math.Min(resolvedDamage, shieldCapacity);
            shieldDrain = Math.Min(
                (int)Math.Ceiling(shieldAbsorbed / shieldEfficiency),
                shieldBefore
            );
        }

        int hpDamage = Math.Max(resolvedDamage - shieldAbsorbed, 0);
        int projectedHp = Math.Max(hpBefore - hpDamage, 0);
        int projectedShieldHp = Math.Max(shieldBefore - shieldDrain, 0);
        bool wouldBeFatal = hpDamage > 0 && hpBefore - hpDamage <= damageInput.MinHpAfterDamage;
        return new DamageApplicationProjection(
            resolvedDamage,
            shieldAbsorbed,
            shieldDrain,
            shieldAbsorbed > 0 && projectedShieldHp <= 0,
            hpDamage,
            hpBefore,
            projectedHp,
            shieldBefore,
            projectedShieldHp,
            wouldBeFatal
        );
    }
}

using System;
using System.Collections.Generic;

internal sealed class BattlePreparedChainDamage
{
    internal static BattlePreparedChainDamage Empty { get; } =
        new(null, Array.Empty<CombatEffectDefinition>(), BattleChainDamagePlan.Empty);

    internal BattlePreparedChainDamage(
        CombatEffectDefinition chainEffect,
        IReadOnlyList<CombatEffectDefinition> targetEffects,
        BattleChainDamagePlan plan
    )
    {
        ChainEffect = chainEffect;
        TargetEffects = targetEffects ?? Array.Empty<CombatEffectDefinition>();
        Plan = plan ?? BattleChainDamagePlan.Empty;
    }

    internal CombatEffectDefinition ChainEffect { get; }
    internal IReadOnlyList<CombatEffectDefinition> TargetEffects { get; }
    internal BattleChainDamagePlan Plan { get; }
    internal bool IsConfigured =>
        ChainEffect?.ChainDamage != null && TargetEffects.Count > 0;
}

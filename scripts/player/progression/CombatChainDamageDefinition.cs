using System.Collections.Generic;
using Godot;

public sealed class CombatChainDamageDefinition
{
    public CombatChainDamageDefinition(
        int baseHopRange,
        int conductiveHopRange,
        int maxTotalTargets,
        IReadOnlyList<StringName> conductiveStatusIds = null,
        IReadOnlyList<StringName> conductiveTerrainEffectIds = null,
        int backlashHopRangeBonus = 0
    )
    {
        BaseHopRange = baseHopRange;
        ConductiveHopRange = conductiveHopRange;
        MaxTotalTargets = maxTotalTargets;
        ConductiveStatusIds = SkillDefinitionCollectionFreeze.List(
            conductiveStatusIds
        );
        ConductiveTerrainEffectIds = SkillDefinitionCollectionFreeze.List(
            conductiveTerrainEffectIds
        );
        BacklashHopRangeBonus = backlashHopRangeBonus;
    }

    public int BaseHopRange { get; }
    public int ConductiveHopRange { get; }
    public int MaxTotalTargets { get; }
    public bool HasTargetLimit => MaxTotalTargets > 0;
    public IReadOnlyList<StringName> ConductiveStatusIds { get; }
    public IReadOnlyList<StringName> ConductiveTerrainEffectIds { get; }
    public int BacklashHopRangeBonus { get; }
}

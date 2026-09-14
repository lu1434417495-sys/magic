#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public enum CombatEffectPayloadKind
{
    Empty = 0,
    Status,
    Heal,
    EquipmentDurabilityDamage,
    RepeatAttackUntilFail,
    LayeredBarrier,
    GradedSaveExecute,
    DispelMagic,
    OnKillGainResources,
}

public interface ICombatEffectPayloadDefinition
{
    CombatEffectPayloadKind Kind { get; }
}

public sealed class EmptyCombatEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public static EmptyCombatEffectPayloadDefinition Instance { get; } = new();

    private EmptyCombatEffectPayloadDefinition() { }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.Empty;
}

public sealed class StatusEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public StatusEffectPayloadDefinition(
        StringName? breaksBarrierLayer = default,
        StringName? sourceSkillId = default,
        IReadOnlyDictionary<StringName, int>? saveBonusByTag = null,
        int sourceBoundAttackRollPenalty = 0,
        int sourceBoundAttackRollPenaltyMinStacks = 1,
        int sourceBoundIncomingAttackRollBonusPerStack = 0,
        int sourceBoundIncomingAttackRollBonusMinStacks = 1
    )
    {
        BreaksBarrierLayer = Normalize(breaksBarrierLayer);
        SourceSkillId = Normalize(sourceSkillId);
        SaveBonusByTag = FreezeMap(saveBonusByTag);
        SourceBoundAttackRollPenalty = sourceBoundAttackRollPenalty;
        SourceBoundAttackRollPenaltyMinStacks = sourceBoundAttackRollPenaltyMinStacks;
        SourceBoundIncomingAttackRollBonusPerStack =
            sourceBoundIncomingAttackRollBonusPerStack;
        SourceBoundIncomingAttackRollBonusMinStacks =
            sourceBoundIncomingAttackRollBonusMinStacks;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.Status;
    public StringName BreaksBarrierLayer { get; }
    public StringName SourceSkillId { get; }
    public IReadOnlyDictionary<StringName, int> SaveBonusByTag { get; }
    public int SourceBoundAttackRollPenalty { get; }
    public int SourceBoundAttackRollPenaltyMinStacks { get; }
    public int SourceBoundIncomingAttackRollBonusPerStack { get; }
    public int SourceBoundIncomingAttackRollBonusMinStacks { get; }

    private static StringName Normalize(StringName? value) => value ?? new StringName("");

    private static IReadOnlyDictionary<StringName, int> FreezeMap(
        IReadOnlyDictionary<StringName, int>? values
    )
    {
        if (values == null || values.Count == 0)
        {
            return new ReadOnlyDictionary<StringName, int>(
                new Dictionary<StringName, int>()
            );
        }
        return new ReadOnlyDictionary<StringName, int>(
            new Dictionary<StringName, int>(values)
        );
    }
}

public sealed class HealEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public HealEffectPayloadDefinition(bool conModHeal = false)
    {
        ConModHeal = conModHeal;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.Heal;
    public bool ConModHeal { get; }
}

public sealed class EquipmentDurabilityDamageEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public EquipmentDurabilityDamageEffectPayloadDefinition(
        int maxDamagedItems = 1,
        IEnumerable<StringName>? targetSlots = null
    )
    {
        MaxDamagedItems = maxDamagedItems;
        TargetSlots = FreezeNames(targetSlots);
    }

    public CombatEffectPayloadKind Kind =>
        CombatEffectPayloadKind.EquipmentDurabilityDamage;
    public int MaxDamagedItems { get; }
    public IReadOnlyList<StringName> TargetSlots { get; }

    private static IReadOnlyList<StringName> FreezeNames(
        IEnumerable<StringName>? values
    )
    {
        if (values == null)
            return Array.Empty<StringName>();
        var copy = new List<StringName>();
        foreach (StringName value in values)
            copy.Add(value ?? new StringName(""));
        return copy.Count == 0
            ? Array.Empty<StringName>()
            : Array.AsReadOnly(copy.ToArray());
    }
}

public sealed class RepeatAttackUntilFailEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public RepeatAttackUntilFailEffectPayloadDefinition(
        int baseAttackBonus = 0,
        StringName? costResource = default,
        int followUpCostAddition = 0,
        double followUpCostMultiplier = 1.0,
        int followUpAttackPenalty = 0,
        IReadOnlyDictionary<int, int>? penaltyFreeStagesByLevel = null,
        bool sameTargetOnly = false,
        int followUpFixedCost = 0,
        bool exponentialPenalty = false,
        bool stopOnInsufficientResource = false
    )
    {
        BaseAttackBonus = baseAttackBonus;
        CostResource = costResource ?? new StringName("");
        FollowUpCostAddition = followUpCostAddition;
        FollowUpCostMultiplier = followUpCostMultiplier;
        FollowUpAttackPenalty = followUpAttackPenalty;
        PenaltyFreeStagesByLevel = FreezeMap(penaltyFreeStagesByLevel);
        SameTargetOnly = sameTargetOnly;
        FollowUpFixedCost = followUpFixedCost;
        ExponentialPenalty = exponentialPenalty;
        StopOnInsufficientResource = stopOnInsufficientResource;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.RepeatAttackUntilFail;
    public int BaseAttackBonus { get; }
    public StringName CostResource { get; }
    public int FollowUpCostAddition { get; }
    public double FollowUpCostMultiplier { get; }
    public int FollowUpAttackPenalty { get; }
    public IReadOnlyDictionary<int, int> PenaltyFreeStagesByLevel { get; }
    public bool SameTargetOnly { get; }
    public int FollowUpFixedCost { get; }
    public bool ExponentialPenalty { get; }
    public bool StopOnInsufficientResource { get; }

    internal CombatResourceKind CostResourceKind =>
        CombatResourceKindUtils.FromStringName(CostResource);

    private static IReadOnlyDictionary<int, int> FreezeMap(
        IReadOnlyDictionary<int, int>? values
    )
    {
        if (values == null || values.Count == 0)
            return new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
        return new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(values));
    }
}

public sealed class LayeredBarrierEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public LayeredBarrierEffectPayloadDefinition(
        StringName? areaPattern = default,
        StringName? profileId = default,
        int radiusCells = 0,
        int saveDc = 0
    )
    {
        AreaPattern = areaPattern ?? new StringName("");
        ProfileId = profileId ?? new StringName("");
        RadiusCells = radiusCells;
        SaveDc = saveDc;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.LayeredBarrier;
    public StringName AreaPattern { get; }
    public StringName ProfileId { get; }
    public int RadiusCells { get; }
    public int SaveDc { get; }

    internal BattleAreaPattern AreaPatternKind => BattleTypedNames.ToAreaPattern(AreaPattern);
}

public sealed class GradedSaveExecuteEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public GradedSaveExecuteEffectPayloadDefinition(
        int criticalFailureDamageDiceCount = 0,
        int criticalFailureDamageDiceSides = 0,
        int criticalFailureExecuteThresholdMaxHpPercent = 0,
        int criticalFailureFrightenedDurationTu = 0,
        int criticalFailureStunnedDurationTu = 0,
        int failureDamageDiceCount = 0,
        int failureDamageDiceSides = 0,
        int failureExecuteThresholdFixed = 0,
        int failureExecuteThresholdMaxHpPercent = 0,
        int failureFrightenedDurationTu = 0,
        int failureReactionLockDurationTu = 0,
        StringName? profileId = default,
        int successAftershockDurationTu = 0
    )
    {
        CriticalFailureDamageDiceCount = criticalFailureDamageDiceCount;
        CriticalFailureDamageDiceSides = criticalFailureDamageDiceSides;
        CriticalFailureExecuteThresholdMaxHpPercent =
            criticalFailureExecuteThresholdMaxHpPercent;
        CriticalFailureFrightenedDurationTu = criticalFailureFrightenedDurationTu;
        CriticalFailureStunnedDurationTu = criticalFailureStunnedDurationTu;
        FailureDamageDiceCount = failureDamageDiceCount;
        FailureDamageDiceSides = failureDamageDiceSides;
        FailureExecuteThresholdFixed = failureExecuteThresholdFixed;
        FailureExecuteThresholdMaxHpPercent = failureExecuteThresholdMaxHpPercent;
        FailureFrightenedDurationTu = failureFrightenedDurationTu;
        FailureReactionLockDurationTu = failureReactionLockDurationTu;
        ProfileId = profileId ?? new StringName("");
        SuccessAftershockDurationTu = successAftershockDurationTu;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.GradedSaveExecute;
    public int CriticalFailureDamageDiceCount { get; }
    public int CriticalFailureDamageDiceSides { get; }
    public int CriticalFailureExecuteThresholdMaxHpPercent { get; }
    public int CriticalFailureFrightenedDurationTu { get; }
    public int CriticalFailureStunnedDurationTu { get; }
    public int FailureDamageDiceCount { get; }
    public int FailureDamageDiceSides { get; }
    public int FailureExecuteThresholdFixed { get; }
    public int FailureExecuteThresholdMaxHpPercent { get; }
    public int FailureFrightenedDurationTu { get; }
    public int FailureReactionLockDurationTu { get; }
    public StringName ProfileId { get; }
    public int SuccessAftershockDurationTu { get; }
}

public sealed class DispelMagicEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public DispelMagicEffectPayloadDefinition(StringName? breaksBarrierLayer = default)
    {
        BreaksBarrierLayer = breaksBarrierLayer ?? new StringName("");
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.DispelMagic;
    public StringName BreaksBarrierLayer { get; }
}

public sealed class OnKillGainResourcesEffectPayloadDefinition
    : ICombatEffectPayloadDefinition
{
    public OnKillGainResourcesEffectPayloadDefinition(
        StringName? grantScope = default,
        bool requireTargetDefeatedBySameSkill = false,
        bool stackOnMultipleKills = false
    )
    {
        GrantScope = grantScope ?? new StringName("");
        RequireTargetDefeatedBySameSkill = requireTargetDefeatedBySameSkill;
        StackOnMultipleKills = stackOnMultipleKills;
    }

    public CombatEffectPayloadKind Kind => CombatEffectPayloadKind.OnKillGainResources;
    public StringName GrantScope { get; }
    public bool RequireTargetDefeatedBySameSkill { get; }
    public bool StackOnMultipleKills { get; }
}

public sealed class CombatSourceStatusGrantDefinition
{
    public CombatSourceStatusGrantDefinition(
        StringName statusId,
        int power = 1,
        int durationTu = 180,
        int stackLimit = 20
    )
    {
        StatusId = statusId ?? new StringName("");
        Power = power;
        DurationTu = durationTu;
        StackLimit = stackLimit;
    }

    public StringName StatusId { get; }
    public int Power { get; }
    public int DurationTu { get; }
    public int StackLimit { get; }
}

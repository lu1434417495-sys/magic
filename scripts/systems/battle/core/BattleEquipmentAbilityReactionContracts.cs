using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAbilityAfterHitContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
    public bool ApplyDamageDiceActions { get; init; } = true;
    public int ForcedRollValue { get; init; }
    public int WeaponHpDamage { get; init; }
    public BattleEventBatch Batch { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityAttackCheckContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
    public StringName SkillId { get; init; } = "";
}

internal sealed class BattleEquipmentAbilityBonusDamageDiceContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
}

internal sealed class BattleEquipmentAbilityDamageRollModeContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public StringName CurrentRollMode { get; init; } = "";
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
}

internal sealed class BattleEquipmentAbilityDamageReductionContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public StringName DamageTag { get; init; } = "";
    public bool AttackSucceeded { get; init; }
    public bool CriticalHit { get; init; }
}

internal sealed class BattleEquipmentAbilityDamageAppliedContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public BattleState BattleState { get; init; }
    public int HpDamage { get; init; }
    public BattleSaveContext SaveContext { get; init; } = BattleSaveContext.Empty;
}

internal sealed class BattleEquipmentAbilityRollResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ReactionId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int RolledValue { get; init; }
    public StringName Compare { get; init; } = "";
    public int Threshold { get; init; }
    public bool Passed { get; init; }
}

internal sealed class BattleEquipmentAbilityDurabilityResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public EquipmentDurabilityCommitResult CommitResult { get; init; }
}

internal sealed class BattleEquipmentAbilityBonusDamageDiceResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int DiceCount { get; init; }
    public int DiceSides { get; init; }
    public int FlatBonus { get; init; }
    public bool Subtract { get; init; }
    public StringName DamageType { get; init; } = "";
    public IReadOnlyList<StringName> DamageTags { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; init; } =
        Array.Empty<StringName>();
    public IReadOnlyList<StringName> MitigationBypassTiers { get; init; } =
        Array.Empty<StringName>();
}

internal sealed class BattleEquipmentAbilityCriticalHitOverrideResult
{
    internal static readonly BattleEquipmentAbilityCriticalHitOverrideResult None = new();

    public bool ForceCriticalOnHit { get; init; }
    public StringName SourceEquipmentInstanceId { get; init; } = "";
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
}

internal sealed class BattleEquipmentAbilityDamageReductionResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public int Amount { get; init; }
    public string Label { get; init; } = "";
}

internal sealed class BattleEquipmentAbilityStatusActionResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public StringName StatusId { get; init; } = "";
    public bool Applied { get; init; }
    public BattleSaveResult SaveResult { get; init; }
}

internal sealed class BattleEquipmentAbilityAfterHitResult
{
    private readonly List<BattleEquipmentAbilityRollResult> _rolls = new();
    private readonly List<BattleEquipmentAbilityDurabilityResult> _durabilityResults = new();
    private readonly List<BattleEquipmentAbilityBonusDamageDiceResult> _bonusDamageDice = new();
    private readonly List<BattleEquipmentAbilityStatusActionResult> _statusResults = new();
    private readonly List<BattleEquipmentAbilityTriggeredSkillResult> _triggeredSkillResults = new();

    public bool Resolved =>
        _rolls.Count > 0
        || _durabilityResults.Count > 0
        || _bonusDamageDice.Count > 0
        || _statusResults.Count > 0
        || _triggeredSkillResults.Count > 0;

    public IReadOnlyList<BattleEquipmentAbilityRollResult> Rolls => _rolls;
    public IReadOnlyList<BattleEquipmentAbilityDurabilityResult> DurabilityResults =>
        _durabilityResults;
    public IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> BonusDamageDice =>
        _bonusDamageDice;
    public IReadOnlyList<BattleEquipmentAbilityStatusActionResult> StatusResults =>
        _statusResults;
    public IReadOnlyList<BattleEquipmentAbilityTriggeredSkillResult> TriggeredSkillResults =>
        _triggeredSkillResults;

    internal void AddRoll(BattleEquipmentAbilityRollResult result)
    {
        if (result != null)
            _rolls.Add(result);
    }

    internal void AddDurabilityResult(BattleEquipmentAbilityDurabilityResult result)
    {
        if (result != null)
            _durabilityResults.Add(result);
    }

    internal void AddBonusDamageDice(BattleEquipmentAbilityBonusDamageDiceResult result)
    {
        if (result != null)
            _bonusDamageDice.Add(result);
    }

    internal void AddStatusResult(BattleEquipmentAbilityStatusActionResult result)
    {
        if (result != null)
            _statusResults.Add(result);
    }

    internal void AddTriggeredSkillResult(BattleEquipmentAbilityTriggeredSkillResult result)
    {
        if (result != null)
            _triggeredSkillResults.Add(result);
    }

    public bool HasRoll(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityRollResult roll in _rolls)
            if (roll.BindingId == bindingId)
                return true;
        return false;
    }

    public bool HasRoll(StringName bindingId, int rolledValue, bool passed)
    {
        foreach (BattleEquipmentAbilityRollResult roll in _rolls)
            if (
                roll.BindingId == bindingId
                && roll.RolledValue == rolledValue
                && roll.Passed == passed
            )
                return true;
        return false;
    }

    public bool HasDestroyedEquipment(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityDurabilityResult result in _durabilityResults)
            if (result.BindingId == bindingId && result.CommitResult?.Destroyed == true)
                return true;
        return false;
    }

    public bool HasDestroyedEquipment(
        StringName bindingId,
        StringName targetUnitId,
        StringName slotId,
        StringName equipmentInstanceId
    )
    {
        foreach (BattleEquipmentAbilityDurabilityResult result in _durabilityResults)
        {
            EquipmentDurabilityCommitResult commit = result.CommitResult;
            if (
                result.BindingId == bindingId
                && commit?.Destroyed == true
                && commit.TargetUnitId == targetUnitId
                && commit.SlotId == slotId
                && commit.EquipmentInstanceId == equipmentInstanceId
            )
            {
                return true;
            }
        }
        return false;
    }

    public bool HasBonusDamageDice(StringName bindingId)
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in _bonusDamageDice)
            if (dice.BindingId == bindingId)
                return true;
        return false;
    }

    public bool HasBonusDamageDice(StringName bindingId, int diceCount, int diceSides)
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in _bonusDamageDice)
            if (
                dice.BindingId == bindingId
                && dice.DiceCount == diceCount
                && dice.DiceSides == diceSides
            )
                return true;
        return false;
    }
}

internal sealed class BattleEquipmentAbilityTriggeredSkillResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public bool MergeIntoParentResult { get; init; }
    public AttackEffectResolutionResult Resolution { get; init; }
}

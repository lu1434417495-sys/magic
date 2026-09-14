using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public enum CombatSkillLevelOverrideAttackResolutionMode
{
    Auto = 0,
    DirectEffect,
    FateAttack,
    ForceHitNoCrit,
}

public enum CombatSkillLevelOverrideAttackDefenseMode
{
    Normal = 0,
    Touch,
    FlatFooted,
}

public enum CombatSkillLevelOverrideAreaPattern
{
    Single = 0,
    Self,
    Diamond,
    Square,
    Radius,
    Cross,
    Line,
    Cone,
    NarrowCone,
    FrontArc,
}

public sealed class CombatSkillLevelOverrideImportModel
{
    public CombatSkillLevelOverrideImportModel(
        int? apCost = null,
        int? mpCost = null,
        int? staminaCost = null,
        int? mpCostPerTargetSlot = null,
        int? staminaCostPerTargetSlot = null,
        int? auraCost = null,
        int? cooldownTu = null,
        int? castingTimeTu = null,
        int? castingMaintenanceDc = null,
        int? castingSpellControlDc = null,
        PendingCastBindingModeKind? pendingCastBindingMode = null,
        int? attackRollBonus = null,
        CombatSkillLevelOverrideAttackResolutionMode? attackResolutionMode = null,
        CombatSkillLevelOverrideAttackDefenseMode? attackDefenseMode = null,
        int? areaValue = null,
        int? rangeValue = null,
        CombatSkillLevelOverrideAreaPattern? areaPattern = null,
        int? maxTargetCount = null,
        int? randomChainAttackCount = null
    )
    {
        ApCost = apCost;
        MpCost = mpCost;
        StaminaCost = staminaCost;
        MpCostPerTargetSlot = mpCostPerTargetSlot;
        StaminaCostPerTargetSlot = staminaCostPerTargetSlot;
        AuraCost = auraCost;
        CooldownTu = cooldownTu;
        CastingTimeTu = castingTimeTu;
        CastingMaintenanceDc = castingMaintenanceDc;
        CastingSpellControlDc = castingSpellControlDc;
        PendingCastBindingMode = pendingCastBindingMode;
        AttackRollBonus = attackRollBonus;
        AttackResolutionMode = attackResolutionMode;
        AttackDefenseMode = attackDefenseMode;
        AreaValue = areaValue;
        RangeValue = rangeValue;
        AreaPattern = areaPattern;
        MaxTargetCount = maxTargetCount;
        RandomChainAttackCount = randomChainAttackCount;
    }

    public int? ApCost { get; }
    public int? MpCost { get; }
    public int? StaminaCost { get; }
    public int? MpCostPerTargetSlot { get; }
    public int? StaminaCostPerTargetSlot { get; }
    public int? AuraCost { get; }
    public int? CooldownTu { get; }
    public int? CastingTimeTu { get; }
    public int? CastingMaintenanceDc { get; }
    public int? CastingSpellControlDc { get; }
    public PendingCastBindingModeKind? PendingCastBindingMode { get; }
    public int? AttackRollBonus { get; }
    public CombatSkillLevelOverrideAttackResolutionMode? AttackResolutionMode { get; }
    public CombatSkillLevelOverrideAttackDefenseMode? AttackDefenseMode { get; }
    public int? AreaValue { get; }
    public int? RangeValue { get; }
    public CombatSkillLevelOverrideAreaPattern? AreaPattern { get; }
    public int? MaxTargetCount { get; }
    public int? RandomChainAttackCount { get; }

    internal CombatSkillLevelOverrideImportModel Overlay(
        CombatSkillLevelOverrideImportModel later
    )
    {
        if (later == null)
            return this;
        return new CombatSkillLevelOverrideImportModel(
            later.ApCost ?? ApCost,
            later.MpCost ?? MpCost,
            later.StaminaCost ?? StaminaCost,
            later.MpCostPerTargetSlot ?? MpCostPerTargetSlot,
            later.StaminaCostPerTargetSlot ?? StaminaCostPerTargetSlot,
            later.AuraCost ?? AuraCost,
            later.CooldownTu ?? CooldownTu,
            later.CastingTimeTu ?? CastingTimeTu,
            later.CastingMaintenanceDc ?? CastingMaintenanceDc,
            later.CastingSpellControlDc ?? CastingSpellControlDc,
            later.PendingCastBindingMode ?? PendingCastBindingMode,
            later.AttackRollBonus ?? AttackRollBonus,
            later.AttackResolutionMode ?? AttackResolutionMode,
            later.AttackDefenseMode ?? AttackDefenseMode,
            later.AreaValue ?? AreaValue,
            later.RangeValue ?? RangeValue,
            later.AreaPattern ?? AreaPattern,
            later.MaxTargetCount ?? MaxTargetCount,
            later.RandomChainAttackCount ?? RandomChainAttackCount
        );
    }
}

public sealed class SkillDescriptionVariables : IReadOnlyDictionary<string, string>
{
    private readonly ReadOnlyDictionary<string, string> _values;

    public SkillDescriptionVariables(IEnumerable<KeyValuePair<string, string>> values = null)
    {
        var sorted = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
        if (values != null)
        {
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (pair.Key == null)
                    throw new System.ArgumentException(
                        "Skill description variable keys must not be null.",
                        nameof(values)
                    );
                if (pair.Value == null)
                    throw new System.ArgumentException(
                        "Skill description variable values must not be null.",
                        nameof(values)
                    );
                if (!sorted.TryAdd(pair.Key, pair.Value))
                    throw new System.ArgumentException(
                        $"Skill description variable key '{pair.Key}' must be unique.",
                        nameof(values)
                    );
            }
        }
        _values = new ReadOnlyDictionary<string, string>(sorted);
    }

    public int Count => _values.Count;
    public IEnumerable<string> Keys => _values.Keys;
    public IEnumerable<string> Values => _values.Values;
    public string this[string key] => _values[key];

    public bool ContainsKey(string key) => key != null && _values.ContainsKey(key);

    public bool TryGetValue(string key, out string value)
    {
        if (key != null && _values.TryGetValue(key, out value))
            return true;
        value = "";
        return false;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
        _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class SkillTypedLevelValueMaps
{
    internal static IReadOnlyDictionary<int, TValue> Freeze<TValue>(
        IEnumerable<KeyValuePair<int, TValue>> values
    )
    {
        var sorted = new SortedDictionary<int, TValue>();
        if (values != null)
        {
            foreach (KeyValuePair<int, TValue> pair in values)
            {
                if (pair.Value is null)
                    throw new System.ArgumentException(
                        "Typed level map values must not be null.",
                        nameof(values)
                    );
                sorted.Add(pair.Key, pair.Value);
            }
        }
        return new ReadOnlyDictionary<int, TValue>(sorted);
    }
}

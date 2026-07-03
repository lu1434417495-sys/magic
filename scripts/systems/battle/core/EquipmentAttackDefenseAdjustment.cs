using System.Collections.Generic;
using Godot;

internal sealed class EquipmentAttackDefenseAdjustment
{
    private sealed class AcComponentMultiplierEntry
    {
        internal StringName ComponentId { get; init; } = "";
        internal int MultiplierPercent { get; set; } = 100;
    }

    private readonly List<StringName> _ignoredAcComponents = new();
    private readonly List<AcComponentMultiplierEntry> _componentMultipliers = new();

    internal bool LockDodgeBonus { get; private set; }
    internal IReadOnlyList<StringName> IgnoredAcComponents => _ignoredAcComponents;

    internal bool IsEmpty =>
        !LockDodgeBonus && _ignoredAcComponents.Count == 0 && _componentMultipliers.Count == 0;

    internal void AddIgnoredAcComponent(StringName componentId)
    {
        if (componentId == "")
            return;
        if (!_ignoredAcComponents.Contains(componentId))
            _ignoredAcComponents.Add(componentId);
        RemoveComponentMultiplier(componentId);
    }

    internal void AddComponentMultiplier(StringName componentId, int multiplierPercent)
    {
        if (componentId == "" || _ignoredAcComponents.Contains(componentId))
            return;
        int clampedPercent = Mathf.Clamp(multiplierPercent, 0, 100);
        AcComponentMultiplierEntry existing = FindComponentMultiplier(componentId);
        if (existing != null)
        {
            existing.MultiplierPercent = Mathf.Min(existing.MultiplierPercent, clampedPercent);
            return;
        }
        _componentMultipliers.Add(
            new AcComponentMultiplierEntry
            {
                ComponentId = componentId,
                MultiplierPercent = clampedPercent,
            }
        );
    }

    internal void AddLockDodgeBonus()
    {
        LockDodgeBonus = true;
    }

    internal bool ShouldIgnoreAcComponent(StringName componentId)
    {
        return componentId != "" && _ignoredAcComponents.Contains(componentId);
    }

    internal int ResolveComponentMultiplierPercent(StringName componentId)
    {
        AcComponentMultiplierEntry entry = FindComponentMultiplier(componentId);
        return entry?.MultiplierPercent ?? 100;
    }

    private AcComponentMultiplierEntry FindComponentMultiplier(StringName componentId)
    {
        if (componentId == "")
            return null;
        foreach (AcComponentMultiplierEntry entry in _componentMultipliers)
            if (entry != null && entry.ComponentId == componentId)
                return entry;
        return null;
    }

    private void RemoveComponentMultiplier(StringName componentId)
    {
        if (componentId == "")
            return;
        for (int i = _componentMultipliers.Count - 1; i >= 0; i--)
            if (_componentMultipliers[i]?.ComponentId == componentId)
                _componentMultipliers.RemoveAt(i);
    }
}

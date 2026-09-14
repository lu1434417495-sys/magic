using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal class BattleGroundEffectCoordService
{
    private WeakReference<IBattleGroundEffectRuntimePort> _runtimeRef;
    private BattleGroundEffectService _owner;

    private IBattleGroundEffectRuntimePort _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<IBattleGroundEffectRuntimePort>(value) : null;
    }

    internal void Setup(
        IBattleGroundEffectRuntimePort runtime,
        BattleGroundEffectService owner
    )
    {
        _runtime = runtime;
        _owner = owner;
    }

    internal int ActiveDependencyCount =>
        (_runtime != null ? 1 : 0) + (_owner != null ? 1 : 0);

    internal void DisposeRuntime()
    {
        _owner = null;
        _runtime = null;
    }

    private static IBattleGroundEffectRuntimePort ResolveWeakRef(WeakReference<IBattleGroundEffectRuntimePort> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out IBattleGroundEffectRuntimePort target))
        {
            return null;
        }
        return target;
    }

    private static readonly StringName Empty = "";

    private IBattleGroundEffectRuntimePort Runtime => _runtime;
    private BattleState State => Runtime?.GetBattleState();
    private BattleGridService GridService => Runtime?.GetGridService();
    private BattleTargetCollectionService TargetCollectionService =>
        Runtime?.GetTargetCollectionService();
    private BattleSkillResolutionRules SkillResolutionRules => Runtime?.GetSkillResolutionRules();


    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitState activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        var normalizedTargetCoords = new List<Vector2I>(
            targetCoords ?? System.Array.Empty<Vector2I>()
        );
        if (
            castVariantDefinition?.Square2Corner is CombatCastSquare2CornerKind corner
            && normalizedTargetCoords.Count == 1
        )
        {
            IReadOnlyList<Vector2I> expanded = ExpandSquare2Corner(
                normalizedTargetCoords[0],
                corner
            );
            var valid = new List<Vector2I>(expanded.Count);
            foreach (Vector2I coord in expanded)
            {
                if (State != null && GridService != null && GridService.IsInside(State, coord))
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return SortCoordsTyped(valid);
            }
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (State == null || skillDefinition == null || combatProfile == null)
        {
            return SortCoordsTyped(normalizedTargetCoords);
        }
        int skillLevel = _owner._get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        BattleTargetCollectionResult collectedTargetCoords =
            TargetCollectionService.CollectCombatProfileTargetCoords(
                State,
                GridService,
                sourceCoord,
                combatProfile,
                normalizedTargetCoords,
                activeUnit,
                System.Array.Empty<BattleUnitState>(),
                skillLevel
            );
        if (collectedTargetCoords.Handled)
        {
            return SortCoordsTyped(collectedTargetCoords.TargetCoords);
        }
        return SortCoordsTyped(normalizedTargetCoords);
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        var normalizedTargetCoords = new List<Vector2I>(
            targetCoords ?? System.Array.Empty<Vector2I>()
        );
        if (
            castVariantDefinition?.Square2Corner is CombatCastSquare2CornerKind corner
            && normalizedTargetCoords.Count == 1
        )
        {
            IReadOnlyList<Vector2I> expanded = ExpandSquare2Corner(
                normalizedTargetCoords[0],
                corner
            );
            var valid = new List<Vector2I>(expanded.Count);
            foreach (Vector2I coord in expanded)
            {
                if (State != null && GridService != null && GridService.IsInside(State, coord))
                {
                    valid.Add(coord);
                }
            }
            if (valid.Count > 0)
            {
                return SortCoordsTyped(valid);
            }
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (State == null || skillDefinition == null || combatProfile == null)
        {
            return SortCoordsTyped(normalizedTargetCoords);
        }
        int skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        BattleTargetCollectionResult collectedTargetCoords =
            TargetCollectionService.CollectCombatProfileTargetCoords(
                State,
                GridService,
                sourceCoord,
                combatProfile,
                normalizedTargetCoords,
                activeUnit,
                System.Array.Empty<BattleUnitReadView>(),
                skillLevel
            );
        if (collectedTargetCoords.Handled)
        {
            return SortCoordsTyped(collectedTargetCoords.TargetCoords);
        }
        return SortCoordsTyped(normalizedTargetCoords);
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    )
    {
        return SkillResolutionRules?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    )
    {
        return SkillResolutionRules?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    )
    {
        return SkillResolutionRules?.CollectGroundTerrainEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    )
    {
        return SkillResolutionRules?.CollectGroundTerrainEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> plan =
            BuildGroundEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                effectDefinitions,
                effectCoords
            );
        var targetUnitIds = new List<StringName>();
        foreach (
            BattleUnitState targetUnit in BattleSkillExecutionOrchestrator.CollectPlannedTargets(
                effectDefinitions,
                plan
            )
        )
        {
            if (targetUnit != null)
            {
                targetUnitIds.Add(targetUnit.unit_id);
            }
        }
        return targetUnitIds;
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>> plan =
            BuildGroundEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                effectDefinitions,
                effectCoords
            );
        var targetUnitIds = new List<StringName>();
        foreach (
            BattleUnitReadView targetUnit in BattleSkillExecutionOrchestrator.CollectPlannedTargets(
                effectDefinitions,
                plan
            )
        )
        {
            if (targetUnit.IsValid)
            {
                targetUnitIds.Add(targetUnit.UnitId);
            }
        }
        return targetUnitIds;
    }

    internal IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>
        BuildGroundEffectTargetPlan(
            BattleUnitState sourceUnit,
            SkillDefinition skillDefinition,
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            IReadOnlyList<Vector2I> effectCoords
        )
    {
        IReadOnlyList<BattleUnitState> candidateUnits = CollectUnitsInCoords(effectCoords);
        return Runtime?.BuildUnitEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                effectDefinitions,
                candidateUnits
            )
            ?? new Dictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>();
    }

    internal IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>>
        BuildGroundEffectTargetPlan(
            BattleUnitReadView sourceUnit,
            SkillDefinition skillDefinition,
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            IReadOnlyList<Vector2I> effectCoords
        )
    {
        var candidateUnits = new List<BattleUnitReadView>();
        foreach (BattleUnitState candidateUnit in CollectUnitsInCoords(effectCoords))
        {
            candidateUnits.Add(new BattleUnitReadView(candidateUnit));
        }
        return Runtime?.BuildUnitEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                effectDefinitions,
                candidateUnits
            )
            ?? new Dictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>>();
    }

    internal List<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> effectCoords)
    {
        return _runtime == null
            ? new List<BattleUnitState>()
            : new List<BattleUnitState>(Runtime.CollectUnitsInCoords(effectCoords));
    }

    internal static HashSet<int> BuildEffectInstanceIdSet(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        var result = new HashSet<int>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition != null)
            {
                result.Add(RuntimeHelpers.GetHashCode(effectDefinition));
            }
        }
        return result;
    }

    private static IReadOnlyList<Vector2I> ExpandSquare2Corner(
        Vector2I center,
        CombatCastSquare2CornerKind corner
    )
    {
        var expanded = new List<Vector2I>(4);
        if (corner == CombatCastSquare2CornerKind.TopLeft)
        {
            expanded.Add(center);
            expanded.Add(new Vector2I(center.X + 1, center.Y));
            expanded.Add(new Vector2I(center.X, center.Y + 1));
            expanded.Add(new Vector2I(center.X + 1, center.Y + 1));
        }
        else if (corner == CombatCastSquare2CornerKind.TopRight)
        {
            expanded.Add(new Vector2I(center.X - 1, center.Y));
            expanded.Add(center);
            expanded.Add(new Vector2I(center.X - 1, center.Y + 1));
            expanded.Add(new Vector2I(center.X, center.Y + 1));
        }
        else if (corner == CombatCastSquare2CornerKind.BottomLeft)
        {
            expanded.Add(new Vector2I(center.X, center.Y - 1));
            expanded.Add(new Vector2I(center.X + 1, center.Y - 1));
            expanded.Add(center);
            expanded.Add(new Vector2I(center.X + 1, center.Y));
        }
        else if (corner == CombatCastSquare2CornerKind.BottomRight)
        {
            expanded.Add(new Vector2I(center.X - 1, center.Y - 1));
            expanded.Add(new Vector2I(center.X, center.Y - 1));
            expanded.Add(new Vector2I(center.X - 1, center.Y));
            expanded.Add(center);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(corner));
        }
        return expanded;
    }

    internal static IReadOnlyList<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>(values ?? System.Array.Empty<Vector2I>());
        result.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return result;
    }
}

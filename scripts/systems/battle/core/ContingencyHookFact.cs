using System;
using System.Collections.Generic;
using Godot;

internal sealed class ContingencyHookFact
{
    private static readonly Vector2I MissingCell = new(-1, -1);
    private IReadOnlyList<StringName> _affectedUnitIds = Array.Empty<StringName>();
    private IReadOnlyList<StringName> _statusIds = Array.Empty<StringName>();
    private IReadOnlyList<Vector2I> _areaCells = Array.Empty<Vector2I>();

    internal StringName TriggerType { get; init; } = "";
    internal StringName Timing { get; init; } = "";
    internal StringName SourceEventId { get; init; } = "";
    internal StringName SourceUnitId { get; init; } = "";
    internal StringName TargetUnitId { get; init; } = "";
    internal Vector2I SourceCell { get; init; } = MissingCell;
    internal Vector2I PreviousSourceCell { get; init; } = MissingCell;
    internal Vector2I TargetCell { get; init; } = MissingCell;
    internal Vector2I TriggerCell { get; init; } = MissingCell;
    internal int PreviousHp { get; init; } = -1;
    internal int CurrentHp { get; init; } = -1;
    internal int MaxHp { get; init; } = -1;
    internal BattleEffectOrigin Origin { get; init; } = BattleEffectOrigin.PlayerCommand();

    internal IReadOnlyList<StringName> AffectedUnitIds
    {
        get => _affectedUnitIds;
        init => _affectedUnitIds = CopyStringNames(value);
    }

    internal IReadOnlyList<StringName> StatusIds
    {
        get => _statusIds;
        init => _statusIds = CopyStringNames(value);
    }

    internal IReadOnlyList<Vector2I> AreaCells
    {
        get => _areaCells;
        init => _areaCells = CopyCells(value);
    }

    internal bool CanTriggerContingencies => Origin?.CanTriggerContingencies != false;

    internal static ContingencyHookFact HpChanged(
        StringName sourceEventId,
        StringName sourceUnitId,
        StringName targetUnitId,
        int previousHp,
        int currentHp,
        int maxHp,
        BattleEffectOrigin origin,
        Vector2I? sourceCell = null,
        Vector2I? targetCell = null
    ) =>
        new()
        {
            TriggerType = "hp_below_percent",
            Timing = "after_hp_changed",
            SourceEventId = Normalize(sourceEventId),
            SourceUnitId = Normalize(sourceUnitId),
            TargetUnitId = Normalize(targetUnitId),
            PreviousHp = previousHp,
            CurrentHp = currentHp,
            MaxHp = maxHp,
            SourceCell = NormalizeCell(sourceCell),
            TargetCell = NormalizeCell(targetCell),
            TriggerCell = NormalizeCell(targetCell),
            Origin = origin ?? BattleEffectOrigin.PlayerCommand(),
        };

    internal static ContingencyHookFact StatusApplied(
        StringName sourceEventId,
        StringName sourceUnitId,
        StringName targetUnitId,
        IReadOnlyList<StringName> statusIds,
        BattleEffectOrigin origin,
        Vector2I? sourceCell = null,
        Vector2I? targetCell = null
    ) =>
        new()
        {
            TriggerType = "status_applied",
            Timing = "after_status_applied",
            SourceEventId = Normalize(sourceEventId),
            SourceUnitId = Normalize(sourceUnitId),
            TargetUnitId = Normalize(targetUnitId),
            StatusIds = statusIds,
            SourceCell = NormalizeCell(sourceCell),
            TargetCell = NormalizeCell(targetCell),
            TriggerCell = NormalizeCell(targetCell),
            Origin = origin ?? BattleEffectOrigin.PlayerCommand(),
        };

    internal static ContingencyHookFact PositionChanged(
        StringName sourceEventId,
        StringName sourceUnitId,
        Vector2I previousSourceCell,
        Vector2I sourceCell,
        BattleEffectOrigin origin
    ) =>
        new()
        {
            TriggerType = "enemy_enter_radius",
            Timing = "after_position_changed",
            SourceEventId = Normalize(sourceEventId),
            SourceUnitId = Normalize(sourceUnitId),
            PreviousSourceCell = NormalizeCell(previousSourceCell),
            SourceCell = NormalizeCell(sourceCell),
            TriggerCell = NormalizeCell(sourceCell),
            Origin = origin ?? BattleEffectOrigin.PlayerCommand(),
        };

    internal static ContingencyHookFact SpellAffected(
        StringName sourceEventId,
        StringName sourceUnitId,
        StringName targetUnitId,
        IReadOnlyList<StringName> affectedUnitIds,
        BattleEffectOrigin origin,
        Vector2I? sourceCell = null,
        Vector2I? targetCell = null,
        IReadOnlyList<Vector2I> areaCells = null
    ) =>
        new()
        {
            TriggerType = "affected_by_spell",
            Timing = "before_spell_effect_resolved",
            SourceEventId = Normalize(sourceEventId),
            SourceUnitId = Normalize(sourceUnitId),
            TargetUnitId = Normalize(targetUnitId),
            AffectedUnitIds = affectedUnitIds,
            SourceCell = NormalizeCell(sourceCell),
            TargetCell = NormalizeCell(targetCell),
            TriggerCell = NormalizeCell(targetCell),
            AreaCells = areaCells,
            Origin = origin ?? BattleEffectOrigin.PlayerCommand(),
        };

    internal ContingencyFrozenTriggerFacts ToFrozenFacts(
        BattleState state,
        StringName matchedTargetUnitId = default
    )
    {
        StringName targetUnitId = TargetUnitId != ""
            ? TargetUnitId
            : Normalize(matchedTargetUnitId);
        Vector2I sourceCell = ResolveCell(state, SourceUnitId, SourceCell);
        Vector2I targetCell = ResolveCell(state, targetUnitId, TargetCell);
        Vector2I triggerCell = TriggerCell != MissingCell
            ? TriggerCell
            : targetCell != MissingCell
                ? targetCell
                : sourceCell;
        return new ContingencyFrozenTriggerFacts
        {
            TriggerSourceUnitId = SourceUnitId,
            TriggerTargetUnitId = targetUnitId,
            TriggerSourceCell = sourceCell,
            TriggerTargetCell = targetCell,
            TriggerCell = triggerCell,
            CurrentDamageEventAreaCells = AreaCells,
        };
    }

    private static Vector2I ResolveCell(BattleState state, StringName unitId, Vector2I explicitCell)
    {
        if (explicitCell != MissingCell)
            return explicitCell;
        if (state != null && unitId != "" && state.TryGetUnitTyped(unitId, out BattleUnitState unitState))
            return unitState?.GetAnchorCoord() ?? MissingCell;
        return MissingCell;
    }

    private static IReadOnlyList<StringName> CopyStringNames(IReadOnlyList<StringName> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<StringName>();
        StringName[] copy = new StringName[values.Count];
        for (int i = 0; i < values.Count; i++)
            copy[i] = Normalize(values[i]);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<Vector2I> CopyCells(IReadOnlyList<Vector2I> cells)
    {
        if (cells == null || cells.Count == 0)
            return Array.Empty<Vector2I>();
        Vector2I[] copy = new Vector2I[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            copy[i] = cells[i];
        return Array.AsReadOnly(copy);
    }

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);

    private static Vector2I NormalizeCell(Vector2I? value) =>
        value ?? MissingCell;
}

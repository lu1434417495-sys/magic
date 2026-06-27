using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal readonly record struct BattleBarrierOutcomeResult(bool Stopped);

internal sealed class BattleBarrierOutcomeResolver
{
    private readonly record struct BarrierSaveDefaults(
        int SaveDc,
        StringName SaveAbility,
        StringName SaveTag
    )
    {
        public static BarrierSaveDefaults FromOutcome(
            BattleBarrierInstanceState barrier,
            BattleBarrierOutcomeState outcome
        )
        {
            int outcomeSaveDc = outcome?.SaveDc ?? 0;
            int barrierSaveDc = barrier?.SaveDc ?? 0;
            return new BarrierSaveDefaults(
                Mathf.Max(outcomeSaveDc > 0 ? outcomeSaveDc : barrierSaveDc, 1),
                ResolveStringName(outcome?.SaveAbility ?? new StringName(""), "willpower"),
                ResolveStringName(outcome?.SaveTag ?? new StringName(""), "magic")
            );
        }
    }

    private readonly record struct BarrierOutcomeSaveParameters(
        int SaveDc,
        StringName SaveAbility,
        StringName SaveTag,
        BattleSaveContext SaveContext
    )
    {
        public static BarrierOutcomeSaveParameters FromOutcome(
            BattleBarrierInstanceState barrier,
            BattleBarrierLayerState layer,
            BattleBarrierOutcomeState outcome
        )
        {
            BarrierSaveDefaults saveDefaults = BarrierSaveDefaults.FromOutcome(barrier, outcome);
            BattleSaveContext context = BattleSaveContext.Empty;
            if (layer != null && layer.HasSaveRollOverride)
            {
                context = BattleSaveContext.WithSaveRollOverride(layer.SaveRollOverride);
            }
            return new BarrierOutcomeSaveParameters(
                saveDefaults.SaveDc,
                saveDefaults.SaveAbility,
                saveDefaults.SaveTag,
                context
            );
        }
    }

    private readonly record struct BarrierStatusRuntimeParameters(
        StringName StatusId,
        StringName SourceUnitId,
        StringName SourceProfileId,
        StringName SourceLayerId,
        int SaveDc,
        StringName SaveAbility,
        StringName SaveTag
    )
    {
        public static BarrierStatusRuntimeParameters FromOutcome(
            BattleBarrierInstanceState barrier,
            BattleBarrierLayerState layer,
            BattleBarrierOutcomeState outcome,
            StringName statusId
        )
        {
            BarrierSaveDefaults saveDefaults = BarrierSaveDefaults.FromOutcome(barrier, outcome);
            return new BarrierStatusRuntimeParameters(
                statusId,
                barrier?.SourceUnitId ?? new StringName(""),
                barrier?.ProfileId ?? new StringName(""),
                layer?.LayerId ?? new StringName(""),
                saveDefaults.SaveDc,
                saveDefaults.SaveAbility,
                saveDefaults.SaveTag
            );
        }
    }

    private const int DEFAULT_FATAL_DAMAGE = 99999;
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private bool _disposed;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    internal void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _runtimeRef = null;
    }

    internal BattleBarrierPassageResult ApplyPassageOutcomesResult(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleEventBatch batch
    )
    {
        if (
            unitState == null
            || barrier == null
            || barrier.IsEmpty
            || layer == null
            || IsLayerEmpty(layer)
        )
            return new BattleBarrierPassageResult(false, false);

        bool applied = false;
        foreach (BattleBarrierOutcomeState outcome in layer.GetPassageOutcomesTyped())
        {
            if (outcome == null || outcome.IsEmpty)
                continue;
            var outcomeResult = _ApplyOutcome(unitState, barrier, layer, outcome, batch);
            applied = true;
            if (outcomeResult.Stopped || !unitState.is_alive)
            {
                return new BattleBarrierPassageResult(applied, true);
            }
        }
        return new BattleBarrierPassageResult(applied, false);
    }

    private BattleBarrierOutcomeResult _ApplyOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        switch (outcome?.OutcomeKind ?? BarrierOutcomeKind.None)
        {
            case BarrierOutcomeKind.Damage:
                return _ApplyDamageOutcome(unitState, barrier, layer, outcome, batch);
            case BarrierOutcomeKind.PoisonDeath:
                return _ApplyPoisonDeathOutcome(unitState, barrier, layer, outcome, batch);
            case BarrierOutcomeKind.Status:
                return _ApplyStatusOutcome(unitState, barrier, layer, outcome, batch);
            case BarrierOutcomeKind.Banish:
                return _ApplyBanishOutcome(unitState, barrier, layer, outcome, batch);
            default:
                return new BattleBarrierOutcomeResult(false);
        }
    }

    private BattleBarrierOutcomeResult _ApplyDamageOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var amount = Mathf.Max(outcome?.Amount ?? 0, 0);
        if (amount <= 0)
            return new BattleBarrierOutcomeResult(false);
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        var finalAmount = amount;
        if (saveResult.Success && outcome.HalfOnSuccess)
            finalAmount = Mathf.Max((int)Mathf.Ceil(amount / 2.0f), 1);
        var damageTag = ResolveDamageTag(outcome.DamageTag, "force");
        int damage = _ApplyDirectDamage(unitState, barrier, finalAmount, damageTag);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 触碰 {_GetLayerLabel(layer)}，受到 {damage} 点伤害。"
        );
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new BattleBarrierOutcomeResult(true);
        }
        return new BattleBarrierOutcomeResult(false);
    }

    private BattleBarrierOutcomeResult _ApplyPoisonDeathOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.Success)
        {
            var successAmount = Mathf.Max(outcome?.SuccessAmount ?? 0, 0);
            if (successAmount <= 0)
                return new BattleBarrierOutcomeResult(false);
            var damageTag = ResolveDamageTag(
                outcome.SuccessDamageTag != "" ? outcome.SuccessDamageTag : outcome.DamageTag,
                "poison"
            );
            int damage = _ApplyDirectDamage(unitState, barrier, successAmount, damageTag);
            _AppendChangedUnit(batch, unitState);
            _AppendLog(
                batch,
                $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免，仍受到 {damage} 点伤害。"
            );
            if (!unitState.is_alive)
            {
                _HandleDefeatedByBarrier(unitState, barrier, batch);
                return new BattleBarrierOutcomeResult(true);
            }
            return new BattleBarrierOutcomeResult(false);
        }
        var fatalDamage = Mathf.Max(
            unitState.current_hp
                + unitState.current_shield_hp
                + Mathf.Max(outcome?.FatalDamage ?? DEFAULT_FATAL_DAMAGE, 1),
            Mathf.Max(outcome?.FatalDamage ?? DEFAULT_FATAL_DAMAGE, 1)
        );
        int deathDamage = _ApplyDirectDamage(unitState, barrier, fatalDamage, "poison");
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，触发即死效果。"
        );
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new BattleBarrierOutcomeResult(true);
        }
        if (deathDamage > 0)
            _AppendLog(batch, $"{unitState.display_name} 的免死效果抵消了即死。");
        return new BattleBarrierOutcomeResult(false);
    }

    private BattleBarrierOutcomeResult _ApplyStatusOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        StringName statusId = outcome?.StatusId ?? new StringName("");
        if (statusId == "")
            return new BattleBarrierOutcomeResult(false);
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.Success)
        {
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免。");
            return new BattleBarrierOutcomeResult(false);
        }
        _ApplyBarrierStatus(unitState, barrier, layer, outcome, statusId);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，获得状态 {statusId}。"
        );
        return new BattleBarrierOutcomeResult(true);
    }

    private BattleBarrierOutcomeResult _ApplyBanishOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.Success)
        {
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免。");
            return new BattleBarrierOutcomeResult(false);
        }
        if (_IsSummonedUnit(unitState))
        {
            _RemoveSummonedUnit(unitState, barrier, layer, batch);
            return new BattleBarrierOutcomeResult(true);
        }
        var destination = _FindBanishTeleportCoord(unitState, barrier);
        if (destination == new Vector2I(-1, -1))
        {
            _AppendLog(
                batch,
                $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 放逐，但没有找到可传送落点。"
            );
            return new BattleBarrierOutcomeResult(true);
        }
        var previousCoords = new List<Vector2I>();
        foreach (Vector2I coord in unitState.occupied_coords)
            previousCoords.Add(coord);
        var runtime = _ResolveRuntime();
        var state = runtime._state;
        runtime._grid_service.ClearUnitOccupancy(state, unitState);
        unitState.SetAnchorCoord(destination);
        runtime._grid_service.SetOccupantsTyped(state, unitState.occupied_coords, unitState.unit_id);
        _AppendChangedCoords(batch, previousCoords);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 随机传送到 ({destination.X}, {destination.Y})。"
        );
        return new BattleBarrierOutcomeResult(true);
    }

    private BattleSaveResult _ResolveOutcomeSave(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome
    )
    {
        BarrierOutcomeSaveParameters saveParams = BarrierOutcomeSaveParameters.FromOutcome(
            barrier,
            layer,
            outcome
        );
        CombatEffectDefinition effect = BattleRuntimeEffectDefinitions.StaticSave(
            saveParams.SaveDc,
            saveParams.SaveAbility,
            saveParams.SaveTag
        );
        return BattleSaveResolver.ResolveSaveResult(
            _GetBarrierSourceUnit(barrier),
            unitState,
            effect,
            saveParams.SaveContext
        );
    }

    private void _ApplyBarrierStatus(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        StringName statusId
    )
    {
        BarrierStatusRuntimeParameters statusParams = BarrierStatusRuntimeParameters.FromOutcome(
            barrier,
            layer,
            outcome,
            statusId
        );
        _ResolveRuntime()
            ._set_runtime_barrier_status_effect(
                unitState,
                statusParams.StatusId,
                statusParams.SourceUnitId,
                statusParams.SourceProfileId,
                statusParams.SourceLayerId,
                statusParams.SaveDc,
                statusParams.SaveAbility,
                statusParams.SaveTag
            );
    }

    private int _ApplyDirectDamage(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        int damageAmount,
        StringName damageTag
    )
    {
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        int normalizedDamage = Mathf.Max(damageAmount, 0);
        int damage = _ResolveRuntime()
            ._damage_resolver
            .ApplyDirectDamageToTargetTyped(unitState, normalizedDamage, sourceUnit);
        unitState.SetCurrentHp(unitState.current_hp);
        return damage;
    }

    private void _HandleDefeatedByBarrier(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleEventBatch batch
    )
    {
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        _ResolveRuntime()
            .HandleUnitDefeatedByRuntimeEffect(
                unitState,
                sourceUnit,
                batch,
                $"{unitState.display_name} 被 {_GetBarrierLabel(barrier)} 击倒。",
                new BattleDefeatHandlingOptions()
            );
    }

    private void _RemoveSummonedUnit(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleEventBatch batch
    )
    {
        _ResolveRuntime()
            .RemoveSummonedUnitFromBattle(
                unitState,
                batch,
                $"{unitState.display_name} 是召唤物，被 {_GetLayerLabel(layer)} 直接放逐消失。"
            );
    }

    private Vector2I _FindBanishTeleportCoord(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return new Vector2I(-1, -1);
        var state = runtime._state;
        var gridService = runtime._grid_service;
        if (state == null || gridService == null || unitState == null)
            return new Vector2I(-1, -1);
        var candidates = new List<Vector2I>();
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            Vector2I coord = cellEntry.Coord;
            if (_IsCoordInsideBarrier(coord, barrier))
                continue;
            if (
                !gridService.CanPlaceFootprint(
                        state,
                        coord,
                        unitState.footprint_size,
                        unitState.unit_id,
                        unitState
                    )
            )
                continue;
            candidates.Add(coord);
        }
        if (candidates.Count == 0)
            return new Vector2I(-1, -1);
        var index = TrueRandomSeedService.RandiRange(0, candidates.Count - 1);
        return candidates[index];
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
        {
            return false;
        }
        var anchor = barrier.AnchorCoord;
        var radius = Mathf.Max(barrier.RadiusCells, 0);
        var pattern = BattleTypedNames.ToAreaPattern(barrier.AreaPattern);
        var dx = Mathf.Abs(coord.X - anchor.X);
        var dy = Mathf.Abs(coord.Y - anchor.Y);
        switch (pattern)
        {
            case BattleAreaPattern.Square:
            case BattleAreaPattern.Radius:
                return Mathf.Max(dx, dy) <= radius;
            default:
                return dx + dy <= radius;
        }
    }

    private bool _IsSummonedUnit(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        if (unitState.HasStatusEffect("summoned"))
            return true;
        if (unitState.ai_blackboard?.summoned == true)
            return true;
        if (unitState.ai_blackboard?.temporary_unit == true)
            return true;
        return unitState.ai_blackboard?.summon_source_unit_id != "";
    }

    private BattleUnitState _GetBarrierSourceUnit(BattleBarrierInstanceState barrier)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return null;
        var state = runtime._state;
        if (state == null)
            return null;
        var sourceUnitId = barrier?.SourceUnitId ?? new StringName("");
        if (sourceUnitId == "")
            return null;
        return state.TryGetUnitTyped(sourceUnitId, out BattleUnitState sourceUnit)
            ? sourceUnit
            : null;
    }

    private string _GetLayerLabel(BattleBarrierLayerState layer)
    {
        if (layer == null)
            return "屏障层";
        if (!string.IsNullOrEmpty(layer.DisplayName))
            return layer.DisplayName;
        string layerId = layer.LayerId.ToString();
        return !string.IsNullOrEmpty(layerId) ? layerId : "屏障层";
    }

    private string _GetBarrierLabel(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
            return "屏障";
        if (!string.IsNullOrEmpty(barrier.DisplayName))
            return barrier.DisplayName;
        string profileId = barrier.ProfileId.ToString();
        return !string.IsNullOrEmpty(profileId) ? profileId : "屏障";
    }

    private static bool IsLayerEmpty(BattleBarrierLayerState layer)
    {
        return layer == null || (layer.LayerId == "" && layer.PassageOutcomes.Count == 0);
    }

    private static StringName ResolveDamageTag(StringName value, StringName fallback)
    {
        return value != "" ? value : fallback;
    }

    private static StringName ResolveStringName(StringName value, StringName fallback)
    {
        return value != "" ? value : fallback;
    }

    private void _AppendChangedUnit(BattleEventBatch batch, BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null || unitState == null)
            return;
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        runtime._append_changed_unit_coords(batch, unitState);
    }

    private void _AppendChangedCoords(BattleEventBatch batch, IEnumerable<Vector2I> coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        runtime._append_changed_coords(batch, ToGodotCoordArray(coords));
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.AddLogLine(line);
    }

    private BattleRuntimeModule _ResolveRuntime()
    {
        if (
            _runtimeRef == null
            || !_runtimeRef.TryGetTarget(out BattleRuntimeModule target)
        )
            return null;
        return target;
    }

    private static GArray ToGodotCoordArray(IEnumerable<Vector2I> coords)
    {
        var result = new GArray();
        foreach (Vector2I coord in coords ?? System.Array.Empty<Vector2I>())
        {
            result.Add(coord);
        }
        return result;
    }
}

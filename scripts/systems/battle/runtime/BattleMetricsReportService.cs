using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleMetricsReportService : BattleRuntimeModuleBorrower
{
    private readonly Stack<BattleEffectOrigin> _effectOriginStack = new();

    internal override void DisposeRuntime()
    {
        base.DisposeRuntime();
        _effectOriginStack.Clear();
    }

    internal void AppendResultReportEntry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    )
    {
        if (batch == null)
            return;
        IReadOnlyDictionary<string, object> reportEntry = result.HasReportEntry
            ? BattleReportEntryPayload.BuildPlainPayload(result.ReportEntry)
            : BuildAutoCastEffectResultReport(result);
        if (reportEntry.Count > 0)
            _append_report_entry_to_batch(batch, reportEntry);
    }

    private IReadOnlyDictionary<string, object> BuildAutoCastEffectResultReport(
        AttackEffectResolutionResult result
    )
    {
        BattleEffectOrigin origin = CurrentEffectOrigin;
        if (
            origin?.OriginKind != new StringName("contingency_auto_cast")
            || !result.Applied
        )
            return new Dictionary<string, object>(StringComparer.Ordinal);
        Dictionary<string, object> payload = AttackEffectResolutionPlainPayload.Build(result);
        payload["entry_kind"] = "effect_result";
        return payload;
    }

    internal void _append_report_entry_to_batch(
        BattleEventBatch batch,
        IReadOnlyDictionary<string, object> report_entry
    )
    {
        if (batch == null || report_entry == null || report_entry.Count == 0)
            return;
        var detachedReportEntry = new Dictionary<string, object>(
            report_entry,
            StringComparer.Ordinal
        );
        AttachCurrentEffectOrigin(detachedReportEntry);
        batch.AddReportEntry(detachedReportEntry);
        string entryText =
            detachedReportEntry.TryGetValue("text", out object textValue)
                ? textValue?.ToString()?.StripEdges() ?? ""
                : "";
        if (!string.IsNullOrEmpty(entryText))
            batch.AddLogLine(entryText);
    }

    internal IDisposable PushEffectOrigin(BattleEffectOrigin origin)
    {
        _effectOriginStack.Push(origin ?? BattleEffectOrigin.PlayerCommand());
        return new EffectOriginScope(this);
    }

    internal BattleEffectOrigin CurrentEffectOrigin =>
        _effectOriginStack.Count > 0 ? _effectOriginStack.Peek() : BattleEffectOrigin.PlayerCommand();

    private void AttachCurrentEffectOrigin(Dictionary<string, object> reportEntry)
    {
        if (reportEntry == null || reportEntry.Count == 0)
            return;
        reportEntry["effect_origin"] = CurrentEffectOrigin.ToPlainDictionary();
    }

    private void PopEffectOrigin()
    {
        if (_effectOriginStack.Count > 0)
            _effectOriginStack.Pop();
    }

    private sealed class EffectOriginScope : IDisposable
    {
        private BattleMetricsReportService _service;

        internal EffectOriginScope(BattleMetricsReportService service)
        {
            _service = service;
        }

        public void Dispose()
        {
            _service?.PopEffectOrigin();
            _service = null;
        }
    }

    internal void RecordEnemyDefeatedAchievement(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    ) => _runtime._battle_rating_system.RecordEnemyDefeatedAchievement(active_unit, target_unit);

    internal void RecordSkillEffectResult(
        BattleUnitState source_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _runtime._battle_rating_system.RecordSkillEffectResult(source_unit, damage, healing, kill_count);
        if (source_unit == null)
            return;
        _runtime._metrics_collector.RecordSkillEffectResult(source_unit, damage, healing, kill_count);
    }

    public void RecordBattleContributionResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool causedDefeat,
        StringName originKind,
        StringName skillId
    )
    {
        _runtime._battle_rating_system.RecordContributionFromUnits(
            source_unit,
            target_unit,
            damage,
            healing,
            causedDefeat,
            originKind,
            skillId
        );
    }

    internal void _initialize_battle_metrics()
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.InitializeBattleMetrics();
    }

    internal void RecordTurnStartedMetrics(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordTurnStarted(unit_state);
    }

    internal void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordActionIssued(unit_state, command_type, ap_cost);
    }

    internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordSkillAttempt(unit_state, skill_id);
    }

    internal void _record_skill_success(BattleUnitState unit_state, StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordSkillSuccess(unit_state, skill_id);
    }

    internal void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordEffectMetrics(
            source_unit,
            target_unit,
            damage,
            healing,
            kill_count
        );
    }

    internal void _record_unit_defeated(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._metrics_collector.RecordUnitDefeated(unit_state);
    }
}

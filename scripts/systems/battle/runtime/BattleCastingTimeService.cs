using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleCastingTimeService
{
    private const int CastProgressPerTu = 100;
    private const int MinorHpLossThreshold = 3;
    private const int MajorHpLossThreshold = 15;
    private const int MediumMaintenanceDc = 12;
    private const int HeavyMaintenanceDc = 15;
    private static readonly StringName AttackResolutionMiss = "miss";
    private static readonly StringName SpellControlResolutionOrdinaryMiss = "ordinary_miss";
    private static readonly StringName WillpowerModifier = "willpower_modifier";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    internal void Dispose()
    {
        _runtimeRef = null;
    }

    internal bool TryHandleCastingSkillStart(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDef skillDef,
        CombatCastVariantDef unitExecutionCastVariant,
        CombatCastVariantDef groundCastVariant,
        bool routesToUnitTargeting,
        BattleEventBatch batch
    )
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (runtime == null || activeUnit == null || command == null || combatProfile == null)
        {
            return false;
        }

        int skillLevel = runtime._get_unit_skill_level(activeUnit, skillDef.skill_id);
        int castingTimeTu = combatProfile.GetEffectiveCastingTimeTu(skillLevel);
        if (castingTimeTu <= 0)
        {
            return false;
        }

        if (activeUnit.HasPendingCast())
        {
            batch?.AddLogLine($"{DisplayName(activeUnit)} 正在读条，无法开始新的施法。");
            return true;
        }

        if (BattleTemporalStatusService.HasTemporalCastBlock(activeUnit))
        {
            batch?.AddLogLine($"{DisplayName(activeUnit)} 处于时间静滞，无法开始读条。");
            return true;
        }

        PendingCastStartPayload payload = BuildStartPayload(
            runtime,
            activeUnit,
            command,
            skillDef,
            unitExecutionCastVariant,
            groundCastVariant,
            routesToUnitTargeting
        );
        if (!payload.Allowed)
        {
            batch?.AddLogLine(payload.Message);
            return true;
        }

        SkillCostTransaction transaction =
            runtime._skill_turn_resolver.BuildSkillCostTransaction(activeUnit, skillDef);
        BattleSpellControlMetadata spellControlMetadata = ResolveCastingSpellControl(
            runtime,
            activeUnit,
            skillDef,
            combatProfile,
            skillLevel
        );
        int spellControlDc = combatProfile.GetEffectiveCastingSpellControlDc(skillLevel);
        spellControlMetadata = ApplyCastingSpellControlDc(spellControlMetadata, spellControlDc);
        if (spellControlMetadata.CriticalFail)
        {
            runtime._skill_turn_resolver.ConsumeCastingFailureApCost(
                activeUnit,
                transaction,
                batch,
                criticalFailure: true
            );
            runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(activeUnit);
            runtime._skill_turn_resolver.StartSkillCooldownFromTransaction(
                activeUnit,
                transaction,
                batch
            );
            runtime._record_skill_attempt(activeUnit, skillDef.skill_id);
            batch?.AddLogLine(
                $"{DisplayName(activeUnit)} 准备 {SkillLabel(skillDef)} 时法术控制大失败，读条中断。"
            );
            return true;
        }
        if (spellControlMetadata.OrdinaryMiss)
        {
            runtime._skill_turn_resolver.ConsumeCastingFailureApCost(activeUnit, transaction, batch);
            runtime._record_skill_attempt(activeUnit, skillDef.skill_id);
            batch?.AddLogLine(
                $"{DisplayName(activeUnit)} 准备 {SkillLabel(skillDef)} 时法术控制失败，本次行动只能移动、等待或取消读条。"
            );
            return true;
        }

        if (
            !runtime._skill_turn_resolver.ConsumePendingCastResourceCosts(
                activeUnit,
                skillDef,
                payload.CastVariant,
                batch,
                out transaction
            )
        )
        {
            return true;
        }

        BattlePendingCastState pendingCast = new()
        {
            SourceUnitId = activeUnit.unit_id,
            SkillId = skillDef.skill_id,
            VariantId = payload.CastVariant?.variant_id ?? "",
            TargetMode = payload.TargetMode,
            BindingMode = combatProfile.GetEffectivePendingCastBindingMode(skillLevel),
            StartedCoord = activeUnit.coord,
            StartedTu = runtime._state?.timeline?.current_tu ?? 0,
            BaseCastingTimeTu = castingTimeTu,
            RemainingCastProgress = castingTimeTu * CastProgressPerTu,
            LastMaintenanceCheckpointHp = activeUnit.current_hp,
            CastSequence = runtime._state?.AllocateCastSequence() ?? 0,
            CostTransaction = transaction,
            SpellControlMetadata = spellControlMetadata,
        };
        pendingCast.SetTargetUnitIds(payload.TargetUnitIds);
        pendingCast.SetTargetCoords(payload.TargetCoords);
        activeUnit.SetPendingCast(pendingCast);
        activeUnit.SetCurrentAp(0);
        runtime._record_skill_attempt(activeUnit, skillDef.skill_id);
        runtime._record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            0
        );
        runtime._append_changed_unit_id(batch, activeUnit.unit_id);
        batch?.AddLogLine(
            $"{DisplayName(activeUnit)} 开始准备 {SkillLabel(skillDef)}（{castingTimeTu} TU）。"
        );
        return true;
    }

    internal void PreviewCancelCast(BattleCommand command, BattlePreview preview)
    {
        if (preview == null)
        {
            return;
        }
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleUnitState unitState = FindCommandUnit(runtime, command);
        if (!CanManualCancel(unitState, runtime?._state, out string message))
        {
            preview.AddLogLine(message);
            return;
        }
        preview.allowed = true;
        preview.AddLogLine($"{DisplayName(unitState)} 可以取消当前读条。");
    }

    internal void HandleCancelCast(BattleCommand command, BattleEventBatch batch)
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleUnitState unitState = FindCommandUnit(runtime, command);
        if (!CanManualCancel(unitState, runtime?._state, out string message))
        {
            batch?.AddLogLine(message);
            return;
        }
        BattlePendingCastState pendingCast = unitState.ClearPendingCast();
        runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(unitState);
        runtime._skill_turn_resolver.RefundSkillCostTransaction(
            unitState,
            pendingCast?.CostTransaction,
            PendingCastRefundPolicy.HalfResources,
            batch
        );
        batch?.AddReportEntry(
            new GDictionary
            {
                ["type"] = "pending_cast_cancel",
                ["entry_type"] = "pending_cast_cancel",
                ["reason_id"] = "manual_cancel",
                ["event_tags"] = new Godot.Collections.Array { "pending_cast", "cancel" },
                ["unit_id"] = unitState.unit_id.ToString(),
                ["skill_id"] = (pendingCast?.SkillId ?? new StringName("")).ToString(),
                ["refund_policy"] = "half_persistent_costs",
                ["text"] = $"{DisplayName(unitState)} 取消了 {SkillLabel(runtime, pendingCast)} 的读条。",
            }
        );
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        batch?.AddLogLine($"{DisplayName(unitState)} 取消了 {SkillLabel(runtime, pendingCast)} 的读条。");
    }

    internal void ReconcilePendingCasts(BattleEventBatch batch)
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleState state = runtime?._state;
        if (runtime == null || state == null)
        {
            return;
        }
        foreach (BattleState.BattleUnitEntry entry in state.GetUnitEntriesTyped())
        {
            BattleUnitState unitState = entry.Unit;
            BattlePendingCastState pendingCast = unitState?.pending_cast;
            if (pendingCast == null)
            {
                continue;
            }
            if (!unitState.is_alive)
            {
                ClearPendingCastNoCooldown(runtime, unitState, batch, "施法者已倒下，读条中断。");
                continue;
            }
            if (unitState.coord != pendingCast.StartedCoord)
            {
                InterruptPendingCastWithCooldown(
                    runtime,
                    unitState,
                    pendingCast,
                    batch,
                    "施法者位置变化，读条中断。"
                );
                continue;
            }
            if (runtime._skill_turn_resolver.IsCastInterruptedByStatus(unitState))
            {
                InterruptPendingCastWithCooldown(
                    runtime,
                    unitState,
                    pendingCast,
                    batch,
                    "施法者受到强控制，读条中断。"
                );
                continue;
            }
            if (ShouldInterruptForHpLoss(runtime, unitState, pendingCast, batch))
            {
                InterruptPendingCastWithCooldown(
                    runtime,
                    unitState,
                    pendingCast,
                    batch,
                    "施法者维持检定失败，读条中断。"
                );
                continue;
            }
            if (ShouldInterruptForTargetBinding(runtime, unitState, pendingCast, batch))
            {
                InterruptPendingCastWithCooldown(
                    runtime,
                    unitState,
                    pendingCast,
                    batch,
                    "绑定目标失效，读条中断。"
                );
            }
        }
    }

    internal void AdvancePendingCasts(
        int elapsedTu,
        BattleEventBatch batch,
        ISet<StringName> stasisFrozenUnitIds = null
    )
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleState state = runtime?._state;
        if (runtime == null || state == null || elapsedTu <= 0)
        {
            return;
        }
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            BattlePendingCastState pendingCast = unitState?.pending_cast;
            if (pendingCast == null)
            {
                continue;
            }
            // step 开始时处于 time_stasis 的施法者本 step 全程冻结，
            // 静滞同 step 自然到期也不在到期当 step 推进读条。
            if (stasisFrozenUnitIds != null && stasisFrozenUnitIds.Contains(unitState.unit_id))
            {
                continue;
            }
            // temporal 状态推导读条进度获取率：time_stasis 冻结，time_slow 带余数累加。
            int progressDelta = BattleTemporalStatusService.ConsumeCastProgressGain(
                unitState,
                elapsedTu * CastProgressPerTu
            );
            if (progressDelta <= 0)
            {
                continue;
            }
            pendingCast.RemainingCastProgress = Math.Max(
                pendingCast.RemainingCastProgress - progressDelta,
                0
            );
            runtime._append_changed_unit_id(batch, unitState.unit_id);
        }
    }

    internal void CompleteReadyPendingCasts(BattleEventBatch batch)
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleState state = runtime?._state;
        if (runtime == null || state == null)
        {
            return;
        }
        List<(ulong sequence, StringName unitId)> readyCasts = new();
        foreach (BattleState.BattleUnitEntry entry in state.GetUnitEntriesTyped())
        {
            BattlePendingCastState pendingCast = entry.Unit?.pending_cast;
            if (pendingCast?.IsComplete == true)
            {
                readyCasts.Add((pendingCast.CastSequence, entry.UnitId));
            }
        }
        readyCasts.Sort((a, b) => a.sequence.CompareTo(b.sequence));
        foreach ((_, StringName unitId) in readyCasts)
        {
            if (!state.TryGetUnitTyped(unitId, out BattleUnitState unitState))
            {
                continue;
            }
            BattlePendingCastState pendingCast = unitState.pending_cast;
            if (pendingCast?.IsComplete != true)
            {
                continue;
            }
            if (BattleTemporalStatusService.HasTemporalCastBlock(unitState))
            {
                // 静滞冻结读条：不中断也不结算，保持 pending 等待解除。
                continue;
            }
            runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(unitState);
            unitState.ClearPendingCast();
            bool applied = runtime._skill_orchestrator.ResolvePendingCast(
                unitState,
                pendingCast,
                batch
            );
            runtime._skill_turn_resolver.StartSkillCooldownFromTransaction(
                unitState,
                pendingCast.CostTransaction,
                batch
            );
            runtime._append_changed_unit_id(batch, unitState.unit_id);
            if (applied)
            {
                batch?.AddLogLine($"{DisplayName(unitState)} 完成了 {SkillLabel(runtime, pendingCast)}。");
            }
            ReconcilePendingCasts(batch);
        }
    }

    private PendingCastStartPayload BuildStartPayload(
        BattleRuntimeModule runtime,
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDef skillDef,
        CombatCastVariantDef unitExecutionCastVariant,
        CombatCastVariantDef groundCastVariant,
        bool routesToUnitTargeting
    )
    {
        if (routesToUnitTargeting || groundCastVariant == null)
        {
            BattleUnitSkillValidationResult validation =
                runtime._skill_orchestrator._validate_unit_skill_targets_result(
                    activeUnit,
                    command,
                    skillDef,
                    unitExecutionCastVariant
                );
            if (!validation.Allowed)
            {
                return PendingCastStartPayload.Denied(validation.Message);
            }
            return PendingCastStartPayload.AllowedUnit(
                unitExecutionCastVariant,
                validation.TargetUnitIds
            );
        }

        BattleGroundSkillValidationResult groundValidation =
            runtime._skill_orchestrator.ValidateGroundSkillCommandResult(
                activeUnit,
                skillDef,
                groundCastVariant,
                command
            );
        if (!groundValidation.Allowed)
        {
            return PendingCastStartPayload.Denied(groundValidation.Message);
        }
        string precastValidationMessage = runtime._skill_orchestrator.GetGroundSpecialEffectValidationMessage(
            activeUnit,
            skillDef,
            groundCastVariant,
            groundValidation.TargetCoords
        );
        if (!string.IsNullOrEmpty(precastValidationMessage))
        {
            return PendingCastStartPayload.Denied(precastValidationMessage);
        }
        return PendingCastStartPayload.AllowedGround(groundCastVariant, groundValidation.TargetCoords);
    }

    private BattleSpellControlMetadata ResolveCastingSpellControl(
        BattleRuntimeModule runtime,
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        int skillLevel
    )
    {
        if (
            runtime?.GetDamageResolver() == null
            || activeUnit == null
            || skillDef == null
            || combatProfile == null
            || (
                !combatProfile.HasSpellFateControl()
                && combatProfile.GetEffectiveCastingSpellControlDc(skillLevel) <= 0
            )
        )
        {
            return BattleSpellControlMetadata.Empty();
        }
        var controlContext = new GDictionary
        {
            ["battle_state"] = runtime._state,
            ["skill_id"] = skillDef.skill_id,
            ["dispatch_events"] = false,
        };
        return runtime
            .GetDamageResolver()
            .ResolveSpellControlCheckTyped(activeUnit, controlContext);
    }

    internal static BattleSpellControlMetadata ApplyCastingSpellControlDc(
        BattleSpellControlMetadata metadata,
        int dc
    )
    {
        metadata ??= BattleSpellControlMetadata.Empty();
        if (dc <= 0 || !metadata.HasResolutionMetadata || metadata.CriticalFail || metadata.CriticalHit)
        {
            return metadata;
        }
        if (metadata.EffectiveHitRoll >= dc)
        {
            return metadata;
        }
        return metadata with
        {
            AttackResolution = AttackResolutionMiss,
            SpellControlResolution = SpellControlResolutionOrdinaryMiss,
            AttackSuccess = false,
            OrdinaryMiss = true,
        };
    }

    private bool ShouldInterruptForHpLoss(
        BattleRuntimeModule runtime,
        BattleUnitState unitState,
        BattlePendingCastState pendingCast,
        BattleEventBatch batch
    )
    {
        int hpLoss = pendingCast.LastMaintenanceCheckpointHp - unitState.current_hp;
        if (hpLoss <= MinorHpLossThreshold)
        {
            return false;
        }
        SkillDef skillDef = runtime.GetSkillDefTyped(pendingCast.SkillId);
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        int skillLevel = pendingCast.CostTransaction?.SkillLevel ?? 1;
        int dc = combatProfile?.GetEffectiveCastingMaintenanceDc(skillLevel) ?? 0;
        if (dc <= 0)
        {
            dc = hpLoss <= MajorHpLossThreshold ? MediumMaintenanceDc : HeavyMaintenanceDc;
        }
        int roll = TrueRandomSeedService.RandiRange(1, 20) + GetWillpowerModifier(unitState);
        pendingCast.LastMaintenanceCheckpointHp = unitState.current_hp;
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        if (roll >= dc)
        {
            return false;
        }
        batch?.AddLogLine($"{DisplayName(unitState)} 维持读条检定 {roll}/{dc} 失败。");
        return true;
    }

    private bool ShouldInterruptForTargetBinding(
        BattleRuntimeModule runtime,
        BattleUnitState unitState,
        BattlePendingCastState pendingCast,
        BattleEventBatch batch
    )
    {
        if (
            pendingCast.BindingMode == PendingCastBindingModeKind.GroundBind
            || pendingCast.TargetMode == BattleTargetMode.Ground
        )
        {
            return false;
        }
        BattleState state = runtime?._state;
        if (state == null)
        {
            return true;
        }
        List<StringName> invalidTargetIds = new();
        foreach (StringName targetUnitId in pendingCast.TargetUnitIds)
        {
            if (!state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit) || !targetUnit.is_alive)
            {
                invalidTargetIds.Add(targetUnitId);
            }
        }
        if (invalidTargetIds.Count == 0)
        {
            return false;
        }
        if (pendingCast.BindingMode == PendingCastBindingModeKind.HardAnchor)
        {
            return true;
        }
        foreach (StringName invalidTargetId in invalidTargetIds)
        {
            pendingCast.RemoveTargetUnitId(invalidTargetId);
        }
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        return pendingCast.TargetUnitIds.Count == 0;
    }

    private void InterruptPendingCastWithCooldown(
        BattleRuntimeModule runtime,
        BattleUnitState unitState,
        BattlePendingCastState pendingCast,
        BattleEventBatch batch,
        string reason
    )
    {
        unitState.ClearPendingCast();
        runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(unitState);
        runtime._skill_turn_resolver.StartSkillCooldownFromTransaction(
            unitState,
            pendingCast?.CostTransaction,
            batch
        );
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        batch?.AddLogLine(reason);
    }

    private void ClearPendingCastNoCooldown(
        BattleRuntimeModule runtime,
        BattleUnitState unitState,
        BattleEventBatch batch,
        string reason
    )
    {
        unitState.ClearPendingCast();
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        batch?.AddLogLine(reason);
    }

    private BattleRuntimeModule ResolveRuntime()
    {
        if (_runtimeRef != null && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime))
        {
            return runtime;
        }
        return null;
    }

    private static BattleUnitState FindCommandUnit(BattleRuntimeModule runtime, BattleCommand command)
    {
        if (runtime?._state == null || command == null)
        {
            return null;
        }
        runtime._state.TryGetUnitTyped(command.unit_id, out BattleUnitState unitState);
        return unitState;
    }

    private static bool CanManualCancel(
        BattleUnitState unitState,
        BattleState state,
        out string message
    )
    {
        if (unitState == null || state == null)
        {
            message = "读条单位无效。";
            return false;
        }
        if (!unitState.HasPendingCast())
        {
            message = "当前没有可取消的读条。";
            return false;
        }
        if (unitState.ControlModeKind != BattleUnitControlMode.Manual)
        {
            message = "只能取消玩家可控单位的读条。";
            return false;
        }
        if (!state.ally_unit_ids.Contains(unitState.unit_id))
        {
            message = "只能取消己方单位的读条。";
            return false;
        }
        message = "";
        return true;
    }

    private static int GetWillpowerModifier(BattleUnitState unitState)
    {
        return unitState?.attribute_snapshot?.GetValue(WillpowerModifier) ?? 0;
    }

    private static string DisplayName(BattleUnitState unitState)
    {
        if (!string.IsNullOrEmpty(unitState?.display_name))
        {
            return unitState.display_name;
        }
        StringName unitId = unitState?.unit_id ?? "";
        return unitId == "" ? "未知单位" : unitId.ToString();
    }

    private static string SkillLabel(SkillDef skillDef)
    {
        if (!string.IsNullOrEmpty(skillDef?.display_name))
        {
            return skillDef.display_name;
        }
        StringName skillId = skillDef?.skill_id ?? "";
        return skillId == "" ? "技能" : skillId.ToString();
    }

    private static string SkillLabel(BattleRuntimeModule runtime, BattlePendingCastState pendingCast)
    {
        SkillDef skillDef = runtime?.GetSkillDefTyped(pendingCast?.SkillId ?? "");
        return SkillLabel(skillDef);
    }

    private readonly record struct PendingCastStartPayload(
        bool Allowed,
        string Message,
        CombatCastVariantDef CastVariant,
        BattleTargetMode TargetMode,
        IReadOnlyList<StringName> TargetUnitIds,
        IReadOnlyList<Vector2I> TargetCoords
    )
    {
        internal static PendingCastStartPayload Denied(string message) =>
            new(
                false,
                string.IsNullOrEmpty(message) ? "技能或目标无效。" : message,
                null,
                BattleTargetMode.Unknown,
                Array.Empty<StringName>(),
                Array.Empty<Vector2I>()
            );

        internal static PendingCastStartPayload AllowedUnit(
            CombatCastVariantDef castVariant,
            IReadOnlyList<StringName> targetUnitIds
        ) =>
            new(
                true,
                "",
                castVariant,
                BattleTargetMode.Unit,
                targetUnitIds ?? Array.Empty<StringName>(),
                Array.Empty<Vector2I>()
            );

        internal static PendingCastStartPayload AllowedGround(
            CombatCastVariantDef castVariant,
            IReadOnlyList<Vector2I> targetCoords
        ) =>
            new(
                true,
                "",
                castVariant,
                BattleTargetMode.Ground,
                Array.Empty<StringName>(),
                targetCoords ?? Array.Empty<Vector2I>()
            );
    }
}

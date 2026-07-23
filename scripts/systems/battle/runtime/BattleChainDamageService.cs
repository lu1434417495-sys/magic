using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal readonly record struct ChainDamageParameters(
    int BaseRadius,
    StringName BonusTerrainEffectId,
    int WetChainRadius,
    bool PreventRepeatTarget
)
{
    public static ChainDamageParameters FromEffect(CombatEffectDefinition effectDefinition)
    {
        int baseRadius = Math.Max(
            effectDefinition?.GetIntParamTyped("base_chain_radius", 1) ?? 1,
            0
        );
        return new ChainDamageParameters(
            baseRadius,
            effectDefinition?.GetStringNameParamTyped("bonus_terrain_effect_id")
                ?? new StringName(""),
            Math.Max(
                effectDefinition?.GetIntParamTyped("wet_chain_radius", baseRadius) ?? baseRadius,
                baseRadius
            ),
            effectDefinition?.PreventRepeatTarget ?? true
        );
    }
}

internal readonly record struct ChainDamageHop(
    Vector2I OriginCoord,
    BattleUnitState TargetUnit
);


internal sealed class BattleChainDamageService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleSkillPreviewService _skillPreviewService;

    private BattleRuntimeModule _runtime
    {
        get =>
            _runtimeRef != null
            && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime)
                ? runtime
                : null;
        set =>
            _runtimeRef =
                value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleRuntimeModule Runtime => _runtime;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleSkillExecutionOrchestrator owner,
        BattleSkillPreviewService skillPreviewService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _skillPreviewService = skillPreviewService;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _skillPreviewService = null;
    }

    internal void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        AttackEffectResolutionResult primaryResolution,
        BattleEventBatch batch,
        string skill_subject,
        BattleSpellControlResult spell_control_context = default
    )
    {
        if (!primaryResolution.Applied)
        {
            return;
        }
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        BattleRatingSystem ratingSystem = Runtime?._battle_rating_system;
        foreach (CombatEffectDefinition chainEffect in effectDefinitions)
        {
            if (chainEffect == null || chainEffect.EffectKind != BattleEffectKind.ChainDamage)
            {
                continue;
            }
            List<CombatEffectDefinition> chainTargetEffects = BuildChainTargetEffectDefinitions(
                effectDefinitions,
                chainEffect
            );
            if (chainTargetEffects.Count == 0)
            {
                continue;
            }
            List<ChainDamageHop> chainTargets = CollectChainDamageTargets(
                source_unit,
                primary_target,
                skillDefinition,
                chainEffect,
                spell_control_context
            );
            if (chainTargets.Count == 0)
            {
                continue;
            }

            int totalDamage = 0;
            int totalHealing = 0;
            int totalKillCount = 0;
            foreach (ChainDamageHop chainHop in chainTargets)
            {
                BattleUnitState chainTarget = chainHop.TargetUnit;
                if (chainTarget == null || !chainTarget.is_alive)
                {
                    continue;
                }
                BattleBarrierInteractionResult barrierResult =
                    Runtime?._layered_barrier_service?.ResolveSkillBarrierInteractionFromCoordResult(
                        source_unit,
                        chainHop.OriginCoord,
                        chainTarget,
                        skillDefinition,
                        chainTargetEffects,
                        batch
                    ) ?? new BattleBarrierInteractionResult(false, false);
                if (barrierResult.Blocked)
                {
                    continue;
                }
                AttackEffectResolutionResult chainResolution =
                    damageResolver?.ResolveEffects(
                        source_unit,
                        chainTarget,
                        chainTargetEffects,
                        DamageResolutionContext
                            .ForSkill(skillDefinition?.SkillId ?? new StringName(""))
                            .WithBattleState(Runtime?.GetState())
                            .WithSourceSkillLevel(
                                Math.Max(
                                    source_unit.GetKnownSkillLevelTyped(
                                        skillDefinition?.SkillId ?? new StringName(""),
                                        fallback: 1
                                    ),
                                    1
                                )
                            )
                            .WithDamageApplicationHookContext(
                                batch,
                                Runtime?.CurrentEffectOriginForContingency
                                    ?? BattleEffectOrigin.PlayerCommand()
                            )
                    ) ?? new AttackEffectResolutionResult
                    {
                        AttackCheck = new AttackCheckInput(
                            skillId: skillDefinition?.SkillId ?? new StringName("")
                        ),
                    };
                skillMasteryService?.RecordTargetResult(
                    source_unit,
                    chainTarget,
                    skillDefinition,
                    chainResolution
                );
                _owner.MarkAppliedStatusesForTurnTiming(
                    chainTarget,
                    chainResolution.StatusEffectIds
                );
                if (!chainResolution.Applied)
                {
                    continue;
                }

                _owner._append_changed_unit_id(batch, source_unit.unit_id);
                _owner._append_changed_unit_id(batch, chainTarget.unit_id);
                _owner._append_changed_unit_coords(batch, chainTarget);
                _owner.append_result_source_status_effects(batch, source_unit, chainResolution);
                _skillPreviewService.AppendDamageResultLogLines(
                    batch,
                    $"{skill_subject} 的连锁闪电",
                    chainTarget.display_name,
                    chainResolution
                );
                foreach (StringName statusId in chainResolution.StatusEffectIds)
                {
                    batch.AddLogLine($"{chainTarget.display_name} 获得状态 {statusId}。");
                }

                int chainDamage = chainResolution.Damage;
                int chainHealing = chainResolution.Healing;
                totalDamage += chainDamage;
                totalHealing += chainHealing;
                if (!chainTarget.is_alive)
                {
                    totalKillCount += 1;
                    Runtime?._apply_on_kill_gain_resources_effects(
                        source_unit,
                        chainTarget,
                        skillDefinition,
                        chainTargetEffects,
                        batch
                    );
                    Runtime?.HandleUnitDefeatedByRuntimeEffect(
                        chainTarget,
                        source_unit,
                        batch,
                        $"{chainTarget.display_name} 被击倒。",
                        new BattleDefeatHandlingOptions(
                            recordEnemyDefeatedAchievement: true,
                            killProvenance: BattleSkillExecutionOrchestrator.BuildWeaponAttackKillProvenance(
                                source_unit,
                                chainResolution,
                                skillDefinition?.SkillId ?? new StringName("")
                            )
                        )
                    );
                }
                bool causedChainDefeat = !chainTarget.is_alive;
                _owner._record_effect_metrics(
                    source_unit,
                    chainTarget,
                    chainDamage,
                    chainHealing,
                    causedChainDefeat ? 1 : 0
                );
                ratingSystem?.RecordContributionFromUnits(
                    source_unit,
                    chainTarget,
                    chainDamage,
                    chainHealing,
                    causedChainDefeat,
                    new StringName("skill"),
                    skillDefinition?.SkillId ?? new StringName("")
                );
            }
        }
    }

    private static List<CombatEffectDefinition> BuildChainTargetEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition == chainEffect
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
            {
                continue;
            }
            chainTargetEffects.Add(effectDefinition);
        }
        return chainTargetEffects;
    }

    private List<ChainDamageHop> CollectChainDamageTargets(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDefinition skillDefinition,
        CombatEffectDefinition chainEffect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        var targets = new List<ChainDamageHop>();
        BattleState state = _owner.RtState();
        if (state == null || source_unit == null || primary_target == null || chainEffect == null)
        {
            return targets;
        }

        int maxRadius = _resolve_chain_damage_radius(
            primary_target,
            chainEffect,
            spell_control_context
        );
        if (maxRadius <= 0)
        {
            return targets;
        }
        bool preventRepeatTarget = ChainDamageParameters
            .FromEffect(chainEffect)
            .PreventRepeatTarget;
        StringName targetFilter = _owner.ResolveEffectTargetFilter(skillDefinition, chainEffect);
        if (BattleSkillExecutionOrchestrator.StringNameIsEmpty(targetFilter))
        {
            return targets;
        }

        BattleGridService gridService = Runtime?.GetGridService();
        var visited = new HashSet<StringName>();
        var queue = new List<BattleUnitState>();
        visited.Add(primary_target.unit_id);
        queue.Add(primary_target);

        while (queue.Count != 0)
        {
            BattleUnitState current = queue[0];
            queue.RemoveAt(0);

            foreach (BattleUnitState candidate in state.GetUnitsTyped())
            {
                if (candidate == null || !candidate.is_alive)
                {
                    continue;
                }
                if (
                    preventRepeatTarget
                    && visited.Contains(candidate.unit_id)
                )
                {
                    continue;
                }
                if (!_owner._is_unit_valid_for_effect(source_unit, candidate, targetFilter))
                {
                    continue;
                }
                if (!_is_within_chain_radius(primary_target, candidate, maxRadius))
                {
                    continue;
                }
                if (!_is_chain_path_clear(current, candidate))
                {
                    continue;
                }

                visited.Add(candidate.unit_id);
                targets.Add(new ChainDamageHop(current.coord, candidate));
                queue.Add(candidate);
            }
        }

        targets.Sort(
            (a, b) =>
            {
                BattleUnitState targetA = a.TargetUnit;
                BattleUnitState targetB = b.TargetUnit;
                int distanceA = gridService?.GetDistanceBetweenUnits(primary_target, targetA) ?? 0;
                int distanceB = gridService?.GetDistanceBetweenUnits(primary_target, targetB) ?? 0;
                if (distanceA != distanceB)
                    return distanceA.CompareTo(distanceB);
                Vector2I ca = targetA?.coord ?? Vector2I.Zero;
                Vector2I cb = targetB?.coord ?? Vector2I.Zero;
                if (ca.Y != cb.Y)
                    return ca.Y.CompareTo(cb.Y);
                if (ca.X != cb.X)
                    return ca.X.CompareTo(cb.X);
                return string.CompareOrdinal(
                    (targetA?.unit_id ?? new StringName("")).ToString(),
                    (targetB?.unit_id ?? new StringName("")).ToString()
                );
            }
        );
        return targets;
    }

    private int _resolve_chain_damage_radius(
        BattleUnitState primary_target,
        CombatEffectDefinition chainEffect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        ChainDamageParameters chainParams = ChainDamageParameters.FromEffect(chainEffect);
        int baseRadius = chainParams.BaseRadius;
        StringName bonusEffectId = chainParams.BonusTerrainEffectId;
        int radius = baseRadius;
        if (
            !BattleSkillExecutionOrchestrator.StringNameIsEmpty(bonusEffectId)
            && primary_target != null
            && _owner._unit_stands_on_terrain_effect(primary_target, bonusEffectId)
        )
        {
            radius = chainParams.WetChainRadius;
        }
        if (spell_control_context.BacklashTriggered)
        {
            radius += 1;
        }
        return radius;
    }

    internal bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    )
    {
        if (primary_target == null || candidate == null || max_radius <= 0)
        {
            return false;
        }
        BattleGridService gridService = Runtime?.GetGridService();
        foreach (Vector2I primaryCoord in primary_target.occupied_coords)
        {
            foreach (Vector2I candidateCoord in candidate.occupied_coords)
            {
                if (gridService != null && gridService.GetDistance(primaryCoord, candidateCoord) <= max_radius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal List<Vector2I> _get_line_coords(Vector2I from, Vector2I to)
    {
        var coords = new List<Vector2I>();
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1;
        int sy = from.Y < to.Y ? 1 : -1;
        int err = dx - dy;
        int x = from.X;
        int y = from.Y;
        while (x != to.X || y != to.Y)
        {
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
            if (x == to.X && y == to.Y)
            {
                break;
            }
            coords.Add(new Vector2I(x, y));
        }
        return coords;
    }

    internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        BattleState state = _owner.RtState();
        BattleGridService gridService = Runtime?.GetGridService();
        if (state == null || source_unit == null || target_unit == null || gridService == null)
        {
            return false;
        }
        foreach (Vector2I sourceCoord in source_unit.occupied_coords)
        {
            BattleCellState sourceCell = gridService.GetCellState(state, sourceCoord);
            if (sourceCell == null)
            {
                continue;
            }
            int sourceHeight = sourceCell.current_height;
            foreach (Vector2I targetCoord in target_unit.occupied_coords)
            {
                foreach (Vector2I midCoord in _get_line_coords(sourceCoord, targetCoord))
                {
                    BattleCellState midCell = gridService.GetCellState(state, midCoord);
                    if (midCell == null)
                    {
                        continue;
                    }
                    if (Math.Abs(midCell.current_height - sourceHeight) > 1)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
}

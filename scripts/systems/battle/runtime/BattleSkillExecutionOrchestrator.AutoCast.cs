using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleSkillExecutionOrchestrator — auto-cast skill execution (meteor swarm / unit / ground) + command build.
// Pure physical split: same class, no behavior change. See BattleSkillExecutionOrchestrator.cs.
internal sealed partial class BattleSkillExecutionOrchestrator
{
    private StringName _scopedAutoCastUnitId = "";
    private StringName _scopedAutoCastSkillId = "";
    private int _scopedAutoCastSkillLevel;

    internal bool ExecuteAutoCast(AutoCastRequest request, BattleEventBatch batch)
    {
        if (request?.IsValid != true || Runtime?.GetState() == null)
            return false;
        BattleState state = Runtime.GetState();
        if (!state.TryGetUnitTyped(request.CasterUnitId, out BattleUnitState caster) || caster?.is_alive != true)
            return false;
        SkillDefinition skillDefinition = Runtime.GetSkillDefinitionTyped(request.StoredSkillId);
        if (skillDefinition?.CombatProfile == null)
            return false;

        BattleCommand command = BuildAutoCastCommand(request);
        if (command == null)
            return false;

        using IDisposable scopedAutoCastLevel = PushScopedAutoCastSkillLevel(
            caster,
            skillDefinition.SkillId,
            request.CastLevel
        );
        using IDisposable scopedResolutionLevel = Runtime?._skill_resolution_rules?.PushScopedSkillLevel(
            caster,
            skillDefinition.SkillId,
            request.CastLevel
        );
        bool allowRepeat = skillDefinition.CombatProfile.AllowRepeatTarget;
        BattleSkillResolutionPolicy policy = Runtime?._skill_resolution_rules
            ?.BuildSkillResolutionPolicy(
                skillDefinition,
                caster,
                command.skill_variant_id,
                _targetValidationService._normalize_target_unit_ids(command, allowRepeat)
            );
        if (policy == null || !policy.OptionAllowed)
        {
            return false;
        }

        bool routesToUnitTargeting = policy.RoutesToUnitTargeting;
        bool isMeteorSwarm =
            skillDefinition.CombatProfile.SpecialResolutionProfileId
            == new StringName("meteor_swarm");
        Runtime?._skill_mastery_service.Clear();
        if (CanHandleUnitSkillCommandFromDefinitions(skillDefinition, policy))
        {
            bool definitionApplied = ExecuteAutoUnitSkill(
                caster,
                command,
                skillDefinition,
                policy.UnitExecutionCastVariantDefinition,
                policy.EffectDefinitions,
                batch
            );
            Runtime?._skill_mastery_service.Clear();
            return definitionApplied;
        }

        if (isMeteorSwarm)
        {
            bool meteorApplied = ExecuteAutoMeteorSwarmSkill(
                caster,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                batch
            );
            Runtime?._skill_mastery_service.Clear();
            return meteorApplied;
        }

        if (!routesToUnitTargeting && policy.GroundCastVariantDefinition != null)
        {
            bool groundApplied = ExecuteAutoGroundSkill(
                caster,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                batch
            );
            Runtime?._skill_mastery_service.Clear();
            return groundApplied;
        }

        Runtime?._skill_mastery_service.Clear();
        return false;
    }

    private IDisposable PushScopedAutoCastSkillLevel(
        BattleUnitState unitState,
        StringName skillId,
        int skillLevel
    )
    {
        StringName previousUnitId = _scopedAutoCastUnitId;
        StringName previousSkillId = _scopedAutoCastSkillId;
        int previousSkillLevel = _scopedAutoCastSkillLevel;
        _scopedAutoCastUnitId = ProgressionDataUtils.to_string_name(unitState?.unit_id ?? "");
        _scopedAutoCastSkillId = ProgressionDataUtils.to_string_name(skillId);
        _scopedAutoCastSkillLevel = Math.Max(skillLevel, 0);
        return new ScopedAutoCastSkillLevelScope(
            this,
            previousUnitId,
            previousSkillId,
            previousSkillLevel
        );
    }

    private void RestoreScopedAutoCastSkillLevel(
        StringName unitId,
        StringName skillId,
        int skillLevel
    )
    {
        _scopedAutoCastUnitId = unitId;
        _scopedAutoCastSkillId = skillId;
        _scopedAutoCastSkillLevel = skillLevel;
    }

    private sealed class ScopedAutoCastSkillLevelScope : IDisposable
    {
        private BattleSkillExecutionOrchestrator _owner;
        private readonly StringName _previousUnitId;
        private readonly StringName _previousSkillId;
        private readonly int _previousSkillLevel;

        internal ScopedAutoCastSkillLevelScope(
            BattleSkillExecutionOrchestrator owner,
            StringName previousUnitId,
            StringName previousSkillId,
            int previousSkillLevel
        )
        {
            _owner = owner;
            _previousUnitId = previousUnitId;
            _previousSkillId = previousSkillId;
            _previousSkillLevel = previousSkillLevel;
        }

        public void Dispose()
        {
            BattleSkillExecutionOrchestrator owner = _owner;
            _owner = null;
            owner?.RestoreScopedAutoCastSkillLevel(
                _previousUnitId,
                _previousSkillId,
                _previousSkillLevel
            );
        }
    }

    private bool ExecuteAutoMeteorSwarmSkill(
        BattleUnitState caster,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleSpecialProfileGateResult gateResult =
            Runtime?._special_profile_gate != null
                ? Runtime._special_profile_gate.CanExecuteSkill(
                    skillDefinition,
                    command,
                    caster,
                    Runtime._state
                )
                : null;
        if (gateResult == null || !gateResult.Allowed)
        {
            Runtime?._append_special_profile_gate_block(batch, gateResult);
            return false;
        }

        IReadOnlyList<Vector2I> targetCoords = ResolveAutoGroundTargetCoords(command);
        if (
            !ValidateAutoMeteorSwarmTarget(
                caster,
                skillDefinition,
                castVariantDefinition,
                targetCoords,
                batch
            )
        )
            return false;

        BattleMeteorSwarmResolver meteorResolver = Runtime?._meteor_swarm_resolver;
        BattleSkillOutcomeCommitter outcomeCommitter = Runtime?._skill_outcome_committer;
        if (meteorResolver == null || outcomeCommitter == null)
        {
            batch?.AddLogLine("该禁咒结算尚未接入。");
            return false;
        }

        BattleSpellControlResult spellControlContext = BattleSpellControlResult.None();
        BattleGroundBacklashTargetResult driftContext =
            BattleGroundBacklashTargetResult.None(targetCoords);
        MeteorSwarmCastContext context = meteorResolver.BuildCastContextTyped(
            caster,
            command,
            skillDefinition,
            castVariantDefinition,
            targetCoords[0],
            targetCoords[0],
            spellControlContext,
            driftContext
        );
        MeteorSwarmTargetPlan plan = meteorResolver.BuildTargetPlanTyped(context);
        MeteorSwarmCommitResult result = meteorResolver.ResolveTyped(plan);
        return outcomeCommitter.CommitMeteorSwarmResult(result, batch);
    }

    private bool ValidateAutoMeteorSwarmTarget(
        BattleUnitState caster,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    )
    {
        int requiredCoordCount = ResolveGroundRequiredCoordCount(castVariantDefinition);
        if (
            Runtime?.GetState() == null
            || Runtime.GetGridService() == null
            || caster == null
            || skillDefinition?.CombatProfile == null
            || castVariantDefinition == null
            || ResolveGroundCastTargetMode(skillDefinition, castVariantDefinition)
                != BattleTargetMode.Ground
            || targetCoords == null
            || targetCoords.Count != requiredCoordCount
        )
        {
            batch?.AddLogLine("技能或目标无效。");
            return false;
        }
        foreach (Vector2I coord in targetCoords)
        {
            if (!Runtime.GetGridService().IsInside(Runtime.GetState(), coord))
            {
                batch?.AddLogLine("存在超出战场范围的目标地格。");
                return false;
            }
        }
        if (
            !ValidateTargetCoordsShapeTyped(
                ResolveGroundFootprintPattern(castVariantDefinition),
                targetCoords
            )
        )
        {
            batch?.AddLogLine("目标地格排布不符合该技能形态。");
            return false;
        }
        string specialValidationMessage = Runtime?.GetGroundSpecialEffectValidationMessageTyped(
            caster,
            skillDefinition,
            castVariantDefinition,
            targetCoords
        ) ?? "";
        if (!string.IsNullOrEmpty(specialValidationMessage))
        {
            batch?.AddLogLine(specialValidationMessage);
            return false;
        }
        return true;
    }

    private static IReadOnlyList<Vector2I> ResolveAutoGroundTargetCoords(BattleCommand command)
    {
        var targetCoords = new List<Vector2I>();
        if (command == null)
            return targetCoords;
        foreach (Vector2I coord in command.TargetCoordsTyped)
            targetCoords.Add(coord);
        if (targetCoords.Count == 0 && command.target_coord != new Vector2I(-1, -1))
            targetCoords.Add(command.target_coord);
        return targetCoords;
    }

    private bool ExecuteAutoUnitSkill(
        BattleUnitState caster,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitSkillValidationResult validation = _targetValidationService._validate_unit_skill_targets_result(
            caster,
            command,
            skillDefinition,
            castVariantDefinition
        );
        bool isRandomChain = IsRandomChainSkill(skillDefinition);
        if (!validation.Allowed || (!isRandomChain && validation.TargetUnits.Count == 0))
        {
            return false;
        }

        IReadOnlyList<CombatEffectDefinition> resolvedEffectDefinitions =
            effectDefinitions
            ?? CollectUnitSkillEffectDefinitions(skillDefinition, castVariantDefinition, caster);
        if (
            !CanApplyUnitSkillOrRepeatResultFromDefinitions(resolvedEffectDefinitions)
        )
        {
            return false;
        }

        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDefinition repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(resolvedEffectDefinitions);
        if (isRandomChain)
        {
            return _randomChainSkillService._handle_random_chain_unit_skill_command(
                caster,
                skillDefinition,
                castVariantDefinition,
                batch,
                resolvedEffectDefinitions,
                repeatAttackEffect,
                BattleSpellControlResult.None()
            );
        }
        bool applied = false;
        foreach (BattleUnitState targetUnit in validation.TargetUnits)
        {
            if (targetUnit == null)
                continue;
            if (repeatAttackEffect != null)
            {
                if (
                    repeatAttackResolver != null
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        caster,
                        targetUnit,
                        skillDefinition,
                        resolvedEffectDefinitions,
                        repeatAttackEffect,
                        batch
                    )
                )
                {
                    applied = true;
                }
                continue;
            }
            if (
                _apply_unit_skill_result(
                    caster,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    resolvedEffectDefinitions,
                    batch,
                    BattleSpellControlResult.None()
                )
            )
            {
                applied = true;
            }
        }
        return applied;
    }

    private bool ExecuteAutoGroundSkill(
        BattleUnitState caster,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                caster,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        if (!validation.Allowed)
            return false;

        IReadOnlyList<Vector2I> targetCoords = validation.TargetCoords ?? Array.Empty<Vector2I>();
        string precastValidationMessage =
            Runtime?.GetGroundSpecialEffectValidationMessageTyped(
                caster,
                skillDefinition,
                castVariantDefinition,
                targetCoords
            ) ?? "";
        if (!string.IsNullOrEmpty(precastValidationMessage))
        {
            batch?.AddLogLine(precastValidationMessage);
            return false;
        }
        Vector2I casterCoordBeforePrecast = caster?.coord ?? new Vector2I(-1, -1);
        if (
            Runtime?.ApplyGroundPrecastSpecialEffectsTyped(
                caster,
                skillDefinition,
                castVariantDefinition,
                targetCoords,
                batch
            ) != true
        )
            return false;
        bool precastRelocationApplied =
            caster != null && caster.coord != casterCoordBeforePrecast;

        GroundEffectBarrierClipContext barrierClip = ResolveGroundEffectBarrierClipContext(
            caster,
            skillDefinition,
            castVariantDefinition,
            targetCoords,
            batch
        );
        BattleGroundUnitEffectsResult unitResult = Runtime.ApplyGroundUnitEffectsResultTyped(
            caster,
            skillDefinition,
            castVariantDefinition,
            barrierClip.UnitEffectDefinitions,
            barrierClip.UnitEffectCoords,
            batch,
            targetCoords,
            barrierClip.VisibleEffectCoords
        );
        BattleGroundTerrainEffectsResult terrainResult =
            Runtime.ApplyGroundTerrainEffectsResultTyped(
                caster,
                skillDefinition,
                barrierClip.TerrainEffectDefinitions,
                barrierClip.TerrainEffectCoords,
                batch
            );
        bool applied =
            precastRelocationApplied
            || barrierClip.BarrierApplied
            || unitResult.Applied
            || terrainResult.Applied;
        if (applied)
        {
            batch?.AddLogLine(
                $"{caster.display_name} 触发 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，影响了 {barrierClip.VisibleEffectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
    }

    private static BattleCommand BuildAutoCastCommand(AutoCastRequest request)
    {
        ContingencyTargetResolutionResult target = request?.TargetResolution;
        if (target == null)
            return null;
        BattleCommand command = new()
        {
            command_type = "skill",
            unit_id = request.CasterUnitId,
            skill_entry_id = request.SkillEntryId,
            skill_id = request.StoredSkillId,
            target_unit_id = target.TargetUnitId,
            target_coord = target.TargetCell,
        };
        if (target.IsGroundTarget)
        {
            command.AddTargetCoord(target.TargetCell);
        }
        else if (target.TargetUnitId != "")
        {
            command.AddTargetUnitId(target.TargetUnitId);
        }
        return command;
    }
}

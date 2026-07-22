using System;
using System.Collections.Generic;
using Godot;

internal class BattleAttackCheckPolicyService
{
    private static readonly StringName ROUTE_SKILL_ATTACK_CHECK = "skill_attack_check";
    private static readonly StringName ROUTE_SKILL_ATTACK_PREVIEW = "skill_attack_preview";
    private static readonly StringName ROUTE_REPEAT_ATTACK_PREVIEW = "repeat_attack_preview";
    private static readonly StringName ROUTE_FORCE_HIT_NO_CRIT_PREVIEW = "force_hit_no_crit_preview";
    private static readonly StringName ROLL_KIND_SPELL_ATTACK = "spell_attack";
    private static readonly StringName ROLL_KIND_REPEAT_WEAPON_STAGE = "repeat_weapon_stage";

    private const int RepeatAttackPreviewStageGuard = 32;
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleHitResolver _hitResolver;
    private BattleTerrainEffectSystem _terrainEffectSystem;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleHitResolver hit_resolver,
        BattleTerrainEffectSystem terrain_effect_system
    )
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _hitResolver = hit_resolver;
        _terrainEffectSystem = terrain_effect_system;
    }

    internal void Dispose()
    {
        _runtimeRef = null;
        _hitResolver = null;
        _terrainEffectSystem = null;
    }

    public BattleAttackRollModifierBundle BuildModifierBundle(
        BattleAttackCheckPolicyContext context
    )
    {
        var bundle = new BattleAttackRollModifierBundle();
        if (context == null)
        {
            return bundle;
        }

        var filteredSpecs = new List<BattleAttackRollModifierSpec>();
        foreach (BattleAttackRollModifierSpec candidate in CollectModifierCandidates(context))
        {
            if (ModifierApplies(candidate, context))
            {
                filteredSpecs.Add(candidate);
            }
        }

        foreach (BattleAttackRollModifierSpec spec in ResolveStackedSpecs(filteredSpecs))
        {
            bundle.AddSpec(spec);
        }
        return bundle;
    }

    public BattleAttackCheckPolicyContext BuildSkillDefinitionAttackContext(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        StringName check_route,
        StringName trace_source,
        bool force_hit_no_crit
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_definition,
            ROLL_KIND_SPELL_ATTACK,
            check_route,
            trace_source
        );
        context.force_hit_no_crit = force_hit_no_crit;
        return context;
    }

    internal BattleAttackCheckPolicyContext BuildSkillDefinitionAttackContext(
        BattleState battle_state,
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        StringName check_route,
        StringName trace_source,
        bool force_hit_no_crit
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_definition,
            ROLL_KIND_SPELL_ATTACK,
            check_route,
            trace_source
        );
        context.force_hit_no_crit = force_hit_no_crit;
        return context;
    }

    public AttackCheckInput BuildAttackCheck(
        BattleAttackCheckPolicyContext context,
        int flat_bonus,
        int flat_penalty
    )
    {
        if (_hitResolver == null || context == null)
        {
            return new AttackCheckInput(invalid: true);
        }
        if (IsEmpty(context.roll_kind))
        {
            context.roll_kind = ROLL_KIND_SPELL_ATTACK;
        }

        BattleAttackRollModifierBundle modifierBundle = BuildModifierBundle(context);
        EquipmentAttackDefenseAdjustment defenseAdjustment = BuildAttackDefenseAdjustment(context);
        bool hasAdvantage = modifierBundle.HasAdvantage;
        AttackCheckInput attackCheck = CopyAttackCheckWithAdvantage(
            _hitResolver.BuildSkillDefinitionAttackCheck(
                context.attacker_view,
                context.target_view,
                context.skill_definition,
                flat_bonus + modifierBundle.TotalBonus,
                flat_penalty + modifierBundle.TotalPenalty,
                defenseAdjustment
            ),
            hasAdvantage
        );
        return CopyAttackCheckWithCriticalOverride(context, attackCheck);
    }

    public AttackPreviewData BuildAttackPreview(BattleAttackCheckPolicyContext context)
    {
        if (_hitResolver == null || context == null)
        {
            return new AttackPreviewData();
        }
        if (IsEmpty(context.roll_kind))
        {
            context.roll_kind = ROLL_KIND_SPELL_ATTACK;
        }

        if (context.force_hit_no_crit)
        {
            AttackPreviewData forcePreview = _hitResolver.BuildForceHitNoCritAttackPreview();
            context.check_route = ROUTE_FORCE_HIT_NO_CRIT_PREVIEW;
            AppendModifierBundlePayload(forcePreview, BuildModifierBundle(context));
            return forcePreview;
        }

        if (IsEmpty(context.check_route))
        {
            context.check_route = ROUTE_SKILL_ATTACK_PREVIEW;
        }

        BattleAttackRollModifierBundle modifierBundle = BuildModifierBundle(context);
        EquipmentAttackDefenseAdjustment defenseAdjustment = BuildAttackDefenseAdjustment(context);
        bool hasCriticalOverride =
            ResolveRuntime()
                ?.GetEquipmentAbilityRuntimeService()
                ?.ResolveCriticalHitOverride(context)
                ?.ForceCriticalOnHit == true;
        if (modifierBundle.IsEmpty() && defenseAdjustment.IsEmpty && !hasCriticalOverride)
        {
            return _hitResolver.BuildSkillDefinitionAttackPreview(
                context.battle_state,
                context.attacker_view,
                context.target_view,
                context.skill_definition,
                false
            );
        }

        AttackCheckInput attackCheck = BuildAttackCheck(context, 0, 0);
        AttackCheckInput resolvedCheck = _hitResolver.BuildFateAwareAttackCheckPreview(
            context.battle_state,
            context.attacker_view,
            context.target_view,
            attackCheck
        );
        int successRate = resolvedCheck.SuccessRatePercent;
        int baseHitRate = resolvedCheck.BaseHitRatePercent;
        string previewText = resolvedCheck.PreviewText;
        bool forceCriticalOnHit =
            resolvedCheck.ForceCriticalOnHit
            && !resolvedCheck.CritLocked
            && !resolvedCheck.ForceHitNoCrit;

        var preview = new AttackPreviewData
        {
            SummaryText = forceCriticalOnHit
                ? $"预计命中率 {previewText}；命中后必定暴击"
                : $"预计命中率 {previewText}",
            Stages = new List<AttackPreviewStage>
            {
                new AttackPreviewStage(
                    hitRatePercent: successRate,
                    successRatePercent: successRate,
                    baseHitRatePercent: baseHitRate,
                    requiredRoll: resolvedCheck.RequiredRoll,
                    displayRequiredRoll: resolvedCheck.DisplayRequiredRoll,
                    previewText: previewText
                ),
            },
            HitRatePercent = successRate,
            SuccessRatePercent = successRate,
            BaseHitRatePercent = baseHitRate,
            ForceCriticalOnHit = forceCriticalOnHit,
            CritLocked = resolvedCheck.CritLocked,
            FatePreview = BattleFatePreviewData.FromAttackCheck(resolvedCheck),
        };
        AppendModifierBundlePayload(preview, modifierBundle);
        return preview;
    }

    public AttackPreviewData BuildRepeatAttackPreview(
        BattleAttackCheckPolicyContext context,
        List<BattleRepeatAttackStageSpec> stage_specs
    )
    {
        if (
            _hitResolver == null
            || context == null
            || !context.attacker_view.IsValid
            || !context.target_view.IsValid
            || context.skill_definition == null
            || stage_specs == null
            || stage_specs.Count == 0
        )
        {
            return new AttackPreviewData();
        }

        int normalizedStageCount = Mathf.Min(
            Mathf.Max(stage_specs.Count, 1),
            RepeatAttackPreviewStageGuard
        );
        var summaryChecks = new List<AttackCheckInput>();
        var stages = new List<AttackPreviewStage>();
        var combinedBreakdown = new List<BattleAttackRollModifierSpec>();

        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            BattleRepeatAttackStageSpec stageSpec = stage_specs[stageIndex];
            stageSpec = stageSpec.WithFateAware(true);
            BattleAttackCheckPolicyContext stageContext = CopyContextForRepeatStage(
                context,
                stageSpec,
                ROUTE_REPEAT_ATTACK_PREVIEW
            );
            AttackCheckInput attackCheck = BuildFateAwareRepeatAttackStageHitCheck(stageContext);
            summaryChecks.Add(attackCheck);
            int stageSuccessRate = attackCheck.SuccessRatePercent;
            stages.Add(
                new AttackPreviewStage(
                    hitRatePercent: stageSuccessRate,
                    successRatePercent: stageSuccessRate,
                    baseHitRatePercent: attackCheck.BaseHitRatePercent,
                    requiredRoll: attackCheck.RequiredRoll,
                    displayRequiredRoll: attackCheck.DisplayRequiredRoll,
                    previewText: attackCheck.PreviewText
                )
            );
            foreach (BattleAttackRollModifierSpec spec in BuildModifierBundle(stageContext).Breakdown)
            {
                combinedBreakdown.Add(spec);
            }
        }

        var preview = new AttackPreviewData
        {
            SummaryText = _hitResolver.FormatRepeatAttackPreviewSummary(summaryChecks),
            Stages = stages,
            HitRatePercent = AverageStageRate(stages, stage => stage.SuccessRatePercent),
            SuccessRatePercent = AverageStageRate(stages, stage => stage.SuccessRatePercent),
            BaseHitRatePercent = AverageStageRate(stages, stage => stage.BaseHitRatePercent),
            BaseAttackBonus = stage_specs[0].stage_base_attack_bonus,
            FollowUpAttackPenalty = stage_specs[0].follow_up_attack_penalty,
            ForceCriticalOnHit = summaryChecks.Exists(
                check => check.ForceCriticalOnHit && !check.CritLocked && !check.ForceHitNoCrit
            ),
            CritLocked = summaryChecks.Count > 0 && summaryChecks[0].CritLocked,
            FatePreview = summaryChecks.Count > 0
                ? BattleFatePreviewData.FromAttackCheck(summaryChecks[0])
                : null,
        };
        if (combinedBreakdown.Count != 0)
        {
            preview.SetAttackRollModifierBreakdown(combinedBreakdown);
        }
        return preview;
    }

    public BattleAttackCheckPolicyContext BuildRepeatAttackStageContext(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        BattleRepeatAttackStageSpec stage_spec,
        StringName check_route,
        StringName trace_source
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_definition,
            ROLL_KIND_REPEAT_WEAPON_STAGE,
            check_route,
            trace_source
        );
        context.repeat_stage_spec = stage_spec;
        context.has_repeat_stage_spec = !stage_spec.Equals(default(BattleRepeatAttackStageSpec));
        return context;
    }

    internal BattleAttackCheckPolicyContext BuildRepeatAttackStageContext(
        BattleState battle_state,
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        BattleRepeatAttackStageSpec stage_spec,
        StringName check_route,
        StringName trace_source
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_definition,
            ROLL_KIND_REPEAT_WEAPON_STAGE,
            check_route,
            trace_source
        );
        context.repeat_stage_spec = stage_spec;
        context.has_repeat_stage_spec = !stage_spec.Equals(default(BattleRepeatAttackStageSpec));
        return context;
    }

    public AttackCheckInput BuildRepeatAttackStageHitCheck(BattleAttackCheckPolicyContext context)
    {
        if (_hitResolver == null || context == null || !context.has_repeat_stage_spec)
        {
            return new AttackCheckInput(invalid: true);
        }

        context.roll_kind = ROLL_KIND_REPEAT_WEAPON_STAGE;
        BattleRepeatAttackStageSpec resolvedStageSpec = context.repeat_stage_spec;
        BattleAttackRollModifierBundle modifierBundle = BuildModifierBundle(context);
        EquipmentAttackDefenseAdjustment defenseAdjustment = BuildAttackDefenseAdjustment(context);
        bool hasAdvantage = modifierBundle.HasAdvantage;
        AttackCheckInput attackCheck = CopyAttackCheckWithAdvantage(
            _hitResolver.BuildSkillDefinitionAttackCheck(
                context.attacker_view,
                context.target_view,
                context.skill_definition,
                resolvedStageSpec.stage_base_attack_bonus + modifierBundle.TotalBonus,
                resolvedStageSpec.ResolveStageAttackPenalty() + modifierBundle.TotalPenalty,
                defenseAdjustment
            ),
            hasAdvantage
        );
        return CopyAttackCheckWithCriticalOverride(context, attackCheck);
    }

    public AttackCheckInput BuildFateAwareRepeatAttackStageHitCheck(
        BattleAttackCheckPolicyContext context
    )
    {
        if (_hitResolver == null || context == null || !context.has_repeat_stage_spec)
        {
            return new AttackCheckInput(invalid: true);
        }
        context.repeat_stage_spec = context.repeat_stage_spec.WithFateAware(true);
        AttackCheckInput baseAttackCheck = BuildRepeatAttackStageHitCheck(context);
        return _hitResolver.BuildFateAwareAttackCheckPreview(
            context.battle_state,
            context.attacker_view,
            context.target_view,
            baseAttackCheck
        );
    }

    public AttackRollResult RollAttackCheck(BattleState battle_state, AttackCheckInput attack_check)
    {
        return _hitResolver != null
            ? _hitResolver.RollAttackCheck(battle_state, attack_check)
            : new AttackRollResult();
    }

    public List<BattleAttackRollModifierSpec> ResolveStackedSpecs(
        IReadOnlyList<BattleAttackRollModifierSpec> candidates
    )
    {
        var grouped = new Dictionary<string, List<BattleAttackRollModifierSpec>>();
        var order = new List<string>();

        for (int index = 0; index < (candidates?.Count ?? 0); index++)
        {
            BattleAttackRollModifierSpec spec = candidates[index];
            if (spec == null)
            {
                continue;
            }
            StringName stackKeyName = spec.stack_key;
            if (IsEmpty(stackKeyName))
            {
                stackKeyName = new StringName(
                    $"__unique_{index}_{spec.source_domain}_{spec.source_id}_{spec.source_instance_id}"
                );
            }
            string stackKey = stackKeyName.ToString();
            if (!grouped.ContainsKey(stackKey))
            {
                grouped[stackKey] = new List<BattleAttackRollModifierSpec>();
                order.Add(stackKey);
            }
            grouped[stackKey].Add(spec);
        }

        var resolvedSpecs = new List<BattleAttackRollModifierSpec>();
        foreach (string stackKey in order)
        {
            BattleAttackRollModifierSpec resolved = ResolveStackGroup(grouped[stackKey]);
            if (resolved != null)
            {
                resolvedSpecs.Add(resolved);
            }
        }
        resolvedSpecs.Sort((a, b) => BuildSortKey(a).CompareTo(BuildSortKey(b)));

        return resolvedSpecs;
    }

    private BattleAttackCheckPolicyContext BuildContext(
        BattleState battleState,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        StringName rollKind,
        StringName checkRoute,
        StringName traceSource
    )
    {
        // 统一交给 read-view builder 归一化 footprint，再回填 State 引用供
        // 不纯消费方（装备能力运行时）使用。
        BattleAttackCheckPolicyContext context = BuildContext(
            battleState,
            (BattleUnitReadView)activeUnit,
            (BattleUnitReadView)targetUnit,
            skillDefinition,
            rollKind,
            checkRoute,
            traceSource
        );
        context.attacker = activeUnit;
        context.target = targetUnit;
        return context;
    }

    private BattleAttackCheckPolicyContext BuildContext(
        BattleState battleState,
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        StringName rollKind,
        StringName checkRoute,
        StringName traceSource
    )
    {
        activeUnit.UnsafeUnitForReadOnlyRules?.RefreshFootprint();
        targetUnit.UnsafeUnitForReadOnlyRules?.RefreshFootprint();
        return new BattleAttackCheckPolicyContext
        {
            battle_state = battleState ?? ResolveBattleState(),
            attacker_view = activeUnit,
            target_view = targetUnit,
            skill_definition = skillDefinition,
            roll_kind = rollKind,
            check_route = checkRoute,
            trace_source = traceSource,
            distance = ResolveDistance(activeUnit, targetUnit),
            source_coord = activeUnit.IsValid ? activeUnit.Coord : new Vector2I(-1, -1),
            target_coord = targetUnit.IsValid ? targetUnit.Coord : new Vector2I(-1, -1),
        };
    }

    private List<BattleAttackRollModifierSpec> CollectModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        var candidates = new List<BattleAttackRollModifierSpec>();
        foreach (BattleAttackRollModifierSpec spec in CollectTerrainModifierCandidates(context))
        {
            candidates.Add(spec);
        }
        foreach (BattleAttackRollModifierSpec spec in CollectStatusModifierCandidates(context))
        {
            candidates.Add(spec);
        }
        foreach (BattleAttackRollModifierSpec spec in CollectEquipmentAbilityModifierCandidates(context))
        {
            candidates.Add(spec);
        }
        return candidates;
    }

    private static List<BattleAttackRollModifierSpec> CollectStatusModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        if (context == null)
            return new List<BattleAttackRollModifierSpec>();
        return CollectStatusModifierCandidates(context.attacker_view, context.target_view);
    }

    private static List<BattleAttackRollModifierSpec> CollectStatusModifierCandidates(
        BattleUnitReadView attacker,
        BattleUnitReadView target
    )
    {
        var candidates = new List<BattleAttackRollModifierSpec>();
        if (!attacker.IsValid || !target.IsValid)
            return candidates;

        foreach (BattleStatusReadView status in attacker.StatusEffects())
        {
            if (status.AttackRollAdvantage)
            {
                candidates.Add(
                    new BattleAttackRollModifierSpec
                    {
                        source_domain = "status",
                        source_id = status.StatusId,
                        source_instance_id = status.SourceUnitId.ToString(),
                        label = ResolveStatusModifierLabel(status),
                        modifier_delta = 0,
                        stack_key = status.StatusId,
                        stack_mode = "max",
                        target_team_filter = "any",
                        endpoint_mode = "target",
                        footprint_mode = "any_cell",
                        applies_to = "attack_advantage",
                    }
                );
            }

            int penalty = Math.Max(status.SourceBoundAttackRollPenalty, 0);
            if (penalty <= 0)
                continue;
            int minStacks = Math.Max(status.SourceBoundAttackRollPenaltyMinStacks, 1);
            if (Math.Max(status.Stacks, 0) < minStacks)
                continue;
            if (ProgressionDataUtils.to_string_name(status.SourceUnitId) != target.UnitId)
                continue;

            candidates.Add(
                new BattleAttackRollModifierSpec
                {
                    source_domain = "status",
                    source_id = status.StatusId,
                    source_instance_id = status.SourceUnitId.ToString(),
                    label = ResolveStatusModifierLabel(status),
                    modifier_delta = -penalty,
                    stack_key = status.StatusId,
                    stack_mode = "max",
                    target_team_filter = "any",
                    endpoint_mode = "target",
                    footprint_mode = "any_cell",
                    applies_to = "attack_roll",
                }
            );
        }
        foreach (BattleStatusReadView status in target.StatusEffects())
        {
            int bonusPerStack = Math.Max(status.SourceBoundIncomingAttackRollBonusPerStack, 0);
            if (bonusPerStack <= 0)
                continue;
            int minStacks = Math.Max(status.SourceBoundIncomingAttackRollBonusMinStacks, 1);
            int stacks = Math.Max(status.Stacks, 0);
            if (stacks < minStacks)
                continue;
            if (ProgressionDataUtils.to_string_name(status.SourceUnitId) != attacker.UnitId)
                continue;

            candidates.Add(
                new BattleAttackRollModifierSpec
                {
                    source_domain = "status",
                    source_id = status.StatusId,
                    source_instance_id = status.SourceUnitId.ToString(),
                    label = ResolveStatusModifierLabel(status),
                    modifier_delta = bonusPerStack * stacks,
                    stack_key = status.StatusId,
                    stack_mode = "max",
                    target_team_filter = "any",
                    endpoint_mode = "target",
                    footprint_mode = "any_cell",
                    applies_to = "attack_roll",
                }
            );
        }
        return candidates;
    }

    private static string ResolveStatusModifierLabel(BattleStatusReadView status)
    {
        string label = status.DisplayLabel;
        return string.IsNullOrWhiteSpace(label) ? status.StatusId.ToString() : label;
    }

    private List<BattleAttackRollModifierSpec> CollectEquipmentAbilityModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        IBattleEquipmentAbilityReactionService equipmentAbilityService =
            ResolveRuntime()?.GetEquipmentAbilityRuntimeService();
        return equipmentAbilityService != null
            ? equipmentAbilityService.CollectAttackRollModifierCandidates(context)
            : new List<BattleAttackRollModifierSpec>();
    }

    private EquipmentAttackDefenseAdjustment BuildAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    )
    {
        IBattleEquipmentAbilityReactionService equipmentAbilityService =
            ResolveRuntime()?.GetEquipmentAbilityRuntimeService();
        return equipmentAbilityService != null
            ? equipmentAbilityService.CollectAttackDefenseAdjustment(context)
            : new EquipmentAttackDefenseAdjustment();
    }

    private List<BattleAttackRollModifierSpec> CollectTerrainModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        var candidates = new List<BattleAttackRollModifierSpec>();
        BattleState state = context?.battle_state;
        if (state == null)
        {
            return candidates;
        }

        foreach (Vector2I coord in CollectEndpointCoords(context))
        {
            if (!state.TryGetCellTyped(coord, out BattleCellState cell))
            {
                continue;
            }
            foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
            {
                if (
                    effectState == null
                    || !BattleTerrainEffectSystem.IsTerrainEffectActive(effectState)
                )
                {
                    continue;
                }
                BattleAttackRollModifierSpec spec = effectState.accuracy_modifier_spec?.Clone();
                if (spec == null)
                {
                    continue;
                }
                if (!EffectCoordMatchesEndpointMode(coord, spec, context))
                {
                    continue;
                }
                if (IsEmpty(spec.source_domain))
                {
                    spec.source_domain = "terrain";
                }
                if (IsEmpty(spec.source_id))
                {
                    spec.source_id = effectState.effect_id;
                }
                if (string.IsNullOrEmpty(spec.source_instance_id))
                {
                    spec.source_instance_id = effectState.field_instance_id.ToString();
                }
                candidates.Add(spec);
            }
        }
        return candidates;
    }

    private bool ModifierApplies(
        BattleAttackRollModifierSpec spec,
        BattleAttackCheckPolicyContext context
    )
    {
        if (spec == null || context == null)
        {
            return false;
        }
        if (
            spec.AppliesToKind != BattleAttackRollModifierApplyTarget.AttackRoll
            && spec.AppliesToKind != BattleAttackRollModifierApplyTarget.AttackAdvantage
        )
        {
            return false;
        }
        if (
            spec.AppliesToKind == BattleAttackRollModifierApplyTarget.AttackRoll
            && spec.modifier_delta == 0
        )
        {
            return false;
        }
        if (!IsEmpty(spec.roll_kind_filter) && spec.roll_kind_filter != context.roll_kind)
        {
            return false;
        }
        if (spec.distance_min_exclusive >= 0 && context.distance <= spec.distance_min_exclusive)
        {
            return false;
        }
        if (spec.distance_max_inclusive >= 0 && context.distance > spec.distance_max_inclusive)
        {
            return false;
        }
        if (!TeamFilterApplies(spec.target_team_filter, context))
        {
            return false;
        }
        return true;
    }

    private BattleAttackRollModifierSpec ResolveStackGroup(List<BattleAttackRollModifierSpec> group)
    {
        if (group == null || group.Count == 0)
        {
            return null;
        }
        BattleAttackRollModifierSpec first = group[0];
        if (first == null)
        {
            return null;
        }

        bool hasBonus = false;
        bool hasPenalty = false;
        foreach (BattleAttackRollModifierSpec spec in group)
        {
            if (spec == null)
            {
                continue;
            }
            hasBonus = hasBonus || spec.modifier_delta > 0;
            hasPenalty = hasPenalty || spec.modifier_delta < 0;
        }
        // 同 stack_key 正负同现不整组丢弃:修正默认累加,直接求和取净值,
        // 净值条目保留在 breakdown 中可见(净 0 也保留,标记冲突相抵)。
        // 同号组继续走各自配置的堆叠模式。
        if (hasBonus && hasPenalty)
        {
            return SumStackGroup(group);
        }

        if (first.StackModeKind == BattleAttackRollModifierStackMode.Exclusive)
        {
            return group.Count == 1 ? first : null;
        }
        if (first.StackModeKind == BattleAttackRollModifierStackMode.Max)
        {
            return PickMaxStackSpec(group);
        }
        if (first.StackModeKind == BattleAttackRollModifierStackMode.Min)
        {
            return PickMinStackSpec(group);
        }
        return SumStackGroup(group);
    }

    private BattleAttackRollModifierSpec PickMaxStackSpec(List<BattleAttackRollModifierSpec> group)
    {
        BattleAttackRollModifierSpec best = group[0];
        foreach (BattleAttackRollModifierSpec spec in group)
        {
            if (spec == null)
            {
                continue;
            }
            if (best.modifier_delta < 0 || spec.modifier_delta < 0)
            {
                if (spec.modifier_delta < best.modifier_delta)
                {
                    best = spec;
                }
            }
            else if (spec.modifier_delta > best.modifier_delta)
            {
                best = spec;
            }
        }
        return best;
    }

    private BattleAttackRollModifierSpec PickMinStackSpec(List<BattleAttackRollModifierSpec> group)
    {
        BattleAttackRollModifierSpec best = group[0];
        foreach (BattleAttackRollModifierSpec spec in group)
        {
            if (spec == null)
            {
                continue;
            }
            if (
                best.modifier_delta > 0
                && spec.modifier_delta > 0
                && spec.modifier_delta < best.modifier_delta
            )
            {
                best = spec;
            }
            else if (
                best.modifier_delta < 0
                && spec.modifier_delta < 0
                && spec.modifier_delta > best.modifier_delta
            )
            {
                best = spec;
            }
        }
        return best;
    }

    private BattleAttackRollModifierSpec SumStackGroup(List<BattleAttackRollModifierSpec> group)
    {
        BattleAttackRollModifierSpec source = group[0];
        if (source == null)
        {
            return null;
        }

        var summed = new BattleAttackRollModifierSpec
        {
            source_domain = source.source_domain,
            source_id = source.source_id,
            source_instance_id = source.source_instance_id,
            label = source.label,
            stack_key = source.stack_key,
            stack_mode = source.stack_mode,
            roll_kind_filter = source.roll_kind_filter,
            endpoint_mode = source.endpoint_mode,
            distance_min_exclusive = source.distance_min_exclusive,
            distance_max_inclusive = source.distance_max_inclusive,
            target_team_filter = source.target_team_filter,
            footprint_mode = source.footprint_mode,
            applies_to = source.applies_to,
        };
        foreach (BattleAttackRollModifierSpec spec in group)
        {
            if (spec != null)
            {
                summed.modifier_delta += spec.modifier_delta;
            }
        }
        return summed;
    }

    private void AppendModifierBundlePayload(
        AttackPreviewData target,
        BattleAttackRollModifierBundle modifierBundle
    )
    {
        if (target == null || modifierBundle == null || modifierBundle.IsEmpty())
        {
            return;
        }
        target.SetAttackRollModifierBreakdown(modifierBundle.Breakdown);
    }

    private static AttackCheckInput CopyAttackCheckWithAdvantage(
        AttackCheckInput source,
        bool isAdvantage
    )
    {
        if (!isAdvantage || source.Invalid)
            return source;
        return new AttackCheckInput(
            attackerBaseAttackBonus: source.AttackerBaseAttackBonus,
            attackerAttackBonus: source.AttackerAttackBonus,
            attackerBab: source.AttackerBab,
            targetArmorClass: source.TargetArmorClass,
            skillAttackBonus: source.SkillAttackBonus,
            lockedSkillHitBonus: source.LockedSkillHitBonus,
            situationalAttackBonus: source.SituationalAttackBonus,
            situationalAttackPenalty: source.SituationalAttackPenalty,
            requiredRoll: source.RequiredRoll,
            displayRequiredRoll: source.DisplayRequiredRoll,
            hitRatePercent: source.HitRatePercent,
            successRatePercent: source.SuccessRatePercent,
            baseHitRatePercent: source.BaseHitRatePercent,
            naturalOneAutoMiss: source.NaturalOneAutoMiss,
            naturalTwentyAutoHit: source.NaturalTwentyAutoHit,
            critThreshold: source.CritThreshold,
            fumbleLowEnd: source.FumbleLowEnd,
            critLocked: source.CritLocked,
            critGateDie: source.CritGateDie,
            effectiveLuck: source.EffectiveLuck,
            forceHitNoCrit: source.ForceHitNoCrit,
            forceCriticalOnHit: source.ForceCriticalOnHit,
            forcedCriticalSourceEquipmentInstanceId: source.ForcedCriticalSourceEquipmentInstanceId,
            forcedCriticalSourceBindingId: source.ForcedCriticalSourceBindingId,
            forcedCriticalSourceActionId: source.ForcedCriticalSourceActionId,
            skillId: source.SkillId,
            followUpAttackPenalty: source.FollowUpAttackPenalty,
            exponentialPenalty: source.ExponentialPenalty,
            isDisadvantage: source.IsDisadvantage,
            isAdvantage: true,
            invalid: source.Invalid,
            errorId: source.ErrorId,
            errorMessage: source.ErrorMessage,
            previewText: source.PreviewText
        );
    }

    private AttackCheckInput CopyAttackCheckWithCriticalOverride(
        BattleAttackCheckPolicyContext context,
        AttackCheckInput source
    )
    {
        if (source.Invalid || source.ForceHitNoCrit || context?.force_hit_no_crit == true)
            return source;
        IBattleEquipmentAbilityReactionService equipmentAbilityService =
            ResolveRuntime()?.GetEquipmentAbilityRuntimeService();
        BattleEquipmentAbilityCriticalHitOverrideResult criticalOverride =
            equipmentAbilityService?.ResolveCriticalHitOverride(context);
        if (criticalOverride?.ForceCriticalOnHit != true)
            return source;
        return new AttackCheckInput(
            attackerBaseAttackBonus: source.AttackerBaseAttackBonus,
            attackerAttackBonus: source.AttackerAttackBonus,
            attackerBab: source.AttackerBab,
            targetArmorClass: source.TargetArmorClass,
            skillAttackBonus: source.SkillAttackBonus,
            lockedSkillHitBonus: source.LockedSkillHitBonus,
            situationalAttackBonus: source.SituationalAttackBonus,
            situationalAttackPenalty: source.SituationalAttackPenalty,
            requiredRoll: source.RequiredRoll,
            displayRequiredRoll: source.DisplayRequiredRoll,
            hitRatePercent: source.HitRatePercent,
            successRatePercent: source.SuccessRatePercent,
            baseHitRatePercent: source.BaseHitRatePercent,
            naturalOneAutoMiss: source.NaturalOneAutoMiss,
            naturalTwentyAutoHit: source.NaturalTwentyAutoHit,
            critThreshold: source.CritThreshold,
            fumbleLowEnd: source.FumbleLowEnd,
            critLocked: source.CritLocked,
            critGateDie: source.CritGateDie,
            effectiveLuck: source.EffectiveLuck,
            forceHitNoCrit: source.ForceHitNoCrit,
            forceCriticalOnHit: true,
            forcedCriticalSourceEquipmentInstanceId: criticalOverride.SourceEquipmentInstanceId,
            forcedCriticalSourceBindingId: criticalOverride.BindingId,
            forcedCriticalSourceActionId: criticalOverride.ActionId,
            skillId: source.SkillId,
            followUpAttackPenalty: source.FollowUpAttackPenalty,
            exponentialPenalty: source.ExponentialPenalty,
            isDisadvantage: source.IsDisadvantage,
            isAdvantage: source.IsAdvantage,
            invalid: source.Invalid,
            errorId: source.ErrorId,
            errorMessage: source.ErrorMessage,
            previewText: source.PreviewText
        );
    }

    private BattleAttackCheckPolicyContext CopyContextForRepeatStage(
        BattleAttackCheckPolicyContext sourceContext,
        BattleRepeatAttackStageSpec stageSpec,
        StringName checkRoute
    )
    {
        var context = new BattleAttackCheckPolicyContext();
        if (sourceContext == null)
        {
            context.repeat_stage_spec = stageSpec;
            context.has_repeat_stage_spec = true;
            return context;
        }
        context.battle_state = sourceContext.battle_state;
        context.attacker = sourceContext.attacker;
        context.target = sourceContext.target;
        context.attacker_view = sourceContext.attacker_view;
        context.target_view = sourceContext.target_view;
        context.skill_definition = sourceContext.skill_definition;
        context.roll_kind = ROLL_KIND_REPEAT_WEAPON_STAGE;
        context.check_route = !IsEmpty(checkRoute) ? checkRoute : sourceContext.check_route;
        context.trace_source = sourceContext.trace_source;
        context.distance = sourceContext.distance;
        context.force_hit_no_crit = sourceContext.force_hit_no_crit;
        context.source_coord = sourceContext.source_coord;
        context.target_coord = sourceContext.target_coord;
        context.repeat_stage_spec = stageSpec;
        context.has_repeat_stage_spec = true;
        return context;
    }

    private List<Vector2I> CollectEndpointCoords(BattleAttackCheckPolicyContext context)
    {
        var coords = new List<Vector2I>();
        if (context == null)
        {
            return coords;
        }
        if (context.attacker_view.IsValid)
        {
            AppendUnitCoords(coords, context.attacker_view, context.source_coord);
        }
        if (context.target_view.IsValid)
        {
            AppendUnitCoords(coords, context.target_view, context.target_coord);
        }
        return coords;
    }

    private void AppendUnitCoords(
        List<Vector2I> coords,
        BattleUnitReadView unitState,
        Vector2I fallbackCoord
    )
    {
        if (!unitState.IsValid)
        {
            return;
        }
        IReadOnlyList<Vector2I> occupiedCoords = unitState.GetOccupiedCoords();
        if (occupiedCoords.Count == 0)
        {
            AppendCoordUnique(coords, fallbackCoord);
            return;
        }
        foreach (Vector2I coord in occupiedCoords)
        {
            AppendCoordUnique(coords, coord);
        }
    }

    private bool EffectCoordMatchesEndpointMode(
        Vector2I coord,
        BattleAttackRollModifierSpec spec,
        BattleAttackCheckPolicyContext context
    )
    {
        bool attackerContains = UnitContainsCoord(
            context?.attacker_view ?? default,
            coord
        );
        bool targetContains = UnitContainsCoord(
            context?.target_view ?? default,
            coord
        );
        if (spec.EndpointModeKind == BattleAttackRollModifierEndpointMode.Attacker)
        {
            return attackerContains;
        }
        if (spec.EndpointModeKind == BattleAttackRollModifierEndpointMode.Target)
        {
            return targetContains;
        }
        if (spec.EndpointModeKind == BattleAttackRollModifierEndpointMode.Both)
        {
            return attackerContains && targetContains;
        }
        return attackerContains || targetContains;
    }

    private bool UnitContainsCoord(BattleUnitReadView unitState, Vector2I coord)
    {
        if (!unitState.IsValid)
        {
            return false;
        }
        IReadOnlyList<Vector2I> occupiedCoords = unitState.GetOccupiedCoords();
        if (occupiedCoords.Count == 0)
        {
            return unitState.Coord == coord;
        }
        foreach (Vector2I occupiedCoord in occupiedCoords)
        {
            if (occupiedCoord == coord)
            {
                return true;
            }
        }
        return false;
    }

    private void AppendCoordUnique(List<Vector2I> coords, Vector2I coord)
    {
        if (coord.X < 0 || coord.Y < 0)
        {
            return;
        }
        if (coords.Contains(coord))
        {
            return;
        }
        coords.Add(coord);
    }

    private bool TeamFilterApplies(
        StringName filter,
        BattleAttackCheckPolicyContext context
    )
    {
        if (context == null)
        {
            return false;
        }
        return BattleTargetTeamRules.IsUnitValidForFilter(
            context.attacker_view,
            context.target_view,
            filter,
            default
        );
    }

    private int ResolveDistance(BattleUnitReadView activeUnit, BattleUnitReadView targetUnit)
    {
        if (!activeUnit.IsValid || !targetUnit.IsValid)
        {
            return -1;
        }
        int bestDistance = 999999;
        foreach (Vector2I activeCoord in activeUnit.GetOccupiedCoords())
        {
            foreach (Vector2I targetCoord in targetUnit.GetOccupiedCoords())
            {
                bestDistance = Math.Min(
                    bestDistance,
                    BattleGridDistanceService.GetDistance(activeCoord, targetCoord)
                );
            }
        }
        return bestDistance;
    }

    private BattleState ResolveBattleState()
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        return runtime?.GetState();
    }

    private BattleRuntimeModule ResolveRuntime()
    {
        if (_runtimeRef == null)
        {
            return null;
        }
        return _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime) ? runtime : null;
    }

    private static string BuildSortKey(BattleAttackRollModifierSpec spec)
    {
        return $"{spec.source_domain}|{spec.stack_key}|{spec.source_id}|{spec.source_instance_id}|{spec.label}";
    }

    private static int AverageStageRate(
        IReadOnlyList<AttackPreviewStage> stages,
        Func<AttackPreviewStage, int> selector
    )
    {
        if (stages == null || stages.Count == 0 || selector == null)
        {
            return 0;
        }
        int total = 0;
        foreach (AttackPreviewStage stage in stages)
        {
            total += selector(stage);
        }
        return Mathf.RoundToInt((float)total / stages.Count);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}

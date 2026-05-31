using System;
using System.Collections.Generic;
using Godot;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GSpecArray = Godot.Collections.Array<BattleAttackRollModifierSpec>;
using GStringArray = Godot.Collections.Array<string>;

[GlobalClass]
public partial class BattleAttackCheckPolicyService : RefCounted
{
    public static readonly StringName ROUTE_SKILL_ATTACK_CHECK = "skill_attack_check";
    public static readonly StringName ROUTE_SKILL_ATTACK_PREVIEW = "skill_attack_preview";
    public static readonly StringName ROUTE_REPEAT_ATTACK_STAGE_CHECK = "repeat_attack_stage_check";
    public static readonly StringName ROUTE_REPEAT_ATTACK_PREVIEW = "repeat_attack_preview";
    public static readonly StringName ROUTE_FORCE_HIT_NO_CRIT_PREVIEW = "force_hit_no_crit_preview";
    public static readonly StringName ROLL_KIND_SPELL_ATTACK = "spell_attack";
    public static readonly StringName ROLL_KIND_REPEAT_WEAPON_STAGE = "repeat_weapon_stage";
    public static readonly StringName TRACE_EXECUTE = "execute";
    public static readonly StringName TRACE_HUD_PREVIEW = "hud_preview";

    private const int RepeatAttackPreviewStageGuard = 32;
    private const string ParamAccuracyModifierSpec = "accuracy_modifier_spec";

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleHitResolver _hitResolver;
    private BattleTerrainEffectSystem _terrainEffectSystem;

    public void setup(
        BattleRuntimeModule runtime,
        BattleHitResolver hit_resolver,
        BattleTerrainEffectSystem terrain_effect_system
    )
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _hitResolver = hit_resolver;
        _terrainEffectSystem = terrain_effect_system;
    }

    public void dispose()
    {
        _runtimeRef = null;
        _hitResolver = null;
        _terrainEffectSystem = null;
    }

    public BattleAttackRollModifierBundle build_modifier_bundle(
        BattleAttackCheckPolicyContext context
    )
    {
        var bundle = new BattleAttackRollModifierBundle();
        if (context == null)
        {
            return bundle;
        }

        var filteredSpecs = new GSpecArray();
        foreach (BattleAttackRollModifierSpec candidate in CollectModifierCandidates(context))
        {
            if (ModifierApplies(candidate, context))
            {
                filteredSpecs.Add(candidate);
            }
        }

        foreach (BattleAttackRollModifierSpec spec in _resolve_stacked_specs(filteredSpecs))
        {
            bundle.add_spec(spec);
        }
        return bundle;
    }

    public BattleAttackCheckPolicyContext build_attack_context(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        StringName check_route,
        StringName trace_source,
        bool force_hit_no_crit
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_def,
            ROLL_KIND_SPELL_ATTACK,
            check_route,
            trace_source
        );
        context.force_hit_no_crit = force_hit_no_crit;
        return context;
    }

    public AttackCheckInput build_attack_check(
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

        BattleAttackRollModifierBundle modifierBundle = build_modifier_bundle(context);
        return _hitResolver.build_skill_attack_check(
            context.attacker,
            context.target,
            context.skill_def,
            flat_bonus + modifierBundle.total_bonus,
            flat_penalty + modifierBundle.total_penalty
        );
    }

    public AttackPreviewData build_attack_preview(BattleAttackCheckPolicyContext context)
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
            AttackPreviewData forcePreview = _hitResolver.build_force_hit_no_crit_attack_preview();
            context.check_route = ROUTE_FORCE_HIT_NO_CRIT_PREVIEW;
            AppendModifierBundlePayload(forcePreview, build_modifier_bundle(context));
            return forcePreview;
        }

        if (IsEmpty(context.check_route))
        {
            context.check_route = ROUTE_SKILL_ATTACK_PREVIEW;
        }

        BattleAttackRollModifierBundle modifierBundle = build_modifier_bundle(context);
        if (modifierBundle.is_empty())
        {
            return _hitResolver.build_skill_attack_preview(
                context.battle_state,
                context.attacker,
                context.target,
                context.skill_def,
                false
            );
        }

        AttackCheckInput attackCheck = build_attack_check(context, 0, 0);
        AttackCheckInput resolvedCheck = _hitResolver._build_fate_aware_attack_check_preview(
            context.battle_state,
            context.attacker,
            context.target,
            attackCheck
        );
        int successRate = resolvedCheck.SuccessRatePercent;
        int baseHitRate = resolvedCheck.BaseHitRatePercent;
        string previewText = resolvedCheck.PreviewText;

        var preview = new AttackPreviewData
        {
            SummaryText = $"预计命中率 {previewText}",
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
        };
        AppendModifierBundlePayload(preview, modifierBundle);
        return preview;
    }

    public AttackPreviewData build_repeat_attack_preview(
        BattleAttackCheckPolicyContext context,
        List<BattleRepeatAttackStageSpec> stage_specs
    )
    {
        if (
            _hitResolver == null
            || context == null
            || context.attacker == null
            || context.target == null
            || context.skill_def == null
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
        var combinedBreakdown = new Godot.Collections.Array();

        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            BattleRepeatAttackStageSpec stageSpec = stage_specs[stageIndex];
            stageSpec = stageSpec.with_fate_aware(true);
            BattleAttackCheckPolicyContext stageContext = CopyContextForRepeatStage(
                context,
                stageSpec,
                ROUTE_REPEAT_ATTACK_PREVIEW
            );
            AttackCheckInput attackCheck = build_fate_aware_repeat_attack_stage_hit_check(stageContext);
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
            foreach (Godot.Collections.Dictionary entry in build_modifier_bundle(stageContext).get_breakdown_payload())
            {
                combinedBreakdown.Add((Godot.Collections.Dictionary)entry.Duplicate(true));
            }
        }

        var successRates = new GIntArray();
        var baseHitRates = new GIntArray();
        foreach (var stage in stages)
        {
            successRates.Add(stage.SuccessRatePercent);
            baseHitRates.Add(stage.BaseHitRatePercent);
        }
        var preview = new AttackPreviewData
        {
            SummaryText = _hitResolver._format_repeat_attack_preview_summary(summaryChecks),
            Stages = stages,
            HitRatePercent = Mathf.RoundToInt(
                (float)_hitResolver._average_ints(successRates)
            ),
            SuccessRatePercent = Mathf.RoundToInt(
                (float)_hitResolver._average_ints(successRates)
            ),
            BaseHitRatePercent = Mathf.RoundToInt(
                (float)_hitResolver._average_ints(baseHitRates)
            ),
            BaseAttackBonus = stage_specs[0].stage_base_attack_bonus,
            FollowUpAttackPenalty = stage_specs[0].follow_up_attack_penalty,
        };
        if (combinedBreakdown.Count != 0)
        {
            preview.AttackRollModifierBreakdown = combinedBreakdown;
        }
        return preview;
    }

    public BattleAttackCheckPolicyContext build_repeat_attack_stage_context(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        BattleRepeatAttackStageSpec stage_spec,
        StringName check_route,
        StringName trace_source
    )
    {
        BattleAttackCheckPolicyContext context = BuildContext(
            battle_state,
            active_unit,
            target_unit,
            skill_def,
            ROLL_KIND_REPEAT_WEAPON_STAGE,
            check_route,
            trace_source
        );
        context.repeat_stage_spec = stage_spec;
        context.has_repeat_stage_spec = !stage_spec.Equals(default(BattleRepeatAttackStageSpec));
        return context;
    }

    public AttackCheckInput build_repeat_attack_stage_hit_check(BattleAttackCheckPolicyContext context)
    {
        if (_hitResolver == null || context == null || !context.has_repeat_stage_spec)
        {
            return new AttackCheckInput(invalid: true);
        }

        context.roll_kind = ROLL_KIND_REPEAT_WEAPON_STAGE;
        BattleRepeatAttackStageSpec resolvedStageSpec = context.repeat_stage_spec;
        BattleAttackRollModifierBundle modifierBundle = build_modifier_bundle(context);
        return _hitResolver.build_skill_attack_check(
            context.attacker,
            context.target,
            context.skill_def,
            resolvedStageSpec.stage_base_attack_bonus + modifierBundle.total_bonus,
            resolvedStageSpec.resolve_stage_attack_penalty() + modifierBundle.total_penalty
        );
    }

    public AttackCheckInput build_fate_aware_repeat_attack_stage_hit_check(
        BattleAttackCheckPolicyContext context
    )
    {
        if (_hitResolver == null || context == null || !context.has_repeat_stage_spec)
        {
            return new AttackCheckInput(invalid: true);
        }
        context.repeat_stage_spec = context.repeat_stage_spec.with_fate_aware(true);
        AttackCheckInput baseAttackCheck = build_repeat_attack_stage_hit_check(context);
        return _hitResolver._build_fate_aware_attack_check_preview(
            context.battle_state,
            context.attacker,
            context.target,
            baseAttackCheck
        );
    }

    public AttackRollResult roll_attack_check(BattleState battle_state, AttackCheckInput attack_check)
    {
        return _hitResolver != null
            ? _hitResolver.roll_attack_check(battle_state, attack_check)
            : new AttackRollResult();
    }

    public GSpecArray _resolve_stacked_specs(GSpecArray candidates)
    {
        var grouped = new Dictionary<string, List<BattleAttackRollModifierSpec>>();
        var order = new List<string>();
        candidates ??= new GSpecArray();

        for (int index = 0; index < candidates.Count; index++)
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

        var result = new GSpecArray();
        foreach (BattleAttackRollModifierSpec spec in resolvedSpecs)
        {
            result.Add(spec);
        }
        return result;
    }

    private BattleAttackCheckPolicyContext BuildContext(
        BattleState battleState,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        StringName rollKind,
        StringName checkRoute,
        StringName traceSource
    )
    {
        return new BattleAttackCheckPolicyContext
        {
            battle_state = battleState ?? ResolveBattleState(),
            attacker = activeUnit,
            target = targetUnit,
            skill_def = skillDef,
            roll_kind = rollKind,
            check_route = checkRoute,
            trace_source = traceSource,
            distance = ResolveDistance(activeUnit, targetUnit),
            source_coord = activeUnit != null ? activeUnit.coord : new Vector2I(-1, -1),
            target_coord = targetUnit != null ? targetUnit.coord : new Vector2I(-1, -1),
        };
    }

    private GSpecArray CollectModifierCandidates(BattleAttackCheckPolicyContext context)
    {
        var candidates = new GSpecArray();
        foreach (BattleAttackRollModifierSpec spec in CollectTerrainModifierCandidates(context))
        {
            candidates.Add(spec);
        }
        return candidates;
    }

    private GSpecArray CollectTerrainModifierCandidates(BattleAttackCheckPolicyContext context)
    {
        var candidates = new GSpecArray();
        BattleState state = context?.battle_state;
        if (state == null)
        {
            return candidates;
        }

        foreach (Vector2I coord in CollectEndpointCoords(context))
        {
            if (!state.cells.ContainsKey(coord))
            {
                continue;
            }
            BattleCellState cell = state.cells[coord].As<BattleCellState>();
            if (cell == null)
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
                GDictionary rawSpec = GetDict(
                    effectState.@params,
                    ParamAccuracyModifierSpec
                );
                if (rawSpec.Count == 0)
                {
                    continue;
                }
                BattleAttackRollModifierSpec spec = BattleAttackRollModifierSpec.from_partial_dict(
                    rawSpec
                );
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
        if (!IsEmpty(spec.applies_to) && spec.applies_to != "attack_roll")
        {
            return false;
        }
        if (spec.modifier_delta == 0)
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
        if (!TeamFilterApplies(spec.target_team_filter, context.attacker, context.target))
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
        if (hasBonus && hasPenalty)
        {
            return null;
        }

        if (first.stack_mode == "exclusive")
        {
            return group.Count == 1 ? first : null;
        }
        if (first.stack_mode == "max")
        {
            return PickMaxStackSpec(group);
        }
        if (first.stack_mode == "min")
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
        if (target == null || modifierBundle == null || modifierBundle.is_empty())
        {
            return;
        }
        target.AttackRollModifierBreakdown = (Godot.Collections.Array)modifierBundle.get_breakdown_payload();
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
        context.skill_def = sourceContext.skill_def;
        context.cast_variant = sourceContext.cast_variant;
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
        bool includeAttacker = context != null && context.attacker != null;
        bool includeTarget = context != null && context.target != null;
        if (includeAttacker)
        {
            AppendUnitCoords(coords, context.attacker, context.source_coord);
        }
        if (includeTarget)
        {
            AppendUnitCoords(coords, context.target, context.target_coord);
        }
        return coords;
    }

    private void AppendUnitCoords(
        List<Vector2I> coords,
        BattleUnitState unitState,
        Vector2I fallbackCoord
    )
    {
        if (unitState == null)
        {
            return;
        }
        unitState.refresh_footprint();
        if (unitState.occupied_coords.Count == 0)
        {
            AppendCoordUnique(coords, fallbackCoord);
            return;
        }
        foreach (Vector2I coord in unitState.occupied_coords)
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
        bool attackerContains = UnitContainsCoord(context?.attacker, coord);
        bool targetContains = UnitContainsCoord(context?.target, coord);
        if (spec.endpoint_mode == "attacker")
        {
            return attackerContains;
        }
        if (spec.endpoint_mode == "target")
        {
            return targetContains;
        }
        if (spec.endpoint_mode == "both")
        {
            return attackerContains && targetContains;
        }
        return attackerContains || targetContains;
    }

    private bool UnitContainsCoord(BattleUnitState unitState, Vector2I coord)
    {
        if (unitState == null)
        {
            return false;
        }
        unitState.refresh_footprint();
        if (unitState.occupied_coords.Count == 0)
        {
            return unitState.coord == coord;
        }
        return unitState.occupied_coords.Contains(coord);
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
        BattleUnitState attacker,
        BattleUnitState targetUnit
    )
    {
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            attacker,
            targetUnit,
            filter,
            default
        );
    }

    private int ResolveDistance(BattleUnitState activeUnit, BattleUnitState targetUnit)
    {
        if (activeUnit == null || targetUnit == null)
        {
            return -1;
        }
        return Mathf.Abs(activeUnit.coord.X - targetUnit.coord.X)
            + Mathf.Abs(activeUnit.coord.Y - targetUnit.coord.Y);
    }

    private BattleState ResolveBattleState()
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        return runtime?.get_state();
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

    private static GDictionary GetDict(GDictionary source, object key)
    {
        return TryGetValue(source, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}

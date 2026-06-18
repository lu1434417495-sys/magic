using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// 翻译自 battle_meteor_swarm_resolver.gd（2026-05-26，陨星雨 C# 迁移）。
// 单位/meteor 数据按 C# 类型直用。
internal sealed class BattleMeteorSwarmResolver
{
    private static readonly StringName PROFILE_ID = "meteor_swarm";
    private static readonly StringName COVERAGE_SHAPE_ID = "square_7x7";
    private static readonly StringName DEFAULT_SKILL_ID = "mage_meteor_swarm";
    private static readonly StringName STATUS_METEOR_CONCUSSED = "meteor_concussed";
    private static readonly StringName MITIGATION_TIER_NORMAL = "normal";
    private static readonly StringName SAVE_PROFILE_METEOR_DEX_HALF = "meteor_dex_half";
    private static readonly StringName SUMMARY_HARD_REJECT = "hard_reject";

    // 镜像 BattleDamageResolver.cs 的预览常量。

    private readonly BattleReportFormatter _reportFormatter = new();

    private BattleRuntimeModule _runtime;
    private BattleAttackCheckPolicyService _attack_check_policy_service;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleAttackCheckPolicyService attack_check_policy_service = null
    )
    {
        _runtime = runtime;
        _attack_check_policy_service = attack_check_policy_service;
    }

    internal void Dispose()
    {
        _runtime = null;
        _attack_check_policy_service = null;
    }

    internal void PopulatePreview(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.allowed = false;
        if (_runtime == null || State() == null)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        CombatCastVariantDef castVariant = ResolveGroundCastVariant(active_unit, command, skill_def);
        BattleGroundSkillValidationResult validation = _runtime.ValidateGroundSkillCommandResultTyped(
            active_unit,
            skill_def,
            castVariant,
            command
        );
        if (!validation.Allowed)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
            );
            return;
        }
        GVector2IArray targetCoords = ExtractTargetCoords(validation);
        if (targetCoords.Count == 0)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        Vector2I anchorCoord = targetCoords[0];
        MeteorSwarmCastContext context = BuildCastContextTyped(
            active_unit,
            command,
            skill_def,
            castVariant,
            anchorCoord,
            anchorCoord
        );
        MeteorSwarmPreviewFacts facts = BuildPreviewFacts(context);
        preview.allowed = true;
        preview.resolved_anchor_coord = anchorCoord;
        preview.SetTargetCoords(facts.target_coords);
        preview.SetTargetUnitIds(facts.target_unit_ids);
        preview.special_profile_preview_facts = facts;
        var hitPreview = new AttackPreviewData
        {
            SummaryText = $"陨星雨影响 {facts.impact_count} 格、预计波及 {facts.expected_target_count} 个单位。",
            Source = "special_profile_preview_facts",
        };
        hitPreview.SetAttackRollModifierBreakdown(
            facts.GetAttackRollModifierBreakdown()
        );
        preview.hit_preview = hitPreview;
        preview.AddLogLine(
            $"可施放陨星雨：影响 {facts.impact_count} 格，预计波及 {facts.expected_target_count} 个单位。"
        );
    }

    internal void PopulatePreview(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDef skill_def,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.allowed = false;
        if (_runtime == null || State() == null)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        CombatCastVariantDef castVariant = ResolveGroundCastVariant(active_unit, command, skill_def);
        BattleGroundSkillValidationResult validation = _runtime.ValidateGroundSkillCommandResultTyped(
            active_unit,
            skill_def,
            castVariant,
            command
        );
        if (!validation.Allowed)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
            );
            return;
        }
        GVector2IArray targetCoords = ExtractTargetCoords(validation);
        if (targetCoords.Count == 0)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        Vector2I anchorCoord = targetCoords[0];
        MeteorSwarmPreviewFacts facts = BuildReadOnlyPreviewFacts(
            active_unit,
            skill_def,
            anchorCoord,
            anchorCoord
        );
        preview.allowed = true;
        preview.resolved_anchor_coord = anchorCoord;
        preview.SetTargetCoords(facts.target_coords);
        preview.SetTargetUnitIds(facts.target_unit_ids);
        preview.special_profile_preview_facts = facts;
        var hitPreview = new AttackPreviewData
        {
            SummaryText = $"陨星雨影响 {facts.impact_count} 格、预计波及 {facts.expected_target_count} 个单位。",
            Source = "special_profile_preview_facts",
        };
        hitPreview.SetAttackRollModifierBreakdown(
            facts.GetAttackRollModifierBreakdown()
        );
        preview.hit_preview = hitPreview;
        preview.AddLogLine(
            $"可施放陨星雨：影响 {facts.impact_count} 格，预计波及 {facts.expected_target_count} 个单位。"
        );
    }

    internal MeteorSwarmCastContext BuildCastContextTyped(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        Vector2I nominal_anchor_coord,
        Vector2I final_anchor_coord,
        BattleSpellControlResult spell_control_context = default,
        BattleGroundBacklashTargetResult drift_context = default
    )
    {
        var context = new MeteorSwarmCastContext
        {
            active_unit = active_unit,
            command = command,
            skill_def = skill_def,
            cast_variant = cast_variant,
            profile = ResolveProfile(),
            nominal_anchor_coord = nominal_anchor_coord,
            final_anchor_coord = final_anchor_coord,
            spell_control_context = spell_control_context,
            drift_context = drift_context,
        };
        return context;
    }

    internal MeteorSwarmPreviewFacts BuildPreviewFacts(MeteorSwarmCastContext context)
    {
        MeteorSwarmTargetPlan plan = BuildTargetPlanTyped(context);
        List<MeteorSwarmNumericSummary> targetSummaries = BuildTargetNumericSummariesTyped(plan);
        List<MeteorSwarmNumericSummary> friendlyFireSummaries =
            BuildFriendlyFireNumericSummariesTyped(plan);
        var facts = new MeteorSwarmPreviewFacts
        {
            profile_id = PROFILE_ID,
            skill_id = plan.skill_id,
            preview_fact_id = new StringName($"meteor_swarm:{plan.nominal_plan_signature}"),
            nominal_plan_signature = plan.nominal_plan_signature,
            final_plan_signature = plan.final_plan_signature,
            resolved_anchor_coord = plan.final_anchor_coord,
            target_unit_ids = new List<StringName>(plan.target_unit_ids),
            target_coords = new List<Vector2I>(plan.affected_coords),
            terrain_summary = BuildTerrainSummaryTyped(plan),
            target_numeric_summaries = targetSummaries,
            friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>(
                friendlyFireSummaries
            ),
            friendly_fire_numeric_summaries = friendlyFireSummaries,
            attack_roll_modifier_breakdown = BuildFutureAttackRollModifierBreakdownTyped(plan),
            impact_count = plan.affected_coords.Count,
            expected_target_count = plan.target_unit_ids.Count,
        };
        facts.expected_terrain_effect_count = _count_expected_terrain_effects(plan);
        facts.friendly_fire_risk_percent = ResolveFriendlyFireRiskPercent(friendlyFireSummaries);
        facts.component_preview = BuildComponentPreviewTyped(plan);
        return facts;
    }

    private MeteorSwarmPreviewFacts BuildReadOnlyPreviewFacts(
        BattleUnitReadView sourceUnit,
        SkillDef skillDef,
        Vector2I nominalAnchorCoord,
        Vector2I finalAnchorCoord
    )
    {
        MeteorSwarmTargetPlan plan = BuildReadOnlyTargetPlan(
            sourceUnit,
            skillDef,
            nominalAnchorCoord,
            finalAnchorCoord
        );
        List<MeteorSwarmNumericSummary> targetSummaries =
            BuildReadOnlyTargetNumericSummaries(plan, sourceUnit);
        List<MeteorSwarmNumericSummary> friendlyFireSummaries =
            BuildReadOnlyFriendlyFireNumericSummaries(targetSummaries);
        var facts = new MeteorSwarmPreviewFacts
        {
            profile_id = PROFILE_ID,
            skill_id = plan.skill_id,
            preview_fact_id = new StringName($"meteor_swarm:{plan.nominal_plan_signature}"),
            nominal_plan_signature = plan.nominal_plan_signature,
            final_plan_signature = plan.final_plan_signature,
            resolved_anchor_coord = plan.final_anchor_coord,
            target_unit_ids = new List<StringName>(plan.target_unit_ids),
            target_coords = new List<Vector2I>(plan.affected_coords),
            terrain_summary = BuildTerrainSummaryTyped(plan),
            target_numeric_summaries = targetSummaries,
            friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>(
                friendlyFireSummaries
            ),
            friendly_fire_numeric_summaries = friendlyFireSummaries,
            attack_roll_modifier_breakdown = BuildFutureAttackRollModifierBreakdownTyped(plan),
            impact_count = plan.affected_coords.Count,
            expected_target_count = plan.target_unit_ids.Count,
        };
        facts.expected_terrain_effect_count = _count_expected_terrain_effects(plan);
        facts.friendly_fire_risk_percent = ResolveFriendlyFireRiskPercent(friendlyFireSummaries);
        facts.component_preview = BuildComponentPreviewTyped(plan);
        return facts;
    }

    private MeteorSwarmTargetPlan BuildReadOnlyTargetPlan(
        BattleUnitReadView sourceUnit,
        SkillDef skillDef,
        Vector2I nominalAnchorCoord,
        Vector2I finalAnchorCoord
    )
    {
        MeteorSwarmProfile profile = ResolveProfile();
        var plan = new MeteorSwarmTargetPlan
        {
            profile = profile,
            source_unit_id = sourceUnit.IsValid ? sourceUnit.UnitId : new StringName(""),
            skill_def = skillDef,
            skill_id = skillDef != null ? skillDef.skill_id : DEFAULT_SKILL_ID,
            nominal_anchor_coord = nominalAnchorCoord,
            final_anchor_coord = finalAnchorCoord,
            coverage_shape_id = profile != null ? profile.coverage_shape_id : COVERAGE_SHAPE_ID,
            radius = profile != null ? profile.radius : 3,
        };
        BattleState state = State();
        if (_runtime == null || state == null || finalAnchorCoord == new Vector2I(-1, -1))
        {
            plan.nominal_plan_signature = BuildPlanSignature(plan, nominalAnchorCoord);
            plan.final_plan_signature = BuildPlanSignature(plan, finalAnchorCoord);
            return plan;
        }

        BattleGridService gridService = GridService();
        var seenUnitIds = new HashSet<StringName>();
        for (int dy = -plan.radius; dy <= plan.radius; dy++)
        {
            for (int dx = -plan.radius; dx <= plan.radius; dx++)
            {
                Vector2I coord = finalAnchorCoord + new Vector2I(dx, dy);
                if (!gridService.IsInside(state, coord))
                {
                    continue;
                }
                int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                plan.affected_coords.Add(coord);
                plan.ring_by_coord[coord] = ring;
                BattleCellState cell = gridService.GetCellState(state, coord);
                StringName occupantId =
                    cell != null
                        ? cell.occupant_unit_id
                        : new StringName("");
                if (
                    cell == null
                    || StringNameIsEmpty(occupantId)
                    || seenUnitIds.Contains(occupantId)
                )
                {
                    continue;
                }
                BattleUnitState unitState = UnitById(occupantId);
                if (unitState == null || !unitState.is_alive)
                {
                    continue;
                }
                seenUnitIds.Add(occupantId);
                plan.target_unit_ids.Add(occupantId);
                plan.unit_primary_coord_by_id[occupantId] = coord;
            }
        }
        SortCoordListAscending(plan.affected_coords);
        SortUnitIdsByPrimaryCoord(plan);
        PopulateUnitDistances(plan);
        plan.nominal_plan_signature = BuildPlanSignatureForAnchor(
            plan,
            plan.nominal_anchor_coord
        );
        plan.final_plan_signature = BuildPlanSignatureForAnchor(plan, plan.final_anchor_coord);
        return plan;
    }

    internal MeteorSwarmTargetPlan BuildTargetPlanTyped(MeteorSwarmCastContext context)
    {
        var plan = new MeteorSwarmTargetPlan();
        MeteorSwarmProfile profile =
            context != null && context.profile != null ? context.profile : ResolveProfile();
        plan.profile = profile;
        plan.coverage_shape_id = profile != null ? profile.coverage_shape_id : COVERAGE_SHAPE_ID;
        plan.radius = profile != null ? profile.radius : 3;
        plan.source_unit = context != null ? context.active_unit : null;
        plan.source_unit_id =
            plan.source_unit != null ? plan.source_unit.unit_id : new StringName("");
        plan.skill_def = context != null ? context.skill_def : null;
        plan.skill_id =
            plan.skill_def != null ? plan.skill_def.skill_id : DEFAULT_SKILL_ID;
        plan.nominal_anchor_coord =
            context != null ? context.nominal_anchor_coord : new Vector2I(-1, -1);
        plan.final_anchor_coord =
            context != null ? context.final_anchor_coord : new Vector2I(-1, -1);
        plan.drift_applied = context != null && context.HasDrift();
        plan.drift_from_coord = plan.drift_applied
            ? plan.nominal_anchor_coord
            : new Vector2I(-1, -1);
        BattleState state = State();
        if (_runtime == null || state == null || plan.final_anchor_coord == new Vector2I(-1, -1))
        {
            plan.nominal_plan_signature = BuildPlanSignature(plan, plan.nominal_anchor_coord);
            plan.final_plan_signature = BuildPlanSignature(plan, plan.final_anchor_coord);
            return plan;
        }

        BattleGridService gridService = GridService();
        var seenUnitIds = new HashSet<StringName>();
        for (int dy = -plan.radius; dy <= plan.radius; dy++)
        {
            for (int dx = -plan.radius; dx <= plan.radius; dx++)
            {
                Vector2I coord = plan.final_anchor_coord + new Vector2I(dx, dy);
                if (!gridService.IsInside(state, coord))
                {
                    continue;
                }
                int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                plan.affected_coords.Add(coord);
                plan.ring_by_coord[coord] = ring;
                BattleCellState cell = gridService.GetCellState(state, coord);
                StringName occupantId =
                    cell != null
                        ? cell.occupant_unit_id
                        : new StringName("");
                if (
                    cell == null
                    || StringNameIsEmpty(occupantId)
                    || seenUnitIds.Contains(occupantId)
                )
                {
                    continue;
                }
                BattleUnitState unitState = UnitById(occupantId);
                if (unitState == null || !unitState.is_alive)
                {
                    continue;
                }
                seenUnitIds.Add(occupantId);
                plan.target_unit_ids.Add(occupantId);
                plan.unit_primary_coord_by_id[occupantId] = coord;
            }
        }
        SortCoordListAscending(plan.affected_coords);
        SortUnitIdsByPrimaryCoord(plan);
        PopulateUnitDistances(plan);
        plan.nominal_plan_signature = BuildPlanSignatureForAnchor(
            plan,
            plan.nominal_anchor_coord
        );
        plan.final_plan_signature = BuildPlanSignatureForAnchor(plan, plan.final_anchor_coord);
        return plan;
    }

    internal MeteorSwarmCommitResult ResolveTyped(MeteorSwarmTargetPlan plan)
    {
        var result = new MeteorSwarmCommitResult();
        if (plan == null || plan.profile == null || _runtime == null || State() == null)
        {
            return result;
        }
        result.plan = plan;
        result.AddChangedUnitId(plan.source_unit_id);
        List<MeteorSwarmTerrainEffectFact> terrainEffects = ApplyTerrainEffects(plan);
        foreach (MeteorSwarmTerrainEffectFact terrainEffect in terrainEffects)
        {
            result.terrain_effects.Add(terrainEffect);
            Vector2I terrainCoord = terrainEffect?.coord ?? new Vector2I(-1, -1);
            if (terrainCoord != new Vector2I(-1, -1))
            {
                result.AddChangedCoord(terrainCoord);
            }
        }
        var componentTotals = new Dictionary<StringName, MeteorSwarmComponentFact>();
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitState targetUnit = UnitById(targetUnitId);
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            MeteorSwarmTargetOutcome targetOutcome = ResolveTarget(
                plan,
                targetUnit,
                componentTotals
            );
            result.target_outcomes.Add(targetOutcome);
            result.total_damage += targetOutcome.total_damage;
            result.total_healing += targetOutcome.total_healing;
            result.AddChangedUnitId(targetUnit.unit_id);
            foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
            {
                result.AddChangedCoord(occupiedCoord);
            }
            if (targetOutcome.defeated)
            {
                result.AddDefeatedUnitId(targetUnit.unit_id);
            }
        }
        foreach (Vector2I terrainCoord in plan.affected_coords)
        {
            result.AddChangedCoord(terrainCoord);
        }
        result.AddReportEntry(_build_report_entry(plan, result, componentTotals));
        result.log_lines.Add(
            $"{(plan.source_unit != null ? plan.source_unit.display_name : "单位")} 施放陨星雨，灾害区覆盖 {plan.affected_coords.Count} 格，波及 {result.target_outcomes.Count} 个单位，造成 {result.total_damage} 点总伤害。"
        );
        MeteorSwarmTerrainSummaryFact terrainSummary = BuildTerrainSummaryTyped(plan);
        result.log_lines.Add(
            $"陨击留下地形：陨坑 {terrainSummary.crater_count} 格，碎石 {terrainSummary.rubble_count} 格，尘土 {terrainSummary.dust_count} 格。"
        );
        return result;
    }

    private MeteorSwarmTargetOutcome ResolveTarget(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit,
        Dictionary<StringName, MeteorSwarmComponentFact> component_totals
    )
    {
        var outcome = new MeteorSwarmTargetOutcome
        {
            target_unit_id = target_unit.unit_id,
            target_coord = plan.GetPrimaryCoordForUnit(target_unit.unit_id),
            target_faction_id = target_unit.faction_id,
            distance_from_anchor = plan.GetDistanceForUnit(target_unit.unit_id),
        };
        bool coversCenter = UnitCoversCoord(target_unit, plan.final_anchor_coord);
        foreach (MeteorSwarmImpactComponent component in plan.profile.impact_components)
        {
            if (
                component == null
                || !component.AppliesToDistance(outcome.distance_from_anchor, coversCenter)
            )
            {
                continue;
            }
            CombatEffectDef effectDef = _build_damage_effect_def(
                component,
                outcome.distance_from_anchor
            );
            var damageContext = new GDictionary
            {
                ["skill_id"] = plan.skill_id,
                ["meteor_component_id"] = component.component_id,
                ["meteor_role_label"] = component.role_label,
                ["dispatch_events"] = false,
            };
            AttackEffectResolutionResult damageResolution = DamageResolver()
                .ResolveEffects(
                    plan.source_unit,
                    target_unit,
                    new GArray { effectDef },
                    damageContext
                );
            outcome.AddComponent(component);
            outcome.total_damage += damageResolution.Damage;
            outcome.total_healing += damageResolution.Healing;
            foreach (DamageEventResult damageEvent in damageResolution.DamageEvents)
            {
                outcome.damage_events.Add(damageEvent);
            }
            MeteorSwarmComponentFact componentFact = BuildComponentReportFactTyped(
                component,
                damageResolution,
                outcome.distance_from_anchor
            );
            outcome.report_component_breakdown.Add(componentFact);
            AddComponentTotalTyped(component_totals, componentFact);
        }
        if (
            outcome.distance_from_anchor <= 1
            && !StringNameIsEmpty(plan.profile.concussed_status_id)
            && target_unit.is_alive
        )
        {
            AttackEffectResolutionResult statusResult = _apply_concussed_status(
                plan,
                target_unit
            );
            foreach (StringName statusId in statusResult.StatusEffectIds ?? new GStringNameArray())
            {
                outcome.AddStatusEffectId(statusId);
            }
        }
        target_unit.SetCurrentHp(target_unit.current_hp);
        outcome.defeated = !target_unit.is_alive;
        return outcome;
    }

    private List<MeteorSwarmTerrainEffectFact> ApplyTerrainEffects(MeteorSwarmTargetPlan plan)
    {
        var effects = new List<MeteorSwarmTerrainEffectFact>();
        BattleState state = State();
        BattleTerrainEffectSystem terrainSystem = _runtime?._terrain_effect_system;
        if (state == null || terrainSystem == null)
        {
            return effects;
        }
        foreach (Vector2I coord in plan.affected_coords)
        {
            int ring = plan.GetRingForCoord(coord);
            foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, ring))
            {
                CombatEffectDef effectDef = _build_terrain_effect_def(terrainProfile);
                StringName fieldInstanceId = _runtime._build_terrain_effect_instance_id(
                    effectDef.terrain_effect_id
                );
                if (
                    !terrainSystem.UpsertTimedTerrainEffect(
                        coord,
                        plan.source_unit,
                        plan.skill_def,
                        effectDef,
                        fieldInstanceId
                    )
                )
                {
                    continue;
                }
                effects.Add(
                    new MeteorSwarmTerrainEffectFact
                    {
                        coord = coord,
                        ring = ring,
                        terrain_profile_id = DictStringName(
                            terrainProfile,
                            "terrain_profile_id"
                        ),
                        terrain_effect_id = effectDef.terrain_effect_id,
                        lifetime_policy = effectDef.lifetime_policy,
                        move_cost_delta = effectDef.move_cost_delta,
                        render_overlay_id = effectDef.render_overlay_id.ToString(),
                    }
                );
            }
        }
        return effects;
    }

    internal CombatEffectDef _build_damage_effect_def(
        MeteorSwarmImpactComponent component,
        int distance_from_anchor
    )
    {
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = component.damage_tag,
            effect_target_team_filter = "any",
            power = component.base_power,
            dice_count = component.dice_count,
            dice_sides = component.dice_sides,
            pre_resistance_damage_multiplier = component.GetDamageScale(distance_from_anchor),
        };
        ApplySaveProfileToDamageEffect(effect, component);
        return effect;
    }

    internal CombatEffectDef _build_terrain_effect_def(GDictionary terrain_profile)
    {
        var effect = new CombatEffectDef();
        StringName terrainProfileId = ProgressionDataUtils.to_string_name(
            DictStringName(terrain_profile, "terrain_profile_id")
        );
        effect.effect_type = "terrain_effect";
        effect.tick_effect_type = ProgressionDataUtils.to_string_name(
            DictStringName(terrain_profile, "tick_effect_type", "none")
        );
        effect.lifetime_policy = DictStringName(
            terrain_profile,
            "lifetime_policy",
            "timed"
        );
        effect.move_cost_delta = DictInt(terrain_profile, "move_cost_delta", 0);
        effect.render_overlay_id = DictStringName(terrain_profile, "render_overlay_id");
        effect.overlay_priority = DictInt(terrain_profile, "overlay_priority", 0);
        effect.display_name = TerrainProfileDisplayName(terrainProfileId);
        effect.terrain_effect_id = terrainProfileId;
        effect.duration_tu = DictInt(terrain_profile, "duration_tu", 0);
        effect.tick_interval_tu = DictInt(terrain_profile, "tick_interval_tu", 0);
        effect.stack_behavior = "refresh";
        effect.effect_target_team_filter = "any";
        effect.@params = new GDictionary
        {
            ["move_cost_stack_key"] = DictStringName(
                terrain_profile,
                "move_cost_stack_key",
                ""
            ),
            ["move_cost_stack_mode"] = DictStringName(
                terrain_profile,
                "move_cost_stack_mode",
                ""
            ),
        };
        effect.accuracy_modifier_spec = BuildAccuracyModifierSpec(terrain_profile);
        return effect;
    }

    internal AttackEffectResolutionResult _apply_concussed_status(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit
    )
    {
        var effect = new CombatEffectDef
        {
            effect_type = "apply_status",
            status_id = plan.profile.concussed_status_id,
            power = 1,
            duration_tu = 60,
            attack_roll_penalty = 2,
        };
        return DamageResolver()
            .ResolveEffects(
                plan.source_unit,
                target_unit,
                new GArray { effect },
                new GDictionary
                {
                    ["skill_id"] = plan.skill_id,
                    ["meteor_component_id"] = STATUS_METEOR_CONCUSSED,
                }
            );
    }

    internal GDictionary _build_report_entry(
        MeteorSwarmTargetPlan plan,
        MeteorSwarmCommitResult result,
        Dictionary<StringName, MeteorSwarmComponentFact> component_totals
    )
    {
        var componentBreakdown = new GDictArray();
        foreach (string componentKey in ProgressionDataUtils.sorted_string_keys(ToComponentTotalsDictionary(component_totals)))
        {
            StringName normalizedComponentId = ProgressionDataUtils.to_string_name(componentKey);
            if (
                StringNameIsEmpty(normalizedComponentId)
                || !component_totals.TryGetValue(
                    normalizedComponentId,
                    out MeteorSwarmComponentFact entryFact
                )
                || entryFact == null
            )
            {
                continue;
            }
            componentBreakdown.Add(entryFact.ToDictionary());
        }
        var targetSummaries = new GDictArray();
        foreach (MeteorSwarmTargetOutcome targetOutcome in result.target_outcomes)
        {
            if (targetOutcome != null)
            {
                targetSummaries.Add(targetOutcome.ToSummaryDictionary());
            }
        }
        var entry = new GDictionary
        {
            ["entry_type"] = "meteor_swarm_impact_summary",
            ["skill_id"] = plan.skill_id.ToString(),
            ["source_unit_id"] = plan.source_unit_id.ToString(),
            ["anchor_coord"] = plan.final_anchor_coord,
            ["nominal_anchor_coord"] = plan.nominal_anchor_coord,
            ["nominal_plan_signature"] = plan.nominal_plan_signature,
            ["final_plan_signature"] = plan.final_plan_signature,
            ["target_count"] = result.target_outcomes.Count,
            ["terrain_effect_count"] = result.terrain_effects.Count,
            ["total_damage"] = result.total_damage,
            ["defeated_count"] = result.defeated_unit_ids.Count,
            ["component_breakdown"] = componentBreakdown,
            ["target_summaries"] = targetSummaries,
            ["terrain_summary"] = BuildTerrainSummaryTyped(plan).ToDictionary(),
        };
        GStringArray summaryLines = _reportFormatter.FormatMeteorSwarmSummary(entry);
        if (summaryLines.Count != 0)
        {
            entry["text"] = summaryLines[0];
        }
        return entry;
    }

    private MeteorSwarmComponentFact BuildComponentReportFactTyped(
        MeteorSwarmImpactComponent component,
        AttackEffectResolutionResult damage_result,
        int distance_from_anchor
    )
    {
        return new MeteorSwarmComponentFact
        {
            component_id = component.component_id,
            role_label = component.role_label,
            damage_tag = component.damage_tag,
            distance_from_anchor = distance_from_anchor,
            damage = damage_result.Damage,
            healing = damage_result.Healing,
        };
    }

    private void AddComponentTotalTyped(
        Dictionary<StringName, MeteorSwarmComponentFact> component_totals,
        MeteorSwarmComponentFact component_fact
    )
    {
        StringName componentId = component_fact?.component_id ?? "";
        if (StringNameIsEmpty(componentId))
        {
            return;
        }
        MeteorSwarmComponentFact existing =
            component_totals.TryGetValue(componentId, out MeteorSwarmComponentFact current)
                ? current
                : new MeteorSwarmComponentFact
                {
                    component_id = componentId,
                    role_label = component_fact.role_label,
                    damage_tag = component_fact.damage_tag,
                };
        existing.damage += component_fact?.damage ?? 0;
        existing.healing += component_fact?.healing ?? 0;
        component_totals[componentId] = existing;
    }

    private MeteorSwarmTerrainSummaryFact BuildTerrainSummaryTyped(MeteorSwarmTargetPlan plan)
    {
        int craterCount = 0;
        int rubbleCount = 0;
        int dustCount = 0;
        int terrainEffectCount = 0;
        if (plan == null || plan.profile == null)
        {
            return new MeteorSwarmTerrainSummaryFact();
        }
        foreach (Vector2I coord in plan.affected_coords)
        {
            int ring = plan.GetRingForCoord(coord);
            foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, ring))
            {
                terrainEffectCount += 1;
                string profileId = DictString(
                    terrainProfile,
                    "terrain_profile_id"
                );
                if (profileId.Contains("crater"))
                {
                    craterCount += 1;
                }
                if (profileId.Contains("rubble"))
                {
                    rubbleCount += 1;
                }
                if (profileId.Contains("dust"))
                {
                    dustCount += 1;
                }
            }
        }
        return new MeteorSwarmTerrainSummaryFact
        {
            coverage_shape_id = plan.coverage_shape_id,
            radius = plan.radius,
            affected_coord_count = plan.affected_coords.Count,
            terrain_effect_count = terrainEffectCount,
            crater_count = craterCount,
            rubble_count = rubbleCount,
            dust_count = dustCount,
        };
    }

    internal GDictArray _build_friendly_fire_numeric_summary(MeteorSwarmTargetPlan plan)
    {
        return MeteorSwarmNumericSummary.ToDictionaryArray(
            BuildFriendlyFireNumericSummariesTyped(plan)
        );
    }

    private List<MeteorSwarmNumericSummary> BuildFriendlyFireNumericSummariesTyped(
        MeteorSwarmTargetPlan plan
    )
    {
        var summaries = new List<MeteorSwarmNumericSummary>();
        if (plan == null || _runtime == null || State() == null)
        {
            return summaries;
        }
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitState targetUnit = UnitById(targetUnitId);
            if (targetUnit == null || plan.source_unit == null)
            {
                continue;
            }
            if (targetUnit.faction_id != plan.source_unit.faction_id)
            {
                continue;
            }
            summaries.Add(BuildFriendlyFireSummaryForUnitTyped(plan, targetUnit));
        }
        return summaries;
    }

    internal GDictArray _build_target_numeric_summary(MeteorSwarmTargetPlan plan)
    {
        return MeteorSwarmNumericSummary.ToDictionaryArray(BuildTargetNumericSummariesTyped(plan));
    }

    private List<MeteorSwarmNumericSummary> BuildTargetNumericSummariesTyped(
        MeteorSwarmTargetPlan plan
    )
    {
        var summaries = new List<MeteorSwarmNumericSummary>();
        if (plan == null || _runtime == null || State() == null)
        {
            return summaries;
        }
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitState targetUnit = UnitById(targetUnitId);
            if (targetUnit == null)
            {
                continue;
            }
            summaries.Add(BuildFriendlyFireSummaryForUnitTyped(plan, targetUnit));
        }
        return summaries;
    }

    private List<MeteorSwarmNumericSummary> BuildReadOnlyTargetNumericSummaries(
        MeteorSwarmTargetPlan plan,
        BattleUnitReadView sourceUnit
    )
    {
        var summaries = new List<MeteorSwarmNumericSummary>();
        BattleState state = State();
        if (plan == null || _runtime == null || state == null)
        {
            return summaries;
        }
        BattleStateReadView stateView = state.AsReadView();
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitReadView targetUnit = stateView.GetUnit(targetUnitId);
            if (!targetUnit.IsValid)
            {
                continue;
            }
            summaries.Add(BuildReadOnlySummaryForUnit(plan, sourceUnit, targetUnit));
        }
        return summaries;
    }

    private List<MeteorSwarmNumericSummary> BuildReadOnlyFriendlyFireNumericSummaries(
        IReadOnlyList<MeteorSwarmNumericSummary> targetSummaries
    )
    {
        var summaries = new List<MeteorSwarmNumericSummary>();
        foreach (MeteorSwarmNumericSummary summary in targetSummaries ?? Array.Empty<MeteorSwarmNumericSummary>())
        {
            if (summary?.IsAlly == true)
            {
                summaries.Add(summary);
            }
        }
        return summaries;
    }

    private MeteorSwarmNumericSummary BuildReadOnlySummaryForUnit(
        MeteorSwarmTargetPlan plan,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit
    )
    {
        int distance = plan.GetDistanceForUnit(targetUnit.UnitId);
        bool coversCenter = UnitCoversCoord(targetUnit, plan.final_anchor_coord);
        var componentBreakdown = new List<MeteorSwarmComponentBreakdownEntry>();
        int expectedDamage = 0;
        int worstCaseDamage = 0;
        int expectedShieldRemaining = targetUnit.CurrentShieldHp;
        int worstShieldRemaining = targetUnit.CurrentShieldHp;
        var resistanceTiers = new Dictionary<StringName, StringName>();

        foreach (MeteorSwarmImpactComponent component in plan.profile?.impact_components ?? new Godot.Collections.Array<MeteorSwarmImpactComponent>())
        {
            if (component == null || !component.AppliesToDistance(distance, coversCenter))
            {
                continue;
            }
            int preSaveExpectedDamage = component.GetAverageBaseDamage(distance);
            int preSaveWorstDamage = component.GetWorstCaseBaseDamage(distance);
            int expectedComponentDamage = EstimatePostSaveDamage(component, preSaveExpectedDamage);
            int worstComponentDamage = EstimateWorstPostSaveDamage(component, preSaveWorstDamage);
            int expectedAfterShield = ApplyShieldEstimate(
                expectedComponentDamage,
                ref expectedShieldRemaining
            );
            int worstAfterShield = ApplyShieldEstimate(
                worstComponentDamage,
                ref worstShieldRemaining
            );
            expectedDamage += expectedAfterShield;
            worstCaseDamage += worstAfterShield;
            if (component.damage_tag != "")
            {
                resistanceTiers[component.damage_tag] = MITIGATION_TIER_NORMAL;
            }
            componentBreakdown.Add(
                new MeteorSwarmComponentBreakdownEntry
                {
                    ComponentId = component.component_id,
                    RoleLabel = component.role_label,
                    DamageTag = component.damage_tag,
                    ExpectedDamage = expectedAfterShield,
                    WorstCaseDamage = worstAfterShield,
                    PostSaveExpectedDamage = expectedComponentDamage,
                    PostSaveWorstCaseDamage = worstComponentDamage,
                    PreSaveExpectedDamage = preSaveExpectedDamage,
                    PreSaveWorstCaseDamage = preSaveWorstDamage,
                    ResistanceTier = MITIGATION_TIER_NORMAL,
                    SaveProfileId = component.save_profile_id.ToString(),
                    SaveEstimate = BuildReadOnlySaveEstimate(component, preSaveExpectedDamage),
                    WorstSaveEstimate = BuildReadOnlyWorstSaveEstimate(component, preSaveWorstDamage),
                    ShieldAbsorbedEstimate = Math.Max(
                        expectedComponentDamage - expectedAfterShield,
                        0
                    ),
                    ShieldAbsorbedWorst = Math.Max(worstComponentDamage - worstAfterShield, 0),
                }
            );
        }

        var statusEffectIds = new List<StringName>();
        int apPenalty = 0;
        if (distance <= 1)
        {
            if (plan.profile?.concussed_status_id != "")
            {
                statusEffectIds.Add(plan.profile.concussed_status_id);
            }
            apPenalty = 1;
        }
        int maxHp = GetUnitMaxHp(targetUnit);
        int currentHp = Math.Max(targetUnit.CurrentHp, 1);
        int expectedHpPercent = Mathf.RoundToInt(
            (float)expectedDamage * 100.0f / Math.Max(maxHp, 1)
        );
        int worstHpPercent = Mathf.RoundToInt((float)worstCaseDamage * 100.0f / Math.Max(maxHp, 1));
        bool hardReject =
            worstCaseDamage >= currentHp
            || expectedHpPercent >= plan.profile.friendly_fire_hard_expected_hp_percent
            || worstHpPercent >= plan.profile.friendly_fire_hard_worst_case_hp_percent;
        bool isAlly =
            sourceUnit.IsValid
            && targetUnit.IsValid
            && targetUnit.FactionId == sourceUnit.FactionId;
        return new MeteorSwarmNumericSummary
        {
            CandidateAnchorCoord = plan.final_anchor_coord,
            TargetUnitId = targetUnit.UnitId,
            AllyUnitId = targetUnit.UnitId,
            TargetFactionId = targetUnit.FactionId,
            IsAlly = isAlly,
            DistanceFromAnchor = distance,
            ComponentExpectedDamage = expectedDamage,
            ComponentWorstCaseDamage = worstCaseDamage,
            ComponentBreakdown = componentBreakdown,
            LethalProbabilityPercent = worstCaseDamage >= currentHp ? 100 : 0,
            SaveProfileIds = CollectComponentSaveProfileIds(componentBreakdown),
            ResistanceTiersByDamageTag = resistanceTiers,
            ShieldHp = targetUnit.CurrentShieldHp,
            GuardBlockEstimate = 0,
            StatusEffectIds = statusEffectIds,
            ApPenalty = apPenalty,
            HostileTerrain = BuildHostileTerrainConsequenceTyped(plan, distance),
            ExpectedDamageHpPercent = expectedHpPercent,
            WorstCaseDamageHpPercent = worstHpPercent,
            HardReject = hardReject,
            SoftPenalty =
                !hardReject
                && expectedHpPercent > plan.profile.friendly_fire_soft_expected_hp_percent,
        };
    }

    internal GDictionary _build_friendly_fire_summary_for_unit(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit
    )
    {
        return BuildFriendlyFireSummaryForUnitTyped(plan, target_unit).ToDictionary();
    }

    private MeteorSwarmNumericSummary BuildFriendlyFireSummaryForUnitTyped(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit
    )
    {
        int distance = plan.GetDistanceForUnit(target_unit.unit_id);
        bool coversCenter = UnitCoversCoord(target_unit, plan.final_anchor_coord);
        var componentBreakdown = new List<MeteorSwarmComponentBreakdownEntry>();
        int expectedDamage = 0;
        int worstCaseDamage = 0;
        BattleUnitState expectedSourcePreview =
            plan.source_unit != null ? plan.source_unit.clone() : null;
        BattleUnitState worstSourcePreview =
            plan.source_unit != null ? plan.source_unit.clone() : null;
        BattleUnitState expectedTargetPreview = target_unit.clone();
        BattleUnitState worstTargetPreview = target_unit.clone();
        var resistanceTiers = new Dictionary<StringName, StringName>();
        int guardBlockEstimate = 0;
        foreach (MeteorSwarmImpactComponent component in plan.profile.impact_components)
        {
            if (component == null || !component.AppliesToDistance(distance, coversCenter))
            {
                continue;
            }
            CombatEffectDef effectDef = _build_damage_effect_def(component, distance);
            BattleDamagePreviewResult expectedPreview = BuildComponentDamagePreview(
                plan,
                expectedSourcePreview,
                expectedTargetPreview,
                effectDef,
                BattleDamagePreviewRollMode.Average,
                BattleDamagePreviewSaveMode.Expected
            );
            BattleDamagePreviewResult worstPreview = BuildComponentDamagePreview(
                plan,
                worstSourcePreview,
                worstTargetPreview,
                effectDef,
                BattleDamagePreviewRollMode.Maximum,
                BattleDamagePreviewSaveMode.Worst
            );
            IReadOnlyDictionary<string, object> expectedOutcome =
                expectedPreview?.DamageOutcome
                ?? new Dictionary<string, object>(StringComparer.Ordinal);
            StringName resistanceTier = ReadPreviewOutcomeStringName(
                expectedOutcome,
                "mitigation_tier",
                MITIGATION_TIER_NORMAL
            );
            if (component.damage_tag != "" && resistanceTier != "")
            {
                resistanceTiers[component.damage_tag] = resistanceTier;
            }
            guardBlockEstimate = Math.Max(
                guardBlockEstimate,
                ReadPreviewOutcomeInt(expectedOutcome, "guard_block", 0)
            );
            int preSaveExpectedDamage = expectedPreview?.PreSaveDamage ?? 0;
            int preSaveWorstDamage = worstPreview?.PreSaveDamage ?? 0;
            int expectedComponentDamage =
                expectedPreview?.PostSaveDamage ?? preSaveExpectedDamage;
            int worstComponentDamage = worstPreview?.PostSaveDamage ?? preSaveWorstDamage;
            int expectedAfterShield = expectedPreview?.HpDamage ?? expectedComponentDamage;
            int worstAfterShield = worstPreview?.HpDamage ?? worstComponentDamage;
            expectedDamage += expectedAfterShield;
            worstCaseDamage += worstAfterShield;
            BattleUnitState nextExpectedSource = expectedPreview?.SourcePreviewAfter;
            BattleUnitState nextExpectedTarget = expectedPreview?.TargetPreviewAfter;
            BattleUnitState nextWorstSource = worstPreview?.SourcePreviewAfter;
            BattleUnitState nextWorstTarget = worstPreview?.TargetPreviewAfter;
            if (nextExpectedSource != null)
            {
                expectedSourcePreview = nextExpectedSource;
            }
            if (nextExpectedTarget != null)
            {
                expectedTargetPreview = nextExpectedTarget;
            }
            if (nextWorstSource != null)
            {
                worstSourcePreview = nextWorstSource;
            }
            if (nextWorstTarget != null)
            {
                worstTargetPreview = nextWorstTarget;
            }
            componentBreakdown.Add(
                new MeteorSwarmComponentBreakdownEntry
                {
                    ComponentId = component.component_id,
                    RoleLabel = component.role_label,
                    DamageTag = component.damage_tag,
                    ExpectedDamage = expectedAfterShield,
                    WorstCaseDamage = worstAfterShield,
                    PostSaveExpectedDamage = expectedComponentDamage,
                    PostSaveWorstCaseDamage = worstComponentDamage,
                    PreSaveExpectedDamage = preSaveExpectedDamage,
                    PreSaveWorstCaseDamage = preSaveWorstDamage,
                    ResistanceTier = resistanceTier,
                    SaveProfileId = component.save_profile_id.ToString(),
                    SaveEstimate = expectedPreview?.SaveEstimate
                        ?? BattleDamagePreviewSaveEstimate.None(preSaveExpectedDamage),
                    WorstSaveEstimate = worstPreview?.SaveEstimate
                        ?? BattleDamagePreviewSaveEstimate.None(preSaveWorstDamage),
                    HalfSourceLabels = ReadMitigationLabels(
                        expectedOutcome,
                        MitigationTierKind.Half
                    ),
                    DoubleSourceLabels = ReadMitigationLabels(
                        expectedOutcome,
                        MitigationTierKind.Double
                    ),
                    ImmuneSourceLabels = ReadMitigationLabels(
                        expectedOutcome,
                        MitigationTierKind.Immune
                    ),
                    FixedMitigationSourceLabels = new List<string>(
                        ReadFixedMitigationSourceLabels(expectedOutcome)
                    ),
                    ShieldAbsorbedEstimate = expectedPreview?.ShieldAbsorbed ?? 0,
                    ShieldAbsorbedWorst = worstPreview?.ShieldAbsorbed ?? 0,
                }
            );
        }
        var statusEffectIds = new List<StringName>();
        int apPenalty = 0;
        if (distance <= 1)
        {
            if (plan.profile.concussed_status_id != "")
            {
                statusEffectIds.Add(plan.profile.concussed_status_id);
            }
            apPenalty = 1;
        }
        int maxHp = GetUnitMaxHp(target_unit);
        int currentHp = Math.Max(target_unit.current_hp, 1);
        int expectedHpPercent = Mathf.RoundToInt(
            (float)expectedDamage * 100.0f / Math.Max(maxHp, 1)
        );
        int worstHpPercent = Mathf.RoundToInt((float)worstCaseDamage * 100.0f / Math.Max(maxHp, 1));
        bool hardReject =
            worstCaseDamage >= currentHp
            || expectedHpPercent >= plan.profile.friendly_fire_hard_expected_hp_percent
            || worstHpPercent >= plan.profile.friendly_fire_hard_worst_case_hp_percent;
        bool isAlly =
            plan.source_unit != null && target_unit.faction_id == plan.source_unit.faction_id;
        var summary = new MeteorSwarmNumericSummary
        {
            CandidateAnchorCoord = plan.final_anchor_coord,
            TargetUnitId = target_unit.unit_id,
            AllyUnitId = target_unit.unit_id,
            TargetFactionId = target_unit.faction_id,
            IsAlly = isAlly,
            DistanceFromAnchor = distance,
            ComponentExpectedDamage = expectedDamage,
            ComponentWorstCaseDamage = worstCaseDamage,
            ComponentBreakdown = componentBreakdown,
            LethalProbabilityPercent = worstCaseDamage >= currentHp ? 100 : 0,
            SaveProfileIds = CollectComponentSaveProfileIds(componentBreakdown),
            ResistanceTiersByDamageTag = resistanceTiers,
            ShieldHp = target_unit.current_shield_hp,
            GuardBlockEstimate = guardBlockEstimate,
            StatusEffectIds = new List<StringName>(statusEffectIds),
            ApPenalty = apPenalty,
            HostileTerrain = BuildHostileTerrainConsequenceTyped(plan, distance),
            ExpectedDamageHpPercent = expectedHpPercent,
            WorstCaseDamageHpPercent = worstHpPercent,
            HardReject = hardReject,
            SoftPenalty =
                !hardReject
                && expectedHpPercent > plan.profile.friendly_fire_soft_expected_hp_percent,
        };
        return summary;
    }

    internal GDictArray _build_future_attack_roll_modifier_breakdown(MeteorSwarmTargetPlan plan)
    {
        return AttackPreviewData.BuildAttackRollModifierBreakdownPayload(
            BuildFutureAttackRollModifierBreakdownTyped(plan)
        );
    }

    private List<BattleAttackRollModifierSpec> BuildFutureAttackRollModifierBreakdownTyped(
        MeteorSwarmTargetPlan plan
    )
    {
        var breakdown = new List<BattleAttackRollModifierSpec>();
        if (plan == null || plan.profile == null)
        {
            return breakdown;
        }
        foreach (GDictionary terrainProfile in ReadDictionaryItems(plan.profile.terrain_profiles))
        {
            BattleAttackRollModifierSpec spec = BuildAccuracyModifierSpec(terrainProfile);
            if (spec == null)
            {
                continue;
            }
            spec.source_instance_id = DictString(terrainProfile, "terrain_profile_id");
            breakdown.Add(spec);
        }
        return breakdown;
    }

    internal GDictArray _build_component_preview(MeteorSwarmTargetPlan plan)
    {
        var preview = new GDictArray();
        foreach (MeteorSwarmComponentFact component in BuildComponentPreviewTyped(plan))
        {
            if (component != null)
            {
                preview.Add(component.ToDictionary());
            }
        }
        return preview;
    }

    private static BattleAttackRollModifierSpec BuildAccuracyModifierSpec(GDictionary terrainProfile)
    {
        GDictionary accuracySpec = DictDictionary(terrainProfile, "accuracy_modifier_spec");
        return accuracySpec.Count == 0
            ? null
            : BattleAttackRollModifierSpec.FromPartialDictionary(accuracySpec);
    }

    private List<MeteorSwarmComponentFact> BuildComponentPreviewTyped(MeteorSwarmTargetPlan plan)
    {
        var preview = new List<MeteorSwarmComponentFact>();
        if (plan == null || plan.profile == null)
        {
            return preview;
        }
        foreach (MeteorSwarmImpactComponent component in plan.profile.impact_components)
        {
            if (component == null)
            {
                continue;
            }
            preview.Add(BuildComponentPreviewFactTyped(component, 0));
        }
        return preview;
    }

    private static MeteorSwarmComponentFact BuildComponentPreviewFactTyped(
        MeteorSwarmImpactComponent component,
        int distance_from_anchor
    )
    {
        return new MeteorSwarmComponentFact
        {
            component_id = component.component_id,
            role_label = component.role_label,
            damage_tag = component.damage_tag,
            base_power = component.base_power,
            dice_count = component.dice_count,
            dice_sides = component.dice_sides,
            damage_scale = component.GetDamageScale(distance_from_anchor),
            save_profile_id = component.save_profile_id,
            can_crit = component.can_crit,
            mastery_weight = component.mastery_weight,
            ring_min = component.ring_min,
            ring_max = component.ring_max,
            distance_from_anchor = distance_from_anchor,
        };
    }

    private static GDictionary ToComponentTotalsDictionary(
        Dictionary<StringName, MeteorSwarmComponentFact> componentTotals
    )
    {
        var result = new GDictionary();
        if (componentTotals == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, MeteorSwarmComponentFact> entry in componentTotals)
        {
            if (StringNameIsEmpty(entry.Key) || entry.Value == null)
            {
                continue;
            }
            result[entry.Key] = entry.Value.ToDictionary();
        }
        return result;
    }

    internal int _count_expected_terrain_effects(MeteorSwarmTargetPlan plan)
    {
        return BuildTerrainSummaryTyped(plan).terrain_effect_count;
    }

    internal int _resolve_friendly_fire_risk_percent(GDictArray summaries)
    {
        var typedSummaries = new List<MeteorSwarmNumericSummary>();
        foreach (GDictionary summary in summaries ?? new GDictArray())
        {
            typedSummaries.Add(MeteorSwarmNumericSummary.FromDictionary(summary));
        }
        return ResolveFriendlyFireRiskPercent(typedSummaries);
    }

    private static int ResolveFriendlyFireRiskPercent(
        IReadOnlyList<MeteorSwarmNumericSummary> summaries
    )
    {
        if (summaries == null || summaries.Count == 0)
        {
            return 0;
        }
        int hardCount = 0;
        foreach (MeteorSwarmNumericSummary summary in summaries)
        {
            if (summary?.HardReject == true)
            {
                hardCount += 1;
            }
        }
        return Mathf.RoundToInt((float)hardCount * 100.0f / summaries.Count);
    }

    private MeteorSwarmHostileTerrainConsequence BuildHostileTerrainConsequenceTyped(
        MeteorSwarmTargetPlan plan,
        int distance_from_anchor
    )
    {
        var consequence = new MeteorSwarmHostileTerrainConsequence();
        foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, distance_from_anchor))
        {
            consequence.MoveCostDelta = Math.Max(
                consequence.MoveCostDelta,
                DictInt(terrainProfile, "move_cost_delta", 0)
            );
            string profileId = DictString(terrainProfile, "terrain_profile_id");
            if (profileId.Contains("dust"))
            {
                consequence.CreatesDust = true;
            }
            if (profileId.Contains("crater"))
            {
                consequence.CreatesCrater = true;
            }
            if (profileId.Contains("rubble"))
            {
                consequence.CreatesRubble = true;
            }
        }
        return consequence;
    }

    private static List<string> CollectComponentSaveProfileIds(
        IEnumerable<MeteorSwarmComponentBreakdownEntry> component_breakdown
    )
    {
        var ids = new List<string>();
        foreach (
            MeteorSwarmComponentBreakdownEntry component
            in component_breakdown ?? Array.Empty<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            string saveProfileId = component?.SaveProfileId ?? "";
            if (!string.IsNullOrEmpty(saveProfileId) && !ids.Contains(saveProfileId))
            {
                ids.Add(saveProfileId);
            }
        }
        return ids;
    }

    private static List<string> ReadMitigationLabels(
        IReadOnlyDictionary<string, object> damageOutcome,
        MitigationTierKind tier
    )
    {
        ReadMitigationSourceLabels(
            damageOutcome,
            out string[] halfSourceLabels,
            out string[] doubleSourceLabels,
            out string[] immuneSourceLabels
        );
        return tier switch
        {
            MitigationTierKind.Half => new List<string>(halfSourceLabels),
            MitigationTierKind.Double => new List<string>(doubleSourceLabels),
            MitigationTierKind.Immune => new List<string>(immuneSourceLabels),
            _ => new List<string>(),
        };
    }

    private static int ReadPreviewOutcomeInt(
        IReadOnlyDictionary<string, object> source,
        string key,
        int fallback = 0
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, out object value))
        {
            return fallback;
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => fallback,
        };
    }

    private static StringName ReadPreviewOutcomeStringName(
        IReadOnlyDictionary<string, object> source,
        string key,
        StringName fallback = default
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, out object value))
        {
            return fallback;
        }
        return value switch
        {
            StringName name => ProgressionDataUtils.to_string_name(name),
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            _ => fallback,
        };
    }

    private static void ReadMitigationSourceLabels(
        IReadOnlyDictionary<string, object> damageOutcome,
        out string[] halfSourceLabels,
        out string[] doubleSourceLabels,
        out string[] immuneSourceLabels
    )
    {
        var half = new List<string>();
        var @double = new List<string>();
        var immune = new List<string>();
        foreach (
            IReadOnlyDictionary<string, object> source in ReadDictionaryItems(
                ReadObjectList(damageOutcome, "mitigation_sources")
            )
        )
        {
            string label = FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }
            switch (
                AttackEffectResolutionResultReader.ParseMitigationTier(
                    ReadPreviewOutcomeStringName(source, "tier")
                )
            )
            {
                case MitigationTierKind.Half:
                    AppendUnique(half, label);
                    break;
                case MitigationTierKind.Double:
                    AppendUnique(@double, label);
                    break;
                case MitigationTierKind.Immune:
                    AppendUnique(immune, label);
                    break;
            }
        }
        halfSourceLabels = half.ToArray();
        doubleSourceLabels = @double.ToArray();
        immuneSourceLabels = immune.ToArray();
    }

    private static string[] ReadFixedMitigationSourceLabels(
        IReadOnlyDictionary<string, object> damageOutcome
    )
    {
        var labels = new List<string>();
        foreach (
            IReadOnlyDictionary<string, object> source in ReadDictionaryItems(
                ReadObjectList(damageOutcome, "fixed_mitigation_sources")
            )
        )
        {
            string label = FormatDamageSourceLabel(source);
            if (!string.IsNullOrEmpty(label))
            {
                AppendUnique(labels, label);
            }
        }
        return labels.ToArray();
    }

    private static void AppendUnique(List<string> target, string value)
    {
        if (string.IsNullOrEmpty(value) || target == null || target.Contains(value))
        {
            return;
        }
        target.Add(value);
    }

    private static string FormatDamageSourceLabel(IReadOnlyDictionary<string, object> source)
    {
        StringName statusId = ReadPreviewOutcomeStringName(source, "status_id");
        if (statusId != "")
        {
            return statusId.ToString();
        }
        if (source != null && source.TryGetValue("type", out object value))
        {
            return value switch
            {
                StringName typeName => typeName.ToString(),
                string text => text ?? "",
                _ => value?.ToString() ?? "",
            };
        }
        return "";
    }

    private static IReadOnlyList<object> ReadObjectList(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, out object value))
        {
            return Array.Empty<object>();
        }
        return value switch
        {
            IReadOnlyList<object> values => values,
            IEnumerable<object> values => new List<object>(values),
            _ => Array.Empty<object>(),
        };
    }

    private void ApplySaveProfileToDamageEffect(
        CombatEffectDef effect,
        MeteorSwarmImpactComponent component
    )
    {
        if (effect == null || component == null)
        {
            return;
        }
        if (component.save_profile_id == SAVE_PROFILE_METEOR_DEX_HALF)
        {
            effect.SaveDcModeKind = BattleSaveDcMode.CasterSpell;
            effect.save_dc_source_ability = "intelligence";
            effect.save_ability = "agility";
            effect.save_partial_on_success = true;
            effect.save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic);
        }
    }

    private BattleDamagePreviewResult BuildComponentDamagePreview(
        MeteorSwarmTargetPlan plan,
        BattleUnitState source_preview,
        BattleUnitState target_preview,
        CombatEffectDef effect_def,
        BattleDamagePreviewRollMode roll_mode,
        BattleDamagePreviewSaveMode save_mode
    )
    {
        if (
            _runtime == null
            || source_preview == null
            || target_preview == null
            || effect_def == null
        )
        {
            return BattleDamagePreviewResult.Empty();
        }
        BattleDamageResolver damageResolver = DamageResolver();
        if (damageResolver == null)
        {
            return BattleDamagePreviewResult.Empty();
        }
        return damageResolver.PreviewDamageEffectTyped(
            source_preview,
            target_preview,
            effect_def,
            new GDictionary
            {
                ["battle_state"] = State(),
                ["skill_id"] = plan != null ? plan.skill_id : DEFAULT_SKILL_ID,
            },
            roll_mode,
            save_mode
        );
    }

    private static int EstimatePostSaveDamage(
        MeteorSwarmImpactComponent component,
        int damageBeforeSave
    )
    {
        if (component == null || component.save_profile_id != SAVE_PROFILE_METEOR_DEX_HALF)
        {
            return Math.Max(damageBeforeSave, 0);
        }
        int successDamage = Mathf.FloorToInt(Math.Max(damageBeforeSave, 0) * 0.5f);
        return Mathf.RoundToInt((Math.Max(damageBeforeSave, 0) + successDamage) * 0.5f);
    }

    private static int EstimateWorstPostSaveDamage(
        MeteorSwarmImpactComponent component,
        int damageBeforeSave
    )
    {
        return Math.Max(damageBeforeSave, 0);
    }

    private static int ApplyShieldEstimate(int damage, ref int shieldRemaining)
    {
        int normalizedDamage = Math.Max(damage, 0);
        if (normalizedDamage == 0 || shieldRemaining <= 0)
        {
            return normalizedDamage;
        }
        int absorbed = Math.Min(normalizedDamage, shieldRemaining);
        shieldRemaining -= absorbed;
        return normalizedDamage - absorbed;
    }

    private static BattleDamagePreviewSaveEstimate BuildReadOnlySaveEstimate(
        MeteorSwarmImpactComponent component,
        int damageBeforeSave
    )
    {
        int normalizedDamage = Math.Max(damageBeforeSave, 0);
        if (component == null || component.save_profile_id != SAVE_PROFILE_METEOR_DEX_HALF)
        {
            return BattleDamagePreviewSaveEstimate.None(normalizedDamage);
        }
        int successDamage = Mathf.FloorToInt(normalizedDamage * 0.5f);
        int expectedDamage = EstimatePostSaveDamage(component, normalizedDamage);
        return BattleDamagePreviewSaveEstimate.Create(
            hasSave: true,
            damageBeforeSave: normalizedDamage,
            damageAfterSave: expectedDamage,
            damageAfterSaveEstimate: expectedDamage,
            damageAfterSaveWorst: normalizedDamage,
            damageOnSaveFailure: normalizedDamage,
            damageOnSaveSuccess: successDamage,
            savePartialOnSuccess: true,
            saveSuccessProbabilityBasisPoints: 5000,
            saveSuccessRatePercent: 50,
            saveFailureProbabilityBasisPoints: 5000,
            dc: 0,
            ability: "agility",
            saveTag: BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic).ToString(),
            advantageState: "",
            abilityValue: 0,
            abilityModifier: 0,
            bonus: 0,
            immune: false,
            sources: Array.Empty<BattleSaveSource>()
        );
    }

    private static BattleDamagePreviewSaveEstimate BuildReadOnlyWorstSaveEstimate(
        MeteorSwarmImpactComponent component,
        int damageBeforeSave
    )
    {
        int normalizedDamage = Math.Max(damageBeforeSave, 0);
        if (component == null || component.save_profile_id != SAVE_PROFILE_METEOR_DEX_HALF)
        {
            return BattleDamagePreviewSaveEstimate.None(normalizedDamage);
        }
        int successDamage = Mathf.FloorToInt(normalizedDamage * 0.5f);
        return BattleDamagePreviewSaveEstimate.Create(
            hasSave: true,
            damageBeforeSave: normalizedDamage,
            damageAfterSave: normalizedDamage,
            damageAfterSaveEstimate: normalizedDamage,
            damageAfterSaveWorst: normalizedDamage,
            damageOnSaveFailure: normalizedDamage,
            damageOnSaveSuccess: successDamage,
            savePartialOnSuccess: true,
            saveSuccessProbabilityBasisPoints: 0,
            saveSuccessRatePercent: 0,
            saveFailureProbabilityBasisPoints: 10000,
            dc: 0,
            ability: "agility",
            saveTag: BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic).ToString(),
            advantageState: "",
            abilityValue: 0,
            abilityModifier: 0,
            bonus: 0,
            immune: false,
            sources: Array.Empty<BattleSaveSource>()
        );
    }

    private void PopulateUnitDistances(MeteorSwarmTargetPlan plan)
    {
        if (_runtime == null || State() == null)
        {
            return;
        }
        BattleGridService gridService = GridService();
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitState targetUnit = UnitById(targetUnitId);
            if (targetUnit == null)
            {
                continue;
            }
            targetUnit.RefreshFootprint();
            int bestDistance = 999999;
            Vector2I bestCoord = plan.GetPrimaryCoordForUnit(targetUnitId);
            GVector2IArray occupiedCoords = targetUnit.occupied_coords;
            if (occupiedCoords.Count == 0)
            {
                occupiedCoords = gridService.GetUnitTargetCoords(targetUnit, targetUnit.coord);
            }
            foreach (Vector2I coord in occupiedCoords)
            {
                if (!plan.ring_by_coord.ContainsKey(coord))
                {
                    continue;
                }
                int distance = plan.GetRingForCoord(coord);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCoord = coord;
                }
            }
            plan.unit_distance_by_id[targetUnitId] = bestDistance;
            plan.unit_primary_coord_by_id[targetUnitId] = bestCoord;
        }
    }

    private string BuildPlanSignatureForAnchor(
        MeteorSwarmTargetPlan plan,
        Vector2I anchor_coord
    )
    {
        if (anchor_coord == plan.final_anchor_coord)
        {
            return BuildPlanSignature(plan, anchor_coord);
        }
        int affectedCount = 0;
        BattleState state = State();
        if (_runtime != null && state != null)
        {
            BattleGridService gridService = GridService();
            for (int dy = -plan.radius; dy <= plan.radius; dy++)
            {
                for (int dx = -plan.radius; dx <= plan.radius; dx++)
                {
                    Vector2I coord = anchor_coord + new Vector2I(dx, dy);
                    if (gridService.IsInside(state, coord))
                    {
                        affectedCount += 1;
                    }
                }
            }
        }
        return $"{plan.skill_id}:{plan.coverage_shape_id}:r{plan.radius}:{anchor_coord.X},{anchor_coord.Y}:{affectedCount}";
    }

    private string BuildPlanSignature(MeteorSwarmTargetPlan plan, Vector2I anchor_coord)
    {
        var unitParts = new List<string>();
        foreach (StringName unitId in plan.target_unit_ids)
        {
            unitParts.Add(unitId.ToString());
        }
        return $"{plan.skill_id}:{plan.coverage_shape_id}:r{plan.radius}:{anchor_coord.X},{anchor_coord.Y}:{plan.affected_coords.Count}:{string.Join(",", unitParts)}";
    }

    private GVector2IArray ExtractTargetCoords(BattleGroundSkillValidationResult validation)
    {
        var targetCoords = new GVector2IArray();
        foreach (Vector2I coord in validation.TargetCoords)
        {
            targetCoords.Add(coord);
        }
        return targetCoords;
    }

    private MeteorSwarmProfile ResolveProfile()
    {
        if (_runtime == null)
        {
            return null;
        }
        GDictionary snapshot = _runtime._special_profile_registry_snapshot;
        GDictionary profiles = DictDictionary(snapshot, "profiles");
        GDictionary meteorProfileSnapshot = DictDictionary(profiles, "meteor_swarm");
        if (meteorProfileSnapshot.Count == 0)
        {
            return null;
        }
        return DictMeteorSwarmProfile(meteorProfileSnapshot, "profile_resource");
    }

    internal CombatCastVariantDef ResolveGroundCastVariant(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def
    )
    {
        if (_runtime == null || skill_def == null)
        {
            return null;
        }
        CombatCastVariantDef castVariant = _runtime.ResolveGroundCastVariantTyped(
            skill_def,
            active_unit,
            command
        );
        if (castVariant != null)
        {
            return castVariant;
        }
        if (
            skill_def.combat_profile != null
            && skill_def.combat_profile.TargetModeKind == BattleTargetMode.Ground
        )
        {
            return _runtime._build_implicit_ground_cast_variant(skill_def);
        }
        return null;
    }

    internal CombatCastVariantDef ResolveGroundCastVariant(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDef skill_def
    )
    {
        if (_runtime == null || skill_def == null)
        {
            return null;
        }
        CombatCastVariantDef castVariant = _runtime.ResolveGroundCastVariantTyped(
            skill_def,
            active_unit,
            command
        );
        if (castVariant != null)
        {
            return castVariant;
        }
        if (
            skill_def.combat_profile != null
            && skill_def.combat_profile.TargetModeKind == BattleTargetMode.Ground
        )
        {
            return _runtime._build_implicit_ground_cast_variant(skill_def);
        }
        return null;
    }

    private bool UnitCoversCoord(BattleUnitState unit_state, Vector2I coord)
    {
        if (unit_state == null)
        {
            return false;
        }
        unit_state.RefreshFootprint();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            if (occupiedCoord == coord)
            {
                return true;
            }
        }
        return unit_state.coord == coord;
    }

    private bool UnitCoversCoord(BattleUnitReadView unit_state, Vector2I coord)
    {
        if (!unit_state.IsValid)
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit_state.GetOccupiedCoords())
        {
            if (occupiedCoord == coord)
            {
                return true;
            }
        }
        return unit_state.Coord == coord;
    }

    private int GetUnitMaxHp(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 1;
        }
        if (unit_state.attribute_snapshot != null)
        {
            int maxHp = unit_state
                .attribute_snapshot.GetValue(new StringName("hp_max"));
            if (maxHp > 0)
            {
                return maxHp;
            }
        }
        return Math.Max(unit_state.current_hp, 1);
    }

    private int GetUnitMaxHp(BattleUnitReadView unit_state)
    {
        if (!unit_state.IsValid)
        {
            return 1;
        }
        int maxHp = unit_state.GetAttributeValue(new StringName("hp_max"), 0);
        if (maxHp > 0)
        {
            return maxHp;
        }
        return Math.Max(unit_state.CurrentHp, 1);
    }

    private string TerrainProfileDisplayName(StringName terrain_profile_id)
    {
        if (terrain_profile_id == "meteor_swarm_crater_core")
        {
            return "陨坑";
        }
        if (terrain_profile_id == "meteor_swarm_crater_rim")
        {
            return "陨坑边缘";
        }
        if (
            terrain_profile_id == "meteor_swarm_rubble"
            || terrain_profile_id == "meteor_swarm_edge_rubble"
        )
        {
            return "碎石";
        }
        if (terrain_profile_id == "meteor_swarm_dust")
        {
            return "尘土";
        }
        return terrain_profile_id.ToString();
    }

    private static void SortCoordListAscending(List<Vector2I> coords)
    {
        if (coords == null)
        {
            return;
        }
        coords.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
    }

    private static void SortUnitIdsByPrimaryCoord(MeteorSwarmTargetPlan plan)
    {
        var ids = new List<StringName>();
        foreach (StringName id in plan.target_unit_ids)
        {
            ids.Add(id);
        }
        ids.Sort(
            (l, r) =>
            {
                Vector2I lc = plan.GetPrimaryCoordForUnit(l);
                Vector2I rc = plan.GetPrimaryCoordForUnit(r);
                return lc.Y != rc.Y ? lc.Y.CompareTo(rc.Y) : lc.X.CompareTo(rc.X);
            }
        );
        plan.target_unit_ids = ids;
    }

    private BattleState State()
    {
        return _runtime?._state;
    }

    private BattleGridService GridService()
    {
        return _runtime?.GetGridService();
    }

    private BattleDamageResolver DamageResolver()
    {
        return _runtime?.GetDamageResolver();
    }

    private BattleUnitState UnitById(StringName id)
    {
        BattleState state = State();
        if (state == null || !state.TryGetUnitTyped(id, out BattleUnitState unitState))
        {
            return null;
        }
        return unitState;
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return new GArray();
        return value.AsGodotArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return new GDictionary();
        return value.AsGodotDictionary();
    }

    private static MeteorSwarmProfile DictMeteorSwarmProfile(GDictionary dictionary, string key)
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return null;
        return value.As<MeteorSwarmProfile>();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return fallback;
        return value.AsInt32();
    }

    private static string DictString(GDictionary dictionary, string key, string fallback = "")
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return fallback;
        string result = value.ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        string key,
        StringName fallback = default
    )
    {
        if (!TryResolveStringKey(dictionary, key, out Variant value))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(value);
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (var value in values)
        {
            yield return value.AsGodotDictionary();
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> ReadDictionaryItems(
        IReadOnlyList<object> values
    )
    {
        if (values == null)
            yield break;
        foreach (object value in values)
        {
            switch (value)
            {
                case IReadOnlyDictionary<string, object> dictionary:
                    yield return dictionary;
                    break;
            }
        }
    }

    private static bool TryResolveStringKey(GDictionary dictionary, string key, out Variant value)
    {
        value = default;
        if (dictionary == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        return false;
    }

    private static bool StringNameIsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static IEnumerable<GDictionary> TerrainProfilesForRing(
        MeteorSwarmProfile profile,
        int ring
    )
    {
        if (profile == null)
        {
            yield break;
        }
        foreach (GDictionary profileValue in ReadDictionaryItems(
            profile.GetTerrainProfilesForRing(ring)
        ))
        {
            yield return profileValue;
        }
    }

    private static IEnumerable<StringName> NormalizeStatusIds(GArray statusEffectIds)
    {
        foreach (var statusIdValue in statusEffectIds ?? new GArray())
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (!StringNameIsEmpty(statusId))
            {
                yield return statusId;
            }
        }
    }

    private static GVector2IArray ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

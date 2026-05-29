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
[GlobalClass]
public partial class BattleMeteorSwarmResolver : RefCounted
{
    private static readonly StringName PROFILE_ID = "meteor_swarm";
    private static readonly StringName COVERAGE_SHAPE_ID = "square_7x7";
    private static readonly StringName DEFAULT_SKILL_ID = "mage_meteor_swarm";
    private static readonly StringName STATUS_METEOR_CONCUSSED = "meteor_concussed";
    private static readonly StringName MITIGATION_TIER_NORMAL = "normal";
    private static readonly StringName SAVE_PROFILE_METEOR_DEX_HALF = "meteor_dex_half";

    // 镜像 BattleDamageResolver.cs 的预览常量。
    private static readonly StringName DAMAGE_PREVIEW_ROLL_MODE_AVERAGE = "average";
    private static readonly StringName DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM = "maximum";
    private static readonly StringName DAMAGE_PREVIEW_SAVE_MODE_EXPECTED = "expected";
    private static readonly StringName DAMAGE_PREVIEW_SAVE_MODE_WORST = "worst";

    private readonly BattleReportFormatter _reportFormatter = new();

    private BattleRuntimeModule _runtime;
    private BattleAttackCheckPolicyService _attack_check_policy_service;

    public void setup(
        BattleRuntimeModule runtime,
        BattleAttackCheckPolicyService attack_check_policy_service = null
    )
    {
        _runtime = runtime;
        _attack_check_policy_service = attack_check_policy_service;
    }

    public void dispose()
    {
        _runtime = null;
        _attack_check_policy_service = null;
    }

    public void populate_preview(
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
            preview.log_lines.Add("技能或目标无效。");
            return;
        }
        CombatCastVariantDef castVariant = _resolve_ground_cast_variant(active_unit, command, skill_def);
        GDictionary validation = _runtime._validate_ground_skill_command(
            active_unit,
            skill_def,
            castVariant,
            command
        );
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            preview.log_lines.Add(GdInterop.GetString(validation, "message", "技能或目标无效。"));
            return;
        }
        GVector2IArray targetCoords = _extract_target_coords(validation);
        if (targetCoords.Count == 0)
        {
            preview.log_lines.Add("技能或目标无效。");
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
        MeteorSwarmPreviewFacts facts = build_preview_facts(context);
        preview.allowed = true;
        preview.resolved_anchor_coord = anchorCoord;
        preview.target_coords = facts.target_coords.Duplicate();
        preview.target_unit_ids = facts.target_unit_ids.Duplicate();
        preview.special_profile_preview_facts = facts;
        preview.hit_preview =
            new GDictionary
            {
                ["summary_text"] =
                    $"陨星雨影响 {facts.impact_count} 格、预计波及 {facts.expected_target_count} 个单位。",
                ["modifier_breakdown"] = facts.attack_roll_modifier_breakdown.Duplicate(true),
                ["source"] = "special_profile_preview_facts",
            };
        preview.log_lines.Add(
            $"可施放陨星雨：影响 {facts.impact_count} 格，预计波及 {facts.expected_target_count} 个单位。"
        );
    }

    public RefCounted build_cast_context(
        RefCounted active_unit,
        RefCounted command,
        Resource skill_def,
        Resource cast_variant,
        Vector2I nominal_anchor_coord,
        Vector2I final_anchor_coord,
        GDictionary spell_control_context = null,
        GDictionary drift_context = null
    )
    {
        return BuildCastContextTyped(
            active_unit as BattleUnitState,
            command as BattleCommand,
            skill_def as SkillDef,
            cast_variant as CombatCastVariantDef,
            nominal_anchor_coord,
            final_anchor_coord,
            spell_control_context,
            drift_context
        );
    }

    internal MeteorSwarmCastContext BuildCastContextTyped(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        Vector2I nominal_anchor_coord,
        Vector2I final_anchor_coord,
        GDictionary spell_control_context = null,
        GDictionary drift_context = null
    )
    {
        spell_control_context ??= new GDictionary();
        drift_context ??= new GDictionary();
        var context = new MeteorSwarmCastContext
        {
            active_unit = active_unit,
            command = command,
            skill_def = skill_def,
            cast_variant = cast_variant,
            profile = _resolve_profile(),
            nominal_anchor_coord = nominal_anchor_coord,
            final_anchor_coord = final_anchor_coord,
            spell_control_context = (GDictionary)spell_control_context.Duplicate(true),
            drift_context = (GDictionary)drift_context.Duplicate(true),
        };
        return context;
    }

    public MeteorSwarmPreviewFacts build_preview_facts(MeteorSwarmCastContext context)
    {
        MeteorSwarmTargetPlan plan = BuildTargetPlanTyped(context);
        var facts = new MeteorSwarmPreviewFacts
        {
            profile_id = PROFILE_ID,
            skill_id = plan.skill_id,
            preview_fact_id = new StringName($"meteor_swarm:{plan.nominal_plan_signature}"),
            nominal_plan_signature = plan.nominal_plan_signature,
            final_plan_signature = plan.final_plan_signature,
            resolved_anchor_coord = plan.final_anchor_coord,
            target_unit_ids = (GStringNameArray)plan.target_unit_ids.Duplicate(),
            target_coords = (GVector2IArray)plan.affected_coords.Duplicate(),
            terrain_summary = _build_terrain_summary(plan),
            target_numeric_summary = _build_target_numeric_summary(plan),
            friendly_fire_numeric_summary = _build_friendly_fire_numeric_summary(plan),
            attack_roll_modifier_breakdown = _build_future_attack_roll_modifier_breakdown(plan),
            impact_count = plan.affected_coords.Count,
            expected_target_count = plan.target_unit_ids.Count,
        };
        facts.expected_terrain_effect_count = _count_expected_terrain_effects(plan);
        facts.friendly_fire_risk_percent = _resolve_friendly_fire_risk_percent(
            facts.friendly_fire_numeric_summary
        );
        facts.component_preview = _build_component_preview(plan);
        return facts;
    }

    public RefCounted build_target_plan(RefCounted context)
    {
        return BuildTargetPlanTyped(context as MeteorSwarmCastContext);
    }

    internal MeteorSwarmTargetPlan BuildTargetPlanTyped(MeteorSwarmCastContext context)
    {
        var plan = new MeteorSwarmTargetPlan();
        MeteorSwarmProfile profile =
            context != null && context.profile != null ? context.profile : _resolve_profile();
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
        plan.drift_applied = context != null && context.has_drift();
        plan.drift_from_coord = plan.drift_applied
            ? plan.nominal_anchor_coord
            : new Vector2I(-1, -1);
        BattleState state = State();
        if (_runtime == null || state == null || plan.final_anchor_coord == new Vector2I(-1, -1))
        {
            plan.nominal_plan_signature = _build_plan_signature(plan, plan.nominal_anchor_coord);
            plan.final_plan_signature = _build_plan_signature(plan, plan.final_anchor_coord);
            return plan;
        }

        BattleGridService gridService = GridService();
        var seenUnitIds = new GDictionary();
        for (int dy = -plan.radius; dy <= plan.radius; dy++)
        {
            for (int dx = -plan.radius; dx <= plan.radius; dx++)
            {
                Vector2I coord = plan.final_anchor_coord + new Vector2I(dx, dy);
                if (!gridService.is_inside(state, coord))
                {
                    continue;
                }
                int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                plan.affected_coords.Add(coord);
                plan.ring_by_coord[coord] = ring;
                BattleCellState cell = gridService.get_cell(state, coord);
                StringName occupantId =
                    cell != null
                        ? cell.occupant_unit_id
                        : new StringName("");
                if (
                    cell == null
                    || GdInterop.IsEmpty(occupantId)
                    || seenUnitIds.ContainsKey(occupantId)
                )
                {
                    continue;
                }
                BattleUnitState unitState = UnitById(occupantId);
                if (unitState == null || !unitState.is_alive)
                {
                    continue;
                }
                seenUnitIds[occupantId] = true;
                plan.target_unit_ids.Add(occupantId);
                plan.unit_primary_coord_by_id[occupantId] = coord;
            }
        }
        SortCoordArrayAscending(plan.affected_coords, out GVector2IArray sortedCoords);
        plan.affected_coords = sortedCoords;
        SortUnitIdsByPrimaryCoord(plan);
        _populate_unit_distances(plan);
        plan.nominal_plan_signature = _build_plan_signature_for_anchor(
            plan,
            plan.nominal_anchor_coord
        );
        plan.final_plan_signature = _build_plan_signature_for_anchor(plan, plan.final_anchor_coord);
        return plan;
    }

    public RefCounted resolve(RefCounted plan)
    {
        return ResolveTyped(plan as MeteorSwarmTargetPlan);
    }

    internal MeteorSwarmCommitResult ResolveTyped(MeteorSwarmTargetPlan plan)
    {
        var result = new MeteorSwarmCommitResult();
        if (plan == null || plan.profile == null || _runtime == null || State() == null)
        {
            return result;
        }
        result.plan = plan;
        result.add_changed_unit_id(plan.source_unit_id);
        GDictArray terrainEffects = _apply_terrain_effects(plan);
        foreach (GDictionary terrainEffect in terrainEffects)
        {
            result.terrain_effects.Add(terrainEffect.Duplicate(true));
            Vector2I terrainCoord = GdInterop.GetVector2I(
                terrainEffect,
                "coord",
                new Vector2I(-1, -1)
            );
            if (terrainCoord != new Vector2I(-1, -1))
            {
                result.add_changed_coord(terrainCoord);
            }
        }
        var componentTotals = new GDictionary();
        foreach (StringName targetUnitId in plan.target_unit_ids)
        {
            BattleUnitState targetUnit = UnitById(targetUnitId);
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            MeteorSwarmTargetOutcome targetOutcome = _resolve_target(
                plan,
                targetUnit,
                componentTotals
            );
            result.target_outcomes.Add(targetOutcome);
            result.total_damage += targetOutcome.total_damage;
            result.total_healing += targetOutcome.total_healing;
            result.add_changed_unit_id(targetUnit.unit_id);
            foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
            {
                result.add_changed_coord(occupiedCoord);
            }
            if (targetOutcome.defeated)
            {
                result.add_defeated_unit_id(targetUnit.unit_id);
            }
        }
        foreach (Vector2I terrainCoord in plan.affected_coords)
        {
            result.add_changed_coord(terrainCoord);
        }
        result.report_entries.Add(_build_report_entry(plan, result, componentTotals));
        result.log_lines.Add(
            $"{(plan.source_unit != null ? plan.source_unit.display_name : "单位")} 施放陨星雨，灾害区覆盖 {plan.affected_coords.Count} 格，波及 {result.target_outcomes.Count} 个单位，造成 {result.total_damage} 点总伤害。"
        );
        GDictionary terrainSummary = _build_terrain_summary(plan);
        result.log_lines.Add(
            $"陨击留下地形：陨坑 {GdInterop.GetInt(terrainSummary, "crater_count", 0)} 格，碎石 {GdInterop.GetInt(terrainSummary, "rubble_count", 0)} 格，尘土 {GdInterop.GetInt(terrainSummary, "dust_count", 0)} 格。"
        );
        return result;
    }

    public MeteorSwarmTargetOutcome _resolve_target(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit,
        GDictionary component_totals
    )
    {
        var outcome = new MeteorSwarmTargetOutcome
        {
            target_unit_id = target_unit.unit_id,
            target_coord = plan.get_primary_coord_for_unit(target_unit.unit_id),
            target_faction_id = target_unit.faction_id,
            distance_from_anchor = plan.get_distance_for_unit(target_unit.unit_id),
        };
        bool coversCenter = _unit_covers_coord(target_unit, plan.final_anchor_coord);
        foreach (MeteorSwarmImpactComponent component in plan.profile.impact_components)
        {
            if (
                component == null
                || !component.applies_to_distance(outcome.distance_from_anchor, coversCenter)
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
            GDictionary damageResult = DamageResolver()
                .resolve_effects(
                    plan.source_unit,
                    target_unit,
                    new GArray { effectDef },
                    damageContext
                );
            _tag_damage_events(damageResult, component, outcome.distance_from_anchor);
            outcome.add_component(component);
            outcome.total_damage += GdInterop.GetInt(damageResult, "damage", 0);
            outcome.total_healing += GdInterop.GetInt(damageResult, "healing", 0);
            foreach (GDictionary damageEvent in GdInterop.ReadDictionaryItems(
                GdInterop.GetArray(damageResult, "damage_events")
            ))
            {
                outcome.damage_events.Add(damageEvent.Duplicate(true));
            }
            GDictionary componentFact = _build_component_report_fact(
                component,
                damageResult,
                outcome.distance_from_anchor
            );
            outcome.report_component_breakdown.Add(componentFact);
            _add_component_total(component_totals, componentFact);
        }
        if (
            outcome.distance_from_anchor <= 1
            && !GdInterop.IsEmpty(plan.profile.concussed_status_id)
            && target_unit.is_alive
        )
        {
            GDictionary statusResult = _apply_concussed_status(plan, target_unit);
            foreach (StringName statusId in NormalizeStatusIds(GdInterop.GetArray(statusResult, "status_effect_ids")))
            {
                outcome.add_status_effect_id(statusId);
            }
        }
        target_unit.is_alive = target_unit.current_hp > 0;
        outcome.defeated = !target_unit.is_alive;
        return outcome;
    }

    public GDictArray _apply_terrain_effects(MeteorSwarmTargetPlan plan)
    {
        var effects = new GDictArray();
        BattleState state = State();
        BattleTerrainEffectSystem terrainSystem = _runtime?._terrain_effect_system;
        if (state == null || terrainSystem == null)
        {
            return effects;
        }
        foreach (Vector2I coord in plan.affected_coords)
        {
            int ring = plan.get_ring_for_coord(coord);
            foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, ring))
            {
                CombatEffectDef effectDef = _build_terrain_effect_def(terrainProfile);
                StringName fieldInstanceId = _runtime._build_terrain_effect_instance_id(
                    effectDef.terrain_effect_id
                );
                if (
                    !terrainSystem.upsert_timed_terrain_effect(
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
                    new GDictionary
                    {
                        ["coord"] = coord,
                        ["ring"] = ring,
                        ["terrain_profile_id"] = GdInterop.GetString(
                            terrainProfile,
                            "terrain_profile_id"
                        ),
                        ["terrain_effect_id"] = effectDef.terrain_effect_id.ToString(),
                        ["lifetime_policy"] = GdInterop.GetString(
                            effectDef.@params,
                            "lifetime_policy"
                        ),
                        ["move_cost_delta"] = GdInterop.GetInt(
                            effectDef.@params,
                            "move_cost_delta",
                            0
                        ),
                        ["render_overlay_id"] = GdInterop.GetString(
                            effectDef.@params,
                            "render_overlay_id"
                        ),
                    }
                );
            }
        }
        return effects;
    }

    public CombatEffectDef _build_damage_effect_def(
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
        };
        effect.@params = new GDictionary
        {
            ["dice_count"] = component.dice_count,
            ["dice_sides"] = component.dice_sides,
            ["runtime_pre_resistance_damage_multiplier"] = component.get_damage_scale(
                distance_from_anchor
            ),
            ["meteor_component_id"] = component.component_id.ToString(),
            ["meteor_role_label"] = component.role_label.ToString(),
        };
        _apply_save_profile_to_damage_effect(effect, component);
        return effect;
    }

    public CombatEffectDef _build_terrain_effect_def(GDictionary terrain_profile)
    {
        var effect = new CombatEffectDef();
        StringName terrainProfileId = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(terrain_profile, "terrain_profile_id")
        );
        effect.effect_type = "terrain_effect";
        effect.tick_effect_type = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(terrain_profile, "tick_effect_type", "none")
        );
        effect.terrain_effect_id = terrainProfileId;
        effect.duration_tu = GdInterop.GetInt(terrain_profile, "duration_tu", 0);
        effect.tick_interval_tu = GdInterop.GetInt(terrain_profile, "tick_interval_tu", 0);
        effect.stack_behavior = "refresh";
        effect.effect_target_team_filter = "any";
        effect.@params = new GDictionary
        {
            ["lifetime_policy"] = GdInterop.GetStringName(
                terrain_profile,
                "lifetime_policy",
                "timed"
            ),
            ["move_cost_delta"] = GdInterop.GetInt(terrain_profile, "move_cost_delta", 0),
            ["move_cost_stack_key"] = GdInterop.GetStringName(
                terrain_profile,
                "move_cost_stack_key",
                ""
            ),
            ["move_cost_stack_mode"] = GdInterop.GetStringName(
                terrain_profile,
                "move_cost_stack_mode",
                ""
            ),
            ["render_overlay_id"] = GdInterop.GetString(terrain_profile, "render_overlay_id"),
            ["overlay_priority"] = GdInterop.GetInt(terrain_profile, "overlay_priority", 0),
            ["display_name"] = _terrain_profile_display_name(terrainProfileId),
        };
        GDictionary accuracySpec = GdInterop.GetDictionary(
            terrain_profile,
            "accuracy_modifier_spec"
        );
        if (accuracySpec.Count > 0)
        {
            effect.@params["accuracy_modifier_spec"] = accuracySpec.Duplicate(true);
        }
        return effect;
    }

    public GDictionary _apply_concussed_status(
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
        };
        effect.@params = new GDictionary { ["duration_tu"] = 60, ["attack_roll_penalty"] = 2 };
        return DamageResolver()
            .resolve_effects(
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

    public GDictionary _build_report_entry(
        MeteorSwarmTargetPlan plan,
        MeteorSwarmCommitResult result,
        GDictionary component_totals
    )
    {
        var componentBreakdown = new GDictArray();
        foreach (string componentKey in ProgressionDataUtils.sorted_string_keys(component_totals))
        {
            GDictionary entryDict = GdInterop.GetDictionary(component_totals, componentKey);
            componentBreakdown.Add((GDictionary)entryDict.Duplicate(true));
        }
        var targetSummaries = new GDictArray();
        foreach (MeteorSwarmTargetOutcome targetOutcome in result.target_outcomes)
        {
            if (targetOutcome != null)
            {
                targetSummaries.Add(targetOutcome.to_summary_dict());
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
            ["terrain_summary"] = _build_terrain_summary(plan),
        };
        GStringArray summaryLines = _reportFormatter.FormatMeteorSwarmSummary(entry);
        if (summaryLines.Count != 0)
        {
            entry["text"] = summaryLines[0];
        }
        return entry;
    }

    public GDictionary _build_component_report_fact(
        MeteorSwarmImpactComponent component,
        GDictionary damage_result,
        int distance_from_anchor
    )
    {
        return new GDictionary
        {
            ["component_id"] = component.component_id.ToString(),
            ["role_label"] = component.role_label.ToString(),
            ["damage_tag"] = component.damage_tag.ToString(),
            ["distance_from_anchor"] = distance_from_anchor,
            ["damage"] = GdInterop.GetInt(damage_result, "damage", 0),
            ["healing"] = GdInterop.GetInt(damage_result, "healing", 0),
            ["damage_events"] = GdInterop.GetArray(damage_result, "damage_events").Duplicate(true),
        };
    }

    public void _add_component_total(GDictionary component_totals, GDictionary component_fact)
    {
        StringName componentId = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(component_fact, "component_id")
        );
        if (GdInterop.IsEmpty(componentId))
        {
            return;
        }
        GDictionary existing = component_totals.ContainsKey(componentId)
            ? component_totals[componentId].AsGodotDictionary()
            : new GDictionary
            {
                ["component_id"] = componentId.ToString(),
                ["role_label"] = GdInterop.GetString(component_fact, "role_label"),
                ["damage_tag"] = GdInterop.GetString(component_fact, "damage_tag"),
                ["damage"] = 0,
                ["healing"] = 0,
            };
        existing["damage"] =
            GdInterop.GetInt(existing, "damage", 0) + GdInterop.GetInt(component_fact, "damage", 0);
        existing["healing"] =
            GdInterop.GetInt(existing, "healing", 0)
            + GdInterop.GetInt(component_fact, "healing", 0);
        component_totals[componentId] = existing;
    }

    public void _tag_damage_events(
        GDictionary damage_result,
        MeteorSwarmImpactComponent component,
        int distance_from_anchor
    )
    {
        foreach (GDictionary eventDict in GdInterop.ReadDictionaryItems(
            GdInterop.GetArray(damage_result, "damage_events")
        ))
        {
            eventDict["meteor_component_id"] = component.component_id.ToString();
            eventDict["role_label"] = component.role_label.ToString();
            eventDict["distance_from_anchor"] = distance_from_anchor;
        }
    }

    public GDictionary _build_terrain_summary(MeteorSwarmTargetPlan plan)
    {
        int craterCount = 0;
        int rubbleCount = 0;
        int dustCount = 0;
        int terrainEffectCount = 0;
        if (plan == null || plan.profile == null)
        {
            return new GDictionary();
        }
        foreach (Vector2I coord in plan.affected_coords)
        {
            int ring = plan.get_ring_for_coord(coord);
            foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, ring))
            {
                terrainEffectCount += 1;
                string profileId = GdInterop.GetString(
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
        return new GDictionary
        {
            ["coverage_shape_id"] = plan.coverage_shape_id.ToString(),
            ["radius"] = plan.radius,
            ["affected_coord_count"] = plan.affected_coords.Count,
            ["terrain_effect_count"] = terrainEffectCount,
            ["crater_count"] = craterCount,
            ["rubble_count"] = rubbleCount,
            ["dust_count"] = dustCount,
        };
    }

    public GDictArray _build_friendly_fire_numeric_summary(MeteorSwarmTargetPlan plan)
    {
        var summaries = new GDictArray();
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
            summaries.Add(_build_friendly_fire_summary_for_unit(plan, targetUnit));
        }
        return summaries;
    }

    public GDictArray _build_target_numeric_summary(MeteorSwarmTargetPlan plan)
    {
        var summaries = new GDictArray();
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
            summaries.Add(_build_friendly_fire_summary_for_unit(plan, targetUnit));
        }
        return summaries;
    }

    public GDictionary _build_friendly_fire_summary_for_unit(
        MeteorSwarmTargetPlan plan,
        BattleUnitState target_unit
    )
    {
        int distance = plan.get_distance_for_unit(target_unit.unit_id);
        bool coversCenter = _unit_covers_coord(target_unit, plan.final_anchor_coord);
        var componentBreakdown = new GDictArray();
        int expectedDamage = 0;
        int worstCaseDamage = 0;
        BattleUnitState expectedSourcePreview =
            plan.source_unit != null ? plan.source_unit.clone() : null;
        BattleUnitState worstSourcePreview =
            plan.source_unit != null ? plan.source_unit.clone() : null;
        BattleUnitState expectedTargetPreview = target_unit.clone();
        BattleUnitState worstTargetPreview = target_unit.clone();
        var resistanceTiers = new GDictionary();
        int guardBlockEstimate = 0;
        foreach (MeteorSwarmImpactComponent component in plan.profile.impact_components)
        {
            if (component == null || !component.applies_to_distance(distance, coversCenter))
            {
                continue;
            }
            CombatEffectDef effectDef = _build_damage_effect_def(component, distance);
            GDictionary expectedPreview = _build_component_damage_preview(
                plan,
                expectedSourcePreview,
                expectedTargetPreview,
                effectDef,
                DAMAGE_PREVIEW_ROLL_MODE_AVERAGE,
                DAMAGE_PREVIEW_SAVE_MODE_EXPECTED
            );
            GDictionary worstPreview = _build_component_damage_preview(
                plan,
                worstSourcePreview,
                worstTargetPreview,
                effectDef,
                DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM,
                DAMAGE_PREVIEW_SAVE_MODE_WORST
            );
            GDictionary expectedOutcome = GdInterop.GetDictionary(
                expectedPreview,
                "damage_outcome"
            );
            StringName resistanceTier = ProgressionDataUtils.to_string_name(
                GdInterop.GetStringName(expectedOutcome, "mitigation_tier", MITIGATION_TIER_NORMAL)
            );
            resistanceTiers[component.damage_tag.ToString()] = resistanceTier.ToString();
            guardBlockEstimate = Math.Max(
                guardBlockEstimate,
                GdInterop.GetInt(expectedOutcome, "guard_block", 0)
            );
            int preSaveExpectedDamage = GdInterop.GetInt(expectedPreview, "pre_save_damage", 0);
            int preSaveWorstDamage = GdInterop.GetInt(worstPreview, "pre_save_damage", 0);
            int expectedComponentDamage = GdInterop.GetInt(
                expectedPreview,
                "post_save_damage",
                preSaveExpectedDamage
            );
            int worstComponentDamage = GdInterop.GetInt(
                worstPreview,
                "post_save_damage",
                preSaveWorstDamage
            );
            int expectedAfterShield = GdInterop.GetInt(
                expectedPreview,
                "hp_damage",
                expectedComponentDamage
            );
            int worstAfterShield = GdInterop.GetInt(
                worstPreview,
                "hp_damage",
                worstComponentDamage
            );
            expectedDamage += expectedAfterShield;
            worstCaseDamage += worstAfterShield;
            var nextExpectedSource =
                GdInterop.GetObject(expectedPreview, "source_preview_after") as BattleUnitState;
            var nextExpectedTarget =
                GdInterop.GetObject(expectedPreview, "target_preview_after") as BattleUnitState;
            var nextWorstSource =
                GdInterop.GetObject(worstPreview, "source_preview_after") as BattleUnitState;
            var nextWorstTarget =
                GdInterop.GetObject(worstPreview, "target_preview_after") as BattleUnitState;
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
                new GDictionary
                {
                    ["component_id"] = component.component_id.ToString(),
                    ["role_label"] = component.role_label.ToString(),
                    ["damage_tag"] = component.damage_tag.ToString(),
                    ["expected_damage"] = expectedAfterShield,
                    ["worst_case_damage"] = worstAfterShield,
                    ["post_save_expected_damage"] = expectedComponentDamage,
                    ["post_save_worst_case_damage"] = worstComponentDamage,
                    ["pre_save_expected_damage"] = preSaveExpectedDamage,
                    ["pre_save_worst_case_damage"] = preSaveWorstDamage,
                    ["resistance_tier"] = resistanceTier.ToString(),
                    ["save_profile_id"] = component.save_profile_id.ToString(),
                    ["save_estimate"] = GdInterop
                        .GetDictionary(expectedPreview, "save_estimate")
                        .Duplicate(true),
                    ["worst_save_estimate"] = GdInterop
                        .GetDictionary(worstPreview, "save_estimate")
                        .Duplicate(true),
                    ["mitigation_sources"] = GdInterop.GetArray(
                        expectedOutcome,
                        "mitigation_sources"
                    ),
                    ["fixed_mitigation_sources"] = GdInterop.GetArray(
                        expectedOutcome,
                        "fixed_mitigation_sources"
                    ),
                    ["shield_absorbed_estimate"] = GdInterop.GetInt(
                        expectedPreview,
                        "shield_absorbed",
                        0
                    ),
                    ["shield_absorbed_worst"] = GdInterop.GetInt(
                        worstPreview,
                        "shield_absorbed",
                        0
                    ),
                }
            );
        }
        var statusEffectIds = new GStringNameArray();
        int apPenalty = 0;
        if (distance <= 1)
        {
            statusEffectIds.Add(plan.profile.concussed_status_id);
            apPenalty = 1;
        }
        int maxHp = _get_unit_max_hp(target_unit);
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
        return new GDictionary
        {
            ["candidate_anchor_coord"] = plan.final_anchor_coord,
            ["target_unit_id"] = target_unit.unit_id.ToString(),
            ["ally_unit_id"] = target_unit.unit_id.ToString(),
            ["target_faction_id"] = target_unit.faction_id.ToString(),
            ["is_ally"] = isAlly,
            ["distance_from_anchor"] = distance,
            ["component_expected_damage"] = expectedDamage,
            ["component_worst_case_damage"] = worstCaseDamage,
            ["component_breakdown"] = componentBreakdown,
            ["lethal_probability_percent"] = worstCaseDamage >= currentHp ? 100 : 0,
            ["save_profile_ids"] = _collect_component_save_profile_ids(componentBreakdown),
            ["resistance_tiers_by_damage_tag"] = resistanceTiers,
            ["shield_hp"] = target_unit.current_shield_hp,
            ["guard_block_estimate"] = guardBlockEstimate,
            ["status_effect_ids"] = (GStringNameArray)statusEffectIds.Duplicate(),
            ["ap_penalty"] = apPenalty,
            ["hostile_terrain_consequence"] = _build_hostile_terrain_consequence(plan, distance),
            ["expected_damage_hp_percent"] = expectedHpPercent,
            ["worst_case_damage_hp_percent"] = worstHpPercent,
            ["hard_reject"] = hardReject,
            ["soft_penalty"] =
                !hardReject
                && expectedHpPercent > plan.profile.friendly_fire_soft_expected_hp_percent,
        };
    }

    public GDictArray _build_future_attack_roll_modifier_breakdown(MeteorSwarmTargetPlan plan)
    {
        var breakdown = new GDictArray();
        if (plan == null || plan.profile == null)
        {
            return breakdown;
        }
        foreach (GDictionary terrainProfile in GdInterop.ReadDictionaryItems(plan.profile.terrain_profiles))
        {
            GDictionary accuracySpec = GdInterop.GetDictionary(
                terrainProfile,
                "accuracy_modifier_spec"
            );
            if (accuracySpec.Count == 0)
            {
                continue;
            }
            GDictionary spec = (GDictionary)accuracySpec.Duplicate(true);
            spec["source_instance_id"] = GdInterop.GetString(terrainProfile, "terrain_profile_id");
            spec["effective_modifier_delta"] = GdInterop.GetInt(spec, "modifier_delta", 0);
            breakdown.Add(spec);
        }
        return breakdown;
    }

    public GDictArray _build_component_preview(MeteorSwarmTargetPlan plan)
    {
        var preview = new GDictArray();
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
            preview.Add(component.to_component_fact(0));
        }
        return preview;
    }

    public int _count_expected_terrain_effects(MeteorSwarmTargetPlan plan)
    {
        return GdInterop.GetInt(_build_terrain_summary(plan), "terrain_effect_count", 0);
    }

    public int _resolve_friendly_fire_risk_percent(GDictArray summaries)
    {
        if (summaries.Count == 0)
        {
            return 0;
        }
        int hardCount = 0;
        foreach (GDictionary summary in summaries)
        {
            if (GdInterop.GetBool(summary, "hard_reject", false))
            {
                hardCount += 1;
            }
        }
        return Mathf.RoundToInt((float)hardCount * 100.0f / summaries.Count);
    }

    public GDictionary _build_hostile_terrain_consequence(
        MeteorSwarmTargetPlan plan,
        int distance_from_anchor
    )
    {
        var consequence = new GDictionary
        {
            ["move_cost_delta"] = 0,
            ["creates_dust"] = false,
            ["creates_crater"] = false,
            ["creates_rubble"] = false,
        };
        foreach (GDictionary terrainProfile in TerrainProfilesForRing(plan.profile, distance_from_anchor))
        {
            consequence["move_cost_delta"] = Math.Max(
                GdInterop.GetInt(consequence, "move_cost_delta", 0),
                GdInterop.GetInt(terrainProfile, "move_cost_delta", 0)
            );
            string profileId = GdInterop.GetString(terrainProfile, "terrain_profile_id");
            if (profileId.Contains("dust"))
            {
                consequence["creates_dust"] = true;
            }
            if (profileId.Contains("crater"))
            {
                consequence["creates_crater"] = true;
            }
            if (profileId.Contains("rubble"))
            {
                consequence["creates_rubble"] = true;
            }
        }
        return consequence;
    }

    public GStringArray _collect_component_save_profile_ids(GDictArray component_breakdown)
    {
        var ids = new GStringArray();
        foreach (GDictionary component in component_breakdown)
        {
            string saveProfileId = GdInterop.GetString(component, "save_profile_id");
            if (!string.IsNullOrEmpty(saveProfileId) && !ids.Contains(saveProfileId))
            {
                ids.Add(saveProfileId);
            }
        }
        return ids;
    }

    public void _apply_save_profile_to_damage_effect(
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
            effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_CASTER_SPELL();
            effect.save_dc_source_ability = "intelligence";
            effect.save_ability = "agility";
            effect.save_partial_on_success = true;
            effect.save_tag = BattleSaveResolver.SAVE_TAG_MAGIC();
        }
    }

    public GDictionary _build_component_damage_preview(
        MeteorSwarmTargetPlan plan,
        BattleUnitState source_preview,
        BattleUnitState target_preview,
        CombatEffectDef effect_def,
        StringName roll_mode,
        StringName save_mode
    )
    {
        if (
            _runtime == null
            || source_preview == null
            || target_preview == null
            || effect_def == null
        )
        {
            return new GDictionary();
        }
        BattleDamageResolver damageResolver = DamageResolver();
        if (damageResolver == null)
        {
            return new GDictionary();
        }
        return preview_damage_effect(
            damageResolver,
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

    private static GDictionary preview_damage_effect(
        BattleDamageResolver damageResolver,
        BattleUnitState sourcePreview,
        BattleUnitState targetPreview,
        CombatEffectDef effectDef,
        GDictionary context,
        StringName rollMode,
        StringName saveMode
    )
    {
        if (damageResolver == null)
        {
            return new GDictionary();
        }
        return damageResolver.preview_damage_effect(
            sourcePreview,
            targetPreview,
            effectDef,
            context,
            rollMode,
            saveMode
        );
    }

    public void _populate_unit_distances(MeteorSwarmTargetPlan plan)
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
            targetUnit.refresh_footprint();
            int bestDistance = 999999;
            Vector2I bestCoord = plan.get_primary_coord_for_unit(targetUnitId);
            GVector2IArray occupiedCoords = targetUnit.occupied_coords;
            if (occupiedCoords.Count == 0)
            {
                occupiedCoords = gridService.get_unit_target_coords(targetUnit, targetUnit.coord);
            }
            foreach (Vector2I coord in occupiedCoords)
            {
                if (!plan.ring_by_coord.ContainsKey(coord))
                {
                    continue;
                }
                int distance = plan.get_ring_for_coord(coord);
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

    public string _build_plan_signature_for_anchor(
        MeteorSwarmTargetPlan plan,
        Vector2I anchor_coord
    )
    {
        if (anchor_coord == plan.final_anchor_coord)
        {
            return _build_plan_signature(plan, anchor_coord);
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
                    if (gridService.is_inside(state, coord))
                    {
                        affectedCount += 1;
                    }
                }
            }
        }
        return $"{plan.skill_id}:{plan.coverage_shape_id}:r{plan.radius}:{anchor_coord.X},{anchor_coord.Y}:{affectedCount}";
    }

    public string _build_plan_signature(MeteorSwarmTargetPlan plan, Vector2I anchor_coord)
    {
        var unitParts = new List<string>();
        foreach (StringName unitId in plan.target_unit_ids)
        {
            unitParts.Add(unitId.ToString());
        }
        return $"{plan.skill_id}:{plan.coverage_shape_id}:r{plan.radius}:{anchor_coord.X},{anchor_coord.Y}:{plan.affected_coords.Count}:{string.Join(",", unitParts)}";
    }

    public GVector2IArray _extract_target_coords(GDictionary validation)
    {
        var targetCoords = new GVector2IArray();
        foreach (Vector2I coord in GdInterop.GetArray(validation, "target_coords"))
        {
            targetCoords.Add(coord);
        }
        return targetCoords;
    }

    public MeteorSwarmProfile _resolve_profile()
    {
        if (_runtime == null)
        {
            return null;
        }
        GDictionary snapshot = _runtime._special_profile_registry_snapshot;
        GDictionary profiles = GdInterop.GetDictionary(snapshot, "profiles");
        GDictionary meteorProfileSnapshot = GdInterop.GetDictionary(profiles, "meteor_swarm");
        if (meteorProfileSnapshot.Count == 0)
        {
            return null;
        }
        return GdInterop.GetObject(meteorProfileSnapshot, "profile_resource") as MeteorSwarmProfile;
    }

    public CombatCastVariantDef _resolve_ground_cast_variant(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def
    )
    {
        if (_runtime == null || skill_def == null)
        {
            return null;
        }
        CombatCastVariantDef castVariant = _runtime._resolve_ground_cast_variant(
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
            && BattleTypedNames.ToTargetMode(skill_def.combat_profile.target_mode)
                == BattleTargetMode.Ground
        )
        {
            return _runtime._build_implicit_ground_cast_variant(skill_def);
        }
        return null;
    }

    public bool _unit_covers_coord(BattleUnitState unit_state, Vector2I coord)
    {
        if (unit_state == null)
        {
            return false;
        }
        unit_state.refresh_footprint();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            if (occupiedCoord == coord)
            {
                return true;
            }
        }
        return unit_state.coord == coord;
    }

    public int _get_unit_max_hp(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 1;
        }
        if (unit_state.attribute_snapshot != null)
        {
            int maxHp = unit_state
                .attribute_snapshot.get_value(new StringName("hp_max"));
            if (maxHp > 0)
            {
                return maxHp;
            }
        }
        return Math.Max(unit_state.current_hp, 1);
    }

    public string _terrain_profile_display_name(StringName terrain_profile_id)
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

    private static void SortCoordArrayAscending(GVector2IArray source, out GVector2IArray sorted)
    {
        var list = new List<Vector2I>();
        foreach (Vector2I c in source)
        {
            list.Add(c);
        }
        list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        sorted = new GVector2IArray();
        foreach (Vector2I c in list)
        {
            sorted.Add(c);
        }
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
                Vector2I lc = plan.get_primary_coord_for_unit(l);
                Vector2I rc = plan.get_primary_coord_for_unit(r);
                return lc.Y != rc.Y ? lc.Y.CompareTo(rc.Y) : lc.X.CompareTo(rc.X);
            }
        );
        var newIds = new GStringNameArray();
        foreach (StringName id in ids)
        {
            newIds.Add(id);
        }
        plan.target_unit_ids = newIds;
    }

    private BattleState State()
    {
        return _runtime?._state;
    }

    private BattleGridService GridService()
    {
        return _runtime?.get_grid_service();
    }

    private BattleDamageResolver DamageResolver()
    {
        return _runtime?.get_damage_resolver();
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

    private static IEnumerable<GDictionary> TerrainProfilesForRing(
        MeteorSwarmProfile profile,
        int ring
    )
    {
        if (profile == null)
        {
            yield break;
        }
        foreach (GDictionary profileValue in GdInterop.ReadDictionaryItems(
            profile.get_terrain_profiles_for_ring(ring)
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
            if (!GdInterop.IsEmpty(statusId))
            {
                yield return statusId;
            }
        }
    }
}

using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_meteor_swarm_special_profile_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestTargetPlanUsesSquare7x7AndEdgeClipping();
        TestPreviewAndExecuteUseTypedProfileNotLegacyArea();
        TestMeteorAttemptMetricsStartAfterRuntimeValidation();
        TestMeteorSwarmTerrainPayloadSurface();
        TestMeteorSwarmDriftChangesFinalAnchorAndTerrain();

        if (_failures.Count == 0)
        {
            GD.Print("Meteor swarm special profile regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Meteor swarm special profile regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTargetPlanUsesSquare7x7AndEdgeClipping()
    {
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), System.Array.Empty<BattleUnitState>());
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleMeteorSwarmResolver resolver = setup.Runtime._meteor_swarm_resolver;
        MeteorSwarmTargetPlan centerPlan = resolver.build_target_plan(
            resolver.build_cast_context(
                setup.Caster,
                BuildCommand(setup.Caster, new Vector2I(4, 4)),
                skillDef,
                null,
                new Vector2I(4, 4),
                new Vector2I(4, 4)
            )
        ) as MeteorSwarmTargetPlan;
        AssertEq(centerPlan.affected_coords.Count, 49, "开放棋盘中心陨星雨应覆盖 7x7 共 49 格。");
        AssertEq(centerPlan.get_ring_for_coord(new Vector2I(1, 1)), 3, "最外层 d==3 应使用 Chebyshev ring。");

        MeteorSwarmTargetPlan edgePlan = resolver.build_target_plan(
            resolver.build_cast_context(
                setup.Caster,
                BuildCommand(setup.Caster, new Vector2I(0, 4)),
                skillDef,
                null,
                new Vector2I(0, 4),
                new Vector2I(0, 4)
            )
        ) as MeteorSwarmTargetPlan;
        AssertEq(edgePlan.affected_coords.Count, 28, "贴边中心应裁剪为 4x7 共 28 格。");

        MeteorSwarmTargetPlan cornerPlan = resolver.build_target_plan(
            resolver.build_cast_context(
                setup.Caster,
                BuildCommand(setup.Caster, Vector2I.Zero),
                skillDef,
                null,
                Vector2I.Zero,
                Vector2I.Zero
            )
        ) as MeteorSwarmTargetPlan;
        AssertEq(cornerPlan.affected_coords.Count, 16, "角落中心应裁剪为 4x4 共 16 格。");
    }

    private void TestPreviewAndExecuteUseTypedProfileNotLegacyArea()
    {
        BattleUnitState enemyCenter = BuildUnit("enemy_center", "中心敌人", "enemy", new Vector2I(4, 4), 160);
        BattleUnitState enemyOuter = BuildUnit("enemy_outer", "外圈敌人", "enemy", new Vector2I(7, 7), 160);
        BattleUnitState allyInner = BuildUnit("ally_inner", "内圈友军", "player", new Vector2I(5, 4), 160);
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, enemyOuter, allyInner });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        skillDef.combat_profile.area_pattern = "diamond";
        skillDef.combat_profile.area_value = 1;
        BattleCommand command = BuildCommand(setup.Caster, new Vector2I(4, 4));
        BattlePreview preview = setup.Runtime.preview_command(command);

        AssertTrue(preview != null && preview.allowed, "陨星雨 typed preview 应可用。");
        AssertTrue(preview.special_profile_preview_facts != null, "preview 应暴露 special_profile_preview_facts。");
        AssertEq(preview.target_coords.Count, 49, "poisoned legacy area_value 不应改变 typed 7x7 target plan。");
        AssertTrue(preview.target_unit_ids.Contains(enemyCenter.unit_id), "preview 应包含中心敌人。");
        AssertTrue(preview.target_unit_ids.Contains(enemyOuter.unit_id), "preview 应包含最外层敌人。");
        AssertTrue(preview.target_unit_ids.Contains(allyInner.unit_id), "preview 友伤应走同一份全量 target plan。");
        AssertTrue(
            preview.special_profile_preview_facts.get_friendly_fire_numeric_summary().Count == 1,
            "友军波及时应输出 numeric friendly fire summary。"
        );

        GArray targetSummaries = preview.special_profile_preview_facts.to_dict()
            .GetValueOrDefault("target_numeric_summary", new GArray())
            .AsGodotArray();
        GDictionary centerSummary = FindTargetSummary(targetSummaries, enemyCenter.unit_id);
        AssertTrue(centerSummary.Count != 0, "中心敌人的 numeric summary 应存在。");
        GDictionary fireComponent = FindComponentSummary(
            centerSummary.GetValueOrDefault("component_breakdown", new GArray()),
            "area_blast_fire"
        );
        AssertTrue(fireComponent.Count != 0, "area_blast_fire component summary 应存在。");
        GDictionary saveEstimate = fireComponent.GetValueOrDefault("save_estimate", new GDictionary()).AsGodotDictionary();
        AssertTrue(DictBool(saveEstimate, "has_save", false), "meteor_dex_half component preview 应计算豁免概率。");
        AssertEq(DictString(saveEstimate, "ability", ""), "agility", "meteor_dex_half 应使用敏捷豁免。");
        AssertTrue(
            DictBool(saveEstimate, "save_partial_on_success", false),
            "meteor_dex_half 成功豁免应保留半伤。"
        );
        BattleEventBatch batch = setup.Runtime.issue_command(command);
        AssertTrue(
            batch != null && batch.report_entries.Count >= 1,
            $"execute 应写入陨星雨聚合战报。logs={FormatLogs(batch?.log_lines)}"
        );
        if (batch == null || batch.report_entries.Count == 0)
        {
            return;
        }
        AssertTrue(enemyCenter.current_hp < 160, "中心敌人应受到 typed component 伤害。");
        AssertTrue(enemyOuter.current_hp < 160, "最外层敌人也应受到灾害波及伤害。");
        AssertTrue(allyInner.current_hp < 160, "友军应走全量数值结算，不应免友伤。");
        AssertTrue(allyInner.has_status_effect("meteor_concussed"), "内环友军同样应按全量结算获得震眩。");

        BattleCellState centerCell = Cell(setup.Runtime.get_state(), new Vector2I(4, 4));
        BattleCellState outerCell = Cell(setup.Runtime.get_state(), new Vector2I(7, 7));
        AssertTrue(centerCell != null && centerCell.timed_terrain_effects.Count >= 3, "中心格应留下陨坑/碎石/尘土地形效果。");
        AssertTrue(outerCell != null && outerCell.timed_terrain_effects.Count >= 1, "最外层应留下碎石地形效果。");
        GDictionary summaryEntry = batch.report_entries[0].AsGodotDictionary();
        AssertEq(DictString(summaryEntry, "entry_type", ""), "meteor_swarm_impact_summary", "战报应使用 meteor_swarm_impact_summary。");
        AssertEq(
            DictString(summaryEntry, "nominal_plan_signature", ""),
            DictString(summaryEntry, "final_plan_signature", ""),
            "无漂移时 final plan signature 应等于 nominal。"
        );
    }

    private void TestMeteorAttemptMetricsStartAfterRuntimeValidation()
    {
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), System.Array.Empty<BattleUnitState>());
        setup.Runtime._initialize_battle_metrics();
        BattleCommand invalidCommand = BuildCommand(setup.Caster, new Vector2I(-1, -1));
        setup.Runtime._skill_orchestrator._handle_skill_command(setup.Caster, invalidCommand, new BattleEventBatch());
        GDictionary casterMetrics = setup.Runtime.get_battle_metrics()
            .GetValueOrDefault("units", new GDictionary())
            .AsGodotDictionary()
            .GetValueOrDefault(setup.Caster.unit_id.ToString(), new GDictionary())
            .AsGodotDictionary();
        GDictionary attemptCounts = casterMetrics.GetValueOrDefault("skill_attempt_counts", new GDictionary()).AsGodotDictionary();
        AssertEq(DictInt(attemptCounts, "mage_meteor_swarm", 0), 0, "陨星雨运行期校验失败不应记录 skill attempt。");

        BattleCommand validCommand = BuildCommand(setup.Caster, new Vector2I(4, 4));
        setup.Runtime._skill_orchestrator._handle_skill_command(setup.Caster, validCommand, new BattleEventBatch());
        AssertEq(DictInt(attemptCounts, "mage_meteor_swarm", 0), 1, "陨星雨通过校验并完成扣费后才记录 skill attempt。");
    }

    private void TestMeteorSwarmTerrainPayloadSurface()
    {
        BattleUnitState enemyCenter = BuildUnit("enemy_center", "中心敌人", "enemy", new Vector2I(4, 4), 160);
        BattleUnitState enemyOuter = BuildUnit("enemy_outer", "外圈敌人", "enemy", new Vector2I(7, 7), 160);
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, enemyOuter });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        skillDef.combat_profile.area_pattern = "diamond";
        skillDef.combat_profile.area_value = 1;
        setup.Runtime.issue_command(BuildCommand(setup.Caster, new Vector2I(4, 4)));

        BattleCellState centerCell = Cell(setup.Runtime.get_state(), new Vector2I(4, 4));
        AssertTrue(centerCell != null, "中心格应存在。");
        int craterCount = 0;
        int dustCount = 0;
        foreach (BattleTerrainEffectState terrainEffect in centerCell.timed_terrain_effects)
        {
            GDictionary terrainParams = terrainEffect?.@params ?? new GDictionary();
            if (terrainParams.Count == 0)
            {
                continue;
            }
            string lifetimePolicy = DictString(terrainParams, "lifetime_policy", "");
            string renderOverlay = DictString(terrainParams, "render_overlay_id", "");
            AssertTrue(!string.IsNullOrEmpty(lifetimePolicy), "地形效果必须声明 lifetime_policy。");
            AssertTrue(!string.IsNullOrEmpty(renderOverlay), "地形效果必须声明 render_overlay_id。");
            if (renderOverlay.StartsWith("meteor_crater", System.StringComparison.Ordinal))
            {
                craterCount += 1;
                AssertEq(lifetimePolicy, "battle", "陨坑 lifetime_policy 应为 battle。");
            }
            else if (renderOverlay == "meteor_dust_cloud")
            {
                dustCount += 1;
                AssertEq(lifetimePolicy, "timed", "尘土 lifetime_policy 应为 timed。");
                GDictionary accuracySpec = terrainParams.GetValueOrDefault("accuracy_modifier_spec", new GDictionary()).AsGodotDictionary();
                AssertTrue(accuracySpec.Count != 0, "尘土必须声明 accuracy_modifier_spec。");
            }
        }
        AssertTrue(craterCount >= 1, "中心格应至少有 1 个陨坑地形效果。");
        AssertTrue(dustCount >= 1, "中心格应至少有 1 个尘土地形效果。");
    }

    private void TestMeteorSwarmDriftChangesFinalAnchorAndTerrain()
    {
        BattleUnitState target = BuildUnit("drift_target", "漂移目标", "enemy", new Vector2I(4, 4), 5);
        Fixture setup = BuildRuntimeFixture(new Vector2I(11, 11), new[] { target });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleMeteorSwarmResolver resolver = setup.Runtime._meteor_swarm_resolver;
        Vector2I nominalAnchor = new(4, 4);
        Vector2I driftedAnchor = new(5, 5);
        MeteorSwarmTargetPlan plan = resolver.build_target_plan(
            resolver.build_cast_context(
                setup.Caster,
                BuildCommand(setup.Caster, nominalAnchor),
                skillDef,
                null,
                nominalAnchor,
                driftedAnchor
            )
        ) as MeteorSwarmTargetPlan;

        AssertTrue(plan.drift_applied, "漂移后 plan 应标记 drift_applied = true。");
        AssertEq(plan.drift_from_coord, nominalAnchor, "drift_from_coord 应等于原始 nominal anchor。");
        AssertEq(plan.final_anchor_coord, driftedAnchor, "final_anchor_coord 应为漂移后坐标。");
        AssertTrue(plan.nominal_plan_signature != plan.final_plan_signature, "漂移后 nominal 和 final plan signature 应不同。");
        MeteorSwarmCommitResult result = resolver.resolve(plan) as MeteorSwarmCommitResult;
        AssertTrue(result.target_outcomes.Count >= 1, "漂移后仍应命中目标。");
        AssertTrue(result.defeated_unit_ids.Contains(target.unit_id), "漂移后伤害应照常击败目标。");
        if (result.report_entries.Count == 0)
        {
            AssertTrue(false, "漂移结算应生成战报。");
            return;
        }
        GDictionary reportEntry = result.report_entries[0].AsGodotDictionary();
        AssertEq(DictString(reportEntry, "nominal_plan_signature", ""), plan.nominal_plan_signature, "战报 nominal_plan_signature 应匹配。");
        AssertEq(DictString(reportEntry, "final_plan_signature", ""), plan.final_plan_signature, "战报 final_plan_signature 应匹配漂移后值。");
    }

    private Fixture BuildRuntimeFixture(Vector2I mapSize, BattleUnitState[] extraUnits)
    {
        var progressionRegistry = new ProgressionContentRegistry();
        GDictionary skillDefs = progressionRegistry.get_skill_defs();
        var specialRegistry = new BattleSpecialProfileRegistry();
        specialRegistry.rebuild(skillDefs);
        AssertTrue(specialRegistry.validate().Count == 0, "正式 special profile registry 应可用于 runtime fixture。");
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            skillDefs,
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            new GDictionary(),
            null,
            default,
            specialRegistry.get_snapshot()
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        BattleState state = BuildState(mapSize);
        BattleUnitState caster = BuildUnit("meteor_caster", "陨星术者", "player", new Vector2I(4, 0), 180);
        caster.known_active_skill_ids.Add("mage_meteor_swarm");
        caster.known_skill_level_map[new StringName("mage_meteor_swarm")] = 9;
        caster.current_ap = 4;
        caster.current_mp = 200;
        caster.current_aura = 3;
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        state.units[caster.unit_id] = caster;
        state.ally_unit_ids.Add(caster.unit_id);
        foreach (BattleUnitState unit in extraUnits)
        {
            if (unit == null)
            {
                continue;
            }
            state.units[unit.unit_id] = unit;
            if (unit.faction_id == caster.faction_id)
            {
                state.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                state.enemy_unit_ids.Add(unit.unit_id);
            }
        }
        state.active_unit_id = caster.unit_id;
        foreach (Variant unitValue in state.units.Values)
        {
            BattleUnitState unitState = unitValue.AsGodotObject() as BattleUnitState;
            AssertTrue(
                runtime._grid_service.place_unit(state, unitState, unitState.coord, true),
                $"单位应能放入陨星雨测试棋盘：{unitState?.unit_id}"
            );
        }
        runtime._state = state;
        return new Fixture
        {
            Runtime = runtime,
            Caster = caster,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "meteor_swarm_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.cells[coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = hp,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hp);
        SeedBaseAttributesAndDeriveAc(unit);
        unit.refresh_footprint();
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        StringName[] baseAttributes =
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        };
        foreach (StringName attributeId in baseAttributes)
        {
            if (!unit.attribute_snapshot.has_value(attributeId))
            {
                unit.attribute_snapshot.set_value(attributeId, 10);
            }
        }
        if (!unit.attribute_snapshot.has_value(AttributeService.ARMOR_CLASS_ID()))
        {
            int agilityModifier = AttributeSnapshot.calculate_score_modifier(
                unit.attribute_snapshot.get_value("agility")
            );
            unit.attribute_snapshot.set_value(
                AttributeService.ARMOR_CLASS_ID(),
                System.Math.Clamp(AttributeService.BASE_ARMOR_CLASS_VALUE() + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I anchorCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = caster.unit_id,
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.target_coords.Add(anchorCoord);
        return command;
    }

    private static GDictionary FindTargetSummary(GArray summaries, StringName targetUnitId)
    {
        foreach (Variant summaryValue in summaries)
        {
            GDictionary summary = summaryValue.AsGodotDictionary();
            if (DictString(summary, "target_unit_id", "") == targetUnitId.ToString())
            {
                return summary;
            }
        }
        return new GDictionary();
    }

    private static GDictionary FindComponentSummary(Variant components, string componentId)
    {
        if (components.VariantType != Variant.Type.Array)
        {
            return new GDictionary();
        }
        foreach (Variant componentValue in components.AsGodotArray())
        {
            GDictionary component = componentValue.AsGodotDictionary();
            if (DictString(component, "component_id", "") == componentId)
            {
                return component;
            }
        }
        return new GDictionary();
    }

    private static BattleCellState Cell(BattleState state, Vector2I coord)
    {
        if (state == null || !state.cells.ContainsKey(coord))
        {
            return null;
        }
        return state.cells[coord].AsGodotObject() as BattleCellState;
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
        {
            return null;
        }
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private static bool DictBool(GDictionary dictionary, Variant key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, Variant key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static string DictString(GDictionary dictionary, Variant key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }

    private static string FormatLogs(GStringArray logLines)
    {
        return logLines == null ? "" : string.Join("|", logLines);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleUnitState Caster;
        public GDictionary SkillDefs;
    }
}

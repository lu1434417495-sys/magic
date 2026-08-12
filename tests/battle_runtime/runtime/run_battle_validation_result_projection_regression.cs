using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_validation_result_projection_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();

        TestUnitSkillValidationProjectsTypedLists();
        TestGroundSkillValidationParsesAndProjectsStringKeyPayload();
        TestAttackEffectResolutionReaderRequiresTypedCritLock();
        TestRuntimeUnitSkillEffectTypedProjectionPreservesCritLock();
        TestTargetCollectionSortsAndProjectsCoords();
        RequestTestExit(_test.Finish("Battle validation result projection regression"));
    }

    private void TestUnitSkillValidationProjectsTypedLists()
    {
        var targetUnit = new BattleUnitState { unit_id = "target_1" };
        try
        {
            BattleUnitSkillValidationResult result = BattleUnitSkillValidationResult.AllowedResult(
                new[] { new StringName("target_1") },
                new[] { targetUnit },
                new[] { new StringName("chain_1") },
                new[] { new Vector2I(2, 3) },
                "ok"
            );

            using GodotProjectionLease<Godot.Collections.Dictionary> payloadLease =
                BattleValidationResultProjection.ProjectUnitSkillLease(result);
            Godot.Collections.Dictionary payload = payloadLease.Value;

            _test.True(payload["allowed"].AsBool(), "单位技能 validation 应投影 allowed。");
            _test.Eq(payload["message"].AsString(), "ok", "单位技能 validation 应投影 message。");
            _test.Eq(
                payload["target_unit_ids"].AsGodotArray<StringName>()[0],
                new StringName("target_1"),
                "单位技能 validation 应投影目标 id。"
            );
            Godot.Collections.Dictionary projectedTargetUnit =
                payload["target_units"].AsGodotArray()[0].AsGodotDictionary();
            _test.Eq(
                projectedTargetUnit["unit_id"].AsString(),
                targetUnit.unit_id.ToString(),
                "单位技能 validation 应投影目标 unit。"
            );
            _test.Eq(
                payload["random_chain_candidate_unit_ids"].AsGodotArray<StringName>()[0],
                new StringName("chain_1"),
                "单位技能 validation 应投影随机连锁候选。"
            );
            _test.Eq(
                payload["preview_coords"].AsGodotArray<Vector2I>()[0],
                new Vector2I(2, 3),
                "单位技能 validation 应投影 preview coord。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(targetUnit);
        }
    }

    private void TestGroundSkillValidationParsesAndProjectsStringKeyPayload()
    {
        var source = new Godot.Collections.Dictionary
        {
            ["allowed"] = true,
            ["message"] = "cast",
            ["target_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
            },
            ["preview_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
                new(5, 5),
            },
            ["direction"] = Vector2I.Right,
            ["distance"] = 2,
            ["resolved_anchor_coord"] = new Vector2I(6, 5),
        };

        BattleGroundSkillValidationResult result = BattleGroundSkillValidationResult.FromDictionary(
            source
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> payloadLease =
            BattleValidationResultProjection.ProjectGroundSkillLease(result);
        Godot.Collections.Dictionary payload = payloadLease.Value;

        _test.True(result.Allowed, "地面技能 validation 应从 string-key payload 解析 allowed。");
        _test.Eq(result.Message, "cast", "地面技能 validation 应解析 message。");
        _test.Eq(result.TargetCoords[0], new Vector2I(4, 5), "地面技能 validation 应解析目标格。");
        _test.Eq(result.PreviewCoords[1], new Vector2I(5, 5), "地面技能 validation 应解析 preview 格。");
        _test.Eq(result.Direction, Vector2I.Right, "地面技能 validation 应解析方向。");
        _test.Eq(result.Distance, 2, "地面技能 validation 应解析距离。");
        _test.Eq(
            payload["resolved_anchor_coord"].AsVector2I(),
            new Vector2I(6, 5),
            "地面技能 validation 应投影 resolved anchor。"
        );
    }

    private void TestAttackEffectResolutionReaderRequiresTypedCritLock()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["applied"] = true,
            ["attack_success"] = true,
            ["attack_resolution"] = "hit",
            ["crit_locked"] = true,
            ["critical_hit"] = false,
            ["skill_id"] = "black_contract_push",
        };

        AttackEffectResolutionResult result = AttackEffectResolutionResultReader.ReadResolverResult(
            payload,
            new AttackCheckInput(skillId: "black_contract_push")
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> projectedLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(result);
        Godot.Collections.Dictionary projected = projectedLease.Value;

        _test.False(
            result.AttackCheck.CritLocked,
            "AttackEffectResolutionResultReader 不应从 payload 回填 crit_locked。"
        );
        _test.False(
            projected.GetValueOrDefault("crit_locked", false).AsBool(),
            "AttackEffectResolutionResult payload projection 应只反映 typed AttackCheckInput。"
        );

        AttackEffectResolutionResult typedResult = AttackEffectResolutionResultReader.ReadResolverResult(
            payload,
            new AttackCheckInput(skillId: "black_contract_push", critLocked: true)
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> typedProjectedLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(typedResult);
        Godot.Collections.Dictionary typedProjected = typedProjectedLease.Value;
        _test.True(
            typedResult.AttackCheck.CritLocked,
            "AttackEffectResolutionResultReader 应保留正式 typed AttackCheckInput 的 crit_locked=true。"
        );
        _test.True(
            typedProjected.GetValueOrDefault("crit_locked", false).AsBool(),
            "AttackEffectResolutionResult payload projection 应输出正式 typed crit_locked=true。"
        );
    }

    private void TestTargetCollectionSortsAndProjectsCoords()
    {
        BattleTargetCollectionResult result = BattleTargetCollectionResult.HandledResult(
            new[] { new Vector2I(3, 2), new Vector2I(1, 1), new Vector2I(2, 1) }
        );

        using GodotProjectionLease<Godot.Collections.Dictionary> payloadLease =
            BattleValidationResultProjection.ProjectTargetCollectionLease(result);
        Godot.Collections.Dictionary payload = payloadLease.Value;

        _test.True(result.Handled, "目标收集结果应保留 handled。");
        _test.Eq(result.TargetCoords[0], new Vector2I(1, 1), "目标收集结果应按 y/x 排序。");
        _test.Eq(result.TargetCoords[1], new Vector2I(2, 1), "目标收集结果排序应稳定按 x。");
        _test.Eq(
            payload["target_coords"].AsGodotArray<Vector2I>()[2],
            new Vector2I(3, 2),
            "目标收集结果应投影排序后的 coords。"
        );
    }

    private void TestRuntimeUnitSkillEffectTypedProjectionPreservesCritLock()
    {
        var runtime = new BattleRuntimeModule();
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = _contentSnapshot.Skills;
        runtime.setup(
            null,
            skillDefinitions,
            new Dictionary<StringName, EnemyTemplateDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            null,
            null,
            new Dictionary<StringName, ItemDefinition>(),
            null
        );
        BattleTestFixture.ConfigureDamageResolverForTests(runtime, new DeterministicBattleDamageResolver());
        BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));

        BattleState state = null;
        BattleUnitState source = null;
        BattleUnitState target = null;
        SkillDefinition skillDefinition = null;
        CombatCastVariantDefinition castVariant = null;
        List<CombatEffectDefinition> effects = null;
        try
        {
            state = new BattleState { map_size = new Vector2I(4, 3) };
            source = new BattleUnitState
            {
                unit_id = "source",
                faction_id = "ally",
            }.WithCombatResourcesForTest(
                hp: 40,
                ap: 2
            );
            source.SetAnchorCoord(new Vector2I(0, 0));
            source.SetKnownActiveSkillIds(
                new[] { new StringName("black_contract_push") }
            );
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
            source.attribute_snapshot.SetValue("action_points", 2);
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 12);
            source.SetKnownSkillLevelTyped("black_contract_push", 1);
            target = new BattleUnitState
            {
                unit_id = "target",
                faction_id = "enemy",
            }.WithCombatResourcesForTest(
                hp: 40
            );
            target.SetAnchorCoord(new Vector2I(1, 0));
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 999);
            skillDefinition = skillDefinitions["black_contract_push"];
            castVariant = runtime._skill_resolution_rules.ResolveUnitCastVariantDefinition(
                skillDefinition,
                source,
                "action_tithe"
            );
            effects = runtime._skill_resolution_rules.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                source
            );
            state.SetUnit(source);
            state.SetUnit(target);
            state.ally_unit_ids = new GStringNameArray { source.unit_id };
            state.enemy_unit_ids = new GStringNameArray { target.unit_id };
            state.active_unit_id = source.unit_id;
            runtime.SetupStateForTests(state);

            BattleSkillExecutionOrchestrator.UnitSkillEffectResolution typed = runtime
                ._skill_orchestrator
                .ResolveUnitSkillEffectResult(
                    source,
                    target,
                    skillDefinition,
                    effects
                );
            using GodotProjectionLease<Godot.Collections.Dictionary> projectedLease =
                AttackEffectResolutionResultReader.BuildGodotPayloadLease(typed.Result);
            Godot.Collections.Dictionary projected = projectedLease.Value;
            if (typed.CustomLogLines.Count != 0)
            {
                Godot.Collections.Array customLines = projectedLease.Own(
                    new Godot.Collections.Array(),
                    "battle validation projected custom log lines"
                );
                foreach (string line in typed.CustomLogLines)
                {
                    customLines.Add(line);
                }
                projected["custom_log_lines"] = customLines;
            }

            _test.True(projected.ContainsKey("damage"), "runtime unit skill effect typed projection 应输出 damage。");
            _test.Eq(
                projected.ContainsKey("custom_log_lines"),
                typed.CustomLogLines.Count != 0,
                "runtime unit skill effect typed projection 应按 custom log lines 决定 payload presence。"
            );
            _test.True(
                typed.Result.AttackCheck.CritLocked && typed.Result.AttackCheck.ForceHitNoCrit,
                "force_hit_no_crit 技能的 typed attack check 应保留 crit_locked/force_hit_no_crit 语义。"
            );
            _test.True(
                projected.GetValueOrDefault("crit_locked", false).AsBool(),
                "runtime unit skill effect typed projection 应输出 force_hit_no_crit 的 crit_locked=true。"
            );
        }
        finally
        {
            effects = null;
            castVariant = null;
            skillDefinition = null;
            skillDefinitions = null;
            BattleTestFixture.DisposeBattleFixture(runtime, state);
        }
    }

}

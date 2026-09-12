using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_mage_voltage_hook_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "mage_voltage_hook";
    private static readonly StringName SkillId = "mage_voltage_hook";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContractAndLevelCurve(skill);
            TestSchemaRejectsInvalidAirbornePull();
            TestCanonicalRulesAndHeightParity(skill);
            TestExplicitBarrierBlocksPull(skill);
            TestRuntimeCrossesIntermediateUnitAndAppliesFollowUp(skill);
            TestRuntimeGatesRejectBeforeCost(skill);
            TestManualSelectionUsesOneFinalDestination(skill);
            TestAiEnumeratesDestinationsAndScoresCasterRisk(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage voltage hook regression"));
    }

    private void TestAuthoredContractAndLevelCurve(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "伏电牵引正式资源与 combat_profile 应可加载。");
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能 ID 应保持 mage_voltage_hook。");
        _test.Eq(skill.DisplayName, "伏电牵引", "显示名应表达感电牵引用途。");
        _test.Eq(skill.MaxLevel, 7, "技能等级上限应为7。");
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Unit, "技能应先选择单位目标。");
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Enemy, "技能只能选择敌方单位。");
        _test.Eq(combat.TargetSelectionModeKind, BattleTargetSelectionMode.SingleUnit, "只能选择一个敌人。");
        _test.Eq(combat.AttackResolutionModeKind, CombatSkillAttackResolutionMode.DirectEffect, "技能不得进行AC攻击检定。");
        _test.True(combat.RequiresLos, "初始目标仍应要求视线。");
        _test.Eq(combat.MasteryTriggerMode, new StringName("effect_applied"), "只有实际牵引成功才应增长熟练度。");
        _test.False(combat.EffectDefinitions.Any(effect => effect?.EffectKind == BattleEffectKind.Damage), "纯控制技能不得残留伤害效果。");
        _test.True(skill.Description.Contains("再选择一次最终落点"), "玩家描述必须明确只选一次最终落点。");
        _test.True(skill.Description.Contains("不受高低差限制"), "玩家描述必须明确高低双向均允许。");
        _test.True(skill.Description.Contains("不造成伤害"), "玩家描述必须明确纯控制定位。");

        AssertLevel(skill, 0, 4, 60, 120, 1, 1);
        AssertLevel(skill, 1, 4, 55, 120, 1, 1);
        AssertLevel(skill, 2, 5, 55, 120, 1, 1);
        AssertLevel(skill, 3, 5, 55, 110, 1, 1);
        AssertLevel(skill, 4, 5, 55, 110, 1, 4);
        AssertLevel(skill, 5, 5, 55, 110, 2, 4);
        AssertLevel(skill, 6, 5, 50, 110, 2, 4);
        AssertLevel(skill, 7, 6, 50, 100, 2, 4);

        CombatEffectDefinition consume = combat.EffectDefinitions.FirstOrDefault(
            effect => effect?.EffectKind == BattleEffectKind.EraseStatus
        );
        CombatEffectDefinition stagger = combat.EffectDefinitions.FirstOrDefault(
            effect => effect?.StatusId == new StringName("staggered")
        );
        _test.Eq(consume?.StatusId ?? new StringName(""), new StringName("shocked"), "成功位移应消耗感电。");
        _test.Eq(consume?.TriggerEventKind ?? CombatEffectTriggerEvent.Unknown, CombatEffectTriggerEvent.ForcedMoveApplied, "消耗感电必须绑定实际位移成功。");
        _test.Eq(stagger?.DurationTu ?? -1, 40, "踉跄持续时间应为40TU。");
        _test.Eq(stagger?.TriggerEventKind ?? CombatEffectTriggerEvent.Unknown, CombatEffectTriggerEvent.ForcedMoveApplied, "踉跄必须绑定实际位移成功。");

        BattleAiSkillAffordanceRecord affordance =
            new BattleAiSkillAffordanceClassifier().ClassifySkill(skill, 5);
        _test.True(affordance.is_generatable, "AI应能从正式定义生成伏电牵引候选。");
        _test.True(affordance.effect_roles.Contains("forced_move"), "AI应识别 typed 强制位移角色。");
        _test.True(affordance.affordances.Contains("displacement_control"), "AI应识别位移控制用途。");
    }

    private void TestSchemaRejectsInvalidAirbornePull()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using var invalid = new CombatEffectDef
        {
            effect_type = "forced_move",
            forced_move_mode = "airborne_pull",
            forced_move_distance = 1,
            forced_move_max_target_body_size = 5,
            save_dc = 12,
            save_ability = "willpower",
        };
        using var profile = new CombatSkillDef
        {
            skill_id = "invalid_airborne_pull",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 1,
            attack_resolution_mode = "fate_attack",
        };
        profile.effect_defs.Add(invalid);
        var errors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(errors, profile.skill_id, profile);
        _test.True(ContainsError(errors, "forced_move_max_target_body_size"), $"体型上限5必须被内容校验拒绝。errors={string.Join(" | ", errors)}");
        _test.True(ContainsError(errors, "required_target_status_id"), $"未声明感电前置必须被内容校验拒绝。errors={string.Join(" | ", errors)}");
        _test.True(ContainsError(errors, "direct_effect"), $"airborne_pull 不得进入AC攻击检定。errors={string.Join(" | ", errors)}");
        _test.True(ContainsError(errors, "saving throw"), $"airborne_pull 不得声明豁免。errors={string.Join(" | ", errors)}");
    }

    private void TestCanonicalRulesAndHeightParity(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("voltage_rules_caster", "player", new Vector2I(0, 4), 5);
        BattleUnitState target = BuildUnit("voltage_rules_target", "enemy", new Vector2I(4, 2));
        ApplyShocked(target);
        using BattleTestFixture fixture = CreateFixture("voltage_rules", skill, caster, target);
        CombatEffectDefinition effect = FindPull(skill, 5);
        SetHeight(fixture, target.GetAnchorCoord(), 2);
        SetHeight(fixture, new Vector2I(2, 2), -2);

        BattleAirbornePullPlan highToLow = BattleAirbornePullRules.BuildPlan(
            fixture.State,
            fixture.Runtime.GetGridService(),
            fixture.Runtime._layered_barrier_service,
            caster,
            target,
            effect,
            new Vector2I(2, 2)
        );
        _test.True(highToLow.Allowed, "高地目标应允许被牵引到低地。");

        SetHeight(fixture, target.GetAnchorCoord(), -2);
        SetHeight(fixture, new Vector2I(2, 2), 2);
        BattleAirbornePullPlan lowToHigh = BattleAirbornePullRules.BuildPlan(
            fixture.State,
            fixture.Runtime.GetGridService(),
            fixture.Runtime._layered_barrier_service,
            new BattleUnitReadView(caster),
            new BattleUnitReadView(target),
            effect,
            new Vector2I(2, 2)
        );
        _test.True(lowToHigh.Allowed, "低地目标也应允许被牵引到高地，且只读预览规则应一致。");
        BattleAirbornePullPlan diagonalPlan = BattleAirbornePullRules.BuildPlan(
                fixture.State,
                fixture.Runtime.GetGridService(),
                fixture.Runtime._layered_barrier_service,
                caster,
                target,
                effect,
                new Vector2I(3, 3)
            );
        _test.True(
            diagonalPlan.Allowed,
            $"L5 的2格曼哈顿距离应允许一次选择斜向视觉落点。reason={diagonalPlan.Message}"
        );
        _test.False(
            BattleAirbornePullRules.BuildPlan(
                fixture.State,
                fixture.Runtime.GetGridService(),
                fixture.Runtime._layered_barrier_service,
                caster,
                target,
                effect,
                new Vector2I(5, 2)
            ).Allowed,
            "不比当前位置更接近施法者的落点必须拒绝。"
        );

        target.SetBodySizeProjection(5);
        _test.False(
            BattleAirbornePullRules.BuildPlan(
                fixture.State,
                fixture.Runtime.GetGridService(),
                fixture.Runtime._layered_barrier_service,
                caster,
                target,
                effect,
                new Vector2I(2, 2)
            ).Allowed,
            "体型5即使在L5以后也必须完全免疫本技能。"
        );
    }

    private void TestRuntimeCrossesIntermediateUnitAndAppliesFollowUp(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("voltage_runtime_caster", "player", new Vector2I(0, 2), 5);
        BattleUnitState target = BuildUnit("voltage_runtime_target", "enemy", new Vector2I(4, 2));
        BattleUnitState blocker = BuildUnit("voltage_runtime_blocker", "enemy", new Vector2I(3, 2));
        ApplyShocked(target);
        using BattleTestFixture fixture = CreateFixture("voltage_runtime", skill, caster, target, blocker);
        SetHeight(fixture, target.GetAnchorCoord(), -2);
        SetHeight(fixture, new Vector2I(2, 2), 2);
        AddContactField(
            fixture,
            blocker.GetAnchorCoord(),
            caster,
            "voltage_intermediate_field",
            "slow"
        );
        AddContactField(
            fixture,
            new Vector2I(2, 2),
            caster,
            "voltage_landing_field",
            "burning"
        );
        BattleCommand command = BuildCommand(caster, target, new Vector2I(2, 2));
        int hpBefore = target.GetCurrentHp();

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"合法目标和落点必须通过正式预览。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}");
        _test.True(preview?.ForcedMovePreviewTyped != null, "正式预览必须投影 typed 强制位移事实。");
        _test.Eq(preview?.ForcedMovePreviewTyped?.DestinationCoord ?? new Vector2I(-1, -1), new Vector2I(2, 2), "预览必须保留玩家唯一选择的最终落点。");
        _test.True(preview?.hit_preview == null || preview.hit_preview.IsEmpty, "纯控制技能不得生成AC命中预览。");
        _test.Eq(target.GetAnchorCoord(), new Vector2I(4, 2), "预览不得移动目标。");

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(target.GetAnchorCoord(), new Vector2I(2, 2), "正式执行应跨过中间单位并直接落到选定格。");
        _test.Eq(blocker.GetAnchorCoord(), new Vector2I(3, 2), "中间单位不得被挤开或参与逐格移动。");
        _test.Eq(target.GetCurrentHp(), hpBefore, "伏电牵引不得造成任何伤害。");
        _test.False(target.HasStatusEffect("slow"), "被跨越的中间地格不得触发地形接触。");
        _test.Eq(target.GetStatusEffect("burning")?.stacks ?? 0, 1, "最终落点应且只应结算一次接触状态。");
        _test.False(target.HasStatusEffect("shocked"), "成功位移后必须消耗感电。");
        _test.True(target.HasStatusEffect("staggered"), "成功位移后必须施加踉跄。");
        _test.Eq(target.GetStatusEffect("staggered")?.duration ?? -1, 40, "踉跄应持续40TU。");
        _test.Eq(caster.GetCurrentAp(), 1, "成功施放应消耗1AP。");
        _test.Eq(caster.GetCurrentMp(), 145, "L5成功施放应消耗55MP。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 110, "L5成功施放应启动110TU冷却。");
        _test.True(batch.changed_unit_ids.Contains(target.unit_id), "位移和状态变化应标记目标已改变。");

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestExplicitBarrierBlocksPull(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage(
            "voltage_barrier_caster",
            "player",
            new Vector2I(0, 2),
            5
        );
        BattleUnitState target = BuildUnit(
            "voltage_barrier_target",
            "enemy",
            new Vector2I(4, 2)
        );
        ApplyShocked(target);
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "voltage_barrier",
            new Vector2I(8, 5),
            new[] { caster },
            new[] { target }
        );
        fixture.Runtime.setup(
            skill_definitions: new Dictionary<StringName, SkillDefinition>
            {
                [SkillId] = skill,
            },
            barrier_profile_definitions: BarrierDefinitionTestContent.LoadValidated()
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        CombatEffectDefinition barrierEffect = TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            durationTu: 120,
            saveDc: 15,
            saveDcMode: "static",
            saveAbility: "willpower",
            saveTag: "magic",
            payload: new LayeredBarrierEffectPayloadDefinition(
                areaPattern: "diamond",
                profileId: "prismatic_sphere",
                radiusCells: 2,
                saveDc: 0
            )
        );
        using var barrierBatch = new BattleEventBatch();
        BattleLayeredBarrierApplyResult applyResult = fixture
            .Runtime
            ._layered_barrier_service
            .ApplyLayeredBarrierEffectResult(
                caster,
                caster,
                skill,
                barrierEffect,
                barrierBatch
            );
        _test.True(applyResult.Applied, "测试前置应成功创建显式魔法边界。");
        _test.True(
            fixture.Runtime._layered_barrier_service.HasUnitBoundaryBarrier(
                target,
                target.GetAnchorCoord(),
                new Vector2I(2, 2)
            ),
            "测试几何必须确实跨越显式魔法边界。"
        );
        BattleAirbornePullPlan plan = BattleAirbornePullRules.BuildPlan(
            fixture.State,
            fixture.Runtime.GetGridService(),
            fixture.Runtime._layered_barrier_service,
            caster,
            target,
            FindPull(skill, 5),
            new Vector2I(2, 2)
        );
        _test.False(plan.Allowed, "空中牵引无视中间格，但不得穿越显式魔法边界。");
    }

    private void TestRuntimeGatesRejectBeforeCost(SkillDefinition skill)
    {
        AssertRejectedWithoutCost(skill, "missing_status", level: 0, bodySize: 1, shocked: false, immune: false);
        AssertRejectedWithoutCost(skill, "l0_body2", level: 0, bodySize: 2, shocked: true, immune: false);
        AssertRejectedWithoutCost(skill, "l4_body5", level: 4, bodySize: 5, shocked: true, immune: false);
        AssertRejectedWithoutCost(skill, "forced_move_immune", level: 5, bodySize: 1, shocked: true, immune: true);
        AssertRejectedWithoutCost(skill, "time_stasis", level: 5, bodySize: 1, shocked: true, immune: false, timeStasis: true);
    }

    private void TestManualSelectionUsesOneFinalDestination(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("voltage_manual_caster", "player", new Vector2I(0, 2), 5);
        BattleUnitState target = BuildUnit("voltage_manual_target", "enemy", new Vector2I(4, 2));
        ApplyShocked(target);
        using BattleTestFixture fixture = CreateFixture("voltage_manual", skill, caster, target);
        var port = new TestBattleSelectionPort(fixture, new SingleSkillCatalog(skill))
        {
            SelectedSkillId = SkillId,
            SelectedSkillEntryId = BattleSkillEntryIds.KnownSkill(SkillId),
        };
        using var selection = new GameRuntimeBattleSelection();
        selection.Setup(port);

        _test.Eq(selection.AttemptBattleMoveTo(target.GetAnchorCoord()), BattleRefreshMode.Overlay, "第一次点击应只锁定感电目标。");
        _test.Eq(port.SelectionStage, GameRuntimeBattleSelectionStage.ForcedMoveDestination, "选择目标后必须进入唯一落点阶段。");
        _test.True(port.LastIssuedCommand == null, "第一次点击不得提前施放。");
        _test.Eq(selection.AttemptBattleMoveTo(new Vector2I(2, 2)), BattleRefreshMode.None, "第二次点击合法落点后应发出命令。");
        _test.Eq(port.LastIssuedCommand?.target_unit_id ?? new StringName(""), target.unit_id, "最终命令必须保留第一阶段目标。");
        _test.Eq(port.LastIssuedCommand?.forced_move_destination_coord ?? new Vector2I(-1, -1), new Vector2I(2, 2), "最终命令只能携带一次选择的落点。");
    }

    private void TestAiEnumeratesDestinationsAndScoresCasterRisk(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("voltage_ai_caster", "player", new Vector2I(0, 2), 5);
        BattleUnitState target = BuildUnit("voltage_ai_target", "enemy", new Vector2I(3, 2));
        BattleUnitState meleeAlly = BuildUnit("voltage_ai_melee", "player", new Vector2I(2, 1));
        ApplyMeleeWeapon(meleeAlly);
        ApplyMeleeWeapon(target);
        ApplyShocked(target);
        using BattleTestFixture fixture = CreateFixture("voltage_ai", skill, caster, target, meleeAlly);
        var destinations = new HashSet<Vector2I>();
        bool sawCasterExposurePenalty = false;
        using var scoreService = new BattleAiScoreService();
        BattleAiContext context = BuildAiContext(fixture, caster);
        context.skill_score_input_callback = (_, definition, command, preview, effects, metadata, facts) =>
        {
            destinations.Add(command.forced_move_destination_coord);
            BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
                context,
                definition,
                command,
                preview,
                effects,
                metadata,
                facts
            );
            sawCasterExposurePenalty |= scoreInput?.forced_move_caster_exposure_penalty > 0;
            return scoreInput;
        };
        var action = new UseUnitSkillActionDefinition(
            "voltage_hook_ai",
            "test",
            BattleAiActionIntent.Control,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            1,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(action, context);
        _test.True(destinations.Count >= 2, "AI必须枚举多个合法的目标-落点组合，而不是自动固定牵引方向。");
        _test.True(decision?.command?.forced_move_destination_coord != new Vector2I(-1, -1), "AI决策必须携带明确最终落点。");
        _test.True(decision?.score_input?.forced_move_distance > 0, "AI评分必须消费 canonical 位移距离。");
        _test.True(decision?.score_input?.forced_move_position_score != 0, "AI评分必须包含落点战术价值。");
        _test.True(sawCasterExposurePenalty, "AI评分必须识别把近战目标拉入施法者威胁范围的风险。");
        _test.True(decision?.score_input?.preview?.allowed == true, "AI最终命令必须来自正式预览允许的候选。");
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int range,
        int mp,
        int cooldown,
        int pull,
        int bodySize
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        CombatEffectDefinition effect = FindPull(skill, level);
        _test.Eq(combat.GetEffectiveRangeValue(level), range, $"L{level}射程不符。");
        _test.Eq(costs.ApCost, 1, $"L{level}应消耗1AP。");
        _test.Eq(costs.MpCost, mp, $"L{level}法力消耗不符。");
        _test.Eq(costs.CooldownTu, cooldown, $"L{level}冷却不符。");
        _test.Eq(effect?.ForcedMoveDistance ?? -1, pull, $"L{level}牵引距离不符。");
        _test.Eq(effect?.ForcedMoveMaxTargetBodySize ?? -1, bodySize, $"L{level}体型上限不符。");
        _test.Eq(effect?.RequiredTargetStatusId ?? new StringName(""), new StringName("shocked"), $"L{level}必须要求感电。");
    }

    private void AssertRejectedWithoutCost(
        SkillDefinition skill,
        string suffix,
        int level,
        int bodySize,
        bool shocked,
        bool immune,
        bool timeStasis = false
    )
    {
        BattleUnitState caster = BuildMage($"voltage_{suffix}_caster", "player", new Vector2I(0, 1), level);
        BattleUnitState target = BuildUnit($"voltage_{suffix}_target", "enemy", new Vector2I(3, 1));
        target.SetBodySizeProjection(bodySize);
        if (shocked)
            ApplyShocked(target);
        if (immune)
        {
            target.SetStatusEffect(new BattleStatusEffectState
            {
                status_id = "voltage_forced_move_immune",
                stacks = 1,
                forced_move_immune = true,
            });
        }
        if (timeStasis)
        {
            target.SetStatusEffect(new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
                stacks = 1,
                duration = 100,
            });
        }
        using BattleTestFixture fixture = CreateFixture($"voltage_{suffix}", skill, caster, target);
        BattleCommand command = BuildCommand(caster, target, new Vector2I(2, 1));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.False(preview?.allowed == true, $"{suffix} 必须在正式预览阶段拒绝。");
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), 2, $"{suffix} 拒绝不得消耗AP。");
        _test.Eq(caster.GetCurrentMp(), 200, $"{suffix} 拒绝不得消耗MP。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{suffix} 拒绝不得启动冷却。");
        _test.Eq(target.GetAnchorCoord(), new Vector2I(3, 1), $"{suffix} 拒绝不得移动目标。");
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(SkillPath, "mage_voltage_hook_regression");

    private static CombatEffectDefinition FindPull(SkillDefinition skill, int level) =>
        skill?.CombatProfile?.EffectDefinitions.FirstOrDefault(
            effect => effect?.ForcedMoveModeKind == BattleForcedMoveMode.AirbornePull
                && effect.IsUnlockedAtSkillLevel(level)
        );

    private BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState caster,
        params BattleUnitState[] others
    )
    {
        BattleUnitState[] enemies = others.Where(unit => unit.faction_id != caster.faction_id).ToArray();
        BattleUnitState[] allies = new[] { caster }.Concat(
            others.Where(unit => unit.faction_id == caster.faction_id)
        ).ToArray();
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(8, 5),
            allies,
            enemies
        );
        fixture.Runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [SkillId] = skill });
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static BattleUnitState BuildMage(
        StringName id,
        StringName faction,
        Vector2I coord,
        int skillLevel
    )
    {
        BattleUnitState unit = BuildUnit(id, faction, coord);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, skillLevel, preserveZero: true);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetCurrentMp(200);
        return unit;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(id, faction, coord, currentAp: 2, currentHp: 100);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.SetCurrentMp(200);
        unit.SetBodySizeProjection(1);
        return unit;
    }

    private static void ApplyShocked(BattleUnitState target) =>
        target.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "shocked",
            stacks = 1,
            duration = 80,
        });

    private static void ApplyMeleeWeapon(BattleUnitState unit) =>
        unit.ApplyWeaponProjectionTyped(new WeaponProjection
        {
            weapon_profile_kind = "equipped",
            weapon_item_id = $"voltage_melee_{unit.unit_id}",
            weapon_profile_type_id = "voltage_melee",
            weapon_range_type = "melee",
            weapon_family = "sword",
            weapon_current_grip = "one_handed",
            weapon_attack_range = 1,
            weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
            weapon_physical_damage_tag = "physical_slash",
        });

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target,
        Vector2I destination
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
            forced_move_destination_coord = destination,
        };
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static void SetHeight(BattleTestFixture fixture, Vector2I coord, int heightOffset) =>
        fixture.Runtime.GetGridService().SetHeightOffset(fixture.State, coord, heightOffset);

    private static void AddContactField(
        BattleTestFixture fixture,
        Vector2I coord,
        BattleUnitState source,
        StringName fieldId,
        StringName statusId
    )
    {
        BattleCellState cell = fixture.Runtime.GetGridService().GetCellState(
            fixture.State,
            coord
        );
        cell?.timed_terrain_effects.Add(new BattleTerrainEffectState
        {
            field_instance_id = fieldId,
            effect_id = fieldId,
            effect_type = "status",
            source_unit_id = source.unit_id,
            source_skill_id = "voltage_contact_fixture",
            target_team_filter = "enemy",
            contact_status_id = statusId,
            contact_status_duration_tu = 30,
            contact_stack_behavior = "add",
            contact_stack_limit = 3,
            remaining_tu = 100,
        });
    }

    private static BattleAiContext BuildAiContext(BattleTestFixture fixture, BattleUnitState caster)
    {
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            preview_command_callback = fixture.Runtime.PreviewCommand,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefinitions(fixture.Runtime.GetSkillDefinitionIndexTyped());
        return context;
    }

    private static bool ContainsError(IEnumerable<string> errors, string fragment) =>
        (errors ?? Array.Empty<string>()).Any(
            error => error?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true
        );

    private sealed class SingleSkillCatalog : ISkillCatalog
    {
        private readonly IReadOnlyDictionary<StringName, SkillDefinition> _definitions;

        internal SingleSkillCatalog(SkillDefinition skill) =>
            _definitions = new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill };

        public long GetRevision() => 1;
        public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() => _definitions;
        public bool HasSkill(StringName skillId) => _definitions.ContainsKey(skillId);
        public bool TryGetSkillDefinition(StringName skillId, out SkillDefinition definition) =>
            _definitions.TryGetValue(skillId, out definition);
        public SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(StringName skillId, int level) =>
            TryGetSkillDefinition(skillId, out SkillDefinition definition)
                ? SkillEffectiveCombatDefinition.BuildUncached(definition, level)
                : SkillEffectiveCombatDefinition.BuildMissing(level);
        public CombatSkillResourceCosts GetEffectiveResourceCostValues(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).ResourceCosts;
        public int GetEffectiveAttackRollBonus(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).AttackRollBonus;
        public StringName GetEffectiveAreaPattern(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).AreaPattern;
        public int GetEffectiveAreaValue(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).AreaValue;
        public int GetEffectiveRangeValue(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).RangeValue;
        public int GetEffectiveMaxTargetCount(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).MaxTargetCount;
        public IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariantDefinitions(StringName skillId, int level) =>
            GetEffectiveCombatDefinition(skillId, level).UnlockedCastVariants;
    }

    private sealed class TestBattleSelectionPort : IGameRuntimeBattleSelectionPort
    {
        private static readonly IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EmptyBindings =
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        private readonly BattleTestFixture _fixture;
        private readonly ISkillCatalog _catalog;

        internal TestBattleSelectionPort(BattleTestFixture fixture, ISkillCatalog catalog)
        {
            _fixture = fixture;
            _catalog = catalog;
        }

        internal StringName SelectedSkillId { get; set; } = "";
        internal StringName SelectedSkillEntryId { get; set; } = "";
        internal GameRuntimeBattleSelectionStage SelectionStage { get; set; } = GameRuntimeBattleSelectionStage.Target;
        internal BattleCommand LastIssuedCommand { get; private set; }
        private readonly List<Vector2I> _targetCoords = new();
        private readonly List<StringName> _targetUnitIds = new();
        private Vector2I _selectedCoord = new(-1, -1);

        public Vector2I GetBattleSelectedCoord() => _selectedCoord;
        public BattleUnitState GetManualBattleUnit() => _fixture.Allies[0];
        public BattleUnitState GetRuntimeBattleActiveUnit() => _fixture.Allies[0];
        public BattleUnitState GetRuntimeBattleUnitAtCoord(Vector2I coord) =>
            _fixture.State.GetUnitsTyped().FirstOrDefault(unit => unit?.OccupiesCoord(coord) == true);
        public BattleUnitState GetRuntimeBattleUnitById(StringName id) => _fixture.State.GetUnit(id);
        public BattleState GetBattleState() => _fixture.State;
        public BattleGridService GetBattleGridService() => _fixture.Runtime.GetGridService();
        public BattleLayeredBarrierService GetBattleLayeredBarrierService() => _fixture.Runtime._layered_barrier_service;
        public ISkillCatalog GetSkillCatalog() => _catalog;
        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindings() => EmptyBindings;
        public int GetBattleWorldStep() => 0;
        public BattlePreview PreviewBattleCommand(BattleCommand command) => _fixture.Runtime.PreviewCommand(command);
        public string GetBattleSkillCastBlockMessage(BattleUnitState unit, StringName skillId) => "";
        public BattleRefreshMode IssueBattleCommand(BattleCommand command)
        {
            LastIssuedCommand = command;
            return BattleRefreshMode.None;
        }
        public void RefreshBattleSelectionState() { }
        public void UpdateStatus(string message) { }
        public string FormatCoord(Vector2I coord) => coord.ToString();
        public bool IsBattleActive() => true;
        public StringName GetSelectedSkillId() => SelectedSkillId;
        public StringName GetSelectedSkillEntryId() => SelectedSkillEntryId;
        public void SetSelectedSkillEntryId(StringName value) => SelectedSkillEntryId = value;
        public void SetSelectedSkillId(StringName value) => SelectedSkillId = value;
        public StringName GetSelectedSkillVariantId() => "";
        public void SetSelectedSkillVariantId(StringName value) { }
        public int GetSelectedWindupTier() => 1;
        public void SetSelectedWindupTier(int tier) { }
        public GameRuntimeBattleSelectionStage GetSelectionStage() => SelectionStage;
        public void SetSelectionStage(GameRuntimeBattleSelectionStage value) => SelectionStage = value;
        public StringName GetLastManualUnitId() => "";
        public void SetLastManualUnitId(StringName value) { }
        public IReadOnlyList<Vector2I> GetTargetCoords() => _targetCoords;
        public void SetTargetCoords(IEnumerable<Vector2I> values)
        {
            _targetCoords.Clear();
            _targetCoords.AddRange(values ?? Array.Empty<Vector2I>());
        }
        public IReadOnlyList<StringName> GetTargetUnitIds() => _targetUnitIds;
        public void SetTargetUnitIds(IEnumerable<StringName> values)
        {
            _targetUnitIds.Clear();
            _targetUnitIds.AddRange(values ?? Array.Empty<StringName>());
        }
        public void SetBattleSelectedCoord(Vector2I value) => _selectedCoord = value;
    }
}

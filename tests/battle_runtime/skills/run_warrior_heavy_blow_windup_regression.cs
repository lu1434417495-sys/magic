using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_heavy_blow_windup_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "res://data/configs/skills/warrior_heavy_blow.tres";
    private static readonly StringName SkillId = "warrior_heavy_blow";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHeavyWeaponGateIsIndependentAuthoringData();
        TestResourceAndLevelCurves();
        TestHeavyGateTierCapAndTwoDiceGroupPreview();
        TestWindupFreezesActionButRecoversStaminaAndCannotCancel();
        TestTemporalStatusesKeepTheirExistingWindupSemantics();
        TestDamageDoesNotInterruptButHardControlDoes();
        TestTargetMovementWhiffsAtCompletion();
        TestWeaponChangeInterrupts();
        TestManualSelectionCyclesWindupTiers();
        TestAiScoreProfileProjectsDelayWeight();
        TestAiEvaluatesEveryLegalWindupTier();
        TestAiCanonicalPreviewRejectsUnaffordableTier();
        TestAiWindupQuoteInvariantFailsClosed();
        TestAiScoreUsesFinalCostReserveAndDelay();
        TestAiNonWindupDelayIsNeutral();
        TestAiProductionScoringRanksWindupTradeoffs();
        TestAutomaticContentReferencesRejectWindup();
        TestAutomaticDispatchPathsRejectWindup();
        RequestTestExit(_test.Finish("Warrior heavy blow windup regression"));
    }

    private void TestHeavyWeaponGateIsIndependentAuthoringData()
    {
        using CombatWindupDef windup = new();
        using CombatSkillDef profile = new()
        {
            skill_id = "light_weapon_windup_probe",
            windup_profile = windup,
            requires_heavy_weapon = false,
        };
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );

        validator.AppendCombatProfileValidationErrors(
            errors,
            profile.skill_id,
            profile
        );

        _test.Eq(
            errors.Count,
            0,
            $"蓄力配置不得隐式要求重型武器；是否要求 heavy 必须只由 requires_heavy_weapon 数据决定。errors={string.Join(" | ", errors)}"
        );

        SkillDefinition lightWeaponWindupSkill = LoadSkillWithHeavyRequirement(false);
        Fixture lightWeaponFixture = BuildFixture(
            skillLevel: 2,
            heavyWeapon: false,
            skillOverride: lightWeaponWindupSkill
        );
        BattlePreview preview = lightWeaponFixture.Runtime.PreviewCommand(
            BuildCommand(
                lightWeaponFixture.Caster,
                lightWeaponFixture.Target,
                tier: 1
            )
        );
        _test.False(
            lightWeaponWindupSkill.CombatProfile.RequiresHeavyWeapon,
            "requires_heavy_weapon=false 应无损投影到运行时定义。"
        );
        _test.True(
            preview.allowed,
            $"同一蓄力配置在 requires_heavy_weapon=false 时不应拒绝非 heavy 武器。logs={string.Join(" | ", preview.LogLinesTyped)}"
        );
    }

    private void TestResourceAndLevelCurves()
    {
        SkillDefinition skill = LoadSkill();
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatWindupDefinition windup = combat?.Windup;

        _test.True(skill != null && combat != null && windup != null, "重压斩应投影类型化蓄力配置。");
        _test.Eq(skill?.MaxLevel ?? -1, 5, "重压斩最高等级应为 5。");
        _test.Eq(skill?.NonCoreMaxLevel ?? -1, 3, "非核心重压斩上限仍应为 3。");
        _test.True(combat?.RequiresHeavyWeapon == true, "重压斩应要求 heavy 近战武器。");
        _test.Eq(combat?.EffectDefinitions.Count ?? -1, 1, "重压斩只应保留一次武器攻击效果。");
        _test.Eq(windup?.StaminaCostPerTier ?? -1, 6, "每挡应额外消耗 6 体力。");
        _test.Eq(windup?.GetSkillTierCap(0) ?? -1, 1, "0 级技能挡位上限应为 1。");
        _test.Eq(windup?.GetSkillTierCap(2) ?? -1, 2, "2 级技能挡位上限应为 2。");
        _test.Eq(windup?.GetSkillTierCap(4) ?? -1, 3, "4 级技能挡位上限应为 3。");
        _test.Eq(windup?.GetSkillTierCap(5) ?? -1, 0, "5 级应取消技能挡位上限。");
        _test.Eq(
            windup?.GetBaseWeaponDiceMultiplier(5) ?? -1,
            2,
            "5 级基础伤害应提升为 2W。"
        );
        int[] expectedStamina = { 14, 12, 12, 10, 10, 10 };
        int[] expectedCooldown = { 90, 90, 80, 80, 70, 60 };
        for (int level = 0; level <= 5; level++)
        {
            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.StaminaCost, expectedStamina[level], $"L{level} 基础体力不符。");
            _test.Eq(costs.CooldownTu, expectedCooldown[level], $"L{level} 冷却不符。");
        }
    }

    private void TestHeavyGateTierCapAndTwoDiceGroupPreview()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: false);
        BattleCommand tierTwoCommand = BuildCommand(
            fixture.Caster,
            fixture.Target,
            tier: 2
        );

        BattlePreview nonHeavyPreview = fixture.Runtime.PreviewCommand(tierTwoCommand);
        _test.False(nonHeavyPreview.allowed, "非 heavy 武器不应通过重压斩预览。");
        _test.True(
            LogsContain(nonHeavyPreview.LogLinesTyped, "heavy"),
            "非 heavy 武器应给出明确门槛提示。"
        );

        ApplyWeapon(fixture.Caster, heavy: true, instanceId: "heavy-a");
        fixture.Caster.attribute_snapshot.SetValue("constitution_modifier", 6);
        BattleCommand tierThreeCommand = BuildCommand(
            fixture.Caster,
            fixture.Target,
            tier: 3
        );
        BattlePreview overCapPreview = fixture.Runtime.PreviewCommand(tierThreeCommand);
        _test.False(overCapPreview.allowed, "L2 即使体质允许也不能选择 3 挡。");

        BattlePreview preview = fixture.Runtime.PreviewCommand(tierTwoCommand);
        BattleDamagePreviewRangeService.SkillDamagePreview? damage =
            preview.DamagePreviewTyped;
        _test.True(preview.allowed, "heavy 近战武器的 2 挡预览应允许。");
        _test.True(damage?.HasDamage == true, "2 挡预览应包含伤害范围。");
        _test.Eq(
            damage?.DamageRanges[0].WeaponDiceRange.DiceCount ?? -1,
            6,
            "基础武器为 2D6 时，L2 的 2 挡应按 3W 预览为 6D6。"
        );
        _test.Eq(
            damage?.DamageRanges[0].WeaponDiceRange.DiceBonus ?? -1,
            3,
            "武器固定加值应只计算一次。"
        );
        _test.True(
            LogsContain(preview.LogLinesTyped, "20 TU")
            && LogsContain(preview.LogLinesTyped, "24 体力"),
            "STR+3 的 2 挡预览应显示 20TU 与总计 24 体力。"
        );

        Fixture levelFiveFixture = BuildFixture(skillLevel: 5, heavyWeapon: true);
        levelFiveFixture.Caster.attribute_snapshot.SetValue("constitution_modifier", 10);
        BattlePreview levelFivePreview = levelFiveFixture.Runtime.PreviewCommand(
            BuildCommand(levelFiveFixture.Caster, levelFiveFixture.Target, tier: 5)
        );
        _test.True(
            levelFivePreview.allowed,
            "L5 不应再施加技能挡位上限，CON+10 应允许选择 5 挡。"
        );
        _test.Eq(
            levelFivePreview.DamagePreviewTyped?.DamageRanges[0].WeaponDiceRange.DiceCount
                ?? -1,
            14,
            "L5 的 2W 基础加 5 挡应为 7W；2D6 武器应预览为 14D6。"
        );
    }

    private void TestWindupFreezesActionButRecoversStaminaAndCannotCancel()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Caster.SetActionProgressTyped(37);
        int targetHpBefore = fixture.Target.GetCurrentHp();

        fixture.Runtime.IssueCommand(BuildCommand(fixture.Caster, fixture.Target, tier: 2));

        _test.True(fixture.Caster.pending_cast?.IsWindup == true, "确认后应进入武技蓄力。");
        _test.Eq(fixture.Caster.GetCurrentAp(), 0, "确认后剩余 AP 应清零。");
        _test.Eq(
            fixture.State.phase,
            "timeline_running",
            "确认蓄力后应立即结束当前行动回合并回到时间线。"
        );
        _test.Eq(
            fixture.State.active_unit_id,
            new StringName(""),
            "确认蓄力后不应继续保留当前行动单位。"
        );
        _test.Eq(fixture.Caster.GetCurrentStamina(), 36, "L2 的 2 挡应立即支付 12+12 体力。");
        _test.Eq(
            fixture.Caster.pending_cast?.RemainingCastProgress ?? -1,
            2000,
            "STR+3 的 2 挡应蓄力 20TU。"
        );
        _test.Eq(
            fixture.Caster.pending_cast?.WindupSnapshot?.WeaponDiceMultiplier ?? -1,
            3,
            "蓄力开始时应冻结 3W 结算倍率。"
        );

        BattlePreview cancelPreview = fixture.Runtime.PreviewCommand(
            new BattleCommand
            {
                CommandKind = BattleCommandKind.CancelCast,
                unit_id = fixture.Caster.unit_id,
            }
        );
        _test.False(cancelPreview.allowed, "蓄力开始后不允许主动取消。");
        _test.True(
            LogsContain(cancelPreview.LogLinesTyped, "不能主动取消"),
            "取消预览应明确说明蓄力不可主动取消。"
        );

        fixture.Caster.SetCurrentHp(90);
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(new BattleEventBatch());
        _test.True(fixture.Caster.HasPendingCast(), "单纯受到伤害不应打断蓄力。");

        AdvanceTimelineTu(fixture, 15);
        int staminaAfter15Tu = fixture.Caster.GetCurrentStamina();
        _test.True(fixture.Caster.HasPendingCast(), "15TU 后 2 挡蓄力尚未完成。");
        _test.Eq(
            fixture.Caster.GetActionProgressTyped(),
            37,
            "蓄力期间行动进度应冻结。"
        );
        _test.True(staminaAfter15Tu > 36, "蓄力期间应按正常规则恢复体力。");

        BattleEventBatch completionBatch = AdvanceTimelineTu(fixture, 5);
        _test.False(fixture.Caster.HasPendingCast(), "20TU 时蓄力应完成。");
        _test.True(
            fixture.Target.GetCurrentHp() < targetHpBefore,
            $"完成时应执行一次武器攻击；hp={fixture.Target.GetCurrentHp()}/{targetHpBefore} logs={string.Join(" | ", completionBatch.LogLinesTyped)}。"
        );
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "完成后应启动 L2 的完整冷却。");
        _test.Eq(
            fixture.Caster.GetActionProgressTyped(),
            37,
            "完成所在 tick 也不能获得行动进度。"
        );
        _test.True(
            fixture.Caster.GetCurrentStamina() > staminaAfter15Tu,
            "完成所在 tick 仍应恢复体力。"
        );
    }

    private void TestDamageDoesNotInterruptButHardControlDoes()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Runtime.IssueCommand(BuildCommand(fixture.Caster, fixture.Target, tier: 2));
        fixture.Caster.SetCurrentHp(80);
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(new BattleEventBatch());
        _test.True(fixture.Caster.HasPendingCast(), "大量伤害本身也不应触发维持检定。");

        fixture.Caster.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "stunned",
                duration = 10,
                stacks = 1,
            }
        );
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(new BattleEventBatch());
        _test.False(fixture.Caster.HasPendingCast(), "stunned 应打断蓄力。");
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "强控打断应启动完整冷却。");
        _test.Eq(fixture.Caster.GetCurrentStamina(), 36, "强控打断不应返还体力。");
    }

    private void TestTemporalStatusesKeepTheirExistingWindupSemantics()
    {
        Fixture slowFixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        slowFixture.Runtime.IssueCommand(
            BuildCommand(slowFixture.Caster, slowFixture.Target, tier: 1)
        );
        slowFixture.Caster.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_SLOW,
                source_unit_id = "temporal_source",
                duration = 30,
                stacks = 1,
            }
        );
        AdvanceTimelineTu(slowFixture, 10);
        _test.Eq(
            slowFixture.Caster.pending_cast?.RemainingCastProgress ?? -1,
            500,
            "time_slow 下 10TU 应只推进 5TU 的蓄力进度。"
        );
        _test.True(
            slowFixture.Caster.GetCurrentStamina() > 42,
            "time_slow 只减慢蓄力，不能冻结正常体力恢复。"
        );

        Fixture stasisFixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        stasisFixture.Runtime.IssueCommand(
            BuildCommand(stasisFixture.Caster, stasisFixture.Target, tier: 1)
        );
        stasisFixture.Caster.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
                source_unit_id = "temporal_source",
                duration = 10,
                stacks = 1,
            }
        );
        AdvanceTimelineTu(stasisFixture, 10);
        _test.True(stasisFixture.Caster.HasPendingCast(), "time_stasis 不应打断蓄力。");
        _test.Eq(
            stasisFixture.Caster.pending_cast?.RemainingCastProgress ?? -1,
            1000,
            "time_stasis 生效期间蓄力进度应完全冻结。"
        );
        _test.Eq(
            stasisFixture.Caster.GetCurrentStamina(),
            42,
            "time_stasis 应沿用既有语义，同时冻结常规体力恢复。"
        );
        AdvanceTimelineTu(stasisFixture, 5);
        _test.Eq(
            stasisFixture.Caster.pending_cast?.RemainingCastProgress ?? -1,
            500,
            "time_stasis 解除后的下一 tick 应恢复蓄力推进。"
        );
        _test.True(
            stasisFixture.Caster.GetCurrentStamina() > 42,
            "time_stasis 解除后应恢复正常体力恢复。"
        );
    }

    private void TestTargetMovementWhiffsAtCompletion()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        int targetHpBefore = fixture.Target.GetCurrentHp();
        fixture.Runtime.IssueCommand(BuildCommand(fixture.Caster, fixture.Target, tier: 1));
        _test.True(
            fixture.Runtime._grid_service.MoveUnit(
                fixture.State,
                fixture.Target,
                new Vector2I(3, 0)
            ),
            "测试目标应能移出近战范围。"
        );
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(new BattleEventBatch());
        _test.True(fixture.Caster.HasPendingCast(), "目标移动不应提前打断蓄力。");

        BattleEventBatch completionBatch = AdvanceTimelineTu(fixture, 10);
        _test.False(fixture.Caster.HasPendingCast(), "目标移出范围后完成时应清除蓄力。");
        _test.Eq(fixture.Target.GetCurrentHp(), targetHpBefore, "完成时范围复验失败应落空。");
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "落空仍应启动完整冷却。");
        _test.True(
            LogsContain(completionBatch.LogLinesTyped, "攻击落空"),
            "范围复验失败应留下明确落空日志。"
        );
    }

    private void TestWeaponChangeInterrupts()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Runtime.IssueCommand(BuildCommand(fixture.Caster, fixture.Target, tier: 1));
        ApplyWeapon(fixture.Caster, heavy: true, instanceId: "heavy-b");
        BattleEventBatch batch = new();
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(batch);

        _test.False(fixture.Caster.HasPendingCast(), "更换同型但不同实例的武器也应打断蓄力。");
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "武器变化应启动完整冷却。");
        _test.Eq(fixture.Caster.GetCurrentStamina(), 42, "武器变化不应返还已付的 18 体力。");
        _test.True(LogsContain(batch.LogLinesTyped, "武器发生变化"), "武器打断应写明原因。");
    }

    private void TestAutomaticDispatchPathsRejectWindup()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        int targetHpBefore = fixture.Target.GetCurrentHp();
        ContingencyReleaseContext releaseContext = new()
        {
            InstanceId = "windup:auto",
            SetupId = "windup_auto",
            OwnerMemberId = fixture.Caster.source_member_id,
            OwnerUnitId = fixture.Caster.unit_id,
            CasterUnitId = fixture.Caster.unit_id,
            TriggerType = "affected_by_spell",
        };
        AutoCastRequest request = new()
        {
            CasterUnitId = fixture.Caster.unit_id,
            OwnerMemberId = fixture.Caster.source_member_id,
            OwnerUnitId = fixture.Caster.unit_id,
            SetupId = "windup_auto",
            InstanceId = "windup:auto",
            SourceSkillId = "test_contingency_source",
            SourceSkillLevel = 1,
            SourceSkillGrantSourceType = UnitSkillGrantSourceType.Player,
            StoredSkillId = SkillId,
            CastLevel = 2,
            TargetResolution = ContingencyTargetResolutionResult.UnitTarget(
                fixture.Target.unit_id,
                fixture.Target.GetAnchorCoord()
            ),
            ReleaseContext = releaseContext,
        };
        BattleEventBatch autoBatch = new();
        bool autoApplied = fixture.Runtime._skill_orchestrator.ExecuteAutoCast(
            request,
            autoBatch
        );
        _test.False(autoApplied, "contingency 自动施放应拒绝蓄力技能。");
        _test.True(LogsContain(autoBatch.LogLinesTyped, "不能通过"), "自动施放拒绝应有明确日志。");
        _test.Eq(fixture.Target.GetCurrentHp(), targetHpBefore, "自动路径拒绝不应造成伤害。");
    }

    private void TestAutomaticContentReferencesRejectWindup()
    {
        var context = new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName>(),
            KnownSkillIds = new HashSet<StringName> { SkillId },
            WindupSkillIds = new HashSet<StringName> { SkillId },
            KnownStatusIds = new HashSet<StringName>(),
        };

        var immediateErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateImmediateWeaponAttackPayload(
            new ImmediateWeaponAttackActionPayloadDef { skill_id = SkillId },
            context,
            "test.immediate_weapon_attack",
            immediateErrors
        );
        _test.True(
            LogsContain(immediateErrors, "EQA_REFERENCE_WINDUP_SKILL_UNSUPPORTED"),
            "装备即时攻击在内容校验期就应拒绝蓄力技能。"
        );

        var triggerErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateTriggerSkillPayload(
            new TriggerSkillActionPayloadDef { skill_id = SkillId },
            context,
            "test.trigger_skill",
            triggerErrors
        );
        _test.True(
            LogsContain(triggerErrors, "EQA_REFERENCE_WINDUP_SKILL_UNSUPPORTED"),
            "trigger_skill 在内容校验期就应拒绝蓄力技能。"
        );
    }

    private void TestAiScoreProfileProjectsDelayWeight()
    {
        using var resource = new BattleAiScoreProfile
        {
            delayed_resolution_cost_per_5_tu = 7,
        };
        BattleAiScoreProfileDefinition definition =
            BattleAiScoreProfileDefinition.FromResource(resource);

        _test.Eq(
            definition.DelayedResolutionCostPer5Tu,
            7,
            "AI profile Resource 应投影每 5TU 延迟权重。"
        );
        _test.True(
            definition.TryWithScalar(
                "delayed_resolution_cost_per_5_tu",
                9,
                out BattleAiScoreProfileDefinition patched
            ),
            "调参器应能按正式标量路径修改延迟权重。"
        );
        _test.Eq(
            patched.DelayedResolutionCostPer5Tu,
            9,
            "调参标量补丁应写入类型化 profile。"
        );
        Dictionary<string, object> plain = BattleAiScoreProjection.BuildProfilePlain(definition);
        _test.Eq(
            Convert.ToInt32(plain.GetValueOrDefault("delayed_resolution_cost_per_5_tu", -1)),
            7,
            "profile 对外投影应包含延迟权重。"
        );
    }

    private void TestManualSelectionCyclesWindupTiers()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Caster.attribute_snapshot.SetValue("constitution_modifier", 6);
        SkillDefinition skill = LoadSkill();
        var port = new TestBattleSelectionPort(
            fixture,
            new SingleSkillCatalog(skill)
        )
        {
            SelectedSkillId = SkillId,
            SelectedSkillEntryId = BattleSkillEntryIds.KnownSkill(SkillId),
            SelectedWindupTier = 1,
        };
        port.TargetCoords.Add(fixture.Target.GetAnchorCoord());
        port.TargetUnitIds.Add(fixture.Target.unit_id);
        using var selection = new GameRuntimeBattleSelection();
        selection.Setup(port);

        _test.Eq(
            selection.GetSelectedBattleSkillVariantName(),
            "蓄力 1 挡 · 10 TU · 18 体力 · 2W",
            "选择界面应显示当前挡位的 canonical 时间、体力与武器骰。"
        );
        selection.CycleSelectedBattleSkillOption(1);
        _test.Eq(port.SelectedWindupTier, 2, "Q/E 应把 L2 技能从 1 挡切到 2 挡。");
        _test.Eq(port.TargetCoords.Count, 0, "切换挡位应清除旧格子目标。");
        _test.Eq(port.TargetUnitIds.Count, 0, "切换挡位应清除旧单位目标。");
        _test.True(port.RefreshCount > 0, "切换挡位应刷新战斗选择状态。");
        _test.True(
            port.LastStatus.Contains("蓄力 2 挡", StringComparison.Ordinal),
            $"切换后状态文本应明确 2 挡。status={port.LastStatus}"
        );
        _test.Eq(
            selection.GetSelectedBattleSkillVariantName(),
            "蓄力 2 挡 · 20 TU · 24 体力 · 3W",
            "2 挡显示应使用与 preview/execution 相同的 quote。"
        );
        BattleCommand previewCommand = selection.BuildSelectedSkillPreviewCommand(
            fixture.Caster,
            fixture.Target.GetAnchorCoord()
        );
        _test.Eq(
            previewCommand?.windup_tier ?? -1,
            2,
            "手动选择生成的 preview command 必须携带当前挡位。"
        );

        selection.CycleSelectedBattleSkillOption(1);
        _test.Eq(
            port.SelectedWindupTier,
            2,
            "CON+6 的自然上限为 3 挡，但 L2 手动选择不得越过技能上限 2 挡。"
        );
        selection.CycleSelectedBattleSkillOption(-1);
        _test.Eq(port.SelectedWindupTier, 1, "向下切换应回到 1 挡。");
        selection.ClearBattleSkillSelection(announce: false);
        _test.Eq(port.SelectedWindupTier, 1, "清除技能选择时应把下次默认挡位重置为 1。");
    }

    private void TestAiCanonicalPreviewRejectsUnaffordableTier()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Caster.SetCurrentStamina(20);
        int scoreCallCount = 0;
        BattleAiContext context = BuildAiContext(fixture, LoadSkill());
        context.trace_enabled = true;
        context.skill_cast_block_reason_callback = (_, _) =>
            BattleSkillCastBlockReasonKind.None;
        context.preview_command_callback = fixture.Runtime.PreviewCommand;
        context.skill_score_input_callback = (
            _,
            _,
            command,
            preview,
            _,
            _,
            _
        ) =>
        {
            scoreCallCount++;
            return BuildArtificialScoreInput(command, preview, command.windup_tier * 100);
        };

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildWindupAiAction(),
            context
        );

        _test.Eq(
            decision?.command?.windup_tier ?? -1,
            1,
            "20 体力只能负担 18 体力的 1 挡，canonical preview 应拒绝 24 体力的 2 挡。"
        );
        _test.Eq(scoreCallCount, 1, "不可负担挡位不得进入正式评分器。");
        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "不可负担挡位仍应保留 action trace。");
        _test.Eq(
            traces.Count > 0 ? traces[0].EvaluationCount : -1,
            2,
            "AI 应枚举两个规则上合法的挡位，再由 canonical preview 校验当前负担能力。"
        );
        _test.Eq(
            traces.Count > 0 ? traces[0].PreviewRejectCount : -1,
            1,
            "不可负担的 2 挡应计为一次 preview reject。"
        );
    }

    private void TestAiWindupQuoteInvariantFailsClosed()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Caster.SetCurrentStamina(0);
        int scoreCallCount = 0;
        BattleAiContext context = BuildAiContext(fixture, LoadSkill());
        context.trace_enabled = true;
        context.skill_cast_block_reason_callback = (_, _) =>
            BattleSkillCastBlockReasonKind.None;
        context.preview_command_callback = _ => BuildAllowedUnitPreview(fixture.Target);
        context.skill_score_input_callback = (
            _,
            _,
            command,
            preview,
            _,
            _,
            _
        ) =>
        {
            scoreCallCount++;
            return BuildArtificialScoreInput(command, preview, 100);
        };

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildWindupAiAction(),
            context
        );

        _test.True(decision == null, "canonical preview 与 quote 规则失配时必须 fail closed。");
        _test.Eq(scoreCallCount, 0, "quote invariant 失败后不得继续评分。");
        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "quote invariant 失败应可追踪。");
        _test.Eq(
            traces.Count > 0
                ? traces[0].BlockReasons.GetValueOrDefault(
                    "windup_quote_invariant_reject",
                    0
                )
                : -1,
            2,
            "两个挡位都应以独立 invariant 原因关闭。"
        );
    }

    private void TestAiScoreUsesFinalCostReserveAndDelay()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        BattleAiScoreProfileDefinition noReserveProfile =
            BattleAiScoreProfileDefinition.Default with
            {
                DamageWeight = 0,
                TargetCountWeight = 0,
                ApCostWeight = 0,
                MpCostWeight = 0,
                StaminaCostWeight = 2,
                AuraCostWeight = 0,
                CooldownWeight = 0,
                DelayedResolutionCostPer5Tu = 2,
                StaminaReserveFloorBp = 0,
                StaminaReservePressureWeight = 0,
                StaminaReserveBreachPenalty = 0,
                PositionObjectiveWeight = 0,
                SafeDistanceAdherenceWeight = 0,
            };
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup();
        scoreService.SetProfile(noReserveProfile);
        BattleAiScoreInput tierOne = BuildWindupScoreInput(
            fixture,
            scoreService,
            tier: 1,
            includeEffects: false
        );
        BattleAiScoreInput tierTwo = BuildWindupScoreInput(
            fixture,
            scoreService,
            tier: 2,
            includeEffects: false
        );

        _test.Eq(tierOne.stamina_cost, 18, "1 挡评分必须使用最终体力 18。");
        _test.Eq(tierTwo.stamina_cost, 24, "2 挡评分必须使用最终体力 24。");
        _test.Eq(
            tierTwo.resource_cost_score - tierOne.resource_cost_score,
            12,
            "额外 6 体力应按 stamina_cost_weight=2 统一计为 12 分资源成本。"
        );
        _test.Eq(tierOne.delayed_resolution_tu, 10, "1 挡应记录 10TU 延迟。");
        _test.Eq(tierTwo.delayed_resolution_tu, 20, "2 挡应记录 20TU 延迟。");
        _test.Eq(tierOne.delayed_resolution_score, 4, "1 挡延迟应按 2 个 5TU 计 4 分。");
        _test.Eq(tierTwo.delayed_resolution_score, 8, "2 挡延迟应按 4 个 5TU 计 8 分。");
        _test.Eq(
            tierTwo.total_score - tierOne.total_score,
            -(
                tierTwo.resource_cost_score
                - tierOne.resource_cost_score
                + tierTwo.delayed_resolution_score
                - tierOne.delayed_resolution_score
            ),
            "控制其他权重后，挡位总分差只能来自统一资源成本与独立延迟成本。"
        );

        scoreService.SetProfile(
            noReserveProfile with
            {
                DelayedResolutionCostPer5Tu = 0,
            }
        );
        BattleAiScoreInput zeroDelayWeight = BuildWindupScoreInput(
            fixture,
            scoreService,
            tier: 2,
            includeEffects: false
        );
        _test.Eq(zeroDelayWeight.delayed_resolution_score, 0, "延迟权重 0 应关闭延迟扣分。");
        _test.Eq(
            zeroDelayWeight.total_score - tierTwo.total_score,
            8,
            "关闭延迟权重只应返还原来的 8 分延迟成本。"
        );

        scoreService.SetProfile(
            noReserveProfile with
            {
                StaminaReserveFloorBp = 6500,
                StaminaReserveBreachPenalty = 100,
                ResourceConservationWeight = 100,
            }
        );
        BattleAiScoreInput reserveTierOne = BuildWindupScoreInput(
            fixture,
            scoreService,
            tier: 1,
            includeEffects: false
        );
        BattleAiScoreInput reserveTierTwo = BuildWindupScoreInput(
            fixture,
            scoreService,
            tier: 2,
            includeEffects: false
        );
        _test.Eq(
            reserveTierOne.resource_cost_score,
            tierOne.resource_cost_score,
            "1 挡结余 70% 体力，不应触发 65% reserve floor。"
        );
        _test.Eq(
            reserveTierTwo.resource_cost_score,
            tierTwo.resource_cost_score + 100,
            "2 挡结余 60% 体力，应在统一资源评分中触发 reserve breach。"
        );

        Dictionary<string, object> trace = tierTwo.ToTraceDictionary();
        _test.Eq(
            Convert.ToInt32(trace.GetValueOrDefault("delayed_resolution_tu", -1)),
            20,
            "score trace 应携带延迟 TU。"
        );
        _test.Eq(
            Convert.ToInt32(trace.GetValueOrDefault("delayed_resolution_score", -1)),
            8,
            "score trace 应携带延迟得分。"
        );
        BattleAiScoreInput clone = BattleAiDecisionResult.CloneScoreInput(tierTwo);
        _test.Eq(clone.delayed_resolution_tu, 20, "decision clone 应保留延迟 TU。");
        _test.Eq(clone.delayed_resolution_score, 8, "decision clone 应保留延迟得分。");
        tierTwo.Seal();
        _test.True(tierTwo.MatchesSealedFingerprint(), "未变更的延迟评分应匹配 sealed fingerprint。");
        tierTwo.delayed_resolution_score++;
        _test.False(
            tierTwo.MatchesSealedFingerprint(),
            "延迟评分被事后改写时 sealed fingerprint 必须失配。"
        );
    }

    private void TestAiNonWindupDelayIsNeutral()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        SkillDefinition basicAttack = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/basic_attack.tres",
            "warrior_heavy_blow_windup_ai_neutral"
        );
        BattleAiContext context = BuildAiContext(fixture, basicAttack);
        BattleCommand command = BuildUnitSkillCommand(
            fixture.Caster,
            fixture.Target,
            basicAttack.SkillId
        );
        BattlePreview preview = BuildAllowedUnitPreview(fixture.Target);
        BattleAiScoreProfileDefinition neutralProfile =
            BattleAiScoreProfileDefinition.Default with
            {
                DamageWeight = 0,
                TargetCountWeight = 0,
                ApCostWeight = 0,
                MpCostWeight = 0,
                StaminaCostWeight = 0,
                AuraCostWeight = 0,
                CooldownWeight = 0,
                PositionObjectiveWeight = 0,
                SafeDistanceAdherenceWeight = 0,
            };
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup();
        scoreService.SetProfile(
            neutralProfile with
            {
                DelayedResolutionCostPer5Tu = 0,
            }
        );
        BattleAiScoreInput zeroWeight = scoreService.BuildSkillScoreInput(
            context,
            basicAttack,
            command,
            preview,
            Array.Empty<CombatEffectDefinition>()
        );
        scoreService.SetProfile(
            neutralProfile with
            {
                DelayedResolutionCostPer5Tu = 9,
            }
        );
        BattleAiScoreInput highWeight = scoreService.BuildSkillScoreInput(
            context,
            basicAttack,
            command,
            preview,
            Array.Empty<CombatEffectDefinition>()
        );

        _test.Eq(highWeight.delayed_resolution_tu, 0, "非蓄力技能不得凭空产生延迟。");
        _test.Eq(highWeight.delayed_resolution_score, 0, "非蓄力技能在任意延迟权重下都应为 0。");
        _test.Eq(
            highWeight.total_score,
            zeroWeight.total_score,
            "延迟权重变化不得影响非蓄力技能。"
        );
    }

    private void TestAiProductionScoringRanksWindupTradeoffs()
    {
        BattleAiScoreProfileDefinition profile =
            BattleAiScoreProfileDefinition.Default with
            {
                DamageWeight = 10,
                TargetCountWeight = 0,
                ApCostWeight = 0,
                MpCostWeight = 0,
                StaminaCostWeight = 2,
                AuraCostWeight = 0,
                CooldownWeight = 0,
                DelayedResolutionCostPer5Tu = 1,
                PositionObjectiveWeight = 0,
                SafeDistanceAdherenceWeight = 0,
                OverkillDamagePenaltyWeight = 0,
            };

        Fixture lowHpFixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        lowHpFixture.Target.SetCurrentHp(1);
        BattleAiDecision lowHpDecision = EvaluateWithProductionScore(
            lowHpFixture,
            profile
        );
        _test.Eq(
            lowHpDecision?.command?.windup_tier ?? -1,
            1,
            "两个挡位都致死时，全局致死排序相同，应由资源与延迟成本选择 1 挡。"
        );
        _test.Eq(
            lowHpDecision?.score_input?.estimated_lethal_target_count ?? -1,
            1,
            "低血量目标上的 1 挡应被生产评分识别为致死。"
        );

        Fixture highHpFixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        highHpFixture.Target.SetCurrentHp(23);
        BattleAiDecision highHpDecision = EvaluateWithProductionScore(
            highHpFixture,
            profile
        );
        _test.Eq(
            highHpDecision?.command?.windup_tier ?? -1,
            2,
            "只有高挡致死时，既有致死排序必须优先于资源与延迟成本。"
        );
        _test.Eq(
            highHpDecision?.score_input?.estimated_lethal_target_count ?? -1,
            1,
            "高血量目标上的 2 挡应被生产评分识别为致死。"
        );
    }

    private void TestAiEvaluatesEveryLegalWindupTier()
    {
        Fixture fixture = BuildFixture(skillLevel: 2, heavyWeapon: true);
        fixture.Caster.attribute_snapshot.SetValue("constitution_modifier", 6);
        var scoreFactsByTier =
            new Dictionary<int, BattleAiSkillCandidateScoreFacts>();
        BattleAiContext context = new()
        {
            state = fixture.State,
            unit_state = fixture.Caster,
            grid_service = fixture.Runtime.GetGridService(),
            trace_enabled = true,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            preview_command_callback = fixture.Runtime.PreviewCommand,
            skill_score_input_callback = (
                _,
                _,
                command,
                preview,
                _,
                _,
                candidateScoreFacts
            ) =>
            {
                if (candidateScoreFacts is BattleAiSkillCandidateScoreFacts facts)
                    scoreFactsByTier[command.windup_tier] = facts;
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = 1,
                    enemy_target_count = 1,
                    total_score = command.windup_tier * 100,
                };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = LoadSkill() }
        );
        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildWindupAiAction(),
            context
        );
        _test.Eq(
            decision?.command?.windup_tier ?? -1,
            2,
            "L2 且 CON+6 时，AI 仍只能比较 1/2 挡并选择评分更高的 2 挡。"
        );
        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "蓄力 AI 应记录一次 action trace。");
        _test.Eq(
            traces.Count > 0 ? traces[0].EvaluationCount : -1,
            2,
            "CON+6 自然允许 3 挡，但 L2 技能上限应使 AI 只评估 1/2 挡。"
        );
        _test.Eq(scoreFactsByTier.Count, 2, "每个合法挡位都应向评分器传入 canonical facts。");
        _test.Eq(
            scoreFactsByTier.GetValueOrDefault(1).FinalStaminaCost ?? -1,
            18,
            "1 挡 AI facts 应携带最终 18 体力。"
        );
        _test.Eq(
            scoreFactsByTier.GetValueOrDefault(2).FinalStaminaCost ?? -1,
            24,
            "2 挡 AI facts 应携带最终 24 体力。"
        );
        _test.Eq(
            scoreFactsByTier.GetValueOrDefault(1).DelayedResolutionTu,
            10,
            "1 挡 AI facts 应携带 10TU 延迟。"
        );
        _test.Eq(
            scoreFactsByTier.GetValueOrDefault(2).DelayedResolutionTu,
            20,
            "2 挡 AI facts 应携带 20TU 延迟。"
        );
    }

    private BattleAiDecision EvaluateWithProductionScore(
        Fixture fixture,
        BattleAiScoreProfileDefinition profile
    )
    {
        SkillDefinition skill = LoadSkill();
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup(
            new FixedRollDamageResolver(
                new GArray { 4, 4, 4, 4, 4, 4, 4, 4 },
                new GArray { 20, 20, 20, 20 }
            )
        );
        scoreService.SetProfile(profile);
        BattleAiContext context = BuildAiContext(fixture, skill);
        context.skill_cast_block_reason_callback = (_, _) =>
            BattleSkillCastBlockReasonKind.None;
        context.preview_command_callback = fixture.Runtime.PreviewCommand;
        context.skill_score_input_callback = (
            scoreContext,
            skillDefinition,
            command,
            preview,
            effectDefinitions,
            metadata,
            candidateScoreFacts
        ) =>
            scoreService.BuildSkillScoreInput(
                scoreContext,
                skillDefinition,
                command,
                preview,
                effectDefinitions,
                metadata,
                candidateScoreFacts
            );
        return new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildWindupAiAction(),
            context
        );
    }

    private BattleAiScoreInput BuildWindupScoreInput(
        Fixture fixture,
        BattleAiScoreService scoreService,
        int tier,
        bool includeEffects
    )
    {
        SkillDefinition skill = LoadSkill();
        _test.True(
            BattleWindupRules.TryBuildQuote(
                fixture.Caster,
                skill,
                tier,
                out BattleWindupQuote quote,
                out string message
            ),
            $"测试挡位 {tier} 应能生成 quote。message={message}"
        );
        IReadOnlyList<CombatEffectDefinition> effects = includeEffects
            ? BattleWindupRules.ApplyWeaponDiceMultiplier(
                skill.CombatProfile.EffectDefinitions,
                quote.WeaponDiceMultiplier
            )
            : Array.Empty<CombatEffectDefinition>();
        return scoreService.BuildSkillScoreInput(
            BuildAiContext(fixture, skill),
            skill,
            BuildCommand(fixture.Caster, fixture.Target, tier),
            BuildAllowedUnitPreview(fixture.Target),
            effects,
            new Dictionary<string, object> { ["action_base_score"] = 0 },
            new BattleAiSkillCandidateScoreFacts(
                quote.TotalStaminaCost,
                quote.TotalWindupTu
            )
        );
    }

    private static BattleAiContext BuildAiContext(
        Fixture fixture,
        SkillDefinition skillDefinition
    )
    {
        BattleAiContext context = new()
        {
            state = fixture.State,
            unit_state = fixture.Caster,
            grid_service = fixture.Runtime.GetGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            }
        );
        return context;
    }

    private static UseUnitSkillActionDefinition BuildWindupAiAction() =>
        new(
            "heavy_blow_windup_ai",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            1,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

    private static BattleAiScoreInput BuildArtificialScoreInput(
        BattleCommand command,
        BattlePreview preview,
        int totalScore
    ) =>
        new()
        {
            command = command,
            preview = preview,
            effective_target_count = 1,
            enemy_target_count = 1,
            total_score = totalScore,
        };

    private static BattlePreview BuildAllowedUnitPreview(BattleUnitState target)
    {
        BattlePreview preview = new()
        {
            allowed = true,
            resolved_anchor_coord = target.GetAnchorCoord(),
        };
        preview.AddTargetUnitId(target.unit_id);
        preview.AddTargetCoord(target.GetAnchorCoord());
        return preview;
    }

    private static BattleCommand BuildUnitSkillCommand(
        BattleUnitState caster,
        BattleUnitState target,
        StringName skillId
    )
    {
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private Fixture BuildFixture(
        int skillLevel,
        bool heavyWeapon,
        SkillDefinition skillOverride = null
    )
    {
        SkillDefinition skill = skillOverride ?? LoadSkill();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(
                new GArray { 4, 4, 4, 4, 4, 4, 4, 4 },
                new GArray { 20, 20, 20, 20 }
            )
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(20));

        BattleState state = BuildState();
        BattleUnitState caster = BuildUnit("heavy_blow_caster", "player", Vector2I.Zero, 3, 60);
        BattleUnitState target = BuildUnit("heavy_blow_target", "enemy", new Vector2I(1, 0), 0, 0);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, skillLevel, preserveZero: skillLevel == 0);
        ApplyWeapon(caster, heavyWeapon, "heavy-a");
        AddUnit(runtime, state, caster, isEnemy: false);
        AddUnit(runtime, state, target, isEnemy: true);
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        return new Fixture(runtime, state, caster, target);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "warrior_heavy_blow_windup"
        );

    private static SkillDefinition LoadSkillWithHeavyRequirement(bool requiresHeavyWeapon)
    {
        SkillDef skillDef = ResourceLoader.Load<SkillDef>(
            SkillPath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        GodotContentOwnership.RegisterBorrowedContent(
            skillDef,
            "warrior_heavy_blow_windup_authored_heavy_gate"
        );
        skillDef.combat_profile.requires_heavy_weapon = requiresHeavyWeapon;
        return SkillDefinition.FromResource(skillDef);
    }

    private static BattleState BuildState()
    {
        var state = new BattleState
        {
            battle_id = "warrior_heavy_blow_windup_regression",
            phase = "unit_acting",
            map_size = new Vector2I(5, 1),
            timeline = new BattleTimelineState { tu_per_tick = 5 },
        };
        for (int x = 0; x < state.map_size.X; x++)
        {
            var cell = new BattleCellState
            {
                coord = new Vector2I(x, 0),
                base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                base_height = 4,
            };
            cell.RecalculateRuntimeValues();
            state.SetCell(cell.coord, cell);
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int ap,
        int stamina
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: 200,
            mp: 0,
            stamina: stamina,
            ap: ap,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 200);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.StaminaMax),
            60
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("strength", 16);
        unit.attribute_snapshot.SetValue("strength_modifier", 3);
        unit.attribute_snapshot.SetValue("constitution", 14);
        unit.attribute_snapshot.SetValue("constitution_modifier", 4);
        unit.SetAnchorCoord(coord);
        unit.SetActionThresholdTyped(1_000_000);
        return unit;
    }

    private void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        (isEnemy ? state.enemy_unit_ids : state.ally_unit_ids).Add(unit.unit_id);
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true),
            $"{unit.unit_id} 应能放入测试战场。"
        );
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        bool heavy,
        StringName instanceId
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "test_heavy_greatsword",
                weapon_instance_id = instanceId,
                weapon_profile_type_id = "greatsword",
                weapon_range_type = "melee",
                weapon_family = "greatsword",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice(),
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 2,
                    dice_sides = 6,
                    flat_bonus = 3,
                },
                weapon_uses_two_hands = true,
                weapon_is_heavy = heavy,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target,
        int tier
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
            windup_tier = tier,
        };
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static BattleEventBatch AdvanceTimelineTu(Fixture fixture, int totalTu)
    {
        fixture.State.phase = "timeline_running";
        fixture.State.active_unit_id = "";
        fixture.State.timeline.ready_unit_ids.Clear();
        return fixture.Runtime.advance(totalTu / 5);
    }

    private static bool LogsContain(IEnumerable<string> lines, string needle)
    {
        foreach (string line in lines ?? Array.Empty<string>())
        {
            if (line?.Contains(needle, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    private sealed class SingleSkillCatalog : ISkillCatalog
    {
        private readonly IReadOnlyDictionary<StringName, SkillDefinition> _definitions;

        internal SingleSkillCatalog(SkillDefinition skillDefinition)
        {
            _definitions = new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            };
        }

        public long GetRevision() => 1;

        public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
            _definitions;

        public bool HasSkill(StringName skillId) => _definitions.ContainsKey(skillId);

        public bool TryGetSkillDefinition(
            StringName skillId,
            out SkillDefinition skillDefinition
        ) => _definitions.TryGetValue(skillId, out skillDefinition);

        public SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(
            StringName skillId,
            int skillLevel
        ) =>
            TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition)
                ? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel)
                : SkillEffectiveCombatDefinition.BuildMissing(skillLevel);

        public CombatSkillResourceCosts GetEffectiveResourceCostValues(
            StringName skillId,
            int skillLevel
        ) => GetEffectiveCombatDefinition(skillId, skillLevel).ResourceCosts;

        public int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AttackRollBonus;

        public StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AreaPattern;

        public int GetEffectiveAreaValue(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AreaValue;

        public int GetEffectiveRangeValue(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).RangeValue;

        public int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).MaxTargetCount;

        public IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariantDefinitions(
            StringName skillId,
            int skillLevel
        ) => GetEffectiveCombatDefinition(skillId, skillLevel).UnlockedCastVariants;
    }

    private sealed class TestBattleSelectionPort : IGameRuntimeBattleSelectionPort
    {
        private static readonly IReadOnlyDictionary<
            StringName,
            EquipmentAbilityBindingDefinition
        > EmptyBindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        private readonly Fixture _fixture;
        private readonly ISkillCatalog _skillCatalog;

        internal TestBattleSelectionPort(Fixture fixture, ISkillCatalog skillCatalog)
        {
            _fixture = fixture;
            _skillCatalog = skillCatalog;
        }

        internal StringName SelectedSkillId { get; set; } = "";
        internal StringName SelectedSkillEntryId { get; set; } = "";
        internal StringName SelectedSkillVariantId { get; set; } = "";
        internal int SelectedWindupTier { get; set; } = 1;
        internal GameRuntimeBattleSelectionStage SelectionStage { get; set; } =
            GameRuntimeBattleSelectionStage.Target;
        internal StringName LastManualUnitId { get; set; } = "";
        internal List<Vector2I> TargetCoords { get; } = new();
        internal List<StringName> TargetUnitIds { get; } = new();
        internal Vector2I SelectedCoord { get; set; } = new(-1, -1);
        internal int RefreshCount { get; private set; }
        internal string LastStatus { get; private set; } = "";
        internal BattleCommand LastIssuedCommand { get; private set; }
        internal BattleCommand LastPreviewCommand { get; private set; }

        public Vector2I GetBattleSelectedCoord() => SelectedCoord;

        public BattleUnitState GetManualBattleUnit() => _fixture.Caster;

        public BattleUnitState GetRuntimeBattleActiveUnit() => _fixture.Caster;

        public BattleUnitState GetRuntimeBattleUnitAtCoord(Vector2I coord)
        {
            if (_fixture.Caster.GetAnchorCoord() == coord)
                return _fixture.Caster;
            return _fixture.Target.GetAnchorCoord() == coord ? _fixture.Target : null;
        }

        public BattleUnitState GetRuntimeBattleUnitById(StringName unitId) =>
            _fixture.State.GetUnit(unitId);

        public BattleState GetBattleState() => _fixture.State;

        public BattleGridService GetBattleGridService() =>
            _fixture.Runtime.GetGridService();

        public ISkillCatalog GetSkillCatalog() => _skillCatalog;

        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            GetEquipmentAbilityBindings() => EmptyBindings;

        public int GetBattleWorldStep() => 0;

        public BattlePreview PreviewBattleCommand(BattleCommand command)
        {
            LastPreviewCommand = command;
            return _fixture.Runtime.PreviewCommand(command);
        }

        public string GetBattleSkillCastBlockMessage(
            BattleUnitState activeUnit,
            StringName skillId
        ) => "";

        public BattleRefreshMode IssueBattleCommand(BattleCommand command)
        {
            LastIssuedCommand = command;
            return BattleRefreshMode.None;
        }

        public void RefreshBattleSelectionState() => RefreshCount++;

        public void UpdateStatus(string message) => LastStatus = message ?? "";

        public string FormatCoord(Vector2I coord) => coord.ToString();

        public bool IsBattleActive() => true;

        public StringName GetSelectedSkillId() => SelectedSkillId;

        public StringName GetSelectedSkillEntryId() => SelectedSkillEntryId;

        public void SetSelectedSkillEntryId(StringName skillEntryId) =>
            SelectedSkillEntryId = skillEntryId;

        public void SetSelectedSkillId(StringName skillId) =>
            SelectedSkillId = skillId;

        public StringName GetSelectedSkillVariantId() => SelectedSkillVariantId;

        public void SetSelectedSkillVariantId(StringName variantId) =>
            SelectedSkillVariantId = variantId;

        public int GetSelectedWindupTier() => SelectedWindupTier;

        public void SetSelectedWindupTier(int tier) =>
            SelectedWindupTier = Math.Max(tier, 1);

        public GameRuntimeBattleSelectionStage GetSelectionStage() => SelectionStage;

        public void SetSelectionStage(GameRuntimeBattleSelectionStage stage) =>
            SelectionStage = stage;

        public StringName GetLastManualUnitId() => LastManualUnitId;

        public void SetLastManualUnitId(StringName unitId) =>
            LastManualUnitId = unitId;

        public IReadOnlyList<Vector2I> GetTargetCoords() => TargetCoords;

        public void SetTargetCoords(IEnumerable<Vector2I> targetCoords)
        {
            TargetCoords.Clear();
            TargetCoords.AddRange(targetCoords ?? Array.Empty<Vector2I>());
        }

        public IReadOnlyList<StringName> GetTargetUnitIds() => TargetUnitIds;

        public void SetTargetUnitIds(IEnumerable<StringName> targetUnitIds)
        {
            TargetUnitIds.Clear();
            TargetUnitIds.AddRange(targetUnitIds ?? Array.Empty<StringName>());
        }

        public void SetBattleSelectedCoord(Vector2I coord) => SelectedCoord = coord;
    }

    private sealed record Fixture(
        BattleRuntimeModule Runtime,
        BattleState State,
        BattleUnitState Caster,
        BattleUnitState Target
    );
}

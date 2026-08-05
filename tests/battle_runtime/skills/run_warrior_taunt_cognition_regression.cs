using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_warrior_taunt_cognition_regression :
    LifecycleTestSceneTree
{
    private const string SkillPath =
        "res://data/configs/skills/warrior_taunt.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            ContentSnapshot snapshot =
                GameSessionTestFactory.GetProcessSnapshot();
            _test.True(
                snapshot.Skills.TryGetValue(
                    "warrior_taunt",
                    out SkillDefinition taunt
                ),
                "挑衅正式技能应可从内容快照加载。"
            );
            TestAuthoredSkillContract(taunt);
            TestCognitionOrderingAndTargetRequirement(taunt);
            TestFormalCommandRejectsIneligibleBeforePayment(taunt);
            TestLevelFourRowAppliesOnlyToSapientTargets(taunt);
            TestLevelFourRowHitsAllThreeSapientTargets(taunt);
            TestStatusAndEquipmentCeilingsUseStrongestRestriction();
            TestTauntSuspendsAndResumesWithoutDeletingStatus();
            TestCognitionCloneAndStrictRoundtrip();
            TestOfficialEnemyCognitionClassification(snapshot);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(
            _test.Finish("Warrior taunt cognition regression")
        );
    }

    private void TestAuthoredSkillContract(SkillDefinition taunt)
    {
        _test.True(
            taunt?.CombatProfile != null,
            "挑衅应具有正式 combat_profile。"
        );
        if (taunt?.CombatProfile == null)
        {
            return;
        }

        _test.Eq(taunt.MaxLevel, 5, "挑衅核心等级上限应为5级。");
        _test.Eq(
            taunt.NonCoreMaxLevel,
            3,
            "挑衅非核心等级上限应为3级。"
        );
        _test.True(
            taunt.Description.Contains("能理解")
                && taunt.Description.Contains("失去理智")
                && taunt.Description.Contains("暂时失效"),
            "总描述应明确理解门槛以及失去理智时仅暂停挑衅。"
        );
        _test.False(
            FileAccess.GetFileAsString(SkillPath)
                .Contains("attack_roll_bonus"),
            "无实际加值的 attack_roll_bonus=0 不应留在技能资源中。"
        );

        int[] durations = { 40, 50, 50, 60, 60, 60 };
        int[] staminaCosts = { 30, 30, 25, 25, 25, 25 };
        int[] cooldowns = { 120, 120, 120, 120, 120, 100 };
        for (int level = 0; level <= 5; level++)
        {
            CombatSkillResourceCosts costs =
                taunt.CombatProfile.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"挑衅{level}级应消耗1AP。");
            _test.Eq(
                costs.StaminaCost,
                staminaCosts[level],
                $"挑衅{level}级体力消耗应匹配批准曲线。"
            );
            _test.Eq(
                costs.CooldownTu,
                cooldowns[level],
                $"挑衅{level}级冷却应匹配批准曲线。"
            );
            _test.Eq(
                taunt.CombatProfile.GetEffectiveAreaPattern(level),
                level >= 4
                    ? new StringName("front_arc")
                    : new StringName("single"),
                $"挑衅{level}级范围形状应匹配批准方案。"
            );
            _test.Eq(
                taunt.CombatProfile.GetEffectiveAreaValue(level),
                level >= 4 ? 1 : 0,
                $"挑衅{level}级范围参数应匹配批准方案。"
            );
            _test.Eq(
                taunt.CombatProfile.GetEffectiveMaxTargetCount(level),
                level >= 4 ? 3 : 1,
                $"挑衅{level}级目标上限应匹配批准方案。"
            );

            List<CombatEffectDefinition> active =
                ActiveEffects(
                    taunt.CombatProfile.EffectDefinitions,
                    level
                );
            _test.Eq(
                active.Count,
                1,
                $"挑衅{level}级应恰好启用一条等级对应状态效果。"
            );
            if (active.Count == 1)
            {
                CombatEffectDefinition effect = active[0];
                _test.Eq(
                    effect.StatusId,
                    BattleStatusSemanticTable.STATUS_TAUNTED,
                    $"挑衅{level}级应施加正式 taunted 状态。"
                );
                _test.Eq(
                    effect.DurationTu,
                    durations[level],
                    $"挑衅{level}级持续时间应匹配批准曲线。"
                );
                _test.Eq(
                    effect.RequiredTargetMinCognition,
                    BattleCognitionKind.Sapient,
                    $"挑衅{level}级应以正式认知字段要求理性心智。"
                );
            }

            string description =
                SkillLevelDescriptionFormatter.BuildLevelDescription(
                    taunt,
                    level
                );
            _test.True(
                description.Contains("理性敌人")
                    && description.Contains($"{durations[level]}TU"),
                $"挑衅{level}级文本应同时说明目标门槛与持续时间。"
            );
        }
    }

    private void TestCognitionOrderingAndTargetRequirement(
        SkillDefinition taunt
    )
    {
        _test.Eq(
            BattleCognitionContentRules.ToKind("mindless"),
            BattleCognitionKind.Mindless,
            "mindless 应映射为封闭枚举。"
        );
        _test.Eq(
            BattleCognitionContentRules.ToKind("instinctive"),
            BattleCognitionKind.Instinctive,
            "instinctive 应映射为封闭枚举。"
        );
        _test.Eq(
            BattleCognitionContentRules.ToKind("sapient"),
            BattleCognitionKind.Sapient,
            "sapient 应映射为封闭枚举。"
        );
        _test.Eq(
            BattleCognitionContentRules.ToKind("clever"),
            BattleCognitionKind.Unknown,
            "认知类型不应接受开放字符串。"
        );

        CombatEffectDefinition effect =
            ActiveEffects(
                taunt.CombatProfile.EffectDefinitions,
                0
            )[0];
        BattleUnitState target = BuildUnit(
            "cognition_requirement_target",
            "enemy",
            new Vector2I(4, 1)
        );
        target.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        _test.True(
            BattleEffectTargetRequirementRules.IsSatisfied(
                effect,
                target
            ),
            "理性心智应满足挑衅效果门槛。"
        );
        target.SetBaseCognitionKindTyped(
            BattleCognitionKind.Instinctive
        );
        _test.False(
            BattleEffectTargetRequirementRules.IsSatisfied(
                effect,
                target
            ),
            "野兽心智不应满足挑衅效果门槛。"
        );
        target.SetBaseCognitionKindTyped(BattleCognitionKind.Mindless);
        _test.False(
            BattleEffectTargetRequirementRules.IsSatisfied(
                effect,
                target
            ),
            "无自主心智不应满足挑衅效果门槛。"
        );
    }

    private void TestStatusAndEquipmentCeilingsUseStrongestRestriction()
    {
        BattleUnitState unit = BuildUnit(
            "cognition_ceiling_unit",
            "player",
            new Vector2I(1, 1)
        );
        unit.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        _test.Eq(
            unit.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Sapient,
            "没有限制源时有效认知应等于基础认知。"
        );

        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_MADNESS,
                duration = 60,
                power = 1,
                stacks = 1,
            }
        );
        _test.Eq(
            unit.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Instinctive,
            "疯狂应把理性心智上限压到野兽心智，而非无自主心智。"
        );

        unit.ReplaceEquipmentAbilityProjectionTyped(
            Array.Empty<BattleEquipmentAbilitySourceState>(),
            Array.Empty<BattleTemporalProgressModifierState>(),
            new[]
            {
                CognitionModifier(
                    "instinctive_cap",
                    BattleCognitionKind.Instinctive
                ),
                CognitionModifier(
                    "mindless_cap",
                    BattleCognitionKind.Mindless
                ),
            }
        );
        _test.Eq(
            unit.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Mindless,
            "多个认知上限同时存在时应采用最严格的最低上限。"
        );

        BattleUnitState clone = unit.clone();
        clone.ReplaceEquipmentAbilityProjectionTyped(
            Array.Empty<BattleEquipmentAbilitySourceState>(),
            Array.Empty<BattleTemporalProgressModifierState>(),
            Array.Empty<BattleCognitionCeilingModifierState>()
        );
        _test.Eq(
            clone.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Instinctive,
            "移除 clone 的装备上限后，疯狂上限仍应独立生效。"
        );
        _test.Eq(
            unit.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Mindless,
            "clone 的装备投影替换不得回写原单位。"
        );

        unit.ReplaceEquipmentAbilityProjectionTyped(
            Array.Empty<BattleEquipmentAbilitySourceState>(),
            Array.Empty<BattleTemporalProgressModifierState>(),
            Array.Empty<BattleCognitionCeilingModifierState>()
        );
        unit.EraseStatusEffect(BattleStatusSemanticTable.STATUS_MADNESS);
        _test.Eq(
            unit.GetEffectiveCognitionKindTyped(),
            BattleCognitionKind.Sapient,
            "所有来源解除后应恢复不可变的基础认知。"
        );
    }

    private void TestFormalCommandRejectsIneligibleBeforePayment(
        SkillDefinition taunt
    )
    {
        BattleUnitState caster = BuildUnit(
            "taunt_runtime_caster",
            "player",
            new Vector2I(1, 1)
        );
        BattleUnitState instinctiveTarget = BuildUnit(
            "taunt_runtime_instinctive",
            "enemy",
            new Vector2I(2, 1)
        );
        instinctiveTarget.SetBaseCognitionKindTyped(
            BattleCognitionKind.Instinctive
        );
        caster.AddKnownActiveSkill("warrior_taunt");
        caster.SetKnownSkillLevelTyped("warrior_taunt", 0);

        using BattleTestFixture fixture =
            BattleTestFixture.CreateFlatBattle(
                "taunt_ineligible_prepayment",
                new Vector2I(5, 4),
                new[] { caster },
                new[] { instinctiveTarget }
            );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                ["warrior_taunt"] = taunt,
            }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);

        BattleCommand command = BuildGroundCommand(
            caster,
            instinctiveTarget.GetAnchorCoord()
        );
        int apBefore = caster.GetCurrentAp();
        int staminaBefore = caster.GetCurrentStamina();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.False(
            preview?.allowed == true,
            "只覆盖野兽心智时，挑衅应在资源支付前拒绝。"
        );
        _test.True(
            FormatLogs(preview).Contains(
                "没有满足效果要求的有效目标"
            ),
            "拒绝预览应说明范围内没有符合认知门槛的目标。"
        );
        _test.Eq(
            caster.GetCurrentAp(),
            apBefore,
            "被认知门槛拒绝的挑衅不得消耗AP。"
        );
        _test.Eq(
            caster.GetCurrentStamina(),
            staminaBefore,
            "被认知门槛拒绝的挑衅不得消耗体力。"
        );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(
            batch != null,
            "正式 issue 路径应返回拒绝结果批次。"
        );
        _test.True(
            string.Join(" | ", batch?.LogLinesTyped ?? new List<string>())
                .Contains("没有满足效果要求的有效目标"),
            "正式 issue 路径也应报告认知门槛拒绝。"
        );
        _test.Eq(
            caster.GetCurrentAp(),
            apBefore,
            "issue 被认知门槛拒绝后不得消耗AP。"
        );
        _test.Eq(
            caster.GetCurrentStamina(),
            staminaBefore,
            "issue 被认知门槛拒绝后不得消耗体力。"
        );
        _test.Eq(
            caster.GetCooldownTyped("warrior_taunt"),
            0,
            "issue 被拒绝后不得写入技能冷却。"
        );
        _test.False(
            instinctiveTarget.HasStatusEffect(
                BattleStatusSemanticTable.STATUS_TAUNTED
            ),
            "issue 被拒绝后不得给不合格目标写入挑衅状态。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestLevelFourRowAppliesOnlyToSapientTargets(
        SkillDefinition taunt
    )
    {
        BattleUnitState caster = BuildUnit(
            "taunt_row_caster",
            "player",
            new Vector2I(1, 2)
        );
        BattleUnitState sapientTarget = BuildUnit(
            "taunt_row_sapient",
            "enemy",
            new Vector2I(2, 2)
        );
        BattleUnitState instinctiveTarget = BuildUnit(
            "taunt_row_instinctive",
            "enemy",
            new Vector2I(2, 1)
        );
        BattleUnitState mindlessTarget = BuildUnit(
            "taunt_row_mindless",
            "enemy",
            new Vector2I(2, 3)
        );
        sapientTarget.SetBaseCognitionKindTyped(
            BattleCognitionKind.Sapient
        );
        instinctiveTarget.SetBaseCognitionKindTyped(
            BattleCognitionKind.Instinctive
        );
        mindlessTarget.SetBaseCognitionKindTyped(
            BattleCognitionKind.Mindless
        );
        caster.AddKnownActiveSkill("warrior_taunt");
        caster.SetKnownSkillLevelTyped("warrior_taunt", 4);

        using BattleTestFixture fixture =
            BattleTestFixture.CreateFlatBattle(
                "taunt_level_four_row",
                new Vector2I(5, 5),
                new[] { caster },
                new[]
                {
                    sapientTarget,
                    instinctiveTarget,
                    mindlessTarget,
                }
            );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                ["warrior_taunt"] = taunt,
            }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);

        BattleCommand command = BuildGroundCommand(
            caster,
            new Vector2I(2, 2)
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"4级挑衅横向三格内有理性目标时应可施放。logs={FormatLogs(preview)}"
        );
        _test.True(
            preview?.TargetUnitIdsTyped.Contains(
                sapientTarget.unit_id
            ) == true,
            "4级挑衅预览应包含横排中的理性目标。"
        );
        _test.False(
            preview?.TargetUnitIdsTyped.Contains(
                instinctiveTarget.unit_id
            ) == true,
            "4级挑衅预览应排除横排中的野兽心智。"
        );
        _test.False(
            preview?.TargetUnitIdsTyped.Contains(
                mindlessTarget.unit_id
            ) == true,
            "4级挑衅预览应排除横排中的无自主心智。"
        );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(
            batch != null,
            "4级挑衅应通过正式命令完成结算。"
        );
        _test.Eq(
            sapientTarget
                .GetStatusEffect(
                    BattleStatusSemanticTable.STATUS_TAUNTED
                )
                ?.duration
                ?? 0,
            60,
            "4级挑衅应给理性目标施加60TU挑衅。"
        );
        _test.False(
            instinctiveTarget.HasStatusEffect(
                BattleStatusSemanticTable.STATUS_TAUNTED
            ),
            "4级挑衅不得给野兽心智施加状态。"
        );
        _test.False(
            mindlessTarget.HasStatusEffect(
                BattleStatusSemanticTable.STATUS_TAUNTED
            ),
            "4级挑衅不得给无自主心智施加状态。"
        );
        _test.Eq(
            caster.GetCurrentAp(),
            1,
            "4级挑衅成功后应消耗1AP。"
        );
        _test.Eq(
            caster.GetCurrentStamina(),
            5,
            "4级挑衅成功后应消耗25体力。"
        );
        _test.Eq(
            caster.GetCooldownTyped("warrior_taunt"),
            120,
            "4级挑衅成功后应进入120TU冷却。"
        );

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestLevelFourRowHitsAllThreeSapientTargets(
        SkillDefinition taunt
    )
    {
        BattleUnitState caster = BuildUnit(
            "taunt_three_target_caster",
            "player",
            new Vector2I(1, 2)
        );
        BattleUnitState upper = BuildUnit(
            "taunt_three_target_upper",
            "enemy",
            new Vector2I(2, 1)
        );
        BattleUnitState center = BuildUnit(
            "taunt_three_target_center",
            "enemy",
            new Vector2I(2, 2)
        );
        BattleUnitState lower = BuildUnit(
            "taunt_three_target_lower",
            "enemy",
            new Vector2I(2, 3)
        );
        BattleUnitState outside = BuildUnit(
            "taunt_three_target_outside",
            "enemy",
            new Vector2I(3, 2)
        );
        foreach (
            BattleUnitState target in new[]
            {
                upper,
                center,
                lower,
                outside,
            }
        )
        {
            target.SetBaseCognitionKindTyped(
                BattleCognitionKind.Sapient
            );
        }
        caster.AddKnownActiveSkill("warrior_taunt");
        caster.SetKnownSkillLevelTyped("warrior_taunt", 4);

        using BattleTestFixture fixture =
            BattleTestFixture.CreateFlatBattle(
                "taunt_level_four_three_sapient",
                new Vector2I(6, 5),
                new[] { caster },
                new[] { upper, center, lower, outside }
            );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                ["warrior_taunt"] = taunt,
            }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);

        BattleCommand command = BuildGroundCommand(
            caster,
            center.GetAnchorCoord()
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"4级挑衅应允许横排三个理性目标。logs={FormatLogs(preview)}"
        );
        _test.Eq(
            preview?.TargetUnitIdsTyped.Count ?? -1,
            3,
            "4级挑衅预览应精确命中横向三格，不得扩成更大范围。"
        );
        foreach (
            BattleUnitState target in new[] { upper, center, lower }
        )
        {
            _test.True(
                preview?.TargetUnitIdsTyped.Contains(target.unit_id)
                    == true,
                $"4级挑衅预览应包含理性目标 {target.unit_id}。"
            );
        }
        _test.False(
            preview?.TargetUnitIdsTyped.Contains(outside.unit_id)
                == true,
            "横向三格外的第四个理性目标不应被挑衅。"
        );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        foreach (
            BattleUnitState target in new[] { upper, center, lower }
        )
        {
            _test.Eq(
                target
                    .GetStatusEffect(
                        BattleStatusSemanticTable.STATUS_TAUNTED
                    )
                    ?.duration
                    ?? 0,
                60,
                $"4级挑衅应给理性目标 {target.unit_id} 施加60TU状态。"
            );
        }
        _test.False(
            outside.HasStatusEffect(
                BattleStatusSemanticTable.STATUS_TAUNTED
            ),
            "正式结算不得影响横向三格外的第四个理性目标。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestTauntSuspendsAndResumesWithoutDeletingStatus()
    {
        BattleState state = new();
        BattleUnitState attacker = BuildUnit(
            "taunted_attacker",
            "enemy",
            new Vector2I(4, 1)
        );
        BattleUnitState taunter = BuildUnit(
            "taunt_source",
            "player",
            new Vector2I(1, 1)
        );
        BattleUnitState otherTarget = BuildUnit(
            "taunt_other_target",
            "player",
            new Vector2I(1, 2)
        );
        attacker.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        attacker.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TAUNTED,
                source_unit_id = taunter.unit_id,
                duration = 60,
                power = 1,
                stacks = 1,
            }
        );
        AddUnits(state, attacker, taunter, otherTarget);
        var aiContext = new BattleAiContext
        {
            state = state,
            unit_state = attacker,
        };

        _test.True(
            state.IsAttackDisadvantage(attacker, otherTarget),
            "理性目标攻击挑衅者以外的单位时应处于劣势。"
        );
        _test.False(
            state.IsAttackDisadvantage(attacker, taunter),
            "攻击挑衅来源本身不应受到挑衅劣势。"
        );
        _test.True(
            ReferenceEquals(
                aiContext.ResolveForcedTargetUnit("enemy"),
                taunter
            ),
            "理性目标的 AI 应把有效挑衅来源解析为强制目标。"
        );

        attacker.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_MADNESS,
                duration = 30,
                power = 1,
                stacks = 1,
            }
        );
        _test.False(
            state.IsAttackDisadvantage(attacker, otherTarget),
            "疯狂期间有效认知不足，已有挑衅应暂停。"
        );
        _test.True(
            aiContext.ResolveForcedTargetUnit("enemy") == null,
            "疯狂期间 AI 不应继续把挑衅来源当作强制目标。"
        );
        _test.True(
            attacker.HasStatusEffect(
                BattleStatusSemanticTable.STATUS_TAUNTED
            ),
            "暂停期间不得删除挑衅状态或剩余时长。"
        );

        var statusDurationResolver =
            new BattleRuntimeSkillTurnResolver();
        _test.True(
            statusDurationResolver.AdvanceUnitStatusDurations(
                attacker,
                20
            ),
            "疯狂导致挑衅暂停时，正式时间轴状态计时仍应推进。"
        );
        _test.Eq(
            attacker
                .GetStatusEffect(
                    BattleStatusSemanticTable.STATUS_TAUNTED
                )
                ?.duration
                ?? 0,
            40,
            "挑衅暂停20TU后自身剩余时长也应从60降到40。"
        );
        _test.Eq(
            attacker
                .GetStatusEffect(
                    BattleStatusSemanticTable.STATUS_MADNESS
                )
                ?.duration
                ?? 0,
            10,
            "疯狂状态应与暂停的挑衅一起正常倒计时。"
        );
        _test.False(
            state.IsAttackDisadvantage(attacker, otherTarget),
            "疯狂尚未结束时，倒计时中的挑衅仍应保持暂停。"
        );

        statusDurationResolver.AdvanceUnitStatusDurations(
            attacker,
            10
        );
        _test.True(
            state.IsAttackDisadvantage(attacker, otherTarget),
            "疯狂自然到期且挑衅仍剩30TU时，挑衅应自动恢复。"
        );
        _test.Eq(
            attacker
                .GetStatusEffect(
                    BattleStatusSemanticTable.STATUS_TAUNTED
                )
                ?.duration
                ?? 0,
            30,
            "暂停不会冻结挑衅计时，恢复时应只剩30TU。"
        );
        _test.True(
            ReferenceEquals(
                aiContext.ResolveForcedTargetUnit("enemy"),
                taunter
            ),
            "疯狂解除后 AI 应恢复尚未到期的挑衅强制目标。"
        );

        attacker.SetBaseCognitionKindTyped(
            BattleCognitionKind.Instinctive
        );
        _test.False(
            state.IsAttackDisadvantage(attacker, otherTarget),
            "基础为野兽心智的单位即使残留挑衅状态也不应受其约束。"
        );
        _test.True(
            aiContext.ResolveForcedTargetUnit("enemy") == null,
            "基础为野兽心智的单位不应被残留挑衅强制选目标。"
        );
    }

    private void TestCognitionCloneAndStrictRoundtrip()
    {
        BattleUnitState unit = BuildUnit(
            "cognition_roundtrip_unit",
            "player",
            new Vector2I(2, 2)
        );
        unit.SetBaseCognitionKindTyped(
            BattleCognitionKind.Instinctive
        );
        BattleUnitState clone = unit.clone();
        _test.Eq(
            clone.GetBaseCognitionKindTyped(),
            BattleCognitionKind.Instinctive,
            "gameplay clone 应保留基础认知。"
        );
        clone.SetBaseCognitionKindTyped(BattleCognitionKind.Mindless);
        _test.Eq(
            unit.GetBaseCognitionKindTyped(),
            BattleCognitionKind.Instinctive,
            "clone 的基础认知修改不得回写原单位。"
        );

        using GodotProjectionLease<GDictionary> lease =
            unit.ToDictionaryLease(
                LifetimeDomain.Request,
                "warrior-taunt-cognition-roundtrip"
            );
        BattleUnitState restored =
            BattleUnitState.FromDictionary(lease.Value);
        _test.Eq(
            restored?.GetBaseCognitionKindTyped()
                ?? BattleCognitionKind.Unknown,
            BattleCognitionKind.Instinctive,
            "正式 payload round-trip 应保留基础认知。"
        );

        GDictionary invalid =
            (GDictionary)lease.Value.Duplicate(true);
        invalid["cognition_kind"] = "clever";
        _test.True(
            BattleUnitState.FromDictionary(invalid) == null,
            "单位 payload 应拒绝未知 cognition_kind，不提供旧格式回退。"
        );
        invalid.Dispose();

        GDictionary missing =
            (GDictionary)lease.Value.Duplicate(true);
        missing.Remove("cognition_kind");
        _test.True(
            BattleUnitState.FromDictionary(missing) == null,
            "单位 payload 缺少 cognition_kind 时应 fail closed，不提供旧69字段兼容。"
        );
        missing.Dispose();
    }

    private void TestOfficialEnemyCognitionClassification(
        ContentSnapshot snapshot
    )
    {
        foreach (
            (StringName templateId, EnemyTemplateDefinition template)
                in snapshot.EnemyTemplates
        )
        {
            _test.True(
                template != null
                    && BattleCognitionContentRules.IsKnown(
                        template.CognitionKind
                    ),
                $"正式敌人模板 {templateId} 必须投影已知认知类型。"
            );
        }
        _test.Eq(
            snapshot.EnemyTemplates["skeleton_soldier"]
                .CognitionKind,
            BattleCognitionKind.Mindless,
            "骷髅士兵应是无自主心智。"
        );
        _test.Eq(
            snapshot.EnemyTemplates["wolf_raider"].CognitionKind,
            BattleCognitionKind.Instinctive,
            "普通狼群单位应是野兽心智。"
        );
        _test.Eq(
            snapshot.EnemyTemplates["red_dragon"].CognitionKind,
            BattleCognitionKind.Sapient,
            "红龙应是理性心智，不能由智力阈值或 creature tag 猜测。"
        );
    }

    private static List<CombatEffectDefinition> ActiveEffects(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in effects
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect != null
                && level >= Math.Max(effect.MinSkillLevel, 0)
                && (
                    effect.MaxSkillLevel < 0
                    || level <= effect.MaxSkillLevel
                )
            )
            {
                result.Add(effect);
            }
        }
        return result;
    }

    private static BattleCognitionCeilingModifierState
        CognitionModifier(
            StringName modifierId,
            BattleCognitionKind ceiling
        ) =>
            new()
            {
                ModifierId = modifierId,
                BindingId = $"{modifierId}_binding",
                SourceEquipmentInstanceId =
                    $"{modifierId}_equipment",
                Ceiling = ceiling,
            };

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: 30,
            mp: 10,
            stamina: 30,
            aura: 0,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.HpMax),
            30
        );
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleCommand BuildGroundCommand(
        BattleUnitState caster,
        Vector2I targetCoord
    )
    {
        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(
                BattleCommandKind.Skill
            ),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(
                "warrior_taunt"
            ),
            skill_id = "warrior_taunt",
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static string FormatLogs(BattlePreview preview) =>
        preview == null
            ? ""
            : string.Join(" | ", preview.LogLinesTyped);

    private static void AddUnits(
        BattleState state,
        params BattleUnitState[] units
    )
    {
        foreach (BattleUnitState unit in units)
        {
            state.SetUnit(unit);
            if (unit.faction_id == "enemy")
            {
                state.enemy_unit_ids.Add(unit.unit_id);
            }
            else
            {
                state.ally_unit_ids.Add(unit.unit_id);
            }
        }
    }
}

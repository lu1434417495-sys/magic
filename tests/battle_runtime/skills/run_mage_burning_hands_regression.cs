using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_mage_burning_hands_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "res://data/configs/skills/mage_burning_hands.tres";
    private static readonly StringName SkillId = "mage_burning_hands";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                SkillPath,
                "mage_burning_hands_regression"
            );
            TestAuthoredContractAndLevelCurve(skill);
            TestOneSaveControlsDamageAndBurning(skill);
            TestMasteryUsesHighDamageOrCriticalPerTarget(skill);
            TestGroundPreviewUsesAdjacentDirectionAndProjectsDamageAndBurning(skill);
            TestAiScoresMarginalBurningSource(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage burning hands regression"));
    }

    private void TestAuthoredContractAndLevelCurve(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(skill != null && combat != null, "焚掌喷流正式资源与 combat_profile 必须可加载。" );
        if (skill == null || combat == null)
            return;
        _test.Eq(skill.SkillId, SkillId, "技能 ID 必须保持稳定。" );
        _test.Eq(skill.GrowthTier, new StringName("advanced"), "大范围高伤法术应保持 advanced 成长档。" );
        _test.Eq(skill.MaxLevel, 7, "技能等级上限应为7。" );
        _test.Eq(skill.NonCoreMaxLevel, 5, "非核心上限应保持5。" );
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Ground, "技能应采用地面定向。" );
        _test.Eq(combat.AreaPattern, new StringName("cone"), "技能应保持锥形范围。" );
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "技能应由敏捷豁免直接结算，不进行AC攻击检定。"
        );
        _test.True(skill.Description.Contains("形成独立来源"), "玩家描述必须解释来源叠加。" );
        _test.True(skill.Description.Contains("敏捷豁免成功伤害减半"), "玩家描述必须解释单次豁免分支。" );

        AssertLevel(skill, 0, 3, 1, 10, 0, 0, 0);
        AssertLevel(skill, 1, 4, 1, 10, 0, 0, 0);
        AssertLevel(skill, 2, 4, 1, 10, 0, 0, 0);
        AssertLevel(skill, 3, 5, 1, 10, 1, 20, 10);
        AssertLevel(skill, 4, 5, 1, 10, 1, 20, 10);
        AssertLevel(skill, 5, 6, 2, 5, 2, 20, 10);
        AssertLevel(skill, 6, 6, 2, 5, 2, 20, 10);
        AssertLevel(skill, 7, 7, 2, 5, 2, 30, 10);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int diceCount,
        int areaValue,
        int cooldownTu,
        int burningPower,
        int burningDurationTu,
        int burningTickTu
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        CombatEffectDefinition damage = FindDamage(skill, level);
        _test.Eq(combat.GetEffectiveRangeValue(level), 1, $"L{level}必须选择相邻格确定方向。" );
        _test.Eq(combat.GetEffectiveAreaValue(level), areaValue, $"L{level}锥形范围不符。" );
        _test.Eq(combat.GetEffectiveAttackRollBonus(level), 0, $"L{level}不得残留攻击检定加值。" );
        _test.Eq(costs.ApCost, 2, $"L{level}应消耗2AP。" );
        _test.Eq(costs.MpCost, 70, $"L{level}应消耗70法力。" );
        _test.Eq(costs.CooldownTu, cooldownTu, $"L{level}冷却不符。" );
        _test.Eq(damage?.DiceCount ?? -1, diceCount, $"L{level}伤害骰数量不符。" );
        _test.Eq(damage?.DiceSides ?? -1, 6, $"L{level}应使用D6。" );
        _test.Eq(damage?.DamageTag ?? new StringName(""), new StringName("fire"), $"L{level}应为火焰伤害。" );
        _test.Eq(damage?.SaveAbility ?? new StringName(""), new StringName("agility"), $"L{level}应进行敏捷豁免。" );
        _test.True(damage?.SavePartialOnSuccess == true, $"L{level}豁免成功应半伤。" );
        _test.Eq(damage?.Power ?? -1, burningPower, $"L{level}燃烧强度不符。" );
        _test.Eq(damage?.DurationTu ?? -1, burningDurationTu, $"L{level}燃烧时长不符。" );
        _test.Eq(damage?.TickIntervalTu ?? -1, burningTickTu, $"L{level}燃烧间隔不符。" );
        _test.Eq(
            damage?.SaveFailureStatusId ?? new StringName(""),
            burningPower > 0 ? new StringName("burning") : new StringName(""),
            $"L{level}豁免失败燃烧配置不符。"
        );
        _test.Eq(
            combat.EffectDefinitions.Count(effect => effect?.IsUnlockedAtSkillLevel(level) == true),
            1,
            $"L{level}只应有一个伤害效果，避免第二次独立燃烧豁免。"
        );
    }

    private void TestOneSaveControlsDamageAndBurning(SkillDefinition skill)
    {
        CombatEffectDefinition effect = FindDamage(skill, 3);
        BattleUnitState source = BuildUnit("burning_hands_source", "player", Vector2I.Zero, 3);
        BattleUnitState targetFailure = BuildUnit("burning_hands_failure", "enemy", Vector2I.One, 0);
        BattleUnitState targetSuccess = BuildUnit("burning_hands_success", "enemy", Vector2I.One, 0);
        using var resolver = new FixedRollDamageResolver(Ones(20));

        AttackEffectResolutionResult failure = resolver.ResolveEffects(
            source,
            targetFailure,
            new[] { effect },
            BuildDamageContext(saveRoll: 1)
        );
        _test.True(failure.Damage > 0, "豁免失败必须受到正式火焰伤害。" );
        BattleStatusEffectState burning = targetFailure.GetStatusEffect("burning");
        _test.True(burning != null, "同一次豁免失败必须附加燃烧。" );
        _test.Eq(burning?.GetSourceContributionsTyped().Count ?? -1, 1, "首次施放应只有一个来源。" );
        BattleStatusSourceIdentity sourceIdentity = BattleStatusSourceIdentity.Skill(
            source.unit_id,
            SkillId
        );
        _test.Eq(
            burning?.GetSourceContributionTyped(sourceIdentity)?.Stacks ?? -1,
            1,
            "燃烧来源必须记录施法者与技能定义。"
        );

        AttackEffectResolutionResult success = resolver.ResolveEffects(
            source,
            targetSuccess,
            new[] { effect },
            BuildDamageContext(saveRoll: 20)
        );
        _test.True(success.Damage > 0, "豁免成功仍应受到半额伤害。" );
        _test.True(
            success.Damage < failure.Damage,
            "同一伤害骰下，豁免成功伤害必须低于失败分支。"
        );
        _test.False(targetSuccess.HasStatusEffect("burning"), "豁免成功不得附加燃烧。" );

        resolver.ResolveEffects(
            source,
            targetFailure,
            new[] { effect },
            BuildDamageContext(saveRoll: 1)
        );
        _test.Eq(
            targetFailure.GetStatusEffect("burning")
                ?.GetSourceContributionTyped(sourceIdentity)?.Stacks ?? -1,
            2,
            "同一施法者同一技能再次命中应叠加同源层数。"
        );

        BattleUnitState otherSource = BuildUnit("burning_hands_other_source", "player", Vector2I.Zero, 3);
        resolver.ResolveEffects(
            otherSource,
            targetFailure,
            new[] { effect },
            DamageResolutionContext.FromDictionary(
                new GDictionary
                {
                    ["save_roll_override"] = 1,
                    ["skill_id"] = SkillId,
                    ["source_skill_level"] = 3,
                }
            )
        );
        _test.Eq(
            targetFailure.GetStatusEffect("burning")?.GetSourceContributionsTyped().Count ?? -1,
            2,
            "不同施法者的同一技能必须形成独立燃烧来源。"
        );
    }

    private void TestMasteryUsesHighDamageOrCriticalPerTarget(SkillDefinition skill)
    {
        CombatEffectDefinition effect = FindDamage(skill, 3);
        BattleUnitState source = BuildUnit(
            "burning_hands_mastery_source",
            "player",
            Vector2I.Zero,
            3
        );
        source.source_member_id = "mastery_hero";
        BattleUnitState normalTarget = BuildUnit(
            "burning_hands_mastery_normal",
            "enemy",
            Vector2I.One,
            0
        );
        BattleUnitState eliteTarget = BuildUnit(
            "burning_hands_mastery_elite",
            "enemy",
            Vector2I.One,
            0
        );
        eliteTarget.attribute_snapshot.SetValue("fortune_mark_target", 1);
        using var mastery = new BattleSkillMasteryService();

        using var belowThresholdResolver = new FixedRollDamageResolver(
            Rolls(5, 5, 5, 4, 4)
        );
        AttackEffectResolutionResult belowThreshold = belowThresholdResolver.ResolveEffects(
            source,
            normalTarget,
            new[] { effect },
            BuildDamageContext(saveRoll: 1)
        );
        _test.False(
            belowThreshold.DamageDiceHighTotalRoll,
            "5D6掷出23点未达到理论最大值30的80%，不得产生高伤骰事件。"
        );
        mastery.RecordTargetResult(source, normalTarget, skill, belowThreshold);
        _test.Eq(
            mastery.ResolveActiveSkillMasteryAmount(),
            0,
            "低于80%的有效伤害不得增长熟练度。"
        );

        using var thresholdResolver = new FixedRollDamageResolver(Rolls(5, 5, 5, 5, 4));
        BattleUnitState thresholdTarget = BuildUnit(
            "burning_hands_mastery_threshold",
            "enemy",
            Vector2I.One,
            0
        );
        AttackEffectResolutionResult threshold = thresholdResolver.ResolveEffects(
            source,
            thresholdTarget,
            new[] { effect },
            BuildDamageContext(saveRoll: 1)
        );
        _test.True(
            threshold.DamageDiceHighTotalRoll,
            "5D6掷出24点恰好达到理论最大值的80%，必须产生高伤骰事件。"
        );
        _test.False(
            threshold.SkillDamageDiceIsMax,
            "80%高伤骰不得依赖全部技能骰满值。"
        );
        mastery.RecordTargetResult(source, thresholdTarget, skill, threshold);
        _test.Eq(
            mastery.ResolveActiveSkillMasteryAmount(),
            1,
            "普通目标的80%高伤骰应贡献1点熟练度。"
        );

        using var criticalResolver = new FixedRollDamageResolver(Ones(10));
        AttackEffectResolutionResult critical = criticalResolver.ResolveEffects(
            source,
            eliteTarget,
            new[] { effect },
            BuildDamageContext(saveRoll: 1, criticalHit: true)
        );
        _test.True(critical.DamageDiceHighTotalRoll, "低骰暴击也必须产生高伤骰事件。" );
        mastery.RecordTargetResult(source, eliteTarget, skill, critical);
        _test.Eq(
            mastery.ResolveActiveSkillMasteryAmount(),
            3,
            "范围法术应按目标分别累计：普通目标1点加精英目标2点。"
        );

        mastery.Clear();
        using (
            GodotProjectionLease<GDictionary> thresholdLease =
                AttackEffectResolutionResultReader.BuildGodotPayloadLease(threshold)
        )
        {
            mastery.RecordTargetResult(source, thresholdTarget, skill, thresholdLease.Value);
        }
        _test.Eq(
            mastery.ResolveActiveSkillMasteryAmount(),
            1,
            "Dictionary结果路径必须与typed结果路径一致读取80%高伤骰事件。"
        );

        mastery.Clear();
        BattleUnitState immuneTarget = BuildUnit(
            "burning_hands_mastery_immune",
            "enemy",
            Vector2I.One,
            0
        );
        immuneTarget.SetDamageResistanceTyped("fire", "immune");
        using var immuneResolver = new FixedRollDamageResolver(Rolls(5, 5, 5, 5, 4));
        AttackEffectResolutionResult immune = immuneResolver.ResolveEffects(
            source,
            immuneTarget,
            new[] { effect },
            BuildDamageContext(saveRoll: 1)
        );
        _test.True(immune.DamageDiceHighTotalRoll, "免疫不应抹掉原始高伤骰事实。" );
        _test.Eq(immune.Damage, 0, "火焰免疫目标最终不得受到伤害。" );
        _test.Eq(immune.ShieldAbsorbed, 0, "免疫目标不应伪造护盾吸收。" );
        mastery.RecordTargetResult(source, immuneTarget, skill, immune);
        _test.Eq(
            mastery.ResolveActiveSkillMasteryAmount(),
            0,
            "没有实际伤害或护盾吸收时，即使达到80%也不得增长熟练度。"
        );
    }

    private void TestGroundPreviewUsesAdjacentDirectionAndProjectsDamageAndBurning(
        SkillDefinition skill
    )
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [SkillId] = skill });
        BattleState state = BuildState("burning_hands_preview", new Vector2I(5, 5));
        BattleUnitState caster = BuildUnit(
            "burning_hands_preview_caster",
            "player",
            new Vector2I(1, 2),
            3
        );
        BattleUnitState target = BuildUnit(
            "burning_hands_preview_target",
            "enemy",
            new Vector2I(2, 2),
            0
        );
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        BattleCommand command = BuildGroundCommand(caster, new Vector2I(2, 2));
        BattlePreview preview = runtime.PreviewCommand(command);

        _test.True(
            preview?.allowed == true,
            $"相邻格应能确定喷流方向。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
        );
        _test.True(preview?.DamagePreviewTyped != null, "地面锥形预览必须给出 canonical 伤害范围。" );
        BattleStatusContributionPreviewData statusPreview =
            preview?.StatusContributionPreviewsTyped.FirstOrDefault(
                value => value.TargetUnitId == target.unit_id
            );
        _test.True(statusPreview != null, "地面预览必须显示目标的燃烧来源变化。" );
        _test.True(statusPreview?.AppliesOnSaveFailure == true, "预览必须明确燃烧只在豁免失败时发生。" );
        _test.True(statusPreview?.AddsNewSource == true, "无现有同源燃烧时应显示新增来源。" );
        _test.Eq(statusPreview?.SourceStackLimit ?? -1, 3, "预览必须显示同源3层上限。" );
        _test.False(target.HasStatusEffect("burning"), "canonical preview 不得改写目标状态。" );

        using GodotProjectionLease<GDictionary> lease = BattlePreviewProjection.BuildLease(preview);
        _test.True(
            lease.Value.ContainsKey("status_contribution_previews")
                && lease.Value["status_contribution_previews"].VariantType == Variant.Type.Array
                && lease.Value["status_contribution_previews"].AsGodotArray().Count > 0,
            "Godot 边界投影必须携带来源贡献预览。"
        );
        using var hudAdapter = new BattleHudAdapter();
        BattleHudSnapshot hudSnapshot = hudAdapter.BuildSnapshot(
            state,
            target.GetAnchorCoord(),
            SkillId,
            skill.DisplayName,
            "",
            new Godot.Collections.Array<Vector2I> { target.GetAnchorCoord() },
            1,
            new Godot.Collections.Array<StringName> { target.unit_id },
            "",
            "焚掌喷流测试",
            preview
        );
        _test.True(
            hudSnapshot.SelectedSkillPreviewTooltipText.Contains("豁免失败时新增独立来源"),
            "HUD技能提示必须展示canonical燃烧来源变化。"
        );
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
        runtime.Dispose();
    }

    private void TestAiScoresMarginalBurningSource(SkillDefinition skill)
    {
        BattleState state = BuildState("burning_hands_ai", new Vector2I(5, 3));
        BattleGridService grid = new();
        BattleUnitState actor = BuildUnit("burning_hands_ai_actor", "hostile", new Vector2I(1, 1), 3);
        BattleUnitState target = BuildUnit("burning_hands_ai_target", "player", new Vector2I(2, 1), 0);
        state.SetUnit(actor);
        state.SetUnit(target);
        state.enemy_unit_ids.Add(actor.unit_id);
        state.ally_unit_ids.Add(target.unit_id);
        grid.PlaceUnit(state, actor, actor.GetAnchorCoord(), true);
        grid.PlaceUnit(state, target, target.GetAnchorCoord(), true);
        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = grid,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup(new BattleDamageResolver());

        BattleAiScoreInput cleanTargetScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.Eq(cleanTargetScore?.estimated_status_count ?? -1, 1, "AI应识别首次燃烧的边际收益。" );
        _test.Eq(cleanTargetScore?.estimated_control_count ?? -1, 0, "燃烧不得伪装成硬控制。" );

        CombatEffectDefinition effect = FindDamage(skill, 3);
        BattleStatusEffectState otherSourceBurning = null;
        for (int index = 0; index < 3; index++)
        {
            otherSourceBurning = BattleStatusSemanticTable.MergeStatus(
                effect,
                "other_caster",
                otherSourceBurning,
                "burning",
                BattleStatusSourceIdentity.Skill("other_caster", SkillId)
            );
        }
        target.SetStatusEffect(otherSourceBurning);
        BattleAiScoreInput otherSourceCappedScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.Eq(
            otherSourceCappedScore?.estimated_status_count ?? -1,
            1,
            "其他来源已满3层时，AI仍应评价本施法者的新来源。"
        );

        BattleStatusEffectState sameSourceBurning = target.GetStatusEffect("burning");
        for (int index = 0; index < 3; index++)
        {
            sameSourceBurning = BattleStatusSemanticTable.MergeStatus(
                effect,
                actor.unit_id,
                sameSourceBurning,
                "burning",
                BattleStatusSourceIdentity.Skill(actor.unit_id, SkillId)
            );
        }
        target.SetStatusEffect(sameSourceBurning);
        BattleAiScoreInput sameSourceCappedScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.Eq(
            sameSourceCappedScore?.estimated_status_count ?? -1,
            0,
            "自身同技能来源已满层且时长不增长时，AI不得重复领取状态收益。"
        );
    }

    private static BattleAiScoreInput BuildAiScore(
        BattleAiScoreService scoreService,
        BattleAiContext context,
        SkillDefinition skill,
        BattleUnitState actor,
        BattleUnitState target
    )
    {
        BattleCommand command = BuildGroundCommand(actor, target.GetAnchorCoord());
        var preview = new BattlePreview { allowed = true };
        preview.AddTargetCoord(target.GetAnchorCoord());
        preview.AddTargetUnitId(target.unit_id);
        return scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            new[] { FindDamage(skill, 3) },
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["desired_min_distance"] = 0,
                ["desired_max_distance"] = 1,
                ["position_target_unit_id"] = target.unit_id,
            }
        );
    }

    private static CombatEffectDefinition FindDamage(SkillDefinition skill, int level) =>
        skill?.CombatProfile?.EffectDefinitions.SingleOrDefault(
            effect => effect?.EffectKind == BattleEffectKind.Damage
                && effect.IsUnlockedAtSkillLevel(level)
        );

    private static DamageResolutionContext BuildDamageContext(
        int saveRoll,
        bool criticalHit = false
    ) =>
        DamageResolutionContext.FromDictionary(
            new GDictionary
            {
                ["save_roll_override"] = saveRoll,
                ["skill_id"] = SkillId,
                ["source_skill_level"] = 3,
                ["critical_hit"] = criticalHit,
            }
        );

    private static GArray Rolls(params int[] values)
    {
        GArray result = new();
        foreach (int value in values ?? Array.Empty<int>())
            result.Add(value);
        return result;
    }

    private static GArray Ones(int count)
    {
        GArray result = new();
        for (int index = 0; index < count; index++)
            result.Add(1);
        return result;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int skillLevel
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(hp: 100, mp: 200, stamina: 20, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 200);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("agility_modifier", 0);
        unit.attribute_snapshot.SetValue("intelligence", 18);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 4);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetAnchorCoord(coord);
        if (skillLevel > 0)
        {
            unit.AddKnownActiveSkill(SkillId);
            unit.SetKnownSkillLevelTyped(SkillId, skillLevel);
        }
        return unit;
    }

    private static BattleState BuildState(string battleId, Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        state.SetUnit(unit);
        if (unit.faction_id == "enemy")
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static BattleCommand BuildGroundCommand(
        BattleUnitState actor,
        Vector2I targetCoord
    ) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = actor.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = targetCoord,
        };
}

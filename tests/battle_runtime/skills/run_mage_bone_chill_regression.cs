using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_mage_bone_chill_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "res://data/configs/skills/mage_bone_chill.tres";
    private const string HealSkillPath =
        "res://data/configs/skills/warrior_battle_recovery.tres";
    private static readonly StringName SkillId = "mage_bone_chill";
    private static readonly StringName HealSkillId = "warrior_battle_recovery";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill(SkillPath, "mage_bone_chill_regression");
            TestAuthoredContractAndLevelCurve(skill);
            TestExcludedCreatureTagSchemaRejectsEmptyAndDuplicates();
            TestCreatureTypeRestrictionRejectsBeforeCost(skill);
            TestPreviewAndExecutionKeepHealingReductionsIndependent(skill);
            TestMissPaysCostsWithoutDamageOrBoneChill(skill);
            TestAiScoresOnlyMarginalHealingDenied(skill);
            TestAiDoesNotEnumerateExcludedCreatureTypes(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage bone chill regression"));
    }

    private void TestExcludedCreatureTagSchemaRejectsEmptyAndDuplicates()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using var profile = new CombatSkillDef
        {
            skill_id = "excluded_creature_tag_schema_probe",
            excluded_target_creature_type_tags = new GStringNameArray
            {
                "",
                "undead",
                "undead",
            },
        };
        var errors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            errors,
            "excluded_creature_tag_schema_probe",
            profile
        );

        _test.True(
            ContainsError(
                errors,
                "combat_profile.excluded_target_creature_type_tags[0] must be non-empty."
            ),
            $"空的目标生物类型排除tag必须被schema拒绝。errors={string.Join(" | ", errors)}"
        );
        _test.True(
            ContainsError(
                errors,
                "combat_profile.excluded_target_creature_type_tags[2] duplicates undead."
            ),
            $"重复的目标生物类型排除tag必须被schema拒绝。errors={string.Join(" | ", errors)}"
        );
    }

    private void TestAuthoredContractAndLevelCurve(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "骨寒术正式资源与 combat_profile 应可加载。" );
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "骨寒术 skill_id 应稳定。" );
        _test.Eq(skill.DisplayName, "骨寒术", "骨寒术显示名应稳定。" );
        _test.Eq(skill.LearnSourceKind, SkillLearnSourceKind.Book, "骨寒术应继续通过书籍学习。" );
        _test.Eq(skill.GrowthTier, new StringName("advanced"), "骨寒术应保持 advanced 成长档。" );
        _test.True(skill.Description.Contains("亡灵与构装除外"), "完整描述应公开目标类型限制。" );
        _test.True(skill.Description.Contains("至多为正常值的50%"), "完整描述应公开减疗上限。" );
        _test.True(skill.Description.Contains("不会延长其他减疗效果"), "完整描述应公开独立续时规则。" );
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Unit, "骨寒术必须选择单位目标。" );
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Enemy, "骨寒术只能选择敌人。" );
        _test.True(combat.RequiresLos, "骨寒术必须要求视线。" );
        _test.Eq(combat.AttackDefenseMode, new StringName("touch"), "骨寒术必须攻击接触AC。" );
        _test.Eq(combat.ProjectileKind, new StringName("magical"), "骨寒术应使用魔法投射物。" );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "骨寒术不得要求武器。" );
        _test.True(Contains(combat.ExcludedTargetCreatureTypeTags, "undead"), "骨寒术应排除亡灵。" );
        _test.True(Contains(combat.ExcludedTargetCreatureTypeTags, "construct"), "骨寒术应排除构装。" );

        int[] expectedRange = { 4, 4, 4, 4, 5, 5, 5, 5 };
        int[] expectedAttack = { 0, 1, 1, 1, 1, 1, 1, 2 };
        int[] expectedMp = { 20, 20, 15, 15, 15, 15, 15, 15 };
        int[] expectedCooldown = { 180, 180, 180, 180, 180, 160, 160, 160 };
        int[] expectedDice = { 1, 1, 1, 2, 2, 2, 2, 3 };
        int[] expectedDuration = { 60, 60, 60, 60, 60, 60, 80, 80 };
        for (int level = 0; level <= 7; level += 1)
        {
            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            IReadOnlyList<CombatEffectDefinition> effects = ActiveEffectsAtLevel(
                combat.EffectDefinitions,
                level
            );
            CombatEffectDefinition damage = FindEffect(effects, BattleEffectKind.Damage);
            CombatEffectDefinition boneChill = FindStatusEffect(effects, "bone_chill");
            _test.Eq(costs.ApCost, 1, $"骨寒术L{level}应消耗1 AP。" );
            _test.Eq(costs.MpCost, expectedMp[level], $"骨寒术L{level}法力消耗不符。" );
            _test.Eq(costs.StaminaCost, 0, $"骨寒术L{level}不得消耗体力。" );
            _test.Eq(costs.CooldownTu, expectedCooldown[level], $"骨寒术L{level}冷却不符。" );
            _test.Eq(combat.GetEffectiveRangeValue(level), expectedRange[level], $"骨寒术L{level}射程不符。" );
            _test.Eq(combat.GetEffectiveAttackRollBonus(level), expectedAttack[level], $"骨寒术L{level}命中加值不符。" );
            _test.Eq(damage?.DiceCount ?? -1, expectedDice[level], $"骨寒术L{level}伤害骰数不符。" );
            _test.Eq(damage?.DiceSides ?? -1, 4, $"骨寒术L{level}必须使用D4。" );
            _test.Eq(damage?.DamageTag ?? "", new StringName("negative_energy"), $"骨寒术L{level}必须造成负能量伤害。" );
            _test.Eq(boneChill?.HealMultiplierPercent ?? -1, 50, $"骨寒术L{level}减疗倍率必须固定为50%。" );
            _test.Eq(boneChill?.DurationTu ?? -1, expectedDuration[level], $"骨寒术L{level}寒蚀持续时间不符。" );
        }

        string levelZero = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            0,
            new GDictionary()
        );
        string levelSeven = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            7,
            new GDictionary()
        );
        _test.True(levelZero.Contains("1D4"), "0级描述应显示1D4负能量伤害。" );
        _test.True(levelZero.Contains("至多为正常值的50%"), "等级描述应显示减疗上限。" );
        _test.True(levelSeven.Contains("3D4"), "7级描述应显示3D4负能量伤害。" );
        _test.True(levelSeven.Contains("持续80TU"), "7级描述应显示80TU寒蚀。" );
    }

    private void TestCreatureTypeRestrictionRejectsBeforeCost(SkillDefinition skill)
    {
        foreach (StringName excludedTag in new[] { new StringName("undead"), new StringName("construct") })
        {
            BattleUnitState caster = BuildCaster($"bone_type_{excludedTag}_caster", 3);
            BattleUnitState target = BuildUnit(
                $"bone_type_{excludedTag}_target",
                "enemy",
                new Vector2I(3, 2)
            );
            target.ReplaceCreatureTypeTagsTyped(
                new[] { excludedTag, new StringName("beast") }
            );
            using BattleTestFixture fixture = CreateFixture(skill, caster, target);
            BattleCommand command = BuildCommand(caster, target);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview != null && !preview.allowed, $"{excludedTag}目标应在预览阶段被拒绝。" );
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(caster.GetCurrentAp(), 2, $"拒绝{excludedTag}目标不得消耗AP。" );
            _test.Eq(caster.GetCurrentMp(), 100, $"拒绝{excludedTag}目标不得消耗MP。" );
            _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"拒绝{excludedTag}目标不得启动冷却。" );
            _test.Eq(target.GetCurrentHp(), 100, $"拒绝{excludedTag}目标不得造成伤害。" );
            batch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestPreviewAndExecutionKeepHealingReductionsIndependent(
        SkillDefinition skill
    )
    {
        BattleUnitState caster = BuildCaster("bone_hit_caster", 3);
        BattleUnitState target = BuildUnit("bone_hit_target", "enemy", new Vector2I(3, 2));
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "stronger_healing_suppression",
                display_label = "更强减疗",
                duration = 30,
                power = 1,
                stacks = 1,
                heal_multiplier_percent = 25,
            }
        );
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(preview?.allowed == true, "普通存活敌人应通过 canonical preview。" );
        _test.Eq(preview?.StatusContributionPreviewsTyped.Count ?? -1, 1, "预览只应展示本次寒蚀。" );
        string summary = preview?.StatusContributionPreviewsTyped.Count == 1
            ? preview.StatusContributionPreviewsTyped[0].SummaryText
            : "";
        _test.True(summary.Contains("施加寒蚀"), "预览应明确显示寒蚀。" );
        _test.True(summary.Contains("至多为正常值的50%"), "预览应明确显示50%治疗上限。" );
        _test.False(summary.Contains("更强减疗"), "预览不得展示目标已有的其他减疗。" );
        _test.False(summary.Contains("25%"), "预览不得泄露其他减疗倍率。" );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        BattleStatusEffectState boneChill = target.GetStatusEffect("bone_chill");
        BattleStatusEffectState stronger = target.GetStatusEffect(
            "stronger_healing_suppression"
        );
        _test.Eq(target.GetCurrentHp(), 92, "L3命中应按2D4最大骰造成8点负能量伤害。" );
        _test.Eq(boneChill?.duration ?? -1, 60, "L3命中应新增60TU寒蚀。" );
        _test.Eq(boneChill?.heal_multiplier_percent ?? -1, 50, "寒蚀应固定为50%治疗倍率。" );
        _test.Eq(stronger?.duration ?? -1, 30, "施加寒蚀不得延长其他减疗。" );
        _test.Eq(BattleStatusModifierRules.ResolveHealMultiplierPercent(target), 25, "并存时应由更强的25%减疗生效。" );
        _test.Eq(BattleStatusModifierRules.ApplyHealMultiplier(target, 8), 2, "更强减疗存在时8点治疗应结算为2点。" );
        target.EraseStatusEffect("stronger_healing_suppression");
        _test.Eq(BattleStatusModifierRules.ResolveHealMultiplierPercent(target), 50, "更强减疗移除后寒蚀应继续限制为50%。" );
        _test.Eq(BattleStatusModifierRules.ApplyHealMultiplier(target, 8), 4, "仅寒蚀存在时8点治疗应结算为4点。" );
        _test.Eq(caster.GetCurrentAp(), 1, "命中应消耗1 AP。" );
        _test.Eq(caster.GetCurrentMp(), 85, "L3命中应消耗15 MP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "L3命中应启动180TU冷却。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiScoresOnlyMarginalHealingDenied(SkillDefinition skill)
    {
        SkillDefinition healSkill = LoadSkill(HealSkillPath, "bone_chill_ai_heal_skill");
        BattleState state = new()
        {
            battle_id = "bone_chill_ai",
            phase = "unit_acting",
            map_size = new Vector2I(6, 4),
        };
        BattleGridService grid = new();
        BattleUnitState actor = BuildCaster("bone_ai_actor", 3, "hostile", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("bone_ai_target", "player", new Vector2I(2, 1));
        target.SetCurrentHp(80);
        target.AddKnownActiveSkill(HealSkillId);
        target.SetKnownSkillLevelTyped(HealSkillId, 5);
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
            new Dictionary<StringName, SkillDefinition>
            {
                [SkillId] = skill,
                [HealSkillId] = healSkill,
            }
        );
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup(new BattleDamageResolver());

        BattleAiScoreInput cleanScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.True(cleanScore?.estimated_enemy_healing_denied > 0, "有治疗能力且可恢复生命时，AI应获得边际减疗事实。" );
        _test.Eq(cleanScore?.estimated_status_count ?? -1, 0, "寒蚀不得领取普通状态分。" );
        _test.Eq(cleanScore?.estimated_control_count ?? -1, 0, "寒蚀不得伪装成控制。" );

        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "stronger_long_suppression",
                duration = 80,
                power = 1,
                stacks = 1,
                heal_multiplier_percent = 25,
            }
        );
        BattleAiScoreInput coveredScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.Eq(coveredScore?.estimated_enemy_healing_denied ?? -1, 0, "更强减疗覆盖整个寒蚀时长时，AI边际收益应为0。" );

        target.GetStatusEffect("stronger_long_suppression").duration = 30;
        BattleAiScoreInput partialScore = BuildAiScore(
            scoreService,
            context,
            skill,
            actor,
            target
        );
        _test.True(partialScore?.estimated_enemy_healing_denied > 0, "更强减疗先结束时，AI应只评价剩余时段。" );
        _test.True(
            partialScore?.estimated_enemy_healing_denied < cleanScore?.estimated_enemy_healing_denied,
            "被更强减疗覆盖一部分时长后的边际收益必须低于首次施加。"
        );
    }

    private void TestMissPaysCostsWithoutDamageOrBoneChill(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("bone_miss_caster", 3);
        BattleUnitState target = BuildUnit("bone_miss_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        BattleCommand command = BuildCommand(caster, target);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(target.GetCurrentHp(), 100, "骨寒术落空不得造成负能量伤害。" );
        _test.False(target.HasStatusEffect("bone_chill"), "骨寒术落空不得施加寒蚀。" );
        _test.Eq(caster.GetCurrentAp(), 1, "骨寒术落空仍应消耗1 AP。" );
        _test.Eq(caster.GetCurrentMp(), 85, "L3骨寒术落空仍应消耗15 MP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "L3骨寒术落空仍应启动180TU冷却。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiDoesNotEnumerateExcludedCreatureTypes(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("bone_ai_filter_caster", 3);
        BattleUnitState target = BuildUnit("bone_ai_filter_target", "enemy", new Vector2I(3, 2));
        target.AddCreatureTypeTagTyped("construct");
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        bool scoreCallbackReached = false;
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            preview_command_callback = fixture.Runtime.PreviewCommand,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            skill_score_input_callback = (_, _, _, preview, _, _, _) =>
            {
                scoreCallbackReached = true;
                return new BattleAiScoreInput { preview = preview, total_score = 100 };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        var action = new UseUnitSkillActionDefinition(
            "bone_chill_ai_filter",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            5,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );
        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            action,
            context
        );
        _test.True(decision == null, "AI不得为构装目标生成骨寒术决策。" );
        _test.False(scoreCallbackReached, "被类型规则排除的目标不得进入AI评分。" );
    }

    private static BattleAiScoreInput BuildAiScore(
        BattleAiScoreService scoreService,
        BattleAiContext context,
        SkillDefinition skill,
        BattleUnitState actor,
        BattleUnitState target
    )
    {
        var preview = new BattlePreview { allowed = true };
        preview.AddTargetUnitId(target.unit_id);
        preview.AddStatusContributionPreview(
            new BattleStatusContributionPreviewData(
                target.unit_id,
                target.display_name,
                "bone_chill",
                "skill",
                SkillId,
                false,
                target.GetStatusEffect("bone_chill") == null,
                0,
                1,
                0,
                1,
                1,
                60,
                0,
                "寒蚀",
                50
            )
        );
        BattleCommand command = BuildCommand(actor, target);
        return scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            ActiveEffectsAtLevel(skill.CombatProfile.EffectDefinitions, 3),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["desired_min_distance"] = 0,
                ["desired_max_distance"] = 4,
                ["position_target_unit_id"] = target.unit_id,
            }
        );
    }

    private static SkillDefinition LoadSkill(string path, string reason) =>
        TestSkillDefinitionProjection.LoadSkillDefinition(path, reason);

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "mage_bone_chill",
            new Vector2I(8, 5),
            new[] { caster },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static BattleUnitState BuildCaster(
        StringName id,
        int level,
        StringName faction = default,
        Vector2I coord = default
    )
    {
        BattleUnitState caster = BuildUnit(
            id,
            faction == default || faction == "" ? new StringName("player") : faction,
            coord == default ? new Vector2I(2, 2) : coord
        );
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level);
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        return caster;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(100);
        unit.SetCurrentMp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.SetCurrentMovePoints(3);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static IReadOnlyList<CombatEffectDefinition> ActiveEffectsAtLevel(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.IsUnlockedAtSkillLevel(level) == true)
                result.Add(effect);
        }
        return result;
    }

    private static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effects,
        BattleEffectKind kind
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == kind)
                return effect;
        }
        return null;
    }

    private static CombatEffectDefinition FindStatusEffect(
        IEnumerable<CombatEffectDefinition> effects,
        StringName statusId
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == BattleEffectKind.Status && effect.StatusId == statusId)
                return effect;
        }
        return null;
    }

    private static bool Contains(
        IEnumerable<StringName> values,
        StringName expected
    )
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static bool ContainsError(IEnumerable<string> errors, string expected)
    {
        foreach (string error in errors ?? Array.Empty<string>())
        {
            if (error.Contains(expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

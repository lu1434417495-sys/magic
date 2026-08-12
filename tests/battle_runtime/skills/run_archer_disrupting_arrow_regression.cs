using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_disrupting_arrow_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "res://data/configs/skills/archer_disrupting_arrow.tres";
    private const string BasicAttackPath =
        "res://data/configs/skills/basic_attack.tres";
    private const string InvalidReactionReferenceDirectory =
        "res://tests/battle_runtime/skills/fixtures/disrupting_arrow_invalid_reference";
    private static readonly StringName SkillId = "archer_disrupting_arrow";
    private static readonly StringName ReadyStatusId = "disrupting_arrow_ready";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill(SkillPath, "disrupting_arrow:authored");
            SkillDefinition basicAttack = LoadSkill(BasicAttackPath, "disrupting_arrow:basic");
            TestAuthoredContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestSchemaRejectsInvalidReactionProfiles();
            TestSchemaRejectsMissingReactionSkillReference();
            TestActivationConsumesCostsAndCreatesReadiness(skill, basicAttack);
            TestInstantSpellCommitsCostsBeforeInterruption(skill, basicAttack);
            TestCastingTimeSpellCommitsCostsBeforeInterruption(skill, basicAttack);
            TestAutomaticResolutionDoesNotTrigger(skill, basicAttack);
            TestAiOnlyValuesReadinessAgainstNearbySpellThreat(skill, basicAttack);
            TestNonSpellAndOutOfRangeDoNotTrigger(skill, basicAttack);
            TestMissAndShieldAbsorptionDoNotInterrupt(skill, basicAttack);
            TestSaveFailureInterruptsAndSuccessContinues(skill, basicAttack);
            TestReactionAttackBonusAffectsHitResolution(skill, basicAttack);
            TestTimelineOrderStopsAfterFirstInterrupt(skill, basicAttack);
            TestReadinessExpiresAtNextOwnerTurn(skill, basicAttack);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer disrupting arrow regression"));
    }

    private void TestAiOnlyValuesReadinessAgainstNearbySpellThreat(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        SkillDefinition spell = BuildSpellSkill();
        BattleUnitState archer = BuildArcher("ai_reaction_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("ai_reaction_caster", new Vector2I(2, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        using var scoreService = new BattleAiScoreService();
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [skill.SkillId] = skill,
            [basicAttack.SkillId] = basicAttack,
            [spell.SkillId] = spell,
        };
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = archer,
            grid_service = fixture.Runtime._grid_service,
        };
        context.SetSkillDefinitions(skillDefinitions);
        BattleCommand command = BuildSelfSkillCommand(archer.unit_id);
        try
        {
            _test.Eq(BattleRangeService.GetWeaponAttackRange(archer), 4, "AI评分前置：弓射程应为4。");
            _test.True(caster.KnowsActiveSkill(spell.SkillId), "AI评分前置：敌人应掌握主动法术。");
            _test.True(
                context.GetSkillDefinitionTyped(spell.SkillId)?.CombatProfile?.DeliveryCategories.Count == 1
                && context.GetSkillDefinitionTyped(spell.SkillId).CombatProfile.DeliveryCategories[0] == "spell",
                "AI评分前置：上下文应能解析敌方法术类别。"
            );
            BattleAiScoreInput withSpellThreat = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(archer),
                skill.CombatProfile.EffectDefinitions,
                BuildAiPositionMetadata()
            );
            _test.True(withSpellThreat != null, "扰咒箭AI评分应可构造正式 score input。");
            _test.Eq(
                withSpellThreat?.estimated_status_count ?? -1,
                1,
                "射程内存在主动法术威胁时，AI应把扰咒待机计为有效增益。"
            );

            caster.SetKnownActiveSkillIds(new[] { basicAttack.SkillId });
            BattleAiScoreInput withoutSpellThreat = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(archer),
                skill.CombatProfile.EffectDefinitions,
                BuildAiPositionMetadata()
            );
            _test.Eq(
                withoutSpellThreat?.estimated_status_count ?? -1,
                0,
                "附近敌人没有主动法术时，AI不得把扰咒待机当成泛用增益。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestInstantSpellCommitsCostsBeforeInterruption(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        SkillDefinition spell = BuildSpellSkill();
        BattleUnitState archer = BuildArcher("instant_hook_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("instant_hook_caster", new Vector2I(1, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        fixture.State.active_unit_id = caster.unit_id;
        Ready(archer, 1);
        int archerHpBefore = archer.GetCurrentHp();
        BattleCommand command = BuildSpellCommand(caster.unit_id, archer.unit_id, spell.SkillId);
        TrueRandomSeedService.ConfigureDeterministicForTests(29);
        try
        {
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(caster.GetCurrentAp(), 1, "即时法术被中断前必须已经支付1AP。");
            _test.Eq(caster.GetCurrentMp(), 90, "即时法术被中断前必须已经支付10MP。");
            _test.Eq(caster.GetCooldownTyped(spell.SkillId), 40, "即时法术中断后冷却不得返还。");
            _test.Eq(archer.GetCurrentHp(), archerHpBefore, "维持失败后法术效果不得落到目标。");
            _test.False(archer.HasStatusEffect(ReadyStatusId), "即时施法应消耗已触发的扰咒待机。");
            _test.True(LogsContain(batch.log_lines, "法术中断"), "战斗日志应明确记录法术被扰咒中断。");
        }
        finally
        {
            TrueRandomSeedService.ClearDeterministicForTests();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestCastingTimeSpellCommitsCostsBeforeInterruption(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        SkillDefinition slowSpell = BuildSpellSkill("test_slow_active_spell", castingTimeTu: 10);
        BattleUnitState archer = BuildArcher("slow_hook_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("slow_hook_caster", new Vector2I(1, 0));
        caster.AddKnownActiveSkill(slowSpell.SkillId);
        caster.SetKnownSkillLevelTyped(slowSpell.SkillId, 1);
        using BattleTestFixture fixture = CreateFixture(
            skill,
            basicAttack,
            archer,
            caster,
            extraSkills: new[] { slowSpell }
        );
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        fixture.State.active_unit_id = caster.unit_id;
        Ready(archer, 1);
        BattleCommand command = BuildSpellCommand(caster.unit_id, archer.unit_id, slowSpell.SkillId);
        TrueRandomSeedService.ConfigureDeterministicForTests(29);
        try
        {
            fixture.Runtime.IssueCommand(command);
            _test.Eq(caster.GetCurrentAp(), 0, "读条法术被扰咒时应清空本次行动AP。");
            _test.Eq(caster.GetCurrentMp(), 90, "读条法术被中断前必须提交持久资源消耗。");
            _test.Eq(caster.GetCooldownTyped(slowSpell.SkillId), 40, "读条启动阶段被中断也必须启动冷却。");
            _test.False(caster.HasPendingCast(), "启动阶段被扰咒中断后不得留下 pending cast。");
            _test.False(archer.HasStatusEffect(ReadyStatusId), "读条法术也应触发并消耗扰咒待机。");
        }
        finally
        {
            TrueRandomSeedService.ClearDeterministicForTests();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestAutomaticResolutionDoesNotTrigger(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        SkillDefinition spell = BuildSpellSkill();
        BattleUnitState archer = BuildArcher("auto_exclusion_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("auto_exclusion_caster", new Vector2I(1, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        Ready(archer, 1);
        int archerHpBefore = archer.GetCurrentHp();
        int casterHpBefore = caster.GetCurrentHp();
        using var batch = new BattleEventBatch();

        bool applied = fixture.Runtime._skill_orchestrator.ExecuteAutoCast(
            BuildAutoCastRequest(caster, spell, archer),
            batch
        );

        _test.True(applied, "自动施法请求必须完成正式技能结算，不能因请求无效而伪通过。");
        _test.True(
            batch.changed_unit_ids.Contains(archer.unit_id),
            "自动法术落地后必须报告目标变化。"
        );
        _test.True(archer.GetCurrentHp() < archerHpBefore, "自动法术效果必须实际落到目标。");
        _test.Eq(caster.GetCurrentHp(), casterHpBefore, "自动法术不得引发扰咒箭反应射击。");
        _test.True(
            archer.HasStatusEffect(ReadyStatusId),
            "自动法术不得触发或消耗扰咒待机。"
        );
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatSpellReactionDefinition reaction = combat?.SpellReaction;
        _test.True(combat != null, "扰咒箭正式资源与 combat_profile 应可加载。");
        _test.True(reaction != null, "扰咒箭必须投影 typed spell reaction profile。");
        if (combat == null || reaction == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "扰咒箭 skill_id 应稳定。");
        _test.Eq(skill.MaxLevel, 5, "扰咒箭核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "扰咒箭非核心上限应为3级。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "扰咒箭应属于基础成长档。");
        _test.Eq(ReadGrowth(skill, "agility"), 40, "扰咒箭应提供40点敏捷成长进度。");
        _test.Eq(ReadGrowth(skill, "perception"), 20, "扰咒箭应提供20点感知成长进度。");
        _test.True(skill.Description.Contains("标准武器攻击"), "描述必须明确反应射击是标准武器攻击。");
        _test.True(skill.Description.Contains("生命伤害"), "描述必须明确护盾吸收不会触发维持检定。");
        _test.True(skill.Description.Contains("消耗与冷却不返还"), "描述必须明确中断后的资源规则。");
        _test.True(skill.Description.Contains("应急预案"), "描述必须明确自动施法不触发。");
        _test.False(skill.Description.Contains("固定伤害"), "非魔法弓术不得声明固定伤害。");
        _test.False(skill.Description.Contains("攻击检定+0"), "描述不得显示无意义的攻击检定+0。");

        _test.Eq(combat.TargetTeamFilter, new StringName("self"), "扰咒箭应只对自己进入待机。");
        _test.Eq(combat.TargetSelectionMode, new StringName("self"), "扰咒箭不应要求选择敌人。");
        _test.Eq(combat.ApCost, 1, "扰咒箭基础 AP 消耗应为1。");
        _test.Eq(combat.StaminaCost, 30, "扰咒箭基础体力消耗应为30。");
        _test.Eq(combat.CooldownTu, 120, "扰咒箭基础冷却应为120TU。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "扰咒箭必须由 typed 武器家族门禁限制。");
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "扰咒箭只允许弓。");
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得绕过装备弓要求。");
        _test.Eq(reaction.TriggerDeliveryCategory, new StringName("spell"), "只响应 spell delivery category。");
        _test.Eq(reaction.ReactionSkillId, new StringName("basic_attack"), "反应射击应复用标准攻击定义。");
        _test.Eq(reaction.ReadinessStatusId, ReadyStatusId, "typed profile 必须精确绑定待机状态，不能按来源技能泛化触发。");
        _test.Eq(reaction.RequiredWeaponFamily, new StringName("bow"), "触发时必须再次验证当前弓。");
        _test.Eq(reaction.SaveAbility, new StringName("constitution"), "维持检定应使用体质。");
        _test.Eq(reaction.BaseSaveDc, 10, "维持检定基础 DC 应为10。");
        _test.Eq(reaction.HpDamageDivisor, 2, "生命伤害 DC 应按二分之一计算。");
        _test.True(reaction.RequireHpDamage, "只有生命伤害才能触发维持检定。");
        _test.True(reaction.ConsumeOnTrigger, "反应射击触发后必须消耗待机。");
        _test.True(reaction.ExpireOnOwnerTurnStart, "待机必须在下一次自身行动开始时到期。");

        int damageEffectCount = 0;
        int readyStatusCount = 0;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect.EffectKind == BattleEffectKind.Damage)
                damageEffectCount++;
            if (effect.StatusId == ReadyStatusId)
                readyStatusCount++;
        }
        _test.Eq(damageEffectCount, 0, "进入待机的主动技能本身不得立即造成伤害。");
        _test.Eq(readyStatusCount, 1, "主动技能应只施加一个正式待机状态。");
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        CombatSpellReactionDefinition reaction = skill?.CombatProfile?.SpellReaction;
        if (reaction == null)
        {
            _test.Fail("扰咒箭等级曲线需要有效 reaction profile。");
            return;
        }
        AssertLevel(skill, 1, 30, 120, 0, 0);
        AssertLevel(skill, 2, 28, 120, 0, 0);
        AssertLevel(skill, 3, 28, 120, 0, 1);
        AssertLevel(skill, 4, 28, 100, 0, 1);
        AssertLevel(skill, 5, 28, 100, 1, 2);
        _test.Eq(
            BattleSkillExecutionOrchestrator.ComputeSpellReactionSaveDc(reaction, 8, 1),
            10,
            "8点生命伤害仍应采用基础DC10。"
        );
        _test.Eq(
            BattleSkillExecutionOrchestrator.ComputeSpellReactionSaveDc(reaction, 30, 3),
            16,
            "3级30点生命伤害应为15+1=DC16。"
        );

        string levelOne = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            1,
            new GDictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.False(levelOne.Contains("攻击检定"), "1级描述不得显示攻击检定+0。");
        _test.True(levelFive.Contains("攻击检定+1"), "5级描述应显示反应射击命中升级。");
        _test.True(levelFive.Contains("+2"), "5级描述应显示维持检定DC+2。");
    }

    private void TestSchemaRejectsInvalidReactionProfiles()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        AssertValidReactionProfileAccepted(validator);

        var cases = new (
            string Id,
            Action<CombatSkillDef, CombatSpellReactionDef> Mutate,
            string ExpectedSuffix
        )[]
        {
            (
                "trigger_empty",
                (_, reaction) => reaction.trigger_delivery_category = "",
                "requires trigger_delivery_category."
            ),
            (
                "reaction_skill_empty",
                (_, reaction) => reaction.reaction_skill_id = "",
                "requires reaction_skill_id."
            ),
            (
                "readiness_empty",
                (_, reaction) => reaction.readiness_status_id = "",
                "requires readiness_status_id."
            ),
            (
                "readiness_mismatch",
                (_, reaction) => reaction.readiness_status_id = "missing_ready_status",
                "readiness_status_id must match a status effect in combat_profile.effect_defs."
            ),
            (
                "weapon_empty",
                (_, reaction) => reaction.required_weapon_family = "",
                "requires required_weapon_family."
            ),
            (
                "weapon_mismatch",
                (_, reaction) => reaction.required_weapon_family = "crossbow",
                "required_weapon_family must also be present in combat_profile.required_weapon_families."
            ),
            (
                "save_ability_invalid",
                (_, reaction) => reaction.save_ability = "luck",
                "uses unsupported save_ability luck."
            ),
            (
                "save_tag_empty",
                (_, reaction) => reaction.save_tag = "",
                "requires save_tag."
            ),
            (
                "base_save_dc_invalid",
                (_, reaction) => reaction.base_save_dc = 0,
                "base_save_dc and hp_damage_divisor must be > 0."
            ),
            (
                "hp_damage_divisor_invalid",
                (_, reaction) => reaction.hp_damage_divisor = 0,
                "base_save_dc and hp_damage_divisor must be > 0."
            ),
            (
                "attack_curve_null",
                (_, reaction) => reaction.attack_roll_bonus_by_skill_level = null,
                "attack roll curve must cover levels 0 through max_level."
            ),
            (
                "attack_curve_short",
                (_, reaction) => reaction.attack_roll_bonus_by_skill_level = new[] { 0, 0, 0, 0, 0 },
                "attack roll curve must cover levels 0 through max_level."
            ),
            (
                "save_curve_null",
                (_, reaction) => reaction.save_dc_bonus_by_skill_level = null,
                "save DC curve must cover levels 0 through max_level."
            ),
            (
                "save_curve_short",
                (_, reaction) => reaction.save_dc_bonus_by_skill_level = new[] { 0, 0, 0, 0, 0 },
                "save DC curve must cover levels 0 through max_level."
            ),
            (
                "attack_bonus_negative",
                (_, reaction) =>
                    reaction.attack_roll_bonus_by_skill_level = new[] { 0, 0, 0, 0, 0, -1 },
                "attack roll bonuses must be >= 0."
            ),
            (
                "save_bonus_negative",
                (_, reaction) =>
                    reaction.save_dc_bonus_by_skill_level = new[] { 0, 0, 0, 0, 0, -1 },
                "save DC bonuses must be >= 0."
            ),
        };
        foreach (
            (
                string caseId,
                Action<CombatSkillDef, CombatSpellReactionDef> mutate,
                string expectedSuffix
            ) in cases
        )
        {
            AssertReactionProfileRejected(
                validator,
                caseId,
                mutate,
                expectedSuffix
            );
        }
    }

    private void TestSchemaRejectsMissingReactionSkillReference()
    {
        using var loader = new TestContentResourceLoader();
        using var registry = new SkillContentRegistry(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(InvalidReactionReferenceDirectory);
        GStringArray errors = registry.Validate();
        _test.True(
            ErrorsContain(errors, "references missing reaction skill"),
            "不存在的反应技能引用必须在内容加载期被拒绝。"
        );
    }

    private void TestActivationConsumesCostsAndCreatesReadiness(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("activation_archer", "player", new Vector2I(1, 1), 3);
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer);
        fixture.State.active_unit_id = archer.unit_id;
        BattleCommand command = BuildSelfSkillCommand(archer.unit_id);
        try
        {
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            BattleStatusEffectState status = archer.GetStatusEffect(ReadyStatusId);
            _test.True(preview.allowed, "持弓时扰咒箭自我待机预览应允许。");
            _test.True(batch.changed_unit_ids.Contains(archer.unit_id), "进入待机应标记施术者变更。");
            _test.Eq(archer.GetCurrentAp(), 1, "扰咒箭应消耗1AP。");
            _test.Eq(archer.GetCurrentStamina(), 72, "3级扰咒箭应消耗28体力。");
            _test.Eq(archer.GetCooldownTyped(SkillId), 120, "3级扰咒箭应启动120TU冷却。");
            _test.True(status != null, "成功使用后应获得扰咒待机状态。");
            _test.Eq(status?.source_skill_id ?? new StringName(""), SkillId, "状态应保留来源技能ID。");
            _test.Eq(status?.source_skill_level ?? -1, 3, "状态应快照使用时的技能等级。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState swordUser = BuildUnit("activation_sword", "player", Vector2I.Zero);
        swordUser.AddKnownActiveSkill(SkillId);
        swordUser.SetKnownSkillLevelTyped(SkillId, 1);
        ApplyWeapon(swordUser, "sword", "melee", 1);
        using BattleTestFixture blockedFixture = CreateFixture(skill, basicAttack, swordUser);
        blockedFixture.State.active_unit_id = swordUser.unit_id;
        BattleCommand blockedCommand = BuildSelfSkillCommand(swordUser.unit_id);
        try
        {
            int apBefore = swordUser.GetCurrentAp();
            int staminaBefore = swordUser.GetCurrentStamina();
            int cooldownBefore = swordUser.GetCooldownTyped(SkillId);
            BattlePreview blockedPreview = blockedFixture.Runtime.PreviewCommand(blockedCommand);
            BattleSkillCastBlockReasonKind blockReason =
                blockedFixture.Runtime.GetSkillCastBlockReason(swordUser, skill);
            using BattleEventBatch blockedBatch = blockedFixture.Runtime.IssueCommand(
                blockedCommand
            );

            _test.False(blockedPreview.allowed, "非弓武器预览必须拒绝扰咒待机。");
            _test.Eq(
                blockReason,
                BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing,
                "拒绝原因必须是缺少要求的武器家族。"
            );
            _test.True(blockedBatch.LogLinesTyped.Count > 0, "正式命令拒绝必须返回可观察反馈。");
            _test.False(
                blockedBatch.changed_unit_ids.Contains(swordUser.unit_id),
                "正式拒绝不得报告单位发生变化。"
            );
            _test.Eq(swordUser.GetCurrentAp(), apBefore, "正式拒绝不得消耗AP。");
            _test.Eq(swordUser.GetCurrentStamina(), staminaBefore, "正式拒绝不得消耗体力。");
            _test.Eq(swordUser.GetCooldownTyped(SkillId), cooldownBefore, "正式拒绝不得启动冷却。");
            _test.False(swordUser.HasStatusEffect(ReadyStatusId), "正式拒绝不得创建扰咒待机状态。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(blockedCommand);
        }
    }

    private void TestNonSpellAndOutOfRangeDoNotTrigger(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("filter_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("filter_caster", new Vector2I(1, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        Ready(archer, 1);
        BattleSpellReactionOutcome nonSpell =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                basicAttack,
                new BattleEventBatch()
            );
        _test.False(nonSpell.Triggered, "普通攻击不得触发扰咒箭。");
        _test.True(archer.HasStatusEffect(ReadyStatusId), "未触发时待机必须保留。");

        fixture.Runtime._grid_service.MoveUnit(
            fixture.State,
            caster,
            new Vector2I(6, 0)
        );
        BattleSpellReactionOutcome outOfRange =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch()
            );
        _test.False(outOfRange.Triggered, "弓射程外的施法不得触发扰咒箭。");
        _test.True(archer.HasStatusEffect(ReadyStatusId), "射程外未触发时待机必须保留。");

        fixture.Runtime._grid_service.MoveUnit(
            fixture.State,
            caster,
            new Vector2I(1, 0)
        );
        ApplyWeapon(archer, "sword", "melee", 1);
        BattleSpellReactionOutcome wrongWeapon =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch()
            );
        _test.False(wrongWeapon.Triggered, "待机期间失去弓时不得发动反应射击。");
        _test.True(archer.HasStatusEffect(ReadyStatusId), "武器不合法而未触发时待机必须保留到正常到期。");
    }

    private void TestMissAndShieldAbsorptionDoNotInterrupt(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("miss_archer", "player", Vector2I.Zero, 1);
        BattleUnitState caster = BuildCaster("miss_caster", new Vector2I(1, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedMissOneDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        Ready(archer, 1);
        BattleSpellReactionOutcome miss =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(1)
            );
        _test.True(miss.Triggered, "范围内敌方法术应消耗待机并发动反应射击。");
        _test.False(miss.Interrupted, "未命中不得中断法术。");
        _test.Eq(miss.HpDamage, 0, "未命中时生命伤害应为0。");
        _test.False(archer.HasStatusEffect(ReadyStatusId), "反应射击即使命中失败也应消耗待机。");

        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitOneDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        caster.SetCurrentHp(100);
        caster.ReplaceShieldStateTyped(10, 10, 100, "test", caster.unit_id, "test_shield");
        Ready(archer, 1);
        BattleSpellReactionOutcome absorbed =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(1)
            );
        _test.True(absorbed.Triggered, "护盾吸收仍代表反应射击已触发。");
        _test.False(absorbed.Interrupted, "未造成生命伤害时不得进行失败维持并中断。");
        _test.Eq(absorbed.HpDamage, 0, "护盾完全吸收时生命伤害应为0。");
        _test.False(absorbed.SaveResult.HasSave, "零生命伤害时不应进行体质维持检定。");
        _test.Eq(caster.GetCurrentHp(), 100, "护盾完全吸收时施法者生命值不得下降。");
        _test.Eq(caster.GetShieldStateTyped().CurrentHp, 9, "1点反应伤害必须实际由护盾10降至9。");
        _test.False(archer.HasStatusEffect(ReadyStatusId), "护盾吸收也必须消耗第二次扰咒待机。");
    }

    private void TestSaveFailureInterruptsAndSuccessContinues(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("save_archer", "player", Vector2I.Zero, 3);
        BattleUnitState caster = BuildCaster("save_caster", new Vector2I(1, 0));
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        Ready(archer, 3);
        BattleSpellReactionOutcome failed =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.True(failed.Triggered, "命中并造成生命伤害时应触发维持检定。");
        _test.True(failed.Interrupted, "自然1维持检定必须中断法术。");
        _test.Eq(failed.HpDamage, 8, "最大伤害解析器下，当前双手弓的1D8必须造成8点生命伤害。");
        _test.Eq(caster.GetCurrentHp(), 92, "1D8最大伤害必须真实写入施法者生命值100→92。");
        _test.Eq(failed.SaveDc, 11, "3级且低于20点伤害时维持DC应为10+1。");
        _test.True(failed.SaveResult.HasSave, "生命伤害后必须走正式豁免解析器。");

        caster.SetCurrentHp(100);
        caster.SetCurrentShieldHpAndNormalizeTyped(0);
        Ready(archer, 3);
        BattleSpellReactionOutcome succeeded =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(20)
            );
        _test.True(succeeded.Triggered, "重新待机后应能再次响应下一次法术。");
        _test.False(succeeded.Interrupted, "自然20维持检定必须让法术继续。");
        _test.True(succeeded.SaveResult.Success, "成功结果应保留正式豁免明细。");
    }

    private void TestReactionAttackBonusAffectsHitResolution(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("accuracy_archer", "player", Vector2I.Zero, 1);
        archer.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        archer.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        BattleUnitState caster = BuildCaster("accuracy_caster", new Vector2I(1, 0));
        caster.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 11);
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer, caster);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitOneDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedThresholdRollHitResolver(10));

        Ready(archer, 1);
        BattleSpellReactionOutcome levelOne =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(20)
            );
        _test.True(levelOne.Triggered, "1级扰咒箭应实际发起反应攻击。");
        _test.Eq(levelOne.HpDamage, 0, "固定掷出10对AC11时，1级无命中加值必须未命中。");
        _test.Eq(caster.GetCurrentHp(), 100, "1级未命中不得改变施法者生命值。");
        _test.False(levelOne.SaveResult.HasSave, "未命中不得误走维持检定。");
        _test.False(archer.HasStatusEffect(ReadyStatusId), "1级反应攻击即使未命中也应消耗待机。");

        caster.SetCurrentHp(100);
        archer.SetKnownSkillLevelTyped(SkillId, 5);
        Ready(archer, 5);
        BattleSpellReactionOutcome levelFive =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(20)
            );
        _test.True(levelFive.Triggered, "5级扰咒箭应实际发起反应攻击。");
        _test.Eq(levelFive.HpDamage, 1, "固定掷出10对AC11时，5级+1命中必须把同一攻击变为命中。");
        _test.Eq(caster.GetCurrentHp(), 99, "5级阈值命中必须真实写入1点生命伤害。");
        _test.True(levelFive.SaveResult.HasSave, "5级命中造成生命伤害后必须进行维持检定。");
        _test.False(levelFive.Interrupted, "固定自然20维持成功后法术应继续。");
    }

    private void TestTimelineOrderStopsAfterFirstInterrupt(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState first = BuildArcher("order_first", "player", Vector2I.Zero, 1);
        BattleUnitState second = BuildArcher("order_second", "player", new Vector2I(0, 1), 1);
        BattleUnitState caster = BuildCaster("order_caster", new Vector2I(1, 0));
        first.SetActionProgressTyped(90);
        second.SetActionProgressTyped(40);
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, first, caster, second);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        Ready(first, 1);
        Ready(second, 1);

        BattleSpellReactionOutcome passedOutcome =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(20)
            );
        _test.False(passedOutcome.Interrupted, "前序维持成功时应继续检查后续待机弓手。");
        _test.Eq(passedOutcome.ReactorUnitId, second.unit_id, "两名弓手均响应后应返回最后一名结果。");
        _test.False(first.HasStatusEffect(ReadyStatusId), "首名弓手维持成功后也应消耗待机。");
        _test.False(second.HasStatusEffect(ReadyStatusId), "法术未中断时后续弓手也应依次响应。");

        caster.SetCurrentHp(100);
        Ready(first, 1);
        Ready(second, 1);
        BattleSpellReactionOutcome outcome =
            fixture.Runtime._skill_orchestrator.ResolveSpellReactionsAfterCost(
                caster,
                BuildSpellSkill(),
                new BattleEventBatch(),
                BattleSaveContext.WithSaveRollOverride(1)
            );
        _test.True(outcome.Interrupted, "首个时间线反应失败维持后应中断法术。");
        _test.Eq(outcome.ReactorUnitId, first.unit_id, "较高行动进度的弓手应先响应。");
        _test.False(first.HasStatusEffect(ReadyStatusId), "已触发的首名弓手应消耗待机。");
        _test.True(second.HasStatusEffect(ReadyStatusId), "法术已中断后，后续弓手待机必须保留。");
    }

    private void TestReadinessExpiresAtNextOwnerTurn(
        SkillDefinition skill,
        SkillDefinition basicAttack
    )
    {
        BattleUnitState archer = BuildArcher("expiry_archer", "player", Vector2I.Zero, 1);
        using BattleTestFixture fixture = CreateFixture(skill, basicAttack, archer);
        Ready(archer, 1);
        BattleStatusTickResult result =
            fixture.Runtime._skill_turn_resolver.ApplyTurnStartStatusesResult(
                archer,
                new BattleEventBatch()
            );
        _test.True(result.Changed, "下一次自身行动开始时应报告状态变化。");
        _test.False(archer.HasStatusEffect(ReadyStatusId), "未触发的扰咒待机不得跨过下一次自身行动。");
    }

    private static SkillDefinition LoadSkill(string path, string reason) =>
        TestSkillDefinitionProjection.LoadSkillDefinition(path, reason);

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        SkillDefinition basicAttack,
        BattleUnitState primaryAlly,
        BattleUnitState primaryEnemy = null,
        BattleUnitState additionalAlly = null,
        IReadOnlyList<SkillDefinition> extraSkills = null
    )
    {
        var allies = new List<BattleUnitState> { primaryAlly };
        if (additionalAlly != null)
            allies.Add(additionalAlly);
        var enemies = new List<BattleUnitState>();
        if (primaryEnemy != null)
            enemies.Add(primaryEnemy);
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_disrupting_arrow",
            new Vector2I(8, 4),
            allies,
            enemies
        );
        SkillDefinition spell = BuildSpellSkill();
        var skillIndex = new Dictionary<StringName, SkillDefinition>
        {
            [skill.SkillId] = skill,
            [basicAttack.SkillId] = basicAttack,
            [spell.SkillId] = spell,
        };
        foreach (SkillDefinition extraSkill in extraSkills ?? Array.Empty<SkillDefinition>())
        {
            if (extraSkill != null)
                skillIndex[extraSkill.SkillId] = extraSkill;
        }
        fixture.Runtime.setup(null, skillIndex);
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildArcher(
        StringName id,
        StringName faction,
        Vector2I coord,
        int skillLevel
    )
    {
        BattleUnitState unit = BuildUnit(id, faction, coord);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, skillLevel);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(unit, "bow", "ranged", 4);
        return unit;
    }

    private static BattleUnitState BuildCaster(StringName id, Vector2I coord)
    {
        BattleUnitState unit = BuildUnit(id, "enemy", coord);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.AddKnownActiveSkill("test_active_spell");
        unit.SetKnownSkillLevelTyped("test_active_spell", 1);
        return unit;
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
            source_member_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(
            hp: 100,
            mp: 100,
            stamina: 100,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int attackRange
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"disrupting_arrow_test_{family}",
                weapon_profile_type_id = $"disrupting_arrow_test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = attackRange,
                weapon_uses_two_hands = true,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static SkillDefinition BuildSpellSkill(
        StringName skillId = default,
        int castingTimeTu = 0
    )
    {
        skillId = ProgressionDataUtils.to_string_name(skillId);
        if (skillId == "")
            skillId = "test_active_spell";
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 12,
            damageTag: "force"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "Test Active Spell",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damage },
                deliveryCategories: new[] { new StringName("spell") },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 4,
                apCost: 1,
                mpCost: 10,
                cooldownTu: 40,
                castingTimeTu: castingTimeTu
            ),
            tags: new[] { new StringName("magic") }
        );
    }

    private static void Ready(BattleUnitState archer, int skillLevel)
    {
        archer.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ReadyStatusId,
                source_unit_id = archer.unit_id,
                source_skill_id = SkillId,
                source_skill_level = skillLevel,
                power = 1,
                stacks = 1,
            }
        );
    }

    private static BattleCommand BuildSelfSkillCommand(StringName unitId)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = unitId,
        };
        command.AddTargetUnitId(unitId);
        return command;
    }

    private static BattleCommand BuildSpellCommand(
        StringName casterId,
        StringName targetId,
        StringName skillId
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = casterId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_unit_id = targetId,
        };
        command.AddTargetUnitId(targetId);
        return command;
    }

    private static AutoCastRequest BuildAutoCastRequest(
        BattleUnitState caster,
        SkillDefinition storedSkill,
        BattleUnitState target
    )
    {
        StringName setupId = "disrupting_arrow_auto_exclusion";
        StringName instanceId = $"{caster.unit_id}:{setupId}";
        var releaseContext = new ContingencyReleaseContext
        {
            InstanceId = instanceId,
            SetupId = setupId,
            OwnerMemberId = caster.source_member_id,
            OwnerUnitId = caster.unit_id,
            CasterUnitId = caster.unit_id,
            TriggerType = "affected_by_spell",
        };
        return new AutoCastRequest
        {
            CasterUnitId = caster.unit_id,
            OwnerMemberId = caster.source_member_id,
            OwnerUnitId = caster.unit_id,
            SetupId = setupId,
            InstanceId = instanceId,
            SourceSkillId = "test_contingency_source",
            SourceSkillLevel = 1,
            SourceSkillGrantSourceType = UnitSkillGrantSourceType.Player,
            StoredSkillId = storedSkill.SkillId,
            CastLevel = 1,
            TargetResolution = ContingencyTargetResolutionResult.UnitTarget(
                target.unit_id,
                target.GetAnchorCoord()
            ),
            ReleaseContext = releaseContext,
            FrozenFacts = ContingencyFrozenTriggerFacts.Empty,
        };
    }

    private static BattlePreview BuildSelfPreview(BattleUnitState unit)
    {
        var preview = new BattlePreview { allowed = true };
        preview.target_unit_ids.Add(unit.unit_id);
        preview.target_coords.Add(unit.GetAnchorCoord());
        return preview;
    }

    private static IReadOnlyDictionary<string, object> BuildAiPositionMetadata() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = 0,
            ["desired_max_distance"] = 4,
        };

    private static bool LogsContain(IEnumerable<string> lines, string needle)
    {
        foreach (string line in lines ?? Array.Empty<string>())
        {
            if (line.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void AssertValidReactionProfileAccepted(SkillCombatProfileValidator validator)
    {
        const string skillId = "valid_spell_reaction_baseline";
        BuildValidReactionValidationFixture(
            skillId,
            out CombatSkillDef profile,
            out _,
            out SkillDef skillDef
        );
        var errors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(errors, skillId, profile, skillDef);
        _test.Eq(
            errors.Count,
            0,
            $"合法 spell reaction baseline 不得产生诊断。errors={string.Join(" | ", errors)}"
        );
    }

    private void AssertReactionProfileRejected(
        SkillCombatProfileValidator validator,
        string caseId,
        Action<CombatSkillDef, CombatSpellReactionDef> mutate,
        string expectedSuffix
    )
    {
        string skillId = $"invalid_spell_reaction_{caseId}";
        BuildValidReactionValidationFixture(
            skillId,
            out CombatSkillDef profile,
            out CombatSpellReactionDef reaction,
            out SkillDef skillDef
        );
        mutate(profile, reaction);
        var errors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(errors, skillId, profile, skillDef);
        string expected =
            $"Skill {skillId} combat_profile.spell_reaction_profile {expectedSuffix}";
        _test.Eq(
            errors.Count,
            1,
            $"{caseId} 必须只产生对应的单因子诊断。errors={string.Join(" | ", errors)}"
        );
        _test.Eq(
            errors.Count == 1 ? errors[0] : "",
            expected,
            $"{caseId} 必须命中精确 schema 分支。"
        );
    }

    private static void BuildValidReactionValidationFixture(
        string skillId,
        out CombatSkillDef profile,
        out CombatSpellReactionDef reaction,
        out SkillDef skillDef
    )
    {
        CombatEffectDef readyEffect = TestResourceOwnership.Own(
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = "validation_ready",
                display_name = "Validation Ready",
                power = 1,
                lifetime_policy = "battle",
            },
            $"disrupting_arrow:schema:{skillId}:ready"
        );
        reaction = TestResourceOwnership.Own(
            new CombatSpellReactionDef
            {
                trigger_delivery_category = "spell",
                reaction_skill_id = "basic_attack",
                readiness_status_id = "validation_ready",
                required_weapon_family = "bow",
                save_ability = "constitution",
                save_tag = "spell_maintenance",
                base_save_dc = 10,
                hp_damage_divisor = 2,
                attack_roll_bonus_by_skill_level = new[] { 0, 0, 0, 0, 0, 1 },
                save_dc_bonus_by_skill_level = new[] { 0, 0, 0, 1, 1, 2 },
            },
            $"disrupting_arrow:schema:{skillId}:reaction"
        );
        profile = TestResourceOwnership.Own(
            new CombatSkillDef
            {
                skill_id = skillId,
                target_mode = "unit",
                target_team_filter = "self",
                target_selection_mode = "self",
                selection_order_mode = "stable",
                range_value = 0,
                required_weapon_families = new Godot.Collections.Array<StringName> { "bow" },
                spell_reaction_profile = reaction,
            },
            $"disrupting_arrow:schema:{skillId}:profile"
        );
        profile.effect_defs.Add(readyEffect);
        skillDef = TestResourceOwnership.Own(
            new SkillDef
            {
                skill_id = skillId,
                max_level = 5,
                non_core_max_level = 3,
            },
            $"disrupting_arrow:schema:{skillId}:skill"
        );
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int attackBonus,
        int saveDcBonus
    )
    {
        SkillEffectiveCombatDefinition effective =
            SkillEffectiveCombatDefinition.BuildUncached(skill, level);
        CombatSpellReactionDefinition reaction = skill.CombatProfile.SpellReaction;
        _test.Eq(effective.ResourceCosts.StaminaCost, stamina, $"L{level}体力消耗应正确。");
        _test.Eq(effective.ResourceCosts.CooldownTu, cooldownTu, $"L{level}冷却应正确。");
        _test.Eq(reaction.GetAttackRollBonus(level), attackBonus, $"L{level}反应攻击加值应正确。");
        _test.Eq(reaction.GetSaveDcBonus(level), saveDcBonus, $"L{level}维持DC加值应正确。");
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill?.AttributeGrowthProgress != null
        && skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value)
            ? value
            : 0;

    private static bool ErrorsContain(IEnumerable<string> errors, string needle)
    {
        foreach (string error in errors)
        {
            if (error.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private sealed class FixedThresholdRollHitResolver : BattleHitResolver
    {
        private readonly int _fixedRoll;

        internal FixedThresholdRollHitResolver(int fixedRoll)
        {
            _fixedRoll = Math.Clamp(fixedRoll, 1, 20);
        }

        protected override int RollTrueRandomAttackRange(
            int minValue,
            int maxValue,
            BattleState battleState
        )
        {
            battleState?.NextAttackRollNonce();
            return Math.Clamp(
                _fixedRoll,
                Math.Min(minValue, maxValue),
                Math.Max(minValue, maxValue)
            );
        }
    }
}

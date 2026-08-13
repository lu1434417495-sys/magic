using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_mage_color_spray_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "mage_color_spray";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "res://data/configs/skills/mage_color_spray.tres",
                "mage_color_spray_regression"
            );
            TestAuthoredContract(skill);
            TestWeightedOutcomeSchemaAndImmutableProjection();
            TestSaveBranchesAndRandomSelection(skill);
            TestCanonicalDamagePreviewDoesNotRollRandomOutcome(skill);
            TestCanonicalSkillPreviewAndHudKeepDamageAndRandomControl(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage color spray regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "七色炫光正式资源应可加载。");
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "七色炫光应包含combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("unit"), "七色炫光必须选择单位而非地格或范围。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "七色炫光只能选择敌人。");
        _test.Eq(combat.TargetSelectionMode, new StringName("single_unit"), "七色炫光必须是单体选择。");
        _test.Eq(combat.MinTargetCount, 1, "七色炫光最少选择一个目标。");
        _test.Eq(combat.MaxTargetCount, 1, "七色炫光最多选择一个目标。");
        _test.True(combat.RequiresLos, "七色炫光必须要求视线。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "七色炫光不应限制武器家族。");
        _test.Eq(combat.RequiredWeaponTypeIds.Count, 0, "七色炫光不应限制武器类型。");

        int[] diceByLevel = { 4, 5, 5, 6, 6, 7, 7, 8 };
        int[] mpByLevel = { 80, 80, 75, 75, 75, 75, 70, 70 };
        int[] cooldownByLevel = { 180, 180, 180, 150, 150, 150, 120, 120 };
        int[] rangeByLevel = { 3, 3, 3, 3, 3, 3, 3, 4 };
        int[] durationByLevel = { 60, 60, 60, 80, 80, 80, 100, 100 };
        for (int level = 0; level <= 7; level++)
        {
            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 2, $"七色炫光{level}级应消耗2AP。");
            _test.Eq(costs.MpCost, mpByLevel[level], $"七色炫光{level}级法力消耗应正确。");
            _test.Eq(costs.CooldownTu, cooldownByLevel[level], $"七色炫光{level}级冷却曲线应正确。");
            _test.True(costs.CooldownTu >= 120, $"七色炫光{level}级冷却不得低于120TU。");
            _test.Eq(combat.GetEffectiveRangeValue(level), rangeByLevel[level], $"七色炫光{level}级射程应正确。");

            CombatEffectDefinition effect = ActiveDamageEffect(skill, level);
            _test.True(effect != null, $"七色炫光{level}级应恰有一个生效伤害段。");
            if (effect == null)
                continue;
            _test.Eq(effect.DiceCount, diceByLevel[level], $"七色炫光{level}级伤害骰数量应正确。");
            _test.Eq(effect.DiceSides, 6, $"七色炫光{level}级应使用D6。");
            _test.Eq(effect.DamageTag, new StringName("psychic"), "七色炫光应造成心灵伤害。");
            _test.Eq(effect.SaveDcMode, new StringName("caster_spell"), "七色炫光应使用施法者法术DC。");
            _test.Eq(effect.SaveDcSourceAbility, new StringName("intelligence"), "七色炫光法术DC应取智力。");
            _test.Eq(effect.SaveAbility, new StringName("willpower"), "七色炫光应进行意志豁免。");
            _test.Eq(effect.SaveTag, new StringName("illusion"), "七色炫光应进入幻术豁免链。");
            _test.True(effect.SavePartialOnSuccess, "七色炫光豁免成功应半伤。");
            _test.Eq(effect.SaveFailureStatusId, new StringName(""), "随机控制不得与静态失败状态并存。");
            AssertOutcomePool(effect, level, durationByLevel[level]);
        }

        _test.True(skill.Description.Contains("只会出现一种"), "玩家描述应明确随机控制互斥。");
        _test.True(skill.Description.Contains("power=1"), "玩家描述应解释失衡power=1。");
        _test.True(skill.Description.Contains("豁免成功"), "玩家描述应区分成功与失败分支。");
    }

    private void AssertOutcomePool(
        CombatEffectDefinition damage,
        int level,
        int expectedDurationTu
    )
    {
        IReadOnlyList<CombatWeightedStatusOutcomeDefinition> outcomes =
            damage.SaveFailureStatusOutcomes;
        _test.Eq(outcomes.Count, 3, $"七色炫光{level}级失败时应有三个随机控制候选。");
        _test.Eq(outcomes.Sum(outcome => outcome.Weight), 3, $"七色炫光{level}级三个候选应等权。");
        if (outcomes.Count != 3)
            return;

        CombatEffectDefinition dazzled = Outcome(outcomes, "dazzled")?.StatusEffect;
        CombatEffectDefinition staggered = Outcome(outcomes, "staggered")?.StatusEffect;
        CombatEffectDefinition shocked = Outcome(outcomes, "shocked")?.StatusEffect;
        _test.Eq(dazzled?.StatusId ?? "", new StringName("color_spray_dazzled"), "目眩应使用独立状态。");
        _test.Eq(dazzled?.AttackRollPenalty ?? 0, level >= 4 ? 3 : 2, "目眩攻击惩罚应在4级强化。");
        _test.True(dazzled?.ConsumeOnNextAttackCheck == true, "目眩必须在下一次实际攻击检定后消耗。");
        _test.Eq(staggered?.StatusId ?? "", new StringName("staggered"), "失衡应复用现有踉跄语义。");
        _test.Eq(staggered?.Power ?? 0, 1, "失衡power必须为1。");
        _test.Eq(shocked?.StatusId ?? "", new StringName("color_spray_shocked"), "感电应使用独立状态。");
        _test.True(shocked?.LockCounterattack == true, "感电应封锁反击。");
        _test.Eq(shocked?.LockCrit == true, level >= 4, "感电应从4级起封锁暴击。");
        foreach (CombatWeightedStatusOutcomeDefinition outcome in outcomes)
            _test.Eq(outcome.StatusEffect.DurationTu, expectedDurationTu, $"七色炫光{level}级随机控制持续时间应一致。");
    }

    private void TestWeightedOutcomeSchemaAndImmutableProjection()
    {
        using CombatEffectDef status = new()
        {
            effect_type = "status",
            status_id = "schema_probe",
            display_name = "Schema Probe",
            duration_tu = 60,
            stack_behavior = "refresh",
            stack_limit = 1,
        };
        using CombatWeightedStatusOutcomeDef outcome = new()
        {
            outcome_id = "probe",
            weight = 1,
            status_effect = status,
        };
        using CombatEffectDef damage = new()
        {
            effect_type = "damage",
            effect_categories = new GStringNameArray { "mental_attack", "psychic" },
            damage_tag = "psychic",
            dice_count = 1,
            dice_sides = 6,
            save_dc = 12,
            save_ability = "willpower",
            save_tag = "illusion",
            save_partial_on_success = true,
        };
        damage.save_failure_status_outcomes.Add(outcome);

        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        GStringArray validErrors = new();
        registry.AppendEffectValidationErrors(
            validErrors,
            "weighted_status_probe",
            damage,
            "test_effect"
        );
        _test.Eq(validErrors.Count, 0, $"合法加权失败状态池应通过内容校验：{string.Join(" | ", validErrors)}");

        CombatEffectDefinition projected = CombatEffectDefinition.FromResource(
            damage,
            "weighted_status_probe"
        );
        outcome.weight = 9;
        status.status_id = "mutated_after_projection";
        _test.Eq(projected.SaveFailureStatusOutcomes[0].Weight, 1, "不可变定义必须复制候选权重。");
        _test.Eq(projected.SaveFailureStatusOutcomes[0].StatusEffect.StatusId, new StringName("schema_probe"), "不可变定义必须深投影嵌套状态。");

        damage.save_failure_status_id = "legacy_static_status";
        GStringArray conflictErrors = new();
        registry.AppendEffectValidationErrors(
            conflictErrors,
            "weighted_status_conflict",
            damage,
            "test_effect"
        );
        _test.True(
            conflictErrors.Any(error => error.Contains("cannot combine save_failure_status_id")),
            "静态失败状态与随机池并存必须被拒绝。"
        );
    }

    private void TestSaveBranchesAndRandomSelection(SkillDefinition skill)
    {
        CombatEffectDefinition effect = ActiveDamageEffect(skill, 0);
        for (int roll = 1; roll <= 3; roll++)
        {
            using var resolver = new FixedColorSprayResolver(roll);
            BattleUnitState source = MakeUnit($"color_spray_source_{roll}", "player", 100);
            BattleUnitState target = MakeUnit($"color_spray_target_{roll}", "enemy", 100);
            AttackEffectResolutionResult result = resolver.ResolveEffects(
                source,
                target,
                new[] { effect },
                DamageResolutionContext.FromDictionary(
                    new Godot.Collections.Dictionary
                    {
                        ["save_roll_override"] = 1,
                        ["skill_id"] = SkillId,
                    }
                )
            );
            _test.Eq(result.Damage, 24, "自然1豁免失败应承受完整4D6最大伤害。");
            _test.Eq(result.StatusEffectIds.Count, 1, "豁免失败必须且只能施加一个随机控制。");
            _test.Eq(resolver.OutcomeRollCount, 1, "豁免失败应只消耗一次随机控制抽取。");
            StringName expectedStatus = roll switch
            {
                1 => "color_spray_dazzled",
                2 => "staggered",
                _ => "color_spray_shocked",
            };
            _test.True(target.HasStatusEffect(expectedStatus), $"权重骰{roll}应选择{expectedStatus}。");
            if (roll == 2)
            {
                BattleStatusEffectState staggered = target.GetStatusEffect("staggered");
                _test.Eq(staggered?.power ?? 0, 1, "正式失衡状态power应为1。");
                _test.Eq(BattleStatusSemanticTable.GetTurnStartApPenalty(staggered), 1, "power=1应解释为回合开始损失1AP。");
                _test.True(BattleState.IsStrongAttackDisadvantageStatusId("staggered"), "踉跄应令攻击处于劣势。");
                _test.True(BattleStatusSemanticTable.BlocksPendingCast("staggered"), "踉跄应阻断待结算施法。");
            }
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(target);
        }

        using var successResolver = new FixedColorSprayResolver(1);
        BattleUnitState successSource = MakeUnit("color_spray_success_source", "player", 100);
        BattleUnitState successTarget = MakeUnit("color_spray_success_target", "enemy", 100);
        AttackEffectResolutionResult success = successResolver.ResolveEffects(
            successSource,
            successTarget,
            new[] { effect },
            DamageResolutionContext.FromDictionary(
                new Godot.Collections.Dictionary
                {
                    ["save_roll_override"] = 20,
                    ["skill_id"] = SkillId,
                }
            )
        );
        _test.Eq(success.Damage, 12, "自然20豁免成功应承受一半4D6最大伤害。");
        _test.Eq(success.StatusEffectIds.Count, 0, "豁免成功不得施加任何随机控制。");
        _test.Eq(successResolver.OutcomeRollCount, 0, "豁免成功不得消耗随机控制抽取。");
        BattleTestFixture.DisposeBattleUnit(successSource);
        BattleTestFixture.DisposeBattleUnit(successTarget);
    }

    private void TestCanonicalDamagePreviewDoesNotRollRandomOutcome(SkillDefinition skill)
    {
        using var resolver = new FixedColorSprayResolver(1);
        BattleUnitState source = MakeUnit("color_spray_preview_source", "player", 100);
        BattleUnitState target = MakeUnit("color_spray_preview_target", "enemy", 100);
        CombatEffectDefinition effect = ActiveDamageEffect(skill, 4);
        BattleDamagePreviewResult preview = resolver.PreviewDamageEffectTyped(
            source,
            target,
            effect,
            DamageResolutionContext.ForSkill(SkillId)
        );
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> outcomes =
            preview.SaveEstimate.SaveFailureStatusOutcomes;
        _test.Eq(outcomes.Count, 3, "canonical伤害预览应公开三个随机控制候选。");
        _test.Eq(outcomes.Sum(outcome => outcome.ConditionalProbabilityBasisPoints), 10000, "候选条件概率之和必须为100%。");
        _test.Eq(outcomes.Sum(outcome => outcome.ApplicationProbabilityBasisPoints), preview.SaveEstimate.SaveFailureProbabilityBasisPoints, "各控制实际概率之和必须等于豁免失败概率。");
        _test.Eq(OutcomePreview(outcomes, "dazzled")?.AttackRollPenalty ?? 0, 3, "4级预览应显示强化后的目眩。");
        _test.True(OutcomePreview(outcomes, "shocked")?.LockCrit == true, "4级预览应显示感电封锁暴击。");
        _test.Eq(resolver.OutcomeRollCount, 0, "canonical预览不得提前抽取实际随机结果。");
        _test.Eq(target.GetSortedStatusEffectIdsTyped().Count, 0, "canonical预览不得污染正式目标状态。");
        BattleTestFixture.DisposeBattleUnit(source);
        BattleTestFixture.DisposeBattleUnit(target);
    }

    private void TestCanonicalSkillPreviewAndHudKeepDamageAndRandomControl(
        SkillDefinition skill
    )
    {
        BattleUnitState source = BattleTestFixture.BuildUnit(
            "color_spray_hud_source",
            "player",
            new Vector2I(1, 1),
            currentAp: 2,
            currentHp: 100
        );
        BattleUnitState target = BattleTestFixture.BuildUnit(
            "color_spray_hud_target",
            "enemy",
            new Vector2I(3, 1),
            currentAp: 2,
            currentHp: 100
        );
        source.AddKnownActiveSkill(SkillId);
        source.SetKnownSkillLevelTyped(SkillId, 4);
        source.UnlockCombatResource("mp");
        source.SetCurrentMp(100);
        source.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        source.attribute_snapshot.SetValue(AttributeService.INTELLIGENCE_MODIFIER, 3);
        source.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus),
            3
        );
        target.attribute_snapshot.SetValue("willpower", 10);
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "mage_color_spray_hud",
            new Vector2I(7, 5),
            new[] { source },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = source.unit_id;

        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"七色炫光正式技能预览应合法。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
        );
        _test.True(preview?.DamagePreviewTyped?.HasDamage == true, "技能预览应保留伤害摘要。");
        _test.Eq(
            preview?.SaveBranchPreviewTyped?.Kind ?? "",
            new StringName("weighted_status_on_save_failure"),
            "技能预览应公开加权失败控制分支。"
        );
        _test.True(
            preview?.SaveBranchPreviewTyped?.SummaryText.Contains("随机控制") == true,
            "技能预览摘要应向玩家说明失败时随机控制。"
        );

        using var adapter = new BattleHudAdapter();
        BattleHoverSnapshot hover = adapter.BuildHoverPreview(
            fixture.State,
            target.GetAnchorCoord(),
            SkillId,
            "",
            new Godot.Collections.Array<Vector2I> { target.GetAnchorCoord() },
            preview
        );
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            target.GetAnchorCoord(),
            SkillId,
            skill.DisplayName,
            "",
            new Godot.Collections.Array<Vector2I> { target.GetAnchorCoord() },
            1,
            new Godot.Collections.Array<StringName> { target.unit_id },
            "",
            "七色炫光测试",
            preview
        );
        _test.True(
            !string.IsNullOrWhiteSpace(hover.DamageText)
                && !string.IsNullOrWhiteSpace(hover.SaveBranchPreviewText),
            "普通豁免伤害技能的hover必须同时显示伤害和随机控制分支。"
        );
        _test.True(
            !string.IsNullOrWhiteSpace(snapshot.SelectedSkillDamagePreviewText)
                && !string.IsNullOrWhiteSpace(snapshot.SelectedSkillSaveBranchPreviewText),
            "HUD技能摘要必须同时显示伤害和随机控制分支。"
        );
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static CombatEffectDefinition ActiveDamageEffect(SkillDefinition skill, int level)
    {
        CombatEffectDefinition match = null;
        foreach (
            CombatEffectDefinition effect in
                skill?.CombatProfile?.EffectDefinitions
                    ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectKind != BattleEffectKind.Damage
                || level < Math.Max(effect.MinSkillLevel, 0)
                || (effect.MaxSkillLevel >= 0 && level > effect.MaxSkillLevel)
            )
            {
                continue;
            }
            if (match != null)
                return null;
            match = effect;
        }
        return match;
    }

    private static CombatWeightedStatusOutcomeDefinition Outcome(
        IReadOnlyList<CombatWeightedStatusOutcomeDefinition> outcomes,
        StringName outcomeId
    ) => outcomes?.FirstOrDefault(outcome => outcome?.OutcomeId == outcomeId);

    private static BattleWeightedStatusOutcomePreviewData OutcomePreview(
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> outcomes,
        StringName outcomeId
    ) => outcomes?.FirstOrDefault(outcome => outcome?.OutcomeId == outcomeId);

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId, int hp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: hp,
            mp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue("intelligence", 16);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus),
            3
        );
        return unit;
    }

    private sealed class FixedColorSprayResolver : BattleDamageResolver
    {
        private readonly int _outcomeRoll;

        internal FixedColorSprayResolver(int outcomeRoll)
        {
            _outcomeRoll = outcomeRoll;
        }

        internal int OutcomeRollCount { get; private set; }

        public override int _roll_damage_die(int diceSides) => Math.Max(diceSides, 1);

        public override int _roll_weighted_status_outcome(int totalWeight)
        {
            OutcomeRollCount++;
            return Math.Clamp(_outcomeRoll, 1, Math.Max(totalWeight, 1));
        }
    }
}

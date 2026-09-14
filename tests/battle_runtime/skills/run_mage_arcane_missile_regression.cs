using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_mage_arcane_missile_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "mage_arcane_missile";
    private static readonly StringName SkillId = "mage_arcane_missile";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContractAndLevelCurves(skill);
            TestOrderedSlotSchemaValidation();
            TestOrderedPreviewExecutionAndLinearCosts(skill);
            TestSelectedCostGateIsAtomic(skill);
            TestDeadTargetMakesLaterAssignedMissilesFizzle(skill);
            TestOrderedCastMasteryCollapsesToSingleGrant(skill);
            TestRangeLimit(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage arcane missile regression"));
    }

    private void TestAuthoredContractAndLevelCurves(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "奥术飞弹正式资源与combat_profile应可加载。" );
        if (combat == null)
            return;

        _test.Eq(combat.ApCost, 1, "奥术飞弹必须固定消耗1AP。" );
        _test.Eq(combat.CooldownTu, 0, "奥术飞弹必须无冷却。" );
        _test.True(combat.RequiresLos, "奥术飞弹必须要求视线。" );
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.ForceHitNoCrit,
            "每枚飞弹必须必中且不能暴击。"
        );
        _test.Eq(
            combat.UnitTargetResolutionModeKind,
            CombatUnitTargetResolutionMode.OrderedSlots,
            "重复目标必须按有序槽位逐发结算。"
        );
        _test.True(combat.AllowRepeatTarget, "必须允许重复指定同一目标。" );
        _test.Eq(
            combat.SelectionOrderModeKind,
            BattleTargetSelectionOrderMode.Manual,
            "玩家指定的飞弹顺序必须保留。"
        );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "奥术飞弹不得要求武器。" );

        int[] expectedRange = { 3, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5 };
        int[] expectedMissiles = { 3, 3, 4, 4, 5, 5, 5, 6, 6, 6, 6 };
        int[] expectedMpPerMissile = { 10, 9, 9, 8, 8, 7, 6, 6, 6, 5, 5 };
        int[] expectedStaminaPerMissile = { 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 7 };
        int[] expectedDiceCount = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2 };
        int[] expectedDiceSides = { 4, 4, 4, 6, 6, 8, 8, 8, 8, 8, 4 };
        int[] expectedDiceBonus = { 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2 };
        for (int level = 0; level <= 10; level++)
        {
            SkillEffectiveCombatDefinition effective =
                SkillEffectiveCombatDefinition.BuildUncached(skill, level);
            _test.Eq(effective.ResourceCosts.ApCost, 1, $"{level}级AP消耗错误。" );
            _test.Eq(effective.ResourceCosts.CooldownTu, 0, $"{level}级冷却必须为0。" );
            _test.Eq(effective.RangeValue, expectedRange[level], $"{level}级射程错误。" );
            _test.Eq(
                effective.MaxTargetCount,
                expectedMissiles[level],
                $"{level}级飞弹上限错误。"
            );
            _test.Eq(
                effective.MpCostPerTargetSlot,
                expectedMpPerMissile[level],
                $"{level}级单发法力错误。"
            );
            _test.Eq(
                effective.StaminaCostPerTargetSlot,
                expectedStaminaPerMissile[level],
                $"{level}级单发体力错误。"
            );
            CombatSkillResourceCosts fullCosts =
                effective.GetResourceCostsForTargetSlots(expectedMissiles[level]);
            _test.Eq(
                fullCosts.MpCost,
                expectedMpPerMissile[level] * expectedMissiles[level],
                $"{level}级满额法力必须线性计算。"
            );
            _test.Eq(
                fullCosts.StaminaCost,
                expectedStaminaPerMissile[level] * expectedMissiles[level],
                $"{level}级满额体力必须线性计算。"
            );
            CombatEffectDefinition damage = ActiveDamage(skill, level);
            _test.Eq(damage?.DiceCount ?? 0, expectedDiceCount[level], $"{level}级伤害骰数错误。" );
            _test.Eq(damage?.DiceSides ?? 0, expectedDiceSides[level], $"{level}级伤害骰面错误。" );
            _test.Eq(damage?.DiceBonus ?? 0, expectedDiceBonus[level], $"{level}级伤害加值错误。" );
        }

        string levelTen = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            10,
            new GDictionary()
        );
        _test.True(levelTen.Contains("2D4+2"), "10级文本必须显示2D4+2。" );
        _test.True(levelTen.Contains("1至6枚"), "10级文本必须显示6发上限。" );
        _test.True(levelTen.Contains("每枚消耗5法力/7体力"), "10级文本必须显示单发费用。" );
        _test.True(levelTen.Contains("30法力/42体力"), "10级文本必须显示满额总费用。" );
        _test.True(levelTen.Contains("无冷却"), "玩家文本必须公开无冷却。" );
    }

    private void TestOrderedPreviewExecutionAndLinearCosts(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("ordered_caster", new Vector2I(1, 1), 10);
        BattleUnitState targetA = BuildUnit("ordered_target_a", new Vector2I(3, 1), 100);
        BattleUnitState targetB = BuildUnit("ordered_target_b", new Vector2I(4, 1), 100);
        using BattleTestFixture fixture = CreateFixture(skill, caster, targetA, targetB);
        var damageProbe = new OrderedDamageProbe();
        fixture.Runtime.ConfigureDamageResolverForTests(damageProbe);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(1));
        BattleCommand command = BuildCommand(caster, targetA, targetA, targetB);

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"三发预览必须合法。logs={JoinLogs(preview)}" );
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? 0, 3, "预览必须保留三个有序飞弹槽位。" );
        if (preview?.TargetUnitIdsTyped.Count == 3)
        {
            _test.Eq(preview.TargetUnitIdsTyped[0], targetA.unit_id, "第一发目标错误。" );
            _test.Eq(preview.TargetUnitIdsTyped[1], targetA.unit_id, "第二发重复目标不得聚合。" );
            _test.Eq(preview.TargetUnitIdsTyped[2], targetB.unit_id, "第三发目标错误。" );
        }
        _test.True(preview?.hit_preview?.ForceHitNoCrit == true, "预览必须公开必中且不可暴击。" );
        _test.True(JoinLogs(preview).Contains("合计 15 法力/21 体力"), "预览必须显示实际三发总费用。" );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        AssertSequence(
            damageProbe.TargetIds,
            new[] { targetA.unit_id, targetA.unit_id, targetB.unit_id },
            "正式执行必须按手动槽位顺序逐发结算。"
        );
        _test.True(damageProbe.AllForceHitNoCrit, "每一发都必须使用force_hit_no_crit。" );
        _test.Eq(caster.GetCurrentAp(), 1, "三发合计只消耗1AP。" );
        _test.Eq(caster.GetCurrentMp(), 85, "三发必须线性消耗15法力。" );
        _test.Eq(caster.GetCurrentStamina(), 79, "三发必须线性消耗21体力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, "施放后不得产生冷却。" );

        batch?.Dispose();
        Dispose(command, preview);
    }

    private void TestOrderedSlotSchemaValidation()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using var invalidOrdered = new CombatSkillDef
        {
            skill_id = "invalid_ordered_slots",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "multi_unit",
            selection_order_mode = "manual",
            unit_target_resolution_mode = "ordered_slots",
            allow_repeat_target = false,
            max_target_count = 3,
            mp_cost_per_target_slot = 2,
        };
        var invalidOrderedErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            invalidOrderedErrors,
            invalidOrdered.skill_id,
            invalidOrdered
        );
        _test.True(
            invalidOrderedErrors.Any(error => error.Contains("allow_repeat_target=true")),
            $"ordered_slots缺少重复目标能力时必须被拒绝。errors={string.Join(" | ", invalidOrderedErrors)}"
        );

        using var invalidAggregate = new CombatSkillDef
        {
            skill_id = "invalid_aggregate_slot_cost",
            unit_target_resolution_mode = "aggregate",
            mp_cost_per_target_slot = 2,
        };
        var invalidAggregateErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            invalidAggregateErrors,
            invalidAggregate.skill_id,
            invalidAggregate
        );
        _test.True(
            invalidAggregateErrors.Any(
                error => error.Contains("per-target-slot costs require")
            ),
            $"aggregate模式不得声明槽位费用。errors={string.Join(" | ", invalidAggregateErrors)}"
        );
    }

    private void TestSelectedCostGateIsAtomic(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster(
            "cost_gate_caster",
            new Vector2I(1, 1),
            10,
            mp: 14,
            stamina: 100
        );
        BattleUnitState target = BuildUnit("cost_gate_target", new Vector2I(3, 1), 100);
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        BattleCommand command = BuildCommand(caster, target, target, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, "14法力不得预览三发10级飞弹。" );
        _test.True(JoinLogs(preview).Contains("法力不足"), "拒绝原因必须公开法力不足。" );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), 2, "费用不足时不得扣AP。" );
        _test.Eq(caster.GetCurrentMp(), 14, "费用不足时不得扣部分法力。" );
        _test.Eq(caster.GetCurrentStamina(), 100, "费用不足时不得扣体力。" );
        batch?.Dispose();
        Dispose(command, preview);
    }

    private void TestDeadTargetMakesLaterAssignedMissilesFizzle(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("fizzle_caster", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("fizzle_target", new Vector2I(3, 1), 5);
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        var damageProbe = new OrderedDamageProbe();
        fixture.Runtime.ConfigureDamageResolverForTests(damageProbe);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(1));
        BattleCommand command = BuildCommand(caster, target, target, target);

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(damageProbe.TargetIds.Count, 1, "第一发击倒目标后，后两发必须消散。" );
        _test.Eq(target.GetCurrentHp(), 0, "第一发1D4+1最大伤害应击倒5HP目标。" );
        _test.Eq(caster.GetCurrentMp(), 70, "消散的既定飞弹仍支付完整30法力。" );
        _test.Eq(caster.GetCurrentStamina(), 67, "消散的既定飞弹仍支付完整33体力。" );
        _test.True(
            (batch?.LogLinesTyped.Count(line => line.Contains("既定目标已失效")) ?? 0) == 2,
            "日志必须逐发说明后续两发消散。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestOrderedCastMasteryCollapsesToSingleGrant(SkillDefinition skill)
    {
        using var masteryService = new BattleSkillMasteryService();
        BattleUnitState caster = BuildCaster("mastery_caster", new Vector2I(1, 1), 10);
        caster.source_member_id = "hero";
        BattleUnitState targetA = BuildUnit("mastery_target_a", new Vector2I(2, 1), 20);
        BattleUnitState targetB = BuildUnit("mastery_target_b", new Vector2I(3, 1), 20);
        var result = new GDictionary
        {
            ["attack_success"] = true,
            ["damage"] = 10,
            ["damage_dice_high_total_roll"] = true,
            ["damage_events"] = new Godot.Collections.Array
            {
                new GDictionary { ["skill_damage_dice_is_max"] = true },
            },
        };
        masteryService.RecordTargetResult(caster, targetA, skill, result);
        masteryService.RecordTargetResult(caster, targetB, skill, result);
        _test.Eq(
            masteryService.ResolveActiveSkillMasteryAmount(),
            2,
            "测试前置必须先产生两个合法逐目标熟练度事件。"
        );
        masteryService.CollapseTargetResultsToSingleGrant();
        _test.Eq(
            masteryService.ResolveActiveSkillMasteryAmount(),
            1,
            "一次有序飞弹施放最多只能保留一个最高合法逐目标熟练度奖励。"
        );
        result.Dispose();
    }

    private void TestRangeLimit(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("los_caster", new Vector2I(1, 1), 10);
        BattleUnitState rangeFive = BuildUnit("range_five_target", new Vector2I(6, 1), 100);
        using BattleTestFixture fixture = CreateFixture(skill, caster, rangeFive);
        BattleCommand command = BuildCommand(caster, rangeFive);
        BattlePreview clearPreview = fixture.Runtime.PreviewCommand(command);
        _test.True(clearPreview?.allowed == true, "10级必须允许射程5的清晰视线目标。" );
        BattleTestFixture.DisposeBattlePreview(clearPreview);
        BattleTestFixture.DisposeBattleCommand(command);

        BattleUnitState farCaster = BuildCaster("far_caster", new Vector2I(1, 1), 10);
        BattleUnitState rangeSix = BuildUnit("range_six_target", new Vector2I(7, 1), 100);
        using BattleTestFixture farFixture = CreateFixture(skill, farCaster, rangeSix);
        BattleCommand farCommand = BuildCommand(farCaster, rangeSix);
        BattlePreview farPreview = farFixture.Runtime.PreviewCommand(farCommand);
        _test.True(farPreview != null && !farPreview.allowed, "最大射程必须严格限制为5。" );
        Dispose(farCommand, farPreview);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "mage_arcane_missile_regression"
        );

    private static CombatEffectDefinition ActiveDamage(
        SkillDefinition skill,
        int skillLevel
    ) =>
        skill.CombatProfile.EffectDefinitions.SingleOrDefault(
            effect =>
                effect?.EffectKind == BattleEffectKind.Damage
                && effect.IsUnlockedAtSkillLevel(skillLevel)
        );

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        params BattleUnitState[] targets
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "mage_arcane_missile",
            new Vector2I(10, 3),
            new[] { caster },
            targets ?? Array.Empty<BattleUnitState>()
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
        Vector2I coord,
        int skillLevel,
        int mp = 100,
        int stamina = 100
    )
    {
        BattleUnitState caster = BuildUnit(id, coord, 100);
        caster.faction_id = "player";
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, skillLevel, preserveZero: true);
        caster.SetCurrentAp(2);
        caster.SetCurrentMp(mp);
        caster.SetCurrentStamina(stamina);
        caster.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        return caster;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        Vector2I coord,
        int hp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            "enemy",
            coord,
            currentAp: 2,
            currentHp: hp
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 99);
        unit.SetCurrentHp(hp);
        return unit;
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        params BattleUnitState[] targets
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
        };
        foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
        {
            command.AddTargetUnitId(target.unit_id);
            if (command.target_coord == new Vector2I(-1, -1))
            {
                command.target_coord = target.GetAnchorCoord();
            }
        }
        return command;
    }

    private void AssertSequence<T>(
        IReadOnlyList<T> actual,
        IReadOnlyList<T> expected,
        string message
    )
    {
        bool equal = actual != null
            && expected != null
            && actual.Count == expected.Count;
        if (equal)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(actual[index], expected[index]))
                    continue;
                equal = false;
                break;
            }
        }
        _test.True(
            equal,
            $"{message} actual=[{string.Join(",", actual ?? Array.Empty<T>())}] expected=[{string.Join(",", expected ?? Array.Empty<T>())}]"
        );
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static void Dispose(BattleCommand command, BattlePreview preview = null)
    {
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private sealed class OrderedDamageProbe : FixedHitMaxDamageResolver
    {
        internal List<StringName> TargetIds { get; } = new();
        internal bool AllForceHitNoCrit { get; private set; } = true;

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            TargetIds.Add(targetUnit?.unit_id ?? new StringName(""));
            AllForceHitNoCrit &= attackCheck.ForceHitNoCrit;
            return base.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                attackContext
            );
        }
    }
}

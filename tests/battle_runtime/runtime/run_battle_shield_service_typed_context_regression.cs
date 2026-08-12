using System.Collections.Generic;
using Godot;

public partial class run_battle_shield_service_typed_context_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedRollContextCachesShieldHp();
        TestTypedApplyPathUsesSharedContext();
        TestShieldReplacementPolicyPreservesAtomicOwnerState();
        RequestTestExit(_test.Finish("Battle shield service typed context regression"));
    }

    private void TestTypedRollContextCachesShieldHp()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("typed_context_source");
        CombatEffectDefinition effect = BuildAttributeScaledShieldEffect();
        var rollContext = new Dictionary<long, int>();

        int shieldHp = service.ResolveShieldHp(source, effect, rollContext);

        _test.Eq(shieldHp, 7, "typed context 首次 roll 应使用属性缩放骰和 dice_bonus。");
        _test.Eq(rollContext.Count, 1, "typed context 首次解析应写入唯一 shield roll cache。");
        long cacheKey = GetOnlyCacheKey(rollContext);
        _test.Eq(rollContext[cacheKey], 7, "typed context cache 值应等于已解析护盾。");

        rollContext[cacheKey] = 19;
        _test.Eq(
            service.ResolveShieldHp(source, effect, rollContext),
            19,
            "typed context 命中 cache 时不应重新 roll。"
        );
    }

    private void TestTypedApplyPathUsesSharedContext()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("typed_apply_source");
        BattleUnitState target = BuildUnit("typed_apply_target");
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill("typed_shield_skill");
        CombatEffectDefinition effectDefinition = BuildAttributeScaledShieldEffect();
        var rollContext = new Dictionary<long, int>();

        BattleShieldApplyResult result = service.ApplyUnitShieldEffectsResult(
            source,
            target,
            skillDefinition,
            new[] { effectDefinition },
            rollContext
        );

        _test.True(result.Applied, "typed apply path 应成功应用 shield。");
        BattleUnitShieldSnapshot shieldState = target.GetShieldStateTyped();
        _test.Eq(shieldState.CurrentHp, 7, "typed apply path 应写入解析后的 shield hp。");
        _test.Eq(shieldState.MaxHp, 7, "typed apply path 应原子写入 shield max hp。");
        _test.Eq(shieldState.Duration, 60, "typed apply path 应原子写入 duration。");
        _test.Eq(
            shieldState.Family,
            new StringName("typed_shield_skill"),
            "typed apply path 应写入 skill-derived family。"
        );
        _test.Eq(
            shieldState.SourceUnitId,
            source.unit_id,
            "typed apply path 应写入 source unit。"
        );
        _test.Eq(
            shieldState.SourceSkillId,
            skillDefinition.SkillId,
            "typed apply path 应写入 source skill。"
        );
        _test.Eq(result.CurrentShieldHp, 7, "typed apply result 应返回当前 shield hp。");
        _test.Eq(rollContext.Count, 1, "typed apply path 应把首次解析结果写入共享 context。");
        long cacheKey = GetOnlyCacheKey(rollContext);
        rollContext[cacheKey] = 13;

        BattleUnitState cachedTarget = BuildUnit("typed_apply_cached_target");
        BattleShieldApplyResult cachedResult = service.ApplyUnitShieldEffectsResult(
            source,
            cachedTarget,
            skillDefinition,
            new[] { effectDefinition },
            rollContext
        );
        _test.True(cachedResult.Applied, "共享 context 的缓存值仍应能应用护盾。");
        _test.Eq(
            cachedTarget.GetShieldStateTyped().CurrentHp,
            13,
            "第二次正式 apply 应复用共享 context 中已观察到的 cache，而非重新 roll。"
        );
    }

    private void TestShieldReplacementPolicyPreservesAtomicOwnerState()
    {
        var service = new BattleShieldService();
        CombatEffectDefinition effect = BuildAttributeScaledShieldEffect();

        BattleUnitState refreshSource = BuildUnit("refresh_source");
        Dictionary<long, int> rollContext = BuildObservedRollContext(
            service,
            refreshSource,
            effect,
            7
        );
        BattleUnitState refreshTarget = BuildUnit("refresh_target");
        refreshTarget.ReplaceShieldStateTyped(
            5,
            10,
            20,
            "same_family",
            "old_source",
            "old_skill"
        );
        BattleShieldApplyResult refreshResult = service.ApplyShieldEffectToTargetResult(
            refreshSource,
            refreshTarget,
            TestSkillDefinitionProjection.BuildSkill("same_family"),
            effect,
            rollContext
        );
        _test.True(refreshResult.Applied, "同 family 有数值提升时应应用刷新。");
        _test.Eq(
            refreshTarget.GetShieldStateTyped(),
            new BattleUnitShieldSnapshot(
                7,
                10,
                60,
                "same_family",
                refreshSource.unit_id,
                "same_family"
            ),
            "同 family 刷新应原子保留 max、提升 current/duration 并更新来源。"
        );

        BattleUnitState noOpSource = BuildUnit("no_op_source");
        BattleUnitState noOpTarget = BuildUnit("no_op_target");
        var noOpBaseline = new BattleUnitShieldSnapshot(
            9,
            12,
            100,
            "same_family",
            "old_source",
            "old_skill"
        );
        noOpTarget.ReplaceShieldStateTyped(
            noOpBaseline.CurrentHp,
            noOpBaseline.MaxHp,
            noOpBaseline.Duration,
            noOpBaseline.Family,
            noOpBaseline.SourceUnitId,
            noOpBaseline.SourceSkillId
        );
        BattleShieldApplyResult noOpResult = service.ApplyShieldEffectToTargetResult(
            noOpSource,
            noOpTarget,
            TestSkillDefinitionProjection.BuildSkill("same_family"),
            effect,
            rollContext
        );
        _test.False(noOpResult.Applied, "同 family 无任何数值提升时应保持 no-op。");
        _test.Eq(
            noOpTarget.GetShieldStateTyped(),
            noOpBaseline,
            "同 family no-op 不应只更新 source metadata。"
        );

        BattleUnitState rejectedTarget = BuildUnit("rejected_target");
        var rejectedBaseline = new BattleUnitShieldSnapshot(
            8,
            12,
            80,
            "old_family",
            "old_source",
            "old_skill"
        );
        rejectedTarget.ReplaceShieldStateTyped(
            rejectedBaseline.CurrentHp,
            rejectedBaseline.MaxHp,
            rejectedBaseline.Duration,
            rejectedBaseline.Family,
            rejectedBaseline.SourceUnitId,
            rejectedBaseline.SourceSkillId
        );
        BattleShieldApplyResult rejectedResult = service.ApplyShieldEffectToTargetResult(
            BuildUnit("rejected_source"),
            rejectedTarget,
            TestSkillDefinitionProjection.BuildSkill("new_family"),
            effect,
            rollContext
        );
        _test.False(rejectedResult.Applied, "异 family 较弱护盾不应替换当前护盾。");
        _test.Eq(
            rejectedTarget.GetShieldStateTyped(),
            rejectedBaseline,
            "异 family 拒绝替换时六字段应保持不变。"
        );

        BattleUnitState replacementSource = BuildUnit("replacement_source");
        BattleUnitState replacementTarget = BuildUnit("replacement_target");
        replacementTarget.ReplaceShieldStateTyped(
            7,
            12,
            50,
            "old_family",
            "old_source",
            "old_skill"
        );
        BattleShieldApplyResult replacementResult = service.ApplyShieldEffectToTargetResult(
            replacementSource,
            replacementTarget,
            TestSkillDefinitionProjection.BuildSkill("new_family"),
            effect,
            rollContext
        );
        _test.True(
            replacementResult.Applied,
            "异 family HP 相等但 duration 更长时应替换。"
        );
        _test.Eq(
            replacementTarget.GetShieldStateTyped(),
            new BattleUnitShieldSnapshot(
                7,
                7,
                60,
                "new_family",
                replacementSource.unit_id,
                "new_family"
            ),
            "异 family 替换应一次性写入完整新 owner 状态。"
        );
    }

    private static BattleUnitState BuildUnit(string unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = "player",
        }.WithCombatResourcesForTest(
            hp: 20,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 14);
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower), 12);
        return unit;
    }

    private static CombatEffectDefinition BuildAttributeScaledShieldEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "shield",
            diceCount: 2,
            diceSidesBase: 4,
            diceSidesPerConstitutionMod: 1,
            diceSidesPerWillpowerMod: 1,
            diceBonus: 5,
            durationTu: 60
        );

    private static Dictionary<long, int> BuildObservedRollContext(
        BattleShieldService service,
        BattleUnitState source,
        CombatEffectDefinition effect,
        int cachedHp
    )
    {
        var rollContext = new Dictionary<long, int>();
        service.ResolveShieldHp(source, effect, rollContext);
        rollContext[GetOnlyCacheKey(rollContext)] = cachedHp;
        return rollContext;
    }

    private static long GetOnlyCacheKey(IReadOnlyDictionary<long, int> rollContext)
    {
        foreach (long key in rollContext.Keys)
            return key;
        throw new System.InvalidOperationException("Expected one observed shield roll cache entry.");
    }

}

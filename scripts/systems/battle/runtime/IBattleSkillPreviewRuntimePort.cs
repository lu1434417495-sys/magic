using System.Collections.Generic;
using Godot;

/// <see cref="BattleSkillPreviewService"/> 需要的运行时能力面。
///
/// 与 <see cref="IBattleTimelineRuntimePort"/> / <see cref="IBattleChargeRuntimePort"/> 同一套模式。
///
/// 这里出现了三种"该开访问器"的情形，都记在下面各自的注释里；其余一律是行为。
/// 注意本服务**仍持有 <see cref="BattleSkillExecutionOrchestrator"/>（`_owner`）**：
/// orchestrator 属 application 层，隔离层依赖它并不违规，把这层关系也端口化是
/// orchestrator 自己那一轮的事，不在本次范围内。
internal interface IBattleSkillPreviewRuntimePort
{
    /// 访问器①：4 处调用全是把对象原样传给 `BattlePositionSwapRules` / `BattleAirbornePullRules`
    /// / `BattleWindPushRules` / 目标收集，包成行为无从下手。
    BattleGridService GetGridService();

    /// 访问器①同上（3 处原样外传）+ 一处直接开护壁预览会话。
    BattleLayeredBarrierService GetLayeredBarrierService();

    /// 访问器②：转发数 ≥5——本服务用到它 7 个不同方法。
    BattleSkillResolutionRules GetSkillResolutionRules();

    /// 访问器③：同层 peer（`BattleChargeResolver` 本身已在 battle_runtime_isolated），
    /// 暴露它完全不泄露 hub 拓扑。
    BattleChargeResolver GetChargeResolver();

    SkillDefinition GetSkillDefinition(StringName skillId);

    IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefIndex();

    BattleSpecialProfileGateResult PreviewSpecialProfileSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitReadView activeUnit
    );

    /// 返回 false 表示禁咒结算器尚未接入（原先是 `_meteor_swarm_resolver == null` 分支）。
    bool PopulateMeteorSwarmPreview(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        BattlePreview preview
    );

    BattleSourceRetreatPlan BuildSourceRetreatPlan(
        BattleUnitReadView sourceUnit,
        Vector2I targetCoord,
        Vector2I direction,
        int distance
    );

    BattleApproachAttackPlan BuildApproachAttackPlan(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition
    );

    BattleTargetCollectionResult CollectCombatProfileTargetCoords(
        BattleState state,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitReadView sourceUnit,
        IEnumerable<BattleUnitReadView> targetUnits,
        int skillLevel
    );

    BattleGroundSkillValidationResult ValidateGroundSkillCommandResult(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    );

    IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    );

    IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    );

    IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    );

    IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    );

    void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    );
}

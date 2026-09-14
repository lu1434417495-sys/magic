using System.Collections.Generic;
using Godot;

/// <see cref="BattleChargeResolver"/> 需要的运行时能力面。
///
/// 与 <see cref="IBattleTimelineRuntimePort"/> 同一套模式：消费者只声明**行为**，
/// 不再引用 <see cref="BattleRuntimeModule"/>。实现见 <see cref="BattleChargeBridgeService"/>。
///
/// 分界原则：hub 自己拥有的状态与 <c>application</c> 层服务（护壁、装备能力、技能执行编排）
/// 一律包成行为，消费者不知道哪个服务负责哪件事；而 <c>domain_runtime</c> 层的协作者
/// （格子、伤害、命中判定）本来就是隔离层允许的向下依赖，用访问器直接给出，
/// 不做十几个无信息量的转发方法。访问器每次经 hub 现取——
/// <c>_damage_resolver</c> 会被 <c>ConfigureDamageResolverForTests</c> 换掉，缓存会拿到旧对象。
internal interface IBattleChargeRuntimePort
{
    BattleLogicalAttackScope BeginLogicalAttack(BattleAttackDeliveryKind deliveryKind);
    void AbortActiveReactionBoundary();

    BattleState GetBattleState();

    BattleGridService GetGridService();

    BattleDamageResolver GetDamageResolver();

    BattleAttackCheckPolicyService GetAttackCheckPolicyService();

    BattleTerrainMovementContactResult ResolveMovementContact(
        BattleUnitState unitState,
        BattleEventBatch batch,
        HashSet<string> processedContactKeys,
        bool startingInsideCheck
    );

    void ApplyTerrainContactEffects(
        BattleUnitState unitState,
        BattleEventBatch batch,
        HashSet<string> processedContactKeys
    );

    BattleBarrierInteractionResult ResolveUnitBoundaryCrossing(
        BattleUnitState unitState,
        Vector2I fromAnchor,
        Vector2I toAnchor,
        BattleEventBatch batch
    );

    /// 返回被护壁裁剪后仍允许生效的格子；未装配护壁服务时原样返回 <paramref name="effectCoords"/>。
    IReadOnlyList<Vector2I> PreviewGroundEffectAllowedCoords(
        BattleUnitState sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        CombatCastVariantDefinition castVariantDefinition
    );

    IReadOnlyList<Vector2I> PreviewGroundEffectAllowedCoords(
        BattleUnitReadView sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        CombatCastVariantDefinition castVariantDefinition
    );

    /// 与 <see cref="PreviewGroundEffectAllowedCoords(BattleUnitState, Vector2I, SkillDefinition, IReadOnlyList{CombatEffectDefinition}, IReadOnlyList{Vector2I}, CombatCastVariantDefinition)"/>
    /// 的区别是它会提交护壁消耗并写入 <paramref name="batch"/>。
    IReadOnlyList<Vector2I> ResolveGroundEffectAllowedCoords(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch,
        CombatCastVariantDefinition castVariantDefinition
    );

    void ApplyMovementTrails(
        BattleUnitState sourceUnit,
        IReadOnlyList<Vector2I> executedPath,
        StringName skillId,
        BattleEventBatch batch
    );

    StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    );

    bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetFilter
    );

    IReadOnlyList<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> effectCoords);

    int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId);

    void ApplySourceBoundWeaponBonusMasteryGrants(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    );

    void MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    );

    void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    );

    void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    );

    void RecordSkillEffectResult(
        BattleUnitState sourceUnit,
        int damage,
        int healing,
        int killCount
    );

    void HandleUnitDefeatedByRuntimeEffect(
        BattleUnitState unitState,
        BattleUnitState sourceUnit,
        BattleEventBatch batch,
        string logLine,
        BattleDefeatHandlingOptions options
    );
}

using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

/// 地面效果服务族（<see cref="BattleGroundEffectService"/> 及其三个子服务
/// <see cref="BattleGroundEffectCoordService"/> / <see cref="BattleGroundRelocationService"/> /
/// <see cref="BattleGroundSkillValidationService"/>）共用的运行时能力面。
///
/// 四个文件共享同一个端口是有意的：它们本来就共享同一个 <c>Runtime</c>，根服务把 hub 往下传给
/// 三个子服务，只解耦根服务等于没解耦。它们是同一个可隔离单元。
///
/// 访问器/行为的分界线见 <see cref="IBattleSkillPreviewRuntimePort"/>。本端口的访问器都属
/// "对象本身参与控制流"或"转发数 ≥5"或"同层 peer"三种情形之一，各自在下面注明。
internal interface IBattleGroundEffectRuntimePort
{
    // ---- 访问器 ----

    BattleState GetBattleState();

    /// 转发数 ≥5：四个文件合计用到它十几个方法。
    BattleGridService GetGridService();

    /// 转发数 ≥5。
    BattleSkillResolutionRules GetSkillResolutionRules();

    /// 原样外传给 rules API（护壁裁剪、强制位移）。
    BattleLayeredBarrierService GetLayeredBarrierService();

    /// 原样外传给目标收集 API。
    BattleTargetCollectionService GetTargetCollectionService();

    /// 转发数 ≥5。
    BattleDamageResolver GetDamageResolver();

    BattleAttackCheckPolicyService GetAttackCheckPolicyService();

    /// 其"是否存在"直接决定分支（`magicBacklashResolver == null` 时整段跳过）。
    BattleMagicBacklashResolver GetMagicBacklashResolver();

    /// 同层 peer：<see cref="BattleChargeResolver"/> 已在 battle_runtime_isolated。
    BattleChargeResolver GetChargeResolver();

    BattleEffectOrigin CurrentEffectOriginForContingency { get; }

    // ---- 行为 ----

    void AppendResultReportEntry(BattleEventBatch batch, AttackEffectResolutionResult result);

    void MarkAppliedStatusesForTurnTiming(BattleUnitState targetUnit, GArray statusEffectIds);

    void MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        GStringNameArray statusEffectIds
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

    void RecordEffectMetrics(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        int killCount
    );

    void RecordUnitDefeated(BattleUnitState unitState);

    void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    );

    void ApplyOnKillGainResourcesEffects(
        BattleUnitState sourceUnit,
        BattleUnitState defeatedUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    );

    bool IsCrownBreakTargetEligible(BattleUnitState activeUnit, BattleUnitState targetUnit);

    bool IsCrownBreakSkill(StringName skillId);

    void RecordVajraBodyMasteryFromIncomingDamage(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    );

    BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        Dictionary<long, int> shieldRollContext
    );

    bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    );

    bool IsUnitValidForEffect(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        StringName targetTeamFilter
    );

    void FlushLastStandMasteryRecords(BattleEventBatch batch);

    void AppendChangedCoord(BattleEventBatch batch, Vector2I coord);

    void AppendChangedUnitId(BattleEventBatch batch, StringName unitId);

    void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unitState);

    void CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit,
        BattleEventBatch batch
    );

    void ClearDefeatedUnit(BattleUnitState unitState, BattleEventBatch batch);

    int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId);

    BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    );

    string GetSkillCastBlockMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    );

    string GetSkillCommandBlockReason(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    );

    int GetEffectiveSkillRange(BattleUnitState activeUnit, SkillDefinition skillDefinition);

    int GetEffectiveSkillRange(BattleUnitReadView activeUnit, SkillDefinition skillDefinition);

    bool IsMovementBlocked(BattleUnitState unitState);

    bool IsMovementBlocked(BattleUnitReadView unitView);

    StringName AllocateContingencySourceEventId(StringName prefix);

    void EmitContingencySpellAffected(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> affectedUnitIds,
        StringName sourceEventId,
        IReadOnlyList<Vector2I> affectedCoords
    );

    void EmitContingencyHpAndStatusHooks(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int previousTargetHp,
        IReadOnlyList<StringName> appliedStatusIds,
        StringName sourceEventId
    );

    BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forcedMoveContext
    );

    void HandleUnitDefeatedByRuntimeEffect(
        BattleUnitState unitState,
        BattleUnitState sourceUnit,
        BattleEventBatch batch,
        string logLine,
        BattleDefeatHandlingOptions options
    );

    void RecordSkillMasteryTargetResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        bool additionalEffectApplied
    );

    void RecordRatingContributionFromUnits(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        bool defeated,
        StringName sourceKind,
        StringName sourceId
    );

    void PrepareTimedTerrainFieldPlacement(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        StringName fieldInstanceId,
        BattleEventBatch batch
    );

    bool UpsertTimedTerrainEffectFromDefinition(
        Vector2I effectCoord,
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        StringName fieldInstanceId
    );

    IReadOnlyList<BattleTerrainTopologyChange> ReclassifyWaterTerrainNearCoords(
        BattleState state,
        IReadOnlyList<Vector2I> effectCoords
    );

    /// 递增并返回地格效果序号。原先是消费者直接读写 hub 上的 <c>_terrain_effect_nonce</c> 字段。
    int AllocateTerrainEffectNonce();

    IReadOnlyDictionary<
        CombatEffectDefinition,
        IReadOnlyList<BattleUnitState>
    > BuildUnitEffectTargetPlan(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<BattleUnitState> targetUnits
    );

    IReadOnlyDictionary<
        CombatEffectDefinition,
        IReadOnlyList<BattleUnitReadView>
    > BuildUnitEffectTargetPlan(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<BattleUnitReadView> targetUnits
    );

    IReadOnlyList<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> effectCoords);

    int ApplyForcedMoveEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch batch,
        BattleForcedMoveContext forcedMoveContext,
        BattleSaveContext saveContext
    );

    BattleSkillMasteryService GetSkillMasteryService();

    IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    );
}

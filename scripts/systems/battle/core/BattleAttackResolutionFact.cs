using Godot;

internal readonly record struct BattleAttackResolutionFact(
    BattleAttackActionId ActionId,
    long RootBoundaryId,
    StringName AttackerUnitId,
    StringName DefenderUnitId,
    Vector2I AttackerCellAtResolution,
    Vector2I DefenderCellAtResolution,
    BattleAttackDeliveryKind DeliveryKind,
    bool AttackSucceeded,
    bool CriticalHit,
    bool IncludesWeaponDamage,
    BattleEffectOrigin Origin
);

internal interface IBattleAttackResolutionSink
{
    void OnAttackResolved(
        in BattleAttackResolutionFact fact,
        BattleEventBatch batch
    );
}

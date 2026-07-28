using System;

internal readonly record struct BattleAttackActionId(long Value)
{
    internal bool IsValid => Value > 0;
}

internal sealed class BattleAttackActionContext
{
    internal BattleAttackActionContext(
        BattleAttackActionId actionId,
        long rootBoundaryId,
        BattleEffectOrigin origin,
        BattleAttackDeliveryKind deliveryKind
    )
    {
        if (!actionId.IsValid)
            throw new ArgumentException("attack action id is invalid");
        if (rootBoundaryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rootBoundaryId));
        if (deliveryKind == BattleAttackDeliveryKind.Unknown)
            throw new ArgumentException("attack delivery kind is required");
        ActionId = actionId;
        RootBoundaryId = rootBoundaryId;
        Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        DeliveryKind = deliveryKind;
    }

    internal BattleAttackActionId ActionId { get; }
    internal long RootBoundaryId { get; }
    internal BattleEffectOrigin Origin { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
}

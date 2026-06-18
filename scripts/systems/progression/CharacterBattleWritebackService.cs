using System;
using Godot;

internal sealed class CharacterBattleWritebackService
{
    private PartyState _partyState;
    private PartyWarehouseService _warehouseService;
    private Func<StringName, AttributeSnapshot> _attributeSnapshotProvider;

    internal void Setup(
        PartyState partyState,
        PartyWarehouseService warehouseService,
        Func<StringName, AttributeSnapshot> attributeSnapshotProvider
    )
    {
        _partyState = partyState;
        _warehouseService = warehouseService;
        _attributeSnapshotProvider = attributeSnapshotProvider;
    }

    internal void Clear()
    {
        _partyState = null;
        _warehouseService = null;
        _attributeSnapshotProvider = null;
    }

    internal void CommitResources(
        StringName memberId,
        int currentHp,
        int currentMp,
        int currentAura
    )
    {
        PartyMemberState memberState = _partyState?.GetMemberState(memberId);
        if (memberState == null)
            return;

        AttributeSnapshot snapshot = _attributeSnapshotProvider?.Invoke(memberId);
        if (snapshot == null)
            return;

        memberState.SetVitals(
            Mathf.Clamp(
                currentHp,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 1)
            ),
            Mathf.Clamp(
                currentMp,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)), 0)
            ),
            Mathf.Clamp(
                currentAura,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax)), 0)
            ),
            false
        );
    }

    internal void CommitDeath(StringName memberId)
    {
        PartyMemberState memberState = _partyState?.GetMemberState(memberId);
        if (memberState == null)
            return;
        SalvageMemberEquipment(memberState);
        memberState.MarkDead();
        _partyState?.RemoveMemberFromRosters(memberId);
    }

    internal void CommitKo(StringName memberId) => CommitDeath(memberId);

    internal int FlushAfterBattle() => (int)Error.Ok;

    private void SalvageMemberEquipment(PartyMemberState memberState)
    {
        EquipmentState equipmentState = memberState?.equipment_state;
        if (equipmentState == null || _warehouseService == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
        {
            if (
                equipmentState.PopEquippedInstance(entrySlotId)
                is EquipmentInstanceState equippedInstance
            )
                _warehouseService.DepositEquipmentInstance(equippedInstance);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Capture-time queries only. The window never retains a runtime or query provider.
internal interface IPartyManagementViewQuery
{
    int GetPromotionOfferCount(StringName memberId);
    AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(StringName memberId, EquipmentState equipmentView);
    BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(StringName memberId, EquipmentState equipmentView);
    GearSetEvaluationSnapshot EvaluateGearSets(StringName memberId, EquipmentState equipmentStateOverride = null);
    GDictionary GetIdentitySummaryForMember(StringName memberId);
}

internal sealed record PartyManagementMemberView(
    PartyMemberState DetachedMember,
    AttributeSnapshot Attributes,
    IReadOnlyList<BattleEffectiveTraitInstanceReadView> EffectiveTraits,
    GearSetEvaluationSnapshot GearSets,
    GDictionary IdentitySummary,
    int PromotionOfferCount
);

// Contains detached member facts and derived display values, never the party's save graph.
internal sealed record PartyManagementViewData(
    StringName LeaderMemberId,
    StringName MainCharacterMemberId,
    IReadOnlyList<StringName> ActiveMemberIds,
    IReadOnlyList<StringName> ReserveMemberIds,
    IReadOnlyDictionary<StringName, PartyManagementMemberView> Members
);

internal static class PartyManagementViewBuilder
{
    internal static PartyManagementViewData Capture(PartyState party, IPartyManagementViewQuery query = null)
    {
        if (party == null)
            return null;

        var members = new Dictionary<StringName, PartyManagementMemberView>();
        foreach (StringName key in party.member_states.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(key);
            PartyMemberState member = party.GetMemberState(memberId)?.DuplicateState();
            if (memberId == "" || member == null)
                continue;
            members[memberId] = new PartyManagementMemberView(
                member,
                query?.GetMemberAttributeSnapshotForEquipmentView(memberId, member.equipment_state),
                query?.BuildEffectiveTraitProjectionForEquipmentView(memberId, member.equipment_state)
                    .GetInstancesReadView() ?? Array.Empty<BattleEffectiveTraitInstanceReadView>(),
                query?.EvaluateGearSets(memberId, member.equipment_state),
                query?.GetIdentitySummaryForMember(memberId)?.Duplicate(true) ?? new GDictionary(),
                query?.GetPromotionOfferCount(memberId) ?? 0
            );
        }
        return new PartyManagementViewData(
            party.leader_member_id,
            party.GetResolvedMainCharacterMemberId(),
            CopyMemberIds(party.active_member_ids),
            CopyMemberIds(party.reserve_member_ids),
            new ReadOnlyDictionary<StringName, PartyManagementMemberView>(members)
        );
    }

    private static IReadOnlyList<StringName> CopyMemberIds(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            StringName memberId = ProgressionDataUtils.to_string_name(value);
            if (memberId != "")
                result.Add(memberId);
        }
        return result.AsReadOnly();
    }
}

using Godot;

public partial class CharacterManagementModule : IPartyManagementViewQuery
{
    int IPartyManagementViewQuery.GetPromotionOfferCount(StringName memberId) =>
        GetPromotionOffers(memberId).Count;
}

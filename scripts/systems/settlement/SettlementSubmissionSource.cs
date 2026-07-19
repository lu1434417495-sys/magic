using Godot;

internal enum SettlementSubmissionSource
{
    None = 0,
    Settlement,
    Shop,
    ContractBoard,
    BountyBoard,
    Forge,
    Stagecoach,
    NpcQuestOffer,
}

internal static class SettlementSubmissionSources
{
    internal static bool TryParse(string value, out SettlementSubmissionSource source)
    {
        source = SettlementSubmissionSource.None;
        switch ((value ?? "").StripEdges())
        {
            case "":
                return true;
            case "settlement":
                source = SettlementSubmissionSource.Settlement;
                return true;
            case "shop":
                source = SettlementSubmissionSource.Shop;
                return true;
            case "contract_board":
                source = SettlementSubmissionSource.ContractBoard;
                return true;
            case "bounty_board":
                source = SettlementSubmissionSource.BountyBoard;
                return true;
            case "forge":
                source = SettlementSubmissionSource.Forge;
                return true;
            case "stagecoach":
                source = SettlementSubmissionSource.Stagecoach;
                return true;
            case "npc_quest_offer":
                source = SettlementSubmissionSource.NpcQuestOffer;
                return true;
            default:
                return false;
        }
    }

    internal static SettlementSubmissionSource FromPanelKind(SettlementPanelKind panelKind) =>
        panelKind switch
        {
            SettlementPanelKind.Shop => SettlementSubmissionSource.Shop,
            SettlementPanelKind.ContractBoard => SettlementSubmissionSource.ContractBoard,
            SettlementPanelKind.BountyBoard => SettlementSubmissionSource.BountyBoard,
            SettlementPanelKind.Forge => SettlementSubmissionSource.Forge,
            SettlementPanelKind.Stagecoach => SettlementSubmissionSource.Stagecoach,
            _ => SettlementSubmissionSource.None,
        };

    internal static string ToPayloadValue(SettlementSubmissionSource source) =>
        source switch
        {
            SettlementSubmissionSource.Settlement => "settlement",
            SettlementSubmissionSource.Shop => "shop",
            SettlementSubmissionSource.ContractBoard => "contract_board",
            SettlementSubmissionSource.BountyBoard => "bounty_board",
            SettlementSubmissionSource.Forge => "forge",
            SettlementSubmissionSource.Stagecoach => "stagecoach",
            SettlementSubmissionSource.NpcQuestOffer => "npc_quest_offer",
            _ => "",
        };
}

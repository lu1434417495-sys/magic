using Godot;

internal enum SettlementSubmissionSource
{
    None = 0,
    Settlement,
    Shop,
    ContractBoard,
    Forge,
    Stagecoach,
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
            case "forge":
                source = SettlementSubmissionSource.Forge;
                return true;
            case "stagecoach":
                source = SettlementSubmissionSource.Stagecoach;
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
            SettlementSubmissionSource.Forge => "forge",
            SettlementSubmissionSource.Stagecoach => "stagecoach",
            _ => "",
        };
}

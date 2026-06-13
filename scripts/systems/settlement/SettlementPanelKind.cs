using Godot;

internal enum SettlementPanelKind
{
    None = 0,
    Shop,
    ContractBoard,
    Forge,
    Stagecoach,
}

internal static class SettlementPanelKinds
{
    internal static bool TryParse(string value, out SettlementPanelKind panelKind)
    {
        panelKind = SettlementPanelKind.None;
        switch ((value ?? "").StripEdges())
        {
            case "":
                return true;
            case "shop":
                panelKind = SettlementPanelKind.Shop;
                return true;
            case "contract_board":
                panelKind = SettlementPanelKind.ContractBoard;
                return true;
            case "forge":
                panelKind = SettlementPanelKind.Forge;
                return true;
            case "stagecoach":
                panelKind = SettlementPanelKind.Stagecoach;
                return true;
            default:
                return false;
        }
    }

    internal static string ToPayloadValue(SettlementPanelKind panelKind) =>
        panelKind switch
        {
            SettlementPanelKind.Shop => "shop",
            SettlementPanelKind.ContractBoard => "contract_board",
            SettlementPanelKind.Forge => "forge",
            SettlementPanelKind.Stagecoach => "stagecoach",
            _ => "",
        };
}

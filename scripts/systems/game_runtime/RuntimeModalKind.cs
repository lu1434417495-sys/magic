public enum RuntimeModalKind
{
    None = 0,
    Settlement,
    Shop,
    ContractBoard,
    BountyBoard,
    NpcQuestOffer,
    Forge,
    Stagecoach,
    CharacterInfo,
    Party,
    Warehouse,
    SubmapConfirm,
    BattleStartConfirm,
    Promotion,
    Reward,
    GameOver,
    BattleLoading,
    ResourceHarvestConfirm,
}

internal static class RuntimeModalKinds
{
    internal static string ToPayloadValue(RuntimeModalKind kind) =>
        kind switch
        {
            RuntimeModalKind.Settlement => "settlement",
            RuntimeModalKind.Shop => "shop",
            RuntimeModalKind.ContractBoard => "contract_board",
            RuntimeModalKind.BountyBoard => "bounty_board",
            RuntimeModalKind.NpcQuestOffer => "npc_quest_offer",
            RuntimeModalKind.Forge => "forge",
            RuntimeModalKind.Stagecoach => "stagecoach",
            RuntimeModalKind.CharacterInfo => "character_info",
            RuntimeModalKind.Party => "party",
            RuntimeModalKind.Warehouse => "warehouse",
            RuntimeModalKind.SubmapConfirm => "submap_confirm",
            RuntimeModalKind.BattleStartConfirm => "battle_start_confirm",
            RuntimeModalKind.Promotion => "promotion",
            RuntimeModalKind.Reward => "reward",
            RuntimeModalKind.GameOver => "game_over",
            RuntimeModalKind.BattleLoading => "battle_loading",
            RuntimeModalKind.ResourceHarvestConfirm => "resource_harvest_confirm",
            _ => "",
        };

    internal static bool IsSettlementServiceModal(RuntimeModalKind kind) =>
        kind == RuntimeModalKind.Settlement
        || kind == RuntimeModalKind.Shop
        || kind == RuntimeModalKind.ContractBoard
        || kind == RuntimeModalKind.BountyBoard
        || kind == RuntimeModalKind.Forge
        || kind == RuntimeModalKind.Stagecoach;
}

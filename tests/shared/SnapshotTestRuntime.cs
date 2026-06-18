using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class SnapshotTestRuntime : IGameRuntimeSnapshotSource
{
    public PartyState PartyState { get; set; }
    public RuntimeModalKind ActiveModalKind { get; set; } = RuntimeModalKind.None;
    public GDictionary ContractBoardWindowData { get; set; } = new();
    public GDictionary ForgeWindowData { get; set; } = new();
    public GDictionary ActiveShopContext { get; set; } = new();
    public GDictionary WarehouseWindowData { get; set; } = new();
    public GDictionary LastBattleLootSnapshot { get; set; } = new();
    public GDictionary GameOverContext { get; set; } = new();
    public BattleState BattleState { get; set; }
    public BattleRuntimeModule BattleRuntime { get; set; }
    public Vector2I BattleSelectedCoord { get; set; } = Vector2I.Zero;
    public StringName ActiveBattleEncounterId { get; set; } = "snapshot_test_anchor";
    public string ActiveBattleEncounterName { get; set; } = "快照测试遭遇";

    public bool IsBattleActive() => BattleState != null && !BattleState.IsEmpty();

    public string GetStatusText() => "";

    public string GetActiveModalId() => RuntimeModalKinds.ToPayloadValue(ActiveModalKind);

    public RuntimeModalKind GetActiveModalKind() => ActiveModalKind;

    public GDictionary GetLogSnapshot(int limit) => new();

    public GDictionary GetSelectedSettlement() => new();

    public GDictionary GetSelectedWorldNpc() => new();

    public EncounterAnchorData GetSelectedEncounterAnchor() => null;

    public GDictionary GetSelectedWorldEvent() => new();

    public GArray GetNearbyEncounterEntries(int limit) => new();

    public GArray GetNearbyWorldEventEntries(int limit) => new();

    public string GetActiveMapId() => "";

    public string GetActiveMapDisplayName() => "";

    public bool IsSubmapActive() => false;

    public int GetWorldStep() => 0;

    public Vector2I GetPlayerCoord() => Vector2I.Zero;

    public bool IsPlayerVisibleOnWorldMap() => false;

    public Vector2I GetSelectedCoord() => Vector2I.Zero;

    public GDictionary GetPendingSubmapPrompt() => new();

    public string GetSubmapReturnHintText() => "";

    public GDictionary GetGameOverContext() => GameOverContext.Duplicate(true);

    public PartyState GetPartyState() => PartyState;

    public StringName GetPartySelectedMemberId() => "";

    public int GetPendingRewardCount() => 0;

    public GDictionary GetMemberAchievementSummary(StringName member_id) => new();

    public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id) => null;

    public GArray GetMemberEquippedEntries(StringName member_id) => new();

    public string GetMemberDisplayName(StringName member_id)
    {
        PartyMemberState memberState = PartyState?.GetMemberState(member_id);
        return memberState != null ? memberState.display_name : "";
    }

    public string GetResolvedSettlementId() => "";

    public GDictionary GetSettlementWindowData(string settlement_id) => new();

    public string GetSettlementFeedbackText() => "";

    public GDictionary GetShopWindowData() => new();

    public GDictionary GetContractBoardWindowData() =>
        ContractBoardWindowData.Duplicate(true);

    public GDictionary GetActiveContractBoardContext() =>
        ContractBoardWindowData.Duplicate(true);

    public GDictionary GetActiveShopContext() => ActiveShopContext.Duplicate(true);

    public GDictionary GetForgeWindowData() => ForgeWindowData.Duplicate(true);

    public GDictionary GetStagecoachWindowData() => new();

    public GDictionary GetCharacterInfoContext() => new();

    public string GetActiveWarehouseEntryLabel() => "";

    public GDictionary GetWarehouseWindowData() => WarehouseWindowData.Duplicate(true);

    public BattleState GetBattleState() => BattleState;

    public BattleRuntimeModule GetBattleRuntime() => BattleRuntime;

    public Vector2I GetBattleSelectedCoord() => BattleSelectedCoord;

    public StringName GetSelectedBattleSkillId() => "";

    public StringName GetSelectedBattleSkillVariantId() => "";

    public string GetSelectedBattleSkillName() => "";

    public string GetSelectedBattleSkillVariantName() => "";

    public GVector2IArray GetSelectedBattleSkillTargetCoords() => new();

    public GStringNameArray GetSelectedBattleSkillTargetUnitIds() => new();

    public int GetSelectedBattleSkillRequiredCoordCount() => 0;

    public BattlePreview GetSelectedBattleSkillPreview() => null;

    public StringName GetActiveBattleEncounterId() => ActiveBattleEncounterId;

    public string GetActiveBattleEncounterName() => ActiveBattleEncounterName;

    public string GetBattleActiveUnitName()
    {
        if (BattleState == null || !BattleState.ContainsUnit(BattleState.active_unit_id))
            return "";
        BattleUnitState activeUnit =
            BattleState.GetUnit(BattleState.active_unit_id);
        return activeUnit != null ? activeUnit.display_name : "";
    }

    public GDictionary GetPendingBattleStartPrompt() => new();

    public GDictionary GetBattleTerrainCounts() => new();

    public PendingCharacterReward GetSnapshotReward() => null;

    public GDictionary GetLastBattleLootSnapshot() =>
        LastBattleLootSnapshot.Duplicate(true);

    public GDictionary GetCurrentPromotionPrompt() => new();

    public GameSession GetGameSession() => null;
}

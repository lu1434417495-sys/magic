using System.Collections.Generic;
using Godot;

public sealed class SnapshotTestRuntime : IGameRuntimeSnapshotSource
{
    public PartyState PartyState { get; set; }
    public RuntimeModalKind ActiveModalKind { get; set; } = RuntimeModalKind.None;
    public Dictionary<string, object> ContractBoardWindowData { get; set; } = new();
    public Dictionary<string, object> NpcQuestOfferWindowData { get; set; } = new();
    public Dictionary<string, object> ForgeWindowData { get; set; } = new();
    public Dictionary<string, object> ActiveShopContext { get; set; } = new();
    public Dictionary<string, object> WarehouseWindowData { get; set; } = new();
    public Dictionary<string, object> LastBattleLootSnapshot { get; set; } = new();
    public Dictionary<string, object> GameOverContext { get; set; } = new();
    public BattleState BattleState { get; set; }
    public BattleRuntimeModule BattleRuntime { get; set; }
    public Vector2I BattleSelectedCoord { get; set; } = Vector2I.Zero;
    public StringName ActiveBattleEncounterId { get; set; } = "snapshot_test_anchor";
    public string ActiveBattleEncounterName { get; set; } = "快照测试遭遇";

    public bool IsBattleActive() => BattleState != null && !BattleState.IsEmpty();

    public string GetStatusText() => "";

    public string GetActiveModalId() => RuntimeModalKinds.ToPayloadValue(ActiveModalKind);

    public RuntimeModalKind GetActiveModalKind() => ActiveModalKind;

    public System.Collections.Generic.IReadOnlyDictionary<string, object> GetLogSnapshotPlain(
        int limit
    ) => new System.Collections.Generic.Dictionary<string, object>();

    public WorldMapSettlementData GetSelectedSettlementData() => null;

    public WorldMapNpcData GetSelectedWorldNpcData() => null;

    public EncounterAnchorData GetSelectedEncounterAnchor() => null;

    public WorldMapEventData GetSelectedWorldEventData() => null;

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyEncounterEntriesSnapshotPlain(
        int limit
    ) => System.Array.Empty<IReadOnlyDictionary<string, object>>();

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyWorldEventEntriesSnapshotPlain(
        int limit
    ) => System.Array.Empty<IReadOnlyDictionary<string, object>>();

    public string GetActiveMapId() => "";

    public string GetActiveMapDisplayName() => "";

    public bool IsSubmapActive() => false;

    public int GetWorldStep() => 0;

    public Vector2I GetPlayerCoord() => Vector2I.Zero;

    public bool IsPlayerVisibleOnWorldMap() => false;

    public Vector2I GetSelectedCoord() => Vector2I.Zero;

    public IReadOnlyDictionary<string, object> GetPendingSubmapPromptSnapshotPlain() =>
        new Dictionary<string, object>();

    public string GetSubmapReturnHintText() => "";

    public IReadOnlyDictionary<string, object> GetGameOverContextSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(GameOverContext);

    public PartyState GetPartyState() => PartyState;

    public StringName GetPartySelectedMemberId() => "";

    public int GetPendingRewardCount() => 0;

    public IReadOnlyDictionary<string, object> GetMemberAchievementSummarySnapshotPlain(
        StringName member_id
    ) => new Dictionary<string, object>();

    public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id) => null;

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetMemberEquippedEntriesSnapshotPlain(
        StringName member_id
    ) => System.Array.Empty<IReadOnlyDictionary<string, object>>();

    public string GetMemberDisplayName(StringName member_id)
    {
        PartyMemberState memberState = PartyState?.GetMemberState(member_id);
        return memberState != null ? memberState.display_name : "";
    }

    public string GetResolvedSettlementId() => "";

    public IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlement_id
    ) => new Dictionary<string, object>();

    public string GetSettlementFeedbackText() => "";

    public IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain() =>
        new Dictionary<string, object>();

    public IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(ContractBoardWindowData);

    public IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(NpcQuestOfferWindowData);

    public IReadOnlyDictionary<string, object> GetBountyBoardWindowDataSnapshotPlain() =>
        new Dictionary<string, object>();

    public IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain() =>
        ForgeWindowData.Count > 0
            ? RuntimePlainPayload.CloneDictionary(ForgeWindowData)
            : ForgeFallbackPlain();

    public IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain() =>
        new Dictionary<string, object>();

    public IReadOnlyDictionary<string, object> GetCharacterInfoContextSnapshotPlain() =>
        new Dictionary<string, object>();

    public string GetActiveWarehouseEntryLabel() => "";

    public IReadOnlyDictionary<string, object> GetWarehouseWindowDataSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(WarehouseWindowData);

    public ContingencySetupMutationResult GetLastContingencyCommandResultTyped() =>
        ContingencySetupMutationResult.Failure("", "", "");

    public BattleState GetBattleState() => BattleState;

    public BattleRuntimeModule GetBattleRuntime() => BattleRuntime;

    public Vector2I GetBattleSelectedCoord() => BattleSelectedCoord;

    public StringName GetSelectedBattleSkillEntryId() => "";

    public StringName GetSelectedBattleSkillId() => "";

    public StringName GetSelectedBattleSkillVariantId() => "";

    public string GetSelectedBattleSkillName() => "";

    public string GetSelectedBattleSkillVariantName() => "";

    public System.Collections.Generic.IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain() =>
        System.Array.Empty<Vector2I>();

    public System.Collections.Generic.IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain() =>
        System.Array.Empty<StringName>();

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

    public IReadOnlyDictionary<string, object> GetPendingBattleStartPromptSnapshotPlain() =>
        new Dictionary<string, object>();

    public IReadOnlyDictionary<string, int> GetBattleTerrainCountsSnapshotTyped() =>
        new Dictionary<string, int>();

    public PendingCharacterReward GetSnapshotReward() => null;

    public IReadOnlyDictionary<string, object> GetLastBattleLootSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(LastBattleLootSnapshot);

    public IReadOnlyDictionary<string, object> GetCurrentPromotionPromptSnapshotPlain() =>
        new Dictionary<string, object>();

    public GameSession GetGameSession() => null;

    private IReadOnlyDictionary<string, object> ForgeFallbackPlain()
    {
        Dictionary<string, object> context = RuntimePlainPayload.CloneDictionary(
            ActiveShopContext
        );
        return context.TryGetValue("panel_kind", out object panelKind)
            && panelKind is string panelKindText
            && panelKindText == SettlementPanelKinds.ToPayloadValue(SettlementPanelKind.Forge)
                ? context
                : new Dictionary<string, object>();
    }
}

internal interface IGameRuntimeBattleWritebackPort
{
    void ApplyBattleLocalPartyState(PartyState candidateParty);

    void ReportBattleLocalWritebackInvariantFailure(
        string statusMessage,
        string contextJson
    );
}

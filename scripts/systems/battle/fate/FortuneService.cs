using System;
using Godot;

public sealed class FortuneMarkEventInput
{
    public StringName BattleId { get; init; } = "";
    public StringName AttackerMemberId { get; init; } = "";
    public StringName AttackerId { get; init; } = "";
    public StringName DefenderId { get; init; } = "";
    public int CritGateDie { get; init; }
    public bool IsDisadvantage { get; init; }
}

internal class FortuneService
{
    internal static readonly StringName FortuneMarkedStatId = "fortune_marked";
    internal const string FortuneMarkAttemptFlagPrefix = "fortune_mark_attempted:";
    internal static readonly StringName CriticalSuccessUnderDisadvantageEventId =
        "critical_success_under_disadvantage";

    private readonly Func<FortuneMarkEventInput, FateAttackFormula.IRollSource> _rollSourceFactory;
    private IBattleRuntimeCharacterGateway _characterGateway;

    public FortuneService(
        Func<FortuneMarkEventInput, FateAttackFormula.IRollSource> rollSourceFactory = null
    )
    {
        _rollSourceFactory = rollSourceFactory;
    }

    internal void Setup(IBattleRuntimeCharacterGateway characterGateway = null)
    {
        _characterGateway = characterGateway;
    }

    internal void Dispose()
    {
        _characterGateway = null;
    }

    public bool HasAttemptedFortuneMark(StringName memberId)
    {
        PartyState partyState = GetPartyState();
        if (partyState == null || IsEmpty(memberId))
            return false;
        return partyState.HasFateRunFlag(BuildFortuneMarkAttemptFlagId(memberId));
    }

    public bool TryGrantFortuneMark(FortuneMarkEventInput payload)
    {
        payload ??= new FortuneMarkEventInput();
        if (IsEmpty(payload.AttackerMemberId))
            return false;

        PartyState partyState = GetPartyState();
        if (partyState == null)
            return false;
        StringName attemptFlagId = BuildFortuneMarkAttemptFlagId(payload.AttackerMemberId);
        if (partyState.HasFateRunFlag(attemptFlagId))
            return false;

        PartyMemberState memberState = GetMemberState(payload.AttackerMemberId);
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        if (memberState == null || memberState.progression == null || attributes == null)
            return false;
        if (GetCustomStatValue(memberState, FortuneMarkedStatId) >= 1)
            return false;

        partyState.SetFateRunFlag(attemptFlagId, true);

        int confirmationRoll = FateAttackFormula.RollDieWithDisadvantageRule(
            payload.CritGateDie,
            payload.IsDisadvantage,
            ResolveRollSource(payload)
        );
        if (confirmationRoll < Mathf.Max(payload.CritGateDie, 1))
            return false;

        attributes.SetAttributeValue(FortuneMarkedStatId, 1);
        return true;
    }

    public static StringName BuildFortuneMarkAttemptFlagId(StringName memberId)
    {
        return new StringName($"{FortuneMarkAttemptFlagPrefix}{memberId}");
    }

    private FateAttackFormula.IRollSource ResolveRollSource(FortuneMarkEventInput payload)
    {
        return _rollSourceFactory?.Invoke(payload)
            ?? new SeededRuntimeRollSource(BuildConfirmationSeedSource(payload));
    }

    private static string BuildConfirmationSeedSource(FortuneMarkEventInput payload)
    {
        return string.Format(
            "{0}:{1}:{2}:{3}:{4}:{5}",
            payload.BattleId,
            payload.AttackerMemberId,
            payload.AttackerId,
            payload.DefenderId,
            payload.CritGateDie,
            payload.IsDisadvantage ? 1 : 0
        );
    }

    private PartyState GetPartyState()
    {
        return _characterGateway?.GetPartyState();
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || IsEmpty(memberId))
            return null;
        return _characterGateway.GetMemberState(memberId);
    }

    private static int GetCustomStatValue(PartyMemberState memberState, StringName statId)
    {
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        return attributes?.GetAttributeValue(statId) ?? 0;
    }

    private static UnitBaseAttributes GetUnitBaseAttributes(PartyMemberState memberState)
    {
        return memberState?.progression?.unit_base_attributes;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }

    private sealed class SeededRuntimeRollSource : FateAttackFormula.IRollSource
    {
        private readonly RuntimeRandom _rng;

        public SeededRuntimeRollSource(string seedSource)
        {
            _rng = new RuntimeRandom(unchecked((long)StringExtensions.Hash(seedSource ?? "")));
        }

        public int RandiRange(int minValue, int maxValue)
        {
            return _rng.RandiRange(minValue, maxValue);
        }
    }
}

using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class FortuneService : RefCounted
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private const string FortuneMarkAttemptFlagPrefix = "fortune_mark_attempted:";
    private static readonly StringName CriticalSuccessUnderDisadvantage =
        "critical_success_under_disadvantage";

    private IBattleRuntimeCharacterGateway _characterGateway;
    private BattleFateEventBus _fateEventBus;
    private RandomNumberGenerator _confirmationRngOverride;

    public static StringName FORTUNE_MARKED_STAT_ID()
    {
        return FortuneMarkedStatId;
    }

    public static string FORTUNE_MARK_ATTEMPT_FLAG_PREFIX()
    {
        return FortuneMarkAttemptFlagPrefix;
    }

    public void setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        BattleFateEventBus fate_event_bus = null
    )
    {
        _characterGateway = character_gateway;
        bind_fate_event_bus(fate_event_bus);
    }

    public void bind_fate_event_bus(BattleFateEventBus fate_event_bus = null)
    {
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched -= OnFateEvent;
        _fateEventBus = fate_event_bus;
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched += OnFateEvent;
    }

    public void dispose()
    {
        bind_fate_event_bus(null);
        _characterGateway = null;
        _confirmationRngOverride = null;
    }

    public void set_confirmation_rng_for_testing(RandomNumberGenerator rng = null)
    {
        _confirmationRngOverride = rng;
    }

    public bool has_attempted_fortune_mark(StringName member_id)
    {
        PartyState partyState = GetPartyState();
        if (partyState == null || member_id == "")
            return false;
        return partyState.has_fate_run_flag(BuildFortuneMarkAttemptFlagId(member_id));
    }

    public bool try_grant_fortune_mark_from_payload(GDictionary payload)
    {
        FortuneMarkPayload markPayload = FortuneMarkPayload.FromDictionary(payload);
        if (markPayload.AttackerMemberId == "")
            return false;

        PartyState partyState = GetPartyState();
        if (partyState == null)
            return false;
        StringName attemptFlagId = BuildFortuneMarkAttemptFlagId(markPayload.AttackerMemberId);
        if (partyState.has_fate_run_flag(attemptFlagId))
            return false;

        PartyMemberState memberState = GetMemberState(markPayload.AttackerMemberId);
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        if (memberState == null || memberState.progression == null || attributes == null)
            return false;
        if (GetCustomStatValue(memberState, FortuneMarkedStatId) >= 1)
            return false;

        partyState.set_fate_run_flag(attemptFlagId, true);

        int confirmationRoll = FateAttackFormula.roll_die_with_disadvantage_rule(
            markPayload.CritGateDie,
            markPayload.IsDisadvantage,
            ResolveConfirmationRng(markPayload)
        );
        if (confirmationRoll < markPayload.CritGateDie)
            return false;

        attributes.set_attribute_value(FortuneMarkedStatId, 1);
        return true;
    }

    private void OnFateEvent(StringName eventType, GDictionary payload)
    {
        if (eventType != CriticalSuccessUnderDisadvantage)
            return;
        try_grant_fortune_mark_from_payload(payload);
    }

    private RandomNumberGenerator ResolveConfirmationRng(FortuneMarkPayload payload)
    {
        if (_confirmationRngOverride != null)
            return _confirmationRngOverride;
        var rng = new RandomNumberGenerator();
        rng.Seed = StringExtensions.Hash(BuildConfirmationSeedSource(payload));
        return rng;
    }

    private static string BuildConfirmationSeedSource(FortuneMarkPayload payload)
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
        return _characterGateway?.get_party_state();
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.get_member_state(memberId);
    }

    private static int GetCustomStatValue(PartyMemberState memberState, StringName statId)
    {
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        return attributes?.get_attribute_value(statId) ?? 0;
    }

    private static UnitBaseAttributes GetUnitBaseAttributes(PartyMemberState memberState)
    {
        return memberState?.progression?.unit_base_attributes;
    }

    private static StringName BuildFortuneMarkAttemptFlagId(StringName memberId)
    {
        return ProgressionDataUtils.to_string_name(
            string.Format("{0}{1}", FortuneMarkAttemptFlagPrefix, (string)memberId)
        );
    }

    private static StringName GetStringName(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(payload[key]);
    }

    private static int GetInt(GDictionary payload, string key, int fallback = 0)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        var value = payload[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static string GetString(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        return payload[key].AsString();
    }

    private readonly struct FortuneMarkPayload
    {
        internal readonly string BattleId;
        internal readonly StringName AttackerMemberId;
        internal readonly string AttackerId;
        internal readonly string DefenderId;
        internal readonly int CritGateDie;
        internal readonly bool IsDisadvantage;

        private FortuneMarkPayload(
            string battleId,
            StringName attackerMemberId,
            string attackerId,
            string defenderId,
            int critGateDie,
            bool isDisadvantage
        )
        {
            BattleId = battleId ?? "";
            AttackerMemberId = attackerMemberId;
            AttackerId = attackerId ?? "";
            DefenderId = defenderId ?? "";
            CritGateDie = Mathf.Max(critGateDie, 1);
            IsDisadvantage = isDisadvantage;
        }

        internal static FortuneMarkPayload FromDictionary(GDictionary payload)
        {
            bool isDisadvantage = false;
            if (payload != null && payload.ContainsKey("is_disadvantage"))
            {
                Variant value = payload["is_disadvantage"];
                if (value.VariantType == Variant.Type.Bool)
                    isDisadvantage = value.AsBool();
            }
            else if (payload != null && payload.ContainsKey((StringName)"is_disadvantage"))
            {
                Variant value = payload[(StringName)"is_disadvantage"];
                if (value.VariantType == Variant.Type.Bool)
                    isDisadvantage = value.AsBool();
            }

            return new FortuneMarkPayload(
                GetString(payload, "battle_id"),
                GetStringName(payload, "attacker_member_id"),
                GetString(payload, "attacker_id"),
                GetString(payload, "defender_id"),
                GetInt(payload, "crit_gate_die", 0),
                isDisadvantage
            );
        }
    }
}

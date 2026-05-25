using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class FortuneService : RefCounted
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private const string FortuneMarkAttemptFlagPrefix = "fortune_mark_attempted:";
    private static readonly StringName CriticalSuccessUnderDisadvantage = "critical_success_under_disadvantage";

    private GodotObject _characterGateway;
    private BattleFateEventBus _fateEventBus;
    private GodotObject _confirmationRngOverride;
    private Callable _fateEventCallback;

    public FortuneService()
    {
        _fateEventCallback = Callable.From<StringName, GDictionary>(OnFateEvent);
    }

    public static StringName FORTUNE_MARKED_STAT_ID()
    {
        return FortuneMarkedStatId;
    }

    public static string FORTUNE_MARK_ATTEMPT_FLAG_PREFIX()
    {
        return FortuneMarkAttemptFlagPrefix;
    }

    public void setup(GodotObject character_gateway = null, BattleFateEventBus fate_event_bus = null)
    {
        _characterGateway = character_gateway;
        bind_fate_event_bus(fate_event_bus);
    }

    public void bind_fate_event_bus(BattleFateEventBus fate_event_bus = null)
    {
        if (_fateEventBus != null && _fateEventBus.IsConnected(BattleFateEventBus.SignalName.EventDispatched, _fateEventCallback))
            _fateEventBus.Disconnect(BattleFateEventBus.SignalName.EventDispatched, _fateEventCallback);
        _fateEventBus = fate_event_bus;
        if (_fateEventBus != null && !_fateEventBus.IsConnected(BattleFateEventBus.SignalName.EventDispatched, _fateEventCallback))
            _fateEventBus.Connect(BattleFateEventBus.SignalName.EventDispatched, _fateEventCallback);
    }

    public void dispose()
    {
        bind_fate_event_bus(null);
        _characterGateway = null;
        _confirmationRngOverride = null;
    }

    public void set_confirmation_rng_for_testing(Variant rng = default)
    {
        _confirmationRngOverride = null;
        if (rng.VariantType != Variant.Type.Object)
            return;
        var rngObject = rng.AsGodotObject();
        if (rngObject != null && rngObject.HasMethod("randi_range"))
            _confirmationRngOverride = rngObject;
    }

    public bool has_attempted_fortune_mark(StringName member_id)
    {
        GodotObject partyState = GetPartyState();
        if (partyState == null || member_id == "")
            return false;
        return partyState.Call("has_fate_run_flag", BuildFortuneMarkAttemptFlagId(member_id)).AsBool();
    }

    public bool try_grant_fortune_mark_from_payload(GDictionary payload)
    {
        StringName attackerMemberId = ProgressionDataUtils.to_string_name(Get(payload, "attacker_member_id", ""));
        if (attackerMemberId == "")
            return false;

        GodotObject partyState = GetPartyState();
        if (partyState == null)
            return false;
        StringName attemptFlagId = BuildFortuneMarkAttemptFlagId(attackerMemberId);
        if (partyState.Call("has_fate_run_flag", attemptFlagId).AsBool())
            return false;

        PartyMemberState memberState = GetMemberState(attackerMemberId);
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        if (memberState == null || memberState.progression == null || attributes == null)
            return false;
        if (GetCustomStatValue(memberState, FortuneMarkedStatId) >= 1)
            return false;

        partyState.Call("set_fate_run_flag", attemptFlagId, true);

        int critGateDie = Mathf.Max(Get(payload, "crit_gate_die", 0).AsInt32(), 1);
        bool isDisadvantage = Get(payload, "is_disadvantage", false).AsBool();
        int confirmationRoll = FateAttackFormula.roll_die_with_disadvantage_rule(
            critGateDie,
            isDisadvantage,
            ResolveConfirmationRng(payload)
        );
        if (confirmationRoll < critGateDie)
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

    private Variant ResolveConfirmationRng(GDictionary payload)
    {
        if (_confirmationRngOverride != null && _confirmationRngOverride.HasMethod("randi_range"))
            return Variant.From(_confirmationRngOverride);
        var rng = new RandomNumberGenerator();
        rng.Seed = StringExtensions.Hash(BuildConfirmationSeedSource(payload));
        return Variant.From(rng);
    }

    private static string BuildConfirmationSeedSource(GDictionary payload)
    {
        return string.Format(
            "{0}:{1}:{2}:{3}:{4}:{5}",
            GetString(payload, "battle_id"),
            GetString(payload, "attacker_member_id"),
            GetString(payload, "attacker_id"),
            GetString(payload, "defender_id"),
            Get(payload, "crit_gate_die", 0).AsInt32(),
            Get(payload, "is_disadvantage", false).AsBool() ? 1 : 0
        );
    }

    private GodotObject GetPartyState()
    {
        if (_characterGateway == null || !_characterGateway.HasMethod("get_party_state"))
            return null;
        Variant partyState = _characterGateway.Call("get_party_state");
        return partyState.VariantType == Variant.Type.Object ? partyState.AsGodotObject() : null;
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "" || !_characterGateway.HasMethod("get_member_state"))
            return null;
        Variant memberState = _characterGateway.Call("get_member_state", memberId);
        return memberState.VariantType == Variant.Type.Object ? memberState.AsGodotObject() as PartyMemberState : null;
    }

    private static int GetCustomStatValue(PartyMemberState memberState, StringName statId)
    {
        UnitBaseAttributes attributes = GetUnitBaseAttributes(memberState);
        return attributes?.get_attribute_value(statId) ?? 0;
    }

    private static UnitBaseAttributes GetUnitBaseAttributes(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return null;
        Variant attributes = memberState.progression.Get("unit_base_attributes");
        return attributes.VariantType == Variant.Type.Object ? attributes.AsGodotObject() as UnitBaseAttributes : null;
    }

    private static StringName BuildFortuneMarkAttemptFlagId(StringName memberId)
    {
        return ProgressionDataUtils.to_string_name(string.Format("{0}{1}", FortuneMarkAttemptFlagPrefix, (string)memberId));
    }

    private static Variant Get(GDictionary payload, string key, Variant fallback = default)
    {
        if (payload != null && payload.ContainsKey(key))
            return payload[key];
        return fallback;
    }

    private static string GetString(GDictionary payload, string key)
    {
        return Get(payload, key, "").AsString();
    }
}

using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class FortuneService : RefCounted
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private const string FortuneMarkAttemptFlagPrefix = "fortune_mark_attempted:";
    private static readonly StringName CriticalSuccessUnderDisadvantage =
        "critical_success_under_disadvantage";

    private GodotObject _characterGateway;
    private BattleFateEventBus _fateEventBus;
    private GodotObject _confirmationRngOverride;

    public static StringName FORTUNE_MARKED_STAT_ID()
    {
        return FortuneMarkedStatId;
    }

    public static string FORTUNE_MARK_ATTEMPT_FLAG_PREFIX()
    {
        return FortuneMarkAttemptFlagPrefix;
    }

    public void setup(
        GodotObject character_gateway = null,
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

    public void set_confirmation_rng_for_testing(GodotObject rng = null)
    {
        _confirmationRngOverride = null;
        if (rng != null && rng.HasMethod("randi_range"))
            _confirmationRngOverride = rng;
    }

    public bool has_attempted_fortune_mark(StringName member_id)
    {
        GodotObject partyState = GetPartyState();
        if (partyState == null || member_id == "")
            return false;
        return partyState
            .Call("has_fate_run_flag", BuildFortuneMarkAttemptFlagId(member_id))
            .AsBool();
    }

    public bool try_grant_fortune_mark_from_payload(GDictionary payload)
    {
        StringName attackerMemberId = GetStringName(payload, "attacker_member_id");
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

        int critGateDie = Mathf.Max(GetInt(payload, "crit_gate_die", 0), 1);
        bool isDisadvantage = GetBool(payload, "is_disadvantage", false);
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

    private GodotObject ResolveConfirmationRng(GDictionary payload)
    {
        if (_confirmationRngOverride != null && _confirmationRngOverride.HasMethod("randi_range"))
            return _confirmationRngOverride;
        var rng = new RandomNumberGenerator();
        rng.Seed = StringExtensions.Hash(BuildConfirmationSeedSource(payload));
        return rng;
    }

    private static string BuildConfirmationSeedSource(GDictionary payload)
    {
        return string.Format(
            "{0}:{1}:{2}:{3}:{4}:{5}",
            GetString(payload, "battle_id"),
            GetString(payload, "attacker_member_id"),
            GetString(payload, "attacker_id"),
            GetString(payload, "defender_id"),
            GetInt(payload, "crit_gate_die", 0),
            GetBool(payload, "is_disadvantage", false) ? 1 : 0
        );
    }

    private GodotObject GetPartyState()
    {
        if (_characterGateway == null || !_characterGateway.HasMethod("get_party_state"))
            return null;
        var partyState = _characterGateway.Call("get_party_state");
        return partyState.VariantType == Variant.Type.Object ? partyState.AsGodotObject() : null;
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (
            _characterGateway == null
            || memberId == ""
            || !_characterGateway.HasMethod("get_member_state")
        )
            return null;
        var memberState = _characterGateway.Call("get_member_state", memberId);
        return memberState.VariantType == Variant.Type.Object
            ? memberState.AsGodotObject() as PartyMemberState
            : null;
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
        var attributes = memberState.progression.Get("unit_base_attributes");
        return attributes.VariantType == Variant.Type.Object
            ? attributes.AsGodotObject() as UnitBaseAttributes
            : null;
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

    private static bool GetBool(GDictionary payload, string key, bool fallback = false)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        var value = payload[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    private static string GetString(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        return payload[key].AsString();
    }
}

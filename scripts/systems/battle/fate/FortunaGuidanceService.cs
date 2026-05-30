using Godot;

[GlobalClass]
public partial class FortunaGuidanceService : RefCounted
{
    public static readonly StringName ACHIEVEMENT_GUIDANCE_TRUE = "fortuna_guidance_true",
        ACHIEVEMENT_GUIDANCE_DEVOUT = "fortuna_guidance_devout",
        ACHIEVEMENT_GUIDANCE_EXALTED = "fortuna_guidance_exalted",
        ACHIEVEMENT_GUIDANCE_BLESSED = "fortuna_guidance_blessed";
    private static readonly StringName FORTUNE_MARKED_STAT_ID = "fortune_marked";
    private const string CHAPTER_EVENT_FLAG_PREFIX = "fortuna_guidance_chapter_seen:",
        DEVOUT_BATTLE_FLAG_PREFIX = "fortuna_guidance_devout_battle:";
    private IBattleRuntimeCharacterGateway _character_gateway;
    private BattleFateEventBus _fate_event_bus;

    public static StringName ACHIEVEMENT_GUIDANCE_TRUE_ID() => ACHIEVEMENT_GUIDANCE_TRUE;

    public static StringName ACHIEVEMENT_GUIDANCE_DEVOUT_ID() => ACHIEVEMENT_GUIDANCE_DEVOUT;

    public static StringName ACHIEVEMENT_GUIDANCE_EXALTED_ID() => ACHIEVEMENT_GUIDANCE_EXALTED;

    public static StringName ACHIEVEMENT_GUIDANCE_BLESSED_ID() => ACHIEVEMENT_GUIDANCE_BLESSED;

    public void setup(IBattleRuntimeCharacterGateway characterGateway = null, BattleFateEventBus fateEventBus = null)
    {
        _character_gateway = characterGateway;
        bind_fate_event_bus(fateEventBus);
    }

    public void bind_fate_event_bus(BattleFateEventBus fateEventBus = null)
    {
        if (_fate_event_bus != null)
            _fate_event_bus.EventDispatched -= _on_fate_event;
        _fate_event_bus = fateEventBus;
        if (_fate_event_bus != null)
            _fate_event_bus.EventDispatched += _on_fate_event;
    }

    public void dispose()
    {
        bind_fate_event_bus(null);
        _character_gateway = null;
    }

    public Godot.Collections.Array<StringName> handle_battle_resolution(
        BattleState battleState,
        BattleResolutionResult battleResolutionResult
    )
    {
        var unlockedIds = new Godot.Collections.Array<StringName>();
        var partyState = _get_party_state();
        if (partyState == null || battleState == null || battleResolutionResult == null)
            return unlockedIds;
        var battleId =
            battleResolutionResult.battle_id != ""
                ? battleResolutionResult.battle_id
                : battleState.battle_id;
        if (battleId == "")
            return unlockedIds;
        bool playerWon = battleResolutionResult.winner_faction_id == "player";
        foreach (var auId in battleState.ally_unit_ids)
        {
            var us = battleState.units.ContainsKey(auId)
                ? battleState.units[auId].AsGodotObject() as BattleUnitState
                : null;
            if (us == null || us.source_member_id == "")
                continue;
            var flagId = _build_devout_battle_flag_id(battleId, us.source_member_id);
            if (!partyState.has_fate_run_flag(flagId))
                continue;
            if (
                playerWon
                && us.is_alive
                && _unlock_achievement(us.source_member_id, ACHIEVEMENT_GUIDANCE_DEVOUT)
            )
                _append_unique_string_name(unlockedIds, ACHIEVEMENT_GUIDANCE_DEVOUT);
            partyState.clear_fate_run_flag(flagId);
        }
        return unlockedIds;
    }

    public Godot.Collections.Array<StringName> handle_chapter_completed(
        Godot.Collections.Dictionary payload
    )
    {
        var unlockedIds = new Godot.Collections.Array<StringName>();
        var partyState = _get_party_state();
        if (partyState == null)
            return unlockedIds;
        var memberIds = _resolve_chapter_member_ids(payload, partyState);
        if (memberIds.Count == 0)
            return unlockedIds;
        bool hadPermDeath =
            (
                payload.ContainsKey("had_permanent_death")
                && payload["had_permanent_death"].VariantType == Variant.Type.Bool
                && payload["had_permanent_death"].AsBool()
            )
            || (
                payload.ContainsKey("has_permanent_death")
                && payload["has_permanent_death"].VariantType == Variant.Type.Bool
                && payload["has_permanent_death"].AsBool()
            );
        foreach (var mid in memberIds)
        {
            var flagId = _build_chapter_event_flag_id(mid);
            bool shouldUnlock =
                !hadPermDeath
                && partyState.has_fate_run_flag(flagId)
                && _is_fortuna_devotee(_get_member_state(mid));
            if (shouldUnlock && _unlock_achievement(mid, ACHIEVEMENT_GUIDANCE_BLESSED))
                _append_unique_string_name(unlockedIds, ACHIEVEMENT_GUIDANCE_BLESSED);
            partyState.clear_fate_run_flag(flagId);
        }
        return unlockedIds;
    }

    private void _on_fate_event(StringName eventType, Godot.Collections.Dictionary payload)
    {
        if (eventType == "critical_success_under_disadvantage")
            _handle_critical_success_under_disadvantage(payload);
        else if (eventType == "high_threat_critical_hit")
            _handle_high_threat_critical_hit(payload);
        else if (eventType == "hardship_survival")
            _handle_hardship_survival(payload);
    }

    private void _handle_critical_success_under_disadvantage(Godot.Collections.Dictionary payload)
    {
        if (
            !payload.ContainsKey("defender_is_elite_or_boss")
            || !payload["defender_is_elite_or_boss"].AsBool()
        )
            return;
        var mid = _resolve_attacker_member_id(payload);
        if (mid == "")
            return;
        _mark_chapter_event_seen(mid);
        if (_is_fortuna_marked(_get_member_state(mid)))
            _unlock_achievement(mid, ACHIEVEMENT_GUIDANCE_TRUE);
    }

    private void _handle_high_threat_critical_hit(Godot.Collections.Dictionary payload)
    {
        if (
            !payload.ContainsKey("defender_is_elite_or_boss")
            || !payload["defender_is_elite_or_boss"].AsBool()
        )
            return;
        var mid = _resolve_attacker_member_id(payload);
        if (mid == "")
            return;
        var ms = _get_member_state(mid);
        if (!_is_fortuna_devotee(ms))
            return;
        _mark_chapter_event_seen(mid);
        _unlock_achievement(mid, ACHIEVEMENT_GUIDANCE_EXALTED);
    }

    private void _handle_hardship_survival(Godot.Collections.Dictionary payload)
    {
        var bid = ProgressionDataUtils.to_string_name(
            payload.ContainsKey("battle_id") ? payload["battle_id"] : ""
        );
        var mid = _resolve_attacker_member_id(payload);
        if (bid == "" || mid == "")
            return;
        if (!_is_fortuna_devotee(_get_member_state(mid)))
            return;
        if (
            !payload.ContainsKey("attacker_low_hp_hardship")
            || !payload["attacker_low_hp_hardship"].AsBool()
        )
            return;
        var sdIds = ProgressionDataUtils.to_string_name_array(
            payload.ContainsKey("attacker_strong_attack_debuff_ids")
                ? payload["attacker_strong_attack_debuff_ids"]
                : new Godot.Collections.Array()
        );
        if (sdIds.Count == 0)
            return;
        _mark_chapter_event_seen(mid);
        var ps = _get_party_state();
        if (ps != null)
            ps.set_fate_run_flag(_build_devout_battle_flag_id(bid, mid), true);
    }

    private static StringName _resolve_attacker_member_id(Godot.Collections.Dictionary payload) =>
        ProgressionDataUtils.to_string_name(
            payload.ContainsKey("attacker_member_id") ? payload["attacker_member_id"] : ""
        );

    private static Godot.Collections.Array<StringName> _resolve_chapter_member_ids(
        Godot.Collections.Dictionary payload,
        GodotObject partyState
    )
    {
        var emIds = ProgressionDataUtils.to_string_name_array(
            payload.ContainsKey("member_ids")
                ? payload["member_ids"]
                : new Godot.Collections.Array()
        );
        if (emIds.Count > 0)
            return emIds;
        var r = new Godot.Collections.Array<StringName>();
        var mss = partyState.Get("member_states").AsGodotDictionary();
        foreach (var mk in ProgressionDataUtils.sorted_string_keys(mss))
            r.Add(new StringName(mk));
        return r;
    }

    private void _mark_chapter_event_seen(StringName mid)
    {
        var ps = _get_party_state();
        if (ps != null && mid != "")
            ps.set_fate_run_flag(_build_chapter_event_flag_id(mid), true);
    }

    private bool _unlock_achievement(StringName mid, StringName achId)
    {
        if (_character_gateway == null || mid == "" || achId == "")
            return false;
        return (_character_gateway as CharacterManagementModule)?.unlock_achievement(
            mid,
            achId,
            new Godot.Collections.Dictionary { { "summary_text", _build_summary_text(achId) } }
        ) ?? false;
    }

    private static bool _is_fortuna_marked(PartyMemberState ms)
    {
        if (ms?.progression?.Get("unit_base_attributes").AsGodotObject() == null)
            return false;
        return ms.progression?.unit_base_attributes?.get_attribute_value(FORTUNE_MARKED_STAT_ID) > 0;
    }

    private static bool _is_fortuna_devotee(PartyMemberState ms) =>
        ms != null && ms.get_faith_luck_bonus() > 0;

    private static string _build_summary_text(StringName achId)
    {
        if (achId == ACHIEVEMENT_GUIDANCE_TRUE)
            return "Fortuna 再次看见了这名角色。";
        if (achId == ACHIEVEMENT_GUIDANCE_DEVOUT)
            return "逆境中的胜利让 Fortuna 的怜悯有了回应。";
        if (achId == ACHIEVEMENT_GUIDANCE_EXALTED)
            return "好运不再只是门骰，而是被抬进了真正的高位威胁区间。";
        if (achId == ACHIEVEMENT_GUIDANCE_BLESSED)
            return "整章旅程都被 Fortuna 的影子护住了。";
        return "";
    }

    private PartyState _get_party_state() =>
        _character_gateway?.get_party_state();

    private PartyMemberState _get_member_state(StringName mid) =>
        _character_gateway != null && mid != "" ? _character_gateway.get_member_state(mid) : null;

    private static StringName _build_chapter_event_flag_id(StringName mid) =>
        ProgressionDataUtils.to_string_name($"{CHAPTER_EVENT_FLAG_PREFIX}{(string)mid}");

    private static StringName _build_devout_battle_flag_id(StringName bid, StringName mid) =>
        ProgressionDataUtils.to_string_name(
            $"{DEVOUT_BATTLE_FLAG_PREFIX}{(string)bid}:{(string)mid}"
        );

    private static void _append_unique_string_name(
        Godot.Collections.Array<StringName> values,
        StringName value
    )
    {
        if (value != "" && !values.Contains(value))
            values.Add(value);
    }
}

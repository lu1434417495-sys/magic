using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public enum ContingencyTriggerKind
{
    Unknown = 0,
    CombatStarted,
    HpBelowPercent,
    IncomingDamagePercent,
    FatalDamageIncoming,
    StatusApplied,
    EnemyEnterRadius,
    AffectedBySpell,
    OwnerTurnStarted,
}

public enum ContingencyTimingKind
{
    Unknown = 0,
    AfterBattleConfirmed,
    BeforeSpellEffectResolved,
    BeforeDamageResolved,
    AfterHpChanged,
    AfterStatusApplied,
    AfterPositionChanged,
    OwnerTurnStarted,
}

public class ContingencyTriggerState
{
    private readonly RuntimePayloadStore _payload = new();

    public ContingencyTriggerKind TriggerKind { get; private set; } =
        ContingencyTriggerKind.Unknown;
    public StringName Type { get; private set; } = "";
    public GDictionary Payload => _payload.ProjectPayload();

    public ContingencyTriggerState DuplicateState() => FromDictionary(ToDictionary());

    public GDictionary ToDictionary() => Payload;

    public static ContingencyTriggerState FromDictionary(GDictionary payload)
    {
        if (!ContingencySchemaUtils.TryReadStringName(payload, "type", false, out StringName type))
            return null;

        string[] keys = GetPayloadKeys(type);
        if (keys == null || !ContingencySchemaUtils.HasExactKeys(payload, keys))
            return null;
        if (!ValidatePayload(type, payload))
            return null;

        var state = new ContingencyTriggerState
        {
            TriggerKind = ToTriggerKind(type),
            Type = type,
        };
        state._payload.ReplaceWithPayload(payload);
        return state;
    }

    internal static StringName ToStringName(ContingencyTriggerKind kind)
    {
        return kind switch
        {
            ContingencyTriggerKind.CombatStarted => "combat_started",
            ContingencyTriggerKind.HpBelowPercent => "hp_below_percent",
            ContingencyTriggerKind.IncomingDamagePercent => "incoming_damage_percent",
            ContingencyTriggerKind.FatalDamageIncoming => "fatal_damage_incoming",
            ContingencyTriggerKind.StatusApplied => "status_applied",
            ContingencyTriggerKind.EnemyEnterRadius => "enemy_enter_radius",
            ContingencyTriggerKind.AffectedBySpell => "affected_by_spell",
            ContingencyTriggerKind.OwnerTurnStarted => "owner_turn_started",
            _ => new StringName(""),
        };
    }

    private static ContingencyTriggerKind ToTriggerKind(StringName type)
    {
        if (type == "combat_started")
            return ContingencyTriggerKind.CombatStarted;
        if (type == "hp_below_percent")
            return ContingencyTriggerKind.HpBelowPercent;
        if (type == "incoming_damage_percent")
            return ContingencyTriggerKind.IncomingDamagePercent;
        if (type == "fatal_damage_incoming")
            return ContingencyTriggerKind.FatalDamageIncoming;
        if (type == "status_applied")
            return ContingencyTriggerKind.StatusApplied;
        if (type == "enemy_enter_radius")
            return ContingencyTriggerKind.EnemyEnterRadius;
        if (type == "affected_by_spell")
            return ContingencyTriggerKind.AffectedBySpell;
        if (type == "owner_turn_started")
            return ContingencyTriggerKind.OwnerTurnStarted;
        return ContingencyTriggerKind.Unknown;
    }

    private static string[] GetPayloadKeys(StringName type)
    {
        if (type == "combat_started" || type == "fatal_damage_incoming" || type == "owner_turn_started")
            return new[] { "type", "subject", "timing" };
        if (type == "hp_below_percent")
            return new[] { "type", "subject", "percent", "crossing_only", "timing" };
        if (type == "incoming_damage_percent")
        {
            return new[]
            {
                "type",
                "subject",
                "damage_percent",
                "damage_basis",
                "damage_amount_mode",
                "timing",
            };
        }
        if (type == "enemy_enter_radius")
            return new[] { "type", "center", "radius", "radius_metric", "source_team", "timing" };
        if (type == "status_applied")
            return new[] { "type", "subject", "status_tags", "application_match", "timing" };
        if (type == "affected_by_spell")
            return new[] { "type", "subject", "source_team", "spell_match", "timing" };
        return null;
    }

    private static bool ValidatePayload(StringName type, GDictionary payload)
    {
        if (type == "combat_started")
            return HasSubject(payload) && HasTiming(payload, "after_battle_confirmed");
        if (type == "owner_turn_started")
            return HasSubject(payload) && HasTiming(payload, "owner_turn_started");
        if (type == "fatal_damage_incoming")
            return HasSubject(payload) && HasTiming(payload, "before_damage_resolved");
        if (type == "hp_below_percent")
        {
            return HasSubject(payload)
                && HasTiming(payload, "after_hp_changed")
                && ContingencySchemaUtils.TryReadInt(payload, "percent", out int percent)
                && percent > 0
                && percent <= 100
                && ContingencySchemaUtils.TryReadBool(payload, "crossing_only", out _);
        }
        if (type == "incoming_damage_percent")
        {
            return HasSubject(payload)
                && HasTiming(payload, "before_damage_resolved")
                && ContingencySchemaUtils.TryReadInt(payload, "damage_percent", out int percent)
                && percent > 0
                && percent <= 100
                && HasStringNameValue(payload, "damage_basis", "max_hp")
                && HasStringNameValue(
                    payload,
                    "damage_amount_mode",
                    "projected_hp_damage_after_shield"
                );
        }
        if (type == "enemy_enter_radius")
        {
            return HasStringNameValue(payload, "center", "owner")
                && ContingencySchemaUtils.TryReadInt(payload, "radius", out int radius)
                && radius > 0
                && HasStringNameValue(payload, "radius_metric", "manhattan")
                && HasStringNameValue(payload, "source_team", "hostile")
                && HasTiming(payload, "after_position_changed");
        }
        if (type == "status_applied")
        {
            return HasSubject(payload)
                && HasStringNameArray(payload, "status_tags", requireNonEmpty: true)
                && HasStringNameValue(payload, "application_match", "new_status_only")
                && HasTiming(payload, "after_status_applied");
        }
        if (type == "affected_by_spell")
        {
            return HasSubject(payload)
                && HasStringNameValue(payload, "source_team", "hostile")
                && HasStringNameValue(payload, "spell_match", "any")
                && HasTiming(payload, "before_spell_effect_resolved");
        }
        return false;
    }

    private static bool HasSubject(GDictionary payload) =>
        HasStringNameValue(payload, "subject", "owner");

    private static bool HasTiming(GDictionary payload, string expected) =>
        HasStringNameValue(payload, "timing", expected);

    private static bool HasStringNameValue(GDictionary payload, string key, string expected)
    {
        return ContingencySchemaUtils.TryReadStringName(payload, key, false, out StringName value)
            && value == expected;
    }

    private static bool HasStringNameArray(GDictionary payload, string key, bool requireNonEmpty)
    {
        if (!ContingencySchemaUtils.TryReadArray(payload, key, out GArray values))
            return false;
        if (requireNonEmpty && values.Count == 0)
            return false;
        foreach (Variant value in values)
        {
            if (!ContingencySchemaUtils.TryAsStringLike(value, out string text))
                return false;
            if (new StringName(text) == "")
                return false;
        }
        return true;
    }
}

using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal readonly record struct DamageDiceEventSnapshot(
    bool DamageDiceHighTotalRoll,
    StringName DamageDiceHighTotalRollReason,
    bool SkillDamageDiceIsMax,
    DamageDiceMaxReasonKind SkillDamageDiceIsMaxReason,
    bool WeaponDamageDiceIsMax,
    DamageDiceMaxReasonKind WeaponDamageDiceIsMaxReason
)
{
    public static DamageDiceEventSnapshot Empty => new(
        false,
        "",
        false,
        DamageDiceMaxReasonKind.None,
        false,
        DamageDiceMaxReasonKind.None
    );

    internal static DamageDiceEventSnapshot FromDictionary(GDictionary payload)
    {
        GDictionary normalized = EnsureDamageDiceEventDefaults(payload);
        return new DamageDiceEventSnapshot(
            ReadDamageDiceFlag(normalized, "damage_dice_high_total_roll"),
            ReadStringName(normalized, "damage_dice_high_total_roll_reason"),
            ReadDamageDiceFlag(normalized, "skill_damage_dice_is_max"),
            AttackEffectResolutionResultReader.ParseDamageDiceMaxReason(
                ReadStringName(normalized, "skill_damage_dice_is_max_reason")
            ),
            ReadDamageDiceFlag(normalized, "weapon_damage_dice_is_max"),
            AttackEffectResolutionResultReader.ParseDamageDiceMaxReason(
                ReadStringName(normalized, "weapon_damage_dice_is_max_reason")
            )
        );
    }

    private static GDictionary EnsureDamageDiceEventDefaults(GDictionary source)
    {
        GDictionary result = source?.Duplicate(false) ?? new GDictionary();
        if (!result.ContainsKey("damage_dice_high_total_roll_reason"))
            result["damage_dice_high_total_roll_reason"] = new StringName("");
        if (!result.ContainsKey("skill_damage_dice_is_max_reason"))
            result["skill_damage_dice_is_max_reason"] = DamageDiceMaxReasonKind.None.ToString();
        if (!result.ContainsKey("weapon_damage_dice_is_max_reason"))
            result["weapon_damage_dice_is_max_reason"] = DamageDiceMaxReasonKind.None.ToString();
        return result;
    }

    private static bool ReadDamageDiceFlag(GDictionary payload, string key)
    {
        return payload != null
            && payload.ContainsKey(key)
            && payload[key].VariantType == Variant.Type.Bool
            && payload[key].AsBool();
    }

    private static StringName ReadStringName(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(payload[key]);
    }
}

internal readonly record struct DamageApplicationInput(
    DamageEventResult Event,
    int ResolvedDamage,
    bool BypassShield,
    bool BypassDeathPrevention,
    double ShieldAbsorptionPercent,
    int MinHpAfterDamage,
    bool LowLuckBlackStarWedgeTriggered,
    DamageDiceEventSnapshot DamageDiceEvent,
    bool SuppressDamageApplicationHook
)
{
    public static DamageApplicationInput Empty => new(
        new DamageEventResult(),
        0,
        false,
        false,
        100.0,
        0,
        false,
        DamageDiceEventSnapshot.Empty,
        true
    );

    public static DamageApplicationInput Create(
        DamageEventResult @event,
        int resolvedDamage,
        bool bypassShield = false,
        bool bypassDeathPrevention = false,
        double shieldAbsorptionPercent = 100.0,
        int minHpAfterDamage = 0,
        bool lowLuckBlackStarWedgeTriggered = false,
        DamageDiceEventSnapshot damageDiceEvent = default,
        bool suppressDamageApplicationHook = false
    )
    {
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        @event.ResolvedDamage = normalizedDamage;
        @event.BypassShield = bypassShield;
        @event.BypassDeathPrevention = bypassDeathPrevention;
        @event.ShieldAbsorptionPercent = shieldAbsorptionPercent;
        @event.MinHpAfterDamage = Math.Max(minHpAfterDamage, 0);
        @event.LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered;
        return new DamageApplicationInput(
            @event,
            normalizedDamage,
            bypassShield,
            bypassDeathPrevention,
            shieldAbsorptionPercent,
            Math.Max(minHpAfterDamage, 0),
            lowLuckBlackStarWedgeTriggered,
            damageDiceEvent.Equals(default(DamageDiceEventSnapshot))
                ? DamageDiceEventSnapshot.Empty
                : damageDiceEvent,
            suppressDamageApplicationHook
        );
    }

    public static DamageApplicationInput Create(
        GDictionary payload,
        int resolvedDamage,
        bool bypassShield = false,
        bool bypassDeathPrevention = false,
        double shieldAbsorptionPercent = 100.0,
        int minHpAfterDamage = 0,
        bool lowLuckBlackStarWedgeTriggered = false,
        DamageDiceEventSnapshot damageDiceEvent = default,
        bool suppressDamageApplicationHook = false
    )
    {
        return Create(
            AttackEffectResolutionResultReader.ReadDamageEventPayload(payload),
            resolvedDamage,
            bypassShield,
            bypassDeathPrevention,
            shieldAbsorptionPercent,
            minHpAfterDamage,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEvent,
            suppressDamageApplicationHook
        );
    }

    internal static DamageApplicationInput FromDictionary(GDictionary payload)
    {
        GDictionary normalized = payload ?? new GDictionary();
        DamageEventResult @event =
            AttackEffectResolutionResultReader.ReadDamageEventPayload(normalized);
        return new DamageApplicationInput(
            @event,
            Math.Max(ReadInt(normalized, "resolved_damage"), 0),
            ReadBool(normalized, "bypass_shield"),
            ReadBool(normalized, "bypass_death_prevention"),
            ReadDouble(normalized, "shield_absorption_percent", 100.0),
            Math.Max(ReadInt(normalized, "min_hp_after_damage"), 0),
            ReadBool(normalized, "low_luck_black_star_wedge_triggered"),
            DamageDiceEventSnapshot.FromDictionary(normalized),
            ReadBool(normalized, "suppress_damage_application_hook")
        );
    }

    internal DamageApplicationInput WithResolvedDamage(int resolvedDamage)
    {
        DamageEventResult @event = Event;
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        @event.ResolvedDamage = normalizedDamage;
        return this with { Event = @event, ResolvedDamage = normalizedDamage };
    }

    internal DamageApplicationInput WithSuppressDamageApplicationHook(bool suppress) =>
        this with { SuppressDamageApplicationHook = suppress };

    internal GDictionary ToDictionary()
    {
        GDictionary payload = AttackEffectResolutionResultReader.BuildDamageEventPayload(Event);
        payload["resolved_damage"] = Math.Max(ResolvedDamage, 0);
        payload["bypass_shield"] = BypassShield;
        payload["bypass_death_prevention"] = BypassDeathPrevention;
        payload["shield_absorption_percent"] = ShieldAbsorptionPercent;
        payload["min_hp_after_damage"] = Math.Max(MinHpAfterDamage, 0);
        payload["low_luck_black_star_wedge_triggered"] = LowLuckBlackStarWedgeTriggered;
        payload["suppress_damage_application_hook"] = SuppressDamageApplicationHook;
        return payload;
    }

    private static bool ReadBool(GDictionary payload, string key)
    {
        return payload != null
            && payload.ContainsKey(key)
            && payload[key].VariantType == Variant.Type.Bool
            && payload[key].AsBool();
    }

    private static int ReadInt(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return 0;
        return payload[key].AsInt32();
    }

    private static double ReadDouble(GDictionary payload, string key, double fallback)
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Float || value.VariantType == Variant.Type.Int
            ? value.AsDouble()
            : fallback;
    }
}

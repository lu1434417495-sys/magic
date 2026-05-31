using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class BattleSpellControlMetadata
{
    public StringName AttackResolution { get; init; }
    public StringName SpellControlResolution { get; init; }
    public bool AttackSuccess { get; init; }
    public bool CriticalHit { get; init; }
    public bool CriticalFail { get; init; }
    public bool OrdinaryMiss { get; init; }
    public bool IsDisadvantage { get; init; }
    public int HiddenLuckAtBirth { get; init; }
    public int FaithLuckBonus { get; init; }
    public int EffectiveLuck { get; init; }
    public bool CritLocked { get; init; }
    public int CritGateDie { get; init; }
    public int CritGateRoll { get; init; }
    public int HitRoll { get; init; }
    public int FumbleLowEnd { get; init; }
    public int CritThreshold { get; init; }
    public int LockedSkillHitBonus { get; init; }
    public bool ReverseFateDowngraded { get; init; }
    public GDictionary Payload { get; init; } = new();
    public bool HasPayload => Payload != null && Payload.Count > 0;

    public static BattleSpellControlMetadata Empty() => new();

    public static BattleSpellControlMetadata FromDictionary(GDictionary payload)
    {
        GDictionary snapshot = DuplicateDictionary(payload);
        return new BattleSpellControlMetadata
        {
            AttackResolution = StringNameField(snapshot, "attack_resolution"),
            SpellControlResolution = StringNameField(snapshot, "spell_control_resolution"),
            AttackSuccess = BoolField(snapshot, "attack_success"),
            CriticalHit = BoolField(snapshot, "critical_hit"),
            CriticalFail = BoolField(snapshot, "critical_fail"),
            OrdinaryMiss = BoolField(snapshot, "ordinary_miss"),
            IsDisadvantage = BoolField(snapshot, "is_disadvantage"),
            HiddenLuckAtBirth = IntField(snapshot, "hidden_luck_at_birth"),
            FaithLuckBonus = IntField(snapshot, "faith_luck_bonus"),
            EffectiveLuck = IntField(snapshot, "effective_luck"),
            CritLocked = BoolField(snapshot, "crit_locked"),
            CritGateDie = IntField(snapshot, "crit_gate_die"),
            CritGateRoll = IntField(snapshot, "crit_gate_roll"),
            HitRoll = IntField(snapshot, "hit_roll"),
            FumbleLowEnd = IntField(snapshot, "fumble_low_end"),
            CritThreshold = IntField(snapshot, "crit_threshold"),
            LockedSkillHitBonus = IntField(snapshot, "locked_skill_hit_bonus"),
            ReverseFateDowngraded = BoolField(snapshot, "reverse_fate_downgraded"),
            Payload = snapshot,
        };
    }

    public GDictionary ToDictionary() => DuplicateDictionary(Payload);

    private static bool BoolField(GDictionary payload, string key, bool fallback = false)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int IntField(GDictionary payload, string key, int fallback = 0)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName StringNameField(GDictionary payload, string key)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static Variant ValueField(GDictionary payload, string key)
    {
        if (payload == null)
            return default;
        if (payload.ContainsKey(key))
            return payload[key];
        var stringNameKey = new StringName(key);
        return payload.ContainsKey(stringNameKey) ? payload[stringNameKey] : default;
    }

    private static GDictionary DuplicateDictionary(GDictionary source) =>
        source?.Duplicate(true) ?? new GDictionary();
}

public readonly record struct BattleSpellControlResult(
    bool SkipEffects,
    bool BacklashTriggered,
    bool FumbleProtected,
    int MpRefund,
    int ExtraMpDrained,
    GDictionary SpellControl
)
{
    public static BattleSpellControlResult None(GDictionary spellControl = null) =>
        new(false, false, false, 0, 0, DuplicateDictionary(spellControl));

    public static BattleSpellControlResult None(BattleSpellControlMetadata spellControl) =>
        None(spellControl?.ToDictionary());

    public static BattleSpellControlResult FromDictionary(GDictionary payload) =>
        None() with
        {
            SkipEffects = BoolField(payload, "skip_effects"),
            BacklashTriggered = BoolField(payload, "backlash_triggered"),
            FumbleProtected = BoolField(payload, "fumble_protected"),
            MpRefund = IntField(payload, "mp_refund"),
            ExtraMpDrained = IntField(payload, "extra_mp_drained"),
            SpellControl = DictionaryField(payload, "spell_control"),
        };

    public GDictionary ToDictionary() =>
        new()
        {
            ["skip_effects"] = SkipEffects,
            ["backlash_triggered"] = BacklashTriggered,
            ["fumble_protected"] = FumbleProtected,
            ["mp_refund"] = MpRefund,
            ["extra_mp_drained"] = ExtraMpDrained,
            ["spell_control"] = DuplicateDictionary(SpellControl),
        };

    private static GDictionary DuplicateDictionary(GDictionary source) =>
        source?.Duplicate(true) ?? new GDictionary();

    private static bool BoolField(GDictionary payload, string key, bool fallback = false)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int IntField(GDictionary payload, string key, int fallback = 0)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static GDictionary DictionaryField(GDictionary payload, string key)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary().Duplicate(true)
            : new GDictionary();
    }

    private static Variant ValueField(GDictionary payload, string key)
    {
        if (payload == null)
            return default;
        if (payload.ContainsKey(key))
            return payload[key];
        var stringNameKey = new StringName(key);
        return payload.ContainsKey(stringNameKey) ? payload[stringNameKey] : default;
    }
}

public readonly record struct BattleGroundBacklashTargetResult(
    IReadOnlyList<Vector2I> TargetCoords,
    bool BacklashTriggered,
    Vector2I OriginalTargetCoord,
    Vector2I ResolvedTargetCoord,
    Vector2I OffsetDelta,
    bool BacklashOffsetFallback
)
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    public static BattleGroundBacklashTargetResult None(IReadOnlyList<Vector2I> targetCoords = null) =>
        new(
            targetCoords ?? System.Array.Empty<Vector2I>(),
            false,
            InvalidCoord,
            InvalidCoord,
            Vector2I.Zero,
            false
        );

    public static BattleGroundBacklashTargetResult FromDictionary(GDictionary payload) =>
        new(
            Vector2IListField(payload, "target_coords"),
            BoolField(payload, "backlash_triggered"),
            Vector2IField(payload, "original_target_coord", InvalidCoord),
            Vector2IField(payload, "resolved_target_coord", InvalidCoord),
            Vector2IField(payload, "offset_delta", Vector2I.Zero),
            BoolField(payload, "backlash_offset_fallback")
        );

    public GVector2IArray TargetCoordsArray()
    {
        var result = new GVector2IArray();
        if (TargetCoords == null)
        {
            return result;
        }
        foreach (Vector2I coord in TargetCoords)
        {
            result.Add(coord);
        }
        return result;
    }

    public GDictionary ToDictionary() =>
        new()
        {
            ["target_coords"] = TargetCoordsArray(),
            ["backlash_triggered"] = BacklashTriggered,
            ["original_target_coord"] = OriginalTargetCoord,
            ["resolved_target_coord"] = ResolvedTargetCoord,
            ["offset_delta"] = OffsetDelta,
            ["backlash_offset_fallback"] = BacklashOffsetFallback,
        };

    private static List<Vector2I> Vector2IListField(GDictionary payload, string key)
    {
        var result = new List<Vector2I>();
        Variant value = ValueField(payload, key);
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant coordValue in value.AsGodotArray())
        {
            if (coordValue.VariantType == Variant.Type.Vector2I)
                result.Add(coordValue.AsVector2I());
        }
        return result;
    }

    private static bool BoolField(GDictionary payload, string key, bool fallback = false)
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Vector2I Vector2IField(
        GDictionary payload,
        string key,
        Vector2I fallback = default
    )
    {
        Variant value = ValueField(payload, key);
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static Variant ValueField(GDictionary payload, string key)
    {
        if (payload == null)
            return default;
        if (payload.ContainsKey(key))
            return payload[key];
        var stringNameKey = new StringName(key);
        return payload.ContainsKey(stringNameKey) ? payload[stringNameKey] : default;
    }
}

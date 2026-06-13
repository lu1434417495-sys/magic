using System.Collections.Generic;
using Godot;

public sealed record class BattleSpellControlMetadata
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
    public int EffectiveHitRoll { get; init; }
    public bool ReverseFateDowngraded { get; init; }
    public bool HasResolutionMetadata =>
        !IsEmpty(AttackResolution) || !IsEmpty(SpellControlResolution);

    public static BattleSpellControlMetadata Empty() => new();

    internal static BattleSpellControlMetadata FromDictionary(Godot.Collections.Dictionary payload)
    {
        return new BattleSpellControlMetadata
        {
            AttackResolution = StringNameField(payload, "attack_resolution"),
            SpellControlResolution = StringNameField(payload, "spell_control_resolution"),
            AttackSuccess = BoolField(payload, "attack_success"),
            CriticalHit = BoolField(payload, "critical_hit"),
            CriticalFail = BoolField(payload, "critical_fail"),
            OrdinaryMiss = BoolField(payload, "ordinary_miss"),
            IsDisadvantage = BoolField(payload, "is_disadvantage"),
            HiddenLuckAtBirth = IntField(payload, "hidden_luck_at_birth"),
            FaithLuckBonus = IntField(payload, "faith_luck_bonus"),
            EffectiveLuck = IntField(payload, "effective_luck"),
            CritLocked = BoolField(payload, "crit_locked"),
            CritGateDie = IntField(payload, "crit_gate_die"),
            CritGateRoll = IntField(payload, "crit_gate_roll"),
            HitRoll = IntField(payload, "hit_roll"),
            FumbleLowEnd = IntField(payload, "fumble_low_end"),
            CritThreshold = IntField(payload, "crit_threshold"),
            LockedSkillHitBonus = IntField(payload, "locked_skill_hit_bonus"),
            EffectiveHitRoll = IntField(payload, "effective_hit_roll"),
            ReverseFateDowngraded = BoolField(payload, "reverse_fate_downgraded"),
        };
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        if (!HasResolutionMetadata)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["attack_resolution"] = AttackResolution,
            ["spell_control_resolution"] = SpellControlResolution,
            ["attack_success"] = AttackSuccess,
            ["critical_hit"] = CriticalHit,
            ["critical_fail"] = CriticalFail,
            ["ordinary_miss"] = OrdinaryMiss,
            ["is_disadvantage"] = IsDisadvantage,
            ["hidden_luck_at_birth"] = HiddenLuckAtBirth,
            ["faith_luck_bonus"] = FaithLuckBonus,
            ["effective_luck"] = EffectiveLuck,
            ["crit_locked"] = CritLocked,
            ["crit_gate_die"] = CritGateDie,
            ["crit_gate_roll"] = CritGateRoll,
            ["hit_roll"] = HitRoll,
            ["fumble_low_end"] = FumbleLowEnd,
            ["crit_threshold"] = CritThreshold,
            ["locked_skill_hit_bonus"] = LockedSkillHitBonus,
            ["effective_hit_roll"] = EffectiveHitRoll,
            ["reverse_fate_downgraded"] = ReverseFateDowngraded,
            ["trait_trigger_results"] = new Godot.Collections.Array(),
        };
    }

    private static bool BoolField(
        Godot.Collections.Dictionary payload,
        string key,
        bool fallback = false
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsBool();
    }

    private static int IntField(
        Godot.Collections.Dictionary payload,
        string key,
        int fallback = 0
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsInt32();
    }

    private static StringName StringNameField(Godot.Collections.Dictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return new StringName("");
        return ProgressionDataUtils.to_string_name(payload[key]);
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";
}

public readonly record struct BattleSpellControlResult(
    bool SkipEffects,
    bool BacklashTriggered,
    bool FumbleProtected,
    int MpRefund,
    int ExtraMpDrained,
    BattleSpellControlMetadata SpellControl
)
{
    public static BattleSpellControlResult None(BattleSpellControlMetadata spellControl = null) =>
        new(false, false, false, 0, 0, spellControl ?? BattleSpellControlMetadata.Empty());

    internal static BattleSpellControlResult FromDictionary(Godot.Collections.Dictionary payload) =>
        None() with
        {
            SkipEffects = BoolField(payload, "skip_effects"),
            BacklashTriggered = BoolField(payload, "backlash_triggered"),
            FumbleProtected = BoolField(payload, "fumble_protected"),
            MpRefund = IntField(payload, "mp_refund"),
            ExtraMpDrained = IntField(payload, "extra_mp_drained"),
            SpellControl = BattleSpellControlMetadata.FromDictionary(
                DictionaryField(payload, "spell_control")
            ),
        };

    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["skip_effects"] = SkipEffects,
            ["backlash_triggered"] = BacklashTriggered,
            ["fumble_protected"] = FumbleProtected,
            ["mp_refund"] = MpRefund,
            ["extra_mp_drained"] = ExtraMpDrained,
            ["spell_control"] = SpellControl?.ToDictionary() ?? new Godot.Collections.Dictionary(),
        };

    private static bool BoolField(
        Godot.Collections.Dictionary payload,
        string key,
        bool fallback = false
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsBool();
    }

    private static int IntField(
        Godot.Collections.Dictionary payload,
        string key,
        int fallback = 0
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsInt32();
    }

    private static Godot.Collections.Dictionary DictionaryField(
        Godot.Collections.Dictionary payload,
        string key
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return new Godot.Collections.Dictionary();
        return payload[key].AsGodotDictionary();
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

    internal static BattleGroundBacklashTargetResult FromDictionary(
        Godot.Collections.Dictionary payload
    ) =>
        new(
            Vector2IListField(payload, "target_coords"),
            BoolField(payload, "backlash_triggered"),
            Vector2IField(payload, "original_target_coord", InvalidCoord),
            Vector2IField(payload, "resolved_target_coord", InvalidCoord),
            Vector2IField(payload, "offset_delta", Vector2I.Zero),
            BoolField(payload, "backlash_offset_fallback")
        );

    private Godot.Collections.Array<Vector2I> TargetCoordsArray()
    {
        var result = new Godot.Collections.Array<Vector2I>();
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

    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["target_coords"] = TargetCoordsArray(),
            ["backlash_triggered"] = BacklashTriggered,
            ["original_target_coord"] = OriginalTargetCoord,
            ["resolved_target_coord"] = ResolvedTargetCoord,
            ["offset_delta"] = OffsetDelta,
            ["backlash_offset_fallback"] = BacklashOffsetFallback,
        };

    private static List<Vector2I> Vector2IListField(
        Godot.Collections.Dictionary payload,
        string key
    )
    {
        var result = new List<Vector2I>();
        if (payload == null || !payload.ContainsKey(key))
            return result;
        var values = payload[key].AsGodotArray();
        foreach (var coordValue in values)
        {
            result.Add(coordValue.AsVector2I());
        }
        return result;
    }

    private static bool BoolField(
        Godot.Collections.Dictionary payload,
        string key,
        bool fallback = false
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsBool();
    }

    private static Vector2I Vector2IField(
        Godot.Collections.Dictionary payload,
        string key,
        Vector2I fallback = default
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return fallback;
        return payload[key].AsVector2I();
    }
}

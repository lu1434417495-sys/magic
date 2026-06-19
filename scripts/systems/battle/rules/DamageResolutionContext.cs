using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class DamageResolutionContext
{
    private static readonly StringName DefaultDamageRollMode = "random";

    public GDictionary RawContext { get; }
    public StringName DamageRollMode { get; }
    public bool CriticalHit { get; }
    public bool AttackSuccess { get; }
    public bool SecondaryHitSuccess { get; }
    public StringName SkillId { get; }
    public IReadOnlyList<int> SaveRollOverrides { get; }
    public bool DispatchEvents { get; }
    public StringName EquipmentSlotOverride { get; }

    private DamageResolutionContext(
        GDictionary rawContext,
        StringName damageRollMode,
        bool criticalHit,
        bool attackSuccess,
        bool secondaryHitSuccess,
        StringName skillId,
        IReadOnlyList<int> saveRollOverrides,
        bool dispatchEvents,
        StringName equipmentSlotOverride
    )
    {
        RawContext = rawContext?.Duplicate(false) ?? new GDictionary();
        DamageRollMode = damageRollMode == "" ? DefaultDamageRollMode : damageRollMode;
        CriticalHit = criticalHit;
        AttackSuccess = attackSuccess;
        SecondaryHitSuccess = secondaryHitSuccess;
        SkillId = skillId == default ? new StringName("") : skillId;
        SaveRollOverrides = saveRollOverrides ?? Array.Empty<int>();
        DispatchEvents = dispatchEvents;
        EquipmentSlotOverride =
            equipmentSlotOverride == default ? new StringName("") : equipmentSlotOverride;
    }

    public static DamageResolutionContext Empty() =>
        Create(false, false, false);

    public static DamageResolutionContext Create(
        bool criticalHit,
        bool attackSuccess,
        bool secondaryHitSuccess,
        StringName skillId = default,
        StringName damageRollMode = default,
        IReadOnlyList<int> saveRollOverrides = null,
        bool dispatchEvents = true,
        StringName equipmentSlotOverride = default
    )
    {
        GDictionary rawContext = new()
        {
            ["critical_hit"] = criticalHit,
            ["attack_success"] = attackSuccess,
            ["secondary_hit_success"] = secondaryHitSuccess,
            ["dispatch_events"] = dispatchEvents,
        };
        if (skillId != default && skillId != "")
            rawContext["skill_id"] = skillId;
        if (damageRollMode != default && damageRollMode != "")
            rawContext["damage_roll_mode"] = damageRollMode;
        if (equipmentSlotOverride != default && equipmentSlotOverride != "")
            rawContext["equipment_slot_override"] = equipmentSlotOverride;
        if (saveRollOverrides != null && saveRollOverrides.Count > 0)
        {
            var rolls = new GArray();
            foreach (int roll in saveRollOverrides)
                rolls.Add(Math.Clamp(roll, 1, 20));
            rawContext["save_roll_overrides"] = rolls;
        }

        return new DamageResolutionContext(
            rawContext,
            damageRollMode == default ? DefaultDamageRollMode : damageRollMode,
            criticalHit,
            attackSuccess,
            secondaryHitSuccess,
            skillId,
            saveRollOverrides ?? Array.Empty<int>(),
            dispatchEvents,
            equipmentSlotOverride
        );
    }

    public static DamageResolutionContext ForSkill(StringName skillId) =>
        Create(false, false, false, skillId: skillId);

    public static DamageResolutionContext FromDictionary(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return Empty();

        bool hasCriticalHit = payload.ContainsKey("critical_hit");
        bool hasAttackSuccess = payload.ContainsKey("attack_success");
        bool hasSecondaryHitSuccess = payload.ContainsKey("secondary_hit_success");
        if (hasSecondaryHitSuccess)
        {
            RequireBool(payload, "critical_hit");
            RequireBool(payload, "attack_success");
            RequireBool(payload, "secondary_hit_success");
        }
        else
        {
            if (hasCriticalHit)
                RequireBool(payload, "critical_hit");
            if (hasAttackSuccess)
                RequireBool(payload, "attack_success");
        }

        return new DamageResolutionContext(
            payload,
            ReadStringName(payload, "damage_roll_mode", DefaultDamageRollMode),
            ReadBool(payload, "critical_hit"),
            ReadBool(payload, "attack_success"),
            ReadBool(payload, "secondary_hit_success"),
            ReadStringName(payload, "skill_id"),
            ReadSaveRollOverrides(payload),
            ReadBool(payload, "dispatch_events", true),
            ReadStringName(payload, "equipment_slot_override")
        );
    }

    public DamageResolutionContext WithDamageRollMode(StringName damageRollMode)
    {
        GDictionary rawContext = RawContext.Duplicate(false);
        rawContext["damage_roll_mode"] = damageRollMode;
        return new DamageResolutionContext(
            rawContext,
            damageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            SaveRollOverrides,
            DispatchEvents,
            EquipmentSlotOverride
        );
    }

    public BattleSaveContext ToBattleSaveContext() =>
        new(SkillId, SaveRollOverrides ?? Array.Empty<int>());

    private static void RequireBool(GDictionary payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key].VariantType != Variant.Type.Bool)
            throw new ArgumentException($"damage_context.{key} must be an explicit bool");
    }

    private static bool ReadBool(GDictionary payload, string key, bool fallback = false)
    {
        if (!payload.ContainsKey(key))
            return fallback;
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static StringName ReadStringName(
        GDictionary payload,
        string key,
        StringName fallback = default
    )
    {
        if (!payload.ContainsKey(key))
            return fallback == default ? new StringName("") : fallback;
        Variant value = payload[key];
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            return fallback == default ? new StringName("") : fallback;
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized == "" && fallback != default ? fallback : normalized;
    }

    private static IReadOnlyList<int> ReadSaveRollOverrides(GDictionary payload)
    {
        if (payload.ContainsKey("save_roll_override"))
            return new[] { Math.Clamp(ReadInt(payload, "save_roll_override"), 1, 20) };
        if (!payload.ContainsKey("save_roll_overrides"))
            return Array.Empty<int>();
        Variant rawRolls = payload["save_roll_overrides"];
        if (rawRolls.VariantType != Variant.Type.Array)
            return Array.Empty<int>();

        GArray values = rawRolls.AsGodotArray();
        int[] rolls = new int[values.Count];
        for (int index = 0; index < values.Count; index++)
            rolls[index] = Math.Clamp(values[index].AsInt32(), 1, 20);
        return rolls;
    }

    private static int ReadInt(GDictionary payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key].VariantType != Variant.Type.Int)
            return 0;
        return payload[key].AsInt32();
    }
}

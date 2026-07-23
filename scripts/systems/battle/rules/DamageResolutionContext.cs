using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class DamageResolutionContext
{
    private static readonly StringName DefaultDamageRollMode = "random";

    public StringName DamageRollMode { get; }
    public bool CriticalHit { get; }
    public bool AttackSuccess { get; }
    public bool SecondaryHitSuccess { get; }
    public StringName SkillId { get; }
    public int SourceSkillLevel { get; }
    public IReadOnlyList<int> SaveRollOverrides { get; }
    public bool DispatchEvents { get; }
    public StringName EquipmentSlotOverride { get; }
    public BattleState BattleState { get; }
    internal BattleEventBatch DamageApplicationHookBatch { get; }
    internal BattleEffectOrigin DamageApplicationHookOrigin { get; }

    private DamageResolutionContext(
        StringName damageRollMode,
        bool criticalHit,
        bool attackSuccess,
        bool secondaryHitSuccess,
        StringName skillId,
        int sourceSkillLevel,
        IReadOnlyList<int> saveRollOverrides,
        bool dispatchEvents,
        StringName equipmentSlotOverride,
        BattleEventBatch damageApplicationHookBatch = null,
        BattleEffectOrigin damageApplicationHookOrigin = null,
        BattleState battleState = null
    )
    {
        DamageRollMode = damageRollMode == "" ? DefaultDamageRollMode : damageRollMode;
        CriticalHit = criticalHit;
        AttackSuccess = attackSuccess;
        SecondaryHitSuccess = secondaryHitSuccess;
        SkillId = skillId == default ? new StringName("") : skillId;
        SourceSkillLevel = Math.Max(sourceSkillLevel, 0);
        SaveRollOverrides = saveRollOverrides ?? Array.Empty<int>();
        DispatchEvents = dispatchEvents;
        EquipmentSlotOverride =
            equipmentSlotOverride == default ? new StringName("") : equipmentSlotOverride;
        BattleState = battleState;
        DamageApplicationHookBatch = damageApplicationHookBatch;
        DamageApplicationHookOrigin = damageApplicationHookOrigin ?? BattleEffectOrigin.PlayerCommand();
    }

    public static DamageResolutionContext Empty() =>
        Create(false, false, false);

    public static DamageResolutionContext Create(
        bool criticalHit,
        bool attackSuccess,
        bool secondaryHitSuccess,
        StringName skillId = default,
        int sourceSkillLevel = 0,
        StringName damageRollMode = default,
        IReadOnlyList<int> saveRollOverrides = null,
        bool dispatchEvents = true,
        StringName equipmentSlotOverride = default
    )
    {
        return new DamageResolutionContext(
            damageRollMode == default ? DefaultDamageRollMode : damageRollMode,
            criticalHit,
            attackSuccess,
            secondaryHitSuccess,
            skillId,
            sourceSkillLevel,
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
            ReadStringName(payload, "damage_roll_mode", DefaultDamageRollMode),
            ReadBool(payload, "critical_hit"),
            ReadBool(payload, "attack_success"),
            ReadBool(payload, "secondary_hit_success"),
            ReadStringName(payload, "skill_id"),
            ReadInt(payload, "source_skill_level") > 0
                ? ReadInt(payload, "source_skill_level")
                : ReadInt(payload, "skill_level"),
            ReadSaveRollOverrides(payload),
            ReadBool(payload, "dispatch_events", true),
            ReadStringName(payload, "equipment_slot_override")
        );
    }

    public DamageResolutionContext WithDamageRollMode(StringName damageRollMode)
    {
        return new DamageResolutionContext(
            damageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            SourceSkillLevel,
            SaveRollOverrides,
            DispatchEvents,
            EquipmentSlotOverride,
            DamageApplicationHookBatch,
            DamageApplicationHookOrigin,
            BattleState
        );
    }

    public DamageResolutionContext WithSourceSkillLevel(int sourceSkillLevel)
    {
        int normalizedLevel = Math.Max(sourceSkillLevel, 0);
        return new DamageResolutionContext(
            DamageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            normalizedLevel,
            SaveRollOverrides,
            DispatchEvents,
            EquipmentSlotOverride,
            DamageApplicationHookBatch,
            DamageApplicationHookOrigin,
            BattleState
        );
    }

    public DamageResolutionContext WithSaveRollOverrides(IReadOnlyList<int> saveRollOverrides)
    {
        int count = saveRollOverrides?.Count ?? 0;
        int[] normalizedRolls = new int[count];
        for (int index = 0; index < count; index++)
            normalizedRolls[index] = Math.Clamp(saveRollOverrides[index], 1, 20);
        return new DamageResolutionContext(
            DamageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            SourceSkillLevel,
            normalizedRolls,
            DispatchEvents,
            EquipmentSlotOverride,
            DamageApplicationHookBatch,
            DamageApplicationHookOrigin,
            BattleState
        );
    }

    public DamageResolutionContext WithBattleState(BattleState battleState)
    {
        return new DamageResolutionContext(
            DamageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            SourceSkillLevel,
            SaveRollOverrides,
            DispatchEvents,
            EquipmentSlotOverride,
            DamageApplicationHookBatch,
            DamageApplicationHookOrigin,
            battleState
        );
    }

    internal DamageResolutionContext WithDamageApplicationHookContext(
        BattleEventBatch batch,
        BattleEffectOrigin origin
    )
    {
        return new DamageResolutionContext(
            DamageRollMode,
            CriticalHit,
            AttackSuccess,
            SecondaryHitSuccess,
            SkillId,
            SourceSkillLevel,
            SaveRollOverrides,
            DispatchEvents,
            EquipmentSlotOverride,
            batch,
            origin ?? BattleEffectOrigin.PlayerCommand(),
            BattleState
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

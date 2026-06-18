using Godot;

public enum BattleLootDropKind
{
    Unknown = 0,
    Item,
    RandomEquipment,
    EquipmentInstance,
}

public enum BattleLootSourceKind
{
    Unknown = 0,
    EnemyUnit,
    CalamityConversion,
    FateStatusDrop,
    LowLuckEvent,
}

internal enum BattleLootSourceIdKind
{
    Unknown = 0,
    OrdinaryBattle,
    EliteBossBattle,
}

internal enum BattleLootSpecialItemKind
{
    Unknown = 0,
    CalamityShard,
    BlackCrownCore,
}

internal static class BattleLootIds
{
    private static readonly StringName DropTypeItem = "item";
    private static readonly StringName DropTypeRandomEquipment = "random_equipment";
    private static readonly StringName DropTypeEquipmentInstance = "equipment_instance";
    private static readonly StringName SourceKindEnemyUnit = "enemy_unit";
    private static readonly StringName SourceKindCalamityConversion = "calamity_conversion";
    private static readonly StringName SourceKindFateStatusDrop = "fate_status_drop";
    private static readonly StringName SourceKindLowLuckEvent = "low_luck_event";
    private static readonly StringName SourceIdOrdinaryBattle = "ordinary_battle";
    private static readonly StringName SourceIdEliteBossBattle = "elite_boss_battle";
    private static readonly StringName ItemCalamityShard = "calamity_shard";
    private static readonly StringName ItemBlackCrownCore = "black_crown_core";

    internal const int OrdinaryBattleCalamityShardChapterCap = 4;
    internal const string CalamityShardChapterFlagPrefix = "calamity_shard_chapter_slot_";

    internal static StringName ToStringName(BattleLootDropKind kind)
    {
        return kind switch
        {
            BattleLootDropKind.Item => DropTypeItem,
            BattleLootDropKind.RandomEquipment => DropTypeRandomEquipment,
            BattleLootDropKind.EquipmentInstance => DropTypeEquipmentInstance,
            _ => new StringName(""),
        };
    }

    internal static BattleLootDropKind ToDropKind(StringName value)
    {
        if (value == DropTypeItem)
            return BattleLootDropKind.Item;
        if (value == DropTypeRandomEquipment)
            return BattleLootDropKind.RandomEquipment;
        if (value == DropTypeEquipmentInstance)
            return BattleLootDropKind.EquipmentInstance;
        return BattleLootDropKind.Unknown;
    }

    internal static StringName ToStringName(BattleLootSourceKind kind)
    {
        return kind switch
        {
            BattleLootSourceKind.EnemyUnit => SourceKindEnemyUnit,
            BattleLootSourceKind.CalamityConversion => SourceKindCalamityConversion,
            BattleLootSourceKind.FateStatusDrop => SourceKindFateStatusDrop,
            BattleLootSourceKind.LowLuckEvent => SourceKindLowLuckEvent,
            _ => new StringName(""),
        };
    }

    internal static BattleLootSourceKind ToSourceKind(StringName value)
    {
        if (value == SourceKindEnemyUnit)
            return BattleLootSourceKind.EnemyUnit;
        if (value == SourceKindCalamityConversion)
            return BattleLootSourceKind.CalamityConversion;
        if (value == SourceKindFateStatusDrop)
            return BattleLootSourceKind.FateStatusDrop;
        if (value == SourceKindLowLuckEvent)
            return BattleLootSourceKind.LowLuckEvent;
        return BattleLootSourceKind.Unknown;
    }

    internal static StringName ToStringName(BattleLootSourceIdKind kind)
    {
        return kind switch
        {
            BattleLootSourceIdKind.OrdinaryBattle => SourceIdOrdinaryBattle,
            BattleLootSourceIdKind.EliteBossBattle => SourceIdEliteBossBattle,
            _ => new StringName(""),
        };
    }

    internal static BattleLootSourceIdKind ToSourceIdKind(StringName value)
    {
        if (value == SourceIdOrdinaryBattle)
            return BattleLootSourceIdKind.OrdinaryBattle;
        if (value == SourceIdEliteBossBattle)
            return BattleLootSourceIdKind.EliteBossBattle;
        return BattleLootSourceIdKind.Unknown;
    }

    internal static StringName ToStringName(BattleLootSpecialItemKind kind)
    {
        return kind switch
        {
            BattleLootSpecialItemKind.CalamityShard => ItemCalamityShard,
            BattleLootSpecialItemKind.BlackCrownCore => ItemBlackCrownCore,
            _ => new StringName(""),
        };
    }

    internal static BattleLootSpecialItemKind ToSpecialItemKind(StringName value)
    {
        if (value == ItemCalamityShard)
            return BattleLootSpecialItemKind.CalamityShard;
        if (value == ItemBlackCrownCore)
            return BattleLootSpecialItemKind.BlackCrownCore;
        return BattleLootSpecialItemKind.Unknown;
    }
}

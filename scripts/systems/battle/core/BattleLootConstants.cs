using Godot;

[GlobalClass]
public partial class BattleLootConstants : RefCounted
{
    public static StringName DROP_TYPE_ITEM() => "item";

    public static StringName DROP_TYPE_RANDOM_EQUIPMENT() => "random_equipment";

    public static StringName DROP_TYPE_EQUIPMENT_INSTANCE() => "equipment_instance";

    public static StringName SOURCE_KIND_ENEMY_UNIT() => "enemy_unit";

    public static StringName SOURCE_KIND_CALAMITY_CONVERSION() => "calamity_conversion";

    public static StringName SOURCE_KIND_FATE_STATUS_DROP() => "fate_status_drop";

    public static StringName SOURCE_KIND_LOW_LUCK_EVENT() => "low_luck_event";

    public static StringName SOURCE_ID_ORDINARY_BATTLE() => "ordinary_battle";

    public static StringName SOURCE_ID_ELITE_BOSS_BATTLE() => "elite_boss_battle";

    public static StringName ITEM_CALAMITY_SHARD() => "calamity_shard";

    public static StringName ITEM_BLACK_CROWN_CORE() => "black_crown_core";

    public static int ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP() => 4;

    public static string CALAMITY_SHARD_CHAPTER_FLAG_PREFIX() => "calamity_shard_chapter_slot_";
}

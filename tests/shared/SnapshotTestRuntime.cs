using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class SnapshotTestRuntime : IGameRuntimeSnapshotSource
{
    public PartyState PartyState { get; set; }
    public string ActiveModalId { get; set; } = "";
    public GDictionary ContractBoardWindowData { get; set; } = new();
    public GDictionary ForgeWindowData { get; set; } = new();
    public GDictionary ActiveShopContext { get; set; } = new();
    public GDictionary WarehouseWindowData { get; set; } = new();
    public GDictionary LastBattleLootSnapshot { get; set; } = new();
    public GDictionary GameOverContext { get; set; } = new();
    public BattleState BattleState { get; set; }
    public BattleRuntimeModule BattleRuntime { get; set; }
    public Vector2I BattleSelectedCoord { get; set; } = Vector2I.Zero;
    public StringName ActiveBattleEncounterId { get; set; } = "snapshot_test_anchor";
    public string ActiveBattleEncounterName { get; set; } = "快照测试遭遇";

    public bool is_battle_active() => BattleState != null && !BattleState.is_empty();

    public string get_status_text() => "";

    public string get_active_modal_id() => ActiveModalId;

    public GDictionary get_log_snapshot(int limit) => new();

    public GDictionary get_selected_settlement() => new();

    public GDictionary get_selected_world_npc() => new();

    public EncounterAnchorData get_selected_encounter_anchor() => null;

    public GDictionary get_selected_world_event() => new();

    public GArray get_nearby_encounter_entries(int limit) => new();

    public GArray get_nearby_world_event_entries(int limit) => new();

    public string get_active_map_id() => "";

    public string get_active_map_display_name() => "";

    public bool is_submap_active() => false;

    public int get_world_step() => 0;

    public Vector2I get_player_coord() => Vector2I.Zero;

    public bool is_player_visible_on_world_map() => false;

    public Vector2I get_selected_coord() => Vector2I.Zero;

    public GDictionary get_pending_submap_prompt() => new();

    public string get_submap_return_hint_text() => "";

    public GDictionary get_game_over_context() => GameOverContext.Duplicate(true);

    public PartyState get_party_state() => PartyState;

    public StringName get_party_selected_member_id() => "";

    public int get_pending_reward_count() => 0;

    public GDictionary get_member_achievement_summary(StringName member_id) => new();

    public AttributeSnapshot get_member_attribute_snapshot(StringName member_id) => null;

    public GArray get_member_equipped_entries(StringName member_id) => new();

    public string get_member_display_name(StringName member_id)
    {
        PartyMemberState memberState = PartyState?.get_member_state(member_id);
        return memberState != null ? memberState.display_name : "";
    }

    public string get_resolved_settlement_id() => "";

    public GDictionary get_settlement_window_data(string settlement_id) => new();

    public string get_settlement_feedback_text() => "";

    public GDictionary get_shop_window_data() => new();

    public GDictionary get_contract_board_window_data() =>
        ContractBoardWindowData.Duplicate(true);

    public GDictionary get_active_contract_board_context() =>
        ContractBoardWindowData.Duplicate(true);

    public GDictionary get_active_shop_context() => ActiveShopContext.Duplicate(true);

    public GDictionary get_forge_window_data() => ForgeWindowData.Duplicate(true);

    public GDictionary get_stagecoach_window_data() => new();

    public GDictionary get_character_info_context() => new();

    public string get_active_warehouse_entry_label() => "";

    public GDictionary get_warehouse_window_data() => WarehouseWindowData.Duplicate(true);

    public BattleState get_battle_state() => BattleState;

    public BattleRuntimeModule get_battle_runtime() => BattleRuntime;

    public Vector2I get_battle_selected_coord() => BattleSelectedCoord;

    public StringName get_selected_battle_skill_id() => "";

    public StringName get_selected_battle_skill_variant_id() => "";

    public string get_selected_battle_skill_name() => "";

    public string get_selected_battle_skill_variant_name() => "";

    public GVector2IArray get_selected_battle_skill_target_coords() => new();

    public GStringNameArray get_selected_battle_skill_target_unit_ids() => new();

    public int get_selected_battle_skill_required_coord_count() => 0;

    public StringName get_active_battle_encounter_id() => ActiveBattleEncounterId;

    public string get_active_battle_encounter_name() => ActiveBattleEncounterName;

    public string get_battle_active_unit_name()
    {
        if (BattleState == null || !BattleState.units.ContainsKey(BattleState.active_unit_id))
            return "";
        BattleUnitState activeUnit =
            BattleState.units[BattleState.active_unit_id].AsGodotObject() as BattleUnitState;
        return activeUnit != null ? activeUnit.display_name : "";
    }

    public GDictionary get_pending_battle_start_prompt() => new();

    public GDictionary get_battle_terrain_counts() => new();

    public PendingCharacterReward get_snapshot_reward() => null;

    public GDictionary get_last_battle_loot_snapshot() =>
        LastBattleLootSnapshot.Duplicate(true);

    public GDictionary get_current_promotion_prompt() => new();

    public GameSession get_game_session() => null;
}

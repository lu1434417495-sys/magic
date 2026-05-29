using Godot;

[GlobalClass]
public partial class SettlementServiceContext : RefCounted
{
    public Godot.Collections.Dictionary settlement_record = new();
    public Godot.Collections.Dictionary facility_record = new();
    public Godot.Collections.Dictionary npc_record = new();
    public PartyState party_state;
    public GodotObject warehouse_service;
    public GodotObject character_management;
    public int world_step;
    public StringName member_id = "";
    public Godot.Collections.Dictionary payload = new();

    public SettlementServiceContext DuplicateContext()
    {
        var clone = new SettlementServiceContext();
        clone.settlement_record = settlement_record.Duplicate(true);
        clone.facility_record = facility_record.Duplicate(true);
        clone.npc_record = npc_record.Duplicate(true);
        clone.party_state = party_state;
        clone.warehouse_service = warehouse_service;
        clone.character_management = character_management;
        clone.world_step = world_step;
        clone.member_id = member_id;
        clone.payload = payload.Duplicate(true);
        return clone;
    }
}

class_name SettlementServiceContext
extends RefCounted

var settlement_record: Dictionary = {}
var facility_record: Dictionary = {}
var npc_record: Dictionary = {}
var party_state = null
var warehouse_service = null
var character_management = null
var world_step := 0
var member_id: StringName = &""
var payload: Dictionary = {}


func duplicate_context() -> SettlementServiceContext:
	var clone := SettlementServiceContext.new()
	clone.settlement_record = settlement_record.duplicate(true)
	clone.facility_record = facility_record.duplicate(true)
	clone.npc_record = npc_record.duplicate(true)
	clone.party_state = party_state
	clone.warehouse_service = warehouse_service
	clone.character_management = character_management
	clone.world_step = world_step
	clone.member_id = member_id
	clone.payload = payload.duplicate(true)
	return clone

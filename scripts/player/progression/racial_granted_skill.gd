class_name RacialGrantedSkill
extends Resource

const CHARGE_KIND_AT_WILL: StringName = &"at_will"
const CHARGE_KIND_PER_BATTLE: StringName = &"per_battle"
const CHARGE_KIND_PER_TURN: StringName = &"per_turn"

const VALID_CHARGE_KINDS: Array[StringName] = [
	CHARGE_KIND_AT_WILL,
	CHARGE_KIND_PER_BATTLE,
	CHARGE_KIND_PER_TURN,
]

@export var skill_id: StringName = &""
@export var minimum_skill_level: int = 1
@export var grant_level: int = 1
@export var charge_kind: StringName = CHARGE_KIND_PER_BATTLE
@export var charges: int = 1

class_name WeaponDamageDiceDef
extends Resource

const SCRIPT = preload("res://scripts/player/warehouse/weapon_damage_dice_def.gd")

@export_range(1, 99, 1) var dice_count := 1
@export_range(1, 999, 1) var dice_sides := 6
@export_range(-999, 999, 1) var flat_bonus := 0


func duplicate_dice() -> WeaponDamageDiceDef:
	var copy: WeaponDamageDiceDef = SCRIPT.new()
	copy.dice_count = get_dice_count()
	copy.dice_sides = get_dice_sides()
	copy.flat_bonus = int(flat_bonus)
	return copy


func get_dice_count() -> int:
	return maxi(int(dice_count), 1)


func get_dice_sides() -> int:
	return maxi(int(dice_sides), 1)


func to_roll_label() -> String:
	var label := "%dD%d" % [get_dice_count(), get_dice_sides()]
	var bonus := int(flat_bonus)
	if bonus > 0:
		label += "+%d" % bonus
	elif bonus < 0:
		label += "%d" % bonus
	return label

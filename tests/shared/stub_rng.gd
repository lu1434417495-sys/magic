extends RefCounted

var _rolls: Array[int] = []
var call_count := 0


func _init(rolls: Array = []) -> void:
	reset(rolls)


func reset(rolls: Array = []) -> void:
	_rolls.clear()
	for roll in rolls:
		_rolls.append(int(roll))
	call_count = 0


func randi_range(min_value: int, max_value: int) -> int:
	var lower := mini(min_value, max_value)
	var upper := maxi(min_value, max_value)
	if call_count >= _rolls.size():
		call_count += 1
		return lower
	var roll := clampi(int(_rolls[call_count]), lower, upper)
	call_count += 1
	return roll


func remaining_count() -> int:
	return maxi(_rolls.size() - call_count, 0)

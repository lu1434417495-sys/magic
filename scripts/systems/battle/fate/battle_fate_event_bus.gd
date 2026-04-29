## 文件说明：该脚本属于战斗命运事件总线相关的辅助脚本，集中维护攻击结算后派发的 fate event 信号与只读 payload 约束。
## 审查重点：重点核对事件类型常量、payload 只读复制、以及后续订阅方拿不到可变运行时引用。
## 备注：后续 Fortuna / Misfortune / 剧情系统应通过订阅该总线读取 payload，而不是直接改写 battle resolver 内部状态。

class_name BattleFateEventBus
extends RefCounted

const EVENT_CRITICAL_FAIL: StringName = &"critical_fail"
const EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE: StringName = &"critical_success_under_disadvantage"
const EVENT_HIGH_THREAT_CRITICAL_HIT: StringName = &"high_threat_critical_hit"
const EVENT_ORDINARY_MISS: StringName = &"ordinary_miss"
const EVENT_HARDSHIP_SURVIVAL: StringName = &"hardship_survival"

signal event_dispatched(event_type: StringName, payload: Dictionary)


func dispatch(event_type: StringName, payload: Dictionary = {}) -> void:
	if event_type == &"":
		return
	var readonly_payload := _make_variant_read_only(payload) as Dictionary
	event_dispatched.emit(event_type, readonly_payload)


func _make_variant_read_only(value):
	if value is Dictionary:
		var readonly_dict: Dictionary = {}
		for key in (value as Dictionary).keys():
			readonly_dict[key] = _make_variant_read_only((value as Dictionary).get(key))
		readonly_dict.make_read_only()
		return readonly_dict
	if value is Array:
		var readonly_array: Array = []
		for entry in value:
			readonly_array.append(_make_variant_read_only(entry))
		readonly_array.make_read_only()
		return readonly_array
	return value

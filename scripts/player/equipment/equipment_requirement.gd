## 文件说明：装备资格校验资源，声明装备对角色职业、体型的要求。Phase 2 首版。
## 审查重点：check() 必须只读成员状态，不允许修改任何状态。
## 备注：属性阈值校验（required_attribute_rules）预留但不在 Phase 2 实现，后续按需补充。

class_name EquipmentRequirement
extends Resource

## 字段说明：允许装备的职业 ID 列表；非空时成员必须已获得其中至少一个职业。
@export var required_profession_ids: Array[String] = []
## 字段说明：允许装备的最小体型；0 表示无限制。
@export_range(0, 99, 1) var min_body_size: int = 0
## 字段说明：允许装备的最大体型；0 表示无限制。
@export_range(0, 99, 1) var max_body_size: int = 0


## 对目标成员执行资格校验，返回 { allowed: bool, blockers: Array[String] }。
## 不修改任何状态；blockers 使用稳定错误码，便于 headless 断言。
func check(member_state) -> Dictionary:
	var blockers: Array[String] = []

	if not required_profession_ids.is_empty():
		var has_profession := false
		for raw_id in required_profession_ids:
			var prof_id := ProgressionDataUtils.to_string_name(raw_id)
			if (
				member_state != null
				and member_state.progression != null
				and member_state.progression.get_profession_progress(prof_id) != null
			):
				has_profession = true
				break
		if not has_profession:
			blockers.append("missing_profession")

	if min_body_size > 0 and (member_state == null or int(member_state.body_size) < min_body_size):
		blockers.append("body_size_too_small")

	if max_body_size > 0 and (member_state == null or int(member_state.body_size) > max_body_size):
		blockers.append("body_size_too_large")

	return {
		"allowed": blockers.is_empty(),
		"blockers": blockers,
	}

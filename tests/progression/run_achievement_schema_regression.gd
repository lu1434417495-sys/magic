extends SceneTree

const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_round_trips()
	_test_achievement_def_rejects_schema_defaults()
	_test_achievement_reward_def_rejects_schema_defaults()
	_test_achievement_def_accepts_empty_rewards_array()

	if _failures.is_empty():
		print("Achievement schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Achievement schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_round_trips() -> void:
	var reward := _build_valid_reward()
	var restored_reward = AchievementRewardDef.from_dict(reward.to_dict())
	_assert_true(restored_reward != null, "AchievementRewardDef valid to_dict payload should round-trip.")
	if restored_reward != null:
		_assert_eq(restored_reward.reward_type, reward.reward_type, "AchievementRewardDef should preserve reward_type.")
		_assert_eq(restored_reward.target_id, reward.target_id, "AchievementRewardDef should preserve target_id.")
		_assert_eq(restored_reward.amount, reward.amount, "AchievementRewardDef should preserve amount.")

	var achievement := _build_valid_achievement()
	var restored_achievement = AchievementDef.from_dict(achievement.to_dict())
	_assert_true(restored_achievement != null, "AchievementDef valid to_dict payload should round-trip.")
	if restored_achievement == null:
		return
	_assert_eq(restored_achievement.achievement_id, achievement.achievement_id, "AchievementDef should preserve achievement_id.")
	_assert_eq(restored_achievement.event_type, achievement.event_type, "AchievementDef should preserve event_type.")
	_assert_eq(restored_achievement.threshold, achievement.threshold, "AchievementDef should preserve threshold.")
	_assert_eq(restored_achievement.rewards.size(), 1, "AchievementDef should preserve rewards.")
	if not restored_achievement.rewards.is_empty():
		_assert_eq(restored_achievement.rewards[0].target_id, reward.target_id, "AchievementDef should preserve nested reward payload.")


func _test_achievement_def_rejects_schema_defaults() -> void:
	_assert_true(AchievementDef.from_dict("not a dictionary") == null, "AchievementDef.from_dict should reject non-Dictionary payloads.")

	var missing_threshold := _build_valid_achievement_payload()
	missing_threshold.erase("threshold")
	_assert_true(AchievementDef.from_dict(missing_threshold) == null, "AchievementDef should reject payloads missing threshold.")

	var extra_field := _build_valid_achievement_payload()
	extra_field["legacy_subject"] = "charge"
	_assert_true(AchievementDef.from_dict(extra_field) == null, "AchievementDef should reject payloads with non-current fields.")

	var wrong_rewards := _build_valid_achievement_payload()
	wrong_rewards["rewards"] = {}
	_assert_true(AchievementDef.from_dict(wrong_rewards) == null, "AchievementDef should reject non-Array rewards.")

	var invalid_reward_entry := _build_valid_achievement_payload()
	var reward_payload := _build_valid_reward_payload()
	reward_payload.erase("target_id")
	invalid_reward_entry["rewards"] = [reward_payload]
	_assert_true(AchievementDef.from_dict(invalid_reward_entry) == null, "AchievementDef should reject invalid reward entries.")

	var empty_event_type := _build_valid_achievement_payload()
	empty_event_type["event_type"] = ""
	_assert_true(AchievementDef.from_dict(empty_event_type) == null, "AchievementDef should reject empty event_type.")

	var bad_subject_id := _build_valid_achievement_payload()
	bad_subject_id["subject_id"] = null
	_assert_true(AchievementDef.from_dict(bad_subject_id) == null, "AchievementDef should reject non-string subject_id.")


func _test_achievement_reward_def_rejects_schema_defaults() -> void:
	_assert_true(AchievementRewardDef.from_dict("not a dictionary") == null, "AchievementRewardDef.from_dict should reject non-Dictionary payloads.")

	var missing_target_id := _build_valid_reward_payload()
	missing_target_id.erase("target_id")
	_assert_true(AchievementRewardDef.from_dict(missing_target_id) == null, "AchievementRewardDef should reject payloads missing target_id.")

	var extra_field := _build_valid_reward_payload()
	extra_field["legacy_amount"] = 1
	_assert_true(AchievementRewardDef.from_dict(extra_field) == null, "AchievementRewardDef should reject payloads with non-current fields.")

	var string_amount := _build_valid_reward_payload()
	string_amount["amount"] = "1"
	_assert_true(AchievementRewardDef.from_dict(string_amount) == null, "AchievementRewardDef should reject string amount.")

	var empty_amount := _build_valid_reward_payload()
	empty_amount["amount"] = 0
	_assert_true(AchievementRewardDef.from_dict(empty_amount) == null, "AchievementRewardDef should reject zero amount.")


func _test_achievement_def_accepts_empty_rewards_array() -> void:
	var payload := _build_valid_achievement_payload()
	payload["rewards"] = []
	var restored = AchievementDef.from_dict(payload)
	_assert_true(restored != null, "AchievementDef should accept explicit empty rewards array.")
	if restored != null:
		_assert_true(restored.rewards.is_empty(), "AchievementDef should preserve empty rewards array.")


func _build_valid_achievement() -> AchievementDef:
	var achievement := AchievementDef.new()
	achievement.achievement_id = &"schema_round_trip"
	achievement.display_name = "Schema Round Trip"
	achievement.description = "Valid achievement schema payload."
	achievement.event_type = &"skill_learned"
	achievement.subject_id = &"charge"
	achievement.threshold = 1
	achievement.rewards.append(_build_valid_reward())
	return achievement


func _build_valid_reward() -> AchievementRewardDef:
	var reward := AchievementRewardDef.new()
	reward.reward_type = AchievementRewardDef.TYPE_SKILL_UNLOCK
	reward.target_id = &"charge"
	reward.target_label = "Charge"
	reward.amount = 1
	reward.reason_text = "Schema reward."
	return reward


func _build_valid_achievement_payload() -> Dictionary:
	return _build_valid_achievement().to_dict()


func _build_valid_reward_payload() -> Dictionary:
	return _build_valid_reward().to_dict()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])

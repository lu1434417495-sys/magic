class_name BodySizeRules
extends RefCounted

const BODY_SIZE_CONTENT_RULES = preload("res://scripts/player/progression/body_size_content_rules.gd")

const BODY_SIZE_CATEGORY_TINY: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_TINY
const BODY_SIZE_CATEGORY_SMALL: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_SMALL
const BODY_SIZE_CATEGORY_MEDIUM: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_MEDIUM
const BODY_SIZE_CATEGORY_LARGE: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_LARGE
const BODY_SIZE_CATEGORY_HUGE: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_HUGE
const BODY_SIZE_CATEGORY_GARGANTUAN: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_GARGANTUAN
const BODY_SIZE_CATEGORY_BOSS: StringName = BODY_SIZE_CONTENT_RULES.BODY_SIZE_CATEGORY_BOSS

const BODY_SIZE_TINY := BODY_SIZE_CONTENT_RULES.BODY_SIZE_TINY
const BODY_SIZE_SMALL := BODY_SIZE_CONTENT_RULES.BODY_SIZE_SMALL
const BODY_SIZE_MEDIUM := BODY_SIZE_CONTENT_RULES.BODY_SIZE_MEDIUM
const BODY_SIZE_LARGE := BODY_SIZE_CONTENT_RULES.BODY_SIZE_LARGE
const BODY_SIZE_HUGE := BODY_SIZE_CONTENT_RULES.BODY_SIZE_HUGE
const BODY_SIZE_GARGANTUAN := BODY_SIZE_CONTENT_RULES.BODY_SIZE_GARGANTUAN
const BODY_SIZE_BOSS := BODY_SIZE_CONTENT_RULES.BODY_SIZE_BOSS

const VALID_BODY_SIZE_CATEGORIES := BODY_SIZE_CONTENT_RULES.VALID_BODY_SIZE_CATEGORIES
const VALID_BODY_SIZES := BODY_SIZE_CONTENT_RULES.VALID_BODY_SIZES
const CATEGORY_TO_BODY_SIZE := BODY_SIZE_CONTENT_RULES.CATEGORY_TO_BODY_SIZE
const BODY_SIZE_TO_CATEGORY := BODY_SIZE_CONTENT_RULES.BODY_SIZE_TO_CATEGORY
const BODY_SIZE_TO_FOOTPRINT := BODY_SIZE_CONTENT_RULES.BODY_SIZE_TO_FOOTPRINT


static func is_valid_body_size_category(category: StringName) -> bool:
	return BODY_SIZE_CONTENT_RULES.is_valid_body_size_category(category)


static func is_valid_body_size(body_size: int) -> bool:
	return BODY_SIZE_CONTENT_RULES.is_valid_body_size(body_size)


static func get_body_size_for_category(category: StringName) -> int:
	return BODY_SIZE_CONTENT_RULES.get_body_size_for_category(category)


static func get_category_for_body_size(body_size: int) -> StringName:
	return BODY_SIZE_CONTENT_RULES.get_category_for_body_size(body_size)


static func body_size_matches_category(category: StringName, body_size: int) -> bool:
	return BODY_SIZE_CONTENT_RULES.body_size_matches_category(category, body_size)


static func get_footprint_for_body_size(body_size: int) -> Vector2i:
	return BODY_SIZE_CONTENT_RULES.get_footprint_for_body_size(body_size)


static func get_footprint_for_category(category: StringName) -> Vector2i:
	return BODY_SIZE_CONTENT_RULES.get_footprint_for_category(category)

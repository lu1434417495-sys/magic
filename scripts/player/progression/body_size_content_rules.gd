class_name BodySizeContentRules
extends RefCounted

const BODY_SIZE_CATEGORY_TINY: StringName = &"tiny"
const BODY_SIZE_CATEGORY_SMALL: StringName = &"small"
const BODY_SIZE_CATEGORY_MEDIUM: StringName = &"medium"
const BODY_SIZE_CATEGORY_LARGE: StringName = &"large"
const BODY_SIZE_CATEGORY_HUGE: StringName = &"huge"
const BODY_SIZE_CATEGORY_GARGANTUAN: StringName = &"gargantuan"
const BODY_SIZE_CATEGORY_BOSS: StringName = &"boss"

const BODY_SIZE_TINY := 1
const BODY_SIZE_SMALL := 1
const BODY_SIZE_MEDIUM := 2
const BODY_SIZE_LARGE := 3
const BODY_SIZE_HUGE := 4
const BODY_SIZE_GARGANTUAN := 5
const BODY_SIZE_BOSS := 6

const VALID_BODY_SIZE_CATEGORIES := {
	BODY_SIZE_CATEGORY_TINY: true,
	BODY_SIZE_CATEGORY_SMALL: true,
	BODY_SIZE_CATEGORY_MEDIUM: true,
	BODY_SIZE_CATEGORY_LARGE: true,
	BODY_SIZE_CATEGORY_HUGE: true,
	BODY_SIZE_CATEGORY_GARGANTUAN: true,
	BODY_SIZE_CATEGORY_BOSS: true,
}

const VALID_BODY_SIZES := {
	BODY_SIZE_TINY: true,
	BODY_SIZE_MEDIUM: true,
	BODY_SIZE_LARGE: true,
	BODY_SIZE_HUGE: true,
	BODY_SIZE_GARGANTUAN: true,
	BODY_SIZE_BOSS: true,
}

const CATEGORY_TO_BODY_SIZE := {
	BODY_SIZE_CATEGORY_TINY: BODY_SIZE_TINY,
	BODY_SIZE_CATEGORY_SMALL: BODY_SIZE_SMALL,
	BODY_SIZE_CATEGORY_MEDIUM: BODY_SIZE_MEDIUM,
	BODY_SIZE_CATEGORY_LARGE: BODY_SIZE_LARGE,
	BODY_SIZE_CATEGORY_HUGE: BODY_SIZE_HUGE,
	BODY_SIZE_CATEGORY_GARGANTUAN: BODY_SIZE_GARGANTUAN,
	BODY_SIZE_CATEGORY_BOSS: BODY_SIZE_BOSS,
}

const BODY_SIZE_TO_CATEGORY := {
	BODY_SIZE_SMALL: BODY_SIZE_CATEGORY_SMALL,
	BODY_SIZE_MEDIUM: BODY_SIZE_CATEGORY_MEDIUM,
	BODY_SIZE_LARGE: BODY_SIZE_CATEGORY_LARGE,
	BODY_SIZE_HUGE: BODY_SIZE_CATEGORY_HUGE,
	BODY_SIZE_GARGANTUAN: BODY_SIZE_CATEGORY_GARGANTUAN,
	BODY_SIZE_BOSS: BODY_SIZE_CATEGORY_BOSS,
}

const BODY_SIZE_TO_FOOTPRINT := {
	BODY_SIZE_TINY: Vector2i.ONE,
	BODY_SIZE_MEDIUM: Vector2i.ONE,
	BODY_SIZE_LARGE: Vector2i(2, 2),
	BODY_SIZE_HUGE: Vector2i(2, 2),
	BODY_SIZE_GARGANTUAN: Vector2i(3, 3),
	BODY_SIZE_BOSS: Vector2i(3, 3),
}


static func is_valid_body_size_category(category: StringName) -> bool:
	return VALID_BODY_SIZE_CATEGORIES.has(category)


static func is_valid_body_size(body_size: int) -> bool:
	return VALID_BODY_SIZES.has(int(body_size))


static func get_body_size_for_category(category: StringName) -> int:
	return int(CATEGORY_TO_BODY_SIZE.get(category, 0))


static func get_category_for_body_size(body_size: int) -> StringName:
	return BODY_SIZE_TO_CATEGORY.get(int(body_size), BODY_SIZE_CATEGORY_MEDIUM)


static func body_size_matches_category(category: StringName, body_size: int) -> bool:
	return is_valid_body_size_category(category) and get_body_size_for_category(category) == int(body_size)


static func get_footprint_for_body_size(body_size: int) -> Vector2i:
	return BODY_SIZE_TO_FOOTPRINT.get(int(body_size), Vector2i.ONE)


static func get_footprint_for_category(category: StringName) -> Vector2i:
	return get_footprint_for_body_size(get_body_size_for_category(category))

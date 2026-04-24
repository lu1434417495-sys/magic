## 文件说明：建卡 reroll 分布仿真脚本，用来验证骰式产生的属性分布是否符合设计预期。
## 审查重点：参数化骰式常量，输出单属性分布、总和分布、单次命中率与累积 reroll 命中率。
## 备注：非回归用例，纯离线仿真工具；当前用精确枚举替代随机抽样，避免极端尾部样本不稳定。
## 运行方式：godot --headless --script tools/character_creation_reroll_simulation.gd

extends SceneTree

## 属性项数（力/敏/体/感/智/意）
const STAT_COUNT: int = 6

## 骰式配置：每项属性 = max(DICE_VALUE_FLOOR, DICE_PER_STAT 个 1-DICE_SIDES 骰子求和 + DICE_OFFSET)
## 当前建卡 5d3-1：最低属性 4，最高属性 14。
const DICE_PER_STAT: int = 5
const DICE_SIDES: int = 3
const DICE_OFFSET: int = -1
const DICE_VALUE_FLOOR: int = 4

## 总和阈值按 q≈1/N 对齐 100 ~ 10,000,000 reroll 档。
const TOTAL_THRESHOLDS: Array[int] = [65, 68, 71, 73, 75, 76]

## UI 自动 reroll 的主要玩法：六项属性都达到该下限。
const ALL_STAT_MIN_THRESHOLDS: Array[int] = [9, 10, 11, 12, 13, 14]

## 要观测累积命中率的 reroll 次数。
const REROLL_COUNTS: Array[int] = [10, 100, 1000, 10000, 100000, 1000000, 10000000]


func _initialize() -> void:
	var start_time := Time.get_ticks_msec()
	var stat_histogram := _build_single_stat_histogram()
	var total_histogram := _build_total_histogram(stat_histogram)
	var elapsed_ms := Time.get_ticks_msec() - start_time

	var stat_outcomes := _sum_counts(stat_histogram)
	var total_outcomes := _sum_counts(total_histogram)
	var stat_min := _min_key(stat_histogram)
	var stat_max := _max_key(stat_histogram)

	print("========================================")
	print("  建卡 reroll 精确分布")
	print("========================================")
	print("骰式：每项 %s（范围 %d-%d），%d 项" % [
		_dice_label(),
		stat_min,
		stat_max,
		STAT_COUNT,
	])
	print("精确枚举结果数：单属性 %s，六属性组合 %s" % [
		_format_int(stat_outcomes),
		_format_int(total_outcomes),
	])
	print("耗时：%d ms" % elapsed_ms)
	print("")

	_print_single_stat_distribution(stat_histogram, stat_outcomes)
	_print_total_distribution(total_histogram, total_outcomes)
	_print_total_threshold_probabilities(total_histogram, total_outcomes)
	_print_all_stat_min_probabilities(stat_histogram, stat_outcomes)
	_print_cumulative_total_thresholds(total_histogram, total_outcomes)
	_print_cumulative_all_stat_minimums(stat_histogram, stat_outcomes)
	_print_design_summary()

	quit(0)


func _build_single_stat_histogram() -> Dictionary:
	var histogram: Dictionary = {0: 1}
	for _die in DICE_PER_STAT:
		var next_histogram: Dictionary = {}
		for subtotal_key in histogram.keys():
			var subtotal := int(subtotal_key)
			var subtotal_count := int(histogram[subtotal_key])
			for face in range(1, DICE_SIDES + 1):
				_add_count(next_histogram, subtotal + face, subtotal_count)
		histogram = next_histogram

	var transformed: Dictionary = {}
	for raw_key in histogram.keys():
		var stat_value := maxi(DICE_VALUE_FLOOR, int(raw_key) + DICE_OFFSET)
		_add_count(transformed, stat_value, int(histogram[raw_key]))
	return transformed


func _build_total_histogram(stat_histogram: Dictionary) -> Dictionary:
	var total_histogram: Dictionary = {0: 1}
	for _stat_idx in STAT_COUNT:
		total_histogram = _convolve_histograms(total_histogram, stat_histogram)
	return total_histogram


func _convolve_histograms(left: Dictionary, right: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for left_key in left.keys():
		var left_value := int(left_key)
		var left_count := int(left[left_key])
		for right_key in right.keys():
			var right_value := int(right_key)
			var right_count := int(right[right_key])
			_add_count(result, left_value + right_value, left_count * right_count)
	return result


func _add_count(histogram: Dictionary, key: int, count: int) -> void:
	histogram[key] = int(histogram.get(key, 0)) + count


func _print_single_stat_distribution(stat_histogram: Dictionary, stat_outcomes: int) -> void:
	print("--- 单属性分布 ---")
	var sorted_stat_keys: Array = stat_histogram.keys()
	sorted_stat_keys.sort()
	for key in sorted_stat_keys:
		var pct := 100.0 * float(int(stat_histogram[key])) / float(stat_outcomes)
		print("  值 %2d: %8.4f%%  (%s)" % [int(key), pct, _format_int(int(stat_histogram[key]))])
	print("")


func _print_total_distribution(total_histogram: Dictionary, total_outcomes: int) -> void:
	var summary := _calculate_histogram_summary(total_histogram)
	print("--- 总和分布 ---")
	print("  min=%d  max=%d  mean=%.3f  std=%.3f" % [
		int(summary.get("min", 0)),
		int(summary.get("max", 0)),
		float(summary.get("mean", 0.0)),
		float(summary.get("std", 0.0)),
	])
	print("")

	var sorted_total_keys: Array = total_histogram.keys()
	sorted_total_keys.sort()
	var max_pct := 0.0
	for k in sorted_total_keys:
		var p := 100.0 * float(int(total_histogram[k])) / float(total_outcomes)
		if p > max_pct:
			max_pct = p
	for key in sorted_total_keys:
		var pct := 100.0 * float(int(total_histogram[key])) / float(total_outcomes)
		var bar_len := 0
		if max_pct > 0.0:
			bar_len = int((pct / max_pct) * 48.0)
		var bar := "#".repeat(bar_len)
		var count_str := _format_int(int(total_histogram[key]))
		print("  总和 %3d: %9.5f%%  (%s)  %s" % [int(key), pct, count_str, bar])
	print("")


func _print_total_threshold_probabilities(total_histogram: Dictionary, total_outcomes: int) -> void:
	print("--- 单次 roll 命中总和阈值的概率 q ---")
	for threshold in TOTAL_THRESHOLDS:
		var q := float(_tail_count(total_histogram, threshold)) / float(total_outcomes)
		print("  总和 ≥ %2d: q = %.9f  (%s)" % [threshold, q, _format_one_in_n(q)])
	print("")


func _print_all_stat_min_probabilities(stat_histogram: Dictionary, stat_outcomes: int) -> void:
	print("--- 单次 roll 命中“六项均达到下限”的概率 q ---")
	for threshold in ALL_STAT_MIN_THRESHOLDS:
		var single_q := float(_tail_count(stat_histogram, threshold)) / float(stat_outcomes)
		var q := pow(single_q, STAT_COUNT)
		print("  六项均 ≥ %2d: q = %.9f  (%s)" % [threshold, q, _format_one_in_n(q)])
	print("")


func _print_cumulative_total_thresholds(total_histogram: Dictionary, total_outcomes: int) -> void:
	print("--- 累积命中率：N 次 reroll 内至少一次命中总和阈值 ---")
	_print_cumulative_header()
	for threshold in TOTAL_THRESHOLDS:
		var q := float(_tail_count(total_histogram, threshold)) / float(total_outcomes)
		var line := "  总和≥%2d    " % threshold
		for n in REROLL_COUNTS:
			line += "  %8.3f%%" % (_cumulative_hit_rate(q, n) * 100.0)
		print(line)
	print("")


func _print_cumulative_all_stat_minimums(stat_histogram: Dictionary, stat_outcomes: int) -> void:
	print("--- 累积命中率：N 次 reroll 内至少一次命中六项下限 ---")
	_print_cumulative_header()
	for threshold in ALL_STAT_MIN_THRESHOLDS:
		var single_q := float(_tail_count(stat_histogram, threshold)) / float(stat_outcomes)
		var q := pow(single_q, STAT_COUNT)
		var line := "  六项≥%2d    " % threshold
		for n in REROLL_COUNTS:
			line += "  %8.3f%%" % (_cumulative_hit_rate(q, n) * 100.0)
		print(line)
	print("")


func _print_cumulative_header() -> void:
	var header := "  阈值 \\ N   "
	for n in REROLL_COUNTS:
		header += "  %9s" % _format_int(n)
	print(header)

	var luck_line := "  出生 luck "
	for n in REROLL_COUNTS:
		luck_line += "  %9s" % _hidden_luck_label(n)
	print(luck_line)


func _print_design_summary() -> void:
	print("--- 设计对照 ---")
	print("  负 luck 六档边界：100 / 1,000 / 10,000 / 100,000 / 1,000,000 / 10,000,000 reroll。")
	print("  当前建卡对玩家只展示单项属性范围 4-14，不展示内部公式。")
	print("  5d3-1 的总和尾部可以自然支撑 100 到 10,000,000 reroll 的六档观察。")
	print("========================================")


func _calculate_histogram_summary(histogram: Dictionary) -> Dictionary:
	var min_value := _min_key(histogram)
	var max_value := _max_key(histogram)
	var total_count_float := 0.0
	var sum := 0.0
	var sq_sum := 0.0
	for key in histogram.keys():
		var value := int(key)
		var count := float(int(histogram[key]))
		total_count_float += count
		sum += float(value) * count
		sq_sum += float(value) * float(value) * count

	var mean := sum / total_count_float
	var variance := sq_sum / total_count_float - mean * mean
	return {
		"min": min_value,
		"max": max_value,
		"mean": mean,
		"std": sqrt(max(variance, 0.0)),
	}


func _tail_count(histogram: Dictionary, threshold: int) -> int:
	var result := 0
	for key in histogram.keys():
		if int(key) >= threshold:
			result += int(histogram[key])
	return result


func _sum_counts(histogram: Dictionary) -> int:
	var result := 0
	for key in histogram.keys():
		result += int(histogram[key])
	return result


func _min_key(histogram: Dictionary) -> int:
	var result := 999999
	for key in histogram.keys():
		result = mini(result, int(key))
	return result


func _max_key(histogram: Dictionary) -> int:
	var result := -999999
	for key in histogram.keys():
		result = maxi(result, int(key))
	return result


func _cumulative_hit_rate(q: float, reroll_count: int) -> float:
	return 1.0 - pow(1.0 - q, reroll_count)


func _dice_label() -> String:
	if DICE_VALUE_FLOOR == DICE_PER_STAT + DICE_OFFSET:
		if DICE_OFFSET == 0:
			return "%dd%d" % [DICE_PER_STAT, DICE_SIDES]
		return "%dd%d%+d" % [DICE_PER_STAT, DICE_SIDES, DICE_OFFSET]
	return "max(%d, %dd%d%+d)" % [
		DICE_VALUE_FLOOR,
		DICE_PER_STAT,
		DICE_SIDES,
		DICE_OFFSET,
	]


func _format_one_in_n(q: float) -> String:
	if q <= 0.0:
		return "1 in ∞"
	return "1 in %s" % _format_int(int(round(1.0 / q)))


## 出生 luck 档：CharacterCreationService 当前规则
##   0             → +2
##   [1, 9]        → +1
##   [10, 99]      → 0
##   [100, 999]    → -1
##   依次类推，每进一档 × 10，10,000,000+ 封顶 -6
func _hidden_luck_label(reroll_count: int) -> String:
	if reroll_count <= 0:
		return "+2"
	if reroll_count < 10:
		return "+1"
	if reroll_count < 100:
		return "0"
	if reroll_count >= 10000000:
		return "-6"

	var magnitude := 0
	var cursor := reroll_count
	while cursor >= 100:
		cursor = cursor / 10
		magnitude += 1
	return "-%d" % magnitude


func _format_int(value: int) -> String:
	var s := str(value)
	var result := ""
	var count := 0
	for i in range(s.length() - 1, -1, -1):
		result = s[i] + result
		count += 1
		if count % 3 == 0 and i > 0:
			result = "," + result
	return result

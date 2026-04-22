## 文件说明：建卡 reroll 分布仿真脚本，用来验证骰式产生的属性分布是否符合设计预期。
## 审查重点：参数化骰式常量，输出单属性分布、总和分布、单次命中率与累积 reroll 命中率。
## 备注：非回归用例，纯离线仿真工具；调整常量即可扫描不同骰式。
## 运行方式：godot --headless --script tools/character_creation_reroll_simulation.gd

extends SceneTree

## 仿真总次数。1e6 约 2-4 秒，想看稀有尾部（总和=24）提到 1e7。
const SIM_TRIALS: int = 10_000_000

## 属性项数（力/敏/体/感/智/意）
const STAT_COUNT: int = 6

## 骰式配置：每项属性 = DICE_PER_STAT 个 1-DICE_SIDES 骰子求和 + DICE_OFFSET
## 默认 3d2-2：范围 1-4，均值 2.5，std≈0.66
const DICE_PER_STAT: int = 9
const DICE_SIDES: int = 2
const DICE_OFFSET: int = -8

## 要观测命中率的总和阈值
const THRESHOLDS: Array[int] = [33, 40, 45, 48, 50, 53, 55, 60]

## 要观测累积命中率的 reroll 次数
const REROLL_COUNTS: Array[int] = [10, 100, 1000, 10000, 100000, 1000000, 10000000]


func _initialize() -> void:
	var rng := RandomNumberGenerator.new()
	rng.randomize()

	var stat_histogram: Dictionary = {}
	var total_histogram: Dictionary = {}
	var threshold_hits: Dictionary = {}
	for t in THRESHOLDS:
		threshold_hits[t] = 0

	var overall_sum: int = 0
	var overall_sq_sum: int = 0
	var min_total: int = 999999
	var max_total: int = -999999

	var start_time := Time.get_ticks_msec()

	for trial in SIM_TRIALS:
		var total: int = 0
		for stat_idx in STAT_COUNT:
			var stat: int = DICE_OFFSET
			for d in DICE_PER_STAT:
				stat += rng.randi_range(1, DICE_SIDES)
			total += stat
			stat_histogram[stat] = int(stat_histogram.get(stat, 0)) + 1

		total_histogram[total] = int(total_histogram.get(total, 0)) + 1
		overall_sum += total
		overall_sq_sum += total * total
		if total < min_total:
			min_total = total
		if total > max_total:
			max_total = total
		for t in THRESHOLDS:
			if total >= t:
				threshold_hits[t] = int(threshold_hits[t]) + 1

	var elapsed_ms := Time.get_ticks_msec() - start_time
	var mean_total := float(overall_sum) / SIM_TRIALS
	var variance := float(overall_sq_sum) / SIM_TRIALS - mean_total * mean_total
	var std_total := sqrt(max(variance, 0.0))

	print("========================================")
	print("  建卡 reroll 分布仿真")
	print("========================================")
	print("骰式：每项 %dd%d%+d（范围 %d-%d），%d 项" % [
		DICE_PER_STAT, DICE_SIDES, DICE_OFFSET,
		DICE_PER_STAT + DICE_OFFSET, DICE_PER_STAT * DICE_SIDES + DICE_OFFSET,
		STAT_COUNT,
	])
	print("仿真次数：%s" % _format_int(SIM_TRIALS))
	print("耗时：%d ms" % elapsed_ms)
	print("")

	print("--- 单属性分布 ---")
	var sorted_stat_keys: Array = stat_histogram.keys()
	sorted_stat_keys.sort()
	var total_stat_count: int = SIM_TRIALS * STAT_COUNT
	for key in sorted_stat_keys:
		var pct: float = 100.0 * int(stat_histogram[key]) / total_stat_count
		print("  值 %d: %.3f%%" % [key, pct])
	print("")

	print("--- 总和分布 ---")
	print("  min=%d  max=%d  mean=%.3f  std=%.3f" % [min_total, max_total, mean_total, std_total])
	print("")

	var sorted_total_keys: Array = total_histogram.keys()
	sorted_total_keys.sort()
	var max_pct: float = 0.0
	for k in sorted_total_keys:
		var p: float = 100.0 * int(total_histogram[k]) / SIM_TRIALS
		if p > max_pct:
			max_pct = p
	for key in sorted_total_keys:
		var pct: float = 100.0 * int(total_histogram[key]) / SIM_TRIALS
		var bar_len: int = 0
		if max_pct > 0.0:
			bar_len = int((pct / max_pct) * 48.0)
		var bar: String = "#".repeat(bar_len)
		var count_str: String = _format_int(int(total_histogram[key]))
		print("  总和 %2d: %9.4f%%  (%s)  %s" % [key, pct, count_str, bar])
	print("")

	print("--- 单次 roll 命中阈值的概率 q ---")
	for t in THRESHOLDS:
		var q: float = float(int(threshold_hits[t])) / SIM_TRIALS
		var one_in_n: String = "1 in ∞"
		if q > 0.0:
			one_in_n = "1 in %s" % _format_int(int(round(1.0 / q)))
		print("  总和 ≥ %2d: q = %.6f  (%s)" % [t, q, one_in_n])
	print("")

	print("--- 累积命中率：N 次 reroll 内至少一次命中 ---")
	var header: String = "  阈值 \\ N   "
	for n in REROLL_COUNTS:
		header += "  %9s" % _format_int(n)
	print(header)
	var penalty_line: String = "  幸运惩罚   "
	for n in REROLL_COUNTS:
		penalty_line += "  %9s" % _luck_penalty_label(n)
	print(penalty_line)
	for t in THRESHOLDS:
		var q: float = float(int(threshold_hits[t])) / SIM_TRIALS
		var line: String = "  ≥%2d        " % t
		for n in REROLL_COUNTS:
			var cumulative: float = 1.0 - pow(1.0 - q, n)
			line += "  %8.3f%%" % (cumulative * 100.0)
		print(line)
	print("")

	print("--- 设计对照 ---")
	print("  你配置的三段惩罚阈值：[0, 100, 1000, 10000]")
	print("  看上表 N=100 / 1000 / 10000 的值，对应免费区/轻度/重度区的玩家命中体验。")
	print("  目标曲线：免费区难命中好属性、重度区能刷到神 roll。")
	print("========================================")

	quit(0)


## 幸运惩罚档：每多一个 10 倍数量级再扣 1 幸运
##   [0, 99]       → 0
##   [100, 999]    → -1
##   [1000, 9999]  → -2
##   依次类推，每进一档 × 10
func _luck_penalty_label(reroll_count: int) -> String:
	if reroll_count < 100:
		return "0"
	var magnitude: int = 0
	var cursor: int = reroll_count
	while cursor >= 100:
		cursor = cursor / 10
		magnitude += 1
	return "-%d" % magnitude


func _format_int(value: int) -> String:
	var s: String = str(value)
	var result: String = ""
	var count: int = 0
	for i in range(s.length() - 1, -1, -1):
		result = s[i] + result
		count += 1
		if count % 3 == 0 and i > 0:
			result = "," + result
	return result

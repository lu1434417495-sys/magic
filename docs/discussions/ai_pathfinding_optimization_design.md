# 战斗 AI Pathfinding 未完成优化设想

更新日期：`2026-06-11`

## 文档定位

这份文档只保留尚未落地、未来仍可能继续推进的 AI pathfinding / screening 性能设想。

已完成或已有回归覆盖的内容不再记录在这里，包括：

- 多候选目标从重复单目标 A* 改为复用 path tree / Dijkstra。
- 地面 AoE setup 中心扫描从全图收窄到施法范围包围盒。
- 相关性能验证、临时大图压测和回归测试结果。

本文件不是当前运行时权威说明，也不是必须执行的任务清单；真正的实现状态以源码、测试和 `docs/design/project_context_units.md` 为准。

## 仍可考虑的方向

### 1. 目标集合驱动 Dijkstra

当前可继续探索的核心思路是：不要默认把可达区域整片扫完，而是让 pathfinding 面向候选目标集合工作。

候选动作需要提供：

- `destination_cell`
- `hard_cost_cap`
- `lower_bound_cost`
- `search_cap`
- `requires_exact_preview`

搜索可以在以下条件满足时提前停止：

- 所有 relevant destinations 已 settled。
- frontier 最小 cost 超过所有剩余候选的 search cap。
- 所有未 settled 候选在当前 frontier 下界下都不可能超过当前 best score。

最小可落地版本可以先做：

```text
search(source, targets, cost_cap)
```

停止条件只保留：

```text
all_targets_settled
frontier_min_cost > cost_cap
heap_empty
```

### 2. Score Upper Bound 剪枝

后续如果要继续减少搜索量，可以给候选动作提供乐观分数上界：

```text
score_upper_bound(cost_lower_bound)
max_cost_that_can_still_beat(best_score)
```

安全原则：

- upper bound 偏高可以接受，只是少剪枝。
- upper bound 偏低不可接受，会误杀最优动作并改变 AI 语义。
- path cost 和 score 不能直接比较，必须通过候选自身的评分上界换算成最大有用 path cost。

示意：

```text
upper_score(cost) =
    base_value
  + max_possible_position_bonus
  + max_possible_screening_bonus
  - move_cost_weight * cost
```

当：

```text
upper_score(lower_bound_cost) <= current_best_score
```

该候选才可以被安全过滤。

### 3. 本回合移动与远距离战略推进拆分

远距离战略目标不应迫使本回合动态 pathfinding 扫完整地图。

建议拆成两类：

- 本回合真实站位：受 movement budget 限制，使用精确动态搜索和 exact preview。
- 远距离战略方向：使用静态距离场、低频 reverse field、A* path prefix 或 goal potential。

远距离推进只参与评分，不直接声明动作合法。

示意：

```text
progress_score =
    distance_to_goal(current_cell)
  - distance_to_goal(move_cell)
```

### 4. Screening / 保护队友评分重写

screening 仍可能隐藏乘法级 pathfinding：

```text
for blocker candidate
  for threat enemy
    for protected target
      replan path
```

未来可考虑改成：

1. 对每个 threat 构建 baseline path / threat distance field。
2. 用 cheap relevance test 过滤不可能影响路径的 blocker。
3. 只有可能改变结论的 blocker 才做 capped exact replan。

评分建议使用饱和函数：

```text
screen_score =
    weight * clamp(new_path_cost - base_path_cost, 0, max_useful_delay)
```

只要评分语义已经饱和，replan 就可以安全限制到：

```text
base_path_cost + max_useful_delay
```

如果搜索到 cap 仍未找到路径，可以把 delay 视为 `max_useful_delay`。

### 5. Shortest Path DAG

如果 baseline path + capped replan 后 screening 仍是热点，再考虑 shortest path DAG。

可使用：

```text
dist_from_threat[cell]
dist_to_protected_zone[cell]
base_cost
```

某格在至少一条最短路径上的条件：

```text
dist_from_threat[cell] + dist_to_protected_zone[cell] == base_cost
```

更进一步可以统计最短路径 DAG 中经过 blocker 的路径数量；如果仍存在不经过 blocker 的最短路径，则该 blocker 不改变 shortest path cost。

### 6. Bounded A*

单目标 A* 也可以支持 cap：

```text
cost_cap
score_cap
stop_if_f_min_cannot_beat_best
```

若 heuristic admissible，则：

```text
f = g + h
```

是完整路径 cost 下界。可以安全停止于：

```text
f_min > search_cap
```

或：

```text
candidate.score_upper_bound(f_min) <= best_score
```

### 7. Profiling 指标补强

如果继续做目标驱动搜索或 screening 重写，应先补内部规模指标，避免只看函数总耗时。

建议每次 pathfinding 记录：

```text
source_cell
target_count
hard_cap
score_cap
expanded_nodes
settled_nodes
edge_checks
heap_pushes
heap_pops
duplicate_pops
max_heap_size
settled_target_count
remaining_target_count
max_settled_cost
frontier_min_cost_on_stop
stop_reason
```

screening 单独记录：

```text
screening_candidate_count
threat_count
protected_target_count
blocker_checked_count
blocker_filtered_by_distance_lb
blocker_filtered_by_baseline_path
exact_replan_count
exact_replan_expanded_nodes_avg
exact_replan_expanded_nodes_max
```

重点派生指标：

```text
time_per_expanded_node = elapsed_time / expanded_nodes
```

用来区分：

- 搜索区域变大。
- 单节点成本变高。
- screening 隐藏 replan 爆炸。
- preview / debug / allocation 常数开销变大。

### 8. 热路径数据结构优化

如果 profiler 指向 pathfinding 内部常数开销，可以再考虑低分配数据结构。

建议方向：

- `cell_id = y * width + x`
- 使用数组保存 `dist` / `prev` / `visited_stamp` / `closed_stamp`
- 使用 stamp 避免反复清空数组
- 预计算邻居表
- 对小整数 cost 评估 bucket queue / Dial's algorithm

这类优化应建立在 profiling 证据上，不作为默认先做项。

## 推荐推进顺序

1. 补 pathfinding / screening 内部规模 profiling。
2. 实现最小版 target-set Dijkstra：`search(source, targets, cost_cap)`。
3. 将移动到攻击范围、施法范围、保护站位等批量站位评估接入 target-set Dijkstra。
4. 加入 score upper bound 剪枝。
5. 拆分远距离战略推进。
6. 重写 screening：baseline path / cheap relevance / capped replan。
7. 只有 profiling 仍指向 screening 时，再做 shortest path DAG。

## 必须保持的安全边界

- cheap filter 只能过滤数学上不可能或确定不可能超过当前 best 的候选。
- exact preview 仍是复杂合法性的最终裁判。
- 远距离距离场只能用于评分，不能直接判定本回合动作合法。
- score upper bound 必须是真上界，宁可偏高，不能偏低。
- capped replan 的 cap 必须来自评分饱和语义，而不是任意截断。

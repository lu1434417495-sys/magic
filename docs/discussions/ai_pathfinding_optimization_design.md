# 回合制网格战斗 AI Pathfinding 性能优化设计文档

## 1. 背景与目标

当前项目是一个回合制网格战斗 AI，地图规模约为 `30 x 18`，约 `540` 个格子，战斗规模约为 `6 方对 12 方`。

每个 AI 单位行动时，会枚举多个候选动作，例如：

- 移动
- 攻击
- 位移
- 控场
- 保护队友
- 阻挡敌人路径
- 靠近远距离战略目标

当前 AI 决策流程可以抽象为：

```text
每个 AI 单位行动时：
  枚举动作类型
    枚举目标 / 目标格 / 站位
      用 pathfinding 或 preview 判断是否合法
      计算评分
  选择最佳动作执行
```

已经完成的优化包括：

1. 多个 destination 不再反复跑单目标 A*，而是改为必要时从当前位置构建 path tree / Dijkstra。
2. 地面技能 preview 不再枚举整张地图，而是先做射程 prefilter。

这些优化已经减少了 pathfinding / preview 的调用次数，但剩余瓶颈变成：

```text
调用次数没有明显增加，
但每次 pathfinding 平均耗时变高。
```

也就是说，问题已经从：

```text
调用太多次
```

转变为：

```text
单次搜索扫得太大
```

本文档目标是把 AI 决策中的 pathfinding 从：

```text
对大量假想候选反复做全量可达性求解
```

改造为：

```text
围绕候选目标、当前最佳分数和安全下界的目标驱动搜索
```

并保证所有提前过滤都是语义保守的，最终合法性仍由精确 preview 裁判。

---

## 2. 核心问题抽象

当前 path tree / Dijkstra 的搜索预算接近整张地图大小：

```text
max_nodes ~= map_width * map_height
30 * 18 = 540
```

这会导致 path tree 实际回答的问题变成：

```text
从当前单位出发，把整片可达区域都扫一遍。
```

但 AI 真正需要的问题通常是：

```text
在这批候选 destination 中，哪些能到？
哪些到达成本足够低，仍可能成为最优动作？
```

因此，性能差异会高度依赖战局结构：

```text
如果可达区域很大：
  Dijkstra 扩展节点多，耗时高。

如果可达区域被单位、障碍、地形切碎：
  Dijkstra 扩展节点少，耗时低。
```

这解释了为什么慢局中：

```text
AI 回合数没有明显变多，
pathfinding 调用次数没有明显变多，
但每次 build path tree / A* 的平均耗时翻倍。
```

---

## 3. 总体设计原则

### 3.1 不再把 pathfinding 当成黑盒合法性谓词

旧思路：

```text
for candidate in candidates:
    if can_reach(candidate.destination):
        preview(candidate)
        score(candidate)
```

问题是：

```text
can_reach / build_path_tree 内部可能扫掉大量和最终选择无关的格子。
```

新思路：

```text
候选动作先提供：
  - destination
  - hard cost cap
  - lower bound cost
  - score upper bound
  - 是否需要 exact preview

pathfinding 只服务于仍可能成为最优解的候选。
```

### 3.2 preview 仍然是最终裁判

优化不能让 cheap filter 替代 preview。

正确职责划分是：

```text
cheap filter：
  只过滤数学上不可能，或者不可能超过当前 best 的候选。

pathfinding：
  只为 remaining candidates 提供精确移动成本或可达性。

preview：
  最终判定动作是否合法。
```

### 3.3 本回合行动和远距离战略推进要拆开

不能简单把所有 pathfinding budget 都缩小到本回合移动力，因为 AI 可能需要知道：

```text
目标虽然本回合到不了，但应该朝哪个方向推进。
```

但远距离战略推进不应该依赖动态全图 Dijkstra。

建议拆成：

```text
本回合实际站位：
  精确、动态、受 movement budget 限制。

远距离战略方向：
  使用静态距离场、低频 reverse field、A* path prefix 或 goal potential。
```

---

## 4. 候选动作数据结构

建议为所有 AI 候选动作建立统一结构。

```text
CandidateAction
{
    id
    action_type

    source_cell
    destination_cell
    target_unit_id optional
    target_cell optional

    hard_cost_cap
    lower_bound_cost
    search_cap

    base_value
    move_cost_weight

    requires_movement_path
    requires_exact_preview
    requires_screening_score

    score_upper_bound(cost_lower_bound)
    max_cost_that_can_still_beat(best_score)

    exact_preview(path_result)
    exact_score(path_result, context)
}
```

### 字段解释

| 字段 | 含义 |
|---|---|
| `destination_cell` | 当前候选动作需要到达或验证的目标格 |
| `hard_cost_cap` | 超过该 cost 后，动作语义上必然无效 |
| `lower_bound_cost` | 数学下界，例如曼哈顿距离 / Chebyshev 距离 / 静态距离 |
| `search_cap` | 本次搜索对该候选实际使用的搜索上限 |
| `score_upper_bound()` | 还未精确 pathfinding 时，该候选最高可能得分 |
| `max_cost_that_can_still_beat()` | 在当前 best score 下，该候选仍可能翻盘的最大移动成本 |
| `exact_preview()` | 精确合法性判定，仍由现有 preview 执行 |
| `exact_score()` | 得到精确 path cost 和 preview 结果后的真实评分 |

---

## 5. 保守 prefilter 设计

保守 prefilter 的原则：

```text
只能过滤“数学上不可能”或“确定不可能成为最优”的候选。
不能过滤只是“看起来不划算”的候选。
```

### 5.1 数学上不可能

例如四方向移动时：

```text
manhattan_distance(source, destination) > hard_cost_cap
```

则候选必然不可达，可以过滤。

八方向移动时，可以使用：

```text
Chebyshev distance
Octile distance
```

如果存在静态墙体，可以预处理静态连通分量：

```text
source_static_component != destination_static_component
```

则候选在静态地图上必然不可达，可以过滤。

### 5.2 被当前 best 支配

如果候选的乐观上界分数都不超过当前 best：

```text
candidate.score_upper_bound(candidate.lower_bound_cost) <= current_best_score
```

则它即使合法，也不可能成为最终选择，可以过滤。

这里的关键是：

```text
score_upper_bound 必须是真实分数的乐观上界。
```

即必须满足：

```text
real_score <= score_upper_bound
```

只要这个关系成立，剪枝就是安全的。

### 5.3 明确的技能必要条件

例如技能射程：

```text
lower_bound_distance(caster_cell, target_cell) > skill_range
```

可以直接过滤，不进入 preview。

例如攻击站位：

```text
lower_bound_distance(stand_cell, enemy_cell) > weapon_range
```

也可以直接过滤。

但以下条件不建议由 cheap filter 独断：

```text
视线 LOS
临时单位占用
动态阻挡
技能特殊判定
复杂地形交互
```

这些仍交给 exact preview。

---

## 6. 目标集合驱动 Dijkstra

### 6.1 新 API

旧 API：

```text
build_path_tree(source, max_nodes = map_size)
```

新 API：

```text
search_to_candidate_set(
    source,
    candidate_destinations,
    hard_cost_cap,
    score_upper_bound,
    incumbent_best_score
)
```

更具体的数据接口：

```text
TargetDrivenSearchRequest
{
    source_cell
    candidates[]
    global_hard_cap
    current_best_score
    movement_rules
    occupancy_mode
}

TargetDrivenSearchResult
{
    reached_candidates[]
    rejected_candidates[]
    best_action_from_this_batch optional

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
}
```

---

### 6.2 Dijkstra 停止条件

目标驱动 Dijkstra 不应该默认扫完整 reachable component。

它应该在以下任一条件满足时停止。

#### 条件 A：所有 relevant destinations 已 settled

```text
所有仍可能成为最优动作的 destination 都已经被弹出并确认最短距离。
```

注意是：

```text
relevant destinations
```

不是地图所有格子。

---

#### 条件 B：frontier 最小 cost 超过所有剩余候选的搜索上限

每个候选有自己的搜索上限：

```text
candidate.search_cap = min(
    candidate.hard_cost_cap,
    candidate.max_cost_that_can_still_beat(current_best_score)
)
```

如果：

```text
frontier_min_cost > max(search_cap of all active candidates)
```

则后续节点不可能服务任何仍有价值的候选，可以停止。

---

#### 条件 C：所有未 settled 候选都无法超过当前 best

Dijkstra 的性质是：

```text
frontier_min_cost 是所有未 settled 节点的最短路下界。
```

因此对任何未 settled 候选：

```text
real_cost >= max(candidate.lower_bound_cost, frontier_min_cost)
```

如果：

```text
candidate.score_upper_bound(
    max(candidate.lower_bound_cost, frontier_min_cost)
) <= current_best_score
```

则该候选不可能翻盘。

如果所有未 settled 候选都满足这个条件，可以停止。

---

### 6.3 不能直接比较 path cost 和 best score

注意不能写：

```text
if frontier_min_cost > best_score:
    stop
```

因为：

```text
path cost 和 score 不是同一个单位。
```

应该把评分函数转换成：

```text
该候选仍可能超过 best score 的最大有用 path cost。
```

例如评分函数近似为：

```text
score = base_value - move_cost_weight * path_cost
```

当前最佳分数为：

```text
best_score
```

则候选还能翻盘的最大 path cost 为：

```text
max_useful_cost = (base_value - best_score) / move_cost_weight
```

更完整的形式：

```text
upper_score(cost) =
    base_value
  + max_possible_position_bonus
  + max_possible_screening_bonus
  + max_possible_combo_bonus
  - move_cost_weight * cost
```

只要：

```text
upper_score(cost_lower_bound) <= best_score
```

就可以安全剪枝。

---

## 7. 目标集合 Dijkstra 伪代码

```text
function evaluate_candidate_batch_with_target_driven_dijkstra(
    source,
    candidates,
    current_best_score
):
    active_candidates = []
    target_map = map cell_id -> candidate list

    for candidate in candidates:
        lb = lower_bound(source, candidate.destination_cell)
        candidate.lower_bound_cost = lb

        if lb > candidate.hard_cost_cap:
            candidate.reject_reason = LOWER_BOUND_EXCEEDS_HARD_CAP
            continue

        if candidate.score_upper_bound(lb) <= current_best_score:
            candidate.reject_reason = DOMINATED_BY_BEST_SCORE
            continue

        candidate.search_cap = min(
            candidate.hard_cost_cap,
            candidate.max_cost_that_can_still_beat(current_best_score)
        )

        if lb > candidate.search_cap:
            candidate.reject_reason = LOWER_BOUND_EXCEEDS_SEARCH_CAP
            continue

        active_candidates.add(candidate)
        target_map[candidate.destination_cell].add(candidate)

    if active_candidates is empty:
        return current_best_score

    init_dijkstra(source)

    while frontier is not empty:
        frontier_min_cost = frontier.peek_cost()

        if frontier_min_cost > max_search_cap(active_candidates):
            stop_reason = FRONTIER_EXCEEDED_SEARCH_CAP
            break

        if all_remaining_candidates_dominated(
            active_candidates,
            frontier_min_cost,
            current_best_score
        ):
            stop_reason = ALL_REMAINING_DOMINATED
            break

        node, cost = frontier.pop()

        if already_settled(node):
            continue

        settle(node, cost)

        if target_map.contains(node):
            for candidate in target_map[node]:
                if cost > candidate.hard_cost_cap:
                    candidate.reject_reason = COST_EXCEEDS_HARD_CAP
                    deactivate(candidate)
                    continue

                if candidate.score_upper_bound(cost) <= current_best_score:
                    candidate.reject_reason = DOMINATED_AFTER_EXACT_COST
                    deactivate(candidate)
                    continue

                if candidate.requires_exact_preview:
                    if not candidate.exact_preview(cost):
                        candidate.reject_reason = PREVIEW_REJECTED
                        deactivate(candidate)
                        continue

                score = candidate.exact_score(cost)

                if score > current_best_score:
                    current_best_score = score
                    best_action = candidate
                    update_search_caps(active_candidates, current_best_score)

                deactivate(candidate)

        for neighbor in neighbors(node):
            new_cost = cost + move_cost(node, neighbor)

            if new_cost > max_search_cap(active_candidates):
                continue

            relax(neighbor, new_cost)

    return best_action
```

---

## 8. 本回合移动与远距离战略推进拆分

### 8.1 本回合真实移动

这些动作必须受当前回合移动力限制：

```text
移动到攻击范围内
移动到施法站位
移动到保护队友位置
移动到阻挡位置
移动到掩护位置
移动后再攻击
移动后再控制
```

因此它们的 hard cap 应该是：

```text
hard_cost_cap = current_unit_movement_budget
```

或者在存在额外移动资源时：

```text
hard_cost_cap = movement_budget + dash_budget + skill_bonus_movement
```

这类动作适合使用：

```text
目标集合 Dijkstra
```

---

### 8.2 远距离战略推进

远距离目标不应该被当成本回合 destination 做全图动态 pathfinding。

建议改成 goal potential：

```text
progress_score =
    distance_to_goal(current_cell)
  - distance_to_goal(move_cell)
```

其中 `distance_to_goal` 可以来自：

```text
静态地形距离场
忽略临时单位占用的 reverse Dijkstra
低频更新的战略距离图
单次 A* 到远目标后取 path prefix
```

最终本回合候选仍然只枚举：

```text
本回合可实际走到的 move_cell
```

然后用 progress_score 给这些 move_cell 加分。

---

## 9. Screening / 保护队友评分优化

### 9.1 当前问题

screening 评分可能隐藏了 pathfinding 乘法：

```text
for each blocker candidate:
    for each threat enemy:
        for each protected target:
            run pathfinding
```

这会导致 `_apply_screening_score` 表面上很贵，但 profile 中不一定直接显示为 A* 很贵。

---

### 9.2 新设计：baseline threat field + 少量 exact replan

推荐流程：

```text
for each threat unit:
    构建一次 baseline path / threat distance field

for each candidate blocker:
    用 cheap relevance test 判断它是否可能改变路径

只有可能改变结论的 blocker:
    才做 capped exact replan
```

---

### 9.3 Screening 分数应使用饱和函数

AI 通常不需要知道：

```text
敌人被挡后绕了 8 格还是 80 格。
```

只需要知道：

```text
这个站位是否显著拖慢敌人。
```

建议评分：

```text
screen_score =
    weight * clamp(new_path_cost - base_path_cost, 0, max_useful_delay)
```

例如：

```text
max_useful_delay = 4
```

那么 exact replan 的搜索上限可以设为：

```text
replan_cap = base_path_cost + max_useful_delay
```

如果搜索到 cap 还没找到路径，可以直接认为：

```text
screening delta = max_useful_delay
```

这不是粗暴近似，而是评分语义已经定义为“最多只奖励 4 格延迟”。

---

### 9.4 Screening 具体流程

#### Step 1：构建 baseline

对每个威胁单位 `threat`：

```text
把当前 AI 单位原位置视为空
不加入候选 blocker
计算 threat 到 protected zone 的 baseline shortest path
```

保护目标不建议只用单格，而应使用目标区域：

```text
protected_zone = 队友周围可被攻击 / 接触 / 威胁的格子集合
```

构建：

```text
base_cost
base_path
dist_from_threat[]
dist_to_protected_zone[]
```

其中：

```text
dist_to_protected_zone[]
```

可以通过 multi-source reverse Dijkstra 从 protected zone 出发计算。

---

#### Step 2：cheap blocker relevance test

对于候选 blocker `b`，先做下界测试：

```text
LB(threat, b) + LB(b, protected_zone)
    > base_cost + max_useful_delay
```

如果成立，则 blocker 不可能影响有用范围内的路径，可以跳过。

---

#### Step 3：baseline path 安全判断

如果只有“占格阻挡”语义，并且：

```text
blocker_cell 不在 baseline shortest path 上
```

则原 baseline path 仍然可用。

加入 blocker 只会让路径不变或变差，因此：

```text
new_cost >= base_cost
```

而 baseline path 没被挡住，因此：

```text
new_cost <= base_cost
```

两者合并：

```text
new_cost == base_cost
```

所以该 blocker 对该 threat 的 shortest path 没影响，不需要 replan。

如果 screening 还包含 ZOC、减速光环、威胁范围，则判断应扩展为：

```text
baseline path 是否穿过 blocker 的影响区域
```

---

#### Step 4：只对可能改变路径的 blocker 做 capped replan

满足以下条件时才精确重算：

```text
blocker_cell 在 baseline path 上
```

或：

```text
blocker_cell 在 near-shortest path corridor 内
```

精确搜索：

```text
A* / Dijkstra from threat to protected_zone
blocked_cell = blocker_cell
cost_cap = base_cost + max_useful_delay
```

如果超过 cap 仍未找到路径：

```text
screening delta = max_useful_delay
```

---

## 10. 更强版本：最短路 DAG

如果需要更高精度，可以用最短路 DAG 判断 blocker 是否真的改变最短路。

已知：

```text
dist_from_threat[cell]
dist_to_protected_zone[cell]
base_cost
```

某格 `x` 在某条最短路径上，当且仅当：

```text
dist_from_threat[x] + dist_to_protected_zone[x] == base_cost
```

如果 blocker 不满足该条件，则它不在任何最短路径上，通常不需要 replan。

更进一步，可以统计 shortest path DAG 上的路径数量：

```text
total_shortest_paths
paths_through_blocker
```

如果：

```text
paths_through_blocker < total_shortest_paths
```

说明还有不经过 blocker 的最短路径，因此挡住它不会改变 shortest path cost：

```text
new_cost == base_cost
```

这个版本比只检查一条 baseline path 更强，但实现复杂度更高。

推荐落地顺序：

```text
先实现 baseline path 检查 + capped replan。
后续如果 screening 仍是热点，再实现 shortest path DAG。
```

---

## 11. blocker / occupancy 缓存安全性

| 缓存内容 | 是否安全 | 用法 |
|---|---:|---|
| 曼哈顿 / Chebyshev 下界 | 永远安全 | 只能做 lower bound |
| 静态墙体连通分量 | 安全 | 判断静态不可达 |
| 忽略单位占用的静态距离场 | 安全 | 作为 optimistic lower bound |
| 当前真实 occupancy 下的完整路径 | 只在 occupancy 不变时精确安全 | 可用于 exact cost |
| 当前真实 occupancy 下的距离 | 加 blocker 后不再精确 | 但可作为 lower bound |
| baseline path 如果不经过 blocker | 安全 | 可证明 blocker 不影响该 path |
| baseline path 如果经过 blocker | 不安全 | 需要 replan |
| blocker-specific path result | 只对该 blocker 安全 | 可以少量缓存，不建议无限缓存 |

核心性质：

```text
加一个 blocker 只会让路径不变或变差，不会让路径变好。
```

因此 baseline distance 在 blocker 变化后可以作为 lower bound，但不能直接作为 exact result。

只有当能证明某条 baseline shortest path 没有被 blocker 影响时，才可以继续使用 exact base_cost。

---

## 12. 正向搜索与反向搜索的选择

### 12.1 一个 source，多个 destination

例如：

```text
当前单位评估多个候选站位。
```

使用：

```text
source -> target_set 的目标驱动 Dijkstra
```

---

### 12.2 多个 source，一个目标区域

例如：

```text
多个敌人都想靠近同一个队友。
多个单位都想靠近同一个战略点。
```

使用：

```text
target_zone -> all relevant cells 的 reverse Dijkstra
```

然后每个 source 直接查表。

---

### 12.3 screening

通常需要：

```text
dist_from_threat[cell]
dist_to_protected_zone[cell]
```

这样可以快速判断：

```text
某个 blocker 是否在 threat 到 protected zone 的路径走廊里。
```

---

## 13. 单目标 A* 也需要 cap

单目标 A* 不应该无上限跑。

应支持：

```text
cost_cap
score_cap
stop_if_f_min_cannot_beat_best
```

A* 中：

```text
f = g + h
```

如果 heuristic 是 admissible，也就是：

```text
h <= 真实剩余距离
```

则 `f` 是完整路径 cost 的下界。

因此可以安全停止：

```text
if candidate.score_upper_bound(f_min) <= best_score:
    stop
```

或：

```text
if f_min > candidate.search_cap:
    stop
```

---

## 14. Pathfinding 策略选择表

| 场景 | 推荐策略 |
|---|---|
| 同一单位评估多个站位 | 目标集合 Dijkstra |
| 单个明确目标 | bounded A* |
| 多个单位查询同一目标区域 | reverse Dijkstra / distance field |
| 远距离战略推进 | 静态距离场 / goal potential / path prefix |
| screening blocker 评估 | baseline threat field + blocker shortlist + capped replan |
| 技能射程过滤 | 几何 lower bound + exact preview |
| 复杂 LOS / 技能合法性 | preview 最终裁判 |

---

## 15. Profiling 指标设计

当前不应只记录：

```text
函数调用次数
函数总耗时
平均耗时
```

还必须记录每次搜索的内部规模。

### 15.1 每次 pathfinding 记录

```text
query_id
caller_name
unit_id
action_family

source_cell
target_count
hard_cap
score_cap
final_effective_cap

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

stop_reason:
    all_targets_settled
    frontier_exceeded_hard_cap
    frontier_exceeded_score_cap
    all_remaining_candidates_dominated
    heap_empty
    preview_rejected_all
```

### 15.2 Screening 单独记录

```text
screening_candidate_count
threat_count
protected_target_count

blocker_checked_count
blocker_filtered_by_distance_lb
blocker_filtered_by_baseline_path
blocker_filtered_by_shortest_path_dag

exact_replan_count
exact_replan_expanded_nodes_avg
exact_replan_expanded_nodes_max

screening_score_delta_avg
screening_score_delta_max
```

### 15.3 候选生成漏斗

```text
raw_candidate_count
after_range_prefilter
after_lower_bound_filter
after_score_upper_bound_filter
after_pathfinding_settled
after_preview_legal
```

### 15.4 必看派生指标

```text
time_per_expanded_node = elapsed_time / expanded_nodes
```

判断方式：

```text
如果慢局 expanded_nodes 翻倍，但 time_per_node 不变：
  主要问题是搜索区域过大。

如果慢局 expanded_nodes 差不多，但 time_per_node 翻倍：
  主要问题是常数开销、数据结构、内存分配、preview 隐藏逻辑或 debug 逻辑。
```

还建议看：

```text
p50 / p90 / p99 expanded_nodes
p50 / p90 / p99 elapsed_time
p50 / p90 / p99 exact_replan_count
```

不要只看平均值。

---

## 16. 数据结构建议

地图只有约 540 格，不建议在热路径里大量使用对象、字典、Vector key。

### 16.1 cell_id 编码

```text
cell_id = y * width + x
```

### 16.2 使用数组而不是动态对象

```text
dist[cell_id]
prev[cell_id]
visited_stamp[cell_id]
closed_stamp[cell_id]
first_step[cell_id]
```

### 16.3 使用 stamp 避免频繁清空数组

```text
if visited_stamp[cell] != current_query_stamp:
    dist[cell] = INF
    visited_stamp[cell] = current_query_stamp
```

### 16.4 target map

```text
is_target[cell_id]
target_candidates[cell_id] = candidate_list
```

### 16.5 邻居预计算

```text
neighbors[cell_id] = [neighbor_id...]
```

如果移动 cost 是小整数，可以考虑 bucket queue / Dial's algorithm：

```text
cost 0..N 的桶队列
```

在小型网格图上，它可能比二叉堆更稳定。

---

## 17. 推荐落地顺序

### 阶段 1：补 profiling，不改逻辑

目标是确认慢局到底慢在哪里。

必须新增：

```text
expanded_nodes
edge_checks
heap_pushes
settled_target_count
max_settled_cost
stop_reason
time_per_expanded_node
screening exact replan count
```

验收标准：

```text
能够区分：
  - 搜索区域变大
  - 单节点成本变高
  - screening 隐藏 pathfinding 爆炸
```

---

### 阶段 2：给 pathfinding API 加 target set + cap

先不用做复杂 branch-and-bound。

最小改造：

```text
search(source, targets, cost_cap)
```

停止条件至少包括：

```text
所有 targets settled
frontier_min_cost > cost_cap
heap_empty
```

验收标准：

```text
path tree 不再默认扫完整 reachable component。
```

---

### 阶段 3：移动到攻击范围内 / 施法范围内 改成目标集合 Dijkstra

优先改最典型的批量站位评估：

```text
移动到攻击范围内
移动到技能释放站位
移动到保护队友站位
```

验收标准：

```text
expanded_nodes 与候选目标距离和 cap 相关，
不再与整片可达区域强绑定。
```

---

### 阶段 4：加入 score upper bound 剪枝

给候选动作实现：

```text
score_upper_bound(cost_lower_bound)
max_cost_that_can_still_beat(best_score)
```

验收标准：

```text
当前 best score 越高，后续候选搜索越少。
```

---

### 阶段 5：拆分远距离战略推进

把远距离目标从动态全图 path tree 中移除。

改为：

```text
本回合实际移动：movement budget bounded
远距离推进方向：goal potential / reverse static distance field
```

验收标准：

```text
远距离目标不会导致本回合 pathfinding 扫完整地图。
```

---

### 阶段 6：重写 screening

先实现简单有效版：

```text
每个 threat 构建 baseline path
blocker 不在 baseline path / 影响区上 => 不 replan
blocker 在 baseline path / 影响区上 => capped replan
```

验收标准：

```text
exact_replan_count 显著下降。
_apply_screening_score 的 p90 / p99 耗时下降。
```

---

### 阶段 7：必要时实现 shortest path DAG

如果 screening 仍是热点，再实现：

```text
dist_from_threat + dist_to_protected_zone
shortest path corridor
path count / dominator 判断
```

验收标准：

```text
只有真正可能改变最短路结论的 blocker 才触发 replan。
```

---

## 18. 最终 AI 决策流程

目标流程：

```text
AI Decide Unit Turn
{
    best_action = null
    best_score = -INF

    candidate_batches = generate_action_candidates()

    for batch in candidate_batches:

        cheap_filter_by_geometry(batch)
        cheap_filter_by_static_lower_bound(batch)
        cheap_filter_by_score_upper_bound(batch, best_score)

        if batch.requires_movement_path:
            target_driven_path_search(batch, best_score)

        if batch.requires_screening:
            screening_evaluator.evaluate_with_cached_threat_fields(batch, best_score)

        exact_preview_for_remaining_candidates(batch)
        exact_score_for_legal_candidates(batch)

        update_best_action(batch)

    execute(best_action)
}
```

核心变化：

```text
pathfinding 不再是 bool can_reach(destination)。
```

而是：

```text
给我一组候选目标，
我只搜索到足够证明最优动作的位置为止。
```

---

## 19. 风险与注意事项

### 19.1 score upper bound 必须真的是上界

如果 `score_upper_bound` 低估了候选真实分数，就可能误杀最优动作。

因此实现时宁可保守：

```text
upper bound 偏高可以接受，只是少剪一点。
upper bound 偏低不可接受，会改变 AI 语义。
```

### 19.2 preview 不要被绕过

所有复杂合法性仍必须由 preview 判定。

尤其是：

```text
LOS
技能特殊规则
单位动态占用
地形特殊交互
控制区
临时状态
```

### 19.3 远距离推进不要污染本回合合法性

远距离距离场只能用于评分，不应该直接声明本回合动作合法。

### 19.4 screening 的 cap 来自评分语义

`max_useful_delay` 不是随便截断搜索，而是定义：

```text
AI 最多只奖励这么多格的延迟。
```

只有评分函数饱和后，capped replan 才是语义安全的。

---

## 20. 一句话总结

当前性能瓶颈不是 A* 本身，而是 AI 把 pathfinding 当成了大量候选动作的黑盒谓词。

应改造为：

```text
候选动作提供 hard cap、lower bound、score upper bound 和 destination；
目标集合 Dijkstra 只搜索仍可能成为最优的候选；
screening 先构建 baseline threat field，再只对可能改变结论的 blocker 做 capped replan；
所有复杂合法性仍由 exact preview 最终裁判。
```

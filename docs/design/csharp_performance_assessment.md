# C# 迁移性能评估

日期：2026-04-04

## 评估范围

本评估基于当前仓库中的主要系统脚本与 UI 脚本，重点关注以下模块：

- `scripts/systems/world_map_spawn_system.gd`
- `scripts/systems/world_map_grid_system.gd`
- `scripts/systems/battle_map_generation_system.gd`
- `scripts/ui/world_map_view.gd`
- `scripts/systems/progression_service.gd`
- `scripts/systems/profession_rule_service.gd`

本评估是结构性判断，不是基于 profiler 的实测 benchmark。

## 结论

对当前项目来说，将整个项目从 GDScript 全量迁移到 C#，整体性能提升预计不会很大。

建议预期：

- 整体项目体感提升：`0% 到 10%`
- 地图/战斗生成等纯 CPU 热点：`20% 到 60%`
- 职业成长、配置、规则判断：`接近 0%`
- UI 绘制相关：`通常接近 0%`

结论原因：

- 当前性能潜力主要集中在“生成型”和“批处理型”逻辑，而不是全局业务逻辑。
- 大部分普通玩法逻辑仍然更受 Godot 自身调用、数据结构设计和重绘策略影响。
- UI 与绘制代码通常不是被脚本语言本身卡住，而是被引擎绘制路径和重绘频率卡住。

## 分模块判断

### 1. 世界地图生成与战斗地图生成

代表文件：

- `scripts/systems/world_map_spawn_system.gd`
- `scripts/systems/battle_map_generation_system.gd`

判断：

- 这类代码有较多循环、随机生成、Dictionary 组装和临时集合分配。
- 如果未来地图规模、生成次数或战斗局部模拟复杂度继续上升，迁移到 C# 可能获得可见收益。

预估收益：

- 常见情况：`1.3x 到 2.5x`
- 如果后续进一步整理为更强类型、更少临时分配的数据结构，局部可能更高

结论：

- 这是当前项目最值得优先考虑迁移到 C# 的部分。

### 2. 网格与基础地图查询

代表文件：

- `scripts/systems/world_map_grid_system.gd`

判断：

- 当前逻辑不算重，但如果后续网格查询变为高频热点，比如大范围寻路、持续扫描、频繁 occupancy 更新，则 C# 可能有价值。

预估收益：

- 当前阶段：有限
- 变成热路径后：中等

结论：

- 先保留 GDScript，除非 profiler 明确证明它成为热点。

### 3. UI 与绘制

代表文件：

- `scripts/ui/world_map_view.gd`

判断：

- `_draw()` 相关逻辑的瓶颈更可能来自：
  - 重绘范围过大
  - 每帧绘制对象过多
  - 引擎层面的 Canvas 绘制开销
- 改为 C# 通常不会显著改善这些问题。

预估收益：

- 通常接近 `0%`

结论：

- 不建议为了性能把这类 UI 绘制代码迁到 C#。

### 4. 职业成长与规则判定

代表文件：

- `scripts/systems/progression_service.gd`
- `scripts/systems/profession_rule_service.gd`
- `scripts/systems/profession_assignment_service.gd`
- `scripts/systems/skill_merge_service.gd`

判断：

- 这些模块本质上是业务规则层。
- 调用频率低，数据规模也不大。
- 即使迁移到 C#，对帧率和体感几乎不会有明显帮助。

预估收益：

- 接近 `0%`

结论：

- 应继续保留在 GDScript。

## 比语言切换更重要的优化方向

在当前项目中，下列优化通常比“换成 C#”更有效：

- 把高频 `Dictionary` 和嵌套字典改成更稳定的 typed data 结构
- 减少大循环中的临时 `Array` / `Dictionary` 创建
- 对地图生成、路径候选、可视范围、搜索结果做缓存
- 降低 `_draw()` 的整块重绘频率与范围
- 先用 profiler 找到真实热点，再决定是否迁移

## 建议的迁移策略

不建议：

- 全项目统一迁移到 C#

建议：

- UI、配置、剧情、职业成长、普通玩法逻辑继续使用 GDScript
- 仅在 profiler 证明存在性能热点时，局部迁移以下模块：
  - 世界地图生成
  - 战斗地图生成
  - 未来可能出现的大规模寻路或批量模拟模块

## 当前优先级建议

最值得优先考虑迁移到 C# 的模块：

1. `scripts/systems/world_map_spawn_system.gd`
2. `scripts/systems/battle_map_generation_system.gd`
3. 未来若出现性能瓶颈，再考虑 `scripts/systems/world_map_grid_system.gd`

当前不建议迁移的模块：

- `scripts/ui/world_map_view.gd`
- `scripts/systems/progression_service.gd`
- `scripts/systems/profession_rule_service.gd`
- 其他以规则判定、配置读取、状态管理为主的脚本

## 后续输入建议

如果后续要继续做 C# 迁移决策，建议补充以下输入：

- Godot profiler 的实测热点数据
- 世界地图生成时长
- 战斗地图生成时长
- 世界地图 UI 重绘开销
- 中后期预计地图规模、单位数量、路径查询频率

在拿到这些数据之前，不建议仅凭语言偏好推进大规模迁移。


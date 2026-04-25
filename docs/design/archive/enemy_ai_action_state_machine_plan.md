# 敌方模板驱动的 State-Machine Action AI 方案

更新日期：`2026-04-09`

关联文档：`docs/design/project_context_units.md`

## Summary

- 敌人 AI 保持现有 `BattleCommand` 作为唯一执行协议，不改玩家手动操作链。
- 敌人思考层新增“状态机 + 可注入 action”结构，不再由 `BattleAiService` 直接硬编码“找最近敌人然后攻击/靠近”。
- action 负责产出候选命令，命令是否合法仍统一走 `BattleRuntimeModule.preview_command(...)`。
- action 的注入入口固定落在敌方模板，而不是单个战斗实例或单位临时脚本。
- 首版直接覆盖完整战斗动作，包括单体技能、地面技能、冲锋、追击、后撤与待机。

## 当前仓库事实

- 正式战斗运行时入口是 `BattleRuntimeModule.start_battle()`，AI 执行节点在 `advance()` 中的 `unit_acting` 分支。
- 当前 `BattleAiService.choose_command(...)` 直接返回 `BattleCommand`，内部逻辑只有：
  - 找最近敌人
  - 若技能可打到则放单体技能
  - 否则朝目标移动一步
  - 都失败则待机
- `BattleCommand` 已经具备 `skill_variant_id` 与 `target_coords`，说明现有命令协议已经能表达地面技能和技能形态，不需要为了 AI 另起一套执行协议。
- 玩家手动链路已经依赖 `BattleCommand + preview_command + issue_command`：
  - `WorldMapSystem` 负责选择技能、变体、目标格
  - `BattleRuntimeModule` 负责预览与执行
- `BattleRuntimeModule` 已支持：
  - 单体技能
  - 地面技能
  - cast variant
  - charge 变体
- 当前敌人构建链路只把敌人标记为 `control_mode = "ai"`，但没有承载“策略注入”的字段：
  - `BattleUnitState` 没有 `ai_brain_id`、`ai_state_id` 一类字段
  - `EncounterRosterBuilder` 只负责属性和默认技能
- `BattleRuntimeModule.setup(...)` 接收 `_enemy_templates`，敌方模板消费应保持在正式 `start_battle()` 主链内，而不是依赖额外 sidecar builder。
- 世界遭遇生成目前把 `EncounterAnchorData.enemy_roster_template_id` 直接写成 `display_name`：
  - `WorldMapSpawnSystem._build_encounter_anchor(...)`
  - 这意味着当前模板 id 不是稳定 slug，而是显示名别名。
- `WildSpawnRule` 当前已包含正式 `monster_template_id`；`monster_name` 只负责展示文案。
- `GameSession` 当前没有敌方模板或 AI brain 的正式加载/提供入口。

## 设计目标

- 让敌人的“会做什么”与“怎么思考”都由可注入 action 和状态机控制，而不是写死在单一 service 里。
- 保持战斗规则、预览、资源消耗、目标合法性仍由 `BattleRuntimeModule` 统一裁决，AI 不绕开规则层。
- 让敌方内容可以按模板复用，并能稳定绑定到世界生成出的遭遇。
- 首版就能覆盖已有战斗动作集合，避免只做一个最小壳子又马上重构。
- 保持玩家操作和 AI 操作最终都收敛到同一套 `BattleCommand`。

## 方案总览

- 引入三层 AI 结构：
  - `AI Brain`：定义状态机、状态切换规则、各状态可用 action 顺序。
  - `AI State`：当前战术状态，例如 `engage`、`pressure`、`support`、`retreat`。
  - `AI Action`：负责挑目标、挑技能、挑目标格、构造候选 `BattleCommand`。
- 决策流程固定为：
  - 读取单位当前 `ai_brain_id` 与 `ai_state_id`
  - 先跑 transition，决定是否切状态
  - 取当前 state 的 action 列表，按顺序依次求解
  - 对每个候选命令调用 `preview_command(...)`
  - 命中首个 `allowed == true` 的命令就返回
  - 全部失败则回退到 `WaitAction`
- 这里的“不同 action 会有不同思考方式”体现在：
  - action 自己决定目标筛选逻辑
  - action 自己决定站位偏好与距离带
  - action 自己决定是否追求多目标命中
  - action 自己决定是否为了某个技能先走位再出手

## 数据与接口变更

### BattleUnitState

- 新增 `ai_brain_id: StringName`
- 新增 `ai_state_id: StringName`
- 新增 `ai_blackboard: Dictionary`
- 这些字段写入 `to_dict()/from_dict()`，确保同一场战斗中的运行时状态一致。

### BattleAiService

- `choose_command(...)` 改为使用显式上下文对象，而不是直接在函数里拼所有判断。
- 新增 `BattleAiContext`，至少包含：
  - `state`
  - `unit_state`
  - `grid_service`
  - `skill_defs`
  - `preview_callback`
- 新增 `BattleAiDecision`，至少包含：
  - `command`
  - `brain_id`
  - `state_id`
  - `action_id`
  - `reason_text`
- `BattleAiService` 自己不拥有规则真源，只负责编排 brain/state/action。

### BattleRuntimeModule

- `setup(...)` 增加敌方模板与 AI brain 注入。
- 正式 `start_battle()` 路径补齐对敌方模板的读取，避免把模板消费散落到额外兼容层。
- `advance()` 在 AI 单位行动时构建 `BattleAiContext`，并把 `preview_command(...)` 暴露给 AI 作为只读验证器。
- battle log 追加 `brain/state/action` 级别的调试信息，便于验证 AI 是否按预期运作。

### 世界生成与内容入口

- `WildSpawnRule` 新增 `monster_template_id: StringName`
- `EncounterAnchorData.enemy_roster_template_id` 语义收敛为稳定模板 id，而不是显示名。
- `WorldMapSpawnSystem` 改为优先写入 `monster_template_id`。
- 当前主线不再为旧配置补 `monster_template_id` fallback。

### GameSession

- 新增敌方内容加载与 getter：
  - `get_enemy_templates()`
  - `get_enemy_ai_brains()`
- `WorldMapSystem._battle_runtime.setup(...)` 改为正式注入这两类内容，而不是始终传空字典。

## 内容组织

- 遵循仓库约束，敌方内容放在 `scripts/enemies/`。
- v1 采用“代码注册表 + 世界配置 id”的方案，不先上独立敌方 `.tres` 资源，减少本轮范围。
- 建议新增：
  - 敌方模板定义脚本
  - AI brain 定义脚本
  - AI state 定义脚本
  - AI action 基类与若干具体 action
  - 敌方内容注册表
- `data/configs/world_map/*.tres` 只负责写稳定的 `monster_template_id`，不直接承载整套 AI 细节。

## Action 模型

### 统一约束

- action 不直接改 `BattleState`。
- action 不直接扣 AP、不直接应用伤害、不直接跳过 `preview_command(...)`。
- action 的职责固定为：
  - 判断当前自己是否值得尝试
  - 生成一个候选 `BattleCommand`
  - 在必要时提供解释文本

### 首版 action 集

- `UseUnitSkillAction`
  - 面向单体技能
  - 可配置目标选择器，例如最近敌人、最低血量敌人、最低血量友军、自身
- `UseGroundSkillAction`
  - 面向地面技能与 AOE
  - 可配置最小命中数、优先命中敌方还是友方、是否接受只打中 1 个目标
- `UseChargeAction`
  - 专门处理 charge 这类有方向与距离语义的地面变体
  - 优先找可命中的冲锋终点，而不是只把它当普通 ground skill
- `MoveToRangeAction`
  - 根据指定距离带移动到技能或战术想要的位置
  - 例如贴近 1 格、保持 3 到 4 格、寻找能下回合开技能的位置
- `RetreatAction`
  - 在低血量或特定状态下优先拉开距离
  - 若无法真正后撤，则回退到其它 action 或待机
- `WaitAction`
  - 兜底 action

## 状态机模型

### 首版通用状态

- `engage`
  - 进入战斗与接敌阶段
  - 更偏向追击、贴近、找开战技机会
- `pressure`
  - 已经进入有效交战距离
  - 更偏向持续输出、AOE、压制
- `support`
  - 发现自己或友军符合辅助条件时进入
  - 更偏向治疗、增益、场地控制
- `retreat`
  - 低血量或局势不利时进入
  - 更偏向拉开距离、保命释放、待机

### 首版 transition 条件

- HP 比例阈值
- 最近敌人距离
- 是否存在可用且能通过 preview 的关键技能
- 地面技能的最少命中数是否达标
- 是否存在低血量友军

### 状态切换顺序

- 先判定强制保命条件，例如低血量进入 `retreat`
- 再判定明确支援条件，例如可治疗目标进入 `support`
- 其余情况在 `engage` 与 `pressure` 之间切换
- 一次决策只允许落在一个最终状态

## 模板示例

### `wolf_pack`

- 旧名 alias：`荒狼群`
- brain：`melee_aggressor`
- 默认状态：`engage`
- 行为偏好：
  - 优先冲锋开场
  - 进入近身后以单体技能压制最近敌人
  - 低血量时后撤 1 到 2 格再观察

### `mist_beast`

- 旧名 alias：`雾沼异兽`
- brain：`ranged_controller`
- 默认状态：`pressure`
- 行为偏好：
  - 优先找多目标地面技能
  - 若没有理想 AOE，则点杀低血量目标
  - 被逼近时后撤并维持中距离
  - 若自身或友军具备治疗窗口，则切入 `support`

## 执行流程

### 战斗开始

- 世界生成提供稳定 `monster_template_id`
- 遭遇锚点把该 id 写入 `enemy_roster_template_id`
- `BattleRuntimeModule.start_battle()` 从敌方模板注册表构建单位
- 每个敌方单位带上：
  - `ai_brain_id`
  - 初始 `ai_state_id`
  - 技能表
  - 属性快照

### 单位行动

- `BattleRuntimeModule.advance()` 发现当前单位是 AI 控制时：
  - 构造 `BattleAiContext`
  - 调用 `BattleAiService.choose_command(...)`
  - 拿到 `BattleAiDecision`
  - 若有合法命令则照常走 `issue_command(...)`
- 回合内 AI 可以多次决策，直到：
  - AP 用尽
  - 单位死亡
  - action 明确返回待机

### 生命周期

- `_activate_next_ready_unit(...)`
  - 初始化本回合所需黑板字段
  - 若单位没有合法状态则回落到 brain 默认状态
- `_end_active_turn(...)`
  - 清理 turn 级黑板
  - 不强制清空 `ai_state_id`
- 战斗结束时再整体释放 AI 运行时状态

## 测试计划

- 新增独立的 `battle_runtime` AI 回归脚本，不把状态机断言塞进现有 timed terrain smoke。
- 覆盖模板接线：
  - `monster_template_id` 能正确映射到敌方模板
- 旧 `monster_name/display_name` 不再参与模板解析
- 覆盖命令生成：
  - 单体技能 action 能产出合法 `BattleCommand`
  - 地面技能 action 能写入 `skill_variant_id + target_coords`
  - charge action 能产出合法冲锋命令
  - 追击、后撤、待机都能按预期回退
- 覆盖状态切换：
  - 高血量接敌时进入 `engage`
  - 有效交战后进入 `pressure`
  - 低血量时切 `retreat`
  - 满足治疗条件时切 `support`
- 覆盖运行时集成：
  - AI 单位一个行动回合内可以连续执行多个命令直到 AP 耗尽
  - battle log 能带出 `brain/state/action`
- 继续跑现有 `tests/battle_runtime/run_battle_runtime_smoke.gd`，确认 AI 接线不破坏当前 runtime 推进。

## 兼容与默认约束

- 首版不改玩家 `BattleCommand` 的输入方式，不重做 `WorldMapSystem` 的技能选择 UI。
- 首版不引入脚本化表达式或可执行 DSL；brain/state/action 的配置能力保持有限而稳定。
- 首版 AI 运行时黑板只保证单场战斗内有效，不作为世界存档长期真源。
- 首版敌方内容先走代码注册表，后续如果策划密度上升，再迁到 `data/configs/enemies/`。
- `enemy_roster_template_id` 以稳定模板 id 为正式值；显示名只负责展示，不参与解析。

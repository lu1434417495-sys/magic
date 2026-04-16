# 战士战棋技能系统落地方案

> 源需求文档：`docs/design/skills.md`
> 命中模型参考：`docs/design/player_growth_system_plan.md`

## 1. 结论摘要

这份 `skills.md` 不适合在当前项目里另起一套平行系统，应该直接落到现有的资源驱动战斗链路中：

- 技能定义链路沿用 `SkillDef -> CombatSkillDef -> CombatEffectDef`
- 战斗执行链路沿用 `BattleRuntimeModule -> BattleDamageResolver -> BattleGridService`
- 战斗主入口以 `scripts/systems/world_map_system.gd` 为准
- `scripts/systems/game_runtime_facade.gd` 仍需保持同等行为，因为测试与工具链还会走这条链路

本次落地采用以下边界：

- 文档中的战士技能作为正式技能体系引入
- 这些技能全部按“技能书 / 通用技能”模式接入，`learn_source = &"book"`
- 不绑定职业主动授予，不塞回 warrior 主动职业树
- 保留现有基础武器技能和职业被动
- 旧 warrior 主动技能进入兼容保留态，不再作为新内容继续扩展
- 文档中的 `aura / 斗气` 新增为独立资源，不与 `MP` 或 `Stamina` 混用

## 2. 当前项目现状

### 已有能力

- 已有技能资源结构：`SkillDef`、`CombatSkillDef`、`CombatEffectDef`、`CombatCastVariantDef`
- 已有战斗指令与预览：`BattleCommand`、`preview_command()`、`issue_command()`
- 已有地面技能、范围技能、冲锋技能雏形
- 已有 AI 决策与战斗 HUD
- 已有技能注册中心：`scripts/player/progression/progression_content_registry.gd`

### 当前缺口

- `cooldown_tu`、`stamina_cost` 已有字段，但运行时没有完整扣费与推进闭环
- 没有独立 `Aura` 资源链
- 命中仍是旧的 `hit_rate - evasion` 百分比口径，尚未切到 `THAC0 + 负 AC + d20`
- `status_effects` 目前只是松散字典，没有统一状态语义表
- 范围图形目前主要支持 `single / diamond / square / cross`，尚未完整支持文档中的 `line / cone / radius / self`
- AI 还不是按文档中的评分模型做技能候选打分

## 3. 设计落点

### 3.1 文档字段到现有结构的映射

| 设计文档字段 | 项目落点 |
| --- | --- |
| `name` | `SkillDef.display_name` |
| `range` | `CombatSkillDef.range_value` |
| `area` | `CombatSkillDef.area_pattern / area_value`，必要时补 `area_origin_mode / area_direction_mode` |
| `hit` | `CombatSkillDef.attack_roll_bonus`，语义为 2e 攻击检定修正 |
| `damage` | `CombatEffectDef.power` + 新增 `damage_ratio_percent` |
| `cost` | `CombatSkillDef.stamina_cost / aura_cost` |
| `cooldown` | `CombatSkillDef.cooldown_tu` |
| `effects` | `CombatSkillDef.effect_defs` 或 `CombatCastVariantDef.effect_defs` |
| `duration` | 状态持续时间或地形效果 `duration_tu` |

### 3.2 技能来源策略

- 文档里的 80 个技能全部注册为通用技能书技能
- warrior 仍然可以学，但不是职业专属授予
- 职业体系继续负责被动、属性倾向和少量基础授予
- 技能书投放、奖励和掉落后续再接入，不放在这次战斗落地里

### 3.3 资源策略

- 保留现有 `MP` 资源，供法系和旧技能继续使用
- 新增 `Aura` 资源，专门承接文档里的斗气系技能
- 保留 `Stamina`，承接近战体力类技能
- 战士文档技能根据描述分流到 `stamina_cost` 或 `aura_cost`

## 4. 需要新增或扩展的系统

### 4.1 数据层

需要扩展以下资源或状态对象：

- `CombatSkillDef`
  - 将 `hit_rate` 迁移为 `attack_roll_bonus`
  - 新增 `aura_cost`
  - 新增 `area_origin_mode`
  - 新增 `area_direction_mode`
  - 新增 `ai_tags`
  - 新增 `roll_disposition`，支持 `normal / advantage / disadvantage`
- `CombatEffectDef`
  - 新增 `damage_ratio_percent`
  - 新增 `forced_move_mode`
  - 新增 `forced_move_distance`
  - 新增 `stack_limit`
  - 新增 `bonus_condition`
  - 新增 `trigger_event`
- `BattleUnitState`
  - 新增 `current_aura`
  - 规范化 `cooldowns`
  - 规范化 `status_effects`
- `BattleState`
  - 新增 `attack_roll_nonce`
  - 作为 battle-seeded 攻击检定随机游标，保证 headless 回归稳定
- `BattlePreview`
  - 新增结构化命中预览
  - 至少包含 `attack_previews / average_hit_chance_percent / expected_hit_score / roll_disposition`

建议新增 `BattleHitResolver`，作为战斗命中真相源，统一承接：

- `THAC0 + 负 AC + d20`
- `natural 1 / natural 20`
- 优势 / 劣势双骰逻辑
- 预览命中率与执行时实际掷骰
- 多段攻击与多目标技能的逐目标独立命中

建议新增 `StatusDef` 注册表，集中定义以下状态语义：

- `bleeding`
- `staggered`
- `armor_break`
- `taunted`
- `rooted`
- `disarmed`
- `knockup_delay`
- `guarding`
- `counter_stance`
- `damage_reduction_up`
- `attack_up`
- `evasion_up`

### 4.2 属性与角色构建

需要让以下链路同时认识 `Aura` 与新的命中属性：

- `AttributeService`
- `CharacterManagementModule`
- `BattleUnitState`
- `GameSession` 存档序列化
- 战斗 HUD 展示

命中属性正式切换为：

- `thac0`
  - 数值越低越容易命中
- `armor_class`
  - 采用 AD&D 2e 的降序 AC
  - `10` 较脆，`0` 较难命中，`-1 / -2 / -5` 更强

玩家单位默认走派生公式：

```text
thac0 = clamp(21 - 2 * perception - agility, -10, 30)
armor_class = clamp(10 - agility - floor(perception / 2), -20, 10)
```

敌方与 fallback 单位不复用玩家派生公式，统一在模板 / 工厂里显式写入：

- `thac0`
- `armor_class`

默认规则：

- `aura_max` 默认为 0
- 只有属性、装备、被动或技能显式授予时，单位才拥有 Aura
- `current_aura` 初始等于 `0`
- 不再维护 `hit_rate / evasion` 第二套正式运行时语义
- 装备、技能、测试夹具统一迁移到 `thac0 / armor_class / attack_roll_bonus`

### 4.3 战斗执行层

`BattleRuntimeModule` 需要补全统一执行顺序：

1. 校验指令和目标
2. 校验 AP / Stamina / Aura / 冷却
3. 扣资源
4. 写入冷却
5. 结算命中
6. 结算伤害 / 治疗 / 状态 / 地形 / 强制位移
7. 处理击杀、行动刷新、追击、日志

需要补的核心能力：

- 技能冷却按 TU 推进
- 状态持续时间按 TU 或行动结束推进
- `THAC0 + 负 AC + d20` 命中结算
- 强制位移：击退、拉拽、击飞、后撤、跳斩落点
- 条件伤害：低血加成、无视护甲、暴击强化、斩杀刷新

正式命中口径固定为：

```text
required_roll =
    attacker_thac0
    - target_armor_class
    - skill_attack_roll_bonus
    - situational_bonus

if d20 == 1: miss
elif d20 == 20: hit
elif d20 >= required_roll: hit
else: miss
```

补充规则：

- 命中预览使用精确枚举 20 面结果，输出最终命中率
- hostile unit 的 unit skill / ground skill / multi-unit skill / repeat attack，全部逐目标独立检定
- self / ally / heal / buff 不走攻击检定
- `repeat_attack` 每一段都单独掷骰，并继续吃阶段命中惩罚
- 执行日志记录 `d20`、`required_roll`、命中结果和优势 / 劣势状态
- 攻击检定随机数必须使用 battle-seeded deterministic RNG，避免回归不稳定

现有状态在本轮先补最小稳定语义：

- `archer_pre_aim`
  - 固定提供攻击检定 `+2`
- `evasion_up`
  - 固定提供 `armor_class -2`

优势 / 劣势本轮只做规则骨架：

- `CombatSkillDef.roll_disposition`
- `BattleHitResolver` 的双骰取高 / 取低
- `BattlePreview` 的展示字段

本轮不接入：

- 高地
- 掩体
- 包夹
- 贴身远程惩罚
- 其他战场来源表

### 4.4 范围与选点

`BattleGridService` 需要正式支持：

- `self`
- `line`
- `cone`
- `radius`

落地规则固定如下：

- `self` 以施法者占位为中心
- `line` 方向由施法者到目标格的向量确定
- `cone` 方向由施法者到目标格的向量确定
- `radius` 以目标格为中心
- 不引入单位朝向系统

### 4.5 AI

`BattleAiService` 改为候选技能评分制，优先级固定为：

1. 击杀目标
2. 控制高威胁单位
3. 保命
4. 位移接敌
5. AOE 最大收益

目标评分模型落为：

`击杀价值 + 威胁值 + 命中收益 + 地形收益 - 风险`

其中必须接入的技能语义：

- `挑衅` 会改变敌方目标选择
- 控制技能优先打高威胁单位
- 治疗/防御技能进入低血保命窗口
- 位移技能要同时评估接敌收益和落点风险

命中系统接入后的 AI 规则固定如下：

- `BattlePreview.expected_hit_score` 进入候选打分
- `0%` 命中的候选不进入最终选择
- 地面技能从“预计命中人数”改为“期望命中人数”

### 4.6 UI

`BattleHudAdapter` 与 `BattleMapPanel` 需要补充：

- Aura 展示
- 技能命中率展示
- `需 X+` 的 2e 点数说明
- `normal / advantage / disadvantage` 的当前掷骰状态展示
- 技能 CD 展示
- 技能资源不足禁用态
- 范围预览说明
- 状态与冷却数量之外的具体状态提示

本轮 UI 反馈固定为：

- 主展示用“最终命中率”
- 次级文案补“需掷出 X+”
- 日志保留完整 `d20` 明细
- multi-unit 技能显示“平均命中率”

### 4.7 迁移与兼容

为避免两套命中体系并存，文档口径固定如下：

- `CombatSkillDef.hit_rate` 迁移为 `attack_roll_bonus`
- 旧技能 `hit_rate` 数值按 `/ 5` 四舍五入迁移为固定攻击检定修正
- `repeat_attack_until_fail.base_hit_rate` 迁移为 `base_attack_bonus`
- `repeat_attack_until_fail.follow_up_hit_rate_penalty` 迁移为 `follow_up_attack_penalty`
- `AttributeService.HIT_RATE / EVASION` 正式迁移为 `THAC0 / ARMOR_CLASS`
- 装备与测试夹具直接改到新字段，不保留运行时映射层
- 本轮不升 `SAVE_VERSION`

## 5. 分阶段落地

### 阶段一：规则底座

目标：先让文档字段在现有系统中“有真实语义”。

本阶段完成：

- Aura 资源链
- `BattleHitResolver`
- `THAC0 / 负 AC / d20`
- `BattlePreview` 命中预览扩展
- 冷却推进
- 状态注册表
- 强制位移框架
- 范围图形扩展
- AI 评分骨架
- HUD 资源与 CD 展示

### 阶段二：Demo 技能池

先实现文档推荐的 15 个技能：

- 重击
- 横扫
- 穿刺
- 裂甲斩
- 断头斩
- 冲锋
- 跳斩
- 后撤步
- 格挡
- 护盾墙
- 战斗回复
- 盾击
- 挑衅
- 战吼
- 真龙斩

这批技能用于验证：

- 单体输出
- 范围攻击
- 位移攻击
- 防御姿态
- 治疗
- 控制
- 团队增益
- 斗气资源

### 阶段三：补齐 80 技能

原则：

- 不再为单个技能新增临时 if/else 分支
- 全部通过统一 `effect/status/shape/condition` 模板继续扩展
- 连击类统一走 `combo_state`
- 控制类统一走状态语义
- 位移类统一走强制位移与特殊落点校验

## 6. 与现有内容的兼容策略

- 旧 warrior 主动技能不直接删除
- 旧技能 ID 继续保留在注册表里，避免旧存档或旧进度崩溃
- 新版本不再继续扩展旧 warrior 主动技能目录
- 新内容全部走通用技能书技能池
- 基础武器技能和职业被动维持现状

## 7. 推荐文件落点

核心修改会集中在以下区域：

- `scripts/player/progression/`
- `scripts/systems/`
- `scripts/ui/`
- `docs/design/`

其中最关键的文件会是：

- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/combat_effect_def.gd`
- `scripts/player/progression/progression_content_registry.gd`
- `scripts/systems/battle_hit_resolver.gd`
- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_grid_service.gd`
- `scripts/systems/battle_ai_service.gd`
- `scripts/systems/world_map_system.gd`
- `scripts/ui/battle_hud_adapter.gd`
- `scripts/systems/game_runtime_snapshot_builder.gd`
- `scripts/utils/game_text_snapshot_renderer.gd`

## 8. 测试清单

### 规则测试

- `THAC0 / 负 AC / d20` 是否正确生效
- `natural 1 / natural 20` 是否正确生效
- 优势 / 劣势是否按双骰逻辑结算
- Aura / Stamina / AP / CD 是否正确校验与扣除
- 状态持续、刷新、叠层、控制递减是否正确
- 击退、拉拽、跳斩、后撤是否正确更新坐标与占位
- 击杀刷新行动是否只在满足条件时触发
- repeat attack 的阶段命中惩罚与逐段掷骰是否正确
- deterministic RNG 是否保证相同 battle seed 的回归稳定

### 技能测试

- 每个 Demo 技能至少覆盖“合法释放”与“非法目标”两类用例
- `挑衅` 必须验证 AI 转火
- `格挡 / 护盾墙` 必须验证减伤
- `断头斩` 必须验证低血增伤
- `真龙斩` 必须验证直线大范围预览与命中
- multi-unit 技能必须验证平均命中率与逐目标预览
- self / ally 技能必须验证不会错误走攻击检定

### UI 测试

- 技能槽禁用态是否正确反映 AP / Stamina / Aura / CD
- 选点技能是否正确显示选点进度
- 形态切换后预览是否正确刷新
- Aura 与状态展示是否可读
- 单目标命中率、`需 X+`、优势 / 劣势标记是否可读
- 文本快照与 headless snapshot 是否能读到命中预览摘要

### AI 测试

- 击杀优先
- 控制高威胁优先
- 低血保命逻辑
- AOE 最大收益逻辑
- 位移接敌逻辑
- 0% 命中的候选是否被正确排除

## 9. 本方案的默认约束

- 本轮不做新的技能树 UI
- 本轮不做技能书掉落/商店/奖励完整投放系统
- 本轮只完成战斗内可用、可学、可显示、可测试的技能系统
- 运行时主逻辑以 `world_map_system.gd` 为准，但 `game_runtime_facade.gd` 必须保持同步
- 本轮不做 saving throw、暴击重构、掩体 / 高地 / 包夹命中来源
- 优势 / 劣势只保留接口与预览展示，不在本轮展开战场规则表

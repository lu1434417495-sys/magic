# 战斗伤害与减伤子系统设计

日期：2026-04-19
关联文档：
- `docs/design/archive/2026-04-19-battle-reference-source-constraints-design.md`
- `docs/design/archive/battle_system_known_issues.md`
- `docs/design/dnd35e_combat_system_vision.md`

覆盖模块：
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_status_semantic_table.gd`
- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/battle_ai_score_service.gd`

## Problem

当前 battle 伤害流水线存在三个结构性问题：

1. 防御侧是多层百分比叠乘，而且每一层都先 `round` 再 `maxi(..., 1)`，导致 `ISSUE-BATTLE-05` 中描述的“叠减伤被系统性压平”。
2. `guarding`、`damage_reduction_up`、元素抗性都被压在同一条“后置百分比乘区”里，语义边界不清，不符合 `PF2e` 的分类承伤思路，也不符合 `3.5e / PF1e` 的 `DR / resistance` 题型表达。
3. 现有技能和 AI 已经依赖 `BattleDamageResolver` 的结果。如果只改 UI 文案或只改单个技能，会让 preview、runtime、AI 的估值分裂。

需要一套正式的伤害与减伤规格，把“攻击侧增幅”和“防御侧减免”拆开，并把现有状态迁移到清晰的分类上。

## Goals

- 修复 `ISSUE-BATTLE-05`，消除中间步骤反复 floor 到 1 的错误语义。
- 建立一条 battle 正式伤害流水线，让 runtime、preview、AI 共用同一口径。
- 让 `PF2e` 风格的分类承伤成为骨架：`IMMUNE / HALF / NORMAL / DOUBLE` 与固定值减伤分层。
- 允许局部借用 `3.5e / PF1e` 的 `DR X / bypass tag`，但仅作为显式内容扩展。
- 尽量复用现有字段和状态名，避免一次性重写全部内容资源。

## Non-goals

- 不在本轮引入完整的 `Shield Block` 反应子系统。
- 不重写现有 `BattleHitResolver` 命中流程。
- 不一次性重做所有技能倍率、职业平衡和敌人模板。
- 不把所有百分比增幅立即清零；攻击侧倍率允许阶段性保留。

## Constraints

- 伤害真相源继续放在 CU-16 的纯规则层，以 `BattleDamageResolver` 为 owner。
- preview、runtime、AI 必须读取同一条伤害流水线。
- `BattleStatusSemanticTable` 继续持有状态的 stack / duration / tick 真相，不允许伤害逻辑私下定义 stack 规则。
- 现有 `damage_ratio_percent`、`runtime_pre_resistance_damage_multiplier`、`guarding`、`damage_reduction_up` 需要兼容过渡。

## Current snapshot

当前 `_resolve_damage_amount()` 的结构大致为：

1. `power + scaling - defense` 得到基础伤害，并立刻保底为 `1`。
2. 依次乘攻击侧增幅和目标侧减伤。
3. 每一步都 `round`，再 `maxi(..., 1)`。
4. 返回最终整数伤害。

这个结构的问题不是“有没有最低伤害保底”，而是：

- 攻防双方的语义被揉成了一条连续百分比链。
- 中间步骤的离散化误差会放大。
- 只要命中过，最终就几乎必定至少 1 点。

## Proposed approach

正式采用“攻击侧单次离散化 + `IMMUNE / HALF / NORMAL / DOUBLE` 先结算 + 固定值减伤后结算”的三段式流水线。

### 1. 总体结构

正式伤害结算顺序：

1. 计算基础伤害 `base_damage`。
2. 聚合攻击侧倍率，得到 `offense_multiplier`。
3. 对攻击侧结果只做一次取整，得到 `rolled_damage`。
4. 先应用防御侧百分比档位：`IMMUNE / HALF / NORMAL / DOUBLE`。
5. 再应用固定值减伤：`all_damage_reduction / DR / guard block`。
6. 最终只在末尾做一次下限夹取，默认 `MIN_DAMAGE_FLOOR = 0`。

正式公式：

```text
base_damage = max(power + scaling_term - defense_term, 0)

offense_multiplier =
	pre_resistance_multiplier
	* bonus_condition_multiplier
	* attacker_status_multiplier
	* target_exposed_multiplier

rolled_damage = max(round(base_damage * offense_multiplier), 0)

if target mitigation tier for damage_tag == immune:
	return 0

if target mitigation tier for damage_tag == half:
	rolled_damage = floor(rolled_damage / 2)

if target mitigation tier for damage_tag == double:
	rolled_damage = rolled_damage * 2

mitigated_damage =
	rolled_damage
	- all_damage_resistance
	- physical_dr_if_applicable
	- guard_block_if_applicable

final_damage = max(mitigated_damage, MIN_DAMAGE_FLOOR)

shield_absorbed = min(final_damage, current_shield_hp)
hp_damage = final_damage - shield_absorbed
```

说明：

- 攻击侧允许继续保留倍率表达，但只能在“攻击阶段”统一乘一次。
- 防御侧百分比减伤不再接受连续数值，只允许 `IMMUNE / HALF / NORMAL / DOUBLE` 四档枚举。
- `IMMUNE / HALF / NORMAL / DOUBLE` 先于固定值减伤结算，用于拉大“正确类型/错误类型”的差距。
- `DOUBLE` 与 `HALF` 同属离散档位，用于表达“易伤”而不是固定值追加伤害。
- 固定值减伤正式独立于百分比档位，负责细粒度调参和内容深度。
- 当前版本不再使用“固定值 weakness”作为正式主链语义；易伤统一进入 `DOUBLE` 档位。
- 若目标拥有护盾层，则 `final_damage` 先由 `shield_hp` 吸收，剩余值以 `hp_damage` 形式进入 `current_hp`；护盾层位于“伤害已完成结算、生命尚未扣除”的后置吸收阶段，不属于减伤乘区。

### 2. 伤害类型与标签

伤害正式引入 `damage_tag` 概念，用于驱动 `IMMUNE / HALF / DOUBLE` 与 `DR bypass`。

首批正式 tag：

- `physical_slash`
- `physical_pierce`
- `physical_blunt`
- `fire`
- `freeze`
- `lightning`
- `negative_energy`

同时引入独立的防御档位枚举 `mitigation_tier`：

- `normal`
- `half`
- `double`
- `immune`

正式约束：

- 同一次伤害最多只取一个百分比档位。
- `IMMUNE` 永远最高优先级。
- `HALF` 与 `DOUBLE` 同时存在时互相抵消，结果回到 `NORMAL`。
- `HALF + HALF` 不继续叠成 `QUARTER`；`DOUBLE + DOUBLE` 不继续叠成 `QUADRUPLE`。
- 除抵消规则外，不允许任意连续百分比叠乘。

过渡规则：

- `effect_def` 应显式提供 `damage_tag`。
- 若历史或临时内容缺少 `damage_tag`，resolver 只做保守默认：归为 `physical_slash`，并由内容校验 / 回归推动补齐。
- 不再保留旧抗性属性入口作为伤害类型推断或抗性读取来源。

### 3. 攻击侧倍率

攻击侧倍率仍允许存在，但只允许进入 `offense_multiplier` 聚合阶段。

首批保留项：

- `runtime_pre_resistance_damage_multiplier`
- `damage_ratio_percent`
- `attack_up`
- `archer_pre_aim`
- `armor_break`
- `marked`

阶段性约束：

- 这些效果可以继续以倍率形式存在，用于避免第一轮就重写全部技能内容。
- 但它们不允许和防御侧减免继续混成同一条链。
- 长期方向是把其中一部分迁回更明确的语义，例如：
  - `armor_break` 更偏向降低防御或削减 `DR`
  - `marked` 更偏向命中收益或 `DOUBLE` 触发器

### 4. 防御侧减免分类

#### 4.1 `damage_reduction_up`

`damage_reduction_up` 的正式语义改为“全伤害固定抗性”，不再表示百分比乘区。

推荐口径：

- `power 1`：提供小额 `all_damage_resistance`
- `power 2+`：按状态强度线性提升固定减免值
- 当前实现口径先锁定为：`power 1 = 2`，后续再做数值回标。

设计意图：

- 让它成为可预期的“缓冲层”
- 与元素抗性同属固定值减免，但来源不同
- 不再和 `guarding` 连续叠乘
- 在 M1 期默认通过 modifier taxonomy 与 `guarding` 隔离，避免白送线性叠加

补充约束：

- `damage_reduction_up` 不再作为 `warrior_shield_wall` 的长期承载效果。
- 若某技能的目标语义是“先吃伤害的独立护盾池”，必须走 `shield_hp`，不能继续包装成固定减伤。

#### 4.1.1 `shield_hp` / 护盾池

对“团队保护但不想继续走减伤”的技能，正式引入护盾值语义。

正式规则：

- 护盾是固定值吸收池 `shield_hp`，不是百分比，也不是隐藏乘区。
- 护盾吸收发生在 `final_damage` 产出之后、`current_hp` 扣减之前。
- 护盾先吃伤害，剩余伤害才进入 `current_hp`。
- 同类护盾默认“取更高值并刷新持续时间”，不做线性叠加。
- 护盾的获得、吸收、耗尽都需要独立 log / preview 表达。

内容锁定：

- `priest_aid` 是正式的牧师系团队保护技能，原 `warrior_shield_wall` 已迁到该 id。
- 保留原几何骨架：以施法者为中心 `radius 1`，目标为友军，持续 `60 TU`。
- 首个正式内容按 D&D 2 环神术口径落地，数值参考 `3.5e Aid` 的 temporary hp 读感：`shield_hp = 1d8 + 3`。
- 群体版本按“单次施法共享一组骰值”结算，不对每个友军分别重掷。
- 不允许把这条技能继续实现成 `damage_reduction_up` 或其他减伤乘区替身。

设计意图：

- 把战士的“自我格挡”与牧师的“团队庇护”明确拆开。
- 让 `warrior_guard` 保持武技型物理防御身份。
- 让团队保护更接近 `temporary hp / holy ward` 的读感，而不是近战 stance 减伤。

#### 4.1.2 `shield_hp` 的 M1 数据模型

`shield_hp` 在 M1 阶段不做“多层护盾数组”，而是作为 `BattleUnitState` 的一组一等字段存在。

原因：

- 当前 `BattleUnitState` 已经以顶层资源字段承载 `current_hp / current_mp / current_stamina / current_aura / current_ap`。
- M1 目标是先把“护盾池先吃伤害”稳定落地，而不是一次引入多层护盾排序系统。
- 现阶段正式内容只明确需要 1 条团队护盾主线；先做单护盾池，后续若真有多来源并存需求，再升格为独立 layer 结构。

M1 正式字段：

- `current_shield_hp: int`
- `shield_max_hp: int`
- `shield_duration: int`
- `shield_family: StringName`
- `shield_source_unit_id: StringName`
- `shield_source_skill_id: StringName`
- `shield_params: Dictionary`

字段语义：

- `current_shield_hp`
  - 当前剩余护盾值。
  - 伤害结算完成后优先被扣减。
- `shield_max_hp`
  - 本次护盾池的原始最大值，供日志、UI、刷新规则和调试使用。
- `shield_duration`
  - 剩余持续时间，单位沿用 battle timeline 的 `TU`。
  - `-1` 表示无护盾。
- `shield_family`
  - 护盾的正式家族键，用于判断“同类护盾刷新”。
  - 例如未来可用 `holy_barrier`、`arcane_aegis`。
- `shield_source_unit_id`
  - 当前护盾来源单位。
- `shield_source_skill_id`
  - 当前护盾来源技能。
- `shield_params`
  - 预留扩展字段。
  - M1 不要求承载核心数学，只用于记录可选表现信息，例如颜色、音效 key、文案 tag。

M1 正式不变量：

- `current_shield_hp <= 0` 时，视为“无护盾”。
- “无护盾”状态下必须同时满足：
  - `current_shield_hp = 0`
  - `shield_max_hp = 0`
  - `shield_duration = -1`
  - `shield_family = &""`
  - `shield_source_unit_id = &""`
  - `shield_source_skill_id = &""`
  - `shield_params = {}`
- `current_shield_hp` 不得大于 `shield_max_hp`。
- `shield_max_hp > 0` 时，`shield_duration` 必须为正数。

M1 持有与 owner 约束：

- `BattleUnitState` 是护盾池的正式真相源 owner。
- `BattleDamageResolver` 只负责消耗 `current_shield_hp`，不负责定义护盾刷新规则。
- `BattleRuntimeModule` 负责：
  - 技能施放时写入/刷新护盾字段
  - M1 不接入按 TU 推进的自动递减；`shield_duration` 先作为写入 / 刷新 / 展示元数据保留
  - 当前只在“被新护盾覆盖”或“被伤害耗尽”时改写 / 清空护盾字段
- `BattleStatusSemanticTable` 不拥有护盾池本体；M1 不把护盾值塞进 `status_effects.params`。

M1 叠加 / 刷新规则：

- 每个单位同一时刻只允许存在 1 个正式护盾池。
- 新护盾施加时：
  - 若目标当前无护盾：直接写入全部字段。
  - 若 `shield_family` 相同：
    - `shield_max_hp` 取更高值。
    - `current_shield_hp` 取更高值。
    - `shield_duration` 刷新为更长值。
    - `shield_source_unit_id / shield_source_skill_id` 更新为最新来源。
  - 若 `shield_family` 不同：
    - M1 默认仍不并存。
    - 比较“当前剩余护盾值”和“新护盾值”，保留更高者。
    - 若数值相同，保留剩余时间更长者；若仍相同，保留最新施加者。
- M1 不支持不同 family 的护盾分层共存；若后续确实需要，再引入 `BattleShieldLayerState`。

M1 序列化约束：

- 上述 7 个字段都需要进入 `BattleUnitState.to_dict()/from_dict()`。
- 缺失这些字段的旧 battle payload 必须回退到“无护盾”默认值。
- 这组字段属于 battle runtime state，不进入 `attribute_snapshot`。

M1 与日志 / preview 的最小联动字段：

- 伤害结算结果至少需要能向上游暴露：
  - `damage`
  - `shield_absorbed`
  - `hp_damage`
  - `shield_broken`
- 这样 `BattleRuntimeModule` 才能稳定输出：
  - “护盾吸收 X 点伤害”
  - “护盾被击碎”
  - “剩余 Y 点伤害穿透到生命”

#### 4.1.3 resolver 护盾结果 contract（M1 锁定）

M1 正式锁定 `BattleDamageResolver` 的护盾相关返回字段语义，避免 `damage` 同时代表“总伤害”和“进血伤害”。

正式字段：

- `damage`
  - 含义：**对 HP 造成的实际伤害**
  - 等价于 `hp_damage`
  - 这是对现有调用方的正式重定义
- `shield_absorbed`
  - 含义：被护盾池吸收的伤害
- `hp_damage`
  - 含义：穿过护盾后实际进入 `current_hp` 的伤害
- `shield_broken`
  - 含义：本次结算后护盾是否被打空

M1 正式约束：

- `damage == hp_damage`
- resolver 在 M1 不额外返回“总结算伤害”字段
- 若上层确实需要“本次命中的最终结算总伤害”，必须显式使用：
  - `shield_absorbed + hp_damage`
- 击杀判定、战斗日志中的“掉血 X 点”、战后 HP 统计，一律读取 `damage`
- 护盾吸收表现、护盾价值统计、护盾破裂提示，一律读取 `shield_absorbed / shield_broken`

这样做的原因：

- `damage` 绑定 HP 实损后，旧调用方最不容易误把“被护盾吃掉的伤害”当成已掉血。
- “是否击倒目标”与“造成了多少 HP 伤害”天然同口径。
- 若未来要单独统计“总命中伤害”或“护盾吸收贡献”，可在调用层按现有字段组合，不需要在 M1 先引入第二个歧义总量字段。

#### 4.2 `guarding`

`guarding` 的最终目标语义改为 `hardness-like block`，而不是晚到的伤害乘区。

正式目标：

- `guarding` 提供一次或一段时间内的格挡值 `guard_block`
- 在直接命中伤害进入防御阶段后扣除
- 只作用于 `physical_slash / physical_pierce / physical_blunt`
- 对火焰、寒冷、闪电、负能量等法术/能量伤害不生效
- 可以与 `damage_reduction_up` 共存，但语义不同

阶段性落地策略：

- **M1**：`guarding` 先视为较高的固定 `physical_dr` / `guard_block_value`，作为持续 stance 的最小实现。
- **M2**：随着 `damage_tag` 与 `DR` 引入，把 `guarding` 约束为物理向减免，不再是泛化的后置护甲乘区。
- **M3**：`guarding` 默认演化为“每回合首次受击自动触发一次 block”的消费式语义。
- “反应式 block”保留为更远期目标；本 spec 范围内不要求引入独立 reaction 资源。
- 当前实现口径先锁定为：`power 1 = 4` 的固定物理减伤。

补充约束：

- `guarding` 在 M1 期虽然数学上表现为固定减伤，但它是 M3 消费式 block 的前置阶段，不是替代方向。
- 文档层面不再把它定义为百分比减伤。
- `guarding` 的 M1 口径默认是固定物理减伤，不允许实现成全伤害减伤。

#### 4.2.1 `warrior_guard` 技能锁定要求

`warrior_guard` 在本 spec 中单独锁定为物理防御技能：

- 迁移后只提供 `guarding`
- 不再附带 `damage_reduction_up`
- 只对物理伤害生效，对法术/能量伤害没有任何效果

设计意图：

- 让 `warrior_guard` 明确承担“近战 / 物理防御姿态”的角色。
- 不让它同时覆盖“泛用法术减伤”，避免和元素抗性、`HALF / IMMUNE`、团队减伤 buff 发生定位重叠。
- 把 `warrior_guard` 与 `priest_aid` 的职责拆开，前者偏自身物理防御，后者负责牧师系团队护盾。

#### 4.3 `DR X / bypass tag`

`DR` 不是普战默认机制，而是 `3.5e / PF1e` 风格的显式内容扩展。

首版支持目标：

- `DR X/—`
- `DR X/magic`

后续扩展：

- `DR X/silver`
- `DR X/cold_iron`

正式规则：

- `DR` 只对物理伤害生效
- 若攻击满足 bypass tag，则该层不生效
- `DR` 与元素抗性、全伤害抗性可共存，但都按固定值顺序扣减

#### 4.4 `IMMUNE / DOUBLE`

新增正式概念：

- `immunity(tag)`：该伤害类型直接 0
- `double(tag)`：该伤害类型先翻倍，再进入固定值减伤序列

目的：

- 让内容设计获得 `PF2e` 风格的清晰分类
- 让“易伤”与 `IMMUNE / HALF / NORMAL / DOUBLE` 处于同一层，形成对称的离散百分比档位
- 避免所有克制都只能通过攻击侧倍率或固定值追加伤害实现

冲突规则：

- `IMMUNE` 覆盖一切，直接得到 0 伤害。
- `HALF` 与 `DOUBLE` 同时存在时，结果回到 `NORMAL`。
- 当前正式主链不支持“固定值 weakness”与 `DOUBLE` 并存；若后续确需引入，必须以单独 spec 重新定义。

### 4.5 最小 modifier taxonomy（M1 前置）

为避免 `guarding`、`damage_reduction_up` 和 `DR` 在 M1 期直接线性叠加，M1 落地前需要补最小减伤 taxonomy。

首批分类：

- `buff_reduction`
- `stance_reduction`
- `content_dr`
- `guard_block`

首批映射：

- `damage_reduction_up` -> `buff_reduction`
- `guarding` 的 M1 持续 stance 形态 -> `stance_reduction`
- `DR X/bypass` -> `content_dr`
- `guarding` 的 M3 消费式 block 形态 -> `guard_block`

正式约束：

- 同一 modifier type 默认不线性叠加，取最大值，除非显式标注可叠加。
- 不同 modifier type 可以按固定减法序列共存。
- 若某技能同时施加多个减伤状态，必须显式说明它们属于不同 type，否则默认按同类处理。

### 5. 最低伤害下限

正式默认值：

- `MIN_DAMAGE_FLOOR = 0`

原因：

- `PF2e` 和 `3.5e / PF1e` 风格下，命中了但被完全吃掉，是合法且有辨识度的结果。
- 全局最低伤害 1 会再次把固定值减伤、`DR`、`guard block` 的价值压平。

补充约束：

- 如果某个技能或效果确实需要“至少造成 1 点”，必须由显式规则表达，例如：
  - `chip_damage_min = 1`
  - `true_damage`
- 不允许再用全局 floor 替代内容语义。
- 在 `0` 伤害的 log / preview 显式表达接入前，`MIN_DAMAGE_FLOOR = 0` 不得合入正式分支。

### 6. AI / preview contract

`BattleAiScoreService` 与 preview 必须读取同一套伤害结果。

正式要求：

- AI 评分中的 `estimated_damage` 使用新的完整流水线。
- preview 的预估伤害使用同一 resolver，不再自行拼装减伤显示。
- 当结果为 `0` 时，preview 必须有显式提示，例如“被抗性/格挡完全吸收”。
- `HALF / DOUBLE / IMMUNE` 的来源必须在 preview 中可读，不允许只在 runtime 暗中生效。

### 7. 日志与表现

一旦允许 `0` 伤害，battle log 必须新增显式口径：

- 命中且造成正伤害：沿用“造成 X 伤害”
- 命中且因 `DOUBLE` 受击：允许追加“触发易伤”类提示
- 命中但被完全吸收：显示“命中，但被格挡/抗性吸收”
- 免疫：显示“免疫该伤害”

否则会出现“命中了但战报静默”的可读性问题。

这是 M1 的硬前置条件，不是可选优化项。

## Migration plan

### M1：修复结构性 bug，不改太多内容表达

- 先补最小 modifier taxonomy，至少覆盖 `buff_reduction / stance_reduction / content_dr / guard_block`
- 去掉中间步骤的 `maxi(..., 1)` 和重复 `round`
- 攻击侧倍率聚合一次
- 防御侧百分比档位改为 `IMMUNE / HALF / NORMAL / DOUBLE`
- 固定值减伤在 `IMMUNE / HALF / DOUBLE` 之后结算
- `damage_reduction_up` 从百分比重解释为固定值减免
- `guarding` 从百分比重解释为 guard block / 临时 fixed mitigation
- `warrior_guard` 迁移后只保留 `guarding`，移除自带 `damage_reduction_up`
- `priest_aid` 作为正式牧师系范围护盾技能保留 `radius 1 / 60 TU` 骨架，效果改为 `shield_hp`
- 在 battle state / preview / log contract 中补正式护盾层，不允许用 `damage_reduction_up` 模拟团队护盾
- 同步接入 `0` 伤害、免疫、被吸收三种 log / preview 表达
- 在数值重解释前准备 `damage_reduction_up / guarding / shield_hp` 的校准基准

### M2：补充正式内容标签

- 为伤害效果补 `damage_tag`
- 为单位或状态补 `mitigation_tier`
- 为单位状态或快照补 `IMMUNE / DOUBLE / DR`
- 让 preview 和 AI 同步展示/估值这些标签
- 审计攻击侧倍率叠乘上限，若发现爆表组合，先做局部限幅

### M3：收敛遗留倍率语义

- 审核 `armor_break`、`marked`、`attack_up`、`archer_pre_aim`
- 把不适合长期保留为倍率的效果迁回更清晰的规则位置
- 把 `guarding` 从 M1 的 stance 减伤形态迁到“每回合首次受击自动触发一次 block”的消费式语义

## Alternatives considered

### Option A：只修 ISSUE-BATTLE-05，不改减伤语义

做法：

- 中间步骤保留 float
- 最终只做一次 `round`
- 仍保留百分比抗性、百分比 `damage_reduction_up`、百分比 `guarding`

优点：

- 改动最小
- 现有内容数值最容易平移

缺点：

- 只是修了量化误差，没有修“语义混乱”
- 后续仍会把 `PF2e` 和 `3.5e / PF1e` 风格挤进同一条乘法链

未选原因：

- 不能作为长期正式伤害规格

### Option B：攻击侧倍率 + `IMMUNE / HALF / NORMAL / DOUBLE` + 固定减伤分层

做法：

- 攻击侧保留倍率
- 防御侧先结算 `IMMUNE / HALF / NORMAL / DOUBLE`
- 再分类为抗性、DR、block

优点：

- 能先修结构错误，再逐步迁移内容
- 与现有代码和技能资源兼容性最好
- 符合参考源分流约束
- 能显著拉大“正确类型/错误类型”的差距
- 能让“易伤”与“减半/免疫”处于同一层，降低概念割裂感

缺点：

- 需要一轮数值重标定
- `guarding` 的最终消费式 block 可能分阶段实现

选用原因：

- 是当前项目最稳的正式方案

### Option C：完整改成纯 3.5e / PF1e DR 系统

优点：

- 题型感和 bypass 价值最强

缺点：

- 复杂度显著上升
- 不适合作为 battle 默认骨架

未选原因：

- 与当前“PF2e 做骨架”的总约束冲突

## Risks

- 元素抗性从百分比改为固定值后，现有法系内容会出现一轮显著重标定。
- `guarding` 若只做“临时 fixed mitigation”过渡，而没有后续 block 消费语义，玩家体感可能仍偏旧系统。
- `MIN_DAMAGE_FLOOR = 0` 后，日志和 preview 若不跟上，会让玩家误以为技能失效。
- 若旧百分比字段直接按同值迁成固定值，普通敌人和 boss 的承伤会发生断崖式失真。
- `HALF` 与固定减伤同层组合后，小伤害和多段伤害更容易被完全吃掉；若缺少 stacking discipline，泛用防御可能过强。
- `DOUBLE` 会进一步放大攻击侧倍率链，若不做上限审计，某些 build 可能在正确克制场景下爆表。
- 攻击侧倍率仍处于聚合阶段，若不做上限审计，防御侧修复后可能暴露新的高爆发组合。

## Open questions

- `damage_reduction_up` 是否要区分 `all_damage_resistance` 与 `physical_damage_resistance` 两个层级？
- `armor_break` 后续更适合削防御、削 `DR`，还是给予物理 `DOUBLE` 条件？

## Calibration template

以下模板用于把当前仍在使用的三类“防御侧百分比减伤”迁移到新口径。目的不是一次给出最终数字，而是把“旧公式、当前内容、候选新值、验证目标”放到同一张表里，避免实现时边改边猜。

### 使用规则

- `当前口径` 只记录现状，不写愿景。
- `候选新值` 可以先填 1 到 2 轮试算值，不代表最终上线值。
- `验证场景` 尽量写成可回归的最小战斗夹具，而不是泛泛描述。
- `预期结果` 用玩家可感知指标表达，例如“重击不再稳定打成 1 点”“错误属性攻击显著吃亏”“格挡后普攻从 3 回合击倒拉到 5 回合击倒”。

### 主表

| 类别 | 当前来源 | 当前口径 | 当前内容实值 | 迁移目标 | 候选新值 | 验证场景 | 预期结果 | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 状态 `damage_reduction_up` | `warrior_guard`、原 `warrior_shield_wall`（旧内容） | `10% * power`，上限 `30%` | 当前确认内容 `power = 1` => `10%` | 从这两个技能中移除；若未来仍保留，只作为固定值减伤状态供其他内容使用 | `power1 = __`、`power2 = __`、`power3 = __` | 若未来保留该状态，再用近战单体 / 法术单体各 1 组测试；若不保留，则只做迁移清理回归 | 不再由 `warrior_guard` / 旧 `warrior_shield_wall` 承担正式防护表达 | 当前已确定团队护盾正式迁为 `priest_aid` |
| 护盾值 `shield_hp` | `priest_aid` | 当前正式字段已落到 `BattleUnitState` | 当前正式内容锁定为 D&D 2 环风格 `1d8 + 3`，单次施法共享一组骰值 | 固定值护盾池；先吸收伤害，再扣 HP | M1 固定为 `1d8 + 3` | 单体近战、单体法术、多段小伤害各 1 组 | 护盾应先被消耗，剩余伤害才进入 HP；同类护盾取高值或刷新持续时间 | 护盾 contract 已进入 `BattleUnitState / BattleRuntimeModule / BattleDamageResolver`，后续只继续调数值与 preview/log 细节 |
| 状态 `guarding` | `warrior_guard` | `15% * power`，上限 `45%` | 当前确认内容 `power = 1` => `15%` | M1 固定值 `stance_reduction`；仅减物理；M3 演化为首次受击自动 block | `power1 = __`、`power2 = __`、`power3 = __` | 普攻、重击、多段物理伤害各 1 组；另加火/冰/雷/负能量法术对照组 | 应显著拉高防御姿态对物理伤害的收益，但对法术伤害没有任何效果 | 必须按 modifier taxonomy 与 `damage_reduction_up` 隔离；`warrior_guard` 迁移后不再附带 `damage_reduction_up` |

### 建议记录的首批验证夹具

- `warrior_guard` 自身承受 `warrior_heavy_strike`。
- `warrior_guard` 自身承受 1 个火系、1 个冰系、1 个雷系、1 个负能量技能，确认 `guarding` 不生效。
- `priest_aid`：友军承受单体近战伤害，确认先掉护盾不掉血。
- `priest_aid`：友军承受单体法术伤害，确认同样先掉护盾。
- 1 个多段 / 小伤害技能打 `guarding` 目标。
- 1 个高单段 / 重击技能打 `guarding` 目标。

## Next implementation steps

1. 在 `BattleDamageResolver` 里落地新的伤害流水线，并去掉中间步骤反复 `round + floor 1`。
2. 先补最小减伤 modifier taxonomy，避免 `guarding + damage_reduction_up` 默认线性叠加。
3. 为 `0` 伤害、免疫、被吸收三种结果补 log / preview 表达，并把它作为 `MIN_DAMAGE_FLOOR = 0` 的合入前置条件。
4. 为团队护盾补正式 `shield_hp` 层、状态/日志/preview contract，并把正式技能固定为 `priest_aid`。
5. 让 `damage_reduction_up`、`guarding` 拥有新的正式减免语义，并补回归。
6. 基于新流水线重新校准首批技能、护盾值与防护状态数值。
7. 审计攻击侧倍率叠乘上限，必要时在 M2 前先加局部限幅。

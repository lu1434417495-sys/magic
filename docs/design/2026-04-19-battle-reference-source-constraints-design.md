# 战斗规则参考源分流设计约束

日期：2026-04-19

## Problem

当前仓库的 battle 规则已经形成 `BattleRuntimeModule -> BattleDamageResolver / BattleHitResolver / BattleStatusSemanticTable / BattleAiScoreService` 的纯规则主链。后续如果同时参考 `PF2e`、`D&D 3.5e / PF1e`、`5e`，但不先锁定“哪个子系统主学谁”，会出现以下问题：

- 同一条规则链同时混入多套数学口径，导致 preview、AI、runtime 结果失配。
- 状态、减伤、命中和动作经济被不同来源反复改写，维护者无法判断正式真相源。
- 玩家表层文案和底层数学不一致，系统体验既不够清晰，也不够稳定。

本设计文档用于把“参考源分流”固化为正式约束，作为 battle 规则后续设计与落地的裁剪标准。

## Goals

- 明确 battle 各子系统的主参考源、允许借用源和禁止导入项。
- 把 `PF2e` 作为正式规则骨架，保证 runtime / preview / AI 可共享同一套数学。
- 让 `3.5e / PF1e` 只为内容深度、克制关系和构筑差异提供增量价值。
- 让 `5e` 只服务于文案表达、职业 fantasy 和玩家理解成本控制。
- 为后续修复 `ISSUE-BATTLE-05` 之类的规则问题提供一致的判断基准。

## Non-goals

- 不完整移植任一桌面系统的全量规则。
- 不把当前 `AP + TU` 直接重写成任一桌面版原教旨动作经济。
- 不同时维护两套正式 battle 数学主链。
- 不在本轮设计里展开具体数值表、职业平衡表或全套技能重做清单。

## Constraints

- 正式规则主链仍以 CU-16 为边界：`BattleDamageResolver`、`BattleHitResolver`、`BattleStatusSemanticTable`、`BattleAiScoreService` 属于 battle 纯规则层。
- runtime、preview、AI 必须共享同一套结算语义；禁止仅为 UI 或仅为 AI 私下拼一套替代口径。
- 现有资源体系仍以 `action_points`、`current_mp`、`current_stamina`、`current_aura`、`TU` 为基础，不引入第二套平行动作资源。
- 后续若引入新 tabletop 灵感，必须先回答“它属于哪个子系统、主参考源是谁、owner 文件在哪”。

## Proposed approach

正式采用“`PF2e` 做骨架、`3.5e / PF1e` 补深度、`5e` 只借表层体验”的分流方案。

### 1. PF2e 作为规则骨架

以下子系统默认主学 `PF2e`：

- 动作经济与资源成本表达。
- 命中 / 未命中 / 强命中 / 弱命中等结果分层思路。
- 状态语义、stack 约束、持续时间推进与 tick 责任边界。
- 承伤分类：`hit gate`、`resistance`、`weakness`、`immunity`、`hardness-like block`。
- 反应式防御与防御姿态的结算接口。
- 供 preview / AI / runtime 共享的可程序化规则表达。

选择理由：

- `PF2e` 的规则边界最清晰，最适合作为程序化骨架。
- 它比 `3.5e / PF1e` 更容易控制 stacking 和例外数量。
- 它比 `5e` 更适合直接支撑 deterministic runtime 与 AI 评分。

### 2. 3.5e / PF1e 作为内容深度来源

以下子系统允许借用 `3.5e / PF1e` 风格，但不能反向改写骨架：

- 敌人身份差异和题型设计。
- 伤害类型、材质、穿透条件、克制标签。
- `DR X / bypass tag` 这类明确的物理减伤内容变体。
- 构筑向专精收益、 niche counter、装备与职业的强身份协同。
- Boss / 精英敌人的局部 hard-answer 设计。

使用边界：

- 只能作为显式内容层扩展，不得变成所有单位默认共享的底层数学。
- 必须通过统一标签系统进入 resolver，不允许直接散落在技能脚本或 UI 逻辑里。
- 一旦引入新的 bypass / counter 规则，preview 和 AI 必须同时能读懂。

### 3. 5e 只作为玩家表层体验来源

以下模块允许借用 `5e` 风格，但禁止主导底层数学：

- 技能说明文案。
- 职业 fantasy、招牌技能的第一印象。
- UI 预览面板、简洁提示、低理解成本的文案组织。
- onboarding 与玩家第一层规则暴露。

使用边界：

- `5e` 的“简洁体感”可以借，但不能要求 runtime 同时维持 `5e` 的模糊裁定空间。
- 如果 `5e` 风格表达与正式数学冲突，优先保留正式数学，调整表层文案。

## Module mapping

| 子系统 | 主参考源 | 允许借用 | 明确禁止 |
| --- | --- | --- | --- |
| 动作经济 / 指令合法性 / 回合窗口 | `PF2e` | `5e` 的简洁呈现 | 直接移植 `3.5e` 标准动作 / 全回合 / swift 的原版分类 |
| 命中系统 / 结果分层 | `PF2e` | `3.5e` 的少量高命中构筑差异 | 同时维护“百分比命中”和第二套完整桌面命中引擎 |
| 状态语义 / stack / duration / tick | `PF2e` | `3.5e` 的少量特殊状态 fantasy | 让每个状态自带私有 stacking 口径 |
| 伤害流水线 / 承伤分类 | `PF2e` | `3.5e / PF1e` 的 `DR`、穿透条件 | 默认多层百分比叠乘减伤 |
| 敌人题型 / 克制 / 材质 / bypass | `3.5e / PF1e` | `PF2e` 的分类表达 | 让所有普通敌人默认携带复杂 bypass 表 |
| 技能 fantasy / 玩家表层文案 | `5e` | `PF2e` / `3.5e` 的机制名词 | 用 `5e` 文案倒逼底层数学迁就模糊规则 |
| AI 评分 / preview 结果 | `PF2e` | `3.5e` 内容标签作为输入 | AI 私有规则、UI 私有规则 |

## Hard rules

后续 battle 设计必须遵守以下硬约束：

1. 一个子系统只能有一个主参考源。
2. 任何 tabletop 规则在进入 runtime 前，必须先标明 owner 文件和共享读取路径。
3. preview、runtime、AI 禁止各自维护不同的正式数学。
4. 默认不新增新的“通用百分比减伤层”；若确需存在，必须给出为什么固定值、分类抗性或反应式格挡不能满足需求。
5. 同类增益默认不自由叠加；stack 语义必须进入 `BattleStatusSemanticTable` 或正式 modifier taxonomy。
6. `3.5e / PF1e` 风格 hard counter 只能是显式内容，不得成为普战默认复杂度。
7. `5e` 只能影响“玩家怎么读懂系统”，不能定义“系统怎么结算”。

## Initial keyword mapping

以下是现有 battle 关键词的首批正式落点：

### `guarding`

- 主学 `PF2e`。
- 语义应优先是防御姿态、格挡窗口、受击前防护或 hardness-like block。
- 不应继续作为“通用后置百分比乘区”长期存在。

### `damage_reduction_up`

- 主学 `PF2e`，可借 `3.5e / PF1e` 风味。
- 默认应优先落为固定值 `resistance`、护盾值或有限 `DR X`。
- 仅在显式内容需要时，才允许扩展为带 bypass tag 的物理减伤。

### 伤害类型防护

- 主学 `PF2e` 的分类抗性。
- 长期方向是不再使用线性百分比抗性作为正式主口径。
- 不再保留旧抗性属性字段；类型防护统一通过 `damage_tag + mitigation_tier`、显式 `DR` 或护盾池表达。

### `attack_up` / `archer_pre_aim` / `marked` / `armor_break`

- 状态语义与 stacking 主学 `PF2e`。
- 内容用途可借 `3.5e / PF1e` 的专精和克制感。
- 玩家可读文案优先保持 `5e` 式简洁表达。

## Alternatives considered

### Option A：全系统主学 PF2e

优点：

- 最整齐、最易维护。
- AI / preview / runtime 共用规则链最轻松。

缺点：

- 容易损失 `3.5e / PF1e` 那种强题型、强标签、强构筑的内容牙齿。
- 敌人与职业身份可能过于平滑。

未选原因：

- 当前项目已经有明确的 DND / PF 风格诉求，只保留 `PF2e` 会让内容层辨识度不够。

### Option B：全系统主学 3.5e / PF1e

优点：

- 题型感和构筑深度最强。
- 标签克制、装备价值和敌人解法非常鲜明。

缺点：

- 例外和 stacking 极易失控。
- 对 runtime、preview、AI 的一致性要求极高，维护成本最大。

未选原因：

- 不适合作为当前 battle 纯规则层的正式骨架。

### Option C：PF2e 骨架 + 3.5e / PF1e 深度 + 5e 表层体验

优点：

- 兼顾结构稳定、内容深度和玩家可读性。
- 最符合当前仓库已有 battle 模块化边界。

缺点：

- 需要持续执行“分流纪律”，不能看到喜欢的 tabletop 规则就直接塞进 runtime。

选用原因：

- 是当前项目最稳、最容易持续演化的折中方案。

## Risks

- 如果没有后续的 modifier taxonomy，这份约束会停留在口头层，无法真正阻止 stacking 失控。
- 如果 UI 文案先行而 runtime 规则未同步，`5e` 风格表层会再次掩盖底层复杂度。
- 如果 3.5/PF1e 标签系统铺得过早、过广，普通战斗的理解成本会迅速上升。

## Open questions

- 是否在 battle 正式规则层引入统一的 modifier type（如 `status / stance / item / terrain`）？
- `guarding` 是更适合做“反应式 block”，还是“持续 stance + 固定减伤”？
- 元素防护是否只保留 `HALF / IMMUNE / DOUBLE` 档位，还是需要后续引入其他显式状态？

## Next implementation steps

1. 审核现有 `guarding`、`damage_reduction_up`、`attack_up`、`armor_break`、`marked` 的正式语义归属。
2. 为 battle 规则层补一份最小 modifier taxonomy，明确哪些来源允许叠加，哪些不允许。
3. 基于本约束，重写伤害与减伤子系统 spec，优先处理 `ISSUE-BATTLE-05`。
4. 为 resolver 增加“DR / 反应式格挡 / 护盾池”所需的最小回归样例。

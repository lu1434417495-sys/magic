# 模块重构当前待办

> 更新日期：2026-04-18
> 范围：当前仓库 `scripts/` 运行时主链、相关 headless/regression，以及 `docs/design/project_context_units.md` 已声明的所有权边界
> 说明：本文只保留当前仍有效的 todo。已完成、已失效或已被当前实现取代的旧项已移除。

---

## 一、使用方式

- 当前模块边界以 `docs/design/project_context_units.md` 为真相源。
- 本文件只记录：
  - 仍未完成、且值得继续执行的功能缺口
  - 当前仍成立的中期结构债务
- 如果后续运行时主链再次发生边界迁移，应先更新 `project_context_units.md`，再回写本文件。

---

## 二、当前高优先级待办

### P0.1 `game_runtime_facade.gd` — `command_return_from_submap()` 仍缺少 battle/modal 守卫

**现状**

- `command_return_from_submap()` 当前在确认“确实位于子地图”后，直接进入 `_return_from_active_submap()`。
- 它没有检查：
  - 当前是否仍在 battle active
  - 当前是否仍有 modal 打开
- 这意味着 headless/文本命令路径理论上仍可能在不安全时机修改：
  - `active_submap_id`
  - 玩家坐标
  - active map 上下文

**当前 owner**

- `scripts/systems/game_runtime_facade.gd`

**执行步骤**

1. 在 `command_return_from_submap()` 的子地图检查之后追加 battle guard。
2. 追加 modal guard。
3. 拒绝时统一返回 `_command_error(...)`，不要静默 return。
4. 增加专门的 headless regression，显式覆盖：
   - battle active 时调用
   - modal 打开时调用
   - 正常 submap return

**验收**

- battle active 时，`command_return_from_submap()` 返回失败，battle/world 状态不变。
- modal 打开时，`command_return_from_submap()` 返回失败，submap 上下文不变。
- 正常 submap return 仍然成功。

### P1.2 `game_text_snapshot_renderer.gd` — 文本快照仍未渲染 battle-start confirm

**现状**

- `GameRuntimeSnapshotBuilder` 已经输出：
  - `start_confirm_visible`
  - `start_prompt`
- `GameTextSnapshotRenderer` 当前 `BATTLE` 段仍未渲染这两个字段。
- 文本 runtime/REPL 当前能通过结构化 snapshot 看到开始确认状态，但纯文本快照里还看不到 battle-start confirm 流。

**当前 owner**

- `scripts/utils/game_text_snapshot_renderer.gd`

**执行步骤**

1. 在 `BATTLE` 段渲染中读取 `start_confirm_visible`。
2. 若为 true，渲染 battle-start confirm 行。
3. 视需要补充 `start_prompt` 的关键字段输出。
4. 保持已有文本快照稳定性，不改变无关字段顺序。

**验收**

- 触发 battle-start confirm 时，文本快照可见对应提示。
- `tests/text_runtime/run_text_command_regression.gd` 继续通过。

---

## 三、当前结构债务

这些工作不是立刻阻塞主链的 bug，但仍然值得保留为中期结构债务。

### S1 `BattleHitResolver` 仍未完全成为单一真相源

当前 `battle_hit_resolver.gd` 已存在，并负责：

- 命中率合成
- deterministic d20 掷骰
- repeat-attack 的正式命中口径

但如果后续要正式引入更复杂的命中体系，例如：

- `THAC0 + 负 AC + d20`
- natural 1 / natural 20 以外的更多命中修正
- 优势 / 劣势
- 逐目标命中预览
- 更统一的 battle log 命中口径

则仍应继续把命中判定、命中预览和日志口径收敛到 `battle_hit_resolver.gd`，避免把公式重新散回 `battle_runtime_module.gd`、`battle_damage_resolver.gd` 或未来 HUD / AI 侧车。

### S2 `DesignSkillCatalog` 仍处于“代码承载数据”的过渡态

当前正式技能真相源已经迁到：

- `data/configs/skills/*.tres`

但兼容层仍保留：

- `design_skill_catalog.gd`
- `design_skill_catalog_mage_specs.gd`

当前问题仍然是：

- 大量字典字面量仍在代码文件中
- review / diff 负担仍偏高
- mage 兼容内容尚未完全资产化

中期目标仍应是：

1. 继续把剩余 catalog 兼容内容迁到声明式资源或数据文件。
2. catalog/registry 只保留加载、校验、缓存和兼容桥接逻辑。

### S3 `GameRuntimeFacade` 仍需控制继续膨胀

当前 facade 已完成第一轮拆分，但仍是大型协调器。后续规则新增时应坚持以下边界：

- 战斗选择状态继续留在 `GameRuntimeBattleSelection(+State)`
- 仓库流程继续留在 `GameRuntimeWarehouseHandler`
- 奖励/晋升流程继续留在 `GameRuntimeRewardFlowHandler`
- 据点动作继续留在 `GameRuntimeSettlementCommandHandler`
- 快照继续留在 `GameRuntimeSnapshotBuilder`

不要把“只是为了省一次跳转”的逻辑重新塞回 facade。

---

## 四、建议执行顺序

### Phase A — 先补当前功能缺口

1. 为 `command_return_from_submap()` 增加 battle/modal guard，并补 headless regression。
2. 为 `game_text_snapshot_renderer.gd` 增加 battle-start confirm 文本输出。

### Phase B — 再处理结构债务

1. 评估是否继续收敛 `BattleHitResolver` 为更完整的命中真相源。
2. 继续推进 `DesignSkillCatalog` 资源化/资产化。
3. 在新增规则时持续约束 `GameRuntimeFacade` 不再回涨。

---

## 五、结论

- 当前仓库的大规模拆分动作已经不是主要问题；这份文档现在只保留仍有效的 todo。
- 现阶段最值得处理的是两个仍未闭合的缺口：
  - `command_return_from_submap()` 的 battle/modal 安全守卫
  - battle-start confirm 的文本快照渲染
- 其余待办主要属于中期结构债务，应以“不回退现有模块边界”为前提继续推进。

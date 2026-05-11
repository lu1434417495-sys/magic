# 模块重构当前状态

> 更新日期：2026-05-09
> 范围：当前仓库 `scripts/` 运行时主链、相关 headless/regression，以及 `docs/design/project_context_units.md` 已声明的所有权边界。

## 关联上下文单元

- CU-06：世界/战斗运行时总编排与场景适配
- CU-15：战斗运行时总编排
- CU-16：战斗状态模型、边规则、伤害、AI 规则层

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文件只记录模块重构债务的收口状态。

---

## 一、结论

原计划里保留的 S1-S5 结构债务已经全部收口。当前没有仍需继续拆分的模块级债务；后续新问题应作为新的条目重新建档，不再复用旧编号。

仍会在部分 Godot 回归日志中看到内容资源校验噪声，例如 `warrior_flaw_read.tres` / `warrior_unbending_rise.tres` 解析错误、部分技能 `status_id` / `save_tag` / `max_level` 校验错误。这些属于内容数据债务，不属于本轮模块边界债务。

---

## 二、已完成条目

### S1 命中真相源收口

- `BattleHitResolver` 现在承接 fate 命中执行路径的 d20、crit-gate、fumble、天然 1 特性重掷、逆命护符降级与 spell-control metadata。
- `BattleDamageResolver` 不再直接持有 fate 攻击规则；攻击结算只向 `BattleHitResolver` 取已解析 metadata，再负责伤害、状态、事件派发与战报。
- 测试替身已同步到新的 owner 边界：固定命中 / 暴击 / 未命中通过 `tests/shared/stub_hit_resolvers.gd` 控制，伤害替身只控制伤害骰。

### S2 / S3 Fate owner 收口

- 新增 `scripts/systems/battle/fate/fate_runtime_module.gd`，统一持有并 setup / dispose：
  - `FortuneService`
  - `MisfortuneService`
  - `FortunaGuidanceService`
  - `MisfortuneGuidanceService`
  - `LowLuckEventService`
- `BattleRuntimeModule` 持有 `FateRuntimeModule`，并向 runtime sidecar 暴露 misfortune gate / consume / trigger 与 battle resolution 入口。
- `GameRuntimeFacade` 不再直接挂 fate service 字段，只在战斗结算、章节完成、forge 结果、settlement low-luck reward 边界调用 `BattleRuntimeModule.get_fate_runtime()`。

### S4 Battle loot 常量收口

- 新增 `scripts/systems/battle/core/battle_loot_constants.gd` 作为 battle loot drop type、source kind、source id、固定 item id 与 calamity shard chapter cap 的单一真相源。
- Runtime、facade、loot resolver、battle resolution result、world encounter preview 及相关测试均改为引用同一组常量。

### S5 机械清理

- 清理旧 `tests/tmp_overlay_check.gd` 及对应 `.uid`。
- 移除 `.gitignore` 中已不存在的 `fixed_test_world_save.dat` 例外。
- 已确认 `scripts/systems` 下不再残留旧孤儿 `.uid`。

---

## 三、验收覆盖

本轮结构收口的重点回归入口：

- `godot --headless --script tests/battle_runtime/fate/run_fate_attack_formula_regression.gd`
- `godot --headless --script tests/battle_runtime/rules/run_battle_hit_resolver_bab_regression.gd`
- `godot --headless --script tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.gd`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd`
- `godot --headless --script tests/battle_runtime/skills/run_battle_weapon_dice_regression.gd`
- `godot --headless --script tests/battle_runtime/skills/run_magic_backlash_regression.gd`
- `godot --headless --script tests/battle_runtime/skills/run_warrior_skill_semantics_regression.gd`
- `godot --headless --script tests/battle_runtime/skills/run_warrior_advanced_skill_regression.gd`
- `godot --headless --script tests/battle_runtime/fate/run_fate_low_luck_tactical_skills_regression.gd`
- `godot --headless --script tests/battle_runtime/fate/run_misfortune_service_regression.gd`
- `godot --headless --script tests/progression/fate/run_misfortune_guidance_regression.gd`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_loot_drop_luck_regression.gd`
- `godot --headless --script tests/battle_runtime/fate/run_fate_calamity_drop_regression.gd`

正常全量回归入口：

- `python tests/run_regression_suite.py`

---

## 四、后续记录规则

- 如果后续发现新的 facade 膨胀、runtime sidecar owner 漂移、或规则真相源分裂，按新的 S 编号追加，不再把旧 S1-S5 重新打开。
- 内容资源校验债务应单独建内容修复计划；不要把 `.tres` 数据合法性问题混入模块边界计划。

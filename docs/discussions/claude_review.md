# Claude Review 待处理项

更新日期：`2026-04-28`

## 说明

- 已从本文件移除当前源码或现有回归能确认已解决的旧 review 条目。
- 已移除的范围包括 `CU-05 ~ CU-08` 旧复核中的已解决项，以及 WPNDICE 增量复核里当前已经落地的项。
- 本文件只保留仍需要修复、设计确认或后续决策的事项。
- 下面只列仍未完成的代码修复或设计确认项。

## 剩余事项

### 低风险 / 后续整理

1. **换装 requirement blockers 信息仍未串到最终错误结果** — `scripts/systems/battle_runtime_module.gd:1754-1759,1914-1948`
    - `_resolve_change_equipment_requirement_rule()` 会生成 `blockers`，但 `_with_change_equipment_error()` 只保留 `error_code` 和 `message`。
    - 当前测试只断言第一个 blocker，所以短期可接受；未来 UI 若要展示多个阻断原因，需要把 blockers 一路传出。

2. **资源解锁目前只门控 HUD 展示，不门控施法** — `scripts/player/progression/unit_progress.gd` / `scripts/systems/battle_runtime_module.gd:4945-4958`
    - `unlocked_combat_resource_ids` 当前只决定 HUD 是否显示 MP / 斗气。
    - `_get_skill_cast_block_reason()` 只按当前资源数值判定能否施法。
    - 目标需要确认：继续定义为 HUD-only，或把未解锁资源也纳入施法前置。

3. **`_dedupe_effect_defs_by_instance()` 按 Object identity 去重** — `scripts/systems/battle_runtime_module.gd:3747-3758`
    - 同一 `.tres` SubResource 引用会去重；程序合成的同语义副本不会去重。
    - 当前 front_arc 用法可接受；若未来生成语义等价副本，需要改成内容级 key。

4. **`_record_action_issued()` 把任何非 WAIT 命令视为已行动** — `scripts/systems/battle_runtime_module.gd:1148-1150`
    - 当前 `change_equipment` 算正式行动，和 AP 消耗一致。
    - 若未来加入不消耗 AP 的自由动作，需要为“是否打断 resting”建立显式规则。

5. **`basic_attack` 同时承担基础攻击、成长触发与 mastery 来源** — `data/configs/skills/basic_attack.tres`
    - 当前 `mastery_trigger_mode = weapon_attack_quality`，并推动 STR / AGI 成长。
    - `max_level = 1` 表示它主要作为资格事件，不走持续升级曲线。
    - 如果未来希望基础攻击自身也升级，需要重审 mastery curve 与自动注入规则。

## 建议处理顺序

1. 先逐项处理资源解锁语义和低风险整理项。

## 建议回归

- `godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd`
- `godot --headless --script tests/battle_runtime/run_battle_change_equipment_requirement_regression.gd`
- `godot --headless --script tests/warehouse/run_party_warehouse_regression.gd`
- `godot --headless --script tests/battle_runtime/run_battle_skill_protocol_regression.gd`
- 如果改 HUD snapshot 或面板显示，再补跑 `godot --headless --script tests/battle_runtime/run_battle_ui_regression.gd`

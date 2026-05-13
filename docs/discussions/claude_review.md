# Claude Review 待处理项

更新日期：`2026-05-13`

## 说明

- 已从本文件移除当前源码或现有回归能确认已解决的旧 review 条目。
- 已移除的范围包括 `CU-05 ~ CU-08` 旧复核中的已解决项，以及 WPNDICE 增量复核里当前已经落地的项。
- 本文件只保留仍需要修复、设计确认或后续决策的事项。
- 下面只列仍未完成的代码修复或设计确认项。

## 剩余事项

暂无。

## 建议处理顺序

1. 先逐项处理低风险整理项。

## 建议回归

- `godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_change_equipment_requirement_regression.gd`
- `godot --headless --script tests/warehouse/run_party_warehouse_regression.gd`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd`
- 如果改 HUD snapshot 或面板显示，再补跑 `godot --headless --script tests/battle_runtime/rendering/run_battle_ui_regression.gd`

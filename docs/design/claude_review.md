# Claude Review 复核

更新日期：`2026-04-18`

## 说明

- 本文档不再重复原始 review 全文，而是按当前工作树重新核实 `CU-05 ~ CU-08` 的问题状态。
- 本次结论以当前未提交代码为准，不以 `HEAD` 或旧 review 发生时的仓库状态为准。
- 状态分三类：
  - `已解决`：代码已经改到位，且本次复核能直接确认。
  - `按当前设计保留`：原问题在当前运行时语义下不再成立，或不应按原建议修改。
  - `未处理`：原问题或建议在当前代码里仍然存在。

## 总结

- `已解决`：15 项
- `按当前设计保留`：1 项
- `未处理`：8 项

## CU-05

- `按当前设计保留`：`scripts/systems/world_map_grid_system.gd:62-63` 的 `is_cell_walkable()` 仍然只检查世界边界，没有把 footprint 占用当成 blocker。
  - 当前这不是回归。`GameRuntimeFacade._move_player()` 依赖“可以踏入据点占格”来触发据点进入链；`tests/world_map/runtime/run_world_map_settlement_entry_regression.gd` 已验证“从外格踏入据点占格时自动打开据点”仍然成立。
  - 如果未来要引入“不可进入的占格”，应新增更精确的 API，而不是直接把 `is_cell_walkable()` 改成“占用即不可走”。
- `已解决`：`scripts/systems/world_map_grid_system.gd:151-167` 的 `_get_occupant_state()` 现在会对 `Object` 分支调用 `is_empty()`，并在为空时自清理 `_occupied_cells`。
  - `tests/world_map/run_world_map_low_level_defensive_regression.gd` 通过。
- `已解决`：`scripts/systems/world_map_fog_system.gd:29-31` 的 `rebuild_visibility_for_faction()` 现在会过滤 `source.faction_id`，不会再把其他阵营的视野混进当前阵营。
  - `tests/world_map/run_world_map_low_level_defensive_regression.gd` 通过。
- `未处理`：`terrain_visual_type` 目前仍只有 `scripts/utils/world_map_cell_data.gd` 和 `scripts/systems/world_map_grid_system.gd` 自己读写，`WorldMapView` 仍不消费它。
- `未处理`：`scripts/systems/world_map_grid_system.gd:71-79` 的 `get_cells_in_rect()` 仍按格子分配 `WorldMapCellData`，并且当前仓库里没有调用点。

## CU-06

- `已解决`：`scripts/systems/game_runtime_facade.gd` 已移除旧的场景节点残留字段；当前 facade 不再持有 `world_map_view`、`battle_map_panel`、`party_warehouse_window` 一类 UI 节点引用，也没有旧的 `_refresh_battle_panel*` / `_set_*_view_active` 死方法。
- `已解决`：`scripts/systems/game_runtime_warehouse_handler.gd` 已不再保留 `_refresh_party_warehouse_window()` 这类永远 no-op 的旧 UI 刷新路径。
- `已解决`：`scripts/systems/world_map_system.gd` 已移除 `_open_local_service_window` / `_build_settlement_window_data` 等场景层死 helper，场景层不再保留旧的 runtime 透传接口。
  - `tests/world_map/runtime/run_world_map_system_surface_regression.gd` 通过。
- `未处理`：`scripts/systems/world_map_system.gd:417-421` 的 `_set_battle_loading_overlay()` 仍然忽略 `progress_value`。
  - 现有行为是稳定的，`tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.gd` 只验证显示/隐藏和文案，不验证进度表现。
- `未处理`：`scripts/systems/world_map_system.gd:640-644` 在 `battle_start_confirm` 状态下取消仍然直接 `return`，没有显式反馈，也没有禁用取消按钮。
- `已解决`：`scripts/systems/world_map_runtime_proxy.gd:390-401` 现在不会吞掉非 `Dictionary` 返回值，而是发出 warning 并把结果改写成结构化错误。
  - `tests/world_map/run_world_map_runtime_proxy_regression.gd` 通过。
- `未处理`：`GameRuntimeFacade` 仍是超大文件，review 中建议的 “WorldMapDataContext” 级别拆分还没落地。
- `未处理`：`scripts/systems/world_map_system.gd:315-317` 与 `scripts/systems/world_map_system.gd:406-407` 仍让 `Enter / KP_ENTER / Space` 同时承担世界态“打开据点”和战斗态“等待 / 结算”。
  - 当前依赖模式分流，不是立即错误，但键位复用问题还在。
- `已解决`：`scripts/systems/battle_session_facade.gd:402-404` 的 `build_battle_seed()` 现在有 `encounter_anchor == null` guard。

## CU-07

- `已解决`：`scripts/ui/world_map_view.gd:257-261` 的选中框现在使用 `SELECTION_OUTLINE_COLOR`，不再是 alpha 为 `0.0` 的透明描边。
- `已解决`：旧 review 提到的 `_get_cell_rect()`、`_get_player_draw_rect()`、`_get_viewport_world_rect()` 这几个未使用 helper 已移除。
- `已解决`：`_draw_settlements()` 现在把 `font` / `font_size` 的获取与空判断提到循环外，不再在每个 settlement 上重复判空。
- `未处理`：`scripts/ui/world_map_view.gd:206-230,340` 仍然保留一组硬编码颜色，尚未改成 `@export Color` 配置。

## CU-08

- `已解决`：`scripts/ui/shop_window.gd:332-336` 与 `scripts/ui/stagecoach_window.gd:309-313` 现在会先把 `settlement_id` / `action_id` 保存到局部变量，再 `hide_window()`，signal 不会再发出空串 ID。
  - `tests/world_map/ui/run_service_window_ui_regression.gd` 通过。
- `已解决`：`scripts/ui/settlement_window.gd` 现在只保留 `action_requested`，不再保留 `shop_requested` / `stagecoach_requested`。
  - `tests/world_map/ui/run_service_window_ui_regression.gd` 通过。
- `已解决`：成员选项与默认成员解析的重复逻辑已抽到 `scripts/ui/party_member_option_utils.gd`，`SettlementWindow` / `ShopWindow` / `StagecoachWindow` 都改成复用同一套工具。
- `已解决`：`StagecoachWindow` 已和 `ShopWindow` 对齐，支持 `allow_empty_entries`，不再无条件注入 fallback 条目。
  - `tests/world_map/ui/run_service_window_ui_regression.gd` 通过。
- `未处理`：`scripts/ui/character_info_window.gd:17-47` 仍然是单块 `RichTextLabel + StatusLabel` 的平面结构，原 review 里提到的“为后续多 section 内容预留容器”还没做。
- `已解决`：`scripts/ui/settlement_window.gd:415-423`、`scripts/ui/shop_window.gd:350-358`、`scripts/ui/stagecoach_window.gd:327-335` 现在都只接受左键点击遮罩关闭窗口，右键不会再直接关闭。
  - `tests/world_map/ui/run_service_window_ui_regression.gd` 通过。

## 跨单元结论

- 原 review 里那组三连问题已经一起落地：
  - facade 的 UI 残留字段已清掉；
  - warehouse handler 的旧窗口刷新 no-op 已移除；
  - `WorldMapSystem` 的场景层死 helper 已移除。
- 原 review 里提到的两个“静默 bug”也都修掉了：
  - 世界地图选中框可见；
  - `ShopWindow` / `StagecoachWindow` 的确认 signal 不再丢失 ID。
- 原 review 里提到的窗口重复逻辑抽取也已经完成，当前统一落在 `scripts/ui/party_member_option_utils.gd`。

## 仍建议继续跟进的点

- `WorldMapGridSystem`：
  - 如果 `terrain_visual_type` 短期内不会接入 renderer，可以考虑删除这层死字段。
  - 如果 `get_cells_in_rect()` 继续无调用点，可以删除；如果未来恢复使用，最好避免逐格分配对象。
- `WorldMapSystem`：
  - `battle_start_confirm` 的取消行为要么显式禁止，要么提供反馈，不要继续静默返回。
  - `battle_loading_overlay` 如果不做进度显示，可以把未使用参数从接口里删掉。
  - 世界态与战斗态的 `Enter / Space` 复用仍然值得收敛。
- `WorldMapView`：
  - world event / encounter / npc / settlement 颜色仍是代码常量，后续如果要给策划或场景作者调色，最好改成导出配置。
- `CharacterInfoWindow`：
  - 如果准备承载世界 NPC 详情、战斗单位详情、装备、技能与 AI 状态，当前平面文本结构会很快变得吃力。

## 本次复核验证

以下脚本已在当前工作树下执行并通过：

- `godot --headless --script tests/world_map/run_world_map_low_level_defensive_regression.gd`
- `godot --headless --script tests/world_map/run_world_map_runtime_proxy_regression.gd`
- `godot --headless --script tests/world_map/ui/run_service_window_ui_regression.gd`
- `godot --headless --script tests/world_map/runtime/run_world_map_system_surface_regression.gd`
- `godot --headless --script tests/world_map/runtime/run_world_map_settlement_entry_regression.gd`
- `godot --headless --script tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.gd`

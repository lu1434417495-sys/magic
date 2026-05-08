# 项目代码审查报告（Bug / 潜在问题）

> 审查日期：2026-05-03  
> 审查方式：Subagent 逐文件检视  
> 约束：仅提出问题与修复建议，未修改任何源代码  
> 覆盖范围：`scripts/systems/`、`scripts/player/`、`scripts/enemies/` 核心模块 + 测试 / 工具 / 配置 / 文档

---

## 目录

1. [文件 1：`game_session.gd`](#文件-1-gamesessiongd)
2. [文件 2：`battle_runtime_module.gd`](#文件-2-battleruntimemodulegd)
3. [文件 3：`game_runtime_facade.gd`](#文件-3-gameruntimefacadegd)
4. [文件 4：`battle_ai_service.gd`](#文件-4-battleaiservicegd)
5. [文件 5：`battle_damage_resolver.gd`](#文件-5-battledamageresolvergd)
6. [文件 6：`battle_hit_resolver.gd`](#文件-6-battlehitresolvergd)
7. [文件 7：`progression_service.gd`](#文件-7-progressionservicegd)
8. [文件 8：`party_equipment_service.gd`](#文件-8-partyequipmentservicegd)
9. [文件 9：`world_map_grid_system.gd`](#文件-9-worldmapgridsystemgd)
10. [测试 / 工具脚本问题](#测试--工具脚本问题)
11. [配置 / 文档 / 版本控制问题](#配置--文档--版本控制问题)
12. [全局跨文件问题](#全局跨文件问题)
13. [建议优先修复顺序](#建议优先修复顺序)

---

## 文件 1：`game_session.gd`

**路径**：`scripts/systems/persistence/game_session.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 566-575 | 🔴 | `_prepare_new_world()` 中 `generation_config` 无类型注解，直接访问 `.world_size_in_chunks` 等属性 | 资源损坏时初始化崩溃 |
| 610-628 | 🔴 | `_persist_game_state()` `store_var` 返回值被忽略；索引写入失败时存档已存在但索引缺失（"孤儿存档"） | 存档目录不一致，用户进度不可见 |
| 262-273 / 631-660 | 🔴 | `load_save()` 先写全局状态再 `_flush_post_decode_save()`，后者失败时 GameSession 处于"半激活"污染状态 | `has_active_world()` 为 true 但数据不完整，后续逻辑异常 |
| 898-919 | 🔴 | `_generate_unique_save_id()` 每次循环都重新加载整个索引，O(n²) 重复 I/O | 存档增多时创建延迟恶化 |
| 1007-1012 | 🟡 | `_load_save_index_entries()` 每次读取都做一次全目录扫描重建，即使索引健康 | 启动/加载时 I/O 开销大 |
| 1117-1128 | 🟡 | `_get_save_meta_by_id()` / `_find_most_recent_save_by_config()` 每次调用都重复反序列化索引 | 同上，重复 I/O |
| 732-736 | 🟡 | `_revoke_orphan_racial_skills()` 创建 ProgressionService 后未调用任何方法，疑似死代码 | 维护困惑，或隐式副作用不可预期 |
| 1587-1601 | 🟡 | `_build_random_start_skill_tier_score()` 直接访问 `combat_profile` 字段，若缺失 Debug 模式崩溃 | 运行时类型错误 |
| 1380-1391 | 🟡 | `_build_default_member_state()` 直接对 `custom_stats[&"hp_max"]` 赋值，假设字典已初始化 | 资源加载顺序变更时 KeyError |
| 1198-1208 | 🟡 | `_apply_character_creation_payload_to_main_character()` 部分修改后失败无事务回滚 | 角色数据处于半修改状态 |
| 922-928 | 🟢 | `_load_generation_config()` 未验证返回对象是否为预期类型 | 错误延迟暴露 |
| 1143-1179 | 🟢 | `_remove_directory_recursive()` `list_dir_begin()` 返回值未检查 | 受限环境下目录清理不完整 |
| 952-954 | 🟢 | `save_size < 8` 魔数无注释说明 | Godot 版本升级后可能失效 |
| 1639-1658 | 🟢 | `_init()` 中 `_refresh_*` 顺序依赖无显式断言保护 | 重构时易误调顺序 |

---

## 文件 2：`battle_runtime_module.gd`

**路径**：`scripts/systems/battle/runtime/battle_runtime_module.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 360 | 🔴 | `start_battle()` 中 `encounter_anchor.display_name` 无 null 保护 | 传入 null 时战斗开始时崩溃 |
| 3923-3924 | 🔴 | `_roll_shield_hp()` null 检查在 `effect_def.power` 访问之后 | 传入 null 立即崩溃 |
| 2611-2616 | 🔴 | `_apply_on_kill_gain_resources_effects()` 死条件：`defeated_unit.is_alive` 前面已 return false，后续 `and defeated_unit.is_alive` 永为 false | 逻辑冗余，与设计意图可能不符 |
| 3190,3204,3238,3247 | 🔴 | `_magic_backlash_resolver` 在当前文件未声明，隐式依赖父类字段 | 父类重构即崩溃 |
| 2489 / 2515 / 2553 | 🔴 | `_build_chain_target_effect_defs()` / `_collect_chain_damage_targets()` / `_resolve_chain_damage_radius()` 访问 `chain_effect.params` 无 null 检查 | 连锁伤害技能解析时崩溃 |
| 4163 | 🔴 | `_build_implicit_ground_cast_variant()` 访问 `skill_def.combat_profile.effect_defs` 未检查 `combat_profile` | 技能缺少 combat_profile 时崩溃 |
| 1856-1861 | 🟡 | `_trigger_last_stand()` 中 `death_ward_entry.params.get(...)` 未检查 `params` 是否为 null | 免死效果触发时崩溃 |
| 3854-3860 | 🟡 | `_apply_shield_effect_to_target()` 等值护盾无条件替换（HP/持续时间相等时仍替换） | 可能覆盖来源信息 |
| 1466-1469 | 🟡 | `_find_spawn_anchor()` 第二重遍历冗余，永远不可能成功 | 无效代码 |
| 172,185-203,213 | 🟡 | 大量服务字段缺少类型注解 | 编译期无法检查，运行时类型错误 |
| 678-679 | 🟡 | `issue_command()` 换装后战斗结束/回合结束检查路径与其他命令不一致 | 状态机可能不一致 |
| 4494-4518 | 🟡 | `_check_battle_end()` 直接修改 `_state` 未检查 null | 新增调用路径时崩溃风险 |
| 4530-4541 | 🟡 | `_end_active_turn()` 同样直接访问 `_state.units` 无 null 守卫 | 同上 |
| 411-440 | 🟡 | `advance()` 手动单位异常时 phase 滞留 `"unit_acting"` | 战斗可能死锁 |
| 8-11 / 48-51 | 🟢 | 常量重复预加载（`BattleState` 等被加载两次） | 内存浪费 |

---

## 文件 3：`game_runtime_facade.gd`

**路径**：`scripts/systems/game_runtime/game_runtime_facade.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 228 | 🔴 | `setup()` 中 `_game_session.get_wild_encounter_rosters().duplicate()` 未判 null | 启动崩溃 |
| 1232-1361 | 🔴 | `finalize_battle_resolution()` 大量 `_game_session` / `_character_management` 调用未判 null | dispose 后或异常路径崩溃 |
| 2077-2139 | 🔴 | `_move_player()` 多处 `_game_session` 调用未判 null | dispose 后残留指令崩溃 |
| 2725-2739 | 🔴 | `_persist_party_state()` 整个函数假设 `_game_session` 永远非 null | 持久化路径崩溃 |
| 3021-3052 / 3055-3084 | 🔴 | `_enter_submap()` / `_return_from_active_submap()` `_game_session.set_player_coord()` 未判 null | 子地图切换崩溃 |
| 312-368 | 🔴 | `dispose()` 未释放大量子系统（`_grid_system`、`_fog_system`、`_character_management` 等） | 内存泄漏、信号重复触发、状态污染 |
| 737 | 🟡 | `get_settlement_shop_service()` 每次调用都 `new()`，语义与 `get_` 前缀不符 | 商店状态丢失 |
| 1509-1511 | 🟡 | `set_party_state()` 直接替换引用，未同步 `_character_management` 等子系统 | 状态分裂，各子系统看到过期数据 |
| 1521-1526 | 🟡 | `set_player_coord()` 仅改内存不持久化，与 `_move_player()` 行为不一致 | 调用方误以为已保存 |
| 1541-1560 | 🟡 | `command_world_move()` 缺少移动次数上限 | 极大 `count` 阻塞主线程 |
| 2625-2633 | 🟡 | `_get_string_name_keyed_value()` 用 O(n) 线性搜索代替 O(1) 字典查找 | 字典大时性能差 |
| 2205-2220 | 🟡 | `_on_world_map_cell_clicked()` 丢弃 `_return_from_active_submap()` 返回值 | 失败静默 |
| 2907 | 🟢 | `_is_battle_active()` 调用 `_battle_state.is_empty()`，需确认 `BattleState` 是否真有此方法 | 运行时方法缺失错误 |
| 65,67,69,91,113 等 | 🟢 | 大量关键字段和公共 API 缺少类型注解 | 维护困难 |

---

## 文件 4：`battle_ai_service.gd`

**路径**：`scripts/systems/battle/ai/battle_ai_service.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 41 / 43 | 🔴 | `choose_command()` 直接访问 `context.skill_score_input_callback` / `action_score_input_callback` | context 为字典或缺少字段时崩溃 |
| 81-84 | 🔴 | `actions` 可能为 null，后续调用 `is_empty()` / `size()` 崩溃 | AI 决策崩溃 |
| 208,213,215,222,224 | 🔴 | `_resolve_state_id()` 直接访问 `brain.default_state_id` / `retreat_hp_ratio` 等字段 | 配置缺失时崩溃 |
| 250 | 🔴 | `_unit_has_support_skill()` 假设 `known_active_skill_ids` 非 null | 字段未初始化时崩溃 |
| 262 / 269 / 272 | 🔴 | `_is_support_skill()` 假设 `effect_defs` / `cast_variants` / `effect_defs` 非 null | 同上 |
| 304-308 | 🔴 | `_commit_decision()` 直接写入 `unit_state.ai_blackboard["..."]`，未检查是否初始化 | 黑盒写入崩溃 |
| 312 / 314 / 317 | 🟡 | `_find_nearest_enemy()` 未检查 `context.grid_service`；`enemy_unit_ids` / `ally_unit_ids` 可能为 null | 空指针崩溃 |
| 328-346 | 🟡 | `_pick_step_toward()` 参数 `state` / `grid_service` 完全无 null 校验 | 外部错误调用时崩溃 |
| 353 | 🟡 | `_get_hp_ratio()` `float(unit_state.current_hp)` 未防护 `current_hp` 为 null | 转换崩溃 |
| 22 | 🟡 | `_score_service` 在类字段处直接 `new()`，单元测试难以注入 mock | 测试困难 |
| 79 | 🟢 | `best_scored_action_index` 硬编码哨兵值 `999999` | 可维护性差 |

---

## 文件 5：`battle_damage_resolver.gd`

**路径**：`scripts/systems/battle/rules/battle_damage_resolver.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 2034-2041 | 🔴 | `_get_status_param_string_key()` 用 `key_variant is String` 判断，但 Godot 4 中 `StringName` 不是 `String` 子类型 | StringName 键永远匹配失败，大量状态参数读取失效 |
| 122-153 | 🔴 | `resolve_attack_effects()` 未校验 `effect_defs` 是否为 null | 技能配置异常时崩溃 |
| 168-335 | 🔴 | `resolve_effects()` 同样未校验 `effect_defs` | 同上 |
| 1856-1861 | 🔴 | `_trigger_last_stand()` `death_ward_entry.params.get(...)` 未检查 `params` 是否为 null | 免死效果触发时崩溃 |
| 2049-2051 / 2069-2071 | 🔴 | `ai_blackboard` 多处直接访问无 null 检查 | 未初始化单位崩溃 |
| 559 / 568 / 688 / 732 等 | 🟡 | `attack_context` 参数在多函数中直接 `.get()`，若传入 null 崩溃 | 异常调用路径崩溃 |
| 338-356 | 🟡 | `_does_effect_trigger()` `damage_context.get(...)` 同样未防护 null | 同上 |
| 1223 / 1279 / 1431 / 1466 等 | 🟡 | `status_effects` / `damage_resistances` 多处直接 `.keys()` 未检查 null | 单位未初始化时崩溃 |
| 1865 | 🟡 | `_trigger_last_stand()` 用 `has_method("get")` 判断类型 | 数据结构微调后免死被动失效 |
| 1896-1910 | 🟡 | `_resolve_heal_fatal_amount()` skill_level=0 时产生负向修正 | 低等级治疗效果异常 |
| 398-401 | 🟡 | `_resolve_attack_metadata()` `force_hit_no_crit` 路径未填充 `hit_roll` | 战报显示命中骰为 0 |
| 1975-2009 | 🟡 | `_get_target_incoming_damage_multiplier`（取最大）与 `_get_source_outgoing_damage_multiplier`（连乘）逻辑不对称 | 伤害倍率叠加规则不一致 |
| 1375 | 🟡 | `_apply_black_star_brand_guard_ignore()` 状态擦除时机过早 | 多段伤害时只有第一段享受破防 |
| 1785 | 🟡 | `_resolve_secondary_hit()` DC 边界判定 `(save_roll + ... ) < dc`，等于时算失败 | 触发率低一个边界点 |
| 1734 / 1681 / 1692 / 867 | 🟢 | 关键累加/乘法未做上限限制 | 极端数值可能溢出 |

---

## 文件 6：`battle_hit_resolver.gd`

**路径**：`scripts/systems/battle/rules/battle_hit_resolver.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 354-382 / 687-694 / 200-227 | 🔴 | `natural_one_auto_miss` / `natural_twenty_auto_hit` 参数被完全忽略，硬编码天然1必失手/天然20必命中 | `build_force_hit_no_crit_attack_preview` 声称100%命中实际仍有5%失手率 |
| 609,611 | 🔴 | `_compute_fate_attack_success_rate_basis_points()` 中 `gate_crit_basis_points = 10000.0 / float(crit_gate_die)`，`crit_gate_die` 未校验是否为正 | 除零崩溃 |
| 714-717 / 385-401 / 675-680 | 🟡 | `_get_required_roll_for_hit_rate(100)` 返回1，但天然1仍失手，实际命中率95%，与宣称100%不一致 | UI 误导玩家 |
| 432-434 | 🟡 | `_roll_battle_d20()` 计算 `nonce` 但随机数调用完全未使用 nonce | 审计/回放功能缺失 |
| 76 / 448 | 🟡 | `int(level_key)` / `StringName(params.get(...))` 类型转换无校验 | 非法配置被静默掩盖 |
| 300 / 309 / 319 | 🟡 | `_get_target_armor_break_penalty()` 等直接访问 `status_entry.power` / `stacks` | 数据结构不规范时异常 |
| 333 | 🟡 | `_unit_has_status_bool_param()` `status_effects.keys()` 未检查 null | 单位未初始化时崩溃 |
| 344-351 | 🟢 | `_get_status_param_string_key()` O(n) 手动遍历字典 keys | 性能差，代码冗余 |
| 276-281 | 🟢 | `_get_unit_attribute_value()` 返回类型声明为 `int` 但未做 `int()` 转换 | 类型不匹配 |
| 35 | 🟢 | `_fate_attack_rules` 类级别实例化，若内部有可变状态则多实例共享 | 潜在副作用 |

---

## 文件 7：`progression_service.gd`

**路径**：`scripts/systems/progression/progression_service.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 159-185 | 🔴 | `_learn_composite_upgrade()` fallback 路径直接标记 `is_learned=true` 但未处理源技能 | 源技能仍保留 learned 状态，数据不一致 |
| 312-315 | 🔴 | `promote_profession()` 遍历 `consumed_skill_ids` 逐个分配，部分失败时直接 return false，已成功分配的不回滚 | 技能归属状态与晋升历史不一致 |
| 367-368 | 🔴 | `_roll_profession_hit_die()` 每次 new RNG 并 randomize() | 时间种子精度不足产生相同序列；不可复现 |
| 150 | 🟡 | `grant_racial_skill()` 直接赋 `skill_level = grant.minimum_skill_level`，未校验是否超过 effective max level | 中间状态超等级 |
| 987-991 | 🟡 | `_grant_profession_skills()` 无条件覆盖 `granted_source_type`/`granted_source_id` | 手动学习技能的来源被篡改 |
| 380-411 | 🟡 | `_index_skill_defs` / `_index_profession_defs` Dictionary 分支空 id 回退用 key，Array 分支空 id 直接丢弃 | 行为不一致 |
| 246-250 | 🟡 | `set_skill_core(true)` 未检查是否超出核心位上限 | 核心技能列表与职业核心位列表不同步 |
| 494 / 510 | 🟡 | `_can_satisfy_skill_level_requirements` / `_can_satisfy_attribute_requirements` 中 required_level/required_value 为 0 时判定为"无法满足" | 配置容错性差 |
| 976 | 🟡 | `_grant_profession_skills()` 未校验 `granted_skill.skill_id` 为空 | 存档中出现空 key |
| 210-216 | 🟡 | `grant_skill_mastery()` 已满级技能仍返回 true 并强制 `current_mastery = 0` | 静默吞掉多余熟练度 |
| 317-327 | 🟡 | `promote_profession()` rank 修改在 HP/技能授予之前 | 异常后处于"半晋升" |
| 708 | 🟢 | `_select_skill_ids_for_tag_rules()` 末尾 `return []` 为死代码 | 冗余 |
| 334 | 🟢 | `calculate_profession_hit_point_gain()` 公式为 `hit_die_roll + modifier * 2` | 与标准 D&D 3.5e 规则不同（需确认设计意图） |
| 100-109 / 463-476 | 🟢 | `learn_skill` 与 `_can_learn_composite_upgrade` 存在冗余校验 | 维护成本高 |

---

## 文件 8：`party_equipment_service.gd`

**路径**：`scripts/systems/inventory/party_equipment_service.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 257-258 | 🔴 | `equip_item()` preview 通过后若成员被移除，`_get_member_state` 返回 null，`_ensure_equipment_state(null)` 新建临时对象，装备写入后丢弃 | 调用者收到 success=true 但实际未装备 |
| 313-314 | 🔴 | `unequip_item()` 若 `item_def` 加载失败则拒绝卸下 | 装备永久卡死在槽中 |
| 271-272 / 326 | 🔴 | `deposit_equipment_instance()` 返回 void，失败无感知且无回滚 | 实例湮灭（数据丢失） |
| 279-285 | 🔴 | `equip_item()` `displaced_entries` 多条目时只汇报第一个 | 多 displaced 信息丢失 |
| 170-177 | 🔴 | `preview_equip()` `withdraw_entries` 混用 Dictionary 与 StringName | 类型契约破坏时崩溃 |
| 247-258 | 🟡 | `equip_item()` preview 与实际执行非原子 | 中间状态被修改可能不一致 |
| 327-328 | 🟡 | `unequip_item()` `pop_equipped_instance` 返回 null 时已内部清理，else 分支 `clear_slot` 冗余 | 语义混淆 |
| 36-38 | 🟡 | `get_equipment_state()` 对不存在成员返回全新孤儿 EquipmentState | 数据写入黑洞 |
| 354-362 | 🟡 | `_ensure_equipment_state()` / `_normalize_equipment_state()` 非字典输入导致 from_dict 返回 null | 连锁崩溃 |
| 27-28 | 🟢 | `setup()` 无条件重新 setup `_warehouse_service` | 可能覆盖已有状态 |
| 380-388 | 🟢 | `_resolve_target_slot()` 无空槽时总是替换 `allowed_slots[0]` | 对戒指等多槽位不友好 |
| 161-162 | 🟢 | `preview_equip()` `blockers` 类型假设为 Array 未校验 | 类型错误 |
| 49 | 🟢 | `get_equipped_entries()` 未校验 `is_equipment()` | 防御性不足 |
| 218 | 🟢 | `unequip_item()` 用 `preview_add_item(item_id, 1)` 检查装备实例 | 语义模糊 |

---

## 文件 9：`world_map_grid_system.gd`

**路径**：`scripts/systems/world/world_map_grid_system.gd`

| 行号 | 严重 | 问题描述 | 潜在影响 |
|------|------|---------|---------|
| 81-89 | 🔴 | `register_footprint()` 未检查 `entity_id` 是否已存在，旧 footprint 残留在 `_occupied_cells` 中永久无法清理 | "幽灵占用"，网格逐渐耗尽 |
| 81-89 | 🔴 | `register_footprint()` 不执行边界检查与占用冲突检查 | 可在世界外写入或静默覆盖其他实体 |
| 56-57 | 🔴 | `is_cell_walkable()` 仅检查 `is_cell_inside_world`，完全不检查 `_occupied_cells` | 函数名严重误导，实体可能重叠 |
| 60-62 | 🟡 | `get_occupant_root()` 未检查世界边界 | 越界坐标返回非空结果 |
| 40 | 🟡 | `get_cell()` 返回类型缺失 | 调用方易忘记 null 检查 |
| 22-29 | 🟡 | `setup()` 未验证 `world_size_in_chunks` 和 `chunk_size` 是否为正 | 世界静默失效 |
| 124-130 | 🟡 | `get_chunk_coord()` 仅检查 `_chunk_size == 0`，未防御负数 | Chunk 坐标计算错乱 |
| 140-149 / 157-166 | 🟡 | 反序列化时未验证取出的值类型 | 加载损坏存档时崩溃 |
| 82-83 | 🟡 | `register_footprint()` 对空 `entity_id` 静默返回 | 调用方误以为注册成功 |
| 65-78 / 81-89 | 🟢 | `can_place_footprint` 与 `register_footprint` 分离，API 层面不强制先检查再注册 | 误用风险 |
| 24-27 | 🟢 | `world_size_in_chunks.x * chunk_size.x` 整数溢出风险 | 世界静默不可用 |
| 135 / 155 | 🟢 | `is Object and has_method("is_empty")` 鸭子类型过于宽泛 | 数据混淆 |
| 44 | 🟢 | `get_cell()` 每次调用都 `new` 对象 | GC 压力大 |
| 97-98 | 🟢 | `clear_footprint()` 对 size 为负的防御依赖前置条件 | 数据残留 |

---

## 测试 / 工具脚本问题

| 文件 | 行号 | 严重 | 问题描述 |
|------|------|------|---------|
| `tests/text_runtime/run_text_command_regression.gd` | L851–L860 | 🔴 | 直接修改全局 `item_defs[&"bronze_sword"]`，异常时恢复代码不执行 |
| `tests/text_runtime/run_text_command_regression.gd` | L756–L824 | 🔴 | 直接向全局 `world_data["settlements"]` 注入虚构服务，测试后残留 |
| `tests/text_runtime/run_text_command_regression.gd` | L312 | 🔴 | 场景文件执行失败时 `return`，不执行 `runner.dispose(true)` |
| `tests/text_runtime/run_text_command_regression.gd` | L158–L176 | 🟡 | `_walk_to_coord` 硬编码 `guard < 256`，到达后仅断言但不阻止后续执行 |
| `tests/battle_runtime/run_battle_runtime_smoke.gd` | L223–L258 | 🔴 | 直接 `runtime._state = state` 绕过 `setup()` 初始化 |
| `tests/battle_runtime/run_battle_runtime_ai_regression.gd` | L210–L255 | 🔴 | 创建 `GameRuntimeFacade` 后未调用 `dispose/free` |
| `tests/battle_runtime/run_battle_runtime_ai_regression.gd` | L408–L441 | 🔴 | 循环遍历 template 每次创建新 runtime，无统一 cleanup |
| `tests/battle_runtime/run_battle_runtime_ai_regression.gd` | L543 | 🔴 | `decision` 为 null 时传入 `null` 给 `preview_command` |
| `tests/progression/run_progression_tests.gd` | L387–L504 | 🔴 | 直接修改 Registry 私有字段（下划线前缀） |
| `tests/progression/run_progression_tests.gd` | L693–L704 | 🟡 | 硬编码期望结果 `45`，与公式强耦合 |
| `tests/warehouse/run_party_warehouse_regression.gd` | L54 / L1188 | 🔴 | `_cleanup` 只调用 `clear_persisted_game()`，不调用 `free()` |
| `tests/warehouse/run_party_warehouse_regression.gd` | L208–L269 | 🟡 | 多次修改全局存档状态，中间失败时未回滚 |
| `tools/run_ralph_loop.py` | `invoke_codex_iteration()` | 🔴 | `process.wait()` 后 `join()` 线程，可能丢失尾部 JSONL |
| `tools/run_ralph_loop.py` | `render_codex_event_line()` | 🟡 | 非 JSON 行直接 `print(line)`，无错误标记 |
| `tools/run_ralph_review_loop.py` | `get_commit_range_lines()` | 🔴 | `range_value` 作为单个字符串参数传入 `git log`，空格会解析为多个参数 |
| `tools/build_battle_sim_analysis_packet.py` | `load_json()` / `write_json()` | 🟡 | 无 try/except 包装 |
| `tools/export_config_html.py` | 全局 `HELPER_SCRIPT` | 🟡 | 约 420 行 GDScript 硬编码为 Python 字符串，无法语法高亮 |
| `tools/run_ai_debate.ps1` | `Test-PathWithinRoot` | 🔴 | 使用字符串 `.StartsWith($Root)` 判断路径包含，存在前缀绕过风险 |

---

## 配置 / 文档 / 版本控制问题

| 位置 | 严重 | 问题描述 |
|------|------|---------|
| `.gitignore` 第 42 行 + 全仓库 | 🔴 | 216 个 `.uid` 文件被追踪但 `.gitignore` 忽略 `*.uid`，新文件被忽略而旧文件残留 |
| `prompts/skill_icon_prompts.md` | 🔴 | 声称 197 个技能，实际 `data/configs/skills/*.tres` 有 303 个，缺失约 106 个 |
| `docs/design/dnd35e_combat_system_vision.md` | 🟡 | `ProfessionDef` 应有 `fort_save/ref_save/will_save`、`ArmorProfile`/`ShieldProfile`、迭代攻击等，代码中均不存在但未标注 |
| `docs/design/project_context_units.md` | 🟡 | 有未提交的修改（`git diff` 显示 6 insertions, 3 deletions） |
| `README.md` 第 22 行 | 🟡 | 引用 `docs/tooling/ralph_loop.md`，该文件不存在 |
| 仓库根目录 | 🟡 | 缺少 `.gitattributes`，Windows 下 Git 默认替换 CRLF，跨平台协作污染 diff |
| `project.godot` | 🟢 | 无 `[input]` 段，若使用键盘输入则干净环境下按键绑定失效 |
| `README.md` 第 1 行 | 🟢 | 标题仍为 `# your_godot_game`，未改为实际项目名 |

---

## 全局跨文件问题

| 问题 | 影响 |
|------|------|
| 大量 getter 返回内部引用而非副本（`get_world_data()`、`get_party_state()` 等） | 外部可直接修改内部状态，违反封装 |
| `load()` 资源加载失败在很多路径只返回 null 或简单 push_error | 错误延迟暴露，难以定位 |
| 信号连接未检查返回值 | 连接失败时静默 |
| 测试脚本中 `game_session.free()` vs `clear_persisted_game()` 行为不一致 | 状态污染风险 |

---

## 建议优先修复顺序

### P0（立即修复，运行时崩溃 / 数据丢失）
1. `game_session.gd`：`_persist_game_state()` 部分写入与孤儿存档问题
2. `game_session.gd`：`load_save()` 半激活状态污染
3. `battle_runtime_module.gd`：`encounter_anchor.display_name` 空指针
4. `battle_damage_resolver.gd`：`_get_status_param_string_key()` StringName 匹配失败
5. `party_equipment_service.gd`：`equip_item()` 成员移除后装备写入临时对象丢弃
6. `party_equipment_service.gd`：`deposit_equipment_instance()` 失败无回滚导致实例湮灭
7. `game_runtime_facade.gd`：`dispose()` 未释放大量子系统
8. `world_map_grid_system.gd`：`register_footprint()` 幽灵占用

### P1（高优先级，功能失效 / 状态不一致）
9. `battle_hit_resolver.gd`：`natural_one_auto_miss` / `natural_twenty_auto_hit` 参数被忽略
10. `progression_service.gd`：复合升级 fallback 路径未处理源技能
11. `progression_service.gd`：职业晋升部分失败无回滚
12. `game_runtime_facade.gd`：`set_party_state()` 未同步子系统
13. `battle_ai_service.gd`：`ai_blackboard` 多处未初始化即写入
14. `game_session.gd`：`_generate_unique_save_id()` O(n²) 重复 I/O

### P2（中优先级，维护性 / 文档 / 版本控制）
15. 处理 `.uid` 文件追踪与 `.gitignore` 冲突
16. 添加 `.gitattributes` 统一换行符
17. 更新 `skill_icon_prompts.md` 技能数量
18. 标记 `dnd35e_combat_system_vision.md` 中未实现设计
19. 修正 `README.md` 引用断裂和项目名称
20. 为关键公共 API 补充返回类型注解

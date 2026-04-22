# Magic 可玩纵切路线图

更新日期：`2026-04-22`

## Summary

- 当前项目的核心循环已经通了：聚落服务 → 战斗产出 → 角色成长 → 再出发。
- 相比 `2026-04-16` 的版本，Phase 1（聚落服务运行化）、Phase 4（数据驱动迁移）以及「战斗系统技术欠债」列表均基本落地；Phase 2（内容宽度）、Phase 3（装备/经济闭环）剩下的是内容填充与少量细节。
- 本份路线图的剩余工作集中在：
  - 补齐战斗地形 profile 的第二套「狭道突击」/「守点推进」。
  - 进一步拉宽物品、敌人、任务内容目录。
  - 打磨掉落 → 入仓 → 反馈的体感细节（如无空位时的显式提示）。
- 执行策略未变，仍然：可玩纵切优先 → 聚落服务 → 内容宽度 → 经济闭环 → 数据迁移。
- 主干架构继续沿用：
  - `GameSession` 负责内容缓存与持久化
  - `GameRuntimeFacade` 负责世界态 / 战斗态 / modal 编排
  - `CharacterManagementModule` 负责角色成长与奖励入账
  - `PartyWarehouseService` 负责共享仓库

## 当前实施状态（2026-04-22）

### Phase 1 聚落服务

| 服务 | interaction_script_id | 状态 |
| --- | --- | --- |
| 仓储 | `party_warehouse` | ✅ 已落地 |
| 休整（篝火） | `service_rest_basic` | ✅ 已落地（免费，恢复 30% HP） |
| 休整（旅店整备） | `service_rest_full` | ✅ 已落地（50 金，全量恢复 HP/MP，推进世界时间） |
| 补给/商店 | `service_basic_supply` 等 | ✅ 已落地（SettlementShopService） |
| 驿站传送 | `service_stagecoach` 等 | ✅ 已落地（按格收费，已访问据点可选） |
| 乡野传闻 | `service_village_rumor` | ✅ 已落地（免费揭雾） |
| 情报网络 | `service_intel_network` | ✅ 已落地（50 金，大范围揭雾） |
| 锻造 | `service_forge_*` | ✅ 已落地（`SettlementForgeService` + 配方 modal，4 份 `RecipeDef` 已铺开） |
| 委托/任务 | `service_contract_board` | ✅ 已落地（`contract_board` modal + `QuestProgressService`，支持 `submit_item` / `defeat_enemy` 目标推进与领奖） |
| 研究 | `service_research_*` | ✅ 已落地（`SettlementResearchService`，奖励走 `PendingCharacterReward`） |

第二批（`service_repair_gear`、`service_join_guild`、`service_identify_relic` 等 17 项）继续保持 `UNIMPLEMENTED_INTERACTION_IDS` 锁定态，非本轮目标。

### Phase 2 内容宽度

| 类别 | 当前 | 目标 | 缺口 |
| --- | --- | --- | --- |
| 单手武器 | 5（bronze_sword / militia_axe / scout_dagger / watchman_mace + iron_greatsword 双手） | ≥4 类单手 | ✅ 已达标 |
| 双手武器 | 1（iron_greatsword） | — | ✅ |
| 防具 | 2（leather_jerkin / leather_cap） | 3 槽 | ⚠️ 缺 accessory 类覆盖（仅 scout_charm） |
| 饰品 | 1（scout_charm） | — | ⚠️ 建议再补 1–2 款 |
| 消耗品 | 5（healing_herb / antidote_herb / bandage_roll / torch_bundle / travel_ration） | ≥4 类 | ✅ 已达标 |
| 材料 | 7（iron_ore / beast_hide / forge_coal / hardwood_lumber / linen_cloth / whetstone / moonfern_sample） | ≥6 类 | ✅ 已达标 |
| 任务物品 | 2（bandit_insignia / sealed_dispatch） | ≥3 类 | ⚠️ 再补 1 类 |
| 敌人模板 | 8（wolf_alpha / wolf_pack / wolf_raider / wolf_vanguard / wolf_shaman / mist_beast / mist_harrier / mist_weaver） | 8 个，覆盖 4 类职责 | ✅ 已达标 |
| AI brains | 5（frontline_bulwark / healer_controller / melee_aggressor / ranged_controller / ranged_suppressor） | 覆盖 4 类职责 | ✅ 已达标 |
| 战斗地形 profile | 1（`default`） | 补「狭道突击」/「守点推进」 | ❌ 未实现，`terrain_profile_id` 仍只用 `default` |

`ItemDef` 字段状态：
- `item_category` / `equipment_slot_ids` / `attribute_modifiers` ✅
- `base_price` ✅
- `buy_price` / `sell_price` ✅（已落地，独立字段支撑商店差价与配方引用）
- `tags` ✅（已落地，支撑配方过滤、任务条件、掉落筛选）

### Phase 3 装备/掉落/经济闭环

| 项目 | 状态 |
| --- | --- |
| 战斗结算掉落入仓 | ✅ 已落地（`WildEncounterRosterDef.drop_entries` + `EncounterRosterBuilder`） |
| 仓库满时显式提示 | ⚠️ 暂无可见空间上限，`WarehouseState` 只有 per-stack `max_stack`。本轮可延迟，等到真正出现容量约束时再补 |
| 锻造材料消耗/产出 | ✅ 已落地（`SettlementForgeService` + 4 份 `RecipeDef`） |
| 任务完成条件 | ✅ `submit_item` / `defeat_enemy` 已接入 `QuestProgressService`；`settlement_action` 条件尚未实现 |
| 装备槽位/装备属性进入快照 | ✅（`equipment_instance_state` + `PartyEquipmentService`） |
| 货币流转 | ✅（`PartyState.gold` 支持消费/奖励/商店/驿站） |

### Phase 4 数据驱动迁移

| 内容 | 状态 |
| --- | --- |
| 职业 → `data/configs/professions/` | ✅ 已落地（7 份 profession 资源，`ProfessionContentRegistry._scan_directory`） |
| 技能 → `data/configs/skills/` | ✅ 已落地（185 份 skill 资源，`SkillContentRegistry._scan_directory`） |
| 敌人模板 / brain / roster → `data/configs/enemies/` | ✅ 已落地（`EnemyContentRegistry._scan_directory` 覆盖 brains / templates / rosters 三目录；兼容桥接 `enemy_content_seed.tres` 仍保留） |
| 资源校验覆盖（缺失 id / 重复 id / 非法引用） | ✅（`content_validation_runner` + `run_resource_validation_regression`） |

### 战斗系统技术欠债

> 这个列表在 `2026-04-16` 版本列为 Phase 3 前置瓶颈；现在已经全部落地，保留在这里是为了归档。

| 缺口 | 状态 |
| --- | --- |
| 命中模型 | ✅ 已切到 BAB + 降序 AC + d20（`BattleHitResolver`，含天然 1 / 天然 20 特判，deterministic d20 走 `attack_roll_nonce`） |
| 冷却/耐力循环 | ✅ `cooldown_tu` / `stamina_cost` 已接入运行时扣费与回合推进 |
| Aura 独立资源 | ✅ 已独立为 `AURA_MAX` / `current_aura` / `aura_cost`，与 HP/MP/Stamina/AP 并列 |
| 状态效果语义表 | ✅ `BattleStatusSemanticTable` 提供 20+ 状态常量、stack_mode、tick_mode 统一语义 |
| 范围图形 | ✅ `BattleGridService` 支持 single / diamond / square / cross / line / cone / radius / self |
| AI 技能评分 | ✅ `BattleAiScoreService` 已替代顺序优先，走分数模型 |

## 剩余待做（截止 2026-04-22）

排序按「对可玩性的阻塞程度」。

1. **战斗地形 profile**（Phase 2 最后一项）
   - 新增「狭道突击」/「守点推进」两个 profile，挂接不同 prop 组合与敌人 roster，让 `terrain_profile_id` 真正分流。
   - 阻塞点：当前 `battle_state.terrain_profile_id` 只有 `default`，战斗视觉/站位意图无差异。
2. **Phase 2 内容厚度补齐**
   - 再补 1–2 款 accessory / 饰品（如防御属性饰品、MP 容量饰品），覆盖非 `scout_charm` 的成长路径。
   - 再补 1 类任务物品（如线索类 / 配送类），拉满 `≥3 类` 目标。
   - 再补 1 份 AI brain 或战斗职责细分（非强制）。
3. **任务条件扩展**（Phase 3 尾部）
   - 支持第三类完成条件：`execute_settlement_action`（例如「使用情报网络揭露指定区域」）。
   - 现有 `submit_item` / `defeat_enemy` 已经够完整，本项优先级可视实际剧情需要。
4. **掉落入仓提示**（延迟执行）
   - 仅当引入仓库容量上限时再做；当前仓库无容量约束，不需要立刻补这条反馈。
5. **Phase 4 收尾清理**
   - `scripts/enemies/enemy_content_seed.gd` 与 `data/configs/enemies/enemy_content_seed.tres` 的兼容桥接在所有 roster/brain/template 资源迁移完成后可评估删除。
   - 确认无引用后清理 `battle_map_view.gd` legacy。
6. **锁定服务（不在本轮范围）**
   - `service_repair_gear`、`service_join_guild`、`service_identify_relic` 等 17 项保持锁定态，不在本轮目标。

## 当前仓库事实

- 聚落服务配置完整落在 `data/configs/world_map/*.tres`；`service_type` / `interaction_script_id` / NPC 绑定关系已接通。
- `GameRuntimeSettlementCommandHandler` 是聚落动作正式分发入口，支持 forge modal / contract_board modal / research 服务 / 休整 / 商店 / 驿站 / 揭雾 / 仓库。
- `CharacterManagementModule.record_achievement_event(...)` 已支持据点行为成就。
- `PartyMemberState.current_hp/current_mp` 会持久化到世界态，休整已基于此做真实恢复。
- `ItemContentRegistry` / `ProfessionContentRegistry` / `SkillContentRegistry` / `EnemyContentRegistry` 全部采用目录扫描模式。
- `PartyState` 已有 `gold`、`active_quests`、`completed_quest_ids` 并接入任务流程。
- `QuestDef` / `QuestState` / `RecipeDef` schema 均已落地，`QuestDef.objective_defs` 支持 `submit_item` / `defeat_enemy` 目标类型。
- 战斗命中走 `BattleHitResolver`（BAB + 降序 AC + d20，deterministic），详见 `docs/design/skills_implementation_plan.md`。

## 目标与非目标

### 目标

- 让玩家在当前世界地图中获得完整的「据点服务价值」。
- 让战斗产出、聚落消耗、角色成长、仓库管理形成稳定闭环。
- 把内容扩展成本从「继续硬编码」切到「可配置扩张」——已基本达成。

### 非目标

- 本轮不做多人 / 联机。
- 本轮不做完整城内地图。
- 本轮不做音频、特效、角色动画主导的表现升级。
- 本轮不做复杂经济、随机商店、装备词缀、组合技、动态难度缩放。

## Phase 1：聚落服务运行化（已完成）

### 已落地的服务

- `仓储` ✅（`party_warehouse`，入口收口到 `GameRuntimeSettlementCommandHandler`）
- `休整` ✅（`service_rest_basic` / `service_rest_full`）
- `补给` ✅（`SettlementShopService` + 驿站传送 `service_stagecoach`）
- `揭雾` ✅（`service_village_rumor` / `service_intel_network`）
- `锻造` ✅（`SettlementForgeService`；resource：`data/configs/recipes/forge_*.tres`、`master_reforge_iron_greatsword.tres`）
- `委托/任务` ✅（`contract_board` modal + `QuestProgressService`，battle/settlement 均可推进）
- `研究` ✅（`SettlementResearchService`，奖励走 `PendingCharacterReward`）

### 第二批保留锁定态的服务

以下 17 项保持 `UNIMPLEMENTED_INTERACTION_IDS` 锁定：

`service_repair_gear`、`service_contract_board`（已从锁定态移出，由正式 contract_board 替代）、`service_join_guild`、`service_identify_relic`、`service_bounty_registry`、`service_recruit_specialist`、`service_issue_regional_edict`、`service_unlock_archive`、`service_diplomatic_clearance`、`service_amnesty_review`、`service_elite_recruitment`、`service_master_reforge`、`service_respecialize_build`、`service_manage_reputation`、`service_open_trade_route`、`service_legend_contracts`、`service_hire_expert`

### Phase 1 完成标准达成情况

- ✅ 锻造、任务、研究三项服务已落地真实状态变化。
- ✅ 任务系统：接取 → 推进 → 完成 → 领奖闭环在 headless 文本回归中已验证。
- ✅ 奖励和持久化继续走现有 runtime 主链。
- ✅ `settlement action service:warehouse` 文本回归继续通过。

## Phase 2：内容宽度补齐（主要达成，剩 profile 与少量内容）

### 已达成

- 物品 22 份，覆盖单手 / 双手 / 防具 / 饰品 / 消耗品 / 材料 / 任务物品。
- `ItemDef` 字段已扩展：`buy_price` / `sell_price` / `tags`。
- 敌人模板 8 份，AI brain 5 份，覆盖近战冲锋 / 前排承伤 / 远程压制 / 治疗控制。
- `EnemyContentRegistry` 已能从 `data/configs/enemies/` 三个子目录完成注册。

### 待补

- 战斗地形 profile：当前仍仅有 `default`，需补「狭道突击」/「守点推进」两套，挂接 prop 组合与敌人 roster。
- 饰品类与任务物品再各补 1 份，拉满目标覆盖。

### Phase 2 完成标准达成情况

- ✅ 世界探索中能稳定遇到不止两类敌人。
- ✅ 商店、锻造、掉落、任务能引用到同一批正式物品定义。
- ❌ 至少两种战斗 profile 在视觉和站位意图上可区分——**仍未达成**。

## Phase 3：装备、掉落、经济闭环（主体达成）

### 已达成

- 装备槽位固定 `main_hand` / `off_hand` / `head` / `body` / `accessory`，`PartyEquipmentService` 已落地。
- 装备属性进入角色快照（`PartyEquipmentSnapshotService`）。
- 货币单一 `gold`，已持久化到 `PartyState`。
- 战斗结算支持掉落入仓（`EncounterRosterBuilder` + `WildEncounterRosterDef.drop_entries`）。
- 补给支持购买（`SettlementShopService`）。
- 锻造消耗材料并产出成品（`SettlementForgeService` + 4 份 recipe 资源）。
- 任务完成条件：`submit_item` / `defeat_enemy` 已接入。

### 待补

- 任务完成条件的 `execute_settlement_action` 分支（按剧情需要推进，非强制）。
- 仓库容量上限与「装不下」反馈链路——暂时没有容量约束，延后。

### Phase 3 完成标准达成情况

- ✅ 玩家可以通过战斗获得材料。
- ✅ 可以在据点购买 / 制作 / 装备物品。
- ✅ 装备属性会影响角色快照，并进入后续战斗。
- ✅ 至少存在一条完整链路：战斗掉落材料 → 锻造装备 → 装备提升 → 再次战斗更强。

## Phase 4：数据驱动迁移与遗留清理（已完成）

### 已达成

- `professions` → `data/configs/professions/`（7 份）。
- `skills` → `data/configs/skills/`（185 份）。
- `enemy templates / brains / rosters` → `data/configs/enemies/`。
- registry 全部改为「扫描 + 校验 + 索引」职责；真相源已切到资源文件。
- 资源校验流程（`content_validation_runner`）覆盖缺失 id / 重复 id / 非法引用。

### 剩余收尾

- `enemy_content_seed.tres` / `enemy_content_seed.gd` 的兼容桥接在确认不再需要后可删除。
- `battle_map_view.gd` 仍标记 legacy，确认无引用后可下线。
- `assets/sprites/`、`assets/fonts/`、`scenes/enemies/`、`scenes/levels/` 正式内容落点仍为表现升级时再处理。

## Public Interfaces（现状）

- `PartyState`
  - `gold: int` ✅
  - `active_quests: Array` ✅
  - `completed_quest_ids: Array[StringName]` ✅
- `ItemDef`
  - `item_category`、`equipment_slot_ids` / `occupied_slot_ids`、`attribute_modifiers`、`base_price`、`buy_price`、`sell_price`、`tags` 全部已落地。
- `RecipeDef` ✅（schema + 资源 + 执行器）
- `QuestDef` ✅（schema + `objective_defs` + 接取/推进/完成）
- 聚落服务统一执行接口：`GameRuntimeSettlementCommandHandler.execute_settlement_action(settlement_id, action_id, payload) -> Dictionary` ✅

## Test Plan（当前主要回归覆盖）

### 文本 / headless 回归

- `休整` 能恢复 `current_hp/current_mp` ✅
- `补给` 购买后金币减少、物品入仓 ✅
- `锻造` 消耗材料并产生成品 ✅（`run_forge_service_regression` 等）
- `任务` 能接取 / 完成 / 领奖 ✅（`run_quest_progress_*`）
- `研究` 能排入奖励队列并在 modal 关闭后展示 ✅

### 仓库回归

- 掉落入仓与战斗结算一致 ✅
- 商店购买入仓 ✅
- 锻造成品入仓 ✅
- 装备穿脱与仓库联动 ✅
- 容量不足提示 ⚠️（当前无容量约束，未覆盖）

### progression 回归

- 装备属性进入角色属性快照 ✅
- 研究解锁技能 / 知识后成就推进正常 ✅
- 据点服务动作继续触发 `settlement_action_completed` ✅

### battle/runtime 回归

- 新敌人模板进入正式战斗 ✅
- 掉落池与战斗结算一致 ✅
- 新战斗 profile 通过 battle smoke / board 契约 ⚠️（仅 `default` profile，未跑新 profile 回归）
- AI vs AI 模拟回归 ✅（`run_battle_ai_vs_ai_simulation_regression`）

### 资源校验

- 缺失 id / 重复 id / 非法 `equip_slot` / 非法配方引用 / 非法任务目标引用 ✅

## 执行顺序约束

- Phase 1 / Phase 4 / 战斗技术欠债均已落地；Phase 2 剩余项（战斗地形 profile、少量内容）与 Phase 3 收尾可并行。
- 表现层升级、锁定服务、多人联机不排入当前路线图。
- 任何大幅度体系改动（如新信仰系统、装备耐久、D&D 武器）请先更新对应计划文档，再排入后续路线图。

## Assumptions

- 本路线图的唯一优先级仍是「尽快形成一个可持续游玩的单机版本」。
- 聚落仍采用窗口交付，不进入城内可行走地图。
- 战斗命中模型已切到 BAB + 降序 AC + d20（见 `docs/design/skills_implementation_plan.md`），经济闭环的数值校准基于此口径。
- 信仰系统（`faith_system_plan.md`）、装备耐久（`equipment_durability_plan.md`）、D&D 武器系统（`dnd_weapon_system_initial_plan.md`）均为计划内但本轮不开工的系统。

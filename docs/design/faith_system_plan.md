# 信仰 / 命运阶位系统当前规格

更新日期：`2026-04-26`

## 关联上下文单元

- CU-11：队伍与角色成长运行时数据模型
- CU-12：CharacterManagement、成就记录、奖励归并桥
- CU-15：战斗运行时总编排

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文记录 FaithService、Fortuna / Misfortune 阶位与当前未接入的 faith settlement 缺口。

## Summary

- 本文是当前 faith rank 系统的维护入口，不再是预实现方案。
- 当前实现已经落地 `FaithService`、`FaithDeityDef`、`FaithRankDef` 与 `data/configs/faith/*.tres`。
- 当前 faith 的正式切片不是通用神殿 UI，而是围绕 Fortuna / Misfortune 的命运阶位：
  - `fortuna` 使用 `faith_luck_bonus` 作为 rank progress stat。
  - `misfortune_black_crown` 使用 `doom_authority` 作为 rank progress stat。
- 阶位结果不写入独立 `UnitFaithState`；当前用角色永久属性容器 `UnitBaseAttributes.custom_stats` 保存 rank progress 与门票标记。
- 升阶奖励只通过 `PartyState.pending_character_rewards` 排队，再由 `CharacterManagementModule.apply_pending_character_reward(...)` 正式入账。
- 当前 `data/configs/world_map/ashen_intersection_world_map_config.tres` 仍只有 `category = "faith"` / `interaction_type = "faith"` 的设施占位；没有正式 `service_faith` 聚落按钮链路。

## 当前仓库事实

- 数据模型：
  - `PartyState.version = 3`。
  - `PartyState.gold` 已是正式字段，并提供 `get_gold()` / `set_gold()` / `add_gold()` / `can_afford()` / `spend_gold()`。
  - `PartyState.main_character_member_id` 已是严格合同字段；反序列化时缺失、为空或指向不存在成员会失败。
  - `PartyState.fate_run_flags` 保存周目级命运尝试锁，例如 Fortuna 标记尝试。
  - `PartyState.meta_flags` 保存通用剧情 / 事件去重标记，例如低 luck 事件。
  - `PartyMemberState -> UnitBaseAttributes.custom_stats` 保存 `faith_luck_bonus`、`fortune_marked`、`doom_marked`、`doom_authority`、`calamity_capacity_bonus` 等永久或半永久标记。
- 静态内容：
  - `scripts/player/progression/faith_deity_def.gd`
  - `scripts/player/progression/faith_rank_def.gd`
  - `data/configs/faith/fortuna.tres`
  - `data/configs/faith/misfortune_black_crown.tres`
- 服务：
  - `scripts/systems/faith_service.gd` 扫描 `res://data/configs/faith`。
  - `FaithService.execute_devotion(party_state, member_id, deity_id)` 只负责 rank gate、扣金、构造 pending reward。
  - `FaithService` 当前不是 `GameSession` 内容缓存的一部分，也不是 `ProgressionContentRegistry` 的扫描域。
- 命运 sidecar：
  - `FortuneService` 在 battle fate event 中按 per-run 尝试锁写入 `fortune_marked`。
  - `FortunaGuidanceService` 把 Fortuna guidance 条件翻译成 `fortuna_guidance_*` achievement。
  - `MisfortuneBlackOmenService` 是 `doom_marked` 的受控写入口。
  - `MisfortuneGuidanceService` 把 Misfortune guidance 条件翻译成 `misfortune_guidance_*` achievement。
- 展示：
  - `PartyManagementWindow` 当前显示 `信仰幸运加值`。
  - `CharacterInfoWindow` / battle HUD fate 信息会展示 `faith_luck_bonus`、`fortune_marked`、`doom_marked` 与 Misfortune 状态。
- 回归：
  - `tests/progression/fate/run_faith_service_regression.gd`
  - `tests/progression/fate/run_fortuna_guidance_regression.gd`
  - `tests/progression/fate/run_misfortune_guidance_regression.gd`
  - `tests/progression/fate/run_misfortune_black_omen_regression.gd`
  - `tests/progression/fate/run_fortune_service_regression.gd`
  - `tests/progression/fate/run_party_state_fate_regression.gd`
  - `tests/world_map/ui/run_character_info_window_fate_regression.gd`

## 当前非事实

以下是旧方案里已经不成立的描述，后续不要按这些口径实现：

- 不存在 `UnitFaithState` / `FaithDevotionState` 运行时状态对象。
- 不存在 `UnitProgress.faith_state` 字段。
- 不存在 `GameSession.get_faith_deity_defs()` 或 `ProgressionContentRegistry.get_faith_deity_defs()`。
- 当前没有 `FacilityNpcConfig.deity_id` 正式透传链，也没有 `WorldMapSpawnSystem._collect_services()` 的 faith deity payload。
- 当前没有正式 `service_faith` settlement action。
- 当前不是“累计供奉金币后检查累计阈值”；`FaithRankDef.required_gold` 是每次升该阶需要支付的金币。
- 当前没有“普通角色单神 / 主角多神”的通用强制裁决；现有 regression 主要用主角样例验证 Fortuna / Misfortune 两条线。

## 核心设计结论

### 一、当前 faith rank 归属角色永久构筑

- rank progress stat 写在 `UnitBaseAttributes.custom_stats`。
- `FaithDeityDef.rank_progress_stat_id` 决定当前 deity 的 rank 读取字段：
  - Fortuna：`faith_luck_bonus`
  - Misfortune：`doom_authority`
- `FaithService.get_current_rank(...)` 的口径是：
  - 已入账的 rank progress stat
  - 加上同一 deity、同一 member、同一 rank progress stat 的 pending faith reward 数量
  - 再 clamp 到该 deity 的 `get_max_rank()`
- 因此升阶 reward 入队后、正式确认前，也会被视为“下一次 devotion 的当前 rank 已推进”，避免重复排同一阶奖励。

### 二、升阶只推进一阶

- `execute_devotion(...)` 每次最多推进一个 rank。
- 当前流程：
  1. 查 member / deity。
  2. 根据 rank progress stat 计算当前 rank。
  3. 读取 `current_rank + 1` 的 `FaithRankDef`。
  4. 检查金币、等级、自定义属性门票、achievement 门票。
  5. 扣除 `required_gold`。
  6. 构造 `PendingCharacterReward`。
  7. 入队 `PartyState.pending_character_rewards`。
- 达到 max rank 后返回 `error_code = "max_rank_reached"`，不会继续扣金或排奖励。

### 三、门票分两类

- `required_custom_stat_id + required_custom_stat_min_value`
  - 用于 rank 1 的入门门票。
  - Fortuna rank 1 要求 `fortune_marked >= 1`。
  - Misfortune rank 1 要求 `doom_marked >= 1`。
- `required_achievement_id`
  - 用于 rank 2-5 的 guidance 门票。
  - Fortuna：`fortuna_guidance_true/devout/exalted/blessed`。
  - Misfortune：`misfortune_guidance_true/devout/exalted/blessed`。
- `FaithRankDef.validate()` 禁止同一 rank 同时混用 custom-stat gate 与 achievement gate。

### 四、奖励只走 PendingCharacterReward

- `FaithService` 不直接修改 `faith_luck_bonus`、`doom_authority` 或其它奖励属性。
- 它只构造：
  - `source_type = "faith_rank_reward"`
  - `source_id = deity_id`
  - `source_label = deity display_name`
  - `summary_text = "<deity> 晋升为 <rank_name>"`
- 奖励条目沿用 `PendingCharacterRewardEntry` shape，当前正式使用：
  - `attribute_delta`
  - `knowledge_unlock`
- 正式属性 / 知识变更由 `CharacterManagementModule.apply_pending_character_reward(...)` 完成。

### 五、Fortuna 与 Misfortune 是当前正式内容

#### Fortuna

- 文件：`data/configs/faith/fortuna.tres`
- `deity_id = "fortuna"`
- `display_name = "Fortuna"`
- `rank_progress_stat_id = "faith_luck_bonus"`
- 5 阶：
  - `浅信徒`：500 gold，要求 `fortune_marked >= 1`，奖励 `faith_luck_bonus +1`
  - `真信徒`：2000 gold，level 8，要求 `fortuna_guidance_true`，奖励 `faith_luck_bonus +1`
  - `虔诚信徒`：4500 gold，level 14，要求 `fortuna_guidance_devout`，奖励 `faith_luck_bonus +1`
  - `至诚信徒`：8000 gold，level 20，要求 `fortuna_guidance_exalted`，奖励 `faith_luck_bonus +1`
  - `神眷者`：14000 gold，level 28，要求 `fortuna_guidance_blessed`，奖励 `faith_luck_bonus +1`

#### Misfortune

- 文件：`data/configs/faith/misfortune_black_crown.tres`
- `deity_id = "misfortune_black_crown"`
- `display_name = "Misfortune"`
- `rank_progress_stat_id = "doom_authority"`
- 5 阶：
  - `见厄者`：500 gold，要求 `doom_marked >= 1`，奖励 `doom_authority +1` 与 `knowledge_unlock black_star_brand`
  - `灾厄持灯者`：2000 gold，level 8，要求 `misfortune_guidance_true`，奖励 `doom_authority +1` 与 `calamity_capacity_bonus +1`
  - `折冠者`：4500 gold，level 14，要求 `misfortune_guidance_devout`，奖励 `doom_authority +1` 与 `knowledge_unlock crown_break`
  - `厄运代行者`：8000 gold，level 20，要求 `misfortune_guidance_exalted`，奖励 `doom_authority +1` 与 `calamity_capacity_bonus +1`
  - `黑冕宣判者`：14000 gold，level 28，要求 `misfortune_guidance_blessed`，奖励 `doom_authority +1` 与 `knowledge_unlock doom_sentence`

## 数据模型

### `PartyState`

- 正式字段：
  - `gold: int`
  - `main_character_member_id: StringName`
  - `fate_run_flags: Dictionary`
  - `meta_flags: Dictionary`
  - `pending_character_rewards: Array[PendingCharacterReward]`
- 约束：
  - `from_dict()` 只接受 `version == 3`。
  - `main_character_member_id` 必须存在并指向有效成员。
  - `fate_run_flags` 与 `meta_flags` 缺失时回退为空字典；类型错误时拒绝载入。

### `UnitBaseAttributes.custom_stats`

- 当前 faith / fate 相关字段均走 custom stats：
  - `hidden_luck_at_birth`
  - `faith_luck_bonus`
  - `fortune_marked`
  - `doom_marked`
  - `doom_authority`
  - `calamity_capacity_bonus`
- `PartyMemberState.get_faith_luck_bonus()` 只是读取 `UnitBaseAttributes.get_faith_luck_bonus()`。
- `FateAttackFormula` 使用 `hidden_luck_at_birth + faith_luck_bonus` 派生战斗幸运、crit threshold 等 fate 攻击参数。

### `FaithDeityDef`

- 字段：
  - `deity_id`
  - `display_name`
  - `facility_id`
  - `service_type_label`
  - `power_domain_tags`
  - `rank_progress_stat_id`
  - `rank_defs`
- 校验：
  - 必须有 `deity_id`、`display_name`、`rank_progress_stat_id`。
  - rank 索引必须连续。
  - 每个 rank 必须包含一条指向 `rank_progress_stat_id` 的 `attribute_delta`，否则该 deity 无法稳定计算当前 rank。

### `FaithRankDef`

- 字段：
  - `rank_index`
  - `rank_name`
  - `required_gold`
  - `required_level`
  - `required_custom_stat_id`
  - `required_custom_stat_min_value`
  - `required_achievement_id`
  - `reward_entries`
- 校验：
  - `rank_index >= 1`
  - `required_gold >= 0`
  - `required_level >= 0`
  - 不能同时设置 custom-stat gate 与 achievement gate
  - 每条 reward entry 必须有 `entry_type`、`target_id`、非零 `amount`

## 运行链路

### Fortuna 标记

- `FortuneService` 订阅 `BattleFateEventBus` 的 `critical_success_under_disadvantage`。
- 成功触发时按 per-run flag 记录尝试，并在二次确认成功后写入 `fortune_marked = 1`。
- `FortunaGuidanceService` 必须先于 `FortuneService` 绑定同一 bus，以便读取 pre-mark 状态，避免第一次授予 `fortune_marked` 的事件顺带解锁 `fortuna_guidance_true`。

### Fortuna guidance

- `FortunaGuidanceService` 把 rare fate payload、battle/chapter 结束回调翻译成一次性 achievement：
  - `fortuna_guidance_true`
  - `fortuna_guidance_devout`
  - `fortuna_guidance_exalted`
  - `fortuna_guidance_blessed`
- guidance achievement 本身不应再排额外 reward；它只是 rank gate。

### Misfortune 入门与 guidance

- `MisfortuneBlackOmenService` 是 `doom_marked = 1` 的受控写入口。
- `MisfortuneGuidanceService` 消费 battle runtime 中的 calamity/reason 状态、seal/boss 结算结果与 forge canonical result，写入：
  - `misfortune_guidance_true`
  - `misfortune_guidance_devout`
  - `misfortune_guidance_exalted`
  - `misfortune_guidance_blessed`
- `MisfortuneService` 维护 battle-local calamity、reverse fortune 与黑星 / 折冠 / 宣判 / 封印技能的施法锁；这些 battle-local 数据不进入永久存档。

### Rank devotion

- 直接入口是 `FaithService.execute_devotion(...)`。
- 当前测试通过直接构造 `PartyState` 与 `FaithService` 验证，不依赖 settlement UI。
- 若后续要接入据点服务，应新增显式 `service_faith` runtime command，并同步 `project_context_units.md` 的 CU-06 / CU-08 / CU-12 read set。

## UI 与快照表面

- `PartyManagementWindow` 当前只显示 `信仰幸运加值`。
- `CharacterInfoWindow` 的 fate 信息显示：
  - 出生幸运
  - 信仰幸运加值
  - 战斗幸运
  - Fortuna 标记
  - Misfortune 黑兆
  - Misfortune 权柄
- 当前没有独立 faith 面板，也没有神灵阶位列表 UI。
- 当前没有 text runtime 的 `faith` 命令域。

## 测试计划

### 当前应运行

- `godot --headless --script tests/progression/fate/run_faith_service_regression.gd`
- `godot --headless --script tests/progression/fate/run_fortuna_guidance_regression.gd`
- `godot --headless --script tests/progression/fate/run_misfortune_guidance_regression.gd`
- `godot --headless --script tests/progression/fate/run_misfortune_black_omen_regression.gd`
- `godot --headless --script tests/progression/fate/run_fortune_service_regression.gd`
- `godot --headless --script tests/progression/fate/run_party_state_fate_regression.gd`
- `godot --headless --script tests/world_map/ui/run_character_info_window_fate_regression.gd`

### 当前覆盖

- Faith config 扫描与基础校验。
- Fortuna / Misfortune rank 1-5 的金币、等级、gate、reward shape。
- Fortuna 升阶后 `faith_luck_bonus` 经 pending reward 入账到 5。
- Misfortune 升阶后 `doom_authority` 经 pending reward 入账到 5。
- Misfortune 的技能占位 knowledge 与 calamity capacity bonus 入账。
- `fortune_marked`、`doom_marked` 与 guidance achievement 的解锁链。
- `PartyState.fate_run_flags` round-trip。
- 角色信息窗口 fate 字段展示。

## 后续缺口

- 是否继续把 faith 作为 fate 子系统的一部分，还是扩展成通用神殿服务，需要单独决策。
- 如果要接入聚落：
  - 新增正式 `service_faith` action。
  - 定义 settlement service payload 中的 `deity_id` 来源。
  - 决定是否由 `FacilityNpcConfig` 或 service payload 持有 deity 绑定。
  - 补 text runtime / snapshot / UI 回归。
- 如果要支持普通角色单神、主角多神：
  - 需要定义当前 rank progress stat 模型如何表达“已信奉某 deity”。
  - 不能只看 `faith_luck_bonus` 或 `doom_authority`，否则跨 deity 的泛化会失真。
- 如果未来要保存每个 deity 的累计供奉、历史 rank、改信状态，才应重新引入 `UnitFaithState` / `FaithDevotionState`；当前实现不需要这层状态。
- 如果增加第三位 deity，必须提供独立 `rank_progress_stat_id` 或明确其与现有 stat 的叠加关系，避免 rank 读取互相污染。

## 默认假设

- 当前正式可用 deity 是 `fortuna` 与 `misfortune_black_crown`。
- 当前 faith rank 的持久化结果是 custom stat，不是独立 faith state。
- 当前 rank gate 的“指引”是一次性 achievement，不是 knowledge item。
- 当前 rank reward 必须经 pending reward 确认后才算正式入账。
- 后续相关设计更新统一落在本文档。

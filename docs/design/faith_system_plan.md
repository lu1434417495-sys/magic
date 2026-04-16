# 信仰系统 v1 方案

更新日期：`2026-04-15`

## Summary

- 新增“角色向对应神殿供奉金币，按神灵独立累计信仰阶位”的正式系统。
- 信仰系统是主角区别于其他可培养角色的唯一正式特权来源。
- 正式信仰层级固定为 5 层：
  - `浅信徒`
  - `真信徒`
  - `虔诚信徒`
  - `至诚信徒`
  - `神眷者`
- 由于信仰奖励会非常强力，正式解锁节奏按高门槛设计：
  - `浅信徒` 仍保留“只靠供奉解锁”的首段规则
  - 从 `真信徒` 开始，每一阶都采用 `高额供奉 + 神灵指引 + 人物成长里程碑` 的三重门槛
- 升阶链路固定为：
  - 初次供奉达到阈值成为 `浅信徒`
  - 后续每一阶都同时要求：
    - 对应神灵累计供奉达到下一档阈值
    - 角色已通过特殊成就获得该阶所需的“神灵指引”知识
    - 角色达到该阶要求的最低成长强度
- 主角与普通角色的规则分离：
  - 固定主角可同时信奉多个神灵
  - 其他角色同一时间只能信奉一个神灵
- 设计目标固定为：
  - 普通角色的单神信仰提供“强专精”
  - 主角通过多神堆叠，在后期达到同等培养角色约 `3-4 倍` 的总战力上限
- 首版既支持信仰阶位状态与门槛，也支持升阶后的属性、知识、技能奖励。
- 首版只落 1 个完整可玩的示例神灵，但底层结构按多神扩展设计。

## 当前仓库事实

- `data/configs/world_map/ashen_intersection_world_map_config.tres` 已有 `category = "faith"` 和 `interaction_type = "faith"` 的设施占位。
- 据点服务正式入口已经固定在 `GameRuntimeFacade.command_execute_settlement_action()`，不是 UI 层。
- 角色永久成长真源当前挂在 `PartyMemberState.progression -> UnitProgress`。
- 仓库已有可复用的长期状态容器：
  - `UnitProgress.reputation_state`
  - `PartyState.pending_character_rewards`
  - `CharacterManagementModule.apply_pending_character_reward(...)`
- 当前没有正式货币字段，但 `docs/design/playable_vertical_slice_roadmap.md` 已经把 `gold` 放进 `PartyState` 作为明确方向。
- 当前没有独立的 faith 文档，也没有成型的神灵状态、供奉扣费、升阶判定、指引门槛或信仰奖励。

## 目标与非目标

### 目标

- 让角色能在对应神殿通过真实扣金完成信仰累计和升阶。
- 让“特殊成就 -> 神灵指引 -> 再次供奉升阶”成为正式成长闭环。
- 保持信仰奖励继续走当前正式的 `PendingCharacterReward` 队列，不新增第二套奖励主链。
- 让信仰阶位既能作为剧情/职业门槛，又能附带永久属性、知识、技能奖励。
- 让主角通过多神堆叠形成明确的后期超规格成长曲线，而普通角色仍停留在单神专精框架内。

### 非目标

- 不做完整的仪式任务系统。
- 不做改信、弃信、神罚、信仰衰减、时间衰减。
- 不做一套独立的 faith UI modal 或玩家专用神殿界面。
- 不在首版一次性补全所有神灵和所有信仰据点。
- 不把信仰状态写在 `world_data` 或窗口脚本里。
- 不追求所有角色最终强度接近；faith 的目的就是制造主角与其他角色之间可预期且显著的后期差距。

## 核心设计结论

### 一、信仰归属是角色成长，不是队伍共享状态

- 信仰真源固定放在 `UnitProgress` 下。
- 队伍只持有公共资源和主角身份，不持有队伍共享信仰等级。
- 这样能保持：
  - 信仰随角色存档 round-trip
  - 角色切换、晋升、奖励入账时 ownership 清晰
  - 非主角单神限制可直接在角色层裁决

### 二、主角与普通角色采用不同的多神规则

- `PartyState` 新增 `main_character_member_id`。
- 新游戏默认把开局角色 `player_sword_01` 写入 `main_character_member_id`。
- 判定规则固定为：
  - 若 `member_id == main_character_member_id`，允许同时拥有多个 `deity_id` 的 faith progress
  - 其他角色如果已经存在任意非空神灵进度，则拒绝对其他神灵继续供奉
- 首版不做改信入口；普通角色一旦开始某位神灵的信仰，就视为锁定。
- 这条规则不是 flavor，而是数值主设计：
  - 普通角色的战力上限按“单神毕业”估算
  - 主角的战力上限按“多神毕业叠加”估算
  - 主角最终强度显著高于其他玩家培养角色是预期行为，不视为平衡缺陷

### 三、每位神灵独立累计金币，阶位只按该神灵自己的阈值推进

- 每次供奉都扣除 `PartyState.gold`。
- 每个 `deity_id` 单独维护：
  - 当前阶位
  - 累计供奉金币
- 阶位不共享捐献额度，不在不同神灵间互相转换。
- 同一神灵的供奉不会因为暂时缺少指引而丢失；金币照常累计，只是不能升到下一阶。

### 四、“仪式”不建成独立服务，改为特殊成就奖励出的神灵指引

- 你要求的“人物特殊成就完成后，会告诉你获得了神灵的指引，接下来去神殿供奉即可升级”，正式落点定义为：
  - 特殊成就解锁时，通过现有成就系统奖励一个 `knowledge_unlock`
  - 这个知识 ID 就是某位神灵某一阶的 `guidance_knowledge_id`
- 神殿供奉动作只检查三个条件：
  - 供奉金币累计是否达到阈值
  - 对应 `guidance_knowledge_id` 是否已经拥有
  - 人物成长里程碑是否已经达到
- 这样仪式来源继续复用现有 achievement 管线，不新增第二套 ritual runtime。

### 五、升阶奖励继续走统一角色奖励队列

- 信仰升阶后不直接在神殿点击那一刻把所有收益硬写进角色。
- 正式行为为：
  1. `FaithService` 判定升阶成功
  2. 构造一条 `PendingCharacterReward`
  3. 交给 `CharacterManagementModule.enqueue_pending_character_rewards(...)`
  4. 由世界地图统一奖励弹窗确认后正式入账
- 奖励条目只使用已有正式类型：
  - `knowledge_unlock`
  - `skill_unlock`
  - `attribute_delta`
- 不新增专用 `faith_reward` 类型。

### 六、信仰门槛需要同步进现有 reputation 规则表面

- 虽然信仰真源不放在 `UnitReputationState`，但升阶后要把当前结果镜像进去。
- 推荐键名固定为：
  - `faith_rank_<deity_id>`
- 例如：
  - `faith_rank_charred_cathedral_patron = 2`
- 这样现有 `ReputationRequirement` 和职业/剧情门槛系统可以直接复用，无需额外改一套条件模型。

### 七、强力奖励必须对应高强度解锁门槛

- faith 不是新手线奖励，而是中后期高价值成长支线。
- 设计原则固定为：
  - `浅信徒` 允许只靠供奉解锁，满足最初的入教直觉
  - 从第 2 阶开始，不能只靠刷钱或只靠刷成就单独突破
  - 每一阶都要同时满足：
    - 更高累计供奉金额
    - 对应神灵的高难特殊成就所给出的指引知识
    - 明确的人物成长里程碑
- v1 的人物成长里程碑首选 `required_character_level`，因为当前仓库已经有稳定持久化字段，且不会把 faith 强行绑定到单一职业。
- deity-specific achievement 的阈值不走“教程级”数值；第 2 阶以后默认按中后期数值带设计。

### 八、主角后期强度目标明确设为普通培养角色的 3-4 倍

- faith 系统承担主角唯一性，因此数值目标不能只做“略强一点”。
- 目标口径固定为：
  - 同样投入培养资源的普通角色，在单神毕业后应处于“高强专精单位”水平
  - 主角在多神毕业后，应达到普通培养角色约 `3-4 倍` 的总战力上限
- 这里的“3-4 倍”按总战力理解，不要求所有单项面板都直接翻 3-4 倍：
  - 允许一部分来自基础属性和派生属性
  - 允许一部分来自多神知识/技能叠加
  - 允许一部分来自多体系覆盖、行动效率和抗性完整度
- 设计上不建议把 3-4 倍全部压成单一属性膨胀；更合理的实现是：
  - 更高的基础面板
  - 更完整的技能覆盖
  - 更高的抗性和资源上限
  - 通过多神叠加形成“既硬、又全、又强”的后期主角模板
- 因此普通角色的单神奖励设计不应以“主角也只能拿一份”为前提，而应以“主角未来会拿很多份”为前提预留上限。

## 数据模型与持久化

### `PartyState`

- 新增字段：
  - `gold: int`
  - `main_character_member_id: StringName`
- `version` 从 `2` 升到 `3`
- 旧存档兼容：
  - 缺少 `gold` 时默认 `0`
  - 缺少 `main_character_member_id` 时默认回退到 `leader_member_id`，若仍为空则取第一个有效成员

### `UnitProgress`

- 新增字段：
  - `faith_state: UnitFaithState`
- `version` 从 `1` 升到 `2`
- 旧存档兼容：
  - 缺少 `faith_state` 时默认空状态

### `UnitFaithState`

- 建议新增文件：`scripts/player/progression/unit_faith_state.gd`
- 负责维护角色拥有的全部神灵进度。
- 最小字段：
  - `devotions: Dictionary`
- 其中 key 为 `deity_id`，value 为 `FaithDevotionState`

### `FaithDevotionState`

- 建议新增文件：`scripts/player/progression/faith_devotion_state.gd`
- 最小字段：
  - `deity_id: StringName`
  - `current_rank_index: int`
  - `total_donated_gold: int`
- 说明：
  - `current_rank_index = 0` 表示尚未成为任何正式信徒
  - 阶位名不直接写死在状态里，而由内容定义解释

### `FaithDeityDef`

- 建议新增文件：`scripts/player/progression/faith_deity_def.gd`
- 最小字段：
  - `deity_id: StringName`
  - `display_name: String`
  - `facility_id: StringName`
  - `service_type_label: String`
  - `power_domain_tags: Array[StringName]`
  - `rank_defs: Array[FaithRankDef]`

### `FaithRankDef`

- 建议新增文件：`scripts/player/progression/faith_rank_def.gd`
- 最小字段：
  - `rank_index: int`
  - `rank_name: String`
  - `required_total_donated_gold: int`
  - `guidance_knowledge_id: StringName`
  - `required_character_level: int`
  - `reward_entries: Array[Dictionary]`
- 规则：
  - `rank_index = 1` 对应 `浅信徒`
  - 首版单神总阶位数固定为 `5`
  - `rank_index >= 2` 时，`required_character_level` 必须是正数
  - 后续每一阶都必须带有效 `guidance_knowledge_id`
  - 奖励条目 shape 直接对齐 `PendingCharacterRewardEntry.from_dict()`
  - 后续补多神内容时，每个 deity 的奖励方向必须有明确 domain，避免不同神灵只是重复堆同一条属性

## 内容注册与真相源

### Progression 内容注册表

- `ProgressionContentRegistry` 新增：
  - `_faith_deity_defs: Dictionary`
  - `get_faith_deity_defs() -> Dictionary`
- 首版继续沿用代码注册模式，不额外引入新的 `.tres` 扫描器。

### GameSession

- `GameSession` 新增缓存与只读接口：
  - `_faith_deity_defs`
  - `get_faith_deity_defs() -> Dictionary`
- 与 `skill_defs`、`profession_defs`、`achievement_defs` 的访问方式保持一致。

### 据点配置

- `FacilityNpcConfig` 新增：
  - `deity_id: StringName`
- `WorldMapSpawnSystem._collect_services()` 需要把 `deity_id` 透传到 `available_services` payload。
- 首版只让示例神殿 NPC 使用：
  - `interaction_script_id = "service_faith"`

## 运行链路

### 服务入口

- 正式命令入口保持不变：
  - `GameRuntimeFacade.command_execute_settlement_action(action_id, payload)`
- 但对 `interaction_script_id == "service_faith"` 走新的 `FaithService`。

### `FaithService`

- 建议新增文件：`scripts/systems/faith_service.gd`
- 只承载信仰规则，不承载 modal、状态文本和 Godot 场景接线。
- 输入：
  - `party_state`
  - `member_state`
  - `faith_deity_defs`
  - `action payload`
- 输出统一为：
  - `success: bool`
  - `message: String`
  - `gold_spent: int`
  - `rank_up: bool`
  - `pending_character_rewards: Array`
  - `updated_faith_summary: Dictionary`

### 执行顺序

1. `GameRuntimeFacade` 解析当前服务 payload，拿到 `member_id`、`deity_id`
2. 检查是否允许该角色对这位神灵供奉
3. 检查 `PartyState.gold` 是否足够
4. 扣金并增加该 deity 的 `total_donated_gold`
5. 按当前阶位只检查“下一阶”是否可升
6. 若下一阶满足：
   - 提升 `current_rank_index`
   - 同步 `reputation_state.custom_states["faith_rank_<deity_id>"]`
   - 构造并入队对应 `PendingCharacterReward`
7. 记录 `settlement_action_completed`
8. 持久化 `party_state`

### 升阶判定规则

- 一次供奉动作最多提升 1 阶。
- 判定条件固定为：
  - `total_donated_gold >= required_total_donated_gold`
  - `guidance_knowledge_id == ""` 或角色已拥有该知识
  - `current_character_level >= required_character_level`
- 如果金币阈值达到但指引知识未满足：
  - 本次供奉成功
  - 金币照常扣除
  - 阶位不提升
  - 反馈文案明确告诉玩家“尚缺神灵指引”
- 如果金币和指引都满足，但人物成长里程碑未满足：
  - 本次供奉成功
  - 金币照常扣除
  - 阶位不提升
  - 反馈文案明确告诉玩家“信仰尚未承载这份神恩”

## 奖励模型

### 正式奖励类型

- `knowledge_unlock`
- `skill_unlock`
- `attribute_delta`

### 入账顺序

- 继续复用当前 `CharacterManagementModule` 的稳定顺序：
  1. `knowledge_unlock`
  2. `skill_unlock`
  3. `skill_mastery`
  4. `attribute_delta`
- faith 奖励本身不需要新增排序规则。

### 属性奖励边界

- faith 的 `attribute_delta` 继续作用于角色永久基础属性或可持久化基础属性键。
- 若奖励目标是派生属性，例如 `hp_max`，仍通过现有 `AttributeService.apply_permanent_attribute_change(...)` 落到 `UnitBaseAttributes.custom_stats`。

### 数值设计边界

- 普通角色只允许拿到 1 条 deity 线的完整奖励，因此单神奖励可以设计得非常强，但仍应保持“专精型强者”口径。
- 主角会横向叠多个 deity，因此多神奖励设计必须遵守 domain 分工：
  - 某些神偏生存
  - 某些神偏雷霆/神术输出
  - 某些神偏资源、抗性、行动效率
- 通过 domain 分工，主角在后期获得的是体系叠加后的 3-4 倍总战力，而不是单一数值粗暴爆表。
- 如果后续实装发现主角只是“比别人强 30%-50%”，就说明 faith 设计目标没有达成，应继续加大多神叠加收益。

## 示例神灵最小切片

### 目标据点

- 世界预设：`ashen_intersection`
- 据点：`charred_cathedral`
- 服务 NPC：当前 `炭化主教母`
- 正式 action：
  - `service:faith`
- `interaction_script_id`：
  - `service_faith`

### 示例神灵

- `deity_id`：`charred_cathedral_patron`
- 显示名：`炭化圣堂之主`
- 说明：
  - 这是首版占位名，后续若剧情命名确定，只改内容定义，不改系统结构。

### 示例阶位

1. `浅信徒`
   - `rank_index = 1`
   - `required_total_donated_gold = 500`
   - `guidance_knowledge_id = ""`
   - `required_character_level = 0`
   - 奖励：
     - `knowledge_unlock charred_prayer_notes`

2. `真信徒`
   - `rank_index = 2`
   - `required_total_donated_gold = 2000`
   - `guidance_knowledge_id = faith_guidance_charred_true`
   - `required_character_level = 8`
   - 奖励：
     - `skill_unlock mage_thunder_lance`
     - `attribute_delta willpower +1`

3. `虔诚信徒`
   - `rank_index = 3`
   - `required_total_donated_gold = 4500`
   - `guidance_knowledge_id = faith_guidance_charred_devout`
   - `required_character_level = 14`
   - 奖励：
     - `knowledge_unlock charred_cathedral_litany`
     - `attribute_delta mp_max +4`

4. `至诚信徒`
   - `rank_index = 4`
   - `required_total_donated_gold = 8000`
   - `guidance_knowledge_id = faith_guidance_charred_exalted`
   - `required_character_level = 20`
   - 奖励：
     - `skill_unlock mage_chain_lightning`
     - `attribute_delta intelligence +1`

5. `神眷者`
   - `rank_index = 5`
   - `required_total_donated_gold = 14000`
   - `guidance_knowledge_id = faith_guidance_charred_blessed`
   - `required_character_level = 28`
   - 奖励：
     - `knowledge_unlock charred_sacred_covenant`
     - `attribute_delta hp_max +6`

### 示例技能说明

- 当前仓库没有完整的 priest/paladin 技能池数据，因此首版示例继续复用现有技能库中的 `mage_thunder_lance` 作为“雷枪”占位。
- 后续如果补了正式圣职技能，只需要把该奖励的 `target_id` 换成新的 skill id。

### 示例指引成就

- 首版继续复用当前成就系统，不新增事件类型。
- 由于正式信仰层级固定为 5 层，首版示例神灵建议补 4 条指引成就，分别对应第 2 到第 5 阶：
  - `charred_guidance_true`
    - `event_type = battle_won`
    - `threshold = 8`
    - 奖励：`knowledge_unlock faith_guidance_charred_true`
  - `charred_guidance_devout`
    - `event_type = enemy_defeated`
    - `threshold = 40`
    - 奖励：`knowledge_unlock faith_guidance_charred_devout`
  - `charred_guidance_exalted`
    - `event_type = profession_promoted`
    - `threshold = 3`
    - 奖励：`knowledge_unlock faith_guidance_charred_exalted`
  - `charred_guidance_blessed`
    - `event_type = skill_mastery_gained`
    - `subject_id = mage_thunder_lance`
    - `threshold = 120`
    - 奖励：`knowledge_unlock faith_guidance_charred_blessed`

## UI 与快照表面

### SettlementWindow

- 首版不新增独立信仰窗口。
- 继续使用现有服务按钮。
- `service_faith` 点击后反馈文本至少覆盖：
  - 供奉成功但未升阶
  - 已达到金币阈值但缺少指引
  - 已达到金币阈值且已有指引，但人物成长强度仍不足
  - 升阶成功，奖励已入队
  - 金币不足
  - 非主角改投其他神失败

### PartyManagementWindow

- 成员详情建议新增轻量信仰摘要：
  - 主信仰/多神列表
  - 各神当前阶位
  - 当前累计供奉金币
- 主角详情额外建议显示：
  - 已完成神灵数量
  - 当前多神叠加进度
- 不做单独信仰面板；先放在角色详情文本区即可。

### Headless Snapshot

- `party`
  - 新增 `gold`
  - 新增 `main_character_member_id`
  - `members[]` 新增 `faith_entries`
- `settlement.services[]`
  - 新增 `deity_id`
- 文本快照至少输出：
  - 当前金币
  - 角色 faith 摘要
  - 可供奉服务对应的 deity

## 测试计划

### progression / serialization

- `PartyState.gold` 与 `main_character_member_id` round-trip 正常
- `UnitProgress.faith_state` round-trip 正常
- 旧存档缺字段时默认恢复成功
- 主角可同时对两个 deity 建立进度
- 非主角对第二个 deity 供奉失败
- 升阶时 `faith_rank_<deity_id>` 会同步进 `reputation_state`
- faith 奖励会按正式队列进入 `pending_character_rewards`
- 金币和指引都满足但角色等级不足时，供奉成功但不会升阶
- 主角的多神叠加状态与普通角色的单神状态在序列化后都保持稳定

### text runtime / headless

- `game new ashen_intersection` 后可见 `service_faith`
- 供奉后 `party.gold` 正确减少
- 达到首阶阈值后角色成为 `浅信徒`
- 缺少指引时达到金币阈值也不会继续升阶
- 缺少成长里程碑时达到金币阈值且已有指引也不会继续升阶
- 获得指引后再次供奉可升到下一阶
- 升阶后的奖励需要 `reward confirm` 后才真正入账

### UI / runtime

- 奖励弹窗在已有 modal 打开时继续排队，不抢占当前窗口
- 关闭当前 modal 后，faith 奖励仍能按现有链路弹出
- 供奉失败不会写脏 `party_state`

## Public Interfaces

- `PartyState`
  - `gold: int`
  - `main_character_member_id: StringName`
- `UnitProgress`
  - `faith_state: UnitFaithState`
- `ProgressionContentRegistry`
  - `get_faith_deity_defs() -> Dictionary`
- `GameSession`
  - `get_faith_deity_defs() -> Dictionary`
- `FacilityNpcConfig`
  - `deity_id: StringName`
- 新增系统服务：
  - `FaithService.execute_devotion(...) -> Dictionary`

## 实现顺序建议

1. 先补 `PartyState` 与 `UnitProgress` 的 faith/gold 数据结构和序列化。
2. 再补 `FaithDeityDef`、`FaithRankDef` 与 `ProgressionContentRegistry` 注册入口。
3. 新增 `FaithService`，把 `service_faith` 接进 `GameRuntimeFacade`。
4. 再补示例成就、示例神灵与 `ashen_intersection` 的服务绑定。
5. 最后补 `PartyManagementWindow`、headless snapshot 和专项回归。

## 默认假设

- 固定主角就是开局成员 `player_sword_01`，而不是当前队长。
- 首版不做改信或清空信仰进度。
- 首版只做 1 个示例神灵，但所有数据结构都允许后续扩容到多神。
- 后续扩神时，要始终以“主角最终会通过多神堆叠达到普通培养角色 3-4 倍强度”为校准目标。
- faith 文档是当前信仰系统的唯一设计入口；后续相关设计更新统一落在本文档。

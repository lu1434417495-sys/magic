# CU-13 Progression Content Subagent Review - 2026-05-11

## Scope

本轮按 `algorithm-design` 流程，对 CU-13 progression 内容定义、条件模型与 seed/validation 面做并行对抗性分析。四个子任务分别覆盖：

- skill / combat effect 内容 schema 与 runtime fallback。
- profession / reward / quest pending reward 内容。
- identity / race / bloodline / ascension / trait dispatch。
- validation runner / headless validation / fixture 覆盖面。

未改运行时代码，未跑 Godot 测试；本文只沉淀审查意见和建议落地顺序。

## 合并判断

未发现 P0。主要风险是多个内容字段只做“非空 / 类型”检查，但运行时会把未知值 fallback 成宽松语义，例如任意阵营、中心单格、物理挥砍、跳过未知 reward entry。这个模式会让坏 `.tres` 或坏 quest payload 在静态校验时通过，在运行时悄悄变成错误战斗 / 成长结果。

建议优先收束三个边界：

1. 为所有 runtime fallback 字段建立共享白名单，并让 content validation 与 runtime 读取同一份 rules。
2. 让 quest / pending reward / identity payload 在入队或建卡前失败得足够早，不允许未知 entry 或不匹配阶段 id 进入 `PartyState`。
3. 对齐 runtime/headless validation surface 与 `tests/runtime/validation/content_validation_runner.gd`，避免不同入口对同一内容给出不同健康判断。

## P1 Findings

### Skill passive effect defs 未进入静态校验

`scripts/player/progression/skill_content_registry.gd` 当前校验主动 `effect_defs` 和 `cast_variants[*].effect_defs`，但没有覆盖 `combat_profile.passive_effect_defs`。这些被动效果会由 `SkillPassiveResolver` / `BattleDamageResolver` 读取。

失败模式：`warrior_last_stand` 这类救命被动若拼错 `trigger_condition`、`effect_type`、`status_id` 或 save/duration 字段，静态校验不拦，运行时可能静默不触发或错误触发。

建议：把 passive effect defs 复用同一套 `CombatEffectDef` schema validator，并补非法 passive effect fixture。

### target_team_filter / effect_target_team_filter 缺白名单

`SkillContentRegistry` 只检查 `target_team_filter` 非空；`CombatEffectDef.effect_target_team_filter` 没有 schema 校验。runtime 对未知 filter 走默认放行，等同 `any`。

失败模式：`enemy` 拼成 `enmey` 的技能通过校验，并可能允许选中或影响任意阵营。

建议：将 skill 级和 effect 级 team filter 纳入共享枚举；runtime 对未知值应拒绝或产生明确错误，而不是放宽为任意目标。

### damage effect 不要求合法 damage_tag

`SkillContentRegistry` 对 `damage` effect 不要求 `damage_tag`，也没有复用 `ProgressionContentRegistry.VALID_DAMAGE_TAGS`。runtime 对空 tag fallback 到 `physical_slash`。

失败模式：新法术漏写 tag 会变成物理挥砍，错误吃物理减伤 / guard；拼错 tag 则无法命中对应抗性或状态减伤。

建议：damage effect 必须提供合法 tag；如果要允许扩展新 tag，应先抽出共享 `DamageTagContentRules`，由 skill、resistance 与 damage resolver 共用。

### PendingCharacterReward entry_type 未白名单且可静默丢奖励

`QuestDef.validate_schema()` 与 CMM 的 `_normalize_pending_character_entry()` 只要求 `entry_type` 非空。`apply_pending_character_reward()` 对未知 entry type 默认 `continue`，最后仍移除整条 pending reward。

失败模式：quest 写入 `skill_level` 或未知 entry type 后可以入队，确认奖励时无成长变化，reward 消失。更危险的是 `content_validation_runner.gd` 把 `skill_level` 当成需要校验技能引用的 quest reward entry type，但 CMM apply 端并不支持。

建议：确认 `skill_level` 是否是正式 entry type。若是，补 apply 语义；若不是，从 validation runner 移除并在 quest/CMM 入口拒绝。无论哪种，entry type 白名单必须由 validation 与 apply 共享。

### Quest 跨表引用未进入 headless/runtime validation snapshot

`GameSession` validation domains 目前汇总 `progression/item/recipe/enemy/world`，而 `ProgressionContentRegistry` 对 quest 只调用 `QuestDef.validate_schema()`。provider、item、skill、enemy 等跨表引用主要由测试 helper `validate_quest_entries()` 检查。

失败模式：quest 奖励物品、击杀敌人、pending skill reward、provider typo 在 headless `validation.ok` 中可能误判为 true，只有 resource validation runner 会抓到。

建议：让 quest cross-reference validation 迁入正式 content registry 或正式 validation service，再由测试 helper 和 headless snapshot 共用。

### 建卡 / 存档 identity payload 不校验阶段组合关系

`CharacterCreationService` 与 `GameSession` 解码会归一 identity 字段类型，但没有校验 `ascension_id + ascension_stage_id`、`bloodline_id + bloodline_stage_id` 成对关系。后续 body size refresh 和 racial grant 会直接读取 stage id。

失败模式：payload 可以传 human/common_human，同时填 `ascension_stage_id = titan_avatar`、`ascension_id = ""`，从而得到 large 体型或 titan ascension skill。

建议：建立正式 identity payload validator，至少校验 race/subrace、bloodline/stage、ascension/stage、body_size_category/body_size 的组合一致性。建卡 UI 生成的 payload 也应走同一入口。

### 坏 race/subrace 双向关系仍可进入建卡候选

建卡 UI 在 `RaceDef.subrace_ids` 没有可用项时，会 fallback 扫所有 `SubraceDef.parent_race_id == race_id` 的亚种；但 Phase 2 已把“parent race 未列出该 subrace”定义为错误。当前内容校验只上报，不阻断建卡。

失败模式：错误 race/subrace 关系可以继续进入建卡池，后续成为正式 `PartyMemberState`。

建议：建卡候选只信任 Phase 2 校验通过的 bundle；或者在内容校验非 ok 时禁止新开档 / 禁止进入建卡。

### validation helper 与 runtime registry 不等价

`content_validation_runner.gd` 的 item validation helper 创建 `ItemContentRegistry` 后清空 `_item_defs/_validation_errors`，但不重新扫描 `items_templates`，且保留了构造期的官方 template cache。

失败模式：官方 item template 缺 id、重复 id、继承环等错误可能被 helper 清掉；fixture 又可能借用官方模板，隔离性不纯。

建议：validation helper 应明确选择“完全 runtime 等价”或“完全 fixture 隔离”。若是前者，不要清掉正式模板校验；若是后者，显式注入 fixture template set 并清掉官方 cache。

## P2 Findings

### 其他技能枚举只做弱校验

`target_mode`、`target_selection_mode`、`selection_order_mode`、`area_pattern`、`cast_variant.target_mode`、`footprint_pattern`、`min_skill_level` 多数只做非空或正数检查。

运行时风险包括未知 `area_pattern` fallback 成中心单格，未知 `selection_order_mode` 影响 manual 顺序，错拼 `cast_variant.target_mode` 导致 variant 解析不到。建议用共享 content rules 白名单收束。

### Profession gate 未校验 rank 可达性

`ProfessionContentRegistry` 校验 profession gate 引用存在、`min_rank > 0` 与 `check_mode` 合法，但不检查 `min_rank <= referenced.max_rank`，也不检查自引用 gate 是否会导致永久不可达。

建议：补 gate 可达性检查。若允许自引用，需要明确只能引用已达到的历史 rank，避免 unlock 或 target rank 自锁。

### Pending reward schema 不拒绝额外字段

`PendingCharacterReward.from_dict()` 与 `PendingCharacterRewardEntry.from_dict()` 只检查必填字段，不拒绝 extra fields。这与当前严格 schema 口径不一致，也可能让旧 payload 或错误 payload 混入正式队列。

建议：按兼容性政策先确认是否需要旧 payload 支持；若不保兼容，应拒绝额外字段并补 schema regression。

### trait dispatch 有两份真相

内容 Phase 2 读取 `TraitTriggerContentRules`，runtime 实际由 `TraitTriggerHooks._DISPATCH` 决定触发。当前部分 `.tres trigger_type` 是 `passive`，runtime 仍按 trait id 触发。

失败模式：作者以为修改 `.tres trigger_type` 能改变行为，实际不会；或只更新 content rules 未更新 runtime dispatch，validation 可过但战斗无触发。

建议：明确 `RaceTraitDef.trigger_type` 是展示字段还是 runtime contract。若是 contract，content rules 与 runtime dispatch 应同源生成或至少有一致性测试。

### RacialGrantedSkill level 约束不足

`minimum_skill_level` 只校验 int，runtime 只拒绝 `< 0`，所以 `0` 可通过并写成 learned level 0；也未校验不超过 `SkillDef.max_level`。`grant_level` 被校验但授予路径实际不用。

建议：`minimum_skill_level` 必须为正且不超过目标 skill max level；同时清理或落地 `grant_level` 的正式语义。

### level description 通用校验不足

`level_description_template/configs` 没有进入通用 skill static validation，目前主要由 mage alignment runner 覆盖 mage 技能。

失败模式：warrior/archer 或非 mage 技能丢描述模板配置时 resource validation 仍 pass，UI formatter 可能返回空描述。

建议：把描述模板最小 schema 纳入 `SkillContentRegistry.validate()`，而不是依赖职业专用 runner。

### enemy seed completeness 与 domain 断言缺口

enemy 官方 validation 主要走 `enemy_content_seed.tres`；未挂进 seed 的敌人模板 / brain / roster 可能不会被 runner 或 runtime 默认路径看到。invalid fixture 断言也更多检查错误文本 fragment，未稳定检查 `domain` 字段。

建议：明确 enemy 真相源是 seed 还是目录。如果是 seed，补“目录中未入 seed 的资源”检查；resource validation regression 同时断言 domain 与错误 fragment。

## Suggested Landing Order

1. 先做白名单收束：team filter、damage tag、area/selection/variant target、pending reward entry type。
2. 再做 reward/quest/identity 入队前阻断：未知 reward entry 不得入队，坏 identity stage pair 不得开档。
3. 把 quest cross-ref validation 从测试 helper 上移到正式 registry/service，并接入 headless validation snapshot。
4. 补 passive effect validation 与 racial grant level 校验。
5. 最后清理 validation helper 与 fixture 隔离策略，补 domain 稳定断言。

## Test Gaps To Add

- `tests/battle_runtime/skills` 或 `tests/runtime/validation`：非法 `target_team_filter/effect_target_team_filter`、空/非法 `damage_tag`、非法 `area_pattern/selection_order_mode/cast_variant.target_mode`。
- `tests/runtime/validation`：`passive_effect_defs` schema 负向 fixture，非 mage skill description 缺失 fixture。
- `tests/progression/core`：未知 `PendingCharacterRewardEntry.entry_type` 不能 claim 成功并消失；`skill_level` 语义必须与白名单一致。
- `tests/progression/schema`：pending reward / entry extra fields 拒绝。
- `tests/progression/schema`：profession gate `min_rank > referenced.max_rank` 与不可能自引用 gate。
- `tests/progression/identity`：坏 `ascension_stage_id` / `bloodline_stage_id` pair 不得授予技能或体型；坏 race/subrace 双向关系不得出现在建卡候选。
- `tests/battle_runtime/skills`：`TraitTriggerContentRules` 与 `TraitTriggerHooks._DISPATCH` 一致性。
- `tests/runtime/validation`：quest cross-ref 在 headless validation snapshot 中也能失败。
- `tests/runtime/validation`：item template invalid fixture、enemy seed completeness、domain 字段稳定断言。

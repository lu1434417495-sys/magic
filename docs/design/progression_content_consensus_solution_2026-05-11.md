# CU-13 Progression Content Consensus Solution - 2026-05-11

## Problem

`progression_content_subagent_review_2026-05-11.md` 暴露的问题集中在一个模式：内容字段只做非空或类型校验，运行时遇到未知值时再 fallback 成宽松语义。

典型风险包括：

- 技能目标过滤拼错后运行时放行为 `any`。
- damage effect 漏写 `damage_tag` 后被当作默认物理挥砍。
- passive effect 没进入与主动效果同等的静态校验。
- pending reward 未知 `entry_type` 被跳过后整条 reward 被删除。
- quest/provider/item/skill/enemy 跨表错误只被测试 helper 抓到，不进入 headless `validation.ok`。
- identity payload 只归一类型，可能把不匹配的 ascension/bloodline stage 当成真实身份阶段。

本轮对抗性讨论后的共识：CU-13 要把“内容契约”从 runtime fallback 和测试 helper 中抽出来。正式内容、建卡 payload、存档 payload、headless validation 和 battle runtime 都应读同一批规则；坏内容 fail closed，不做隐式修复、不做旧 schema 兼容。

## Current Ownership

- `SkillContentRegistry` 拥有 skill resource 扫描和 skill/combat schema 校验。
- `CombatSkillDef`、`CombatCastVariantDef`、`CombatEffectDef` 是数据载体，不反向 preload battle runtime。
- progression content rules 文件拥有共享枚举和轻量 schema helper。
- `QuestDef.validate_schema()` 只拥有 quest 本表 shape；跨表引用由正式 content cross-reference validator 或 `ProgressionContentRegistry` 聚合层拥有。
- `PendingCharacterReward` / `PendingCharacterRewardEntry` 拥有 pending reward 当前存档 schema。
- `ProfessionContentRegistry` 拥有 profession gate 静态可达性校验。
- `ProgressionContentRegistry` 拥有 identity Phase 2 跨表引用校验。
- `CharacterCreationService` / `GameSession` 拥有 identity payload 落入 `PartyMemberState` 前的入口校验调用。
- `GameSession.get_content_validation_snapshot()` 是官方运行时内容健康度真相源；`content_validation_runner.gd` 是测试/fixture 编排器。

## Core Invariants

- runtime 不能把未知 content enum 自动放宽为合法语义。
- `effect_target_team_filter == ""` 是“继承 skill target filter”的显式语义；其他未知 filter 不合法。
- `damage` effect 必须有合法 `damage_tag`，或显式声明使用武器物理伤害标签且满足武器前置条件。
- `passive_effect_defs` 与主动 `effect_defs`、cast variant `effect_defs` 使用同一套 `CombatEffectDef` schema 校验。
- `PendingCharacterRewardEntry.entry_type` 是闭集。本轮正式支持：`knowledge_unlock`、`skill_unlock`、`skill_mastery`、`attribute_delta`、`attribute_progress`。
- `skill_level` 不是正式 pending reward entry type；本轮拒绝它，不补临时设级语义。
- 未知 pending reward entry 不能 `continue` 后删除整条 reward。
- quest 跨表引用必须进入官方 validation snapshot，不能只在测试 helper 中存在。
- profession gate 必须静态可达：引用存在、rank 可达到、自引用不死锁。
- `race_id + subrace_id`、`bloodline_id + bloodline_stage_id`、`ascension_id + ascension_stage_id` 必须成对合法。
- `body_size` 是从 identity category 派生的缓存，不是 payload 真相源；payload 与派生结果冲突时拒绝。
- `TraitTriggerContentRules` 是 trait trigger 内容契约真相源，`TraitTriggerHooks` 是执行器；二者必须一致。
- official validation 是 runtime 等价；invalid fixture validation 是隔离模式，不能污染或替代官方 registry。

## Disputed Options

### A. 只在 validation runner 补负例

继续让 `content_validation_runner.gd` 捕获 quest、skill、item 等坏 fixture，官方 `GameSession` snapshot 不变。

结论：反对。headless `validation.ok` 会误判官方内容健康，正式运行时仍可能消费坏内容。

### B. 保留 runtime fallback 作为容错

未知 target filter 放行、未知 area 退成中心单格、缺 damage tag 退成 `physical_slash`。

结论：反对。不同 runtime 路径已出现放行/拒绝不一致，且会把内容拼写错误变成错误战斗结果。

### C. 把完整校验下沉到 Resource 类

让 `CombatSkillDef` / `CombatEffectDef` / `QuestDef` 直接查询所有 registry 或 battle runtime。

结论：反对。Resource 应保持数据载体；跨表校验放在 registry 聚合层或正式 validator，避免加载方向反转。

### D. 立即支持 `skill_level` reward entry

把 `skill_level` 定义为设置或增加技能等级。

结论：反对。它会绕过 mastery、动态 max level、资源解锁、成就触发等现有 progression 语义。若以后需要，必须单独设计绝对设级/增级/clamp/事件契约。

### E. 继续修正 identity payload

读到不匹配的体型或 stage 时刷新派生字段并继续。

结论：反对。可以派生 body size，但不能用派生掩盖错误身份组合。身份组合必须先合法，之后才能刷新派生缓存和身份 grant。

## Recommended Design

### 1. Shared combat content rules

- 新增 `scripts/player/progression/combat_skill_content_rules.gd` 或等价共享 rules。
- 把 `VALID_DAMAGE_TAGS` 从单一 registry 常量抽到共享 rules，供 skill validation、identity resistance validation、battle damage resolver 共用。
- 最小白名单：
  - team filter: `self / ally / friendly / enemy / hostile / any`；effect 级允许空值表示继承。
  - target mode: `unit / ground / self`。
  - target selection: `single_unit / multi_unit / random_chain / coord_pair / self`。
  - selection order: `stable / manual`。
  - area pattern: 当前 battle grid 支持的正式范围，如 `single / self / diamond / square / radius / cross / line / cone / narrow_cone / front_arc`。
  - footprint pattern: `single / line2 / square2 / unordered`。
  - damage tag: 现有正式 damage tags，包括物理三系、元素、能量和特殊标签。
- `SkillContentRegistry` 使用共享 rules 校验：
  - `combat_profile.target_mode`
  - `target_team_filter`
  - `target_selection_mode`
  - `selection_order_mode`
  - `area_pattern`
  - level override 中的 `area_pattern`
  - cast variant `target_mode / footprint_pattern / min_skill_level`
  - effect `effect_target_team_filter / damage_tag`
- `passive_effect_defs` 逐项送入同一个 `_append_effect_validation_errors()`，并要求 passive effect 使用正式 `trigger_condition`。
- `min_skill_level` 保持 `0` 代表学会即生效；规则是 `>= 0`，`max_skill_level == -1 || max >= min`，且有限 `SkillDef.max_level` 下不超过最大等级。

### 2. Runtime fail-closed for bad skill content

- battle target filter 读取未知值时返回 false 或稳定错误，不再放行为 `any`。
- 未知 area pattern 不退成中心单格。
- damage resolver 不再把空或未知 damage tag 落到 `physical_slash`；只有显式武器伤害标签来源且满足条件时才使用武器标签。
- runtime 不直接崩溃；命令不可施放、效果不应用，并输出稳定错误便于测试断言。
- `footprint_pattern` 当前未知已会拒绝坐标形状，后续接入共享 rules 以防 validation/runtime 漂移。

### 3. Pending reward and quest contract

- 新增轻量 `PendingCharacterRewardRules`，或在 `PendingCharacterRewardEntry` 暴露 `SUPPORTED_ENTRY_TYPES`。
- `QuestDef._validate_pending_character_reward()`、`PendingCharacterRewardEntry.from_dict()`、CMM normalize/apply 前置检查共用这个 entry type 白名单。
- 本轮将 `skill_level` 从 `QUEST_REWARD_ENTRY_TYPES_REQUIRING_SKILL` 移除，并作为 unsupported entry 拒绝。
- `PendingCharacterReward.from_dict()` 与 `PendingCharacterRewardEntry.from_dict()` 做 exact field check，拒绝 extra fields。
- CMM `apply_pending_character_reward()` 遇到 unsupported entry 时返回失败或保留队列，不删除 pending reward。
- quest materializer 与 pending reward 存档 payload 分开字段表：quest reward entry 可以有 quest 专用输入字段，但生成的 pending reward 必须符合当前 schema。
- provider id 列表抽成共享 content rules，至少覆盖当前正式 provider。
- quest cross-reference validation 上移到正式 registry/service：
  - provider id。
  - submit item objective 的 item。
  - defeat enemy objective 的 enemy template。
  - item reward 的 item。
  - pending reward 中 `skill_unlock / skill_mastery` 的 skill。
- 这些错误并入 `GameSession` official validation snapshot，使 headless `validation.ok=false`。

### 4. Profession gate reachability

- `ProfessionContentRegistry._append_profession_gate_errors()` 增加可达性校验：
  - referenced profession 必须存在。
  - `min_rank > 0`。
  - `min_rank <= referenced_profession.max_rank`。
  - unlock gate 不允许自引用。
  - rank-up 自引用只允许 `min_rank < target_rank`；`min_rank >= target_rank` 是死锁。
- 不在 runtime 中用 fallback 放行不可达 gate；内容错误应在静态校验时暴露。

### 5. Identity payload validator

- 新增 progression 规则层 identity payload validator，读取 `ProgressionContentRegistry` bundle 与 `BodySizeRules`。
- `CharacterCreationService.apply_character_creation_payload_to_member()` 写入前调用 validator。
- `GameSession` 解码 `PartyState` 后、刷新 body size 与 racial grants 前，对所有 members 调同一 validator；失败则拒绝 load/create。
- 校验内容：
  - race 存在。
  - subrace 存在，且 race/subrace 双向引用成立。
  - `bloodline_stage_id` 非空时 `bloodline_id` 非空，stage 属于该 bloodline。
  - `ascension_stage_id` 非空时 `ascension_id` 非空，stage 属于该 ascension。
  - ascension allowed bloodline/subrace 条件满足。
  - `body_size_category/body_size` 与派生结果一致。
  - effective age stage/source 组合合法，按现有 `AgeStageResolver` 规则收敛。
- 建卡候选只从双向合法 race/subrace 图生成；identity content domain 有错误时，不让坏候选进入建卡。

### 6. Trait and racial grant content contract

- `TraitTriggerContentRules` 继续作为 trait trigger 内容契约 owner。
- 行为型 trait 的 `.tres trigger_type` 必须等于正式 dispatch trigger；不能资源写 `passive`，runtime 却按 trait id 触发。
- `TraitTriggerHooks` 与 `TraitTriggerContentRules.DISPATCH_TRIGGER_TYPES` 增加一致性测试或生成式断言。
- `RacialGrantedSkill` level 校验：
  - `minimum_skill_level >= 0`，因为当前技能等级体系允许 0 级。
  - `minimum_skill_level <= SkillDef.max_level`。
  - `grant_level` 已移除，身份授予等级只认 `minimum_skill_level`。
- `ProgressionService.grant_racial_skill()` 也做同样拒绝，避免坏内容绕过 validation。

### 7. Validation surface and fixture isolation

- `GameSession.get_content_validation_snapshot()` 是官方 runtime 内容健康度真相源。
- official snapshot 必须包含 progression、item、recipe、enemy、world 以及正式跨表错误。
- `content_validation_runner.gd` 分成两类明确入口：
  - official runtime validation：复用 runtime registry/snapshot。
  - fixture validation：允许注入坏目录/坏 seed/坏资源，并保证不污染官方 registry。
- item skill book cross-table、quest cross-ref、enemy seed completeness 都属于正式跨表合同，不能只在测试 helper 里存在。
- enemy 官方真相源暂定为 `enemy_content_seed.tres`。官方 validation 检查目录中 brain/template/roster 是否全部被 seed 引用；若允许未发布草稿，必须另行标注目录或命名规则。
- `run_resource_validation_regression.gd` 负例同时断言 `domain`、`label/error_count` 和错误 fragment，避免报错落错 domain 但测试仍绿。
- battle simulation 与 balance runner 不进入本轮 `validation.ok`。

## Minimal Slice

1. 新增 combat skill shared rules，并接入 `SkillContentRegistry`。
2. 校验 `passive_effect_defs`、skill/cast/effect target enums、damage tags 和 level windows。
3. battle runtime 对未知 filter/area/damage tag fail closed。
4. 新增 pending reward entry type rules；拒绝 `skill_level` 和未知 entry；pending reward schema exact fields。
5. quest cross-reference validation 上移到正式 snapshot。
6. profession gate reachability 校验。
7. identity payload validator 接入建卡与 save decode。
8. trait dispatch contract 与 racial grant level 校验。
9. validation runner 拆分 official/fixture 模式，并补 enemy seed completeness 与 domain 断言。

## Files To Change

- `scripts/player/progression/skill_content_registry.gd`
- `scripts/player/progression/combat_skill_content_rules.gd` new, or equivalent shared rules file
- `scripts/player/progression/progression_content_registry.gd`
- `scripts/player/progression/combat_skill_def.gd` only if helper exposure is needed
- `scripts/player/progression/combat_cast_variant_def.gd` only if helper exposure is needed
- `scripts/player/progression/combat_effect_def.gd` only if helper exposure is needed
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/rules/battle_damage_resolver.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/progression/pending_character_reward.gd`
- `scripts/systems/progression/pending_character_reward_entry.gd`
- `scripts/systems/progression/character_management_module.gd`
- `scripts/player/progression/quest_def.gd`
- `scripts/player/progression/profession_content_registry.gd`
- `scripts/systems/progression/character_creation_service.gd`
- `scripts/systems/progression/identity_payload_validator.gd` new, or equivalent rules file
- `scripts/player/progression/trait_trigger_content_rules.gd`
- `scripts/systems/battle/runtime/trait_trigger_hooks.gd`
- `scripts/player/progression/identity_content_registry_base.gd`
- `scripts/systems/progression/progression_service.gd`
- `scripts/systems/persistence/game_session.gd`
- `tests/runtime/validation/content_validation_runner.gd`

## Tests To Add Or Run

- `tests/runtime/validation/run_resource_validation_regression.gd`
  - invalid passive effect `trigger_condition/effect_type/status_id/save/duration_tu`.
  - invalid `target_team_filter/effect_target_team_filter/target_mode/target_selection_mode/selection_order_mode/area_pattern/cast_variant.target_mode/footprint_pattern`.
  - damage effect missing tag, bad tag, bad param tag, weapon-tag-source without weapon condition.
  - `skill_level` pending reward entry rejected.
  - quest bad provider/item/skill/enemy cross-ref makes official snapshot fail.
  - profession `min_rank > max_rank`, unlock self-reference, impossible rank self-reference.
  - racial grant `minimum_skill_level < 0`, `minimum_skill_level=0`, above max, and absence of legacy `grant_level`.
  - enemy seed completeness and stable domain assertions.
- `tests/battle_runtime/skills`
  - unknown target filter does not hit arbitrary units.
  - unknown area pattern does not collapse to center cell.
  - unknown damage tag does not become `physical_slash`.
  - positive protection for `warrior_last_stand`, `archer_shooting_specialization`, dragon breath damage tags, `mage_portal_step` `coord_pair`, and existing footprint variants.
- `tests/progression/core` or `tests/progression/schema`
  - unknown pending reward entry is not claimed and deleted.
  - pending reward extra fields rejected.
  - legal `skill_unlock / skill_mastery / attribute_delta / attribute_progress` still materialize and apply.
- `tests/progression/identity`
  - bad ascension stage pair fails create/load before body size/grant refresh.
  - bad bloodline stage pair fails.
  - one-way race/subrace relationship fails validation and does not appear in candidates.
  - body size category/int mismatch fails.
  - trait trigger contract parity.
- Headless validation regression:
  - `validation.ok` equals `GameSession` official snapshot.
  - fixture validation does not pollute official snapshot.

Do not include battle simulation or balance runners in this slice.

## Deferred / Policy Decisions

- Whether every official content validation error should block app startup, or only block new game / load / content-consuming commands. Current consensus requires fail closed at consuming entry points; full startup hard gate can be decided separately.
- Whether `skill_level` reward should become a formal entry type. If yes, design it as a separate CU-14 progression rule task.
- Whether enemy directories may contain unpublished draft resources outside `enemy_content_seed.tres`. If yes, add an explicit draft convention before enforcing completeness.

## Project Context Units Impact

No context map edit is required until implementation lands.

When implemented, update CU-13 to reflect:

- combat skill content rules are shared by validation and runtime.
- passive effects are validated with the same effect schema as active effects.
- pending reward entry types are a closed current-schema set.
- quest cross-reference validation is part of official runtime snapshot.
- identity payload validation runs before create/load writes identity into `PartyMemberState`.
- validation runner has explicit official vs fixture modes.

If `skill_level` reward is later formalized, update CU-14 alongside CU-13 because it becomes progression rule execution, not just content schema.

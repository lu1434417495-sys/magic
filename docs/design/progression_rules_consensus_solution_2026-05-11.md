# CU-14 Progression Rules Consensus Solution - 2026-05-11

## Problem

`progression_rules_subagent_review_2026-05-11.md` 暴露的问题集中在“规则合同与执行口径漂移”：

- `AttributeService` 普通 AC 实现为 `8 + AGI modifier`，但 CU-14 合同是 `10 + AGI modifier`。
- 职业 rank 1 -> rank 2 可能被当前核心容量自锁：rank 2 需要第二个核心，但 rank 1 不允许预先分配第二个核心。
- promotion submit 失败时，world/battle UI 状态可能被当作成功清掉，battle timeline 还会解冻。
- `ProgressionService.promote_profession()` 失败路径可能留下 rank 0、HP、history、核心分配等半状态。
- formatter 主要合并 effect `params`，而正式规则大量使用 typed effect fields，描述和运行时容易漂移。
- battle-local 换装刷新只处理 HP，没有统一 clamp MP/Aura/stamina/AP/action threshold。

本轮对抗性讨论后的共识：先锁定规则合同，不扩大到 skill merge / dynamic max 的大重构。晋升是事务；属性公式以 CU-14 为准；formatter 只做展示派生但必须读取 typed fields；正式入口 fail closed，不用 UI/runtime 兜底掩盖底层状态污染。

## Current Ownership

- `AttributeService` 拥有属性快照、AC 组件、资源上限与 action threshold 派生。
- `AttributeSourceContext` 拥有属性来源容器，不拥有公式。
- `ProgressionService` 拥有学习、职业晋升、职业技能授予、触发核心锁定、runtime refresh 的规则事务。
- `ProfessionRuleService` 只读判断 profession unlock / rank-up / gate / tag 条件。
- `ProfessionAssignmentService` 拥有核心技能写入与容量守卫。
- `LevelGrowthEvaluationService` 拥有 active trigger 的 ready/clear/lock 状态选择。
- `CharacterManagementModule` 是 PartyMemberState 与 progression services 的应用桥接，负责 delta 构造与 pending reward。
- `PracticeGrowthService` 拥有冥想/修炼轨替换等级公式和旧技能清理，不拥有普通学习前置的绕过权。
- `BattleUnitFactory` / `BattleChangeEquipmentResolver` 拥有 battle-local equipment view 刷新与战斗单位 clamp。
- `SkillLevelDescriptionFormatter` 拥有技能等级描述渲染，不拥有技能内容或战斗结算。
- `GameRuntimeRewardFlowHandler` / `BattleRuntimeModule` 拥有 promotion prompt/modal/timeline 编排，不拥有 progression 规则。

## Core Invariants

- 普通 AC 是 `10 + AGI/DEX modifier`，再叠加 AC 组件；`armor_max_dex_bonus` 只限制正向敏捷加值。
- `armor_max_dex_bonus = -1` 表示不限制；多个非负 cap 取最小；负敏捷惩罚不被 cap 抹掉。
- 职业晋升是原子事务：rank、profession progress、核心分配、HP、授予技能、promotion history、trigger lock、runtime refresh 要么全成功，要么完全不变。
- rank-up 可在 promotion transaction 内按 `target_rank` 临时纳入 ready active trigger core；普通核心分配仍按当前 rank / 当前 character level 容量。
- active trigger 必须被本次 promotion selection 覆盖，且只在真实晋升成功后锁定/清空。
- `active_level_trigger_core_skill_id` 只能指向已学习、核心、未锁定、达到有效上限的技能。
- world/battle promotion prompt 是阻塞状态。提交失败不能清 prompt/modal，battle 不能解冻 timeline。
- 修炼替换最终仍是学习新技能，必须通过正式学习前置，不得直接 new learned `UnitSkillProgress`。
- battle-local equipment view 是换装后属性真相源；换装 refresh 只 clamp 当前资源，不 refill。
- formatter 不依赖 battle runtime，但必须从 typed effect fields 派生最小展示字段。
- text/headless snapshot 应让 progression 关键状态可观察，不新增第二套规则判断。

## Disputed Options

### A. 保留 AC 8 并修改文档

把当前实现视为平衡结果。

结论：反对。CU-14、CMM attribute snapshot、BattleHitResolver 消费的展示用 `armor_class` 已围绕普通 AC 10 收束；AC 8 是实现漂移。

### B. 普通 rank-up 继续只看当前 core_skill_ids

让玩家先通过普通核心分配凑够 rank 2 条件。

结论：反对。rank 1 容量阻止提前塞第二个核心，正式职业表可能自锁。

### C. target-rank 容量开放给普通核心分配

把 `can_promote_non_core_to_core()` 也改成可按下一级 rank 容量预分配。

结论：反对。只有有效 promotion transaction 能预提交触发核心；普通分配不能提前扩容。

### D. UI/runtime 层保留 prompt 即可

底层 promotion 失败仍可能写半状态，只在外层不清 modal。

结论：反对。`ProgressionService.promote_profession()` 自身必须原子，否则任何调用方都可能污染状态。

### E. formatter 继续靠手写 config

内容作者在 `level_description_configs` 手写所有 typed fields。

结论：反对。这会让 typed field 改动后描述静默过期。formatter 应派生最小显示字段，手写 config 只做覆盖和文案补充。

### F. battle-local 换装复用入场初始化

直接调用完整 unit 初始化以同步所有资源。

结论：反对。入场初始化可能把当前资源补满或覆盖 AP。换装要专用 refresh + clamp。

## Recommended Design

### 1. AttributeService AC contract

- `AttributeService.BASE_ARMOR_CLASS` 改为 `10`。
- 保持现有 AC 组件模型：
  - `armor_class` 对外仍是单一展示/命中消费值。
  - `armor_ac_bonus / shield_ac_bonus / dodge_bonus / deflection_bonus` 是内部组件。
  - `armor_max_dex_bonus` 只限制正向敏捷加值。
- `_resolve_armor_max_dex_bonus()` 继续采用最小非负 cap；`-1` 和负值表示不限制。
- 不调整属性来源顺序；当前来源拓扑与 CU-14 基本一致。

### 2. Promotion transaction and rank-up capacity

- `ProgressionService.promote_profession()` 先完整 resolve/validate selection，再写状态。
- 创建 rank 0 profession progress 必须延后到所有校验通过后。
- 给 promotion 增加局部快照/回滚：
  - profession progress。
  - involved skill progress。
  - `hp_max`。
  - `active_level_trigger_core_skill_id`。
  - locked trigger list。
  - promotion history。
- rank-up preview 使用：当前 assigned cores + ready active trigger core。
- `ProfessionRuleService` 增加只读 preview candidate set，用于判断 rank-up requirement；它仍不写状态。
- `ProfessionAssignmentService` 增加 promotion 专用 assignment/capacity override，只允许本次 selection 中的合法 trigger core 写入。
- 成功顺序：
  - validate promotion selection。
  - assign new core if needed。
  - write rank。
  - apply HP gain。
  - grant profession skills。
  - append promotion history。
  - lock/clear active trigger。
  - refresh runtime state。
- 任一步失败恢复快照并返回 false。

### 3. Active trigger and growth hygiene

- `refresh_runtime_state()` 增加 active trigger sanitize：
  - skill missing。
  - not learned。
  - not core。
  - already locked。
  - not at effective max level。
  - any of the above clears `active_level_trigger_core_skill_id` and related active marker.
- active trigger 晋升属性成长与普通核心满级 pending reward 共用 attribute growth helper。
- helper 只接受当前 schema 中合法的 `attribute_growth_progress` key/value。
- 只有实际应用至少一项 growth 后才设置 `core_max_growth_claimed`。
- 全无效 growth 不消耗一次性领取标记。

### 4. Practice replacement learning gate

- `PracticeGrowthService` 保留：
  - track 检查。
  - replacement level formula。
  - old skill cleanup helper。
- 新技能落地前必须复用 `ProgressionService` 的正式学习前置，或由 CMM 调用等价 `can_learn_skill` / prevalidated callback。
- 替换失败时旧技能不移除，技能书或外部消耗不扣。
- 不在本轮重构 `SkillMergeService` 或 `SkillEffectiveMaxLevelRules`；只补保护性回归确保行为不变。

### 5. Promotion prompt/modal result semantics

- `CharacterProgressionDelta` 或 command result 需要能表达真实 promotion success，例如 `changed_profession_ids` 包含目标职业。
- world submit：
  - 先调用 promotion。
  - 只有实际晋升成功，或成功后生成后续 prompt，才清旧 prompt/modal。
  - 失败保留 prompt/modal，返回 failure status，不持久化成功文案。
- battle submit：
  - 失败 batch 不追加成功 progression delta，不写“完成职业晋升”日志。
  - 保留 `modal_state = "promotion_choice"`。
  - 保持 `timeline.frozen = true`。
  - 成功但仍有后续 prompt 时继续冻结并保留 promotion modal。
- runtime handler 只编排阻塞状态，不把职业规则塞入 UI 或 facade。

### 6. Character creation body size source

- 正式建卡路径必须提供 progression content source 或 identity payload validator。
- `CharacterCreationService.apply_character_creation_payload_to_member()` 无法从内容源派生合法 body size 时返回 false。
- `create_member_from_character_creation_payload()` 不能忽略 apply 失败；失败时返回 null 或明确失败 result。
- 测试需要裸成员时直接构造 state 或提供最小 content bundle，不让正式入口接受 `body_size=99` 这类 payload。

### 7. Battle-local equipment refresh clamp

- 给 `BattleUnitFactory` 或相邻 helper 增加换装专用 refresh：
  - 从 `BattleUnitState.equipment_view` 通过 CMM 重新取 attribute snapshot。
  - 刷新 weapon projection。
  - 刷新 skill projection / unlocked resources。
  - 更新 action threshold 并按 5 TU 粒度归一。
- clamp 当前资源：
  - `current_hp` to `hp_max`。
  - `current_mp` to `mp_max`。
  - `current_aura` to `aura_max`。
  - `current_stamina` to `stamina_max`。
  - `current_ap` 先扣换装 AP，再 clamp 到新 AP 上限。
- 不 refill，不清零 `action_progress`。
- `action_progress` 是否立即触发 ready 交给 timeline driver 下一次推进处理。

### 8. Formatter typed field derivation

- `SkillLevelDescriptionFormatter` 增加描述字段派生层。
- effect 收集口径：
  - root `effect_defs` 按 skill level 过滤。
  - cast variants 必须先按 `cast_variant.min_skill_level` 过滤，再收集其 effects。
  - effects 继续按 `min_skill_level/max_skill_level` 过滤。
- 自动派生最小 display keys：
  - cost/range/area/attack bonus/aura。
  - `power`。
  - `duration_tu`。
  - `status_id` / status power。
  - `damage_tag`。
  - save ability / save partial / save tag。
  - forced move distance。
- `params` 仍可提供 dice 等补充字段；手写 `level_description_configs` 可覆盖自动派生。
- optional block 契约：
  - empty string、null、false、自动 numeric 0 隐藏。
  - 显式字符串 `"0"` / `"+0"` 可显示。
  - 直接 `{attack_roll_bonus}` 仍可替换原值。
- 修正式内容，例如 `mage_chain_lightning` 描述必须同时覆盖敏捷半伤豁免与体质豁免 shocked。

### 9. Headless/text observability

- 结构化 snapshot 继续是 text renderer 的数据源，不新增独立 progression 规则判断。
- 文本层增加每个成员的 progression 关键状态：
  - `resources` / `unlocked_combat_resource_ids`。
  - learned skills with level。
  - active trigger。
  - professions with rank/core/active。
  - locked trigger/core skill ids。
- 目标是让 MP/Aura 解锁、核心技能锁定、职业 rank、active trigger 出错时文本快照可见。

## Minimal Slice

1. AC 常量改为 10，并补 AC component/max dex 回归。
2. promotion transaction：selection 先验、rank0 防污染、快照回滚。
3. rank-up target-rank preview capacity，只在 promotion transaction 内纳入 ready trigger core。
4. world/battle promotion submit 按真实 result 清 prompt/modal/解冻。
5. active trigger sanitize 与 attribute growth helper 共用。
6. Practice replacement 复用正式学习前置。
7. CharacterCreationService 正式路径要求内容源并拒绝坏 body size payload。
8. battle-local equipment refresh 专用 clamp helper。
9. formatter typed field derivation、cast variant min level 过滤、optional 0 契约和 text progression snapshot。

## Files To Change

- `scripts/systems/attributes/attribute_service.gd`
- `scripts/systems/progression/progression_service.gd`
- `scripts/systems/progression/profession_rule_service.gd`
- `scripts/systems/progression/profession_assignment_service.gd`
- `scripts/systems/progression/level_growth_evaluation_service.gd`
- `scripts/systems/progression/character_management_module.gd`
- `scripts/systems/progression/practice_growth_service.gd`
- `scripts/systems/progression/character_creation_service.gd`
- `scripts/systems/progression/skill_level_description_formatter.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/runtime/battle_change_equipment_resolver.gd`
- `scripts/systems/battle/runtime/battle_unit_factory.gd`
- `scripts/systems/game_runtime/game_runtime_reward_flow_handler.gd`
- `scripts/systems/game_runtime/game_runtime_facade.gd` only if command result shape needs exposure
- `scripts/systems/game_runtime/battle_session_facade.gd` only if prompt capture needs result changes
- `scripts/utils/game_runtime_snapshot_builder.gd` or current snapshot builder owner
- `scripts/utils/game_text_snapshot_renderer.gd`
- `data/configs/skills/mage_chain_lightning.tres`

## Tests To Add Or Run

- `tests/progression/core/run_progression_tests.gd`
  - no-armor AC 10 at AGI 10.
  - positive AGI modifier adds to AC.
  - armor max dex `-1/0/3` semantics.
  - negative AGI remains a penalty under cap.
  - invalid promotion selection leaves professions/rank/hp/history/core/trigger unchanged.
  - official warrior/mage rank 1 -> rank 2 with ready second core succeeds.
  - ordinary core assignment cannot pre-fill target rank capacity.
  - active trigger sanitize clears missing/unlearned/non-core/locked/not-max skill.
  - active trigger attribute growth matches ordinary core max reward path.
  - all-invalid growth does not set `core_max_growth_claimed`.
  - practice replacement cannot bypass unmet learn prerequisites.
  - dynamic max and skill merge existing tests still pass.
- `tests/battle_runtime/runtime`
  - battle promotion invalid submit keeps modal frozen and no success log.
  - battle-local equipment change clamps HP/MP/Aura/stamina/AP/action threshold without refill.
- `tests/text_runtime`
  - world promotion invalid submit keeps prompt/modal.
  - text snapshot shows resources, learned skills, active trigger, profession rank/core/locked state.
- `tests/runtime/validation` or `tests/battle_runtime/skills`
  - formatter typed duration/power/save fields affect rendered description.
  - `mage_chain_lightning` description covers both agility damage save and constitution shocked save.
  - optional block hides automatic numeric 0 but displays explicit string.
  - cast variant `min_skill_level` filters description fields.
- `tests/progression/identity`
  - creation without content source rejects inconsistent body size payload.

Do not include battle simulation or balance runners in this slice.

## Deferred / Policy Decisions

- Whether `active_level_trigger_core_skill_id` should be rejected at save decode instead of sanitized at `refresh_runtime_state()`. Current consensus: sanitize runtime state first; strict payload cross-content validation can land with CU-13/CU-11 schema work.
- Whether `SkillLevelDescriptionFormatter` should eventually emit structured description tokens. Current slice keeps string rendering and adds derived fields.
- Whether all official content validation errors should block startup. CU-14 only requires consuming rule entries to fail closed.
- Whether practice replacement should consume items before or after validation. Current consensus: validate first, consume only after successful replacement.

## Project Context Units Impact

No context map edit is required until implementation lands.

When implemented, update CU-14 to reflect:

- base AC is enforced as 10.
- profession rank-up can precommit the ready trigger core only inside promotion transaction.
- promotion submit failure preserves prompt/modal/timeline freeze.
- formatter derives minimum display fields from typed effect fields.
- battle-local equipment refresh clamps all current resources and action threshold without refill.

If identity payload validator from CU-13 is implemented together with CharacterCreationService changes, update CU-13 and CU-14 together.

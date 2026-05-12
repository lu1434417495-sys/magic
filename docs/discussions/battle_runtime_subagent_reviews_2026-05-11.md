# Battle Runtime Subagent Reviews - 2026-05-11

Scope: `scripts/systems/battle/runtime`

Request: use multiple GPT-5.5 xhigh subagents for aggressive architecture and logic review.

Status: all four subagents were closed after their outputs were collected.

Note: this document preserves the subagents' raw findings without de-duplication or severity normalization.

## Lovelace - 019e131e-e4a1-7481-a369-abe5978c0ceb

[high] E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:787 - `random_chain` 没有被强制路由到单位自动选目标分支，而是继续按 `target_mode/target_unit_ids` 判定。正式资源 `warrior_hundred_shadow_final_dance` 是 `target_mode = ground` + `target_selection_mode = random_chain`，选择时会走 ground preview 并要求 1 个地格，导致“无需选目标自动攻击”的技能不可用，或被当成普通地面技能执行。

[high] E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:624 - `random_chain` 在消耗 AP/资源之后才用 `_can_skill_target_unit()` 建候选池，而 `_can_skill_target_unit()` 又检查 `active_unit.current_ap < ap_cost`。当单位正好只有施放所需 AP 时，preview 会放行，issue 会先扣 AP，然后候选池变空，结果是花掉资源、记录行动，但没有任何攻击目标。

[high] E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:671 - random chain 预先展开 `chain_targets` 后循环执行，但循环只跳过 `null`，不跳过已经死亡的目标。同一目标允许多段命中时，第一段击杀后后续段仍可能继续对 dead unit 结算伤害，造成重复击倒日志、重复死亡指标/成就推进，loot 虽有去重但战斗统计会污染。

[medium] E:/game/magic/scripts/systems/battle/runtime/battle_movement_service.gd:178 - 移动 preview/path validation 完全不查询 `BattleLayeredBarrierService`，但执行路径在 E:/game/magic/scripts/systems/battle/runtime/battle_movement_service.gd:276 才解析虹光边界穿越。失败模式：HUD/AI 认为目标格可达并给出 move_cost，issue 时却被屏障伤害、阻挡或传送，preview-first 口径和实际执行分叉。

[medium] E:/game/magic/scripts/systems/battle/runtime/battle_movement_service.gd:270 - 移动执行逐步 `move_unit()`，但只有整条路径成功后才扣移动力、记录行动和统一追加 changed coords。若前几步未跨屏障已移动，后续某一步跨虹光法球被 stopped，函数返回 false，单位会停在半路，但 batch 只记录“落点失效”，不扣 cost、不锁移动，UI/AI 状态和真实 occupancy 会短暂失真。

[medium] E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:427 - 单体技能 preview 只验证资源、目标和射程，不查询 layered barrier；执行在 E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:975 才处理屏障。AI/玩家 preview 会显示“可对目标使用/命中伤害预览”，issue 后却可能只破解或被虹光层阻挡，资源已消耗但目标完全没被影响。

检查过的核心调用链：
`BattleRuntimeModule.preview_command()` -> move/skill preview；`BattleRuntimeModule.issue_command()` -> preview-first gate -> move/skill sidecar；`BattleRuntimeModule.advance()` -> AI decision -> `issue_command()`；`BattleTimelineDriver.advance_timeline()` -> status/terrain tick -> ready queue -> turn activation；`BattleMovementService._resolve_move_path_result()` -> `_move_unit_along_validated_path()`；`BattleSkillExecutionOrchestrator._preview_skill_command()` -> `_handle_skill_command()` -> unit/ground execution -> barrier/damage/chain handling。

残余测试缺口：现有 prismatic tests 主要直测 barrier service，没覆盖 runtime preview-vs-issue；random_chain 只覆盖“有目标且 AP 充足”的 happy path，缺少 formal `warrior_hundred_shadow_final_dance`、AP 恰好等于 cost、无合法目标、首段击杀后重复目标这些回归。未运行测试，按你的要求只读静态审查。

## Herschel - 019e131e-e4ca-7e81-a9e8-8cada17be3fa

[P1] [battle_charge_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:312) - `validate_charge_command()` 为了算 `resolved_anchor_coord` 调 `handle_charge_skill_command()` 做“预览模拟”，但这条路径会走真实副作用；同文件 [149](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:149) 记录冲锋熟练度，path AOE 还会在 [451](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:451) 记录逐目标结果。可复现：玩家用 `charge` 冲 3 格时，执行阶段先验证一次预览锚点、再真正执行一次，最终熟练度按 6 而不是 3 入账；`warrior_whirlwind_slash` 这类 path AOE 还可能在验证阶段消耗真实命中/命运侧副作用。

[P1] [battle_repeat_attack_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd:135) / [battle_charge_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:491) - repeat attack 与 charge/path/collision kill 都只 `clear_defeated_unit()`，没有走 defeated loot 收集和 `_record_unit_defeated`。可复现：让带 `drop_entries` 的敌人死于 `warrior_combo_strike`、`saint_blade_combo`，或 `warrior_whirlwind_slash` 路径伤害，战斗能结束但 `BattleResolutionResult.loot_entries` 少掉该敌人的掉落，单位击败 metrics 也漏记。

[P1] [battle_ground_effect_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_ground_effect_service.gd:314), [battle_charge_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:82), [battle_special_skill_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_special_skill_resolver.gd:260) - 多条位移路径绕过 `BattleLayeredBarrierService.resolve_unit_boundary_crossing()`：ground `blink/jump` 直接 `move_unit_force`，charge 直接 `move_unit`，`doom_shift` 直接清占位交换。可复现：先放 `mage_prismatic_sphere`，再用 `mage_blink`、`charge` 或 `doom_shift` 从球外进球内，单位不会承受虹光层、不会被石化/放逐/阻断，屏障等于被穿墙。

[P2] [battle_shield_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_shield_service.gd:174) + [battle_ground_effect_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_ground_effect_service.gd:705) - shield 掷骰按 effect instance 缓存在整次 ground AoE 循环里，导致同一个 `priest_aid` 范围内所有友军拿到完全相同的 1d8+3 护盾值。CU-16 写的是护盾骰逐次调用 RNG；可复现：固定 RNG 为 1、8、4，三个友军仍都会得到第一次 roll 的护盾。

[P2] [battle_charge_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:737) - forward push 先移动 blocker，再在 [827](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:827) 用 `unit.coord` 和 `target_coord` 计算坠落层数；移动后两者相同，所以永远是 0。可复现：把 blocker 从高度 3 正向顶到高度 1，侧推会有坠落伤害，前推没有任何坠落伤害或日志。

[P2] [battle_ground_effect_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_ground_effect_service.gd:737) - ground unit effects 先结算伤害，再无视目标是否已死亡继续在 [746](E:/game/magic/scripts/systems/battle/runtime/battle_ground_effect_service.gd:746) 应用 forced move/special，死亡清理到 [799](E:/game/magic/scripts/systems/battle/runtime/battle_ground_effect_service.gd:799) 才发生。可复现：`warrior_qi_shockwave` 或 `warrior_tail_sweep` 一击打死目标时，尸体仍会被推动，甚至能触发屏障穿越/占位变化后再被清除。

[P2] [battle_skill_turn_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_turn_resolver.gd:222) - racial skill 同时有 per-battle 和 per-turn charge 时，只扣 per-battle 后立刻返回，不扣 per-turn。可复现：设置 `per_battle_charges[racial_skill_x]=2`、`per_turn_charges[racial_skill_x]=1`，同一回合第一次施放后 per-turn 仍为 1，第二次不会被每回合次数挡住。

[P2] [battle_skill_turn_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_turn_resolver.gd:487) - body size override 到期恢复时不做 `can_place_footprint`，直接清占位、改体型、`set_occupants`。可复现：单位被缩小后其他单位走进原大体型 footprint，占用期满恢复大体型会覆盖对方 occupant，造成两个单位逻辑重叠/占位归属损坏。

**Open Questions / Assumptions**

我按 CU-15/CU-16 的描述假设：blink、charge、swap 只要跨虹光边界，都必须触发同一套 passage 检定；护盾骰应按目标独立掷。若设计想要“范围护盾共享一次 roll”，需要把这个作为显式规则并补测试。

**Residual Risks / Test Gaps**

未运行测试，按你的要求只读审查。当前缺少覆盖：charge/ground relocation/doom_shift 穿越虹光法球、repeat/charge kill 掉落、ground lethal forced move、body size 恢复占位冲突、AoE shield 多目标独立掷骰。

## Kepler - 019e131e-e506-7c33-8136-1931db60df0e

**Findings**

[P1] [battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:93) - `refresh_battle_unit()` rewrites `current_ap` to the refreshed max AP. This is unsafe for mastery回灌: a skill can spend AP, grant mastery, request promotion, then `submit_promotion_choice()` refreshes the battle unit and restores AP mid-action-window. Concrete failure: promotion choice after an in-battle level-up gives the acting unit extra actions it already paid for.

[P1] [passive_status_orchestrator.gd](E:/game/magic/scripts/systems/battle/runtime/passive_status_orchestrator.gd:24) - identity passive reprojection clears trait/tag/resistance fields, but not `per_battle_charges`, `per_turn_charges`, or `per_turn_charge_limits`. `RaceTraitResolver`/`AscensionTraitResolver` then only initialize charges when absent. Concrete failure: if an ascension starts suppressing original race traits, stale `racial_skill_*` charges and per-turn limits survive; next turn `reset_per_turn_charges()` can refill a suppressed race skill and let it fire again.

[P2] [battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:134) - battle-local equipment refresh updates attributes and weapon projection but does not rebuild equipment-gated skill availability. Since `requires_equipped_shield` is only handled by `_filter_skills_by_equipment_requirements()` at lines 357-369, battle equip/unequip can leave shield skills stale. Concrete failure: equipping a shield mid-battle does not surface shield skills; unequipping/replacing the shield can leave shield skills visible/available from the old projection.

[P2] [trait_trigger_hooks.gd](E:/game/magic/scripts/systems/battle/runtime/trait_trigger_hooks.gd:36) - schema-facing dispatch checks now delegate to `TraitTriggerContentRules`, while runtime dispatch still uses the local `_DISPATCH` table. Concrete failure: adding a trait to `TraitTriggerContentRules.DISPATCH_TRIGGER_TYPES` will pass registry validation, but battle runtime silently no-ops unless `_DISPATCH` is also updated.

**Open Questions / Assumptions**

I assumed in-battle promotion choice is reachable from mastery grants, based on the runtime回灌 path and modal handling. I also treated identity changes during battle as possible because the refresh path is explicitly battle-local and progression-aware.

**Residual Risks / Test Gaps**

I did not run Godot tests because you asked for read-only review. The missing coverage I’d want is: promotion-after-skill-use preserves spent AP, suppressed race charges are removed on reprojection, and battle equipment swaps refresh shield-gated skill availability.

## Dalton - 019e131e-e535-7dd3-97ef-6b4b584b5b83

**Findings**

[P1] [battle_repeat_attack_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd:135) and [battle_charge_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_charge_resolver.gd:491) bypass the normal death pipeline. These paths clear defeated units directly, then call aggregate `record_skill_effect_result`, instead of going through loot collection and per-target metrics. Normal deaths call [battle_runtime_loot_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_loot_resolver.gd:56) and [battle_metrics_collector.gd](E:/game/magic/scripts/systems/battle/runtime/battle_metrics_collector.gd:144). Failure mode: enemies killed by repeat attacks or charge path AoE can drop no `drop_entries` loot, while metrics show source damage/kills but miss target `damage_taken`, `death_count`, and faction taken/death totals.

[P1] [battle_spawn_reachability_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_spawn_reachability_service.gd:169) can approve spawns using skills that actual battle casting rejects. It only filters target mode/team and then checks effective range at [battle_spawn_reachability_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_spawn_reachability_service.gd:302), but actual cast legality blocks weapon-family and melee-weapon requirements in [battle_skill_turn_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_turn_resolver.gd:96). `BattleRangeService` can still return a range for `requires_current_melee_weapon` without proving the required weapon exists at [battle_range_service.gd](E:/game/magic/scripts/systems/battle/rules/battle_range_service.gd:73). Failure mode: a unit whose only “attackable” skill is weapon-gated can pass spawn reachability, then have no legal offensive cast in combat.

[P2] Transient equipment loot schema is internally inconsistent. [battle_runtime_loot_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_loot_resolver.gd:182) emits `equipment_instance` loot from pre-rolled transient instances, and [equipment_drop_service.gd](E:/game/magic/scripts/systems/inventory/equipment_drop_service.gd:38) creates those with empty `instance_id`. `BattleResolutionResult.set_loot_entries` accepts that path, but strict `from_dict` rejects the same payload via [battle_resolution_result.gd](E:/game/magic/scripts/systems/battle/core/battle_resolution_result.gd:225) and normal `EquipmentInstanceState` validation at [equipment_instance_state.gd](E:/game/magic/scripts/systems/inventory/equipment_instance_state.gd:65). Failure mode: a valid in-memory battle result containing random equipment cannot round-trip through `to_dict/from_dict` before facade commit; the parsed result becomes invalid/null even though direct commit can allocate the persistent ID.

[P2] [battle_change_equipment_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_change_equipment_resolver.gd:94) refreshes equipment projection but only clamps HP afterward. The called factory method at [battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:134) refreshes attributes/weapon/basic attack, while full unit refresh clamps MP, stamina, aura, AP, and related runtime fields at [battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:63). Failure mode: gear that changes resource maxima or action stats can leave `current_mp`, `current_stamina`, `current_aura`, `current_ap`, or turn threshold stale after an in-battle equipment swap, distorting skill legality, HUD state, turn order, and metrics until another full refresh path happens.

**Additional Risks**

[ battle_spawn_reachability_service.gd](E:/game/magic/scripts/systems/battle/runtime/battle_spawn_reachability_service.gd:257) has a hard `max_search_nodes` cap. On a connected large map, the only valid attack anchor can be beyond the first 2048 visited cells, producing a false unreachable result and unnecessary terrain rerolls.

[battle_runtime_loot_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_loot_resolver.gd:51) treats elite/boss conversion as `fortune_mark_target > 0`, while boss checks elsewhere include `boss_target`. If content ever marks a boss only with `boss_target`, calamity conversion can classify it as ordinary.

只读完成，未改文件，未运行测试。

## 2026-05-12 追加：可执行代码方案与复审闭环

本节只追加方案，不覆盖上面的 raw findings。目标是把 2026-05-11 的审查项归并成可执行代码切片；已在当前代码中确认修复的原问题标为 `FIXED/SKIP`，未修项保留为实施任务。

### 当前状态归并

- `random_chain` 路由 / AP 二次检查 / 死亡目标重选：`FIXED/SKIP`。当前 `BattleSkillResolutionRules.should_route_skill_command_to_unit_targeting()` 会把 `target_selection_mode = random_chain` 路由到 unit targeting；`_build_random_chain_target_pool()` 用 `require_ap = false`，避免扣 AP 后再因 AP 不足清空候选；每段重新从存活池选目标。已有 `tests/battle_runtime/skills/run_warrior_advanced_skill_regression.gd` 覆盖 unit-target contract、living pool、AP once、miss stop。
- `random_chain` 无合法目标仍扣成本：`OPEN`。`_validate_unit_skill_targets()` 当前对 random_chain 直接 allowed；需要在消耗成本前构建 resource-free legal target pool，池为空时 preview false 且 issue 不扣 AP/资源。
- transient `equipment_instance` loot round-trip：`FIXED/SKIP`。当前 loot resolver 会在构建 equipment loot entry 时分配正式 `instance_id`，`run_battle_resolution_contract_regression.gd` 已覆盖 assignment/round-trip；保留现有测试即可。
- wind/generic forced move 穿屏障：`PARTIAL/FIXED`。当前 wind/generic forced move 已调用 `resolve_unit_boundary_crossing()`；后续只纳入统一 relocation helper 防漂移。真正仍绕过屏障的是 blink/jump relocation、charge step movement、doom_shift swap。

### Phase A：统一死亡流水线

不要新增同名 helper；`BattleRuntimeModule.handle_unit_defeated_by_runtime_effect()` 已存在。实施时扩展现有契约，或新增 `handle_unit_defeated_by_skill_effect(defeat_context)` 包装它。

`defeat_context` 至少包含：`unit_state`、`source_unit`、`batch`、`log_line`、`skill_def/effect_id`、`damage/healing`、`record_enemy_defeated_achievement`、`record_effect_metrics`、`apply_on_kill_resources` 开关或回调。

统一 helper 负责：loot collection、occupancy clear、unit/faction `death_count`、可选 battle rating achievement、battle end check。技能特有的 on-kill resource 可以由调用方先执行，也可以通过回调传入，不能丢语义。

接入点：

- `scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd`：repeat kill 不再直接 `clear_defeated_unit()`。
- `scripts/systems/battle/runtime/battle_charge_resolver.gd`：path step AOE kill、blocker fall/collision kill 都走统一 helper。
- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`：ground lethal damage、height/fall lethal 都走统一 helper；damage 已杀死目标后跳过 forced move/special。
- `scripts/systems/battle/runtime/battle_timeline_driver.gd`：持续伤害 / 回合开始死亡路径统一处理，并显式 battle-end check。
- `scripts/systems/battle/runtime/battle_skill_outcome_committer.gd`：typed outcome kill 复用同一 helper。

新增测试：`tests/battle_runtime/runtime/run_battle_runtime_defeat_pipeline_regression.gd`，覆盖 repeat、charge path AOE、charge blocker fall/collision、ground lethal、timeline death、outcome committer kill 的 loot、death metrics、achievement、occupancy clear、battle end。

### Phase B：位移 / 屏障 / Preview 统一

新增 `BattleRuntimeRelocationService` 或等价 helper，使用结构化 request/result，不使用单一 `(unit, to_coord, mode)` 形状。

request 字段：`kind = normal_step / forced_step / teleport_place / swap / preview_path`、`unit`、`from_coord`、`to_coord`、可选 `other_unit`、`check_barrier`、`apply_occupancy`、`force_place`、`source_label`。

result 字段：`moved`、`blocked`、`barrier_applied`、`teleported`、`defeated`、`stop_reason`、`from_coords`、`to_coords`。

接口要求：

- `classify/preview` 不调用 damage、mastery、RNG、loot。
- passage preview 只报告 barrier crossing / hazard，不承诺随机 outcome。
- projected skill preview 报告 deterministic `blocked / breaks_layer / barrier_only`，改写 hit/damage preview；合法破解屏障的施放不能被误标为不可施放。
- 替换 `BattleChargeResolver._resolve_preview_charge_anchor()` 的执行器模拟；不要再用 `handle_charge_skill_command()` 推导 preview 落点。
- normal movement partial stop 后只扣已成功步数 cost，记录 changed coords，并明确锁移动/行动状态。

接入点：

- `BattleMovementService`：move preview 和 issue 都通过 relocation preview/apply，preview 跨 barrier 不 mutate。
- `BattleChargeResolver`：charge 每步移动和推人都走 relocation apply。
- `BattleGroundEffectService`：blink/jump relocation 接入；wind/generic forced move 迁入 helper 防漂移。
- `BattleSpecialSkillResolver`：doom_shift swap 用双单位 request，检查两方向 barrier crossing 与占位恢复。

新增测试：

- `tests/battle_runtime/runtime/run_battle_barrier_preview_issue_regression.gd`：unit/projected skill preview 的 barrier-only/no-mutate、charge preview 无 mastery/RNG/loot/伤害副作用、movement preview 跨 barrier no-mutate 且暴露 hazard/blocked 口径、movement issue partial stop 扣已走 cost/记录 changed coords/锁移动。
- `tests/battle_runtime/runtime/run_battle_relocation_regression.gd`：blink/jump、charge step、doom_shift swap、wind/generic forced move 的统一 barrier crossing 行为。

### Phase C：战斗单位刷新、身份投影与体型恢复

- `BattleUnitFactory.refresh_battle_unit()`：保存 old `current_ap`，刷新后 `current_ap = clampi(old_current_ap, 0, refreshed_max_ap)`，不回满 AP；只有 build / turn activation 显式初始化 AP。
- `BattleUnitFactory.refresh_equipment_projection()`：同步 clamp HP / MP / stamina / aura / AP / move / action_threshold，并从 progression 基线重建 `known_active_skill_ids` / `known_skill_level_map` 后再套 equipment gate。战中装备盾牌应出现盾牌技能，卸盾应移除。
- `PassiveStatusOrchestrator._clear_identity_projection()`：清理 race / subrace / ascension / bloodline 派生 charges 与 per-turn limits，避免 suppressed race stale charges。
- `BattleSkillTurnResolver.consume_racial_skill_charge()`：per-battle 与 per-turn 同时存在时两者都扣；任一为 0 时阻止施放。
- body size override 到期恢复前检查恢复 footprint。若被占，调用方不能 erase 原 status；应保留 status 或写入 pending restore params，下次 tick 再尝试。恢复失败时不能覆盖 occupant。

测试更新：

- `tests/battle_runtime/runtime/run_battle_unit_factory_regression.gd`：把 refresh AP 预期改为保留 spent AP / clamp；增加 equipment refresh 后全资源 clamp 与盾牌技能 equip/unequip 出现/移除断言。
- `tests/battle_runtime/skills/run_passive_status_orchestrator_regression.gd`：用已有 stale racial charge 的 suppressed ascension 单位证明清理。
- `tests/battle_runtime/skills/run_dragon_breath_regression.gd`：覆盖 per-battle + per-turn 同时存在时两者都扣。
- `tests/battle_runtime/skills/run_titan_colossus_form_regression.gd`：恢复 footprint 被占时，原单位保持当前 body size/category 与 occupied coords，blocker occupant 不被覆盖；status/pending restore 保留；移走 blocker 后下一 tick 恢复并记录 changed coords。

### Phase D：规则一致性与数据契约

- `BattleSpawnReachabilityService`：不要调用会检查 AP/MP/cooldown 的 full cast block。抽出 resource-free cast legality helper，只检查 weapon family、`requires_current_melee_weapon`、`requires_equipped_shield`、target mode/filter/range 与静态装备门禁。`max_search_nodes` 溢出返回 tri-state `inconclusive`；start/terrain build 可扩大 cap 或记录诊断重试，不能当 hard unreachable。
- `BattleExecutionRules`：先新增 shared `is_elite_or_boss_target()` 并统一 `is_boss_target()`，再替换 loot / special / mastery / report / AI / fate 中的本地 predicate；本轮至少覆盖 loot / special / report 的 `boss_target` only 分叉。
- `TraitTriggerHooks`：runtime dispatch 与 `TraitTriggerContentRules` 同源。倾向把 method name 放进同一张 content rules 表，content validation 与 runtime 共用；最低限度要加 parity contract test。

测试更新：

- `tests/battle_runtime/runtime/run_battle_spawn_reachability_regression.gd`：唯一攻击技能需要近战武器但单位无近战武器时不可通过；search cap overflow 返回 inconclusive。
- `tests/battle_runtime/skills/run_trait_trigger_regression.gd`：content/runtime dispatch parity。
- `tests/battle_runtime/runtime/run_battle_loot_drop_luck_regression.gd` 或 `tests/battle_runtime/runtime/run_battle_resolution_contract_regression.gd`：`boss_target` only 仍按 elite/boss 分类。

### 建议执行顺序

1. 先修 `random_chain` 无合法目标不扣成本，并补 warrior advanced skill 回归。
2. 落地 Phase A 死亡流水线，先统一 helper 契约，再改调用点，最后加 defeat pipeline runner。
3. 落地 Phase B relocation / barrier preview；先替换 charge preview 模拟，再接 movement / blink / jump / charge / doom_shift。
4. 落地 Phase C refresh / identity / body size。
5. 落地 Phase D reachability / trait dispatch / elite-boss predicate。
6. 每个 phase 后跑对应 focused runner；最后跑非 simulation 的 battle runtime 相关集合。不要把 `tests/battle_runtime/simulation/run_battle_simulation_regression.gd`、`tests/battle_runtime/simulation/run_battle_ai_vs_ai_simulation_regression.gd`、`tests/battle_runtime/simulation/run_battle_balance_simulation.gd` 纳入常规全量。

### Project Context Units Impact

本次只追加讨论文档，未改变运行时代码关系，因此不更新 `docs/design/project_context_units.md`。

真正实施代码时必须同步更新：

- Phase A / B / D：CU-15、CU-16、CU-19。
- Phase C：除 CU-15 / CU-19 外，还会影响 CU-10 和 CU-12 的装备 / 角色投影边界描述。

### 子代理复审闭环

- 首轮：架构、运行时、测试三名子代理均未通过；主要问题是死亡 helper 已存在但契约不完整、relocation request 形状不足、spawn reachability 不能使用资源门禁、random_chain 不能整体标 fixed、body size 延迟恢复会丢 status、测试缺少证明入口。
- 第二轮：架构审查通过；运行时审查通过；测试审查仍要求补 movement preview 与 body size occupant 不覆盖断言。
- 第三轮：测试审查通过。

最终确认：

- 架构审查：通过。
- 运行时审查：通过。
- 测试审查：通过。

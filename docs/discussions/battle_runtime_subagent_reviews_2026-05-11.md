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

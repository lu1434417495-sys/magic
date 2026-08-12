# 装备套装系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-12`

## 定位

套装是装备视图上的派生规则，不是新的持久装备类型。原装备实例、槽位、耐久、物品 ID 和价格继续由 `EquipmentState` / `EquipmentInstanceState` / `ItemDefinition` 拥有；套装只根据当前有效装备计算成员数、阈值属性和阈值 trait。

当前正式内容目录是 `res://data/configs/gear_sets/**/*.tres`。`GearSetContentRegistry` 在进程内容构建期把 `GearSetDef` / `GearSetThresholdDef` 投影为只读 `GearSetDefinition`，并由 `ContentSnapshot` 和 `GameContentCatalog` 发布。运行期不保留 raw Resource。

## 所有权与主链

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `GearSetDef`、`GearSetThresholdDef` | 成员、阈值、必需成员、阈值属性、阈值 trait、usage 锚点 |
| 加载与校验 | `GearSetContentRegistry` | 校验 ID、成员物品、阈值顺序、trait 引用和锚点，并投影 immutable definition |
| 进程内容 | `ContentSnapshot`、`GameContentCatalog` | 发布套装 definition 索引 |
| 角色聚合 | `GearSetEvaluationService`、`CharacterTraitService`、`CharacterManagementModule` | 按当前装备视图计算阈值，合并属性与有效 trait |
| 战斗投影 | `BattleUnitFactory`、`BattleEquipmentAbilityProjectionService` | 将阈值 trait 投影为 `PlayerPersistentGearSetThreshold` 能力来源 |
| 展示 | `GameRuntimeCharacterInfoBuilder`、`PartyManagementWindow` | 复用同一套装摘要投影，在战斗人物信息和战外人物管理装备页显示成员数与全部阈值状态 |
| 世界唯一实例 | `WorldRuntimeData`、`WorldUniqueEquipmentPoolState`、`GameSession` | 仅在新档初始化凤凰十件原始实例，并持久化 reserve/shop location |
| 获取转移 | `SettlementShopService`、`GameRuntimeFacade.BattleLootPort` | 在商店刷新、买卖和随机装备掉落中转移既有实例，不重新生成凤凰成员 |

```text
GearSetDef
  -> GearSetContentRegistry
  -> ContentSnapshot / GameContentCatalog
  -> GearSetEvaluationService(current EquipmentState)
  -> threshold attributes + gear_set_threshold traits
  -> CharacterManagement / BattleUnitFactory / character-info HUD
```

## 计算合同

- 只计算当前装备视图中、属于 `member_item_ids` 的不同有效物品 ID；同一成员的重复副本只计一次。每个候选还必须满足 `ItemDefinition` 声明的允许 entry slot，且实际占用槽位集合必须与该物品的 canonical footprint 完全一致；套装定义不复制第二份槽位表。
- 耐久归零的装备不计入套装成员。
- 阈值结构由各套装自己声明，不要求统一为 2/4、3/5/7/10 或其他固定形状；达到高阈值时，已达到的低阈值继续生效。
- `mandatory_member_item_ids` 非空时，件数和必需成员必须同时满足。
- 纯静态阈值属性直接 authored 在 `GearSetThresholdDef.attribute_modifiers`，Resource→Definition 投影会强制把来源规范为 `gear_set` / `gear_set::<set_id>::<threshold_id>`；需要抗性、豁免、状态或装备能力的阈值才授予 trait。同一阈值的直接属性不能与其授予 trait 重复声明同一 `attribute_id`。
- 阈值 trait 的稳定实例键为 `gear_set::<set_id>::<threshold_id>::<trait_id>`，来源类型固定为 `GearSetThreshold`。
- 需要保存 usage 的阈值能力绑定到一个真实装备实例。优先使用仍装备且有效的 `usage_anchor_item_id`；否则按套装成员配置顺序选择首个有效成员。
- 战斗内换装通过 `BattleUnitFactory.RefreshEquipmentProjection(...)` 重新计算属性、trait 和装备能力来源。阈值失效后不保留其派生来源；最大生命下降沿现有资源钳制规则处理。
- 套装定义和阈值本身不写入存档。每世界日次数复用锚点装备实例已有的 equipment-ability usage；阈值机制自身没有新增 save 字段、版本或兼容迁移。凤凰唯一实例池是独立的世界获取链字段，不属于阈值计算状态。

## 凤凰重生：当前已落地范围

正式套装定义为 `phoenix_rebirth_set`，保留十个既有 `item_id`、槽位、物品名、价格和 150,000 总基础价格，并只追加系统唯一获取所需的通用 `world_unique_equipment` tag。原始作者文档已同步正式单件语义与 3/5/7/10 阈值；正式空间单位统一使用格。

| 阈值 | 当前正式效果 |
|---:|---|
| 3：凤凰血脉 | `freeze` 伤害 `half`；constitution save `+2`。 |
| 5：不灭心火 | `hp_max +20`。 |
| 7：余烬展翼 | `fire` 伤害 `immune`；每场战斗首次在正式伤害与致死仲裁结束后仍存活且生命不高于 25% 时恢复 `2D8`，进入 `120 TU` 余烬形态；移动点上限 `+1`，武器、徒手或法术攻击的主直接伤害段附加 `1D4 fire`。 |
| 10：太阳涅槃 | 生命不高于 50% 时消耗 `3 AP`、每日一次：自身治疗到 50% 生命地板；半径 3 格敌人受到 `4D10 fire`、agility `DC 17` 成功半伤；按 `hp_percent_bp ASC, unit_id ordinal ASC` 选择至多四名其他友方恢复 `2D8`；进入 `240 TU` 金焰形态，移动点上限 `+2`，主直接伤害段附加 `1D10 fire`。 |

七件套使用 `OnDamageTakenFinalized` defender reaction：只有实际 HP 伤害、致死保护处理完毕且单位仍存活时才检查低血条件。Preview 在 detached unit 上使用骰值期望，不推进正式 RNG、状态或每战次数。十件套是正式装备技能入口，HUD、命令、preview、执行和每日 usage 共用 `BattleSkillAvailabilityService`；AI 从同一 availability 取得 entry，再由 `BattleAiSkillAffordanceClassifier` 按每个 effect 的 canonical target filter 聚合 hostile/support affordance。灰烬之火生成敌方 unit action，太阳涅槃生成 ground-hostile action，候选都必须通过 canonical preview 与 mutation guard。

金焰启动时清除余烬；金焰存在时，七件套仍可完成治疗和每战次数消费，但不会用低阶形态覆盖金焰。两种形态附伤都允许 `IncludesWeaponDamage=false`，因此徒手和法术主直接段不会被错误排除。

太阳涅槃的基础 command preview 单独展示治疗、伤害与目标结果；随后在新的 detached battle 子集上投影装备技能 after-use，清除余烬、施加金焰及对应后置动作通过 reaction action previews / `source_preview_after` 可见，canonical battle state 与每日账本保持不变。`source_preview_after` 只表示装备 reaction 后的来源单位，不合并基础技能自身的 HP/status 变化。致死预览同样公开凤凰候选的有序概率与条件成功动作；带 saving throw 的分支不会因平均伤害不致死而丢失候选。连续伤害段会延续每条分支的凤凰免死 usage；披风概率复活的成功分支会先写入 detached 的 `low_hp_burst_used`，再进入下一段，因此不会重复爆发。

单件方面，当前已落地十个真实 `ItemDef`、对应装备能力 trait、内部/授予技能与 typed 状态。原物品 ID、槽位、价格和基础属性不变；空间距离统一使用格，持续时间统一使用 TU。

| 单件 | 槽位 | 基础价格 | 当前 typed 基础属性/被动 |
|---|---|---:|---|
| 凤凰涅槃头冠 | head | 22,000 | `armor_ac_bonus +2`；单件固定 `fire/half`，达到7件阈值后由套装升级为 `fire/immune` |
| 凤凰涅槃板甲 | body | 37,000 | `armor_ac_bonus +6`；`hp_max +20` |
| 凤凰涅槃护手 | hands | 17,000 | `armor_ac_bonus +1`；`attack_bonus +1` |
| 凤凰涅槃胫甲 | feet | 17,000 | `armor_ac_bonus +1` |
| 凤凰重生烈焰披风 | cloak | 12,000 | `fire/half`；constitution save `+2` |
| 涅槃之心项链 | necklace | 10,000 | 当前无额外 typed 属性；见下方旧字段边界 |
| 重生之戒 | ring_1 | 8,000 | `hp_max +10` |
| 灰烬之戒 | ring_2 | 8,000 | `fire/half`；constitution save `+1` |
| 凤凰蛋 | special_trinket | 10,000 | `mp_max +15` |
| 凤凰徽章 | badge | 9,000 | constitution save `+1` |

十件基础价格合计 150,000。上表只记录当前运行时实际可消费的 typed 配置，不把缺少 owner 的旧文本字段映射成近似属性。

## 单件特殊效果当前边界

| 单件 | 当前正式特殊效果 |
|---|---|
| 凤凰涅槃头冠 | 单件固定 `fire half`。普通致死时每战一次 25% 尝试；成功恢复至最大生命 25%，随后对半径 3 格所有敌人造成 `3D10 fire`，无敌人时仍完成恢复。正式掷骰即消费本战尝试；常驻 `fire immune` 只由7件套阈值提供。 |
| 凤凰涅槃板甲 | 每次外部、非自身、非装备能力生成的正 `fire` 原始伤害增加并刷新一层 `60 TU` 燃烧；最多 3 层，每层 `AC +1`。每日一次、`2 AP` 释放火盾：清除全部燃烧，获得 `60 TU`、`AC +3`；期间每次成功近战命中后攻击者受到 `2D6 fire`，无豁免。 |
| 凤凰涅槃护手 | 主手为空时可用 `1 AP` 火焰打击，正式命中链造成 `1D8 physical_blunt + 1D10 fire`，两段骰均按各自显式配置参与 critical；每日三次、`1 AP` 触碰相邻的其他存活友方 `2D10 HP`，不能选择自己或死者。 |
| 凤凰涅槃胫甲 | 普通移动的每个离开格留下 `60 TU`、进入时 `1D6 fire` 的任意阵营足迹，同一单位每次移动命令只受同一片轨迹一次伤害。每日一次、`2 AP` 火焰冲锋沿正交直线移动至有效移动点容量的 3 倍；离开格改留 `2D6 fire` 轨迹，不与 `1D6` 重复。普通足迹使用 `phoenix_rebirth_fire_step`，冲锋轨迹使用 `phoenix_rebirth_flame_charge_trail`；board snapshot 投影该 overlay id，默认 render profile 以已登记 source（暂无专用贴图时使用 generated fallback）保证危险格可见。 |
| 凤凰重生烈焰披风 | 常驻 `fire/half`。每战第一次因外部、非自身、非装备能力生成的正原始伤害从高于 50% 降至不高于 50% 时，对半径 1 格敌人造成 `2D6 fire`；空范围也消费。另有每战一次 25% 普通致死尝试，成功恢复 `1D12 HP`；致死恢复造成跨阈值时也可触发低血爆发。 |
| 涅槃之心项链 | 每日一次、`1 AP`，自身或 1 格内一个存活友方恢复 `3D8 HP`，并移除至多一个当前 canonical 状态系统认定为可驱散的有害魔法；不可驱散状态不受影响。当前状态 ABI 没有独立 disease/curse/legendary 分类，因此不伪造该筛选。 |
| 重生之戒 | 保留 `hp_max +10`。生命不高于 50% 时可消耗 `2 AP` 使用“生命重燃”，恢复已损失生命的 60%；每场战斗一次，不能复活，且正式治疗倍率可削减实际恢复量。该戒指不再参与致死拦截。 |
| 灰烬之戒 | 每日一次、`1 AP` 的同一技能入口：对自身/1 格内友方治疗 `2D10`，或对 1 格内敌人造成 `3D10 fire`；两个分支共享同一日账本。 |
| 凤凰蛋 | 只有完整10件套的 `gear_set.phoenix_rebirth.10.solar_rebirth` trait 存在时才投影该能力；月度账本仍归凤凰蛋实例。一个世界月固定为 `450 world steps`。每世界月一次致死拦截，可拦截律令死亡，恢复最大生命 30%；先获得 `120 TU` 火焰化身（`fire immune`、攻击附加 `1D10 fire`），再对半径 2 格敌人造成 `3D10 fire`。 |
| 凤凰徽章 | 佩戴者及半径 2 格存活友方获得动态 `fire/half` 光环；来源死亡、卸装或离开范围即失效，同层不重复折半。每日一次、`2 AP` 使当前范围存活友方获得 `120 TU` 凤凰祝福：每名受益者每战一次普通致死恢复 `1D10 HP`，攻击附加 `1D6 fire`。 |

普通致死的正式顺序是：fatal trait → Death Ward / Last Stand → 凤凰祝福 `100` → 烈焰披风 `300` → 凤凰头冠 `400` → 凤凰蛋 `500` → `MarkDead`。重生之戒已改为低血主动治疗，不进入该仲裁。凤凰祝福、披风和头冠的 `protection_priority` 为 `100`；凤凰蛋为 `900`。`phantasmal_kill_execute` 的 death-source priority 为 `300`，会绕过前三个 protection priority 为 `100` 的 Phoenix 候选，但仍可被凤凰蛋拦截。律令死亡的 death-source priority 为 `900`，会绕过普通 fatal trait、Death Ward / Last Stand 以及前三个凤凰候选；当前正式内容中只有完整10件套时存在的凤凰蛋能够拦截。优先级阻断发生在 usage 消耗之前。

凤凰攻击附伤统一加入 `phoenix_attack_append` 替换组，优先级为余烬 `100`、祝福 `200`、凤凰蛋形态 `300`、金焰 `400`；同组只保留当前最高优先级候选，较高效果结束后较低效果自动恢复，不重复计算。凤凰蛋致死成功后的 `3D10` 内部爆发、火盾反伤与移动足迹都是装备能力生成伤害，不是攻击主直接伤害段，因此不会再取得该组附伤，也不会递归触发板甲燃烧或披风爆发。

同理，当前属性域没有正式 owner 的 `movement_speed`、`religion_bonus`、`medicine_bonus` 等旧字段未伪装成其他战斗属性。它们需要先建立对应 typed 属性/utility query，再补资源和行为回归。

## 凤凰唯一获取链

只对新存档生效：`GameSession.CreateNewSave(...)` 在建档事务内按 `phoenix_rebirth_set` 的十个成员各 mint 一个传奇、稳定 `instance_id` 的原始装备实例，全部放入 `WorldRuntimeData.unique_equipment_pool` 的隐藏 reserve。十件内容带通用 `world_unique_equipment` tag；`PartyWarehouseService` 的 raw `item_id` mint、空实例 ID 或强制换新 ID 路径全部 fail closed。正式商店与掉落链只传入池中已有的稳定实例，并在 reserve/shop/玩家仓库之间转移，保留其 instance id、耐久与 trait rolls。内部 typed 实例转移目前以非空 `instance_id` 作为可信调用前置，不提供独立的池签发凭证；该 seam 不面向未受信输入，也不用于扫描、去重或拒绝玩家手改存档。

随机装备在战斗层只冻结 `item_id / quantity / drop_luck` typed request，不预生成实例；`GameRuntimeBattleLootCommitService` 在同时拥有 world-pool checkpoint、仓库容量和失败回滚的事务边界选择唯一原实例或调用普通装备生成器。商店刷新和普通随机装备掉落的凤凰注入概率当前均为 `5%`；若随机掉落明确请求凤凰成员，则只从 reserve 取对应原实例，池中不存在时不会回退到普通装备生成器复制一件。

旧存档缺少可选 `unique_equipment_pool` 字段时仍按原形加载，且不会回填凤凰套装。读取存档时只校验该字段自身的当前结构，不扫描玩家仓库、角色装备或其他存档位置，也不做全局去重；玩家手工修改存档制造出的额外凤凰实例允许保留，不属于系统唯一生成保证。

## 代表性回归

- `tests/equipment/run_gear_set_evaluation_regression.cs`
- `tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs`
- `tests/battle_runtime/runtime/run_phoenix_rebirth_single_item_behavior_regression.cs`
- `tests/battle_runtime/runtime/run_phoenix_rebirth_body_cloak_behavior_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_movement_trail_regression.cs`
- `tests/battle_runtime/rendering/run_equipment_movement_trail_overlay_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_finalized_damage_status_ac_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_mitigation_aura_regression.cs`
- `tests/battle_runtime/rules/run_dynamic_move_capacity_skill_range_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_equipment_granted_skill_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_fatal_intercept_runtime_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_ability_preview_integrity_regression.cs`
- `tests/battle_runtime/runtime/run_combat_effect_heal_floor_target_limiter_regression.cs`
- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/progression/schema/run_phoenix_rebirth_single_item_content_regression.cs`
- `tests/progression/schema/run_phoenix_rebirth_body_cloak_content_regression.cs`
- `tests/equipment/run_phoenix_rebirth_unique_acquisition_regression.cs`
- `tests/world_map/ui/run_party_management_window_regression.cs`
- `tests/runtime/validation/run_resource_validation_regression.cs`

装备能力通用合同见 [`../battle/equipment_ability_runtime.md`](../battle/equipment_ability_runtime.md)，技能执行合同见 [`../battle/skill_runtime.md`](../battle/skill_runtime.md)。

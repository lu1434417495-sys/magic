# 通用 Trait 系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-19`

## 定位

本文记录当前通用人物 trait、装备随机 trait、effective trait 合并和战斗投影链。内容目录、未来 trait 类型和扩展阶段不属于本文；相关历史方案仍在 [`../../proposals/progression/`](../../proposals/progression/) 中。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `data/configs/json/traits/traits.json`、`TraitJsonImportContracts` | 声明 trait、来源范围、堆叠、roll schema、charge、属性和被动效果 |
| 校验与投影 | `TraitContentJsonAuthoringDomain`、`TraitJsonImportParser`、`TraitImportModelValidator`、`TraitDefinitionProjector`、`TraitContentRegistry` | strict DTO → plain ImportModel → domain validator → immutable definition；production 不加载 trait Resource |
| 人物持久态 | `PartyMemberState.trait_instances`、`TraitInstanceState` | 保存 `character` 来源的实例、等级、roll 值和 charge 状态 |
| 装备持久态 | `EquipmentInstanceState.trait_instances`、`EquipmentTraitRollService` | 在装备实例获得稳定 instance id 后生成并保存 `equipment_roll` trait |
| Effective 合并 | `CharacterTraitService`、`EffectiveTraitSet` | 合并人物和当前装备视图，执行 source、stack、rank 和实例键规则 |
| 战斗投影 | `BattleUnitFactory`、`BattleUnitEffectiveTraitState`、`BattleEffectiveTraitInstanceState` | 把 effective trait 深拷贝为 battle-local owner state，并维护实例与派生 trait id 投影 |
| 战斗消费 | `BattleTraitPassiveProjectionService`、`BattleUnitSaveModifierState`、`BattleUnitDamageResistanceState`、`TraitTriggerHooks` | 把 trait 被动投影到独立下游 owner，并执行 typed trigger 与 charge 生命周期 |

## 运行链

```text
trait JSON
  -> TraitContentJsonAuthoringDomain
  -> TraitJsonImportParser / TraitImportModelValidator
  -> TraitDefinitionProjector
  -> TraitContentRegistry index
  -> TraitDefinition in ContentSnapshot
  -> PartyMemberState / EquipmentInstanceState TraitInstanceState
  -> CharacterTraitService.BuildEffectiveTraits(...)
  -> BattleUnitFactory
  -> BattleUnitState typed gateway
  -> BattleUnitEffectiveTraitState
  -> passive projection + trigger hooks
```

## 实现约束

- 正式 trait authoring 只有 JSON；239 个 entry 通过同一 pure CLR import validator。`TraitDef`、nested authoring Resource、TRES adapter/converter 和 Resource→Definition 兼容桥均已删除。session、UI、CharacterManagement 和 battle runtime 只消费 `TraitDefinition` 索引。
- save advantage/disadvantage/immunity 字段保存裸 save tag；`*_immunity` 后缀写法由 `TraitImportModelValidator` fail closed。离线 CLI 与 production descriptor 复用相同词表和规则。
- `TraitInstanceState` 是持久实例的单一数据形状。人物实例只允许人物来源，装备实例只允许装备 roll 来源；反序列化严格校验字段集和 source kind。
- `CharacterTraitService` 必须以当前 equipment view 重算 effective set。战斗内换装或装备损坏后由 `BattleUnitFactory.RefreshEquipmentProjection(...)` 重建，不从角色原装备状态旁路读取。
- `BattleUnitEffectiveTraitState` 是战斗内 effective trait 真相源，由 `BattleUnitState` 提供 typed gateway；普通写入口深拷贝并规范化实例，再从实例派生去重且按 ordinal 排序的 trait id。
- 规则消费者只能读取 detached scalar view，不能取得 owner 内的可变实例或 roll-value 列表。canonical/plain snapshot 同样从实例重建 trait id；strict load 在校验实例与 id 集合等价后保留 payload 原始 id 顺序。
- 属性、save advantage、damage resistance 和 passive status 由 `BattleTraitPassiveProjectionService` 投影；damage resistance 的 stronger-only 选择仍属于该投影服务，`BattleUnitDamageResistanceState` 只负责容器所有权、规范化写入和只读查询。事件型行为由 `TraitTriggerHooks` 执行，两条路径不互相复制规则。
- AI mutation guard 的 exact snapshot 保留 owner 缺失、null 列表/条目、原始 id 顺序和非法 sentinel，用于准确检测并恢复突变；gameplay clone 和 canonical/plain snapshot 走规范化路径，不能与 exact 诊断语义混用。

## 代表性回归

- `tests/progression/identity/run_trait_content_registry_regression.cs`
- `tests/progression/schema/run_trait_json_content_regression.cs`
- `tests/progression/schema/run_trait_content_rules_regression.cs`
- `tests/progression/schema/run_trait_instance_state_schema_regression.cs`
- `tests/equipment/run_equipment_trait_roll_regression.cs`
- `tests/progression/core/run_effective_trait_set_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_owner_api_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`
- `tests/battle_runtime/skills/run_trait_trigger_regression.cs`

架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-10、CU-11、CU-12、CU-13、CU-15 和 CU-16。

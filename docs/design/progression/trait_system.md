# 通用 Trait 系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-17`

## 定位

本文记录当前通用人物 trait、装备随机 trait、effective trait 合并和战斗投影链。内容目录、未来 trait 类型和扩展阶段不属于本文；相关历史方案仍在 [`../../proposals/progression/`](../../proposals/progression/) 中。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `data/configs/traits/*.tres`、`TraitDef` 及 typed 子资源 | 声明 trait、来源范围、堆叠、roll schema、charge、属性和被动效果 |
| 校验与投影 | `TraitContentRules`、`TraitTriggerContentRules`、`TraitContentRegistry`、`TraitDefinition` | 加载期校验固定值与跨字段约束，并把 Resource 投影为只读 definition |
| 人物持久态 | `PartyMemberState.trait_instances`、`TraitInstanceState` | 保存 `character` 来源的实例、等级、roll 值和 charge 状态 |
| 装备持久态 | `EquipmentInstanceState.trait_instances`、`EquipmentTraitRollService` | 在装备实例获得稳定 instance id 后生成并保存 `equipment_roll` trait |
| Effective 合并 | `CharacterTraitService`、`EffectiveTraitSet` | 合并人物和当前装备视图，执行 source、stack、rank 和实例键规则 |
| 战斗投影 | `BattleUnitFactory`、`BattleEffectiveTraitInstanceState` | 把 effective trait 深拷贝为 battle-local state |
| 战斗消费 | `BattleTraitPassiveProjectionService`、`TraitTriggerHooks` | 投影被动属性/状态并执行 typed trigger 与 charge 生命周期 |

## 运行链

```text
TraitDef Resource
  -> TraitContentRegistry validation
  -> TraitDefinition in ContentSnapshot
  -> PartyMemberState / EquipmentInstanceState TraitInstanceState
  -> CharacterTraitService.BuildEffectiveTraits(...)
  -> BattleUnitFactory
  -> BattleUnitState.effective_trait_instances
  -> passive projection + trigger hooks
```

## 实现约束

- Authored `TraitDef` 只存在于同步内容构建边界；session、UI、CharacterManagement 和 battle runtime 消费 `TraitDefinition` 索引。
- `TraitInstanceState` 是持久实例的单一数据形状。人物实例只允许人物来源，装备实例只允许装备 roll 来源；反序列化严格校验字段集和 source kind。
- `CharacterTraitService` 必须以当前 equipment view 重算 effective set。战斗内换装或装备损坏后由 `BattleUnitFactory.RefreshEquipmentProjection(...)` 重建，不从角色原装备状态旁路读取。
- `BattleUnitState.effective_trait_instances` 是战斗内真相源；战斗规则不在执行期反查 trait/item catalog。
- 属性、save advantage、damage resistance 和 passive status 由 `BattleTraitPassiveProjectionService` 投影；事件型行为由 `TraitTriggerHooks` 执行，两条路径不互相复制规则。
- AI mutation guard、battle snapshot 和 save projection 必须深拷贝实例与 roll 值，不能保留对可变列表的别名。

## 代表性回归

- `tests/progression/identity/run_trait_content_registry_regression.cs`
- `tests/progression/schema/run_trait_content_rules_regression.cs`
- `tests/progression/schema/run_trait_instance_state_schema_regression.cs`
- `tests/equipment/run_equipment_trait_roll_regression.cs`
- `tests/progression/core/run_effective_trait_set_regression.cs`
- `tests/battle_runtime/skills/run_trait_trigger_regression.cs`

架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-10、CU-11、CU-12、CU-13、CU-15 和 CU-16。

# Fate、Fortuna 与 Misfortune 当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-17`

## 定位

本文记录已经落地的 luck 读取、fate-aware 攻击、Fortuna 标记与 guidance、Misfortune calamity/技能规则、low-luck 事件及战后奖励链。属性 breakpoint 等未落地扩展保留在 [`../../proposals/progression/fortune_luck_expansion.md`](../../proposals/progression/fortune_luck_expansion.md)，不属于当前实现。

## 当前所有权

| 领域 | 当前 owner |
|---|---|
| 角色 luck 与永久标记 | `UnitBaseAttributes.custom_stats`、`PartyMemberState` |
| 队伍级尝试锁与事件事实 | `PartyState.fate_run_flags`、meta flags |
| Fate 攻击公式 | `BattleFateAttackRules`、`FateAttackFormula`、`BattleHitResolver` |
| Typed fate 事件 | `BattleFateEventBus`、`BattleFateEventPayload` |
| 战斗侧编排 | `FateRuntimeModule` |
| Fortuna | `FortuneService`、`FortunaGuidanceService` |
| Misfortune | `MisfortuneService`、`MisfortuneGuidanceService`、`BattleCalamityStore` |
| Low luck | `LowLuckEventService`、`LowLuckRelicRules` |

## 运行链

```text
BattleHitResolver / battle events
  -> BattleFateEventBus typed payload
  -> FateRuntimeModule
     -> FortuneService / FortunaGuidanceService
     -> MisfortuneService / MisfortuneGuidanceService
     -> LowLuckEventService
  -> battle-local calamity, character gateway writes, loot or pending rewards
```

## 实现约束

- `FateRuntimeModule` 是 battle runtime 的 sidecar 编排点；各服务不能自行持有另一个 BattleRuntimeModule 或绕过 character gateway 写永久状态。
- Fortuna guidance 必须先观察事件前状态，随后 `FortuneService` 才写入标记；事件订阅顺序属于当前合同。
- Calamity 是战斗内状态，由 `BattleCalamityStore` 与 `MisfortuneService` 管理；永久 rank 和角色成长仍走 faith/pending reward 链。
- Low-luck 战后 loot 与 pending reward 先合并到 `BattleResolutionResult`/待提交集合，再随正式战斗结算提交，不能在 resolver 中直接写仓库。
- Rollback 会捕获并恢复 low-luck sidecar 状态；重试或事务失败不能重复发放事件收益。
- `hidden_luck_at_birth` 等受保护 custom stat 只能由获授权的角色创建或剧情来源写入，普通奖励入口不能旁路修改。

## 代表性回归

- `tests/battle_runtime/fate/run_fate_attack_formula_regression.cs`
- `tests/battle_runtime/fate/run_fate_typed_event_regression.cs`
- `tests/battle_runtime/fate/run_misfortune_service_regression.cs`
- `tests/progression/fate/run_fortune_service_regression.cs`
- `tests/progression/fate/run_fortuna_guidance_regression.cs`
- `tests/progression/fate/run_low_luck_event_service_regression.cs`
- `tests/progression/fate/run_party_state_fate_regression.cs`

架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-11、CU-12、CU-14、CU-15 和 CU-16。

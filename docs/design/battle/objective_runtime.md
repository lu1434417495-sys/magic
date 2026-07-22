# 战斗目标与终局运行时

> 状态：`Current / Implemented (P0)`
> 核对日期：`2026-07-23`

## 当前实现边界

当前代码已经建立统一的战斗目标与终局管线，但只有 `elimination`（歼灭）具备可创作内容、运行时求值和正式回归。`boss`、`rescue`、`escape`、`escort`、`defense`、`intercept`、`node_operation`、`control` 已占用稳定的 typed mode 标识，尚不能在正式内容中使用；它们的规则仍属于 proposal。

这一区分是硬边界：mode 出现在 enum 中不等于该模式已经可玩。`BattleEncounterContentRegistry` 目前只接受 `BattleEliminationObjectiveDef`，遇到其他 objective Resource 必须校验失败。

## 内容所有权

正式链路为：

```text
EncounterAnchorData.encounter_profile_id
  -> BattleEncounterDefinition
    -> roster_profile_id -> WildEncounterRosterDefinition
    -> objective -> BattleObjectiveDefinition
    -> world_resolution -> success / failure / draw policy
```

`BattleEncounterDef` 是遭遇级 authoring owner。敌方编队、胜负目标和战后世界处理在这里汇合；世界锚点不再保存 `enemy_roster_template_id`，运行时也不得从敌人存在与否推断默认歼灭目标。缺失或不存在的 `encounter_profile_id` 必须在内容校验或开战入口失败，不能回退到旧 schema。

当前正式内容：

- `wolf_wilds`：`wolf_pack_skirmish` 编队、歼灭目标、成功后清除。
- `wolf_den`：`wolf_den` 成长编队、歼灭目标、成功后压制 3 世界步。
- `mist_hollow`：`mist_hollow` 编队、歼灭目标、成功后清除。

`BattleEncounterWorldResolutionDefinition` 分别声明 `PlayerSuccess`、`PlayerFailure`、`Draw` 的 `Preserve / Clear / Suppress` 行为。只要任一分支使用 `Suppress`，`suppression_steps` 必须为正；没有分支使用时必须为 0。

## 运行时所有权

`BattleState` 持有：

- 一个 `BattleObjectiveRuntimeState`，表示本场目标运行态；
- 最多一个不可变 `BattleFinalDecision`；
- `winner_faction_id` 只由 final decision 派生，不是可写真相。

`BattleFinalDecision` 包含 `ObjectiveMode`、`Outcome`、`EndReason`、`DecisionTu`。当前歼灭模式的合法终局组合是：

| Outcome | EndReason | 派生 winner |
|---|---|---|
| `PlayerSuccess` | `EliminationHostilesDefeated` | `player` |
| `PlayerFailure` | `EliminationAlliesDefeated` | `hostile` |
| `Draw` | `EliminationMutualDestruction` | `draw` |

`BattleResolutionResult` 复制 final decision，并把 `winner_faction_id` 与 `encounter_resolution` 作为只读输出投影。Fate、掉落、任务、世界结算和 BattleSim 的业务判断消费 typed `Outcome`，不能再比较 winner 字符串。

## 原子求值边界

所有可能改变目标事实的根 mutation 必须遵循：

```text
BeginObjectiveMutation
  -> 完整命令 / timeline step / start-confirm reaction / promotion choice
  -> 任意同步递归反应与多目标伤害
EndObjectiveMutation
  -> FlushBattleOutcomeEvaluation
```

嵌套 mutation 只标记 dirty，只有最外层成功结束时求值。因此同一技能让双方同时阵亡时，只能得到一次 `Draw`，不能在第一个阵亡回调后提前宣布胜负。mutation 异常时不提交中间终局。

终局决定一旦锁存不可替换，同时冻结 timeline。若 `PromotionChoice` 或 `StartConfirm` modal 仍在处理，系统只锁存决定，等待 modal 结束后由同一 flush 管线完成战斗。`CompleteBattle` 是 phase、奖励、resolution result、batch flags 和终局日志的唯一完成入口；重复 flush 必须幂等。

## 开战和失败策略

`BattleRuntimeModule.StartBattle*` 必须显式接收 `BattleObjectiveDefinition`。`GameRuntimeFacade` 只从 anchor 对应的正式 `BattleEncounterDefinition` 解析该目标。缺少目标时在创建 pending generation request 前立即失败，避免界面永久停留在 BattleLoading 或 battle save lock 无法释放。

暂时返回空地形的生成器仍可保留 pending 并在后续 frame 重试；“内容缺目标”和“地形尚未就绪”不是同一种失败。

## 存档边界

战斗目标运行态仍是 battle-only，不写入世界存档。此次 schema 删除了 encounter anchor 的旧 `enemy_roster_template_id`，并将 save/index 版本提升为 `15 / 4`。没有旧存档迁移、别名或 fallback；旧版本必须被严格拒绝。

## 回归入口

```bash
dotnet build magic.csproj
godot --headless -s res://tests/battle_runtime/objectives/run_battle_elimination_objective_regression.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_pending_battle_request_regression.cs
godot --headless -s res://tests/world_map/runtime/run_wild_encounter_growth_system_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```


# 虹光法球 AoE 局部裁剪设计修改文档

状态：标准地面 AoE 与特殊入口均已实现（2026-07-14）。冲锋位移/推挤复用单位穿越结算，冲锋路径步 AoE 逐格裁剪，重复攻击与连锁跳跃逐目标检查；`meteor_swarm` 明确作为垂直坠落灾害豁免水平投射屏障。

本文针对 `虹光法球` 与地面 AoE 技能的交互规则做设计修订。核心变化是：AoE 不再因为任意路径触碰屏障而整发失效，而是按最终生效地格逐格裁剪。没有越过法球边界的部分继续生效；越界的部分被当前屏障层阻挡，本次不生效。

## 一、背景

当前 `虹光法球` 由 `layered_barrier` 通用屏障系统实现：

- 技能配置：`data/configs/skills/mage_prismatic_sphere.tres`
- 七层 profile：`data/configs/barriers/prismatic_sphere.tres`
- 屏障创建与交互：`scripts/systems/battle/runtime/BattleBarrierService.cs`
- 地面技能 AoE 管线：`scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- 地面单位/地形效果：`scripts/systems/battle/runtime/BattleGroundEffectService.cs`

当前投射拦截主要以 `sourceUnit.coord -> targetCoord` 的单条线段判断是否穿过屏障。这对单体技能基本够用，但对地面 AoE 有两个问题：

1. AoE 的最终影响区域可能比目标中心点更大。
2. 玩家期望 AoE 被屏障按空间裁剪，而不是整发取消。

用户明确需求：

> AoE 要求是没有越界的部分要生效，越界了的不生效。

## 二、目标行为

### 2.1 核心规则

地面 AoE 技能先正常计算完整 `effectCoords`，再由法球对这些地格进行裁剪：

| 情况 | 结果 |
| --- | --- |
| 施法者在法球外，AoE 部分落在法球外、部分落入法球内 | 法球外地格生效，进入法球的地格不生效 |
| 施法者在法球内，AoE 部分留在法球内、部分扩到法球外 | 法球内地格生效，扩出法球的地格不生效 |
| 施法者和某个 AoE 地格都在同一个法球内部 | 该地格不算越界，生效 |
| 施法者和某个 AoE 地格都在法球外，但连线穿过法球区域 | 该地格被阻挡，不生效 |
| 施法者和某个 AoE 地格都在法球外，连线不穿过法球区域 | 该地格生效 |

这里的“越界”沿用现有 `BattleBarrierGeometryService.LineCrossesBarrierArea` 语义：只要从来源格到目标生效格的投射线段跨过屏障区域边界，就视为被屏障拦截。

### 2.2 单位命中规则

AoE 裁剪后，单位是否受影响只看其占用格是否与裁剪后的 `effectCoords` 相交：

- 大体型单位有任意占用格位于允许生效格内，则该单位被 AoE 命中。
- 如果单位只有被屏障裁掉的占用格被覆盖，则该单位不受本次 AoE 影响。
- 不再用 `targetUnit.coord` 对整只单位做一次额外屏障判定。

这能避免大体型单位被“锚点格”误判。例如单位身体一部分在法球外，一部分在法球内；AoE 只扫到外侧部分时，应该能命中。

### 2.3 地形效果规则

地形效果与单位效果使用同样的裁剪原则：

- 未越界地格可以被改变，例如高度、地表、持续区域效果。
- 越界地格不产生地形变化。
- 最终日志中的影响地格数量应使用裁剪后的生效地格，而不是原始 AoE 范围。

### 2.4 屏障破解规则

如果 AoE 技能本身是当前活动层的破解技能：

1. 只要本次 AoE 有任意地格投射越界并触碰当前活动层，就打破该层一次。
2. 本次施法中，越界地格仍然不生效。
3. 没有越界的地格照常生效。
4. 被打破的层从后续技能、移动、穿越开始失效。

不采用“同一发 AoE 先破层，然后被破层后的部分继续穿透”的模型。原因是它会引入同一技能内部的时间顺序歧义，也会让 AoE breaker 过强。

如果 AoE 技能是更深层的破解技能，但当前外层还未破：

- 越界地格被当前活动层阻挡。
- 记录“必须先处理外层”的屏障日志。
- 没有越界的地格仍然生效。

## 三、非目标

本次修改不做以下事情：

1. 不改变 `虹光法球` 的七层数据配置。
2. 不改变单位移动穿越屏障的结算规则；冲锋与冲锋推挤等旁路必须复用该规则。
3. 不改变单体技能的现有阻挡语义，除非实现时需要共享底层 helper。
4. 不引入 Godot `Area2D`、物理查询或场景树碰撞。
5. 不做真正的连续几何体碰撞；战斗系统继续使用格子坐标。
6. 不让 AoE breaker 在同一次施法中破层后继续穿透。

## 四、推荐技术路线

采用“地面 AoE 生效格裁剪”路线：

1. 地面技能先通过现有 `BuildGroundEffectCoordsTyped` 得到完整 `effectCoords`。
2. 新增屏障裁剪服务，把 `effectCoords` 裁剪成 allowed / blocked 两组。
3. 单位效果只使用 allowed unit coords。
4. 地形效果只使用 allowed terrain coords。
5. 屏障层破解、阻挡日志、blocked coord 统计由裁剪服务统一处理。
6. 所有使用地面 AoE 生效格的入口必须复用同一个裁剪步骤，不能只在玩家常规地面施法路径内临时裁剪。

该路线的优点：

- 保留现有地面技能目标校验、消耗、反噬漂移、范围收集、单位收集、地形效果管线。
- 修改点集中在 `effectCoords` 进入效果结算之前。
- AoE 与单体技能的行为边界清晰。
- 能自然支持大体型单位和地形效果。

不采用“整发 AoE 先问屏障是否阻挡”的路线，因为它无法表达部分生效。

不采用“在每个目标单位处单独问屏障”的路线，因为它会把空间裁剪退化成单位锚点判定，仍然会误伤大体型单位和地形效果。

不采用“只修改 `_handle_ground_skill_command`”的路线，因为地面 AoE 在预览、自动施法、读条完成、冲锋路径步和特殊 profile 中都有独立入口。只改主路径会导致玩家手动施法、AI/触发施法、HUD 预览和特殊技能表现不一致。

## 五、接口设计

### 5.1 新增结果类型

建议在 `BattleBarrierService.cs` 或相邻文件中新增：

```csharp
internal readonly record struct BattleBarrierCoordClipResult(
    IReadOnlyList<Vector2I> AllowedCoords,
    IReadOnlyList<Vector2I> BlockedCoords
);

internal readonly record struct BattleGroundEffectBarrierClipResult(
    BattleBarrierCoordClipResult UnitEffects,
    BattleBarrierCoordClipResult TerrainEffects,
    IReadOnlyList<Vector2I> VisibleCoords,
    bool Applied
);
```

字段含义：

| 字段 | 含义 |
| --- | --- |
| `AllowedCoords` | 本次屏障裁剪后仍允许生效的地格 |
| `BlockedCoords` | 被一个或多个屏障阻挡的地格 |
| `UnitEffects` / `TerrainEffects` | 单位效果与地形效果各自的 allowed / blocked 结果 |
| `VisibleCoords` | 两组 allowed coords 的并集，用于预览、日志与后续触发 |
| `Applied` | 屏障是否发生了可见交互，例如阻挡日志或破层 |

`BattleGroundEffectBarrierClipResult.Applied` 需要参与地面技能最终 `applied` 计算。否则当整片 AoE 都被法球挡住时，技能消耗已经发生，但命令可能被错误视为“没有应用任何东西”。

### 5.2 新增裁剪方法

建议在 `BattleBarrierService.cs` 新增：

```csharp
internal BattleGroundEffectBarrierClipResult ResolveGroundEffectBarrierClipResult(
    BattleUnitState sourceUnit,
    SkillDefinition skillDefinition,
    IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
    IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
    IReadOnlyList<Vector2I> effectCoords,
    BattleEventBatch batch
)
```

职责：

1. 接收完整 AoE 生效格。
2. 按当前所有 layered barrier 逐个裁剪。
3. 对越界地格应用当前屏障层的阻挡/破解逻辑。
4. 返回裁剪后的 allowed coords 与 blocked coords。
5. 聚合日志，避免每个地格输出一行。

该方法只负责“投射效果是否能到达该地格”，不负责实际伤害、状态、地形变化。

### 5.3 Orchestrator 内部统一裁剪上下文

建议在 `BattleSkillExecutionOrchestrator.cs` 内新增一个内部结果类型，用于统一常规执行、自动施法、读条完成和预览使用的裁剪逻辑：

```csharp
private readonly record struct GroundEffectBarrierClipContext(
    IReadOnlyList<CombatEffectDefinition> UnitEffectDefinitions,
    IReadOnlyList<CombatEffectDefinition> TerrainEffectDefinitions,
    IReadOnlyList<Vector2I> RawEffectCoords,
    IReadOnlyList<Vector2I> UnitEffectCoords,
    IReadOnlyList<Vector2I> TerrainEffectCoords,
    IReadOnlyList<Vector2I> VisibleEffectCoords,
    bool BarrierApplied
);
```

建议封装方法：

```csharp
private GroundEffectBarrierClipContext ResolveGroundEffectCoordsAfterBarrierClip(
    BattleUnitState sourceUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<Vector2I> targetCoords,
    BattleEventBatch batch
)
```

另提供只读预览重载：

```csharp
private GroundEffectBarrierClipContext ResolveGroundEffectCoordsAfterBarrierClip(
    BattleUnitReadView sourceUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<Vector2I> targetCoords
)
```

职责：

1. 调用 `BuildGroundEffectCoordsTyped` 得到 `RawEffectCoords`。
2. 收集 unit effect definitions 与 terrain effect definitions。
3. 一次调用 `BattleBarrierService.ResolveGroundEffectBarrierClipResult`，由它分别裁剪 unit / terrain 两组效果。
4. 返回 unit / terrain 的裁剪后地格。
5. 计算 `VisibleEffectCoords`，即 unit / terrain allowed coords 的并集，用于日志、预览和 AI 命中范围。

如果预览路径不能安全地产生破层日志或写入屏障状态，预览版本必须使用只读裁剪，不得调用会改变屏障层状态的 `_BreakActiveLayer`。预览只展示“哪些格会被挡”，不应改变战斗状态。

### 5.4 几何 helper

建议在 `BattleBarrierGeometryService.cs` 保留现有方法，并新增一个更直接的包装：

```csharp
internal static bool ProjectedCoordCrossesBarrierArea(
    Vector2I sourceCoord,
    Vector2I effectCoord,
    IReadOnlyCollection<Vector2I> barrierCoords
)
```

内部可以直接调用现有 `LineCrossesBarrierArea`。

后续如果要升级为 source footprint 到 effect coord 的判定，可以在该 helper 内扩展，而不影响上层调用。

## 六、运行流程修改

### 6.1 当前流程

`BattleSkillExecutionOrchestrator._handle_ground_skill_command` 当前核心流程：

1. 校验地面技能命令。
2. 消耗 AP/MP。
3. 处理施法反噬与目标漂移。
4. 调用 `BuildGroundEffectCoordsTyped` 得到 `effectCoords`。
5. 调用 `ApplyGroundUnitEffectsResultTyped`。
6. 调用 `ApplyGroundTerrainEffectsResultTyped`。
7. 根据单位/地形结果写最终日志。

### 6.2 入口覆盖要求

地面 AoE 屏障裁剪必须覆盖所有会计算和消费 AoE 生效格的入口：

| 入口 | 代码位置 | 裁剪要求 |
| --- | --- | --- |
| 常规地面技能执行 | `BattleSkillExecutionOrchestrator._handle_ground_skill_command` | 必须使用统一裁剪上下文 |
| 地面技能预览 | `BattleSkillExecutionOrchestrator._preview_ground_skill_command_impl` | 必须使用只读裁剪上下文，预览显示裁剪后范围 |
| 自动地面技能 | `BattleSkillExecutionOrchestrator.ExecuteAutoGroundSkill` | 必须使用统一裁剪上下文 |
| 读条/挂起地面施法完成 | `BattleSkillExecutionOrchestrator.ResolvePendingGroundCast` | 必须使用统一裁剪上下文 |
| 冲锋位移与推挤 | `BattleChargeResolver` | 每个实际位移步骤在移动前调用统一单位边界穿越结算 |
| 冲锋路径步 AoE | `BattleChargeResolver.ApplyChargePathStepAoeEffects` | 每个路径锚点直接调用同一屏障裁剪服务；预览使用指定锚点的只读裁剪 |
| 重复攻击 | `BattleRepeatAttackResolver.ApplyRepeatAttackSkillResult` | 在共享 resolver 内做一次目标屏障检查，覆盖手动、自动、随机链与 pending cast |
| 连锁伤害 | `BattleSkillExecutionOrchestrator._apply_chain_damage_effects` | 保留每一跳的实际来源单位，并以该跳来源格检查次级目标 |
| 陨石术 | `BattleMeteorSwarmResolver` / `BattleBarrierService` | `meteor_swarm` 是从上方坠落的灾害，明确豁免水平投射屏障，不裁剪影响范围 |

常规执行、自动施法、读条完成和预览都位于 `BattleSkillExecutionOrchestrator`，通过同一个 `ResolveGroundEffectCoordsAfterBarrierClip` 封装接入。冲锋路径步 AoE 不在该管线中，因此直接复用 `BattleBarrierService.ResolveGroundEffectBarrierClipResult`；陨石术的豁免由屏障服务按 special profile 显式表达，即使未来误接入普通投射检查也不会被裁剪。

### 6.3 修改后常规执行流程

修改后流程：

1. 校验地面技能命令。
2. 消耗 AP/MP。
3. 处理施法反噬与目标漂移。
4. 调用 `BuildGroundEffectCoordsTyped` 得到 `rawEffectCoords`。
5. 收集 unit effect definitions。
6. 收集 terrain effect definitions。
7. 分别执行屏障裁剪：
   - `unitEffectCoords = Clip(rawEffectCoords, unitEffectDefinitions)`
   - `terrainEffectCoords = Clip(rawEffectCoords, terrainEffectDefinitions)`
8. `ApplyGroundUnitEffectsResultTyped` 使用 `unitEffectCoords`。
9. `ApplyGroundTerrainEffectsResultTyped` 使用 `terrainEffectCoords`。
10. 最终 `applied` 包含 unit result、terrain result、barrier clip result。
11. 最终影响地格数量使用 `unitEffectCoords` 与 `terrainEffectCoords` 的并集。

unit / terrain 分开裁剪的原因：

- 当前虹光法球 `catch_all_projected_effects = false`，只有命中任一未破坏色层 `blocked_categories` 的投射类别才会被挡。
- 但通用屏障系统可能出现只挡法术单位效果、不挡地形效果，或只挡某类地形投射的 profile。
- 分开裁剪可以避免 effect category 被另一组效果污染。

### 6.4 预览流程要求

`_preview_ground_skill_command_impl` 当前也会根据目标坐标构造预览地格。修改后预览必须展示裁剪后的 `VisibleEffectCoords`：

1. 校验目标。
2. 计算原始 preview/effect coords。
3. 使用只读屏障裁剪。
4. `preview.SetTargetCoords` 或等价调用只接收裁剪后的可生效地格。
5. 对被屏障阻挡的地格，可选地在预览日志中增加一条摘要，例如“虹光法球将阻挡 5 个地格”。

预览不得产生以下副作用：

- 不消耗资源。
- 不破坏屏障层。
- 不写 battle log。
- 不改变 `BattleState.LayeredBarrierStore`。

### 6.5 自动施法与读条完成要求

`ExecuteAutoGroundSkill` 与 `ResolvePendingGroundCast` 应与 `_handle_ground_skill_command` 使用同一裁剪上下文。差异只允许存在于各自已有的前置流程，例如自动施法的命令来源、读条技能的 pending cast 数据来源。

这两条路径的最终单位效果、地形效果、Contingency payload 和日志数量必须与同一目标下的常规地面施法保持一致。

### 6.6 特殊入口最终规则

- 冲锋者自身以及被冲锋侧推/前推的单位，都在每次实际位移前调用 `ResolveUnitBoundaryCrossingResult`；色层使单位停止、死亡或改变位置时，不再提交该步移动。
- 冲锋路径步 AoE 属于会被虹光法球阻挡的投射范围效果。在 `BuildChargeStepEffectCoords` 后、`CollectUnitsInCoords` 前调用 `ResolveGroundEffectBarrierClipResult`；预览以每个路径锚点为效果起点调用 `PreviewGroundEffectBarrierClipResultAtCoord`，不得写屏障状态。
- 重复攻击在 `BattleRepeatAttackResolver` 的共享入口检查屏障，避免手动、自动、随机攻击链与读条完成各自复制规则。
- 连锁伤害不是始终从原施法者投射。收集次级目标时必须保存 `origin -> target` 跳跃，以该跳实际来源格调用 `ResolveSkillBarrierInteractionFromCoordResult`，日志与伤害归因仍保留原施法者和原技能。
- `meteor_swarm` 被定义为从天外垂直坠落的灾害，不受水平投射屏障裁剪。`BattleBarrierService` 按 `SpecialResolutionProfileId == meteor_swarm` 显式放行投射与地格裁剪，预览和执行都保留完整落点计划，且不会破坏或推进法球色层。

### 6.7 地面单位效果服务契约调整

`BattleGroundEffectService._apply_ground_unit_effects_result` 当前会在收集到目标单位后再次调用 `ResolveSkillBarrierInteractionResult`。

修改后该方法的契约应调整为：

> 传入的 `effectCoords` 已经是最终允许生效的地格，地面单位效果服务不再执行投射屏障判定。

因此应移除或绕过以下 per-target 屏障判断：

- 普通地面单位效果中的 `ResolveSkillBarrierInteractionResult`
- 风推目标收集中的 `ResolveSkillBarrierInteractionResult`

理由：

- AoE 屏障判断必须按地格，不应按单位锚点。
- 前置裁剪后，`CollectUnitsInCoords(effectCoords)` 已经能正确处理大体型单位。
- 重复 per-target 判定会重新引入整只单位被挡的问题。

## 七、裁剪算法

### 7.1 单个屏障裁剪

对一个屏障，按以下步骤处理：

1. 读取屏障区域 `barrierCoords`。
2. 找到当前活动层 `activeLayer`。
3. 解析本组 effects 的投射 categories。
4. 对每个 `effectCoord`：
   1. 如果 `sourceUnit.coord -> effectCoord` 不穿过屏障区域，保留。
   2. 如果穿过，进入屏障交互逻辑。
   3. 如果技能破解当前活动层，记录本层需要破除，当前 coord 阻挡。
   4. 如果技能破解更深层但不是当前层，当前 coord 阻挡，记录外层阻挡日志。
   5. 如果 categories 命中任一未破层的 `blocked_categories`，当前 coord 阻挡。
   6. 如果未命中任何未破坏色层的 categories，则当前 coord 穿透；虹光法球不使用 catch-all 兜底。
   7. 否则保留。
5. 如果本次触发当前层破解，对该屏障只破一次。
6. 聚合本屏障阻挡地格数量并输出一条日志。

### 7.2 多个屏障裁剪

如果战场上有多个 layered barrier：

1. 按 `_SortedBarrierKeys()` 的既有顺序处理。
2. 前一个屏障的 allowed coords 作为下一个屏障的输入。
3. blocked coords 累积。
4. 任一屏障产生阻挡或破层，`Applied = true`。

这样可以表达多个屏障重叠时的保守阻挡效果。

### 7.3 日志策略

不按每个格子输出日志，避免 AoE 大范围时刷屏。

推荐日志：

- 阻挡普通 AoE：
  - `虹光法球的 红色层 阻挡了 火球术 的 5 个地格。`
- breaker 破层：
  - `寒冰锥 击碎了虹光法球的 红色层，5 个越界地格被本次屏障阻挡。`
- 深层 breaker 被外层挡住：
  - `奥术飞弹 试图破解虹光法球，但必须先处理外层 红色层，3 个越界地格被阻挡。`

日志内容应包含：

- 技能显示名。
- 屏障名称。
- 层名称。
- 被阻挡地格数量。

## 八、与现有系统的交互

### 8.1 施法消耗

施法消耗仍发生在地面技能管线中，不因裁剪回滚。

如果所有地格都被屏障阻挡：

- AP/MP 已消耗。
- 屏障日志输出。
- 技能命令应视为 applied，因为屏障发生了可见交互。

### 8.2 施法反噬

反噬目标漂移应在裁剪之前发生。

顺序：

1. 原始目标。
2. 反噬可能改变 target coords。
3. 根据漂移后的 target coords 计算 raw effect coords。
4. 屏障裁剪 raw effect coords。

这样屏障裁剪永远作用于最终实际投射区域。

### 8.3 Contingency 触发

`EmitContingencySpellAffected` 应使用裁剪后的 `effectCoords`。

原因：

- 被屏障阻挡的单位不应触发“受到该法术影响”。
- 被屏障阻挡的地格不应进入 affected coord payload。

如果所有地格都被屏障阻挡，则不应发出普通 spell affected 事件；屏障日志已经表达本次交互。

### 8.4 地形效果

地形效果只处理 `terrainEffectCoords`。

如果 unit effect coords 与 terrain effect coords 不同：

- 单位影响按 unit effect coords。
- 地形影响按 terrain effect coords。
- 最终日志中的地格数使用二者并集。

### 8.5 风推

风推属于地面单位效果的一种，也应基于裁剪后的 `unitEffectCoords` 收集目标。

不应在 `CollectWindPushTargetUnits` 内再次用 `targetUnit.coord` 判屏障；否则会破坏局部裁剪语义。

## 九、测试设计

建议在 `tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs` 追加以下回归。

### 9.1 AoE 外部部分生效，内部部分被挡

场景：

- 法球固定在地图中部。
- 施法者在法球外。
- 地面 AoE 覆盖若干法球外地格和若干法球内地格。
- 法球外放一个敌人，法球内放一个敌人。

期望：

- 法球外敌人受到 AoE 效果。
- 法球内敌人不受 AoE 效果。
- 日志显示虹光法球阻挡了若干地格。
- 技能最终 applied 为 true。

### 9.2 AoE 从内部向外扩散时只保留内部

场景：

- 施法者位于法球内部。
- AoE 同时覆盖法球内部和外部。

期望：

- 内部地格生效。
- 外部地格被阻挡。
- 施法者不能通过 AoE 影响法球外单位。

### 9.3 外部绕过法球的 AoE 地格不被误挡

场景：

- 施法者和 AoE 地格都在法球外。
- 部分 AoE 地格与施法者连线不穿过法球。
- 另一部分 AoE 地格与施法者连线穿过法球。

期望：

- 不穿过法球的地格生效。
- 穿过法球的地格不生效。

### 9.4 大体型单位按占用格命中

场景：

- 一个大体型单位横跨法球边界。
- AoE 只覆盖该单位位于法球外的一格。

期望：

- 单位被命中。
- 不再因为 `targetUnit.coord` 位于法球内而整只单位被挡。

反向场景：

- AoE 只覆盖该单位位于法球内且被阻挡的一格。

期望：

- 单位不被命中。

### 9.5 地形效果只改变未阻挡地格

场景：

- 使用带地形变化的地面技能。
- AoE 同时覆盖未阻挡地格和被阻挡地格。

期望：

- 未阻挡地格产生地形变化。
- 被阻挡地格保持原状。
- changed coords 不包含被阻挡地格的地形变化。

### 9.6 AoE breaker 破层但本次越界地格不生效

场景：

- 红层为当前活动层。
- 施法者使用红层 breaker AoE。
- AoE 有部分地格越过法球边界，部分地格不越界。

期望：

- 红层被标记为 broken。
- 本次越界地格不生效。
- 本次未越界地格生效。
- 下一次非 breaker 投射面对橙层，而不是红层。

### 9.7 深层 breaker 不能越级破层

场景：

- 红层仍为当前活动层。
- 施法者使用蓝层 breaker AoE。
- AoE 有越界地格。

期望：

- 红层未破。
- 蓝层未破。
- 越界地格被阻挡。
- 日志提示必须先处理外层红层。

### 9.8 预览与实际执行一致

场景：

- 对同一地面 AoE 目标先请求预览，再正式施放。
- AoE 同时覆盖被阻挡地格和未阻挡地格。

期望：

- 预览显示的目标地格等于正式执行的 allowed coords。
- 预览不破坏屏障层。
- 正式执行后才产生破层或阻挡日志。

### 9.9 自动地面技能与常规施法一致

场景：

- 使用同一施法者、同一地面技能、同一目标坐标。
- 分别走 `_handle_ground_skill_command` 与 `ExecuteAutoGroundSkill`。

期望：

- 两条路径得到相同的 allowed coords。
- 被影响单位集合一致。
- 被阻挡地格数量一致。

### 9.10 读条完成地面技能与常规施法一致

场景：

- 创建 pending ground cast。
- 读条完成时目标 AoE 横跨虹光法球边界。

期望：

- `ResolvePendingGroundCast` 使用同一裁剪规则。
- 法球内外裁剪结果与常规施法一致。

### 9.11 冲锋路径步 AoE 裁剪

场景：

- 冲锋路径步 AoE 覆盖法球边界两侧单位。
- 冲锋者或被推单位跨过法球边界。

期望：

- 路径步 AoE 只影响未被阻挡的地格内单位。
- 法球内被阻挡地格的单位不受影响。
- 预览与执行都以当前路径锚点为投射起点，且预览不改变色层。
- 冲锋者与被推单位都触发统一色层穿越结果；允许继续时再提交位移。

### 9.12 陨石术例外锁定

场景：

- 陨石落点 affected coords 覆盖虹光法球内目标。

期望：

- 预览保留法球内目标和完整落点计划。
- 执行正常影响法球内目标。
- 当前色层保持不变，也不产生投射阻挡日志。

### 9.13 重复攻击与连锁跳跃

场景：

- 分别从手动、自动、随机攻击链与 pending cast 入口发动重复攻击，目标位于法球另一侧。
- 连锁技能主目标与来源同侧，后续一跳跨入法球。

期望：

- 四种重复攻击入口都在共享 resolver 内被屏障阻挡，且屏障交互仍算有效处理。
- 连锁主目标正常受击，跨界次级目标不受伤。
- 次级跳跃的屏障日志保留原施法者与原技能归因。

## 十、实现文件清单

### 10.1 修改 `BattleBarrierGeometryService.cs`

新增投射地格 helper，复用现有 `LineCrossesBarrierArea`。

### 10.2 修改 `BattleBarrierService.cs`

新增：

- `BattleBarrierCoordClipResult` 与 `BattleGroundEffectBarrierClipResult`
- `ResolveGroundEffectBarrierClipResult`
- `PreviewGroundEffectBarrierClipResult` 与支持特殊路径锚点的 `PreviewGroundEffectBarrierClipResultAtCoord`
- 支持连锁跳跃实际起点的 `ResolveSkillBarrierInteractionFromCoordResult`
- `meteor_swarm` special profile 的显式水平投射豁免
- 私有 helper：判断单个 coord 是否被当前 barrier 阻挡。
- 私有 helper：聚合 blocked coord 日志。

复用既有逻辑：

- `_GetActiveLayer`
- `_SkillBreaksLayer`
- `_SkillBreaksAnyRemainingLayer`
- `_FindFirstBlockingLayer`
- `_BreakActiveLayer`
- `_GetBarrierCoords`
- `BattleEffectCategoryResolver.ResolveCategories`

### 10.3 修改 `BattleSkillExecutionOrchestrator.cs`

新增统一封装：

- `GroundEffectBarrierClipContext`
- `ResolveGroundEffectCoordsAfterBarrierClip`
- 只读预览版本 `ResolveGroundEffectCoordsAfterBarrierClip`

以下入口必须改为使用该封装：

- `_handle_ground_skill_command`
- `ExecuteAutoGroundSkill`
- `ResolvePendingGroundCast`
- `_preview_ground_skill_command_impl`

在每条执行路径中，都应在 `BuildGroundEffectCoordsTyped` 后插入屏障裁剪。

需要把原本 inline 收集的 unit / terrain effect definitions 提前保存到局部变量，分别裁剪后传入：

- `ApplyGroundUnitEffectsResultTyped`
- `ApplyGroundTerrainEffectsResultTyped`

最终 `applied` 需要包含屏障裁剪结果。

### 10.4 修改 `BattleGroundEffectService.cs`

调整地面单位效果服务契约：

- 删除普通地面单位效果里的 per-target 屏障判断。
- 删除风推目标收集里的 per-target 屏障判断。
- 确保 `EmitContingencySpellAffected` 使用传入的裁剪后 coords。

### 10.5 修改测试

在 `run_prismatic_sphere_regression.cs` 添加 AoE 裁剪相关测试。

如现有 helper 不适合构造地面 AoE，可在该测试文件内新增局部测试 helper，避免污染生产代码。

### 10.6 修改 `BattleChargeResolver.cs`

- 冲锋者自身与被侧推/前推单位在移动提交前调用 `ResolveUnitBoundaryCrossingResult`。
- 在 `ApplyChargePathStepAoeEffects` 中，`BuildChargeStepEffectCoords` 之后、`CollectUnitsInCoords` 之前调用 `BattleBarrierService.ResolveGroundEffectBarrierClipResult`。
- 使用裁剪后的 coords 收集目标。
- 路径预览按每个预测锚点调用 `PreviewGroundEffectBarrierClipResultAtCoord`。

### 10.7 修改重复攻击与连锁伤害所有者

- `BattleRepeatAttackResolver.ApplyRepeatAttackSkillResult` 在进入多段循环前调用统一单体投射屏障检查。
- `CollectChainDamageTargets` 返回包含实际来源与目标的跳跃记录。
- `_apply_chain_damage_effects` 在每个次级目标伤害前，以该跳来源格调用 `ResolveSkillBarrierInteractionFromCoordResult`。

### 10.8 明确 `meteor_swarm` 豁免

- `BattleBarrierService` 对 `SpecialResolutionProfileId == meteor_swarm` 的技能显式跳过水平投射和地格裁剪。
- `run_meteor_swarm_special_profile_regression.cs` 同时锁定 profile、`requires_los=false`、`meteor/disaster` 投送类别、预览目标、执行伤害和色层不变。

## 十一、风险与对策

### 11.1 风险：现有单体屏障测试被影响

对策：

- 单体技能继续使用 `ResolveSkillBarrierInteractionResult`。
- 新裁剪方法只在地面 AoE 管线调用。
- 保留现有 `TestProjectedEffectBarrierGeometryRespectsBoundary` 等测试。

### 11.2 风险：地面服务被其他入口直接调用

对策：

- 在修改前用 `rg "ApplyGroundUnitEffectsResultTyped|_apply_ground_unit_effects_result"` 确认调用点。
- 如果存在绕过 orchestrator 的调用点，必须在调用前同样接入裁剪，或给方法增加明确参数表示 coords 已裁剪。

### 11.3 风险：只改常规施法导致多路径行为不一致

对策：

- `BattleSkillExecutionOrchestrator` 内的常规执行、自动施法、读条完成和预览必须共用 `ResolveGroundEffectCoordsAfterBarrierClip`。
- 测试必须覆盖至少一个自动地面技能路径和一个读条完成路径。
- 预览必须使用只读裁剪，不能破层或写 battle log。

### 11.4 风险：日志重复

对策：

- 每个屏障、每个层、每个技能组最多输出一条阻挡日志。
- breaker 破层日志与 blocked coord 数量合并。

### 11.5 风险：unit / terrain 分开裁剪导致日志数量不一致

对策：

- 最终技能日志显示 unit/terrain allowed coords 的并集。
- 屏障日志显示按 effect group 阻挡的地格数量。
- 如果两个 group 完全相同，可以实现层面复用裁剪结果，避免重复日志。

### 11.6 风险：全部地格被挡时技能返回 false

对策：

- `BattleBarrierCoordClipResult.Applied` 必须参与最终 applied。
- 只要屏障发生阻挡或破层，本次命令就是有效交互。

### 11.7 风险：特殊入口规则再次漂移

对策：

- 冲锋位移/推挤、路径步 AoE、重复攻击和连锁跳跃都由专项回归锁定。
- 陨石术的例外同时写入屏障服务、设计文档与特殊 profile 回归，避免后续误判为遗漏。
- 新增特殊 resolver 时必须判断它属于单位穿越、水平投射、逐格 AoE，还是有明确依据的垂直灾害例外。

## 十二、验收标准

实现完成后应满足：

1. 地面 AoE 被虹光法球逐格裁剪。
2. 未越界地格继续结算单位效果与地形效果。
3. 越界地格不结算单位效果与地形效果。
4. 大体型单位按实际被允许的占用格命中。
5. 风推不再因为目标锚点而整只单位误挡。
6. Contingency 只收到实际被 AoE 影响的单位和地格。
7. AoE breaker 可以破当前活动层，但本次越界地格仍不生效。
8. 深层 breaker 不能越级破层。
9. 常规地面施法、自动地面施法、读条完成地面施法使用同一裁剪规则。
10. 地面技能预览显示裁剪后的生效范围，并且预览无副作用。
11. 冲锋自身/推挤穿越与路径步 AoE 裁剪被明确测试锁定。
12. 手动、自动、随机链与读条完成的重复攻击均不能绕过法球。
13. 连锁伤害按实际跳跃起点逐跳检查屏障。
14. `meteor_swarm` 的垂直灾害豁免在预览和执行中被明确锁定。
15. 现有虹光法球单体投射、移动穿越、七层破解顺序测试继续通过。

## 十三、推荐提交拆分

建议按以下提交顺序实现：

1. `test: cover prismatic sphere aoe clipping`
   - 先添加失败测试，覆盖外到内、内到外、大体型单位、地形效果、breaker。
2. `feat: add barrier coord clipping result`
   - 新增裁剪结果类型、几何 helper、屏障裁剪服务，并支持只读预览裁剪。
3. `feat: add shared ground aoe barrier clip context`
   - 在 `BattleSkillExecutionOrchestrator` 中新增统一裁剪上下文，供常规执行、自动施法、读条完成和预览复用。
4. `feat: clip all standard ground aoe entry points`
   - 将 `_handle_ground_skill_command`、`ExecuteAutoGroundSkill`、`ResolvePendingGroundCast` 和 `_preview_ground_skill_command_impl` 接入统一裁剪上下文。
5. `fix: remove per-target barrier checks from ground aoe effects`
   - 调整地面单位效果服务契约，移除锚点式二次屏障判断。
6. `feat: handle special aoe barrier clipping decisions`
   - 接通冲锋穿越/推挤、路径步 AoE、重复攻击和连锁跳跃，并锁定 `meteor_swarm` 垂直灾害豁免。
7. `test: preserve prismatic sphere single-target behavior`
   - 确认现有单体和移动穿越测试仍通过，必要时补单体回归。

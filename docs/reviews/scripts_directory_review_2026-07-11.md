# scripts 目录分层代码审查报告（2026-07-11）

- 审查日期：2026-07-11
- 审查范围：`scripts/` 正式代码（703 个 .cs 文件，约 28 万行，含 partial 拆分）。
- 审查目标：架构问题、BUG、隐藏玩法问题。
- 审查方式：按风险分层。`scripts/systems/battle/rules/` 数学层逐文件深读（BattleHitResolver、BattleAttackCheckPolicyService、BattleDamageResolver 主体 + Dice/Mitigation partial、BattleSaveResolver、PhantasmalKillExecutionRules 全文）；runtime 执行层、persistence、AI、settlement 经济、UI 做定向抽查（沿调用链核对，非孤立读文件）；全库做模式扫描（裸 RNG、可变静态、async void、Task 阻塞、取整方向）。
- 与 2026-06-17 逐文件索引报告的关系：本轮不重复该报告的逐文件矩阵，专注跨文件规则一致性、数值口径与经济闭环；上一轮 findings 未复查（其中 `BattleDamageResolver.Dice.cs` 的 `Call("_roll_damage_die")` 一项本轮确认已改为直接虚方法调用 `RollDamageDieVirtual` → `_roll_damage_die`，已修复）。
- 结论先行：核心命中/豁免/伤害管线的**预览与执行一致性**质量很高（fate 口径逐枚举概率与执行顺序一致），持久化原子写扎实；实际问题集中在**双路径判定不对称、数据校验缺口、以及 State/ReadView 双份实现的维护风险**。所有发现均未修复，仅记录。

## 高优先级发现（玩法/规则）

### [高] 暴击刷"技能骰极值"精通：技能骰与武器骰判定不对称

- 现象：暴击时 `BuildDamageDiceEventFlags` 无条件强制 `SkillDamageDiceIsMax = true`（reason=CriticalHit，`scripts/systems/battle/rules/BattleDamageResolver.Dice.cs:391`）。
- 精通侧消费：`scripts/systems/battle/runtime/BattleSkillMasteryService.cs:480` 的 `_ResultHasSkillDamageDieEvent` **不过滤 reason**，任何 `SkillDamageDiceIsMax=true` 都算数；而同文件 `:492` 的 `_ResultHasWeaponDiceMaxEvent` 明确要求 `reason == DamageDiceMaxReasonKind.WeaponDiceMax`，把暴击原因排除在外。
- 后果：`CombatSkillMasteryTriggerMode.SkillDamageDiceMax`（含 default 分支，`BattleSkillMasteryService.cs:407-414`）的精通在**每次暴击**都触发。高幸运角色（crit gate die、高位大成功阈值）精通增速远超"骰出极值"这一小概率事件的设计预期；武器系同类精通却不受益。两侧必有一侧不符合意图。
- 修复方向二选一：技能骰判定补 `reason == SkillDiceMax` 过滤（对齐武器骰）；或明确"暴击视同极值"的设计并让武器骰同样放行，同时更新文档。

### [高·潜在] 商店套利缺口：买价吃折扣、卖价不吃，且无比价校验

- 买价：`scripts/systems/settlement/SettlementShopService.cs:557` `ResolveBuyPrice(itemDef, source.PriceBasisPoints)`，折扣经 `ItemDef.ApplyPriceBasisPoints`（`scripts/player/warehouse/ItemDef.cs:374`）。数据中实际存在折扣商店：`SettlementShopService.cs:86/140/141` 有 9500/9000 bp 条目。
- 卖价：`SettlementShopService.cs:629` `ResolveSellPrice(itemDef)` 走无参 `GetSellPrice()`，**恒按 10000 bp**，不吃任何折扣/溢价。
- 校验缺口：`scripts/player/warehouse/ItemContentRegistry.cs:340-353` 只要求 sellable 物品显式声明 buy/sell 价格，**没有 `sell_price < buy_price × 最低折扣` 的规则**。
- 现状：扫描全部 `data/configs/**/*.tres`，当前最高 sell/buy 比价为 0.5，未触发。但任何一件新物品配到 `sell ≥ 0.9 × buy`（或未来出现更深折扣商店）即形成买→卖无限金币循环。
- 修复方向：在 ItemContentRegistry 校验加比价规则（阈值取全库最深折扣），或让卖价同样应用 basis points。

### [高] 黑星烙印 -3 攻击惩罚可被任意正面攻击加成状态覆盖

- 位置：`scripts/systems/battle/rules/BattleHitResolver.cs:855-881`（State 版）与 `:883-907`（ReadView 版，两份实现行为一致）。
- 逻辑：单位带 `black_star_brand_elite` 时先置 `attackDelta = -3`；随后遍历所有状态执行 `attackDelta = Math.Max(attackDelta, statusEntry.attack_roll_bonus)`。任何 `attack_roll_bonus > 0` 的状态（哪怕 +1）都会把 -3 整个吞掉——-3 与 +2 并存的结果是 +2 而非 -1。
- 后果：精英烙印的命中惩罚在目标叠了任意攻击 buff 时形同虚设。若设计是"取最大加成、惩罚另算"，应把烙印惩罚移到 penalty 通道（`_get_attacker_status_attack_penalty`）而非与加成同池取 Max。

### [中] 连击衰减倍率被静默钳成 1.0

- 位置：`scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs:31-34`，`follow_up_damage_multiplier` 解析时 `Math.Max(GetFloat(...), 1.0)`。
- 下游 `_build_repeat_attack_stage_effects`（`:978-985`）只在 `damage_multiplier > 1.0` 时应用倍率，与钳制自洽——但内容作者配置 0.5 衰减连击时**无任何报错**，按 1.0 全额伤害执行。
- 修复方向：若设计只允许递增，在内容校验层拒绝 <1.0 的配置；若允许衰减，去掉钳制（`> 1.0` 判断同步改为 `!= 1.0`）。

### [中] 攻击修正堆叠：同 stack_key 正负同现时整组丢弃

- 位置：`scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs:920-923`，`ResolveStackGroup` 检测到组内既有 bonus 又有 penalty 时直接 `return null`。
- 后果：两个修正**全部蒸发**（而非相抵取净值），且 UI 的 modifier breakdown 中也不出现，玩家无从得知。同 key 正负同现虽罕见（通常同源同号），一旦发生就是静默数值错误。
- 修复方向：至少在 breakdown 里保留"冲突已取消"条目；或改为求净值。

### [中] 豁免标签 forcedMode 覆盖后缀语义（数据卫生陷阱）

- 位置：`scripts/systems/battle/rules/BattleSaveResolver.cs:513-524`，`ResolveSaveTagMode` 在 forcedMode 非空时，`{saveTag}_disadvantage`、`{saveTag}_immunity` 等后缀值一律返回 forcedMode。
- 后果：状态若把 `poison_disadvantage` 误写进 `save_advantage_tags` 字段，会被解析成**优势**——写错字段不报错，只静默反转效果。
- 修复方向：forced 路径下检测到后缀与 forcedMode 冲突时报内容校验错误（或至少 GameLog 警告）。

### [低] 一次性遗物预览失真

- `BattleUnitState.clone()`（`scripts/systems/battle/core/BattleUnitState.cs:1260`）不复制 `ai_blackboard`，克隆体拿字段初始化器的全新黑板。伤害预览（`BattleDamageResolver.PreviewDamageEffectTyped`，`:1199-1200` 先 clone 再结算）因此不会污染真实单位的一次性标记——这是对的；代价是逆命护符/黑星楔**已用过后预览仍按可用显示**（克隆黑板 `low_luck_*_used` 恒为 false）。
- 同理确认健康：`ApplyBlackStarBrandGuardIgnore`（`BattleDamageResolver.Mitigation.cs:410`）在结算中 Erase 状态、`ApplyLowLuckBlackStarWedgeGuardIgnore`（`:427`）置 used 标记，预览路径均只作用于克隆。

### [低] PhantasmalKill 档位概率独立取整

- `scripts/systems/battle/rules/PhantasmalKillExecutionRules.cs:453-460`：五档 basis points 各自 `Math.Round`，总和可能 ≠ 10000（纯显示层影响）。

## 架构发现

### [高] State/ReadView 全套手写双份重载（附合并设计）

- `BattleHitResolver`、`BattleAttackCheckPolicyService`、`BattleSkillExecutionOrchestrator` 等核心规则文件中，几乎每个函数都存在 `BattleUnitState` 与 `BattleUnitReadView` 两份几乎逐行相同的实现（例：`_get_attacker_status_attack_bonus_delta` ×2、`_get_target_armor_class` ×2、`BuildSkillDefinitionAttackCheck` ×2、`CollectStatusModifierCandidates` ×2）。
- 风险：任何规则改动必须同步两处，漏一处即形成 State 路径与 ReadView 路径的**规则分叉**（本报告第 3 条这类修改尤其危险）。上面所有发现的修复都要记得改两份。
- 已确认漂移实例：`BattleAttackCheckPolicyService.ResolveDistance` State 版走 `BattleGridDistanceService.GetDistanceBetweenUnits`（`:1352`），ReadView 版手循环 occupied coords 且空集时返回 999999（`:1361-1379`）——双份实现已经在分叉。

**合并设计（推荐方案：ReadView 作为规则层唯一入参 + 隐式转换）**

前提事实：`BattleUnitReadView`（`scripts/systems/battle/core/BattleStateReadView.cs:60`）不是快照，是包着同一个 `BattleUnitState` 引用的只读 `readonly struct`。两条路径读同一对象，重复是纯意外复杂度。且成对重复的**全部是纯计算函数**（AC/攻击 delta/预览/修正收集），带副作用的执行函数（`ResolveAttackMetadata`、`ApplyBlackStarBrandGuardIgnore`、`ConsumeSkillCosts`）只有 State 版——重复边界恰好就是纯/不纯边界，合并即把这条边界显式化：

1. `BattleUnitState` 加 `public static implicit operator BattleUnitReadView(BattleUnitState unit) => new(unit);`。struct 单引用字段，转换零分配；`new BattleUnitReadView(null).IsValid == false` 与现有 `unit == null` 守卫语义一致。所有传 State 的调用点自动兼容 ReadView 签名，State 版重载可整对删除、调用方零改动。
2. 纯规则函数只保留 ReadView 签名；执行层继续持有 State 做变更，调用纯 helper 时隐式降级为视图。迁移完成后删除 `UnsafeUnitForReadOnlyRules` 后门。
3. 两个阻塞点：
   - `RefreshFootprint()` 差异（State 版调用、ReadView 版不调）：这是幂等缓存归一化而非语义写操作。让 `ReadView.GetOccupiedCoords()/OccupiesCoord()` 内部先 `RefreshFootprint()` 并文档化"视图保证归一化读"——差异消失，且修掉 ReadView 路径今天"赌调用前有人刷过"的隐患。
   - view 表面积缺口：迁移中按需补（`GetStatus(StringName)→BattleStatusReadView`、StatusReadView 补 `save_bonus`/`passive_reduction`/`guard_block` 等）。Save/Mitigation 等本无重复的 State-only 函数不动。
4. 不采用 `IBattleUnitCombatView` 接口方案的理由：热路径装箱/泛型虚分发开销；State 直接实现只读接口后一次向下转型即破坏纯度边界。包装 struct + 隐式转换零开销且物理挡住变更入口。`BattleAiUnitSnapshot` 是真快照，不参与合并。
5. 迁移步骤（每步独立提交可回归）：① 加隐式转换 + 补 view 成员（纯增量零行为变化）→ ② 按文件逐对删 State 版，**每删一对先 diff 两份实现并裁决分歧**（每个分歧点都是潜在活 bug，如上 ResolveDistance）→ ③ 每文件迁完跑对应 preview-vs-execution 回归 → ④ CI 加 grep 护栏：禁止 `battle/rules` 下非 `*Execution*/*Apply*` 类新增 `BattleUnitState` 参数。
6. 规模：重复对约 30–40 组（HitResolver ~15、PolicyService ~8、Orchestrator/SkillTurnResolver 若干）。第 ① 步后删除是机械操作，风险集中在第 ② 步分歧裁决。

**实施进度（2026-07-11 当日第一批已落地）**

- ✅ 步骤①：`BattleStateReadView.cs` 加 `implicit operator BattleUnitReadView(BattleUnitState)`（声明在 view 侧规避可见性冲突）；`GetOccupiedCoords/GetMovementTags/GetSortedStatusEffectIds` 改零拷贝；补 `GetStatus(StringName)`。
- ✅ 步骤②：`BattleAttackCheckPolicyContext` 收敛——`attacker/target` State setter 同步归一化 footprint 并填充 `*_view`，视图为唯一权威轨道，State 引用仅保留给装备能力运行时逃逸；`HasReadView` 已删除。
- ✅ 步骤③（rules 层）：`BattleHitResolver` 16 对、`BattleAttackCheckPolicyService` 8 对 State 版重载已删除，全部逐对 diff 裁决为等价（含 `AttributeSnapshot.GetValue(missing)==0`、`IsAttackDisadvantage` 两重载 taunt 空 source 边界殊途同归）；`ResolveDistance` 分歧按"State 版先刷 footprint"语义收敛到 `BuildContext(State)` 入口统一刷新。死代码 `_unit_has_lock_dodge_bonus_status`、`ResolveStatusModifierLabel(State)` 一并清除。
- ⏳ 未做（下一批次）：Orchestrator 簇（`_can_skill_target_unit` 对、`:2655-2920` 两个整块重复的执行段）与 `BattleRuntimeSkillTurnResolver` 的 `GetSkillCastBlockReason/GetSkillCommandBlockReason` 对——这批级联进 `BattleRuntimeModule`/`BattleGridService` 的下层配对，应作为独立提交。
- ⏳ 回归验证被并行的 SaveSerializer 改动（`BuildSavePayload` 改名进行中）阻塞；battle 层编译零错误已确认（全部剩余错误位于 `tests\` persistence/world_map，与本次改动无关）。
- 后门 `UnsafeUnitForReadOnlyRules` 保留：`BattleEquipmentAbilityRuntimeService.cs:7255/7262` 是合法不纯消费方，待装备能力层重构时再收。

### [中] SaveVersion 严格相等、无迁移路径

- `scripts/systems/persistence/SaveSerializer.cs:112-114`（及 `:280-281`）：payload version `!= _save_version` 一律拒绝。当前 SaveVersion=12。
- 后果：每次 bump，玩家旧档全部被 autoload 视为坏档跳过（`GameSession.cs:1314` skip_bad_candidate 路径）。开发期可接受，发布前必须补迁移链或至少给用户明确提示。

### [中] dynamic + 空 catch 的弱类型残留 helper

- `scripts/systems/battle/rules/BattleHitResolver.cs:2249-2273`（`ToInt`）、`:2275-2296`（`TryGetValue`）：`dynamic` 调用 + 空 `catch` 吞异常，数据类型错误静默降级为 fallback。与 memory 中".Call 残留已清零"结论不冲突（口径不同），建议同样清理为 Variant 类型判断。

### [低] 死代码与残留

- `BattleHitResolver.ResolveRepeatAttackStageHit`（`BattleHitResolver.cs:42`）：普通阈值掷骰口径，正式代码无调用方（真实连击执行走 `BattleAttackCheckPolicyService` + fate 口径），属死代码候选。
- `scripts/systems/persistence/GameSession.cs:1324`：`return attemptedCandidate ? false : false;` 无意义三元，重构残留。

### [低] 巨型文件

- `BattleEquipmentAbilityRuntimeService.cs` 7369 行、`GameRuntimeSettlementCommandHandler.cs` 4805 行、`BattleSkillExecutionOrchestrator.cs` 4747 行。已到"任何改动难以 review"的体量。注意：`GameRuntimeFacade` 体量结论见 memory（四种调用路径，agent 报告需打折），不在此重复。

## 已验证为健康的链路（后续审查不必重复）

- **命中预览 = 执行**：fate 口径成功率用逐枚举（`_compute_fate_attack_success_rate_basis_points`，含门骰先行的复合概率 `gate + (1-gate)×d20`，`BattleHitResolver.cs:1927-1962`）与 `ResolveAttackMetadata` 的执行顺序（门骰→hit roll→fumble→高位暴击→阈值）一致；真实伤害结算入口 `BattleDamageResolver.cs:684` 确认走 fate 口径。
- **豁免预览 = 执行**：`BattleSaveResolver` 概率估算与掷骰同用 `DoesNaturalSaveRollSucceed`（天然 1 必败/20 必成，`:661-677`）；degree 升降级规则一致（`:273-293`）。
- **存档原子性**：`FileIOCoordinator` tmp→backup→rename、失败回滚（`scripts/systems/persistence/FileIOCoordinator.cs:16-191`）。
- **技能资源闭环**：validation 层全查 AP/MP/耐力/灵气 + 锁资源 + 冷却（`BattleRuntimeSkillTurnResolver.GetSkillCastBlockReason`，`:276-387`）；`SetCurrentAp/Mp/Stamina/Aura` 均钳 0（`BattleUnitState.cs:396-414`）。"validation 拒绝不扣费、执行期失败扣费"符合既定设计（见 memory）。
- **AI 评估无污染**：MoveToRangeAction 屏蔽评估的棋盘占位改动有 try/finally 还原（`scripts/enemies/actions/MoveToRangeAction.cs:1470-1516`）；MutationGuard 快照/校验/还原机制在位。
- **UI 揭示竞态**：`BattleMapPanel._complete_battle_reveal_async` 每个 await 后有 reveal ticket 校验，headless 分支保留 frame_post_draw 回退（`scripts/ui/BattleMapPanel.cs:668-719`，对应 memory 中 frame_post_draw 根因）。
- **全库模式扫描无命中**：无裸 `new Random()`、无可变静态字段、无 `Task.Result/.Wait()` 阻塞（命中均为自定义属性名）、`async void` 仅两处 Godot 信号处理器（惯例内）。

## 覆盖率与后续批次建议

| 层 | 深度 | 说明 |
| --- | --- | --- |
| battle/rules | 深读 | 核心文件全文 + 调用链核对 |
| battle/runtime | 定向抽查 | 编排器成本链、连击、精通服务；GroundEffectService/ChargeResolver/MeteorSwarm 未全文深读 |
| persistence | 定向抽查 | GameSession 生命周期/写盘链路/版本校验；SaveSerializer 字段级映射未逐行 |
| battle/ai + enemies | 定向抽查 | MutationGuard 结构、MoveToRange 还原纪律；评分公式质量未逐项 |
| settlement/经济 | 定向抽查 | 商店买卖闭环 + 数据比价扫描 |
| progression/player | 浅 | 仅 ItemDef/ItemContentRegistry 价格链；SkillContentRegistry(3491)、EquipmentAbilityContentRegistry(3875)、CharacterManagementModule(2491) 未深读 |
| ui/utils | 抽查 | BattleMapPanel 揭示流程；其余未读 |

下一批次建议优先：`EquipmentAbilityContentRegistry` + `BattleEquipmentAbilityRuntimeService`（体量最大、与伤害管线耦合最深、本轮多个发现的修正通道都经过它）；其次 `BattleGroundEffectService` 全文与 progression 升级/加点公式。

## 未修复项索引（供修复批次直接引用）

1. 技能骰精通暴击不对称 — `BattleSkillMasteryService.cs:480` vs `:492`
2. 商店套利缺口（潜在）— `SettlementShopService.cs:629` + `ItemContentRegistry.cs:340`
3. 黑星烙印惩罚被 Max 覆盖 — `BattleHitResolver.cs:855`（两份实现都要改）
4. 连击衰减倍率静默钳升 — `BattleRepeatAttackResolver.cs:31`
5. 修正堆叠正负同现整组丢弃 — `BattleAttackCheckPolicyService.cs:920`
6. 豁免标签 forcedMode 覆盖后缀 — `BattleSaveResolver.cs:513`
7. 一次性遗物预览失真 — `BattleUnitState.cs:1260`（clone 不带黑板）
8. SaveVersion 无迁移 — `SaveSerializer.cs:112`
9. dynamic 吞异常 helper — `BattleHitResolver.cs:2249`
10. 死代码/残留 — `BattleHitResolver.cs:42`、`GameSession.cs:1324`

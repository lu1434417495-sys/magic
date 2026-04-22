
# 命运的怜悯与黑冕诅咒 · 命运系统 v2 方案

更新日期：`2026-04-20`

> 本文是在 v1《命运的怜悯 · 幸运值系统》基础上的重写版。
> v2 的核心变化有三项：
> 1. luck 不再只是“独立 crit 子系统”，而是正式介入命中主流程；
> 2. 命运神灵扩展为 **Fortuna / Misfortune** 双神，对应“随机上限”和“确定性下限”两条路线；
> 3. low luck 的平衡不再依赖把 luck 补回去，而改用 **固定成长、特殊剧情、另类战术权** 来补偿。

---

## Summary

- luck 从“主角独占的独立暴击子系统”升级为“角色级命运属性 + 命中主流程判定”。
- 每个可招募角色都允许拥有 `hidden_luck_at_birth`，但**主角由建卡 reroll 决定，队友默认 0，仅能通过模板或重大剧情事件改写**。
- 最终参与命运规则判定的 `effective_luck` 为：
  - `hidden_luck_at_birth`：出生刻印，范围 `-6 ~ +2`，不可变
  - `faith_luck_bonus`：神恩修正，范围 `0 ~ +5`
  - `effective_luck = hidden_luck_at_birth + faith_luck_bonus`，范围 `-6 ~ +7`
- **正 luck 的战斗收益**不再线性拉爆，而是用“高位大成功威胁区间”软上限处理。
- **负 luck 的战斗惩罚**分成两部分：
  - 大成功门骰变大，导致神迹极稀有
  - 命中 d20 的低位区间扩张，导致大失败更常见
- 大成功不再与命中独立：
  - 当 `crit_gate_die > d20` 时，先过“大成功门”，失败后再投正常命中
  - 当 `crit_gate_die == d20` 时，高幸运使用命中骰高位区间直接判定大成功
- 大失败也不再使用独立门骰，而是直接属于命中判定本身：
  - `>= -4 / -5 / -6` 分别对应命中骰 `1 / 1-2 / 1-3` 自动大失败
- 劣势统一采用“**两次投骰取小**”：
  - 对 `crit_gate_die` 生效
  - 对普通命中 d20 生效
- **命运的怜悯**只在劣势下对 `effective_luck <= -5` 生效：把大成功门骰缩小一档。
- Fortuna 不再是“每周目稳定可进入”的系统功能，而是**极稀有命运事件**：
  - 只在对 `elite / boss` 触发 `critical_success_under_disadvantage` 后，完成一次同尺寸二次确认时才获得 `fortune_marked`
  - 不存在保底剧情、弱福兆、累计阈值保底
- Misfortune 的定位是与 Fortuna 对标的另一条主神路线：
  - **Fortuna = 神迹 + 随机成长**
  - **Misfortune = boss 控制 + 固定成长**
- 为避免高 luck 把随机掉落打穿，掉落稀有度只读取：
  - `drop_luck = clamp(drop_bearer.effective_luck, -6, +5)`
  - `+6 / +7` 仍有战斗价值，但不继续推高随机掉落档位
- low luck 的平衡原则不是“把 luck 补回去”，而是给出：
  - 特殊剧情池
  - 固定奖励池
  - 失败前行（fail-forward）战术权

---

## 当前仓库事实

- `UnitBaseAttributes.custom_stats` 已支持任意 `StringName -> int` 的永久存储，可直接承载：
  - `hidden_luck_at_birth`
  - `faith_luck_bonus`
  - `fortune_marked`
  - `doom_marked`
  - `doom_authority`
- 装备基础设施完整（`equipment_instance_state.gd`、`equipment_rules.gd`、`equipment_state.gd`），但仍**没有 rarity tier、drop table、drop service 与掉落触发流程**
- `battle_damage_resolver.gd` 承载伤害结算；`battle_runtime_module.gd` 承载战斗流程；命中/crit/fumble 新版公式都应在此接入
- Faith v1 方案已定义统一的 `FaithDeityDef / FaithRankDef / FaithService` 管线，v2 仍复用，不新增第三套奖励框架
- `AttributeService.apply_permanent_attribute_change()` 已支持写入 `custom_stats`，可继续承载神恩型永久成长
- `main_character_member_id` 已落地，但 v2 不再把命运系统限定为“主角唯一拥有”；它主要承担：
  - 建卡 reroll 输入
  - 默认掉落承担者 fallback
- 建卡 reroll 计数器与 `hidden_luck_at_birth` 烘焙逻辑仍由 `CharacterCreationService` 负责

---

## 目标与非目标

### 目标

- 让建卡 reroll 的代价在整个周目中长期可感，但不把 low luck 做成“无法游玩”
- 让 luck 正式成为命中主流程的一部分，而不是外挂暴击脚本
- 做成**非常规战棋**：允许命运、失败、坏运、神迹进入战斗节奏，而不是只做“干净公平”的传统战棋
- 让 Fortuna 与 Misfortune 成为两条都足以构筑整局的主神路线：
  - Fortuna：更高的随机上限
  - Misfortune：更高的确定性下限
- 让 low luck 玩家获得独特内容价值，但不把 low luck 做成隐藏最优路线

### 非目标

- 不把 low luck 直接补平为普通 luck
- 不给负 luck 做随机掉落保底
- 不让所有神都去争“掉率提升 / crit 提升”同一条轴
- 不让队伍平均 luck 直接决定掉落
- 不用“剧情保底”“弱福兆”“累计阈值必出”来保证 Fortuna / Misfortune 每周目稳定出现
- 不允许游戏内直接修改 `hidden_luck_at_birth`

---

## 核心设计结论

## 一、命运值数据模型

### 1.1 角色级命运数据

| 变量 | 来源 | 取值 | 可变性 | 存储位置 |
|---|---|---:|---|---|
| `hidden_luck_at_birth` | 建卡 reroll / 队友模板 / 剧情事件 | `-6 ~ +2` | 不可变 | `custom_stats[&"hidden_luck_at_birth"]` |
| `faith_luck_bonus` | 神恩累计 | `0 ~ +5` | 可增 | `custom_stats[&"faith_luck_bonus"]` |
| `effective_luck` | 二者相加 | `-6 ~ +7` | 派生 | 不存储 |
| `fortune_marked` | Fortuna 罕见入教事件 | `0 / 1` | 永久 | `custom_stats[&"fortune_marked"]` |
| `doom_marked` | Misfortune 黑兆入教事件 | `0 / 1` | 永久 | `custom_stats[&"doom_marked"]` |
| `doom_authority` | Misfortune rank-up 主成长值 | `0 ~ +5` | 可增 | `custom_stats[&"doom_authority"]` |

### 1.2 队伍级数据

| 变量 | 含义 | 存储建议 |
|---|---|---|
| `party_drop_luck_source_member_id` | 当前随机掉落承担者 | `PartyState` |
| `fate_run_flags` | 本周目命运神触发锁与事件锁 | `CampaignState` / `PartyState.meta_flags` |

> 说明：
> - `fortune_marked` / `doom_marked` 是永久角色状态，应放 `custom_stats`
> - “本周目是否已尝试过 Fortuna 标记事件” 这类状态是**周目运行期 flag**，不应放进 `custom_stats`

### 1.3 reroll 到出生 luck 的映射

| reroll 次数 | `hidden_luck_at_birth` |
|---|---:|
| 0 | +2 |
| 1 ~ 9 | +1 |
| 10 ~ 99 | 0 |
| 100 ~ 999 | -1 |
| 1,000 ~ 9,999 | -2 |
| 10,000 ~ 99,999 | -3 |
| 100,000 ~ 999,999 | -4 |
| 1,000,000 ~ 9,999,999 | -5 |
| 10,000,000+ | -6 |

### 1.4 队友默认口径

- 普通队友默认 `hidden_luck_at_birth = 0`
- 特殊模板队友可以带 `+1 / +2 / -1 / -2 ...`
- 某些重大剧情可以永久改写队友的出生命运标签
- 这类事件应被视为**稀有世界观事件**，而不是普通成长按钮

---

## 二、命中 / 大成功 / 大失败统一判定

v2 把“命运”正式并入攻击主流程。

### 2.1 劣势规则

本文中的“劣势”统一采用：

- **相关骰子掷两次，取较小值**

该规则同时作用于：
- `crit_gate_die`
- 普通命中 `d20`

### 2.2 劣势（Hardship）来源

首版建议只保留真正的“苦境”来源，避免玩家故意做坏选择刷命运：

1. 被 2 个以上敌人相邻包夹
2. 当前 HP ≤ 30%
3. 身上存在强攻击型减益（致盲 / 眩晕 / 恐惧 / 重疲劳等）
4. 场景显式打上的不利标签（如黑暗、塌方、毒雾）

**明确排除**：
- 主动用错误元素打抗性目标
- 玩家故意做的坏选择
- 纯经济拖延行为

### 2.3 大成功门骰尺寸

```gdscript
func calc_crit_gate_die_size(effective_luck: int, is_disadvantage: bool) -> int:
    var k := maxi(0, -effective_luck - 3)

    # 命运的怜悯：仅对 <= -5 生效
    if is_disadvantage and effective_luck <= -5 and k > 0:
        k -= 1

    return 20 << k
```

对应表：

| effective_luck | 正常 `crit_gate_die` | 劣势 `crit_gate_die` |
|---|---:|---:|
| ≥ -3 | d20 | d20 |
| -4 | d40 | d40 |
| -5 | d80 | d40 |
| -6 | d160 | d80 |

### 2.4 正 luck 的战斗收益：高位大成功威胁区间

当 `crit_gate_die == d20` 时，不再额外掷 crit 子骰，而是直接把命中 d20 的高位区间视为大成功威胁区间。

为避免 `effective_luck = +7` 直接把 crit 拉爆，战斗侧使用软上限：

```gdscript
func calc_combat_luck_score(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int:
    return mini(4, maxi(0, hidden_luck_at_birth) + maxi(0, faith_luck_bonus) / 2)

func calc_crit_threshold(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int:
    return 20 - calc_combat_luck_score(hidden_luck_at_birth, faith_luck_bonus)
```

对应表：

| `combat_luck_score` | 威胁区间 | 正常概率 | 劣势概率（两次取小） |
|---:|---|---:|---:|
| 0 | 20 | 5.00% | 0.25% |
| 1 | 19–20 | 10.00% | 1.00% |
| 2 | 18–20 | 15.00% | 2.25% |
| 3 | 17–20 | 20.00% | 4.00% |
| 4 | 16–20 | 25.00% | 6.25% |

> 设计语义：
> - `hidden_luck_at_birth` 比 `faith_luck_bonus` 更珍贵
> - faith 可以放大好运，但不能完全复制“天生命好”

### 2.5 负 luck 的战斗惩罚：低位大失败区间

大失败不再使用独立门骰，而是直接属于命中 d20 判定本身。

```gdscript
func calc_fumble_low_end(effective_luck: int) -> int:
    return 1 + clamp(-effective_luck - 4, 0, 2)
```

对应表：

| effective_luck | 大失败区间 | 正常概率 | 劣势概率（两次取小） |
|---|---|---:|---:|
| ≥ -4 | 1 | 5.00% | 9.75% |
| -5 | 1–2 | 10.00% | 19.00% |
| -6 | 1–3 | 15.00% | 27.75% |

### 2.6 完整攻击流程

```gdscript
func resolve_attack(attacker, defender) -> AttackResult:
    var effective_luck := attacker.get_effective_luck()
    var hidden_luck := attacker.get_hidden_luck_at_birth()
    var faith_luck := attacker.get_faith_luck_bonus()
    var is_disadvantage := BattleState.is_attack_disadvantage(attacker, defender)

    var crit_die := calc_crit_gate_die_size(effective_luck, is_disadvantage)

    # 1. 当 crit_gate_die > d20 时，先过大成功门
    if crit_die > 20:
        var crit_gate_roll := roll_die_with_disadvantage_rule(crit_die, is_disadvantage)
        if crit_gate_roll == crit_die:
            return AttackResult.CRITICAL_HIT

    # 2. 掷命中 d20（劣势则两次取小）
    var hit_roll := roll_die_with_disadvantage_rule(20, is_disadvantage)

    # 3. 低位区间直接大失败
    var fumble_low_end := calc_fumble_low_end(effective_luck)
    if hit_roll <= fumble_low_end:
        return AttackResult.CRITICAL_FAIL

    # 4. 当 crit_gate_die == d20 时，高位区间命中才升级为大成功
    if crit_die == 20:
        var crit_threshold := calc_crit_threshold(hidden_luck, faith_luck)
        if hit_roll >= crit_threshold and attack_total(hit_roll, attacker, defender) >= target_ac(defender):
            return AttackResult.CRITICAL_HIT

    # 5. 中间区间才做普通命中比较
    if attack_total(hit_roll, attacker, defender) >= target_ac(defender):
        return AttackResult.HIT

    return AttackResult.MISS
```

### 2.7 关键口径

- `crit_gate_die > d20` 时，普通命中骰即使掷出 20，也只是普通命中，不会再升级成大成功
- 大失败区间一旦命中，立即视为大失败，不再比较 AC
- 命运的怜悯只降低**大成功门骰**，不降低**大失败概率**
- v2 不再承认“crit 与命中独立”“命中失败也被 Fortuna 看到”的口径

---

## 三、随机掉落与固定成长分流

### 3.1 掉落仍采用三段式

```text
数量 roll × 稀有度 roll × 物品 roll
```

### 3.2 随机掉落只读取一个承担者

为避免“全队平均 luck”或“全队最高 luck”把经济系统炸掉，随机掉落只读取一名承担者：

- `party_drop_luck_source_member_id`
- 默认 fallback：`main_character_member_id`
- 仅可在安全区 / 营地切换

### 3.3 掉落 luck 软封顶

```gdscript
func get_drop_luck(member) -> int:
    return clamp(member.get_effective_luck(), -6, +5)
```

也就是说：

- `effective_luck = +6 / +7` 仍有战斗价值
- 但不会继续把随机掉落往上推爆

### 3.4 稀有度 roll 仍沿用 `3d6 + drop_luck`

```gdscript
func roll_drop_rarity(drop_luck: int) -> int:
    var roll := _rng.randi_range(1, 6) + _rng.randi_range(1, 6) + _rng.randi_range(1, 6)
    roll += drop_luck
    if roll >= 18: return Rarity.LEGENDARY
    elif roll >= 16: return Rarity.EPIC
    elif roll >= 13: return Rarity.RARE
    elif roll >= 10: return Rarity.UNCOMMON
    else: return Rarity.COMMON
```

### 3.5 固定来源不受 luck 影响

以下路径继续**完全绕过**随机掉落 luck 体系：

- 剧情任务奖励
- boss 保底掉落
- 商店固定库存
- 锻造 / 合成
- 神灵奖励
- low luck 专属固定事件与黑市制作

---

## 四、Fortuna：神迹 + 随机成长路线

### 4.1 定位

- 她不再是“每周目稳定可进入”的普遍系统，而是**极稀有命运事件**
- 她代表：
  - 神迹
  - 偶然中的救赎
  - 更高的随机上限
- 她不再承担“必然救赎所有 low luck”的职能
- v2 中，low luck 的可玩性主要靠：
  - Misfortune
  - 固定成长
  - 特殊剧情
  来补，而不是靠 Fortuna 保底

### 4.2 入教门票：`fortune_marked`

Fortuna 不再使用“弱福兆”“弱怜悯”“剧情保底”“累计阈值保底”。

**唯一来源**：

- 角色本周目第一次对 `elite / boss` 触发 `critical_success_under_disadvantage`
- 立刻再按**相同 `crit_gate_die` 与相同劣势规则**做一次二次确认
- 若二次确认仍为大成功，则直接获得 `fortune_marked = 1`

```gdscript
func try_grant_fortune_mark(attacker, defender) -> bool:
    if not defender.is_elite_or_boss():
        return false
    if FateRunFlags.has_attempted_fortune_mark(attacker.member_id):
        return false

    FateRunFlags.set_attempted_fortune_mark(attacker.member_id, true)

    if not attacker.last_attack_was_critical_success_under_disadvantage:
        return false

    var die_size := calc_crit_gate_die_size(attacker.get_effective_luck(), true)
    var confirm_roll := roll_die_with_disadvantage_rule(die_size, true)
    if confirm_roll == die_size:
        attacker.set_custom_stat(&"fortune_marked", 1)
        return true
    return false
```

### 4.3 rank 主成长

Fortuna rank 仍沿用：

- `faith_luck_bonus +1` × 5 阶
- 总计 `+5`

但 v2 明确区分：
- 战斗大成功率不会被 `+5` 线性拉爆
- 随机掉落仍吃这条神恩，但掉落 luck 软封顶在 `+5`

### 4.4 rank 表（保留 5 阶骨架）

| rank | 名称 | required_gold | required_level | 入门条件 | 奖励 |
|---|---|---:|---:|---|---|
| 1 | 浅信徒 | 500 | 0 | `fortune_marked == 1` | `faith_luck_bonus +1` |
| 2 | 真信徒 | 2,000 | 8 | Fortuna guidance I | `faith_luck_bonus +1` |
| 3 | 虔诚信徒 | 4,500 | 14 | Fortuna guidance II | `faith_luck_bonus +1` |
| 4 | 至诚信徒 | 8,000 | 20 | Fortuna guidance III | `faith_luck_bonus +1` |
| 5 | 神眷者 | 14,000 | 28 | Fortuna guidance IV | `faith_luck_bonus +1` |

### 4.5 guidance 口径更新

由于 v2 把 crit 接进命中主流程，且劣势大成功已经显著稀有，**废弃 v1 的固定计数阈值 `1 / 3 / 10 / 30`**。

改为 4 个一锤子 rare achievement：

| achievement_id | 条件 | 说明 |
|---|---|---|
| `fortuna_guidance_true` | 已 `fortune_marked` 后，再次对 `elite / boss` 触发 `critical_success_under_disadvantage` | 真正被她再次看见 |
| `fortuna_guidance_devout` | 已信 Fortuna 的角色在低血与强 debuff 下存活并获胜 | 怜悯不是白给 |
| `fortuna_guidance_exalted` | 已信 Fortuna 的角色用高位威胁区间（而非门骰）对 `elite / boss` 打出一次大成功 | 好运被放大 |
| `fortuna_guidance_blessed` | 完成一个章节且无角色永久死亡，且本章内该角色至少出现过一次 Fortuna 相关战斗事件 | 被命运庇护的整章体验 |

> 这些 guidance 是**一次性剧情/成就门槛**，而不是 farm 型计数条。

---

## 五、Misfortune：boss 控制 + 固定成长路线

### 5.1 定位

Misfortune 不是 Fortuna 的反色复制品。  
她的目标不是“也去加爆率 / 加 crit”，而是拿走 Fortuna 没占满的两条轴：

- **boss 控制权**
- **固定成长权**

一句话定义：

- **Fortuna = 更高的随机上限**
- **Misfortune = 更高的确定性下限**

### 5.2 入教门票：`doom_marked`

Misfortune 同样不做保底，不做累计必出。

推荐黑兆事件池（满足其一即可直接获得 `doom_marked`）：

1. 角色携带诅咒遗物并打赢 `elite / boss`
2. 角色在 `boss` 战中吃到专属诅咒 / 黑印后存活并获胜
3. 同一场战斗内，角色同时经历“大失败 / 强 debuff / 队友倒地 / 低血幸存”中的两项以上并获胜
4. 角色在黑祭坛事件中接受代价型契约并完成后续试炼

这些事件本身就够窄，因此不再叠第二层百分比。

### 5.3 战斗资源：`calamity`

- 每战重置
- 不序列化
- 作为 Misfortune 技能与固定成长的燃料

#### 生成规则

每战第一次发生以下任一事件时，获得 `+1 calamity`：

- 普通 miss
- 大失败
- 获得强 debuff
- 相邻队友倒地
- 低血结束回合仍存活
- boss 进入新阶段

若本单位本战第一次发生的是**大失败**，额外获得【逆运】1 回合：

- 下次施放 Misfortune 技能 `calamity` 消耗 -1
- 或下一次对受封印目标的攻击不可被闪避

#### 上限

- 基础：3
- rank 2：+1
- rank 4：+1
- `hidden_luck_at_birth <= -5`：再 +1

### 5.4 rank 表

`deity_id`: `misfortune_black_crown`

| rank | 名称 | required_gold | required_level | 入门条件 | 奖励 |
|---|---|---:|---:|---|---|
| 1 | 见厄者 | 500 | 0 | `doom_marked == 1` | `doom_authority +1`、`black_star_brand`、`坏运成筹`、`每战第一次黑印免费` |
| 2 | 灾厄持灯者 | 2,000 | 8 | guidance I | `doom_authority +1`、`calamity` 上限 +1、`灾厄结算`、黑市 oath |
| 3 | 折冠者 | 4,500 | 14 | guidance II | `doom_authority +1`、`crown_break` |
| 4 | 厄运代行者 | 8,000 | 20 | guidance III | `doom_authority +1`、封印持续 +1、固定材料掉落强化、深厄特权 |
| 5 | 黑冕宣判者 | 14,000 | 28 | guidance IV | `doom_authority +1`、`doom_sentence`、宣判击杀返 `calamity`、心核掉落 |

### 5.5 Misfortune 技能骨架

#### `black_star_brand`
- 每战第一次施放免费，此后消耗 1 `calamity`
- 对普通敌人：
  - 不能反击
  - 不能 guard
- 对 elite / boss：
  - 不能 crit
  - 命中下降
  - 你的第一次攻击忽视部分 guard

#### `crown_break`
- 消耗 2 `calamity`
- 只能对已被烙印的 `elite / boss`
- 三选一封印 2 回合：
  - 断牙：不能 crit
  - 折手：不能反击 / 追击
  - 遮目：不能闪避

#### `doom_sentence`
- 每战 1 次，消耗 5 `calamity`
- 对 `elite / boss` 施加 2 回合【厄命宣判】：
  - 不能 crit
  - 不能反击
  - 不能闪避
  - 受到全队额外伤害
  - 若身上已有 2 个 debuff，则主技能失效 / 行动受限

### 5.6 Misfortune 的战后经济回路

这是与 Fortuna 对标的关键。

#### `灾厄结算`
战斗结束后：
- 每 2 点未消耗的 `calamity`
- 自动结算为 1 个 `calamity_shard`
- 普通战每章最多结算 4 个 shard
- elite / boss 不受此上限影响

#### elite / boss 固定掉落
- 死于【黑星烙印】或任一封印状态下的 `elite / boss`
  - 固定掉 `calamity_shard`
- 死于【厄命宣判】状态下的 `boss`
  - 固定掉 `black_crown_core`

### 5.7 Misfortune guidance 口径

Misfortune 的 guidance 不做“多打几次就行”的计数条，而绑定“在坏事中掌控战局”。

| achievement_id | 条件 |
|---|---|
| `misfortune_guidance_true` | 已 `doom_marked` 后，首次用 Misfortune 技能成功封印 `elite / boss` |
| `misfortune_guidance_devout` | 本战曾大失败或吃强 debuff，随后用封印技能赢下 `elite / boss` |
| `misfortune_guidance_exalted` | 同一战斗内把未用完的 `calamity` 成功转化为 shard，并用固定材料打造第一件黑暗装备 |
| `misfortune_guidance_blessed` | 用 `doom_sentence` 完成一次章末 boss 的宣判击杀 |

---

## 六、低 luck 的平衡原则：补“维度”，不补“主轴”

### 6.1 设计原则

low luck 的问题是：

- 神迹更少
- 随机掉落更差
- 失败更常见

v2 的平衡原则不是去偷偷把这些东西补回去，而是：

- 补固定成长
- 补特殊剧情
- 补另类战术权
- 但**不补 luck 本体，不补爆率，不补 crit**

### 6.2 low luck 专属事件池（样板）

#### `断桥生还`
- 条件：低血 / 包夹 / 强 debuff 下结束战斗仍存活
- 奖励：
  - 固定 `calamity_shard`
  - 开启隐藏短支线
  - 获得一次性诅咒道具

#### `灯下无人`
- 条件：在神龛/旅舍/赌坊休整时，队伍中存在 `hidden_luck_at_birth <= -4` 的角色
- 奖励：
  - 解锁厄运线索
  - 特殊 NPC 反应
  - 黑市知识

#### `死里借来的路`
- 条件：本章中该角色出现过大失败，但整场战斗最终获胜
- 奖励：
  - 隐藏路径
  - 固定材料
  - 低 luck 商人

### 6.3 low luck 固定奖励池（样板）

#### `逆命护符`
- 每战第一次大失败降级为普通 miss
- 代价：之后 2 回合伤害降低

#### `黑星楔钉`
- 对 `elite / boss` 第一击忽视部分 guard
- 代价：若未击杀，自己获得 1 回合破绽

#### `血债披肩`
- 低血减伤
- 队友倒地后临时获得行动相关资源
- 代价：战斗结束恢复量降低

#### `亡途灯笼`
- 显示隐藏陷阱 / 黑市 / 厄兆路径
- 代价：佩戴者更容易卷入黑兆事件

### 6.4 low luck 战术权池（样板）

#### `失手成筹`
- 每战第一次大失败，额外获得 1 `calamity`

#### `黑契推进`
- 主动失去 HP / Guard / 下一回合行动力
- 换取一次“必定命中但不暴击”的攻击

#### `断命换位`
- 自己获得 1 个 debuff
- 立刻与 3 格内队友交换位置

#### `黑冠封印`
- 每战 1 次，对 boss 施加 1 回合“不能反击”或“不能暴击”

> 这些都是**高代价换确定性**，而不是“免费把坏运变好运”。

---

## 七、UI 与可读性要求

由于 v2 大幅提高了系统复杂度，UI 不再能只显示“劣势大成功”。

### 7.1 战斗前/悬浮提示必须显示

- 当前是否处于劣势
- 当前 `crit_gate_die`
- 当前大失败区间
- 当前高位大成功区间（若 `crit_gate_die == d20`）
- 当前是否吃到“命运的怜悯”

### 7.2 战报必须解释

- 为什么这个 20 只是普通命中
- 为什么这个 2 直接是大失败
- 这次大成功是“门骰命中”还是“高位威胁区命中”
- 这次攻击是否满足 Fortuna / Misfortune 相关触发条件

### 7.3 CharacterInfoWindow

显示角色级命运信息：

- `hidden_luck_at_birth`
- `faith_luck_bonus`
- `effective_luck`
- `fortune_marked`
- `doom_marked`
- `doom_authority`（若已入 Misfortune）

---

## 八、数据模型与持久化

### 8.1 `custom_stats` 新键

| 键 | 类型 | 默认 | 写入时机 |
|---|---|---:|---|
| `hidden_luck_at_birth` | int | 0 | 建卡 / 特殊剧情 |
| `faith_luck_bonus` | int | 0 | rank 奖励 |
| `fortune_marked` | int | 0 | 罕见 Fortuna 事件 |
| `doom_marked` | int | 0 | 黑兆事件 |
| `doom_authority` | int | 0 | Misfortune rank 奖励 |

### 8.2 保护列表

```gdscript
const PROTECTED_CUSTOM_STAT_KEYS := [
    &"hidden_luck_at_birth",
]
```

`hidden_luck_at_birth` 仍然只能由：
- `CharacterCreationService`
- 特殊剧情脚本白名单
写入。

### 8.3 `PartyState` 新增字段

```gdscript
var party_drop_luck_source_member_id: int = -1
var fate_run_flags: Dictionary = {}
```

### 8.4 `BattleRuntime` 临时字段

```gdscript
var calamity_by_member_id: Dictionary = {}
```

> `calamity` 不写入永久存档；若战斗可中断续打，则写入 battle snapshot，而不是角色永久进度

### 8.5 `EquipmentInstanceState`

保持 v1 的 `rarity` 字段扩展，旧存档缺省为 `COMMON`。

---

## 九、运行链路

### 9.1 攻击结算

1. `BattleState.is_attack_disadvantage(attacker, defender)`
2. `calc_crit_gate_die_size(effective_luck, is_disadvantage)`
3. 若 `crit_gate_die > 20`：
   - 先按劣势规则掷 `crit_gate_die`
   - 成功则 `CRITICAL_HIT`
4. 掷普通命中 d20（劣势则两次取小）
5. 若命中骰落入大失败区间：
   - `CRITICAL_FAIL`
6. 若 `crit_gate_die == 20`：
   - 命中骰落入高位威胁区且命中成立
   - `CRITICAL_HIT`
7. 否则做普通命中比较
8. 记录：
   - `critical_success_under_disadvantage`
   - `critical_fail`
   - `ordinary_miss`
   - `hardship_survival`
   - 供神灵系统与剧情系统订阅

### 9.2 Fortuna 标记

- 仅在 `critical_success_under_disadvantage` against `elite / boss` 时检查
- 同一角色本周目只尝试一次
- 成功则写入 `fortune_marked = 1`

### 9.3 Misfortune 标记

- 由黑兆事件脚本直接写入 `doom_marked = 1`
- 不再额外掷百分比

### 9.4 掉落

1. 结算战斗奖励时读取 `party_drop_luck_source_member_id`
2. 取得 `drop_luck = clamp(effective_luck, -6, +5)`
3. 随机掉落走 `EquipmentDropService`
4. 固定材料（`calamity_shard` / `black_crown_core`）走 boss / elite 固定战利品逻辑

---

## 十、测试计划

### 10.1 progression / serialization

- `hidden_luck_at_birth ∈ [-6, +2]` round-trip 正确
- `fortune_marked / doom_marked / doom_authority` round-trip 正确
- 旧存档缺字段回退默认值
- `hidden_luck_at_birth` 非白名单写入被拒绝
- `effective_luck = +2 + +5 = +7` 计算正确
- `drop_luck` 在 `effective_luck = +7` 时仍为 `+5`

### 10.2 battle / formula

- `effective_luck = -6` 正常：
  - `crit_gate_die = d160`
- `effective_luck = -6` 劣势：
  - `crit_gate_die = d80`
  - `P(大成功) = 1/6400`
- `effective_luck = -5` 劣势：
  - `crit_gate_die = d40`
  - `P(大成功) = 1/1600`
- `effective_luck = -4` 劣势：
  - `crit_gate_die = d40`
  - `P(大成功) = 1/1600`
- `effective_luck = 0, combat_luck_score = 0`：
  - 正常 crit 率 5%
- `combat_luck_score = 4`：
  - 正常 crit 率 25%
  - 劣势 crit 率 6.25%
- `effective_luck = -6`：
  - 大失败区间 `1-3`
  - 劣势大失败率 ≈ 27.75%
- `crit_gate_die > 20` 时：
  - 普通命中 d20 掷出 20 只算普通命中
- 低位区间直接大失败，不比较 AC

### 10.3 Fortuna

- 未 `fortune_marked` 时不能进入 Fortuna
- `elite / boss` 劣势大成功后，正确进行二次确认
- 二次确认成功后 `fortune_marked = 1`
- 同一角色本周目二次尝试被锁死
- guidance 改为一次性 rare achievement，不再使用计数阈值

### 10.4 Misfortune

- 黑兆事件正确写入 `doom_marked`
- rank 1 后第一次 `black_star_brand` 免费
- `calamity` 在大失败 / miss / 强 debuff / 队友倒地 / boss 相变时正确增长
- 未消耗 `calamity` 战后正确折算 shard
- `doom_sentence` 对 boss 的封印、增伤、固定材料掉落正确

### 10.5 drops

- `effective_luck = +7` 时，随机掉落按 `drop_luck = +5` 计算
- `party_drop_luck_source_member_id` 正确切换
- 固定材料掉落不受 `drop_luck` 影响

---

## 十一、Public Interfaces

### `PartyMemberState`
- `get_hidden_luck_at_birth() -> int`
- `get_faith_luck_bonus() -> int`
- `get_effective_luck() -> int`
- `get_combat_luck_score() -> int`
- `get_drop_luck() -> int`

### `PartyState`
- `party_drop_luck_source_member_id: int`
- `fate_run_flags: Dictionary`

### `UnitBaseAttributes.custom_stats` 新语义键
- `hidden_luck_at_birth`
- `faith_luck_bonus`
- `fortune_marked`
- `doom_marked`
- `doom_authority`

### `scripts/systems/fate_attack_formula.gd`
- `calc_crit_gate_die_size(effective_luck: int, is_disadvantage: bool) -> int`
- `calc_fumble_low_end(effective_luck: int) -> int`
- `calc_combat_luck_score(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int`
- `calc_crit_threshold(hidden_luck_at_birth: int, faith_luck_bonus: int) -> int`
- `roll_die_with_disadvantage_rule(die_size: int, is_disadvantage: bool) -> int`

### `scripts/systems/equipment_drop_service.gd`
- `roll_drops(drop_table_id: StringName, drop_luck: int) -> Array[EquipmentInstanceState]`
- `roll_drop_rarity(drop_luck: int) -> int`

---

## 十二、实施顺序建议

1. 先扩展 `custom_stats` 读取封装：
   - `get_hidden_luck_at_birth()`
   - `get_faith_luck_bonus()`
   - `get_effective_luck()`
   - `get_combat_luck_score()`
   - `get_drop_luck()`
2. 新增 `fate_attack_formula.gd`，先只做公式与单测
3. 在 `battle_state.gd` 中把“劣势”收窄到真正 Hardship 条件
4. 把新版攻击流程接进 `battle_damage_resolver.gd`
5. 接入 `critical_fail`、`critical_success_under_disadvantage` 事件
6. 补 `PartyState.party_drop_luck_source_member_id`
7. 扩展 `equipment_drop_service.gd` 与 `EquipmentInstanceState.rarity`
8. 先落 Fortuna 标记逻辑与 rank 1~5 空骨架
9. 再落 Misfortune 的 `doom_marked / doom_authority / calamity`
10. 最后补 low luck 专属事件池、黑市、固定制作池与 UI 解释层

---

## 默认假设

- 这是一个**刻意允许命运感侵入战棋核心回合**的非常规战棋，不追求传统“信息完全公平”
- Fortuna / Misfortune 是命运事件，不保证每周目都能进入
- low luck 的平衡主要靠固定成长与特殊内容，而不是把随机数偷偷修回正常
- `faith_luck_bonus` 仍为单调不减；`doom_authority` 也为单调不减
- 若后续要扩展“改信 / 弃信 / 神罚”，应与 `fortune_marked / doom_marked` 一并设计，而不是单改数值

---

## 十三、两条路线总预算对照表（属性超高权重前提）

> 本节用于明确：
> - **高 luck + Fortuna** 与 **低 luck + Misfortune** 不要求“神恩包逐项对等”
> - 真正需要对齐的是**整局总预算**
> - 当前前提是：**每 1 点属性在后续都会持续放大，并跨越关键 breakpoint**
>
> 因此，low luck 主角通过大量 reroll 获得的更高出生属性，必须被视为 Misfortune 路线的一部分预算；
> 在该前提下，Misfortune 的神灵包可以、也应该，**略弱于** Fortuna 的神灵包。

### 13.1 预算原则

- **Fortuna 路线**：
  - 低出生属性
  - 高 luck
  - 更高的随机上限
  - 更强的掉落经济
  - 更高的神迹感与高光密度
- **Misfortune 路线**：
  - 高出生属性
  - 低 luck
  - 更高的战术确定性
  - 更强的 boss 压制与固定成长
  - 更强的 fail-forward 转化

### 13.2 预算表（推荐口径）

| 预算项 | 高 luck + Fortuna | 低 luck + Misfortune | 设计口径 |
|---|---|---|---|
| **出生属性预算** | 低 | 高 | low luck 的高属性是整条路线最重的预算之一，不应再被神恩完全补强 |
| **战斗高光预算** | 很高 | 中 | Fortuna 强在神迹与高光；Misfortune 强在压制与稳态，而不是瞬时高光 |
| **战斗确定性预算** | 中低 | 很高 | Misfortune 负责把高属性真正落地：封印、转资源、代价换确定收益 |
| **随机经济预算** | 很高 | 低 | Fortuna 吃随机掉落与战后爆率；Misfortune 不争这条轴 |
| **固定成长预算** | 中 | 很高 | Misfortune 用固定材料、固定制作、黑市、契约物来对标 Fortuna 的随机经济 |
| **小战常驻收益** | 中高 | 中高 | 两者都应在普通战有存在感；但 Fortuna 偏自然触发，Misfortune 偏失败转资源 |
| **elite / boss 战预算** | 中高 | 很高 | Misfortune 在高价值敌人战中应明显更强 |
| **周目上限预算** | 很高 | 中高 | Fortuna 代表“神迹 + 丰收”的高波动上限 |
| **周目下限预算** | 中 | 很高 | Misfortune 代表“再倒霉也能靠控制与固定成长打完整局” |
| **特殊剧情预算** | 中 | 中高 | low luck 可拥有更黑、更窄、更有代价的专属剧情，但不能更肥 |
| **操作负担预算** | 低 | 中高 | Misfortune 允许更复杂，因为它本来就更像战术路线；但不能复杂到玩家不愿意使用 |

### 13.3 推荐的“神灵包体积”比例

在“每 1 点属性都非常值钱”的前提下，推荐把 **Misfortune 神灵包** 控制在 **Fortuna 神灵包的 70% ~ 85%** 左右。

解释：
- 剩余的 15% ~ 30% 预算，由 low luck 主角通过大量 reroll 获得的**更高出生属性**来承担
- 如果 Misfortune 也做成 100% 对等，最终往往会出现：
  - low luck 主角有更高面板
  - 还拿到稳定 boss 控制与固定成长
  - 反而成为隐藏最优路线

### 13.4 设计检查表

当感觉 Misfortune “不够强”时，先不要立刻给它加数值，先问四个问题：

1. 该路线是否已经拿到了**更高出生属性**？
2. 该路线是否已经在 boss / elite 战中明显优于 Fortuna？
3. 该路线是否已经拥有一条**不受 luck 影响**的固定成长线？
4. 该路线的弱点，是否正是 Fortuna 的强点（随机上限、爆率、神迹感）？

若四项中有三项成立，则 Misfortune 可以继续保持“略弱于 Fortuna 神包”的配置。

### 13.5 失衡警报

#### Misfortune 过强的信号

- 玩家主动追求低 luck 开局
- 玩家认为大量 reroll 是“高端玩法”而非代价
- low luck 主角既有更高属性，又有与 Fortuna 同级别的神恩包
- Fortuna 被视为“新手浪漫路线”，Misfortune 被视为“高手正确路线”

#### Misfortune 过弱的信号

- low luck 主角长期处于“双重惩罚”
- 即使有更高属性，仍无法弥补随机惩罚与大失败带来的损失
- 玩家不愿意进入 Misfortune，只把它视为倒霉者的安慰奖
- 低 luck 的特殊剧情与固定成长，也无法让其形成完整 build

### 13.6 最终平衡结论

v2 推荐采用以下总原则：

**不要求 Fortuna 与 Misfortune 的神恩逐项对等；**
**要求的是：**
- **低属性 + 高 luck + Fortuna**
- **高属性 + 低 luck + Misfortune**

这两种完整周目的总价值接近、但气质不同：

- Fortuna：更神、更富、更浮动
- Misfortune：更黑、更稳、更可控

这是一种**刻意的不对称平衡**，不是数值未调齐。

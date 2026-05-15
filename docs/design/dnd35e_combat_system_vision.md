# DND 3.5e 战斗系统全量展望

> 更新日期：`2026-04-15`
> 定位：全量系统蓝图，不受当前代码可行性和改动量约束
> 前置文档：`../discussions/dnd_weapon_system_initial_plan.md`、`skills_implementation_plan.md`、`player_growth_system_plan.md`

## 关联上下文单元

- CU-10：共享仓库、物品定义与装备基础流转
- CU-14：progression 规则与属性服务
- CU-16：战斗状态模型、边规则、伤害、AI 规则层

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文是长期规则蓝图，不代表当前代码已经完整采用 DND 3.5e 全量口径。

## 设计意图

采用 DND 3.5e 规则作为战斗系统内核，核心目的是：

- **双向等级碾压**：高等级角色的攻击加值（BAB）和防御等级（AC）同时压制低等级，让等级差产生不可逾越的战斗力鸿沟
- **装备权重极大**：3.5e 的 AC 和攻击加值都严重依赖魔法装备的附魔等级，装备构筑成为战力核心轴
- **职业差异化鲜明**：战士满 BAB 每轮多次攻击，法师 AC 薄但法术毁天灭地，盗贼依赖偷袭——每个职业在战场上的行为模式截然不同
- **规则可预期**：d20 + 修正 vs 目标值，玩家能在出手前精确计算命中概率，支撑战棋的策略深度

## 一、攻击系统

### 1.1 基础攻击加值（BAB）

BAB 是角色命中能力的核心驱动。本项目 BAB 由**每个职业的 `profession.rank` × 该职业 BAB 档位**累加而成，**总 BAB 上限锁定 +10**。这与 3.5e 原版 +20 的上限不同，是有意把整体加值堆栈压扁的设计选择（详见 §19）。

#### 三档 BAB 曲线（rate / 8 整数算法）

为避免多职业时的精度丢失，BAB 用整数分子 + 公分母 8 的形式表达：

| 档位 | rate（分子）| 适用职业组 | rank 1 | rank 5 | rank 10 | rank 15 | rank 20 |
|------|------|-----------|----|----|-----|-----|-----|
| Full | 4 | 战士/野蛮人/圣骑/游侠 | 0 | 2 | 5 | 7 | **10** |
| ¾ | 3 | 牧师/盗贼/武僧/吟游 | 0 | 1 | 3 | 5 | 7 |
| ½ | 2 | 法师/术士 | 0 | 1 | 2 | 3 | 5 |

> rank 1 战士 BAB = 0 是有意设计：rank 1 战士命中靠 Str 修正撑，BAB 在 rank 2 起才贡献。线性公式干净、多职业累加无歧义。

#### 多职业累加规则（先乘后除，禁止 per-prof floor）

**关键不变量**：必须先把所有职业的 `rank × rate` 累加成总分子，**再除以分母 8**。如果先在每个职业各自 floor，会因为低 rank × 低 rate 经常落在 1 以下而被截断到 0，多职业玩家系统性丢 BAB。

```
total_bab = floor( Σ (profession_i.rank × rate_i) / 8 )

正确：法师 7 + 牧师 5 → (7×2 + 5×3)/8 = 29/8 = 3
错误：法师 7 + 牧师 5 → floor(14/8) + floor(15/8) = 1 + 1 = 2  ← 丢 1 BAB
```

#### +10 上限的天然保证

总 rank ≤ 20 时（多职业上限）：
- 全 Full：20 × 4 / 8 = **10**
- 全 ¾：20 × 3 / 8 = 7.5 → **7**
- 全 ½：20 × 2 / 8 = **5**

任意混合都不会超 +10，**无需 explicit clamp**——数学保证。

#### dip 防呆

由于"专精 > 杂学"的累加结构，1-2 级的副职业 dip 几乎贡献不到 BAB：
- 战士 19 + 法师 1：(76+2)/8 = 9
- 战士 20：80/8 = 10（多 1 点 BAB，但代价是放弃法师能力）
- 法师 19 + 战士 1：(38+4)/8 = 5（dip 战士 1 级只换到 +0 BAB 增量）

#### BAB 与本项目的衔接

- `ProfessionDef` 新增 `bab_progression: StringName`（`full / three_quarter / half`，默认 `half`）
- BAB 由**每个职业的 rank**累加得到，**不由** `character_level` 驱动
- 多职业按累加分子计算，**不**取最优档位
- BAB 作为派生属性注入 `AttributeSnapshot`，字段名 `base_attack_bonus`

#### 迭代攻击（Iterative Attacks）

由于 BAB 顶到 +10，迭代攻击门槛同步压缩为每 +4 一刀（原 3.5e 是每 +5）：

```
BAB +1~+3   → 1 刀
BAB +4~+7   → 2 刀（第二刀 -4）
BAB +8~+10  → 3 刀（每级 -4）
```

战士 rank 20 = BAB +10 → 三刀 +10/+6/+2。比原案 4 刀少一档，但 +10 框架下 3 刀已是顶配。

**战棋适配**：
- 迭代攻击消耗额外 AP，不是免费的
- 第一次攻击：正常 AP 消耗
- 每次迭代攻击：额外消耗 1 AP，攻击加值 -4
- 玩家可以选择只打一次全力攻击，或消耗更多 AP 打多次
- 这让 AP 管理成为战士的核心决策

### 1.2 攻击掷骰

#### 基础公式

```
d20 + attack_bonus >= target_ac → 命中

attack_bonus =
    BAB
    + ability_modifier      # 力量(近战) / 敏捷(远程) / 其他
    + weapon_enchantment    # 魔法武器加值 +1~+5
    + size_modifier         # 体型修正
    + situational_modifiers # 高地/包夹/状态/buff
```

#### 自然骰特殊规则

```
natural 1  → 必定未命中（无论加值多高）
natural 20 → 必定命中 + 暴击威胁
```

#### 优势 / 劣势（可选扩展，来自 5e 简化）

```
advantage:    掷 2d20 取高
disadvantage: 掷 2d20 取低
```

来源：包夹（advantage）、失明（disadvantage）、束缚（disadvantage）等。

### 1.3 远程攻击特殊规则

```
近战范围内射击：-4 攻击（或触发借机攻击）
射程递减：每超一个射程增量 -2
最大射程：投掷武器 5 个增量，射击武器 10 个增量
```

### 1.4 双武器战斗（Two-Weapon Fighting）

```
无专长：主手 -6，副手 -10
双武器战斗专长：主手 -4，副手 -4（副手为轻武器时主手 -2）
精通双武器：副手获得第二次攻击（-5）
高等双武器：副手获得第三次攻击（-10）
```

**战棋适配**：
- 装备系统已有 `main_hand / off_hand` 双槽位
- 双持武器消耗 1 次标准攻击的 AP，但出两刀
- 每把武器独立走攻击掷骰 + 伤害结算

---

## 二、防御系统

### 2.1 AC 组件化

```
AC = 8
   + armor_bonus          # 护甲加值（穿甲）
   + shield_bonus         # 盾牌加值（持盾）
   + dex_modifier         # 敏捷修正（受甲上限）
   + size_modifier        # 体型修正
   + natural_armor        # 天生护甲（怪物皮厚、魔法护符）
   + deflection_bonus     # 偏斜加值（防护戒指等）
   + dodge_bonus          # 闪避加值（武僧/专长）
   + enhancement_bonus    # 增强加值（法术护甲等）
   + situational          # 掩体/高地/状态
```

#### 三种 AC 变体

| 类型 | 包含组件 | 触发场景 |
|------|---------|---------|
| **正常 AC** | 全部组件 | 常规攻击 |
| **措手不及 AC** | 去掉 Dex + Dodge | 突袭轮 / 未行动 |
| **接触 AC** | 去掉 Armor + Shield + Natural | 法术接触攻击 |

### 2.2 护甲数据模型

```
armor_bonus: int          # 护甲 AC 加值
max_dex_bonus: int        # 穿甲时敏捷上限
armor_check_penalty: int  # 护甲惩罚（影响技能检定）
arcane_spell_failure: int # 奥术失败率（百分比）
armor_category: light / medium / heavy
```

#### 护甲 AC 参考表

| 护甲 | AC加值 | Dex上限 | 类别 |
|------|--------|---------|------|
| 软皮甲 | +2 | +6 | 轻甲 |
| 镶嵌皮甲 | +3 | +5 | 轻甲 |
| 链甲衫 | +4 | +4 | 轻甲 |
| 鳞甲 | +4 | +3 | 中甲 |
| 胸甲 | +5 | +3 | 中甲 |
| 链甲 | +5 | +2 | 中甲 |
| 板条甲 | +6 | +0 | 重甲 |
| 半身板甲 | +7 | +0 | 重甲 |
| 全身板甲 | +8 | +1 | 重甲 |

盾牌：
| 盾牌 | AC加值 |
|------|--------|
| 小圆盾 | +1 |
| 轻盾 | +1 |
| 重盾 | +2 |
| 塔盾 | +4 |

### 2.3 AC 随等级成长（等级碾压的防御端）

AC 的等级碾压主要来自**魔法装备附魔等级**：

| 等级段 | 典型战士 AC | 典型法师 AC | AC 来源 |
|--------|------------|------------|---------|
| L1-3 | 16-18 | 11-13 | 普通甲+盾+Dex |
| L4-6 | 19-22 | 14-17 | +1 甲/盾, 防护戒指 |
| L7-10 | 23-27 | 17-21 | +2~3 甲/盾, 天生护甲护符, 偏斜 |
| L11-14 | 28-32 | 21-25 | +3~4 甲/盾, 全套防御魔法物品 |
| L15-18 | 33-37 | 25-30 | +4~5 甲/盾, 高等防御 |
| L19-20 | 38-42 | 30-35 | 满附魔全套 |

**关键**：L1 角色的 +4 攻击加值 vs L15 角色的 AC 33 → 需要 d20 掷 29+，物理上不可能。这就是双向碾压。

### 2.4 与本项目装备系统的衔接

- `ItemDef` 的 `armor` 类物品通过 `ArmorProfile` 子资源声明护甲数据
- `ItemDef` 的 `shield` 类物品通过 `ShieldProfile` 子资源声明盾牌数据
- `PartyEquipmentService` 换装时，计算当前护甲组件并缓存到属性链
- AC 组件在 `AttributeService.get_snapshot()` 中组装为最终 AC 值
- `BattleUnitFactory` 在建单位时把 AC 值注入 `attribute_snapshot`

---

## 三、伤害系统

### 3.1 武器伤害公式

```
damage = weapon_dice_roll + ability_modifier + weapon_enchantment + other_bonus
```

#### 力量与伤害

```
单手近战：+Str_mod
双手近战：+Str_mod × 1.5（向下取整）
副手近战：+Str_mod × 0.5（向下取整）
远程：一般不加 Str（复合弓可加 Str）
投掷：+Str_mod
```

### 3.2 暴击系统

3.5e 暴击分两步：**威胁** + **确认**。

#### 步骤一：暴击威胁

```
if d20_roll >= weapon.crit_range_min:
    crit_threatened = true
```

| 武器 | 威胁范围 | 暴击倍率 |
|------|---------|---------|
| 长剑 | 19-20 | ×2 |
| 巨剑 | 19-20 | ×2 |
| 弯刀 | 18-20 | ×2 |
| 战斧 | ×3 | 20 |
| 大锤 | ×3 | 20 |
| 镐 | ×4 | 20 |
| 匕首 | 19-20 | ×2 |
| 长矛 | ×3 | 20 |
| 重弩 | 19-20 | ×2 |

#### 步骤二：暴击确认

```
confirm_roll = d20 + attack_bonus
if confirm_roll >= target_ac:
    critical_hit = true  # 伤害乘以暴击倍率
else:
    normal_hit = true    # 普通伤害
```

#### 暴击伤害计算

```
crit_damage = (weapon_dice_roll × crit_multiplier) + (static_bonus × crit_multiplier)
```

注意：3.5e 中**静态加值也乘以暴击倍率**（不同于文档里说的"只翻武器骰"）。

#### 暴击免疫

不死生物、构造体、泥形怪等无要害生物免疫暴击。

### 3.3 伤害类型

#### 物理伤害类型

```
slashing   # 斩击：剑、斧
piercing   # 穿刺：矛、箭、匕首
bludgeoning # 钝击：锤、棍
```

部分武器兼具多种类型（如十字弩是 piercing）。

#### 能量伤害类型

```
fire          # 火焰
cold          # 寒冷
lightning     # 闪电
acid          # 酸蚀
sonic         # 音波
positive      # 正能量（治疗活物/伤害不死）
negative      # 负能量（伤害活物/治疗不死）
force         # 力场（无法抵抗）
```

### 3.4 伤害减免（DR）

3.5e 的物理伤害减免机制：

```
DR X/material_or_alignment

示例：
DR 5/magic      → 非魔法武器的物理伤害减 5
DR 10/silver    → 非银制武器的物理伤害减 10
DR 10/good      → 非善良阵营武器的物理伤害减 10
DR 15/—         → 所有物理伤害减 15（无法绕过）
```

DR 只对物理伤害（斩/刺/钝）生效，不影响能量伤害。

**战棋适配**：
- 首版支持 `DR X/magic` 和 `DR X/—`
- 后续扩展 `DR X/silver`、`DR X/cold_iron` 等材质绕过
- DR 值写在 `BattleUnitState` 的运行时快照中

### 3.5 能量抗性（Energy Resistance）

```
resistance fire 10   → 火焰伤害减 10
immunity cold        → 寒冷伤害完全免疫
vulnerability fire   → 火焰伤害 ×1.5
```

**与现有系统的关系**：
- 现有 `fire_resistance / freeze_resistance / lightning_resistance` 等百分比抗性需要迁移为整数减免值或保留百分比但定义清晰的换算
- 建议：采用 3.5e 的整数减免模型（`resistance X`），废弃百分比抗性

### 3.6 法术抗性（Spell Resistance / SR）

```
caster_check = d20 + caster_level
if caster_check >= target_SR:
    spell_affects_target
else:
    spell_resisted
```

- SR 是对魔法的整体抵抗
- 不影响非魔法攻击
- 部分法术标注"SR: No"则无视法术抗性

---

## 四、豁免检定（Saving Throws）

### 4.1 三类豁免

| 豁免 | 对抗 | 关键属性 |
|------|------|---------|
| **强韧（Fortitude）** | 毒素、疾病、石化、即死 | 体质 |
| **反射（Reflex）** | 火球、闪电、范围伤害 | 敏捷 |
| **意志（Will）** | 魅惑、恐惧、精神控制 | 感知/意志 |

### 4.2 豁免加值计算

```
save_bonus = base_save + ability_mod + magic_bonus + other

base_save 分两档：
  good_save:  +floor(level/2) + 2
  poor_save:  +floor(level/3)
```

| 职业组 | 强韧 | 反射 | 意志 |
|--------|------|------|------|
| 战士/圣骑/野蛮人 | Good | Poor | Poor |
| 游侠 | Good | Good | Poor |
| 牧师/德鲁伊 | Good | Poor | Good |
| 盗贼/吟游 | Poor | Good | Poor |
| 武僧 | Good | Good | Good |
| 法师/术士 | Poor | Poor | Good |

### 4.3 豁免掷骰

```
d20 + save_bonus >= DC → 豁免成功

DC 来源：
  法术 DC = 10 + 法术环级 + 施法属性修正
  毒素 DC = 毒素固定值
  能力 DC = 10 + HD/2 + 属性修正
```

### 4.4 豁免与本项目的衔接

- `ProfessionDef` 新增 `fort_save / ref_save / will_save: StringName`（`good / poor`）
- 豁免加值作为派生属性注入 `AttributeSnapshot`
- 技能的 `CombatEffectDef` 对需要豁免的效果声明 `save_type` + `save_dc_formula`
- 豁免成功的效果：减半伤害 / 无效果 / 部分效果（由 `save_effect` 字段控制）

---

## 五、动作经济

### 5.1 3.5e 原版动作类型

| 动作类型 | 每轮次数 | 典型行为 |
|---------|---------|---------|
| 整轮动作 | 1 | 完全攻击、冲锋、撤退 |
| 标准动作 | 1 | 单次攻击、施法、使用物品 |
| 移动动作 | 1 | 移动、起立、拔武器 |
| 迅捷动作 | 1 | 快速施法、部分能力 |
| 自由动作 | 多次 | 说话、丢东西 |
| 直觉动作 | 1/轮（非你回合） | 反制法术 |

标准动作 + 移动动作 = 一个整轮。

### 5.2 战棋 AP 适配

本项目用 `action_points` 统一表达动作经济，对 3.5e 动作做如下映射：

| 3.5e 动作 | AP 消耗 | 说明 |
|-----------|---------|------|
| 移动 1 格 | 1 AP | 困难地形 2 AP/格 |
| 单次近战攻击 | 2 AP | 标准动作 |
| 单次远程攻击 | 2 AP | 标准动作 |
| 迭代攻击（额外一击） | 1 AP | 每多打一次加 1 AP |
| 施法 | 2-4 AP | 取决于法术施放时间 |
| 5 尺快步 | 0 AP | 但本轮不能再移动 |
| 冲锋 | 全部剩余 AP | 直线移动+攻击，+2攻击/-2AC |
| 战斗机动（绊摔/缴械等） | 2 AP | 替代标准攻击 |
| 使用物品 | 2 AP | 喝药水等 |
| 起立（倒地后） | 2 AP | 触发借机攻击 |
| 切换武器 | 1 AP | 收起+拔出 |
| 防御姿态 | 2 AP | 全防御 +4 AC |

#### AP 基础值

```
base_ap = 6（大部分角色）

AP 可由以下来源增加：
  agility 派生：+floor(agility / 3)
  专长：Combat Reflexes 等
  职业能力：武僧额外动作等
  状态：加速 +2 AP
```

### 5.3 借机攻击（Attack of Opportunity / AoO）

3.5e 核心机制——离开敌人威胁范围时会被免费打一下。

```
触发条件：
  - 从威胁格移出（非 5 尺快步）
  - 在威胁范围内施法
  - 在威胁范围内使用远程武器
  - 在威胁范围内起立

每轮 AoO 次数 = 1（默认）
  Combat Reflexes 专长：= 1 + Dex_mod
```

**战棋适配**：
- 每个近战单位在相邻格有"威胁区"
- 敌方从威胁格移出时触发一次免费近战攻击（不消耗 AP）
- `reach` 武器威胁范围为 2 格
- AoO 不消耗 AP 但消耗"反应次数"（默认 1 次/轮）

---

## 六、武器系统

### 6.1 武器数据模型（WeaponProfile）

```
weapon_category: simple / martial / exotic    # 简易/军用/异种
weapon_type: melee / ranged / thrown           # 近战/远程/投掷
handedness: light / one_handed / two_handed    # 轻型/单手/双手
damage_dice_count: int                         # 伤害骰数量
damage_dice_sides: int                         # 伤害骰面数
damage_type: slashing / piercing / bludgeoning # 伤害类型（可多选）
crit_range_min: int                            # 暴击威胁起始值（默认 20）
crit_multiplier: int                           # 暴击倍率（默认 ×2）
range_increment: int                           # 射程增量（近战为 0）
reach: bool                                    # 是否长兵器
attack_ability: strength / dexterity / finesse # 攻击加值来源
damage_ability: strength / none                # 伤害加值来源
enchantment_bonus: int                         # 魔法武器附魔 +X
weapon_tags: Array[StringName]                 # 特殊标签
proficiency_group: StringName                  # 熟练分组
weapon_material: normal / silver / cold_iron / adamantine / mithral  # 材质
```

### 6.2 武器标签（weapon_tags）

| 标签 | 效果 |
|------|------|
| `finesse` | 攻击掷骰可用 Dex 代替 Str |
| `light` | 双持副手惩罚减轻 |
| `thrown` | 可投掷，用 Str 加伤害 |
| `reach` | 威胁范围 2 格，但不能打邻格 |
| `double` | 双头武器，可当双持 |
| `loading` | 需要一个动作装填 |
| `monk` | 武僧可用连打 |
| `trip` | 可用于绊摔 |
| `disarm` | 缴械加成 |
| `brace` | 对冲锋敌人伤害翻倍 |
| `versatile` | 单手/双手皆可，双手时伤害骰升一级 |

### 6.3 种子武器库

#### 简易武器

| 武器 | 骰 | 类型 | 暴击 | 标签 |
|------|-----|------|------|------|
| 匕首 | 1d4 | P/S | 19-20/×2 | finesse, light, thrown |
| 木棍 | 1d6 | B | 20/×2 | — |
| 轻弩 | 1d8 | P | 19-20/×2 | loading |
| 标枪 | 1d6 | P | 20/×2 | thrown |
| 锤矛 | 1d6 | B | 20/×2 | — |

#### 军用武器

| 武器 | 骰 | 类型 | 暴击 | 标签 |
|------|-----|------|------|------|
| 长剑 | 1d8 | S | 19-20/×2 | versatile |
| 巨剑 | 2d6 | S | 19-20/×2 | two_handed |
| 战斧 | 1d8 | S | 20/×3 | versatile |
| 大斧 | 1d12 | S | 20/×3 | two_handed |
| 弯刀 | 1d6 | S | 18-20/×2 | finesse, light |
| 重剑 | 1d10 | S | 19-20/×2 | two_handed |
| 长矛 | 1d8 | P | 20/×3 | reach, brace |
| 戟 | 1d10 | S/P | 20/×3 | reach, trip, two_handed |
| 战锤 | 1d8 | B | 20/×3 | — |
| 大锤 | 2d6 | B | 20/×3 | two_handed |
| 链枷 | 1d8 | B | 20/×2 | trip, disarm |
| 短弓 | 1d6 | P | 20/×3 | — |
| 长弓 | 1d8 | P | 20/×3 | — |
| 重弩 | 1d10 | P | 19-20/×2 | loading |
| 短剑 | 1d6 | P | 19-20/×2 | finesse, light |
| 细剑 | 1d6 | P | 18-20/×2 | finesse |

#### 异种武器

| 武器 | 骰 | 类型 | 暴击 | 标签 |
|------|-----|------|------|------|
| 双刃剑 | 1d8/1d6 | S | 19-20/×2 | double, two_handed |
| 链刃 | 2d4 | S | 19-20/×2 | reach, trip, disarm |
| 手弩 | 1d4 | P | 19-20/×2 | loading, light |

### 6.4 魔法武器附魔

```
附魔等级 +1 ~ +5：
  攻击加值 +X
  伤害加值 +X
  视为魔法武器（绕过 DR/magic）

等效附魔（占用附魔等级但不加基础加值）：
  +1 等效：Keen（暴击范围翻倍）、Flaming（+1d6 火焰）
  +2 等效：Flaming Burst（暴击额外火焰骰）、Wounding（每命中 1 体质伤害）
  +3 等效：Speed（每轮额外一次攻击）、Keen + Flaming 组合
  +5 等效：Vorpal（暴击斩首即死）

总等效附魔上限 = +10
示例：+3 Keen Flaming Longsword = +3 基础 + 1(Keen) + 1(Flaming) = 等效 +5
```

---

## 七、护甲系统

### 7.1 护甲数据模型（ArmorProfile）

```
armor_bonus: int            # 护甲 AC 加值
max_dex_bonus: int          # 敏捷上限
armor_check_penalty: int    # 技能检定惩罚
arcane_spell_failure: int   # 奥术失败率 %
armor_category: light / medium / heavy
armor_material: normal / mithral / adamantine / darkwood
enchantment_bonus: int      # 魔法护甲附魔 +X
special_properties: Array[StringName]  # 特殊属性
```

### 7.2 盾牌数据模型（ShieldProfile）

```
shield_bonus: int           # 盾牌 AC 加值
max_dex_bonus: int          # 通常无限（塔盾除外）
armor_check_penalty: int
arcane_spell_failure: int
shield_category: buckler / light / heavy / tower
enchantment_bonus: int
special_properties: Array[StringName]
```

### 7.3 魔法护甲附魔

```
基础附魔 +1 ~ +5：
  AC 加值 +X（与基础 armor_bonus 叠加）

等效附魔：
  +1 等效：Shadow（潜行加成）、Slick（脱逃加成）
  +2 等效：Moderate Fortification（25% 暴击免疫）
  +3 等效：Heavy Fortification（75% 暴击免疫）、Ghost Touch
  +5 等效：Etherealness（以太化）
```

### 7.4 护甲熟练与职业限制

```
# 穿不熟练的护甲：
  攻击掷骰受 armor_check_penalty
  不能施法（奥术职业）

# 职业护甲熟练
  战士/圣骑：全甲 + 全盾
  牧师：全甲 + 轻盾/重盾
  游侠/野蛮人：轻甲 + 中甲 + 盾
  盗贼/吟游：轻甲
  武僧：无甲（有 Wis 加 AC + 职业 AC 加值）
  法师/术士：无甲
```

---

## 八、体型系统

### 8.1 体型等级

| 体型 | 攻击/AC修正 | 格数占位 | 威胁范围 | 擒抱修正 | 典型生物 |
|------|------------|---------|---------|---------|---------|
| 超微型 Fine | +8 | 1/4 格 | 0 | -16 | 虫子 |
| 微型 Diminutive | +4 | 1/2 格 | 0 | -12 | 蟾蜍 |
| 超小型 Tiny | +2 | 1 格 | 0 | -8 | 猫 |
| 小型 Small | +1 | 1 格 | 1 格 | -4 | 半身人 |
| 中型 Medium | +0 | 1 格 | 1 格 | +0 | 人类 |
| 大型 Large | -1 | 2×2 格 | 2 格 | +4 | 马/食人魔 |
| 超大型 Huge | -2 | 3×3 格 | 3 格 | +8 | 巨人 |
| 巨型 Gargantuan | -4 | 4×4 格 | 4 格 | +12 | 龙 |
| 超巨型 Colossal | -8 | 5×5+ | 5 格 | +16 | 泰坦 |

### 8.2 体型与本项目的衔接

- `BattleUnitState.body_size` 已有基础，需扩展为正式 `size_category` 枚举
- 体型影响：攻击修正、AC 修正、擒抱修正、占位格数、威胁范围、隐藏难度
- `footprint_size` 与 `occupied_coords` 已有实现，直接映射到体型等级表

---

## 九、战斗机动（Combat Maneuvers）

### 9.1 机动列表

| 机动 | 对抗 | 效果 | AP 消耗 |
|------|------|------|---------|
| **绊摔 Trip** | 攻击掷骰 vs CMD | 目标倒地 | 2（替代攻击） |
| **缴械 Disarm** | 攻击掷骰 vs CMD | 目标掉武器 | 2 |
| **冲撞 Bull Rush** | 攻击掷骰 vs CMD | 推后 1+ 格 | 2 |
| **擒抱 Grapple** | 攻击掷骰 vs CMD | 擒住，双方受限 | 2 |
| **破武 Sunder** | 攻击掷骰 vs AC | 损坏装备 | 2 |
| **闯越 Overrun** | 攻击掷骰 vs CMD | 穿过敌人 | 移动中 |

### 9.2 CMB / CMD（借用 Pathfinder 简化）

```
CMB = BAB + Str_mod + size_mod       # 战斗机动加值
CMD = 10 + BAB + Str_mod + Dex_mod + size_mod  # 战斗机动防御
```

**战棋适配**：
- 战斗机动以技能形式实装（`skill_type: combat_maneuver`）
- 机动命中走攻击掷骰 vs CMD，不走 AC
- 成功后执行效果（倒地状态/强制位移/缴械状态等）
- 某些武器有机动加成（`trip` / `disarm` 标签）

---

## 十、状态效果系统

### 10.1 核心状态表

#### 移动限制

| 状态 | 效果 | 持续 | 豁免 |
|------|------|------|------|
| **倒地 Prone** | -4 近战攻击，+4 被近战命中，-4 被远程命中 | 直到花 AP 起立 | — |
| **定身 Paralyzed** | 不能行动，AC 视为措手不及 | 持续时间 | Will |
| **纠缠 Entangled** | -2 攻击，-4 Dex，半速 | 持续时间 | Ref |
| **减速 Slowed** | 半速，-1 攻击，-1 AC，-1 Ref | 持续时间 | Fort |
| **束缚 Pinned** | 不能行动（擒抱中） | 直到脱离 | — |

#### 精神状态

| 状态 | 效果 | 持续 | 豁免 |
|------|------|------|------|
| **恐惧 Frightened** | -2 攻击/豁免/技能，必须远离恐惧源 | 持续时间 | Will |
| **恐慌 Panicked** | 恐惧+丢东西逃跑 | 持续时间 | Will |
| **魅惑 Charmed** | 视施法者为友方 | 持续时间 | Will |
| **困惑 Confused** | 随机行为 | 持续时间 | Will |
| **震慑 Stunned** | 不能行动，丢 Dex AC，-2 AC | 1 轮 | 通常无 |

#### 身体状态

| 状态 | 效果 | 持续 | 豁免 |
|------|------|------|------|
| **失明 Blinded** | -2 AC，丢 Dex AC，半速，-4 大部分检定 | 持续时间 | Fort |
| **耳聋 Deafened** | -4 先攻，20% 施法失败 | 持续时间 | Fort |
| **恶心 Nauseated** | 只能移动，不能攻击/施法 | 持续时间 | Fort |
| **反胃 Sickened** | -2 攻击/伤害/豁免/技能 | 持续时间 | Fort |
| **疲乏 Fatigued** | -2 Str/Dex，不能冲锋/奔跑 | 休息恢复 | — |
| **力竭 Exhausted** | -6 Str/Dex，半速 | 长休恢复 | — |
| **中毒 Poisoned** | 属性伤害 | 多次豁免 | Fort |
| **流血 Bleeding** | 每轮 X 伤害 | 治疗/检定止血 | — |
| **石化 Petrified** | 变石头 | 直到解除 | Fort |

#### 增益状态

| 状态 | 效果 | 来源 |
|------|------|------|
| **加速 Haste** | +1 攻击，+1 AC，+1 Ref，额外一次攻击 | 法术 |
| **减速 Slow** | 上述反转 | 法术 |
| **祝福 Bless** | +1 士气加值于攻击和恐惧豁免 | 法术 |
| **牛之力量 Bull's Strength** | +4 Str（增强加值） | 法术 |
| **猫之优雅 Cat's Grace** | +4 Dex | 法术 |
| **护盾术 Shield** | +4 盾牌加值 | 法术 |
| **法术护甲 Mage Armor** | +4 护甲加值 | 法术 |
| **虔诚护盾 Shield of Faith** | +2~+5 偏斜加值 | 法术 |
| **树皮术 Barkskin** | +2~+5 天生护甲增强 | 法术 |

### 10.2 状态与本项目的衔接

- 建立 `StatusDef` 注册表，每个状态声明：ID、显示名、效果列表、持续类型、豁免类型
- 效果类型枚举：`attack_mod / ac_mod / save_mod / attribute_mod / disable / forced_behavior / dot / hot`
- 状态持续类型：`rounds / until_removed / until_rest / until_save`
- `BattleUnitState.status_effects` 已有基础结构，扩展为正式 `StatusEffectEntry`

---

## 十一、法术系统框架

### 11.1 法术核心属性

```
spell_level: int                    # 法术环级 0-9
spell_school: abjuration / conjuration / divination / enchantment / evocation / illusion / necromancy / transmutation
casting_time: standard / full_round / 1_minute / ...
range_category: personal / touch / close / medium / long
area_type: none / burst / cone / cylinder / line / spread
duration_type: instantaneous / rounds / minutes / hours / permanent / concentration
saving_throw: none / fortitude / reflex / will
save_effect: none / half / negates / partial
spell_resistance: yes / no
components: verbal / somatic / material / focus / divine_focus
```

### 11.2 法术 DC

```
spell_DC = 10 + spell_level + casting_ability_modifier + feat_bonus
```

### 11.3 施法者等级（Caster Level）

```
caster_level = class_level_in_casting_class

效果：
  - 法术持续时间
  - 法术伤害骰数量（火球 1d6/CL，最高 10d6）
  - 克服法术抗性的检定
  - 解除魔法的检定
```

### 11.4 法术位系统

```
# 每日法术位（已知/已准备法术）
# 法师：准备制（每天准备固定数量法术）
# 术士：自发施法（已知法术少，但每日次数多）

# 战棋适配：
  - 用 MP 取代法术位（简化管理）
  - 法术环级决定 MP 消耗：cost = spell_level * 2 + 1
  - 0 环法术（戏法）无限使用
  - 保留法术位作为可选高级规则
```

### 11.5 法术类型对战棋的映射

| 法术类型 | 战棋表现 |
|---------|---------|
| 攻击射线 | 单体远程攻击检定 |
| 范围即时伤害 | 选点→范围→逐目标豁免→半伤 |
| 接触攻击 | 近战接触 AC 检定 |
| 增益持续 | 自身/友方 buff 状态 |
| 召唤 | 在指定格产生可控单位 |
| 控制 | 单体/范围豁免，失败获得控制状态 |
| 治疗 | 友方回复，不走攻击检定 |
| 地形法术 | 在区域放置持续地形效果 |

---

## 十二、战场规则

### 12.1 掩体（Cover）

```
软掩体（人体等）：+4 AC
硬掩体（墙角/障碍）：+4 AC，+2 Ref
完全掩体：无法被直接攻击
```

**战棋适配**：
- `BattleCellState` 已有 edge / low_wall 概念
- 半墙提供 +4 AC
- 完整墙体阻断视线
- 友方单位提供 +4 软掩体（对远程攻击）

### 12.2 高地（Elevation）

```
高地攻击者：+1 攻击
低地攻击者：-1 攻击

高度差 ≥ 2 层时：
  高地：+2 攻击
  低地：-2 攻击，且近战只能攻击 reach 范围内
```

`BattleCellState` 已有 `base_height + height_offset`，可直接读取双方高度差。

### 12.3 包夹（Flanking）

```
条件：两个友方近战单位分别在敌人对角线两侧
效果：+2 攻击加值
盗贼额外效果：触发偷袭伤害
```

**战棋适配**：
- 检测目标单位周围格是否有两个不同友方形成对角
- 格子判定比圆桌更精确，天然适合战棋

### 12.4 困难地形

```
效果：移动消耗翻倍（2 AP/格）
来源：碎石、沼泽、灌木、法术效果区域
不影响：飞行单位、无视地形的能力
```

### 12.5 视线与射程

```
视线阻断：完整墙体、大型不透明物体
射程递减：每超一个射程增量 -2 攻击
最远射程：投掷 5 个增量，射击 10 个增量
暗视：部分种族/怪物在黑暗中正常视野
```

---

## 十三、先攻与回合

### 13.1 先攻掷骰

```
initiative = d20 + dex_mod + feat_bonus + misc

先攻决定行动顺序
平局：dex 高的先行动，仍平局则掷骰
```

### 13.2 突袭轮

```
条件：一方对另一方有察觉优势
效果：被突袭方只能做移动或标准动作，不能完全攻击
     被突袭方视为措手不及（丢 Dex AC）
```

### 13.3 与本项目 Timeline 的衔接

- 当前 `BattleTimelineState` 已实现行动顺序系统
- 先攻掷骰可替代当前按单位 `action_threshold` 入队的节奏
- 先攻值 = `d20 + dex_mod + feat_bonus`
- 突袭轮：首轮行动前被突袭单位处于措手不及状态

---

## 十四、偷袭 / 精准打击

### 14.1 盗贼偷袭（Sneak Attack）

```
条件（满足任一）：
  - 目标措手不及
  - 攻击者包夹目标
  - 目标被剥夺 Dex AC（震慑/定身/失明等）

额外伤害：+Xd6（X = ceil(rogue_level / 2)）
  L1: +1d6, L3: +2d6, L5: +3d6 ... L19: +10d6

限制：
  - 仅近战或 30 尺内远程
  - 不对暴击免疫生物生效
  - 不对看不到的目标生效（需要明确看到要害）
```

### 14.2 战棋适配

- 偷袭作为盗贼职业被动能力，在满足条件时自动追加
- `BattleAttackResolver` 在计算伤害时检查偷袭条件
- 包夹检测复用 §12.3 的格子判定
- 措手不及检测复用状态系统

---

## 十五、专长系统框架

### 15.1 战斗相关核心专长

| 专长 | 前置 | 效果 |
|------|------|------|
| **强力攻击 Power Attack** | Str 13, BAB +1 | 攻击-X，伤害+X（双手+2X） |
| **顺势斩 Cleave** | Power Attack | 击倒后对邻近敌人免费攻击 |
| **大顺势斩 Great Cleave** | Cleave, BAB +4 | 击倒后无限顺势斩 |
| **精准射击 Precise Shot** | Point Blank Shot | 射击不受近战惩罚 |
| **近距射击 Point Blank Shot** | — | 30 尺内 +1 攻击/伤害 |
| **多重射击 Manyshot** | BAB +6, PBS, Rapid Shot | 一次射多箭 |
| **快速射击 Rapid Shot** | Dex 13, PBS | 额外一次远程攻击（-2全部） |
| **双武器战斗 TWF** | Dex 15 | 减轻双持惩罚 |
| **精通先攻 Improved Initiative** | — | +4 先攻 |
| **战斗反射 Combat Reflexes** | — | 额外 AoO 次数 = Dex mod |
| **精通绊摔 Improved Trip** | Int 13, Combat Expertise | 绊摔不触发 AoO，成功后免费攻击 |
| **精通缴械 Improved Disarm** | Int 13, Combat Expertise | 缴械不触发 AoO |
| **猛力冲锋 Improved Bull Rush** | Str 13, Power Attack | 冲撞不触发 AoO |
| **要害打击 Improved Critical** | BAB +8 | 暴击威胁范围翻倍 |
| **武器专攻 Weapon Focus** | BAB +1 | 指定武器 +1 攻击 |
| **高等武器专攻 Greater Weapon Focus** | Fighter 8, WF | 指定武器再 +1 攻击 |
| **武器专精 Weapon Specialization** | Fighter 4, WF | 指定武器 +2 伤害 |
| **高等武器专精 Greater Weapon Specialization** | Fighter 12, GWF | 指定武器再 +2 伤害 |
| **闪避 Dodge** | Dex 13 | +1 闪避 AC |
| **灵活移动 Mobility** | Dodge | 移动中 +4 AC 对 AoO |
| **跳跃攻击 Spring Attack** | Mobility, BAB +4 | 移动中攻击后继续移动 |
| **旋风攻击 Whirlwind Attack** | Spring Attack, BAB +4 | 攻击所有邻近敌人 |

### 15.2 与本项目的衔接

- 专长映射为 `SkillDef` 中 `skill_type = "passive"` 的被动技能
- 职业授予专长通过 `ProfessionGrantedSkill` 机制
- 通用专长通过技能书学习
- 专长效果通过 `attribute_modifiers` + `combat_tags` 注入战斗链路
- 复杂专长（如强力攻击的可变值、顺势斩的触发条件）需要在 `BattleAttackResolver` 中实装专门逻辑

---

## 十六、经济与装备成长曲线

### 16.1 财富随等级（Wealth By Level）

3.5e 对各等级角色预期总财富有严格指引：

| 等级 | 预期总财富 (gp) | 说明 |
|------|----------------|------|
| 1 | 0 | 起始装备 |
| 2 | 900 | |
| 3 | 2,700 | |
| 4 | 5,400 | 首件 +1 武器 |
| 5 | 9,000 | |
| 6 | 13,000 | +1 护甲 |
| 7 | 19,000 | |
| 8 | 27,000 | +2 武器或多件+1 |
| 9 | 36,000 | |
| 10 | 49,000 | +2 甲+盾+附魔 |
| 11 | 66,000 | |
| 12 | 88,000 | +3 武器或+2+属性附魔 |
| 13 | 110,000 | |
| 14 | 150,000 | +3 甲+3 盾+多件杂项 |
| 15 | 200,000 | +4 武器 |
| 16 | 260,000 | |
| 17 | 340,000 | +4/+5 组合 |
| 18 | 440,000 | |
| 19 | 580,000 | |
| 20 | 760,000 | 全身 +5 + 各种 |

### 16.2 魔法物品价格公式

```
武器：基础价 + (附魔等效等级)² × 2000 gp + 基础武器价
  +1 长剑：2000 + 315 = 2315 gp
  +3 长剑：18000 + 315 = 18315 gp
  +5 长剑：50000 + 315 = 50315 gp

护甲：基础价 + (附魔等效等级)² × 1000 gp + 基础护甲价
  +1 全身板甲：1000 + 1500 = 2500 gp
  +5 全身板甲：25000 + 1500 = 26500 gp

戒指/护符/杂项：按效果定价
  防护戒指 +1：2000 gp
  天生护甲护符 +2：8000 gp
  抗力斗篷 +3：9000 gp
```

### 16.3 与本项目的衔接

- 金币系统由 `PartyState.gold` 承载
- 装备掉落/商店按等级对应的财富曲线投放
- 魔法装备的 `enchantment_bonus` 是 AC/攻击等级碾压的主要推手
- 信仰系统的供奉金币消耗与装备投资形成资源竞争——这是有意义的策略选择

---

## 十七、属性体系全景

### 17.1 六大属性（3.5e 标准）

| 属性 | 缩写 | 影响 |
|------|------|------|
| **力量 Strength** | Str | 近战攻击/伤害，负重，攀爬/游泳 |
| **敏捷 Dexterity** | Dex | 远程攻击，AC，Ref 豁免，先攻，潜行 |
| **体质 Constitution** | Con | HP，Fort 豁免，专注 |
| **智力 Intelligence** | Int | 技能点，法师施法属性，知识 |
| **感知 Wisdom** | Wis | Will 豁免，牧师/德鲁伊施法属性，察觉 |
| **魅力 Charisma** | Cha | 术士施法属性，交涉，圣骑士能力 |

#### 属性修正

```
modifier = floor((score - 10) / 2)

Score:  1   4   6   8  10  12  14  16  18  20  24  30
Mod:   -5  -3  -2  -1   0  +1  +2  +3  +4  +5  +7 +10
```

### 17.2 与本项目属性的映射

当前项目用小整数基础属性：`strength / constitution / intelligence / willpower / agility / perception`。

建议的映射关系：

| 3.5e 属性 | 项目属性 | 修正公式（适配小整数） |
|-----------|---------|---------------------|
| Strength | strength | `str_mod = strength - 3`（基础值约 1-8） |
| Dexterity | agility | `dex_mod = agility - 3` |
| Constitution | constitution | `con_mod = constitution - 3` |
| Intelligence | intelligence | `int_mod = intelligence - 3` |
| Wisdom | willpower | `wis_mod = willpower - 3`（或 perception） |
| Charisma | 新增或复用 perception | `cha_mod = perception - 3` |

**或者**采用直接缩放：

```
# 把项目小整数乘以 2 映射到 3.5e 10-based 标度
effective_score = base_value * 2 + 6
modifier = floor((effective_score - 10) / 2) = base_value - 2
```

这样 `strength = 5` → effective 16 → mod +3，符合 3.5e 中等偏上的水准。

### 17.3 派生属性全表

由六大属性 + 等级 + 职业 + 装备派生出的战斗属性：

```
# 攻击
base_attack_bonus:  character_level × bab_rate
melee_attack:       BAB + str_mod + size_mod + weapon_enchant
ranged_attack:      BAB + dex_mod + size_mod + weapon_enchant
cmb:                BAB + str_mod + size_mod

# 防御
armor_class:        8 + armor + shield + dex_mod(capped) + size + natural + deflection + dodge
touch_ac:           8 + dex_mod + size + deflection + dodge
flat_footed_ac:     AC - dex_mod - dodge
cmd:                10 + BAB + str_mod + dex_mod + size_mod

# 豁免
fortitude_save:     base_fort + con_mod + magic_bonus
reflex_save:        base_ref + dex_mod + magic_bonus
will_save:          base_will + wis_mod + magic_bonus

# 资源
hp_max:             hit_dice_total + (con_mod × level)
mp_max:             派生（法系专属）
aura_max:           派生（武系/功法专属）
stamina_max:        派生（体力系）
action_points:      6 + agility_bonus

# 先攻
initiative:         dex_mod + feat_bonus + misc

```

---

## 十八、怪物与敌方单位

### 18.1 怪物数据模型

```
monster_id: StringName
display_name: String
size_category: StringName
hit_dice: int                    # 生命骰数
hit_dice_sides: int              # 生命骰面数（d8/d10/d12）
base_attack_bonus: int           # 直接给 BAB，不走职业曲线
base_attributes: Dictionary      # 六维基础属性，AC 从敏捷修正派生
ac_components: Dictionary        # armor/shield/natural/deflection/dodge 等组件，不直接给最终 AC
touch_ac: int
flat_footed_ac: int
fortitude_save: int
reflex_save: int
will_save: int
damage_reduction: String         # "5/magic" 格式
spell_resistance: int            # 0 = 无
energy_resistances: Dictionary   # {"fire": 10, "cold": 5}
energy_immunities: Array[String]
natural_attacks: Array[Dictionary]  # 天然武器列表
special_abilities: Array[StringName]
crit_immunity: bool
sneak_attack_immunity: bool
challenge_rating: float          # CR
```

### 18.2 天然武器

```
# 怪物可以有多种天然攻击
natural_attacks = [
    {type: "claw", count: 2, dice: "1d6", damage_type: "slashing", attack_bonus: +8},
    {type: "bite", count: 1, dice: "1d8", damage_type: "piercing", attack_bonus: +8},
    {type: "tail", count: 1, dice: "1d6", damage_type: "bludgeoning", attack_bonus: +3},  # 次要攻击 -5
]
```

### 18.3 挑战等级（CR）与遭遇设计

```
CR = party_level 时为"标准"遭遇
CR = party_level + 2 时为"困难"遭遇
CR = party_level + 4 时为"致命"遭遇
CR = party_level - 2 时为"简单"遭遇

4 个 CR X 怪物 ≈ 1 个 CR X+4 遭遇
```

---

## 十九、数值平衡参考线

### 19.1 同级期望命中率（裸数值，不计 buff/战场）

以下为"同 rank 战士 vs 同 rank 战士"的**裸**命中率（仅 BAB + Str + 武器附魔，不含专长/buff/包夹）：

| rank | 攻击方加值 | 防御方 AC | 需掷 | 命中率 |
|------|-----------|----------|------|--------|
| 1 | +4 (BAB0+Str3+MW1) | 17 (甲5+盾2+Dex0) | 13+ | 40% |
| 5 | +7 (BAB2+Str4+E1) | 21 (+1甲盾+ring) | 14+ | 35% |
| 10 | +12 (BAB5+Str5+E2) | 25 (+2全套) | 13+ | 40% |
| 15 | +16 (BAB7+Str6+E3) | 30 (+3全套+misc) | 14+ | 35% |
| 20 | +21 (BAB10+Str7+E4) | 38 (+4/5全套) | 17+ | 20% |

#### 中段命中率低谷是有意取舍

由于 BAB 顶部从 +20 砍到 +10 而 AC 通胀曲线**未同步压扁**，rank 5-20 的同级裸命中率落在 20-40% 区间，比 3.5e 原案（45-70%）低一档。这是**有意的设计选择**：

- 高级战斗**强制依赖战术加值**：专长（Weapon Focus +1×2）、buff（Bless/Haste +2）、战场（包夹 +2、高地 +1）累加可拉到 +5~+8，把命中率拉回 50%+
- rank 20 满配满 buff：+21+6 = +27 vs AC 38 → 需 11+ → **50%**
- 鼓励玩家组队配合（牧师 buff、盗贼侧位）而非单挑硬刚
- 战斗节奏自然变长，但每一刀都更有"攒条件"的策略含量

### 19.2 跨级压制表（裸数值）

| 攻击方 rank | 防御方 rank | 攻击加值 | 目标 AC | 需掷 | 命中率 |
|-----------|-----------|---------|--------|------|--------|
| 10 | 1 | +12 | 17 | 5+ | **80%** |
| 1 | 10 | +4 | 25 | 21+ | **不可能（仅 nat20 5%）** |
| 10 | 5 | +12 | 21 | 9+ | **60%** |
| 5 | 10 | +7 | 25 | 18+ | **15%** |
| 15 | 10 | +16 | 25 | 9+ | **60%** |
| 10 | 15 | +12 | 30 | 18+ | **15%** |
| 20 | 1 | +21 | 17 | nat1 only | **95%** |
| 1 | 20 | +4 | 38 | 34+ | **不可能** |

**压迫感保留**：跨 5+ 级时低打高仅靠 nat20 摸鱼（5%/刀），高打低 60-95% 必中——即使 BAB 顶部砍半，等级碾压感仍在。

#### 与 3.5e 原案的差距

| 对拍点 | 3.5e 原案 | 本项目（BAB cap +10） |
|---|---|---|
| 同级 r20 裸命中 | 70% | 20%（满 buff 50%） |
| 同级 r10 裸命中 | 55% | 40% |
| L10 打 L1 | 95% | 80% |
| L5 vs L10 | 20% | 15% |

整体口径：**同级偏低 + 跨级压制略平 + 战术依赖更强**。这是把"加值堆栈整体压扁"换来的，与战棋的策略深度方向一致。

---

## 二十、与现有项目设计的联动

### 20.1 与成长系统（player_growth_system_plan.md）

- `character_level` 驱动 BAB、豁免、迭代攻击次数
- 核心技能升级时，属性增长方向影响 Str/Dex/Con 等修正 → 间接影响攻击和 AC
- 功法系统（meditation/cultivation）提供 MP/Aura 资源，驱动法术和特殊技能

### 20.2 与信仰系统（faith_system_plan.md）

- 主角多神叠加 → 3-4 倍总战力优势，映射为：
  - 额外属性加值（增强型，可与装备叠加）
  - 特殊被动能力（DR、SR、特殊豁免）
  - 信仰专属法术/技能
- 信仰供奉消耗金币 → 与装备购买竞争资源，玩家需要权衡

### 20.3 与装备系统（equipment_system_plan.md）

- `ItemDef` 扩展 `WeaponProfile` / `ArmorProfile` / `ShieldProfile` 子资源
- 魔法附魔等级是等级碾压的核心推手
- 装备熟练度限制职业穿甲选择
- Phase 2 的多槽位占用天然支持双手武器和双持

### 20.4 与技能系统（skills_implementation_plan.md）

- **关键变更**：现有文档的 `THAC0 / 负 AC / d20` 命中口径需要全部替换为 `BAB / 升序 AC / d20`
- `CombatSkillDef.attack_roll_bonus` 语义不变，但基线从 THAC0 改为 BAB
- 技能 hit_rate 迁移方式改为：`attack_roll_bonus = old_hit_rate / 10`
- `BattleHitResolver` 的公式从 `required_roll = THAC0 - AC - bonus` 改为 `d20 + BAB + bonus >= AC`

### 20.5 迁移清单

需要修订的现有设计文档：

| 文档 | 修订内容 |
|------|---------|
| `skills_implementation_plan.md` | §4.2 命中属性从 THAC0/负AC 改为 BAB/升序AC |
| `skills_implementation_plan.md` | §4.3 命中公式改为 d20+attack_bonus >= AC |
| `player_growth_system_plan.md` | 锁定核心技能的命中强化改为 BAB bonus 而非 THAC0 修正 |
| `../discussions/dnd_weapon_system_initial_plan.md` | 2E bridge 删除，改为纯 3.5e |
| `equipment_system_plan.md` | 补充 ArmorProfile/ShieldProfile/WeaponProfile 子资源定义 |

---

## 二十一、实装分期建议

### Phase 1：攻防闭环

- BAB 三档曲线 + 属性修正 → `attack_bonus`
- AC 组件化（armor + shield + dex_mod）
- `d20 + attack_bonus >= AC` 命中判定
- 武器伤害骰 + 能力修正
- 暴击威胁 + 确认 + 倍率
- 6 把种子武器 + 3 套种子护甲 + 2 面种子盾牌
- 武器/护甲子资源模型
- `BattleAttackResolver` 接受注入 RNG
- 基础战斗日志

### Phase 2：豁免与状态

- 三类豁免检定 + 职业基础豁免
- 核心状态表实装（倒地/震慑/流血/恐惧/加速/减速）
- 状态持续时间按轮推进
- DR / 能量抗性
- 物理伤害类型 3 分类

### Phase 3：动作经济与战场

- 迭代攻击
- 借机攻击 / 威胁区
- 掩体 AC 加值
- 高地修正
- 包夹 + 偷袭
- 冲锋（直线移动+攻击，+2攻击/-2AC）

### Phase 4：战斗机动与专长

- CMB / CMD 体系
- 绊摔 / 缴械 / 冲撞 / 擒抱
- 核心战斗专长（强力攻击、顺势斩、快速射击等）
- 双武器战斗

### Phase 5：法术与高级规则

- 法术 DC + 法术抗性
- 接触 AC / 措手不及 AC
- 范围法术 + 豁免半伤
- 魔法装备附魔系统
- 体型系统完整实装

### Phase 6：数值校准与内容填充

- 怪物 CR 系统
- 财富曲线投放
- 20 级全等级数值对拍
- 跨等级碾压验证
- 完整武器/护甲/盾牌种子库

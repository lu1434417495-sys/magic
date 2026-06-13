# Baldur's Gate 3 Weapon Types and Base Damage

> 本文档整理《博德之门 3》（Baldur's Gate 3）中常见基础武器类型、基础伤害骰、伤害类型与关键武器属性。  
> 这里的“基础伤害”只指武器本身的伤害骰，不包括属性调整值、熟练加值、附魔加值、毒药、火焰附伤、职业特性或其他额外伤害。

## 关联上下文单元

- CU-10：共享仓库、物品定义与装备基础流转
- CU-16：战斗状态模型、边规则、伤害、AI 规则层

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文是武器类型与基础伤害参考资料，不是当前仓库装备或伤害规则的真相源。

---

## 1. Core Classification

BG3 的武器可以按以下几层理解：

1. **Simple Weapons / 简易武器**  
   普通角色较容易掌握的基础武器。

2. **Martial Weapons / 军用武器**  
   需要专门战斗训练的武器，通常由战士、圣武士、游侠、野蛮人等武斗职业熟练掌握。

3. **Melee Weapons / 近战武器**  
   用于近距离攻击。

4. **Ranged Weapons / 远程武器**  
   用于远距离攻击。

5. **Damage Types / 伤害类型**  
   - **Bludgeoning / 钝击**
   - **Piercing / 穿刺**
   - **Slashing / 挥砍**

---

## 2. Simple Melee Weapons / 简易近战武器

| 中文名称 | English Name | Base Damage / 基础伤害 | Key Properties / 关键属性 |
|---|---|---:|---|
| 短棍 | Club | **1d4 Bludgeoning / 钝击** | Light / 轻型 |
| 匕首 | Dagger | **1d4 Piercing / 穿刺** | Finesse, Light, Thrown / 灵巧、轻型、投掷 |
| 手斧 | Handaxe | **1d6 Slashing / 挥砍** | Light, Thrown / 轻型、投掷 |
| 标枪 | Javelin | **1d6 Piercing / 穿刺** | Thrown / 投掷 |
| 轻锤 | Light Hammer | **1d4 Bludgeoning / 钝击** | Light, Thrown / 轻型、投掷 |
| 硬头锤 | Mace | **1d6 Bludgeoning / 钝击** | — |
| 镰刀 | Sickle | **1d4 Slashing / 挥砍** | Light / 轻型 |
| 长棍 / 法杖 | Quarterstaff | **1d6 / 1d8 Bludgeoning / 钝击** | Versatile / 两用 |
| 长矛 | Spear | **1d6 / 1d8 Piercing / 穿刺** | Versatile, Thrown / 两用、投掷 |
| 巨棒 | Greatclub | **1d8 Bludgeoning / 钝击** | Two-Handed / 双手 |

---

## 3. Simple Ranged Weapons / 简易远程武器

| 中文名称 | English Name | Base Damage / 基础伤害 | Key Properties / 关键属性 |
|---|---|---:|---|
| 轻弩 | Light Crossbow | **1d8 Piercing / 穿刺** | Two-Handed / 双手 |
| 短弓 | Shortbow | **1d6 Piercing / 穿刺** | Two-Handed / 双手 |

---

## 4. Martial Melee Weapons / 军用近战武器

| 中文名称 | English Name | Base Damage / 基础伤害 | Key Properties / 关键属性 |
|---|---|---:|---|
| 连枷 | Flail | **1d8 Bludgeoning / 钝击** | — |
| 钉头锤 | Morningstar | **1d8 Piercing / 穿刺** | — |
| 刺剑 | Rapier | **1d8 Piercing / 穿刺** | Finesse / 灵巧 |
| 弯刀 | Scimitar | **1d6 Slashing / 挥砍** | Finesse, Light / 灵巧、轻型 |
| 短剑 | Shortsword | **1d6 Piercing / 穿刺** | Finesse, Light / 灵巧、轻型 |
| 战镐 | War Pick | **1d8 Piercing / 穿刺** | — |
| 战斧 | Battleaxe | **1d8 / 1d10 Slashing / 挥砍** | Versatile / 两用 |
| 长剑 | Longsword | **1d8 / 1d10 Slashing / 挥砍** | Versatile / 两用 |
| 三叉戟 | Trident | **1d6 / 1d8 Piercing / 穿刺** | Versatile, Thrown / 两用、投掷 |
| 战锤 | Warhammer | **1d8 / 1d10 Bludgeoning / 钝击** | Versatile / 两用 |
| 长柄刀 | Glaive | **1d10 Slashing / 挥砍** | Two-Handed, Extra Reach / 双手、长触及 |
| 巨斧 | Greataxe | **1d12 Slashing / 挥砍** | Two-Handed / 双手 |
| 巨剑 | Greatsword | **2d6 Slashing / 挥砍** | Two-Handed / 双手 |
| 戟 | Halberd | **1d10 Slashing / 挥砍** | Two-Handed, Extra Reach / 双手、长触及 |
| 巨锤 | Maul | **2d6 Bludgeoning / 钝击** | Two-Handed / 双手 |
| 长枪 / 长矛枪 | Pike | **1d10 Piercing / 穿刺** | Two-Handed, Extra Reach / 双手、长触及 |

---

## 5. Martial Ranged Weapons / 军用远程武器

| 中文名称 | English Name | Base Damage / 基础伤害 | Key Properties / 关键属性 |
|---|---|---:|---|
| 手弩 | Hand Crossbow | **1d6 Piercing / 穿刺** | Light / 轻型 |
| 重弩 | Heavy Crossbow | **1d10 Piercing / 穿刺** | Two-Handed / 双手 |
| 长弓 | Longbow | **1d8 Piercing / 穿刺** | Two-Handed / 双手 |

---

## 6. Notes on Weapon Properties / 武器属性说明

### Light / 轻型

轻型武器通常适合双持或快速攻击思路，例如匕首、短剑、弯刀、手斧等。

### Finesse / 灵巧

灵巧武器可以使用 **Strength / 力量** 或 **Dexterity / 敏捷** 中较高者来进行攻击与伤害计算。  
常见灵巧武器包括匕首、刺剑、短剑、弯刀。

### Thrown / 投掷

投掷武器可以作为远程投掷攻击使用。  
常见投掷武器包括匕首、手斧、标枪、轻锤、长矛、三叉戟。

### Versatile / 两用

两用武器可以单手或双手使用。  
表格中的两个伤害值表示：

```text
单手伤害 / 双手伤害
```

例如：

```text
Longsword / 长剑 = 1d8 / 1d10 Slashing
```

也就是说，长剑单手使用时是 **1d8 挥砍**，双手使用时是 **1d10 挥砍**。

### Two-Handed / 双手

双手武器必须双手使用，通常基础伤害更高，例如巨剑、巨斧、巨锤、长弓、重弩等。

### Extra Reach / 长触及

长触及武器拥有更远的近战攻击距离，常见于长柄武器，例如长柄刀、戟、长枪。

---

## 7. Quick Balance Summary / 快速强度总结

| Category / 类型 | Representative Weapons / 代表武器 | Notes / 说明 |
|---|---|---|
| 最高单骰伤害 | Greataxe / 巨斧 | **1d12**，上限高，波动也大 |
| 最稳定双手伤害 | Greatsword, Maul / 巨剑、巨锤 | **2d6**，平均值稳定 |
| 标准单手高伤害 | Rapier, Flail, Morningstar, War Pick / 刺剑、连枷、钉头锤、战镐 | **1d8**，单手武器中的高基础骰 |
| 标准两用武器 | Longsword, Battleaxe, Warhammer / 长剑、战斧、战锤 | **1d8 / 1d10**，可盾牌也可双手 |
| 长触及武器 | Glaive, Halberd, Pike / 长柄刀、戟、长枪 | **1d10**，优势在攻击距离 |
| 灵巧武器 | Dagger, Rapier, Scimitar, Shortsword / 匕首、刺剑、弯刀、短剑 | 适合敏捷角色 |
| 投掷武器 | Javelin, Handaxe, Spear, Trident / 标枪、手斧、长矛、三叉戟 | 可兼顾近战与远程投掷 |
| 远程高伤害 | Heavy Crossbow / 重弩 | **1d10**，远程基础骰最高之一 |
| 远程通用武器 | Longbow, Light Crossbow / 长弓、轻弩 | **1d8**，稳定远程输出 |

---

## 8. Game Design Interpretation / 战棋设计参考

如果用于战棋游戏系统设计，可以把 BG3 武器进一步整理成以下功能类型：

| Tactical Type / 战棋类型 | Example Weapons / 示例武器 | Design Role / 设计定位 |
|---|---|---|
| Light One-Handed / 轻型单手 | Dagger, Shortsword, Scimitar, Handaxe / 匕首、短剑、弯刀、手斧 | 双持、连击、敏捷角色 |
| Standard One-Handed / 标准单手 | Mace, Rapier, Flail, War Pick / 硬头锤、刺剑、连枷、战镐 | 盾牌搭配、稳定近战 |
| Versatile Weapons / 两用武器 | Longsword, Battleaxe, Warhammer, Spear / 长剑、战斧、战锤、长矛 | 单手防御或双手输出切换 |
| Heavy Two-Handed / 双手重武器 | Greatsword, Greataxe, Maul / 巨剑、巨斧、巨锤 | 高伤害、低机动或高体型需求 |
| Polearms / 长柄武器 | Glaive, Halberd, Pike / 长柄刀、戟、长枪 | 控制距离、反冲锋、借机攻击 |
| Thrown Weapons / 投掷武器 | Javelin, Handaxe, Spear, Trident / 标枪、手斧、长矛、三叉戟 | 中距离补刀、近远混合 |
| Bows / 弓类 | Shortbow, Longbow / 短弓、长弓 | 高机动远程、持续输出 |
| Crossbows / 弩类 | Light Crossbow, Hand Crossbow, Heavy Crossbow / 轻弩、手弩、重弩 | 高爆发远程、可设计装填限制 |
| Special Control Weapons / 特殊控制武器 | Reach weapons, thrown weapons / 长触及、投掷类武器 | 推拉、压制、区域控制 |

---

## 9. Implementation-Friendly Data Fields / 数据结构建议

如果你要把这些武器导入游戏数据，可以考虑以下字段：

```json
{
  "id": "longsword",
  "name_en": "Longsword",
  "name_zh": "长剑",
  "category": "martial_melee",
  "damage_dice_one_handed": "1d8",
  "damage_dice_two_handed": "1d10",
  "damage_type": "slashing",
  "properties": ["versatile"],
  "is_melee": true,
  "is_ranged": false,
  "is_thrown": false,
  "requires_two_hands": false,
  "can_use_strength": true,
  "can_use_dexterity": false,
  "notes": "Standard versatile martial weapon."
}
```

建议至少保留以下字段：

| Field / 字段 | Purpose / 用途 |
|---|---|
| `id` | 程序内部唯一标识 |
| `name_en` | 英文名称 |
| `name_zh` | 中文名称 |
| `category` | 简易近战、简易远程、军用近战、军用远程 |
| `damage_dice` | 基础伤害骰 |
| `damage_type` | 钝击、穿刺、挥砍 |
| `properties` | 轻型、灵巧、投掷、两用、双手、长触及等 |
| `range_type` | melee / ranged / thrown |
| `hands_required` | one_hand / two_hand / versatile |
| `weapon_family` | sword / axe / hammer / polearm / bow / crossbow 等 |

---

## 10. Short Summary / 简短总结

BG3 正常可用的基础武器类型大致包括：

```text
10 Simple Melee Weapons
2 Simple Ranged Weapons
16 Martial Melee Weapons
3 Martial Ranged Weapons
```

合计约 **31 类基础武器类型**。

从战棋设计角度看，最有用的分类不是只按“简易/军用”，而是进一步拆成：

```text
轻型单手
标准单手
两用武器
双手重武器
长柄武器
灵巧武器
投掷武器
弓类
弩类
特殊控制武器
```

这样更适合做技能树、职业熟练、AI 选择逻辑和战斗定位。

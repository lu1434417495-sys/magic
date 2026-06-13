# 据点武器设计规范

## 设计哲学

每一件武器都必须是**有故事的个体**，拒绝数值套壳。

参考标准：
- **苍白的正义**（DND）：外观平凡，内涵颠覆性力量，力量来源与使用者的信念绑定
- **霜之哀伤**（魔兽世界）：武器即悲剧，有独立意志或诅咒，与关键历史事件纠缠

## 文档结构

每件武器包含以下字段：

```
### [序号]. [武器名称]
- **item_id**: `weapon_unique_<family>_<name_snake>`
- **display_name**: `[中文名]`
- **family**: `<weapon_family>`
- **training_group**: `simple | martial`
- **range_type**: `melee | ranged`
- **damage_tag**: `physical_slash | physical_pierce | physical_blunt`
- **attack_range**: `<tiles>`
- **one_handed_dice**: `<count>D<sides>[+bonus]`
- **two_handed_dice**: `<count>D<sides>[+bonus]`（若有）
- **properties**: `[<prop1>, <prop2>]`
- **base_price**: `<金币>`
- **attribute_modifiers**: `[<修正项>]`（若有）

#### 外观描述
<300-500字外观、材质、手感、气息>

#### 历史渊源
<500-800字来历、锻造者、历任持有者、关键战役、悲剧或荣耀>

#### 特殊性质
<游戏内表现：隐藏效果、触发条件、副作用、与特定技能/种族的联动>
```

## 武器家族清单

| 家族 | 计划数量 | 状态 |
|------|---------|------|
| sword（长剑/刺剑/弯刀/短剑） | 60 | 进行中 |
| greatsword（巨剑） | 40 | 待开始 |
| dagger（匕首） | 40 | 待开始 |
| axe（战斧/手斧） | 40 | 待开始 |
| greataxe（巨斧） | 30 | 待开始 |
| hammer（战锤/轻锤/巨锤） | 40 | 待开始 |
| mace/morningstar（钉头锤/晨星） | 30 | 待开始 |
| spear/pike/javelin（矛/长枪/标枪） | 40 | 待开始 |
| staff/quarterstaff/club（棍棒/长棍/巨棒） | 35 | 待开始 |
| polearm（薙刀/戟/镰刀/战镐） | 35 | 待开始 |
| bow（长弓/短弓） | 30 | 待开始 |
| crossbow（轻弩/手弩/重弩） | 30 | 待开始 |
| exotic（三叉戟/异种武器） | 20 | 待开始 |
| **合计** | **500** | |

## 命名规则

- `item_id` 格式：`weapon_unique_<family>_<主题>_<序号>`
- 文件名：`by_family/<家族>.md`
- 单文件武器数：15-20 件，避免单文件过大

# 传奇装备套装饰品设计文档（套装 81–90：重甲套装饰品）

> 10 套重甲套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装八十一：钢铁泰坦（Iron Titan）饰品

---

### 81.5 钢铁泰坦斗篷

```gdscript
item_id = "acc_iron_titan_cloak_81"
display_name = "钢铁泰坦斗篷"
description = "一件由精钢鳞片与泰坦丝编织而成的厚重斗篷，斗篷表面覆盖着层层叠叠的钢鳞——每一片钢鳞都来自不同的泰坦造物，因此呈现出深浅不一的金属色。斗篷非常沉重——比普通斗篷重五倍，因为其中蕴含了泰坦之力。斗篷的边缘有特殊的「钢铁流苏」——每一根流苏都如同微型铁鞭，可以用来攻击靠近的敌人。\n\n克罗诺斯在编织这件斗篷时，将自己的泰坦铠甲碎片都熔入了丝线。他说："这件斗篷不只是斗篷，它是'泰坦的铠甲'——让最脆弱的部位也坚不可摧。"\n\n这件斗篷的特殊效果是：穿戴者的 AC +2（钢铁护盾）。且可以通过斗篷「释放」钢铁风暴——每日一次，释放一阵钢铁碎片（15 尺半径 2D10 slashing，所有生物须通过 DC16 敏捷豁免，失败则 bleeding 1D6/回合，持续 3 回合）。克罗诺斯说："斗篷不是只能防护，它也可以攻击——用钢铁碎片来切割敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "iron_titan_set_81"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_titan_cloak_81" },
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_titan_cloak_81" }
]
```

---

### 81.6 钢铁泰坦项链

```gdscript
item_id = "acc_iron_titan_necklace_81"
display_name = "钢铁泰坦项链"
description = "一条由泰坦核心碎片与钢铁丝串联而成的项链，项链中央镶嵌着一颗「泰坦之心」——一颗能够感知并增强金属的魔法宝石。项链佩戴时，宝石会随金属的接近微微发热——越近越热。\n\n克罗诺斯在制作这条项链时，从自己的泰坦核心中取下了碎片。他说："这块碎片不是被取下的，是'被赐予的'——我的核心选择了我，让我成为它的代言人。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有金属的位置（金属雷达）。且可以通过项链「强化」——每日一次，强化一件金属装备（该装备的攻击或防御 +2，持续 1 小时）。克罗诺斯说："泰坦之心不是只能感知，它也可以强化——强化所有金属装备。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "iron_titan_set_81"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_iron_titan_necklace_81" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_iron_titan_necklace_81" }
]
```

---

### 81.7 钢铁泰坦戒指（左）

```gdscript
item_id = "acc_iron_titan_ring_1_81"
display_name = "钢铁泰坦戒指·铁拳"
description = "一枚由泰坦钢与钢铁丝锻造而成的厚重戒指，戒指表面有类似铁拳的纹理——坚硬、粗糙、充满力量。戒指佩戴时，周围的空气会微微震动——形成一层力量场。\n\n克罗诺斯在锻造这枚戒指时，将自己的泰坦之拳封入了钢中。他说："这枚戒指不只是戒指，它是'泰坦之拳'——让我可以击碎任何敌人。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」铁拳—— bonus action，释放一道铁拳冲击（5 尺射程 2D8 bludgeoning，目标须通过 DC16 力量豁免，失败则 stunned 1 回合，每日三次）。且在进行力量检定时，检定 +3（泰坦之力）。克罗诺斯说："铁拳不是只能用来攻击，它也可以用来震慑——用泰坦之拳来震慑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "iron_titan_set_81"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "strength_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_titan_ring_1_81" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_iron_titan_ring_1_81" }
]
```

---

### 81.8 钢铁泰坦戒指（右）

```gdscript
item_id = "acc_iron_titan_ring_2_81"
display_name = "钢铁泰坦戒指·坚盾"
description = "一枚由泰坦钢与钢铁丝锻造而成的厚重戒指，戒指表面有类似盾牌的纹理——坚硬、光滑、充满保护力。戒指佩戴时，会根据受到的攻击变化温度——受到攻击时变热（吸收冲击）。\n\n克罗诺斯在锻造这枚戒指时，将自己的泰坦之盾封入了钢中。他说："这枚戒指不只是戒指，它是'泰坦之盾'——让我可以抵挡任何攻击。"\n\n这枚戒指的特殊效果是：穿戴者的 AC +1（坚盾防御）。且可以通过戒指「格挡」——每日一次，完全格挡一次物理攻击（如同 shield spell）。克罗诺斯说："坚盾不是只能用来防御，它也可以用来格挡——格挡任何致命的攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "iron_titan_set_81"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_titan_ring_2_81" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_iron_titan_ring_2_81" }
]
```

---

### 81.9 钢铁泰坦特殊饰品

```gdscript
item_id = "acc_iron_titan_trinket_81"
display_name = "钢铁泰坦熔炉核心"
description = "一个由泰坦核心碎片与钢铁制成的小型熔炉核心，核心不断散发着热量——热量足够熔化任何金属。核心佩戴时，会随穿戴者的意愿微微发光——当有锻造需求时发光最强。\n\n克罗诺斯在制作这个核心时，将自己的「锻造之火」封入了泰坦核心。他说："这个核心不是普通的工具，它是'泰坦的熔炉'——让我可以随时随地锻造任何金属。"\n\n这个核心的特殊效果是：穿戴者可以用核心「锻造」—— bonus action，瞬间修复一件破损的金属装备（恢复至完好）。且可以通过核心「强化」——每日一次，强化一件金属武器（该武器攻击附加 2D6 fire，持续 1 小时）。克罗诺斯说："核心不是只能用来锻造，它也可以用来强化——强化所有金属武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "iron_titan_set_81"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_titan_trinket_81" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_titan_trinket_81" }
]
```

---

### 81.10 钢铁泰坦徽章

```gdscript
item_id = "acc_iron_titan_badge_81"
display_name = "钢铁泰坦徽章"
description = "一枚由泰坦钢与钢铁丝锻造而成的厚重徽章，徽章上刻有钢铁泰坦的标志——一把铁锤和一面盾牌交叉在齿轮中。徽章佩戴时，会根据附近金属的数量变化光芒的强度——越多越明亮。\n\n克罗诺斯在锻造这枚徽章时，将自己的「泰坦之魂」封入了钢中。他说："这枚徽章不只是徽章，它是'钢铁的象征'——让所有人知道，钢铁泰坦来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他钢铁泰坦的存在（徽章会微微发热）。且可以通过徽章「鼓舞」——每日一次，鼓舞 15 尺内所有盟友（力量检定 +3，持续 1 分钟）。克罗诺斯说："徽章不是只能佩戴，它也可以用来鼓舞——鼓舞所有钢铁的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "iron_titan_set_81"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_iron_titan_badge_81" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_titan_badge_81" }
]
```

---

## 套装八十二：龙鳞卫士（Dragon Scale Guardian）饰品

---

### 82.5 龙鳞卫士斗篷

```gdscript
item_id = "acc_dragon_scale_guardian_cloak_82"
display_name = "龙鳞卫士斗篷"
description = "一件由真龙鳞片与龙丝编织而成的华丽斗篷，斗篷表面覆盖着层层叠叠的龙鳞——每一片龙鳞都来自不同的巨龙，因此呈现出五彩斑斓的颜色。斗篷非常坚硬——龙鳞的硬度超过钢铁，但也因为这硬度，斗篷几乎没有柔韧性。斗篷的边缘有特殊的「龙翼流苏」——在展开时可以像龙翼一样滑翔。\n\n贝奥武夫在编织这件斗篷时，收集了七条巨龙的鳞片。他说："这件斗篷不只是斗篷，它是'巨龙的铠甲'——让我穿着巨龙本身。"\n\n这件斗篷的特殊效果是：穿戴者免疫「龙息」damage（对应鳞片颜色的龙息）。且可以通过斗篷「释放」龙息——每日一次，释放一道微型龙息（15 尺锥形 3D10 对应元素 damage，目标须通过 DC16 敏捷豁免，失败则 burning/frozen/shocked 1D6/回合，持续 3 回合）。贝奥武夫说："斗篷不是只能防护，它也可以攻击——用龙息来焚烧敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_guardian_cloak_82" },
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_dragon_scale_guardian_cloak_82" }
]
```

---

### 82.6 龙鳞卫士项链

```gdscript
item_id = "acc_dragon_scale_guardian_necklace_82"
display_name = "龙鳞卫士项链"
description = "一条由龙牙与龙丝串联而成的项链，项链中央镶嵌着一颗「龙之心」——一颗能够感知并增强龙族力量的魔法宝石。项链佩戴时，宝石会随龙族的接近微微发热——越近越热。\n\n贝奥武夫在制作这条项链时，从一条千年巨龙身上取下了龙牙。他说："这颗龙牙不是被取下的，是'被赐予的'——巨龙选择了我，让我成为它的代言人。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有龙族的位置（龙族雷达）。且可以通过项链「沟通」——每日一次，与 30 尺内一条龙进行简短沟通（了解它的意图）。贝奥武夫说："龙之心不是只能感知，它也可以沟通——与龙族沟通，了解它们的秘密。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dragon_scale_guardian_necklace_82" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_dragon_scale_guardian_necklace_82" }
]
```

---

### 82.7 龙鳞卫士戒指（左）

```gdscript
item_id = "acc_dragon_scale_guardian_ring_1_82"
display_name = "龙鳞卫士戒指·龙爪"
description = "一枚由龙爪与龙丝锻造而成的锋利戒指，戒指表面有类似龙爪的纹理——尖锐、弯曲、充满力量。戒指佩戴时，周围的空气会微微震动——形成一层龙威。\n\n贝奥武夫在锻造这枚戒指时，将一条巨龙的龙爪封入了金属。他说："这枚戒指不只是戒指，它是'龙爪的延伸'——让我可以像龙一样撕裂敌人。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」龙爪—— bonus action，释放一道龙爪（5 尺射程 2D8 slashing，目标须通过 DC16 敏捷豁免，失败则 bleeding 1D6/回合，持续 3 回合，每日三次）。且在进行恐吓检定时，检定 +3（龙威）。贝奥武夫说："龙爪不是只能用来攻击，它也可以用来震慑——用龙威来震慑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_guardian_ring_1_82" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dragon_scale_guardian_ring_1_82" }
]
```

---

### 82.8 龙鳞卫士戒指（右）

```gdscript
item_id = "acc_dragon_scale_guardian_ring_2_82"
display_name = "龙鳞卫士戒指·龙威"
description = "一枚由龙威石与龙丝锻造而成的奇异戒指，戒指表面有类似龙眼的纹理——在光线下会折射出龙族的光芒。戒指佩戴时，会根据周围敌人的数量变化温度——越多越热（龙威的觉醒）。\n\n贝奥武夫在锻造这枚戒指时，将一条巨龙的龙威封入了石头。他说："这枚戒指不只是戒指，它是'龙威的容器'——让我可以释放巨龙的威严。"\n\n这枚戒指的特殊效果是：穿戴者可以「释放」龙威——每日一次，释放一阵龙威（15 尺半径，所有生物须通过 DC16 智慧豁免，失败则 frightened 1 回合）。且在进行魅力检定时，检定 +2（龙族的优雅）。贝奥武夫说："龙威不是只能用来震慑，它也可以用来优雅——用龙族的优雅来影响他人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_guardian_ring_2_82" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dragon_scale_guardian_ring_2_82" }
]
```

---

### 82.9 龙鳞卫士特殊饰品

```gdscript
item_id = "acc_dragon_scale_guardian_trinket_82"
display_name = "龙鳞卫士龙蛋"
description = "一个由龙蛋壳与龙丝制成的小型龙蛋，龙蛋内不断有龙族的脉动——脉动与穿戴者的心跳同步。龙蛋佩戴时，会随龙族的接近微微发光——越近越亮。\n\n贝奥武夫在制作这个龙蛋时，将一颗未孵化的龙蛋封印了起来。他说："这个龙蛋不是被封印的，是'被保护的'——我保护它，它也将保护我。"\n\n这个龙蛋的特殊效果是：穿戴者可以用龙蛋「召唤」龙族——每日一次，召唤一条幼龙协助战斗（CR 2，持续 10 分钟）。且可以通过龙蛋「沟通」——与 30 尺内任何龙族进行心灵沟通。贝奥武夫说："龙蛋不是只能用来召唤，它也可以用来沟通——沟通所有龙族的心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dragon_scale_guardian_trinket_82" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_guardian_trinket_82" }
]
```

---

### 82.10 龙鳞卫士徽章

```gdscript
item_id = "acc_dragon_scale_guardian_badge_82"
display_name = "龙鳞卫士徽章"
description = "一枚由龙鳞与龙丝锻造而成的华丽徽章，徽章上刻有龙鳞卫士的标志——一条盘绕的巨龙。徽章佩戴时，会根据附近龙族的数量变化光芒的强度——越多越明亮。\n\n贝奥武夫在锻造这枚徽章时，将自己的「龙卫之魂」封入了龙鳞。他说："这枚徽章不只是徽章，它是'龙族的象征'——让所有人知道，龙鳞卫士来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他龙鳞卫士的存在（徽章会微微发热）。且可以通过徽章「鼓舞」——每日一次，鼓舞 15 尺内所有盟友（攻击检定 +2，AC +1，持续 1 分钟）。贝奥武夫说："徽章不是只能佩戴，它也可以用来鼓舞——鼓舞所有龙族的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dragon_scale_guardian_set_82"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dragon_scale_guardian_badge_82" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_guardian_badge_82" }
]
```

---

## 套装八十三：圣骑士审判者（Paladin Inquisitor）饰品

---

### 83.5 圣骑士审判者斗篷

```gdscript
item_id = "acc_paladin_inquisitor_cloak_83"
display_name = "圣骑士审判者斗篷"
description = "一件由圣光布料与审判丝编织而成的庄严斗篷，斗篷表面覆盖着金色的审判符文——每一个符文都代表一条神圣法律。斗篷非常沉重——不是因为材质，而是因为上面承载的正义之重。斗篷的边缘有特殊的「审判流苏」——每一根流苏都会让邪恶生物感到灼烧。\n\n乌瑟尔在编织这件斗篷时，将所有神圣法律都编入了符文。他说："这件斗篷不只是斗篷，它是'正义的旗帜'——让邪恶无所遁形。"\n\n这件斗篷的特殊效果是：穿戴者可以「审判」——每日一次，审判 15 尺内一个邪恶生物（该生物须通过 DC16 智慧豁免，失败则 frightened 并受到 2D10 radiant）。且斗篷会「净化」——5 尺内任何 undead 或 fiend 每回合受到 1D6 radiant。乌瑟尔说："斗篷不是只能装饰，它也可以审判——用正义来审判邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_cloak_83" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_cloak_83" }
]
```

---

### 83.6 圣骑士审判者项链

```gdscript
item_id = "acc_paladin_inquisitor_necklace_83"
display_name = "圣骑士审判者项链"
description = "一条由圣光水晶与审判丝串联而成的项链，项链中央镶嵌着一颗「审判之眼」——一颗能够看穿谎言和伪装的魔法宝石。项链佩戴时，宝石会随邪恶的接近变红——越邪恶越红。\n\n乌瑟尔在制作这条项链时，将自己的「正义之心」封入了水晶。他说："这颗宝石不是普通的石头，它是'正义的镜子'——让一切邪恶无所遁形。"\n\n这条项链的特殊效果是：穿戴者可以「看穿」谎言和伪装（如同 zone of truth 的 personal 版，范围 15 尺）。且可以通过项链「审判」——每日一次，对一个说谎者造成 2D10 radiant（正义的惩罚）。乌瑟尔说："审判之眼不是只能看，它也可以惩罚——惩罚所有说谎者。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_paladin_inquisitor_necklace_83" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_paladin_inquisitor_necklace_83" }
]
```

---

### 83.7 圣骑士审判者戒指（左）

```gdscript
item_id = "acc_paladin_inquisitor_ring_1_83"
display_name = "圣骑士审判者戒指·真理"
description = "一枚由圣光金属与审判丝锻造而成的精致戒指，戒指表面刻有「真理」二字——以神圣之火写就。戒指佩戴时，会根据周围谎言的数量变化温度——谎言越多越热。\n\n乌瑟尔在锻造这枚戒指时，将自己的「真理之力」封入了金属。他说："这枚戒指不只是戒指，它是'真理的象征'——让一切谎言无所遁形。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」谎言——30 尺内任何谎言会被自动识别。且可以通过戒指「强迫」——每日一次，强迫一个目标说出真相（目标须通过 DC16 智慧豁免，失败则被迫说出真相 1 分钟）。乌瑟尔说："真理不是只能感知，它也可以强迫——强迫所有说谎者说出真相。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_ring_1_83" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_paladin_inquisitor_ring_1_83" }
]
```

---

### 83.8 圣骑士审判者戒指（右）

```gdscript
item_id = "acc_paladin_inquisitor_ring_2_83"
display_name = "圣骑士审判者戒指·正义"
description = "一枚由圣光金属与审判丝锻造而成的精致戒指，戒指表面刻有「正义」二字——以神圣之火写就。戒指佩戴时，会根据周围邪恶的数量变化光芒——邪恶越多越亮。\n\n乌瑟尔在锻造这枚戒指时，将自己的「正义之剑」封入了金属。他说："这枚戒指不只是戒指，它是'正义的延伸'——让我可以惩罚任何邪恶。"\n\n这枚戒指的特殊效果是：穿戴者可以「释放」正义—— bonus action，释放一道正义之光（30 尺射程 1D10 radiant，对 evil 双倍，每日三次）。且在进行对抗邪恶生物的攻击时，攻击检定 +2（正义的加持）。乌瑟尔说："正义不是只能用来惩罚，它也可以用来攻击——用正义之光来净化邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_paladin_inquisitor_ring_2_83" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_paladin_inquisitor_ring_2_83" }
]
```

---

### 83.9 圣骑士审判者特殊饰品

```gdscript
item_id = "acc_paladin_inquisitor_trinket_83"
display_name = "圣骑士审判者圣典"
description = "一个由圣光皮革与审判丝制成的小型圣典，圣典内写有所有神圣法律——不是普通的法律，是「神圣律法」，可以审判任何罪恶。圣典佩戴时，会随邪恶的接近微微发热——越邪恶越热。\n\n乌瑟尔在制作这个圣典时，将自己的「审判之锤」封入了皮革。他说："这个圣典不是普通的书籍，它是'正义的法庭'——让我可以审判任何罪恶。"\n\n这个圣典的特殊效果是：穿戴者可以用圣典「审判」——每日一次，审判 15 尺内一个目标（目标须通过 DC16 智慧豁免，失败则 frightened 并受到 2D10 radiant）。且可以通过圣典「查询」——查询任何神圣法律（自动成功）。乌瑟尔说："圣典不是只能用来审判，它也可以用来查询——查询所有神圣法律。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_trinket_83" },
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_trinket_83" }
]
```

---

### 83.10 圣骑士审判者徽章

```gdscript
item_id = "acc_paladin_inquisitor_badge_83"
display_name = "圣骑士审判者徽章"
description = "一枚由圣光金属与审判丝锻造而成的庄严徽章，徽章上刻有圣骑士审判者的标志——一把燃烧的圣剑。徽章佩戴时，会根据附近邪恶的数量变化光芒的强度——越多越明亮。\n\n乌瑟尔在锻造这枚徽章时，将自己的「审判者之魂」封入了金属。他说："这枚徽章不只是徽章，它是'正义的象征'——让所有人知道，圣骑士审判者来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他圣骑士审判者的存在（徽章会微微发热）。且可以通过徽章「号召」——每日一次，号召 15 尺内所有盟友对邪恶生物发动联合攻击（所有盟友下一回合对 evil 攻击检定 +3）。乌瑟尔说："徽章不是只能佩戴，它也可以用来号召——号召所有正义的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "paladin_inquisitor_set_83"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_paladin_inquisitor_badge_83" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_paladin_inquisitor_badge_83" }
]
```

---

## 套装八十四：深渊守望者（Abyss Watcher）饰品

---

### 84.5 深渊守望者斗篷

```gdscript
item_id = "acc_abyss_watcher_cloak_84"
display_name = "深渊守望者斗篷"
description = "一件由深渊物质与守望丝编织而成的奇异斗篷，斗篷呈现出深渊的黑色——不是普通的那种黑，是连光都无法逃脱的绝对黑。斗篷不断散发出深渊的气息——靠近它的人会感到寒冷和恐惧。斗篷的边缘有特殊的「深渊流苏」——每一根流苏都连接到深渊深处，仿佛斗篷本身就是通往深渊的入口。\n\n阿图姆在编织这件斗篷时，将自己的「深渊之影」编入了丝线。他说："这件斗篷不只是斗篷，它是'深渊的碎片'——让我穿着深渊本身。"\n\n这件斗篷的特殊效果是：穿戴者可以「融入」深渊——在黑暗中完全隐形且无法被任何感知探测（如同 greater invisibility + nondetection）。且可以通过斗篷「释放」深渊——每日一次，释放一道深渊裂缝（15 尺锥形 3D10 necrotic，目标须通过 DC16 体质豁免，失败则 frightened 1 回合）。阿图姆说："斗篷不是只能隐藏，它也可以攻击——用深渊来吞噬敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "abyss_watcher_set_84"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_cloak_84" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_cloak_84" }
]
```

---

### 84.6 深渊守望者项链

```gdscript
item_id = "acc_abyss_watcher_necklace_84"
display_name = "深渊守望者项链"
description = "一条由深渊物质与守望丝串联而成的项链，项链中央镶嵌着一颗「深渊之眼」——一颗能够看到深渊最深处并感知深渊生物的魔法宝石。项链佩戴时，宝石会随深渊的波动微微脉动——脉动与深渊的存在同步。\n\n阿图姆在制作这条项链时，从深渊深处取下了一块结晶。他说："这块结晶不是被取下的，是'被赐予的'——深渊选择了我，让我成为它的守望者。"\n\n这条项链的特殊效果是：穿戴者可以「看见」深渊——看到所有隐藏在深渊中的生物和物体（如同 see invisibility，但对深渊生物有效，范围 120 尺）。且可以通过项链「预警」——每日一次，预知深渊的一次攻击（获得 advantage 在该次攻击的豁免上）。阿图姆说："深渊之眼不是只能看，它也可以预警——预警深渊的每一次攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "abyss_watcher_set_84"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_watcher_necklace_84" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_abyss_watcher_necklace_84" }
]
```

---

### 84.7 深渊守望者戒指（左）

```gdscript
item_id = "acc_abyss_watcher_ring_1_84"
display_name = "深渊守望者戒指·凝视"
description = "一枚由深渊物质与守望丝锻造而成的黑色戒指，戒指表面有类似眼睛的纹理——不断凝视着深渊。戒指佩戴时，周围的空气会微微扭曲——形成一层深渊护盾。\n\n阿图姆在锻造这枚戒指时，将自己的「凝视之力」封入了物质。他说："这枚戒指不只是戒指，它是'深渊的凝视'——让我可以凝视深渊而不被深渊吞噬。"\n\n这枚戒指的特殊效果是：穿戴者可以「凝视」深渊——每日一次，凝视一个目标（目标须通过 DC16 智慧豁免，失败则 frightened 并受到 2D10 psychic）。且在进行感知检定时，检定 +3（凝视的清晰度）。阿图姆说："凝视不是只能用来观察，它也可以用来攻击——用深渊的凝视来击垮敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "abyss_watcher_set_84"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_watcher_ring_1_84" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_abyss_watcher_ring_1_84" }
]
```

---

### 84.8 深渊守望者戒指（右）

```gdscript
item_id = "acc_abyss_watcher_ring_2_84"
display_name = "深渊守望者戒指·守望"
description = "一枚由守望石与守望丝锻造而成的精致戒指，戒指表面有类似守望塔的纹理——坚固、高耸、充满警戒。戒指佩戴时，会根据周围危险的浓度变化温度——越危险越热。\n\n阿图姆在锻造这枚戒指时，将自己的「守望之心」封入了石头。他说："这枚戒指不只是戒指，它是'守望的誓言'——让我永远守望深渊。"\n\n这枚戒指的特殊效果是：穿戴者可以「守望」—— bonus action，选择一个方向进行守望（该方向 60 尺内任何移动会被自动感知，持续 1 分钟）。且可以通过戒指「警报」——每日一次，向 15 尺内所有盟友发出警报（所有盟友获得 advantage 在下一回合的先攻上）。阿图姆说："守望不是只能用来观察，它也可以用来警报——警报所有危险的接近。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "abyss_watcher_set_84"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_ring_2_84" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_abyss_watcher_ring_2_84" }
]
```

---

### 84.9 深渊守望者特殊饰品

```gdscript
item_id = "acc_abyss_watcher_trinket_84"
display_name = "深渊守望者灯笼"
description = "一个由深渊物质与守望丝制成的奇异灯笼，灯笼内燃烧着「深渊之火」——一种永远不会熄灭、但也不会产生热量的奇异火焰。灯笼佩戴时，会随深渊的波动微微发光——深渊越近光芒越强。\n\n阿图姆在制作这个灯笼时，将自己的「守望之火」封入了玻璃。他说："这个灯笼不是普通的工具，它是'深渊的光芒'——让我在黑暗中看清一切。"\n\n这个灯笼的特殊效果是：穿戴者可以用灯笼「照亮」深渊——15 尺半径内所有隐形或隐藏生物显形（如同 faerie fire）。且可以通过灯笼「驱散」——每日一次，驱散 15 尺内所有 darkness 效果。阿图姆说："灯笼不是只能照亮，它也可以驱散——驱散所有黑暗。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "abyss_watcher_set_84"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_trinket_84" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_trinket_84" }
]
```

---

### 84.10 深渊守望者徽章

```gdscript
item_id = "acc_abyss_watcher_badge_84"
display_name = "深渊守望者徽章"
description = "一枚由深渊物质与守望丝锻造而成的黑色徽章，徽章上刻有深渊守望者的标志——一只睁开的眼睛在深渊之上。徽章佩戴时，会根据附近深渊裂缝的数量变化光芒的强度——越多越明亮。\n\n阿图姆在锻造这枚徽章时，将自己的「守望者之魂」封入了物质。他说："这枚徽章不只是徽章，它是'守望的象征'——让所有人知道，深渊守望者来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他深渊守望者的存在（徽章会微微发热）。且可以通过徽章「联合守望」——每日一次，与 30 尺内另一个深渊守望者建立联合守望（双方感知范围翻倍，持续 1 小时）。阿图姆说："徽章不是只能佩戴，它也可以用来联合——联合所有守望者的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "abyss_watcher_set_84"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_watcher_badge_84" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_watcher_badge_84" }
]
```

---

## 套装八十五：战争领主（Warlord）饰品

---

### 85.5 战争领主斗篷

```gdscript
item_id = "acc_warlord_cloak_85"
display_name = "战争领主斗篷"
description = "一件由战旗碎片与战争丝编织而成的威严斗篷，斗篷表面覆盖着无数微型战旗——每一面战旗都代表一场胜利。斗篷非常沉重——不是因为材质，而是因为上面承载的荣耀之重。斗篷的边缘有特殊的「战旗流苏」——每一根流苏都会让盟友感到鼓舞，让敌人感到恐惧。\n\n成吉思汗在编织这件斗篷时，将自己所有胜利的战旗都编入了丝线。他说："这件斗篷不只是斗篷，它是'胜利的旗帜'——让所有人知道，战争领主来了。"\n\n这件斗篷的特殊效果是：穿戴者可以「鼓舞」——每日一次，鼓舞 30 尺内所有盟友（攻击检定 +2，移动力 +10，持续 1 分钟）。且斗篷会「震慑」——15 尺内任何敌人攻击检定 -1（战争领主的威严）。成吉思汗说："斗篷不是只能装饰，它也可以鼓舞——用胜利的旗帜来鼓舞盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "warlord_set_85"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_warlord_cloak_85" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_warlord_cloak_85" }
]
```

---

### 85.6 战争领主项链

```gdscript
item_id = "acc_warlord_necklace_85"
display_name = "战争领主项链"
description = "一条由战利品牙齿与战争丝串联而成的项链，项链中央镶嵌着一颗「征服之心」——一颗能够感知敌人弱点并增强士气的魔法宝石。项链佩戴时，宝石会随敌人的接近微微发热——越强大越热。\n\n成吉思汗在制作这条项链时，将自己击败的最强大敌人的牙齿都编入了项链。他说："这些牙齿不是被收集的，是'被继承的'——它们代表了我的每一次胜利。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有敌人的弱点（自动识别敌人的最低属性）。且可以通过项链「激励」——每日一次，激励 15 尺内一个盟友（该盟友下一回合攻击检定 +5，伤害翻倍）。成吉思汗说："征服之心不是只能感知，它也可以激励——激励所有盟友战胜敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "warlord_set_85"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "strength_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_warlord_necklace_85" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_warlord_necklace_85" }
]
```

---

### 85.7 战争领主戒指（左）

```gdscript
item_id = "acc_warlord_ring_1_85"
display_name = "战争领主戒指·征服"
description = "一枚由征服金属与战争丝锻造而成的厚重戒指，戒指表面刻有「征服」二字——以敌人的血写就。戒指佩戴时，会根据周围敌人的数量变化温度——越多越热。\n\n成吉思汗在锻造这枚戒指时，将自己的第一滴征服之血都编入了金属。他说："这枚戒指不只是戒指，它是'征服的象征'——让我可以征服任何敌人。"\n\n这枚戒指的特殊效果是：穿戴者可以「征服」—— bonus action，选择一个 5 尺内的敌人，该敌人下一回合攻击检定 -3（征服的压制，每日三次）。且在进行力量检定时，检定 +3（征服之力）。成吉思汗说："征服不是只能用来攻击，它也可以用来压制——压制所有敌人的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "warlord_set_85"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_warlord_ring_1_85" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_warlord_ring_1_85" }
]
```

---

### 85.8 战争领主戒指（右）

```gdscript
item_id = "acc_warlord_ring_2_85"
display_name = "战争领主戒指·指挥"
description = "一枚由指挥金属与战争丝锻造而成的精致戒指，戒指表面有类似军号的纹理——在触碰时会发出无声的命令。戒指佩戴时，会根据周围盟友的数量变化光芒——越多越明亮。\n\n成吉思汗在锻造这枚戒指时，将自己的「指挥之心」封入了金属。他说："这枚戒指不只是戒指，它是'指挥的权杖'——让我可以指挥任何军队。"\n\n这枚戒指的特殊效果是：穿戴者可以「指挥」—— bonus action，指挥 15 尺内一个盟友进行额外攻击（该盟友立即进行一次攻击，每日三次）。且在进行领导检定时，检定 +3（指挥的威严）。成吉思汗说："指挥不是只能用来命令，它也可以用来攻击——指挥盟友进行额外的攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "warlord_set_85"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_warlord_ring_2_85" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_warlord_ring_2_85" }
]
```

---

### 85.9 战争领主特殊饰品

```gdscript
item_id = "acc_warlord_trinket_85"
display_name = "战争领主战鼓"
description = "一个由战鼓皮与战争丝制成的小型战鼓，战鼓可以发出最震撼的声音——声音可以传到数里之外，让所有听到的人感到热血沸腾。战鼓佩戴时，会随战斗的激烈程度微微振动——战斗越激烈振动越强。\n\n成吉思汗在制作这个战鼓时，将自己的「战鼓之声」封入了鼓面。他说："这个战鼓不是普通的乐器，它是'战争的号角'——让我可以号召任何军队。"\n\n这个战鼓的特殊效果是：穿戴者可以用战鼓「鼓舞」——每日一次，鼓舞 30 尺内所有盟友（攻击检定 +2，damage +1D6，持续 1 分钟）。且可以通过战鼓「震慑」——每日一次，震慑 15 尺内所有敌人（须通过 DC16 智慧豁免，失败则 frightened 1 回合）。成吉思汗说："战鼓不是只能用来听，它也可以用来鼓舞——鼓舞所有盟友的斗志。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "warlord_set_85"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_warlord_trinket_85" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_warlord_trinket_85" }
]
```

---

### 85.10 战争领主徽章

```gdscript
item_id = "acc_warlord_badge_85"
display_name = "战争领主徽章"
description = "一枚由征服金属与战争丝锻造而成的威严徽章，徽章上刻有战争领主的标志——一把战斧和一面战旗。徽章佩戴时，会根据附近军队的数量变化光芒的强度——越多越明亮。\n\n成吉思汗在锻造这枚徽章时，将自己的「战争领主之魂」封入了金属。他说："这枚徽章不只是徽章，它是'战争的象征'——让所有人知道，战争领主来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他战争领主的存在（徽章会微微发热）。且可以通过徽章「号召」——每日一次，号召 30 尺内所有盟友发动联合冲锋（所有盟友移动力 +15，下一回合攻击检定 +3）。成吉思汗说："徽章不是只能佩戴，它也可以用来号召——号召所有盟友冲锋。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "warlord_set_85"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_warlord_badge_85" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_warlord_badge_85" }
]
```

---

## 套装八十六：符文巨像（Rune Golem）饰品

---

### 86.5 符文巨像斗篷

```gdscript
item_id = "acc_rune_golem_cloak_86"
display_name = "符文巨像斗篷"
description = "一件由符文石板碎片与魔力丝编织而成的奇异斗篷，斗篷表面覆盖着无数微型符文——每一个符文都代表一种魔法效果。斗篷非常沉重——因为符文的魔力，但也因为这重量，斗篷可以吸收魔法攻击。斗篷的边缘有特殊的「符文流苏」——每一根流苏都会自动激活一个防护符文。\n\n塔罗斯在编织这件斗篷时，将自己的所有符文都编入了丝线。他说："这件斗篷不只是斗篷，它是'符文的集合'——让我穿着魔法本身。"\n\n这件斗篷的特殊效果是：穿戴者可以「吸收」魔法——每日一次，完全吸收一个 directed 法术（如同 counter spell 但不需要反应）。且斗篷会「反射」——每日一次，将一个 absorbed 法术反射回施法者。塔罗斯说："斗篷不是只能防护，它也可以反射——反射所有魔法攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "rune_golem_set_86"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_rune_golem_cloak_86" },
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_golem_cloak_86" }
]
```

---

### 86.6 符文巨像项链

```gdscript
item_id = "acc_rune_golem_necklace_86"
display_name = "符文巨像项链"
description = "一条由符文核心碎片与魔力丝串联而成的项链，项链中央镶嵌着一颗「符文之心」——一颗能够储存和释放符文的魔法宝石。项链佩戴时，宝石会随魔法的接近微微发光——光芒的颜色代表魔法的类型。\n\n塔罗斯在制作这条项链时，从自己的符文核心中取下了碎片。他说："这块碎片不是被取下的，是'被赐予的'——我的核心选择了我，让我成为它的代言人。"\n\n这条项链的特殊效果是：穿戴者可以「储存」符文——每日一次，储存一个法术在宝石中（之后可以随时释放）。且可以通过项链「识别」——自动识别任何符文或魔法印记（如同 identify）。塔罗斯说："符文之心不是只能储存，它也可以识别——识别所有符文。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "rune_golem_set_86"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_golem_necklace_86" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_rune_golem_necklace_86" }
]
```

---

### 86.7 符文巨像戒指（左）

```gdscript
item_id = "acc_rune_golem_ring_1_86"
display_name = "符文巨像戒指·符文"
description = "一枚由符文石板碎片与魔力丝锻造而成的奇异戒指，戒指表面刻有无数微型符文——每一个符文都可以单独激活。戒指佩戴时，符文会随魔法的流动微微发光——光芒的颜色代表激活的符文。\n\n塔罗斯在锻造这枚戒指时，将自己的「符文之力」封入了碎片。他说："这枚戒指不只是戒指，它是'符文的钥匙'——让我可以激活任何符文。"\n\n这枚戒指的特殊效果是：穿戴者可以「激活」符文—— bonus action，激活一个符文（可以选择：护盾符文：+2 AC；攻击符文：+2 攻击；治愈符文：恢复 2D8 HP；每日三次）。塔罗斯说："符文不是只能用来储存，它也可以激活——激活所有符文的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "rune_golem_set_86"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rune_golem_ring_1_86" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_rune_golem_ring_1_86" }
]
```

---

### 86.8 符文巨像戒指（右）

```gdscript
item_id = "acc_rune_golem_ring_2_86"
display_name = "符文巨像戒指·魔力"
description = "一枚由魔力水晶与魔力丝锻造而成的精致戒指，戒指表面有类似魔力的纹理——在光线下会折射出魔法的光芒。戒指佩戴时，会根据周围魔法的浓度变化温度——浓度越高越热。\n\n塔罗斯在锻造这枚戒指时，将自己的「魔力之源」封入了水晶。他说："这枚戒指不只是戒指，它是'魔力的容器'——储存着我的魔力。"\n\n这枚戒指的特殊效果是：穿戴者的魔力恢复速度 +1/回合（魔力回复）。且可以通过戒指「释放」——每日一次，释放所有储存的魔力（恢复 3D10 mana）。塔罗斯说："魔力不是只能用来施法，它也可以储存——储存魔力，释放力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "rune_golem_set_86"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_rune_golem_ring_2_86" },
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_golem_ring_2_86" }
]
```

---

### 86.9 符文巨像特殊饰品

```gdscript
item_id = "acc_rune_golem_trinket_86"
display_name = "符文巨像符文石板"
description = "一个由符文石板碎片与魔力丝制成的小型符文石板，符文石板上刻有无数符文——每一个符文都可以随时激活。符文石板佩戴时，会随魔法的流动微微发光——光芒的颜色代表激活的符文类型。\n\n塔罗斯在制作这个符文石板时，将自己的「符文之魂」封入了石板。他说："这个符文石板不是普通的工具，它是'符文的心脏'——让我可以激活任何符文。"\n\n这个符文石板的特殊效果是：穿戴者可以用符文石板「激活」符文—— bonus action，激活一个符文（可以选择：火符文：释放 2D10 fire；冰符文：释放 2D10 cold；雷符文：释放 2D10 lightning；每日三次）。且可以通过符文石板「识别」——自动识别任何符文或魔法印记。塔罗斯说："符文石板不是只能用来激活，它也可以用来识别——识别所有符文。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "rune_golem_set_86"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_rune_golem_trinket_86" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rune_golem_trinket_86" }
]
```

---

### 86.10 符文巨像徽章

```gdscript
item_id = "acc_rune_golem_badge_86"
display_name = "符文巨像徽章"
description = "一枚由符文石板碎片与魔力丝锻造而成的奇异徽章，徽章上刻有符文巨像的标志——一个由符文组成的巨人。徽章佩戴时，会根据附近魔法的浓度变化光芒的强度——浓度越高越明亮。\n\n塔罗斯在锻造这枚徽章时，将自己的「巨像之魂」封入了碎片。他说："这枚徽章不只是徽章，它是'魔法的象征'——让所有人知道，符文巨像来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他符文巨像的存在（徽章会微微发热）。且可以通过徽章「联合」——每日一次，与 30 尺内另一个符文巨像建立魔力链接（双方共享魔力池，持续 1 分钟）。塔罗斯说："徽章不是只能佩戴，它也可以用来联合——联合所有符文巨像的魔力。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "rune_golem_set_86"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rune_golem_badge_86" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rune_golem_badge_86" }
]
```

---

## 套装八十七：不灭战神（Undying War God）饰品

---

### 87.5 不灭战神斗篷

```gdscript
item_id = "acc_undying_war_god_cloak_87"
display_name = "不灭战神斗篷"
description = "一件由不灭之火与战神丝编织而成的威严斗篷，斗篷表面不断有不灭之火在燃烧——这不是普通的火，是永远不会熄灭、永远不会产生热量的神火。斗篷非常轻盈——因为神火没有重量，但也因为这轻盈，穿戴者在战斗中会微微飘起。斗篷的边缘有特殊的「战神流苏」——每一根流苏都会让穿戴者感到无尽的力量。\n\n阿瑞斯在编织这件斗篷时，将自己的不灭之火都编入了丝线。他说："这件斗篷不只是斗篷，它是'战神的火焰'——让我穿着不灭之火本身。"\n\n这件斗篷的特殊效果是：穿戴者免疫「死亡」效果（例如 power word kill、disintegrate）。且可以通过斗篷「复活」——每日一次，在 HP 降至 0 时自动恢复至最大 HP 的一半（不灭之火）。阿瑞斯说："斗篷不是只能装饰，它也可以复活——用不灭之火来复活自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "undying_war_god_set_87"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_undying_war_god_cloak_87" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_undying_war_god_cloak_87" }
]
```

---

### 87.6 不灭战神项链

```gdscript
item_id = "acc_undying_war_god_necklace_87"
display_name = "不灭战神项链"
description = "一条由不灭之火与战神丝串联而成的项链，项链中央镶嵌着一颗「不灭之心」——一颗能够储存生命并释放生命的魔法宝石。项链佩戴时，宝石会随生命的流动微微脉动——脉动与穿戴者的心跳同步。\n\n阿图姆在制作这条项链时，将自己的「不灭之魂」封入了宝石。他说："这颗宝石不是普通的石头，它是'生命的源泉'——让我永远不会死去。"\n\n这条项链的特殊效果是：穿戴者可以「储存」生命—— bonus action，储存当前 HP 的 10%（最多储存 50 HP，储存的 HP 可以在需要时释放）。且可以通过项链「治愈」——每日一次，释放所有储存的生命（恢复等量 HP）。阿瑞斯说："不灭之心不是只能储存，它也可以治愈——治愈所有伤口。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "undying_war_god_set_87"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_undying_war_god_necklace_87" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_undying_war_god_necklace_87" }
]
```

---

### 87.7 不灭战神戒指（左）

```gdscript
item_id = "acc_undying_war_god_ring_1_87"
display_name = "不灭战神戒指·不灭"
description = "一枚由不灭之火与战神丝锻造而成的奇异戒指，戒指表面不断有不灭之火在燃烧——在黑暗中会照亮周围。戒指佩戴时，周围的空气会微微温暖——形成一层生命护盾。\n\n阿瑞斯在锻造这枚戒指时，将自己的「不灭之火」封入了金属。他说："这枚戒指不只是戒指，它是'不灭的誓言'——让我永远不会停止战斗。"\n\n这枚戒指的特殊效果是：穿戴者可以「不灭」——每日一次，在 HP 降至 0 时自动恢复 1 HP（继续战斗）。且在进行体质检定时，检定 +3（不灭之力）。阿瑞斯说："不灭不是只能用来复活，它也可以用来坚持——坚持到最后一刻。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "undying_war_god_set_87"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_undying_war_god_ring_1_87" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_undying_war_god_ring_1_87" }
]
```

---

### 87.8 不灭战神戒指（右）

```gdscript
item_id = "acc_undying_war_god_ring_2_87"
display_name = "不灭战神戒指·战意"
description = "一枚由战神金属与战神丝锻造而成的厚重戒指，戒指表面刻有「战意」二字——以不灭之火写就。戒指佩戴时，会根据战斗的激烈程度变化温度——越激烈越热。\n\n阿瑞斯在锻造这枚戒指时，将自己的「战意之魂」封入了金属。他说："这枚戒指不只是戒指，它是'战意的化身'——让我永远不会停止战斗。"\n\n这枚戒指的特殊效果是：穿戴者可以「战意」—— bonus action，激发战意（攻击检定 +2，damage +1D6，持续 1 回合，每日三次）。且在进行攻击检定时，检定 +2（战意的加持）。阿瑞斯说："战意不是只能用来激发，它也可以用来攻击——用战意来击败敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "undying_war_god_set_87"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_undying_war_god_ring_2_87" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_undying_war_god_ring_2_87" }
]
```

---

### 87.9 不灭战神特殊饰品

```gdscript
item_id = "acc_undying_war_god_trinket_87"
display_name = "不灭战神战魂瓶"
description = "一个由不灭之火与战神丝制成的小型战魂瓶，战魂瓶内储存着「战魂」——来自无数战死英雄的灵魂碎片。战魂瓶佩戴时，会随战斗的激烈程度微微发光——战斗越激烈光芒越强。\n\n阿瑞斯在制作这个战魂瓶时，将自己的「战魂之刃」封入了玻璃。他说："这个战魂瓶不是普通的容器，它是'战魂的监狱'——让我可以召唤所有战死的英雄。"\n\n这个战魂瓶的特殊效果是：穿戴者可以用战魂瓶「召唤」战魂——每日一次，召唤一个战魂协助战斗（CR 3，持续 10 分钟）。且可以通过战魂瓶「激励」—— bonus action，激励自己（恢复 2D8 HP 并获得 +2 攻击，每日三次）。阿瑞斯说："战魂不是只能用来召唤，它也可以用来激励——激励所有战斗的意志。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "undying_war_god_set_87"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_undying_war_god_trinket_87" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_undying_war_god_trinket_87" }
]
```

---

### 87.10 不灭战神徽章

```gdscript
item_id = "acc_undying_war_god_badge_87"
display_name = "不灭战神徽章"
description = "一枚由不灭之火与战神丝锻造而成的威严徽章，徽章上刻有不灭战神的标志——一团永不熄灭的火焰中有一把剑。徽章佩戴时，会根据附近战斗的激烈程度变化光芒的强度——越激烈越明亮。\n\n阿瑞斯在锻造这枚徽章时，将自己的「不灭战神之魂」封入了火焰。他说："这枚徽章不只是徽章，它是'不灭的象征'——让所有人知道，不灭战神来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他不灭战神的存在（徽章会微微发热）。且可以通过徽章「鼓舞」——每日一次，鼓舞 15 尺内所有盟友（免疫 frightened 和 charm，持续 1 分钟）。阿瑞斯说："徽章不是只能佩戴，它也可以用来鼓舞——鼓舞所有不灭的灵魂。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "undying_war_god_set_87"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_undying_war_god_badge_87" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_undying_war_god_badge_87" }
]
```

---

## 套装八十八：地狱熔炉（Inferno Forge）饰品

---

### 88.5 地狱熔炉斗篷

```gdscript
item_id = "acc_inferno_forge_cloak_88"
display_name = "地狱熔炉斗篷"
description = "一件由地狱之火与熔炉丝编织而成的恐怖斗篷，斗篷表面不断有地狱之火在燃烧——这不是普通的火，是永远不会熄灭、永远燃烧灵魂的地狱火。斗篷非常沉重——因为地狱火的重量，但也因为这重量，穿戴者在地狱中不会被风吹走。斗篷的边缘有特殊的「地狱流苏」——每一根流苏都会让周围的空气微微燃烧。\n\n撒旦在编织这件斗篷时，将地狱之火本身都编入了丝线。他说："这件斗篷不只是斗篷，它是'地狱的化身'——让我穿着地狱本身。"\n\n这件斗篷的特殊效果是：穿戴者免疫「火焰」和「necrotic」damage。且可以通过斗篷「释放」地狱火——每日一次，释放一道地狱火（15 尺锥形 3D10 fire + 3D10 necrotic，目标须通过 DC18 敏捷豁免，失败则 burning 2D6/回合，持续 3 回合）。撒旦说："斗篷不是只能防护，它也可以攻击——用地狱火来焚烧敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "inferno_forge_set_88"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_inferno_forge_cloak_88" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_inferno_forge_cloak_88" }
]
```

---

### 88.6 地狱熔炉项链

```gdscript
item_id = "acc_inferno_forge_necklace_88"
display_name = "地狱熔炉项链"
description = "一条由地狱之火与熔炉丝串联而成的项链，项链中央镶嵌着一颗「地狱之心」——一颗能够感知并控制地狱之火的魔法宝石。项链佩戴时，宝石会随地狱之火的接近微微发光——越近越亮。\n\n撒旦在制作这条项链时，从地狱之火中取下了结晶。他说："这块结晶不是被取下的，是'被赐予的'——地狱选择了我，让我成为它的代言人。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有地狱之火和热源（热源雷达）。且可以通过项链「控制」——每日一次，控制 30 尺内一团地狱之火（可以指挥它攻击或移动）。撒旦说："地狱之心不是只能感知，它也可以控制——控制所有地狱之火。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "inferno_forge_set_88"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_inferno_forge_necklace_88" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_inferno_forge_necklace_88" }
]
```

---

### 88.7 地狱熔炉戒指（左）

```gdscript
item_id = "acc_inferno_forge_ring_1_88"
display_name = "地狱熔炉戒指·熔火"
description = "一枚由地狱之火与熔炉丝锻造而成的粗糙戒指，戒指表面不断有地狱之火在燃烧——在黑暗中会照亮周围。戒指佩戴时，周围的空气会微微燃烧——形成一层火焰护盾。\n\n撒旦在锻造这枚戒指时，将一团地狱之火封入了金属。他说："这团火不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」熔火—— bonus action，释放一道熔火（30 尺射程 2D10 fire + 1D10 necrotic，每日三次）。且在进行火焰法术时，伤害 +1D8（熔火强化）。撒旦说："熔火不是只能用来攻击，它也可以用来燃烧——燃烧所有敌人的灵魂。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "inferno_forge_set_88"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_inferno_forge_ring_1_88" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_inferno_forge_ring_1_88" }
]
```

---

### 88.8 地狱熔炉戒指（右）

```gdscript
item_id = "acc_inferno_forge_ring_2_88"
display_name = "地狱熔炉戒指·灵魂"
description = "一枚由灵魂石与熔炉丝锻造而成的奇异戒指，戒指表面有类似灵魂的纹理——在光线下会折射出灵魂的光芒。戒指佩戴时，会根据周围灵魂的数量变化温度——越多越热。\n\n撒旦在锻造这枚戒指时，将自己的「灵魂之火」封入了石头。他说："这枚戒指不只是戒指，它是'灵魂的熔炉'——让我可以熔炼任何灵魂。"\n\n这枚戒指的特殊效果是：穿戴者可以「熔炼」灵魂—— bonus action，熔炼 5 尺内一个刚死去的生物的灵魂（恢复 2D8 HP 或增加 2D8 法术伤害，每日三次）。且在进行 necrotic 伤害时，伤害 +1D8（灵魂强化）。撒旦说："灵魂不是只能用来熔炼，它也可以用来强化——强化所有法术。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "inferno_forge_set_88"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_inferno_forge_ring_2_88" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_inferno_forge_ring_2_88" }
]
```

---

### 88.9 地狱熔炉特殊饰品

```gdscript
item_id = "acc_inferno_forge_trinket_88"
display_name = "地狱熔炉熔炉"
description = "一个由地狱之火与熔炉丝制成的小型熔炉，熔炉内燃烧着「地狱之火」——一种永远不会熄灭、可以熔化任何物质的神火。熔炉佩戴时，会随穿戴者的意愿微微发光——当有锻造需求时发光最强。\n\n撒旦在制作这个熔炉时，将自己的「地狱熔炉」封入了火焰。他说："这个熔炉不是普通的工具，它是'地狱的心脏'——让我可以随时随地熔化任何物质。"\n\n这个熔炉的特殊效果是：穿戴者可以用熔炉「熔化」任何物质—— bonus action，熔化一个物体（大小不超过 1 立方尺）。且可以通过熔炉「锻造」——每日一次，锻造一件地狱武器（该武器攻击附加 3D6 fire + 2D6 necrotic，持续 1 小时）。撒旦说："熔炉不是只能用来熔化，它也可以用来锻造——锻造地狱的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "inferno_forge_set_88"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_inferno_forge_trinket_88" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_inferno_forge_trinket_88" }
]
```

---

### 88.10 地狱熔炉徽章

```gdscript
item_id = "acc_inferno_forge_badge_88"
display_name = "地狱熔炉徽章"
description = "一枚由地狱之火与熔炉丝锻造而成的恐怖徽章，徽章上刻有地狱熔炉的标志——一团燃烧的地狱火中有一把铁锤。徽章佩戴时，会根据附近热源的数量变化光芒的强度——越多越明亮。\n\n撒旦在锻造这枚徽章时，将自己的「地狱熔炉之魂」封入了火焰。他说："这枚徽章不只是徽章，它是'地狱的象征'——让所有人知道，地狱熔炉来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他地狱熔炉的存在（徽章会微微发热）。且可以通过徽章「召唤」——每日一次，召唤一团地狱火（5 尺半径，困难地形，所有生物受到 2D10 fire + 1D10 necrotic/回合，持续 3 回合）。撒旦说："徽章不是只能佩戴，它也可以用来召唤——召唤地狱的火焰。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "inferno_forge_set_88"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_inferno_forge_badge_88" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_inferno_forge_badge_88" }
]
```

---

## 套装八十九：天堂守卫（Heavenly Sentinel）饰品

---

### 89.5 天堂守卫斗篷

```gdscript
item_id = "acc_heavenly_sentinel_cloak_89"
display_name = "天堂守卫斗篷"
description = "一件由天堂之光与神圣丝编织而成的华丽斗篷，斗篷表面不断有天堂之光在流动——这不是装饰，而是真正的神圣之光被封印在了丝线中。斗篷非常轻盈——因为神圣之光没有重量，但也因为这轻盈，穿戴者在阳光下会微微飘起。斗篷的边缘有特殊的「天堂流苏」——每一根流苏都会让周围的空气微微发光，仿佛天堂降临。\n\n米迦勒在编织这件斗篷时，将天堂之光本身都编入了丝线。他说："这件斗篷不只是斗篷，它是'天堂的化身'——让我穿着神圣之光本身。"\n\n这件斗篷的特殊效果是：穿戴者免疫「radiant」和「necrotic」damage。且可以通过斗篷「释放」天堂之光——每日一次，释放一道天堂之光（15 尺锥形 3D10 radiant，undead 和 fiend 受到双倍伤害，目标须通过 DC18 体质豁免，失败则 blinded 1 回合）。米迦勒说："斗篷不是只能防护，它也可以攻击——用天堂之光来净化邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_heavenly_sentinel_cloak_89" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_heavenly_sentinel_cloak_89" }
]
```

---

### 89.6 天堂守卫项链

```gdscript
item_id = "acc_heavenly_sentinel_necklace_89"
display_name = "天堂守卫项链"
description = "一条由天堂之光与神圣丝串联而成的项链，项链中央镶嵌着一颗「天堂之心」——一颗能够感知并净化邪恶的魔法宝石。项链佩戴时，宝石会随邪恶的接近变红——越邪恶越红。\n\n米迦勒在制作这条项链时，从天堂之光中取下了结晶。他说："这块结晶不是被取下的，是'被赐予的'——天堂选择了我，让我成为它的守卫者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有邪恶生物的位置（天堂之心微微发热指示方向）。且可以通过项链「净化」——每日一次，净化 15 尺内所有 undead 和 fiend（3D10 radiant，双倍伤害）。米迦勒说："天堂之心不是只能感知，它也可以净化——净化所有邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_heavenly_sentinel_necklace_89" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_heavenly_sentinel_necklace_89" }
]
```

---

### 89.7 天堂守卫戒指（左）

```gdscript
item_id = "acc_heavenly_sentinel_ring_1_89"
display_name = "天堂守卫戒指·圣光"
description = "一枚由天堂之光与神圣丝锻造而成的精致戒指，戒指表面不断有天堂之光在流动——在黑暗中会照亮周围。戒指佩戴时，周围的空气会微微温暖——形成一层神圣护盾。\n\n米迦勒在锻造这枚戒指时，将一束天堂之光封入了金属。他说："这束光不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」圣光—— bonus action，释放一道圣光（30 尺射程 2D10 radiant，对 evil 双倍，每日三次）。且在进行光明法术时，伤害 +1D8（圣光强化）。米迦勒说："圣光不是只能用来照明，它也可以用来攻击——用圣光来净化邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_heavenly_sentinel_ring_1_89" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_heavenly_sentinel_ring_1_89" }
]
```

---

### 89.8 天堂守卫戒指（右）

```gdscript
item_id = "acc_heavenly_sentinel_ring_2_89"
display_name = "天堂守卫戒指·守护"
description = "一枚由守护石与神圣丝锻造而成的精致戒指，戒指表面有类似盾牌的纹理——坚硬、光滑、充满保护力。戒指佩戴时，会根据周围盟友的数量变化光芒——越多越明亮。\n\n米迦勒在锻造这枚戒指时，将自己的「守护之心」封入了石头。他说："这枚戒指不只是戒指，它是'天堂的守护'——让我可以保护所有需要保护的人。"\n\n这枚戒指的特殊效果是：穿戴者可以「守护」—— bonus action，选择一个 5 尺内的盟友，该盟友获得 +3 AC 和免疫 necrotic（守护光环，持续 1 回合）。且可以通过戒指「治愈」——每日一次，治愈一个盟友 3D8 HP（天堂治愈）。米迦勒说："守护不是只能用来防御，它也可以用来治愈——用天堂之光来治愈盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_heavenly_sentinel_ring_2_89" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_heavenly_sentinel_ring_2_89" }
]
```

---

### 89.9 天堂守卫特殊饰品

```gdscript
item_id = "acc_heavenly_sentinel_trinket_89"
display_name = "天堂守卫圣杯"
description = "一个由天堂之光与神圣丝制成的小型圣杯，圣杯内永远有「天堂之露」——一种可以治愈任何伤口、净化任何邪恶的神圣液体。圣杯佩戴时，会随穿戴者的意愿微微发光——当有治愈需求时发光最强。\n\n米迦勒在制作这个圣杯时，将自己的「治愈之光」封入了玻璃。他说："这个圣杯不是普通的容器，它是'天堂的源泉'——让我可以治愈任何伤口。"\n\n这个圣杯的特殊效果是：穿戴者可以用圣杯「治愈」—— bonus action，治愈一个 5 尺内的盟友 3D8 HP（天堂治愈，每日三次）。且可以通过圣杯「净化」——每日一次，净化一个目标的所有 diseases、poisons、curses 和 evil influence。米迦勒说："圣杯不是只能用来治愈，它也可以用来净化——净化所有邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_heavenly_sentinel_trinket_89" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_heavenly_sentinel_trinket_89" }
]
```

---

### 89.10 天堂守卫徽章

```gdscript
item_id = "acc_heavenly_sentinel_badge_89"
display_name = "天堂守卫徽章"
description = "一枚由天堂之光与神圣丝锻造而成的华丽徽章，徽章上刻有天堂守卫的标志——一对展开的翅膀中有一把燃烧的圣剑。徽章佩戴时，会根据附近邪恶的数量变化光芒的强度——越多越明亮。\n\n米迦勒在锻造这枚徽章时，将自己的「天堂守卫之魂」封入了光芒。他说："这枚徽章不只是徽章，它是'天堂的象征'——让所有人知道，天堂守卫来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他天堂守卫的存在（徽章会微微发热）。且可以通过徽章「号召」——每日一次，号召 15 尺内所有盟友发动神圣冲锋（所有盟友对 evil 攻击检定 +3，持续 1 分钟）。米迦勒说："徽章不是只能佩戴，它也可以用来号召——号召所有正义的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "heavenly_sentinel_set_89"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_heavenly_sentinel_badge_89" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_heavenly_sentinel_badge_89" }
]
```

---

## 套装九十：创世巨人（World Forge Titan）饰品

---

### 90.5 创世巨人斗篷

```gdscript
item_id = "acc_world_forge_titan_cloak_90"
display_name = "创世巨人斗篷"
description = "一件由创世之石与创世丝编织而成的宏伟斗篷，斗篷表面不断有世界在诞生和毁灭——你可以看到山川在隆起、海洋在形成、生命在萌发。斗篷非常沉重——因为上面承载的是一个世界的重量，但也因为这重量，穿戴者可以感受到创世之力。斗篷的边缘有特殊的「创世流苏」——每一根流苏都代表一个正在诞生的世界。\n\n盘古在编织这件斗篷时，将自己的创世之力都编入了丝线。他说："这件斗篷不只是斗篷，它是'世界的缩影'——让我穿着世界本身。"\n\n这件斗篷的特殊效果是：穿戴者可以「创造」——每日一次，在 5 尺内创造一个小型地形（例如山丘、河流、森林，持续 1 小时）。且可以通过斗篷「毁灭」——每日一次，毁灭 15 尺内一个地形（所有生物须通过 DC18 敏捷豁免，失败则受到 4D10 force 并被推后 20 尺）。盘古说："斗篷不是只能装饰，它也可以创造——用创世之力来改变世界。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "world_forge_titan_set_90"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "strength_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_world_forge_titan_cloak_90" },
    { attribute_id = "constitution_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_world_forge_titan_cloak_90" }
]
```

---

### 90.6 创世巨人项链

```gdscript
item_id = "acc_world_forge_titan_necklace_90"
display_name = "创世巨人项链"
description = "一条由创世之石与创世丝串联而成的项链，项链中央镶嵌着一颗「创世之心」——一颗能够感知并改变世界结构的魔法宝石。项链佩戴时，宝石会随世界的脉动微微发光——脉动与世界的存在同步。\n\n盘古在制作这条项链时，从自己的心脏中取下了碎片。他说："这块碎片不是被取下的，是'被赐予的'——世界选择了我，让我成为它的创造者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 5 里内所有地形的变化和自然灾害（自然灾害雷达）。且可以通过项链「改变」——每日一次，改变 15 尺内一个小型地形（例如将平地变成沼泽、将河流变成冰面）。盘古说："创世之心不是只能感知，它也可以改变——改变世界的每一个角落。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "world_forge_titan_set_90"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_world_forge_titan_necklace_90" },
    { attribute_id = "max_mana", mode = "flat", value = 30, source_type = "equipment", source_id = "acc_world_forge_titan_necklace_90" }
]
```

---

### 90.7 创世巨人戒指（左）

```gdscript
item_id = "acc_world_forge_titan_ring_1_90"
display_name = "创世巨人戒指·创造"
description = "一枚由创世之石与创世丝锻造而成的奇异戒指，戒指表面有类似世界的纹理——在光线下会折射出山川和海洋。戒指佩戴时，周围的空气会微微震动——形成一层创世之力。\n\n盘古在锻造这枚戒指时，将自己的「创造之力」封入了石头。他说："这枚戒指不只是戒指，它是'创造的钥匙'——让我可以创造任何想要的东西。"\n\n这枚戒指的特殊效果是：穿戴者可以「创造」物体—— bonus action，在 5 尺内创造一个小型物体（大小不超过 1 立方尺，持续 1 小时，每日三次）。且在进行创造检定时，检定 +5（创世之力）。盘古说："创造不是只能用来装饰，它也可以用来攻击——用创造的物体来攻击敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "world_forge_titan_set_90"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_world_forge_titan_ring_1_90" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_world_forge_titan_ring_1_90" }
]
```

---

### 90.8 创世巨人戒指（右）

```gdscript
item_id = "acc_world_forge_titan_ring_2_90"
display_name = "创世巨人戒指·毁灭"
description = "一枚由毁灭石与创世丝锻造而成的粗糙戒指，戒指表面有类似裂缝的纹理——在光线下会折射出毁灭的光芒。戒指佩戴时，会根据周围毁灭的程度变化温度——毁灭越多越热。\n\n盘古在锻造这枚戒指时，将自己的「毁灭之力」封入了石头。他说："这枚戒指不只是戒指，它是'毁灭的钥匙'——让我可以毁灭任何想要毁灭的东西。"\n\n这枚戒指的特殊效果是：穿戴者可以「毁灭」—— bonus action，毁灭 5 尺内一个小型物体（大小不超过 1 立方尺，每日三次）。且在进行毁灭攻击时，伤害 +2D10（毁灭之力）。盘古说："毁灭不是只能用来破坏，它也可以用来攻击——用毁灭之力来击败敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "world_forge_titan_set_90"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_world_forge_titan_ring_2_90" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_world_forge_titan_ring_2_90" }
]
```

---

### 90.9 创世巨人特殊饰品

```gdscript
item_id = "acc_world_forge_titan_trinket_90"
display_name = "创世巨人世界模型"
description = "一个由创世之石与创世丝制成的小型世界模型，世界模型内有完整的世界——山川、河流、海洋、生命。世界模型佩戴时，会随世界的脉动微微发光——脉动与世界的存在同步。\n\n盘古在制作这个世界模型时，将自己的「世界之魂」封入了石头。他说："这个世界模型不是普通的玩具，它是'世界的缩影'——让我可以操控整个世界。"\n\n这个世界模型的特殊效果是：穿戴者可以用世界模型「创造」地形——每日一次，在 30 尺内创造一个中型地形（例如山丘、河流、森林，持续 1 小时）。且可以通过世界模型「毁灭」——每日一次，毁灭 30 尺内一个地形（所有生物须通过 DC18 敏捷豁免，失败则受到 4D10 force 并被推后 20 尺）。盘古说："世界模型不是只能用来观赏，它也可以用来创造——创造任何想要的地形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "world_forge_titan_set_90"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_world_forge_titan_trinket_90" },
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_world_forge_titan_trinket_90" }
]
```

---

### 90.10 创世巨人徽章

```gdscript
item_id = "acc_world_forge_titan_badge_90"
display_name = "创世巨人徽章"
description = "一枚由创世之石与创世丝锻造而成的宏伟徽章，徽章上刻有创世巨人的标志——一个正在创造世界的巨人。徽章佩戴时，会根据附近生命的数量变化光芒的强度——越多越明亮。\n\n盘古在锻造这枚徽章时，将自己的「创世巨人之魂」封入了石头。他说："这枚徽章不只是徽章，它是'世界的象征'——让所有人知道，创世巨人来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他创世巨人的存在（徽章会微微发热）。且可以通过徽章「创造」——每日一次，在 15 尺内创造一层保护地形（所有盟友获得 +2 AC 和 cover，持续 1 分钟）。盘古说："徽章不是只能佩戴，它也可以用来创造——创造保护所有盟友的地形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "world_forge_titan_set_90"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_world_forge_titan_badge_90" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_world_forge_titan_badge_90" }
]
```

---

*重甲套装 81–90 饰品部分完结 · 共 60 件饰品装备*

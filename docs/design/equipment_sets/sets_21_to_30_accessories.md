# 传奇装备套装饰品设计文档（套装 21–30：中甲套装饰品）

> 10 套中甲套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装二十一：银月游侠（Silvermoon Ranger）饰品

---

### 21.5 银月游侠斗篷

```gdscript
item_id = "acc_silvermoon_ranger_cloak"
display_name = "银月游侠斗篷"
description = "一件由银月城圣树的落叶与月光银丝编织而成的斗篷，斗篷表面有类似月光的纹理——在黑暗中会发出微弱的银光。斗篷的边缘镶嵌着小型风铃——不是为装饰，而是为了在风吹过时的声音可以迷惑敌人的方向感。\n\n希尔瓦娜斯在编织这件斗篷时，收集了所有在月光下掉落的树叶。她说："月光下的树叶不会发出声音，但风铃会——风铃的声音是我的诱饵，让敌人永远找不到我。"\n\n这件斗篷的特殊效果是：在月光下，斗篷会自动与周围环境融为一体（+3 AC 来自 camouflage）。且可以通过斗篷「释放」月光——每日一次，释放一道月光（30 尺射程 2D8 radiant，目标须通过 DC14 体质豁免，失败则 blinded 1 回合）。希尔瓦娜斯说："斗篷不是只能隐藏，它也可以攻击——用月光来致盲敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "silvermoon_ranger_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_silvermoon_ranger_cloak" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_silvermoon_ranger_cloak" }
]
```

---

### 21.6 银月游侠项链

```gdscript
item_id = "acc_silvermoon_ranger_necklace"
display_name = "银月游侠项链"
description = "一条由银月城圣树的树脂与月光石串联而成的项链，项链中央镶嵌着一颗「月光之眼」——一颗能够感知月光方向的魔法宝石。项链佩戴时，宝石会微微发光，光芒的方向指向月亮的位置。\n\n希尔瓦娜斯在制作这条项链时，将一颗月光石封入了树脂。她说："这颗月光石不是普通的宝石，它是'月光的指南针'——让我永远不会在月光下迷路。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到月亮的方向（即使在云层下或洞穴中）。且可以通过项链「吸收」月光——在月光下，每小时恢复 1 HP（月光治愈）。希尔瓦娜斯说："月光不是只能用来照明，它也可以用来治愈——只是需要正确的工具。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "silvermoon_ranger_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_silvermoon_ranger_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_silvermoon_ranger_necklace" }
]
```

---

### 21.7 银月游侠戒指（左）

```gdscript
item_id = "acc_silvermoon_ranger_ring_1"
display_name = "银月游侠戒指·月痕"
description = "一枚由银月城圣树的年轮与月光银锻造而成的精致戒指，戒指表面刻有月亮的盈亏图案。戒指佩戴时，会根据月亮的相位变化发出不同强度的光芒——新月时暗淡，满月时明亮。\n\n希尔瓦娜斯在锻造这枚戒指时，将一段圣树的年轮封入了银中。她说："这段年轮不是普通的纹理，它是'时间的记录'——记录了一千年的月光。"\n\n这枚戒指的特殊效果是：在满月时，戒指会发出最强的光芒（攻击检定 +1）。在新月时，光芒最暗淡（隐匿检定 +2）。希尔瓦娜斯说："月亮的每一个相位都有它的力量——满月时攻击，新月时隐匿。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "silvermoon_ranger_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_silvermoon_ranger_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_silvermoon_ranger_ring_1" }
]
```

---

### 21.8 银月游侠戒指（右）

```gdscript
item_id = "acc_silvermoon_ranger_ring_2"
display_name = "银月游侠戒指·星痕"
description = "一枚由银月城圣树的树皮与星辰碎片锻造而成的精致戒指，戒指表面刻有星座图案。戒指佩戴时，会根据当前可见的星座发出不同颜色的光芒——猎户座时蓝色，仙女座时粉色，北斗七星时白色。\n\n希尔瓦娜斯在锻造这枚戒指时，将一颗微型星辰碎片封入了树皮。她说："这颗碎片不是普通的石头，它是'星辰的记忆'——记录了整个宇宙的历史。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到当前可见的星座（自动识别）。且可以通过戒指「借助」星辰之力——每日一次，选择一颗可见的星座，获得对应的加成（猎户座：攻击检定 +2；仙女座：魅力检定 +2；北斗七星：导航检定 +5）。希尔瓦娜斯说："星辰不是只能看的，它们也可以用来导航——用星辰来确定方向，用星座来获得力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "silvermoon_ranger_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_silvermoon_ranger_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_silvermoon_ranger_ring_2" }
]
```

---

### 21.9 银月游侠特殊饰品

```gdscript
item_id = "acc_silvermoon_ranger_trinket"
display_name = "银月游侠月影吊坠"
description = "一个由银月城圣树的树瘤与月光精华凝结而成的小型吊坠，吊坠呈现出半透明的银白色，内部有一团不断流动的月光。吊坠佩戴时，会随穿戴者的心跳微微脉动——月光与心跳同步，形成一种独特的节奏。\n\n希尔瓦娜斯在制作这个吊坠时，将一整年的月光精华都凝结在了一起。她说："这个吊坠不是普通的饰品，它是'月光的心脏'——让我可以感受到月光的脉动。"\n\n这个吊坠的特殊效果是：在月光下，吊坠会「共鸣」——所有与月光相关的技能效果 +1（例如月光治愈、月光攻击、月光隐匿等）。且可以通过吊坠「召唤」月光——每日一次，在 15 尺半径内创造出人工月光（持续 1 分钟，所有月光技能生效）。希尔瓦娜斯说："月光不是只能等待的，它也可以被召唤——只要有正确的工具。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "silvermoon_ranger_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_silvermoon_ranger_trinket" },
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_silvermoon_ranger_trinket" }
]
```

---

### 21.10 银月游侠徽章

```gdscript
item_id = "acc_silvermoon_ranger_badge"
display_name = "银月游侠徽章"
description = "一枚由银月城圣树的叶片与月光银锻造而成的徽章，徽章上刻有银月游侠的标志——一轮弯月下的一把弓箭。徽章佩戴时，会根据穿戴者的情绪变化光芒的强度——平静时暗淡，兴奋时明亮，愤怒时炽烈。\n\n希尔瓦娜斯在锻造这枚徽章时，将自己的「游侠之心」封入了叶片。她说："这枚徽章不只是徽章，它是'身份的证明'——证明我是银月游侠的一员。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他银月游侠的存在（徽章会微微发热）。且可以通过徽章「传递」信息——每日一次，向 1 里内的其他银月游侠传递一条简短的信息（如同 message spell）。希尔瓦娜斯说："徽章不是只能佩戴，它也可以用来通信——让分散的游侠可以相互联系。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "silvermoon_ranger_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_silvermoon_ranger_badge" },
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_silvermoon_ranger_badge" }
]
```

---

## 套装二十二：沙漠蝎刺（Desert Scorpion）饰品

---

### 22.5 沙漠蝎刺斗篷

```gdscript
item_id = "acc_desert_scorpion_cloak"
display_name = "沙漠蝎刺斗篷"
description = "一件由沙漠毒蝎的蜕皮与耐热沙丝编织而成的斗篷，斗篷表面有类似蝎壳的纹理，在阳光下会反射出金属般的光泽。斗篷的边缘镶有微型毒刺——不是为攻击，而是为了在沙尘暴中保持斗篷的形状不被吹走。\n\n克里奥帕特拉在编织这件斗篷时，收集了所有她遇到的毒蝎的蜕皮。她说："蜕皮不是死亡，它是'重生'——每一次蜕皮，毒蝎都变得更加强大。"\n\n这件斗篷的特殊效果是：在沙漠中，斗篷会自动调整颜色以匹配沙地（+3 AC 来自 camouflage）。且可以通过斗篷「释放」沙尘——每日一次，释放一道沙尘暴（15 尺锥形，所有生物 blinded 1 回合，须通过 DC14 体质豁免）。克里奥帕特拉说："斗篷不是只能隐藏，它也可以攻击——用沙尘来致盲敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "desert_scorpion_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_desert_scorpion_cloak" },
    { attribute_id = "resistance_fire", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_desert_scorpion_cloak" }
]
```

---

### 22.6 沙漠蝎刺项链

```gdscript
item_id = "acc_desert_scorpion_necklace"
display_name = "沙漠蝎刺项链"
description = "一条由沙漠毒蝎的毒腺与耐热沙石串联而成的项链，项链中央镶嵌着一颗「毒蝎之眼」——一颗由真正毒蝎眼睛制成的宝石。项链佩戴时，宝石会发出微弱的绿光，光芒的强度代表周围毒物的浓度。\n\n克里奥帕特拉在制作这条项链时，从一只活了五十年的毒蝎身上取下了它的眼睛。她说："这只眼睛不是被取下的，是'被继承的'——它选择了我，让我成为它的继承者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有毒素和毒物的位置（毒物雷达）。且可以通过项链「提取」毒素——每日一次，从周围的毒物中提取一剂毒液（涂抹武器或混入食物，效果持续 1 小时）。克里奥帕特拉说："毒蝎之眼不是只能看，它也可以感知——感知所有毒物的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "desert_scorpion_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_desert_scorpion_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_desert_scorpion_necklace" }
]
```

---

### 22.7 沙漠蝎刺戒指（左）

```gdscript
item_id = "acc_desert_scorpion_ring_1"
display_name = "沙漠蝎刺戒指·毒牙"
description = "一枚由沙漠毒蝎的毒牙与耐热金属锻造而成的粗糙戒指，戒指表面有类似毒牙的纹理——尖锐、粗糙、充满威胁。戒指佩戴时，会根据周围温度变化颜色——炎热时红色，寒冷时蓝色。\n\n克里奥帕特拉在锻造这枚戒指时，将一只巨型毒蝎的两颗毒牙都熔入了金属。她说："这两颗毒牙不是被熔化的，是'被融合的'——它们的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」毒素——每日一次，触碰一个目标，注入毒液（目标须通过 DC14 体质豁免，失败则 poisoned 1 分钟，每回合受到 1D6 poison）。克里奥帕特拉说："毒牙不是只能用来咬，它也可以用来刺——用戒指上的毒牙刺入敌人的皮肤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "desert_scorpion_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_desert_scorpion_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_desert_scorpion_ring_1" }
]
```

---

### 22.8 沙漠蝎刺戒指（右）

```gdscript
item_id = "acc_desert_scorpion_ring_2"
display_name = "沙漠蝎刺戒指·甲壳"
description = "一枚由沙漠毒蝎的甲壳与耐热金属锻造而成的厚重戒指，戒指表面有类似甲壳的纹理——坚硬、粗糙、布满伤痕。戒指佩戴时，会随穿戴者的心跳微微发热——热量来自甲壳中储存的沙漠能量。\n\n克里奥帕特拉在锻造这枚戒指时，将一只巨型毒蝎的整个甲壳都熔入了金属。她说："这个甲壳不是被熔化的，是'被融合的'——它的防御现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者的 AC +1（甲壳防御）。且在炎热环境中，戒指会「储存」热量——每小时储存 1 点热量能量（最多 10 点），储存的热量可以在需要时释放——每点热量可以恢复 1 HP（热量治愈）。克里奥帕特拉说："甲壳不是只能用来防御，它也可以用来储存——储存热量，储存生命。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "desert_scorpion_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_desert_scorpion_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_desert_scorpion_ring_2" }
]
```

---

### 22.9 沙漠蝎刺特殊饰品

```gdscript
item_id = "acc_desert_scorpion_trinket"
display_name = "沙漠蝎刺毒囊"
description = "一个由沙漠毒蝎的毒囊与耐热皮革制成的小型容器，容器内储存着一滴「永恒毒液」——一滴永远不会蒸发、永远不会变质的毒液。毒囊佩戴时，会随穿戴者的心跳微微脉动——毒液在脉动，仿佛有生命一般。\n\n克里奥帕特拉在制作这个毒囊时，从一只活了百年的毒蝎身上取下了它的毒囊。她说："这个毒囊不是被取下的，是'被继承的'——它选择了我，让我成为它的守护者。"\n\n这个毒囊的特殊效果是：穿戴者可以「提取」毒液——每日三次，提取一滴毒液（涂抹武器或混入食物，效果持续 1 小时，攻击附加 1D6 poison）。且可以通过毒囊「感知」毒素——30 尺内任何毒素或毒物会被自动识别。克里奥帕特拉说："毒囊不是只能储存，它也可以感知——感知所有毒素的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "desert_scorpion_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_desert_scorpion_trinket" },
    { attribute_id = "resistance_poison", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_desert_scorpion_trinket" }
]
```

---

### 22.10 沙漠蝎刺徽章

```gdscript
item_id = "acc_desert_scorpion_badge"
display_name = "沙漠蝎刺徽章"
description = "一枚由沙漠毒蝎的尾刺与耐热金属锻造而成的徽章，徽章上刻有沙漠蝎尾部落的标志——一只举尾欲刺的毒蝎。徽章佩戴时，会根据周围危险程度变化光芒的强度——安全时暗淡，危险时明亮，致命时炽烈。\n\n克里奥帕特拉在锻造这枚徽章时，将自己的「蝎后之心」封入了尾刺。她说："这枚徽章不只是徽章，它是'警告的信号'——让所有人知道，沙漠蝎刺来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有毒蝎的存在（徽章会微微发热）。且可以通过徽章「召唤」毒蝎——每日一次，召唤一只巨型毒蝎协助战斗（持续 10 分钟）。克里奥帕特拉说："徽章不是只能佩戴，它也可以用来召唤——召唤沙漠中最致命的猎手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "desert_scorpion_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_desert_scorpion_badge" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_desert_scorpion_badge" }
]
```

---

## 套装二十三：丛林猎豹（Jungle Panther）饰品

---

### 23.5 丛林猎豹斗篷

```gdscript
item_id = "acc_jungle_panther_cloak"
display_name = "丛林猎豹斗篷"
description = "一件由黑豹皮毛与雨林藤蔓编织而成的斗篷，斗篷表面有类似豹纹的图案——在丛林中几乎无法被肉眼察觉。斗篷的边缘有特殊的「无声流苏」——在风中不会产生任何声音。\n\n塞克美特在编织这件斗篷时，将整只黑豹的尾毛都编入了斗篷。她说："这件斗篷不只是隐藏，它是'丛林的一部分'——让我可以成为丛林中的幽灵。"\n\n这件斗篷的特殊效果是：在丛林/雨林中，斗篷提供 +3 AC（camouflage）。且可以通过斗篷「释放」豹吼——每日一次，释放一声豹吼（15 尺锥形，所有生物须通过 DC14 智慧豁免，失败则 frightened 1 回合）。塞克美特说："斗篷不是只能隐藏，它也可以攻击——用豹吼来震慑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "jungle_panther_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_jungle_panther_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_cloak" }
]
```

---

### 23.6 丛林猎豹项链

```gdscript
item_id = "acc_jungle_panther_necklace"
display_name = "丛林猎豹项链"
description = "一条由黑豹的牙齿与雨林种子串联而成的项链，项链中央镶嵌着一颗「猎豹之眼」——一颗由真正猎豹眼睛制成的宝石。项链佩戴时，宝石会发出微弱的绿光，让穿戴者在黑暗中也能看清。\n\n塞克美特在制作这条项链时，从一只黑豹身上取下了它最锋利的一颗牙齿。她说："这颗牙齿不是被取下的，是'被继承的'——它选择了我，让我成为它的继承者。"\n\n这条项链的特殊效果是：穿戴者获得「夜视」——在黑暗中看清 60 尺（darkvision）。且可以通过项链「感知」到 30 尺内所有猫科动物的位置（猎豹雷达）。塞克美特说："猎豹之眼不是只能看，它也可以感知——感知所有猫科动物的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "jungle_panther_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_jungle_panther_necklace" }
]
```

---

### 23.7 丛林猎豹戒指（左）

```gdscript
item_id = "acc_jungle_panther_ring_1"
display_name = "丛林猎豹戒指·爪痕"
description = "一枚由黑豹的爪子与雨林硬木锻造而成的锋利戒指，戒指表面有类似爪痕的纹理——尖锐、锋利、充满野性。戒指佩戴时，会根据穿戴者的情绪变化温度——平静时温暖，兴奋时炎热，愤怒时炽烈。\n\n塞克美特在锻造这枚戒指时，将一只黑豹的四只爪子都熔入了金属。她说："这四只爪子不是被熔化的，是'被融合的'——它们的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指进行「爪击」—— bonus action，用戒指划向一个目标（1D6 slashing，每日三次）。且在丛林中，攻击检定 +1（丛林主场）。塞克美特说："爪子不是只能用来抓，它也可以用来刺——用戒指上的爪子刺入敌人的皮肤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "jungle_panther_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_jungle_panther_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_jungle_panther_ring_1" }
]
```

---

### 23.8 丛林猎豹戒指（右）

```gdscript
item_id = "acc_jungle_panther_ring_2"
display_name = "丛林猎豹戒指·斑纹"
description = "一枚由黑豹的斑纹皮毛与雨林树脂锻造而成的精致戒指，戒指表面有类似豹纹的图案——每一片斑纹都来自不同的黑豹，因此呈现出深浅不一的黑色和金色。戒指佩戴时，会根据周围环境变化颜色——在丛林中变绿，在沙漠中变黄，在雪地中变白。\n\n塞克美特在锻造这枚戒指时，收集了所有她遇到的黑豹的斑纹皮毛。她说："这些斑纹不是被收集的，是'被继承的'——它们选择了我，让我成为它们的守护者。"\n\n这枚戒指的特殊效果是：在任何环境中，戒指会自动调整颜色以匹配环境（+1 AC 来自 camouflage）。且可以通过戒指「感知」到 30 尺内所有猎物的位置（猎物雷达）。塞克美特说："斑纹不是只能用来隐藏，它也可以用来感知——感知所有猎物的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "jungle_panther_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_jungle_panther_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_jungle_panther_ring_2" }
]
```

---

### 23.9 丛林猎豹特殊饰品

```gdscript
item_id = "acc_jungle_panther_trinket"
display_name = "丛林猎豹尾鞭"
description = "一个由黑豹的尾骨与雨林藤蔓制成的小型鞭子，鞭子长度约一尺，可以缠绕在手腕上。鞭子表面覆盖着黑豹的尾毛——柔软但坚韧，可以用来攻击或捆绑。鞭子佩戴时，会随穿戴者的动作自然摆动，仿佛还有生命。\n\n塞克美特在制作这个鞭子时，从一只黑豹身上取下了它的整条尾巴。她说："这条尾巴不是被取下的，是'被继承的'——它选择了我，让我成为它的继承者。"\n\n这个鞭子的特殊效果是：穿戴者可以用鞭子进行「尾击」—— bonus action，用鞭子抽向一个目标（1D6 bludgeoning + grappled 1 回合，须通过 DC14 敏捷豁免，每日三次）。且可以通过鞭子「攀爬」——鞭子可以缠绕树枝或岩石，帮助攀爬（攀爬检定 +3）。塞克美特说："尾巴不是只能用来平衡，它也可以用来攻击——用尾巴鞭打敌人，用尾巴抓住猎物。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "jungle_panther_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_trinket" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_trinket" }
]
```

---

### 23.10 丛林猎豹徽章

```gdscript
item_id = "acc_jungle_panther_badge"
display_name = "丛林猎豹徽章"
description = "一枚由黑豹的头骨碎片与雨林硬木锻造而成的徽章，徽章上刻有丛林猎豹的标志——一只跃起扑击的黑豹。徽章佩戴时，会根据周围危险程度变化光芒的强度——安全时暗淡，危险时明亮，致命时炽烈。\n\n塞克美特在锻造这枚徽章时，将自己的「猎豹之魂」封入了头骨碎片。她说："这枚徽章不只是徽章，它是'警告的信号'——让所有人知道，丛林猎豹来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有猫科动物的存在（徽章会微微发热）。且可以通过徽章「召唤」黑豹——每日一次，召唤一只黑豹协助战斗（持续 10 分钟）。塞克美特说："徽章不是只能佩戴，它也可以用来召唤——召唤丛林中最致命的猎手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "jungle_panther_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_badge" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_jungle_panther_badge" }
]
```

---

## 套装二十四：高山雄鹰（Highland Eagle）饰品

---

### 24.5 高山雄鹰斗篷

```gdscript
item_id = "acc_highland_eagle_cloak"
display_name = "高山雄鹰斗篷"
description = "一件由雄鹰羽毛与高山风丝编织而成的斗篷，斗篷表面覆盖着层层叠叠的鹰羽——每一片羽毛都来自不同的雄鹰，因此呈现出深浅不一的棕色和金色。斗篷的边缘有特殊的设计——不是为美观，而是为了让斗篷在飞行时可以像翅膀一样展开。\n\n荷鲁斯在编织这件斗篷时，收集了所有他遇到的雄鹰的羽毛。他说："这件斗篷不只是斗篷，它是'翅膀的延伸'——让我可以在空中滑翔。"\n\n这件斗篷的特殊效果是：穿戴者可以从高处「滑翔」——免疫 falling damage（斗篷展开滑翔）。且可以通过斗篷「释放」风刃——每日一次，释放一道风刃（15 尺锥形 2D8 slashing，目标须通过 DC14 敏捷豁免）。荷鲁斯说："斗篷不是只能滑翔，它也可以攻击——用风刃来切割敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "highland_eagle_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_highland_eagle_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_highland_eagle_cloak" }
]
```

---

### 24.6 高山雄鹰项链

```gdscript
item_id = "acc_highland_eagle_necklace"
display_name = "高山雄鹰项链"
description = "一条由雄鹰的嗉囊石与高山水晶串联而成的项链，项链中央镶嵌着一颗「鹰眼石」——一颗能够看穿云雾和风暴的魔法宝石。项链佩戴时，宝石会发出微弱的蓝光，让穿戴者可以看清最远的目标。\n\n荷鲁斯在制作这条项链时，从一只活了八十年的雄鹰身上取下了它的嗉囊石。他说："这颗石头不是被取下的，是'被继承的'——它选择了我，让我成为它的继承者。"\n\n这条项链的特殊效果是：穿戴者的视野范围翻倍（可以看清 2 里外的细节）。且可以通过项链「感知」到 1 里内所有风暴的位置和强度（风暴雷达）。荷鲁斯说："鹰眼石不是只能看，它也可以感知——感知所有风暴的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "highland_eagle_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_highland_eagle_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_highland_eagle_necklace" }
]
```

---

### 24.7 高山雄鹰戒指（左）

```gdscript
item_id = "acc_highland_eagle_ring_1"
display_name = "高山雄鹰戒指·爪痕"
description = "一枚由雄鹰的爪子与高山金属锻造而成的锋利戒指，戒指表面有类似鹰爪的纹理——尖锐、弯曲、充满力量。戒指佩戴时，会根据海拔高度变化光芒的强度——越高越明亮。\n\n荷鲁斯在锻造这枚戒指时，将一只巨型雄鹰的四只爪子都熔入了金属。他说："这四只爪子不是被熔化的，是'被融合的'——它们的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指进行「爪击」—— bonus action，用戒指抓向一个目标（1D6 slashing，每日三次）。且在高处（超过 1000 米），攻击检定 +1（高空主场）。荷鲁斯说："爪子不是只能用来抓，它也可以用来刺——用戒指上的爪子刺入敌人的皮肤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "highland_eagle_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_highland_eagle_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_highland_eagle_ring_1" }
]
```

---

### 24.8 高山雄鹰戒指（右）

```gdscript
item_id = "acc_highland_eagle_ring_2"
display_name = "高山雄鹰戒指·风羽"
description = "一枚由雄鹰的飞羽与高山风丝编织而成的轻盈戒指，戒指表面有类似羽毛的纹理——轻盈、柔软、充满弹性。戒指佩戴时，会根据风速变化温度——风大时凉爽，风小时温暖。\n\n荷鲁斯在锻造这枚戒指时，收集了所有他遇到的雄鹰的飞羽。他说："这些羽毛不是被收集的，是'被继承的'——它们选择了我，让我成为它们的守护者。"\n\n这枚戒指的特殊效果是：穿戴者的移动力 +5（风羽轻盈）。且可以通过戒指「感知」到 30 尺内所有气流的方向和强度（气流雷达）。荷鲁斯说："羽毛不是只能用来飞行，它也可以用来感知——感知所有气流的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "highland_eagle_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_highland_eagle_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_highland_eagle_ring_2" }
]
```

---

### 24.9 高山雄鹰特殊饰品

```gdscript
item_id = "acc_highland_eagle_trinket"
display_name = "高山雄鹰哨笛"
description = "一个由雄鹰的鸣管骨与高山竹制成的小型哨笛，哨笛可以发出高亢的鹰啸声——声音可以传到数里之外。哨笛佩戴时，会随穿戴者的呼吸微微振动——仿佛还有生命。\n\n荷鲁斯在制作这个哨笛时，从一只雄鹰身上取下了它的鸣管骨。他说："这根骨头不是被取下的，是'被继承的'——它选择了我，让我成为它的继承者。"\n\n这个哨笛的特殊效果是：穿戴者可以用哨笛「召唤」雄鹰——每日一次，发出鹰啸召唤一只雄鹰（持续 10 分钟，雄鹰可以侦察或攻击）。且可以通过哨笛「传递」信息——向 2 里内的其他高山雄鹰信徒传递一条简短的信息。荷鲁斯说："哨笛不是只能吹，它也可以用来召唤——召唤天空中的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "highland_eagle_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_highland_eagle_trinket" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_highland_eagle_trinket" }
]
```

---

### 24.10 高山雄鹰徽章

```gdscript
item_id = "acc_highland_eagle_badge"
display_name = "高山雄鹰徽章"
description = "一枚由雄鹰的头骨碎片与高山金属锻造而成的徽章，徽章上刻有高山雄鹰的标志——一只展翅翱翔的雄鹰。徽章佩戴时，会根据海拔高度变化光芒的强度——越高越明亮。\n\n荷鲁斯在锻造这枚徽章时，将自己的「鹰王之魂」封入了头骨碎片。他说："这枚徽章不只是徽章，它是'天空的象征'——让所有人知道，高山雄鹰来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有鸟类动物的存在（徽章会微微发热）。且可以通过徽章「召唤」鹰群——每日一次，召唤 1D4 只雄鹰协助战斗（持续 10 分钟）。荷鲁斯说："徽章不是只能佩戴，它也可以用来召唤——召唤天空中的猎手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "highland_eagle_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_highland_eagle_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_highland_eagle_badge" }
]
```

---

## 套装二十五：海盗船长（Pirate Captain）饰品

---

### 25.5 海盗船长斗篷

```gdscript
item_id = "acc_pirate_captain_cloak"
display_name = "海盗船长斗篷"
description = "一件由深海巨鲨的皮与海盗旗帜编织而成的斗篷，斗篷表面有类似鲨皮的纹理——在阳光下会反射出金属般的光泽。斗篷的边缘镶有小型锚链——不是为装饰，而是为了在风暴中保持斗篷不被吹走。\n\n黑胡子在编织这件斗篷时，将黑帆海盗团的旗帜都缝入了斗篷。他说："这件斗篷不只是斗篷，它是'海盗的旗帜'——让所有人知道，黑帆海盗团来了。"\n\n这件斗篷的特殊效果是：在海上，斗篷提供 +2 AC（海风中旗帜飘扬干扰敌人视线）。且可以通过斗篷「释放」风暴——每日一次，召唤一阵海风（15 尺锥形，所有生物须通过 DC14 力量豁免，失败则推后 10 尺）。黑胡子说："斗篷不是只能装饰，它也可以攻击——用风暴来击退敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "pirate_captain_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_cloak" },
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_cloak" }
]
```

---

### 25.6 海盗船长项链

```gdscript
item_id = "acc_pirate_captain_necklace"
display_name = "海盗船长项链"
description = "一条由深海珍珠与海盗金币串联而成的项链，项链中央镶嵌着一颗「海之泪」——一颗据说由海神的眼泪凝结而成的宝石。项链佩戴时，宝石会发出微弱的蓝光，让穿戴者可以感知海洋的脉动。\n\n黑胡子在制作这条项链时，从一艘沉船宝藏中取下了这颗珍珠。他说："这颗珍珠不是普通的宝石，它是'海神的礼物'——让海洋成为我的朋友。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有船只的位置（海之泪微微发热指示方向）。且可以通过项链「召唤」海风——每日一次，召唤一阵顺风（移动力 +10，持续 1 小时，仅限海上）。黑胡子说："海之泪不是只能看，它也可以感知——感知所有船只的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "pirate_captain_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_pirate_captain_necklace" }
]
```

---

### 25.7 海盗船长戒指（左）

```gdscript
item_id = "acc_pirate_captain_ring_1"
display_name = "海盗船长戒指·金币"
description = "一枚由深海沉船的金币与海盗印记锻造而成的华丽戒指，戒指表面刻有黑帆海盗团的标志——一面黑色风帆下交叉的弯刀。戒指佩戴时，会根据附近宝藏的距离变化温度——越近越热。\n\n黑胡子在锻造这枚戒指时，将自己找到的第一枚金币封入了戒指。他说："这枚金币不是普通的财宝，它是'命运的钥匙'——让财富永远追随我。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 100 尺内所有贵金属的位置（金币雷达）。且可以通过戒指「吸引」财富——每日一次，让附近的金币或小珠宝自动飞向穿戴者（如同 mage hand 的吸引版）。黑胡子说："金币不是只能花，它也可以用来感知——感知所有财富的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "pirate_captain_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_pirate_captain_ring_1" }
]
```

---

### 25.8 海盗船长戒指（右）

```gdscript
item_id = "acc_pirate_captain_ring_2"
display_name = "海盗船长戒指·罗盘"
description = "一枚由深海磁石与海盗罗盘锻造而成的实用戒指，戒指表面刻有罗盘刻度，中央有一颗微型磁针。戒指佩戴时，磁针会始终指向北方——无论在哪里，无论天气如何。\n\n黑胡子在锻造这枚戒指时，将自己使用了三十年的罗盘封入了戒指。他说："这个罗盘不是普通的工具，它是'方向的灵魂'——让我永远不会在海上迷路。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到北方的方向（免疫迷路）。且可以通过戒指「感知」到 1 里内所有磁异常的位置（可以用来发现沉船或隐藏的铁门）。黑胡子说："罗盘不是只能指路，它也可以用来探测——探测所有隐藏的金属。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "pirate_captain_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_pirate_captain_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_pirate_captain_ring_2" }
]
```

---

### 25.9 海盗船长特殊饰品

```gdscript
item_id = "acc_pirate_captain_trinket"
display_name = "海盗船长酒壶"
description = "一个由深海鲸鱼的骨与海盗皮革制成的小型酒壶，酒壶表面刻有黑帆海盗团的航海图。酒壶内永远有酒——不是普通的酒，是「海盗之酒」，一种可以让饮用者暂时忘记恐惧的神奇液体。酒壶佩戴时，会随穿戴者的动作微微晃动，发出悦耳的液体声。\n\n黑胡子在制作这个酒壶时，将自己的「海盗之魂」封入了鲸鱼骨。他说："这个酒壶不是普通的容器，它是'勇气的源泉'——让任何人都可以成为海盗。"\n\n这个酒壶的特殊效果是：穿戴者可以「饮用」海盗之酒——每日一次，饮用后免疫 frightened 1 小时（勇气之酒）。且可以通过酒壶「分享」——让 15 尺内的一个盟友也饮用一口（同样效果）。黑胡子说："酒不是只能喝，它也可以用来激励——激励所有人面对恐惧。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "pirate_captain_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_pirate_captain_trinket" },
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_trinket" }
]
```

---

### 25.10 海盗船长徽章

```gdscript
item_id = "acc_pirate_captain_badge"
display_name = "海盗船长徽章"
description = "一枚由深海沉船的锚与海盗印记锻造而成的徽章，徽章上刻有黑帆海盗团的标志——一面黑色风帆下交叉的弯刀。徽章佩戴时，会根据附近船只的数量变化光芒的强度——越多越明亮。\n\n黑胡子在锻造这枚徽章时，将自己的「船长之心」封入了锚。他说："这枚徽章不只是徽章，它是'权威的象征'——让所有人知道，我是船长。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 1 里内所有船只的位置（徽章会微微发热）。且可以通过徽章「发出信号」——每日一次，向 2 里内的所有海盗船只发出信号（集结、警告或求救）。黑胡子说："徽章不是只能佩戴，它也可以用来指挥——指挥整个海盗舰队。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "pirate_captain_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_pirate_captain_badge" }
]
```

---

## 套装二十六：赏金猎人（Bounty Hunter）饰品

---

### 26.5 赏金猎人斗篷

```gdscript
item_id = "acc_bounty_hunter_cloak"
display_name = "赏金猎人斗篷"
description = "一件由通缉犯皮衣与追踪丝编织而成的斗篷，斗篷表面有无数小型口袋——每个口袋里都装着追踪工具：绳索、钩子、小型弩箭、毒药样本。斗篷的内侧缝有「通缉名单」——以血写就，列出了所有被追捕的目标。\n\n杰西在编织这件斗篷时，将自己抓捕的所有通缉犯的皮衣都缝入了斗篷。他说："这件斗篷不只是斗篷，它是'战利品'——每一件皮衣都代表一个成功。"\n\n这件斗篷的特殊效果是：在追踪时，斗篷提供 +3 AC（追踪 camouflage）。且可以通过斗篷「释放」追踪网——每日一次，释放一张追踪网（15 尺锥形，所有生物须通过 DC14 敏捷豁免，失败则 restrained 1 回合）。杰西说："斗篷不是只能隐藏，它也可以攻击——用追踪网来束缚敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "bounty_hunter_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_cloak" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_cloak" }
]
```

---

### 26.6 赏金猎人项链

```gdscript
item_id = "acc_bounty_hunter_necklace"
display_name = "赏金猎人项链"
description = "一条由通缉犯牙齿与追踪骨串联而成的项链，项链中央镶嵌着一颗「赏金之眼」——一颗能够看穿 disguise 和 illusion 的魔法宝石。项链佩戴时，宝石会发出微弱的绿光，当附近有通缉犯时会变成红色。\n\n杰西在制作这条项链时，从自己抓捕的第一个通缉犯身上取下了他的牙齿。他说："这颗牙齿不是被取下的，是'被继承的'——它提醒我为什么开始这条路。"\n\n这条项链的特殊效果是：穿戴者可以「看穿」disguise 和 illusion（如同 true seeing 的 miniature 版，范围 30 尺）。且可以通过项链「感知」到 30 尺内所有被通缉或有悬赏的生物（项链会微微发热）。杰西说："赏金之眼不是只能看，它也可以感知——感知所有猎物的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "bounty_hunter_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_bounty_hunter_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_bounty_hunter_necklace" }
]
```

---

### 26.7 赏金猎人戒指（左）

```gdscript
item_id = "acc_bounty_hunter_ring_1"
display_name = "赏金猎人戒指·锁链"
description = "一枚由通缉犯镣铐与追踪金属锻造而成的粗糙戒指，戒指表面有类似镣铐的纹理——粗糙、冰冷、充满束缚感。戒指佩戴时，会根据附近通缉犯的距离变化温度——越近越热。\n\n杰西在锻造这枚戒指时，将自己使用的第一个镣铐熔入了金属。他说："这个镣铐不是普通的工具，它是'正义的象征'——让罪犯永远被束缚。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 100 尺内所有通缉犯的位置（锁链雷达）。且可以通过戒指「束缚」——每日一次，触碰一个目标，目标须通过 DC14 敏捷豁免，失败则 restrained 1 回合（微型锁链束缚）。杰西说："锁链不是只能用来绑，它也可以用来感知——感知所有罪犯的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "bounty_hunter_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "grapple_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_bounty_hunter_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_bounty_hunter_ring_1" }
]
```

---

### 26.8 赏金猎人戒指（右）

```gdscript
item_id = "acc_bounty_hunter_ring_2"
display_name = "赏金猎人戒指·赏金"
description = "一枚由赏金金币与追踪印记锻造而成的华丽戒指，戒指表面刻有「赏金」二字——以通缉犯的血写就。戒指佩戴时，会根据附近悬赏金额的大小变化光芒的强度——金额越大越明亮。\n\n杰西在锻造这枚戒指时，将自己收到的第一笔赏金金币封入了戒指。他说："这枚金币不是普通的报酬，它是'正义的回报'——让正义永远有回报。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 30 尺内所有悬赏目标的悬赏金额（赏金雷达）。且可以通过戒指「提取」情报——每日一次，向当地的赏金猎人公会传递一条信息（获得关于一个悬赏目标的情报）。杰西说："赏金不是只能拿，它也可以用来获取——获取所有需要的情报。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "bounty_hunter_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_bounty_hunter_ring_2" }
]
```

---

### 26.9 赏金猎人特殊饰品

```gdscript
item_id = "acc_bounty_hunter_trinket"
display_name = "赏金猎人手铐"
description = "一对由通缉犯镣铐改造而成的小型手铐，可以挂在腰带上。手铐表面刻有无数通缉犯的名字——每一个名字都代表一次成功的抓捕。手铐佩戴时，会随穿戴者的动作微微作响——那不是噪音，是警告，告诉所有人：赏金猎人来了。\n\n杰西在制作这个手铐时，将自己抓捕的第一个通缉犯的镣铐改造成了装饰品。他说："这个手铐不是普通的工具，它是'荣誉的象征'——让所有人知道我的成就。"\n\n这个手铐的特殊效果是：穿戴者可以「使用」手铐——每日一次，快速铐住一个 5 尺内的目标（目标须通过 DC16 敏捷豁免，失败则 restrained 1 分钟）。且可以通过手铐「威慑」——威吓检定 +3（手铐是威慑的象征）。杰西说："手铐不是只能用来铐，它也可以用来威慑——威慑所有潜在的罪犯。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "bounty_hunter_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_trinket" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_bounty_hunter_trinket" }
]
```

---

### 26.10 赏金猎人徽章

```gdscript
item_id = "acc_bounty_hunter_badge"
display_name = "赏金猎人徽章"
description = "一枚由赏金猎人公会的印记与追踪金属锻造而成的徽章，徽章上刻有「正义有价」四个字——以通缉犯的血写就。徽章佩戴时，会根据附近通缉犯的数量变化光芒的强度——越多越明亮。\n\n杰西在锻造这枚徽章时，将自己的「猎人之心」封入了印记。他说："这枚徽章不只是徽章，它是'身份的象征'——让所有人知道，赏金猎人来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有赏金猎人公会成员的存在（徽章会微微发热）。且可以通过徽章「求助」——每日一次，向最近的赏金猎人公会成员发送求救信号（1 里内有效）。杰西说："徽章不是只能佩戴，它也可以用来求助——在危险时召唤盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "bounty_hunter_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_badge" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bounty_hunter_badge" }
]
```

---

## 套装二十七：瘟疫医生（Plague Doctor）饰品

---

### 27.5 瘟疫医生斗篷

```gdscript
item_id = "acc_plague_doctor_cloak"
display_name = "瘟疫医生斗篷"
description = "一件由多层蜡布与消毒丝编织而成的斗篷，斗篷表面涂有防水蜡质，可以阻挡任何液体的渗透。斗篷的长度几乎拖到地面——不是为了美观，而是为了防止地面上的病原体接触到身体。斗篷的内部有特殊的「消毒层」——可以自动杀死接触到的细菌和病毒。\n\n阿斯克勒庇俄斯在编织这件斗篷时，将自己治疗过的所有病人的蜡布都缝入了斗篷。他说："这件斗篷不只是斗篷，它是'防护罩'——让我可以在最危险的环境中安全工作。"\n\n这件斗篷的特殊效果是：穿戴者免疫所有 airborne diseases（空气传播疾病）。且可以通过斗篷「释放」消毒雾——每日一次，释放一道消毒雾（15 尺锥形，所有生物恢复 2D8 HP 并解除 diseases/poisons）。阿斯克勒庇俄斯说："斗篷不是只能防护，它也可以治愈——用消毒雾来治愈盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "plague_doctor_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_plague_doctor_cloak" },
    { attribute_id = "resistance_poison", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_plague_doctor_cloak" }
]
```

---

### 27.6 瘟疫医生项链

```gdscript
item_id = "acc_plague_doctor_necklace"
display_name = "瘟疫医生项链"
description = "一条由消毒草药与防护骨串联而成的项链，项链中央镶嵌着一颗「净化之心」——一颗能够净化任何毒素的魔法宝石。项链佩戴时，宝石会发出微弱的绿光，光芒的强度代表周围毒素的浓度。\n\n阿斯克勒庇俄斯在制作这条项链时，将自己收集的所有草药都编入了项链。他说："这条项链不只是饰品，它是'药房'——让我可以随时获取任何草药。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有毒素和病原体的位置（净化雷达）。且可以通过项链「净化」——每日三次，净化一个目标身上的毒素或疾病（自动成功）。阿斯克勒庇俄斯说："净化之心不是只能感知，它也可以净化——净化所有毒素。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "plague_doctor_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_plague_doctor_necklace" }
]
```

---

### 27.7 瘟疫医生戒指（左）

```gdscript
item_id = "acc_plague_doctor_ring_1"
display_name = "瘟疫医生戒指·解剖"
description = "一枚由手术刀碎片与消毒金属锻造而成的锋利戒指，戒指表面有类似手术刀的纹理——锋利、精确、充满科学感。戒指佩戴时，会根据周围疾病的浓度变化温度——浓度越高越热。\n\n阿斯克勒庇俄斯在锻造这枚戒指时，将自己使用的第一把手术刀熔入了金属。他说："这把刀不是普通的工具，它是'知识的象征'——让科学成为我的武器。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指进行「精确切割」—— bonus action，用戒指划向一个目标（1D6 slashing，每日三次）。且在进行医学检查时，检定 +3（手术刀精确度）。阿斯克勒庇俄斯说："手术刀不是只能用来切割，它也可以用来感知——感知所有疾病的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "plague_doctor_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_plague_doctor_ring_1" }
]
```

---

### 27.8 瘟疫医生戒指（右）

```gdscript
item_id = "acc_plague_doctor_ring_2"
display_name = "瘟疫医生戒指·药剂"
description = "一枚由药瓶碎片与消毒玻璃锻造而成的透明戒指，戒指内部有微型药瓶——可以储存三滴不同的药剂。戒指佩戴时，药瓶内的药剂会微微发光——红色代表治愈，绿色代表解毒，蓝色代表麻醉。\n\n阿斯克勒庇俄斯在锻造这枚戒指时，将自己配制的三种最重要的药剂封入了戒指。他说："这些药剂不是普通的药，它们是'生命的保障'——让我在任何时候都有药可用。"\n\n这枚戒指的特殊效果是：穿戴者可以「释放」药剂——每日一次，选择一种药剂释放（治愈：恢复 2D8 HP；解毒：解除所有 poisons；麻醉：目标 asleep 1 分钟，须通过 DC14 体质豁免）。阿斯克勒庇俄斯说："药剂不是只能储存，它也可以用来释放——释放治愈，释放解毒，释放麻醉。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "plague_doctor_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_plague_doctor_ring_2" }
]
```

---

### 27.9 瘟疫医生特殊饰品

```gdscript
item_id = "acc_plague_doctor_trinket"
display_name = "瘟疫医生烧瓶"
description = "一个由耐热玻璃与消毒金属制成的小型烧瓶，烧瓶内永远有一种神秘的液体——它会根据周围环境的病原体自动变化颜色：红色代表细菌，绿色代表病毒，蓝色代表毒素。烧瓶佩戴时，会随穿戴者的动作微微晃动，液体在瓶中旋转。\n\n阿斯克勒庇俄斯在制作这个烧瓶时，将自己的「诊断之眼」封入了玻璃。他说："这个烧瓶不是普通的容器，它是'诊断的工具'——让我可以随时诊断任何疾病。"\n\n这个烧瓶的特殊效果是：穿戴者可以「诊断」—— bonus action，观察烧瓶的颜色变化，自动识别周围 15 尺内的所有病原体类型。且可以通过烧瓶「制造」药剂——每日一次，根据识别的病原体自动制造出对应的解药（解除该病原体效果）。阿斯克勒庇俄斯说："烧瓶不是只能诊断，它也可以制造——制造出所有需要的解药。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "plague_doctor_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_trinket" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_trinket" }
]
```

---

### 27.10 瘟疫医生徽章

```gdscript
item_id = "acc_plague_doctor_badge"
display_name = "瘟疫医生徽章"
description = "一枚由乌鸦面具的碎片与消毒金属锻造而成的徽章，徽章上刻有乌鸦面具的标志——一个长长的鸟嘴。徽章佩戴时，会根据周围疾病的浓度变化光芒的强度——浓度越高越明亮。\n\n阿斯克勒庇俄斯在锻造这枚徽章时，将自己的「医者之心」封入了面具碎片。他说："这枚徽章不只是徽章，它是'希望的象征'——让患者知道，医生来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有患者的存在（徽章会微微发热）。且可以通过徽章「安抚」——每日一次，安抚 15 尺内所有患者（解除 frightened 和 pain，恢复 1D10 HP）。阿斯克勒庇俄斯说："徽章不是只能佩戴，它也可以用来治愈——治愈所有患者的心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "plague_doctor_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_plague_doctor_badge" }
]
```

---

## 套装二十八：月影舞者（Moonshadow Dancer）饰品

---

### 28.5 月影舞者斗篷

```gdscript
item_id = "acc_moonshadow_dancer_cloak"
display_name = "月影舞者斗篷"
description = "一件由月光丝绸与银月之露编织而成的华丽斗篷，斗篷表面不断有月光般的纹理在流动。斗篷非常轻薄——薄到几乎不存在，但这正是优势，因为舞者的斗篷不是为了防御，而是为了美感。斗篷的边缘有特殊的「月光流苏」——在舞动时会折射出彩虹般的光芒。\n\n莎乐美在编织这件斗篷时，将自己所有舞蹈中的「最美瞬间」都编入了丝线。她说："这件斗篷不只是斗篷，它是'舞蹈的延续'——让我即使不在舞台上也在跳舞。"\n\n这件斗篷的特殊效果是：在月光下，斗篷会「发光」——15 尺半径 dim light（不暴露位置，因为看起来像自然月光）。且可以通过斗篷「释放」月光——每日一次，释放一道月光（15 尺锥形 2D8 radiant，目标须通过 DC14 体质豁免，失败则 charmed 1 回合）。莎乐美说："斗篷不是只能装饰，它也可以攻击——用月光来迷惑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "moonshadow_dancer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_moonshadow_dancer_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_cloak" }
]
```

---

### 28.6 月影舞者项链

```gdscript
item_id = "acc_moonshadow_dancer_necklace"
display_name = "月影舞者项链"
description = "一条由月光石与银月之露串联而成的华丽项链，项链中央镶嵌着一颗「月影之心」——一颗能够储存月光并在黑暗中释放的魔法宝石。项链佩戴时，宝石会随穿戴者的心跳微微脉动——脉动的节奏与月光同步。\n\n莎乐美在制作这条项链时，将自己对月光的所有感情都封入了宝石。她说："这颗宝石不是普通的石头，它是'月光的情人'——让我与月光永远相连。"\n\n这条项链的特殊效果是：在月光下，项链会「共鸣」——所有与月光相关的技能效果 +1。且可以通过项链「召唤」月光——每日一次，在 15 尺半径内创造出人工月光（持续 1 分钟）。莎乐美说："月光不是只能等待的，它也可以被召唤——只要有正确的工具。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "moonshadow_dancer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_moonshadow_dancer_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_moonshadow_dancer_necklace" }
]
```

---

### 28.7 月影舞者戒指（左）

```gdscript
item_id = "acc_moonshadow_dancer_ring_1"
display_name = "月影舞者戒指·月华"
description = "一枚由月光银与银月之露锻造而成的精致戒指，戒指表面有类似月光的纹理——在黑暗中会发出微弱的银光。戒指佩戴时，会根据月亮的相位变化光芒的强度——满月时最亮，新月时最暗。\n\n莎乐美在锻造这枚戒指时，将一滴银月之露封入了金属。她说："这滴露水不是普通的水，它是'月光的精华'——让我可以随时感受到月光。"\n\n这枚戒指的特殊效果是：在月光下，戒指会「共鸣」——攻击检定 +1。且可以通过戒指「释放」月光——每日一次，释放一道月光（30 尺射程 1D10 radiant，目标须通过 DC14 体质豁免，失败则 blinded 1 回合）。莎乐美说："月光不是只能感受，它也可以释放——释放月光的美丽和力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "moonshadow_dancer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_moonshadow_dancer_ring_1" }
]
```

---

### 28.8 月影舞者戒指（右）

```gdscript
item_id = "acc_moonshadow_dancer_ring_2"
display_name = "月影舞者戒指·舞步"
description = "一枚由舞蹈鞋的皮革与银月之露锻造而成的轻盈戒指，戒指表面有类似舞步的纹理——轻盈、优雅、充满节奏感。戒指佩戴时，会随穿戴者的动作微微振动——振动与心跳同步，形成一种独特的节奏。\n\n莎乐美在锻造这枚戒指时，将自己第一双舞蹈鞋的皮革封入了金属。她说："这块皮革不是普通的材料，它是'舞蹈的记忆'——记录了我所有的舞步。"\n\n这枚戒指的特殊效果是：穿戴者的移动力 +5（舞步轻盈）。且可以通过戒指「加速」——每日一次，移动力 +15（持续 1 回合）。莎乐美说："舞步不是只能用来跳舞，它也可以用来移动——用舞步来移动，比跑步更快。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "moonshadow_dancer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_moonshadow_dancer_ring_2" }
]
```

---

### 28.9 月影舞者特殊饰品

```gdscript
item_id = "acc_moonshadow_dancer_trinket"
display_name = "月影舞者音乐盒"
description = "一个由月光银与银月之露制成的小型音乐盒，音乐盒可以发出最优美的旋律——旋律会随着月光的变化而变化：满月时欢快，新月时忧伤，弦月时优雅。音乐盒佩戴时，会随穿戴者的心跳微微脉动——脉动与旋律同步。\n\n莎乐美在制作这个音乐盒时，将自己所有舞蹈中的「最美旋律」都编入了齿轮。她说："这个音乐盒不是普通的玩具，它是'舞蹈的灵魂'——让我即使不在舞台上也在跳舞。"\n\n这个音乐盒的特殊效果是：穿戴者可以用音乐盒「演奏」——每日一次，演奏一段旋律（15 尺半径，所有盟友攻击检定 +2，所有敌人攻击检定 -2，持续 1 分钟）。且可以通过音乐盒「治愈」——每日一次，播放治愈旋律（15 尺内所有盟友恢复 2D8 HP）。莎乐美说："音乐不是只能用来欣赏，它也可以用来治愈——用旋律来治愈心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "moonshadow_dancer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_trinket" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_trinket" }
]
```

---

### 28.10 月影舞者徽章

```gdscript
item_id = "acc_moonshadow_dancer_badge"
display_name = "月影舞者徽章"
description = "一枚由月光银与银月之露锻造而成的精致徽章，徽章上刻有月影舞者的标志——一轮弯月下跳舞的影子。徽章佩戴时，会根据周围观众的数量变化光芒的强度——越多越明亮。\n\n莎乐美在锻造这枚徽章时，将自己的「舞者之魂」封入了金属。她说："这枚徽章不只是徽章，它是'舞台的象征'——让所有人知道，月影舞者来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有观众的存在（徽章会微微发热）。且可以通过徽章「吸引」——每日一次，强制 15 尺内一个目标注视穿戴者（目标须通过 DC14 魅力豁免，失败则 charmed 1 回合）。莎乐美说："徽章不是只能佩戴，它也可以用来吸引——吸引所有观众的目光。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "moonshadow_dancer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_moonshadow_dancer_badge" }
]
```

---

## 套装二十九：血骑士（Blood Knight）饰品

---

### 29.5 血骑士斗篷

```gdscript
item_id = "acc_blood_knight_cloak"
display_name = "血骑士斗篷"
description = "一件由凝固的鲜血与契约丝编织而成的恐怖斗篷，斗篷表面不断有血液在流动——不是穿戴者的血，是「契约之血」，由吸血鬼提供的永恒血液。斗篷的边缘有特殊的「血滴流苏」——每一滴血都会在落地前蒸发，不会留下任何痕迹。\n\n德古拉在编织这件斗篷时，将自己的第一滴契约之血都缝入了斗篷。他说："这件斗篷不只是斗篷，它是'契约的见证'——每一滴血都是一个承诺。"\n\n这件斗篷的特殊效果是：穿戴者可以将斗篷的血液「释放」——每日一次，释放所有血液形成一个血雾（15 尺半径，困难地形，所有生物在区域内攻击检定 -2，因为血雾遮蔽视线，持续 1 分钟）。且斗篷会「吸血」——任何 melee 攻击穿戴者的生物受到 1D6 necrotic（血液反噬）。德古拉说："斗篷不是只能装饰，它也可以攻击——用血液来反噬敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "blood_knight_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_blood_knight_cloak" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_blood_knight_cloak" }
]
```

---

### 29.6 血骑士项链

```gdscript
item_id = "acc_blood_knight_necklace"
display_name = "血骑士项链"
description = "一条由吸血鬼的獠牙与契约血串联而成的项链，项链中央镶嵌着一颗「血之心」——一颗由吸血鬼心脏碎片制成的宝石。项链佩戴时，宝石会随穿戴者的心跳微微脉动——脉动与吸血鬼的心跳同步。\n\n德古拉在制作这条项链时，从自己的吸血鬼主人身上取下了一颗獠牙。他说："这颗獠牙不是被取下的，是'被赐予的'——我的主人选择了我，让我成为它的代理人。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有流血生物的位置（血之心微微发热）。且可以通过项链「吸血」——每日一次，触碰一个流血的生物，吸取 2D8 HP（恢复自身等量 HP）。德古拉说："血之心不是只能感知，它也可以吸血——吸取所有可用的血液。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "blood_knight_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_knight_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_blood_knight_necklace" }
]
```

---

### 29.7 血骑士戒指（左）

```gdscript
item_id = "acc_blood_knight_ring_1"
display_name = "血骑士戒指·獠牙"
description = "一枚由吸血鬼的獠牙与契约金属锻造而成的锋利戒指，戒指表面有类似獠牙的纹理——尖锐、弯曲、充满威胁。戒指佩戴时，会根据附近血液的数量变化温度——越多越热。\n\n德古拉在锻造这枚戒指时，将一只吸血鬼的两颗獠牙都熔入了金属。他说："这两颗獠牙不是被熔化的，是'被融合的'——它们的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「吸血」—— bonus action，触碰一个流血的生物，吸取 1D8 HP（恢复自身等量 HP，每日三次）。且在吸血后，攻击检定 +1（血液强化，持续 1 回合）。德古拉说："獠牙不是只能用来咬，它也可以用来刺——用戒指上的獠牙刺入敌人的皮肤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "blood_knight_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_knight_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_blood_knight_ring_1" }
]
```

---

### 29.8 血骑士戒指（右）

```gdscript
item_id = "acc_blood_knight_ring_2"
display_name = "血骑士戒指·契约"
description = "一枚由契约符文与契约金属锻造而成的神秘戒指，戒指表面刻有无数契约符文——每一个符文都代表一个已签订的契约。戒指佩戴时，符文会微微发光，光芒的颜色代表契约的类型——红色代表鲜血契约，黑色代表灵魂契约，白色代表保护契约。\n\n德古拉在锻造这枚戒指时，将自己的第一个契约都编入了符文。他说："这些符文不是普通的文字，它们是'承诺的见证'——每一个符文都是一个不可违背的誓言。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 30 尺内所有存在的契约（契约雷达）。且可以通过戒指「签订」临时契约——每日一次，与一个生物签订临时契约（例如"我给你 5 HP，你给我 +2 攻击"，持续 1 小时）。德古拉说："契约不是只能感知，它也可以签订——签订新的契约，获得新的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "blood_knight_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_blood_knight_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_blood_knight_ring_2" }
]
```

---

### 29.9 血骑士特殊饰品

```gdscript
item_id = "acc_blood_knight_trinket"
display_name = "血骑士血瓶"
description = "一个由凝固的鲜血与契约玻璃制成的小型血瓶，血瓶内储存着「契约之血」——一种永远不会凝固、永远不会变质的神秘血液。血瓶佩戴时，会随穿戴者的心跳微微脉动——血液在脉动，仿佛有生命一般。\n\n德古拉在制作这个血瓶时，将自己的第一滴契约之血都封入了玻璃。他说："这个血瓶不是普通的容器，它是'生命的源泉'——让我在任何时候都有血可用。"\n\n这个血瓶的特殊效果是：穿戴者可以「饮用」契约之血——每日一次，饮用后恢复 3D10 HP（血液治愈）。且可以通过血瓶「分享」——让 15 尺内的一个盟友也饮用一口（同样效果）。德古拉说："血不是只能用来吸血，它也可以用来分享——分享生命，分享力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "blood_knight_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_knight_trinket" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_blood_knight_trinket" }
]
```

---

### 29.10 血骑士徽章

```gdscript
item_id = "acc_blood_knight_badge"
display_name = "血骑士徽章"
description = "一枚由吸血鬼的印记与契约金属锻造而成的徽章，徽章上刻有血骑士的标志——一滴鲜血下交叉的剑。徽章佩戴时，会根据附近血液的数量变化光芒的强度——越多越明亮。\n\n德古拉在锻造这枚徽章时，将自己的「血骑士之心」封入了印记。他说："这枚徽章不只是徽章，它是'身份的象征'——让所有人知道，血骑士来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有吸血鬼的存在（徽章会微微发热）。且可以通过徽章「召唤」蝙蝠——每日一次，召唤一群吸血蝙蝠（15 尺半径，所有生物受到 2D6 necrotic，持续 1 回合）。德古拉说："徽章不是只能佩戴，它也可以用来召唤——召唤黑暗中的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "blood_knight_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_blood_knight_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_blood_knight_badge" }
]
```

---

## 套装三十：幽灵骑士（Phantom Rider）饰品

---

### 30.5 幽灵骑士斗篷

```gdscript
item_id = "acc_phantom_rider_cloak"
display_name = "幽灵骑士斗篷"
description = "一件由幽灵之尘与记忆丝编织而成的奇异斗篷，斗篷呈现出半透明的黑色——你可以看到后面的东西，但一切都变得扭曲。斗篷没有固定的形状——它会随风飘动，仿佛穿戴者本身就是一团幽灵。\n\n卡斯帕在编织这件斗篷时，将自己的一部分灵魂从死亡之地拉了回来，封入了斗篷。他说："这件斗篷不只是斗篷，它是'灵魂的碎片'——让我可以穿着自己的灵魂。"\n\n这件斗篷的特殊效果是：穿戴者可以「融入」阴影中——在阴影中完全隐形（如同 greater invisibility，但只在阴影中有效）。且可以通过斗篷「释放」幽灵——每日一次，释放一个幽灵分身（持续 1 分钟，分身可以进行一次攻击 1D10 necrotic）。卡斯帕说："斗篷不是只能隐藏，它也可以攻击——用幽灵来攻击敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "phantom_rider_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_phantom_rider_cloak" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_phantom_rider_cloak" }
]
```

---

### 30.6 幽灵骑士项链

```gdscript
item_id = "acc_phantom_rider_necklace"
display_name = "幽灵骑士项链"
description = "一条由幽灵之骨与记忆丝串联而成的奇异项链，项链中央镶嵌着一颗「幽灵之眼」——一颗能够看到死亡之地的魔法宝石。项链佩戴时，宝石会发出微弱的紫光，让穿戴者可以「看见」幽灵。\n\n卡斯帕在制作这条项链时，从死亡之地带回了一块幽灵之骨。他说："这块骨头不是被带回的，是'被借用的'——死亡之地允许我看一眼，但不允许我带走。"\n\n这条项链的特殊效果是：穿戴者可以「看见」所有 undead 和幽灵（如同 see invisibility，但对 undead 有效，范围 60 尺）。且可以通过项链「沟通」——每日一次，与 30 尺内的一个 undead 进行简短沟通（了解它的来历和目的）。卡斯帕说："幽灵之眼不是只能看，它也可以沟通——与死者沟通，了解他们的故事。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "phantom_rider_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_phantom_rider_necklace" }
]
```

---

### 30.7 幽灵骑士戒指（左）

```gdscript
item_id = "acc_phantom_rider_ring_1"
display_name = "幽灵骑士戒指·幽灵"
description = "一枚由幽灵之尘与记忆金属锻造而成的奇异戒指，戒指呈现出半透明的黑色。戒指佩戴时，会随穿戴者的动作微微闪烁——闪烁的频率与幽灵的存在同步。\n\n卡斯帕在锻造这枚戒指时，将自己的「幽灵之触」封入了金属。他说："这枚戒指不只是戒指，它是'幽灵的延伸'——让我可以用手触碰幽灵。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「触碰」幽灵—— bonus action，触碰一个 undead 或 ethereal 生物，造成 1D10 necrotic（每日三次）。且在触碰后，可以「读取」它的记忆（自动了解它的最近记忆）。卡斯帕说："幽灵之触不是只能攻击，它也可以读取——读取死者的记忆。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "phantom_rider_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_phantom_rider_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_phantom_rider_ring_1" }
]
```

---

### 30.8 幽灵骑士戒指（右）

```gdscript
item_id = "acc_phantom_rider_ring_2"
display_name = "幽灵骑士戒指·虚空"
description = "一枚由虚空碎片与记忆金属锻造而成的奇异戒指，戒指呈现出半透明的黑色，内部不断有虚空漩涡在旋转。戒指佩戴时，会随穿戴者的心跳微微脉动——脉动与虚空的存在同步。\n\n卡斯帕在锻造这枚戒指时，将一块虚空碎片封入了金属。他说："这块碎片不是被封入的，是'被融合的'——虚空的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以「虚空步」——每日一次，踏入虚空并瞬间移动至 30 尺内任何位置（如同 misty step，但不会留下法术痕迹）。且可以通过戒指「感知」虚空——30 尺内任何虚空裂缝或传送门会被自动识别。卡斯帕说："虚空不是只能用来移动，它也可以用来感知——感知所有虚空的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "phantom_rider_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_phantom_rider_ring_2" }
]
```

---

### 30.9 幽灵骑士特殊饰品

```gdscript
item_id = "acc_phantom_rider_trinket"
display_name = "幽灵骑士灯笼"
description = "一个由幽灵之火与记忆玻璃制成的小型灯笼，灯笼内燃烧着「幽灵之火」——一种永远不会熄灭、永远不会产生热量的奇异火焰。灯笼佩戴时，会随穿戴者的移动微微摇晃，火光在黑暗中摇曳。\n\n卡斯帕在制作这个灯笼时，从死亡之地带回了一团幽灵之火。他说："这团火不是被带回的，是'被借用的'——死亡之地允许我用一下，但不允许我带走。"\n\n这个灯笼的特殊效果是：穿戴者可以用灯笼「照亮」幽灵——15 尺半径内所有隐形或 ethereal 生物显形（如同 faerie fire）。且可以通过灯笼「引导」幽灵——每日一次，控制一个 15 尺内的 undead（须通过 DC16 智慧豁免，失败则被控制 1 分钟）。卡斯帕说："灯笼不是只能照亮，它也可以引导——引导幽灵，控制 undead。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "phantom_rider_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_trinket" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_trinket" }
]
```

---

### 30.10 幽灵骑士徽章

```gdscript
item_id = "acc_phantom_rider_badge"
display_name = "幽灵骑士徽章"
description = "一枚由幽灵之骨与记忆金属锻造而成的徽章，徽章上刻有幽灵骑士的标志——一匹骑着幽灵马的骑士。徽章佩戴时，会根据附近 undead 的数量变化光芒的强度——越多越明亮。\n\n卡斯帕在锻造这枚徽章时，将自己的「幽灵骑士之魂」封入了骨头。他说："这枚徽章不只是徽章，它是'身份的象征'——让所有人知道，幽灵骑士来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有 undead 的存在（徽章会微微发热）。且可以通过徽章「召唤」幽灵马——每日一次，召唤一匹幽灵马（持续 10 分钟，飞行速度 60 尺，可以穿越实体障碍物）。卡斯帕说："徽章不是只能佩戴，它也可以用来召唤——召唤死亡之地的坐骑。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "phantom_rider_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phantom_rider_badge" }
]
```

---

*中甲套装 21–30 饰品部分完结 · 共 60 件饰品装备*

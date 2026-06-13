# 传奇装备套装饰品设计文档（套装 71–80）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装七十一：武士之魂（Samurai Spirit）饰品

> *"武士之魂不是一件盔甲——它是一种荣誉，一种责任，一种永不弯曲的脊梁。"*

---

### 71.5 武士之魂阵羽织披风（Samurai Spirit Jinbaori Cloak）

```gdscript
item_id = "acc_samurai_spirit_cloak"
display_name = "武士之魂阵羽织披风"
description = "一件由武士阵羽织与魔法丝编织而成的披风，披风表面绣着家纹——一只展翅的雄鹰，代表着武士家族的自由与威严。这件阵羽织不是给普通士兵穿的——它是给将军穿的。当穿戴者行走时，披风会自动展开，如同雄鹰展翅。\n\n这件阵羽织是武士之魂在「鹤冈城」中，从自己的最后一场战役中保留下来的。他说："这件披风承载了我的荣耀和耻辱。每一次战斗，我都在它面前发誓——要么胜利，要么死亡。"\n\n披风的效果是：魅力检定 +2（武士威严），且免疫 frightened（武士不屈）。当穿戴者 HP 低于 25% 时，攻击检定 +3（死战）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "samurai_spirit_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_samurai_spirit_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_samurai_spirit_cloak" }
]
```

---

### 71.6 武士之魂家传御守项链（Samurai Spirit Omamori Necklace）

```gdscript
item_id = "acc_samurai_spirit_necklace"
display_name = "武士之魂家传御守项链"
description = "一条由武士家的传家丝与神社御守编织而成的项链，吊坠是一个古老御守——里面装着一位祖先的遗书。御守表面已经泛黄，边缘磨损，但上面的字迹依然清晰：「子孙万代，武运长久」。据说这个御守已经传承了三百七十二年，每一位佩戴它的武士都在战场上活了下来。\n\n这条项链是武士之魂在「鹤冈神社」中，从祖母手中接过的最后一件遗物。他说："这个御守承载了我家族三百年的祈祷。它不会让我无敌，但它会让我在最黑暗的时刻看到光明。"\n\n项链的效果是：每日一次，当受到致命攻击时，自动触发「祖先庇佑」——免疫该次伤害，且恢复 2D8 HP（御守祝福）。且 HP 低于 50% 时， AC +1（祖先守护）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "samurai_spirit_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_samurai_spirit_necklace" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_samurai_spirit_necklace" }
]
```

---

### 71.7 武士之魂切先之戒（Samurai Spirit Kissaki Ring）

```gdscript
item_id = "acc_samurai_spirit_ring_1"
display_name = "武士之魂切先之戒"
description = "一枚由切先钢与武士金铸造的戒指，戒指表面刻有微型刀刃图案——那是名刀「村雨」的刀尖形状。当佩戴者集中精神时，戒指会释放出一股切割之力，使佩戴者的下一次攻击如同名刀出鞘。这种切割不是物理的——它是精神的，是意志的延伸。\n\n这枚戒指是武士之魂在「刀匠铺」中，从村雨的碎片上铸造的。他说："这枚戒指让我能够借用村雨的锋利。但记住——刀是工具，不是生命。真正的锋利在心中。"\n\n戒指的效果是：每日一次，下一次攻击检定自动命中且暴击（名刀一斩），伤害骰翻倍。如果该攻击击杀目标，可以 bonus action 再进行一次攻击（连斩）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "samurai_spirit_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_samurai_spirit_ring_1" }
]
```

---

### 71.8 武士之魂忠义之戒（Samurai Spirit Loyalty Ring）

```gdscript
item_id = "acc_samurai_spirit_ring_2"
display_name = "武士之魂忠义之戒"
description = "一枚由忠义石与武士银铸造的戒指，戒指表面刻有「忠」「义」二字。当佩戴者集中精神时，戒指会释放出一股忠义之力，增强佩戴者对盟友的保护。这种力量不是魔法的——它是精神的，是武士道的核心。据说这枚戒指来自一位为主君切腹的武士，他在死前将自己的忠义封印在了其中。\n\n这枚戒指是武士之魂在「忠义碑」前，从碑下取出的。他说："这枚戒指承载了我的忠义。它让我能够为我的主君、我的同伴、我的信念而战，即使代价是生命。"\n\n戒指的效果是：5 尺内友方受到伤害时，可以 reaction 转移 50% 伤害给自己（忠义守护）。且自身 HP 低于 25% 时，所有攻击附加 1D8 radiant（忠义之光）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "samurai_spirit_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_samurai_spirit_ring_2" }
]
```

---

### 71.9 武士之魂家纹印笼（Samurai Spirit Inro）

```gdscript
item_id = "acc_samurai_spirit_trinket"
display_name = "武士之魂家纹印笼"
description = "一个由黑漆与家纹金制成的微型印笼（inro），印笼表面绘着家纹——雄鹰展翅。印笼内部有微型隔间，每个隔间装着不同的秘药：止血、解毒、强心、提神。这些药物不是普通的药剂——它们是武士家族世代相传的秘药，每一滴都凝聚了三百年的智慧。\n\n这个印笼是武士之魂在「鹤冈城」的药房中，从自己的珍藏中取出的。他说："这个印笼承载了我的生存之道。武士不是不死的——武士是知道如何活下去的。"\n\n印笼的效果是：每日三次，可以 bonus action 服用一种秘药：\n- 止血药：恢复 2D8 HP\n- 解毒药：移除一个 poison/paralysis 效果\n- 强心药：下一回合攻击检定 +2\n- 提神药：免疫 sleep/fatigue 2 回合"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "samurai_spirit_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_samurai_spirit_trinket" }
]
```

---

### 71.10 武士之魂徽章（Samurai Spirit Badge）

```gdscript
item_id = "acc_samurai_spirit_badge"
display_name = "武士之魂徽章"
description = "一枚由武士金与黑石铸造的徽章，徽章上刻着一把刀和一片樱花——武士之魂的标志。徽章背面刻着一行小字：「武士道者，发现死亡之处，即为忠义之路。」这枚徽章是武士公会的信物，拥有它意味着你已理解武士道的真谛。\n\n徽章的效果是：10 尺内所有友方免疫 frightened（武士威严）。且每日一次，可以释放「武士之魂」——10 尺内所有友方攻击检定 +2（武士鼓舞），且击杀敌人时恢复 1D8 HP（武士荣耀），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "samurai_spirit_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_samurai_spirit_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_samurai_spirit_badge" }
]
```

---

## 套装七十二：忍者之影（Ninja Shadow）饰品

> *"忍者之影不是一个人——他是一阵风，一片叶，一滴落在敌人颈后的冷汗。"*

---

### 72.5 忍者之影胧夜披风（Ninja Shadow Oboro Cloak）

```gdscript
item_id = "acc_ninja_shadow_cloak"
display_name = "忍者之影胧夜披风"
description = "一件由胧夜丝与暗影布编织而成的披风，披风表面如同夜色一般深邃，没有任何反光。当穿戴者静止不动时，披风会自动与周围环境融合，使穿戴者几乎不可见。据说这件披风是用一百个夜晚的影子编织的，每一根丝线都来自不同的月黑之夜。\n\n这件披风是忍者之影在「伊贺之里」中，从自己的师傅那里继承的。他说："这件披风让我成为了夜的一部分。我可以隐藏在任何一个角落，消失在任何一个瞬间。"\n\n披风的效果是：隐匿检定 +4（胧夜之隐），且在黑暗中完全隐形（如同 greater invisibility）。当从隐匿状态发动攻击时，攻击检定有优势，且伤害 +2D6（暗杀）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "ninja_shadow_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_ninja_shadow_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ninja_shadow_cloak" }
]
```

---

### 72.6 忍者之影苦无项链（Ninja Shadow Kunai Necklace）

```gdscript
item_id = "acc_ninja_shadow_necklace"
display_name = "忍者之影苦无项链"
description = "一条由忍者丝与苦无金编织而成的项链，吊坠是一把微型苦无——这把苦无不是装饰品，它是真正的武器。苦无的刀刃虽然小，但极其锋利，足以割断喉咙。据说这把苦无属于一位传奇忍者，他用这把苦无刺杀了三位大名。\n\n这条项链是忍者之影在「甲贺之里」中，从师傅的遗物中取出的。他说："这把苦无承载了我的暗杀之道。它不是用来炫耀的——它是用来完成任务的。"\n\n项链的效果是：每日三次，可以 bonus action 投掷苦无——20/60 射程，+6 攻击，2D6+3 piercing + 1D6 poison（涂毒苦无）。如果目标 HP 低于 25%，自动暴击（暗杀者之刃）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "ninja_shadow_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ninja_shadow_necklace" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ninja_shadow_necklace" }
]
```

---

### 72.7 忍者之影手里剑之戒（Ninja Shadow Shuriken Ring）

```gdscript
item_id = "acc_ninja_shadow_ring_1"
display_name = "忍者之影手里剑之戒"
description = "一枚由手里剑钢与忍者金铸造的戒指，戒指表面刻有微型手里剑图案。当佩戴者集中精神时，戒指会释放出一股投掷之力，使佩戴者的投掷武器飞行更快、更远、更精准。这种力量不是魔法的——它是技巧的延伸，是无数年训练的结晶。\n\n这枚戒指是忍者之影在「手里剑道场」中，从自己的训练成果中铸造的。他说："这枚戒指让我能够将手里剑的力量发挥到极致。但记住——手里剑不是杀人的工具，它是艺术的表达。"\n\n戒指的效果是：投掷武器射程 +10 尺，且攻击检定 +2（手里剑精通）。每日一次，可以释放「手里剑风暴」——对 15 尺内一个敌人投掷 3D4 枚手里剑（每枚 +5，1D4+2 piercing），须通过 DC16 敏捷豁免，失败则全中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "ninja_shadow_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ninja_shadow_ring_1" }
]
```

---

### 72.8 忍者之影替身之戒（Ninja Shadow Substitution Ring）

```gdscript
item_id = "acc_ninja_shadow_ring_2"
display_name = "忍者之影替身之戒"
description = "一枚由替身木与忍者银铸造的戒指，戒指表面刻有微型木桩图案。当佩戴者集中精神时，戒指会释放出一股替身之力，在受到攻击的瞬间与一根木桩交换位置。这种替身术是忍者的基本技能，但这枚戒指让它变得更快、更隐蔽。\n\n这枚戒指是忍者之影在「替身术训练场」中，从自己的替身木桩上取下的碎片铸造的。他说："这枚戒指让我能够在最危险的时刻逃脱。但替身不是万能的——它只能救你一次。"\n\n戒指的效果是：每日一次，当受到致命攻击时，自动触发「替身术」——与 10 尺内的一根木桩交换位置，免疫该次伤害，且木桩承受伤害（木桩 1 HP）。下一回合可以 bonus action 传送到木桩位置（逆转）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "ninja_shadow_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ninja_shadow_ring_2" }
]
```

---

### 72.9 忍者之影烟雾弹（Ninja Shadow Smoke Bomb）

```gdscript
item_id = "acc_ninja_shadow_trinket"
display_name = "忍者之影烟雾弹"
description = "一个由忍者秘方制成的烟雾弹，烟雾弹表面没有任何标记——真正的忍者从不留下痕迹。当激活时，烟雾弹会释放出一股浓密的紫色烟雾，烟雾不仅遮蔽视线，还会让吸入者产生幻觉。这种烟雾不是普通的烟——它是用「幻梦草」和「夜影花」制成的，每一颗都需要三年时间准备。\n\n这个烟雾弹是忍者之影在「烟雾工坊」中，从自己的珍藏中取出的。他说："这个烟雾弹让我能够在任何情况下逃脱。烟雾是忍者的朋友，是敌人的噩梦。"\n\n烟雾弹的效果是：每日两次，可以 bonus action 投掷烟雾弹——10 尺半径内充满浓雾，能见度降至 5 尺，持续 2 回合。敌人在雾中攻击检定有劣势，且每回合开始时须通过 DC14 体质豁免，失败则 poisoned 1 回合（幻觉烟雾）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "ninja_shadow_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_ninja_shadow_trinket" }
]
```

---

### 72.10 忍者之影徽章（Ninja Shadow Badge）

```gdscript
item_id = "acc_ninja_shadow_badge"
display_name = "忍者之影徽章"
description = "一枚由忍者金与暗影石铸造的徽章，徽章上刻着一片叶子和一把刀——忍者之影的标志。徽章背面刻着一行小字：「木の葉隠れ、影に生きる。」这枚徽章是忍者公会的信物，拥有它意味着你已被暗影认可。\n\n徽章的效果是：10 尺内所有友方隐匿检定 +1（暗影庇护）。且每日一次，可以释放「影分身」——10 尺内所有友方各创造一个镜像分身（1 HP，AC = 自身 AC），持续 1 回合。分身被击中时消散。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "ninja_shadow_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ninja_shadow_badge" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ninja_shadow_badge" }
]
```

---

## 套装七十三：少林武僧（Shaolin Monk）饰品

> *"少林武僧不需要武器——他的身体就是武器，他的意志就是盔甲。"*

---

### 73.5 少林武僧袈裟披风（Shaolin Monk Kasaya Cloak）

```gdscript
item_id = "acc_shaolin_monk_cloak"
display_name = "少林武僧袈裟披风"
description = "一件由少林袈裟与禅丝编织而成的披风，披风表面绣着「佛」字和莲花图案。这件袈裟不是给普通僧侣穿的——它是给武僧穿的。当穿戴者修行时，袈裟会自动调整温度，使穿戴者保持最佳的修行状态。据说这件袈裟是达摩祖师穿越嵩山时留下的。\n\n这件袈裟是少林武僧在「少林寺」中，从自己的师傅那里继承的。他说："这件袈裟承载了我的修行之道。它不是保护——它是提醒，提醒我永远不要忘记自己的初心。"\n\n披风的效果是：体质豁免 +2（禅心坚韧），且免疫 frightened 和 charmed（禅心清明）。当穿戴者受到致命攻击时，有 25% 概率触发「金钟罩」——免疫该次伤害（禅武护身）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "shaolin_monk_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shaolin_monk_cloak" },
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shaolin_monk_cloak" }
]
```

---

### 73.6 少林武僧佛珠项链（Shaolin Monk Prayer Beads Necklace）

```gdscript
item_id = "acc_shaolin_monk_necklace"
display_name = "少林武僧佛珠项链"
description = "一条由一百零八颗佛珠编织而成的项链，每一颗佛珠都来自不同的树木：菩提、楠木、檀香、沉香。据说这串佛珠是达摩祖师在面壁九年时，用他每日掉落的头发和指甲制成的。每一颗佛珠都蕴含着达摩的智慧，佩戴者可以通过触摸它们，感受到禅的力量。\n\n这条项链是少林武僧在「达摩洞」中，从自己的修行成果中取出的。他说："这串佛珠承载了我的修行。每一次捻动，都是一次冥想；每一颗佛珠，都是一次觉悟。"\n\n项链的效果是：每日一次，可以 bonus action 捻动佛珠——恢复 3D8 HP（禅心治愈），且移除一个非 legendary 恐惧或魅惑效果（禅心净化）。且宗教检定 +2（佛法）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "shaolin_monk_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shaolin_monk_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shaolin_monk_necklace" }
]
```

---

### 73.7 少林武僧铁指环（Shaolin Monk Iron Finger Ring）

```gdscript
item_id = "acc_shaolin_monk_ring_1"
display_name = "少林武僧铁指环"
description = "一枚由少林铁与禅金铸造的戒指，戒指表面刻有「铁指」二字。当佩戴者集中精神时，戒指会释放出一股铁指之力，使佩戴者的手指变得如同钢铁般坚硬。这种力量不是魔法的——它是无数年铁指禅训练的结果，是意志的延伸。据说这枚戒指来自一位铁指禅大师，他用这枚戒指击碎了十八块花岗岩石板。\n\n这枚戒指是少林武僧在「铁指禅房」中，从自己的训练成果中铸造的。他说："这枚戒指让我能够将铁指禅的力量发挥到极致。但记住——手指是身体的延伸，身体是心灵的容器。"\n\n戒指的效果是：徒手攻击变为 1D10 bludgeoning（铁指），且每日一次，可以释放「一指禅」——指定一个 5 尺内的敌人，受到 3D10 bludgeoning（铁指爆发），目标须通过 DC16 体质豁免，失败则 stunned 1 回合（点穴）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "shaolin_monk_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shaolin_monk_ring_1" }
]
```

---

### 73.8 少林武僧气之戒（Shaolin Monk Qi Ring）

```gdscript
item_id = "acc_shaolin_monk_ring_2"
display_name = "少林武僧气之戒"
description = "一枚由气石与禅银铸造的戒指，戒指表面刻有微型气脉图案。当佩戴者集中精神时，戒指会释放出一股气之力，增强佩戴者的内力。这种力量不是魔法的——它是气功的延伸，是呼吸的艺术。据说这枚戒指来自一位气功大师，他用这枚戒指将内力提升到前所未有的境界。\n\n这枚戒指是少林武僧在「气功房」中，从自己的修行成果中铸造的。他说："这枚戒指让我能够将气功的力量发挥到极致。气是生命的力量，是宇宙的呼吸。学会感受它。"\n\n戒指的效果是：每日一次，可以释放「气功爆发」——恢复 2D10 HP（气功治愈），且下一回合所有攻击附加 1D8 force（气功冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "shaolin_monk_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shaolin_monk_ring_2" }
]
```

---

### 73.9 少林武僧达摩杖（Shaolin Monk Dharma Staff）

```gdscript
item_id = "acc_shaolin_monk_trinket"
display_name = "少林武僧达摩杖"
description = "一根由达摩木与禅金制成的微型法杖，法杖表面刻有「佛法无边」四字。这根法杖不是武器——它是修行的工具，是禅的象征。当佩戴者需要时，可以激活法杖，释放出达摩的智慧，治愈伤痛或驱散邪恶。据说这根法杖是达摩祖师穿越嵩山时使用的，上面还留有他的体温。\n\n这根法杖是少林武僧在「达摩洞」中，从自己的修行成果中取出的。他说："这根法杖承载了我的修行之道。它不是用来打人的——它是用来唤醒的。"\n\n法杖的效果是：每日一次，可以释放「佛法无边」——10 尺内所有友方恢复 2D8 HP（佛法治愈），且移除所有非 legendary 恐惧、魅惑、诅咒（佛法净化）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "shaolin_monk_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_shaolin_monk_trinket" }
]
```

---

### 73.10 少林武僧徽章（Shaolin Monk Badge）

```gdscript
item_id = "acc_shaolin_monk_badge"
display_name = "少林武僧徽章"
description = "一枚由禅金与佛石铸造的徽章，徽章上刻着一朵莲花和一个拳头——少林武僧的标志。徽章背面刻着一行小字：「禅武合一，万法归宗。」这枚徽章是少林公会的信物，拥有它意味着你已理解禅武合一的真谛。\n\n徽章的效果是：10 尺内所有友方体质豁免 +1（禅心庇护）。且每日一次，可以释放「禅武气场」——10 尺内所有友方免疫 frightened 和 charmed 2 回合（禅心庇护），且徒手攻击 +1D6 force（禅武之力）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "shaolin_monk_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shaolin_monk_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shaolin_monk_badge" }
]
```

---

## 套装七十四：天狗面具（Tengu Mask）饰品

> *"天狗面具不是装饰——它是天狗的脸，戴上它，你就成为了天狗。"*

---

### 74.5 天狗面具天狗羽衣披风（Tengu Mask Tengu Feather Cloak）

```gdscript
item_id = "acc_tengu_mask_cloak"
display_name = "天狗面具天狗羽衣披风"
description = "一件由天狗羽毛与山风丝编织而成的披风，披风表面覆盖着黑色的天狗羽毛，每一根羽毛都散发着淡淡的风之力。当穿戴者跳跃时，羽毛会自动扇动，使穿戴者可以在空中滑翔。据说这件披风是山之大天狗亲手编织的，每一根羽毛都来自不同的天狗。\n\n这件披风是天狗面具在「天狗之山」中，从大天狗那里得到的。大天狗说："这件披风让你能够借用天狗的力量。但天狗是骄傲的——不要让它失望。"\n\n披风的效果是：跳跃距离 ×2，且可以滑翔（每回合下降不超过 30 尺，水平移动速度 40 尺）。当从高处跳下攻击时，攻击检定有优势，且伤害 +2D6（天狗俯冲）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "tengu_mask_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_tengu_mask_cloak" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_tengu_mask_cloak" }
]
```

---

### 74.6 天狗面具羽扇项链（Tengu Mask Feather Fan Necklace）

```gdscript
item_id = "acc_tengu_mask_necklace"
display_name = "天狗面具羽扇项链"
description = "一条由天狗羽毛与山金编织而成的项链，吊坠是一把微型羽扇——这把羽扇不是装饰品，它是真正的法器。羽扇表面有天狗的脸，当扇动时，可以召唤山风和雷电。据说这把羽扇属于山之大天狗，他用这把羽扇扇起了摧毁一座城市的台风。\n\n这条项链是天狗面具在「天狗之社」中，从大天狗的遗物中取出的。他说："这把羽扇承载了我的山之力。它可以召唤风，也可以驱散邪恶。"\n\n项链的效果是：自然检定 +3，且每日一次，可以释放「天狗之扇」——15 尺锥形强风，所有生物须通过 DC16 力量豁免，失败则被推开 10 尺并 knocked prone，成功则 half push。且友方在强风中移动力 +10 尺（顺风）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "tengu_mask_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_tengu_mask_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_tengu_mask_necklace" }
]
```

---

### 74.7 天狗面具疾风之戒（Tengu Mask Gale Ring）

```gdscript
item_id = "acc_tengu_mask_ring_1"
display_name = "天狗面具疾风之戒"
description = "一枚由疾风石与天狗金铸造的戒指，戒指表面刻有微型风涡图案。当佩戴者集中精神时，戒指会释放出一股疾风之力，使佩戴者的速度大幅提升。这种力量不是魔法的——它是山风的延伸，是天狗的速度。据说这枚戒指来自一位天狗将军，他用这枚戒指在战场上如风一般穿梭。\n\n这枚戒指是天狗面具在「风之谷」中，从天狗将军的遗骸中取出的。他说："这枚戒指让我能够借用天狗的速度。但速度不是一切——方向比速度更重要。"\n\n戒指的效果是：移动力 +10 尺（疾风步），且每日一次，可以释放「疾风冲刺」——bonus action 移动至 30 尺内任何位置，路径上的所有敌人须通过 DC15 敏捷豁免，失败则受到 2D6 bludgeoning（风压）并 knocked prone。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "tengu_mask_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_tengu_mask_ring_1" }
]
```

---

### 74.8 天狗面具山岚之戒（Tengu Mask Mountain Mist Ring）

```gdscript
item_id = "acc_tengu_mask_ring_2"
display_name = "天狗面具山岚之戒"
description = "一枚由山岚石与天狗银铸造的戒指，戒指表面刻有微型山岚图案。当佩戴者集中精神时，戒指会释放出一股山岚之力，使周围充满迷雾。这种迷雾不是普通的雾——它是山岚，可以遮蔽敌人的视线，也可以让佩戴者在雾中隐形。据说这枚戒指来自一位山岚天狗，他用这枚戒指在战场上制造了永不消散的迷雾。\n\n这枚戒指是天狗面具在「岚之峰」中，从山岚天狗的遗骸中取出的。他说："这枚戒指让我能够借用山岚的力量。山岚不是遮掩——它是自然的一部分。"\n\n戒指的效果是：隐匿检定 +2（山岚掩护），且每日一次，可以释放「山岚领域」——20 尺半径内充满山岚，能见度降至 5 尺，持续 2 回合。佩戴者在山岚中完全隐形（如同 greater invisibility）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "tengu_mask_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_tengu_mask_ring_2" }
]
```

---

### 74.9 天狗面具天狗面具（Tengu Mask Tengu Mask）

```gdscript
item_id = "acc_tengu_mask_trinket"
display_name = "天狗面具天狗面具"
description = "一张由天狗骨与山金制成的面具，面具表面是天狗的脸——红鼻、长须、怒目。这张面具不是普通的装饰品——它是活的。当佩戴者戴上它时，会感受到天狗的力量涌入身体：速度、力量、骄傲。但面具也有自己的意志——它会试图控制佩戴者，让佩戴者变得傲慢和冲动。\n\n这张面具是天狗面具在「天狗之山」中，从大天狗的脸上取下的。大天狗说："这张面具让你能够借用我的力量。但记住——天狗是骄傲的，骄傲会带来毁灭。"\n\n面具的效果是：每日一次，可以戴上/取下天狗面具。戴上时：力量 +2，敏捷 +2，攻击检定 +2，但智慧豁免 -2（傲慢）。持续 3 回合。取下后疲惫 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "tengu_mask_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_tengu_mask_trinket" }
]
```

---

### 74.10 天狗面具徽章（Tengu Mask Badge）

```gdscript
item_id = "acc_tengu_mask_badge"
display_name = "天狗面具徽章"
description = "一枚由天狗金与山石铸造的徽章，徽章上刻着一把羽扇和一座山——天狗面具的标志。徽章背面刻着一行小字：「山高则天狗远，心高则天狗近。」这枚徽章是天狗公会的信物，拥有它意味着你已被天狗认可。\n\n徽章的效果是：10 尺内所有友方跳跃距离 +5 尺（天狗之翼）。且每日一次，可以释放「山风祝福」——10 尺内所有友方移动力 +10 尺（疾风步），且攻击检定 +1（山风之力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "tengu_mask_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_tengu_mask_badge" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_tengu_mask_badge" }
]
```

---

## 套装七十五：九尾妖狐（Nine-Tailed Fox）饰品

> *"九尾妖狐不是欺骗你——她只是让你看到了你想看到的。"*

---

### 75.5 九尾妖狐狐火披风（Nine-Tailed Fox Foxfire Cloak）

```gdscript
item_id = "acc_nine_tailed_fox_cloak"
display_name = "九尾妖狐狐火披风"
description = "一件由九尾妖狐的毛发与狐火丝编织而成的披风，披风表面不断有微型狐火在飘动和闪烁——不是装饰，而是真正的狐火被封印在了丝线中。这些狐火来自九尾妖狐的尾巴，每一朵狐火都有其独特的颜色：红、橙、黄、绿、蓝、靛、紫、白、黑。当穿戴者移动时，狐火会跟随，照亮道路，也迷惑敌人。\n\n这件披风是九尾妖狐在「玉藻前宫」中，从自己的尾巴上取下的毛发编织的。她说："这件披风让我能够将狐火的力量带到任何地方。它可以照亮，也可以燃烧；可以迷惑，也可以治愈。"\n\n披风的效果是：火焰抗性 +15（狐火之躯），且在黑暗中 10 尺半径内提供 dim light（狐火照明）。每日一次，可以释放「狐火风暴」——15 尺半径内所有敌人受到 3D10 fire（DC16 敏捷豁免 half），且被迷惑 1 回合（狐火幻惑，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "nine_tailed_fox_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_nine_tailed_fox_cloak" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nine_tailed_fox_cloak" }
]
```

---

### 75.6 九尾妖狐勾玉项链（Nine-Tailed Fox Magatama Necklace）

```gdscript
item_id = "acc_nine_tailed_fox_necklace"
display_name = "九尾妖狐勾玉项链"
description = "一条由九尾妖狐的勾玉与狐火金编织而成的项链，吊坠是三颗勾玉——这三颗勾玉是九尾妖狐的力量的象征。每一颗勾玉都有不同的能力：第一颗掌控火焰，第二颗掌控幻象，第三颗掌控生命。据说这三颗勾玉来自天照大神，她将它们赠予了第一位九尾妖狐。\n\n这条项链是九尾妖狐在「天岩户」中，从天照大神的祭坛上取出的。她说："这三颗勾玉承载了我的全部力量。它们让我能够掌控火焰、幻象和生命。"\n\n项链的效果是：魅力检定 +3（妖狐之魅），且每日一次，可以选择释放一种勾玉之力：\n- 火焰勾玉：指定一个敌人，受到 3D10 fire（DC16 敏捷豁免 half）\n- 幻象勾玉：指定一个敌人，charmed 2 回合（DC16 智慧豁免抵抗）\n- 生命勾玉：自身或 5 尺内一个友方恢复 3D8 HP"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "nine_tailed_fox_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_nine_tailed_fox_necklace" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nine_tailed_fox_necklace" }
]
```

---

### 75.7 九尾妖狐幻惑之戒（Nine-Tailed Fox Illusion Ring）

```gdscript
item_id = "acc_nine_tailed_fox_ring_1"
display_name = "九尾妖狐幻惑之戒"
description = "一枚由幻惑石与狐火金铸造的戒指，戒指表面刻有微型九尾图案。当佩戴者集中精神时，戒指会释放出一股幻惑之力，创造出一个佩戴者的幻象。这个幻象不是普通的幻象——它是「真实幻象」，可以触摸，可以说话，甚至可以攻击。但幻象是脆弱的——一旦被识破，就会消散。\n\n这枚戒指是九尾妖狐在「幻惑深渊」中，从幻惑之神那里得到的。幻惑之神说："这枚戒指让你能够借用我的力量。但幻惑是双刃剑——它既能欺骗敌人，也能欺骗你自己。"\n\n戒指的效果是：每日一次，可以释放「真实幻象」——创造一个完全相同的分身（HP = 你的 max HP × 25%，AC = 你的 AC），持续 2 回合。分身可以进行攻击（+5，1D8+3 force），且可以 cast 一个你拥有的法术（消耗你的 mana）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "nine_tailed_fox_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nine_tailed_fox_ring_1" }
]
```

---

### 75.8 九尾妖狐魅惑之戒（Nine-Tailed Fox Charm Ring）

```gdscript
item_id = "acc_nine_tailed_fox_ring_2"
display_name = "九尾妖狐魅惑之戒"
description = "一枚由魅惑石与狐火银铸造的戒指，戒指表面刻有微型魅惑符文。当佩戴者集中精神时，戒指会释放出一股魅惑之力，使目标对佩戴者产生强烈的好感。这种魅惑不是魔法的——它是自然的，是九尾妖狐的天赋。据说这枚戒指来自一位九尾妖狐公主，她用这枚戒指魅惑了一整个王国。\n\n这枚戒指是九尾妖狐在「魅惑之宫」中，从自己的收藏中取出的。她说："这枚戒指让我能够借用魅惑的力量。但魅惑不是爱——它只是欺骗。真正的爱是无需魅惑的。"\n\n戒指的效果是：魅力检定 +2（魅惑之力），且每日一次，可以释放「魅惑凝视」——指定一个 15 尺内的敌人，其 charmed 2 回合（DC16 智慧豁免抵抗）。被魅惑的目标会保护你，攻击你的敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "nine_tailed_fox_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nine_tailed_fox_ring_2" }
]
```

---

### 75.9 九尾妖狐狐尾（Nine-Tailed Fox Fox Tail）

```gdscript
item_id = "acc_nine_tailed_fox_trinket"
display_name = "九尾妖狐狐尾"
description = "一条由九尾妖狐的尾巴制成的微型饰品，尾巴表面覆盖着柔软的毛发，颜色从根部的白色渐变到尖端的金色。这条尾巴不是普通的尾巴——它是活的。当佩戴者需要时，可以激活尾巴，释放出九尾妖狐的力量：火焰、幻象、治愈、魅惑。但尾巴有自己的意志——它会试图控制佩戴者，让佩戴者变得越来越像九尾妖狐。\n\n这条尾巴是九尾妖狐在「玉藻前宫」中，从自己的一条尾巴上取下的。她说："这条尾巴承载了我的力量。但它也让我越来越难以保持人类的形态。"\n\n尾巴的效果是：每日一次，可以释放「九尾之力」——选择一个效果：\n- 火焰：15 尺半径 3D10 fire（DC16 敏捷豁免 half）\n- 幻象：自身隐形 2 回合\n- 治愈：恢复 4D8 HP\n- 魅惑：15 尺内一个敌人 charmed 2 回合（DC16 智慧豁免抵抗）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "nine_tailed_fox_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_nine_tailed_fox_trinket" }
]
```

---

### 75.10 九尾妖狐徽章（Nine-Tailed Fox Badge）

```gdscript
item_id = "acc_nine_tailed_fox_badge"
display_name = "九尾妖狐徽章"
description = "一枚由狐火金与幻石铸造的徽章，徽章上刻着九条尾巴和一个月亮——九尾妖狐的标志。徽章背面刻着一行小字：「九尾遮天，一眸惑世。」这枚徽章是九尾公会的信物，拥有它意味着你已被九尾认可。\n\n徽章的效果是：10 尺内所有友方魅力检定 +1（妖狐庇护）。且每日一次，可以释放「狐火领域」——10 尺半径内所有敌人每回合开始时受到 1D6 fire（狐火灼烧），且友方在领域内 AC +1（狐火守护），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "nine_tailed_fox_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_nine_tailed_fox_badge" },
    { attribute_id = "deception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_nine_tailed_fox_badge" }
]
```

---

## 套装七十六：阴阳师（Onmyoji）饰品

> *"阴阳师不是在占卜命运——他是在与命运谈判。"*

---

### 76.5 阴阳师狩衣披风（Onmyoji Kariginu Cloak）

```gdscript
item_id = "acc_onmyoji_cloak"
display_name = "阴阳师狩衣披风"
description = "一件由阴阳师狩衣与咒丝编织而成的披风，披风表面绣着十二神将的符号和阴阳鱼的图案。这件狩衣不是给普通法师穿的——它是给阴阳师穿的。当穿戴者施法时，狩衣会自动调整，使法术更加精准。据说这件狩衣是安倍晴明亲手制作的，上面还留有他的咒力。\n\n这件披风是阴阳师在「阴阳寮」中，从安倍晴明的遗物中继承的。他说："这件狩衣承载了我的咒术之道。它不是保护——它是媒介，是人与灵之间的桥梁。"\n\n披风的效果是：法术攻击检定 +2（咒术精准），且法术豁免 DC +1（咒术强度）。当施法时，有 25% 概率不消耗法术位（咒术回流）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "onmyoji_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_onmyoji_cloak" },
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_onmyoji_cloak" }
]
```

---

### 76.6 阴阳师五芒星项链（Onmyoji Pentagram Necklace）

```gdscript
item_id = "acc_onmyoji_necklace"
display_name = "阴阳师五芒星项链"
description = "一条由咒金与五芒星水晶编织而成的项链，吊坠是一个五芒星——阴阳师最基本的咒术符号。但这个五芒星不是普通的符号——它是活的。当佩戴者施法时，五芒星会自动旋转，吸收周围的灵力，增强法术的效果。据说这个五芒星来自高天原，是神赐予阴阳师的礼物。\n\n这条项链是阴阳师在「高天原之社」中，从神的祭坛上取出的。他说："这个五芒星承载了我的咒术之源。它让我能够与灵界沟通，也让我能够召唤神灵。"\n\n项链的效果是：奥秘检定 +3（咒术知识），且每日一次，可以释放「式神召唤」——召唤一个式神（HP 25，AC 15，攻击 +5，2D6+3 force），持续 1 分钟。式神可以 bonus action 命令，且可以 cast 一个辅助法术（消耗你的 mana）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "onmyoji_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_onmyoji_necklace" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_onmyoji_necklace" }
]
```

---

### 76.7 阴阳师咒符之戒（Onmyoji Talisman Ring）

```gdscript
item_id = "acc_onmyoji_ring_1"
display_name = "阴阳师咒符之戒"
description = "一枚由咒符石与咒金铸造的戒指，戒指表面刻有微型咒符图案。当佩戴者集中精神时，戒指会释放出一股咒符之力，使佩戴者可以瞬间释放咒符。这种咒符不是纸制的——它是能量的，是意志的延伸。据说这枚戒指来自一位阴阳师大师，他用这枚戒指在一秒钟内释放了十二张咒符。\n\n这枚戒指是阴阳师在「咒符房」中，从自己的咒符制作成果中铸造的。他说："这枚戒指让我能够借用咒符的力量。但咒符只是工具——真正的力量在心中。"\n\n戒指的效果是：每日三次，可以 bonus action 释放一张咒符：\n- 火符：指定一个敌人，受到 2D8 fire\n- 冰符：指定一个敌人，受到 2D8 cold 且移动力 -10 尺 1 回合\n- 雷符：指定一个敌人，受到 2D8 lightning 且 stunned 1 回合（DC15 体质豁免抵抗）\n- 治愈符：指定一个友方，恢复 2D8 HP"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "onmyoji_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_onmyoji_ring_1" }
]
```

---

### 76.8 阴阳师封印之戒（Onmyoji Seal Ring）

```gdscript
item_id = "acc_onmyoji_ring_2"
display_name = "阴阳师封印之戒"
description = "一枚由封印石与咒银铸造的戒指，戒指表面刻有微型封印图案。当佩戴者集中精神时，戒指会释放出一股封印之力，封印目标的某个能力。这种封印不是永久的——它只能持续很短的时间。但封印的效果是彻底的——它可以封印魔法、封印动作、甚至封印生命。\n\n这枚戒指是阴阳师在「封印之间」中，从封印之神的祭坛上取出的。封印之神说："这枚戒指让你能够借用我的力量。但封印是危险的——它可以封印邪恶，也可以封印善良。"\n\n戒指的效果是：每日一次，可以释放「封印之术」——指定一个 15 尺内的敌人，其一种能力被封印 2 回合：施法、攻击、移动、或恢复（由你选择，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "onmyoji_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_onmyoji_ring_2" }
]
```

---

### 76.9 阴阳师式神卷轴（Onmyoji Shikigami Scroll）

```gdscript
item_id = "acc_onmyoji_trinket"
display_name = "阴阳师式神卷轴"
description = "一个由咒纸与咒金制成的微型卷轴，卷轴上写满了式神的名字和召唤咒文。这个卷轴不是普通的魔法物品——它是活的。当佩戴者需要时，可以打开卷轴，召唤其中一个式神。每个式神都有不同的能力：有的擅长攻击，有的擅长防御，有的擅长治疗。但式神需要报酬——每次召唤，会消耗佩戴者的部分 mana。\n\n这个卷轴是阴阳师在「式神之间」中，从自己的式神契约中取出的。他说："这个卷轴承载了我的式神契约。每一个式神都是我的朋友，也是我的守护者。"\n\n卷轴的效果是：每日一次，可以从卷轴中召唤一个式神（选择一种）：\n- 火之式神：HP 30，AC 14，攻击 +6，2D8+4 fire\n- 水之式神：HP 35，AC 16，攻击 +4，1D8+2 bludgeoning，可以为友方恢复 2D8 HP\n- 风之式神：HP 20，AC 18，攻击 +7，1D8+3 slashing，可以 bonus action 传送到 30 尺内任何位置\n- 土之式神：HP 40，AC 18，攻击 +4，1D10+4 bludgeoning，可以 taunt 敌人\n持续 1 分钟，可以用 bonus action 指挥。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "onmyoji_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_onmyoji_trinket" }
]
```

---

### 76.10 阴阳师徽章（Onmyoji Badge）

```gdscript
item_id = "acc_onmyoji_badge"
display_name = "阴阳师徽章"
description = "一枚由咒金与阴阳石铸造的徽章，徽章上刻着阴阳鱼和一把扇子——阴阳师的标志。徽章背面刻着一行小字：「阴阳调和，万物归一。」这枚徽章是阴阳寮的信物，拥有它意味着你已被阴阳之道认可。\n\n徽章的效果是：10 尺内所有友方法术豁免 +1（咒术庇护）。且每日一次，可以释放「阴阳领域」——10 尺半径内友方法术攻击检定 +1（阳之力），敌方法术攻击检定 -1（阴之力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "onmyoji_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_onmyoji_badge" },
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_onmyoji_badge" }
]
```

---

*套装 71–76 饰品部分（36/60 件）*

## 套装七十七：夜叉（Yaksha）饰品

> *"夜叉不是恶魔——他是守护神，只是他的守护方式比较...直接。"*

---

### 77.5 夜叉战鬼披风（Yaksha Battle Oni Cloak）

```gdscript
item_id = "acc_yaksha_cloak"
display_name = "夜叉战鬼披风"
description = "一件由夜叉战鬼的皮与愤怒丝编织而成的披风，披风表面不断有微型战鬼面孔在扭曲和咆哮——不是装饰，而是真正的战鬼灵魂被封印在了丝线中。当穿戴者进入战斗时，披风会自动膨胀，释放出愤怒的气息，使敌人感到恐惧。据说这件披风来自一位战死的夜叉将军，他的愤怒如此强大，即使在死后也无法消散。\n\n这件披风是夜叉在「战鬼之山」中，从自己的战利品上取下的碎片编织的。他说："这件披风承载了我的愤怒。每一次战斗，我都会感受到战鬼的力量在我体内燃烧。"\n\n披风的效果是：免疫 frightened（战鬼之怒），且进入战斗时，10 尺内所有敌人须通过 DC15 智慧豁免，失败则 frightened 1 回合（战鬼咆哮）。当击杀敌人时，攻击检定 +1（可叠加至 +3，战鬼狂怒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "yaksha_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_yaksha_cloak" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_yaksha_cloak" }
]
```

---

### 77.6 夜叉獠牙项链（Yaksha Fang Necklace）

```gdscript
item_id = "acc_yaksha_necklace"
display_name = "夜叉獠牙项链"
description = "一条由夜叉獠牙与战鬼金编织而成的项链，吊坠是一颗夜叉獠牙——这颗獠牙来自一位千年夜叉王。獠牙内部有微型火焰在燃烧，那是夜叉王的愤怒之火。据说这颗獠牙可以咬碎任何东西：钢铁、岩石、甚至是魔法护盾。\n\n这条项链是夜叉在「夜叉王墓」中，从夜叉王的遗骸上取下的。他说："这颗獠牙承载了我的守护之力。它不是用来杀戮的——它是用来保护的。"\n\n项链的效果是：力量检定 +2（夜叉之力），且每日一次，可以释放「獠牙撕咬」——指定一个 5 尺内的敌人，受到 3D10 piercing + 2D6 fire（獠牙之火），目标须通过 DC16 力量豁免，失败则 grappled 2 回合（獠牙咬住）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "yaksha_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_yaksha_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_yaksha_necklace" }
]
```

---

### 77.7 夜叉破坏之戒（Yaksha Destruction Ring）

```gdscript
item_id = "acc_yaksha_ring_1"
display_name = "夜叉破坏之戒"
description = "一枚由破坏石与战鬼金铸造的戒指，戒指表面刻有微型破坏符文。当佩戴者集中精神时，戒指会释放出一股破坏之力，使佩戴者的下一次攻击如同夜叉的破坏之拳。这种破坏不是物理的——它是纯粹的毁灭之力，可以粉碎任何东西。\n\n这枚戒指是夜叉在「破坏深渊」中，从破坏之神那里得到的。破坏之神说："这枚戒指让你能够借用我的力量。但破坏是最后的手段——它不会带来和平。"\n\n戒指的效果是：每日一次，下一次攻击伤害骰翻倍（破坏之击），且无视 damage resistance（破坏穿透）。如果该攻击击杀目标，对 5 尺内所有敌人造成 2D8 force（破坏冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "yaksha_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_yaksha_ring_1" }
]
```

---

### 77.8 夜叉守护之戒（Yaksha Protection Ring）

```gdscript
item_id = "acc_yaksha_ring_2"
display_name = "夜叉守护之戒"
description = "一枚由守护石与战鬼银铸造的戒指，戒指表面刻有微型守护符文。当佩戴者集中精神时，戒指会释放出一股守护之力，在佩戴者周围形成一道无形的护盾。这种守护不是被动的——它是主动的，会主动攻击靠近的敌人。\n\n这枚戒指是夜叉在「守护之山」中，从守护之神的祭坛上取出的。守护之神说："这枚戒指让你能够借用我的力量。但守护是有代价的——它会消耗你的生命力。"\n\n戒指的效果是：5 尺内友方 AC +1（守护光环）。每日一次，可以释放「夜叉守护」——指定一个 10 尺内的友方，其获得「夜叉守护」2 回合：AC +3，且近战攻击者受到 1D8 force（守护反击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "yaksha_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_yaksha_ring_2" }
]
```

---

### 77.9 夜叉战鬼之角（Yaksha Battle Oni Horn）

```gdscript
item_id = "acc_yaksha_trinket"
display_name = "夜叉战鬼之角"
description = "一根由战鬼之角与愤怒金制成的微型饰品，角表面覆盖着黑色的纹路，那是战鬼的愤怒印记。这根角不是普通的角——它是活的。当佩戴者需要时，可以激活角，释放出战鬼的愤怒，使自己变得更加强大。但角有自己的意志——它会试图控制佩戴者，让佩戴者变得越来越愤怒。\n\n这根角是夜叉在「战鬼之山」中，从一位战死的战鬼身上取下的。他说："这根角承载了我的愤怒之力。但它也让我越来越难以控制自己的情绪。"\n\n角的效果是：每日一次，可以释放「战鬼觉醒」——力量 +4，攻击检定 +2，移动力 +10 尺，持续 2 回合。但结束后 stunned 1 回合（愤怒过载）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "yaksha_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_yaksha_trinket" }
]
```

---

### 77.10 夜叉徽章（Yaksha Badge）

```gdscript
item_id = "acc_yaksha_badge"
display_name = "夜叉徽章"
description = "一枚由战鬼金与愤怒石铸造的徽章，徽章上刻着一把三叉戟和一个战鬼面孔——夜叉的标志。徽章背面刻着一行小字：「守护即破坏，破坏即守护。」这枚徽章是夜叉公会的信物，拥有它意味着你已被战鬼认可。\n\n徽章的效果是：10 尺内所有友方免疫 frightened（战鬼威严）。且每日一次，可以释放「战鬼领域」——10 尺内所有敌人攻击检定 -1（战鬼压迫），且友方攻击检定 +1（战鬼鼓舞），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "yaksha_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_yaksha_badge" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_yaksha_badge" }
]
```

---

## 套装七十八：机关傀儡师（Karakuri Puppeteer）饰品

> *"机关傀儡师不是在操纵木偶——他是在创造生命，哪怕只是暂时的。"*

---

### 78.5 机关傀儡师机关披风（Karakuri Puppeteer Karakuri Cloak）

```gdscript
item_id = "acc_karakuri_puppeteer_cloak"
display_name = "机关傀儡师机关披风"
description = "一件由机关丝与齿轮布编织而成的披风，披风表面不断有微型齿轮在转动和咬合——不是装饰，而是真正的机关被封印在了丝线中。当穿戴者移动时，齿轮会自动调整披风的形状，使穿戴者更加灵活。据说这件披风是机关傀儡师亲手制作的，每一个齿轮都来自不同的机关装置。\n\n这件披风是机关傀儡师在「机关工坊」中，从自己的收藏中取出的。他说："这件披风让我能够将机关的力量带到任何地方。它可以变形，可以防御，可以攻击——它是我身体的延伸。"\n\n披风的效果是：AC +1（机关护甲），且每日一次，可以释放「机关变形」——披风变形为以下一种形态，持续 2 回合：\n- 盾形态：AC +3\n- 翼形态：移动力 +15 尺\n- 刃形态：徒手攻击变为 1D10 slashing"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "karakuri_puppeteer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_karakuri_puppeteer_cloak" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_karakuri_puppeteer_cloak" }
]
```

---

### 78.6 机关傀儡师发条之心项链（Karakuri Puppeteer Clockwork Heart Necklace）

```gdscript
item_id = "acc_karakuri_puppeteer_necklace"
display_name = "机关傀儡师发条之心项链"
description = "一条由发条金与齿轮丝编织而成的项链，吊坠是一颗发条之心——一颗由纯粹机械凝结的心脏。心脏在吊坠中永恒地跳动，发出咔哒咔哒的声音。据说这颗心脏来自一位机关傀儡师自己，他在将自己的心脏替换为机械心脏后，将原来的心脏制成了这件饰品。\n\n这条项链是机关傀儡师在「机关工坊」中，从自己的身体里取出的。他说："这颗心脏承载了我的机械之魂。它让我能够理解机关的语言，也让我能够创造生命。"\n\n项链的效果是：奥秘检定 +3（机关知识），且免疫 poison 和 disease（机械之躯）。每日一次，可以释放「发条治愈」——恢复 2D10 HP（发条修复），且移除一个非 legendary 诅咒或疾病（机械净化）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "karakuri_puppeteer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_karakuri_puppeteer_necklace" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_karakuri_puppeteer_necklace" }
]
```

---

### 78.7 机关傀儡师操控之戒（Karakuri Puppeteer Control Ring）

```gdscript
item_id = "acc_karakuri_puppeteer_ring_1"
display_name = "机关傀儡师操控之戒"
description = "一枚由操控石与机关金铸造的戒指，戒指表面刻有微型操控杆图案。当佩戴者集中精神时，戒指会释放出一股操控之力，使佩戴者可以远程操控机关装置。这种操控不是魔法的——它是机械的，是精确的。据说这枚戒指来自一位机关傀儡师大师，他用这枚戒指同时操控了十二只机关傀儡。\n\n这枚戒指是机关傀儡师在「操控室」中，从自己的操控装置上取下的碎片铸造的。他说："这枚戒指让我能够借用操控的力量。但操控是有代价的——它会消耗你的精神。"\n\n戒指的效果是：每日一次，可以释放「傀儡操控」——操控一个 30 尺内的机关装置或构造体，使其为你战斗 2 回合（如同 dominate monster，但仅对 construct 有效，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "karakuri_puppeteer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_karakuri_puppeteer_ring_1" }
]
```

---

### 78.8 机关傀儡师齿轮之戒（Karakuri Puppeteer Gear Ring）

```gdscript
item_id = "acc_karakuri_puppeteer_ring_2"
display_name = "机关傀儡师齿轮之戒"
description = "一枚由齿轮石与机关银铸造的戒指，戒指表面刻有微型齿轮图案。当佩戴者集中精神时，戒指会释放出一股齿轮之力，使佩戴者的动作更加精准和快速。这种力量不是魔法的——它是机械的，是精确的。据说这枚戒指来自一位机关傀儡师大师，他用这枚戒指在一秒钟内完成了三十六个机关动作。\n\n这枚戒指是机关傀儡师在「齿轮工坊」中，从自己的齿轮装置上取下的碎片铸造的。他说："这枚戒指让我能够借用齿轮的力量。齿轮是机械的灵魂——它让我能够精确地控制一切。"\n\n戒指的效果是：灵巧检定 +2（齿轮精准），且每日一次，可以释放「齿轮加速」——bonus action 获得一个额外动作（齿轮超速），但下一回合无法使用 bonus action（齿轮冷却）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "karakuri_puppeteer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_karakuri_puppeteer_ring_2" }
]
```

---

### 78.9 机关傀儡师机关傀儡（Karakuri Puppeteer Karakuri Puppet）

```gdscript
item_id = "acc_karakuri_puppeteer_trinket"
display_name = "机关傀儡师机关傀儡"
description = "一个由机关与魔法制成的微型傀儡，傀儡表面覆盖着精美的机关装置。这个傀儡不是普通的玩具——它是活的。当佩戴者需要时，可以激活傀儡，让它为自己战斗。傀儡可以变形为不同的形态：人形、兽形、甚至是武器。但傀儡需要维护——如果不定期上发条，它会停止运作。\n\n这个傀儡是机关傀儡师在「机关工坊」中，从自己的第一个作品上取下的部分制成的。他说："这个傀儡是我的第一个孩子。虽然它不完整，但它会永远保护我。"\n\n傀儡的效果是：每日一次，可以召唤「机关傀儡」——傀儡可以变形为以下一种形态，持续 1 分钟：\n- 人形：HP 25，AC 15，攻击 +5，1D8+3 bludgeoning\n- 兽形：HP 20，AC 16，攻击 +6，1D10+4 slashing，移动力 40 尺\n- 武器形：可以作为武器使用（+3，2D6+4 slashing/piercing/bludgeoning，可选择）\n可以用 bonus action 指挥傀儡变形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "karakuri_puppeteer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_karakuri_puppeteer_trinket" }
]
```

---

### 78.10 机关傀儡师徽章（Karakuri Puppeteer Badge）

```gdscript
item_id = "acc_karakuri_puppeteer_badge"
display_name = "机关傀儡师徽章"
description = "一枚由机关金与齿轮石铸造的徽章，徽章上刻着一把钥匙和一个齿轮——机关傀儡师的标志。徽章背面刻着一行小字：「机关即生命，生命即机关。」这枚徽章是机关傀儡师公会的信物，拥有它意味着你已被机关之道认可。\n\n徽章的效果是：10 尺内所有友方 AC +1（机关守护）。且每日一次，可以释放「机关领域」——10 尺半径内所有友方获得「机关护甲」2 回合：AC +2，且受到攻击时有 25% 概率触发「机关反击」——对攻击者造成 1D8 piercing（机关刺针）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "karakuri_puppeteer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_karakuri_puppeteer_badge" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_karakuri_puppeteer_badge" }
]
```

---

## 套装七十九：雷神之鼓（Raijin Drum）饰品

> *"雷神之鼓不是乐器——它是雷霆的语言，是天空的咆哮。"*

---

### 79.5 雷神之鼓雷云披风（Raijin Drum Thunder Cloud Cloak）

```gdscript
item_id = "acc_raijin_drum_cloak"
display_name = "雷神之鼓雷云披风"
description = "一件由雷云丝与闪电布编织而成的披风，披风表面不断有微型闪电在闪烁和跳动——不是装饰，而是真正的雷电被封印在了丝线中。当穿戴者愤怒时，披风会自动释放出雷电，攻击周围的敌人。据说这件披风是雷神亲手编织的，每一根丝线都来自不同的雷云。\n\n这件披风是雷神之鼓在「雷云之峰」中，从自己的雷云中取下的碎片编织的。他说："这件披风让我能够将雷电的力量带到任何地方。它可以攻击，也可以防御；可以照亮，也可以毁灭。"\n\n披风的效果是：lightning 抗性 +15（雷云之躯），且每日一次，可以释放「雷电披风」——10 尺半径内所有敌人每回合开始时受到 2D6 lightning（雷电环绕），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "raijin_drum_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_lightning", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_raijin_drum_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_raijin_drum_cloak" }
]
```

---

### 79.6 雷神之鼓雷电之珠项链（Raijin Drum Thunder Pearl Necklace）

```gdscript
item_id = "acc_raijin_drum_necklace"
display_name = "雷神之鼓雷电之珠项链"
description = "一条由雷电金与雷云丝编织而成的项链，吊坠是一颗雷电之珠——一颗由纯粹雷电凝结的宝珠。宝珠内部不断有微型闪电在闪烁，如同一个小型的雷暴。据说这颗宝珠来自雷神的心脏，他在创造这颗宝珠时，将自己的一部分力量封印在了其中。\n\n这条项链是雷神之鼓在「雷神殿」中，从雷神的祭坛上取出的。他说："这颗宝珠承载了我的雷电之力。它让我能够召唤雷电，也让我能够理解天空的语言。"\n\n项链的效果是：自然检定 +3（雷电知识），且每日一次，可以释放「雷电之珠」——指定一个 30 尺内的敌人，受到 4D10 lightning（DC17 敏捷豁免 half），且 stunned 1 回合（雷电麻痹）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "raijin_drum_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_raijin_drum_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_raijin_drum_necklace" }
]
```

---

### 79.7 雷神之鼓雷鸣之戒（Raijin Drum Thunder Ring）

```gdscript
item_id = "acc_raijin_drum_ring_1"
display_name = "雷神之鼓雷鸣之戒"
description = "一枚由雷鸣石与雷电金铸造的戒指，戒指表面刻有微型雷鸣图案。当佩戴者集中精神时，戒指会释放出一股雷鸣之力，使佩戴者的攻击附带雷电。这种雷电不是普通的闪电——它是雷神的雷电，可以穿透任何防御。\n\n这枚戒指是雷神之鼓在「雷鸣深渊」中，从雷鸣恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用雷神的力量。但雷电是无情的——它不会区分敌我。"\n\n戒指的效果是：所有攻击附加 1D6 lightning（雷鸣），且每日一次，可以释放「雷鸣爆发」——指定一个 15 尺内的敌人，受到 3D10 lightning（DC16 敏捷豁免 half），且 10 尺内所有敌人受到 2D6 lightning（雷鸣连锁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "raijin_drum_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raijin_drum_ring_1" }
]
```

---

### 79.8 雷神之鼓鼓点之戒（Raijin Drum Drumbeat Ring）

```gdscript
item_id = "acc_raijin_drum_ring_2"
display_name = "雷神之鼓鼓点之戒"
description = "一枚由鼓点石与雷电银铸造的戒指，戒指表面刻有微型鼓面图案。当佩戴者集中精神时，戒指会释放出一股鼓点之力，使周围的节奏与佩戴者的心跳同步。这种同步不是普通的——它可以增强盟友的力量，也可以扰乱敌人的节奏。\n\n这枚戒指是雷神之鼓在「鼓点神殿」中，从雷神的鼓面上取下的碎片铸造的。他说："这枚戒指让我能够借用鼓点的力量。节奏是宇宙的语言——学会倾听它。"\n\n戒指的效果是：每日一次，可以释放「雷神鼓点」——10 尺内所有友方攻击检定 +2（节奏同步），且 10 尺内所有敌人攻击检定 -1（节奏紊乱），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "raijin_drum_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_raijin_drum_ring_2" }
]
```

---

### 79.9 雷神之鼓迷你雷神鼓（Raijin Drum Mini Raijin Drum）

```gdscript
item_id = "acc_raijin_drum_trinket"
display_name = "雷神之鼓迷你雷神鼓"
description = "一个由雷神皮与雷电金制成的微型鼓，鼓面由雷神的皮肤制成，鼓身由雷电木制成。这个鼓不是普通的乐器——它是活的。当佩戴者敲击它时，会召唤雷电，攻击敌人或治愈盟友。但鼓有自己的意志——它会选择自己的节奏，有时会拒绝佩戴者的命令。\n\n这个鼓是雷神之鼓在「雷神殿」中，从雷神的鼓上取下的碎片制成的。他说："这个鼓承载了我的雷电之力。但它也让我越来越难以控制自己的情绪——鼓的节奏会影响我的心跳。"\n\n鼓的效果是：每日一次，可以敲击雷神鼓——选择一个效果：\n- 雷之鼓：15 尺半径内所有敌人受到 3D10 lightning（DC16 敏捷豁免 half）且 stunned 1 回合\n- 电之鼓：10 尺内所有友方恢复 2D10 HP（雷电治愈）\n- 风之鼓：10 尺内所有友方移动力 +15 尺（雷电加速）\n- 雨之鼓：10 尺半径内所有敌人攻击检定 -2（雷电干扰），持续 2 回合"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "raijin_drum_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_raijin_drum_trinket" }
]
```

---

### 79.10 雷神之鼓徽章（Raijin Drum Badge）

```gdscript
item_id = "acc_raijin_drum_badge"
display_name = "雷神之鼓徽章"
description = "一枚由雷电金与雷云石铸造的徽章，徽章上刻着一面鼓和一道闪电——雷神之鼓的标志。徽章背面刻着一行小字：「雷鸣九霄，鼓震三界。」这枚徽章是雷神公会的信物，拥有它意味着你已被雷神认可。\n\n徽章的效果是：10 尺内所有友方 lightning 抗性 +5（雷电庇护）。且每日一次，可以释放「雷电领域」——10 尺半径内所有敌人每回合开始时受到 1D6 lightning（雷电环绕），且友方在领域内攻击检定 +1（雷电之力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "raijin_drum_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raijin_drum_badge" },
    { attribute_id = "performance_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raijin_drum_badge" }
]
```

---

## 套装八十：河童铠甲（Kappa Armor）饰品

> *"河童不是水鬼——他是水的朋友，只是他的友谊有时候...比较湿。"*

---

### 80.5 河童铠甲荷叶披风（Kappa Armor Lotus Leaf Cloak）

```gdscript
item_id = "acc_kappa_armor_cloak"
display_name = "河童铠甲荷叶披风"
description = "一件由荷叶与水流丝编织而成的披风，披风表面不断有微型水滴在流动和滴落——不是装饰，而是真正的水被封印在了丝线中。当穿戴者接触水时，披风会自动吸收水分，使穿戴者更加轻盈。据说这件披风是河童亲手编织的，每一片荷叶都来自不同的池塘。\n\n这件披风是河童铠甲在「河童之池」中，从自己的荷叶上取下的碎片编织的。他说："这件披风让我能够将水的力量带到任何地方。它可以保护我，也可以治愈我——只要我有水。"\n\n披风的效果是：在水中呼吸（荷叶之息），且在水中移动力 +10 尺（水之流畅）。当在水中时，每回合开始时恢复 1D8 HP（水之治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "kappa_armor_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_kappa_armor_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_kappa_armor_cloak" }
]
```

---

### 80.6 河童铠甲水之玉项链（Kappa Armor Water Jade Necklace）

```gdscript
item_id = "acc_kappa_armor_necklace"
display_name = "河童铠甲水之玉项链"
description = "一条由水之玉与荷叶丝编织而成的项链，吊坠是一颗水之玉——一颗由纯粹水凝结的宝玉。宝玉内部不断有微型水流在旋转，如同一个小型的漩涡。据说这颗宝玉来自河童的头顶，河童将头顶的「水盘」力量封印在了其中。\n\n这条项链是河童铠甲在「河童之池」中，从自己的水盘上取下的碎片制成的。他说："这颗宝玉承载了我的水之力量。它让我能够控制水，也让我能够与水沟通。"\n\n项链的效果是：自然检定 +3（水之知识），且每日一次，可以释放「水之玉」——指定一个 20 尺内的敌人，受到 3D10 cold（DC16 体质豁免 half）且移动力 -10 尺 2 回合（水之束缚）。或者指定一个友方，恢复 3D8 HP（水之治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "kappa_armor_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_kappa_armor_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_kappa_armor_necklace" }
]
```

---

### 80.7 河童铠甲水之戒（Kappa Armor Water Ring）

```gdscript
item_id = "acc_kappa_armor_ring_1"
display_name = "河童铠甲水之戒"
description = "一枚由水之石与荷叶金铸造的戒指，戒指表面刻有微型水流图案。当佩戴者集中精神时，戒指会释放出一股水之力，使佩戴者可以控制水。这种控制不是魔法的——它是自然的，是水的延伸。据说这枚戒指来自一位河童长老，他用这枚戒指控制了一条河流的流向。\n\n这枚戒指是河童铠甲在「水之深渊」中，从河童长老的遗骸上取下的。他说："这枚戒指让我能够借用水的力量。水是生命之源——学会尊重它。"\n\n戒指的效果是：在水中完全隐形（水之隐匿），且每日一次，可以释放「水流冲击」——指定一个 15 尺内的敌人，受到 3D8 bludgeoning（水流冲击，DC15 力量豁免 half）且 knocked prone（水流推倒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "kappa_armor_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_kappa_armor_ring_1" }
]
```

---

### 80.8 河童铠甲黄瓜之戒（Kappa Armor Cucumber Ring）

```gdscript
item_id = "acc_kappa_armor_ring_2"
display_name = "河童铠甲黄瓜之戒"
description = "一枚由黄瓜石与荷叶银铸造的戒指，戒指表面刻有微型黄瓜图案。这枚戒指看起来滑稽——但它不是玩笑。黄瓜是河童最爱的食物，也是河童力量的来源。据说这枚戒指来自一位河童长老，他用这枚戒指将黄瓜的力量提升到了前所未有的境界。\n\n这枚戒指是河童铠甲在「黄瓜田」中，从河童长老的收藏中取出的。他说："这枚戒指让我能够借用黄瓜的力量。不要笑——黄瓜是神圣的。"\n\n戒指的效果是：每日一次，可以 bonus action 吃一根魔法黄瓜——恢复 2D8 HP（黄瓜治愈），且下一回合 AC +2（黄瓜护盾，因为河童吃黄瓜时会变得极其满足和防御）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "kappa_armor_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_kappa_armor_ring_2" }
]
```

---

### 80.9 河童铠甲河童之盘（Kappa Armor Kappa Plate）

```gdscript
item_id = "acc_kappa_armor_trinket"
display_name = "河童铠甲河童之盘"
description = "一个由荷叶与魔法制成的微型水盘，水盘表面不断有微型水流在旋转。这个水盘不是普通的盘子——它是活的。当佩戴者需要时，可以激活水盘，控制周围的水。水盘可以制造水、吸收水、甚至将水转化为武器。但水盘需要水——如果在干燥环境中，水盘会失去力量。\n\n这个水盘是河童铠甲在「河童之池」中，从自己的头顶取下的。他说："这个水盘承载了我的水之力量。它让我能够控制水，也让我能够理解水的语言。"\n\n水盘的效果是：每日一次，可以释放「水盘之力」——选择一个效果：\n- 水之盾：10 尺内一个友方获得 water shield 2 回合：免疫 fire，且受到 fire 攻击时恢复 1D8 HP\n- 水之刃：制造一把水之刃（+3，2D6+4 cold/slashing），持续 2 回合\n- 水之牢：指定一个 15 尺内的敌人，restrained 2 回合（DC16 力量豁免挣脱）\n- 水之愈：10 尺内所有友方恢复 2D8 HP"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "kappa_armor_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_kappa_armor_trinket" }
]
```

---

### 80.10 河童铠甲徽章（Kappa Armor Badge）

```gdscript
item_id = "acc_kappa_armor_badge"
display_name = "河童铠甲徽章"
description = "一枚由荷叶金与河童石铸造的徽章，徽章上刻着一片荷叶和一个水盘——河童铠甲的标志。徽章背面刻着一行小字：「水即生命，生命即水。」这枚徽章是河童公会的信物，拥有它意味着你已被水认可。\n\n徽章的效果是：10 尺内所有友方在水中 AC +1（水之庇护）。且每日一次，可以释放「水之领域」——10 尺半径内所有敌人移动力 -5 尺（水之束缚），且友方在领域内每回合开始时恢复 1D6 HP（水之治愈），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "kappa_armor_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_kappa_armor_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_kappa_armor_badge" }
]
```

---

*套装 71–80 饰品部分完结 · 共 60 件饰品装备*

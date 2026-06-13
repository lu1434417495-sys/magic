# 传奇装备套装饰品设计文档（套装 91–100）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装九十一：创世泰坦（Creation Titan）饰品

> *"创世泰坦不是在创造世界——他只是在为世界提供一个开始，而世界自己决定了如何结束。"*

---

### 91.5 创世泰坦星辰披风（Creation Titan Star Cloak）

```gdscript
item_id = "acc_creation_titan_cloak"
display_name = "创世泰坦星辰披风"
description = "一件由星辰丝与创世布编织而成的披风，披风表面不断有微型星辰在诞生和毁灭——不是装饰，而是真正的宇宙被封印在了丝线中。当穿戴者移动时，星辰会自动排列成星座，为穿戴者指引方向。据说这件披风是创世泰坦在创造第一个宇宙时，从宇宙边缘取下的碎片编织的。\n\n这件披风是创世泰坦在「创世神殿」中，从自己的创世之力中取下的碎片编织的。他说："这件披风承载了我的创世之力。每一颗星辰都是一个世界，每一个世界都有一个故事。"\n\n披风的效果是：force 抗性 +20（创世之躯），且每日一次，可以释放「星辰陨落」——指定一个 30 尺内的点，10 尺半径内所有敌人受到 4D10 force（DC18 敏捷豁免 half）且 knocked prone（星辰冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "creation_titan_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_creation_titan_cloak" },
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_creation_titan_cloak" }
]
```

---

### 91.6 创世泰坦创世之核项链（Creation Titan Creation Core Necklace）

```gdscript
item_id = "acc_creation_titan_necklace"
display_name = "创世泰坦创世之核项链"
description = "一条由创世金与星辰水晶编织而成的项链，吊坠是一颗创世之核——一颗由纯粹创世之力凝结的核心。核心内部不断有微型宇宙在诞生和毁灭，如同一个永恒的创世循环。据说这颗核心来自创世泰坦的心脏，他在创造万物时，将自己的一部分力量封印在了其中。\n\n这条项链是创世泰坦在「创世之源」中，从自己的心脏位置取出的。他说："这颗核心承载了我的创世之力。它让我能够创造，也让我能够理解毁灭。"\n\n项链的效果是：宗教检定 +4（创世知识），且每日一次，可以释放「创世之光」——10 尺内所有友方恢复 4D8 HP（创世治愈），且移除所有非 legendary 负面状态（创世净化）。或者释放「创世之锤」——指定一个敌人，受到 4D10 force（DC18 体质豁免 half）且 stunned 1 回合（创世冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "creation_titan_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_creation_titan_necklace" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_creation_titan_necklace" }
]
```

---

### 91.7 创世泰坦创造之戒（Creation Titan Creation Ring）

```gdscript
item_id = "acc_creation_titan_ring_1"
display_name = "创世泰坦创造之戒"
description = "一枚由创造石与创世金铸造的戒指，戒指表面刻有微型创造图案。当佩戴者集中精神时，戒指会释放出一股创造之力，使佩戴者可以从虚无中创造出物质。这种创造不是幻术——它是真实的，是创世之力的延伸。据说这枚戒指来自创世泰坦本人，他用这枚戒指创造了第一个世界。\n\n这枚戒指是创世泰坦在「创造深渊」中，从自己的创造之力中取下的碎片铸造的。他说："这枚戒指让我能够借用创造的力量。创造不是无中生有——它是从可能性中提取现实。"\n\n戒指的效果是：每日一次，可以释放「创造」——在 10 尺内创造一个物体或生物（由 DM 决定）：\n- 创造一面石墙：10 尺 × 10 尺，AC 17，HP 50，持续 1 分钟\n- 创造一把武器：+3，2D8+4 force/slashing，持续 2 回合\n- 创造一个元素生物：HP 30，AC 14，攻击 +5，2D6+3 force，持续 1 分钟"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "creation_titan_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_creation_titan_ring_1" }
]
```

---

### 91.8 创世泰坦毁灭之戒（Creation Titan Destruction Ring）

```gdscript
item_id = "acc_creation_titan_ring_2"
display_name = "创世泰坦毁灭之戒"
description = "一枚由毁灭石与创世银铸造的戒指，戒指表面刻有微型毁灭图案。当佩戴者集中精神时，戒指会释放出一股毁灭之力，使目标从存在中抹去。这种毁灭不是死亡——它是从现实中删除，是让目标从未存在过。据说这枚戒指来自创世泰坦本人，他在毁灭一个失败的宇宙时，将自己的一部分力量封印在了其中。\n\n这枚戒指是创世泰坦在「毁灭深渊」中，从自己的毁灭之力中取下的碎片铸造的。他说："这枚戒指让我能够借用毁灭的力量。毁灭不是结束——它是新的开始。"\n\n戒指的效果是：每日一次，可以释放「毁灭之触」——指定一个 10 尺内的敌人，其受到 5D10 force（DC18 体质豁免 half），且如果 HP 降至 0，其尸体被从现实中抹去（无法复活，直至 wish/true resurrection）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "creation_titan_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_creation_titan_ring_2" }
]
```

---

### 91.9 创世泰坦创世之种（Creation Titan Creation Seed）

```gdscript
item_id = "acc_creation_titan_trinket"
display_name = "创世泰坦创世之种"
description = "一颗由创世之力凝结的种子，种子表面不断有微型宇宙在诞生和毁灭。这颗种子不是普通的种子——它是活的。当佩戴者需要时，可以激活种子，创造出一个微型世界。这个世界可以保护佩戴者，也可以攻击敌人。但种子需要能量——如果不定期滋养，它会枯萎。\n\n这颗种子是创世泰坦在「创世之源」中，从自己的创世之力中取下的核心制成的。他说："这颗种子承载了我的创世之力。它不是给凡人的礼物——它是给创造者的责任。"\n\n种子的效果是：每日一次，可以释放「微型世界」——在 15 尺内创造一个 10 尺半径的微型世界，持续 2 回合：\n- 友方在微型世界内：AC +2，每回合恢复 2D6 HP\n- 敌人在微型世界内：移动力减半，每回合受到 2D6 force\n- 微型世界结束时，所有敌人须通过 DC17 体质豁免，失败则 knocked prone"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "creation_titan_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_creation_titan_trinket" }
]
```

---

### 91.10 创世泰坦徽章（Creation Titan Badge）

```gdscript
item_id = "acc_creation_titan_badge"
display_name = "创世泰坦徽章"
description = "一枚由创世金与星辰石铸造的徽章，徽章上刻着一个星球和一个锤子——创世泰坦的标志。徽章背面刻着一行小字：「创造即毁灭，毁灭即创造。」这枚徽章是创世公会的信物，拥有它意味着你已被创世之力认可。\n\n徽章的效果是：10 尺内所有友方 force 抗性 +5（创世庇护）。且每日一次，可以释放「创世领域」——10 尺半径内友方 AC +2（创世守护），且每回合开始时恢复 1D8 HP（创世滋养），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "creation_titan_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_creation_titan_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_creation_titan_badge" }
]
```

---

## 套装九十二：虚空吞噬者（Void Devourer）饰品

> *"虚空吞噬者不是在饥饿——他只是让一切回归应有的状态：虚无。"*

---

### 92.5 虚空吞噬者虚空披风（Void Devourer Void Cloak）

```gdscript
item_id = "acc_void_devourer_cloak"
display_name = "虚空吞噬者虚空披风"
description = "一件由虚空丝与虚无布编织而成的披风，披风表面不断有微型虚空在吞噬和膨胀——不是装饰，而是真正的虚空被封印在了丝线中。当穿戴者移动时，披风会自动吞噬周围的光线，使穿戴者陷入绝对的黑暗。在黑暗中，只有穿戴者能看到——因为虚空吞噬者本身就是黑暗。\n\n这件披风是虚空吞噬者在「虚空深渊」中，从自己的虚空中取下的碎片编织的。他说："这件披风承载了我的虚空之力。它不是黑暗——它是虚无，是一切存在之前的宁静。"\n\n披风的效果是：force 抗性 +20（虚空之躯），且在黑暗中完全隐形（如同 greater invisibility）。当穿戴者被攻击时，有 25% 概率触发「虚空吞噬」——攻击被虚空吞噬，免疫该次伤害。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "void_devourer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_void_devourer_cloak" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_void_devourer_cloak" }
]
```

---

### 92.6 虚空吞噬者虚无之核项链（Void Devourer Void Core Necklace）

```gdscript
item_id = "acc_void_devourer_necklace"
display_name = "虚空吞噬者虚无之核项链"
description = "一条由虚无金与虚空水晶编织而成的项链，吊坠是一颗虚无之核——一颗由纯粹虚空凝结的核心。核心内部不断有微型虚空在吞噬和膨胀，如同一个小型的黑洞。据说这颗核心来自虚空吞噬者的心脏，他在吞噬第一个宇宙时，将自己的一部分力量封印在了其中。\n\n这条项链是虚空吞噬者在「虚空之源」中，从自己的心脏位置取出的。他说："这颗核心承载了我的虚空之力。它让我能够吞噬一切，也让我能够理解虚无的美丽。"\n\n项链的效果是：奥秘检定 +4（虚空知识），且每日一次，可以释放「虚空吞噬」——指定一个 20 尺内的敌人，其受到 5D10 necrotic（DC18 体质豁免 half），且 max HP 降低 15 直至长休（虚空侵蚀）。或者释放「虚无治愈」——自身恢复 4D8 HP（虚空吞噬周围的能量）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "void_devourer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_void_devourer_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_void_devourer_necklace" }
]
```

---

### 92.7 虚空吞噬者吞噬之戒（Void Devourer Devour Ring）

```gdscript
item_id = "acc_void_devourer_ring_1"
display_name = "虚空吞噬者吞噬之戒"
description = "一枚由吞噬石与虚无金铸造的戒指，戒指表面刻有微型吞噬图案。当佩戴者集中精神时，戒指会释放出一股吞噬之力，将目标拉向虚空。这种吞噬不是物理的——它是虚空的，可以穿透任何防御，将目标从现实中抹去。据说这枚戒指来自虚空吞噬者本人，他用这枚戒指吞噬了一个星系。\n\n这枚戒指是虚空吞噬者在「吞噬深渊」中，从自己的吞噬之力中取下的碎片铸造的。他说："这枚戒指让我能够借用吞噬的力量。吞噬不是贪婪——它是宇宙的循环，是一切回归虚无的方式。"\n\n戒指的效果是：每日一次，可以释放「虚空之口」——指定一个 15 尺内的敌人，其受到 4D10 necrotic（DC17 体质豁免 half），且被拉向佩戴者 10 尺（虚空引力）。如果拉到 5 尺内，额外受到 2D10 necrotic（虚空撕咬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "void_devourer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_devourer_ring_1" }
]
```

---

### 92.8 虚空吞噬者湮灭之戒（Void Devourer Annihilation Ring）

```gdscript
item_id = "acc_void_devourer_ring_2"
display_name = "虚空吞噬者湮灭之戒"
description = "一枚由湮灭石与虚无银铸造的戒指，戒指表面刻有微型湮灭图案。当佩戴者集中精神时，戒指会释放出一股湮灭之力，使目标与反物质接触，瞬间湮灭。这种湮灭不是爆炸——它是从存在中完全消失，不留任何痕迹。据说这枚戒指来自虚空吞噬者本人，他用这枚戒指湮灭了一个维度。\n\n这枚戒指是虚空吞噬者在「湮灭深渊」中，从自己的湮灭之力中取下的碎片铸造的。他说："这枚戒指让我能够借用湮灭的力量。湮灭不是暴力——它是最终的和平。"\n\n戒指的效果是：每日一次，可以释放「湮灭之触」——指定一个 5 尺内的敌人，其受到 6D10 force（DC18 体质豁免 half）。如果该攻击击杀目标，目标被完全湮灭，不留任何痕迹（无法复活，直至 wish/true resurrection）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "void_devourer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_devourer_ring_2" }
]
```

---

### 92.9 虚空吞噬者虚空之眼（Void Devourer Void Eye）

```gdscript
item_id = "acc_void_devourer_trinket"
display_name = "虚空吞噬者虚空之眼"
description = "一只由虚空与虚无制成的眼睛，眼睛表面不断有微型虚空在吞噬和膨胀。这只眼睛不是普通的眼睛——它是活的。当佩戴者需要时，可以激活眼睛，释放出虚空的凝视。被凝视的目标会感受到虚空的召唤，他们的灵魂会被虚空吸引，最终被吞噬。但眼睛有自己的意志——它会试图吞噬佩戴者的灵魂。\n\n这只眼睛是虚空吞噬者在「虚空之源」中，从自己的脸上取下的。他说："这只眼睛承载了我的虚空之力。它让我能够窥视虚空，也让我能够理解虚无的真谛。但它也让我越来越难以感受到生命的温暖。"\n\n眼睛的效果是：每日一次，可以释放「虚空凝视」——指定一个 20 尺内的敌人，其受到 4D10 necrotic（DC17 智慧豁免 half）且 frightened 2 回合（虚空恐惧）。如果目标 HP 低于 25%，直接被吞噬（即死效果，DC18 体质豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "void_devourer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_void_devourer_trinket" }
]
```

---

### 92.10 虚空吞噬者徽章（Void Devourer Badge）

```gdscript
item_id = "acc_void_devourer_badge"
display_name = "虚空吞噬者徽章"
description = "一枚由虚无金与虚空石铸造的徽章，徽章上刻着一个黑洞和一个漩涡——虚空吞噬者的标志。徽章背面刻着一行小字：「一切归于虚无，虚无孕育一切。」这枚徽章是虚空公会的信物，拥有它意味着你已被虚空认可。\n\n徽章的效果是：10 尺内所有友方 necrotic 抗性 +5（虚空庇护）。且每日一次，可以释放「虚空领域」——10 尺半径内所有敌人每回合开始时受到 1D8 necrotic（虚空侵蚀），且移动力 -5 尺（虚空引力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "void_devourer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_devourer_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_devourer_badge" }
]
```

---

## 套装九十三：圣光仲裁者（Holy Arbiter）饰品

> *"圣光仲裁者不是在审判——他只是在执行早已写好的律法，而律法从不偏袒任何人。"*

---

### 93.5 圣光仲裁者圣光披风（Holy Arbiter Holy Light Cloak）

```gdscript
item_id = "acc_holy_arbiter_cloak"
display_name = "圣光仲裁者圣光披风"
description = "一件由圣光丝与神圣布编织而成的披风，披风表面不断有微型圣光在流动和闪耀——不是装饰，而是真正的神圣之力被封印在了丝线中。当穿戴者行走时，圣光会自动照亮周围，驱散黑暗和邪恶。在邪恶面前，披风会释放出耀眼的光芒，使邪恶生物无法直视。\n\n这件披风是圣光仲裁者在「圣光神殿」中，从自己的神圣之力中取下的碎片编织的。他说："这件披风承载了我的圣光之力。它不是武器——它是律法，是秩序，是永恒的光明。"\n\n披风的效果是：radiant 抗性 +20（圣光之躯），且在黑暗中 30 尺半径内提供 bright light（圣光照明）。10 尺内 undead 和 demon 攻击检定 -2（圣光压制）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "holy_arbiter_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_holy_arbiter_cloak" },
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_holy_arbiter_cloak" }
]
```

---

### 93.6 圣光仲裁者天秤项链（Holy Arbiter Scale Necklace）

```gdscript
item_id = "acc_holy_arbiter_necklace"
display_name = "圣光仲裁者天秤项链"
description = "一条由神圣金与正义水晶编织而成的项链，吊坠是一个微型天秤——这个天秤不是普通的秤，它是真正的正义之秤。天秤的两端永远不会失衡——因为圣光仲裁者的判决永远是公正的。当佩戴者面对不公时，天秤会自动倾斜，指引佩戴者找到真相。\n\n这条项链是圣光仲裁者在「正义神殿」中，从正义之神的祭坛上取出的。他说："这个天秤承载了我的正义之力。它让我能够分辨善恶，也让我能够执行律法。"\n\n项链的效果是：洞察检定 +4（正义之眼），且每日一次，可以释放「神圣审判」——指定一个 20 尺内的敌人，其受到 4D10 radiant（DC18 智慧豁免 half）。如果目标在过去 24 小时内犯下过 evil 行为，伤害翻倍（正义制裁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "holy_arbiter_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_holy_arbiter_necklace" },
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_holy_arbiter_necklace" }
]
```

---

### 93.7 圣光仲裁者制裁之戒（Holy Arbiter Sanction Ring）

```gdscript
item_id = "acc_holy_arbiter_ring_1"
display_name = "圣光仲裁者制裁之戒"
description = "一枚由制裁石与神圣金铸造的戒指，戒指表面刻有微型制裁图案。当佩戴者集中精神时，戒指会释放出一股制裁之力，使目标的邪恶行为受到惩罚。这种制裁不是物理的——它是神圣的，是律法的延伸。据说这枚戒指来自圣光仲裁者本人，他用这枚戒指制裁了一位堕落的神明。\n\n这枚戒指是圣光仲裁者在「制裁深渊」中，从自己的制裁之力中取下的碎片铸造的。他说："这枚戒指让我能够借用制裁的力量。制裁不是报复——它是恢复秩序的方式。"\n\n戒指的效果是：对 undead 和 demon 攻击检定 +2（神圣制裁），且每日一次，可以释放「神圣制裁」——指定一个 15 尺内的 evil 敌人，其受到 4D10 radiant + 2D10 force（DC17 智慧豁免 half），且 blinded 2 回合（圣光致盲）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "holy_arbiter_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_holy_arbiter_ring_1" }
]
```

---

### 93.8 圣光仲裁者宽恕之戒（Holy Arbiter Forgiveness Ring）

```gdscript
item_id = "acc_holy_arbiter_ring_2"
display_name = "圣光仲裁者宽恕之戒"
description = "一枚由宽恕石与神圣银铸造的戒指，戒指表面刻有微型宽恕图案。当佩戴者集中精神时，戒指会释放出一股宽恕之力，治愈目标的伤痛，赦免目标的罪过。这种宽恕不是软弱的——它是强大的，是神圣之力的最高形式。据说这枚戒指来自圣光仲裁者本人，他用这枚戒指宽恕了一位毁灭世界的恶魔。\n\n这枚戒指是圣光仲裁者在「宽恕深渊」中，从自己的宽恕之力中取下的碎片铸造的。他说："这枚戒指让我能够借用宽恕的力量。宽恕不是忘记——它是选择放下仇恨，选择重新开始。"\n\n戒指的效果是：每日一次，可以释放「神圣宽恕」——指定一个 10 尺内的友方，其恢复 4D10 HP（神圣治愈），且移除所有 curse、disease、poison（神圣净化）。如果目标是 undead，则改为受到 4D10 radiant（神圣净化之焰）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "holy_arbiter_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_holy_arbiter_ring_2" }
]
```

---

### 93.9 圣光仲裁者圣典（Holy Arbiter Holy Tome）

```gdscript
item_id = "acc_holy_arbiter_trinket"
display_name = "圣光仲裁者圣典"
description = "一本由神圣之力凝结的微型书籍，书页上写满了神圣的律法和判决。这本圣典不是普通的书籍——它是活的。当佩戴者需要时，可以翻开圣典，找到相应的律法，释放神圣的力量。但圣典是公正的——它不会偏袒任何人，包括佩戴者。\n\n这本圣典是圣光仲裁者在「律法神殿」中，从自己的知识中取出的核心制成的。他说："这本圣典承载了我的律法之力。它让我能够执行正义，也让我能够理解秩序的真谛。"\n\n圣典的效果是：每日一次，可以翻开圣典——选择一个律法：\n- 制裁律法：指定一个 evil 敌人，受到 5D10 radiant（DC18 智慧豁免 half）且 stunned 1 回合（律法震击）\n- 保护律法：10 尺内所有友方获得 20 点临时 HP（律法护盾）且 AC +2（律法守护）\n- 审判律法：指定一个敌人，其所有属性在 2 回合内降至 10（DC17 智慧豁免抵抗）\n- 救赎律法：指定一个友方，其恢复至 max HP（律法救赎），但佩戴者失去 20 HP（律法代价）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "holy_arbiter_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_holy_arbiter_trinket" }
]
```

---

### 93.10 圣光仲裁者徽章（Holy Arbiter Badge）

```gdscript
item_id = "acc_holy_arbiter_badge"
display_name = "圣光仲裁者徽章"
description = "一枚由神圣金与正义石铸造的徽章，徽章上刻着一个天秤和一把剑——圣光仲裁者的标志。徽章背面刻着一行小字：「律法无情，正义永恒。」这枚徽章是圣光公会的信物，拥有它意味着你已被神圣律法认可。\n\n徽章的效果是：10 尺内所有友方 radiant 抗性 +5（圣光庇护）。且每日一次，可以释放「律法领域」——10 尺半径内 evil 敌人攻击检定 -2（律法压制），且友方攻击检定 +1（律法鼓舞），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "holy_arbiter_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_holy_arbiter_badge" },
    { attribute_id = "insight_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_holy_arbiter_badge" }
]
```

---

## 套装九十四：死亡使者（Death Herald）饰品

> *"死亡使者不是在带来死亡——他只是比死亡早到一步，然后安静地等待。"*

---

### 94.5 死亡使者死亡披风（Death Herald Death Cloak）

```gdscript
item_id = "acc_death_herald_cloak"
display_name = "死亡使者死亡披风"
description = "一件由死亡丝与虚无布编织而成的披风，披风表面不断有微型灵魂在飘动和消散——不是装饰，而是真正的死亡被封印在了丝线中。当穿戴者行走时，披风会自动吸收周围的生机，使周围的花朵枯萎，昆虫死去。在濒死者面前，披风会变得更加沉重，仿佛承载了无数的灵魂。\n\n这件披风是死亡使者在「死亡殿堂」中，从自己的死亡之力中取下的碎片编织的。他说："这件披风承载了我的死亡之力。它不是诅咒——它是礼物，是解脱，是永恒的安宁。"\n\n披风的效果是：necrotic 抗性 +20（死亡之躯），且可以感知 30 尺内濒死生物（HP ≤ 10% max）。当击杀一个生物时，恢复 2D8 HP（灵魂吞噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "death_herald_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_necrotic", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_death_herald_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_death_herald_cloak" }
]
```

---

### 94.6 死亡使者灵魂沙漏项链（Death Herald Soul Hourglass Necklace）

```gdscript
item_id = "acc_death_herald_necklace"
display_name = "死亡使者灵魂沙漏项链"
description = "一条由死亡金与灵魂水晶编织而成的项链，吊坠是一个微型沙漏——这个沙漏不是普通的计时器，它是真正的灵魂沙漏。沙漏中的沙子不是普通的沙子——它是灵魂的碎片，每一粒沙子都代表一个逝去的生命。当沙漏倒转时，佩戴者可以短暂地操纵时间，加速或延缓死亡的到来。\n\n这条项链是死亡使者在「死亡之源」中，从死亡之神的祭坛上取出的。他说："这个沙漏承载了我的死亡之力。它让我能够看见生命的尽头，也让我能够决定何时到来。"\n\n项链的效果是：宗教检定 +4（死亡知识），且每日一次，可以释放「沙漏倒转」——指定一个 15 尺内的友方，其恢复 4D8 HP（时间回溯），或指定一个敌人，其受到 4D10 necrotic（时间加速，DC17 体质豁免 half）且老化 10 年（外观变化，无游戏效果）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "death_herald_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_death_herald_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_herald_necklace" }
]
```

---

### 94.7 死亡使者收割之戒（Death Herald Reap Ring）

```gdscript
item_id = "acc_death_herald_ring_1"
display_name = "死亡使者收割之戒"
description = "一枚由收割石与死亡金铸造的戒指，戒指表面刻有微型镰刀图案。当佩戴者集中精神时，戒指会释放出一股收割之力，使佩戴者的攻击如同死神的镰刀。这种收割不是物理的——它是死亡的，可以穿透任何防御，直接收割灵魂。据说这枚戒指来自死亡使者本人，他用这枚戒指收割了一个王国的灵魂。\n\n这枚戒指是死亡使者在「收割深渊」中，从自己的收割之力中取下的碎片铸造的。他说："这枚戒指让我能够借用收割的力量。收割不是杀戮——它是收获，是生命循环的一部分。"\n\n戒指的效果是：对 HP 低于 25% 的敌人攻击检定 +3（收割之刃），且每日一次，可以释放「灵魂收割」——指定一个 10 尺内的敌人，其受到 4D10 necrotic（DC17 体质豁免 half）。如果该攻击击杀目标，佩戴者恢复 max HP 的 20%（灵魂吞噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "death_herald_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_herald_ring_1" }
]
```

---

### 94.8 死亡使者永眠之戒（Death Herald Eternal Sleep Ring）

```gdscript
item_id = "acc_death_herald_ring_2"
display_name = "死亡使者永眠之戒"
description = "一枚由永眠石与死亡银铸造的戒指，戒指表面刻有微型永眠图案。当佩戴者集中精神时，戒指会释放出一股永眠之力，使目标陷入永恒的睡眠。这种睡眠不是普通的 sleep——它是死亡的预演，是灵魂的安息。据说这枚戒指来自死亡使者本人，他用这枚戒指让一位暴君在梦中安详地死去。\n\n这枚戒指是死亡使者在「永眠深渊」中，从自己的永眠之力中取下的碎片铸造的。他说："这枚戒指让我能够借用永眠的力量。永眠不是死亡——它是休息，是准备，是新的开始。"\n\n戒指的效果是：每日一次，可以释放「永眠」——指定一个 15 尺内的敌人，其陷入 sleep 2 回合（DC17 智慧豁免抵抗）。如果目标在睡眠中被攻击，攻击自动暴击（永眠之脆弱）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "death_herald_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_herald_ring_2" }
]
```

---

### 94.9 死亡使者死神镰刀（Death Herald Scythe Charm）

```gdscript
item_id = "acc_death_herald_trinket"
display_name = "死亡使者死神镰刀"
description = "一把由死亡之力凝结的微型镰刀，镰刀表面不断有微型灵魂在流动和消散。这把镰刀不是普通的武器——它是活的。当佩戴者需要时，可以激活镰刀，释放出死神的收割。这把镰刀可以收割灵魂，可以切割命运，甚至可以斩断时间。但镰刀有自己的意志——它会选择自己的收割对象，有时会违背佩戴者的意愿。\n\n这把镰刀是死亡使者在「死亡之源」中，从自己的收割之力中取下的核心制成的。他说："这把镰刀承载了我的死亡之力。它让我能够收割灵魂，也让我能够理解死亡的真谛。但它也让我越来越难以感受到生命的温暖。"\n\n镰刀的效果是：每日一次，可以释放「死神收割」——15 尺锥形，所有敌人受到 5D10 necrotic（DC18 敏捷豁免 half）。HP 低于 25% 的敌人直接死亡（DC18 体质豁免抵抗即死）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "death_herald_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_death_herald_trinket" }
]
```

---

### 94.10 死亡使者徽章（Death Herald Badge）

```gdscript
item_id = "acc_death_herald_badge"
display_name = "死亡使者徽章"
description = "一枚由死亡金与灵魂石铸造的徽章，徽章上刻着一把镰刀和一个沙漏——死亡使者的标志。徽章背面刻着一行小字：「死亡是终点，也是起点。」这枚徽章是死亡公会的信物，拥有它意味着你已被死亡认可。\n\n徽章的效果是：10 尺内所有友方 necrotic 抗性 +5（死亡庇护）。且每日一次，可以释放「死亡领域」——10 尺半径内所有敌人每回合开始时受到 1D8 necrotic（死亡侵蚀），且友方击杀敌人时恢复 1D8 HP（灵魂分享），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "death_herald_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_herald_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_herald_badge" }
]
```

---

## 套装九十五：星辰编织者（Star Weaver）饰品

> *"星辰编织者不是在编织星星——他是在编织命运，而每一颗星星都是一个选择。"*

---

### 95.5 星辰编织者星图披风（Star Weaver Star Chart Cloak）

```gdscript
item_id = "acc_star_weaver_cloak"
display_name = "星辰编织者星图披风"
description = "一件由星图丝与宇宙布编织而成的披风，披风表面不断有微型星辰在流动和排列——不是装饰，而是真正的星图被封印在了丝线中。当穿戴者需要时，披风会自动显示当前的星图，指引穿戴者找到正确的方向。据说这件披风是星辰编织者在创造第一个星座时，从宇宙边缘取下的碎片编织的。\n\n这件披风是星辰编织者在「星辰神殿」中，从自己的星辰之力中取下的碎片编织的。他说："这件披风承载了我的星辰之力。每一颗星星都是一个故事，每一个星座都是一个命运。"\n\n披风的效果是：在夜晚可以感知 60 尺内所有生物的位置（星辰之眼），且不会在野外迷路（星辰指引）。每日一次，可以释放「星辰陨落」——指定一个 30 尺内的点，10 尺半径内所有敌人受到 3D10 radiant（DC17 敏捷豁免 half）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "star_weaver_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_star_weaver_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_weaver_cloak" }
]
```

---

### 95.6 星辰编织者命运之星项链（Star Weaver Fate Star Necklace）

```gdscript
item_id = "acc_star_weaver_necklace"
display_name = "星辰编织者命运之星项链"
description = "一条由星辰金与命运水晶编织而成的项链，吊坠是一颗命运之星——一颗由纯粹命运之力凝结的星辰。星辰内部不断有微型命运线在交织和分离，如同一个永恒的编织过程。据说这颗星辰来自命运女神的眼泪，她在编织第一个命运时，将自己的一部分力量封印在了其中。\n\n这条项链是星辰编织者在「命运神殿」中，从命运女神的祭坛上取出的。她说："这颗星辰承载了我的命运之力。它让我能够看见命运的线，也让我能够编织新的命运。"\n\n项链的效果是：奥秘检定 +4（星辰知识），且每日一次，可以释放「命运编织」——指定一个 20 尺内的友方，其下一回合攻击自动命中且暴击（命运眷顾），或指定一个敌人，其下一回合攻击自动 miss（命运抛弃）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "star_weaver_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_star_weaver_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_weaver_necklace" }
]
```

---

### 95.7 星辰编织者星光之戒（Star Weaver Starlight Ring）

```gdscript
item_id = "acc_star_weaver_ring_1"
display_name = "星辰编织者星光之戒"
description = "一枚由星光石与星辰金铸造的戒指，戒指表面刻有微型星光图案。当佩戴者集中精神时，戒指会释放出一股星光之力，使佩戴者的攻击如同星辰般闪耀。这种力量不是魔法的——它是宇宙的，是星辰的延伸。据说这枚戒指来自星辰编织者本人，她用这枚戒指将星光编织成了武器。\n\n这枚戒指是星辰编织者在「星光深渊」中，从自己的星光之力中取下的碎片铸造的。她说："这枚戒指让我能够借用星光的力量。星光是宇宙的礼物——它穿越了无数光年，只为照亮你。"\n\n戒指的效果是：在夜晚攻击检定 +2（星光之力），且每日一次，可以释放「星光爆发」——指定一个 20 尺内的敌人，其受到 4D10 radiant（DC17 敏捷豁免 half）且 blinded 2 回合（星光致盲）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "star_weaver_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_ring_1" }
]
```

---

### 95.8 星辰编织者星座之戒（Star Weaver Constellation Ring）

```gdscript
item_id = "acc_star_weaver_ring_2"
display_name = "星辰编织者星座之戒"
description = "一枚由星座石与星辰银铸造的戒指，戒指表面刻有微型星座图案。当佩戴者集中精神时，戒指会释放出一股星座之力，召唤星座的力量。每个星座都有不同的能力：有的增强力量，有的增强智慧，有的增强魅力。据说这枚戒指来自星辰编织者本人，她用这枚戒指召唤了十二星座的力量。\n\n这枚戒指是星辰编织者在「星座深渊」中，从自己的星座之力中取下的碎片铸造的。她说："这枚戒指让我能够借用星座的力量。星座是宇宙的密码——学会了阅读它们，就学会了宇宙的秘密。"\n\n戒指的效果是：每日一次，可以选择一个星座，获得其力量 2 回合：\n- 白羊座：力量 +4，攻击检定 +2\n- 金牛座：体质 +4，AC +2\n- 双子座：敏捷 +4，可以 bonus action 进行一次额外攻击\n- 巨蟹座：智慧 +4，AC +1 且每回合恢复 1D8 HP\n- 狮子座：魅力 +4，攻击伤害 +2D6\n- 处女座：智力 +4，法术 DC +2"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "star_weaver_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_weaver_ring_2" }
]
```

---

### 95.9 星辰编织者星辰纺锤（Star Weaver Star Spindle）

```gdscript
item_id = "acc_star_weaver_trinket"
display_name = "星辰编织者星辰纺锤"
description = "一个由星辰之力凝结的微型纺锤，纺锤表面不断有微型星辰在诞生和毁灭。这个纺锤不是普通的工具——它是活的。当佩戴者需要时，可以激活纺锤，编织出新的星辰。这些星辰可以攻击敌人，也可以保护盟友。但纺锤有自己的意志——它会选择自己的编织图案，有时会与佩戴者的意愿相悖。\n\n这个纺锤是星辰编织者在「星辰之源」中，从自己的星辰之力中取下的核心制成的。她说："这个纺锤承载了我的星辰之力。它让我能够编织命运，也让我能够理解宇宙的编织方式。"\n\n纺锤的效果是：每日一次，可以释放「星辰编织」——在 20 尺内编织出 3 颗星辰（持续 2 回合）：\n- 每颗星辰可以攻击一个敌人（+6，2D8+4 radiant）\n- 或保护一个友方（AC +2，免疫 radiant）\n- 或治愈一个友方（2D8 HP）\n可以用 bonus action 指挥每颗星辰行动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "star_weaver_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_star_weaver_trinket" }
]
```

---

### 95.10 星辰编织者徽章（Star Weaver Badge）

```gdscript
item_id = "acc_star_weaver_badge"
display_name = "星辰编织者徽章"
description = "一枚由星辰金与命运石铸造的徽章，徽章上刻着一颗星星和一个纺锤——星辰编织者的标志。徽章背面刻着一行小字：「星辰即命运，命运即编织。」这枚徽章是星辰公会的信物，拥有它意味着你已被星辰认可。\n\n徽章的效果是：10 尺内所有友方在夜晚攻击检定 +1（星辰之力）。且每日一次，可以释放「星辰领域」——10 尺半径内友方 AC +1（星辰守护），且每回合开始时恢复 1D6 HP（星辰滋养），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "star_weaver_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_badge" }
]
```

---

## 套装九十六：命运编织者（Fate Weaver）饰品

> *"命运编织者不是在预测未来——他是在编织未来，而每一根线都是一个选择。"*

---

### 96.5 命运编织者命运披风（Fate Weaver Fate Cloak）

```gdscript
item_id = "acc_fate_weaver_cloak"
display_name = "命运编织者命运披风"
description = "一件由命运丝与时间布编织而成的披风，披风表面不断有微型命运线在交织和分离——不是装饰，而是真正的命运被封印在了丝线中。当穿戴者行走时，披风会自动显示周围的命运线，让穿戴者看到可能的未来。但这些未来不是确定的——它们是可能性，是选择，是等待被编织的线。\n\n这件披风是命运编织者在「命运神殿」中，从自己的命运之力中取下的碎片编织的。她说："这件披风承载了我的命运之力。每一根线都是一个选择，每一个选择都是一个未来。"\n\n披风的效果是：先攻检定 +3（命运预知），且免疫 surprised（命运洞察）。每日一次，可以释放「命运之线」——指定一个 20 尺内的敌人，其下一回合必须按照你的意愿行动（选择其攻击目标或移动方向，DC18 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "fate_weaver_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_fate_weaver_cloak" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_fate_weaver_cloak" }
]
```

---

### 96.6 命运编织者命运之轮项链（Fate Weaver Wheel of Fate Necklace）

```gdscript
item_id = "acc_fate_weaver_necklace"
display_name = "命运编织者命运之轮项链"
description = "一条由命运金与时间水晶编织而成的项链，吊坠是一个微型命运之轮——这个轮子不是普通的装饰品，它是真正的命运之轮。轮子不断转动，显示出不同的命运：财富、爱情、战争、死亡。当轮子停止时，佩戴者可以短暂地改变命运的走向。据说这个轮子来自命运女神，她在编织第一个命运时，将自己的一部分力量封印在了其中。\n\n这条项链是命运编织者在「命运之源」中，从命运女神的祭坛上取出的。她说："这个轮子承载了我的命运之力。它让我能够转动命运，也让我能够理解选择的重量。"\n\n项链的效果是：奥秘检定 +4（命运知识），且每日一次，可以转动命运之轮——随机获得一个命运效果（1D6）：\n- 1：10 尺内所有友方恢复 3D8 HP（命运之赐）\n- 2：10 尺内所有敌人受到 3D10 force（命运之击）\n- 3：自身获得 +3 AC 2 回合（命运之盾）\n- 4：自身攻击检定 +3 2 回合（命运之刃）\n- 5：指定一个敌人，其 stunned 1 回合（命运之绊）\n- 6：10 尺内所有友方获得 +2 所有检定 2 回合（命运之佑）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "fate_weaver_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_fate_weaver_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_fate_weaver_necklace" }
]
```

---

### 96.7 命运编织者命运之戒（Fate Weaver Fate Ring）

```gdscript
item_id = "acc_fate_weaver_ring_1"
display_name = "命运编织者命运之戒"
description = "一枚由命运石与命运金铸造的戒指，戒指表面刻有微型命运线图案。当佩戴者集中精神时，戒指会释放出一股命运之力，使目标的命运线变得可见。这些命运线不是装饰——它们是真实的，是命运的轨迹。佩戴者可以通过拉扯这些线，改变目标的命运。但改变命运是有代价的——每次改变，佩戴者自己的命运线也会受到影响。\n\n这枚戒指是命运编织者在「命运深渊」中，从自己的命运之力中取下的碎片铸造的。她说："这枚戒指让我能够借用命运的力量。命运不是注定的——它是可以被编织的。但每一次编织，都会留下痕迹。"\n\n戒指的效果是：每日一次，可以释放「命运编织」——指定一个 15 尺内的目标：\n- 如果是友方：其下一回合攻击检定有优势（命运眷顾）\n- 如果是敌人：其下一回合攻击检定有劣势（命运抛弃）\n- 如果是自己：可以重掷一次攻击或豁免（命运重织）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "fate_weaver_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_fate_weaver_ring_1" }
]
```

---

### 96.8 命运编织者宿命之戒（Fate Weaver Destiny Ring）

```gdscript
item_id = "acc_fate_weaver_ring_2"
display_name = "命运编织者宿命之戒"
description = "一枚由宿命石与命运银铸造的戒指，戒指表面刻有微型宿命图案。当佩戴者集中精神时，戒指会释放出一股宿命之力，使目标无法逃避自己的命运。这种宿命不是诅咒——它是命运的必然，是选择的后果。据说这枚戒指来自命运编织者本人，她用这枚戒指让一位暴君无法逃避自己的审判。\n\n这枚戒指是命运编织者在「宿命深渊」中，从自己的宿命之力中取下的碎片铸造的。她说："这枚戒指让我能够借用宿命的力量。宿命不是惩罚——它是结果，是责任，是成长的代价。"\n\n戒指的效果是：每日一次，可以释放「宿命锁定」——指定一个 15 尺内的敌人，其在 2 回合内无法使用 bonus action 或 reaction（宿命束缚，DC17 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "fate_weaver_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_fate_weaver_ring_2" }
]
```

---

### 96.9 命运编织者命运剪刀（Fate Weaver Fate Scissors）

```gdscript
item_id = "acc_fate_weaver_trinket"
display_name = "命运编织者命运剪刀"
description = "一把由命运之力凝结的微型剪刀，剪刀表面不断有微型命运线在被剪断和连接。这把剪刀不是普通的工具——它是活的。当佩戴者需要时，可以激活剪刀，剪断目标的命运线，或连接两个目标的命运线。但剪刀有自己的意志——它会选择自己的剪切对象，有时会违背佩戴者的意愿。\n\n这把剪刀是命运编织者在「命运之源」中，从自己的命运之力中取下的核心制成的。她说："这把剪刀承载了我的命运之力。它让我能够剪断命运，也让我能够连接命运。但每一次剪切，都是一次选择，都是一次责任。"\n\n剪刀的效果是：每日一次，可以释放「命运剪切」——选择一个效果：\n- 剪断：指定一个 15 尺内的敌人，其一种能力被封印 2 回合（DC17 智慧豁免抵抗）\n- 连接：链接两个 20 尺内的目标，2 回合内共享伤害和治愈（如同 quantum entanglement）\n- 重织：指定一个友方，其可以重掷一次失败的检定（命运重织）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "fate_weaver_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_fate_weaver_trinket" }
]
```

---

### 96.10 命运编织者徽章（Fate Weaver Badge）

```gdscript
item_id = "acc_fate_weaver_badge"
display_name = "命运编织者徽章"
description = "一枚由命运金与时间石铸造的徽章，徽章上刻着一根线和一把剪刀——命运编织者的标志。徽章背面刻着一行小字：「命运是线，选择是剪。」这枚徽章是命运公会的信物，拥有它意味着你已被命运认可。\n\n徽章的效果是：10 尺内所有友方先攻检定 +1（命运加速）。且每日一次，可以释放「命运领域」——10 尺半径内友方攻击检定 +1（命运眷顾），敌方攻击检定 -1（命运抛弃），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "fate_weaver_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_fate_weaver_badge" },
    { attribute_id = "insight_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_fate_weaver_badge" }
]
```

---

## 套装九十七：梦境之主（Dream Lord）饰品

> *"梦境之主不是在睡觉——他只是在另一个现实中醒着，而那个现实比这个更加真实。"*

---

### 97.5 梦境之主梦境披风（Dream Lord Dream Cloak）

```gdscript
item_id = "acc_dream_lord_cloak"
display_name = "梦境之主梦境披风"
description = "一件由梦境丝与幻境布编织而成的披风，披风表面不断有微型梦境在流动和变化——不是装饰，而是真正的梦境被封印在了丝线中。当穿戴者行走时，披风会自动释放出梦境之力，使周围的人产生幻觉。在睡眠中，披风会自动连接穿戴者与梦境世界，让穿戴者在梦中也能行动。\n\n这件披风是梦境之主在「梦境神殿」中，从自己的梦境之力中取下的碎片编织的。他说："这件披风承载了我的梦境之力。梦境不是虚幻——它是另一个现实，一个更加自由、更加真实的现实。"\n\n披风的效果是：免疫 sleep 和 charm（梦境之主），且每日一次，可以释放「梦境漫步」——进入梦境世界 1 回合：完全隐形且免疫所有伤害，可以指定一个 20 尺内的敌人，其陷入 sleep 2 回合（DC18 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dream_lord_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dream_lord_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_lord_cloak" }
]
```

---

### 97.6 梦境之主梦境之核项链（Dream Lord Dream Core Necklace）

```gdscript
item_id = "acc_dream_lord_necklace"
display_name = "梦境之主梦境之核项链"
description = "一条由梦境金与幻境水晶编织而成的项链，吊坠是一颗梦境之核——一颗由纯粹梦境凝结的核心。核心内部不断有微型梦境在诞生和毁灭，如同一个永恒的梦境循环。据说这颗核心来自梦境之神的大脑，他在创造第一个梦境时，将自己的一部分力量封印在了其中。\n\n这条项链是梦境之主在「梦境之源」中，从梦境之神的祭坛上取出的。他说："这颗核心承载了我的梦境之力。它让我能够进入任何梦境，也让我能够创造新的梦境。"\n\n项链的效果是：奥秘检定 +4（梦境知识），且每日一次，可以释放「梦境入侵」——指定一个 20 尺内的敌人，其陷入梦境幻觉 2 回合（stunned 1 回合，随后 attack 和 perception 检定有劣势 2 回合，DC18 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dream_lord_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_dream_lord_necklace" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_lord_necklace" }
]
```

---

### 97.7 梦境之主幻象之戒（Dream Lord Illusion Ring）

```gdscript
item_id = "acc_dream_lord_ring_1"
display_name = "梦境之主幻象之戒"
description = "一枚由幻象石与梦境金铸造的戒指，戒指表面刻有微型幻象图案。当佩戴者集中精神时，戒指会释放出一股幻象之力，创造出一个完全真实的幻象。这个幻象不是普通的 illusion——它是「真实幻象」，可以触摸，可以攻击，甚至可以施法。据说这枚戒指来自梦境之主本人，他用这枚戒指创造了一个幻象城市，存在了整整一百年。\n\n这枚戒指是梦境之主在「幻象深渊」中，从自己的幻象之力中取下的碎片铸造的。他说："这枚戒指让我能够借用幻象的力量。幻象不是欺骗——它是另一种现实。"\n\n戒指的效果是：每日一次，可以释放「真实幻象」——创造一个完全相同的分身（HP = 你的 max HP × 50%，AC = 你的 AC），持续 2 回合。分身可以进行攻击（+6，2D6+4 force），且可以 cast 一个你拥有的法术（消耗你的 mana）。敌人无法分辨真假（DC19 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dream_lord_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_lord_ring_1" }
]
```

---

### 97.8 梦境之主梦魇之戒（Dream Lord Nightmare Ring）

```gdscript
item_id = "acc_dream_lord_ring_2"
display_name = "梦境之主梦魇之戒"
description = "一枚由梦魇石与梦境银铸造的戒指，戒指表面刻有微型梦魇图案。当佩戴者集中精神时，戒指会释放出一股梦魇之力，使目标陷入最深的恐惧。这种梦魇不是普通的恐惧——它是梦境的延伸，是潜意识的暴露。据说这枚戒指来自梦境之主本人，他用这枚戒指让一位国王在梦中经历了千年的恐怖。\n\n这枚戒指是梦境之主在「梦魇深渊」中，从自己的梦魇之力中取下的碎片铸造的。他说："这枚戒指让我能够借用梦魇的力量。梦魇不是惩罚——它是镜子，让你看见自己最深的恐惧。"\n\n戒指的效果是：每日一次，可以释放「梦魇」——指定一个 15 尺内的敌人，其 frightened 2 回合（DC17 智慧豁免抵抗），且每回合开始时受到 2D10 psychic（梦魇伤害）。如果目标在梦魇中 HP 降至 0，其陷入 coma 而非死亡（直至被唤醒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dream_lord_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_lord_ring_2" }
]
```

---

### 97.9 梦境之主梦境之镜（Dream Lord Dream Mirror）

```gdscript
item_id = "acc_dream_lord_trinket"
display_name = "梦境之主梦境之镜"
description = "一面由梦境之力凝结的镜子，镜面不断有微型梦境在流动和变化。这面镜子不是普通的镜子——它是活的。当佩戴者凝视它时，会看到自己的梦境：有的是过去的记忆，有的是未来的预兆，有的是从未存在的幻想。据说这面镜子来自梦境之主本人，他用这面镜子穿越了无数个梦境世界。\n\n这面镜子是梦境之主在「梦境之源」中，从自己的梦境之力中取下的核心制成的。他说："这面镜子承载了我的梦境之力。它让我能够窥视梦境，也让我能够穿越梦境。但它也让我越来越难以分辨哪个是现实。"\n\n镜子的效果是：每日一次，可以释放「梦境穿越」——进入一个目标的梦境（15 尺内），在梦境中攻击其精神：目标受到 4D10 psychic（DC18 智慧豁免 half）且 stunned 1 回合（梦境震荡）。如果目标在梦境中 HP 降至 0，其陷入 sleep 2 回合（梦境囚禁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dream_lord_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_dream_lord_trinket" }
]
```

---

### 97.10 梦境之主徽章（Dream Lord Badge）

```gdscript
item_id = "acc_dream_lord_badge"
display_name = "梦境之主徽章"
description = "一枚由梦境金与幻境石铸造的徽章，徽章上刻着一个月亮和一个眼睛——梦境之主的标志。徽章背面刻着一行小字：「梦境即现实，现实即梦境。」这枚徽章是梦境公会的信物，拥有它意味着你已被梦境认可。\n\n徽章的效果是：10 尺内所有友方免疫 sleep 和 charm（梦境庇护）。且每日一次，可以释放「梦境领域」——10 尺半径内敌人攻击检定 -2（梦境干扰），且友方攻击检定 +1（梦境清晰），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dream_lord_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_lord_badge" },
    { attribute_id = "deception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_lord_badge" }
]
```

---

## 套装九十八：时间旅者（Time Traveler）饰品

> *"时间旅者不是在穿越时间——他是在纠正时间的错误，而时间总是犯很多错误。"*

---

### 98.5 时间旅者时间披风（Time Traveler Time Cloak）

```gdscript
item_id = "acc_time_traveler_cloak"
display_name = "时间旅者时间披风"
description = "一件由时间丝与时空布编织而成的披风，披风表面不断有微型时间流在流动和扭曲——不是装饰，而是真正的时间被封印在了丝线中。当穿戴者移动时，披风会自动调整时间的流速，使穿戴者的动作更加快速或缓慢。据说这件披风是时间旅者在穿越第一个时间线时，从时间边缘取下的碎片编织的。\n\n这件披风是时间旅者在「时间神殿」中，从自己的时间之力中取下的碎片编织的。他说："这件披风承载了我的时间之力。时间不是线性的——它是一个网，一个循环，一个可以被穿越的维度。"\n\n披风的效果是：先攻检定 +4（时间加速），且每日一次，可以释放「时间扭曲」——指定一个 15 尺内的敌人，其 slowed 2 回合（时间减速：移动力减半，攻击检定有劣势，DC18 体质豁免抵抗），或指定一个友方，其获得 haste 1 回合（不需要 concentration）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "time_traveler_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_time_traveler_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_time_traveler_cloak" }
]
```

---

### 98.6 时间旅者时间之核项链（Time Traveler Time Core Necklace）

```gdscript
item_id = "acc_time_traveler_necklace"
display_name = "时间旅者时间之核项链"
description = "一条由时间金与时空水晶编织而成的项链，吊坠是一颗时间之核——一颗由纯粹时间凝结的核心。核心内部不断有微型时间在流动和倒转，如同一个永恒的时间循环。据说这颗核心来自时间之神的心脏，他在创造时间时，将自己的一部分力量封印在了其中。\n\n这条项链是时间旅者在「时间之源」中，从时间之神的祭坛上取出的。他说："这颗核心承载了我的时间之力。它让我能够穿越时间，也让我能够理解时间的本质。"\n\n项链的效果是：奥秘检定 +4（时间知识），且每日一次，可以释放「时间回溯」——将自身或一个 10 尺内的友方状态恢复至上一回合开始时（HP、mana、状态效果），但位置不变。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "time_traveler_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "acc_time_traveler_necklace" },
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_time_traveler_necklace" }
]
```

---

### 98.7 时间旅者加速之戒（Time Traveler Acceleration Ring）

```gdscript
item_id = "acc_time_traveler_ring_1"
display_name = "时间旅者加速之戒"
description = "一枚由加速石与时间金铸造的戒指，戒指表面刻有微型加速图案。当佩戴者集中精神时，戒指会释放出一股加速之力，使佩戴者的时间流速加快。在这种加速状态下，佩戴者可以在一瞬间完成多个动作。但加速是有代价的——每次使用，佩戴者会老化。\n\n这枚戒指是时间旅者在「加速深渊」中，从自己的加速之力中取下的碎片铸造的。他说："这枚戒指让我能够借用加速的力量。加速不是作弊——它只是利用了时间的缝隙。"\n\n戒指的效果是：每日一次，可以释放「时间加速」——获得一个额外回合（时间加速），但 aging 1 年（外观变化，无游戏效果）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "time_traveler_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_time_traveler_ring_1" }
]
```

---

### 98.8 时间旅者减速之戒（Time Traveler Deceleration Ring）

```gdscript
item_id = "acc_time_traveler_ring_2"
display_name = "时间旅者减速之戒"
description = "一枚由减速石与时间银铸造的戒指，戒指表面刻有微型减速图案。当佩戴者集中精神时，戒指会释放出一股减速之力，使目标的时间流速减慢。在这种减速状态下，目标的行动变得极其缓慢，仿佛被困在了琥珀中。据说这枚戒指来自时间旅者本人，他用这枚戒指让一支军队在时间中停滞了一千年。\n\n这枚戒指是时间旅者在「减速深渊」中，从自己的减速之力中取下的碎片铸造的。他说："这枚戒指让我能够借用减速的力量。减速不是惩罚——它是保护，是观察，是理解。"\n\n戒指的效果是：每日一次，可以释放「时间停滞」——指定一个 15 尺内的敌人，其 stunned 1 回合（时间停滞，DC18 体质豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "time_traveler_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_time_traveler_ring_2" }
]
```

---

### 98.9 时间旅者时间沙漏（Time Traveler Hourglass）

```gdscript
item_id = "acc_time_traveler_trinket"
display_name = "时间旅者时间沙漏"
description = "一个由时间之力凝结的微型沙漏，沙漏中的沙子不是普通的沙子——它们是时间的碎片。当佩戴者翻转沙漏时，可以短暂地操控时间：加速、减速、甚至倒流。但沙漏中的沙子是有限的——每次使用，沙子会减少。当沙子耗尽时，沙漏会停止，时间也会停止。\n\n这个沙漏是时间旅者在「时间之源」中，从自己的时间之力中取下的核心制成的。他说："这个沙漏承载了我的时间之力。它让我能够操控时间，也让我能够理解时间的宝贵。"\n\n沙漏的效果是：每日一次，可以翻转沙漏——选择一个时间效果：\n- 加速：自身获得 haste 2 回合（不需要 concentration）\n- 减速：指定一个 20 尺内的敌人，其 slowed 2 回合（DC17 体质豁免抵抗）\n- 倒流：将自身状态恢复至 2 回合前\n- 冻结：指定一个 15 尺内的敌人，其 stunned 1 回合（DC18 体质豁免抵抗）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "time_traveler_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_time_traveler_trinket" }
]
```

---

### 98.10 时间旅者徽章（Time Traveler Badge）

```gdscript
item_id = "acc_time_traveler_badge"
display_name = "时间旅者徽章"
description = "一枚由时间金与时空石铸造的徽章，徽章上刻着一个沙漏和一个箭头——时间旅者的标志。徽章背面刻着一行小字：「时间是环，旅行者即编织者。」这枚徽章是时间公会的信物，拥有它意味着你已被时间认可。\n\n徽章的效果是：10 尺内所有友方先攻检定 +1（时间加速）。且每日一次，可以释放「时间领域」——10 尺半径内友方攻击检定 +1（时间加速），敌方攻击检定 -1（时间减速），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "time_traveler_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_time_traveler_badge" },
    { attribute_id = "history_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_time_traveler_badge" }
]
```

---

## 套装九十九：宇宙吞噬者（Cosmic Devourer）饰品

> *"宇宙吞噬者不是在饥饿——他只是让一切回归应有的状态：一个奇点。"*

---

### 99.5 宇宙吞噬者宇宙披风（Cosmic Devourer Cosmic Cloak）

```gdscript
item_id = "acc_cosmic_devourer_cloak"
display_name = "宇宙吞噬者宇宙披风"
description = "一件由宇宙丝与虚空布编织而成的披风，披风表面不断有微型宇宙在诞生和毁灭——不是装饰，而是真正的宇宙被封印在了丝线中。当穿戴者移动时，披风会自动吞噬周围的光线，使周围陷入黑暗。在黑暗中，披风会释放出宇宙的背景辐射，照亮周围的一切。\n\n这件披风是宇宙吞噬者在「宇宙边缘」中，从自己的吞噬之力中取下的碎片编织的。他说："这件披风承载了我的宇宙之力。它不是黑暗——它是宇宙的起点，也是终点。"\n\n披风的效果是：force 抗性 +25（宇宙之躯），且在黑暗中 15 尺半径内提供 dim light（宇宙辐射）。每日一次，可以释放「宇宙吞噬」——指定一个 20 尺内的点，15 尺半径内所有敌人受到 5D10 force（DC18 敏捷豁免 half），且被拉向中心 15 尺（宇宙引力）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "cosmic_devourer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_cosmic_devourer_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_cosmic_devourer_cloak" }
]
```

---

### 99.6 宇宙吞噬者奇点项链（Cosmic Devourer Singularity Necklace）

```gdscript
item_id = "acc_cosmic_devourer_necklace"
display_name = "宇宙吞噬者奇点项链"
description = "一条由奇点金与宇宙水晶编织而成的项链，吊坠是一个微型奇点——一个由纯粹引力凝结的核心。奇点内部不断有微型宇宙在被吞噬和压缩，如同一个永恒的毁灭循环。据说这个奇点来自宇宙吞噬者的心脏，他在吞噬第一个宇宙时，将自己的一部分力量封印在了其中。\n\n这条项链是宇宙吞噬者在「奇点之源」中，从自己的心脏位置取出的。他说："这个奇点承载了我的宇宙之力。它让我能够吞噬一切，也让我能够理解宇宙的命运。"\n\n项链的效果是：奥秘检定 +5（宇宙知识），且每日一次，可以释放「奇点爆发」——指定一个 20 尺内的点，20 尺半径内所有敌人受到 6D10 force（DC19 敏捷豁免 half），且被拉向中心 20 尺（奇点引力）。中心点的敌人受到双倍伤害且 stunned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "cosmic_devourer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_cosmic_devourer_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_cosmic_devourer_necklace" }
]
```

---

### 99.7 宇宙吞噬者引力之戒（Cosmic Devourer Gravity Ring）

```gdscript
item_id = "acc_cosmic_devourer_ring_1"
display_name = "宇宙吞噬者引力之戒"
description = "一枚由引力石与奇点金铸造的戒指，戒指表面刻有微型引力图案。当佩戴者集中精神时，戒指会释放出一股引力之力，创造出一个微型黑洞。这个黑洞不是真正的黑洞——它只是引力的极端，可以吞噬光线、物质、甚至时间。据说这枚戒指来自宇宙吞噬者本人，他用这枚戒指吞噬了一颗恒星。\n\n这枚戒指是宇宙吞噬者在「引力深渊」中，从自己的引力之力中取下的碎片铸造的。他说："这枚戒指让我能够借用引力的力量。引力是宇宙的纽带——它连接了一切，也束缚了一切。"\n\n戒指的效果是：每日一次，可以释放「微型黑洞」——指定一个 20 尺内的点，15 尺半径内所有敌人被拉向中心 15 尺（DC18 力量豁免抵抗），且受到 4D10 force（黑洞撕裂）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "cosmic_devourer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cosmic_devourer_ring_1" }
]
```

---

### 99.8 宇宙吞噬者坍缩之戒（Cosmic Devourer Collapse Ring）

```gdscript
item_id = "acc_cosmic_devourer_ring_2"
display_name = "宇宙吞噬者坍缩之戒"
description = "一枚由坍缩石与奇点银铸造的戒指，戒指表面刻有微型坍缩图案。当佩戴者集中精神时，戒指会释放出一股坍缩之力，使目标从内部坍缩。这种坍缩不是物理的——它是引力的，是宇宙的法则。据说这枚戒指来自宇宙吞噬者本人，他用这枚戒指让一个星系在瞬间坍缩成了黑洞。\n\n这枚戒指是宇宙吞噬者在「坍缩深渊」中，从自己的坍缩之力中取下的碎片铸造的。他说："这枚戒指让我能够借用坍缩的力量。坍缩不是毁灭——它是重生，是宇宙的循环。"\n\n戒指的效果是：每日一次，可以释放「宇宙坍缩」——指定一个 15 尺内的敌人，其受到 5D10 force（DC18 体质豁免 half），且 max HP 降低 20 直至长休（细胞坍缩）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "cosmic_devourer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cosmic_devourer_ring_2" }
]
```

---

### 99.9 宇宙吞噬者宇宙之口（Cosmic Devourer Cosmic Maw）

```gdscript
item_id = "acc_cosmic_devourer_trinket"
display_name = "宇宙吞噬者宇宙之口"
description = "一个由宇宙之力凝结的微型黑洞，黑洞表面不断有微型光线在被吞噬。这个黑洞不是普通的黑洞——它是活的。当佩戴者需要时，可以激活黑洞，释放出吞噬之力。这个黑洞可以吞噬任何东西：物质、能量、魔法、甚至时间。但黑洞是饥饿的——它会试图吞噬佩戴者。\n\n这个黑洞是宇宙吞噬者在「奇点之源」中，从自己的吞噬之力中取下的核心制成的。他说："这个黑洞承载了我的宇宙之力。它让我能够吞噬一切，也让我能够理解饥饿的真谛。但它也让我越来越难以满足。"\n\n黑洞的效果是：每日一次，可以释放「宇宙吞噬」——20 尺半径内所有敌人受到 6D10 force（DC19 敏捷豁免 half），且被拉向佩戴者 20 尺（宇宙引力）。被拉到 5 尺内的敌人额外受到 3D10 force（吞噬撕咬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "cosmic_devourer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_cosmic_devourer_trinket" }
]
```

---

### 99.10 宇宙吞噬者徽章（Cosmic Devourer Badge）

```gdscript
item_id = "acc_cosmic_devourer_badge"
display_name = "宇宙吞噬者徽章"
description = "一枚由奇点金与宇宙石铸造的徽章，徽章上刻着一个黑洞和一个漩涡——宇宙吞噬者的标志。徽章背面刻着一行小字：「一切归于奇点，奇点孕育一切。」这枚徽章是宇宙公会的信物，拥有它意味着你已被宇宙认可。\n\n徽章的效果是：10 尺内所有友方 force 抗性 +5（宇宙庇护）。且每日一次，可以释放「宇宙领域」——10 尺半径内所有敌人每回合开始时受到 1D10 force（宇宙侵蚀），且移动力 -5 尺（宇宙引力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "cosmic_devourer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cosmic_devourer_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cosmic_devourer_badge" }
]
```

---

## 套装一百：创世神（Creation Divine）饰品

> *"创世神不是在创造世界——他只是在让世界从可能性中成为现实，而可能性是无限的。"*

---

### 100.5 创世神创世披风（Creation Divine Creation Cloak）

```gdscript
item_id = "acc_creation_divine_cloak"
display_name = "创世神创世披风"
description = "一件由创世之力与神圣布编织而成的披风，披风表面不断有微型世界在诞生和毁灭——不是装饰，而是真正的创世之力被封印在了丝线中。当穿戴者行走时，披风会自动释放出创世之力，使周围的花朵绽放，伤口愈合，生命诞生。在毁灭面前，披风会释放出神圣的光芒，驱散一切邪恶。\n\n这件披风是创世神在「创世之源」中，从自己的创世之力中取下的碎片编织的。他说："这件披风承载了我的创世之力。它不是武器——它是生命，是爱，是无限的可能性。"\n\n披风的效果是：所有抗性 +10（创世之躯），且每日一次，可以释放「创世之光」——20 尺半径内所有友方恢复 4D10 HP（创世治愈），且移除所有非 legendary 负面状态（创世净化）。10 尺内 undead 和 demon 受到 4D10 radiant（创世制裁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "creation_divine_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_acid", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_radiant", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_force", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_psychic", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" },
    { attribute_id = "resistance_thunder", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_creation_divine_cloak" }
]
```

---

### 100.6 创世神生命之核项链（Creation Divine Life Core Necklace）

```gdscript
item_id = "acc_creation_divine_necklace"
display_name = "创世神生命之核项链"
description = "一条由创世金与生命水晶编织而成的项链，吊坠是一颗生命之核——一颗由纯粹生命之力凝结的核心。核心内部不断有微型生命在诞生和进化，如同一个永恒的创世循环。据说这颗核心来自创世神的心脏，他在创造第一个生命时，将自己的一部分力量封印在了其中。\n\n这条项链是创世神在「生命之源」中，从自己的心脏位置取出的。他说："这颗核心承载了我的生命之力。它让我能够创造生命，也让我能够理解爱的本质。"\n\n项链的效果是：宗教检定 +5（创世知识），且每日一次，可以释放「创世之息」——10 尺内一个死亡的友方复活（恢复至 max HP 的 50%，如同 revivify 但不需要材料），或 10 尺内所有友方恢复 5D10 HP（创世治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "creation_divine_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_creation_divine_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_creation_divine_necklace" }
]
```

---

### 100.7 创世神创造之戒（Creation Divine Creation Ring）

```gdscript
item_id = "acc_creation_divine_ring_1"
display_name = "创世神创造之戒"
description = "一枚由创造石与创世金铸造的戒指，戒指表面刻有微型创造图案。当佩戴者集中精神时，戒指会释放出一股创造之力，使佩戴者可以从虚无中创造出任何东西。这种创造不是幻术——它是真实的，是创世之力的最高形式。据说这枚戒指来自创世神本人，他用这枚戒指创造了第一个宇宙。\n\n这枚戒指是创世神在「创造之源」中，从自己的创造之力中取下的碎片铸造的。他说："这枚戒指让我能够借用创造的力量。创造不是无中生有——它是从无限的可能性中选择现实。"\n\n戒指的效果是：每日一次，可以释放「创世」——在 15 尺内创造一个物体、生物、或现象（由 DM 决定）：\n- 创造一座山：15 尺 × 15 尺的石墙，AC 20，HP 100，持续 1 分钟\n- 创造一条河：20 尺 × 20 尺的水域，敌人移动力减半\n- 创造一个元素：HP 40，AC 16，攻击 +6，3D8+5 force/elemental\n- 创造一片光：20 尺半径 bright light，undead 和 demon 受到 3D10 radiant/回合"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "creation_divine_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_creation_divine_ring_1" }
]
```

---

### 100.8 创世神毁灭之戒（Creation Divine Destruction Ring）

```gdscript
item_id = "acc_creation_divine_ring_2"
display_name = "创世神毁灭之戒"
description = "一枚由毁灭石与创世银铸造的戒指，戒指表面刻有微型毁灭图案。当佩戴者集中精神时，戒指会释放出一股毁灭之力，使目标从存在中完全抹去。这种毁灭不是死亡——它是从现实中删除，是让目标从未存在过。据说这枚戒指来自创世神本人，他在毁灭一个失败的宇宙时，将自己的一部分力量封印在了其中。\n\n这枚戒指是创世神在「毁灭之源」中，从自己的毁灭之力中取下的碎片铸造的。他说："这枚戒指让我能够借用毁灭的力量。毁灭不是结束——它是新的开始，是无限可能性的释放。"\n\n戒指的效果是：每日一次，可以释放「创世审判」——指定一个 20 尺内的敌人，其受到 6D10 force + 4D10 radiant（DC19 体质豁免 half）。如果该攻击击杀目标，目标被从现实中抹去（无法复活，直至 wish/true resurrection），且 10 尺半径内所有敌人受到 3D10 force（创世余波）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "creation_divine_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_creation_divine_ring_2" }
]
```

---

### 100.9 创世神创世之种（Creation Divine Creation Seed）

```gdscript
item_id = "acc_creation_divine_trinket"
display_name = "创世神创世之种"
description = "一颗由创世之力凝结的种子，种子表面不断有微型宇宙在诞生和毁灭。这颗种子不是普通的种子——它是活的。当佩戴者需要时，可以激活种子，创造出一个新的世界。这个世界可以保护佩戴者，也可以攻击敌人。但种子需要能量——如果不定期滋养，它会枯萎。\n\n这颗种子是创世神在「创世之源」中，从自己的创世之力中取下的核心制成的。他说："这颗种子承载了我的创世之力。它不是给凡人的礼物——它是给创造者的责任。"\n\n种子的效果是：每日一次，可以释放「新世界」——在 20 尺内创造一个 15 尺半径的新世界，持续 2 回合：\n- 友方在新世界内：AC +3，每回合恢复 3D8 HP，免疫所有非 legendary 负面状态\n- 敌人在新世界内：移动力减半，每回合受到 3D10 force，攻击检定 -2\n- 新世界结束时，所有敌人须通过 DC19 体质豁免，失败则 stunned 1 回合（创世震荡）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "creation_divine_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "acc_creation_divine_trinket" }
]
```

---

### 100.10 创世神徽章（Creation Divine Badge）

```gdscript
item_id = "acc_creation_divine_badge"
display_name = "创世神徽章"
description = "一枚由创世金与神圣石铸造的徽章，徽章上刻着一个星球和一道光芒——创世神的标志。徽章背面刻着一行小字：「创造即爱，爱即永恒。」这枚徽章是创世公会的至高信物，拥有它意味着你已被创世之力完全认可。\n\n徽章的效果是：10 尺内所有友方所有抗性 +3（创世庇护）。且每日一次，可以释放「创世领域」——15 尺半径内友方 AC +2（创世守护），每回合恢复 2D8 HP（创世滋养），且攻击检定 +2（创世之力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "creation_divine_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_creation_divine_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_creation_divine_badge" }
]
```

---

*套装 91–100 饰品部分完结 · 共 60 件饰品装备*

---

# 附录：套装 91–100 饰品汇总表

| 套装编号 | 套装名称 | 饰品件数 | 套装标签 |
|---------|---------|---------|---------|
| 91 | 创世泰坦 | 6 | `creation_titan_set` |
| 92 | 虚空吞噬者 | 6 | `void_devourer_set` |
| 93 | 圣光仲裁者 | 6 | `holy_arbiter_set` |
| 94 | 死亡使者 | 6 | `death_herald_set` |
| 95 | 星辰编织者 | 6 | `star_weaver_set` |
| 96 | 命运编织者 | 6 | `fate_weaver_set` |
| 97 | 梦境之主 | 6 | `dream_lord_set` |
| 98 | 时间旅者 | 6 | `time_traveler_set` |
| 99 | 宇宙吞噬者 | 6 | `cosmic_devourer_set` |
| 100 | 创世神 | 6 | `creation_divine_set` |

**本文件总计：60 件饰品装备**

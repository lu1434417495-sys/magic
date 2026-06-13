# 传奇装备套装饰品设计文档（套装 81–90）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装八十一：蒸汽骑士（Steam Knight）饰品

> *"蒸汽骑士不是在驾驶机器——他就是机器，而蒸汽是他的血液。"*

---

### 81.5 蒸汽骑士蒸汽披风（Steam Knight Steam Cloak）

```gdscript
item_id = "acc_steam_knight_cloak"
display_name = "蒸汽骑士蒸汽披风"
description = "一件由蒸汽管道与隔热丝编织而成的披风，披风表面不断有微型蒸汽从缝隙中喷出，发出嘶嘶的声音。这不是装饰——披风内部有一台微型蒸汽机，永恒地运转着。当穿戴者移动时，蒸汽机会加速运转，为穿戴者提供额外的力量。但当蒸汽机过热时，披风会释放出滚烫的蒸汽，烧伤周围的敌人。\n\n这件披风是蒸汽骑士在「蒸汽工坊」中，从自己的蒸汽机甲上取下的管道编织的。他说："这件披风是我的蒸汽之心。它让我能够在任何地方感受到蒸汽的力量。"\n\n披风的效果是：fire 抗性 +10（蒸汽隔热），且每回合开始时可以选择释放蒸汽——5 尺半径内所有敌人受到 1D6 fire（蒸汽灼伤），但下一回合移动力 -5 尺（蒸汽冷却）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "steam_knight_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_steam_knight_cloak" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_steam_knight_cloak" }
]
```

---

### 81.6 蒸汽骑士压力阀项链（Steam Knight Pressure Valve Necklace）

```gdscript
item_id = "acc_steam_knight_necklace"
display_name = "蒸汽骑士压力阀项链"
description = "一条由压力阀金与蒸汽管道编织而成的项链，吊坠是一个微型压力阀——这个压力阀不是装饰品，它是真正的机械装置。当穿戴者的压力（体力/精神）达到极限时，压力阀会自动释放，为穿戴者提供额外的力量。但当压力释放过多时，穿戴者会感到虚弱。\n\n这条项链是蒸汽骑士在「压力室」中，从自己的蒸汽机甲上取下的压力阀制成的。他说："这个压力阀让我能够控制自己的力量。压力不是敌人——它是动力。"\n\n项链的效果是：体质豁免 +2（压力坚韧），且当 HP 低于 25% 时，自动触发「压力释放」——恢复 3D8 HP（蒸汽爆发），但下一回合攻击检定 -2（压力耗尽）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "steam_knight_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_steam_knight_necklace" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_steam_knight_necklace" }
]
```

---

### 81.7 蒸汽骑士齿轮之戒（Steam Knight Gear Ring）

```gdscript
item_id = "acc_steam_knight_ring_1"
display_name = "蒸汽骑士齿轮之戒"
description = "一枚由齿轮与蒸汽金铸造的戒指，戒指表面不断有微型齿轮在转动。当佩戴者集中精神时，齿轮会加速转动，为佩戴者提供额外的力量。这种力量不是魔法的——它是机械的，是蒸汽的。据说这枚戒指来自一位蒸汽骑士大师，他用这枚戒指驱动了一台巨大的蒸汽机甲。\n\n这枚戒指是蒸汽骑士在「齿轮工坊」中，从自己的蒸汽机甲上取下的齿轮铸造的。他说："这枚戒指让我能够借用齿轮的力量。齿轮是机械的灵魂——它让我能够精确地控制一切。"\n\n戒指的效果是：力量检定 +2（齿轮之力），且每日一次，可以释放「齿轮加速」——bonus action 获得一个额外动作（齿轮超速），但下一回合无法使用 bonus action（齿轮冷却）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "steam_knight_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_steam_knight_ring_1" }
]
```

---

### 81.8 蒸汽骑士活塞之戒（Steam Knight Piston Ring）

```gdscript
item_id = "acc_steam_knight_ring_2"
display_name = "蒸汽骑士活塞之戒"
description = "一枚由活塞与蒸汽银铸造的戒指，戒指表面刻有微型活塞图案。当佩戴者集中精神时，戒指会释放出一股活塞之力，使佩戴者的攻击如同活塞般有力。这种力量不是魔法的——它是机械的，是物理的。据说这枚戒指来自一位蒸汽骑士大师，他用这枚戒指一拳打穿了一面城墙。\n\n这枚戒指是蒸汽骑士在「活塞工坊」中，从自己的蒸汽机甲上取下的活塞铸造的。他说："这枚戒指让我能够借用活塞的力量。活塞是力量的象征——它让我能够将蒸汽的力量转化为物理的力量。"\n\n戒指的效果是：徒手攻击变为 1D8 bludgeoning（活塞拳），且每日一次，可以释放「活塞冲击」——指定一个 5 尺内的敌人，受到 3D10 bludgeoning（活塞重击），目标须通过 DC16 力量豁免，失败则 knocked prone 并 stunned 1 回合（活塞震击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "steam_knight_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_steam_knight_ring_2" }
]
```

---

### 81.9 蒸汽骑士微型蒸汽机（Steam Knight Mini Steam Engine）

```gdscript
item_id = "acc_steam_knight_trinket"
display_name = "蒸汽骑士微型蒸汽机"
description = "一个由黄铜与魔法制成的微型蒸汽机，蒸汽机表面不断有微型齿轮在转动，微型活塞在推动。这个蒸汽机不是普通的玩具——它是活的。当佩戴者需要时，可以激活蒸汽机，为穿戴者提供额外的力量。但蒸汽机需要燃料——如果不定期添加水，它会停止运转。\n\n这个蒸汽机是蒸汽骑士在「蒸汽工坊」中，从自己的蒸汽机甲上取下的核心制成的。他说："这个蒸汽机是我的心脏。它让我能够在任何地方感受到蒸汽的力量。"\n\n蒸汽机的效果是：每日一次，可以激活「蒸汽超载」——力量 +4，攻击检定 +2，移动力 +10 尺，持续 2 回合。但结束后移动力 -10 尺 1 回合（蒸汽耗尽）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "steam_knight_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_steam_knight_trinket" }
]
```

---

### 81.10 蒸汽骑士徽章（Steam Knight Badge）

```gdscript
item_id = "acc_steam_knight_badge"
display_name = "蒸汽骑士徽章"
description = "一枚由蒸汽金与齿轮石铸造的徽章，徽章上刻着一台蒸汽机和一把剑——蒸汽骑士的标志。徽章背面刻着一行小字：「蒸汽即力量，力量即正义。」这枚徽章是蒸汽骑士公会的信物，拥有它意味着你已被蒸汽认可。\n\n徽章的效果是：10 尺内所有友方 fire 抗性 +5（蒸汽庇护）。且每日一次，可以释放「蒸汽领域」——10 尺半径内所有敌人每回合开始时受到 1D6 fire（蒸汽灼伤），且移动力 -5 尺（蒸汽迷雾），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "steam_knight_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_steam_knight_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_steam_knight_badge" }
]
```

---

## 套装八十二：发条之心（Clockwork Heart）饰品

> *"发条之心的每一次跳动，都是时间的脚步——精准、无情、永恒。"*

---

### 82.5 发条之心发条披风（Clockwork Heart Clockwork Cloak）

```gdscript
item_id = "acc_clockwork_heart_cloak"
display_name = "发条之心发条披风"
description = "一件由发条与精密丝编织而成的披风，披风表面不断有微型发条在转动和收缩——不是装饰，而是真正的发条机构被封印在了丝线中。这些发条来自不同的时钟：有的来自古老的钟楼，有的来自精密的怀表，有的甚至来自时间之神的时钟。当穿戴者移动时，发条会自动调整披风的形状，使穿戴者的动作更加精准。\n\n这件披风是发条之心在「时间工坊」中，从自己的时间装置上取下的发条编织的。他说："这件披风让我能够感受到时间的流动。每一次滴答，都是一次心跳；每一次转动，都是一次呼吸。"\n\n披风的效果是：先攻检定 +3（发条精准），且免疫 surprised（发条预知）。当穿戴者进行需要精准的动作时（开锁、拆陷阱、精细攻击），检定 +2（发条稳定）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "clockwork_heart_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_clockwork_heart_cloak" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_clockwork_heart_cloak" }
]
```

---

### 82.6 发条之心时间之链项链（Clockwork Heart Time Chain Necklace）

```gdscript
item_id = "acc_clockwork_heart_necklace"
display_name = "发条之心时间之链项链"
description = "一条由时间金与发条丝编织而成的项链，吊坠是一个微型怀表——这个怀表不是普通的计时器，它是真正的时间装置。怀表的指针不断转动，但方向不定：有时顺时针，有时逆时针，有时停止。据说这个怀表来自时间之神，他在创造时间时，将自己的一部分力量封印在了其中。\n\n这条项链是发条之心在「时间神殿」中，从时间之神的祭坛上取出的。他说："这个怀表承载了我的时间之力。它让我能够感受时间的流动，也让我能够短暂地控制时间。"\n\n项链的效果是：每日一次，可以释放「时间暂停」——bonus action 获得一个额外回合（时间暂停，只有你可以行动），但下一回合 stunned 1 回合（时间反噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "clockwork_heart_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_clockwork_heart_necklace" },
    { attribute_id = "history_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_clockwork_heart_necklace" }
]
```

---

### 82.7 发条之心精密之戒（Clockwork Heart Precision Ring）

```gdscript
item_id = "acc_clockwork_heart_ring_1"
display_name = "发条之心精密之戒"
description = "一枚由精密石与时间金铸造的戒指，戒指表面刻有微型精密齿轮图案。当佩戴者集中精神时，戒指会释放出一股精密之力，使佩戴者的动作如同机械般精准。这种力量不是魔法的——它是机械的，是精确的。据说这枚戒指来自一位发条大师，他用这枚戒指制作了世界上最精确的时钟。\n\n这枚戒指是发条之心在「精密工坊」中，从自己的精密装置上取下的碎片铸造的。他说："这枚戒指让我能够借用精密的力量。精密是机械的灵魂——它让我能够精确到毫厘。"\n\n戒指的效果是：攻击检定 +2（精密攻击），且暴击阈值 -1（精密暴击，19-20 暴击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "clockwork_heart_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_clockwork_heart_ring_1" }
]
```

---

### 82.8 发条之心倒转之戒（Clockwork Heart Rewind Ring）

```gdscript
item_id = "acc_clockwork_heart_ring_2"
display_name = "发条之心倒转之戒"
description = "一枚由倒转石与时间银铸造的戒指，戒指表面刻有微型倒转箭头图案。当佩戴者集中精神时，戒指会释放出一股倒转之力，使时间短暂倒流。这种倒流不是真正的回到过去——它只是让佩戴者的状态恢复到几秒钟前。据说这枚戒指来自一位时间法师，他用这枚戒指避免了无数次死亡。\n\n这枚戒指是发条之心在「倒转深渊」中，从时间法师的遗骸上取下的。他说："这枚戒指让我能够借用倒转的力量。但倒转不是逃避——它只是给了你一个重新选择的机会。"\n\n戒指的效果是：每日一次，可以释放「时间倒转」——将自身状态恢复至上一回合开始时（HP、mana、状态效果），但位置不变。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "clockwork_heart_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_clockwork_heart_ring_2" }
]
```

---

### 82.9 发条之心时间齿轮（Clockwork Heart Time Gear）

```gdscript
item_id = "acc_clockwork_heart_trinket"
display_name = "发条之心时间齿轮"
description = "一个由时间金属与魔法制成的微型齿轮，齿轮表面不断有微型时间流在闪烁。这个齿轮不是普通的机械零件——它是活的。当佩戴者需要时，可以激活齿轮，加速或减速时间。但齿轮有自己的意志——它会选择自己的速度，有时会拒绝佩戴者的命令。\n\n这个齿轮是发条之心在「时间工坊」中，从自己的时间装置上取下的核心制成的。他说："这个齿轮承载了我的时间之力。它让我能够控制时间的流速，也让我能够理解时间的真谛。"\n\n齿轮的效果是：每日一次，可以选择一种时间效果：\n- 加速：自身获得 haste 1 回合（不需要 concentration）\n- 减速：指定一个 15 尺内的敌人，其 slowed 1 回合（移动力减半，攻击检定有劣势，DC16 体质豁免抵抗）\n- 冻结：指定一个 10 尺内的敌人，其 stunned 1 回合（DC17 体质豁免抵抗）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "clockwork_heart_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_clockwork_heart_trinket" }
]
```

---

### 82.10 发条之心徽章（Clockwork Heart Badge）

```gdscript
item_id = "acc_clockwork_heart_badge"
display_name = "发条之心徽章"
description = "一枚由时间金与齿轮石铸造的徽章，徽章上刻着一个怀表和一个齿轮——发条之心的标志。徽章背面刻着一行小字：「时间就是一切，一切就是时间。」这枚徽章是时间公会的信物，拥有它意味着你已被时间认可。\n\n徽章的效果是：10 尺内所有友方先攻检定 +1（时间加速）。且每日一次，可以释放「时间领域」——10 尺半径内友方攻击检定 +1（时间加速），敌方攻击检定 -1（时间减速），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "clockwork_heart_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_clockwork_heart_badge" },
    { attribute_id = "history_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_clockwork_heart_badge" }
]
```

---

## 套装八十三：以太行者（Aether Walker）饰品

> *"以太行者不是在行走——他是在以太中游泳，而现实只是他偶尔上岸的沙滩。"*

---

### 83.5 以太行者以太披风（Aether Walker Aether Cloak）

```gdscript
item_id = "acc_aether_walker_cloak"
display_name = "以太行者以太披风"
description = "一件由以太丝与虚空布编织而成的披风，披风表面不断有微型以太波纹在流动和扭曲——不是装饰，而是真正的以太被封印在了丝线中。当穿戴者移动时，披风会自动与以太融合，使穿戴者可以在现实与以太之间短暂穿梭。据说这件披风是第一位以太行者亲手编织的，每一根丝线都来自不同的以太维度。\n\n这件披风是以太行者在「以太之门」中，从自己的以太形态中取下的碎片编织的。他说："这件披风让我能够在以太中自由行走。现实只是以太的一个投影——学会了在以太中行走，就学会了穿越现实。"\n\n披风的效果是：force 抗性 +15（以太之躯），且每日一次，可以 bonus action 传送到 30 尺内任何位置（以太步）。传送时不触发 opportunity attack。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "aether_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_aether_walker_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_aether_walker_cloak" }
]
```

---

### 83.6 以太行者以太之核项链（Aether Walker Aether Core Necklace）

```gdscript
item_id = "acc_aether_walker_necklace"
display_name = "以太行者以太之核项链"
description = "一条由以太金与虚空水晶编织而成的项链，吊坠是一颗以太之核——一颗由纯粹以太凝结的核心。核心内部不断有微型维度在诞生和毁灭，如同一个小型的宇宙。据说这颗核心来自以太之神的身体，他在创造以太时，将自己的一部分力量封印在了其中。\n\n这条项链是以太行者在「以太神殿」中，从以太之神的祭坛上取出的。他说："这颗核心承载了我的以太之力。它让我能够穿越维度，也让我能够理解宇宙的结构。"\n\n项链的效果是：奥秘检定 +3（以太知识），且每日一次，可以释放「以太之门」——在 20 尺内创造一个以太之门，允许友方通过门传送到 60 尺内任何位置（以太传送）。门持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "aether_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_aether_walker_necklace" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_aether_walker_necklace" }
]
```

---

### 83.7 以太行者相位之戒（Aether Walker Phase Ring）

```gdscript
item_id = "acc_aether_walker_ring_1"
display_name = "以太行者相位之戒"
description = "一枚由相位石与以太金铸造的戒指，戒指表面刻有微型相位图案。当佩戴者集中精神时，戒指会释放出一股相位之力，使佩戴者可以短暂地脱离现实，进入相位状态。在这种状态下，佩戴者可以穿过物体，免疫物理攻击，但无法攻击物理目标。\n\n这枚戒指是以太行者在「相位深渊」中，从相位恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用相位的力量。相位不是逃避——它是另一种存在方式。"\n\n戒指的效果是：每日一次，可以释放「相位步」——获得 incorporeal 1 回合：可以穿过物体，免疫非魔法物理伤害，但无法攻击物理目标（法术和 force 伤害仍可生效）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "aether_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_aether_walker_ring_1" }
]
```

---

### 83.8 以太行者虚空之戒（Aether Walker Void Ring）

```gdscript
item_id = "acc_aether_walker_ring_2"
display_name = "以太行者虚空之戒"
description = "一枚由虚空石与以太银铸造的戒指，戒指表面刻有微型虚空图案。当佩戴者集中精神时，戒指会释放出一股虚空之力，创造一个微型虚空黑洞，吞噬周围的物质。这种黑洞不是真正的黑洞——它只是以太中的一个漩涡，可以吸收和释放能量。\n\n这枚戒指是以太行者在「虚空深渊」中，从虚空之神那里得到的。虚空之神说："这枚戒指让你能够借用虚空的力量。但虚空是饥饿的——它会吞噬一切，包括你自己。"\n\n戒指的效果是：每日一次，可以释放「虚空吞噬」——指定一个 15 尺内的敌人，其受到 3D10 force（虚空吞噬，DC16 体质豁免 half），且被拉向中心 10 尺（虚空引力）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "aether_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_dc", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_aether_walker_ring_2" }
]
```

---

### 83.9 以太行者以太罗盘（Aether Walker Aether Compass）

```gdscript
item_id = "acc_aether_walker_trinket"
display_name = "以太行者以太罗盘"
description = "一个由以太金属与魔法制成的微型罗盘，罗盘指针不断旋转，指向不同的方向——不是指向北方，而是指向以太的不同维度。这个罗盘不是普通的导航工具——它是活的。当佩戴者需要时，可以激活罗盘，穿越到以太的不同维度。但罗盘有自己的意志——它会选择自己的方向，有时会带佩戴者到意想不到的地方。\n\n这个罗盘是以太行者在「以太之门」中，从自己的以太形态中取下的核心制成的。他说："这个罗盘承载了我的以太之力。它让我能够穿越维度，也让我能够找到回家的路。"\n\n罗盘的效果是：每日一次，可以释放「维度穿越」——bonus action 传送到以太维度，1 回合后返回原位。在以太维度中：完全隐形且免疫所有伤害，但无法攻击或施法。可以用 bonus action 提前返回。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "aether_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_aether_walker_trinket" }
]
```

---

### 83.10 以太行者徽章（Aether Walker Badge）

```gdscript
item_id = "acc_aether_walker_badge"
display_name = "以太行者徽章"
description = "一枚由以太金与虚空石铸造的徽章，徽章上刻着一个门和一个脚印——以太行者的标志。徽章背面刻着一行小字：「现实是门，以太是路。」这枚徽章是以太行者的信物，拥有它意味着你已被以太认可。\n\n徽章的效果是：10 尺内所有友方 force 抗性 +5（以太庇护）。且每日一次，可以释放「以太领域」——10 尺半径内友方可以 bonus action 传送到 15 尺内任何位置（以太步），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "aether_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_aether_walker_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_aether_walker_badge" }
]
```

---

## 套装八十四：磁力大师（Magnet Master）饰品

> *"磁力大师不是在操纵金属——他是在与金属对话，而金属总是听他的话。"*

---

### 84.5 磁力大师磁力披风（Magnet Master Magnetic Cloak）

```gdscript
item_id = "acc_magnet_master_cloak"
display_name = "磁力大师磁力披风"
description = "一件由磁力丝与金属布编织而成的披风，披风表面不断有微型磁力线在流动和扭曲——不是装饰，而是真正的磁力被封印在了丝线中。当穿戴者受到金属攻击时，披风会自动产生反磁力，偏转攻击。但当磁力过强时，披风会吸引周围的金属物体，造成意外的伤害。\n\n这件披风是磁力大师在「磁力实验室」中，从自己的磁力装置上取下的碎片编织的。他说："这件披风让我能够感受到磁力的流动。磁力是宇宙的基本力——学会了控制它，就学会了控制一切。"\n\n披风的效果是：免疫金属武器的攻击（磁力偏转，金属武器攻击自动 miss）。但每回合有 20% 概率吸引 5 尺内的金属物体，造成 1D6 bludgeoning（磁力吸引）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "magnet_master_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_magnet_master_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_magnet_master_cloak" }
]
```

---

### 84.6 磁力大师磁极项链（Magnet Master Magnetic Pole Necklace）

```gdscript
item_id = "acc_magnet_master_necklace"
display_name = "磁力大师磁极项链"
description = "一条由磁极金与磁力丝编织而成的项链，吊坠是一个微型磁极——这个磁极不是普通的磁铁，它是真正的磁极核心。磁极内部不断有微型磁力线在旋转，如同一个小型的磁场。据说这个磁极来自地球的磁心，磁力大师在深入地心后取出了它。\n\n这条项链是磁力大师在「地心之旅」中，从地球磁心上取下的。他说："这个磁极承载了我的磁力之源。它让我能够理解磁力的本质，也让我能够控制磁力的方向。"\n\n项链的效果是：奥秘检定 +3（磁力知识），且每日一次，可以释放「磁极反转」——指定一个 20 尺内的敌人，其金属装备（武器/护甲）被反转磁力：武器被吸走（disarmed，DC16 力量豁免抵抗），护甲被剥离（AC -2，DC16 力量豁免抵抗），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "magnet_master_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_magnet_master_necklace" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_magnet_master_necklace" }
]
```

---

### 84.7 磁力大师吸引之戒（Magnet Master Attraction Ring）

```gdscript
item_id = "acc_magnet_master_ring_1"
display_name = "磁力大师吸引之戒"
description = "一枚由吸引石与磁力金铸造的戒指，戒指表面刻有微型吸引图案。当佩戴者集中精神时，戒指会释放出一股吸引之力，将目标拉向自己。这种吸引不是物理的——它是磁力的，可以穿透障碍物，可以无视距离。据说这枚戒指来自一位磁力大师，他用这枚戒指将一座城堡的城墙拉倒。\n\n这枚戒指是磁力大师在「吸引深渊」中，从吸引之神那里得到的。吸引之神说："这枚戒指让你能够借用我的力量。但吸引是双刃剑——它既能拉近敌人，也能拉近危险。"\n\n戒指的效果是：每日一次，可以释放「磁力吸引」——指定一个 30 尺内的敌人，其被拉向自己 20 尺（DC16 力量豁免抵抗），且如果拉到 5 尺内，自动受到 2D8 bludgeoning（撞击伤害）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "magnet_master_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_magnet_master_ring_1" }
]
```

---

### 84.8 磁力大师排斥之戒（Magnet Master Repulsion Ring）

```gdscript
item_id = "acc_magnet_master_ring_2"
display_name = "磁力大师排斥之戒"
description = "一枚由排斥石与磁力银铸造的戒指，戒指表面刻有微型排斥图案。当佩戴者集中精神时，戒指会释放出一股排斥之力，将目标推开。这种排斥不是物理的——它是磁力的，可以穿透障碍物，可以无视距离。据说这枚戒指来自一位磁力大师，他用这枚戒指将一支军队推回了边界。\n\n这枚戒指是磁力大师在「排斥深渊」中，从排斥之神那里得到的。排斥之神说："这枚戒指让你能够借用我的力量。但排斥是孤独的——它会推开一切，包括朋友。"\n\n戒指的效果是：每日一次，可以释放「磁力排斥」——指定一个 15 尺内的敌人，其被推开 20 尺（DC16 力量豁免抵抗），且如果撞到墙壁或物体，受到 2D8 bludgeoning（撞击伤害）。或者指定一个 10 尺内的友方，其被推出危险区域 15 尺（无豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "magnet_master_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_magnet_master_ring_2" }
]
```

---

### 84.9 磁力大师磁力球（Magnet Master Magnetic Sphere）

```gdscript
item_id = "acc_magnet_master_trinket"
display_name = "磁力大师磁力球"
description = "一个由磁力金属与魔法制成的微型球体，球体表面不断有微型磁力线在流动。这个球体不是普通的磁铁——它是活的。当佩戴者需要时，可以激活球体，创造出一个强大的磁场。这个磁场可以吸引或排斥金属物体，可以偏转金属攻击，甚至可以扭曲金属的结构。\n\n这个球体是磁力大师在「磁力实验室」中，从自己的磁力核心上取下的碎片制成的。他说："这个球体承载了我的磁力之力。它让我能够控制磁力，也让我能够理解磁力的语言。"\n\n球体的效果是：每日一次，可以释放「磁力风暴」——20 尺半径内所有金属武器被 disarmed 并吸向中心（DC17 力量豁免抵抗，保留武器），所有金属护甲 AC -3（磁力扭曲），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "magnet_master_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_magnet_master_trinket" }
]
```

---

### 84.10 磁力大师徽章（Magnet Master Badge）

```gdscript
item_id = "acc_magnet_master_badge"
display_name = "磁力大师徽章"
description = "一枚由磁力金与磁石铸造的徽章，徽章上刻着一个磁极和一个箭头——磁力大师的标志。徽章背面刻着一行小字：「吸引与排斥，宇宙的呼吸。」这枚徽章是磁力公会的信物，拥有它意味着你已被磁力认可。\n\n徽章的效果是：10 尺内所有友方免疫金属武器的 disarm（磁力稳定）。且每日一次，可以释放「磁力领域」——10 尺半径内所有敌人金属武器攻击检定 -2（磁力干扰），且友方金属武器攻击检定 +1（磁力加速），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "magnet_master_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_magnet_master_badge" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_magnet_master_badge" }
]
```

---

## 套装八十五：光子剑士（Photon Swordsman）饰品

> *"光子剑士的剑不是金属——它是光，是纯粹的能量，是速度本身。"*

---

### 85.5 光子剑士光之披风（Photon Swordsman Light Cloak）

```gdscript
item_id = "acc_photon_swordsman_cloak"
display_name = "光子剑士光之披风"
description = "一件由光子丝与能量布编织而成的披风，披风表面不断有微型光子在流动和闪烁——不是装饰，而是真正的光被封印在了丝线中。当穿戴者移动时，光子会自动形成光晕，使穿戴者的轮廓变得模糊。在黑暗中，披风会发出耀眼的光芒，照亮周围的一切。\n\n这件披风是光子剑士在「光之工坊」中，从自己的光子装置上取下的碎片编织的。他说："这件披风让我能够将光的力量带到任何地方。它可以照亮，也可以致盲；可以温暖，也可以灼烧。"\n\n披风的效果是：radiant 抗性 +15（光之躯），且在黑暗中 15 尺半径内提供 bright light（光照）。当从光亮中发动攻击时，攻击检定有优势（光之突袭）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "photon_swordsman_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_photon_swordsman_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_photon_swordsman_cloak" }
]
```

---

### 85.6 光子剑士光子核心项链（Photon Swordsman Photon Core Necklace）

```gdscript
item_id = "acc_photon_swordsman_necklace"
display_name = "光子剑士光子核心项链"
description = "一条由光子金与能量水晶编织而成的项链，吊坠是一颗光子核心——一颗由纯粹光子凝结的核心。核心内部不断有微型光束在闪烁，如同一个小型的太阳。据说这颗核心来自光之神的身体，他在创造光时，将自己的一部分力量封印在了其中。\n\n这条项链是光子剑士在「光之神殿」中，从光之神的祭坛上取出的。他说："这颗核心承载了我的光之力。它让我能够创造光，也让我能够理解光的本质。"\n\n项链的效果是：法术攻击检定 +2（光子精准），且每日一次，可以释放「光子爆发」——15 尺锥形，所有敌人受到 4D10 radiant（DC17 体质豁免 half）且 blinded 1 回合（光之致盲）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "photon_swordsman_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_photon_swordsman_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_photon_swordsman_necklace" }
]
```

---

### 85.7 光子剑士光速之戒（Photon Swordsman Light Speed Ring）

```gdscript
item_id = "acc_photon_swordsman_ring_1"
display_name = "光子剑士光速之戒"
description = "一枚由光速石与光子金铸造的戒指，戒指表面刻有微型光速图案。当佩戴者集中精神时，戒指会释放出一股光速之力，使佩戴者的速度接近光速。在这种速度下，佩戴者可以在一瞬间移动到任何地方，攻击任何敌人。但光速是有代价的——每次使用，佩戴者会感到极度的疲惫。\n\n这枚戒指是光子剑士在「光速深渊」中，从光速恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用光速的力量。但光速不是凡人能够承受的——它会燃烧你的身体，撕裂你的灵魂。"\n\n戒指的效果是：移动力 +10 尺（光速步），且每日一次，可以 bonus action 移动至 30 尺内任何位置（光速冲刺），路径上的所有敌人须通过 DC16 敏捷豁免，失败则受到 2D8 radiant（光速冲击）且 blinded 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "photon_swordsman_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_photon_swordsman_ring_1" }
]
```

---

### 85.8 光子剑士折射之戒（Photon Swordsman Refraction Ring）

```gdscript
item_id = "acc_photon_swordsman_ring_2"
display_name = "光子剑士折射之戒"
description = "一枚由折射石与光子银铸造的戒指，戒指表面刻有微型折射图案。当佩戴者集中精神时，戒指会释放出一股折射之力，使光线在佩戴者周围弯曲，创造出多个镜像。这些镜像不是幻象——它们是光的折射，可以迷惑敌人，也可以分散攻击。\n\n这枚戒指是光子剑士在「折射深渊」中，从折射之神那里得到的。折射之神说："这枚戒指让你能够借用折射的力量。光不会直线传播——它会弯曲，会反射，会欺骗。"\n\n戒指的效果是：每日一次，可以释放「光之镜像」——创造 1D4+1 个镜像分身（1 HP，AC = 你的 AC），持续 2 回合。分身可以进行攻击（+5，1D8+3 radiant）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "photon_swordsman_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_photon_swordsman_ring_2" }
]
```

---

### 85.9 光子剑士光子剑柄（Photon Swordsman Photon Hilt）

```gdscript
item_id = "acc_photon_swordsman_trinket"
display_name = "光子剑士光子剑柄"
description = "一个由光子金属与魔法制成的微型剑柄，剑柄表面不断有微型光子在流动。这个剑柄不是普通的剑柄——它是活的。当佩戴者需要时，可以激活剑柄，创造出一把纯粹由光子构成的剑。这把剑可以切割任何东西：金属、岩石、魔法护盾、甚至灵魂。但剑柄需要能量——如果不定期充能，剑会变得暗淡。\n\n这个剑柄是光子剑士在「光之工坊」中，从自己的光子剑上取下的核心制成的。他说："这个剑柄承载了我的光之剑。它让我能够在任何时候召唤光之剑，也让我能够理解光的力量。"\n\n剑柄的效果是：每日一次，可以召唤「光子剑」——作为武器使用（+3，2D8+4 radiant/slashing，可选择伤害类型），持续 2 回合。光子剑无视 damage resistance，且对 undead 和 demon 伤害翻倍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "photon_swordsman_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_photon_swordsman_trinket" }
]
```

---

### 85.10 光子剑士徽章（Photon Swordsman Badge）

```gdscript
item_id = "acc_photon_swordsman_badge"
display_name = "光子剑士徽章"
description = "一枚由光子金与光石铸造的徽章，徽章上刻着一把剑和一道光束——光子剑士的标志。徽章背面刻着一行小字：「光速即正义，光刃即真理。」这枚徽章是光子公会的信物，拥有它意味着你已被光认可。\n\n徽章的效果是：10 尺内所有友方 radiant 抗性 +5（光之庇护）。且每日一次，可以释放「光之领域」——10 尺半径内所有敌人攻击检定 -1（光之干扰），且友方攻击附加 1D6 radiant（光之加持），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "photon_swordsman_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_photon_swordsman_badge" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_photon_swordsman_badge" }
]
```

---

## 套装八十六：音波战士（Sonic Warrior）饰品

> *"音波战士不是在战斗——他是在演奏，而战场就是他的音乐厅。"*

---

### 86.5 音波战士音波披风（Sonic Warrior Sonic Cloak）

```gdscript
item_id = "acc_sonic_warrior_cloak"
display_name = "音波战士音波披风"
description = "一件由音波丝与共振布编织而成的披风，披风表面不断有微型音波在流动和共振——不是装饰，而是真正的音波被封印在了丝线中。当穿戴者移动时，披风会自动产生音波，干扰周围的敌人。在战斗中，披风会放大穿戴者的声音，使其如同雷鸣般响亮。\n\n这件披风是音波战士在「音波工坊」中，从自己的音波装置上取下的碎片编织的。他说："这件披风让我能够将音波的力量带到任何地方。它可以震碎，也可以治愈；可以混乱，也可以秩序。"\n\n披风的效果是：thunder 抗性 +15（音波之躯），且每日一次，可以释放「音波护盾」——10 尺半径内所有敌人攻击检定 -2（音波干扰），且友方 AC +1（音波共振），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "sonic_warrior_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_thunder", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_sonic_warrior_cloak" },
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_sonic_warrior_cloak" }
]
```

---

### 86.6 音波战士共鸣水晶项链（Sonic Warrior Resonance Crystal Necklace）

```gdscript
item_id = "acc_sonic_warrior_necklace"
display_name = "音波战士共鸣水晶项链"
description = "一条由共鸣金与音波水晶编织而成的项链，吊坠是一颗共鸣水晶——一颗由纯粹音波凝结的水晶。水晶内部不断有微型音波在共振，如同一个小型的音乐厅。据说这颗水晶来自音波之神的声音，他在创造音乐时，将自己的一部分力量封印在了其中。\n\n这条项链是音波战士在「音波神殿」中，从音波之神的祭坛上取出的。他说："这颗水晶承载了我的音波之力。它让我能够创造音乐，也让我能够理解声音的本质。"\n\n项链的效果是：表演检定 +3（音波表演），且每日一次，可以释放「音波冲击」——15 尺锥形，所有敌人受到 3D10 thunder（DC16 体质豁免 half）且 deafened 2 回合（音波致聋）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "sonic_warrior_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_sonic_warrior_necklace" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_sonic_warrior_necklace" }
]
```

---

### 86.7 音波战士共振之戒（Sonic Warrior Resonance Ring）

```gdscript
item_id = "acc_sonic_warrior_ring_1"
display_name = "音波战士共振之戒"
description = "一枚由共振石与音波金铸造的戒指，戒指表面刻有微型共振图案。当佩戴者集中精神时，戒指会释放出一股共振之力，使目标的内部结构产生共振。这种共振不是物理的——它是音波的，可以穿透盔甲，粉碎骨骼，甚至震碎灵魂。\n\n这枚戒指是音波战士在「共振深渊」中，从共振恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用共振的力量。共振是宇宙的频率——找到敌人的频率，就能摧毁他们。"\n\n戒指的效果是：所有攻击附加 1D6 thunder（音波共振），且每日一次，可以释放「共振粉碎」——指定一个 10 尺内的敌人，其受到 3D10 thunder（DC16 体质豁免 half），且护甲 AC -2（共振裂痕，持续至修复）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "sonic_warrior_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_sonic_warrior_ring_1" }
]
```

---

### 86.8 音波战士静音之戒（Sonic Warrior Silence Ring）

```gdscript
item_id = "acc_sonic_warrior_ring_2"
display_name = "音波战士静音之戒"
description = "一枚由静音石与音波银铸造的戒指，戒指表面刻有微型静音图案。当佩戴者集中精神时，戒指会释放出一股静音之力，创造出一个绝对静音的领域。在这个领域中，没有任何声音可以传播——说话、脚步、甚至心跳都会被吞噬。这种静音是音波的反面，是音波战士的终极防御。\n\n这枚戒指是音波战士在「静音深渊」中，从静音之神那里得到的。静音之神说："这枚戒指让你能够借用静音的力量。静音不是空虚——它是另一种声音，是音波的休息。"\n\n戒指的效果是：隐匿检定 +2（静音移动），且每日一次，可以释放「绝对静音」——10 尺半径内所有声音消失 2 回合，期间区域内无法施法（需要语言成分），且所有生物的攻击检定 -1（失去声音反馈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "sonic_warrior_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_sonic_warrior_ring_2" }
]
```

---

### 86.9 音波战士音叉（Sonic Warrior Tuning Fork）

```gdscript
item_id = "acc_sonic_warrior_trinket"
display_name = "音波战士音叉"
description = "一个由音波金属与魔法制成的微型音叉，音叉表面不断有微型音波在振动。这个音叉不是普通的乐器——它是活的。当佩戴者敲击它时，会释放出特定频率的音波，可以治愈盟友或摧毁敌人。但音叉有自己的意志——它会选择自己的频率，有时会与佩戴者的意图相悖。\n\n这个音叉是音波战士在「音波工坊」中，从自己的音波装置上取下的核心制成的。他说："这个音叉承载了我的音波之力。它让我能够控制频率，也让我能够理解声音的语言。"\n\n音叉的效果是：每日一次，可以敲击音叉——选择一个频率：\n- 治愈频率：10 尺内所有友方恢复 3D8 HP（音波治愈）\n- 破坏频率：15 尺锥形，所有敌人受到 3D10 thunder（DC16 体质豁免 half）\n- 混乱频率：10 尺内所有敌人攻击检定有劣势（音波混乱）2 回合\n- 秩序频率：10 尺内所有友方攻击检定有优势（音波秩序）2 回合"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "sonic_warrior_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_sonic_warrior_trinket" }
]
```

---

### 86.10 音波战士徽章（Sonic Warrior Badge）

```gdscript
item_id = "acc_sonic_warrior_badge"
display_name = "音波战士徽章"
description = "一枚由音波金与声石铸造的徽章，徽章上刻着一个音符和一个波纹——音波战士的标志。徽章背面刻着一行小字：「声波即力量，频率即命运。」这枚徽章是音波公会的信物，拥有它意味着你已被声音认可。\n\n徽章的效果是：10 尺内所有友方 thunder 抗性 +5（音波庇护）。且每日一次，可以释放「音波领域」——10 尺半径内所有敌人每回合开始时受到 1D6 thunder（音波侵蚀），且友方在领域内攻击检定 +1（音波共振），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "sonic_warrior_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_sonic_warrior_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_sonic_warrior_badge" }
]
```

---

*套装 81–86 饰品部分（36/60 件）*

## 套装八十七：重力行者（Gravity Walker）饰品

> *"重力行者不是在行走——他是在重新定义'上'和'下'。"*

---

### 87.5 重力行者重力披风（Gravity Walker Gravity Cloak）

```gdscript
item_id = "acc_gravity_walker_cloak"
display_name = "重力行者重力披风"
description = "一件由重力丝与空间布编织而成的披风，披风表面不断有微型重力波纹在流动和扭曲——不是装饰，而是真正的重力场被封印在了丝线中。当穿戴者移动时，披风会自动调整重力方向，使穿戴者可以在墙壁和天花板上行走。据说这件披风是重力行者亲手编织的，每一根丝线都来自不同的重力维度。\n\n这件披风是重力行者在「重力实验室」中，从自己的重力装置上取下的碎片编织的。他说："这件披风让我能够控制重力。上可以是下，下可以是上——重力只是相对的。"\n\n披风的效果是：可以无视困难地形（重力调整），且可以在墙壁和天花板上行走（重力反转），速度减半。当从高处跳下时，可以控制下落速度，免疫坠落伤害（重力缓冲）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "gravity_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_gravity_walker_cloak" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_gravity_walker_cloak" }
]
```

---

### 87.6 重力行者重力核心项链（Gravity Walker Gravity Core Necklace）

```gdscript
item_id = "acc_gravity_walker_necklace"
display_name = "重力行者重力核心项链"
description = "一条由重力金与空间水晶编织而成的项链，吊坠是一颗重力核心——一颗由纯粹重力凝结的核心。核心内部不断有微型引力波在旋转，如同一个小型的黑洞。据说这颗核心来自重力之神的心脏，他在创造重力时，将自己的一部分力量封印在了其中。\n\n这条项链是重力行者在「重力神殿」中，从重力之神的祭坛上取出的。他说："这颗核心承载了我的重力之力。它让我能够控制重力，也让我能够理解宇宙的结构。"\n\n项链的效果是：奥秘检定 +3（重力知识），且每日一次，可以释放「重力井」——指定一个 15 尺内的点，10 尺半径内所有敌人移动力减半（重力增强），且每回合开始时受到 2D6 bludgeoning（重力压迫，DC16 力量豁免 half），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "gravity_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_gravity_walker_necklace" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_gravity_walker_necklace" }
]
```

---

### 87.7 重力行者重压之戒（Gravity Walker Heavy Pressure Ring）

```gdscript
item_id = "acc_gravity_walker_ring_1"
display_name = "重力行者重压之戒"
description = "一枚由重压石与重力金铸造的戒指，戒指表面刻有微型重压图案。当佩戴者集中精神时，戒指会释放出一股重压之力，增加目标的重力。这种重压不是物理的——它是重力的，可以穿透盔甲，压碎骨骼，甚至压扁灵魂。\n\n这枚戒指是重力行者在「重压深渊」中，从重压恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用重压的力量。重压是重力的极端——它会让敌人无法动弹。"\n\n戒指的效果是：每日一次，可以释放「重力重压」——指定一个 15 尺内的敌人，其移动力归零（重力压迫），且攻击检定 -3（重力沉重），持续 2 回合（DC16 力量豁免抵抗，失败则 full effect，成功则 half effect）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "gravity_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_gravity_walker_ring_1" }
]
```

---

### 87.8 重力行者失重之戒（Gravity Walker Weightlessness Ring）

```gdscript
item_id = "acc_gravity_walker_ring_2"
display_name = "重力行者失重之戒"
description = "一枚由失重石与重力银铸造的戒指，戒指表面刻有微型失重图案。当佩戴者集中精神时，戒指会释放出一股失重之力，使佩戴者或目标进入失重状态。在这种状态下，目标可以在空中自由移动，但攻击会变得困难。据说这枚戒指来自一位重力行者大师，他用这枚戒指在太空中生存了三百年。\n\n这枚戒指是重力行者在「失重深渊」中，从失重之神那里得到的。失重之神说："这枚戒指让你能够借用失重的力量。失重不是自由——它是另一种束缚。"\n\n戒指的效果是：每日一次，可以释放「失重领域」——10 尺半径内所有友方获得失重 2 回合：移动力 +15 尺，跳跃距离 ×3，但攻击检定 -1（失重不稳定）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "gravity_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_gravity_walker_ring_2" }
]
```

---

### 87.9 重力行者重力球（Gravity Walker Gravity Sphere）

```gdscript
item_id = "acc_gravity_walker_trinket"
display_name = "重力行者重力球"
description = "一个由重力金属与魔法制成的微型球体，球体表面不断有微型引力波在流动。这个球体不是普通的物体——它是活的。当佩戴者需要时，可以激活球体，创造出一个强大的重力场。这个重力场可以吸引或排斥物体，可以扭曲空间，甚至可以创造微型黑洞。\n\n这个球体是重力行者在「重力实验室」中，从自己的重力核心上取下的碎片制成的。他说："这个球体承载了我的重力之力。它让我能够控制重力，也让我能够理解宇宙的奥秘。"\n\n球体的效果是：每日一次，可以释放「重力坍缩」——指定一个 20 尺内的点，15 尺半径内所有敌人被拉向中心 10 尺（DC17 力量豁免抵抗），且受到 4D10 bludgeoning（重力坍缩）。中心点的敌人受到双倍伤害。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "gravity_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_gravity_walker_trinket" }
]
```

---

### 87.10 重力行者徽章（Gravity Walker Badge）

```gdscript
item_id = "acc_gravity_walker_badge"
display_name = "重力行者徽章"
description = "一枚由重力金与空间石铸造的徽章，徽章上刻着一个箭头和一个球——重力行者的标志。徽章背面刻着一行小字：「重力是绳，也是箭。」这枚徽章是重力公会的信物，拥有它意味着你已被重力认可。\n\n徽章的效果是：10 尺内所有友方免疫 knocked prone（重力稳定）。且每日一次，可以释放「重力平衡」——10 尺内所有友方 AC +1（重力护盾），且攻击检定 +1（重力加速），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "gravity_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_gravity_walker_badge" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_gravity_walker_badge" }
]
```

---

## 套装八十八：量子幽灵（Quantum Ghost）饰品

> *"量子幽灵不是在这里——他同时在所有地方，直到你观察他的那一刻。"*

---

### 88.5 量子幽灵量子披风（Quantum Ghost Quantum Cloak）

```gdscript
item_id = "acc_quantum_ghost_cloak"
display_name = "量子幽灵量子披风"
description = "一件由量子丝与概率布编织而成的披风，披风表面不断有微型概率波在流动和坍缩——不是装饰，而是真正的量子态被封印在了丝线中。当穿戴者移动时，披风会自动进入量子叠加态，使穿戴者同时存在于多个位置。直到被观察的那一刻，穿戴者的真正位置是不确定的。\n\n这件披风是量子幽灵在「量子实验室」中，从自己的量子装置上取下的碎片编织的。他说："这件披风让我能够借用量子的力量。我不是在这里——我在所有地方，直到你看到我。"\n\n披风的效果是：每日一次，可以进入「量子叠加」1 回合——对所有攻击有 50% 概率 miss（量子闪避），且可以 bonus action 传送到 20 尺内任何位置（量子跳跃）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "quantum_ghost_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_quantum_ghost_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_quantum_ghost_cloak" }
]
```

---

### 88.6 量子幽灵概率之核项链（Quantum Ghost Probability Core Necklace）

```gdscript
item_id = "acc_quantum_ghost_necklace"
display_name = "量子幽灵概率之核项链"
description = "一条由概率金与量子水晶编织而成的项链，吊坠是一颗概率之核——一颗由纯粹概率凝结的核心。核心内部不断有微型概率波在坍缩和重生，如同一个小型的宇宙。据说这颗核心来自概率之神，他在创造概率时，将自己的一部分力量封印在了其中。\n\n这条项链是量子幽灵在「概率神殿」中，从概率之神的祭坛上取出的。他说："这颗核心承载了我的概率之力。它让我能够操纵概率，也让我能够理解不确定性的本质。"\n\n项链的效果是：奥秘检定 +3（量子知识），且每日一次，可以释放「概率操纵」——指定一个 20 尺内的敌人，其下一次攻击自动 miss（概率坍缩），或指定一个友方，其下一次攻击自动命中且暴击（概率提升）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "quantum_ghost_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_quantum_ghost_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_quantum_ghost_necklace" }
]
```

---

### 88.7 量子幽灵不确定性之戒（Quantum Ghost Uncertainty Ring）

```gdscript
item_id = "acc_quantum_ghost_ring_1"
display_name = "量子幽灵不确定性之戒"
description = "一枚由不确定性石与量子金铸造的戒指，戒指表面刻有微型不确定性图案。当佩戴者集中精神时，戒指会释放出一股不确定性之力，使目标的状态变得不确定。这种不确定不是混乱——它是量子的，是概率的。据说这枚戒指来自一位量子大师，他用这枚戒指让一位敌人的位置永远无法确定。\n\n这枚戒指是量子幽灵在「不确定性深渊」中，从不确定性之神那里得到的。不确定性之神说："这枚戒指让你能够借用我的力量。不确定性是宇宙的本质——学会拥抱它。"\n\n戒指的效果是：每日一次，可以释放「不确定性原理」——指定一个 15 尺内的敌人，其在 2 回合内攻击检定和 AC 互换（高攻击则低 AC，低攻击则高 AC）（DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "quantum_ghost_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_quantum_ghost_ring_1" }
]
```

---

### 88.8 量子幽灵纠缠之戒（Quantum Ghost Entanglement Ring）

```gdscript
item_id = "acc_quantum_ghost_ring_2"
display_name = "量子幽灵纠缠之戒"
description = "一枚由纠缠石与量子银铸造的戒指，戒指表面刻有微型纠缠图案。当佩戴者集中精神时，戒指会释放出一股纠缠之力，将两个目标量子纠缠在一起。在这种纠缠状态下，两个目标会共享伤害和状态——一个受伤，另一个也会受伤；一个治愈，另一个也会治愈。\n\n这枚戒指是量子幽灵在「纠缠深渊」中，从纠缠之神那里得到的。纠缠之神说："这枚戒指让你能够借用纠缠的力量。纠缠是宇宙的纽带——它连接了一切，也束缚了一切。"\n\n戒指的效果是：每日一次，可以释放「量子纠缠」——链接两个 30 尺内的目标（可以是敌敌、友友、或敌友），2 回合内：当一方受到伤害时，另一方受到 50% 伤害；当一方恢复 HP 时，另一方恢复 50% HP（DC16 智慧豁免抵抗纠缠）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "quantum_ghost_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_quantum_ghost_ring_2" }
]
```

---

### 88.9 量子幽灵量子骰子（Quantum Ghost Quantum Dice）

```gdscript
item_id = "acc_quantum_ghost_trinket"
display_name = "量子幽灵量子骰子"
description = "一个由量子金属与魔法制成的微型骰子，骰子表面不断有微型概率波在闪烁。这个骰子不是普通的赌博工具——它是活的。当佩戴者投掷它时，骰子会坍缩出一个结果，但这个结果不是随机的——它是由佩戴者的意志决定的。据说这个骰子来自概率之神，他在创造概率时，将自己的一部分力量封印在了其中。\n\n这个骰子是量子幽灵在「概率神殿」中，从概率之神的祭坛上取出的。他说："这个骰子承载了我的概率之力。它让我能够操纵命运，也让我能够理解不确定性的美丽。"\n\n骰子的效果是：每日一次，可以投掷量子骰子——结果由你选择（1-6）：\n- 1：指定一个敌人，其下一回合 stunned（概率坍缩）\n- 2：指定一个友方，其下一回合攻击检定有优势（概率提升）\n- 3：指定一个敌人，其下一回合攻击检定有劣势（概率降低）\n- 4：自身恢复 3D8 HP（概率治愈）\n- 5：指定一个敌人，受到 3D10 force（概率冲击）\n- 6：10 尺内所有友方获得 +2 AC（概率护盾）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "quantum_ghost_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_quantum_ghost_trinket" }
]
```

---

### 88.10 量子幽灵徽章（Quantum Ghost Badge）

```gdscript
item_id = "acc_quantum_ghost_badge"
display_name = "量子幽灵徽章"
description = "一枚由量子金与概率石铸造的徽章，徽章上刻着一个波函数和一个骰子——量子幽灵的标志。徽章背面刻着一行小字：「观察即现实，概率即真理。」这枚徽章是量子公会的信物，拥有它意味着你已被概率认可。\n\n徽章的效果是：10 尺内所有友方攻击检定有 10% 概率自动命中（量子命中）。且每日一次，可以释放「概率领域」——10 尺半径内友方攻击检定 +1（概率提升），敌方攻击检定 -1（概率降低），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "quantum_ghost_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_quantum_ghost_badge" },
    { attribute_id = "insight_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_quantum_ghost_badge" }
]
```

---

## 套装八十九：辐射行者（Radiation Walker）饰品

> *"辐射行者不是在承受辐射——他就是辐射本身，而他选择照亮你。"*

---

### 89.5 辐射行者辐射披风（Radiation Walker Radiation Cloak）

```gdscript
item_id = "acc_radiation_walker_cloak"
display_name = "辐射行者辐射披风"
description = "一件由辐射丝与能量布编织而成的披风，披风表面不断有微型辐射波纹在流动和闪烁——不是装饰，而是真正的辐射被封印在了丝线中。这些辐射来自不同的源头：核反应、恒星爆炸、甚至是创世之初。当穿戴者移动时，披风会自动释放辐射，照亮周围，也灼伤敌人。\n\n这件披风是辐射行者在「辐射实验室」中，从自己的辐射装置上取下的碎片编织的。他说："这件披风让我能够将辐射的力量带到任何地方。它可以照亮，也可以毁灭；可以治愈，也可以腐蚀。"\n\n披风的效果是：radiant 抗性 +15（辐射免疫），且 5 尺半径内所有敌人每回合开始时受到 1D6 radiant（辐射灼伤）。当穿戴者受到伤害时，有 25% 概率释放「辐射脉冲」——5 尺半径内所有敌人受到 2D6 radiant（辐射爆发）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "radiation_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_radiation_walker_cloak" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_radiation_walker_cloak" }
]
```

---

### 89.6 辐射行者辐射核心项链（Radiation Walker Radiation Core Necklace）

```gdscript
item_id = "acc_radiation_walker_necklace"
display_name = "辐射行者辐射核心项链"
description = "一条由辐射金与能量水晶编织而成的项链，吊坠是一颗辐射核心——一颗由纯粹辐射凝结的核心。核心内部不断有微型辐射波在闪烁，如同一个小型的太阳。据说这颗核心来自辐射之神的心脏，他在创造辐射时，将自己的一部分力量封印在了其中。\n\n这条项链是辐射行者在「辐射神殿」中，从辐射之神的祭坛上取出的。他说："这颗核心承载了我的辐射之力。它让我能够控制辐射，也让我能够理解能量的本质。"\n\n项链的效果是：自然检定 +3（辐射知识），且每日一次，可以释放「辐射治愈」——10 尺内所有友方恢复 3D8 HP（辐射治愈），但受到 1D4 radiant（辐射副作用）。或者释放「辐射爆发」——15 尺半径内所有敌人受到 4D10 radiant（DC17 体质豁免 half）且 poisoned 2 回合（辐射中毒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "radiation_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_radiation_walker_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_radiation_walker_necklace" }
]
```

---

### 89.7 辐射行者衰变之戒（Radiation Walker Decay Ring）

```gdscript
item_id = "acc_radiation_walker_ring_1"
display_name = "辐射行者衰变之戒"
description = "一枚由衰变石与辐射金铸造的戒指，戒指表面刻有微型衰变图案。当佩戴者集中精神时，戒指会释放出一股衰变之力，加速目标的衰变。这种衰变不是物理的——它是辐射的，可以穿透盔甲，腐蚀细胞，甚至瓦解灵魂。据说这枚戒指来自一位辐射大师，他用这枚戒指让一座城市在三天内变成了废墟。\n\n这枚戒指是辐射行者在「衰变深渊」中，从衰变之神那里得到的。衰变之神说："这枚戒指让你能够借用衰变的力量。衰变是宇宙的法则——一切终将腐朽。"\n\n戒指的效果是：所有攻击附加 1D4 radiant（辐射衰变），且每日一次，可以释放「衰变之触」——指定一个 5 尺内的敌人，其受到 3D10 radiant（DC16 体质豁免 half），且 max HP 降低 10 直至长休（细胞衰变）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "radiation_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_radiation_walker_ring_1" }
]
```

---

### 89.8 辐射行者净化之戒（Radiation Walker Purification Ring）

```gdscript
item_id = "acc_radiation_walker_ring_2"
display_name = "辐射行者净化之戒"
description = "一枚由净化石与辐射银铸造的戒指，戒指表面刻有微型净化图案。当佩戴者集中精神时，戒指会释放出一股净化之力，用辐射清除一切毒素和疾病。这种净化不是温和的——它是剧烈的，会烧毁一切不纯净的东西。据说这枚戒指来自一位辐射医者，他用这枚戒指治愈了瘟疫，但也烧伤了许多病人。\n\n这枚戒指是辐射行者在「净化深渊」中，从净化之神那里得到的。净化之神说："这枚戒指让你能够借用净化的力量。净化是残酷的——它会烧毁一切，包括你自己。"\n\n戒指的效果是：免疫 poison 和 disease（辐射净化），且每日一次，可以释放「辐射净化」——指定一个 10 尺内的友方，其移除所有 poison、disease、curse（辐射净化），但受到 2D6 radiant（净化副作用）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "radiation_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_radiation_walker_ring_2" }
]
```

---

### 89.9 辐射行者辐射之源（Radiation Walker Radiation Source）

```gdscript
item_id = "acc_radiation_walker_trinket"
display_name = "辐射行者辐射之源"
description = "一个由辐射金属与魔法制成的微型源，源表面不断有微型辐射波在闪烁。这个源不是普通的能源——它是活的。当佩戴者需要时，可以激活源，释放出强大的辐射。但这种辐射是无差别的——它会伤害敌人，也会伤害盟友。据说这个源来自创世之初，是宇宙最初的辐射。\n\n这个源是辐射行者在「辐射之源」中，从创世之初取出的。他说："这个源承载了我的辐射之力。它不是给凡人的礼物——它是给毁灭者的武器。"\n\n源的效果是：每日一次，可以释放「辐射风暴」——20 尺半径内所有生物（包括友方）受到 4D10 radiant（DC17 体质豁免 half），且被 blinded 1 回合（辐射致盲）。友方受到的伤害减半。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "radiation_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_radiation_walker_trinket" }
]
```

---

### 89.10 辐射行者徽章（Radiation Walker Badge）

```gdscript
item_id = "acc_radiation_walker_badge"
display_name = "辐射行者徽章"
description = "一枚由辐射金与能量石铸造的徽章，徽章上刻着三个扇形和一个圆点——辐射的标志。徽章背面刻着一行小字：「辐射即生命，生命即辐射。」这枚徽章是辐射公会的信物，拥有它意味着你已被辐射认可。\n\n徽章的效果是：10 尺内所有友方 radiant 抗性 +5（辐射庇护）。且每日一次，可以释放「辐射领域」——10 尺半径内所有敌人每回合开始时受到 1D6 radiant（辐射侵蚀），且友方在领域内每回合开始时恢复 1D6 HP（辐射治愈），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "radiation_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_radiation_walker_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_radiation_walker_badge" }
]
```

---

## 套装九十：赛博行者（Cyber Walker）饰品

> *"赛博行者不是在升级——他是在进化，而进化没有终点。"*

---

### 90.5 赛博行者全息披风（Cyber Walker Hologram Cloak）

```gdscript
item_id = "acc_cyber_walker_cloak"
display_name = "赛博行者全息披风"
description = "一件由全息丝与数据布编织而成的披风，披风表面不断有微型全息图像在流动和变化——不是装饰，而是真正的全息投影被封印在了丝线中。当穿戴者移动时，披风会自动生成多个全息分身，迷惑敌人。在战斗中，披风可以投影出任何形象，让敌人无法确定真正的目标。\n\n这件披风是赛博行者在「赛博工坊」中，从自己的全息装置上取下的碎片编织的。他说："这件披风让我能够将全息的力量带到任何地方。现实只是数据——学会了操纵数据，就学会了操纵现实。"\n\n披风的效果是：隐匿检定 +3（全息掩护），且每日一次，可以释放「全息分身」——创造 1D4+1 个全息分身（1 HP，AC = 你的 AC），持续 2 回合。分身可以进行攻击（+5，1D8+3 force）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "cyber_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_cyber_walker_cloak" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_cyber_walker_cloak" }
]
```

---

### 90.6 赛博行者数据核心项链（Cyber Walker Data Core Necklace）

```gdscript
item_id = "acc_cyber_walker_necklace"
display_name = "赛博行者数据核心项链"
description = "一条由数据金与量子丝编织而成的项链，吊坠是一颗数据核心——一颗由纯粹数据凝结的核心。核心内部不断有微型数据流在闪烁，如同一个小型的网络。据说这颗核心来自赛博之神的数据库，他在创造赛博空间时，将自己的一部分力量封印在了其中。\n\n这条项链是赛博行者在「赛博神殿」中，从赛博之神的祭坛上取出的。他说："这颗核心承载了我的数据之力。它让我能够接入任何网络，也让我能够理解数据的本质。"\n\n项链的效果是：调查检定 +3（数据解析），且每日一次，可以释放「数据入侵」——指定一个 20 尺内的敌人（必须是 construct 或有机械部件的生物），其 stunned 1 回合（系统崩溃，DC17 智力豁免抵抗），且下一回合攻击检定 -3（数据混乱）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "cyber_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_cyber_walker_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_cyber_walker_necklace" }
]
```

---

### 90.7 赛博行者黑客之戒（Cyber Walker Hack Ring）

```gdscript
item_id = "acc_cyber_walker_ring_1"
display_name = "赛博行者黑客之戒"
description = "一枚由黑客石与数据金铸造的戒指，戒指表面刻有微型黑客图案。当佩戴者集中精神时，戒指会释放出一股黑客之力，使佩戴者可以「入侵」目标的系统。这种入侵不是数字的——它是概念性的，可以影响任何有系统的目标：机械、生物、甚至是魔法。\n\n这枚戒指是赛博行者在「黑客深渊」中，从黑客之神那里得到的。黑客之神说："这枚戒指让你能够借用我的力量。但黑客是有代价的——每次入侵，你都会暴露自己。"\n\n戒指的效果是：每日一次，可以释放「系统入侵」——指定一个 15 尺内的目标：\n- 如果是 construct：stunned 2 回合（系统崩溃，DC17 智力豁免 half duration）\n- 如果是生物：其一项能力被封印 2 回合（DC16 智慧豁免抵抗）\n- 如果是魔法物品：其效果被反转 2 回合"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "cyber_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_cyber_walker_ring_1" }
]
```

---

### 90.8 赛博行者防火墙之戒（Cyber Walker Firewall Ring）

```gdscript
item_id = "acc_cyber_walker_ring_2"
display_name = "赛博行者防火墙之戒"
description = "一枚由防火墙石与数据银铸造的戒指，戒指表面刻有微型防火墙图案。当佩戴者集中精神时，戒指会释放出一股防火墙之力，在佩戴者周围形成一道数据护盾。这道护盾可以阻挡物理攻击、魔法攻击、甚至是概念攻击。但防火墙需要能量——每次阻挡攻击，会消耗佩戴者的 mana。\n\n这枚戒指是赛博行者在「防火墙深渊」中，从防火墙之神那里得到的。防火墙之神说："这枚戒指让你能够借用我的力量。但防火墙不是万能的——它只能阻挡已知的威胁。"\n\n戒指的效果是：每日一次，可以释放「数据护盾」——获得 20 点临时 HP（数据护盾），且免疫所有非物理效果（魔法、毒素、诅咒）2 回合。护盾被击破时，对 5 尺内所有敌人造成 2D8 force（数据爆炸）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "cyber_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_intelligence", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cyber_walker_ring_2" }
]
```

---

### 90.9 赛博行者赛博芯片（Cyber Walker Cyber Chip）

```gdscript
item_id = "acc_cyber_walker_trinket"
display_name = "赛博行者赛博芯片"
description = "一个由数据金属与魔法制成的微型芯片，芯片表面不断有微型数据流在闪烁。这个芯片不是普通的计算机零件——它是活的。当佩戴者需要时，可以激活芯片，「上传」自己的意识进入赛博空间。在赛博空间中，佩戴者可以操纵数据、入侵系统、甚至改变现实。但芯片有风险——如果意识无法返回，佩戴者会变成植物人。\n\n这个芯片是赛博行者在「赛博工坊」中，从自己的大脑中取出的。他说："这个芯片承载了我的意识。它让我能够进入赛博空间，也让我能够理解数据的本质。"\n\n芯片的效果是：每日一次，可以释放「赛博空间」——进入赛博空间 1 回合：完全免疫所有伤害，可以指定一个 30 尺内的敌人，其受到 4D10 force（数据冲击，DC17 智力豁免 half）且 stunned 1 回合（系统过载）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "cyber_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_cyber_walker_trinket" }
]
```

---

### 90.10 赛博行者徽章（Cyber Walker Badge）

```gdscript
item_id = "acc_cyber_walker_badge"
display_name = "赛博行者徽章"
description = "一枚由数据金与芯片石铸造的徽章，徽章上刻着一个芯片和一个电路——赛博行者的标志。徽章背面刻着一行小字：「数据即现实，代码即命运。」这枚徽章是赛博公会的信物，拥有它意味着你已被赛博空间认可。\n\n徽章的效果是：10 尺内所有友方对 construct 攻击检定 +2（赛博优势）。且每日一次，可以释放「数据领域」——10 尺半径内所有 construct 攻击检定 -2（数据干扰），且友方对 construct 伤害 +1D6（数据增强），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "cyber_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cyber_walker_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_cyber_walker_badge" }
]
```

---

*套装 81–90 饰品部分完结 · 共 60 件饰品装备*

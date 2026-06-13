# 传奇装备套装饰品设计文档（套装 11–20）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装十一：星辰编织者（Star Weaver）饰品

> *"星辰编织者不是在仰望星空——他是在与星辰对话，而每一颗星星都在回应。"*

---

### 11.5 星辰编织者星图披风（Star Weaver Star Chart Cloak）

```gdscript
item_id = "acc_star_weaver_11_cloak"
display_name = "星辰编织者星图披风"
description = "一件由星辰丝与夜空布编织而成的披风，披风表面绣着一幅完整的星图——不是任何已知的星座，而是一幅从未被人类见过的星图。这幅星图来自星辰编织者在「无尽星空」中的旅行记录，每一颗星星都代表一个他曾经到访的世界。当穿戴者在夜晚行走时，披风会自动与天空中的星辰共鸣，发出微弱的银色光芒。\n\n这件披风是星辰编织者在「观星塔」中，从自己的星图笔记上取下的碎片编织的。他说："这件披风是我的星图，我的记忆，我的道路。每一颗星星都是一个故事，每一个故事都是一次冒险。"\n\n披风的效果是：在夜晚感知检定 +3（星辰之眼），且不会迷路（星辰指引）。每日一次，可以释放「星辰导航」——指定一个 30 尺内的点，可以 bonus action 传送到该点（星辰步），路径上的敌人须通过 DC14 敏捷豁免，失败则受到 1D8 radiant（星光擦过）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "star_weaver_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_star_weaver_11_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_weaver_11_cloak" }
]
```

---

### 11.6 星辰编织者北极星项链（Star Weaver Polaris Necklace）

```gdscript
item_id = "acc_star_weaver_11_necklace"
display_name = "星辰编织者北极星项链"
description = "一条由星辰金与北极光水晶编织而成的项链，吊坠是一颗微型北极星——这颗星星不是普通的宝石，它是真正的星星碎片。星辰编织者在「北极星神殿」中，从神殿的核心取出了这块碎片。据说北极星是所有迷失者的灯塔，无论你身在何处，只要能看到北极星，就能找到回家的路。\n\n这条项链是星辰编织者在迷失于「虚空星海」时，依靠北极星的指引找到归途后制作的。他说："这颗北极星是我的灯塔。在最黑暗的时刻，它让我知道方向。"\n\n项链的效果是：不会在野外迷路（北极星指引），且每日一次，可以释放「北极星光」——自身或 10 尺内一个友方恢复 2D8 HP（星光治愈），并移除 frightened 和 confused 状态（星光清明）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "star_weaver_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_star_weaver_11_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_weaver_11_necklace" }
]
```

---

### 11.7 星辰编织者流星之戒（Star Weaver Meteor Ring）

```gdscript
item_id = "acc_star_weaver_11_ring_1"
display_name = "星辰编织者流星之戒"
description = "一枚由流星铁与星辰金铸造的戒指，戒指表面刻有微型流星图案。当佩戴者集中精神时，戒指会释放出一股流星之力，使佩戴者的下一次攻击如同流星般迅猛。这种力量不是魔法的——它是星辰的，是宇宙的延伸。据说这枚戒指来自一颗真正的流星，星辰编织者在流星坠落的那一刻，从陨石核心中取出了这块金属。\n\n这枚戒指是星辰编织者在「流星陨落之地」中，从陨石核心中取出的。他说："这枚戒指让我能够借用流星的力量。流星是宇宙的使者——它带来了远方的消息。"\n\n戒指的效果是：每日一次，下一次攻击变为 ranged 15/30 射程（流星投掷），伤害 +2D6 radiant（流星之光），且目标须通过 DC15 敏捷豁免，失败则 knocked prone（流星冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "star_weaver_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_11_ring_1" }
]
```

---

### 11.8 星辰编织者银河之戒（Star Weaver Galaxy Ring）

```gdscript
item_id = "acc_star_weaver_11_ring_2"
display_name = "星辰编织者银河之戒"
description = "一枚由银河石与星辰银铸造的戒指，戒指内部有一幅微型银河在缓慢旋转。当佩戴者集中精神时，戒指会释放出一股银河之力，使佩戴者可以借用银河的引力。这种力量不是魔法的——它是宇宙的，是引力的延伸。据说这枚戒指来自银河的核心，星辰编织者在穿越银河时，从核心中取出了这块石头。\n\n这枚戒指是星辰编织者在「银河之心」中，从银河核心取出的。他说："这枚戒指让我能够借用银河的力量。银河是宇宙的河流——它连接了一切，也分离了一切。"\n\n戒指的效果是：每日一次，可以释放「银河引力」——指定一个 15 尺内的敌人，其被拉向佩戴者 10 尺（DC15 力量豁免抵抗），且受到 2D6 bludgeoning（引力冲击）。或者指定一个友方，将其推出危险区域 10 尺（银河推力）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "star_weaver_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_11_ring_2" }
]
```

---

### 11.9 星辰编织者观星镜（Star Weaver Star Gazer）

```gdscript
item_id = "acc_star_weaver_11_trinket"
display_name = "星辰编织者观星镜"
description = "一个由星辰水晶与魔法制成的微型望远镜，望远镜表面不断有微型星辰在闪烁。这个望远镜不是普通的观星工具——它可以看穿时间。当佩戴者通过它凝视星辰时，可以看到星辰的过去和未来：有的星辰正在诞生，有的正在死亡，有的已经化为虚无。据说这个望远镜来自「时间之星」，星辰编织者在穿越时间线时，从这颗星星上取下了这块水晶。\n\n这个望远镜是星辰编织者在「时间之星」上，从自己的观星仪式中取出的。他说："这个望远镜让我能够看见时间的流动。星辰不是静止的——它们在讲述宇宙的故事。"\n\n望远镜的效果是：每日一次，可以释放「星辰预言」——指定一个 20 尺内的敌人，预见其下一回合的行动：可以选择使其下一回合 attack miss（星辰干涉），或使其下一回合受到的攻击自动命中（星辰锁定）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "star_weaver_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_star_weaver_11_trinket" }
]
```

---

### 11.10 星辰编织者徽章（Star Weaver Badge）

```gdscript
item_id = "acc_star_weaver_11_badge"
display_name = "星辰编织者徽章"
description = "一枚由星辰金与夜空石铸造的徽章，徽章上刻着一颗星星和一根编织针——星辰编织者的标志。徽章背面刻着一行小字：「星辰为线，夜空为布。」这枚徽章是星辰旅者的信物，拥有它意味着你已被星辰认可。\n\n徽章的效果是：10 尺内所有友方在夜晚攻击检定 +1（星辰之力）。且每日一次，可以释放「星光领域」——10 尺半径内友方 AC +1（星光守护），且每回合开始时恢复 1D6 HP（星光滋养），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "star_weaver_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_11_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_weaver_11_badge" }
]
```

---

## 套装十二：毒蛇之吻（Viper's Kiss）饰品

> *"毒蛇之吻不是致命的——它只是告诉你，美丽的东西往往最危险。"*

---

### 12.5 毒蛇之吻蛇鳞披风（Viper's Kiss Scale Cloak）

```gdscript
item_id = "acc_vipers_kiss_cloak"
display_name = "毒蛇之吻蛇鳞披风"
description = "一件由毒蛇鳞片与毒丝编织而成的披风，披风表面覆盖着微型蛇鳞，每一片鳞片都来自不同的毒蛇：眼镜蛇、响尾蛇、蝰蛇、甚至是传说中的多头蛇。当穿戴者移动时，鳞片会相互摩擦，发出沙沙的声音，如同蛇在草丛中滑行。在敌人靠近时，鳞片会自动竖起，释放微量毒素。\n\n这件披风是毒蛇之吻在「蛇穴」中，从自己的宠物蛇身上取下的鳞片编织的。她说："这件披风让我成为了蛇的一部分。它会保护我，也会警告敌人——不要靠近。"\n\n披风的效果是：poison 抗性 +15（蛇毒免疫），且近战攻击者受到 1D4 poison（蛇鳞反击）。当穿戴者被 grapple 时，攻击者受到 2D6 poison（蛇缠毒素）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "vipers_kiss_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_poison", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_vipers_kiss_cloak" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_vipers_kiss_cloak" }
]
```

---

### 12.6 毒蛇之吻毒牙项链（Viper's Kiss Fang Necklace）

```gdscript
item_id = "acc_vipers_kiss_necklace"
display_name = "毒蛇之吻毒牙项链"
description = "一条由蛇骨与毒金编织而成的项链，吊坠是一颗真正的毒蛇牙齿——这颗牙齿来自一条活了三百年的眼镜蛇王。牙齿内部有微型毒液在流动，发出微弱的绿光。据说这颗牙齿含有眼镜蛇王毕生的毒素精华，一滴就足以毒死一头龙。\n\n这条项链是毒蛇之吻在「眼镜蛇王墓」中，从蛇王的遗骸上取下的。她说："这颗牙齿承载了我的毒之力量。它不是用来杀戮的——它是用来保护的。"\n\n项链的效果是：自然检定 +3（毒物知识），且每日一次，可以释放「毒牙撕咬」——指定一个 5 尺内的敌人，受到 2D8 piercing + 3D8 poison（毒牙注入），目标须通过 DC16 体质豁免，失败则 poisoned 2 回合且 paralyzed 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "vipers_kiss_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_vipers_kiss_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_vipers_kiss_necklace" }
]
```

---

### 12.7 毒蛇之吻蛇信之戒（Viper's Kiss Tongue Ring）

```gdscript
item_id = "acc_vipers_kiss_ring_1"
display_name = "毒蛇之吻蛇信之戒"
description = "一枚由蛇信石与毒金铸造的戒指，戒指表面刻有微型蛇信图案。当佩戴者集中精神时，戒指会释放出一股蛇信之力，使佩戴者可以感知周围的热量变化。这种感知不是普通的——它可以穿透墙壁，可以感知隐形生物，甚至可以感知生物的情绪。据说这枚戒指来自一位蛇人祭司，他用这枚戒指感知到了敌人的每一次心跳。\n\n这枚戒指是毒蛇之吻在「蛇人神殿」中，从蛇人祭司的遗骸上取下的。她说："这枚戒指让我能够借用蛇的感知。蛇不是靠眼睛看世界的——它是靠热量，靠振动，靠气味。"\n\n戒指的效果是：感知检定 +2（热感），且可以感知 30 尺内的隐形生物和隐藏陷阱（热感视觉）。每日一次，可以释放「蛇信探测」——感知 60 尺内所有生物的位置和 HP（热感扫描）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "vipers_kiss_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_vipers_kiss_ring_1" }
]
```

---

### 12.8 毒蛇之吻蜕皮之戒（Viper's Kiss Shedding Ring）

```gdscript
item_id = "acc_vipers_kiss_ring_2"
display_name = "毒蛇之吻蜕皮之戒"
description = "一枚由蜕皮石与毒银铸造的戒指，戒指表面覆盖着一层薄薄的蛇皮。当佩戴者集中精神时，戒指会释放出一股蜕皮之力，使佩戴者可以「蜕皮」，摆脱所有的负面状态。这种蜕皮不是物理的——它是精神的，是重生的象征。据说这枚戒指来自一位蛇人女王，她在每次蜕皮后都会变得更加美丽和强大。\n\n这枚戒指是毒蛇之吻在「蛇人女王宫」中，从女王的蜕皮中取下的碎片铸造的。她说："这枚戒指让我能够借用蜕皮的力量。蜕皮不是软弱——它是成长，是更新，是变得更加强大。"\n\n戒指的效果是：每日一次，可以释放「蜕皮」——移除自身所有非 legendary 负面状态（中毒、疾病、诅咒、恐惧等），且恢复 2D8 HP（新生治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "vipers_kiss_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_vipers_kiss_ring_2" }
]
```

---

### 12.9 毒蛇之吻蛇笛（Viper's Kiss Snake Flute）

```gdscript
item_id = "acc_vipers_kiss_trinket"
display_name = "毒蛇之吻蛇笛"
description = "一支由蛇骨与毒木制成的微型笛子，笛子表面刻有蛇的图案。这支笛子不是普通的乐器——它是蛇的语言。当佩戴者吹奏它时，可以召唤蛇类，命令它们攻击敌人或保护盟友。据说这支笛子来自「蛇之女神」，她在创造第一条蛇时，用这支笛子赋予了蛇生命。\n\n这支笛子是毒蛇之吻在「蛇之女神殿」中，从女神的祭坛上取出的。她说："这支笛子让我能够与蛇对话。蛇不是邪恶的——它们只是被误解了。"\n\n笛子的效果是：每日一次，可以吹奏蛇笛——召唤 2D4 条魔法蛇（HP 10，AC 12，攻击 +4，1D6+2 piercing + 1D4 poison），持续 1 分钟。可以用 bonus action 指挥蛇群。或者选择释放「蛇群缠绕」——指定一个 15 尺内的敌人，其 restrained 2 回合（蛇群缠绕，DC16 力量豁免挣脱），且每回合受到 2D6 poison。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "vipers_kiss_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_vipers_kiss_trinket" }
]
```

---

### 12.10 毒蛇之吻徽章（Viper's Kiss Badge）

```gdscript
item_id = "acc_vipers_kiss_badge"
display_name = "毒蛇之吻徽章"
description = "一枚由毒金与蛇鳞石铸造的徽章，徽章上刻着一条蛇和一个嘴唇——毒蛇之吻的标志。徽章背面刻着一行小字：「美丽即危险，危险即美丽。」这枚徽章是毒蛇公会的信物，拥有它意味着你已被蛇认可。\n\n徽章的效果是：10 尺内所有友方 poison 抗性 +5（蛇毒庇护）。且每日一次，可以释放「毒蛇领域」——10 尺半径内所有敌人每回合开始时受到 1D6 poison（蛇毒侵蚀），且移动力 -5 尺（蛇毒麻痹），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "vipers_kiss_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_vipers_kiss_badge" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_vipers_kiss_badge" }
]
```

---

## 套装十三：北风旅者（Northwind Traveler）饰品

> *"北风旅者不是在抵抗寒冷——他就是寒冷本身，而寒冷只是另一种自由。"*

---

### 13.5 北风旅者风雪披风（Northwind Traveler Blizzard Cloak）

```gdscript
item_id = "acc_northwind_traveler_cloak"
display_name = "北风旅者风雪披风"
description = "一件由北极熊毛与风雪丝编织而成的披风，披风表面不断有微型雪花在飘落和融化——不是装饰，而是真正的冰雪被封印在了丝线中。当穿戴者在寒冷环境中行走时，披风会自动与风雪融合，使穿戴者几乎不可见。据说这件披风是北风旅者在「永恒冰原」中，从第一只北极熊身上取下的毛发编织的。\n\n这件披风是北风旅者在「冰风谷」中，从自己的生存装备上取下的碎片编织的。他说："这件披风让我成为了风的一部分。我可以穿越任何暴风雪，消失在任何一个雪堆中。"\n\n披风的效果是：cold 抗性 +20（风雪之躯），且在暴风雪/冰雪环境中完全隐形（如同 greater invisibility）。免疫 difficult terrain caused by ice/snow。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "northwind_traveler_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_cold", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_northwind_traveler_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_northwind_traveler_cloak" }
]
```

---

### 13.6 北风旅者冰晶项链（Northwind Traveler Ice Crystal Necklace）

```gdscript
item_id = "acc_northwind_traveler_necklace"
display_name = "北风旅者冰晶项链"
description = "一条由冰晶与风雪金编织而成的项链，吊坠是一颗永恒冰晶——这颗冰晶来自「世界之冰」，一块从未融化的冰块。冰晶内部有一朵微型雪花，雪花永远在旋转，发出微弱的蓝光。据说这颗冰晶包含了北风的力量，可以召唤暴风雪，也可以冻结一切。\n\n这条项链是北风旅者在「世界之冰」前，从冰块中心取出的。他说："这颗冰晶承载了我的北风之力。它让我能够控制寒冷，也让我能够理解冰的语言。"\n\n项链的效果是：自然检定 +3（冰雪知识），且每日一次，可以释放「冰晶爆发」——指定一个 20 尺内的敌人，其受到 3D10 cold（DC16 体质豁免 half），且移动力 -15 尺 2 回合（冰冻）。或者指定一个友方，其获得 ice armor 2 回合：AC +2，且近战攻击者受到 1D6 cold（冰霜反击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "northwind_traveler_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_northwind_traveler_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_northwind_traveler_necklace" }
]
```

---

### 13.7 北风旅者霜冻之戒（Northwind Traveler Frost Ring）

```gdscript
item_id = "acc_northwind_traveler_ring_1"
display_name = "北风旅者霜冻之戒"
description = "一枚由霜冻石与风雪金铸造的戒指，戒指表面刻有微型霜冻图案。当佩戴者集中精神时，戒指会释放出一股霜冻之力，使佩戴者的武器被冰霜包裹。这种冰霜不会融化——它是永恒的，是北风的力量。据说这枚戒指来自一位冰霜巨人，他用这枚戒指冻结了一条河流。\n\n这枚戒指是北风旅者在「冰霜巨人遗迹」中，从巨人的遗骸上取下的。他说："这枚戒指让我能够借用霜冻的力量。霜冻不是死亡——它是保护，是保存，是永恒。"\n\n戒指的效果是：所有攻击附加 1D6 cold（霜冻），且每日一次，可以释放「霜冻之触」——指定一个 5 尺内的敌人，其受到 3D8 cold（DC15 体质豁免 half），且武器被冻结 2 回合（攻击检定 -2）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "northwind_traveler_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_northwind_traveler_ring_1" }
]
```

---

### 13.8 北风旅者寒风之戒（Northwind Traveler Gale Ring）

```gdscript
item_id = "acc_northwind_traveler_ring_2"
display_name = "北风旅者寒风之戒"
description = "一枚由寒风石与风雪银铸造的戒指，戒指表面刻有微型寒风图案。当佩戴者集中精神时，戒指会释放出一股寒风之力，使周围充满刺骨的寒风。这种寒风不仅可以冻结敌人，还可以推动自己或盟友移动。据说这枚戒指来自一位风神，他用这枚戒指创造了第一场暴风雪。\n\n这枚戒指是北风旅者在「风神祭坛」上，从风神的遗物中取出的。他说："这枚戒指让我能够借用寒风的力量。寒风不是敌人——它是道路，是力量，是自由。"\n\n戒指的效果是：移动力 +10 尺（寒风助推），且每日一次，可以释放「寒风冲击」——15 尺锥形，所有敌人受到 2D8 cold（DC15 体质豁免 half）且 knocked prone（寒风吹倒）。友方在锥形区域内移动力 +10 尺 1 回合（寒风加速）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "northwind_traveler_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_northwind_traveler_ring_2" }
]
```

---

### 13.9 北风旅者冰狼图腾（Northwind Traveler Ice Wolf Totem）

```gdscript
item_id = "acc_northwind_traveler_trinket"
display_name = "北风旅者冰狼图腾"
description = "一个由冰狼骨与风雪之力制成的微型图腾，图腾表面刻着冰狼的面孔。这个图腾不是普通的装饰品——它是活的。当佩戴者需要时，可以激活图腾，召唤一只冰狼的灵魂。这只冰狼会保护佩戴者，攻击敌人，或者在暴风雪中为佩戴者引路。但图腾需要尊重——如果不尊重冰狼，它会拒绝帮助。\n\n这个图腾是北风旅者在「冰狼圣山」上，从冰狼王的遗骸中取出的。他说："这个图腾承载了我的冰狼伙伴。它让我能够与冰狼同行，也让我能够理解荒野的语言。"\n\n图腾的效果是：每日一次，可以召唤「冰狼灵魂」——召唤一只冰狼（HP 25，AC 15，攻击 +5，2D6+3 cold/slashing，移动力 50 尺），持续 1 分钟。冰狼可以 bonus action 命令，且可以在冰雪地形上隐形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "northwind_traveler_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_northwind_traveler_trinket" }
]
```

---

### 13.10 北风旅者徽章（Northwind Traveler Badge）

```gdscript
item_id = "acc_northwind_traveler_badge"
display_name = "北风旅者徽章"
description = "一枚由风雪金与冰晶石铸造的徽章，徽章上刻着一片雪花和一个脚印——北风旅者的标志。徽章背面刻着一行小字：「北风即道路，寒冷即自由。」这枚徽章是北风旅者的信物，拥有它意味着你已被寒冷认可。\n\n徽章的效果是：10 尺内所有友方 cold 抗性 +5（风雪庇护）。且每日一次，可以释放「风雪领域」——10 尺半径内所有敌人每回合开始时受到 1D6 cold（风雪侵蚀），且移动力 -5 尺（冰雪地面），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "northwind_traveler_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_northwind_traveler_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_northwind_traveler_badge" }
]
```

---

## 套装十四：虚空行者（Void Walker）饰品

> *"虚空行者不是在探索虚空——他是在虚空之中寻找自己失落的倒影。"*

---

### 14.5 虚空行者虚空披风（Void Walker Void Cloak）

```gdscript
item_id = "acc_void_walker_cloak"
display_name = "虚空行者虚空披风"
description = "一件由虚空丝与暗影布编织而成的披风，披风表面不断有微型虚空在吞噬和膨胀——不是装饰，而是真正的虚空被封印在了丝线中。当穿戴者移动时，披风会自动与周围的阴影融合，使穿戴者几乎不可见。在完全黑暗中，披风会让穿戴者完全消失，如同从未存在过。\n\n这件披风是虚空行者在「虚空之门」中，从自己的虚空中取下的碎片编织的。他说："这件披风让我成为了虚空的一部分。我可以穿越任何黑暗，隐藏在任何一个角落。但它也让我在阳光下感到不适。"\n\n披风的效果是：force 抗性 +15（虚空之躯），且在黑暗中完全隐形（如同 greater invisibility）。每日一次，可以 bonus action 传送到 20 尺内任何阴影中（虚空步）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "void_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_void_walker_cloak" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_void_walker_cloak" }
]
```

---

### 14.6 虚空行者虚空之眼项链（Void Walker Void Eye Necklace）

```gdscript
item_id = "acc_void_walker_necklace"
display_name = "虚空行者虚空之眼项链"
description = "一条由虚空金与暗影水晶编织而成的项链，吊坠是一颗虚空之眼——一颗从虚空恶魔身上取下的真实眼睛。这只眼睛看到的不是现实——它看到的是现实的反面：光明背后的黑暗，存在背后的虚无。通过这只眼睛，佩戴者可以看到事物的本质：不是表面的样子，而是内在的虚空。\n\n这条项链是虚空行者在「虚空神殿」中，从虚空之主的祭坛上取出的。他说："这只眼睛让我能够看到世界的真相——不是表面的真相，而是虚空背后的真相。"\n\n项链的效果是：洞察检定 +3（虚空之眼），且可以感知 30 尺内生物的「虚空本质」（如同 detect evil and good，但感知的是虚空/存在的对立）。每日一次，可以释放「虚空凝视」——指定一个 15 尺内的敌人，其受到 3D10 force（DC16 智慧豁免 half）且 frightened 1 回合（虚空恐惧）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "void_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_void_walker_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_void_walker_necklace" }
]
```

---

### 14.7 虚空行者吞噬之戒（Void Walker Devour Ring）

```gdscript
item_id = "acc_void_walker_ring_1"
display_name = "虚空行者吞噬之戒"
description = "一枚由吞噬石与虚空金铸造的戒指，戒指表面刻有微型吞噬图案。当佩戴者集中精神时，戒指会释放出一股吞噬之力，将目标的能量吸入虚空。这种吞噬不是物理的——它是虚空的，可以穿透任何防御，直接吞噬目标的 mana 或生命力。据说这枚戒指来自一位虚空行者大师，他用这枚戒指吞噬了一位神明的力量。\n\n这枚戒指是虚空行者在「吞噬深渊」中，从虚空恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用虚空的力量。虚空是饥饿的——它会吞噬一切，包括你自己。"\n\n戒指的效果是：每日一次，可以释放「虚空吞噬」——指定一个 15 尺内的敌人，其失去 2D10 mana（或如果无 mana，则受到 2D10 necrotic），且佩戴者恢复等量 mana（或 HP）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "void_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_walker_ring_1" }
]
```

---

### 14.8 虚空行者虚无之戒（Void Walker Nothingness Ring）

```gdscript
item_id = "acc_void_walker_ring_2"
display_name = "虚空行者虚无之戒"
description = "一枚由虚无石与虚空银铸造的戒指，戒指表面刻有微型虚无图案。当佩戴者集中精神时，戒指会释放出一股虚无之力，使目标的部分存在被抹去。这种虚无不是死亡——它是从现实中删除，是让目标的一部分从未存在过。据说这枚戒指来自一位虚空行者大师，他用这枚戒指让一位敌人的一只手臂从现实中消失了。\n\n这枚戒指是虚空行者在「虚无深渊」中，从虚无之神那里得到的。虚无之神说："这枚戒指让你能够借用虚无的力量。但虚无是不可逆的——一旦被抹去，就永远无法恢复。"\n\n戒指的效果是：每日一次，可以释放「虚无之触」——指定一个 10 尺内的敌人，其受到 3D10 force（DC16 体质豁免 half），且下一回合无法使用一种能力（攻击、施法、或移动，由你选择）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "void_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_walker_ring_2" }
]
```

---

### 14.9 虚空行者虚空碎片（Void Walker Void Shard）

```gdscript
item_id = "acc_void_walker_trinket"
display_name = "虚空行者虚空碎片"
description = "一块由虚空凝结的碎片，碎片表面不断有微型虚空在吞噬和膨胀。这块碎片不是普通的石头——它是活的。当佩戴者需要时，可以激活碎片，创造出一个微型虚空黑洞。这个黑洞可以吞噬光线、物质、甚至魔法。但碎片是危险的——如果不小心，它会吞噬佩戴者。\n\n这块碎片是虚空行者在「虚空之门」中，从虚空本身取下的。他说："这块碎片承载了我的虚空之力。它让我能够控制虚空，也让我能够理解虚无的美丽。但它也让我越来越难以感受到存在的意义。"\n\n碎片的效果是：每日一次，可以释放「虚空黑洞」——指定一个 20 尺内的点，10 尺半径内所有敌人受到 4D10 force（DC17 敏捷豁免 half），且被拉向中心 10 尺（虚空引力）。中心点的敌人受到双倍伤害。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "void_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_void_walker_trinket" }
]
```

---

### 14.10 虚空行者徽章（Void Walker Badge）

```gdscript
item_id = "acc_void_walker_badge"
display_name = "虚空行者徽章"
description = "一枚由虚空金与暗影石铸造的徽章，徽章上刻着一个漩涡和一个脚印——虚空行者的标志。徽章背面刻着一行小字：「虚空即道路，虚无即归宿。」这枚徽章是虚空行者的信物，拥有它意味着你已被虚空认可。\n\n徽章的效果是：10 尺内所有友方 force 抗性 +5（虚空庇护）。且每日一次，可以释放「虚空领域」——10 尺半径内所有敌人攻击检定 -1（虚空干扰），且友方在领域内可以 bonus action 传送到 10 尺内任何位置（虚空步），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "void_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_walker_badge" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_walker_badge" }
]
```

---

## 套装十五：自然之语（Nature's Whisper）饰品

> *"自然之语不是在说话——她只是让风代替她说，让花代替她笑，让雨代替她哭。"*

---

### 15.5 自然之语绿叶披风（Nature's Whisper Leaf Cloak）

```gdscript
item_id = "acc_natures_whisper_cloak"
display_name = "自然之语绿叶披风"
description = "一件由绿叶与藤蔓丝编织而成的披风，披风表面不断有微型叶子在生长和凋零——不是装饰，而是真正的活植物被封印在了丝线中。当穿戴者在森林中行走时，披风会自动与周围的植物融合，使穿戴者几乎不可见。据说这件披风是自然之语在「世界树」下，从世界树的叶片上取下的碎片编织的。\n\n这件披风是自然之语在「翡翠森林」中，从自己的花园中取下的叶子编织的。她说："这件披风让我成为了森林的一部分。我可以与树木对话，与花朵共鸣，与风一起歌唱。"\n\n披风的效果是：在自然环境中（森林/草原/丛林）完全隐形（如同 greater invisibility），且可以感知 30 尺内所有植物和动物的位置（自然之眼）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "natures_whisper_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_natures_whisper_cloak" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_natures_whisper_cloak" }
]
```

---

### 15.6 自然之语生命之种项链（Nature's Whisper Life Seed Necklace）

```gdscript
item_id = "acc_natures_whisper_necklace"
display_name = "自然之语生命之种项链"
description = "一条由生命之种与自然金编织而成的项链，吊坠是一颗生命之种——这颗种子不是普通的种子，它是世界树的第一颗种子。种子内部有一株微型植物在生长，发出微弱的绿光。据说这颗种子包含了生命的全部秘密，从种子到森林，从森林到世界。\n\n这条项链是自然之语在「世界树」下，从树根中取出的第一颗种子制成的。她说："这颗种子承载了我的生命之力。它让我能够创造生命，也让我能够理解自然的循环。"\n\n项链的效果是：自然检定 +3（自然知识），且每日一次，可以释放「生命之种」——指定一个 10 尺内的点，生长出藤蔓：10 尺半径内所有敌人 restrained 2 回合（藤蔓缠绕，DC15 力量豁免挣脱），且友方在藤蔓中每回合恢复 1D8 HP（生命滋养）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "natures_whisper_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_natures_whisper_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_natures_whisper_necklace" }
]
```

---

### 15.7 自然之语花粉之戒（Nature's Whisper Pollen Ring）

```gdscript
item_id = "acc_natures_whisper_ring_1"
display_name = "自然之语花粉之戒"
description = "一枚由花粉石与自然金铸造的戒指，戒指表面刻有微型花朵图案。当佩戴者集中精神时，戒指会释放出一股花粉之力，使周围充满魔法花粉。这些花粉可以治愈盟友，也可以让敌人陷入沉睡或幻觉。据说这枚戒指来自一位花仙子，她用这枚戒指让整个花园在春天绽放。\n\n这枚戒指是自然之语在「花仙子花园」中，从花仙子的花冠上取下的碎片铸造的。她说："这枚戒指让我能够借用花粉的力量。花粉是自然的信使——它带来了生命的消息。"\n\n戒指的效果是：每日一次，可以释放「花粉风暴」——10 尺半径内所有友方恢复 2D8 HP（花粉治愈），且所有敌人须通过 DC15 体质豁免，失败则 poisoned 1 回合（花粉幻觉）或 sleep 1 回合（花粉安眠，由佩戴者选择）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "natures_whisper_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_natures_whisper_ring_1" }
]
```

---

### 15.8 自然之语根系之戒（Nature's Whisper Root Ring）

```gdscript
item_id = "acc_natures_whisper_ring_2"
display_name = "自然之语根系之戒"
description = "一枚由根系石与自然银铸造的戒指，戒指表面刻有微型根系图案。当佩戴者集中精神时，戒指会释放出一股根系之力，使佩戴者的脚下长出微型根系，扎根于大地。这种扎根会使佩戴者无法移动，但会极大地增强防御力和恢复力。据说这枚戒指来自世界树的根系，自然之语在触摸世界树时，根系自动缠绕了她的手指。\n\n这枚戒指是自然之语在「世界树」下，从树根上取下的碎片铸造的。她说："这枚戒指让我能够借用大地的力量。大地是生命的母亲——它会保护所有扎根于它的孩子。"\n\n戒指的效果是：站立不动时，每回合开始时恢复 2D6 HP（根系治愈），且 AC +2（扎根稳固）。但移动力归零（扎根束缚，可以用 bonus action 解除）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "natures_whisper_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_natures_whisper_ring_2" }
]
```

---

### 15.9 自然之语精灵之瓶（Nature's Whisper Fairy Bottle）

```gdscript
item_id = "acc_natures_whisper_trinket"
display_name = "自然之语精灵之瓶"
description = "一个由水晶与魔法制成的微型瓶子，瓶子内部有一只微型自然精灵在飞舞。这只精灵不是普通的生物——它是自然的化身，可以操控植物、治愈伤痛、甚至召唤风暴。但精灵是自由的——如果不喜欢佩戴者，它会拒绝帮助。\n\n这个瓶子是自然之语在「精灵森林」中，从一只受伤的精灵那里得到的。精灵说："这个瓶子让我能够随时帮助你。但记住——自然是自由的，不受任何人的控制。"\n\n瓶子的效果是：每日一次，可以释放「精灵之力」——选择一个效果：\n- 治愈：10 尺内所有友方恢复 3D8 HP\n- 缠绕：15 尺内一个敌人 restrained 2 回合（DC16 力量豁免挣脱）\n- 风暴：15 尺半径内所有敌人受到 2D8 lightning（DC15 敏捷豁免 half）且 knocked prone\n- 绽放：10 尺半径内所有友方移除一个非 legendary 负面状态"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "natures_whisper_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_natures_whisper_trinket" }
]
```

---

### 15.10 自然之语徽章（Nature's Whisper Badge）

```gdscript
item_id = "acc_natures_whisper_badge"
display_name = "自然之语徽章"
description = "一枚由自然金与叶石铸造的徽章，徽章上刻着一片叶子和一滴水——自然之语的标志。徽章背面刻着一行小字：「自然即语言，倾听即理解。」这枚徽章是自然公会的信物，拥有它意味着你已被自然认可。\n\n徽章的效果是：10 尺内所有友方在自然环境中 AC +1（自然庇护）。且每日一次，可以释放「自然领域」——10 尺半径内友方每回合开始时恢复 1D6 HP（自然滋养），且敌人移动力 -5 尺（藤蔓缠绕），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "natures_whisper_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_natures_whisper_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_natures_whisper_badge" }
]
```

---

*套装 11–15 饰品部分（30/60 件）*

## 套装十六：深渊凝视者（Abyss Gazer）饰品

> *"深渊凝视者不是在凝视深渊——他是在与深渊对视，而深渊也在看着他。"*

---

### 16.5 深渊凝视者深渊披风（Abyss Gazer Abyss Cloak）

```gdscript
item_id = "acc_abyss_gazer_cloak"
display_name = "深渊凝视者深渊披风"
description = "一件由深渊丝与黑暗布编织而成的披风，披风表面不断有微型深渊在旋转和吞噬——不是装饰，而是真正的深渊被封印在了丝线中。当穿戴者凝视披风时，会看到无尽的黑暗，黑暗中仿佛有无数的眼睛在回望着你。据说这件披风是深渊凝视者在「深渊边缘」中，从深渊本身取下的碎片编织的。\n\n这件披风是深渊凝视者在「深渊祭坛」上，从自己的深渊之力中取下的碎片编织的。他说："这件披风让我能够与深渊对视。深渊不是敌人——它是镜子，让你看见自己最深的恐惧。"\n\n披风的效果是：psychic 抗性 +15（深渊之躯），且免疫 frightened（深渊麻木）。当穿戴者被攻击时，有 25% 概率触发「深渊反击」——攻击者受到 2D10 psychic（深渊回视，DC15 智慧豁免 half）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "abyss_gazer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_psychic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_abyss_gazer_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_gazer_cloak" }
]
```

---

### 16.6 深渊凝视者深渊之眼项链（Abyss Gazer Abyss Eye Necklace）

```gdscript
item_id = "acc_abyss_gazer_necklace"
display_name = "深渊凝视者深渊之眼项链"
description = "一条由深渊金与黑暗水晶编织而成的项链，吊坠是一颗深渊之眼——一颗从深渊之主身上取下的真实眼睛。这只眼睛看到的不是现实——它看到的是深渊的底部，那里没有光，没有声音，只有无尽的黑暗和寂静。凝视这只眼睛的人会看到自己的内心深处，那里隐藏着最深的恐惧和秘密。\n\n这条项链是深渊凝视者在「深渊神殿」中，从深渊之主的祭坛上取出的。他说："这只眼睛让我能够窥视深渊。深渊不是外在的——它在每个人的心中。"\n\n项链的效果是：奥秘检定 +3（深渊知识），且每日一次，可以释放「深渊凝视」——指定一个 15 尺内的敌人，其受到 3D10 psychic（DC16 智慧豁免 half）且 frightened 2 回合（深渊恐惧）。如果目标在恐惧中 HP 降至 0，其陷入 coma 而非死亡（深渊囚禁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "abyss_gazer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_gazer_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_gazer_necklace" }
]
```

---

### 16.7 深渊凝视者疯狂之戒（Abyss Gazer Madness Ring）

```gdscript
item_id = "acc_abyss_gazer_ring_1"
display_name = "深渊凝视者疯狂之戒"
description = "一枚由疯狂石与深渊金铸造的戒指，戒指表面刻有微型疯狂图案。当佩戴者集中精神时，戒指会释放出一股疯狂之力，使目标陷入幻觉和疯狂。这种疯狂不是普通的混乱——它是深渊的馈赠，是看到真相的代价。据说这枚戒指来自一位深渊先知，他在凝视深渊后获得了无尽的知识，但也失去了理智。\n\n这枚戒指是深渊凝视者在「疯狂深渊」中，从深渊先知的遗骸上取下的。他说："这枚戒指让我能够借用疯狂的力量。疯狂不是缺陷——它是另一种视角，是看到常人看不到的东西。"\n\n戒指的效果是：每日一次，可以释放「深渊低语」——指定一个 15 尺内的敌人，其陷入混乱 2 回合（attack 最近的生物，无论敌友，DC16 智慧豁免抵抗）。且每回合开始时受到 1D10 psychic（低语侵蚀）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "abyss_gazer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_gazer_ring_1" }
]
```

---

### 16.8 深渊凝视者黑暗之戒（Abyss Gazer Darkness Ring）

```gdscript
item_id = "acc_abyss_gazer_ring_2"
display_name = "深渊凝视者黑暗之戒"
description = "一枚由黑暗石与深渊银铸造的戒指，戒指表面刻有微型黑暗图案。当佩戴者集中精神时，戒指会释放出一股黑暗之力，使周围陷入绝对的黑暗。这种黑暗不是普通的 night——它是深渊的黑暗，可以吞噬光线、声音、甚至希望。据说这枚戒指来自一位黑暗之神，他用这枚戒指让整个世界陷入了永恒的黑暗。\n\n这枚戒指是深渊凝视者在「黑暗深渊」中，从黑暗之神的祭坛上取出的。他说："这枚戒指让我能够借用黑暗的力量。黑暗不是邪恶——它是休息，是准备，是新的开始。"\n\n戒指的效果是：隐匿检定 +2（黑暗掩护），且每日一次，可以释放「绝对黑暗」——15 尺半径内陷入 magical darkness（如同 darkness spell），持续 2 回合。佩戴者在黑暗中可以正常视物（深渊视觉）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "abyss_gazer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_gazer_ring_2" }
]
```

---

### 16.9 深渊凝视者深渊之镜（Abyss Gazer Abyss Mirror）

```gdscript
item_id = "acc_abyss_gazer_trinket"
display_name = "深渊凝视者深渊之镜"
description = "一面由深渊凝结的镜子，镜面不断有微型深渊在旋转和吞噬。这面镜子不是普通的镜子——它显示的不是你的倒影，而是你的内心深处。当你凝视它时，你会看到自己最深的恐惧、最大的欲望、最黑暗的秘密。据说这面镜子来自深渊本身，深渊凝视者在凝视它后，终于理解了深渊的真谛。\n\n这面镜子是深渊凝视者在「深渊之心」中，从深渊本身取下的。他说："这面镜子让我能够看见真实的自己。深渊不是外在的——它在每个人的心中。"\n\n镜子的效果是：每日一次，可以释放「深渊映像」——指定一个 15 尺内的敌人，其被迫凝视深渊之镜：stunned 1 回合（看到自己的恐惧），随后 frightened 2 回合（DC17 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "abyss_gazer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_abyss_gazer_trinket" }
]
```

---

### 16.10 深渊凝视者徽章（Abyss Gazer Badge）

```gdscript
item_id = "acc_abyss_gazer_badge"
display_name = "深渊凝视者徽章"
description = "一枚由深渊金与黑暗石铸造的徽章，徽章上刻着一个眼睛和一个漩涡——深渊凝视者的标志。徽章背面刻着一行小字：「凝视深渊，深渊亦凝视你。」这枚徽章是深渊公会的信物，拥有它意味着你已被深渊认可。\n\n徽章的效果是：10 尺内所有友方 psychic 抗性 +5（深渊庇护）。且每日一次，可以释放「深渊领域」——10 尺半径内所有敌人每回合开始时受到 1D6 psychic（深渊侵蚀），且攻击检定 -1（深渊恐惧），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "abyss_gazer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_gazer_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_gazer_badge" }
]
```

---

## 套装十七：圣光使者（Lightbringer）饰品

> *"圣光使者不是在传播光明——他只是在黑暗中点燃了一根蜡烛，而蜡烛从不问黑暗有多深。"*

---

### 17.5 圣光使者光辉披风（Lightbringer Radiant Cloak）

```gdscript
item_id = "acc_lightbringer_cloak"
display_name = "圣光使者光辉披风"
description = "一件由圣光丝与光辉布编织而成的披风，披风表面不断有微型圣光在流动和闪耀——不是装饰，而是真正的神圣之力被封印在了丝线中。当穿戴者行走时，披风会自动释放出柔和的光芒，照亮周围的一切。在邪恶面前，披风会释放出耀眼的光芒，驱散一切黑暗。\n\n这件披风是圣光使者在「圣光神殿」中，从自己的圣光之力中取下的碎片编织的。他说："这件披风承载了我的光明之力。它不是武器——它是希望，是指引，是永不熄灭的火焰。"\n\n披风的效果是：radiant 抗性 +15（光辉之躯），且在黑暗中 30 尺半径内提供 bright light（圣光照明）。10 尺内 undead 和 demon 攻击检定 -2（圣光压制）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "lightbringer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_lightbringer_cloak" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_lightbringer_cloak" }
]
```

---

### 17.6 圣光使者圣徽项链（Lightbringer Holy Symbol Necklace）

```gdscript
item_id = "acc_lightbringer_necklace"
display_name = "圣光使者圣徽项链"
description = "一条由圣金与光辉水晶编织而成的项链，吊坠是一个微型圣徽——这个圣徽不是普通的宗教符号，它是第一位圣光使者亲手制作的。圣徽内部有一团微弱的圣光在永恒地燃烧，即使在最黑暗的地方，这团光也不会熄灭。据说这个圣徽可以驱散任何邪恶，治愈任何伤痛。\n\n这条项链是圣光使者在「圣光之源」中，从第一位圣光使者的遗物中取出的。他说："这个圣徽承载了我的信仰之力。它让我能够驱散黑暗，也让我能够照亮他人的道路。"\n\n项链的效果是：宗教检定 +3（圣光知识），且每日一次，可以释放「圣光治愈」——10 尺内所有友方恢复 3D8 HP（圣光治愈），且移除所有 non-legendary 恐惧和魅惑（圣光净化）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "lightbringer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_lightbringer_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_lightbringer_necklace" }
]
```

---

### 17.7 圣光使者净化之戒（Lightbringer Purification Ring）

```gdscript
item_id = "acc_lightbringer_ring_1"
display_name = "圣光使者净化之戒"
description = "一枚由净化石与圣金铸造的戒指，戒指表面刻有微型净化图案。当佩戴者集中精神时，戒指会释放出一股净化之力，驱散一切邪恶和污秽。这种净化不是温和的——它是剧烈的，会烧毁一切不纯净的东西。据说这枚戒指来自一位圣光大师，他用这枚戒指净化了一座被诅咒的城市。\n\n这枚戒指是圣光使者在「净化神殿」中，从圣光之神的祭坛上取出的。他说："这枚戒指让我能够借用净化的力量。净化不是破坏——它是恢复，是治愈，是重新开始。"\n\n戒指的效果是：每日一次，可以释放「净化之光」——15 尺锥形，所有 undead 和 demon 受到 3D10 radiant（DC16 体质豁免 half）且 blinded 1 回合（圣光致盲）。友方在锥形区域内恢复 2D8 HP（净化治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "lightbringer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_lightbringer_ring_1" }
]
```

---

### 17.8 圣光使者守护之戒（Lightbringer Guardian Ring）

```gdscript
item_id = "acc_lightbringer_ring_2"
display_name = "圣光使者守护之戒"
description = "一枚由守护石与圣银铸造的戒指，戒指表面刻有微型守护图案。当佩戴者集中精神时，戒指会释放出一股守护之力，在佩戴者周围形成一道圣光护盾。这道护盾可以阻挡物理攻击、魔法攻击、甚至是邪恶的诅咒。但护盾需要信仰——如果信仰动摇，护盾会破碎。\n\n这枚戒指是圣光使者在「守护神殿」中，从守护之神的祭坛上取出的。他说："这枚戒指让我能够借用守护的力量。守护不是被动——它是主动的选择，是愿意为他人承受伤害。"\n\n戒指的效果是：5 尺内友方 AC +1（守护光环）。每日一次，可以释放「圣光护盾」——指定一个 10 尺内的友方，其获得 15 点临时 HP（圣光护盾），且 immune to necrotic 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "lightbringer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_lightbringer_ring_2" }
]
```

---

### 17.9 圣光使者圣光之烛（Lightbringer Holy Candle）

```gdscript
item_id = "acc_lightbringer_trinket"
display_name = "圣光使者圣光之烛"
description = "一支由圣光凝结的蜡烛，烛火永恒地燃烧，即使在水中、在风中、在真空中也不会熄灭。这支蜡烛不是普通的蜡烛——它是活的。当佩戴者需要时，可以点燃蜡烛，释放出强大的圣光。这种圣光可以驱散任何黑暗，治愈任何伤痛，甚至可以让死者复活。但蜡烛的燃烧需要代价——每次使用，蜡烛会缩短一点。\n\n这支蜡烛是圣光使者在「圣光之源」中，从第一位圣光使者的遗物中取出的。他说："这支蜡烛承载了我的光明之力。它不是无限的——每一次燃烧，都是一次牺牲。"\n\n蜡烛的效果是：每日一次，可以点燃「圣光之烛」——选择一个效果：\n- 驱散：20 尺半径内所有 magical darkness 被驱散，undead 和 demon 受到 3D10 radiant（DC16 体质豁免 half）\n- 治愈：10 尺内一个友方恢复 4D8 HP，且移除所有 curse 和 disease\n- 复活：触摸一个死亡不超过 1 分钟的生物，其恢复至 1 HP（如同 spare the dying 的强化版）\n- 祝福：10 尺内所有友方攻击检定 +2 2 回合（圣光祝福）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "lightbringer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_lightbringer_trinket" }
]
```

---

### 17.10 圣光使者徽章（Lightbringer Badge）

```gdscript
item_id = "acc_lightbringer_badge"
display_name = "圣光使者徽章"
description = "一枚由圣金与光辉石铸造的徽章，徽章上刻着一支蜡烛和一道光芒——圣光使者的标志。徽章背面刻着一行小字：「黑暗越深，光明越亮。」这枚徽章是圣光公会的信物，拥有它意味着你已被圣光认可。\n\n徽章的效果是：10 尺内所有友方 radiant 抗性 +5（圣光庇护）。且每日一次，可以释放「光辉领域」——10 尺半径内 undead 和 demon 每回合开始时受到 1D6 radiant（光辉侵蚀），且友方在领域内攻击检定 +1（光辉之力），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "lightbringer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_lightbringer_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_lightbringer_badge" }
]
```

---

## 套装十八：永恒学徒（Eternal Apprentice）饰品

> *"永恒学徒不是在失败——他只是在学习，而学习永无止境。"*

---

### 18.5 永恒学徒求知披风（Eternal Apprentice Knowledge Cloak）

```gdscript
item_id = "acc_eternal_apprentice_cloak"
display_name = "永恒学徒求知披风"
description = "一件由求知丝与智慧布编织而成的披风，披风表面不断有微型文字在流动和变化——不是装饰，而是真正的知识被封印在了丝线中。这些文字来自不同的书籍：有的来自古老的魔法书，有的来自失落的历史卷轴，有的甚至来自未来的预言。当穿戴者凝视披风时，会不自觉地学习到新的知识。\n\n这件披风是永恒学徒在「无尽图书馆」中，从自己的读书笔记上取下的碎片编织的。他说："这件披风承载了我的求知之路。每一页都是一个发现，每一个字都是一次领悟。"\n\n披风的效果是：所有知识检定 +2（历史、奥秘、宗教、自然、调查，求知之路），且每日一次，可以「快速学习」——在 1 分钟内掌握一项技能的基础（获得一次性的 +3 该技能检定，持续 1 小时）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "eternal_apprentice_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "history_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_cloak" }
]
```

---

### 18.6 永恒学徒智慧之核项链（Eternal Apprentice Wisdom Core Necklace）

```gdscript
item_id = "acc_eternal_apprentice_necklace"
display_name = "永恒学徒智慧之核项链"
description = "一条由智慧金与知识水晶编织而成的项链，吊坠是一颗智慧之核——一颗由纯粹知识凝结的核心。核心内部不断有微型书籍在翻开和闭合，如同一个永恒的学习循环。据说这颗核心来自智慧之神的大脑，他在创造第一个智慧时，将自己的一部分力量封印在了其中。\n\n这条项链是永恒学徒在「智慧神殿」中，从智慧之神的祭坛上取出的。他说："这颗核心承载了我的智慧之力。它让我能够理解任何知识，也让我能够创造新的知识。"\n\n项链的效果是：所有智力相关检定 +2（智慧之核），且每日一次，可以释放「智慧闪光」——指定一个 20 尺内的敌人，其陷入困惑 1 回合（被知识压倒，DC16 智力豁免抵抗），或指定一个友方，其下一回合所有检定 +3（智慧启迪）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "eternal_apprentice_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_necklace" },
    { attribute_id = "history_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_necklace" }
]
```

---

### 18.7 永恒学徒模仿之戒（Eternal Apprentice Mimic Ring）

```gdscript
item_id = "acc_eternal_apprentice_ring_1"
display_name = "永恒学徒模仿之戒"
description = "一枚由模仿石与智慧金铸造的戒指，戒指表面刻有微型模仿图案。当佩戴者集中精神时，戒指会释放出一股模仿之力，使佩戴者可以模仿目标的某个能力。这种模仿不是复制——它是学习，是理解，是掌握。据说这枚戒指来自一位永恒学徒大师，他用这枚戒指学会了所有已知的魔法。\n\n这枚戒指是永恒学徒在「模仿深渊」中，从自己的学习成果中取下的碎片铸造的。他说："这枚戒指让我能够借用模仿的力量。模仿不是抄袭——它是学习的第一步。"\n\n戒指的效果是：每日一次，可以「模仿」一个可见敌人的一项能力（攻击方式、法术、或特殊能力），在下一回合内使用一次（模仿攻击检定/DC 使用你的数值，伤害/效果为原效果的 75%）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "eternal_apprentice_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_ring_1" }
]
```

---

### 18.8 永恒学徒实验之戒（Eternal Apprentice Experiment Ring）

```gdscript
item_id = "acc_eternal_apprentice_ring_2"
display_name = "永恒学徒实验之戒"
description = "一枚由实验石与智慧银铸造的戒指，戒指表面刻有微型实验图案。当佩戴者集中精神时，戒指会释放出一股实验之力，使佩戴者可以「实验」不同的魔法效果。这种实验是有风险的——有时会成功，有时会失败，有时会产生意想不到的结果。据说这枚戒指来自一位炼金术大师，他用这枚戒指创造了无数的发明。\n\n这枚戒指是永恒学徒在「实验室」中，从自己的实验成果中取下的碎片铸造的。他说："这枚戒指让我能够借用实验的力量。实验是知识的源泉——每一次失败，都是一次学习。"\n\n戒指的效果是：每日一次，可以释放「魔法实验」——随机获得一个魔法效果（1D6）：\n- 1：自身恢复 3D8 HP（治疗药水效果）\n- 2：指定一个敌人，受到 3D10 force（爆炸药水）\n- 3：自身 AC +3 2 回合（硬化药水）\n- 4：指定一个敌人，blinded 1 回合（闪光药水）\n- 5：自身攻击检定 +3 2 回合（力量药水）\n- 6：10 尺内所有友方恢复 2D8 HP（群体治疗药水）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "eternal_apprentice_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_eternal_apprentice_ring_2" }
]
```

---

### 18.9 永恒学徒无限之书（Eternal Apprentice Infinite Book）

```gdscript
item_id = "acc_eternal_apprentice_trinket"
display_name = "永恒学徒无限之书"
description = "一本由知识之力凝结的微型书籍，书页不断有微型文字在流动和变化。这本书不是普通的书籍——它是活的。当佩戴者翻开它时，会找到任何想要的知识：魔法、历史、科学、艺术。但知识是有代价的——每次阅读，佩戴者会失去一部分记忆。据说这本书来自「无尽图书馆」，永恒学徒在图书馆的最深处找到了它。\n\n这本书是永恒学徒在「无尽图书馆」中，从图书馆的核心取出的。他说："这本书承载了我的求知之力。它让我能够获得任何知识，也让我理解了知识的代价。"\n\n书的效果是：每日一次，可以翻开无限之书——选择一个效果：\n- 学习：获得一个技能 proficiency 1 小时\n- 记忆：恢复一个已使用的法术位（任何等级）\n- 预言：预见一个敌人的弱点——下一回合对其攻击检定有优势且伤害 +2D6\n- 治愈：恢复 3D8 HP（知识治愈）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "eternal_apprentice_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_eternal_apprentice_trinket" }
]
```

---

### 18.10 永恒学徒徽章（Eternal Apprentice Badge）

```gdscript
item_id = "acc_eternal_apprentice_badge"
display_name = "永恒学徒徽章"
description = "一枚由智慧金与知识石铸造的徽章，徽章上刻着一本书和一个问号——永恒学徒的标志。徽章背面刻着一行小字：「知之为知之，不知为不知。」这枚徽章是求知公会的信物，拥有它意味着你已被知识认可。\n\n徽章的效果是：10 尺内所有友方智力检定 +1（智慧庇护）。且每日一次，可以释放「求知领域」——10 尺半径内友方所有知识检定 +2（求知光环），且可以 bonus action 进行一次知识检定来识别敌人的弱点（获得对该敌人的攻击检定 +1），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "eternal_apprentice_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_eternal_apprentice_badge" },
    { attribute_id = "history_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_eternal_apprentice_badge" }
]
```

---

## 套装十九：血月猎人（Blood Moon Hunter）饰品

> *"血月猎人不是在狩猎——他只是在等待月亮变红，然后完成早已注定的杀戮。"*

---

### 19.5 血月猎人血月披风（Blood Moon Hunter Blood Moon Cloak）

```gdscript
item_id = "acc_blood_moon_hunter_cloak"
display_name = "血月猎人血月披风"
description = "一件由狼毛与血月丝编织而成的披风，披风表面不断有微型血月在升起和落下——不是装饰，而是真正的血月之力被封印在了丝线中。当血月升起时，披风会自动膨胀，释放出狼性的力量。据说这件披风是血月猎人在「血月之夜」中，从第一只狼人身上取下的毛发编织的。\n\n这件披风是血月猎人在「狼人之森」中，从自己的猎物上取下的碎片编织的。他说："这件披风让我成为了狼的一部分。当血月升起时，我会感受到狼的力量在我体内燃烧。"\n\n披风的效果是：在夜晚攻击检定 +2（月夜之力），且当血月升起时（由 DM 决定或每日一次模拟），获得「狼化」2 回合：力量 +2，敏捷 +2，攻击检定 +2，徒手攻击变为 1D8 slashing（狼爪）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "blood_moon_hunter_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_blood_moon_hunter_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_blood_moon_hunter_cloak" }
]
```

---

### 19.6 血月猎人狼牙项链（Blood Moon Hunter Fang Necklace）

```gdscript
item_id = "acc_blood_moon_hunter_necklace"
display_name = "血月猎人狼牙项链"
description = "一条由狼骨与血月金编织而成的项链，吊坠是一颗狼牙——这颗狼牙来自一只活了五百年的狼王。狼牙内部有微型月光在流动，发出微弱的红光。据说这颗狼牙含有狼王的全部力量，可以召唤狼群，也可以让佩戴者变成狼。\n\n这条项链是血月猎人在「狼王墓」中，从狼王的遗骸上取下的。他说："这颗狼牙承载了我的狩猎之力。它让我能够召唤狼群，也让我能够理解狼的语言。"\n\n项链的效果是：自然检定 +3（野兽知识），且每日一次，可以释放「狼牙召唤」——召唤 1D4+1 只魔法狼（HP 15，AC 13，攻击 +4，1D6+2 slashing，移动力 40 尺），持续 1 分钟。可以用 bonus action 指挥狼群。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "blood_moon_hunter_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_blood_moon_hunter_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_blood_moon_hunter_necklace" }
]
```

---

### 19.7 血月猎人猎杀之戒（Blood Moon Hunter Hunt Ring）

```gdscript
item_id = "acc_blood_moon_hunter_ring_1"
display_name = "血月猎人猎杀之戒"
description = "一枚由猎杀石与血月金铸造的戒指，戒指表面刻有微型猎杀图案。当佩戴者集中精神时，戒指会释放出一股猎杀之力，使佩戴者可以追踪任何猎物。这种追踪不是普通的——它可以穿透伪装，可以追踪气味，甚至可以追踪灵魂。据说这枚戒指来自一位传奇猎人，他用这枚戒指追踪到了一位隐形的神明。\n\n这枚戒指是血月猎人在「猎人神殿」中，从传奇猎人的遗骸上取下的。他说："这枚戒指让我能够借用猎杀的力量。猎杀不是杀戮——它是尊重，是理解，是完成自然的循环。"\n\n戒指的效果是：追踪检定 +3（猎杀追踪），且可以感知 30 尺内所有生物的「狩猎标记」（HP 最低的敌人自动被标记）。对被标记敌人的攻击检定 +2（猎杀专注）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "blood_moon_hunter_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_moon_hunter_ring_1" }
]
```

---

### 19.8 血月猎人血月之戒（Blood Moon Hunter Blood Moon Ring）

```gdscript
item_id = "acc_blood_moon_hunter_ring_2"
display_name = "血月猎人血月之戒"
description = "一枚由血月石与血月银铸造的戒指，戒指表面刻有微型血月图案。当佩戴者集中精神时，戒指会释放出一股血月之力，使佩戴者的攻击附带血月的效果。这种效果不是普通的——它可以让目标流血不止，可以召唤血月的诅咒，甚至可以让目标变成狼人。据说这枚戒指来自血月本身，血月猎人在血月之夜，从月光中凝结出了这块石头。\n\n这枚戒指是血月猎人在「血月之夜」中，从月光中取出的。他说："这枚戒指让我能够借用血月的力量。血月不是诅咒——它是力量，是自由，是野性的呼唤。"\n\n戒指的效果是：所有攻击附加 1D4 necrotic（血月之噬），且每日一次，可以释放「血月诅咒」——指定一个 10 尺内的敌人，其受到 3D10 necrotic（DC16 体质豁免 half），且每回合开始时受到 1D6 bleed（血月流血，持续 3 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "blood_moon_hunter_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_moon_hunter_ring_2" }
]
```

---

### 19.9 血月猎人猎人之笛（Blood Moon Hunter Hunter Flute）

```gdscript
item_id = "acc_blood_moon_hunter_trinket"
display_name = "血月猎人猎人之笛"
description = "一支由狼骨与血月木制成的微型笛子，笛子表面刻有月亮和狼的图案。这支笛子不是普通的乐器——它是猎人的语言。当佩戴者吹奏它时，可以召唤野兽，命令它们攻击敌人或追踪猎物。据说这支笛子来自「狩猎之神」，他在创造第一个猎人时，赠予了这支笛子。\n\n这支笛子是血月猎人在「狩猎之神殿」中，从神的祭坛上取出的。他说："这支笛子让我能够与野兽对话。野兽不是猎物——它们是伙伴，是老师，是自然的化身。"\n\n笛子的效果是：每日一次，可以吹奏猎人之笛——选择一个效果：\n- 召唤：召唤 1D4 只野兽（狼、熊、或鹰，HP 20，AC 14，攻击 +5），持续 1 分钟\n- 追踪：指定一个敌人，其在 1 分钟内无法隐形或隐藏（猎人之眼）\n- 恐吓：10 尺内所有敌人须通过 DC16 智慧豁免，失败则 frightened 1 回合（狼嚎）\n- 治愈：10 尺内所有友方恢复 2D8 HP（自然治愈）"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "blood_moon_hunter_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_blood_moon_hunter_trinket" }
]
```

---

### 19.10 血月猎人徽章（Blood Moon Hunter Badge）

```gdscript
item_id = "acc_blood_moon_hunter_badge"
display_name = "血月猎人徽章"
description = "一枚由血月金与狼骨石铸造的徽章，徽章上刻着一个月亮和一把弓——血月猎人的标志。徽章背面刻着一行小字：「狩猎即生存，生存即尊重。」这枚徽章是猎人公会的信物，拥有它意味着你已被狩猎之神认可。\n\n徽章的效果是：10 尺内所有友方在夜晚攻击检定 +1（月夜之力）。且每日一次，可以释放「狩猎领域」——10 尺半径内友方攻击被标记的敌人时检定 +1（猎杀专注），且击杀敌人时恢复 1D6 HP（猎杀恢复），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "blood_moon_hunter_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_moon_hunter_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_blood_moon_hunter_badge" }
]
```

---

## 套装二十：锈蚀齿轮（Rusted Gear）饰品

> *"锈蚀齿轮不是旧了——它是经历了时间的考验，而时间是最好的锻造师。"*

---

### 20.5 锈蚀齿轮锈蚀披风（Rusted Gear Rust Cloak）

```gdscript
item_id = "acc_rusted_gear_cloak"
display_name = "锈蚀齿轮锈蚀披风"
description = "一件由锈蚀铁片与齿轮丝编织而成的披风，披风表面覆盖着红褐色的锈迹，每一道锈迹都代表着一次战斗、一次胜利、一次幸存。当穿戴者行走时，锈迹会脱落，留下一条铁锈的痕迹。在潮湿环境中，披风会自动生锈，变得更加沉重，但也更加坚固。\n\n这件披风是锈蚀齿轮在「锈蚀工坊」中，从自己的机械装置上取下的碎片编织的。他说："这件披风承载了我的所有战斗。每一道锈迹都是一个故事，每一个故事都是一次生死。"\n\n披风的效果是：acid 抗性 +15（锈蚀免疫），且被近战攻击时，攻击者的武器有 25% 概率生锈（-1 攻击检定，可叠加至 -3，持续至修复）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "rusted_gear_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_acid", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_rusted_gear_cloak" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rusted_gear_cloak" }
]
```

---

### 20.6 锈蚀齿轮机械心脏项链（Rusted Gear Mechanical Heart Necklace）

```gdscript
item_id = "acc_rusted_gear_necklace"
display_name = "锈蚀齿轮机械心脏项链"
description = "一条由齿轮金与机械丝编织而成的项链，吊坠是一颗机械心脏——一颗由纯粹机械凝结的心脏。心脏在吊坠中永恒地跳动，发出咔哒咔哒的声音。据说这颗心脏来自一位机械师，他在将自己的心脏替换为机械心脏后，将原来的心脏制成了这件饰品。\n\n这条项链是锈蚀齿轮在「机械工坊」中，从自己的身体里取出的。他说："这颗心脏承载了我的机械之魂。它让我能够理解机械的语言，也让我能够创造生命。"\n\n项链的效果是：奥秘检定 +3（机械知识），且免疫 poison 和 disease（机械之躯）。每日一次，可以释放「机械修复」——恢复 2D10 HP（机械修复），且移除一个非 legendary 诅咒或疾病（机械净化）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "rusted_gear_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_rusted_gear_necklace" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rusted_gear_necklace" }
]
```

---

### 20.7 锈蚀齿轮齿轮之戒（Rusted Gear Gear Ring）

```gdscript
item_id = "acc_rusted_gear_ring_1"
display_name = "锈蚀齿轮齿轮之戒"
description = "一枚由齿轮与机械金铸造的戒指，戒指表面不断有微型齿轮在转动。当佩戴者集中精神时，齿轮会加速转动，为佩戴者提供额外的力量。这种力量不是魔法的——它是机械的，是物理的。据说这枚戒指来自一位机械大师，他用这枚戒指驱动了一台巨大的战争机器。\n\n这枚戒指是锈蚀齿轮在「齿轮工坊」中，从自己的机械装置上取下的齿轮铸造的。他说："这枚戒指让我能够借用齿轮的力量。齿轮是机械的灵魂——它让我能够精确地控制一切。"\n\n戒指的效果是：力量检定 +2（齿轮之力），且每日一次，可以释放「齿轮加速」——bonus action 获得一个额外动作（齿轮超速），但下一回合无法使用 bonus action（齿轮冷却）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "rusted_gear_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rusted_gear_ring_1" }
]
```

---

### 20.8 锈蚀齿轮锈蚀之戒（Rusted Gear Rust Ring）

```gdscript
item_id = "acc_rusted_gear_ring_2"
display_name = "锈蚀齿轮锈蚀之戒"
description = "一枚由锈蚀石与机械银铸造的戒指，戒指表面覆盖着厚厚的锈迹。当佩戴者集中精神时，戒指会释放出一股锈蚀之力，加速目标的 decay——金属会生锈，木头会腐烂，石头会风化。这种锈蚀是不可逆的，即使魔法也无法完全修复。据说这枚戒指来自锈蚀之神，他在创造锈蚀时，将自己的一部分力量封印在了其中。\n\n这枚戒指是锈蚀齿轮在「锈蚀深渊」中，从锈蚀之神那里得到的。锈蚀之神说："这枚戒指让你能够借用我的力量。但锈蚀是公平的——它会腐蚀一切，包括你自己。"\n\n戒指的效果是：所有攻击附加 1D4 acid（锈蚀），且每日一次，可以释放「锈蚀之触」——指定一个 5 尺内的敌人，其金属护甲 AC -2（锈蚀），且武器攻击检定 -2（锈蚀），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "rusted_gear_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rusted_gear_ring_2" }
]
```

---

### 20.9 锈蚀齿轮机械乌鸦（Rusted Gear Mechanical Raven）

```gdscript
item_id = "acc_rusted_gear_trinket"
display_name = "锈蚀齿轮机械乌鸦"
description = "一个由齿轮与魔法制成的微型乌鸦，乌鸦表面覆盖着精美的机械装置。这个乌鸦不是普通的玩具——它是活的。当佩戴者需要时，可以激活乌鸦，让它为自己侦察、攻击敌人或传递信息。乌鸦可以飞行，可以说话，甚至可以施法。但乌鸦需要维护——如果不定期上发条，它会停止运转。\n\n这个乌鸦是锈蚀齿轮在「机械工坊」中，从自己的第一个作品上取下的部分制成的。他说："这个乌鸦是我的第一个孩子。虽然它不完整，但它会永远陪伴我。"\n\n乌鸦的效果是：每日一次，可以激活「机械乌鸦」——乌鸦可以进行以下行动（选择一个，持续 2 回合）：\n- 侦察：乌鸦飞到 60 尺高空，揭示 30 尺半径内的所有隐藏敌人和陷阱\n- 攻击：乌鸦攻击一个敌人（+5，1D8+3 piercing），且可以 bonus action 再次攻击\n- 干扰：乌鸦干扰一个敌人的施法——该敌人法术 DC -2 且 concentration 检定有劣势\n- 传递：乌鸦可以传递一个信息或物品至 1 英里内的任何地点"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "rusted_gear_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_rusted_gear_trinket" }
]
```

---

### 20.10 锈蚀齿轮徽章（Rusted Gear Badge）

```gdscript
item_id = "acc_rusted_gear_badge"
display_name = "锈蚀齿轮徽章"
description = "一枚由机械金与齿轮石铸造的徽章，徽章上刻着一个齿轮和一个扳手——锈蚀齿轮的标志。徽章背面刻着一行小字：「锈蚀即历史，历史即力量。」这枚徽章是机械公会的信物，拥有它意味着你已被机械认可。\n\n徽章的效果是：10 尺内所有友方 acid 抗性 +5（锈蚀庇护）。且每日一次，可以释放「锈蚀领域」——10 尺半径内所有敌人的金属装备攻击检定 -1（锈蚀侵蚀），且 AC -1（锈蚀护甲），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "rusted_gear_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rusted_gear_badge" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rusted_gear_badge" }
]
```

---

*套装 11–20 饰品部分完结 · 共 60 件饰品装备*

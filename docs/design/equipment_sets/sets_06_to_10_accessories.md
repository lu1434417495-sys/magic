# 传奇装备套装饰品设计文档（套装 6–10）

> 5 套传奇套装的饰品部分，共 30 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装六：风暴行者（Storm Walker）饰品

---

### 6.5 风暴行者斗篷

```gdscript
item_id = "acc_storm_walker_cloak_6"
display_name = "风暴行者斗篷"
description = "一件由风暴鹰羽与导电丝线编织而成的深蓝色斗篷，表面不断有微弱的电弧在跳动——那不是装饰，而是真正的雷电被封印在了丝线中。斗篷非常轻盈——因为风暴鹰羽几乎没有重量，但也因为这轻盈，穿戴者在奔跑时会微微飘起。斗篷的边缘有特殊的"电弧流苏"——每一根流苏都会在地面上留下短暂的电火花。\n\n托尔温在创建雷霆之柱时，从一只被雷电击中的风暴鹰身上收集了所有羽毛。那只风暴鹰不是被雷电杀死的——它是"选择"被击中的，因为它想要"与天空融为一体"。托尔温说："这只鹰的勇气值得尊重，它的羽毛将成为我们与天空对话的媒介。"他用鹰羽与导电丝线编织了这件斗篷，成为了雷霆之柱大祭司的标志。\n\n斗篷会在雷暴天气中自动激活——电弧变得更加明亮，穿戴者的移动力 +10（顺应风的节奏）。且在雷暴中，斗篷可以「引雷」——每日一次，引导一道雷电攻击指定目标（30 尺射程 3D10 lightning，目标须通过 DC16 敏捷豁免，失败则 paralyzed 1 回合）。托尔温说："雷电不是天罚，它是天空的呼吸——学会与它对话，你就学会了天空的语言。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "storm_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_storm_walker_cloak_6" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_storm_walker_cloak_6" }
]
```

---

### 6.6 风暴行者项链

```gdscript
item_id = "acc_storm_walker_necklace_6"
display_name = "风暴行者项链"
description = "一条由雷击银链与一颗风暴核心串联而成的项链，风暴核心被封装在一枚蓝宝石中。宝石内部不断有微型雷电在闪烁——那不是普通的雷电，是托尔温在迎接第一千道闪电时，从闪电本身中捕捉到的"天空之泪"。项链佩戴时，宝石会随周围的电场变化亮度——电场越强，光芒越耀眼。\n\n这条项链是托尔温在成为大祭司后，用自己的一滴血与天空之泪一同制作的。他说："我的血液来自大地，天空之泪来自天空。当它们结合在一起时，我就成为了连接天地的桥梁。"他将项链戴在了脖子上，成为了雷霆之柱最神圣的圣物。\n\n项链的特殊效果是：在雷暴天气中，佩戴者的所有 lightning damage +1D6（天空的共鸣）。且可以通过项链「预警」——每日一次，预知一次闪电攻击的方向（获得 advantage 在该次攻击的豁免上）。托尔温说："天空不会无缘无故地发怒，每一次闪电都有它的原因——学会倾听，你就能预知它的到来。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "storm_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_storm_walker_necklace_6" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_storm_walker_necklace_6" }
]
```

---

### 6.7 风暴行者戒指·左

```gdscript
item_id = "acc_storm_walker_ring_1_6"
display_name = "风暴行者戒指·雷霆"
description = "一枚由雷击铜与风暴核心碎片锻造的粗糙戒指，戒指表面不断有微弱的电弧在跳动——在黑暗中会照亮周围数尺。戒指佩戴时，周围的空气会微微电离——头发会竖起，金属物品会微微震动。\n\n这枚戒指是「雷拳」瓦尔基在一次与雷兽的战斗中，从对方被击碎的心脏中取出的核心碎片锻造的。瓦尔基将碎片与自己的雷击铜护手一同熔铸，制作成了这枚戒指。他说："雷兽的心脏是最强大的电池——它储存了天空的力量。我要将这力量掌握在自己手中。"\n\n戒指会在佩戴者进行近战攻击时释放雷电——每日三次，近战攻击附加 1D8 lightning damage（雷霆之拳）。且在进行力量检定时，检定 +2（雷电的强化）。瓦尔基说："雷电不仅来自天空，它也来自拳头——当你用足够快的速度击打时，你也能创造雷电。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "storm_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_storm_walker_ring_1_6" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_storm_walker_ring_1_6" }
]
```

---

### 6.8 风暴行者戒指·右

```gdscript
item_id = "acc_storm_walker_ring_2_6"
display_name = "风暴行者戒指·疾风"
description = "一枚由风暴鹰爪骨与风之水晶锻造的轻盈戒指，戒指表面有类似羽毛的纹理——在光线下会折射出彩虹般的光芒。戒指佩戴时，会根据风速变化温度——风越大，戒指越温暖（仿佛在"拥抱"风）。\n\n这枚戒指是「闪电步」莱拉在一次暴风雨中，从一只被雷电击中的风暴鹰爪骨中提取的物质锻造的。莱拉说："这只鹰选择被雷电击中，因为它想要飞翔得更高。我将它的一部分带走，让它继续陪伴我飞翔。"她将鹰爪骨与风之水晶结合，制作成了这枚戒指。\n\n戒指的特殊效果是：佩戴者的移动力 +5（风的祝福）。且可以通过戒指「顺风」——每日一次，在移动时召唤一阵强风（移动力翻倍，持续 1 回合）。莱拉说："不要逆风跑，要侧身跑——让风推着你走。当你学会顺应风的节奏时，风就会成为你的朋友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "storm_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "dexterity_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_storm_walker_ring_2_6" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_storm_walker_ring_2_6" }
]
```

---

### 6.9 风暴行者特殊饰品

```gdscript
item_id = "acc_storm_walker_trinket_6"
display_name = "风暴行者风暴瓶"
description = "一个由雷击玻璃与风暴核心制成的小型瓶子，瓶内封存着一团微型风暴——你可以看到旋涡状的云层在瓶中旋转，偶尔有微型闪电划过。瓶子佩戴时，会随周围的天气状况变化——晴朗时平静，阴天时旋转加速，雷暴时剧烈震动。\n\n这个瓶子是托尔温在迎接第一千道闪电前，将自己的「天空之语」封入的容器。他说："这团风暴不是被封印的，是'被邀请的'——它选择留在这个瓶子中，成为我与天空对话的媒介。"他将瓶子放在了神殿最高的祭坛上，让它成为雷霆之柱最神圣的圣物。\n\n风暴瓶的特殊效果是：每日一次，可以释放瓶中的风暴（15 尺半径，所有生物须通过 DC16 力量豁免，失败则 knocked prone 并受到 2D10 lightning damage）。且可以通过瓶子「预测天气」——自动预知接下来 24 小时的天气变化（天空在向你透露它的计划）。一位后来的祭司说："当我凝视瓶中的风暴时，我仿佛听到了托尔温的声音——'天空在说话，你听懂了吗？'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "storm_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_storm_walker_trinket_6" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_storm_walker_trinket_6" }
]
```

---

### 6.10 风暴行者徽章

```gdscript
item_id = "acc_storm_walker_badge_6"
display_name = "风暴行者徽章"
description = "一枚由雷击铜与风暴核心碎片锻造的奇异徽章，徽章上刻有雷霆之柱的标志——一根被闪电环绕的石柱，柱顶有一只展翅的风暴鹰。徽章没有固定的形状——它的边缘不断变化，如同真实的闪电在跳动。\n\n这枚徽章是托尔温在创建雷霆之柱时，用被雷电熔化的铜矿与风暴鹰的羽毛一同铸造的。他说："这枚徽章不是荣誉，它是'天空之约'——佩戴它的人，必须发誓永远倾听天空的声音，永远尊重雷电的力量。"第一批祭司在加入时，都将自己的一小缕头发投入了徽章的熔炉——那是他们与天空的契约。\n\n徽章会在佩戴者无视天空的警告时剧烈震动——那不是魔法，而是契约的惩罚。托尔温说："天空是最诚实的朋友——它会在危险来临前发出警告。无视警告的人，不配称为风暴行者。"一位后来的祭司在一次暴风雨中无视徽章的震动强行出海，结果船只被闪电击中——他说："那不是惩罚，那是天空的教诲。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "storm_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_storm_walker_badge_6" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_storm_walker_badge_6" }
]
```

---

## 套装七：亡灵收割者（Death Reaper）饰品

---

### 7.5 亡灵收割者斗篷

```gdscript
item_id = "acc_death_reaper_cloak_7"
display_name = "亡灵收割者斗篷"
description = "一件由亡者之发与阴影布料编织而成的黑色斗篷，表面不断有微弱的幽绿色光芒在流动——那不是装饰，而是被封印在丝线中的灵魂碎片在"活动"。斗篷没有固定的形状——它会随周围的死亡气息而变化，死亡越多，斗篷越厚重。斗篷的边缘有特殊的"灵魂流苏"——每一根流苏都连接着一个被收割的灵魂。\n\n莫德雷德在创建终末之镰时，从一百个亡者身上取下了头发，用它们编织了这件斗篷。他说："头发是灵魂与肉体最后的连接，用头发编织的衣服，就是灵魂的囚笼。"他在编织过程中，将每个亡者的最后一丝意识也编入了斗篷——那些流动的幽绿色光芒，就是那些意识的"残余"。\n\n斗篷会在附近有生物死亡时变得更加厚重——那不是物理变化，而是死亡气息的"附着"。一位后来的亡灵法师在描述这件斗篷时说："当我穿上它走过战场时，斗篷变得越来越重，越来越长——那不是布料在增长，是死亡在依附。当我脱下它时，发现它比早上重了三倍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "death_reaper_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_reaper_cloak_7" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_death_reaper_cloak_7" }
]
```

---

### 7.6 亡灵收割者项链

```gdscript
item_id = "acc_death_reaper_necklace_7"
display_name = "亡灵收割者项链"
description = "一条由人骨碎片与阴影丝串联而成的项链，项链中央镶嵌着一颗「死亡之眼」——一颗能够看到灵魂并沟通亡者的魔法宝石。项链佩戴时，宝石会随周围的死亡气息变化颜色——死亡越多，宝石越绿，光芒越亮。\n\n这条项链是莫德雷德在研究灵魂转生时，从一位垂死的圣骑士眼中取出的晶状体制作的。那位圣骑士名叫加拉哈德，是前来剿灭终末之镰的骑士团先锋。莫德雷德在与加拉哈德的决斗中，没有杀死他，而是"请求"他——"让我看看你的眼睛，我想知道光明如何看待死亡。"加拉哈德在临终前同意了他的请求。\n\n莫德雷德将加拉哈德的眼睛晶状体封入了宝石，说："这双眼睛曾经只看到光明，现在它将看到死亡——以及死亡之后的真相。"项链会让佩戴者看到所有 undead 和幽灵（如同 see invisibility，但对 undead 有效，范围 60 尺）。一位后来的亡灵法师说："当我戴上这条项链时，我看到了一个我从未见过的世界——到处都是行走的灵魂，有些在等待，有些在迷失，有些在愤怒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "death_reaper_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_reaper_necklace_7" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_death_reaper_necklace_7" }
]
```

---

### 7.7 亡灵收割者戒指·左

```gdscript
item_id = "acc_death_reaper_ring_1_7"
display_name = "亡灵收割者戒指·收割"
description = "一枚由人骨与死亡符文锻造的苍白戒指，戒指表面刻有终末之镰的收割符文——一个不断旋转的微型镰刀图案。戒指佩戴时，会根据周围的死亡气息变化温度——死亡越多，戒指越冷，冷得几乎会冻伤手指。\n\n这枚戒指是莫德雷德在完成他最后的"收割"——一座村庄的全部人口后，用村长的心脏碎片制作的。他说："村长是最后一个死去的，他见证了所有人的死亡。他的心脏中蕴含着所有灵魂的恐惧和悲伤——那是最好的'收割材料'。"他将心脏碎片与自己的死亡符文结合，制作成了这枚戒指。\n\n戒指的特殊效果是：每日三次，可以通过触碰一个生物「收割」其生命力（2D6 necrotic damage，恢复自身等量 HP）。且在击杀一个生物后，戒指会让佩戴者恢复 1D8 HP（收割者之触）。莫德雷德说："收割不是杀戮，它是'帮助'——帮助灵魂提前完成它们的旅程。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "death_reaper_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_death_reaper_ring_1_7" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_death_reaper_ring_1_7" }
]
```

---

### 7.8 亡灵收割者戒指·右

```gdscript
item_id = "acc_death_reaper_ring_2_7"
display_name = "亡灵收割者戒指·亡语"
description = "一枚由亡者喉骨与阴影水晶锻造的奇异戒指，戒指表面没有任何装饰——只有一圈细密的纹路，那是亡者声带留下的痕迹。戒指佩戴时，会根据周围的亡灵数量变化温度——亡灵越多，戒指越温暖（仿佛在"欢迎"它们）。\n\n这枚戒指是「无声收割者」奈克斯在临终前，请求莫德雷德用自己的喉骨制作的。奈克斯说："我一生都在追求安静，但死后我想说话——我想告诉后来的收割者，安静不是目的，理解才是。"莫德雷德将奈克斯的喉骨与阴影水晶结合，制作成了这枚戒指。\n\n戒指的特殊效果是：佩戴者可以通过戒指与 undead 沟通——每日一次，与 30 尺内一个 undead 进行简短沟通（了解它的来历和目的）。且在进行亡灵法术时，法术 DC +1（亡语的加持）。奈克斯说："亡灵不是敌人，它们是迷路的人——学会与它们沟通，你就能帮助它们找到回家的路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "death_reaper_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_reaper_ring_2_7" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_death_reaper_ring_2_7" }
]
```

---

### 7.9 亡灵收割者特殊饰品

```gdscript
item_id = "acc_death_reaper_trinket_7"
display_name = "亡灵收割者灵魂沙漏"
description = "一个由人骨与阴影玻璃制成的小型沙漏，沙漏中的沙子不是普通的沙子——是「灵魂沙」，每一粒都代表一个被收割的灵魂。沙漏佩戴时，沙子会不断从上部流向下部，但永远不会流完——因为每当最后一粒沙子落下时，新的沙子会自动出现。\n\n这个沙漏是莫德雷德在他最后的"收割"后，用所有被收割灵魂的"残余"制作的。他说："灵魂不会真正消失，它们只是转化了——从肉体转化为记忆，从记忆转化为沙子，从沙子转化为永恒。"他将沙漏放在了终末之镰的祭坛上，作为所有灵魂的"归宿"。\n\n灵魂沙漏的特殊效果是：每日一次，可以翻转沙漏，释放其中一个灵魂（对一个目标造成 3D10 necrotic damage，或恢复一个盟友 3D10 HP——灵魂的转化可以是破坏，也可以是治愈）。且可以通过沙漏「感知」——30 尺内任何 undead 或 recently deceased 生物会被自动感知。一位后来的亡灵法师说："当我翻转沙漏时，我仿佛听到了三百六十五个声音同时说：'谢谢你还记得我们。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "death_reaper_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_reaper_trinket_7" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_reaper_trinket_7" }
]
```

---

### 7.10 亡灵收割者徽章

```gdscript
item_id = "acc_death_reaper_badge_7"
display_name = "亡灵收割者徽章"
description = "一枚由人骨与死亡符文锻造的苍白徽章，徽章上刻有终末之镰的标志——一把镰刀，刀刃由无数微型灵魂组成，刀柄是一根脊椎骨。徽章佩戴时，会随周围的死亡气息微微发光——死亡越多，光芒越绿，但也越冷。\n\n这枚徽章是莫德雷德在创建终末之镰时，用自己的第一根肋骨与死亡符文一同铸造的。他说："这枚徽章不是荣誉，它是'收割之约'——佩戴它的人，必须发誓尊重死亡，理解死亡，最终接受死亡。"第一批成员在加入时，都将自己的一根头发投入了徽章的熔炉——那是他们与死亡的契约。\n\n徽章会在佩戴者试图逃避死亡时剧烈刺痛——那不是惩罚，而是契约的提醒。莫德雷德说："死亡不是敌人，它是最终的真相。试图逃避死亡的人，只是在逃避真相。"一位后来的亡灵法师在面临致命疾病时，徽章突然变得滚烫——他说："那不是警告，那是莫德雷德在提醒我——'接受它，转化它，成为它。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "death_reaper_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_death_reaper_badge_7" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_death_reaper_badge_7" }
]
```

---

## 套装八：龙鳞铠甲（Dragon Scale）饰品

---

### 8.5 龙鳞铠甲斗篷

```gdscript
item_id = "acc_dragon_scale_cloak_8"
display_name = "龙鳞铠甲斗篷"
description = "一件由真龙翼膜与龙丝编织而成的厚重斗篷，表面覆盖着层层叠叠的微型龙鳞——每一片龙鳞都来自不同的龙种，因此呈现出五彩斑斓的颜色：红色代表火焰，蓝色代表闪电，绿色代表毒素，黑色代表酸液，白色代表冰霜。斗篷非常坚硬——龙鳞的硬度超过钢铁，但也因为这硬度，斗篷几乎没有柔韧性。\n\n西格德在创建龙血誓言时，从他猎获的第一条龙——一条青年红龙「焚烬者」卡扎克的翼膜上取下了碎片。他说："龙不屑于穿戴铠甲，但龙的翼膜是最完美的披风——它既坚固又轻盈，既耐火又美观。"他用卡扎克的翼膜与龙丝编织了这件斗篷，成为了龙血誓言创始人的标志。\n\n斗篷会在面对龙类敌人时自动激活——龙鳞会竖起，释放出龙威的残余气息（龙类敌人攻击检定 -1）。且斗篷可以「反射」——每日一次，完全反射一次龙息攻击（如同 mirror spell，但只对龙息有效）。西格德说："用龙的武器对抗龙，是最公平、最有效的方式。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dragon_scale_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_cloak_8" },
    { attribute_id = "resistance_fire", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dragon_scale_cloak_8" }
]
```

---

### 8.6 龙鳞铠甲项链

```gdscript
item_id = "acc_dragon_scale_necklace_8"
display_name = "龙鳞铠甲项链"
description = "一条由龙牙与龙筋串联而成的项链，项链中央镶嵌着一颗「龙血之心」——一颗由纯龙血凝固而成的宝石，内部不断有龙血的脉动在闪烁。项链佩戴时，宝石会随穿戴者的心跳微微脉动——脉动越有力，宝石越明亮。\n\n这条项链是「五龙」艾拉妮丝在完成五龙胸甲后，用五条老龙的最后一滴血液制作的。五条老龙在临终前都同意了自己的血液被使用——但它们提出了条件：这滴血液必须用来"保护"，而不是"杀戮"。艾拉妮丝将五滴血液混合，凝固成了这颗龙血之心。\n\n她说："这五滴血液代表着五种不同的元素力量，但它们有一个共同点——都渴望被用来守护。"项链会让佩戴者在面对龙类敌人时，所有攻击检定 +2（龙血的共鸣）。且可以通过项链「威慑」——每日一次，释放一阵龙威（15 尺半径，所有生物须通过 DC16 智慧豁免，失败则 frightened 1 回合）。艾拉妮丝说："龙威不是用来恐吓的，它是用来保护的——让敌人知道，你身上有龙的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dragon_scale_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "strength_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_necklace_8" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_dragon_scale_necklace_8" }
]
```

---

### 8.7 龙鳞铠甲戒指·左

```gdscript
item_id = "acc_dragon_scale_ring_1_8"
display_name = "龙鳞铠甲戒指·龙爪"
description = "一枚由龙爪骨与龙血锻造的锋利戒指，戒指表面有类似龙爪的纹理——尖锐、弯曲、充满力量。戒指佩戴时，周围的空气会微微震动——形成一层龙威护盾。\n\n这枚戒指是「龙爪」贝奥武夫在一次与上古黑龙的战斗中，从对方撕下的逆鳞碎片锻造的。贝奥武夫将逆鳞与自己的龙血混合，制作成了这枚戒指。他说："逆鳞是龙身上最敏感、最骄傲的部分——撕下逆鳞，就是击碎龙的骄傲。这枚戒指提醒我，永远不要骄傲。"\n\n戒指会在佩戴者进行近战攻击时释放龙威——每日三次，近战攻击附加 1D6 对应元素 damage（根据戒指上镶嵌的龙鳞颜色决定）。且在进行对抗龙类的攻击时，攻击检定 +2（龙爪的锋利）。贝奥武夫说："龙的爪子不是武器，它们是'语言'——龙用爪子来表达力量、愤怒和警告。学会理解这种语言，你就能与龙对话。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dragon_scale_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_ring_1_8" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dragon_scale_ring_1_8" }
]
```

---

### 8.8 龙鳞铠甲戒指·右

```gdscript
item_id = "acc_dragon_scale_ring_2_8"
display_name = "龙鳞铠甲戒指·龙息"
description = "一枚由龙喉骨与元素水晶锻造的奇异戒指，戒指内部封存着一团微型龙息——你可以选择激活哪种元素：火焰、闪电、毒素、酸液或冰霜。戒指佩戴时，会根据激活的元素变化颜色——火焰红、闪电蓝、毒素绿、酸液黑、冰霜白。\n\n这枚戒指是「龙步」赫拉克勒斯在杀死自己的"父亲"——一条贪婪的红龙后，用对方的喉骨制作的。赫拉克勒斯说："这条龙用它的吐息烧毁了我的家园，现在我要用它的吐息来保护别人的家园。"他将龙喉骨与元素水晶结合，制作成了这枚可以切换元素类型的戒指。\n\n戒指的特殊效果是：每日三次，可以释放一道微型龙息（15 尺锥形 2D8 对应元素 damage，目标须通过 DC14 敏捷豁免，失败则受到对应元素状态效果）。赫拉克勒斯说："龙息不是用来毁灭的，它是用来保护的——就像龙用吐息来保护它的巢穴一样。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dragon_scale_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_scale_ring_2_8" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dragon_scale_ring_2_8" }
]
```

---

### 8.9 龙鳞铠甲特殊饰品

```gdscript
item_id = "acc_dragon_scale_trinket_8"
display_name = "龙鳞铠甲龙蛋"
description = "一个由龙蛋壳与龙丝制成的小型龙蛋，龙蛋内不断有微弱的脉动——脉动与某条远方的龙的心跳同步。龙蛋佩戴时，会随龙族的接近微微发光——越近越亮，光芒的颜色代表接近的龙的元素类型。\n\n这个龙蛋是艾拉妮丝在请求五条老龙献出鳞甲时，从其中一条绿龙那里得到的"礼物"。那条绿龙说："我的鳞甲可以给你，但我的蛋不能——它代表着未来。不过，我可以给你一颗'已经死去'的蛋，它曾经是我的第一个孩子，但没有孵化。我希望它能帮助你保护其他的龙蛋。"\n\n龙蛋的特殊效果是：每日一次，可以通过龙蛋「感知」到 5 里内所有龙族的位置和数量（龙蛋雷达）。且可以通过龙蛋「沟通」——与 30 尺内一条龙进行简短的心灵感应沟通（了解它的情绪和意图）。艾拉妮丝说："龙蛋不是武器，它是'桥梁'——连接人类与龙族的桥梁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dragon_scale_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dragon_scale_trinket_8" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_trinket_8" }
]
```

---

### 8.10 龙鳞铠甲徽章

```gdscript
item_id = "acc_dragon_scale_badge_8"
display_name = "龙鳞铠甲徽章"
description = "一枚由龙鳞与龙血锻造的华丽徽章，徽章上刻有龙血誓言的标志——一把剑刺穿一片龙鳞，剑柄上缠绕着龙筋。徽章佩戴时，会根据附近龙族的数量变化光芒的强度——越多越明亮，但也越热（龙血的共鸣）。\n\n这枚徽章是西格德在创建龙血誓言时，用他猎获的第一条龙的鳞片与龙血一同铸造的。他说："这枚徽章不是荣誉，它是'责任'——佩戴它的人，必须发誓只猎杀恶龙，保护善龙，尊重所有龙族的生命。"第一批屠龙者在加入时，都将自己的一滴血与龙血混合，滴入了徽章的熔炉——那是他们与龙血的契约。\n\n徽章会在佩戴者试图猎杀非恶龙时剧烈灼烧——那不是魔法，而是契约的惩罚。西格德说："龙不是怪物，它们是生物——有善有恶，有老有少。猎杀无辜的龙，就等于猎杀无辜的人。"一位后来的屠龙者在试图偷猎一条正在孵蛋的母龙时，徽章突然变得像烙铁一样烫——他说："那不是惩罚，那是西格德在提醒我——'记住你的誓言。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dragon_scale_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_badge_8" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_scale_badge_8" }
]
```

---

## 套装九：铁壁要塞（Iron Bulwark）饰品

---

### 9.5 铁壁要塞斗篷

```gdscript
item_id = "acc_iron_bulwark_cloak_9"
display_name = "铁壁要塞斗篷"
description = "一件由三层精钢链甲片与强化布料交织而成的厚重斗篷，表面覆盖着无数微型凹痕——每一道凹痕都代表一次被挡下的攻击。斗篷非常沉重——比普通斗篷重十倍，因为它本质上是一件可穿戴的盾牌。斗篷的边缘有特殊的"盾片流苏"——每一根流苏都是一块微型塔盾，可以用来格挡来自侧面的攻击。\n\n马克西穆斯在创建铁壁军团时，命令铸造师用收集到的所有破损盾牌的碎片编织了这件斗篷。他说："这些盾牌曾经保护过无数士兵，它们的碎片不应该被丢弃。我要让它们继续守护——以另一种方式。"铸造师花了六个月时间，将三千块盾牌碎片缝入了斗篷。\n\n斗篷会在佩戴者面对 multiple enemies 时自动激活——盾片流苏会竖起，形成一圈额外的防护（AC +1，每有一个相邻敌人）。且斗篷可以「掩护」——每日一次，用斗篷掩护一个 5 尺内的盟友，让该盟友免疫一次攻击（如同 protection reaction）。马克西穆斯说："最好的防守不是保护自己，而是保护身边的人——因为一个人的盾墙不够坚固，但三千人的盾墙可以挡住一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "iron_bulwark_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_bulwark_cloak_9" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_iron_bulwark_cloak_9" }
]
```

---

### 9.6 铁壁要塞项链

```gdscript
item_id = "acc_iron_bulwark_necklace_9"
display_name = "铁壁要塞项链"
description = "一条由精钢链与一颗「坚韧之心」串联而成的项链，坚韧之心是一颗由铁壁军团所有阵亡士兵的盔甲碎片熔铸而成的宝石。宝石呈现出深沉的铁灰色，内部不断有微弱的金属光泽在流动——那是三千士兵的意志在共鸣。项链佩戴时，宝石会随危险接近而变得更加明亮——越危险越亮。\n\n这条项链是马克西穆斯在围城战的第四十天，用收集到的所有阵亡士兵的盔甲碎片制作的。他说："这些士兵已经倒下了，但他们的意志还在——在每一块碎片中，在每一道凹痕中。我要将他们的意志凝聚起来，让它们继续守护活着的人。"他将碎片熔铸成了这颗坚韧之心，挂在了自己的脖子上。\n\n项链的特殊效果是：当佩戴者 HP 降至 20% 以下时，坚韧之心会释放所有储存的意志——恢复 2D10 HP（不屈之魂，每日一次）。且在进行体质豁免时，检定 +2（三千士兵的共同意志）。马克西穆斯说："一个人的意志是有限的，但三千人的意志是无限的——当你佩戴这条项链时，你不是一个人在战斗。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "iron_bulwark_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_bulwark_necklace_9" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_iron_bulwark_necklace_9" }
]
```

---

### 9.7 铁壁要塞戒指·左

```gdscript
item_id = "acc_iron_bulwark_ring_1_9"
display_name = "铁壁要塞戒指·坚守"
description = "一枚由精钢与陨铁锻造的厚重戒指，戒指表面刻有铁壁军团的座右铭："不动如山，不退一步。"戒指内侧刻着马克西穆斯的名字——不是作为拥有者，而是作为誓言的见证人。\n\n这枚戒指是马克西穆斯在创建铁壁军团时，用自己的第一滴汗与精钢一同锻造的。他说："这枚戒指不是我的，它是所有铁壁军团士兵的共同誓言。任何佩戴它的人，都是铁壁军团的一员——无论生死。"他将戒指传给了军团的第一位百夫长，那位百夫长又将它传给了下一位——直到围城战，最后一位佩戴它的百夫长在倒下前，用最后的力气将戒指抛向了援军的方向。\n\n戒指会在佩戴者坚守阵地时变得更加坚固——站立不动时，AC +1（坚守之力）。且可以通过戒指「号召」——每日一次，号召 15 尺内所有盟友组成临时盾墙（所有盟友 AC +1，免疫被推动，持续 1 分钟）。马克西穆斯说："坚守不是懦弱，它是最勇敢的行为——因为坚守意味着你选择承受，而不是逃避。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "iron_bulwark_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_bulwark_ring_1_9" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_iron_bulwark_ring_1_9" }
]
```

---

### 9.8 铁壁要塞戒指·右

```gdscript
item_id = "acc_iron_bulwark_ring_2_9"
display_name = "铁壁要塞戒指·牺牲"
description = "一枚由精钢与阵亡士兵的血液结晶锻造的深沉戒指，戒指呈现出暗红色——不是装饰，而是真正的血液被融入了钢铁。戒指佩戴时，会根据周围盟友的数量变化温度——盟友越多，戒指越温暖（仿佛在"拥抱"他们）。\n\n这枚戒指是「盾墙之基」格拉古在第四十九天的最后攻击中，用自己的断骨与血液制作的。格拉古在冲车撞碎他的臂铠、撞碎他的双臂后，用牙齿咬断了自己的一根手指，将血液滴入了地面的钢铁碎片中。他说："我的身体已经不能战斗了，但我的血液还可以——它将成为后来的守护者的力量。"他的血液与钢铁融合，形成了这枚戒指。\n\n戒指的特殊效果是：每日一次，可以激活「牺牲」——选择一个 5 尺内的盟友，承受该盟友下一次受到的全部伤害（如同 warding bond 的单次版）。且在承受伤害后，攻击检定 +2（牺牲的力量，持续 1 回合）。格拉古说："牺牲不是失去，它是给予——当你为别人承受痛苦时，你获得了比痛苦更强大的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "iron_bulwark_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_bulwark_ring_2_9" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_iron_bulwark_ring_2_9" }
]
```

---

### 9.9 铁壁要塞特殊饰品

```gdscript
item_id = "acc_iron_bulwark_trinket_9"
display_name = "铁壁要塞军旗碎片"
description = "一块由精钢与陨铁制成的小型军旗碎片，碎片上刻有铁壁军团的旗帜图案——一面被鲜血浸透但仍屹立不倒的旗帜。碎片佩戴时，会随战斗的激烈程度微微发热——战斗越激烈，碎片越热，仿佛在"激励"穿戴者。\n\n这块碎片是「盾墙之牙」卡西乌斯在第四十九天的最后攻击中，用头盔上的尖刺固定住的那面军旗的一部分。援军到达时，发现卡西乌斯的尸体跪在军旗下，头盔上的尖刺深深插入了地面。他们花了三个小时才将他的头盔取下，然后将军旗取下，发现旗杆已经被折断——但旗帜仍然飘扬。\n\n他们将旗帜的碎片分给了所有幸存的士兵，作为铁壁军团永不倒下的象征。这块碎片的特殊效果是：每日一次，可以高举碎片「鼓舞」——15 尺内所有盟友获得 +2 攻击检定和 +2 豁免检定（铁壁军团的意志，持续 1 分钟）。且在佩戴者被击倒时，碎片会自动发热，给予佩戴者 advantage 在下一次站起的检定上（不屈之魂）。卡西乌斯说："旗帜不倒，军团不死——即使只剩一块碎片，铁壁军团的精神永远屹立。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "iron_bulwark_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_bulwark_trinket_9" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_bulwark_trinket_9" }
]
```

---

### 9.10 铁壁要塞徽章

```gdscript
item_id = "acc_iron_bulwark_badge_9"
display_name = "铁壁要塞徽章"
description = "一枚由精钢与陨铁锻造的庄严徽章，徽章上刻有铁壁军团的完整纹章——一面盾墙，盾墙上方有一只展翅的雄鹰，鹰爪中握着一根断裂的长矛。徽章背面刻着马克西穆斯的遗言："不动如山，不退一步。"\n\n这枚徽章是马克西穆斯在创建铁壁军团时，为每一位士兵亲手打造的。他说："这枚徽章不是荣誉的象征，它是'铁壁之约'——佩戴它的人，必须发誓永远站在最前排，永远保护身后的人。"三千士兵在出征前，都将这枚徽章别在了心口——那是他们与马克西穆斯的最后约定。\n\n徽章会在佩戴者试图逃离战场时剧烈变冷——那不是魔法，而是契约的惩罚。马克西穆斯说："铁壁军团的士兵不会逃跑，因为逃跑意味着抛弃身后的人。如果你佩戴这枚徽章，你就是铁壁的一部分——铁壁不会移动，不会倒塌，不会背叛。"一位后来的士兵在面临溃败时，徽章突然变得像玄冰一样冷——他说："那不是警告，那是马克西穆斯在提醒我——'站稳，援军就在路上。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "iron_bulwark_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_bulwark_badge_9" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_bulwark_badge_9" }
]
```

---

## 套装十：古代帝王（Ancient Emperor）饰品

---

### 10.5 古代帝王斗篷

```gdscript
item_id = "acc_ancient_emperor_cloak_10"
display_name = "古代帝王斗篷"
description = "一件由纯金丝线、秘银与古代丝绸交织而成的华丽斗篷，表面不断有微型帝国的版图在流动——你可以看到边境在扩张、城市在建立、军队在行军。斗篷非常沉重——不是因为材质，而是因为上面承载的千年历史之重。斗篷的边缘有特殊的" provinces 流苏"——每一根流苏都代表一个省份，流苏的颜色代表该省份的特产。\n\n凯撒在创建永恒王朝时，用征服的第一座城市的全部黄金打造了这件斗篷。他说："这件斗篷不是衣服，它是'帝国的地图'——当我穿上它时，我穿着整个帝国。"他在斗篷上绣了十二个省份的版图，每一个针脚都代表一条道路、一座城市、一支军队。\n\n末代皇帝奥古斯都在临终前，将这件斗篷披在了自己的铠甲上。他说："即使帝国要灭亡了，我也要穿着它死去——因为我是帝国的守护者，不是它的抛弃者。"斗篷会在穿戴者面对大量敌人时变得更加沉重——每有一个敌人，斗篷重一分（象征帝国的负担）。但同时，每有一个敌人，斗篷也会变得更加明亮——象征帝王不惧众敌。\n
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "ancient_emperor_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ancient_emperor_cloak_10" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ancient_emperor_cloak_10" }
]
```

---

### 10.6 古代帝王项链

```gdscript
item_id = "acc_ancient_emperor_necklace_10"
display_name = "古代帝王项链"
description = "一条由纯金链与一颗「帝王之心」串联而成的项链，帝王之心是一颗由永恒王朝所有皇帝的遗嘱碎片熔铸而成的宝石。宝石呈现出深邃的紫色——那是权力与智慧的象征，内部不断有微弱的金色光芒在流动——那是历代皇帝的意志在共鸣。项链佩戴时，宝石会随穿戴者的情绪变化颜色——愤怒时红色，平静时蓝色，决断时金色。\n\n这条项链是「建造者」哈德良在建造永恒王朝的首都时，用收集到的所有前任皇帝的遗嘱碎片制作的。他说："这些遗嘱不是死者的遗言，它们是活者的指南——每一位皇帝的智慧都应该被传承，而不是被埋葬。"他将碎片熔铸成了这颗帝王之心，挂在了自己的脖子上，然后传给了下一代皇帝。\n\n项链的特殊效果是：佩戴者在面对社交挑战时，可以获得历代皇帝的智慧——每日一次，在任何 charisma-based 检定上获得 advantage（帝王之声）。且在进行领导检定时，检定 +3（千年的统治经验）。哈德良说："皇帝不是最聪明的人，但他应该是最善于倾听的人——倾听前辈的智慧，倾听人民的声音，倾听历史的教训。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "ancient_emperor_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ancient_emperor_necklace_10" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_ancient_emperor_necklace_10" }
]
```

---

### 10.7 古代帝王戒指·左

```gdscript
item_id = "acc_ancient_emperor_ring_1_10"
display_name = "古代帝王戒指·征服"
description = "一枚由纯金与陨铁锻造的厚重戒指，戒指表面刻有永恒王朝的开国皇帝的肖像——「征服者」凯撒，他骑在战马上，手持长矛，目光远大。戒指内侧刻着凯撒的遗言："帝国不是土地，是人民。"\n\n这枚戒指是凯撒在征服最后一个省份时，用自己的战马的马蹄铁与黄金一同锻造的。他说："这枚戒指不是战利品，它是'责任的象征'——每一次征服都意味着更多的责任，更多的守护，更多的牺牲。"他将戒指戴在了左手上——"左手是握住缰绳的手，它控制着帝国的方向"。\n\n戒指的特殊效果是：每日一次，在面对一个可以被「说服」或「恐吓」的目标时，可以自动成功（帝王之命）。且在进行历史检定时，检定 +3（凯撒的记忆仍在指引）。凯撒说："征服不是目的，它是手段——目的是建立一个让人民安居乐业的帝国。如果征服不能带来和平，那它就是失败的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "ancient_emperor_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ancient_emperor_ring_1_10" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_ancient_emperor_ring_1_10" }
]
```

---

### 10.8 古代帝王戒指·右

```gdscript
item_id = "acc_ancient_emperor_ring_2_10"
display_name = "古代帝王戒指·立法"
description = "一枚由纯金与秘银锻造的精致戒指，戒指表面刻有永恒王朝的第二代皇帝「立法者」查士丁尼的肖像——他坐在王座上，手持法典，目光公正。戒指内侧刻着查士丁尼的遗言："法律不是枷锁，它是保护——保护弱者不受强者欺凌，保护诚实者不受欺诈者伤害。"\n\n这枚戒指是查士丁尼在完成永恒王朝的第一部法典时，用法典的封面金饰与自己的印章一同锻造的。他说："这枚戒指不是权力的象征，它是'正义的象征'——佩戴它的人，必须发誓永远公正，永远诚实，永远保护弱者。"他将戒指戴在了右手上——"右手是签署法令的手，它决定了帝国的命运"。\n\n戒指的特殊效果是：每日一次，在面对一个法律或道德困境时，可以获得查士丁尼的智慧——自动知道最公正、最合法的解决方案（立法者的智慧）。且在进行洞察检定时，检定 +3（看穿谎言和欺诈）。查士丁尼说："法律不是完美的，但它是我们能做到的最好的——只要还有人愿意遵守它，文明就不会灭亡。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "ancient_emperor_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ancient_emperor_ring_2_10" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_ancient_emperor_ring_2_10" }
]
```

---

### 10.9 古代帝王特殊饰品

```gdscript
item_id = "acc_ancient_emperor_trinket_10"
display_name = "古代帝王传国玉玺"
description = "一个由纯金与秘银制成的小型玉玺，玉玺底部刻有永恒王朝的国徽——一只衔着橄榄枝的雄鹰。玉玺顶部是一颗微型帝王之心，与项链上的宝石同源。玉玺佩戴时，会随穿戴者的决策变化温度——做出正确决策时温暖，做出错误决策时冰冷。\n\n这个玉玺是永恒王朝的开国皇帝凯撒在建立帝国时，用征服的第一座城市的全部玉石雕刻的。他说："这枚玉玺不是权力的象征，它是'信任的象征'——人民信任皇帝，皇帝用这枚玉玺来签署保护人民的法令。"他将玉玺放在了王座的旁边，成为了永恒王朝最神圣的圣物。\n\n玉玺的特殊效果是：每日一次，可以用玉玺「颁布」一道临时法令——选择一个 15 尺内的盟友，赋予其一项临时能力（例如：额外一次攻击、免疫一次伤害、或恢复 2D10 HP）。且在佩戴者面临重大决策时，玉玺会给予 guidance（advantage 在一次智慧检定上）。凯撒说："皇帝不是最聪明的人，但他必须是最负责任的人——因为每一个决策都会影响千万人的命运。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "ancient_emperor_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ancient_emperor_trinket_10" },
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ancient_emperor_trinket_10" }
]
```

---

### 10.10 古代帝王徽章

```gdscript
item_id = "acc_ancient_emperor_badge_10"
display_name = "古代帝王徽章"
description = "一枚由纯金与秘银锻造的庄严徽章，徽章上刻有永恒王朝的完整国徽——一只衔着橄榄枝的雄鹰，鹰爪下是十二个省份的徽章，鹰背后是永恒王朝的座右铭："帝国永存，人民永恒。"徽章背面刻着末代皇帝奥古斯都的最后话语："皇帝不是帝国的统治者，皇帝是帝国的守护者。如果帝国要灭亡，那皇帝应该第一个死去。"\n\n这枚徽章是奥古斯都在临终前，亲手为自己打造的最后一件饰品。他说："这枚徽章不是荣誉，它是'最后的誓言'——我发誓与帝国同生共死。"他将徽章别在了心口——那是他最后还能移动的部位，因为瘟疫已经侵蚀了他的四肢。\n\n徽章会在佩戴者面临"个人利益 vs 集体利益"的抉择时变得更加沉重——那不是魔法，而是奥古斯都的提醒。一位后来的统治者在面临放弃人民以保全自己的抉择时，徽章突然变得像铅块一样重——他说："那不是惩罚，那是奥古斯都在提醒我——'记住你的誓言，守护者。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "ancient_emperor_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ancient_emperor_badge_10" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ancient_emperor_badge_10" }
]
```

---

*套装 6–10 饰品部分完结 · 共 30 件饰品装备*

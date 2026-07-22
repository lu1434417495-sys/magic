# 传奇装备套装设计文档（套装 81–90：科幻 fantasy 混合）

> 10 套科幻与 fantasy 混合主题套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×2、中甲×3、轻甲×5。

---

## 套装八十一：蒸汽骑士（Steam Knight）

> *"蒸汽不是废气，它是'力量'——一种可以驱动任何机器的力量。"*

**套装主题**：蒸汽科技教团「永恒锅炉」的装备。这些骑士使用蒸汽动力装甲，他们的装备由铜管、齿轮和蒸汽丝制成，赋予了穿戴者蒸汽动力增强、机械修复和高温攻击的能力。集齐四件时，穿戴者获得「蒸汽之躯」——可以用蒸汽加速、释放高温蒸汽、修复机械装置。

**历史渊源**：永恒锅炉的最高骑士「锅炉之心」瓦特（另一位同名者），是一位据说发明了永动蒸汽机的传奇工程师。他说："我不是在制造机器，我是在'创造生命'——蒸汽就是我的生命力。"

---

### 81.1 蒸汽骑士头盔

```gdscript
item_id = "armor_steam_knight_head"
display_name = "蒸汽骑士头盔"
description = "一顶由铜管与蒸汽丝锻造的工业风头盔，头盔顶部有一个小型「蒸汽烟囱」——不断有白色蒸汽从中喷出。头盔的正面有一个「压力表」——显示当前蒸汽压力，压力越高，穿戴者的力量越强。头盔的两侧有「散热片」——用于散发多余的热量。\n\n瓦特在锻造这顶头盔时，将自己的「蒸汽之心」封入了锅炉。他说："这顶头盔不只是头盔，它是'蒸汽的指挥中心'——让我可以控制所有的蒸汽流动。"\n\n这顶头盔的特殊效果是：穿戴者可以「感知」到 30 尺内所有机械装置的状态（机械雷达）。且可以通过头盔「释放」蒸汽——每日三次，从烟囱喷出一道高温蒸汽（15 尺锥形 3D10 fire，目标须通过 DC16 体质豁免，失败则 blinded 1 回合）。瓦特说："蒸汽不是只能用来驱动，它也可以用来攻击——高温蒸汽可以灼伤任何敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "steam_knight_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_steam_knight_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_steam_knight_head" }
]
```

**2件套（设计预留）**：`resistance_fire` +10；免疫「高温环境」和「蒸汽窒息」。
**4件套（设计预留）**：每日一次「蒸汽加速」： bonus action，蒸汽压力提升至最大（移动力 +20、攻击检定 +2、力量检定 +5，持续 1 分钟，但之后需要 1 回合「冷却」，期间速度减半）；每日一次「蒸汽修复」： bonus action，修复一个机械装置或构造体（恢复 4D10 HP）；近战攻击附加 1D6 fire（高温蒸汽）；可以「蒸汽跳跃」——利用蒸汽推力进行超高跳跃（跳跃距离 ×2）。

---

### 81.2 蒸汽骑士板甲

```gdscript
item_id = "armor_steam_knight_body"
display_name = "蒸汽骑士板甲"
description = "一副由铜管与蒸汽丝锻造的工业风板甲，板甲表面布满了复杂的管道系统和齿轮机构。板甲的背部有一个小型「蒸汽锅炉」——不断有蒸汽在管道中流动，为穿戴者提供动力。板甲的接缝处有特殊的「蒸汽密封」——防止蒸汽泄漏，同时也防止外部液体进入。\n\n瓦特在锻造这副板甲时，将自己设计的所有蒸汽系统都编入了管道。他说："这副板甲不只是铠甲，它是'蒸汽工厂'——让我穿着一座工厂。"\n\n这副板甲的特殊效果是：穿戴者的力量视为 +2（蒸汽动力）。且可以将板甲的蒸汽「释放」——每日一次，释放所有蒸汽形成一个蒸汽云（15 尺半径，困难地形，所有生物在区域内 blinded 并每回合受到 1D6 fire，持续 1 分钟）。瓦特说："蒸汽不是只能用来驱动，它也可以用来遮蔽——用蒸汽遮蔽视线，让敌人无法瞄准。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "steam_knight_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_steam_knight_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_steam_knight_body" }
]
```

---

### 81.3 蒸汽骑士护手

```gdscript
item_id = "armor_steam_knight_hands"
display_name = "蒸汽骑士护手"
description = "一对由铜管与蒸汽丝锻造的工业风护手，护手表面布满了小型管道和阀门。握紧拳头时，蒸汽会通过管道汇聚在拳峰，形成高温蒸汽拳。松开拳头时，蒸汽会通过散热片释放。护手的掌心有「蒸汽喷嘴」——可以精确控制蒸汽的释放。\n\n瓦特在锻造这对手套时，将自己的「蒸汽之手」能力封入了管道。他说："这对手套不只是手套，它们是'蒸汽的工具'——让我可以用手操控蒸汽。"\n\n这对手套的特殊效果是：穿戴者可以用蒸汽拳进行「高温打击」——徒手攻击造成 1D8 bludgeoning + 1D8 fire（高温蒸汽）。且可以用手套「释放」精确蒸汽——每日三次，从掌心释放一道高压蒸汽（30 尺射程，2D10 fire，可以切割金属障碍物）。瓦特说："蒸汽不是只能用来加热，它也可以用来切割——高压蒸汽可以切割任何金属。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "steam_knight_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_steam_knight_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_steam_knight_hands" }
]
```

---

### 81.4 蒸汽骑士胫甲

```gdscript
item_id = "armor_steam_knight_feet"
display_name = "蒸汽骑士胫甲"
description = "一对由铜管与蒸汽丝锻造的工业风胫甲，胫甲的底部有「蒸汽推进器」——可以在需要时提供额外的推进力。每一步踏下，推进器都会发出轻微的蒸汽声——不是弱点，是力量的象征。\n\n瓦特在锻造这对胫甲时，将「蒸汽之步」能力封入了推进器。他说："这对胫甲不只是鞋子，它们是'蒸汽的推进器'——让我可以用蒸汽推动自己。"\n\n这对胫甲的特殊效果是：穿戴者可以进行「蒸汽冲锋」——每日一次，利用蒸汽推进器以三倍速度直线冲锋（路径上所有生物受到 2D10 fire 并被推后 10 尺）。且可以在湿滑表面上正常行走（蒸汽蒸发水分，增加摩擦力）。瓦特说："蒸汽骑士不需要马，他需要蒸汽——蒸汽可以带他到任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "steam_knight_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_steam_knight_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_steam_knight_feet" }
]
```

---

## 套装八十二：齿轮之心（Clockwork Heart）

> *"心脏不是血肉，它是'机器'。齿轮转动的声音，就是生命的声音。"*

**套装主题**：齿轮教团「永恒转动」的装备。这些信徒相信生命可以由齿轮和弹簧模拟，他们的装备由精密齿轮和发条制成，赋予了穿戴者精确计算、机械增强和时间操控的能力。集齐四件时，穿戴者获得「齿轮之躯」——可以用齿轮计算未来、用发条增强力量、用机械修复身体。

**历史渊源**：永恒转动的最高祭司「齿轮之心」特斯拉（另一位同名者），是一位据说可以用齿轮模拟任何生命功能的传奇工程师。他说："我不是在制造机器，我是在'理解生命'——生命就是一台精密的机器。"

---

### 82.1 齿轮之心头冠

```gdscript
item_id = "armor_clockwork_heart_head"
display_name = "齿轮之心头冠"
description = "一顶由精密齿轮与发条丝锻造的奇异头冠，头冠内部有一个小型「机械脑」——由无数微型齿轮组成，可以代替穿戴者的大脑进行计算。头冠的表面有一个「时间盘」——显示当前时间（精确到毫秒），并且可以预测接下来 1 秒内的事件。\n\n特斯拉在锻造这头冠时，将自己的「计算能力」封入了机械脑。他说："这头冠不只是头冠，它是'第二大脑'——让我可以同时思考两个问题。"\n\n这头冠的特殊效果是：穿戴者可以「计算」未来——每日一次，预测接下来 1 分钟内的一个事件（DM 描述一个即将发生的事件，准确率 75%）。且可以通过头冠「精确计算」——所有需要精确度的检定（开锁、陷阱解除、射击等）+3。特斯拉说："未来不是随机的，它是'可以计算的'——只要你有足够的齿轮和足够的数据。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "clockwork_heart_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_clockwork_heart_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_clockwork_heart_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +2；所有需要「精确度」的检定 +3。
**4件套（设计预留）**：每日一次「时间预测」：预知接下来 1 分钟内的一个攻击（可以选择让该攻击自动命中或自动 miss）；每日一次「发条加速」： bonus action，上紧发条（移动力 +15、攻击检定 +2、先攻 +5，持续 1 分钟，但之后需要 1 回合「上发条」）；可以「机械修复」—— bonus action，修复自己的身体（恢复 2D10 HP，齿轮替换损坏的组织）；免疫「毒素」和「疾病」（机械身体不受影响）。

---

### 82.2 齿轮之心链甲

```gdscript
item_id = "armor_clockwork_heart_body"
display_name = "齿轮之心链甲"
description = "一副由精密齿轮与发条丝锻造而成的奇异链甲，链甲的每一块甲片都是一个小型齿轮——不断有齿轮在转动、咬合、释放能量。链甲的背部有一个小型「发条盒」——可以为所有齿轮提供动力。链甲的接缝处有特殊的「齿轮锁」——只有正确的齿轮组合才能打开。\n\n特斯拉在锻造这副链甲时，将自己设计的所有齿轮系统都编入了甲片。他说："这副链甲不只是铠甲，它是'移动的钟表'——让我穿着一座钟表。"\n\n这副链甲的特殊效果是：穿戴者可以将链甲的齿轮「加速」——每日一次，让所有齿轮同时加速（移动力 +15、攻击检定 +2，持续 1 分钟）。且链甲会「自动修复」——每小时恢复 1 HP（齿轮的自我修复）。且可以将链甲的发条「释放」——每日一次，释放所有能量进行一次「齿轮风暴」（15 尺半径，所有生物受到 3D8 slashing，因为飞出的齿轮切割一切）。特斯拉说："齿轮不是只能转动，它也可以飞行——飞行并切割一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "clockwork_heart_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_clockwork_heart_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_clockwork_heart_body" }
]
```

---

### 82.3 齿轮之心手套

```gdscript
item_id = "armor_clockwork_heart_hands"
display_name = "齿轮之心手套"
description = "一对由精密齿轮与发条丝锻造而成的奇异手套，手套的每一根手指都是一个小型机械臂——有可伸缩的工具、隐藏的齿轮飞镖和精密的传感器。手套的掌心有「核心接口」——可以直接连接任何机械装置。\n\n特斯拉在锻造这对手套时，将自己的「机械之手」能力封入了齿轮。他说："这对手套不只是手套，它们是'机械的工具箱'——让我可以用手指创造任何机械。"\n\n这对手套的特殊效果是：穿戴者可以用手指「制造」临时机械—— bonus action，制造一个微型机械装置（持续 1 分钟）：齿轮飞镖（30 尺射程 1D8 piercing）、弹簧跳板（跳跃距离 ×2）、或小型护盾（AC +1）。每日三次。且可以用手套「修复」机械—— bonus action，触碰一个损坏的机械装置，恢复它的功能（恢复 2D8 HP）。特斯拉说："机械师最强大的不是他的工具，是他的手——手可以创造出工具无法创造的东西。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "clockwork_heart_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_clockwork_heart_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_clockwork_heart_hands" }
]
```

---

### 82.4 齿轮之心软靴

```gdscript
item_id = "armor_clockwork_heart_feet"
display_name = "齿轮之心软靴"
description = "一对由精密齿轮与发条丝锻造而成的奇异软靴，软靴的底部有「齿轮垫」——可以在任何地面上保持稳定。软靴的鞋跟有「发条弹簧」——可以在需要时提供额外的弹跳力。每一步踏下，齿轮都会发出轻微的咔嗒声——不是弱点，是节奏，是生命的声音。\n\n特斯拉在锻造这对软靴时，将「齿轮之步」能力封入了发条。他说："这对软靴不只是鞋子，它们是'齿轮的移动'——让我可以用齿轮的方式移动。"\n\n这对软靴的特殊效果是：穿戴者可以在任何机械装置上正常行走（包括陷阱、齿轮和传送带）。且可以用「弹簧跳跃」——每日三次，利用发条弹簧进行超高跳跃（跳跃距离 ×2，且可以在空中改变方向一次）。特斯拉说："齿轮师不需要路，他可以创造路——用齿轮创造属于自己的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "clockwork_heart_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_clockwork_heart_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_clockwork_heart_feet" }
]
```

---

## 套装八十三：以太行者（Aether Walker）

> *"以太不是真空，它是'最纯净的物质'。学会在以太中行走，你就能到达任何地方。"*

**套装主题**：以太探索者结社「星空之航」的装备。这些探索者学会了操控以太能量，他们的装备由以太结晶和星辰丝制成，赋予了穿戴者在虚空中生存、释放以太攻击和进行星际传送的能力。集齐四件时，穿戴者获得「以太之躯」——可以在虚空中呼吸、释放以太射线、进行短距离传送。

**历史渊源**：星空之航的最高探索者「以太之心」爱迪生（另一位同名者），是一位据说发现了以太能源的传奇科学家。他说："我不是在探索太空，我是在'探索以太'——以太是太空的本质，是连接一切的媒介。"

---

### 83.1 以太行者头冠

```gdscript
item_id = "armor_aether_walker_head"
display_name = "以太行者头冠"
description = "一顶由以太结晶与星辰丝编织而成的奇异头冠，头冠上镶嵌着一颗「以太核心」——一颗能够储存和释放以太能量的魔法宝石。头冠的表面不断有微弱的星光在流动，仿佛穿戴者头顶着一片小型星空。\n\n爱迪生在编织这头冠时，从以太裂缝中取出了一块结晶。他说："这块结晶不是被取出的，是'被捕获的'——我捕获了一段以太，让它为我服务。"\n\n这头冠的特殊效果是：穿戴者可以在虚空中呼吸（以太供氧）。且可以通过头冠「感知」到 1 里内所有以太能量的流动（以太雷达）。且可以在星光下「充能」——在夜晚的户外，每小时恢复 1 HP（星光充能）。爱迪生说："以太不是只能用来呼吸，它也可以用来感知——感知宇宙的脉动，感知星辰的呼吸。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "aether_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_aether_walker_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_aether_walker_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；免疫「真空」和「辐射」环境。
**4件套（设计预留）**：每日三次「以太射线」： bonus action，释放一道以太射线（60 尺射程 3D10 force，可以穿透实体障碍物）；每日一次「以太传送」： bonus action，传送至 60 尺内任意位置（如同 dimension door，但无需 verbal component）；在星光下所有检定 +1（以太充能）；可以「以太步」—— bonus action，1 回合内免疫所有物理伤害（身体部分以太化）。

---

### 83.2 以太行者长袍

```gdscript
item_id = "armor_aether_walker_body"
display_name = "以太行者长袍"
description = "一件由以太结晶与星辰丝编织而成的奇异长袍，长袍表面不断有星光在流动，仿佛穿戴者身上披着一片星空。这件长袍没有重量——它完全由以太能量支撑，在微重力环境中几乎没有质量。\n\n爱迪生在编织这件长袍时，将一整片星空的能量都编入了丝线。他说："这件长袍不只是衣服，它是'星空的碎片'——让我穿着整个宇宙。"\n\n这件长袍的特殊效果是：穿戴者的重量减半（以太浮力）。且可以从高处「滑翔」——免疫 falling damage（以太缓冲）。且可以通过长袍「吸收」星光——在夜晚的户外，每小时恢复 1 HP（星光治愈）。爱迪生说："以太不是只能用来传送，它也可以用来保护——用星光来缓冲坠落，用虚空来隔绝伤害。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "aether_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_aether_walker_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_aether_walker_body" }
]
```

---

### 83.3 以太行者手套

```gdscript
item_id = "armor_aether_walker_hands"
display_name = "以太行者手套"
description = "一对由以太结晶与星辰丝编织而成的奇异手套，手套表面镶嵌着小型以太结晶。挥舞手臂时，结晶会划出星光轨迹——不是魔法，是物理现象，因为以太结晶在摩擦空气时会发光。手套的掌心有「以太聚焦器」——可以聚集和释放以太能量。\n\n爱迪生在编织这对手套时，将自己的「以太之手」能力封入了结晶。他说："这对手套不只是手套，它们是'以太的延伸'——让我可以用手操控以太。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「释放」以太能量——每日三次，释放一道以太冲击（30 尺射程，2D10 force，可以穿透透明障碍物如玻璃和力场）。且可以用手套「以太化」一个物体—— bonus action，触碰一个物体，让它暂时变得无形（可以穿过实体障碍物，持续 1 回合）。爱迪生说："以太不是只能用来攻击，它也可以用来穿越——穿越任何障碍，到达任何目的地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "aether_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_aether_walker_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_aether_walker_hands" }
]
```

---

### 83.4 以太行者便鞋

```gdscript
item_id = "armor_aether_walker_feet"
display_name = "以太行者便鞋"
description = "一对由以太结晶与星辰丝编织而成的奇异便鞋，便鞋没有与地面接触——它们悬浮在地面之上约半寸，因为以太能量让穿戴者几乎失重。行走时，便鞋不会发出任何声音，也不会留下任何足迹。\n\n爱迪生在编织这对便鞋时，将自己的「以太之步」能力封入了结晶。他说："这对便鞋不只是鞋子，它们是'以太的滑板'——让我可以在以太的表面上滑行。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（以太隔绝声音）。且可以在任何表面上行走——包括水面、熔岩和虚空（以太平台）。且可以用「以太滑行」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。爱迪生说："以太行者不需要路，他只需要以太——以太无处不在，所以以太行者可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "aether_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_aether_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_aether_walker_feet" }
]
```

---

## 套装八十四：磁力大师（Magnet Master）

> *"磁力不是力，它是'爱的表现'——因为磁力让相隔万里的东西相互吸引。"*

**套装主题**：磁力操控者教团「两极之力」的装备。这些操控者学会了操控磁力，他们的装备由磁性金属和磁力丝制成，赋予了穿戴者吸引金属、排斥攻击和操控磁场的能力。集齐四件时，穿戴者获得「磁力之躯」——可以吸引或排斥金属、用磁力飞行、释放电磁脉冲。

**历史渊源**：两极之力的最高操控者「磁力之心」法拉第（另一位同名者），是一位据说可以操控地球磁场的传奇科学家。他说："我不是在操控磁力，我是在'与磁力对话'——磁力是宇宙的语言，我学会了说这种语言。"

---

### 84.1 磁力大师头冠

```gdscript
item_id = "armor_magnet_master_head"
display_name = "磁力大师头冠"
description = "一顶由磁性金属与磁力丝锻造的奇异头冠，头冠上镶嵌着一颗「磁极石」——一颗能够储存和释放磁力的魔法宝石。头冠的表面不断有微弱的磁场线在流动，可以看到细小的铁屑在头冠周围悬浮旋转。\n\n法拉第在锻造这头冠时，从地球磁极取下了一块磁石。他说："这块磁石不是被取下的，是'被借用的'——我借用了地球的力量，让我可以操控磁力。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 30 尺内所有金属物体的位置和类型（金属雷达）。且可以通过头冠「吸引」或「排斥」金属——每日三次，吸引 15 尺内的一个金属物体（包括武器、护甲或构造体，目标须通过 DC16 力量豁免，失败则被拉至 5 尺内）或排斥（推后 15 尺）。法拉第说："磁力不是只能用来吸引，它也可以用来排斥——只是需要正确的极性。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "magnet_master_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_magnet_master_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_magnet_master_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +2；对金属构造体的攻击检定 +2。
**4件套（设计预留）**：每日一次「磁力飞行」： bonus action，利用地球磁场飞行 1 分钟（速度 40 尺）；每日一次「电磁脉冲」：15 尺半径内所有电子设备失效 1 分钟，构造体受到 4D10 lightning 并须通过 DC16 体质豁免，失败则 stunned 1 回合；可以「磁力护盾」—— bonus action，创造一个磁力场（AC +2，任何 metal 武器攻击有 50% 概率被偏转）；可以「操控金属」—— bonus action，操控 15 尺内的金属物体（如同 telekinesis，但只对金属有效）。

---

### 84.2 磁力大师链甲

```gdscript
item_id = "armor_magnet_master_body"
display_name = "磁力大师链甲"
description = "一副由磁性金属与磁力丝锻造而成的奇异链甲，链甲的每一块甲片都是一个小型磁铁——不断有磁场线在甲片之间流动。链甲的表面不断有细小的金属物体在悬浮旋转——铁屑、针、钉子，它们是链甲的「装饰」，也是「武器」。\n\n法拉第在锻造这副链甲时，将自己设计的所有磁力系统都编入了甲片。他说："这副链甲不只是铠甲，它是'移动的磁场'——让我穿着一座磁场。"\n\n这副链甲的特殊效果是：穿戴者可以将链甲的磁力「释放」——每日一次，释放所有磁力形成一个磁力风暴（15 尺半径，所有 metal 武器和护甲被吸引至中心，生物须通过 DC16 力量豁免，失败则武器脱手并被吸引至中心）。且链甲会「偏转」金属攻击——任何 metal 武器攻击有 25% 概率被磁力偏转（miss）。法拉第说："磁力不是只能用来吸引，它也可以用来防御——用磁场来偏转金属攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "magnet_master_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_magnet_master_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_magnet_master_body" }
]
```

---

### 84.3 磁力大师手套

```gdscript
item_id = "armor_magnet_master_hands"
display_name = "磁力大师手套"
description = "一对由磁性金属与磁力丝锻造而成的奇异手套，手套的掌心有「磁极」——可以产生强大的磁场。握紧拳头时，磁场会增强；张开手掌时，磁场会减弱。手套的指尖有「磁力聚焦器」——可以精确控制磁力的方向和强度。\n\n法拉第在锻造这对手套时，将自己的「磁力之手」能力封入了磁极。他说："这对手套不只是手套，它们是'磁力的延伸'——让我可以用手操控磁力。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「吸引」或「排斥」——每日三次，吸引或排斥 15 尺内的一个金属物体（包括武器、护甲或构造体，目标须通过 DC16 力量豁免）。且可以用手套「磁化」一个物体—— bonus action，触碰一个金属物体，让它暂时变成磁铁（可以吸引其他金属，持续 1 分钟）。法拉第说："磁力不是只能用来移动物体，它也可以用来改变物体——让任何金属变成磁铁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "magnet_master_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_magnet_master_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_magnet_master_hands" }
]
```

---

### 84.4 磁力大师软靴

```gdscript
item_id = "armor_magnet_master_feet"
display_name = "磁力大师软靴"
description = "一对由磁性金属与磁力丝锻造而成的奇异软靴，软靴的底部有「磁极垫」——可以在金属表面上保持强大的抓握力。在金属地面上行走时，软靴会让穿戴者如同生根一般稳固。\n\n法拉第在锻造这对软靴时，将「磁力之步」能力封入了磁极。他说："这对软靴不只是鞋子，它们是'磁力的锚'——让我可以在任何金属表面上稳固站立。"\n\n这对软靴的特殊效果是：穿戴者可以在金属墙壁上正常行走（磁力吸附）。且可以在金属天花板上行正常行走（磁力反转）。且可以用「磁力冲刺」——每日一次，利用磁力排斥以三倍速度直线移动（移动力 ×3，可以穿过金属障碍物，免疫借机攻击，持续 1 回合）。法拉第说："磁力大师不需要楼梯，他可以用磁力——在金属建筑中，磁力大师无所不能。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "magnet_master_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_magnet_master_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_magnet_master_feet" }
]
```

---

## 套装八十五：光子剑士（Photon Swordsman）

> *"光不是只能看的，它也可以切割——只要足够集中。"*

**套装主题**：光剑士团「永恒之光」的装备。这些剑士使用光子武器，他们的装备由光导纤维和光子丝制成，赋予了穿戴者释放光刃、用光速移动和操控光线的能力。集齐四件时，穿戴者获得「光子之躯」——可以释放光刃、用光速移动、用光线治愈。

**历史渊源**：永恒之光的最高剑士「光子之心」爱因斯坦（另一位同名者），是一位据说发现了光子能量的传奇科学家。他说："我不是在挥舞光，我是在'与光共舞'——光是我的伙伴，我的武器，我的盾牌。"

---

### 85.1 光子剑士头冠

```gdscript
item_id = "armor_photon_swordsman_head"
display_name = "光子剑士头冠"
description = "一顶由光导纤维与光子丝编织而成的辉煌头冠，头冠上镶嵌着一颗「光子核心」——一颗能够储存和释放光子能量的魔法宝石。头冠的表面不断有光线在流动，仿佛穿戴者头顶着一盏永不熄灭的灯。\n\n爱因斯坦在编织这头冠时，将自己的「光子之眼」封入了核心。他说："这头冠不只是头冠，它是'光的聚焦器'——让我可以控制所有的光子。"\n\n这头冠的特殊效果是：穿戴者可以「看见」所有光谱——包括红外线、紫外线和 X 射线（如同 truesight + 热成像 + X 光）。且可以通过头冠「释放」强光——每日三次，释放一道强光（15 尺锥形，所有生物须通过 DC16 体质豁免，失败则 blinded 1 回合）。爱因斯坦说："光不是只能用来照明，它也可以用来攻击——强光可以致盲任何敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "photon_swordsman_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_photon_swordsman_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_photon_swordsman_head" }
]
```

**2件套（设计预留）**：`perception_bonus` +3；免疫「blinded」（光子之眼适应强光）。
**4件套（设计预留）**：每日三次「光子刃」： bonus action，从手掌释放一把光子剑（1D10 radiant，持续 1 分钟，可以切割任何实体障碍物）；每日一次「光速移动」： bonus action，以光速移动至 30 尺内任意位置（如同 dimension door，但只能直线移动，路径上所有生物受到 2D10 radiant）；可以「光子治愈」： bonus action，用光线治愈一个盟友（恢复 2D10 HP，每日三次）；在强光下 AC +2（光线护盾）。

---

### 85.2 光子剑士长袍

```gdscript
item_id = "armor_photon_swordsman_body"
display_name = "光子剑士长袍"
description = "一件由光导纤维与光子丝编织而成的辉煌长袍，长袍表面不断有光线在流动，仿佛穿戴者身上披着一道彩虹。这件长袍没有固定的颜色——它会根据当前使用的光子技能变化：攻击时是红色，防御时是蓝色，治愈时是绿色，移动时是白色。\n\n爱因斯坦在编织这件长袍时，将一整道光都编入了丝线。他说："这件长袍不只是衣服，它是'光的容器'——让我穿着整个光谱。"\n\n这件长袍的特殊效果是：穿戴者可以随时「改变」长袍的颜色和亮度（被动效果，无需动作）。且可以通过长袍「释放」光子——每日一次，释放一道光子束（60 尺射程 4D10 radiant，可以穿透任何实体障碍物）。爱因斯坦说："光不是只能用来照明，它也可以用来穿透——穿透任何障碍，到达任何目的地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "photon_swordsman_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_photon_swordsman_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_photon_swordsman_body" }
]
```

---

### 85.3 光子剑士手套

```gdscript
item_id = "armor_photon_swordsman_hands"
display_name = "光子剑士手套"
description = "一对由光导纤维与光子丝编织而成的辉煌手套，手套表面不断有光线在流动。手套的掌心有「光子发射器」——可以释放和操控光子。手套的指尖有「光刃聚焦器」——可以将光子聚焦成锋利的光刃。\n\n爱因斯坦在编织这对手套时，将自己的「光子之手」能力封入了发射器。他说："这对手套不只是手套，它们是'光的延伸'——让我可以用手操控光子。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「释放」光子刃—— bonus action，从掌心释放一把光子剑（1D10 radiant，持续 1 分钟）。每日三次。且可以用手套「释放」光子冲击——每日三次，从掌心释放一道光子冲击（30 尺射程 2D10 radiant，可以穿透透明障碍物如玻璃和冰）。爱因斯坦说："光不是只能用来切割，它也可以用来冲击——用光子的力量冲击任何敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "photon_swordsman_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_photon_swordsman_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_photon_swordsman_hands" }
]
```

---

### 85.4 光子剑士便鞋

```gdscript
item_id = "armor_photon_swordsman_feet"
display_name = "光子剑士便鞋"
description = "一对由光导纤维与光子丝编织而成的辉煌便鞋，便鞋没有与地面接触——它们悬浮在地面之上约半寸，因为光子能量让穿戴者几乎失重。行走时，便鞋会在地面上留下短暂的光迹——不是弱点，是美丽，也是标记。\n\n爱因斯坦在编织这对便鞋时，将自己的「光子之步」能力封入了发射器。他说："这对便鞋不只是鞋子，它们是'光的滑板'——让我可以在光的表面上滑行。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（光子隔绝声音）。且可以用「光速步」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。且在强光下，移动力 +15（光子加速）。爱因斯坦说："光子剑士不需要路，他只需要光——光无处不在，所以光子剑士可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "photon_swordsman_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_photon_swordsman_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_photon_swordsman_feet" }
]
```

---

## 套装八十六：声波战士（Sonic Warrior）

> *"声音不是空气，它是'力量'——一种可以震碎岩石、穿透盔甲的力量。"*

**套装主题**：声波操控者教团「共振之环」的装备。这些操控者学会了操控声波，他们的装备由共振晶体和声波丝制成，赋予了穿戴者释放音波攻击、用声波探测和用频率干扰敌人的能力。集齐四件时，穿戴者获得「声波之躯」——可以释放致命音波、用声波探测隐藏敌人、用频率干扰法术。

**历史渊源**：共振之环的最高操控者「声波之心」贝多芬（另一位同名者），是一位据说可以用声波操控物质的传奇音乐家。他说："我不是在演奏音乐，我是在'操控世界'——世界就是一首巨大的交响乐，我只是学会了如何演奏它。"

---

### 86.1 声波战士头冠

```gdscript
item_id = "armor_sonic_warrior_head"
display_name = "声波战士头冠"
description = "一顶由共振晶体与声波丝编织而成的奇异头冠，头冠上镶嵌着一颗「声波核心」——一颗能够储存和释放声波能量的魔法宝石。头冠的表面不断有微弱的声波在振动，可以看到细小的灰尘在头冠周围悬浮振动。\n\n贝多芬在编织这头冠时，将自己的「绝对音感」封入了核心。他说："这头冠不只是头冠，它是'耳朵的延伸'——让我可以听见世界上所有的声音。"\n\n这头冠的特殊效果是：穿戴者可以「听见」所有频率——包括超声波和次声波（如同 blindsight 60 尺 + 生命感知）。且可以通过头冠「释放」音波——每日三次，释放一道音波（15 尺锥形 3D10 thunder，目标须通过 DC16 体质豁免，失败则 deafened 1 回合）。贝多芬说："声音不是只能用来听，它也可以用来攻击——音波可以震碎任何敌人的耳膜。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "sonic_warrior_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_sonic_warrior_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_sonic_warrior_head" }
]
```

**2件套（设计预留）**：`perception_bonus` +3；免疫「deafened」（声波战士的耳朵已经适应了巨响）。
**4件套（设计预留）**：每日一次「共振粉碎」： bonus action，释放一道共振波（15 尺锥形 4D10 thunder + 2D10 force，可以粉碎岩石和金属障碍物，目标须通过 DC16 体质豁免，失败则 deafened 1 分钟并 stunned 1 回合）；每日一次「频率干扰」： bonus action，释放干扰频率（30 尺半径内所有施法者须通过 DC16 体质豁免，失败则本回合不能施法）；可以「声波探测」—— bonus action，释放超声波（60 尺 blindsight，持续 1 回合）；可以用「声波护盾」—— bonus action，创造音波护盾（AC +2，任何 melee 攻击者受到 1D6 thunder）。

---

### 86.2 声波战士长袍

```gdscript
item_id = "armor_sonic_warrior_body"
display_name = "声波战士长袍"
description = "一件由共振晶体与声波丝编织而成的奇异长袍，长袍表面不断有微弱的声波在振动，可以看到细小的灰尘在长袍表面悬浮振动。这件长袍没有固定的形状——它会随着声波的变化而变化：高音时紧缩，低音时松散。\n\n贝多芬在编织这件长袍时，将自己最伟大的交响乐都编入了丝线。他说："这件长袍不只是衣服，它是'交响乐的容器'——让我穿着整个交响乐团。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的声波「释放」——每日一次，释放所有声波形成一个音波风暴（15 尺半径，所有生物每回合受到 2D6 thunder 并被 deafened，持续 1 分钟）。且长袍会「共鸣」——当附近有声音时，长袍的 AC +1（声波护盾）。贝多芬说："声音不是只能用来攻击，它也可以用来防御——用声波来偏转攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "sonic_warrior_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_sonic_warrior_body" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_sonic_warrior_body" }
]
```

---

### 86.3 声波战士手套

```gdscript
item_id = "armor_sonic_warrior_hands"
display_name = "声波战士手套"
description = "一对由共振晶体与声波丝编织而成的奇异手套，手套表面不断有微弱的声波在振动。手套的掌心有「声波聚焦器」——可以聚集和释放声波。手套的指尖有「频率调节器」——可以精确控制声波的频率和强度。\n\n贝多芬在编织这对手套时，将自己的「声波之手」能力封入了聚焦器。他说："这对手套不只是手套，它们是'声波的延伸'——让我可以用手操控声波。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「释放」声波冲击——每日三次，从掌心释放一道声波冲击（30 尺射程 2D10 thunder，可以穿透实体障碍物如墙壁）。且可以用手套「调音」—— bonus action，调整一个物体的共振频率（可以让金属变脆——下一击 damage +1D6，或让玻璃碎裂——5 尺半径 1D6 slashing）。贝多芬说："声音不是只能用来攻击，它也可以用来改变——改变物质的性质，改变战场的地形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "sonic_warrior_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_sonic_warrior_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_sonic_warrior_hands" }
]
```

---

### 86.4 声波战士便鞋

```gdscript
item_id = "armor_sonic_warrior_feet"
display_name = "声波战士便鞋"
description = "一对由共振晶体与声波丝编织而成的奇异便鞋，便鞋的底部有「声波垫」——可以在任何地面上产生微弱的声波振动。行走时，便鞋不会发出任何声音——因为声波被控制在特定频率，人耳无法听见。\n\n贝多芬在编织这对便鞋时，将「声波之步」能力封入了声波垫。他说："这对便鞋不只是鞋子，它们是'声波的道路'——让我可以用声波在任何地方行走。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（超声波行走）。且可以用「声波冲刺」——每日一次，利用声波推进以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。贝多芬说："声波战士不需要路，他只需要空气——空气无处不在，所以声波战士可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "sonic_warrior_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_sonic_warrior_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_sonic_warrior_feet" }
]
```

---

## 套装八十七：重力行者（Gravity Walker）

> *"重力不是束缚，它是'舞蹈的舞伴'。学会与它共舞，你就能飞翔。"*

**套装主题**：重力操控者教团「质量之核」的装备。这些操控者学会了操控重力，他们的装备由重力石和引力丝制成，赋予了穿戴者改变重力、飞行和用重力压碎敌人的能力。集齐四件时，穿戴者获得「重力之躯」——可以改变局部重力、用重力飞行、用重力压碎敌人。

**历史渊源**：质量之核的最高操控者「重力之心」牛顿（另一位同名者），是一位据说发现了重力秘密的传奇科学家。他说："我不是在对抗重力，我是在'与它对话'——重力是宇宙的语言，我学会了说这种语言。"

---

### 87.1 重力行者头冠

```gdscript
item_id = "armor_gravity_walker_head"
display_name = "重力行者头冠"
description = "一顶由重力石与引力丝编织而成的奇异头冠，头冠上镶嵌着一颗「重力核心」——一颗能够储存和释放重力能量的魔法宝石。头冠的表面不断有微弱的引力波在流动，可以看到细小的物体在头冠周围微微悬浮或下沉。\n\n牛顿在编织这头冠时，从地球核心取下了一块重力石。他说："这块石头不是被取下的，是'被借用的'——我借用了地球的力量，让我可以操控重力。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 30 尺内所有物体的重量和质量（重力雷达）。且可以通过头冠「改变」局部重力——每日三次，改变 15 尺半径内的重力（重力加倍：所有生物速度减半、跳跃距离减半；或重力减半：所有生物速度翻倍、跳跃距离翻倍，持续 1 回合）。牛顿说："重力不是只能向下，它也可以向上——只是需要正确的角度和足够的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "gravity_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_gravity_walker_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_gravity_walker_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +2；免疫「falling damage」（重力操控）。
**4件套（设计预留）**：每日一次「重力飞行」： bonus action，抵消自身重力，飞行 1 分钟（速度 40 尺）；每日一次「重力压碎」： bonus action，增加一个目标的重力（目标须通过 DC16 力量豁免，失败则 restrained 1 回合并受到 3D10 force）；可以「重力跳跃」—— bonus action，减少自身重力（跳跃距离 ×3，持续 1 回合）；可以「重力护盾」—— bonus action，创造重力场（AC +2，任何 melee 攻击者受到 1D6 force，因为重力增加了）。

---

### 87.2 重力行者链甲

```gdscript
item_id = "armor_gravity_walker_body"
display_name = "重力行者链甲"
description = "一副由重力石与引力丝编织而成的奇异链甲，链甲的每一块甲片都是一个小型重力石——不断有引力波在甲片之间流动。链甲的重量可以根据需要变化：需要防御时加重（AC +1），需要移动时减轻（移动力 +10）。\n\n牛顿在锻造这副链甲时，将自己设计的所有重力系统都编入了甲片。他说："这副链甲不只是铠甲，它是'移动的重力场'——让我穿着一座重力场。"\n\n这副链甲的特殊效果是：穿戴者可以将链甲的重力「调整」—— bonus action，增加或减少链甲的重力（增加：AC +1 但速度 -5；减少：AC -1 但速度 +10，持续 1 回合）。且可以将链甲的重力「释放」——每日一次，释放所有重力形成一个重力井（15 尺半径，所有生物被拉向中心，须通过 DC16 力量豁免，失败则移动至中心并 prone）。牛顿说："重力不是只能用来飞行，它也可以用来困住敌人——用重力井将敌人拉向中心，然后一举消灭。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "gravity_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_gravity_walker_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_gravity_walker_body" }
]
```

---

### 87.3 重力行者手套

```gdscript
item_id = "armor_gravity_walker_hands"
display_name = "重力行者手套"
description = "一对由重力石与引力丝编织而成的奇异手套，手套的掌心有「重力聚焦器」——可以产生局部的重力场。握紧拳头时，重力会增强；张开手掌时，重力会减弱。手套的指尖有「引力纹」——可以精确控制重力的方向和强度。\n\n牛顿在锻造这对手套时，将自己的「重力之手」能力封入了聚焦器。他说："这对手套不只是手套，它们是'重力的延伸'——让我可以用手操控重力。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「吸引」或「排斥」——每日三次，吸引或排斥 15 尺内的一个物体（包括生物，目标须通过 DC16 力量豁免，失败则被拉至 5 尺内或推后 15 尺）。且可以用手套「改变」物体的重量—— bonus action，触碰一个物体，让它变轻（重量减半，持续 1 分钟）或变重（重量翻倍，移动力 -10）。牛顿说："重力不是只能用来移动物体，它也可以用来改变物体——让任何物体变轻或变重。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "gravity_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_gravity_walker_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_gravity_walker_hands" }
]
```

---

### 87.4 重力行者软靴

```gdscript
item_id = "armor_gravity_walker_feet"
display_name = "重力行者软靴"
description = "一对由重力石与引力丝编织而成的奇异软靴，软靴的底部有「重力垫」——可以在任何地面上保持稳定。软靴的鞋跟有「反重力弹簧」——可以在需要时提供额外的弹跳力。\n\n牛顿在锻造这对软靴时，将「重力之步」能力封入了重力垫。他说："这对软靴不只是鞋子，它们是'重力的锚'——让我可以在任何重力环境中稳固站立。"\n\n这对软靴的特殊效果是：穿戴者可以在任何重力环境中正常行走（包括零重力、高重力和反重力环境）。且可以用「重力跳跃」——每日三次，利用反重力进行超高跳跃（跳跃距离 ×3，且可以在空中改变方向一次）。牛顿说："重力行者不需要翅膀，他可以用重力——在重力的舞蹈中，重力行者可以到达任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "gravity_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_gravity_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_gravity_walker_feet" }
]
```

---

## 套装八十八：量子幽灵（Quantum Ghost）

> *"量子不是粒子，它是'可能性'。在量子世界中，一切可能同时发生——只是需要正确的观察者。"*

**套装主题**：量子探索者结社「叠加之态」的装备。这些探索者学会了操控量子状态，他们的装备由量子晶体和概率丝制成，赋予了穿戴者叠加状态、量子隧穿和概率操控的能力。集齐四件时，穿戴者获得「量子之躯」——可以同时存在于多个位置、穿过实体障碍物、操控概率。

**历史渊源**：叠加之态的最高探索者「量子之心」薛定谔（另一位同名者），是一位据说可以同时存在于生死之间的传奇科学家。他说："我不是活着，也不是死了——我是'叠加态'，直到有人观察我。"

---

### 88.1 量子幽灵头冠

```gdscript
item_id = "armor_quantum_ghost_head"
display_name = "量子幽灵头冠"
description = "一顶由量子晶体与概率丝编织而成的奇异头冠，头冠上镶嵌着一颗「量子核心」——一颗能够同时存在于多个状态的魔法宝石。头冠的表面不断有微弱的闪烁——那不是光线，是量子态的叠加和坍缩。当你看着它时，它既在这里，又不在这里。\n\n薛定谔在编织这头冠时，将自己的「量子态」封入了核心。他说："这头冠不只是头冠，它是'叠加态的容器'——让我可以同时存在于多个状态。"\n\n这头冠的特殊效果是：穿戴者可以「叠加」——每日一次，同时存在于两个位置（如同 mirror image，但影像是实体，可以进行简单动作，持续 1 回合）。且可以通过头冠「观测」—— bonus action，观测一个目标，迫使它的量子态坍缩（目标须通过 DC16 智慧豁免，失败则 stunned 1 回合，因为现实突然变得确定）。薛定谔说："量子不是只能用来移动，它也可以用来攻击——用观测来迫使敌人的量子态坍缩。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "quantum_ghost_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_quantum_ghost_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_quantum_ghost_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；免疫「确定性的攻击」（25% 概率 miss，因为量子不确定性）。
**4件套（设计预留）**：每日两次「量子隧穿」： bonus action，穿过实体障碍物（如同 etherealness，但只能持续 1 回合）；每日一次「概率操控」： bonus action，改变一个 d20 检定的结果（可以重掷一次，选择更好的结果）；被攻击时 25% 概率触发「量子闪避」：攻击穿过身体（miss）；每日一次「叠加攻击」：同时从两个位置攻击一个目标（两次攻击，每次伤害减半）。

---

### 88.2 量子幽灵长袍

```gdscript
item_id = "armor_quantum_ghost_body"
display_name = "量子幽灵长袍"
description = "一件由量子晶体与概率丝编织而成的奇异长袍，长袍表面不断有微弱的闪烁——那不是光线，是量子态的叠加和坍缩。这件长袍没有固定的形状——它同时存在于多种状态，直到被观测时才确定。\n\n薛定谔在编织这件长袍时，将自己的「量子叠加态」编入了丝线。他说："这件长袍不只是衣服，它是'叠加态的化身'——让我穿着多种可能性。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的量子态「释放」——每日一次，释放叠加态形成一个量子云（15 尺半径，所有生物在区域内攻击检定 -3，因为目标同时存在于多个位置，持续 1 分钟）。且长袍会「不确定性」——被攻击时 25% 概率 miss（因为穿戴者的位置不确定）。薛定谔说："量子不是只能用来移动，它也可以用来防御——用不确定性来躲避攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "quantum_ghost_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_quantum_ghost_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_quantum_ghost_body" }
]
```

---

### 88.3 量子幽灵手套

```gdscript
item_id = "armor_quantum_ghost_hands"
display_name = "量子幽灵手套"
description = "一对由量子晶体与概率丝编织而成的奇异手套，手套表面不断有微弱的闪烁。手套的掌心有「量子聚焦器」——可以操控量子态。手套的指尖有「概率调节器」——可以改变事件的概率。\n\n薛定谔在编织这对手套时，将自己的「量子之手」能力封入了聚焦器。他说："这对手套不只是手套，它们是'概率的延伸'——让我可以用手操控概率。"\n\n这对手套的特殊效果是：穿戴者可以用手指「改变概率」——每日三次，改变一个 d20 检定的结果（可以重掷一次）。且可以用手套「量子隧穿」—— bonus action，让一只手穿过实体障碍物（可以穿过墙壁抓取物品或攻击，持续 1 回合）。薛定谔说："量子不是只能用来移动，它也可以用来偷窃——用量子隧穿穿过墙壁，偷走任何东西。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "quantum_ghost_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_quantum_ghost_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_quantum_ghost_hands" }
]
```

---

### 88.4 量子幽灵便鞋

```gdscript
item_id = "armor_quantum_ghost_feet"
display_name = "量子幽灵便鞋"
description = "一对由量子晶体与概率丝编织而成的奇异便鞋，便鞋没有与地面接触——它们同时存在于多个位置，直到被观测时才确定位置。行走时，便鞋会在地面上留下多个重叠的足迹——不是弱点，是迷惑，让追踪者无法确定真实方向。\n\n薛定谔在编织这对便鞋时，将自己的「量子之步」能力封入了概率丝。他说："这对便鞋不只是鞋子，它们是'叠加态的移动'——让我可以同时走向多个方向。"\n\n这对便鞋的特殊效果是：穿戴者走过的地面会留下「量子足迹」（多个重叠的足迹，追踪者无法确定真实方向）。且可以用「量子步」——每日一次，同时存在于两个位置（可以选择其中一个位置作为真实位置，另一个消散，持续 1 回合）。薛定谔说："量子行者不需要路，他可以同时走所有路——只是需要正确的观测者来确定他走了哪条。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "quantum_ghost_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_quantum_ghost_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_quantum_ghost_feet" }
]
```

---

## 套装八十九：辐射行者（Radiation Walker）

> *"辐射不是毒药，它是'进化的催化剂'。学会承受它，你就能超越常人。"*

**套装主题**：辐射幸存者教团「原子之心」的装备。这些幸存者生活在高辐射环境中，他们的装备由铅板和辐射丝制成，赋予了穿戴者辐射抗性、辐射攻击和从辐射中恢复的能力。集齐四件时，穿戴者获得「辐射之躯」——可以释放辐射攻击、免疫辐射、从辐射中恢复。

**历史渊源**：原子之心的最高幸存者「辐射之心」居里夫人（另一位同名者），是一位据说可以从辐射中汲取力量的传奇科学家。她说："我不是在对抗辐射，我是在'与它共生'——辐射改变了我的身体，但也给了我力量。"

---

### 89.1 辐射行者头冠

```gdscript
item_id = "armor_radiation_walker_head"
display_name = "辐射行者头冠"
description = "一顶由铅板与辐射丝锻造的奇异头冠，头冠上镶嵌着一颗「辐射核心」——一颗能够储存和释放辐射能量的魔法宝石。头冠的表面不断有微弱的绿光在流动，那是辐射能量的可视化。头冠的正面有一个「辐射计数器」——显示当前的辐射水平。\n\n居里夫人在锻造这头冠时，将自己的「辐射耐受」封入了核心。她说："这头冠不只是头冠，它是'辐射的容器'——让我可以安全地储存和释放辐射。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 60 尺内所有辐射源的位置和强度（辐射雷达）。且可以通过头冠「吸收」辐射——受到 radiation damage 时，50% 概率将伤害转化为「辐射能量」储存（最多储存 20 点）。居里夫人说："辐射不是只能伤害，它也可以被利用——用辐射来攻击敌人，用辐射来治愈自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "radiation_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_radiation_walker_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_radiation_walker_head" }
]
```

**2件套（设计预留）**：免疫「radiation damage」和「radiation sickness」；`resistance_necrotic` +10。
**4件套（设计预留）**：每日三次「辐射射线」： bonus action，释放一道辐射射线（30 尺射程 3D10 necrotic，目标须通过 DC16 体质豁免，失败则 poisoned 1 回合）；每日一次「辐射爆发」：15 尺半径内所有生物受到 4D10 necrotic，须通过 DC16 体质豁免，失败则 poisoned 1 分钟；可以用「辐射治愈」：消耗 5 点辐射能量，恢复 2D10 HP（辐射刺激细胞再生）；10 尺内所有敌人每回合受到 1D6 necrotic（辐射光环）。

---

### 89.2 辐射行者板甲

```gdscript
item_id = "armor_radiation_walker_body"
display_name = "辐射行者板甲"
description = "一副由铅板与辐射丝锻造而成的奇异板甲，板甲表面覆盖着一层铅涂层——防止辐射泄漏。板甲的背部有一个小型「辐射反应炉」——不断有辐射能量在反应炉中流动，为穿戴者提供动力。板甲的接缝处有特殊的「辐射密封」——防止辐射泄漏，同时也防止外部辐射进入。\n\n居里夫人在锻造这副板甲时，将自己收集的所有放射性物质都编入了反应炉。她说："这副板甲不只是铠甲，它是'移动的核电站'——让我穿着一座核电站。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的辐射「释放」——每日一次，释放所有辐射形成一个辐射云（15 尺半径，困难地形，所有生物在区域内每回合受到 2D6 necrotic 并被 poisoned，持续 1 分钟）。且板甲会「吸收」辐射——在辐射环境中，每小时恢复 1 HP（辐射治愈）。居里夫人说："辐射不是只能伤害，它也可以用来治愈——只是需要正确的剂量和正确的方式。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "radiation_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_radiation_walker_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_radiation_walker_body" }
]
```

---

### 89.3 辐射行者护手

```gdscript
item_id = "armor_radiation_walker_hands"
display_name = "辐射行者护手"
description = "一对由铅板与辐射丝锻造而成的奇异护手，护手表面覆盖着一层铅涂层。握紧拳头时，辐射能量会通过管道汇聚在拳峰，形成辐射拳。松开拳头时，辐射能量会通过散热片释放。护手的掌心有「辐射喷嘴」——可以精确控制辐射的释放。\n\n居里夫人在锻造这对手套时，将自己的「辐射之手」能力封入了喷嘴。她说："这对手套不只是手套，它们是'辐射的工具'——让我可以用手操控辐射。"\n\n这对手套的特殊效果是：穿戴者可以用辐射拳进行「辐射打击」——徒手攻击造成 1D8 bludgeoning + 1D10 necrotic（辐射之力）。且可以用手套「释放」精确辐射——每日三次，从掌心释放一道辐射束（30 尺射程 2D10 necrotic，可以穿透实体障碍物如墙壁）。居里夫人说："辐射不是只能用来大范围攻击，它也可以用来精确打击——用辐射束穿透任何障碍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "radiation_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_radiation_walker_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_radiation_walker_hands" }
]
```

---

### 89.4 辐射行者胫甲

```gdscript
item_id = "armor_radiation_walker_feet"
display_name = "辐射行者胫甲"
description = "一对由铅板与辐射丝锻造而成的奇异胫甲，胫甲的底部有「辐射推进器」——可以在需要时提供额外的推进力。每一步踏下，推进器都会发出轻微的辐射声——不是弱点，是力量的象征。\n\n居里夫人在锻造这对胫甲时，将「辐射之步」能力封入了推进器。她说："这对胫甲不只是鞋子，它们是'辐射的推进器'——让我可以用辐射推动自己。"\n\n这对胫甲的特殊效果是：穿戴者可以进行「辐射冲锋」——每日一次，利用辐射推进器以三倍速度直线冲锋（路径上留下辐射轨迹，轨迹持续 1 分钟，任何踩到的生物受到 2D6 necrotic 并被 poisoned）。且可以在辐射环境中正常行走（免疫辐射地形）。居里夫人说："辐射行者不需要路，他需要辐射——在辐射中，辐射行者是最强大的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "radiation_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_radiation_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_radiation_walker_feet" }
]
```

---

## 套装九十：赛博行者（Cyber Walker）

> *"肉体不是限制，它是'平台'。在平台上安装正确的软件，你就能超越肉体。"*

**套装主题**：赛博改造者教团「数字之魂」的装备。这些改造者将机械与肉体融合，他们的装备由赛博组件和数据丝制成，赋予了穿戴者黑客能力、机械增强和数据操控的能力。集齐四件时，穿戴者获得「赛博之躯」——可以黑客入侵、用机械增强力量、在数字世界中穿梭。

**历史渊源**：数字之魂的最高改造者「赛博之心」图灵（另一位同名者），是一位据说可以将自己的意识上传至数字世界的传奇科学家。他说："我不是人类，我不是机器——我是'两者之间的存在'，一种新型的生命形式。"

---

### 90.1 赛博行者头冠

```gdscript
item_id = "armor_cyber_walker_head"
display_name = "赛博行者头冠"
description = "一顶由赛博组件与数据丝编织而成的奇异头冠，头冠上镶嵌着一颗「数据核心」——一颗能够储存和处理海量数据的魔法宝石。头冠的表面有一个「全息界面」——显示当前系统状态、网络连接和目标信息。头冠的两侧有「数据天线」——可以接收和发送数据信号。\n\n图灵在编织这头冠时，将自己的「数字意识」封入了数据核心。他说："这头冠不只是头冠，它是'数字世界的入口'——让我可以随时进入数字世界。"\n\n这头冠的特殊效果是：穿戴者可以「连接」任何电子设备——与 30 尺内的一个机械装置或构造体建立数据连接，可以读取它的数据或控制它的功能（如同 hack，目标须通过 DC16 智力豁免，失败则被控制 1 回合）。且可以通过头冠「下载」信息——自动知道 30 尺内所有电子设备的功能和漏洞。图灵说："数字世界不是另一个世界，它是'这个世界的一部分'——只是大多数人看不见。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "cyber_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_cyber_walker_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_cyber_walker_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；`investigation_bonus` +3（数字侦查）。
**4件套（设计预留）**：每日一次「黑客入侵」： bonus action，入侵 30 尺内的一个构造体或魔法装置（目标须通过 DC16 智力豁免，失败则被控制 1 分钟）；每日一次「数据护盾」： bonus action，创造一个数字护盾（免疫所有 directed 法术和远程攻击 1 回合，因为数据干扰了瞄准）；可以「数字步」—— bonus action，1 回合内免疫所有物理伤害（身体部分数字化）；可以「上传意识」——每日一次，将意识上传至数字世界（无法被物理攻击命中，但无法物理攻击，持续 1 回合）。

---

### 90.2 赛博行者链甲

```gdscript
item_id = "armor_cyber_walker_body"
display_name = "赛博行者链甲"
description = "一副由赛博组件与数据丝编织而成的奇异链甲，链甲的每一块甲片都是一个小型处理器——不断有数据在甲片之间流动。链甲的背部有一个小型「数据端口」——可以连接外部设备。链甲的接缝处有特殊的「数据密封」——防止数据泄漏，同时也防止外部黑客入侵。\n\n图灵在锻造这副链甲时，将自己设计的所有赛博系统都编入了甲片。他说："这副链甲不只是铠甲，它是'移动的服务器'——让我穿着一座服务器。"\n\n这副链甲的特殊效果是：穿戴者可以将链甲的数据「释放」——每日一次，释放所有数据形成一个数据风暴（15 尺半径，所有机械装置和构造体 stunned 1 回合，因为数据过载）。且链甲会「自动修复」——每小时恢复 1 HP（赛博系统的自我修复）。且可以将链甲的处理器「加速」—— bonus action，加速所有处理器（移动力 +10、攻击检定 +1，持续 1 分钟）。图灵说："数据不是只能用来计算，它也可以用来攻击——用数据风暴来瘫痪任何机械。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "cyber_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_cyber_walker_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_cyber_walker_body" }
]
```

---

### 90.3 赛博行者手套

```gdscript
item_id = "armor_cyber_walker_hands"
display_name = "赛博行者手套"
description = "一对由赛博组件与数据丝编织而成的奇异手套，手套的每一根手指都是一个小型数据线——可以连接任何电子设备。手套的掌心有「数据接口」——可以直接下载或上传数据。手套的指尖有「激光发射器」——可以用来切割或攻击。\n\n图灵在锻造这对手套时，将自己的「数字之手」能力封入了接口。他说："这对手套不只是手套，它们是'数字世界的双手'——让我可以用手操控数字世界。"\n\n这对手套的特殊效果是：穿戴者可以用手指「连接」任何电子设备—— bonus action，连接一个设备，读取或修改它的数据（如同 hack）。每日三次。且可以用手套「释放」激光——每日三次，从指尖释放一道激光（30 尺射程 2D10 force，可以切割金属障碍物）。图灵说："数字之手不是只能用来输入数据，它也可以用来切割——用激光切割任何障碍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "cyber_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_cyber_walker_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_cyber_walker_hands" }
]
```

---

### 90.4 赛博行者软靴

```gdscript
item_id = "armor_cyber_walker_feet"
display_name = "赛博行者软靴"
description = "一对由赛博组件与数据丝编织而成的奇异软靴，软靴的底部有「磁悬浮垫」——可以在金属表面上悬浮行走。软靴的鞋跟有「数据推进器」——可以在需要时提供额外的推进力。\n\n图灵在锻造这对软靴时，将「赛博之步」能力封入了推进器。他说："这对软靴不只是鞋子，它们是'数字世界的移动'——让我可以用赛博的方式移动。"\n\n这对软靴的特殊效果是：穿戴者可以在金属表面上「悬浮」行走（免疫 difficult terrain from metal）。且可以用「数据冲刺」——每日一次，利用数据推进器以三倍速度直线移动（移动力 ×3，可以穿过数字防火墙，免疫借机攻击，持续 1 回合）。图灵说："赛博行者不需要路，他需要数据——在数字世界中，赛博行者可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "cyber_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_cyber_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_cyber_walker_feet" }
]
```

---

*科幻 fantasy 混合主题套装 81–90 完结 · 共 40 件护甲装备（重甲 8 + 中甲 12 + 轻甲 20）*

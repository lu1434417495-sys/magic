# 传奇装备套装护甲设计文档（套装 31–40：轻甲）

> 10 套轻甲套装的护甲部分，共 40 件护甲装备，覆盖 head / body / hands / feet。

---

## 套装三十一：风语者（Wind Whisperer）护甲

> *"风从不说话，但它一直在低语——只是很少有人懂得倾听。"*

**套装主题**：远古风之灵崇拜者「风之语」结社的装备。这个结社的成员能够与风对话，借助风的力量飞翔和战斗。

**历史渊源**：风之语的创始人「第一聆听者」艾欧洛斯，是一位能够听懂所有风的语言的盲眼少年。他说："我听不见人类的声音，但我能听见风的歌声。这不是残疾，而是天赋。"

---

### 31.1 风语者风之冠（Wind Whisperer Wind Crown）

```gdscript
item_id = "armor_wind_whisperer_head"
display_name = "风语者风之冠"
description = "一顶由风之灵的羽毛与云丝编织而成的轻盈头冠，头冠上镶嵌着三颗微型风之水晶。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「听见」风的声音。\n\n艾欧洛斯在编织这头冠时，用了一整年的风。他说："风不是声音，它是'低语'——树木的低语，海洋的低语，远方的低语。"他将风凝结成丝线，与风之灵的羽毛编织在一起。\n\n这头冠的特殊效果是：在强风中，穿戴者的感知范围翻倍，且可以「听见」300 尺内的任何移动（如同 tremorsense，但只对风能吹到的区域有效）。艾欧洛斯说："风吹到的地方，就是我的领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "wind_whisperer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wind_whisperer_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wind_whisperer_head" }
]
```

**特殊效果（设计预留）**：在强风天气中，感知检定+3。

---

### 31.2 风语者风之袍（Wind Whisperer Wind Robe）

```gdscript
item_id = "armor_wind_whisperer_body"
display_name = "风语者风之袍"
description = "一件由风之灵的核心与云丝编织而成的奇异长袍，袍身不断有微型旋风在流动——不是装饰，而是真正的风被封印在了丝线中。长袍非常轻盈——因为风之灵几乎没有重量，但也因为这轻盈，穿戴者在奔跑时会微微飘起。\n\n这件长袍是艾欧洛斯在走遍了所有风暴之地后，用收集到的所有风之灵的核心编织的。他说："每一种风都有它的故事——海风带来远方的新闻，山风带来高处的消息，沙漠之风带来古老的秘密。这件长袍承载着所有风的故事。"\n\n长袍会在佩戴者移动时自动激活——移动力 +5（风的助力）。且长袍可以让佩戴者在任何高度坠落时免疫 falling damage（风托住身体）。一位后来的风之语者在描述这件长袍时说："穿着它，我感觉自己不是在走路，我是在'飘'——风推着我，云托着我，世界在我面前展开。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "wind_whisperer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wind_whisperer_body" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_wind_whisperer_body" }
]
```

**特殊效果（设计预留）**：免疫 falling damage，且在任何地形上移动力不受减速影响。

---

### 31.3 风语者风之手套（Wind Whisperer Wind Gloves）

```gdscript
item_id = "armor_wind_whisperer_hands"
display_name = "风语者风之手套"
description = "一对由风之灵的触须与云丝编织而成的奇异手套，手套表面不断有微弱的旋风在指尖旋转。握拳时，旋风会聚集在拳头上，形成一圈风刃；张开手掌时，旋风会散去，化作一阵温柔的微风。\n\n这对手套是艾欧洛斯在最后一次风暴中，从最后一个风之灵那里得到的礼物。那只风之灵说："你要走了，但你的旅程永远不会结束。让我的一部分陪伴你。"它将触须缠绕在艾欧洛斯的手上，化作了一对手套。\n\n手套会在佩戴者进行远程攻击时自动激活——每日三次，可以释放一道风刃（30 尺射程 1D8 slashing）。且在进行攀爬或跳跃检定时，检定 +3（风的助力）。艾欧洛斯说："风不是阻力，它是助力——学会顺应风，你就能到达任何想去的地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "wind_whisperer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wind_whisperer_hands" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wind_whisperer_hands" }
]
```

**特殊效果（设计预留）**：远程攻击射程+15尺，且不受风力影响。

---

### 31.4 风语者风之靴（Wind Whisperer Wind Boots）

```gdscript
item_id = "armor_wind_whisperer_feet"
display_name = "风语者风之靴"
description = "一对由风之灵的核心碎片与云丝编织而成的奇异软靴，靴底刻有微型风向玫瑰——每一个方向都对应着一条艾欧洛斯曾经走过的道路。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是艾欧洛斯在临终前，将自己的双脚化成的两阵风的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一条路的感觉。让后来的风语者穿着它们，继续走下去。"他将双脚的风之碎片提取出来，与云丝结合，制作成了这对风之靴。\n\n软靴的特殊效果是：佩戴者永远不会感到疲劳——可以进行双倍时间的强行军而不受 exhaustion 影响（永不停歇）。且每日一次，可以通过软靴「顺风而行」——移动力翻倍，持续 1 分钟。一位后来的风语者在穿着这对软靴走了一年后说："它们不会告诉你去哪里，但它们会让你永远能够到达——无论你选择哪条路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "wind_whisperer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wind_whisperer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_wind_whisperer_feet" }
]
```

**特殊效果（设计预留）**：免疫因长途旅行产生的exhaustion。

---

## 套装三十二：水波行者（Wave Walker）护甲

> *"海洋不是敌人，它只是另一种形态的大地——学会了游泳，你就学会了飞翔。"*

**套装主题**：远古水之灵崇拜者「水之波」结社的装备。这个结社的成员能够在水中呼吸，借助水的力量潜行和战斗。

**历史渊源**：水之波的创始人「第一潜行者」涅柔斯，是一位能够在水下生活整整一年的渔夫。他说："我不是在水下呼吸，我是在'喝水'——水进入我的身体，成为我的一部分。"

---

### 32.1 水波行者水之冠（Wave Walker Water Crown）

```gdscript
item_id = "armor_wave_walker_head"
display_name = "水波行者水之冠"
description = "一顶由深海珍珠与水之丝编织而成的奇异头冠，头冠上镶嵌着三颗微型水之水晶。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」水下的世界。\n\n涅柔斯在编织这头冠时，用了一整年的海水。他说："海水不是液体，它是'眼睛'——它能看到水下的一切，从最小的浮游生物到最大的鲸鱼。"他将海水凝结成丝线，与深海珍珠编织在一起。\n\n这头冠的特殊效果是：在水下，穿戴者的感知范围翻倍，且可以「看见」300 尺内的任何移动（如同 blindsight，但只在水下有效）。涅柔斯说："水覆盖的地方，就是我的领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "wave_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wave_walker_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wave_walker_head" }
]
```

**特殊效果（设计预留）**：在水下，感知检定+3。

---

### 32.2 水波行者水之甲（Wave Walker Water Mail）

```gdscript
item_id = "armor_wave_walker_body"
display_name = "水波行者水之甲"
description = "一件由深海鳞片与水之丝编织而成的奇异鳞甲，甲身不断有微型水流在流动——不是装饰，而是真正的水被封印在了丝线中。鳞甲非常轻盈——在水中几乎没有重量，但在空气中会变得沉重。\n\n这件鳞甲是涅柔斯在走遍了所有海洋后，用收集到的所有深海生物的鳞片编织的。他说："每一片鳞片都是一个生命的记忆，一件由生命记忆编织的铠甲，比任何金属都坚固。"他在鳞甲内侧缝了七个小口袋，每个口袋中都藏着一种不同的海洋生物的鳞片——从能够发光的深海鱼到能够发电的电鳗。\n\n鳞甲会在佩戴者进入水中时自动激活——在水中 AC +2（水的护盾）。且鳞甲可以让佩戴者在任何深度呼吸——不需要空气，水本身就是氧气（如同 water breathing）。一位后来的水波行者在描述这件鳞甲时说："穿着它，我不是在穿衣服，我是在'穿着'整个海洋——每一片鳞片都是一位海洋生物的灵魂，它们在保护我，指引我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "wave_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_wave_walker_body" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wave_walker_body" }
]
```

**特殊效果（设计预留）**：在水下 AC +2，且可以在任何深度呼吸。

---

### 32.3 水波行者水之手套（Wave Walker Water Gloves）

```gdscript
item_id = "armor_wave_walker_hands"
display_name = "水波行者水之手套"
description = "一对由深海生物的鳍与水之丝编织而成的奇异手套，手套表面不断有微型水流在指尖旋转。握拳时，水流会聚集在拳头上，形成一圈水刃；张开手掌时，水流会散去，化作一阵温柔的细雨。\n\n这对手套是涅柔斯在最后一次潜水中，从一只深海巨兽那里得到的礼物。那只巨兽说："你要走了，但你的旅程永远不会结束。让我的一部分陪伴你。"它将鳍缠绕在涅柔斯的手上，化作了一对手套。\n\n手套会在佩戴者进行近战攻击时自动激活——每日三次，可以释放一道水刃（5 尺射程 1D8 bludgeoning）。且在进行游泳或潜水检定时，检定 +3（水的助力）。涅柔斯说："水不是阻力，它是助力——学会顺应水，你就能到达任何想去的地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "wave_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wave_walker_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_wave_walker_hands" }
]
```

**特殊效果（设计预留）**：在水中，攻击检定+2。

---

### 32.4 水波行者水之靴（Wave Walker Water Boots）

```gdscript
item_id = "armor_wave_walker_feet"
display_name = "水波行者水之靴"
description = "一对由深海生物的蹼与水之丝编织而成的奇异软靴，靴底刻有微型水流纹路——每一个纹路都对应着一条涅柔斯曾经游过的洋流。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。在水中，靴底会变成蹼状，提供强大的推进力。\n\n这对软靴是涅柔斯在临终前，将自己的双脚化成的两股水流的一部分制作的。他说："我的脚已经游不动了，但它们还记得每一条洋流的感觉。让后来的水波行者穿着它们，继续游下去。"他将双脚的水之碎片提取出来，与水之丝结合，制作成了这对水之靴。\n\n软靴的特殊效果是：在水中，佩戴者的移动力翻倍（水的助力）。且每日一次，可以通过软靴「水流冲刺」——在水中移动力翻倍，持续 1 分钟。一位后来的水波行者在穿着这对软靴游了一年后说："它们不会告诉你去哪里，但它们会让你永远能够到达——无论你选择哪条洋流。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "wave_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_wave_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_wave_walker_feet" }
]
```

**特殊效果（设计预留）**：在水中移动力+10，且可以行走于水面。

---

## 套装三十三：地震先知（Earthquake Seer）护甲

> *"大地不会说话，但它会震动——每一次震动都是一条信息，只是很少有人懂得解读。"*

**套装主题**：远古大地崇拜者「地震之眼」结社的装备。这个结社的成员能够感知地震，借助大地的力量预知未来和战斗。

**历史渊源**：地震之眼的创始人「第一感知者」盖亚，是一位能够感知所有地震的盲眼少女。她说："我听不见人类的声音，但我能听见大地的震动。这不是残疾，而是天赋。"

---

### 33.1 地震先知感知头盔（Earthquake Seer Sense Helm）

```gdscript
item_id = "armor_earthquake_seer_head"
display_name = "地震先知感知头盔"
description = "一顶由感知石与矿物丝编织而成的沉重头盔，头盔上镶嵌着三颗微型地震水晶。头盔没有面甲，因为它不是为了防御——它是为了让穿戴者能够「听见」大地的震动。\n\n盖亚在编织这头盔时，用了一整年的地震能量。她说："地震不是灾难，它是'信息'——地壳的变动，岩浆的流动，矿脉的生长。每一次地震都在讲述一个故事。"她将地震能量凝结成丝线，与感知石编织在一起。\n\n这头盔的特殊效果是：在地震多发区域，穿戴者的感知范围翻倍，且可以「听见」1 里内的任何地震预兆（如同 tremorsense，但只对地面震动有效）。盖亚说："大地震动的地方，就是我的领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "earthquake_seer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earthquake_seer_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earthquake_seer_head" }
]
```

**特殊效果（设计预留）**：可以感知 1 里内的地震预兆和地下结构。

---

### 33.2 地震先知岩石甲（Earthquake Seer Rock Mail）

```gdscript
item_id = "armor_earthquake_seer_body"
display_name = "地震先知岩石甲"
description = "一件由岩石碎片与矿物丝编织而成的沉重链甲，甲身不断有微型岩石在流动——不是装饰，而是真正的岩石被封印在了丝线中。链甲非常沉重——比普通链甲重三倍，因为其中蕴含了大地之力。\n\n这件链甲是盖亚在走遍了所有地震带后，用收集到的所有岩石碎片编织的。她说："每一块岩石都是一个时代的记忆，一件由时代记忆编织的铠甲，比任何金属都坚固。"她在链甲内侧缝了七个小口袋，每个口袋中都藏着一种不同的岩石——从能够导电的矿石到能够发热的火山岩。\n\n链甲会在佩戴者站立不动时自动激活——站立不动时 AC +2（大地的稳定）。且链甲可以让佩戴者免疫被推离或击倒（大地的根基）。一位后来的地震先知在描述这件链甲时说："穿着它，我不是在穿衣服，我是在'穿着'整个大地——每一块岩石都是一位大地之灵的碎片，它们在保护我，稳定我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "earthquake_seer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_earthquake_seer_body" },
    { attribute_id = "saving_throw_strength", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earthquake_seer_body" }
]
```

**特殊效果（设计预留）**：站立不动时 AC +2，免疫被推离或击倒。

---

### 33.3 地震先知震击手套（Earthquake Seer Quake Gauntlets）

```gdscript
item_id = "armor_earthquake_seer_hands"
display_name = "地震先知震击手套"
description = "一对由震击石与矿物丝编织而成的沉重手套，手套表面不断有微弱的震动波纹——那不是物理现象，而是地震能量在"渴望"释放。手套的掌心各嵌有一枚微型地震水晶——每一枚水晶都可以释放出一道震击波。\n\n这对手套是盖亚在最后一次地震中，从地震的核心取出的震击石制作的。她说："地震的力量不是破坏，它是'改变'——改变地形，改变结构，改变一切。这对手套让我能够借用地震的力量。"她将震击石与矿物丝结合，制作成了这对手套。\n\n手套会在佩戴者进行近战攻击时自动激活——每日三次，可以释放一道震击波（5 尺射程 1D10 bludgeoning，目标须通过 DC14 敏捷豁免，失败则 prone）。且在进行力量检定时，检定 +3（大地的力量）。盖亚说："大地不是敌人，它是另一种力量——学会与它互动，你就能获得超越常规的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "earthquake_seer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earthquake_seer_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earthquake_seer_hands" }
]
```

**特殊效果（设计预留）**：近战攻击附加1D6 bludgeoning，且可以击倒目标。

---

### 33.4 地震先知稳固靴（Earthquake Seer Stable Boots）

```gdscript
item_id = "armor_earthquake_seer_feet"
display_name = "地震先知稳固靴"
description = "一对由震击石与矿物丝编织而成的沉重重靴，靴底刻有微型地震纹路——每一个纹路都对应着一条盖亚曾经感知过的地震带。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。在地震中，靴底会生根，提供完美的稳定性。\n\n这对重靴是盖亚在临终前，将自己的双脚化成的大地碎片的一部分制作的。她说："我的脚已经走不动了，但它们还记得每一场地震的感觉。让后来的地震先知穿着它们，继续感知下去。"她将双脚的大地碎片提取出来，与矿物丝结合，制作成了这对稳固靴。\n\n重靴的特殊效果是：在地震中，佩戴者免疫 prone（大地的根基）。且每日一次，可以通过重靴「地震步」——踩踏地面，释放一道微型地震（5 尺半径，所有生物须通过 DC14 敏捷豁免，失败则 prone 并受到 2D6 bludgeoning）。一位后来的地震先知在穿着这对重靴走了一年后说："它们不会告诉你地震什么时候来，但它们会让你在地震中屹立不倒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "earthquake_seer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earthquake_seer_feet" },
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earthquake_seer_feet" }
]
```

**特殊效果（设计预留）**：免疫地震导致的 prone，且在困难地形上不移动力减半。

---

## 套装三十四：夜幕吟游诗人（Night Bard）护甲

> *"黑夜不是沉默的，它在歌唱——只是凡人听不见那频率。"*

**套装主题**：远古夜之灵崇拜者「夜之歌」结社的装备。这个结社的成员能够在黑夜中歌唱，借助夜的力量魅惑和战斗。

**历史渊源**：夜之歌的创始人「第一歌者」俄耳甫斯，是一位能够在黑夜中唱出最美旋律的盲眼诗人。他说："我听不见白天的喧嚣，但我能听见黑夜的旋律。这不是残疾，而是天赋。"

---

### 34.1 夜幕吟游诗人夜之冠（Night Bard Night Crown）

```gdscript
item_id = "armor_night_bard_head"
display_name = "夜幕吟游诗人夜之冠"
description = "一顶由夜莺羽毛与月光丝编织而成的精致头冠，头冠上镶嵌着三颗微型夜之水晶。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「听见」黑夜的声音。\n\n俄耳甫斯在编织这头冠时，用了一整年的黑夜。他说："黑夜不是黑暗，它是'旋律'——星星的旋律，月亮的旋律，远方的旋律。"他将黑夜凝结成丝线，与夜莺羽毛编织在一起。\n\n这头冠的特殊效果是：在黑夜中，穿戴者的魅力检定 +3，且可以「听见」300 尺内的任何声音（如同 blindsight，但只对声音有效）。俄耳甫斯说："黑夜覆盖的地方，就是我的舞台。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "night_bard_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_night_bard_head" },
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_night_bard_head" }
]
```

**特殊效果（设计预留）**：在黑夜中，魅力检定+3。

---

### 34.2 夜幕吟游诗人夜之袍（Night Bard Night Robe）

```gdscript
item_id = "armor_night_bard_body"
display_name = "夜幕吟游诗人夜之袍"
description = "一件由夜之灵的核心与月光丝编织而成的华丽长袍，袍身不断有微型音符在流动——不是装饰，而是真正的音乐被封印在了丝线中。长袍非常轻薄——薄到几乎不存在，但这正是优势，因为吟游诗人的长袍不是为了防御，而是为了表演。\n\n这件长袍是俄耳甫斯在走遍了所有黑夜之地后，用收集到的所有夜之灵的核心编织的。他说："每一种黑夜都有它的旋律——森林的黑夜是低沉的，城市的黑夜是嘈杂的，沙漠的黑夜是寂静的。这件长袍承载着所有黑夜的旋律。"\n\n长袍会在佩戴者表演时自动激活——表演检定 +3（黑夜的共鸣）。且长袍可以让佩戴者在黑夜中完全隐形——不是魔法隐身，而是与黑夜完美融合（如同 pass without trace 的黑夜版）。一位后来的夜之歌者在描述这件长袍时说："穿着它，我不是在穿衣服，我是在'穿着'整个黑夜——每一个音符都是一位黑夜之灵的碎片，它们在保护我，指引我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "night_bard_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_night_bard_body" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_night_bard_body" }
]
```

**特殊效果（设计预留）**：在黑夜中，可以完美融入环境（隐匿检定+3）。

---

### 34.3 夜幕吟游诗人音波手套（Night Bard Sonic Gloves）

```gdscript
item_id = "armor_night_bard_hands"
display_name = "夜幕吟游诗人音波手套"
description = "一对由夜莺喉骨与月光丝编织而成的奇异手套，手套表面不断有微型音符在指尖旋转。握拳时，音符会聚集在拳头上，形成一圈音波；张开手掌时，音符会散去，化作一阵温柔的旋律。\n\n这对手套是俄耳甫斯在最后一次黑夜中，从最后一只夜莺那里得到的礼物。那只夜莺说："你要走了，但你的歌声永远不会结束。让我的一部分陪伴你。"它将喉骨缠绕在俄耳甫斯的手上，化作了一对手套。\n\n手套会在佩戴者进行表演时自动激活——每日三次，可以释放一道音波（15 尺锥形 2D8 thunder，目标须通过 DC14 体质豁免，失败则 deafened 1 回合）。且在进行欺瞒或说服检定时，检定 +3（夜莺的魅惑）。俄耳甫斯说："黑夜不是沉默的，它在歌唱——学会与它合唱，你就能魅惑任何听众。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "night_bard_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_night_bard_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_night_bard_hands" }
]
```

**特殊效果（设计预留）**：可以释放音波攻击，且表演检定+3。

---

### 34.4 夜幕吟游诗人静步靴（Night Bard Silent Boots）

```gdscript
item_id = "armor_night_bard_feet"
display_name = "夜幕吟游诗人静步靴"
description = "一对由夜莺足骨与月光丝编织而成的奇异软靴，靴底刻有微型音符纹路——每一个纹路都对应着一首俄耳甫斯曾经唱过的歌曲。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是俄耳甫斯在临终前，将自己的双脚化成的两段旋律的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一首歌曲的感觉。让后来的夜之歌者穿着它们，继续唱下去。"他将双脚的旋律碎片提取出来，与月光丝结合，制作成了这对静步靴。\n\n软靴的特殊效果是：佩戴者在行走时不会发出任何声音——如同 pass without trace（静默之歌）。且每日一次，可以通过软靴「旋律步」——移动力 +10，且在移动过程中可以「歌唱」（持续释放音波，5 尺内所有敌人须通过 DC14 智慧豁免，失败则 charmed 1 回合）。一位后来的夜之歌者在穿着这对软靴走了一年后说："它们不会告诉你唱什么，但它们会让你永远能够安静地到达——无论你选择哪个舞台。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "night_bard_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_night_bard_feet" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_night_bard_feet" }
]
```

**特殊效果（设计预留）**：移动不发出声音，且免疫追踪。

---

## 套装三十五：白鸦信使（White Raven）护甲

> *"白鸦不是死亡的使者，它是消息的传递者——只是有些消息，人们不愿意听见。"*

**套装主题**：远古信使结社「白鸦之翼」的装备。这个结社的成员能够在任何环境中传递消息，借助白鸦的力量飞翔和隐匿。

**历史渊源**：白鸦之翼的创始人「第一信使」墨丘利，是一位能够与所有鸟类对话的聋哑少年。他说："我听不见人类的声音，但我能听见鸟儿的歌声。这不是残疾，而是天赋。"

---

### 35.1 白鸦信使白鸦冠（White Raven Raven Crown）

```gdscript
item_id = "armor_white_raven_head"
display_name = "白鸦信使白鸦冠"
description = "一顶由白鸦羽毛与变色丝编织而成的奇异头冠，头冠上镶嵌着三颗微型白鸦之眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」远方的消息。\n\n墨丘利在编织这头冠时，收集了九十九只白鸦的羽毛。他说："白鸦不是死亡的使者，它是'消息的传递者'——它能看到远方，听到远方，知道远方发生的一切。"他将白鸦羽毛与变色丝编织在一起。\n\n这头冠的特殊效果是：在任何环境中，头冠会自动调整颜色以匹配环境（+2 AC 来自 camouflage）。且可以「看见」1 里内的任何烟雾信号或反光信号（信使之眼）。墨丘利说："白鸦飞到的地方，就是我的消息网络。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "white_raven_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_white_raven_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_white_raven_head" }
]
```

**特殊效果（设计预留）**：可以感知 1 里内的信号和消息传递活动。

---

### 35.2 白鸦信使变色甲（White Raven Chameleon Mail）

```gdscript
item_id = "armor_white_raven_body"
display_name = "白鸦信使变色甲"
description = "一件由白鸦羽毛与变色丝编织而成的奇异鳞甲，甲身不断有微型羽毛在流动——不是装饰，而是真正的白鸦羽毛被封印在了丝线中。鳞甲的颜色会随环境自动变化——在雪地中是白色，在森林中是绿色，在黑夜中是黑色。\n\n这件鳞甲是墨丘利在走遍了所有环境后，用收集到的所有白鸦羽毛编织的。他说："每一片羽毛都是一个消息的载体，一件由消息载体编织的铠甲，比任何金属都隐蔽。"他在鳞甲内侧缝了七个小口袋，每个口袋中都藏着一种不同颜色的羽毛——从能够反射阳光的白色羽毛到能够吸收光线的黑色羽毛。\n\n鳞甲会在佩戴者进入任何环境时自动激活——颜色自动匹配环境（+3 AC 来自 camouflage，且隐匿检定 +3）。且鳞甲可以让佩戴者在任何地形上行走时不受减速影响（白鸦的轻盈）。一位后来的白鸦信使在描述这件鳞甲时说："穿着它，我不是在穿衣服，我是在'穿着'整个自然——每一片羽毛都是一位白鸦的灵魂，它们在保护我，隐藏我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "white_raven_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_white_raven_body" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_white_raven_body" }
]
```

**特殊效果（设计预留）**：在任何环境中自动 camouflage（+3 AC），且隐匿检定+3。

---

### 35.3 白鸦信使信使之手套（White Raven Messenger Gloves）

```gdscript
item_id = "armor_white_raven_hands"
display_name = "白鸦信使信使之手套"
description = "一对由白鸦爪骨与变色丝编织而成的奇异手套，手套表面不断有微型羽毛在指尖旋转。握拳时，羽毛会聚集在拳头上，形成一圈利爪；张开手掌时，羽毛会散去，化作一阵温柔的微风。\n\n这对手套是墨丘利在最后一次飞行中，从最后一只白鸦那里得到的礼物。那只白鸦说："你要走了，但你的消息永远不会停止。让我的一部分陪伴你。"它将爪骨缠绕在墨丘利的手上，化作了一对手套。\n\n手套会在佩戴者需要传递消息时自动激活——每日三次，可以释放一道羽毛（30 尺射程，羽毛可以传递一条简短信息给指定目标）。且在进行巧手或隐匿检定时，检定 +3（白鸦的敏捷）。墨丘利说："白鸦不是死亡的使者，它是希望的使者——学会与它同行，你就能将消息带到任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "white_raven_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_white_raven_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_white_raven_hands" }
]
```

**特殊效果（设计预留）**：可以传递消息（如同 message spell），且巧手检定+3。

---

### 35.4 白鸦信使飞翔靴（White Raven Flying Boots）

```gdscript
item_id = "armor_white_raven_feet"
display_name = "白鸦信使飞翔靴"
description = "一对由白鸦足骨与变色丝编织而成的奇异软靴，靴底刻有微型风向纹路——每一个纹路都对应着一条墨丘利曾经飞过的航线。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是墨丘利在临终前，将自己的双脚化成的两对白鸦之足的一部分制作的。他说："我的脚已经飞不动了，但它们还记得每一条航线的感觉。让后来的白鸦信使穿着它们，继续飞下去。"他将双脚的白鸦碎片提取出来，与变色丝结合，制作成了这对飞翔靴。\n\n软靴的特殊效果是：佩戴者在跳跃时高度翻倍（白鸦的轻盈）。且每日一次，可以通过软靴「白鸦之翼」——获得 30 尺飞行速度，持续 1 分钟。一位后来的白鸦信使在穿着这对软靴飞了一年后说："它们不会告诉你飞到哪里，但它们会让你永远能够到达——无论你选择哪条航线。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "white_raven_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_white_raven_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_white_raven_feet" }
]
```

**特殊效果（设计预留）**：跳跃高度翻倍，且每日一次获得 30 尺飞行速度。

---

## 套装三十六：红狐盗贼（Red Fox Thief）护甲

> *"红狐不是狡猾，它只是懂得在正确的时间出现在正确的地方——然后带着正确的东西离开。"*

**套装主题**：远古盗贼行会「红狐之尾」的装备。这个行会的成员能够在任何环境中偷窃，借助红狐的力量隐匿和逃跑。

**历史渊源**：红狐之尾的创始人「第一盗贼」雷纳德，是一位能够与所有狐狸对话的聋哑少年。他说："我听不见人类的声音，但我能听见狐狸的低语。这不是残疾，而是天赋。"

---

### 36.1 红狐盗贼狐之冠（Red Fox Thief Fox Crown）

```gdscript
item_id = "armor_red_fox_head"
display_name = "红狐盗贼狐之冠"
description = "一顶由红狐毛皮与狡诈丝编织而成的奇异头冠，头冠上镶嵌着两颗微型红狐之眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」别人的盲点。\n\n雷纳德在编织这头冠时，收集了九十九只红狐的尾毛。他说："红狐不是狡猾，它只是懂得'时机'——什么时候出现，什么时候消失，什么时候带走什么。"他将红狐尾毛与狡诈丝编织在一起。\n\n这头冠的特殊效果是：在任何环境中，头冠会自动调整颜色以匹配环境（+2 AC 来自 camouflage）。且可以「看见」30 尺内任何生物的盲点（攻击检定 +2 当目标未察觉）。雷纳德说："红狐跑到的地方，就是我的狩猎场。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "red_fox_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_red_fox_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_red_fox_head" }
]
```

**特殊效果（设计预留）**：可以发现 30 尺内任何生物的盲点。

---

### 36.2 红狐盗贼变色甲（Red Fox Chameleon Mail）

```gdscript
item_id = "armor_red_fox_body"
display_name = "红狐盗贼变色甲"
description = "一件由红狐毛皮与狡诈丝编织而成的奇异鳞甲，甲身不断有微型毛发在流动——不是装饰，而是真正的红狐毛被封印在了丝线中。鳞甲的颜色会随环境自动变化——在雪地中是白色，在森林中是绿色，在黑夜中是黑色。\n\n这件鳞甲是雷纳德在走遍了所有环境后，用收集到的所有红狐毛编织的。他说："每一根毛发都是一个隐藏的秘诀，一件由隐藏秘诀编织的铠甲，比任何金属都隐蔽。"他在鳞甲内侧缝了七个小口袋，每个口袋中都藏着一种不同颜色的毛发——从能够反射阳光的白色毛发到能够吸收光线的黑色毛发。\n\n鳞甲会在佩戴者进入任何环境时自动激活——颜色自动匹配环境（+3 AC 来自 camouflage，且隐匿检定 +3）。且鳞甲可以让佩戴者在任何地形上行走时不受减速影响（红狐的敏捷）。一位后来的红狐盗贼在描述这件鳞甲时说："穿着它，我不是在穿衣服，我是在'穿着'整个自然——每一根毛发都是一位红狐的灵魂，它们在保护我，隐藏我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "red_fox_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_red_fox_body" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_red_fox_body" }
]
```

**特殊效果（设计预留）**：在任何环境中自动 camouflage（+3 AC），且隐匿检定+3。

---

### 36.3 红狐盗贼窃贼手套（Red Fox Thief Thief Gloves）

```gdscript
item_id = "armor_red_fox_hands"
display_name = "红狐盗贼窃贼手套"
description = "一对由红狐爪骨与狡诈丝编织而成的奇异手套，手套表面不断有微型毛发在指尖旋转。握拳时，毛发会聚集在拳头上，形成一圈利爪；张开手掌时，毛发会散去，化作一阵温柔的微风。\n\n这对手套是雷纳德在最后一次盗窃中，从最后一只红狐那里得到的礼物。那只红狐说："你要走了，但你的盗窃永远不会停止。让我的一部分陪伴你。"它将爪骨缠绕在雷纳德的手上，化作了一对手套。\n\n手套会在佩戴者进行盗窃时自动激活——每日三次，可以进行一次完美的盗窃（自动成功，不被发现）。且在进行巧手或隐匿检定时，检定 +3（红狐的敏捷）。雷纳德说："红狐不是狡猾，它只是懂得'时机'——学会与它同行，你就能带走任何东西。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "red_fox_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_red_fox_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_red_fox_hands" }
]
```

**特殊效果（设计预留）**：每日三次完美盗窃（自动成功），且巧手检定+3。

---

### 36.4 红狐盗贼静音靴（Red Fox Thief Silent Boots）

```gdscript
item_id = "armor_red_fox_feet"
display_name = "红狐盗贼静音靴"
description = "一对由红狐足骨与狡诈丝编织而成的奇异软靴，靴底刻有微型爪纹——每一个纹路都对应着一条雷纳德曾经走过的路线。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是雷纳德在临终前，将自己的双脚化成的两对红狐之足的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一条路线的感觉。让后来的红狐盗贼穿着它们，继续走下去。"他将双脚的红狐碎片提取出来，与狡诈丝结合，制作成了这对静音靴。\n\n软靴的特殊效果是：佩戴者在行走时不会发出任何声音——如同 pass without trace（静默之步）。且每日一次，可以通过软靴「红狐之跃」——移动力 +15，且在移动过程中可以「偷窃」（bonus action 进行一次盗窃）。一位后来的红狐盗贼在穿着这对软靴走了一年后说："它们不会告诉你偷什么，但它们会让你永远能够安静地到达——无论你选择哪个目标。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "red_fox_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_red_fox_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_red_fox_feet" }
]
```

**特殊效果（设计预留）**：移动不发出声音，且免疫追踪。

---

## 套装三十七：青蛇刺客（Green Viper）护甲

> *"青蛇不是毒物，它只是懂得在正确的时间释放正确的毒素——然后安静地离开。"*

**套装主题**：远古刺客行会「青蛇之牙」的装备。这个行会的成员能够在任何环境中暗杀，借助青蛇的力量隐匿和致命。

**历史渊源**：青蛇之牙的创始人「第一刺客」美杜莎（另一位同名者），是一位能够与所有蛇类对话的聋哑少女。她说："我听不见人类的声音，但我能听见蛇类的嘶嘶声。这不是残疾，而是天赋。"

---

### 37.1 青蛇刺客蛇之冠（Green Viper Snake Crown）

```gdscript
item_id = "armor_green_viper_head"
display_name = "青蛇刺客蛇之冠"
description = "一顶由青蛇蜕皮与毒丝编织而成的奇异头冠，头冠上镶嵌着两颗微型蛇眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」猎物的弱点。\n\n美杜莎在编织这头冠时，收集了一千条青蛇的蜕皮。她说："青蛇不是毒物，它只是懂得'时机'——什么时候攻击，什么时候撤退，什么时候释放毒素。"她将青蛇蜕皮与毒丝编织在一起。\n\n这头冠的特殊效果是：在任何环境中，头冠会自动调整颜色以匹配环境（+2 AC 来自 camouflage）。且可以「看见」30 尺内任何生物的血管和弱点（攻击检定 +2 当目标未察觉，且暴击范围+1）。美杜莎说："青蛇爬到的地方，就是我的猎场。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "green_viper_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_green_viper_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_green_viper_head" }
]
```

**特殊效果（设计预留）**：可以发现 30 尺内任何生物的弱点（暴击范围+1）。

---

### 37.2 青蛇刺客蛇鳞甲（Green Viper Scale Mail）

```gdscript
item_id = "armor_green_viper_body"
display_name = "青蛇刺客蛇鳞甲"
description = "一件由青蛇鳞片与毒丝编织而成的奇异鳞甲，甲身不断有微型鳞片在流动——不是装饰，而是真正的青蛇鳞片被封印在了丝线中。鳞甲的颜色会随环境自动变化——在草地中是绿色，在黑夜中是黑色，在水中是蓝色。\n\n这件鳞甲是美杜莎在走遍了所有环境后，用收集到的所有青蛇鳞片编织的。她说："每一片鳞片都是一个毒液的容器，一件由毒液容器编织的铠甲，比任何金属都致命。"她在鳞甲内侧缝了七个小口袋，每个口袋中都藏着一种不同的毒液——从能够麻痹的神经毒到能够溶血的血液毒。\n\n鳞甲会在佩戴者进入任何环境时自动激活——颜色自动匹配环境（+3 AC 来自 camouflage，且隐匿检定 +3）。且任何近战攻击鳞甲的生物会受到 1D4 poison damage（蛇鳞的反击）。一位后来的青蛇刺客在描述这件鳞甲时说："穿着它，我不是在穿衣服，我是在'穿着'整个蛇群——每一片鳞片都是一条青蛇的灵魂，它们在保护我，毒杀敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "green_viper_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "armor_green_viper_body" },
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_green_viper_body" }
]
```

**特殊效果（设计预留）**：近战攻击者受到 1D4 poison damage，且免疫所有毒素。

---

### 37.3 青蛇刺客毒爪手套（Green Viper Venom Claw Gloves）

```gdscript
item_id = "armor_green_viper_hands"
display_name = "青蛇刺客毒爪手套"
description = "一对由青蛇毒牙与毒丝编织而成的奇异手套，手套表面不断有微型毒液在指尖旋转。握拳时，毒液会聚集在拳头上，形成一圈毒爪；张开手掌时，毒液会散去，化作一阵温柔的香气。\n\n这对手套是美杜莎在最后一次暗杀中，从最后一条青蛇那里得到的礼物。那条青蛇说："你要走了，但你的暗杀永远不会停止。让我的一部分陪伴你。"它将毒牙缠绕在美杜莎的手上，化作了一对手套。\n\n手套会在佩戴者进行暗杀时自动激活——每日三次，可以释放一道毒液（5 尺射程 2D6 poison，目标须通过 DC14 体质豁免，失败则 poisoned 1 回合）。且在进行巧手或隐匿检定时，检定 +3（青蛇的敏捷）。美杜莎说："青蛇不是毒物，它只是懂得'时机'——学会与它同行，你就能毒杀任何目标。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "green_viper_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_green_viper_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_green_viper_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击附加 1D6 poison damage，且可以释放毒液。

---

### 37.4 青蛇刺客静音靴（Green Viper Silent Boots）

```gdscript
item_id = "armor_green_viper_feet"
display_name = "青蛇刺客静音靴"
description = "一对由青蛇足骨与毒丝编织而成的奇异软靴，靴底刻有微型蛇纹——每一个纹路都对应着一条美杜莎曾经爬行过的路线。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是美杜莎在临终前，将自己的双脚化成的两对青蛇之足的一部分制作的。她说："我的脚已经爬不动了，但它们还记得每一条路线的感觉。让后来的青蛇刺客穿着它们，继续爬下去。"她将双脚的青蛇碎片提取出来，与毒丝结合，制作成了这对静音靴。\n\n软靴的特殊效果是：佩戴者在行走时不会发出任何声音——如同 pass without trace（静默之爬）。且每日一次，可以通过软靴「青蛇之跃」——移动力 +15，且在移动过程中可以「释放毒雾」（5 尺半径，所有生物须通过 DC14 体质豁免，失败则 poisoned 1 回合）。一位后来的青蛇刺客在穿着这对软靴爬了一年后说："它们不会告诉你暗杀谁，但它们会让你永远能够安静地到达——无论你选择哪个猎物。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "green_viper_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_green_viper_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_green_viper_feet" }
]
```

**特殊效果（设计预留）**：移动不发出声音，且免疫追踪。

---

## 套装三十八：金蝶幻术师（Golden Butterfly）护甲

> *"金蝶不是虚荣，它只是懂得用美丽来掩盖真相——因为最美丽的幻象，往往最接近真实。"*

**套装主题**：远古幻术师结社「金蝶之翼」的装备。这个结社的成员能够用美丽来欺骗，借助金蝶的力量幻化和魅惑。

**历史渊源**：金蝶之翼的创始人「第一幻术师」莫甘娜，是一位能够与所有蝴蝶对话的聋哑少女。她说："我听不见人类的声音，但我能听见蝴蝶的振翅声。这不是残疾，而是天赋。"

---

### 38.1 金蝶幻术师蝶之冠（Golden Butterfly Butterfly Crown）

```gdscript
item_id = "armor_golden_butterfly_head"
display_name = "金蝶幻术师蝶之冠"
description = "一顶由金蝶翅膀与幻光丝编织而成的华丽头冠，头冠上镶嵌着三颗微型金蝶之眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」别人的欲望。\n\n莫甘娜在编织这头冠时，收集了七只千年金蝶的翅膀。她说："金蝶不是虚荣，它只是懂得'美丽'——用美丽来吸引，用美丽来欺骗，用美丽来掩盖真相。"她将金蝶翅膀与幻光丝编织在一起。\n\n这头冠的特殊效果是：在任何社交场合，头冠会散发出迷人的光芒（魅力检定 +3）。且可以「看见」30 尺内任何生物最深的欲望（如同 detect thoughts 的表面层）。莫甘娜说："金蝶飞到的地方，就是我的舞台。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "golden_butterfly_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_golden_butterfly_head" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_golden_butterfly_head" }
]
```

**特殊效果（设计预留）**：可以感知 30 尺内任何生物的欲望和情绪。

---

### 38.2 金蝶幻术师幻光袍（Golden Butterfly Illusion Robe）

```gdscript
item_id = "armor_golden_butterfly_body"
display_name = "金蝶幻术师幻光袍"
description = "一件由金蝶翅膀与幻光丝编织而成的华丽长袍，袍身不断有彩虹般的光芒在流动——不是装饰，而是真正的幻光被封印在了丝线中。长袍非常轻薄——薄到几乎不存在，但这正是优势，因为幻术师的长袍不是为了防御，而是为了表演。\n\n这件长袍是莫甘娜在走遍了所有花园后，用收集到的所有金蝶翅膀编织的。她说："每一片翅膀都是一个幻象的载体，一件由幻象载体编织的长袍，比任何金属都迷惑。"她在长袍内侧缝了七个小口袋，每个口袋中都藏着一种不同的幻象粉末——从能够让人看到最美梦境的梦幻粉到能够让人看到最恐惧噩梦的恐惧粉。\n\n长袍会在佩戴者表演时自动激活——表演检定 +3（幻光的共鸣）。且长袍可以让佩戴者「创造」幻象——每日一次，创造一个 15 尺立方体的幻象（持续 1 分钟，如同 major illusion）。一位后来的金蝶幻术师在描述这件长袍时说："穿着它，我不是在穿衣服，我是在'穿着'整个梦境——每一片翅膀都是一位金蝶的灵魂，它们在保护我，迷惑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "golden_butterfly_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_golden_butterfly_body" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_golden_butterfly_body" }
]
```

**特殊效果（设计预留）**：可以创造大型幻象，且免疫魅惑和恐惧。

---

### 38.3 金蝶幻术师幻光手套（Golden Butterfly Illusion Gloves）

```gdscript
item_id = "armor_golden_butterfly_hands"
display_name = "金蝶幻术师幻光手套"
description = "一对由金蝶翅膀与幻光丝编织而成的华丽手套，手套表面不断有微型幻光在指尖旋转。握拳时，幻光会聚集在拳头上，形成一圈光刃；张开手掌时，幻光会散去，化作一阵温柔的彩虹。\n\n这对手套是莫甘娜在最后一次表演中，从最后一只金蝶那里得到的礼物。那只金蝶说："你要走了，但你的幻象永远不会停止。让我的一部分陪伴你。"它将翅膀缠绕在莫甘娜的手上，化作了一对手套。\n\n手套会在佩戴者进行幻术时自动激活——每日三次，可以释放一道幻光（15 尺锥形 2D8 radiant，目标须通过 DC14 体质豁免，失败则 blinded 1 回合）。且在进行欺瞒或表演检定时，检定 +3（金蝶的魅惑）。莫甘娜说："金蝶不是虚荣，它只是懂得'美丽'——学会与它同行，你就能魅惑任何观众。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "golden_butterfly_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_golden_butterfly_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_golden_butterfly_hands" }
]
```

**特殊效果（设计预留）**：可以释放幻光攻击，且表演检定+3。

---

### 38.4 金蝶幻术师飞舞靴（Golden Butterfly Dancing Boots）

```gdscript
item_id = "armor_golden_butterfly_feet"
display_name = "金蝶幻术师飞舞靴"
description = "一对由金蝶足骨与幻光丝编织而成的奇异软靴，靴底刻有微型花纹——每一个花纹都对应着一首莫甘娜曾经跳过的舞曲。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是莫甘娜在临终前，将自己的双脚化成的两对金蝶之足的一部分制作的。她说："我的脚已经跳不动了，但它们还记得每一首舞曲的感觉。让后来的金蝶幻术师穿着它们，继续跳下去。"她将双脚的金蝶碎片提取出来，与幻光丝结合，制作成了这对飞舞靴。\n\n软靴的特殊效果是：佩戴者在跳舞时魅力检定 +3（金蝶的舞步）。且每日一次，可以通过软靴「金蝶之舞」——移动力 +15，且在移动过程中可以「释放幻光」（15 尺锥形，所有生物须通过 DC14 体质豁免，失败则 charmed 1 回合）。一位后来的金蝶幻术师在穿着这对软靴跳了一年后说："它们不会告诉你跳什么，但它们会让你永远能够优雅地到达——无论你选择哪个舞台。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "golden_butterfly_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_golden_butterfly_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_golden_butterfly_feet" }
]
```

**特殊效果（设计预留）**：跳舞时魅力检定+3，且每日一次可以魅惑周围生物。

---

## 套装三十九：紫晶心灵师（Amethyst Psychic）护甲

> *"紫晶不是宝石，它是凝固的精神——每一块都封存着一个灵魂的碎片。"*

**套装主题**：远古心灵师结社「紫晶之眼」的装备。这个结社的成员能够读取和操控心灵，借助紫晶的力量感知和攻击。

**历史渊源**：紫晶之眼的创始人「第一心灵师」泽维尔，是一位能够用心灵与所有生物对话的盲眼少年。他说："我看不见物质世界，但我能看见心灵的世界。这不是残疾，而是天赋。"

---

### 39.1 紫晶心灵师紫晶冠（Amethyst Psychic Amethyst Crown）

```gdscript
item_id = "armor_amethyst_psychic_head"
display_name = "紫晶心灵师紫晶冠"
description = "一顶由紫水晶碎片与心灵丝编织而成的奇异头冠，头冠上镶嵌着三颗微型紫晶之眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」别人的心灵。\n\n泽维尔在编织这头冠时，收集了一百块紫水晶的碎片。他说："紫晶不是宝石，它是'凝固的精神'——每一块都封存着一个灵魂的碎片。"他将紫水晶碎片与心灵丝编织在一起。\n\n这头冠的特殊效果是：在任何环境中，头冠会散发出微弱的紫色光芒（心灵感应范围 30 尺）。且可以「看见」30 尺内任何生物的心灵轮廓（如同 detect thoughts 的表面层）。泽维尔说："紫晶照耀的地方，就是我的心灵领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "amethyst_psychic_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_amethyst_psychic_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_amethyst_psychic_head" }
]
```

**特殊效果（设计预留）**：可以感知 30 尺内任何生物的情绪和意图。

---

### 39.2 紫晶心灵师心灵袍（Amethyst Psychic Mind Robe）

```gdscript
item_id = "armor_amethyst_psychic_body"
display_name = "紫晶心灵师心灵袍"
description = "一件由紫水晶碎片与心灵丝编织而成的奇异长袍，袍身不断有微弱的紫色光芒在流动——不是装饰，而是真正的精神能量被封印在了丝线中。长袍非常轻薄——薄到几乎不存在，但这正是优势，因为心灵师的长袍不是为了防御，而是为了集中精神。\n\n这件长袍是泽维尔在走遍了所有矿山后，用收集到的所有紫水晶碎片编织的。他说："每一块碎片都是一个灵魂的记忆，一件由灵魂记忆编织的长袍，比任何金属都强大。"他在长袍内侧缝了七个小口袋，每个口袋中都藏着一块不同颜色的紫水晶——从能够增强感知的透明紫晶到能够释放精神冲击的深色紫晶。\n\n长袍会在佩戴者集中精神时自动激活——所有心灵相关检定 +3（紫晶的共鸣）。且长袍可以让佩戴者「防御」心灵攻击——每日一次，完全免疫一次 psychic damage（精神护盾）。一位后来的紫晶心灵师在描述这件长袍时说："穿着它，我不是在穿衣服，我是在'穿着'整个精神世界——每一块碎片都是一个灵魂的守护，它们在保护我，指引我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "amethyst_psychic_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_amethyst_psychic_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_amethyst_psychic_body" }
]
```

**特殊效果（设计预留）**：每日一次免疫 psychic damage，且所有心灵检定+3。

---

### 39.3 紫晶心灵师心灵手套（Amethyst Psychic Mind Gloves）

```gdscript
item_id = "armor_amethyst_psychic_hands"
display_name = "紫晶心灵师心灵手套"
description = "一对由紫水晶碎片与心灵丝编织而成的奇异手套，手套表面不断有微弱的紫色光芒在指尖旋转。握拳时，光芒会聚集在拳头上，形成一圈精神利刃；张开手掌时，光芒会散去，化作一阵温柔的抚慰。\n\n这对手套是泽维尔在最后一次心灵探测中，从最后一块紫水晶那里得到的礼物。那块紫水晶说："你要走了，但你的心灵永远不会停止探索。让我的一部分陪伴你。"它将碎片缠绕在泽维尔的手上，化作了一对手套。\n\n手套会在佩戴者进行心灵攻击时自动激活——每日三次，可以释放一道精神冲击（30 尺射程 2D8 psychic，目标须通过 DC14 智慧豁免，失败则 stunned 1 回合）。且在进行洞悉或欺瞒检定时，检定 +3（紫晶的洞察）。泽维尔说："紫晶不是宝石，它是'精神'——学会与它同行，你就能操控任何心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "amethyst_psychic_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_amethyst_psychic_hands" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_amethyst_psychic_hands" }
]
```

**特殊效果（设计预留）**：可以释放精神冲击，且洞悉检定+3。

---

### 39.4 紫晶心灵师静步靴（Amethyst Psychic Silent Boots）

```gdscript
item_id = "armor_amethyst_psychic_feet"
display_name = "紫晶心灵师静步靴"
description = "一对由紫水晶碎片与心灵丝编织而成的奇异软靴，靴底刻有微型精神纹路——每一个纹路都对应着一条泽维尔曾经走过的路线。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是泽维尔在临终前，将自己的双脚化成的两段精神能量的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一条路线的感觉。让后来的紫晶心灵师穿着它们，继续走下去。"他将双脚的精神碎片提取出来，与心灵丝结合，制作成了这对静步靴。\n\n软靴的特殊效果是：佩戴者在行走时不会发出任何声音——如同 pass without trace（静默之步）。且每日一次，可以通过软靴「心灵步」——移动力 +10，且在移动过程中可以「读取」周围生物的思想（30 尺内所有生物的表面思想）。一位后来的紫晶心灵师在穿着这对软靴走了一年后说："它们不会告诉你去哪里，但它们会让你安静地到达——同时知道所有人的秘密。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "amethyst_psychic_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_amethyst_psychic_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_amethyst_psychic_feet" }
]
```

**特殊效果（设计预留）**：移动不发出声音，且免疫追踪。

---

## 套装四十：黑铁咒术师（Black Iron Warlock）护甲

> *"契约不是束缚，它是'交换'——你付出什么，就得到什么。关键在于，你是否愿意付出。"*

**套装主题**：远古咒术师结社「黑铁之契」的装备。这个结社的成员通过与各种存在签订契约获得力量，借助黑铁的力量约束和释放。

**历史渊源**：黑铁之契的创始人「第一契约者」浮士德，是一位能够与任何存在签订契约的疯狂学者。他说："我不是在出卖灵魂，我是在'投资'——用有限的代价，换取无限的知识。"

---

### 40.1 黑铁咒术师黑铁冠（Black Iron Warlock Iron Crown）

```gdscript
item_id = "armor_black_iron_warlock_head"
display_name = "黑铁咒术师黑铁冠"
description = "一顶由黑铁与契约丝编织而成的沉重头冠，头冠上镶嵌着三颗微型契约之眼。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「看见」所有契约的条款。\n\n浮士德在编织这头冠时，将自己的第一份契约的条款都刻入了黑铁。他说："契约不是束缚，它是'记录'——记录了每一次交换，每一次付出，每一次收获。"他将黑铁与契约丝编织在一起。\n\n这头冠的特殊效果是：在任何环境中，头冠会散发出微弱的黑色光芒（契约感知范围 30 尺）。且可以「看见」30 尺内所有存在的契约条款（如同 detect magic，但只对契约有效）。浮士德说："契约签订的地方，就是我的领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "black_iron_warlock_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_black_iron_warlock_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_black_iron_warlock_head" }
]
```

**特殊效果（设计预留）**：可以感知 30 尺内所有契约和魔法约束。

---

### 40.2 黑铁咒术师黑铁甲（Black Iron Warlock Iron Mail）

```gdscript
item_id = "armor_black_iron_warlock_body"
display_name = "黑铁咒术师黑铁甲"
description = "一件由黑铁与契约丝编织而成的沉重链甲，甲身不断有契约符文在流动——不是装饰，而是真正的契约被封印在了丝线中。链甲非常沉重——比普通链甲重三倍，因为其中蕴含了契约之力。\n\n这件链甲是浮士德在与一百个存在签订契约后，用所有契约的条款编织的。他说："每一条条款都是一个力量的来源，一件由力量来源编织的铠甲，比任何金属都强大。"他在链甲内侧缝了七个小口袋，每个口袋中都藏着一份不同的契约副本——从与恶魔的契约到与天使的契约。\n\n链甲会在佩戴者签订契约时自动激活——契约效果增强 50%（黑铁的约束）。且链甲可以让佩戴者「防御」契约反噬——每日一次，完全免疫一次契约的负面效果（契约护盾）。一位后来的黑铁咒术师在描述这件链甲时说："穿着它，我不是在穿衣服，我是在'穿着'所有契约——每一条条款都是一个存在的承诺，它们在保护我，约束敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "black_iron_warlock_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 26000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_black_iron_warlock_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_black_iron_warlock_body" }
]
```

**特殊效果（设计预留）**：契约效果增强 50%，且每日一次免疫契约反噬。

---

### 40.3 黑铁咒术师契约手套（Black Iron Warlock Contract Gauntlets）

```gdscript
item_id = "armor_black_iron_warlock_hands"
display_name = "黑铁咒术师契约手套"
description = "一对由黑铁与契约丝编织而成的沉重手套，手套表面不断有契约符文在指尖旋转。握拳时，符文会聚集在拳头上，形成一圈契约之刃；张开手掌时，符文会散去，化作一阵温柔的约束。\n\n这对手套是浮士德在最后一次契约中，从最后一份契约那里得到的礼物。那份契约说："你要走了，但你的契约永远不会停止。让我的一部分陪伴你。"它将条款缠绕在浮士德的手上，化作了一对手套。\n\n手套会在佩戴者释放契约力量时自动激活——每日三次，可以释放一道契约之刃（30 尺射程 2D8 force，目标须通过 DC14 体质豁免，失败则 restrained 1 回合）。且在进行奥术或欺瞒检定时，检定 +3（契约的约束）。浮士德说："契约不是束缚，它是'交换'——学会与它同行，你就能获得任何力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "black_iron_warlock_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_black_iron_warlock_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_black_iron_warlock_hands" }
]
```

**特殊效果（设计预留）**：可以释放契约之刃，且奥术检定+3。

---

### 40.4 黑铁咒术师黑铁靴（Black Iron Warlock Iron Boots）

```gdscript
item_id = "armor_black_iron_warlock_feet"
display_name = "黑铁咒术师黑铁靴"
description = "一对由黑铁与契约丝编织而成的沉重重靴，靴底刻有微型契约纹路——每一个纹路都对应着一条浮士德曾经走过的路线。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对重靴是浮士德在临终前，将自己的双脚化成的两段契约能量的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一条路线的感觉。让后来的黑铁咒术师穿着它们，继续走下去。"他将双脚的契约碎片提取出来，与契约丝结合，制作成了这对黑铁靴。\n\n重靴的特殊效果是：佩戴者在签订契约时不会受到欺骗——自动识别契约中的所有隐藏条款（契约之眼）。且每日一次，可以通过重靴「契约步」——移动力 +10，且在移动过程中可以「签订」临时契约（与一个 5 尺内的生物达成临时协议，效果由 DM 决定）。一位后来的黑铁咒术师在穿着这对重靴走了一年后说："它们不会告诉你签订什么，但它们会让你安全地到达——同时不被任何契约欺骗。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "black_iron_warlock_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_black_iron_warlock_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_black_iron_warlock_feet" }
]
```

**特殊效果（设计预留）**：自动识别契约中的隐藏条款，且免疫契约欺骗。

---

*套装 31–40 护甲部分完结 · 共 40 件护甲装备*

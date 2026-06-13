# 传奇装备套装饰品设计文档（套装 31–40：轻甲套装饰品）

> 10 套轻甲套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装三十一：风语者（Wind Whisperer）饰品

---

### 31.5 风语者斗篷

```gdscript
item_id = "acc_wind_whisperer_cloak"
display_name = "风语者斗篷"
description = "一件由凝结的风与云丝编织而成的奇异斗篷，斗篷轻薄到几乎不存在——当你触摸它时，感觉到的只是空气的流动。斗篷的边缘不断有微风在旋转，将穿戴者的身体包裹在一层看不见的气流中。\n\n艾欧洛斯在编织这件斗篷时，将一整阵风都压缩成了丝线。他说："这件斗篷不只是斗篷，它是'风的化身'——让我穿着风本身。"\n\n这件斗篷的特殊效果是：穿戴者的重量减半（风的托力）。且可以从高处「滑翔」——免疫 falling damage（风托住身体）。且可以通过斗篷「释放」狂风——每日一次，释放一道狂风（15 尺锥形，所有生物须通过 DC14 力量豁免，失败则推后 15 尺）。艾欧洛斯说："斗篷不是只能滑翔，它也可以攻击——用狂风来击退敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "wind_whisperer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_wind_whisperer_cloak" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_wind_whisperer_cloak" }
]
```

---

### 31.6 风语者项链

```gdscript
item_id = "acc_wind_whisperer_necklace"
display_name = "风语者项链"
description = "一条由风之结晶与云丝串联而成的项链，项链中央镶嵌着一颗「风之心」——一颗能够感知风向和风速的魔法宝石。项链佩戴时，宝石会随风的吹拂微微旋转——旋转的方向指向风的来向。\n\n艾欧洛斯在制作这条项链时，从一阵龙卷风中取出了一块结晶。他说："这块结晶不是被取出的，是'被借用的'——风允许我看一眼它的内心。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有风的流向和强度（风向雷达）。且可以通过项链「借助」风力——顺风时移动力 +10，逆风时移动力不受惩罚（风之祝福）。艾欧洛斯说："风之心不是只能感知，它也可以借助——借助风的力量来移动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "wind_whisperer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_wind_whisperer_necklace" }
]
```

---

### 31.7 风语者戒指（左）

```gdscript
item_id = "acc_wind_whisperer_ring_1"
display_name = "风语者戒指·疾风"
description = "一枚由风之结晶与云丝锻造而成的轻盈戒指，戒指表面有类似风纹的纹理——在光线下几乎看不见。戒指佩戴时，会根据风速变化温度——风大时凉爽，风小时温暖。\n\n艾欧洛斯在锻造这枚戒指时，将一道永远旋转的风封入了结晶。他说："这道风不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者的移动力 +5（风的轻盈）。且可以通过戒指「释放」风刃——每日三次，释放一道风刃（30 尺射程 1D10 slashing）。艾欧洛斯说："风不是只能用来移动，它也可以用来切割——用风刃来切割敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "wind_whisperer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_wind_whisperer_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_wind_whisperer_ring_1" }
]
```

---

### 31.8 风语者戒指（右）

```gdscript
item_id = "acc_wind_whisperer_ring_2"
display_name = "风语者戒指·云纹"
description = "一枚由云之结晶与风丝锻造而成的柔软戒指，戒指表面有类似云朵的纹理——柔软、轻盈、不断变化。戒指佩戴时，会根据天气变化颜色——晴天时白色，阴天时灰色，雨天时蓝色。\n\n艾欧洛斯在锻造这枚戒指时，将一朵永远漂浮的云封入了结晶。他说："这朵云不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 1 里内所有天气的变化（天气雷达）。且可以通过戒指「召唤」云雾——每日一次，召唤一团云雾（15 尺半径，困难地形，所有生物在区域内 blinded，持续 1 分钟）。艾欧洛斯说："云不是只能用来观赏，它也可以用来遮蔽——用云雾来遮蔽敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "wind_whisperer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_wind_whisperer_ring_2" }
]
```

---

### 31.9 风语者特殊饰品

```gdscript
item_id = "acc_wind_whisperer_trinket"
display_name = "风语者风铃"
description = "一个由风之金属与云丝制成的小型风铃，风铃可以发出最悦耳的声音——声音会随着风的变化而变化：微风时轻柔，大风时激昂，暴风时震撼。风铃佩戴时，会随风的吹拂自然摇摆，发出悦耳的铃声。\n\n艾欧洛斯在制作这个风铃时，将一阵永远歌唱的风封入了金属。他说："这阵风不是被封入的，是'被邀请的'——它选择留在风铃中，永远歌唱。"\n\n这个风铃的特殊效果是：穿戴者可以用风铃「传递」信息——向 1 里内的其他风语者传递一条信息（如同 message spell，每日三次）。且可以通过风铃「驱散」——每日一次，释放一阵强风（15 尺锥形，所有生物须通过 DC14 力量豁免，失败则推后 10 尺并解除所有 cloud/fog 效果）。艾欧洛斯说："风铃不是只能听，它也可以用来传递——传递信息，传递力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "wind_whisperer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_trinket" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_trinket" }
]
```

---

### 31.10 风语者徽章

```gdscript
item_id = "acc_wind_whisperer_badge"
display_name = "风语者徽章"
description = "一枚由风之金属与云丝锻造而成的轻盈徽章，徽章上刻有风语者的标志——一阵旋转的风。徽章佩戴时，会根据风速变化光芒的强度——风越大越明亮。\n\n艾欧洛斯在锻造这枚徽章时，将自己的「风语者之心」封入了金属。他说："这枚徽章不只是徽章，它是'风的象征'——让所有人知道，风语者来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他风语者的存在（徽章会微微发热）。且可以通过徽章「召唤」风——每日一次，召唤一阵顺风（15 尺半径内所有盟友移动力 +10，持续 1 回合）。艾欧洛斯说："徽章不是只能佩戴，它也可以用来召唤——召唤风的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "wind_whisperer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wind_whisperer_badge" }
]
```

---

## 套装三十二：水波行者（Wave Walker）饰品

---

### 32.5 水波行者斗篷

```gdscript
item_id = "acc_wave_walker_cloak"
display_name = "水波行者斗篷"
description = "一件由凝结的水与珍珠丝编织而成的奇异斗篷，斗篷表面不断有水波在流动，仿佛穿戴者身上披着一片海洋。斗篷非常重——在干燥环境中重量正常，但在水中几乎失重，因为水的浮力托住了它。\n\n涅柔斯在编织这件斗篷时，将一片海洋的水压缩成了丝线。他说："这件斗篷不只是斗篷，它是'海洋的碎片'——让我穿着海洋本身。"\n\n这件斗篷的特殊效果是：在水中，斗篷提供 +2 AC（水的护盾）。且可以通过斗篷「释放」水流——每日一次，释放一道水流（15 尺锥形 2D8 bludgeoning，目标须通过 DC14 力量豁免，失败则推后 10 尺）。涅柔斯说："斗篷不是只能防护，它也可以攻击——用水流来冲击敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "wave_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_wave_walker_cloak" },
    { attribute_id = "resistance_cold", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_wave_walker_cloak" }
]
```

---

### 32.6 水波行者项链

```gdscript
item_id = "acc_wave_walker_necklace"
display_name = "水波行者项链"
description = "一条由深海珍珠与珍珠丝串联而成的项链，项链中央镶嵌着一颗「海之心」——一颗能够感知潮汐和洋流的魔法宝石。项链佩戴时，宝石会随潮汐的涨落微微脉动——脉动的节奏与海洋同步。\n\n涅柔斯在制作这条项链时，从一只千年巨蚌中取出了这颗珍珠。他说："这颗珍珠不是被取出的，是'被赐予的'——巨蚌选择了我，让我成为它的守护者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 1 里内所有水源的位置和状态（水源雷达）。且可以通过项链「借助」潮汐——涨潮时移动力 +10（水中），退潮时感知范围翻倍。涅柔斯说："海之心不是只能感知，它也可以借助——借助潮汐的力量来移动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "wave_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wave_walker_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_wave_walker_necklace" }
]
```

---

### 32.7 水波行者戒指（左）

```gdscript
item_id = "acc_wave_walker_ring_1"
display_name = "水波行者戒指·潮汐"
description = "一枚由潮汐石与珍珠丝锻造而成的湿润戒指，戒指表面有类似水波的纹理——在光线下会折射出彩虹般的光芒。戒指佩戴时，会根据潮汐的涨落变化温度——涨潮时凉爽，退潮时温暖。\n\n涅柔斯在锻造这枚戒指时，将一道永远流动的潮汐封入了石头。他说："这道潮汐不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：在水中，戒指会「共鸣」——游泳速度 +10（潮汐推进）。且可以通过戒指「释放」水弹——每日三次，释放一个水弹（30 尺射程 1D10 bludgeoning）。涅柔斯说："潮汐不是只能用来游泳，它也可以用来攻击——用水弹来冲击敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "wave_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_wave_walker_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_wave_walker_ring_1" }
]
```

---

### 32.8 水波行者戒指（右）

```gdscript
item_id = "acc_wave_walker_ring_2"
display_name = "水波行者戒指·冰晶"
description = "一枚由深海冰晶与珍珠丝锻造而成的冰冷戒指，戒指表面有类似冰花的纹理——寒冷、透明、充满力量。戒指佩戴时，会根据周围温度变化光芒——越冷越明亮。\n\n涅柔斯在锻造这枚戒指时，将一块来自海底冰川的冰晶封入了金属。他说："这块冰晶不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者免疫「寒冷环境」和「冰冻」效果（冰晶保护）。且可以通过戒指「释放」冰锥——每日三次，释放一道冰锥（30 尺射程 1D10 cold + 1D6 piercing）。涅柔斯说："冰晶不是只能用来保暖，它也可以用来攻击——用冰锥来刺穿敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "wave_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_wave_walker_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_wave_walker_ring_2" }
]
```

---

### 32.9 水波行者特殊饰品

```gdscript
item_id = "acc_wave_walker_trinket"
display_name = "水波行者海螺"
description = "一个由深海海螺与珍珠制成的小型海螺，海螺可以发出最深沉的声音——声音可以传到海底最深处。海螺佩戴时，会随穿戴者的呼吸微微振动——振动与海洋的脉动同步。\n\n涅柔斯在制作这个海螺时，从一只千年巨螺身上取下了它的壳。他说："这个壳不是被取下的，是'被继承的'——巨螺选择了我，让我成为它的继承者。"\n\n这个海螺的特殊效果是：穿戴者可以用海螺「传递」信息——向 2 里内的其他水波行者传递一条信息（如同 sending spell，每日一次）。且可以通过海螺「召唤」海洋生物——每日一次，召唤一只海洋生物协助（CR 1，持续 10 分钟）。涅柔斯说："海螺不是只能听，它也可以用来传递——传递信息，召唤盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "wave_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_wave_walker_trinket" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wave_walker_trinket" }
]
```

---

### 32.10 水波行者徽章

```gdscript
item_id = "acc_wave_walker_badge"
display_name = "水波行者徽章"
description = "一枚由深海贝壳与珍珠丝锻造而成的湿润徽章，徽章上刻有水波行者的标志——一道流动的波浪。徽章佩戴时，会根据周围湿度变化光芒的强度——越湿越明亮。\n\n涅柔斯在锻造这枚徽章时，将自己的「水波行者之心」封入了贝壳。他说："这枚徽章不只是徽章，它是'海洋的象征'——让所有人知道，水波行者来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他水波行者的存在（徽章会微微发热）。且可以通过徽章「召唤」水流——每日一次，召唤一股水流（15 尺半径内所有盟友恢复 2D8 HP）。涅柔斯说："徽章不是只能佩戴，它也可以用来召唤——召唤水的治愈。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "wave_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wave_walker_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_wave_walker_badge" }
]
```

---

## 套装三十三：地震先知（Earthquake Seer）饰品

---

### 33.5 地震先知斗篷

```gdscript
item_id = "acc_earthquake_seer_cloak"
display_name = "地震先知斗篷"
description = "一件由感知石与矿物丝编织而成的沉重斗篷，斗篷表面有类似岩石纹理的图案——那不是画上去的，而是真正的岩石粉末与丝线融合形成的。斗篷非常沉重——比普通斗篷重三倍，因为其中蕴含了大地之力。\n\n盖亚在编织这件斗篷时，将一座小山的岩石粉末都收集了起来。她说："这件斗篷不只是斗篷，它是'山脉的缩影'——让我穿着山脉本身。"\n\n这件斗篷的特殊效果是：穿戴者的重量翻倍（大地的重量）。且免疫「推离」和「击倒」（大地的稳定）。且可以通过斗篷「释放」地震——每日一次，释放一道微型地震（5 尺半径，所有生物须通过 DC14 敏捷豁免，失败则 prone）。盖亚说："斗篷不是只能防护，它也可以攻击——用地震来击倒敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "earthquake_seer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_earthquake_seer_cloak" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_earthquake_seer_cloak" }
]
```

---

### 33.6 地震先知项链

```gdscript
item_id = "acc_earthquake_seer_necklace"
display_name = "地震先知项链"
description = "一条由感知石与矿物丝串联而成的项链，项链中央镶嵌着一颗「地震之眼」——一颗能够感知地震波和地下结构的魔法宝石。项链佩戴时，宝石会随地震的震动微微发光——光芒的颜色代表震动的强度。\n\n盖亚在制作这条项链时，从一次大地震的震中取下了这块石头。她说："这块石头不是被取下的，是'被赐予的'——大地选择了我，让我成为它的代言人。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 5 里内所有地震的发生（地震雷达）。且可以通过项链「预测」地震——提前 1 分钟感知到即将到来的地震（足够时间寻找掩护）。盖亚说："地震之眼不是只能感知，它也可以预测——预测大地的每一次震动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "earthquake_seer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_earthquake_seer_necklace" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_earthquake_seer_necklace" }
]
```

---

### 33.7 地震先知戒指（左）

```gdscript
item_id = "acc_earthquake_seer_ring_1"
display_name = "地震先知戒指·震波"
description = "一枚由感知石与矿物丝锻造而成的粗糙戒指，戒指表面有类似地震波的纹理——粗糙、坚硬、充满力量。戒指佩戴时，会根据地震的强度变化温度——越强越热。\n\n盖亚在锻造这枚戒指时，将一道地震波的震动封入了石头。她说："这道震波不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「释放」震波——每日三次，释放一道震波（5 尺半径，所有生物须通过 DC14 敏捷豁免，失败则 prone）。且在站立不动时，AC +1（大地的稳定）。盖亚说："震波不是只能用来感知，它也可以用来攻击——用震波来击倒敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "earthquake_seer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_earthquake_seer_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_earthquake_seer_ring_1" }
]
```

---

### 33.8 地震先知戒指（右）

```gdscript
item_id = "acc_earthquake_seer_ring_2"
display_name = "地震先知戒指·矿脉"
description = "一枚由矿脉石与矿物丝锻造而成的沉重戒指，戒指表面有类似矿脉的纹理——金色、银色、铜色交织。戒指佩戴时，会根据附近矿物的距离变化温度——越近越热。\n\n盖亚在锻造这枚戒指时，将一段地下矿脉的样本封入了金属。她说："这段矿脉不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 30 尺内所有矿物的位置（矿物雷达）。且可以通过戒指「提取」矿物——每日一次，从周围的岩石中提取一小块有价值的矿物（价值 1D10 ×10 金币）。盖亚说："矿脉不是只能用来感知，它也可以用来提取——提取大地的财富。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "earthquake_seer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earthquake_seer_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_earthquake_seer_ring_2" }
]
```

---

### 33.9 地震先知特殊饰品

```gdscript
item_id = "acc_earthquake_seer_trinket"
display_name = "地震先知罗盘"
description = "一个由感知石与矿物制成的小型罗盘，罗盘的指针不是指向北方，而是指向「地心」——大地的中心。罗盘佩戴时，会随大地的脉动微微振动——振动与地震的频率同步。\n\n盖亚在制作这个罗盘时，将自己的「大地之心」封入了指针。她说："这个罗盘不是普通的工具，它是'大地的指南针'——让我永远不会在大地中迷失。"\n\n这个罗盘的特殊效果是：穿戴者可以「感知」到 30 尺内所有地下空洞和隧道（地下雷达）。且可以通过罗盘「导航」——在任何地下环境中不会迷路（自动找到最短路径）。盖亚说："罗盘不是只能指向，它也可以用来导航——导航大地的每一个角落。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "earthquake_seer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_earthquake_seer_trinket" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earthquake_seer_trinket" }
]
```

---

### 33.10 地震先知徽章

```gdscript
item_id = "acc_earthquake_seer_badge"
display_name = "地震先知徽章"
description = "一枚由感知石与矿物丝锻造而成的沉重徽章，徽章上刻有地震先知的标志——一座裂开的山。徽章佩戴时，会根据地震的强度变化光芒的强度——越强越明亮。\n\n盖亚在锻造这枚徽章时，将自己的「地震先知之心」封入了石头。她说："这枚徽章不只是徽章，它是'大地的象征'——让所有人知道，地震先知来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他地震先知的存在（徽章会微微发热）。且可以通过徽章「预警」——每日一次，向 15 尺内的所有盟友预警即将到来的地震（给予 advantage 在豁免检定上）。盖亚说："徽章不是只能佩戴，它也可以用来预警——预警大地的每一次震动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "earthquake_seer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earthquake_seer_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earthquake_seer_badge" }
]
```

---

## 套装三十四：夜幕吟游诗人（Night Bard）饰品

---

### 34.5 夜幕吟游诗人斗篷

```gdscript
item_id = "acc_night_bard_cloak"
display_name = "夜幕吟游诗人斗篷"
description = "一件由夜莺羽毛与月光丝绸编织而成的华丽斗篷，斗篷表面不断有音符般的纹理在流动。斗篷非常轻薄——薄到几乎不存在，但这正是优势，因为吟游诗人的斗篷不是为了防御，而是为了表演。斗篷的边缘有特殊的「音符流苏」——在舞动时会发出悦耳的音调。\n\n俄耳甫斯在编织这件斗篷时，将自己所有歌曲中的「最美旋律」都编入了丝线。他说："这件斗篷不只是斗篷，它是'歌曲的延续'——让我即使不在舞台上也在歌唱。"\n\n这件斗篷的特殊效果是：在表演时，斗篷会「共鸣」——表演检定 +3。且可以通过斗篷「释放」音波——每日一次，释放一道音波（15 尺锥形 2D8 thunder，目标须通过 DC14 体质豁免，失败则 deafened 1 回合）。俄耳甫斯说："斗篷不是只能装饰，它也可以攻击——用音波来震聋敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "night_bard_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_night_bard_cloak" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_cloak" }
]
```

---

### 34.6 夜幕吟游诗人项链

```gdscript
item_id = "acc_night_bard_necklace"
display_name = "夜幕吟游诗人项链"
description = "一条由夜莺羽毛与月光丝绸串联而成的项链，项链中央镶嵌着一颗「音乐之心」——一颗能够储存和播放音乐的魔法宝石。项链佩戴时，宝石会随穿戴者的情绪播放不同的旋律——快乐时欢快，悲伤时忧伤，愤怒时激昂。\n\n俄耳甫斯在制作这条项链时，将自己最伟大的歌曲都封入了宝石。他说："这颗宝石不是普通的石头，它是'音乐的灵魂'——让我可以随时播放任何歌曲。"\n\n这条项链的特殊效果是：穿戴者可以「播放」音乐——无需乐器，自动播放任何已知的歌曲（持续 1 小时）。且可以通过项链「治愈」——每日一次，播放治愈旋律（15 尺内所有盟友恢复 2D8 HP）。俄耳甫斯说："音乐之心不是只能播放，它也可以治愈——用旋律来治愈心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "night_bard_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_night_bard_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_night_bard_necklace" }
]
```

---

### 34.7 夜幕吟游诗人戒指（左）

```gdscript
item_id = "acc_night_bard_ring_1"
display_name = "夜幕吟游诗人戒指·旋律"
description = "一枚由琴弦碎片与月光丝绸锻造而成的精致戒指，戒指表面有类似琴弦的纹理——在触碰时会发出音调。戒指佩戴时，会根据周围声音的变化变化颜色——音乐时彩色，噪音时灰色，寂静时透明。\n\n俄耳甫斯在锻造这枚戒指时，将自己使用的第一根琴弦熔入了金属。他说："这根琴弦不是普通的材料，它是'音乐的起源'——让我可以随时创造音乐。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「演奏」—— bonus action，用戒指演奏一个简单的音符（可以用来传递信号或迷惑敌人）。且在进行表演时，检定 +2（琴弦的灵敏度）。俄耳甫斯说："琴弦不是只能用来演奏，它也可以用来传递——传递信息，传递情感。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "night_bard_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_night_bard_ring_1" }
]
```

---

### 34.8 夜幕吟游诗人戒指（右）

```gdscript
item_id = "acc_night_bard_ring_2"
display_name = "夜幕吟游诗人戒指·诗篇"
description = "一枚由诗篇纸张与月光丝绸锻造而成的精致戒指，戒指表面刻有无数微型诗篇——每一个字都是一个故事，每一行都是一段历史。戒指佩戴时，会根据穿戴者的情绪变化光芒——创作时明亮，表演时闪烁，休息时暗淡。\n\n俄耳甫斯在锻造这枚戒指时，将自己所有的诗篇都刻入了戒指。他说："这些诗篇不是普通的文字，它们是'灵魂的声音'——让我可以随时吟诵任何诗篇。"\n\n这枚戒指的特殊效果是：穿戴者可以「吟诵」诗篇——无需准备，自动吟诵任何已知的诗篇（可以用来激励盟友或迷惑敌人）。且在进行说服时，检定 +2（诗篇的说服力）。俄耳甫斯说："诗篇不是只能用来吟诵，它也可以用来说服——用诗篇来说服人心。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "night_bard_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_night_bard_ring_2" }
]
```

---

### 34.9 夜幕吟游诗人特殊饰品

```gdscript
item_id = "acc_night_bard_trinket"
display_name = "夜幕吟游诗人竖琴"
description = "一个由夜莺骨与月光丝绸制成的小型竖琴，竖琴只有三根弦，但每一根弦都可以发出任何音调。竖琴佩戴时，会随穿戴者的呼吸微微振动——振动与心跳同步，形成一种独特的节奏。\n\n俄耳甫斯在制作这个竖琴时，将夜莺的鸣叫封入了琴弦。他说："这个竖琴不是普通的乐器，它是'夜莺的灵魂'——让我可以随时演奏任何旋律。"\n\n这个竖琴的特殊效果是：穿戴者可以用竖琴「演奏」任何旋律——无需技能检定，自动成功。且可以通过竖琴「治愈」——每日一次，演奏治愈旋律（15 尺内所有盟友恢复 2D8 HP 并解除 frightened）。俄耳甫斯说："竖琴不是只能演奏，它也可以治愈——用旋律来治愈心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "night_bard_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_night_bard_trinket" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_trinket" }
]
```

---

### 34.10 夜幕吟游诗人徽章

```gdscript
item_id = "acc_night_bard_badge"
display_name = "夜幕吟游诗人徽章"
description = "一枚由夜莺羽毛与月光丝绸锻造而成的精致徽章，徽章上刻有夜幕吟游诗人的标志——一把竖琴下交叉的羽毛笔。徽章佩戴时，会根据周围观众的数量变化光芒的强度——越多越明亮。\n\n俄耳甫斯在锻造这枚徽章时，将自己的「吟游诗人之魂」封入了羽毛。他说："这枚徽章不只是徽章，它是'音乐的象征'——让所有人知道，夜幕吟游诗人来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有观众的存在（徽章会微微发热）。且可以通过徽章「吸引」——每日一次，强制 15 尺内一个目标倾听穿戴者的表演（目标须通过 DC14 魅力豁免，失败则 charmed 1 回合）。俄耳甫斯说："徽章不是只能佩戴，它也可以用来吸引——吸引所有观众的目光。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "night_bard_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_night_bard_badge" }
]
```

---

## 套装三十五：白鸦信使（White Raven）饰品

---

### 35.5 白鸦信使斗篷

```gdscript
item_id = "acc_white_raven_cloak"
display_name = "白鸦信使斗篷"
description = "一件由白鸦羽毛与变色蚕丝编织而成的奇异斗篷，斗篷的颜色会随着环境自动变化——在雪地中是白色，在森林中是绿色，在黑夜中是黑色。斗篷非常轻薄——薄到几乎不存在，但这正是优势，因为信使的斗篷不是为了防御，而是为了隐匿。\n\n墨丘利在编织这件斗篷时，收集了九十九只白鸦的羽毛。他说："这件斗篷不只是斗篷，它是'白鸦的翅膀'——让我可以像白鸦一样飞翔。"\n\n这件斗篷的特殊效果是：在任何环境中，斗篷会自动调整颜色以匹配环境（+3 AC 来自 camouflage）。且可以通过斗篷「释放」羽毛——每日一次，释放一片白鸦羽毛（30 尺射程，羽毛可以传递一条信息给指定目标）。墨丘利说："斗篷不是只能隐藏，它也可以用来传递——用羽毛来传递信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "white_raven_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_white_raven_cloak" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_cloak" }
]
```

---

### 35.6 白鸦信使项链

```gdscript
item_id = "acc_white_raven_necklace"
display_name = "白鸦信使项链"
description = "一条由白鸦羽毛与变色蚕丝串联而成的项链，项链中央镶嵌着一颗「信使之眼」——一颗能够看穿密封信件内容的魔法宝石。项链佩戴时，宝石会随信件的距离微微发热——越近越热。\n\n墨丘利在制作这条项链时，从一只白鸦身上取下了一根羽毛。他说："这根羽毛不是被取下的，是'被赐予的'——白鸦选择了我，让我成为它的信使。"\n\n这条项链的特殊效果是：穿戴者可以「看穿」密封信件的内容（无需打开信封）。且可以通过项链「追踪」信件——30 尺内任何未投递的信件会被自动定位。墨丘利说："信使之眼不是只能看，它也可以追踪——追踪所有需要传递的信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "white_raven_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_white_raven_necklace" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_necklace" }
]
```

---

### 35.7 白鸦信使戒指（左）

```gdscript
item_id = "acc_white_raven_ring_1"
display_name = "白鸦信使戒指·密文"
description = "一枚由密文金属与变色蚕丝锻造而成的精致戒指，戒指表面刻有无数微型密文——每一个字都是一个密码，每一行都是一个暗号。戒指佩戴时，会根据周围秘密的数量变化温度——越多越热。\n\n墨丘利在锻造这枚戒指时，将自己掌握的所有密文都刻入了戒指。他说："这些密文不是普通的文字，它们是'信息的钥匙'——让我可以解开任何密码。"\n\n这枚戒指的特殊效果是：穿戴者可以「解读」任何密文或密码（自动成功）。且可以通过戒指「加密」信息——每日一次，将一条信息加密（只有指定的人可以解读）。墨丘利说："密文不是只能用来解读，它也可以用来加密——加密所有需要保护的信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "white_raven_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_white_raven_ring_1" }
]
```

---

### 35.8 白鸦信使戒指（右）

```gdscript
item_id = "acc_white_raven_ring_2"
display_name = "白鸦信使戒指·速度"
description = "一枚由风之金属与变色蚕丝锻造而成的轻盈戒指，戒指表面有类似风纹的纹理——在光线下几乎看不见。戒指佩戴时，会根据移动力的变化变化温度——越快越热。\n\n墨丘利在锻造这枚戒指时，将一阵永远奔跑的风封入了金属。他说："这阵风不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者的移动力 +5（风的轻盈）。且可以通过戒指「加速」——每日一次，移动力 +15（持续 1 回合）。墨丘利说："速度不是只能用来移动，它也可以用来传递——用速度来传递信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "white_raven_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_white_raven_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_white_raven_ring_2" }
]
```

---

### 35.9 白鸦信使特殊饰品

```gdscript
item_id = "acc_white_raven_trinket"
display_name = "白鸦信使哨笛"
description = "一个由白鸦骨与变色蚕丝制成的小型哨笛，哨笛可以发出最尖锐的声音——声音可以传到数里之外。哨笛佩戴时，会随穿戴者的呼吸微微振动——振动与心跳同步。\n\n墨丘利在制作这个哨笛时，从一只白鸦身上取下了它的鸣管骨。他说："这根骨头不是被取下的，是'被继承的'——白鸦选择了我，让我成为它的继承者。"\n\n这个哨笛的特殊效果是：穿戴者可以用哨笛「传递」信息——向 2 里内的其他白鸦信使传递一条信息（如同 sending spell，每日一次）。且可以通过哨笛「召唤」白鸦——每日一次，召唤一只白鸦（持续 10 分钟，可以侦察或传递信息）。墨丘利说："哨笛不是只能吹，它也可以用来传递——传递信息，召唤盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "white_raven_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_trinket" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_trinket" }
]
```

---

### 35.10 白鸦信使徽章

```gdscript
item_id = "acc_white_raven_badge"
display_name = "白鸦信使徽章"
description = "一枚由白鸦羽毛与变色蚕丝锻造而成的精致徽章，徽章上刻有白鸦信使的标志——一只飞翔的白鸦。徽章佩戴时，会根据附近信息的数量变化光芒的强度——越多越明亮。\n\n墨丘利在锻造这枚徽章时，将自己的「信使之心」封入了羽毛。他说："这枚徽章不只是徽章，它是'信息的象征'——让所有人知道，白鸦信使来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他白鸦信使的存在（徽章会微微发热）。且可以通过徽章「传递」——每日一次，向 1 里内的其他白鸦信使传递一条紧急信息（无需动作）。墨丘利说："徽章不是只能佩戴，它也可以用来传递——传递所有需要传递的信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "white_raven_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_badge" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_white_raven_badge" }
]
```

---

## 套装三十六：红狐盗贼（Red Fox Thief）饰品

---

### 36.5 红狐盗贼斗篷

```gdscript
item_id = "acc_red_fox_cloak"
display_name = "红狐盗贼斗篷"
description = "一件由红狐毛皮与狡诈蚕丝编织而成的斗篷，斗篷表面有类似红狐毛的纹理——在火焰中是红色，在阴影中是橙色，在雪地中是白色。斗篷非常轻盈——轻盈到几乎不存在，但这正是优势，因为盗贼的斗篷不是为了防御，而是为了隐匿。\n\n雷纳德在编织这件斗篷时，将整只红狐的尾毛都编入了斗篷。他说："这件斗篷不只是斗篷，它是'红狐的尾巴'——让我可以像红狐一样隐藏。"\n\n这件斗篷的特殊效果是：在任何环境中，斗篷会自动调整颜色以匹配环境（+3 AC 来自 camouflage）。且可以通过斗篷「释放」狐烟——每日一次，释放一团烟雾（5 尺半径，困难地形，所有生物 blinded，持续 1 回合）。雷纳德说："斗篷不是只能隐藏，它也可以用来逃跑——用烟雾来遮蔽逃跑。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "red_fox_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_red_fox_cloak" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_cloak" }
]
```

---

### 36.6 红狐盗贼项链

```gdscript
item_id = "acc_red_fox_necklace"
display_name = "红狐盗贼项链"
description = "一条由红狐牙齿与狡诈蚕丝串联而成的项链，项链中央镶嵌着一颗「狐之眼」——一颗能够感知宝藏和陷阱的魔法宝石。项链佩戴时，宝石会随宝藏的距离微微发热——越近越热。\n\n雷纳德在制作这条项链时，从一只红狐身上取下了一颗牙齿。他说："这颗牙齿不是被取下的，是'被继承的'——红狐选择了我，让我成为它的继承者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有宝藏和陷阱的位置（狐之眼微微发热指示方向）。且可以通过项链「识别」陷阱——自动识别任何已知的陷阱类型和解除方法。雷纳德说："狐之眼不是只能看，它也可以感知——感知所有宝藏和陷阱。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "red_fox_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_necklace" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_necklace" }
]
```

---

### 36.7 红狐盗贼戒指（左）

```gdscript
item_id = "acc_red_fox_ring_1"
display_name = "红狐盗贼戒指·灵巧"
description = "一枚由红狐爪子与狡诈蚕丝锻造而成的精致戒指，戒指表面有类似爪痕的纹理——尖锐、精确、充满技巧。戒指佩戴时，会根据灵巧度的变化变化温度——越灵巧越温暖。\n\n雷纳德在锻造这枚戒指时，将一只红狐的前爪爪子都熔入了金属。他说："这些爪子不是被熔化的，是'被融合的'——它们的灵巧现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者的开锁和扒窃检定 +3（爪子的精确度）。且可以通过戒指「快速开锁」——每日一次，瞬间打开一个普通锁（无需检定）。雷纳德说："爪子不是只能用来抓，它也可以用来开锁——用戒指上的爪子来开锁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "red_fox_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_red_fox_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_red_fox_ring_1" }
]
```

---

### 36.8 红狐盗贼戒指（右）

```gdscript
item_id = "acc_red_fox_ring_2"
display_name = "红狐盗贼戒指·狡诈"
description = "一枚由狡诈蚕丝与变色金属锻造而成的精致戒指，戒指表面有类似狐纹的图案——狡猾、多变、充满欺骗。戒指佩戴时，会根据周围谎言的数量变化颜色——越多越红。\n\n雷纳德在锻造这枚戒指时，将自己的「狡诈之心」封入了金属。他说："这枚戒指不只是戒指，它是'欺骗的工具'——让我可以欺骗任何人。"\n\n这枚戒指的特殊效果是：穿戴者的欺骗检定 +3（狡诈的加持）。且可以通过戒指「识破」谎言——30 尺内任何谎言会被自动识别。雷纳德说："狡诈不是只能用来欺骗，它也可以用来识破——识破所有谎言。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "red_fox_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_red_fox_ring_2" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_ring_2" }
]
```

---

### 36.9 红狐盗贼特殊饰品

```gdscript
item_id = "acc_red_fox_trinket"
display_name = "红狐盗贼万能钥匙"
description = "一个由红狐骨与狡诈金属制成的小型万能钥匙，钥匙可以变形以适应任何锁孔。钥匙佩戴时，会随穿戴者的动作微微发热——当附近有锁时发热最强。\n\n雷纳德在制作这个钥匙时，从一只红狐身上取下了它的颚骨。他说："这块骨头不是被取下的，是'被继承的'——红狐选择了我，让我成为它的继承者。"\n\n这个钥匙的特殊效果是：穿戴者可以用钥匙「开锁」——任何普通锁自动成功（无需检定）。且可以通过钥匙「复制」——每日一次，复制一把已见过的钥匙（复制持续 1 小时）。雷纳德说："钥匙不是只能用来开锁，它也可以用来复制——复制任何需要的钥匙。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "red_fox_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_red_fox_trinket" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_trinket" }
]
```

---

### 36.10 红狐盗贼徽章

```gdscript
item_id = "acc_red_fox_badge"
display_name = "红狐盗贼徽章"
description = "一枚由红狐毛皮与狡诈蚕丝锻造而成的精致徽章，徽章上刻有红狐盗贼的标志——一只偷笑的狐狸。徽章佩戴时，会根据附近宝藏的距离变化光芒的强度——越近越明亮。\n\n雷纳德在锻造这枚徽章时，将自己的「盗贼之心」封入了毛皮。他说："这枚徽章不只是徽章，它是'财富的象征'——让所有人知道，红狐盗贼来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内其他红狐盗贼的存在（徽章会微微发热）。且可以通过徽章「标记」——每日一次，标记一个目标（标记后 24 小时内可以追踪该目标的位置）。雷纳德说："徽章不是只能佩戴，它也可以用来标记——标记所有需要追踪的目标。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "red_fox_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_badge" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_red_fox_badge" }
]
```

---

## 套装三十七：青蛇刺客（Green Viper）饰品

---

### 37.5 青蛇刺客斗篷

```gdscript
item_id = "acc_green_viper_cloak"
display_name = "青蛇刺客斗篷"
description = "一件由青蛇蜕皮与毒丝编织而成的奇异斗篷，斗篷表面有类似蛇鳞的纹理——在光线下会折射出彩虹般的油彩光泽。斗篷非常轻盈——轻盈到几乎不存在，但这正是优势，因为刺客的斗篷不是为了防御，而是为了隐匿。\n\n美杜莎在编织这件斗篷时，收集了一千条青蛇的蜕皮。她说："这件斗篷不只是斗篷，它是'蛇的皮'——让我可以像蛇一样隐藏。"\n\n这件斗篷的特殊效果是：在任何环境中，斗篷会自动调整颜色以匹配环境（+3 AC 来自 camouflage）。且可以通过斗篷「释放」毒雾——每日一次，释放一团毒雾（5 尺半径，所有生物须通过 DC14 体质豁免，失败则 poisoned 1 回合）。美杜莎说："斗篷不是只能隐藏，它也可以用来攻击——用毒雾来毒害敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "green_viper_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_green_viper_cloak" },
    { attribute_id = "resistance_poison", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_green_viper_cloak" }
]
```

---

### 37.6 青蛇刺客项链

```gdscript
item_id = "acc_green_viper_necklace"
display_name = "青蛇刺客项链"
description = "一条由青蛇毒腺与毒丝串联而成的项链，项链中央镶嵌着一颗「毒之眼」——一颗能够感知毒素和毒物的魔法宝石。项链佩戴时，宝石会随毒素的浓度变化颜色——越浓越绿。\n\n美杜莎在制作这条项链时，从一千条青蛇中提取了毒腺。她说："这些毒腺不是被提取的，是'被继承的'——它们选择了我，让我成为它们的守护者。"\n\n这条项链的特殊效果是：穿戴者可以「感知」到 30 尺内所有毒素和毒物的位置（毒之眼微微发光指示方向）。且可以通过项链「提取」毒素——每日一次，从周围的毒物中提取一剂毒液（涂抹武器或混入食物）。美杜莎说："毒之眼不是只能看，它也可以感知——感知所有毒素的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "green_viper_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_green_viper_necklace" },
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_green_viper_necklace" }
]
```

---

### 37.7 青蛇刺客戒指（左）

```gdscript
item_id = "acc_green_viper_ring_1"
display_name = "青蛇刺客戒指·毒牙"
description = "一枚由青蛇毒牙与毒丝锻造而成的锋利戒指，戒指表面有类似毒牙的纹理——尖锐、弯曲、充满毒性。戒指佩戴时，会根据附近血液的数量变化温度——越多越热。\n\n美杜莎在锻造这枚戒指时，将一千条青蛇的毒牙都熔入了金属。她说："这些毒牙不是被熔化的，是'被融合的'——它们的毒性现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「注射」毒液—— bonus action，触碰一个目标，注入毒液（目标须通过 DC14 体质豁免，失败则 poisoned 1 分钟，每回合受到 1D6 poison，每日三次）。美杜莎说："毒牙不是只能用来咬，它也可以用来刺——用戒指上的毒牙刺入敌人的皮肤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "green_viper_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_green_viper_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_green_viper_ring_1" }
]
```

---

### 37.8 青蛇刺客戒指（右）

```gdscript
item_id = "acc_green_viper_ring_2"
display_name = "青蛇刺客戒指·蛇纹"
description = "一枚由青蛇鳞片与毒丝锻造而成的精致戒指，戒指表面有类似蛇纹的图案——蜿蜒、神秘、充满诱惑。戒指佩戴时，会根据周围温度的变化变化颜色——越热越红，越冷越蓝。\n\n美杜莎在锻造这枚戒指时，将一千条青蛇的鳞片都熔入了金属。她说："这些鳞片不是被熔化的，是'被融合的'——它们的力量现在属于我。"\n\n这枚戒指的特殊效果是：穿戴者的 AC +1（鳞片防御）。且可以通过戒指「感知」到 30 尺内所有蛇类动物的位置（蛇纹雷达）。美杜莎说："蛇纹不是只能用来装饰，它也可以用来感知——感知所有蛇类的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "green_viper_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_green_viper_ring_2" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_green_viper_ring_2" }
]
```

---

### 37.9 青蛇刺客特殊饰品

```gdscript
item_id = "acc_green_viper_trinket"
display_name = "青蛇刺客毒囊"
description = "一个由青蛇毒囊与毒丝制成的小型容器，容器内储存着「永恒毒液」——一滴永远不会蒸发、永远不会变质的毒液。毒囊佩戴时，会随穿戴者的心跳微微脉动——毒液在脉动，仿佛有生命一般。\n\n美杜莎在制作这个毒囊时，从一千条青蛇中提取了毒液。她说："这个毒囊不是被提取的，是'被继承的'——它选择了我，让我成为它的守护者。"\n\n这个毒囊的特殊效果是：穿戴者可以「提取」毒液——每日三次，提取一滴毒液（涂抹武器或混入食物，效果持续 1 小时，攻击附加 1D6 poison）。且可以通过毒囊「感知」毒素——30 尺内任何毒素或毒物会被自动识别。美杜莎说："毒囊不是只能储存，它也可以感知——感知所有毒素的存在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "green_viper_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_green_viper_trinket" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_green_viper_trinket" }
]
```

---

### 37.10 青蛇刺客徽章

```gdscript
item_id = "acc_green_viper_badge"
display_name = "青蛇刺客徽章"
description = "一枚由青蛇头骨碎片与毒丝锻造而成的恐怖徽章，徽章上刻有青蛇刺客的标志——一条盘绕的毒蛇。徽章佩戴时，会根据附近毒物的数量变化光芒的强度——越多越明亮。\n\n美杜莎在锻造这枚徽章时，将自己的「蛇母之心」封入了头骨碎片。她说："这枚徽章不只是徽章，它是'恐惧的象征'——让所有人知道，青蛇刺客来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有毒蛇的存在（徽章会微微发热）。且可以通过徽章「召唤」毒蛇——每日一次，召唤一条巨型毒蛇协助战斗（持续 10 分钟）。美杜莎说："徽章不是只能佩戴，它也可以用来召唤——召唤最致命的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "green_viper_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_green_viper_badge" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_green_viper_badge" }
]
```

---

## 套装三十八：金蝶幻术师（Golden Butterfly）饰品

---

### 38.5 金蝶幻术师斗篷

```gdscript
item_id = "acc_golden_butterfly_cloak"
display_name = "金蝶幻术师斗篷"
description = "一件由金蝶翅膀与幻光丝编织而成的华丽斗篷，斗篷表面不断有彩虹般的光芒在流动。斗篷非常轻薄——薄到几乎不存在，但这正是优势，因为幻术师的斗篷不是为了防御，而是为了表演。斗篷的边缘有特殊的「幻光流苏」——在舞动时会折射出无数幻象。\n\n莫甘娜在编织这件斗篷时，将七只千年金蝶的翅膀都编入了丝线。她说："这件斗篷不只是斗篷，它是'幻光的化身'——让我穿着幻象本身。"\n\n这件斗篷的特殊效果是：穿戴者可以「创造」幻象——每日一次，创造一个 5 尺立方体的幻象（持续 1 分钟，如同 minor illusion）。且可以通过斗篷「释放」强光——每日一次，释放一道强光（15 尺锥形，所有生物须通过 DC14 体质豁免，失败则 blinded 1 回合）。莫甘娜说："斗篷不是只能装饰，它也可以攻击——用强光来致盲敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "golden_butterfly_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_golden_butterfly_cloak" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_cloak" }
]
```

---

### 38.6 金蝶幻术师项链

```gdscript
item_id = "acc_golden_butterfly_necklace"
display_name = "金蝶幻术师项链"
description = "一条由金蝶翅膀与幻光丝串联而成的华丽项链，项链中央镶嵌着一颗「幻光之心」——一颗能够储存和释放幻象的魔法宝石。项链佩戴时，宝石会随穿戴者的情绪变化颜色——快乐时金色，悲伤时银色，愤怒时红色。\n\n莫甘娜在制作这条项链时，将自己的「幻术之心」封入了宝石。她说："这颗宝石不是普通的石头，它是'幻象的灵魂'——让我可以随时创造任何幻象。"\n\n这条项链的特殊效果是：穿戴者可以「储存」幻象——每日一次，将一个幻象储存在宝石中（之后可以随时释放，持续 1 分钟）。且可以通过项链「迷惑」——每日一次，迷惑 15 尺内一个目标（目标须通过 DC14 智慧豁免，失败则 charmed 1 回合）。莫甘娜说："幻光之心不是只能储存，它也可以迷惑——迷惑所有看到它的人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "golden_butterfly_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_golden_butterfly_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_golden_butterfly_necklace" }
]
```

---

### 38.7 金蝶幻术师戒指（左）

```gdscript
item_id = "acc_golden_butterfly_ring_1"
display_name = "金蝶幻术师戒指·幻光"
description = "一枚由金蝶翅膀碎片与幻光丝锻造而成的精致戒指，戒指表面有类似蝶翼的纹理——在光线下会折射出彩虹般的光芒。戒指佩戴时，会根据周围光线的变化变化颜色——阳光下金色，月光下银色，烛光下红色。\n\n莫甘娜在锻造这枚戒指时，将一片千年金蝶的翅膀封入了金属。她说："这片翅膀不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以用戒指「创造」小型幻象—— bonus action，创造一个 1 尺立方体的幻象（持续 1 分钟，如同 prestidigitation）。且在进行幻术检定时，检定 +2（蝶翼的灵敏度）。莫甘娜说："幻光不是只能用来观赏，它也可以用来创造——创造任何想要的幻象。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "golden_butterfly_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_golden_butterfly_ring_1" }
]
```

---

### 38.8 金蝶幻术师戒指（右）

```gdscript
item_id = "acc_golden_butterfly_ring_2"
display_name = "金蝶幻术师戒指·镜影"
description = "一枚由镜面金属与幻光丝锻造而成的奇异戒指，戒指表面如同镜子般光滑——可以反射出穿戴者的影像。戒指佩戴时，会根据周围幻象的数量变化温度——越多越热。\n\n莫甘娜在锻造这枚戒指时，将一面魔法镜子的碎片封入了金属。她说："这面镜子不是被封入的，是'被邀请的'——它选择留在戒指中，成为我的一部分。"\n\n这枚戒指的特殊效果是：穿戴者可以「反射」幻象——每日一次，将一个 directed 幻象法术反射回施法者。且可以通过戒指「复制」—— bonus action，复制自己的影像（持续 1 回合，影像可以分散敌人注意力）。莫甘娜说："镜影不是只能用来反射，它也可以用来复制——复制自己，迷惑敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "golden_butterfly_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_golden_butterfly_ring_2" }
]
```

---

### 38.9 金蝶幻术师特殊饰品

```gdscript
item_id = "acc_golden_butterfly_trinket"
display_name = "金蝶幻术师万花筒"
description = "一个由金蝶翅膀与幻光丝制成的小型万花筒，万花筒可以展现出最美丽的幻象——幻象会随着观看者的情绪而变化：快乐时花园，悲伤时海洋，愤怒时火焰。万花筒佩戴时，会随穿戴者的动作微微旋转——旋转与心跳同步。\n\n莫甘娜在制作这个万花筒时，将自己的「幻术之眼」封入了镜片。她说："这个万花筒不是普通的玩具，它是'幻象的窗户'——让我可以创造任何想要的幻象。"\n\n这个万花筒的特殊效果是：穿戴者可以用万花筒「创造」幻象——每日一次，创造一个 10 尺立方体的复杂幻象（持续 1 分钟，如同 major illusion）。且可以通过万花筒「迷惑」——每日一次，迷惑 15 尺内所有生物（须通过 DC14 智慧豁免，失败则 charmed 1 回合）。莫甘娜说："万花筒不是只能看，它也可以用来迷惑——迷惑所有看到它的人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "golden_butterfly_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_golden_butterfly_trinket" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_trinket" }
]
```

---

### 38.10 金蝶幻术师徽章

```gdscript
item_id = "acc_golden_butterfly_badge"
display_name = "金蝶幻术师徽章"
description = "一枚由金蝶翅膀与幻光丝锻造而成的华丽徽章，徽章上刻有金蝶幻术师的标志——一只展翅的金蝶。徽章佩戴时，会根据周围观众的数量变化光芒的强度——越多越明亮。\n\n莫甘娜在锻造这枚徽章时，将自己的「幻术师之魂」封入了翅膀。她说："这枚徽章不只是徽章，它是'幻象的象征'——让所有人知道，金蝶幻术师来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有被幻象迷惑的生物（徽章会微微发热）。且可以通过徽章「增强」——每日一次，增强 15 尺内所有盟友的幻术效果（幻术 DC +2，持续 1 分钟）。莫甘娜说："徽章不是只能佩戴，它也可以用来增强——增强所有幻象的效果。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "golden_butterfly_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_badge" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_golden_butterfly_badge" }
]
```

---

## 套装三十九：紫晶心灵师（Amethyst Psychic）饰品

---

### 39.5 紫晶心灵师斗篷

```gdscript
item_id = "acc_amethyst_psychic_cloak"
display_name = "紫晶心灵师斗篷"
description = "一件由紫水晶碎片与心灵丝编织而成的奇异斗篷，斗篷表面不断有微弱的紫色光芒在流动。斗篷非常轻薄——薄到几乎不存在，但这正是优势，因为心灵师的斗篷不是为了防御，而是为了集中精神。斗篷的边缘有特殊的「心灵流苏」——在精神集中时会微微发光。\n\n泽维尔在编织这件斗篷时，将自己的「精神护盾」编入了丝线。他说："这件斗篷不只是斗篷，它是'精神的屏障'——保护我的精神不受外界入侵。"\n\n这件斗篷的特殊效果是：穿戴者免疫所有「心灵阅读」和「情绪操控」效果（精神护盾）。且可以通过斗篷「反射」精神攻击——每日一次，将一个 directed 心灵法术反射回施法者。泽维尔说："斗篷不是只能防护，它也可以反射——反射所有精神攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "amethyst_psychic_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_amethyst_psychic_cloak" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_amethyst_psychic_cloak" }
]
```

---

### 39.6 紫晶心灵师项链

```gdscript
item_id = "acc_amethyst_psychic_necklace"
display_name = "紫晶心灵师项链"
description = "一条由紫水晶碎片与心灵丝串联而成的项链，项链中央镶嵌着一颗「心灵之眼」——一颗能够读取和传递思想的魔法宝石。项链佩戴时，宝石会随穿戴者的情绪变化颜色——平静时紫色，激动时红色，悲伤时蓝色。\n\n泽维尔在制作这条项链时，将自己的「心灵之眼」封入了宝石。他说："这颗宝石不是普通的石头，它是'心灵的窗户'——让我可以读取任何思想。"\n\n这条项链的特殊效果是：穿戴者可以「读取」30 尺内一个目标的表面思想（每日三次，目标须通过 DC14 智慧豁免，失败则思想被读取）。且可以通过项链「传递」思想——向 30 尺内的一个目标传递一条思想（无需语言）。泽维尔说："心灵之眼不是只能读取，它也可以传递——传递任何想要传递的思想。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "amethyst_psychic_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_amethyst_psychic_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_amethyst_psychic_necklace" }
]
```

---

### 39.7 紫晶心灵师戒指（左）

```gdscript
item_id = "acc_amethyst_psychic_ring_1"
display_name = "紫晶心灵师戒指·思维"
description = "一枚由紫水晶碎片与心灵丝锻造而成的精致戒指，戒指表面有类似脑纹的纹理——复杂、精细、充满智慧。戒指佩戴时，会根据思维的速度变化温度——思维越快越热。\n\n泽维尔在锻造这枚戒指时，将自己的「思维之力」封入了水晶。他说："这枚戒指不只是戒指，它是'思维的延伸'——让我可以控制任何思维。"\n\n这枚戒指的特殊效果是：穿戴者的智力检定 +2（思维的清晰度）。且可以通过戒指「集中」——每日一次，集中精神（下一回合施法时 DC +2）。泽维尔说："思维不是只能用来思考，它也可以用来集中——集中所有精神力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "amethyst_psychic_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_amethyst_psychic_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_amethyst_psychic_ring_1" }
]
```

---

### 39.8 紫晶心灵师戒指（右）

```gdscript
item_id = "acc_amethyst_psychic_ring_2"
display_name = "紫晶心灵师戒指·精神"
description = "一枚由紫水晶碎片与心灵丝锻造而成的精致戒指，戒指表面有类似精神波的纹理——在光线下会微微闪烁。戒指佩戴时，会根据精神力的强度变化光芒——越强越明亮。\n\n泽维尔在锻造这枚戒指时，将自己的「精神之力」封入了水晶。他说："这枚戒指不只是戒指，它是'精神的容器'——储存着我的精神力量。"\n\n这枚戒指的特殊效果是：穿戴者的精神攻击伤害 +1D6（精神强化）。且可以通过戒指「释放」精神冲击——每日三次，释放一道精神冲击（30 尺射程 2D8 psychic）。泽维尔说："精神不是只能用来思考，它也可以用来攻击——用精神冲击来击垮敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "amethyst_psychic_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_amethyst_psychic_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_amethyst_psychic_ring_2" }
]
```

---

### 39.9 紫晶心灵师特殊饰品

```gdscript
item_id = "acc_amethyst_psychic_trinket"
display_name = "紫晶心灵师水晶球"
description = "一个由紫水晶与心灵丝制成的小型水晶球，水晶球可以展现出最远的目标——目标可以是任何生物、任何地点、任何时间。水晶球佩戴时，会随穿戴者的精神力微微发光——精神力越强光芒越亮。\n\n泽维尔在制作这个水晶球时，将自己的「预知之眼」封入了水晶。他说："这个水晶球不是普通的玩具，它是'未来的窗户'——让我可以看见任何想要看见的东西。"\n\n这个水晶球特殊效果是：穿戴者可以用水晶球「预知」——每日一次，看见 1 里内一个目标的位置（如同 scrying 的 miniature 版）。且可以通过水晶球「探测」——每日一次，探测 15 尺内所有隐藏的思想（如同 detect thoughts）。泽维尔说："水晶球不是只能看，它也可以用来预知——预知任何想要预知的事情。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "amethyst_psychic_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_amethyst_psychic_trinket" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_amethyst_psychic_trinket" }
]
```

---

### 39.10 紫晶心灵师徽章

```gdscript
item_id = "acc_amethyst_psychic_badge"
display_name = "紫晶心灵师徽章"
description = "一枚由紫水晶碎片与心灵丝锻造而成的精致徽章，徽章上刻有紫晶心灵师的标志——一只睁开的眼睛。徽章佩戴时，会根据附近思维的数量变化光芒的强度——越多越明亮。\n\n泽维尔在锻造这枚徽章时，将自己的「心灵师之魂」封入了水晶。他说："这枚徽章不只是徽章，它是'心灵的象征'——让所有人知道，紫晶心灵师来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有有思维的生物（徽章会微微发热）。且可以通过徽章「链接」——每日一次，与 30 尺内一个目标建立心灵链接（持续 1 分钟，可以无声交流）。泽维尔说："徽章不是只能佩戴，它也可以用来链接——链接任何想要链接的心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "amethyst_psychic_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_amethyst_psychic_badge" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_amethyst_psychic_badge" }
]
```

---

## 套装四十：黑铁咒术师（Black Iron Warlock）饰品

---

### 40.5 黑铁咒术师斗篷

```gdscript
item_id = "acc_black_iron_warlock_cloak"
display_name = "黑铁咒术师斗篷"
description = "一件由黑铁与契约丝编织而成的奇异斗篷，斗篷表面不断有契约符文在流动——每一个符文都代表一个已签订的契约。斗篷非常沉重——比普通斗篷重三倍，因为其中蕴含了契约之力。斗篷的边缘有特殊的「契约流苏」——每一根流苏都代表一个契约的代价。\n\n浮士德在编织这件斗篷时，将自己的所有契约都编入了符文。他说："这件斗篷不只是斗篷，它是'契约的记录'——记录了我所有的交易。"\n\n这件斗篷的特殊效果是：穿戴者可以「感知」到 30 尺内所有存在的契约（契约雷达）。且可以通过斗篷「签订」临时契约——每日一次，与任何愿意交易的存在达成临时协议（效果由 DM 决定）。浮士德说："斗篷不是只能记录，它也可以用来签订——签订新的契约，获得新的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "black_iron_warlock_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_black_iron_warlock_cloak" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_black_iron_warlock_cloak" }
]
```

---

### 40.6 黑铁咒术师项链

```gdscript
item_id = "acc_black_iron_warlock_necklace"
display_name = "黑铁咒术师项链"
description = "一条由黑铁与契约丝串联而成的项链，项链中央镶嵌着一颗「契约之眼」——一颗能够看到所有契约联系的魔法宝石。项链佩戴时，宝石会随契约的数量变化颜色——契约越多颜色越深。\n\n浮士德在制作这条项链时，将自己的第一个契约都编入了宝石。他说："这颗宝石不是普通的石头，它是'契约的见证'——记录了我所有的交易。"\n\n这条项链的特殊效果是：穿戴者可以「看见」所有契约联系——看到任何生物身上的契约痕迹（谁与谁签订了什么契约，代价是什么）。且可以通过项链「解除」契约——每日一次，解除一个自愿的契约（需要双方同意）。浮士德说："契约之眼不是只能看，它也可以解除——解除任何不需要的契约。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "black_iron_warlock_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_black_iron_warlock_necklace" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_black_iron_warlock_necklace" }
]
```

---

### 40.7 黑铁咒术师戒指（左）

```gdscript
item_id = "acc_black_iron_warlock_ring_1"
display_name = "黑铁咒术师戒指·契约"
description = "一枚由黑铁与契约丝锻造而成的粗糙戒指，戒指表面刻有无数契约符文——每一个符文都代表一个已签订的契约。戒指佩戴时，符文会微微发光，光芒的颜色代表契约的类型——红色代表鲜血契约，黑色代表灵魂契约，白色代表保护契约。\n\n浮士德在锻造这枚戒指时，将自己的第一个契约都编入了符文。他说："这些符文不是普通的文字，它们是'承诺的见证'——每一个符文都是一个不可违背的誓言。"\n\n这枚戒指的特殊效果是：穿戴者可以「感知」到 30 尺内所有存在的契约（契约雷达）。且可以通过戒指「签订」临时契约——每日一次，与一个生物签订临时契约（例如"我给你 5 HP，你给我 +2 攻击"，持续 1 小时）。浮士德说："契约不是只能感知，它也可以签订——签订新的契约，获得新的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "black_iron_warlock_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_black_iron_warlock_ring_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_black_iron_warlock_ring_1" }
]
```

---

### 40.8 黑铁咒术师戒指（右）

```gdscript
item_id = "acc_black_iron_warlock_ring_2"
display_name = "黑铁咒术师戒指·代价"
description = "一枚由黑铁与契约丝锻造而成的粗糙戒指，戒指表面刻有「代价」二字——以血写就。戒指佩戴时，会根据已支付的代价变化温度——代价越大越热。\n\n浮士德在锻造这枚戒指时，将自己的第一滴契约之血都编入了金属。他说："这枚戒指不只是戒指，它是'代价的提醒'——提醒我为什么获得力量。"\n\n这枚戒指的特殊效果是：穿戴者可以「储存」代价——将受到的 damage 的一部分储存起来（最多 20 点），储存的代价可以在需要时释放——每 5 点代价可以增加 1D6 法术伤害。浮士德说："代价不是只能支付，它也可以储存——储存代价，释放力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "black_iron_warlock_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_black_iron_warlock_ring_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_black_iron_warlock_ring_2" }
]
```

---

### 40.9 黑铁咒术师特殊饰品

```gdscript
item_id = "acc_black_iron_warlock_trinket"
display_name = "黑铁咒术师契约卷轴"
description = "一个由黑铁与契约丝制成的小型卷轴，卷轴上写有无数契约条款——每一个条款都可以随时修改。卷轴佩戴时，会随穿戴者的意愿微微发光——当有契约达成时发光最强。\n\n浮士德在制作这个卷轴时，将自己的所有契约都编入了文字。他说："这个卷轴不是普通的纸张，它是'契约的集合'——让我可以随时签订任何契约。"\n\n这个卷轴的特殊效果是：穿戴者可以用卷轴「签订」契约——每日一次，与任何愿意交易的存在签订正式契约（效果由 DM 决定，但必须有对等的代价）。且可以通过卷轴「查询」——查询任何已签订的契约条款（自动成功）。浮士德说："卷轴不是只能用来签订，它也可以用来查询——查询所有契约的细节。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "black_iron_warlock_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_black_iron_warlock_trinket" },
    { attribute_id = "history_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_black_iron_warlock_trinket" }
]
```

---

### 40.10 黑铁咒术师徽章

```gdscript
item_id = "acc_black_iron_warlock_badge"
display_name = "黑铁咒术师徽章"
description = "一枚由黑铁与契约丝锻造而成的粗糙徽章，徽章上刻有黑铁咒术师的标志——一把燃烧的铁锤。徽章佩戴时，会根据附近契约的数量变化光芒的强度——越多越明亮。\n\n浮士德在锻造这枚徽章时，将自己的「咒术师之魂」封入了铁锤。他说："这枚徽章不只是徽章，它是'力量的象征'——让所有人知道，黑铁咒术师来了。"\n\n这枚徽章的特殊效果是：穿戴者可以「感知」到 30 尺内所有签订过契约的生物（徽章会微微发热）。且可以通过徽章「召唤」——每日一次，召唤一个契约生物协助战斗（CR 1，持续 10 分钟）。浮士德说："徽章不是只能佩戴，它也可以用来召唤——召唤契约中的盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "black_iron_warlock_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_black_iron_warlock_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_black_iron_warlock_badge" }
]
```

---

*轻甲套装 31–40 饰品部分完结 · 共 60 件饰品装备*

# 传奇装备套装护甲设计文档（套装 11–15）

> 5 套传奇套装的护甲部分，共 20 件护甲装备，覆盖 head / body / hands / feet 四个部位。

---

## 套装十一：星辰织者（Star Weaver）护甲

> *"星辰不是遥远的光点，而是命运的丝线。织者的工作，就是将这些丝线编织成未来。"*

**套装主题**：古老占星师结社「命运之织」的传承装备。这个结社在两百年前因预言了一场无法改变的灾难而自我解散，他们的装备被分散到世界的各个角落。

**历史渊源**：命运之织的创始人「第一织者」诺恩，是一位能够看到命运丝线的盲眼女占星师。她在临终前将自己的双眼化作了两颗星辰，永远悬挂在北方天空。

---

### 11.1 星辰织者星冠（Star Weaver Star Crown）

```gdscript
item_id = "armor_star_weaver_head"
display_name = "星辰织者星冠"
description = "一顶由陨铁与星蓝宝石锻造的精致头冠，头冠表面镶嵌着七颗微型星辰核心——每一颗都代表一个已知的星座。头冠没有面甲，因为诺恩相信"星辰不需要防御，它们只需要被看见"。佩戴时，七颗星辰核心会随佩戴者的脉搏同步闪烁，仿佛一顶微型的星空。\n\n这顶星冠是诺恩在失明前最后一次观测星空时设计的。她用三天三夜的时间，将自己观测到的所有星座位置都刻入了星冠的底座。她说："即使我的眼睛看不见了，我的头冠仍然可以'看见'——它会替我记住每一颗星辰的位置。"在她失明后，星冠成为了她的"第二双眼睛"——通过星辰核心的微弱共鸣，她能够感知到天空的变化。\n\n诺恩去世后，星冠上的星辰核心曾经全部熄灭了一整天——那是结社成员们最恐惧的一天，他们以为诺恩的星辰已经陨落。但在第二天黎明前，七颗核心同时重新亮起，排列成了一幅从未见过的星座图。后来的占星师们相信，那是诺恩从星辰之间发送的最后一条信息——但至今无人能够解读。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_star_weaver_head" }
]
```

**特殊效果（设计预留）**：在星光下，所有感知检定+2。

---

### 11.2 星辰织者星袍（Star Weaver Star Robe）

```gdscript
item_id = "armor_star_weaver_body"
display_name = "星辰织者星袍"
description = "一件由夜空丝绸与星尘编织而成的深蓝色长袍，袍身不断有微型星系在流动——不是装饰，而是真正的星辰被封印在了丝线中。长袍的腰带是一圈微型行星带，每一颗"行星"都是一颗能够增强特定魔法的微型水晶。\n\n这件星袍是诺恩用她最后一次观测到的夜空中的星尘编织的。她说："星辰的尘埃是宇宙的记忆，每一件由星尘编织的衣服，都是一段宇宙的历史。"她在编织过程中，将七种不同星座的"命运丝线"编入了长袍——每一种丝线代表一种不同的命运走向。\n\n星袍会在特定星象下自动变化颜色——当命运之织预言的"灾星"出现时，星袍会变成血红色；当"吉星"出现时，星袍会变成金黄色。结社成员们通过星袍的颜色变化来判断是否应该进行重要的占卜。一位后来的占星师在日记中写道："星袍变成血红色的那一天，结社解散了。诺恩预言了这一切，但她没有告诉我们如何阻止。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 26000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_star_weaver_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_star_weaver_body" },
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_body" }
]
```

**特殊效果（设计预留）**：在特定星象下（由 DM 决定或随机），所有法术 DC+1。

---

### 11.3 星辰织者星纹护腕（Star Weaver Star Bracers）

```gdscript
item_id = "armor_star_weaver_hands"
display_name = "星辰织者星纹护腕"
description = "一对由陨铁与星尘锻造的精致护腕，护腕表面刻有七道星轨纹路——每一道纹路代表一个已知的星座轨迹。施法时，星轨纹路会依次亮起，如同一串被点亮的星辰，指引着魔法的流向。\n\n这对护腕是诺恩的右眼化作的。她在临终前，将自己的右眼挖出，说："这只眼睛已经看够了未来，让它去看看现在吧。"她的右眼在离开眼眶的瞬间化为了一颗星尘结晶，掉落在她的右手中。学徒们将这颗结晶研磨成粉末，与陨铁混合，锻造成了这对护腕。\n\n护腕会在佩戴者进行占卜或预知时自动激活——星轨纹路会按照命运的"正确方向"流动，如果纹路逆流，则预示此次占卜的结果可能不准确。一位后来的占星师在使用这对护腕时，发现纹路曾经全部同时熄灭——那是在诺恩去世的那一天。他说："那不是故障，那是诺恩的眼睛最后一次闭上。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_hands" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_star_weaver_hands" }
]
```

**特殊效果（设计预留）**：进行占卜或预知检定时，检定+3。

---

### 11.4 星辰织者星步靴（Star Weaver Star Boots）

```gdscript
item_id = "armor_star_weaver_feet"
display_name = "星辰织者星步靴"
description = "一对由夜空皮革与星尘制成的轻便软靴，靴底刻有微型星图——每一颗"星辰"都是一块能够感应地磁场的微型水晶。行走时，靴底的水晶会根据佩戴者的"命运方向"微微发热或发冷——热代表正确方向，冷代表错误方向。\n\n这对软靴是诺恩在最后一次观测星空时穿的。她说："星辰不仅指引天空的方向，它们也指引地面的方向。只要懂得倾听星辰的声音，你就永远不会迷路。"她在靴底刻下了她观测到的所有星座的"地面投影"——那是她独特的占星术，将天空的星辰与地面的路径对应起来。\n\n软靴会在佩戴者面临重要抉择时自动激活——如果当前方向与佩戴者的"命运丝线"一致，靴底会变得温暖；如果不一致，会变得冰冷。一位后来的旅者在使用这对软靴时说："它们不会告诉我应该去哪里，但它们会告诉我'不应该'去哪里——这就足够了。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_star_weaver_feet" }
]
```

**特殊效果（设计预留）**：在星光下，移动力额外+10尺。

---

## 套装十二：毒蛇之吻（Viper's Kiss）护甲

> *"爱是最甜的毒药，也是最后的解药。"*

**套装主题**：毒师联盟「蛇巢」历代大师的传承装备。这个联盟在八十年前因内部毒杀事件而崩溃，但大师们的装备因浸透了各种毒素而获得了奇异的力量。

**历史渊源**：蛇巢的创始人「蛇母」美杜莎，原本是一位被丈夫抛弃的草药师。她在绝望中发现了「毒即药，药即毒」的真理。

---

### 12.1 毒蛇之吻头巾（Viper's Kiss Headscarf）

```gdscript
item_id = "armor_vipers_kiss_head"
display_name = "毒蛇之吻头巾"
description = "一条由毒蛛丝与蛇蜕编织而成的深绿色头巾，头巾表面不断有微弱的鳞片纹路在流动——那不是装饰，而是被封印在丝线中的蛇灵在"活动"。头巾的内衬涂有微量的麻痹毒素，能够在佩戴者受到攻击时让攻击者逐渐失去知觉。\n\n这条头巾是美杜莎在毒杀背叛她的丈夫后，用对方的头发与第一条被她驯服的毒蛇的蛇蜕编织的。她说："这条头巾不是装饰，它是'警告'——告诉所有靠近我的人，我与蛇为伍。"她在头巾的内衬涂上了她自己配制的"微笑毒素"——一种能够让受害者在微笑中死去的剧毒。\n\n头巾会在佩戴者面临背叛时自动激活——内衬的毒素会释放到佩戴者的皮肤表面，让任何触碰佩戴者的人受到 1D6 poison damage（每日一次）。一位后来的毒师在描述这条头巾时说："它不是头巾，它是'信任测试器'——任何试图背叛你的人，都会在触碰你的那一刻付出代价。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "vipers_kiss_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_vipers_kiss_head" },
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_vipers_kiss_head" }
]
```

**特殊效果（设计预留）**：对毒素-based 攻击的豁免检定+2。

---

### 12.2 毒蛇之吻鳞甲（Viper's Kiss Scale Mail）

```gdscript
item_id = "armor_vipers_kiss_body"
display_name = "毒蛇之吻鳞甲"
description = "一件由数百条不同毒蛇的鳞片拼接而成的紧身鳞甲，每一片鳞片都保留着原主的毒素特性——红色鳞片带有火毒，蓝色鳞片带有冰毒，绿色鳞片带有麻痹毒，黑色鳞片带有致命毒。鳞甲的表面不断有微弱的毒素光泽在流动，如同一条活着的巨蛇。\n\n这件鳞甲是美杜莎在创建蛇巢后，用她收集到的所有毒蛇鳞片制作的。她说："每一片鳞片都是一个生命的记忆，一件由生命记忆编织的铠甲，比任何金属都坚固。"她在鳞甲的内衬缝了四十九个小口袋，每个口袋中都藏着一种不同的毒素——从能够让人昏迷的睡眠毒，到能够让人在瞬间化为脓血的溶解毒。\n\n鳞甲会在佩戴者受到攻击时自动激活——攻击者的武器会被鳞甲表面的毒素污染（攻击者受到 1D6 poison damage，且武器在接下来 3 回合内攻击检定-1）。一位后来的战士在攻击一位穿着这件鳞甲的毒师后，发现自己的剑在数分钟内锈蚀了一半——他说："那不是铠甲，那是'毒沼泽'——一旦触碰，就再也无法摆脱。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "vipers_kiss_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "armor_vipers_kiss_body" },
    { attribute_id = "resistance_poison", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_vipers_kiss_body" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_vipers_kiss_body" }
]
```

**特殊效果（设计预留）**：近战攻击附加1D4随机毒素伤害（火毒/冰毒/麻痹毒/致命毒）。

---

### 12.3 毒蛇之吻毒爪手套（Viper's Kiss Venom Claw Gloves）

```gdscript
item_id = "armor_vipers_kiss_hands"
display_name = "毒蛇之吻毒爪手套"
description = "一对由蛇皮与毒刺编织而成的黑色手套，指尖各嵌有一枚可伸缩的毒蛇獠牙。手套内侧涂有微量的" kiss of death "——一种能够在触碰时让目标心脏骤停的剧毒。握紧拳头时，獠牙会竖起，释放出混合毒素的气息。\n\n这对手套是美杜莎在发现"毒即药，药即毒"的真理后，用自己的头发与第一条毒蛇的獠牙制作的。她说："这对手套不是武器，它们是'治愈工具'——当它们刺入正确的人时，它们在杀人；当它们刺入错误的人时，它们在救人。"她在手套内侧涂上了她自己发明的"逆转毒素"——一种能够在致命剂量下反而治愈疾病的奇异毒药。\n\n手套会在佩戴者进行徒手攻击或接触性法术时自动激活——每日三次，可以选择释放一种毒素（麻痹：目标须通过 DC14 体质豁免，失败则 paralyzed 1 回合；睡眠：目标须通过 DC14 体质豁免，失败则 unconscious 1 分钟；或致命：2D6 poison damage）。一位后来的医者在使用这对手套时说："它们既可以杀人，也可以救人——关键在于使用者的意图。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "vipers_kiss_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_vipers_kiss_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_vipers_kiss_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击附加1D6 poison damage，且可以"治疗"中毒目标（将毒素转化为解药）。

---

### 12.4 毒蛇之吻潜行靴（Viper's Kiss Stealth Boots）

```gdscript
item_id = "armor_vipers_kiss_feet"
display_name = "毒蛇之吻潜行靴"
description = "一对由毒蛇腹部软皮与毒蛛丝制成的轻便软靴，靴底覆盖着细密的鳞片纹路——能够在任何地面上行走而不发出声音。靴筒内侧缝有小型毒囊，可以在半秒内释放出麻痹毒雾。\n\n这对软靴是美杜莎在被丈夫抛弃后，独自进入蛇巢森林时穿的。她说："蛇不会发出声音，因为它们不需要声音——它们用舌头感知世界，用鳞片感知震动。我要学会像蛇一样行走，像蛇一样感知，像蛇一样安静。"她在靴底覆盖了毒蛇腹部的鳞片——那是蛇身上最柔软、最安静的部分。\n\n软靴会在佩戴者需要隐匿时自动激活——移动力 +5，且不会留下任何足迹（如同 pass without trace 的局部效果）。且靴筒内侧的毒囊可以每日一次释放麻痹毒雾（5 尺半径，所有生物须通过 DC14 体质豁免，失败则 paralyzed 1 回合）。一位后来的刺客在描述这对软靴时说："它们让你成为一条蛇——安静、致命、不可察觉。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "vipers_kiss_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_vipers_kiss_feet" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_vipers_kiss_feet" }
]
```

**特殊效果（设计预留）**：移动不留下任何痕迹，免疫追踪。

---

## 套装十三：北风旅者（Northwind Traveler）护甲

> *"我不是迷路了，我是选择了一条没有终点的路。"*

**套装主题**：传奇旅者行会「无尽之路」历代大师的传承装备。这个行会在三百年前的「大封闭」事件中失去了所有成员，但他们的装备被散落到世界的每一条道路上。

**历史渊源**：无尽之路的创始人「第一旅者」奥德修斯，是一位永远找不到回家的路的流浪者。他在临终前，将自己的双脚化为了两条道路，一条通向过去，一条通向未来。

---

### 13.1 北风旅者风帽（Northwind Traveler Wind Hood）

```gdscript
item_id = "armor_northwind_traveler_head"
display_name = "北风旅者风帽"
description = "一顶由风之羽毛与旅行者布料编织而成的轻便风帽，风帽表面不断有微弱的旋风在流动——那不是装饰，而是被封印在布料中的"风之灵"在活动。风帽的边缘没有固定形状——它会随风的方向和强度不断变化，仿佛风帽本身就是一团凝固的风。\n\n这顶风帽是奥德修斯在创建无尽之路时，从一只被北风吹散的风之灵身上收集的羽毛编织的。那只风之灵不是被捕获的——它是"自愿"留下羽毛的，因为它也想"看看地面上的世界"。奥德修斯说："风从不停留，但它留下了羽毛——这是它对这个世界的'纪念品'。"他用这些羽毛编织了这顶风帽，成为了无尽之路的标志。\n\n风帽会在佩戴者面临强风时自动激活——旋风会形成一个保护罩，让佩戴者在强风中不受任何影响（包括移动力惩罚和远程攻击劣势）。且风帽可以让佩戴者"感知"到风的来源和去向——自动预知接下来数小时的风向变化。一位后来的旅者在描述这顶风帽时说："它不是帽子，它是'风的耳朵'——它让你能够听懂风的语言。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "northwind_traveler_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_northwind_traveler_head" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_northwind_traveler_head" }
]
```

**特殊效果（设计预留）**：在强风天气中，感知检定+3。

---

### 13.2 北风旅者旅袍（Northwind Traveler Coat）

```gdscript
item_id = "armor_northwind_traveler_body"
display_name = "北风旅者旅袍"
description = "一件由风之灵的核心与旅行者布料编织而成的轻便长袍，袍身不断有微型旋风在流动——不是装饰，而是真正的风被封印在了丝线中。长袍非常轻盈——因为风之灵几乎没有重量，但也因为这轻盈，穿戴者在奔跑时会微微飘起。长袍的内侧缝有无数个小口袋，每个口袋中都藏着一张手绘的地图碎片。\n\n这件旅袍是奥德修斯在走遍了已知世界的每一条道路后，用收集到的所有风之灵的核心编织的。他说："每一条路都有它的风，每一种风都有它的故事。这件旅袍承载着所有风的故事，穿着它的人，就是在穿着整个世界。"他在旅袍的内侧缝了三百六十五个小口袋，每个口袋中都藏着一张他亲手绘制的地图——从繁华的城市到荒芜的沙漠，从深邃的森林到高耸的山脉。\n\n旅袍会在佩戴者进入未知区域时自动激活——对应的地图碎片会微微发热，指引正确的方向。且旅袍可以让佩戴者在任何地形上行走时不受减速影响（如同 freedom of movement 的移动部分）。一位后来的旅者说："穿着这件旅袍，我感觉自己不是在走路，我是在'飘'——风推着我，地图指引我，世界在我面前展开。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "northwind_traveler_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_northwind_traveler_body" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_northwind_traveler_body" }
]
```

**特殊效果（设计预留）**：在任何地形上行走时移动力不受减速影响。

---

### 13.3 北风旅者风行者手套（Northwind Traveler Windwalker Gloves）

```gdscript
item_id = "armor_northwind_traveler_hands"
display_name = "北风旅者风行者手套"
description = "一对由风之灵的触须与旅行者皮革制成的轻便手套，手套表面不断有微弱的旋风在指尖旋转。握拳时，旋风会聚集在拳头上，形成一圈风刃；张开手掌时，旋风会散去，化作一阵温柔的微风。\n\n这对手套是奥德修斯在"大封闭"事件前，从最后一位风之灵那里得到的礼物。那只风之灵说："你要走了，但你的旅程永远不会结束。让我的一部分陪伴你，即使我不能再跟随你。"它将触须缠绕在奥德修斯的手上，化作了一对手套。\n\n手套会在佩戴者进行远程攻击时自动激活——每日三次，可以释放一道风刃（30 尺射程 1D8 slashing，每日三次）。且在进行攀爬或跳跃检定时，检定 +3（风的助力）。奥德修斯说："风不是阻力，它是助力——学会顺应风，你就能到达任何想去的地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "northwind_traveler_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_northwind_traveler_hands" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_northwind_traveler_hands" }
]
```

**特殊效果（设计预留）**：远程攻击射程+15尺，且不受风力影响。

---

### 13.4 北风旅者永行靴（Northwind Traveler Everwalk Boots）

```gdscript
item_id = "armor_northwind_traveler_feet"
display_name = "北风旅者永行靴"
description = "一对由风之灵的核心碎片与旅行者皮革制成的轻便软靴，靴底刻有微型风向玫瑰——每一个方向都对应着一条奥德修斯曾经走过的道路。行走时，靴底会根据地面的质地自动调整硬度——在硬地面上柔软无声，在软地面上坚硬防滑。\n\n这对软靴是奥德修斯在临终前，用自己的双脚化成的两条道路的一部分制作的。他说："我的脚已经走不动了，但它们还记得每一条路的感觉。让后来的旅者穿着它们，继续走下去。"他将双脚的道路碎片提取出来，与风之灵的核心结合，制作成了这对永行靴。\n\n软靴的特殊效果是：佩戴者永远不会感到疲劳——可以进行双倍时间的强行军而不受 exhaustion 影响（永不停歇）。且每日一次，可以通过软靴「顺风而行」——移动力翻倍，持续 1 分钟。一位后来的旅者在穿着这对软靴走了一年后说："它们不会告诉你去哪里，但它们会让你永远能够到达——无论你选择哪条路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "northwind_traveler_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_northwind_traveler_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_northwind_traveler_feet" }
]
```

**特殊效果（设计预留）**：免疫因长途旅行产生的exhaustion。

---

## 套装十四：虚空行者（Void Walker）护甲

> *"如果世界是一个牢笼，那虚空就是牢笼外的草原。"*

**套装主题**：虚空探索者结社「边界之外」的传承装备。这个结社在一百五十年前的一次虚空探索中全员迷失，但他们的装备因为长期暴露在虚空能量中而获得了扭曲现实的能力。

**历史渊源**：边界之外的创始人「第一行者」Null，是一位为了寻找"世界之外的真相"而自愿走入虚空的疯狂法师。

---

### 14.1 虚空行者虚空兜帽（Void Walker Void Hood）

```gdscript
item_id = "armor_void_walker_head"
display_name = "虚空行者虚空兜帽"
description = "一顶由虚空物质与扭曲布料编织而成的奇异兜帽，兜帽呈现出绝对的黑色——不是普通的那种黑，是连光都无法逃脱的绝对黑。兜帽没有固定的形状——它会随虚空的波动而不断变化，仿佛兜帽本身就是一团凝固的虚空。\n\n这顶兜帽是Null在第一次踏入虚空时，从自己的影子上撕下的一块制作的。他说："虚空不是没有，虚空是'不同'——它有不同的规则，不同的逻辑，不同的存在方式。这顶兜帽让我能够在虚空中'存在'，而不被虚空吞噬。"他在兜帽内侧刻下了虚空的第一条法则："在虚空中，不存在就是存在，存在就是不存在。"\n\n兜帽会在佩戴者进入虚空或类似环境时自动激活——提供完全的虚空适应性（免疫虚空伤害，且可以在虚空中正常呼吸和行动）。且兜帽可以让佩戴者"感知"到虚空裂缝的位置——30 尺内任何虚空裂缝或传送门会被自动感知。一位后来的虚空探索者在描述这顶兜帽时说："它不是兜帽，它是'虚空的皮肤'——穿上它，你就成为了虚空的一部分。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "void_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_walker_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_void_walker_head" }
]
```

**特殊效果（设计预留）**：免疫虚空伤害，且可以感知 30 尺内的虚空裂缝。

---

### 14.2 虚空行者虚空长袍（Void Walker Void Robe）

```gdscript
item_id = "armor_void_walker_body"
display_name = "虚空行者虚空长袍"
description = "一件由虚空物质与扭曲丝线编织而成的奇异长袍，袍身不断有微型虚空漩涡在流动——不是装饰，而是真正的虚空被封印在了丝线中。长袍没有固定的颜色——它会随周围的光线变化而变化，在明亮处呈现深紫色，在黑暗处呈现绝对黑。长袍非常轻盈——因为虚空几乎没有重量。\n\n这件长袍是Null在虚空探索中，从一块漂浮的虚空碎片中提取的物质编织的。他说："虚空不是空的，虚空是'满的'——充满了我们无法理解的东西。这件长袍让我能够携带一小块虚空，而不被它吞噬。"他在长袍内侧缝了七道"虚空封印"——每一道封印都代表一种虚空危险的防护措施。\n\n长袍会在佩戴者受到魔法攻击时自动激活——每日一次，可以将一次 directed 法术完全吸收（如同 spell turning 的单次版），并将其转化为虚空能量储存在长袍中。储存的能量可以在之后释放—— bonus action，释放储存的法术（以虚空能量的形式，伤害类型变为 force）。一位后来的法师在描述这件长袍时说："它不是长袍，它是'虚空的容器'——它可以吞噬魔法，也可以释放魔法。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "void_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_void_walker_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_void_walker_body" },
    { attribute_id = "resistance_force", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_void_walker_body" }
]
```

**特殊效果（设计预留）**：每日一次，吸收一个 directed 法术并转化为虚空能量释放。

---

### 14.3 虚空行者扭曲护手（Void Walker Distortion Gauntlets）

```gdscript
item_id = "armor_void_walker_hands"
display_name = "虚空行者扭曲护手"
description = "一对由虚空物质与扭曲金属锻造的奇异护手，护手表面不断有微弱的扭曲波纹——那不是光线折射，而是"现实"在被轻微修改。护手佩戴时，周围的物体会偶尔出现短暂的"错位"——例如桌子腿会偏移几寸，或者自己的手指会突然变长。\n\n这对手套是Null在虚空探索中，从自己的双手上脱落的皮肤碎片制作的。他说："虚空改变了我的身体，但我不害怕——改变是理解的开始。我将脱落的皮肤收集起来，与虚空物质结合，制作成了这对手套。它们让我能够'触碰'虚空，而不被虚空吞噬。"\n\n手套会在佩戴者进行徒手攻击或接触性法术时自动激活——每日三次，可以释放一道虚空扭曲（5 尺射程 1D10 force damage，目标须通过 DC14 体质豁免，失败则 teleport 随机 1D6×5 尺）。且在进行奥术检定时，检定 +2（虚空的智慧）。Null说："虚空不是敌人，它是另一种现实——学会与它互动，你就能获得超越常规的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "light_armor", "void_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_walker_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_walker_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击附加1D6 force damage，且可以将目标随机传送。

---

### 14.4 虚空行者虚空步靴（Void Walker Voidstep Boots）

```gdscript
item_id = "armor_void_walker_feet"
display_name = "虚空行者虚空步靴"
description = "一对由虚空物质与扭曲皮革制成的奇异软靴，靴底没有纹路——因为它们不需要接触地面。行走时，软靴会在地面之上悬浮约半寸，不会留下任何脚印。靴筒内侧刻有七道微型虚空传送符文。\n\n这对软靴是Null在虚空探索中，从一块漂浮的虚空碎片中提取的物质制作的。他说："在虚空中，距离没有意义——一步可以跨越千里，也可以原地踏步。这对软靴让我能够在现实世界中体验虚空的'距离感'。"他在靴筒内侧刻下了七道传送符文——每一道符文代表一种不同的"距离折叠"方式。\n\n软靴的特殊效果是：每日一次，可以激活「虚空步」——踏入虚空并瞬间移动至 30 尺内任何位置（如同 dimension door 的短距离版，但不会引发虚空的不稳定）。且在进行隐匿检定时，检定 +3（虚空步不留痕迹）。一位后来的探索者在描述这对软靴时说："穿着它们，我感觉自己不是在走路，我是在'折叠'距离——每一步都是一次微型传送。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "void_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_walker_feet" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_void_walker_feet" }
]
```

**特殊效果（设计预留）**：每日一次，短距离传送（30尺内任意位置）。

---

## 套装十五：自然之语（Nature's Whisper）护甲

> *"我听不见人类的声音，但我能听见树木的心跳。这不是残疾，而是天赋。"*

**套装主题**：远古德鲁伊结社「翠绿之环」的传承装备。这个结社在五百年前因人类扩张而隐入深山，但他们的装备被森林本身保护着。

**历史渊源**：翠绿之环的创始人「第一聆听者」希尔瓦娜斯，是一位能够听懂所有生物语言的聋女。

---

### 15.1 自然之语叶冠（Nature's Whisper Leaf Crown）

```gdscript
item_id = "armor_natures_whisper_head"
display_name = "自然之语叶冠"
description = "一顶由千年古树的枝叶与藤蔓编织而成的有机头冠，头冠表面不断有新叶在萌发和老叶在脱落——它是一个活着的生态系统，与佩戴者的生命力相连。头冠的颜色随季节变化——春天嫩绿，夏天深绿，秋天金黄，冬天雪白。\n\n这顶叶冠是希尔瓦娜斯在创建翠绿之环时，从她最喜爱的千年古树上取下的枝条编织的。她说："这棵树已经在这里站了一千年，它听到了所有风吹过树叶的声音。我要将它的一部分带走，让它继续聆听。"她在头冠上嫁接了十二种不同的植物——每一种都代表一种不同的自然之力。\n\n叶冠会在佩戴者进入森林时自动激活——所有动物不会对佩戴者表现出敌意（自然之友的初步效果），且可以感知到方圆 1 里内所有植物的状态（健康、枯萎、受威胁）。一位后来的德鲁伊在描述这顶叶冠时说："它不是头冠，它是'森林的耳朵'——戴上它，你就能听到树木的心跳、花朵的呼吸、根系的低语。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "natures_whisper_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_natures_whisper_head" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_natures_whisper_head" }
]
```

**特殊效果（设计预留）**：在森林中，所有动物不会主动攻击，且可以感知植物状态。

---

### 15.2 自然之语树皮甲（Nature's Whisper Bark Mail）

```gdscript
item_id = "armor_natures_whisper_body"
display_name = "自然之语树皮甲"
description = "一件由千年古树的树皮与藤蔓编织而成的有机铠甲，铠甲表面覆盖着一层真正的苔藓和微型蕨类植物——它们与铠甲形成了一种奇妙的共生关系，从环境中获取水分和养分，同时释放出清新的氧气。铠甲的硬度随季节变化——夏天最坚硬（树皮充满汁液），冬天最脆弱（树皮干燥收缩）。\n\n这件树皮甲是希尔瓦娜斯在她最喜爱的千年古树"自愿"献出的树皮上制作的。那棵古树说："我已经活了一千年，我的树皮比任何金属都坚固。但我不需要它来保护我自己，我需要它来保护我的朋友们。"希尔瓦娜斯将树皮与藤蔓编织成了这件铠甲，成为了翠绿之环最神圣的守护装备。\n\n树皮甲会在佩戴者受到攻击时自动激活——树皮会瞬间硬化，提供额外的 +2 AC（自然的防御，持续 1 回合，每日三次）。且铠甲可以让佩戴者在森林中完全隐形——不是魔法隐身，而是与周围环境的完美融合（如同 pass without trace 的森林版）。一位后来的德鲁伊说："穿着这件铠甲，我不是在森林中行走，我是在'成为'森林的一部分。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "natures_whisper_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_natures_whisper_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_natures_whisper_body" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_natures_whisper_body" }
]
```

**特殊效果（设计预留）**：在森林中，AC额外+1，且可以完美融入环境。

---

### 15.3 自然之语根须手套（Nature's Whisper Root Gloves）

```gdscript
item_id = "armor_natures_whisper_hands"
display_name = "自然之语根须手套"
description = "一对由古树根须与藤蔓编织而成的有机手套，手套表面不断有微型根系在生长——它们会随佩戴者的意愿延伸或收缩，可以用来攀爬、抓握、甚至攻击。手套的指尖各嵌有一枚微型种子——每一枚种子都可以在短时间内生长成一种工具或武器。\n\n这对手套是希尔瓦娜斯在她最喜爱的千年古树的根系中提取的物质制作的。她说："根是树木的手——它们深入大地，寻找水分和养分，同时也与其他的根相连，形成一个巨大的地下网络。这对手套让我能够'触碰'这个网络，与自然对话。"她在手套的指尖嵌入了十二种不同的种子——从能够瞬间生长的荆棘种子，到能够治愈伤口的药草种子。\n\n手套会在佩戴者进行自然相关检定时自动激活——每日三次，可以从指尖释放一种植物（选择：荆棘：5 尺内困难地形；藤蔓：攀爬绳索；药草：恢复 2D8 HP）。且在进行自然检定时，检定 +3（根须的智慧）。希尔瓦娜斯说："自然不是敌人，它是朋友——学会与它对话，你就能获得无限的帮助。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "natures_whisper_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_natures_whisper_hands" },
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_natures_whisper_hands" }
]
```

**特殊效果（设计预留）**：可以操控植物生长（困难地形、攀爬辅助、或治疗）。

---

### 15.4 自然之语根系靴（Nature's Whisper Root Boots）

```gdscript
item_id = "armor_natures_whisper_feet"
display_name = "自然之语根系靴"
description = "一对由古树根系与藤蔓编织而成的有机软靴，靴底没有纹路——因为它们不需要纹路。行走时，靴底会伸出微型根须，抓住地面，提供完美的抓地力。在软土地上，根须会深入地下，让佩戴者几乎不可能被推倒；在硬地面上，根须会缠绕住地面的缝隙，提供额外的稳定性。\n\n这对软靴是希尔瓦娜斯在她最喜爱的千年古树的根系中提取的物质制作的。她说："根不需要移动，它们只需要抓住——抓住大地，抓住水分，抓住生命。这对软靴让我能够像树一样站立，像根一样稳固。"她在靴底嫁接了多种不同的根系——每一种都适应不同的地面类型。\n\n软靴会在佩戴者站立不动时自动激活——根须会深入地下，提供免疫被击倒（prone）、推离（shove）和强制移动效果（扎根之力）。且在进行平衡或攀爬检定时，检定 +3（根系的稳定）。希尔瓦娜斯说："树不会因为风大而倒下，因为它的根足够深。学会像树一样扎根，你就能在任何风暴中屹立不倒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "natures_whisper_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_natures_whisper_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_natures_whisper_feet" }
]
```

**特殊效果（设计预留）**：站立不动时免疫被推倒/推离，且在自然地形上移动力+5。

---

*套装 11–15 护甲部分完结 · 共 20 件护甲装备*

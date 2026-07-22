# 传奇装备套装设计文档（套装 21–30：中甲）

> 10 套中甲套装，共 40 件护甲装备，覆盖 head / body / hands / feet。

---

## 套装二十一：银月游侠（Silvermoon Ranger）

> *"精灵不需要城墙，他们有森林。不需要军队，他们有游侠。"*

**套装主题**：高等精灵王国「银月城」边境游侠部队的装备。这些游侠在月光下巡逻千年，他们的装备被月光祝福，获得了与森林和星辰沟通的能力。集齐四件时，穿戴者获得「月影之姿」——在月光下隐匿和射击能力大幅提升。

**历史渊源**：银月游侠的指挥官「月影」希尔瓦娜斯（另一位同名者），是一位在满月之夜诞生的精灵。她说："我不是在白天出生的，所以白天的规则不适用我。"

---

### 21.1 银月游侠头冠

```gdscript
item_id = "armor_silvermoon_ranger_head"
display_name = "银月游侠头冠"
description = "一顶由银月城圣树的树皮与月光银丝编织而成的轻盈头冠，头冠上镶嵌着三颗微型月光石。头冠没有面甲，因为它不是为了防御——它是为了让穿戴者能够「听见」月光的声音。\n\n希尔瓦娜斯在编织这头冠时，用了一整年的月光。她说："月光不是光，它是'低语'——森林的低语，星辰的低语，远方的低语。"她将月光凝结成丝线，与圣树树皮编织在一起。\n\n这头冠的特殊效果是：在月光下，穿戴者的感知范围翻倍，且可以「听见」300 尺内的任何移动（如同 tremorsense，但只对月光照射到的区域有效）。希尔瓦娜斯说："月光照到的地方，就是我的领地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "silvermoon_ranger_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_silvermoon_ranger_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_silvermoon_ranger_head" }
]
```

**2件套（设计预留）**：月光下隐匿检定 +3；`max_dex_bonus` 视为 +1（中甲允许更高敏捷）。
**4件套（设计预留）**：月光下远程攻击伤害 +1D6；从隐匿状态射出的第一箭自动暴击；每日一次「月影箭」：射程内任一目标，视为隐形射击（无需隐匿检定）。

---

### 21.2 银月游侠链甲

```gdscript
item_id = "armor_silvermoon_ranger_body"
display_name = "银月游侠链甲"
description = "一副由银月城圣树的藤蔓与月光银环串联而成的链甲，链甲表面覆盖着一层会自动变色的苔藓——白天是绿色，夜晚是银色，月光下会发出微弱的荧光。\n\n希尔瓦娜斯在编织这副链甲时，将整棵圣树的藤蔓都收集了起来。她说："圣树活了五千年，它的藤蔓记得五千年的故事。我要让这些故事成为我的铠甲。"\n\n这副链甲的特殊效果是：在森林中，链甲上的苔藓会自动与周围环境融为一体，提供额外的 +2 AC（ camouflage 效果）。希尔瓦娜斯说："最好的铠甲不是最硬的，而是最不可见的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "silvermoon_ranger_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_silvermoon_ranger_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_silvermoon_ranger_body" }
]
```

---

### 21.3 银月游侠手套

```gdscript
item_id = "armor_silvermoon_ranger_hands"
display_name = "银月游侠手套"
description = "一对由银月城圣树的叶片与月光银丝编织而成的手套，指尖涂有一层「月光树脂」——一种能够让箭矢在飞行中隐形的奇异物质。\n\n希尔瓦娜斯在编织这对手套时，收集了七百年的月光树脂。她说："这种树脂不是普通的胶，它是'光的陷阱'——它让箭矢在飞行中吸收周围的光线，变得不可见。"\n\n这对手套的特殊效果是：穿戴者射出的箭矢在月光下自动隐形（目标无法通过视觉察觉箭矢，只能通过听觉或魔法感知）。希尔瓦娜斯用这对手套射杀了一个恶魔领主，恶魔领主在临死前说："我没有看到箭，我只看到了月光——然后月光变成了死亡。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "silvermoon_ranger_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_silvermoon_ranger_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_silvermoon_ranger_hands" }
]
```

---

### 21.4 银月游侠软靴

```gdscript
item_id = "armor_silvermoon_ranger_feet"
display_name = "银月游侠软靴"
description = "一对由银月城圣树的落叶与月光银丝编织而成的软靴，靴底覆盖着一层「无声苔藓」——一种能够吸收所有声音的奇异植物。\n\n希尔瓦娜斯在编织这对软靴时，收集了所有在月光下掉落的树叶。她说："月光下的树叶不会发出声音，因为它们知道——声音是猎人的敌人。"\n\n这对软靴的特殊效果是：穿戴者在森林中移动时不会发出任何声音（如同 pass without trace），且不会留下任何脚印。希尔瓦娜斯说："最好的游侠不是最快的，而是最安静的——安静到连风都不会注意到你。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "silvermoon_ranger_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_silvermoon_ranger_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_silvermoon_ranger_feet" }
]
```

---

## 套装二十二：沙漠蝎刺（Desert Scorpion）

> *"沙漠不是空无一物，它是充满了等待的猎手。"*

**套装主题**：沙漠游牧部落「蝎尾」的装备。这个部落在最炎热的沙漠中生存了千年，他们的装备适应了极端的高温和干旱，带有剧毒和耐热的能力。集齐四件时，穿戴者获得「蝎王之躯」——免疫高温和毒素，且在沙漠中几乎无法被追踪。

**历史渊源**：蝎尾部落的酋长「蝎后」克里奥帕特拉（另一位同名者），是一位能够与毒蝎对话的沙漠女巫。她说："蝎子不是敌人，它们是老师——教给你如何在最恶劣的环境中生存。"

---

### 22.1 沙漠蝎刺头巾

```gdscript
item_id = "armor_desert_scorpion_head"
display_name = "沙漠蝎刺头巾"
description = "一条由沙漠毒蝎的蜕皮与耐热沙丝编织而成的头巾，头巾表面有类似蝎壳的纹理。头巾可以完全覆盖头部和面部，只露出双眼——那不是弱点，而是诱饵，让敌人误以为找到了攻击目标。\n\n克里奥帕特拉在编织这条头巾时，收集了一千只毒蝎的蜕皮。她说："蜕皮不是死亡，它是'重生'——每一次蜕皮，蝎子都变得更加强大。"\n\n这条头巾的特殊效果是：在炎热环境中（超过 40°C），头巾会释放一种「冷却毒素」——不是伤害穿戴者，而是降低体温，防止中暑。但在寒冷环境中，头巾会释放「加热毒素」——让穿戴者保持温暖。克里奥帕特拉说："最好的衣服不是阻挡环境，而是与环境对话。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "desert_scorpion_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_desert_scorpion_head" },
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_desert_scorpion_head" }
]
```

**2件套（设计预留）**：`resistance_fire` +10，`resistance_poison` +10。
**4件套（设计预留）**：免疫 heat exhaustion（中暑）和 dehydration（脱水）；沙漠中隐匿检定 +5；近战攻击附加 1D6 poison；每日一次「蝎尾突袭」：从隐匿状态攻击时，目标须通过 DC16 体质豁免，失败则 paralyzed 1 回合（神经毒素）。

---

### 22.2 沙漠蝎刺鳞甲

```gdscript
item_id = "armor_desert_scorpion_body"
display_name = "沙漠蝎刺鳞甲"
description = "一副由沙漠毒蝎的甲壳与耐热皮革拼接而成的鳞甲，每一片鳞片都来自不同的毒蝎，因此呈现出深浅不一的黄褐色。鳞甲表面有一层天然的蜡质，可以反射阳光，减少热量吸收。\n\n克里奥帕特拉在制作这副鳞甲时，走遍了整个沙漠，收集了所有她遇到的毒蝎的甲壳。她说："每一片甲壳都是一个故事——一个关于生存的故事。"\n\n这副鳞甲的特殊效果是：在阳光下，鳞甲的蜡质层会反射阳光，使穿戴者在远距离难以被察觉（如同天然的 camouflage）。但在近距离，蜡质层会散发出一种「蝎子的气息」——让大多数野兽自动回避。克里奥帕特拉说："沙漠中最强大的不是狮子，是蝎子——因为所有生物都知道，不要惹蝎子。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "desert_scorpion_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_desert_scorpion_body" },
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_desert_scorpion_body" }
]
```

---

### 22.3 沙漠蝎刺手套

```gdscript
item_id = "armor_desert_scorpion_hands"
display_name = "沙漠蝎刺手套"
description = "一对由沙漠毒蝎的毒腺与耐热皮革制成的手套，指尖各嵌有一根可伸缩的毒刺。握紧拳头时，毒刺会从指尖伸出；张开手掌时，毒刺会缩回。\n\n克里奥帕特拉在制作这对手套时，从一百只毒蝎中提取了毒腺。她说："毒蝎的毒不是武器，它是'语言'——用来告诉世界'不要碰我'。现在，这种语言也属于我了。"\n\n这对手套的特殊效果是：穿戴者可以用毒刺进行徒手攻击（1D6 piercing + 2D6 poison），且可以将毒液涂抹到武器上（持续 1 小时或 3 次攻击）。但缺点是，如果手套被损坏，毒液可能泄漏，穿戴者必须通过 DC14 体质豁免，失败则中毒。克里奥帕特拉说："力量总是有代价的，只是看你是否愿意支付。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "desert_scorpion_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_desert_scorpion_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_desert_scorpion_hands" }
]
```

---

### 22.4 沙漠蝎刺软靴

```gdscript
item_id = "armor_desert_scorpion_feet"
display_name = "沙漠蝎刺软靴"
description = "一对由沙漠毒蝎的足底软皮与耐热皮革制成的软靴，靴底有细密的纹路——不是为防滑，而是为了在沙地上行走时不留下任何可以被追踪的痕迹。\n\n克里奥帕特拉在制作这对软靴时，研究了毒蝎在沙地上行走的方式。她说："毒蝎走路时，沙粒会自己回到原位，掩盖足迹。我要学会这种方式。"\n\n这对软靴的特殊效果是：在沙地上移动时不会留下任何足迹（即使是魔法追踪也无法发现），且可以在流沙上正常行走（如同 solid ground）。克里奥帕特拉说："沙漠是猎人的盟友——只要你懂得与它合作。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "desert_scorpion_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_desert_scorpion_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_desert_scorpion_feet" }
]
```

---

## 套装二十三：丛林猎豹（Jungle Panther）

> *"丛林不是绿色的海洋，它是无数猎手的竞技场。最快的猎手生存，最慢的成为食物。"*

**套装主题**：热带雨林部落「豹影」的装备。这个部落的猎手模仿猎豹的方式生活和战斗，他们的装备由雨林中最稀有的材料制成，赋予了穿戴者猎豹般的速度和伏击能力。集齐四件时，穿戴者获得「豹王之躯」——可以在短时间内爆发出猎豹的速度，且从高处跳跃时攻击伤害翻倍。

**历史渊源**：豹影部落的最强猎手「猎豹之心」塞克美特，是一位据说能够与猎豹灵魂融合的部落巫医。她说："我不是在模仿猎豹，我是在成为猎豹。"

---

### 23.1 丛林猎豹头冠

```gdscript
item_id = "armor_jungle_panther_head"
display_name = "丛林猎豹头冠"
description = "一顶由黑豹头骨与雨林藤蔓编织而成的头冠，头冠上镶嵌着两颗真正的黑豹眼睛——那不是装饰，而是被封印的豹魂。戴上头冠时，穿戴者会感受到豹魂的存在，它会低语，会警告，会兴奋。\n\n塞克美特在制作这顶头冠时，猎杀了一只活了五十年的黑豹——不是为杀戮，而是因为黑豹在临终前选择了她。黑豹说："我的时间到了，但我的灵魂还可以继续奔跑。带上我的眼睛，让我通过你继续狩猎。"\n\n这顶头冠的特殊效果是：在黑暗中，穿戴者获得黑豹的「夜视」——可以看清 60 尺内的一切，如同 daylight。但缺点是，白天时穿戴者的视力会略微下降（过于敏感的夜视在强光下会不适）。塞克美特说："豹属于夜晚，你也属于夜晚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "jungle_panther_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_jungle_panther_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_jungle_panther_head" }
]
```

**2件套（设计预留）**：`movement_speed` +10，先攻检定 +2。
**4件套（设计预留）**：每日三次「猎豹冲刺」： bonus action，本回合移动力翻倍；从高处（10尺以上）跳下的第一次攻击伤害翻倍；从隐匿状态攻击后，可以免费进行一次 disengage（猎豹的撤离本能）。

---

### 23.2 丛林猎豹皮甲

```gdscript
item_id = "armor_jungle_panther_body"
display_name = "丛林猎豹皮甲"
description = "一副由黑豹皮毛与雨林树叶编织而成的皮甲，皮甲表面有类似豹纹的图案——那不是画上去的，而是真正的豹毛与树叶自然形成的纹理。在丛林中，这副皮甲几乎无法被肉眼察觉。\n\n塞克美特在制作这副皮甲时，将整只黑豹的皮毛都缝入了皮甲。她说："皮毛不只是保暖，它是'伪装'——让穿戴者成为丛林的一部分。"\n\n这副皮甲的特殊效果是：在丛林/雨林环境中，穿戴者获得 +3 AC（ camouflage ），且敌人必须通过 DC16 感知检定才能发现穿戴者（即使在近距离）。塞克美特说："在丛林中，最好的防御是'不存在'。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "jungle_panther_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "armor_jungle_panther_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_jungle_panther_body" }
]
```

---

### 23.3 丛林猎豹手套

```gdscript
item_id = "armor_jungle_panther_hands"
display_name = "丛林猎豹手套"
description = "一对由黑豹爪子与雨林藤蔓制成的手套，指尖嵌有可伸缩的豹爪。握紧拳头时，豹爪会伸出；张开手掌时，豹爪会缩回。\n\n塞克美特在制作这对手套时，将黑豹的四只爪子都嵌入了手套。她说："爪子不只是武器，它们是'工具'——用来攀爬，用来抓住猎物，用来在树上行走。"\n\n这对手套的特殊效果是：穿戴者可以用豹爪进行徒手攻击（1D8 slashing），且可以在树上以正常速度移动（如同蜘蛛攀爬）。塞克美特说："在丛林中，地面是给猎物走的，树才是给猎手走的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "jungle_panther_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_jungle_panther_hands" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_jungle_panther_hands" }
]
```

---

### 23.4 丛林猎豹软靴

```gdscript
item_id = "armor_jungle_panther_feet"
display_name = "丛林猎豹软靴"
description = "一对由黑豹足底软皮与雨林藤蔓制成的软靴，靴底有肉垫状的纹路——模仿猎豹的足底结构，可以在任何表面上无声行走。\n\n塞克美特在制作这对软靴时，仔细研究了猎豹足底的每一道纹路。她说："猎豹的脚底不是平的，它有纹路——不是为了防滑，而是为了'无声'。纹路让脚底与地面接触时不会发出声音。"\n\n这对软靴的特殊效果是：在任何表面上移动都不会发出声音（如同 pass without trace），且可以在湿滑表面（苔藓、湿树叶）上正常速度移动。塞克美特说："猎豹的脚步比落叶还轻，你的也应该如此。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "jungle_panther_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_jungle_panther_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_jungle_panther_feet" }
]
```

---

## 套装二十四：高山雄鹰（Highland Eagle）

> *"最高的山峰不是给凡人爬的，是给雄鹰筑巢的。"*

**套装主题**：高山部落「鹰巢」的装备。这个部落生活在世界最高的山峰上，他们的装备适应了极端的高空和寒冷，赋予了穿戴者鹰眼般的视野和高空俯冲的攻击能力。集齐四件时，穿戴者获得「鹰王之躯」——可以从高空俯冲攻击，且视野范围大幅提升。

**历史渊源**：鹰巢部落的最强战士「鹰眼」荷鲁斯（另一位同名者），是一位据说能够与雄鹰灵魂融合的部落萨满。他说："我不是在模仿雄鹰，我是在让雄鹰成为我的一部分。"

---

### 24.1 高山雄鹰头冠

```gdscript
item_id = "armor_highland_eagle_head"
display_name = "高山雄鹰头冠"
description = "一顶由雄鹰头骨与高山羽毛编织而成的头冠，头冠两侧各有一支巨大的鹰羽——那不是装饰，而是真正的雄鹰翅膀羽毛，可以让穿戴者感受到风的流动。\n\n荷鲁斯在制作这顶头冠时，与一只活了八十年的雄鹰达成了契约。雄鹰说："我的眼睛已经看不清了，但我的羽毛还记得风的方向。带上它们，让我通过你继续飞翔。"\n\n这顶头冠的特殊效果是：穿戴者的视野范围翻倍，且可以看清 1 里外的细节（如同望远镜）。但缺点是，穿戴者对强光更加敏感（鹰眼的副作用），在强光下攻击检定 -1。荷鲁斯说："看得远的人，也必须承受看得太多的代价。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "highland_eagle_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_highland_eagle_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_highland_eagle_head" }
]
```

**2件套（设计预留）**：感知检定 +3，免疫 altitude sickness（高原反应）。
**4件套（设计预留）**：从高处（20尺以上）俯冲的第一次攻击伤害翻倍；免疫 falling damage（风中滑翔）；每日一次「鹰眼锁定」：30 尺内一个目标被「标记」，接下来 3 回合内对该目标的攻击检定 +3。

---

### 24.2 高山雄鹰鳞甲

```gdscript
item_id = "armor_highland_eagle_body"
display_name = "高山雄鹰鳞甲"
description = "一副由雄鹰羽毛与高山岩石鳞片拼接而成的鳞甲，每一片羽毛都经过特殊处理，可以在高空中保持体温。鳞甲的重量极轻——比普通的皮甲还轻，但防御力却更高。\n\n荷鲁斯在制作这副鳞甲时，收集了鹰巢周围所有死去的雄鹰的羽毛。他说："每一根羽毛都是一次飞翔的记忆，我要让这些记忆保护我。"\n\n这副鳞甲的特殊效果是：在高空（超过 3000 米）环境中，鳞甲的羽毛会自动展开，形成一层保暖层，防止体温流失。同时，展开的羽毛可以增加空气阻力，让穿戴者从高处下落时速度减半（免疫 falling damage 的前提）。荷鲁斯说："雄鹰不怕高，因为它有羽毛。我也不怕高，因为我有雄鹰的羽毛。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "highland_eagle_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_highland_eagle_body" },
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_highland_eagle_body" }
]
```

---

### 24.3 高山雄鹰手套

```gdscript
item_id = "armor_highland_eagle_hands"
display_name = "高山雄鹰手套"
description = "一对由雄鹰爪子与高山藤蔓制成的手套，指尖嵌有可伸缩的鹰爪。握紧拳头时，鹰爪会伸出；张开手掌时，鹰爪会缩回。\n\n荷鲁斯在制作这对手套时，将雄鹰的四只爪子都嵌入了手套。他说："鹰爪不只是武器，它们是'工具'——用来抓住岩石，用来撕开猎物，用来在悬崖上攀爬。"\n\n这对手套的特殊效果是：穿戴者可以用鹰爪进行徒手攻击（1D8 slashing），且可以在悬崖上以正常速度攀爬（如同蜘蛛攀爬）。荷鲁斯说："在山上，地面是平的，悬崖才是路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "highland_eagle_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_highland_eagle_hands" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_highland_eagle_hands" }
]
```

---

### 24.4 高山雄鹰软靴

```gdscript
item_id = "armor_highland_eagle_feet"
display_name = "高山雄鹰软靴"
description = "一对由雄鹰足底软皮与高山藤蔓制成的软靴，靴底有类似鹰爪的纹路——不是为了行走，而是为了抓住岩石。在陡峭的斜坡上，这双软靴可以让穿戴者像雄鹰一样稳稳站立。\n\n荷鲁斯在制作这对软靴时，研究了雄鹰如何在悬崖上站立。他说："雄鹰的脚不是为走路的，是为抓住的。它的脚趾可以弯曲成任何角度，抓住任何形状。"\n\n这对软靴的特殊效果是：在陡峭地形（超过 45 度）上，穿戴者可以正常站立和移动，不会滑落。且从高处跳下时，可以展开靴筒内的隐藏羽毛，减缓下落速度（免疫 falling damage）。荷鲁斯说："雄鹰从高处跳下不是为了坠落，是为了飞翔。我也一样。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "highland_eagle_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_highland_eagle_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_highland_eagle_feet" }
]
```

---

## 套装二十五：海盗船长（Pirate Captain）

> *"大海不属于任何人，但它尊重那些敢与它搏斗的人。"*

**套装主题**：传奇海盗团「黑帆」的装备。这个海盗团在七海上横行了三代，他们的装备吸收了海洋的狂野气息，赋予了穿戴者海战和航海的特化能力。集齐四件时，穿戴者获得「海霸王之躯」——在水上战斗力大幅提升，且可以召唤海怪协助。

**历史渊源**：黑帆海盗团的最后一位船长「海霸王」黑胡子（另一位同名者），是一位能够与海洋生物对话的疯狂海盗。他说："我不是海盗，我是海洋的客人——只是我不付账。"

---

### 25.1 海盗船长三角帽

```gdscript
item_id = "armor_pirate_captain_head"
display_name = "海盗船长三角帽"
description = "一顶由黑帆海盗团的旗帜与深海鲨鱼皮制成的三角帽，帽檐上镶嵌着三颗从沉船宝藏中取出的宝石。帽子内部缝有「黑帆守则」——以血写就："抢富不抢贫，杀兵不杀民，留酒不留命。"\n\n黑胡子在制作这顶帽子时，将黑帆海盗团三代船长的血都滴入了帽子的内衬。他说："这顶帽子不只是帽子，它是'传承'——三代船长的意志都在里面。"\n\n这顶帽子的特殊效果是：在海上（船只或岛屿），穿戴者的威吓和说服检定 +3（船长的威严）。且可以「感知」到 1 里内的任何船只（通过帽子的宝石微微发光指示方向）。黑胡子说："船长不需要地图，他只需要帽子。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "pirate_captain_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_pirate_captain_head" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_pirate_captain_head" }
]
```

**2件套（设计预留）**：水上（船只或涉水）攻击检定 +2；游泳速度 = 步行速度。
**4件套（设计预留）**：水上免疫 prone（船只摇晃不影响）；登船攻击伤害 +1D6；每日一次「召唤海怪」：召唤一只巨型章鱼或鲨鱼协助战斗（持续 1 分钟）。

---

### 25.2 海盗船长皮甲

```gdscript
item_id = "armor_pirate_captain_body"
display_name = "海盗船长皮甲"
description = "一副由深海巨鲨的皮与黑帆海盗团的旗帜拼接而成的皮甲，皮甲表面有类似鲨鱼皮的纹理，可以减少水流阻力。皮甲的背部缝有一面小型的黑帆——不是为装饰，而是为了让穿戴者在落水时能够更快地浮出水面。\n\n黑胡子在制作这副皮甲时，猎杀了一条长达三十尺的巨鲨。他说："这条鲨不是普通的鲨，它是'海洋的贵族'——只有贵族的皮才配得上船长。"\n\n这副皮甲的特殊效果是：在水中，皮甲的鲨鱼皮纹理可以减少水流阻力，使游泳速度翻倍。且当穿戴者落水时，背部的黑帆会自动展开，提供浮力（免疫 drowning 的疲劳效果）。黑胡子说："船长可以沉船，但不能沉自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "pirate_captain_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_pirate_captain_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_pirate_captain_body" }
]
```

---

### 25.3 海盗船长手套

```gdscript
item_id = "armor_pirate_captain_hands"
display_name = "海盗船长手套"
description = "一对由深海巨蟹的钳子与海盗绳索制成的手套，手掌部分覆盖着一层粗糙的蟹壳——不是为了美观，而是为了在湿滑的甲板和绳索上保持抓握力。\n\n黑胡子在制作这对手套时，从一只巨型巨蟹身上取下了两只钳子。他说："巨蟹的钳子不是为了攻击，是为了'抓住'——抓住猎物，抓住岩石，抓住绳索。我也要学会抓住。"\n\n这对手套的特殊效果是：在湿滑的表面上（甲板、绳索、冰面），穿戴者的攀爬和抓握检定 +5，且不会失手滑落。且可以用蟹壳进行徒手攻击（1D6 bludgeoning + 1D4 slashing）。黑胡子说："船长的手不是用来握剑的，是用来握舵的。但必要时，它也可以用来握敌人的喉咙。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "pirate_captain_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_pirate_captain_hands" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_pirate_captain_hands" }
]
```

---

### 25.4 海盗船长软靴

```gdscript
item_id = "armor_pirate_captain_feet"
display_name = "海盗船长软靴"
description = "一对由深海章鱼的吸盘与海盗皮革制成的软靴，靴底有类似章鱼吸盘的纹路——不是为了防滑，而是为了在摇晃的甲板上保持绝对稳定。\n\n黑胡子在制作这对软靴时，从一只巨型章鱼身上取下了吸盘。他说："章鱼的吸盘不是为了走路，是为了'吸附'——吸附在任何表面上，不管多滑。我要学会这种能力。"\n\n这对软靴的特殊效果是：在摇晃的表面上（船只甲板、地震地面），穿戴者免疫 prone（击倒），且移动力不受惩罚。且可以在垂直的表面（船帆、桅杆）上正常行走（如同蜘蛛攀爬）。黑胡子说："船长不需要地板，他可以在任何地方站立。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "pirate_captain_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_pirate_captain_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_pirate_captain_feet" }
]
```

---

## 套装二十六：赏金猎人（Bounty Hunter）

> *"每个人都有自己的价格。我的工作是找到那个人，然后收取那个价格。"*

**套装主题**：荒野赏金猎人公会「锁链」的装备。这个公会专门追捕通缉犯，他们的装备赋予了穿戴者追踪、束缚和审讯的特化能力。集齐四件时，穿戴者获得「猎手之躯」——可以标记目标并持续追踪，且对被标记目标造成额外伤害。

**历史渊源**：锁链公会的创始人「锁链大师」杰西，是一位被通缉犯杀害了全家的孤独猎手。他说："我不是为了钱而狩猎，我是为了确保没有人再经历我所经历的。"

---

### 26.1 赏金猎人面罩

```gdscript
item_id = "armor_bounty_hunter_head"
display_name = "赏金猎人面罩"
description = "一张由锁链公会的标志锁链与通缉犯名单皮革制成的面罩，面罩上刻有所有被成功追捕的通缉犯的名字。每一个名字都是一道刻痕，刻痕越多，面罩越有价值。\n\n杰西在制作这张面罩时，将第一张通缉犯的皮革鞣制后缝入了面罩。他说："这张面罩不是遮挡我的脸的，它是'记录'——记录我的每一次成功。"\n\n这张面罩的特殊效果是：穿戴者可以「感知」到 60 尺内任何被通缉或有悬赏的生物（面罩会微微发热）。且可以通过面罩「读取」一个目标的「恐惧」——知道目标最害怕什么（社交检定 +3 用于威胁和审讯）。杰西说："最好的猎手不是最强壮的，是最懂得猎物心理的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "bounty_hunter_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_bounty_hunter_head" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_bounty_hunter_head" }
]
```

**2件套（设计预留）**：追踪检定 +3，对 humanoid 类型生物感知检定 +2。
**4件套（设计预留）**：每日一次「赏金标记」：标记一个目标，持续 24 小时，被标记目标的位置始终可知，且对其攻击检定 +2、伤害 +1D6；被标记目标无法隐匿（对抗你的隐匿自动失败）。

---

### 26.2 赏金猎人链甲

```gdscript
item_id = "armor_bounty_hunter_body"
display_name = "赏金猎人链甲"
description = "一副由锁链公会的标志锁链与通缉犯链条拼接而成的链甲，甲面挂满了小型工具——手铐、绳索、钩子、小刀、镣铐。这些工具不是装饰，它们是功能性的，可以在任何时候取出使用。\n\n杰西在制作这副链甲时，将自己使用过的所有工具都挂在了甲面上。他说："这副链甲不是防御的，它是'工具箱'——让我在任何情况下都有办法应对。"\n\n这副链甲的特殊效果是：穿戴者可以随时从链甲上取出任何小型工具（无需动作），且所有与束缚、攀爬、开锁相关的检定 +3。杰西说："猎手需要的不只是武器，是手段——任何能够达成目的的手段。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "bounty_hunter_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_bounty_hunter_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_bounty_hunter_body" }
]
```

---

### 26.3 赏金猎人手套

```gdscript
item_id = "armor_bounty_hunter_hands"
display_name = "赏金猎人手套"
description = "一对由锁链公会的标志锁链与通缉犯镣铐制成的手套，手掌部分覆盖着粗糙的皮革——不是为了保护，而是为了在抓住猎物时不会滑落。手套的腕部有一根隐藏的绳索，可以在瞬间弹出束缚目标。\n\n杰西在制作这对手套时，将自己抓捕的第一个通缉犯的镣铐熔入了手套。他说："这对手套不只是手套，它是'第一滴血'——提醒我为什么开始这条路。"\n\n这对手套的特殊效果是：穿戴者可以用 bonus action 弹出手套腕部的绳索束缚一个 5 尺内的目标（目标须通过 DC14 敏捷豁免，失败则 restrained 1 回合）。且对被 restrained 的目标，攻击检定 +2。杰西说："猎手的任务不是杀死猎物，是抓住它。死亡太容易了，活着才难。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "bounty_hunter_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_bounty_hunter_hands" },
    { attribute_id = "grapple_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_bounty_hunter_hands" }
]
```

---

### 26.4 赏金猎人软靴

```gdscript
item_id = "armor_bounty_hunter_feet"
display_name = "赏金猎人软靴"
description = "一对由锁链公会的标志锁链与追踪犬足底皮革制成的软靴，靴底有细密的纹路——不是为了防滑，而是为了在追踪时不会留下自己的足迹。\n\n杰西在制作这对软靴时，研究了追踪犬的足底结构。他说："追踪犬的脚底有特殊的纹路，可以让它们在任何地面上行走而不发出声音。我要学会这种方式。"\n\n这对软靴的特殊效果是：穿戴者移动时不会留下任何足迹（即使是魔法追踪也无法发现），且可以通过嗅觉追踪目标（+5 追踪检定）。杰西说："最好的猎手不是最快的，是最耐心的——耐心到可以追踪一个目标三个月而不被发现。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "bounty_hunter_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_bounty_hunter_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_bounty_hunter_feet" }
]
```

---

## 套装二十七：瘟疫医生（Plague Doctor）

> *"疾病不是敌人，它是老师。它教会我们什么是脆弱，什么是坚强。"*

**套装主题**：中世纪瘟疫医师结社「乌鸦面具」的装备。这个结社在无数次瘟疫中穿梭，治愈病人、研究疾病，他们的装备赋予了穿戴者对抗疾病和毒素的特化能力。集齐四件时，穿戴者获得「瘟疫之主」——可以操控疾病治疗盟友或伤害敌人。

**历史渊源**：乌鸦面具的创始人「瘟疫之主」阿斯克勒庇俄斯（另一位同名者），是一位在瘟疫中失去了所有家人的孤独医生。他说："我不是来治愈疾病的，我是来理解它的——只有理解了，才能战胜它。"

---

### 27.1 瘟疫医生面具

```gdscript
item_id = "armor_plague_doctor_head"
display_name = "瘟疫医生面具"
description = "一张标志性的乌鸦嘴面具，由乌鸦皮革与草药填充物制成。面具的鸟嘴中塞满了各种草药和香料——不是为了美观，而是为了过滤空气中的病原体。面具的眼眶处镶嵌着两块红色的玻璃，让穿戴者的眼睛看起来像恶魔。\n\n阿斯克勒庇俄斯在制作这张面具时，将自己治疗过的所有病人的草药都塞入了鸟嘴。他说："这张面具不只是面具，它是'药房'——让我在任何地方都能呼吸到治疗的空气。"\n\n这张面具的特殊效果是：穿戴者免疫所有 airborne diseases（空气传播疾病）和 inhaled toxins（吸入性毒素）。且可以通过面具「嗅出」30 尺内的任何疾病或毒素（面具会微微发热指示方向）。阿斯克勒庇俄斯说："医生需要的不只是眼睛，是鼻子——鼻子能闻到眼睛看不到的东西。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "plague_doctor_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_plague_doctor_head" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_plague_doctor_head" }
]
```

**2件套（设计预留）**：免疫所有 diseases 和 poisons；`medicine_bonus` +3。
**4件套（设计预留）**：每日三次「瘟疫之触」：触碰一个生物，可以选择治愈（恢复 2D8 + 智力修正 HP 并解除疾病/毒素）或伤害（造成 2D8 + 智力修正 necrotic 并感染疾病）；被感染的敌人每回合受到 1D6 necrotic（持续 1 分钟，可治愈）。

---

### 27.2 瘟疫医生长袍

```gdscript
item_id = "armor_plague_doctor_body"
display_name = "瘟疫医生长袍"
description = "一件由特殊处理的皮革与多层蜡布制成的长袍，长袍表面涂有一层防水的蜡质，可以阻挡任何液体的渗透。长袍的下摆很长，几乎拖到地面——不是为了美观，而是为了防止地面上的病原体接触到身体。\n\n阿斯克勒庇俄斯在制作这件长袍时，将自己治疗过的所有病人的蜡布都缝入了长袍。他说："这件长袍不只是衣服，它是'屏障'——让我可以在最危险的环境中安全地工作。"\n\n这件长袍的特殊效果是：穿戴者免疫所有 contact diseases（接触传播疾病）和 bloodborne pathogens（血液传播病原体）。且长袍上的蜡质层可以在接触酸性或腐蚀性液体时提供保护（acid 抗性 +10）。阿斯克勒庇俄斯说："医生不能只治愈别人，也要保护自己。死了的医生救不了任何人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "plague_doctor_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_plague_doctor_body" },
    { attribute_id = "resistance_poison", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_plague_doctor_body" }
]
```

---

### 27.3 瘟疫医生手套

```gdscript
item_id = "armor_plague_doctor_hands"
display_name = "瘟疫医生手套"
description = "一对由多层皮革与金属丝编织而成的手套，手套非常厚——厚到几乎无法感受到触摸。这是故意的，因为瘟疫医生不需要感受，他只需要操作。手套的指尖嵌有小型手术刀和镊子，可以在需要时弹出。\n\n阿斯克勒庇俄斯在制作这对手套时，将自己进行过的所有手术的工具都嵌入了手套。他说："这对手套不只是手套，它是'手术室'——让我可以在任何地方进行手术。"\n\n这对手套的特殊效果是：穿戴者可以用 bonus action 进行一次「紧急手术」：触碰一个濒死（HP 0）的生物，恢复 1D10 HP（稳定伤势）。且在进行任何治疗时，治疗效果 +2D6。阿斯克勒庇俄斯说："医生的手不是为杀戮的，是为拯救的。但拯救有时候比杀戮更需要技巧。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "plague_doctor_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_plague_doctor_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_plague_doctor_hands" }
]
```

---

### 27.4 瘟疫医生软靴

```gdscript
item_id = "armor_plague_doctor_feet"
display_name = "瘟疫医生软靴"
description = "一对由多层皮革与蜡布制成的软靴，靴底非常厚——厚到可以踩过任何污染物而不受影响。靴子的后跟有一根隐藏的金属钉，可以在需要时弹出，用来检测地面的硬度或刺穿敌人的脚。\n\n阿斯克勒庇俄斯在制作这对软靴时，走遍了所有瘟疫区，测试了各种地面的污染物。他说："这对软靴不只是鞋子，它是'探测器'——让我可以安全地行走在任何地方。"\n\n这对软靴的特殊效果是：穿戴者可以在任何污染物（污水、腐烂物、毒素）上正常行走，不受影响。且可以通过靴底的金属钉「检测」地面的成分（自动识别是否有毒素或病原体）。阿斯克勒庇俄斯说："医生走路不看风景，看地面——因为疾病往往从脚下开始。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "plague_doctor_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_plague_doctor_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_plague_doctor_feet" }
]
```

---

## 套装二十八：月影舞者（Moonshadow Dancer）

> *"月光下的一切都是舞蹈——包括死亡。"*

**套装主题**：月光神殿「银月之舞」的装备。这个神殿的舞者们相信，战斗是一种舞蹈，死亡是最后的舞步。他们的装备赋予了穿戴者优雅而致命的战斗舞蹈能力。集齐四件时，穿戴者获得「月影之舞」——可以在战斗中跳舞，每次命中后获得额外移动力和AC。

**历史渊源**：银月之舞的首席舞者「月影」莎乐美（另一位同名者），是一位将暗杀升华为艺术的舞蹈家。她说："我不是在杀人，我是在为他们表演最后的舞蹈。"

---

### 28.1 月影舞者面纱

```gdscript
item_id = "armor_moonshadow_dancer_head"
display_name = "月影舞者面纱"
description = "一条由月光丝绸与银月之露编织而成的面纱，面纱轻薄到几乎透明，但在月光下会折射出彩虹般的光芒。面纱遮住面部，只露出双眼——那是舞台上最神秘的部分。\n\n莎乐美在编织这条面纱时，用了一整年的月光。她说："面纱不是遮挡，它是' reveal '——让你看到我想让你看到的，隐藏我不想让你看到的。"\n\n这条面纱的特殊效果是：在月光下，穿戴者可以通过面纱「迷惑」一个目标（目标须通过 DC14 魅力豁免，失败则被 charmed 1 回合）。且穿戴者在舞蹈中（移动超过 10 尺后）的攻击检定 +2。莎乐美说："最好的舞者不是技术最好的，是最让人移不开眼的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "medium_armor", "moonshadow_dancer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_moonshadow_dancer_head" },
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_moonshadow_dancer_head" }
]
```

**2件套（设计预留）**：`acrobatics_bonus` +3，舞蹈中（移动后）AC +1。
**4件套（设计预留）**：每次 melee 命中后，可以立即移动 10 尺（不引发借机攻击）且 AC +1（持续至下回合，可叠加至 +3）；月光下可以「月影步」： bonus action 传送至 15 尺内任意位置（如同 misty step）。

---

### 28.2 月影舞者舞衣

```gdscript
item_id = "armor_moonshadow_dancer_body"
display_name = "月影舞者舞衣"
description = "一件由月光丝绸与银月之露编织而成的舞衣，舞衣非常轻薄，几乎无法提供物理防御——但它不是为防御而设计的，它是为移动而设计的。舞衣的下摆在旋转时会展开，如同月光下的花朵。\n\n莎乐美在编织这件舞衣时，将自己所有的舞蹈记忆都编入了丝线。她说："这件舞衣不只是衣服，它是'编年史'——记录了我所有的舞蹈。"\n\n这件舞衣的特殊效果是：穿戴者的移动力 +15 尺，且可以在舞蹈中（移动超过 15 尺后）进行一次免费的 melee 攻击（如同 bonus action）。但缺点是，舞衣的防御力很低——AC 基础值只有 4（比普通中甲低）。莎乐美说："最好的防御不是铠甲，是不要被打中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "medium_armor", "moonshadow_dancer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "armor_moonshadow_dancer_body" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_moonshadow_dancer_body" }
]
```

---

### 28.3 月影舞者手套

```gdscript
item_id = "armor_moonshadow_dancer_hands"
display_name = "月影舞者手套"
description = "一对由月光丝绸与银月之露编织而成的手套，手套的指尖嵌有小型刀片——不是为杀戮，而是为舞蹈中的「点缀」。刀片在月光下会发出银光，如同舞蹈中的星光。\n\n莎乐美在编织这对手套时，将自己所有舞蹈中的「致命动作」都编入了刀片的角度。她说："这对手套不只是手套，它是'编舞'——每一次挥舞都是一个动作，每一个动作都是一段舞蹈。"\n\n这对手套的特殊效果是：穿戴者可以用手套进行「舞蹈攻击」——每次攻击都是一次优雅的舞蹈动作，命中后可以让目标「陶醉」（目标须通过 DC14 魅力豁免，失败则下一回合攻击检定 -2，因为被舞蹈迷惑）。莎乐美说："最好的武器不是最锋利的，是最美丽的——因为美丽让人放松警惕。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "medium_armor", "moonshadow_dancer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_moonshadow_dancer_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_moonshadow_dancer_hands" }
]
```

---

### 28.4 月影舞者舞鞋

```gdscript
item_id = "armor_moonshadow_dancer_feet"
display_name = "月影舞者舞鞋"
description = "一对由月光丝绸与银月之露编织而成的舞鞋，舞鞋的鞋底非常薄——薄到可以感受到地面的每一道纹理。这不是弱点，而是优势，因为舞者需要感受地面才能跳出最完美的舞蹈。\n\n莎乐美在编织这对舞鞋时，将自己所有的舞步记忆都编入了鞋底的纹路。她说："这对舞鞋不只是鞋子，它是'地图'——记录了我所有走过的舞台。"\n\n这对舞鞋的特殊效果是：穿戴者可以在任何表面上跳舞——甚至是水面（如同 water dance，在月光下可以短暂在水面行走 3 回合）。且每次舞蹈中（移动后）的攻击命中后，可以立即进行一次「舞步撤退」（10 尺移动，不引发借机攻击）。莎乐美说："舞蹈中最重要的不是进攻，是撤退——优雅的撤退比华丽的进攻更难。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "medium_armor", "moonshadow_dancer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_moonshadow_dancer_feet" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_moonshadow_dancer_feet" }
]
```

---

## 套装二十九：血骑士（Blood Knight）

> *"血不是生命的结束，它是力量的开始。别人的血让我强大，我的血让我不屈。"*

**套装主题**：吸血鬼骑士团「血杯」的装备。这个骑士团的成员不是完全的吸血鬼，而是与吸血鬼达成了契约的人类——他们用鲜血换取力量。集齐四件时，穿戴者获得「血之契约」——可以通过吸收敌人的血液来恢复自己，且低血量时变得更加强大。

**历史渊源**：血杯骑士团的创始人「第一血骑士」德古拉（另一位同名者），是一位被吸血鬼咬过后没有变成吸血鬼的人类。他说："我不是吸血鬼的奴隶，我是它的合伙人——我用鲜血支付，它用力量回报。"

---

### 29.1 血骑士头盔

```gdscript
item_id = "armor_blood_knight_head"
display_name = "血骑士头盔"
description = "一顶由吸血蝙蝠的骨骼与凝固的鲜血熔铸而成的狰狞头盔，头盔两侧各有一支向上弯曲的蝙蝠翅膀突起。面甲上有一道裂缝——那不是损伤，而是故意留下的，为了让穿戴者可以伸出舌头舔舐敌人的血液。\n\n德古拉在锻造这顶头盔时，将一百只吸血蝙蝠的骨骼熔入了金属。他说："蝙蝠不是恶魔，它们是'商人'——用血液交易生命。我也要成为这样的商人。"\n\n这顶头盔的特殊效果是：穿戴者可以通过面甲的裂缝「品尝」敌人的血液（近战命中后，bonus action 舔舐伤口），恢复 1D6 HP。但缺点是，如果 24 小时内没有尝到血液，头盔会「饥渴」，导致穿戴者攻击检定 -2。德古拉说："血液是契约的货币，不支付就会受罚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "blood_knight_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_blood_knight_head" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_blood_knight_head" }
]
```

**2件套（设计预留）**：`resistance_necrotic` +10；近战命中后恢复 1D6 HP（吸血）。
**4件套（设计预留）**：HP 越低攻击越强（HP 75% 时伤害 +1D6，50% 时 +2D6，25% 时 +3D6）；HP 降至 0 时，有 25% 概率触发「血之狂怒」：恢复至 10 HP 并获得额外一个 action（每场战斗一次）；副作用：无法通过常规治疗恢复 HP（只能通过吸血或 necrotic 伤害恢复）。

---

### 29.2 血骑士链甲

```gdscript
item_id = "armor_blood_knight_body"
display_name = "血骑士链甲"
description = "一副由吸血蝙蝠的皮革与凝固的鲜血编织而成的链甲，链甲表面覆盖着一层永远湿润的血液——那不是穿戴者的血，而是「契约之血」，由吸血鬼提供的永恒血液。\n\n德古拉在制作这副链甲时，与自己的吸血鬼主人达成了契约——吸血鬼提供永恒的血液作为链甲的材料，德古拉则提供战场上收集的敌人血液作为回报。他说："这副链甲不是防御的，它是'储血罐'——储存着我和主人的共同财富。"\n\n这副链甲的特殊效果是：穿戴者可以将吸收的血液储存在链甲中（最多储存 20 点「血液能量」）。储存的血液可以在需要时释放——每 5 点血液能量可以恢复 1D10 HP 或增加 1D6 伤害（持续 1 回合）。德古拉说："血液不是一次性使用的，它是货币——可以储蓄，可以投资，可以消费。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "blood_knight_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_blood_knight_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_blood_knight_body" }
]
```

---

### 29.3 血骑士手套

```gdscript
item_id = "armor_blood_knight_hands"
display_name = "血骑士手套"
description = "一对由吸血蝙蝠的爪子与凝固的鲜血制成的手套，手掌部分覆盖着一层吸血的薄膜——不是为了保护，而是为了在接触血液时能够更快地吸收。手套的指尖有可伸缩的吸管，可以刺入敌人的皮肤直接吸取血液。\n\n德古拉在制作这对手套时，将自己的指甲改造成了吸血器官。他说："这对手套不只是手套，它们是'吸管'——让我可以直接从敌人身上喝饮料。"\n\n这对手套的特殊效果是：穿戴者可以用手套进行「直接吸血」—— melee 命中后，bonus action 刺入目标，吸取 2D6 HP（恢复自身等量 HP）。但缺点是，如果目标没有血液（construct、undead、ooze 等），吸血失败且穿戴者受到 1D6 psychic 伤害（「饥渴」的反噬）。德古拉说："不是所有敌人都有血，但没有血的敌人不值得猎杀。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "blood_knight_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_blood_knight_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_blood_knight_hands" }
]
```

---

### 29.4 血骑士软靴

```gdscript
item_id = "armor_blood_knight_feet"
display_name = "血骑士软靴"
description = "一对由吸血蝙蝠的足底薄膜与凝固的鲜血制成的软靴，靴底非常薄——薄到可以感受到地面的每一道血液痕迹。这不是弱点，而是优势，因为血骑士可以通过地面的血液痕迹追踪猎物。\n\n德古拉在制作这对软靴时，将自己的足底改造成了血液感应器官。他说："这对软靴不只是鞋子，它们是'血液雷达'——让我可以追踪任何流血的猎物。"\n\n这对软靴的特殊效果是：穿戴者可以「感知」到 60 尺内任何流血的生物（软靴会微微发热指示方向）。且在血液上行走时，移动力 +10（血液让软靴更加润滑）。德古拉说："血液是我的路标，也是我的燃料。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "blood_knight_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_blood_knight_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_blood_knight_feet" }
]
```

---

## 套装三十：幽灵骑士（Phantom Rider）

> *"生与死之间，有一条狭窄的路。我走在那条路上，不属于任何一边。"*

**套装主题**：幽灵骑士团「无形之骑」的装备。这个骑士团的成员是半人半幽灵的存在——他们在死亡边缘徘徊，获得了穿梭于生死之间的能力。集齐四件时，穿戴者获得「幽灵之躯」——可以在虚实之间切换，免疫物理伤害的一半。

**历史渊源**：无形之骑的创始人「第一幽灵」卡斯帕，是一位在死亡边缘被救回的人类——但不是完全救回，他的一部分灵魂留在了死亡之地。他说："我不是活着，也不是死了——我是'中间态'，而中间态有时候比两边都强大。"

---

### 30.1 幽灵骑士头盔

```gdscript
item_id = "armor_phantom_rider_head"
display_name = "幽灵骑士头盔"
description = "一顶由幽灵之尘与记忆金属锻造的奇异头盔，头盔呈现出半透明的状态——你可以透过它看到后面的东西，但它仍然提供防御。头盔内部不断有微弱的低语声，那是卡斯帕留在死亡之地的那部分灵魂的"回音"。\n\n卡斯帕在锻造这顶头盔时，将自己的一部分灵魂从死亡之地拉了回来，封入了头盔。他说："这顶头盔不只是头盔，它是'桥梁'——连接生者和死者的桥梁。"\n\n这顶头盔的特殊效果是：穿戴者可以「倾听」死亡之地的低语——每长休一次，可以通过头盔与死亡之地进行一次简短的"对话"（获得关于过去或未来的模糊信息）。但缺点是，每次对话后，穿戴者必须通过 DC14 智慧豁免，失败则受到 1D10 psychic 伤害（死亡之地的信息太过沉重）。卡斯帕说："死亡知道一切，但它不会免费告诉你。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "phantom_rider_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_phantom_rider_head" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_phantom_rider_head" }
]
```

**2件套（设计预留）**：`resistance_necrotic` +10，免疫 frightened（幽灵不怕恐惧）。
**4件套（设计预留）**：每日两次「幽灵形态」：1 回合内，免疫所有 non-magical 物理伤害（武器穿过身体），且可以穿过实体障碍物；被魔法攻击命中时 25% 概率触发「幽灵闪避」：攻击穿过身体，miss；副作用：每次使用幽灵形态后， aging 1 年（灵魂更加接近死亡）。

---

### 30.2 幽灵骑士链甲

```gdscript
item_id = "armor_phantom_rider_body"
display_name = "幽灵骑士链甲"
description = "一副由幽灵之尘与记忆金属锻造的奇异链甲，链甲呈现出半透明的状态，不断有微弱的蓝色光芒在链环间流动。链甲的重量只有普通链甲的一半，因为一半的链甲存在于"另一个维度"。\n\n卡斯帕在锻造这副链甲时，将自己的一半身体留在了死亡之地，另一半带回了人间。他说："这副链甲不只是铠甲，它是'分割'——一半在人间保护我，一半在死亡之地保护我的灵魂。"\n\n这副链甲的特殊效果是：穿戴者可以将链甲的「幽灵一半」释放，形成一个「幽灵分身」（持续 1 回合，每日一次）。幽灵分身可以进行一次免费的 melee 攻击（1D8 necrotic），然后消散。卡斯帕说："我不需要战友，我的另一半就是战友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "phantom_rider_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_phantom_rider_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_phantom_rider_body" }
]
```

---

### 30.3 幽灵骑士手套

```gdscript
item_id = "armor_phantom_rider_hands"
display_name = "幽灵骑士手套"
description = "一对由幽灵之尘与记忆金属锻造的奇异手套，手套呈现出半透明的状态，你可以看到穿戴者的手骨在手套内部。手套可以「穿透」实体——不是破坏，而是穿过，如同幽灵的手。\n\n卡斯帕在锻造这对手套时，将自己的双手变成了"半幽灵"状态——一半在人间，一半在死亡之地。他说："这对手套不只是手套，它们是'门'——让我可以触摸两个世界。"\n\n这对手套的特殊效果是：穿戴者可以用手套「穿透」实体障碍物（墙壁、门、盔甲缝隙）进行攻击——目标无法通过常规护甲防御，必须通过 DC14 敏捷豁免，失败则受到全额伤害（如同 touch attack）。卡斯帕说："最好的攻击不是最强的，是最无法防御的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "phantom_rider_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phantom_rider_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phantom_rider_hands" }
]
```

---

### 30.4 幽灵骑士软靴

```gdscript
item_id = "armor_phantom_rider_feet"
display_name = "幽灵骑士软靴"
description = "一对由幽灵之尘与记忆金属锻造的奇异软靴，软靴呈现出半透明的状态，穿戴者的双脚仿佛悬浮在地面上方约一寸。行走时，软靴不会接触地面——它们在空中漂浮，如同幽灵的脚步。\n\n卡斯帕在锻造这对软靴时，将自己的双脚变成了"半幽灵"状态——不再完全存在于人间。他说："这对软靴不只是鞋子，它们是'翅膀'——让我可以在不需要翅膀的情况下飞翔。"\n\n这对软靴的特殊效果是：穿戴者可以「悬浮」在地面上方（免疫 difficult terrain 和地面陷阱），且移动时不会发出任何声音（如同 pass without trace）。且每日一次，可以「幽灵冲刺」：以三倍速度直线移动，穿过所有实体障碍物（持续 1 回合）。卡斯帕说："幽灵不需要走路，幽灵只需要飘。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "phantom_rider_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phantom_rider_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_phantom_rider_feet" }
]
```

---

*中甲套装 21–30 完结 · 共 40 件护甲装备*

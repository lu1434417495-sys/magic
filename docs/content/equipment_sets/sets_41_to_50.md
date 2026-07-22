# 传奇装备套装设计文档（套装 41–50：混合主题）

> 10 套混合主题套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×2、中甲×3、轻甲×5。

---

## 套装四十一：太阳圣骑士（Solar Paladin）

> *"太阳升起不是为了温暖大地，而是为了提醒黑暗——它终究会退去。"*

**套装主题**：太阳神殿「永恒黎明」的圣骑士装备。这些骑士崇拜太阳神，他们的装备由日光精华锻造，在白天无比强大，在夜晚则相对暗淡。集齐四件时，穿戴者获得「日光化身」——可以在日光下释放圣光攻击、治愈盟友、驱散亡灵。

**历史渊源**：永恒黎明的最高骑士「太阳之手」阿波罗（另一位同名者），是一位在日出时出生、据说体内流淌着阳光的圣骑士。他说："我不是太阳的仆人，我是它的延伸——它在天上照耀，我在地上执行。"

---

### 41.1 太阳圣骑士头盔

```gdscript
item_id = "armor_solar_paladin_head"
display_name = "太阳圣骑士头盔"
description = "一顶由太阳金与日光丝锻造的辉煌头盔，头盔正面有一道裂缝——那不是损伤，而是「日光通道」，让穿戴者可以通过头盔「看」到太阳的位置和强度。头盔顶部有一颗「太阳宝石」——一颗能够储存日光并在黑暗中释放的魔法宝石。\n\n阿波罗在锻造这顶头盔时，在日出时分将第一缕阳光封入了宝石。他说："第一缕阳光是最纯净的，因为它还没有被任何人污染过。"\n\n这顶头盔的特殊效果是：在日光下，穿戴者的视野范围翻倍，且可以「看见」隐形的 undead（它们在日光下会显现出轮廓）。在黑暗中，太阳宝石会释放储存的日光（15 尺半径 daylight，持续 1 小时/天）。阿波罗说："太阳不会永远照耀，但我的头盔会。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "solar_paladin_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_solar_paladin_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_solar_paladin_head" }
]
```

**2件套（设计预留）**：日光下 `attack_bonus` +2；`resistance_radiant` +10。
**4件套（设计预留）**：日光下 melee 攻击附加 1D8 radiant；每日三次「治愈之光」：30 尺内一个盟友恢复 2D8 + 魅力修正 HP；每日一次「日光爆发」：15 尺半径内所有 undead 受到 4D8 radiant 并须通过 DC16 体质豁免，失败则 blinded 1 回合；undead 在 10 尺内自动受到 1D4 radiant/回合（日光光环）。

---

### 41.2 太阳圣骑士板甲

```gdscript
item_id = "armor_solar_paladin_body"
display_name = "太阳圣骑士板甲"
description = "一副由太阳金与日光丝锻造的辉煌板甲，板甲表面刻有太阳神的神圣符文。在日光下，板甲会发出耀眼的金色光芒，让穿戴者看起来如同太阳的化身。在黑暗中，板甲则变得暗淡，但仍然散发着微弱的温暖。\n\n阿波罗在锻造这副板甲时，将一整天的日光都压缩进了金属。他说："这副板甲不只是铠甲，它是'移动的太阳'——让我可以把日光带到任何地方。"\n\n这副板甲的特殊效果是：在日光下，板甲的 AC +2（日光强化）。且穿戴者免疫 blinded（强光的副作用已经被板甲过滤）。阿波罗说："太阳的光芒是我的盾，不是我的弱点。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "solar_paladin_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_solar_paladin_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_solar_paladin_body" }
]
```

---

### 41.3 太阳圣骑士护手

```gdscript
item_id = "armor_solar_paladin_hands"
display_name = "太阳圣骑士护手"
description = "一对由太阳金与日光丝锻造的辉煌护手，护手表面不断有微弱的金色光芒在流动。握紧拳头时，光芒会汇聚在拳峰，形成两颗小型的「日光弹」。松开拳头时，光芒会消散，回到护手表面。\n\n阿波罗在锻造这对手套时，将自己的「日光之拳」能力封入了金属。他说："这对手套不只是手套，它们是'太阳拳'——让我可以用拳头释放日光。"\n\n这对手套的特殊效果是：穿戴者可以用拳头进行「日光打击」——徒手攻击造成 1D8 bludgeoning + 1D6 radiant。且可以用手掌「释放」一次「日光射线」（30 尺射程，2D8 radiant，每日三次）。阿波罗说："太阳不需要武器，它本身就是最强大的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "solar_paladin_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_solar_paladin_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_solar_paladin_hands" }
]
```

---

### 41.4 太阳圣骑士胫甲

```gdscript
item_id = "armor_solar_paladin_feet"
display_name = "太阳圣骑士胫甲"
description = "一对由太阳金与日光丝锻造的辉煌胫甲，胫甲表面刻有日出的图案。每一步踏下，图案会微微发光，在地面上留下短暂的金色足迹——不是弱点，是警告，告诉所有人：太阳正在移动。\n\n阿波罗在锻造这对胫甲时，将「日出行走」的能力封入了金属。他说："这对胫甲不只是鞋子，它们是'日出的脚步'——让我每一步都如同日出般不可阻挡。"\n\n这对胫甲的特殊效果是：穿戴者在日光下移动力 +10，且免疫 difficult terrain（日光照亮了道路）。且每日一次，可以进行「日光冲锋」——以三倍速度直线冲锋，路径上所有 undead 受到 2D8 radiant 并被推后 10 尺。阿波罗说："太阳不会绕路，它直接穿过黑暗。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "solar_paladin_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_solar_paladin_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_solar_paladin_feet" }
]
```

---

## 套装四十二：虚空守望者（Void Warden）

> *"虚空不是空无一物，它是充满了我们看不见的东西。我学会了看见它们。"*

**套装主题**：虚空守望者教团「无尽深渊」的装备。这个教团的成员守护现实与虚空的边界，他们的装备由虚空物质锻造，赋予了穿戴者看见和操控虚空的能力。集齐四件时，穿戴者获得「虚空之躯」——可以短暂进入虚空、释放虚空攻击、免疫某些现实规则。

**历史渊源**：无尽深渊的最高守望者「虚空之眼」诺登斯，是一位在虚空裂缝中幸存后被虚空力量改变的探索者。他说："虚空不是敌人，它是'另一面'——就像硬币有两面，现实也有另一面。"

---

### 42.1 虚空守望者头盔

```gdscript
item_id = "armor_void_warden_head"
display_name = "虚空守望者头盔"
description = "一顶由虚空黑曜石与虚空丝锻造的奇异头盔，头盔呈现出半透明的黑色——你可以看到后面的东西，但一切都变得扭曲。头盔的双眼位置没有眼洞——因为穿戴者不需要眼睛，虚空之眼可以直接「看见」一切。\n\n诺登斯在锻造这顶头盔时，将自己的双眼都献祭给了虚空，换取了「虚空之眼」的能力。他说："物质世界的眼睛只能看见物质，但虚空之眼可以看见一切——包括过去、未来和可能性。"\n\n这顶头盔的特殊效果是：穿戴者获得「虚空之眼」——可以看透 illusions、invisibility 和 etherealness（如同 truesight，但范围只有 30 尺）。但缺点是，穿戴者失去了物质世界的色彩视觉——只能看见黑白和虚空能量的紫色。诺登斯说："看见太多是有代价的，看不见颜色只是最小的代价。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "void_warden_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 21000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_void_warden_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_void_warden_head" }
]
```

**2件套（设计预留）**：免疫 frightened 和 charmed（虚空让人超脱情感）；`resistance_psychic` +10。
**4件套（设计预留）**：每日两次「虚空步」： bonus action，进入虚空 1 回合（免疫所有物理伤害，可以穿过实体，但不能攻击）；近战攻击附加 1D8 force（虚空能量）；被攻击时 25% 概率触发「虚空闪避」：攻击穿过身体（miss）；副作用：每次使用虚空能力后，受到 1D10 necrotic（虚空侵蚀）。

---

### 42.2 虚空守望者板甲

```gdscript
item_id = "armor_void_warden_body"
display_name = "虚空守望者板甲"
description = "一副由虚空黑曜石与虚空丝锻造的奇异板甲，板甲呈现出半透明的黑色，不断有微弱的紫色光芒在甲缝中流动。板甲没有重量——它的重量存在于另一个维度，所以在物质世界中它几乎没有质量。\n\n诺登斯在锻造这副板甲时，将一块虚空碎片嵌入了板甲的核心。他说："这副板甲不只是铠甲，它是'锚'——让我在物质世界和虚空之间保持稳定。"\n\n这副板甲的特殊效果是：穿戴者免疫「推离」和「强制传送」（虚空锚定效果）。且可以将板甲的虚空能量「释放」——每日一次，在 15 尺半径内创造一个「虚空区域」（持续 1 分钟），区域内所有生物每回合受到 2D6 necrotic（现实撕裂），且无法使用 teleportation 或 etherealness。诺登斯说："虚空不是武器，它是'空间'——一种可以占据的空间。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "void_warden_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 36000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_void_warden_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_void_warden_body" }
]
```

---

### 42.3 虚空守望者护手

```gdscript
item_id = "armor_void_warden_hands"
display_name = "虚空守望者护手"
description = "一对由虚空黑曜石与虚空丝锻造的奇异护手，护手呈现出半透明的黑色，手掌内部不断有虚空漩涡在旋转。握紧拳头时，漩涡会汇聚在拳峰；张开手掌时，漩涡会扩散。\n\n诺登斯在锻造这对手套时，将自己的双手变成了「虚空之门」——可以让物质在两个维度之间穿梭。他说："这对手套不只是手套，它们是'门'——让我可以打开通往虚空的道路。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「打开」一个小型虚空裂缝（5 尺半径，持续 1 回合），将范围内的任何物体吸入虚空（生物须通过 DC16 力量豁免，失败则被吸入虚空 1D6 回合，然后随机出现在 60 尺内某处）。每日一次。诺登斯说："虚空不是敌人，它是'垃圾桶'——你可以把不想看见的东西扔进去。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "void_warden_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_warden_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_warden_hands" }
]
```

---

### 42.4 虚空守望者胫甲

```gdscript
item_id = "armor_void_warden_feet"
display_name = "虚空守望者胫甲"
description = "一对由虚空黑曜石与虚空丝锻造的奇异胫甲，胫甲呈现出半透明的黑色，每一步踏下都会在地面留下短暂的紫色裂痕——那是虚空与现实的交界。\n\n诺登斯在锻造这对胫甲时，将「虚空行走」的能力封入了金属。他说："这对胫甲不只是鞋子，它们是'裂缝制造者'——让我每一步都在现实上留下痕迹。"\n\n这对胫甲的特殊效果是：穿戴者可以在虚空中「行走」——每日一次，可以进入虚空并瞬间移动至 60 尺内任何位置（如同 dimension door，但不会留下法术痕迹）。且可以在任何表面上行走——包括垂直的墙壁和天花板（虚空重力不同）。诺登斯说："虚空没有上下左右，所以它也没有限制。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "void_warden_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_warden_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_void_warden_feet" }
]
```

---

## 套装四十三：自然守护者（Nature Warden）

> *"文明不是进步，它是遗忘。忘记了土地的声音，忘记了风的语言，忘记了树的故事。"*

**套装主题**：德鲁伊结社「世界之根」的装备。这个结社的成员守护自然的平衡，他们的装备由活化的植物和动物材料制成，赋予了穿戴者与自然沟通、操控植物和动物的能力。集齐四件时，穿戴者获得「自然之躯」——可以召唤自然盟友、操控植物、与动物对话。

**历史渊源**：世界之根的最高德鲁伊「世界树之心」伊格德拉希尔，是一位据说与世界树灵魂融合的传奇德鲁伊。她说："我不是守护者，我是'自然的一部分'——就像树木、河流和动物一样。"

---

### 43.1 自然守护者头冠

```gdscript
item_id = "armor_nature_warden_head"
display_name = "自然守护者头冠"
description = "一顶由世界树的嫩枝与活藤编织而成的头冠，头冠上镶嵌着一颗「自然之心」——一颗能够与自然万物沟通的魔法种子。头冠佩戴时，周围的植物会微微向穿戴者倾斜，仿佛在致敬。\n\n伊格德拉希尔在编织这顶头冠时，从世界树的顶端折下了一段嫩枝。她说："这段嫩枝不是被折断的，是'被给予的'——世界树选择了我，让我成为它的延伸。"\n\n这顶头冠的特殊效果是：穿戴者可以「听懂」所有动物和植物的语言（如同 speak with animals and plants）。且可以通过头冠「感知」到 1 里内所有自然环境的异常（疾病、枯萎、污染等）。伊格德拉希尔说："自然不是沉默的，它只是说得很慢——慢到大多数人没有耐心听完。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "nature_warden_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_nature_warden_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_nature_warden_head" }
]
```

**2件套（设计预留）**：`animal_handling_bonus` +3；`survival_bonus` +3。
**4件套（设计预留）**：每日一次「召唤自然盟友」：召唤 1D4 只 CR 不超过 1 的动物（狼、熊、鹰等，持续 10 分钟）；可以在自然环境中操控植物（如同 plant growth 和 entangle 的结合，15 尺半径困难地形，敌人须通过 DC16 力量豁免，失败则 restrained）；在森林/丛林中 AC +2（自然 camouflage）。

---

### 43.2 自然守护者皮甲

```gdscript
item_id = "armor_nature_warden_body"
display_name = "自然守护者皮甲"
description = "一副由活化树皮与活藤编织而成的皮甲，皮甲表面不断有新芽在生长和凋谢——生长代表生命的延续，凋谢代表生命的循环。皮甲的重量会随着季节变化——春天最轻，冬天最重。\n\n伊格德拉希尔在编织这副皮甲时，将一整棵古树的树皮都缝入了皮甲。她说："这副皮甲不只是防御，它是'生命'——它会生长，会愈合，会适应。"\n\n这副皮甲的特殊效果是：穿戴者在自然环境中（森林、草原、沼泽）每小时恢复 1 HP（自然愈合）。且可以将皮甲上的植物「活化」——每日一次，让皮甲上的藤蔓生长，缠绕 15 尺内的一个目标（目标须通过 DC16 力量豁免，失败则 restrained 1 分钟）。伊格德拉希尔说："自然是最好的医生，也是最好的狱卒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "nature_warden_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_nature_warden_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_nature_warden_body" }
]
```

---

### 43.3 自然守护者手套

```gdscript
item_id = "armor_nature_warden_hands"
display_name = "自然守护者手套"
description = "一对由活化树皮与活藤编织而成的手套，手套表面不断有小型花朵在开放和凋谢。触碰任何植物时，植物会立即生长；触碰任何动物时，动物会平静下来。\n\n伊格德拉希尔在编织这对手套时，将自己的「自然之触」能力封入了藤蔓。她说："这对手套不只是手套，它们是'生命之手'——让我可以触摸并改变生命。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「治愈」植物和动物（恢复 2D8 HP）。且可以用触碰「加速」植物生长——在 1 回合内让种子长成大树（可以用来制造临时掩体或桥梁）。每日一次。伊格德拉希尔说："生命是最强大的力量，也是最温柔的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "nature_warden_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nature_warden_hands" },
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_nature_warden_hands" }
]
```

---

### 43.4 自然守护者软靴

```gdscript
item_id = "armor_nature_warden_feet"
display_name = "自然守护者软靴"
description = "一对由活化树皮与活藤编织而成的软靴，靴底覆盖着一层苔藓。每一步踏下，苔藓会在脚下生长，让穿戴者可以在任何自然地面上正常行走——包括沼泽、流沙和荆棘。\n\n伊格德拉希尔在编织这对软靴时，将「自然之步」能力封入了苔藓。她说："这对软靴不只是鞋子，它们是'自然的一部分'——让我可以行走在任何地方而不伤害自然。"\n\n这对软靴的特殊效果是：穿戴者在自然环境中免疫 difficult terrain。且走过的地方会留下「生命足迹」——植物会在足迹处生长（1 小时后长出小花，一天后长出草丛）。伊格德拉希尔说："最好的旅行不是 fastest，是 leave the world better than you found it。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "nature_warden_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nature_warden_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_nature_warden_feet" }
]
```

---

## 套装四十四：符文铁匠（Rune Smith）

> *"每个符文都是一个承诺——金属对力量的承诺，工匠对金属的承诺。"*

**套装主题**：符文工匠公会「永恒熔炉」的装备。这个公会的成员在装备上刻写强大的符文，赋予装备魔法能力。他们的装备本身也是符文的杰作，集齐四件时，穿戴者获得「符文之躯」——可以在战斗中刻写临时符文、激活装备的隐藏能力、甚至将符文附魔到武器上。

**历史渊源**：永恒熔炉的大师工匠「符文之心」索林（另一位同名者），是一位能够在战斗中即兴刻写符文的传奇铁匠。他说："我不是在锻造武器，我是在'书写'——用金属书写魔法，用火焰书写力量。"

---

### 44.1 符文铁匠头冠

```gdscript
item_id = "armor_rune_smith_head"
display_name = "符文铁匠头冠"
description = "一顶由符文金属与魔法丝锻造的工匠头冠，头冠表面刻有七颗永久性符文——每颗符文代表一种元素（火、水、风、土、光、暗、奥术）。当穿戴者集中精神时，对应的符文会发光，赋予穿戴者对应的能力。\n\n索林在锻造这顶头冠时，将七种元素的本质都封入了符文。他说："这顶头冠不只是头冠，它是'元素辞典'——让我可以随时调用任何元素的力量。"\n\n这顶头冠的特殊效果是：穿戴者可以「激活」一个符文，获得对应的抗性（+10 resistance，持续 1 小时，每日一次每种元素）。且可以通过头冠「读取」任何装备上的符文（自动识别符文效果和激活方式）。索林说："最好的工匠不是创造新东西的，是读懂已有东西的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "rune_smith_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_rune_smith_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_rune_smith_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；`crafting_bonus` +3（符文刻写）。
**4件套（设计预留）**：每日三次「符文刻写」： bonus action，在当前武器或护甲上刻写一个临时符文（持续 1 分钟）：火符文（+1D6 fire）、冰符文（+1D6 cold）、雷符文（+1D6 lightning）、或盾符文（AC +2）；可以「激活」装备上的隐藏符文（自动识别并激活任何魔法装备的隐藏能力）。

---

### 44.2 符文铁匠链甲

```gdscript
item_id = "armor_rune_smith_body"
display_name = "符文铁匠链甲"
description = "一副由符文金属与魔法丝锻造的工匠链甲，链甲的每一环上都刻有微型符文。这些符文在正常情况下是暗淡的，但当链甲受到攻击时，对应的防御符文会自动激活，发出光芒并减少伤害。\n\n索林在锻造这副链甲时，将三百个防御符文都刻入了链环。他说："这副链甲不只是铠甲，它是'自动防御系统'——每一个符文都是一个哨兵，随时准备保护我。"\n\n这副链甲的特殊效果是：当穿戴者受到元素伤害时，链甲会自动激活对应的抗性符文，减少 1D10 该元素伤害（每种元素每日一次）。且可以将链甲的符文「过载」——每日一次，让所有符文同时激活，获得所有元素抗性 +10（持续 1 回合）。索林说："最好的防御不是最强的，是最快的——快到在伤害到来之前就已经准备好了。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "rune_smith_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_rune_smith_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_rune_smith_body" }
]
```

---

### 44.3 符文铁匠手套

```gdscript
item_id = "armor_rune_smith_hands"
display_name = "符文铁匠手套"
description = "一对由符文金属与魔法丝锻造的工匠手套，手套表面刻有「刻写之印」——一个可以临时在任何表面上刻写符文的符文阵。手套非常耐热——可以在熔炉中直接操作炽热的金属。\n\n索林在锻造这对手套时，将自己的「刻写之手」能力封入了符文。他说："这对手套不只是手套，它们是'笔'——让我可以在任何地方书写魔法。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「刻写」临时符文——在任何表面上（武器、护甲、墙壁、甚至生物的皮肤）刻写一个持续 1 小时的符文。符文效果包括：光亮、火焰、冰冻、震动、或警报（当特定条件触发时）。每日三次。索林说："最好的魔法不是最强大的，是最灵活的——灵活到可以在任何情况下即兴创作。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "rune_smith_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rune_smith_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rune_smith_hands" }
]
```

---

### 44.4 符文铁匠软靴

```gdscript
item_id = "armor_rune_smith_feet"
display_name = "符文铁匠软靴"
description = "一对由符文金属与魔法丝锻造的工匠软靴，靴底刻有「稳固之印」——让穿戴者可以在最不稳定的地面上保持稳定。软靴的表面有耐火涂层，可以在熔炉边安全行走。\n\n索林在锻造这对软靴时，将自己的「稳固之步」能力封入了符文。他说："这对软靴不只是鞋子，它们是'锚'——让我在任何地方都能站稳。"\n\n这对软靴的特殊效果是：穿戴者免疫 prone（击倒）和 shove（推离）（稳固锚定）。且可以在任何热表面上正常行走——包括熔岩、炽热的金属和火焰（耐火涂层保护）。索林说："铁匠不需要地板，他需要'任何地方'都可以站稳。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "rune_smith_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rune_smith_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_rune_smith_feet" }
]
```

---

## 套装四十五：灵魂收割者（Soul Reaper）

> *"死亡不是终点，它是另一种开始。我的工作不是杀死敌人，是收集他们的灵魂——为了审判。"*

**套装主题**：死神仆从教团「终焉之镰」的装备。这个教团的成员为死神服务，收集游荡的灵魂，他们的装备赋予了穿戴者看见灵魂、收割灵魂和操控亡灵的能力。集齐四件时，穿戴者获得「死神之躯」——可以收割敌人的灵魂、召唤亡灵仆从、与死神对话。

**历史渊源**：终焉之镰的最高收割者「第一镰」塔纳托斯，是一位在濒死体验中见到了死神并选择为其服务的战士。他说："我不是死神的奴隶，我是它的'合伙人'——我收集灵魂，它给我力量。"

---

### 45.1 灵魂收割者头冠

```gdscript
item_id = "armor_soul_reaper_head"
display_name = "灵魂收割者头冠"
description = "一顶由魂骨与死亡丝编织而成的恐怖头冠，头冠上镶嵌着七颗「灵魂宝石」——每颗宝石中都封印着一个被收割的灵魂。头冠佩戴时，宝石会发出微弱的幽光，光芒的颜色代表被封印灵魂的情绪。\n\n塔纳托斯在编织这顶头冠时，将自己的第一个收割的灵魂封入了第一颗宝石。他说："这头冠不只是头冠，它是'监狱'——也是'图书馆'，每个灵魂都有一个故事。"\n\n这顶头冠的特殊效果是：穿戴者可以「看见」所有灵魂——包括活人的灵魂（显示为微弱的光芒）、死者的灵魂（显示为幽灵般的轮廓）和 undead 的灵魂（显示为扭曲的黑影）。且可以通过头冠「读取」一个灵魂的记忆（每日一次，目标须通过 DC16 智慧豁免，失败则最近记忆被读取）。塔纳托斯说："灵魂不会说谎，因为它们已经没有身体可以隐藏。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "soul_reaper_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_soul_reaper_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_soul_reaper_head" }
]
```

**2件套（设计预留）**：`resistance_necrotic` +15；免疫 frightened（见过真正的死神后，什么都不会再让你恐惧）。
**4件套（设计预留）**：近战击杀敌人后，可以 bonus action「收割」其灵魂（恢复 1D10 HP 或储存一个灵魂能量）；每日一次「召唤亡灵」：消耗一个储存的灵魂，召唤一个 CR 不超过 2 的 undead（持续 10 分钟）；每日一次「死神对话」：与死神进行一次简短对话（获得关于死亡的模糊信息）。

---

### 45.2 灵魂收割者皮甲

```gdscript
item_id = "armor_soul_reaper_body"
display_name = "灵魂收割者皮甲"
description = "一副由魂骨与死亡丝编织而成的恐怖皮甲，皮甲表面不断有幽灵般的面孔在浮现和消失——那是被收割灵魂的最后表情。皮甲的重量会随着收割的灵魂数量增加——每个灵魂增加约 1 磅的重量。\n\n塔纳托斯在编织这副皮甲时，将所有他收割的灵魂都编入了皮甲的纤维。他说："这副皮甲不只是防御，它是'负担'——每个灵魂都在提醒我为什么而战。"\n\n这副皮甲的特殊效果是：穿戴者可以将皮甲上的灵魂「释放」——每日一次，释放所有储存的灵魂能量，对 15 尺半径内所有生物造成 1D6 necrotic/灵魂（最多 5D6）。且皮甲会「吸收」necrotic damage——穿戴者受到 necrotic 伤害时，50% 概率将伤害转化为灵魂能量储存。塔纳托斯说："死亡不是我的敌人，是我的资源。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "soul_reaper_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_soul_reaper_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_soul_reaper_body" }
]
```

---

### 45.3 灵魂收割者手套

```gdscript
item_id = "armor_soul_reaper_hands"
display_name = "灵魂收割者手套"
description = "一对由魂骨与死亡丝编织而成的恐怖手套，手套表面不断有冰冷的气息在流动。触碰任何生物时，手套会感受到对方的生命力量——越强越热，越弱越冷。\n\n塔纳托斯在编织这对手套时，将自己的「灵魂之触」能力封入了死亡丝。他说："这对手套不只是手套，它们是'天平'——让我可以感受到生命的重量。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「感知」一个生物的 HP 剩余百分比（如同医疗诊断，但不需要医学知识）。且可以用触碰「收割」一个濒死（HP 0）生物的灵魂——如果该生物的死亡是穿戴者造成的，则可以 bonus action 收割（恢复 2D8 HP 或储存一个灵魂能量）。塔纳托斯说："收割不是杀戮，它是'完成'——完成生与死的循环。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "soul_reaper_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_soul_reaper_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_soul_reaper_hands" }
]
```

---

### 45.4 灵魂收割者软靴

```gdscript
item_id = "armor_soul_reaper_feet"
display_name = "灵魂收割者软靴"
description = "一对由魂骨与死亡丝编织而成的恐怖软靴，靴底没有与地面接触——它们悬浮在地面之上约半寸，因为死亡不需要走路，死亡只是「经过」。\n\n塔纳托斯在编织这对软靴时，将自己的「死亡之步」能力封入了死亡丝。他说："这对软靴不只是鞋子，它们是'预兆'——让它们告诉我，死亡何时何地会到来。"\n\n这对软靴的特殊效果是：穿戴者移动时不会留下任何足迹（如同幽灵），且可以「感知」到 60 尺内任何即将死亡或濒死的生物（软靴会微微震动）。且可以在任何表面上行走——包括水面、熔岩和虚空（死亡无处不在）。塔纳托斯说："死神不需要路，它只需要目的地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "soul_reaper_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_soul_reaper_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_soul_reaper_feet" }
]
```

---

## 套装四十六：时空旅者（Chrono Walker）

> *"时间不是一条线，它是一个圆。我学会了在圆上行走——向前，向后，甚至 sideways。"*

**套装主题**：时间守护者结社「永恒螺旋」的装备。这个结社的成员守护时间的流动，他们的装备赋予了穿戴者操控时间的能力。集齐四件时，穿戴者获得「时间之躯」——可以减缓时间、加速自己、甚至短暂回溯。

**历史渊源**：永恒螺旋的最高守护者「时间之主」克罗诺斯（另一位同名者），是一位在时间裂缝中迷路后学会了操控时间的探索者。他说："时间不是敌人，它是'道路'——只是大多数人只能走一个方向。"

---

### 46.1 时空旅者头冠

```gdscript
item_id = "armor_chrono_walker_head"
display_name = "时空旅者头冠"
description = "一顶由时间水晶与时光丝编织而成的奇异头冠，头冠上镶嵌着一颗「时间之心」——一颗能够看到过去和未来的魔法水晶。水晶中 constantly 有影像在流动——有的是过去，有的是未来，有的可能是平行时间线。\n\n克罗诺斯在编织这头冠时，从时间裂缝中取出了一块凝固的时间。他说："这块时间不是被取出的，是'被借用的'——时间允许我看一眼，但不允许我带走。"\n\n这头冠的特殊效果是：穿戴者可以「预见」未来——每日一次，可以看到接下来 1 分钟内的一个可能未来（DM 描述一个即将发生的事件，但可能改变）。且可以通过头冠「回忆」过去——看到 24 小时内任何时间点的场景（如同回顾录像）。克罗诺斯说："时间不是秘密，它只是需要正确的眼睛来看。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "chrono_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chrono_walker_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_chrono_walker_head" }
]
```

**2件套（设计预留）**：先攻检定 +3；`insight_bonus` +3。
**4件套（设计预留）**：每日两次「时间减缓」： bonus action，1 回合内除自己外所有生物速度减半、攻击检定 -2（时间变慢）；每日一次「时间回溯」： reaction，当受到伤害时，回溯至上一回合的开始（恢复所有在该回合受到的伤害和消耗的资源）；副作用：每次使用时间能力后 aging 6 个月（时间旅行消耗生命）。

---

### 46.2 时空旅者长袍

```gdscript
item_id = "armor_chrono_walker_body"
display_name = "时空旅者长袍"
description = "一件由时间水晶与时光丝编织而成的奇异长袍，长袍表面不断有钟表指针的图案在流动。这件长袍没有固定的颜色——它会随着时间变化：早晨是金色，中午是白色，傍晚是红色，夜晚是黑色。\n\n克罗诺斯在编织这件长袍时，将一整天的时间都编入了丝线。他说："这件长袍不只是衣服，它是'时间表'——让我穿着时间本身。"\n\n这件长袍的特殊效果是：穿戴者可以「储存」时间——每日一次，可以将一个动作或 bonus action「储存」到长袍中，之后在任何时候释放（如同 time stop 的 miniature 版本，只影响自己）。且长袍会自动「修复」自己受到的任何损伤（每小时恢复 1 HP，如同时间倒流修复伤口）。克罗诺斯说："时间是最好的医生，因为它可以 undo 一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "chrono_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_chrono_walker_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_chrono_walker_body" }
]
```

---

### 46.3 时空旅者手套

```gdscript
item_id = "armor_chrono_walker_hands"
display_name = "时空旅者手套"
description = "一对由时间水晶与时光丝编织而成的奇异手套，手套表面不断有秒针在旋转。触碰任何物体时，手套可以「加速」或「减缓」该物体的时间——让水果瞬间腐烂，让伤口瞬间愈合，让金属瞬间生锈。\n\n克罗诺斯在编织这对手套时，将自己的「时间之触」能力封入了水晶。他说："这对手套不只是手套，它们是'时钟'——让我可以调整任何物体的时间。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「加速」一个物体——让植物瞬间生长（如同 plant growth 的瞬间版）、让伤口加速愈合（恢复 2D8 HP）、或让食物瞬间成熟。每日三次。且可以用触碰「减缓」一个生物——目标须通过 DC16 体质豁免，失败则 slowed 1 回合（速度减半，不能 reaction，攻击/施法每回合只能做一种）。克罗诺斯说："时间不是公平的，它来得快去得也快——但由我控制。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "chrono_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chrono_walker_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chrono_walker_hands" }
]
```

---

### 46.4 时空旅者便鞋

```gdscript
item_id = "armor_chrono_walker_feet"
display_name = "时空旅者便鞋"
description = "一对由时间水晶与时光丝编织而成的奇异便鞋，鞋底没有与地面接触——它们悬浮在地面之上约一寸，因为时间旅者不需要走路，他们只是在时间上「滑动」。\n\n克罗诺斯在编织这对便鞋时，将自己的「时间滑行」能力封入了水晶。他说："这对便鞋不只是鞋子，它们是'时间滑板'——让我可以在时间的表面上滑行。"\n\n这对便鞋的特殊效果是：穿戴者可以「滑行」——移动力 +15，且不会引发借机攻击（因为攻击者看到的是穿戴者的过去或未来位置，不是现在的）。且每日一次，可以进行「时间跳跃」——传送至 30 尺内任何位置（不是在空间中传送，是在时间中跳跃，因此不会触发任何陷阱或结界）。克罗诺斯说："最好的旅行不是 fastest，是 most efficient——在时间中走捷径。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "chrono_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chrono_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_chrono_walker_feet" }
]
```

---

## 套装四十七：梦境织者（Dream Weaver）

> *"梦境不是虚幻的，它是另一种真实。在梦中，我可以做到现实中不可能的事情。"*

**套装主题**：梦境守护者结社「暮光之织」的装备。这个结社的成员守护梦境与现实的边界，他们的装备赋予了穿戴者进入梦境、操控梦境和在梦中战斗的能力。集齐四件时，穿戴者获得「梦境之躯」——可以进入敌人的梦境、在梦中造成伤害、甚至将梦中的物品带回现实。

**历史渊源**：暮光之织的最高守护者「梦境之主」墨菲斯（另一位同名者），是一位在梦中出生、在现实中觉醒的神秘存在。他说："我不是活在现实中的人，我是活在梦境中的存在——只是在现实中有一个身体罢了。"

---

### 47.1 梦境织者头冠

```gdscript
item_id = "armor_dream_weaver_head"
display_name = "梦境织者头冠"
description = "一顶由梦境丝与月光编织而成的奇异头冠，头冠呈现出半透明的银白色，仿佛由月光本身编织而成。头冠佩戴时，穿戴者会感到一阵困意——不是普通的困意，是「梦境之门」正在打开的感觉。\n\n墨菲斯在编织这头冠时，将自己的第一个梦境都编入了丝线。他说："这头冠不只是头冠，它是'枕头'——让我可以随时进入梦境。"\n\n这头冠的特殊效果是：穿戴者可以「清醒梦」——在睡眠中保持清醒意识，控制梦境内容。且可以通过头冠「进入」30 尺内一个睡眠中生物的梦境（每日一次，持续 1 小时或直到目标醒来）。在梦中，穿戴者可以与目标交流、战斗或修改目标的记忆。墨菲斯说："梦境不是逃避，它是'另一个战场'——一个只有做梦者才能进入的战场。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "dream_weaver_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_weaver_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_dream_weaver_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；免疫「梦境操控」和「睡眠诅咒」。
**4件套（设计预留）**：每日一次「梦境入侵」：进入 30 尺内一个睡眠目标的梦境，在梦中造成 4D10 psychic（目标醒来时转化为真实伤害）；每日一次「梦境物品」：在梦中找到一件物品，醒来后该物品出现在现实中（由 DM 决定物品，通常是消耗品或信息）；被攻击时 25% 概率触发「梦境闪避」：攻击穿过梦境中的身体（miss）。

---

### 47.2 梦境织者长袍

```gdscript
item_id = "armor_dream_weaver_body"
display_name = "梦境织者长袍"
description = "一件由梦境丝与月光编织而成的奇异长袍，长袍呈现出半透明的银白色，表面不断有梦境般的图案在流动——有时是风景，有时是面孔，有时是无法描述的形状。这件长袍没有固定的形态——它会根据穿戴者的梦境而变化。\n\n墨菲斯在编织这件长袍时，将自己所有的梦境都编入了丝线。他说："这件长袍不只是衣服，它是'梦境日记'——记录了我所有的梦。"\n\n这件长袍的特殊效果是：穿戴者可以在现实中「召唤」梦境中的事物——每日一次，可以从长袍中召唤出一个梦境中的生物或物品（持续 1 分钟，生物不能造成伤害，物品可以使用）。且长袍会自动「保护」穿戴者在睡眠中——任何试图在睡眠中攻击或操控穿戴者的生物都会进入一个「噩梦陷阱」（须通过 DC16 智慧豁免，失败则 frightened 1 分钟）。墨菲斯说："梦境不是安全的，但它可以是你的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "dream_weaver_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dream_weaver_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_dream_weaver_body" }
]
```

---

### 47.3 梦境织者手套

```gdscript
item_id = "armor_dream_weaver_hands"
display_name = "梦境织者手套"
description = "一对由梦境丝与月光编织而成的奇异手套，手套呈现出半透明的银白色。触碰任何生物时，手套会将对方的意识拉入一个微型梦境——不是完整的梦境，只是一个瞬间的幻象。\n\n墨菲斯在编织这对手套时，将自己的「梦境之触」能力封入了丝线。他说："这对手套不只是手套，它们是'梦之门'——让我可以用触碰打开任何人的梦境。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「催眠」一个目标（目标须通过 DC16 智慧豁免，失败则 asleep 1 分钟，或被攻击时醒来）。每日三次。且可以用触碰「唤醒」一个睡眠中的目标——无论目标受到多强的睡眠魔法影响。墨菲斯说："梦境的钥匙是触碰——触碰可以打开梦境，也可以关闭它。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "dream_weaver_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_weaver_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dream_weaver_hands" }
]
```

---

### 47.4 梦境织者便鞋

```gdscript
item_id = "armor_dream_weaver_feet"
display_name = "梦境织者便鞋"
description = "一对由梦境丝与月光编织而成的奇异便鞋，鞋底没有与地面接触——它们悬浮在地面之上约半寸，因为梦境中的一切都是漂浮的。行走时，便鞋不会发出任何声音，也不会留下任何足迹。\n\n墨菲斯在编织这对便鞋时，将自己的「梦境之步」能力封入了丝线。他说："这对便鞋不只是鞋子，它们是'梦之翼'——让我在现实中也能像在梦中一样自由。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（如同 pass without trace），且可以在任何表面上行走——包括水面、空气（持续 3 回合/天）和梦境边界。且每日一次，可以进行「梦境漫步」：进入睡眠状态但保持清醒，在 1 里内的任何地方「出现」（如同投影，可以观察和聆听但不能交互）。墨菲斯说："最好的旅行不是 fastest，是最自由的——在梦中，你可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "dream_weaver_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_weaver_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_dream_weaver_feet" }
]
```

---

## 套装四十八：深渊潜行者（Abyss Stalker）

> *"深渊不是最深处，它是另一个世界的表面。我学会了在那个表面上行走。"*

**套装主题**：深渊探索者结社「无光之渊」的装备。这个结社的成员探索深海深渊，他们的装备赋予了穿戴者在极端深海环境中生存和战斗的能力。集齐四件时，穿戴者获得「深渊之躯」——可以在深海中自由行动、与深海生物沟通、释放深渊压力攻击。

**历史渊源**：无光之渊的最深潜行者「深渊之眼」达贡，是一位在深海中生活了十年的人类——他的身体已经被深渊改变，不再完全属于人类。他说："我不是人类，我不是深海生物——我是两者之间的存在。"

---

### 48.1 深渊潜行者头冠

```gdscript
item_id = "armor_abyss_stalker_head"
display_name = "深渊潜行者头冠"
description = "一顶由深海生物甲壳与深渊丝编织而成的头冠，头冠表面覆盖着一层生物发光层——在深海中发出微弱的蓝绿色光芒，在陆地上则完全暗淡。头冠的眼睛位置不是眼洞，而是两颗「深渊之眼」——由深海生物的眼睛改造而成，可以在完全黑暗中看见东西。\n\n达贡在编织这头冠时，从一只活了千年的深海巨兽身上取下了它的眼睛。他说："这只眼睛看过深渊的最深处，它知道那里有什么。"\n\n这头冠的特殊效果是：穿戴者获得「深渊之眼」——可以在完全黑暗中看见 120 尺（如同 darkvision 的强化版），且可以「看见」水压的分布（知道哪里水压高，哪里可以安全通过）。但缺点是，穿戴者在强光下会 blinded（深渊之眼不适应强光）。达贡说："深渊不需要光，深渊有自己的方式看世界。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "abyss_stalker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_abyss_stalker_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_abyss_stalker_head" }
]
```

**2件套（设计预留）**：水下呼吸；水下压力免疫（不受 depth 影响）；`swim_speed` = 步行速度 +15。
**4件套（设计预留）**：水下攻击不受惩罚；近战攻击附加 1D8 cold（深渊寒意）；每日一次「深渊重压」：15 尺半径内所有生物受到 4D8 force（水压），须通过 DC16 体质豁免，失败则 restrained 1 回合；可以与深海生物沟通（如同 speak with animals，但只对深海生物有效）。

---

### 48.2 深渊潜行者皮甲

```gdscript
item_id = "armor_abyss_stalker_body"
display_name = "深渊潜行者皮甲"
description = "一副由深海生物皮肤与深渊丝编织而成的皮甲，皮甲表面覆盖着一层滑腻的粘液。这层粘液不是为了美观，而是为了减少水流阻力——在深海中，阻力是生存的敌人。皮甲的颜色是深黑色，但在深海中会发出微弱的生物光。\n\n达贡在编织这副皮甲时，从一只巨型深海章鱼身上取下了它的皮肤。他说："这只章鱼的皮肤可以适应任何深度的压力，我也要学会这种能力。"\n\n这副皮甲的特殊效果是：穿戴者免疫水压伤害——可以在任何深度（即使是马里亚纳海沟）自由活动。且皮甲的粘液层可以减少水流阻力——游泳速度 +15，且在水中的隐匿检定 +5（如同深海生物的天然 camouflage）。达贡说："深渊不是限制，它是'主场'——在深渊中，我是最强的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "abyss_stalker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_abyss_stalker_body" },
    { attribute_id = "resistance_cold", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_abyss_stalker_body" }
]
```

---

### 48.3 深渊潜行者手套

```gdscript
item_id = "armor_abyss_stalker_hands"
display_name = "深渊潜行者手套"
description = "一对由深海生物触手与深渊丝编织而成的手套，手套表面覆盖着吸盘。这些吸盘不是为了攀爬，而是为了在深海中抓住东西——在深渊中，东西很容易漂走。\n\n达贡在编织这对手套时，从一只巨型深海乌贼身上取下了它的触手。他说："这只乌贼的触手可以抓住任何东西，不管多滑、多快、多重。我也要学会这种能力。"\n\n这对手套的特殊效果是：穿戴者在水中的抓握检定 +5（吸盘效果），且可以用手套「释放」深渊的寒冷——触碰一个目标，造成 2D8 cold（每日三次）。且可以用吸盘「吸附」在任何表面上——包括光滑的岩石、湿滑的船底和敌人的盔甲（grapple 检定 +5）。达贡说："深渊中最重要的不是力量，是'抓住'——抓住生存的机会。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "abyss_stalker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_abyss_stalker_hands" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_abyss_stalker_hands" }
]
```

---

### 48.4 深渊潜行者软靴

```gdscript
item_id = "armor_abyss_stalker_feet"
display_name = "深渊潜行者软靴"
description = "一对由深海生物鳍与深渊丝编织而成的软靴，软靴的形状不像人类的鞋子——它们更像鳍，宽大而扁平。在陆地上行走时，软靴会很笨拙（移动力 -5），但在水中，它们是完美的游泳工具。\n\n达贡在编织这对软靴时，从一只巨型深海鱼身上取下了它的鳍。他说："这只鱼的鳍可以让它在深渊中高速游动，我也要学会这种能力。"\n\n这对软靴的特殊效果是：在水中，穿戴者的游泳速度 = 步行速度 +20（鳍的效果）。且可以用「深渊冲刺」——每日一次，在水中以三倍速度直线冲刺，路径上所有生物受到 2D8 bludgeoning（水流冲击）并被推后 15 尺。达贡说："深渊中最重要的不是攻击，是'速度'——在深渊中，最快的猎手生存。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "abyss_stalker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_abyss_stalker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = -5, source_type = "equipment", source_id = "armor_abyss_stalker_feet" }
]
```

---

## 套装四十九：星辰观测者（Star Gazer）

> *"星辰不是遥不可及的光点，它们是'记忆'——宇宙的记忆。学会读取它们，你就能预知未来。"*

**套装主题**：占星术士结社「永恒天穹」的装备。这个结社的成员通过观测星辰来预知未来和操控命运，他们的装备赋予了穿戴者读取星辰、预知命运和释放星光攻击的能力。集齐四件时，穿戴者获得「星辰之躯」——可以召唤星光、预知命运、与星辰对话。

**历史渊源**：永恒天穹的最高占星术士「星辰之眼」乌拉诺斯，是一位据说在出生时被流星击中的传奇占星术士。他说："我不是在观测星辰，我是在'倾听'——它们在说话，只是大多数人听不见。"

---

### 49.1 星辰观测者头冠

```gdscript
item_id = "armor_star_gazer_head"
display_name = "星辰观测者头冠"
description = "一顶由陨铁与星光丝编织而成的华丽头冠，头冠上镶嵌着七颗不同颜色的「星辰宝石」——每颗宝石对应一个星座。当对应的星座在天空中可见时，宝石会发出对应颜色的光芒。头冠的顶部有一个小型「星盘」——一个可以自动旋转并指向当前星座位置的魔法装置。\n\n乌拉诺斯在编织这头冠时，从七颗不同的流星中取下了核心宝石。他说："每颗流星都是一颗星辰的碎片，它们记得星辰的一切——包括过去和未来。"\n\n这头冠的特殊效果是：穿戴者可以在白天「看见」星辰（如同恒定的 sky 法术），且可以通过头冠「读取」当前的星象——获得关于未来的模糊信息（每日一次，DM 提供一个关于接下来 24 小时内可能发生的事件的提示）。且当对应的星座在天空中时，穿戴者获得对应的加成（例如猎户座可见时攻击检定 +1，仙女座可见时魅力检定 +1 等）。乌拉诺斯说："星辰不是随机的，它们是有序的——只是这种秩序需要几千年来理解。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "star_gazer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_gazer_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_star_gazer_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；夜间（有星辰时）所有检定 +1。
**4件套（设计预留）**：每日一次「星辰坠落」：召唤一颗小流星攻击 60 尺内一个目标（6D10 fire + bludgeoning，15 尺半径，目标须通过 DC16 敏捷豁免，失败则全伤）；每日一次「命运预知」：预知接下来 1 分钟内的一个攻击（可以选择让该攻击自动命中或自动 miss）；夜间时 `spell_dc_bonus` +1。

---

### 49.2 星辰观测者长袍

```gdscript
item_id = "armor_star_gazer_body"
display_name = "星辰观测者长袍"
description = "一件由陨铁与星光丝编织而成的华丽长袍，长袍表面不断有星辰图案在流动——不是固定的图案，而是根据当前天空中的星辰实时变化。在夜晚，长袍会发出微弱的星光，让穿戴者看起来如同移动的星座。\n\n乌拉诺斯在编织这件长袍时，将一整片星空的图案都编入了丝线。他说："这件长袍不只是衣服，它是'星图'——让我穿着整个天空。"\n\n这件长袍的特殊效果是：在夜晚，长袍的星光可以提供 10 尺半径的 dim light（不会暴露位置，因为星光看起来像是自然现象）。且可以通过长袍「吸收」星光——在夜晚的户外，每小时恢复 1 HP（星光治愈）。且当穿戴者在夜晚施法时，法术效果 +1（星辰加持）。乌拉诺斯说："星辰不是遥远的，它们就在你身边——只要你愿意抬头看。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "star_gazer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_star_gazer_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_star_gazer_body" }
]
```

---

### 49.3 星辰观测者手套

```gdscript
item_id = "armor_star_gazer_hands"
display_name = "星辰观测者手套"
description = "一对由陨铁与星光丝编织而成的华丽手套，手套表面镶嵌着小型星辰宝石。挥舞手臂时，宝石会划出星光轨迹——不是魔法，是物理现象，因为星辰宝石在摩擦空气时会发光。\n\n乌拉诺斯在编织这对手套时，将自己的「星辰之手」能力封入了宝石。他说："这对手套不只是手套，它们是'星辰的延伸'——让我可以用手触摸星辰。"\n\n这对手套的特殊效果是：穿戴者可以用手指「画出」星座图案——每画出一个完整星座，就激活一个对应的效果（例如画出猎户座，获得攻击检定 +1，持续 1 小时；画出仙女座，获得魅力检定 +1，持续 1 小时）。每日三次。且可以用手掌「释放」星光——向前推出一道星光射线（30 尺射程，2D8 radiant，每日三次）。乌拉诺斯说："星辰不是只能看的，它们也是可以用的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "star_gazer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_gazer_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_gazer_hands" }
]
```

---

### 49.4 星辰观测者便鞋

```gdscript
item_id = "armor_star_gazer_feet"
display_name = "星辰观测者便鞋"
description = "一对由陨铁与星光丝编织而成的华丽便鞋，鞋底镶嵌着小型星辰宝石。行走时，宝石会与地面摩擦发出微弱的星光，在地面上留下短暂的星座图案——不是弱点，是美丽，也是标记。\n\n乌拉诺斯在编织这对便鞋时，将自己的「星辰之步」能力封入了宝石。他说："这对便鞋不只是鞋子，它们是'画笔'——让我每一步都在大地上绘制星座。"\n\n这对便鞋的特殊效果是：穿戴者可以在夜晚「漂浮」——移动力 +10，且免疫 falling damage（星光托住身体）。且可以通过「星辰步」在星光下快速移动——在夜晚的户外，可以以两倍速度移动（如同 haste 的 movement 效果，但不影响 actions）。乌拉诺斯说："在星辰下，重力只是一种建议。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "star_gazer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_gazer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_star_gazer_feet" }
]
```

---

## 套装五十：混沌使者（Chaos Herald）

> *"秩序是幻觉，混沌是真实。我拥抱真实。"*

**套装主题**：混沌信徒教团「无序之舞」的装备。这个教团的成员崇拜混沌本身，他们的装备由混沌物质锻造，赋予了穿戴者随机但强大的能力。集齐四件时，穿戴者获得「混沌之躯」——每次攻击或施法都会触发随机效果，无法预测但总是有趣。

**历史渊源**：无序之舞的至高使者「混沌之子」洛基（另一位同名者），是一位据说由纯粹的混沌能量诞生的存在。他说："我不是被创造的，我是'发生的'——就像掷骰子，结果是随机的，但总是有趣的。"

---

### 50.1 混沌使者头冠

```gdscript
item_id = "armor_chaos_herald_head"
display_name = "混沌使者头冠"
description = "一顶由混沌石与随机丝编织而成的奇异头冠，头冠的颜色、形状和纹理每秒钟都在变化——有时是金，有时是木，有时是火，有时是水。头冠上没有固定的装饰，因为混沌不允许固定。\n\n洛基在锻造这头冠时，将自己的「混沌之脑」能力封入了石头。他说："这头冠不只是头冠，它是'骰子'——每次戴上它，结果都不一样。"\n\n这头冠的特殊效果是：穿戴者的所有检定都会触发一个「混沌骰」——在每次 d20 检定后，额外掷一个 d6：1-2 为减益（检定 -2），3-4 为中性（无变化），5-6 为增益（检定 +2）。这种随机性无法被任何方式消除或预测。洛基说："混沌不是不公平的，它只是'不可预测'——而这正是它的美丽之处。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "chaos_herald_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chaos_herald_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_chaos_herald_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +2；所有 damage rolls 附加 1D4 随机元素（fire/cold/lightning/acid/thunder/force，每击掷骰决定）。
**4件套（设计预留）**：每次攻击后，额外触发一个随机效果（掷 d8）：1-2 自己受到 1D6 随机伤害（反噬），3-4 无额外效果，5-6 目标受到额外 2D6 随机伤害，7 目标须通过 DC14 豁免（随机属性），失败则 stunned 1 回合，8 自己恢复 1D10 HP；每日一次「混沌爆发」：15 尺半径内所有生物（包括自己）受到 3D10 随机元素伤害；免疫「秩序」效果（如 hold person、command 等控制效果）。

---

### 50.2 混沌使者长袍

```gdscript
item_id = "armor_chaos_herald_body"
display_name = "混沌使者长袍"
description = "一件由混沌石与随机丝编织而成的奇异长袍，长袍的颜色、图案和形状每秒钟都在变化——有时是火焰，有时是水流，有时是风暴，有时是岩石。这件长袍没有固定的形态，因为混沌不允许固定。\n\n洛基在锻造这件长袍时，将自己的「混沌之体」能力封入了石头。他说："这件长袍不只是衣服，它是'画布'——让混沌在我身上作画。"\n\n这件长袍的特殊效果是：穿戴者的 AC 也是随机的——每个回合开始时，掷一个 d4：1 时 AC -2，2 时 AC -1，3 时 AC +1，4 时 AC +2。这种随机性无法被任何方式消除或预测。且当穿戴者受到元素伤害时，有 50% 概率将该伤害转化为治疗（混沌的随机善意）。洛基说："混沌不是敌人，它是'惊喜'——有时是坏的，有时是好的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "chaos_herald_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_chaos_herald_body" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_chaos_herald_body" }
]
```

---

### 50.3 混沌使者手套

```gdscript
item_id = "armor_chaos_herald_hands"
display_name = "混沌使者手套"
description = "一对由混沌石与随机丝编织而成的奇异手套，手套的颜色和纹理每秒钟都在变化。触碰任何物体时，手套会产生随机效果——有时让物体发光，有时让物体变热，有时让物体漂浮，有时让物体爆炸（小型爆炸，只造成 1D4 伤害）。\n\n洛基在锻造这对手套时，将自己的「混沌之触」能力封入了石头。他说："这对手套不只是手套，它们是'骰子'——每次触碰都是一次掷骰。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「随机化」一个物体——让它的属性随机变化（例如武器的伤害类型随机改变，护甲的 AC 随机变化 ±1，药水的颜色随机变化等）。每日三次。且可以用手掌「释放」一个随机元素球（30 尺射程，2D8 随机元素伤害，每日三次）。洛基说："混沌不是混乱，它是'可能性'——每一次随机都是一个新的可能。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "chaos_herald_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chaos_herald_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chaos_herald_hands" }
]
```

---

### 50.4 混沌使者便鞋

```gdscript
item_id = "armor_chaos_herald_feet"
display_name = "混沌使者便鞋"
description = "一对由混沌石与随机丝编织而成的奇异便鞋，便鞋的形状和颜色每秒钟都在变化。有时它们看起来像普通的鞋子，有时像爪子，有时像鳍，有时像翅膀。行走时，便鞋会在地面上留下随机的足迹——有时是脚印，有时是爪印，有时是蹄印。\n\n洛基在锻造这对便鞋时，将自己的「混沌之步」能力封入了石头。他说："这对便鞋不只是鞋子，它们是'骰子'——每走一步都是一次掷骰。"\n\n这对便鞋的特殊效果是：穿戴者的移动力是随机的——每个回合开始时，掷一个 d6：1-2 时移动力 -10，3-4 时正常，5-6 时移动力 +15。且每日一次，可以进行「混沌传送」——随机传送至 60 尺内的一个位置（由 DM 随机决定，可能是安全的，可能是危险的）。洛基说："混沌不是迷路，它是'探索'——每一次随机都是一次新的冒险。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "chaos_herald_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_chaos_herald_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_chaos_herald_feet" }
]
```

---

*混合主题套装 41–50 完结 · 共 40 件护甲装备（重甲 8 + 中甲 12 + 轻甲 20）*

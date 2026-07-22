# 传奇装备套装设计文档（套装 61–70：暗黑 fantasy）

> 10 套暗黑 fantasy 主题套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×2、中甲×4、轻甲×4。

---

## 套装六十一：血肉编织者（Flesh Weaver）

> *"皮肤不是边界，它是'画布'。血液不是生命，它是'颜料'。我学会了用血肉作画。"*

**套装主题**：血肉术士教团「永恒肉体」的装备。这个教团的成员用血肉和器官进行魔法实验，他们的装备由活化的血肉和骨骼制成，赋予了穿戴者操控血肉、治愈伤口和改造身体的能力。集齐四件时，穿戴者获得「血肉之躯」——可以操控自己的血肉、吸收敌人的血肉、甚至创造血肉仆从。

**历史渊源**：永恒肉体的最高术士「血肉之母」莉莉丝（另一位同名者），是一位据说能够用一根头发创造出一个完整人体的疯狂术士。她说："我不是在玩弄生命，我是在'理解'它——只有理解了，才能超越它。"

---

### 61.1 血肉编织者头冠

```gdscript
item_id = "armor_flesh_weaver_head"
display_name = "血肉编织者头冠"
description = "一顶由活化血肉与神经丝编织而成的恐怖头冠，头冠呈现出粉红色——不是布料的颜色，是真正的血肉。头冠表面不断有血管在搏动，仿佛它是有生命的。头冠的正面镶嵌着一颗「第三眼」——一颗由真正的人类眼睛制成的宝石，让穿戴者可以看见血液的流动。\n\n莉莉丝在编织这头冠时，从自己的额头上取下了第三只眼睛（她通过实验长出的）。她说："这只眼睛不是被取下的，是'被分享的'——它仍然与我相连，让我可以同时看见两个视角。"\n\n这头冠的特殊效果是：穿戴者可以「看见」血液——看到任何生物体内的血管分布和血液流动（如同 x-ray vision，但只对血液有效）。且可以通过头冠「感知」到 30 尺内任何受伤生物的位置和伤势严重程度。莉莉丝说："血液不是秘密，它只是需要正确的眼睛来看。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "flesh_weaver_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_flesh_weaver_head" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_flesh_weaver_head" }
]
```

**2件套（设计预留）**：`medicine_bonus` +3；治疗 spell 和 ability 效果 +2D6（血肉强化）。
**4件套（设计预留）**：每日三次「血肉操控」： bonus action，操控 15 尺内一个生物的血肉（目标须通过 DC16 体质豁免，失败则受到 3D10 necrotic 并失去 1D4 力量或敏捷）；每日一次「血肉再生」：消耗一个动作，恢复 4D10 HP（从周围生物身上吸取生命力，30 尺内所有生物受到 1D10 necrotic）；可以「创造血肉仆从」： bonus action，从自己的血肉中分离出一个小型血肉生物（CR 1/2，持续 10 分钟）。

---

### 61.2 血肉编织者皮甲

```gdscript
item_id = "armor_flesh_weaver_body"
display_name = "血肉编织者皮甲"
description = "一副由活化血肉与神经丝编织而成的恐怖皮甲，皮甲呈现出粉红色，表面不断有血管在搏动。这副皮甲是有生命的——它会生长，会愈合，会适应。当穿戴者受伤时，皮甲会「伸出」额外的血肉来填补伤口；当穿戴者健康时，皮甲会「收缩」以减少重量。\n\n莉莉丝在编织这副皮甲时，将自己的一部分皮肤分离出来，与皮甲融合。她说："这副皮甲不只是铠甲，它是'我的第二层皮肤'——它会保护我，就像我自己的皮肤一样。"\n\n这副皮甲的特殊效果是：穿戴者每小时恢复 1 HP（血肉再生）。且可以将皮甲的血肉「释放」——每日一次，释放皮甲上的血肉形成一个血肉盾（吸收 30 点伤害，然后消散，皮甲的 AC 暂时 -1 直到长休恢复）。莉莉丝说："血肉不是弱点，它是'资源'——一种可以牺牲、可以再生、可以操控的资源。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "flesh_weaver_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_flesh_weaver_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_flesh_weaver_body" }
]
```

---

### 61.3 血肉编织者手套

```gdscript
item_id = "armor_flesh_weaver_hands"
display_name = "血肉编织者手套"
description = "一对由活化血肉与神经丝编织而成的恐怖手套，手套呈现出粉红色，表面不断有血管在搏动。手套非常敏感——可以感受到细胞的层次，这让穿戴者可以进行最精细的手术。\n\n莉莉丝在编织这对手套时，将自己的双手改造成了「血肉操控器」。她说："这对手套不只是手套，它们是'手术刀'——让我可以用手指切割、缝合、改造任何血肉。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「治愈」或「伤害」—— bonus action，触碰一个生物，可以选择恢复 2D10 HP（血肉缝合）或造成 2D10 necrotic（血肉撕裂）。每日三次。且可以用手套「改造」自己的身体—— bonus action，暂时增加 1 点力量或敏捷（从其他部位「借用」血肉），持续 1 小时。莉莉丝说："身体不是固定的，它是'可塑的'——只要你有足够的知识和勇气来重塑它。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "flesh_weaver_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flesh_weaver_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_flesh_weaver_hands" }
]
```

---

### 61.4 血肉编织者软靴

```gdscript
item_id = "armor_flesh_weaver_feet"
display_name = "血肉编织者软靴"
description = "一对由活化血肉与神经丝编织而成的恐怖软靴，软靴呈现出粉红色，表面不断有血管在搏动。软靴的底部有特殊的「神经末梢」——不是为了行走，而是为了感受地面的每一道纹理和温度变化。\n\n莉莉丝在编织这对软靴时，将自己的足底改造成了「感知器官」。她说："这对软靴不只是鞋子，它们是'触手'——让我可以通过地面感知周围的一切。"\n\n这对软靴的特殊效果是：穿戴者可以通过地面「感知」到 30 尺内所有生物的位置（通过感知地面的震动和温度变化，如同 tremorsense + thermal sense）。且可以用「血肉步」——在血肉上（包括尸体、活物、甚至自己的伤口）正常行走，速度不减。莉莉丝说："血肉不是地面，但对我来说，它比地面更熟悉。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "flesh_weaver_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flesh_weaver_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_flesh_weaver_feet" }
]
```

---

## 套装六十二：骸骨领主（Bone Lord）

> *"血肉会腐烂，但骨骼永恒。我抛弃了血肉，拥抱了永恒。"*

**套装主题**：亡灵法师教团「永恒骨骼」的装备。这个教团的成员用骨骼进行魔法研究，他们的装备由各种生物的骨骼拼接而成，赋予了穿戴者操控骨骼、召唤骷髅和免疫物理伤害的能力。集齐四件时，穿戴者获得「骸骨之躯」——可以操控骨骼、创造骨骼武器、从死亡中重生。

**历史渊源**：永恒骨骼的最高法师「骨王」奥西里斯（另一位同名者），是一位自愿褪去血肉、只留下骨骼的疯狂法师。他说："我不是死了，我是'精炼了'——从血肉之躯精炼为骨骼之躯，从有限变为永恒。"

---

### 62.1 骸骨领主头冠

```gdscript
item_id = "armor_bone_lord_head"
display_name = "骸骨领主头冠"
description = "一顶由各种生物头骨与骨丝编织而成的恐怖头冠，头冠的主体是一个小型龙的头骨——龙的眼睛位置镶嵌着两颗「魂火石」，让穿戴者可以获得亡灵的眼睛。头冠佩戴时，魂火石会发出微弱的绿光，仿佛有幽灵在其中燃烧。\n\n奥西里斯在编织这头冠时，从一只幼年龙的坟墓中盗取了它的头骨。他说："这头骨不是被盗的，是'被借用的'——我会用它来创造更大的价值，然后还给它。"\n\n这头冠的特殊效果是：穿戴者可以「看见」生命力量——看到任何生物的「生命之火」（显示为不同颜色的火焰，健康者是明亮的，濒死者是暗淡的）。且可以通过头冠「感知」到 60 尺内所有 undead 的位置和状态（如同 undead 雷达）。奥西里斯说："生命不是秘密，它只是需要正确的眼睛来看——而骷髅的眼睛看得最清楚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "bone_lord_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_bone_lord_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_bone_lord_head" }
]
```

**2件套（设计预留）**：免疫 piercing 和 slashing（骨骼结构让利器滑开）；`arcana_bonus` +3。
**4件套（设计预留）**：每日三次「骨骼操控」： bonus action，操控 15 尺内一个生物的骨骼（目标须通过 DC16 体质豁免，失败则 restrained 1 回合，因为骨骼不听使唤）；每日一次「召唤骷髅」：召唤 1D6 只骷髅战士（持续 10 分钟）；每日一次「骨甲覆盖」： bonus action，用骨骼覆盖全身（AC +3，免疫 piercing 和 slashing，持续 1 分钟）；HP 降至 0 时，有 25% 概率触发「骨骼重组」：恢复至 1 HP（骨骼重新拼接）。

---

### 62.2 骸骨领主板甲

```gdscript
item_id = "armor_bone_lord_body"
display_name = "骸骨领主板甲"
description = "一副由各种生物骨骼与骨丝拼接而成的恐怖板甲，板甲的每一块甲片都来自不同的生物——龙肋骨、巨人腿骨、恶魔臂骨、人类脊椎。这些骨骼被魔法拼接在一起，形成了一副既恐怖又威严的铠甲。板甲的接缝处不断有骨粉在脱落。\n\n奥西里斯在锻造这副板甲时，收集了他能找到的所有强大生物的骨骼。他说："这副板甲不只是铠甲，它是'骨骼博物馆'——让我穿着所有曾经强大的生物的力量。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的骨骼「重组」——每日一次，改变板甲的形状以适应不同的战斗需求（例如增加胸甲厚度以抵抗钝击、增加骨刺以反伤 melee 攻击者、或减轻重量以增加移动力）。奥西里斯说："骨骼不是固定的，它是'可塑的'——只要你有足够的知识和力量来重塑它。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "bone_lord_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_bone_lord_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_bone_lord_body" }
]
```

---

### 62.3 骸骨领主护手

```gdscript
item_id = "armor_bone_lord_hands"
display_name = "骸骨领主护手"
description = "一对由各种生物骨骼与骨丝拼接而成的恐怖护手，护手的骨骼可以「重组」——在需要时可以伸出额外的骨刺、骨爪或骨盾。手套的内部有「魂火」在燃烧——不是真正的火，是亡灵的能量，让手套可以保持活动。\n\n奥西里斯在锻造这对手套时，将自己的双手骨骼改造成了「万能工具」。他说："这对手套不只是手套，它们是'骨骼工具箱'——让我可以创造出任何我需要的骨骼工具。"\n\n这对手套的特殊效果是：穿戴者可以用骨骼「创造」临时武器—— bonus action，从手套中伸出一把骨剑（1D8 slashing）、骨矛（1D10 piercing）或骨盾（AC +2）。武器持续 1 分钟，然后碎裂。每日三次。且可以用手套「骨化」一个触碰的目标—— bonus action，触碰一个生物，目标的一部分骨骼变硬（速度 -10，但 AC +1，持续 1 回合）。奥西里斯说："骨骼不是只能用来攻击，它也可以用来防御——甚至用来帮助盟友。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "bone_lord_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_bone_lord_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_bone_lord_hands" }
]
```

---

### 62.4 骸骨领主胫甲

```gdscript
item_id = "armor_bone_lord_feet"
display_name = "骸骨领主胫甲"
description = "一对由各种生物骨骼与骨丝拼接而成的恐怖胫甲，胫甲的每一步都会发出骨骼摩擦的声音——咔嗒、咔嗒、咔嗒。这不是弱点，是警告，告诉所有人：死亡正在接近。\n\n奥西里斯在锻造这对胫甲时，将自己的双脚骨骼改造成了「无声行走者」。他说："这对胫甲不只是鞋子，它们是'死亡的节拍器'——每一步都是死亡的倒计时。"\n\n这对胫甲的特殊效果是：穿戴者可以「骨骼步」——在骨骼上（包括尸体、坟墓、甚至自己的骨骼）正常行走，速度 +10。且每日一次，可以进行「骨骼冲锋」——以三倍速度直线冲锋，路径上留下骨骼碎片（碎片形成 difficult terrain，任何踩到的生物受到 1D6 piercing）。奥西里斯说："死亡不是终点，它是'道路'——一条由骨骼铺成的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "bone_lord_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_bone_lord_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_bone_lord_feet" }
]
```

---

## 套装六十三：荆棘女王（Thorn Queen）

> *"美丽不是无害的，玫瑰有刺，最娇艳的玫瑰有最锋利的刺。"*

**套装主题**：荆棘花园的守护者「血玫瑰」的装备。这个花园由一种会吸血的黑玫瑰覆盖，守护者学会了与这些玫瑰共生，用它们的刺来防御和攻击。集齐四件时，穿戴者获得「荆棘之躯」——可以操控荆棘、释放毒刺、用玫瑰治愈自己。

**历史渊源**：血玫瑰的创造者「荆棘之母」芙洛拉（另一位同名者），是一位据说与黑玫瑰灵魂融合的女巫。她说："我不是玫瑰的主人，我是它的'共生者'——我给它血，它给我力量。"

---

### 63.1 荆棘女王头冠

```gdscript
item_id = "armor_thorn_queen_head"
display_name = "荆棘女王头冠"
description = "一顶由黑玫瑰藤蔓与荆棘丝编织而成的优雅头冠，头冠上绽放着一朵永不凋谢的黑玫瑰。这朵玫瑰不是装饰，它是活的——它会随着穿戴者的情绪变化：平静时是闭合的，兴奋时是半开的，愤怒时是完全绽放的。玫瑰的刺会保护穿戴者，攻击任何试图靠近的敌人。\n\n芙洛拉在编织这头冠时，将自己的一根头发编入了玫瑰的根系。她说："这朵玫瑰不是普通的玫瑰，它是'我的延伸'——我用我的血喂养它，它用它的刺保护我。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 30 尺内所有植物的位置和状态（如同植物感知）。且可以通过头冠「命令」黑玫瑰——让周围的玫瑰生长、攻击或防御（15 尺半径内的玫瑰听从命令）。芙洛拉说："玫瑰不是装饰品，它是'武器'——一种美丽而致命的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "thorn_queen_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_thorn_queen_head" },
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_thorn_queen_head" }
]
```

**2件套（设计预留）**：`nature_bonus` +3；免疫「植物毒素」和「自然诅咒」。
**4件套（设计预留）**：每日三次「荆棘鞭挞」： bonus action，命令 15 尺内的荆棘攻击一个目标（2D8 piercing + 1D6 poison，目标须通过 DC16 敏捷豁免，失败则 grappled 1 回合）；每日一次「玫瑰治愈」： bonus action，用玫瑰的刺吸取自己的血（受到 1D6 piercing），恢复 3D8 HP（玫瑰将血液转化为治愈能量）；任何 melee 攻击穿戴者的生物受到 1D6 piercing（荆棘反伤）。

---

### 63.2 荆棘女王鳞甲

```gdscript
item_id = "armor_thorn_queen_body"
display_name = "荆棘女王鳞甲"
description = "一副由黑玫瑰花瓣与荆棘丝编织而成的优雅鳞甲，鳞甲表面覆盖着类似玫瑰花瓣的纹理——每一片花瓣都来自不同的黑玫瑰，因此呈现出深浅不一的深红色。鳞甲的接缝处不断有小型玫瑰在生长和凋谢——生长代表生命的延续，凋谢代表生命的循环。\n\n芙洛拉在编织这副鳞甲时，将花园中所有最美丽的黑玫瑰花瓣都收集了起来。她说："这副鳞甲不只是铠甲，它是'花园'——让我穿着我的花园。"\n\n这副鳞甲的特殊效果是：穿戴者可以将鳞甲的玫瑰花瓣「释放」——每日一次，释放所有花瓣形成一个玫瑰风暴（15 尺半径，所有生物受到 2D8 slashing 并被 blinded 1 回合，因为花瓣遮蔽视线）。且鳞甲会「吸血」——任何 melee 攻击穿戴者的生物受到 1D6 piercing，穿戴者恢复等量 HP（玫瑰将敌人的血转化为治愈能量）。芙洛拉说："玫瑰不是只能被给予，它也可以被索取——而索取有时候比给予更美丽。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "thorn_queen_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_thorn_queen_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_thorn_queen_body" }
]
```

---

### 63.3 荆棘女王手套

```gdscript
item_id = "armor_thorn_queen_hands"
display_name = "荆棘女王手套"
description = "一对由黑玫瑰藤蔓与荆棘丝编织而成的优雅手套，手套表面覆盖着细密的荆棘。这些荆棘会在需要时伸出，在不需要时缩回。手套的指尖嵌有玫瑰刺——不是为攻击，而是为精确注射毒素。\n\n芙洛拉在编织这对手套时，将自己的指尖改造成了「玫瑰刺」。她说："这对手套不只是手套，它们是'玫瑰的刺'——让我可以用触碰注入美丽和死亡。"\n\n这对手套的特殊效果是：穿戴者可以用玫瑰刺进行「精确毒击」—— bonus action，触碰一个目标，注入玫瑰毒素（目标须通过 DC16 体质豁免，失败则 poisoned 1 分钟，每回合受到 2D6 poison，且魅力 -2，因为面容被毒素侵蚀）。每日两次。且可以用手套「种植」玫瑰—— bonus action，在地面上种下一颗玫瑰种子（1 回合后长成带刺的玫瑰丛，5 尺半径困难地形，任何进入的生物受到 1D6 piercing）。芙洛拉说："最好的花园不是最整齐的，是最危险的——因为危险让人记住。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "thorn_queen_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_thorn_queen_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_thorn_queen_hands" }
]
```

---

### 63.4 荆棘女王软靴

```gdscript
item_id = "armor_thorn_queen_feet"
display_name = "荆棘女王软靴"
description = "一对由黑玫瑰藤蔓与荆棘丝编织而成的优雅软靴，软靴表面覆盖着细密的荆棘。每一步踏下，荆棘会在地面上留下短暂的玫瑰足迹——不是弱点，是美丽，也是陷阱（任何踩到足迹的生物受到 1D4 piercing）。\n\n芙洛拉在编织这对软靴时，将「玫瑰之步」能力封入了藤蔓。她说："这对软靴不只是鞋子，它们是'花园的延伸'——让我每一步都在大地上播种玫瑰。"\n\n这对软靴的特殊效果是：穿戴者可以在荆棘上正常行走（免疫 difficult terrain from plants）。且可以用「荆棘冲刺」——每日一次，以三倍速度直线冲锋，路径上留下荆棘轨迹（轨迹持续 1 分钟，任何踩到的生物受到 2D6 piercing 并须通过 DC14 敏捷豁免，失败则 grappled 1 回合）。芙洛拉说："最好的道路不是最平坦的，是最美丽的——即使美丽意味着危险。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "thorn_queen_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_thorn_queen_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_thorn_queen_feet" }
]
```

---

## 套装六十四：灰烬行者（Ash Walker）

> *"火燃尽了，但灰烬记得。我学会了在灰烬中行走，在余烬中寻找答案。"*

**套装主题**：灰烬废墟的幸存者「余烬之誓」的装备。这个教团的成员在一场毁灭性的大火中幸存，他们的装备由灰烬和余烬锻造，赋予了穿戴者操控灰烬、释放余烬攻击和从灰烬中重生的能力。集齐四件时，穿戴者获得「灰烬之躯」——可以化作灰烬移动、释放灰烬风暴、从灰烬中重生。

**历史渊源**：余烬之誓的领袖「灰烬之子」普罗米修斯（另一位同名者），是一位在神罚之火中幸存并被永远燃烧的悲剧英雄。他说："我不是被火惩罚的，我是被火'选中'的——它燃尽了我的一切，但留下了我。"

---

### 64.1 灰烬行者头巾

```gdscript
item_id = "armor_ash_walker_head"
display_name = "灰烬行者头巾"
description = "一条由灰烬与余烬丝编织而成的灰色头巾，头巾表面不断有细小的灰烬在脱落。头巾只露出双眼，但那双眼不是活人的眼睛——它们是灰色的、空洞的，仿佛已经被火焰烧尽了所有色彩。头巾的正面镶嵌着一颗「余烬之心」——一颗能够储存火焰并在需要时释放的魔法宝石。\n\n普罗米修斯在编织这条头巾时，将自己被神罚之火燃烧后的灰烬都收集了起来。他说："这条头巾不只是遮挡，它是'我的过去'——让我永远记住那场火，以及火后的一切。"\n\n这条头巾的特殊效果是：穿戴者可以「感知」到 60 尺内任何火焰或热源的位置和强度（如同 thermal sense）。且可以通过头巾「吸收」火焰——受到 fire damage 时，50% 概率将伤害转化为「余烬能量」储存（最多储存 20 点）。普罗米修斯说："火不是我的敌人，它是'我的记忆'——每一次火焰都让我想起那场神罚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "ash_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ash_walker_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ash_walker_head" }
]
```

**2件套（设计预留）**：`resistance_fire` +15；免疫「烟雾窒息」和「火焰致盲」。
**4件套（设计预留）**：每日两次「灰烬形态」： bonus action，化作一团灰烬 1 回合（免疫所有物理伤害，可以穿过缝隙，速度 +20）；每日一次「灰烬风暴」：15 尺半径内所有生物受到 3D10 fire 并被 blinded 1 回合（灰烬遮蔽视线）；可以用「余烬治疗」：消耗 5 点余烬能量，恢复 2D8 HP（从灰烬中重生）；在火焰中移动力 +10（灰烬随风飘动）。

---

### 64.2 灰烬行者长袍

```gdscript
item_id = "armor_ash_walker_body"
display_name = "灰烬行者长袍"
description = "一件由灰烬与余烬丝编织而成的灰色长袍，长袍表面不断有细小的灰烬在脱落。这件长袍没有固定的形状——它会随着风飘动，如同一团活着的灰烬。在火焰中，长袍会发出微弱的橙红色光芒；在黑暗中，长袍则几乎不可见。\n\n普罗米修斯在编织这件长袍时，将自己被神罚之火燃烧后的所有灰烬都收集了起来。他说："这件长袍不只是衣服，它是'我的灰烬'——让我穿着我的过去，走向我的未来。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的灰烬「释放」——每日一次，释放所有灰烬形成一个灰烬云（15 尺半径，困难地形，所有生物在区域内攻击检定 -3 并被 blinded，持续 1 分钟）。且长袍会「吸收」火焰——在火焰中，长袍的 AC +2（余烬强化）。普罗米修斯说："灰烬不是结束，它是'开始'——每一团灰烬都曾经是火焰，也终将成为火焰。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "ash_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ash_walker_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ash_walker_body" }
]
```

---

### 64.3 灰烬行者手套

```gdscript
item_id = "armor_ash_walker_hands"
display_name = "灰烬行者手套"
description = "一对由灰烬与余烬丝编织而成的灰色手套，手套表面不断有微弱的火星在闪烁。触碰任何物体时，手套会留下一层灰烬——不是脏，是标记，也是警告。\n\n普罗米修斯在编织这对手套时，将自己的双手改造成了「灰烬之触」。他说："这对手套不只是手套，它们是'火的余烬'——让我可以用触碰留下火的记忆。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「点燃」或「熄灭」—— bonus action，触碰一个物体，可以选择点燃它（如果可燃）或熄灭它（如果正在燃烧）。且可以用手套「释放」余烬——每日三次，向前推出一道余烬流（15 尺锥形，2D10 fire，目标须通过 DC14 敏捷豁免，失败则 blinded 1 回合）。普罗米修斯说："火不是只能创造，它也可以毁灭——而毁灭有时候比创造更重要。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "ash_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ash_walker_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ash_walker_hands" }
]
```

---

### 64.4 灰烬行者便鞋

```gdscript
item_id = "armor_ash_walker_feet"
display_name = "灰烬行者便鞋"
description = "一对由灰烬与余烬丝编织而成的灰色便鞋，便鞋表面不断有细小的灰烬在脱落。行走时，便鞋不会发出任何声音——因为灰烬吸收了所有声音。每一步踏下，都会留下短暂的灰烬足迹——不是弱点，是标记，也是陷阱（灰烬会让地面变得滑腻）。\n\n普罗米修斯在编织这对便鞋时，将自己的双脚改造成了「灰烬之步」。他说："这对便鞋不只是鞋子，它们是'灰烬的道路'——让我可以在任何地方行走，不留下任何可以被追踪的痕迹。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（灰烬吸收声音）。且可以在灰烬上正常行走（包括火场、灰烬堆、甚至正在燃烧的地面）。且可以用「灰烬滑行」——每日一次，化作一团灰烬滑行（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。普罗米修斯说："灰烬不是终点，它是'道路'——一条由火的记忆铺成的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "ash_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ash_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_ash_walker_feet" }
]
```

---

## 套装六十五：迷雾行者（Mist Walker）

> *"雾不是遮蔽，它是'揭示'——它遮蔽了远处，但揭示了附近。在雾中，只有近距离的东西才是真实的。"*

**套装主题**：迷雾沼泽的居民「雾之子」的装备。这些居民生活在永恒的迷雾中，他们的装备由雾气和沼泽材料制成，赋予了穿戴者在迷雾中隐匿、释放雾气和操控湿气的能力。集齐四件时，穿戴者获得「雾之躯」——可以化作雾气移动、在雾中隐形、释放毒雾。

**历史渊源**：雾之子的长老「雾之心」涅贝利娅，是一位据说由纯粹雾气诞生的神秘存在。她说："我不是人类，我不是精灵——我是'雾'，只是在某些时候选择了人形。"

---

### 65.1 迷雾行者兜帽

```gdscript
item_id = "armor_mist_walker_head"
display_name = "迷雾行者兜帽"
description = "一顶由凝结雾气与湿气丝编织而成的奇异兜帽，兜帽呈现出半透明的灰白色——你可以看到后面的东西，但一切都变得模糊。兜帽的边缘不断有雾气在流动，仿佛穿戴者的头部被一团小型云雾包围。\n\n涅贝利娅在编织这顶兜帽时，将一整片沼泽的雾气都压缩成了丝线。她说："这顶兜帽不只是兜帽，它是'雾的浓缩'——让我穿着一团雾。"\n\n这顶兜帽的特殊效果是：穿戴者可以「感知」到 30 尺内所有生物的位置（通过雾气的扰动，如同 blindsight 30 尺）。且在雾中，穿戴者获得 +3 AC（雾 camouflage）。涅贝利娅说："雾不是弱点，它是'主场'——在雾中，我是不可战胜的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "mist_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mist_walker_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mist_walker_head" }
]
```

**2件套（设计预留）**：在雾中 `stealth_bonus` +5；`perception_bonus` +2。
**4件套（设计预留）**：每日两次「雾形态」： bonus action，化作一团雾 1 回合（免疫所有物理伤害，可以穿过缝隙，速度 +10）；每日一次「毒雾释放」：15 尺半径内所有生物受到 3D10 poison，须通过 DC16 体质豁免，失败则 poisoned 1 分钟；在雾中攻击检定 +2、被攻击时 25% 概率 miss（雾 camouflage）；可以「操控雾气」： bonus action，改变 30 尺内雾气的浓度和方向。

---

### 65.2 迷雾行者长袍

```gdscript
item_id = "armor_mist_walker_body"
display_name = "迷雾行者长袍"
description = "一件由凝结雾气与湿气丝编织而成的奇异长袍，长袍呈现出半透明的灰白色。这件长袍没有固定的形状——它会随着空气流动而变化，仿佛穿戴者本身就是一团雾。在潮湿环境中，长袍会变得更加浓密；在干燥环境中，长袍则几乎消失。\n\n涅贝利娅在编织这件长袍时，将整片沼泽的雾气都编入了丝线。她说："这件长袍不只是衣服，它是'雾的化身'——让我穿着整个沼泽的雾。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的雾气「释放」——每日一次，在 15 尺半径内创造一个浓雾区域（困难地形，所有生物在区域内攻击检定 -3 且无法看见 5 尺外的东西，持续 1 分钟）。且长袍会「吸收」湿气——在潮湿环境中，每小时恢复 1 HP（湿气治愈）。涅贝利娅说："雾不是只能遮蔽，它也可以治愈——只是大多数人只看见了它的遮蔽。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "mist_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mist_walker_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mist_walker_body" }
]
```

---

### 65.3 迷雾行者手套

```gdscript
item_id = "armor_mist_walker_hands"
display_name = "迷雾行者手套"
description = "一对由凝结雾气与湿气丝编织而成的奇异手套，手套呈现出半透明的灰白色。挥舞手臂时，手套会拉出长长的雾气——不是普通的雾，是「活雾」，可以短暂地脱离身体独立行动。\n\n涅贝利娅在编织这对手套时，将自己的「雾之手」能力封入了湿气。她说："这对手套不只是手套，它们是'雾的延伸'——让我可以用手操控雾气。"\n\n这对手套的特殊效果是：穿戴者可以用雾气「缠绕」一个 15 尺内的目标——雾气会伸长并缠绕目标（目标须通过 DC16 力量豁免，失败则 grappled 1 回合，因为雾气变得浓稠如绳）。每日三次。且可以用手套「凝结」湿气—— bonus action，在手中凝结出一团水球（可以作为武器投掷，1D6 bludgeoning，或用来熄灭火焰）。涅贝利娅说："雾不是只能遮蔽，它也可以束缚——只是大多数人只看见了它的遮蔽。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "mist_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mist_walker_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mist_walker_hands" }
]
```

---

### 65.4 迷雾行者便鞋

```gdscript
item_id = "armor_mist_walker_feet"
display_name = "迷雾行者便鞋"
description = "一对由凝结雾气与湿气丝编织而成的奇异便鞋，便鞋呈现出半透明的灰白色。行走时，便鞋不会发出任何声音——因为雾气吸收了所有声音。每一步踏下，都会留下短暂的雾气足迹——不是弱点，是标记，也是陷阱（雾气会让地面变得湿滑）。\n\n涅贝利娅在编织这对便鞋时，将自己的「雾之步」能力封入了湿气。她说："这对便鞋不只是鞋子，它们是'雾的移动'——让我可以像雾一样移动。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（雾气吸收声音）。且可以在湿滑的表面上正常行走（雾气增加了摩擦力）。且可以用「雾之滑行」——每日一次，化作一团雾气滑行（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。涅贝利娅说："雾不是只能遮蔽，它也可以移动——只是大多数人只看见了它的遮蔽。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "mist_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mist_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_mist_walker_feet" }
]
```

---

## 套装六十六：铁处女（Iron Maiden）

> *"疼痛不是惩罚，它是'净化'。只有在极致的痛苦中，灵魂才能得到升华。"*

**套装主题**：痛苦修道会「永恒之痛」的装备。这个修道会的成员相信痛苦是通往神圣的必经之路，他们的装备由带刺的金属和血锻铁制成，赋予了穿戴者将痛苦转化为力量、用荆棘攻击和承受极端伤害的能力。集齐四件时，穿戴者获得「痛苦之躯」——可以将受到的伤害转化为攻击力、用荆棘反伤、从痛苦中恢复。

**历史渊源**：永恒之痛的最高修女「痛苦之母」玛丽亚（另一位同名者），是一位据说从未感受过无痛时刻的圣徒。她说："我不是在受苦，我是在'祈祷'——每一次疼痛都是对神圣的致敬。"

---

### 66.1 铁处女头冠

```gdscript
item_id = "armor_iron_maiden_head"
display_name = "铁处女头冠"
description = "一顶由带刺金属与血锻铁锻造的恐怖头冠，头冠内侧有细密的尖刺——不是为攻击敌人，是为刺穿戴者自己。头冠佩戴时，尖刺会轻轻刺入头皮，产生持续的轻微疼痛——这不是折磨，是「提醒」，提醒穿戴者痛苦的存在。头冠的外侧镶嵌着一颗「血晶」——一颗由无数痛苦的泪水凝结而成的宝石。\n\n玛丽亚在锻造这头冠时，将自己的头皮 blood 都献给了血晶。她说："这头冠不只是头冠，它是'冠冕'——一顶用痛苦和 blood 铸造的冠冕。"\n\n这头冠的特殊效果是：穿戴者受到的 pain 会转化为力量——每失去 10% 最大 HP，攻击检定 +1（最多 +5）。且可以通过头冠「释放」痛苦——每日一次，将所有受到的痛苦转化为一次强大的攻击（伤害 = 本战斗受到的总伤害 ×0.5，一次性释放）。玛丽亚说："痛苦不是弱点，它是'燃料'——一种可以转化为力量的燃料。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "iron_maiden_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 21000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_iron_maiden_head" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_iron_maiden_head" }
]
```

**2件套（设计预留）**：`intimidation_bonus` +3；每失去 10% HP，伤害 +1（痛苦强化）。
**4件套（设计预留）**：任何 melee 攻击穿戴者的生物受到 1D8 piercing（荆棘反伤）；每日一次「痛苦转化」： bonus action，将当前失去的所有 HP 转化为攻击力（下一击伤害 +失去的 HP ×0.3）；每日一次「痛苦治愈」： bonus action，受到 2D10 piercing（自残），恢复 4D10 HP（痛苦唤醒身体的再生能力）；免疫「恐惧」和「魅惑」（痛苦让人清醒）。

---

### 66.2 铁处女板甲

```gdscript
item_id = "armor_iron_maiden_body"
display_name = "铁处女板甲"
description = "一副由带刺金属与血锻铁锻造的恐怖板甲，板甲内侧布满尖刺——不是为了攻击，是为了持续刺激穿戴者的痛觉神经。板甲的外侧也有尖刺——这些是为了攻击敌人。这副板甲是有「生命」的——它会根据穿戴者的痛苦程度变化：痛苦越深，板甲的尖刺越长，防御力越高。\n\n玛丽亚在锻造这副板甲时，将自己的 blood 都涂在了板甲上。她说："这副板甲不只是铠甲，它是'刑具'——一种我自愿穿戴的刑具，因为它让我更接近神圣。"\n\n这副板甲的特殊效果是：穿戴者受到的 damage 会「强化」板甲——每受到 10 点 damage，板甲的 AC +1（最多 +3），持续 1 回合。且可以将板甲的尖刺「释放」——每日一次，释放所有尖刺形成一个荆棘风暴（5 尺半径，所有生物受到 3D8 piercing，须通过 DC16 敏捷豁免，失败则 bleeding 1 分钟）。玛丽亚说："痛苦不是只能承受，它也可以分享——分享给那些试图伤害我的人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "iron_maiden_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 36000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_iron_maiden_body" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_iron_maiden_body" }
]
```

---

### 66.3 铁处女护手

```gdscript
item_id = "armor_iron_maiden_hands"
display_name = "铁处女护手"
description = "一对由带刺金属与血锻铁锻造的恐怖护手，护手内侧有细密的尖刺——不是为了攻击，是为了持续刺激手掌的痛觉神经。护手的外侧有可伸缩的荆棘——在需要时可以伸出，在不需要时缩回。\n\n玛丽亚在锻造这对手套时，将自己的手掌 blood 都涂在了护手上。她说："这对手套不只是手套，它们是'荆棘之手'——让我可以用痛苦触碰世界。"\n\n这对手套的特殊效果是：穿戴者可以用荆棘进行「痛苦攻击」——徒手攻击造成 1D8 piercing + 1D6 psychic（痛苦不仅是物理的，也是精神的）。且可以用手套「共享痛苦」—— bonus action，触碰一个目标，将自己受到的一半痛苦转移给目标（目标受到穿戴者本回合受到伤害的一半，无视护甲）。每日一次。玛丽亚说："痛苦不是只能独自承受的，它也可以分享——分享痛苦是最高级的慈悲。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "iron_maiden_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_iron_maiden_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_iron_maiden_hands" }
]
```

---

### 66.4 铁处女胫甲

```gdscript
item_id = "armor_iron_maiden_feet"
display_name = "铁处女胫甲"
description = "一对由带刺金属与血锻铁锻造的恐怖胫甲，胫甲内侧有细密的尖刺——不是为了攻击，是为了持续刺激脚底的痛觉神经。每一步踏下，尖刺都会刺入脚底，产生疼痛——这不是折磨，是「冥想」，让穿戴者每一步都保持清醒。\n\n玛丽亚在锻造这对胫甲时，将自己的脚底 blood 都涂在了胫甲上。她说："这对胫甲不只是鞋子，它们是'荆棘之路'——让我每一步都走在痛苦中，从而更接近神圣。"\n\n这对胫甲的特殊效果是：穿戴者的每一步都会产生「痛苦共鸣」——每走一步，攻击检定 +0.5（最多 +3，持续 1 回合）。且可以进行「痛苦冲锋」——每日一次，以三倍速度直线冲锋，路径上留下血迹（血迹形成 difficult terrain，任何踩到的生物受到 1D6 psychic，因为感受到残留的痛苦）。玛丽亚说："最好的道路不是最平坦的，是最痛苦的——因为痛苦让人成长。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "iron_maiden_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_iron_maiden_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_iron_maiden_feet" }
]
```

---

## 套装六十七：蜘蛛女王（Spider Queen）

> *"网不是陷阱，它是'艺术'。每一条丝线都是精心计算的，每一个交叉点都是完美的。"*

**套装主题**：蜘蛛教派「永丝之网」的装备。这个教派的成员崇拜蜘蛛，学会了编织丝线、操控毒液和在蛛网上移动。集齐四件时，穿戴者获得「蜘蛛之躯」——可以编织魔法蛛网、释放毒液、在墙壁上行走。

**历史渊源**：永丝之网的最高祭司「网之心」阿拉克涅（另一位同名者），是一位据说与蜘蛛女神达成了交易的传奇织女。她说："我不是蜘蛛的女神，我是她的'学生'——她教会了我编织的艺术，我教会了她人类的野心。"

---

### 67.1 蜘蛛女王头冠

```gdscript
item_id = "armor_spider_queen_head"
display_name = "蜘蛛女王头冠"
description = "一顶由蜘蛛丝与毒腺丝编织而成的优雅头冠，头冠上镶嵌着八颗小型「蜘蛛眼石」——每颗眼石代表一只蜘蛛的眼睛。头冠佩戴时，眼石会发出微弱的绿光，让穿戴者可以获得蜘蛛的视野。\n\n阿拉克涅在编织这头冠时，从一只巨型蜘蛛身上取下了它的八只眼睛。她说："这八只眼睛不是被取下的，是'被借用的'——它们仍然与我相连，让我可以同时看见八个方向。"\n\n这头冠的特殊效果是：穿戴者获得「蜘蛛视野」——可以同时看见前方和后方（免疫 flanking 的偷袭加值）。且可以通过头冠「感知」到 30 尺内所有蛛网的振动（如同 tremorsense，但只对蛛网有效）。阿拉克涅说："蜘蛛不是只能看见前方，它可以看到所有方向——因为危险可能来自任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "spider_queen_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_spider_queen_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_spider_queen_head" }
]
```

**2件套（设计预留）**：免疫「蛛网束缚」和「蜘蛛毒素」；`perception_bonus` +2。
**4件套（设计预留）**：每日三次「编织蛛网」： bonus action，在 30 尺内编织一个 15 尺半径的蛛网（困难地形，所有生物须通过 DC16 敏捷豁免，失败则 restrained）；近战攻击附加 1D6 poison（蜘蛛毒液）；可以在墙壁和天花板上正常行走（蜘蛛攀爬）；每日一次「召唤蜘蛛」：召唤 1D6 只巨型蜘蛛（持续 10 分钟）。

---

### 67.2 蜘蛛女王鳞甲

```gdscript
item_id = "armor_spider_queen_body"
display_name = "蜘蛛女王鳞甲"
description = "一副由蜘蛛丝与毒腺丝编织而成的优雅鳞甲，鳞甲表面覆盖着类似蜘蛛腹部的纹理——每一片鳞片都来自不同的巨型蜘蛛，因此呈现出深浅不一的黑色和棕色。鳞甲的接缝处不断有小型蜘蛛在游走——它们是鳞甲的一部分，会修复任何损伤。\n\n阿拉克涅在编织这副鳞甲时，将所有她收集的蜘蛛丝都编入了鳞甲。她说："这副鳞甲不只是铠甲，它是'蛛网'——一张穿在身上的蛛网。"\n\n这副鳞甲的特殊效果是：穿戴者可以在蛛网上以三倍速度移动（蜘蛛之速）。且鳞甲会「自动修复」——每小时恢复 1 HP（小型蜘蛛的修复工作）。且可以将鳞甲的蛛丝「释放」——每日一次，释放所有蛛丝形成一个巨大的蛛网（30 尺半径，所有生物 restrained，须通过 DC16 力量豁免才能挣脱）。阿拉克涅说："蛛网不是只能困住敌人，它也可以保护自己——只是需要正确的编织方式。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "spider_queen_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_spider_queen_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_spider_queen_body" }
]
```

---

### 67.3 蜘蛛女王手套

```gdscript
item_id = "armor_spider_queen_hands"
display_name = "蜘蛛女王手套"
description = "一对由蜘蛛丝与毒腺丝编织而成的优雅手套，手套表面覆盖着细密的蛛丝。手套的指尖有可伸缩的毒牙——不是为攻击，而是为精确注射毒液。手套的掌心有特殊的蛛丝腺——可以随时随地编织蛛丝。\n\n阿拉克涅在编织这对手套时，将自己的指尖改造成了「蜘蛛毒牙」。她说："这对手套不只是手套，它们是'蜘蛛的嘴'——让我可以用触碰注入毒液。"\n\n这对手套的特殊效果是：穿戴者可以用毒牙进行「精确毒击」—— bonus action，触碰一个目标，注入蜘蛛毒液（目标须通过 DC16 体质豁免，失败则 poisoned 1 分钟，每回合受到 2D6 poison，且 speed 减半）。每日两次。且可以用手套「编织」蛛丝—— bonus action，编织出 30 尺长的蛛丝（可以用来攀爬、捆绑或制造陷阱）。每日三次。阿拉克涅说："蜘蛛的丝不是只能用来困住，它也可以用来创造——创造桥梁、创造绳索、创造艺术。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "spider_queen_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_spider_queen_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_spider_queen_hands" }
]
```

---

### 67.4 蜘蛛女王软靴

```gdscript
item_id = "armor_spider_queen_feet"
display_name = "蜘蛛女王软靴"
description = "一对由蜘蛛丝与毒腺丝编织而成的优雅软靴，软靴表面覆盖着细密的蛛丝。靴底有特殊的「蛛丝垫」——不是为了防滑，而是为了在蛛网上保持抓握力。在蛛网上行走时，软靴会让穿戴者如同与蛛网融为一体。\n\n阿拉克涅在编织这对软靴时，将「蜘蛛之步」能力封入了蛛丝。她说："这对软靴不只是鞋子，它们是'蛛网的延伸'——让我可以在蛛网上自由移动。"\n\n这对软靴的特殊效果是：穿戴者可以在蛛网上以三倍速度移动（蜘蛛之速）。且可以在墙壁和天花板上正常行走（蜘蛛攀爬）。且可以用「蛛丝跳跃」——每日一次，利用蛛丝的弹性进行一次超高跳跃（跳跃距离 ×3，且可以在空中改变方向一次）。阿拉克涅说："蜘蛛不是只能爬行，它也可以跳跃——只是大多数人只看见了它的网。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "spider_queen_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_spider_queen_feet" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_spider_queen_feet" }
]
```

---

## 套装六十八：乌鸦领主（Raven Lord）

> *"乌鸦不是死亡的使者，它是'信息的收集者'。它知道一切，只是不会说话——除非你愿意倾听。"*

**套装主题**：乌鸦密教「黑羽之誓」的装备。这个密教的成员与乌鸦建立了特殊的联系，学会了通过乌鸦收集信息、操控死亡气息和释放暗影攻击。集齐四件时，穿戴者获得「乌鸦之躯」——可以召唤乌鸦群、通过乌鸦的眼睛观察、释放暗影和死亡攻击。

**历史渊源**：黑羽之誓的最高祭司「鸦王」奥丁（另一位同名者），是一位据说能够同时通过一千只乌鸦的眼睛观察世界的传奇祭司。他说："我不是一个人，我是'一千个视角'——每一只乌鸦都是我的另一只眼睛。"

---

### 68.1 乌鸦领主头冠

```gdscript
item_id = "armor_raven_lord_head"
display_name = "乌鸦领主头冠"
description = "一顶由乌鸦羽毛与暗影丝编织而成的威严头冠，头冠上镶嵌着一颗「鸦之眼」——一颗由真正乌鸦的眼睛制成的宝石。头冠佩戴时，鸦之眼会发出微弱的黑光，让穿戴者可以获得乌鸦的视野。头冠的两侧各有一支巨大的乌鸦翅膀——不是装饰，是真正的乌鸦翅膀，可以让穿戴者在坠落时滑翔。\n\n奥丁在编织这头冠时，从他最忠诚的乌鸦「思想」身上取下了一根羽毛。他说："这根羽毛不是被取下的，是'被给予的'——思想选择了我，让我成为它的延伸。"\n\n这头冠的特殊效果是：穿戴者可以「连接」一只乌鸦——每日一次，与 1 里内的一只乌鸦建立心灵连接，可以通过乌鸦的眼睛观察（持续 1 小时）。且可以通过头冠「听懂」乌鸦的语言——乌鸦会告诉穿戴者它们看到和听到的一切（自动获得 1 里内的情报）。奥丁说："乌鸦不是只会叫，它会说话——只是你需要学会它的语言。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "raven_lord_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raven_lord_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_raven_lord_head" }
]
```

**2件套（设计预留）**：`perception_bonus` +3；`investigation_bonus` +3。
**4件套（设计预留）**：每日一次「召唤鸦群」：召唤 1D6 只巨型乌鸦（持续 10 分钟，乌鸦可以攻击、侦察或传递信息）；每日一次「死亡之喙」：命令鸦群攻击一个目标（4D8 piercing + 2D6 necrotic，目标须通过 DC16 敏捷豁免，失败则 blinded 1 回合）；可以通过任何乌鸦的眼睛观察（无需动作，持续连接）；免疫「死亡气息」（necrotic aura 不影响）。

---

### 68.2 乌鸦领主长袍

```gdscript
item_id = "armor_raven_lord_body"
display_name = "乌鸦领主长袍"
description = "一件由乌鸦羽毛与暗影丝编织而成的威严长袍，长袍表面覆盖着密密麻麻的乌鸦羽毛——每一片羽毛都来自不同的乌鸦，因此呈现出深浅不一的黑色。长袍的接缝处不断有小型乌鸦在进出——它们是长袍的一部分，也是穿戴者的「信使」。\n\n奥丁在编织这件长袍时，收集了所有他遇到的乌鸦的羽毛。他说："这件长袍不只是衣服，它是'乌鸦的巢'——让我穿着整个乌鸦群落。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的乌鸦「释放」——每日一次，释放所有乌鸦形成一个乌鸦风暴（15 尺半径，困难地形，所有生物在区域内攻击检定 -3 并被 blinded，因为乌鸦遮蔽视线，持续 1 分钟）。且长袍会「收集」死亡气息——在 undead 或濒死生物附近，每小时恢复 1 HP（死亡气息治愈）。奥丁说："乌鸦不是只能收集信息，它也可以收集力量——只是需要正确的方式。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "raven_lord_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_raven_lord_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raven_lord_body" }
]
```

---

### 68.3 乌鸦领主手套

```gdscript
item_id = "armor_raven_lord_hands"
display_name = "乌鸦领主手套"
description = "一对由乌鸦爪子与暗影丝编织而成的威严手套，手套表面覆盖着乌鸦爪子的纹理。指尖嵌有可伸缩的乌鸦爪——不是为攻击，而是为抓取和撕扯。手套的掌心有特殊的「暗影纹」——可以吸收和释放暗影能量。\n\n奥丁在编织这对手套时，从一只巨型乌鸦身上取下了它的爪子。他说："这对手套不只是手套，它们是'乌鸦的爪子'——让我可以用乌鸦的方式抓取世界。"\n\n这对手套的特殊效果是：穿戴者可以用乌鸦爪进行「撕裂攻击」——徒手攻击造成 1D8 slashing + 1D6 necrotic（暗影之力）。且可以用手套「释放」暗影——每日三次，向前推出一道暗影波（15 尺锥形，2D8 necrotic，目标须通过 DC14 智慧豁免，失败则 frightened 1 回合）。奥丁说："乌鸦的爪子不是只能抓取，它也可以释放——释放暗影，释放死亡，释放信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "raven_lord_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raven_lord_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raven_lord_hands" }
]
```

---

### 68.4 乌鸦领主软靴

```gdscript
item_id = "armor_raven_lord_feet"
display_name = "乌鸦领主软靴"
description = "一对由乌鸦爪子与暗影丝编织而成的威严软靴，软靴的形状不像人类的鞋子——它们更像乌鸦的爪子，弯曲而锋利。在陆地上行走时，软靴会很笨拙（移动力 -5），但在需要时，软靴可以让穿戴者在坠落时滑翔。\n\n奥丁在编织这对软靴时，从一只巨型乌鸦身上取下了它的爪垫。他说："这对软靴不只是鞋子，它们是'乌鸦的脚步'——让我可以像乌鸦一样移动。"\n\n这对软靴的特殊效果是：穿戴者可以从高处「滑翔」——免疫 falling damage（乌鸦翅膀滑翔）。且可以用「鸦步」——每日一次，化作一群乌鸦分散飞行（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合，然后在目标位置重组）。奥丁说："乌鸦不是只能飞，它也可以分散——分散成无数只，然后在任何地方重组。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "raven_lord_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raven_lord_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = -5, source_type = "equipment", source_id = "armor_raven_lord_feet" }
]
```

---

## 套装六十九：锈刃骑士（Rust Blade Knight）

> *"锈蚀不是衰败，它是'时间的美化'。每一道锈痕都是一段历史，每一片剥落的金属都是一个故事。"*

**套装主题**：锈蚀骑士团「永恒之锈」的装备。这个骑士团的成员崇拜锈蚀和时间的力量，他们的装备故意保持锈蚀状态，赋予了穿戴者操控锈蚀、削弱敌人和从衰败中汲取力量的能力。集齐四件时，穿戴者获得「锈蚀之躯」——可以释放锈蚀、削弱敌人的装备、从时间中汲取力量。

**历史渊源**：永恒之锈的最高骑士「锈之王」克罗诺斯（另一位同名者），是一位据说从古代遗迹中苏醒的时间守护者。他说："我不是老了，我是'成熟了'——就像好酒，时间越长，味道越浓。"

---

### 69.1 锈刃骑士头盔

```gdscript
item_id = "armor_rust_blade_head"
display_name = "锈刃骑士头盔"
description = "一顶由锈蚀金属与时间丝锻造的古老头盔，头盔表面覆盖着厚厚的锈层——不是因为没有保养，而是故意保留的。头盔的正面有一道巨大的裂缝，裂缝中可以看到内部的「时间之心」——一颗能够储存时间能量的魔法宝石。\n\n克罗诺斯在锻造这顶头盔时，将一千年的锈蚀都保留了下来。他说："这顶头盔不只是头盔，它是'时间的记录'——每一道锈痕都是一段历史。"\n\n这顶头盔的特殊效果是：穿戴者可以「感知」到任何物体的「年龄」——自动知道一个物体的制造年代和使用历史（如同物体阅读）。且可以通过头盔「加速」一个物体的锈蚀——每日一次，让一件金属装备瞬间锈蚀（武器攻击检定 -2，护甲 AC -2，持续 1 小时）。克罗诺斯说："时间不是敌人，它是'力量'——一种可以摧毁一切的力量。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "rust_blade_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_rust_blade_head" },
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_rust_blade_head" }
]
```

**2件套（设计预留）**：`history_bonus` +3；免疫「时间操控」和「老化」效果。
**4件套（设计预留）**：每日三次「锈蚀之触」： bonus action，触碰一件金属装备或构造体，使其锈蚀（攻击检定 -2 或 AC -2，持续 1 小时，或构造体受到 3D10 acid）；每日一次「时间吸取」： bonus action，从周围 15 尺内的所有生物身上吸取时间（所有生物 aging 1 年，穿戴者恢复 2D10 HP）；免疫「构造体」的弱点（锈蚀免疫）。

---

### 69.2 锈刃骑士板甲

```gdscript
item_id = "armor_rust_blade_body"
display_name = "锈刃骑士板甲"
description = "一副由锈蚀金属与时间丝锻造的古老板甲，板甲表面覆盖着厚厚的锈层。这副板甲是有「历史」的——每一块甲片都来自不同的时代、不同的战士、不同的战场。板甲的接缝处有特殊的「时间锁」——只有穿戴者可以打开，任何试图强行打开的人都会加速老化。\n\n克罗诺斯在锻造这副板甲时，收集了所有他找到的古老铠甲的碎片。他说："这副板甲不只是铠甲，它是'历史的拼图'——每一块碎片都是一个故事。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的「时间」释放——每日一次，释放板甲储存的时间能量，让周围 15 尺内的所有生物 aging 1D10 年（须通过 DC16 体质豁免，失败则 aging，成功则免疫）。且板甲会「吸收」时间——穿戴者每小时老化 1 个月，但作为交换，每小时恢复 1 HP（时间换取生命）。克罗诺斯说："时间不是免费的，但它可以用来交易——用未来的时间换取现在的生命。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "rust_blade_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_rust_blade_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_rust_blade_body" }
]
```

---

### 69.3 锈刃骑士护手

```gdscript
item_id = "armor_rust_blade_hands"
display_name = "锈刃骑士护手"
description = "一对由锈蚀金属与时间丝锻造的古老护手，护手表面覆盖着厚厚的锈层。握紧拳头时，锈层会脱落，露出下面更加古老的金属——那是来自古代的金属，比现在的任何金属都更坚硬。\n\n克罗诺斯在锻造这对手套时，将古代遗迹中找到的金属都熔入了护手。他说："这对手套不只是手套，它们是'古代的力量'——让我可以使用已经失传的技术。"\n\n这对手套的特殊效果是：穿戴者可以用古代金属进行「锈蚀攻击」——徒手攻击造成 1D8 bludgeoning + 1D6 acid（锈蚀之力）。且可以用手套「修复」锈蚀—— bonus action，触碰一件锈蚀的金属装备，恢复它的功能（移除锈蚀惩罚）。每日三次。克罗诺斯说："锈蚀不是只能破坏，它也可以被控制——控制锈蚀就是控制时间。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "rust_blade_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rust_blade_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rust_blade_hands" }
]
```

---

### 69.4 锈刃骑士胫甲

```gdscript
item_id = "armor_rust_blade_feet"
display_name = "锈刃骑士胫甲"
description = "一对由锈蚀金属与时间丝锻造的古老胫甲，胫甲表面覆盖着厚厚的锈层。每一步踏下，锈层都会脱落一些，在地面上留下短暂的锈迹——不是弱点，是标记，也是陷阱（锈迹会让金属滑腻）。\n\n克罗诺斯在锻造这对胫甲时，将「时间之步」能力封入了金属。他说："这对胫甲不只是鞋子，它们是'时间的脚步'——让我每一步都在大地上留下时间的痕迹。"\n\n这对胫甲的特殊效果是：穿戴者可以在锈蚀的表面上正常行走（免疫 difficult terrain from rust）。且可以进行「锈蚀冲锋」——每日一次，以三倍速度直线冲锋，路径上留下锈蚀轨迹（轨迹持续 1 小时，任何踩到的金属装备受到锈蚀，攻击检定 -1 或 AC -1）。克罗诺斯说："时间不是只能流逝，它也可以被留下——留下痕迹，留下影响，留下历史。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "rust_blade_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rust_blade_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_rust_blade_feet" }
]
```

---

## 套装七十：镜中恶魔（Mirror Demon）

> *"镜子不是反射，它是'门'——一扇通往另一个世界的门。我在门的这边，也有恶魔在门的那边。"*

**套装主题**：镜像教派「双面之誓」的装备。这个教派的成员学会了操控镜像，他们的装备由镜面金属和倒影丝制成，赋予了穿戴者创造镜像、操控反射和在镜子之间穿梭的能力。集齐四件时，穿戴者获得「镜像之躯」——可以创造镜像分身、在镜子之间传送、用反射攻击敌人。

**历史渊源**：双面之誓的最高祭司「镜之主」纳西瑟斯（另一位同名者），是一位据说被困在镜中世界千年的传奇法师。他说："我不是在现实中生活，我是在'反射'中生活——只是我的反射比现实更真实。"

---

### 70.1 镜中恶魔面具

```gdscript
item_id = "armor_mirror_demon_head"
display_name = "镜中恶魔面具"
description = "一张由镜面金属与倒影丝锻造的奇异面具，面具的表面是完全光滑的镜面——当你看着它时，你看到的不是穿戴者的脸，是你自己的脸。但这张脸不是普通的反射——它会「动」，会「笑」，会「说话」，即使你没有动、没有笑、没有说话。\n\n纳西瑟斯在锻造这张面具时，将自己的倒影封入了镜面。他说："这张面具不只是面具，它是'我的另一半'——一个在镜中世界生活的我。"\n\n这张面具的特殊效果是：穿戴者可以「交换」——每日一次，与镜中的自己交换位置（相当于 teleport 至 30 尺内任意位置，但只能通过镜面或反射表面）。且可以通过面具「读取」任何镜面中的信息——看到镜面「记住」的影像（自动知道镜面在过去 24 小时内反射过的场景）。纳西瑟斯说："镜子不是只能反射现在，它也可以记住过去——只是大多数人不知道如何读取。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "light_armor", "mirror_demon_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mirror_demon_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_mirror_demon_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；免疫「幻象」和「隐形」（镜面可以看穿）。
**4件套（设计预留）**：每日两次「镜像分身」： bonus action，创造一个镜像分身（如同 mirror image，但只有 1 个分身，持续 1 分钟）；每日一次「镜中穿梭」：通过任何镜面传送至 60 尺内任意位置（如同 dimension door，但必须有镜面）；可以将攻击「反射」——每日一次，当一个攻击命中时，可以用面具反射该攻击（攻击者受到自己的攻击伤害）；在 bright light 中 AC +2（更多反射面）。

---

### 70.2 镜中恶魔长袍

```gdscript
item_id = "armor_mirror_demon_body"
display_name = "镜中恶魔长袍"
description = "一件由镜面金属碎片与倒影丝编织而成的奇异长袍，长袍表面镶嵌着无数小型镜面碎片。这件长袍没有固定的颜色——它会反射周围的环境，让穿戴者看起来如同一团移动的镜子。在强光下，长袍会发出耀眼的光芒；在黑暗中，长袍则几乎不可见。\n\n纳西瑟斯在编织这件长袍时，将所有他收集的镜面碎片都编入了丝线。他说："这件长袍不只是衣服，它是'移动的镜子'——让我穿着反射本身。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的镜面「释放」——每日一次，释放所有镜面碎片形成一个镜盾（AC +3，任何 directed 法术或远程攻击有 50% 概率被反射回攻击者）。且长袍会「反射」光线——在强光下，长袍的 AC +2（光线反射产生干扰）。纳西瑟斯说："镜子不是只能反射，它也可以防御——用反射来偏转攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "light_armor", "mirror_demon_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 5
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mirror_demon_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mirror_demon_body" }
]
```

---

### 70.3 镜中恶魔手套

```gdscript
item_id = "armor_mirror_demon_hands"
display_name = "镜中恶魔手套"
description = "一对由镜面金属与倒影丝锻造的奇异手套，手套的表面是完全光滑的镜面。触碰任何物体时，手套会在物体表面留下一个短暂的「镜像」——不是真正的复制，是光的残留。\n\n纳西瑟斯在锻造这对手套时，将自己的「镜像之触」能力封入了镜面。他说："这对手套不只是手套，它们是'镜像的创造者'——让我可以用触碰创造镜像。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「镜像」一个物体—— bonus action，触碰一个物体，创造一个该物体的镜像（镜像持续 1 分钟，看起来像真的，但没有实体，可以用来迷惑敌人）。每日三次。且可以用手套「反射」攻击——每日一次，当一个 melee 攻击命中时，可以用手套反射该攻击（攻击者受到自己的攻击伤害）。纳西瑟斯说："镜像不是只能迷惑，它也可以防御——用敌人的力量反击敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "light_armor", "mirror_demon_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mirror_demon_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mirror_demon_hands" }
]
```

---

### 70.4 镜中恶魔便鞋

```gdscript
item_id = "armor_mirror_demon_feet"
display_name = "镜中恶魔便鞋"
description = "一对由镜面金属与倒影丝锻造的奇异便鞋，便鞋的表面镶嵌着小型镜面碎片。行走时，便鞋会在地面上留下短暂的「镜像足迹」——不是真正的足迹，是光的反射，让追踪者看到两个方向的足迹。\n\n纳西瑟斯在锻造这对便鞋时，将「镜像之步」能力封入了镜面。他说："这对便鞋不只是鞋子，它们是'镜像的道路'——让我每一步都在大地上留下迷惑。"\n\n这对便鞋的特殊效果是：穿戴者走过的地面会留下「镜像足迹」（持续 1 小时，追踪者无法分辨真实方向）。且可以用「镜面滑行」——每日一次，在任何光滑表面（镜面、冰面、水面）上以三倍速度滑行（移动力 ×3，免疫借机攻击）。纳西瑟斯说："镜像不是只能迷惑敌人，它也可以帮助自己——在镜面中滑行，比在现实中行走更快。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "light_armor", "mirror_demon_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mirror_demon_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_mirror_demon_feet" }
]
```

---

*暗黑 fantasy 主题套装 61–70 完结 · 共 40 件护甲装备（重甲 12 + 中甲 16 + 轻甲 12）*

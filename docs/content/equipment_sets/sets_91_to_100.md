# 传奇装备套装设计文档（套装 91–100：终极 fantasy）

> 10 套终极 fantasy 主题套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×3、中甲×3、轻甲×4。

---

## 套装九十一：创世泰坦（Creation Titan）

> *"世界不是被创造的，它是'被雕刻的'——用时间做凿子，用元素做锤子。"*

**套装主题**：创世泰坦的遗物「原始之力」的装备。这些装备据说是创世泰坦本人穿戴过的，赋予了穿戴者操控元素、创造地形和重塑现实的能力。集齐四件时，穿戴者获得「泰坦之躯」——可以召唤元素风暴、创造地形、免疫所有元素伤害。

**历史渊源**：原始之力的守护者「泰坦之心」盖亚（另一位同名者），是一位据说与创世泰坦灵魂融合的大地女神。她说："我不是在穿戴装备，我是在'成为泰坦'——泰坦的力量通过装备流向我，我的意志通过装备流向世界。"

---

### 91.1 创世泰坦头盔

```gdscript
item_id = "armor_creation_titan_head"
display_name = "创世泰坦头盔"
description = "一顶由原始元素与创世丝锻造的宏伟头盔，头盔表面不断有四种元素在流动——火、水、风、土。头盔的正面有一个「元素之眼」——可以看到所有元素的本质和流动。头盔的顶部有一颗「世界之核」——一颗据说来自创世之初的宝石。\n\n盖亚在锻造这顶头盔时，从创世之地取下了一块原始元素。她说："这块元素不是被取下的，是'被给予的'——创世泰坦选择了我，让我成为它的继承者。"\n\n这顶头盔的特殊效果是：穿戴者可以「看见」所有元素——看到任何地方的元素流动和浓度（如同元素视觉）。且可以通过头盔「感知」到 1 里内所有自然灾害的位置和强度（地震、火山、风暴等）。盖亚说："泰坦不是只能创造，它也可以感知——感知世界的每一次脉动，感知元素的每一次流动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "creation_titan_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_creation_titan_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_creation_titan_head" }
]
```

**2件套（设计预留）**：`resistance_fire` +10、`resistance_cold` +10、`resistance_lightning` +10；`perception_bonus` +3。
**4件套（设计预留）**：每日一次「元素风暴」： bonus action，召唤一场元素风暴（30 尺半径，所有敌人每回合受到 2D8 fire + 2D8 cold + 2D8 lightning，须通过 DC18 敏捷豁免，失败则全伤并 knocked prone）；每日一次「创造地形」： bonus action，重塑 30 尺内的地形（创造山脉、河流、森林或熔岩，困难地形，持续 10 分钟）；免疫所有元素伤害（fire、cold、lightning、acid、thunder）；可以「元素化身」—— bonus action，化作一种元素 1 回合（火：近战附加 2D8 fire；水：免疫物理伤害；风：飞行 40 尺；土：AC +5）。

---

### 91.2 创世泰坦板甲

```gdscript
item_id = "armor_creation_titan_body"
display_name = "创世泰坦板甲"
description = "一副由原始元素与创世丝锻造的宏伟板甲，板甲表面不断有四种元素在流动——火、水、风、土。这副板甲是有「生命」的——它会根据穿戴者的意愿变化形态：需要防御时变成岩石，需要移动时变成风，需要攻击时变成火，需要治愈时变成水。\n\n盖亚在锻造这副板甲时，将创世泰坦的骨骼都熔入了金属。她说："这副板甲不只是铠甲，它是'泰坦的身体'——让我可以分享泰坦的力量。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的元素「释放」——每日一次，释放所有元素形成一个元素领域（30 尺半径，穿戴者可以选择一种元素主导：火——所有敌人每回合受到 2D10 fire；水——所有盟友每回合恢复 2D10 HP；风——所有盟友移动力 +20；土——所有盟友 AC +3，持续 1 分钟）。盖亚说："泰坦不是只能攻击，它也可以创造——创造有利于自己的环境。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "creation_titan_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 45000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 8, source_type = "equipment", source_id = "armor_creation_titan_body" },
    { attribute_id = "max_hp", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_creation_titan_body" }
]
```

---

### 91.3 创世泰坦护手

```gdscript
item_id = "armor_creation_titan_hands"
display_name = "创世泰坦护手"
description = "一对由原始元素与创世丝锻造的宏伟护手，护手表面不断有四种元素在流动。握紧拳头时，元素会汇聚在拳峰；张开手掌时，元素会扩散。护手的掌心有「创世之印」——可以创造和毁灭。\n\n盖亚在锻造这对手套时，将创世泰坦的双手都熔入了金属。她说："这对手套不只是手套，它们是'泰坦的双手'——让我可以用泰坦的方式创造和毁灭。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「创造」或「毁灭」——每日一次，创造一个小型地形（5 尺立方体的岩石、水、火或风，持续 1 分钟）或毁灭一个 5 尺立方体的地形（造成 4D10 对应元素伤害）。且可以用手套「释放」元素冲击——每日三次，释放一道元素冲击（30 尺射程 3D10 随机元素）。盖亚说："泰坦的双手不是只能战斗，它也可以创造——创造生命，创造世界，创造未来。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "creation_titan_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_creation_titan_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_creation_titan_hands" }
]
```

---

### 91.4 创世泰坦胫甲

```gdscript
item_id = "armor_creation_titan_feet"
display_name = "创世泰坦胫甲"
description = "一对由原始元素与创世丝锻造的宏伟胫甲，胫甲表面不断有四种元素在流动。每一步踏下，大地都会微微震动——不是弱点，是力量，是创世之力在地面上留下的痕迹。\n\n盖亚在锻造这对胫甲时，将创世泰坦的双脚都熔入了金属。她说："这对胫甲不只是鞋子，它们是'泰坦的脚步'——让我每一步都如同地震。"\n\n这对胫甲的特殊效果是：穿戴者可以「震地」——每日一次，用力踏地引发一次大地震（30 尺半径，所有生物须通过 DC18 敏捷豁免，失败则 prone 并受到 3D10 bludgeoning + 1D10 随机元素）。且可以在任何地形上正常行走——包括熔岩、沼泽和虚空（泰坦的脚步无所畏惧）。盖亚说："泰坦不需要路，它只需要世界——世界在哪里，泰坦就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "creation_titan_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_creation_titan_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_creation_titan_feet" }
]
```

---

## 套装九十二：虚空吞噬者（Void Devourer）

> *"虚空不是空无一物，它是'饥饿'——一种永远无法满足的饥饿，吞噬一切存在。"*

**套装主题**：虚空崇拜者教团「无尽饥饿」的装备。这些崇拜者拥抱虚空的饥饿，他们的装备由虚空物质和吞噬丝制成，赋予了穿戴者吞噬能量、释放虚空攻击和从虚空中重生的能力。集齐四件时，穿戴者获得「虚空之躯」——可以吞噬敌人的能量、释放虚空风暴、从虚空中重生。

**历史渊源**：无尽饥饿的最高崇拜者「虚空之心」涅扎尔，是一位据说被虚空选中并吞噬了灵魂的疯狂法师。他说："我不是我，我是'虚空'——虚空通过我吞噬，我通过虚空存在。"

---

### 92.1 虚空吞噬者头盔

```gdscript
item_id = "armor_void_devourer_head"
display_name = "虚空吞噬者头盔"
description = "一顶由虚空物质与吞噬丝锻造的恐怖头盔，头盔呈现出纯粹的黑色——不是布料的黑，是「不存在」的黑。头盔的正面没有眼洞——因为虚空不需要眼睛，虚空可以感知一切。头盔的内部不断有低语声——那是虚空的声音，它在说："饥饿……饥饿……"\n\n涅扎尔在锻造这顶头盔时，将自己的灵魂都献给了虚空。他说："这顶头盔不只是头盔，它是'虚空的嘴巴'——让虚空可以通过我吞噬。"\n\n这顶头盔的特殊效果是：穿戴者可以「感知」到 60 尺内所有生命能量的流动（虚空视觉）。且可以通过头盔「吞噬」能量——受到任何 damage 时，25% 概率将伤害转化为 HP（虚空吞噬）。涅扎尔说："虚空不是只能吞噬，它也可以感知——感知一切存在的能量，然后吞噬它们。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "void_devourer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_void_devourer_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_void_devourer_head" }
]
```

**2件套（设计预留）**：`resistance_necrotic` +20；免疫「charmed」和「frightened」（虚空让人超脱情感）。
**4件套（设计预留）**：每日三次「虚空吞噬」： bonus action，吞噬 30 尺内一个目标的能量（目标受到 3D10 necrotic，穿戴者恢复等量 HP）；每日一次「虚空风暴」：15 尺半径内所有生物受到 4D10 necrotic，须通过 DC18 体质豁免，失败则力量/敏捷/体质 -2（暂时，长休恢复）；HP 降至 0 时，有 50% 概率触发「虚空重生」：恢复至 50% 最大 HP 并释放一次虚空风暴；10 尺内所有敌人每回合受到 1D10 necrotic（虚空光环）。

---

### 92.2 虚空吞噬者板甲

```gdscript
item_id = "armor_void_devourer_body"
display_name = "虚空吞噬者板甲"
description = "一副由虚空物质与吞噬丝锻造的恐怖板甲，板甲呈现出纯粹的黑色，不断有微弱的紫色光芒在甲缝中流动。这副板甲是有「饥饿」的——它会不断吞噬周围的能量，包括光线、热量和生命能量。在板甲附近，植物会枯萎，火焰会熄灭，声音会消失。\n\n涅扎尔在锻造这副板甲时，将一块虚空碎片嵌入了板甲的核心。他说："这副板甲不只是铠甲，它是'虚空的胃'——让虚空可以通过我吞噬一切。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的虚空能量「释放」——每日一次，释放所有虚空能量形成一个虚空领域（15 尺半径，所有生物每回合受到 2D10 necrotic 并被 restrained，因为虚空在吞噬他们的能量，持续 1 分钟）。且板甲会「吞噬」能量——在能量充沛的环境中（魔法区域、元素区域），每小时恢复 1 HP（虚空吞噬）。涅扎尔说："虚空不是只能攻击，它也可以治愈——用吞噬来的能量治愈自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "void_devourer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 45000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 8, source_type = "equipment", source_id = "armor_void_devourer_body" },
    { attribute_id = "max_hp", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_void_devourer_body" }
]
```

---

### 92.3 虚空吞噬者护手

```gdscript
item_id = "armor_void_devourer_hands"
display_name = "虚空吞噬者护手"
description = "一对由虚空物质与吞噬丝锻造的恐怖护手，护手呈现出纯粹的黑色，手掌内部不断有虚空漩涡在旋转。握紧拳头时，漩涡会汇聚在拳峰；张开手掌时，漩涡会扩散。护手的掌心有「吞噬之印」——可以吞噬任何触碰的能量。\n\n涅扎尔在锻造这对手套时，将自己的双手改造成了「虚空之口」。他说："这对手套不只是手套，它们是'虚空的手'——让虚空可以通过我吞噬。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「吞噬」—— bonus action，触碰一个目标，吞噬它的能量（目标受到 2D10 necrotic，穿戴者恢复等量 HP）。每日三次。且可以用手套「释放」虚空冲击——每日一次，释放一道虚空冲击（30 尺射程 4D10 necrotic，可以穿透任何实体障碍物）。涅扎尔说："虚空的手不是只能吞噬，它也可以释放——释放虚空的饥饿，让一切归于虚无。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "void_devourer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_devourer_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_devourer_hands" }
]
```

---

### 92.4 虚空吞噬者胫甲

```gdscript
item_id = "armor_void_devourer_feet"
display_name = "虚空吞噬者胫甲"
description = "一对由虚空物质与吞噬丝锻造的恐怖胫甲，胫甲呈现出纯粹的黑色，每一步踏下都会在地面留下短暂的虚空裂痕——那是现实被吞噬的痕迹。\n\n涅扎尔在锻造这对胫甲时，将「虚空之步」能力封入了吞噬丝。他说："这对胫甲不只是鞋子，它们是'虚空的道路'——让我每一步都在吞噬现实。"\n\n这对胫甲的特殊效果是：穿戴者可以「虚空步」——每日一次，踏入虚空并瞬间移动至 60 尺内任何位置（如同 dimension door，但不会留下法术痕迹）。且可以进行「虚空冲锋」——每日一次，以三倍速度直线冲锋，路径上留下虚空轨迹（轨迹持续 1 回合，任何踩到的生物受到 2D10 necrotic）。涅扎尔说："虚空行者不需要路，他只需要虚空——虚空无处不在，所以虚空行者可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "void_devourer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_void_devourer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_void_devourer_feet" }
]
```

---

## 套装九十三：圣光裁决者（Holy Arbiter）

> *"正义不是温柔的，它是'锋利的'——一种可以切割邪恶、净化罪恶的锋利。"*

**套装主题**：神圣裁决教团「永恒正义」的装备。这些裁决者执行神圣的正义，他们的装备由圣光金和裁决丝制成，赋予了穿戴者释放圣光攻击、审判邪恶和治愈无辜的能力。集齐四件时，穿戴者获得「裁决之躯」——可以释放圣光审判、免疫邪恶、治愈盟友。

**历史渊源**：永恒正义的最高裁决者「正义之心」米迦勒（另一位同名者），是一位据说被神明亲自选中的天使化身。他说："我不是在执行正义，我是在'成为正义'——正义通过我流动，我通过正义存在。"

---

### 93.1 圣光裁决者头盔

```gdscript
item_id = "armor_holy_arbiter_head"
display_name = "圣光裁决者头盔"
description = "一顶由圣光金与裁决丝锻造的辉煌头盔，头盔表面不断有神圣的光芒在流动。头盔的正面有一个「正义之眼」——可以看穿一切邪恶和谎言。头盔的顶部有一颗「神圣之心」——一颗能够储存和释放神圣能量的魔法宝石。\n\n米迦勒在锻造这顶头盔时，将自己的「正义之眼」封入了宝石。他说："这顶头盔不只是头盔，它是'正义的延伸'——让正义可以通过我看清一切。"\n\n这顶头盔的特殊效果是：穿戴者可以「看见」邪恶——看到任何生物的「善恶属性」（善良者发光，邪恶者发黑，中立者正常）。且可以通过头盔「感知」到 60 尺内所有 undead、fiend 和邪恶生物的位置（正义雷达）。米迦勒说："正义不是盲目的，它看得最清楚——清楚到可以看见灵魂的每一个污点。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "holy_arbiter_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_holy_arbiter_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_holy_arbiter_head" }
]
```

**2件套（设计预留）**：`resistance_radiant` +20；免疫「charmed」和「frightened」（正义让人坚定）。
**4件套（设计预留）**：每日三次「圣光审判」： bonus action，审判 30 尺内一个邪恶目标（目标受到 3D10 radiant，须通过 DC18 智慧豁免，失败则 frightened 1 回合）；每日一次「神圣治愈」：30 尺内所有善良盟友恢复 4D10 HP，所有邪恶敌人受到 4D10 radiant；免疫 undead 和 fiend 的「生命吸取」和「诅咒」；10 尺内所有邪恶生物每回合受到 1D10 radiant（正义光环）。

---

### 93.2 圣光裁决者板甲

```gdscript
item_id = "armor_holy_arbiter_body"
display_name = "圣光裁决者板甲"
description = "一副由圣光金与裁决丝锻造的辉煌板甲，板甲表面不断有神圣的光芒在流动。这副板甲是有「正义」的——它会自动保护善良者，惩罚邪恶者。当善良者触碰板甲时，会感到温暖和安全；当邪恶者触碰板甲时，会感到灼痛和恐惧。\n\n米迦勒在锻造这副板甲时，将自己的「正义之心」封入了金属。他说："这副板甲不只是铠甲，它是'正义的盾牌'——让正义可以通过我保护无辜。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的圣光「释放」——每日一次，释放所有圣光形成一个神圣领域（30 尺半径，所有善良盟友 AC +2 并每回合恢复 1D10 HP，所有邪恶敌人每回合受到 2D10 radiant 并攻击检定 -2，持续 1 分钟）。且板甲会「审判」攻击者——任何 evil 生物 melee 攻击穿戴者时受到 1D10 radiant（正义反伤）。米迦勒说："正义不是只能攻击，它也可以保护——用圣光来保护无辜，用审判来惩罚邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "holy_arbiter_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 45000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 8, source_type = "equipment", source_id = "armor_holy_arbiter_body" },
    { attribute_id = "max_hp", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_holy_arbiter_body" }
]
```

---

### 93.3 圣光裁决者护手

```gdscript
item_id = "armor_holy_arbiter_hands"
display_name = "圣光裁决者护手"
description = "一对由圣光金与裁决丝锻造的辉煌护手，护手表面不断有神圣的光芒在流动。握紧拳头时，光芒会汇聚在拳峰，形成两颗小型的「圣光球」。松开拳头时，光芒会消散，回到护手表面。护手的掌心有「审判之印」——可以审判任何触碰的邪恶。\n\n米迦勒在锻造这对手套时，将自己的「正义之手」封入了金属。他说："这对手套不只是手套，它们是'正义的剑'——让正义可以通过我惩罚邪恶。"\n\n这对手套的特殊效果是：穿戴者可以用圣光拳进行「正义打击」——徒手攻击造成 1D8 bludgeoning + 1D10 radiant（对 evil 生物伤害翻倍）。且可以用手套「治愈」——每日三次，触碰一个善良盟友，恢复 2D10 HP（圣光治愈）。米迦勒说："正义的手不是只能惩罚，它也可以治愈——治愈无辜，保护善良。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "holy_arbiter_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_holy_arbiter_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_holy_arbiter_hands" }
]
```

---

### 93.4 圣光裁决者胫甲

```gdscript
item_id = "armor_holy_arbiter_feet"
display_name = "圣光裁决者胫甲"
description = "一对由圣光金与裁决丝锻造的辉煌胫甲，胫甲表面不断有神圣的光芒在流动。每一步踏下，光芒会在地面上留下短暂的圣光足迹——不是弱点，是标记，也是祝福（任何善良者踩到足迹恢复 1D6 HP）。\n\n米迦勒在锻造这对胫甲时，将「正义之步」能力封入了金属。他说："这对胫甲不只是鞋子，它们是'正义的道路'——让我每一步都留下正义的痕迹。"\n\n这对胫甲的特殊效果是：穿戴者可以「圣光冲锋」——每日一次，以三倍速度直线冲锋，路径上留下圣光轨迹（轨迹持续 1 分钟，任何 evil 生物踩到受到 2D10 radiant，任何 good 生物踩到恢复 2D10 HP）。且免疫 difficult terrain（正义的道路永远平坦）。米迦勒说："正义行者不需要路，他只需要正义——正义在哪里，他就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "holy_arbiter_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_holy_arbiter_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_holy_arbiter_feet" }
]
```

---

## 套装九十四：死亡使者（Death Herald）

> *"死亡不是终点，它是'新的起点'。我的工作不是带来死亡，是确保死亡按时到来。"*

**套装主题**：死亡守护者教团「永恒之镰」的装备。这些守护者为死神服务，确保生死轮回的正常运转，他们的装备由死亡骨和死神丝制成，赋予了穿戴者收割灵魂、操控亡灵和从死亡中恢复的能力。集齐四件时，穿戴者获得「死亡之躯」——可以收割灵魂、召唤亡灵军团、从死亡中重生。

**历史渊源**：永恒之镰的最高守护者「死亡之心」塔纳托斯（另一位同名者），是一位据说与死神本人签订了契约的传奇战士。他说："我不是在带来死亡，我是在'维护秩序'——生死轮回是世界的基础，我的工作就是确保它正常运转。"

---

### 94.1 死亡使者头冠

```gdscript
item_id = "armor_death_herald_head"
display_name = "死亡使者头冠"
description = "一顶由死亡骨与死神丝编织而成的恐怖头冠，头冠上镶嵌着一颗「死亡之眼」——一颗能够看到灵魂和生命线的魔法宝石。头冠的表面不断有幽灵般的面孔在浮现和消失——那是被收割灵魂的最后表情。头冠佩戴时，周围的温度会下降，仿佛死亡正在接近。\n\n塔纳托斯在编织这头冠时，从死神本人手中接过了这颗宝石。他说："这颗宝石不是被给予的，是'被委托的'——死神信任我，让我成为它的代理人。"\n\n这头冠的特殊效果是：穿戴者可以「看见」灵魂——看到任何生物的「生命之火」（显示为不同颜色的火焰，健康者是明亮的，濒死者是暗淡的，已死者是熄灭的）。且可以通过头冠「感知」到 60 尺内所有濒死生物的位置（死亡雷达）。塔纳托斯说："死亡不是秘密，它只是需要正确的眼睛来看——而死神的眼睛看得最清楚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "death_herald_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_death_herald_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_death_herald_head" }
]
```

**2件套（设计预留）**：`resistance_necrotic` +20；免疫「frightened」和「charmed」（死亡让人超脱情感）。
**4件套（设计预留）**：每日三次「灵魂收割」： bonus action，收割 30 尺内一个濒死目标的灵魂（目标 HP 0 时自动死亡，无法复活，穿戴者恢复 2D10 HP）；每日一次「召唤亡灵军团」：召唤 1D6 只骷髅战士 + 1D4 只幽灵（持续 10 分钟）；HP 降至 0 时，有 50% 概率触发「死亡拒绝」：恢复至 25% 最大 HP 并释放一次 necrotic 爆发（15 尺半径 3D10 necrotic）；10 尺内所有生物每回合受到 1D10 necrotic（死亡光环）。

---

### 94.2 死亡使者鳞甲

```gdscript
item_id = "armor_death_herald_body"
display_name = "死亡使者鳞甲"
description = "一副由死亡骨与死神丝编织而成的恐怖鳞甲，鳞甲表面不断有幽灵般的面孔在浮现和消失。这副鳞甲是有「记忆」的——每一块鳞片都记录了一个被收割的灵魂。鳞甲的重量会随着收割的灵魂数量增加——每个灵魂增加约 1 磅的重量。\n\n塔纳托斯在编织这副鳞甲时，将所有他收割的灵魂都编入了鳞甲。他说："这副鳞甲不只是铠甲，它是'灵魂的监狱'——也是'灵魂的图书馆'，每个灵魂都有一个故事。"\n\n这副鳞甲的特殊效果是：穿戴者可以将鳞甲的灵魂「释放」——每日一次，释放所有储存的灵魂形成一个灵魂风暴（15 尺半径，所有生物受到 1D6 necrotic/灵魂，最多 5D6，须通过 DC18 智慧豁免，失败则 frightened 1 回合）。且鳞甲会「吸收」necrotic damage——穿戴者受到 necrotic 伤害时，50% 概率将伤害转化为 HP（死亡吞噬）。塔纳托斯说："死亡不是只能收割，它也可以保护——用灵魂来保护，用死亡来治愈。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "death_herald_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 38000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_death_herald_body" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_death_herald_body" }
]
```

---

### 94.3 死亡使者手套

```gdscript
item_id = "armor_death_herald_hands"
display_name = "死亡使者手套"
description = "一对由死亡骨与死神丝编织而成的恐怖手套，手套表面不断有冰冷的气息在流动。手套的指尖有「收割之爪」——可以收割任何触碰的灵魂。手套的掌心有「死神之印」——可以判断一个生物的剩余寿命。\n\n塔纳托斯在编织这对手套时，将自己的「收割之手」能力封入了死神丝。他说："这对手套不只是手套，它们是'死神的手'——让死神可以通过我收割。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「收割」一个濒死目标的灵魂—— bonus action，触碰一个 HP 0 的生物，立即杀死它（无法复活），穿戴者恢复 3D10 HP。每日两次。且可以用手套「判断」—— bonus action，触碰一个生物，自动知道它的剩余 HP 和预计寿命（如果自然死亡）。塔纳托斯说："死神的手不是只能收割，它也可以判断——判断谁该死，谁该活。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "death_herald_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_herald_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_herald_hands" }
]
```

---

### 94.4 死亡使者软靴

```gdscript
item_id = "armor_death_herald_feet"
display_name = "死亡使者软靴"
description = "一对由死亡骨与死神丝编织而成的恐怖软靴，软靴的表面不断有冰冷的气息在流动。行走时，软靴不会发出任何声音——因为死亡不需要声音，死亡只是「经过」。每一步踏下，都会在地面留下短暂的黑色足迹——不是弱点，是标记，也是警告。\n\n塔纳托斯在编织这对软靴时，将「死亡之步」能力封入了死神丝。他说："这对软靴不只是鞋子，它们是'死亡的道路'——让我每一步都在走向死亡。"\n\n这对软靴的特殊效果是：穿戴者移动时完全无声（如同幽灵）。且可以「死亡步」——每日一次，化作一团黑雾移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。且可以感知 60 尺内任何即将死亡或濒死的生物（死亡预感）。塔纳托斯说："死亡行者不需要路，他只需要目的地——目的地在哪里，死亡就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "death_herald_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_herald_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_death_herald_feet" }
]
```

---

## 套装九十五：星辰编织者（Star Weaver）

> *"星辰不是遥远的光点，它们是'丝线'——用来编织宇宙的巨大丝线。"*

**套装主题**：星辰编织者结社「宇宙之织」的装备。这些编织者学会了用星辰能量编织现实，他们的装备由星辰丝和宇宙结晶制成，赋予了穿戴者操控星辰能量、编织星图和召唤星辰攻击的能力。集齐四件时，穿戴者获得「星辰之躯」——可以用星辰能量攻击、编织星图预测未来、召唤星辰坠落。

**历史渊源**：宇宙之织的最高编织者「星辰之心」织女（另一位同名者），是一位据说可以用星辰丝线编织银河的传奇织女。她说："我不是在观测星辰，我是在'编织'它们——用星辰的丝线，编织属于自己的宇宙。"

---

### 95.1 星辰编织者头冠

```gdscript
item_id = "armor_star_weaver_head"
display_name = "星辰编织者头冠"
description = "一顶由星辰丝与宇宙结晶编织而成的华丽头冠，头冠上镶嵌着一颗「星辰之心」——一颗能够储存和释放星辰能量的魔法宝石。头冠的表面不断有星辰图案在流动，仿佛穿戴者头顶着一片旋转的银河。\n\n织女在编织这头冠时，从银河中心取下了一颗微型星辰。她说："这颗星辰不是被取下的，是'被借用的'——银河借给我一颗星辰，让我可以编织更多的美丽。"\n\n这头冠的特殊效果是：穿戴者可以「看见」星辰能量——看到任何地方的星辰能量流动和浓度（如同星辰视觉）。且可以通过头冠「感知」到 1 里内所有星辰的位置和状态（星辰雷达）。织女说："星辰编织者不是只能编织，她也可以感知——感知宇宙的每一次脉动，感知星辰的每一次闪烁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_star_weaver_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；夜间所有检定 +2（星辰加持）。
**4件套（设计预留）**：每日三次「星辰射线」： bonus action，释放一道星辰射线（60 尺射程 3D10 radiant + 1D10 force，可以穿透任何实体障碍物）；每日一次「星辰坠落」：召唤一颗小星辰坠落（15 尺半径 6D10 radiant + 2D10 force，须通过 DC18 敏捷豁免，失败则全伤并 prone）；可以「编织星图」—— bonus action，预测接下来 1 分钟内的一个事件（DM 描述，准确率 80%）；夜间时 `spell_dc_bonus` +2。

---

### 95.2 星辰编织者长袍

```gdscript
item_id = "armor_star_weaver_body"
display_name = "星辰编织者长袍"
description = "一件由星辰丝与宇宙结晶编织而成的华丽长袍，长袍表面不断有星辰图案在流动，仿佛穿戴者身上披着一片银河。这件长袍没有固定的颜色——它会根据当前编织的星图变化：织女星时是银白色，牛郎星时是金黄色，北斗星时是深蓝色。\n\n织女在编织这件长袍时，将整个银河的图案都编入了丝线。她说："这件长袍不只是衣服，它是'银河的碎片'——让我穿着整个宇宙。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的星辰能量「释放」——每日一次，释放所有星辰能量形成一个星辰领域（15 尺半径，所有盟友每回合恢复 1D10 HP，所有敌人每回合受到 1D10 radiant，持续 1 分钟）。且长袍会「吸收」星光——在夜晚的户外，每小时恢复 1 HP（星光治愈）。织女说："星辰不是只能用来攻击，它也可以用来治愈——用星光来温暖，用银河来庇护。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 38000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_star_weaver_body" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_star_weaver_body" }
]
```

---

### 95.3 星辰编织者手套

```gdscript
item_id = "armor_star_weaver_hands"
display_name = "星辰编织者手套"
description = "一对由星辰丝与宇宙结晶编织而成的华丽手套，手套表面不断有星辰图案在流动。手套的指尖有「星辰聚焦器」——可以将星辰能量聚焦成锋利的丝线。手套的掌心有「编织之印」——可以编织现实。\n\n织女在编织这对手套时，将自己的「编织之手」能力封入了星辰丝。她说："这对手套不只是手套，它们是'宇宙的织针'——让我可以用手编织星辰。"\n\n这对手套的特殊效果是：穿戴者可以用手指「编织」星辰丝线——每日三次，编织出一道星辰丝线（30 尺射程 2D10 radiant + 1D10 force，可以切割任何实体障碍物）。且可以用手套「编织」现实—— bonus action，编织一个小型现实（5 尺立方体的光、声音或幻象，持续 1 分钟）。织女说："星辰编织者的手不是只能编织，它也可以切割——用星辰丝线切割任何障碍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_hands" }
]
```

---

### 95.4 星辰编织者便鞋

```gdscript
item_id = "armor_star_weaver_feet"
display_name = "星辰编织者便鞋"
description = "一对由星辰丝与宇宙结晶编织而成的华丽便鞋，便鞋没有与地面接触——它们悬浮在地面之上约半寸，因为星辰能量让穿戴者几乎失重。行走时，便鞋会在地面上留下短暂的星光足迹——不是弱点，是美丽，也是标记。\n\n织女在编织这对便鞋时，将自己的「星辰之步」能力封入了星辰丝。她说："这对便鞋不只是鞋子，它们是'星辰的道路'——让我每一步都在星辰上行走。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（星辰隔绝声音）。且可以用「星辰滑行」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。且在星光下，移动力 +20（星辰加速）。织女说："星辰编织者不需要路，她只需要星辰——星辰无处不在，所以星辰编织者可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "star_weaver_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_star_weaver_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_star_weaver_feet" }
]
```

---

## 套装九十六：命运纺织者（Fate Weaver）

> *"命运不是注定的，它是'被编织的'——用选择做丝线，用行动做织针。"*

**套装主题**：命运纺织者结社「命运之织」的装备。这些纺织者学会了编织命运，他们的装备由命运丝和时间结晶制成，赋予了穿戴者操控命运、改变概率和预知未来的能力。集齐四件时，穿戴者获得「命运之躯」——可以编织命运、改变概率、预知未来。

**历史渊源**：命运之织的最高纺织者「命运之心」诺恩（另一位同名者），是一位据说可以编织任何人命运的传奇纺织者。她说："我不是在编织命运，我是在'展示可能性'——命运不是一条线，它是无数条线的交织。"

---

### 96.1 命运纺织者头冠

```gdscript
item_id = "armor_fate_weaver_head"
display_name = "命运纺织者头冠"
description = "一顶由命运丝与时间结晶编织而成的神秘头冠，头冠上镶嵌着一颗「命运之眼」——一颗能够看见所有可能未来的魔法宝石。头冠的表面不断有细线在流动——那是命运的丝线，每一根都代表一个可能的未来。\n\n诺恩在编织这头冠时，将自己的「命运之眼」封入了宝石。她说："这头冠不只是头冠，它是'命运的面纱'——让我可以掀开面纱，看见所有可能的未来。"\n\n这头冠的特殊效果是：穿戴者可以「看见」命运的丝线——看到任何生物的「命运走向」（显示为不同颜色的丝线，明亮的代表好的未来，暗淡的代表坏的未来，断裂的代表死亡）。且可以通过头冠「感知」到 30 尺内所有重大事件的发生概率（命运雷达）。诺恩说："命运不是秘密，它只是需要正确的眼睛来看——而命运纺织者的眼睛看得最清楚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "fate_weaver_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_fate_weaver_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_fate_weaver_head" }
]
```

**2件套（设计预留）**：`insight_bonus` +3；`history_bonus` +3（预知过去）。
**4件套（设计预留）**：每日两次「命运编织」： bonus action，改变一个 d20 检定的结果（可以重掷一次，选择更好的结果）；每日一次「命运预知」：预知接下来 1 分钟内的一个攻击（可以选择让该攻击自动命中或自动 miss）；每日一次「命运束缚」： bonus action，束缚一个目标的命运（目标须通过 DC18 智慧豁免，失败则下一回合不能移动或攻击，因为命运被束缚）；被攻击时 25% 概率触发「命运闪避」：攻击 miss（命运让攻击偏离）。

---

### 96.2 命运纺织者长袍

```gdscript
item_id = "armor_fate_weaver_body"
display_name = "命运纺织者长袍"
description = "一件由命运丝与时间结晶编织而成的神秘长袍，长袍表面不断有细线在流动——那是命运的丝线。这件长袍没有固定的颜色——它会根据当前编织的命运变化：编织好运时是金色，编织厄运时是黑色，编织爱情时是红色，编织死亡时是灰色。\n\n诺恩在编织这件长袍时，将所有她编织过的命运都编入了丝线。她说："这件长袍不只是衣服，它是'命运的记录'——记录着我编织过的所有命运。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的命运丝线「释放」——每日一次，释放所有命运丝线形成一个命运领域（15 尺半径，所有盟友的攻击检定 +2、豁免检定 +2，所有敌人的攻击检定 -2、豁免检定 -2，持续 1 分钟）。且长袍会「保护」穿戴者——被攻击时 25% 概率 miss（命运让攻击偏离）。诺恩说："命运不是只能用来预知，它也可以用来保护——用命运来保护盟友，用命运来束缚敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "fate_weaver_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 38000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_fate_weaver_body" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_fate_weaver_body" }
]
```

---

### 96.3 命运纺织者手套

```gdscript
item_id = "armor_fate_weaver_hands"
display_name = "命运纺织者手套"
description = "一对由命运丝与时间结晶编织而成的神秘手套，手套表面不断有细线在流动。手套的指尖有「命运针」——可以编织和剪断命运的丝线。手套的掌心有「命运印」——可以改变一个生物的命运走向。\n\n诺恩在编织这对手套时，将自己的「命运之手」能力封入了命运丝。她说："这对手套不只是手套，它们是'命运的织针'——让我可以用手编织命运。"\n\n这对手套的特殊效果是：穿戴者可以用手指「编织」命运——每日三次，改变一个 d20 检定的结果（可以重掷一次）。且可以用手套「剪断」命运—— bonus action，触碰一个目标，剪断它的一条命运丝线（目标失去一个即将获得的 buff 或一个即将触发的优势，持续 1 回合）。诺恩说："命运纺织者的手不是只能编织，它也可以剪断——剪断厄运，编织好运。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "fate_weaver_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_fate_weaver_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_fate_weaver_hands" }
]
```

---

### 96.4 命运纺织者便鞋

```gdscript
item_id = "armor_fate_weaver_feet"
display_name = "命运纺织者便鞋"
description = "一对由命运丝与时间结晶编织而成的神秘便鞋，便鞋没有与地面接触——它们行走在命运的丝线上，而不是地面上。行走时，便鞋会在命运的丝线上留下短暂的痕迹——不是弱点，是标记，也是陷阱。\n\n诺恩在编织这对便鞋时，将自己的「命运之步」能力封入了命运丝。她说："这对便鞋不只是鞋子，它们是'命运的脚步'——让我每一步都走在命运上。"\n\n这对便鞋的特殊效果是：穿戴者可以「命运步」——每日一次，沿着命运的丝线移动（传送至 60 尺内任意位置，如同 dimension door，但不会留下法术痕迹）。且可以用「命运滑行」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。诺恩说："命运纺织者不需要路，她只需要命运——命运在哪里，她就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "fate_weaver_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_fate_weaver_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_fate_weaver_feet" }
]
```

---

## 套装九十七：梦境之主（Dream Lord）

> *"梦境不是虚幻的，它是'另一种真实'——一种比现实更自由、更丰富的真实。"*

**套装主题**：梦境之主教团「永恒之梦」的装备。这些梦境之主学会了操控梦境，他们的装备由梦境丝和睡魔结晶制成，赋予了穿戴者进入梦境、操控梦境和在梦中战斗的能力。集齐四件时，穿戴者获得「梦境之躯」——可以进入任何人的梦境、在梦中造成真实伤害、将梦中的物品带回现实。

**历史渊源**：永恒之梦的最高主宰「梦境之心」墨菲斯（另一位同名者），是一位据说可以在梦中创造整个世界的传奇梦境之主。他说："我不是在做梦，我是在'创造'——梦境是我的画布，想象是我的颜料。"

---

### 97.1 梦境之主头冠

```gdscript
item_id = "armor_dream_lord_head"
display_name = "梦境之主头冠"
description = "一顶由梦境丝与睡魔结晶编织而成的奇异头冠，头冠上镶嵌着一颗「梦境之眼」——一颗能够看见所有梦境的魔法宝石。头冠的表面不断有梦境般的图案在流动——有时是风景，有时是面孔，有时是无法描述的形状。\n\n墨菲斯在编织这头冠时，将自己的「梦境之眼」封入了宝石。他说："这头冠不只是头冠，它是'梦境的入口'——让我可以随时进入任何梦境。"\n\n这头冠的特殊效果是：穿戴者可以「看见」梦境——看到 30 尺内所有睡眠中生物的梦境内容（如同梦境电视）。且可以通过头冠「进入」梦境——每日一次，进入 30 尺内一个睡眠中生物的梦境（持续 1 小时或直到目标醒来，在梦中可以交流、战斗或修改记忆）。墨菲斯说："梦境不是只能用来逃避，它也可以用来探索——探索他人的内心，探索潜意识的深处。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "dream_lord_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_lord_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_dream_lord_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；免疫「梦境操控」和「睡眠诅咒」。
**4件套（设计预留）**：每日一次「梦境入侵」：进入 30 尺内一个睡眠目标的梦境，在梦中造成 4D10 psychic（目标醒来时转化为真实伤害）；每日一次「梦境造物」：在梦中找到一件物品，醒来后该物品出现在现实中（由 DM 决定物品）；被攻击时 25% 概率触发「梦境闪避」：攻击穿过梦境中的身体（miss）；可以「清醒梦」——在睡眠中保持清醒意识，控制梦境内容。

---

### 97.2 梦境之主长袍

```gdscript
item_id = "armor_dream_lord_body"
display_name = "梦境之主长袍"
description = "一件由梦境丝与睡魔结晶编织而成的奇异长袍，长袍表面不断有梦境般的图案在流动。这件长袍没有固定的形态——它会根据穿戴者的梦境而变化：梦见海洋时变成蓝色，梦见火焰时变成红色，梦见森林时变成绿色。\n\n墨菲斯在编织这件长袍时，将自己所有的梦境都编入了丝线。他说："这件长袍不只是衣服，它是'梦境的日记'——记录着我所有的梦。"\n\n这件长袍的特殊效果是：穿戴者可以在现实中「召唤」梦境中的事物——每日一次，可以从长袍中召唤出一个梦境中的生物或物品（持续 1 分钟，生物不能造成伤害，物品可以使用）。且长袍会自动「保护」穿戴者在睡眠中——任何试图在睡眠中攻击或操控穿戴者的生物都会进入一个「噩梦陷阱」（须通过 DC18 智慧豁免，失败则 frightened 1 分钟）。墨菲斯说："梦境不是只能用来逃避，它也可以用来防御——用噩梦来保护睡眠中的自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "dream_lord_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 38000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dream_lord_body" },
    { attribute_id = "max_mana", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_dream_lord_body" }
]
```

---

### 97.3 梦境之主手套

```gdscript
item_id = "armor_dream_lord_hands"
display_name = "梦境之主手套"
description = "一对由梦境丝与睡魔结晶编织而成的奇异手套，手套表面不断有梦境般的图案在流动。手套的指尖有「梦境针」——可以编织和修改梦境。手套的掌心有「梦境印」——可以将现实中的物体拉入梦境。\n\n墨菲斯在编织这对手套时，将自己的「梦境之手」能力封入了梦境丝。他说："这对手套不只是手套，它们是'梦境的织针'——让我可以用手编织任何梦境。"\n\n这对手套的特殊效果是：穿戴者可以用手指「编织」梦境——每日三次，编织一个小型梦境（5 尺立方体的幻象，持续 1 分钟，可以迷惑敌人）。且可以用手套「催眠」—— bonus action，触碰一个目标，目标须通过 DC18 智慧豁免，失败则 asleep 1 分钟（或被攻击时醒来）。墨菲斯说："梦境之主的手不是只能编织，它也可以催眠——让敌人永远沉睡在美丽的梦境中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "dream_lord_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_lord_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dream_lord_hands" }
]
```

---

### 97.4 梦境之主便鞋

```gdscript
item_id = "armor_dream_lord_feet"
display_name = "梦境之主便鞋"
description = "一对由梦境丝与睡魔结晶编织而成的奇异便鞋，便鞋没有与地面接触——它们行走在梦境的边缘，而不是现实的地面上。行走时，便鞋会在梦境和现实之间留下短暂的痕迹——不是弱点，是通道，让穿戴者可以在两者之间穿梭。\n\n墨菲斯在编织这对便鞋时，将自己的「梦境之步」能力封入了梦境丝。他说："这对便鞋不只是鞋子，它们是'梦境的桥梁'——让我可以在现实和梦境之间自由穿梭。"\n\n这对便鞋的特殊效果是：穿戴者可以「梦境步」——每日一次，从现实踏入梦境（免疫所有物理伤害 1 回合，但无法物理攻击，持续 1 回合）。且可以用「梦境滑行」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。墨菲斯说："梦境之主不需要路，他只需要梦境——梦境在哪里，他就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "dream_lord_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dream_lord_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_dream_lord_feet" }
]
```

---

## 套装九十八：时间旅者（Time Traveler）

> *"时间不是河流，它是'圆环'——过去、现在、未来，都是同一个圆环上的点。"*

**套装主题**：时间旅者结社「永恒之环」的装备。这些时间旅者学会了操控时间，他们的装备由时间结晶和轮回丝制成，赋予了穿戴者回溯时间、加速时间和冻结时间的能力。集齐四件时，穿戴者获得「时间之躯」——可以回溯时间、冻结时间、预见未来。

**历史渊源**：永恒之环的最高旅者「时间之心」克罗诺斯（另一位同名者），是一位据说可以穿越任何时间点的传奇时间旅者。他说："我不是在穿越时间，我是在'跳舞'——在时间之环上跳舞，每一步都是一个时代。"

---

### 98.1 时间旅者头冠

```gdscript
item_id = "armor_time_traveler_head"
display_name = "时间旅者头冠"
description = "一顶由时间结晶与轮回丝编织而成的奇异头冠，头冠上镶嵌着一颗「时间之眼」——一颗能够看见所有时间点的魔法宝石。头冠的表面不断有钟表指针在旋转——不是装饰，是真正的时间，显示着过去、现在和未来的时间。\n\n克罗诺斯在编织这头冠时，将自己的「时间之眼」封入了宝石。他说："这头冠不只是头冠，它是'时间的窗户'——让我可以看见所有的时间。"\n\n这头冠的特殊效果是：穿戴者可以「看见」时间——看到任何地方的「时间线」（显示为不同颜色的线，明亮的代表现在，暗淡的代表过去，闪烁的代表未来）。且可以通过头冠「感知」到 30 尺内所有时间异常（时间扭曲、时间裂缝等）。克罗诺斯说："时间不是秘密，它只是需要正确的眼睛来看——而时间旅者的眼睛看得最清楚。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "time_traveler_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_time_traveler_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_time_traveler_head" }
]
```

**2件套（设计预留）**：`insight_bonus` +3；先攻检定 +5。
**4件套（设计预留）**：每日一次「时间回溯」： reaction，当受到伤害时，回溯至上一回合的开始（恢复所有在该回合受到的伤害和消耗的资源）；每日一次「时间冻结」： bonus action，冻结时间 1 回合（只有自己可以行动，所有其他生物 frozen）；每日一次「时间预见」：预知接下来 1 分钟内的一个攻击（可以选择让该攻击自动命中或自动 miss）；被攻击时 25% 概率触发「时间闪避」：攻击 miss（时间让攻击偏离）。

---

### 98.2 时间旅者长袍

```gdscript
item_id = "armor_time_traveler_body"
display_name = "时间旅者长袍"
description = "一件由时间结晶与轮回丝编织而成的奇异长袍，长袍表面不断有钟表指针在旋转。这件长袍没有固定的颜色——它会根据当前的时间变化：早晨是金色，中午是白色，傍晚是红色，夜晚是黑色。在特殊的时间点（午夜、正午、黎明、黄昏），长袍会发出微弱的光芒。\n\n克罗诺斯在编织这件长袍时，将所有的时间都编入了丝线。他说："这件长袍不只是衣服，它是'时间的容器'——让我穿着所有的时间。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的时间「释放」——每日一次，释放所有时间形成一个时间领域（15 尺半径，所有盟友的攻击速度翻倍——每回合可以进行两次 action，所有敌人的攻击速度减半——每回合只能进行一次 action，持续 1 分钟）。且长袍会「保护」穿戴者——被攻击时 25% 概率 miss（时间让攻击偏离）。克罗诺斯说："时间不是只能用来预知，它也可以用来操控——加速盟友，减速敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "time_traveler_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 45000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_time_traveler_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_time_traveler_body" }
]
```

---

### 98.3 时间旅者手套

```gdscript
item_id = "armor_time_traveler_hands"
display_name = "时间旅者手套"
description = "一对由时间结晶与轮回丝编织而成的奇异手套，手套表面不断有钟表指针在旋转。手套的指尖有「时间针」——可以编织和剪断时间的丝线。手套的掌心有「时间印」——可以改变一个物体的时间状态。\n\n克罗诺斯在编织这对手套时，将自己的「时间之手」能力封入了轮回丝。他说："这对手套不只是手套，它们是'时间的织针'——让我可以用手编织时间。"\n\n这对手套的特殊效果是：穿戴者可以用手指「加速」或「减速」——每日三次，改变一个物体的时间流速（加速：攻击速度翻倍，持续 1 回合；减速：攻击速度减半，持续 1 回合）。且可以用手套「老化」或「恢复」—— bonus action，触碰一个物体，让它老化（金属变锈、木头腐烂）或恢复（恢复至原始状态）。克罗诺斯说："时间旅者的手不是只能编织，它也可以改变——改变时间的流速，改变物体的年龄。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "time_traveler_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_time_traveler_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_time_traveler_hands" }
]
```

---

### 98.4 时间旅者便鞋

```gdscript
item_id = "armor_time_traveler_feet"
display_name = "时间旅者便鞋"
description = "一对由时间结晶与轮回丝编织而成的奇异便鞋，便鞋没有与地面接触——它们行走在时间的丝线上，而不是空间的地面上。行走时，便鞋会在时间中留下短暂的痕迹——不是弱点，是标记，也是通道。\n\n克罗诺斯在编织这对便鞋时，将自己的「时间之步」能力封入了轮回丝。他说："这对便鞋不只是鞋子，它们是'时间的脚步'——让我每一步都走在时间上。"\n\n这对便鞋的特殊效果是：穿戴者可以「时间步」——每日一次，沿着时间的丝线移动（传送至 60 尺内任意位置，如同 dimension door，但不会留下法术痕迹）。且可以用「时间滑行」——每日一次，以三倍速度直线移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。克罗诺斯说："时间旅者不需要路，他只需要时间——时间在哪里，他就走向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "time_traveler_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_time_traveler_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_time_traveler_feet" }
]
```

---

## 套装九十九：宇宙吞噬者（Cosmic Devourer）

> *"宇宙不是永恒的，它是'可以被吞噬的'——就像一切存在最终都会被吞噬一样。"*

**套装主题**：宇宙吞噬者教团「无尽饥饿」的装备。这些吞噬者相信宇宙终将被吞噬，他们的装备由黑洞物质和吞噬丝制成，赋予了穿戴者吞噬物质、释放引力波和从黑洞中重生的能力。集齐四件时，穿戴者获得「宇宙之躯」——可以吞噬任何物质、释放黑洞、从死亡中重生。

**历史渊源**：无尽饥饿的最高吞噬者「宇宙之心」厄瑞玻斯（另一位同名者），是一位据说被黑洞吞噬后又从黑洞中爬出的传奇存在。他说："我不是我，我是'黑洞'——黑洞通过我吞噬，我通过黑洞存在。"

---

### 99.1 宇宙吞噬者头冠

```gdscript
item_id = "armor_cosmic_devourer_head"
display_name = "宇宙吞噬者头冠"
description = "一顶由黑洞物质与吞噬丝锻造的恐怖头冠，头冠呈现出纯粹的黑色——不是布料的黑，是「不存在」的黑。头冠的正面有一个「事件视界」——一个可以看到黑洞内部的窗口。头冠的内部不断有低语声——那是被吞噬星系的声音，它们在说："饥饿……饥饿……"\n\n厄瑞玻斯在锻造这头冠时，将自己的意识都献给了黑洞。他说："这头冠不只是头冠，它是'黑洞的嘴巴'——让黑洞可以通过我吞噬。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 60 尺内所有物质的质量和能量（宇宙视觉）。且可以通过头冠「吞噬」能量——受到任何 damage 时，25% 概率将伤害转化为 HP（黑洞吞噬）。厄瑞玻斯说："黑洞不是只能吞噬，它也可以感知——感知一切存在的质量和能量，然后吞噬它们。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "cosmic_devourer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 25000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_cosmic_devourer_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_cosmic_devourer_head" }
]
```

**2件套（设计预留）**：免疫「charmed」、「frightened」和「poisoned」（黑洞让人超脱一切）；`resistance_force` +20。
**4件套（设计预留）**：每日三次「引力吞噬」： bonus action，吞噬 15 尺内一个目标（目标须通过 DC18 力量豁免，失败则被拉至 5 尺内并受到 3D10 force）；每日一次「黑洞释放」：15 尺半径内所有生物受到 4D10 force 并被 restrained（引力束缚），须通过 DC18 力量豁免，失败则 restrained 1 回合；HP 降至 0 时，有 50% 概率触发「黑洞重生」：恢复至 50% 最大 HP 并释放一次黑洞；10 尺内所有敌人每回合受到 1D10 force（引力光环）。

---

### 99.2 宇宙吞噬者板甲

```gdscript
item_id = "armor_cosmic_devourer_body"
display_name = "宇宙吞噬者板甲"
description = "一副由黑洞物质与吞噬丝锻造的恐怖板甲，板甲呈现出纯粹的黑色，不断有微弱的引力波在甲缝中流动。这副板甲是有「饥饿」的——它会不断吞噬周围的物质，包括光线、热量和生命能量。在板甲附近，光线会弯曲，时间会变慢，空间会扭曲。\n\n厄瑞玻斯在锻造这副板甲时，将一块黑洞碎片嵌入了板甲的核心。他说："这副板甲不只是铠甲，它是'黑洞的胃'——让黑洞可以通过我吞噬一切。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的引力「释放」——每日一次，释放所有引力形成一个黑洞领域（15 尺半径，所有生物每回合受到 2D10 force 并被拉向中心，须通过 DC18 力量豁免，失败则移动至中心，持续 1 分钟）。且板甲会「吞噬」能量——在能量充沛的环境中（魔法区域、元素区域），每小时恢复 1 HP（黑洞吞噬）。厄瑞玻斯说："黑洞不是只能攻击，它也可以治愈——用吞噬来的能量治愈自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "cosmic_devourer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 45000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 8, source_type = "equipment", source_id = "armor_cosmic_devourer_body" },
    { attribute_id = "max_hp", mode = "flat", value = 25, source_type = "equipment", source_id = "armor_cosmic_devourer_body" }
]
```

---

### 99.3 宇宙吞噬者护手

```gdscript
item_id = "armor_cosmic_devourer_hands"
display_name = "宇宙吞噬者护手"
description = "一对由黑洞物质与吞噬丝锻造的恐怖护手，护手呈现出纯粹的黑色，手掌内部不断有引力漩涡在旋转。握紧拳头时，漩涡会汇聚在拳峰；张开手掌时，漩涡会扩散。护手的掌心有「吞噬之印」——可以吞噬任何触碰的物质。\n\n厄瑞玻斯在锻造这对手套时，将自己的双手改造成了「黑洞之口」。他说："这对手套不只是手套，它们是'黑洞的手'——让黑洞可以通过我吞噬。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「吞噬」—— bonus action，触碰一个目标，吞噬它的物质（目标受到 2D10 force，穿戴者恢复等量 HP）。每日三次。且可以用手套「释放」引力冲击——每日一次，释放一道引力冲击（30 尺射程 4D10 force，可以穿透任何实体障碍物并吸引目标）。厄瑞玻斯说："黑洞的手不是只能吞噬，它也可以释放——释放引力的力量，让一切归于黑洞。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "cosmic_devourer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_cosmic_devourer_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_cosmic_devourer_hands" }
]
```

---

### 99.4 宇宙吞噬者胫甲

```gdscript
item_id = "armor_cosmic_devourer_feet"
display_name = "宇宙吞噬者胫甲"
description = "一对由黑洞物质与吞噬丝锻造的恐怖胫甲，胫甲呈现出纯粹的黑色，每一步踏下都会在地面留下短暂的引力扭曲——那是空间被吞噬的痕迹。\n\n厄瑞玻斯在锻造这对胫甲时，将「黑洞之步」能力封入了吞噬丝。他说："这对胫甲不只是鞋子，它们是'黑洞的道路'——让我每一步都在吞噬空间。"\n\n这对胫甲的特殊效果是：穿戴者可以「黑洞步」——每日一次，踏入黑洞并瞬间移动至 60 尺内任何位置（如同 dimension door，但不会留下法术痕迹）。且可以进行「引力冲锋」——每日一次，以三倍速度直线冲锋，路径上留下引力轨迹（轨迹持续 1 回合，任何踩到的生物受到 2D10 force 并被拉向中心）。厄瑞玻斯说："黑洞行者不需要路，他只需要黑洞——黑洞无处不在，所以黑洞行者可以去任何地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "cosmic_devourer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_cosmic_devourer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_cosmic_devourer_feet" }
]
```

---

## 套装一百：创世神兵（Creation Divine）

> *"我创造了世界，也创造了装备。穿上它们，你就穿上了创世本身。"*

**套装主题**：创世神的终极装备「永恒创世」。这是第一百套套装，也是最强大的一套。据说是创世神本人穿戴过的装备，赋予了穿戴者创造和毁灭世界的能力。集齐四件时，穿戴者获得「创世之神」——可以创造生命、毁灭世界、重塑现实。

**历史渊源**：永恒创世的创造者「创世之神」耶和华（另一位同名者），是一位据说创造了整个宇宙的至高存在。他说："我不是在创造装备，我是在'创造创造'——让创造本身成为装备，让装备成为创造。"

---

### 100.1 创世神兵头盔

```gdscript
item_id = "armor_creation_divine_head"
display_name = "创世神兵头盔"
description = "一顶由创世之光与永恒丝锻造的至高头盔，头盔表面不断有宇宙的诞生和毁灭在流动——你可以看到星辰的形成、星系的旋转、黑洞的吞噬、宇宙的终结。头盔的正面有一个「创世之眼」——可以看到所有时间线、所有可能性、所有现实。头盔的顶部有一颗「永恒之心」——一颗包含整个宇宙能量的宝石。\n\n耶和华在锻造这顶头盔时，将自己的「创世之眼」封入了宝石。他说："这顶头盔不只是头盔，它是'宇宙的窗户'——让穿戴者可以看见一切，知道一切，理解一切。"\n\n这顶头盔的特殊效果是：穿戴者可以「看见」一切——看穿所有 illusions、invisibility、etherealness 和 time manipulations（如同 truesight + 预知 + 全知，范围 120 尺）。且可以通过头盔「感知」到 1 里内所有重大事件的发生（宇宙雷达）。耶和华说："创世之神不是只能创造，它也可以感知——感知宇宙的每一次脉动，感知生命的每一次呼吸。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "creation_divine_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 50000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_creation_divine_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_creation_divine_head" }
]
```

**2件套（设计预留）**：所有属性 +1（创世加持）；`perception_bonus` +5；免疫所有精神效果和状态异常。
**4件套（设计预留）**：每日一次「创世」： bonus action，创造一个小型生命（CR 2 的生物，持续 10 分钟）；每日一次「毁灭」： bonus action，毁灭 15 尺半径内的一个目标（目标须通过 DC20 体质豁免，失败则受到 10D10 force 并 stunned 1 回合）；每日一次「重塑现实」： bonus action，重塑 30 尺内的现实（改变地形、天气或元素属性，持续 10 分钟）；免疫所有 damage（创世之神的庇护）；被攻击时 50% 概率 miss（命运的庇护）。

---

### 100.2 创世神兵板甲

```gdscript
item_id = "armor_creation_divine_body"
display_name = "创世神兵板甲"
description = "一副由创世之光与永恒丝锻造的至高板甲，板甲表面不断有宇宙的诞生和毁灭在流动。这副板甲是有「生命」的——它会根据穿戴者的意愿创造或毁灭：需要创造时，板甲会发光并产生生命能量；需要毁灭时，板甲会变暗并吸收周围的一切。板甲的接缝处有「永恒锁」——只有被创世神选中的人才能穿戴。\n\n耶和华在锻造这副板甲时，将自己的「创世之心」封入了金属。他说："这副板甲不只是铠甲，它是'宇宙本身'——让穿戴者可以穿着整个宇宙。"\n\n这副板甲的特殊效果是：穿戴者可以将板甲的创世能量「释放」——每日一次，释放所有创世能量形成一个创世领域（30 尺半径，所有盟友每回合恢复 3D10 HP 并获得所有属性 +2，所有敌人每回合受到 3D10 force 并所有属性 -2，持续 1 分钟）。且板甲会「保护」穿戴者——免疫所有 damage（创世之神的庇护）。耶和华说："创世之神不是只能攻击，它也可以保护——用创世之力来保护盟友，用毁灭之力来惩罚敌人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "creation_divine_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 80000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_creation_divine_body" },
    { attribute_id = "max_hp", mode = "flat", value = 50, source_type = "equipment", source_id = "armor_creation_divine_body" }
]
```

---

### 100.3 创世神兵护手

```gdscript
item_id = "armor_creation_divine_hands"
display_name = "创世神兵护手"
description = "一对由创世之光与永恒丝锻造的至高护手，护手表面不断有宇宙的诞生和毁灭在流动。握紧拳头时，能量会汇聚在拳峰，形成两颗小型的「创世球」——一颗代表创造，一颗代表毁灭。松开拳头时，能量会消散，回到护手表面。护手的掌心有「创世之印」——可以创造或毁灭任何触碰的物质。\n\n耶和华在锻造这对手套时，将自己的「创世之手」封入了金属。他说："这对手套不只是手套，它们是'宇宙的手'——让穿戴者可以用手创造和毁灭。"\n\n这对手套的特殊效果是：穿戴者可以用手掌「创造」或「毁灭」——每日一次，创造一个 5 尺立方体的任何物质（持续 1 小时）或毁灭一个 5 尺立方体的任何物质（造成 5D10 force）。且可以用手套「治愈」或「伤害」——每日三次，触碰一个目标，选择恢复 3D10 HP（创世治愈）或造成 3D10 force（创世伤害）。耶和华说："创世之神的手不是只能创造，它也可以毁灭——创造生命，毁灭邪恶。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "creation_divine_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_creation_divine_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_creation_divine_hands" }
]
```

---

### 100.4 创世神兵胫甲

```gdscript
item_id = "armor_creation_divine_feet"
display_name = "创世神兵胫甲"
description = "一对由创世之光与永恒丝锻造的至高胫甲，胫甲表面不断有宇宙的诞生和毁灭在流动。每一步踏下，大地都会微微震动——不是弱点，是力量，是创世之力在地面上留下的痕迹。\n\n耶和华在锻造这对胫甲时，将「创世之步」能力封入了永恒丝。他说："这对胫甲不只是鞋子，它们是'宇宙的脚步'——让穿戴者每一步都如同创世。"\n\n这对胫甲的特殊效果是：穿戴者可以「创世步」——每日一次，踏入创世之光并瞬间移动至 120 尺内任何位置（如同 greater teleportation，但不会留下法术痕迹）。且可以进行「创世冲锋」——每日一次，以三倍速度直线冲锋，路径上留下创世轨迹（轨迹持续 1 分钟，任何盟友踩到恢复 2D10 HP，任何敌人踩到受到 2D10 force）。耶和华说："创世之神不需要路，他只需要创造——创造路，创造世界，创造宇宙。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "creation_divine_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_creation_divine_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_creation_divine_feet" }
]
```

---

*终极 fantasy 主题套装 91–100 完结 · 共 40 件护甲装备（重甲 20 + 中甲 12 + 轻甲 8）*

---

# 100 套传奇装备套装总览

> 全部 100 套套装，共 400 件护甲装备，已在此系列文档中完整记录。

## 套装分类统计

| 批次 | 套装编号 | 主题类别 | 护甲类型分布 |
|------|----------|----------|-------------|
| 初始 | 1–20 | 经典 fantasy | 重甲 12 + 中甲 4 + 轻甲 4 |
| 扩展一 | 21–30 | 中甲主题 | 中甲 10 |
| 扩展二 | 31–40 | 轻甲主题 | 轻甲 10 |
| 扩展三 | 41–50 | 混合主题 | 重甲 2 + 中甲 3 + 轻甲 5 |
| 扩展四 | 51–60 | 神话传说 | 重甲 3 + 中甲 3 + 轻甲 4 |
| 扩展五 | 61–70 | 暗黑 fantasy | 重甲 2 + 中甲 4 + 轻甲 4 |
| 扩展六 | 71–80 | 东方 fantasy | 重甲 2 + 中甲 3 + 轻甲 5 |
| 扩展七 | 81–90 | 科幻 fantasy 混合 | 重甲 2 + 中甲 3 + 轻甲 5 |
| 扩展八 | 91–100 | 终极 fantasy | 重甲 3 + 中甲 3 + 轻甲 4 |
| **总计** | **1–100** | **全部** | **重甲 29 + 中甲 33 + 轻甲 38** |

## 文档文件列表

- `sets_01_to_05.md` — 套装 1–5
- `sets_06_to_10.md` — 套装 6–10
- `sets_11_to_15.md` — 套装 11–15
- `sets_16_to_20.md` — 套装 16–20
- `sets_21_to_30.md` — 套装 21–30
- `sets_31_to_40.md` — 套装 31–40
- `sets_41_to_50.md` — 套装 41–50
- `sets_51_to_60.md` — 套装 51–60
- `sets_61_to_70.md` — 套装 61–70
- `sets_71_to_80.md` — 套装 71–80
- `sets_81_to_90.md` — 套装 81–90
- `sets_91_to_100.md` — 套装 91–100

---

*100 套传奇装备套装设计文档系列 · 完结*

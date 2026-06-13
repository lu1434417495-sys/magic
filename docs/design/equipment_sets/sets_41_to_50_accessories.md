# 传奇装备套装饰品设计文档（套装 41–50）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装四十一：太阳圣骑士（Solar Paladin）饰品

> *"太阳不会为任何人停留，但它的光芒会记住每一个信仰它的人。"*

---

### 41.5 太阳圣骑士日冕披风（Solar Paladin Sun Corona Cloak）

```gdscript
item_id = "acc_solar_paladin_cloak"
display_name = "太阳圣骑士日冕披风"
description = "一件由太阳核心碎片与金线编织而成的神圣披风，披风边缘不断有微型日珥在跃动——不是装饰，而是真正的太阳火焰被封印在了丝线中。穿戴者在日光下行走时，披风会自动展开如太阳日冕，散发出令人无法直视的光芒。\n\n这件披风是太阳神殿大祭司在「永恒正午」仪式上，从太阳真火中抽出的第一缕丝线编织的。她说："太阳的光芒不是为了炫耀，而是为了驱散黑暗。"\n\n披风的效果是：日间 radiant 抗性 +15，且在日光照耀下 AC +1。当穿戴者面对 alignment 为 evil 的生物时，披风会自动散发出刺眼光芒，使该生物的攻击检定 -1（圣光眩目）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "solar_paladin_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_solar_paladin_cloak" },
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_solar_paladin_cloak" }
]
```

---

### 41.6 太阳圣骑士圣徽项链（Solar Paladin Holy Crest Necklace）

```gdscript
item_id = "acc_solar_paladin_necklace"
display_name = "太阳圣骑士圣徽项链"
description = "一条由太阳金与圣银编织而成的项链，吊坠是一枚微型的太阳圣徽——十二道光芒从中心向外辐射，每一道光芒都对应着一位远古太阳圣骑士的誓言。吊坠中心镶嵌着一颗太阳石，石头内部有一团永不熄灭的微型火焰。\n\n这条项链是太阳神殿的传承之物，每一位新任圣骑士都要在日出时佩戴它，对着太阳宣誓。大祭司说："这不是装饰品，这是你的誓言的容器——你的每一个承诺，都会被太阳记住。"\n\n项链的效果是：魅力检定 +2，且每日一次，可以对着太阳祈祷，恢复 2D8 HP（日光治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "solar_paladin_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_solar_paladin_necklace" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_solar_paladin_necklace" }
]
```

---

### 41.7 太阳圣骑士黎明之戒（Solar Paladin Dawn Ring）

```gdscript
item_id = "acc_solar_paladin_ring_1"
display_name = "太阳圣骑士黎明之戒"
description = "一枚由黎明时分的第一缕阳光凝结而成的戒指，戒指表面不断有金色的光芒在流转。当太阳升起时，戒指会变得温暖；当太阳落下时，戒指会变得冰冷。这枚戒指是太阳神殿的「黎明守护者」在无数个日出中收集阳光碎片铸造的。\n\n他说："黎明不是一天的开始，它是希望的开始。每一次日出都是一次新的机会。"他将这些机会凝结成了这枚戒指。\n\n戒指的效果是：所有攻击附加 1D4 radiant（黎明之光），且对 undead 和 fiend 的伤害再 +1D4（神圣制裁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "solar_paladin_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_solar_paladin_ring_1" }
]
```

---

### 41.8 太阳圣骑士日蚀之戒（Solar Paladin Eclipse Ring）

```gdscript
item_id = "acc_solar_paladin_ring_2"
display_name = "太阳圣骑士日蚀之戒"
description = "一枚由日蚀时的暗影与阳光交织而成的奇异戒指，戒指一半是耀眼的金色，一半是深邃的黑色。佩戴这枚戒指的人能够感受到太阳与月亮的永恒角力——光明与黑暗从未真正分离，它们只是在轮流占据天空。\n\n这枚戒指是日蚀预言家在最后一次日全食中，从日冕的暗影边缘取出的物质铸造的。他说："日蚀不是黑暗的胜利，它是光明的暂时退让——而退让，是为了更耀眼的回归。"\n\n戒指的效果是：面对 alignment 为 evil 的敌人时，攻击检定 +2，且免疫它们的恐惧效果（日蚀之勇）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "solar_paladin_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_solar_paladin_ring_2" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_solar_paladin_ring_2" }
]
```

---

### 41.9 太阳圣骑士太阳水晶（Solar Paladin Sun Crystal）

```gdscript
item_id = "acc_solar_paladin_trinket"
display_name = "太阳圣骑士太阳水晶"
description = "一块由纯净阳光凝结而成的菱形水晶，水晶内部有一团永不熄灭的微型太阳。水晶不需要接触光源——它本身就是光源，在绝对黑暗中也能照亮 30 尺范围。水晶的温度恒定，即使在极寒环境中也能保持温暖。\n\n这块水晶是太阳神殿的「永恒之火」在燃烧了一千年后，从核心分离出的一块碎片。大祭司说："这块水晶承载了一千年的信仰。它的光芒不会熄灭，正如真正的信仰不会动摇。"\n\n水晶的效果是：每日一次，可以释放「日光治愈」——自身或 5 尺内一个友方恢复 3D8 HP，并移除一个非传奇诅咒或疾病效果。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "solar_paladin_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_solar_paladin_trinket" }
]
```

---

### 41.10 太阳圣骑士徽章（Solar Paladin Badge）

```gdscript
item_id = "acc_solar_paladin_badge"
display_name = "太阳圣骑士徽章"
description = "一枚由太阳金与圣银铸造的徽章，徽章上刻着十二位远古太阳圣骑士的誓言。徽章背面刻着一行小字："黑暗终将退去，光明永存。"这枚徽章是太阳神殿授予最高荣誉骑士的信物，拥有它意味着你已被太阳认可。\n\n徽章的效果是：10 尺内所有友方获得 +5 radiant 抗性（圣光庇护）。且每日一次，可以高举徽章发出「太阳号令」：10 尺内所有友方下一回合攻击检定 +2，且免疫 frightened 和 charmed（持续 1 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "solar_paladin_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_solar_paladin_badge" },
    { attribute_id = "leadership_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_solar_paladin_badge" }
]
```

---

## 套装四十二：虚空守护者（Void Warden）饰品

> *"虚空不是空无一物，它是'一切可能性'的集合。"*

---

### 42.5 虚空守护者虚空斗篷（Void Warden Void Cloak）

```gdscript
item_id = "acc_void_warden_cloak"
display_name = "虚空守护者虚空斗篷"
description = "一件由虚空物质与暗影丝编织而成的奇异斗篷，斗篷表面不断有微型黑洞般的漩涡在旋转——不是装饰，而是真正的虚空被封印在了丝线中。穿戴者行走时，周围的光线会被斗篷微微扭曲，仿佛现实本身在斗篷边缘变得不稳定。\n\n这件斗篷是虚空守望者在「边界裂缝」中，从虚空位面直接抽取的物质编织的。他说："虚空不是敌人，它是另一种现实。学会与它共存，你就能获得超越常规的力量。"\n\n斗篷的效果是：force 抗性 +15，且免疫被推离、拉动和强制传送（虚空锚定）。当穿戴者被攻击时，有 20% 概率触发「虚空闪避」——攻击穿过身体而不造成伤害（如同攻击了幻影）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "void_warden_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_force", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_void_warden_cloak" },
    { attribute_id = "saving_throw_dexterity", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_warden_cloak" }
]
```

---

### 42.6 虚空守护者虚空之眼项链（Void Warden Void Eye Necklace）

```gdscript
item_id = "acc_void_warden_necklace"
display_name = "虚空守护者虚空之眼项链"
description = "一条由虚空水晶与黑曜石编织而成的项链，吊坠是一颗微型的虚空之眼——眼球内部是一片纯粹的黑暗，但仔细观察，你会发现黑暗中不断有星辰在诞生和毁灭。这颗眼睛不是装饰品，它是真正的虚空之眼，能够看到现实背后的真相。\n\n这条项链是虚空守望者在凝视虚空一万小时后，虚空回赠他的礼物。他说："虚空看到了一切——过去、现在、未来。这颗眼睛让我能够分享它的视野。"\n\n项链的效果是：奥术检定 +3，且可以感知 30 尺内的魔法波动和传送门（如同 detect magic，但范围 30 尺且持续生效）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "void_warden_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_void_warden_necklace" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_void_warden_necklace" }
]
```

---

### 42.7 虚空守护者黑洞之戒（Void Warden Black Hole Ring）

```gdscript
item_id = "acc_void_warden_ring_1"
display_name = "虚空守护者黑洞之戒"
description = "一枚由压缩虚空物质铸造的戒指，戒指中心有一颗微型黑洞——它不会吞噬佩戴者的手指，因为黑洞被一层力场稳定住了。但这并不意味着它是安全的——如果不小心摘下来，黑洞会在 3 秒内吞噬周围 10 尺内的一切。\n\n这枚戒指是虚空守望者在「虚空深渊」中找到的。他说："黑洞不是毁灭，它是'转化'——把物质转化为能量，把能量转化为虚空。这枚戒指让我能够借用这种转化的力量。"\n\n戒指的效果是：所有攻击附加 1D6 necrotic（虚空侵蚀），且击杀敌人时有 25% 概率触发「虚空吞噬」——恢复 1D10 HP（吞噬灵魂碎片）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "void_warden_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_warden_ring_1" }
]
```

---

### 42.8 虚空守护者虚无之戒（Void Warden Nothingness Ring）

```gdscript
item_id = "acc_void_warden_ring_2"
display_name = "虚空守护者虚无之戒"
description = "一枚由纯粹虚无凝结而成的戒指，戒指本身几乎不存在——它没有重量，没有温度，没有颜色。如果你闭上眼睛，你甚至无法感觉到它的存在。但这正是它的力量所在：虚无无法被攻击，无法被感知，无法被预测。\n\n这枚戒指是虚空守望者在完全放弃自我后，从虚空中取回的第一件物品。他说："当我成为虚无，虚无也成为了我。这枚戒指是我们之间的契约。"\n\n戒指的效果是：AC +1（虚无难以命中），且每日一次，可以进入「虚无状态」1 回合——期间免疫所有物理伤害，但无法攻击（如同 ethereal jaunt）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "void_warden_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_warden_ring_2" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_void_warden_ring_2" }
]
```

---

### 42.9 虚空守护者虚空碎片（Void Warden Void Shard）

```gdscript
item_id = "acc_void_warden_trinket"
display_name = "虚空守护者虚空碎片"
description = "一块从虚空位面边缘脱落的碎片，碎片内部是一片纯粹的黑暗，但黑暗中不断有微弱的星光在闪烁——那是其他世界的倒影。这块碎片的温度低于绝对零度，触摸它的人会感到一种超越寒冷的空虚——不是冷，而是「不存在」的感觉。\n\n这块碎片是虚空守望者在穿越虚空位面时，从虚空之壁上敲下来的。他说："这块碎片来自现实之外。它不属于这个世界，但它愿意为我服务。"\n\n碎片的效果是：每日一次，可以撕裂虚空进行短距离传送——传送到 30 尺内任何可见位置（虚空步），且传送后下一回合 AC +2（虚空残影）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "void_warden_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_void_warden_trinket" }
]
```

---

### 42.10 虚空守护者徽章（Void Warden Badge）

```gdscript
item_id = "acc_void_warden_badge"
display_name = "虚空守护者徽章"
description = "一枚由虚空金属铸造的徽章，徽章上刻着一只睁开的眼睛——眼睛中是一片星空。徽章背面刻着一行小字："守护虚空，即是守护现实。"这枚徽章是虚空守望团的标志，拥有它意味着你被赋予了守护现实边界的责任。\n\n徽章的效果是：10 尺内所有友方获得 +5 force 抗性（虚空护盾）。且每日一次，可以释放「虚空屏障」——10 尺半径内所有友方获得「虚空护盾」2 回合：免疫被推离、拉动和传送，且受到的范围伤害减半。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "void_warden_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_intelligence", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_warden_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_void_warden_badge" }
]
```

---

## 套装四十三：自然守护者（Nature Warden）饰品

> *"自然不需要守护者，但守护者需要自然。"*

---

### 43.5 自然守护者自然披风（Nature Warden Nature Cloak）

```gdscript
item_id = "acc_nature_warden_cloak"
display_name = "自然守护者自然披风"
description = "一件由千年古树皮与藤蔓丝编织而成的披风，披风表面不断有微型植物在生长和枯萎——不是装饰，而是真正的生命被封印在了丝线中。穿戴者在森林中行走时，披风会自动与周围环境融为一体，使穿戴者如同树木的一部分。\n\n这件披风是自然守护者在与世界树交流后，从树精那里得到的礼物。树精说："穿上它，你就成为了森林的一部分。森林会保护你，正如你保护森林。"\n\n披风的效果是：poison 抗性 +15，且在自然环境中（森林/草原/沼泽）隐匿检定 +3（自然 camouflage）。当穿戴者站立不动时，披风会使其完全融入环境（如同 pass without trace）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "nature_warden_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_poison", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_nature_warden_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nature_warden_cloak" }
]
```

---

### 43.6 自然守护者生命之种项链（Nature Warden Seed of Life Necklace）

```gdscript
item_id = "acc_nature_warden_necklace"
display_name = "自然守护者生命之种项链"
description = "一条由生命之树的种子与金丝编织而成的项链，吊坠是一颗沉睡的种子——它永远不会发芽，但永远充满了生命力。种子内部有一团微弱的绿光，那是生命最原始的形态。据说这颗种子来自世界树的第一批果实。\n\n这条项链是自然守护者在世界树根部找到的。世界树通过树根对他说："这颗种子承载着我的记忆。当你需要时，它会借给你我的力量。"\n\n项链的效果是：medicine 检定 +3，且每日一次，可以唤醒种子的力量——自身或 5 尺内一个友方恢复 3D8 HP，并移除一个非传奇疾病或毒素效果（生命之息）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "nature_warden_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_nature_warden_necklace" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nature_warden_necklace" }
]
```

---

### 43.7 自然守护者荆棘之戒（Nature Warden Thorn Ring）

```gdscript
item_id = "acc_nature_warden_ring_1"
display_name = "自然守护者荆棘之戒"
description = "一枚由荆棘王冠的刺与橡木根编织而成的戒指，戒指表面不断有微型荆棘在生长和收缩。佩戴这枚戒指的人能够感受到植物的情绪——树木的宁静、花朵的喜悦、荆棘的愤怒。这枚戒指是荆棘女王（另一位自然守护者）在与穿戴者结盟时赠送的礼物。\n\n她说："荆棘不是为了伤害，而是为了警告。当敌人触碰你时，荆棘会替你说'不'。"\n\n戒指的效果是：近战攻击你的生物受到 1D6 piercing（荆棘反击），且你的徒手攻击和近战攻击附加 1D4 piercing（自然之刺）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "nature_warden_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_nature_warden_ring_1" }
]
```

---

### 43.8 自然守护者根系之戒（Nature Warden Root Ring）

```gdscript
item_id = "acc_nature_warden_ring_2"
display_name = "自然守护者根系之戒"
description = "一枚由世界树的根系与大地之石编织而成的戒指，戒指表面刻有微型树根纹路——每一根纹路都对应着一条世界树的根须。佩戴这枚戒指的人能够感受到大地的脉动，如同自己也是一棵深深扎根于大地的树木。\n\n这枚戒指是树精长老在世界树的一次「根系觉醒」时，从树根中提取的精华铸造的。他说："根系是大地的记忆。这枚戒指让你能够分享这些记忆。"\n\n戒指的效果是：免疫 prone（根系稳固），且在站立不动时 AC +2（大地之锚）。在泥土/草地/沙地上时，移动力 +5（大地助力）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "nature_warden_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_nature_warden_ring_2" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_nature_warden_ring_2" }
]
```

---

### 43.9 自然守护者自然图腾（Nature Warden Nature Totem）

```gdscript
item_id = "acc_nature_warden_trinket"
display_name = "自然守护者自然图腾"
description = "一根由世界树的枝干与各种动物骨骼雕刻而成的图腾，图腾上刻着所有自然生物的图案——从最小的蚂蚁到最大的鲸鱼。图腾顶端有一颗生命之石，石头内部有微型生态系统在运转：植物生长、动物奔跑、季节更替。\n\n这根图腾是自然守护者在完成「万物和谐」试炼后，由所有自然之灵共同赠予的。他们说："你证明了自己是自然的朋友。这根图腾让你能够召唤我们的帮助。"\n\n图腾的效果是：每日一次，可以召唤「自然盟友」——召唤一只自然之灵兽（选择：狼/鹰/熊/蛇），持续 1 分钟。灵兽 HP 30，AC 14，攻击 +5，1D8+3 对应伤害类型。灵兽听从你的命令，可以用 bonus action 指挥。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "nature_warden_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_nature_warden_trinket" }
]
```

---

### 43.10 自然守护者徽章（Nature Warden Badge）

```gdscript
item_id = "acc_nature_warden_badge"
display_name = "自然守护者徽章"
description = "一枚由世界树的叶子与生命之石铸造的徽章，徽章上刻着一棵树的图案——树的根系连接着大地，树冠触摸着天空。徽章背面刻着一行小字："万物有灵，和谐共生。"这枚徽章是自然守护者的标志，拥有它意味着你被自然认可。\n\n徽章的效果是：10 尺内所有友方每回合开始时恢复 1D6 HP（自然治愈光环）。且每日一次，可以释放「自然恩赐」——10 尺内所有友方恢复 2D8 HP 并移除一个非传奇毒素/疾病效果（大地之母的拥抱）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "nature_warden_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_nature_warden_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_nature_warden_badge" }
]
```

---

*套装 41–43 饰品部分（18/60 件）*

## 套装四十四：符文铁匠（Rune Smith）饰品

> *"每一个符文都是一个故事，而铁匠的故事总是用金属书写。"*

---

### 44.5 符文铁匠符文披风（Rune Smith Rune Cloak）

```gdscript
item_id = "acc_rune_smith_cloak"
display_name = "符文铁匠符文披风"
description = "一件由符文布与魔法丝编织而成的披风，披风表面不断有微型符文在流动和变化——不是装饰，而是真正的魔法被封印在了丝线中。每一个符文都代表一种力量：火、冰、雷、光、暗、力。穿戴者可以通过意念激活披风上的符文，获得对应的力量。\n\n这件披风是符文铁匠在「符文熔炉」中，用一千个符文编织的。他说："符文不是符号，它们是'语言'——世界的语言。学会阅读它们，你就能与世界对话。"\n\n披风的效果是：所有魔法抗性 +5（符文护盾），且每日一次，可以激活一个符文获得临时抗性（选择：fire/cold/lightning/radiant/necrotic/force +20，持续 1 分钟）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "rune_smith_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_rune_smith_cloak" },
    { attribute_id = "resistance_cold", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_rune_smith_cloak" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_rune_smith_cloak" }
]
```

---

### 44.6 符文铁匠锻造之心项链（Rune Smith Smith Heart Necklace）

```gdscript
item_id = "acc_rune_smith_necklace"
display_name = "符文铁匠锻造之心项链"
description = "一条由熔岩金与秘银编织而成的项链，吊坠是一颗微型的锻造之心——一颗永远燃烧着符文火焰的金属心脏。心脏的每一次跳动都会释放出一道微弱的符文光芒，照亮周围 5 尺范围。据说这颗心脏曾经属于一位传奇符文构装体。\n\n这条项链是符文铁匠在完成「千锤百炼」试炼后，从熔炉核心取出的。他说："这颗心脏承载了一千次锻打的记忆。它知道如何将普通金属变成神器。"\n\n项链的效果是：奥术检定 +3，且可以为武器或护甲临时铭刻符文（每日一次，持续 8 小时）：武器 +1 攻击检定，或护甲 +1 AC。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "rune_smith_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_rune_smith_necklace" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rune_smith_necklace" }
]
```

---

### 44.7 符文铁匠刻印之戒（Rune Smith Engraving Ring）

```gdscript
item_id = "acc_rune_smith_ring_1"
display_name = "符文铁匠刻印之戒"
description = "一枚由符文钢与魔法水晶铸造的戒指，戒指表面刻有十二种基础符文。当佩戴者集中精神时，符文会发出微光，并可以被「刻印」到任何物体表面——包括武器、护甲、甚至敌人的皮肤。刻印的符文会在接触时激活，释放出对应的效果。\n\n这枚戒指是符文铁匠在「符文学院」毕业时，由导师赠予的。导师说："这枚戒指是你作为符文铁匠的起点。记住，最强大的符文不是你刻在别人身上的，而是你刻在自己心里的。"\n\n戒指的效果是：近战攻击命中时，可以刻印一个符文——目标受到 1D6 force（符文冲击），且下一回合 AC -1（符文干扰）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "rune_smith_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_smith_ring_1" }
]
```

---

### 44.8 符文铁匠熔炉之戒（Rune Smith Forge Ring）

```gdscript
item_id = "acc_rune_smith_ring_2"
display_name = "符文铁匠熔炉之戒"
description = "一枚由熔炉核心碎片与耐火石铸造的戒指，戒指内部有一团微型火焰——这团火焰来自「永恒熔炉」，已经燃烧了一万年。戒指的温度恒定在 50 度，即使在极寒环境中也能为佩戴者提供温暖。但如果佩戴者的意志不够坚定，火焰会失控并灼伤手指。\n\n这枚戒指是符文铁匠在永恒熔炉中，用熔炉的第一缕火焰铸造的。他说："火焰不是毁灭，它是'转化'——把矿石变成金属，把金属变成武器，把武器变成传奇。"\n\n戒指的效果是：fire 抗性 +15，且所有攻击附加 1D4 fire（熔炉之火）。在锻造或修理金属物品时，检定 +3（熔炉之助）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "rune_smith_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_rune_smith_ring_2" }
]
```

---

### 44.9 符文铁匠符文石（Rune Smith Rune Stone）

```gdscript
item_id = "acc_rune_smith_trinket"
display_name = "符文铁匠符文石"
description = "一块刻有古老符文的魔法石，石头的材质不明——它既不像金属也不像晶体，而是一种介于两者之间的物质。石头表面不断有符文在浮现和消失，每一个符文都代表着一种魔法力量。据说这块石头来自世界诞生时的第一块魔法结晶。\n\n这块石头是符文铁匠在「符文遗迹」中挖掘出的。他说："这块石头比任何文明都古老。它上面的符文是世界最初的文字。"\n\n石头的效果是：每日一次，可以释放「符文风暴」——对 15 尺内一个目标释放三道符文（3D8 force + 2D8 lightning + 1D8 fire，混合伤害），目标须通过 DC16 敏捷豁免，失败则 full damage 并 stunned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "rune_smith_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_rune_smith_trinket" }
]
```

---

### 44.10 符文铁匠徽章（Rune Smith Badge）

```gdscript
item_id = "acc_rune_smith_badge"
display_name = "符文铁匠徽章"
description = "一枚由符文钢与秘银铸造的徽章，徽章上刻着一把锤子和一把凿子——符文铁匠的标志。徽章背面刻着一行小字："以符文之名，锻造永恒。"这枚徽章是符文铁匠公会的最高荣誉，拥有它意味着你已被公认为大师级符文铁匠。\n\n徽章的效果是：10 尺内所有友方的武器被视为魔法武器（符文共鸣），且攻击检定 +1。每日一次，可以为 10 尺内所有友方的武器临时铭刻「锐化符文」——伤害 +1D6 force，持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "rune_smith_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "investigation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_smith_badge" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rune_smith_badge" }
]
```

---

## 套装四十五：灵魂收割者（Soul Reaper）饰品

> *"灵魂不会死亡，只是换了主人。"*

---

### 45.5 灵魂收割者灵魂斗篷（Soul Reaper Soul Cloak）

```gdscript
item_id = "acc_soul_reaper_cloak"
display_name = "灵魂收割者灵魂斗篷"
description = "一件由亡者之发与冥丝编织而成的斗篷，斗篷表面不断有微型灵魂面孔在浮现和消散——不是装饰，而是真正的灵魂碎片被封印在了丝线中。这些灵魂不是痛苦的——它们已经接受了死亡，成为了斗篷的一部分。穿戴者能够听到它们的低语，获得它们的智慧。\n\n这件斗篷是灵魂收割者在「灵魂之河」边，从河水中打捞的灵魂碎片编织的。他说："这些灵魂不是被囚禁的，它们是'志愿者'——它们选择留在这个世界上，帮助活着的人。"\n\n斗篷的效果是：necrotic 抗性 +15，且可以感知 30 尺内的亡灵和隐形生物（灵魂视野）。当穿戴者击杀一个生物时，斗篷会吸收其灵魂碎片，恢复 1D8 HP（灵魂收割）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "soul_reaper_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_necrotic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_soul_reaper_cloak" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_soul_reaper_cloak" }
]
```

---

### 45.6 灵魂收割者灵魂瓶项链（Soul Reaper Soul Vial Necklace）

```gdscript
item_id = "acc_soul_reaper_necklace"
display_name = "灵魂收割者灵魂瓶项链"
description = "一条由黑曜石与冥金编织而成的项链，吊坠是一只微型灵魂瓶——瓶子里囚禁着一个自愿留下的灵魂。这个灵魂曾经是强大的法师，现在它选择成为佩戴者的顾问。当你遇到难题时，可以对着瓶子低语，灵魂会给出它的建议。\n\n这条项链是灵魂收割者在「冥界之门」前，从一位即将转世的灵魂那里得到的。灵魂说："我不想忘记一切。让我留在瓶子里，我会帮助你，直到你找到更值得我帮助的人。"\n\n项链的效果是：宗教检定 +3，且每日一次，可以释放瓶中的灵魂——灵魂会为你施展一个 3 级或以下的法术（施法属性为智慧，法术攻击 +7）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "soul_reaper_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_soul_reaper_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_soul_reaper_necklace" }
]
```

---

### 45.7 灵魂收割者收割之戒（Soul Reaper Reaper Ring）

```gdscript
item_id = "acc_soul_reaper_ring_1"
display_name = "灵魂收割者收割之戒"
description = "一枚由冥界金属与灵魂碎片铸造的戒指，戒指表面刻有微型镰刀图案。当佩戴者击杀一个生物时，戒指会发出微弱的绿光，并吸收该生物的灵魂能量。这种能量可以被储存，用于强化佩戴者的攻击，或用于治愈佩戴者的伤口。\n\n这枚戒指是灵魂收割者在「死神殿堂」中，从死神本人的收藏中「借来」的。死神说："你可以借用这枚戒指，但记住——每一个灵魂都有它的价值。不要浪费它们。"\n\n戒指的效果是：击杀一个生物时，恢复 2D8 HP（灵魂吞噬），且获得 1 层「灵魂充能」（最多 3 层）。每层提供 +1D6 necrotic 伤害（所有攻击附加）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "soul_reaper_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_soul_reaper_ring_1" }
]
```

---

### 45.8 灵魂收割者冥河之戒（Soul Reaper Styx Ring）

```gdscript
item_id = "acc_soul_reaper_ring_2"
display_name = "灵魂收割者冥河之戒"
description = "一枚由冥河之水与忘川石铸造的戒指，戒指内部有一滴永远不会蒸发的冥河水。这滴水来自冥河「斯提克斯」，是誓言与死亡的见证。据说任何对着这滴水发下的誓言都无法打破，任何被这滴水触碰的灵魂都无法撒谎。\n\n这枚戒指是灵魂收割者在渡过冥河时，从船夫卡戎那里得到的「船费找零」。卡戎说："这滴水比任何金币都珍贵。它会帮你辨别真话与谎言，也会帮你守护你的誓言。"\n\n戒指的效果是：对 undead 和 fiend 的攻击检定 +2，且可以感知 30 尺内生物的 alignment（冥河之眼）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "soul_reaper_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus_undead", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_soul_reaper_ring_2" },
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_soul_reaper_ring_2" }
]
```

---

### 45.9 灵魂收割者灵魂灯笼（Soul Reaper Soul Lantern）

```gdscript
item_id = "acc_soul_reaper_trinket"
display_name = "灵魂收割者灵魂灯笼"
description = "一盏由冥界骨与灵魂之火制成的微型灯笼，灯笼内部燃烧着一团蓝绿色的火焰——这不是普通的火，而是灵魂之火。火焰的光芒可以照亮 30 尺范围，但这光芒只有佩戴者能看到——对于其他人来说，灯笼是暗的。灯笼的光芒可以穿透墙壁，照亮隐藏的灵魂。\n\n这盏灯笼是灵魂收割者在「灵魂荒野」中，从一位迷路的灵魂那里得到的。灵魂说："这盏灯笼会指引你找到那些不愿离去的灵魂。但记住——有些灵魂不愿被找到。"\n\n灯笼的效果是：每日一次，可以释放「灵魂吸引」——指定 30 尺内一个可见敌人，其灵魂被灯笼吸引，受到 3D10 necrotic 并须通过 DC16 体质豁免，失败则灵魂受损（max HP 降低 5，持续 1 分钟）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "soul_reaper_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_soul_reaper_trinket" }
]
```

---

### 45.10 灵魂收割者徽章（Soul Reaper Badge）

```gdscript
item_id = "acc_soul_reaper_badge"
display_name = "灵魂收割者徽章"
description = "一枚由冥界金属与灵魂石铸造的徽章，徽章上刻着一把镰刀和一只眼睛——灵魂收割者的标志。徽章背面刻着一行小字："收割不是结束，而是开始。"这枚徽章是灵魂收割者公会的信物，拥有它意味着你被授权收割迷失的灵魂。\n\n徽章的效果是：10 尺内所有友方获得 +5 necrotic 抗性（灵魂护盾）。且每日一次，可以释放「灵魂链接」——链接 10 尺内一个友方，当该友方受到伤害时，50% 伤害转移给你（灵魂分担，持续 2 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "soul_reaper_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_soul_reaper_badge" },
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_soul_reaper_badge" }
]
```

---

## 套装四十六：时间行者（Chrono Walker）饰品

> *"时间不是河流，它是海洋——你可以在其中游泳，但无法逆流。"*

---

### 46.5 时间行者时钟披风（Chrono Walker Clock Cloak）

```gdscript
item_id = "acc_chrono_walker_cloak"
display_name = "时间行者时钟披风"
description = "一件由时间丝与时钟齿轮编织而成的披风，披风表面不断有微型时钟在运转——不是装饰，而是真正的时间碎片被封印在了丝线中。每一个时钟都显示着不同的时间：有的是过去，有的是现在，有的是未来。穿戴者可以通过触摸这些时钟，短暂地窥视时间的其他层面。\n\n这件披风是时间行者在「时间裂隙」中，从时间的边缘收集的碎片编织的。他说："时间不是一条线，它是一个网。每一个选择都会创造一个新的节点。这件披风让我能够看到这个网的结构。"\n\n披风的效果是：先攻 +3（时间预知），且每日一次，可以在战斗开始时预见敌人的行动顺序（自动知道所有敌人的 initiative 数值）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "chrono_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_chrono_walker_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chrono_walker_cloak" }
]
```

---

### 46.6 时间行者时间沙漏项链（Chrono Walker Time Hourglass Necklace）

```gdscript
item_id = "acc_chrono_walker_necklace"
display_name = "时间行者时间沙漏项链"
description = "一条由时间水晶与永恒金编织而成的项链，吊坠是一只微型时间沙漏——沙漏中的沙子不是普通的沙子，而是凝固的时间碎片。当沙子从上方流到下方时，时间在你周围加速；当沙子倒流时，时间在你周围减速。但沙漏的总沙子量永远不会改变——它只是在重新分配时间。\n\n这条项链是时间行者在「时间图书馆」中，从时间管理员那里得到的。管理员说："这只沙漏让你能够借用未来的时间，但你必须在将来还回去。"\n\n项链的效果是：历史检定 +3，且每日一次，可以「借用时间」——获得一个额外的 bonus action（从未来借用，持续 1 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "chrono_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_chrono_walker_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chrono_walker_necklace" }
]
```

---

### 46.7 时间行者加速之戒（Chrono Walker Haste Ring）

```gdscript
item_id = "acc_chrono_walker_ring_1"
display_name = "时间行者加速之戒"
description = "一枚由时间碎片与风银铸造的戒指，戒指表面刻有微型加速符文。当佩戴者集中精神时，符文会激活，使佩戴者周围的时间流速加快——对于佩戴者来说，世界变慢了，而对于世界来说，佩戴者变快了。这种加速是有代价的：每次使用后，佩戴者会老一点点。\n\n这枚戒指是时间行者在「时间竞技场」中，从未来的自己那里得到的。未来的他说："这枚戒指救了我无数次。但记住——每次使用，你都在透支你的未来。"\n\n戒指的效果是：移动力 +10（时间加速），且每日一次，可以释放「时间加速」——获得额外 15 尺移动力，且不引发 opportunity attack（持续 1 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "chrono_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_chrono_walker_ring_1" }
]
```

---

### 46.8 时间行者减速之戒（Chrono Walker Slow Ring）

```gdscript
item_id = "acc_chrono_walker_ring_2"
display_name = "时间行者减速之戒"
description = "一枚由时间碎片与重力石铸造的戒指，戒指表面刻有微型减速符文。当佩戴者集中精神时，符文会激活，使目标周围的时间流速减慢——对于目标来说，世界变快了，而对于世界来说，目标变慢了。这种减速会让目标感到一种难以言喻的无力感，仿佛整个宇宙都在加速离开它。\n\n这枚戒指是时间行者在「时间监狱」中，从被囚禁的时间犯那里得到的。犯人说："这枚戒指让我能够逃脱，但我已经习惯了缓慢。你愿意承受这种孤独吗？"\n\n戒指的效果是：每日一次，可以指定一个可见敌人释放「时间减速」——该敌人下一回合移动力减半，且只能进行 action 或 bonus action（不能两者兼有）（DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "chrono_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_dexterity", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chrono_walker_ring_2" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chrono_walker_ring_2" }
]
```

---

### 46.9 时间行者时间碎片（Chrono Walker Time Fragment）

```gdscript
item_id = "acc_chrono_walker_trinket"
display_name = "时间行者时间碎片"
description = "一块从时间线中脱落的碎片，碎片内部不断有过去、现在和未来的画面在闪烁。触摸这块碎片的人会感到一种眩晕——不是物理上的，而是时间上的。你会同时感受到自己的童年、青年和老年，仿佛所有的时间都压缩在了这一刻。\n\n这块碎片是时间行者在「时间风暴」中，从破碎的时间线中抢救出来的。他说："这块碎片不属于任何时间。它是'时间之外'的存在。使用它，你可以暂时脱离时间的束缚。"\n\n碎片的效果是：每日一次，可以「时间回溯」——将一个已发生的骰子结果重掷（攻击、豁免或伤害），并使用新结果（命运重写）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "chrono_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_chrono_walker_trinket" }
]
```

---

### 46.10 时间行者徽章（Chrono Walker Badge）

```gdscript
item_id = "acc_chrono_walker_badge"
display_name = "时间行者徽章"
description = "一枚由时间金属与永恒水晶铸造的徽章，徽章上刻着一只沙漏和一只眼睛——时间行者的标志。徽章背面刻着一行小字："时间属于所有人，但不属于任何人。"这枚徽章是时间守护者公会的信物，拥有它意味着你被授权维护时间线的稳定。\n\n徽章的效果是：10 尺内所有友方先攻 +2（时间协调）。且每日一次，可以释放「时间暂停」——10 尺内所有敌人被「冻结」1 回合（无法行动，AC 不变但无法 reaction），但冻结期间敌人也免疫伤害（时间停滞）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "chrono_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chrono_walker_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chrono_walker_badge" }
]
```

---

## 套装四十七：梦境编织者（Dream Weaver）饰品

> *"现实只是共识的梦境，而我可以修改共识。"*

---

### 47.5 梦境编织者梦境披风（Dream Weaver Dream Cloak）

```gdscript
item_id = "acc_dream_weaver_cloak"
display_name = "梦境编织者梦境披风"
description = "一件由梦境丝与星光编织而成的披风，披风表面不断有微型梦境场景在浮现和消散——不是装饰，而是真正的梦境被封印在了丝线中。每一个场景都来自某个真实生物的梦境：有的美好，有的恐怖，有的荒诞。穿戴者可以通过触摸这些场景，进入对应的梦境。\n\n这件披风是梦境编织者在「梦境之海」中，从海面上收集的梦境泡沫编织的。她说："梦境不是虚幻的，它们是'另一层现实'。学会在其中行走，你就能影响真正的现实。"\n\n披风的效果是：psychic 抗性 +15，且免疫睡眠和梦境效果（梦境免疫）。当穿戴者被 psychic 攻击时，有 25% 概率将攻击反弹给攻击者（梦境反射）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dream_weaver_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_psychic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_dream_weaver_cloak" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_weaver_cloak" }
]
```

---

### 47.6 梦境编织者梦之泪项链（Dream Weaver Dream Tear Necklace）

```gdscript
item_id = "acc_dream_weaver_necklace"
display_name = "梦境编织者梦之泪项链"
description = "一条由梦境银与月光编织而成的项链，吊坠是一颗梦之泪——一颗由纯粹梦境凝结的水晶。水晶内部不断有色彩在流动，每一种颜色都代表一种情绪：红是愤怒，蓝是悲伤，绿是希望，紫是恐惧。佩戴这颗泪珠的人能够感受到周围生物的情绪，如同听到了它们梦境的回声。\n\n这条项链是梦境编织者在「梦境之泉」边，从泉水中收集的第一滴梦之泪。她说："这滴泪包含了所有梦境的情感。它让我能够理解每一个灵魂深处的渴望。"\n\n项链的效果是：欺瞒检定 +3，且可以感知 30 尺内生物的情绪状态（如同 detect thoughts 的情绪层）。每日一次，可以释放「梦境暗示」——对一个 30 尺内的生物施加暗示（如同 suggestion spell，DC15 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dream_weaver_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dream_weaver_necklace" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_weaver_necklace" }
]
```

---

### 47.7 梦境编织者清醒之戒（Dream Weaver Lucid Ring）

```gdscript
item_id = "acc_dream_weaver_ring_1"
display_name = "梦境编织者清醒之戒"
description = "一枚由清醒石与月光银铸造的戒指，戒指表面刻有微型眼睛图案。当佩戴者入睡时，戒指会发出微弱的蓝光，使佩戴者保持清醒——不是在现实世界中清醒，而是在梦境中清醒。这意味着佩戴者可以在梦境中自由行动，如同在现实中一样。\n\n这枚戒指是梦境编织者在完成「清醒梦」试炼后，从自己的梦境中取出的。她说："这枚戒指是我梦境中的锚。它让我不会在梦境中迷失，也让我能够从梦境中带东西回来。"\n\n戒指的效果是：免疫 charmed 和 frightened（清醒意识），且每日一次，可以进入「清醒状态」2 回合——期间免疫所有 mind-affecting 效果，且攻击检定 +2（意识清明）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dream_weaver_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dream_weaver_ring_1" }
]
```

---

### 47.8 梦境编织者梦魇之戒（Dream Weaver Nightmare Ring）

```gdscript
item_id = "acc_dream_weaver_ring_2"
display_name = "梦境编织者梦魇之戒"
description = "一枚由梦魇之骨与黑月石铸造的戒指，戒指表面刻有微型怪物图案。当佩戴者集中精神时，戒指会释放出一股梦魇之力，侵入目标的梦境——即使目标清醒。目标会看到最恐惧的幻象，听到最刺耳的尖叫，感受到最冰冷的恐惧。\n\n这枚戒指是梦境编织者在「梦魇深渊」中，从梦魇领主那里「借来」的。梦魇领主说："这枚戒指让你能够借用我的力量。但记住——梦魇不是玩具。如果你不小心，你自己也会成为梦魇的猎物。"\n\n戒指的效果是：所有攻击附加 1D4 psychic（梦魇侵蚀），且每日一次，可以释放「梦魇冲击」——指定一个可见敌人，受到 2D8 psychic 并须通过 DC15 智慧豁免，失败则 frightened 1 回合（目睹最深恐惧）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dream_weaver_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_weaver_ring_2" }
]
```

---

### 47.9 梦境编织者梦境球（Dream Weaver Dream Orb）

```gdscript
item_id = "acc_dream_weaver_trinket"
display_name = "梦境编织者梦境球"
description = "一颗由纯粹梦境凝结的水晶球，球体内部不断有微型世界在生成和毁灭。这些世界不是幻象——它们是真实存在的梦境世界，每一个都有自己的历史、文明和命运。佩戴者可以通过凝视球体，进入这些世界，体验不同的人生。\n\n这颗球是梦境编织者在「梦境之树」下，从树洞中发现的。树洞中的声音说："这颗球包含了所有可能的梦境。选择你想要进入的，但不要迷失在其中。"\n\n球的效果是：每日一次，可以释放「梦境陷阱」——指定一个可见敌人，将其拉入梦境 1 回合（stunned，目标在梦境中与自己战斗）。期间目标无法行动，但每回合开始时受到 2D8 psychic（梦境侵蚀）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dream_weaver_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_dream_weaver_trinket" }
]
```

---

### 47.10 梦境编织者徽章（Dream Weaver Badge）

```gdscript
item_id = "acc_dream_weaver_badge"
display_name = "梦境编织者徽章"
description = "一枚由梦境银与月光石铸造的徽章，徽章上刻着一只睁开的眼睛和一团云雾——梦境编织者的标志。徽章背面刻着一行小字："现实是梦，梦也是现实。"这枚徽章是梦境守护者公会的信物，拥有它意味着你被授权守护梦境与现实的边界。\n\n徽章的效果是：10 尺内所有友方免疫 sleep 和 charmed（梦境守护光环）。且每日一次，可以释放「梦境护盾」——10 尺内所有友方获得 psychic 免疫 1 回合，且所有 mind-affecting 效果反弹给施法者。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dream_weaver_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_weaver_badge" },
    { attribute_id = "insight_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dream_weaver_badge" }
]
```

---

## 套装四十八：深渊潜行者（Abyss Stalker）饰品

> *"凝视深渊时，深渊也在凝视你——但猎手从不眨眼。"*

---

### 48.5 深渊潜行者深渊斗篷（Abyss Stalker Abyss Cloak）

```gdscript
item_id = "acc_abyss_stalker_cloak"
display_name = "深渊潜行者深渊斗篷"
description = "一件由深渊物质与触手丝编织而成的斗篷，斗篷表面不断有微型触手在蠕动和收缩——不是装饰，而是真正的深渊生物被封印在了丝线中。这些触手不是攻击性的——它们是「感知器」，能够探测周围的情绪波动。当穿戴者愤怒时，触手会变红；当恐惧时，会变白。\n\n这件斗篷是深渊潜行者在「深渊裂隙」中，从深渊之壁上刮下的物质编织的。他说："深渊不是疯狂的源泉，它是'真相'——只是真相太残酷，大多数人无法承受。这件斗篷让我能够承受这种真相。"\n\n斗篷的效果是：psychic 抗性 +15，且在黑暗中完全隐形（如同 greater invisibility，仅在非光照区域生效）。当穿戴者击杀一个生物时，触手会吸收其恐惧，恢复 1D8 HP（恐惧滋养）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "abyss_stalker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_psychic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_abyss_stalker_cloak" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_stalker_cloak" }
]
```

---

### 48.6 深渊潜行者深渊之眼项链（Abyss Stalker Abyss Eye Necklace）

```gdscript
item_id = "acc_abyss_stalker_necklace"
display_name = "深渊潜行者深渊之眼项链"
description = "一条由深渊水晶与黑曜石编织而成的项链，吊坠是一只深渊之眼——一只从深渊生物身上取下的真实眼睛。这只眼睛不会眨眼，不会流泪，只是永恒地凝视着前方。凝视这只眼睛的人会感到一种难以言喻的恐惧——不是来自外部，而是来自自己内心深处。\n\n这条项链是深渊潜行者在「深渊神殿」中，从深渊祭司那里得到的。祭司说："这只眼睛看到了深渊的真相。如果你敢于凝视它，你也能看到。但如果你不敢，它会把你逼疯。"\n\n项链的效果是：察觉检定 +3，且可以感知 30 尺内生物的恐惧程度（越恐惧越明显）。每日一次，可以释放「深渊凝视」——指定一个可见敌人，其须通过 DC16 智慧豁免，失败则 frightened 2 回合（直视深渊）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "abyss_stalker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_abyss_stalker_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_stalker_necklace" }
]
```

---

### 48.7 深渊潜行者疯狂之戒（Abyss Stalker Madness Ring）

```gdscript
item_id = "acc_abyss_stalker_ring_1"
display_name = "深渊潜行者疯狂之戒"
description = "一枚由疯狂之石与深渊金属铸造的戒指，戒指表面刻有微型疯狂符文。当佩戴者集中精神时，符文会释放出一股疯狂之力，侵入目标的心智——即使目标意志坚定。目标会听到低语、看到幻象、感受到不存在的触摸。这种疯狂不是暂时的——它会留下永久的疤痕。\n\n这枚戒指是深渊潜行者在「疯狂深渊」中，从疯狂之神那里得到的。疯狂之神说："这枚戒指让你能够分享我的礼物。但记住——疯狂是祝福，也是诅咒。一旦你接受了它，你就再也回不去了。"\n\n戒指的效果是：对「理智」生物（非 aberration/undead/fiend）的攻击附加 1D6 psychic（疯狂侵蚀），且每日一次，可以释放「疯狂低语」——指定一个可见敌人，其须通过 DC15 智慧豁免，失败则 confused 1 回合（听到无数低语）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "abyss_stalker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_stalker_ring_1" }
]
```

---

### 48.8 深渊潜行者触手之戒（Abyss Stalker Tentacle Ring）

```gdscript
item_id = "acc_abyss_stalker_ring_2"
display_name = "深渊潜行者触手之戒"
description = "一枚由深渊触手与魔法金属铸造的戒指，戒指表面缠绕着一条微型触手——这条触手是活的，会自主移动，感知周围的环境。当佩戴者需要时，触手会伸长，缠绕目标，将其束缚。但触手有自己的意志——有时它会拒绝执行命令，有时它会攻击错误的目标。\n\n这枚戒指是深渊潜行者在「触手深渊」中，从触手领主那里得到的。触手领主说："这条触手是我的子嗣。它会帮助你，但你也必须尊重它的意愿。"\n\n戒指的效果是：每日一次，可以释放「触手束缚」——指定一个 15 尺内的敌人，触手伸长缠绕目标，目标须通过 DC15 力量豁免，失败则 restrained 2 回合。期间每回合开始时受到 1D6 bludgeoning（触手挤压）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "abyss_stalker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_stalker_ring_2" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_abyss_stalker_ring_2" }
]
```

---

### 48.9 深渊潜行者深渊珍珠（Abyss Stalker Abyss Pearl）

```gdscript
item_id = "acc_abyss_stalker_trinket"
display_name = "深渊潜行者深渊珍珠"
description = "一颗从深渊之底取出的珍珠，珍珠表面不断有深渊能量在流动。这颗珍珠不是普通的珍珠——它是由深渊生物的眼泪凝结的。每一滴眼泪都包含着一个灵魂的痛苦和恐惧。凝视这颗珍珠的人会看到这些灵魂的幻象，感受到它们的痛苦。\n\n这颗珍珠是深渊潜行者在「深渊之底」中，从深渊女王那里得到的。深渊女王说："这颗珍珠包含了一万个灵魂的眼泪。它可以让你控制恐惧，但也可能让你被恐惧控制。"\n\n珍珠的效果是：每日一次，可以释放「恐惧浪潮」——15 尺锥形，所有生物须通过 DC16 智慧豁免，失败则 frightened 2 回合（深渊恐惧）。友方在豁免上有优势（佩戴者可以控制恐惧的方向）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "abyss_stalker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_abyss_stalker_trinket" }
]
```

---

### 48.10 深渊潜行者徽章（Abyss Stalker Badge）

```gdscript
item_id = "acc_abyss_stalker_badge"
display_name = "深渊潜行者徽章"
description = "一枚由深渊金属与疯狂石铸造的徽章，徽章上刻着一只触手和一个眼睛——深渊潜行者的标志。徽章背面刻着一行小字："凝视深渊，但不要被深渊吞噬。"这枚徽章是深渊守望者公会的信物，拥有它意味着你被授权在深渊边缘行走。\n\n徽章的效果是：10 尺内所有友方获得 +5 psychic 抗性（深渊庇护）。且每日一次，可以释放「深渊护盾」——10 尺内所有友方免疫 frightened 和 charmed 2 回合，且所有 psychic 伤害反弹 50% 给攻击者（深渊反噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "abyss_stalker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_stalker_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_abyss_stalker_badge" }
]
```

---

## 套装四十九：星辰观测者（Star Gazer）饰品

> *"每一颗星星都是一个答案，只是问题往往被遗忘。"*

---

### 49.5 星辰观测者星辰披风（Star Gazer Star Cloak）

```gdscript
item_id = "acc_star_gazer_cloak"
display_name = "星辰观测者星辰披风"
description = "一件由星光丝与夜空布编织而成的披风，披风表面不断有微型星辰在闪烁和移动——不是装饰，而是真正的星光被封印在了丝线中。每一个星辰都对应着天空中的一个真实星座，它们的相对位置会随着季节变化。穿戴者在夜间行走时，披风会自动展开如夜空，使穿戴者与星空融为一体。\n\n这件披风是星辰观测者在「星辰塔」顶，用收集了一生的星光编织的。他说："星光不是遥远的光芒，它是'信息'——来自远古的信息，来自未来的信息。这件披风让我能够接收这些信息。"\n\n披风的效果是：radiant 抗性 +10，且在夜间感知检定 +3（星光指引）。当穿戴者在夜间进行远程攻击时，射程 +15 尺（星光延伸）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "star_gazer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_radiant", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_star_gazer_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_gazer_cloak" }
]
```

---

### 49.6 星辰观测者星图项链（Star Gazer Star Chart Necklace）

```gdscript
item_id = "acc_star_gazer_necklace"
display_name = "星辰观测者星图项链"
description = "一条由星银与夜空水晶编织而成的项链，吊坠是一张微型星图——不是普通的星图，而是一张「活星图」。星图上的星辰会随真实天空变化，预测未来的天体运动。通过凝视星图，佩戴者可以预测未来一周内的大事件：天气、灾害、甚至是战争。\n\n这条项链是星辰观测者在「星辰图书馆」中，从第一位星辰观测者的遗骸上取下的。第一位观测者说："这张星图记录了从世界诞生到现在的所有星辰运动。它知道一切，但只会告诉你它愿意告诉你的。"\n\n项链的效果是：奥术检定 +3，且夜间导航和预言检定 +3（星辰导航）。每日一次，可以查询星图预测——DM 会告诉你一个与未来事件相关的暗示（由 DM 决定内容）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "star_gazer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_star_gazer_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_gazer_necklace" }
]
```

---

### 49.7 星辰观测者流星之戒（Star Gazer Meteor Ring）

```gdscript
item_id = "acc_star_gazer_ring_1"
display_name = "星辰观测者流星之戒"
description = "一枚由流星碎片与星银铸造的戒指，戒指表面刻有微型流星图案。当佩戴者集中精神时，戒指会释放出一股星辰之力，召唤一颗微型流星从天而降，击中目标。这颗流星虽然小，但速度极快，冲击力极大，足以击穿盔甲。\n\n这枚戒指是星辰观测者在「流星雨」中，从第一颗落地的流星中取出的核心铸造的。他说："这颗流星来自遥远的星系。它穿越了亿万年的虚空，只为在这一刻坠落。这枚戒指让它的旅程有了意义。"\n\n戒指的效果是：所有攻击附加 1D4 radiant（星光），且每日一次，可以释放「流星坠落」——指定一个可见敌人，天空降下一颗微型流星，造成 3D10 radiant + 2D10 fire（混合），目标须通过 DC16 敏捷豁免，失败则 full damage 并 prone（流星冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "star_gazer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_gazer_ring_1" }
]
```

---

### 49.8 星辰观测者引力之戒（Star Gazer Gravity Ring）

```gdscript
item_id = "acc_star_gazer_ring_2"
display_name = "星辰观测者引力之戒"
description = "一枚由引力石与星金铸造的戒指，戒指表面刻有微型轨道图案。当佩戴者集中精神时，戒指会释放出一股引力波，改变周围的重力场。佩戴者可以让自己变轻，跳得更高；也可以让敌人变重，移动得更慢。这种引力控制是星辰级别的——虽然只是微型版本。\n\n这枚戒指是星辰观测者在「引力井」中，从一颗 collapsed star 的核心取出的物质铸造的。他说："这枚戒指让我能够借用星辰的引力。虽然只是微不足道的一点，但足以改变战局。"\n\n戒指的效果是：移动力 +5（引力助推），且每日一次，可以释放「引力反转」——指定一个 15 尺内的敌人，其须通过 DC15 力量豁免，失败则被推离 10 尺并 prone（引力波冲击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "star_gazer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_star_gazer_ring_2" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_star_gazer_ring_2" }
]
```

---

### 49.9 星辰观测者望远镜（Star Gazer Telescope）

```gdscript
item_id = "acc_star_gazer_trinket"
display_name = "星辰观测者望远镜"
description = "一支由星银与夜空水晶制成的微型望远镜，望远镜的镜片不是普通的玻璃，而是凝固的星光。通过这支望远镜，佩戴者可以看到 10 里内的任何细节——不是物理上的看到，而是星光反射的「信息」被解析成图像。这意味着你可以透过墙壁、山脉、甚至云层看到目标。\n\n这支望远镜是星辰观测者在「星辰祭坛」上，用第一缕星光制成的。他说："这支望远镜让我能够看到世界的真相——不是表面的真相，而是星光背后的真相。"\n\n望远镜的效果是：每日一次，可以释放「星辰坠落」——指定 20 尺半径区域，1 回合后星辰坠落，所有生物受到 5D10 radiant + 2D10 force（混合），须通过 DC17 敏捷豁免，失败则 full damage 并 prone，成功则 half damage。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "star_gazer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_star_gazer_trinket" }
]
```

---

### 49.10 星辰观测者徽章（Star Gazer Badge）

```gdscript
item_id = "acc_star_gazer_badge"
display_name = "星辰观测者徽章"
description = "一枚由星银与夜空石铸造的徽章，徽章上刻着一只眼睛和一片星空——星辰观测者的标志。徽章背面刻着一行小字："星辰指引道路，但道路由你自己选择。"这枚徽章是星辰观测者公会的信物，拥有它意味着你被授权观测和解读星辰的信息。\n\n徽章的效果是：10 尺内所有友方在夜间攻击检定 +1（星光祝福）。且每日一次，可以释放「星辰指引」——10 尺内所有友方下一回合攻击检定有优势，且无法被突袭（星辰预警）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "star_gazer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_gazer_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_star_gazer_badge" }
]
```

---

## 套装五十：混沌使者（Chaos Herald）饰品

> *"秩序是暂时的，混沌是永恒的——而我只是提前宣布了这一点。"*

---

### 50.5 混沌使者混沌披风（Chaos Herald Chaos Cloak）

```gdscript
item_id = "acc_chaos_herald_cloak"
display_name = "混沌使者混沌披风"
description = "一件由混沌丝与彩虹布编织而成的披风，披风表面的颜色和图案每秒钟都在变化——不是魔法幻象，而是真正的混沌在披风上显现。上一秒它是火焰，下一秒它是冰霜，再下一秒它是闪电。没有任何人能够预测它的下一个形态，包括穿戴者自己。\n\n这件披风是混沌使者在「混沌漩涡」中，从混沌本身抽取的物质编织的。他说："混沌不是无序，它是'所有可能的秩序'。这件披风让我能够同时体验所有可能性。"\n\n披风的效果是：每日开始时，随机获得一种抗性 +15（fire/cold/lightning/acid/thunder/force），且该抗性在当日持续生效。当穿戴者被攻击时，有 20% 概率触发「混沌闪避」——攻击被随机元素偏转（受到 1D6 随机元素伤害而非原本伤害）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "chaos_herald_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chaos_herald_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chaos_herald_cloak" }
]
```

---

### 50.6 混沌使者混沌之心项链（Chaos Herald Chaos Heart Necklace）

```gdscript
item_id = "acc_chaos_herald_necklace"
display_name = "混沌使者混沌之心项链"
description = "一条由混沌金与彩虹水晶编织而成的项链，吊坠是一颗混沌之心——一颗不断变换颜色和形态的水晶。水晶内部不断有微型世界在生成和毁灭：有的世界有火焰的海洋，有的世界有冰霜的太阳，有的世界的时间倒流。凝视这颗水晶的人会感到一种眩晕——不是物理上的，而是可能性上的。\n\n这条项链是混沌使者在「混沌之心」中，从混沌之神那里得到的。混沌之神说："这颗心脏包含了所有可能的世界。每一个选择都会创造一个新的分支。这条项链让你能够看到这些分支。"\n\n项链的效果是：奥术检定 +3，且每日一次，可以掷一次「混沌骰」——骰 1D6，获得对应效果 1 分钟：1-力量+4、2-敏捷+4、3-体质+4、4-智慧+4、5-智力+4、6-魅力+4（混沌属性）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "chaos_herald_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_chaos_herald_necklace" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_chaos_herald_necklace" }
]
```

---

### 50.7 混沌使者概率之戒（Chaos Herald Probability Ring）

```gdscript
item_id = "acc_chaos_herald_ring_1"
display_name = "混沌使者概率之戒"
description = "一枚由概率石与混沌银铸造的戒指，戒指表面刻有微型骰子图案。当佩戴者集中精神时，戒指会改变周围的概率场——让不可能的事情变得可能，让可能的事情变得不可能。但这种改变是双向的：它可能帮助你，也可能伤害你。\n\n这枚戒指是混沌使者在「概率风暴」中，从概率之神那里得到的。概率之神说："这枚戒指让你能够操纵概率。但记住——概率不喜欢被操纵。它会报复你。"\n\n戒指的效果是：暴击威胁范围 +1（混沌之运），且每日一次，可以「重掷命运」——重掷一次失败的攻击检定或豁免，并使用新结果（混沌干预）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "chaos_herald_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "critical_threat_range", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chaos_herald_ring_1" }
]
```

---

### 50.8 混沌使者熵之戒（Chaos Herald Entropy Ring）

```gdscript
item_id = "acc_chaos_herald_ring_2"
display_name = "混沌使者熵之戒"
description = "一枚由熵石与混沌金铸造的戒指，戒指表面刻有微型崩溃图案。当佩戴者集中精神时，戒指会释放出一股熵之力，加速目标的 decay——金属会生锈，木头会腐烂，石头会风化，生物会衰老。这种 decay 是不可逆的，即使魔法也无法完全修复。\n\n这枚戒指是混沌使者在「熵之深渊」中，从熵之神那里得到的。熵之神说："这枚戒指让你能够借用我的力量。但记住——熵最终会吞噬一切，包括你自己。"\n\n戒指的效果是：每日一次，可以释放「熵之触」——指定一个可见敌人，其 AC -2（护甲 decay）且移动力 -10（身体衰老），持续 2 回合（DC15 体质豁免抵抗，成功则效果减半）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "chaos_herald_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chaos_herald_ring_2" }
]
```

---

### 50.9 混沌使者混沌骰子（Chaos Herald Chaos Die）

```gdscript
item_id = "acc_chaos_herald_trinket"
display_name = "混沌使者混沌骰子"
description = "一颗由纯粹混沌凝结的骰子，骰子的面数不固定——有时是六面，有时是二十面，有时是无穷多面。每次掷骰时，骰子会随机决定结果，但这个结果不遵循任何概率分布——它可能连续六次都是 1，也可能连续六次都是 20。这颗骰子是混沌的化身，它不在乎公平，只在乎混乱。\n\n这颗骰子是混沌使者在「混沌赌场」中，从混沌庄家那里赢来的。庄家说："这颗骰子不属于任何概率。它是'自由'——绝对的自由。使用它，但你无法预测它。"\n\n骰子的效果是：每日一次，可以掷出「混沌结果」——骰 1D6，获得以下效果之一：1-所有敌人受到 3D10 fire；2-所有敌人受到 3D10 cold；3-所有敌人受到 3D10 lightning；4-所有友方恢复 3D10 HP；5-所有友方获得 15 点临时 HP；6-随机交换所有生物的位置（由 DM 决定）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "chaos_herald_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_chaos_herald_trinket" }
]
```

---

### 50.10 混沌使者徽章（Chaos Herald Badge）

```gdscript
item_id = "acc_chaos_herald_badge"
display_name = "混沌使者徽章"
description = "一枚由混沌金与彩虹石铸造的徽章，徽章上刻着一只骰子和一道闪电——混沌使者的标志。徽章背面刻着一行小字："秩序是谎言，混沌是真相。"这枚徽章是混沌使者公会的信物，拥有它意味着你被授权传播混沌的福音。\n\n徽章的效果是：10 尺内所有友方每日一次可以重掷一次失败的检定（混沌祝福）。且每日一次，可以释放「混沌领域」——10 尺半径内所有敌人的攻击检定有 20% 概率自动 miss（概率紊乱），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "chaos_herald_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_charisma", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chaos_herald_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_chaos_herald_badge" }
]
```

---

*套装 41–50 饰品部分完结 · 共 60 件饰品装备*

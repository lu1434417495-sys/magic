# 传奇装备套装饰品设计文档（套装 51–60）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装五十一：雷神之甲（Thunder God's Armor）饰品

> *"雷声不是警告，它是宣判。"*

---

### 51.5 雷神之甲雷云披风（Thunder God Storm Cloak）

```gdscript
item_id = "acc_thunder_god_cloak"
display_name = "雷神之甲雷云披风"
description = "一件由雷云丝与风暴布编织而成的披风，披风表面不断有微型闪电在跳跃——不是装饰，而是真正的雷电被封印在了丝线中。当穿戴者愤怒时，披风会自动聚集电荷，发出低沉的雷鸣声。在雷暴天气中，披风会与天空的雷电共鸣，使穿戴者成为雷电的导体。\n\n这件披风是雷神祭司在「雷霆之巅」，从第一道雷电中抽出的能量编织的。他说："雷电不是愤怒，它是'力量'——纯粹的力量。这件披风让我能够借用这种力量。"\n\n披风的效果是：lightning 抗性 +20，且在雷暴天气中 AC +2（雷电护盾）。当穿戴者被近战攻击时，攻击者受到 1D6 lightning（雷电反击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "thunder_god_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_lightning", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_thunder_god_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thunder_god_cloak" }
]
```

---

### 51.6 雷神之甲雷霆护符项链（Thunder God Thunder Amulet Necklace）

```gdscript
item_id = "acc_thunder_god_necklace"
display_name = "雷神之甲雷霆护符项链"
description = "一条由雷霆金与风暴水晶编织而成的项链，吊坠是一枚雷霆护符——护符内部有一团微型雷云，雷云中不断有闪电在闪烁。这枚护符是雷神亲自祝福过的，拥有它意味着你已被雷电认可。据说当护符碎裂时，会释放出一道真正的天雷。\n\n这条项链是雷神祭司在「雷神祭坛」上，用雷电击中的第一块金属铸造的。他说："这枚护符承载了雷神的意志。当你需要时，雷电会回应你的召唤。"\n\n项链的效果是：宗教检定 +3，且每日一次，可以召唤「雷电之怒」——天空降下一道雷电，对指定敌人造成 3D10 lightning（DC16 敏捷豁免 half），并 stunned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "thunder_god_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_thunder_god_necklace" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thunder_god_necklace" }
]
```

---

### 51.7 雷神之甲风暴之戒（Thunder God Storm Ring）

```gdscript
item_id = "acc_thunder_god_ring_1"
display_name = "雷神之甲风暴之戒"
description = "一枚由风暴核心与雷电银铸造的戒指，戒指表面不断有电弧在跳跃。当佩戴者集中精神时，戒指会释放出一股风暴之力，召唤一阵强风环绕佩戴者。这阵风可以推开敌人，也可以加速佩戴者的移动。但风暴是不可控的——有时它会帮助佩戴者，有时它会伤害佩戴者。\n\n这枚戒指是雷神祭司在「风暴之眼」中，从风暴核心取出的物质铸造的。他说："这枚戒指让我能够借用风暴的力量。但风暴是自由的——它不会永远服从你。"\n\n戒指的效果是：所有攻击附加 1D4 lightning（雷电），且每日一次，可以释放「风暴步伐」——移动力 +20，且经过的每个敌人受到 1D8 lightning（DC14 敏捷豁免 half），持续 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "thunder_god_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thunder_god_ring_1" }
]
```

---

### 51.8 雷神之甲静电之戒（Thunder God Static Ring）

```gdscript
item_id = "acc_thunder_god_ring_2"
display_name = "雷神之甲静电之戒"
description = "一枚由静电石与雷电金铸造的戒指，戒指表面覆盖着一层微型静电场。当佩戴者移动时，静电场会积累电荷；当电荷达到临界点时，会释放出一道强大的电击。这种电击可以击晕敌人，也可以破坏魔法装置。但积累电荷的过程会让佩戴者的头发竖起，手指发麻。\n\n这枚戒指是雷神祭司在「静电平原」中，从静电生物身上取下的核心铸造的。他说："这枚戒指让我能够储存雷电。但储存太多会很危险——我曾经不小心电焦了自己的眉毛。"\n\n戒指的效果是：移动力 +5，且每日一次，可以释放「静电爆发」——5 尺半径 2D10 lightning，所有生物须通过 DC15 敏捷豁免，失败则 stunned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "thunder_god_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_thunder_god_ring_2" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thunder_god_ring_2" }
]
```

---

### 51.9 雷神之甲雷电核心（Thunder God Thunder Core）

```gdscript
item_id = "acc_thunder_god_trinket"
display_name = "雷神之甲雷电核心"
description = "一颗由纯粹雷电凝结的核心，核心内部有一团永不熄灭的雷电风暴。这颗核心的温度极高，触摸它的人会感到手指发麻，头发竖起。但如果佩戴者能够承受这种能量，核心会赋予他操控雷电的能力。据说这颗核心来自雷神本人的心脏。\n\n这颗核心是雷神祭司在「雷神神殿」中，从雷神的雕像心脏位置取出的。他说："这颗核心承载了雷神的一部分力量。它不是给凡人的礼物——它是给勇者的考验。"\n\n核心的效果是：每日一次，可以释放「雷神之锤」——指定一个可见敌人，天空降下雷电之锤，造成 5D10 lightning + 3D10 thunder（混合），目标须通过 DC17 体质豁免，失败则 full damage 并 stunned 2 回合，成功则 half damage 并 stunned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "thunder_god_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_thunder_god_trinket" }
]
```

---

### 51.10 雷神之甲徽章（Thunder God Badge）

```gdscript
item_id = "acc_thunder_god_badge"
display_name = "雷神之甲徽章"
description = "一枚由雷霆金与风暴石铸造的徽章，徽章上刻着一道闪电和一把锤子——雷神的标志。徽章背面刻着一行小字："雷声是神的声音，闪电是神的意志。"这枚徽章是雷神祭司的标志，拥有它意味着你已被雷神认可。\n\n徽章的效果是：10 尺内所有友方获得 +5 lightning 抗性（雷电庇护）。且每日一次，可以释放「雷电共鸣」——10 尺内所有友方的武器附加 1D6 lightning（雷电附魔），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "thunder_god_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thunder_god_badge" },
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thunder_god_badge" }
]
```

---

## 套装五十二：美杜莎之凝视（Medusa's Gaze）饰品

> *"美杜莎的头发不是蛇，它们是'警告'——不要看她的眼睛。"*

---

### 52.5 美杜莎之凝视蛇鳞披风（Medusa's Gaze Scale Cloak）

```gdscript
item_id = "acc_medusa_gaze_cloak"
display_name = "美杜莎之凝视蛇鳞披风"
description = "一件由美杜莎的蛇鳞与魔法丝编织而成的披风，披风表面覆盖着微型蛇鳞，每一片鳞片都是活的——它们会自主蠕动，感知周围的环境。当穿戴者受到威胁时，鳞片会竖起来，释放出微弱的石化毒素。这些毒素不足以石化敌人，但足以让它们感到迟缓。\n\n这件披风是猎蛇者在「美杜莎巢穴」中，从死去的蛇发上取下的鳞片编织的。他说："这些鳞片曾经是美杜莎的一部分。它们记得她的愤怒，也记得她的孤独。这件披风让我能够分享她的力量，也分享她的诅咒。"\n\n披风的效果是：poison 抗性 +15，且近战攻击者受到 1D4 poison（鳞片毒素）。当穿戴者被凝视时，有 25% 概率反射石化效果（蛇鳞反噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "medusa_gaze_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_poison", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_medusa_gaze_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_medusa_gaze_cloak" }
]
```

---

### 52.6 美杜莎之凝视石化之眼项链（Medusa's Gaze Petrify Eye Necklace）

```gdscript
item_id = "acc_medusa_gaze_necklace"
display_name = "美杜莎之凝视石化之眼项链"
description = "一条由黑曜石与蛇金编织而成的项链，吊坠是一只石化之眼——一只从美杜莎头上取下的真实蛇眼。这只眼睛不会眨眼，不会转动，只是永恒地凝视着前方。凝视这只眼睛的人会感到一种难以抗拒的沉重感——仿佛身体正在慢慢变成石头。\n\n这条项链是猎蛇者在击败美杜莎后，从她头上取下的第一只眼睛制成的。他说："这只眼睛承载了美杜莎的诅咒。它不是给弱者的装饰品——它是给强者的武器。"\n\n项链的效果是：察觉检定 +3，且每日一次，可以释放「石化凝视」——指定一个 15 尺内的敌人，其须通过 DC16 体质豁免，失败则开始石化（移动力减半，AC +2 但无法 bonus action，持续 2 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "medusa_gaze_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_medusa_gaze_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_medusa_gaze_necklace" }
]
```

---

### 52.7 美杜莎之凝视蛇毒之戒（Medusa's Gaze Venom Ring）

```gdscript
item_id = "acc_medusa_gaze_ring_1"
display_name = "美杜莎之凝视蛇毒之戒"
description = "一枚由蛇毒石与黑曜金铸造的戒指，戒指表面刻有微型蛇牙图案。当佩戴者集中精神时，戒指会释放出一股蛇毒，注入目标的血液。这种毒不是致命的——它会让目标感到麻痹、迟缓、视觉模糊。但如果目标体质虚弱，毒可能会永久损伤其神经系统。\n\n这枚戒指是猎蛇者在「蛇毒沼泽」中，从千年蛇王身上取下的毒牙铸造的。他说："这枚戒指的毒比任何刀剑都锋利。它不会立刻杀死敌人，但它会让敌人希望被立刻杀死。"\n\n戒指的效果是：所有攻击附加 1D4 poison（蛇毒），且每日一次，可以释放「蛇毒喷射」——5 尺射程 2D8 poison，目标须通过 DC15 体质豁免，失败则 poisoned 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "medusa_gaze_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_medusa_gaze_ring_1" }
]
```

---

### 52.8 美杜莎之凝视石肤之戒（Medusa's Gaze Stone Skin Ring）

```gdscript
item_id = "acc_medusa_gaze_ring_2"
display_name = "美杜莎之凝视石肤之戒"
description = "一枚由石化石与蛇骨铸造的戒指，戒指表面覆盖着微型石化纹路。当佩戴者集中精神时，戒指会释放出一股石化之力，使佩戴者的皮肤暂时变成石头。这种石化不是完全的——它只影响外层皮肤，提供额外的防御，但不会影响行动。然而，如果石化持续太久，佩戴者可能会永远变成石头。\n\n这枚戒指是猎蛇者在「石化森林」中，从第一棵被美杜莎石化的树上取下的核心铸造的。他说："这枚戒指让我能够借用石化的力量。但每一次使用，我都感到自己的身体变得更重，更冷。"\n\n戒指的效果是：每日一次，可以释放「石肤术」——AC +3，免疫 poison 和 bleed，但移动力 -5（石肤状态），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "medusa_gaze_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_medusa_gaze_ring_2" },
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_medusa_gaze_ring_2" }
]
```

---

### 52.9 美杜莎之凝视蛇发玩偶（Medusa's Gaze Snake Hair Doll）

```gdscript
item_id = "acc_medusa_gaze_trinket"
display_name = "美杜莎之凝视蛇发玩偶"
description = "一个由美杜莎的蛇发与魔法布制成的微型玩偶，玩偶的头发是活的蛇——它们会自主移动，攻击靠近的敌人。玩偶的眼睛是两颗微型宝石，宝石内部有微弱的石化光芒。据说这个玩偶是美杜莎在还是人类时制作的，是她最后的「正常」记忆。\n\n这个玩偶是猎蛇者在「美杜莎密室」中，从她的枕头下发现的。他说："这个玩偶让我看到了美杜莎的另一面——她不是怪物，她是受害者。但这个玩偶也是武器——它的蛇发会保护我。"\n\n玩偶的效果是：每日一次，可以释放「蛇发攻击」——蛇发伸长攻击 15 尺内一个敌人，造成 3D8 piercing + 2D8 poison，目标须通过 DC16 敏捷豁免，失败则 full damage 并 poisoned 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "medusa_gaze_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_medusa_gaze_trinket" }
]
```

---

### 52.10 美杜莎之凝视徽章（Medusa's Gaze Badge）

```gdscript
item_id = "acc_medusa_gaze_badge"
display_name = "美杜莎之凝视徽章"
description = "一枚由黑曜石与蛇金铸造的徽章，徽章上刻着一只眼睛和一条蛇——美杜莎的标志。徽章背面刻着一行小字："凝视我，然后成为我。"这枚徽章是猎蛇者公会的信物，拥有它意味着你已击败或驯服了蛇发女妖。\n\n徽章的效果是：10 尺内所有友方获得 +5 poison 抗性（蛇毒庇护）。且每日一次，可以释放「石化光环」——10 尺内所有敌人移动力减半（石化恐惧），持续 2 回合（DC15 体质豁免抵抗，成功则效果减半）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "medusa_gaze_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_medusa_gaze_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_medusa_gaze_badge" }
]
```

---

## 套装五十三：凤凰重生（Phoenix Rebirth）饰品

> *"凤凰不是不会死，它只是懂得如何优雅地复活。"*

---

### 53.5 凤凰重生烈焰披风（Phoenix Rebirth Flame Cloak）

```gdscript
item_id = "acc_phoenix_rebirth_cloak"
display_name = "凤凰重生烈焰披风"
description = "一件由凤凰羽毛与火焰丝编织而成的披风，披风表面不断有微型火焰在燃烧——不是装饰，而是真正的火焰被封印在了丝线中。这些火焰不会烧伤穿戴者，但会烧伤任何试图触碰披风的敌人。当穿戴者受伤时，火焰会变得更旺，仿佛在愤怒。\n\n这件披风是凤凰使者在「凤凰巢穴」中，从凤凰蜕下的第一根羽毛编织的。他说："这根羽毛承载了凤凰的记忆——它的死亡，它的重生，它的永恒。这件披风让我能够分享这种永恒。"\n\n披风的效果是：fire 抗性 +20，且在 HP 低于 50% 时，火焰自动爆发——5 尺半径 2D6 fire（凤凰怒火）。当穿戴者 HP 降至 0 时，有 25% 概率自动恢复 1D12 HP（凤凰余烬，每场战斗一次）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "phoenix_rebirth_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_phoenix_rebirth_cloak" },
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phoenix_rebirth_cloak" }
]
```

---

### 53.6 凤凰重生涅槃之心项链（Phoenix Rebirth Nirvana Heart Necklace）

```gdscript
item_id = "acc_phoenix_rebirth_necklace"
display_name = "凤凰重生涅槃之心项链"
description = "一条由凤凰心金与火焰水晶编织而成的项链，吊坠是一颗涅槃之心——一颗由凤凰的心脏碎片凝结的宝石。宝石内部有一团微型火焰，火焰中不断有凤凰的虚影在飞舞、死亡、重生。据说这颗心脏经历了九十九次涅槃，每一次都让它更加强大。\n\n这条项链是凤凰使者在「涅槃祭坛」上，从凤凰的灰烬中找到的。他说："这颗心脏承载了九十九次死亡和重生的记忆。它知道如何在最黑暗的时刻找到光明。"\n\n项链的效果是：宗教检定 +3，且每日一次，可以释放「涅槃之火」——自身或 5 尺内一个友方恢复 3D8 HP，并移除一个非传奇疾病或诅咒效果（凤凰治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "phoenix_rebirth_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_phoenix_rebirth_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_phoenix_rebirth_necklace" }
]
```

---

### 53.7 凤凰重生重生之戒（Phoenix Rebirth Rebirth Ring）

```gdscript
item_id = "acc_phoenix_rebirth_ring_1"
display_name = "凤凰重生重生之戒"
description = "一枚由涅槃石与凤凰金铸造的戒指，戒指表面刻有微型凤凰图案。当佩戴者死亡时，戒指会自动激活，释放出一股涅槃之力，将佩戴者从死亡的边缘拉回。但这种力量不是无限的——每次使用，戒指上的凤凰图案就会变淡一点。当图案完全消失时，戒指就会碎裂。\n\n这枚戒指是凤凰使者在「凤凰神殿」中，从凤凰的眼泪中取出的精华铸造的。他说："这枚戒指给了我第二次生命。但每一次使用，我都感到自己的一部分永远留在了死亡的那一边。"\n\n戒指的效果是：max HP +10（生命强化），且每场战斗一次，当 HP 降至 0 时，自动恢复 1D10 HP（凤凰重生）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "phoenix_rebirth_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_phoenix_rebirth_ring_1" }
]
```

---

### 53.8 凤凰重生灰烬之戒（Phoenix Rebirth Ash Ring）

```gdscript
item_id = "acc_phoenix_rebirth_ring_2"
display_name = "凤凰重生灰烬之戒"
description = "一枚由凤凰灰烬与火焰银铸造的戒指，戒指表面覆盖着一层微型灰烬。当佩戴者集中精神时，灰烬会重新燃烧，释放出凤凰的火焰。这种火焰可以治愈伤口，也可以焚烧敌人。但灰烬是有限的——每次使用后，戒指会变得更轻，更脆弱。\n\n这枚戒指是凤凰使者在「灰烬平原」中，从凤凰最后一次涅槃的灰烬中取出的核心铸造的。他说："这枚戒指让我能够从灰烬中召唤火焰。但记住——火焰需要燃料，而燃料终将耗尽。"\n\n戒指的效果是：每日一次，可以释放「灰烬之火」——选择治愈或攻击：治愈：自身或 5 尺内友方恢复 2D10 HP；攻击：5 尺射程 3D10 fire（凤凰灰烬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "phoenix_rebirth_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_phoenix_rebirth_ring_2" },
    { attribute_id = "resistance_fire", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_phoenix_rebirth_ring_2" }
]
```

---

### 53.9 凤凰重生凤凰蛋（Phoenix Rebirth Phoenix Egg）

```gdscript
item_id = "acc_phoenix_rebirth_trinket"
display_name = "凤凰重生凤凰蛋"
description = "一颗微型的凤凰蛋，蛋壳由纯粹火焰结晶而成，蛋壳内部有一团微弱的火光——这是凤凰的胚胎，正在沉睡。这颗蛋不会孵化——它永远处于「即将孵化」的状态。但当佩戴者需要时，可以借用蛋中的生命力，治愈伤口或强化攻击。\n\n这颗蛋是凤凰使者在「凤凰巢穴」中，从凤凰的巢里取出的。他说："这颗蛋包含了无限的可能性。它既是死亡，也是重生；既是终结，也是开始。"\n\n蛋的效果是：每日一次，可以释放「凤凰涅槃」——当 HP 降至 0 时，自动触发：恢复至 max HP 的 30%，对 10 尺内所有敌人造成 3D10 fire（凤凰爆发），且自身获得「火焰化身」2 回合——攻击附加 1D10 fire，免疫 fire。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "phoenix_rebirth_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_phoenix_rebirth_trinket" }
]
```

---

### 53.10 凤凰重生徽章（Phoenix Rebirth Badge）

```gdscript
item_id = "acc_phoenix_rebirth_badge"
display_name = "凤凰重生徽章"
description = "一枚由凤凰金与火焰石铸造的徽章，徽章上刻着一只展翅的凤凰——凤凰重生的标志。徽章背面刻着一行小字："从灰烬中升起，比从前更强大。"这枚徽章是凤凰使者的信物，拥有它意味着你已理解重生的真谛。\n\n徽章的效果是：10 尺内所有友方获得 +5 fire 抗性（火焰庇护）。且每日一次，可以释放「凤凰祝福」——10 尺内所有友方获得「涅槃」2 回合：HP 降至 0 时自动恢复 1D10 HP（每场战斗一次），且攻击附加 1D6 fire。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "phoenix_rebirth_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_phoenix_rebirth_badge" },
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_phoenix_rebirth_badge" }
]
```

---

*套装 51–53 饰品部分（18/60 件）*

## 套装五十四：塞壬之歌（Siren's Song）饰品

> *"塞壬的歌声不是音乐，它是'召唤'——召唤船只撞向礁石，召唤水手走向深渊。"*

---

### 54.5 塞壬之歌海藻披风（Siren's Song Seaweed Cloak）

```gdscript
item_id = "acc_siren_song_cloak"
display_name = "塞壬之歌海藻披风"
description = "一件由深海海藻与人鱼丝编织而成的披风，披风表面不断有微型水流在流动——不是装饰，而是真正的海水被封印在了丝线中。当穿戴者入水时，披风会自动展开如鱼尾，提供强大的推进力。当穿戴者歌唱时，披风会随着歌声的节奏波动，增强歌声的魅惑效果。\n\n这件披风是塞壬在「深海宫殿」中，从自己的鱼尾上取下的鳞片编织的。她说："这件披风让我能够将海洋的力量带到陆地上。但它也让我永远无法忘记海洋的呼唤。"\n\n披风的效果是：水中移动力 +15，且在水中可以完全隐形（如同 greater invisibility，仅在水中生效）。当穿戴者进行表演时，魅惑效果 DC +1（歌声增强）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "siren_song_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_siren_song_cloak" },
    { attribute_id = "performance_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_siren_song_cloak" }
]
```

---

### 54.6 塞壬之歌海妖之泪项链（Siren's Song Siren Tear Necklace）

```gdscript
item_id = "acc_siren_song_necklace"
display_name = "塞壬之歌海妖之泪项链"
description = "一条由深海珍珠与月光银编织而成的项链，吊坠是一颗海妖之泪——一颗由塞壬的眼泪凝结的珍珠。珍珠内部有微型海洋在运转：潮汐起伏、鱼群游动、珊瑚生长。据说这颗眼泪来自一位爱上人类水手的塞壬，她在水手死后流下了这颗永恒的眼泪。\n\n这条项链是塞壬在「珊瑚墓」中，从自己的眼泪中取出的第一颗珍珠制成的。她说："这颗眼泪承载了我的悲伤，也承载了我的力量。当你佩戴它时，你会感受到海洋的所有情感。"\n\n项链的效果是：表演检定 +3，且每日一次，可以释放「塞壬之歌」——30 尺半径内所有敌人须通过 DC16 智慧豁免，失败则 charmed 2 回合（被歌声迷惑，期间只能向歌声来源移动）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "siren_song_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_siren_song_necklace" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_siren_song_necklace" }
]
```

---

### 54.7 塞壬之歌魅惑之戒（Siren's Song Charm Ring）

```gdscript
item_id = "acc_siren_song_ring_1"
display_name = "塞壬之歌魅惑之戒"
display_name = "塞壬之歌魅惑之戒"
description = "一枚由魅惑石与深海金铸造的戒指，戒指表面刻有微型音符图案。当佩戴者说话时，戒指会释放出一股魅惑之力，使听者更容易相信佩戴者。这种魅惑不是强制的——它只是让听者更愿意相信，更愿意服从。但如果佩戴者滥用这种力量，戒指会反噬，让佩戴者陷入无尽的孤独。\n\n这枚戒指是塞壬在「魅惑深渊」中，从魅惑之神那里得到的。魅惑之神说："这枚戒指让你能够借用我的力量。但记住——魅惑的代价是孤独。越是魅惑他人，你越是无法被真正理解。"\n\n戒指的效果是：欺瞒和说服检定 +2（魅惑之声），且每日一次，可以释放「魅惑凝视」——指定一个 15 尺内的敌人，其须通过 DC15 魅力豁免，失败则 charmed 1 回合（被佩戴者吸引）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "siren_song_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_siren_song_ring_1" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_siren_song_ring_1" }
]
```

---

### 54.8 塞壬之歌潮汐之戒（Siren's Song Tide Ring）

```gdscript
item_id = "acc_siren_song_ring_2"
display_name = "塞壬之歌潮汐之戒"
description = "一枚由潮汐石与月光银铸造的戒指，戒指表面刻有微型潮汐图案。当佩戴者集中精神时，戒指会释放出一股潮汐之力，召唤一阵强浪。这阵浪可以推开敌人，也可以将盟友从危险中拉出。但潮汐是不可控的——有时它会帮助佩戴者，有时它会将佩戴者卷入深渊。\n\n这枚戒指是塞壬在「潮汐神殿」中，从潮汐之神那里得到的。潮汐之神说："这枚戒指让我能够借用潮汐的力量。但潮汐是自由的——它不会永远服从你。"\n\n戒指的效果是：每日一次，可以释放「潮汐冲击」——15 尺锥形，所有生物受到 3D8 bludgeoning（潮汐冲击），须通过 DC15 力量豁免，失败则被推离 10 尺并 prone。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "siren_song_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_siren_song_ring_2" }
]
```

---

### 54.9 塞壬之歌海螺号角（Siren's Song Conch Horn）

```gdscript
item_id = "acc_siren_song_trinket"
display_name = "塞壬之歌海螺号角"
description = "一支由深海巨螺与魔法骨制成的号角，号角的表面覆盖着微型海洋生物的图案。当号角被吹响时，会发出一种超越人类听觉频率的声音——这种声音可以穿透海水，召唤深海生物，或者震碎敌人的耳膜。但号角的代价是：每次吹响，吹号者会失去一部分听力。\n\n这支号角是塞壬在「深海废墟」中，从沉没的古代战舰上找到的。她说："这支号角曾经属于一位古代海神。它的声音可以召唤风暴，也可以平息风暴。但它也会召唤来你不想要的东西。"\n\n号角的效果是：每日一次，可以吹响「塞壬号角」——30 尺半径内所有敌人受到 3D10 thunder（音波冲击），须通过 DC16 体质豁免，失败则 deafened 2 回合且 stunned 1 回合。水中使用时范围变为 60 尺。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "siren_song_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_siren_song_trinket" }
]
```

---

### 54.10 塞壬之歌徽章（Siren's Song Badge）

```gdscript
item_id = "acc_siren_song_badge"
display_name = "塞壬之歌徽章"
description = "一枚由深海金与月光石铸造的徽章，徽章上刻着一只鱼尾和一个音符——塞壬的标志。徽章背面刻着一行小字："歌声是陷阱，也是救赎。"这枚徽章是塞壬公会的信物，拥有它意味着你已被海洋认可。\n\n徽章的效果是：10 尺内所有友方在水中移动力 +10（海洋祝福）。且每日一次，可以释放「海洋之歌」——10 尺内所有友方恢复 2D8 HP，并在水中获得 10 尺额外移动力，持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "siren_song_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_siren_song_badge" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_siren_song_badge" }
]
```

---

## 套装五十五：狼人诅咒（Werewolf Curse）饰品

> *"狼人不是怪物，它只是被月光揭穿了真面目。"*

---

### 55.5 狼人诅咒兽皮披风（Werewolf Curse Beast Cloak）

```gdscript
item_id = "acc_werewolf_curse_cloak"
display_name = "狼人诅咒兽皮披风"
description = "一件由狼人皮毛与魔法丝编织而成的披风，披风表面覆盖着粗糙的兽毛，每一根毛发都是活的——它们会自主竖立，感知周围的威胁。当月光照射时，披风会自动散发出银色的光芒，使穿戴者的轮廓变得模糊，如同一头真正的狼。\n\n这件披风是猎狼者在「狼人巢穴」中，从一只古老的狼人 alpha 身上取下的皮毛编织的。他说："这件披风让我能够理解狼人的本能——它们的愤怒，它们的忠诚，它们的孤独。但它也让我在满月时难以控制自己。"\n\n披风的效果是：夜间隐匿检定 +3（月光 camouflage），且在满月夜 AC +2（狼人之力）。当穿戴者 HP 低于 50% 时，披风会散发出野性气息——攻击检定 +1（兽性觉醒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "werewolf_curse_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_werewolf_curse_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_werewolf_curse_cloak" }
]
```

---

### 55.6 狼人诅咒月光之牙项链（Werewolf Curse Moon Fang Necklace）

```gdscript
item_id = "acc_werewolf_curse_necklace"
display_name = "狼人诅咒月光之牙项链"
description = "一条由狼牙与月光银编织而成的项链，吊坠是一颗狼人的犬齿——这颗牙齿在月光下会发出银色的光芒。牙齿内部有微型狼魂在咆哮，每当月圆之夜，咆哮声会变得更加响亮。据说这颗牙齿来自第一只狼人，它承载了所有狼人的诅咒和力量。\n\n这条项链是猎狼者在「月光祭坛」上，从狼人王的遗骸中取下的。他说："这颗牙齿承载了狼人的原始力量。它不是给凡人的礼物——它是给勇者的诅咒。"\n\n项链的效果是：夜间攻击检定 +1（月光之力），且满月夜攻击检定再 +2（满月狂化）。每日一次，可以释放「狼嚎」——30 尺半径内所有敌人须通过 DC15 智慧豁免，失败则 frightened 1 回合（狼嚎恐惧）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "werewolf_curse_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_werewolf_curse_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_werewolf_curse_necklace" }
]
```

---

### 55.7 狼人诅咒撕裂之戒（Werewolf Curse Rend Ring）

```gdscript
item_id = "acc_werewolf_curse_ring_1"
display_name = "狼人诅咒撕裂之戒"
description = "一枚由狼爪与月光金铸造的戒指，戒指表面刻有微型爪痕图案。当佩戴者愤怒时，戒指会释放出一股野性之力，使佩戴者的指甲变长变硬，如同狼爪。这种变形不是完全的——它只影响手指，但足以撕裂皮肉，粉碎骨骼。\n\n这枚戒指是猎狼者在「狼人战场」中，从一只狼人战士的爪子上取下的碎片铸造的。他说："这枚戒指让我能够借用狼人的撕裂之力。但每一次使用，我都感到自己的理智在消退。"\n\n戒指的效果是：徒手攻击和近战攻击附加 1D6 slashing（狼爪撕裂），且夜间暴击伤害 +1D6（月光暴击）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "werewolf_curse_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_werewolf_curse_ring_1" }
]
```

---

### 55.8 狼人诅咒野性之戒（Werewolf Curse Wild Ring）

```gdscript
item_id = "acc_werewolf_curse_ring_2"
display_name = "狼人诅咒野性之戒"
description = "一枚由野性石与月光银铸造的戒指，戒指表面刻有微型狼头图案。当佩戴者集中精神时，戒指会释放出一股野性之力，增强佩戴者的感官和反应速度。佩戴者可以闻到远处的气味，听到远处的声音，感受到远处的震动。但这种增强是有代价的——佩戴者会变得更加易怒，更加冲动。\n\n这枚戒指是猎狼者在「野性森林」中，从野性之神那里得到的。野性之神说："这枚戒指让你能够借用野兽的感官。但野兽的感官带来的不只是信息——还有本能的冲动。"\n\n戒指的效果是：嗅觉和听觉感知检定 +3（野兽感官），且先攻 +2（野性直觉）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "werewolf_curse_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "initiative_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_werewolf_curse_ring_2" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_werewolf_curse_ring_2" }
]
```

---

### 55.9 狼人诅咒月光石（Werewolf Curse Moon Stone）

```gdscript
item_id = "acc_werewolf_curse_trinket"
display_name = "狼人诅咒月光石"
description = "一块由月光凝结的水晶，水晶内部有一团银色的光芒——这是月光的精华。当满月之夜，水晶会变得异常明亮，散发出强大的月光之力。这种力量可以治愈伤口，也可以诱发变形。据说这块水晶来自月球本身，是月亮女神的眼泪凝结的。\n\n这块水晶是猎狼者在「月光祭坛」上，从月亮女神的雕像手中取下的。他说："这块水晶承载了月亮的意志。它既是治愈，也是诅咒；既是光明，也是黑暗。"\n\n水晶的效果是：每日一次，可以释放「月光变身」——夜间，获得「狼人形态」1 分钟：AC +1，攻击附加 1D6 slashing，移动力 +10，感知检定 +2。变身期间免疫 frightened，但每回合开始时须通过 DC14 智慧豁免，失败则攻击最近的生物（敌我不分）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "werewolf_curse_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_werewolf_curse_trinket" }
]
```

---

### 55.10 狼人诅咒徽章（Werewolf Curse Badge）

```gdscript
item_id = "acc_werewolf_curse_badge"
display_name = "狼人诅咒徽章"
description = "一枚由狼金与月光石铸造的徽章，徽章上刻着一只狼头和一个月亮——狼人的标志。徽章背面刻着一行小字："月光下，我们都是野兽。"这枚徽章是猎狼者公会的信物，拥有它意味着你已理解狼人的诅咒。\n\n徽章的效果是：10 尺内所有友方夜间攻击检定 +1（月光祝福）。且每日一次，可以释放「狼群召唤」——召唤 2 只幽灵狼（HP 20，AC 14，攻击 +5，1D8+3 piercing），持续 1 分钟。幽灵狼听从你的命令。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "werewolf_curse_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_werewolf_curse_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_werewolf_curse_badge" }
]
```

---

## 套装五十六：影舞者（Shadow Dancer）饰品

> *"影舞者不是在阴影中行走——阴影因她而存在。"*

---

### 56.5 影舞者暗影披风（Shadow Dancer Shadow Cloak）

```gdscript
item_id = "acc_shadow_dancer_cloak"
display_name = "影舞者暗影披风"
description = "一件由暗影丝与虚空布编织而成的披风，披风表面不断有微型暗影在流动和扭曲——不是装饰，而是真正的暗影被封印在了丝线中。当穿戴者移动时，披风会自动与周围的阴影融合，使穿戴者几乎不可见。在完全黑暗中，披风会让穿戴者完全消失，如同从未存在过。\n\n这件披风是影舞者在「暗影殿堂」中，从自己的影子上取下的碎片编织的。她说："这件披风让我成为了阴影的一部分。我可以穿越任何黑暗，隐藏在任何一个角落。但它也让我在阳光下感到不适。"\n\n披风的效果是：阴影中（非光照区域）完全隐形（如同 greater invisibility），且可以传送到 20 尺内任何阴影中（每日两次）。当穿戴者从阴影中发动攻击时，攻击检定有优势（暗影突袭）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "shadow_dancer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_shadow_dancer_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_dancer_cloak" }
]
```

---

### 56.6 影舞者影之泪项链（Shadow Dancer Shadow Tear Necklace）

```gdscript
item_id = "acc_shadow_dancer_necklace"
display_name = "影舞者影之泪项链"
description = "一条由暗影水晶与月光银编织而成的项链，吊坠是一颗影之泪——一颗由纯粹暗影凝结的水晶。水晶内部不断有微型暗影在舞动，如同无数个看不见的舞者在其中表演。据说这颗眼泪来自第一位影舞者，她在最后一次表演中流下了这颗永恒的眼泪。\n\n这条项链是影舞者在「暗影舞台」上，从自己的影子中取出的第一滴泪制成的。她说："这颗眼泪承载了我所有的表演——我的喜悦，我的悲伤，我的孤独。当你佩戴它时，你会感受到影舞者的真正情感。"\n\n项链的效果是：表演检定 +3，且每日一次，可以释放「暗影之舞」——自身进入「暗影形态」2 回合：完全免疫物理伤害，可以穿过墙壁和障碍物，且可以 bonus action 传送到 15 尺内任何阴影中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "shadow_dancer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_shadow_dancer_necklace" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_dancer_necklace" }
]
```

---

### 56.7 影舞者暗影之戒（Shadow Dancer Shadow Ring）

```gdscript
item_id = "acc_shadow_dancer_ring_1"
display_name = "影舞者暗影之戒"
description = "一枚由暗影石与虚空金铸造的戒指，戒指表面刻有微型暗影图案。当佩戴者集中精神时，戒指会释放出一股暗影之力，使佩戴者的影子独立行动。这个影子可以攻击敌人，也可以为佩戴者挡下攻击。但影子有自己的意志——有时它会拒绝执行命令，有时它会攻击错误的目标。\n\n这枚戒指是影舞者在「暗影深渊」中，从暗影之神那里得到的。暗影之神说："这枚戒指让你的影子获得了自由。但它也是独立的——它不会永远服从你。"\n\n戒指的效果是：每日一次，可以释放「影子攻击」——影子独立攻击 15 尺内一个敌人，造成 2D8 necrotic + 1D8 cold（影子之触），目标须通过 DC15 敏捷豁免，失败则 blinded 1 回合（被影子遮蔽）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "shadow_dancer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shadow_dancer_ring_1" }
]
```

---

### 56.8 影舞者沉默之戒（Shadow Dancer Silence Ring）

```gdscript
item_id = "acc_shadow_dancer_ring_2"
display_name = "影舞者沉默之戒"
description = "一枚由沉默石与暗影银铸造的戒指，戒指表面刻有微型静音图案。当佩戴者集中精神时，戒指会释放出一股沉默之力，使周围的声音完全消失。在沉默领域中，没有任何声音可以传播——说话、脚步、甚至心跳都会被吞噬。这种沉默是绝对的，也是可怕的。\n\n这枚戒指是影舞者在「沉默殿堂」中，从沉默之神那里得到的。沉默之神说："这枚戒指让你能够借用沉默的力量。但沉默不是空虚——它是另一种声音。学会倾听它。"\n\n戒指的效果是：隐匿检定 +2（无声移动），且每日一次，可以释放「沉默领域」——10 尺半径内所有声音消失 2 回合，期间区域内无法施法（需要语言成分），且所有生物的攻击检定 -1（失去声音反馈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "shadow_dancer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_dancer_ring_2" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_dancer_ring_2" }
]
```

---

### 56.9 影舞者暗影面具（Shadow Dancer Shadow Mask）

```gdscript
item_id = "acc_shadow_dancer_trinket"
display_name = "影舞者暗影面具"
description = "一张由暗影丝与魔法布制成的面具，面具表面没有任何特征——没有眼睛，没有嘴巴，没有鼻子。但当你凝视它时，你会看到你自己的倒影，但这个倒影在做着你没有做的事情：它在微笑，你在哭泣；它在跳舞，你在站立。这张面具是影舞者的真正面容——它没有自己的脸，它只反射他人的内心。\n\n这张面具是影舞者在「暗影舞台」上，从自己的脸上取下的。她说："这张面具让我能够变成任何人。但它也让我忘记了自己是谁。"\n\n面具的效果是：每日一次，可以释放「暗影分身」——创造一个完全相同的分身（1 HP，AC = 你的 AC），持续 2 回合。分身可以进行一次攻击（+5，1D8+3 necrotic）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "shadow_dancer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_shadow_dancer_trinket" }
]
```

---

### 56.10 影舞者徽章（Shadow Dancer Badge）

```gdscript
item_id = "acc_shadow_dancer_badge"
display_name = "影舞者徽章"
description = "一枚由暗影金与虚空石铸造的徽章，徽章上刻着一只舞动的影子——影舞者的标志。徽章背面刻着一行小字："在黑暗中起舞，在光明中消失。"这枚徽章是影舞者公会的信物，拥有它意味着你已掌握暗影的艺术。\n\n徽章的效果是：10 尺内所有友方在阴影中隐匿检定 +2（暗影庇护）。且每日一次，可以释放「暗影帷幕」——10 尺半径内所有友方获得「暗影形态」1 回合：完全免疫物理伤害，可以穿过障碍物。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "shadow_dancer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shadow_dancer_badge" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shadow_dancer_badge" }
]
```

---

## 套装五十七：水晶先知（Crystal Seer）饰品

> *"水晶不会说谎，它只是把真相折射成了你能理解的颜色。"*

---

### 57.5 水晶先知水晶披风（Crystal Seer Crystal Cloak）

```gdscript
item_id = "acc_crystal_seer_cloak"
display_name = "水晶先知水晶披风"
description = "一件由水晶丝与星光布编织而成的披风，披风表面不断有微型水晶在生长和碎裂——不是装饰，而是真正的水晶被封印在了丝线中。当穿戴者集中精神时，披风会折射周围的光线，形成彩虹般的光芒。这些光芒不仅仅是美丽的——它们可以揭示隐藏的事物，暴露幻象的真面目。\n\n这件披风是水晶先知在「水晶洞穴」中，从洞壁上生长出的第一块水晶编织的。她说："这件披风让我能够看到世界的真相——不是表面的真相，而是水晶背后的真相。"\n\n披风的效果是：可以感知 30 尺内的幻象和隐形生物（水晶之眼），且免疫 blinded（水晶折射）。当穿戴者被魔法攻击时，有 20% 概率将攻击反射回施法者（水晶反射）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "crystal_seer_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_crystal_seer_cloak" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_crystal_seer_cloak" }
]
```

---

### 57.6 水晶先知预知之眼项链（Crystal Seer Foresight Eye Necklace）

```gdscript
item_id = "acc_crystal_seer_necklace"
display_name = "水晶先知预知之眼项链"
description = "一条由水晶金与星光银编织而成的项链，吊坠是一颗预知之眼——一颗由纯粹预知能量凝结的水晶。水晶内部不断有未来的片段在闪烁：有的清晰，有的模糊，有的快速，有的缓慢。据说这颗眼睛可以看到未来三天内所有可能的未来，但它只会显示佩戴者需要看到的。\n\n这条项链是水晶先知在「预知之泉」边，从泉水中取出的第一块水晶制成的。她说："这颗眼睛让我能够预见未来。但未来不是固定的——每一个选择都会改变它。"\n\n项链的效果是：洞察检定 +3，且每日一次，可以「预见」——下一回合开始前，你可以看到所有敌人的 intended action（攻击目标、移动方向、施法选择等），并据此调整你的行动（时间预知）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "crystal_seer_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_crystal_seer_necklace" },
    { attribute_id = "history_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_crystal_seer_necklace" }
]
```

---

### 57.7 水晶先知折射之戒（Crystal Seer Refraction Ring）

```gdscript
item_id = "acc_crystal_seer_ring_1"
display_name = "水晶先知折射之戒"
description = "一枚由折射石与水晶金铸造的戒指，戒指表面刻有微型折射图案。当佩戴者集中精神时，戒指会折射周围的光线，使佩戴者的位置变得模糊——敌人无法确定佩戴者的真正位置，攻击常常会命中错误的位置。这种折射是物理的，不是魔法的，因此无法被 dispel。\n\n这枚戒指是水晶先知在「折射迷宫」中，从迷宫核心取出的水晶铸造的。她说："这枚戒指让我能够借用光的特性。光既是粒子，也是波——它既在这里，也在那里。"\n\n戒指的效果是：AC +1（位置模糊），且每日一次，可以释放「水晶折射」——指定一个攻击你的敌人，其该次攻击自动 miss（光线误导），且你可以立即 reaction 进行一次反击（1D8+3 force，水晶碎片）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "crystal_seer_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_crystal_seer_ring_1" }
]
```

---

### 57.8 水晶先知共鸣之戒（Crystal Seer Resonance Ring）

```gdscript
item_id = "acc_crystal_seer_ring_2"
display_name = "水晶先知共鸣之戒"
description = "一枚由共鸣石与水晶银铸造的戒指，戒指表面刻有微型共鸣图案。当佩戴者集中精神时，戒指会释放出共鸣波，与周围的魔法能量产生共振。这种共振可以增强佩戴者的法术，也可以破坏敌人的法术。但共振是不稳定的——有时它会增强太多，导致法术失控。\n\n这枚戒指是水晶先知在「共鸣洞穴」中，从洞穴核心取出的水晶铸造的。她说："这枚戒指让我能够与魔法共鸣。但共鸣是双向的——我可以增强魔法，也可以被魔法摧毁。"\n\n戒指的效果是：法术攻击检定 +1（魔法共鸣），且每日一次，可以释放「共鸣干扰」——指定一个正在施法的敌人，其法术被干扰（如同 counterspell，DC15 奥术检定）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "crystal_seer_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_crystal_seer_ring_2" }
]
```

---

### 57.9 水晶先知水晶球（Crystal Seer Crystal Ball）

```gdscript
item_id = "acc_crystal_seer_trinket"
display_name = "水晶先知水晶球"
description = "一颗由纯粹预知水晶制成的球体，球体内部不断有未来的片段在旋转和变化。这颗水晶球不是普通的占卜工具——它是活的。它会根据佩戴者的问题，显示出最可能的答案。但水晶球有自己的脾气——有时它会拒绝回答，有时它会显示出可怕的真相。\n\n这颗水晶球是水晶先知在「水晶神殿」中，从预知之神那里得到的。预知之神说："这颗水晶球可以看到所有可能的未来。但它只会告诉你它愿意告诉你的。不要强迫它。"\n\n水晶球的效果是：每日一次，可以释放「水晶风暴」——对 20 尺半径内所有敌人释放水晶碎片，造成 4D8 force + 2D8 radiant（混合），须通过 DC16 敏捷豁免，失败则 full damage 并 blinded 1 回合（被水晶碎片刺伤眼睛）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "crystal_seer_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_crystal_seer_trinket" }
]
```

---

### 57.10 水晶先知徽章（Crystal Seer Badge）

```gdscript
item_id = "acc_crystal_seer_badge"
display_name = "水晶先知徽章"
description = "一枚由水晶金与星光石铸造的徽章，徽章上刻着一只眼睛和一颗水晶——水晶先知的标志。徽章背面刻着一行小字："真相在水晶中，但水晶不会主动展示。"这枚徽章是水晶先知公会的信物，拥有它意味着你已被认可为预知者。\n\n徽章的效果是：10 尺内所有友方感知检定 +1（水晶洞察）。且每日一次，可以释放「预知护盾」——10 尺内所有友方获得「预知」1 回合：对下一次攻击自动闪避（如同 shield spell 的效果，但不消耗反应）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "crystal_seer_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_crystal_seer_badge" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_crystal_seer_badge" }
]
```

---

## 套装五十八：霜巨人（Frost Giant）饰品

> *"霜巨人不是冷酷，它只是习惯了温度低到连情感都要结冰。"*

---

### 58.5 霜巨人冰霜披风（Frost Giant Frost Cloak）

```gdscript
item_id = "acc_frost_giant_cloak"
display_name = "霜巨人冰霜披风"
description = "一件由冰霜丝与寒风布编织而成的披风，披风表面不断有微型雪花在飘落和融化——不是装饰，而是真正的冰霜被封印在了丝线中。当穿戴者行走时，脚下会留下一层薄冰；当穿戴者呼吸时，口鼻会喷出白色寒气。在炎热环境中，披风会自动降低周围的温度，使穿戴者感到舒适。\n\n这件披风是霜巨人在「冰霜堡垒」中，从永冻之壁上刮下的冰屑编织的。他说："这件披风让我能够将冰霜的力量带到任何地方。但它也让我在温暖的环境中感到不适。"\n\n披风的效果是：cold 抗性 +20，且在寒冷环境中 AC +2（冰霜护甲）。当穿戴者被近战攻击时，攻击者受到 1D6 cold（冰霜反噬），且移动力 -5 1 回合（脚下结冰）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "frost_giant_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_cold", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_frost_giant_cloak" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_giant_cloak" }
]
```

---

### 58.6 霜巨人永冻之心项链（Frost Giant Frozen Heart Necklace）

```gdscript
item_id = "acc_frost_giant_necklace"
display_name = "霜巨人永冻之心项链"
description = "一条由永冻金与寒冰水晶编织而成的项链，吊坠是一颗永冻之心——一颗由纯粹冰霜凝结的心脏。心脏内部有一团微弱的蓝光，那是冰霜的灵魂。据说这颗心脏来自一位死去的霜巨人，他的心脏在死后继续跳动，永远不会停止。\n\n这条项链是霜巨人在「永冻墓」中，从死去的霜巨人王胸口取出的。他说："这颗心脏承载了我的祖先的力量。它让我能够承受最寒冷的环境，也让我在最绝望的时刻保持冷静。"\n\n项链的效果是：体质豁免 +2（冰霜坚韧），且免疫寒冷环境效果（永冻之躯）。每日一次，可以释放「冰霜之息」——15 尺锥形 3D8 cold，目标须通过 DC15 体质豁免，失败则移动力减半 2 回合（被冰冻）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "frost_giant_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_giant_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_giant_necklace" }
]
```

---

### 58.7 霜巨人碎冰之戒（Frost Giant Shatter Ring）

```gdscript
item_id = "acc_frost_giant_ring_1"
display_name = "霜巨人碎冰之戒"
description = "一枚由碎冰石与永冻金铸造的戒指，戒指表面刻有微型冰裂纹路。当佩戴者集中精神时，戒指会释放出一股碎冰之力，使目标周围的空气瞬间冻结，然后爆裂。这种爆裂可以伤害敌人，也可以破坏障碍物。但碎冰是不可控的——有时它会伤害佩戴者自己。\n\n这枚戒指是霜巨人在「碎冰峡谷」中，从峡谷核心取出的冰块铸造的。他说："这枚戒指让我能够借用碎冰的力量。但碎冰没有朋友——它会碎裂一切。"\n\n戒指的效果是：所有攻击附加 1D4 cold（冰霜），且每日一次，可以释放「碎冰爆裂」——指定一个 15 尺内的敌人，其受到 3D10 cold + 2D10 piercing（冰爆裂），须通过 DC16 敏捷豁免，失败则 full damage 并 prone（被冰块击倒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "frost_giant_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_giant_ring_1" }
]
```

---

### 58.8 霜巨人重力之戒（Frost Giant Gravity Ring）

```gdscript
item_id = "acc_frost_giant_ring_2"
display_name = "霜巨人重力之戒"
description = "一枚由重力石与永冻银铸造的戒指，戒指表面刻有微型重力图案。当佩戴者集中精神时，戒指会释放出一股重力之力，使佩戴者的身体变得更加沉重。这种沉重不是负担——它是力量。佩戴者可以利用这种重力，发出更强大的攻击，承受更强大的冲击。\n\n这枚戒指是霜巨人在「重力冰川」中，从冰川核心取出的物质铸造的。他说："这枚戒指让我能够借用重力的力量。但重力是公平的——它会让一切都变得更重。"\n\n戒指的效果是：力量检定 +2（重力加成），且站立不动时攻击伤害 +1D6（重力一击）。但移动力 -5（重力减速）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "frost_giant_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_giant_ring_2" }
]
```

---

### 58.9 霜巨人冰核（Frost Giant Ice Core）

```gdscript
item_id = "acc_frost_giant_trinket"
display_name = "霜巨人冰核"
description = "一块由纯粹冰霜凝结的核心，核心内部有一团永不熄灭的寒冰火焰——这不是普通的火焰，它是「冷焰」，一种只存在于绝对零度附近的奇异现象。这块核心的温度低于绝对零度，触摸它的人会感到一种超越寒冷的空虚——不是冷，而是「不存在」的感觉。\n\n这块核心是霜巨人在「冰核深渊」中，从深渊底部取出的。他说："这块核心来自世界的最深处。它让我能够操控冰霜的终极力量。但它也让我永远无法感受到温暖。"\n\n核心的效果是：每日一次，可以释放「绝对零度」——20 尺半径内所有敌人受到 5D10 cold，须通过 DC17 体质豁免，失败则 full damage 并 frozen 1 回合（无法行动），成功则 half damage 且移动力减半 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "frost_giant_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_frost_giant_trinket" }
]
```

---

### 58.10 霜巨人徽章（Frost Giant Badge）

```gdscript
item_id = "acc_frost_giant_badge"
display_name = "霜巨人徽章"
description = "一枚由永冻金与冰霜石铸造的徽章，徽章上刻着一座冰山和一把斧头——霜巨人的标志。徽章背面刻着一行小字："寒冷是力量，孤独是代价。"这枚徽章是霜巨人氏族的信物，拥有它意味着你已被霜巨人认可。\n\n徽章的效果是：10 尺内所有友方获得 +5 cold 抗性（冰霜庇护）。且每日一次，可以释放「冰霜护甲」——10 尺内所有友方获得 +2 AC（冰甲），且近战攻击者受到 1D6 cold（冰霜反噬），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "frost_giant_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_giant_badge" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_giant_badge" }
]
```

---

## 套装五十九：木乃伊诅咒（Mummy's Curse）饰品

> *"木乃伊的裹尸布不是绷带，它是'封印'——封印着不该被遗忘的愤怒。"*

---

### 59.5 木乃伊诅咒裹尸布披风（Mummy's Curse Shroud Cloak）

```gdscript
item_id = "acc_mummy_curse_cloak"
display_name = "木乃伊诅咒裹尸布披风"
description = "一件由远古裹尸布与诅咒丝编织而成的披风，披风表面覆盖着微型诅咒符文，每一个符文都代表着一个被诅咒的灵魂。当穿戴者受到威胁时，符文会发出微弱的绿光，释放出诅咒之力。这些诅咒不是针对敌人的——它们是针对命运的，是对死亡的反抗。\n\n这件披风是盗墓者在「法老墓」中，从第一具木乃伊身上取下的裹尸布编织的。他说："这件披风让我能够理解木乃伊的愤怒——它们不是怪物，它们是受害者。它们的诅咒不是恶意，而是绝望。"\n\n披风的效果是：necrotic 抗性 +15，且免疫疾病和诅咒效果（诅咒免疫）。当穿戴者被击杀时，会释放「死亡诅咒」——击杀者受到 3D10 necrotic（DC16 体质豁免 half），且 max HP 降低 10 直至长休（木乃伊的复仇）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "mummy_curse_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_necrotic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_mummy_curse_cloak" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mummy_curse_cloak" }
]
```

---

### 59.6 木乃伊诅咒诅咒之眼项链（Mummy's Curse Curse Eye Necklace）

```gdscript
item_id = "acc_mummy_curse_necklace"
display_name = "木乃伊诅咒诅咒之眼项链"
description = "一条由黑曜石与诅咒金编织而成的项链，吊坠是一只诅咒之眼——一只从木乃伊身上取下的真实眼睛。这只眼睛不会眨眼，不会转动，只是永恒地凝视着前方。凝视这只眼睛的人会感到一种难以言喻的恐惧——不是来自外部，而是来自自己内心深处对死亡的恐惧。\n\n这条项链是盗墓者在「诅咒神殿」中，从木乃伊王的脸上取下的。他说："这只眼睛承载了木乃伊王的诅咒。它不是给活人的礼物——它是给死者的遗嘱。"\n\n项链的效果是：宗教检定 +3，且可以感知 30 尺内的亡灵和诅咒物品（诅咒之眼）。每日一次，可以释放「诅咒凝视」——指定一个 15 尺内的敌人，其须通过 DC16 智慧豁免，失败则 cursed 2 回合（所有检定 -2，且每回合开始时受到 1D6 necrotic）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "mummy_curse_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_mummy_curse_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mummy_curse_necklace" }
]
```

---

### 59.7 木乃伊诅咒腐朽之戒（Mummy's Curse Decay Ring）

```gdscript
item_id = "acc_mummy_curse_ring_1"
display_name = "木乃伊诅咒腐朽之戒"
description = "一枚由腐朽石与诅咒金铸造的戒指，戒指表面刻有微型腐朽图案。当佩戴者集中精神时，戒指会释放出一股腐朽之力，加速目标的 decay——金属会生锈，木头会腐烂，石头会风化，生物会衰老。这种腐朽是不可逆的，即使魔法也无法完全修复。\n\n这枚戒指是盗墓者在「腐朽深渊」中，从腐朽之神那里得到的。腐朽之神说："这枚戒指让你能够借用我的力量。但腐朽最终会吞噬一切，包括你自己。"\n\n戒指的效果是：所有攻击附加 1D4 necrotic（腐朽侵蚀），且每日一次，可以释放「腐朽之触」——指定一个 5 尺内的敌人，其受到 2D8 necrotic 且 AC -1（护甲腐朽），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "mummy_curse_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mummy_curse_ring_1" }
]
```

---

### 59.8 木乃伊诅咒绷带之戒（Mummy's Curse Bandage Ring）

```gdscript
item_id = "acc_mummy_curse_ring_2"
display_name = "木乃伊诅咒绷带之戒"
description = "一枚由绷带丝与诅咒银铸造的戒指，戒指表面缠绕着微型绷带——这些绷带是活的，会自主缠绕和松开。当佩戴者受伤时，绷带会自动缠绕伤口，止住流血；当佩戴者中毒时，绷带会吸收毒素，防止扩散。但这些绷带不会治愈伤口——它们只是封印伤口，让佩戴者能够继续战斗。\n\n这枚戒指是盗墓者在「绷带墓穴」中，从第一具木乃伊的绷带上取下的碎片铸造的。他说："这枚戒指让我能够借用木乃伊的不死之力。但它也让我变得越来越像木乃伊——干燥、冰冷、没有感觉。"\n\n戒指的效果是：免疫 bleed（绷带止血），且 HP 降至 25% 以下时，AC +2（绷带缠绕），但移动力 -5（绷带束缚）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "mummy_curse_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mummy_curse_ring_2" }
]
```

---

### 59.9 木乃伊诅咒法老之心（Mummy's Curse Pharaoh Heart）

```gdscript
item_id = "acc_mummy_curse_trinket"
display_name = "木乃伊诅咒法老之心"
description = "一颗由黄金与防腐剂制成的微型心脏，心脏内部有一团微弱的绿光——那是法老的灵魂。这颗心脏不是给活人的——它是给死者的。但当佩戴者拥有它时，法老的灵魂会保护佩戴者，赋予他不死的力量。据说这颗心脏来自第一位法老，他用自己的灵魂封印了永恒的诅咒。\n\n这颗心脏是盗墓者在「法老墓」中，从法老的胸腔中取出的。他说："这颗心脏承载了法老的永恒。它不是给凡人的礼物——它是给勇者的诅咒。"\n\n心脏的效果是：每日一次，可以释放「死亡复苏」——当 HP 降至 0 时，自动触发：恢复至 max HP 的 25%，且对 10 尺内所有敌人释放「死亡之息」——3D10 necrotic（DC16 体质豁免 half）。触发后 24 小时内无法再次使用。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "mummy_curse_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_mummy_curse_trinket" }
]
```

---

### 59.10 木乃伊诅咒徽章（Mummy's Curse Badge）

```gdscript
item_id = "acc_mummy_curse_badge"
display_name = "木乃伊诅咒徽章"
description = "一枚由诅咒金与黑曜石铸造的徽章，徽章上刻着一只眼睛和一个棺材——木乃伊的标志。徽章背面刻着一行小字："死亡不是终点，遗忘才是。"这枚徽章是木乃伊守护者的信物，拥有它意味着你已理解死亡的真相。\n\n徽章的效果是：10 尺内所有友方获得 +5 necrotic 抗性（死亡庇护）。且每日一次，可以释放「死亡光环」——10 尺内所有敌人每回合开始时受到 1D6 necrotic（死亡侵蚀），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "mummy_curse_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mummy_curse_badge" },
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mummy_curse_badge" }
]
```

---

## 套装六十：龙骑士（Dragon Rider）饰品

> *"龙骑士不是驯服了龙，他只是赢得了龙的尊重——而尊重是龙唯一能给予的东西。"*

---

### 60.5 龙骑士龙鳞披风（Dragon Rider Scale Cloak）

```gdscript
item_id = "acc_dragon_rider_cloak"
display_name = "龙骑士龙鳞披风"
description = "一件由龙鳞与魔法丝编织而成的披风，披风表面覆盖着微型龙鳞，每一片鳞片都来自不同的龙——红龙的火焰鳞、蓝龙的闪电鳞、白龙的冰霜鳞、黑龙的酸液鳞、绿龙的毒气鳞。这些鳞片不仅提供保护，还赋予穿戴者操控元素的力量。\n\n这件披风是龙骑士在「龙巢」中，从自己的龙伙伴身上取下的蜕皮编织的。他说："这件披风让我能够与我的龙伙伴分享力量。每一片鳞片都是它的礼物，也是我们的羁绊。"\n\n披风的效果是：选择一种龙息类型（fire/cold/lightning/acid/poison），获得该抗性 +15（龙鳞护盾）。且当穿戴者骑乘时，AC +2（龙骑协同）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dragon_rider_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_dragon_rider_cloak" },
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_rider_cloak" }
]
```

---

### 60.6 龙骑士龙之牙项链（Dragon Rider Dragon Fang Necklace）

```gdscript
item_id = "acc_dragon_rider_necklace"
display_name = "龙骑士龙之牙项链"
description = "一条由龙金与魔法银编织而成的项链，吊坠是一颗龙之牙——一颗从龙骑士的龙伙伴身上取下的乳牙。这颗牙齿在月光下会发出对应龙种颜色的光芒：红牙发红光，蓝牙发蓝光，白牙发白光。牙齿内部有微型龙魂在咆哮，每当龙骑士遇到危险时，咆哮声会变得更加响亮。\n\n这条项链是龙骑士在「龙族仪式」上，从龙伙伴那里得到的礼物。龙说："这颗牙齿是我给你的信任。当你需要时，我的力量会透过它保护你。"\n\n项链的效果是：驯兽检定 +3（龙族亲和），且每日一次，可以释放「龙息共鸣」——释放一道微型龙息（30 尺锥形 4D8 伤害，类型与抗性选择相同，DC16 敏捷豁免 half）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dragon_rider_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_dragon_rider_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_rider_necklace" }
]
```

---

### 60.7 龙骑士龙翼之戒（Dragon Rider Wing Ring）

```gdscript
item_id = "acc_dragon_rider_ring_1"
display_name = "龙骑士龙翼之戒"
description = "一枚由龙翼骨与飞行金铸造的戒指，戒指表面刻有微型龙翼图案。当佩戴者集中精神时，戒指会释放出一股飞行之力，使佩戴者背后浮现出微型龙翼。这些龙翼虽然不能支持长时间飞行，但足以让佩戴者在空中滑翔，或者从高处安全降落。\n\n这枚戒指是龙骑士在「飞行试炼」中，从龙伙伴的翅膀上取下的碎片铸造的。他说："这枚戒指让我能够分享龙的天空。虽然我不能像它一样自由翱翔，但我至少可以触摸云朵。"\n\n戒指的效果是：免疫 falling damage（龙翼滑翔），且每日一次，可以获得 30 尺飞行速度 1 分钟（龙翼展开）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dragon_rider_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dragon_rider_ring_1" }
]
```

---

### 60.8 龙骑士龙血之戒（Dragon Rider Blood Ring）

```gdscript
item_id = "acc_dragon_rider_ring_2"
display_name = "龙骑士龙血之戒"
description = "一枚由龙血石与龙金铸造的戒指，戒指内部有一滴微型龙血——这滴血来自龙骑士的龙伙伴，在它们结成契约时交换的血液。这滴血永远不会干涸，永远不会变质，它在戒指内部永恒地流动，为佩戴者提供龙的力量。\n\n这枚戒指是龙骑士在「血契仪式」上，与龙伙伴交换血液时凝结的。他说："这枚戒指承载了我们的血契。当我受伤时，它的血会治愈我；当它受伤时，我的血会治愈它。我们是永恒的伙伴。"\n\n戒指的效果是：max HP +15（龙血强化），且当 HP 低于 50% 时，攻击检定 +2（龙血狂怒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dragon_rider_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_dragon_rider_ring_2" }
]
```

---

### 60.9 龙骑士龙蛋（Dragon Rider Dragon Egg）

```gdscript
item_id = "acc_dragon_rider_trinket"
display_name = "龙骑士龙蛋"
description = "一颗微型的龙蛋，蛋壳由对应龙种的元素结晶而成：红龙蛋是火焰水晶，蓝龙蛋是闪电水晶，白龙蛋是冰霜水晶。蛋壳内部有一团微弱的元素光芒——这是龙的胚胎，正在沉睡。这颗蛋不会孵化——它永远处于「即将孵化」的状态。但当佩戴者需要时，可以借用蛋中的元素力量。\n\n这颗蛋是龙骑士在「龙巢」中，从龙伙伴的巢里取出的。他说："这颗蛋包含了无限的可能性。它既是死亡，也是重生；既是终结，也是开始。"\n\n蛋的效果是：每日一次，可以释放「龙蛋爆发」——蛋壳裂开，释放出强大的元素能量：20 尺半径 5D10 伤害（类型与抗性选择相同），须通过 DC17 敏捷豁免，失败则 full damage 并 prone，成功则 half damage。释放后蛋壳自动复原。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dragon_rider_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_dragon_rider_trinket" }
]
```

---

### 60.10 龙骑士徽章（Dragon Rider Badge）

```gdscript
item_id = "acc_dragon_rider_badge"
display_name = "龙骑士徽章"
description = "一枚由龙金与元素石铸造的徽章，徽章上刻着一只龙和一个骑士——龙骑士的标志。徽章背面刻着一行小字："天空不是极限，它只是开始。"这枚徽章是龙骑士公会的信物，拥有它意味着你已与龙结成契约。\n\n徽章的效果是：10 尺内所有友方获得 +5 选择的元素抗性（龙鳞庇护）。且每日一次，可以释放「龙骑召唤」——召唤龙伙伴的幻影进行攻击：指定一个可见敌人，龙幻影俯冲攻击，造成 4D10 + 2D10（类型与抗性选择相同），目标须通过 DC17 敏捷豁免，失败则 full damage 并 prone（被龙翼击倒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dragon_rider_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_rider_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dragon_rider_badge" }
]
```

---

*套装 51–60 饰品部分完结 · 共 60 件饰品装备*

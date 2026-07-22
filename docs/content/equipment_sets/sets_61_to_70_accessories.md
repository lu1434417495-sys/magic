# 传奇装备套装饰品设计文档（套装 61–70）

> 10 套传奇装备套装的饰品部分，共 60 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装六十一：血肉编织者（Flesh Weaver）饰品

> *"血肉编织者不创造生命，他只是重新排列了死亡的顺序。"*

---

### 61.5 血肉编织者血肉披风（Flesh Weaver Flesh Cloak）

```gdscript
item_id = "acc_flesh_weaver_cloak"
display_name = "血肉编织者血肉披风"
description = "一件由活肉与魔法丝编织而成的披风，披风表面不断有微型肌肉在蠕动和收缩——不是装饰，而是真正的活组织被封印在了丝线中。这些组织来自不同的生物：人类、兽人、精灵、甚至是龙。每一片组织都保留了原主人的部分记忆，穿戴者可以通过触摸它们，感受到原主人的情感。\n\n这件披风是血肉编织者在「血肉工坊」中，从自己的作品上取下的组织编织的。他说："这件披风让我能够与我的材料沟通。每一片血肉都有它的故事，我只是帮助它们继续存在。"\n\n披风的效果是：necrotic 抗性 +10，且免疫疾病（活肉免疫）。当穿戴者受到伤害时，披风会自动修复伤口——每回合开始时恢复 1D6 HP（血肉再生），但仅在 HP 低于 50% 时生效。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "flesh_weaver_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_flesh_weaver_cloak" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_flesh_weaver_cloak" }
]
```

---

### 61.6 血肉编织者心脏项链（Flesh Weaver Heart Necklace）

```gdscript
item_id = "acc_flesh_weaver_necklace"
display_name = "血肉编织者心脏项链"
description = "一条由血管金与血肉丝编织而成的项链，吊坠是一颗微型心脏——一颗由纯粹生命力凝结的心脏。心脏在吊坠中永恒地跳动，发出微弱的脉搏声。据说这颗心脏来自一位自愿捐赠的圣人，他的生命力如此强大，即使死后心脏仍在跳动。\n\n这条项链是血肉编织者在「心脏神殿」中，从圣人的遗体上取下的。他说："这颗心脏承载了圣人的慈悲。它让我能够理解生命的价值，也让我能够创造新的生命。"\n\n项链的效果是：medicine 检定 +3，且每日一次，可以释放「血肉缝合」——自身或 5 尺内一个友方恢复 3D8 HP，并移除一个非 legendary bleed 或 poison 效果（血肉治愈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "flesh_weaver_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_flesh_weaver_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_flesh_weaver_necklace" }
]
```

---

### 61.7 血肉编织者缝合之戒（Flesh Weaver Suture Ring）

```gdscript
item_id = "acc_flesh_weaver_ring_1"
display_name = "血肉编织者缝合之戒"
description = "一枚由骨针与血肉金铸造的戒指，戒指表面刻有微型缝合图案。当佩戴者集中精神时，戒指会释放出一股缝合之力，将伤口瞬间愈合。这种愈合不是魔法——它是物理的缝合，将撕裂的组织重新连接。但缝合是有代价的：每次使用，佩戴者会感到一阵剧痛，仿佛自己的神经也被缝合了。\n\n这枚戒指是血肉编织者在「缝合室」中，从自己的骨针上取下的碎片铸造的。他说："这枚戒指让我能够在战斗中快速缝合伤口。但它也让我变得越来越像我的作品——缝合的、不完整的、痛苦的。"\n\n戒指的效果是：每日两次，可以 bonus action 缝合伤口——恢复 2D8 HP（快速缝合），但下一回合攻击检定 -1（缝合疼痛）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "flesh_weaver_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flesh_weaver_ring_1" }
]
```

---

### 61.8 血肉编织者寄生之戒（Flesh Weaver Parasite Ring）

```gdscript
item_id = "acc_flesh_weaver_ring_2"
display_name = "血肉编织者寄生之戒"
description = "一枚由寄生石与血肉银铸造的戒指，戒指内部有一只微型寄生虫——这只寄生虫是活的，它会在佩戴者的手指上钻一个小孔，进入血液循环。寄生虫不会伤害佩戴者——相反，它会帮助佩戴者：修复损伤、清除毒素、甚至增强力量。但寄生虫需要营养——它会从佩戴者身上吸取少量血液。\n\n这枚戒指是血肉编织者在「寄生深渊」中，从第一只共生寄生虫身上取下的核心铸造的。他说："这枚戒指让我能够与寄生虫共生。它不是我的敌人——它是我的伙伴。"\n\n戒指的效果是：免疫 poison 和 disease（寄生虫清除），且每回合开始时恢复 1 HP（寄生治疗）。但 max HP -5（寄生消耗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "flesh_weaver_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "max_hp", mode = "flat", value = -5, source_type = "equipment", source_id = "acc_flesh_weaver_ring_2" }
]
```

---

### 61.9 血肉编织者血肉傀儡（Flesh Weaver Flesh Puppet）

```gdscript
item_id = "acc_flesh_weaver_trinket"
display_name = "血肉编织者血肉傀儡"
description = "一个由各种生物的血肉拼接而成的微型傀儡，傀儡的四肢来自不同的生物：人类的手臂、兽人的腿、精灵的耳朵、龙尾巴。傀儡没有自己的意识——它只是佩戴者的延伸，会模仿佩戴者的动作。但当佩戴者失去意识时，傀儡会自主行动，保护佩戴者。\n\n这个傀儡是血肉编织者在「血肉工坊」中，从自己的第一个作品上取下的部分制成的。他说："这个傀儡是我的第一个孩子。虽然它不完整，但它会永远保护我。"\n\n傀儡的效果是：每日一次，可以释放「傀儡替身」——当受到致命攻击时，傀儡自动替佩戴者承受伤害（视为一次自动成功的 counter，完全阻挡该次伤害），但傀儡被摧毁（24 小时后自动修复）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "flesh_weaver_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_flesh_weaver_trinket" }
]
```

---

### 61.10 血肉编织者徽章（Flesh Weaver Badge）

```gdscript
item_id = "acc_flesh_weaver_badge"
display_name = "血肉编织者徽章"
description = "一枚由血肉金与骨石铸造的徽章，徽章上刻着一把刀和一颗心——血肉编织者的标志。徽章背面刻着一行小字："生命是材料，死亡是工具。"这枚徽章是血肉编织者公会的信物，拥有它意味着你已理解生命的可塑性。\n\n徽章的效果是：10 尺内所有友方每回合开始时恢复 1D4 HP（血肉光环）。且每日一次，可以释放「血肉链接」——链接 10 尺内一个友方，当该友方受到伤害时，50% 伤害转移给你（血肉分担，持续 2 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "flesh_weaver_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flesh_weaver_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flesh_weaver_badge" }
]
```

---

## 套装六十二：骸骨领主（Bone Lord）饰品

> *"骸骨领主不需要盔甲——他穿着亡者的骨头，而亡者从不需要保护。"*

---

### 62.5 骸骨领主骨翼披风（Bone Lord Bone Wing Cloak）

```gdscript
item_id = "acc_bone_lord_cloak"
display_name = "骸骨领主骨翼披风"
description = "一件由骨骼与魔法丝编织而成的披风，披风表面覆盖着微型骨片，每一片骨片都来自不同的生物：人类、兽人、龙、甚至是神。当穿戴者行走时，骨片会相互碰撞，发出咔哒咔哒的声音。在完全黑暗中，这种声音会变得更加响亮，如同无数亡灵在跟随。\n\n这件披风是骸骨领主在「骨墓」中，从自己的收藏品上取下的骨片编织的。他说："这件披风让我能够与我的亡灵军团共鸣。每一片骨头都是一位战士，它们会永远跟随我。"\n\n披风的效果是：necrotic 抗性 +15，且免疫恐惧（亡灵意志）。当穿戴者被击杀时，会释放「骨爆」——10 尺半径 3D10 necrotic（DC16 体质豁免 half），且召唤 1D4 只骷髅战士（HP 15，AC 13，攻击 +4，1D6+2 piercing）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "bone_lord_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_necrotic", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_bone_lord_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bone_lord_cloak" }
]
```

---

### 62.6 骸骨领主骷髅王冠项链（Bone Lord Skull Crown Necklace）

```gdscript
item_id = "acc_bone_lord_necklace"
display_name = "骸骨领主骷髅王冠项链"
description = "一条由脊椎骨与冥金编织而成的项链，吊坠是一顶微型骷髅王冠——这顶王冠由一百个微型骷髅头组成，每一个骷髅头都代表一位被骸骨领主征服的敌人。王冠内部有一团微弱的绿光，那是亡灵的灵魂之火。据说这顶王冠来自第一位亡灵之王。\n\n这条项链是骸骨领主在「王座厅」中，从亡灵之王的遗骸上取下的。他说："这顶王冠承载了一百位王者的灵魂。它让我能够命令所有亡灵，也让我能够感受到它们的痛苦。"\n\n项链的效果是：宗教检定 +3，且可以指挥 30 尺内的低级亡灵（骷髅、僵尸），它们不会主动攻击你，且可以用 bonus action 命令一个亡灵行动（骷髅王权）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "bone_lord_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_bone_lord_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bone_lord_necklace" }
]
```

---

### 62.7 骸骨领主骨爪之戒（Bone Lord Bone Claw Ring）

```gdscript
item_id = "acc_bone_lord_ring_1"
display_name = "骸骨领主骨爪之戒"
description = "一枚由骨爪与冥金铸造的戒指，戒指表面刻有微型骨爪图案。当佩戴者集中精神时，戒指会释放出一股骨之力，使佩戴者的手指骨化，变成锋利的骨爪。这些骨爪可以撕裂皮肉，粉碎骨骼，甚至可以穿透盔甲。但骨化是有代价的——每次使用，佩戴者会感到一阵剧痛，仿佛自己的手指正在被撕裂。\n\n这枚戒指是骸骨领主在「骨爪深渊」中，从骨爪恶魔身上取下的碎片铸造的。他说："这枚戒指让我能够借用骨爪的力量。但它也让我变得越来越像骷髅——冰冷、坚硬、没有感觉。"\n\n戒指的效果是：徒手攻击变为 1D8 slashing（骨爪），且每日一次，可以释放「骨爪撕裂」——对 5 尺内一个敌人造成 3D8 slashing + 2D8 necrotic（骨爪粉碎），目标须通过 DC16 敏捷豁免，失败则 full damage 并 bleed 1D6/回合（持续 3 回合）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "bone_lord_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_bone_lord_ring_1" }
]
```

---

### 62.8 骸骨领主亡灵之戒（Bone Lord Undead Ring）

```gdscript
item_id = "acc_bone_lord_ring_2"
display_name = "骸骨领主亡灵之戒"
description = "一枚由亡灵石与冥银铸造的戒指，戒指内部有一团微弱的绿光——那是一个微型亡灵的灵魂。这个灵魂不会说话，不会思考，只是永恒地燃烧。但它会为佩戴者提供亡灵的力量：不死、不眠、不饿。这种力量是有代价的——佩戴者会逐渐失去对生命的感觉，变得越来越像亡灵。\n\n这枚戒指是骸骨领主在「亡灵深渊」中，从第一个亡灵身上取下的灵魂铸造的。他说："这枚戒指让我能够借用亡灵的不死之力。但它也让我越来越难以感受到生命的温暖。"\n\n戒指的效果是：免疫 sleep 和 exhaustion（亡灵之躯），且不需要食物和水（亡灵代谢）。但魅力检定 -1（亡灵气息）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "bone_lord_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_bone_lord_ring_2" }
]
```

---

### 62.9 骸骨领主死灵之书（Bone Lord Necronomicon）

```gdscript
item_id = "acc_bone_lord_trinket"
display_name = "骸骨领主死灵之书"
description = "一本由人皮与骨粉制成的微型书籍，书页上写满了亡灵咒语。这本书不是给活人看的——它是给死者的遗嘱。每一页都记载着一位亡灵的名字、生平、和死因。当佩戴者需要时，可以召唤书中记载的亡灵，让它们为自己战斗。但召唤是有代价的——每召唤一个亡灵，佩戴者会失去一部分生命力。\n\n这本书是骸骨领主在「死灵图书馆」中，从第一任死灵法师的遗骸上取下的。他说："这本书承载了一千位亡灵的记忆。它让我能够召唤它们，也让我能够理解死亡的真谛。"\n\n书的效果是：每日一次，可以释放「亡灵召唤」——召唤 1D4+1 只骷髅战士（HP 15，AC 13，攻击 +4，1D6+2 piercing），持续 1 分钟。可以用 bonus action 指挥所有亡灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "bone_lord_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_bone_lord_trinket" }
]
```

---

### 62.10 骸骨领主徽章（Bone Lord Badge）

```gdscript
item_id = "acc_bone_lord_badge"
display_name = "骸骨领主徽章"
description = "一枚由冥金与骨石铸造的徽章，徽章上刻着一把镰刀和一个骷髅头——骸骨领主的标志。徽章背面刻着一行小字："死亡不是终点，只是开始。"这枚徽章是亡灵公会的信物，拥有它意味着你已被死亡认可。\n\n徽章的效果是：10 尺内所有友方亡灵（骷髅、僵尸等）攻击检定 +2（亡灵统帅）。且每日一次，可以释放「死亡光环」——10 尺内所有敌人每回合开始时受到 1D6 necrotic（死亡侵蚀），且 10 尺内所有友方亡灵恢复 1D6 HP（亡灵治愈），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "bone_lord_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_bone_lord_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_bone_lord_badge" }
]
```

---

*套装 61–62 饰品部分（12/60 件）*

## 套装六十三：荆棘女王（Thorn Queen）饰品

> *"玫瑰之所以美丽，是因为你知道触碰它会流血。"*

---

### 63.5 荆棘女王荆棘披风（Thorn Queen Thorn Cloak）

```gdscript
item_id = "acc_thorn_queen_cloak"
display_name = "荆棘女王荆棘披风"
description = "一件由荆棘与玫瑰丝编织而成的披风，披风表面不断有微型荆棘在生长和收缩——不是装饰，而是真正的活荆棘被封印在了丝线中。这些荆棘会保护穿戴者：当有人试图攻击穿戴者时，荆棘会自动竖起，刺入攻击者的皮肤。但荆棘也会伤害穿戴者——每次移动，荆棘都会轻轻刺入皮肤，留下微小的伤口。\n\n这件披风是荆棘女王在「荆棘花园」中，从自己的王座上取下的荆棘编织的。她说："这件披风让我成为了花园的一部分。它会保护我，也会伤害我——就像真正的玫瑰一样。"\n\n披风的效果是：近战攻击者受到 1D6 piercing（荆棘反击），且穿戴者免疫 grapple（荆棘挣脱）。但每移动 10 尺受到 1 点 piercing（荆棘刺痛）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "thorn_queen_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_piercing", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_thorn_queen_cloak" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thorn_queen_cloak" }
]
```

---

### 63.6 荆棘女王玫瑰之心项链（Thorn Queen Rose Heart Necklace）

```gdscript
item_id = "acc_thorn_queen_necklace"
display_name = "荆棘女王玫瑰之心项链"
description = "一条由玫瑰金与荆棘丝编织而成的项链，吊坠是一颗玫瑰之心——一颗由纯粹生命力凝结的宝石。宝石内部有一朵微型玫瑰，玫瑰永远在盛开和凋零之间循环。据说这颗心脏来自荆棘女王自己，她将自己的心取出，制成了这件饰品。\n\n这条项链是荆棘女王在「玫瑰祭坛」上，从自己的胸口取出的。她说："这颗心脏承载了我的爱和我的痛苦。它让我能够理解所有生物的情感，也让我能够控制它们。"\n\n项链的效果是：魅力检定 +3，且每日一次，可以释放「荆棘缠绕」——指定一个 20 尺内的敌人，其被荆棘缠绕（restrained 2 回合），且每回合开始时受到 2D6 piercing（DC16 力量豁免挣脱）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "thorn_queen_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_thorn_queen_necklace" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thorn_queen_necklace" }
]
```

---

### 63.7 荆棘女王毒刺之戒（Thorn Queen Venom Ring）

```gdscript
item_id = "acc_thorn_queen_ring_1"
display_name = "荆棘女王毒刺之戒"
description = "一枚由毒刺石与玫瑰金铸造的戒指，戒指表面刻有微型毒刺图案。当佩戴者集中精神时，戒指会释放出一股毒素，注入目标的血液。这种毒不是致命的——它会让目标感到麻痹、迟缓、视觉模糊。但如果目标体质虚弱，毒可能会永久损伤其神经系统。\n\n这枚戒指是荆棘女王在「毒刺深渊」中，从第一朵毒玫瑰身上取下的刺铸造的。她说："这枚戒指让我能够借用玫瑰的毒。但记住——最美丽的玫瑰往往最毒。"\n\n戒指的效果是：所有攻击附加 1D4 poison（玫瑰毒），且每日一次，可以释放「毒刺喷射」——5 尺射程 2D8 poison，目标须通过 DC15 体质豁免，失败则 poisoned 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "thorn_queen_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thorn_queen_ring_1" }
]
```

---

### 63.8 荆棘女王扎根之戒（Thorn Queen Root Ring）

```gdscript
item_id = "acc_thorn_queen_ring_2"
display_name = "荆棘女王扎根之戒"
description = "一枚由根系石与玫瑰银铸造的戒指，戒指表面刻有微型根系图案。当佩戴者集中精神时，戒指会释放出一股根系之力，使佩戴者的脚下长出微型根系，扎根于大地。这种扎根会使佩戴者无法移动，但会极大地增强防御力和恢复力。据说这种力量来自世界树的根系。\n\n这枚戒指是荆棘女王在「根系深渊」中，从世界树的根系上取下的碎片铸造的。她说："这枚戒指让我能够借用大地的力量。但大地是固执的——它不会让你轻易离开。"\n\n戒指的效果是：站立不动时，每回合开始时恢复 2D6 HP（根系治愈），且 AC +2（扎根稳固）。但移动力归零（扎根束缚，可以用 bonus action 解除）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "thorn_queen_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_thorn_queen_ring_2" }
]
```

---

### 63.9 荆棘女王荆棘王冠（Thorn Queen Thorn Crown）

```gdscript
item_id = "acc_thorn_queen_trinket"
display_name = "荆棘女王荆棘王冠"
description = "一顶由荆棘与玫瑰编织而成的微型王冠，王冠表面不断有微型玫瑰在盛开和凋零。这顶王冠不是给普通人戴的——它是给女王戴的。当佩戴者戴上它时，会感受到一种王者的威严，所有植物都会听从佩戴者的命令。但王冠也有代价：它会刺入佩戴者的头皮，留下永久的伤痕。\n\n这顶王冠是荆棘女王在「加冕仪式」上，从自己的花园中取下的荆棘编织的。她说："这顶王冠让我成为了花园的女王。但它也让我永远无法忘记自己的责任。"\n\n王冠的效果是：每日一次，可以释放「花园领域」——20 尺半径内所有敌人移动力减半（荆棘缠绕），且每回合开始时受到 1D6 piercing（DC15 力量豁免 half）。友方在领域内 AC +1（玫瑰庇护），持续 1 分钟。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "thorn_queen_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_thorn_queen_trinket" }
]
```

---

### 63.10 荆棘女王徽章（Thorn Queen Badge）

```gdscript
item_id = "acc_thorn_queen_badge"
display_name = "荆棘女王徽章"
description = "一枚由玫瑰金与荆棘石铸造的徽章，徽章上刻着一朵玫瑰和一根荆棘——荆棘女王的标志。徽章背面刻着一行小字："美丽与痛苦，本就一体。"这枚徽章是荆棘花园的信物，拥有它意味着你已被花园认可。\n\n徽章的效果是：10 尺内所有友方获得 +5 poison 抗性（荆棘庇护）。且每日一次，可以释放「玫瑰治愈」——10 尺内所有友方恢复 2D8 HP，但受到 1D4 piercing（玫瑰的刺），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "thorn_queen_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thorn_queen_badge" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_thorn_queen_badge" }
]
```

---

## 套装六十四：灰烬行者（Ash Walker）饰品

> *"灰烬行者不是从火中逃生的人——他是火选择留下的人。"*

---

### 64.5 灰烬行者灰烬披风（Ash Walker Ash Cloak）

```gdscript
item_id = "acc_ash_walker_cloak"
display_name = "灰烬行者灰烬披风"
description = "一件由灰烬与火焰丝编织而成的披风，披风表面不断有微型灰烬在飘落和燃烧——不是装饰，而是真正的灰烬被封印在了丝线中。这些灰烬来自不同的火灾：森林大火、城市火灾、甚至是火山爆发。每一片灰烬都保留了原火灾的记忆，穿戴者可以通过触摸它们，感受到火灾的恐怖和美丽。\n\n这件披风是灰烬行者在「灰烬平原」中，从自己的足迹上收集的灰烬编织的。他说："这件披风让我能够与火焰共存。它不会烧伤我，因为它知道我是火焰的一部分。"\n\n披风的效果是：fire 抗性 +20，且在火焰环境中（温度极高/火山）移动力不受困难地形影响。当穿戴者受到伤害时，有 25% 概率触发「灰烬闪避」——身体化作灰烬消散，免疫该次伤害（如同 blink），下一回合在 10 尺内重组。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "ash_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_fire", mode = "flat", value = 20, source_type = "equipment", source_id = "acc_ash_walker_cloak" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ash_walker_cloak" }
]
```

---

### 64.6 灰烬行者余烬之心项链（Ash Walker Ember Heart Necklace）

```gdscript
item_id = "acc_ash_walker_necklace"
display_name = "灰烬行者余烬之心项链"
description = "一条由余烬金与火焰水晶编织而成的项链，吊坠是一颗余烬之心——一颗由纯粹火焰凝结的心脏。心脏在吊坠中永恒地燃烧，发出微弱的橙光。据说这颗心脏来自一位自愿投身火焰的圣人，他的信仰如此强大，即使在灰烬中，他的心脏仍在燃烧。\n\n这条项链是灰烬行者在「余烬祭坛」上，从圣人的灰烬中找到的。他说："这颗心脏承载了圣人的信仰。它让我能够理解火焰的美丽，也让我能够承受火焰的恐怖。"\n\n项链的效果是：宗教检定 +3，且每日一次，可以释放「余烬治愈」——自身或 5 尺内一个友方恢复 2D10 HP，并移除一个非 legendary 诅咒或疾病（火焰净化）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "ash_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_ash_walker_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ash_walker_necklace" }
]
```

---

### 64.7 灰烬行者燃烧之戒（Ash Walker Burning Ring）

```gdscript
item_id = "acc_ash_walker_ring_1"
display_name = "灰烬行者燃烧之戒"
description = "一枚由燃烧石与火焰金铸造的戒指，戒指表面刻有微型火焰图案。当佩戴者集中精神时，戒指会释放出一股火焰之力，使佩戴者的武器被火焰包裹。这种火焰不会烧伤佩戴者，但会烧伤敌人。火焰的温度极高，足以熔化金属，蒸发水分。但火焰需要燃料——每次使用，佩戴者会感到一阵虚弱，仿佛自己的生命力被燃烧了。\n\n这枚戒指是灰烬行者在「燃烧深渊」中，从火焰恶魔身上取下的核心铸造的。他说："这枚戒指让我能够借用火焰的力量。但它也让我越来越难以感受到寒冷。"\n\n戒指的效果是：所有攻击附加 1D6 fire（灰烬之火），且每日一次，可以释放「灰烬爆发」——5 尺半径 3D10 fire（DC15 敏捷豁免 half），且范围内所有生物的装备有 20% 概率被烧毁（非魔法装备）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "ash_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ash_walker_ring_1" }
]
```

---

### 64.8 灰烬行者烟尘之戒（Ash Walker Soot Ring）

```gdscript
item_id = "acc_ash_walker_ring_2"
display_name = "灰烬行者烟尘之戒"
description = "一枚由烟尘石与灰烬银铸造的戒指，戒指表面刻有微型烟尘图案。当佩戴者集中精神时，戒指会释放出一股烟尘之力，使周围充满浓烟。这种烟尘不仅会遮蔽视线，还会让吸入者感到窒息。烟尘是火灾的副产品，它代表了死亡的沉默和绝望的呼喊。\n\n这枚戒指是灰烬行者在「烟尘迷宫」中，从迷宫核心取出的烟尘铸造的。他说："这枚戒指让我能够借用烟尘的力量。烟尘不是废物——它是火焰的记忆，是生命的最后叹息。"\n\n戒指的效果是：隐匿检定 +2（烟尘掩护），且每日一次，可以释放「烟尘帷幕」——10 尺半径内充满浓烟，能见度降至 5 尺，且所有生物每回合开始时受到 1D6 fire（烟尘窒息），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "ash_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ash_walker_ring_2" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_ash_walker_ring_2" }
]
```

---

### 64.9 灰烬行者凤凰灰烬（Ash Walker Phoenix Ash）

```gdscript
item_id = "acc_ash_walker_trinket"
display_name = "灰烬行者凤凰灰烬"
description = "一小瓶由凤凰灰烬制成的粉末，粉末在瓶中不断有微弱的光芒在闪烁。这些灰烬来自一只真正的凤凰，它在涅槃时留下的最后一点痕迹。据说这瓶灰烬包含了凤凰重生的秘密——只要将灰烬撒在自己的身体上，就能从死亡中重生。但重生是有代价的——每次重生，你都会失去一部分记忆。\n\n这瓶灰烬是灰烬行者在「凤凰墓」中，从凤凰的骨灰中找到的。他说："这瓶灰烬让我理解了重生的真谛。死亡不是终点，它只是另一个开始。"\n\n灰烬的效果是：每日一次，当 HP 降至 0 时，自动触发「灰烬重生」——恢复至 max HP 的 30%，对 10 尺内所有敌人造成 3D10 fire（凤凰余烬），且自身获得「火焰化身」2 回合——攻击附加 1D10 fire，免疫 fire。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "ash_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_ash_walker_trinket" }
]
```

---

### 64.10 灰烬行者徽章（Ash Walker Badge）

```gdscript
item_id = "acc_ash_walker_badge"
display_name = "灰烬行者徽章"
description = "一枚由灰烬金与火焰石铸造的徽章，徽章上刻着一只凤凰和一团火焰——灰烬行者的标志。徽章背面刻着一行小字："从灰烬中升起，或者成为灰烬。"这枚徽章是灰烬行者公会的信物，拥有它意味着你已理解火焰的真谛。\n\n徽章的效果是：10 尺内所有友方获得 +5 fire 抗性（灰烬庇护）。且每日一次，可以释放「灰烬祝福」——10 尺内所有友方获得「灰烬形态」2 回合：免疫 fire，且攻击附加 1D6 fire。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "ash_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ash_walker_badge" },
    { attribute_id = "survival_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_ash_walker_badge" }
]
```

---

## 套装六十五：迷雾行者（Mist Walker）饰品

> *"迷雾行者不是在雾中行走——他就是雾本身。"*

---

### 65.5 迷雾行者迷雾披风（Mist Walker Mist Cloak）

```gdscript
item_id = "acc_mist_walker_cloak"
display_name = "迷雾行者迷雾披风"
description = "一件由迷雾丝与虚空布编织而成的披风，披风表面不断有微型雾气在流动和扭曲——不是装饰，而是真正的迷雾被封印在了丝线中。当穿戴者移动时，披风会自动与周围的雾气融合，使穿戴者几乎不可见。在浓雾中，披风会让穿戴者完全消失，如同从未存在过。\n\n这件披风是迷雾行者在「迷雾森林」中，从自己的雾气中取下的碎片编织的。他说："这件披风让我成为了迷雾的一部分。我可以穿越任何雾气，隐藏在任何一个角落。但它也让我在阳光下感到不适。"\n\n披风的效果是：雾/霾/蒸汽环境中完全隐形（如同 greater invisibility），且可以传送到 20 尺内任何雾气中（每日两次）。当穿戴者从雾气中发动攻击时，攻击检定有优势（迷雾突袭）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "mist_walker_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_mist_walker_cloak" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mist_walker_cloak" }
]
```

---

### 65.6 迷雾行者雾之泪项链（Mist Walker Mist Tear Necklace）

```gdscript
item_id = "acc_mist_walker_necklace"
display_name = "迷雾行者雾之泪项链"
description = "一条由雾银与月光水晶编织而成的项链，吊坠是一颗雾之泪——一颗由纯粹迷雾凝结的水晶。水晶内部不断有微型雾气在旋转，如同一个小型的风暴。据说这颗眼泪来自第一位迷雾行者，她在最后一次行走中流下了这颗永恒的眼泪。\n\n这条项链是迷雾行者在「雾之泉」边，从泉水中取出的第一滴泪制成的。她说："这颗眼泪承载了我所有的行走——我的孤独，我的自由，我的迷失。当你佩戴它时，你会感受到迷雾的真正意义。"\n\n项链的效果是：察觉检定 +3，且每日一次，可以释放「迷雾召唤」——在 20 尺半径内召唤浓雾，能见度降至 5 尺，持续 1 分钟。敌人在雾中攻击检定有劣势，友方在雾中隐匿检定有优势。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "mist_walker_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_mist_walker_necklace" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mist_walker_necklace" }
]
```

---

### 65.7 迷雾行者幻影之戒（Mist Walker Phantom Ring）

```gdscript
item_id = "acc_mist_walker_ring_1"
display_name = "迷雾行者幻影之戒"
description = "一枚由幻影石与雾金铸造的戒指，戒指表面刻有微型幻影图案。当佩戴者集中精神时，戒指会释放出一股幻影之力，创造出一个佩戴者的幻象。这个幻象会模仿佩戴者的动作，吸引敌人的攻击。但幻象是脆弱的——一旦被击中，就会消散。\n\n这枚戒指是迷雾行者在「幻影迷宫」中，从幻影恶魔身上取下的核心铸造的。她说："这枚戒指让我能够借用幻影的力量。但幻影不是真实的——它只能欺骗，不能保护。"\n\n戒指的效果是：每日一次，可以释放「幻影分身」——创造一个完全相同的分身（1 HP，AC = 你的 AC），持续 2 回合。分身可以进行一次攻击（+5，1D8+3 force）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "mist_walker_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mist_walker_ring_1" }
]
```

---

### 65.8 迷雾行者沉默之戒（Mist Walker Silence Ring）

```gdscript
item_id = "acc_mist_walker_ring_2"
display_name = "迷雾行者沉默之戒"
description = "一枚由沉默石与雾银铸造的戒指，戒指表面刻有微型静音图案。当佩戴者集中精神时，戒指会释放出一股沉默之力，使周围的声音完全消失。在沉默领域中，没有任何声音可以传播——说话、脚步、甚至心跳都会被吞噬。这种沉默是绝对的，也是可怕的。\n\n这枚戒指是迷雾行者在「沉默雾」中，从雾气核心取出的物质铸造的。她说："这枚戒指让我能够借用沉默的力量。沉默不是空虚——它是另一种声音。学会倾听它。"\n\n戒指的效果是：隐匿检定 +2（无声移动），且每日一次，可以释放「沉默领域」——10 尺半径内所有声音消失 2 回合，期间区域内无法施法（需要语言成分），且所有生物的攻击检定 -1（失去声音反馈）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "mist_walker_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mist_walker_ring_2" }
]
```

---

### 65.9 迷雾行者雾中镜（Mist Walker Mist Mirror）

```gdscript
item_id = "acc_mist_walker_trinket"
display_name = "迷雾行者雾中镜"
description = "一面由迷雾凝结而成的镜子，镜子表面不断有雾气在流动，使得镜中的倒影变得模糊和扭曲。这面镜子不是普通的镜子——它可以显示佩戴者想要看到的任何东西：过去、未来、甚至是其他世界。但镜子有自己的意志——有时它会显示佩戴者不想看到的东西。\n\n这面镜子是迷雾行者在「雾中镜厅」中，从镜子恶魔身上取下的核心制成的。她说："这面镜子让我能够窥视迷雾背后的真相。但真相往往是残酷的。"\n\n镜子的效果是：每日一次，可以释放「雾中幻象」——指定一个 20 尺内的敌人，其陷入幻象（stunned 1 回合，随后 disoriented 2 回合——攻击检定和感知检定有劣势，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "mist_walker_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_mist_walker_trinket" }
]
```

---

### 65.10 迷雾行者徽章（Mist Walker Badge）

```gdscript
item_id = "acc_mist_walker_badge"
display_name = "迷雾行者徽章"
description = "一枚由雾金与虚空石铸造的徽章，徽章上刻着一团雾和一个脚印——迷雾行者的标志。徽章背面刻着一行小字："在迷雾中行走，但不要迷失。"这枚徽章是迷雾行者公会的信物，拥有它意味着你已掌握迷雾的艺术。\n\n徽章的效果是：10 尺内所有友方在雾中隐匿检定 +2（迷雾庇护）。且每日一次，可以释放「迷雾传送」——10 尺内所有友方可以 bonus action 传送到 20 尺内任何雾气中（迷雾步）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "mist_walker_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mist_walker_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mist_walker_badge" }
]
```

---

## 套装六十六：铁处女（Iron Maiden）饰品

> *"铁处女不是残忍，她只是把'保护'做到了极致——哪怕保护的方式是痛苦。"*

---

### 66.5 铁处女铁刺披风（Iron Maiden Spike Cloak）

```gdscript
item_id = "acc_iron_maiden_cloak"
display_name = "铁处女铁刺披风"
description = "一件由铁刺与魔法丝编织而成的披风，披风表面覆盖着微型铁刺，每一根铁刺都是锋利的。当穿戴者受到威胁时，铁刺会自动竖起，刺入攻击者的皮肤。但铁刺也会伤害穿戴者——每次移动，铁刺都会轻轻刺入皮肤，留下微小的伤口。这件披风是铁处女的延伸——它既是保护，也是惩罚。\n\n这件披风是铁处女在「铁处女工坊」中，从自己的外壳上取下的铁刺编织的。她说："这件披风让我能够将痛苦转化为力量。每一次刺痛，都让我更加清醒。"\n\n披风的效果是：近战攻击者受到 1D8 piercing（铁刺反击），且穿戴者免疫 grapple（铁刺挣脱）。但每回合开始时受到 1D4 piercing（铁刺刺痛）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "iron_maiden_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_maiden_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_maiden_cloak" }
]
```

---

### 66.6 铁处女痛苦之心项链（Iron Maiden Pain Heart Necklace）

```gdscript
item_id = "acc_iron_maiden_necklace"
display_name = "铁处女痛苦之心项链"
description = "一条由痛苦金与铁刺丝编织而成的项链，吊坠是一颗痛苦之心——一颗由纯粹痛苦凝结的宝石。宝石内部不断有微型痛苦面孔在扭曲和哀嚎。据说这颗心脏来自第一位铁处女，她在无尽的痛苦中，将自己的心取出，制成了这件饰品。\n\n这条项链是铁处女在「痛苦祭坛」上，从自己的胸口取出的。她说："这颗心脏承载了我的所有痛苦。它让我能够理解痛苦的本质，也让我能够将痛苦转化为力量。"\n\n项链的效果是：当穿戴者受到伤害时，下一回合攻击检定 +2（痛苦反击，可叠加至 +4）。且免疫 frightened（痛苦麻木）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "iron_maiden_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_maiden_necklace" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_iron_maiden_necklace" }
]
```

---

### 66.7 铁处女锁链之戒（Iron Maiden Chain Ring）

```gdscript
item_id = "acc_iron_maiden_ring_1"
display_name = "铁处女锁链之戒"
description = "一枚由锁链与痛苦金铸造的戒指，戒指表面刻有微型锁链图案。当佩戴者集中精神时，戒指会释放出一股锁链之力，束缚目标。这种束缚不是物理的——它是痛苦的束缚，让目标感受到无尽的痛苦，无法行动。但锁链也会束缚佩戴者——每次使用，佩戴者会感到同样的痛苦。\n\n这枚戒指是铁处女在「锁链深渊」中，从痛苦之神那里得到的。痛苦之神说："这枚戒指让你能够分享痛苦。但记住——痛苦的分享不会减少痛苦，它只会增加痛苦。"\n\n戒指的效果是：每日一次，可以释放「痛苦锁链」——指定一个 15 尺内的敌人，其 restrained 2 回合，且每回合开始时受到 2D6 piercing（锁链穿刺，DC16 力量豁免提前挣脱）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "iron_maiden_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_maiden_ring_1" }
]
```

---

### 66.8 铁处女荆棘之戒（Iron Maiden Thorn Ring）

```gdscript
item_id = "acc_iron_maiden_ring_2"
display_name = "铁处女荆棘之戒"
description = "一枚由荆棘与痛苦银铸造的戒指，戒指表面缠绕着微型荆棘——这些荆棘是活的，会自主刺入佩戴者的手指。但荆棘不会流血——它们会吸收血液，转化为力量。这种力量可以增强佩戴者的攻击，也可以增强佩戴者的防御。但荆棘的需求是无止境的——它们永远不会满足。\n\n这枚戒指是铁处女在「荆棘深渊」中，从荆棘恶魔身上取下的核心铸造的。她说："这枚戒指让我能够借用荆棘的力量。但荆棘是贪婪的——它会永远索取，永不回报。"\n\n戒指的效果是：攻击检定 +1，但每回合开始时受到 1D4 piercing（荆棘吸血）。当 HP 低于 50% 时，攻击检定再 +2（荆棘狂怒）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "iron_maiden_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_maiden_ring_2" }
]
```

---

### 66.9 铁处女痛苦面具（Iron Maiden Pain Mask）

```gdscript
item_id = "acc_iron_maiden_trinket"
display_name = "铁处女痛苦面具"
description = "一张由铁刺与痛苦凝结而成的面具，面具表面没有任何表情——只有无尽的痛苦。当佩戴者戴上它时，会感受到所有前任佩戴者的痛苦：他们的尖叫、他们的哀嚎、他们的绝望。但这种痛苦会转化为力量——越痛苦，越强大。据说这张面具来自第一位铁处女，她在无尽的痛苦中，将自己的脸制成了这件饰品。\n\n这张面具是铁处女在「痛苦殿堂」中，从自己的脸上取下的。她说："这张面具让我能够将痛苦转化为力量。但它也让我永远无法微笑。"\n\n面具的效果是：每日一次，可以释放「痛苦爆发」——将积累的痛苦释放为攻击：对 10 尺内所有敌人造成 4D10 force（痛苦冲击），须通过 DC17 体质豁免，失败则 full damage 并 stunned 1 回合（痛苦过载），成功则 half damage。释放后穿戴者受到 2D10 force（痛苦反噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "iron_maiden_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_iron_maiden_trinket" }
]
```

---

### 66.10 铁处女徽章（Iron Maiden Badge）

```gdscript
item_id = "acc_iron_maiden_badge"
display_name = "铁处女徽章"
description = "一枚由痛苦金与铁石铸造的徽章，徽章上刻着一把钥匙和一个锁孔——铁处女的标志。徽章背面刻着一行小字："痛苦是钥匙，力量是锁。"这枚徽章是铁处女公会的信物，拥有它意味着你已理解痛苦的真谛。\n\n徽章的效果是：10 尺内所有友方受到伤害时，下一回合攻击检定 +1（痛苦共鸣，可叠加至 +3）。且每日一次，可以释放「痛苦领域」——10 尺内所有敌人每回合开始时受到 1D6 piercing（铁刺侵蚀），且移动时受到 1D4 piercing（铁刺地面），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "iron_maiden_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_maiden_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_iron_maiden_badge" }
]
```

---

## 套装六十七：蜘蛛女王（Spider Queen）饰品

> *"蜘蛛女王不是在织网——她在编织命运。"*

---

### 67.5 蜘蛛女王蛛丝披风（Spider Queen Silk Cloak）

```gdscript
item_id = "acc_spider_queen_cloak"
display_name = "蜘蛛女王蛛丝披风"
description = "一件由蛛丝与魔法丝编织而成的披风，披风表面不断有微型蛛丝在流动和纠结——不是装饰，而是真正的蛛丝被封印在了丝线中。这些蛛丝来自不同的蜘蛛：有的黏性极强，有的坚韧如钢，有的毒性剧烈。当穿戴者受到威胁时，蛛丝会自动喷出，束缚攻击者。\n\n这件披风是蜘蛛女王在「蛛网宫殿」中，从自己的腹部取下的蛛丝编织的。她说："这件披风让我能够将蛛网的力量带到任何地方。它可以束缚敌人，也可以保护我。"\n\n披风的效果是：免疫 web 和 grapple（蛛丝挣脱），且在蛛网/丝类地形上移动力不受限制。当穿戴者被近战攻击时，攻击者须通过 DC14 敏捷豁免，失败则 restrained 1 回合（蛛丝缠绕）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "spider_queen_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_spider_queen_cloak" },
    { attribute_id = "acrobatics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_spider_queen_cloak" }
]
```

---

### 67.6 蜘蛛女王毒牙项链（Spider Queen Venom Fang Necklace）

```gdscript
item_id = "acc_spider_queen_necklace"
display_name = "蜘蛛女王毒牙项链"
description = "一条由毒牙金与蛛丝编织而成的项链，吊坠是一颗毒牙——一颗从蜘蛛女王自己身上取下的毒牙。毒牙内部有微型毒液在流动，发出微弱的绿光。据说这颗毒牙包含了蜘蛛女王的所有毒素知识，每一种毒都有其独特的效果。\n\n这条项链是蜘蛛女王在「毒牙祭坛」上，从自己的口器中取下的。她说："这颗毒牙承载了我的所有毒素。它让我能够治愈，也让我能够杀死。"\n\n项链的效果是：自然检定 +3，且每日一次，可以释放「毒牙攻击」——指定一个 5 尺内的敌人，受到 2D8 piercing + 3D8 poison（毒牙注入），目标须通过 DC17 体质豁免，失败则 poisoned 2 回合且 paralyzed 1 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "spider_queen_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_spider_queen_necklace" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_spider_queen_necklace" }
]
```

---

### 67.7 蜘蛛女王蛛网之戒（Spider Queen Web Ring）

```gdscript
item_id = "acc_spider_queen_ring_1"
display_name = "蜘蛛女王蛛网之戒"
description = "一枚由蛛网石与毒金铸造的戒指，戒指表面刻有微型蛛网图案。当佩戴者集中精神时，戒指会释放出一股蛛丝之力，喷出一张蛛网，束缚目标。这种蛛丝不是普通的蛛丝——它是魔法蛛丝，可以束缚灵魂，甚至束缚魔法。\n\n这枚戒指是蜘蛛女王在「蛛网深渊」中，从蛛网恶魔身上取下的核心铸造的。她说："这枚戒指让我能够借用蛛网的力量。蛛网不是陷阱——它是艺术，是命运，是控制。"\n\n戒指的效果是：每日一次，可以释放「蛛网喷射」——20 尺射程，目标 restrained 2 回合（魔法蛛网，DC16 力量豁免挣脱），且每回合开始时受到 1D6 poison（蛛网毒素）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "spider_queen_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_spider_queen_ring_1" }
]
```

---

### 67.8 蜘蛛女王寄生之戒（Spider Queen Parasite Ring）

```gdscript
item_id = "acc_spider_queen_ring_2"
display_name = "蜘蛛女王寄生之戒"
description = "一枚由寄生石与蛛金铸造的戒指，戒指内部有一只微型蜘蛛卵——这只卵是活的，它会在佩戴者的手指上钻一个小孔，进入血液循环。蜘蛛不会伤害佩戴者——相反，它会帮助佩戴者：修复损伤、清除毒素、甚至增强感知。但蜘蛛需要营养——它会从佩戴者身上吸取少量血液。\n\n这枚戒指是蜘蛛女王在「寄生深渊」中，从第一只共生蜘蛛身上取下的卵铸造的。她说："这枚戒指让我能够与蜘蛛共生。它不是我的敌人——它是我的子嗣。"\n\n戒指的效果是：免疫 poison（蜘蛛清除），且感知检定 +2（蜘蛛感官）。但每回合开始时受到 1 点 piercing（蜘蛛吸血）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "spider_queen_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_spider_queen_ring_2" }
]
```

---

### 67.9 蜘蛛女王蜘蛛卵囊（Spider Queen Spider Egg Sac）

```gdscript
item_id = "acc_spider_queen_trinket"
display_name = "蜘蛛女王蜘蛛卵囊"
description = "一个由蛛丝与魔法制成的微型卵囊，卵囊内部有数十只微型蜘蛛在蠕动。这些蜘蛛不是普通的蜘蛛——它们是魔法蜘蛛，可以听从佩戴者的命令。当佩戴者需要时，可以释放这些蜘蛛，让它们攻击敌人或编织蛛网。但蜘蛛是饥饿的——如果不定期喂养，它们会攻击佩戴者。\n\n这个卵囊是蜘蛛女王在「蜘蛛巢穴」中，从自己的腹部取下的。她说："这个卵囊让我能够随时召唤我的子嗣。它们会为我战斗，为我牺牲，为我织网。"\n\n卵囊的效果是：每日一次，可以释放「蜘蛛群」——释放 2D4 只魔法蜘蛛（HP 10，AC 12，攻击 +4，1D6+2 piercing + 1D4 poison），持续 1 分钟。可以用 bonus action 指挥蜘蛛群。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "spider_queen_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_spider_queen_trinket" }
]
```

---

### 67.10 蜘蛛女王徽章（Spider Queen Badge）

```gdscript
item_id = "acc_spider_queen_badge"
display_name = "蜘蛛女王徽章"
description = "一枚由蛛金与毒石铸造的徽章，徽章上刻着一只蜘蛛和一张网——蜘蛛女王的标志。徽章背面刻着一行小字："织网者控制一切。"这枚徽章是蜘蛛公会的信物，拥有它意味着你已被蜘蛛认可。\n\n徽章的效果是：10 尺内所有友方免疫 web 和 grapple（蛛网庇护）。且每日一次，可以释放「蛛网领域」——15 尺半径内所有敌人移动力减半（蛛网缠绕），且每回合开始时受到 1D6 poison（蛛网毒素），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "spider_queen_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_spider_queen_badge" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_spider_queen_badge" }
]
```

---

## 套装六十八：乌鸦领主（Raven Lord）饰品

> *"乌鸦领主不是带来死亡的人——他只是比死亡早到一步。"*

---

### 68.5 乌鸦领主黑羽披风（Raven Lord Feather Cloak）

```gdscript
item_id = "acc_raven_lord_cloak"
display_name = "乌鸦领主黑羽披风"
description = "一件由乌鸦羽毛与暗影丝编织而成的披风，披风表面覆盖着微型黑羽，每一根羽毛都来自不同的乌鸦——有的来自普通的乌鸦，有的来自魔法乌鸦，有的甚至来自死神乌鸦。当穿戴者行走时，羽毛会轻轻飘动，发出沙沙的声音。在死亡发生时，羽毛会自动竖立，感知到灵魂的气息。\n\n这件披风是乌鸦领主在「乌鸦塔」中，从自己的鸟群上取下的羽毛编织的。他说："这件披风让我能够与乌鸦共鸣。它们是我的眼睛，我的耳朵，我的使者。"\n\n披风的效果是：可以感知 30 尺内的濒死生物（HP ≤ 10% max），且对这些生物的攻击检定 +2（死亡之眼）。当穿戴者击杀一个生物时，恢复 1D8 HP（灵魂吞噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "raven_lord_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_raven_lord_cloak" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_raven_lord_cloak" }
]
```

---

### 68.6 乌鸦领主死亡之眼项链（Raven Lord Death Eye Necklace）

```gdscript
item_id = "acc_raven_lord_necklace"
display_name = "乌鸦领主死亡之眼项链"
description = "一条由乌鸦骨与冥金编织而成的项链，吊坠是一只死亡之眼——一只从死神乌鸦身上取下的真实眼睛。这只眼睛不会眨眼，不会转动，只是永恒地凝视着前方。凝视这只眼睛的人会看到自己的死亡——不是预测，而是感受。据说这只眼睛可以看到所有生物的死亡时间，但它只会告诉佩戴者它愿意告诉的。\n\n这条项链是乌鸦领主在「死亡之眼祭坛」上，从死神乌鸦身上取下的。他说："这只眼睛承载了死亡的秘密。它让我能够预见死亡，也让我能够逃避死亡。"\n\n项链的效果是：宗教检定 +3，且可以感知 30 尺内生物的「死亡气息」（HP 越低越明显）。每日一次，可以释放「死亡凝视」——指定一个 15 尺内的敌人，其受到 3D10 necrotic（死亡之眼），须通过 DC16 体质豁免，失败则 max HP 降低 10 直至长休（死亡预兆）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "raven_lord_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_raven_lord_necklace" },
    { attribute_id = "insight_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_raven_lord_necklace" }
]
```

---

### 68.7 乌鸦领主夺魂之戒（Raven Lord Soul Ring）

```gdscript
item_id = "acc_raven_lord_ring_1"
display_name = "乌鸦领主夺魂之戒"
description = "一枚由夺魂石与冥金铸造的戒指，戒指表面刻有微型乌鸦图案。当佩戴者集中精神时，戒指会释放出一股夺魂之力，吸收目标的灵魂碎片。这种吸收不是致命的——它只是让目标感到虚弱和空虚。但吸收的灵魂碎片会增强佩戴者的力量，使其变得更加强大。\n\n这枚戒指是乌鸦领主在「夺魂深渊」中，从死神身上取下的碎片铸造的。他说："这枚戒指让我能够借用死神的力量。但它也让我越来越难以感受到生命的温暖。"\n\n戒指的效果是：击杀一个生物时，恢复 2D8 HP（灵魂吞噬），且获得 1 层「灵魂充能」（最多 3 层）。每层提供 +1D6 necrotic 伤害（所有攻击附加）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "raven_lord_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raven_lord_ring_1" }
]
```

---

### 68.8 乌鸦领主厄运之戒（Raven Lord Doom Ring）

```gdscript
item_id = "acc_raven_lord_ring_2"
display_name = "乌鸦领主厄运之戒"
description = "一枚由厄运石与冥银铸造的戒指，戒指表面刻有微型厄运符文。当佩戴者集中精神时，戒指会释放出一股厄运之力，使目标遭遇不幸。这种不幸不是立即的——它会在未来某个时刻降临，让目标在最不合时宜的时候遭遇灾难。据说这枚戒指来自厄运女神，她将自己的一部分力量封印在了其中。\n\n这枚戒指是乌鸦领主在「厄运神殿」中，从厄运女神那里得到的。厄运女神说："这枚戒指让你能够借用我的力量。但厄运是公平的——它不会永远只降临在敌人身上。"\n\n戒指的效果是：每日一次，可以释放「厄运标记」——指定一个可见敌人，其在 1 分钟内遭遇厄运：下一次攻击自动 miss，下一次豁免自动失败，或下一次技能检定自动失败（由 DM 选择）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "raven_lord_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raven_lord_ring_2" }
]
```

---

### 68.9 乌鸦领主乌鸦雕像（Raven Lord Raven Statue）

```gdscript
item_id = "acc_raven_lord_trinket"
display_name = "乌鸦领主乌鸦雕像"
description = "一座由乌鸦骨与冥金制成的微型雕像，雕像是一只展翅的乌鸦，眼睛由两颗红宝石制成。这座雕像不是普通的装饰品——它是活的。当佩戴者需要时，可以激活雕像，召唤一只死神乌鸦。这只乌鸦会听从佩戴者的命令，攻击敌人或传递信息。但乌鸦需要报酬——每次召唤，它会从佩戴者身上取走一小部分生命力。\n\n这座雕像是乌鸦领主在「乌鸦巢穴」中，从死神乌鸦的蛋中孵化出的第一只乌鸦制成的。他说："这座雕像让我能够随时召唤我的使者。但它也让我越来越接近死亡。"\n\n雕像的效果是：每日一次，可以召唤「死神乌鸦」——乌鸦攻击一个可见敌人（+6，2D8+4 necrotic + 1D6 bleed），且可以 bonus action 命令乌鸦传送到 60 尺内任何位置。乌鸦持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "raven_lord_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_raven_lord_trinket" }
]
```

---

### 68.10 乌鸦领主徽章（Raven Lord Badge）

```gdscript
item_id = "acc_raven_lord_badge"
display_name = "乌鸦领主徽章"
description = "一枚由冥金与骨石铸造的徽章，徽章上刻着一只乌鸦和一个骷髅——乌鸦领主的标志。徽章背面刻着一行小字："死亡是消息，而我是信使。"这枚徽章是乌鸦公会的信物，拥有它意味着你已被死亡认可。\n\n徽章的效果是：10 尺内所有友方击杀敌人时恢复 1D6 HP（灵魂分享）。且每日一次，可以释放「死亡宣告」——指定一个 20 尺内的敌人，其受到 2D10 necrotic（死亡宣告），且下一回合攻击检定 -2（死亡恐惧，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "raven_lord_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "religion_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raven_lord_badge" },
    { attribute_id = "perception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_raven_lord_badge" }
]
```

---

## 套装六十九：锈刃骑士（Rust Blade Knight）饰品

> *"锈刃骑士的盔甲不是旧了——它是'成熟'，就像好酒需要陈酿。"*

---

### 69.5 锈刃骑士锈铁披风（Rust Blade Knight Rust Cloak）

```gdscript
item_id = "acc_rust_blade_cloak"
display_name = "锈刃骑士锈铁披风"
description = "一件由锈蚀铁片与魔法丝编织而成的披风，披风表面覆盖着红褐色的锈迹，每一道锈迹都代表着一次战斗、一次胜利、一次幸存。当穿戴者行走时，锈迹会脱落，留下一条铁锈的痕迹。在潮湿环境中，披风会自动生锈，变得更加沉重，但也更加坚固。\n\n这件披风是锈刃骑士在「锈蚀堡垒」中，从自己的盔甲上取下的碎片编织的。他说："这件披风承载了我所有的战斗。每一道锈迹都是一个故事，每一个故事都是一次生死。"\n\n披风的效果是：acid 抗性 +10（锈蚀免疫），且被近战攻击时，攻击者的武器有 20% 概率生锈（-1 攻击检定，可叠加至 -3，持续至修复）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "rust_blade_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "resistance_acid", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_rust_blade_cloak" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rust_blade_cloak" }
]
```

---

### 69.6 锈刃骑士锈蚀之心项链（Rust Blade Knight Rust Heart Necklace）

```gdscript
item_id = "acc_rust_blade_necklace"
display_name = "锈刃骑士锈蚀之心项链"
description = "一条由锈蚀金与铁刺丝编织而成的项链，吊坠是一颗锈蚀之心——一颗由纯粹锈蚀凝结的宝石。宝石内部不断有微型铁片在生锈和剥落，如同一个小型的时间流逝。据说这颗心脏来自一位古老的锈刃骑士，他在战斗中失去了自己的心，用锈蚀填补了这个空缺。\n\n这条项链是锈刃骑士在「锈蚀祭坛」上，从古老骑士的遗骸中取出的。他说："这颗心脏承载了古老骑士的意志。它让我能够理解锈蚀的美丽，也让我能够承受时间的流逝。"\n\n项链的效果是：体质豁免 +2（锈蚀坚韧），且免疫 rust 效果（锈蚀免疫）。每日一次，可以释放「锈蚀之息」——15 尺锥形，所有金属装备受到 2D8 acid（锈蚀侵蚀，非魔法装备有 30% 概率损毁）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "rust_blade_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rust_blade_necklace" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_rust_blade_necklace" }
]
```

---

### 69.7 锈刃骑士锈蚀之戒（Rust Blade Knight Rust Ring）

```gdscript
item_id = "acc_rust_blade_ring_1"
display_name = "锈刃骑士锈蚀之戒"
description = "一枚由锈蚀石与铁金铸造的戒指，戒指表面覆盖着厚厚的锈迹。当佩戴者集中精神时，戒指会释放出一股锈蚀之力，加速目标的 decay——金属会生锈，木头会腐烂，石头会风化。这种锈蚀是不可逆的，即使魔法也无法完全修复。\n\n这枚戒指是锈刃骑士在「锈蚀深渊」中，从锈蚀之神那里得到的。锈蚀之神说："这枚戒指让你能够借用我的力量。但锈蚀是公平的——它会腐蚀一切，包括你自己。"\n\n戒指的效果是：所有攻击附加 1D4 acid（锈蚀），且每日一次，可以释放「锈蚀之触」——指定一个 5 尺内的敌人，其金属护甲 AC -2（锈蚀），且武器攻击检定 -2（锈蚀），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "rust_blade_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rust_blade_ring_1" }
]
```

---

### 69.8 锈刃骑士钝刃之戒（Rust Blade Knight Dull Ring）

```gdscript
item_id = "acc_rust_blade_ring_2"
display_name = "锈刃骑士钝刃之戒"
description = "一枚由钝刃石与铁银铸造的戒指，戒指表面刻有微型钝刃图案。当佩戴者集中精神时，戒指会释放出一股钝化之力，使目标的武器变钝。这种钝化不是物理的——它是魔法的，可以让最锋利的剑变得如木棒般无力。但钝化也会反噬——佩戴者自己的武器也会受到影响。\n\n这枚戒指是锈刃骑士在「钝刃迷宫」中，从迷宫核心取出的物质铸造的。他说："这枚戒指让我能够借用钝化的力量。但记住——钝化是公平的，它不会区分敌我。"\n\n戒指的效果是：每日一次，可以释放「钝刃领域」——10 尺半径内所有敌人的武器攻击伤害 -1D6（钝化处理），持续 2 回合。但佩戴者自己的武器也受到伤害 -1D4（反噬）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "rust_blade_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_strength", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rust_blade_ring_2" }
]
```

---

### 69.9 锈刃骑士锈蚀之种（Rust Blade Knight Rust Seed）

```gdscript
item_id = "acc_rust_blade_trinket"
display_name = "锈刃骑士锈蚀之种"
description = "一颗由纯粹锈蚀凝结的种子，种子表面覆盖着厚厚的锈迹。这颗种子不会发芽——它只会生锈。但当佩戴者需要时，可以激活种子，让它在目标身上「生长」，迅速锈蚀目标的所有金属装备。这种锈蚀是致命的——它可以在几秒钟内将一副精钢盔甲变成一堆铁锈。\n\n这颗种子是锈刃骑士在「锈蚀之源」中，从锈蚀之神的身上取下的。他说："这颗种子包含了锈蚀的源头。它不是给凡人的礼物——它是给毁灭者的武器。"\n\n种子的效果是：每日一次，可以释放「锈蚀爆发」——指定一个 15 尺内的敌人，其所有金属装备立即受到 4D8 acid（锈蚀爆发），非魔法金属装备有 50% 概率当场损毁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "rust_blade_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_rust_blade_trinket" }
]
```

---

### 69.10 锈刃骑士徽章（Rust Blade Knight Badge）

```gdscript
item_id = "acc_rust_blade_badge"
display_name = "锈刃骑士徽章"
description = "一枚由铁金与锈石铸造的徽章，徽章上刻着一把生锈的剑和一个沙漏——锈刃骑士的标志。徽章背面刻着一行小字："时间会腐蚀一切，但意志不会。"这枚徽章是锈刃骑士公会的信物，拥有它意味着你已被时间认可。\n\n徽章的效果是：10 尺内所有友方 acid 抗性 +5（锈蚀庇护）。且每日一次，可以释放「锈蚀光环」——10 尺内所有敌人的金属装备攻击检定 -1（锈蚀侵蚀），且 AC -1（锈蚀护甲），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "rust_blade_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "athletics_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rust_blade_badge" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_rust_blade_badge" }
]
```

---

## 套装七十：镜中恶魔（Mirror Demon）饰品

> *"镜中恶魔不是在镜子中——镜子中的只是你的倒影，而它，是倒影中的你。"*

---

### 70.5 镜中恶魔镜面披风（Mirror Demon Mirror Cloak）

```gdscript
item_id = "acc_mirror_demon_cloak"
display_name = "镜中恶魔镜面披风"
description = "一件由镜面丝与虚空布编织而成的披风，披风表面不断有微型镜面在闪烁和反射——不是装饰，而是真正的镜面被封印在了丝线中。当穿戴者移动时，披风会自动反射周围的光线，使穿戴者的位置变得模糊。敌人无法确定穿戴者的真正位置，攻击常常会命中错误的位置。\n\n这件披风是镜中恶魔在「镜之殿堂」中，从自己的镜面上取下的碎片编织的。他说："这件披风让我能够借用镜面的力量。我既在这里，也在那里；既是真实，也是虚幻。"\n\n披风的效果是：AC +1（镜面反射），且每日一次，可以释放「镜像分身」——创造一个完全相同的分身（1 HP，AC = 你的 AC），持续 2 回合。分身可以进行一次攻击（+5，1D8+3 force）。敌人无法分辨真假（DC18 洞察豁免）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "mirror_demon_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mirror_demon_cloak" },
    { attribute_id = "deception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mirror_demon_cloak" }
]
```

---

### 70.6 镜中恶魔倒影之眼项链（Mirror Demon Reflection Eye Necklace）

```gdscript
item_id = "acc_mirror_demon_necklace"
display_name = "镜中恶魔倒影之眼项链"
description = "一条由镜面金与虚空水晶编织而成的项链，吊坠是一只倒影之眼——一只从镜中恶魔身上取下的真实眼睛。这只眼睛看到的不是现实——它看到的是现实的倒影。通过这只眼睛，佩戴者可以看到事物的反面：善良背后的邪恶，美丽背后的丑陋，真实背后的虚假。\n\n这条项链是镜中恶魔在「倒影祭坛」上，从自己的脸上取下的。他说："这只眼睛让我能够看到世界的真相——不是表面的真相，而是倒影背后的真相。"\n\n项链的效果是：洞察检定 +3，且可以感知 30 尺内生物的「真实意图」（如同 detect thoughts 的深层，DC15 智慧豁免抵抗）。每日一次，可以释放「倒影诅咒」——指定一个 15 尺内的敌人，其下一回合必须攻击最近的友方（倒影迷惑，DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "mirror_demon_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_mirror_demon_necklace" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mirror_demon_necklace" }
]
```

---

### 70.7 镜中恶魔复制之戒（Mirror Demon Copy Ring）

```gdscript
item_id = "acc_mirror_demon_ring_1"
display_name = "镜中恶魔复制之戒"
description = "一枚由复制石与镜面金铸造的戒指，戒指表面刻有微型复制图案。当佩戴者集中精神时，戒指会释放出一股复制之力，复制目标的某个能力。这种复制不是永久的——它只能持续很短的时间。但复制的力量是完整的——它可以复制任何能力，无论是物理的还是魔法的。\n\n这枚戒指是镜中恶魔在「复制深渊」中，从复制之神那里得到的。复制之神说："这枚戒指让你能够借用他人的力量。但记住——复制不是创造，它只是模仿。"\n\n戒指的效果是：每日一次，可以「复制」一个可见敌人的一项能力（攻击方式、法术、或特殊能力），在下一回合内使用一次（复制攻击检定/DC 使用你的数值）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "mirror_demon_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_mirror_demon_ring_1" }
]
```

---

### 70.8 镜中恶魔反转之戒（Mirror Demon Inversion Ring）

```gdscript
item_id = "acc_mirror_demon_ring_2"
display_name = "镜中恶魔反转之戒"
description = "一枚由反转石与镜面银铸造的戒指，戒指表面刻有微型反转图案。当佩戴者集中精神时，戒指会释放出一股反转之力，反转目标的某个属性。这种反转不是永久的——它只能持续很短的时间。但反转的效果是彻底的——它可以将力量变为弱点，将优势变为劣势。\n\n这枚戒指是镜中恶魔在「反转迷宫」中，从迷宫核心取出的物质铸造的。他说："这枚戒指让我能够借用反转的力量。但反转是危险的——它不会区分敌我。"\n\n戒指的效果是：每日一次，可以释放「属性反转」——指定一个 15 尺内的敌人，其一项属性（力量/敏捷/体质/智慧/智力/魅力）在 2 回合内变为 10（如果原本高于 10）或保持不变（如果原本低于 10）（DC16 智慧豁免抵抗）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "mirror_demon_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mirror_demon_ring_2" }
]
```

---

### 70.9 镜中恶魔碎镜（Mirror Demon Broken Mirror）

```gdscript
item_id = "acc_mirror_demon_trinket"
display_name = "镜中恶魔碎镜"
description = "一面破碎的镜子，镜面由无数碎片组成，每一片碎片都反射出不同的景象。这面镜子不是普通的镜子——它是活的。当你凝视它时，你会看到无数个自己：有的是过去的你，有的是未来的你，有的是从未存在的你。据说这面镜子来自镜中恶魔的世界，它是连接现实与倒影的通道。\n\n这面镜子是镜中恶魔在「镜之深渊」中，从自己的心脏位置取下的。他说："这面镜子让我能够穿越现实与倒影。但它也让我越来越难以分辨哪个是真实的我。"\n\n镜子的效果是：每日一次，可以释放「镜界崩塌」——指定一个 15 尺内的敌人，其被拉入镜界 1 回合（stunned，目标在镜界中与自己战斗），且受到 3D10 force（镜界崩塌）。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "mirror_demon_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_mirror_demon_trinket" }
]
```

---

### 70.10 镜中恶魔徽章（Mirror Demon Badge）

```gdscript
item_id = "acc_mirror_demon_badge"
display_name = "镜中恶魔徽章"
description = "一枚由镜面金与虚空石铸造的徽章，徽章上刻着一面镜子和一个倒影——镜中恶魔的标志。徽章背面刻着一行小字："真实是倒影，倒影是真实。"这枚徽章是镜中恶魔公会的信物，拥有它意味着你已被倒影认可。\n\n徽章的效果是：10 尺内所有友方获得 +1 AC（镜面反射）。且每日一次，可以释放「镜像领域」——10 尺内所有敌人攻击时有 50% 概率命中自己的倒影（自动 miss），持续 2 回合。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "mirror_demon_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mirror_demon_badge" },
    { attribute_id = "deception_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_mirror_demon_badge" }
]
```

---

*套装 61–70 饰品部分完结 · 共 60 件饰品装备*

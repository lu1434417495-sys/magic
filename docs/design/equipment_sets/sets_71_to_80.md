# 传奇装备套装设计文档（套装 71–80：东方 fantasy）

> 10 套东方 fantasy 主题套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×2、中甲×3、轻甲×5。

---

## 套装七十一：武士之魂（Samurai Spirit）

> *"刀不是武器，它是'灵魂的延伸'。拔出刀时，你不是在战斗，你是在展示你是谁。"*

**套装主题**：古代武士道「不屈之魂」的装备。这些武士追求极致的剑术和精神力量，他们的装备由传统的日本甲胄材料制成，赋予了穿戴者拔刀术、精神集中和致命一击的能力。集齐四件时，穿戴者获得「武士之躯」——可以在拔刀时释放强力一击、免疫恐惧、在濒死时爆发。

**历史渊源**：不屈之魂的最高剑士「剑圣」宫本武藏（另一位同名者），是一位据说可以用木刀击败任何敌人的传奇剑士。他说："我不是在练剑，我是在'修行'——剑只是修行的工具，真正的修行在心中。"

---

### 71.1 武士之魂兜帽

```gdscript
item_id = "armor_samurai_spirit_head"
display_name = "武士之魂兜帽"
description = "一顶由传统武士头盔与灵魂丝锻造而成的威严兜帽，兜帽的造型模仿古代武士的「兜」——有高耸的前立（装饰）和可开合的面颊。兜帽的内部刻有武士道的「五戒」——「不背叛、不畏惧、不贪婪、不愤怒、不死亡」。每当穿戴者违背一戒时，兜帽会微微发热作为警告。\n\n宫本武藏在锻造这顶兜帽时，将自己的「精神力」封入了前立。他说："这顶兜帽不只是头盔，它是'精神的牢笼'——囚禁着我的恐惧，让我可以无畏地战斗。"\n\n这顶兜帽的特殊效果是：穿戴者免疫 frightened（武士道精神）。且在拔刀后的第一击，攻击检定 +2（精神集中）。宫本武藏说："恐惧是剑士最大的敌人，不是对手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "samurai_spirit_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_samurai_spirit_head" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_samurai_spirit_head" }
]
```

**2件套（设计预留）**：免疫 frightened；`intimidation_bonus` +3。
**4件套（设计预留）**：拔刀后的第一击（每回合一次）伤害 ×2（居合斩）；HP 降至 25% 时，攻击检定 +3、伤害 +1D8（濒死爆发）；每日一次「精神统一」： bonus action，接下来 3 回合内免疫所有精神效果（charm、fear、illusion）且攻击检定 +2。

---

### 71.2 武士之魂板甲

```gdscript
item_id = "armor_samurai_spirit_body"
display_name = "武士之魂板甲"
description = "一副由传统武士甲胄与灵魂丝锻造而成的威严板甲，板甲的造型模仿古代武士的「大铠」——有大袖（肩甲）、笼手（护手）和臑当（胫甲）。板甲表面刻有无数的刀痕——每一道刀痕都代表一场战斗，一场胜利。板甲的颜色是深红色——不是漆，是血，无数敌人的血和穿戴者自己的血。\n\n宫本武藏在锻造这副板甲时，将自己所有战斗中的血都涂在了板甲上。他说："这副板甲不只是铠甲，它是'战历'——每一道痕迹都是一段记忆。"\n\n这副板甲的特殊效果是：穿戴者每受到一次攻击，板甲的「战意」会增加——每受到 10 点 damage，攻击检定 +1（最多 +3，持续 1 回合）。且可以将板甲的战意「释放」——每日一次，释放所有战意进行一次「必杀斩」（伤害 +本战斗受到的总伤害 ×0.5）。宫本武藏说："最好的攻击不是最强的，是最有意义的——每一次斩击都应该有理由。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "samurai_spirit_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_samurai_spirit_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_samurai_spirit_body" }
]
```

---

### 71.3 武士之魂护手

```gdscript
item_id = "armor_samurai_spirit_hands"
display_name = "武士之魂护手"
description = "一对由传统武士笼手与灵魂丝锻造而成的威严护手，护手的造型模仿古代武士的手甲——有复杂的板片结构和可活动的关节。护手的掌心有特殊的设计——不是为了保护，而是为了更好地握住刀柄。握紧拳头时，护手会微微发热，仿佛在为即将到来的斩击积蓄力量。\n\n宫本武藏在锻造这对手套时，将自己的「握力」封入了护手。他说："这对手套不只是手套，它们是'刀的延伸'——让我可以感受到刀的每一道纹理。"\n\n这对手套的特殊效果是：穿戴者握刀时，攻击检定 +1（更好的握力带来更好的控制）。且可以在拔刀时进行一次「居合」—— bonus action，拔刀并进行一次快速斩击（伤害 ×1.5，每日一次）。宫本武藏说："刀不是在手中挥舞的，是在心中挥舞的——手套只是连接心和刀的桥梁。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "samurai_spirit_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_samurai_spirit_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_samurai_spirit_hands" }
]
```

---

### 71.4 武士之魂胫甲

```gdscript
item_id = "armor_samurai_spirit_feet"
display_name = "武士之魂胫甲"
description = "一对由传统武士臑当与灵魂丝锻造而成的威严胫甲，胫甲的造型模仿古代武士的腿甲——有复杂的板片结构和可活动的关节。胫甲的底部有特殊的设计——不是为了行走，而是为了「踏稳」。在拔刀时，胫甲会让穿戴者的双脚如同扎根于地面，提供最大的稳定性。\n\n宫本武藏在锻造这对胫甲时，将自己的「步法」封入了板甲。他说："这对胫甲不只是鞋子，它们是'根'——让我在斩击时如同大树一样稳固。"\n\n这对胫甲的特殊效果是：穿戴者在攻击时免疫 prone（击倒）（稳固的步法）。且可以进行「踏步斩」——每日一次，向前踏出一步并进行一次强力斩击（移动力 +10，伤害 +2D8）。宫本武藏说："最好的斩击不是最快的，是最稳的——稳固的步法带来致命的斩击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "samurai_spirit_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_samurai_spirit_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_samurai_spirit_feet" }
]
```

---

## 套装七十二：忍者影（Ninja Shadow）

> *"忍者不是刺客，它是'信息的艺术家'。最好的忍者不是杀人的，是让你永远不知道他曾经来过。"*

**套装主题**：忍者家族「无形之叶」的装备。这些忍者擅长隐匿、潜入和信息收集，他们的装备由轻量化材料和暗影丝制成，赋予了穿戴者极致的隐匿能力和致命一击。集齐四件时，穿戴者获得「忍者之躯」——可以在阴影中完全隐形、释放手里剑风暴、使用替身术逃脱。

**历史渊源**：无形之叶的最强忍者「影之手」服部半藏（另一位同名者），是一位据说从未被任何人看见过真面目的传奇忍者。他说："我不是不存在，我是'选择性存在'——只在我想出现的时候出现。"

---

### 72.1 忍者影面罩

```gdscript
item_id = "armor_ninja_shadow_head"
display_name = "忍者影面罩"
description = "一张由暗影丝与忍者布编织而成的黑色面罩，面罩只露出双眼——但那双眼在黑暗中会发出微弱的绿光，像猫的眼睛。面罩可以完全覆盖面部和头部，有特殊的「呼吸孔」——不是为呼吸，是为了在隐匿时减少体温散失（热成像无法探测）。\n\n服部半藏在编织这张面罩时，将自己的「气息」封入了暗影丝。他说："这张面罩不只是遮挡，它是'气息的封印'——让我可以完全不散发任何气息。"\n\n这张面罩的特殊效果是：穿戴者免疫「热成像探测」和「气息追踪」（完全隐匿）。且在阴影中，隐匿检定 +5（忍者 camouflage）。服部半藏说："最好的隐匿不是看不见，是不被感知——包括视觉、听觉、嗅觉和温度。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "ninja_shadow_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ninja_shadow_head" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_ninja_shadow_head" }
]
```

**2件套（设计预留）**：`stealth_bonus` +3；`acrobatics_bonus` +3。
**4件套（设计预留）**：在 dim light 或 shadow 中完全隐形（如同 greater invisibility，但只在阴影中有效）；从隐匿状态攻击时，伤害 ×2（暗杀）；每日一次「替身术」： reaction，当受到攻击时，与一个预先放置的物体交换位置（攻击命中物体，miss）；每日一次「手里剑风暴」： bonus action，向 15 尺锥形区域投掷手里剑（3D8 piercing，目标须通过 DC16 敏捷豁免，失败则全伤并 bleeding 1 回合）。

---

### 72.2 忍者影长袍

```gdscript
item_id = "armor_ninja_shadow_body"
display_name = "忍者影长袍"
description = "一件由暗影丝与忍者布编织而成的黑色长袍，长袍表面没有任何装饰——装饰是弱点，因为装饰会反光、会发出声音、会暴露位置。长袍的内部有数十个隐藏的口袋，每个口袋都有独立的密封空间，可以存放忍具、毒药或小型工具。\n\n服部半藏在编织这件长袍时，将自己的「收藏」都编入了口袋。他说："这件长袍不只是衣服，它是'工具库'——让我在任何情况下都有工具可用。"\n\n这件长袍的特殊效果是：穿戴者可以随时从长袍中取出任何小型忍具（无需动作）。且长袍可以完全「融入」阴影——在阴影中，长袍会自动调整颜色以匹配环境（+3 AC 来自 camouflage）。服部半藏说："最好的长袍不是最华丽的，是最实用的——实用到让敌人永远不知道你有多少工具。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "ninja_shadow_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ninja_shadow_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ninja_shadow_body" }
]
```

---

### 72.3 忍者影手套

```gdscript
item_id = "armor_ninja_shadow_hands"
display_name = "忍者影手套"
description = "一对由暗影丝与忍者布编织而成的黑色手套，手套非常薄——薄到可以感受到硬币的边缘。手套的表面有特殊的纹路，可以在攀爬时增加抓握力。手套的指尖嵌有可伸缩的爪子——不是为攻击，而是为攀爬和开锁。\n\n服部半藏在编织这对手套时，将自己的「手指灵敏度」封入了暗影丝。他说："这对手套不只是手套，它们是'手的延伸'——让我可以感受到最细微的纹理。"\n\n这对手套的特殊效果是：穿戴者进行的所有开锁、扒窃和陷阱解除检定 +5。且可以用爪子进行「墙壁攀爬」——在任何表面（包括光滑的玻璃和湿滑的石头）上以正常速度攀爬。且在阴影中，手的移动完全无声（如同 pass without trace）。服部半藏说："最好的忍者不是走门的，是走墙的——墙是忍者的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "ninja_shadow_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ninja_shadow_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_ninja_shadow_hands" }
]
```

---

### 72.4 忍者影软靴

```gdscript
item_id = "armor_ninja_shadow_feet"
display_name = "忍者影软靴"
description = "一对由暗影丝与忍者布编织而成的黑色软靴，软靴的底部有特殊的「猫足垫」——模仿猫的足底结构，可以在任何地面上无声行走。软靴的鞋帮非常柔软——柔软到可以卷起来藏在腰带中。\n\n服部半藏在编织这对软靴时，将自己的「步法」封入了暗影丝。他说："这对软靴不只是鞋子，它们是'寂静的脚步'——让我可以行走在任何地方而不被听见。"\n\n这对软靴的特殊效果是：穿戴者移动时完全无声（如同 pass without trace）。且可以用「忍者跑」——在墙壁和天花板上正常行走（如同 spider climb）。且从高处跳下时，可以用「猫之翻身」减少坠落伤害（免疫 30 尺以下的 falling damage）。服部半藏说："最好的脚步不是最轻的，是最不可预测的——不可预测到连你自己都不知道下一步会踩在哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "ninja_shadow_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ninja_shadow_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_ninja_shadow_feet" }
]
```

---

## 套装七十三：少林武僧（Shaolin Monk）

> *"拳不是武器，它是'心的表达'。每一拳都是一个问题，每一脚都是一个答案。"*

**套装主题**：少林武僧「禅定之拳」的装备。这些武僧追求身心合一，他们的装备由轻便的僧袍材料和气功丝制成，赋予了穿戴者气功攻击、快速移动和精神防御的能力。集齐四件时，穿戴者获得「武僧之躯」——可以释放气功波、用内力治愈、在战斗中冥想恢复。

**历史渊源**：禅定之拳的最高大师「拳圣」达摩，是一位据说可以一拳打碎山石的传奇武僧。他说："我不是在练拳，我是在'悟道'——拳只是悟道的方式，不是目的。"

---

### 73.1 少林武僧头巾

```gdscript
item_id = "armor_shaolin_monk_head"
display_name = "少林武僧头巾"
description = "一条由僧袍布与气功丝编织而成的简朴头巾，头巾上绣有一个「禅」字——不是装饰，是提醒，提醒穿戴者保持内心的平静。头巾的正面镶嵌着一颗「气海石」——一颗能够储存和释放内力的魔法宝石。\n\n达摩在编织这条头巾时，将自己的「气」封入了气海石。他说："这条头巾不只是头巾，它是'气的容器'——储存着我的内力，让我在需要时可以释放。"\n\n这条头巾的特殊效果是：穿戴者可以「冥想」——每长休一次，可以通过冥想恢复所有 HP 和清除所有负面状态（但不能在战斗中进行）。且可以通过头巾「感知」到 30 尺内所有生物的「气」——感知对方的情绪、意图和健康状况（如同 empathy + 医疗诊断）。达摩说："气不是只能用来攻击，它也可以用来感知——感知世界，感知他人，感知自己。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "shaolin_monk_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shaolin_monk_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shaolin_monk_head" }
]
```

**2件套（设计预留）**：`insight_bonus` +3；`acrobatics_bonus` +3。
**4件套（设计预留）**：每日三次「气功波」： bonus action，向前推出一道气功波（15 尺锥形 3D8 force，目标须通过 DC16 力量豁免，失败则推后 10 尺）；每日一次「内力治愈」： bonus action，恢复 4D8 HP（消耗内力）；每日一次「禅定」： bonus action，接下来 3 回合内 AC +3 且免疫所有精神效果（进入禅定状态）；徒手攻击伤害 1D10（气功加持）。

---

### 73.2 少林武僧长袍

```gdscript
item_id = "armor_shaolin_monk_body"
display_name = "少林武僧长袍"
description = "一件由僧袍布与气功丝编织而成的简朴长袍，长袍的颜色是土黄色——不是染料，是岁月和修行的痕迹。长袍非常轻薄——薄到几乎不存在，但这正是优势，因为武僧不需要铠甲，武僧的身体就是铠甲。\n\n达摩在编织这件长袍时，将自己多年的修行都编入了丝线。他说："这件长袍不只是衣服，它是'修行的见证'——记录了我所有的汗水和血液。"\n\n这件长袍的特殊效果是：穿戴者的 AC 不受 armor 限制——使用「无甲防御」时，AC = 10 + 敏捷修正 + 智慧修正 + 2（气功护盾）。且可以通过长袍「聚气」——每回合开始时，如果上回合没有攻击，则恢复 1 HP（聚气回元）。达摩说："最好的防御不是铠甲，是'气'——气可以化为盾，化为剑，化为治愈。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "shaolin_monk_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_shaolin_monk_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_shaolin_monk_body" }
]
```

---

### 73.3 少林武僧护腕

```gdscript
item_id = "armor_shaolin_monk_hands"
display_name = "少林武僧护腕"
description = "一对由僧袍布与气功丝编织而成的简朴护腕，护腕上绣有「拳」字——提醒穿戴者，拳是心的表达。护腕非常轻薄，但内有乾坤——内部有「气脉」纹路，可以帮助穿戴者引导内力。\n\n达摩在编织这对护腕时，将自己的「拳意」封入了气脉。他说："这对护腕不只是护腕，它们是'拳的延伸'——让我可以将内力直接注入拳头。"\n\n这对护腕的特殊效果是：穿戴者的徒手攻击伤害 1D10（气功加持）。且可以用护腕「释放」气功——每日三次，用拳头释放气功冲击（15 尺射程，2D8 force，可以穿透固体障碍物如墙壁）。达摩说："拳不是只能打肉，它也可以打气——气可以穿透一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "shaolin_monk_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shaolin_monk_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shaolin_monk_hands" }
]
```

---

### 73.4 少林武僧便鞋

```gdscript
item_id = "armor_shaolin_monk_feet"
display_name = "少林武僧便鞋"
description = "一对由僧袍布与气功丝编织而成的简朴便鞋，便鞋的底部非常薄——薄到可以感受到地面的每一道纹理。这不是弱点，而是优势，因为武僧需要感受地面才能跳出最完美的步法。\n\n达摩在编织这对便鞋时，将自己的「步法」封入了气脉。他说："这对便鞋不只是鞋子，它们是'脚的延伸'——让我可以感受到大地的气脉。"\n\n这对便鞋的特殊效果是：穿戴者可以在任何表面上正常行走——包括水面（气功托体，持续 3 回合/天）。且可以用「气功跳跃」——每日三次，利用气功进行超高跳跃（跳跃距离 ×3，且可以在空中改变方向一次）。达摩说："最好的步法不是最快的，是最稳的——稳到可以在水面上行走。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "shaolin_monk_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shaolin_monk_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_shaolin_monk_feet" }
]
```

---

## 套装七十四：天狗面具（Tengu Mask）

> *"天狗不是恶魔，它是'山的精灵'。它傲慢、强大、不可预测——但它也尊重强者。"*

**套装主题**：天狗信仰「风之翼」的装备。这些信徒崇拜山中的天狗，学会了操控风力、使用太刀和释放傲慢之力。集齐四件时，穿戴者获得「天狗之躯」——可以飞行、释放狂风、用太刀释放风刃。

**历史渊源**：风之翼的最高祭司「天狗之首」鞍马山僧正，是一位据说与天狗大王达成了交易的传奇僧人。他说："我不是天狗的仆人，我是它的'对手'——天狗尊重强者，所以我必须变得更强。"

---

### 74.1 天狗面具面具

```gdscript
item_id = "armor_tengu_mask_head"
display_name = "天狗面具面具"
description = "一张由天狗羽毛与风丝编织而成的恐怖面具，面具的造型模仿天狗的「大天狗」——有长长的红鼻子、凶猛的眉毛和向上的嘴角。面具佩戴时，穿戴者会感受到天狗的「傲慢」在体内流动——傲慢不是弱点，它是力量，因为它让人无所畏惧。\n\n鞍马山僧正在锻造这张面具时，从天狗大王身上取下了一根羽毛。他说："这根羽毛不是被取下的，是'被赐予的'——天狗大王认可了我的力量，给了我它的祝福。"\n\n这张面具的特殊效果是：穿戴者免疫 frightened 和 intimidated（天狗的傲慢让人无畏）。且可以通过面具「释放」狂风——每日三次，向前喷出一道狂风（15 尺锥形，2D8 force，目标须通过 DC16 力量豁免，失败则推后 15 尺）。鞍马山僧正说："天狗的风不是自然的风，它是'傲慢的风'——一种可以吹走一切的风。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "tengu_mask_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_tengu_mask_head" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_tengu_mask_head" }
]
```

**2件套（设计预留）**：免疫 frightened；`intimidation_bonus` +3；`perception_bonus` +2。
**4件套（设计预留）**：每日一次「天狗之翼」： bonus action，背后生出风之翼，飞行 1 分钟（速度 40 尺）；每日三次「风刃斩」： melee 攻击释放风刃（15 尺线 3D8 slashing）；可以从高处滑翔（免疫 falling damage）；在山地/森林中移动力 +15（天狗的主场）。

---

### 74.2 天狗面具鳞甲

```gdscript
item_id = "armor_tengu_mask_body"
display_name = "天狗面具鳞甲"
description = "一副由天狗羽毛与风丝编织而成的威严鳞甲，鳞甲表面覆盖着类似天狗翅膀的羽毛——每一片羽毛都来自不同的天狗，因此呈现出深浅不一的黑色和红色。鳞甲非常轻——比普通鳞甲轻一半，因为风托住了它。\n\n鞍马山僧正在编织这副鳞甲时，收集了所有他遇到的天狗的羽毛。他说："这副鳞甲不只是铠甲，它是'天狗的翅膀'——让我可以分享天狗的飞行能力。"\n\n这副鳞甲的特殊效果是：穿戴者的重量减半（风的托力）。且可以从高处「滑翔」——免疫 falling damage（风之翼滑翔）。且可以将鳞甲的羽毛「释放」——每日一次，释放所有羽毛形成一个风盾（AC +2，任何 ranged 攻击有 50% 概率被风吹偏）。鞍马山僧正说："天狗的羽毛不是只能飞行，它也可以防御——用风来偏转攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "tengu_mask_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_tengu_mask_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_tengu_mask_body" }
]
```

---

### 74.3 天狗面具手套

```gdscript
item_id = "armor_tengu_mask_hands"
display_name = "天狗面具手套"
description = "一对由天狗爪子与风丝编织而成的威严手套，手套表面覆盖着类似天狗爪子的纹理。指尖嵌有可伸缩的利爪——不是为攻击，而是为在山地中攀爬和抓住东西。手套的掌心有特殊的「风穴」——可以聚集和释放风力。\n\n鞍马山僧正在编织这对手套时，将天狗的「爪力」封入了风丝。他说："这对手套不只是手套，它们是'天狗的爪子'——让我可以用天狗的方式抓取世界。"\n\n这对手套的特殊效果是：穿戴者可以用利爪进行「撕裂攻击」——徒手攻击造成 1D8 slashing。且可以用手套「聚集」风力——每日三次，释放一道风刃（30 尺射程，2D8 slashing，可以切断绳索、树枝和轻型障碍物）。鞍马山僧正说："天狗的爪子不是只能抓取，它也可以切割——用风来切割一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "tengu_mask_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_tengu_mask_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_tengu_mask_hands" }
]
```

---

### 74.4 天狗面具软靴

```gdscript
item_id = "armor_tengu_mask_feet"
display_name = "天狗面具软靴"
description = "一对由天狗爪子与风丝编织而成的威严软靴，软靴的形状不像人类的鞋子——它们更像天狗的爪子，弯曲而锋利。在山地上行走时，软靴会让穿戴者如同山羊一样灵活。\n\n鞍马山僧正在编织这对软靴时，将「天狗之步」能力封入了风丝。他说："这对软靴不只是鞋子，它们是'天狗的脚步'——让我可以像天狗一样在山地中自由移动。"\n\n这对软靴的特殊效果是：穿戴者在山地/森林中移动力 +15（天狗的主场）。且可以用「风之跳跃」——每日三次，利用风力进行超高跳跃（跳跃距离 ×3，且可以在空中滑翔一段距离）。鞍马山僧正说："天狗不是只能飞行，它也可以跳跃——只是它的跳跃看起来像是飞行。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "tengu_mask_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_tengu_mask_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_tengu_mask_feet" }
]
```

---

## 套装七十五：九尾妖狐（Nine-Tailed Fox）

> *"狐狸不是狡猾，它是'智慧的化身'。九尾不是力量，它是'经验的积累'。"*

**套装主题**：妖狐崇拜「幻梦之尾」的装备。这些崇拜者与九尾妖狐建立了契约，学会了幻术、魅惑和操控火焰。集齐四件时，穿戴者获得「妖狐之躯」——可以释放狐火、创造幻象、魅惑敌人。

**历史渊源**：幻梦之尾的最高祭司「狐之主」玉藻前（另一位同名者），是一位据说本身就是九尾妖狐化身的传奇女巫。她说："我不是人类，我不是狐狸——我是'幻'，一种可以变成任何形态的存在。"

---

### 75.1 九尾妖狐头冠

```gdscript
item_id = "armor_nine_tailed_fox_head"
display_name = "九尾妖狐头冠"
description = "一顶由狐毛与幻丝编织而成的华丽头冠，头冠上镶嵌着两颗「狐眼石」——两颗由真正妖狐的眼睛制成的宝石。头冠的背面有九条小型狐尾——不是装饰，是力量的象征，每条尾巴代表一种能力。\n\n玉藻前在编织这头冠时，从自己的尾巴上取下了九根毛发。她说："这九根毛发不是被取下的，是'被赐予的'——我将自己的力量分享给了这头冠。"\n\n这头冠的特殊效果是：穿戴者可以「魅惑」一个目标——每日三次，与一个生物进行目光接触（30 尺内），目标须通过 DC16 魅力豁免，失败则 charmed 1 分钟。且可以通过头冠「读取」一个目标的表面思想（如同 detect thoughts，但只对 charmed 目标有效）。玉藻前说："魅惑不是欺骗，它是'邀请'——邀请对方进入你的世界。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "nine_tailed_fox_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nine_tailed_fox_head" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_nine_tailed_fox_head" }
]
```

**2件套（设计预留）**：`persuasion_bonus` +3；`deception_bonus` +3。
**4件套（设计预留）**：每日三次「狐火」： bonus action，释放一团狐火（30 尺射程，2D10 fire + 1D6 radiant，狐火可以追踪目标，自动命中）；每日一次「幻象分身」：创造 1D4 个幻象（如同 mirror image）；免疫「魅惑」和「幻象」；可以「变形」—— bonus action，改变外貌（如同 alter self，持续 1 小时）。

---

### 75.2 九尾妖狐长袍

```gdscript
item_id = "armor_nine_tailed_fox_body"
display_name = "九尾妖狐长袍"
description = "一件由狐毛与幻丝编织而成的华丽长袍，长袍表面不断有九条狐尾的图案在流动。这件长袍没有固定的颜色——它会根据穿戴者的意愿变化：可以是红色（火焰）、可以是白色（幻术）、可以是金色（财富）、可以是黑色（死亡）。\n\n玉藻前在编织这件长袍时，将自己的九条尾巴的影子都编入了丝线。她说："这件长袍不只是衣服，它是'我的尾巴'——让我可以分享九尾的力量。"\n\n这件长袍的特殊效果是：穿戴者可以随时「改变」长袍的颜色和图案（被动效果，无需动作）。且可以通过长袍「释放」狐火——每日一次，释放九团狐火围绕自身（每团狐火可以独立攻击 30 尺内的目标，2D8 fire，持续 1 分钟）。玉藻前说："九尾不是只能用来攻击，它也可以用来保护——用火焰环绕自己，让敌人无法接近。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "nine_tailed_fox_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_nine_tailed_fox_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_nine_tailed_fox_body" }
]
```

---

### 75.3 九尾妖狐手套

```gdscript
item_id = "armor_nine_tailed_fox_hands"
display_name = "九尾妖狐手套"
description = "一对由狐毛与幻丝编织而成的华丽手套，手套表面覆盖着类似狐爪的纹理。指尖嵌有可伸缩的狐爪——不是为攻击，而是为幻术中的「点缀」。手套的掌心有特殊的「幻火纹」——可以聚集和释放狐火。\n\n玉藻前在编织这对手套时，将自己的「幻火之手」能力封入了幻丝。她说："这对手套不只是手套，它们是'幻火的创造者'——让我可以用手创造狐火。"\n\n这对手套的特殊效果是：穿戴者可以用手指「绘制」幻火——在空中绘制出狐火的图案（如同 prestidigitation 的火焰版，可以创造光亮、温暖和火焰效果）。且可以用手套「释放」狐火——每日三次，从掌心释放一团狐火（30 尺射程，2D10 fire + 1D6 radiant）。玉藻前说："狐火不是只能用来攻击，它也可以用来表演——用火焰绘制美丽的图案，让敌人迷失在美丽中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "nine_tailed_fox_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nine_tailed_fox_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nine_tailed_fox_hands" }
]
```

---

### 75.4 九尾妖狐便鞋

```gdscript
item_id = "armor_nine_tailed_fox_feet"
display_name = "九尾妖狐便鞋"
description = "一对由狐毛与幻丝编织而成的华丽便鞋，便鞋的底部非常柔软——软到可以感受到地面的每一道纹理。行走时，便鞋不会发出任何声音，因为妖狐的脚步本来就是无声的。\n\n玉藻前在编织这对便鞋时，将自己的「幻步」能力封入了幻丝。她说："这对便鞋不只是鞋子，它们是'幻的脚步'——让我可以像幻影一样移动。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（如同 pass without trace）。且可以用「幻步」——每日一次，化作一团幻影移动（移动力 ×3，可以穿过任何缝隙，免疫借机攻击，持续 1 回合）。且在月光下，移动力 +15（妖狐的主场）。玉藻前说："妖狐不是只能奔跑，它也可以飘——飘得像幻影一样，让人永远抓不住。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "nine_tailed_fox_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_nine_tailed_fox_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_nine_tailed_fox_feet" }
]
```

---

## 套装七十六：阴阳师（Onmyoji）

> *"阴阳不是对立，它是'平衡'。阴阳师的工作不是消灭阴或阳，是让它们和谐共存。"*

**套装主题**：阴阳师流派「两仪之衡」的装备。这些阴阳师操控阴阳之力，使用符咒和式神进行战斗，他们的装备由符咒纸和阴阳丝制成，赋予了穿戴者召唤式神、释放符咒和操控阴阳平衡的能力。集齐四件时，穿戴者获得「阴阳之躯」——可以召唤强大的式神、释放各种符咒、在阴阳之间穿梭。

**历史渊源**：两仪之衡的最高阴阳师「安倍晴明」（另一位同名者），是一位据说可以操控十二神将的传奇阴阳师。他说："我不是在操控鬼神，我是在'协商'——与它们协商，让它们帮助我。"

---

### 76.1 阴阳师头冠

```gdscript
item_id = "armor_onmyoji_head"
display_name = "阴阳师头冠"
description = "一顶由符咒纸与阴阳丝编织而成的神秘头冠，头冠上镶嵌着一颗「阴阳玉」——一颗能够平衡阴阳之力的魔法宝石。头冠的表面写满了符咒——每一个符咒代表一种能力：火符、水符、风符、雷符、治愈符、封印符。当穿戴者集中精神时，对应的符咒会发光。\n\n安倍晴明在编织这头冠时，将自己的「阴阳之眼」封入了阴阳玉。他说："这头冠不只是头冠，它是'符咒的容器'——储存着我所有的符咒，让我在需要时可以释放。"\n\n这头冠的特殊效果是：穿戴者可以「看见」阴阳之力——看到任何生物的「阴阳属性」（偏阳则发光，偏阴则暗淡，平衡则正常）。且可以通过头冠「激活」一个符咒——每日一次每种符咒：火符（30 尺射程 3D10 fire）、水符（30 尺射程 3D10 cold）、风符（15 尺锥形 3D8 force）、雷符（30 尺射程 3D10 lightning）、治愈符（恢复 3D10 HP）、封印符（目标须通过 DC16 智慧豁免，失败则不能施法 1 回合）。安倍晴明说："阴阳不是只能用来攻击，它也可以用来治愈——只是需要正确的符咒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "onmyoji_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_onmyoji_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_onmyoji_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +3；`spell_dc_bonus` +1。
**4件套（设计预留）**：每日一次「召唤式神」：召唤一个强大的式神（CR 2，持续 10 分钟，可以选择火之式神、水之式神、风之式神或雷之式神）；每日一次「阴阳转换」： bonus action，将当前受到的所有元素伤害转化为治疗（吸收并转化）；可以「绘制符咒」—— bonus action，在空中绘制符咒（效果如同对应 spell，但无需 verbal component）；免疫「诅咒」和「附身」。

---

### 76.2 阴阳师长袍

```gdscript
item_id = "armor_onmyoji_body"
display_name = "阴阳师长袍"
description = "一件由符咒纸与阴阳丝编织而成的神秘长袍，长袍表面写满了符咒——每一个符咒都在微微发光。这件长袍没有固定的颜色——它会根据当前使用的符咒变化：火符时是红色，水符时是蓝色，风符时是绿色，雷符时是紫色。\n\n安倍晴明在编织这件长袍时，将自己所有的符咒都编入了丝线。他说："这件长袍不只是衣服，它是'符咒的集合'——让我穿着所有的符咒。"\n\n这件长袍的特殊效果是：穿戴者可以随时「激活」一个符咒—— bonus action，激活长袍上的一个符咒（效果如同对应 spell，但无需 material component）。每日三次。且长袍会「保护」穿戴者——当受到元素伤害时，有 50% 概率将该伤害转化为对应元素的治疗（阴阳转换）。安倍晴明说："阴阳不是只能用来攻击，它也可以用来防御——用阴来吸收，用阳来释放。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "onmyoji_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_onmyoji_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_onmyoji_body" }
]
```

---

### 76.3 阴阳师手套

```gdscript
item_id = "armor_onmyoji_hands"
display_name = "阴阳师手套"
description = "一对由符咒纸与阴阳丝编织而成的神秘手套，手套表面写满了微型符咒。手套的指尖有特殊的「符咒墨」——可以随时随地绘制符咒。手套的掌心有「阴阳印」——可以聚集和释放阴阳之力。\n\n安倍晴明在编织这对手套时，将自己的「符咒之手」能力封入了阴阳丝。他说："这对手套不只是手套，它们是'符咒的笔'——让我可以用手指绘制任何符咒。"\n\n这对手套的特殊效果是：穿戴者可以用手指「绘制」符咒—— bonus action，在空中绘制一个符咒（效果如同对应 cantrip，但无需 verbal component）。且可以用手套「释放」阴阳之力——每日三次，从掌心释放一道阴阳射线（30 尺射程，3D8 force + 1D8 随机元素）。安倍晴明说："符咒不是只能写在纸上，它也可以写在空中——只是需要正确的工具和正确的手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "onmyoji_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_onmyoji_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_onmyoji_hands" }
]
```

---

### 76.4 阴阳师便鞋

```gdscript
item_id = "armor_onmyoji_feet"
display_name = "阴阳师便鞋"
description = "一对由符咒纸与阴阳丝编织而成的神秘便鞋，便鞋表面写满了「步罡符」——一种可以让穿戴者在空中行走的符咒。行走时，便鞋会在脚下创造出一小片「阴阳平台」——让穿戴者可以在空中短暂行走。\n\n安倍晴明在编织这对便鞋时，将「阴阳步」能力封入了符咒。他说："这对便鞋不只是鞋子，它们是'阴阳的道路'——让我可以在任何地方行走。"\n\n这对便鞋的特殊效果是：穿戴者可以在空中「行走」——每日三次，在空中创造阴阳平台（持续 3 回合，可以在空中正常行走和战斗）。且可以用「阴阳传送」——每日一次，通过阴阳之门传送至 30 尺内任意位置（如同 misty step，但无需 verbal component）。安倍晴明说："阴阳师不需要路，他可以创造路——用阴阳之力创造属于自己的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "onmyoji_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_onmyoji_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_onmyoji_feet" }
]
```

---

## 套装七十七：夜叉（Yaksha）

> *"夜叉不是恶魔，它是'守护神'。只是它的守护方式比较……直接。"*

**套装主题**：夜叉信仰「金刚之力」的装备。这些信徒崇拜夜叉的力量，学会了释放狂暴之力、用巨力战斗和承受极端伤害。集齐四件时，穿戴者获得「夜叉之躯」——可以在战斗中狂暴、用巨力粉碎敌人、免疫恐惧。

**历史渊源**：金刚之力的最高战士「夜叉王」哪吒（另一位同名者），是一位据说拥有三头六臂的传奇战士。他说："我不是在战斗，我是在'守护'——只是我的守护方式比较激烈。"

---

### 77.1 夜叉头盔

```gdscript
item_id = "armor_yaksha_head"
display_name = "夜叉头盔"
description = "一顶由夜叉骨骼与金刚丝锻造的恐怖头盔，头盔的造型模仿夜叉的面孔——有三只眼睛、尖锐的獠牙和向上的犄角。头盔的第三只眼位置镶嵌着一颗「金刚石」——一颗能够看穿幻象和隐藏的魔法宝石。\n\n哪吒在锻造这顶头盔时，从一只夜叉身上取下了它的第三只眼。他说："这只眼睛不是被取下的，是'被赐予的'——夜叉认可了我的力量，给了我它的眼睛。"\n\n这顶头盔的特殊效果是：穿戴者可以「看见」隐藏的东西——看穿 illusions、invisibility 和 etherealness（如同 truesight，但范围只有 30 尺）。且可以通过头盔「释放」怒吼——每日三次，发出一声夜叉怒吼（15 尺锥形，所有生物须通过 DC16 体质豁免，失败则 frightened 1 回合）。哪吒说："夜叉的怒吼不是恐吓，它是'警告'——警告所有人，守护神来了。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "yaksha_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_yaksha_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_yaksha_head" }
]
```

**2件套（设计预留）**：免疫 frightened；`intimidation_bonus` +3。
**4件套（设计预留）**：每日一次「夜叉狂暴」： bonus action，进入狂暴状态（力量 +4、体质 +4、AC -2、攻击检定 +2、伤害 +1D8，持续 1 分钟）；徒手攻击伤害 1D12（夜叉之力）；每日一次「金刚之力」： bonus action，下一次攻击伤害 ×3（如同暴击，但不需要骰子）；被攻击时可以用 reaction 进行一次反击（1D12 bludgeoning + 力量修正）。

---

### 77.2 夜叉板甲

```gdscript
item_id = "armor_yaksha_body"
display_name = "夜叉板甲"
description = "一副由夜叉骨骼与金刚丝锻造的恐怖板甲，板甲表面覆盖着类似夜叉皮肤的纹理——深绿色、粗糙、布满疤痕。板甲非常重——比普通板甲重一倍，因为夜叉的力量需要重量来平衡。板甲的接缝处有特殊的「金刚锁」——只有力量足够强大的人才能穿戴。\n\n哪吒在锻造这副板甲时，将夜叉的骨骼都熔入了金属。他说："这副板甲不只是铠甲，它是'夜叉的身体'——让我可以分享夜叉的力量。"\n\n这副板甲的特殊效果是：穿戴者的力量视为 +2（夜叉之力）。且可以将板甲的「金刚」释放——每日一次，释放金刚之力形成一个金刚盾（免疫所有物理伤害 1 回合，但无法移动或攻击）。哪吒说："夜叉不是只能攻击，它也可以防御——只是它的防御方式比较极端。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "yaksha_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_yaksha_body" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_yaksha_body" }
]
```

---

### 77.3 夜叉护手

```gdscript
item_id = "armor_yaksha_hands"
display_name = "夜叉护手"
description = "一对由夜叉骨骼与金刚丝锻造的恐怖护手，护手表面覆盖着类似夜叉爪子的纹理。指尖嵌有可伸缩的利爪——不是为了攻击，而是为了在狂暴时不会伤到自己。护手的内部有「金刚脉」——可以引导夜叉之力。\n\n哪吒在锻造这对手套时，将夜叉的爪子都熔入了护手。他说："这对手套不只是手套，它们是'夜叉的爪子'——让我可以用夜叉的力量战斗。"\n\n这对手套的特殊效果是：穿戴者可以用夜叉爪进行「粉碎攻击」——徒手攻击造成 1D12 bludgeoning（夜叉之力）。且可以用手套「释放」金刚冲击——每日三次，用拳头释放一道冲击波（5 尺半径，2D10 force，目标须通过 DC14 力量豁免，失败则推后 10 尺）。哪吒说："夜叉的爪子不是只能撕裂，它也可以粉碎——粉碎一切障碍。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "yaksha_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_yaksha_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_yaksha_hands" }
]
```

---

### 77.4 夜叉胫甲

```gdscript
item_id = "armor_yaksha_feet"
display_name = "夜叉胫甲"
description = "一对由夜叉骨骼与金刚丝锻造的恐怖胫甲，胫甲表面覆盖着类似夜叉蹄的纹理。每一步踏下，地面都会微微震动——不是弱点，是警告，告诉所有人：守护神来了。\n\n哪吒在锻造这对胫甲时，将夜叉的蹄子都熔入了金属。他说："这对胫甲不只是鞋子，它们是'夜叉的脚步'——让我每一步都如同地震。"\n\n这对胫甲的特殊效果是：穿戴者可以「震地」——每日一次，用力踏地引发一次小地震（15 尺半径，所有生物须通过 DC16 敏捷豁免，失败则 prone 并受到 2D10 bludgeoning）。且可以在任何地面上正常行走——包括熔岩、尖刺和沼泽（夜叉的脚步无所畏惧）。哪吒说："夜叉不需要路，它只需要目标——目标在哪里，夜叉就冲向哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "yaksha_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_yaksha_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_yaksha_feet" }
]
```

---

## 套装七十八：机关傀儡师（Karakuri Puppeteer）

> *"人不是最完美的机器，机器不是最完美的人。但结合起来，它们可以超越两者。"*

**套装主题**：机关傀儡师流派「千机之巧」的装备。这些傀儡师制造和操控各种机关傀儡，他们的装备由精密机关和傀儡丝制成，赋予了穿戴者操控傀儡、释放机关和改造身体的能力。集齐四件时，穿戴者获得「傀儡之躯」——可以召唤机关傀儡、释放各种机关、将自己部分身体改造为机关。

**历史渊源**：千机之巧的最高傀儡师「机关之心」公输班（另一位同名者），是一位据说可以制造出与真人无异的机关人的传奇工匠。他说："我不是在制造傀儡，我是在'创造生命'——只是我的创造方式比较机械。"

---

### 78.1 机关傀儡师头冠

```gdscript
item_id = "armor_karakuri_puppeteer_head"
display_name = "机关傀儡师头冠"
description = "一顶由精密机关与傀儡丝锻造而成的奇异头冠，头冠上镶嵌着一颗「核心齿轮」——一颗能够控制和协调所有机关的魔法装置。头冠的表面布满了微型机关——有自动调节的镜片、可伸缩的天线和隐藏的机关发射口。\n\n公输班在锻造这头冠时，将自己的「计算能力」封入了核心齿轮。他说："这头冠不只是头冠，它是'思考的延伸'——让我可以同时控制多个机关。"\n\n这头冠的特殊效果是：穿戴者可以「连接」一个机关傀儡——与 30 尺内的一个机关傀儡建立心灵连接，可以直接控制它的行动（如同 find familiar 的机关版）。且可以通过头冠「感知」到 60 尺内所有机关装置的位置和状态（机关雷达）。公输班说："傀儡师最强大的不是他的手，是他的脑——脑可以同时控制无数傀儡。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "karakuri_puppeteer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_karakuri_puppeteer_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_karakuri_puppeteer_head" }
]
```

**2件套（设计预留）**：`arcana_bonus` +2；`crafting_bonus` +3（机关制造）。
**4件套（设计预留）**：每日一次「召唤机关傀儡」：召唤一个机关傀儡（CR 2，持续 10 分钟，可以选择战斗型、侦察型或辅助型）；每日三次「机关发射」： bonus action，从头冠发射一个微型机关（30 尺射程：毒针 1D6 piercing + 1D6 poison、闪光弹 blinded 1 回合、烟雾弹 difficult terrain、或绳索 restrained 1 回合）；可以将自己的一只手臂改造为机关臂（徒手攻击 1D10 bludgeoning，且可以发射机关）。

---

### 78.2 机关傀儡师链甲

```gdscript
item_id = "armor_karakuri_puppeteer_body"
display_name = "机关傀儡师链甲"
description = "一副由精密机关与傀儡丝锻造而成的奇异链甲，链甲的每一块甲片都是一个小型机关——有自动修复的齿轮、可伸缩的刺针和隐藏的储物空间。链甲的接缝处有特殊的「机关锁」——只有穿戴者知道如何打开，任何试图强行打开的人都会触发机关陷阱。\n\n公输班在锻造这副链甲时，将自己所有的机关设计都编入了甲片。他说："这副链甲不只是铠甲，它是'移动的工坊'——让我在任何地方都可以制造和修理机关。"\n\n这副链甲的特殊效果是：穿戴者可以从链甲中「取出」任何小型工具或机关零件（无需动作）。且链甲会「自动修复」——每小时恢复 1 HP（微型齿轮的修复工作）。且可以将链甲的机关「释放」——每日一次，释放所有机关形成一个机关盾（AC +2，任何 melee 攻击穿戴者的生物触发随机机关：毒针、闪光、或电击 1D6 lightning）。公输班说："机关不是只能用来攻击，它也可以用来防御——只是防御的方式比较意外。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "karakuri_puppeteer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_karakuri_puppeteer_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_karakuri_puppeteer_body" }
]
```

---

### 78.3 机关傀儡师手套

```gdscript
item_id = "armor_karakuri_puppeteer_hands"
display_name = "机关傀儡师手套"
description = "一对由精密机关与傀儡丝锻造而成的奇异手套，手套的每一根手指都是一个小型机关——有可伸缩的工具、隐藏的机关发射口和精密的传感器。手套的掌心有「核心接口」——可以直接连接任何机关装置。\n\n公输班在锻造这对手套时，将自己的「机关之手」能力封入了傀儡丝。他说："这对手套不只是手套，它们是'机关的延伸'——让我可以用手指创造任何机关。"\n\n这对手套的特殊效果是：穿戴者可以用手指「制造」临时机关—— bonus action，制造一个微型机关（持续 1 分钟）：陷阱（1D8 piercing，触发时）、烟雾（5 尺半径 difficult terrain）、或闪光（blinded 1 回合）。每日三次。且可以用手套「修复」机关—— bonus action，触碰一个损坏的机关装置，恢复它的功能（恢复 2D8 HP）。公输班说："机关师最强大的不是他的工具，是他的手——手可以创造出工具无法创造的东西。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "karakuri_puppeteer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_karakuri_puppeteer_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_karakuri_puppeteer_hands" }
]
```

---

### 78.4 机关傀儡师软靴

```gdscript
item_id = "armor_karakuri_puppeteer_feet"
display_name = "机关傀儡师软靴"
description = "一对由精密机关与傀儡丝锻造而成的奇异软靴，软靴的底部有特殊的「机关垫」——可以在任何地面上保持稳定。软靴的鞋跟有「弹簧机关」——可以在需要时提供额外的弹跳力。\n\n公输班在锻造这对软靴时，将「机关之步」能力封入了傀儡丝。他说："这对软靴不只是鞋子，它们是'机关的移动'——让我可以用机关的方式移动。"\n\n这对软靴的特殊效果是：穿戴者可以在任何机关装置上正常行走（包括陷阱、齿轮和传送带，免疫 difficult terrain from machinery）。且可以用「弹簧跳跃」——每日三次，利用弹簧机关进行超高跳跃（跳跃距离 ×2，且可以在空中改变方向一次）。公输班说："机关师不需要路，他可以创造路——用机关创造属于自己的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "karakuri_puppeteer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_karakuri_puppeteer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_karakuri_puppeteer_feet" }
]
```

---

## 套装七十九：雷神鼓（Raijin Drum）

> *"雷声不是天空的愤怒，它是'节奏'——一种可以让万物随之舞动的节奏。"*

**套装主题**：雷神信仰「雷霆之鼓」的装备。这些信徒崇拜雷神，学会了用鼓声操控雷电和节奏，他们的装备由雷神鼓皮和雷电丝制成，赋予了穿戴者用节奏增强自己、释放雷电攻击和干扰敌人的能力。集齐四件时，穿戴者获得「雷神之躯」——可以用鼓声召唤雷电、用节奏增强盟友、用音波干扰敌人。

**历史渊源**：雷霆之鼓的最高祭司「雷神之手」建御雷神（另一位同名者），是一位据说可以用鼓声召唤真正雷神的传奇祭司。他说："我不是在击鼓，我是在'对话'——与雷神对话，让它回应我的节奏。"

---

### 79.1 雷神鼓头冠

```gdscript
item_id = "armor_raijin_drum_head"
display_name = "雷神鼓头冠"
description = "一顶由雷神鼓皮与雷电丝编织而成的威严头冠，头冠的造型模仿雷神的「雷鼓」——有六个小型鼓面环绕头部，每个鼓面都可以发出不同的音调。头冠佩戴时，鼓面会随穿戴者的心跳自动敲击，形成一种持续的节奏。\n\n建御雷神在编织这头冠时，从真正的雷神鼓上取下了一块鼓皮。他说："这块鼓皮不是被取下的，是'被赐予的'——雷神认可了我的节奏，给了我它的鼓皮。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 60 尺内所有生物的心跳（通过鼓面的共鸣，如同生命感知）。且可以通过头冠「释放」雷电——每日三次，敲击一个鼓面释放一道雷电（30 尺射程，2D10 lightning，目标须通过 DC16 敏捷豁免，失败则 stunned 1 回合）。建御雷神说："雷神不是只能听见大声音，它也听见小节奏——心跳就是最古老的节奏。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "raijin_drum_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_raijin_drum_head" },
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_raijin_drum_head" }
]
```

**2件套（设计预留）**：`performance_bonus` +3；`resistance_lightning` +10。
**4件套（设计预留）**：每日一次「雷神之鼓」： bonus action，敲击鼓面召唤雷神（15 尺半径内所有敌人受到 4D10 lightning，须通过 DC16 敏捷豁免，失败则 stunned 1 回合）；每日三次「节奏增强」： bonus action，敲击鼓面增强一个盟友（攻击检定 +2、移动力 +10，持续 1 回合）；可以用鼓声「干扰」敌人——15 尺内所有敌人攻击检定 -1（节奏干扰）；免疫「deafened」（雷神的耳朵已经习惯了巨响）。

---

### 79.2 雷神鼓鳞甲

```gdscript
item_id = "armor_raijin_drum_body"
display_name = "雷神鼓鳞甲"
description = "一副由雷神鼓皮与雷电丝编织而成的威严鳞甲，鳞甲表面覆盖着类似鼓皮的纹理——每一片鳞片都来自不同的雷神鼓，因此呈现出深浅不一的棕色和金色。鳞甲的接缝处不断有小型鼓面在振动——它们是鳞甲的一部分，会随穿戴者的动作发出节奏。\n\n建御雷神在编织这副鳞甲时，收集了所有他遇到的雷神鼓的鼓皮。他说："这副鳞甲不只是铠甲，它是'雷神鼓的集合'——让我穿着所有的雷神鼓。"\n\n这副鳞甲的特殊效果是：穿戴者可以将鳞甲的鼓面「释放」——每日一次，释放所有鼓面形成一个雷鼓阵（15 尺半径，所有敌人每回合受到 2D6 lightning 并被 deafened，持续 1 分钟）。且鳞甲会「共鸣」——当附近有雷电时，鳞甲的 AC +2（雷电共鸣）。建御雷神说："雷神鼓不是只能用来召唤，它也可以用来防御——用雷电来偏转攻击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "raijin_drum_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_raijin_drum_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_raijin_drum_body" }
]
```

---

### 79.3 雷神鼓手套

```gdscript
item_id = "armor_raijin_drum_hands"
display_name = "雷神鼓手套"
description = "一对由雷神鼓皮与雷电丝编织而成的威严手套，手套的掌心有特殊的「鼓面」——可以随时随地敲击。手套的指尖有「雷电纹」——可以在敲击时释放雷电。\n\n建御雷神在编织这对手套时，将自己的「鼓手之魂」封入了雷电丝。他说："这对手套不只是手套，它们是'雷神鼓的延伸'——让我可以用手敲击任何表面，召唤雷电。"\n\n这对手套的特殊效果是：穿戴者可以用任何表面「击鼓」—— bonus action，敲击一个表面释放雷电（15 尺锥形 2D8 lightning，目标须通过 DC14 敏捷豁免，失败则 stunned 1 回合）。每日三次。且可以用手套「增强」节奏—— bonus action，为 30 尺内的一个盟友加速（额外一个 bonus action，持续 1 回合）。建御雷神说："雷神鼓不是只能用来攻击，它也可以用来加速——用节奏来加速心跳，加速行动。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "raijin_drum_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raijin_drum_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raijin_drum_hands" }
]
```

---

### 79.4 雷神鼓软靴

```gdscript
item_id = "armor_raijin_drum_feet"
display_name = "雷神鼓软靴"
description = "一对由雷神鼓皮与雷电丝编织而成的威严软靴，软靴的底部有特殊的「鼓面」——每一步踏下都会发出轻微的鼓声。这不是弱点，是节奏，是力量的来源。在战斗中，穿戴者可以通过脚步的节奏来控制雷电的释放。\n\n建御雷神在编织这对软靴时，将「雷神之步」能力封入了雷电丝。他说："这对软靴不只是鞋子，它们是'雷神鼓的延伸'——让我可以用脚步敲击大地，召唤雷电。"\n\n这对软靴的特殊效果是：穿戴者的每一步都会产生「雷电共鸣」——每走一步，下一个 lightning damage +1（最多 +5，持续 1 回合）。且可以进行「雷电踏」——每日一次，用力踏地引发一次雷电爆发（15 尺半径，所有生物受到 3D10 lightning，须通过 DC16 敏捷豁免，失败则 stunned 1 回合）。建御雷神说："雷神不需要手，它可以用脚——用脚敲击大地，大地会回应雷电。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "raijin_drum_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_raijin_drum_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_raijin_drum_feet" }
]
```

---

## 套装八十：河童之甲（Kappa Armor）

> *"河童不是怪物，它是'水的精灵'。它狡猾、淘气、爱恶作剧——但它也遵守承诺。"*

**套装主题**：河童信仰「水之顶」的装备。这些信徒崇拜河童，学会了操控水力、在水中战斗和用头顶的「水盘」储存力量。集齐四件时，穿戴者获得「河童之躯」——可以在水中自由战斗、用头顶的水盘释放力量、在水中快速恢复。

**历史渊源**：水之顶的最高祭司「河童之首」川太郎，是一位据说与河童大王成为了朋友的传奇渔夫。他说："我不是河童的主人，我是它的'朋友'——朋友之间互相尊重，互相帮忙。"

---

### 80.1 河童之甲头冠

```gdscript
item_id = "armor_kappa_armor_head"
display_name = "河童之甲头冠"
description = "一顶由河童皮肤与水丝编织而成的奇异头冠，头冠的造型模仿河童的头顶——有一个凹陷的「水盘」。水盘中必须始终保持有水——如果水盘干涸，头冠会失去所有能力。水盘中的水不是普通的水，是「河童之水」，可以在任何环境中保持不蒸发。\n\n川太郎在编织这头冠时，从河童大王身上取下了一滴水。他说："这滴水不是被取下的，是'被赐予的'——河童大王给了我它的力量之源。"\n\n这头冠的特殊效果是：穿戴者可以「感知」到 60 尺内所有水源的位置和状态（水源雷达）。且可以通过头冠「吸收」水——受到 water-based damage 时，50% 概率将伤害转化为 HP（水盘充能）。但缺点是，如果水盘干涸（例如被火烤干），所有能力失效直到重新注水。川太郎说："河童的力量来自头顶的水盘，我的力量也来自头顶的水盘——永远不要让它干涸。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "kappa_armor_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_kappa_armor_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_kappa_armor_head" }
]
```

**2件套（设计预留）**：水下呼吸；游泳速度 = 步行速度 +15。
**4件套（设计预留）**：每日三次「水球术」： bonus action，从水盘释放一个水球（30 尺射程，3D10 bludgeoning，目标须通过 DC16 力量豁免，失败则推后 10 尺）；在水中每小时恢复 2 HP（河童之水的治愈）；每日一次「河童之力」： bonus action，从水盘释放所有力量（力量 +4，持续 1 分钟，但水盘干涸后能力失效直到重新注水）；可以用「河童鞠躬」——向一个目标鞠躬，目标必须回礼（须通过 DC14 魅力豁免，失败则浪费一个动作回礼）。

---

### 80.2 河童之甲鳞甲

```gdscript
item_id = "armor_kappa_armor_body"
display_name = "河童之甲鳞甲"
description = "一副由河童皮肤与水丝编织而成的奇异鳞甲，鳞甲表面覆盖着类似河童皮肤的纹理——绿色、滑腻、布满黏液。鳞甲非常轻——比普通鳞甲轻一半，因为水的浮力托住了它。鳞甲的接缝处不断有小型水流在流动——它们是鳞甲的一部分，会保持鳞甲的湿润。\n\n川太郎在编织这副鳞甲时，收集了所有他遇到的河童的皮肤碎片。他说："这副鳞甲不只是铠甲，它是'河童的皮肤'——让我可以分享河童的水性。"\n\n这副鳞甲的特殊效果是：穿戴者在水中 AC +2（水的护盾）。且可以在水中「快速恢复」——每回合恢复 1 HP（河童之水的治愈）。且可以将鳞甲的黏液「释放」——每日一次，释放所有黏液形成一个黏液盾（任何 melee 攻击穿戴者的生物受到 1D6 acid 并被 grappled 1 回合，因为黏液粘住了武器）。川太郎说："河童的黏液不是脏，它是'防御'——一种让敌人无法靠近的防御。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "kappa_armor_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_kappa_armor_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_kappa_armor_body" }
]
```

---

### 80.3 河童之甲手套

```gdscript
item_id = "armor_kappa_armor_hands"
display_name = "河童之甲手套"
description = "一对由河童皮肤与水丝编织而成的奇异手套，手套表面覆盖着类似河童手掌的纹理——有蹼状的连接，可以在水中更好地划水。手套的掌心有「水穴」——可以吸收和释放水。\n\n川太郎在编织这对手套时，将河童的「水之触」能力封入了水丝。他说："这对手套不只是手套，它们是'河童的手'——让我可以用河童的方式操控水。"\n\n这对手套的特殊效果是：穿戴者可以在水中以三倍速度游泳（蹼的效果）。且可以用手套「吸收」水—— bonus action，吸收周围的水（15 尺半径内的积水消失，恢复 1D10 HP）。每日三次。且可以用手套「释放」水—— bonus action，向前推出一道水流（15 尺锥形 2D8 bludgeoning，目标须通过 DC14 力量豁免，失败则推后 10 尺）。川太郎说："河童的手不是只能游泳，它也可以操控——操控水流，操控战场的地形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "kappa_armor_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_kappa_armor_hands" },
    { attribute_id = "athletics_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_kappa_armor_hands" }
]
```

---

### 80.4 河童之甲软靴

```gdscript
item_id = "armor_kappa_armor_feet"
display_name = "河童之甲软靴"
description = "一对由河童皮肤与水丝编织而成的奇异软靴，软靴的形状不像人类的鞋子——它们更像河童的脚，宽大而扁平，有蹼状的连接。在陆地上行走时，软靴会很笨拙（移动力 -5），但在水中，它们是完美的游泳工具。\n\n川太郎在编织这对软靴时，从河童大王身上取下了它的脚蹼。他说："这对脚蹼不是被取下的，是'被赐予的'——河童大王给了我它的游泳之力。"\n\n这对软靴的特殊效果是：在水中，穿戴者的游泳速度 = 步行速度 +20（蹼的效果）。且可以用「河童冲刺」——每日一次，在水中以三倍速度直线冲刺（路径上所有生物受到 2D8 bludgeoning 并被推后 15 尺）。且可以在湿滑的表面上正常行走（河童的脚不怕滑）。川太郎说："河童不是只能游泳，它也可以奔跑——只是在水中奔跑比在陆地上更快。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "kappa_armor_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_kappa_armor_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = -5, source_type = "equipment", source_id = "armor_kappa_armor_feet" }
]
```

---

*东方 fantasy 主题套装 71–80 完结 · 共 40 件护甲装备（重甲 8 + 中甲 16 + 轻甲 16）*

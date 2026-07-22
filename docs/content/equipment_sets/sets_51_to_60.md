# 传奇装备套装设计文档（套装 51–60：神话与传说）

> 10 套以神话传说为灵感的套装，共 40 件护甲装备，覆盖 head / body / hands / feet。包含重甲×3、中甲×3、轻甲×4。

---

## 套装五十一：雷神之锤（Thunder God's Armor）

> *"雷声不是天空的愤怒，它是天空的笑声——只是凡人不懂得欣赏。"*

**套装主题**：北欧雷神托尔的追随者「雷霆之誓」的装备。这些狂战士崇拜雷电的力量，他们的装备由雷云金属锻造，在雷暴天气中无比强大。集齐四件时，穿戴者获得「雷神之躯」——可以召唤雷电、免疫电击、在雷暴中力量翻倍。

**历史渊源**：雷霆之誓的创始人「托尔之手」托尔芬，是一位在雷暴中被闪电击中七次的狂战士——他没有死，反而获得了雷电的力量。他说："闪电不是想杀死我，它是在'选择'我——就像托尔选择他的追随者一样。"

---

### 51.1 雷神之锤头盔

```gdscript
item_id = "armor_thunder_god_head"
display_name = "雷神之锤头盔"
description = "一顶由雷云金属与闪电丝锻造的威严头盔，头盔正面有一道闪电状的裂缝——那不是损伤，而是「闪电通道」，让穿戴者可以直接吸收雷电。头盔顶部有两只向上弯曲的金属角——模仿雷神的头盔，但不是为了装饰，而是为了引导雷电。\n\n托尔芬在锻造这顶头盔时，在雷暴中站在山顶，让闪电直接击中头盔。他说："闪电不是被捕捉的，是'被邀请的'——我邀请它进入我的装备，它给了我力量。"\n\n这顶头盔的特殊效果是：在雷暴中，穿戴者的视野不受雨水影响（闪电让雨水透明化）。且可以「吸收」雷电——受到 lightning damage 时，50% 概率将伤害转化为 HP（雷电充能）。托尔芬说："雷电不是我的敌人，是我的食物。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "thunder_god_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 21000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_thunder_god_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_thunder_god_head" }
]
```

**2件套（设计预留）**：`resistance_lightning` +15；雷暴中 `strength_bonus` +1。
**4件套（设计预留）**：每日三次「召唤雷电」：60 尺内一个目标受到 3D10 lightning（目标须通过 DC16 敏捷豁免，失败则全伤并 stunned 1 回合）；免疫 lightning damage（吸收并储存）；雷暴中 melee 攻击附加 1D8 lightning；每日一次「雷神之怒」：15 尺半径内所有生物受到 4D10 lightning，须通过 DC16 体质豁免，失败则 paralyzed 1 回合。

---

### 51.2 雷神之锤板甲

```gdscript
item_id = "armor_thunder_god_body"
display_name = "雷神之锤板甲"
description = "一副由雷云金属与闪电丝锻造的威严板甲，板甲表面不断有微弱的电光在流动。在雷暴中，板甲会发出耀眼的蓝光，让穿戴者看起来如同雷电的化身。板甲的接缝处有特殊的绝缘层——不是为了保护自己，而是为了保护别人（穿戴者拥抱雷电，但不想伤害盟友）。\n\n托尔芬在锻造这副板甲时，将一道真正的闪电封入了板甲的核心。他说："这副板甲不只是铠甲，它是'闪电的容器'——储存着雷电的力量，随时准备释放。"\n\n这副板甲的特殊效果是：穿戴者可以将吸收的雷电「储存」起来（最多储存 30 点雷电能量）。储存的雷电可以在需要时释放——每 5 点雷电能量可以释放一次「闪电打击」（30 尺射程，1D10 lightning）。托尔芬说："雷电不是一次性使用的，它是货币——可以储蓄，可以投资，可以消费。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "thunder_god_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 36000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_thunder_god_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_thunder_god_body" }
]
```

---

### 51.3 雷神之锤护手

```gdscript
item_id = "armor_thunder_god_hands"
display_name = "雷神之锤护手"
description = "一对由雷云金属与闪电丝锻造的威严护手，护手表面不断有电火花在跳跃。握紧拳头时，电火花会汇聚在拳峰，形成两颗小型的「雷电球」。松开拳头时，雷电球会消散，但电火花会继续跳跃。\n\n托尔芬在锻造这对手套时，将自己的双手改造成了「雷电导体」。他说："这对手套不只是手套，它们是'雷电的延伸'——让我可以用手释放雷电。"\n\n这对手套的特殊效果是：穿戴者可以用拳头进行「雷电打击」——徒手攻击造成 1D8 bludgeoning + 1D8 lightning。且可以用手掌「释放」一道「闪电链」（30 尺射程，2D10 lightning，可以弹跳至另一个 15 尺内的目标，每次弹跳伤害减半，最多 3 次）。每日一次。托尔芬说："雷电不是直线前进的，它会找到最快的路径——就像智慧一样。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "thunder_god_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_thunder_god_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_thunder_god_hands" }
]
```

---

### 51.4 雷神之锤胫甲

```gdscript
item_id = "armor_thunder_god_feet"
display_name = "雷神之锤胫甲"
description = "一对由雷云金属与闪电丝锻造的威严胫甲，胫甲表面刻有雷电的图案。每一步踏下，图案会微微发光，在地面上留下短暂的电光足迹——不是弱点，是警告，告诉所有人：雷电正在接近。\n\n托尔芬在锻造这对胫甲时，将「雷电之步」能力封入了金属。他说："这对胫甲不只是鞋子，它们是'雷电的道路'——让我每一步都伴随着雷鸣。"\n\n这对胫甲的特殊效果是：穿戴者在雷暴中移动力 +15（雷电加速）。且可以进行「雷电冲锋」——每日一次，以三倍速度直线冲锋，路径上所有生物受到 2D10 lightning 并被推后 10 尺。托尔芬说："雷电不会绕路，它直接穿过一切。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "thunder_god_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_thunder_god_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_thunder_god_feet" }
]
```

---

## 套装五十二：美杜莎凝视（Medusa's Gaze）

> *"不要看我。如果你看了，你就永远不能再移开眼睛——不是因为魔法，是因为美丽。"*

**套装主题**：蛇发女妖美杜莎的追随者「石化之眼」的装备。这些战士学会了操控目光和石化的力量，他们的装备由石化生物的碎片和蛇鳞制成。集齐四件时，穿戴者获得「石化之躯」——可以用目光石化敌人、免疫石化、释放蛇群攻击。

**历史渊源**：石化之眼的创始人「第二美杜莎」斯忒诺，是一位据说与美杜莎有血缘关系的蛇发女妖。她说："我不是美杜莎，我是她的'姐妹'——我们共享同一种诅咒，也共享同一种力量。"

---

### 52.1 美杜莎凝视头冠

```gdscript
item_id = "armor_medusa_gaze_head"
display_name = "美杜莎凝视头冠"
description = "一顶由石化生物碎片与蛇鳞编织而成的恐怖头冠，头冠上缠绕着七条活蛇——它们不是装饰，它们是武器。每条蛇都有自己的意志，会攻击任何试图从头冠后方接近穿戴者的敌人。头冠的正面镶嵌着一颗「石化之眼」——一颗由真正的美杜莎眼睛制成的宝石。\n\n斯忒诺在编织这头冠时，从美杜莎的遗骸中取下了她的眼睛。她说："这只眼睛不是被取下的，是'被继承的'——美杜莎选择了我，让我继续她的力量。"\n\n这头冠的特殊效果是：穿戴者可以「石化凝视」——每日一次，与一个生物进行目光接触（30 尺内），目标须通过 DC16 体质豁免，失败则 slowed 1 回合（部分石化），第二次失败则 petrified（完全石化）。且头冠上的蛇会「自动防御」——任何从后方接近穿戴者的生物都会受到蛇咬攻击（1D6 piercing + 1D6 poison）。斯忒诺说："不要看我，除非你想永远成为我的收藏。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "medusa_gaze_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_medusa_gaze_head" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_medusa_gaze_head" }
]
```

**2件套（设计预留）**：免疫 petrified 和 poisoned；`intimidation_bonus` +3。
**4件套（设计预留）**：每日两次「石化凝视」（DC16 体质豁免）；近战攻击附加 1D6 poison（蛇毒）；每日一次「召唤蛇群」：15 尺半径内涌出蛇群（困难地形，所有生物每回合受到 1D6 poison，持续 1 分钟）；头冠上的蛇可以进行 opportunity attack（1D6 piercing + 1D6 poison）。

---

### 52.2 美杜莎凝视板甲

```gdscript
item_id = "armor_medusa_gaze_body"
display_name = "美杜莎凝视板甲"
description = "一副由石化生物碎片与蛇鳞锻造的恐怖板甲，板甲表面覆盖着类似蛇鳞的纹理——每一片鳞片都来自不同的蛇，因此呈现出深浅不一的绿色和金色。板甲的接缝处不断有小型蛇在游走——它们是板甲的一部分，会修复任何损伤。\n\n斯忒诺在锻造这副板甲时，将所有她收集的蛇鳞都缝入了板甲。她说："这副板甲不只是铠甲，它是'蛇巢'——让我永远被蛇保护着。"\n\n这副板甲的特殊效果是：任何 melee 攻击板甲的攻击者都会受到 1D6 poison 反伤（触碰蛇鳞）。且板甲上的蛇会「自动修复」——每小时恢复 1 HP（蛇的唾液有愈合作用）。且可以将板甲上的蛇「释放」——每日一次，释放所有蛇形成一个蛇盾（AC +3，持续 1 回合，任何攻击穿戴者的生物都会受到蛇咬）。斯忒诺说："蛇不是宠物，它们是'家人'——永远不要背叛家人。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "medusa_gaze_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 37000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_medusa_gaze_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_medusa_gaze_body" }
]
```

---

### 52.3 美杜莎凝视护手

```gdscript
item_id = "armor_medusa_gaze_hands"
display_name = "美杜莎凝视护手"
description = "一对由石化生物碎片与蛇鳞锻造的恐怖护手，护手表面缠绕着两条活蛇——它们会沿着手臂游动，在需要时攻击。手套的指尖有可伸缩的蛇牙——不是为攻击，而是为精确注射毒液。\n\n斯忒诺在锻造这对手套时，将自己的指甲改造成了蛇牙。她说："这对手套不只是手套，它们是'毒牙'——让我可以用触碰注入死亡。"\n\n这对手套的特殊效果是：穿戴者可以用蛇牙进行「精确毒击」—— bonus action，触碰一个目标，注入毒液（目标须通过 DC16 体质豁免，失败则 poisoned 1 分钟，每回合受到 2D6 poison）。每日两次。且可以用手套「石化触摸」——触碰一个无生命物体，将其石化（石头变石头，木头变石头，金属变石头）。每日一次。斯忒诺说："石化不是死亡，它是'永恒'——让你的敌人永远留在最美的瞬间。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "medusa_gaze_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_medusa_gaze_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_medusa_gaze_hands" }
]
```

---

### 52.4 美杜莎凝视胫甲

```gdscript
item_id = "armor_medusa_gaze_feet"
display_name = "美杜莎凝视胫甲"
description = "一对由石化生物碎片与蛇鳞锻造的恐怖胫甲，胫甲表面不断有小型蛇在游走。每一步踏下，蛇会在地面上留下蜿蜒的痕迹——不是弱点，是标记，告诉所有人：蛇正在移动。\n\n斯忒诺在锻造这对胫甲时，将「蛇之步」能力封入了鳞片。她说："这对胫甲不只是鞋子，它们是'蛇的延伸'——让我可以像蛇一样移动。"\n\n这对胫甲的特殊效果是：穿戴者可以「蛇行」——在狭窄空间中正常移动（管道、缝隙、通风口，速度不减）。且可以用「蛇之缠绕」——每日一次，用腿部的蛇缠绕一个 5 尺内的目标（目标须通过 DC16 力量豁免，失败则 restrained 1 分钟）。斯忒诺说："蛇不是最快的，但它是最耐心的——它会等到最好的时机才出击。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "medusa_gaze_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_medusa_gaze_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_medusa_gaze_feet" }
]
```

---

## 套装五十三：凤凰涅槃（Phoenix Rebirth）

> *"死亡不是结束，它是'准备'——准备下一次更华丽的登场。"*

**套装主题**：凤凰崇拜者教团「不灭之火」的装备。这个教团的成员崇拜凤凰的轮回之力，他们的装备由凤凰羽毛和余烬锻造，赋予了穿戴者火焰力量和不死重生的能力。集齐四件时，穿戴者获得「凤凰之躯」——可以释放火焰、在死亡时重生、治愈盟友。

**历史渊源**：不灭之火的最高祭司「凤凰之子」菲尼克斯，是一位据说被凤凰之火烧死后重生的传奇战士。他说："我不是死过一次，我是'死了很多次'——每次死亡都让我更强大。"

---

### 53.1 凤凰涅槃头冠

```gdscript
item_id = "armor_phoenix_rebirth_head"
display_name = "凤凰涅槃头冠"
description = "一顶由凤凰羽毛与余烬丝编织而成的辉煌头冠，头冠上镶嵌着一颗「凤凰之心」——一颗能够储存火焰并在死亡时释放的魔法宝石。头冠佩戴时，周围的温度会略微升高，仿佛穿戴者体内有一团火在燃烧。\n\n菲尼克斯在编织这头冠时，从一只真正的凤凰身上取下了三根尾羽。他说："凤凰不是普通的鸟，它是'永恒的火'——它死了，但它总会回来。"\n\n这头冠的特殊效果是：穿戴者免疫 fire damage（凤凰之火不会伤害自己的信徒）。且可以在死亡时触发「涅槃」——HP 降至 0 时，有 25% 概率不死亡，而是恢复至 25% 最大 HP 并释放一次「火焰爆发」（15 尺半径 3D10 fire，每场战斗一次）。菲尼克斯说："死亡不是终点，它只是'中场休息'。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "heavy_armor", "phoenix_rebirth_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_phoenix_rebirth_head" },
    { attribute_id = "resistance_fire", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_phoenix_rebirth_head" }
]
```

**2件套（设计预留）**：免疫 fire damage；`resistance_cold` +10（凤凰之火抵抗寒冷）。
**4件套（设计预留）**：每日三次「火焰之翼」：30 尺内一个目标受到 3D10 fire；每日一次「治愈之火」：30 尺内一个盟友恢复 3D10 HP（凤凰之火的治愈力量）；HP 降至 0 时「涅槃」概率提升至 50%（恢复 50% 最大 HP）；火焰 aura（10 尺内所有敌人每回合受到 1D6 fire）。

---

### 53.2 凤凰涅槃板甲

```gdscript
item_id = "armor_phoenix_rebirth_body"
display_name = "凤凰涅槃板甲"
description = "一副由凤凰羽毛与余烬丝编织而成的辉煌板甲，板甲表面不断有微弱的火焰在流动。在战斗中，火焰会变得更加旺盛——燃烧的越猛烈，板甲的防御力越高。板甲的颜色是火红色，但在涅槃后会变成金色（象征重生）。\n\n菲尼克斯在锻造这副板甲时，将凤凰涅槃时的余烬都收集了起来。他说："这副板甲不只是铠甲，它是'重生的见证'——每一次火焰的燃烧都是一次涅槃。"\n\n这副板甲的特殊效果是：穿戴者受到 fire damage 时，板甲会「燃烧」——AC +1（最多叠加至 +3），持续 1 回合。且可以将板甲的火焰「释放」——每日一次，释放所有火焰形成一个火盾（AC +3，任何 melee 攻击穿戴者的生物受到 2D6 fire，持续 1 分钟）。菲尼克斯说："火焰不是毁灭，它是'转化'——将旧的东西烧掉，为新的东西腾出空间。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "heavy_armor", "phoenix_rebirth_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 37000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_phoenix_rebirth_body" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_phoenix_rebirth_body" }
]
```

---

### 53.3 凤凰涅槃护手

```gdscript
item_id = "armor_phoenix_rebirth_hands"
display_name = "凤凰涅槃护手"
description = "一对由凤凰羽毛与余烬丝编织而成的辉煌护手，护手表面不断有火焰在跳跃。握紧拳头时，火焰会汇聚在拳峰，形成两颗小型的「火球」。松开拳头时，火球会消散，但火焰会继续跳跃。\n\n菲尼克斯在锻造这对手套时，将自己的双手变成了「火焰导体」。他说："这对手套不只是手套，它们是'凤凰的爪子'——让我可以用手释放火焰。"\n\n这对手套的特殊效果是：穿戴者可以用拳头进行「火焰打击」——徒手攻击造成 1D8 bludgeoning + 1D10 fire。且可以用手掌「治愈之火」——触碰一个盟友，恢复 2D10 HP（每日三次）。菲尼克斯说："凤凰之火不是只能毁灭，它也能治愈——就像太阳，它既能烧伤，也能温暖。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "heavy_armor", "phoenix_rebirth_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phoenix_rebirth_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phoenix_rebirth_hands" }
]
```

---

### 53.4 凤凰涅槃胫甲

```gdscript
item_id = "armor_phoenix_rebirth_feet"
display_name = "凤凰涅槃胫甲"
description = "一对由凤凰羽毛与余烬丝编织而成的辉煌胫甲，胫甲表面不断有火焰在流动。每一步踏下，火焰会在地面上留下短暂的燃烧足迹——不是弱点，是标记，也是陷阱（任何踩到足迹的生物受到 1D6 fire）。\n\n菲尼克斯在锻造这对胫甲时，将「火焰之步」能力封入了羽毛。他说："这对胫甲不只是鞋子，它们是'火焰的道路'——让我每一步都留下燃烧的痕迹。"\n\n这对胫甲的特殊效果是：穿戴者可以在火焰上正常行走——包括熔岩、燃烧的建筑和火墙（免疫 fire 的前提）。且可以进行「火焰冲锋」——每日一次，以三倍速度直线冲锋，路径上留下火焰轨迹（轨迹持续 1 回合，任何踩到的生物受到 2D6 fire）。菲尼克斯说："凤凰不需要路，它只需要天空——但在地面上，火焰就是它的路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "heavy_armor", "phoenix_rebirth_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_phoenix_rebirth_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_phoenix_rebirth_feet" }
]
```

---

## 套装五十四：海妖之歌（Siren's Song）

> *"歌声不是声音，它是'绳索'——用来捆绑那些愿意被捆绑的灵魂。"*

**套装主题**：海妖姐妹会的装备。这个姐妹会的成员用歌声操控 sailors 的心智，她们的装备赋予了穿戴者用声音操控情绪和释放音波攻击的能力。集齐四件时，穿戴者获得「海妖之躯」——可以用歌声魅惑敌人、释放致命音波、在水下自由歌唱。

**历史渊源**：海妖姐妹会的首领「第一海妖」塞壬（另一位同名者），是一位据说歌声可以让石头流泪的传奇歌者。她说："我的歌声不是为了娱乐，它是'武器'——一种能够穿透灵魂、控制意志的武器。"

---

### 54.1 海妖之歌头冠

```gdscript
item_id = "armor_siren_song_head"
display_name = "海妖之歌头冠"
description = "一顶由海螺与音波丝编织而成的优雅头冠，头冠上镶嵌着一颗「海妖之泪」——一颗能够放大声音并赋予其魔法力量的珍珠。头冠佩戴时，穿戴者的声音会变得异常悦耳——不是魔法，是物理现象，因为珍珠会共振并美化声音。\n\n塞壬在编织这头冠时，将自己的第一滴眼泪封入了珍珠。她说："这滴眼泪不是悲伤的，是'力量的结晶'——当我的歌声打动了海洋本身时，海洋回赠了我这颗珍珠。"\n\n这头冠的特殊效果是：穿戴者的声音范围翻倍（正常 60 尺的声音可以传到 120 尺）。且可以通过头冠「魅惑」一个听到歌声的目标（目标须通过 DC16 魅力豁免，失败则 charmed 1 分钟，或直到被攻击）。每日三次。塞壬说："最好的武器不是刀剑，是声音——因为声音可以穿透铠甲，直击心灵。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "siren_song_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_siren_song_head" },
    { attribute_id = "performance_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_siren_song_head" }
]
```

**2件套（设计预留）**：`performance_bonus` +3；`persuasion_bonus` +3。
**4件套（设计预留）**：每日三次「魅惑之歌」：30 尺内所有听到歌声的生物须通过 DC16 魅力豁免，失败则 charmed 1 回合；每日一次「致命音波」：15 尺锥形 4D10 thunder，目标须通过 DC16 体质豁免，失败则 deafened 1 分钟并 stunned 1 回合；水下声音可以正常传播（如同 air）；免疫「声音操控」和「魅惑」。

---

### 54.2 海妖之歌鳞甲

```gdscript
item_id = "armor_siren_song_body"
display_name = "海妖之歌鳞甲"
description = "一副由鱼鳞与音波丝编织而成的优雅鳞甲，鳞甲表面有类似鱼鳞的纹理——每一片鳞片都来自不同的鱼，因此呈现出彩虹般的色彩。鳞甲的接缝处不断有小型气泡在产生和消失——那是海妖的呼吸，鳞甲让穿戴者可以在水下自由呼吸。\n\n塞壬在编织这副鳞甲时，收集了海洋中所有最美丽的鱼鳞。她说："这副鳞甲不只是防御，它是'海洋的赞美'——让我穿着海洋的美丽。"\n\n这副鳞甲的特殊效果是：穿戴者可以在水下呼吸（如同 water breathing）。且鳞甲可以减少水流阻力——游泳速度 = 步行速度 +10。且可以通过鳞甲「释放」音波——每日一次，释放一次强烈的音波（15 尺半径 3D10 thunder，所有生物须通过 DC16 体质豁免，失败则 deafened 1 回合）。塞壬说："海洋不是安静的，它充满了声音——只是大多数人听不见。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "siren_song_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_siren_song_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_siren_song_body" }
]
```

---

### 54.3 海妖之歌手套

```gdscript
item_id = "armor_siren_song_hands"
display_name = "海妖之歌手套"
description = "一对由鱼鳍与音波丝编织而成的优雅手套，手套非常薄——薄到可以感受到水流的每一道纹理。手套的表面有特殊的纹路，可以在挥舞时产生音调——就像乐器一样。\n\n塞壬在编织这对手套时，将自己的指尖改造成了「乐器」。她说："这对手套不只是手套，它们是'乐器'——让我可以用手指演奏任何旋律。"\n\n这对手套的特殊效果是：穿戴者可以用手指「演奏」魔法旋律——每个旋律对应一种效果（治愈：恢复 2D8 HP；魅惑：目标 charmed 1 回合；恐惧：目标 frightened 1 回合；睡眠：目标 asleep 1 分钟）。每日各一次。且可以用双手「合奏」产生更强的效果——双手合奏时，效果范围和强度翻倍。塞壬说："最好的音乐不是一个人演奏的，是'合奏'——即使那个'合奏'只是自己的两只手。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "siren_song_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_siren_song_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_siren_song_hands" }
]
```

---

### 54.4 海妖之歌软靴

```gdscript
item_id = "armor_siren_song_feet"
display_name = "海妖之歌软靴"
description = "一对由鱼尾与音波丝编织而成的优雅软靴，软靴的形状不像人类的鞋子——它们更像鱼尾，宽大而扁平。在陆地上行走时，软靴会很笨拙（移动力 -5），但在水中，它们是完美的游泳工具。\n\n塞壬在编织这对软靴时，从一只人鱼身上取下了她的尾鳍。她说："这只人鱼自愿给了我她的尾鳍——作为交换，我教会了她一首可以让水手永远爱她的歌。"\n\n这对软靴的特殊效果是：在水中，穿戴者的游泳速度 = 步行速度 +20（鱼尾效果）。且可以用「歌声推进」——每日一次，通过歌声产生声波推进，在水中以三倍速度直线冲刺（不引发借机攻击）。塞壬说："海洋中最重要的不是力量，是'优雅'——优雅地游动，优雅地歌唱，优雅地狩猎。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "siren_song_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_siren_song_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = -5, source_type = "equipment", source_id = "armor_siren_song_feet" }
]
```

---

## 套装五十五：狼人诅咒（Werewolf Curse）

> *"月圆之夜，我不是人，我不是狼——我是两者之间的怪物。但怪物也有怪物的力量。"*

**套装主题**：狼人部族「银月之嚎」的装备。这个部族的成员接纳了狼人诅咒，用装备来控制和增强这种诅咒的力量。集齐四件时，穿戴者获得「狼人之躯」——可以在月圆之夜变身为狼人、免疫银器以外的武器、用狼群战术狩猎。

**历史渊源**：银月之嚎的族长「第一狼人」芬里尔（另一位同名者），是一位主动拥抱狼人诅咒的战士。他说："诅咒不是惩罚，它是'礼物'——只是大多数人不知道如何打开它。"

---

### 55.1 狼人诅咒头冠

```gdscript
item_id = "armor_werewolf_curse_head"
display_name = "狼人诅咒头冠"
description = "一顶由狼皮与月光丝编织而成的野性头冠，头冠上镶嵌着两颗「狼眼石」——两颗由真正狼人的眼睛制成的宝石。头冠佩戴时，穿戴者会感受到狼的本能正在觉醒——嗅觉变得敏锐，听觉变得灵敏，对月光的渴望变得强烈。\n\n芬里尔在编织这头冠时，将自己的狼眼封入了宝石。他说："这只眼睛不是被取下的，是'被分享的'——我保留了人眼，将狼眼给了头冠，让我可以同时看见两个世界。"\n\n这头冠的特殊效果是：穿戴者获得「狼之嗅觉」——可以分辨 100 尺内的任何气味（如同 scent ability）。且在月光下，嗅觉范围翻倍。但缺点是，穿戴者对银器变得敏感——银器造成的伤害翻倍。芬里尔说："力量总是有代价的，只是看你是否愿意支付。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "medium_armor", "werewolf_curse_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_werewolf_curse_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_werewolf_curse_head" }
]
```

**2件套（设计预留）**：`perception_bonus` +2；月光下 `strength_bonus` +1。
**4件套（设计预留）**：月圆之夜（或由 DM 决定的月光充足之夜），可以变身为狼人（力量 +2、敏捷 +2、AC +2、徒手攻击 1D8 slashing、移动力 +15、嗅觉范围 300 尺，持续至日出）；免疫 non-magical 和 non-silver 物理伤害（狼人再生）；可以「召唤狼群」——每日一次，召唤 1D4 只狼协助战斗（持续 10 分钟）；副作用：变身期间无法控制对人类的攻击性（须通过 DC14 智慧豁免，失败则攻击最近的生物）。

---

### 55.2 狼人诅咒鳞甲

```gdscript
item_id = "armor_werewolf_curse_body"
display_name = "狼人诅咒鳞甲"
description = "一副由狼皮与月光丝编织而成的野性鳞甲，鳞甲表面覆盖着类似狼毛的纹理——在月光下，这些毛发会变得更加浓密，提供更多的保护。鳞甲的接缝处有特殊的弹性材料——不是为了舒适，而是为了在变身为狼人时不会撕裂。\n\n芬里尔在编织这副鳞甲时，将自己变身为狼人时的皮毛都收集了起来。他说："这副鳞甲不只是铠甲，它是'我的另一面'——让我即使在人类形态也能感受到狼的力量。"\n\n这副鳞甲的特殊效果是：穿戴者在月光下 AC +2（狼毛竖起提供额外保护）。且可以将鳞甲的狼毛「释放」——每日一次，释放所有狼毛形成一个狼毛盾（AC +2，任何 melee 攻击穿戴者的生物受到 1D6 slashing，因为狼毛像针一样竖立）。芬里尔说："狼毛不只是保暖，它是'武器'——当狼生气时，每一根毛都是一根针。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "werewolf_curse_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 4
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_werewolf_curse_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_werewolf_curse_body" }
]
```

---

### 55.3 狼人诅咒手套

```gdscript
item_id = "armor_werewolf_curse_hands"
display_name = "狼人诅咒手套"
description = "一对由狼爪与月光丝编织而成的野性手套，手套表面覆盖着粗糙的皮革。指尖嵌有可伸缩的狼爪——不是为攻击，而是为攀爬和撕裂。\n\n芬里尔在编织这对手套时，将自己的狼爪封入了手套。他说："这对手套不只是手套，它们是'狼的爪子'——让我即使在人类形态也能使用狼的力量。"\n\n这对手套的特殊效果是：穿戴者可以用狼爪进行「撕裂攻击」——徒手攻击造成 1D8 slashing（如同狼爪）。且可以用爪子「攀爬」——在任何表面（包括光滑的墙壁和天花板）上以正常速度攀爬。且在月光下，爪子的伤害增加到 1D10。芬里尔说："狼不需要武器，狼本身就是武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "medium_armor", "werewolf_curse_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_werewolf_curse_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_werewolf_curse_hands" }
]
```

---

### 55.4 狼人诅咒软靴

```gdscript
item_id = "armor_werewolf_curse_feet"
display_name = "狼人诅咒软靴"
description = "一对由狼足底软皮与月光丝编织而成的野性软靴，靴底有肉垫状的纹路——模仿狼的足底结构，可以在任何地面上无声行走。\n\n芬里尔在编织这对软靴时，将自己变身为狼人时的足底都收集了起来。他说："这对软靴不只是鞋子，它们是'狼的脚步'——让我可以像狼一样安静地移动。"\n\n这对软靴的特殊效果是：穿戴者移动时完全无声（如同 pass without trace）。且可以在任何地面上追踪——通过气味和足迹，追踪检定 +5。且在月光下，移动力 +10（狼的奔跑速度）。芬里尔说："狼不是最快的猎手，但它是最耐心的——它会追踪猎物直到猎物疲惫。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "werewolf_curse_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_werewolf_curse_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_werewolf_curse_feet" }
]
```

---

## 套装五十六：影舞者（Shadow Dancer）

> *"影子不是黑暗的，它是'光的对立面'。学会与影子共舞，你就能在光明和黑暗之间自由穿梭。"*

**套装主题**：暗影行者行会「无形之影」的装备。这个行会的成员学会了操控影子，在影子之间移动和攻击。集齐四件时，穿戴者获得「影之躯」——可以进入影子、从影子中攻击、创造影子分身。

**历史渊源**：无形之影的创始人「影之主」厄瑞玻斯，是一位在完全黑暗中出生、从未见过光明的神秘存在。他说："我不是在黑暗中生活，我是在影子中生活——影子不是黑暗，它是'光的缺席'，而缺席也是一种存在。"

---

### 56.1 影舞者兜帽

```gdscript
item_id = "armor_shadow_dancer_head"
display_name = "影舞者兜帽"
description = "一顶由影子物质与暗丝编织而成的奇异兜帽，兜帽呈现出纯粹的黑色——不是布料的黑，是「没有光」的黑。戴上兜帽时，穿戴者的面部会完全融入阴影，连最敏锐的双眼也无法看清。\n\n厄瑞玻斯在编织这顶兜帽时，将自己的影子封入了丝线。他说："这顶兜帽不只是兜帽，它是'影子的面具'——让我可以隐藏在任何一个影子中。"\n\n这顶兜帽的特殊效果是：穿戴者可以「融入」任何影子中——只要影子足够大（至少覆盖穿戴者的身体），就可以完全隐藏（如同 greater invisibility，但只对在光亮处的观察者有效——在完全黑暗中反而更容易被发现，因为没有影子可以融入）。厄瑞玻斯说："最好的隐藏不是消失，是成为背景的一部分。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "shadow_dancer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_dancer_head" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shadow_dancer_head" }
]
```

**2件套（设计预留）**：`stealth_bonus` +3；在 dim light 或 shadow 中 AC +1。
**4件套（设计预留）**：每日两次「影步」： bonus action，传送至 60 尺内任意 shadow 中（如同 dimension door，但只能在 shadow 之间移动）；从 shadow 中攻击时，攻击检定 +2、伤害 +1D6 necrotic（影子偷袭）；每日一次「影子分身」：创造一个自己的影子分身（如同 mirror image，但只有 1 个分身，持续 1 分钟）；在 bright light 中 AC -1（没有 shadow 可以借助）。

---

### 56.2 影舞者长袍

```gdscript
item_id = "armor_shadow_dancer_body"
display_name = "影舞者长袍"
description = "一件由影子物质与暗丝编织而成的奇异长袍，长袍呈现出纯粹的黑色——当你触摸它时，感觉到的只是空无。长袍没有固定的形状——它会根据周围的光线变化：在强光下变得轻薄，在阴影中变得浓密，在完全黑暗中则几乎消失。\n\n厄瑞玻斯在编织这件长袍时，将一千个影子都压缩成了丝线。他说："这件长袍不只是衣服，它是'影子的集合'——让我穿着一千个影子的保护。"\n\n这件长袍的特殊效果是：穿戴者在 shadow 中 AC +2（影子护盾）。且可以通过长袍「释放」影子——每日一次，在 15 尺半径内创造一个「影子区域」（困难地形，所有生物在区域内攻击检定 -2，因为视线被影子干扰，持续 1 分钟）。厄瑞玻斯说："影子不是弱点，它是'武器'——一种可以困住敌人的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "shadow_dancer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_shadow_dancer_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_shadow_dancer_body" }
]
```

---

### 56.3 影舞者护腕

```gdscript
item_id = "armor_shadow_dancer_hands"
display_name = "影舞者护腕"
description = "一对由影子物质与暗丝编织而成的奇异护腕，护腕呈现出纯粹的黑色。挥舞手臂时，护腕会拉出长长的影子——不是普通的影子，是「活影子」，可以短暂地脱离身体独立行动。\n\n厄瑞玻斯在编织这对护腕时，将自己的「影子之手」能力封入了暗丝。他说："这对护腕不只是护腕，它们是'影子的延伸'——让我可以用手操控影子。"\n\n这对护腕的特殊效果是：穿戴者可以用影子「抓取」一个 15 尺内的目标——影子会伸长并缠绕目标（目标须通过 DC16 敏捷豁免，失败则 grappled 1 回合）。每日三次。且可以用影子「偷窃」——让影子脱离身体，潜入阴影中偷窃物品（如同 mage hand，但只能在 shadow 中移动，且可以偷窃）。厄瑞玻斯说："最好的小偷不是最灵活的，是最不可见的——而不可见就是影子的本质。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "shadow_dancer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_dancer_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shadow_dancer_hands" }
]
```

---

### 56.4 影舞者便鞋

```gdscript
item_id = "armor_shadow_dancer_feet"
display_name = "影舞者便鞋"
description = "一对由影子物质与暗丝编织而成的奇异便鞋，便鞋呈现出纯粹的黑色。行走时，便鞋不会发出任何声音，不会留下任何足迹，甚至不会触动地面的灰尘——因为影子只是光的缺席，缺席不会留下痕迹。\n\n厄瑞玻斯在编织这对便鞋时，将自己的「影子之步」能力封入了暗丝。他说："这对便鞋不只是鞋子，它们是'影子的移动'——让我可以像影子一样移动。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（如同 pass without trace）。且可以在 shadow 中「滑行」——移动力 +15（影子滑行）。且每日一次，可以进行「影子穿越」——进入地面上的任何一个 shadow，从 60 尺内的另一个 shadow 中出现（如同 dimension door，但只能在 shadow 之间）。厄瑞玻斯说："影子不是终点，它是'道路'——一条只有影子行者才能走的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "shadow_dancer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_dancer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_shadow_dancer_feet" }
]
```

---

## 套装五十七：水晶先知（Crystal Seer）

> *"水晶不是石头，它是'凝固的时间'——它记得过去，看见未来，只是需要正确的眼睛来读取。"*

**套装主题**：水晶占卜师结社「永恒之光」的装备。这个结社的成员用水晶来预知未来和操控光能，他们的装备赋予了穿戴者预知能力、光能攻击和水晶护盾。集齐四件时，穿戴者获得「水晶之躯」——可以预知攻击、释放激光、创造水晶护盾。

**历史渊源**：永恒之光的最高占卜师「水晶之心」克里斯特尔，是一位天生能够与水晶对话的女性。她说："水晶不是死的，它是'睡着的'——只是大多数人不知道如何唤醒它。"

---

### 57.1 水晶先知头冠

```gdscript
item_id = "armor_crystal_seer_head"
display_name = "水晶先知头冠"
description = "一顶由七彩水晶与光丝编织而成的华丽头冠，头冠上镶嵌着一颗巨大的「先知水晶」——一颗能够看见过去和未来的魔法水晶。水晶中 constantly 有影像在流动，但这些影像只有穿戴者才能看见。\n\n克里斯特尔在编织这头冠时，从世界最深的水晶洞中取下了这颗水晶。她说："这颗水晶不是被取下的，是'被选择的'——它选择了我，让我成为它的声音。"\n\n这头冠的特殊效果是：穿戴者可以「预知」未来——每日一次，可以看到接下来 1 分钟内的一个可能未来（DM 描述一个即将发生的事件）。且可以通过头冠「读取」任何水晶中的记忆——触碰一个水晶，可以看到它「记住」的影像（自动识别水晶的来源和历史）。克里斯特尔说："水晶不是工具，它是'伙伴'——你需要尊重它，它才会帮助你。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "crystal_seer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_crystal_seer_head" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_crystal_seer_head" }
]
```

**2件套（设计预留）**：`insight_bonus` +3；免疫「幻象」和「隐形」（水晶之眼可以看穿）。
**4件套（设计预留）**：每日两次「预知闪避」： reaction，当受到攻击时，预知攻击轨迹并自动闪避（miss）；每日一次「激光眼」：60 尺射程 4D10 radiant（聚焦光束），目标须通过 DC16 敏捷豁免，失败则 blinded 1 回合；可以「创造水晶盾」： bonus action，创造一个水晶护盾（AC +2，可以吸收 20 点伤害，然后破碎）。

---

### 57.2 水晶先知长袍

```gdscript
item_id = "armor_crystal_seer_body"
display_name = "水晶先知长袍"
description = "一件由七彩水晶与光丝编织而成的华丽长袍，长袍表面不断有彩虹般的光芒在流动。这件长袍没有固定的颜色——它会根据穿戴者的情绪和周围的光线变化。在黑暗中，长袍会发出微弱的荧光；在强光下，长袍会折射出彩虹。\n\n克里斯特尔在编织这件长袍时，将所有她收集的水晶碎片都编入了丝线。她说："这件长袍不只是衣服，它是'水晶的集合'——让我穿着所有水晶的力量。"\n\n这件长袍的特殊效果是：穿戴者可以「折射」光线——将任何 directed light（如激光、日光射线）折射向其他方向（每日一次，可以完全偏转一次 light-based 攻击）。且可以通过长袍「聚集」光线——在日光下，每小时恢复 1 HP（光能治愈）。克里斯特尔说："光不是只能看的，它也是可以穿的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "crystal_seer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_crystal_seer_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_crystal_seer_body" }
]
```

---

### 57.3 水晶先知手套

```gdscript
item_id = "armor_crystal_seer_hands"
display_name = "水晶先知手套"
description = "一对由七彩水晶与光丝编织而成的华丽手套，手套表面镶嵌着小型水晶。挥舞手臂时，水晶会折射光线，在周围创造出小型彩虹。触碰任何物体时，手套会感知物体的「历史」——不是读心，是读取物体「记住」的影像。\n\n克里斯特尔在编织这对手套时，将自己的「水晶之触」能力封入了光丝。她说："这对手套不只是手套，它们是'读取器'——让我可以通过触碰读取任何物体的记忆。"\n\n这对手套的特殊效果是：穿戴者可以通过触碰「读取」一个物体的历史——自动知道该物体的制造者、用途、和经历过的重要事件（类似于 object reading）。且可以用水晶「聚焦」光线——每日三次，释放一道聚焦光束（30 尺射程，2D10 radiant，可以穿透透明障碍物如玻璃和冰）。克里斯特尔说："每一个物体都有故事，只是大多数人不愿意倾听。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "crystal_seer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_crystal_seer_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_crystal_seer_hands" }
]
```

---

### 57.4 水晶先知便鞋

```gdscript
item_id = "armor_crystal_seer_feet"
display_name = "水晶先知便鞋"
description = "一对由七彩水晶与光丝编织而成的华丽便鞋，鞋底镶嵌着小型水晶。行走时，水晶会与地面摩擦发出微弱的光芒，在地面上留下短暂的发光足迹——不是弱点，是美丽，也是「光之路径」，让穿戴者可以回溯自己的脚步。\n\n克里斯特尔在编织这对便鞋时，将自己的「光之步」能力封入了光丝。她说："这对便鞋不只是鞋子，它们是'光的画笔'——让我每一步都在大地上绘制光芒。"\n\n这对便鞋的特殊效果是：穿戴者走过的地方会留下「光之足迹」（持续 1 小时，只有穿戴者可以看见）。且可以通过「光之回溯」——沿着光之足迹以两倍速度返回（如同 haste 的 movement 效果，但只能在已经走过的路径上使用）。且在日光下，移动力 +10（光能加速）。克里斯特尔说："光是最好的路标，因为它永远不会消失——只是有时候我们看不见。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "crystal_seer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_crystal_seer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_crystal_seer_feet" }
]
```

---

## 套装五十八：霜巨人（Frost Giant）

> *"寒冷不是弱点，它是'力量'——一种可以冻结时间、凝固空间的力量。"*

**套装主题**：霜巨人部族「永冬之峰」的装备。这个部族的成员生活在永恒的寒冬中，他们的装备由冰霜和永冻金属锻造，赋予了穿戴者操控寒冷和冰霜的能力。集齐四件时，穿戴者获得「霜巨人之躯」——可以释放冰霜攻击、冻结敌人、免疫寒冷。

**历史渊源**：永冬之峰的首领「霜王」尤弥尔（另一位同名者），是一位据说由永恒之冰诞生的霜巨人。他说："我不是被创造的，我是'冻结的'——时间冻结了我，我也冻结了时间。"

---

### 58.1 霜巨人头盔

```gdscript
item_id = "armor_frost_giant_head"
display_name = "霜巨人头盔"
description = "一顶由永冻金属与冰丝锻造的庞大头盔，头盔表面覆盖着一层永不融化的霜。头盔的呼吸孔不断有冷气喷出——不是穿戴者在呼吸，是头盔在「呼吸」寒冷。头盔的两侧有两支向上弯曲的冰角——模仿霜巨人的角，但不是为了装饰，而是为了「聚集」寒气。\n\n尤弥尔在锻造这顶头盔时，从永恒之冰的核心取下了一块冰晶。他说："这块冰晶不是普通的冰，它是'时间的冻结'——它冻结了亿万年前的某个瞬间。"\n\n这顶头盔的特殊效果是：穿戴者免疫 cold damage（霜巨人之血）。且可以「喷吐」寒气——每日三次，向前喷出一道 15 尺锥形的寒气（2D10 cold，目标须通过 DC16 体质豁免，失败则 speed 减半 1 回合）。尤弥尔说："寒冷不是攻击，它是'礼物'——大多数人只是不懂得欣赏。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "frost_giant_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_frost_giant_head" },
    { attribute_id = "resistance_cold", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_frost_giant_head" }
]
```

**2件套（设计预留）**：免疫 cold damage；`resistance_fire` -10（霜巨人怕火）。
**4件套（设计预留）**：每日三次「冰霜之握」： melee 命中后，目标须通过 DC16 体质豁免，失败则 restrained 1 回合（冰冻）；近战攻击附加 1D10 cold；每日一次「暴风雪」：30 尺半径内所有生物受到 4D10 cold，须通过 DC16 体质豁免，失败则 blinded 1 回合（暴风雪遮蔽视线）；10 尺内所有敌人每回合受到 1D6 cold（冰霜光环）。

---

### 58.2 霜巨人板甲

```gdscript
item_id = "armor_frost_giant_body"
display_name = "霜巨人板甲"
description = "一副由永冻金属与冰丝锻造的庞大板甲，板甲表面覆盖着一层永不融化的霜。在战斗中，霜会变得更加浓厚，形成一层冰甲。板甲的接缝处有特殊的防冻层——不是为了保护穿戴者，而是为了保护别人（穿戴者拥抱寒冷，但不想冻死盟友）。\n\n尤弥尔在锻造这副板甲时，将永恒之冰的核心都熔入了金属。他说："这副板甲不只是铠甲，它是'寒冬的化身'——让我可以把永冬带到任何地方。"\n\n这副板甲的特殊效果是：穿戴者受到 cold damage 时，板甲会「增厚」——AC +1（最多叠加至 +3），持续 1 回合。且可以将板甲的寒气「释放」——每日一次，释放所有寒气形成一个冰盾（AC +3，任何 melee 攻击穿戴者的生物受到 2D6 cold 并被 slowed 1 回合，因为肢体被冻僵）。尤弥尔说："寒冷不是防御，它是'武器'——一种可以冻结敌人肢体的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "frost_giant_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_frost_giant_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_frost_giant_body" }
]
```

---

### 58.3 霜巨人护手

```gdscript
item_id = "armor_frost_giant_hands"
display_name = "霜巨人护手"
description = "一对由永冻金属与冰丝锻造的庞大护手，护手表面覆盖着一层永不融化的霜。握紧拳头时，霜会汇聚在拳峰，形成两颗小型的「冰球」。松开拳头时，冰球会消散，但霜会继续存在。\n\n尤弥尔在锻造这对手套时，将自己的双手改造成了「冰霜导体」。他说："这对手套不只是手套，它们是'寒冬的爪子'——让我可以用手释放寒冷。"\n\n这对手套的特殊效果是：穿戴者可以用拳头进行「冰霜打击」——徒手攻击造成 1D8 bludgeoning + 1D10 cold。且可以用手掌「冻结」一个无生命物体——触碰一个物体，将其冻结（水变冰，金属变脆，木头变硬）。每日三次。尤弥尔说："寒冷不是只能伤害，它也能改变——改变物质的形态，改变战场的地形。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "frost_giant_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_giant_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_giant_hands" }
]
```

---

### 58.4 霜巨人胫甲

```gdscript
item_id = "armor_frost_giant_feet"
display_name = "霜巨人胫甲"
description = "一对由永冻金属与冰丝锻造的庞大胫甲，胫甲表面覆盖着一层永不融化的霜。每一步踏下，霜会在地面上扩散，形成一片「冰面」（困难地形，持续 1 回合）。\n\n尤弥尔在锻造这对胫甲时，将「冰霜之步」能力封入了金属。他说："这对胫甲不只是鞋子，它们是'寒冬的道路'——让我每一步都在大地上播种寒冷。"\n\n这对胫甲的特殊效果是：穿戴者可以在冰面上正常行走（免疫 difficult terrain from ice）。且可以进行「冰霜冲锋」——每日一次，以三倍速度直线冲锋，路径上留下冰霜轨迹（轨迹持续 1 分钟，任何踩到的生物受到 2D6 cold 并须通过 DC14 敏捷豁免，失败则 prone）。尤弥尔说："霜巨人不需要路，它只需要冰——在冰上，没有人比霜巨人更快。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "frost_giant_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_giant_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_frost_giant_feet" }
]
```

---

## 套装五十九：木乃伊诅咒（Mummy's Curse）

> *"死亡不是结束，它是'另一种存在'。我学会了在死亡中生存。"*

**套装主题**：古埃及木乃伊守护者的装备。这些守护者用绷带和诅咒保护自己的陵墓，他们的装备赋予了穿戴者诅咒敌人、操控亡灵和从死亡中恢复的能力。集齐四件时，穿戴者获得「木乃伊之躯」——可以释放诅咒、从绷带中恢复、免疫毒素和疾病。

**历史渊源**：木乃伊守护者的最高祭司「诅咒之主」阿努比斯（另一位同名者），是一位自愿被制成木乃伊以永远守护法老陵墓的祭司。他说："我不是死了，我是'转化了'——从血肉转化为永恒。"

---

### 59.1 木乃伊诅咒头巾

```gdscript
item_id = "armor_mummy_curse_head"
display_name = "木乃伊诅咒头巾"
description = "一条由千年绷带与诅咒丝编织而成的头巾，头巾上写满了死亡诅咒的象形文字——每一个符文都是一个诅咒，诅咒任何试图取下头巾的人。头巾只露出双眼，但那双眼不是活人的眼睛——它们是干涸的、空洞的，但仍然可以看见。\n\n阿努比斯在编织这条头巾时，将自己的脸完全包裹了起来。他说："这条头巾不只是遮挡，它是'封印'——封印着我的人性，让我可以永远执行我的职责。"\n\n这条头巾的特殊效果是：穿戴者免疫所有 diseases、poisons 和 necrotic damage（木乃伊已经死了，无法再死一次）。且可以通过头巾「释放」诅咒——每日一次，对一个目标施加「木乃伊诅咒」（目标须通过 DC16 体质豁免，失败则每长休一次失去 1 点最大 HP，直到诅咒被解除）。阿努比斯说："诅咒不是恶意，它是'警告'——告诉所有人，不要打扰死者。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "mummy_curse_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mummy_curse_head" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_mummy_curse_head" }
]
```

**2件套（设计预留）**：免疫 diseases、poisons 和 necrotic damage；`resistance_fire` -10（木乃伊怕火）。
**4件套（设计预留）**：每日两次「恐惧凝视」：30 尺内一个目标须通过 DC16 智慧豁免，失败则 frightened 1 回合；近战攻击附加 1D8 necrotic（腐朽之触）；每日一次「绷带缠绕」： bonus action，用绷带缠绕自己（恢复 3D8 HP，如同第二 wind）；10 尺内所有敌人每回合受到 1D6 necrotic（腐朽光环）。

---

### 59.2 木乃伊诅咒长袍

```gdscript
item_id = "armor_mummy_curse_body"
display_name = "木乃伊诅咒长袍"
description = "一件由千年绷带与诅咒丝编织而成的恐怖长袍，长袍表面写满了死亡诅咒的象形文字。这件长袍没有固定的形态——它会根据穿戴者的状态变化：当穿戴者健康时，绷带是紧致的；当穿戴者受伤时，绷带会松散并渗出黑色的液体（腐朽的象征）。\n\n阿努比斯在编织这件长袍时，将自己所有的绷带都编入了丝线。他说："这件长袍不只是衣服，它是'我的第二层皮肤'——让我永远与死亡相连。"\n\n这件长袍的特殊效果是：穿戴者可以将长袍的绷带「释放」——每日一次，释放绷带缠绕 15 尺内的一个目标（目标须通过 DC16 力量豁免，失败则 restrained 1 分钟，且每回合受到 2D6 necrotic）。且长袍会自动「修复」自己受到的任何损伤（每小时恢复 1 HP，如同不死之躯的缓慢再生）。阿努比斯说："绷带不是束缚，它是'保护'——保护着我永恒的身体。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "mummy_curse_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_mummy_curse_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_mummy_curse_body" }
]
```

---

### 59.3 木乃伊诅咒手套

```gdscript
item_id = "armor_mummy_curse_hands"
display_name = "木乃伊诅咒手套"
description = "一对由千年绷带与诅咒丝编织而成的恐怖手套，手套表面不断有黑色的粉末在脱落——那是腐朽的象征，也是诅咒的载体。触碰任何生物时，手套会将「腐朽」传递给目标。\n\n阿努比斯在编织这对手套时，将自己的双手完全包裹了起来。他说："这对手套不只是手套，它们是'死亡的触碰'——让我可以用触碰传递永恒。"\n\n这对手套的特殊效果是：穿戴者可以用触碰「腐朽」一个目标—— bonus action，触碰一个生物，目标须通过 DC16 体质豁免，失败则受到 2D10 necrotic 并失去 1 点力量或敏捷（暂时，长休恢复）。每日两次。且可以用手套「治愈」不死生物——触碰一个 undead，恢复 2D8 HP。阿努比斯说："死亡不是敌人，它是'另一种治愈'——只是大多数人无法接受。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "mummy_curse_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mummy_curse_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mummy_curse_hands" }
]
```

---

### 59.4 木乃伊诅咒便鞋

```gdscript
item_id = "armor_mummy_curse_feet"
display_name = "木乃伊诅咒便鞋"
description = "一对由千年绷带与诅咒丝编织而成的恐怖便鞋，便鞋表面不断有沙粒在脱落。行走时，便鞋不会发出任何声音——不是因为有特殊材料，而是因为木乃伊不需要走路，它只是「滑动」。\n\n阿努比斯在编织这对便鞋时，将自己的双脚完全包裹了起来。他说："这对便鞋不只是鞋子，它们是'永恒的脚步'——让我在千年之后仍然可以行走。"\n\n这对便鞋的特殊效果是：穿戴者移动时完全无声（如同 pass without trace）。且可以在沙地上正常行走——包括流沙（木乃伊轻到不会被吞没）。且可以用「沙之步」——每日一次，化作一阵沙尘暴移动（移动力 ×3，穿过任何缝隙，免疫借机攻击，持续 1 回合）。阿努比斯说："木乃伊不需要路，它只需要沙——在沙中，木乃伊无处不在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "mummy_curse_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_mummy_curse_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_mummy_curse_feet" }
]
```

---

## 套装六十：龙骑士（Dragon Rider）

> *"龙不是坐骑，它是'伙伴'。你骑的不是它的背，是它的信任。"*

**套装主题**：龙骑士团「天空之翼」的装备。这个骑士团的成员与龙建立了深厚的羁绊，他们的装备由龙鳞和龙息锻造，赋予了穿戴者与龙协同作战的能力。集齐四件时，穿戴者获得「龙骑士之躯」——可以召唤龙息、免疫对应龙种的元素、与龙进行心灵沟通。

**历史渊源**：天空之翼的最高骑士「龙心」贝奥武夫，是一位与红龙「烈焰之心」建立了灵魂羁绊的传奇骑士。他说："我不是龙的主人，我是龙的'兄弟'——我们共享同一种火焰，同一种骄傲，同一种愤怒。"

---

### 60.1 龙骑士头盔

```gdscript
item_id = "armor_dragon_rider_head"
display_name = "龙骑士头盔"
description = "一顶由龙鳞与龙息丝锻造的威严头盔，头盔的造型模仿龙首——有两支向上弯曲的龙角、尖锐的鼻突和可开合的下颚面甲。头盔的双眼位置镶嵌着两颗「龙睛石」——两颗由真正龙的眼睛制成的宝石，让穿戴者可以获得龙的视野。\n\n贝奥武夫在锻造这顶头盔时，从他的伙伴「烈焰之心」身上取下了一片鳞片和一滴眼泪（龙的眼泪会凝结成宝石）。他说："这顶头盔不只是头盔，它是'龙的延伸'——让我可以分享龙的眼睛。"\n\n这顶头盔的特殊效果是：穿戴者获得「龙之视野」——可以看清 120 尺内的一切（如同 darkvision + 远视），且可以「看见」热量分布（如同 thermal vision）。且可以通过头盔与任何龙进行「心灵沟通」——无需语言，直接交换思想和情感。贝奥武夫说："龙的眼睛不是为地面设计的，它们是为天空设计的——在天空中，你需要看得远，看得清。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "dragon_rider_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dragon_rider_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dragon_rider_head" }
]
```

**2件套（设计预留）**：`resistance_fire` +15（与红龙的羁绊）；与龙类生物的社交检定 +5。
**4件套（设计预留）**：每日三次「龙息模拟」：15 尺锥形 4D10 fire（模拟红龙吐息）；免疫 fire damage（龙骑士与龙共享元素免疫）；每日一次「召唤龙影」：召唤一个龙的影子（如同 spiritual weapon，但形态为龙，伤害 2D8 fire + 2D8 slashing，持续 1 分钟）；骑乘龙时 AC +2、攻击检定 +2（协同作战）。

---

### 60.2 龙骑士板甲

```gdscript
item_id = "armor_dragon_rider_body"
display_name = "龙骑士板甲"
description = "一副由龙鳞与龙息丝锻造的威严板甲，板甲表面覆盖着真正的龙鳞——每一片鳞片都来自不同的龙（红、蓝、绿、黑、白、金、银），因此呈现出彩虹般的色彩。板甲的接缝处有特殊的弹性材料——不是为了舒适，而是为了在骑龙时能够承受高速飞行中的气压变化。\n\n贝奥武夫在锻造这副板甲时，收集了七只不同龙种的鳞片。他说："这副板甲不只是铠甲，它是'龙的博物馆'——让我穿着所有龙的力量。"\n\n这副板甲的特殊效果是：穿戴者免疫对应龙种的元素伤害（例如如果主要鳞片来自红龙，则免疫 fire；如果来自蓝龙，则免疫 lightning）。且可以将板甲的龙鳞「激活」——每日一次，让所有龙鳞同时发光，释放一次「彩虹吐息」（30 尺锥形，2D8 随机元素伤害 ×7 种元素 = 14D8 总伤害，目标须通过 DC18 敏捷豁免，失败则全伤）。贝奥武夫说："一条龙的力量是强大的，但七条龙的力量是不可阻挡的。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "dragon_rider_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 38000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_dragon_rider_body" },
    { attribute_id = "max_hp", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_dragon_rider_body" }
]
```

---

### 60.3 龙骑士护手

```gdscript
item_id = "armor_dragon_rider_hands"
display_name = "龙骑士护手"
description = "一对由龙爪与龙息丝锻造的威严护手，护手表面覆盖着龙鳞。指尖嵌有可伸缩的龙爪——不是为攻击，而是为在龙背上保持抓握力。握紧拳头时，龙爪会伸出；张开手掌时，龙爪会缩回。\n\n贝奥武夫在锻造这对手套时，从「烈焰之心」身上取下了她的爪子。他说："这对手套不只是手套，它们是'龙的爪子'——让我可以用龙的力量战斗。"\n\n这对手套的特殊效果是：穿戴者可以用龙爪进行「撕裂攻击」——徒手攻击造成 1D10 slashing + 1D8 fire（龙爪 + 龙息）。且可以用手掌「释放」龙息——每日三次，释放一道 15 尺锥形的火焰（3D10 fire）。贝奥武夫说："龙的爪子不是为了地面战斗的，是为了空中战斗——在空中，爪子是唯一的武器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "dragon_rider_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dragon_rider_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dragon_rider_hands" }
]
```

---

### 60.4 龙骑士胫甲

```gdscript
item_id = "armor_dragon_rider_feet"
display_name = "龙骑士胫甲"
description = "一对由龙爪与龙息丝锻造的威严胫甲，胫甲表面刻有龙的图案。胫甲的底部有特殊的「龙爪纹路」——不是为了行走，而是为了在龙背上保持抓握力。在龙背上时，这对胫甲会让穿戴者如同与龙融为一体。\n\n贝奥武夫在锻造这对胫甲时，将「龙之步」能力封入了龙爪。他说："这对胫甲不只是鞋子，它们是'龙的鞍'——让我可以与龙融为一体。"\n\n这对胫甲的特殊效果是：穿戴者骑乘龙时，免疫 falling damage（龙骑士不会被甩下）。且可以在龙背上正常战斗——不会因为龙的高速移动而受到攻击惩罚。且每日一次，可以进行「龙跃」——从龙背上跳下，以两倍速度滑翔至地面（免疫 falling damage），落地时释放一次冲击波（5 尺半径 2D10 bludgeoning）。贝奥武夫说："龙骑士最强大的时刻不是骑在龙背上，是从龙背上跳下的那一刻——因为那一刻，他既是龙，也是骑士。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "dragon_rider_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dragon_rider_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_dragon_rider_feet" }
]
```

---

*神话传说主题套装 51–60 完结 · 共 40 件护甲装备（重甲 20 + 中甲 12 + 轻甲 8）*

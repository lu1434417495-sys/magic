# 传奇装备套装护甲设计文档（套装 16–20）

> 5 套传奇套装的护甲部分，共 20 件护甲装备，覆盖 head / body / hands / feet 四个部位。

---

## 套装十六：深渊凝视（Abyss Gazer）护甲

> *"如果真理藏在恐惧深处，那我就潜入恐惧的最深处。"*

**套装主题**：深海探险家结社「深渊之眼」的传承装备。这个结社在两百年前的一次深海探索中全员被某种不可名状的存在"注视"而疯狂，但他们的装备因为长期暴露在深渊能量中而获得了看穿真相的能力。

**历史渊源**：深渊之眼的创始人「第一凝视者」克苏鲁，是一位为了"看见真相"而自愿被深渊注视的疯狂哲学家。

---

### 16.1 深渊凝视深渊头盔（Abyss Gazer Abyss Helm）

```gdscript
item_id = "armor_abyss_gazer_head"
display_name = "深渊凝视深渊头盔"
description = "一顶由深海黑曜石与不可名状生物的骨骼熔铸而成的奇异头盔，头盔呈现出深海蓝与绝对黑的混合色——在光线下会折射出令人不安的虹彩。头盔的面甲是一块能够吸收光线的黑色水晶，佩戴者可以通过这块水晶"看见"隐藏在现实之下的真相。\n\n这顶头盔是克苏鲁在深海的最深处，从那个"存在"的注视下带回来的。他说："那个存在没有眼睛，但它能够'看见'——不是用光线，而是用一种我们无法理解的感知方式。这顶头盔让我能够借用它的感知，看见以前看不见的东西。"他在头盔内侧刻下了深渊的第一条法则："看见真相的代价是失去对虚假的舒适。"\n\n头盔会在佩戴者面对幻象或伪装时自动激活——面甲会过滤掉所有"虚假的光线"，让佩戴者看到事物的"真实形态"（如同 true seeing 的 limited 版，可以看出 illusion 和 shapechange，但不能看透实体障碍物）。一位后来的探险者在描述这顶头盔时说："戴上它，我看到了一个我从未想象过的世界——美丽、恐怖、真实。我不敢再摘下来，因为我害怕忘记真相。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "abyss_gazer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_abyss_gazer_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_abyss_gazer_head" }
]
```

**特殊效果（设计预留）**：可以看穿 illusion 和 disguise（深渊之眼的 limited true seeing）。

---

### 16.2 深渊凝视深渊铠甲（Abyss Gazer Abyss Armor）

```gdscript
item_id = "armor_abyss_gazer_body"
display_name = "深渊凝视深渊铠甲"
description = "一件由深海黑曜石与不可名状生物的膜编织而成的奇异铠甲，铠甲表面不断有微弱的生物荧光在流动——如同深海的微光层。铠甲没有固定的形状——它会随深渊能量的浓度而变化，在深渊附近变得更加厚重，在远离深渊时变得更加轻盈。\n\n这件铠甲是克苏鲁在深海的最深处，从那个"存在"的"身体"上取下的一块碎片制作的。他说："那个存在不是生物，不是物体，它是某种'状态'——一种我们尚未理解的存在方式。这块碎片让我能够携带一小部分深渊，而不被它吞噬。"他在铠甲内侧缝了七道"深渊封印"——每一道封印都代表一种深渊危险的防护措施。\n\n铠甲会在佩戴者受到精神攻击时自动激活——每日一次，可以完全吸收一次 psychic damage（深渊的庇护），并将其转化为深渊能量储存在铠甲中。储存的能量可以在之后释放—— bonus action，释放一道深渊凝视（15 尺锥形 2D10 psychic，目标须通过 DC14 智慧豁免，失败则 frightened 1 回合）。一位后来的探险者说："这件铠甲不是保护你免受深渊伤害，它是让你'成为'深渊的一部分——这样深渊就不会再伤害你。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "abyss_gazer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 27000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_abyss_gazer_body" },
    { attribute_id = "resistance_psychic", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_abyss_gazer_body" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_abyss_gazer_body" }
]
```

**特殊效果（设计预留）**：免疫精神控制效果（charm, dominate），但对恐怖效果的豁免检定-1。

---

### 16.3 深渊凝视触须护手（Abyss Gazer Tentacle Gauntlets）

```gdscript
item_id = "armor_abyss_gazer_hands"
display_name = "深渊凝视触须护手"
description = "一对由不可名状生物的触须与深海黑曜石锻造的奇异护手，护手表面不断有微弱的吸力波纹——那不是物理现象，而是深渊能量在"渴望"接触。护手的指尖各嵌有一枚微型深渊之眼——每一枚眼睛都可以独立转动，让佩戴者能够同时看到多个方向。\n\n这对手套是克苏鲁在深海的最深处，从那个"存在"的"触须"上取下的一段制作的。他说："那个存在的触须不是用来抓握的，它们是用来'感知'的——感知温度、压力、化学成分，甚至'情绪'。这段触须让我能够借用它的感知能力。"他将触须与深海黑曜石结合，制作成了这对手套。\n\n手套会在佩戴者进行感知或调查检定时自动激活——指尖的深渊之眼会独立转动，提供 360 度的视野（免疫被 flank 或 surprise）。且可以通过手套「深渊之触」——每日三次，触碰一个目标，感知其情绪、意图和最近记忆（如同 detect thoughts 的触摸版，DC14 智慧豁免抵抗）。一位后来的探险者说："戴上这对手套，我感觉自己有了六双眼睛——它们看到的不是表面，是'真实'。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "abyss_gazer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_abyss_gazer_hands" },
    { attribute_id = "insight_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_abyss_gazer_hands" }
]
```

**特殊效果（设计预留）**：免疫被 flank 和 surprise，且可以感知目标的情绪和意图。

---

### 16.4 深渊凝视深海靴（Abyss Gazer Abyss Boots）

```gdscript
item_id = "armor_abyss_gazer_feet"
display_name = "深渊凝视深海靴"
description = "一对由不可名状生物的鳍与深海黑曜石制成的奇异软靴，靴底覆盖着一层能够吸附任何表面的微型吸盘——在光滑的表面上不会滑倒，在垂直的表面上可以攀爬。靴筒内侧刻有七道微型深渊压力符文。\n\n这对软靴是克苏鲁在深海的最深处，从那个"存在"的"移动器官"上取下的一部分制作的。他说："那个存在不需要'行走'，它通过某种我们无法理解的方式'移动'——穿透物质、扭曲空间、或者直接'出现'。这部分器官让我能够借用它的移动能力。"他将器官碎片与深海黑曜石结合，制作成了这对软靴。\n\n软靴的特殊效果是：佩戴者可以在任何表面上行走——包括垂直墙壁、天花板、甚至水面（如同 spider climb + water walk 的组合）。且在水下环境中，软靴会提供完全的适应性——可以在任何深度正常行动，不受水压影响。一位后来的探险者说："穿着这对软靴，我不是在走路，我是在'爬行'——像那个存在一样，穿透空间，无处不在。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "abyss_gazer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_abyss_gazer_feet" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_abyss_gazer_feet" }
]
```

**特殊效果（设计预留）**：可以在任何表面行走（墙壁、天花板、水面），且在水下不受水压影响。

---

## 套装十七：圣光使者（Lightbringer）护甲

> *"我看不见黑暗，但我能让黑暗看见我。"*

**套装主题**：失落神殿「永恒之光」的传承装备。这个神殿在一百年前被黑暗势力摧毁，但神殿守护者们将最后的圣光封入了他们的装备中。

**历史渊源**：永恒之光的最后一位大主教「不灭之光」拉斐尔，是一位在神殿被毁时选择留下守护圣火的盲眼老人。

---

### 17.1 圣光使者光冠（Lightbringer Halo Crown）

```gdscript
item_id = "armor_lightbringer_head"
display_name = "圣光使者光冠"
description = "一顶由纯金与圣光水晶锻造的庄严头冠，头冠上方悬浮着一圈微型圣光光环——那不是装饰，而是真正的圣光被封印在了光环中。光环会随佩戴者的情绪变化亮度——坚定时明亮如正午，动摇时暗淡如黄昏。头冠没有面甲，因为拉斐尔相信"圣光不需要防御，它本身就是最强的防御"。\n\n这顶光冠是拉斐尔在神殿被毁前的最后一日，用永恒之光的圣火与自己的一滴眼泪一同铸造的。他说："我的眼泪不是因为恐惧，而是因为不舍——不舍这座神殿，不舍这些书籍，不舍这些信仰。但圣光不会因为神殿的毁灭而熄灭，它会在每一个信仰者的心中继续燃烧。"他将圣火封入了光环，成为了永恒之光的最后遗产。\n\n光冠会在佩戴者面对邪恶生物时自动激活——光环会释放出刺目的圣光，30 尺内所有 undead 和 fiend 须通过 DC14 体质豁免，失败则 blinded 1 回合（圣光的净化）。且光冠可以让佩戴者在黑暗中看清 60 尺——不是夜视，而是圣光照亮了黑暗（如同 light spell 的 personal 版）。一位后来的圣骑士在描述这顶光冠时说："它不是头冠，它是'移动的圣火'——无论我走到哪里，圣光就跟我到哪里。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "lightbringer_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_lightbringer_head" },
    { attribute_id = "resistance_radiant", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_lightbringer_head" }
]
```

**特殊效果（设计预留）**：对 undead 和 fiend 的攻击检定+2。

---

### 17.2 圣光使者圣铠（Lightbringer Holy Plate）

```gdscript
item_id = "armor_lightbringer_body"
display_name = "圣光使者圣铠"
description = "一副由纯金板甲与圣光水晶镶嵌而成的重型铠甲，甲面上不断有圣光纹路在流动——如同活物。铠甲的胸甲位置镶嵌着一枚永恒之光的圣火核心——那是拉斐尔在神殿被毁时，从圣坛上抢救下来的最后一点圣火。铠甲非常沉重——不是因为金属，而是因为上面承载的信仰之重。\n\n这副圣铠是拉斐尔在神殿被毁前的最后一日，命令神殿的铸造师用所有剩余的材料打造的最后一件作品。铸造师们在锤打每一块甲片时，都在心中默念祈祷词——他们说："我们不是在打造铠甲，我们是在铸造信仰。"铠甲完成后，拉斐尔是第一个也是最后一个穿上它的人——他穿着它站在神殿门口，面对着潮水般的黑暗势力。\n\n圣铠会在佩戴者受到邪恶生物攻击时自动激活——圣火核心会释放出保护性圣光，将一次攻击的伤害减半（圣光的庇护，每日三次）。且铠甲可以让佩戴者免疫 necrotic damage——圣火会将一切黑暗能量净化为无害的光芒。一位后来的圣骑士在描述这件铠甲时说："穿着它，我感觉不到恐惧——不是因为勇敢，而是因为圣火在我心中燃烧，黑暗无法靠近。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "lightbringer_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_lightbringer_body" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_lightbringer_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_lightbringer_body" }
]
```

**特殊效果（设计预留）**：受到 evil 生物攻击时，50% 概率将伤害减半（圣火庇护）。

---

### 17.3 圣光使者圣光护手（Lightbringer Holy Gauntlets）

```gdscript
item_id = "armor_lightbringer_hands"
display_name = "圣光使者圣光护手"
description = "一对由纯金与圣光水晶锻造的华丽护手，手背处各嵌有一枚微型圣火核心。握紧拳头时，圣火核心会释放出刺目的光芒，将周围的黑暗驱散；张开手掌时，光芒会变得柔和，可以治愈伤口。护手的指尖各刻有一道圣光符文——每一道符文代表一种不同的神圣之力。\n\n这对手套是拉斐尔在神殿被毁前的最后一日，用自己的双手与圣火一同锻造的。他说："我的手曾经抚摸过无数圣书，曾经给予过无数祝福，现在它们将最后一次释放圣光——不是为了攻击，而是为了守护。"他在手套上刻下了七道圣光符文——从能够驱散邪恶的"净化"，到能够治愈伤口的"恢复"。\n\n手套会在佩戴者进行近战攻击时自动激活——每日三次，可以释放一道圣光（5 尺射程 2D8 radiant，对 undead 双倍）。且可以通过手套「治愈」—— bonus action，触碰一个 5 尺内的盟友，恢复 2D8 HP（圣光的触摸，每日三次）。一位后来的圣骑士说："这对手套既是武器也是工具——它们可以驱散黑暗，也可以治愈伤口，关键在于使用者的意图。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "lightbringer_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_lightbringer_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_lightbringer_hands" }
]
```

**特殊效果（设计预留）**：近战攻击附加1D6 radiant，且可以治愈盟友。

---

### 17.4 圣光使者圣行靴（Lightbringer Holy Boots）

```gdscript
item_id = "armor_lightbringer_feet"
display_name = "圣光使者圣行靴"
description = "一对由纯金与圣光水晶锻造的庄严重靴，靴底刻有圣光符文——每一步踏下，都会在地面上留下一个短暂发光的金色足迹，数秒后才消散。靴筒内侧缝有七道微型圣光封印，每一道封印都代表一种神圣的保护。\n\n这对重靴是拉斐尔在神殿被毁前的最后一日，用自己的双脚与圣火一同锻造的。他说："我的脚曾经走过无数神圣的道路，现在它们将最后一次踏出圣光的印记——不是为了炫耀，而是为了指引。"他在靴底刻下了圣光符文，让每一个足迹都成为迷失者的灯塔。\n\n重靴的特殊效果是：佩戴者在行走时会自动留下圣光足迹——30 尺内所有盟友可以跟随足迹获得 +5 移动力（圣光的指引）。且每日一次，可以通过重靴「神圣冲锋」——移动力 +15，冲锋后的第一次近战攻击附加 2D8 radiant damage。一位后来的圣骑士说："穿着这对靴子，我不是在走路，我是在'播种'——每一步都播下一颗圣光的种子，照亮后来者的道路。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "lightbringer_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_lightbringer_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_lightbringer_feet" }
]
```

**特殊效果（设计预留）**：盟友跟随足迹获得额外移动力，且冲锋攻击附加 radiant 伤害。

---

## 套装十八：永恒学徒（Eternal Apprentice）护甲

> *"最高的智慧不是知道一切，而是知道自己不知道一切。"*

**套装主题**：古老学院「无限之塔」的传承装备。这个学院在四百年前因一场知识爆炸而被"封印"在时间中，但学院中最优秀的学徒们的装备因为长期暴露在纯知识能量中而获得了增强学习能力的能力。

**历史渊源**：无限之塔的创始人「第一学徒」索克拉底，是一位声称"我知道我一无所知"的谦逊智者。

---

### 18.1 永恒学徒学者帽（Eternal Apprentice Scholar Cap）

```gdscript
item_id = "armor_eternal_apprentice_head"
display_name = "永恒学徒学者帽"
description = "一顶由知识之布与无限丝线编织而成的奇异帽子，帽子表面不断有微型文字在流动——不是装饰，而是被封印在布料中的"知识碎片"在活动。帽子的形状会随佩戴者的学习进度而变化——初学者时是圆形，进阶时变成方形，大师时变成星形。\n\n这顶学者帽是索克拉底在创建无限之塔时，用自己的第一根白发与知识之布一同编织的。他说："白发不是衰老的象征，它是'学习的勋章'——每一根白发都代表一个被理解的概念。"他在帽子上缝了三百六十五个微型口袋，每个口袋中都藏着一张写有问题的纸条——从最简单的"什么是存在"，到最复杂的"时间的本质是什么"。\n\n学者帽会在佩戴者学习新知识时自动激活——知识碎片会流动得更快，帮助佩戴者加速理解（学习新技能或语言的时间减半）。且帽子可以让佩戴者"感知"到周围的知识源——30 尺内任何书籍、卷轴、或知识性物品会被自动感知。一位后来的学者在描述这顶帽子时说："它不是帽子，它是'知识的天线'——戴上它，你就能接收到来自四面八方的知识信号。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "eternal_apprentice_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_eternal_apprentice_head" },
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_eternal_apprentice_head" }
]
```

**特殊效果（设计预留）**：学习新技能或语言的时间减半。

---

### 18.2 永恒学徒学者袍（Eternal Apprentice Scholar Robe）

```gdscript
item_id = "armor_eternal_apprentice_body"
display_name = "永恒学徒学者袍"
description = "一件由知识之布与无限丝线编织而成的奇异长袍，袍身不断有微型公式和符文在流动——不是装饰，而是被封印在布料中的"知识之海"在活动。长袍的颜色会随佩戴者的知识领域而变化——红色代表炼金术，蓝色代表占星术，绿色代表自然学，紫色代表奥术。\n\n这件学者袍是索克拉底在创建无限之塔后，用收集到的所有学科的知识碎片编织的。他说："知识不是孤立的，它是相互连接的——炼金术与占星术有关，占星术与数学有关，数学与音乐有关。这件长袍承载着所有学科的连接，穿着它的人，就是在穿着整个知识体系。"他在长袍内侧缝了四十九个大口袋，每个口袋中都藏着一本微型书籍——从最古老的史诗到最新的研究报告。\n\n学者袍会在佩戴者进行知识检定时自动激活——每日三次，可以在任何知识检定上获得 advantage（知识的加持）。且长袍可以让佩戴者"阅读"任何语言——即使是从未见过的古代语言或外星文字，也能够理解其基本含义（如同 comprehend languages 的常驻版）。一位后来的学者说："穿着这件长袍，我感觉自己不是在穿衣服，我是在'穿着'整个图书馆。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "eternal_apprentice_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 24000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_eternal_apprentice_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_eternal_apprentice_body" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_eternal_apprentice_body" }
]
```

**特殊效果（设计预留）**：可以理解任何书面语言，且所有知识检定+2。

---

### 18.3 永恒学徒书记护手（Eternal Apprentice Scribe Gauntlets）

```gdscript
item_id = "armor_eternal_apprentice_hands"
display_name = "永恒学徒书记护手"
description = "一对由知识之布与无限丝线编织而成的奇异手套，手套表面不断有微型文字在指尖流动——不是装饰，而是被封印在手套装的"书写之力"在活动。手套的指尖各嵌有一枚微型知识水晶——每一枚水晶都可以将佩戴者的思想直接转化为文字。\n\n这对手套是索克拉底在创建无限之塔后，用自己的双手与知识之布一同编织的。他说："书写是思考的延伸——当你将思想转化为文字时，你不仅在记录，你也在理解。这对手套让我能够将思想直接转化为最完美的文字，不需要任何媒介。"他在手套上缝了七道"书写封印"——每一道封印都代表一种不同的书写技巧。\n\n手套会在佩戴者进行书写或记录时自动激活——书写速度提升十倍，且可以完美复制任何见过的文字或图案（如同复印机）。且可以通过手套「知识传输」——每日一次，将一种知识直接"传输"给另一个生物（如同 telepathic bond 的单向版，持续 10 分钟，传输一种技能或知识）。一位后来的学者说："戴上这对手套，我不是在写字，我是在'打印'——思想直接变成文字，没有任何中间步骤。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "eternal_apprentice_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_eternal_apprentice_hands" },
    { attribute_id = "history_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_eternal_apprentice_hands" }
]
```

**特殊效果（设计预留）**：可以完美复制任何文字或图案，且每日一次传输一种知识给另一个生物。

---

### 18.4 永恒学徒求知靴（Eternal Apprentice Seeker Boots）

```gdscript
item_id = "armor_eternal_apprentice_feet"
display_name = "永恒学徒求知靴"
description = "一对由知识之布与无限丝线编织而成的奇异软靴，靴底刻有微型知识符文——每一步踏下，都会在地面上留下一个短暂的"知识印记"，任何踩到这个印记的生物都会获得一个随机的小知识（例如："你知道了苹果含有维生素C"或"你知道了古代的货币系统"）。\n\n这对软靴是索克拉底在创建无限之塔后，用自己的双脚与知识之布一同编织的。他说："知识不应该被关在图书馆里，它应该被传播——每一步都是一个传播知识的机会。"他在靴底刻下了三百六十五个微型知识符文——每一个都代表一个不同的小知识。\n\n软靴的特殊效果是：佩戴者在行走时会自动留下知识印记——30 尺内所有盟友踩到印记时，可以在一次知识检定上获得 +2（知识的传播）。且每日一次，可以通过软靴「知识冲刺」——移动力翻倍，且在移动过程中可以"吸收"周围的知识（自动识别 30 尺内所有知识性物品和隐藏信息）。一位后来的学者说："穿着这对靴子，我不是在走路，我是在'播种'——每一步都播下一颗知识的种子，让后来者在无意中收获。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "eternal_apprentice_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_eternal_apprentice_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_eternal_apprentice_feet" }
]
```

**特殊效果（设计预留）**：移动时留下知识印记，盟友获得知识检定加值。

---

## 套装十九：血月猎人（Blood Moon Hunter）护甲

> *"我不是为了复仇而狩猎，我是为了防止其他人经历我所经历的。"*

**套装主题**：猎魔人公会「银牙」的传承装备。这个公会在一百五十年前的一次血月之夜被狼人部落覆灭，但公会最精锐的猎人们的装备因为浸透了狼人之血而获得了狩猎狼人的特异能力。

**历史渊源**：银牙的创始人「第一猎人」范海辛，是一位被狼人摧毁了全家的孤独猎手。

---

### 19.1 血月猎人猎人头盔（Blood Moon Hunter Hunter Helm）

```gdscript
item_id = "armor_blood_moon_hunter_head"
display_name = "血月猎人猎人头盔"
display_name = "血月猎人猎人头盔"
description = "一顶由银钢与狼人骨骼熔铸而成的奇异头盔，头盔呈现出银白色与血红色的混合色——在月光下会折射出令人不安的虹彩。头盔的面甲是一块能够增强夜视的银色水晶，佩戴者可以通过这块水晶在黑暗中看清 120 尺，且可以看透狼人的变形伪装。\n\n这顶头盔是范海辛在创建银牙后，用他猎杀的第一头狼人的头骨与银钢一同熔铸的。他说："这头狼人摧毁了我的家庭，现在它的骨头将保护其他人免受同样的命运。"他在头盔内侧刻下了银牙的第一条守则："我们不猎杀野兽，我们猎杀'披着人皮的野兽'。"\n\n头盔会在血月之夜自动激活——银色水晶会变成血红色，提供对狼人和变形生物的 true sight（可以看出所有 shapechange 和 polymorph）。且头盔可以让佩戴者"感知"到 60 尺内所有狼人和变形生物的位置（猎人之感）。一位后来的猎人在描述这顶头盔时说："它不是头盔，它是'猎人的眼睛'——戴上它，你就能在人群中一眼认出那些'披着人皮的野兽'。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "blood_moon_hunter_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_blood_moon_hunter_head" },
    { attribute_id = "perception_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_blood_moon_hunter_head" }
]
```

**特殊效果（设计预留）**：在月光下，感知检定+3，且可以看穿狼人的变形伪装。

---

### 19.2 血月猎人猎装（Blood Moon Hunter Hunter Coat）

```gdscript
item_id = "armor_blood_moon_hunter_body"
display_name = "血月猎人猎装"
description = "一件由银钢链甲与狼人皮革拼接而成的中型铠甲，铠甲表面覆盖着一层能够反射月光的微型银片——在月光下，铠甲会发出耀眼的银白色光芒，让狼人和变形生物感到恐惧。铠甲的内衬缝有七道银牙封印——每一道封印都代表一种对付狼人的特殊技巧。\n\n这件猎装是范海辛在银牙覆灭前，用他收集到的所有狼人皮革与银牙封印制作的最后一件作品。他说："这件猎装不是为了防御，它是'宣言'——告诉所有狼人，猎人们还在，银牙还在。"他在猎装上缝了四十九个银片——每一片都来自一头被他猎杀的狼人，每一片都代表着一次胜利。\n\n猎装会在血月之夜自动激活——银片会发出刺目的光芒，30 尺内所有狼人和变形生物须通过 DC14 智慧豁免，失败则 frightened 1 回合（银光的恐惧）。且铠甲可以让佩戴者对狼人和变形生物的攻击检定 +2（猎人的专注）。一位后来的猎人在描述这件猎装时说："穿着它，我感觉自己不是在穿衣服，我是在'穿着'所有前辈的胜利——每一片银片都是一位猎人的灵魂，他们在保护我，指引我。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "medium_armor", "blood_moon_hunter_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 27000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_blood_moon_hunter_body" },
    { attribute_id = "resistance_slashing", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_blood_moon_hunter_body" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_blood_moon_hunter_body" }
]
```

**特殊效果（设计预留）**：对狼人和变形生物的伤害+1D6 silver。

---

### 19.3 血月猎人银牙手套（Blood Moon Hunter Silver Fang Gloves）

```gdscript
item_id = "armor_blood_moon_hunter_hands"
display_name = "血月猎人银牙手套"
description = "一对由银钢与狼人獠牙锻造的奇异手套，手套的指尖各嵌有一枚可伸缩的银质獠牙——每一枚獠牙都可以独立转动，可以用来攀爬、抓握、甚至攻击。手套内侧涂有微量的"银毒"——一种对狼人和变形生物致命的毒素，但对人类无害。\n\n这对手套是范海辛在创建银牙后，用他猎杀的第一头狼人的獠牙制作的。他说："这头狼人用它獠牙杀死了我的妻子和孩子，现在它的獠牙将成为我保护其他人的工具。"他将獠牙与银钢结合，制作成了这对手套——银质的獠牙对狼人有天然的克制作用。\n\n手套会在佩戴者进行近战攻击时自动激活——每日三次，可以释放银牙攻击（5 尺射程 1D8 piercing + 1D6 radiant，对狼人和变形生物双倍）。且在进行追踪检定时，检定 +3（猎人的直觉）。范海辛说："追踪不是技术，它是直觉——当你足够了解你的猎物时，你就能预测它的每一步。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "blood_moon_hunter_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_blood_moon_hunter_hands" },
    { attribute_id = "survival_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_blood_moon_hunter_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击视为银质武器，对狼人伤害翻倍。

---

### 19.4 血月猎人追踪靴（Blood Moon Hunter Tracker Boots）

```gdscript
item_id = "armor_blood_moon_hunter_feet"
display_name = "血月猎人追踪靴"
description = "一对由狼人皮革与银钢制成的奇异软靴，靴底覆盖着一层能够感知地面震动的微型银片——在追踪时，靴底会根据猎物的重量、步幅和方向微微发热或发冷——热代表正确方向，冷代表错误方向。靴筒内侧刻有七道追踪符文。\n\n这对软靴是范海辛在创建银牙后，用他猎杀的第一头狼人的皮革制作的。他说："这头狼人曾经用它四足的敏捷逃避过我的追踪，现在它的皮革将帮助我不再失去任何猎物。"他将皮革与银钢结合，制作成了这对追踪靴——银片可以感知到最微弱的地面震动。\n\n软靴的特殊效果是：在月光下，佩戴者可以追踪任何目标——即使在最复杂的地形上，也不会失去目标的踪迹（如同 hunter's mark 的常驻版，自动追踪最近一个被标记的目标）。且每日一次，可以通过软靴「月光冲刺」——移动力翻倍，且在移动过程中可以"感知"到 30 尺内所有隐藏的生物（猎人之眼）。一位后来的猎人说："穿着这对靴子，我不是在走路，我是在'追踪'——每一步都让我更接近猎物。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "blood_moon_hunter_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_blood_moon_hunter_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_blood_moon_hunter_feet" }
]
```

**特殊效果（设计预留）**：在月光下移动力+10，且可以自动追踪一个目标。

---

## 套装二十：锈蚀齿轮（Rusted Gear）护甲

> *"机器不是敌人，它们只是迷路的孩子。"*

**套装主题**：失落文明「齿轮之城」的传承装备。这个文明在两千年前因一场机械反叛而毁灭，但他们的机械装备因为长期暴露在蒸汽和机油中而获得了增强工艺的能力。

**历史渊源**：齿轮之城的最后一位大工程师「齿轮之心」达芬奇，是一位在机械反叛中选择了与机器"对话"而非"战斗"的疯狂发明家。

---

### 20.1 锈蚀齿轮工程师帽（Rusted Gear Engineer Cap）

```gdscript
item_id = "armor_rusted_gear_head"
display_name = "锈蚀齿轮工程师帽"
description = "一顶由黄铜与齿轮碎片锻造的奇异帽子，帽子表面镶嵌着七个微型齿轮——每一个齿轮都可以独立转动，发出微弱的咔嗒声。帽子的形状会随佩戴者正在思考的问题而变化——思考机械问题时变成圆形，思考建筑问题时变成方形，思考抽象问题时变成不规则形状。\n\n这顶工程师帽是达芬奇在齿轮之城鼎盛时期，用他自己的第一顶工作帽与七个关键齿轮一同改造的。他说："思考是齿轮的运转——每一个想法都是一个齿轮，当它们咬合在一起时，思考就变成了创造。"他在帽子上安装了七个微型齿轮——每一个都代表一种不同的思维方式。\n\n工程师帽会在佩戴者进行工艺或发明检定时自动激活——齿轮会按照最优的顺序转动，帮助佩戴者加速思考（工艺检定 +3）。且帽子可以让佩戴者"感知"到 30 尺内所有机械装置的状态——正常运转、故障、或被篡改（机械雷达）。一位后来的工程师在描述这顶帽子时说："它不是帽子，它是'思考的引擎'——戴上它，你的思维就像齿轮一样精确、高效、永不停歇。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "rusted_gear_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_rusted_gear_head" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_rusted_gear_head" }
]
```

**特殊效果（设计预留）**：可以理解任何机械装置的工作原理（自动识别故障和修复方法）。

---

### 20.2 锈蚀齿轮机械铠甲（Rusted Gear Mechanical Armor）

```gdscript
item_id = "armor_rusted_gear_body"
display_name = "锈蚀齿轮机械铠甲"
description = "一件由黄铜板甲与微型机械装置拼接而成的奇异铠甲，铠甲表面不断有微型齿轮、杠杆和活塞在活动——不是装饰，而是真正的机械装置在运作。铠甲的胸甲位置有一个微型蒸汽引擎——它会在佩戴者运动时自动充能，在需要时释放出额外的力量。铠甲非常沉重——因为上面镶嵌了数百个微型机械装置。\n\n这件机械铠甲是达芬奇在齿轮之城鼎盛时期，用他毕生收集的所有微型机械装置制作的巅峰之作。他说："这件铠甲不是被动的防御，它是'活的'——它会思考，会适应，会保护。每一个装置都有它的功能，每一个齿轮都有它的位置。"他在铠甲上安装了三百六十五个微型装置——从能够增强力量的液压杠杆，到能够自动修复损伤的纳米齿轮。\n\n铠甲会在佩戴者受到攻击时自动激活——蒸汽引擎会释放出额外的力量，提供 +2 AC（机械防御，持续 1 回合，每日三次）。且铠甲可以「自我修复」——每小时恢复 1D6 HP（纳米齿轮的修复）。一位后来的工程师在描述这件铠甲时说："穿着它，我感觉自己不是在穿衣服，我是在'驾驶'一台机器——一台专门设计来保护我的机器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "rusted_gear_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 26000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_rusted_gear_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_rusted_gear_body" },
    { attribute_id = "strength_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rusted_gear_body" }
]
```

**特殊效果（设计预留）**：每小时自动恢复 1D6 HP（自我修复），且可以临时增强力量（+2 STR，持续 1 分钟，每日一次）。

---

### 20.3 锈蚀齿轮工具手套（Rusted Gear Tool Gauntlets）

```gdscript
item_id = "armor_rusted_gear_hands"
display_name = "锈蚀齿轮工具手套"
description = "一对由黄铜与微型工具锻造的奇异手套，手套表面不断有微型工具在变换——从螺丝刀到扳手，从钳子到锤子，每一种工具都可以在半秒内从手套中"生长"出来。手套的指尖各嵌有一枚微型传感器——可以检测温度、压力、化学成分和电磁场。\n\n这对手套是达芬奇在齿轮之城鼎盛时期，用他自己的双手与所有基本工具一同改造的。他说："工具是手的延伸——当你有足够多的工具时，你的手就变成了万能的手。"他在手套上安装了四十九种不同的微型工具——每一种都可以独立使用，也可以组合使用。\n\n手套会在佩戴者进行工艺或修理时自动激活——所有工艺检定 +3，且可以在没有外部工具的情况下完成任何基本修理（万能工具）。且可以通过手套「机械操控」——每日三次，触碰一个机械装置，可以立即理解其工作原理并进行修改（如同 modify memory 但对机械装置）。一位后来的工程师说："戴上这对手套，我不是在用手，我是在'使用'整个工具箱——而且工具箱就在我的手上。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "rusted_gear_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rusted_gear_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_rusted_gear_hands" }
]
```

**特殊效果（设计预留）**：可以进行任何基本工艺操作（无需外部工具），且可以临时增强或削弱机械装置。

---

### 20.4 锈蚀齿轮蒸汽靴（Rusted Gear Steam Boots）

```gdscript
item_id = "armor_rusted_gear_feet"
display_name = "锈蚀齿轮蒸汽靴"
description = "一对由黄铜与微型蒸汽引擎制成的奇异重靴，靴底装有微型推进器——在需要时可以释放出蒸汽推力，提供额外的移动力或跳跃高度。靴筒内侧刻有七道机械符文——每一道符文都代表一种不同的机械增强功能。行走时，靴子会发出微弱的蒸汽声和齿轮咔嗒声。\n\n这对重靴是达芬奇在齿轮之城鼎盛时期，用他设计的第一台蒸汽引擎的碎片制作的。他说："蒸汽是力量的源泉——它可以将热能转化为动能，将静止变为运动。这对靴子让我能够将蒸汽的力量掌握在自己脚下。"他在靴底安装了微型推进器——可以在短时间内提供巨大的推力。\n\n重靴的特殊效果是：每日一次，可以激活「蒸汽冲刺」——移动力 +20，且可以跳越三倍正常高度（蒸汽推进，持续 1 分钟）。且在进行力量检定时，检定 +2（蒸汽的助力）。一位后来的工程师说："穿着这对靴子，我不是在走路，我是在'飞行'——蒸汽推着我，齿轮转动着，我感觉自己就是一台机器。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "rusted_gear_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_rusted_gear_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_rusted_gear_feet" }
]
```

**特殊效果（设计预留）**：每日一次蒸汽冲刺（移动力+20，跳跃三倍高度）。

---

*套装 16–20 护甲部分完结 · 共 20 件护甲装备*

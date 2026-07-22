# 传奇装备套装设计文档（套装 1–5）

> 共 20 件护甲装备，覆盖 head / body / hands / feet 四个部位。
> 每件装备均包含独立历史渊源、外观描述及完整 ItemDef 字段。

---

## 套装一：晨光圣骑士（Dawn Paladin）

> *"当第一缕阳光穿透永夜，圣骑士的铠甲便是最坚固的黎明。"*

**套装主题**：古老的圣骑士团「晨曦之手」遗留的圣物。该骑士团在三百年前的一次恶魔入侵中全员殉道，他们的装备被熔入晨光之力，散落在要塞的废墟中。集齐四件时，穿戴者在日出时分会获得「黎明庇护」——周身环绕淡金色光晕，对邪恶生物的伤害额外提升。

**历史渊源**：晨曦之手的创始人「第一黎明」埃拉里克，在末日火山口以自身为媒介，将初升太阳的第一道光芒封入铠甲。他留下遗言："当黑暗再次笼罩大地，穿上这套铠甲的人，将成为新的黎明。"

---

### 1.1 晨光圣骑士头盔（Dawn Paladin Helm）

```gdscript
item_id = "armor_dawn_paladin_helm"
display_name = "晨光圣骑士头盔"
description = "一顶覆盖着晨光祝福的全覆式头盔。面甲上刻有晨曦之手的纹章——一轮从地平线升起的太阳，光芒化作剑刃。\n\n三百年前，恶魔大军攻破要塞东门，圣骑士团团长埃拉里克率领十二名骑士发起最后一次冲锋。他们的呐喊被晨光照亮，他们的鲜血染红了铠甲。战后，幸存的铁匠将他们的遗甲熔铸为四件圣物，封存了那道永不熄灭的晨光。\n\n据说，当穿戴者在日出时戴上这顶头盔，会听到远方传来十二位骑士的战歌——那不是幻觉，而是被封印在金属中的灵魂共鸣。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "dawn_paladin_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 18000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dawn_paladin_helm" },
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dawn_paladin_helm" }
]
```

**特殊效果（设计预留）**：日出后1小时内，对恶魔/亡灵类型生物的攻击检定+1。

---

### 1.2 晨光圣骑士胸甲（Dawn Paladin Breastplate）

```gdscript
item_id = "armor_dawn_paladin_body"
display_name = "晨光圣骑士胸甲"
description = "一副由秘银与圣钢混铸的全身胸甲，胸甲正面镶嵌着一枚晨曦水晶——那是一颗在末日火山口被晨光照射了七天七夜才形成的宝石，内部封存着永不消散的光芒。\n\n胸甲的内衬上绣有晨曦之手的骑士誓言，以金线绣就："以光之名，守护至暗；以血为誓，不退一步。"据说，当穿戴者面临致命一击时，胸甲会发出刺目的光芒，将攻击偏转——那不是铠甲的物理防御，而是十二位殉道骑士的灵魂在代为承受。\n\n第一任团长埃拉里克的胸甲上有一道永远无法修复的裂痕，那是他用身体挡住恶魔领主「蚀日者」的利爪时留下的。后来的铸造师们特意保留了这道裂痕，他们说："完美的铠甲没有灵魂，这道裂痕才是它最珍贵的部分。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "dawn_paladin_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 32000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_dawn_paladin_body" },
    { attribute_id = "resistance_radiant", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_dawn_paladin_body" }
]
```

**特殊效果（设计预留）**：HP降至20%以下时，触发「殉道者之光」——自身及10尺内盟友获得10点临时HP（每日一次）。

---

### 1.3 晨光圣骑士护手（Dawn Paladin Gauntlets）

```gdscript
item_id = "armor_dawn_paladin_hands"
display_name = "晨光圣骑士护手"
description = "一双沉重的精钢护手，指关节处各镶嵌着一枚微型晨曦符文。握紧拳头时，符文会发出微弱的金色光芒，仿佛在回应穿戴者的决心。\n\n这双护手曾属于晨曦之手最年轻的骑士「晨星」塞拉芬。塞拉芬只有十七岁，却在末日之战中独自守住了要塞的侧门长达三小时，为平民撤离争取了宝贵时间。当援军赶到时，他已经力竭倒下，双手仍然死死扣住门闩。他的护手被血浸透，但指关节处的符文依然发光——那是他最后一丝意志的残留。\n\n后来的铸造师将他的护手与其他骑士的装备一同熔铸，但保留了指关节处的符文。他们说："塞拉芬的握力比任何钢铁都坚固。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "dawn_paladin_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dawn_paladin_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dawn_paladin_hands" }
]
```

**特殊效果（设计预留）**：对 grabbed/restrained 目标进行力量检定时+2。

---

### 1.4 晨光圣骑士胫甲（Dawn Paladin Greaves）

```gdscript
item_id = "armor_dawn_paladin_feet"
display_name = "晨光圣骑士胫甲"
description = "一对覆盖小腿的精钢胫甲，靴底刻有防滑纹路和晨曦之手的太阳纹章。每一步踏下，都会在石质地面上留下淡淡的金色足迹，数秒后才消散。\n\n这对胫甲曾属于晨曦之手的「冲锋者」巴尔达斯——一位以速度著称的重甲骑士。传说他能在全身重甲的情况下跑得比轻骑兵还快，因为他相信"速度即是正义"。在末日之战的最后时刻，巴尔达斯背负着受伤的团长埃拉里克冲向恶魔领主，用自己的身体挡住了致命一击。他倒下前说的最后一句话是："团长，别停下，黎明还在前面。"\n\n铸造师将他的胫甲与其他装备熔铸时，发现靴底的纹路已经被磨得几乎平滑——那是无数次冲锋留下的痕迹。他们保留了这双靴子，说："最好的铠甲不是防御，而是让你能跑得更快。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "dawn_paladin_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 14000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dawn_paladin_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_dawn_paladin_feet" }
]
```

**特殊效果（设计预留）**：冲锋时，移动力额外+10尺，且第一次近战攻击伤害+1D6 radiant。

---

## 套装二：暗影刺客（Shadow Assassin）

> *"在光与影的交界处，死亡是最安静的访客。"*

**套装主题**：「无声之刃」刺客公会的传承装备。这个公会在两百年前因内部政变而覆灭，他们的装备被叛徒分散藏匿。集齐四件时，穿戴者在阴影中获得「无形之姿」——身形与背景融为一体，几乎无法被肉眼察觉。

**历史渊源**：无声之刃的创始人「无声者」维克多，原本是一位宫廷乐师。他在目睹国王被毒杀后，发誓要用最安静的方式伸张正义。他将自己的音乐天赋转化为暗杀技艺——"最好的暗杀是没有声音的，就像一首没有音符的乐曲。"

---

### 2.1 暗影刺客面罩（Shadow Assassin Mask）

```gdscript
item_id = "armor_shadow_assassin_head"
display_name = "暗影刺客面罩"
description = "一张由夜影蚕丝编织而成的黑色面罩，仅露出双眼。面罩内侧绣有无声之刃的暗语："声音是敌人，寂静是朋友。"\n\n维克多亲手制作了第一副面罩，用的是从南方沙漠运来的夜影蚕丝——据说这种蚕只在无月之夜吐丝，吐出的丝线比头发还细，却能吸收所有光线。维克多在面罩内衬绣上了自己的暗语，然后戴着它完成了第一次暗杀：他用一根琴弦勒死了毒杀国王的宰相，整个过程没有发出任何声音。\n\n后来，无声之刃的每一任首领都会在这副面罩上添加一层新的丝线。到公会覆灭时，面罩已经积累了二十七层丝线，每一层都代表着一次完美的暗杀。覆灭之夜，叛徒割下了首领的面罩作为战利品，但面罩上的二十七层丝线突然全部断裂——那是二十七个亡灵的诅咒。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "leather", "light_armor", "shadow_assassin_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_assassin_head" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shadow_assassin_head" }
]
```

**特殊效果（设计预留）**：在 dim light 或 darkness 中，隐匿检定+2。

---

### 2.2 暗影刺客皮甲（Shadow Assassin Leather）

```gdscript
item_id = "armor_shadow_assassin_body"
display_name = "暗影刺客皮甲"
description = "一件由黑龙幼龙的腹皮鞣制而成的紧身皮甲，表面经过特殊处理，能够吸收光线和声音。皮甲内侧缝有二十七个小口袋，每个口袋中都藏着一种不同的毒剂。\n\n这件皮甲的原型是维克多用第一条被他猎杀的黑龙幼龙的皮制作的。那条幼龙只有三个月大，维克多花了整整一个月跟踪它，学习它的呼吸节奏和移动模式。最后，他在幼龙熟睡时用一根涂了麻痹毒素的细针刺穿了它的逆鳞——整个过程，幼龙甚至没有醒来。\n\n无声之刃的每一任首领都会在皮甲上添加一层新的皮革。到公会覆灭时，皮甲已经叠加了九层皮革，每一层都来自一种不同的黑暗生物：黑龙、暗影豹、夜行蛇、深渊蜘蛛……覆灭之夜，叛徒刺穿了皮甲，但每一层皮革都释放出了不同的毒素——那是九种黑暗生物的复仇。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "leather", "light_armor", "shadow_assassin_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 28000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shadow_assassin_body" },
    { attribute_id = "dodge_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_shadow_assassin_body" }
]
```

**特殊效果（设计预留）**：从隐匿状态发动的第一次攻击，伤害+1D6 necrotic。

---

### 2.3 暗影刺客手套（Shadow Assassin Gloves）

```gdscript
item_id = "armor_shadow_assassin_hands"
display_name = "暗影刺客手套"
description = "一双薄如蝉翼的黑色皮手套，指尖嵌有可伸缩的精钢利爪。手套内侧涂有微量的麻痹毒素，能够在触碰时让目标逐渐失去知觉。\n\n这双手套是无声之刃的「毒师」莉莉丝发明的。莉莉丝原本是一位宫廷药剂师，因为发现国王的御医在慢性毒杀国王而被灭口。她逃入地下，将药剂学转化为毒理学，发明了二十七种不同的暗杀毒素。这双手套是她最后的杰作——指尖的利爪可以在三秒内刺穿锁子甲的缝隙，而内侧的麻痹毒素可以让目标在不知不觉中失去反抗能力。\n\n莉莉丝死于自己的毒素。她在一次暗杀中不小心割破了自己的手套，毒素渗入她的血液。她在临死前将这双手套交给了维克多，说："最好的暗杀者，不是杀死目标的人，而是让目标在微笑中死去的人。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "leather", "light_armor", "shadow_assassin_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_assassin_hands" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_shadow_assassin_hands" }
]
```

**特殊效果（设计预留）**：从背后攻击时，攻击检定+2。

---

### 2.4 暗影刺客软靴（Shadow Assassin Boots）

```gdscript
item_id = "armor_shadow_assassin_feet"
display_name = "暗影刺客软靴"
description = "一双由暗影豹的足底软皮制成的软靴，靴底覆盖着细密的肉垫纹路，能够在任何地面上行走而不发出声音。靴筒内侧缝有小型匕首鞘，可以在半秒内抽出隐藏的匕首。\n\n这双软靴属于无声之刃最快的刺客「影子」莫里安。莫里安能在 candlelight 下穿过满是落叶的庭院而不发出任何声音——不是因为他轻，而是因为他懂得"顺应地面的节奏"。他说："不要踩在落叶上，要踩在落叶之间的空隙；不要走在木板中央，要走在木板与木板的接缝处——那里是最安静的地方。"\n\n莫里安死于一次过于自信的暗杀。他潜入了目标的书房，却发现目标正在等他——书桌上放着一封写给他本人的信，信上只有一句话："我知道你会踩在第三块木板的左边。"那是他唯一一次踩错了地方。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "light_armor", "shadow_assassin_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_shadow_assassin_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_shadow_assassin_feet" }
]
```

**特殊效果（设计预留）**：移动不引发借机攻击（如同 disengage）。

---

## 套装三：霜冻守望者（Frost Warden）

> *"在北风的尽头，守望者用冰霜铸就了永恒的城墙。"*

**套装主题**：北境要塞「永冻堡」守军的传承装备。这座要塞在百年前的一次冰龙袭击中被彻底冰封，所有守军与装备一同被冻结在时间中。集齐四件时，穿戴者获得「冰封之心」——免疫寒冷伤害，且周围敌人移动力降低。

**历史渊源**：永冻堡的指挥官「冰墙」索拉娜，是一位来自南方却深深爱上北境的女战士。她在冰龙「霜喉」袭击时，用自身的生命之力激活了要塞的古老冰封法阵，将整个要塞冻结在永恒的瞬间。她说："如果死亡是不可避免的，那就让死亡成为守护。"

---

### 3.1 霜冻守望者头盔（Frost Warden Helm）

```gdscript
item_id = "armor_frost_warden_head"
display_name = "霜冻守望者头盔"
description = "一顶由永冻堡深处的玄冰铁锻造的角盔，两侧各有一支向上弯曲的冰晶犄角。面甲上覆盖着一层永不融化的薄霜，呼出的气息会在面甲内侧凝结成冰花。\n\n这顶头盔原属于永冻堡的第一任守望者「冰墙」索拉娜。索拉娜来自温暖的南方，却在第一次踏上北境冻原时就爱上了这片冷酷的土地。她说："南方的温暖让人软弱，北方的寒冷让人坚强。"她用永冻堡深处的玄冰铁打造了这顶头盔，并在两侧各加装了一支冰晶犄角——那不是装饰，而是"天线"，能够感知方圆数里内的温度变化。\n\n在霜喉袭击的那个夜晚，索拉娜戴着这顶头盔站在要塞最高的塔楼上，通过犄角感知到了远方异常的低温。她立即下令启动冰封法阵，但为时已晚——霜喉的吐息已经笼罩了整个要塞。索拉娜在最后一刻将头盔抛给了最近的副官，说："记住，守望者的使命不是战胜死亡，而是拖延它。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "frost_warden_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_frost_warden_head" },
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_frost_warden_head" }
]
```

**特殊效果（设计预留）**：感知检定+2（冰晶犄角感知温度变化）。

---

### 3.2 霜冻守望者链甲（Frost Warden Mail）

```gdscript
item_id = "armor_frost_warden_body"
display_name = "霜冻守望者链甲"
description = "一副由玄冰铁环与霜巨人毛发编织而成的链甲，每一环铁环上都刻有微型的冰封符文。链甲表面覆盖着一层薄薄的冰霜，触碰时不会融化，反而会让人感到一种刺骨的清醒。\n\n这副链甲是永冻堡的铸造大师「寒铁」格里姆用了一整年时间打造的。他用永冻堡深处的玄冰铁锻造成环，再用霜巨人战利品中的毛发作为连接线。每一环铁环上都刻有冰封符文——那是他从一本上古卷轴中学到的古老文字，据说能够"冻结时间"。\n\n格里姆在链甲完成的那天夜里消失了。有人在要塞最深处的冰窖中发现了他——他被冻在一块完美的冰块中，脸上带着微笑。他的手中握着一块未完成的铁环，上面只刻了半个符文。后来的守望者们说："格里姆终于完成了他的 masterpiece——他把自己也刻进了冰封符文里。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "frost_warden_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 30000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_frost_warden_body" },
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_frost_warden_body" }
]
```

**特殊效果（设计预留）**：受到火焰伤害时，有50%概率将一半伤害转化为寒冷伤害（冰封符文中和）。

---

### 3.3 霜冻守望者臂铠（Frost Warden Bracers）

```gdscript
item_id = "armor_frost_warden_hands"
display_name = "霜冻守望者臂铠"
description = "一对覆盖前臂的玄冰铁臂铠，手背处各嵌有一块霜巨人心脏的碎片。握紧拳头时，碎片会发出幽蓝色的微光，周围的空气温度骤降。\n\n这对臂铠属于永冻堡最强的战士「冰拳」托里克。托里克在一次与霜巨人的战斗中，徒手击碎了对方的心脏——不是因为他力量大，而是因为他的拳头已经被寒冷冻得比钢铁还硬。他将霜巨人心脏的碎片嵌入臂铠，说："敌人最强大的部分，应该成为我们的盾牌。"\n\n托里克在霜喉袭击时站在要塞大门前，用双臂挡住了冰龙的第一次吐息。他的臂铠结满了冰霜，但他没有后退一步。当冰封法阵最终启动时，他的身体已经与大门冻在了一起，双臂仍然保持着阻挡的姿势。后来的守望者们每年都会在解冻日去擦拭他的臂铠，他们说："托里克还在守门，我们只是替他擦擦灰尘。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "frost_warden_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_warden_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_warden_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击或金属武器攻击附加1D4寒冷伤害。

---

### 3.4 霜冻守望者冰靴（Frost Warden Boots）

```gdscript
item_id = "armor_frost_warden_feet"
display_name = "霜冻守望者冰靴"
description = "一对由玄冰铁与极地冰熊毛皮制成的厚底靴，靴底刻有防滑的冰晶纹路。在冰面上行走时，靴底会微微发热以防止滑倒；在雪地上行走时，靴底会微微降温以防止下陷。\n\n这对冰靴属于永冻堡最好的斥候「雪狐」艾拉。艾拉能在暴风雪中行走三天三夜而不迷失方向，不是因为她有地图，而是因为她懂得"倾听雪的声音"。她说："每一片雪花落地的声音都不一样——硬雪的声音清脆，软雪的声音沉闷，冰层下面的声音空洞。只要懂得听，雪原就是一张巨大的地图。"\n\n艾拉在霜喉袭击前三天就感知到了异常。她追踪着空气中异样的寒冷，一路走到了冰龙的巢穴，亲眼看到了正在蓄势的霜喉。她跑回要塞报信，但冰龙的吐息比她更快。她在距离要塞大门十尺的地方被冻结成了冰雕，脸上仍然保持着奔跑的表情。后来的守望者们说："艾拉从未停止奔跑，她只是换了一种方式。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "frost_warden_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_frost_warden_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_frost_warden_feet" }
]
```

**特殊效果（设计预留）**：在冰面或雪地上移动力不受地形影响。

---

## 套装四：烈焰法师（Flame Mage）

> *"火焰不是毁灭，而是净化。灰烬之下，新的生命正在萌发。"*

**套装主题**：烈焰学派「灰烬之环」法师们的传承装备。这个学派在八十年前的一场魔法事故中被焚毁，但学派创始人的装备在烈焰中得到了升华。集齐四件时，穿戴者获得「火焰亲和」——火焰法术伤害提升，且免疫自身火焰伤害。

**历史渊源**：灰烬之环的创始人「灰烬之子」伊格尼斯，原本是一位被正统法师学院开除的"问题学生"。他坚信火焰不是破坏之力，而是"转化的媒介"。他在一次失控的实验中焚毁了自己的实验室，但在灰烬中发现了一套被火焰重塑的装备——那是火焰对他的"认可"。

---

### 4.1 烈焰法师兜帽（Flame Mage Hood）

```gdscript
item_id = "armor_flame_mage_head"
display_name = "烈焰法师兜帽"
description = "一顶由火焰精华编织而成的深红色兜帽，兜帽边缘不断有微小的火星跳动，但不会引燃周围的物体。兜帽内侧绣有灰烬之环的核心咒语——那不是用文字写就的，而是用烧焦的痕迹留下的。\n\n伊格尼斯被正统法师学院开除的那天，他烧掉了自己的所有笔记，只留下一顶被火焰烧毁边缘的兜帽。他说："文字是弱者的工具，真正的法师用火焰书写。"他戴着自己烧焦的兜帽，走进了沙漠深处的火山，在岩浆池中冥想了一百日。当他走出火山时，他的兜帽已经不再燃烧，但边缘永远残留着火焰的痕迹——那是他与火焰达成的契约。\n\n据说，当伊格尼斯戴上这顶兜帽施法时，兜帽上的火星会按照施法的节奏跳动。一位目击者曾说："那不是魔法，那是火焰在跳舞。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "light_armor", "flame_mage_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flame_mage_head" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flame_mage_head" }
]
```

**特殊效果（设计预留）**：火焰法术伤害+1D4。

---

### 4.2 烈焰法袍（Flame Mage Robe）

```gdscript
item_id = "armor_flame_mage_body"
display_name = "烈焰法袍"
description = "一件由凤凰灰烬与炎魔丝绸混织而成的深红色法袍，袍身不断有火焰纹路在布料上游走，如同活物。法袍的腰带扣是一枚凝固的火焰精华，触碰时温暖而不灼人。\n\n这件法袍是伊格尼斯在火山冥想第一百日时，由一只垂死的凤凰赠予他的。那只凤凰在火山中涅槃失败，它的灰烬与伊格尼斯的意志产生了共鸣，自动编织成了这件法袍。凤凰在完全消散前对伊格尼斯说："火焰不是毁灭，而是重生。穿上这件法袍，你就是火焰的使者。"\n\n伊格尼斯穿着这件法袍创建了灰烬之环，招收了第一批学徒。但他在一次教授"终极转化"法术时失控了——法袍上的火焰纹路突然爆发，将整个学派焚毁。伊格尼斯在火焰中既没有惨叫也没有逃跑，他只是微笑着张开双臂，说："看，这就是转化的真谛。"当火焰熄灭时，他的身体化为了灰烬，但法袍完好无损，上面的火焰纹路比以前更加鲜艳。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "light_armor", "flame_mage_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 6
base_price = 26000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_flame_mage_body" },
    { attribute_id = "resistance_fire", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_flame_mage_body" },
    { attribute_id = "max_mana", mode = "flat", value = 20, source_type = "equipment", source_id = "armor_flame_mage_body" }
]
```

**特殊效果（设计预留）**：HP降至0时，触发「凤凰涅槃」——恢复20%最大HP并释放一圈火焰冲击（3D6 fire damage，15尺半径），每日一次。

---

### 4.3 烈焰法师护腕（Flame Mage Bracers）

```gdscript
item_id = "armor_flame_mage_hands"
display_name = "烈焰法师护腕"
description = "一对由凝固的岩浆与符文皮革制成的护腕，护腕表面刻有灰烬之环的二十七道火焰符文。施法时，符文会依次亮起，如同一串被点燃的导火索。\n\n这对护腕是伊格尼斯在创建灰烬之环后，用火山深处的凝固岩浆亲手雕刻的。他花了三年时间，在护腕上刻下了二十七道火焰符文——每一道符文代表一种火焰的形态：营火、烛火、野火、地狱火、凤凰火……他说："不懂得尊重火焰的所有形态的人，不配操控火焰。"\n\n在学派焚毁的那一天，护腕上的二十七道符文同时亮起，形成了一条完整的火焰之链。伊格尼斯在临终前将护腕抛向了门外的一名学徒，说："记住这二十七道符文，但不要犯我的错误——转化的对象，永远应该是自己，而不是他人。"那名学徒后来成为了灰烬之环唯一的幸存者。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "light_armor", "flame_mage_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flame_mage_hands" },
    { attribute_id = "spell_damage_fire", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_flame_mage_hands" }
]
```

**特殊效果（设计预留）**：施放火焰法术后，下回合施法速度+1（若为回合制，则为下次施法不消耗 bonus action）。

---

### 4.4 烈焰法师便鞋（Flame Mage Sandals）

```gdscript
item_id = "armor_flame_mage_feet"
display_name = "烈焰法师便鞋"
description = "一对由不燃之木与炎魔皮革制成的轻便便鞋，鞋底刻有火焰跳跃符文。行走时，每一步都会在身后留下一个转瞬即逝的火脚印，数秒后才消散。\n\n这对便鞋是伊格尼斯在火山冥想时穿的。不燃之木来自火山口唯一存活的一棵古树，那棵树在岩浆中生长了千年，它的木材永远不会燃烧。伊格尼斯用这棵树的木材制作了鞋底，用炎魔的皮革制作了鞋面，然后在鞋底刻下了火焰跳跃符文——那不是用来攻击的，而是用来"逃离"的。\n\n伊格尼斯常说："最好的法师不是能打败敌人的法师，而是能在敌人反应过来之前就已经离开的法师。"他在学派焚毁的那一天，本来可以凭借火焰跳跃符文逃离，但他选择了留下。他说："有些转化，必须亲自体验。"他的便鞋在火焰中完好无损，鞋底的火焰跳跃符文比以前更加明亮——那是火焰对他的最后馈赠。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "light_armor", "flame_mage_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_flame_mage_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_flame_mage_feet" }
]
```

**特殊效果（设计预留）**：被火焰伤害击杀时，自动传送到30尺内最近的安全位置（每日一次）。

---

## 套装五：大地守护者（Earth Warden）

> *"大地的力量不是攻击，而是承受。千年不移，万世不改。"*

**套装主题**：远古德鲁伊结社「磐石之环」的传承装备。这个结社信奉大地本身即为最高神明，他们的装备由大地最古老的部分制成。集齐四件时，穿戴者获得「大地之躯」——站立不动时AC额外提升，且无法被击倒或推离原位。

**历史渊源**：磐石之环的第一位大德鲁伊「不动者」格朗姆，是一位放弃了一切移动的石巨人。他在一座山谷中站立了三千年，直到身体与周围的岩石完全融为一体。他在「石化"前说："我将成为大地的一部分，大地将成为我的铠甲。"

---

### 5.1 大地守护者头冠（Earth Warden Crown）

```gdscript
item_id = "armor_earth_warden_head"
display_name = "大地守护者头冠"
description = "一顶由远古花岗岩雕刻而成的沉重头冠，表面布满了苔藓和地衣。头冠上没有金属装饰，只有自然形成的石英结晶，在光线下会折射出微弱的彩虹。\n\n这顶头冠原属于磐石之环的第一位大德鲁伊格朗姆。格朗姆原本是一位石巨人，他在一次与地震之神的对峙中，发誓绝不移动一步。他站在一座山谷中，任凭风吹雨打、地震山摇，始终未曾移动。三千年后，他的身体已经与周围的岩石融为一体，只有这顶头冠还保持着最初的形状——因为那是他在"石化"前最后一刻从自己的头顶取下的，他说："这是我最后的'移动'，也是我最后的'自由'。"\n\n头冠上的苔藓和地衣是三千年来自然生长的，它们与头冠形成了一种奇妙的共生关系。后来的德鲁伊们发现，佩戴这顶头冠时，能够感知到方圆数里内地下的震动——那不是魔法，而是苔藓和地衣的根系在传递信息。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "earth_warden_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earth_warden_head" },
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earth_warden_head" }
]
```

**特殊效果（设计预留）**：感知检定+2（感知地下震动）。

---

### 5.2 大地守护者板甲（Earth Warden Plate）

```gdscript
item_id = "armor_earth_warden_body"
display_name = "大地守护者板甲"
description = "一副由山岭巨人的遗骨与远古陨铁熔铸而成的重型板甲，甲面上镶嵌着十二块来自不同地质年代的石板。每一块石板都代表一个地质纪元，上面刻有那个时代的生物化石。\n\n这副板甲是磐石之环的铸造师「石心」德尔瓦用了一生时间打造的。他走遍了世界的十二座最古老的山脉，从每一块山脉的核心取出了一块石板。他说："大地的历史比任何文明都悠久，穿戴这副铠甲的人，就是穿戴了整个世界。"\n\n德尔瓦在板甲完成的第二天就去世了。他的学徒们在为他举行葬礼时，发现他的身体已经开始石化——不是疾病，而是他长期与石板接触的结果。他的心脏位置有一块完美的水晶，那是他的"石心"。后来的德鲁伊们将这块水晶嵌入了板甲的胸甲位置，说："德尔瓦终于成为了他最爱的铠甲的一部分。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "earth_warden_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 34000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_earth_warden_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_earth_warden_body" }
]
```

**特殊效果（设计预留）**：站立不动时，AC额外+2。

---

### 5.3 大地守护者拳套（Earth Warden Gauntlets）

```gdscript
item_id = "armor_earth_warden_hands"
display_name = "大地守护者拳套"
description = "一对覆盖着碎石与苔藓的重型拳套，指关节处各嵌有一块来自地心的黑曜石。握拳时，黑曜石会与周围的碎石产生共鸣，发出低沉的震动声。\n\n这对拳套属于磐石之环最强大的战士「碎岩者」布隆。布隆不相信武器，他只相信拳头——"拳头是大地的延伸，武器是人类的软弱"。他在一次与山岭巨人的决斗中，徒手击碎了对方的膝盖骨，用的不是力量，而是"大地的节奏"。他说："每一块岩石都有裂缝，找到裂缝，岩石就会自己碎裂。"\n\n布隆在一次地震中选择了留在原地，用自己的身体为身后的村庄挡住了滚落的巨石。当村民们找到他时，他的身体已经被压成了扁平状，但双手仍然保持着推举的姿势。后来的德鲁伊们将他的双手石化后制成了这对拳套，说："布隆还在守护我们，只是换了一种方式。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "earth_warden_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earth_warden_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earth_warden_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击视为魔法武器，伤害1D8 bludgeoning。

---

### 5.4 大地守护者重靴（Earth Warden Boots）

```gdscript
item_id = "armor_earth_warden_feet"
display_name = "大地守护者重靴"
description = "一对由整块花岗岩雕刻而成的沉重重靴，靴底刻有防滑的岩石纹路。每一步踏下，都会在地面上留下一个浅浅的脚印，周围的泥土和碎石会自动向脚印聚拢，仿佛大地在"拥抱"穿戴者的脚步。\n\n这对重靴属于磐石之环的守望者「扎根者」塔拉。塔拉不相信移动，她在一片草原上站立了整整十年，直到双脚与大地融为一体。她说："根不需要移动，只需要向下。"她的重靴是她在"扎根"前最后一刻穿上的，那是她与"移动"的最后一次告别。\n\n十年后，当人们发现塔拉时，她的身体已经与周围的草原融为一体，只有这对重靴还保持着原样——因为那是花岗岩，不会被根系穿透。后来的德鲁伊们将重靴取出时，发现靴底沾满了泥土和草根，那是大地对塔拉的"纪念品"。他们说："塔拉还在那里，只是我们现在看不见她了。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "earth_warden_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_earth_warden_feet" },
    { attribute_id = "saving_throw_strength", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_earth_warden_feet" }
]
```

**特殊效果（设计预留）**：免疫被击倒（prone）、推离（shove）和强制移动效果。

---

*套装 1–5 完结 · 共 20 件护甲装备*

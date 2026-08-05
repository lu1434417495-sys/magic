# 传奇装备套装设计文档（套装 6–10）

> 共 20 件护甲装备，覆盖 head / body / hands / feet 四个部位。

---

## 套装六：风暴行者（Storm Walker）

> *"风在前，雷在后。我不追赶风暴，我就是风暴。"*

**套装主题**：天空神殿「雷霆之柱」失落祭司的装备。神殿在五十前的雷暴之年中被闪电击毁，但祭司们的装备吸收了无尽雷电。集齐四件时，穿戴者获得「风暴化身」——移动时周围环绕电弧，对触及的敌人造成闪电伤害。

**历史渊源**：雷霆之柱的大祭司「唤雷者」托尔温，是一位能够与雷电对话的狂信者。他相信雷电不是天罚，而是"天空的呼吸"。在最后的雷暴之年中，他站在神殿最高的尖塔上，张开双臂迎接了连续一千道闪电——不是被杀死，而是"成为了雷电的一部分"。

---

### 6.1 风暴行者头冠（Storm Walker Crown）

```gdscript
item_id = "armor_storm_walker_head"
display_name = "风暴行者头冠"
description = "一顶由雷击铜与风暴核心碎片锻造的尖刺头冠，顶端镶嵌着一颗不断闪烁电光的蓝宝石。头冠内侧刻有天空语——一种只有雷电才能"朗读"的古老文字。\n\n这顶头冠原属于唤雷者托尔温。他在成为大祭司之前，曾是一位普通的牧羊人。某个暴风雨之夜，他的羊群被雷电惊散，他追逐着最后一头羊跑到了一座孤峰上。就在那里，一道闪电击中了他——但他没有死。当他醒来时，他发现自己能听懂雷电的"语言"了。他说："那不是疼痛，那是天空在跟我打招呼。"\n\n托尔温用被雷电熔化的铜矿打造了这顶头冠，并在顶端镶嵌了一颗从云层中落下的风暴核心碎片。他说："这颗宝石是天空的眼泪，我要把它戴在离天空最近的地方。"在最后的雷暴之年中，一千道闪电击中了头冠，但头冠没有融化——它只是变得更加明亮，仿佛一千颗星星被封存在了铜中。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "storm_walker_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 17500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_storm_walker_head" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_storm_walker_head" }
]
```

**特殊效果（设计预留）**：雷暴天气中，感知检定+3。

---

### 6.2 风暴行者链甲（Storm Walker Mail）

```gdscript
item_id = "armor_storm_walker_body"
display_name = "风暴行者链甲"
description = "一副由雷击铁环串联而成的链甲，每一环铁环上都残留着微弱的电荷。链甲内衬缝有导电丝线，能够将外部雷电导入地面而不伤害穿戴者。\n\n这副链甲是托尔温成为大祭司后，命令神殿的铁匠们用特殊的雷击铁打造的。雷击铁是一种只在被闪电击中过的矿脉中才能找到的稀有金属，它保留了雷电的"记忆"。铁匠们花了三年时间，将数千枚雷击铁环串联成这副链甲。\n\n在雷暴之年的最后一日，托尔温穿着这副链甲站在神殿尖塔上，迎接了第一千道闪电。闪电穿过链甲的每一环铁环，将它们全部点亮，链甲在那一刻变成了一件由纯粹电光编织而成的圣衣。托尔温在光芒中微笑着说："我终于听懂了天空的全部语言。"然后他与链甲一同化为了一道永恒的闪电，至今仍在某些暴风雨之夜出现在北方的天空。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "storm_walker_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 31000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_storm_walker_body" },
    { attribute_id = "resistance_lightning", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_storm_walker_body" }
]
```

**特殊效果（设计预留）**：受到闪电伤害时，50%概率吸收并储存，下回合释放为额外伤害。

---

### 6.3 风暴行者护手（Storm Walker Gauntlets）

```gdscript
item_id = "armor_storm_walker_hands"
display_name = "风暴行者护手"
description = "一对由雷击铜与导电皮革制成的护手，手背上各嵌有一片风暴鹰的羽毛。握拳时，羽毛会竖立起来，指尖会跃动微弱的电弧。\n\n这对护手属于雷霆之柱最强的战士「雷拳」瓦尔基。瓦尔基不是祭司，他是一位被托尔温从战场上救下的孤儿。托尔温发现他天生对电流免疫——不是魔法，而是他身体的一种奇特构造。瓦尔基用这对护手学会了"雷拳"——将雷电之力集中于拳头，一击打出时如同小型雷击。\n\n瓦尔基在雷暴之年站在托尔温身旁，用自己的身体为托尔温挡住了前五百道闪电。当第一千道闪电落下时，他的身体已经烧焦了，但双手仍然保持着握拳的姿势。托尔温将他的护手取下，说："你的拳头比雷电还硬。"然后将它们与自己的装备一同封存。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "storm_walker_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 13500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_storm_walker_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_storm_walker_hands" }
]
```

**特殊效果（设计预留）**：近战攻击附加1D4 lightning伤害。

---

### 6.4 风暴行者胫甲（Storm Walker Greaves）

```gdscript
item_id = "armor_storm_walker_feet"
display_name = "风暴行者胫甲"
description = "一对由雷击铁与风暴豹皮制成的胫甲，靴底刻有导电纹路。奔跑时，每一步都会在身后留下一道短暂的电弧轨迹。\n\n这对胫甲属于雷霆之柱最快的信使「闪电步」莱拉。莱拉能在暴风雨中以比马还快的速度奔跑，不是因为她轻，而是因为她懂得"顺应风的节奏"。她说："不要逆风跑，要侧身跑——让风推着你走。"她用风暴豹的皮制作了靴筒，因为风暴豹是唯一能在雷暴中奔跑而不被击中的生物。\n\n莱拉在雷暴之年负责疏散神殿周围的村民。她在暴风雨中来回奔跑了三十七次，救出了超过两百人。第三十八次她跑出神殿时，一道闪电击中了她前方的地面，她下意识地跳了起来——然后发现自己悬浮在了空中。那是她最后一次奔跑，也是她第一次飞翔。托尔温在光芒中看到了她的身影，微笑着说："莱拉终于学会了天空的终极秘密。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "leather", "medium_armor", "storm_walker_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 13500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_storm_walker_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_storm_walker_feet" }
]
```

**特殊效果（设计预留）**：雷暴天气中，移动力额外+15尺。

---

## 套装七：亡灵收割者（Death Reaper）

> *"死亡不是终点，而是另一场收割的开始。"*

**套装主题**：亡灵法师结社「终末之镰」的传承装备。这个结社在一百二十年前被圣骑士团剿灭，但他们的装备在亡灵能量的侵蚀下获得了诡异的生命力。集齐四件时，穿戴者获得「收割者之躯」——击杀敌人后恢复HP，且周围散发令人不安的死亡气息。

**历史渊源**：终末之镰的创始人「第一收割者」莫德雷德，原本是一位致力于研究灵魂转生的善良死灵法师。他在发现灵魂无法真正"转生"，只能被"收割"后，精神崩溃了。他说："如果死亡是骗局，那我就成为最大的骗子。"

---

### 7.1 亡灵收割者兜帽（Death Reaper Hood）

```gdscript
item_id = "armor_death_reaper_head"
display_name = "亡灵收割者兜帽"
description = "一顶由人皮与阴影丝绸缝制的黑色兜帽，兜帽内部缝有数百个微型灵魂瓶——每个瓶子中都封存着一个被收割的灵魂碎片。戴上兜帽时，能听到微弱的低语声，那是瓶中灵魂在"诉说"。\n\n这顶兜帽是莫德雷德亲手制作的。他在发现灵魂无法转生后，开始疯狂收割灵魂，将它们封存在微型灵魂瓶中。他说："既然你们无法自己找到归宿，那我就成为你们的'归宿'。"他在兜帽内部缝了三百六十五个灵魂瓶——一天一个，代表他一年的"收藏"。\n\n莫德雷德在临终前将兜帽交给了自己的学徒，说："这些灵魂不是囚犯，他们是……我的日记。每一个灵魂都告诉我一个关于死亡的故事。"学徒接过兜帽时，听到三百六十五个声音同时说："欢迎来到终末。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "cloth", "medium_armor", "death_reaper_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 16500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_death_reaper_head" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_death_reaper_head" }
]
```

**特殊效果（设计预留）**：对undead类型生物的攻击检定+2。

---

### 7.2 亡灵收割者长袍（Death Reaper Robe）

```gdscript
item_id = "armor_death_reaper_body"
display_name = "亡灵收割者长袍"
description = "一件由亡者之发与阴影布料编织而成的黑色长袍，袍身上不断有微弱的幽绿色光芒流动，如同血管中的血液。长袍的下摆永远沾着某种黑色的液体——那不是污垢，而是"浓缩的死亡"，干涸后又会重新渗出。\n\n这件长袍是莫德雷德用了一百个亡者的头发编织的。他说："头发是灵魂与肉体最后的连接，用头发编织的衣服，就是灵魂的囚笼。"他在编织过程中，将每个亡者的最后一丝意识也编入了长袍——那些流动的幽绿色光芒，就是那些意识的"残余"。\n\n莫德雷德穿着这件长袍完成了他最后的"收割"——一座村庄的全部人口。当他站在村中央的广场上时，长袍上的幽绿色光芒前所未有的明亮，仿佛一百个灵魂同时睁开了眼睛。他说："不要害怕，我只是帮你们提前完成了旅程。"然后他自己也融入了长袍的光芒中——他的学徒在第二天发现了空荡荡的长袍，里面只有一颗仍在跳动的心脏。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "cloth", "medium_armor", "death_reaper_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 3
base_price = 29000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 4, source_type = "equipment", source_id = "armor_death_reaper_body" },
    { attribute_id = "resistance_necrotic", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_death_reaper_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_death_reaper_body" }
]
```

**特殊效果（设计预留）**：击杀一个生物后，恢复1D8+4 HP（收割者之触）。

---

### 7.3 亡灵收割者手套（Death Reaper Gloves）

```gdscript
item_id = "armor_death_reaper_hands"
display_name = "亡灵收割者手套"
description = "一对由亡者皮肤鞣制而成的苍白手套，指尖各嵌有一枚指甲形状的黑色水晶。触碰活物时，指尖的水晶会微微发热，仿佛渴望吸取生命力。\n\n这对手套是莫德雷德用一位被他亲手杀死的圣骑士的双手制作的。那位圣骑士名叫加拉哈德，是前来剿灭终末之镰的骑士团先锋。莫德雷德在与加拉哈德的决斗中，用一根手指刺穿了对方的心脏——那不是魔法，而是他指尖的水晶在渴望生命力。\n\n加拉哈德在临死前对莫德雷德说："你杀死了我，但你无法收割我的灵魂——它属于光明。"莫德雷德笑着说："我不需要收割你的灵魂，我只需要你的双手——它们比任何工具都适合'收割'。"他将加拉哈德的双手鞣制成了手套，并将对方的指甲替换成了黑色水晶。他说："现在，你也在为我'收割'了。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "cloth", "medium_armor", "death_reaper_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 12500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_reaper_hands" },
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_reaper_hands" }
]
```

**特殊效果（设计预留）**：徒手攻击或接触性法术附加1D6 necrotic伤害。

---

### 7.4 亡灵收割者软靴（Death Reaper Boots）

```gdscript
item_id = "armor_death_reaper_feet"
display_name = "亡灵收割者软靴"
description = "一对由亡者足底皮与阴影布料制成的软靴，靴底没有纹路——因为它们不需要接触地面。行走时，软靴会在地面之上悬浮约半寸，不会留下任何脚印或声音。\n\n这对软靴属于终末之镰最安静的刺客「无声收割者」奈克斯。奈克斯不是亡灵法师，他是一位被莫德雷德从绞刑架上救下的连环杀手。莫德雷德看中的不是他的残忍，而是他的"安静"——奈克斯能在全程静音的情况下完成一次暗杀。\n\n奈克斯在临终前请求莫德雷德将自己的皮做成软靴，他说："我一生都在追求安静，死后请让我继续安静下去。"莫德雷德满足了他的愿望，并将他的"安静"封入了软靴——穿着这对软靴的人，不会发出任何脚步声，不会留下任何痕迹，甚至不会在雪地上留下凹陷。莫德雷德说："奈克斯终于实现了他的梦想——他成为了真正的'无声'。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "cloth", "medium_armor", "death_reaper_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 12500
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_death_reaper_feet" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_death_reaper_feet" }
]
```

**特殊效果（设计预留）**：移动不留下任何痕迹，免疫追踪。

---

## 套装八：龙鳞铠甲（Dragon Scale）

> *"龙不屑于穿戴铠甲，但龙鳞本身就是世间最完美的防御。"*

**套装主题**：屠龙者公会「龙血誓言」历代精英的战利品熔铸而成。每一代屠龙者都会将猎获的龙鳞贡献给公会，由大师铁匠锻造成装备。集齐四件时，穿戴者获得「龙血沸腾」——对龙类敌人伤害提升，且免疫对应龙种的吐息伤害。

> **实现口径**：主题句中的“免疫吐息”以 `set_bonus_design.md` 的详细规则“dragon breath 获得 `half` mitigation tier”为准。本文旧片段中的 `resistance_fire/cold/lightning +15/+5` 与当前抗性 schema 冲突，数值直接废止，不换算为百分比或固定 DR；正式内容按“抗性 = `half`、免疫 = `immune`、弱点 = `double`”写入 trait `damage_resistance_entries`，其中 `cold` 归一化为 `freeze`。四件基础属性、每件“特殊效果（设计预留）”以及 2/4 件套效果均属于完整交付范围；相同的 `half` 不重复乘算。龙鳞套只在角色 10 级以后进入获取区间，具体是 10–12、15–17 还是 18–20 级档由正式获取内容冻结。字段归一化、叠加规则和缺失接口见 [龙鳞铠甲套装完整落地方案](../../proposals/inventory/dragon_scale_set_full_landing.md)。

**历史渊源**：龙血誓言的创始人「第一屠龙者」西格德，原本是一位被龙焰烧毁家园的孤儿。他在废墟中捡到了一片龙鳞，发现龙焰无法烧毁龙鳞本身——"如果龙的武器无法伤害龙，那就用龙来对抗龙。"

---

### 8.1 龙鳞头盔（Dragon Scale Helm）

```gdscript
item_id = "armor_dragon_scale_head"
display_name = "龙鳞头盔"
description = "一顶由红龙顶鳞与精钢骨架熔铸而成的覆面头盔，头盔表面覆盖着真正的龙鳞——每一片都经过特殊处理，保留了龙鳞的天然耐火性。面甲造型模仿龙吻，双眼位置镶嵌着两颗缩小版的龙睛宝石。\n\n这顶头盔是龙血誓言第三代大师铁匠「龙火」哈肯用一条青年红龙的顶鳞打造的。那条红龙名叫「焚烬者」卡扎克，曾经烧毁了三座村庄。哈肯在屠龙战斗中失去了左眼，但他亲手用匕首刺穿了卡扎克的眼睛，将龙睛挖了出来。\n\n哈肯在打造头盔时，特意将卡扎克的龙睛宝石镶嵌在了双眼位置。他说："我要让这条龙亲眼看着，它的力量被用来保护它曾经伤害的人。"每当穿戴者在战斗中面对龙类敌人时，龙睛宝石会发出愤怒的红光——那是卡扎克残留的意识在"抗议"。哈肯笑着说："抗议吧，你越抗议，我越高兴。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "dragon_scale_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 20000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dragon_scale_head" }
]
trait_ids = ["armor.dragon_scale.head.fire_resistance"]
```

**特殊效果（设计预留）**：对dragon类型生物的攻击检定+2。

---

### 8.2 龙鳞胸甲（Dragon Scale Breastplate）

```gdscript
item_id = "armor_dragon_scale_body"
display_name = "龙鳞胸甲"
description = "一副由五条不同龙种的鳞甲拼接而成的全身胸甲：胸口是红龙的火焰鳞，护肩是蓝龙的闪电鳞，护腹是绿龙的毒鳞，护背是黑龙的酸鳞，护腰是白龙的冰霜鳞。每一种龙鳞都保留了其原主的元素抗性。\n\n这副胸甲是龙血誓言第五代大师铁匠「五龙」艾拉妮丝的巅峰之作。她一生只猎杀了五条龙——但她没有猎杀它们，而是"请求"它们。她走遍了世界，找到了五条垂死的老龙，说服它们在临终前自愿献出鳞甲。她说："我不是来杀死你们的，我是来帮你们延续生命的——以另一种方式。"\n\n五条老龙都同意了。红龙说："让我继续燃烧。"蓝龙说："让我继续闪耀。"绿龙说："让我继续蔓延。"黑龙说："让我继续腐蚀。"白龙说："让我继续冻结。"艾拉妮丝将它们的鳞甲拼接成这副胸甲，说："你们不会死去，你们会成为最坚固的守护。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "dragon_scale_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 1
base_price = 36000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 7, source_type = "equipment", source_id = "armor_dragon_scale_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_dragon_scale_body" }
]
trait_ids = ["armor.dragon_scale.body.element_resistance"]
```

**特殊效果（设计预留）**：受到对应龙种吐息伤害时，免疫该种元素伤害的一半。

---

### 8.3 龙鳞护手（Dragon Scale Gauntlets）

```gdscript
item_id = "armor_dragon_scale_hands"
display_name = "龙鳞护手"
description = "一对覆盖着龙爪鳞片的重型护手，手背处各嵌有一片龙逆鳞——那是龙身上最坚硬、最敏感的部分。握紧拳头时，逆鳞会微微竖起，释放出龙威的残余气息。\n\n这对护手属于龙血誓言最强的战士「龙爪」贝奥武夫（另一位同名者）。贝奥武夫在一次与上古黑龙的战斗中，徒手撕下了对方的逆鳞——不是因为他力量大，而是因为他在龙焰中坚持了足够长的时间，让黑龙的鳞片因过热而脆化。他说："龙的鳞片不是无敌的，它们只是需要足够的时间和温度。"\n\n贝奥武夫将黑龙的逆鳞嵌入护手，作为自己的"勋章"。他说："这不是装饰，这是警告——告诉所有龙，有人曾经撕下过它们的逆鳞。"在他晚年，他将护手传给了公会中最年轻的屠龙者，说："不要只是穿戴它，要配得上它。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "dragon_scale_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dragon_scale_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_dragon_scale_hands" }
]
```

**特殊效果（设计预留）**：每次近战武器命中dragon类型生物时，伤害额外+1D4；重复攻击和随机链逐个独立命中段触发。

---

### 8.4 龙鳞胫甲（Dragon Scale Greaves）

```gdscript
item_id = "armor_dragon_scale_feet"
display_name = "龙鳞胫甲"
description = "一对由龙骨与龙鳞熔铸而成的沉重重靴，靴底刻有龙爪纹路。每一步踏下，都会在地面上留下类似龙爪的痕迹，周围的弱小生物会本能地感到恐惧。\n\n这对胫甲属于龙血誓言最传奇的屠龙者「龙步」赫拉克勒斯（另一位同名者）。赫拉克勒斯不是人类，他是半龙——他的母亲是一位被龙掳走的人类女子。他在龙巢中长大，学会了龙的语言和思维方式，然后在成年后的第一天杀死了自己的"父亲"——一条贪婪的红龙。\n\n赫拉克勒斯用"父亲"的腿骨和鳞片制作了这对胫甲，说："我要用它的身体，走出一条与它完全不同的路。"他穿着这对胫甲走遍了世界，猎杀了超过一百条恶龙。晚年时，他在最后一次屠龙行动中与一条上古绿龙同归于尽。他的胫甲被找回时，靴底的龙爪纹路已经磨得几乎平滑——那是无数次战斗和行走留下的痕迹。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "dragon_scale_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 16000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_dragon_scale_feet" },
    { attribute_id = "saving_throw_fear", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_dragon_scale_feet" }
]
```

**特殊效果（设计预留）**：免疫龙威（frightful presence）效果。

本件的 `saving_throw_fear +3` 与 2 件套的同类 `+3` 按 `add` 相加；完整套装的 trait 加值为 `+6`。龙威 immunity 是布尔语义，不因两个来源重复增强。

---

## 套装九：铁壁要塞（Iron Bulwark）

> *"最好的攻击是坚不可摧的防守。让敌人在你的盾墙上撞碎自己。"*

**套装主题**：古代帝国「铁壁军团」重装步兵的标准装备升级版。铁壁军团在一次围城战中全员阵亡，但他们的装备在敌人的攻击下反而变得更加坚固。集齐四件时，穿戴者获得「移动的城墙」——相邻盟友获得AC加值，且穿戴者免疫被推动或击倒。

**历史渊源**：铁壁军团的指挥官「不动如山」马克西穆斯，是一位坚信"防守即是胜利"的战略家。他在一次被十倍兵力围困的战役中，命令全军组成盾墙，坚持了整整四十九天。当援军终于到达时，马克西穆斯和他的三千士兵已经全部战死——但他们的尸体仍然保持着盾墙的姿势。

---

### 9.1 铁壁要塞头盔（Iron Bulwark Helm）

```gdscript
item_id = "armor_iron_bulwark_head"
display_name = "铁壁要塞头盔"
description = "一顶由精钢与陨铁混铸的封闭式重盔，盔顶有一根粗短的尖刺——那不是装饰，而是用来在盾墙中固定长矛的支架。面甲上只有一道水平的观察缝，最大限度地保护面部。\n\n这顶头盔原属于铁壁军团的百夫长「盾墙之牙」卡西乌斯。卡西乌斯在第四十九天的最后攻击中，被敌人的投石击中了头盔。投石碎裂了，但头盔上留下了一道永远无法修复的凹痕。卡西乌斯在倒下前，用头盔上的尖刺固定住了即将倒下的军旗，说："旗帜不倒，军团不死。"\n\n援军到达时，发现卡西乌斯的尸体跪在军旗下，头盔上的尖刺深深插入了地面。他们花了三个小时才将他的头盔取下——不是因为卡住了，而是因为他的肌肉在死后仍然保持着紧握的姿势。后来的铁匠们将他的头盔重新熔铸，保留了那道凹痕和尖刺，说："那是铁壁军团最坚硬的证明。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "heavy_armor", "iron_bulwark_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 0
base_price = 19000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_iron_bulwark_head" },
    { attribute_id = "max_hp", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_iron_bulwark_head" }
]
```

**特殊效果（设计预留）**：被 critical hit 时，50%概率将其降为普通命中（头盔阻挡）。

---

### 9.2 铁壁要塞板甲（Iron Bulwark Plate）

```gdscript
item_id = "armor_iron_bulwark_body"
display_name = "铁壁要塞板甲"
description = "一副由三层精钢与一层陨铁叠加锻造的超重型板甲，甲面上布满了锤击留下的凹痕——那不是缺陷，而是无数攻击的"纪念"。胸甲正面刻有铁壁军团的座右铭："不动如山，不退一步。"\n\n这副板甲是铁壁军团的铸造大师「铁砧」布鲁图斯在围城战前夜完成的最后一件作品。他用了整整一个月的时间，将收集到的所有陨铁熔铸成这副板甲。他说："普通的铁只能挡住普通的攻击，但陨铁来自天上——它能挡住一切。"\n\n布鲁图斯在围城战的第三十天阵亡。他的板甲被敌人的重锤击中了十七次，每一次都在甲面上留下了一个凹痕，但没有一次击穿。第十八个敌人用一把魔法长矛刺穿了甲缝——那是板甲唯一的弱点。布鲁图斯在倒下前，将自己的身体压在了盾墙的缺口上，说："我的铠甲挡不住第十八次攻击，但我的身体可以。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "heavy_armor", "iron_bulwark_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 0
base_price = 35000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 8, source_type = "equipment", source_id = "armor_iron_bulwark_body" },
    { attribute_id = "max_hp", mode = "flat", value = 15, source_type = "equipment", source_id = "armor_iron_bulwark_body" }
]
```

**特殊效果（设计预留）**：5尺内每有一个盟友，AC额外+1（最高+2）。

---

### 9.3 铁壁要塞臂铠（Iron Bulwark Bracers）

```gdscript
item_id = "armor_iron_bulwark_hands"
display_name = "铁壁要塞臂铠"
description = "一对覆盖至肘部的超重型臂铠，前臂外侧各有一块可拆卸的小型塔盾。握拳时，塔盾会自动滑至手背，形成额外的防护。\n\n这对臂铠属于铁壁军团最坚固的战士「盾墙之基」格拉古。格拉古在盾墙中始终站在最前排的中心位置——那是承受攻击最多的位置。他的臂铠在四十九天中被击中了超过一千次，塔盾更换了十七次。\n\n格拉古在第四十九天的最后攻击中，用双臂挡住了敌人的冲车。冲车撞碎了他的臂铠，也撞碎了他的双臂，但他用断骨卡住了冲车的轮子。他在倒下前说："我的骨头比钢铁还硬——因为我有信念。"后来的铁匠们将他的断骨熔入了臂铠的钢铁中，说："格拉古还在盾墙中，只是换了一种形式。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "heavy_armor", "iron_bulwark_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_iron_bulwark_hands" },
    { attribute_id = "saving_throw_constitution", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_iron_bulwark_hands" }
]
```

**特殊效果（设计预留）**：可以主动举起臂铠格挡（reaction），将一次近战攻击的伤害减半。

---

### 9.4 铁壁要塞重靴（Iron Bulwark Sabatons）

```gdscript
item_id = "armor_iron_bulwark_feet"
display_name = "铁壁要塞重靴"
description = "一对由整块精钢铸造的沉重重靴，靴底有粗大的防滑钉，能够在任何地形上保持稳固。每只重靴重达二十磅，穿上后几乎无法奔跑——但这正是设计目的：铁壁军团的士兵不需要奔跑，他们只需要"站稳"。\n\n这对重靴属于铁壁军团最年轻的士兵「盾墙之末」提图斯。提图斯只有十六岁，是被父亲强行送入军团的。他在第四十九天的战斗中，第一次也是最后一次举起了盾牌。敌人的重骑兵冲锋撞碎了他的盾牌，也撞断了他的双腿，但他的重靴死死钉在了地面上——他的身体被推出了原位，但双脚仍然留在盾墙中。\n\n援军找到他时，他的上半身倒在血泊中，双腿仍然直立在原地。后来的铁匠们将他的双腿骨熔入了重靴的钢铁中，说："提图斯终于成为了真正的铁壁——他比任何钢铁都坚定。""
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "heavy_armor", "iron_bulwark_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 15000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_iron_bulwark_feet" },
    { attribute_id = "saving_throw_strength", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_iron_bulwark_feet" }
]
```

**特殊效果（设计预留）**：免疫被推动（shove）、击倒（prone）和强制移动效果。

---

## 套装十：古代帝王（Ancient Emperor）

> *"帝国会衰落，但帝王的意志永不消亡。"*

**套装主题**：已灭亡帝国「永恒王朝」末代皇帝的陪葬装备。永恒王朝在一千年前因一场大瘟疫而灭亡，末代皇帝将自己的装备与自己的遗体一同封入了地下陵墓。集齐四件时，穿戴者获得「帝王威仪」——对低等级敌人的攻击和社交检定获得优势。

**历史渊源**：永恒王朝的末代皇帝「不屈者」奥古斯都，在帝国灭亡的最后一日，拒绝逃离首都。他穿上全套铠甲，坐在王座上等待死亡。他说："皇帝不是帝国的统治者，皇帝是帝国的守护者。如果帝国要灭亡，那皇帝应该第一个死去。"

---

### 10.1 古代帝王冠冕（Ancient Emperor Crown）

```gdscript
item_id = "armor_ancient_emperor_head"
display_name = "古代帝王冠冕"
description = "一顶由纯金与秘银编织而成的华丽冠冕，冠冕上镶嵌着十二颗代表帝国十二省的宝石。冠冕内侧刻有永恒王朝的开国皇帝的临终遗言："帝国不是土地，是人民。"\n\n这顶冠冕是永恒王朝的开国皇帝「征服者」凯撒（另一位同名者）用征服的第一座城市的全部黄金打造的。他在冠冕上镶嵌了十二颗宝石，每一颗代表一个被征服的省份。他说："我要让这顶冠冕比任何城墙都重——因为它承载着十二个省份的命运。"\n\n末代皇帝奥古斯都在临终前将这顶冠冕戴在了头上，尽管当时他已经高烧到无法视物。他说："即使我看不见，我也要让人民知道，皇帝还在这里。"他的遗体被封入陵墓时，冠冕上的十二颗宝石突然全部变暗——那是十二个省份同时灭亡的征兆。但传说，当永恒的帝国再次崛起时，十二颗宝石会重新亮起。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "head", "metal", "medium_armor", "ancient_emperor_set"]
equipment_slot_ids = ["head"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 22000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "armor_ancient_emperor_head" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "armor_ancient_emperor_head" }
]
```

**特殊效果（设计预留）**：对CR低于穿戴者等级的生物，社交检定+3。

---

### 10.2 古代帝王铠甲（Ancient Emperor Armor）

```gdscript
item_id = "armor_ancient_emperor_body"
display_name = "古代帝王铠甲"
description = "一副由纯金板甲与秘银内衬组成的仪式性铠甲，甲面上刻有永恒王朝历代皇帝的浮雕——从开国皇帝到末代皇帝，每一位的面容都被永久地铸在了黄金中。胸甲正面是永恒王朝的国徽：一只衔着橄榄枝的雄鹰。\n\n这副铠甲是永恒王朝的第二代皇帝「建造者」哈德良（另一位同名者）下令打造的。他召集了帝国最好的十二位金匠，花了十年时间将一副普通板甲改造成了这件黄金艺术品。每一位金匠负责雕刻一位皇帝的面容，他们说："我们不是在打造铠甲，我们是在铸造历史。"\n\n末代皇帝奥古斯都在穿上这副铠甲时，黄金已经因为年代久远而微微发黑。他说："这副铠甲比我爷爷的爷爷还老，但它是帝国最年轻的部分——因为它承载着所有人的记忆。"他坐在王座上等待死亡时，铠甲上的浮雕在烛光中仿佛活了过来，十二位先帝的面容同时露出了微笑。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "body", "metal", "medium_armor", "ancient_emperor_set"]
equipment_slot_ids = ["body"]
equipment_type_id = "armor"
max_dex_bonus = 2
base_price = 40000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 6, source_type = "equipment", source_id = "armor_ancient_emperor_body" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "armor_ancient_emperor_body" },
    { attribute_id = "charisma_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ancient_emperor_body" }
]
```

**特殊效果（设计预留）**：5尺内每有一个敌人，AC额外+1（最高+2，"帝王不惧众敌"）。

---

### 10.3 古代帝王护手（Ancient Emperor Gauntlets）

```gdscript
item_id = "armor_ancient_emperor_hands"
display_name = "古代帝王护手"
description = "一对由纯金铸造的华丽护手，手背处各镶嵌着一枚永恒王朝的皇家印章。右手印章用于签署法令，左手印章用于签署赦免令。握紧拳头时，印章会深深印入掌心，留下永久的痕迹——那是帝王之权的"烙印"。\n\n这对护手是永恒王朝的第三代皇帝「立法者」查士丁尼（另一位同名者）下令改造的。他说："皇帝的权力不应该来自剑，而应该来自印章。剑只能杀死一个人，印章可以影响一千万人。"他在护手上加装了皇家印章，这样他即使在战场上也能随时签署法令。\n\n末代皇帝奥古斯都在临终前，用右手印章签署了最后一道法令——"所有奴隶即刻获得自由"，用左手印章签署了最后一道赦免令——"所有囚犯即刻释放"。他说："帝国要灭亡了，但人在帝国灭亡前应该是自由的。"他的右手印章在签署最后一道法令时碎裂了，因为黄金已经太脆——但碎裂的印章印出的痕迹比以往任何一次都深。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "hands", "metal", "medium_armor", "ancient_emperor_set"]
equipment_slot_ids = ["hands"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ancient_emperor_hands" },
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ancient_emperor_hands" }
]
```

**特殊效果（设计预留）**：对CR低于穿戴者等级的生物，攻击检定+2。

---

### 10.4 古代帝王战靴（Ancient Emperor Boots）

```gdscript
item_id = "armor_ancient_emperor_feet"
display_name = "古代帝王战靴"
description = "一对由纯金与象牙雕刻而成的仪式性战靴，靴面上刻有永恒王朝的所有战役地图。靴底刻有十二个省份的徽章，每一步踏下，都会有一个省份的徽章短暂亮起。\n\n这对战靴是永恒王朝的第五代皇帝「征服者」亚历山大（另一位同名者）下令打造的。他说："我要让每一步都踏在帝国的土地上。"他走遍了帝国的每一个省份，在靴底刻下了它们的徽章。他说："即使我闭上眼睛走路，大地也会告诉我，我在哪里。"\n\n末代皇帝奥古斯都穿着这对战靴走上了王座。他说："这对靴子比我走过更多的路，但它们最终停在了这里——帝国的终点。"他在王座上坐了三天三夜，直到瘟疫夺走他的生命。他的战靴在死后仍然保持着站立的姿势，因为黄金已经硬化。后来的盗墓者在打开陵墓时，发现奥古斯都的遗体已经化为白骨，但战靴仍然完好——靴底的十二个省份徽章，在黑暗中微弱地闪烁了千年。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["armor", "feet", "metal", "medium_armor", "ancient_emperor_set"]
equipment_slot_ids = ["feet"]
equipment_type_id = "armor"
base_price = 17000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "armor_ancient_emperor_feet" },
    { attribute_id = "movement_speed", mode = "flat", value = 5, source_type = "equipment", source_id = "armor_ancient_emperor_feet" }
]
```

**特殊效果（设计预留）**：在曾经属于永恒王朝的领土上，所有检定+1。

---

*套装 6–10 完结 · 共 20 件护甲装备*

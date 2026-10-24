namespace RollOfTheScout
{
    /// <summary>
    /// 职业「关键词 / 简介」文本表（中英双语，按游戏语言择一）。
    ///
    /// 用途：玩家按键显示「当前职业分配情况」时，在分配面板下方追加一块「本人职业」的介绍，
    /// 让玩家不必翻文档就能快速了解自己的职业（只显示本人，不列出全部职业）。
    /// 文案与仓库根目录的 <c>ModClassIntroduction.md</c>（中文部分的关键词 / 简介）保持一致；
    /// 英文为对应翻译。
    ///
    /// 本表由主模组与附属模组各自持有一份（两者必须能独立安装，客机可能只装附属模组）。
    /// 因此主模组新增 / 调整职业文案时，需同步更新 <c>RollOfTheScoutClient/ClassIntroText.cs</c>。
    /// </summary>
    internal static class ClassIntroText
    {
        /// <summary>当前语言下「职业介绍」板块的标题前缀（拼在职业名之前）。</summary>
        public static string TitlePrefix
        {
            get { return "[职业介绍 / Class Intro] "; }
        }

        /// <summary>该职业的关键词原文（中英双语，两行；含 Keywords / 关键词 标签）。未知职业返回空字符串。</summary>
        public static string Keywords(int code)
        {
            string en = KeywordsEn(code);
            string zh = KeywordsZh(code);
            if (string.IsNullOrEmpty(en) && string.IsNullOrEmpty(zh)) return string.Empty;
            if (string.IsNullOrEmpty(zh)) return "Keywords: " + en;
            if (string.IsNullOrEmpty(en)) return "关键词：" + zh;
            return "Keywords: " + en + "\n关键词：" + zh;
        }

        /// <summary>该职业的简介原文（中英双语，两行；两行都自带「【名称】」前缀）。未知职业返回空字符串。</summary>
        public static string Intro(int code)
        {
            string en = IntroEn(code);
            string zh = IntroZh(code);
            if (string.IsNullOrEmpty(en)) return zh;
            if (string.IsNullOrEmpty(zh)) return en;
            return en + "\n" + zh;
        }

        /// <summary>关键词行（中英双语）。职业未知时返回空字符串。</summary>
        public static string KeywordsLine(int code)
        {
            return Keywords(code);
        }

        /// <summary>简介行（中英双语）。职业未知时返回空字符串。</summary>
        public static string IntroLine(int code)
        {
            return Intro(code);
        }

        // ---------------------------------------------------------------
        // 中文（与 ModClassIntroduction.md 中文部分一致）
        // ---------------------------------------------------------------

        private static string KeywordsZh(int code)
        {
            switch (code)
            {
                case 1: return "每关随机道具、一次性昏迷复活、高饥饿消耗、无限周期加速";
                case 2: return "范围增益、开局负债、绑定全员生死、亡语激励、额外神秘物品";
                case 3: return "周期随机补给、开局背包、初始高石化";
                case 4: return "每关医疗物资、范围减伤解毒、队友死亡自伤惩罚";
                case 5: return "初始武器、匕首吹箭、拾取转化";
                case 6: return "范围潘多拉、拾取转化、概率消失、持续自增诅咒";
                case 7: return "骸骨之书、死亡增益、骷髅强化、初始高诅咒、诅咒与增益运营";
                case 8: return "毒孢抗性、自然食物、周期粉蕉、禁用工具";
                case 9: return "负面状态转化、无负面周期惩罚、周期负面耐受";
                case 10: return "开局随机物资腰包、独行流血、集体行动、同伴增益";
                case 11: return "检查点旗标、插旗范围清负、自身插旗石化、全员随机增益、团队爆发";
                case 12: return "跳舞回体、强制共舞、周期后空翻、摔倒风险";
                case 13: return "周期产蛋、传染转职、同类大范围回血、周期后空翻";
                case 14: return "禁用物品、拾取消失、周期重置、受伤放大、职业免疫";
                case 15: return "周期卷轴、双形态、随机效果、负面代价";
                case 16: return "初始高负面、自带黄金抓钩、抓人高惩罚、潘多拉免疫";
                case 17: return "便携烤炉、烤物解饿、烤多中毒、非食物奖励、持炉炎热";
                case 18: return "便携烤炉、生食烹饪、群体增益、熟食增益、生食惩罚、饥饿昏迷";
                case 19: return "踢击替代拉人、踢中回体、踢空困倦、初始诅咒";
                case 20: return "开局背包、物品囤积、负重奖励、周期加速、额外体力";
                case 21: return "昏迷救援、传送无敌、救起加速、背人治疗、死亡惩罚";
                case 22: return "防晒喷雾、每关刷新、强制覆盖、防晒回体、用完额外效果";
                case 23: return "独行奖励、结伴惩罚、半径检测";
                case 24: return "童子军大炮、每关与周期获得、受伤炸药、主手风险";
                case 25: return "喇叭乐器、演奏范围增益、拾取转化、库存奖励、初始诅咒、特殊转职";
                case 26: return "童军宝石、诅咒锁定、拾取消失、特殊增益、零号守则、天底解放";
                case 27: return "早起虫儿、攀爬高频脱手、分解垃圾奖励、感染转职、同类增益、免疫孢子、食虫惩罚";
                case 28: return "周期绑气球、拾取转化、只吃包装熟食、冷热抵抗、额外负面";
                case 29: return "周期荔莓、食用奖励、非莓惩罚、进食损伤、特殊转职";
                case 30: return "周期获得昆虫、额外体力溢出取模、夜间加速、独行清负、职业效果复制";
                default: return string.Empty;
            }
        }

        private static string IntroZh(int code)
        {
            switch (code)
            {
                case 1: return "【开路先锋】每关获得随机道具背包；首次昏迷将完全清除负面复活；【高代谢】迫使你不断管理饥饿与冲刺。";
                case 2: return "【团队核心】持续强力范围增益；自身诅咒石化随全员生死波动；自身死亡触发全员激励；全员存活时，自己开启雕像可获额外物品。";
                case 3: return "【移动补给】开局自带随机背包；定时获得随机物资；初始诅咒与较高石化需谨慎管理；与【炊事员】有特殊羁绊。";
                case 4: return "【随队医护】每关获得随机医疗物资背包；近身小范围持续减伤解毒；队友阵亡会触发自伤惩罚。";
                case 5: return "【生死问题】开局自带仪式匕首与吹箭筒；拾取物品概率变为武器；杀人还是救人，这是一个问题。";
                case 6: return "【混沌之子】持续向周围散播潘多拉效果；拾取物品半变魔盒半消失；自身诅咒持续叠加。";
                case 7: return "【骷髅之王】致敬本模组最不小团体的职业；通过玩家死亡、与骷髅配合降低自身诅咒；持续为周围的人类玩家施加中毒、骷髅玩家获得额外体力与无敌。";
                case 8: return "【自然力量】自身对中毒与孢子有较强抗性；只能食用生的自然食物，否则获得损伤；定时获得粉莓蕉；自身无法主动使用攀爬道具，拾取会直接变为木棍。";
                case 9: return "【负面转化】受到绝大部分负面状态时，等量转化额外体力；身上无负面状态则周期损失额外体力；对特定负面状态有耐受。";
                case 10: return "【结伴而行】开局自带随机物资腰包；落单独行持续流血与饥饿；需和玩家结伴同行缓解，与同职业【童军】与【领袖】有加强羁绊。";
                case 11: return "【团队旗手】每关获得检查点旗标；自身插旗立即范围清除负面，三秒后全员获得随机增益效果；自身每次插旗增加石化。";
                case 12: return "【一起跳舞】自身范围内玩家跳舞获得额外体力；自身跳舞强制周围玩家共舞；定时强制后空翻且必定摔倒。";
                case 13: return "【模因传播】语音与产蛋周期获得额外体力；传染式转职，可主动通过喂食煎蛋强制同化他人；较大范围内，同类周期回血减伤，但定时强制后空翻且必定摔倒。";
                case 14: return "【请输入文本】禁用食物及绝大部分道具，拾取物品直接消失；每关饥饿值清零重置；周期回满额外体力；受伤放大即倒；免疫【潘多拉之子】【死灵法师】【舞王】【区】职业效果。";
                case 15: return "【随机施法】定时获得卷轴，打开随机触发效果并增加自身石化/诅咒；人类形态效果偏辅助，骷髅形态效果高收益高代价。";
                case 16: return "【立体机动】初始高诅咒高石化需谨慎管理；开局自带黄金抓钩与滑翔翼；使用抓钩勾中玩家会立即触发高损伤惩罚；免疫【潘多拉之子】且拾取的潘多拉餐盒直接消失。";
                case 17: return "【烤炉大师】定时获得便携烤炉；烤任意物品都能解饿；烤多会中毒；周期烤非食物（道具）可获额外体力；手持便携烤炉时有高频炎热。";
                case 18: return "【饮食保障】定时获得便携烤炉；烤熟生食可为周围回饥饿与体力；食用熟食有额外收益，食用生食有较大惩罚；附近有人饥饿会致困昏迷；与【后勤】有特殊羁绊。";
                case 19: return "【并非和平】右键拉人变为踢人；踢中玩家获得额外体力，踢空加困倦；开局自带诅咒。";
                case 20: return "【负重前行】开局自带背包；负重过半达标后，周期获得加速与额外体力。";
                case 21: return "【火线救援】队友昏迷将自动传送并获得一次无敌，背起再获得一次加速与大量额外体力；背起玩家时治疗双方；有玩家死亡会获得石化惩罚。";
                case 22: return "【精致防晒】每关获得三瓶防晒喷雾，强制覆盖物品栏；获得防晒奖励额外体力，每次用完防晒喷雾会额外奖励额外体力并积累石化。";
                case 23: return "【单人模式】离人够远时周期奖励额外体力；靠近他人持续困倦。";
                case 24: return "【重火力者】每关获得童子军大炮；定时获得童子军大炮；受伤就会获得炸药，且物品栏满时会直接塞入主手。";
                case 25: return "【演奏乐队】拾取物品概率变乐器，库存与演奏可范围奖励额外体力；库存集齐三种乐器有额外加速奖励，且给予他人有低概率将其转职。";
                case 26: return "【领队残魂】初始诅咒锁定，仅能通过获取童军宝石道具降低；使用童军宝石道具有专属特殊增益；禁用童军宝石道具以外物品，拾取物品直接消失；即时传送救援，不可放弃他人；集齐所有童军宝石道具有额外奖励；抵达天底解放灵魂全员奖励。";
                case 27: return "【蠕动之区】攀爬时高频强制脱手；靠拾取分解垃圾物品、获取积攒早起虫儿积累收益；感染式转职，周期对附近施加孢子，过半会使他人强制同化，并获得早起虫儿；同类聚集有加速与无限体力效果奖励；自身免疫孢子；食用早期虫儿有诅咒惩罚。";
                case 28: return "【太空漂流】身上定时绑上气球；无法拾取气球/一束气球；拾取物品概率变为火箭背包/喷气背包；只吃包装食品或熟食，否则中毒；对冷热有一定抗性，但部分负面状态会额外加重。";
                case 29: return "【大果成瘾】定时获得青/紫荔莓；食用青荔莓获得冲刺效果、紫荔莓则给无限体力；食用其他食物加困倦；每次进食固定损伤；他人多次连吃荔莓会被强制同化。";
                case 30: return "【代码之力】定时获得随机虫类，获得时大概率中毒；额外体力溢出会取模重置；夜晚周期获得加速；独行会周期清负面；无负面且无虫时会复制在场他人职业效果，并持续生效一段时间。";
                default: return string.Empty;
            }
        }

        // ---------------------------------------------------------------
        // English
        // ---------------------------------------------------------------

        private static string KeywordsEn(int code)
        {
            switch (code)
            {
                case 1: return "Random items each level · One-time faint revive · High hunger drain · Periodic infinite sprint";
                case 2: return "Area buffs · Opening debt · Bound to everyone's life · Death-triggered rally · Extra mystery item";
                case 3: return "Periodic random supply · Starting backpack · High starting petrify";
                case 4: return "Medical supplies each level · Area injury/poison relief · Self-injury on teammate death";
                case 5: return "Starting weapons · Dagger and blowgun · Pickup conversion";
                case 6: return "Area Pandora · Pickup conversion · Chance to vanish · Steadily rising curse";
                case 7: return "Book of Bones · Death buffs · Skeleton empowerment · High starting curse · Curse/gain management";
                case 8: return "Poison/spore resistance · Natural food only · Periodic pink banana · Tools disabled";
                case 9: return "Debuff conversion · Penalty without debuffs · Periodic debuff tolerance";
                case 10: return "Starting random fanny pack · Solo bleeding · Group action · Companion buffs";
                case 11: return "Checkpoint flag · Area debuff purge on plant · Self petrify on plant · Random team buffs · Team burst";
                case 12: return "Dance for stamina · Forced group dance · Periodic backflip · Fall risk";
                case 13: return "Periodic eggs · Contagious reclass · Same-class area healing · Periodic backflip";
                case 14: return "Items disabled · Pickups vanish · Periodic reset · Amplified injury · Class immunity";
                case 15: return "Periodic scrolls · Two forms · Random effects · Negative cost";
                case 16: return "High starting debuffs · Golden grapnel · Heavy grab penalty · Pandora immunity";
                case 17: return "Portable stove · Cooking sates hunger · Cooking builds poison · Non-food rewards · Heat while holding";
                case 18: return "Portable stove · Cooking raw food · Group buffs · Cooked-food bonus · Raw-food penalty · Hunger-induced drowsiness";
                case 19: return "Kick replaces grab · Hits restore stamina · Misses cause drowsiness · Starting curse";
                case 20: return "Starting backpack · Item hoarding · Load reward · Periodic sprint · Extra stamina";
                case 21: return "Faint rescue · Teleport invincibility · Post-rescue sprint · Carrying heals both · Death penalty";
                case 22: return "Sunscreen · Refreshed each level · Forced overwrite · Sunscreen stamina · Per-bottle bonus";
                case 23: return "Solo reward · Company penalty · Radius check";
                case 24: return "Scout cannon · Each level and periodic · Injury dynamite · Main-hand risk";
                case 25: return "Horns and instruments · Performance area buff · Pickup conversion · Inventory bonus · Starting curse · Special reclass";
                case 26: return "Scout gems · Locked curse · Pickups vanish · Special bonuses · Rule Zero · Nadir liberation";
                case 27: return "Early bug · Frequent climbing slips · Decomposing junk rewards · Contagious reclass · Same-class buffs · Spore immunity · Bug-eating penalty";
                case 28: return "Periodic balloons · Pickup conversion · Packaged/cooked food only · Heat/cold resistance · Extra debuffs";
                case 29: return "Periodic kingberries · Eating rewards · Non-berry penalty · Injury per meal · Special reclass";
                case 30: return "Periodic insects · Extra stamina overflow modulo · Night sprint · Solo debuff cleanse · Class-effect copy";
                default: return string.Empty;
            }
        }

        private static string IntroEn(int code)
        {
            switch (code)
            {
                case 1: return "【Pioneer】 a random-item backpack every level; your first faint fully clears debuffs as you revive; 【Hyper-metabolism】 forces you to keep managing hunger and sprints.";
                case 2: return "【Team core】 strong sustained area buffs; your curse and petrify swing with everyone's life and death; your death rallies everyone; while all are alive, opening an Ancient Idol yourself yields an extra item.";
                case 3: return "【Mobile supply】 starts with a random backpack; receives random supplies on a timer; your starting curse and high petrify need careful management; special bond with the Cook.";
                case 4: return "【Field medic】 a random medical backpack every level; sustained close-range injury and poison relief; a teammate's death triggers a self-injury penalty.";
                case 5: return "【A matter of life and death】 starts with a ritual dagger and a blowgun; picked-up items may become weapons; to kill or to save, that is the question.";
                case 6: return "【Child of chaos】 keeps spreading the Pandora effect around you; half of the items you pick up become boxes and half vanish; your curse keeps stacking.";
                case 7: return "【King of skeletons】 a tribute to the mod's least small group; lower your curse through player deaths and skeleton allies; keep poisoning nearby humans while skeletons gain extra stamina and invincibility.";
                case 8: return "【Power of nature】 strong resistance to poison and spores; you can only eat raw natural food or take injury; gains a pink banana on a timer; climbing tools are unusable — picked-up ones turn into wooden sticks.";
                case 9: return "【Negative conversion】 most debuffs you take convert into an equal amount of extra stamina; with no debuffs you periodically lose extra stamina; tolerant against specific debuffs.";
                case 10: return "【Better together】 starts with a random-item fanny pack; alone you bleed and starve; you need company to ease it; strengthened bond with fellow Scouts and Leaders.";
                case 11: return "【Team standard-bearer】 a checkpoint flag every level; planting it immediately clears debuffs in range and 3 s later grants everyone random buffs; each plant adds petrify to yourself.";
                case 12: return "【Dance together】 players dancing in your range grant extra stamina; your own dance forces nearby players to dance; periodic forced backflips that always end in a fall.";
                case 13: return "【Meme spread】 voice and egg cycles grant extra stamina; contagious reclassing — you can force-merge others by feeding fried eggs; same-class players heal and take less injury over a larger range, but periodic backflips always end in a fall.";
                case 14: return "【Input text here】 food and most items are disabled and picked-up items vanish; hunger resets to zero every level; extra stamina refills periodically; any injury is amplified into a knockdown; immune to the Pandora, Necromancer, Dancer and Qu class effects.";
                case 15: return "【Random casting】 scrolls on a timer; opening one triggers a random effect and adds petrify/curse; the human form leans support, the skeleton form trades a high cost for high gain.";
                case 16: return "【Aerial mobility】 high starting curse and petrify need careful management; starts with a golden grapnel and a glider; hooking a player with the grapnel immediately triggers a heavy injury penalty; immune to Pandora effects and picked-up Pandora lunchboxes vanish.";
                case 17: return "【Grill master】 portable stoves on a timer; cooking anything sates hunger; cooking too much poisons you; periodically cooking non-food (tools) grants extra stamina; holding a portable stove causes frequent heat.";
                case 18: return "【Food security】 portable stoves on a timer; cooking raw food restores hunger and stamina around you; cooked food pays extra, raw food punishes hard; nearby hungry players make you drowsy; special bond with the Logistician.";
                case 19: return "【Not so peaceful】 the right-click grab becomes a kick; kicking a player grants extra stamina, missing adds drowsiness; starts with curse.";
                case 20: return "【Carrying the weight】 starts with a backpack; once your load passes the threshold you periodically gain a sprint and extra stamina.";
                case 21: return "【Fireline rescue】 a teammate's faint auto-teleports you with invincibility; picking them up grants a sprint and a large amount of extra stamina; carrying a player heals both; a player's death gives you petrify.";
                case 22: return "【Exquisite sunscreen】 three sunscreens every level, force-overwriting your inventory; gaining sunscreen grants extra stamina, and finishing each bottle grants more extra stamina plus petrify.";
                case 23: return "【Single-player mode】 periodically rewarded with extra stamina when far from others; constant drowsiness when someone is close.";
                case 24: return "【Heavy gunner】 a scout cannon every level; scout cannons on a timer; taking damage grants dynamite, which is forced into your main hand when the inventory is full.";
                case 25: return "【The playing band】 picked-up items may become instruments; inventory and performance grant area extra stamina; holding all three instruments grants an extra sprint, and giving them to others has a low chance to reclass them.";
                case 26: return "【Scoutmaster's soul】 starting curse is locked and only Scout gem items can lower it; Scout gems grant exclusive bonuses; all non-gem items are disabled and picked-up items vanish; instant teleport rescue, never abandon others; collecting every Scout gem grants an extra reward; freeing the soul at the Nadir rewards everyone.";
                case 27: return "【Wriggling Qu】 frequent forced let-go while climbing; profit from decomposing junk items and accumulating early bugs; contagious reclassing — periodically applies spores nearby, and past the threshold forces others to merge and grants early bugs; same-class clusters get sprint and infinite stamina rewards; immune to spores; eating early bugs costs curse.";
                case 28: return "【Space drift】 balloons are tied on periodically; balloons and bunches of balloons cannot be picked up; picked-up items may become rocket packs or jetpacks; only packaged or cooked food, otherwise poison; some heat/cold resistance but certain debuffs hit harder.";
                case 29: return "【Big-berry addiction】 green/purple kingberries on a timer; green grants a sprint, purple grants infinite stamina; other food adds drowsiness; every meal costs injury; others who chain-eat kingberries are forced to merge.";
                case 30: return "【Power of code】 random insects on a timer, usually with poison; extra stamina overflow wraps around; periodic sprint at night; solo play periodically cleanses debuffs; with no debuffs and no insects you copy another present class's effect for a while.";
                default: return string.Empty;
            }
        }
    }
}

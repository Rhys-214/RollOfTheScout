using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace RollOfTheScout
{
    /// <summary>
    /// 全部可调参数。只有主机（房主）的配置文件有意义，其他玩家即使安装了本模组也不会生效。
    ///
    /// 中英双语：本模组按「游戏语言」择一加载配置文件（见 <see cref="ConfigLocalization"/>）——
    /// · 游戏语言为中文（简体 / 繁体）：使用 <c>com.peakmodding.RollOfTheScout.cfg</c>，Section / Key / Description 均为中文；
    /// · 其它语言：使用 <c>com.peakmodding.RollOfTheScout.en.cfg</c>，Section 为英文名、Key 为小驼峰（camelCase）、Description 为英文。
    /// 之所以 Section / Key 也随之本地化：PEAKModding 的 ModConfig 模组在局内配置界面里显示的正是
    /// 「Section（分类页签）」与「Key（条目名）」（Description 不显示），两者必须是当前语言，界面才会跟着切换语言。
    /// </summary>
    public static class ModConfig
    {
        public static GeneralConfig General { get; private set; } = new GeneralConfig();
        public static ExplorerConfig Explorer { get; private set; } = new ExplorerConfig();
        public static LeaderConfig Leader { get; private set; } = new LeaderConfig();
        public static LogisticianConfig Logistician { get; private set; } = new LogisticianConfig();
        public static DoctorConfig Doctor { get; private set; } = new DoctorConfig();
        public static AssassinConfig Assassin { get; private set; } = new AssassinConfig();
        public static PandoraConfig Pandora { get; private set; } = new PandoraConfig();
        public static NecromancerConfig Necromancer { get; private set; } = new NecromancerConfig();
        public static MConfig M { get; private set; } = new MConfig();
        public static SavageConfig Savage { get; private set; } = new SavageConfig();
        public static ScoutConfig Scout { get; private set; } = new ScoutConfig();
        public static StandardBearerConfig StandardBearer { get; private set; } = new StandardBearerConfig();
        public static DancerConfig Dancer { get; private set; } = new DancerConfig();
        public static DaihaoConfig Daihao { get; private set; } = new DaihaoConfig();
        public static SaintConfig Saint { get; private set; } = new SaintConfig();
        public static MageConfig Mage { get; private set; } = new MageConfig();
        public static ChallengerConfig Challenger { get; private set; } = new ChallengerConfig();
        public static MasterChefConfig MasterChef { get; private set; } = new MasterChefConfig();
        public static CookConfig Cook { get; private set; } = new CookConfig();
        public static PacifistConfig Pacifist { get; private set; } = new PacifistConfig();
        public static HamsterConfig Hamster { get; private set; } = new HamsterConfig();
        public static FirefighterConfig Firefighter { get; private set; } = new FirefighterConfig();
        public static NarcissistConfig Narcissist { get; private set; } = new NarcissistConfig();
        public static LoneWolfConfig LoneWolf { get; private set; } = new LoneWolfConfig();
        public static GunnerConfig Gunner { get; private set; } = new GunnerConfig();
        public static BandConfig Band { get; private set; } = new BandConfig();
        public static MilesSoulConfig MilesSoul { get; private set; } = new MilesSoulConfig();
        public static QuConfig Qu { get; private set; } = new QuConfig();
        public static AstronautConfig Astronaut { get; private set; } = new AstronautConfig();
        public static LangwangConfig Langwang { get; private set; } = new LangwangConfig();
        public static ProgrammerConfig Programmer { get; private set; } = new ProgrammerConfig();

        /// <summary>
        /// 本模组实际使用的配置文件（按游戏语言在中文 / 英文两个文件之间择一）。
        /// </summary>
        public static ConfigFile File { get; private set; }

        public static void Initialize()
        {
            ConfigLocalization.DetectLanguage();

            string fileName = ConfigLocalization.UseEnglish
                ? "com.peakmodding.RollOfTheScout.en.cfg"
                : "com.peakmodding.RollOfTheScout.cfg";
            File = new ConfigFile(Path.Combine(Paths.ConfigPath, fileName), true);
            File.SaveOnConfigSet = true;

            General.Initialize(File);
            Explorer.Initialize(File);
            Leader.Initialize(File);
            Logistician.Initialize(File);
            Doctor.Initialize(File);
            Assassin.Initialize(File);
            Pandora.Initialize(File);
            Necromancer.Initialize(File);
            M.Initialize(File);
            Savage.Initialize(File);
            Scout.Initialize(File);
            StandardBearer.Initialize(File);
            Dancer.Initialize(File);
            Daihao.Initialize(File);
            Saint.Initialize(File);
            Mage.Initialize(File);
            Challenger.Initialize(File);
            MasterChef.Initialize(File);
            Cook.Initialize(File);
            Pacifist.Initialize(File);
            Hamster.Initialize(File);
            Firefighter.Initialize(File);
            Narcissist.Initialize(File);
            LoneWolf.Initialize(File);
            Gunner.Initialize(File);
            Band.Initialize(File);
            MilesSoul.Initialize(File);
            Qu.Initialize(File);
            Astronaut.Initialize(File);
            Langwang.Initialize(File);
            Programmer.Initialize(File);
        }

        /// <summary>
        /// 本地化版 <c>config.Bind</c>：按当前语言翻译 Section / Key / Description 后再绑定。
        /// 中文语言下三者均为传入的中文原文；英文语言下改用英文分类名 / 小驼峰键 / 英文说明
        /// （翻译表见 <see cref="ConfigLocalization"/>；未收录的条目自动回退为中文原文）。
        /// </summary>
        private static ConfigEntry<T> BindL<T>(ConfigFile config, string zhSection, string zhKey,
            T defaultValue, string zhDescription)
        {
            return config.Bind(
                ConfigLocalization.Section(zhSection),
                ConfigLocalization.Key(zhSection, zhKey),
                defaultValue,
                ConfigLocalization.Desc(zhSection, zhKey, zhDescription));
        }

        public class GeneralConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<bool> RandomAssign { get; private set; }
            public ConfigEntry<float> PollInterval { get; private set; }
            public ConfigEntry<string> ClassListKey { get; private set; }
            public ConfigEntry<string> ClassListMode { get; private set; }
            public ConfigEntry<bool> MageEffectNotify { get; private set; }
            public ConfigEntry<bool> DisplaySync { get; private set; }
            public ConfigEntry<bool> NamePrefix { get; private set; }
            public ConfigEntry<bool> ReclassEnabled { get; private set; }
            public ConfigEntry<string> ReclassItem { get; private set; }
            public ConfigEntry<int> ReclassCount { get; private set; }
            public ConfigEntry<bool> NightHeatEnabled { get; private set; }
            public ConfigEntry<float> NightHeatIntervalSeconds { get; private set; }
            public ConfigEntry<float> NightHeatAmount { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "通用", "启用模组", true,
                    "总开关。关闭后主机不再分配职业、不再施加任何职业效果。");
                RandomAssign = BindL(config, "通用", "随机分配职业", true,
                    "是否由主机随机分配职业。true=按权重随机分配（无职业选择界面）；false=按职业顺序依次分配。");
                PollInterval = BindL(config, "通用", "状态轮询间隔", 0.5f,
                    "主机轮询玩家同步状态的间隔（秒），用于「不屈 / 高代谢 / 共情」的闭环检测。建议 0.25~1。");
                ClassListKey = BindL(config, "通用", "职业显示-按键", "Tab",
                    "按哪个键在主机屏幕上显示「当前职业分配情况」。\n" +
                    "填写 Unity 的 KeyCode 名，例如 Tab、F1、LeftAlt、BackQuote、Mouse2。\n" +
                    "注意：客机没有安装本模组，该面板只能显示在主机（房主）自己的屏幕上。");
                // 该项的「值」也随语言变化：英文配置用 HoldToShow / PressToShow / ToggleOnPress，
                // 中文配置沿用 按住显示 / 按下显示 / 按下切换；代码对中英两种写法都识别（见 HostClassManager）。
                ClassListMode = BindL(config, "通用", "职业显示-模式",
                    ConfigLocalization.UseEnglish ? "HoldToShow" : "按住显示",
                    "职业分配面板的显示方式：\n" +
                    "按住显示 = 按住该键时显示，松开立即隐藏；\n" +
                    "按下显示 = 按一下显示 5 秒后自动隐藏；\n" +
                    "按下切换 = 每按一次在「显示 / 隐藏」之间切换。");
                MageEffectNotify = BindL(config, "通用", "法师效果提示", true,
                    "是否在主机屏幕左上角提示区，显示法师每次触发的是什么效果。\n" +
                    "默认 true。提示形如「法师：XXX 触发【强化】」，显示在左上角、约 8 秒后消失。\n" +
                    "提示只出现在主机（房主）自己的屏幕上；客机未安装本模组，看不到。\n" +
                    "若客机安装了附属模组 com.peakmodding.RollOfTheScoutClient，见下方「职业显示-网络同步」。");
                DisplaySync = BindL(config, "通用", "职业显示-网络同步", true,
                    "是否把「职业分配情况」与「职业效果提示」广播给安装了附属模组\n" +
                    "com.peakmodding.RollOfTheScoutClient 的客机（供其在客机自己屏幕上显示）。\n" +
                    "原理：主机用 Photon 自定义事件（码 197）周期广播职业快照、并在每次提示时广播该提示文本。\n" +
                    "未安装附属模组的客机完全不受影响（收到即忽略），因此客机仍然零安装可用。\n" +
                    "默认 true。设为 false 可彻底关闭该广播（客机侧将看不到任何职业显示）。");
                NamePrefix = BindL(config, "通用", "头顶职业前缀", true,
                    "是否把「头顶玩家名称」改写成「【职业】昵称」，例如「Rhys」→「【先驱】Rhys」。\n" +
                    "「【职业】」用该玩家进入游戏时的皮肤色着色；「昵称」维持原版颜色不变。\n" +
                    "⚠ 头顶名称是**每个客户端本地**的 UI，因此本开关只影响**本机屏幕**上看到的名字：\n" +
                    "   · 主机：显示本局账本里的职业；\n" +
                    "   · 装了本模组或附属模组的客机：显示主机广播过来的职业（见「职业显示-网络同步」）；\n" +
                    "   · 未装任何模组的客机：看不到任何前缀（仍显示原版昵称）。\n" +
                    "默认 true。");
                ReclassEnabled = BindL(config, "通用", "转职-启用", true,
                    "转职机制总开关。true=玩家食用下方「转职-触发物品」后可更换职业；false=完全关闭。\n" +
                    "转职会在「当前未占满名额的其他职业」中按权重随机重新分配；\n" +
                    "每名玩家每局最多成功转职的次数由下方「转职-次数」决定；\n" +
                    "若没有可转的职业（其余职业名额都已满）则本次不生效、不消耗次数。");
                ReclassItem = BindL(config, "通用", "转职-触发物品", "Mushroom Glow",
                    "食用哪种物品可触发转职（填物品预制体名）。默认 Mushroom Glow = 诡异菇。\n" +
                    "填错或留空则转职机制不会触发。");
                ReclassCount = BindL(config, "通用", "转职-次数", 1,
                    "每名玩家每局最多可成功转职的次数（默认 1）。\n" +
                    "每次成功转职消耗 1 次；没有可转职业时不消耗次数。\n" +
                    "设为 0 等价于关闭转职。每局开始时重置。");
                NightHeatEnabled = BindL(config, "通用", "夜晚抵消寒冷", true,
                    "是否抵消「天阶 5 及以上难度」夜晚的寒冷（夜晚每 5 秒 +2.5 寒冷）。\n" +
                    "原理：该夜寒由原版在**每个玩家自己的客户端上**结算，主机无法直接关闭，\n" +
                    "因此改为由主机周期性给所有存活玩家 +「炎热」——原版里炎热与寒冷互为抵消。\n" +
                    "仅在「夜晚 + 天阶>=5（或自定义局开启冷夜）+ 尚未走完 3 个检查点」时生效，条件与原版一致。\n" +
                    "默认 true（开启）。设为 false 恢复原版夜寒。");
                NightHeatIntervalSeconds = BindL(config, "通用", "夜晚抵消寒冷-间隔", 5f,
                    "每隔多少秒下发一次「炎热」（秒）。默认 5，与原版夜寒的积累节奏一致。\n" +
                    "设为 0 或负数关闭本效果。");
                NightHeatAmount = BindL(config, "通用", "夜晚抵消寒冷-量", 0.025f,
                    "每次下发的「炎热」量（0.025 = 2.5 点）。\n" +
                    "默认 0.025：满夜时原版正好每 5 秒 +2.5 寒冷（0.005/秒），因此该默认值可完全抵消。\n" +
                    "若同时受其它寒冷来源影响，可适当调大。");
            }
        }

        public class ExplorerConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<int> BackpackItemCount { get; private set; }
            public ConfigEntry<int> RopeSpoolWeight { get; private set; }
            public ConfigEntry<int> ClimbingSpikeWeight { get; private set; }
            public ConfigEntry<int> ScoutCookiesWeight { get; private set; }
            public ConfigEntry<int> ChainShooterWeight { get; private set; }
            public ConfigEntry<int> RescueHookWeight { get; private set; }
            public ConfigEntry<int> AntiZookaWeight { get; private set; }
            public ConfigEntry<float> HungerIntervalSeconds { get; private set; }
            public ConfigEntry<float> HungerPerTick { get; private set; }
            public ConfigEntry<float> SprintIntervalSeconds { get; private set; }
            public ConfigEntry<float> DrowsyClearAmount { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "先驱", "启用职业", true, "是否启用先驱职业。");
                MaxPlayers = BindL(config, "先驱", "最大人数", 1,
                    "先驱最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "先驱", "最小人数", 0,
                    "先驱最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "先驱", "抽取权重", 10, "随机分配时先驱被抽中的权重。");
                BackpackItemCount = BindL(config, "先驱", "背包物品数", 4,
                    "开路：每次进入新关卡时直接装备到该玩家身上的背包内装入几件物品（背包容量 4）。\n" +
                    "物品在下方物品池中按权重「独立随机」抽取，允许重复。");
                RopeSpoolWeight = BindL(config, "先驱", "物品权重-绳索", 10, "物品池权重：绳索 RopeSpool。");
                ClimbingSpikeWeight = BindL(config, "先驱", "物品权重-岩钉", 7, "物品池权重：岩钉 ClimbingSpike。");
                ScoutCookiesWeight = BindL(config, "先驱", "物品权重-童军饼干", 2, "物品池权重：童军饼干 ScoutCookies。");
                ChainShooterWeight = BindL(config, "先驱", "物品权重-锁链发射器", 2, "物品池权重：锁链发射器 ChainShooter。");
                RescueHookWeight = BindL(config, "先驱", "物品权重-救援抓钩", 2, "物品池权重：救援抓钩 RescueHook。");
                AntiZookaWeight = BindL(config, "先驱", "物品权重-反重力炮", 1, "物品池权重：反重力炮 AntiZooka。");
                HungerIntervalSeconds = BindL(config, "先驱", "高代谢-饥饿间隔", 20f,
                    "高代谢：每隔多少秒给先驱注入一次饥饿（秒）。默认 20。\n" +
                    "该注入会直接生效并清空原版的饥饿累积器，因此会「覆盖替换」游戏默认的饥饿自然增长。");
                HungerPerTick = BindL(config, "先驱", "高代谢-每次饥饿", 0.025f,
                    "高代谢：每次注入的饥饿量。0.025 = 显示 2.5 点（引擎最小有效步长）。");
                SprintIntervalSeconds = BindL(config, "先驱", "高代谢-加速间隔", 40f,
                    "高代谢：每隔多少秒让先驱获得一次加速（秒）。默认 40。\n" +
                    "加速通过让先驱「饮用」一瓶能量饮料实现；时长由游戏内能量饮料自身决定（约 10 秒），\n" +
                    "仅房主安装时无法改写该时长，实际值会打印到日志。");
                DrowsyClearAmount = BindL(config, "先驱", "高代谢-加速后清除困倦", 0.25f,
                    "高代谢：加速效果结束时，为先驱清除的困倦值（0.25 = 25 点）。\n" +
                    "即抵消饮用能量饮料在加速结束时会附加的困倦效果。设为 0 关闭。");
            }
        }

        public class LeaderConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> InspireIntervalSeconds { get; private set; }
            public ConfigEntry<float> InspireRadius { get; private set; }
            public ConfigEntry<float> InspireExtraStamina { get; private set; }
            public ConfigEntry<float> InspireHungerAmount { get; private set; }
            public ConfigEntry<float> InspireHealAmount { get; private set; }
            public ConfigEntry<float> InspireDrowsyIntervalSeconds { get; private set; }
            public ConfigEntry<float> InspireDrowsyRadius { get; private set; }
            public ConfigEntry<float> InspireDrowsyClearAmount { get; private set; }
            public ConfigEntry<float> InitCurseAmount { get; private set; }
            public ConfigEntry<float> InitPetrifyAmount { get; private set; }
            public ConfigEntry<float> InitExtraStamina { get; private set; }
            public ConfigEntry<float> CursePerDeath { get; private set; }
            public ConfigEntry<float> PetrifyPerDeath { get; private set; }
            public ConfigEntry<float> CurseReducePerAlive { get; private set; }
            public ConfigEntry<bool> CurseReduceIncludeLeader { get; private set; }
            public ConfigEntry<float> MoraleCurseAmount { get; private set; }
            public ConfigEntry<float> MoraleExtraStamina { get; private set; }
            public ConfigEntry<float> MoralePetrifyAmount { get; private set; }
            public ConfigEntry<int> DivineExtraMystical { get; private set; }
            public ConfigEntry<string> DivineItemPool { get; private set; }
            public ConfigEntry<int> RevivePetrifyCap { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "领袖", "启用职业", true, "是否启用领袖职业。");
                MaxPlayers = BindL(config, "领袖", "最大人数", 1,
                    "领袖最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "领袖", "最小人数", 0,
                    "领袖最少分配给几名玩家。人数不足时会优先补齐该职业。默认 0（不强制）。");
                Weight = BindL(config, "领袖", "抽取权重", 10, "随机分配时领袖被抽中的权重。");
                InspireIntervalSeconds = BindL(config, "领袖", "号召-触发间隔", 180f,
                    "领袖号召：自动触发的间隔（秒），默认 180 = 3 分钟。" +
                    "每次触发对半径内所有人（含领袖自己）加额外体力、恢复饥饿与损伤。");
                InspireRadius = BindL(config, "领袖", "号召-作用半径", 30f, "领袖号召：作用半径（米）。");
                InspireExtraStamina = BindL(config, "领袖", "号召-额外体力", 0.25f,
                    "领袖号召：额外体力（0.25 = 25）。");
                InspireHungerAmount = BindL(config, "领袖", "号召-恢复饥饿", 0.1f,
                    "领袖号召：恢复的饥饿值（0.1 = 10 点）。");
                InspireHealAmount = BindL(config, "领袖", "号召-恢复损伤", 0.05f,
                    "领袖号召：每次触发为半径内所有人（含领袖自己）恢复的损伤（0.05 = 5 点）。设为 0 关闭。");
                InspireDrowsyIntervalSeconds = BindL(config, "领袖", "号召-困倦间隔", 60f,
                    "领袖号召·清醒：单独计时的困倦清除间隔（秒），默认 60 = 1 分钟。\n" +
                    "与上方「号召-触发间隔」相互独立（体力/饥饿/损伤按 3 分钟一次，困倦按 1 分钟一次）。");
                InspireDrowsyRadius = BindL(config, "领袖", "号召-困倦半径", 20f,
                    "领袖号召·清醒：作用半径（米），默认 20。");
                InspireDrowsyClearAmount = BindL(config, "领袖", "号召-清除困倦", 0.30f,
                    "领袖号召·清醒：每次为半径内所有人（含领袖自己）清除的困倦（0.30 = 30 点）。");
                InitCurseAmount = BindL(config, "领袖", "宿命-初始诅咒", 0.20f,
                    "宿命：开局初始诅咒（0.20 = 20 点）。");
                InitPetrifyAmount = BindL(config, "领袖", "宿命-初始石化", 0.20f,
                    "宿命：开局初始石化（0.20 = 20 点）。");
                InitExtraStamina = BindL(config, "领袖", "宿命-初始额外体力", 0.30f,
                    "宿命：开局初始额外体力（0.30 = 30 点）。");
                CursePerDeath = BindL(config, "领袖", "圣洁-每死亡者诅咒", 0.05f,
                    "圣洁：使用古老雕像时，每有 1 人死亡则领袖增加的诅咒（0.05 = 5 点）。");
                PetrifyPerDeath = BindL(config, "领袖", "圣洁-每死亡者石化", 0.10f,
                    "圣洁：使用古老雕像时，每有 1 人死亡则领袖增加的石化（0.10 = 10 点）。");
                CurseReducePerAlive = BindL(config, "领袖", "圣洁-每存活者消除诅咒", 0.025f,
                    "圣洁：使用古老雕像时，每有 1 人存活则领袖消除的诅咒（0.025 = 2.5 点）。\n" +
                    "存活人数是否含领袖本人，由下方「圣洁-存活计数含领袖」决定。\n" +
                    "注意：游戏状态的「最小步长」为 0.025（STATUS_INCREMENT），低于该值不会生效，\n" +
                    "0.025（= 显示 2.5 点）即引擎允许的最小有效变动量。");
                CurseReduceIncludeLeader = BindL(config, "领袖", "圣洁-存活计数含领袖", false,
                    "圣洁：计算「每有 1 人存活则领袖消除诅咒」时，存活人数是否把领袖本人也计入。\n" +
                    "false（默认）= 不含领袖本人；true = 含领袖本人（即领袖活着时自己多消一份）。");
                MoraleCurseAmount = BindL(config, "领袖", "士气-诅咒", 0.10f,
                    "士气：领袖死亡时，对每名存活玩家施加的诅咒（0.10 = 10 点）；领袖复活时等量消除。");
                MoraleExtraStamina = BindL(config, "领袖", "士气-额外体力", 0.30f,
                    "士气：领袖死亡时，对每名存活玩家施加的额外体力（0.30 = 30 点）。");
                MoralePetrifyAmount = BindL(config, "领袖", "士气-石化", 0.10f,
                    "士气：领袖死亡时，对每名存活玩家施加的石化（0.10 = 10 点）；领袖复活时等量消除。\n" +
                    "另外领袖死亡时，存活玩家还会获得一次持续约 10 秒的加速（能量饮料效果）。");
                DivineExtraMystical = BindL(config, "领袖", "神启-额外神秘物品", 1,
                    "神启：全员存活时使用古老雕像，为领袖额外发放的神秘物品数量。\n" +
                    "优先直接塞进领袖的 3 个物品栏空位；三个物品栏都满时改为在篝火附近生成并固定（不会滚落）。设为 0 关闭。");
                DivineItemPool = BindL(config, "领袖", "神启-物品池",
                    "BookOfBones,PandorasBox,ScoutEffigy,Lantern_Faerie,Bugle_Magic,Cure-All," +
                    "RitualDagger,AntiZooka,Anti-Rope Spool,RopeShooterAnti,AncientIdol,Cursed Skull",
                    "神启：额外神秘物品的「白名单」物品池（英文逗号分隔的预制体名，从名单中等概率随机抽取）。\n" +
                    "默认只包含真正的神秘物品；留空则会退化为「按原版 Mystical 标签筛选」，\n" +
                    "该筛选会把未实装物品（例如 ClimbingChalk）也算进来，不建议留空。");
                RevivePetrifyCap = BindL(config, "领袖", "复活继承-石化上限", 90,
                    "领袖复活后继承死亡瞬间的石化值，但最高只继承到该值（0~100）。\n" +
                    "例：死亡时石化 100 → 复活后石化 = 90；死亡时石化 20 → 复活后石化 = 20。\n" +
                    "该上限用于防止「满石化死亡 → 复活即满石化 → 无限死亡循环」。");
            }
        }

        public class LogisticianConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> AirdropIntervalSeconds { get; private set; }
            public ConfigEntry<int> AirdropCount { get; private set; }
            public ConfigEntry<string> AirdropPool { get; private set; }
            public ConfigEntry<string> AirdropBlacklist { get; private set; }
            public ConfigEntry<int> InitialBackpacks { get; private set; }
            public ConfigEntry<int> BackpackWeight { get; private set; }
            public ConfigEntry<int> JetpackWeight { get; private set; }
            public ConfigEntry<float> InitCurseAmount { get; private set; }
            public ConfigEntry<float> InitPetrifyAmount { get; private set; }
            public ConfigEntry<int> RevivePetrifyFixed { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "后勤", "启用职业", true, "是否启用后勤职业。");
                MaxPlayers = BindL(config, "后勤", "最大人数", 1,
                    "后勤最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "后勤", "最小人数", 0,
                    "后勤最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "后勤", "抽取权重", 10, "随机分配时后勤被抽中的权重。");
                AirdropIntervalSeconds = BindL(config, "后勤", "空投-触发间隔", 90f,
                    "紧急空投：自动触发的间隔（秒），默认 90 = 1.5 分钟。");
                AirdropCount = BindL(config, "后勤", "空投-每次数量", 1, "紧急空投：每次发放的物品数量。");
                AirdropPool = BindL(config, "后勤", "空投-物品池",
                    "RopeSpool,ClimbingSpike,Lantern,Torch,Parasol,Flare,RescueHook,Glider,Parachute,RopeShooter,ChainShooter,PortableStovetopItem,Balloon,BalloonBunch,Antidote,AloeVera,Heat Pack,Sunscreen,Bandages,MedicinalRoot,FirstAidKit," +
                    "ScoutCookies,Energy Drink,Sports Drink,Compass,Pirate Compass,Guidebook,Binoculars,Candle,Frisbee,Stone,ScoutCannonItem,BingBong",
                    "紧急空投：可掉落物品的预制体名列表（英文逗号分隔）。留空表示使用全部非禁用物品。\n" +
                    "名称中含空格的要保留原样（如 Energy Drink）。");
                AirdropBlacklist = BindL(config, "后勤", "空投-物品黑名单",
                    "Cheat Compass,Cheat Compass 1,Passport,RescueHook_Infinite," +
                    "Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor",
                    "紧急空投：物品黑名单（英文逗号分隔），优先级高于物品池。\n" +
                    "默认排除 5 件童军护符：童军的毅力/进取/盛情/野心/荣耀。");
                InitialBackpacks = BindL(config, "后勤", "备战充分-初始背包数", 1,
                    "备战充分：开局初始直接发放的背包类物品数量。\n" +
                    "每件物品在「背包」与「喷气背包」之间按下方两个权重二选一。");
                BackpackWeight = BindL(config, "后勤", "备战充分-背包权重", 5,
                    "备战充分：发放的物品为「背包 Backpack」的权重。");
                JetpackWeight = BindL(config, "后勤", "备战充分-喷气背包权重", 5,
                    "备战充分：发放的物品为「喷气背包 Jetpack」的权重。\n" +
                    "与「备战充分-背包权重」默认各 5，即各 50% 概率。");
                InitCurseAmount = BindL(config, "后勤", "积损-初始诅咒", 0.10f,
                    "积损：开局初始诅咒（0.10 = 10 点）。");
                InitPetrifyAmount = BindL(config, "后勤", "积损-初始石化", 0.70f,
                    "积损：开局初始石化（0.70 = 70 点）。");
                RevivePetrifyFixed = BindL(config, "后勤", "复活继承-石化值", 70,
                    "后勤不再继承死亡瞬间的石化值，而是在每次复活后把石化「设置」为该固定值（0~100）。\n" +
                    "例：死亡时石化 100 → 复活后石化 = 70（而不是 25 + 70 = 95 导致立刻再次石化死亡）。");
            }
        }

        public class DoctorConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<int> BackpackItemCount { get; private set; }
            public ConfigEntry<int> BandagesWeight { get; private set; }
            public ConfigEntry<int> MedicinalRootWeight { get; private set; }
            public ConfigEntry<int> FirstAidKitWeight { get; private set; }
            public ConfigEntry<int> CureAllWeight { get; private set; }
            public ConfigEntry<int> HealingPuffShroomWeight { get; private set; }
            public ConfigEntry<float> PoisonReduceInterval { get; private set; }
            public ConfigEntry<float> PoisonReduceRadius { get; private set; }
            public ConfigEntry<float> PoisonReduceAlly { get; private set; }
            public ConfigEntry<float> PoisonReduceSelfMultiplier { get; private set; }
            public ConfigEntry<float> NursingInterval { get; private set; }
            public ConfigEntry<float> NursingRadius { get; private set; }
            public ConfigEntry<float> NursingAlly { get; private set; }
            public ConfigEntry<float> NursingSelfMultiplier { get; private set; }
            public ConfigEntry<float> EmpathyDamage { get; private set; }
            public ConfigEntry<float> EmpathyCooldown { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "医生", "启用职业", true, "是否启用医生职业。");
                MaxPlayers = BindL(config, "医生", "最大人数", 1,
                    "医生最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "医生", "最小人数", 0,
                    "医生最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "医生", "抽取权重", 10, "随机分配时医生被抽中的权重。");
                BackpackItemCount = BindL(config, "医生", "背包物品数", 4,
                    "随身医疗箱：每次进入新关卡时直接装备到该玩家身上的背包内装入几件物品（背包容量 4）。\n" +
                    "物品在下方物品池中按权重「独立随机」抽取，允许重复。");
                BandagesWeight = BindL(config, "医生", "物品权重-绷带", 8, "物品池权重：绷带 Bandages。");
                MedicinalRootWeight = BindL(config, "医生", "物品权重-药用根茎", 6, "物品池权重：药用根茎 MedicinalRoot。");
                FirstAidKitWeight = BindL(config, "医生", "物品权重-医疗箱", 5, "物品池权重：医疗箱 FirstAidKit。");
                CureAllWeight = BindL(config, "医生", "物品权重-万灵药", 1, "物品池权重：万灵药 Cure-All。");
                HealingPuffShroomWeight = BindL(config, "医生", "物品权重-灵药菇", 5, "物品池权重：灵药菇 HealingPuffShroom。");
                PoisonReduceInterval = BindL(config, "医生", "毒理学-触发间隔", 3f,
                    "毒理学（原「毒性熟悉」）：触发间隔（秒）。");
                PoisonReduceRadius = BindL(config, "医生", "毒理学-作用半径", 5f,
                    "毒理学（原「毒性熟悉」）：作用半径（米）。");
                PoisonReduceAlly = BindL(config, "医生", "毒理学-队友削减量", 0.025f,
                    "毒理学（原「毒性熟悉」）：对队友削减的中毒/孢子（0.025 = 2.5 点）。");
                PoisonReduceSelfMultiplier = BindL(config, "医生", "毒理学-自身倍率", 1f,
                    "毒理学（原「毒性熟悉」）：对自身削减的倍率（1 = 与队友相同）。");
                NursingInterval = BindL(config, "医生", "护理学-触发间隔", 8f,
                    "护理学：每隔多少秒对半径内所有玩家治疗一次损伤（秒），默认 8。");
                NursingRadius = BindL(config, "医生", "护理学-作用半径", 5f,
                    "护理学：作用半径（米），默认 5。");
                NursingAlly = BindL(config, "医生", "护理学-队友削减量", 0.025f,
                    "护理学：对半径内队友削减（治疗）的损伤（0.025 = 2.5 点）。");
                NursingSelfMultiplier = BindL(config, "医生", "护理学-自身倍率", 1f,
                    "护理学：对医生自身削减（治疗）的倍率（1 = 与队友相同）。");
                EmpathyDamage = BindL(config, "医生", "共情-自身损伤", 0.10f,
                    "共情：队友真正死亡时自身受到的损伤（0.10 = 10 点）。\n" +
                    "注意：仅「死亡」触发，队友昏迷倒地待救不触发。");
                EmpathyCooldown = BindL(config, "医生", "共情-冷却", 5f,
                    "共情：医生受到一次共情损伤后的冷却时间（秒）。\n" +
                    "冷却期间即使再有队友死亡，该医生也不会再次受到共情损伤，\n" +
                    "用于避免「一次死亡多人 → 医生被反复扣损伤而暴毙」。设为 0 关闭冷却。");
            }
        }

        public class AssassinConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<int> InitDaggerCount { get; private set; }
            public ConfigEntry<int> InitBlowgunCount { get; private set; }
            public ConfigEntry<float> PickupDaggerChance { get; private set; }
            public ConfigEntry<int> PickupDaggerWeight { get; private set; }
            public ConfigEntry<int> PickupBlowgunWeight { get; private set; }
            public ConfigEntry<string> PickupBlacklist { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "刺客", "启用职业", true, "是否启用刺客职业。默认 true。");
                MaxPlayers = BindL(config, "刺客", "最大人数", 1,
                    "刺客最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "刺客", "最小人数", 0,
                    "刺客最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "刺客", "抽取权重", 5,
                    "随机分配时刺客被抽中的权重。默认 5。");
                InitDaggerCount = BindL(config, "刺客", "刺杀-初始匕首数", 1,
                    "刺杀：开局直接放入该玩家 3 个物品栏的仪式匕首 RitualDagger 数量（默认 1）。");
                InitBlowgunCount = BindL(config, "刺客", "刺杀-初始吹箭筒数", 1,
                    "刺杀：开局直接放入该玩家 3 个物品栏的吹箭筒 HealingDart Variant 数量（默认 1）。");
                PickupDaggerChance = BindL(config, "刺客", "刺杀-拾取变匕首概率", 0.10f,
                    "刺杀：刺客从世界里拾取任何物品时，该物品被替换为「仪式匕首 / 吹箭筒」的概率（0.10 = 10%）。\n" +
                    "命中后在本两项权重（刺杀-变匕首权重 / 刺杀-变吹箭筒权重）之间随机取一个作为替换目标。\n" +
                    "设为 0 关闭（拾取物品保持原样）。仅主机安装即生效，客机零安装。");
                PickupDaggerWeight = BindL(config, "刺客", "刺杀-变匕首权重", 1,
                    "刺杀：拾取替换命中概率后，变成仪式匕首 RitualDagger 的权重。默认 1。");
                PickupBlowgunWeight = BindL(config, "刺客", "刺杀-变吹箭筒权重", 1,
                    "刺杀：拾取替换命中概率后，变成吹箭筒 HealingDart Variant 的权重。默认 1。\n" +
                    "两项权重之和为 0 时不替换（保持原物品）。");
                PickupBlacklist = BindL(config, "刺客", "刺杀-拾取变匕首黑名单",
                    "HealingDart Variant,Flare,Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor,Mushroom Glow,RitualDagger,Strange Gem",
                    "刺杀：拾取时永不变成仪式匕首 / 吹箭筒的物品黑名单（预制体名，英文逗号分隔）。\n" +
                    "默认排除：吹箭筒 HealingDart Variant、信号棒 Flare、" +
                    "童军的毅力 Amulet_Healing、童军的进取 Amulet_SuperJump、童军的盛情 Amulet_Clone、" +
                    "童军的野心 Amulet_InfiniteStamina、童军的荣耀 ScoutsHonor、诡异菇 Mushroom Glow、" +
                    "仪式匕首 RitualDagger（避免新增职业自带的仪式匕首被再次替换）。\n" +
                    "名称中含空格的要保留原样（如 HealingDart Variant）。留空表示不设黑名单。");
            }
        }

        public class PandoraConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> BoxIntervalSeconds { get; private set; }
            public ConfigEntry<float> BoxRadius { get; private set; }
            public ConfigEntry<float> PickupBoxChance { get; private set; }
            public ConfigEntry<bool> PickupBlacklistEnabled { get; private set; }
            public ConfigEntry<string> PickupBlacklist { get; private set; }
            public ConfigEntry<float> CurseIntervalSeconds { get; private set; }
            public ConfigEntry<float> CurseAmount { get; private set; }
            public ConfigEntry<bool> BoxIncludeSelf { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "潘多拉", "启用职业", true, "是否启用潘多拉之子职业。");
                MaxPlayers = BindL(config, "潘多拉", "最大人数", 1,
                    "潘多拉之子最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "潘多拉", "最小人数", 0,
                    "潘多拉之子最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "潘多拉", "抽取权重", 5, "随机分配时潘多拉之子被抽中的权重。");
                BoxIntervalSeconds = BindL(config, "潘多拉", "潘多拉-触发间隔", 5f,
                    "潘多拉：每隔多少秒对半径内的其他玩家施加一次潘多拉魔盒（秒），默认 5。\n" +
                    "实现方式与先驱「高代谢」的加速一致：生成一个潘多拉魔盒并让目标玩家「使用」它。");
                BoxRadius = BindL(config, "潘多拉", "潘多拉-作用半径", 5f,
                    "潘多拉：作用半径（米），默认 5。范围内的其他玩家会被施加潘多拉魔盒（不含自己）。");
                PickupBoxChance = BindL(config, "潘多拉", "潘多拉-拾取变魔盒概率", 0.5f,
                    "潘多拉：该职业从世界里拾取任何物品时，该物品变成潘多拉魔盒的概率（0.5 = 50%）。\n" +
                    "剩余概率（1 − 该值）下，该物品会直接消失 —— 即「消失概率 = 1 − 该值」，两者互补。\n" +
                    "例如 0.5 = 50% 变魔盒 / 50% 消失；0 = 100% 消失；1 = 100% 变魔盒。\n" +
                    "命中「潘多拉-拾取黑名单」的物品不受影响。仅主机安装即生效，客机零安装。");
                PickupBlacklistEnabled = BindL(config, "潘多拉", "潘多拉-拾取黑名单启用", true,
                    "潘多拉：是否启用「拾取黑名单」。\n" +
                    "开启（默认）时，黑名单内的物品被潘多拉之子拾取不会发生变化与消失；\n" +
                    "关闭时黑名单不生效，所有拾取到的物品都会参与「变魔盒 / 消失」判定。");
                PickupBlacklist = BindL(config, "潘多拉", "潘多拉-拾取黑名单",
                    "PandorasBox,Flare,Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor,Mushroom Glow,Strange Gem",
                    "潘多拉：拾取时「不受潘多拉效果影响」的物品黑名单（预制体名，英文逗号分隔）。\n" +
                    "默认排除：潘多拉魔盒 PandorasBox、信号棒 Flare、" +
                    "童军的毅力 Amulet_Healing、童军的进取 Amulet_SuperJump、童军的盛情 Amulet_Clone、" +
                    "童军的野心 Amulet_InfiniteStamina、童军的荣耀 ScoutsHonor、诡异菇 Mushroom Glow。\n" +
                    "名称中含空格的要保留原样。留空表示不设黑名单。仅在「潘多拉-拾取黑名单启用」为 true 时生效。");
                BoxIncludeSelf = BindL(config, "潘多拉", "潘多拉-魔盒含自身", false,
                    "潘多拉：上方「每 N 秒施加潘多拉魔盒」是否把潘多拉之子本人也算进作用范围。\n" +
                    "false（默认）= 只对半径内的其他玩家施加（不含自己）；true = 也包括自己。");
                CurseIntervalSeconds = BindL(config, "潘多拉", "潘多拉-诅咒间隔", 30f,
                    "潘多拉：每隔多少秒为潘多拉之子自身叠加一次诅咒（秒），默认 30。");
                CurseAmount = BindL(config, "潘多拉", "潘多拉-诅咒量", 0.05f,
                    "潘多拉：每次为自己叠加的诅咒量（0.05 = 5 点）。设为 0 关闭。\n" +
                    "注意：状态以 0.025 为最小量子，低于该值的增量不会生效。");
            }
        }

        public class NecromancerConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> InitCurseAmount { get; private set; }
            public ConfigEntry<int> BookCount { get; private set; }
            public ConfigEntry<bool> BoxToBook { get; private set; }
            public ConfigEntry<float> DeathCurseReduce { get; private set; }
            public ConfigEntry<float> DeathExtraStamina { get; private set; }
            public ConfigEntry<float> ReviveHumanCurse { get; private set; }
            public ConfigEntry<float> ReviveSkeletonCurse { get; private set; }
            public ConfigEntry<float> SkeletonStaminaIntervalSeconds { get; private set; }
            public ConfigEntry<float> SkeletonStaminaPer { get; private set; }
            public ConfigEntry<float> SkeletonCurseIntervalSeconds { get; private set; }
            public ConfigEntry<float> SkeletonCursePer { get; private set; }
            public ConfigEntry<float> AuraIntervalSeconds { get; private set; }
            public ConfigEntry<float> AuraRadius { get; private set; }
            public ConfigEntry<float> AuraHumanPoison { get; private set; }
            public ConfigEntry<float> AuraSkeletonStamina { get; private set; }
            public ConfigEntry<float> AuraSkeletonInvincibleIntervalSeconds { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "死灵法师", "启用职业", true, "是否启用死灵法师职业。");
                MaxPlayers = BindL(config, "死灵法师", "最大人数", 1,
                    "死灵法师最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "死灵法师", "最小人数", 0,
                    "死灵法师最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "死灵法师", "抽取权重", 10, "随机分配时死灵法师被抽中的权重。");
                InitCurseAmount = BindL(config, "死灵法师", "骸骨-初始诅咒", 0.50f,
                    "骸骨：开局初始诅咒（0.50 = 50 点）。");
                BookCount = BindL(config, "死灵法师", "骸骨-每层骸骨之书数", 1,
                    "骸骨：每次进入新关卡获得的骸骨之书 BookOfBones 数量。\n" +
                    "优先放入该玩家 3 个物品栏的空位；物品栏已满时在该玩家脚下生成，落地后可拾取。设为 0 关闭。");
                BoxToBook = BindL(config, "死灵法师", "骸骨-魔盒变骸骨之书", true,
                    "骸骨：死灵法师拾取的潘多拉魔盒 PandorasBox 是否直接变为骸骨之书 BookOfBones。\n" +
                    "默认 true（开启）。");
                DeathCurseReduce = BindL(config, "死灵法师", "亡灵-每死亡减诅咒", 0.05f,
                    "亡灵：每有 1 名玩家死亡，死灵法师减少的诅咒（0.05 = 5 点）。");
                DeathExtraStamina = BindL(config, "死灵法师", "亡灵-每死亡加体力", 0.20f,
                    "亡灵：每有 1 名玩家死亡，死灵法师增加的额外体力（0.20 = 20 点）。");
                ReviveHumanCurse = BindL(config, "死灵法师", "亡灵-复活为人类加诅咒", 0.05f,
                    "亡灵：每有 1 名玩家复活为人类，死灵法师增加的诅咒（0.05 = 5 点）。设为 0 关闭。");
                ReviveSkeletonCurse = BindL(config, "死灵法师", "亡灵-复活为骷髅减诅咒", -0.10f,
                    "亡灵：每有 1 名玩家复活为骷髅，死灵法师减少的诅咒（-0.10 = 减 10 点）。设为 0 关闭。");
                SkeletonStaminaIntervalSeconds = BindL(config, "死灵法师", "亡灵-骷髅体力间隔", 60f,
                    "亡灵：场上每有骷髅玩家时，每隔多少秒为死灵法师增加一次额外体力（秒）。默认 60。");
                SkeletonStaminaPer = BindL(config, "死灵法师", "亡灵-每骷髅加体力", 0.15f,
                    "亡灵：每名骷髅玩家每次为死灵法师增加的额外体力（0.15 = 15 点）。按场上骷髅数线性叠加。");
                SkeletonCurseIntervalSeconds = BindL(config, "死灵法师", "亡灵-骷髅减诅咒间隔", 180f,
                    "亡灵：场上每有骷髅玩家时，每隔多少秒为死灵法师减少一次诅咒（秒）。默认 180（3 分钟）。");
                SkeletonCursePer = BindL(config, "死灵法师", "亡灵-每骷髅减诅咒", -0.025f,
                    "亡灵：每名骷髅玩家每次为死灵法师减少的诅咒（-0.025 = 减 2.5 点）。按场上骷髅数线性叠加。\n" +
                    "注意：状态以 0.025 为最小量子，低于该值的增量不会生效。");
                AuraIntervalSeconds = BindL(config, "死灵法师", "亡灵-光环间隔", 5f,
                    "亡灵：死灵法师对半径内玩家施加光环效果的间隔（秒）。默认 5。设为 0 关闭。");
                AuraRadius = BindL(config, "死灵法师", "亡灵-光环半径", 15f,
                    "亡灵：光环的作用半径（米），以死灵法师自身为中心（不含自身）。默认 15。");
                AuraHumanPoison = BindL(config, "死灵法师", "亡灵-光环人类中毒", 0.025f,
                    "亡灵：光环每次为半径内人类玩家增加的中毒（0.025 = 2.5 点）。设为 0 关闭。");
                AuraSkeletonStamina = BindL(config, "死灵法师", "亡灵-光环骷髅体力", 0.025f,
                    "亡灵：光环每次为半径内骷髅玩家增加的额外体力（0.025 = 2.5 点）。设为 0 关闭。");
                AuraSkeletonInvincibleIntervalSeconds = BindL(config, "死灵法师", "亡灵-光环无敌间隔", 10f,
                    "亡灵：每隔多少秒，为光环半径内的骷髅玩家（不含死灵法师自身）施加一次无敌效果（奶白金）。\n" +
                    "默认 10。设为 0 或负数关闭。与「亡灵-光环间隔」各自独立计时，\n" +
                    "受「亡灵-光环半径」限制；人类玩家不受该项影响。");
            }
        }

        public class MConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> SufferMultiplier { get; private set; }
            public ConfigEntry<float> CleanIntervalSeconds { get; private set; }
            public ConfigEntry<float> CleanDeduct { get; private set; }
            public ConfigEntry<bool> ToleranceEnabled { get; private set; }
            public ConfigEntry<float> ToleranceIntervalSeconds { get; private set; }
            public ConfigEntry<float> ToleranceReduce { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "M", "启用职业", true, "是否启用 M 职业。");
                MaxPlayers = BindL(config, "M", "最大人数", 1,
                    "M 最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "M", "最小人数", 0,
                    "M 最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "M", "抽取权重", 10, "随机分配时 M 被抽中的权重。");
                SufferMultiplier = BindL(config, "M", "享受-倍率", 1f,
                    "享受：M 每受到 1 点下列负面状态时获得的额外体力倍率（默认 1 = 等量）。\n" +
                    "覆盖的负面状态：损伤、中毒、孢子、荆棘、烧伤、冻伤、蛛网、箭、捕蝇草。\n" +
                    "例如 1 = 受到 5 点损伤则获得 5 点额外体力；2 = 获得 10 点；设为 0 关闭该效果。");

                CleanIntervalSeconds = BindL(config, "M", "享受-检测间隔", 60f,
                    "享受：每隔多少秒检测一次自身状态（秒）。默认 60。\n" +
                    "若检测时**自身没有任何**下列特定负面状态，则扣除【享受-无状态扣除】的额外体力。\n" +
                    "特定负面状态：损伤、中毒、孢子、荆棘、烧伤、冻伤、蛛网、箭、捕蝇草。设为 0 关闭该效果。");
                CleanDeduct = BindL(config, "M", "享受-无状态扣除", 0.10f,
                    "享受：上面那次检测「无任何特定负面状态」时扣除的额外体力（0.10 = 10 点）。默认 10 点。\n" +
                    "额外体力下限为 0，不会扣成负数。设为 0 关闭该效果。");

                ToleranceEnabled = BindL(config, "M", "耐受-启用", true,
                    "耐受：是否启用【耐受】效果。默认 true。");
                ToleranceIntervalSeconds = BindL(config, "M", "耐受-间隔", 10f,
                    "耐受：每隔多少秒削减一次下列特定负面状态（秒）。默认 10。");
                ToleranceReduce = BindL(config, "M", "耐受-削减量", 0.05f,
                    "耐受：每次削减的特定负面状态量（0.05 = 5 点）。默认 5 点。设为 0 关闭。\n" +
                    "覆盖：损伤、中毒、孢子、荆棘、烧伤、冻伤。\n" +
                    "注：荆棘属原版「状态数组通道」硬性排除项，仅当 M 就是主机本人时才会真正被削减。");
            }
        }

        public class SavageConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> CleanseIntervalSeconds { get; private set; }
            public ConfigEntry<float> CleansePoison { get; private set; }
            public ConfigEntry<float> CleanseSpores { get; private set; }
            public ConfigEntry<float> EatDamage { get; private set; }
            public ConfigEntry<bool> CookedNaturalFoodTriggers { get; private set; }
            public ConfigEntry<string> EatBlacklist { get; private set; }
            public ConfigEntry<float> BananaIntervalSeconds { get; private set; }
            public ConfigEntry<string> BananaItem { get; private set; }
            public ConfigEntry<int> BananaCount { get; private set; }
            public ConfigEntry<string> PickupTargetItem { get; private set; }
            public ConfigEntry<string> PickupSourceItems { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "野人", "启用职业", true, "是否启用野人职业。");
                MaxPlayers = BindL(config, "野人", "最大人数", 1,
                    "野人最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "野人", "最小人数", 0,
                    "野人最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "野人", "抽取权重", 10, "随机分配时野人被抽中的权重。");

                CleanseIntervalSeconds = BindL(config, "野人", "自然韧性-触发间隔", 5f,
                    "自然韧性：每隔多少秒为野人清除一次中毒与孢子（秒）。默认 5。");
                CleansePoison = BindL(config, "野人", "自然韧性-中毒量", 0.025f,
                    "自然韧性：每次清除的中毒值（0.025 = 2.5 点，为引擎最小有效步长）。设为 0 关闭。");
                CleanseSpores = BindL(config, "野人", "自然韧性-孢子量", 0.025f,
                    "自然韧性：每次清除的孢子值（0.025 = 2.5 点）。设为 0 关闭。");

                EatDamage = BindL(config, "野人", "野性未泯-损伤", 0.05f,
                    "野性未泯：野人食用「包装食物」或「烤过的食物」时增加的损伤（0.05 = 5 点）。\n" +
                    "判定依据游戏物品标签：包装食物 = PackagedFood；烤过的食物 = CookedAmount ≥ 1。\n" +
                    "自然食物（浆果、蘑菇、生的食材等）在不烤熟时不触发；设为 0 关闭该效果。\n" +
                    "（诡异菇、能量饮料、奶白金、大棒棒糖除外）");
                CookedNaturalFoodTriggers = BindL(config, "野人", "野性未泯-烤熟自然食物触发", true,
                    "野性未泯：自然食物（浆果、蘑菇等）被烤熟后是否也触发扣血。\n" +
                    "开启（默认）：只要物品被烤过（CookedAmount ≥ 1）就触发，符合「烤过的食物」的原意。\n" +
                    "关闭：仅原版「美食家」类食物（带 GourmandRequirement 标签）烤熟后才触发，自然食物烤熟不触发（旧行为）。");
                EatBlacklist = BindL(config, "野人", "野性未泯-黑名单",
                    "Mushroom Glow,Energy Drink,FortifiedMilk,Lollipop",
                    "野性未泯：食用这些物品时**不触发**扣血（预制体名，英文逗号分隔）。\n" +
                    "默认：诡异菇 Mushroom Glow、能量饮料 Energy Drink、奶白金 FortifiedMilk、大棒棒糖 Lollipop。\n" +
                    "留空表示不设黑名单（此时仅「自然食物」等标签判定生效）。");

                BananaIntervalSeconds = BindL(config, "野人", "不拿拿-触发间隔", 60f,
                    "不拿拿：每隔多少秒为野人发放一次粉香蕉（秒）。默认 60。");
                BananaItem = BindL(config, "野人", "不拿拿-物品", "Berrynana Pink",
                    "不拿拿：发放的物品预制体名。默认 Berrynana Pink = 粉香蕉。填错或留空则不发。");
                BananaCount = BindL(config, "野人", "不拿拿-数量", 1,
                    "不拿拿：每次发放的数量。优先放入物品栏空位；物品栏已满时在脚下生成。设为 0 关闭。");

                PickupTargetItem = BindL(config, "野人", "知识污染-目标物品", "FireWood",
                    "知识污染：命中的来源物品会被直接替换成该物品（填预制体名）。默认 FireWood = 木棍。");
                PickupSourceItems = BindL(config, "野人", "知识污染-来源物品",
                    "ClimbingSpike,RopeShooter,ChainShooter,RescueHook,AntiZooka",
                    "知识污染：会被替换成「目标物品」的来源物品预制体名，英文逗号分隔。\n" +
                    "默认：岩钉 ClimbingSpike、绳索炮 RopeShooter、锁链发射器 ChainShooter、救援抓钩 RescueHook、反重力炮 AntiZooka。");
            }
        }

        public class ScoutConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 行李 ----
            public ConfigEntry<bool> LuggageEnabled { get; private set; }
            public ConfigEntry<string> LuggageBackpack { get; private set; }
            public ConfigEntry<int> LuggageItemCount { get; private set; }
            public ConfigEntry<string> LuggageItemPool { get; private set; }

            // ---- 集体（三档） ----
            public ConfigEntry<float> GroupRadius { get; private set; }
            public ConfigEntry<float> SoloIntervalSeconds { get; private set; }
            public ConfigEntry<float> SoloInjury { get; private set; }
            public ConfigEntry<float> SoloHunger { get; private set; }
            public ConfigEntry<float> PeerInjuryIntervalSeconds { get; private set; }
            public ConfigEntry<float> PeerInjuryReduce { get; private set; }
            public ConfigEntry<float> PeerHungerIntervalSeconds { get; private set; }
            public ConfigEntry<float> PeerHungerReduce { get; private set; }
            public ConfigEntry<float> AllyInjuryIntervalSeconds { get; private set; }
            public ConfigEntry<float> AllyInjuryReduce { get; private set; }
            public ConfigEntry<float> AllyHungerIntervalSeconds { get; private set; }
            public ConfigEntry<float> AllyHungerReduce { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "童军", "启用职业", true,
                    "是否启用童军职业。默认 true。\n" +
                    "童军【行李】开局自带「滑稽背包（内含 2 个随机物品）」，单人时【集体】为纯惩罚、附近有其他玩家或童军/领袖时才转为收益。");
                MaxPlayers = BindL(config, "童军", "最大人数", 4,
                    "童军最多分配给几名玩家。设为 0 表示不限制。默认 4。\n" +
                    "注意：第 2/3 档增益需要半径内有其他玩家、或其他童军/领袖。");
                MinPlayers = BindL(config, "童军", "最小人数", 0,
                    "童军最少分配给几名玩家。人数不足时会优先补齐该职业。默认 0（不强制）。");
                Weight = BindL(config, "童军", "抽取权重", 5, "随机分配时童军被抽中的权重。");

                // ---------------- 【行李】 ----------------
                LuggageEnabled = BindL(config, "童军", "行李-启用", true,
                    "【行李】是否启用。默认 true。");
                LuggageBackpack = BindL(config, "童军", "行李-背包", "Fannypack",
                    "【行李】开局为童军直接装备的背包预制体名。默认 Fannypack = 滑稽背包。\n" +
                    "填错或留空则退化为默认的 Backpack。");
                LuggageItemCount = BindL(config, "童军", "行李-物品数", 2,
                    "【行李】背包内放入的随机物品个数（**不重复**）。默认 2。设 0 则只给空背包。");
                LuggageItemPool = BindL(config, "童军", "行李-物品池",
                    "ScoutCookies,Lollipop,Airplane Food,Glizzy,Marshmallow,TrailMix,Granola Bar," +
                    "Winterberry Yellow,Apple Berry Red,Berrynana Yellow,NestEgg_Raven,Energy Drink," +
                    "Sports Drink,RopeSpool,FortifiedMilk,Bandages,FirstAidKit,Balloon,BalloonBunch," +
                    "Glider,Parachute,Parasol,Flare,Lantern,Pirate Compass,Binoculars,Basketball,Compass,Frisbee," +
                    "Torch,Stone,Passport,BingBong",
                    "【行李】背包内随机物品的候选池（预制体名，英文逗号分隔，共 33 项）。\n" +
                    "对应：童军饼干 ScoutCookies / 大棒棒糖 Lollipop / 飞机餐 Airplane Food / 热狗肠 Glizzy / " +
                    "棉花糖 Marshmallow / 混合坚果 TrailMix / 燕麦棒 Granola Bar / 黄雪莓 Winterberry Yellow / " +
                    "红脆莓 Apple Berry Red / 黄莓蕉 Berrynana Yellow / 小小蛋 NestEgg_Raven / 能量饮料 Energy Drink / " +
                    "运动饮料 Sports Drink / 绳索 RopeSpool / 奶白金 FortifiedMilk / 绷带 Bandages / 急救箱 FirstAidKit / " +
                    "气球 Balloon / 一束气球 BalloonBunch / 滑翔伞 Glider / 自动降落伞 Parachute / 太阳伞 Parasol / " +
                    "照明棒 Flare / 提灯 Lantern / 海盗罗盘 Pirate Compass / 望远镜 Binoculars / 篮球 Basketball / 罗盘 Compass / " +
                    "飞盘 Frisbee / 火炬 Torch / 石头 Stone / 护照 Passport / 宾邦 BingBong。\n" +
                    "⚠ 「滑翔伞 / 照明棒」两项的预制体名未能从现有反编译资料确证，暂用 Glider / Flare；\n" +
                    "若实测发不出来（日志会提示该名字未找到物品），请把这两项换成真实预制体名。");

                // ---------------- 【集体】 ----------------
                GroupRadius = BindL(config, "童军", "集体-半径", 15f,
                    "【集体】判定「附近有其他玩家 / 童军或领袖」的半径（米）。默认 15。");

                SoloIntervalSeconds = BindL(config, "童军", "集体-独自间隔", 20f,
                    "【集体·独自】半径内**没有其他玩家**时，每隔多少秒为童军叠加一次损伤与饥饿（秒）。默认 20。");
                SoloInjury = BindL(config, "童军", "集体-独自损伤", 0.025f,
                    "【集体·独自】每次叠加的损伤（0.025 = 2.5 点）。设为 0 关闭该项。");
                SoloHunger = BindL(config, "童军", "集体-独自饥饿", 0.025f,
                    "【集体·独自】每次叠加的饥饿（0.025 = 2.5 点）。设为 0 关闭该项。");

                PeerInjuryIntervalSeconds = BindL(config, "童军", "集体-同伴损伤间隔", 20f,
                    "【集体·同伴】半径内有其他玩家（但**没有**童军或领袖）时，每隔多少秒减少一次损伤（秒）。默认 20。");
                PeerInjuryReduce = BindL(config, "童军", "集体-同伴减损伤", 0.025f,
                    "【集体·同伴】每次减少的损伤（0.025 = 2.5 点）。设为 0 关闭该项。");
                PeerHungerIntervalSeconds = BindL(config, "童军", "集体-同伴饥饿间隔", 50f,
                    "【集体·同伴】每隔多少秒减少一次饥饿（秒）。默认 50。");
                PeerHungerReduce = BindL(config, "童军", "集体-同伴减饥饿", 0.025f,
                    "【集体·同伴】每次减少的饥饿（0.025 = 2.5 点）。设为 0 关闭该项。");

                AllyInjuryIntervalSeconds = BindL(config, "童军", "集体-童军损伤间隔", 15f,
                    "【集体·童军/领袖】半径内有其他童军或领袖时，每隔多少秒减少一次损伤（秒）。默认 15。");
                AllyInjuryReduce = BindL(config, "童军", "集体-童军减损伤", 0.025f,
                    "【集体·童军/领袖】每次减少的损伤（0.025 = 2.5 点）。设为 0 关闭该项。");
                AllyHungerIntervalSeconds = BindL(config, "童军", "集体-童军饥饿间隔", 30f,
                    "【集体·童军/领袖】每隔多少秒减少一次饥饿（秒）。默认 30。");
                AllyHungerReduce = BindL(config, "童军", "集体-童军减饥饿", 0.025f,
                    "【集体·童军/领袖】每次减少的饥饿（0.025 = 2.5 点）。设为 0 关闭该项。");
            }
        }

        public class StandardBearerConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<int> FlagCount { get; private set; }
            public ConfigEntry<float> EffectDelaySeconds { get; private set; }
            public ConfigEntry<int> EffectCountMin { get; private set; }
            public ConfigEntry<int> EffectCountMax { get; private set; }
            public ConfigEntry<int> StaminaWeight { get; private set; }
            public ConfigEntry<int> SprintWeight { get; private set; }
            public ConfigEntry<int> InvincibleWeight { get; private set; }
            public ConfigEntry<float> SprintDrowsyClearAmount { get; private set; }
            public ConfigEntry<float> StaminaDrowsyClearAmount { get; private set; }
            public ConfigEntry<float> CleanseRadius { get; private set; }
            public ConfigEntry<bool> CleanseIncludeSelf { get; private set; }
            public ConfigEntry<float> CleanseSelfPetrify { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "旗手", "启用职业", true, "是否启用旗手职业。");
                MaxPlayers = BindL(config, "旗手", "最大人数", 1,
                    "旗手最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "旗手", "最小人数", 0,
                    "旗手最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "旗手", "抽取权重", 10, "随机分配时旗手被抽中的权重。");
                FlagCount = BindL(config, "旗手", "执旗-每层旗标数", 1,
                    "执旗：每次进入新关卡获得的检查点旗标 Flag_Plantable_Checkpoint 数量。\n" +
                    "优先放入该玩家 3 个物品栏的空位；物品栏已满时在该玩家脚下生成，落地后可拾取。设为 0 关闭。");
                EffectDelaySeconds = BindL(config, "旗手", "执旗-触发延迟", 3f,
                    "执旗：插旗后延迟多少秒才施加效果（秒）。默认 3。");
                EffectCountMin = BindL(config, "旗手", "执旗-效果数量下限", 1,
                    "执旗：每次插旗，每名存活玩家至少获得的随机效果数量（1~3）。默认 1。");
                EffectCountMax = BindL(config, "旗手", "执旗-效果数量上限", 3,
                    "执旗：每次插旗，每名存活玩家至多获得的随机效果数量（1~3）。默认 3。\n" +
                    "三种效果（无限体力 / 加速 / 无敌）可同时叠加。下限或上限为 0 时会被强制夹到 1~3 区间。");
                StaminaWeight = BindL(config, "旗手", "执旗-无限体力权重", 1,
                    "执旗：随机效果「无限体力（大棒棒糖 Lollipop）」的权重。设为 0 表示该效果不参与抽取。\n" +
                    "注意：大棒棒糖的无限体力结束后会通过原版 Affliction_AdjustDrowsyOverTime「持续」叠加困倦" +
                    "（实测约 32.5 点），该困倦在客户端会因「持续叠加不断刷新 LastAddedStatus」而无法自然消退，\n" +
                    "故由主机在本效果结束后代为清除（见「执旗-无限体力-清除困倦」）。");
                SprintWeight = BindL(config, "旗手", "执旗-加速权重", 1,
                    "执旗：随机效果「加速（能量饮料）」的权重。设为 0 表示该效果不参与抽取。");
                InvincibleWeight = BindL(config, "旗手", "执旗-无敌权重", 1,
                    "执旗：随机效果「无敌（奶白金）」的权重。设为 0 表示该效果不参与抽取。");
                SprintDrowsyClearAmount = BindL(config, "旗手", "执旗-加速-清除困倦", 0.25f,
                    "执旗：能量饮料的加速效果结束、玩家被附加困倦后，主机下发抵消的困倦量（0.25 = 25 点）。\n" +
                    "与先驱「高代谢-加速后清除困倦」同理：加速时长由能量饮料自身决定（约 10 秒），主机无法改写。\n" +
                    "设为 0 关闭清除。");
                StaminaDrowsyClearAmount = BindL(config, "旗手", "执旗-无限体力-清除困倦", 0.35f,
                    "执旗：大棒棒糖的无限体力结束、且其后续「持续叠加困倦」的 affliction 也结束后，" +
                    "主机下发抵消的困倦量（0.35 = 35 点，覆盖实测的约 32.5 点）。\n" +
                    "原理：该「持续叠加」在客户端每帧调整困倦并刷新 LastAddedStatus(Drowsy)，" +
                    "使困倦永远不满足自然消退条件（表现为「困倦值保持不变」）；" +
                    "且它是在无限体力结束之后才开始叠加的，故必须在它结束后再清除。\n" +
                    "主机通过观察该 affliction（DrowsyOverTime）由「有」变「无」来确定清除时机。设为 0 关闭清除。");
                CleanseRadius = BindL(config, "旗手", "执旗-净化半径", 15f,
                    "执旗：插旗时「立即」清除半径内所有玩家负面状态的半径（米）。默认 15。\n" +
                    "清除的负面状态为「损伤 / 饥饿 / 寒冷 / 中毒 / 困倦 / 过热 / 孢子 / 蛛网 / 捕蝇草」，\n" +
                    "**不包括诅咒与石化**。设为 0 关闭净化。\n" +
                    "（螃蟹 / 负重 / 荆棘 / 箭 与原版一致地不可清除，属游戏原版「不可治愈」判定。）");
                CleanseIncludeSelf = BindL(config, "旗手", "执旗-净化含自身", true,
                    "执旗：净化是否也作用于插旗者自己。默认 true（含自身）。");
                CleanseSelfPetrify = BindL(config, "旗手", "执旗-插旗石化", 0.20f,
                    "执旗：插旗净化时，为旗手「自身」额外施加的石化值（0.20 = 20 点）。默认 0.20。\n" +
                    "该石化是净化效果的代价，不会被同一轮净化清除（净化本就排除诅咒与石化）。设为 0 关闭。");
            }
        }

        public class DancerConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> YouthRadius { get; private set; }
            public ConfigEntry<float> YouthStamina { get; private set; }
            public ConfigEntry<float> YouthCooldownSeconds { get; private set; }
            public ConfigEntry<float> CheerRadius { get; private set; }
            public ConfigEntry<float> FrenzyIntervalSeconds { get; private set; }
            public ConfigEntry<string> DanceEmoteNames { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "舞王", "启用职业", true, "是否启用舞王职业。");
                MaxPlayers = BindL(config, "舞王", "最大人数", 1,
                    "舞王最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "舞王", "最小人数", 0,
                    "舞王最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "舞王", "抽取权重", 10, "随机分配时舞王被抽中的权重。");

                YouthRadius = BindL(config, "舞王", "舞动青春-半径", 15f,
                    "舞动青春：判定「玩家在舞王多近范围内跳舞」的半径（米）。默认 15。");
                YouthStamina = BindL(config, "舞王", "舞动青春-额外体力", 0.025f,
                    "舞动青春：半径内有 1 名玩家跳舞时，为舞王与该跳舞玩家各增加的额外体力（0.025 = 2.5 点）。\n" +
                    "设为 0 关闭该效果。");
                YouthCooldownSeconds = BindL(config, "舞王", "舞动青春-检测冷却", 1f,
                    "舞动青春：检测到一次跳舞后，该玩家在多少秒内的再次跳舞不再触发本次加成（秒）。默认 1。");
                CheerRadius = BindL(config, "舞王", "全场欢呼-半径", 15f,
                    "全场欢呼：舞王自己跳舞时，强制其该半径（米）内的所有其他存活玩家一起跳舞。默认 15。");
                FrenzyIntervalSeconds = BindL(config, "舞王", "狂热舞者-间隔", 120f,
                    "狂热舞者：每隔多少秒强制舞王自己后空翻一次（秒）。默认 120。\n" +
                    "后空翻为原版表情 A_Scout_Emote_BackFlip，翻完后会倒地约 1 秒；\n" +
                    "该动作不是「跳舞」，不会触发「舞动青春」与「全场欢呼」。设为 0 或负数关闭。");
                DanceEmoteNames = BindL(config, "舞王", "跳舞动作名", "A_Scout_Emote_Dance1,A_Scout_Emote_Dance",
                    "跳舞动作的动画名（可英文逗号分隔填多个）；主机「强制跳舞」时使用列表的第一项。\n" +
                    "默认 A_Scout_Emote_Dance1（实测有效）；此外只要表情名里含 dance（不区分大小写）也会被当作跳舞，\n" +
                    "因此多数情况下无需修改。若强制跳舞看不到动作：在游戏里手动做一次跳舞表情，" +
                    "然后到日志里搜索「舞王·观测到表情」找到实际动画名，再填到此处（放第一位）。");
            }
        }

        public class DaihaoConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<bool> AvatarEnabled { get; private set; }
            public ConfigEntry<int> AvatarSkin { get; private set; }
            public ConfigEntry<int> AvatarEyes { get; private set; }
            public ConfigEntry<int> AvatarMouth { get; private set; }
            public ConfigEntry<int> AvatarAccessory { get; private set; }
            public ConfigEntry<int> AvatarOutfit { get; private set; }
            public ConfigEntry<int> AvatarHat { get; private set; }
            public ConfigEntry<int> AvatarSash { get; private set; }
            public ConfigEntry<int> AvatarMedal { get; private set; }
            public ConfigEntry<float> AvatarMonitorSeconds { get; private set; }
            public ConfigEntry<float> VoiceIntervalSeconds { get; private set; }
            public ConfigEntry<float> VoiceStamina { get; private set; }
            public ConfigEntry<float> SlipIntervalSeconds { get; private set; }
            public ConfigEntry<string> ProductionItem { get; private set; }
            public ConfigEntry<float> ProductionEggIntervalSeconds { get; private set; }
            public ConfigEntry<float> ProductionStaminaIntervalSeconds { get; private set; }
            public ConfigEntry<float> ProductionStamina { get; private set; }
            public ConfigEntry<bool> MemeEnabled { get; private set; }
            public ConfigEntry<string> MemeItem { get; private set; }
            public ConfigEntry<float> AllSameRadius { get; private set; }
            public ConfigEntry<float> AllSameIntervalSeconds { get; private set; }
            public ConfigEntry<float> AllSameInjuryReduce { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "待皓", "启用职业", true, "是否启用待皓职业。");
                MaxPlayers = BindL(config, "待皓", "最大人数", 20,
                    "待皓最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "待皓", "最小人数", 0,
                    "待皓最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "待皓", "抽取权重", 3, "随机分配时待皓被抽中的权重。");

                AvatarEnabled = BindL(config, "待皓", "化身-启用", true,
                    "化身：本局内是否把待皓玩家的装扮替换为下方指定的款式。false=完全不改装扮。\n" +
                    "仅当玩家职业为待皓时套用；玩家不再是待皓（如转职离开）时立即还原其原本装扮。");
                AvatarSkin = BindL(config, "待皓", "化身-皮肤", 0,
                    "化身：皮肤颜色编号。默认 0（待皓套装）。-1 = 保持该玩家原本的皮肤。");
                AvatarEyes = BindL(config, "待皓", "化身-眼睛", 3,
                    "化身：眼睛编号。默认 3（待皓套装）。-1 = 保持该玩家原本的眼睛。");
                AvatarMouth = BindL(config, "待皓", "化身-嘴巴", 6,
                    "化身：嘴巴编号。默认 6（待皓套装）。-1 = 保持该玩家原本的嘴巴。");
                AvatarAccessory = BindL(config, "待皓", "化身-饰品", 8,
                    "化身：饰品编号。默认 8（待皓套装）。-1 = 保持该玩家原本的饰品。");
                AvatarOutfit = BindL(config, "待皓", "化身-服装", 25,
                    "化身：服装编号。默认 25（待皓套装）。-1 = 保持该玩家原本的服装。");
                AvatarHat = BindL(config, "待皓", "化身-帽子", 25,
                    "化身：帽子编号。默认 25（待皓套装）。-1 = 保持该玩家原本的帽子。");
                AvatarSash = BindL(config, "待皓", "化身-腰带", -1,
                    "化身：腰带编号。默认 -1 = 保持该玩家原本的腰带。");
                AvatarMedal = BindL(config, "待皓", "化身-奖章", -1,
                    "化身：奖章编号。默认 -1 = 保持该玩家原本的奖章。");
                AvatarMonitorSeconds = BindL(config, "待皓", "化身-监控时长", 15f,
                    "化身：套用后继续「每秒比对、被覆盖则重发」的自愈监控时长（秒）。默认 15。\n" +
                    "目标玩家自己的客户端会在角色实例化后异步读取 Steam 装扮并再广播一份真实装扮，\n" +
                    "若晚于本效果到达会把化身覆盖回去（表现为「开局不生效、转职出去再转回才生效」）。\n" +
                    "故在玩家被分配为待皓后的一段时间内每秒检测一次并自愈重发；窗口结束后不再检测。\n" +
                    "设为 0 或负数 = 关闭自愈（只套用一次）。");

                VoiceIntervalSeconds = BindL(config, "待皓", "喂喂喂-检测间隔", 60f,
                    "喂喂喂，听得见吗：每隔多少秒检测一次待皓玩家的语音状态（秒）。默认 60。\n" +
                    "检测依据游戏内「真实出声」判定（带噪音门限，仅开麦不出声不算），\n" +
                    "仅在该整点瞬间确实处于讲话状态时才加额外体力。设为 0 或负数关闭。");
                VoiceStamina = BindL(config, "待皓", "喂喂喂-额外体力", 0.1f,
                    "喂喂喂，听得见吗：检测到正在讲话时，为该待皓玩家增加的额外体力（0.1 = 10 点）。\n" +
                    "设为 0 关闭该效果。");
                SlipIntervalSeconds = BindL(config, "待皓", "脚滑-触发间隔", 150f,
                    "脚滑：每隔多少秒强制待皓玩家后空翻一次（秒）。默认 150。\n" +
                    "后空翻为原版表情 A_Scout_Emote_BackFlip，翻完后必定摔倒（倒地约 1 秒）。\n" +
                    "设为 0 或负数关闭。");

                ProductionItem = BindL(config, "待皓", "生产-物品", "NestEgg",
                    "生产：定期发放的物品预制体名。默认 NestEgg（好大蛋，砸开后产出煎蛋 Egg）。");
                ProductionEggIntervalSeconds = BindL(config, "待皓", "生产-发放间隔", 120f,
                    "生产：每隔多少秒为每名待皓玩家发放 1 个「生产-物品」（秒）。默认 120。\n" +
                    "发放优先放入 3 个物品栏空位；三栏全满时在该玩家脚下生成（落地后可拾取）。\n" +
                    "设为 0 或负数关闭发放。");
                ProductionStaminaIntervalSeconds = BindL(config, "待皓", "生产-体力间隔", 60f,
                    "生产：当待皓玩家的 3 个物品栏中任意一格存在「生产-物品」时，" +
                    "每隔多少秒为其增加一次额外体力（秒）。默认 60。设为 0 或负数关闭。");
                ProductionStamina = BindL(config, "待皓", "生产-额外体力", 0.05f,
                    "生产：每次检测到物品栏中有「生产-物品」时增加的额外体力（0.05 = 5 点）。设为 0 关闭。");

                MemeEnabled = BindL(config, "待皓", "模因污染-启用", true,
                    "模因污染：本局存在待皓职业玩家时，其他玩家食用「模因污染-触发物品」会直接转职成待皓。\n" +
                    "默认 true。若要转职，该玩家当前不能已经是待皓。\n" +
                    "注意：该转职会绕过「最大人数」限制（否则待皓已达上限时本效果永远无法触发）。");
                MemeItem = BindL(config, "待皓", "模因污染-触发物品", "Egg",
                    "模因污染：触发转职的物品预制体名（英文逗号分隔，可填多个）。\n" +
                    "默认 Egg（煎蛋，由好大蛋 NestEgg 砸开后产出）。\n" +
                    "注意：好大蛋 NestEgg 本身是可砸开的蛋，不经过食用通道，" +
                    "因此触发物只能是它砸出的煎蛋 Egg。");

                AllSameRadius = BindL(config, "待皓", "我们全都是-半径", 50f,
                    "我们全都是：统计半径（米）。默认 50。");
                AllSameIntervalSeconds = BindL(config, "待皓", "我们全都是-触发间隔", 30f,
                    "我们全都是：每隔多少秒结算一次（秒）。默认 30。设为 0 或负数关闭该效果。");
                AllSameInjuryReduce = BindL(config, "待皓", "我们全都是-减损伤", 0.025f,
                    "我们全都是：半径内除自己外每有 1 名其他待皓，本次结算削减（治疗）的损伤（0.025 = 2.5 点）。\n" +
                    "默认 0.025，即每 30 秒按「其他待皓数量 × 2.5 点」治疗自己。设为 0 关闭该效果。");
            }
        }

        public class SaintConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }
            public ConfigEntry<float> TeachIntervalSeconds { get; private set; }
            public ConfigEntry<float> TeachRadius { get; private set; }
            public ConfigEntry<float> TeachStamina { get; private set; }
            public ConfigEntry<float> TeachDrowsy { get; private set; }
            public ConfigEntry<float> PerfectionIntervalSeconds { get; private set; }
            public ConfigEntry<float> PerfectionStamina { get; private set; }
            public ConfigEntry<float> PerfectionPickupDisappearChance { get; private set; }
            public ConfigEntry<string> PerfectionPickupBlacklist { get; private set; }
            public ConfigEntry<float> FastingHunger { get; private set; }
            public ConfigEntry<bool> FastingInvincible { get; private set; }
            public ConfigEntry<bool> TranscendentEnabled { get; private set; }
            public ConfigEntry<float> FallIntervalSeconds { get; private set; }
            public ConfigEntry<float> FallInjury { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "圣人", "启用职业", true, "是否启用圣人职业。");
                MaxPlayers = BindL(config, "圣人", "最大人数", 1,
                    "圣人最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "圣人", "最小人数", 0,
                    "圣人最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "圣人", "抽取权重", 10, "随机分配时圣人被抽中的权重。");

                TeachIntervalSeconds = BindL(config, "圣人", "教诲-触发间隔", 10f,
                    "教诲：每隔多少秒对半径内的其他玩家施加一次加成（秒）。默认 10。");
                TeachRadius = BindL(config, "圣人", "教诲-半径", 10f,
                    "教诲：作用半径（米）。默认 10。范围内的其他玩家会被加成（不含自己）。");
                TeachStamina = BindL(config, "圣人", "教诲-额外体力", 0.025f,
                    "教诲：每次为半径内每名其他玩家增加的额外体力（0.025 = 2.5 点）。设为 0 关闭该项。");
                TeachDrowsy = BindL(config, "圣人", "教诲-困倦", 0.10f,
                    "教诲：每次为半径内每名其他玩家附加的困倦（0.10 = 10 点）。设为 0 关闭该项。\n" +
                    "注意：状态以 0.025 为最小量子，低于该值的增量不会生效。");

                PerfectionIntervalSeconds = BindL(config, "圣人", "完美主义-触发间隔", 60f,
                    "完美主义：每隔多少秒为圣人自己增加一次额外体力（秒）。默认 60。");
                PerfectionStamina = BindL(config, "圣人", "完美主义-额外体力", 1f,
                    "完美主义：每次为圣人自己增加的额外体力（1 = 100 点）。设为 0 关闭该项。");
                PerfectionPickupDisappearChance = BindL(config, "圣人", "完美主义-拾取消失概率", 1f,
                    "完美主义：圣人从世界里拾取物品时，该物品直接消失的概率（1 = 100%）。\n" +
                    "命中「完美主义-拾取黑名单」的物品不受影响（保持原样入包）。仅主机安装即生效，客机零安装。");
                PerfectionPickupBlacklist = BindL(config, "圣人", "完美主义-拾取黑名单",
                    "Mushroom Glow,Flare,Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor,Strange Gem",
                    "完美主义：拾取时「不受完美主义影响（不会消失）」的物品黑名单（预制体名，英文逗号分隔）。\n" +
                    "默认排除：诡异菇 Mushroom Glow、信号棒 Flare、" +
                    "童军的毅力 Amulet_Healing、童军的进取 Amulet_SuperJump、童军的盛情 Amulet_Clone、" +
                    "童军的野心 Amulet_InfiniteStamina、童军的荣耀 ScoutsHonor。\n" +
                    "名称中含空格的要保留原样。留空表示不设黑名单。");

                FastingHunger = BindL(config, "圣人", "辟谷养生-饥饿", 1f,
                    "辟谷养生：每次进入新关卡时为圣人清除的饥饿值（1 = 100 点，即清空饥饿）。\n" +
                    "该值为正数，表示要「减去」的饥饿量；设为 0 关闭该项。");
                FastingInvincible = BindL(config, "圣人", "辟谷养生-无敌", true,
                    "辟谷养生：每次进入新关卡时，除清除饥饿外，是否额外为圣人施加一次无敌效果（奶白金 FortifiedMilk）。\n" +
                    "默认 true。仅当「辟谷养生-饥饿」> 0 时才会结算。");
                TranscendentEnabled = BindL(config, "圣人", "超然物外-启用", true,
                    "超然物外：圣人是否免疫下列由其他职业施加的负面效果。默认 true。\n" +
                    "免疫范围：① 潘多拉之子·潘多拉（在潘多拉作用范围内不会被施加潘多拉魔盒）；\n" +
                    "② 死灵法师·亡灵·光环（作为人类玩家时不会因光环获得损伤，骷髅形态的体力加成不受影响）；\n" +
                    "③ 舞王·全场欢呼（舞王跳舞时不会被强制跟跳）。");
                FallIntervalSeconds = BindL(config, "圣人", "跌落神坛-检测间隔", 1f,
                    "跌落神坛：每隔多少秒检测一次圣人自身是否受到损伤（秒）。默认 1。");
                FallInjury = BindL(config, "圣人", "跌落神坛-损伤", 1f,
                    "跌落神坛：检测到圣人自身在本次间隔内损伤值增加时，额外追加的损伤（1 = 100 点）。\n" +
                    "该效果极为致命，且会与自身追加的损伤互相作用；设为 0 关闭。");
            }
        }

        /// <summary>
        /// 法师（Mage）：魔法卷轴 / 黑魔法。
        ///
        /// 【魔法卷轴】每「卷轴-发放间隔」秒获得 1 个卷轴（优先放入物品栏空位，全满则脚下生成）。
        ///   · 人类形态：每次打开卷轴立即使自身 +「卷轴-人类石化」石化，并在「卷轴-延迟」秒后
        ///     从 9 种人类效果中按权重随机触发 1 种；
        ///   · 骷髅形态（黑魔法）：每次打开卷轴立即使自身 +「卷轴-骷髅诅咒」诅咒，
        ///     随后从 8 种骷髅效果中按权重逐次触发：首次立即结算，之后每次按「卷轴-连锁概率」（默认 30%）
        ///     判定是否继续、并以「卷轴-连锁间隔」（默认 1 秒）为间隔依次触发，已触发过的效果本串内不重复。
        ///
        /// 全部 17 种效果各有独立权重（默认均为 10）；权重为 0 的效果不会被抽中。
        /// </summary>
        public class MageConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 卷轴本体 ----
            public ConfigEntry<float> ScrollIntervalSeconds { get; private set; }
            public ConfigEntry<string> ScrollItem { get; private set; }
            public ConfigEntry<float> HumanDelaySeconds { get; private set; }
            public ConfigEntry<float> HumanPetrify { get; private set; }
            public ConfigEntry<float> SkeletonCurse { get; private set; }
            public ConfigEntry<float> ChainChance { get; private set; }
            public ConfigEntry<float> ChainIntervalSeconds { get; private set; }

            // ---- 人类效果数值 ----
            public ConfigEntry<float> BuffRadius { get; private set; }
            public ConfigEntry<float> StaminaRadius { get; private set; }
            public ConfigEntry<float> InvincibleRadius { get; private set; }
            public ConfigEntry<float> EnlightenRadius { get; private set; }
            public ConfigEntry<float> EnlightenStamina { get; private set; }
            public ConfigEntry<float> CleanseRadius { get; private set; }
            public ConfigEntry<float> CleanseDuration { get; private set; }
            public ConfigEntry<float> CleansePerSecond { get; private set; }
            public ConfigEntry<float> HealRadius { get; private set; }
            public ConfigEntry<float> HealDuration { get; private set; }
            public ConfigEntry<float> HealPerSecond { get; private set; }
            public ConfigEntry<float> AntiGravRadius { get; private set; }
            public ConfigEntry<float> AntiGravDuration { get; private set; }
            public ConfigEntry<float> AntiGravInterval { get; private set; }
            public ConfigEntry<string> AntiGravPrefab { get; private set; }
            public ConfigEntry<float> TeleportRadius { get; private set; }
            public ConfigEntry<float> TeleportMinDistance { get; private set; }
            public ConfigEntry<float> ReviveDistance { get; private set; }

            // ---- 骷髅（黑魔法）效果数值 ----
            public ConfigEntry<float> BloodRadius { get; private set; }
            public ConfigEntry<float> BloodInjury { get; private set; }
            public ConfigEntry<float> BloodStamina { get; private set; }
            public ConfigEntry<float> BloodSelfCursePerTarget { get; private set; }
            public ConfigEntry<float> BloodSelfStaminaPerTarget { get; private set; }
            public ConfigEntry<float> SacrificeRadius { get; private set; }
            public ConfigEntry<string> SacrificeItem { get; private set; }
            public ConfigEntry<float> SacrificeSelfCurse { get; private set; }
            public ConfigEntry<float> SwapMinDistance { get; private set; }
            public ConfigEntry<float> DisasterLifetime { get; private set; }
            public ConfigEntry<float> PlagueRadius { get; private set; }
            public ConfigEntry<float> PlagueHeight { get; private set; }
            public ConfigEntry<float> CorpseRadius { get; private set; }
            public ConfigEntry<float> CorpseSpores { get; private set; }
            public ConfigEntry<float> CorpseSelfCurse { get; private set; }
            public ConfigEntry<float> CorpseZombieDistance { get; private set; }
            public ConfigEntry<float> RevenantDistance { get; private set; }
            public ConfigEntry<float> RevenantChaseTime { get; private set; }
            public ConfigEntry<float> BacklashRadius { get; private set; }
            public ConfigEntry<float> BacklashCurse { get; private set; }
            public ConfigEntry<float> BacklashPetrify { get; private set; }

            // ---- 17 种效果的抽取权重（默认均为 10）----
            public ConfigEntry<int> WeightBuff { get; private set; }
            public ConfigEntry<int> WeightStamina { get; private set; }
            public ConfigEntry<int> WeightInvincible { get; private set; }
            public ConfigEntry<int> WeightEnlighten { get; private set; }
            public ConfigEntry<int> WeightCleanse { get; private set; }
            public ConfigEntry<int> WeightHeal { get; private set; }
            public ConfigEntry<int> WeightAntiGrav { get; private set; }
            public ConfigEntry<int> WeightTeleport { get; private set; }
            public ConfigEntry<int> WeightRevive { get; private set; }
            public ConfigEntry<int> WeightBlood { get; private set; }
            public ConfigEntry<int> WeightSacrifice { get; private set; }
            public ConfigEntry<int> WeightSwap { get; private set; }
            public ConfigEntry<int> WeightDisaster { get; private set; }
            public ConfigEntry<int> WeightPlague { get; private set; }
            public ConfigEntry<int> WeightCorpse { get; private set; }
            public ConfigEntry<int> WeightRevenant { get; private set; }
            public ConfigEntry<int> WeightBacklash { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "法师", "启用职业", true, "是否启用法师职业。");
                MaxPlayers = BindL(config, "法师", "最大人数", 1,
                    "法师最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "法师", "最小人数", 0,
                    "法师最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "法师", "抽取权重", 10, "随机分配时法师被抽中的权重。");

                ScrollIntervalSeconds = BindL(config, "法师", "卷轴-发放间隔", 180f,
                    "魔法卷轴：每隔多少秒为每名法师发放 1 个卷轴（秒）。默认 180（3 分钟）。\n" +
                    "发放优先放入 3 个物品栏空位；三栏全满时在该法师脚下生成（落地后可拾取）。\n" +
                    "设为 0 或负数关闭发放。");
                ScrollItem = BindL(config, "法师", "卷轴-物品", "GuidebookPageScroll Variant",
                    "魔法卷轴：卷轴对应的物品预制体名。\n" +
                    "默认 GuidebookPageScroll Variant（使用后会变成一张教程纸页的卷轴）。");
                HumanDelaySeconds = BindL(config, "法师", "卷轴-延迟", 3f,
                    "魔法卷轴：人类形态下，打开卷轴后延迟多少秒才触发随机效果（秒）。默认 3。");
                HumanPetrify = BindL(config, "法师", "卷轴-人类石化", 0.05f,
                    "魔法卷轴：人类形态每次打开卷轴时，为法师自身增加的石化值（0.05 = 5 点）。设为 0 关闭。");
                SkeletonCurse = BindL(config, "法师", "卷轴-骷髅诅咒", 0.05f,
                    "黑魔法：骷髅形态每次打开卷轴时，为法师自身增加的诅咒（0.05 = 5 点）。设为 0 关闭。");
                ChainChance = BindL(config, "法师", "卷轴-连锁概率", 0.30f,
                    "黑魔法：触发 1 个效果后，再次触发下一个效果的概率（0~1）。默认 0.30（30%）。\n" +
                    "每次连锁触发后都重新按此概率判定是否继续；设为 0 则一次只触发 1 个效果。");
                ChainIntervalSeconds = BindL(config, "法师", "卷轴-连锁间隔", 1f,
                    "黑魔法：连锁触发时，下一个效果相对上一个效果的延迟（秒）。默认 1。\n" +
                    "用于避免一次性触发过多效果、降低瞬时性能损耗。设为 0 则几乎立即（约下一帧）连续结算。");

                // ---------------- 人类效果 ----------------
                BuffRadius = BindL(config, "法师", "强化-半径", 15f, "【强化】作用半径（米），含自己。默认 15。");
                StaminaRadius = BindL(config, "法师", "激化-半径", 15f, "【激化】作用半径（米），含自己。默认 15。");
                InvincibleRadius = BindL(config, "法师", "固化-半径", 15f, "【固化】作用半径（米），含自己。默认 15。");
                EnlightenRadius = BindL(config, "法师", "点化-半径", 10f,
                    "【点化】作用半径（米）。默认 10。不含自己。");
                EnlightenStamina = BindL(config, "法师", "点化-额外体力", 0.5f,
                    "【点化】每次为半径内每名玩家增加的额外体力（0.5 = 50 点）。设为 0 关闭该项。");
                CleanseRadius = BindL(config, "法师", "净化-半径", 10f, "【净化】作用半径（米），含自己。默认 10。");
                CleanseDuration = BindL(config, "法师", "净化-时长", 5f,
                    "【净化】持续时间（秒）。默认 5。期间每秒对负面状态 -「净化-每秒」。");
                CleansePerSecond = BindL(config, "法师", "净化-每秒", 0.025f,
                    "【净化】每秒为半径内每名玩家清除的负面状态量（0.025 = 2.5 点）。\n" +
                    "作用于可治愈的负面状态（损伤/饥饿/寒冷/中毒/困倦/过热/孢子/蛛网/捕蝇草）。设为 0 关闭。");
                HealRadius = BindL(config, "法师", "治愈-半径", 15f, "【治愈】作用半径（米），含自己。默认 15。");
                HealDuration = BindL(config, "法师", "治愈-时长", 10f,
                    "【治愈】持续时间（秒）。默认 10。期间每秒对损伤 -「治愈-每秒」。");
                HealPerSecond = BindL(config, "法师", "治愈-每秒", 0.025f,
                    "【治愈】每秒为半径内每名玩家清除的损伤量（0.025 = 2.5 点）。设为 0 关闭。");
                AntiGravRadius = BindL(config, "法师", "反重-半径", 20f, "【反重】作用半径（米），含自己。默认 20。");
                AntiGravDuration = BindL(config, "法师", "反重-时长", 10f,
                    "【反重】持续时间（秒）。默认 10。期间每「反重-间隔」秒在脚下生成 1 个反重力场。");
                AntiGravInterval = BindL(config, "法师", "反重-间隔", 5f,
                    "【反重】每隔多少秒在半径内每名玩家脚下生成 1 个反重力场（秒）。默认 5。");
                AntiGravPrefab = BindL(config, "法师", "反重-预制体", "AntiSphere",
                    "【反重】每次生成的反重力场预制体名。\n" +
                    "默认 AntiSphere（与反重力炮开炮生成的反重力场同一个预制体）。留空则本效果失效。\n" +
                    "实现：该预制体在游戏内的注册名会先在场景中查找并解析，找不到时才退回本配置名。");
                TeleportRadius = BindL(config, "法师", "传送-半径", 10f,
                    "【传送】被传送的玩家选取半径（米），含自己。默认 10。");
                TeleportMinDistance = BindL(config, "法师", "传送-最小距离", 50f,
                    "【传送】目标玩家与法师的最小距离（米）。默认 50。\n" +
                    "即把半径内玩家传送到「离法师 50 米以外」的随机一名玩家处。");
                ReviveDistance = BindL(config, "法师", "复活-距离", 1.5f,
                    "【苏生】/【还魂】复活玩家时，复活点相对法师的偏移距离（米）。默认 1.5（即「自己身边」）。");

                // ---------------- 骷髅（黑魔法）效果 ----------------
                BloodRadius = BindL(config, "法师", "血祭-半径", 15f, "【血祭】作用半径（米）。默认 15。");
                BloodInjury = BindL(config, "法师", "血祭-人类损伤", 0.10f,
                    "【血祭】为半径内每名人类玩家增加的损伤（0.10 = 10 点）。设为 0 关闭。");
                BloodStamina = BindL(config, "法师", "血祭-人类体力", 0.20f,
                    "【血祭】为半径内每名人类玩家增加的额外体力（0.20 = 20 点）。设为 0 关闭。");
                BloodSelfCursePerTarget = BindL(config, "法师", "血祭-自身每人诅咒", 0.025f,
                    "【血祭】每有 1 名玩家受影响，为法师自身增加的诅咒（0.025 = 2.5 点）。设为 0 关闭。");
                BloodSelfStaminaPerTarget = BindL(config, "法师", "血祭-自身每人体力", 0.25f,
                    "【血祭】每有 1 名玩家受影响，为法师自身增加的额外体力（0.25 = 25 点）。设为 0 关闭。");
                SacrificeRadius = BindL(config, "法师", "献祭-半径", 15f, "【献祭】作用半径（米）。默认 15。");
                SacrificeItem = BindL(config, "法师", "献祭-物品", "Cursed Skull",
                    "【献祭】对半径内随机 1 名玩家「使用」的物品预制体名。\n" +
                    "默认 Cursed Skull（诅咒头骨）。实现：主机生成该物品并通过原版喂食通道交给目标玩家使用。\n" +
                    "留空则本效果只给自己上增益、不对目标生效。");
                SacrificeSelfCurse = BindL(config, "法师", "献祭-自身诅咒", 0.05f,
                    "【献祭】为法师自身增加的诅咒（0.05 = 5 点）。设为 0 关闭。");
                SwapMinDistance = BindL(config, "法师", "易位-最小距离", 50f,
                    "【易位】互换位置的目标玩家与法师的最小距离（米）。默认 50。\n" +
                    "若没有符合条件的玩家，则改为获得 1 个曲迁菇。");
                DisasterLifetime = BindL(config, "法师", "天灾-存在时长", 5f,
                    "【天灾】龙卷风存在时长（秒），到时由主机销毁。默认 5。");
                PlagueRadius = BindL(config, "法师", "虫灾-半径", 15f, "【虫灾】作用半径（米），含自己。默认 15。");
                PlagueHeight = BindL(config, "法师", "虫灾-生成高度", 2.5f,
                    "【虫灾】甲虫在每名玩家「头顶上方」生成的高度（米）。默认 2.5。");
                CorpseRadius = BindL(config, "法师", "尸变-半径", 15f, "【尸变】作用半径（米），不含自己。默认 15。");
                CorpseSpores = BindL(config, "法师", "尸变-孢子", 1f,
                    "【尸变】为半径内随机 1 名人类玩家增加的孢子（1 = 100 点）。设为 0 关闭。");
                CorpseSelfCurse = BindL(config, "法师", "尸变-自身诅咒", 0.025f,
                    "【尸变】为法师自身增加的诅咒（0.025 = 2.5 点）。设为 0 关闭。");
                CorpseZombieDistance = BindL(config, "法师", "尸变-僵尸距离", 3f,
                    "【尸变】半径内没有符合条件的人类玩家时，改为在法师「半径多少米外」生成 1 只僵尸。默认 3。");
                RevenantDistance = BindL(config, "法师", "还魂-领队距离", 10f,
                    "【还魂】无死亡玩家可复活时，在法师「面前多少米处」生成 1 名童军领队（米）。默认 10。");
                RevenantChaseTime = BindL(config, "法师", "还魂-追击时间", 15f,
                    "【还魂】童军领队强制追击法师的时长（秒）。默认 15，超时后领队停止追击并自行离去（同原版喇叭机制）。");
                BacklashRadius = BindL(config, "法师", "反噬-半径", 15f, "【反噬】作用半径（米），含自己。默认 15。");
                BacklashCurse = BindL(config, "法师", "反噬-诅咒", 0.10f,
                    "【反噬】为半径内每名玩家增加的诅咒（0.10 = 10 点）。设为 0 关闭。");
                BacklashPetrify = BindL(config, "法师", "反噬-石化", 0.10f,
                    "【反噬】为半径内每名玩家增加的石化（0.10 = 10 点）。设为 0 关闭。");

                // ---------------- 抽取权重（17 项，默认均为 10） ----------------
                WeightBuff = BindL(config, "法师", "权重-强化", 10,
                    "【强化】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightStamina = BindL(config, "法师", "权重-激化", 10,
                    "【激化】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightInvincible = BindL(config, "法师", "权重-固化", 10,
                    "【固化】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightEnlighten = BindL(config, "法师", "权重-点化", 10,
                    "【点化】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightCleanse = BindL(config, "法师", "权重-净化", 10,
                    "【净化】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightHeal = BindL(config, "法师", "权重-治愈", 10,
                    "【治愈】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightAntiGrav = BindL(config, "法师", "权重-反重", 10,
                    "【反重】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightTeleport = BindL(config, "法师", "权重-传送", 10,
                    "【传送】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightRevive = BindL(config, "法师", "权重-苏生", 10,
                    "【苏生】被随机抽中的权重。设为 0 则该效果不会被抽中。");
                WeightBlood = BindL(config, "法师", "权重-血祭", 10,
                    "【血祭】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightSacrifice = BindL(config, "法师", "权重-献祭", 10,
                    "【献祭】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightSwap = BindL(config, "法师", "权重-易位", 10,
                    "【易位】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightDisaster = BindL(config, "法师", "权重-天灾", 10,
                    "【天灾】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightPlague = BindL(config, "法师", "权重-虫灾", 10,
                    "【虫灾】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightCorpse = BindL(config, "法师", "权重-尸变", 10,
                    "【尸变】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightRevenant = BindL(config, "法师", "权重-还魂", 10,
                    "【还魂】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
                WeightBacklash = BindL(config, "法师", "权重-反噬", 10,
                    "【反噬】被随机抽中的权重（仅黑魔法池）。设为 0 则该效果不会被抽中。");
            }
        }

        /// <summary>
        /// 挑战者（Challenger）。
        /// 【极限】开局自带诅咒 / 石化，死亡复活后诅咒与石化都回到固定值；
        /// 【顶级玩家】开局自带黄金抓钩与滑翔翼；
        /// 【不屑】用抓钩钩到玩家时立刻给自己增加损伤，避免靠钩人取巧。
        /// </summary>
        public class ChallengerConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 极限 ----
            public ConfigEntry<float> InitCurseAmount { get; private set; }
            public ConfigEntry<float> InitPetrifyAmount { get; private set; }
            public ConfigEntry<float> ReviveCurseFixed { get; private set; }
            public ConfigEntry<float> RevivePetrifyFixed { get; private set; }

            // ---- 顶级玩家 ----
            public ConfigEntry<int> GoldenHookCount { get; private set; }
            public ConfigEntry<int> GliderCount { get; private set; }
            public ConfigEntry<bool> TopPlayerPandoraImmune { get; private set; }

            // ---- 不屑 ----
            public ConfigEntry<float> DisdainInjury { get; private set; }
            public ConfigEntry<bool> DisdainBoxDisappear { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "挑战者", "启用职业", true,
                    "是否启用「挑战者」职业。");
                MaxPlayers = BindL(config, "挑战者", "最大人数", 1,
                    "本局最多分配多少名挑战者。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "挑战者", "最小人数", 0,
                    "本局至少分配多少名挑战者（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "挑战者", "抽取权重", 10,
                    "随机分配时「挑战者」被抽中的权重。默认 10。");

                // ---------------- 极限 ----------------
                InitCurseAmount = BindL(config, "挑战者", "极限-初始诅咒", 0.60f,
                    "【极限】开局自动获得的诅咒（0.60 = 60 点）。默认 0.60。设为 0 关闭。");
                InitPetrifyAmount = BindL(config, "挑战者", "极限-初始石化", 0.70f,
                    "【极限】开局自动获得的石化（0.70 = 70 点）。默认 0.70。设为 0 关闭。");
                ReviveCurseFixed = BindL(config, "挑战者", "极限-复活诅咒", 0.60f,
                    "【极限】死亡后复活时，诅咒被固定恢复为该值（0.60 = 60 点）。默认 0.60。\n" +
                    "原版复活会额外 +5 点诅咒，本项由主机在复活后把诅咒对齐到该固定值（差值下发）。\n" +
                    "设为 0 则复活时诅咒归零。");
                RevivePetrifyFixed = BindL(config, "挑战者", "极限-复活石化", 0.70f,
                    "【极限】死亡后复活时，石化被固定恢复为该值（0.70 = 70 点）。默认 0.70。\n" +
                    "原版复活会把石化减掉 75 点，本项由主机在复活后反复校正到位（绝对赋值、幂等）。\n" +
                    "设为 0 则复活时不再恢复石化。");

                // ---------------- 顶级玩家 ----------------
                GoldenHookCount = BindL(config, "挑战者", "顶级玩家-黄金抓钩", 1,
                    "【顶级玩家】开局自带的黄金抓钩数量（预制体 RescueHook_Infinite，即抓钩模式的无限金色救援爪）。\n" +
                    "默认 1。设为 0 不发放。");
                GliderCount = BindL(config, "挑战者", "顶级玩家-滑翔翼", 1,
                    "【顶级玩家】开局自带的滑翔翼数量（预制体 Glider）。默认 1。设为 0 不发放。");
                TopPlayerPandoraImmune = BindL(config, "挑战者", "顶级玩家-免疫潘多拉", true,
                    "【顶级玩家】挑战者是否免疫潘多拉之子的职业效果：站在潘多拉作用范围内时不会被施加潘多拉魔盒。\n" +
                    "默认 true（参考圣人·超然物外，但只覆盖潘多拉一项，不免疫亡灵光环损伤 / 全场欢呼强制跟跳）。");

                // ---------------- 不屑 ----------------
                DisdainInjury = BindL(config, "挑战者", "不屑-损伤", 0.50f,
                    "【不屑】用抓钩（黄金抓钩 / 救援抓钩）钩到玩家时，立刻为自己增加的损伤（0.50 = 50 点）。\n" +
                    "设为 0 关闭。");
                DisdainBoxDisappear = BindL(config, "挑战者", "不屑-魔盒消失", true,
                    "【不屑】挑战者拾取「潘多拉魔盒」（预制体 PandorasBox）时，物品直接消失、不进背包。\n" +
                    "默认 true。设为 false 关闭（此时可正常拾取魔盒）。");
            }
        }

        /// <summary>
        /// 特级厨师（MasterChef）：自带厨具 / 手暖炉 / 烹饪 / 煤气中毒 / 破坏大师。
        /// 「厨具」即便携烤炉，预制体名 PortableStovetopItem（Silver 模组物品表核对：便携火炉）。
        /// </summary>
        public class MasterChefConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 自带厨具 ----
            public ConfigEntry<int> InitialStoveCount { get; private set; }
            public ConfigEntry<float> StoveIntervalSeconds { get; private set; }
            public ConfigEntry<int> StoveCountPerInterval { get; private set; }

            // ---- 手暖炉 ----
            public ConfigEntry<float> HandWarmerIntervalSeconds { get; private set; }
            public ConfigEntry<float> HandWarmerHot { get; private set; }

            // ---- 烹饪 / 煤气中毒 ----
            public ConfigEntry<float> CookHungerReduce { get; private set; }
            public ConfigEntry<int> GasPoisonEveryCooks { get; private set; }
            public ConfigEntry<float> GasPoisonAmount { get; private set; }

            // ---- 破坏大师 ----
            public ConfigEntry<float> BreakerIntervalSeconds { get; private set; }
            public ConfigEntry<float> BreakerStamina { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "特级厨师", "启用职业", true,
                    "是否启用「特级厨师」职业。");
                MaxPlayers = BindL(config, "特级厨师", "最大人数", 1,
                    "本局最多分配多少名特级厨师。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "特级厨师", "最小人数", 0,
                    "本局至少分配多少名特级厨师（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "特级厨师", "抽取权重", 10,
                    "随机分配时「特级厨师」被抽中的权重。默认 10。");

                // ---------------- 自带厨具 ----------------
                InitialStoveCount = BindL(config, "特级厨师", "自带厨具-初始数量", 1,
                    "【自带厨具】开局初始自带的便携烤炉数量（预制体 PortableStovetopItem）。\n" +
                    "优先放入 3 个物品栏空位，全满则在脚下生成。默认 1。设为 0 不发放。");
                StoveIntervalSeconds = BindL(config, "特级厨师", "自带厨具-发放间隔", 300f,
                    "【自带厨具】每隔多少秒额外获得一次便携烤炉（默认 300 秒 = 5 分钟）。\n" +
                    "设为 0 或负数关闭。");
                StoveCountPerInterval = BindL(config, "特级厨师", "自带厨具-每次数量", 1,
                    "【自带厨具】每次触发发放的便携烤炉数量。默认 1。");

                // ---------------- 手暖炉 ----------------
                HandWarmerIntervalSeconds = BindL(config, "特级厨师", "手暖炉-间隔", 1f,
                    "【手暖炉】每隔多少秒结算一次「手持便携烤炉」的炎热加成（默认 1 秒）。\n" +
                    "设为 0 或负数关闭。");
                HandWarmerHot = BindL(config, "特级厨师", "手暖炉-炎热量", 0.025f,
                    "【手暖炉】每次结算增加的炎热（0.025 = 2.5 点）。默认 0.025。");

                // ---------------- 烹饪 / 煤气中毒 ----------------
                CookHungerReduce = BindL(config, "特级厨师", "烹饪-减饥饿", 0.025f,
                    "【烹饪】本局每烤制一次物品，自身减少的饥饿（0.025 = 2.5 点）。默认 0.025。设为 0 关闭。");
                GasPoisonEveryCooks = BindL(config, "特级厨师", "煤气中毒-每N次", 3,
                    "【煤气中毒】本局每烤制多少次物品，触发一次中毒。默认 3。设为 0 关闭。");
                GasPoisonAmount = BindL(config, "特级厨师", "煤气中毒-中毒量", 0.10f,
                    "【煤气中毒】每次触发增加的中毒（0.10 = 10 点）。默认 0.10。");

                // ---------------- 破坏大师 ----------------
                BreakerIntervalSeconds = BindL(config, "特级厨师", "破坏大师-触发间隔", 60f,
                    "【破坏大师】每「多少秒」内，烤制非食物物品最多触发一次额外体力（默认 60 秒 = 1 分钟）。\n" +
                    "即同一分钟内多次烤制非食物物品只触发一次。设为 0 或负数关闭。");
                BreakerStamina = BindL(config, "特级厨师", "破坏大师-额外体力", 0.10f,
                    "【破坏大师】触发时获得的额外体力（0.10 = 10 点）。默认 0.10。");
            }
        }

        /// <summary>
        /// 炊事员（Cook）：后勤保障 / 饮食供给 / 食品安全 / 创伤应激。
        /// 「食物」判定沿用游戏原生组件 Action_RestoreHunger（与原版「恢复饥饿」一致）。
        /// </summary>
        public class CookConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 后勤保障 ----
            public ConfigEntry<int> InitialStoveCount { get; private set; }
            public ConfigEntry<float> StoveIntervalSeconds { get; private set; }
            public ConfigEntry<int> StoveCountPerInterval { get; private set; }
            public ConfigEntry<float> SupportRadius { get; private set; }
            public ConfigEntry<float> FoodIntervalSeconds { get; private set; }
            public ConfigEntry<string> FoodPool { get; private set; }

            // ---- 饮食供给 ----
            public ConfigEntry<float> SupplyRadius { get; private set; }
            public ConfigEntry<float> SupplyHungerReduce { get; private set; }
            public ConfigEntry<float> SupplyStamina { get; private set; }

            // ---- 食品安全 ----
            public ConfigEntry<float> SafetyHungerReduce { get; private set; }
            public ConfigEntry<float> SafetyInjuryReduce { get; private set; }
            public ConfigEntry<float> SafetyStamina { get; private set; }

            // ---- 创伤应激 ----
            public ConfigEntry<float> StressInjury { get; private set; }
            public ConfigEntry<float> StressPoison { get; private set; }
            public ConfigEntry<float> StressDrowsy { get; private set; }
            public ConfigEntry<float> StressRadius { get; private set; }
            public ConfigEntry<float> StressIntervalSeconds { get; private set; }
            public ConfigEntry<float> StressHungerThreshold { get; private set; }
            public ConfigEntry<float> StressDrowsyPerTick { get; private set; }
            public ConfigEntry<string> StressBlacklist { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "炊事员", "启用职业", true,
                    "是否启用「炊事员」职业。");
                MaxPlayers = BindL(config, "炊事员", "最大人数", 1,
                    "本局最多分配多少名炊事员。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "炊事员", "最小人数", 0,
                    "本局至少分配多少名炊事员（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "炊事员", "抽取权重", 10,
                    "随机分配时「炊事员」被抽中的权重。默认 10。");

                // ---------------- 后勤保障 ----------------
                InitialStoveCount = BindL(config, "炊事员", "后勤保障-每层数量", 1,
                    "【后勤保障】每次进入新关卡自动获得的便携烤炉数量（预制体 PortableStovetopItem）。\n" +
                    "优先放入 3 个物品栏空位，全满则在脚下生成。默认 1。设为 0 不发放。");
                StoveIntervalSeconds = BindL(config, "炊事员", "后勤保障-发放间隔", 180f,
                    "【后勤保障】每隔多少秒额外获得一次便携烤炉（默认 180 秒 = 3 分钟）。\n" +
                    "设为 0 或负数关闭。");
                StoveCountPerInterval = BindL(config, "炊事员", "后勤保障-每次数量", 1,
                    "【后勤保障】每次触发发放的便携烤炉数量。默认 1。");
                SupportRadius = BindL(config, "炊事员", "后勤保障-后勤半径", 30f,
                    "【后勤保障】自身半径多少米内存在「后勤」职业玩家时，才会触发下方的随机食物发放。\n" +
                    "默认 30。");
                FoodIntervalSeconds = BindL(config, "炊事员", "后勤保障-食物间隔", 90f,
                    "【后勤保障】半径内存在后勤时，每隔多少秒向自身的 3 个物品栏发放 1 个随机食物（默认 90 秒）。\n" +
                    "三个物品栏全满时不发放，也绝不塞主手（与后勤·紧急空投同规则）。设为 0 或负数关闭。");
                FoodPool = BindL(config, "炊事员", "后勤保障-食物池",
                    // 普通食物
                    "Item_Coconut_half,Item_Coconut,Energy Drink,Sports Drink,Granola Bar,TrailMix," +
                    "FortifiedMilk,Lollipop,Marshmallow,Glizzy,Airplane Food,ScoutCookies," +
                    "ScoutCookies_Vanilla,Beehive,Item_Honeycomb,NestEgg,NestEgg_Raven,Egg,EggTurkey," +
                    "EarlyWorm,FrogLegs,Bugfix," +
                    // 浆果
                    "Apple Berry Green,Apple Berry Yellow,Apple Berry Red,Pepper Berry," +
                    "Berrynana Pink,Berrynana Yellow,Berrynana Blue,Berrynana Brown," +
                    "Clusterberry Black,Clusterberry Yellow,Clusterberry Red," +
                    "Kingberry Green,Kingberry Purple,Kingberry Yellow," +
                    "Prickleberry_Gold,Prickleberry_Red,Winterberry Orange,Winterberry Yellow,Napberry," +
                    // 蘑菇（含各种有毒变种）
                    "Mushroom Chubby,Mushroom Cluster,Mushroom Cluster Poison," +
                    "Mushroom Lace,Mushroom Lace Poison,Mushroom Normie,Mushroom Normie Poison," +
                    "Shroomberry_Blue,Shroomberry_Green,Shroomberry_Purple,Shroomberry_Red,Shroomberry_Yellow",
                    "【后勤保障】随机食物的候选池（英文逗号分隔的物品预制体名）。\n" +
                    "默认 53 项：普通食物 22 + 全部浆果 19 + 蘑菇 12（含 Mushroom Cluster Poison、Mushroom Lace Poison、\n" +
                    "Mushroom Normie Poison 等有毒变种）。\n" +
                    "写错或未实装的名称会被自动跳过。留空时退化为「数据库中带恢复饥饿组件 Action_RestoreHunger 的全部物品」。");

                // ---------------- 饮食供给 ----------------
                SupplyRadius = BindL(config, "炊事员", "饮食供给-半径", 15f,
                    "【饮食供给】烤制「从未烤过的食物」时，作用半径（默认 15 米，含自己）。");
                SupplyHungerReduce = BindL(config, "炊事员", "饮食供给-减饥饿", 0.05f,
                    "【饮食供给】半径内每名玩家减少的饥饿（0.05 = 5 点）。默认 0.05。");
                SupplyStamina = BindL(config, "炊事员", "饮食供给-额外体力", 0.05f,
                    "【饮食供给】半径内每名玩家获得的额外体力（0.05 = 5 点）。默认 0.05。");

                // ---------------- 食品安全 ----------------
                SafetyHungerReduce = BindL(config, "炊事员", "食品安全-减饥饿", 0.025f,
                    "【食品安全】食用「烤熟的食物」后自身减少的饥饿（0.025 = 2.5 点）。默认 0.025。");
                SafetyInjuryReduce = BindL(config, "炊事员", "食品安全-减损伤", 0.025f,
                    "【食品安全】食用「烤熟的食物」后自身减少的损伤（0.025 = 2.5 点）。默认 0.025。");
                SafetyStamina = BindL(config, "炊事员", "食品安全-额外体力", 0.10f,
                    "【食品安全】食用「烤熟的食物」后自身获得的额外体力（0.10 = 10 点）。默认 0.10。");

                // ---------------- 创伤应激 ----------------
                StressInjury = BindL(config, "炊事员", "创伤应激-损伤", 0.10f,
                    "【创伤应激】食用「未烤熟的食物」后自身增加的损伤（0.10 = 10 点）。默认 0.10。\n" +
                    "（诡异菇、能量饮料、奶白金、大棒棒糖除外）");
                StressPoison = BindL(config, "炊事员", "创伤应激-中毒", 0.20f,
                    "【创伤应激】食用「未烤熟的食物」后自身增加的中毒（0.20 = 20 点）。默认 0.20。");
                StressDrowsy = BindL(config, "炊事员", "创伤应激-困倦", 0.30f,
                    "【创伤应激】食用「未烤熟的食物」后自身增加的困倦（0.30 = 30 点）。默认 0.30。");
                StressRadius = BindL(config, "炊事员", "创伤应激-光环半径", 15f,
                    "【创伤应激】持续检测的半径（默认 15 米）。");
                StressIntervalSeconds = BindL(config, "炊事员", "创伤应激-间隔", 3f,
                    "【创伤应激】半径内存在饥饿值超过阈值的玩家（含自身）时，每隔多少秒为自身叠加一次困倦\n" +
                    "（默认 3 秒）。设为 0 或负数关闭。");
                StressHungerThreshold = BindL(config, "炊事员", "创伤应激-饥饿阈值", 0.30f,
                    "【创伤应激】判定用饥饿阈值（0.30 = 30 点）。半径内有玩家饥饿高于该值时触发。默认 0.30。");
                StressDrowsyPerTick = BindL(config, "炊事员", "创伤应激-困倦量", 0.05f,
                    "【创伤应激】每次触发为自身增加的困倦（0.05 = 5 点）。默认 0.05。");
                StressBlacklist = BindL(config, "炊事员", "创伤应激-黑名单",
                    "Mushroom Glow,Energy Drink,FortifiedMilk,Lollipop",
                    "【创伤应激】食用这些物品时**不触发**损伤 / 中毒 / 困倦（预制体名，英文逗号分隔）。\n" +
                    "默认：诡异菇 Mushroom Glow（转职触发物品）、能量饮料 Energy Drink、奶白金 FortifiedMilk、大棒棒糖 Lollipop。\n" +
                    "留空表示不设黑名单。");
            }
        }

        /// <summary>
        /// 和平主义者（Pacifist）：【和平？】开局初始诅咒 / 右键（原本拉人）改为踢人 / 踢中与未踢中的体力结算。
        /// 「踢人」复用原版 <c>CharacterGrabbing</c> 的踢击（<c>RPCA_Kick</c> → 属主端 <c>KickCast</c>），
        /// 命中与否由原版广播的 <c>RPCA_KickImpact</c> 判定。
        /// </summary>
        public class PacifistConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 和平？ ----
            public ConfigEntry<float> InitCurseAmount { get; private set; }
            public ConfigEntry<float> HitStamina { get; private set; }
            public ConfigEntry<float> MissStamina { get; private set; }
            public ConfigEntry<float> MissDrowsy { get; private set; }
            public ConfigEntry<float> KickCooldown { get; private set; }
            public ConfigEntry<float> KickDistance { get; private set; }
            public ConfigEntry<float> KickAngle { get; private set; }
            public ConfigEntry<float> KickRagdollSeconds { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "和平主义者", "启用职业", true,
                    "是否启用「和平主义者」职业。");
                MaxPlayers = BindL(config, "和平主义者", "最大人数", 1,
                    "本局最多分配多少名和平主义者。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "和平主义者", "最小人数", 0,
                    "本局至少分配多少名和平主义者（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "和平主义者", "抽取权重", 10,
                    "随机分配时「和平主义者」被抽中的权重。默认 10。");

                // ---------------- 和平？ ----------------
                InitCurseAmount = BindL(config, "和平主义者", "和平？-初始诅咒", 0.20f,
                    "【和平？】开局自动获得的诅咒（0.20 = 20 点）。默认 0.20。设为 0 关闭。");
                HitStamina = BindL(config, "和平主义者", "和平？-踢中体力", 0.025f,
                    "【和平？】踢中一名玩家时获得的额外体力（0.025 = 2.5 点）。默认 0.025。");
                MissStamina = BindL(config, "和平主义者", "和平？-未中扣体力", 0.025f,
                    "【和平？】踢人未踢中玩家时**扣除**的额外体力（0.025 = 2.5 点）。默认 0.025。");
                MissDrowsy = BindL(config, "和平主义者", "和平？-未中困倦", 0.20f,
                    "【和平？】踢人未踢中玩家时增加的困倦（0.20 = 20 点）。默认 0.20。");
                KickCooldown = BindL(config, "和平主义者", "和平？-踢人冷却", 0.7f,
                    "【和平？】两次右键踢人之间的最小间隔（秒），用于去重：\n" +
                    "按住右键后原版会在约 0.2 秒内反复广播「开始拉人」，若不限流会一次按键踢出多脚。\n" +
                    "默认 0.7。");
                KickDistance = BindL(config, "和平主义者", "和平？-踢人距离", 1.5f,
                    "【和平？】踢击的判定距离（米）：只对该距离内、且位于身前锥形范围内的玩家判定为「踢中」。\n" +
                    "默认 1.5（原版踢击的判定距离约 1 米）。");
                KickAngle = BindL(config, "和平主义者", "和平？-踢人角度", 60f,
                    "【和平？】踢击的判定角度（度，单侧）：目标与踢人者朝向的夹角超过该值则不算踢中。\n" +
                    "默认 60（原版踢击约 45）。");
                KickRagdollSeconds = BindL(config, "和平主义者", "和平？-击倒时长", 1f,
                    "【和平？】被踢中者进入倒地（布娃娃）状态的时长（秒），与原版踢击的踢倒时长一致。\n" +
                    "默认 1。设为 0 则只结算体力与音效、不让对方倒地。");
            }
        }

        /// <summary>
        /// 仓鼠（Hamster）：【我全都要】开局默认装备一个背包；自身负重达到阈值后，
        /// 每隔若干秒获得一次「加速」（直接参考先驱【高代谢】的加速实现）。
        /// </summary>
        public class HamsterConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 我全都要 ----
            public ConfigEntry<int> InitialBackpacks { get; private set; }
            public ConfigEntry<float> WeightThreshold { get; private set; }
            public ConfigEntry<float> SprintIntervalSeconds { get; private set; }
            public ConfigEntry<float> SprintDrowsyClearAmount { get; private set; }
            public ConfigEntry<float> ExtraStaminaIntervalSeconds { get; private set; }
            public ConfigEntry<float> ExtraStamina { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "仓鼠", "启用职业", true,
                    "是否启用「仓鼠」职业。");
                MaxPlayers = BindL(config, "仓鼠", "最大人数", 1,
                    "本局最多分配多少名仓鼠。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "仓鼠", "最小人数", 0,
                    "本局至少分配多少名仓鼠（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "仓鼠", "抽取权重", 10,
                    "随机分配时「仓鼠」被抽中的权重。默认 10。");

                // ---------------- 我全都要 ----------------
                InitialBackpacks = BindL(config, "仓鼠", "我全都要-初始背包", 1,
                    "【我全都要】开局为仓鼠装备的背包数量。默认 1（一个空背包）。\n" +
                    "与后勤「备战充分」同套机制：分配职业后每帧幂等补发，已装备背包会被覆盖替换。设为 0 关闭。");
                WeightThreshold = BindL(config, "仓鼠", "我全都要-负重阈值", 0.50f,
                    "【我全都要】触发加速所需的自身「负重」下限（0.50 = 50 点）。\n" +
                    "负重由原版按「背包 + 3 个物品栏 + 主手 + 背负队友」实时统计，并以 0.025 为一个量子步长\n" +
                    "写入状态数组（下标 Weight），本模组直接读取该同步值。默认 0.50。");
                SprintIntervalSeconds = BindL(config, "仓鼠", "我全都要-加速间隔", 10f,
                    "【我全都要】负重达标时维持加速：加速效果一结束就立刻续发（实现「无限加速」），\n" +
                    "本项为两次续发之间的最短间隔（秒）。默认 10。\n" +
                    "注意：原版能量饮料的加速（FasterBoi）在「已生效时再次饮用」不会延长时长\n" +
                    "（Affliction_FasterBoi.Stack 只取 max(totalTime)、不重置已累计的 timeElapsed），\n" +
                    "因此不能靠固定间隔盲发来衔接，本模组改为「检测到效果结束即续发」。\n" +
                    "本值需小于能量饮料的实测时长（约 11 秒）才能保证无间断。设为 0 或负数关闭本效果。");
                SprintDrowsyClearAmount = BindL(config, "仓鼠", "我全都要-加速清除困倦", 0.25f,
                    "【我全都要】能量饮料的加速效果结束、玩家被附加困倦后，主机下发抵消的困倦量（0.25 = 25 点）。\n" +
                    "与先驱「高代谢-加速后清除困倦」同理。设为 0 关闭清除。");
                ExtraStaminaIntervalSeconds = BindL(config, "仓鼠", "我全都要-额外体力间隔", 20f,
                    "【我全都要】负重达标时，每隔多少秒获得一次额外体力（秒）。默认 20。\n" +
                    "与「我全都要-加速间隔」各自独立计时。设为 0 或负数关闭。");
                ExtraStamina = BindL(config, "仓鼠", "我全都要-额外体力", 0.05f,
                    "【我全都要】负重达标时每次获得的额外体力（0.05 = 5 点）。默认 0.05。设为 0 关闭。");
            }
        }

        /// <summary>
        /// 救火队员（Firefighter）：【温暖臂膀】背起玩家时周期性为「自身与所背玩家」治疗损伤；
        /// 【火线救援】每有玩家进入昏迷（倒地，非死亡）时，为每名存活救火队员安排一次延迟救援 ——
        /// 到点后施加一次无敌效果并传送到该昏迷玩家处，背起该玩家时再一次性给予加速与额外体力；
        /// 【尽职尽责】每有「人类玩家」死亡，自身增加配置的石化值（带冷却；骷髅玩家死亡不触发）。
        /// </summary>
        public class FirefighterConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 温暖臂膀 ----
            public ConfigEntry<float> WarmIntervalSeconds { get; private set; }
            public ConfigEntry<float> WarmHealInjury { get; private set; }

            // ---- 火线救援 ----
            public ConfigEntry<string> RescueInvincibleItem { get; private set; }
            public ConfigEntry<float> RescueSprintDrowsyClearAmount { get; private set; }
            public ConfigEntry<float> RescueDelaySeconds { get; private set; }
            public ConfigEntry<float> RescueExtraStamina { get; private set; }
            public ConfigEntry<float> RescueCooldownSeconds { get; private set; }

            // ---- 尽职尽责 ----
            public ConfigEntry<float> DutyPetrify { get; private set; }
            public ConfigEntry<float> DutyCooldownSeconds { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "救火队员", "启用职业", true,
                    "是否启用「救火队员」职业。");
                MaxPlayers = BindL(config, "救火队员", "最大人数", 1,
                    "本局最多分配多少名救火队员。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "救火队员", "最小人数", 0,
                    "本局至少分配多少名救火队员（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "救火队员", "抽取权重", 10,
                    "随机分配时「救火队员」被抽中的权重。默认 10。");

                // ---------------- 温暖臂膀 ----------------
                WarmIntervalSeconds = BindL(config, "救火队员", "温暖臂膀-间隔", 20f,
                    "【温暖臂膀】背起玩家时，每隔多少秒结算一次治疗（秒）。默认 20。设为 0 或负数关闭。");
                WarmHealInjury = BindL(config, "救火队员", "温暖臂膀-减损伤", 0.025f,
                    "【温暖臂膀】每次结算为「救火队员自身」与「他正背着的玩家」各削减的损伤（0.025 = 2.5 点）。默认 0.025。\n" +
                    "只有救火队员正在背人时才结算：被背着的昏迷队友正是背人的目的，因此同样获得治疗。");

                // ---------------- 火线救援 ----------------
                RescueInvincibleItem = BindL(config, "救火队员", "火线救援-无敌物品", "FortifiedMilk",
                    "【火线救援】每有玩家进入昏迷（倒地，非死亡）时，发放给救火队员的无敌物品预制体名。\n" +
                    "默认 FortifiedMilk = 奶白金（原版无敌效果）。优先放入空物品栏，三栏全满时在脚下生成。");
                RescueSprintDrowsyClearAmount = BindL(config, "救火队员", "火线救援-加速清除困倦", 0.25f,
                    "【火线救援】能量饮料的加速效果结束、玩家被附加困倦后，主机下发抵消的困倦量（0.25 = 25 点）。\n" +
                    "与先驱「高代谢-加速后清除困倦」同理。设为 0 关闭清除。");
                RescueDelaySeconds = BindL(config, "救火队员", "火线救援-延迟", 3f,
                    "【火线救援】有玩家进入昏迷后，救火队员延迟多少秒再前往救援（秒）。默认 3。\n" +
                    "到点时若该昏迷玩家仍在昏迷、且救火队员仍存活，则为其施加一次无敌效果并传送到该昏迷玩家处，等待背起。");
                RescueExtraStamina = BindL(config, "救火队员", "火线救援-额外体力", 0.50f,
                    "【火线救援】救火队员背起「等待救援的那名昏迷玩家」时，一次性获得的额外体力（0.50 = 50 点）。默认 0.50。设为 0 关闭。");
                RescueCooldownSeconds = BindL(config, "救火队员", "火线救援-冷却", 10f,
                    "【火线救援】每名救火队员两次「触发」之间的最短间隔（秒）。默认 10。\n" +
                    "有玩家进入昏迷时为救火队员登记一次救援任务即视为一次触发；冷却内不再登记新的救援任务。\n" +
                    "另：救火队员自身正背着玩家时无法触发本效果。设为 0 或负数关闭冷却。");

                // ---------------- 尽职尽责 ----------------
                DutyPetrify = BindL(config, "救火队员", "尽职尽责-石化", 0.15f, "【尽职尽责】每有「人类玩家」死亡（真正死亡，昏迷不算；骷髅玩家不算）时，所有救火队员各自增加的石化值（0.15 = 15 点）。默认 0.15。设为 0 关闭。");
                DutyCooldownSeconds = BindL(config, "救火队员", "尽职尽责-冷却", 60f,
                    "【尽职尽责】每名救火队员两次石化之间的最短间隔（秒）。默认 60。\n" +
                    "仍由每一次人类玩家死亡触发，但每名救火队员在一次冷却内最多结算一次。设为 0 或负数关闭冷却。");
            }
        }

        /// <summary>
        /// 自恋狂（Narcissist）：【化妆盒】每次进入新关卡获得 3 个防晒喷雾（强制覆盖 3 个物品栏）；
        /// 【精致】开局自带初始诅咒、每用完 1 瓶防晒喷雾 +额外体力、每次获得防晒状态 +额外体力；
        /// 【化学中毒】每用完 1 瓶防晒喷雾 +石化。
        /// </summary>
        public class NarcissistConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 化妆盒 / 精致 / 化学中毒 ----
            public ConfigEntry<int> SunscreenCount { get; private set; }
            public ConfigEntry<string> SunscreenItem { get; private set; }
            public ConfigEntry<float> RefinedInitCurse { get; private set; }
            public ConfigEntry<float> RefinedStamina { get; private set; }
            public ConfigEntry<float> SunscreenStatusStamina { get; private set; }
            public ConfigEntry<float> SunscreenStatusCooldown { get; private set; }
            public ConfigEntry<float> ChemicalPetrify { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "自恋狂", "启用职业", true,
                    "是否启用「自恋狂」职业。");
                MaxPlayers = BindL(config, "自恋狂", "最大人数", 1,
                    "本局最多分配多少名自恋狂。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "自恋狂", "最小人数", 0,
                    "本局至少分配多少名自恋狂（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "自恋狂", "抽取权重", 10,
                    "随机分配时「自恋狂」被抽中的权重。默认 10。");

                // ---------------- 化妆盒 / 精致 / 化学中毒 ----------------
                SunscreenCount = BindL(config, "自恋狂", "化妆盒-每层数量", 3,
                    "【化妆盒】每次进入新关卡自动获得的防晒喷雾数量（默认 3）。\n" +
                    "强制覆盖该玩家 3 个物品栏（先清空目标槽位再写入，物品栏已满也能拿到）。设为 0 关闭。");
                SunscreenItem = BindL(config, "自恋狂", "化妆盒-物品", "Sunscreen",
                    "【化妆盒】发放的物品预制体名。默认 Sunscreen = 防晒喷雾。");
                RefinedInitCurse = BindL(config, "自恋狂", "精致-初始诅咒", 0.10f,
                    "【精致】开局初始自带的诅咒值（0.10 = 10 点）。默认 0.10。设为 0 关闭。\n" +
                    "与领袖「宿命」等初始诅咒同套机制（绝对赋值 + 反复校正），中途加入者复活到位后幂等补齐。");
                RefinedStamina = BindL(config, "自恋狂", "精致-额外体力", 0.30f,
                    "【精致】每用完 1 瓶防晒喷雾获得的额外体力（0.30 = 30 点）。默认 0.30。");
                SunscreenStatusStamina = BindL(config, "自恋狂", "精致-状态额外体力", 0.20f,
                    "【精致】自身每次「获得防晒状态」（使用防晒喷雾套上原版 Sunscreen 状态，含刷新）时获得的额外体力（0.20 = 20 点）。默认 0.20。设为 0 关闭。");
                SunscreenStatusCooldown = BindL(config, "自恋狂", "精致-状态额外体力冷却", 5f,
                    "【精致】「每次获得防晒状态 +额外体力」的冷却时间（秒）。默认 5。\n" +
                    "防晒状态在持续期间会被反复刷新（每次刷新都会触发一次「获得」），为防止高频刷额外体力，\n" +
                    "同一玩家在冷却时间内只结算一次。设为 0 或负数关闭冷却（不限频）。");
                ChemicalPetrify = BindL(config, "自恋狂", "化学中毒-石化", 0.10f,
                    "【化学中毒】每用完 1 瓶防晒喷雾增加的石化值（0.10 = 10 点）。默认 0.10。设为 0 关闭。");
            }
        }

        /// <summary>
        /// 独狼（LoneWolf）：【独行长路】身边没有其他玩家时，每 30 秒 +额外体力；
        /// 身边有玩家时，每秒 +困倦（两条按距离互斥）。
        /// </summary>
        public class LoneWolfConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 独行长路 ----
            public ConfigEntry<float> SoloIntervalSeconds { get; private set; }
            public ConfigEntry<float> SoloRadius { get; private set; }
            public ConfigEntry<float> SoloExtraStamina { get; private set; }
            public ConfigEntry<float> NearbyRadius { get; private set; }
            public ConfigEntry<float> NearbyDrowsyInterval { get; private set; }
            public ConfigEntry<float> NearbyDrowsy { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "独狼", "启用职业", true,
                    "是否启用「独狼」职业。");
                MaxPlayers = BindL(config, "独狼", "最大人数", 1,
                    "本局最多分配多少名独狼。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "独狼", "最小人数", 0,
                    "本局至少分配多少名独狼（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "独狼", "抽取权重", 10,
                    "随机分配时「独狼」被抽中的权重。默认 10。");

                // ---------------- 独行长路 ----------------
                SoloIntervalSeconds = BindL(config, "独狼", "独行-间隔", 30f,
                    "【独行长路】身边无其他玩家（自身 独行-半径 内没有任何其他存活玩家）时，每隔多少秒结算一次额外体力（秒）。默认 30。设为 0 关闭。");
                SoloRadius = BindL(config, "独狼", "独行-半径", 50f,
                    "【独行长路】判定「身边无人」的距离（米），以独狼自身为中心；此半径内没有任何其他玩家即视为独行。默认 50。");
                SoloExtraStamina = BindL(config, "独狼", "独行-额外体力", 0.10f,
                    "【独行长路】每次独行结算获得的额外体力（0.10 = 10 点）。默认 0.10。设为 0 关闭。");
                NearbyRadius = BindL(config, "独狼", "同伴-半径", 15f,
                    "【独行长路】判定「身边有人」的距离（米），以独狼自身为中心；此半径内存在任意其他玩家即触发困倦。默认 15。");
                NearbyDrowsyInterval = BindL(config, "独狼", "同伴-困倦间隔", 3f,
                    "【独行长路】身边有玩家时，每隔多少秒施加一次困倦（秒）。默认 3。设为 0 关闭。");
                NearbyDrowsy = BindL(config, "独狼", "同伴-困倦", 0.025f,
                    "【独行长路】身边有玩家时每次施加的困倦量（0.025 = 2.5 点）。默认 0.025。设为 0 关闭。");
            }
        }

        /// <summary>
        /// 炮手（Gunner）：【开炮】每次进入新关卡 +1 童子军大炮、每 3 分钟 +1 童子军大炮；
        /// 【炸膛】每次受到损伤（损伤状态增加）时 +1 炸药（优先物品栏，全满则塞主手并握住）。
        /// </summary>
        public class GunnerConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 开炮 ----
            public ConfigEntry<int> LevelCannonCount { get; private set; }
            public ConfigEntry<float> CannonIntervalSeconds { get; private set; }
            public ConfigEntry<int> CannonCount { get; private set; }

            // ---- 炸膛 ----
            public ConfigEntry<int> ExplosiveCount { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "炮手", "启用职业", true,
                    "是否启用「炮手」职业。");
                MaxPlayers = BindL(config, "炮手", "最大人数", 1,
                    "本局最多分配多少名炮手。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "炮手", "最小人数", 0,
                    "本局至少分配多少名炮手（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "炮手", "抽取权重", 10,
                    "随机分配时「炮手」被抽中的权重。默认 10。");

                // ---------------- 开炮 ----------------
                LevelCannonCount = BindL(config, "炮手", "开炮-每层大炮数", 1,
                    "【开炮】每次进入新关卡自动获得的童子军大炮 ScoutCannonItem 数量（默认 1）。\n" +
                    "优先放入空物品栏；物品栏没有空位时本次不发、后续帧继续尝试。设为 0 关闭。");
                CannonIntervalSeconds = BindL(config, "炮手", "开炮-大炮间隔", 180f,
                    "【开炮】每隔多少秒额外获得一次童子军大炮（秒）。默认 180（3 分钟）。设为 0 关闭。");
                CannonCount = BindL(config, "炮手", "开炮-大炮数", 1,
                    "【开炮】每到大炮间隔时获得的童子军大炮 ScoutCannonItem 数量（默认 1）。");
                ExplosiveCount = BindL(config, "炮手", "炸膛-炸药", 1,
                    "【炸膛】每次受到损伤（损伤状态较上一轮增加）时获得的炸药 Dynamite 数量（默认 1）。\n" +
                    "优先放入空物品栏；3 个物品栏全满时放入主手并强制握住。设为 0 关闭。");
            }
        }

        /// <summary>
        /// 乐队成员（Band）：【乐器】拾取物品会被替换成小号 / 童军领队的号角 / 友谊喇叭，
        /// 并每 30 秒按身背的乐器数量结算额外体力（三种齐全再给一次加速）；
        /// 【演奏】吹响乐器时对半径内玩家加成，持续吹响再触发一次更强效果；
        /// 【乐队】附近有其他乐队成员时获得额外体力 / 清除困倦，并放大【演奏】半径；
        /// 【体弱】开局自带初始诅咒。
        /// </summary>
        public class BandConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 乐器 ----
            public ConfigEntry<int> InstrumentNoChangeWeight { get; private set; }
            public ConfigEntry<int> InstrumentBugleWeight { get; private set; }
            public ConfigEntry<int> InstrumentScoutmasterWeight { get; private set; }
            public ConfigEntry<int> InstrumentMagicWeight { get; private set; }
            public ConfigEntry<string> InstrumentBlacklist { get; private set; }
            public ConfigEntry<float> InstrumentIntervalSeconds { get; private set; }
            public ConfigEntry<float> BugleExtraStamina { get; private set; }
            public ConfigEntry<float> ScoutmasterExtraStamina { get; private set; }
            public ConfigEntry<float> MagicExtraStamina { get; private set; }

            // ---- 演奏 ----
            public ConfigEntry<float> BlowRadius { get; private set; }
            public ConfigEntry<float> BlowExtraStamina { get; private set; }
            public ConfigEntry<float> BlowCooldown { get; private set; }
            public ConfigEntry<float> HornSelfExtraStamina { get; private set; }
            public ConfigEntry<int> DrowsyEveryBlows { get; private set; }
            public ConfigEntry<float> DrowsyPerBlows { get; private set; }

            // ---- 乐队 ----
            public ConfigEntry<float> GroupIntervalSeconds { get; private set; }
            public ConfigEntry<float> GroupRadius { get; private set; }
            public ConfigEntry<float> GroupExtraStamina { get; private set; }
            public ConfigEntry<float> GroupClearDrowsy { get; private set; }
            public ConfigEntry<float> GroupRadiusBonus { get; private set; }
            public ConfigEntry<float> GroupRadiusBonusCap { get; private set; }

            // ---- 转职（补充效果） ----
            public ConfigEntry<bool> TransferEnabled { get; private set; }
            public ConfigEntry<string> TransferItems { get; private set; }
            public ConfigEntry<float> TransferRadius { get; private set; }
            public ConfigEntry<float> TransferIntervalSeconds { get; private set; }
            public ConfigEntry<int> TransferStreakRequired { get; private set; }
            public ConfigEntry<float> TransferChance { get; private set; }

            // ---- 体弱 ----
            public ConfigEntry<float> FrailInitCurse { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "乐队成员", "启用职业", true,
                    "是否启用「乐队成员」职业。");
                MaxPlayers = BindL(config, "乐队成员", "最大人数", 5,
                    "本局最多分配多少名乐队成员。0 = 不限制。默认 5。");
                MinPlayers = BindL(config, "乐队成员", "最小人数", 0,
                    "本局至少分配多少名乐队成员（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "乐队成员", "抽取权重", 5,
                    "随机分配时「乐队成员」被抽中的权重。默认 5。");

                // ---------------- 乐器 ----------------
                InstrumentNoChangeWeight = BindL(config, "乐队成员", "乐器-不变权重", 50,
                    "【乐器】拾取物品时「保持不变」的权重（默认 50）。");
                InstrumentBugleWeight = BindL(config, "乐队成员", "乐器-小号权重", 30,
                    "【乐器】拾取物品时「变成小号 Bugle」的权重（默认 30）。");
                InstrumentScoutmasterWeight = BindL(config, "乐队成员", "乐器-号角权重", 10,
                    "【乐器】拾取物品时「变成童军领队的号角 Bugle_Scoutmaster Variant」的权重（默认 10）。");
                InstrumentMagicWeight = BindL(config, "乐队成员", "乐器-友谊喇叭权重", 10,
                    "【乐器】拾取物品时「变成友谊喇叭 Bugle_Magic」的权重（默认 10）。");
                InstrumentBlacklist = BindL(config, "乐队成员", "乐器-黑名单",
                    "Mushroom Glow,Bugle,Bugle_Scoutmaster Variant,Bugle_Magic,Flare," +
                    "Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor,Strange Gem",
                    "【乐器】拾取替换黑名单（英文逗号分隔的预制体名）：命中的物品永不被替换为乐器。\n" +
                    "默认排除诡异菇、三种乐器本体、信号棒与 5 件童军护符。");
                InstrumentIntervalSeconds = BindL(config, "乐队成员", "乐器-检测间隔", 30f,
                    "【乐器】每隔多少秒扫描一次自身 3 个物品栏中身背的乐器（秒）。默认 30。设为 0 关闭。");
                BugleExtraStamina = BindL(config, "乐队成员", "乐器-小号额外体力", 0.025f,
                    "【乐器】每检测到时身上有 1 个小号 Bugle 增加的额外体力（0.025 = 2.5 点）。默认 0.025。");
                ScoutmasterExtraStamina = BindL(config, "乐队成员", "乐器-号角额外体力", 0.05f,
                    "【乐器】每检测到时身上有 1 个童军领队的号角 Bugle_Scoutmaster Variant 增加的额外体力（0.05 = 5 点）。默认 0.05。");
                MagicExtraStamina = BindL(config, "乐队成员", "乐器-友谊喇叭额外体力", 0.05f,
                    "【乐器】每检测到时身上有 1 个友谊喇叭 Bugle_Magic 增加的额外体力（0.05 = 5 点）。默认 0.05。");

                // ---------------- 演奏 ----------------
                BlowRadius = BindL(config, "乐队成员", "演奏-单次半径", 15f,
                    "【演奏】单次吹响时作用半径（米，不含自身）。默认 15。");
                BlowExtraStamina = BindL(config, "乐队成员", "演奏-单次额外体力", 0.025f,
                    "【演奏】每次吹响为半径内其他玩家增加的额外体力（0.025 = 2.5 点）。默认 0.025。设为 0 关闭。");
                BlowCooldown = BindL(config, "乐队成员", "演奏-冷却", 1f,
                    "【演奏】小号单次吹响效果的冷却 / 去重窗口（秒）。默认 1。设为 0 关闭去重。");
                HornSelfExtraStamina = BindL(config, "乐队成员", "演奏-号角自身额外体力", 0.025f,
                    "【演奏】吹响「童军领队的号角 Bugle_Scoutmaster Variant」时为自身增加的额外体力（0.025 = 2.5 点）。默认 0.025。设为 0 关闭。");
                DrowsyEveryBlows = BindL(config, "乐队成员", "演奏-困倦每当吹响次数", 4,
                    "【演奏】每累计吹响多少次（小号 / 号角 / 友谊喇叭合计）为自身施加一次困倦。默认 4。设为 0 关闭。");
                DrowsyPerBlows = BindL(config, "乐队成员", "演奏-困倦量", 0.05f,
                    "【演奏】每累计吹响「演奏-困倦每当吹响次数」次为自身增加的困倦（0.05 = 5 点）。默认 0.05。");

                // ---------------- 乐队 ----------------
                GroupIntervalSeconds = BindL(config, "乐队成员", "乐队-间隔", 30f,
                    "【乐队】每隔多少秒按附近其他乐队成员数量结算一次（秒）。默认 30。设为 0 关闭。");
                GroupRadius = BindL(config, "乐队成员", "乐队-半径", 15f,
                    "【乐队】扫描附近其他乐队成员的距离（米）。默认 15。");
                GroupExtraStamina = BindL(config, "乐队成员", "乐队-额外体力", 0.05f,
                    "【乐队】每有 1 名附近的其他乐队成员增加的额外体力（0.05 = 5 点）。默认 0.05。");
                GroupClearDrowsy = BindL(config, "乐队成员", "乐队-清除困倦", 0.30f,
                    "【乐队】每有 1 名附近的其他乐队成员清除的困倦（0.30 = 30 点）。默认 0.30。");
                GroupRadiusBonus = BindL(config, "乐队成员", "乐队-半径加成", 5f,
                    "【乐队】每有 1 名附近的其他乐队成员为【演奏】半径增加的米数。默认 5。");
                GroupRadiusBonusCap = BindL(config, "乐队成员", "乐队-半径加成上限", 20f,
                    "【乐队】【演奏】半径加成的上限（米）。默认 20。");

                // ---------------- 转职（补充效果） ----------------
                TransferEnabled = BindL(config, "乐队成员", "转职-启用", true,
                    "是否启用【乐队】的补充效果「长期在乐队成员身边身背乐器的玩家有概率转职为乐队成员」。");
                TransferItems = BindL(config, "乐队成员", "转职-物品",
                    "Bugle,Bugle_Scoutmaster Variant,Bugle_Magic",
                    "【乐队】触发转职判定所需的「乐器」预制体名（英文逗号分隔）：\n" +
                    "玩家 3 个物品栏中至少有 1 格是其中任意一件，才算满足条件。\n" +
                    "默认小号 Bugle、童军领队的号角 Bugle_Scoutmaster Variant、友谊喇叭 Bugle_Magic。");
                TransferRadius = BindL(config, "乐队成员", "转职-半径", 5f,
                    "【乐队】判定「附近存在乐队成员」的距离（米）。默认 5。");
                TransferIntervalSeconds = BindL(config, "乐队成员", "转职-检测间隔", 30f,
                    "【乐队】补充效果的检测间隔（秒）。默认 30。设为 0 关闭本效果。");
                TransferStreakRequired = BindL(config, "乐队成员", "转职-连续次数", 2,
                    "【乐队】连续检测到多少次满足条件后才进入「概率转职判定」。\n" +
                    "默认 2（即需连续满足约 2 × 转职-检测间隔 秒），未连续满足时不做任何判定，以节省性能。");
                TransferChance = BindL(config, "乐队成员", "转职-概率", 0.10f,
                    "【乐队】满足条件后每「转职-检测间隔」秒判定一次、立即转职为乐队成员的概率（0.10 = 10%）。默认 0.10。\n" +
                    "该转职由被动效果触发，不消耗也不受「转职-次数」限制，并会绕过「最大人数」限制。");

                // ---------------- 体弱 ----------------
                FrailInitCurse = BindL(config, "乐队成员", "体弱-初始诅咒", 0.20f,
                    "【体弱】开局初始自带的诅咒值（0.20 = 20 点）。默认 0.20。设为 0 关闭。\n" +
                    "与领袖「宿命」等初始诅咒同套机制（绝对赋值 + 反复校正），中途加入者复活到位后幂等补齐。");
            }
        }

        /// <summary>领队·迈尔斯残魂（受诅残魂 / 众魂阻挠 / 封印之力 / 零号守则 / 封印解放）。</summary>
        public class MilesSoulConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 受诅残魂 ----
            public ConfigEntry<float> InitCurse { get; private set; }
            public ConfigEntry<bool> CurseProtectEnabled { get; private set; }
            public ConfigEntry<float> CurseCheckInterval { get; private set; }
            public ConfigEntry<bool> PandoraImmune { get; private set; }
            public ConfigEntry<bool> BeachOnly { get; private set; }

            // ---- 众魂阻挠 ----
            public ConfigEntry<float> CursePerLevel { get; private set; }
            public ConfigEntry<int> GemUsesPerScoutmaster { get; private set; }
            public ConfigEntry<float> ScoutmasterSpawnDistance { get; private set; }
            public ConfigEntry<float> ScoutmasterChaseTime { get; private set; }

            // ---- 封印之力 ----
            public ConfigEntry<bool> SealEnabled { get; private set; }
            public ConfigEntry<string> SpecialItems { get; private set; }
            public ConfigEntry<float> RemoteGemPetrifyThreshold { get; private set; }
            public ConfigEntry<float> RemoteGemCheckInterval { get; private set; }
            public ConfigEntry<float> RemoteGemUseCooldown { get; private set; }
            public ConfigEntry<float> HealingPetrify { get; private set; }
            public ConfigEntry<float> HealingHunger { get; private set; }
            public ConfigEntry<float> HealingFirstCurse { get; private set; }
            public ConfigEntry<float> SuperJumpPetrify { get; private set; }
            public ConfigEntry<float> SuperJumpHunger { get; private set; }
            public ConfigEntry<float> SuperJumpExtraStamina { get; private set; }
            public ConfigEntry<float> SuperJumpFirstCurse { get; private set; }
            public ConfigEntry<float> ClonePetrify { get; private set; }
            public ConfigEntry<float> CloneHunger { get; private set; }
            public ConfigEntry<float> CloneExtraStamina { get; private set; }
            public ConfigEntry<float> CloneFirstCurse { get; private set; }
            public ConfigEntry<float> AmbitionPetrify { get; private set; }
            public ConfigEntry<float> AmbitionHunger { get; private set; }
            public ConfigEntry<float> AmbitionExtraStamina { get; private set; }
            public ConfigEntry<float> AmbitionFirstCurse { get; private set; }
            public ConfigEntry<float> PowerCurse { get; private set; }

            // ---- 零号守则 ----
            public ConfigEntry<bool> RuleZeroHealAssist { get; private set; }
            public ConfigEntry<float> RuleZeroHealAssistExtraStamina { get; private set; }
            public ConfigEntry<string> RuleZeroInvincibleItem { get; private set; }
            public ConfigEntry<float> RuleZeroTeleportCooldown { get; private set; }
            public ConfigEntry<float> RuleZeroDeathInjury { get; private set; }
            public ConfigEntry<float> RuleZeroNearbyInterval { get; private set; }
            public ConfigEntry<float> RuleZeroNearbyRadius { get; private set; }
            public ConfigEntry<float> RuleZeroNearbyStamina { get; private set; }

            // ---- 封印解放 ----
            public ConfigEntry<bool> SealReleaseEnabled { get; private set; }
            public ConfigEntry<float> SealReleaseExtraStamina { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "残魂", "启用职业", true,
                    "是否启用「领队·迈尔斯残魂」职业。");
                MaxPlayers = BindL(config, "残魂", "最大人数", 1,
                    "本局最多分配多少名迈尔斯残魂。0 = 不限制。默认 1。");
                MinPlayers = BindL(config, "残魂", "最小人数", 0,
                    "本局至少分配多少名迈尔斯残魂（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "残魂", "抽取权重", 5,
                    "随机分配时「迈尔斯残魂」被抽中的权重。默认 5。");

                // ---------------- 受诅残魂 ----------------
                InitCurse = BindL(config, "残魂", "受诅残魂-初始诅咒", 0.15f,
                    "【受诅残魂】开局初始自带的诅咒值（0.15 = 15 点）。默认 0.15。设为 0 关闭。\n" +
                    "与领袖「宿命」等初始诅咒同套机制（绝对赋值 + 反复校正），中途加入者复活到位后幂等补齐。");
                CurseProtectEnabled = BindL(config, "残魂", "受诅残魂-保护启用", true,
                    "【受诅残魂】是否启用诅咒保护：除「自身职业效果」与「法师·魔法卷轴·净化」以外，\n" +
                    "任何方式降低自身诅咒都会被强制恢复到「最新记录值」。默认 true。");
                CurseCheckInterval = BindL(config, "残魂", "受诅残魂-检测间隔", 1f,
                    "【受诅残魂】每隔多少秒检测一次自身诅咒是否被外部降低（秒）。默认 1。");
                PandoraImmune = BindL(config, "残魂", "受诅残魂-免疫潘多拉", true,
                    "【受诅残魂】是否免疫潘多拉之子的【潘多拉】效果（不被施加潘多拉魔盒）。默认 true。\n" +
                    "与圣人【超然物外】共用潘多拉入口守卫。");
                BeachOnly = BindL(config, "残魂", "受诅残魂-仅海滩可成为", true,
                    "【受诅残魂】是否限制「只有第一关海滩（Shore 生物群系）才能成为本职业」：\n" +
                    "点燃海滩篝火进入第二关（雨林 / 森蕈等）之后，**新的中途加入玩家**与**在场玩家转职**都无法再成为残魂\n" +
                    "（已经分配到的残魂不受影响，只是不再新增）。默认 true。");

                // ---------------- 众魂阻挠 ----------------
                CursePerLevel = BindL(config, "残魂", "众魂阻挠-每层诅咒", 0.05f,
                    "【众魂阻挠】每次进入新关卡时自身增加的诅咒（0.05 = 5 点）。默认 0.05。\n" +
                    "进入「顶峰（Peak 生物群系）」与「天底（Void 生物群系）」时不再增加。");
                GemUsesPerScoutmaster = BindL(config, "残魂", "众魂阻挠-触发次数", 13,
                    "【众魂阻挠】每累计使用多少次童军宝石道具（毅力 / 进取 / 盛情 / 野心合计），\n" +
                    "在自身半径外生成 1 名童军领队。默认 13。设为 0 关闭。");
                ScoutmasterSpawnDistance = BindL(config, "残魂", "众魂阻挠-生成距离", 30f,
                    "【众魂阻挠】童军领队的生成距离：在自身「该距离之外」随机方向生成（米）。默认 30。");
                ScoutmasterChaseTime = BindL(config, "残魂", "众魂阻挠-追击时间", 30f,
                    "【众魂阻挠】童军领队生成后强制追击的时间（秒），超时后恢复原版 AI。默认 30。");

                // ---------------- 封印之力 ----------------
                SealEnabled = BindL(config, "残魂", "封印之力-启用", true,
                    "【封印之力】总开关。开启后除「特殊物品」外的任何物品被拾取都会直接消失。默认 true。");
                SpecialItems = BindL(config, "残魂", "封印之力-特殊物品",
                    "Mushroom Glow,Flare,Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor,Strange Gem,Backpack,Fannypack",
                    "【封印之力】特殊物品白名单（英文逗号分隔的预制体名）：这些物品仍可正常拾取。\n" +
                    "默认 = 诡异菇、信号棒、童军的毅力 / 进取 / 盛情 / 野心 / 荣耀、奇怪水晶、背包、滑稽背包。");
                RemoteGemPetrifyThreshold = BindL(config, "残魂", "封印之力-客机判定阈值", 2.5f,
                    "【封印之力】客机玩家「使用童军宝石道具」的判定阈值（点）。\n" +
                    "原因：童军宝石的直接使用是使用者客户端的**本地动作**、不广播 RPC，主机无法直接感知。\n" +
                    "因此对客机改用启发式：玩家**手执童军宝石道具**（`CharacterData.currentItem`）时，\n" +
                    "其石化值**一次性增加 ≥ 本阈值**即视为「使用了一次该宝石」。默认 2.5：\n" +
                    "熔炉 / 雾沼特殊石头每接触一次恰好 +2.5（不触发），而宝石每次使用的石化增量大于 2.5（触发）。\n" +
                    "设为 0 或负数关闭该客机判定通道（此时客机使用宝石将不被感知）。");
                RemoteGemCheckInterval = BindL(config, "残魂", "封印之力-客机判定间隔", 0.5f,
                    "【封印之力】客机「手执宝石 + 石化增加」启发式判定的轮询间隔（秒）。默认 0.5。");
                RemoteGemUseCooldown = BindL(config, "残魂", "封印之力-客机判定冷却", 1f,
                    "【封印之力】客机启发式判定命中后的冷却（秒）：同一玩家的**同一件**宝石在此冷却内不重复结算，\n" +
                    "避免「石化持续增加」型宝石被反复触发。冷却按「玩家 + 宝石」分别计时，换用另一件宝石不受影响。默认 1。");
                HealingPetrify = BindL(config, "残魂", "毅力-石化", 0.10f,
                    "【童军的毅力 Amulet_Healing】每次使用后立即降低的石化（0.10 = 10 点）。默认 0.10。");
                HealingHunger = BindL(config, "残魂", "毅力-饥饿", 0.10f,
                    "【童军的毅力】每次使用后立即降低的饥饿（0.10 = 10 点）。默认 0.10。");
                HealingFirstCurse = BindL(config, "残魂", "毅力-首拾诅咒", 0.05f,
                    "【童军的毅力】本局第一次拾取时降低的诅咒（0.05 = 5 点）。默认 0.05。");
                SuperJumpPetrify = BindL(config, "残魂", "进取-石化", 0.05f,
                    "【童军的进取 Amulet_SuperJump】每次使用后立即降低的石化（0.05 = 5 点）。默认 0.05。");
                SuperJumpHunger = BindL(config, "残魂", "进取-饥饿", 0.05f,
                    "【童军的进取】每次使用后立即降低的饥饿（0.05 = 5 点）。默认 0.05。");
                SuperJumpExtraStamina = BindL(config, "残魂", "进取-额外体力", 0.05f,
                    "【童军的进取】每次使用后立即增加的额外体力（0.05 = 5 点）。默认 0.05。设为 0 关闭。");
                SuperJumpFirstCurse = BindL(config, "残魂", "进取-首拾诅咒", 0.05f,
                    "【童军的进取】本局第一次拾取时降低的诅咒（0.05 = 5 点）。默认 0.05。");
                ClonePetrify = BindL(config, "残魂", "盛情-石化", 0.20f,
                    "【童军的盛情 Amulet_Clone】每次「使用且成功复制了物品」后立即降低的石化（0.20 = 20 点）。默认 0.20。");
                CloneHunger = BindL(config, "残魂", "盛情-饥饿", 0.10f,
                    "【童军的盛情】每次「使用且成功复制了物品」后立即降低的饥饿（0.10 = 10 点）。默认 0.10。");
                CloneExtraStamina = BindL(config, "残魂", "盛情-额外体力", 0.20f,
                    "【童军的盛情】每次「使用且成功复制了物品」后立即增加的额外体力（0.20 = 20 点）。默认 0.20。设为 0 关闭。");
                CloneFirstCurse = BindL(config, "残魂", "盛情-首拾诅咒", 0.05f,
                    "【童军的盛情】本局第一次拾取时降低的诅咒（0.05 = 5 点）。默认 0.05。");
                AmbitionPetrify = BindL(config, "残魂", "野心-石化", 0.25f,
                    "【童军的野心 Amulet_InfiniteStamina】每次使用后立即降低的石化（0.25 = 25 点）。默认 0.25。");
                AmbitionHunger = BindL(config, "残魂", "野心-饥饿", 0.10f,
                    "【童军的野心】每次使用后立即降低的饥饿（0.10 = 10 点）。默认 0.10。");
                AmbitionExtraStamina = BindL(config, "残魂", "野心-额外体力", 0.25f,
                    "【童军的野心】每次使用后立即增加的额外体力（0.25 = 25 点）。默认 0.25。");
                AmbitionFirstCurse = BindL(config, "残魂", "野心-首拾诅咒", 0.05f,
                    "【童军的野心】本局第一次拾取时降低的诅咒（0.05 = 5 点）。默认 0.05。");
                PowerCurse = BindL(config, "残魂", "童军力量-诅咒", 0.05f,
                    "【童军的力量】四种童军宝石道具都拾取过后，额外降低的诅咒（0.05 = 5 点），\n" +
                    "并清除「诅咒以外」的所有负面状态（包括石化）。默认 0.05。");

                // ---------------- 零号守则 ----------------
                RuleZeroHealAssist = BindL(config, "残魂", "零号守则-毅力助人", true,
                    "【零号守则】**把童军的毅力喂给「昏迷（倒地未死亡）」的其他玩家**时：\n" +
                    "本次使用给自身追加的石化**等量清除**（净变化为 0），且该次使用不计入【众魂阻挠】的次数累计。\n" +
                    "注意区分三种情形（只有毅力分情形）：自用 = 封印之力（−10 石化 / −10 饥饿）；\n" +
                    "喂给未昏迷的其他玩家 = 无任何职业效果（原版涨多少石化就是多少，仍计入次数）；喂给昏迷玩家 = 本项。默认 true。");
                RuleZeroHealAssistExtraStamina = BindL(config, "残魂", "零号守则-额外体力", 0.30f,
                    "【零号守则】把毅力喂给「昏迷（倒地未死亡）」玩家时，除了等量清除本次追加的石化，\n" +
                    "还为自己增加的额外体力（0.30 = 30 点）。默认 0.30。设为 0 关闭。");
                RuleZeroInvincibleItem = BindL(config, "残魂", "零号守则-无敌物品", "FortifiedMilk",
                    "【零号守则】有人进入昏迷时为自己施加的无敌效果物品（默认奶白金 FortifiedMilk）。\n" +
                    "留空则不施加无敌效果（仅传送）。");
                RuleZeroTeleportCooldown = BindL(config, "残魂", "零号守则-传送冷却", 10f,
                    "【零号守则】「昏迷 → 无敌 + 传送到昏迷者处」的触发冷却（秒）。默认 10。设为 0 关闭冷却。");
                RuleZeroDeathInjury = BindL(config, "残魂", "零号守则-死亡损伤", 0.10f,
                    "【零号守则】每有 1 名玩家死亡时，自身增加的损伤（0.10 = 10 点）。默认 0.10。");
                RuleZeroNearbyInterval = BindL(config, "残魂", "零号守则-附近玩家-间隔", 30f,
                    "【零号守则】每隔多少秒检测一次自身周围「半径」内的人数（秒）。默认 30。设为 0 关闭。");
                RuleZeroNearbyRadius = BindL(config, "残魂", "零号守则-附近玩家-半径", 30f,
                    "【零号守则】上述检测的半径（米）。默认 30。");
                RuleZeroNearbyStamina = BindL(config, "残魂", "零号守则-附近玩家-额外体力", 0.05f,
                    "【零号守则】检测半径内**每有一名玩家**（**不包括自己**）为自身增加的额外体力（0.05 = 5 点）。默认 0.05。\n" +
                    "例：半径内有 4 人 → 一次 +0.20（20 点）。");

                // ---------------- 封印解放 ----------------
                SealReleaseEnabled = BindL(config, "残魂", "封印解放-启用", true,
                    "【封印解放】是否启用：在天底解放童军领队的灵魂后，清除在场所有玩家的全部负面状态\n" +
                    "（含诅咒与石化）并增加额外体力。默认 true。");
                SealReleaseExtraStamina = BindL(config, "残魂", "封印解放-额外体力", 1f,
                    "【封印解放】解放时给予在场每名玩家的额外体力（1 = 100 点）。默认 1。");
            }
        }

        /// <summary>
        /// 区（Qu）：【！？区区？！】（攀爬湿滑 + 死亡产虫）/【分解者】/【区块链】/【好多区】/【区毒不食子】。
        ///
        /// 说明：本职业不含音效效果（PEAK 音效完全本地、不经网络传输音频，本模组又只装在主机上
        /// ⇒ 客机侧不可能替换/播放，收益过低）。攀爬的「速度减慢」同样无法实现
        /// （`climbMod` 的倍率在攀爬者本机按射线命中表面结算，主机改不了），
        /// 故以「周期性强制中断攀爬（跳舞 / 脱手）」等效实现「体力消耗增加」。
        /// </summary>
        public class QuConfig
        {
            // ---- 基础 ----
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---- 【！？区区？！】攀爬湿滑 ----
            public ConfigEntry<bool> ClimbEnabled { get; private set; }
            public ConfigEntry<float> ClimbInterval { get; private set; }
            public ConfigEntry<bool> ClimbIgnoreAids { get; private set; }

            // ---- 【！？区区？！】死亡产虫 ----
            public ConfigEntry<bool> DeathWormEnabled { get; private set; }

            // ---- 【分解者】 ----
            public ConfigEntry<bool> DecomposerEnabled { get; private set; }
            public ConfigEntry<float> DecomposerHunger { get; private set; }
            public ConfigEntry<string> DecomposerItems { get; private set; }

            // ---- 【区块链】 ----
            public ConfigEntry<bool> BlockchainEnabled { get; private set; }
            public ConfigEntry<float> BlockchainInterval { get; private set; }
            public ConfigEntry<float> BlockchainSelfSporeClear { get; private set; }
            public ConfigEntry<float> BlockchainAllySpore { get; private set; }
            public ConfigEntry<float> BlockchainRadius { get; private set; }
            public ConfigEntry<float> BlockchainFeedSpore { get; private set; }
            public ConfigEntry<bool> BlockchainConvertEnabled { get; private set; }
            public ConfigEntry<float> BlockchainConvertThreshold { get; private set; }
            public ConfigEntry<bool> BlockchainRewardWorm { get; private set; }

            // ---- 【好多区】 ----
            public ConfigEntry<bool> ManyQuEnabled { get; private set; }
            public ConfigEntry<int> ManyQuTriggerCount { get; private set; }
            public ConfigEntry<float> ManyQuInterval { get; private set; }
            public ConfigEntry<float> ManyQuHunger { get; private set; }
            public ConfigEntry<float> ManyQuStamina { get; private set; }
            public ConfigEntry<float> ManyQuQuRadius { get; private set; }
            public ConfigEntry<float> ManyQuThresholdRadius { get; private set; }
            public ConfigEntry<int> ManyQuMaxStacks { get; private set; }
            public ConfigEntry<int> ManyQuSpeedWormCount { get; private set; }
            public ConfigEntry<int> ManyQuSpeedQuCount { get; private set; }
            public ConfigEntry<int> ManyQuInfiniteQuCount { get; private set; }

            // ---- 【区毒不食子】 ----
            public ConfigEntry<bool> ToxinEnabled { get; private set; }
            public ConfigEntry<float> ToxinCurse { get; private set; }

            public void Initialize(ConfigFile config)
            {
                // ---------------- 基础 ----------------
                Enabled = BindL(config, "区", "启用职业", true,
                    "是否启用「区」职业。");
                MaxPlayers = BindL(config, "区", "最大人数", 4,
                    "本局最多分配多少名区。0 = 不限制。默认 4。\n" +
                    "【好多区】按「自身半径内（见『好多区-阈值半径』）区的存活人数（**含自己**）」（≥3 / ≥4）结算加成，\n" +
                    "且【区块链】会把孢子过量的玩家强制转职成区，需要邻近能凑出多名区，故默认给到 4。");
                MinPlayers = BindL(config, "区", "最小人数", 0,
                    "本局至少分配多少名区（用于随机分配时优先补齐）。默认 0。");
                Weight = BindL(config, "区", "抽取权重", 10,
                    "随机分配时「区」被抽中的权重。默认 10。");

                // ---------------- 【！？区区？！】攀爬湿滑 ----------------
                ClimbEnabled = BindL(config, "区", "攀爬-启用", true,
                    "【！？区区？！】是否启用「攀爬时固定有雨林下雨湿滑」效果。默认 true。\n" +
                    "原版「攀爬速度」与「攀爬体力消耗倍率」都在**攀爬者自己的客户端**上结算（`data.climbMod` 不网络同步、\n" +
                    "每帧被属主按本机射线覆盖），主机**无法改写** → 「速度减慢」**已放弃实现**。\n" +
                    "现改为「周期性强制脱手」（见下两项），由「必须重新抓墙、把同一段高度再爬一次」等效实现「体力消耗增加」。");
                ClimbInterval = BindL(config, "区", "攀爬-间隔", 0.5f,
                    "【！？区区？！】攀爬期间，每隔多少秒强制中断一次攀爬（秒）。默认 0.5。设为 0 或负数关闭。\n" +
                    "中断后角色会脱手（掉落 / 需重新抓墙恢复攀爬），从而**等效额外消耗体力**。\n" +
                    "计时只在「正在攀爬」时累计：一旦被中断（`isClimbing` 变假）即清零，故不会在脱手后空转，\n" +
                    "而是等玩家重新抓墙并再爬满该间隔后才会再次触发。");
                ClimbIgnoreAids = BindL(config, "区", "攀爬-忽略攀爬物", true,
                    "【！？区区？！】攀爬时若**借助已放置/已挂载的攀爬物**（藤蔓 / 绳索 / 反重绳索 / 锁链 / 岩钉），是否**不触发**强制脱手。默认 true。\n" +
                    "true = 只有**自然岩壁**（原版 `isClimbing`）才会「湿滑」脱手；\n" +
                    "      借助上述攀爬物（绳索 / 反重绳索 / 锁链 → `isRopeClimbing`；藤蔓 → `isVineClimbing`；\n" +
                    "      岩钉锤入后变成的攀爬钉 / 镐 → 悬挂在 `ClimbHandle`）时**不脱手**。\n" +
                    "false = 任何攀爬（自然岩壁 / 绳索 / 藤蔓）都会脱手。");

                // ---------------- 【！？区区？！】死亡产虫 ----------------
                DeathWormEnabled = BindL(config, "区", "死亡生成虫儿-启用", true,
                    "【！？区区？！】区真正死亡时，是否在尸体处生成一只**烤熟的**早起虫儿。默认 true。");

                // ---------------- 【分解者】 ----------------
                DecomposerEnabled = BindL(config, "区", "分解者-启用", true,
                    "【分解者】是否启用：拾取下方物品池中的物品时，物品直接消失并使自身饥饿下降。默认 true。");
                DecomposerHunger = BindL(config, "区", "分解者-饥饿", 0.05f,
                    "【分解者】每次触发时自身**减少**的饥饿（0.05 = 5 点，负向下发）。默认 0.05。");
                DecomposerItems = BindL(config, "区", "分解者-物品池",
                    "Berrynana Peel Pink Variant,Berrynana Peel Blue Variant,Berrynana Peel Brown Variant," +
                    "Berrynana Peel Yellow,FireWood,Torch,Mushroom Lace Poison,Mushroom Cluster Poison," +
                    "Mushroom Normie Poison,BounceShroom,ShelfShroom,CloudFungus,WarpFungus,HealingPuffShroom," +
                    "CactusBall,MagicBean,Guidebook,GuidebookPageScroll Variant,GuidebookPage," +
                    "BookOfBones",
                    "【分解者】会把拾取物直接分解掉的物品清单（英文逗号分隔的预制体名）。默认 = \n" +
                    "四种香蕉皮、木棍、火把、三种有毒蘑菇（喇叭菇 / 银针菇 / 馒头菇的有毒变种）、\n" +
                    "弹力菇、踏板菇、云雾菇、曲迁菇、灵药菇、仙人球、魔豆、旅行指南、卷轴、骸骨之书。\n" +
                    "⚠ 撕下的书页（含卷轴使用后生成的纸页）**不需要**写进本清单：它们的预制体名统一以\n" +
                    "`GuidebookPage` 开头（实测为 `GuidebookPage_0_Intro` / `GuidebookPage_3_Revival` /\n" +
                    "`GuidebookPage_10_Sleepy` / `GuidebookPage_7_BurningSun` …，各版本的编号与名称会变），\n" +
                    "代码里按该前缀统一命中，因此本清单里的 `GuidebookPage` / `GuidebookPageScroll Variant` 仅为\n" +
                    "文档说明用途。写错或未实装的名称会被自动跳过。");

                // ---------------- 【区块链】 ----------------
                BlockchainEnabled = BindL(config, "区", "区块链-启用", true,
                    "【区块链】是否启用。默认 true。");
                BlockchainInterval = BindL(config, "区", "区块链-间隔", 7f,
                    "【区块链】每隔多少秒结算一次「自身清除孢子 + 半径内其他玩家增加孢子 + 传染判定」（秒）。默认 7。");
                BlockchainSelfSporeClear = BindL(config, "区", "区块链-自身清除孢子", 1f,
                    "【区块链】每次结算为**自身**清除的孢子量（1 = 100 点，足以清空）。默认 1。设为 0 关闭。");
                BlockchainAllySpore = BindL(config, "区", "区块链-同伴孢子", 0.025f,
                    "【区块链】每次结算为半径内「除区以外」的所有玩家增加的孢子（0.025 = 2.5 点）。默认 0.025。\n" +
                    "**多名区不叠加**：只要玩家 10 米内有任意存活区就加一次，而不是按区数量反复叠加。");
                BlockchainRadius = BindL(config, "区", "区块链-作用半径", 10f,
                    "【区块链】作用半径（米）：半径内的其他玩家获得孢子，半径内孢子过量的玩家会被强制转职成区。默认 10。");
                BlockchainFeedSpore = BindL(config, "区", "区块链-喂食孢子", 0.15f,
                    "【区块链】区每喂其他玩家食用 1 只早起虫儿（无论是否烤熟），额外使被喂玩家增加的孢子（0.15 = 15 点）。默认 0.15。");
                BlockchainConvertEnabled = BindL(config, "区", "区块链-传染启用", true,
                    "【区块链】孢子值达到阈值且位于半径内的玩家是否会被**强制转职成区**。默认 true。");
                BlockchainConvertThreshold = BindL(config, "区", "区块链-传染阈值", 0.5f,
                    "【区块链】传染所需的孢子阈值（0.5 = 50 点）。默认 0.5。");
                BlockchainRewardWorm = BindL(config, "区", "区块链-奖励虫儿", true,
                    "【区块链】每次成功把其他玩家传染成区时，是否让**场上所有区**各获得 1 只早起虫儿。默认 true。");

                // ---------------- 【好多区】 ----------------
                ManyQuEnabled = BindL(config, "区", "好多区-启用", true,
                    "【好多区】是否启用。默认 true。");
                ManyQuTriggerCount = BindL(config, "区", "好多区-触发次数", 5,
                    "【好多区】自身每触发多少次【分解者】，就获得 1 只早起虫儿（各玩家独立计数）。默认 5。设为 0 关闭。");
                ManyQuInterval = BindL(config, "区", "好多区-间隔", 60f,
                    "【好多区】每隔多少秒结算一次「虫儿 / 区 数量」加成（秒）。默认 60。");
                ManyQuHunger = BindL(config, "区", "好多区-减饥饿", 0.025f,
                    "【好多区】每 1 个「虫儿或附近区」为自身减少的饥饿（0.025 = 2.5 点）。默认 0.025。");
                ManyQuStamina = BindL(config, "区", "好多区-额外体力", 0.025f,
                    "【好多区】每 1 个「虫儿或附近区」为自身增加的额外体力（0.025 = 2.5 点）。默认 0.025。");
                ManyQuQuRadius = BindL(config, "区", "好多区-区半径", 30f,
                    "【好多区】「自身半径多少米内的区」也计入加成（米）。默认 30。");
                ManyQuThresholdRadius = BindL(config, "区", "好多区-阈值半径", 20f,
                    "【好多区】「加速 / 无限体力」阈值判定时，统计「自身半径多少米内存活的区人数（**含自己**）」所用的半径（米）。默认 20。\n" +
                    "（与上面「好多区-区半径」30m 是两套半径：后者用于计算层数，本项用于判定加速 / 无限体力。）");
                ManyQuMaxStacks = BindL(config, "区", "好多区-最大层数", 4,
                    "【好多区】加成最多叠加多少层。默认 4。\n" +
                    "层数 = 物品栏中的早起虫儿数 + 自身半径内（见「好多区-区半径」）的区数（不含自己），\n" +
                    "超过本值后按本值封顶（每层 = 「好多区-减饥饿」的饥饿 + 「好多区-额外体力」的额外体力）。\n" +
                    "设为 0 或负数 = 不限制层数。");
                ManyQuSpeedWormCount = BindL(config, "区", "好多区-加速虫儿数", 3,
                    "【好多区】物品栏中早起虫儿数量达到该值时，额外获得一次**加速**效果。默认 3。");
                ManyQuSpeedQuCount = BindL(config, "区", "好多区-加速区数", 3,
                    "【好多区】「自身『好多区-阈值半径』内存活的区人数（含自己）」达到该值时，额外获得一次**加速**效果。默认 3。");
                ManyQuInfiniteQuCount = BindL(config, "区", "好多区-无限体力区数", 4,
                    "【好多区】「自身『好多区-阈值半径』内存活的区人数（含自己）」达到该值时，再额外获得一次**无限体力**效果。默认 4。");

                // ---------------- 【区毒不食子】 ----------------
                ToxinEnabled = BindL(config, "区", "区毒不食子-启用", true,
                    "【区毒不食子】是否启用。默认 true。");
                ToxinCurse = BindL(config, "区", "区毒不食子-诅咒", 0.10f,
                    "【区毒不食子】区每食用 1 只早起虫儿（无论是否烤熟），自身增加的诅咒（0.10 = 10 点）。默认 0.10。");
            }
        }

        // ===============================================================
        // 太空人（Astronaut）
        // ===============================================================
        public class AstronautConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            public ConfigEntry<int> FloatInitialBalloons { get; private set; }
            public ConfigEntry<float> FloatIntervalSeconds { get; private set; }
            public ConfigEntry<int> FloatBalloonsPerTick { get; private set; }
            public ConfigEntry<bool> FloatPickupDisappear { get; private set; }

            public ConfigEntry<bool> FlyEnabled { get; private set; }
            public ConfigEntry<float> FlyRocketpackChance { get; private set; }
            public ConfigEntry<float> FlyJetpackChance { get; private set; }
            public ConfigEntry<bool> FlyIncludeFood { get; private set; }
            public ConfigEntry<string> FlyBlacklist { get; private set; }

            public ConfigEntry<float> FoodSafetyPoison { get; private set; }
            public ConfigEntry<string> FoodSafetyBlacklist { get; private set; }

            public ConfigEntry<float> NasaIntervalSeconds { get; private set; }
            public ConfigEntry<float> NasaHotReduce { get; private set; }
            public ConfigEntry<float> NasaColdReduce { get; private set; }
            public ConfigEntry<float> NasaExtraBonus { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "太空人", "启用职业", true, "是否启用太空人职业。");
                MaxPlayers = BindL(config, "太空人", "最大人数", 1,
                    "太空人最多分配给几名玩家。设为 0 表示不限制。");
                MinPlayers = BindL(config, "太空人", "最小人数", 0,
                    "太空人最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "太空人", "抽取权重", 10, "随机分配时太空人被抽中的权重。");

                // ---------------- 【轻飘飘】 ----------------
                FloatInitialBalloons = BindL(config, "太空人", "轻飘飘-初始气球数", 2,
                    "【轻飘飘】开局初始为太空人绑上的气球个数。默认 2。设为 0 关闭初始气球。");
                FloatIntervalSeconds = BindL(config, "太空人", "轻飘飘-间隔", 120f,
                    "【轻飘飘】每隔多少秒再次为该太空人绑上气球（秒）。默认 120（2 分钟）。设为 0 关闭周期绑气球。");
                FloatBalloonsPerTick = BindL(config, "太空人", "轻飘飘-每次气球数", 2,
                    "【轻飘飘】每到「轻飘飘-间隔」时为该太空人绑上的气球个数。默认 2。");
                FloatPickupDisappear = BindL(config, "太空人", "轻飘飘-拾取气球消失", true,
                    "【轻飘飘】太空人拾取「气球 Balloon」或「一束气球 BalloonBunch」时，物品是否直接消失（不进物品栏）。\n" +
                    "默认 true。设为 false 则按原版正常拾取。");

                // ---------------- 【飞起来】 ----------------
                FlyEnabled = BindL(config, "太空人", "飞起来-启用", true,
                    "【飞起来】是否启用。默认 true。");
                FlyRocketpackChance = BindL(config, "太空人", "飞起来-火箭背包概率", 0.20f,
                    "【飞起来】拾取物品时变为火箭背包 Rocketpack 的概率（0.20 = 20%）。\n" +
                    "概率判定成功时：拾取的物品直接消失，并在脚下生成 1 个火箭背包。默认 0.20。设为 0 关闭该分支。");
                FlyJetpackChance = BindL(config, "太空人", "飞起来-喷气背包概率", 0.10f,
                    "【飞起来】拾取物品时变为喷气背包 Jetpack 的概率（0.10 = 10%）。\n" +
                    "先判定火箭背包分支，未命中时再判定本分支。命中时拾取物品消失并在脚下生成喷气背包。默认 0.10。设为 0 关闭该分支。");
                FlyIncludeFood = BindL(config, "太空人", "飞起来-对食物物品也生效", false,
                    "【飞起来】是否让「食物物品」也参与概率变化。默认 false。\n" +
                    "需求口径：拾取「非食物物品（且不在特殊物品黑名单内）」的物品才有概率变为火箭背包 / 喷气背包；\n" +
                    "拾取**食物物品**与**特殊物品黑名单**内的物品时必定保持不变、**不做概率判定**。\n" +
                    "食物物品 = 带 Berry（浆果）/ Mushroom（蘑菇）/ PackagedFood（包装食物）标签，或 CookedAmount ≥ 1（烤过）的物品。\n" +
                    "仅当你希望「连食物也可能变成背包」时才改为 true（会偏离需求口径）。");
                FlyBlacklist = BindL(config, "太空人", "飞起来-特殊物品黑名单",
                    "Mushroom Glow,Rocketpack,Jetpack,Flare,Strange Gem," +
                    "Amulet_Healing,Amulet_SuperJump,Amulet_Clone,Amulet_InfiniteStamina,ScoutsHonor",
                    "【飞起来】不受影响（不会被变成背包）的特殊物品预制体名，英文逗号分隔。\n" +
                    "默认：诡异菇 Mushroom Glow、火箭背包 Rocketpack、喷气背包 Jetpack、信号棒 Flare、奇怪水晶 Strange Gem、" +
                    "童军的毅力/进取/盛情/野心（四种护符）、童军的荣耀 ScoutsHonor。");

                // ---------------- 【太空食品安全】 ----------------
                FoodSafetyPoison = BindL(config, "太空人", "太空食品安全-中毒", 0.10f,
                    "【太空食品安全】太空人每次食用「不符合条件的食物」时增加的中毒值（0.10 = 10 点）。\n" +
                    "符合条件 = 包装食物（PackagedFood 标签）或烤过的食物（CookedAmount ≥ 1）。\n" +
                    "黑名单内的物品豁免、不触发中毒（不包括诡异菇、万灵药）。默认 0.10。设为 0 关闭。");
                FoodSafetyBlacklist = BindL(config, "太空人", "太空食品安全-黑名单", "Mushroom Glow,Cure-All",
                    "【太空食品安全】食用这些物品**不触发**中毒（预制体名，英文逗号分隔）。\n" +
                    "默认 Mushroom Glow（诡异菇）/ Cure-All（万灵药）—— 即「不包括诡异菇、万灵药」。\n" +
                    "留空表示不豁免任何物品（此时诡异菇、万灵药也会触发中毒）。");

                // ---------------- 【我去，老美的NASA这么厉害】 ----------------
                NasaIntervalSeconds = BindL(config, "太空人", "NASA-间隔", 5f,
                    "【我去，老美的NASA这么厉害】每隔多少秒结算一次「炎热 / 寒冷」削减（秒）。默认 5。");
                NasaHotReduce = BindL(config, "太空人", "NASA-炎热", 0.05f,
                    "【NASA】每次为该太空人减少的炎热（Hot）值（0.05 = 5 点）。默认 0.05。设为 0 关闭。");
                NasaColdReduce = BindL(config, "太空人", "NASA-寒冷", 0.05f,
                    "【NASA】每次为该太空人减少的寒冷（Cold）值（0.05 = 5 点）。默认 0.05。设为 0 关闭。");
                NasaExtraBonus = BindL(config, "太空人", "NASA-额外增加", 0.025f,
                    "【NASA】每次该太空人获得（增加）蛛网 / 捕蝇草 / 诅咒时，额外追加的同种状态值（0.025 = 2.5 点）。\n" +
                    "即：诅咒每增加 X 点，会额外再 +2.5 诅咒；蛛网 / 捕蝇草同理。默认 0.025。设为 0 关闭。");
            }
        }

        // ===============================================================
        // 榔王（Langwang）
        // ===============================================================
        public class LangwangConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            public ConfigEntry<float> BerryIntervalSeconds { get; private set; }
            public ConfigEntry<string> GreenBerryItem { get; private set; }
            public ConfigEntry<int> GreenBerryWeight { get; private set; }
            public ConfigEntry<string> YellowBerryItem { get; private set; }
            public ConfigEntry<int> YellowBerryWeight { get; private set; }
            public ConfigEntry<string> BerryItems { get; private set; }

            public ConfigEntry<float> AddictionDrowsyPerEat { get; private set; }
            public ConfigEntry<float> AddictionNoBerryInterval { get; private set; }
            public ConfigEntry<float> AddictionTickInterval { get; private set; }
            public ConfigEntry<float> AddictionDrowsyPerTick { get; private set; }

            public ConfigEntry<float> FibrosisInjuryPerEat { get; private set; }
            public ConfigEntry<string> FibrosisBlacklist { get; private set; }

            public ConfigEntry<bool> ConvertEnabled { get; private set; }
            public ConfigEntry<int> ConvertStreak { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "榔王", "启用职业", true, "是否启用榔王职业。");
                MaxPlayers = BindL(config, "榔王", "最大人数", 10,
                    "榔王最多分配给几名玩家。设为 0 表示不限制。默认 10。\n" +
                    "（榔王能通过【没吃的赶紧戒】把其他玩家强制转职为榔王，属于「可传染」职业，\n" +
                    "故默认人数与待皓 / 区 同类放宽到 10，而不是 1。）\n" +
                    "注：【没吃的赶紧戒】的强制转职不受本上限限制。");
                MinPlayers = BindL(config, "榔王", "最小人数", 0,
                    "榔王最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "榔王", "抽取权重", 5,
                    "随机分配时榔王被抽中的权重。默认 5（可传染职业，与待皓 / 区 同档，低于普通职业的 10）。");

                // ---------------- 【精品大果】 ----------------
                BerryIntervalSeconds = BindL(config, "榔王", "精品大果-发放间隔", 100f,
                    "【精品大果】每隔多少秒给每名榔王发放 1 个荔莓（秒）。默认 100。\n" +
                    "优先放入 3 个物品栏空位；物品栏已满则在脚下生成。设为 0 关闭发放。");
                GreenBerryItem = BindL(config, "榔王", "精品大果-青荔莓物品", "Kingberry Green",
                    "【精品大果】「青荔莓」的物品预制体名。默认 Kingberry Green（绿王莓）。\n" +
                    "食用后榔王获得一次加速效果。");
                GreenBerryWeight = BindL(config, "榔王", "精品大果-青荔莓权重", 10,
                    "【精品大果】抽中「青荔莓」的权重。默认 10。");
                YellowBerryItem = BindL(config, "榔王", "精品大果-黄荔莓物品", "Kingberry Yellow",
                    "【精品大果】「黄荔莓」的物品预制体名。默认 Kingberry Yellow（黄王莓）。\n" +
                    "食用后榔王获得一次无限体力效果。");
                YellowBerryWeight = BindL(config, "榔王", "精品大果-黄荔莓权重", 5,
                    "【精品大果】抽中「黄荔莓」的权重。默认 5。");
                BerryItems = BindL(config, "榔王", "荔莓物品", "Kingberry Green,Kingberry Yellow,Kingberry Purple",
                    "【上瘾】【没吃的赶紧戒】中视为「荔莓」的物品预制体名，英文逗号分隔。\n" +
                    "默认：绿王莓 / 黄王莓 / 紫王莓（王莓系列）。\n" +
                    "若与实际游戏内「荔莓」称呼不符，请把真实预制体名填入此处（含青 / 黄荔莓）。");

                // ---------------- 【上瘾】 ----------------
                AddictionDrowsyPerEat = BindL(config, "榔王", "上瘾-每次困倦", 0.30f,
                    "【上瘾】榔王每次食用「非荔莓」食物时增加的困倦值（0.30 = 30 点）。默认 0.30。设为 0 关闭。");
                AddictionNoBerryInterval = BindL(config, "榔王", "上瘾-未食用间隔", 300f,
                    "【上瘾】距上次食用荔莓超过多少秒后开始持续增加困倦（秒）。默认 300。");
                AddictionTickInterval = BindL(config, "榔王", "上瘾-检查间隔", 5f,
                    "【上瘾】超时后每隔多少秒结算一次困倦（秒）。默认 5。");
                AddictionDrowsyPerTick = BindL(config, "榔王", "上瘾-困倦", 0.05f,
                    "【上瘾】超时后每次结算增加的困倦值（0.05 = 5 点）。默认 0.05。设为 0 关闭。");

                // ---------------- 【纤维化】 ----------------
                FibrosisInjuryPerEat = BindL(config, "榔王", "纤维化-损伤", 0.05f,
                    "【纤维化】榔王每次食用任意物品时增加的损伤值（0.05 = 5 点）。默认 0.05。设为 0 关闭。\n" +
                    "（诡异菇、能量饮料、奶白金、大棒棒糖除外）");
                FibrosisBlacklist = BindL(config, "榔王", "纤维化-黑名单",
                    "Mushroom Glow,Cure-All,Energy Drink,FortifiedMilk,Lollipop",
                    "【纤维化】食用这些物品时**不触发**加损伤（预制体名，英文逗号分隔）。\n" +
                    "默认：诡异菇 Mushroom Glow（转职触发物品）、万灵药 Cure-All、能量饮料 Energy Drink、" +
                    "奶白金 FortifiedMilk、大棒棒糖 Lollipop。\n" +
                    "⚠ 其中「万灵药 Cure-All」也在黑名单内（需求列举的 5 项之一），故实际豁免 5 项；" +
                    "效果描述里的括注按需求原文只写了 4 项。留空表示不设黑名单。");

                // ---------------- 【没吃的赶紧戒】 ----------------
                ConvertEnabled = BindL(config, "榔王", "没吃的赶紧戒-启用", true,
                    "【没吃的赶紧戒】本局存在榔王时，其他玩家连续食用荔莓超过指定次数会被强制转职为榔王。默认 true。");
                ConvertStreak = BindL(config, "榔王", "没吃的赶紧戒-连续次数", 3,
                    "【没吃的赶紧戒】连续食用荔莓的次数「大于」该值时触发强制转职（连续次数 > 本值）。默认 3（即连续第 4 次起触发）。\n" +
                    "“连续”指期间没有食用非荔莓食物；玩家各局独立计数。设为 0 或负数关闭。");
            }
        }

        // ===============================================================
        // 程序员（Programmer）：【BUG】/【溢出】/【熬夜】/【开挂】
        // ===============================================================
        public class ProgrammerConfig
        {
            public ConfigEntry<bool> Enabled { get; private set; }
            public ConfigEntry<int> MaxPlayers { get; private set; }
            public ConfigEntry<int> MinPlayers { get; private set; }
            public ConfigEntry<int> Weight { get; private set; }

            // ---------------- 【BUG】：每 N 秒获得一个随机昆虫 ----------------
            public ConfigEntry<float> BugIntervalSeconds { get; private set; }
            public ConfigEntry<string> BugTickItem { get; private set; }
            public ConfigEntry<int> BugTickWeight { get; private set; }
            public ConfigEntry<string> BugScorpionItem { get; private set; }
            public ConfigEntry<int> BugScorpionWeight { get; private set; }
            public ConfigEntry<string> BugWormItem { get; private set; }
            public ConfigEntry<int> BugWormWeight { get; private set; }
            public ConfigEntry<float> BugTickPoison { get; private set; }
            public ConfigEntry<float> BugScorpionPoison { get; private set; }

            // ---------------- 【溢出】：额外体力超过上限后取模 ----------------
            public ConfigEntry<bool> OverflowEnabled { get; private set; }
            public ConfigEntry<float> OverflowLimit { get; private set; }

            // ---------------- 【熬夜】：夜晚定时获得一次能量饮料效果 ----------------
            public ConfigEntry<bool> NightOwlEnabled { get; private set; }
            public ConfigEntry<float> NightOwlIntervalSeconds { get; private set; }

            // ---------------- 【开挂】：无队友时削减负面状态 / 复制在场玩家职业效果 ----------------
            public ConfigEntry<bool> CheatEnabled { get; private set; }
            public ConfigEntry<float> CheatCleanIntervalSeconds { get; private set; }
            public ConfigEntry<float> CheatSoloRadius { get; private set; }
            public ConfigEntry<float> CheatCleanAmount { get; private set; }
            public ConfigEntry<string> CheatCleanTypes { get; private set; }
            public ConfigEntry<bool> CheatCopyEnabled { get; private set; }
            public ConfigEntry<float> CheatCopyIntervalSeconds { get; private set; }
            public ConfigEntry<float> CheatCopyDurationSeconds { get; private set; }
            public ConfigEntry<bool> CheatCopyNotify { get; private set; }

            public void Initialize(ConfigFile config)
            {
                Enabled = BindL(config, "程序员", "启用职业", true, "是否启用程序员职业。");
                MaxPlayers = BindL(config, "程序员", "最大人数", 1,
                    "程序员最多分配给几名玩家。设为 0 表示不限制。默认 1。");
                MinPlayers = BindL(config, "程序员", "最小人数", 0,
                    "程序员最少分配给几名玩家。人数不足时会优先补齐该职业。");
                Weight = BindL(config, "程序员", "抽取权重", 10,
                    "随机分配时程序员被抽中的权重。默认 10。");

                // ---------------- 【BUG】 ----------------
                BugIntervalSeconds = BindL(config, "程序员", "BUG-间隔", 180f,
                    "【BUG】每隔多少秒获得一个随机昆虫（秒）。默认 180。设为 0 关闭发放。");
                BugTickItem = BindL(config, "程序员", "BUG-蜱虫物品", "Bugfix",
                    "【BUG】「蜱虫」的物品预制体名。默认 Bugfix。");
                BugTickWeight = BindL(config, "程序员", "BUG-蜱虫权重", 70,
                    "【BUG】抽中「蜱虫」的权重。默认 70。");
                BugScorpionItem = BindL(config, "程序员", "BUG-蝎子物品", "Scorpion",
                    "【BUG】「蝎子」的物品预制体名。默认 Scorpion。");
                BugScorpionWeight = BindL(config, "程序员", "BUG-蝎子权重", 20,
                    "【BUG】抽中「蝎子」的权重。默认 20。");
                BugWormItem = BindL(config, "程序员", "BUG-早起虫儿物品", "EarlyWorm",
                    "【BUG】「早起虫儿」的物品预制体名。默认 EarlyWorm。");
                BugWormWeight = BindL(config, "程序员", "BUG-早起虫儿权重", 10,
                    "【BUG】抽中「早起虫儿」的权重。默认 10。");
                BugTickPoison = BindL(config, "程序员", "BUG-蜱虫中毒", 0.10f,
                    "【BUG】抽中「蜱虫」时立刻增加的中毒值（0.10 = 10 点）。默认 0.10。设为 0 关闭。");
                BugScorpionPoison = BindL(config, "程序员", "BUG-蝎子中毒", 0.25f,
                    "【BUG】抽中「蝎子」时立刻增加的中毒值（0.25 = 25 点）。默认 0.25。设为 0 关闭。");

                // ---------------- 【溢出】 ----------------
                OverflowEnabled = BindL(config, "程序员", "溢出-启用", true,
                    "【溢出】是否启用。默认 true。");
                OverflowLimit = BindL(config, "程序员", "溢出-上限", 100f,
                    "【溢出】额外体力达到该值后不再封顶、而是取模（100 = 满，与游戏内额外体力上限一致）。默认 100。\n" +
                    "例：额外体力 95 时吃下本会 +10 额外体力的食物，(95+10) mod 100 = 5，额外体力变为 5。");

                // ---------------- 【熬夜】 ----------------
                NightOwlEnabled = BindL(config, "程序员", "熬夜-启用", true,
                    "【熬夜】是否启用。默认 true。");
                NightOwlIntervalSeconds = BindL(config, "程序员", "熬夜-间隔", 60f,
                    "【熬夜】每隔多少秒检测一次（秒）。默认 60。\n" +
                    "检测时若为「夜晚」（DayNightManager.isDay < 0.5），则获得一次能量饮料效果；\n" +
                    "加速结束后**不会**额外清除因此产生的困倦。设为 0 关闭。");

                // ---------------- 【开挂】 ----------------
                CheatEnabled = BindL(config, "程序员", "开挂-启用", true,
                    "【开挂】是否启用。默认 true。");
                CheatCleanIntervalSeconds = BindL(config, "程序员", "开挂-检测间隔", 10f,
                    "【开挂·无队友】每隔多少秒检测一次（秒）。默认 10。\n" +
                    "检测时若自身「无队友半径」内没有其他玩家，则随机削减自身 1 项负面状态。设为 0 关闭。");
                CheatSoloRadius = BindL(config, "程序员", "开挂-无队友半径", 30f,
                    "【开挂·无队友】判定「没有其他玩家」的半径（米）。默认 30。");
                CheatCleanAmount = BindL(config, "程序员", "开挂-削减量", 0.025f,
                    "【开挂·无队友】每次随机削减 1 项负面状态的值（0.025 = 2.5 点）。默认 0.025。设为 0 关闭。");
                CheatCleanTypes = BindL(config, "程序员", "开挂-负面状态", "Injury,Spores,Poison,Drowsy,Cold,Hot",
                    "【开挂·无队友】可被随机削减的负面状态（STATUSTYPE 名，英文逗号分隔）。\n" +
                    "默认：损伤 / 孢子 / 中毒 / 睡眠 / 寒冷 / 炎热。");
                CheatCopyEnabled = BindL(config, "程序员", "开挂-复制启用", true,
                    "【开挂·复制】是否启用。默认 true。");
                CheatCopyIntervalSeconds = BindL(config, "程序员", "开挂-复制间隔", 180f,
                    "【开挂·复制】每隔多少秒检测一次（秒）。默认 180。\n" +
                    "检测时若自身「没有任何负面状态」（见「开挂-负面状态」），\n" +
                    "则复制在场随机一名其他玩家当前职业的随机 1 个职业效果，持续「开挂-复制时长」秒。设为 0 关闭。");
                CheatCopyDurationSeconds = BindL(config, "程序员", "开挂-复制时长", 180f,
                    "【开挂·复制】复制到的职业效果生效时长（秒）。默认 180（与复制间隔一致，直到下一次检测）。\n" +
                    "对「加速 / 无限体力」这类有持续时间的 affliction 效果会在窗口内自动续发，\n" +
                    "对一次性效果（发放物品 / 施加状态）只生效一次。设为 0 表示只生效一次、不续发。");
                CheatCopyNotify = BindL(config, "程序员", "开挂-复制提示", true,
                    "【开挂·复制】成功复制到效果时是否在屏幕上显示提示（与法师效果提示同款 Toast）。默认 true。");
            }
        }
    }
}
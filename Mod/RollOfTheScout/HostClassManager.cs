using System.Collections;
using System.Collections.Generic;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace RollOfTheScout
{
    /// <summary>
    /// 主机权威职业系统驱动器。整个模组的所有逻辑都只在这里运行，
    /// 且只有 MasterClient 会真正产生效果。
    ///
    /// 非主机（客机）上：本组件只承担「接收端」角色 —— 不执行任何职业逻辑、不影响主机，
    /// 仅接收主机广播的职业分配与提示并显示在**本机屏幕**上（相当于附属模组的功能）。
    /// 这样「客机只装主模组」「客机只装附属模组」「两者都装」三种情形都能正常工作。
    /// </summary>
    public sealed class HostClassManager : MonoBehaviour, Photon.Realtime.IOnEventCallback
    {
        public static HostClassManager Instance { get; private set; }

        private readonly ClassLedger _ledger = new ClassLedger();

        // 闭环检测缓存：玩家稳定标识 → 上次读到的状态（用稳定标识以便重连后继续跟踪）
        private readonly Dictionary<string, bool> _wasDead = new Dictionary<string, bool>();

        // 先驱·不屈：本局已经用掉「昏迷时清除全部负面状态」的玩家稳定标识（每局每人仅一次）
        private readonly HashSet<string> _unbrokenUsed = new HashSet<string>();

        /// <summary>不屈「本局已用过」的提示是否已打印（每局每人一次，避免每帧刷屏）。</summary>
        private readonly HashSet<string> _unbrokenSkipLogged = new HashSet<string>();

        // 转职：玩家稳定标识 → 本局已成功转职的次数（上限由配置「转职-次数」决定）
        private readonly Dictionary<string, int> _reclassUsed = new Dictionary<string, int>();

        // 转职去重：玩家稳定标识 → 去重窗口结束时刻。同一次「食用」可能同时经由
        // Harmony 补丁（Item.Consume）与全局事件（GlobalEvents.OnItemConsumed）两条通道到达，
        // 用 0.5 秒窗口保证只处理一次，避免一次食用消耗两次转职次数。
        private readonly Dictionary<string, float> _reclassGuardUntil = new Dictionary<string, float>();

        /// <summary>转职两条触发通道之间的去重窗口（秒）。</summary>
        private const float ReclassDedupWindow = 0.5f;

        // 待皓·模因污染：玩家稳定标识 → 去重窗口结束时刻。与转职同理，同一次「食用」可能同时
        // 经由 Harmony 补丁（Item.Consume）与全局事件（GlobalEvents.OnItemConsumed）两条通道到达。
        private readonly Dictionary<string, float> _memeGuardUntil = new Dictionary<string, float>();

        /// <summary>模因污染两条触发通道之间的去重窗口（秒）。</summary>
        private const float MemeDedupWindow = 0.5f;

        /// <summary>模因污染触发物品的解析缓存（配置字符串 → 预制体名集合）。</summary>
        private string _memeItemRaw;
        private readonly HashSet<string> _memeItems = new HashSet<string>();

        // 野人·野性未泯：玩家稳定标识 → 去重窗口结束时刻。同一次「食用」可能同时经由
        // Harmony 补丁（Item.Consume）与全局事件（GlobalEvents.OnItemConsumed）两条通道到达，
        // 用短窗保证「非自然食物」的损伤只施加一次。
        private readonly Dictionary<string, float> _savageEatGuardUntil = new Dictionary<string, float>();

        /// <summary>野人「野性未泯」两条食用通道之间的去重窗口（秒）。</summary>
        private const float SavageEatDedupWindow = 0.5f;

        /// <summary>监听 GlobalEvents.OnItemConsumed 的委托实例（用于幂等订阅与重订阅）。</summary>
        private System.Action<Item, Character> _onItemConsumed;

        /// <summary>下一次检查「全局食用事件订阅是否仍然有效」的时刻。</summary>
        private float _nextConsumeSubCheck;

        // 领袖「士气」：领袖死亡时受过惩罚的存活玩家稳定标识，领袖复活时按此名单恢复
        private readonly HashSet<string> _moraleTargets = new HashSet<string>();

        // 领袖「士气」：受罚玩家在「被施加惩罚之前」的诅咒 / 石化快照。
        // 复活恢复时按「快照 − 当前值」计算实际回退量，并夹到不超过实际施加的惩罚量，
        // 因此绝不会把玩家自己原有的诅咒 / 石化一并抹掉
        //（实测问题：开局自带 10 诅咒的后勤，士气 -10 后连自身的 10 一起被清成 0）。
        private readonly Dictionary<string, float> _moraleCurseBefore = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _moralePetrifyBefore = new Dictionary<string, int>();

        /// <summary>引擎的最小状态步长：只有 ≥ 该值（且为其整数倍）的增量才会实际生效。</summary>
        private const float StatusIncrement = 0.025f;

        // 先驱「高代谢」加速周期：玩家稳定标识 → 一次加速的监听状态（用于加速结束后清除困倦）
        private readonly Dictionary<string, SprintWatch> _sprintWatches = new Dictionary<string, SprintWatch>();

        // 旗手「执旗」无限体力周期：玩家稳定标识 → 一次无限体力的监听状态
        //（用于等其后续「持续叠加困倦」affliction 结束后清除残留困倦，见 StaminaDrowsyWatch）。
        private readonly Dictionary<string, StaminaDrowsyWatch> _staminaDrowsyWatches
            = new Dictionary<string, StaminaDrowsyWatch>();

        // 领袖 / 后勤「复活继承」：死亡瞬间记录诅咒与石化
        private readonly Dictionary<string, float> _deathCurse = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _deathPetrify = new Dictionary<string, int>();

        // 医生「共情」：玩家稳定标识 → 本次共情冷却的结束时刻（Time.time）。
        // 一次共情损伤后进入冷却，冷却期内再有队友死亡也不再扣损伤，避免医生被连续扣死。
        private readonly Dictionary<string, float> _empathyCooldownUntil = new Dictionary<string, float>();

        // M·「享受」：玩家稳定标识 → 上一次轮询时这 9 种「受力」负面状态的快照。
        // 主机轮询（被动倍率只能靠「主机轮询同步值 + 施加补偿增量」实现，见硬约束 #5）：
        // 对比快照得到每种状态的「增量」，正增量之和 × 倍率即本次应获得的额外体力。
        private readonly Dictionary<string, float[]> _sufferSnapshot = new Dictionary<string, float[]>();

        // 圣人·跌落神坛：玩家稳定标识 → 上一次检测时该圣人的损伤值快照。
        // 每「跌落神坛-检测间隔」秒检测一次，若损伤值较上次增加（即受到损伤），
        // 则追加一笔「跌落神坛-损伤」，并把快照更新为「追加后」的预期值，
        // 避免把自身追加的损伤误判为下一次「受到损伤」而无限触发。
        private readonly Dictionary<string, float> _saintFallSnapshot = new Dictionary<string, float>();

        /// <summary>「享受」覆盖的 9 种负面状态（不含 饥饿 / 螃蟹 / 诅咒 / 困倦 / 负重 / 石化）。</summary>
        private static readonly CharacterAfflictions.STATUSTYPE[] SufferTypes =
        {
            CharacterAfflictions.STATUSTYPE.Injury,   // 损伤
            CharacterAfflictions.STATUSTYPE.Poison,   // 中毒
            CharacterAfflictions.STATUSTYPE.Spores,   // 孢子
            CharacterAfflictions.STATUSTYPE.Thorns,   // 荆棘
            CharacterAfflictions.STATUSTYPE.Hot,      // 烧伤
            CharacterAfflictions.STATUSTYPE.Cold,     // 冻伤
            CharacterAfflictions.STATUSTYPE.Web,      // 蛛网
            CharacterAfflictions.STATUSTYPE.Arrow,    // 箭
            CharacterAfflictions.STATUSTYPE.FlyTrap,  // 捕蝇草
        };

        /// <summary>
        /// M·「耐受」覆盖的 6 种特定负面状态（损伤 / 中毒 / 孢子 / 荆棘 / 烧伤 / 冻伤）。
        /// ⚠ <see cref="CharacterAfflictions.STATUSTYPE.Thorns"/> 属原版状态数组通道的硬性排除项，
        /// 对客机角色无法下发（见 <see cref="HostApi.TrySubtractStatusLocalOnly"/>），只有 M 就是主机本人时才会真正被削减。
        /// </summary>
        private static readonly CharacterAfflictions.STATUSTYPE[] ToleranceTypes =
        {
            CharacterAfflictions.STATUSTYPE.Injury,   // 损伤
            CharacterAfflictions.STATUSTYPE.Poison,   // 中毒
            CharacterAfflictions.STATUSTYPE.Spores,   // 孢子
            CharacterAfflictions.STATUSTYPE.Thorns,   // 荆棘（仅主机本人有效）
            CharacterAfflictions.STATUSTYPE.Hot,      // 烧伤
            CharacterAfflictions.STATUSTYPE.Cold,     // 冻伤
        };

        /// <summary>判定「是否带有负面状态」的阈值：状态值是 0.025 的整数倍，用极小 epsilon 避免浮点噪声。</summary>
        private const float StatusPresenceEpsilon = 0.0001f;

        // 石化校正待办：石化真实值存在 CharacterData.petrifyAmount，
        // 原版复活会 -75，而且远端角色的这次减少要等其客户端同步回主机才可见，
        // 所以不能只发一次 —— 登记目标值后反复校正（绝对赋值、幂等），直到达成或超时。
        // 「开局宿命/积损的初始石化」与「复活继承」共用本机制，仅日志前缀不同。
        private readonly Dictionary<string, PetrifyRestore> _petrifyRestores = new Dictionary<string, PetrifyRestore>();

        // 开局「宿命 / 积损」诅咒的校正待办：客机（尤其中途加入者）会在收到职业分配后
        // 再执行一次 ApplyReconnectData → SetAll(自身状态)，把主机刚下发的诅咒覆盖回 0。
        // 因此这里登记目标值，随后反复用「绝对赋值、幂等」的 SetStatusAbsolute 校正。
        private readonly Dictionary<string, InitialCurseGrant> _initialCurse =
            new Dictionary<string, InitialCurseGrant>();

        /// <summary>开局诅咒校正的两次尝试之间的等待（秒）。</summary>
        private const float InitialCurseRecheckDelay = 2f;

        /// <summary>开局诅咒校正的额外尝试次数上限（连同首次下发共 1 + 此值 次）。</summary>
        private const int InitialCurseMaxRetries = 2;

        /// <summary>一次开局诅咒校正待办。</summary>
        private sealed class InitialCurseGrant
        {
            public float Target;
            public float NextCheck;
            public float Deadline;
            public int Retries;
        }

        /// <summary>石化校正的两次尝试之间的等待（秒）：留出该角色客户端把结果同步回主机的时间。</summary>
        private const float PetrifyRecheckDelay = 1.5f;

        /// <summary>石化校正的总时长上限（秒），超时放弃并打印警告。</summary>
        private const float PetrifyRestoreTimeout = 12f;

        /// <summary>一次石化校正待办。</summary>
        private sealed class PetrifyRestore
        {
            public int Target;
            public float NextCheck;
            public float Deadline;
            public int Applied;

            /// <summary>日志前缀，用于区分「复活继承」与「开局石化」两种来源。</summary>
            public string Reason = "复活继承";
        }

        /// <summary>一次加速 / 无限体力的监听状态。</summary>
        private sealed class SprintWatch
        {
            public Character Character;
            /// <summary>触发本次效果的效果名（高代谢 / 士气 / 执旗），仅用于日志。</summary>
            public string Reason;
            /// <summary>是否已经观察到效果（对应 affliction）生效。</summary>
            public bool Seen;
            /// <summary>已等待时间（秒）：未生效时从喂食起算，生效后从生效那一刻起算（即效果已持续时长）。</summary>
            public float Elapsed;
            /// <summary>被监听的 affliction 类型（加速 = FasterBoi；大棒棒糖的无限体力 = InfiniteStamina）。</summary>
            public Affliction.AfflictionType AfflictionType = Affliction.AfflictionType.FasterBoi;
            /// <summary>日志里对该效果的中文称呼（加速 / 无限体力）。</summary>
            public string EffectLabel = "加速";
            /// <summary>该效果结束后要清除的困倦量（0 表示不清除）。</summary>
            public float ClearDrowsyAmount;
        }

        /// <summary>
        /// 一次「无限体力（大棒棒糖）后续困倦」的监听状态。
        ///
        /// 大棒棒糖的 <c>Affliction_InfiniteStamina.OnRemoved()</c>（在目标客户端执行）会追加一个
        /// <c>Affliction_AdjustDrowsyOverTime</c>（类型 <c>DrowsyOverTime</c>），它每帧
        /// <c>AdjustStatus(Drowsy, rate*dt)</c> → 不断刷新 <c>LastAddedStatus(Drowsy)</c>，
        /// 使 <c>UpdateNormalStatuses</c> 的自然消退条件永远不成立（表现为「困倦值保持不变」）。
        /// 又因为它是「先结束无限体力、再开始叠加困倦」的两段结构，一次性清除（旧第 31 批做法）会失效。
        /// 故这里按四个阶段推进，等 <c>DrowsyOverTime</c> 真正结束后由主机下发抵消。
        /// </summary>
        private sealed class StaminaDrowsyWatch
        {
            public Character Character;
            /// <summary>触发本次效果的效果名（执旗），仅用于日志。</summary>
            public string Reason;
            /// <summary>总等待时间（秒），用于兜底超时。</summary>
            public float Elapsed;
            /// <summary>当前阶段已等待时间（秒）。</summary>
            public float PhaseElapsed;
            public StaminaDrowsyPhase Phase = StaminaDrowsyPhase.WaitStamina;
        }

        private enum StaminaDrowsyPhase
        {
            /// <summary>等待无限体力生效。</summary>
            WaitStamina,
            /// <summary>等待无限体力结束。</summary>
            WaitStaminaEnd,
            /// <summary>等待「持续叠加困倦」出现。</summary>
            WaitDrowsyAppear,
            /// <summary>等待「持续叠加困倦」结束。</summary>
            WaitDrowsyEnd,
        }

        /// <summary>一次「执旗」待施加：插旗后延迟若干秒，再给所有存活玩家发随机效果。</summary>
        private sealed class FlagBearerPending
        {
            public Character Planter;
            public float ApplyAt;
        }

        /// <summary>
        /// 待皓·化身：某位玩家被改写装扮前的「原始装扮」快照，用于还原。
        /// 同时记录其 Photon 玩家对象，供一局结束时（已无法按角色查职业）统一还原使用。
        /// </summary>
        private sealed class AvatarSnapshot
        {
            public Photon.Realtime.Player Player;

            /// <summary>被改写前的原始装扮，用于离开待皓时还原。</summary>
            public CharacterCustomizationData Data;

            /// <summary>本效果「期望装扮」的副本（配置项 &gt;=0 的项为配置值，&lt;0 的项为套用当时的现值），
            /// 供每帧漂移检测使用：若当前装扮被客户端自身同步覆盖，则据此重新套用。</summary>
            public CharacterCustomizationData Applied;
        }

        /// <summary>「执旗」待施加队列（插旗后延迟触发，见 TickFlagBearerEffects）。</summary>
        private readonly List<FlagBearerPending> _flagPending = new List<FlagBearerPending>();

        /// <summary>
        /// 每帧驱动循环复用的「活跃角色」列表缓冲（见 Update 与 HostApi.FillActiveCharacters）。
        /// 仅在本驱动器每帧的开头写入一次，供本帧各 Ensure* / Grant* / Tick* 只读遍历，避免每帧新分配列表。
        /// </summary>
        private readonly List<Character> _activeCharacters = new List<Character>(16);

        /// <summary>等待观察到加速生效的最长时间（秒），超时视为喂食失败。</summary>
        private const float SprintApplyTimeout = 10f;

        /// <summary>等待加速结束的最长时间（秒），兜底防止监听残留。</summary>
        private const float SprintEndTimeout = 180f;

        /// <summary>等待无限体力生效的最长时间（秒），超时视为喂食失败。</summary>
        private const float StaminaApplyTimeout = 10f;

        /// <summary>等待无限体力结束的最长时间（秒）。</summary>
        private const float StaminaEndTimeout = 180f;

        /// <summary>无限体力结束后，等待「持续叠加困倦」出现的最长时间（秒）；超时说明该物品本就不加困倦。</summary>
        private const float DrowsyAppearTimeout = 60f;

        /// <summary>等待「持续叠加困倦」结束的最长时间（秒），超时则兜底清除并告警。</summary>
        private const float DrowsyEndTimeout = 120f;

        private bool _inRun;
        private bool _pendingNewLevel;
        private float _pollTimer;
        private float _inspireTimer;
        private float _inspireDrowsyTimer;
        private float _airdropTimer;
        private float _poisonTimer;
        private float _nursingTimer;
        private float _explorerHungerTimer;
        private float _explorerSprintTimer;
        private float _pandoraTimer;
        private float _pandoraCurseTimer;
        private float _savageCleanseTimer;
        private float _savageBananaTimer;
        private float _mDrainTimer;
        /// <summary>M·「耐受」：定时削减 6 种特定负面状态的计时器（秒）。</summary>
        private float _mToleranceTimer;
        private float _scoutSoloTimer;
        /// <summary>童军·【集体】（第 85 批三档）：同伴档的损伤 / 饥饿计时器（间隔不同，故分开）。</summary>
        private float _scoutPeerInjuryTimer;
        private float _scoutPeerHungerTimer;
        /// <summary>童军·【集体】（第 85 批三档）：童军/领袖档的损伤 / 饥饿计时器（间隔不同，故分开）。</summary>
        private float _scoutAllyInjuryTimer;
        private float _scoutAllyHungerTimer;
        private float _dancerDanceTimer;
        private float _daihaoVoiceTimer;
        private float _daihaoSliplTimer;
        private float _daihaoEggTimer;
        private float _daihaoEggStaminaTimer;
        private float _daihaoAllSameTimer;
        private float _saintTeachTimer;
        private float _saintPerfectionTimer;
        private float _saintFallTimer;
        // 死灵法师·亡灵：骷髅计数加成（体力/诅咒）与光环（人类损伤/骷髅体力）三个独立定时器
        private float _necSkeletonStaminaTimer;
        private float _necSkeletonCurseTimer;
        private float _necAuraTimer;
        private float _necAuraInvincibleTimer;

        // 通用·夜晚抵消寒冷：下发「炎热」的定时器与「本段夜晚是否已打过日志」标记
        private float _nightHeatTimer;
        private bool _nightHeatLogged;

        // 法师·魔法卷轴：发放定时器 + 人类形态的延迟结算队列 + 持续型效果（净化/治愈/反重）
        private float _mageScrollTimer;
        private readonly List<MagePending> _magePending = new List<MagePending>();
        private readonly List<MageContinuous> _mageContinuous = new List<MageContinuous>();

        // 特级厨师：自带厨具发放定时器 / 手暖炉结算定时器
        private float _chefStoveTimer;
        private float _chefHeatTimer;

        // 特级厨师·煤气中毒：玩家稳定标识 → 本局烤制次数（每 N 次触发一次中毒）
        private readonly Dictionary<string, int> _chefCookCount = new Dictionary<string, int>();

        // 特级厨师·破坏大师：玩家稳定标识 → 本次「烤制非食物触发额外体力」的冷却结束时刻（Time.time）
        private readonly Dictionary<string, float> _chefBreakerUntil = new Dictionary<string, float>();

        // 炊事员：便携烤炉发放定时器 / 随机食物发放定时器 / 创伤应激结算定时器
        private float _cookStoveTimer;
        private float _cookFoodTimer;
        private float _cookStressTimer;

        // 和平主义者·和平？：玩家稳定标识 → 本次踢人的冷却结束时刻（去重：一次右键按住会反复广播 StartReaching）
        private readonly Dictionary<string, float> _pacifistKickCooldownUntil = new Dictionary<string, float>();

        // 仓鼠·我全都要：负重达标时的加速「续发」守卫 / 额外体力定时器（两者独立）
        // 原版能量饮料的加速（FasterBoi）在已生效时再次饮用不会延长时长，
        // 故加速改为「检测到效果结束即续发」：本表记录玩家稳定标识 → 下次允许续发的时刻。
        private readonly Dictionary<string, float> _hamsterSprintGuardUntil = new Dictionary<string, float>();
        private float _hamsterExtraTimer;

        // 救火队员·温暖臂膀：背人时的治疗定时器
        private float _firefighterHealTimer;

        // 救火队员·火线救援：玩家稳定标识 → 上一次采样时是否处于「昏迷倒地（未死亡）」状态。
        // 用「false → true」的跳变识别「有玩家进入昏迷」，与 _wasDead 的死亡跳变同套思路。
        private readonly Dictionary<string, bool> _wasDowned = new Dictionary<string, bool>();

        // 救火队员·火线救援：昏迷跳变的采样计时器。
        // 该判定由「每帧」改为按 <see cref="DownedPollInterval"/> 采样：
        // 采样间隔刻意取很小（0.15s ≈ 60fps 下约 9 帧），使「检测到昏迷」的延迟在实际游戏中不可感知，
        // 从而在不改变效果语义的前提下，把「对全体玩家的每帧检查」降为「每 0.15 秒一次」。
        private float _downedPollTimer;

        /// <summary>「进入昏迷」跳变的采样间隔（秒）。取小值以兼顾「降低每帧开销」与「触发延迟不可感知」。</summary>
        private const float DownedPollInterval = 0.15f;

        // 救火队员·尽职尽责：玩家稳定标识 → 上一轮轮询观测到的「骷髅形态（data.isSkeleton）」。
        //
        // 为什么不能直接用死亡那一帧的 IsSkeleton()：骷髅玩家死亡时，其客户端在
        // Character.RPCA_Die 里会紧接着调用 data.SetSkeleton(false)（第 1833 行）并向所有客户端广播，
        // 与「dead = true」的两条同步几乎同时到达主机 —— 主机这一轮轮询读到的 isSkeleton 往往已经是 false，
        // 于是「骷髅玩家死亡不触发尽职尽责」失效（实测问题）。
        // 骷髅形态只在死亡那一刻被清除，因此用**上一轮**的观测值即可可靠判定。
        private readonly Dictionary<string, bool> _wasSkeleton = new Dictionary<string, bool>();

        // 救火队员·火线救援：一次「延迟救援」任务 —— 目标昏迷玩家的稳定标识 + 触发时刻。
        private struct RescueTask
        {
            public string TargetId;
            public float FireAt;
        }

        // 救火队员·火线救援：救火队员稳定标识 → 待执行的延迟救援任务。
        // 由 PollDownedTransitions 检测「昏迷 false → true」跳变后为每名存活救火队员登记；
        // TickRescue 到点后（目标仍昏迷、自身仍存活）施加无敌并传送，再转入「等待背起」。
        private readonly Dictionary<string, RescueTask> _rescuePending = new Dictionary<string, RescueTask>();

        // 救火队员·火线救援：救火队员稳定标识 → 已传送到位、等待背起的目标昏迷玩家稳定标识。
        // 背起该目标后立即一次性给予加速与额外体力。BeginRun / EndRun 清空。
        private readonly Dictionary<string, string> _rescueAwaitCarry = new Dictionary<string, string>();

        // 救火队员·火线救援：救火队员稳定标识 → 本效果的触发冷却结束时刻（Time.time）。
        // 每名救火队员在「火线救援-冷却」内最多触发一次（登记延迟救援任务时检查并顺延）。BeginRun / EndRun 清空。
        private readonly Dictionary<string, float> _rescueCooldownUntil = new Dictionary<string, float>();

        // 救火队员·尽职尽责：救火队员稳定标识 → 石化效果的冷却结束时刻（Time.time）。
        // 人类玩家死亡仍触发，但每名救火队员在「尽职尽责-冷却」内最多结算一次。BeginRun / EndRun 清空。
        private readonly Dictionary<string, float> _dutyCooldownUntil = new Dictionary<string, float>();

        // 自恋狂·精致 / 化学中毒：玩家稳定标识 → 本次「防晒喷雾用完」的去重窗口结束时刻。
        // 同一次「用完」可能同时经由 Harmony 补丁（Action_ReduceUses.ReduceUsesRPC）与
        // 全局食用事件（GlobalEvents.OnItemConsumed）两条通道到达，用短窗保证只结算一次。
        private readonly Dictionary<string, float> _narcissistSunscreenGuardUntil = new Dictionary<string, float>();

        /// <summary>自恋狂「防晒喷雾用完」两条触发通道之间的去重窗口（秒）。</summary>
        private const float NarcissistSunscreenDedupWindow = 1.5f;

        // 自恋狂·精致（补充）：玩家稳定标识 → 本次「获得防晒状态 +额外体力」的冷却结束时刻。
        // 防晒状态持续期间会被反复刷新（每次刷新都会走 OnApplied / Stack 触发一次「获得」），
        // 用冷却限制结算频率。BeginRun / EndRun 清空。
        private readonly Dictionary<string, float> _narcissistStatusCooldownUntil = new Dictionary<string, float>();

        // 独狼·独行长路：两个独立定时器（互斥的两条效果各按自己的间隔计时）
        private float _loneWolfSoloTimer;    // 独行（远离所有玩家）每「独行-间隔」秒一次额外体力
        private float _loneWolfDrowsyTimer;  // 同伴（附近有玩家）每「同伴-困倦间隔」秒一次困倦

        // 炮手·开炮：每「大炮间隔」秒发放一次童子军大炮的定时器
        private float _gunnerCannonTimer;

        // 炮手·炸膛：玩家稳定标识 → 上一轮轮询时该角色的损伤（Injury）快照。
        // 与 M·「享受」同理（硬约束 #5）：被动感知只能靠「主机轮询同步值 + 对比快照」，
        // 损伤较上一轮增加即视为「本次受到损伤」，随即发放炸药。
        private readonly Dictionary<string, float> _gunnerInjurySnapshot = new Dictionary<string, float>();

        // 乐队成员·乐器：每「乐器-检测间隔」秒扫描一次物品栏的定时器
        private float _bandInstrumentTimer;

        // 乐队成员·乐队：每「乐队-间隔」秒按附近乐队成员数量结算一次的定时器
        private float _bandGroupTimer;

        // 乐队成员·演奏：玩家稳定标识 → 累计吹响次数（只计小号 / 童军领队的号角 / 友谊喇叭）。
        // 每累计「演奏-困倦每当吹响次数」次为自身施加一次困倦。BeginRun / EndRun 清空。
        private readonly Dictionary<string, int> _bandBlowCount = new Dictionary<string, int>();

        // 乐队成员·演奏：玩家稳定标识 → 本次「单次吹响」去重窗口结束时刻（Time.time）。
        // 小号单次吹响效果对同一吹奏者每「演奏-冷却」秒最多结算一次，避免快速连点刷加成。
        private readonly Dictionary<string, float> _bandBlowCooldownUntil = new Dictionary<string, float>();

        // 乐队成员·乐队：玩家稳定标识 → 【演奏】半径加成（米），由每次【乐队】结算写入，
        // 持续到下一次【乐队】结算（默认 30 秒）为止。BeginRun / EndRun 清空。
        private readonly Dictionary<string, float> _bandRadiusBonus = new Dictionary<string, float>();

        // 乐队成员·转职（补充效果）：每「转职-检测间隔」秒检测一次的定时器。
        private float _bandTransferTimer;

        // 乐队成员·转职：玩家稳定标识 → 已「连续满足条件（身背乐器 且 附近有乐队成员）」的次数。
        // 只有连续满足「转职-连续次数」次后才进入概率判定，条件一断即清零（节省性能）。BeginRun / EndRun 清空。
        private readonly Dictionary<string, int> _bandTransferStreak = new Dictionary<string, int>();

        // 乐队成员·转职：【转职-物品】配置串的解析缓存（配置改动时才重新解析）。
        private string _bandTransferItemsRaw;
        private readonly HashSet<string> _bandTransferItems = new HashSet<string>();

        // ---------------------------------------------------------------
        // 领队·迈尔斯残魂
        // ---------------------------------------------------------------

        // 【受诅残魂】诅咒保护：玩家稳定标识 → 「最新记录的诅咒值」。
        // 每次进入新关卡（+5 后）、以及触发「会减少自身诅咒值的自身职业效果」后都会立即记录一次；
        // 轮询发现诅咒因「自身职业效果与法师·净化以外」的原因被降低时，强制恢复到该记录值。
        private readonly Dictionary<string, float> _milesCurseRecord = new Dictionary<string, float>();

        // 【受诅残魂】允许降低诅咒的窗口：玩家稳定标识 → 窗口结束时刻（Time.time）。
        // 法师·魔法卷轴·净化的持续期间内，允许诅咒被降低并同步更新记录值（否则净化会被保护逻辑立刻撤销）。
        private readonly Dictionary<string, float> _milesCurseAllowUntil = new Dictionary<string, float>();

        // 【受诅残魂】诅咒保护轮询计时器。
        private float _milesCurseTimer;

        // 【封印之力】本局已「第一次拾取」过的童军宝石预制体名（用于 -诅咒 与【童军的力量】判定）。
        private readonly HashSet<string> _milesGemPicked = new HashSet<string>();

        // 【众魂阻挠】玩家稳定标识 → 本局累计使用童军宝石道具的次数（每累计 N 次生成 1 名童军领队）。
        private readonly Dictionary<string, int> _milesGemUseCount = new Dictionary<string, int>();

        // 【封印之力】宝石「使用」多通道去重：键 = 玩家稳定标识 + "|" + 宝石名 → 窗口结束时刻（Time.time）。
        // 同一次使用可能同时经由「本地动作补丁（ToggleGem / RunAction）」与「食用事件（Item.Consume）」两条通道到达，
        // 用短窗保证一次使用只结算一次（不同宝石用不同键，互不影响）。
        private readonly Dictionary<string, float> _milesUseGuardUntil = new Dictionary<string, float>();

        /// <summary>宝石「使用」两条触发通道之间的去重窗口（秒）。</summary>
        private const float MilesUseDedupWindow = 0.3f;

        // 【封印之力】客机宝石使用启发式判定：玩家稳定标识 → 上一次轮询时的石化值（0~100）。
        // 童军宝石的直接使用是使用者客户端的本地动作、不广播 RPC，主机收不到；
        // 因此对**客机**改用「手执童军宝石 + 石化值一次性增加 ≥ 阈值」作为判定依据（详见 PollMilesRemoteGemUse）。
        private readonly Dictionary<string, int> _milesPetrifySnapshot = new Dictionary<string, int>();

        // 【封印之力】客机启发式判定的轮询计时器。
        private float _milesPetrifyTimer;

        // 【零号守则·附近玩家】加成计时器（每 `零号守则-附近玩家-间隔` 结算一次）。
        private float _milesNearbyTimer;

        // 【封印之力】客机启发式判定命中后的冷却：键 = 玩家稳定标识 + "|" + 宝石名 → 冷却结束时刻（Time.time）。
        // 按「玩家 + 宝石」分别计时，避免用了毅力后紧接着用进取 / 野心被同一玩家的冷却一并吞掉；
        // 也避免「石化持续增加」型宝石（野心持续 +1）被反复结算。
        private readonly Dictionary<string, float> _milesRemoteUseCooldownUntil = new Dictionary<string, float>();

        // 【封印之力】宝石「减石化」的延迟扣减待办：玩家稳定标识 → 待扣减点数与执行时刻。
        // 原因：宝石的石化代价由原版在使用动作中/之后追加 —— 若在 `ToggleGem` 的 Postfix 里**立即**扣减，
        // 此时石化往往还是 0（代价尚未加上），`-N` 被夹到 0 等于**空放**（实测「第一次使用不减石化、第二次才生效」）。
        // 故改为「延迟一小段（默认 0.35s，够同帧/次帧追加的代价落地）后按当前值绝对扣减」。
        private readonly Dictionary<string, MilesPetrifyReduce> _milesPetrifyReduce = new Dictionary<string, MilesPetrifyReduce>();

        /// <summary>宝石「减石化」延迟扣减的等待时长（秒）。</summary>
        private const float MilesPetrifyReduceDelay = 0.35f;

        /// <summary>一次宝石「减石化」延迟扣减待办（同一玩家的多次使用会累加点数）。</summary>
        private sealed class MilesPetrifyReduce
        {
            public int Amount;
            public float FireAt;
        }

        // 【零号守则】玩家稳定标识 → 「昏迷 → 无敌 + 传送到昏迷者处」的冷却结束时刻（Time.time）。
        private readonly Dictionary<string, float> _milesRuleZeroCooldownUntil = new Dictionary<string, float>();

        // 【零号守则】玩家稳定标识 → 上一次采样时的「昏迷」快照（用于识别 false → true 跳变）。
        private readonly Dictionary<string, bool> _milesWasDowned = new Dictionary<string, bool>();

        // 【零号守则】昏迷跳变的采样计时器（与救火队员·火线救援同频，见 PollDownedTransitions）。
        private float _milesRuleZeroPollTimer;

        // 【零号守则·毅力助人】玩家稳定标识 → 待等量清除的石化目标值 / 校正截止时刻。
        // 使用「童军的毅力」帮助附近昏迷玩家时，游戏自身给使用者追加的石化会被等量抵消。
        private readonly Dictionary<string, int> _milesZeroPetrifyTarget = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _milesZeroPetrifyUntil = new Dictionary<string, float>();

        // 【零号守则·毅力助人】下一次允许下发「石化校正」的时刻 —— 校正窗口内每帧都会把实际值拉回目标，
        // 若每帧都发 RPC 会白刷网络包（实测一次消耗会连续打出 10 行日志），故按 0.25s 节流。
        private readonly Dictionary<string, float> _milesZeroPetrifyNextSend = new Dictionary<string, float>();

        /// <summary>零号守则「石化校正」的下发节流间隔（秒）。</summary>
        private const float MilesZeroPetrifySendInterval = 0.25f;

        // 【封印之力 / 零号守则】玩家稳定标识 → 「使用前」石化快照（值, 记录时刻）。
        // 由宝石动作 / 喂食的 Prefix（<see cref="Patches.MilesGemUsePatch"/> /
        // <see cref="Patches.MilesFeedGemPatch"/>）在动作执行**之前**写入。
        // 用途：零号守则的「等量清除」必须以「使用前」的石化值为基准 ——
        // 结算通常发生在宝石已把石化加上之后（本地动作 Postfix / 客机启发式轮询都晚于代价落地），
        // 若以「当前值」为基准，只会扣掉职业自带的减量，宝石追加的石化完全没被清除。
        private readonly Dictionary<string, KeyValuePair<int, float>> _milesPreUsePetrify =
            new Dictionary<string, KeyValuePair<int, float>>();

        /// <summary>「使用前」石化快照的有效期（秒）：超过则视为过期、按当前值兜底。</summary>
        private const float MilesPreUsePetrifyLifetime = 1.5f;

        /// <summary>
        /// 童军宝石「使用」的情形 —— 只有**毅力**需要区分：
        /// · <see cref="SelfUse"/>：对自己使用 → 【封印之力】子效果（−10 石化、−10 饥饿）；
        /// · <see cref="FeedNormal"/>：喂给其他**未昏迷**玩家 → **无任何职业效果**（原版涨多少石化就是多少），但仍计入【众魂阻挠】次数；
        /// · <see cref="FeedDowned"/>：喂给其他**昏迷**玩家 → 【零号守则】（等量清除本次追加的石化、**不计入**次数）；
        /// · <see cref="Unknown"/>：信号来源未给出（客机启发式 / 食用事件），由「喂食意图」推断，推断不出按「自用」处理。
        /// </summary>
        public enum MilesGemUsage
        {
            Unknown = 0,
            SelfUse,
            FeedNormal,
            FeedDowned,
        }

        /// <summary>一次喂食的意图（由 <c>Item.SendFeedDataRPC</c> 记录）。</summary>
        private sealed class MilesFeedIntent
        {
            public bool Downed;      // 被喂者是否处于「昏迷（倒地未死亡）」
            public string Gem;       // 被喂的宝石预制体名
            public float Until;      // 失效时刻（喂食动作时长 + 余量）
        }

        // 【封印之力 / 零号守则】喂食意图：喂食者稳定标识 → 意图。
        // 客机喂宝石时，主机看不到「被喂的是谁、是不是昏迷」；但原版喂食在**开始时**会由喂食者以
        // RpcTarget.All 广播 `Item.SendFeedDataRPC(giverID, recieverID, itemID, totalUsingTime)`，
        // 主机借此即可还原「谁在喂谁、喂的哪件宝石」，从而把「喂给昏迷者」与「喂给未昏迷者」精确分开。
        private readonly Dictionary<string, MilesFeedIntent> _milesFeedIntent = new Dictionary<string, MilesFeedIntent>();

        // 【封印解放】本局是否已触发（解放童军领队灵魂后只结算一次）。
        private bool _milesSealFreedDone;

        // ---------------------------------------------------------------
        // 区（Qu）
        // ---------------------------------------------------------------

        /// <summary>分解者物品池解析缓存（配置字符串 → 预制体名集合）。</summary>
        private string _quDecomposerRaw;
        private readonly HashSet<string> _quDecomposerItems = new HashSet<string>();

        /// <summary>玩家稳定标识 → 本局已触发【分解者】的次数（用于【好多区】「每 N 次获得 1 只虫儿」）。</summary>
        private readonly Dictionary<string, int> _quDecomposedCount = new Dictionary<string, int>();

        /// <summary>玩家稳定标识 → 攀爬强制中断的累计计时（秒）。仅攀爬中的区才累计。</summary>
        private readonly Dictionary<string, float> _quClimbDrainTimer = new Dictionary<string, float>();

        /// <summary>【区块链】结算计时器（自身清除孢子 + 同伴加孢子 + 传染判定）。</summary>
        private float _quBlockchainTimer;

        /// <summary>【好多区】结算计时器。</summary>
        private float _quManyTimer;

        /// <summary>房间内是否存在「区」职业玩家（供音效 / 拾取补丁做廉价短路，避免无区时也走完整判定）。</summary>
        public bool HasQuInRoom => _ledger.HasClassInRoom(ClassType.Qu);

        // ===============================================================
        // 太空人（Astronaut）：【轻飘飘】/【飞起来】/【太空食品安全】/【我去，老美的NASA这么厉害】
        // ===============================================================

        /// <summary>【轻飘飘】周期绑气球的计时器（秒）。</summary>
        private float _astronautBalloonTimer;

        /// <summary>【NASA】每 N 秒削减炎热 / 寒冷的计时器（秒）。</summary>
        private float _nasaTimer;

        /// <summary>本局已发放过「开局初始气球」的太空人稳定标识。</summary>
        private readonly HashSet<string> _astronautInitialBalloons = new HashSet<string>();

        /// <summary>【NASA】玩家稳定标识 → 上一次轮询的「蛛网 / 捕蝇草 / 诅咒」快照（追加值会后移快照，避免自触发）。</summary>
        private readonly Dictionary<string, float[]> _nasaBonusSnapshot = new Dictionary<string, float[]>();

        /// <summary>【太空食品安全】食用去重：玩家稳定标识 → 可再次处理的最早时刻。</summary>
        private readonly Dictionary<string, float> _astronautEatGuardUntil = new Dictionary<string, float>();

        /// <summary>【太空食品安全】两条食用通道之间的去重窗口（秒）。</summary>
        private const float AstronautEatDedupWindow = 0.5f;

        /// <summary>【飞起来】「特殊物品黑名单」解析缓存（配置字符串 → 预制体名集合）。</summary>
        private string _flyBlacklistRaw;
        private readonly HashSet<string> _flyBlacklist = new HashSet<string>();

        // ===============================================================
        // 通用「食用黑名单」缓存（第 85 批）
        //   榔王·纤维化 / 野人·野性未泯 / 炊事员·创伤应激：命中黑名单的物品食用时不触发该效果。
        //   按**预制体名**比对（实例名带 (Clone)，不可靠），解析结果带缓存。
        // ===============================================================

        private string _fibrosisBlacklistRaw;
        private readonly HashSet<string> _fibrosisBlacklist = new HashSet<string>();

        /// <summary>野人·野性未泯的食用黑名单缓存（静态：<see cref="IsSavageForbiddenFood"/> 是静态方法）。</summary>
        private static string _savageEatBlacklistRaw;
        private static readonly HashSet<string> _savageEatBlacklist = new HashSet<string>();

        private string _cookStressBlacklistRaw;
        private readonly HashSet<string> _cookStressBlacklist = new HashSet<string>();

        /// <summary>太空人·【太空食品安全】的食用黑名单缓存（默认：诡异菇 Mushroom Glow、万灵药 Cure-All）。</summary>
        private string _spaceFoodBlacklistRaw;
        private readonly HashSet<string> _spaceFoodBlacklist = new HashSet<string>();

        /// <summary>「超然物外·免疫区块链」的日志去重集合（每局每人只提示一次，区块链是周期性结算，否则会刷屏）。</summary>
        private readonly HashSet<string> _transcendentBlockchainLogged = new HashSet<string>();

        /// <summary>
        /// 童军·行李「物品池」的解析缓存（配置字符串 → 已解析的预制体名列表）。
        /// 该池由「每帧幂等补发」流程使用，若每次都 <c>ParseList</c>（内部 <c>Split</c>）会**每帧产生字符串垃圾**；
        /// 这里只在配置字符串变化时重新解析。
        /// </summary>
        private string _scoutLuggagePoolRaw;
        private readonly List<string> _scoutLuggagePool = new List<string>();

        /// <summary>
        /// 炊事员「后勤保障-食物池」的解析缓存（配置字符串 → 已解析的预制体名列表）。
        /// 与童军行李池同理：只在配置字符串变化时重新解析，避免周期性抽取食物时的字符串分配。
        /// </summary>
        private string _cookFoodPoolRaw;
        private readonly List<string> _cookFoodPool = new List<string>();

        /// <summary>炊事员「食物池」：按配置字符串缓存的只读列表（<see cref="PickRandomFood"/> 用）。</summary>
        private List<string> CookFoodPool()
        {
            string raw = ModConfig.Cook.FoodPool.Value;
            if (raw != _cookFoodPoolRaw)
            {
                _cookFoodPoolRaw = raw;
                _cookFoodPool.Clear();
                _cookFoodPool.AddRange(WeightedPicker.ParseList(raw));
            }
            return _cookFoodPool;
        }

        /// <summary>【NASA】额外加成覆盖的状态：蛛网 / 捕蝇草 / 诅咒。</summary>
        private static readonly CharacterAfflictions.STATUSTYPE[] NasaBonusTypes =
        {
            CharacterAfflictions.STATUSTYPE.Web,      // 蛛网
            CharacterAfflictions.STATUSTYPE.FlyTrap,  // 捕蝇草
            CharacterAfflictions.STATUSTYPE.Curse,    // 诅咒
        };

        // ===============================================================
        // 榔王（Langwang）：【精品大果】/【上瘾】/【纤维化】/【没吃的赶紧戒】
        // ===============================================================

        /// <summary>【精品大果】发放荔莓的计时器（秒）。</summary>
        private float _langwangBerryTimer;

        /// <summary>【上瘾】「超时未食用荔莓」的结算计时器（秒）。</summary>
        private float _langwangAddictionTimer;

        /// <summary>玩家稳定标识 → 上一次食用荔莓（或首次见到该榔王）的时刻。</summary>
        private readonly Dictionary<string, float> _langwangLastBerryAt = new Dictionary<string, float>();

        /// <summary>玩家稳定标识 → 「连续食用荔莓」的当前次数（期间食用非荔莓食物会清零）。</summary>
        private readonly Dictionary<string, int> _langwangBerryStreak = new Dictionary<string, int>();

        /// <summary>食用去重：玩家稳定标识 → 可再次处理的最早时刻（榔王自身的四个效果）。</summary>
        private readonly Dictionary<string, float> _langwangEatGuardUntil = new Dictionary<string, float>();

        /// <summary>榔王两条食用通道之间的去重窗口（秒）。</summary>
        private const float LangwangEatDedupWindow = 0.5f;

        /// <summary>「荔莓物品」解析缓存（配置字符串 → 预制体名集合）。</summary>
        private string _langwangBerryRaw;
        private readonly HashSet<string> _langwangBerryItems = new HashSet<string>();

        // ===============================================================
        // 程序员（Programmer）：【BUG】/【溢出】/【熬夜】/【开挂】
        // ===============================================================

        /// <summary>【BUG】发放随机昆虫的计时器（秒）。</summary>
        private float _programmerBugTimer;

        /// <summary>【熬夜】检测夜晚并发放加速的计时器（秒）。</summary>
        private float _programmerNightTimer;

        /// <summary>【开挂·无队友】削减 1 项随机负面状态的计时器（秒）。</summary>
        private float _programmerCleanTimer;

        /// <summary>【开挂·复制】重新检测并复制「在场玩家职业效果」的计时器（秒）。</summary>
        private float _programmerCopyTimer;

        /// <summary>「可被削减的负面状态」配置字符串缓存，用于检测配置是否变化。</summary>
        private string _programmerStatusTypesRaw;

        /// <summary>【开挂】用于判定「负面状态」的状态类型（默认：损伤 / 孢子 / 中毒 / 睡眠 / 寒冷 / 炎热）。</summary>
        private readonly List<CharacterAfflictions.STATUSTYPE> _programmerStatusTypes
            = new List<CharacterAfflictions.STATUSTYPE>();

        /// <summary>玩家稳定标识 → 当前复制到的职业效果记录（用于窗口内续发）。</summary>
        private readonly Dictionary<string, ProgrammerCopy> _programmerCopies
            = new Dictionary<string, ProgrammerCopy>();

        /// <summary>程序员【开挂·复制】一次「复制到的职业效果」记录（含「下一次检测时清理」所需的信息）。</summary>
        private sealed class ProgrammerCopy
        {
            /// <summary>被复制效果所属职业。</summary>
            public ClassType SourceClass;
            /// <summary>效果名（如「顶级玩家」「骸骨」）。</summary>
            public string Effect;
            /// <summary>「续发窗口」截止时刻（<c>Time.time</c>，&lt; 0 表示不续发）——加速 / 无限体力在窗口内自动续发。</summary>
            public float RefreshUntil = -1f;
            /// <summary>被复制的目标角色。</summary>
            public Character Target;

            // ---- 清理信息（下一次检测 / 目标失效时撤销，保证「不继承」）----
            /// <summary>需从 3 个物品栏移除的物品预制体名（最多 2 件）。</summary>
            public string Item1;
            public string Item2;
            /// <summary>物品栏已满时「在脚下生成」的物品（清理时销毁，避免「继承」）。</summary>
            public List<GameObject> GroundItems;
            /// <summary>是否需清空背包槽（背包类效果）。</summary>
            public bool RemoveBackpack;
            /// <summary>需反向撤销的状态（1~2 项，存**已施加的有符号量**，撤销时取相反数）。</summary>
            public CharacterAfflictions.STATUSTYPE? Status1;
            public float Status1Amount;
            public CharacterAfflictions.STATUSTYPE? Status2;
            public float Status2Amount;
            /// <summary>需反向扣减的额外体力（正数；撤销时下发等量负值）。</summary>
            public float StaminaUndo;
            /// <summary>需要**绝对恢复**的石化值（&lt; 0 表示不处理）。</summary>
            public int PetrifyRestore = -1;
            /// <summary>true = 该效果无法通过 RPC 从远端移除（加速 / 无限体力 affliction），清理时仅停止续发。</summary>
            public bool StopRefreshOnly;

            // ---- 定时型效果：生效期内按自身节奏重复施加 ----
            /// <summary>定时型效果间隔（秒；&lt;= 0 表示一次性效果）。</summary>
            public float Interval;
            /// <summary>下一次定时触发的时刻（<c>Time.time</c>）。</summary>
            public float NextTickAt;

            // ---- 窗口内续发（仅加速 / 无限体力）----
            public string RefreshKind;
            public float NextReapplyAt;
        }

        /// <summary>监听 GlobalEvents.OnSoulFreed 的委托实例（用于幂等订阅与重订阅）。</summary>
        private System.Action<int> _onSoulFreed;

        /// <summary>下一次检查「灵魂解放事件订阅是否仍然有效」的时刻。</summary>
        private float _nextSoulSubCheck;

        /// <summary>童军宝石道具（毅力 / 进取 / 盛情 / 野心）的预制体名集合。</summary>
        private static readonly HashSet<string> MilesGemItems = new HashSet<string>
        {
            ItemNames.AmuletHealing,
            ItemNames.AmuletSuperJump,
            ItemNames.AmuletClone,
            ItemNames.AmuletInfiniteStamina,
        };

        /// <summary>该预制体名是否属于「童军宝石」四件套（供补丁做廉价短路）。</summary>
        public static bool IsMilesGemName(string prefabName)
        {
            return !string.IsNullOrEmpty(prefabName) && MilesGemItems.Contains(prefabName);
        }

        // 待皓·化身：本关已套用过装扮的玩家稳定标识（每关重置，见 Process 的新关卡处理）。
        // 用于识别「本关首次套用」（据此开启自愈监控窗口并区分日志），以及还原时的清理。
        private readonly HashSet<string> _avatarApplied = new HashSet<string>();

        // 待皓·化身：玩家稳定标识 → 被改写前的原始装扮快照（本局内一直保留，不随关卡切换清空）。
        // 用于「玩家不再是待皓（如转职离开）时立即还原原始装扮」，以及一局结束时统一还原。
        private readonly Dictionary<string, AvatarSnapshot> _avatarOriginals = new Dictionary<string, AvatarSnapshot>();

        // 待皓·化身「自愈监控」：玩家稳定标识 → 监控窗口结束时刻（Time.unscaledTime）。
        // 被分配为待皓（或换关重建角色）后的这段时间内，每秒比对一次装扮是否被客户端自身同步覆盖，
        // 被覆盖则重发；窗口结束后不再检测，避免无谓的每帧开销。
        private readonly Dictionary<string, float> _avatarMonitorUntil = new Dictionary<string, float>();

        // 待皓·化身「自愈监控」：玩家稳定标识 → 下次允许比对的时刻（1 秒节流）。
        private readonly Dictionary<string, float> _avatarNextCheck = new Dictionary<string, float>();

        // 舞王·舞动青春：玩家稳定标识 → 本次检测冷却的结束时刻。
        // 同一玩家在冷却窗口内的再次跳舞不再触发加成（配置「舞动青春-检测冷却」，默认 1 秒）。
        private readonly Dictionary<string, float> _dancerYouthCooldownUntil = new Dictionary<string, float>();

        // 舞王诊断：已经打印过日志的表情动作名，避免同一名字反复刷屏。
        // 用于帮助用户从日志里找到「跳舞」表情的真实动画名（反编译代码中查不到，在资源包里）。
        private readonly HashSet<string> _observedEmotes = new HashSet<string>();

        /// <summary>新关卡发放背包的最小间隔，避免同一关卡重复触发。</summary>
        private const float NewLevelCooldown = 8f;

        private float _lastNewLevelTime = -999f;

        /// <summary>分配职业时是否已记录过异常（避免每帧刷屏）。</summary>
        private bool _assignErrorLogged;

        // ---- 发放状态（幂等 + 重试）----
        // 客机可能比主机晚进入关卡、晚被分配职业，因此所有「发放类」效果都不能只做一次性尝试，
        // 而要每帧补发一次：本关已发放成功的角色记入 _levelGranted，开局效果成功的记入 _initialApplied。
        // 失败时会在后续帧重试（最多 MaxGrantAttempts 次），以覆盖「角色/背包尚未就绪」的时机问题。
        private readonly HashSet<string> _levelGranted = new HashSet<string>();
        private readonly Dictionary<string, int> _levelGrantAttempts = new Dictionary<string, int>();
        private readonly HashSet<string> _initialApplied = new HashSet<string>();

        /// <summary>
        /// 已施加过「开局状态类」效果（宿命 / 积损 / 骸骨 / 极限）的玩家稳定标识。
        /// 与物品类不同，状态类效果**只在玩家存活时**施加一次：中途加入的客机先以「灵魂形态（已死亡）」
        /// 加入，此时下发的石化会被原版复活流程与客户端迟到初始化吞掉（实测问题：挑战者只拿到 60 诅咒、
        /// 拿不到 70 石化），等其复活为人类后再施加即可一次成功。
        /// </summary>
        private readonly HashSet<string> _initialStatusDone = new HashSet<string>();

        /// <summary>
        /// 「中途加入·灵魂待复活」的玩家稳定标识：分配职业时该玩家仍处于「死亡 / 灵魂」状态。
        ///
        /// PEAK 不支持从大厅进入关卡后再中途加入，加入者会先以「已死亡的角色（灵魂）」进入关卡，
        /// 必须等其他人使用**童军雕像**（`ScoutEffigy`）或**古老雕像**（`RespawnChest`）复活后才真正参与游戏。
        /// 这类玩家在复活到位之前**不能**接收任何「开局 / 每层发放」：
        ///   · 死亡状态下物品栏不可用，物品会掉在未 warp 的哨兵坐标上（实测书本 / 抓钩落在 (0, 4961, -4999)）；
        ///   · 更要命的是原版 <c>RPCA_ReviveAtPosition</c> 会先 <c>DropAllItems(includeBackpack: true)</c>、
        ///     再 <c>ReviveCharacter</c>（内部 <c>ClearAllStatus()</c> + 石化 -75），复活前发的东西会被一并清掉。
        /// 因此分配职业时先登记，等其「复活到位」后再解除（见 <see cref="UpdateJoinSoulSettle"/>）。
        /// 旧方案依赖的 LateJoinFix（会自动复活并反复清除 dead 标记）已从整合包移除，本机制不依赖任何外部模组。
        /// </summary>
        private readonly HashSet<string> _joinSouls = new HashSet<string>();

        /// <summary>「中途加入者」被观测到「持续存活」的起始时刻（稳定标识 → Time.time），用于滤掉 dead 标记的瞬时抖动。</summary>
        private readonly Dictionary<string, float> _joinSoulAliveSince = new Dictionary<string, float>();

        /// <summary>
        /// 判定「中途加入者已复活到位」所需的**持续存活**时长（秒）。
        /// 取 2 秒：既比原版复活流程（同一 RPC 内 warp + 复活）宽裕，又足以滤掉 dead 标记的瞬时抖动 ——
        /// 若按「观测到一次复活」就发放，可能在其实还没真正复活时提前发放，被随后的原版复活流程清掉。
        /// </summary>
        private const float JoinSoulSettleSeconds = 2f;

        private readonly Dictionary<string, int> _initialAttempts = new Dictionary<string, int>();

        /// <summary>备战充分待发放的物品名（按玩家稳定标识固定下来，重试时不会改抽别的物品）。</summary>
        private readonly Dictionary<string, List<string>> _initialGrantPlan = new Dictionary<string, List<string>>();

        /// <summary>发放类效果的最大尝试次数（超过后不再重试，避免背包槽被占用时无限刷警告）。</summary>
        private const int MaxGrantAttempts = 5;

        // ---- 本机（主机）屏幕提示 ----
        // 原版没有任何可用的通知 UI（UI_Notifications 在场景中不存在实例、也无任何调用方），
        // 因此这里自绘一个左上角提示层，只显示在主机自己的屏幕上。
        private readonly List<string> _toasts = new List<string>();
        private readonly List<float> _toastExpiries = new List<float>();
        private const float ToastDuration = 8f;
        private const int ToastMaxCount = 5;
        private GUIStyle _toastStyle;

        /// <summary>
        /// 提示队列的「内容版本号」：内容（增/删/清空）变化时自增。
        /// 叠加层据此判断「无需重建字符串」，从而消除每帧把同一份提示重新拼接的开销。
        /// </summary>
        private int _toastVersion;

        // ---- 按键显示「当前职业分配情况」（右上角面板，仅主机可见）----
        private bool _classListHeld;          // 「按住显示」模式：按住时为 true
        private bool _classListToggled;       // 「按下切换」模式：当前是否处于显示状态
        private float _classListPressUntil;   // 「按下显示」模式：显示到该时间点为止
        private const float ClassListPressDuration = 5f;
        private GUIStyle _classListStyle;
        private GUIStyle _classListTitleStyle;

        // ---- 「职业显示」按键 / 模式的解析缓存（每帧读取输入时避免重复解析）----
        // 配置字符串不变时直接复用上次的 KeyCode 与模式分类；字符串变化（用户改 cfg 后重载）才重算。
        private string _classListKeyRaw;
        private UnityEngine.KeyCode _classListKeyParsed = UnityEngine.KeyCode.Tab;
        private string _classListModeRaw;
        private int _classListModeKind;        // 0 = 按下显示，1 = 按住显示，2 = 按下切换

        /// <summary>面板标题文本缓存（取值只随配置「职业显示-按键」变化，不必每帧重建）。</summary>
        private string _classListTitleCache;
        private string _classListTitleCacheKey;

        // ---- 「职业显示」网络同步到客机（安装了本模组或附属模组的客机）----
        // 只有主机发送；周期重发保证中途加入的客机也能尽快拿到快照；职业变动时置脏以尽快同步。
        private float _nextDisplaySyncTime;
        private bool _displaySyncDirty;

        // ---- 客机接收模式（本机非主机）：接收主机广播的职业分配与提示 ----
        // 仅在「本机安装本模组、但不是 MasterClient」时使用；不影响主机、也不影响未装模组的客机。
        private readonly List<KeyValuePair<int, int>> _remoteClasses = new List<KeyValuePair<int, int>>();
        private readonly Dictionary<int, int> _remoteClassMap = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _remoteClassNames = new Dictionary<int, string>();
        private bool _hasRemoteSnapshot;

        private void Awake()
        {
            Instance = this;
            // 接收端：订阅 Photon 自定义事件（客机模式下接收主机广播的职业显示；
            // 主机收到自己发的事件会被 Photon 默认排除，且 OnEvent 里另有 IsMasterClient 守卫）。
            PhotonNetwork.AddCallbackTarget(this);
            // 转职兜底通道：订阅游戏自身的「物品被食用」全局事件（原版 ResetAllRunEvents 会清空它，
            // 因此 Update 里还会周期性重订阅）。
            EnsureConsumeSubscription();
            // 迈尔斯残魂·封印解放：订阅游戏自身的「童军领队灵魂被解放」全局事件（同样会被 ResetAllRunEvents 清空）。
            EnsureSoulFreedSubscription();
            // 程序员·【溢出】：注册「额外体力增量改写」钩子（把超过上限的增量改写为取模后的增量）。
            HostApi.ExtraStaminaAdjust = AdjustProgrammerOverflow;
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>查询某角色当前被分配的职业（供「刺客·刺杀」的拾取补丁调用）。</summary>
        public ClassType GetClassOf(Character character)
        {
            return _ledger.GetClass(character);
        }

        /// <summary>
        /// 玩家食用「转职物品」（默认诡异菇 Mushroom Glow）后触发转职。
        /// 规则：在该玩家**当前未占满名额的其他职业**中按权重随机重新分配一个；
        /// 每人每局最多成功转职「转职-次数」次（默认 1）；没有可转职业时不生效、也不消耗次数。
        ///
        /// 状态处理：**转职成功时**先 `ClearAllNegativeStatuses` 清零全部负面状态，再施加新职业的初始状态
        /// （避免旧职业残留值被叠加，如领袖石化 20 + 后勤石化 70 = 90）；转职失败时**不清除**，
        /// 以免被当作白嫖的「清负面」手段。
        ///
        /// 本方法有两个调用来源（互为备份，去重窗口见 <see cref="ReclassDedupWindow"/>）：
        /// 1. <c>Item.Consume</c> 的 Harmony 补丁（<see cref="Patches.ReclassConsumePatch"/>）；
        /// 2. 游戏自身的全局事件 <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="HandleItemConsumed"/>）。
        /// 后者用于兜底：游戏运行时 Assembly-CSharp 会被预加载补丁重写，补丁可能挂不上。
        /// </summary>
        public void OnReclassItemConsumed(Character character)
        {
            if (!_inRun) return;
            if (character == null) return;
            if (!ModConfig.General.ReclassEnabled.Value) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            // 去重：同一次食用不要被两条通道处理两次
            if (_reclassGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
            _reclassGuardUntil[id] = Time.time + ReclassDedupWindow;

            int maxCount = ModConfig.General.ReclassCount.Value;
            _reclassUsed.TryGetValue(id, out int used);
            if (used >= maxCount)
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 转职：{Nick(character)} 本局转职次数已用尽（{used}/{maxCount}），本次不生效。");
                return;
            }

            ClassType oldType = _ledger.GetClass(character);
            if (oldType == ClassType.None)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 转职：{Nick(character)} 当前无职业，无法转职。");
                return;
            }

            if (!_ledger.TryReassign(character, out _, out var newType))
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 转职：{Nick(character)} 附近没有可转的其他职业（其余职业名额已占满），本次不生效。");
                return;
            }

            _reclassUsed[id] = used + 1;

            ApplyClassChange(character, id, oldType, newType);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 转职：{Nick(character)} 食用转职物品，职业 {ClassLedger.Describe(oldType)} → " +
                $"{ClassLedger.Describe(newType)}（本局已转职 {_reclassUsed[id]}/{maxCount} 次）。");
            ShowToast($"{Nick(character)} 转职：{ClassLedger.Describe(oldType)} → {ClassLedger.Describe(newType)}");
        }

        /// <summary>
        /// 改职（转职 / 模因污染）的**公共收尾**：先把玩家身上的「全部负面状态」清零，
        /// 再让新职业的「开局类」效果重新走一遍幂等发放流程。
        ///
        /// 清零原因：否则新职业的初始状态会在旧职业残留值上继续叠加 —— 实测：
        /// 领袖（宿命：诅咒20 + 石化20）→ 后勤（积损：诅咒70 + 石化70）会得到石化 20+70=90。
        /// 注意：只有改职**成功**才调用；改不过去（名额占满 / 无反例）时不调用，避免被当作白嫖的「清负面」手段。
        /// 另外，若**旧职业是挑战者**，还会一并清除【顶级玩家】发放的黄金抓钩（`RescueHook_Infinite`）。
        /// </summary>
        private void ApplyClassChange(Character character, string id, ClassType oldType, ClassType newType)
        {
            _displaySyncDirty = true;   // 转职 / 模因污染改职 → 尽快把新快照同步给客机
            HostApi.ClearAllNegativeStatuses(character);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 改职：{Nick(character)} 已清除全部负面状态，准备施加新职业初始状态。");

            // 挑战者离开时：清除【顶级玩家】发放的黄金抓钩（`RescueHook_Infinite`）。
            // 该物品只在挑战者【顶级玩家】里发放，若不在改职时清掉，转职后仍会残留一把本职业专属物品。
            if (oldType == ClassType.Challenger)
            {
                int removed = HostApi.RemoveItemsFromInventorySlots(character, ItemNames.GoldenHook);
                if (removed > 0)
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 改职：{Nick(character)} 离开挑战者，已清除 {removed} 个黄金抓钩（{ItemNames.GoldenHook}）。");
                }
            }

            // 让新职业的「开局类」效果重新走一遍幂等发放流程：
            // 状态类（宿命/积损）与物品类（背包/匕首）都在后续帧由 Ensure* / Grant* 重新补发。
            // 背包类物品（开路 / 随身医疗箱）由 GrantLevelItems 走「EmptyOut() 后覆盖」流程，
            // 会直接替换掉玩家原本装备的背包（含其中物品），与用户预期一致。
            _initialApplied.Remove(id);
            _initialAttempts.Remove(id);
            _initialGrantPlan.Remove(id);
            _levelGranted.Remove(id);
            _levelGrantAttempts.Remove(id);
            _initialStatusDone.Remove(id);
            // 改职说明该玩家此刻存活（能食用转职物品），因此不再属于「中途加入·灵魂待复活」
            _joinSouls.Remove(id);
            _joinSoulAliveSince.Remove(id);
            _wasDead[id] = IsTrueDead(character);
        }

        /// <summary>
        /// 游戏自身事件 <c>GlobalEvents.OnItemConsumed(Item, Character)</c> 的处理器。
        /// 该事件由原版 <c>Item.Consume</c> 在所有客户端（含主机）内触发，因此主机可借此
        /// 零客机安装地识别「谁吃了什么」。作为 Harmony 补丁的备份通道。
        /// </summary>
        private void HandleItemConsumed(Item item, Character character)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (item == null || character == null) return;

                // 转职通道（与 Item.Consume 补丁互为备份）
                if (ModConfig.General.ReclassEnabled.Value)
                {
                    string trigger = ModConfig.General.ReclassItem.Value;
                    if (!string.IsNullOrEmpty(trigger)
                        && ItemDatabase.TryGetItem(item.itemID, out Item prefab) && prefab != null
                        && prefab.gameObject.name == trigger)
                    {
                        OnReclassItemConsumed(character);
                    }
                }

                // 野人·野性未泯：食用非自然食物增加损伤
                OnSavageItemConsumed(item, character);

                // 待皓·模因污染：食用触发物品直接转职成待皓
                OnDaihaoMemeConsumed(item, character);

                // 法师·魔法卷轴 / 黑魔法：打开卷轴触发随机效果
                OnMageScrollConsumed(character, item);

                // 炊事员·食品安全 / 创伤应激：食用烤熟 / 未烤熟的食物
                OnCookItemConsumed(item, character);

                // 自恋狂·精致 / 化学中毒：用完 1 瓶防晒喷雾（兜底通道之一，见 OnNarcissistSunscreenUsedUp）
                OnNarcissistSunscreenConsumed(item, character);

                // 领队·迈尔斯残魂·封印之力：使用（食用）童军宝石道具
                OnMilesGemConsumed(item, character);

                // 区·区毒不食子 / 区块链（喂食虫儿）：食用早起虫儿
                OnQuItemConsumed(item, character);

                // 太空人·太空食品安全：食用不符合条件的食物 +中毒
                OnAstronautItemConsumed(item, character);

                // 榔王·精品大果 / 上瘾 / 纤维化 / 没吃的赶紧戒
                OnLangwangItemConsumed(item, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 处理全局食用事件时异常：{e.Message}");
            }
        }

        // ---------------------------------------------------------------
        // 野人（Savage）：自然韧性 / 野性未泯 / 不拿拿 / 知识污染
        // ---------------------------------------------------------------

        /// <summary>
        /// 野人·野性未泯：野人食用「包装食物」或「烤过的食物」时增加损伤。
        ///
        /// 判定直接采用游戏原生标签（与原版 <c>CharacterData.TestItemConsumed</c> 一致）：
        /// 1. 包装食物：<c>item.itemTags.HasFlag(Item.ItemTags.PackagedFood)</c>；
        /// 2. 烤过的食物：<c>item.itemTags.HasFlag(Item.ItemTags.GourmandRequirement)</c>
        ///    且 <c>GetData&lt;IntItemData&gt;(DataEntryKey.CookedAmount).Value &gt;= 1</c>。
        /// 自然食物（浆果、蘑菇、生的食材等）不含以上标签，因此不触发，无需维护物品名清单。
        ///
        /// 调用来源有两个（互为备份，去重窗口见 <see cref="SavageEatDedupWindow"/>）：
        /// 1. <c>Item.Consume</c> 的 Harmony 补丁（<see cref="Patches.SavageEatPatch"/>）；
        /// 2. 游戏自身的全局事件 <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="HandleItemConsumed"/>）。
        /// </summary>
        public void OnSavageItemConsumed(Item item, Character character)
        {
            if (!_inRun) return;
            if (item == null || character == null) return;
            if (_ledger.GetClass(character) != ClassType.Savage) return;

            float damage = ModConfig.Savage.EatDamage.Value;
            if (damage <= 0f) return;

            if (!IsSavageForbiddenFood(item)) return;

            // 去重：同一次食用不要被两条通道处理两次
            string id = _ledger.IdOf(character);
            if (!string.IsNullOrEmpty(id))
            {
                if (_savageEatGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
                _savageEatGuardUntil[id] = Time.time + SavageEatDedupWindow;
            }

            HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Injury, damage);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 野性未泯：{Nick(character)} 食用了非自然食物（{item.gameObject.name}），损伤+{damage * 100f:0.#}");
        }

        /// <summary>
        /// 该物品是否属于野人「野性未泯」禁止的食物（包装食物 / 烤过的食物）。
        ///
        /// 包装食物：<c>ItemTags.PackagedFood</c>。
        /// 烤过的食物：<c>GetData&lt;IntItemData&gt;(DataEntryKey.CookedAmount).Value &gt;= 1</c>。
        /// 由 <c>野性未泯-烤熟自然食物触发</c> 控制是否额外要求 <c>GourmandRequirement</c> 标签：
        /// - 开启（默认）：只要被烤过（CookedAmount ≥ 1）就触发 —— 自然食物烤熟同样扣血，符合「烤过的食物」原意；
        /// - 关闭：仅有「美食家」类物品（GourmandRequirement）烤熟才触发，自然食物烤熟不触发（旧行为）。
        ///
        /// 注：<c>GourmandRequirement</c> 是预制体上的静态标签（用于原版美食家成就），
        /// 烹饪（<c>ItemCooking.FinishCooking</c> → <c>SetCookedAmountRPC</c>）只会把 <c>CookedAmount</c> 从 0 变为 ≥1，
        /// **不会**给物品追加该标签 —— 这正是旧实现下「烤过的自然食物不扣血」的原因。
        /// </summary>
        private static bool IsSavageForbiddenFood(Item item)
        {
            if (item == null) return false;

            // 黑名单（第 85 批新增）：诡异菇是「转职触发物品」（通用·转职-触发物品），
            // 食用它本身即触发转职，不应再叠加职业负面效果；能量饮料 / 奶白金 / 大棒棒糖同属黑名单。
            // 按**预制体名**比对（实例名带 (Clone)，旧实现直接比实例名不可靠）。
            if (NameListContains(ModConfig.Savage.EatBlacklist.Value, ref _savageEatBlacklistRaw, _savageEatBlacklist, PrefabNameOf(item)))
                return false;

            try
            {
                if (item.itemTags.HasFlag(Item.ItemTags.PackagedFood)) return true;

                bool needGourmandTag = !ModConfig.Savage.CookedNaturalFoodTriggers.Value;
                if (needGourmandTag && !item.itemTags.HasFlag(Item.ItemTags.GourmandRequirement)) return false;

                var cooked = item.GetData<IntItemData>(DataEntryKey.CookedAmount);
                if (cooked != null && cooked.Value >= 1) return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 野性未泯·读取物品标签时异常：{e.Message}");
            }
            return false;
        }

        /// <summary>
        /// 野人·不拿拿：为野人发放配置数量的制定物品（默认 1 个粉香蕉）。
        /// 发放逻辑与死灵法师的骸骨之书一致：优先放入 3 个物品栏空位，
        /// 物品栏已满时在该玩家脚下生成（落地后可直接拾取）。
        /// </summary>
        private void GrantSavageBananas(Character savage, string itemName, int count)
        {
            if (savage == null || string.IsNullOrEmpty(itemName) || count <= 0) return;

            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                if (HostApi.GiveItemToInventorySlots(savage, itemName, out _))
                {
                    ok++;
                    continue;
                }
                if (HostApi.SpawnItemAtFeet(savage, itemName) != null)
                {
                    ok++;
                    Plugin.Log.LogInfo($"[RollOfTheScout] 不拿拿：{Nick(savage)} 物品栏已满，{itemName} 改在脚下生成。");
                }
            }

            if (ok > 0)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 不拿拿：{Nick(savage)} 获得 {ok} 个{itemName}。");
            }
            else
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 不拿拿：{Nick(savage)} 的{itemName}发放失败（物品栏已满且脚下生成失败）。");
            }
        }

        /// <summary>
        /// 待皓·生产：为待皓玩家发放 1 个配置物品（默认好大蛋 NestEgg）。
        /// 与不拿拿同套写法：优先放入 3 个物品栏空位，三栏全满时在该玩家脚下生成（落地后可拾取）。
        /// </summary>
        private void GrantDaihaoEgg(Character daihao, string itemName)
        {
            if (daihao == null || string.IsNullOrEmpty(itemName)) return;

            if (HostApi.GiveItemToInventorySlots(daihao, itemName, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 生产：{Nick(daihao)} 获得 1 个{itemName}。");
                return;
            }

            if (HostApi.SpawnItemAtFeet(daihao, itemName) != null)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 生产：{Nick(daihao)} 物品栏已满，{itemName} 改在脚下生成。");
            }
            else
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 生产：{Nick(daihao)} 的{itemName}发放失败（物品栏已满且脚下生成失败）。");
            }
        }

        /// <summary>
        /// 待皓·生产：该角色的 3 个物品栏中是否至少有 1 格是「指定预制体」的物品（不看背包槽）。
        /// 按 itemID 比对，避免受实例化后对象名带 (Clone) 后缀的影响。
        /// </summary>
        private static bool HasItemInInventory(Character character, string prefabName)
        {
            if (character == null || character.player == null) return false;
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null) return false;

            var slots = character.player.itemSlots;
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty()) continue;
                var item = slot.prefab;
                if (item != null && item.itemID == itemPrefab.itemID) return true;
            }
            return false;
        }

        /// <summary>
        /// 待皓·模因污染：本局存在待皓职业玩家时，其他玩家食用「模因污染-触发物品」
        /// （默认煎蛋 <c>Egg</c>，由好大蛋 <c>NestEgg</c> 砸开后产出）会直接转职成待皓。
        ///
        /// 与转职（<see cref="OnReclassItemConsumed"/>）共用同一套「改职收尾」（<see cref="ApplyClassChange"/>），
        /// 因此同样会清空负面状态并按新职业补发开局类效果。
        ///
        /// 两个关键差异：
        /// 1. 绕过「最大人数」限制（<see cref="ClassLedger.ForceAssign"/>）—— 否则待皓已达上限时本效果
        ///    永远无法触发（待皓默认最大人数为 1，而本效果又要求场上已有待皓）；
        /// 2. 不消耗也不受「转职次数」限制 —— 它是被动的模因传播，而非玩家主动转职。
        ///
        /// 调用来源与野人·野性未泯一致（互为备份，去重窗口见 <see cref="MemeDedupWindow"/>）：
        /// 1. <c>Item.Consume</c> 的 Harmony 补丁（<see cref="Patches.MemeConsumePatch"/>）；
        /// 2. 游戏自身的全局事件 <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="HandleItemConsumed"/>）。
        /// </summary>
        public void OnDaihaoMemeConsumed(Item item, Character character)
        {
            if (!_inRun) return;
            if (item == null || character == null) return;
            if (!ModConfig.Daihao.Enabled.Value) return;
            if (!ModConfig.Daihao.MemeEnabled.Value) return;

            // 已经是待皓则不处理
            if (_ledger.GetClass(character) == ClassType.Daihao) return;

            // 本局必须已经存在待皓职业玩家，模因才会「污染」其他人
            if (!_ledger.HasClassInRoom(ClassType.Daihao)) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            if (!IsMemeTriggerItem(item)) return;

            // 去重：同一次食用不要被两条通道处理两次
            if (_memeGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
            _memeGuardUntil[id] = Time.time + MemeDedupWindow;

            ClassType oldType = _ledger.GetClass(character);
            if (!_ledger.ForceAssign(character, ClassType.Daihao)) return;

            ApplyClassChange(character, id, oldType, ClassType.Daihao);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 模因污染：{Nick(character)} 食用了「{item.gameObject.name}」，职业 " +
                $"{ClassLedger.Describe(oldType)} → 待皓。");
            ShowToast($"{Nick(character)} 模因污染：{ClassLedger.Describe(oldType)} → 待皓");
        }

        /// <summary>
        /// 该物品是否为「模因污染-触发物品」（英文逗号分隔的预制体名列表）。
        /// 按 itemID 从 ItemDatabase 反查预制体名，避免实例对象名带 (Clone) 后缀的干扰。
        /// </summary>
        private bool IsMemeTriggerItem(Item item)
        {
            if (item == null) return false;

            string raw = ModConfig.Daihao.MemeItem.Value;
            if (raw != _memeItemRaw)
            {
                _memeItemRaw = raw;
                _memeItems.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _memeItems.Add(name);
            }
            if (_memeItems.Count == 0) return false;

            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return false;
            return _memeItems.Contains(prefab.gameObject.name);
        }

        /// <summary>
        /// 幂等订阅 <c>GlobalEvents.OnItemConsumed</c>。
        /// 原版 <c>GlobalEvents.ResetAllRunEvents()</c> 会把该事件清空，因此需要周期性重订阅。
        /// </summary>
        private void EnsureConsumeSubscription()
        {
            try
            {
                if (_onItemConsumed == null) _onItemConsumed = HandleItemConsumed;

                var current = GlobalEvents.OnItemConsumed;
                if (current != null)
                {
                    foreach (var h in current.GetInvocationList())
                    {
                        if (h.Equals(_onItemConsumed)) return;
                    }
                }
                GlobalEvents.OnItemConsumed += _onItemConsumed;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 转职·订阅全局食用事件时异常：{e.Message}");
            }
        }

        /// <summary>
        /// 迈尔斯残魂·封印解放：幂等订阅游戏自身的「童军领队灵魂被解放」全局事件
        /// （<c>GlobalEvents.OnSoulFreed</c>；原版 <c>ResetAllRunEvents</c> 会把它清空，故周期性重订阅）。
        /// 该事件在灵魂柱被破坏后由所有客户端（含主机）触发，见 ScoutmasterSoulPillar.BreakTriggeredRoutine。
        /// </summary>
        private void EnsureSoulFreedSubscription()
        {
            try
            {
                if (_onSoulFreed == null) _onSoulFreed = OnMilesSoulFreed;

                var current = GlobalEvents.OnSoulFreed;
                if (current != null)
                {
                    foreach (var h in current.GetInvocationList())
                    {
                        if (h.Equals(_onSoulFreed)) return;
                    }
                }
                GlobalEvents.OnSoulFreed += _onSoulFreed;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 封印解放·订阅灵魂解放事件时异常：{e.Message}");
            }
        }

        /// <summary>
        /// 迈尔斯残魂·封印解放：童军领队的灵魂被解放（天底）后，清除在场所有玩家（含自身）的
        /// 全部负面状态（含诅咒与石化），并为每人增加额外体力。每局只结算一次。
        /// </summary>
        private void OnMilesSoulFreed(int phase)
        {
            if (!_inRun) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (!ModConfig.MilesSoul.SealReleaseEnabled.Value) return;
            if (_milesSealFreedDone) return;
            if (!_ledger.HasClassInRoom(ClassType.MilesSoul)) return;

            _milesSealFreedDone = true;

            float stamina = ModConfig.MilesSoul.SealReleaseExtraStamina.Value;
            foreach (var c in HostApi.GetActiveCharacters())
            {
                if (c == null) continue;
                HostApi.ClearAllNegativeStatuses(c);
                if (stamina != 0f) HostApi.BoostExtraStamina(c, stamina);
            }

            // 本效果是「自身职业效果」，因此对迈尔斯残魂本人需要把保护基准重置为清零后的 0，
            // 否则诅咒保护会在下一次轮询把刚被清除的诅咒又恢复回来。
            foreach (var miles in HostApi.GetActiveCharacters())
            {
                if (_ledger.GetClass(miles) != ClassType.MilesSoul) continue;
                RecordMilesCurse(miles, 0f);
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 封印解放：童军领队的灵魂已解放，清除在场所有玩家的全部负面状态并 +{stamina * 100f:0.#} 额外体力。");
            ShowToast("封印解放：童军领队的灵魂已解放，全场净化！");
        }

        /// <summary>
        /// 由 RespawnChest.SpawnItems 的补丁调用 —— 即「使用篝火附近的古老雕像（复活雕像）」。
        /// 负责 领袖·神启（额外神秘物品）与 领袖·圣洁（按存活人数削减诅咒）。
        /// </summary>
        public void OnAncientStatueUsed(RespawnChest chest, List<Transform> spawnSpots)
        {
            if (!_inRun) return;
            if (!_ledger.HasClassInRoom(ClassType.Leader)) return;

            // ---- 圣洁：任意玩家使用古老雕像时，按「死亡人数 / 存活人数」调整领袖的诅咒与石化 ----
            // 每死亡 1 人 → 领袖 +5 诅咒、+10 石化；每有 1 人存活 → 领袖 -2.5 诅咒
            // （存活人数是否含领袖本人由配置「圣洁-存活计数含领袖」决定，默认不含）。
            // 注意：游戏状态的最小步长为 0.025（STATUS_INCREMENT），低于该值的配置不会生效，
            // 0.025（= 显示 2.5 点）就是引擎允许的最小有效变动量。
            var actives = HostApi.GetActiveCharacters();
            int deadCount = 0;
            foreach (var c in actives)
            {
                if (IsDead(c)) deadCount++;
            }

            float cursePerDeath = ModConfig.Leader.CursePerDeath.Value;
            float petrifyPerDeath = ModConfig.Leader.PetrifyPerDeath.Value;
            float curseReducePerAlive = ModConfig.Leader.CurseReducePerAlive.Value;

            // 「每有 1 人存活则领袖 -2.5 诅咒」的存活人数是否把领袖本人计入（可配置）
            bool includeLeader = ModConfig.Leader.CurseReduceIncludeLeader.Value;

            foreach (var leader in actives)
            {
                if (_ledger.GetClass(leader) != ClassType.Leader) continue;

                int aliveCount = 0;
                foreach (var c in actives)
                {
                    if (IsDead(c)) continue;
                    if (!includeLeader && c == leader) continue;
                    aliveCount++;
                }

                float curseDelta = deadCount * cursePerDeath - aliveCount * curseReducePerAlive;
                float petrifyDelta = deadCount * petrifyPerDeath;

                if (!Mathf.Approximately(curseDelta, 0f))
                {
                    HostApi.ApplyStatus(leader, CharacterAfflictions.STATUSTYPE.Curse, curseDelta);
                }
                if (petrifyDelta > 0f)
                {
                    HostApi.ApplyStatus(leader, CharacterAfflictions.STATUSTYPE.Petrify, petrifyDelta);
                }
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 圣洁：死亡 {deadCount} 人 / 存活 {aliveCount} 人（{(includeLeader ? "含" : "不含")}领袖） → " +
                    $"{Nick(leader)} 诅咒{curseDelta:+0.###;-0.###;+0}、石化+{petrifyDelta:0.###}");
            }

            // ---- 神启：全员存活时，为领袖额外发放神秘物品 ----
            int extra = ModConfig.Leader.DivineExtraMystical.Value;
            if (extra <= 0 || !HostApi.EveryoneAlive()) return;

            // 额外物品优先直接塞进领袖的 3 个物品栏空位；三个物品栏都满时，
            // 改为在篝火附近生成并「插在地上固定」（kinematic，拾取前不会滚落山下）。
            Vector3 origin;
            if (spawnSpots != null && spawnSpots.Count > 0 && spawnSpots[0] != null)
            {
                origin = spawnSpots[0].position;
            }
            else if (chest != null)
            {
                origin = chest.transform.position;
            }
            else
            {
                origin = Vector3.zero;
            }

            int granted = 0;
            int dropped = 0;
            foreach (var character in HostApi.GetActiveCharacters())
            {
                if (_ledger.GetClass(character) != ClassType.Leader) continue;

                for (int i = 0; i < extra; i++)
                {
                    if (HostApi.GiveRandomMysticalItemToInventory(character, out string itemName))
                    {
                        granted++;
                        Plugin.Log.LogInfo($"[RollOfTheScout] 神启：{Nick(character)} 的物品栏获得 {itemName}");
                        continue;
                    }

                    var prefab = HostApi.PickRandomMysticalPrefab();
                    if (prefab == null || HostApi.SpawnItemFixedNearCampfire(prefab.name, origin) == null)
                    {
                        Plugin.Log.LogWarning("[RollOfTheScout] 神启：无法发放额外神秘物品（物品栏已满且篝火生成失败）。");
                        continue;
                    }
                    dropped++;
                    Plugin.Log.LogInfo($"[RollOfTheScout] 神启：{Nick(character)} 物品栏已满，{prefab.name} 已固定在篝火附近。");
                }
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 神启：全员存活，额外神秘物品 塞入物品栏 {granted} 件 / 篝火固定 {dropped} 件。");
        }

        /// <summary>由 Campfire.Light_Rpc(updateSegment:true) 的补丁调用。</summary>
        public void NotifyNewLevel()
        {
            if (!_inRun) return;
            if (Time.time - _lastNewLevelTime < NewLevelCooldown) return;
            _lastNewLevelTime = Time.time;

            // 只置标志，真正的发放交给 Update 里的幂等补发流程 ——
            // 补丁触发的时刻客机可能还没进入关卡，立刻发放会漏掉它们。
            _pendingNewLevel = true;
        }

        private void Update()
        {
            // 第三方「头顶id显示」模组兼容层：限流重试挂补丁（不依赖是否在房间 / 关卡内）。
            PeakOverheadCompat.ThirdPartyOverheadNameCompat.Tick(Plugin.HarmonyInstance);

            // 配置文件板块顺序修正（内部 2 秒节流）：游戏内改过配置后，把 BepInEx 排序过的 .cfg 顺序改回业务顺序。
            ConfigFileOrder.Tick();

            // 原生字体叠加层所需的按键状态 / 过期提示 / 内容推送都在最前面结算，
            // 这样即使后续因「不在房间 / 在机场 / 模组关闭」提前返回，叠加层也能正确隐藏或清空。
            UpdateClassListInput();
            ExpireToasts();
            PushNativeOverlay();

            if (!PhotonNetwork.InRoom)
            {
                EndRun();
                return;
            }

            // 回到「机场」（大厅）即代表上一局已结束：清空职业分配，保证每局独立、不跨局继承。
            // 这里必须用「机场场景」判定，而不能用「不在关卡场景」判定 ——
            // 否则局内关卡切换（更深的层数）也会被误判为离开，导致职业被重置。
            if (IsInAirportScene())
            {
                EndRun();
                return;
            }

            if (!ModConfig.General.Enabled.Value) return;

            if (!PhotonNetwork.IsMasterClient) return;

            // 转职兜底通道：周期性确保「全局食用事件」订阅仍然有效
            //（原版 ResetAllRunEvents 会清空该事件，且它可能发生在 BeginRun 之后）。
            if (Time.time >= _nextConsumeSubCheck)
            {
                _nextConsumeSubCheck = Time.time + 2f;
                EnsureConsumeSubscription();
            }

            // 封印解放兜底通道：周期性确保「灵魂解放事件」订阅仍然有效（原版 ResetAllRunEvents 会清空该事件）。
            if (Time.time >= _nextSoulSubCheck)
            {
                _nextSoulSubCheck = Time.time + 2f;
                EnsureSoulFreedSubscription();
            }

            if (!GameHandler.IsInGameplayScene) return;

            // 摊开执行「临时物品喂食」队列（每帧最多 TransientFeedsPerFrame 次）：
            // 把群体效果造成的同帧实例化爆发摊到后续若干帧，消除单帧尖峰（见 EnqueueTransientFeed 的说明）。
            DrainTransientFeeds();

            // 复用同一个列表实例填充本帧的活跃角色，避免每帧新分配 List（名单只在主机内存、每帧重建）。
            // 各 Ensure* / Grant* / Tick* 只读取该列表，不会改动或长期持有；内部若再次取用角色名单，
            // 走的是会自行分配新列表的 HostApi.GetActiveCharacters()，与本缓冲互不影响。
            HostApi.FillActiveCharacters(_activeCharacters);
            var characters = _activeCharacters;
            if (characters.Count == 0) return;

            if (!_inRun) BeginRun(characters);

            // 先分配职业（中途加入者会在这一步被登记为「灵魂待复活」），再结算其是否已复活到位。
            EnsureAssignments(characters);
            UpdateJoinSoulSettle(characters);

            // 「职业显示」同步到客机：职业变动时立即重发，否则每 SnapshotIntervalSeconds 秒保底重发一次，
            // 保证中途加入的客机也能尽快看到当前分配情况。
            if (_displaySyncDirty || Time.time >= _nextDisplaySyncTime)
            {
                _displaySyncDirty = false;
                _nextDisplaySyncTime = Time.time + ClassDisplayNet.SnapshotIntervalSeconds;
                PublishClassSnapshot();
            }

            // 「开局 / 每层发放」类效果只作用于「已复活到位」的玩家：中途加入者先以死亡灵魂加入，
            // 复活前发放的物品会被原版 DropAllItems 清掉、状态会被 ClearAllStatus 清掉且不再补发。
            // 因此把仍在 _joinSouls 里的角色从发放名单中剔除（其余每帧逻辑仍作用于全部角色）。
            List<Character> grantable = characters;
            if (_joinSouls.Count > 0)
            {
                grantable = new List<Character>(characters.Count);
                foreach (var c in characters)
                {
                    string cid = _ledger.IdOf(c);
                    if (!string.IsNullOrEmpty(cid) && _joinSouls.Contains(cid)) continue;
                    grantable.Add(c);
                }
            }

            EnsureInitialStatuses(grantable);
            EnsureCharacterEffects(grantable);
            EnsureAssassinItems(grantable);
            EnsureChallengerItems(grantable);
            EnsureMasterChefStove(grantable);
            EnsureHamsterBackpack(grantable);
            EnsureScoutLuggage(grantable);
            EnsureAstronautBalloons(grantable);

            // 进入新关卡：清空「本关已发放」记录，让下面的幂等补发重新发一遍
            if (_pendingNewLevel)
            {
                _pendingNewLevel = false;
                _levelGranted.Clear();
                _levelGrantAttempts.Clear();
                _avatarApplied.Clear();
                _avatarNextCheck.Clear();
                // 迈尔斯残魂·众魂阻挠：每次进入新关卡 +诅咒（进入天底则不加，由该方法内部判定）
                OnMilesSoulNewLevel(characters);
                Plugin.Log.LogInfo("[RollOfTheScout] 进入新关卡，重新发放先驱/医生背包、死灵法师骸骨之书、旗手检查点旗标，并重新套用待皓化身。");
            }

            // 每帧幂等补发（见 GrantLevelItems）——客机可能比主机晚进入关卡、晚被分配职业，
            // 只发一次会漏掉它们（实测问题 1.1 / 3.1 / 4.1 的根因）。
            GrantLevelItems(grantable);
            GrantNecromancerBooks(grantable);
            GrantStandardBearerFlags(grantable);
            GrantSaintFasting(grantable);
            GrantCookStoves(grantable);
            GrantNarcissistSunscreens(grantable);
            GrantGunnerCannons(grantable);
            ApplyDaihaoAvatar(grantable);

            PollClosedLoop(characters);
            TickTimers(characters);
            TickQu(characters);
            DoNightColdOffset(characters);
            UpdateSprintWatches();
            UpdateStaminaDrowsyWatches();
            TickFlagBearerEffects();
            TickMageEffects(characters);
            TickMilesSoul(characters);
        }

        // ---------------------------------------------------------------
        // 一局的开始 / 结束
        // ---------------------------------------------------------------

        private void BeginRun(List<Character> characters)
        {
            _inRun = true;
            _pendingNewLevel = true;
            _assignErrorLogged = false;
            _pollTimer = 0f;
            _inspireTimer = 0f;
            _inspireDrowsyTimer = 0f;
            _airdropTimer = 0f;
            _poisonTimer = 0f;
            _nursingTimer = 0f;
            _explorerHungerTimer = 0f;
            _explorerSprintTimer = 0f;
            _pandoraTimer = 0f;
            _pandoraCurseTimer = 0f;
            _savageCleanseTimer = 0f;
            _savageBananaTimer = 0f;
            _mDrainTimer = 0f;
            _mToleranceTimer = 0f;
            _scoutSoloTimer = 0f;
            _scoutPeerInjuryTimer = 0f;
            _scoutPeerHungerTimer = 0f;
            _scoutAllyInjuryTimer = 0f;
            _scoutAllyHungerTimer = 0f;
            _dancerDanceTimer = 0f;
            _daihaoVoiceTimer = 0f;
            _daihaoSliplTimer = 0f;
            _daihaoEggTimer = 0f;
            _daihaoEggStaminaTimer = 0f;
            _daihaoAllSameTimer = 0f;
            _saintTeachTimer = 0f;
            _saintPerfectionTimer = 0f;
            _saintFallTimer = 0f;
            _necSkeletonStaminaTimer = 0f;
            _necSkeletonCurseTimer = 0f;
            _necAuraTimer = 0f;
            _necAuraInvincibleTimer = 0f;
            _mageScrollTimer = 0f;
            _chefStoveTimer = 0f;
            _chefHeatTimer = 0f;
            _cookStoveTimer = 0f;
            _cookFoodTimer = 0f;
            _cookStressTimer = 0f;
            _hamsterSprintGuardUntil.Clear();
            _hamsterExtraTimer = 0f;
            _firefighterHealTimer = 0f;
            _downedPollTimer = 0f;
            _loneWolfSoloTimer = 0f;
            _loneWolfDrowsyTimer = 0f;
            _gunnerCannonTimer = 0f;
            _bandInstrumentTimer = 0f;
            _bandGroupTimer = 0f;
            _nightHeatTimer = 0f;
            _nightHeatLogged = false;
            _avatarApplied.Clear();
            _avatarOriginals.Clear();
            _avatarMonitorUntil.Clear();
            _avatarNextCheck.Clear();
            _unbrokenUsed.Clear();
            _unbrokenSkipLogged.Clear();
            _reclassUsed.Clear();
            _reclassGuardUntil.Clear();
            _savageEatGuardUntil.Clear();
            _memeGuardUntil.Clear();
            _moraleTargets.Clear();
            _moraleCurseBefore.Clear();
            _moralePetrifyBefore.Clear();
            _sprintWatches.Clear();
            _staminaDrowsyWatches.Clear();
            _deathCurse.Clear();
            _deathPetrify.Clear();
            _empathyCooldownUntil.Clear();
            _flagPending.Clear();
            _sufferSnapshot.Clear();
            _dancerYouthCooldownUntil.Clear();
            _saintFallSnapshot.Clear();
            _petrifyRestores.Clear();
            _initialCurse.Clear();
            _levelGranted.Clear();
            _levelGrantAttempts.Clear();
            _initialApplied.Clear();
            _initialStatusDone.Clear();
            _joinSouls.Clear();
            _joinSoulAliveSince.Clear();
            _initialAttempts.Clear();
            _initialGrantPlan.Clear();
            _magePending.Clear();
            _mageContinuous.Clear();
            _chefCookCount.Clear();
            _chefBreakerUntil.Clear();
            _pacifistKickCooldownUntil.Clear();
            _wasDowned.Clear();
            _wasSkeleton.Clear();
            _rescuePending.Clear();
            _rescueAwaitCarry.Clear();
            _rescueCooldownUntil.Clear();
            _dutyCooldownUntil.Clear();
            _narcissistSunscreenGuardUntil.Clear();
            _narcissistStatusCooldownUntil.Clear();
            _hamsterSprintGuardUntil.Clear();
            _gunnerInjurySnapshot.Clear();
            _bandBlowCount.Clear();
            _bandBlowCooldownUntil.Clear();
            _bandRadiusBonus.Clear();
            _bandTransferStreak.Clear();
            _milesCurseRecord.Clear();
            _milesCurseAllowUntil.Clear();
            _milesCurseTimer = 0f;
            _milesGemPicked.Clear();
            _milesGemUseCount.Clear();
            _milesUseGuardUntil.Clear();
            _milesPetrifySnapshot.Clear();
            _milesPetrifyTimer = 0f;
            _milesRuleZeroPollTimer = 0f;
            _milesRemoteUseCooldownUntil.Clear();
            _milesPetrifyReduce.Clear();
            _milesRuleZeroCooldownUntil.Clear();
            _milesWasDowned.Clear();
            _milesZeroPetrifyTarget.Clear();
            _milesZeroPetrifyUntil.Clear();
            _milesZeroPetrifyNextSend.Clear();
            _milesPreUsePetrify.Clear();
            _milesFeedIntent.Clear();
            _milesNearbyTimer = 0f;
            _milesSealFreedDone = false;
            _quBlockchainTimer = 0f;
            _quManyTimer = 0f;
            _quDecomposedCount.Clear();
            _quClimbDrainTimer.Clear();
            _astronautBalloonTimer = 0f;
            _nasaTimer = 0f;
            _astronautInitialBalloons.Clear();
            _nasaBonusSnapshot.Clear();
            _astronautEatGuardUntil.Clear();
            _langwangBerryTimer = 0f;
            _langwangAddictionTimer = 0f;
            _langwangLastBerryAt.Clear();
            _langwangBerryStreak.Clear();
            _langwangEatGuardUntil.Clear();
            _transcendentBlockchainLogged.Clear();
            _programmerBugTimer = 0f;
            _programmerNightTimer = 0f;
            _programmerCleanTimer = 0f;
            _programmerCopyTimer = 0f;
            _programmerCopies.Clear();
            _toasts.Clear();
            _toastExpiries.Clear();
            _toastVersion++;
            _transientFeeds.Clear();   // 清掉上一局残留的「临时物品喂食」队列
            _classListHeld = false;
            _classListToggled = false;
            _classListPressUntil = 0f;
            _lastNewLevelTime = Time.time;
            Plugin.Log.LogInfo($"[RollOfTheScout] 检测到新的一局开始，共 {characters.Count} 名角色。");
        }

        private void EndRun()
        {
            // 客机接收模式的数据与本局绑定：无论本机是否为主机，离开本局时都必须清空，
            // 否则回到机场（大厅）后仍会显示上一局的职业分配（该清空不能受下方 _inRun 早退影响）。
            _remoteClasses.Clear();
            _remoteClassMap.Clear();
            _hasRemoteSnapshot = false;
            _displaySyncDirty = false;
            _nextDisplaySyncTime = 0f;

            if (!_inRun) return;
            _inRun = false;
            _pendingNewLevel = false;

            // 一局结束：先把待皓玩家被改写的装扮还原，再清账本（还原需用到快照里记录的玩家对象）。
            RevertAllAvatars();

            _ledger.Clear();
            _wasDead.Clear();
            _avatarApplied.Clear();
            _avatarOriginals.Clear();
            _avatarMonitorUntil.Clear();
            _avatarNextCheck.Clear();
            _unbrokenUsed.Clear();
            _unbrokenSkipLogged.Clear();
            _reclassUsed.Clear();
            _reclassGuardUntil.Clear();
            _savageEatGuardUntil.Clear();
            _memeGuardUntil.Clear();
            _moraleTargets.Clear();
            _moraleCurseBefore.Clear();
            _moralePetrifyBefore.Clear();
            _sprintWatches.Clear();
            _staminaDrowsyWatches.Clear();
            _deathCurse.Clear();
            _deathPetrify.Clear();
            _empathyCooldownUntil.Clear();
            _flagPending.Clear();
            _sufferSnapshot.Clear();
            _dancerYouthCooldownUntil.Clear();
            _saintFallSnapshot.Clear();
            _petrifyRestores.Clear();
            _initialCurse.Clear();
            _levelGranted.Clear();
            _levelGrantAttempts.Clear();
            _initialApplied.Clear();
            _initialStatusDone.Clear();
            _joinSouls.Clear();
            _joinSoulAliveSince.Clear();
            _initialAttempts.Clear();
            _initialGrantPlan.Clear();
            _magePending.Clear();
            _mageContinuous.Clear();
            _chefCookCount.Clear();
            _chefBreakerUntil.Clear();
            _pacifistKickCooldownUntil.Clear();
            _wasDowned.Clear();
            _wasSkeleton.Clear();
            _rescuePending.Clear();
            _rescueAwaitCarry.Clear();
            _rescueCooldownUntil.Clear();
            _dutyCooldownUntil.Clear();
            _narcissistSunscreenGuardUntil.Clear();
            _narcissistStatusCooldownUntil.Clear();
            _hamsterSprintGuardUntil.Clear();
            _gunnerInjurySnapshot.Clear();
            _bandBlowCount.Clear();
            _bandBlowCooldownUntil.Clear();
            _bandRadiusBonus.Clear();
            _bandTransferStreak.Clear();
            _milesCurseRecord.Clear();
            _milesCurseAllowUntil.Clear();
            _milesCurseTimer = 0f;
            _milesGemPicked.Clear();
            _milesGemUseCount.Clear();
            _milesUseGuardUntil.Clear();
            _milesPetrifySnapshot.Clear();
            _milesPetrifyTimer = 0f;
            _milesRuleZeroPollTimer = 0f;
            _milesRemoteUseCooldownUntil.Clear();
            _milesPetrifyReduce.Clear();
            _milesRuleZeroCooldownUntil.Clear();
            _milesWasDowned.Clear();
            _milesZeroPetrifyTarget.Clear();
            _milesZeroPetrifyUntil.Clear();
            _milesZeroPetrifyNextSend.Clear();
            _milesPreUsePetrify.Clear();
            _milesFeedIntent.Clear();
            _milesNearbyTimer = 0f;
            _milesSealFreedDone = false;
            _quBlockchainTimer = 0f;
            _quManyTimer = 0f;
            _quDecomposedCount.Clear();
            _quClimbDrainTimer.Clear();
            _astronautBalloonTimer = 0f;
            _nasaTimer = 0f;
            _astronautInitialBalloons.Clear();
            _nasaBonusSnapshot.Clear();
            _astronautEatGuardUntil.Clear();
            _langwangBerryTimer = 0f;
            _langwangAddictionTimer = 0f;
            _langwangLastBerryAt.Clear();
            _langwangBerryStreak.Clear();
            _langwangEatGuardUntil.Clear();
            _transcendentBlockchainLogged.Clear();
            _programmerBugTimer = 0f;
            _programmerNightTimer = 0f;
            _programmerCleanTimer = 0f;
            _programmerCopyTimer = 0f;
            ClearProgrammerCopies("一局结束");
            _toasts.Clear();
            _toastExpiries.Clear();
            _toastVersion++;
            _transientFeeds.Clear();   // 清掉本局残留的「临时物品喂食」队列
            _classListHeld = false;
            _classListToggled = false;
            _classListPressUntil = 0f;
            _lastNewLevelTime = -999f;
            Plugin.Log.LogInfo("[RollOfTheScout] 一局结束，职业分配已清空（下一局重新随机）。");
        }

        /// <summary>当前是否处于「机场」场景（一局结束后回到的大厅，场景名 Airport）。</summary>
        private static bool IsInAirportScene()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.name.Contains("Airport");
        }

        // ---------------------------------------------------------------
        // 随机分配 + 开局效果
        // ---------------------------------------------------------------

        /// <summary>
        /// 每帧结算「中途加入者是否已复活到位」。
        ///
        /// 判定口径（三者同时满足，且**持续** <see cref="JoinSoulSettleSeconds"/> 秒）：
        ///   ① 已不在「死亡 / 昏迷」状态（<see cref="IsDead"/> 为假）；
        ///   ② 物品栏已就绪（<see cref="HostApi.IsInventoryReady"/>，避免发放失败 / 掉在未 warp 坐标）；
        ///   ③ 上述状态连续保持 2 秒 —— 用于滤掉 dead 标记的瞬时抖动（中途加入过程中该标记可能短暂为假，
        ///      但此时并非真正的复活）。
        ///
        /// 满足后解除「灵魂待复活」状态，本帧起该玩家就会进入上面构造的 <c>grantable</c> 名单，
        /// 由既有的每帧幂等补发把**开局物品 / 背包 / 诅咒 / 石化**在「已复活、已 warp」的正确时机一次性补上。
        /// 注意：只解除「首次复活」这一次 —— 之后该玩家的死亡 / 复活回归正常流程（复活继承等照旧）。
        /// </summary>
        private void UpdateJoinSoulSettle(List<Character> characters)
        {
            if (_joinSouls.Count == 0) return;

            foreach (var character in characters)
            {
                var view = character.photonView;
                if (view == null || view.Owner == null) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_joinSouls.Contains(id)) continue;

                if (IsDead(character) || !HostApi.IsInventoryReady(character))
                {
                    // 仍是灵魂 / 尚未就绪：重新计时（这样「短暂存活后又变回死亡」不会误判为已复活）
                    _joinSoulAliveSince.Remove(id);
                    continue;
                }

                if (!_joinSoulAliveSince.TryGetValue(id, out float since))
                {
                    _joinSoulAliveSince[id] = Time.time;
                    continue;
                }
                if (Time.time - since < JoinSoulSettleSeconds) continue;

                _joinSouls.Remove(id);
                _joinSoulAliveSince.Remove(id);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 中途加入：{Nick(character)} 已复活到位，开始补发开局效果（初始物品 / 背包 / 状态）。");
            }
        }

        private void EnsureAssignments(List<Character> characters)
        {
            // 游戏更新可能改变玩家数据结构（例如 2026-10-03 更新把 CharacterData.userID 由 ulong 改成 string），
            // 一旦某处访问失败就会中断整个分配流程、全员无职业，而本机环境的 Unity 日志写入器已失效
            // （BepInEx: Unable to start Unity log writer），异常不会落到日志里。这里兜底记录一次，便于定位。
            try
            {
                // 全部职业名额已满 → 剩余玩家不分配职业（其职业保持为「无」）
                if (_ledger.IsFull()) return;

                foreach (var character in characters)
                {
                    var view = character.photonView;
                    if (view == null || view.Owner == null) continue;

                    string id = _ledger.IdOf(character);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (_ledger.TryGet(id, out _)) continue;

                    var type = _ledger.Assign(character);
                    if (type == ClassType.None) continue;

                    _wasDead[id] = IsTrueDead(character);
                    // 分配职业时仍处于「死亡 / 灵魂」状态 → 中途加入者：先登记，等其
                    // 「复活到位」（见 UpdateJoinSoulSettle）后再发放开局效果，避免被原版复活流程清掉。
                    if (IsDead(character)) _joinSouls.Add(id);

                    // 「状态类」开局效果（宿命 / 积损 / 骸骨 / 极限）不在此施加：
                    // 中途加入的客机先以灵魂形态（已死亡）加入，此时下发会被原版复活流程吞掉，
                    // 改由 EnsureInitialStatuses 等其存活后再施加（物品类同样见 Ensure* / Grant*）。
                    NotifyAssignment(character, type);
                    _displaySyncDirty = true;   // 职业有变动 → 尽快把新快照同步给客机
                }
            }
            catch (System.Exception e)
            {
                if (_assignErrorLogged) return;
                _assignErrorLogged = true;
                Plugin.Log.LogError($"[RollOfTheScout] 分配职业时发生异常（本次仅提示一次）：{e}");
            }
        }

        /// <summary>
        /// 开局「状态类」效果（宿命 / 积损 / 骸骨 / 极限）的幂等补发：**只在玩家存活时**施加一次。
        ///
        /// 为什么不能像以前那样在「分配职业」那一刻就施加：中途加入的客机是由相关模组以
        /// 「先以灵魂形态（已死亡）加入、再复活为人类」的方式实现的 ——
        ///   · 死亡 / 灵魂状态下写进去的石化会被原版复活流程清掉；
        ///   · 该角色的客户端此时尚未就绪，状态还会被迟到初始化（SetAll → PushStatuses）覆盖。
        /// 实测症状：中途加入的挑战者只拿到 60 诅咒、拿不到 70 石化，直到真正死一次再复活才补齐。
        /// 因此这里每帧检查：一旦该玩家复活为「活着」，立刻施加并记账（重复施加是安全的，
        /// 诅咒走绝对赋值、石化走 RPC_SyncPetrify 附加校正）。
        /// </summary>
        private void EnsureInitialStatuses(List<Character> characters)
        {
            foreach (var character in characters)
            {
                var type = _ledger.GetClass(character);
                if (type == ClassType.None) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialStatusDone.Contains(id)) continue;

                // 死亡（含灵魂形态）时不施加，等复活后再来（见上方说明）
                if (IsDead(character)) continue;

                ApplyInitialStatuses(character, type);
                _initialStatusDone.Add(id);
            }
        }

        /// <summary>
        /// 「备战充分」开局背包的幂等补发。
        /// 分配职业那一刻客机的角色可能尚未就绪（AddItem 失败），因此这里每帧重试，
        /// 直到成功为止；失败最多尝试 MaxGrantAttempts 次，避免持续刷警告。
        /// </summary>
        private void EnsureCharacterEffects(List<Character> characters)
        {
            // 职业不在场 → 直接跳过对全体玩家的遍历（判定为 O(1)；旧实现靠遍历账本，人数越多越贵）
            if (!_ledger.HasClassInRoom(ClassType.Logistician)) return;

            int count = Mathf.Max(0, ModConfig.Logistician.InitialBackpacks.Value);
            if (count == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Logistician) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时背包尚未就绪，物品会掉在地上而不是入包
                if (IsDead(character)) continue;

                _initialAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                // 每件物品在「背包 / 喷气背包」之间按权重二选一（默认各 5 = 50%）；
                // 抽中的物品名固定下来再发，失败重试时不会临时改抽到别的物品。
                if (!_initialGrantPlan.TryGetValue(id, out var planned))
                {
                    planned = WeightedPicker.PickMany(BuildLogisticianBackpackPool(), count);
                    if (planned.Count == 0) continue;
                    _initialGrantPlan[id] = planned;
                }

                int ok = 0;
                foreach (var prefabName in planned)
                {
                    if (HostApi.GiveItem(character, prefabName, out _)) ok++;
                }

                if (ok >= planned.Count)
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                    _initialGrantPlan.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 备战充分：{Nick(character)} 开局获得 {string.Join("、", planned)}。");
                }
                else
                {
                    _initialAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 备战充分：{Nick(character)} 物品发放失败，将在后续帧重试（已发 {ok}/{planned.Count}）。");
                    }
                }
            }
        }

        /// <summary>
        /// 刺客·刺杀（开局）：把配置数量的仪式匕首与吹箭筒直接放入该玩家 3 个物品栏。
        /// 与「备战充分」同样是每帧幂等补发：分配那一刻客机的物品栏可能尚未就绪，
        /// 一次性发放会漏发；发满为止，失败最多尝试 MaxGrantAttempts 次。
        /// </summary>
        private void EnsureAssassinItems(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Assassin)) return;

            int daggers = Mathf.Max(0, ModConfig.Assassin.InitDaggerCount.Value);
            int blowguns = Mathf.Max(0, ModConfig.Assassin.InitBlowgunCount.Value);
            if (daggers == 0 && blowguns == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Assassin) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时物品栏尚未就绪，物品会掉在地上而不是入包
                if (IsDead(character)) continue;

                _initialAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                // 抽签结果固定：要发的物品名列表一旦确定便不再改变，失败重试不会改发别的。
                if (!_initialGrantPlan.TryGetValue(id, out var planned))
                {
                    planned = new List<string>();
                    for (int i = 0; i < daggers; i++) planned.Add(ItemNames.RitualDagger);
                    for (int i = 0; i < blowguns; i++) planned.Add(ItemNames.Blowgun);
                    if (planned.Count == 0) continue;
                    _initialGrantPlan[id] = planned;
                }

                int ok = 0;
                foreach (var prefabName in planned)
                {
                    if (HostApi.GiveItemToInventorySlots(character, prefabName, out _)) ok++;
                }

                if (ok >= planned.Count)
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                    _initialGrantPlan.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 刺杀：{Nick(character)} 开局获得 {string.Join("、", ItemCounts(planned))}。");
                }
                else
                {
                    _initialAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 刺杀：{Nick(character)} 初始物品发放失败，将在后续帧重试（已发 {ok}/{planned.Count}）。");
                    }
                }
            }
        }

        /// <summary>
        /// 挑战者·顶级玩家：把配置数量的黄金抓钩（`RescueHook_Infinite`）与滑翔翼（`Glider`）
        /// **强制覆盖**写入该玩家物品栏 1、2（不做空位检查，先清空目标槽位再写入）。
        /// 这样「游玩途中转职为挑战者（物品栏已满）」与「中途加入后从灵魂状态复活」两种情况
        /// 都能拿到物品。每帧幂等补发（死亡/灵魂状态时跳过），成功后记入 <c>_initialApplied</c>。
        /// </summary>
        private void EnsureChallengerItems(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Challenger)) return;

            int hooks = Mathf.Max(0, ModConfig.Challenger.GoldenHookCount.Value);
            int gliders = Mathf.Max(0, ModConfig.Challenger.GliderCount.Value);
            if (hooks == 0 && gliders == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Challenger) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;

                // 死亡（含灵魂状态）时不发放：中途加入的玩家会先以灵魂状态进入游戏，需等其
                // 复活为人类后再强制覆盖物品栏，避免物品在复活流程中被清掉。
                if (IsDead(character)) continue;

                // 抽签结果固定：要发的物品名列表一旦确定便不再改变。
                if (!_initialGrantPlan.TryGetValue(id, out var planned))
                {
                    planned = new List<string>();
                    for (int i = 0; i < hooks; i++) planned.Add(ItemNames.GoldenHook);
                    for (int i = 0; i < gliders; i++) planned.Add(ItemNames.Glider);
                    if (planned.Count == 0) continue;
                    _initialGrantPlan[id] = planned;
                }

                // 强制覆盖（物品栏 1、2）：不做空位检查，先清空目标槽位再写入。
                // 这样「游玩途中转职为挑战者」、「中途加入后从灵魂复活」两种情况下都能拿到物品。
                if (HostApi.ForceGiveItemsToInventorySlots(character, planned))
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                    _initialGrantPlan.Remove(id);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 顶级玩家：{Nick(character)} 强制覆盖物品栏获得 {string.Join("、", ItemCounts(planned))}。");
                }
                else
                {
                    _initialAttempts.TryGetValue(id, out int attempts);
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 顶级玩家：{Nick(character)} 强制发放物品失败，将在后续帧继续重试。");
                    }
                    _initialAttempts[id] = attempts + 1;
                }
            }
        }

        /// <summary>
        /// 仓鼠·我全都要（开局）：为仓鼠直接装备一个背包。
        /// 与后勤「备战充分」同套机制：分配职业那一刻客机的角色/背包槽可能尚未就绪，
        /// 故每帧幂等补发、失败重试（最多 MaxGrantAttempts 次），成功后记入 _initialApplied。
        /// 背包内容为空（仅「装备一个背包」）；若已装备背包则由 HostApi.EquipBackpack 覆盖替换。
        /// </summary>
        private void EnsureHamsterBackpack(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Hamster)) return;

            int count = Mathf.Max(0, ModConfig.Hamster.InitialBackpacks.Value);
            if (count == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Hamster) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时背包槽尚未就绪，背包会掉在地上而不是装备在身上
                if (IsDead(character)) continue;

                _initialAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                // 空背包：装备 count 个（正常只会装备 1 个，多次装备以「覆盖替换」收尾）
                int ok = 0;
                for (int i = 0; i < count; i++)
                {
                    if (HostApi.EquipBackpack(character, new List<string>())) ok++;
                }

                if (ok >= count)
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 我全都要：{Nick(character)} 开局装备了 {count} 个背包。");
                }
                else
                {
                    _initialAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 我全都要：{Nick(character)} 背包发放失败，将在后续帧重试（已发 {ok}/{count}）。");
                    }
                }
            }
        }

        /// <summary>
        /// 童军·【行李】（第 85 批新增）：开局直接装备一个「滑稽背包」`Fannypack`，背包内放
        /// `行李-物品数`（默认 2）个**不重复**的随机物品（取自 `行李-物品池`，默认 32 项）。
        /// 与后勤「备战充分」/ 仓鼠「我全都要」同套幂等补发机制
        /// （`_initialApplied` 记账 + `_initialAttempts` 重试 + <see cref="MaxGrantAttempts"/>），
        /// 死亡 / 灵魂状态不发放（此时背包槽尚未就绪，背包会掉在地上而不是装备在身上）。
        /// </summary>
        private void EnsureScoutLuggage(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Scout)) return;
            if (!ModConfig.Scout.LuggageEnabled.Value) return;

            int count = Mathf.Clamp(ModConfig.Scout.LuggageItemCount.Value, 0, BackpackData.slots);
            var pool = ScoutLuggagePool();

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Scout) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;
                if (IsDead(character)) continue;

                _initialAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                // 从池中「不重复」随机取 count 个（洗牌后取前 count）
                var items = WeightedPicker.PickUnique(pool, count);
                if (items.Count < count)
                {
                    Plugin.Log.LogWarning(
                        $"[RollOfTheScout] 行李：{Nick(character)} 物品池可用项不足（{items.Count}/{count}），按实际数量发放。");
                }

                if (HostApi.EquipBackpack(character, ModConfig.Scout.LuggageBackpack.Value, items))
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 行李：{Nick(character)} 开局装备了滑稽背包，内含 {items.Count} 个物品" +
                        $"（{(items.Count > 0 ? string.Join("、", items) : "空")}）");
                }
                else
                {
                    _initialAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning($"[RollOfTheScout] 行李：{Nick(character)} 背包发放失败，将在后续帧重试。");
                    }
                }
            }
        }

        /// <summary>
        /// 童军·行李「物品池」：按配置字符串缓存解析结果（配置不变则复用，避免每帧 <c>ParseList</c> 的字符串分配）。
        /// 返回的列表**只读**使用（<see cref="WeightedPicker.PickUnique"/> 内部会自行复制，不会改动它）。
        /// </summary>
        private List<string> ScoutLuggagePool()
        {
            string raw = ModConfig.Scout.LuggageItemPool.Value;
            if (raw != _scoutLuggagePoolRaw)
            {
                _scoutLuggagePoolRaw = raw;
                _scoutLuggagePool.Clear();
                _scoutLuggagePool.AddRange(WeightedPicker.ParseList(raw));
            }
            return _scoutLuggagePool;
        }

        /// <summary>
        /// 自恋狂·化妆盒（每层）：每次进入新关卡强制覆盖 3 个物品栏写入配置数量的防晒喷雾。
        /// 用「强制覆盖」而非「优先空位」：需求明确要求覆盖 3 个物品栏，且与挑战者「顶级玩家」同理，
        /// 可覆盖「游玩途中转职为自恋狂（物品栏已满）」与「中途加入后从灵魂状态复活」两种情况。
        /// 每帧幂等补发（_levelGranted 记账 + 失败重试），死亡 / 灵魂 / 物品栏未就绪时跳过。
        /// </summary>
        private void GrantNarcissistSunscreens(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Narcissist)) return;

            int want = Mathf.Max(0, ModConfig.Narcissist.SunscreenCount.Value);
            string item = ModConfig.Narcissist.SunscreenItem.Value;
            if (want == 0 || string.IsNullOrEmpty(item)) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Narcissist) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放（中途加入者先以灵魂加入，物品会被复活流程清掉）
                if (IsDead(character)) continue;
                if (!HostApi.IsInventoryReady(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                // 强制覆盖：先清空前 want 个物品栏槽位再写入（不做空位检查）
                var planned = new List<string>();
                for (int i = 0; i < want; i++) planned.Add(item);

                if (HostApi.ForceGiveItemsToInventorySlots(character, planned))
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 化妆盒：{Nick(character)} 本层强制获得 {want} 个{item}（覆盖物品栏）。");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 化妆盒：{Nick(character)} 防晒喷雾发放失败，将在后续帧重试。");
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // 独狼（LoneWolf）：独行长路
        // ---------------------------------------------------------------

        /// <summary>
        /// 独狼·独行长路：两条按距离互斥的主机定时器。
        ///   · 自身「独行-半径」内没有任何其他存活玩家 → 每「独行-间隔」秒 +额外体力；
        ///   · 自身「同伴-半径」内存在任意其他玩家 → 每「同伴-困倦间隔」秒 +困倦。
        /// 以独狼自身为中心，距离判定用 <see cref="HostApi.GetCharactersInRadius"/>（排除自己与已死者）；
        /// 已死亡 / 昏迷者跳过。
        /// </summary>
        private void TickLoneWolf(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 独行：远离所有其他玩家，每「独行-间隔」秒一次额外体力 ----
            float soloInterval = ModConfig.LoneWolf.SoloIntervalSeconds.Value;
            _loneWolfSoloTimer += dt;
            if (soloInterval > 0f && _loneWolfSoloTimer >= soloInterval)
            {
                _loneWolfSoloTimer = 0f;
                float stamina = ModConfig.LoneWolf.SoloExtraStamina.Value;
                float soloRadius = ModConfig.LoneWolf.SoloRadius.Value;
                if (stamina != 0f)
                {
                    foreach (var wolf in characters)
                    {
                        if (_ledger.GetClass(wolf) != ClassType.LoneWolf) continue;
                        if (IsDead(wolf)) continue;
                        if (HasOtherPlayerWithin(wolf, soloRadius)) continue;
                        HostApi.BoostExtraStamina(wolf, stamina);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 独行长路：{Nick(wolf)} 半径 {soloRadius:0.#} 米内无人，额外体力+{stamina * 100f:0.#}");
                    }
                }
            }

            // ---- 同伴：附近有玩家，每「同伴-困倦间隔」秒一次困倦 ----
            float drowsyInterval = ModConfig.LoneWolf.NearbyDrowsyInterval.Value;
            _loneWolfDrowsyTimer += dt;
            if (drowsyInterval > 0f && _loneWolfDrowsyTimer >= drowsyInterval)
            {
                _loneWolfDrowsyTimer = 0f;
                float drowsy = ModConfig.LoneWolf.NearbyDrowsy.Value;
                float nearbyRadius = ModConfig.LoneWolf.NearbyRadius.Value;
                if (drowsy != 0f)
                {
                    foreach (var wolf in characters)
                    {
                        if (_ledger.GetClass(wolf) != ClassType.LoneWolf) continue;
                        if (IsDead(wolf)) continue;
                        if (!HasOtherPlayerWithin(wolf, nearbyRadius)) continue;
                        HostApi.ApplyStatus(wolf, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 独行长路：{Nick(wolf)} 半径 {nearbyRadius:0.#} 米内有玩家，困倦+{drowsy * 100f:0.#}");
                    }
                }
            }
        }

        /// <summary>
        /// 自身「radius」米内是否存在**其他存活玩家**（不含自己）。
        /// 半径扫描用 <see cref="HostApi.GetCharactersInRadius"/>（含自己与死者，需再过滤）。
        /// </summary>
        private static bool HasOtherPlayerWithin(Character self, float radius)
        {
            if (self == null) return false;
            var nearby = HostApi.GetCharactersInRadius(self.Center, Mathf.Max(0f, radius));
            foreach (var c in nearby)
            {
                if (c == null || c == self) continue;
                if (IsDead(c)) continue;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // 炮手（Gunner）：开炮 / 炸膛
        // ---------------------------------------------------------------

        /// <summary>
        /// 炮手·开炮（每层）：每次进入新关卡自动获得「开炮-每层大炮数」个童子军大炮。
        /// 与自恋狂「化妆盒」等同套「每层发放」机制：用 <c>_levelGranted</c> 记账、每帧幂等补发、失败重试，
        /// 死亡 / 灵魂 / 物品栏未就绪时跳过。
        /// 需求为「优先放入空物品栏」：物品栏无空位时本次不发，后续帧继续尝试（不塞主手）。
        /// </summary>
        private void GrantGunnerCannons(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Gunner)) return;

            int want = Mathf.Max(0, ModConfig.Gunner.LevelCannonCount.Value);
            if (want == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Gunner) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                if (IsDead(character)) continue;
                if (!HostApi.IsInventoryReady(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                int ok = 0;
                for (int i = 0; i < want; i++)
                {
                    if (HostApi.GiveItemToInventorySlots(character, ItemNames.ScoutCannonItem, out _)) ok++;
                    else break;
                }

                if (ok >= want)
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开炮：{Nick(character)} 本层获得 {want} 个童子军大炮 {ItemNames.ScoutCannonItem}。");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 开炮：{Nick(character)} 童子军大炮发放失败（物品栏无空位？），将在后续帧重试。");
                    }
                }
            }
        }

        /// <summary>
        /// 炮手·开炮（定时）：每隔「开炮-大炮间隔」秒额外获得「开炮-大炮数」个童子军大炮。
        /// 优先放入空物品栏（<see cref="HostApi.GiveItemToInventorySlots"/>，不塞主手）。
        /// </summary>
        private void TickGunnerCannon(List<Character> characters)
        {
            float interval = ModConfig.Gunner.CannonIntervalSeconds.Value;
            int count = Mathf.Max(0, ModConfig.Gunner.CannonCount.Value);
            if (interval <= 0f || count == 0) return;

            _gunnerCannonTimer += Time.deltaTime;
            if (_gunnerCannonTimer < interval) return;
            _gunnerCannonTimer = 0f;

            foreach (var gunner in characters)
            {
                if (_ledger.GetClass(gunner) != ClassType.Gunner) continue;
                if (IsDead(gunner)) continue;

                int ok = 0;
                for (int i = 0; i < count; i++)
                {
                    if (HostApi.GiveItemToInventorySlots(gunner, ItemNames.ScoutCannonItem, out _)) ok++;
                }
                if (ok > 0)
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开炮：{Nick(gunner)} 定时获得 {ok} 个童子军大炮 {ItemNames.ScoutCannonItem}。");
            }
        }

        /// <summary>
        /// 炮手·炸膛：把炮手「新受到的损伤」转化为炸药。
        ///
        /// 与 M·「享受」同套做法（硬约束 #5）：被动倍率 / 受伤事件无法直接感知，
        /// 只能「主机轮询同步值 + 对比上一轮快照」——损伤较上一轮增加即视为本次受到损伤。
        /// 负增量（治疗 / 复活清状态）一律忽略；首次见到某角色只登记基线、不发放，
        /// 避免把「分配职业前就已存在的损伤」也算作本次受到。
        /// 发放优先级：空物品栏 → 3 栏全满时塞主手并强制握住（见 <see cref="HostApi.GiveItemForceHold"/>）。
        /// </summary>
        private void PollGunnerInjury(List<Character> characters)
        {
            int count = Mathf.Max(0, ModConfig.Gunner.ExplosiveCount.Value);

            foreach (var character in characters)
            {
                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                bool isGunner = _ledger.GetClass(character) == ClassType.Gunner;
                float now = HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Injury);
                bool had = _gunnerInjurySnapshot.TryGetValue(id, out float prev);

                // 非炮手 / 无基线（首次见到或刚换职业）/ 已死亡或昏迷：只更新快照，不发放
                if (!isGunner || !had || IsDead(character))
                {
                    _gunnerInjurySnapshot[id] = now;
                    continue;
                }

                // 损伤增加 = 受到损伤 → 发放炸药
                if (now - prev > 0.0001f && count > 0)
                {
                    int ok = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (HostApi.GiveItemForceHold(character, ItemNames.Dynamite, out _)) ok++;
                    }
                    if (ok > 0)
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 炸膛：{Nick(character)} 受到损伤（{prev * 100f:0.#} → {now * 100f:0.#}），获得炸药 {ok} 个 {ItemNames.Dynamite}。");
                }

                _gunnerInjurySnapshot[id] = now;
            }
        }

        // ---------------------------------------------------------------
        // 乐队成员（Band）：乐器 / 演奏 / 乐队 / 体弱
        // 【乐器】拾取替换由 Patches.BandPickupPatch 实现；【演奏】的吹响感知由
        // Patches.BandBuglePatch（BugleSFX.RPC_StartToot）回调本类的 OnBandBlowStarted。
        // ---------------------------------------------------------------

        /// <summary>
        /// 乐队成员·乐器：每隔「乐器-检测间隔」秒扫描自身 3 个物品栏中身背的乐器，
        /// 按「小号 / 童军领队的号角 / 友谊喇叭」各自数量结算额外体力；
        /// 三种乐器齐全时，额外再给一次加速（复用先驱·高代谢的加速通道）。
        /// </summary>
        private void DoBandInstrumentBonus(List<Character> characters)
        {
            float perBugle = ModConfig.Band.BugleExtraStamina.Value;
            float perScout = ModConfig.Band.ScoutmasterExtraStamina.Value;
            float perMagic = ModConfig.Band.MagicExtraStamina.Value;

            foreach (var band in characters)
            {
                if (_ledger.GetClass(band) != ClassType.Band) continue;
                if (IsDead(band)) continue;

                int bugles = CountItemInInventory(band, ItemNames.Bugle);
                int scouts = CountItemInInventory(band, ItemNames.BugleScoutmaster);
                int magics = CountItemInInventory(band, ItemNames.BugleMagic);

                float stamina = bugles * perBugle + scouts * perScout + magics * perMagic;
                if (stamina != 0f) HostApi.BoostExtraStamina(band, stamina);

                bool allThree = bugles > 0 && scouts > 0 && magics > 0;
                if (allThree) GrantSprint(band, "乐器", clearDrowsyAfter: true);

                if (bugles + scouts + magics > 0)
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 乐器：{Nick(band)} 身背 小号×{bugles} 号角×{scouts} 友谊喇叭×{magics}，" +
                        $"额外体力+{stamina * 100f:0.#}" + (allThree ? "，三种齐全获得一次加速。" : "。"));
                }
            }
        }

        /// <summary>
        /// 乐队成员·乐队：每隔「乐队-间隔」秒，统计自身「乐队-半径」内**其他乐队成员**数量 N。
        /// N ≥ 1 时：+「乐队-额外体力」×N 额外体力、-「乐队-清除困倦」×N 困倦，
        /// 并把自身【演奏】半径加成设为 min(「乐队-半径加成」×N, 「乐队-半径加成上限」)，
        /// 该加成持续到下一次结算（N = 0 时复位为 0）。
        /// </summary>
        private void DoBandResonance(List<Character> characters)
        {
            float radius = ModConfig.Band.GroupRadius.Value;
            float perStamina = ModConfig.Band.GroupExtraStamina.Value;
            float perDrowsy = ModConfig.Band.GroupClearDrowsy.Value;
            float perBonus = ModConfig.Band.GroupRadiusBonus.Value;
            float bonusCap = ModConfig.Band.GroupRadiusBonusCap.Value;

            foreach (var band in characters)
            {
                if (_ledger.GetClass(band) != ClassType.Band) continue;
                string id = _ledger.IdOf(band);
                if (string.IsNullOrEmpty(id)) continue;

                int n = 0;
                if (!IsDead(band))
                {
                    var nearby = HostApi.GetCharactersInRadius(band.Center, Mathf.Max(0f, radius));
                    foreach (var other in nearby)
                    {
                        if (other == null || other == band) continue;
                        if (IsDead(other)) continue;
                        if (_ledger.GetClass(other) != ClassType.Band) continue;
                        n++;
                    }
                }

                // 每次结算都重写半径加成：N = 0 即复位为 0，实现「持续到下次结算」的语义。
                float bonus = n > 0 ? Mathf.Min(perBonus * n, bonusCap) : 0f;
                _bandRadiusBonus[id] = bonus;

                if (n <= 0) continue;

                if (perStamina != 0f) HostApi.BoostExtraStamina(band, perStamina * n);
                if (perDrowsy != 0f)
                    HostApi.ApplyStatus(band, CharacterAfflictions.STATUSTYPE.Drowsy, -perDrowsy * n);

                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 乐队：{Nick(band)} 附近有 {n} 名乐队成员，额外体力+{perStamina * n * 100f:0.#}、" +
                    $"清除困倦{perDrowsy * n * 100f:0.#}、演奏半径+{bonus:0.#} 米。");
            }
        }

        /// <summary>
        /// 乐队成员·转职（补充效果）：长期「身背乐器」并待在乐队成员身边的玩家有概率直接转职为乐队成员。
        ///
        /// 判定（每「转职-检测间隔」秒执行一次，默认 30 秒）：
        ///   ① 该玩家（非乐队成员、存活）的 3 个物品栏中至少有 1 格是「转职-物品」里的乐器
        ///      （默认小号 / 童军领队的号角 / 友谊喇叭）；
        ///   ② 该玩家「转职-半径」（默认 5 米）内存在**其他**存活乐队成员。
        /// ① + ② 连续满足「转职-连续次数」次（默认 2，即约 2 × 30 = 60 秒以上）后才进入概率判定；
        /// 一旦某次不满足就把连续计数清零、不做任何判定（节省性能）。进入判定后每轮以
        /// 「转职-概率」（默认 10%）掷一次，命中即立即转职为乐队成员。
        ///
        /// 改职走与「转职 / 待皓·模因污染」完全相同的收尾（<see cref="ApplyClassChange"/>）：
        /// 清空全部负面状态、并按新职业重新补发开局类效果；使用 <see cref="ClassLedger.ForceAssign"/>
        /// 绕过「最大人数」限制（被动效果不应因名额占满而静默失效），且不消耗「转职-次数」。
        /// </summary>
        private void DoBandTransferChance(List<Character> characters)
        {
            if (!ModConfig.Band.TransferEnabled.Value) return;

            var items = BandTransferItems();
            if (items.Count == 0) return;

            float chance = Mathf.Clamp01(ModConfig.Band.TransferChance.Value);
            float radius = Mathf.Max(0f, ModConfig.Band.TransferRadius.Value);
            int required = Mathf.Max(1, ModConfig.Band.TransferStreakRequired.Value);

            foreach (var character in characters)
            {
                if (character == null || character.photonView == null) continue;
                if (IsDead(character)) continue;                                // 死亡 / 昏迷者不参与
                if (_ledger.GetClass(character) == ClassType.Band) continue;    // 只作用于「除乐队成员以外」的玩家

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                bool qualified = PlayerHasAnyInventoryItem(character, items)
                    && HasOtherBandMemberNearby(character, radius);

                if (!qualified)
                {
                    // 条件中断：清零连续计数，本次不做任何判定
                    _bandTransferStreak[id] = 0;
                    continue;
                }

                _bandTransferStreak.TryGetValue(id, out int streak);
                streak++;
                _bandTransferStreak[id] = streak;
                if (streak < required) continue;   // 连续满足次数不足：不做判定

                if (chance <= 0f) continue;
                if (UnityEngine.Random.value >= chance) continue;

                ClassType oldType = _ledger.GetClass(character);
                if (!_ledger.ForceAssign(character, ClassType.Band)) continue;

                ApplyClassChange(character, id, oldType, ClassType.Band);
                _bandTransferStreak[id] = 0;

                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 乐队·转职：{Nick(character)} 身背乐器且附近有乐队成员（已连续 {streak} 次检测），" +
                    $"职业 {ClassLedger.Describe(oldType)} → 乐队成员（{chance * 100f:0.#}% 判定命中）。");
                ShowToast($"{Nick(character)} 乐队·转职：{ClassLedger.Describe(oldType)} → 乐队成员");
            }
        }

        /// <summary>解析并缓存「转职-物品」配置串（英文逗号分隔的预制体名）。</summary>
        private HashSet<string> BandTransferItems()
        {
            string raw = ModConfig.Band.TransferItems.Value;
            if (raw != _bandTransferItemsRaw)
            {
                _bandTransferItemsRaw = raw;
                _bandTransferItems.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _bandTransferItems.Add(name);
            }
            return _bandTransferItems;
        }

        /// <summary>
        /// 该角色 3 个物品栏中是否至少有 1 格属于给定的预制体名集合（不看背包槽）。
        /// 按 itemID 反查预制体名比对，避免受实例化后对象名带 (Clone) 后缀的影响。
        /// </summary>
        private static bool PlayerHasAnyInventoryItem(Character character, HashSet<string> prefabNames)
        {
            if (character == null || character.player == null) return false;
            if (prefabNames == null || prefabNames.Count == 0) return false;

            var slots = character.player.itemSlots;
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty()) continue;
                var item = slot.prefab;
                if (item == null) continue;
                if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) continue;
                if (prefabNames.Contains(prefab.gameObject.name)) return true;
            }
            return false;
        }

        /// <summary>该角色「半径」内是否存在**其他**存活乐队成员（<see cref="DoBandTransferChance"/> 的判定条件之一）。</summary>
        private bool HasOtherBandMemberNearby(Character character, float radius)
        {
            foreach (var other in HostApi.GetCharactersInRadius(character.Center, radius))
            {
                if (other == null || other == character) continue;
                if (IsDead(other)) continue;
                if (_ledger.GetClass(other) != ClassType.Band) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 乐队成员·演奏（吹响）：由 <see cref="Patches.BandBuglePatch"/> 在
        /// <c>BugleSFX.RPC_StartToot</c>（RpcTarget.All，主机可收到）时回调，
        /// 并传入所吹乐器的**预制体名**（只处理小号 / 童军领队的号角 / 友谊喇叭，其它乐器忽略）。
        ///   · 有 1 个小号：对「演奏-单次半径」内其他玩家施加一次额外体力
        ///     （同一吹奏者每「演奏-冷却」秒最多结算一次，避免快速连点刷加成）。
        ///   · 有 1 个童军领队的号角：为**自身**增加「演奏-号角自身额外体力」。
        ///   · 三种乐器统一计数：每累计「演奏-困倦每当吹响次数」次，为**自身**施加「演奏-困倦量」困倦。
        /// </summary>
        public void OnBandBlowStarted(Character character, string itemName)
        {
            if (!_inRun) return;
            if (character == null) return;
            if (!ModConfig.Band.Enabled.Value) return;
            if (_ledger.GetClass(character) != ClassType.Band) return;
            if (string.IsNullOrEmpty(itemName)) return;

            // 只处理三种「乐队乐器」；其它乐器（或物品名无法解析）一律忽略
            if (itemName != ItemNames.Bugle
                && itemName != ItemNames.BugleScoutmaster
                && itemName != ItemNames.BugleMagic) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            // 「每当吹响次数」困倦：三种乐器统一计数，每满 N 次给自身一次困倦
            _bandBlowCount.TryGetValue(id, out int blows);
            blows++;
            _bandBlowCount[id] = blows;

            int every = ModConfig.Band.DrowsyEveryBlows.Value;
            float drowsy = ModConfig.Band.DrowsyPerBlows.Value;
            if (every > 0 && drowsy != 0f && blows % every == 0)
            {
                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 演奏：{Nick(character)} 累计吹响 {blows} 次，自身困倦+{drowsy * 100f:0.#}");
            }

            // 童军领队的号角：为自身增加额外体力
            if (itemName == ItemNames.BugleScoutmaster)
            {
                float hornStamina = ModConfig.Band.HornSelfExtraStamina.Value;
                if (hornStamina != 0f)
                {
                    HostApi.BoostExtraStamina(character, hornStamina);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 演奏：{Nick(character)} 吹响号角，自身额外体力+{hornStamina * 100f:0.#}");
                }
            }

            // 小号：对半径内其他玩家施加单次额外体力（同一吹奏者每「演奏-冷却」秒最多一次）
            if (itemName != ItemNames.Bugle) return;

            float cooldown = ModConfig.Band.BlowCooldown.Value;
            if (cooldown > 0f && _bandBlowCooldownUntil.TryGetValue(id, out float until) && Time.time < until) return;
            if (cooldown > 0f) _bandBlowCooldownUntil[id] = Time.time + cooldown;

            float stamina = ModConfig.Band.BlowExtraStamina.Value;
            if (stamina == 0f) return;

            float radius = ModConfig.Band.BlowRadius.Value + BandRadiusBonusOf(id);
            int n = ApplyBandAura(character, radius, stamina, 0f);
            if (n > 0)
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 演奏：{Nick(character)} 吹响小号，半径 {radius:0.#} 米内 {n} 名玩家 额外体力+{stamina * 100f:0.#}");
        }

        /// <summary>
        /// 乐队成员·Tick：每帧调用。
        ///   · 【乐器】：按「乐器-检测间隔」扫描物品栏乐器。
        ///   · 【乐队】：按「乐队-间隔」结算附近乐队成员加成。
        /// </summary>
        private void TickBand(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 乐器·检测 ----
            float instInterval = ModConfig.Band.InstrumentIntervalSeconds.Value;
            _bandInstrumentTimer += dt;
            if (instInterval > 0f && _bandInstrumentTimer >= instInterval)
            {
                _bandInstrumentTimer = 0f;
                DoBandInstrumentBonus(characters);
            }

            // ---- 乐队·共鸣 ----
            float groupInterval = ModConfig.Band.GroupIntervalSeconds.Value;
            _bandGroupTimer += dt;
            if (groupInterval > 0f && _bandGroupTimer >= groupInterval)
            {
                _bandGroupTimer = 0f;
                DoBandResonance(characters);
            }

            // ---- 乐队·转职（补充效果）----
            float transferInterval = ModConfig.Band.TransferIntervalSeconds.Value;
            _bandTransferTimer += dt;
            if (transferInterval > 0f && _bandTransferTimer >= transferInterval)
            {
                _bandTransferTimer = 0f;
                DoBandTransferChance(characters);
            }
        }

        /// <summary>取某乐队成员当前的【演奏】半径加成（米），未登记时为 0。</summary>
        private float BandRadiusBonusOf(string id)
        {
            return _bandRadiusBonus.TryGetValue(id, out float bonus) ? bonus : 0f;
        }

        /// <summary>
        /// 【演奏】的通用作用：对以 <paramref name="source"/> 为中心、<paramref name="radius"/> 米内的
        /// **其他存活玩家**施加额外体力（<paramref name="drowsy"/> 非 0 时附带困倦），返回命中人数（不含自己）。
        /// 已死亡 / 昏迷者跳过。
        /// </summary>
        private static int ApplyBandAura(Character source, float radius, float stamina, float drowsy)
        {
            if (source == null) return 0;
            var targets = HostApi.GetCharactersInRadius(source.Center, Mathf.Max(0f, radius));
            int n = 0;
            foreach (var target in targets)
            {
                if (target == null || target == source) continue;
                if (IsDead(target)) continue;

                if (stamina != 0f) HostApi.BoostExtraStamina(target, stamina);
                if (drowsy != 0f) HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                n++;
            }
            return n;
        }

        /// <summary>
        /// 统计该角色 3 个物品栏中「指定预制体」的物品数量（不看背包槽）。
        /// 按 itemID 比对，避免受实例化后对象名带 (Clone) 后缀的影响。
        /// </summary>
        private static int CountItemInInventory(Character character, string prefabName)
        {
            if (character == null || character.player == null) return 0;
            if (string.IsNullOrEmpty(prefabName)) return 0;
            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null) return 0;

            var slots = character.player.itemSlots;
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty()) continue;
                var item = slot.prefab;
                if (item != null && item.itemID == itemPrefab.itemID) n++;
            }
            return n;
        }

        /// <summary>
        /// 自恋狂·精致 / 化学中毒：每「用完」1 瓶防晒喷雾时，为自恋狂 +额外体力、+石化。
        ///
        /// 两条触发通道（互为备份，去重窗口见 <see cref="NarcissistSunscreenDedupWindow"/>）：
        /// 1. <c>Action_ReduceUses.ReduceUsesRPC</c> 的 Harmony 补丁（<see cref="Patches.SunscreenUsePatch"/>）——
        ///    防晒喷雾的「剩余使用次数」降到 0 时触发，等价于「用完 1 瓶」；
        /// 2. 游戏自身的全局事件 <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="HandleItemConsumed"/>）——
        ///    瓶子用完后若原版开启 <c>consumeOnFullyUsed</c>，会走 <c>Item.Consume</c> 广播，主机也收得到。
        /// </summary>
        public void OnNarcissistSunscreenUsedUp(Character character)
        {
            if (!_inRun) return;
            if (character == null) return;
            if (_ledger.GetClass(character) != ClassType.Narcissist) return;

            // 去重：同一次「用完」不要被两条通道结算两次
            string id = _ledger.IdOf(character);
            if (!string.IsNullOrEmpty(id))
            {
                if (_narcissistSunscreenGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
                _narcissistSunscreenGuardUntil[id] = Time.time + NarcissistSunscreenDedupWindow;
            }

            float stamina = ModConfig.Narcissist.RefinedStamina.Value;
            if (stamina != 0f) HostApi.BoostExtraStamina(character, stamina);

            float petrify = ModConfig.Narcissist.ChemicalPetrify.Value;
            if (petrify > 0f)
                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Petrify, petrify);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 精致/化学中毒：{Nick(character)} 用完 1 瓶防晒喷雾，额外体力+{stamina * 100f:0.#}、石化+{petrify * 100f:0.#}");
        }

        /// <summary>
        /// 自恋狂「防晒喷雾用完」的食用通道兜底：全局食用事件（<c>GlobalEvents.OnItemConsumed</c>）里
        /// 若发现该物品是配置的防晒喷雾，则交由 <see cref="OnNarcissistSunscreenUsedUp"/> 结算（含去重）。
        /// 用 itemID → 预制体名比对，避免实例对象名带 (Clone) 后缀的干扰。
        /// </summary>
        public void OnNarcissistSunscreenConsumed(Item item, Character character)
        {
            if (item == null || character == null) return;
            if (!ModConfig.Narcissist.Enabled.Value) return;

            string trigger = ModConfig.Narcissist.SunscreenItem.Value;
            if (string.IsNullOrEmpty(trigger)) return;
            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            if (prefab.gameObject.name != trigger) return;

            OnNarcissistSunscreenUsedUp(character);
        }

        /// <summary>
        /// 自恋狂·精致（补充）：自身每次「获得防晒状态」时 +额外体力。
        ///
        /// 感知通道：补丁 <see cref="Patches.NarcissistSunscreenStatusPatch"/> 挂原版
        /// <c>Affliction_Sunscreen.OnApplied</c>（首次套上）与 <c>Affliction_Sunscreen.Stack</c>（再次套上刷新），
        /// 这两条合起来恰好覆盖「每一次获得防晒状态」，且主机对远端角色是经 <c>SyncAfflictionsRPC</c>
        /// 同步而来（<c>AddAffliction(fromRPC:true)</c> / <c>Stack</c>），因此零客机安装也能感知。
        /// 每次获得（含刷新）都会触发，但受「精致-状态额外体力冷却」限制：防晒状态在持续期间
        /// 会被反复刷新（每次刷新都会触发一次「获得」），冷却是为了避免额外体力被高频刷出。
        /// </summary>
        public void OnNarcissistSunscreenStatusGained(Character character)
        {
            if (!_inRun) return;
            if (character == null) return;
            if (_ledger.GetClass(character) != ClassType.Narcissist) return;

            float stamina = ModConfig.Narcissist.SunscreenStatusStamina.Value;
            if (stamina == 0f) return;

            // 冷却：同一玩家在「精致-状态额外体力冷却」秒内只结算一次（设为 0 或负数则不限频）
            string id = _ledger.IdOf(character);
            float cooldown = ModConfig.Narcissist.SunscreenStatusCooldown.Value;
            if (cooldown > 0f && !string.IsNullOrEmpty(id))
            {
                if (_narcissistStatusCooldownUntil.TryGetValue(id, out float until) && Time.time < until) return;
                _narcissistStatusCooldownUntil[id] = Time.time + cooldown;
            }

            HostApi.BoostExtraStamina(character, stamina);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 精致：{Nick(character)} 获得防晒状态，额外体力+{stamina * 100f:0.#}");
        }

        /// <summary>
        /// 救火队员·火线救援（触发）：某名玩家进入昏迷（倒地，非死亡）时，为**每名存活救火队员**
        /// 安排一次延迟「火线救援-延迟」秒的救援任务（目标即该名昏迷玩家）。
        /// 由 <see cref="PollDownedTransitions"/> 检测「昏迷状态 false → true」的跳变后逐名昏迷玩家调用。
        /// </summary>
        private void OnPlayerDowned(List<Character> characters, Character downedTarget)
        {
            if (downedTarget == null) return;
            string targetId = _ledger.IdOf(downedTarget);
            if (string.IsNullOrEmpty(targetId)) return;

            float delay = Mathf.Max(0f, ModConfig.Firefighter.RescueDelaySeconds.Value);
            float cooldown = Mathf.Max(0f, ModConfig.Firefighter.RescueCooldownSeconds.Value);

            foreach (var firefighter in characters)
            {
                if (_ledger.GetClass(firefighter) != ClassType.Firefighter) continue;
                // 自身已倒地 / 死亡时不安排（无法前往救援）
                if (IsDead(firefighter)) continue;
                // 自身正背着玩家时无法触发本效果（腾不出手去救援）
                if (firefighter.data != null && firefighter.data.carriedPlayer != null) continue;

                string id = _ledger.IdOf(firefighter);
                if (string.IsNullOrEmpty(id)) continue;

                // 触发冷却：同一救火队员在「火线救援-冷却」内最多触发一次
                if (cooldown > 0f && _rescueCooldownUntil.TryGetValue(id, out float until) && Time.time < until)
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 火线救援：{Nick(firefighter)} 冷却未结束，跳过对 {Nick(downedTarget)} 的救援。");
                    continue;
                }

                _rescuePending[id] = new RescueTask { TargetId = targetId, FireAt = Time.time + delay };
                if (cooldown > 0f) _rescueCooldownUntil[id] = Time.time + cooldown;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 火线救援：{Nick(firefighter)} 将在 {delay:0.#} 秒后前往救援昏迷的 {Nick(downedTarget)}。");
            }
        }

        /// <summary>
        /// 救火队员·火线救援（执行）：每帧调用。
        ///   ① 延迟任务到点后（目标仍昏迷、自身仍存活）→ 施加一次无敌效果（奶白金）并传送到目标处，
        ///      转入「等待背起」；目标中途被救起 / 死亡则丢弃任务。
        ///   ② 等待背起：救火队员背起**该名**昏迷玩家（<c>data.carriedPlayer</c> 稳定标识 == 目标）后，
        ///      立即一次性给予加速与「火线救援-额外体力」，只结算一次。
        /// </summary>
        private void TickRescue(List<Character> characters)
        {
            // ---- ① 延迟任务 ----
            if (_rescuePending.Count > 0)
            {
                List<string> done = null;
                foreach (var firefighter in characters)
                {
                    if (_ledger.GetClass(firefighter) != ClassType.Firefighter) continue;
                    string id = _ledger.IdOf(firefighter);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!_rescuePending.TryGetValue(id, out RescueTask task)) continue;

                    if (IsDead(firefighter)) { (done ??= new List<string>()).Add(id); continue; }
                    if (Time.time < task.FireAt) continue;

                    var target = FindCharacterById(characters, task.TargetId);
                    if (target == null || !IsDowned(target)) { (done ??= new List<string>()).Add(id); continue; }

                    string item = ModConfig.Firefighter.RescueInvincibleItem.Value;
                    if (!string.IsNullOrEmpty(item)) FeedTransientItem(firefighter, item, "火线救援");
                    firefighter.photonView.RPC("WarpPlayerRPC", RpcTarget.All, target.Center, false);

                    _rescueAwaitCarry[id] = task.TargetId;
                    (done ??= new List<string>()).Add(id);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 火线救援：{Nick(firefighter)} 传送到昏迷的 {Nick(target)} 处，等待背起。");
                }
                if (done != null) foreach (var id in done) _rescuePending.Remove(id);
            }

            // ---- ② 等待背起 ----
            if (_rescueAwaitCarry.Count > 0)
            {
                List<string> done = null;
                foreach (var firefighter in characters)
                {
                    if (_ledger.GetClass(firefighter) != ClassType.Firefighter) continue;
                    string id = _ledger.IdOf(firefighter);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!_rescueAwaitCarry.TryGetValue(id, out string targetId)) continue;

                    if (IsDead(firefighter)) { (done ??= new List<string>()).Add(id); continue; }

                    // 背起了目标昏迷玩家 → 一次性结算
                    var carried = firefighter.data != null ? firefighter.data.carriedPlayer : null;
                    if (carried != null && _ledger.IdOf(carried) == targetId)
                    {
                        GrantSprint(firefighter, "火线救援", clearDrowsyAfter: true,
                            clearAmount: ModConfig.Firefighter.RescueSprintDrowsyClearAmount.Value);
                        float extra = ModConfig.Firefighter.RescueExtraStamina.Value;
                        if (extra != 0f) HostApi.BoostExtraStamina(firefighter, extra);
                        (done ??= new List<string>()).Add(id);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 火线救援：{Nick(firefighter)} 背起昏迷的 {Nick(carried)}，获得一次加速与额外体力+{extra * 100f:0.#}。");
                        continue;
                    }

                    // 目标已被救起 / 死亡 → 放弃
                    var target = FindCharacterById(characters, targetId);
                    if (target == null || !IsDowned(target)) { (done ??= new List<string>()).Add(id); }
                }
                if (done != null) foreach (var id in done) _rescueAwaitCarry.Remove(id);
            }
        }

        /// <summary>按稳定标识在当前角色列表中查回角色对象（用于延迟任务 / 等待背起的目标解析）。</summary>
        private Character FindCharacterById(List<Character> characters, string id)
        {
            if (string.IsNullOrEmpty(id) || characters == null) return null;
            foreach (var c in characters)
            {
                if (c == null) continue;
                if (_ledger.IdOf(c) == id) return c;
            }
            return null;
        }

        /// <summary>「昏迷（倒地，未死亡）」判定：完全昏迷且尚未真正死亡（用于火线救援的触发与抵达判定）。</summary>
        public static bool IsDowned(Character character)
        {
            return character != null && character.data != null
                && character.data.fullyPassedOut && !character.data.dead;
        }

        /// <summary>
        /// 救火队员·温暖臂膀：背起玩家（<c>data.carriedPlayer != null</c>）时，
        /// 每「温暖臂膀-间隔」秒为**自身**与**他正背着的玩家**各削减一次损伤。
        /// 只有「救援者」与「被救援者」两人受益（不再按半径治疗周围玩家）；
        /// 已真正死亡者跳过（被背着的昏迷队友可被治疗 —— 他们正是背人的目的）。
        /// </summary>
        private void DoFirefighterWarmShoulder(Character firefighter)
        {
            if (firefighter == null || firefighter.data == null) return;

            var carried = firefighter.data.carriedPlayer;
            if (carried == null) return;

            float amount = ModConfig.Firefighter.WarmHealInjury.Value;
            if (amount <= 0f) return;

            int n = 0;

            // 自身
            if (!IsTrueDead(firefighter))
            {
                HostApi.ApplyStatus(firefighter, CharacterAfflictions.STATUSTYPE.Injury, -amount);
                n++;
            }

            // 被背着的玩家（昏迷中的队友）
            if (!IsTrueDead(carried))
            {
                HostApi.ApplyStatus(carried, CharacterAfflictions.STATUSTYPE.Injury, -amount);
                n++;
            }

            if (n > 0)
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 温暖臂膀：{Nick(firefighter)} 背起 {Nick(carried)}，为 {n} 人各 -{amount * 100f:0.#} 损伤。");
            }
        }

        /// <summary>
        /// 救火队员·火线救援的「进入昏迷」感知：按「通用·状态轮询间隔」比较各角色
        /// <c>data.fullyPassedOut &amp;&amp; !data.dead</c> 与上次采样快照，出现 false → true 跳变即视为「有玩家进入昏迷」。
        /// 首次观测只登记基线（与 _wasDead 同套思路），避免开局把已倒地者误判为「刚昏迷」。
        /// </summary>
        private void PollDownedTransitions(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Firefighter)) return;

            // 按 DownedPollInterval 采样（默认 0.15s），不再每帧对全体玩家检查一遍。
            _downedPollTimer += Time.deltaTime;
            if (_downedPollTimer < DownedPollInterval) return;
            _downedPollTimer = 0f;

            List<Character> justDowned = null;
            foreach (var character in characters)
            {
                var view = character.photonView;
                if (view == null || view.Owner == null) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                bool downed = IsDowned(character);
                if (_wasDowned.TryGetValue(id, out bool was))
                {
                    if (was == downed) continue;   // 状态未变：不必写回字典（旧实现每帧都写，属无谓开销）
                    _wasDowned[id] = downed;
                    if (!was && downed) (justDowned ??= new List<Character>()).Add(character);
                }
                else
                {
                    _wasDowned[id] = downed;       // 首次登记：与旧实现一致，本帧不触发
                }
            }

            if (justDowned == null) return;
            foreach (var downedChar in justDowned) OnPlayerDowned(characters, downedChar);
        }

        /// <summary>把物品名列表压缩成「名称×数量」形式，仅用于日志。</summary>
        private static List<string> ItemCounts(List<string> names)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var name in names)
            {
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }
            var result = new List<string>();
            foreach (var name in order)
            {
                result.Add(counts[name] > 1 ? $"{name}×{counts[name]}" : name);
            }
            return result;
        }

        /// <summary>
        /// 开局职业提示。仅能在「本机（主机）」屏幕上显示 —— 其他玩家未安装模组，
        /// 原版没有任何能携带任意文本到远端客户端 UI 的网络接口（详见技术文档 §8）。
        /// </summary>
        private void NotifyAssignment(Character character, ClassType type)
        {
            ShowToast($"{Nick(character)} 的职业：{ClassLedger.Describe(type)}");
        }

        private static string Nick(Character character)
        {
            var owner = character != null && character.photonView != null ? character.photonView.Owner : null;
            return owner != null ? owner.NickName : "?";
        }

        /// <summary>
        /// 开局「状态类」效果（宿命 / 积损），在分配职业时一次性施加。
        /// 物品类效果不在此处理（见 EnsureCharacterEffects / GrantLevelItems）。
        /// </summary>
        private void ApplyInitialStatuses(Character character, ClassType type)
        {
            switch (type)
            {
                case ClassType.Leader:
                {
                    // 负面·宿命：开局初始诅咒 + 石化，并附带额外体力
                    float curse = ModConfig.Leader.InitCurseAmount.Value;
                    float petrify = ModConfig.Leader.InitPetrifyAmount.Value;
                    float stamina = ModConfig.Leader.InitExtraStamina.Value;

                    GrantInitialCurse(character, curse);
                    GrantInitialPetrify(character, petrify);
                    if (stamina != 0f)
                        HostApi.BoostExtraStamina(character, stamina);

                    Plugin.Log.LogInfo($"[RollOfTheScout] 宿命：{Nick(character)} 诅咒+{curse}，石化+{petrify}，额外体力+{stamina}");
                    break;
                }

                case ClassType.Logistician:
                {
                    // 负面·积损：开局初始诅咒 + 石化（背包由 EnsureCharacterEffects 发放）
                    float curse = ModConfig.Logistician.InitCurseAmount.Value;
                    float petrify = ModConfig.Logistician.InitPetrifyAmount.Value;
                    GrantInitialCurse(character, curse);
                    GrantInitialPetrify(character, petrify);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 积损：{Nick(character)} 诅咒+{curse}，石化+{petrify}");
                    break;
                }

                case ClassType.Necromancer:
                {
                    // 负面·骸骨：开局初始诅咒（骸骨之书为每层发放，见 GrantNecromancerBooks）
                    float curse = ModConfig.Necromancer.InitCurseAmount.Value;
                    GrantInitialCurse(character, curse);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 骸骨：{Nick(character)} 诅咒+{curse}");
                    break;
                }

                case ClassType.Challenger:
                {
                    // 负面·极限：开局初始诅咒 + 石化（黄金抓钩 / 滑翔翼由 EnsureChallengerItems 发放）
                    float curse = ModConfig.Challenger.InitCurseAmount.Value;
                    float petrify = ModConfig.Challenger.InitPetrifyAmount.Value;
                    GrantInitialCurse(character, curse);
                    GrantInitialPetrify(character, petrify);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 极限：{Nick(character)} 诅咒+{curse}，石化+{petrify}");
                    break;
                }

                case ClassType.Pacifist:
                {
                    // 负面·和平？：开局初始诅咒（右键改踢人由 Patches.PacifistReachPatch 实现）
                    float curse = ModConfig.Pacifist.InitCurseAmount.Value;
                    GrantInitialCurse(character, curse);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 和平？：{Nick(character)} 诅咒+{curse}");
                    break;
                }

                case ClassType.Narcissist:
                {
                    // 负面·精致：开局自带初始诅咒（防晒喷雾由 GrantNarcissistSunscreens 每层发放）
                    float curse = ModConfig.Narcissist.RefinedInitCurse.Value;
                    GrantInitialCurse(character, curse);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 精致：{Nick(character)} 诅咒+{curse}");
                    break;
                }

                case ClassType.Band:
                {
                    // 负面·体弱：开局自带初始诅咒（乐器拾取替换由 Patches.BandPickupPatch 实现）
                    float curse = ModConfig.Band.FrailInitCurse.Value;
                    GrantInitialCurse(character, curse);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 体弱：{Nick(character)} 诅咒+{curse}");
                    break;
                }

                case ClassType.MilesSoul:
                {
                    // 负面·受诅残魂：开局自带初始诅咒，并立即记录一次「最新诅咒值」（诅咒保护的基准）。
                    float curse = ModConfig.MilesSoul.InitCurse.Value;
                    GrantInitialCurse(character, curse);
                    RecordMilesCurse(character, curse);

                    // 补上「本层」的 +5：转职 / 中途加入成为残魂者不会经过 `_pendingNewLevel` 处理块
                    //（该块只在「进入新关卡」那一帧结算，彼时他们还不是残魂）→ 否则会少一次每层诅咒。
                    // 开局就分配为残魂的玩家，本帧 `_pendingNewLevel` 为真，由 OnMilesSoulNewLevel 统一结算，故此处跳过。
                    float perLevel = ModConfig.MilesSoul.CursePerLevel.Value;
                    if (!_pendingNewLevel && perLevel > 0f && !MilesNoCurseLevel())
                    {
                        HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Curse, perLevel);
                        RecordMilesCurse(character);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 众魂阻挠：{Nick(character)} 转职 / 中途加入成为残魂，补发本层诅咒 +{perLevel * 100f:0.#}。");
                    }

                    Plugin.Log.LogInfo($"[RollOfTheScout] 受诅残魂：{Nick(character)} 诅咒+{curse * 100f:0.#}（已记录为保护基准）");
                    break;
                }
            }
        }

        /// <summary>
        /// 开局「宿命 / 积损」的诅咒施加。
        ///
        /// 诅咒走的是状态数组；而客机（尤其中途加入者）在收到职业分配之后还会执行一次
        /// ApplyReconnectData → SetAll(自身状态) → PushStatuses，把主机刚下发的诅咒覆盖回原值。
        /// 石化虽然存在 CharacterData.petrifyAmount、额外体力是独立字段，不受 SetAll 影响，
        /// 但石化当年走的是「状态数组增量」通道，同样会在客机未就绪时被静默丢弃（见 GrantInitialPetrify）。
        ///
        /// 因此这里：① 用「绝对赋值（幂等）」下发；② 登记一笔待校正记录，
        /// 由 <see cref="TickInitialCurseGrant"/> 在随后数秒内再校正两次，
        /// 覆盖客机迟到初始化造成的覆盖（重复下发不会累加，幂等安全）。
        /// </summary>
        private void GrantInitialCurse(Character character, float curse)
        {
            if (curse <= 0f || character == null) return;

            HostApi.SetStatusAbsolute(character, CharacterAfflictions.STATUSTYPE.Curse, curse);

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            _initialCurse[id] = new InitialCurseGrant
            {
                Target = curse,
                NextCheck = Time.time + InitialCurseRecheckDelay,
                Deadline = Time.time + 20f,
            };
        }

        /// <summary>
        /// 开局诅咒的反复校正（与石化校正同理：绝对赋值、幂等）。
        /// 主机读远端状态有同步延迟，因此两次尝试之间留出等待；
        /// 一旦主机看到的值已达到目标（或更高）即认为生效并停止，最多额外校正两次。
        /// </summary>
        private void TickInitialCurseGrant(Character character, string id)
        {
            if (!_initialCurse.TryGetValue(id, out var grant)) return;

            if (character == null || character.data == null)
            {
                _initialCurse.Remove(id);
                return;
            }

            // 半个状态量子（0.0125）作为容差：0.025 是引擎最小有效变化量
            float current = HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Curse);
            if (current >= grant.Target - 0.0125f)
            {
                _initialCurse.Remove(id);
                return;
            }

            if (grant.Retries >= InitialCurseMaxRetries || Time.time >= grant.Deadline)
            {
                Plugin.Log.LogWarning(
                    $"[RollOfTheScout] 开局诅咒校正结束：{Nick(character)} 主机可见 {current * 100f:0.#} / 目标 {grant.Target * 100f:0.#}" +
                    "（客机未把该变化同步回主机时属正常现象）。");
                _initialCurse.Remove(id);
                return;
            }

            if (Time.time < grant.NextCheck) return;

            HostApi.SetStatusAbsolute(character, CharacterAfflictions.STATUSTYPE.Curse, grant.Target);
            grant.Retries++;
            grant.NextCheck = Time.time + InitialCurseRecheckDelay;
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 开局诅咒补发（第 {grant.Retries} 次）：{Nick(character)} 校正为 {grant.Target * 100f:0.#}");
        }

        /// <summary>
        /// 开局「宿命 / 积损」的石化施加（<paramref name="petrifyFraction"/> 为 0.20 / 0.70 这样的比例）。
        ///
        /// 石化真实值存在 CharacterData.petrifyAmount，但此前是用「状态数组增量」下发的
        /// （RPC_ApplyStatusesFromFloatArray → AddStatus(Petrify) → AddPetrify），它有两处脆弱点：
        ///   · AddStatus 在客机侧有一串守卫（statusesLocked / m_inAirport / isSkeleton / isZombie 等），
        ///     角色刚实例化、场景尚未就绪时会被静默丢弃；
        ///   · AddPetrify 内部读的是 <c>Character.localCharacter</c>，中途加入者的本地角色尚未赋值时会直接异常。
        /// 实测「中途加入并被分配为后勤」的玩家因此拿不到开局的 70 石化（死亡记录里石化恒为 0），
        /// 而复活继承走的是绝对赋值通道，所以一复活又恢复成 70 —— 症状正好与此吻合。
        ///
        /// 因此这里改为：① 用「绝对赋值、幂等」的 <see cref="HostApi.SetPetrify"/>（RPC_SyncPetrify，
        /// 直接写 CharacterData，绕过上述全部守卫）；② 登记一笔校正待办，由 <see cref="TickPetrifyRestore"/>
        /// 在随后数秒内反复校正，直到主机确认达成或超时。
        /// 诅咒之所以没出现同样的问题，是因为它另有两次补发；石化当年没有补发。
        /// </summary>
        private void GrantInitialPetrify(Character character, float petrifyFraction)
        {
            if (character == null || petrifyFraction <= 0f) return;

            int target = Mathf.Clamp(Mathf.RoundToInt(petrifyFraction * 100f), 0, 100);
            if (target <= 0) return;

            HostApi.SetPetrify(character, target);

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            _petrifyRestores[id] = new PetrifyRestore
            {
                Target = target,
                NextCheck = Time.time + PetrifyRecheckDelay,
                Deadline = Time.time + PetrifyRestoreTimeout,
                Reason = "开局石化",
            };
        }

        // ---------------------------------------------------------------
        // 开路（先驱） / 随身医疗箱：每次进入新关卡，把「装满物品的背包」直接装备到玩家身上
        // ---------------------------------------------------------------

        /// <summary>
        /// 每次进入新关卡，把装满物品的背包直接装备到先驱 / 医生身上。
        /// 本方法每帧调用且幂等：已成功发放的角色记入 _levelGranted 并跳过，
        /// 未成功的会在后续帧重试（最多 MaxGrantAttempts 次），
        /// 以覆盖「客机比主机晚进入关卡/晚被分配职业」的时机问题。
        /// </summary>
        private void GrantLevelItems(List<Character> characters)
        {
            // 先驱 / 医生 都不在场 → 无需遍历全体玩家
            if (!_ledger.HasClassInRoom(ClassType.Explorer) && !_ledger.HasClassInRoom(ClassType.Doctor)) return;

            foreach (var character in characters)
            {
                var type = _ledger.GetClass(character);
                if (type != ClassType.Explorer && type != ClassType.Doctor) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时背包槽尚未就绪，
                // 背包会掉在身后而不是装备在身上（原版复活流程也会把它清掉）
                if (IsDead(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                var pool = type == ClassType.Explorer ? BuildExplorerPool() : BuildDoctorPool();
                int want = type == ClassType.Explorer
                    ? ModConfig.Explorer.BackpackItemCount.Value
                    : ModConfig.Doctor.BackpackItemCount.Value;
                want = Mathf.Clamp(want, 0, BackpackData.slots);

                // 每件物品都按各自权重独立随机抽取（有放回），允许重复
                var itemNames = WeightedPicker.PickMany(pool, want);

                if (HostApi.EquipBackpack(character, itemNames))
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 已为 {Nick(character)} 直接装备背包：{string.Join("、", itemNames)}");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning($"[RollOfTheScout] 为 {Nick(character)} 装备背包失败，将在后续帧重试。");
                    }
                }
            }
        }

        /// <summary>
        /// 死灵法师·骸骨：每次进入新关卡获得配置数量的骸骨之书。
        /// 优先放入该玩家 3 个物品栏的空位；物品栏已满时在该玩家脚下生成（落地后可拾取）。
        /// 与 <see cref="GrantLevelItems"/> 同样每帧幂等补发（_levelGranted 记账 + 失败重试）。
        /// </summary>
        private void GrantNecromancerBooks(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Necromancer)) return;

            int want = Mathf.Max(0, ModConfig.Necromancer.BookCount.Value);
            if (want == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Necromancer) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时物品栏尚未就绪，书本会掉在地上而不是入包
                if (IsDead(character)) continue;

                // 物品栏尚未就绪（Character/Player 两个预制体先后实例化）时不要尝试：
                // 此时 AddItem 必然失败，而「回退到脚下生成」会落在该角色尚未 warp 的坐标上（实测书本掉在 (0,4961,-4999)）
                if (!HostApi.IsInventoryReady(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                int ok = 0;
                for (int i = 0; i < want; i++)
                {
                    // 优先放入空的物品栏；物品栏已满时在脚下生成（兜底，避免凭空丢失）
                    if (HostApi.GiveItemToInventorySlots(character, ItemNames.BookOfBones, out _))
                    {
                        ok++;
                        continue;
                    }
                    if (HostApi.SpawnItemAtFeet(character, ItemNames.BookOfBones) != null)
                    {
                        ok++;
                        Plugin.Log.LogInfo($"[RollOfTheScout] 骸骨：{Nick(character)} 物品栏已满，骸骨之书改在脚下生成。");
                    }
                }

                if (ok >= want)
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 骸骨：{Nick(character)} 本层获得 {want} 本骸骨之书。");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 骸骨：{Nick(character)} 骸骨之书发放失败，将在后续帧重试（已发 {ok}/{want}）。");
                    }
                }
            }
        }

        /// <summary>
        /// 旗手·执旗：每次进入新关卡获得配置数量的检查点旗标。
        /// 优先放入该玩家 3 个物品栏的空位；物品栏已满时在该玩家脚下生成（落地后可拾取）。
        /// 与 <see cref="GrantNecromancerBooks"/> 同样每帧幂等补发（_levelGranted 记账 + 失败重试）。
        /// </summary>
        private void GrantStandardBearerFlags(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.StandardBearer)) return;

            int want = Mathf.Max(0, ModConfig.StandardBearer.FlagCount.Value);
            if (want == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.StandardBearer) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时物品栏尚未就绪，旗标会掉在地上而不是入包
                if (IsDead(character)) continue;

                // 同骸骨之书：物品栏未就绪时先不发放，避免回退到脚下生成、掉在未 warp 的坐标上
                if (!HostApi.IsInventoryReady(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                int ok = 0;
                for (int i = 0; i < want; i++)
                {
                    // 优先放入空的物品栏；物品栏已满时在脚下生成（兜底，避免凭空丢失）
                    if (HostApi.GiveItemToInventorySlots(character, ItemNames.CheckpointFlag, out _))
                    {
                        ok++;
                        continue;
                    }
                    if (HostApi.SpawnItemAtFeet(character, ItemNames.CheckpointFlag) != null)
                    {
                        ok++;
                        Plugin.Log.LogInfo($"[RollOfTheScout] 执旗：{Nick(character)} 物品栏已满，检查点旗标改在脚下生成。");
                    }
                }

                if (ok >= want)
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 执旗：{Nick(character)} 本层获得 {want} 个检查点旗标。");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 执旗：{Nick(character)} 检查点旗标发放失败，将在后续帧重试（已发 {ok}/{want}）。");
                    }
                }
            }
        }

        /// <summary>
        /// 圣人·辟谷养生：每次进入新关卡时清除圣人的饥饿（默认 -100，由配置「辟谷养生-饥饿」决定），
        /// 并可选（「辟谷养生-无敌」，默认开）额外施加一次无敌效果（奶白金 FortifiedMilk）。
        /// 与其它「每层发放」类效果同样用 _levelGranted 记账、每帧幂等补发：
        /// 已在本关处理过的圣人跳过；未处理过且当前未倒地的圣人执行一次后记账
        ///（饥饿清除是「减法且夹到 0」，重复执行也安全）。
        /// </summary>
        private void GrantSaintFasting(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Saint)) return;

            float amount = ModConfig.Saint.FastingHunger.Value;
            if (amount <= 0f) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Saint) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;
                if (IsDead(character)) continue;

                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -amount);
                if (ModConfig.Saint.FastingInvincible.Value)
                    FeedTransientItem(character, ItemNames.FortifiedMilk, "辟谷养生");
                _levelGranted.Add(id);
                Plugin.Log.LogInfo($"[RollOfTheScout] 辟谷养生：{Nick(character)} 进入新关卡，饥饿-{amount * 100f:0.#}"
                    + $"{(ModConfig.Saint.FastingInvincible.Value ? "，并获得一次无敌（奶白金）" : "")}");
            }
        }

        // ---------------------------------------------------------------
        // 特级厨师（MasterChef）：自带厨具 / 手暖炉 / 烹饪 / 煤气中毒 / 破坏大师
        // 炊事员（Cook）：后勤保障 / 饮食供给 / 食品安全 / 创伤应激
        //
        // 两职业都围绕「烤制」展开：
        //   · 烤制事件感知 = Patches.CookingFinishPatch（挂 ItemCooking.FinishCookingImpl 的 Prefix），
        //     主机在任意玩家烤制时都会收到，见 OnItemCooked；
        //   · 食用事件感知 = 既有的 GlobalEvents.OnItemConsumed（见 OnCookItemConsumed）；
        //   · 「食物」判定沿用游戏原生组件 Action_RestoreHunger（食用后恢复饥饿的物品）。
        // ---------------------------------------------------------------

        /// <summary>
        /// 烤制完成事件（仅主机处理，来自 <see cref="Patches.CookingFinishPatch"/>）。
        ///
        /// 关键：补丁挂在 <c>FinishCookingImpl</c> 的 **Prefix**，此时 <c>CookedAmount</c> 尚未自增，
        /// 因此 <c>before == 0</c> 即「这次烤制之前从未烤过」（炊事员·饮食供给的口径）。
        /// 烤制者取 <c>item.holderCharacter</c>：篝火 / 便携烤炉烤制时物品正被玩家手持；
        /// 丢进岩浆烤制的物品没有持有者，直接忽略（不属于任何玩家的「烤制」）。
        /// </summary>
        public void OnItemCooked(ItemCooking cooking)
        {
            if (!_inRun) return;
            if (cooking == null) return;

            var item = cooking.item;
            if (item == null) return;

            var cook = item.holderCharacter;
            if (cook == null) return;

            int before = item.GetData<IntItemData>(DataEntryKey.CookedAmount).Value;
            bool isFood = IsFoodItem(cooking);

            var type = _ledger.GetClass(cook);
            if (type == ClassType.MasterChef) OnMasterChefCooked(cook, item, isFood);
            else if (type == ClassType.Cook) OnCookCooked(cook, item, before, isFood);
        }

        /// <summary>该物品是否为「食物」：沿用游戏原生组件 Action_RestoreHunger（食用后恢复饥饿）。</summary>
        private static bool IsFoodItem(ItemCooking cooking)
        {
            return cooking != null && cooking.GetComponent<Action_RestoreHunger>() != null;
        }

        /// <summary>特级厨师·烹饪 / 煤气中毒 / 破坏大师（烤制时的结算）。</summary>
        private void OnMasterChefCooked(Character chef, Item item, bool isFood)
        {
            // 【烹饪】本局每烤制一次物品 → 减少饥饿
            float hungerReduce = ModConfig.MasterChef.CookHungerReduce.Value;
            if (hungerReduce > 0f)
                HostApi.ApplyStatus(chef, CharacterAfflictions.STATUSTYPE.Hunger, -hungerReduce);

            string id = _ledger.IdOf(chef);
            string itemName = item.gameObject.name;

            // 【煤气中毒】本局每 N 次烤制 → 增加中毒
            int every = ModConfig.MasterChef.GasPoisonEveryCooks.Value;
            float poison = ModConfig.MasterChef.GasPoisonAmount.Value;
            if (every > 0 && poison > 0f && !string.IsNullOrEmpty(id))
            {
                _chefCookCount.TryGetValue(id, out int count);
                count++;
                _chefCookCount[id] = count;
                if (count % every == 0)
                {
                    HostApi.ApplyStatus(chef, CharacterAfflictions.STATUSTYPE.Poison, poison);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 煤气中毒：{Nick(chef)} 本局已烤制 {count} 次，中毒+{poison * 100f:0.#}。");
                }
            }

            // 【破坏大师】每「触发间隔」秒内，烤制「非食物」物品最多触发一次额外体力
            float breakerInterval = ModConfig.MasterChef.BreakerIntervalSeconds.Value;
            float breakerStamina = ModConfig.MasterChef.BreakerStamina.Value;
            if (!isFood && breakerInterval > 0f && breakerStamina > 0f && !string.IsNullOrEmpty(id))
            {
                if (!_chefBreakerUntil.TryGetValue(id, out float until) || Time.time >= until)
                {
                    _chefBreakerUntil[id] = Time.time + breakerInterval;
                    HostApi.BoostExtraStamina(chef, breakerStamina);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 破坏大师：{Nick(chef)} 烤制非食物物品（{itemName}），额外体力+{breakerStamina * 100f:0.#}。");
                }
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 烹饪：{Nick(chef)} 烤制了 {itemName}（{(isFood ? "食物" : "非食物")}），饥饿-{hungerReduce * 100f:0.#}。");
        }

        /// <summary>炊事员·饮食供给（烤制「从未烤过的食物」时的半径加成）。</summary>
        private void OnCookCooked(Character cook, Item item, int cookedBefore, bool isFood)
        {
            if (!isFood || cookedBefore != 0) return;

            float radius = ModConfig.Cook.SupplyRadius.Value;
            float hungerReduce = ModConfig.Cook.SupplyHungerReduce.Value;
            float stamina = ModConfig.Cook.SupplyStamina.Value;
            if (radius <= 0f || (hungerReduce <= 0f && stamina <= 0f)) return;

            int n = 0;
            foreach (var target in HostApi.GetCharactersInRadius(cook.Center, radius))
            {
                if (target == null) continue;
                if (hungerReduce > 0f)
                    HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Hunger, -hungerReduce);
                if (stamina > 0f)
                    HostApi.BoostExtraStamina(target, stamina);
                n++;
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 饮食供给：{Nick(cook)} 烤制了未烤过的食物 {item.gameObject.name}，" +
                $"半径 {radius:0.#} 米内 {n} 名玩家 饥饿-{hungerReduce * 100f:0.#}、额外体力+{stamina * 100f:0.#}。");
        }

        /// <summary>
        /// 炊事员·食品安全 / 创伤应激：食用「烤熟的食物」或「未烤熟的食物」时的结算。
        /// 感知通道与其它食用类效果一致（<c>GlobalEvents.OnItemConsumed</c>），且原版 <c>Item.Consume</c>
        /// 每次食用只触发一次，因此无需去重窗口。
        /// </summary>
        public void OnCookItemConsumed(Item item, Character character)
        {
            if (!_inRun) return;
            if (item == null || character == null) return;
            if (_ledger.GetClass(character) != ClassType.Cook) return;

            // 只处理「食物」（带 Action_RestoreHunger 的物品）；非食物不触发
            if (item.GetComponent<Action_RestoreHunger>() == null) return;

            int cooked = item.GetData<IntItemData>(DataEntryKey.CookedAmount).Value;
            if (cooked >= 1)
            {
                // 【食品安全】食用烤熟的食物 → -饥饿 / -损伤 / +额外体力
                float hungerReduce = ModConfig.Cook.SafetyHungerReduce.Value;
                float injuryReduce = ModConfig.Cook.SafetyInjuryReduce.Value;
                float stamina = ModConfig.Cook.SafetyStamina.Value;
                // 保留旧实现的「> 0 才生效」口径：先算出差量再合并，避免把负值配置变成反向效果
                float hungerDelta = hungerReduce > 0f ? -hungerReduce : 0f;
                float injuryDelta = injuryReduce > 0f ? -injuryReduce : 0f;
                // 同一时刻对同一玩家减饥饿 + 减损伤：合并为单条 RPC（语义与逐条等价）
                HostApi.ApplyStatusPair(character,
                    CharacterAfflictions.STATUSTYPE.Hunger, hungerDelta,
                    CharacterAfflictions.STATUSTYPE.Injury, injuryDelta);
                if (stamina > 0f)
                    HostApi.BoostExtraStamina(character, stamina);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 食品安全：{Nick(character)} 食用了烤熟的食物（{item.gameObject.name}），" +
                    $"饥饿-{hungerReduce * 100f:0.#}、损伤-{injuryReduce * 100f:0.#}、额外体力+{stamina * 100f:0.#}。");
            }
            else
            {
                // 【创伤应激】食用未烤熟的食物 → +损伤 / +中毒 / +困倦
                // 黑名单（第 85 批新增）：诡异菇（转职触发物品，食用即触发转职，不应再叠加职业负面效果）、
                // 能量饮料 / 奶白金 / 大棒棒糖 → 直接豁免。按**预制体名**比对（实例名带 (Clone)）。
                string prefabName = PrefabNameOf(item);
                if (NameListContains(ModConfig.Cook.StressBlacklist.Value, ref _cookStressBlacklistRaw, _cookStressBlacklist, prefabName))
                    return;
                float injury = ModConfig.Cook.StressInjury.Value;
                float poison = ModConfig.Cook.StressPoison.Value;
                float drowsy = ModConfig.Cook.StressDrowsy.Value;
                // 保留旧实现的「> 0 才生效」口径：先算出差量再合并下发
                float stressInjuryDelta = injury > 0f ? injury : 0f;
                float stressPoisonDelta = poison > 0f ? poison : 0f;
                float stressDrowsyDelta = drowsy > 0f ? drowsy : 0f;
                // 同一时刻对同一玩家叠加 损伤 + 中毒 + 困倦：合并为单条 RPC（语义与逐条等价）
                HostApi.ApplyStatuses(character,
                    CharacterAfflictions.STATUSTYPE.Injury, stressInjuryDelta,
                    CharacterAfflictions.STATUSTYPE.Poison, stressPoisonDelta,
                    CharacterAfflictions.STATUSTYPE.Drowsy, stressDrowsyDelta);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 创伤应激：{Nick(character)} 食用了未烤熟的食物（{item.gameObject.name}），" +
                    $"损伤+{injury * 100f:0.#}、中毒+{poison * 100f:0.#}、困倦+{drowsy * 100f:0.#}。");
            }
        }

        /// <summary>给目标发放 1 个便携烤炉：优先放入 3 个物品栏空位，全满则在脚下生成。</summary>
        private bool GrantStove(Character character, string reason)
        {
            if (HostApi.GiveItemToInventorySlots(character, ItemNames.PortableStove, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 物品栏获得 1 个便携烤炉。");
                return true;
            }
            if (HostApi.SpawnItemAtFeet(character, ItemNames.PortableStove) != null)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 物品栏已满，便携烤炉改在脚下生成。");
                return true;
            }
            Plugin.Log.LogWarning(
                $"[RollOfTheScout] {reason}：{Nick(character)} 的便携烤炉发放失败（物品栏已满且脚下生成失败）。");
            return false;
        }

        /// <summary>特级厨师·自带厨具（开局）：每帧幂等补发，直到成功（_initialApplied 记账 + 失败重试）。</summary>
        private void EnsureMasterChefStove(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.MasterChef)) return;

            int count = Mathf.Max(0, ModConfig.MasterChef.InitialStoveCount.Value);
            if (count == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.MasterChef) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_initialApplied.Contains(id)) continue;

                // 死亡 / 灵魂状态时不发放：中途加入者此时物品栏尚未就绪，物品会掉在地上而不是入包
                if (IsDead(character)) continue;
                if (!HostApi.IsInventoryReady(character)) continue;

                _initialAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                int ok = 0;
                for (int i = 0; i < count; i++)
                {
                    if (GrantStove(character, "自带厨具")) ok++;
                }

                if (ok >= count)
                {
                    _initialApplied.Add(id);
                    _initialAttempts.Remove(id);
                }
                else
                {
                    _initialAttempts[id] = attempts + 1;
                }
            }
        }

        /// <summary>炊事员·后勤保障（每层）：每次进入新关卡获得配置数量的便携烤炉（每帧幂等补发）。</summary>
        private void GrantCookStoves(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Cook)) return;

            int want = Mathf.Max(0, ModConfig.Cook.InitialStoveCount.Value);
            if (want == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Cook) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_levelGranted.Contains(id)) continue;

                if (IsDead(character)) continue;
                if (!HostApi.IsInventoryReady(character)) continue;

                _levelGrantAttempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxGrantAttempts) continue;

                int ok = 0;
                for (int i = 0; i < want; i++)
                {
                    if (GrantStove(character, "后勤保障")) ok++;
                }

                if (ok >= want)
                {
                    _levelGranted.Add(id);
                    _levelGrantAttempts.Remove(id);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 后勤保障：{Nick(character)} 本层获得 {want} 个便携烤炉。");
                }
                else
                {
                    _levelGrantAttempts[id] = attempts + 1;
                    if (attempts == 0)
                    {
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 后勤保障：{Nick(character)} 便携烤炉发放失败，将在后续帧重试（已发 {ok}/{want}）。");
                    }
                }
            }
        }

        /// <summary>特级厨师·手暖炉：该角色当前手持的（主手）物品是否为便携烤炉。</summary>
        private static bool IsHoldingStove(Character character)
        {
            if (character == null || character.data == null) return false;
            var held = character.data.currentItem;
            if (held == null) return false;
            if (!ItemDatabase.TryGetItem(ItemNames.PortableStove, out Item stove) || stove == null) return false;
            return held.itemID == stove.itemID;
        }

        /// <summary>炊事员·后勤保障：该角色半径内是否存在「后勤」职业玩家（不含自己）。</summary>
        private bool HasLogisticianNearby(Character cook, float radius)
        {
            if (cook == null || radius <= 0f) return false;
            foreach (var c in HostApi.GetCharactersInRadius(cook.Center, radius))
            {
                if (c == null || c == cook) continue;
                if (_ledger.GetClass(c) == ClassType.Logistician) return true;
            }
            return false;
        }

        /// <summary>炊事员·创伤应激：该角色半径内是否存在饥饿值高于阈值的玩家（含自己）。</summary>
        private static bool HasHungryPlayerNearby(Character cook, float radius, float threshold)
        {
            if (cook == null) return false;
            foreach (var c in HostApi.GetCharactersInRadius(cook.Center, radius))
            {
                if (c == null) continue;
                if (HostApi.GetStatus(c, CharacterAfflictions.STATUSTYPE.Hunger) > threshold) return true;
            }
            return false;
        }

        /// <summary>
        /// 炊事员·后勤保障：随机取一个「食物」预制体名。
        /// 优先从配置「后勤保障-食物池」中抽取；池为空或全部无效时，
        /// 退化为「数据库中所有带 <c>Action_RestoreHunger</c> 的物品」（与普通食物判定同一口径）。
        ///
        /// 配置池按字符串缓存（<see cref="CookFoodPool"/>），避免每次调用都 <c>ParseList</c>（字符串分配）。
        /// </summary>
        private string PickRandomFood()
        {
            var pool = CookFoodPool();
            var candidates = new List<string>();
            foreach (var name in pool)
            {
                if (ItemDatabase.TryGetItem(name, out Item item) && item != null) candidates.Add(name);
            }

            if (candidates.Count == 0)
            {
                var database = Zorro.Core.SingletonAsset<ItemDatabase>.Instance;
                if (database != null && database.Objects != null)
                {
                    foreach (var item in database.Objects)
                    {
                        if (item == null) continue;
                        if (item.GetComponent<Action_RestoreHunger>() == null) continue;
                        candidates.Add(item.name);
                    }
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        // ---------------------------------------------------------------
        // 和平主义者（Pacifist）：和平？ —— 初始诅咒 / 右键（原本拉人）改为踢人 / 踢中与未中的体力结算
        //
        // 感知通道：Patches.PacifistReachPatch，挂 CharacterGrabbing.RPCA_StartReaching（[PunRPC]，RpcTarget.All）。
        //
        // ⚠ 明确**不调用**原版 `CharacterGrabbing.RPCA_Kick`：
        //   它会把 `character.data.isKicking` 置真，而该标记**只在**「帮助之手（踢人）模式」（run setting
        //   HelpingHand == 1）下才会被 `CharacterGrabbing.Update` 自动复位；正常模式下一旦为真就**永不复位**，
        //   而 `Character.KickCast()` 那条分支之外的 `CharacterMovement.GetMovementForce()` 会因
        //   `CheckMovement()` 读到 isKicking 而返回 0 → 角色**永久无法移动**（实测 bug）。
        //   何况客机没有安装本模组，主机也无法替它们复位该标记。故改为完全主机侧复刻：
        //     · 取消原版拉人：广播 RPCA_StopReaching；
        //     · 踢击音效：广播原版 RPCA_KickImpact（纯音效、无副作用）；
        //     · 命中判定：主机按「身前距离 + 夹角」自行判定；
        //     · 踢中表现：对被踢者广播原版 RPCA_Fall（与原版踢击的 Fall(kickRagdollTime) 等价）。
        // ---------------------------------------------------------------

        /// <summary>和平？：玩家按下右键（原版「开始拉人」）时的处理——取消拉人并改为踢人。</summary>
        public void OnPacifistReachPressed(Character character)
        {
            if (!_inRun) return;
            if (character == null || character.photonView == null) return;
            if (!ModConfig.Pacifist.Enabled.Value) return;
            if (_ledger.GetClass(character) != ClassType.Pacifist) return;

            // 去重：按下右键后原版会在约 0.2s 内反复广播 StartReaching，用冷却保证一次按键只踢一次
            string id = _ledger.IdOf(character);
            float cooldown = Mathf.Max(0.2f, ModConfig.Pacifist.KickCooldown.Value);
            if (!string.IsNullOrEmpty(id))
            {
                if (_pacifistKickCooldownUntil.TryGetValue(id, out float until) && Time.time < until) return;
                _pacifistKickCooldownUntil[id] = Time.time + cooldown;
            }

            // ① 取消原版「拉人」（右键原本的动作）：广播 StopReaching，使本人客户端退出 reach 状态
            character.photonView.RPC("RPCA_StopReaching", RpcTarget.All);

            // ② 主机侧踢击判定
            float distance = Mathf.Max(0.5f, ModConfig.Pacifist.KickDistance.Value);
            float angle = Mathf.Clamp(ModConfig.Pacifist.KickAngle.Value, 1f, 179f);
            var victim = FindPacifistKickVictim(character, distance, angle);

            // ③ 播放原版踢击音效（纯音效、无任何玩法副作用），在踢人者位置
            character.photonView.RPC("RPCA_KickImpact", RpcTarget.All, character.Center);

            if (victim != null)
            {
                // 踢中：让被踢者进入与原版踢击等价的倒地状态
                float ragdoll = Mathf.Max(0f, ModConfig.Pacifist.KickRagdollSeconds.Value);
                if (ragdoll > 0f)
                    victim.photonView.RPC("RPCA_Fall", RpcTarget.All, ragdoll, 0f);
                AwardPacifistHit(character);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 和平？：{Nick(character)} 右键踢人（已取消拉人），踢中 {Nick(victim)}。");
            }
            else
            {
                AwardPacifistMiss(character);
                Plugin.Log.LogInfo($"[RollOfTheScout] 和平？：{Nick(character)} 右键踢人（已取消拉人），未踢中玩家。");
            }
        }

        /// <summary>
        /// 和平？·踢人：在踢人者「身前锥形范围」内取最近的存活玩家（与原版踢击的近距离锥形判定等价）。
        /// 朝向取髋部 forward：布娃娃各部位 Transform 会随原版同步到主机，因此对客机也有效
        ///（`CharacterData.lookDirection` 是各客户端本地由输入推导的，并未网络同步，不可用）。
        /// </summary>
        private static Character FindPacifistKickVictim(Character pacifist, float distance, float angle)
        {
            if (pacifist == null) return null;

            Vector3 facing = pacifist.refs != null && pacifist.refs.hip != null
                ? pacifist.refs.hip.transform.forward
                : pacifist.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = pacifist.transform.forward;
                facing.y = 0f;
            }
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
            facing.Normalize();

            float cosLimit = Mathf.Cos(angle * Mathf.Deg2Rad);

            Character best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in HostApi.GetCharactersInRadius(pacifist.Center, distance))
            {
                if (c == null || c == pacifist) continue;
                if (IsDead(c)) continue;

                Vector3 to = c.Center - pacifist.Center;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr > 0.0001f && Vector3.Dot(facing, to / Mathf.Sqrt(sqr)) < cosLimit) continue;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        private void AwardPacifistHit(Character pacifist)
        {
            if (pacifist == null) return;
            float stamina = ModConfig.Pacifist.HitStamina.Value;
            if (stamina != 0f) HostApi.BoostExtraStamina(pacifist, stamina);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 和平？：{Nick(pacifist)} 踢中玩家，额外体力+{stamina * 100f:0.#}。");
            ShowToast($"和平？：{Nick(pacifist)} 踢中玩家");
        }

        private void AwardPacifistMiss(Character pacifist)
        {
            if (pacifist == null) return;
            float stamina = ModConfig.Pacifist.MissStamina.Value;
            float drowsy = ModConfig.Pacifist.MissDrowsy.Value;
            if (stamina > 0f) HostApi.BoostExtraStamina(pacifist, -stamina);
            if (drowsy > 0f) HostApi.ApplyStatus(pacifist, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 和平？：{Nick(pacifist)} 踢人未踢中玩家，额外体力-{stamina * 100f:0.#}、困倦+{drowsy * 100f:0.#}。");
            ShowToast($"和平？：{Nick(pacifist)} 踢空");
        }

        /// <summary>由 Photon 玩家（旗标的 Owner）反查场景中的 Character。</summary>
        public Character FindCharacterOfPlayer(Photon.Realtime.Player owner)
        {
            if (owner == null) return null;
            foreach (var character in HostApi.GetActiveCharacters())
            {
                var view = character.photonView;
                if (view != null && view.Owner == owner) return character;
            }
            return null;
        }

        /// <summary>
        /// 旗手·执旗：观察到「检查点旗标被插下」（由 <see cref="CheckpointFlagPatch"/> 回调）。
        /// 只认职业为旗手的插旗者；登记一个延迟任务，<paramref name="delay"/> 秒后统一结算效果。
        /// </summary>
        public void OnCheckpointFlagPlanted(Character planter)
        {
            if (!_inRun) return;
            if (planter == null) return;
            if (_ledger.GetClass(planter) != ClassType.StandardBearer) return;

            // 立即净化：清除半径内所有玩家的负面状态（不含诅咒 / 石化）
            CleanseAroundPlanter(planter);

            float delay = Mathf.Max(0f, ModConfig.StandardBearer.EffectDelaySeconds.Value);
            _flagPending.Add(new FlagBearerPending { Planter = planter, ApplyAt = Time.time + delay });
            Plugin.Log.LogInfo($"[RollOfTheScout] 执旗：{Nick(planter)} 插下检查点旗标，{delay:0.#} 秒后为全体存活玩家结算祝福。");
        }

        /// <summary>
        /// 旗手·执旗（净化）：清除插旗者半径内所有玩家的负面状态，**不包括诅咒与石化**。
        /// 是否作用于插旗者自己由 `执旗-净化含自身` 决定（默认含自身）。
        /// 净化后，插旗者**自身**额外 +石化（`执旗-插旗石化`，默认 0.20 = 20 点），作为该效果的代价。
        /// </summary>
        private void CleanseAroundPlanter(Character planter)
        {
            float radius = Mathf.Max(0f, ModConfig.StandardBearer.CleanseRadius.Value);
            if (radius <= 0f) return;

            bool includeSelf = ModConfig.StandardBearer.CleanseIncludeSelf.Value;
            var targets = HostApi.GetCharactersInRadius(planter.Center, radius);
            int count = 0;

            foreach (var target in targets)
            {
                if (target == null || target.photonView == null) continue;
                if (!includeSelf && target == planter) continue;

                HostApi.ClearNegativeStatuses(target, excludeCurse: true, excludePetrify: true);
                count++;
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 执旗：{Nick(planter)} 插旗净化，半径 {radius:0.#} 米内 {count} 名玩家" +
                $"（含自身：{(includeSelf ? "是" : "否")}）的负面状态已清除（不含诅咒 / 石化）。");

            // 净化代价：旗手「自身」额外 +石化（不计入上面的净化 —— 净化本就排除石化）。
            float selfPetrify = ModConfig.StandardBearer.CleanseSelfPetrify.Value;
            if (selfPetrify > 0f)
            {
                HostApi.ApplyStatus(planter, CharacterAfflictions.STATUSTYPE.Petrify, selfPetrify);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 执旗：{Nick(planter)} 插旗净化代价，自身石化 +{selfPetrify * 100f:0.#} 点。");
            }
        }

        /// <summary>结算到期的「插旗祝福」任务。</summary>
        private void TickFlagBearerEffects()
        {
            if (_flagPending.Count == 0) return;

            float now = Time.time;
            for (int i = _flagPending.Count - 1; i >= 0; i--)
            {
                var pending = _flagPending[i];
                if (pending.Planter == null || pending.Planter.photonView == null)
                {
                    _flagPending.RemoveAt(i);
                    continue;
                }
                if (now < pending.ApplyAt) continue;
                _flagPending.RemoveAt(i);
                ApplyFlagBearerEffects(pending.Planter);
            }
        }

        /// <summary>对当前所有存活玩家（含插旗者本人）随机施加 1~N 种执旗祝福。</summary>
        private void ApplyFlagBearerEffects(Character planter)
        {
            int min = Mathf.Max(1, ModConfig.StandardBearer.EffectCountMin.Value);
            int max = Mathf.Max(min, ModConfig.StandardBearer.EffectCountMax.Value);

            var recipients = HostApi.GetActiveCharacters();
            int affected = 0;
            foreach (var character in recipients)
            {
                if (character == null || character.photonView == null) continue;
                if (IsDead(character)) continue;

                int n = Random.Range(min, max + 1);
                var effects = PickDistinctFlagEffects(n);
                foreach (var effect in effects) ApplyFlagEffect(character, effect);
                affected++;
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 执旗：{Nick(planter)} 的插旗祝福已结算，作用 {affected} 名存活玩家" +
                $"（每人随机 {min}~{max} 种效果）。");
        }

        /// <summary>从三种执旗祝福中不重复地随机抽取 <paramref name="count"/> 种（权重 &lt;= 0 者不参与）。</summary>
        private static List<string> PickDistinctFlagEffects(int count)
        {
            var result = new List<string>();
            if (count <= 0) return result;

            var pool = new List<WeightedItem>();
            if (ModConfig.StandardBearer.StaminaWeight.Value > 0)
                pool.Add(new WeightedItem("无限体力", ModConfig.StandardBearer.StaminaWeight.Value));
            if (ModConfig.StandardBearer.SprintWeight.Value > 0)
                pool.Add(new WeightedItem("加速", ModConfig.StandardBearer.SprintWeight.Value));
            if (ModConfig.StandardBearer.InvincibleWeight.Value > 0)
                pool.Add(new WeightedItem("无敌", ModConfig.StandardBearer.InvincibleWeight.Value));

            if (pool.Count == 0)
            {
                Plugin.Log.LogWarning("[RollOfTheScout] 执旗：三种祝福权重都为 0，本次不施加任何效果。");
                return result;
            }

            int want = Mathf.Min(count, pool.Count);
            for (int i = 0; i < want; i++)
            {
                var picked = WeightedPicker.Pick(pool);
                if (string.IsNullOrEmpty(picked)) break;
                result.Add(picked);
                pool.RemoveAll(w => w.PrefabName == picked); // 去重：同一玩家同一种效果至多一次
            }
            return result;
        }

        /// <summary>对单个玩家施加一种执旗祝福。</summary>
        private void ApplyFlagEffect(Character character, string effect)
        {
            switch (effect)
            {
                case "无限体力":
                    // 无限体力（默认大棒棒糖，可用配置改为烤过的大棒棒糖以免除结束时的困倦）
                    GrantInfiniteStamina(character, "执旗");
                    break;
                case "加速":
                    // 能量饮料：加速（与先驱「高代谢」同理，效果结束后清除等量困倦）
                    GrantSprint(character, "执旗", clearDrowsyAfter: true,
                        clearAmount: ModConfig.StandardBearer.SprintDrowsyClearAmount.Value);
                    break;
                case "无敌":
                    // 奶白金：无敌
                    FeedTransientItem(character, ItemNames.FortifiedMilk, "执旗");
                    break;
            }
        }

        /// <summary>
        /// 让目标角色「使用」一件临时物品（生成在场景外、玩家看不到 → FeedItem → 延迟销毁）。
        /// 见 <see cref="EnqueueTransientFeed"/>：实际生成被摊到后续若干帧执行，以避免群体效果同帧爆发实例化。
        /// </summary>
        private void FeedTransientItem(Character character, string prefabName, string reason)
        {
            EnqueueTransientFeed(character, prefabName, reason, TransientKind.Generic);
        }

        // ---------------------------------------------------------------
        // 「临时物品喂食」的摊帧执行（性能优化）
        //
        // 背景：加速 / 无限体力 / 无敌 / 潘多拉魔盒等效果，都靠「主机生成一件临时物品喂给目标、
        // 让**目标自己的客户端**去消费它」来实现。每次生成都是一次 `PhotonNetwork.Instantiate`（网络对象），
        // **所有客户端都要实例化 + 稍后销毁**。而群体效果（领袖号召 / 旗手执旗 / 亡灵光环无敌 /
        // 好多区 / 教诲 / 童军的力量 …）会在**同一次结算里对多名玩家各来一次** →
        // 10 人局可能**同一帧内爆发 10~30 次实例化**，10 个客户端同时处理，直接造成单帧尖峰与掉帧。
        //
        // 做法：把这类「临时物品喂食」放进一个队列，**每帧最多执行 TransientFeedsPerFrame 次**，
        // 其余顺延到后续帧。内容与结果完全不变，只是把一次爆发摊成几帧；
        // 取 3 次/帧（60fps ≈ 180 次/秒的吞吐）时，30 次的爆发最迟约 0.17 秒发完，实际不可感知。
        // ---------------------------------------------------------------

        /// <summary>「临时物品喂食」的类别（仅用于区分配套记账与日志措辞）。</summary>
        private enum TransientKind
        {
            /// <summary>通用喂食（<see cref="FeedTransientItem"/>）。</summary>
            Generic,
            /// <summary>加速（能量饮料，喂食后登记「加速结束清困倦」监听）。</summary>
            Sprint,
            /// <summary>无限体力（大棒棒糖，喂食后登记「后续持续困倦」监听）。</summary>
            InfiniteStamina,
            /// <summary>潘多拉魔盒。</summary>
            PandoraBox,
        }

        /// <summary>一次排队的「临时物品喂食」请求。</summary>
        private sealed class TransientFeed
        {
            public Character Target;
            public string Prefab;
            public string Reason;
            public TransientKind Kind;
            /// <summary>加速类：加速结束后要清除的困倦量（&lt;= 0 表示不登记监听）。</summary>
            public float SprintClearAmount;
        }

        private readonly Queue<TransientFeed> _transientFeeds = new Queue<TransientFeed>();

        /// <summary>每帧最多执行的「临时物品实例化」次数（摊帧粒度）。</summary>
        private const int TransientFeedsPerFrame = 3;

        /// <summary>队列上限：异常情况下防止无限堆积（正常一次爆发远小于该值）。</summary>
        private const int TransientFeedQueueCap = 64;

        /// <summary>把一次「临时物品喂食」排入队列，由 <see cref="DrainTransientFeeds"/> 逐帧摊开执行。</summary>
        private void EnqueueTransientFeed(Character character, string prefabName, string reason,
            TransientKind kind, float sprintClearAmount = -1f)
        {
            if (character == null || string.IsNullOrEmpty(prefabName)) return;
            if (_transientFeeds.Count >= TransientFeedQueueCap) return;   // 上限保护（正常不会命中）

            _transientFeeds.Enqueue(new TransientFeed
            {
                Target = character,
                Prefab = prefabName,
                Reason = reason,
                Kind = kind,
                SprintClearAmount = sprintClearAmount,
            });
        }

        /// <summary>每帧调用：摊开执行队列中最前面的若干次「临时物品喂食」（见上面的性能说明）。</summary>
        private void DrainTransientFeeds()
        {
            for (int i = 0; i < TransientFeedsPerFrame && _transientFeeds.Count > 0; i++)
            {
                ExecuteTransientFeed(_transientFeeds.Dequeue());
            }
        }

        /// <summary>真正执行一次「临时物品喂食」：生成 → FeedItem → 延迟销毁，并做对应的配套记账与日志。</summary>
        private void ExecuteTransientFeed(TransientFeed req)
        {
            var character = req.Target;
            if (character == null) return;   // 目标已离开 / 已销毁：与原实现「对空对象直接返回」一致

            string prefabName = req.Prefab;
            string reason = req.Reason;

            var go = HostApi.SpawnTransientItem(prefabName);
            if (go == null)
            {
                Plugin.Log.LogWarning(req.Kind == TransientKind.PandoraBox
                    ? $"[RollOfTheScout] 潘多拉：生成潘多拉魔盒失败，{Nick(character)} 未受影响。"
                    : $"[RollOfTheScout] {reason}：生成 {prefabName} 失败，{Nick(character)} 未获得"
                      + (req.Kind == TransientKind.Sprint ? "加速。" : req.Kind == TransientKind.InfiniteStamina ? "无限体力。" : "该效果。"));
                return;
            }

            var item = go.GetComponent<Item>();
            if (item == null)
            {
                PhotonNetwork.Destroy(go);
                return;
            }

            character.FeedItem(item);
            StartCoroutine(DestroyAfter(go, 1f));

            // 配套记账：加速 → 登记「加速结束清困倦」；无限体力 → 登记「后续持续困倦」监听
            if (req.Kind == TransientKind.Sprint && req.SprintClearAmount > 0f)
            {
                RegisterEffectWatch(character, reason, Affliction.AfflictionType.FasterBoi, "加速", req.SprintClearAmount);
            }
            else if (req.Kind == TransientKind.InfiniteStamina
                     && ModConfig.StandardBearer.StaminaDrowsyClearAmount.Value > 0f)
            {
                var id = _ledger.IdOf(character);
                if (!string.IsNullOrEmpty(id))
                {
                    _staminaDrowsyWatches[id] = new StaminaDrowsyWatch { Character = character, Reason = reason };
                }
            }

            switch (req.Kind)
            {
                case TransientKind.Sprint:
                    Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 获得加速（能量饮料）。");
                    break;
                case TransientKind.InfiniteStamina:
                    Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 获得无限体力（{prefabName}）。");
                    break;
                case TransientKind.PandoraBox:
                    Plugin.Log.LogInfo($"[RollOfTheScout] 潘多拉：{Nick(character)} 被施加潘多拉魔盒。");
                    break;
                default:
                    Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 获得 {prefabName}。");
                    break;
            }
        }

        // ---------------------------------------------------------------
        // 法师：魔法卷轴（人类） / 黑魔法（骷髅）
        // ---------------------------------------------------------------

        /// <summary>一次延迟结算：人类形态「打开卷轴后延迟触发 1 种效果」或黑魔法「连锁触发的一步」。</summary>
        private sealed class MagePending
        {
            public Character Mage;
            public float ApplyAt;
            /// <summary>true = 黑魔法连锁的一次触发；false = 人类形态的延迟单次触发。</summary>
            public bool Skeleton;
            /// <summary>黑魔法连锁：本串已触发过的效果名（本串内去重）。人类形态为 null。</summary>
            public List<string> Used;
        }

        /// <summary>法师的一种持续型效果（净化 / 治愈 / 反重）。</summary>
        private sealed class MageContinuous
        {
            public Character Mage;
            /// <summary>效果类型："净化" / "治愈" / "反重"。</summary>
            public string Kind;
            public float EndAt;
            public float NextTickAt;
            public float Interval;
            public float Radius;
            public bool IncludeSelf;
            /// <summary>净化 / 治愈：每 tick 的量。</summary>
            public float Amount;
            /// <summary>反重：每 tick 生成的反重力场预制体名。</summary>
            public string Item;
        }

        /// <summary>
        /// 净化作用的状态下标：**所有可被降低的负面状态**（第 64 批起含诅咒与石化）。
        /// 覆盖 损伤 / 饥饿 / 寒冷 / 中毒 / 诅咒 / 困倦 / 过热 / 孢子 / 蛛网 / 捕蝇草 / 石化；
        /// 原版「不可治愈」的 螃蟹 / 负重 / 荆棘 / 箭 不在其中（状态通道本身也排除 Weight/Thorns/Arrow）。
        /// 石化（下标 13）经该通道下发负值时走 <c>SubtractStatus → AddPetrify(-x)</c>，即**相对扣减**石化。
        /// </summary>
        private static readonly int[] MageCurableIndices =
        {
            (int)CharacterAfflictions.STATUSTYPE.Injury,
            (int)CharacterAfflictions.STATUSTYPE.Hunger,
            (int)CharacterAfflictions.STATUSTYPE.Cold,
            (int)CharacterAfflictions.STATUSTYPE.Poison,
            (int)CharacterAfflictions.STATUSTYPE.Curse,
            (int)CharacterAfflictions.STATUSTYPE.Drowsy,
            (int)CharacterAfflictions.STATUSTYPE.Hot,
            (int)CharacterAfflictions.STATUSTYPE.Spores,
            (int)CharacterAfflictions.STATUSTYPE.Web,
            (int)CharacterAfflictions.STATUSTYPE.FlyTrap,
            (int)CharacterAfflictions.STATUSTYPE.Petrify,
        };

        /// <summary>
        /// 法师每帧驱动：卷轴发放定时器 + 人类形态的延迟结算队列 + 持续型效果（净化 / 治愈 / 反重）。
        /// </summary>
        private void TickMageEffects(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 魔法卷轴：定时为每名存活的法师发放 1 个卷轴 ----
            float interval = ModConfig.Mage.ScrollIntervalSeconds.Value;
            if (interval <= 0f)
            {
                _mageScrollTimer = 0f;
            }
            else
            {
                _mageScrollTimer += dt;
                if (_mageScrollTimer >= interval)
                {
                    _mageScrollTimer = 0f;
                    foreach (var mage in characters)
                    {
                        if (_ledger.GetClass(mage) != ClassType.Mage) continue;
                        if (IsDead(mage)) continue;
                        GiveMageItem(mage, ModConfig.Mage.ScrollItem.Value);
                    }
                }
            }

            // ---- 人类形态：延迟结算随机效果 ----
            if (_magePending.Count > 0)
            {
                float now = Time.time;
                for (int i = _magePending.Count - 1; i >= 0; i--)
                {
                    var pending = _magePending[i];
                    if (pending.Mage == null || pending.Mage.photonView == null)
                    {
                        _magePending.RemoveAt(i);
                        continue;
                    }
                    if (now < pending.ApplyAt) continue;
                    _magePending.RemoveAt(i);
                    if (IsDead(pending.Mage)) continue;
                    if (pending.Skeleton) ApplyMageSkeletonChainStep(pending);
                    else ApplyMageHumanEffect(pending.Mage);
                }
            }

            TickMageContinuous();
        }

        /// <summary>推进所有「持续型效果」（净化 / 治愈 / 反重）的计时与结算。</summary>
        private void TickMageContinuous()
        {
            if (_mageContinuous.Count == 0) return;
            float now = Time.time;
            for (int i = _mageContinuous.Count - 1; i >= 0; i--)
            {
                var fx = _mageContinuous[i];
                if (fx.Mage == null || fx.Mage.photonView == null || IsDead(fx.Mage))
                {
                    _mageContinuous.RemoveAt(i);
                    continue;
                }
                if (now >= fx.EndAt)
                {
                    _mageContinuous.RemoveAt(i);
                    continue;
                }
                if (now < fx.NextTickAt) continue;
                fx.NextTickAt = now + Mathf.Max(0.1f, fx.Interval);
                ApplyMageContinuousTick(fx);
            }
        }

        private void ApplyMageContinuousTick(MageContinuous fx)
        {
            switch (fx.Kind)
            {
                case "净化":
                {
                    var targets = HostApi.GetCharactersInRadius(fx.Mage.Center, fx.Radius);
                    int n = 0;
                    foreach (var t in targets)
                    {
                        if (t == null || t.photonView == null) continue;
                        if (!fx.IncludeSelf && t == fx.Mage) continue;
                        if (IsDead(t)) continue;
                        // 迈尔斯残魂【受诅残魂】：法师·魔法卷轴·净化是「被允许降低诅咒」的例外之一，
                        // 在此登记允许窗口，避免保护逻辑把净化的降诅咒立即撤销。
                        if (_ledger.GetClass(t) == ClassType.MilesSoul)
                        {
                            string mid = _ledger.IdOf(t);
                            if (!string.IsNullOrEmpty(mid))
                            {
                                float until = fx.EndAt + 1f;
                                _milesCurseAllowUntil.TryGetValue(mid, out float cur);
                                _milesCurseAllowUntil[mid] = Mathf.Max(cur, until);
                            }
                        }
                        var data = new float[HostApi.StatusArrayLength];
                        foreach (int idx in MageCurableIndices) data[idx] = -fx.Amount;
                        HostApi.SendStatuses(t, data);
                        n++;
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 魔法卷轴·净化：{Nick(fx.Mage)} 为半径 {fx.Radius:0.#} 米内 {n} 名玩家清除负面状态 -{fx.Amount * 100f:0.#}。");
                    break;
                }
                case "治愈":
                {
                    var targets = HostApi.GetCharactersInRadius(fx.Mage.Center, fx.Radius);
                    int n = 0;
                    foreach (var t in targets)
                    {
                        if (t == null || t.photonView == null) continue;
                        if (!fx.IncludeSelf && t == fx.Mage) continue;
                        if (IsDead(t)) continue;
                        HostApi.ApplyStatus(t, CharacterAfflictions.STATUSTYPE.Injury, -fx.Amount);
                        n++;
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 魔法卷轴·治愈：{Nick(fx.Mage)} 为半径 {fx.Radius:0.#} 米内 {n} 名玩家清除损伤 -{fx.Amount * 100f:0.#}。");
                    break;
                }
                case "反重":
                {
                    if (string.IsNullOrEmpty(fx.Item)) break;
                    var targets = HostApi.GetCharactersInRadius(fx.Mage.Center, fx.Radius);
                    int n = 0;
                    foreach (var t in targets)
                    {
                        if (t == null || t.photonView == null) continue;
                        if (!fx.IncludeSelf && t == fx.Mage) continue;
                        if (IsDead(t)) continue;
                        SpawnMageAntiSphere(t.Center + Vector3.down * 0.6f);
                        n++;
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 魔法卷轴·反重：{Nick(fx.Mage)} 为半径 {fx.Radius:0.#} 米内 {n} 名玩家脚下生成反重力场（{fx.Item}）。");
                    break;
                }
            }
        }

        /// <summary>
        /// 法师·魔法卷轴 / 黑魔法：由 <see cref="HandleItemConsumed"/> 调用。
        /// 只认职业为法师、且消耗的是配置的卷轴物品。
        /// 人类形态：立即 +石化，并登记「延迟」秒后随机触发 1 种效果；
        /// 骷髅形态（黑魔法）：立即 +诅咒，并「立即」随机触发（下限~上限）种效果，效果池替换为黑魔法池。
        /// 感知通道复用全局食用事件，无需新增补丁。
        /// </summary>
        private void OnMageScrollConsumed(Character character, Item item)
        {
            if (!_inRun) return;
            if (character == null || item == null) return;
            if (_ledger.GetClass(character) != ClassType.Mage) return;

            string scroll = ModConfig.Mage.ScrollItem.Value;
            if (string.IsNullOrEmpty(scroll)) return;
            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            if (prefab.gameObject.name != scroll) return;

            if (IsSkeleton(character))
            {
                // 黑魔法：立即 +诅咒，随后按「卷轴-连锁概率」逐次触发（首次立即，之后每隔「卷轴-连锁间隔」秒）
                float curse = ModConfig.Mage.SkeletonCurse.Value;
                if (curse != 0f)
                    HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Curse, curse);

                _magePending.Add(new MagePending
                {
                    Mage = character,
                    ApplyAt = Time.time,
                    Skeleton = true,
                    Used = new List<string>(),
                });
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法：骷髅法师 {Nick(character)} 打开卷轴，诅咒+{curse * 100f:0.#}，" +
                    $"开始连锁触发（继续概率 {ModConfig.Mage.ChainChance.Value * 100f:0.#}%，" +
                    $"间隔 {ModConfig.Mage.ChainIntervalSeconds.Value:0.#} 秒）。");
                return;
            }

            // 人类：立即 +石化，并登记延迟触发
            float petrify = ModConfig.Mage.HumanPetrify.Value;
            if (petrify != 0f)
                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Petrify, petrify);

            float delay = Mathf.Max(0f, ModConfig.Mage.HumanDelaySeconds.Value);
            if (delay <= 0f)
            {
                ApplyMageHumanEffect(character);
            }
            else
            {
                _magePending.Add(new MagePending { Mage = character, ApplyAt = Time.time + delay });
            }
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴：{Nick(character)} 打开卷轴，石化+{petrify * 100f:0.#}，{delay:0.#} 秒后触发随机效果。");
        }

        private static List<WeightedItem> BuildMageHumanPool()
        {
            return new List<WeightedItem>
            {
                new WeightedItem("强化", ModConfig.Mage.WeightBuff.Value),
                new WeightedItem("激化", ModConfig.Mage.WeightStamina.Value),
                new WeightedItem("固化", ModConfig.Mage.WeightInvincible.Value),
                new WeightedItem("点化", ModConfig.Mage.WeightEnlighten.Value),
                new WeightedItem("净化", ModConfig.Mage.WeightCleanse.Value),
                new WeightedItem("治愈", ModConfig.Mage.WeightHeal.Value),
                new WeightedItem("反重", ModConfig.Mage.WeightAntiGrav.Value),
                new WeightedItem("传送", ModConfig.Mage.WeightTeleport.Value),
                new WeightedItem("苏生", ModConfig.Mage.WeightRevive.Value),
            };
        }

        private static List<WeightedItem> BuildMageSkeletonPool()
        {
            return new List<WeightedItem>
            {
                new WeightedItem("血祭", ModConfig.Mage.WeightBlood.Value),
                new WeightedItem("献祭", ModConfig.Mage.WeightSacrifice.Value),
                new WeightedItem("易位", ModConfig.Mage.WeightSwap.Value),
                new WeightedItem("天灾", ModConfig.Mage.WeightDisaster.Value),
                new WeightedItem("虫灾", ModConfig.Mage.WeightPlague.Value),
                new WeightedItem("尸变", ModConfig.Mage.WeightCorpse.Value),
                new WeightedItem("还魂", ModConfig.Mage.WeightRevenant.Value),
                new WeightedItem("反噬", ModConfig.Mage.WeightBacklash.Value),
            };
        }

        /// <summary>
        /// 法师·黑魔法：执行一次连锁触发（抽取并结算 1 种效果），再按「卷轴-连锁概率」决定是否排入下一次。
        /// 首次进入时 <paramref name="pending"/>.Used 为空、立即结算；后续每次相隔「卷轴-连锁间隔」秒。
        /// 本串内已触发过的效果不再重复（故最多等于有效效果数）。
        /// </summary>
        private void ApplyMageSkeletonChainStep(MagePending pending)
        {
            var mage = pending.Mage;
            if (mage == null || mage.photonView == null || IsDead(mage)) return;

            var pool = BuildMageSkeletonPool();
            pool.RemoveAll(w => w.Weight <= 0);
            if (pending.Used != null && pending.Used.Count > 0)
                pool.RemoveAll(w => pending.Used.Contains(w.PrefabName)); // 去重：本串不重复触发同一种效果
            if (pool.Count == 0)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法：{Nick(mage)} 无更多可触发效果，连锁结束。");
                return;
            }

            string effect = WeightedPicker.Pick(pool);
            if (string.IsNullOrEmpty(effect)) return;
            if (pending.Used == null) pending.Used = new List<string>();
            pending.Used.Add(effect);
            int index = pending.Used.Count;

            Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法：{Nick(mage)} 触发第 {index} 个效果【{effect}】。");
            if (ModConfig.General.MageEffectNotify.Value)
                ShowToast($"法师·黑魔法：{Nick(mage)} 触发第 {index} 个【{effect}】");
            ApplyMageSkeletonEffect(mage, effect);

            // 判定是否继续连锁
            float chance = Mathf.Clamp01(ModConfig.Mage.ChainChance.Value);
            if (chance > 0f && Random.value < chance)
            {
                float interval = Mathf.Max(0f, ModConfig.Mage.ChainIntervalSeconds.Value);
                _magePending.Add(new MagePending
                {
                    Mage = mage,
                    ApplyAt = Time.time + interval,
                    Skeleton = true,
                    Used = pending.Used,
                });
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法：{Nick(mage)} 判定继续连锁（概率 {chance * 100f:0.#}%），{interval:0.#} 秒后触发下一个。");
            }
            else
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法：{Nick(mage)} 连锁结束（本次共触发 {index} 个效果）。");
            }
        }

        /// <summary>人类形态：随机触发 1 种效果（9 选 1）。</summary>
        private void ApplyMageHumanEffect(Character mage)
        {
            if (mage == null || mage.photonView == null) return;
            if (IsDead(mage)) return;

            var pool = BuildMageHumanPool();
            pool.RemoveAll(w => w.Weight <= 0);
            if (pool.Count == 0)
            {
                Plugin.Log.LogWarning("[RollOfTheScout] 魔法卷轴：9 种效果权重全为 0，本次不触发任何效果。");
                return;
            }

            string effect = WeightedPicker.Pick(pool);
            Plugin.Log.LogInfo($"[RollOfTheScout] 魔法卷轴：{Nick(mage)} 触发【{effect}】。");
            if (ModConfig.General.MageEffectNotify.Value)
                ShowToast($"法师·魔法卷轴：{Nick(mage)} 触发【{effect}】");
            switch (effect)
            {
                case "强化": DoMageBuff(mage); break;
                case "激化": DoMageStamina(mage); break;
                case "固化": DoMageInvincible(mage); break;
                case "点化": DoMageEnlighten(mage); break;
                case "净化": DoMageCleanse(mage); break;
                case "治愈": DoMageHeal(mage); break;
                case "反重": DoMageAntiGrav(mage); break;
                case "传送": DoMageTeleport(mage); break;
                case "苏生": DoMageRevive(mage); break;
            }
        }

        // ---------------- 人类形态：9 种效果 ----------------

        private void DoMageBuff(Character mage)
        {
            float radius = ModConfig.Mage.BuffRadius.Value;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                GrantSprint(t, "魔法卷轴·强化", clearDrowsyAfter: true);
                n++;
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 魔法卷轴·强化：{Nick(mage)} 为半径 {radius:0.#} 米内 {n} 名玩家施加加速。");
        }

        private void DoMageStamina(Character mage)
        {
            float radius = ModConfig.Mage.StaminaRadius.Value;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                GrantInfiniteStamina(t, "魔法卷轴·激化");
                n++;
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 魔法卷轴·激化：{Nick(mage)} 为半径 {radius:0.#} 米内 {n} 名玩家施加无限体力。");
        }

        private void DoMageInvincible(Character mage)
        {
            float radius = ModConfig.Mage.InvincibleRadius.Value;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                FeedTransientItem(t, ItemNames.FortifiedMilk, "魔法卷轴·固化");
                n++;
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 魔法卷轴·固化：{Nick(mage)} 为半径 {radius:0.#} 米内 {n} 名玩家施加无敌。");
        }

        private void DoMageEnlighten(Character mage)
        {
            float radius = ModConfig.Mage.EnlightenRadius.Value;
            float stamina = ModConfig.Mage.EnlightenStamina.Value;
            if (stamina == 0f) return;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (t == mage) continue;    // 不含自己
                if (IsDead(t)) continue;
                HostApi.BoostExtraStamina(t, stamina);
                n++;
            }
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴·点化：{Nick(mage)} 为半径 {radius:0.#} 米内 {n} 名玩家额外体力+{stamina * 100f:0.#}。");
        }

        private void DoMageCleanse(Character mage)
        {
            float duration = ModConfig.Mage.CleanseDuration.Value;
            float amount = ModConfig.Mage.CleansePerSecond.Value;
            if (duration <= 0f || amount <= 0f) return;
            _mageContinuous.Add(new MageContinuous
            {
                Mage = mage,
                Kind = "净化",
                Radius = ModConfig.Mage.CleanseRadius.Value,
                IncludeSelf = true,
                Amount = amount,
                Interval = 1f,
                NextTickAt = Time.time,
                EndAt = Time.time + duration,
            });
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴·净化：{Nick(mage)} 触发持续 {duration:0.#} 秒的净化（每秒 -{amount * 100f:0.#}）。");
        }

        private void DoMageHeal(Character mage)
        {
            float duration = ModConfig.Mage.HealDuration.Value;
            float amount = ModConfig.Mage.HealPerSecond.Value;
            if (duration <= 0f || amount <= 0f) return;
            _mageContinuous.Add(new MageContinuous
            {
                Mage = mage,
                Kind = "治愈",
                Radius = ModConfig.Mage.HealRadius.Value,
                IncludeSelf = true,
                Amount = amount,
                Interval = 1f,
                NextTickAt = Time.time,
                EndAt = Time.time + duration,
            });
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴·治愈：{Nick(mage)} 触发持续 {duration:0.#} 秒的治愈（每秒 -{amount * 100f:0.#}）。");
        }

        private void DoMageAntiGrav(Character mage)
        {
            float duration = ModConfig.Mage.AntiGravDuration.Value;
            float interval = ModConfig.Mage.AntiGravInterval.Value;
            string prefab = ModConfig.Mage.AntiGravPrefab.Value;
            if (duration <= 0f || interval <= 0f || string.IsNullOrEmpty(prefab)) return;
            _mageContinuous.Add(new MageContinuous
            {
                Mage = mage,
                Kind = "反重",
                Radius = ModConfig.Mage.AntiGravRadius.Value,
                IncludeSelf = true,
                Interval = interval,
                Item = prefab,
                NextTickAt = Time.time,
                EndAt = Time.time + duration,
            });
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴·反重：{Nick(mage)} 触发持续 {duration:0.#} 秒、每 {interval:0.#} 秒生成反重力场（{prefab}）。");
        }

        private void DoMageTeleport(Character mage)
        {
            float radius = ModConfig.Mage.TeleportRadius.Value;
            float minDist = ModConfig.Mage.TeleportMinDistance.Value;

            Character dest = PickRandomFarPlayer(mage, minDist);
            if (dest == null)
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 魔法卷轴·传送：{Nick(mage)} 附近没有 {minDist:0.#} 米外的玩家，本次不传送。");
                return;
            }

            Vector3 target = dest.Center;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                t.photonView.RPC("WarpPlayerRPC", RpcTarget.All, target, true);
                n++;
            }
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 魔法卷轴·传送：{Nick(mage)} 将半径 {radius:0.#} 米内 {n} 名玩家传送至 {Nick(dest)} 处。");
        }

        private void DoMageRevive(Character mage)
        {
            var dead = CollectRevivable(mage);
            if (dead.Count == 0)
            {
                HostApi.ClearAllNegativeStatuses(mage);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 魔法卷轴·苏生：{Nick(mage)} 没有可复活的玩家，改为清除自身全部负面状态（不屈）。");
                return;
            }
            var target = dead[Random.Range(0, dead.Count)];
            ReviveNear(mage, target, asSkeleton: false);
            Plugin.Log.LogInfo($"[RollOfTheScout] 魔法卷轴·苏生：{Nick(mage)} 复活了 {Nick(target)}。");
        }

        // ---------------- 骷髅形态（黑魔法）：8 种效果 ----------------

        private void ApplyMageSkeletonEffect(Character mage, string effect)
        {
            switch (effect)
            {
                case "血祭": DoMageBlood(mage); break;
                case "献祭": DoMageSacrifice(mage); break;
                case "易位": DoMageSwap(mage); break;
                case "天灾": DoMageDisaster(mage); break;
                case "虫灾": DoMagePlague(mage); break;
                case "尸变": DoMageCorpse(mage); break;
                case "还魂": DoMageRevenant(mage); break;
                case "反噬": DoMageBacklash(mage); break;
            }
        }

        private void DoMageBlood(Character mage)
        {
            float radius = ModConfig.Mage.BloodRadius.Value;
            float injury = ModConfig.Mage.BloodInjury.Value;
            float stamina = ModConfig.Mage.BloodStamina.Value;

            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsSkeleton(t)) continue;    // 只作用于人类玩家
                if (IsDead(t)) continue;
                if (injury != 0f) HostApi.ApplyStatus(t, CharacterAfflictions.STATUSTYPE.Injury, injury);
                if (stamina != 0f) HostApi.BoostExtraStamina(t, stamina);
                GrantSprint(t, "黑魔法·血祭");
                n++;
            }

            float selfCurse = ModConfig.Mage.BloodSelfCursePerTarget.Value * n;
            float selfStamina = ModConfig.Mage.BloodSelfStaminaPerTarget.Value * n;
            if (selfCurse != 0f) HostApi.ApplyStatus(mage, CharacterAfflictions.STATUSTYPE.Curse, selfCurse);
            if (selfStamina != 0f) HostApi.BoostExtraStamina(mage, selfStamina);
            GrantSprint(mage, "黑魔法·血祭");
            FeedTransientItem(mage, ItemNames.FortifiedMilk, "黑魔法·血祭");
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 黑魔法·血祭：{Nick(mage)} 影响 {n} 名人类玩家，自身诅咒+{selfCurse * 100f:0.#}、体力+{selfStamina * 100f:0.#}。");
        }

        private void DoMageSacrifice(Character mage)
        {
            float radius = ModConfig.Mage.SacrificeRadius.Value;
            string item = ModConfig.Mage.SacrificeItem.Value;
            float selfCurse = ModConfig.Mage.SacrificeSelfCurse.Value;

            var candidates = new List<Character>();
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                // 需求明确「不包括自己」：实测曾把法师自己选中，这里显式排除。
                if (t == mage) continue;
                if (IsDead(t)) continue;
                candidates.Add(t);
            }

            if (candidates.Count > 0 && !string.IsNullOrEmpty(item))
            {
                var victim = candidates[Random.Range(0, candidates.Count)];
                FeedTransientItem(victim, item, "黑魔法·献祭");
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法·献祭：{Nick(mage)} 使 {Nick(victim)} 被使用 {item}。");
            }
            else
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·献祭：{Nick(mage)} 半径 {radius:0.#} 米内没有可献祭的玩家。");
            }

            GrantSprint(mage, "黑魔法·献祭");
            GrantInfiniteStamina(mage, "黑魔法·献祭");
            FeedTransientItem(mage, ItemNames.FortifiedMilk, "黑魔法·献祭");
            if (selfCurse != 0f)
                HostApi.ApplyStatus(mage, CharacterAfflictions.STATUSTYPE.Curse, selfCurse);
        }

        private void DoMageSwap(Character mage)
        {
            float minDist = ModConfig.Mage.SwapMinDistance.Value;
            Character other = PickRandomFarPlayer(mage, minDist);
            if (other == null)
            {
                GiveMageItem(mage, ItemNames.WarpFungus);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法·易位：{Nick(mage)} 附近没有 {minDist:0.#} 米外的玩家，改为获得 1 个曲迁菇。");
                return;
            }

            Vector3 magePos = mage.Center;
            Vector3 otherPos = other.Center;
            mage.photonView.RPC("WarpPlayerRPC", RpcTarget.All, otherPos, true);
            other.photonView.RPC("WarpPlayerRPC", RpcTarget.All, magePos, true);
            Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·易位：{Nick(mage)} 与 {Nick(other)} 互换位置。");
        }

        private void DoMageDisaster(Character mage)
        {
            var candidates = new List<Character>();
            foreach (var c in HostApi.GetActiveCharacters())
            {
                if (c == null || c.photonView == null) continue;
                if (IsDead(c)) continue;
                candidates.Add(c);
            }

            // 单人测试时场上只有法师自己存活，候选可能为空 —— 此时在法师自己脚下生成，
            // 保证「天灾必定生成龙卷风」。
            Character victim = null;
            if (candidates.Count > 0) victim = candidates[Random.Range(0, candidates.Count)];
            Vector3 pos = victim != null ? victim.Center : mage.Center;

            if (SpawnMageTornado(pos))
            {
                Plugin.Log.LogInfo(victim != null
                    ? $"[RollOfTheScout] 黑魔法·天灾：{Nick(mage)} 在 {Nick(victim)} 处生成龙卷风。"
                    : $"[RollOfTheScout] 黑魔法·天灾：{Nick(mage)} 场上无其他存活玩家，在自己脚下生成龙卷风。");
            }
            else
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 黑魔法·天灾：{Nick(mage)} 龙卷风生成失败。");
            }
            FeedTransientItem(mage, ItemNames.FortifiedMilk, "黑魔法·天灾");
            TieBalloonBunches(mage, 2);
        }

        /// <summary>
        /// 黑魔法·天灾的补充效果：给目标玩家「绑 N 束气球」——等价于由系统对其使用 N 个
        /// 「一束气球」（`BalloonBunch`）。直接复刻原版 <c>Action_Balloon.RunAction</c> 对 `isBunch` 的处理：
        /// 每束依次绑 3 个气球（颜色 0 / 2 / 4），走 <c>CharacterBalloons.TieNewBalloon</c>
        /// （内部 <c>PhotonNetwork.Instantiate("TiedBalloon")</c> + <c>RPC_Init</c> 广播到 `RpcTarget.All`），
        /// 因此所有客户端（含客机）都能看到气球绑在该玩家身上并影响其浮力/跳跃。
        /// </summary>
        private void TieBalloonBunches(Character character, int bunches)
        {
            if (character == null || character.refs == null || character.refs.balloons == null) return;
            for (int i = 0; i < bunches; i++)
            {
                character.refs.balloons.TieNewBalloon(0);
                character.refs.balloons.TieNewBalloon(2);
                character.refs.balloons.TieNewBalloon(4);
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·天灾：{Nick(character)} 身上绑了 {bunches} 束气球（每束 3 个）。");
        }

        private void DoMagePlague(Character mage)
        {
            float radius = ModConfig.Mage.PlagueRadius.Value;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                SpawnMageBeetle(t);
                n++;
            }
            GiveMageItem(mage, ItemNames.Beehive);
            Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·虫灾：{Nick(mage)} 为半径 {radius:0.#} 米内 {n} 名玩家（含自己）头顶生成甲虫，自身获得 1 个蜂巢。");
        }

        private void DoMageCorpse(Character mage)
        {
            float radius = ModConfig.Mage.CorpseRadius.Value;
            float spores = ModConfig.Mage.CorpseSpores.Value;
            float selfCurse = ModConfig.Mage.CorpseSelfCurse.Value;

            var humans = new List<Character>();
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (t == mage) continue;      // 不含自己
                if (IsSkeleton(t)) continue;  // 只针对人类玩家
                if (IsDead(t)) continue;
                humans.Add(t);
            }

            // ① 优先：半径内随机 1 名「人类玩家」（不含自己）+孢子
            if (humans.Count > 0 && spores != 0f)
            {
                var victim = humans[Random.Range(0, humans.Count)];
                HostApi.ApplyStatus(victim, CharacterAfflictions.STATUSTYPE.Spores, spores);
                Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·尸变：{Nick(mage)} 使 {Nick(victim)} 孢子 +{spores * 100f:0.#}。");
            }

            // ② 无论是否有符合条件的人类玩家，法师自身都获得 1 个曼德拉草并 +自身诅咒
            GiveMageItem(mage, ItemNames.Mandrake);
            if (selfCurse != 0f)
                HostApi.ApplyStatus(mage, CharacterAfflictions.STATUSTYPE.Curse, selfCurse);

            // ③ 若半径内没有符合条件的人类玩家，则改为在自身半径外生成 1 只僵尸
            if (humans.Count == 0)
            {
                float dist = ModConfig.Mage.CorpseZombieDistance.Value;
                SpawnMageZombie(mage, dist);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法·尸变：{Nick(mage)} 半径 {radius:0.#} 米内没有符合条件的人类玩家，改为在 {dist:0.#} 米外生成 1 只僵尸。");
            }
        }

        private void DoMageRevenant(Character mage)
        {
            var dead = CollectRevivable(mage);
            if (dead.Count == 0)
            {
                SpawnMageScoutmaster(mage);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 黑魔法·还魂：{Nick(mage)} 没有可复活的玩家，改为在半径 {ModConfig.Mage.RevenantDistance.Value:0.#} 米外召唤 1 名童军领队" +
                    $"（强制追击 {ModConfig.Mage.RevenantChaseTime.Value:0.#} 秒，随后按原版喇叭机制停止追击离去）。");
                return;
            }
            var target = dead[Random.Range(0, dead.Count)];
            ReviveNear(mage, target, asSkeleton: true);
            Plugin.Log.LogInfo($"[RollOfTheScout] 黑魔法·还魂：{Nick(mage)} 复活了 {Nick(target)} 并使其变为骷髅。");
        }

        private void DoMageBacklash(Character mage)
        {
            float radius = ModConfig.Mage.BacklashRadius.Value;
            float curse = ModConfig.Mage.BacklashCurse.Value;
            float petrify = ModConfig.Mage.BacklashPetrify.Value;
            int n = 0;
            foreach (var t in HostApi.GetCharactersInRadius(mage.Center, radius))
            {
                if (t == null || t.photonView == null) continue;
                if (IsDead(t)) continue;
                // 同一时刻对同一玩家施加诅咒 + 石化：合并为单条 RPC（语义与逐条等价）
                HostApi.ApplyStatusPair(t,
                    CharacterAfflictions.STATUSTYPE.Curse, curse,
                    CharacterAfflictions.STATUSTYPE.Petrify, petrify);
                n++;
            }
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 黑魔法·反噬：{Nick(mage)} 使半径 {radius:0.#} 米内 {n} 名玩家诅咒+{curse * 100f:0.#}、石化+{petrify * 100f:0.#}。");
        }

        // ---------------- 法师公共辅助 ----------------

        /// <summary>收集可复活（真正死亡或完全昏迷）的玩家，不含施法者自己。</summary>
        private static List<Character> CollectRevivable(Character mage)
        {
            var result = new List<Character>();
            foreach (var c in HostApi.GetActiveCharacters())
            {
                if (c == null || c.photonView == null) continue;
                if (c == mage) continue;
                if (IsDead(c)) result.Add(c);
            }
            return result;
        }

        /// <summary>在与 <paramref name="mage"/> 距离超过 <paramref name="minDist"/> 的存活玩家中随机取一名（不含自己）。</summary>
        private static Character PickRandomFarPlayer(Character mage, float minDist)
        {
            var candidates = new List<Character>();
            foreach (var c in HostApi.GetActiveCharacters())
            {
                if (c == mage) continue;
                if (IsTrueDead(c)) continue;
                if (Vector3.Distance(mage.Center, c.Center) <= minDist) continue;
                candidates.Add(c);
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 把 <paramref name="target"/> 复活在 <paramref name="mage"/> 身边。
        /// 走原版 <c>RPCA_ReviveAtPosition(position, applyStatus, statueSegment)</c>（与童军雕像 / 骸骨之书同通道）；
        /// <paramref name="asSkeleton"/> 为 true 时，紧接其后把目标变为骷髅。
        /// </summary>
        private void ReviveNear(Character mage, Character target, bool asSkeleton)
        {
            Vector3 pos = MageRevivePosition(mage);
            target.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, pos, false, -1);
            if (asSkeleton)
                target.data.SetSkeleton(true);
        }

        /// <summary>复活点：法师身边（水平方向随机偏移「复活-距离」米）。</summary>
        private static Vector3 MageRevivePosition(Character mage)
        {
            float dist = Mathf.Max(0.5f, ModConfig.Mage.ReviveDistance.Value);
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir = dir.normalized * dist;
            return mage.Center + new Vector3(dir.x, 0f, dir.y) + Vector3.up * 0.5f;
        }

        /// <summary>给法师发放一件物品：优先放入 3 个物品栏空位，物品栏已满时在脚下生成。</summary>
        private void GiveMageItem(Character mage, string prefabName)
        {
            if (mage == null || mage.photonView == null) return;
            if (string.IsNullOrEmpty(prefabName)) return;

            if (HostApi.GiveItemToInventorySlots(mage, prefabName, out _)) return;
            if (HostApi.SpawnItemAtFeet(mage, prefabName) != null)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 法师：{Nick(mage)} 物品栏已满，{prefabName} 改在脚下生成。");
            }
        }

        /// <summary>
        /// 天灾：在指定位置生成一个龙卷风，并在「天灾-存在时长」秒后由主机销毁，返回是否生成成功。
        ///
        /// 原版龙卷风的 <c>RPCA_InitTornado(targetViewID)</c> 需要一个带 "TornadoPoints" 子物体的
        /// <c>TornadoSpawner</c> 的 PhotonView.ViewID。关卡内存在该 spawner 时按原版行为朝预置
        /// TornadoPoints 漂移；不存在时（实测单人测试关卡即无）仍会生成龙卷风，但跳过
        /// <c>RPCA_InitTornado</c>（其内部 PhotonView.Find 会拿到 null），龙卷风原地停留 ——
        /// 此时靠 <c>TornadoPickTargetPatch</c> 跳过原版 PickTarget，避免 targetParent 为空导致每帧 NRE。
        /// </summary>
        private bool SpawnMageTornado(Vector3 position)
        {
            var spawnerView = ResolveMageTornadoInitView();

            var go = PhotonNetwork.Instantiate(ItemNames.Tornado, position, Quaternion.identity, 0);
            if (go == null) return false;
            var view = go.GetComponent<PhotonView>();
            if (view == null)
            {
                PhotonNetwork.Destroy(go);
                return false;
            }

            if (spawnerView != null)
            {
                // 关卡内存在带 TornadoPoints 的 TornadoSpawner：按原版行为朝预置目标点漂移。
                view.RPC("RPCA_InitTornado", RpcTarget.All, spawnerView.ViewID);
            }
            else
            {
                // 关卡内没有 TornadoSpawner（实测单人测试关卡即是如此）：仍生成龙卷风，
                // 但不调 RPCA_InitTornado（其内部 PhotonView.Find 会拿到 null）。
                // 此时 targetParent 为空，由 TornadoPickTargetPatch 跳过原版 PickTarget 防止每帧 NRE，
                // 龙卷风原地停留，仍由主机按时长销毁（符合「只存在 5 秒」的需求）。
                Plugin.Log.LogInfo(
                    "[RollOfTheScout] 黑魔法·天灾：关卡内无 TornadoSpawner，龙卷风将在原地停留而非漂移。");
            }

            float lifetime = Mathf.Max(1f, ModConfig.Mage.DisasterLifetime.Value);
            StartCoroutine(DestroyAfter(go, lifetime));
            return true;
        }

        /// <summary>
        /// 解析原版龙卷风初始化所需的 ViewID 来源（带 "TornadoPoints" 子物体的 PhotonView）。
        /// 优先取场景中的 <c>TornadoSpawner</c>，找不到时退化为扫描所有 PhotonView 找含 TornadoPoints 子物体者。
        /// </summary>
        private static PhotonView ResolveMageTornadoInitView()
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<TornadoSpawner>();
            if (spawner != null)
            {
                var v = spawner.GetComponent<PhotonView>();
                if (v != null) return v;
            }
            var views = UnityEngine.Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var v in views)
            {
                if (v == null) continue;
                if (v.transform.Find("TornadoPoints") != null) return v;
            }
            return null;
        }

        // ---------------- 实体类效果：反重力场 / 甲虫 / 僵尸 / 童军领队 ----------------

        /// <summary>
        /// 通用实体生成：按候选名逐个尝试 <c>PhotonNetwork.Instantiate</c>，返回首个成功的对象（失败返回 null）。
        /// 实体类预制体（反重力场 / 甲虫 / 僵尸 / 童军领队）使用「裸名」，**不加** <c>0_Items/</c> 前缀
        /// （该前缀只用于普通物品，见 <see cref="HostApi.SpawnWorldItem"/>）。
        /// </summary>
        private static GameObject SpawnMagePrefab(string[] candidates, Vector3 pos, Quaternion rot)
        {
            if (candidates == null) return null;
            foreach (var name in candidates)
            {
                if (string.IsNullOrEmpty(name)) continue;
                try
                {
                    var go = PhotonNetwork.Instantiate(name, pos, rot, 0);
                    if (go != null)
                    {
                        go.SetActive(true);
                        return go;
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"[RollOfTheScout] 法师：生成实体预制体 {name} 失败：{e.Message}");
                }
            }
            return null;
        }

        /// <summary>反重力场预制体名缓存（首次从反重力炮 action 解析后复用）。</summary>
        private static string _mageAntiSpherePrefab;

        /// <summary>
        /// 反重：在指定位置生成一个「反重力场」（与反重力炮开炮产生的实体同一个预制体，也即烤过后的大反重力场本体）。
        /// 反重力场没有初始化 RPC，Instantiate + SetActive 即成（参照参考模组 Trapped_Luggage 的做法）。
        /// </summary>
        private void SpawnMageAntiSphere(Vector3 position)
        {
            var candidates = new List<string>();
            string resolved = ResolveMageAntiSpherePrefab();
            if (!string.IsNullOrEmpty(resolved)) candidates.Add(resolved);
            candidates.Add(ItemNames.AntiSphere);
            candidates.Add("0_Items/AntiSphere");
            candidates.Add("AntigravSphere");
            candidates.Add("AntiGravSphere");
            candidates.Add("AntiGravBubble");

            var go = SpawnMagePrefab(candidates.ToArray(), position, Quaternion.identity);
            if (go == null)
                Plugin.Log.LogWarning("[RollOfTheScout] 魔法卷轴·反重：反重力场生成失败（找不到已注册的 AntiSphere 预制体）。");
        }

        /// <summary>从场景里的反重力炮（<c>Action_RaycastSpawnSomething</c>）解析反重力场预制体的真实注册名。</summary>
        private static string ResolveMageAntiSpherePrefab()
        {
            if (!string.IsNullOrEmpty(_mageAntiSpherePrefab)) return _mageAntiSpherePrefab;
            try
            {
                foreach (var action in Resources.FindObjectsOfTypeAll<Peak.Action_RaycastSpawnSomething>())
                {
                    if (action == null || action.prefabToSpawn == null) continue;
                    if (action.prefabToSpawn.GetComponent<Peak.AntiSphere>() == null &&
                        action.prefabToSpawn.GetComponentInChildren<Peak.AntiSphere>(true) == null) continue;
                    _mageAntiSpherePrefab = action.prefabToSpawn.gameObject.name;
                    Plugin.Log.LogInfo($"[RollOfTheScout] 反重力场：已解析预制体名 {_mageAntiSpherePrefab}。");
                    return _mageAntiSpherePrefab;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 反重力场：预制体名解析失败：{e.Message}");
            }
            return null;
        }

        /// <summary>
        /// 虫灾：在目标玩家「头顶上方」生成 1 只真甲虫（<c>Beetle</c>，森蕈关卡会追玩家顶玩家的那种，非蜱虫 <c>BugfixOnYou</c>），
        /// 并唤醒它、强制其索敌目标为该玩家。
        /// </summary>
        private void SpawnMageBeetle(Character target)
        {
            if (target == null || target.photonView == null) return;
            float height = Mathf.Max(0.5f, ModConfig.Mage.PlagueHeight.Value);
            Vector3 pos = target.Center + Vector3.up * height;

            var go = SpawnMagePrefab(new[] { "0_Items/Beetle", ItemNames.Beetle }, pos, Random.rotation);
            if (go == null)
            {
                Plugin.Log.LogWarning("[RollOfTheScout] 黑魔法·虫灾：甲虫生成失败（找不到已注册的 Beetle 预制体）。");
                return;
            }
            StartCoroutine(AggroMageBeetle(go, target));
        }

        /// <summary>唤醒甲虫并多次强制其追击目标（甲虫 Update 需 <c>Time.time - _timeSpawned ≥ 0.2s</c> 才走 AI，故延迟重试）。</summary>
        private IEnumerator AggroMageBeetle(GameObject go, Character target)
        {
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 4; i++)
            {
                if (go == null || target == null || target.photonView == null) yield break;
                ForceMageMobAggro(go, target);
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>
        /// 强制 Mob 索敌：只用公开成员（<c>sleeping</c>/<c>hasBrain</c>/<c>aggroDistance</c>/<c>SetForcedTarget</c>/<c>UpdateSleeping</c>）
        /// 与字符串 RPC（<c>RPC_SyncMobState</c>/<c>RPC_SyncTargetCharacter</c>），避免访问 internal/private 成员。
        /// </summary>
        private void ForceMageMobAggro(GameObject go, Character target)
        {
            var mob = go.GetComponent<Mob>() ?? go.GetComponentInChildren<Mob>(true);
            if (mob == null) return;

            mob.sleeping = false;
            mob.hasBrain = true;
            mob.aggroDistance = Mathf.Max(mob.aggroDistance, 40f);
            mob.SetForcedTarget(target);
            mob.UpdateSleeping();

            if (Beetle.playersIgnoredUntil != null && target.photonView.Owner != null)
                Beetle.playersIgnoredUntil.Remove(target.photonView.Owner);

            var view = go.GetComponent<PhotonView>() ?? go.GetComponentInChildren<PhotonView>(true);
            if (view == null) return;
            view.RPC("RPC_SyncMobState", RpcTarget.All, 1);   // MobState.Walking = 1，让甲虫行走/追击
            if (target.photonView.ViewID > 0)
                view.RPC("RPC_SyncTargetCharacter", RpcTarget.All, target.photonView.ViewID);
        }

        /// <summary>
        /// 尸变兜底：在法师「半径 X 米外」生成 1 只蘑菇僵尸。
        /// 原版 <c>WakeUpFromSleep()</c> 为私有方法，这里用公开成员复现其唤醒效果（清倒地计时 + 解除 ragdoll 锁定 + 切到 WakingUp），
        /// 并把 <c>initialWakeUpTime</c> 置 0 以立即转入 Idle（僵尸随后自然索敌最近玩家）。
        /// </summary>
        private void SpawnMageZombie(Character mage, float distance)
        {
            if (mage == null) return;
            float dist = Mathf.Max(1f, distance);
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir = dir.normalized * dist;
            Vector3 pos = mage.Center + new Vector3(dir.x, 0f, dir.y);

            var go = SpawnMagePrefab(new[] { ItemNames.MushroomZombie, "MushroomZombie_Player", "0_Items/MushroomZombie" }, pos, Random.rotation);
            if (go == null)
            {
                Plugin.Log.LogWarning("[RollOfTheScout] 黑魔法·尸变：僵尸生成失败（找不到已注册的 MushroomZombie 预制体）。");
                return;
            }
            var zombie = go.GetComponent<MushroomZombie>();
            if (zombie == null)
            {
                PhotonNetwork.Destroy(go);
                return;
            }

            zombie.initialWakeUpTime = 0f;
            var ch = go.GetComponent<Character>();
            if (ch != null)
            {
                ch.data.fallSeconds = 0f;
                ch.data.currentRagdollControll = 0f;
                ch.refs.animations.ResetForceSpeed();
                ch.refs.ragdoll.ToggleKinematic(false);
            }
            zombie.currentState = MushroomZombie.State.WakingUp;
        }

        /// <summary>
        /// 还魂兜底：在法师「半径 X 米外」召唤 1 名童军领队（房间对象），并强制其追击法师 X 秒
        /// （直接调 <c>RPCA_SetCurrentTarget</c>，绕过 <c>SetCurrentTarget</c> 的 preventSpawning 校验；
        /// 超时后领队恢复原版 AI —— 被发现时远遁，即「停止追击离去」，同原版喇叭机制）。
        /// 位置取「以法师为圆心、随机方向、距离 X 米」，保证落在半径外（对齐 <see cref="SpawnMageZombie"/>）。
        /// </summary>
        private void SpawnMageScoutmaster(Character mage)
        {
            if (mage == null || mage.photonView == null) return;
            float dist = Mathf.Max(1f, ModConfig.Mage.RevenantDistance.Value);
            float chase = Mathf.Max(1f, ModConfig.Mage.RevenantChaseTime.Value);

            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir = dir.normalized * dist;
            Vector3 pos = mage.Center + new Vector3(dir.x, 0f, dir.y);

            GameObject go;
            try
            {
                go = PhotonNetwork.InstantiateRoomObject(ItemNames.Scoutmaster, pos, Quaternion.identity, 0);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 黑魔法·还魂：童军领队生成失败：{e.Message}");
                return;
            }
            if (go == null) return;
            StartCoroutine(InitMageScoutmaster(go, mage, pos, chase));
        }

        /// <summary>等待童军领队初始化后，把它传送到指定位置并强制追击法师若干秒。</summary>
        private IEnumerator InitMageScoutmaster(GameObject go, Character mage, Vector3 pos, float chase)
        {
            var view = go.GetComponent<PhotonView>();
            for (int i = 0; i < 12 && view == null; i++)
            {
                yield return new WaitForSeconds(0.25f);
                if (go == null) yield break;
                view = go.GetComponent<PhotonView>();
            }
            if (view == null || mage == null || mage.photonView == null) yield break;

            view.RPC("WarpPlayerRPC", RpcTarget.All, pos, false);
            if (mage.photonView.ViewID > 0)
                view.RPC("RPCA_SetCurrentTarget", RpcTarget.All, mage.photonView.ViewID, chase);
        }

        private static List<WeightedItem> BuildExplorerPool()
        {
            return new List<WeightedItem>
            {
                new WeightedItem(ItemNames.RopeSpool, ModConfig.Explorer.RopeSpoolWeight.Value),
                new WeightedItem(ItemNames.ClimbingSpike, ModConfig.Explorer.ClimbingSpikeWeight.Value),
                new WeightedItem(ItemNames.ScoutCookies, ModConfig.Explorer.ScoutCookiesWeight.Value),
                new WeightedItem(ItemNames.ChainShooter, ModConfig.Explorer.ChainShooterWeight.Value),
                new WeightedItem(ItemNames.RescueHook, ModConfig.Explorer.RescueHookWeight.Value),
                new WeightedItem(ItemNames.AntiZooka, ModConfig.Explorer.AntiZookaWeight.Value),
            };
        }

        private static List<WeightedItem> BuildDoctorPool()
        {
            return new List<WeightedItem>
            {
                new WeightedItem(ItemNames.Bandages, ModConfig.Doctor.BandagesWeight.Value),
                new WeightedItem(ItemNames.MedicinalRoot, ModConfig.Doctor.MedicinalRootWeight.Value),
                new WeightedItem(ItemNames.FirstAidKit, ModConfig.Doctor.FirstAidKitWeight.Value),
                new WeightedItem(ItemNames.CureAll, ModConfig.Doctor.CureAllWeight.Value),
                new WeightedItem(ItemNames.HealingPuffShroom, ModConfig.Doctor.HealingPuffShroomWeight.Value),
            };
        }

        /// <summary>备战充分的物品池：背包 / 喷气背包 按配置权重二选一（默认各 50%）。</summary>
        private static List<WeightedItem> BuildLogisticianBackpackPool()
        {
            return new List<WeightedItem>
            {
                new WeightedItem(ItemNames.Backpack, ModConfig.Logistician.BackpackWeight.Value),
                new WeightedItem(ItemNames.Jetpack, ModConfig.Logistician.JetpackWeight.Value),
            };
        }

        // ---------------------------------------------------------------
        // 通用·夜晚抵消寒冷（天阶 5 及以上难度的夜寒）
        // ---------------------------------------------------------------

        /// <summary>
        /// 「夜晚抵消寒冷」：游戏在天阶 5 及以上（自定义局为「冷夜」开关）的**夜晚**，
        /// 会给玩家的角色持续叠加「寒冷」——速率 <c>Ascents.nightColdRate</c>（= 0.005/秒，
        /// 满夜时正好「每 5 秒 +2.5 寒冷」，与用户实测一致；非自定义局的 <c>etcDamageMultiplier</c> 为 1，
        /// 不会再加倍）。
        ///
        /// 关键约束：这段夜寒是在 <c>CharacterAfflictions.UpdateNormalStatuses()</c> 里、
        /// 且在 <c>!photonView.IsMine</c> 直接 return 之后的 <c>character.IsLocal</c> 分支中结算的 ——
        /// 也就是说**只会在每个玩家自己的客户端上、对自己的角色生效**。本模组「仅主机安装」，
        /// 无法用补丁替客机关掉它。因此这里反向补偿：由主机周期性给所有存活玩家 +「炎热」——
        /// 原版里炎热与寒冷**互为抵消**（<c>AddStatus</c> 内：加冷时若有炎热会先扣炎热，反之亦然），
        /// 默认值 0.025/5 秒正好抵消满夜夜寒。
        ///
        /// 生效条件与原版完全对齐（避免在游戏本就不加夜寒时白给炎热）：
        /// 夜晚（<c>DayNightManager.isDay &lt; 0.5</c>，该值实际为 0/1）、<c>Ascents.isNightCold</c>、
        /// 且尚未走完 3 个检查点（<c>MountainProgressHandler.maxProgressPointReached &lt; 3</c>）。
        /// </summary>
        private void DoNightColdOffset(List<Character> characters)
        {
            if (!ModConfig.General.NightHeatEnabled.Value)
            {
                _nightHeatTimer = 0f;
                _nightHeatLogged = false;
                return;
            }

            float interval = ModConfig.General.NightHeatIntervalSeconds.Value;
            float amount = ModConfig.General.NightHeatAmount.Value;
            if (interval <= 0f || amount <= 0f)
            {
                _nightHeatTimer = 0f;
                _nightHeatLogged = false;
                return;
            }

            // 与原版夜寒的触发条件对齐（见方法注释）
            var dayNight = DayNightManager.instance;
            if (dayNight == null) return;
            if (dayNight.isDay >= 0.5f)                 // 白天：游戏不加夜寒，也不补偿
            {
                _nightHeatTimer = 0f;
                _nightHeatLogged = false;
                return;
            }
            if (!Ascents.isNightCold)                   // 天阶 < 5 且自定义局未开「冷夜」
            {
                _nightHeatTimer = 0f;
                _nightHeatLogged = false;
                return;
            }
            var progress = Zorro.Core.Singleton<MountainProgressHandler>.Instance;
            if (progress != null && progress.maxProgressPointReached >= 3)
            {
                _nightHeatTimer = 0f;
                _nightHeatLogged = false;
                return;
            }

            _nightHeatTimer += Time.deltaTime;
            if (_nightHeatTimer < interval) return;
            _nightHeatTimer = 0f;

            int n = 0;
            foreach (var character in characters)
            {
                if (character == null || character.photonView == null) continue;
                if (IsDead(character)) continue;        // 死亡 / 灵魂状态不补
                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hot, amount);
                n++;
            }

            if (!_nightHeatLogged)
            {
                _nightHeatLogged = true;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 夜晚抵消寒冷：夜晚（天阶 {Ascents.currentAscent}）开始，每 {interval:0.#} 秒为 {n} 名存活玩家 +{amount * 100f:0.#} 炎热。");
            }
        }

        // ---------------------------------------------------------------
        // 闭环检测：不屈 / 高代谢 / 共情
        // ---------------------------------------------------------------

        private void PollClosedLoop(List<Character> characters)
        {
            _pollTimer += Time.deltaTime;
            float interval = Mathf.Max(0.1f, ModConfig.General.PollInterval.Value);
            if (_pollTimer < interval) return;
            float elapsed = _pollTimer;
            _pollTimer = 0f;

            bool anyDoctor = _ledger.HasClassInRoom(ClassType.Doctor);
            bool anyNecromancer = _ledger.HasClassInRoom(ClassType.Necromancer);
            bool anyFirefighter = _ledger.HasClassInRoom(ClassType.Firefighter);
            bool anyM = _ledger.HasClassInRoom(ClassType.M);
            bool anyGunner = _ledger.HasClassInRoom(ClassType.Gunner);
            bool anyMilesSoul = _ledger.HasClassInRoom(ClassType.MilesSoul);
            bool anyAstronaut = _ledger.HasClassInRoom(ClassType.Astronaut);

            // ---- 第一遍：只用「真死亡」判定领袖的死亡 / 复活（昏迷与被救起都不算）----
            //   士气的施加必须早于、撤销必须晚于所有角色的「复活继承」：
            //   · 先施加：同一 tick 内阵亡的受罚玩家，其死亡记录里也包含这份惩罚，
            //     撤销时再按记录扣回来，最终值才对得上（实测问题 3）；
            //   · 后撤销：避免「先撤销 -20 → 随后 RestoreOnRevive 又按死亡记录加回 +20」。
            List<Character> diedLeaders = null;
            List<Character> revivedLeaders = null;
            foreach (var character in characters)
            {
                var view = character.photonView;
                if (view == null || view.Owner == null) continue;
                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_wasDead.TryGetValue(id, out bool was)) continue;      // 尚未登记过：本 tick 只登记
                if (_ledger.GetClass(character) != ClassType.Leader) continue;

                bool nowDead = IsTrueDead(character);
                if (!was && nowDead)
                {
                    if (diedLeaders == null) diedLeaders = new List<Character>();
                    diedLeaders.Add(character);
                }
                else if (was && !nowDead)
                {
                    if (revivedLeaders == null) revivedLeaders = new List<Character>();
                    revivedLeaders.Add(character);
                }
            }
            if (diedLeaders != null)
            {
                foreach (var leader in diedLeaders) ApplyMorale(leader);
            }

            // ---- 第二遍：逐个角色处理死亡 / 复活事件 ----
            foreach (var character in characters)
            {
                var view = character.photonView;
                if (view == null || view.Owner == null) continue;
                string actor = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(actor)) continue;

                // 死亡 / 复活一律以「真正死亡」为准：共情、复活继承都只应在队友真正死亡时发生
                // （实测问题 5 / 6：昏迷并非死亡，不应触发共情、也不应触发复活继承）。
                bool dead = IsTrueDead(character);
                bool wasDead = _wasDead.TryGetValue(actor, out bool w) && w;

                // 骷髅形态：本轮的观测值 + **上一轮**的观测值。死亡那一刻 isSkeleton 会被同步清除，
                // 因此「骷髅玩家死亡」必须用上一轮的值判定（见 _wasSkeleton 字段说明）。
                bool skeletonNow = IsSkeleton(character);
                bool wasSkeleton = _wasSkeleton.TryGetValue(actor, out bool sk) && sk;

                if (_wasDead.ContainsKey(actor))
                {
                    if (!wasDead && dead)
                    {
                        if (anyDoctor) ApplyEmpathy();
                        if (anyNecromancer) ApplyNecromancerDeath();
                        if (anyFirefighter) ApplyFirefighterDuty(wasSkeleton || skeletonNow);
                        if (anyMilesSoul) ApplyMilesDeath();
                        // 区·！？区区？！：真正死亡时在尸体处生成一只烤熟的早起虫儿
                        if (_ledger.GetClass(character) == ClassType.Qu) SpawnQuCookedWorm(character);
                        CaptureOnDeath(character);
                    }
                    else if (wasDead && !dead)
                    {
                        // 亡灵·复活：复活为「骷髅」还是「人类」以 data.isSkeleton 为准。
                        // 原版 Skelleton.Interact_CastFinished 先 SetSkeleton(true) 再广播复活，
                        // 两条 RPC 同属一个 PhotonView 且有序，主机此处置信该值即可。
                        if (anyNecromancer) ApplyNecromancerRevive(IsSkeleton(character));
                        // 「复活继承」会按死亡记录恢复诅咒 / 石化；中途加入者的首次复活由
                        // UpdateJoinSoulSettle 负责（他们因此前一直处于 _joinSouls，未产生死亡记录、也未提前发放）。
                        RestoreOnRevive(character);
                    }
                }
                _wasDead[actor] = dead;
                _wasSkeleton[actor] = skeletonNow;

                TickPetrifyRestore(character, actor);
                TickInitialCurseGrant(character, actor);
            }

            if (revivedLeaders != null)
            {
                foreach (var leader in revivedLeaders) RevertMorale(leader);
            }

            // ---- M·「享受」：对比上次轮询快照，把新受到的 9 种负面状态等量转化为额外体力 ----
            if (anyM) PollSuffer(characters);

            // ---- 炮手·炸膛：对比上次轮询的损伤快照，把「新受到的损伤」转化为炸药 ----
            if (anyGunner) PollGunnerInjury(characters);

            // ---- 太空人·NASA：对比上次轮询快照，为「新获得的蛛网 / 捕蝇草 / 诅咒」追加同种状态 ----
            if (anyAstronaut) PollAstronautExtraStatus(characters);

            void ApplyEmpathy()
            {
                float damage = ModConfig.Doctor.EmpathyDamage.Value;
                float cooldown = ModConfig.Doctor.EmpathyCooldown.Value;
                foreach (var doctor in characters)
                {
                    if (_ledger.GetClass(doctor) != ClassType.Doctor) continue;

                    // 冷却：刚受过一次共情损伤的医生，在 cooldown 秒内不再重复扣损伤，
                    // 避免「同一时刻死了好几个人 → 医生被连扣数次直接暴毙」（实测问题 8）。
                    string id = _ledger.IdOf(doctor);
                    if (cooldown > 0f && !string.IsNullOrEmpty(id)
                        && _empathyCooldownUntil.TryGetValue(id, out float until) && Time.time < until)
                    {
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 共情：{Nick(doctor)} 处于冷却中（剩 {until - Time.time:0.#}s），本次不再受到损伤。");
                        continue;
                    }

                    HostApi.ApplyStatus(doctor, CharacterAfflictions.STATUSTYPE.Injury, damage);
                    if (cooldown > 0f && !string.IsNullOrEmpty(id))
                        _empathyCooldownUntil[id] = Time.time + cooldown;
                    Plugin.Log.LogInfo($"[RollOfTheScout] 共情：{Nick(doctor)} 因队友死亡 +{damage} 损伤");
                }
            }

            // 亡灵：每有 1 名玩家「真正死亡」，所有死灵法师各减一次诅咒、加一次额外体力。
            // 与共情一样只在真死亡事件里触发（昏迷不算），且计入所有死灵法师（含死亡者本人）。
            void ApplyNecromancerDeath()
            {
                float curse = ModConfig.Necromancer.DeathCurseReduce.Value;
                float stamina = ModConfig.Necromancer.DeathExtraStamina.Value;
                foreach (var necro in characters)
                {
                    if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;

                    if (curse != 0f)
                        HostApi.ApplyStatus(necro, CharacterAfflictions.STATUSTYPE.Curse, -curse);
                    if (stamina != 0f)
                        HostApi.BoostExtraStamina(necro, stamina);

                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 亡灵：{Nick(necro)} 因有玩家死亡 诅咒-{curse * 100f:0.#}、额外体力+{stamina * 100f:0.#}");
                }
            }

            // 尽职尽责：每有 1 名「人类玩家」真正死亡（骷髅玩家不算），所有救火队员各 +1 次石化（默认 +15）。
            // 与共情 / 亡灵一致：只在真死亡事件里触发（昏迷不算），计入所有救火队员。
            //
            // 骷髅判定用「上一轮轮询的骷髅形态 或 本轮值」：骷髅玩家死亡时其客户端会同时广播
            // dead = true 与 isSkeleton = false，主机这一轮往往已经读不到 true 了（见 _wasSkeleton 说明）。
            void ApplyFirefighterDuty(bool wasSkeleton)
            {
                if (wasSkeleton) return;   // 只计人类玩家；骷髅玩家死亡不触发

                float petrify = ModConfig.Firefighter.DutyPetrify.Value;
                if (petrify <= 0f) return;

                float cooldown = ModConfig.Firefighter.DutyCooldownSeconds.Value;

                foreach (var ff in characters)
                {
                    if (_ledger.GetClass(ff) != ClassType.Firefighter) continue;

                    string id = _ledger.IdOf(ff);
                    if (string.IsNullOrEmpty(id)) continue;

                    // 冷却：每名救火队员在「尽职尽责-冷却」内最多结算一次（仍由每次死亡触发）
                    if (cooldown > 0f && _dutyCooldownUntil.TryGetValue(id, out float until) && Time.time < until)
                        continue;
                    if (cooldown > 0f) _dutyCooldownUntil[id] = Time.time + cooldown;

                    HostApi.ApplyStatus(ff, CharacterAfflictions.STATUSTYPE.Petrify, petrify);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 尽职尽责：{Nick(ff)} 因有人类玩家死亡 石化+{petrify * 100f:0.#}");
                }
            }

            // 亡灵·复活：每有 1 名玩家复活，按「复活为骷髅 / 人类」给所有死灵法师各调整一次诅咒。
            // 复活为人类 → 增加诅咒（默认 +5）；复活为骷髅 → 减少诅咒（默认 -10）。
            // 与死亡事件一致，计入所有死灵法师（含复活者本人）。
            void ApplyNecromancerRevive(bool asSkeleton)
            {
                float delta = asSkeleton
                    ? ModConfig.Necromancer.ReviveSkeletonCurse.Value
                    : ModConfig.Necromancer.ReviveHumanCurse.Value;
                if (delta == 0f) return;

                foreach (var necro in characters)
                {
                    if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;

                    HostApi.ApplyStatus(necro, CharacterAfflictions.STATUSTYPE.Curse, delta);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 亡灵：{Nick(necro)} 因有玩家复活为{(asSkeleton ? "骷髅" : "人类")} " +
                        $"诅咒{delta * 100f:+0.#;-0.#;0}");
                }
            }

            // 零号守则：每有 1 名玩家真正死亡（昏迷不算），所有迈尔斯残魂各 +1 次损伤（默认 +10）。
            // 前置条件：本局已拾取过「童军的毅力」（`_milesGemPicked` 含 Amulet_Healing）。
            void ApplyMilesDeath()
            {
                if (!MilesRuleZeroUnlocked()) return;

                float injury = ModConfig.MilesSoul.RuleZeroDeathInjury.Value;
                if (injury <= 0f) return;

                foreach (var miles in characters)
                {
                    if (_ledger.GetClass(miles) != ClassType.MilesSoul) continue;

                    HostApi.ApplyStatus(miles, CharacterAfflictions.STATUSTYPE.Injury, injury);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 零号守则：{Nick(miles)} 因有玩家死亡 损伤+{injury * 100f:0.#}");
                }
            }
        }

        /// <summary>
        /// 该角色当前是否带有给定状态集合中的**任意一种**（读了值大于 <see cref="StatusPresenceEpsilon"/> 即视为「有」）。
        /// 用于【享受】的「无任何特定负面状态」判定。
        /// ⚠ 边界：`Thorns(9)` / `Arrow(12)` 属原版状态数组通道的硬性排除项，主机对客机读到的值可能偏低或恒为 0
        /// （见技术文档 §3.9 已知边界），故这两项在客机身上可能判不出「有」。
        /// </summary>
        private static bool HasAnyStatus(Character character, CharacterAfflictions.STATUSTYPE[] types)
        {
            if (character == null || types == null) return false;
            for (int i = 0; i < types.Length; i++)
            {
                if (HostApi.GetStatus(character, types[i]) > StatusPresenceEpsilon) return true;
            }
            return false;
        }

        /// <summary>
        /// M·「享受」：把 M 新受到的 9 种负面状态（损伤 / 中毒 / 孢子 / 荆棘 / 烧伤 / 冻伤 / 蛛网 / 箭 / 捕蝇草）
        /// 等量转化为额外体力。
        ///
        /// 「被动倍率」无法直接改写（硬约束 #5），只能用「主机轮询同步值 + 施加补偿增量」：
        /// 每次轮询读取这 9 种状态、与上一轮快照逐项作差，把**正增量之和** × 倍率 作为额外体力下发。
        /// 负增量（治疗 / 净化 / 复活清状态）一律忽略；首次见到某角色只登记基线、不发放，
        /// 避免把「分配职业前就已存在的状态」也算作本次受到。
        /// </summary>
        private void PollSuffer(List<Character> characters)
        {
            float multiplier = ModConfig.M.SufferMultiplier.Value;

            foreach (var character in characters)
            {
                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                bool isM = _ledger.GetClass(character) == ClassType.M;

                bool had = _sufferSnapshot.TryGetValue(id, out float[] prev) && prev != null
                    && prev.Length == SufferTypes.Length;

                // 首次见到 / 刚换职业：只登记基线，不发放（数组常驻复用，避免每轮新分配 float[9]）
                if (!had)
                {
                    prev = new float[SufferTypes.Length];
                    for (int i = 0; i < SufferTypes.Length; i++)
                        prev[i] = HostApi.GetStatus(character, SufferTypes[i]);
                    _sufferSnapshot[id] = prev;
                    continue;
                }

                // 把「本轮值 − 上轮值」的正增量累加；同时就地刷新快照，供下一轮作差（数组复用，无分配）。
                float gain = 0f;
                for (int i = 0; i < SufferTypes.Length; i++)
                {
                    float now = HostApi.GetStatus(character, SufferTypes[i]);
                    float delta = now - prev[i];
                    if (delta > 0f) gain += delta;
                    prev[i] = now;
                }

                if (isM && multiplier > 0f && gain > 0f)
                {
                    float stamina = gain * multiplier;
                    HostApi.BoostExtraStamina(character, stamina);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 享受：{Nick(character)} 受到负面状态 额外体力+{stamina * 100f:0.#}");
                }
            }
        }

        /// <summary>
        /// 先驱·不屈：玩家「昏迷」（完全昏迷倒地但尚未死亡）时，立即清除其全部负面状态，每局每人仅一次。
        /// 由 TickTimers 每帧调用，保证昏迷瞬间即生效；用 _unbrokenUsed 记账防止重复触发。
        /// </summary>
        private void TryUnbroken(Character explorer, string id)
        {
            if (_unbrokenUsed.Contains(id))
            {
                // 本局已经用过：每局提示一次，便于在日志里核对
                // 「某个先驱用掉后，其他先驱仍能各自触发（不是全局共用）」。
                if (explorer.data != null && explorer.data.fullyPassedOut && !explorer.data.dead
                    && _unbrokenSkipLogged.Add(id))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 不屈：{Nick(explorer)} 本局已用过，不再触发（[{id}]）。");
                }
                return;
            }

            var data = explorer.data;
            if (data == null) return;
            if (data.dead) return;              // 已死亡不属于「昏迷」
            if (!data.fullyPassedOut) return;   // 未进入昏迷

            _unbrokenUsed.Add(id);
            HostApi.ClearAllNegativeStatuses(explorer);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 不屈：{Nick(explorer)} 昏迷 → 已清除全部负面状态（本局限一次，已用掉，[{id}]）；" +
                $"本局已用掉不屈的玩家数：{_unbrokenUsed.Count}。");
        }

        // ---------------------------------------------------------------
        // 领袖·士气：领袖死亡时惩罚所有存活玩家，领袖复活时恢复
        // ---------------------------------------------------------------

        private void ApplyMorale(Character leader)
        {
            float curse = ModConfig.Leader.MoraleCurseAmount.Value;
            float stamina = ModConfig.Leader.MoraleExtraStamina.Value;
            float petrify = ModConfig.Leader.MoralePetrifyAmount.Value;

            _moraleTargets.Clear();
            _moraleCurseBefore.Clear();
            _moralePetrifyBefore.Clear();
            foreach (var target in HostApi.GetActiveCharacters())
            {
                if (target == leader) continue;   // 领袖本人不参与
                if (IsDead(target)) continue;     // 只作用于存活玩家

                var id = _ledger.IdOf(target);
                if (string.IsNullOrEmpty(id)) continue;

                // 先记录「受罚前」的诅咒 / 石化快照，供复活恢复时按实际差额回退，
                // 避免盲目相减把玩家自身原有的数值一并清除。
                if (curse > 0f)
                    _moraleCurseBefore[id] = HostApi.GetStatus(target, CharacterAfflictions.STATUSTYPE.Curse);
                if (petrify > 0f)
                    _moralePetrifyBefore[id] = HostApi.GetPetrify(target);

                if (curse > 0f) HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Curse, curse);
                if (stamina != 0f) HostApi.BoostExtraStamina(target, stamina);
                if (petrify > 0f) HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Petrify, petrify);
                // 加速用的是能量饮料，其效果结束时会在目标客户端附加「困倦」；
                // 这里顺势在加速结束后清除，避免非先驱职业（尤其后勤）留下无法清除的困倦（实测问题 5）。
                GrantSprint(target, "士气", clearDrowsyAfter: true);

                _moraleTargets.Add(id);
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 士气：领袖 {Nick(leader)} 死亡，{_moraleTargets.Count} 名存活玩家受罚。");
        }

        private void RevertMorale(Character leader)
        {
            if (_moraleTargets.Count == 0) return;

            float curse = ModConfig.Leader.MoraleCurseAmount.Value;
            float petrify = ModConfig.Leader.MoralePetrifyAmount.Value;

            int restored = 0;
            float removedCurse = 0f;
            int removedPetrify = 0;
            foreach (var target in HostApi.GetActiveCharacters())
            {
                var id = _ledger.IdOf(target);
                if (string.IsNullOrEmpty(id) || !_moraleTargets.Contains(id)) continue;

                // 诅咒：只按「受罚前快照 − 当前值」回退，并把回退量夹在 [-惩罚量, 0] 之间。
                //   · 差额 ≤ 0 说明当前值不比受罚前多（惩罚已消失或从未生效）→ 什么都不做，
                //     因此绝不会把玩家自己原有的诅咒一并清掉；
                //   · 差额被夹到不超过 -惩罚量，保证不会多扣（主机读到远端状态滞后时也不会多扣）。
                float appliedCurseDelta = 0f;
                if (curse > 0f && _moraleCurseBefore.TryGetValue(id, out float beforeCurse))
                {
                    float currentCurse = HostApi.GetStatus(target, CharacterAfflictions.STATUSTYPE.Curse);
                    appliedCurseDelta = Mathf.Clamp(beforeCurse - currentCurse, -curse, 0f);
                    if (appliedCurseDelta <= -StatusIncrement)
                        HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Curse, appliedCurseDelta);
                    else
                        appliedCurseDelta = 0f;
                }

                // 石化：同构处理。石化是 0~100 的整数、走绝对赋值，故按整数差额回退。
                int appliedPetrifyDelta = 0;
                if (petrify > 0f && _moralePetrifyBefore.TryGetValue(id, out int beforePetrify))
                {
                    int currentPetrify = HostApi.GetPetrify(target);
                    int petrifyBonus = Mathf.RoundToInt(petrify * 100f);
                    appliedPetrifyDelta = Mathf.Clamp(beforePetrify - currentPetrify, -petrifyBonus, 0);
                    if (appliedPetrifyDelta < 0)
                        HostApi.SetPetrify(target, currentPetrify + appliedPetrifyDelta);
                    else
                        appliedPetrifyDelta = 0;
                }

                restored++;
                removedCurse += appliedCurseDelta;
                removedPetrify += appliedPetrifyDelta;

                // 若该玩家在受罚期间死亡、且尚未复活，其死亡记录里也含这份惩罚。
                // 这里把「实际回退量」从记录值里扣掉，否则复活继承会把惩罚原样恢复回来，
                // 造成「-20 诅咒 -30 石化随即又被加回」的现象（实测问题 3）。
                if (appliedCurseDelta < 0f && _deathCurse.TryGetValue(id, out float recordedCurse))
                    _deathCurse[id] = Mathf.Max(0f, recordedCurse + appliedCurseDelta);
                if (appliedPetrifyDelta < 0 && _deathPetrify.TryGetValue(id, out int recordedPetrify))
                    _deathPetrify[id] = Mathf.Max(0, recordedPetrify + appliedPetrifyDelta);
            }
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 士气：领袖 {Nick(leader)} 复活，{restored} 名玩家恢复" +
                $"（实际回退 诅咒 {removedCurse * 100f:0.#}、石化 {removedPetrify}）。");
            _moraleTargets.Clear();
            _moraleCurseBefore.Clear();
            _moralePetrifyBefore.Clear();
        }

        // ---------------------------------------------------------------
        // 领袖 / 后勤·复活继承：死亡时记录诅咒与石化，复活后恢复为该值
        //   原版 ReviveCharacter 的顺序是：
        //     ClearAllStatus()          —— 默认 excludeCurse/excludePetrify = true，不清理诅咒与石化；
        //     AdjustStatus(Petrify, -0.75) —— 石化 -75 点（接近清空）；
        //     ApplyPostReviveStatus()   —— 诅咒 += Ascents.revivalCurse（+5 点）；
        //   因此死亡时的值会被改写（尤其石化几乎归零），本效果把两者重新对齐到死亡瞬间的值。
        // ---------------------------------------------------------------

        /// <summary>
        /// 需要「复活继承」的职业：领袖（继承死亡时的诅咒与石化，石化不超过上限）、
        /// 后勤（继承诅咒，石化固定为该值）、挑战者【极限】（诅咒与石化都固定为配置值）。
        /// </summary>
        private static bool InheritsStatusOnRevive(ClassType type)
        {
            return type == ClassType.Leader || type == ClassType.Logistician
                || type == ClassType.Challenger;
        }

        /// <summary>
        /// 记录领袖 / 后勤 / 挑战者的诅咒与石化，供复活时恢复。
        /// 两个调用来源：
        ///   · 轮询的「真死亡」事件（<paramref name="reason"/> = 死亡记录）；
        ///   · <see cref="Patches.ReviveCharacterPatch"/> 的 Prefix（reason = 复活前记录）——
        ///     它在原版改状态之前执行，因此记录的是「复活前一刻」的真实值，覆盖雕像复活 / 昏迷被复活等全部来源。
        /// </summary>
        private void CaptureOnDeath(Character character, string reason = "死亡记录")
        {
            if (!InheritsStatusOnRevive(_ledger.GetClass(character))) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            float curse = HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Curse);
            int petrify = HostApi.GetPetrify(character);
            _deathCurse[id] = curse;
            _deathPetrify[id] = petrify;

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] {reason}：{Nick(character)} 诅咒 {curse * 100f:0.#}、石化 {petrify}（复活后继承）。");
        }

        /// <summary>
        /// 复活时的状态恢复：
        /// 诅咒按「目标值 − 当前值」补齐（领袖 / 后勤取死亡记录值，挑战者取固定的「极限-复活诅咒」；
        /// 差值为 0.025 的整数倍时引擎可完整应用）；
        /// 石化则登记一个目标值，交由 <see cref="TickPetrifyRestore"/> 在原版减石化之后校正到位。
        /// </summary>
        private void RestoreOnRevive(Character character)
        {
            var type = _ledger.GetClass(character);
            if (!InheritsStatusOnRevive(type)) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;
            if (!_deathCurse.TryGetValue(id, out float targetCurse)) return;

            _deathPetrify.TryGetValue(id, out int deathPetrify);
            _deathCurse.Remove(id);
            _deathPetrify.Remove(id);

            // 诅咒：领袖 / 后勤恢复为死亡瞬间的值；挑战者【极限】恢复为固定值「极限-复活诅咒」。
            // 诅咒走状态数组，按「目标值 − 当前值」下发差值即可（差值为 0.025 的整数倍时引擎可完整应用）。
            float curseTarget = type == ClassType.Challenger
                ? ModConfig.Challenger.ReviveCurseFixed.Value
                : targetCurse;
            float curseDelta = curseTarget - HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Curse);
            if (!Mathf.Approximately(curseDelta, 0f))
                HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Curse, curseDelta);

            // 石化：领袖 = 继承死亡瞬间的值，但不超过「石化上限」；后勤 / 挑战者 = 每次固定为该值。
            // 原版复活会 -75 点，且远端角色的这次减少要等它同步回主机才可见，
            // 因此这里只登记「目标值」，由 TickPetrifyRestore 反复校正（绝对赋值、幂等）。
            int petrifyTarget;
            if (type == ClassType.Leader)
            {
                petrifyTarget = Mathf.Min(deathPetrify, Mathf.Clamp(ModConfig.Leader.RevivePetrifyCap.Value, 0, 100));
            }
            else if (type == ClassType.Challenger)
            {
                petrifyTarget = Mathf.Clamp(
                    Mathf.RoundToInt(ModConfig.Challenger.RevivePetrifyFixed.Value * 100f), 0, 100);
            }
            else
            {
                petrifyTarget = Mathf.Clamp(ModConfig.Logistician.RevivePetrifyFixed.Value, 0, 100);
            }

            _petrifyRestores[id] = new PetrifyRestore
            {
                Target = petrifyTarget,
                NextCheck = Time.time + PetrifyRecheckDelay,
                Deadline = Time.time + PetrifyRestoreTimeout,
            };

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 复活继承：{Nick(character)} 诅咒恢复至 {curseTarget * 100f:0.#}（{curseDelta * 100f:+0.#;-0.#;0}）、" +
                $"石化目标 {petrifyTarget}（死亡时 {deathPetrify}）。");
        }

        // ---------------------------------------------------------------
        // 复活继承 · 确定性通道：原版 Character.ReviveCharacter(bool) 的补丁
        //   （见 Patches.ReviveCharacterPatch，由 Plugin.cs 注册）
        //
        // 主机的 0.5s 轮询只能识别「真死亡 → 复活」这一种跳变；而原版 ReviveCharacter 会对
        // **任何**复活来源执行同一套改写（ClearAllStatus() 不改诅咒/石化 + 石化 -75 点）：
        //   · 童军雕像（ScoutEffigy.FinishConstruction）
        //   · 古老雕像（RespawnChest.RespawnAllPlayersHere）
        //   · 骸骨之书（Skelleton.Interact_CastFinished）
        //   · 篝火 / 虚空复活、中途加入者的首次复活
        //   · 以及「玩家仅处于昏迷倒地（fullyPassedOut）时被雕像复活」——这种既没有真死亡事件、
        //     也照样把石化 -75 清掉（实测问题：挑战者被童军雕像复活后石化没回到 70）。
        // 因此这里补上确定性的那一条：Prefix 记录「复活前一刻」的值，Postfix 立即恢复。
        // 轮询那条通道保留为兜底（补丁万一挂不上时仍能覆盖常规死亡复活）。
        // ---------------------------------------------------------------

        /// <summary>
        /// 由 <see cref="Patches.ReviveCharacterPatch"/> 的 Prefix 调用（仅主机）：
        /// 在原版清状态 / 石化 -75 之前记录该角色当前的诅咒与石化，供 Postfix 恢复。
        /// </summary>
        public void NotifyBeforeRevive(Character character)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!_inRun) return;
            if (character == null) return;
            CaptureOnDeath(character, "复活前记录");
        }

        /// <summary>
        /// 由 <see cref="Patches.ReviveCharacterPatch"/> 的 Postfix 调用（仅主机）：
        /// 复活（原版已完成 清状态 + 石化 -75）之后立即把领袖 / 后勤 / 挑战者的诅咒与石化恢复到目标值。
        /// </summary>
        public void NotifyAfterRevive(Character character)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!_inRun) return;
            if (character == null) return;
            RestoreOnRevive(character);
        }

        /// <summary>
        /// 把复活后登记的石化目标值校正到位。
        /// 之所以要「反复校正」而不是只发一次：石化存于 CharacterData.petrifyAmount，
        /// 原版复活流程的 -75 发生在该角色自己的客户端上，主机要等它同步过来才能看到结果；
        /// 若在同步到达之前就判定「已完成」，就会被随后的同步值覆盖回低值。
        /// 校正使用绝对赋值（RPC_SyncPetrify），因此重复执行是幂等的，不会叠加。
        /// </summary>
        private void TickPetrifyRestore(Character character, string id)
        {
            if (!_petrifyRestores.TryGetValue(id, out var restore)) return;

            if (character == null || IsTrueDead(character))
            {
                // 又（真正）死了：本次校正作废（下次复活会重新登记）。
                // 注意用「真死亡」判定：昏迷倒地仍属于活着，不能因为昏迷就放弃校正（实测问题 5）。
                _petrifyRestores.Remove(id);
                return;
            }

            int current = HostApi.GetPetrify(character);
            if (current >= restore.Target)
            {
                if (restore.Applied > 0)
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] {restore.Reason}：{Nick(character)} 石化已校正为 {current}（目标 {restore.Target}）。");
                }
                _petrifyRestores.Remove(id);
                return;
            }

            if (Time.time >= restore.Deadline)
            {
                Plugin.Log.LogWarning(
                    $"[RollOfTheScout] {restore.Reason}：{Nick(character)} 石化校正超时（当前 {current} / 目标 {restore.Target}）。");
                _petrifyRestores.Remove(id);
                return;
            }

            // 等待一个同步周期，避免读到「尚未同步到主机」的旧值而误判
            if (Time.time < restore.NextCheck) return;

            HostApi.SetPetrify(character, restore.Target);
            restore.Applied++;
            restore.NextCheck = Time.time + PetrifyRecheckDelay;

            if (restore.Applied == 1)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {restore.Reason}：{Nick(character)} 石化 {current} → 设为 {restore.Target}。");
            }
        }

        // ---------------------------------------------------------------
        // 加速（能量饮料）：借原版「喂食」通道，在目标玩家自己的客户端触发
        // ---------------------------------------------------------------

        /// <summary>
        /// 让目标角色获得一次能量饮料的加速效果。
        /// 加速（Affliction_FasterBoi）改的是客户端本地物理量，无法由主机直接下发，
        /// 因此这里生成一瓶能量饮料并对目标角色调用 FeedItem，由目标客户端自行「饮用」。
        /// <paramref name="clearDrowsyAfter"/> 为 true 时，还会在加速结束后清除困倦（见 UpdateSprintWatches）；
        /// <paramref name="clearAmount"/> &gt;= 0 时使用指定清除量，否则回退到先驱「高代谢-加速后清除困倦」。
        /// </summary>
        private void GrantSprint(Character character, string reason, bool clearDrowsyAfter = false, float clearAmount = -1f)
        {
            float amount = clearAmount >= 0f ? clearAmount : ModConfig.Explorer.DrowsyClearAmount.Value;
            // 摊帧执行：入队后由 DrainTransientFeeds 逐帧生成（避免群体效果同帧爆发实例化）
            EnqueueTransientFeed(character, ItemNames.EnergyDrink, reason, TransientKind.Sprint,
                clearDrowsyAfter ? amount : -1f);
        }

        /// <summary>
        /// 让目标角色获得一次「无限体力」效果。实现方式与 <see cref="GrantSprint"/> 完全相同：
        /// 生成一件「无限体力物品」（大棒棒糖 Lollipop）→ FeedItem → 由目标客户端自行「食用」。
        ///
        /// 注意：大棒棒糖的无限体力结束时会通过 <c>Affliction_AdjustDrowsyOverTime</c>
        /// 「持续」叠加困倦（实测约 32.5 点），且该困倦在客户端无法自然消退（见 <see cref="StaminaDrowsyWatch"/>）。
        /// 故这里登记一次监听：等该「持续叠加困倦」affliction 结束后由主机代为清除残留困倦。
        /// </summary>
        private void GrantInfiniteStamina(Character character, string reason)
        {
            // 摊帧执行：入队后由 DrainTransientFeeds 逐帧生成（避免群体效果同帧爆发实例化）
            EnqueueTransientFeed(character, ItemNames.Lollipop, reason, TransientKind.InfiniteStamina);
        }

        /// <summary>登记一次「临时效果周期」，用于在效果结束后清除其残留的困倦。</summary>
        private void RegisterEffectWatch(Character character, string reason,
            Affliction.AfflictionType type, string label, float clearAmount)
        {
            var id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;
            // 同一玩家可能同时带着「加速」与「无限体力」两种效果，key 必须带上效果类型以免互相覆盖。
            string key = id + "|" + (int)type;
            _sprintWatches[key] = new SprintWatch
            {
                Character = character,
                Reason = reason,
                AfflictionType = type,
                EffectLabel = label,
                ClearDrowsyAmount = clearAmount,
            };
        }

        /// <summary>
        /// 临时效果结束后的困倦清除。
        ///
        /// 目标客户端在加速（FasterBoi）/ 无限体力（InfiniteStamina）结束的那一刻执行
        /// OnRemoved → 给自己附加「困倦」，随后把 affliction 列表同步给主机（`SyncAfflictionsRPC`）；
        /// 主机据此观察到该 affliction 由「有」变「无」后，立即下发 -困倦 抵消（困倦走状态通道，
        /// 下标 6，主机可对任意远端玩家下发）。效果的实际时长由物品自身决定，无法改写，
        /// 这里把实测到的持续时长一并打印到日志。
        /// </summary>
        private void UpdateSprintWatches()
        {
            if (_sprintWatches.Count == 0) return;

            float dt = Time.deltaTime;
            List<string> finished = null;

            foreach (var pair in _sprintWatches)
            {
                var watch = pair.Value;
                watch.Elapsed += dt;

                var character = watch.Character;
                if (character == null || character.photonView == null)
                {
                    if (finished == null) finished = new List<string>();
                    finished.Add(pair.Key);
                    continue;
                }

                bool hasEffect = HasAffliction(character, watch.AfflictionType);
                float clear = watch.ClearDrowsyAmount;
                string label = string.IsNullOrEmpty(watch.EffectLabel) ? "加速" : watch.EffectLabel;

                if (!watch.Seen)
                {
                    if (hasEffect)
                    {
                        watch.Seen = true;
                        watch.Elapsed = 0f;
                    }
                    else if (watch.Elapsed >= SprintApplyTimeout)
                    {
                        Plugin.Log.LogWarning($"[RollOfTheScout] {watch.Reason}：未观察到 {Nick(character)} 的{label}生效，跳过困倦清除。");
                        if (finished == null) finished = new List<string>();
                        finished.Add(pair.Key);
                    }
                    continue;
                }

                if (!hasEffect)
                {
                    if (clear > 0f)
                    {
                        HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Drowsy, -clear);
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] {watch.Reason}：{Nick(character)} {label}结束（实测持续约 {watch.Elapsed:0.#} 秒），" +
                        $"已清除困倦 {clear * 100f:0.#} 点。");
                    if (finished == null) finished = new List<string>();
                    finished.Add(pair.Key);
                }
                else if (watch.Elapsed >= SprintEndTimeout)
                {
                    Plugin.Log.LogWarning($"[RollOfTheScout] {watch.Reason}：{Nick(character)} 的{label}监听超时（{SprintEndTimeout:0} 秒），放弃困倦清除。");
                    if (finished == null) finished = new List<string>();
                    finished.Add(pair.Key);
                }
            }

            if (finished != null)
            {
                foreach (var key in finished) _sprintWatches.Remove(key);
            }
        }

        /// <summary>目标角色身上是否正带着指定类型的 affliction。</summary>
        private static bool HasAffliction(Character character, Affliction.AfflictionType type)
        {
            if (character == null || character.refs == null) return false;
            var afflictions = character.refs.afflictions;
            if (afflictions == null) return false;
            return afflictions.HasAfflictionType(type, out _);
        }

        /// <summary>
        /// 大棒棒糖「无限体力 → 持续叠加困倦」的残留困倦清除。
        ///
        /// 目标客户端在无限体力（<c>InfiniteStamina</c>）结束的那一刻执行 OnRemoved → 追加一个
        /// <c>Affliction_AdjustDrowsyOverTime</c>（类型 <c>DrowsyOverTime</c>），它每帧叠加困倦并刷新
        /// <c>LastAddedStatus(Drowsy)</c>，使原版自然消退条件永远不成立（表现为「困倦保持不变」）。
        /// 该 affliction 与无限体力不同时存在（先结束再去叠加），故不能像加速那样「结束后立刻清一次」，
        /// 必须等 <c>DrowsyOverTime</c> 也结束之后再清除残留。四个阶段依次推进：
        /// 等无限体力生效 → 等无限体力结束 → 等 DrowsyOverTime 出现 → 等 DrowsyOverTime 结束 → 清除。
        /// </summary>
        private void UpdateStaminaDrowsyWatches()
        {
            if (_staminaDrowsyWatches.Count == 0) return;

            float dt = Time.deltaTime;
            float clear = ModConfig.StandardBearer.StaminaDrowsyClearAmount.Value;
            List<string> finished = null;

            foreach (var pair in _staminaDrowsyWatches)
            {
                var watch = pair.Value;
                watch.Elapsed += dt;
                watch.PhaseElapsed += dt;

                var character = watch.Character;
                if (character == null || character.photonView == null)
                {
                    if (finished == null) finished = new List<string>();
                    finished.Add(pair.Key);
                    continue;
                }

                string reason = string.IsNullOrEmpty(watch.Reason) ? "执旗" : watch.Reason;
                bool drop = false;

                switch (watch.Phase)
                {
                    case StaminaDrowsyPhase.WaitStamina:
                    {
                        if (HasAffliction(character, Affliction.AfflictionType.InfiniteStamina))
                        {
                            watch.Phase = StaminaDrowsyPhase.WaitStaminaEnd;
                            // 生效后重新计时，使「实测持续约 X 秒」等于无限体力的真实时长
                            watch.Elapsed = 0f;
                            watch.PhaseElapsed = 0f;
                        }
                        else if (watch.Elapsed >= StaminaApplyTimeout)
                        {
                            Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：未观察到 {Nick(character)} 的无限体力生效，跳过困倦清除。");
                            drop = true;
                        }
                        break;
                    }
                    case StaminaDrowsyPhase.WaitStaminaEnd:
                    {
                        if (!HasAffliction(character, Affliction.AfflictionType.InfiniteStamina))
                        {
                            watch.Phase = StaminaDrowsyPhase.WaitDrowsyAppear;
                            watch.PhaseElapsed = 0f;
                            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 无限体力结束（实测持续约 {watch.Elapsed:0.#} 秒），开始等待其后续困倦叠加结束。");
                        }
                        else if (watch.Elapsed >= StaminaEndTimeout)
                        {
                            Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：{Nick(character)} 的无限体力监听超时（{StaminaEndTimeout:0} 秒），放弃困倦清除。");
                            drop = true;
                        }
                        break;
                    }
                    case StaminaDrowsyPhase.WaitDrowsyAppear:
                    {
                        if (HasAffliction(character, Affliction.AfflictionType.DrowsyOverTime))
                        {
                            watch.Phase = StaminaDrowsyPhase.WaitDrowsyEnd;
                            watch.PhaseElapsed = 0f;
                        }
                        else if (watch.PhaseElapsed >= DrowsyAppearTimeout)
                        {
                            // 该物品本就不加困倦（如烤过变体），无需清除。
                            drop = true;
                        }
                        break;
                    }
                    case StaminaDrowsyPhase.WaitDrowsyEnd:
                    {
                        if (!HasAffliction(character, Affliction.AfflictionType.DrowsyOverTime))
                        {
                            if (clear > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Drowsy, -clear);
                            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 无限体力·持续困倦结束，已清除困倦 {clear * 100f:0.#} 点。");
                            drop = true;
                        }
                        else if (watch.PhaseElapsed >= DrowsyEndTimeout)
                        {
                            // 兜底：该 affliction 长时间不结束，仍清除一次以免玩家永久背负困倦。
                            if (clear > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Drowsy, -clear);
                            Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：{Nick(character)} 的持续困倦监听超时（{DrowsyEndTimeout:0} 秒），已兜底清除困倦 {clear * 100f:0.#} 点。");
                            drop = true;
                        }
                        break;
                    }
                }

                if (drop)
                {
                    if (finished == null) finished = new List<string>();
                    finished.Add(pair.Key);
                }
            }

            if (finished != null)
            {
                foreach (var key in finished) _staminaDrowsyWatches.Remove(key);
            }
        }

        /// <summary>延迟销毁临时物品（等目标客户端的喂食 RPC 处理完再删）。</summary>
        private static IEnumerator DestroyAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) PhotonNetwork.Destroy(go);
        }

        // ---------------------------------------------------------------
        // 定时效果：先驱高代谢 / 领袖号召 / 紧急空投 / 医生毒理学 / 医生护理学 / 潘多拉
        // ---------------------------------------------------------------

        private void TickTimers(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 先驱·不屈：昏迷时立即清除全部负面状态（每局每人仅一次）----
            // 该效果依赖「昏迷瞬间」判定，故必须每帧采样（不能改成按间隔检测）；
            // 但没有先驱在场时可直接跳过整轮遍历。
            if (_ledger.HasClassInRoom(ClassType.Explorer))
            {
                foreach (var explorer in characters)
                {
                    if (_ledger.GetClass(explorer) != ClassType.Explorer) continue;
                    string explorerId = _ledger.IdOf(explorer);
                    if (string.IsNullOrEmpty(explorerId)) continue;
                    TryUnbroken(explorer, explorerId);
                }
            }

            // ---- 先驱·高代谢：定时注入饥饿（覆盖游戏默认的自然增长）----
            _explorerHungerTimer += dt;
            float hungerInterval = Mathf.Max(1f, ModConfig.Explorer.HungerIntervalSeconds.Value);
            if (_explorerHungerTimer >= hungerInterval)
            {
                _explorerHungerTimer = 0f;
                float hunger = ModConfig.Explorer.HungerPerTick.Value;
                // 无先驱在场 → 跳过对全体玩家的遍历（计时器照常重置，效果时序不变）
                if (hunger > 0f && _ledger.HasClassInRoom(ClassType.Explorer))
                {
                    foreach (var explorer in characters)
                    {
                        if (_ledger.GetClass(explorer) != ClassType.Explorer) continue;
                        if (IsDead(explorer)) continue;
                        HostApi.ApplyStatus(explorer, CharacterAfflictions.STATUSTYPE.Hunger, hunger);
                    }
                }
            }

            // ---- 先驱·高代谢：定时加速（让先驱「饮用」能量饮料）----
            _explorerSprintTimer += dt;
            float sprintInterval = Mathf.Max(1f, ModConfig.Explorer.SprintIntervalSeconds.Value);
            if (_explorerSprintTimer >= sprintInterval)
            {
                _explorerSprintTimer = 0f;
                // 无先驱在场 → 跳过对全体玩家的遍历（计时器照常重置，效果时序不变）
                if (_ledger.HasClassInRoom(ClassType.Explorer))
                {
                    foreach (var explorer in characters)
                    {
                        if (_ledger.GetClass(explorer) != ClassType.Explorer) continue;
                        if (IsDead(explorer)) continue;
                        GrantSprint(explorer, "高代谢", clearDrowsyAfter: true);
                    }
                }
            }

            // ---- 领袖号召 ----
            _inspireTimer += dt;
            float inspireInterval = Mathf.Max(1f, ModConfig.Leader.InspireIntervalSeconds.Value);
            if (_inspireTimer >= inspireInterval)
            {
                _inspireTimer = 0f;
                // 无领袖在场 → 跳过对全体玩家的遍历（号召会对半径内每人生成一次加速物品，跳过收益更高）
                if (_ledger.HasClassInRoom(ClassType.Leader))
                {
                    foreach (var leader in characters)
                    {
                        if (_ledger.GetClass(leader) != ClassType.Leader) continue;
                        DoInspire(leader);
                    }
                }
            }

            // ---- 领袖号召·清醒：单独按 1 分钟计时，清除半径内所有人的困倦 ----
            _inspireDrowsyTimer += dt;
            float drowsyInterval = Mathf.Max(1f, ModConfig.Leader.InspireDrowsyIntervalSeconds.Value);
            if (_inspireDrowsyTimer >= drowsyInterval)
            {
                _inspireDrowsyTimer = 0f;
                foreach (var leader in characters)
                {
                    if (_ledger.GetClass(leader) != ClassType.Leader) continue;
                    DoInspireDrowsy(leader);
                }
            }

            // ---- 紧急空投 ----
            _airdropTimer += dt;
            float airdropInterval = Mathf.Max(1f, ModConfig.Logistician.AirdropIntervalSeconds.Value);
            if (_airdropTimer >= airdropInterval)
            {
                _airdropTimer = 0f;
                foreach (var logistician in characters)
                {
                    if (_ledger.GetClass(logistician) != ClassType.Logistician) continue;
                    DoAirdrop(logistician);
                }
            }

            // ---- 医生·毒理学（原「毒性熟悉」）----
            _poisonTimer += dt;
            float poisonInterval = Mathf.Max(0.5f, ModConfig.Doctor.PoisonReduceInterval.Value);
            if (_poisonTimer >= poisonInterval)
            {
                _poisonTimer = 0f;
                // 无医生在场 → 跳过对全体玩家的遍历
                if (_ledger.HasClassInRoom(ClassType.Doctor))
                {
                    foreach (var doctor in characters)
                    {
                        if (_ledger.GetClass(doctor) != ClassType.Doctor) continue;
                        DoPoisonReduce(doctor);
                    }
                }
            }

            // ---- 医生·护理学：定时治疗半径内所有玩家的损伤 ----
            _nursingTimer += dt;
            float nursingInterval = Mathf.Max(0.5f, ModConfig.Doctor.NursingInterval.Value);
            if (_nursingTimer >= nursingInterval)
            {
                _nursingTimer = 0f;
                // 无医生在场 → 跳过对全体玩家的遍历
                if (_ledger.HasClassInRoom(ClassType.Doctor))
                {
                    foreach (var doctor in characters)
                    {
                        if (_ledger.GetClass(doctor) != ClassType.Doctor) continue;
                        DoNursing(doctor);
                    }
                }
            }

            // ---- 潘多拉：定时对半径内的玩家施加潘多拉魔盒（是否含自己由配置决定）----
            _pandoraTimer += dt;
            float pandoraInterval = Mathf.Max(1f, ModConfig.Pandora.BoxIntervalSeconds.Value);
            if (_pandoraTimer >= pandoraInterval)
            {
                _pandoraTimer = 0f;
                // 无潘多拉之子在场 → 跳过对全体玩家的遍历（该效果每次命中都会生成世界物品，跳过收益更高）
                if (_ledger.HasClassInRoom(ClassType.Pandora))
                {
                    foreach (var pandora in characters)
                    {
                        if (_ledger.GetClass(pandora) != ClassType.Pandora) continue;
                        if (IsDead(pandora)) continue;
                        DoPandora(pandora);
                    }
                }
            }

            // ---- 潘多拉：定时为自身叠加诅咒 ----
            _pandoraCurseTimer += dt;
            float curseInterval = Mathf.Max(1f, ModConfig.Pandora.CurseIntervalSeconds.Value);
            if (_pandoraCurseTimer >= curseInterval)
            {
                _pandoraCurseTimer = 0f;
                float curse = ModConfig.Pandora.CurseAmount.Value;
                if (curse > 0f)
                {
                    int n = 0;
                    foreach (var pandora in characters)
                    {
                        if (_ledger.GetClass(pandora) != ClassType.Pandora) continue;
                        if (IsDead(pandora)) continue;
                        HostApi.ApplyStatus(pandora, CharacterAfflictions.STATUSTYPE.Curse, curse);
                        n++;
                    }
                    Plugin.Log.LogInfo($"[RollOfTheScout] 潘多拉：为 {n} 名潘多拉之子自身叠加诅咒 {curse * 100f:0.#} 点。");
                }
            }

            // ---- 死灵法师·亡灵：场上每有骷髅玩家，定时为死灵法师增加额外体力 ----
            _necSkeletonStaminaTimer += dt;
            float necStaminaInterval = ModConfig.Necromancer.SkeletonStaminaIntervalSeconds.Value;
            if (necStaminaInterval <= 0f)
            {
                _necSkeletonStaminaTimer = 0f;
            }
            else if (_necSkeletonStaminaTimer >= necStaminaInterval)
            {
                _necSkeletonStaminaTimer = 0f;
                int skeletonCount = CountSkeletonPlayers(characters);
                float perStamina = ModConfig.Necromancer.SkeletonStaminaPer.Value;
                if (skeletonCount > 0 && perStamina != 0f)
                {
                    float stamina = perStamina * skeletonCount;
                    foreach (var necro in characters)
                    {
                        if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;
                        if (IsDead(necro)) continue;
                        HostApi.BoostExtraStamina(necro, stamina);
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 亡灵：场上 {skeletonCount} 名骷髅玩家，为死灵法师各 +{stamina * 100f:0.#} 额外体力。");
                }
            }

            // ---- 死灵法师·亡灵：场上每有骷髅玩家，定时为死灵法师减少诅咒 ----
            _necSkeletonCurseTimer += dt;
            float necCurseInterval = ModConfig.Necromancer.SkeletonCurseIntervalSeconds.Value;
            if (necCurseInterval <= 0f)
            {
                _necSkeletonCurseTimer = 0f;
            }
            else if (_necSkeletonCurseTimer >= necCurseInterval)
            {
                _necSkeletonCurseTimer = 0f;
                int skeletonCount = CountSkeletonPlayers(characters);
                float perCurse = ModConfig.Necromancer.SkeletonCursePer.Value;
                if (skeletonCount > 0 && perCurse != 0f)
                {
                    float curse = perCurse * skeletonCount;
                    foreach (var necro in characters)
                    {
                        if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;
                        if (IsDead(necro)) continue;
                        HostApi.ApplyStatus(necro, CharacterAfflictions.STATUSTYPE.Curse, curse);
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 亡灵：场上 {skeletonCount} 名骷髅玩家，为死灵法师各 {curse * 100f:+0.#;-0.#;0} 诅咒。");
                }
            }

            // ---- 死灵法师·亡灵·光环：定时对半径内玩家施加效果（人类 +损伤、骷髅 +额外体力，不含自身）----
            _necAuraTimer += dt;
            float necAuraInterval = ModConfig.Necromancer.AuraIntervalSeconds.Value;
            if (necAuraInterval <= 0f)
            {
                _necAuraTimer = 0f;
            }
            else if (_necAuraTimer >= necAuraInterval)
            {
                _necAuraTimer = 0f;
                // 无死灵法师在场 → 跳过对全体玩家的遍历（光环每次会对半径内玩家逐一下发状态）
                if (_ledger.HasClassInRoom(ClassType.Necromancer))
                {
                    foreach (var necro in characters)
                    {
                        if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;
                        if (IsDead(necro)) continue;
                        DoNecromancerAura(necro);
                    }
                }
            }

            // ---- 死灵法师·亡灵·光环：独立计时，为半径内骷髅玩家施加无敌（奶白金），不含自身 ----
            _necAuraInvincibleTimer += dt;
            float necAuraInvincibleInterval = ModConfig.Necromancer.AuraSkeletonInvincibleIntervalSeconds.Value;
            if (necAuraInvincibleInterval <= 0f)
            {
                _necAuraInvincibleTimer = 0f;
            }
            else if (_necAuraInvincibleTimer >= necAuraInvincibleInterval)
            {
                _necAuraInvincibleTimer = 0f;
                // 无死灵法师在场 → 跳过对全体玩家的遍历（无敌效果每人都会生成一次世界物品，跳过收益更高）
                if (_ledger.HasClassInRoom(ClassType.Necromancer))
                {
                    foreach (var necro in characters)
                    {
                        if (_ledger.GetClass(necro) != ClassType.Necromancer) continue;
                        if (IsDead(necro)) continue;
                        GrantNecromancerAuraInvincibility(necro);
                    }
                }
            }

            // ---- 野人·自然韧性：定时为野人清除中毒与孢子 ----
            _savageCleanseTimer += dt;
            float savageCleanseInterval = Mathf.Max(1f, ModConfig.Savage.CleanseIntervalSeconds.Value);
            if (_savageCleanseTimer >= savageCleanseInterval)
            {
                _savageCleanseTimer = 0f;
                float poison = ModConfig.Savage.CleansePoison.Value;
                float spores = ModConfig.Savage.CleanseSpores.Value;
                if (poison > 0f || spores > 0f)
                {
                    foreach (var savage in characters)
                    {
                        if (_ledger.GetClass(savage) != ClassType.Savage) continue;
                        if (IsDead(savage)) continue;
                        // 保留旧实现的「> 0 才生效」口径：先算出差量再合并下发
                        //（同一时刻对同一玩家清中毒 + 清孢子，合并为单条 RPC 以减少网络开销）
                        float poisonDelta = poison > 0f ? -poison : 0f;
                        float sporesDelta = spores > 0f ? -spores : 0f;
                        HostApi.ApplyStatusPair(savage,
                            CharacterAfflictions.STATUSTYPE.Poison, poisonDelta,
                            CharacterAfflictions.STATUSTYPE.Spores, sporesDelta);
                    }
                }
            }

            // ---- 野人·不拿拿：定时为野人发放粉香蕉 ----
            _savageBananaTimer += dt;
            float bananaInterval = Mathf.Max(1f, ModConfig.Savage.BananaIntervalSeconds.Value);
            if (_savageBananaTimer >= bananaInterval)
            {
                _savageBananaTimer = 0f;
                int bananaCount = Mathf.Max(0, ModConfig.Savage.BananaCount.Value);
                string bananaItem = ModConfig.Savage.BananaItem.Value;
                // 无野人在场 → 跳过对全体玩家的遍历（每人都会发放一次物品）
                if (bananaCount > 0 && !string.IsNullOrEmpty(bananaItem)
                    && _ledger.HasClassInRoom(ClassType.Savage))
                {
                    foreach (var savage in characters)
                    {
                        if (_ledger.GetClass(savage) != ClassType.Savage) continue;
                        if (IsDead(savage)) continue;
                        GrantSavageBananas(savage, bananaItem, bananaCount);
                    }
                }
            }

            // ---- M·享受：每隔「享受-检测间隔」秒检测一次，若「没有任何特定负面状态」则扣额外体力 ----
            _mDrainTimer += dt;
            float mCleanInterval = ModConfig.M.CleanIntervalSeconds.Value;
            float mCleanDeduct = ModConfig.M.CleanDeduct.Value;
            if (mCleanInterval > 0f && mCleanDeduct > 0f && _mDrainTimer >= mCleanInterval)
            {
                _mDrainTimer = 0f;
                foreach (var m in characters)
                {
                    if (_ledger.GetClass(m) != ClassType.M) continue;
                    if (IsDead(m)) continue;
                    if (HasAnyStatus(m, SufferTypes)) continue;   // 只要还有任一特定负面状态就不扣

                    HostApi.BoostExtraStamina(m, -mCleanDeduct);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 享受：{Nick(m)} 检测时无任何特定负面状态，额外体力-{mCleanDeduct * 100f:0.#}");
                }
            }

            // ---- M·耐受：每隔「耐受-间隔」秒削减 6 种特定负面状态 ----
            _mToleranceTimer += dt;
            float mToleranceInterval = ModConfig.M.ToleranceIntervalSeconds.Value;
            float mToleranceReduce = ModConfig.M.ToleranceReduce.Value;
            if (ModConfig.M.ToleranceEnabled.Value && mToleranceInterval > 0f && mToleranceReduce > 0f
                && _mToleranceTimer >= mToleranceInterval)
            {
                _mToleranceTimer = 0f;
                foreach (var m in characters)
                {
                    if (_ledger.GetClass(m) != ClassType.M) continue;
                    if (IsDead(m)) continue;
                    foreach (var type in ToleranceTypes)
                    {
                        // 荆棘(9) 属原版状态数组通道的硬性排除项：客机角色下不发，只有主机本人走直连路径才有效
                        if (type == CharacterAfflictions.STATUSTYPE.Thorns)
                            HostApi.TrySubtractStatusLocalOnly(m, type, mToleranceReduce);
                        else
                            HostApi.ApplyStatus(m, type, -mToleranceReduce);
                    }
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 耐受：{Nick(m)} 特定负面状态-{mToleranceReduce * 100f:0.#}");
                }
            }

            // ---- 童军·【集体】（第 85 批重置为三档）----
            //   ① 独自（半径内没有任何其他存活玩家）：每「集体-独自间隔」+独自损伤 / +独自饥饿
            //   ② 同伴（有其他玩家，但其中没有童军或领袖）：每「集体-同伴损伤间隔」-同伴减损伤、每「集体-同伴饥饿间隔」-同伴减饥饿
            //   ③ 童军/领袖（有其他童军或领袖）：每「集体-童军损伤间隔」-童军减损伤、每「集体-童军饥饿间隔」-童军减饥饿
            //   档位由 ScoutCompanyTier 每轮实时判定；三个档位互斥（取最高档）。
            float scoutRadius = Mathf.Max(0f, ModConfig.Scout.GroupRadius.Value);
            FireScoutTierPair(characters, ref _scoutSoloTimer,
                ModConfig.Scout.SoloIntervalSeconds.Value, 0, scoutRadius,
                CharacterAfflictions.STATUSTYPE.Injury, ModConfig.Scout.SoloInjury.Value,
                CharacterAfflictions.STATUSTYPE.Hunger, ModConfig.Scout.SoloHunger.Value);
            FireScoutTierStatus(characters, ref _scoutPeerInjuryTimer,
                ModConfig.Scout.PeerInjuryIntervalSeconds.Value, 1, scoutRadius,
                CharacterAfflictions.STATUSTYPE.Injury, -ModConfig.Scout.PeerInjuryReduce.Value);
            FireScoutTierStatus(characters, ref _scoutPeerHungerTimer,
                ModConfig.Scout.PeerHungerIntervalSeconds.Value, 1, scoutRadius,
                CharacterAfflictions.STATUSTYPE.Hunger, -ModConfig.Scout.PeerHungerReduce.Value);
            FireScoutTierStatus(characters, ref _scoutAllyInjuryTimer,
                ModConfig.Scout.AllyInjuryIntervalSeconds.Value, 2, scoutRadius,
                CharacterAfflictions.STATUSTYPE.Injury, -ModConfig.Scout.AllyInjuryReduce.Value);
            FireScoutTierStatus(characters, ref _scoutAllyHungerTimer,
                ModConfig.Scout.AllyHungerIntervalSeconds.Value, 2, scoutRadius,
                CharacterAfflictions.STATUSTYPE.Hunger, -ModConfig.Scout.AllyHungerReduce.Value);

            // ---- 舞王·狂热舞者：定时强制舞王自己后空翻一次 ----
            _dancerDanceTimer += dt;
            float frenzyInterval = ModConfig.Dancer.FrenzyIntervalSeconds.Value;
            if (frenzyInterval > 0f && _dancerDanceTimer >= frenzyInterval)
            {
                _dancerDanceTimer = 0f;
                // 无舞王在场 → 跳过对全体玩家的遍历（狂热舞者每人都会广播一条表情 RPC，跳过收益更高）
                if (_ledger.HasClassInRoom(ClassType.Dancer))
                {
                    foreach (var dancer in characters)
                    {
                        if (_ledger.GetClass(dancer) != ClassType.Dancer) continue;
                        if (IsDead(dancer)) continue;
                        ForceBackflip(dancer, "狂热舞者");
                    }
                }
            }

            // ---- 待皓·喂喂喂，听得见吗：定时检测语音状态，正在讲话则 +额外体力 ----
            _daihaoVoiceTimer += dt;
            float voiceInterval = ModConfig.Daihao.VoiceIntervalSeconds.Value;
            if (voiceInterval > 0f && _daihaoVoiceTimer >= voiceInterval)
            {
                _daihaoVoiceTimer = 0f;
                float voiceStamina = ModConfig.Daihao.VoiceStamina.Value;
                if (voiceStamina > 0f)
                {
                    foreach (var c in characters)
                    {
                        if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                        if (IsDead(c)) continue;
                        if (!IsActuallySpeaking(c)) continue;
                        HostApi.BoostExtraStamina(c, voiceStamina);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 喂喂喂：{Nick(c)} 正在讲话，+{voiceStamina * 100f:0.#} 额外体力。");
                    }
                }
            }

            // ---- 待皓·脚滑：定时强制待皓玩家后空翻一次（翻完必定摔倒）----
            _daihaoSliplTimer += dt;
            float slipInterval = ModConfig.Daihao.SlipIntervalSeconds.Value;
            if (slipInterval > 0f && _daihaoSliplTimer >= slipInterval)
            {
                _daihaoSliplTimer = 0f;
                // 无待皓在场 → 跳过对全体玩家的遍历（脚滑每人都会广播一条表情 RPC）
                if (_ledger.HasClassInRoom(ClassType.Daihao))
                {
                    foreach (var c in characters)
                    {
                        if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                        if (IsDead(c)) continue;
                        ForceBackflip(c, "脚滑");
                    }
                }
            }

            // ---- 待皓·生产：定时获得 1 个「生产-物品」（默认好大蛋 NestEgg；优先空物品栏，满则脚下生成）----
            _daihaoEggTimer += dt;
            float eggInterval = ModConfig.Daihao.ProductionEggIntervalSeconds.Value;
            if (eggInterval > 0f && _daihaoEggTimer >= eggInterval)
            {
                _daihaoEggTimer = 0f;
                string eggItem = ModConfig.Daihao.ProductionItem.Value;
                // 无待皓在场 → 跳过对全体玩家的遍历（每人都会发放一次物品）
                if (!string.IsNullOrEmpty(eggItem) && _ledger.HasClassInRoom(ClassType.Daihao))
                {
                    foreach (var c in characters)
                    {
                        if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                        if (IsDead(c)) continue;
                        GrantDaihaoEgg(c, eggItem);
                    }
                }
            }

            // ---- 待皓·生产：3 个物品栏中只要有 1 个「生产-物品」，定时 +额外体力 ----
            _daihaoEggStaminaTimer += dt;
            float eggStaminaInterval = ModConfig.Daihao.ProductionStaminaIntervalSeconds.Value;
            if (eggStaminaInterval > 0f && _daihaoEggStaminaTimer >= eggStaminaInterval)
            {
                _daihaoEggStaminaTimer = 0f;
                float eggStamina = ModConfig.Daihao.ProductionStamina.Value;
                string eggItem = ModConfig.Daihao.ProductionItem.Value;
                if (eggStamina > 0f && !string.IsNullOrEmpty(eggItem))
                {
                    foreach (var c in characters)
                    {
                        if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                        if (IsDead(c)) continue;
                        if (!HasItemInInventory(c, eggItem)) continue;
                        HostApi.BoostExtraStamina(c, eggStamina);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 生产：{Nick(c)} 物品栏中有{eggItem}，+{eggStamina * 100f:0.#} 额外体力。");
                    }
                }
            }

            // ---- 待皓·我们全都是：半径内除自己外每有一名其他待皓，定时削减（治疗）损伤 ----
            _daihaoAllSameTimer += dt;
            float allSameInterval = ModConfig.Daihao.AllSameIntervalSeconds.Value;
            if (allSameInterval > 0f && _daihaoAllSameTimer >= allSameInterval)
            {
                _daihaoAllSameTimer = 0f;
                float allSameReduce = ModConfig.Daihao.AllSameInjuryReduce.Value;
                float allSameRadius = ModConfig.Daihao.AllSameRadius.Value;
                // 无待皓在场 → 跳过对全体玩家的遍历（每人都要做一次半径扫描）
                if (allSameReduce > 0f && allSameRadius > 0f && _ledger.HasClassInRoom(ClassType.Daihao))
                {
                    foreach (var c in characters)
                    {
                        if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                        if (IsDead(c)) continue;
                        int others = CountOtherDaihaoInRadius(c, allSameRadius);
                        if (others <= 0) continue;
                        float delta = allSameReduce * others;
                        HostApi.ApplyStatus(c, CharacterAfflictions.STATUSTYPE.Injury, -delta);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 我们全都是：{Nick(c)} 半径 {allSameRadius:0.#} 米内有 {others} 名其他待皓，" +
                            $"-{delta * 100f:0.#} 损伤。");
                    }
                }
            }

            // ---- 圣人·教诲：定时为半径内的其他玩家 +额外体力 / +困倦 ----
            _saintTeachTimer += dt;
            float teachInterval = Mathf.Max(1f, ModConfig.Saint.TeachIntervalSeconds.Value);
            if (_saintTeachTimer >= teachInterval)
            {
                _saintTeachTimer = 0f;
                // 无圣人在场 → 跳过对全体玩家的遍历（教诲每次会对半径内玩家逐一下发状态，跳过收益更高）
                if (_ledger.HasClassInRoom(ClassType.Saint))
                {
                    foreach (var saint in characters)
                    {
                        if (_ledger.GetClass(saint) != ClassType.Saint) continue;
                        if (IsDead(saint)) continue;
                        DoSaintTeach(saint);
                    }
                }
            }

            // ---- 圣人·完美主义：定时为圣人自己 +额外体力 ----
            _saintPerfectionTimer += dt;
            float perfectionInterval = Mathf.Max(1f, ModConfig.Saint.PerfectionIntervalSeconds.Value);
            if (_saintPerfectionTimer >= perfectionInterval)
            {
                _saintPerfectionTimer = 0f;
                float perfectionStamina = ModConfig.Saint.PerfectionStamina.Value;
                if (perfectionStamina != 0f)
                {
                    foreach (var saint in characters)
                    {
                        if (_ledger.GetClass(saint) != ClassType.Saint) continue;
                        if (IsDead(saint)) continue;
                        HostApi.BoostExtraStamina(saint, perfectionStamina);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 完美主义：{Nick(saint)} 额外体力+{perfectionStamina * 100f:0.#}");
                    }
                }
            }

            // ---- 圣人·跌落神坛：定时检测自身受到损伤，追加更多损伤 ----
            _saintFallTimer += dt;
            float fallInterval = ModConfig.Saint.FallIntervalSeconds.Value;
            if (fallInterval > 0f && _saintFallTimer >= fallInterval)
            {
                _saintFallTimer = 0f;
                PollSaintFall(characters);
            }

            // ---- 特级厨师·自带厨具：定时额外发放便携烤炉 ----
            _chefStoveTimer += dt;
            float chefStoveInterval = ModConfig.MasterChef.StoveIntervalSeconds.Value;
            if (chefStoveInterval > 0f && _chefStoveTimer >= chefStoveInterval)
            {
                _chefStoveTimer = 0f;
                int chefStoveCount = Mathf.Max(1, ModConfig.MasterChef.StoveCountPerInterval.Value);
                foreach (var chef in characters)
                {
                    if (_ledger.GetClass(chef) != ClassType.MasterChef) continue;
                    if (IsDead(chef)) continue;
                    for (int i = 0; i < chefStoveCount; i++) GrantStove(chef, "自带厨具");
                }
            }

            // ---- 特级厨师·手暖炉：手持便携烤炉时定时 +炎热 ----
            _chefHeatTimer += dt;
            float heatInterval = ModConfig.MasterChef.HandWarmerIntervalSeconds.Value;
            if (heatInterval > 0f && _chefHeatTimer >= heatInterval)
            {
                _chefHeatTimer = 0f;
                float hot = ModConfig.MasterChef.HandWarmerHot.Value;
                if (hot > 0f)
                {
                    foreach (var chef in characters)
                    {
                        if (_ledger.GetClass(chef) != ClassType.MasterChef) continue;
                        if (IsDead(chef)) continue;
                        if (!IsHoldingStove(chef)) continue;
                        HostApi.ApplyStatus(chef, CharacterAfflictions.STATUSTYPE.Hot, hot);
                    }
                }
            }

            // ---- 炊事员·后勤保障：定时额外发放便携烤炉 ----
            _cookStoveTimer += dt;
            float cookStoveInterval = ModConfig.Cook.StoveIntervalSeconds.Value;
            if (cookStoveInterval > 0f && _cookStoveTimer >= cookStoveInterval)
            {
                _cookStoveTimer = 0f;
                int cookStoveCount = Mathf.Max(1, ModConfig.Cook.StoveCountPerInterval.Value);
                foreach (var cook in characters)
                {
                    if (_ledger.GetClass(cook) != ClassType.Cook) continue;
                    if (IsDead(cook)) continue;
                    for (int i = 0; i < cookStoveCount; i++) GrantStove(cook, "后勤保障");
                }
            }

            // ---- 炊事员·后勤保障：半径内存在后勤时，定时向自身物品栏发放随机食物 ----
            _cookFoodTimer += dt;
            float foodInterval = ModConfig.Cook.FoodIntervalSeconds.Value;
            if (foodInterval > 0f && _cookFoodTimer >= foodInterval)
            {
                _cookFoodTimer = 0f;
                float supportRadius = ModConfig.Cook.SupportRadius.Value;
                foreach (var cook in characters)
                {
                    if (_ledger.GetClass(cook) != ClassType.Cook) continue;
                    if (IsDead(cook)) continue;
                    if (!HasLogisticianNearby(cook, supportRadius)) continue;

                    // 只往 3 个物品栏塞：物品栏全满时不发放（绝不塞进主手），与后勤·紧急空投同规则
                    if (!HostApi.HasEmptyItemSlot(cook))
                    {
                        Plugin.Log.LogInfo($"[RollOfTheScout] 后勤保障：{Nick(cook)} 的 3 个物品栏已满，本次不发放食物。");
                        continue;
                    }

                    string food = PickRandomFood();
                    if (string.IsNullOrEmpty(food)) continue;
                    if (HostApi.GiveItemToInventorySlots(cook, food, out _))
                        Plugin.Log.LogInfo($"[RollOfTheScout] 后勤保障：{Nick(cook)} 物品栏获得随机食物 {food}。");
                }
            }

            // ---- 炊事员·创伤应激：半径内有饥饿值超阈值的玩家（含自身）时，定时 +困倦 ----
            _cookStressTimer += dt;
            float stressInterval = ModConfig.Cook.StressIntervalSeconds.Value;
            if (stressInterval > 0f && _cookStressTimer >= stressInterval)
            {
                _cookStressTimer = 0f;
                float stressDrowsy = ModConfig.Cook.StressDrowsyPerTick.Value;
                float stressRadius = ModConfig.Cook.StressRadius.Value;
                float stressThreshold = ModConfig.Cook.StressHungerThreshold.Value;
                // 无炊事员在场 → 跳过对全体玩家的遍历（每人都可能触发半径扫描）
                if (stressDrowsy > 0f && _ledger.HasClassInRoom(ClassType.Cook))
                {
                    foreach (var cook in characters)
                    {
                        if (_ledger.GetClass(cook) != ClassType.Cook) continue;
                        if (IsDead(cook)) continue;
                        if (!HasHungryPlayerNearby(cook, stressRadius, stressThreshold)) continue;
                        HostApi.ApplyStatus(cook, CharacterAfflictions.STATUSTYPE.Drowsy, stressDrowsy);
                    }
                }
            }

            // ---- 仓鼠·我全都要：负重达标时维持加速（效果一结束即续发 → 无限加速）----
            // 原版能量饮料的加速（Affliction_FasterBoi）在「已生效时再次饮用」不会延长时间：
            // AddAffliction 命中同类型时走 Affliction_FasterBoi.Stack，而它只做
            // totalTime = Mathf.Max(totalTime, 新值)（两次饮料时长相同 → 等于没变）、
            // 不重置已累计的 timeElapsed，故续发后「约 1 秒」就按原来的终点结束（实测）。
            // 因此不再按固定间隔盲发，改为每帧检测：负重达标且当前「没有加速」时立刻续发，
            // 从而让加速不断衔接。本项复用「我全都要-加速间隔」作为两次续发之间的最短间隔，
            // 防止「刚发放、效果尚未生效/同步尚未到达」时重复发放。
            float hamsterInterval = ModConfig.Hamster.SprintIntervalSeconds.Value;
            if (hamsterInterval > 0f)
            {
                float threshold = ModConfig.Hamster.WeightThreshold.Value;
                float minGap = Mathf.Max(1f, hamsterInterval);
                foreach (var hamster in characters)
                {
                    if (_ledger.GetClass(hamster) != ClassType.Hamster) continue;
                    if (IsDead(hamster)) continue;

                    // 负重走原版状态数组（下标 7），主机对远端角色读到的是其客户端推送的同步值
                    float weight = HostApi.GetStatus(hamster, CharacterAfflictions.STATUSTYPE.Weight);
                    if (weight + 0.0125f < threshold) continue;

                    string hamsterId = _ledger.IdOf(hamster);
                    if (string.IsNullOrEmpty(hamsterId)) continue;
                    if (_hamsterSprintGuardUntil.TryGetValue(hamsterId, out float guardUntil)
                        && Time.time < guardUntil) continue;

                    // 已有加速就不再发放：续发不会延长时长，只会白生成一瓶饮料
                    if (HasAffliction(hamster, Affliction.AfflictionType.FasterBoi)) continue;

                    _hamsterSprintGuardUntil[hamsterId] = Time.time + minGap;
                    GrantSprint(hamster, "我全都要", clearDrowsyAfter: true,
                        clearAmount: ModConfig.Hamster.SprintDrowsyClearAmount.Value);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 我全都要：{Nick(hamster)} 负重 {weight * 100f:0.#} ≥ {threshold * 100f:0.#}，续发一次加速。");
                }
            }

            // ---- 仓鼠·我全都要：自身负重达标时，定时获得一次额外体力 ----
            _hamsterExtraTimer += dt;
            float hamsterExtraInterval = ModConfig.Hamster.ExtraStaminaIntervalSeconds.Value;
            if (hamsterExtraInterval > 0f && _hamsterExtraTimer >= hamsterExtraInterval)
            {
                _hamsterExtraTimer = 0f;
                float threshold = ModConfig.Hamster.WeightThreshold.Value;
                float extra = ModConfig.Hamster.ExtraStamina.Value;
                // 无仓鼠在场 → 跳过对全体玩家的遍历（每人达标时都会生成一次加速物品）
                if (extra != 0f && _ledger.HasClassInRoom(ClassType.Hamster))
                {
                    foreach (var hamster in characters)
                    {
                        if (_ledger.GetClass(hamster) != ClassType.Hamster) continue;
                        if (IsDead(hamster)) continue;

                        float weight = HostApi.GetStatus(hamster, CharacterAfflictions.STATUSTYPE.Weight);
                        if (weight + 0.0125f < threshold) continue;

                        HostApi.BoostExtraStamina(hamster, extra);
                        Plugin.Log.LogInfo($"[RollOfTheScout] 我全都要：{Nick(hamster)} 负重 {weight * 100f:0.#} ≥ {threshold * 100f:0.#}，额外体力+{extra * 100f:0.#}。");
                    }
                }
            }

            // ---- 救火队员·温暖臂膀：背起队友时，定时为半径内玩家削减损伤 ----
            _firefighterHealTimer += dt;
            float warmInterval = ModConfig.Firefighter.WarmIntervalSeconds.Value;
            if (warmInterval > 0f && _firefighterHealTimer >= warmInterval)
            {
                _firefighterHealTimer = 0f;
                // 无救火队员在场 → 跳过对全体玩家的遍历（每人触发时都会做一次半径扫描）
                if (_ledger.HasClassInRoom(ClassType.Firefighter))
                {
                    foreach (var firefighter in characters)
                    {
                        if (_ledger.GetClass(firefighter) != ClassType.Firefighter) continue;
                        if (IsDead(firefighter)) continue;
                        DoFirefighterWarmShoulder(firefighter);
                    }
                }
            }

            // ---- 救火队员·火线救援：检测「有玩家进入昏迷（倒地，非死亡）」，为救火队员安排延迟救援 ----
            PollDownedTransitions(characters);

            // ---- 救火队员·火线救援：执行延迟救援（到点无敌+传送）/ 等待背起结算 ----
            TickRescue(characters);

            // ---- 独狼·独行长路：远离所有玩家时定时 +额外体力；附近有玩家时定时 +困倦 ----
            TickLoneWolf(characters);

            // ---- 炮手·开炮：每「大炮间隔」秒发放一次童子军大炮 ----
            TickGunnerCannon(characters);

            // ---- 乐队成员：乐器扫描 / 演奏持续 / 乐队共鸣 ----
            TickBand(characters);

            // ---- 太空人：轻飘飘（周期绑气球）/ NASA（削减炎热寒冷）----
            TickAstronaut(characters);

            // ---- 榔王：精品大果（发放荔莓）/ 上瘾（超时叠加困倦）----
            TickLangwang(characters);

            // ---- 程序员：BUG（随机昆虫）/ 熬夜（夜晚加速）/ 开挂（削状态 + 复制职业效果）----
            TickProgrammer(characters);
        }

        // ---------------------------------------------------------------
        // 圣人（Saint）：教诲 / 完美主义 / 辟谷养生 / 跌落神坛
        // 完美主义·拾取消失由 Patches.SaintPickupPatch 实现；辟谷养生见 GrantSaintFasting。
        // ---------------------------------------------------------------

        /// <summary>
        /// 圣人·教诲：为指定圣人半径内的每名「其他存活玩家」增加额外体力并附加困倦。
        /// 不含圣人自己（需求为「不包括自身」）；已死亡 / 完全昏迷者跳过。
        /// </summary>
        private void DoSaintTeach(Character saint)
        {
            float stamina = ModConfig.Saint.TeachStamina.Value;
            float drowsy = ModConfig.Saint.TeachDrowsy.Value;
            if (stamina == 0f && drowsy == 0f) return;

            float radius = Mathf.Max(0f, ModConfig.Saint.TeachRadius.Value);
            var targets = HostApi.GetCharactersInRadius(saint.Center, radius);

            int n = 0;
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (target == saint) continue; // 教诲不含自身
                if (IsDead(target)) continue;

                if (stamina != 0f) HostApi.BoostExtraStamina(target, stamina);
                if (drowsy != 0f) HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                n++;
            }

            if (n > 0)
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 教诲：{Nick(saint)} 对半径 {radius:0.#} 米内的 {n} 名玩家 " +
                    $"额外体力+{stamina * 100f:0.#}、困倦+{drowsy * 100f:0.#}");
            }
        }

        /// <summary>
        /// 圣人·跌落神坛：每隔「跌落神坛-检测间隔」秒检测一次圣人自身的损伤值，
        /// 若较上次检测增加（即这段时间内受到了损伤），则额外追加一笔「跌落神坛-损伤」。
        ///
        /// 与 M·「享受」同理，「损伤增量」只能靠主机轮询同步值来判断；这里只针对圣人自己。
        /// 关键点：追加损伤后把快照更新为「追加后的预期值」，避免把自身注入的损伤
        /// 在下一轮误判为「又受到了一次损伤」而无限自我叠加（否则会瞬间暴毙并刷屏日志）。
        /// 首次见到某角色只登记基线、不追加，避免把分配职业前就已存在的损伤算作本次受到。
        /// </summary>
        private void PollSaintFall(List<Character> characters)
        {
            float amount = ModConfig.Saint.FallInjury.Value;
            if (amount == 0f) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Saint) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                float now = HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Injury);

                // 已死亡 / 昏迷：只更新快照，不追加（避免在尸体上继续叠加损伤）
                if (IsDead(character))
                {
                    _saintFallSnapshot[id] = now;
                    continue;
                }

                // 首次见到 / 刚换职业：只登记基线，不追加
                if (!_saintFallSnapshot.TryGetValue(id, out float prev))
                {
                    _saintFallSnapshot[id] = now;
                    continue;
                }

                // 本间隔内损伤增加 = 受到损伤 → 追加「跌落神坛-损伤」
                if (now - prev > 0.0001f)
                {
                    HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Injury, amount);
                    // 记录「追加后」的预期值（损伤上限为 1），防止下一轮把自身追加的损伤再次计入
                    _saintFallSnapshot[id] = Mathf.Min(now + amount, 1f);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 跌落神坛：{Nick(character)} 受到损伤（{prev * 100f:0.#} → {now * 100f:0.#}），" +
                        $"追加损伤+{amount * 100f:0.#}");
                }
                else
                {
                    _saintFallSnapshot[id] = now;
                }
            }
        }

        /// <summary>
        /// 圣人·超然物外：该角色当前是否「免疫其他职业施加的负面效果」。
        /// 条件为「超然物外-启用」且其职业为圣人。
        /// 免疫范围（在各效果入口处按此守卫跳过该角色）：
        ///   ① 潘多拉之子·潘多拉（<see cref="DoPandora"/>）；
        ///   ② 死灵法师·亡灵·光环的人类中毒（<see cref="DoNecromancerAura"/>，骷髅体力加成不受影响）；
        ///   ③ 舞王·全场欢呼的强制跟跳（<see cref="ForceNearbyPlayersToDance"/>）。
        /// </summary>
        private bool IsTranscendent(Character character)
        {
            if (!ModConfig.Saint.TranscendentEnabled.Value) return false;
            if (character == null) return false;
            return _ledger.GetClass(character) == ClassType.Saint;
        }

        /// <summary>
        /// 挑战者·顶级玩家：该角色当前是否「免疫潘多拉之子的职业效果」。
        /// 与圣人·超然物外共用潘多拉入口守卫（<see cref="DoPandora"/>），但**只覆盖潘多拉一项** ——
        /// 挑战者不免疫亡灵光环损伤、也不免疫全场欢呼的强制跟跳。
        /// </summary>
        private bool IsPandoraImmune(Character character)
        {
            if (character == null) return false;
            if (_ledger.GetClass(character) != ClassType.Challenger) return false;
            return ModConfig.Challenger.TopPlayerPandoraImmune.Value;
        }

        /// <summary>
        /// 迈尔斯残魂·受诅残魂：该角色当前是否「免疫潘多拉之子的职业效果」。
        /// 与圣人·超然物外 / 挑战者·顶级玩家共用潘多拉入口守卫（<see cref="DoPandora"/>），只覆盖潘多拉一项。
        /// </summary>
        private bool IsMilesPandoraImmune(Character character)
        {
            if (character == null) return false;
            if (_ledger.GetClass(character) != ClassType.MilesSoul) return false;
            return ModConfig.MilesSoul.PandoraImmune.Value;
        }

        // ---------------------------------------------------------------
        // 领队·迈尔斯残魂：受诅残魂 / 众魂阻挠 / 封印之力 / 零号守则 / 封印解放
        // ---------------------------------------------------------------

        /// <summary>
        /// 【众魂阻挠】「本层不再增加诅咒」的关卡判定：处于「天底（Void 生物群系）」或「顶峰（Peak 生物群系）」时为 true。
        /// 天底用 <c>VoidBiome.VoidBiomeActive</c>；顶峰用 <c>MapHandler.GetCurrentBiome() == Biome.BiomeType.Peak</c>。
        /// </summary>
        private static bool MilesNoCurseLevel()
        {
            try
            {
                if (Peak.VoidBiome.VoidBiomeActive) return true;
            }
            catch { /* 忽略：类型不可用时按非天底处理 */ }

            try
            {
                var map = Singleton<MapHandler>.Instance;
                return map != null && map.GetCurrentBiome() == Biome.BiomeType.Peak;
            }
            catch { return false; }
        }

        /// <summary>
        /// 【受诅残魂】「海滩之后无法成为本职业」的关卡判定：**已离开第一关（海滩 / Shore 生物群系）**时为 true。
        /// 判定口径：当前生物群系存在且 ≠ <c>Biome.BiomeType.Shore</c>（第二关起是 Tropics / Roots 等）。
        ///
        /// 由 <see cref="ClassLedger.GetEnabledClasses"/> 调用：命中时把残魂从候选池里剔除，
        /// 于是**开局分配、中途加入者分配、在场玩家转职**三条路径同时生效
        /// （三条路径都只经过 <c>Assign</c> / <c>TryReassign</c> + 同一个池）。
        ///
        /// ⚠ 生物群系取不到时（<c>MapHandler</c> 尚不可用 / 关卡加载中 / 不在关卡）返回 <c>false</c>：
        /// 宁可放行也不能把「海滩这一关的正常分配」一并挡掉。
        /// </summary>
        public static bool MilesPastBeach()
        {
            try
            {
                var map = Singleton<MapHandler>.Instance;
                if (map == null) return false;
                return map.GetCurrentBiome() != Biome.BiomeType.Shore;
            }
            catch { return false; }
        }

        /// <summary>记录一次「最新诅咒值」（诅咒保护的基准），可指定覆盖值（如开局初始诅咒 / 封印解放清零）。</summary>
        private void RecordMilesCurse(Character character, float value)
        {
            if (character == null) return;
            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;
            _milesCurseRecord[id] = Mathf.Clamp01(value);
        }

        /// <summary>记录一次「最新诅咒值」（读取当前主机可见值）。</summary>
        private void RecordMilesCurse(Character character)
        {
            if (character == null) return;
            RecordMilesCurse(character, HostApi.GetStatus(character, CharacterAfflictions.STATUSTYPE.Curse));
        }

        /// <summary>
        /// 迈尔斯残魂「自身职业效果」降低诅咒：施加负增量、同步下调保护基准，
        /// 并开启短暂允许窗口以吸收远端状态同步延迟（避免保护逻辑立刻把这次降低撤销）。
        /// </summary>
        private void ReduceMilesCurse(Character character, float amount, string reason)
        {
            if (character == null || amount <= 0f) return;
            HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Curse, -amount);

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;
            if (_milesCurseRecord.TryGetValue(id, out float rec))
                _milesCurseRecord[id] = Mathf.Max(0f, rec - amount);
            else
                _milesCurseRecord[id] = 0f;

            _milesCurseAllowUntil.TryGetValue(id, out float until);
            _milesCurseAllowUntil[id] = Mathf.Max(until, Time.time + 3f);

            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 诅咒-{amount * 100f:0.#}（自身职业效果，保护基准同步下调）。");
        }

        /// <summary>【众魂阻挠】每次进入新关卡 +诅咒（进入顶峰 / 天底不加），并同步刷新保护基准。</summary>
        private void OnMilesSoulNewLevel(List<Character> characters)
        {
            if (characters == null) return;
            float perLevel = ModConfig.MilesSoul.CursePerLevel.Value;
            bool noCurse = MilesNoCurseLevel();
            foreach (var c in characters)
            {
                if (_ledger.GetClass(c) != ClassType.MilesSoul) continue;

                if (noCurse)
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 众魂阻挠：{Nick(c)} 已进入顶峰 / 天底，本层不再增加诅咒。");
                }
                else if (perLevel > 0f)
                {
                    HostApi.ApplyStatus(c, CharacterAfflictions.STATUSTYPE.Curse, perLevel);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 众魂阻挠：{Nick(c)} 进入新关卡 诅咒+{perLevel * 100f:0.#}。");
                }
                RecordMilesCurse(c);
            }
        }

        /// <summary>【众魂阻挠】累计使用童军宝石道具；每满 N 次生成 1 名童军领队（保留余数）。</summary>
        private void CountMilesGemUse(Character character)
        {
            int need = ModConfig.MilesSoul.GemUsesPerScoutmaster.Value;
            if (need <= 0) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            _milesGemUseCount.TryGetValue(id, out int count);
            count++;
            if (count >= need)
            {
                count -= need;
                SpawnMilesScoutmaster(character);
            }
            _milesGemUseCount[id] = count;
        }

        /// <summary>【众魂阻挠】在自身「生成距离」之外随机方向生成 1 名童军领队并强制追击。</summary>
        private void SpawnMilesScoutmaster(Character character)
        {
            if (character == null || character.photonView == null) return;
            float dist = Mathf.Max(1f, ModConfig.MilesSoul.ScoutmasterSpawnDistance.Value);
            float chase = Mathf.Max(1f, ModConfig.MilesSoul.ScoutmasterChaseTime.Value);

            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir = dir.normalized * dist;
            Vector3 pos = character.Center + new Vector3(dir.x, 0f, dir.y);

            GameObject go;
            try
            {
                go = PhotonNetwork.InstantiateRoomObject(ItemNames.Scoutmaster, pos, Quaternion.identity, 0);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 众魂阻挠：童军领队生成失败：{e.Message}");
                return;
            }
            if (go == null) return;

            StartCoroutine(InitMilesScoutmaster(go, character, pos, chase));
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 众魂阻挠：{Nick(character)} 使用童军宝石达阈值，在 {dist:0.#} 米外生成童军领队（追击 {chase:0.#}s）。");
        }

        /// <summary>等待童军领队初始化后，把它传送到指定位置并强制追击若干秒（同法师·还魂）。</summary>
        private IEnumerator InitMilesScoutmaster(GameObject go, Character target, Vector3 pos, float chase)
        {
            var view = go.GetComponent<PhotonView>();
            for (int i = 0; i < 12 && view == null; i++)
            {
                yield return new WaitForSeconds(0.25f);
                if (go == null) yield break;
                view = go.GetComponent<PhotonView>();
            }
            if (view == null || target == null || target.photonView == null) yield break;

            view.RPC("WarpPlayerRPC", RpcTarget.All, pos, false);
            if (target.photonView.ViewID > 0)
                view.RPC("RPCA_SetCurrentTarget", RpcTarget.All, target.photonView.ViewID, chase);
        }

        /// <summary>
        /// 【封印之力】本局第一次拾取童军宝石道具时的结算（由拾取补丁 <see cref="Patches.MilesPickupPatch"/> 调用）；
        /// 集齐四种后触发【童军的力量】。
        /// </summary>
        public void OnMilesGemPicked(Character character, string gemName)
        {
            if (!_inRun) return;
            if (character == null || string.IsNullOrEmpty(gemName)) return;
            if (_ledger.GetClass(character) != ClassType.MilesSoul) return;
            if (!MilesGemItems.Contains(gemName)) return;      // 童军的荣耀不参与「首拾」

            if (!_milesGemPicked.Add(gemName)) return;          // 本局已首拾过该宝石

            float curse = GemFirstPickCurse(gemName);
            if (curse > 0f)
                ReduceMilesCurse(character, curse, $"封印之力·{gemName}首拾");
            else
                Plugin.Log.LogInfo($"[RollOfTheScout] 封印之力：{Nick(character)} 首次拾取 {gemName}（首拾诅咒为 0）。");

            // 【童军的力量】：四种宝石都拾取过后，额外 -诅咒、清除「诅咒以外」的所有负面状态（含石化），
            // 并立即获得一次无敌（奶白金）与加速（同先驱·高代谢）。
            if (_milesGemPicked.Contains(ItemNames.AmuletHealing)
                && _milesGemPicked.Contains(ItemNames.AmuletSuperJump)
                && _milesGemPicked.Contains(ItemNames.AmuletClone)
                && _milesGemPicked.Contains(ItemNames.AmuletInfiniteStamina))
            {
                float powerCurse = ModConfig.MilesSoul.PowerCurse.Value;
                if (powerCurse > 0f) ReduceMilesCurse(character, powerCurse, "童军的力量");
                HostApi.ClearNegativeStatuses(character, excludeCurse: true, excludePetrify: false);
                FeedTransientItem(character, ItemNames.FortifiedMilk, "童军的力量");
                GrantSprint(character, "童军的力量", clearDrowsyAfter: true);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 童军的力量：{Nick(character)} 已集齐四种童军宝石，清除非诅咒的全部负面状态（含石化），并获得无敌 + 加速。");
            }
        }

        private static float GemFirstPickCurse(string gemName)
        {
            switch (gemName)
            {
                case ItemNames.AmuletHealing: return ModConfig.MilesSoul.HealingFirstCurse.Value;
                case ItemNames.AmuletSuperJump: return ModConfig.MilesSoul.SuperJumpFirstCurse.Value;
                case ItemNames.AmuletClone: return ModConfig.MilesSoul.CloneFirstCurse.Value;
                case ItemNames.AmuletInfiniteStamina: return ModConfig.MilesSoul.AmbitionFirstCurse.Value;
                default: return 0f;
            }
        }

        /// <summary>由 <see cref="HandleItemConsumed"/> 调用：使用（食用）童军宝石道具时的通行结算。</summary>
        private void OnMilesGemConsumed(Item item, Character character)
        {
            if (_ledger.GetClass(character) != ClassType.MilesSoul) return;
            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            // 消费者就是残魂本人 → 「对自己使用」（喂食时消费者是被喂的队友，职业检查已在上行跳过）。
            OnMilesGemUsed(character, prefab.gameObject.name, usage: MilesGemUsage.SelfUse);
        }

        /// <summary>
        /// 由「宝石直接使用」补丁（<see cref="Patches.MilesGemUsePatch"/>）调用：
        /// 由物品对象反查持有者与宝石预制体名，再走统一的 <see cref="OnMilesGemUsed"/> 结算。
        /// </summary>
        public void NotifyMilesGemUsed(Item item, Character holder,
            MilesGemUsage usage = MilesGemUsage.Unknown)
        {
            if (!_inRun) return;
            if (item == null || holder == null) return;
            if (_ledger.GetClass(holder) != ClassType.MilesSoul) return;
            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            OnMilesGemUsed(holder, prefab.gameObject.name, usage: usage);
        }

        /// <summary>
        /// 【封印之力 / 零号守则】使用童军宝石道具的结算。
        /// 调用来源：① <c>Item.Consume</c> / <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="OnMilesGemConsumed"/>）；
        ///          ② 宝石「直接使用」的本地动作补丁（见 <see cref="Patches.MilesGemUsePatch"/> → <see cref="NotifyMilesGemUsed"/>）；
        ///          ③ <c>Action_CloneSelectedItem.RPCA_Shoot</c> 的补丁（童军的盛情·克隆成功，见 <see cref="Patches.MilesClonePatch"/>）；
        ///          ④ 客机启发式轮询（见 <see cref="PollMilesRemoteGemUse"/>，自带「使用前」石化基线）；
        ///          ⑤ 喂食补丁（见 <see cref="Patches.MilesFeedGemPatch"/>，自带「喂给谁、是否昏迷」）。
        /// 各通道由 <c>_milesUseGuardUntil</c>（玩家 + 宝石名，0.3s）去重，保证一次使用只结算一次。
        /// </summary>
        /// <param name="preUsePetrify">
        /// 「使用前」的石化值（0~100）；<c>-1</c> 表示调用方未提供，此时改用动作 Prefix 记录的快照
        /// （见 <see cref="TakeMilesPreUsePetrify"/>）。零号守则的「等量清除」以此值为基准。
        /// </param>
        /// <param name="usage">
        /// 本次使用的情形（见 <see cref="MilesGemUsage"/>）。只有毅力需要区分：自用 / 喂给未昏迷者 / 喂给昏迷者。
        /// </param>
        public void OnMilesGemUsed(Character character, string gemName, bool cloneSuccess = false, int preUsePetrify = -1,
            MilesGemUsage usage = MilesGemUsage.Unknown)
        {
            if (!_inRun) return;
            if (character == null || string.IsNullOrEmpty(gemName)) return;
            if (_ledger.GetClass(character) != ClassType.MilesSoul) return;
            if (!MilesGemItems.Contains(gemName)) return;

            // 童军的盛情：只有「成功复制了物品」（`RPCA_Shoot` 的 success==true）才算一次「使用」——
            // 空放不结算效果、不计入【众魂阻挠】，也**不占用去重窗口**（否则会挡掉紧随其后的克隆成功通道）。
            if (gemName == ItemNames.AmuletClone && !cloneSuccess) return;

            // 多通道去重：同一次使用可能同时命中「本地动作补丁」与「食用事件」两条通道
            string uid = _ledger.IdOf(character);
            string key = uid + "|" + gemName;
            if (!string.IsNullOrEmpty(uid))
            {
                if (_milesUseGuardUntil.TryGetValue(key, out float guardUntil) && Time.time < guardUntil) return;
                _milesUseGuardUntil[key] = Time.time + MilesUseDedupWindow;
            }

            // 「使用前」石化基线：调用方未给出时取动作 Prefix 记录的快照（去重通过后才取，避免被去重的那次白耗快照）。
            if (preUsePetrify < 0) preUsePetrify = TakeMilesPreUsePetrify(character);

            // 本次使用的情形（自用 / 喂给未昏迷者 / 喂给昏迷者）—— 只有毅力需要区分。
            usage = ResolveMilesGemUsage(character, gemName, usage);

            if (gemName == ItemNames.AmuletHealing)
            {
                float petrify = ModConfig.MilesSoul.HealingPetrify.Value;
                float hunger = ModConfig.MilesSoul.HealingHunger.Value;

                // 情形②：喂给其他**未昏迷**玩家 —— 无任何职业效果（原版涨多少石化就是多少），但仍计入【众魂阻挠】次数。
                if (usage == MilesGemUsage.FeedNormal)
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 童军的毅力：{Nick(character)} 喂给未昏迷的队友，无职业效果（原版石化照常）。");
                }
                // 情形③：喂给其他**昏迷**玩家 —— 【零号守则】：本次使用追加的石化**等量清除**（净变化为 0），
                // 并额外 +`零号守则-额外体力`，且不计入次数。
                else if (usage == MilesGemUsage.FeedDowned && ModConfig.MilesSoul.RuleZeroHealAssist.Value)
                {
                    int baseline = preUsePetrify >= 0 ? preUsePetrify : HostApi.GetPetrify(character);
                    RegisterMilesZeroPetrifyCancel(character, 0f, baseline);

                    float assistStamina = ModConfig.MilesSoul.RuleZeroHealAssistExtraStamina.Value;
                    if (assistStamina != 0f) HostApi.BoostExtraStamina(character, assistStamina);

                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 童军的毅力：{Nick(character)} 喂给昏迷队友 → 零号守则：石化抵消至 {Mathf.Clamp(baseline, 0, 100)}"
                        + $"（等量清除本次追加、不计入众魂阻挠；使用前基准 {baseline}）、额外体力+{assistStamina * 100f:0.#}。");
                }
                // 情形①：对自己使用 —— 【封印之力】子效果：−石化、−饥饿。
                else
                {
                    if (hunger > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -hunger);
                    ReduceMilesPetrifyDelayed(character, petrify);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 童军的毅力：{Nick(character)} 自用，饥饿-{hunger * 100f:0.#}、石化-{petrify * 100f:0.#}");
                }
            }
            else if (gemName == ItemNames.AmuletSuperJump)
            {
                float petrify = ModConfig.MilesSoul.SuperJumpPetrify.Value;
                float hunger = ModConfig.MilesSoul.SuperJumpHunger.Value;
                float stamina = ModConfig.MilesSoul.SuperJumpExtraStamina.Value;
                if (hunger > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -hunger);
                ReduceMilesPetrifyDelayed(character, petrify);
                if (stamina != 0f) HostApi.BoostExtraStamina(character, stamina);
                Plugin.Log.LogInfo($"[RollOfTheScout] 童军的进取：{Nick(character)} 使用，饥饿-{hunger * 100f:0.#}、石化-{petrify * 100f:0.#}、额外体力+{stamina * 100f:0.#}");
            }
            else if (gemName == ItemNames.AmuletClone)
            {
                // 盛情：仅「成功复制了物品」时结算效果（克隆成功由 Patches.MilesClonePatch 感知）
                float petrify = ModConfig.MilesSoul.ClonePetrify.Value;
                float hunger = ModConfig.MilesSoul.CloneHunger.Value;
                float stamina = ModConfig.MilesSoul.CloneExtraStamina.Value;
                if (hunger > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -hunger);
                ReduceMilesPetrifyDelayed(character, petrify);
                if (stamina != 0f) HostApi.BoostExtraStamina(character, stamina);
                Plugin.Log.LogInfo($"[RollOfTheScout] 童军的盛情：{Nick(character)} 成功复制物品，饥饿-{hunger * 100f:0.#}、石化-{petrify * 100f:0.#}、额外体力+{stamina * 100f:0.#}");
            }
            else if (gemName == ItemNames.AmuletInfiniteStamina)
            {
                float petrify = ModConfig.MilesSoul.AmbitionPetrify.Value;
                float hunger = ModConfig.MilesSoul.AmbitionHunger.Value;
                float stamina = ModConfig.MilesSoul.AmbitionExtraStamina.Value;
                if (hunger > 0f) HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -hunger);
                ReduceMilesPetrifyDelayed(character, petrify);
                if (stamina != 0f) HostApi.BoostExtraStamina(character, stamina);
                Plugin.Log.LogInfo($"[RollOfTheScout] 童军的野心：{Nick(character)} 使用，饥饿-{hunger * 100f:0.#}、石化-{petrify * 100f:0.#}、额外体力+{stamina * 100f:0.#}");
            }

            // 情形③（喂给昏迷队友的零号守则）不计入【众魂阻挠】次数；其余情形（自用、喂给未昏迷者）照常计入。
            bool ruleZeroAssist = gemName == ItemNames.AmuletHealing
                && usage == MilesGemUsage.FeedDowned
                && ModConfig.MilesSoul.RuleZeroHealAssist.Value;
            if (!ruleZeroAssist) CountMilesGemUse(character);
        }

        /// <summary>
        /// 登记一次「宝石减石化」的**延迟扣减**（同一玩家的多次使用会累加点数）。
        ///
        /// 为什么延迟：宝石的石化代价由原版在**使用动作中/之后**追加（`Action_HealingGem.FeederAction`、
        /// 或口袋行为的持续 tick）——若在 `ToggleGem` / 动作 Postfix 里立即 `-N`，此刻石化往往仍是 0，
        /// `-N` 被夹到 0 等于空放（实测「第一次使用不减石化、第二次才生效」）。
        /// 延迟一小段后按**当前值**绝对扣减即可覆盖追加时机，由 <see cref="TickMilesPetrifyReduce"/> 执行。
        /// </summary>
        private void ReduceMilesPetrifyDelayed(Character character, float amountFraction)
        {
            int points = Mathf.RoundToInt(Mathf.Max(0f, amountFraction) * 100f);
            if (points <= 0 || character == null) return;

            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            if (!_milesPetrifyReduce.TryGetValue(id, out var pending))
            {
                pending = new MilesPetrifyReduce();
                _milesPetrifyReduce[id] = pending;
            }
            pending.Amount += points;
            pending.FireAt = Time.time + MilesPetrifyReduceDelay;
        }

        /// <summary>推进所有「宝石减石化」延迟扣减待办：到点后按当前石化值绝对扣减。</summary>
        private void TickMilesPetrifyReduce(List<Character> characters)
        {
            if (_milesPetrifyReduce.Count == 0) return;

            List<string> done = null;
            foreach (var c in characters)
            {
                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_milesPetrifyReduce.TryGetValue(id, out var pending)) continue;

                if (IsTrueDead(c))
                {
                    (done ??= new List<string>()).Add(id);
                    continue;
                }
                if (Time.time < pending.FireAt) continue;

                int cur = HostApi.GetPetrify(c);
                int target = Mathf.Clamp(cur - pending.Amount, 0, 100);
                HostApi.SetPetrify(c, target);
                if (target != cur)
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 封印之力：{Nick(c)} 宝石减石化结算，石化 {cur} → {target}。");
                }
                (done ??= new List<string>()).Add(id);
            }
            if (done != null)
            {
                foreach (var id in done) _milesPetrifyReduce.Remove(id);
            }
        }

        /// <summary>
        /// 【零号守则·毅力助人】登记「等量清除」该次使用给自身追加的石化（绝对值写回）。
        ///
        /// 基准必须是**使用前**的石化值：本次使用（喂食 / 自用）给自身追加的石化会落在基准之上，
        /// <see cref="TickMilesZeroPetrify"/> 随后把实际值强制拉回目标，从而把「本次追加的石化」整段清除，
        /// 并照常保留职业自带的 <c>毅力-石化</c> 减量。
        ///
        /// ⚠ 历史 bug：早期版本以「登记瞬间的当前石化」为基准 —— 但登记总发生在宝石已加上代价**之后**
        /// （本地动作 Postfix / 客机启发式轮询都晚于代价落地），于是目标 = 已增加后的值 − 职业减量，
        /// 宝石追加的石化完全没被清除（实测日志 `石化 100 → 抵消为 90`、`30 → 20`）。现改用使用前基准。
        /// </summary>
        private void RegisterMilesZeroPetrifyCancel(Character character, float classPetrifyReduction, int baselinePetrify)
        {
            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;
            int reduction = Mathf.RoundToInt(Mathf.Max(0f, classPetrifyReduction) * 100f);
            _milesZeroPetrifyTarget[id] = Mathf.Clamp(baselinePetrify - reduction, 0, 100);
            _milesZeroPetrifyUntil[id] = Time.time + 3f;
            _milesZeroPetrifyNextSend.Remove(id);
        }

        /// <summary>
        /// 记录该玩家「使用宝石前」的石化值（由宝石动作 / 喂食的 Prefix 调用）。
        /// 同一段时间（0.5s）内的重复调用不覆盖 —— 一次喂食会先后经过 FinishCastSecondary 与
        /// RemoveFeedDataRPC 两处，保证留下的是**代价落地之前**的那次快照。
        /// </summary>
        public void SnapshotMilesPreUsePetrify(Character character)
        {
            if (!_inRun || character == null) return;
            if (_ledger.GetClass(character) != ClassType.MilesSoul) return;
            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return;

            if (_milesPreUsePetrify.TryGetValue(id, out var old) && Time.time - old.Value < 0.5f) return;
            _milesPreUsePetrify[id] = new KeyValuePair<int, float>(HostApi.GetPetrify(character), Time.time);
        }

        /// <summary>取出该玩家最近记录的「使用前」石化值；没有或已过期（&gt;1.5s）则返回 -1。取出即消费。</summary>
        private int TakeMilesPreUsePetrify(Character character)
        {
            string id = _ledger.IdOf(character);
            if (string.IsNullOrEmpty(id)) return -1;
            if (!_milesPreUsePetrify.TryGetValue(id, out var snap)) return -1;
            _milesPreUsePetrify.Remove(id);
            return Time.time - snap.Value > MilesPreUsePetrifyLifetime ? -1 : snap.Key;
        }

        /// <summary>
        /// 记录一次「喂食童军宝石」的意图（由 <see cref="Patches.MilesFeedStartPatch"/> 挂在
        /// <c>Item.SendFeedDataRPC</c> 上调用；该 RPC 为 <c>RpcTarget.All</c>，主机也能收到）。
        /// 关键信息是**被喂者是否昏迷** —— 毅力要靠它区分「喂给未昏迷者（无职业效果）」与「喂给昏迷者（零号守则）」。
        /// </summary>
        public void OnMilesFeedStarted(Character giver, Character receiver, string gemName, float totalUsingTime)
        {
            if (!_inRun || giver == null || receiver == null) return;
            if (_ledger.GetClass(giver) != ClassType.MilesSoul) return;
            if (!MilesGemItems.Contains(gemName)) return;

            string id = _ledger.IdOf(giver);
            if (string.IsNullOrEmpty(id)) return;

            if (!_milesFeedIntent.TryGetValue(id, out var intent))
            {
                intent = new MilesFeedIntent();
                _milesFeedIntent[id] = intent;
            }
            intent.Downed = IsDowned(receiver);
            intent.Gem = gemName;
            // 喂食耗时由 RPC 自带；完成只可能发生在时长之内，故以「时长 + 余量」为有效期，
            // 超时即作废（防止「开始喂食后取消」留下的陈旧意图被后续的自用误用）。
            intent.Until = Time.time + Mathf.Max(0f, totalUsingTime) + 1.5f;
        }

        /// <summary>
        /// 定出本次使用的情形：调用方已给出（本地动作 / 喂食补丁）则直接用；
        /// 否则（客机启发式、食用事件）查「喂食意图」——命中即判为喂食（并按被喂者是否昏迷细分），
        /// 查不到则按「对自己使用」处理。
        /// </summary>
        private MilesGemUsage ResolveMilesGemUsage(Character character, string gemName, MilesGemUsage usage)
        {
            if (usage != MilesGemUsage.Unknown) return usage;

            string id = _ledger.IdOf(character);
            if (!string.IsNullOrEmpty(id) && _milesFeedIntent.TryGetValue(id, out var intent))
            {
                if (Time.time <= intent.Until && intent.Gem == gemName)
                {
                    _milesFeedIntent.Remove(id);
                    return intent.Downed ? MilesGemUsage.FeedDowned : MilesGemUsage.FeedNormal;
                }
                if (Time.time > intent.Until) _milesFeedIntent.Remove(id);   // 过期清理
            }
            return MilesGemUsage.SelfUse;
        }

        /// <summary>
        /// 【零号守则】的前置条件：本局已拾取过「童军的毅力」（<c>Amulet_Healing</c>）。
        /// 未满足时，「昏迷 → 无敌 + 传送」与「每有玩家死亡 +损伤」两个子效果均不触发。
        /// </summary>
        private bool MilesRuleZeroUnlocked()
        {
            return _milesGemPicked.Contains(ItemNames.AmuletHealing);
        }

        /// <summary>迈尔斯残魂每帧驱动：诅咒保护轮询 + 零号守则昏迷检测 + 毅力助人石化校正 + 附近玩家加成。</summary>
        private void TickMilesSoul(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.MilesSoul)) return;

            PollMilesCurse(characters);
            PollMilesRemoteGemUse(characters);
            PollMilesRuleZero(characters);
            TickMilesZeroPetrify(characters);
            TickMilesPetrifyReduce(characters);
            TickMilesNearbyStamina(characters);
        }

        /// <summary>
        /// 【零号守则·附近玩家】每 `零号守则-附近玩家-间隔`（默认 30s）检测一次：
        /// 对每名存活的迈尔斯残魂，统计「自身 `零号守则-附近玩家-半径`（默认 30m）内的**其他存活玩家**」数量，
        /// **每有一名** → 自身 +`零号守则-附近玩家-额外体力`（默认 0.05 = 5 点）。
        /// 例：半径内有 4 名玩家 → 一次 +0.20（20 点）。间隔设为 0 或负数关闭。
        /// </summary>
        private void TickMilesNearbyStamina(List<Character> characters)
        {
            float interval = ModConfig.MilesSoul.RuleZeroNearbyInterval.Value;
            if (interval <= 0f) { _milesNearbyTimer = 0f; return; }

            // 跨场景 / 长时间停留时不累积：计时器最多推进到「一个间隔」，避免一次结算翻倍。
            if (_milesNearbyTimer > interval) _milesNearbyTimer = interval;
            _milesNearbyTimer += Time.deltaTime;
            if (_milesNearbyTimer < interval) return;
            _milesNearbyTimer = 0f;

            float per = ModConfig.MilesSoul.RuleZeroNearbyStamina.Value;
            if (per == 0f) return;
            float radius = Mathf.Max(0f, ModConfig.MilesSoul.RuleZeroNearbyRadius.Value);

            foreach (var c in characters)
            {
                if (_ledger.GetClass(c) != ClassType.MilesSoul) continue;
                if (IsDead(c)) continue;

                int n = 0;
                foreach (var other in HostApi.GetCharactersInRadius(c.Center, radius))
                {
                    if (other == null || other == c) continue;
                    if (IsDead(other)) continue;
                    n++;
                }
                if (n <= 0) continue;

                HostApi.BoostExtraStamina(c, per * n);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 零号守则：{Nick(c)} 半径 {radius:0.#} 米内有 {n} 名玩家，额外体力+{per * n * 100f:0.#}");
            }
        }

        /// <summary>
        /// 【封印之力】客机玩家的宝石「使用」启发式判定。
        ///
        /// 背景：童军宝石的直接使用是**使用者客户端本地动作**（`Action_StrangeGem.RunAction → AmuletBase.ToggleGem` 等），
        /// 既不广播 RPC 也不经过 `Item.Consume`，因此主机**收不到客机的使用信号**（主机本人已由本地动作补丁覆盖）。
        ///
        /// 原版中「增加石化」的来源只有两类：① 熔炉 / 雾沼的特殊石头（每接触一次恰好 **+2.5**）；
        /// ② 使用童军宝石道具（每次增量 **大于 2.5**）。因此对客机改用启发式判定：
        /// **玩家手执童军宝石道具**（`CharacterData.currentItem`，由 `EquipSlotRpc` 同步到主机）
        /// 且其石化值**一次性增加 ≥ `封印之力-客机判定阈值`（默认 2.5）** → 视为「使用了一次该宝石」。
        ///
        /// 防误触与防重复：
        /// - 「手执宝石」是硬条件 —— 未手持宝石（如只是碰石头）不参与判定；
        /// - 命中后写一条 `封印之力-客机判定冷却`（默认 2.5s）的每人冷却，避免「石化持续增加」型宝石（野心持续 +1）被反复结算；
        /// - 仅对 `!photonView.IsMine`（客机）生效，避免与主机本人的本地动作补丁重复触发。
        /// </summary>
        private void PollMilesRemoteGemUse(List<Character> characters)
        {
            float threshold = ModConfig.MilesSoul.RemoteGemPetrifyThreshold.Value;
            if (threshold <= 0f)
            {
                _milesPetrifySnapshot.Clear();
                return;
            }

            float interval = Mathf.Max(0.2f, ModConfig.MilesSoul.RemoteGemCheckInterval.Value);
            _milesPetrifyTimer += Time.deltaTime;
            if (_milesPetrifyTimer < interval) return;
            _milesPetrifyTimer = 0f;

            float cooldown = Mathf.Max(0f, ModConfig.MilesSoul.RemoteGemUseCooldown.Value);

            foreach (var c in characters)
            {
                if (_ledger.GetClass(c) != ClassType.MilesSoul) continue;
                var view = c.photonView;
                if (view == null) continue;
                if (view.IsMine) continue;   // 主机本人由本地动作补丁负责
                if (c.data == null) continue;

                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;

                int cur = HostApi.GetPetrify(c);
                if (!_milesPetrifySnapshot.TryGetValue(id, out int prev))
                {
                    _milesPetrifySnapshot[id] = cur;   // 首次见到：仅登记基线
                    continue;
                }
                _milesPetrifySnapshot[id] = cur;

                int delta = cur - prev;
                if (delta < threshold) continue;       // 增量不足（含特殊石头的 +2.5）→ 忽略

                string gem = FindHeldGemName(c);
                if (gem == null) continue;             // 未手执童军宝石 → 不是「使用宝石」造成的

                // 冷却按「玩家 + 宝石」分别计时：避免用了毅力后，接着用进取 / 野心被一并吞掉。
                string coolKey = id + "|" + gem;
                if (cooldown > 0f
                    && _milesRemoteUseCooldownUntil.TryGetValue(coolKey, out float until) && Time.time < until)
                    continue;

                if (cooldown > 0f) _milesRemoteUseCooldownUntil[coolKey] = Time.time + cooldown;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 封印之力：{Nick(c)} 手执 {gem} 且石化 +{delta}（≥{threshold:0.#}），判定为使用该宝石。");
                // prev 即「使用前」的石化值（本次增量就发生在 prev → cur 之间），传给结算作为零号守则的抵消基准。
                OnMilesGemUsed(c, gem, cloneSuccess: false, preUsePetrify: prev);
            }
        }

        /// <summary>
        /// 取该玩家**当前手执**的童军宝石道具预制体名（没有则返回 null）。
        /// 手执物品取自 <c>CharacterData.currentItem</c> —— 它由原版 <c>CharacterItems.EquipSlotRpc</c>（RpcTarget.All）
        /// 经 <c>Equip(item)</c> 同步到所有客户端（含主机），因此主机可零客机安装地读到客机的手执物品。
        /// </summary>
        private static string FindHeldGemName(Character character)
        {
            if (character == null || character.data == null) return null;
            var held = character.data.currentItem;
            if (held == null) return null;

            if (!ItemDatabase.TryGetItem(held.itemID, out Item prefab) || prefab == null) return null;
            string name = prefab.gameObject.name;
            return MilesGemItems.Contains(name) ? name : null;
        }

        /// <summary>
        /// 【受诅残魂】诅咒保护：每「检测间隔」秒对比「实际诅咒」与「最新记录值」。
        /// 记录值只会在「进入新关卡」与「自身职业效果 / 法师·净化降低诅咒」时刷新；
        /// 若记录值 &gt; 实际值（非预期降低）则强制恢复到记录值；允许窗口内的降低会被接受并同步刷新记录值。
        /// </summary>
        private void PollMilesCurse(List<Character> characters)
        {
            float interval = Mathf.Max(0.2f, ModConfig.MilesSoul.CurseCheckInterval.Value);
            _milesCurseTimer += Time.deltaTime;
            if (_milesCurseTimer < interval) return;
            _milesCurseTimer = 0f;

            if (!ModConfig.MilesSoul.CurseProtectEnabled.Value) return;

            foreach (var c in characters)
            {
                if (_ledger.GetClass(c) != ClassType.MilesSoul) continue;
                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;

                float cur = HostApi.GetStatus(c, CharacterAfflictions.STATUSTYPE.Curse);
                if (!_milesCurseRecord.TryGetValue(id, out float rec))
                {
                    _milesCurseRecord[id] = cur;   // 尚未记录：登记基线
                    continue;
                }

                // 允许窗口内（自身职业效果 / 法师·净化）：记录值**双向跟随**实际值。
                // 这一步必须早于「上升」分支 —— 否则自身效果刚下调记录值、而客户端同步尚未到达时，
                // 会被「实际值仍偏高 → 刷新记录值」误判为上升，从而把这次降低撤销掉（竞态）。
                if (_milesCurseAllowUntil.TryGetValue(id, out float until) && Time.time < until)
                {
                    _milesCurseRecord[id] = cur;
                    continue;
                }

                // 上升（每层 +5 / 被诅咒等）→ 刷新记录值（保护只针对「降低」）
                if (cur > rec + 0.0125f)
                {
                    _milesCurseRecord[id] = cur;
                    continue;
                }

                // 降低（非允许窗口内）→ 强制恢复到最新记录值
                if (cur < rec - 0.0125f)
                {
                    HostApi.SetStatusAbsolute(c, CharacterAfflictions.STATUSTYPE.Curse, rec);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 受诅残魂：{Nick(c)} 诅咒被外部降低（{cur * 100f:0.#} → 目标 {rec * 100f:0.#}），已强制恢复。");
                }
            }
        }

        /// <summary>
        /// 【零号守则】昏迷检测：任意玩家「由未昏迷变为昏迷」时，为每名存活迈尔斯残魂
        /// 施加一次无敌（奶白金）并传送到该昏迷玩家处（默认 10 秒冷却）。
        /// 前置条件：本局已拾取过「童军的毅力」。
        /// </summary>
        private void PollMilesRuleZero(List<Character> characters)
        {
            if (!MilesRuleZeroUnlocked()) return;

            // 与救火队员·火线救援同理：昏迷跳变按 DownedPollInterval 采样，不再每帧遍历全体玩家。
            _milesRuleZeroPollTimer += Time.deltaTime;
            if (_milesRuleZeroPollTimer < DownedPollInterval) return;
            _milesRuleZeroPollTimer = 0f;

            List<Character> justDowned = null;
            foreach (var c in characters)
            {
                var view = c.photonView;
                if (view == null || view.Owner == null) continue;
                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;

                bool downed = IsDowned(c);
                if (_milesWasDowned.TryGetValue(id, out bool was))
                {
                    if (was == downed) continue;   // 状态未变：不必写回字典
                    _milesWasDowned[id] = downed;
                    if (!was && downed) (justDowned ??= new List<Character>()).Add(c);
                }
                else
                {
                    _milesWasDowned[id] = downed;  // 首次登记：本帧不触发
                }
            }
            if (justDowned == null) return;

            float cooldown = Mathf.Max(0f, ModConfig.MilesSoul.RuleZeroTeleportCooldown.Value);
            foreach (var downedChar in justDowned)
            {
                foreach (var miles in characters)
                {
                    if (_ledger.GetClass(miles) != ClassType.MilesSoul) continue;
                    if (IsDead(miles)) continue;

                    string id = _ledger.IdOf(miles);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (cooldown > 0f && _milesRuleZeroCooldownUntil.TryGetValue(id, out float until) && Time.time < until)
                        continue;
                    if (cooldown > 0f) _milesRuleZeroCooldownUntil[id] = Time.time + cooldown;

                    string item = ModConfig.MilesSoul.RuleZeroInvincibleItem.Value;
                    if (!string.IsNullOrEmpty(item)) FeedTransientItem(miles, item, "零号守则");
                    miles.photonView.RPC("WarpPlayerRPC", RpcTarget.All, downedChar.Center, false);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 零号守则：{Nick(downedChar)} 昏迷，{Nick(miles)} 获得无敌并传送至其身边。");
                }
            }
        }

        /// <summary>零号守则·毅力助人：等量清除该次使用给自身追加的石化（绝对值写回，短暂校正）。</summary>
        private void TickMilesZeroPetrify(List<Character> characters)
        {
            if (_milesZeroPetrifyTarget.Count == 0) return;
            List<string> done = null;
            foreach (var c in characters)
            {
                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_milesZeroPetrifyTarget.TryGetValue(id, out int target)) continue;

                float until = _milesZeroPetrifyUntil.TryGetValue(id, out float u) ? u : 0f;
                int cur = HostApi.GetPetrify(c);
                if (cur > target && Time.time >= (_milesZeroPetrifyNextSend.TryGetValue(id, out float next) ? next : 0f))
                {
                    _milesZeroPetrifyNextSend[id] = Time.time + MilesZeroPetrifySendInterval;
                    HostApi.SetPetrify(c, target);
                    Plugin.Log.LogInfo($"[RollOfTheScout] 零号守则：{Nick(c)} 毅力助人，石化 {cur} → 抵消为 {target}。");
                }
                if (Time.time >= until) (done ??= new List<string>()).Add(id);
            }
            if (done != null)
            {
                foreach (var id in done)
                {
                    _milesZeroPetrifyTarget.Remove(id);
                    _milesZeroPetrifyUntil.Remove(id);
                    _milesZeroPetrifyNextSend.Remove(id);
                }
            }
        }

        // ---------------------------------------------------------------
        // 舞王（Dancer）：舞动青春 / 全场欢呼 / 狂热舞者
        // ---------------------------------------------------------------

        /// <summary>
        /// 舞王：观察到「某玩家播放了表情」（由 <see cref="Patches.EmotePatch"/> 回调，主机侧）。
        ///
        /// - 若不是跳舞动作，仅做一次「表情名观测」诊断日志后返回；
        /// - 【全场欢呼】跳舞者本身是舞王时，强制其半径内的所有其他玩家一起跳舞；
        /// - 【舞动青春】按「跳舞玩家」的检测冷却，为半径内的舞王与该跳舞玩家各 +额外体力。
        /// </summary>
        public void OnEmotePlayed(Character dancer, string emoteName)
        {
            if (dancer == null || string.IsNullOrEmpty(emoteName)) return;
            if (!_inRun) return;
            if (!ModConfig.General.Enabled.Value) return;

            bool isDance = IsDanceEmote(emoteName);
            LogObservedEmote(dancer, emoteName, isDance);
            if (!isDance) return;

            // 【全场欢呼】：自己（舞王）跳舞时，强制半径内的所有其他玩家一起跳舞
            if (_ledger.GetClass(dancer) == ClassType.Dancer)
            {
                ForceNearbyPlayersToDance(dancer);
            }

            // 【舞动青春】：半径内的舞王与该跳舞玩家各获得额外体力
            ApplyDanceYouth(dancer);
        }

        /// <summary>该表情动作名是否算作「跳舞」。见配置「跳舞动作名」。</summary>
        private static bool IsDanceEmote(string emoteName)
        {
            if (string.IsNullOrEmpty(emoteName)) return false;

            foreach (var name in WeightedPicker.ParseList(ModConfig.Dancer.DanceEmoteNames.Value))
            {
                if (string.Equals(name, emoteName, System.StringComparison.OrdinalIgnoreCase)) return true;
            }

            // 兜底：名字里含 "dance" 的动画一律视作跳舞，避免用户没填对名字时完全失效
            return emoteName.IndexOf("dance", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>配置里第一个「跳舞动作名」，用于主机强制玩家跳舞；未配置则返回 null。</summary>
        private static string FirstDanceEmoteName()
        {
            var list = WeightedPicker.ParseList(ModConfig.Dancer.DanceEmoteNames.Value);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>首次观测到某表情名时打印一次日志，便于用户定位「跳舞」的真实动画名。</summary>
        private void LogObservedEmote(Character dancer, string emoteName, bool isDance)
        {
            if (!_observedEmotes.Add(emoteName)) return;
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 舞王·观测到表情「{emoteName}」（{Nick(dancer)} 播放，" +
                $"{(isDance ? "已判定为跳舞" : "非跳舞")}）。若跳舞判定有误，请据此修改配置「舞王-跳舞动作名」。");
        }

        /// <summary>
        /// 舞王·舞动青春：为「跳舞玩家半径内」的所有舞王与该跳舞玩家各增加一次额外体力。
        /// 检测冷却按「跳舞玩家」记账（配置「舞动青春-检测冷却」，默认 1 秒），
        /// 冷却窗口内的再次跳舞不触发。半径内没有舞王时不产生任何效果。
        /// </summary>
        private void ApplyDanceYouth(Character dancer)
        {
            float amount = ModConfig.Dancer.YouthStamina.Value;
            if (amount <= 0f) return;

            float radius = Mathf.Max(0f, ModConfig.Dancer.YouthRadius.Value);

            // 找出跳舞玩家半径内的所有舞王（不含自己时的自己会在下面补上）
            var recipients = new List<Character>();
            foreach (var c in HostApi.GetActiveCharacters())
            {
                if (c == null || c.photonView == null) continue;
                if (_ledger.GetClass(c) != ClassType.Dancer) continue;
                if (Vector3.Distance(c.Center, dancer.Center) > radius) continue;
                recipients.Add(c);
            }
            if (recipients.Count == 0) return; // 附近没有舞王 → 无人受益

            // 检测冷却：冷却窗口内同一玩家的再次跳舞不触发
            float cooldown = Mathf.Max(0f, ModConfig.Dancer.YouthCooldownSeconds.Value);
            string dancerId = _ledger.IdOf(dancer);
            if (!string.IsNullOrEmpty(dancerId))
            {
                if (_dancerYouthCooldownUntil.TryGetValue(dancerId, out float until) && Time.time < until) return;
                _dancerYouthCooldownUntil[dancerId] = Time.time + cooldown;
            }

            // 跳舞玩家若也是舞王（必然已在 recipients 内），避免重复加成
            if (!recipients.Contains(dancer)) recipients.Add(dancer);

            foreach (var r in recipients) HostApi.BoostExtraStamina(r, amount);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 舞动青春：{Nick(dancer)} 跳舞，半径 {radius:0.#} 米内 {recipients.Count} 名玩家" +
                $"（含跳舞玩家本人）各 +{amount * 100f:0.#} 额外体力。");
        }

        /// <summary>
        /// 舞王·全场欢呼：舞王自己跳舞时，强制其半径内的**所有其他玩家**一起跳舞。
        /// （管控：跳过舞王自己与已死亡 / 昏迷者；第 33 批由「仅童军」扩大为「所有其他玩家」。）
        /// </summary>
        private void ForceNearbyPlayersToDance(Character dancer)
        {
            float radius = Mathf.Max(0f, ModConfig.Dancer.CheerRadius.Value);

            int n = 0;
            foreach (var c in HostApi.GetCharactersInRadius(dancer.Center, radius))
            {
                if (c == null || c == dancer) continue;
                if (IsDead(c)) continue;
                if (IsTranscendent(c))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 超然物外：{Nick(c)} 免疫全场欢呼的强制跟跳。");
                    continue;
                }
                if (ForceDance(c, "全场欢呼")) n++;
            }

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 全场欢呼：{Nick(dancer)} 跳舞，半径 {radius:0.#} 米内 {n} 名其他玩家被强制跳舞。");
        }

        /// <summary>
        /// 强制指定角色播放一次跳舞表情。
        /// 原理：对目标角色的 PhotonView 直接调用原版 <c>[PunRPC] RPCA_PlayRemove</c>（<c>RpcTarget.All</c>），
        /// 原版表情系统无归属权校验，因此主机可让任意玩家（含客机）播放指定表情，客机零安装。
        /// 若目标正在攀爬（墙面 / 绳索 / 藤蔓），先打断攀爬，否则攀爬动画会盖住跳舞动画。
        /// </summary>
        private bool ForceDance(Character target, string reason)
        {
            if (target == null || target.photonView == null) return false;

            string emoteName = FirstDanceEmoteName();
            if (string.IsNullOrEmpty(emoteName))
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：未配置「舞王-跳舞动作名」，无法强制跳舞。");
                return false;
            }

            bool interrupted = InterruptClimbing(target);
            target.photonView.RPC("RPCA_PlayRemove", RpcTarget.All, emoteName, true);
            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：强制 {Nick(target)} 跳舞（{emoteName}）" +
                $"{(interrupted ? "，已先打断攀爬" : "")}。");
            return true;
        }

        /// <summary>
        /// 舞王·狂热舞者：强制指定舞王自己做一次「后空翻」表情。
        /// 原理同 <see cref="ForceDance"/>：对目标 PhotonView 直接调原版 <c>[PunRPC] RPCA_PlayRemove</c>，
        /// 表情名固定为原版后空翻 <c>A_Scout_Emote_BackFlip</c>（反编译已确认）。
        /// <c>succeeded = true</c> → 原版 <c>BackflipRoutine</c> 会在翻完后调用 <c>Fall(1f)</c>，即**必定倒地约 1 秒**（用户要求）。
        /// 该动作不是「跳舞」，不会再触发「舞动青春」与「全场欢呼」。
        /// 若目标正在攀爬，先打断攀爬，否则攀爬动画会盖住后空翻。
        /// </summary>
        private bool ForceBackflip(Character target, string reason)
        {
            if (target == null || target.photonView == null) return false;

            const string emoteName = "A_Scout_Emote_BackFlip";
            bool interrupted = InterruptClimbing(target);
            target.photonView.RPC("RPCA_PlayRemove", RpcTarget.All, emoteName, true);
            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：强制 {Nick(target)} 后空翻（{emoteName}）" +
                $"{(interrupted ? "，已先打断攀爬" : "")}。");
            return true;
        }

        // ---------------------------------------------------------------
        // 待皓（Daihao）：化身 / 喂喂喂，听得见吗 / 脚滑
        // ---------------------------------------------------------------

        /// <summary>
        /// 待皓·喂喂喂，听得见吗：该角色在这一瞬间是否「真的在说话」。
        ///
        /// 依据原版 <see cref="AnimatedMouth"/>.<c>isSpeaking</c>：
        /// - 对本地角色：由本地麦克风缓冲 + 噪音门限（<c>localNoiseGate</c>）实时计算，开麦但不出声 = false；
        /// - 对远端角色：由本机收到的语音音频（<c>CharacterVoiceHandler.LastReadSquaredMax</c>）实时计算，
        ///   因此**主机上也能读到客机是否真的出声**（每个角色在本机都有 AnimatedMouth 实例）。
        /// </summary>
        private static bool IsActuallySpeaking(Character character)
        {
            if (character == null) return false;
            var mouth = character.GetComponent<AnimatedMouth>();
            return mouth != null && mouth.isSpeaking;
        }

        /// <summary>
        /// 待皓·化身：把待皓玩家的装扮替换为配置中指定的款式。
        ///
        /// 原理：玩家的 8 项装扮编号存在 <see cref="CharacterCustomizationData"/> 里，
        /// 经 <c>PersistentPlayerDataService.SetPlayerData()</c> 广播给所有客户端
        /// （<c>ReceiverGroup.Others</c>，含客机本人）；该方法**无归属权校验**，
        /// 因此主机可改写任意玩家的装扮，客机零安装。
        ///
        /// 套用后会在「监控窗口」（配置 `化身-监控时长`，默认 15 秒）内**每秒比对一次**：
        /// 只要本效果控制的项不再等于期望值，就重新套用并广播，从而自愈；窗口结束后不再检测（避免无谓开销）。
        /// 之所以需要自愈：目标玩家自己的客户端会在「角色实例化后异步读取 Steam 装扮」时，通过
        /// <c>CharacterCustomization.SetCharacterXxx → SetCustomizationData → SetPlayerData(LocalPlayer, …)</c>
        /// 再广播一份自己的真实装扮；该包若晚于本效果到达，会把装扮覆盖回去 —— 这正是「开局（尤其客机 /
        /// 中途加入）化身不生效、转职离开再转回才生效」的根因。
        /// 配置项填 -1 的项保持该玩家原值不变（不参与比对与覆盖）。
        ///
        /// **归属判定**：只对当前职业为「待皓」的玩家套用。对曾经套用过、但现在已不是待皓的玩家
        /// （典型场景：转职离开），立即把装扮还原为套用前记录的原始款式，避免其顶着待皓外观。
        /// </summary>
        private void ApplyDaihaoAvatar(List<Character> characters)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 既没有待皓玩家、也没有「曾套用待皓装扮待还原」的记录 → 无事可做：
            // 直接返回，省掉每帧的服务查找与对全体玩家的遍历。
            if (!_ledger.HasClassInRoom(ClassType.Daihao) && _avatarOriginals.Count == 0) return;

            var service = GameHandler.GetService<PersistentPlayerDataService>();
            if (service == null) return;

            bool enabled = ModConfig.Daihao.AvatarEnabled.Value;
            int skin = ModConfig.Daihao.AvatarSkin.Value;
            int eyes = ModConfig.Daihao.AvatarEyes.Value;
            int mouth = ModConfig.Daihao.AvatarMouth.Value;
            int accessory = ModConfig.Daihao.AvatarAccessory.Value;
            int outfit = ModConfig.Daihao.AvatarOutfit.Value;
            int hat = ModConfig.Daihao.AvatarHat.Value;
            int sash = ModConfig.Daihao.AvatarSash.Value;
            int medal = ModConfig.Daihao.AvatarMedal.Value;
            float monitorSeconds = ModConfig.Daihao.AvatarMonitorSeconds.Value;

            // 8 项全为 -1 表示没有要套用的装扮。此时仍要往下走「还原」分支：
            // 可能刚把「化身-启用」关掉，需要把已套用过的玩家还原回去。
            bool anyTarget = skin >= 0 || eyes >= 0 || mouth >= 0 || accessory >= 0 ||
                             outfit >= 0 || hat >= 0 || sash >= 0 || medal >= 0;

            foreach (var c in characters)
            {
                if (c == null || c.photonView == null || c.photonView.Owner == null) continue;
                string id = _ledger.IdOf(c);
                if (string.IsNullOrEmpty(id)) continue;

                bool shouldHaveAvatar = enabled && anyTarget && _ledger.GetClass(c) == ClassType.Daihao;

                if (shouldHaveAvatar)
                {
                    var player = c.photonView.Owner;
                    var data = service.GetPlayerData(player);
                    if (data == null) continue;
                    if (data.customizationData == null) data.customizationData = new CharacterCustomizationData();

                    var cd = data.customizationData;

                    // 本关首次看到该待皓玩家：记录原始装扮快照，并开启「自愈监控窗口」。
                    // 换关会重建角色、可能再次触发客户端装扮同步，故每关重新开窗（_avatarApplied 每关清空）。
                    if (!_avatarApplied.Contains(id))
                    {
                        if (!_avatarOriginals.ContainsKey(id))
                        {
                            _avatarOriginals[id] = new AvatarSnapshot
                            {
                                Player = player,
                                Data = CloneCustomization(cd)
                            };
                        }
                        _avatarMonitorUntil[id] = Time.unscaledTime + Mathf.Max(0f, monitorSeconds);
                    }
                    var snap = _avatarOriginals[id];

                    // 期望装扮：只在首次时计算一次并缓存到快照，避免每帧分配（-1 的项不参与比对与覆盖）。
                    var expected = snap.Applied;
                    if (expected == null)
                    {
                        expected = CloneCustomization(cd) ?? new CharacterCustomizationData();
                        if (skin >= 0) expected.currentSkin = skin;
                        if (eyes >= 0) expected.currentEyes = eyes;
                        if (mouth >= 0) expected.currentMouth = mouth;
                        if (accessory >= 0) expected.currentAccessory = accessory;
                        if (outfit >= 0) expected.currentOutfit = outfit;
                        if (hat >= 0) expected.currentHat = hat;
                        if (sash >= 0) expected.currentSash = sash;
                        if (medal >= 0) expected.currentMedal = medal;
                        expected.CorrectValues();
                        snap.Applied = expected;
                    }

                    // 节流：每秒最多比对一次（首次立即执行，不受节流影响）。
                    if (_avatarNextCheck.TryGetValue(id, out float next) && Time.unscaledTime < next) continue;
                    _avatarNextCheck[id] = Time.unscaledTime + 1f;

                    // 监控窗口之外不再检测（窗口 = 被分配为待皓 / 换关后的一段时间，覆盖客户端装扮同步的竞态）。
                    if (!_avatarMonitorUntil.TryGetValue(id, out float until) || Time.unscaledTime > until)
                    {
                        _avatarApplied.Add(id);
                        continue;
                    }

                    // 漂移检测（本修复的核心）：装扮仍等于期望 → 无需重复广播。
                    // 目标玩家自己的客户端会在「角色实例化后异步读取 Steam 装扮」时再广播一份自己的真实装扮，
                    // 晚于本效果到达时会把装扮覆盖回去；每秒检测一次即可自愈重发。
                    if (AvatarMatches(cd, expected, skin, eyes, mouth, accessory, outfit, hat, sash, medal))
                    {
                        _avatarApplied.Add(id);
                        continue;
                    }

                    bool firstApply = !_avatarApplied.Contains(id);

                    // 套用期望值并广播（只覆盖本效果控制的项，-1 的项保持玩家现值）。
                    if (skin >= 0) cd.currentSkin = expected.currentSkin;
                    if (eyes >= 0) cd.currentEyes = expected.currentEyes;
                    if (mouth >= 0) cd.currentMouth = expected.currentMouth;
                    if (accessory >= 0) cd.currentAccessory = expected.currentAccessory;
                    if (outfit >= 0) cd.currentOutfit = expected.currentOutfit;
                    if (hat >= 0) cd.currentHat = expected.currentHat;
                    if (sash >= 0) cd.currentSash = expected.currentSash;
                    if (medal >= 0) cd.currentMedal = expected.currentMedal;
                    cd.CorrectValues();
                    service.SetPlayerData(player, data);
                    _avatarApplied.Add(id);

                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 化身：{Nick(c)} " +
                        (firstApply ? "装扮已套用为" : "装扮被客户端自身同步覆盖，已重新套用为") +
                        $" 皮肤{cd.currentSkin} 眼睛{cd.currentEyes} 嘴巴{cd.currentMouth} " +
                        $"饰品{cd.currentAccessory} 服装{cd.currentOutfit} 帽子{cd.currentHat} " +
                        $"腰带{cd.currentSash} 奖章{cd.currentMedal}。");
                }
                else if (_avatarOriginals.ContainsKey(id))
                {
                    // 曾经套用过，但现在不再是待皓（或「化身-启用」被关闭）→ 立即还原原始装扮。
                    RevertAvatar(id, c.photonView.Owner, Nick(c), service);
                }
            }
        }

        /// <summary>
        /// 把某位玩家的装扮还原为被改写前的原始快照并广播，同时清掉其「已套用 / 原始快照」记账，
        /// 使其之后若再次成为待皓时可以重新套用。
        /// </summary>
        private void RevertAvatar(string id, Photon.Realtime.Player player, string who, PersistentPlayerDataService service)
        {
            AvatarSnapshot snap;
            if (!_avatarOriginals.TryGetValue(id, out snap)) return;

            try
            {
                if (player != null && service != null && snap != null && snap.Data != null)
                {
                    var data = service.GetPlayerData(player);
                    if (data != null)
                    {
                        data.customizationData = snap.Data;
                        service.SetPlayerData(player, data);
                        Plugin.Log.LogInfo($"[RollOfTheScout] 化身：{who} 已不是待皓，装扮已还原为原始款式。");
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 化身：还原 {who} 装扮失败：{e.Message}");
            }

            _avatarOriginals.Remove(id);
            _avatarApplied.Remove(id);
            _avatarMonitorUntil.Remove(id);
            _avatarNextCheck.Remove(id);
        }

        /// <summary>克隆一份装扮数据。<see cref="CharacterCustomizationData"/> 是普通引用类型，需自行复制各字段。</summary>
        private static CharacterCustomizationData CloneCustomization(CharacterCustomizationData src)
        {
            if (src == null) return null;
            return new CharacterCustomizationData
            {
                currentSkin = src.currentSkin,
                currentAccessory = src.currentAccessory,
                currentEyes = src.currentEyes,
                currentMouth = src.currentMouth,
                currentOutfit = src.currentOutfit,
                currentHat = src.currentHat,
                currentSash = src.currentSash,
                currentMedal = src.currentMedal
            };
        }

        /// <summary>
        /// 判断当前装扮 <paramref name="cur"/> 是否已等于本效果期望装扮 <paramref name="exp"/>
        /// （只比对配置项 &gt;=0、也就是本效果真正控制的那些项；-1 的项不干预）。
        /// </summary>
        private static bool AvatarMatches(CharacterCustomizationData cur, CharacterCustomizationData exp,
            int skin, int eyes, int mouth, int accessory, int outfit, int hat, int sash, int medal)
        {
            if (cur == null || exp == null) return false;
            if (skin >= 0 && cur.currentSkin != exp.currentSkin) return false;
            if (eyes >= 0 && cur.currentEyes != exp.currentEyes) return false;
            if (mouth >= 0 && cur.currentMouth != exp.currentMouth) return false;
            if (accessory >= 0 && cur.currentAccessory != exp.currentAccessory) return false;
            if (outfit >= 0 && cur.currentOutfit != exp.currentOutfit) return false;
            if (hat >= 0 && cur.currentHat != exp.currentHat) return false;
            if (sash >= 0 && cur.currentSash != exp.currentSash) return false;
            if (medal >= 0 && cur.currentMedal != exp.currentMedal) return false;
            return true;
        }

        /// <summary>
        /// 一局结束时（回到机场 / 离开房间）把所有待皓玩家被改写的装扮统一还原为原始款式。
        /// 此时职业账本已被清空、无法再按职业判定，因此直接按快照里记录的玩家对象还原。
        /// </summary>
        private void RevertAllAvatars()
        {
            if (_avatarOriginals.Count == 0) return;
            if (!PhotonNetwork.InRoom) return;

            var service = GameHandler.GetService<PersistentPlayerDataService>();
            if (service == null) return;

            // 复制一份键值对再遍历：RevertAvatar 会就地删除 _avatarOriginals 中的项。
            foreach (var kv in new List<KeyValuePair<string, AvatarSnapshot>>(_avatarOriginals))
            {
                var snap = kv.Value;
                string who = snap != null && snap.Player != null ? snap.Player.NickName : kv.Key;
                RevertAvatar(kv.Key, snap != null ? snap.Player : null, who, service);
            }
        }

        /// <summary>
        /// 强制目标角色停止攀爬（墙面 / 绳索 / 藤蔓），以便其能正常播放跳舞动画。
        /// 原理：原版三种攀爬的停止逻辑均为 <c>[PunRPC]</c> 且无归属权校验，主机可代远端玩家触发。
        /// 返回是否确实打断了至少一种攀爬。
        /// </summary>
        private static bool InterruptClimbing(Character target)
        {
            if (target == null || target.photonView == null || target.data == null) return false;

            bool any = false;
            try
            {
                if (target.data.isClimbing)
                {
                    target.photonView.RPC("StopClimbingRpc", RpcTarget.All, 0f);
                    any = true;
                }
                if (target.data.isRopeClimbing)
                {
                    target.photonView.RPC("StopRopeClimbingRpc", RpcTarget.All, false);
                    any = true;
                }
                if (target.data.isVineClimbing)
                {
                    target.photonView.RPC("StopVineClimbingRpc", RpcTarget.All, false);
                    any = true;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 打断攀爬时异常：{e.Message}");
            }
            return any;
        }

        /// <summary>
        /// 童军·【集体】的档位判定（第 85 批三档重置）：
        ///   0 = **独自**（半径内没有任何其他存活玩家）
        ///   1 = **同伴**（半径内有其他存活玩家，但其中没有童军或领袖）
        ///   2 = **童军/领袖**（半径内存在其他存活的童军或领袖）
        /// 昏迷 / 死亡者不计入「其他玩家」（与待皓·我们全都是的计数口径一致）。
        /// </summary>
        private int ScoutCompanyTier(Character scout, float radius)
        {
            if (scout == null) return 0;

            var nearby = HostApi.GetCharactersInRadius(scout.Center, radius);
            bool anyPeer = false;
            foreach (var c in nearby)
            {
                if (c == null || c == scout) continue;
                if (IsDead(c)) continue;

                var type = _ledger.GetClass(c);
                if (type == ClassType.Scout || type == ClassType.Leader) return 2;   // 最高档
                anyPeer = true;
            }
            return anyPeer ? 1 : 0;
        }

        /// <summary>
        /// 童军·【集体】**单状态**档：每 <paramref name="interval"/> 秒给「当前处于 <paramref name="tier"/> 档」的童军施加一份
        /// <paramref name="amount"/> 点 <paramref name="type"/>（负值即恢复）。interval ≤ 0 或 amount == 0 视为关闭。
        /// </summary>
        private void FireScoutTierStatus(List<Character> characters, ref float timer, float interval, int tier, float radius,
            CharacterAfflictions.STATUSTYPE type, float amount)
        {
            if (interval <= 0f || amount == 0f) return;

            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            foreach (var scout in characters)
            {
                if (_ledger.GetClass(scout) != ClassType.Scout) continue;
                if (IsDead(scout)) continue;
                if (ScoutCompanyTier(scout, radius) != tier) continue;

                HostApi.ApplyStatus(scout, type, amount);
            }
        }

        /// <summary>
        /// 童军·【集体】**双状态**档（损伤与饥饿同间隔）：每 <paramref name="interval"/> 秒把两项状态合并为**单条 RPC** 结算
        /// （与逐条 <see cref="HostApi.ApplyStatus"/> 语义等价，减少网络开销；某一项为 0 时自动退回单条下发）。
        /// </summary>
        private void FireScoutTierPair(List<Character> characters, ref float timer, float interval, int tier, float radius,
            CharacterAfflictions.STATUSTYPE typeA, float amountA, CharacterAfflictions.STATUSTYPE typeB, float amountB)
        {
            if (interval <= 0f || (amountA == 0f && amountB == 0f)) return;

            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            foreach (var scout in characters)
            {
                if (_ledger.GetClass(scout) != ClassType.Scout) continue;
                if (IsDead(scout)) continue;
                if (ScoutCompanyTier(scout, radius) != tier) continue;

                HostApi.ApplyStatusPair(scout, typeA, amountA, typeB, amountB);
            }
        }

        /// <summary>
        /// 待皓·我们全都是：统计半径内「除自己以外」的待皓数量（不含自己；昏迷/死亡者不计）。
        /// </summary>
        private int CountOtherDaihaoInRadius(Character daihao, float radius)
        {
            int count = 0;
            var nearby = HostApi.GetCharactersInRadius(daihao.Center, radius);
            foreach (var c in nearby)
            {
                if (c == null || c == daihao) continue;
                if (_ledger.GetClass(c) != ClassType.Daihao) continue;
                if (IsDead(c)) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// 潘多拉：对半径内的玩家施加潘多拉魔盒。
        /// 是否包含自己由 `潘多拉-魔盒含自身` 决定（默认 false = 不含自己）。
        /// 实现方式与先驱「高代谢」的加速完全一致 —— 潘多拉魔盒改的是目标客户端本地的东西，
        /// 无法由主机直接下发，因此这里生成一个潘多拉魔盒并对目标角色调用 FeedItem，
        /// 由目标客户端自己「使用」它。
        /// </summary>
        private void DoPandora(Character pandora)
        {
            float radius = ModConfig.Pandora.BoxRadius.Value;
            bool includeSelf = ModConfig.Pandora.BoxIncludeSelf.Value;
            var targets = HostApi.GetCharactersInRadius(pandora.Center, radius);

            int n = 0;
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (!includeSelf && target == pandora) continue; // 默认不含自己
                if (IsDead(target)) continue;
                if (IsTranscendent(target))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 超然物外：{Nick(target)} 免疫潘多拉魔盒。");
                    continue;
                }
                if (IsPandoraImmune(target))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 顶级玩家：{Nick(target)} 免疫潘多拉魔盒。");
                    continue;
                }
                if (IsMilesPandoraImmune(target))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 受诅残魂：{Nick(target)} 免疫潘多拉魔盒。");
                    continue;
                }
                GrantPandoraBox(target);
                n++;
            }
            string scope = includeSelf ? "含自身" : "不含自己";
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 潘多拉：{Nick(pandora)} 对半径 {radius:0.#} 米内的 {n} 名玩家施加了潘多拉魔盒（{scope}）。");
        }

        /// <summary>
        /// 让目标角色「使用」一个潘多拉魔盒（见 <see cref="DoPandora"/>）。
        /// 与 <see cref="GrantSprint"/> 同理：生成临时物品 → FeedItem → 延迟销毁。
        /// </summary>
        private void GrantPandoraBox(Character character)
        {
            // 摊帧执行：入队后由 DrainTransientFeeds 逐帧生成（潘多拉对半径内多名玩家逐一施加，属同帧爆发源）
            EnqueueTransientFeed(character, ItemNames.PandorasBox, "潘多拉", TransientKind.PandoraBox);
        }

        private void DoInspire(Character leader)
        {
            float radius = ModConfig.Leader.InspireRadius.Value;
            float stamina = ModConfig.Leader.InspireExtraStamina.Value;
            float hunger = ModConfig.Leader.InspireHungerAmount.Value;
            float heal = ModConfig.Leader.InspireHealAmount.Value;

            var targets = HostApi.GetCharactersInRadius(leader.Center, radius);
            foreach (var target in targets)
            {
                if (stamina != 0f) HostApi.BoostExtraStamina(target, stamina);
                // 同一时刻对同一玩家减饥饿 + 减损伤：合并为单条 RPC（语义与逐条等价）
                HostApi.ApplyStatusPair(target,
                    CharacterAfflictions.STATUSTYPE.Hunger, -hunger,
                    CharacterAfflictions.STATUSTYPE.Injury, -heal);
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 领袖号召：{leader.photonView.Owner.NickName} 影响了 {targets.Count} 名角色。");
        }

        /// <summary>
        /// 领袖号召·清醒：单独按 1 分钟计时，为半径内所有人（含领袖自己）清除困倦。
        /// 与「号召」主效果（3 分钟一次的体力/饥饿/损伤）分开计时，因为两者间隔不同。
        /// </summary>
        private void DoInspireDrowsy(Character leader)
        {
            float radius = ModConfig.Leader.InspireDrowsyRadius.Value;
            float amount = ModConfig.Leader.InspireDrowsyClearAmount.Value;
            if (amount <= 0f) return;

            var targets = HostApi.GetCharactersInRadius(leader.Center, radius);
            foreach (var target in targets)
            {
                HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Drowsy, -amount);
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] 领袖号召·清醒：{leader.photonView.Owner.NickName} 为 {targets.Count} 名角色清除困倦 {amount * 100f:0.#} 点。");
        }

        private void DoAirdrop(Character logistician)
        {
            int count = Mathf.Max(1, ModConfig.Logistician.AirdropCount.Value);
            string nick = logistician.photonView != null && logistician.photonView.Owner != null
                ? logistician.photonView.Owner.NickName
                : "?";

            for (int i = 0; i < count; i++)
            {
                // 只往 3 个物品栏塞：物品栏全满时不再发放（不会塞进主手）
                if (!HostApi.HasEmptyItemSlot(logistician))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 紧急空投：{nick} 的 3 个物品栏已满，本次不发放。");
                    break;
                }

                string itemName = PickAirdropItem();
                if (string.IsNullOrEmpty(itemName)) continue;

                if (HostApi.GiveItemToInventorySlots(logistician, itemName, out _))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 紧急空投：{nick} 的物品栏获得 {itemName}");
                }
            }
        }

        private static string PickAirdropItem()
        {
            var blacklist = WeightedPicker.ParseList(ModConfig.Logistician.AirdropBlacklist.Value);
            var pool = WeightedPicker.ParseList(ModConfig.Logistician.AirdropPool.Value);

            // 池子为空时退化为「全部非禁用物品」
            if (pool.Count == 0)
            {
                var database = Zorro.Core.SingletonAsset<ItemDatabase>.Instance;
                if (database != null && database.Objects != null)
                {
                    foreach (var item in database.Objects)
                    {
                        if (item == null) continue;
                        pool.Add(item.name);
                    }
                }
            }

            var candidates = new List<string>();
            foreach (var name in pool)
            {
                if (blacklist.Contains(name)) continue;
                candidates.Add(name);
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        private void DoPoisonReduce(Character doctor)
        {
            float radius = ModConfig.Doctor.PoisonReduceRadius.Value;
            float allyAmount = ModConfig.Doctor.PoisonReduceAlly.Value;
            float selfMultiplier = ModConfig.Doctor.PoisonReduceSelfMultiplier.Value;

            var targets = HostApi.GetCharactersInRadius(doctor.Center, radius);
            foreach (var target in targets)
            {
                bool isSelf = target == doctor;
                float amount = allyAmount * (isSelf ? selfMultiplier : 1f);
                if (amount <= 0f) continue;

                // 同一时刻对同一玩家清中毒 + 清孢子：合并为单条 RPC（语义与逐条等价）
                HostApi.ApplyStatusPair(target,
                    CharacterAfflictions.STATUSTYPE.Poison, -amount,
                    CharacterAfflictions.STATUSTYPE.Spores, -amount);
            }
        }

        /// <summary>
        /// 医生·护理学：每隔一段时间对半径内所有玩家的损伤做一次削减（治疗）。
        /// 与毒理学同样支持「自身倍率」，默认 1（即医生自身与队友削减量相同）。
        /// </summary>
        private void DoNursing(Character doctor)
        {
            float radius = ModConfig.Doctor.NursingRadius.Value;
            float allyAmount = ModConfig.Doctor.NursingAlly.Value;
            float selfMultiplier = ModConfig.Doctor.NursingSelfMultiplier.Value;

            var targets = HostApi.GetCharactersInRadius(doctor.Center, radius);
            foreach (var target in targets)
            {
                bool isSelf = target == doctor;
                float amount = allyAmount * (isSelf ? selfMultiplier : 1f);
                if (amount <= 0f) continue;

                HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Injury, -amount);
            }
        }

        /// <summary>
        /// 死灵法师·亡灵·光环：以自身为中心、半径内（**不含自身**）——
        /// 人类玩家 +中毒，骷髅玩家 +额外体力。已死亡/昏迷者跳过。
        /// 骷髅判定使用原版 <c>CharacterData.isSkeleton</c>（见 <see cref="IsSkeleton"/>）。
        /// </summary>
        private void DoNecromancerAura(Character necromancer)
        {
            float radius = ModConfig.Necromancer.AuraRadius.Value;
            if (radius <= 0f) return;

            float humanPoison = ModConfig.Necromancer.AuraHumanPoison.Value;
            float skeletonStamina = ModConfig.Necromancer.AuraSkeletonStamina.Value;
            if (humanPoison == 0f && skeletonStamina == 0f) return;

            var targets = HostApi.GetCharactersInRadius(necromancer.Center, radius);
            foreach (var target in targets)
            {
                if (target == null || target == necromancer) continue; // 不含自身
                if (IsDead(target)) continue;

                if (IsSkeleton(target))
                {
                    if (skeletonStamina != 0f)
                        HostApi.BoostExtraStamina(target, skeletonStamina);
                }
                else if (humanPoison != 0f)
                {
                    if (IsTranscendent(target))
                    {
                        Plugin.Log.LogInfo($"[RollOfTheScout] 超然物外：{Nick(target)} 免疫亡灵光环中毒。");
                        continue;
                    }
                    HostApi.ApplyStatus(target, CharacterAfflictions.STATUSTYPE.Poison, humanPoison);
                }
            }
        }

        /// <summary>
        /// 死灵法师·亡灵·光环·无敌：每隔「亡灵-光环无敌间隔」秒，为半径「亡灵-光环半径」内的
        /// **骷髅玩家**（不含死灵法师自身）各施加一次无敌效果（奶白金 `FortifiedMilk`）。
        /// 与 <see cref="DoNecromancerAura"/> 各自独立计时；人类玩家不受影响；已死亡/昏迷者跳过。
        /// </summary>
        private void GrantNecromancerAuraInvincibility(Character necromancer)
        {
            float radius = ModConfig.Necromancer.AuraRadius.Value;
            if (radius <= 0f) return;

            int n = 0;
            foreach (var target in HostApi.GetCharactersInRadius(necromancer.Center, radius))
            {
                if (target == null || target.photonView == null) continue;
                if (target == necromancer) continue;   // 不含自身
                if (IsDead(target)) continue;
                if (!IsSkeleton(target)) continue;     // 只对骷髅玩家生效

                FeedTransientItem(target, ItemNames.FortifiedMilk, "亡灵光环");
                n++;
            }

            if (n > 0)
                Plugin.Log.LogInfo($"[RollOfTheScout] 亡灵：{Nick(necromancer)} 的光环为半径 {radius:0.#} 米内 {n} 名骷髅玩家施加了无敌（奶白金）。");
        }

        // ---------------------------------------------------------------
        // 挑战者·不屑：用抓钩（黄金抓钩 / 救援抓钩）钩到玩家时，立刻为自己增加损伤
        //   感知通道：RescueHook.RPCA_RescueCharacter（[PunRPC]，钩到角色时以 RpcTarget.All 广播，
        //   主机也收得到），由 Patches/ChallengerHookPatch 的 Postfix 调用本方法。
        //   持钩者由 Item.holderCharacter 反查（原版 EquipSlotRpc 以 RpcTarget.All 在主机端设置，可靠）。
        // ---------------------------------------------------------------

        /// <summary>
        /// 挑战者【不屑】：抓钩钩到玩家的瞬间，为**持钩的挑战者本人**增加「不屑-损伤」（默认 0.50 = 50 点）。
        /// 第 54 批起由「+150 困倦（直接昏迷）」改为「+50 损伤」（数值由配置项控制）。
        /// 黄金抓钩（`RescueHook_Infinite`）与救援抓钩（`RescueHook`）共用同一个 <c>RescueHook</c> 组件，
        /// 因此两种抓钩都会命中本效果。
        /// </summary>
        public void OnChallengerHookedCharacter(Character holder)
        {
            if (holder == null) return;
            if (_ledger.GetClass(holder) != ClassType.Challenger) return;

            float injury = ModConfig.Challenger.DisdainInjury.Value;
            if (injury <= 0f) return;

            // 已倒地者不重复施加：再叠加没有意义，也会刷屏
            if (IsDead(holder)) return;

            HostApi.ApplyStatus(holder, CharacterAfflictions.STATUSTYPE.Injury, injury);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 不屑：{Nick(holder)} 用抓钩钩到了玩家，自身损伤 +{injury * 100f:0.#}。");
        }

        // ---------------------------------------------------------------
        // 本机（主机）屏幕提示
        //   原版没有可用的通知 UI（UI_Notifications 无实例、无调用方），
        //   因此自绘一个左上角提示层，只在主机自己的屏幕上显示。
        // ---------------------------------------------------------------

        private void ShowToast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_toasts.Count >= ToastMaxCount)
            {
                _toasts.RemoveAt(0);
                _toastExpiries.RemoveAt(0);
            }
            _toasts.Add(text);
            _toastExpiries.Add(Time.time + ToastDuration);
            _toastVersion++;   // 内容已变：让每帧的叠加层推送重建一次文本

            // 同步到安装了附属模组（RollOfTheScoutClient）的客机，让其在客机自己屏幕上也能看到同一条提示。
            ClassDisplayNet.BroadcastToast(text);
        }

        private void ExpireToasts()
        {
            if (_toasts.Count == 0) return;
            float now = Time.time;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (now >= _toastExpiries[i])
                {
                    _toasts.RemoveAt(i);
                    _toastExpiries.RemoveAt(i);
                    _toastVersion++;   // 内容已变（有提示过期）
                }
            }
        }

        /// <summary>
        /// 读取「职业显示」按键，更新面板的可见状态。
        /// 三种模式由配置「职业显示-模式」决定：
        /// 含「按住」= 按住显示、含「切换」= 按下切换、其余 = 按下显示 5 秒。
        /// PEAK 同时启用了新版 Input System 与旧版 Input Manager（游戏自身也在用 Input.GetKey），
        /// 因此这里直接用 UnityEngine.Input 读取即可。
        /// </summary>
        private void UpdateClassListInput()
        {
            RefreshClassListInputCache();
            KeyCode key = _classListKeyParsed;
            bool down = Input.GetKeyDown(key);
            bool held = Input.GetKey(key);

            if (_classListModeKind == 1)            // 按住显示
            {
                _classListHeld = held;
            }
            else if (_classListModeKind == 2)       // 按下切换
            {
                if (down) _classListToggled = !_classListToggled;
            }
            else                                    // 按下显示 5 秒
            {
                if (down) _classListPressUntil = Time.time + ClassListPressDuration;
            }
        }

        /// <summary>
        /// 刷新「职业显示」按键与模式的解析缓存：仅在对应的配置字符串发生变化时重算，
        /// 避免每帧重复执行 <see cref="System.Enum.TryParse{T}"/> 与两次字符串 <c>Contains</c>。
        /// 分类规则与旧实现完全一致：先判「按住」，再判「切换」，其余按「按下显示」。
        /// </summary>
        private void RefreshClassListInputCache()
        {
            string key = ModConfig.General.ClassListKey.Value;
            if (!string.Equals(_classListKeyRaw, key))
            {
                _classListKeyRaw = key;
                _classListKeyParsed = ParseKey(key);
            }

            string mode = ModConfig.General.ClassListMode.Value ?? string.Empty;
            if (!string.Equals(_classListModeRaw, mode))
            {
                _classListModeRaw = mode;
                // 中文配置值为 按住显示 / 按下显示 / 按下切换；英文配置值为 HoldToShow / PressToShow / ToggleOnPress。
                string lower = mode.ToLowerInvariant();
                _classListModeKind = (mode.Contains("按住") || lower.Contains("hold")) ? 1
                    : (mode.Contains("切换") || lower.Contains("toggle")) ? 2 : 0;
            }
        }

        /// <summary>把配置里的按键名解析为 KeyCode；无法解析时退回 Tab。</summary>
        private static KeyCode ParseKey(string text)
        {
            if (!string.IsNullOrEmpty(text) &&
                System.Enum.TryParse(text.Trim(), true, out KeyCode key))
            {
                return key;
            }
            return KeyCode.Tab;
        }

        private bool ClassListVisible()
        {
            RefreshClassListInputCache();
            if (_classListModeKind == 1) return _classListHeld;
            if (_classListModeKind == 2) return _classListToggled;
            return Time.time < _classListPressUntil;
        }

        /// <summary>
        /// 取「本机所见的」某角色职业数值：主机读本局账本，客机读主机广播过来的职业表。
        /// 客机模式（未收到该玩家数据时）返回 0 =「无职业」。
        /// </summary>
        private int GetLocalClassCode(Character character)
        {
            if (PhotonNetwork.IsMasterClient) return (int)_ledger.GetClass(character);

            var owner = character != null && character.photonView != null ? character.photonView.Owner : null;
            if (owner != null && _remoteClassMap.TryGetValue(owner.ActorNumber, out int code)) return code;
            return 0;
        }

        /// <summary>
        /// 按 <b>ActorNumber</b> 取「本机所见的」职业数值（供第三方头顶id显示模组兼容层使用：
        /// 那些模组手里只有 ActorNumber，没有角色引用）。无分配 / 未收到时返回 0。
        /// </summary>
        public int GetLocalClassCodeByActor(int actor)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // 直接遍历全局角色表（不额外分配 List），按 owner 的 ActorNumber 命中后读账本缓存。
                var all = Character.AllCharacters;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all[i];
                        if (c == null || c.photonView == null || c.photonView.Owner == null) continue;
                        if (c.photonView.Owner.ActorNumber != actor) continue;
                        return (int)_ledger.GetClass(c);
                    }
                }
                return 0;
            }

            return _remoteClassMap.TryGetValue(actor, out int remote) ? remote : 0;
        }

        /// <summary>
        /// 职业数值 → 职业名：**优先用主机广播来的名称**，本地表兜底。
        ///
        /// 这样即使本机装的是「比主机旧的」模组版本，也仍能正确显示主机新加的职业名（而不是「无职业」）。
        /// </summary>
        private string RemoteClassNameOf(int code)
        {
            if (_remoteClassNames.TryGetValue(code, out var name) && !string.IsNullOrEmpty(name)) return name;
            return ClassLedger.Describe((ClassType)code);
        }

        /// <summary>按 ActorNumber 取职业名（空字符串 = 无职业 / 未分配），供第三方头顶id显示模组兼容层使用。</summary>
        public string GetLocalClassNameByActor(int actor)
        {
            int code = GetLocalClassCodeByActor(actor);
            return code == 0 ? string.Empty : RemoteClassNameOf(code);
        }

        /// <summary>
        /// 构造「当前职业分配情况」的每个玩家一行文本（形如「昵称：职业」，含「无职业」）。
        /// 主机屏幕面板与发往客机的广播（<see cref="PublishClassSnapshot"/>）、
        /// 以及客机侧的接收面板共用同一套措辞，保证显示内容一致。
        /// </summary>
        private List<string> BuildClassListLines()
        {
            var lines = new List<string>();

            if (PhotonNetwork.IsMasterClient)
            {
                foreach (var character in HostApi.GetActiveCharacters())
                {
                    lines.Add($"{Nick(character)}：{ClassLedger.Describe(_ledger.GetClass(character))}");
                }
            }
            else
            {
                // 客机：按主机广播的 ActorNumber → 昵称（与主机的 PhotonView.Owner.NickName 完全一致）
                var room = PhotonNetwork.CurrentRoom;
                foreach (var kv in _remoteClasses)
                {
                    var player = room != null ? room.GetPlayer(kv.Key) : null;
                    string name = player != null && !string.IsNullOrEmpty(player.NickName)
                        ? player.NickName : $"#{kv.Key}";
                    lines.Add($"{name}：{RemoteClassNameOf(kv.Value)}");
                }
            }
            return lines;
        }

        /// <summary>把「当前职业分配情况」广播给安装了本模组 / 附属模组（RollOfTheScoutClient）的客机。</summary>
        private void PublishClassSnapshot()
        {
            if (_ledger.Count == 0) return;

            var characters = HostApi.GetActiveCharacters();
            if (characters.Count == 0) return;

            var pairs = new List<KeyValuePair<int, int>>(characters.Count);
            var names = new List<KeyValuePair<int, string>>();
            var seenCodes = new HashSet<int>();
            foreach (var character in characters)
            {
                var owner = character.photonView != null ? character.photonView.Owner : null;
                if (owner == null) continue;

                int code = (int)_ledger.GetClass(character);
                pairs.Add(new KeyValuePair<int, int>(owner.ActorNumber, code));

                // 顺带把「出现过的职业数值 → 职业名」也发出去：客机端因此不需要内置职业名表，
                // 将来主模组新增职业时客机（含只装主模组、或装附属模组）无需更新即能正确显示。
                if (seenCodes.Add(code))
                    names.Add(new KeyValuePair<int, string>(code, ClassLedger.Describe((ClassType)code)));
            }
            if (pairs.Count == 0) return;

            ClassDisplayNet.BroadcastClassSnapshot(pairs);
            ClassDisplayNet.BroadcastClassNames(names);
        }

        // ---------------------------------------------------------------
        // 客机接收模式：接收主机广播的职业分配 / 提示，并驱动本地显示
        // ---------------------------------------------------------------

        /// <summary>Photon 自定义事件回调（<c>ClassDisplayNet.EventCode</c>）。仅客机模式处理。</summary>
        void Photon.Realtime.IOnEventCallback.OnEvent(ExitGames.Client.Photon.EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != ClassDisplayNet.EventCode) return;
            if (PhotonNetwork.IsMasterClient) return;   // 主机一律用本地账本，不处理广播

            // 只接受来自主机（MasterClient）的事件，避免误信其它客户端。
            var master = PhotonNetwork.MasterClient;
            if (master == null || photonEvent.Sender != master.ActorNumber) return;

            try
            {
                if (!(photonEvent.CustomData is object[] arr) || arr.Length < 3) return;
                if (!(arr[0] is string id) || id != ClassDisplayNet.ModId) return;

                byte kind = System.Convert.ToByte(arr[1]);
                string data = arr[2] as string;
                if (string.IsNullOrEmpty(data)) return;

                if (kind == ClassDisplayNet.KindClassSnapshot)
                {
                    _remoteClasses.Clear();
                    _remoteClassMap.Clear();
                    foreach (var kv in ClassDisplayNet.ParseSnapshot(data))
                    {
                        _remoteClasses.Add(kv);
                        _remoteClassMap[kv.Key] = kv.Value;
                    }
                    _hasRemoteSnapshot = _remoteClasses.Count > 0;
                }
                else if (kind == ClassDisplayNet.KindClassNames)
                {
                    // 职业名对照表：新版主模组新增职业时，本机（客机模式）无需更新即可正确显示其名称。
                    foreach (var kv in ClassDisplayNet.ParseClassNames(data))
                        _remoteClassNames[kv.Key] = kv.Value;
                }
                else if (kind == ClassDisplayNet.KindToast)
                {
                    // 提示复用主机端同一套 Toast 队列与绘制（客机自身不会调用 ShowToast，
                    // 因此这里加与主机完全一致的 8 秒 / 最多 5 条规则）。
                    if (_toasts.Count >= ToastMaxCount)
                    {
                        _toasts.RemoveAt(0);
                        _toastExpiries.RemoveAt(0);
                    }
                    _toasts.Add(data);
                    _toastExpiries.Add(Time.time + ToastDuration);
                    _toastVersion++;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 处理职业显示广播时异常：{e.Message}");
            }
        }

        /// <summary>
        /// 「头顶职业前缀」每帧回调（由 <see cref="Patches.DisplayNamePatch"/> 调用）。
        /// 主机与客机都能用：职业来源由 <see cref="GetLocalClassCode"/> 决定。
        /// </summary>
        public void OnOverheadNameTick(UIPlayerNames ui, int index)
        {
            if (!ModConfig.General.NamePrefix.Value) return;
            if (ui == null) return;

            var arr = ui.playerNameText;
            if (arr == null || index < 0 || index >= arr.Length) return;

            var pn = arr[index];
            if (pn == null) return;

            var interactible = pn.characterInteractable;
            var character = interactible != null ? interactible.character : null;
            if (character == null) return;

            OverheadNamePrefix.Apply(pn, character, OverheadClassNameOf);
        }

        /// <summary>
        /// 命中「头顶职业前缀」用的职业名查询：主机读本局账本、客机读主机广播的名称表
        /// （<see cref="RemoteClassNameOf"/> 已含「优先用广播名、本地表兜底」逻辑）。
        /// 返回空字符串表示「无职业」。
        /// </summary>
        private string OverheadClassNameOf(Character character)
        {
            int code = GetLocalClassCode(character);
            return code == 0 ? string.Empty : RemoteClassNameOf(code);
        }

        private void OnGUI()
        {
            // 优先用「原生字体叠加层」绘制（与游戏同款 TMP 字体）；
            // 字体尚未解析出来时，回退到旧的 IMGUI 自绘（Arial），保证功能不中断。
            if (PeakNativeUi.NativeOverlay.Ready) return;

            DrawToasts();
            DrawClassList();
        }

        /// <summary>
        /// 把「职业效果提示」与「职业分配情况」推送到原生字体叠加层（每帧在 Update 里调用）。
        /// 叠加层未就绪时什么都不做，由 <see cref="OnGUI"/> 回退绘制。
        /// </summary>
        private void PushNativeOverlay()
        {
            if (!PeakNativeUi.NativeOverlay.Ensure()) return;

            // Toast：带内容版本号推送 —— 内容未变时连字符串拼接都不做（消除每帧 GC 抖动）。
            PeakNativeUi.NativeOverlay.SetToast(_toasts, _toastVersion);

            // 面板：仅在「已按键显示 + 模组启用 + 有可显示数据」时显示。
            bool visible = ClassListVisible()
                && ModConfig.General.Enabled.Value
                && (PhotonNetwork.IsMasterClient ? _ledger.Count > 0 : _hasRemoteSnapshot);

            if (!visible)
            {
                // 不可见：走「只隐藏、不构建文本」的通道，避免每帧无谓的字符串分配。
                PeakNativeUi.NativeOverlay.SetPanelHidden();
                return;
            }

            var lines = BuildClassListLines();
            if (lines.Count == 0)
            {
                PeakNativeUi.NativeOverlay.SetPanelHidden();
                return;
            }

            PeakNativeUi.NativeOverlay.SetPanel(ClassListTitle(), lines, visible: true);
        }

        /// <summary>
        /// 职业分配面板的标题文本（含当前按键名）。按键名来自配置、几乎不变，
        /// 故缓存下来，避免每帧重新插值出一份内容相同的字符串。
        /// </summary>
        private string ClassListTitle()
        {
            string key = ModConfig.General.ClassListKey.Value;
            if (_classListTitleCache != null && string.Equals(_classListTitleCacheKey, key))
                return _classListTitleCache;

            _classListTitleCacheKey = key;
            return _classListTitleCache = $"[职业分配] 按 {key} 显示";
        }

        private void DrawClassList()
        {
            if (!ClassListVisible()) return;
            if (!ModConfig.General.Enabled.Value) return;

            if (PhotonNetwork.IsMasterClient)
            {
                if (_ledger.Count == 0) return;
            }
            else
            {
                if (!_hasRemoteSnapshot) return;   // 客机模式：尚未收到主机广播，暂不显示
            }

            var lines = BuildClassListLines();
            if (lines.Count == 0) return;

            if (_classListStyle == null)
            {
                int size = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 48f));
                _classListTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                _classListTitleStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
                _classListStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = TextAnchor.MiddleLeft,
                };
                _classListStyle.normal.textColor = Color.white;
            }

            lines.Insert(0, ClassListTitle());

            float w = Screen.width * 0.32f;
            float lineH = _classListStyle.fontSize * 1.6f;
            float h = lineH * lines.Count + 12f;
            var box = new Rect(Screen.width - w - 12f, 12f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = new Rect(box.x + 10f, box.y + 6f + lineH * i, box.width - 20f, lineH);
                GUI.Label(line, lines[i], i == 0 ? _classListTitleStyle : _classListStyle);
            }
        }

        private void DrawToasts()
        {
            if (_toasts.Count == 0) return;

            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 48f)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                _toastStyle.normal.textColor = Color.white;
            }

            float w = Screen.width * 0.45f;
            float lineH = _toastStyle.fontSize * 1.6f;
            float h = lineH * _toasts.Count + 12f;
            var box = new Rect(12f, 12f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < _toasts.Count; i++)
            {
                var line = new Rect(box.x + 10f, box.y + 6f + lineH * i, box.width - 20f, lineH);
                GUI.Label(line, _toasts[i], _toastStyle);
            }
        }

        // ---------------------------------------------------------------

        /// <summary>
        /// 「失去行动能力」：真正死亡（data.dead）或昏迷倒地（data.fullyPassedOut）。
        /// 仅用于「定时类效果是否该作用于该玩家」的门控（如高代谢、士气的受罚对象筛选）。
        /// </summary>
        private static bool IsDead(Character character)
        {
            if (character == null || character.data == null) return false;
            return character.data.dead || character.data.fullyPassedOut;
        }

        /// <summary>
        /// 角色当前是否为「骷髅形态」。
        /// 直接读取原版 <c>CharacterData.isSkeleton</c>：该值由 <c>RPC_SyncSkeleton</c> 同步到
        /// <c>RpcTarget.All</c>，因此主机上每个角色（含客机）的副本都是准确的，模组无需自行跟踪。
        /// </summary>
        private static bool IsSkeleton(Character character)
        {
            return character != null && character.data != null && character.data.isSkeleton;
        }

        /// <summary>场上处于「骷髅形态」的玩家数量（亡灵效果的计数依据）。</summary>
        private static int CountSkeletonPlayers(List<Character> characters)
        {
            int count = 0;
            if (characters == null) return 0;
            foreach (var character in characters)
            {
                if (IsSkeleton(character)) count++;
            }
            return count;
        }

        /// <summary>
        /// 「真正死亡」：只判定 data.dead，**不含昏迷（fullyPassedOut）**。
        ///
        /// 必须与 <see cref="IsDead"/> 区分开：昏迷只是倒地待救，并未死亡。
        /// 死亡/复活事件（共情、领袖士气、复活继承）只能用本判定，
        /// 否则昏迷/被救起会被误当成死亡/复活（实测问题 2 / 5 / 6 的根因）。
        /// </summary>
        private static bool IsTrueDead(Character character)
        {
            if (character == null || character.data == null) return false;
            return character.data.dead;
        }

        // ===============================================================
        // 区（Qu）：【！？区区？！】/【分解者】/【区块链】/【好多区】/【区毒不食子】
        // ===============================================================

        /// <summary>
        /// 该 itemID 是否为【分解者】物品池中的物品（命中时同时给出预制体名，供日志使用）。
        /// 按 itemID 从 ItemDatabase 反查预制体名，避免实例对象名带 (Clone) 后缀的干扰。
        /// 另外，凡预制体名以 `GuidebookPage` 开头的物品（卷轴 + 全部撕下的书页）**一律命中**，
        /// 不依赖配置清单（真实纸页名随版本变化，见方法内注释）。
        /// </summary>
        public bool IsDecomposerItem(ushort itemID, out string itemName)
        {
            itemName = null;
            string raw = ModConfig.Qu.DecomposerItems.Value;
            if (raw != _quDecomposerRaw)
            {
                _quDecomposerRaw = raw;
                _quDecomposerItems.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _quDecomposerItems.Add(name);
            }
            if (!ItemDatabase.TryGetItem(itemID, out Item prefab) || prefab == null) return false;
            itemName = prefab.gameObject.name;

            if (_quDecomposerItems.Contains(itemName)) return true;

            // 撕下的书页（含卷轴使用后生成的纸页）统一按前缀命中：
            // 真实预制体名形如 `GuidebookPage_<编号>_<名称>`（实测 GuidebookPage_0_Intro / GuidebookPage_3_Revival /
            // GuidebookPage_10_Sleepy / GuidebookPage_7_BurningSun …），各版本的编号与名称都会变，
            // 逐一列举并不可靠（旧版参考表里的名字已被证明对不上），故不依赖「分解者-物品池」清单。
            return itemName.StartsWith(ItemNames.GuidebookPagePrefix, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 【分解者】被触发（由 <see cref="Patches.QuDecomposerPickupPatch"/> 在物品被销毁后调用）：
        /// 自身饥饿下降（负向下发），并累计触发次数；累计到【好多区】「触发次数」的整数倍时发放一只早起虫儿。
        /// </summary>
        public void OnQuDecomposed(Character qu)
        {
            if (!_inRun || qu == null) return;

            float hunger = ModConfig.Qu.DecomposerHunger.Value;
            if (hunger > 0f)
            {
                HostApi.ApplyStatus(qu, CharacterAfflictions.STATUSTYPE.Hunger, -hunger);
            }

            string id = _ledger.IdOf(qu);
            if (string.IsNullOrEmpty(id)) return;

            _quDecomposedCount.TryGetValue(id, out int count);
            count++;
            _quDecomposedCount[id] = count;

            int need = Mathf.Max(0, ModConfig.Qu.ManyQuTriggerCount.Value);
            if (!ModConfig.Qu.ManyQuEnabled.Value || need <= 0) return;
            if (count % need != 0) return;

            GrantQuWorm(qu, $"好多区（第 {count} 次分解）");
        }

        /// <summary>发放一只早起虫儿给指定玩家：优先放入 3 个物品栏空位，全满则在脚下生成。</summary>
        private void GrantQuWorm(Character qu, string reason)
        {
            if (qu == null) return;

            if (HostApi.GiveItemToInventorySlots(qu, ItemNames.EarlyWorm, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(qu)} 获得 1 只早起虫儿。");
                return;
            }
            if (HostApi.SpawnItemAtFeet(qu, ItemNames.EarlyWorm) != null)
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(qu)} 物品栏已满，早起虫儿改在脚下生成。");
                return;
            }
            Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：{Nick(qu)} 的早起虫儿发放失败（物品栏已满且脚下生成失败）。");
        }

        /// <summary>
        /// 区·物品食用（【区毒不食子】+【区块链】喂食）：由 <see cref="HandleItemConsumed"/>
        /// （全局食用事件，所有客户端含主机均触发）调用。
        ///
        /// 1. 【区毒不食子】：区食用早起虫儿（无论是否烤熟 —— 烤制只改 CookedAmount 数据，预制体名不变）→ 自身 +诅咒。
        /// 2. 【区块链】：区**喂**其他玩家食用早起虫儿 → 被喂玩家额外 +孢子。
        ///    喂食者取 <c>item.trueHolderCharacter</c>（物品真正的持有者；喂食时被喂者只是 overrideHolderCharacter），
        ///    它与消费者不同即代表「这是一次喂食」而非自己吃。
        /// </summary>
        public void OnQuItemConsumed(Item item, Character consumer)
        {
            if (!_inRun || item == null || consumer == null) return;

            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            if (prefab.gameObject.name != ItemNames.EarlyWorm) return;

            // 1. 区毒不食子：区吃虫儿 → +诅咒
            float toxin = ModConfig.Qu.ToxinCurse.Value;
            if (ModConfig.Qu.ToxinEnabled.Value && toxin > 0f && _ledger.GetClass(consumer) == ClassType.Qu)
            {
                HostApi.ApplyStatus(consumer, CharacterAfflictions.STATUSTYPE.Curse, toxin);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 区毒不食子：{Nick(consumer)} 食用了早起虫儿，诅咒+{toxin * 100f:0.#}");
            }

            // 2. 区块链：区喂其他玩家吃虫儿 → 被喂玩家 +孢子
            float feedSpore = ModConfig.Qu.BlockchainFeedSpore.Value;
            if (!ModConfig.Qu.BlockchainEnabled.Value || feedSpore <= 0f) return;

            var feeder = item.trueHolderCharacter;
            if (feeder == null || feeder == consumer) return;
            if (_ledger.GetClass(feeder) != ClassType.Qu) return;

            HostApi.ApplyStatus(consumer, CharacterAfflictions.STATUSTYPE.Spores, feedSpore);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 区块链：{Nick(feeder)} 喂 {Nick(consumer)} 食用早起虫儿，被喂者孢子+{feedSpore * 100f:0.#}");
        }

        // ---------------------------------------------------------------
        // 区·定时效果驱动
        // ---------------------------------------------------------------

        private void TickQu(List<Character> characters)
        {
            if (!ModConfig.Qu.Enabled.Value) return;

            // ---- ！？区区？！·攀爬湿滑 ----
            TickQuClimb(characters);

            // ---- 区块链：定时结算（自身清孢子 / 同伴加孢子 / 传染判定）----
            if (ModConfig.Qu.BlockchainEnabled.Value)
            {
                _quBlockchainTimer += Time.deltaTime;
                float blockchainInterval = Mathf.Max(1f, ModConfig.Qu.BlockchainInterval.Value);
                if (_quBlockchainTimer >= blockchainInterval)
                {
                    _quBlockchainTimer = 0f;
                    DoQuBlockchain(characters);
                }
            }

            // ---- 好多区：定时结算 ----
            if (ModConfig.Qu.ManyQuEnabled.Value)
            {
                _quManyTimer += Time.deltaTime;
                float manyInterval = Mathf.Max(1f, ModConfig.Qu.ManyQuInterval.Value);
                if (_quManyTimer >= manyInterval)
                {
                    _quManyTimer = 0f;
                    DoQuMany(characters);
                }
            }
        }

        /// <summary>
        /// 【！？区区？！】攀爬湿滑：区在攀爬（抓墙 / 绳索 / 藤蔓）期间，主机周期性**强制中断攀爬**，
        /// 由「必须重新抓墙、把同一段高度再爬一次」来**等效实现「攀爬体力消耗增加」**。
        ///
        /// ⚠ 为什么不是字面的「速度减慢」：原版攀爬速度与「攀爬体力消耗倍率」都在**攀爬者自己的客户端**上、
        /// 按本机射线命中的 `ClimbModifierSurface` 结算（`data.climbMod` **不网络同步**、每帧被属主覆盖），
        /// 主机**无法改写** → **「速度减慢」已放弃实现**（见 §8.81 取证）。
        ///
        /// 强制方式为**脱手**：只调 <see cref="InterruptClimbing"/>（发 `StopClimbingRpc` / `StopRopeClimbingRpc` /
        /// `StopVineClimbingRpc`，均 `[PunRPC]`、无归属权校验）打断攀爬，不播放任何表情。
        ///
        /// 计时只在「正在攀爬」时累计：一旦被中断（`isClimbing` 变假）就清零，等玩家重新抓墙并再爬满
        /// `攀爬-间隔` 才会再次触发 —— 因此不会在脱手后空转，也天然形成「爬一段 → 掉下去 → 重爬」的循环。
        /// 「攀爬-间隔」设为 0 或负数即关闭本效果。
        ///
        /// 豁免（配置 `攀爬-忽略攀爬物`，默认 true）：**只对「自然岩壁」生效**，借助已放置 / 已挂载的攀爬物时**不脱手**——
        /// 绳索 / 反重绳索 / 锁链（`Rope` → `isRopeClimbing`）、藤蔓（`JungleVine` → `isVineClimbing`）、
        /// 岩钉（锤入后 `ShittyPiton`，含镐 → 挂在 `ClimbHandle`）。设 false 即退回「任何攀爬都脱手」的旧行为。
        /// </summary>
        private void TickQuClimb(List<Character> characters)
        {
            if (!ModConfig.Qu.ClimbEnabled.Value) return;

            float interval = ModConfig.Qu.ClimbInterval.Value;
            if (interval <= 0f) return;

            foreach (var qu in characters)
            {
                if (_ledger.GetClass(qu) != ClassType.Qu) continue;

                string id = _ledger.IdOf(qu);
                if (string.IsNullOrEmpty(id)) continue;

                // 借助「已放置 / 已挂载的攀爬物」时豁免（配置 `攀爬-忽略攀爬物`，默认 true）：
                //   绳索 / 反重绳索 / 锁链 → 抓的是 `Rope` 组件 → `isRopeClimbing`
                //   藤蔓（丛林藤 / 藤蔓发射器）→ `JungleVine` 组件 → `isVineClimbing`
                //   岩钉（ClimbingSpike 锤入后变成 ShittyPiton，含镐）→ 悬挂在 `ClimbHandle`（`currentClimbHandle`）
                bool onClimbAid = qu.data != null
                    && (qu.data.isRopeClimbing || qu.data.isVineClimbing || qu.data.currentClimbHandle != null);
                bool climbing = qu.data != null
                    && (qu.data.isClimbing || qu.data.isRopeClimbing || qu.data.isVineClimbing)
                    && !(ModConfig.Qu.ClimbIgnoreAids.Value && onClimbAid);

                if (!climbing || IsDead(qu))
                {
                    _quClimbDrainTimer.Remove(id);
                    continue;
                }

                _quClimbDrainTimer.TryGetValue(id, out float elapsed);
                elapsed += Time.deltaTime;
                if (elapsed < interval)
                {
                    _quClimbDrainTimer[id] = elapsed;
                    continue;
                }
                // 触发后清零：被打断即不再攀爬，下面的分支会把计时移除；重新抓墙后重新累计
                _quClimbDrainTimer[id] = 0f;

                if (InterruptClimbing(qu))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 区区？！：{Nick(qu)} 攀爬中（雨林湿滑），强制脱手。");
                }
            }
        }

        /// <summary>
        /// 【区块链】每「区块链-间隔」秒（默认 7）结算一次：
        ///   ① 每名存活区：为**自身**清除孢子（默认 1 = 100 点，足以清空）；
        ///   ② 半径内存活区 ≥ 1 名 的非区玩家：+孢子（默认 0.025 = 2.5 点）——
        ///      **多名区不叠加**：只要「10 米内有任意存活区」就结算一次，
        ///      而不是按区数量反复叠加（实现上对每名非区玩家各自判定一次「半径内有无区」）；
        ///   ③ 传染判定：半径内孢子达到阈值（默认 0.5 = 50 点）的玩家被**强制转职成区**
        ///      （绕过最大人数限制，与待皓·模因污染同法），成功后若「区块链-奖励虫儿」开启，
        ///      则让场上所有存活区各获得 1 只早起虫儿。
        /// 说明：传染判定在「加孢子之前」按当前值进行，避免刚加完 2.5 点就被判为过量。
        /// </summary>
        private void DoQuBlockchain(List<Character> characters)
        {
            float radius = Mathf.Max(0f, ModConfig.Qu.BlockchainRadius.Value);
            float selfClear = ModConfig.Qu.BlockchainSelfSporeClear.Value;
            float allySpore = ModConfig.Qu.BlockchainAllySpore.Value;
            float threshold = ModConfig.Qu.BlockchainConvertThreshold.Value;
            bool convertEnabled = ModConfig.Qu.BlockchainConvertEnabled.Value;

            List<Character> converted = null;

            // ① 每名存活区各自清除自身孢子（与「作用于他人」无关，先单独结算）
            foreach (var qu in characters)
            {
                if (_ledger.GetClass(qu) != ClassType.Qu) continue;
                if (IsDead(qu)) continue;

                if (selfClear > 0f)
                    HostApi.ApplyStatus(qu, CharacterAfflictions.STATUSTYPE.Spores, -selfClear);
            }

            // ② 对每名非区玩家：半径内有存活区就结算一次（多名区不叠加）
            foreach (var other in characters)
            {
                if (other == null) continue;
                if (_ledger.GetClass(other) == ClassType.Qu) continue;   // 只作用于「除区以外的所有玩家」
                if (IsDead(other)) continue;
                if (!HasAliveQuWithin(other, radius)) continue;

                // 圣人·超然物外（第 85 批扩展）：免疫【区块链】—— 不会被选为目标（既不染孢子也不会被传染转职）
                if (IsTranscendent(other))
                {
                    if (_transcendentBlockchainLogged.Add(_ledger.IdOf(other) ?? Nick(other)))
                    {
                        Plugin.Log.LogInfo($"[RollOfTheScout] 超然物外：{Nick(other)} 免疫区·区块链（不会被选为目标）。");
                    }
                    continue;
                }

                // ③ 传染：孢子已达阈值 → 强制转职成区（本轮不再为其加孢子）
                if (convertEnabled && threshold > 0f
                    && HostApi.GetStatus(other, CharacterAfflictions.STATUSTYPE.Spores) >= threshold
                    && TryQuInfect(other))
                {
                    if (converted == null) converted = new List<Character>();
                    converted.Add(other);
                    continue;
                }

                if (allySpore > 0f)
                    HostApi.ApplyStatus(other, CharacterAfflictions.STATUSTYPE.Spores, allySpore);
            }

            if (converted == null || !ModConfig.Qu.BlockchainRewardWorm.Value) return;

            foreach (var qu in characters)
            {
                if (_ledger.GetClass(qu) != ClassType.Qu) continue;
                if (IsDead(qu)) continue;
                GrantQuWorm(qu, "区块链传染奖励");
            }
        }

        /// <summary>
        /// 【区块链】辅助判定：<paramref name="target"/> 的「区块链-作用半径」内是否存在存活的区。
        /// 用于实现「多名区不叠加」——每名非区玩家每轮只结算一次。
        /// </summary>
        private bool HasAliveQuWithin(Character target, float radius)
        {
            var near = HostApi.GetCharactersInRadius(target.Center, radius);
            foreach (var nb in near)
            {
                if (nb == null || nb == target) continue;
                if (IsDead(nb)) continue;
                if (_ledger.GetClass(nb) == ClassType.Qu) return true;
            }
            return false;
        }

        /// <summary>
        /// 【区块链】把孢子过量的玩家强制转职成区：绕过「最大人数」限制（与待皓·模因污染同法，
        /// <c>ClassLedger.ForceAssign</c>），转职成功后清空负面状态并按新职业重发开局类效果
        /// （<see cref="ApplyClassChange"/>）。返回是否发生了转职。
        /// </summary>
        private bool TryQuInfect(Character target)
        {
            if (target == null) return false;

            string id = _ledger.IdOf(target);
            if (string.IsNullOrEmpty(id)) return false;

            ClassType oldType = _ledger.GetClass(target);
            if (oldType == ClassType.Qu) return false;
            if (!_ledger.ForceAssign(target, ClassType.Qu)) return false;

            ApplyClassChange(target, id, oldType, ClassType.Qu);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 区块链：{Nick(target)} 孢子过量，职业 {ClassLedger.Describe(oldType)} → 区。");
            ShowToast($"{Nick(target)} 区块链：{ClassLedger.Describe(oldType)} → 区");
            return true;
        }

        /// <summary>
        /// 【好多区】每「好多区-间隔」秒结算一次：
        ///   层数 = 自身 3 个物品栏中的早起虫儿数 + 自身半径内（不含自己，半径见「好多区-区半径」）的存活区数，
        ///   最多按「好多区-最大层数」封顶；每 1 层为自身 -饥饿、+额外体力；
        ///   物品栏虫儿数 ≥「加速虫儿数」或**自身「好多区-阈值半径」内存活区人数（含自己）** ≥「加速区数」→ 额外一次加速；
        ///   自身「好多区-阈值半径」内存活区人数（含自己）≥「无限体力区数」→ 再额外一次无限体力。
        /// </summary>
        private void DoQuMany(List<Character> characters)
        {
            float quRadius = Mathf.Max(0f, ModConfig.Qu.ManyQuQuRadius.Value);
            float thresholdRadius = Mathf.Max(0f, ModConfig.Qu.ManyQuThresholdRadius.Value);
            float hunger = ModConfig.Qu.ManyQuHunger.Value;
            float stamina = ModConfig.Qu.ManyQuStamina.Value;
            int maxStacks = ModConfig.Qu.ManyQuMaxStacks.Value;

            int speedWorms = ModConfig.Qu.ManyQuSpeedWormCount.Value;
            int speedQu = ModConfig.Qu.ManyQuSpeedQuCount.Value;
            int infiniteQu = ModConfig.Qu.ManyQuInfiniteQuCount.Value;

            foreach (var qu in characters)
            {
                if (_ledger.GetClass(qu) != ClassType.Qu) continue;
                if (IsDead(qu)) continue;

                int worms = CountItemInInventory(qu, ItemNames.EarlyWorm);
                int nearbyQu = CountAliveQuWithin(qu, characters, quRadius, excludeSelf: true);
                int total = worms + nearbyQu;
                // 层数上限：超过「好多区-最大层数」后按上限封顶（0 或负数 = 不限制）。
                int layers = total;
                if (maxStacks > 0 && layers > maxStacks) layers = maxStacks;

                if (layers > 0)
                {
                    if (hunger > 0f)
                        HostApi.ApplyStatus(qu, CharacterAfflictions.STATUSTYPE.Hunger, -hunger * layers);
                    if (stamina > 0f)
                        HostApi.BoostExtraStamina(qu, stamina * layers);
                }

                // 「加速 / 无限体力」阈值判据：自身「好多区-阈值半径」内存活区人数（**含自己**，不再是全场计数）。
                int localQu = CountAliveQuWithin(qu, characters, thresholdRadius, excludeSelf: false);

                if ((speedWorms > 0 && worms >= speedWorms) || (speedQu > 0 && localQu >= speedQu))
                {
                    GrantSprint(qu, "好多区", clearDrowsyAfter: true);
                }
                if (infiniteQu > 0 && localQu >= infiniteQu)
                {
                    GrantInfiniteStamina(qu, "好多区");
                }

                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 好多区：{Nick(qu)} 物品栏虫儿 {worms} 只、附近区 {nearbyQu} 名、判定区 {localQu} 名（含自己）→ " +
                    $"层数 {layers}（上限 {maxStacks}）、饥饿-{hunger * layers * 100f:0.#}、额外体力+{stamina * layers * 100f:0.#}。");
            }
        }

        /// <summary>统计「自身半径内」的存活区数量（<paramref name="excludeSelf"/> 为 true 时不含自己）。</summary>
        private int CountAliveQuWithin(Character self, List<Character> characters, float radius, bool excludeSelf)
        {
            int n = 0;
            if (self == null || characters == null) return 0;

            foreach (var c in characters)
            {
                if (c == null) continue;
                if (excludeSelf && c == self) continue;
                if (_ledger.GetClass(c) != ClassType.Qu) continue;
                if (IsDead(c)) continue;
                if (radius >= 0f && Vector3.Distance(c.Center, self.Center) > radius) continue;
                n++;
            }
            return n;
        }

        /// <summary>
        /// 【！？区区？！】区真正死亡时，在尸体处生成一只**烤熟**的早起虫儿。
        /// 「烤熟」= 预制体仍是 <c>EarlyWorm</c>，但把 <c>CookedAmount</c> 置 1（烤制只改该数据、不改预制体名），
        /// 并广播原版 <c>SetCookedAmountRPC</c>（<c>RpcTarget.All</c>）让各客户端都把它显示为烤熟的虫儿。
        /// 生成位置复用 <see cref="HostApi.SpawnItemAtFeet"/>（内含「死亡哨兵坐标」防护，
        /// 避免角色尚未定位时把物品生成到 (0,5000,-5000) 这种永远拿不到的地方）。
        /// </summary>
        private void SpawnQuCookedWorm(Character qu)
        {
            if (!ModConfig.Qu.DeathWormEnabled.Value) return;
            if (qu == null) return;

            var go = HostApi.SpawnItemAtFeet(qu, ItemNames.EarlyWorm);
            if (go == null)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 区区？！：{Nick(qu)} 死亡后生成早起虫儿失败（位置未就绪或预制体缺失）。");
                return;
            }

            var item = go.GetComponent<Item>();
            if (item != null)
            {
                try
                {
                    item.photonView.RPC("SetCookedAmountRPC", RpcTarget.All, 1);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"[RollOfTheScout] 区区？！：把虫儿标记为「烤熟」时异常：{e.Message}");
                }
            }

            Plugin.Log.LogInfo($"[RollOfTheScout] 区区？！：{Nick(qu)} 死亡，尸体处生成了一只烤熟的早起虫儿。");
        }

        // ===============================================================
        // 太空人（Astronaut）：【轻飘飘】/【飞起来】/【太空食品安全】/【我去，老美的NASA这么厉害】
        // 【飞起来】/【轻飘飘】的拾取拦截见 Patches.AstronautPickupPatch（调用 OnAstronautPickup）。
        // ===============================================================

        /// <summary>
        /// 【轻飘飘】给目标角色绑上若干只气球：复刻原版 <c>Action_Balloon.RunAction</c> 的单只分支
        /// （<c>CharacterBalloons.TieNewBalloon(colorIndex)</c>），其内部
        /// <c>PhotonNetwork.Instantiate("TiedBalloon") + RPC_Init(RpcTarget.All)</c> 会让所有客户端
        /// （含客机）都看到气球绑在该玩家身上并影响其浮力 / 跳跃（与法师·天灾绑气球同法）。
        ///
        /// 颜色下标按该角色的气球色板长度随机取：<c>TiedBalloon.InitRoutine</c> 会直接
        /// <c>balloonColors[colorID]</c>，越界会抛异常。
        /// </summary>
        private void TieBalloons(Character character, int count, string reason)
        {
            if (character == null || character.refs == null || character.refs.balloons == null) return;
            if (count <= 0) return;

            var colors = character.refs.balloons.balloonColors;
            int palette = (colors != null && colors.Length > 0) ? colors.Length : 5;

            for (int i = 0; i < count; i++)
            {
                character.refs.balloons.TieNewBalloon(UnityEngine.Random.Range(0, palette));
            }

            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(character)} 身上绑了 {count} 只气球。");
        }

        /// <summary>
        /// 【轻飘飘】开局初始气球的幂等补发（每帧调用，直到成功）。
        /// 中途加入（灵魂待复活）者不发放 —— 与其它「开局类」效果一致，等复活到位后再给。
        /// </summary>
        private void EnsureAstronautBalloons(List<Character> characters)
        {
            if (!_ledger.HasClassInRoom(ClassType.Astronaut)) return;

            int initial = Mathf.Max(0, ModConfig.Astronaut.FloatInitialBalloons.Value);
            if (initial == 0) return;

            foreach (var character in characters)
            {
                if (_ledger.GetClass(character) != ClassType.Astronaut) continue;

                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;
                if (_astronautInitialBalloons.Contains(id)) continue;
                if (IsDead(character)) continue;

                TieBalloons(character, initial, "轻飘飘·开局");
                _astronautInitialBalloons.Add(id);
            }
        }

        /// <summary>【轻飘飘】周期绑气球 + 【NASA】定时削减炎热 / 寒冷。每帧由 TickTimers 调用。</summary>
        private void TickAstronaut(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 轻飘飘：每「轻飘飘-间隔」秒再绑一批气球 ----
            _astronautBalloonTimer += dt;
            float balloonInterval = ModConfig.Astronaut.FloatIntervalSeconds.Value;
            int perTick = Mathf.Max(0, ModConfig.Astronaut.FloatBalloonsPerTick.Value);
            if (balloonInterval > 0f && perTick > 0 && _astronautBalloonTimer >= balloonInterval)
            {
                _astronautBalloonTimer = 0f;
                foreach (var a in characters)
                {
                    if (_ledger.GetClass(a) != ClassType.Astronaut) continue;
                    if (IsDead(a)) continue;
                    TieBalloons(a, perTick, "轻飘飘");
                }
            }

            // ---- NASA：每「NASA-间隔」秒削减一次炎热 / 寒冷 ----
            _nasaTimer += dt;
            float nasaInterval = ModConfig.Astronaut.NasaIntervalSeconds.Value;
            if (nasaInterval > 0f && _nasaTimer >= nasaInterval)
            {
                _nasaTimer = 0f;
                float hot = ModConfig.Astronaut.NasaHotReduce.Value;
                float cold = ModConfig.Astronaut.NasaColdReduce.Value;
                if (hot != 0f || cold != 0f)
                {
                    foreach (var a in characters)
                    {
                        if (_ledger.GetClass(a) != ClassType.Astronaut) continue;
                        if (IsDead(a)) continue;
                        // 同一时刻对同一玩家削减炎热 + 寒冷：合并为单条 RPC（语义与逐条等价）
                        HostApi.ApplyStatusPair(a,
                            CharacterAfflictions.STATUSTYPE.Hot, -Mathf.Abs(hot),
                            CharacterAfflictions.STATUSTYPE.Cold, -Mathf.Abs(cold));
                    }
                }
            }
        }

        /// <summary>
        /// 【我去，老美的NASA这么厉害】的「额外加成」：对比上一轮快照，只要该太空人的
        /// 蛛网 / 捕蝇草 / 诅咒**有增加**，就额外追加同种状态「NASA-额外增加」（默认 2.5 点）。
        ///
        /// 实现与 M·享受 / 圣人·跌落神坛 同法（硬约束 #5：被动倍率只能靠「主机轮询同步值 + 施加补偿增量」）：
        /// 追加后把快照**后移**为「本轮值 + 追加值」（预期值），避免下一轮把自身追加的值误判成新的「获得」而无限自增。
        /// 负增量（净化 / 治疗）忽略；首次见到某角色只登记基线、不追加。
        /// </summary>
        private void PollAstronautExtraStatus(List<Character> characters)
        {
            float bonus = ModConfig.Astronaut.NasaExtraBonus.Value;
            if (bonus <= 0f) return;

            foreach (var character in characters)
            {
                if (character == null) continue;
                string id = _ledger.IdOf(character);
                if (string.IsNullOrEmpty(id)) continue;

                bool isAstronaut = _ledger.GetClass(character) == ClassType.Astronaut;

                bool had = _nasaBonusSnapshot.TryGetValue(id, out float[] prev) && prev != null
                    && prev.Length == NasaBonusTypes.Length;

                // 首次见到 / 刚换职业：只登记基线，不追加（数组常驻复用，避免每轮新分配）
                if (!had)
                {
                    prev = new float[NasaBonusTypes.Length];
                    for (int i = 0; i < NasaBonusTypes.Length; i++)
                        prev[i] = HostApi.GetStatus(character, NasaBonusTypes[i]);
                    _nasaBonusSnapshot[id] = prev;
                    continue;
                }

                for (int i = 0; i < NasaBonusTypes.Length; i++)
                {
                    float now = HostApi.GetStatus(character, NasaBonusTypes[i]);
                    float delta = now - prev[i];
                    if (isAstronaut && delta > 0f)
                    {
                        HostApi.ApplyStatus(character, NasaBonusTypes[i], bonus);
                        prev[i] = now + bonus;
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] NASA：{Nick(character)} 获得「{NasaBonusTypes[i]}」{delta * 100f:0.#} 点，" +
                            $"额外追加 {bonus * 100f:0.#} 点。");
                    }
                    else
                    {
                        prev[i] = now;
                    }
                }
            }
        }

        /// <summary>
        /// 【轻飘飘】/【飞起来】的拾取拦截（供 <see cref="Patches.AstronautPickupPatch"/> 调用）。
        /// 返回 true 表示「该世界物品应直接消失、跳过原版入包」。
        ///
        /// ①【轻飘飘】拾取「气球 Balloon」或「一束气球 BalloonBunch」直接消失；
        /// ②【飞起来】拾取非食物物品（或配置允许时的食物）按概率变为火箭背包 / 喷气背包：
        ///    命中时拾取物消失，并在脚下生成对应背包（<c>HostApi.SpawnItemAtFeet</c>）；生成失败则不销毁原物。
        /// </summary>
        public bool OnAstronautPickup(Item item, Character character)
        {
            if (!_inRun) return false;
            if (item == null || character == null) return false;
            if (_ledger.GetClass(character) != ClassType.Astronaut) return false;

            string name = PrefabNameOf(item);

            // ① 轻飘飘：拾取气球 / 一束气球直接消失
            if (ModConfig.Astronaut.FloatPickupDisappear.Value
                && (name == ItemNames.Balloon || name == ItemNames.BalloonBunch))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 轻飘飘：{Nick(character)} 拾取的 {name} 直接消失。");
                return true;
            }

            // ② 飞起来：非食物物品（或允许食物时）按概率变成背包
            if (!ModConfig.Astronaut.FlyEnabled.Value) return false;
            if (IsFlyBlacklisted(name)) return false;
            if (!ModConfig.Astronaut.FlyIncludeFood.Value && IsFoodItem(item)) return false;

            float rocket = Mathf.Clamp01(ModConfig.Astronaut.FlyRocketpackChance.Value);
            float jet = Mathf.Clamp01(ModConfig.Astronaut.FlyJetpackChance.Value);
            if (rocket <= 0f && jet <= 0f) return false;

            float roll = UnityEngine.Random.value;
            string target;
            if (rocket > 0f && roll < rocket) target = ItemNames.Rocketpack;
            else if (jet > 0f && roll < rocket + jet) target = ItemNames.Jetpack;
            else return false;

            var go = HostApi.SpawnItemAtFeet(character, target);
            if (go == null)
            {
                Plugin.Log.LogWarning(
                    $"[RollOfTheScout] 飞起来：{Nick(character)} 拾取的 {name} 本应变为 {target}，但脚下生成失败，保持原样。");
                return false;
            }

            Plugin.Log.LogInfo($"[RollOfTheScout] 飞起来：{Nick(character)} 拾取的 {name} 变为 {target}，已在脚下生成。");
            return true;
        }

        /// <summary>【飞起来】判定：该预制体名是否在「特殊物品黑名单」中（缓存解析）。</summary>
        private bool IsFlyBlacklisted(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return true;
            string raw = ModConfig.Astronaut.FlyBlacklist.Value;
            if (raw != _flyBlacklistRaw)
            {
                _flyBlacklistRaw = raw;
                _flyBlacklist.Clear();
                foreach (var n in WeightedPicker.ParseList(raw)) _flyBlacklist.Add(n);
            }
            return _flyBlacklist.Contains(prefabName);
        }

        /// <summary>
        /// 通用「食用黑名单」判定：给定**预制体名**是否落在逗号分隔的黑名单里（解析结果带缓存）。
        /// 用于榔王·纤维化 / 炊事员·创伤应激（野人那份是静态方法，单独缓存，见 <see cref="IsSavageForbiddenFood"/>）。
        /// </summary>
        private static bool NameListContains(string raw, ref string cacheRaw, HashSet<string> cacheSet, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (string.IsNullOrEmpty(raw)) return false;

            if (raw != cacheRaw)
            {
                cacheRaw = raw;
                cacheSet.Clear();
                foreach (var n in WeightedPicker.ParseList(raw)) cacheSet.Add(n);
            }
            return cacheSet.Contains(prefabName);
        }

        /// <summary>
        /// 取某个 <see cref="Item"/> 实例对应的**预制体名**（不是实例名）。
        /// 世界 / 背包里的物品都是 `PhotonNetwork.Instantiate` 出来的克隆体，实例名会带 `(Clone)` 后缀，
        /// 直接比名字不可靠；统一走 <c>ItemDatabase.TryGetItem(item.itemID, out prefab)</c> 反查（与转职 / 模因污染同法）。
        /// 反查失败时退化为实例名（仅作兜底，不影响标签类判定）。
        /// </summary>
        private static string PrefabNameOf(Item item)
        {
            if (item == null) return null;
            try
            {
                if (ItemDatabase.TryGetItem(item.itemID, out Item prefab) && prefab != null)
                    return prefab.gameObject.name;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 读取物品预制体名时异常：{e.Message}");
            }
            return item.gameObject.name;
        }

        /// <summary>
        /// 是否为「食物物品」：带 Berry（浆果）/ Mushroom（蘑菇）/ PackagedFood（包装食物）标签，
        /// 或烤过（<c>CookedAmount ≥ 1</c>）。口径与原版食物标签一致。
        /// </summary>
        private static bool IsFoodItem(Item item)
        {
            if (item == null) return false;
            try
            {
                var tags = item.itemTags;
                if (tags.HasFlag(Item.ItemTags.Berry)
                    || tags.HasFlag(Item.ItemTags.Mushroom)
                    || tags.HasFlag(Item.ItemTags.PackagedFood)) return true;

                var cooked = item.GetData<IntItemData>(DataEntryKey.CookedAmount);
                if (cooked != null && cooked.Value >= 1) return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 飞起来·读取物品标签时异常：{e.Message}");
            }
            return false;
        }

        /// <summary>【太空食品安全】判定：是否为「符合条件的食物」（包装食物或烤过的食物）。</summary>
        private static bool IsAllowedSpaceFood(Item item)
        {
            if (item == null) return false;
            try
            {
                if (item.itemTags.HasFlag(Item.ItemTags.PackagedFood)) return true;

                var cooked = item.GetData<IntItemData>(DataEntryKey.CookedAmount);
                if (cooked != null && cooked.Value >= 1) return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 太空食品安全·读取物品标签时异常：{e.Message}");
            }
            return false;
        }

        /// <summary>
        /// 【太空食品安全】：太空人只能食用「包装食物」与「烤熟的食物」，
        /// 每次食用不符合条件的食物 +「太空食品安全-中毒」（默认 10 点）。
        /// 判定与原版标签一致（PackagedFood / CookedAmount ≥ 1）；
        /// 黑名单「太空食品安全-黑名单」（默认：诡异菇 Mushroom Glow、万灵药 Cure-All）内的物品豁免 ——
        /// 前者是通用·转职触发物品（避免转职的同一口食物被扣中毒），后者为药剂。
        ///
        /// 调用来源有两个（互为备份，去重窗口见 <see cref="AstronautEatDedupWindow"/>）：
        /// 1. <c>Item.Consume</c> 的 Harmony 补丁（<see cref="Patches.AstronautEatPatch"/>）；
        /// 2. 游戏自身的全局事件 <c>GlobalEvents.OnItemConsumed</c>（见 <see cref="HandleItemConsumed"/>）。
        /// </summary>
        public void OnAstronautItemConsumed(Item item, Character character)
        {
            if (!_inRun) return;
            if (item == null || character == null) return;
            if (_ledger.GetClass(character) != ClassType.Astronaut) return;

            float poison = ModConfig.Astronaut.FoodSafetyPoison.Value;
            if (poison <= 0f) return;

            string name = PrefabNameOf(item);
            if (NameListContains(ModConfig.Astronaut.FoodSafetyBlacklist.Value,
                    ref _spaceFoodBlacklistRaw, _spaceFoodBlacklist, name)) return;
            if (IsAllowedSpaceFood(item)) return;

            string id = _ledger.IdOf(character);
            if (!string.IsNullOrEmpty(id))
            {
                if (_astronautEatGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
                _astronautEatGuardUntil[id] = Time.time + AstronautEatDedupWindow;
            }

            HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Poison, poison);
            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 太空食品安全：{Nick(character)} 食用了不符合条件的食物（{name}），中毒+{poison * 100f:0.#}");
        }

        // ===============================================================
        // 榔王（Langwang）：【精品大果】/【上瘾】/【纤维化】/【没吃的赶紧戒】
        // ===============================================================

        /// <summary>该预制体名是否属于配置的「荔莓物品」（默认王莓系列的绿 / 黄 / 紫王莓）。</summary>
        private bool IsLiBerry(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            string raw = ModConfig.Langwang.BerryItems.Value;
            if (raw != _langwangBerryRaw)
            {
                _langwangBerryRaw = raw;
                _langwangBerryItems.Clear();
                foreach (var n in WeightedPicker.ParseList(raw)) _langwangBerryItems.Add(n);
            }
            return _langwangBerryItems.Contains(prefabName);
        }

        /// <summary>【精品大果】：按权重发放 1 个荔莓（青 / 黄），优先放入 3 个物品栏空位，已满则脚下生成。</summary>
        private void GrantLangwangBerry(Character langwang)
        {
            var pool = new List<WeightedItem>(2);
            string green = ModConfig.Langwang.GreenBerryItem.Value;
            string yellow = ModConfig.Langwang.YellowBerryItem.Value;
            int greenWeight = Mathf.Max(0, ModConfig.Langwang.GreenBerryWeight.Value);
            int yellowWeight = Mathf.Max(0, ModConfig.Langwang.YellowBerryWeight.Value);

            if (!string.IsNullOrEmpty(green) && greenWeight > 0) pool.Add(new WeightedItem(green, greenWeight));
            if (!string.IsNullOrEmpty(yellow) && yellowWeight > 0) pool.Add(new WeightedItem(yellow, yellowWeight));
            if (pool.Count == 0) return;

            string picked = WeightedPicker.Pick(pool);
            if (string.IsNullOrEmpty(picked)) return;

            if (HostApi.GiveItemToInventorySlots(langwang, picked, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] 精品大果：{Nick(langwang)} 获得 1 个 {picked}。");
                return;
            }

            HostApi.SpawnItemAtFeet(langwang, picked);
            Plugin.Log.LogInfo($"[RollOfTheScout] 精品大果：{Nick(langwang)} 物品栏已满，{picked} 改在脚下生成。");
        }

        /// <summary>【精品大果】发放 + 【上瘾】超时叠加困倦。每帧由 TickTimers 调用。</summary>
        private void TickLangwang(List<Character> characters)
        {
            float dt = Time.deltaTime;

            // ---- 精品大果：每「发放间隔」秒发放 1 个荔莓 ----
            _langwangBerryTimer += dt;
            float berryInterval = ModConfig.Langwang.BerryIntervalSeconds.Value;
            if (berryInterval > 0f && _langwangBerryTimer >= berryInterval)
            {
                _langwangBerryTimer = 0f;
                foreach (var l in characters)
                {
                    if (_ledger.GetClass(l) != ClassType.Langwang) continue;
                    if (IsDead(l)) continue;
                    GrantLangwangBerry(l);
                }
            }

            // ---- 上瘾：超过「未食用间隔」秒没有食用任何荔莓 → 每「检查间隔」秒 +困倦 ----
            _langwangAddictionTimer += dt;
            float tick = ModConfig.Langwang.AddictionTickInterval.Value;
            float window = ModConfig.Langwang.AddictionNoBerryInterval.Value;
            float drowsy = ModConfig.Langwang.AddictionDrowsyPerTick.Value;
            if (tick > 0f && window > 0f && drowsy > 0f && _langwangAddictionTimer >= tick)
            {
                _langwangAddictionTimer = 0f;
                foreach (var l in characters)
                {
                    if (_ledger.GetClass(l) != ClassType.Langwang) continue;
                    if (IsDead(l)) continue;

                    string id = _ledger.IdOf(l);
                    if (string.IsNullOrEmpty(id)) continue;

                    // 首次见到该榔王：以当前时刻作为「上次食用荔莓」的基线（本局从这里开始计时）
                    if (!_langwangLastBerryAt.TryGetValue(id, out float lastAt))
                    {
                        _langwangLastBerryAt[id] = Time.time;
                        continue;
                    }

                    if (Time.time - lastAt < window) continue;

                    HostApi.ApplyStatus(l, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 上瘾：{Nick(l)} 已 {Time.time - lastAt:0.#} 秒未食用荔莓，困倦+{drowsy * 100f:0.#}");
                }
            }
        }

        /// <summary>
        /// 榔王 / 其他玩家 的食用处理（由 <c>Item.Consume</c> 补丁与 <c>GlobalEvents.OnItemConsumed</c> 双通道调用）。
        ///
        /// · 榔王自身：
        ///     ①【精品大果】食用青荔莓 → 加速；食用黄荔莓 → 无限体力；并刷新「最近食用荔莓」时刻；
        ///     ②【上瘾】食用非荔莓食物 → +困倦；
        ///     ③【纤维化】每次食用任意物品 → +损伤。
        /// · 其他玩家：
        ///     ④【没吃的赶紧戒】连续食用荔莓（连续次数 &gt; 阈值）且本局存在榔王 → 强制转职为榔王（绕过最大人数）。
        /// </summary>
        public void OnLangwangItemConsumed(Item item, Character character)
        {
            if (!_inRun) return;
            if (item == null || character == null) return;

            ClassType cls = _ledger.GetClass(character);
            string id = _ledger.IdOf(character);
            string name = PrefabNameOf(item);
            bool isBerry = IsLiBerry(name);

            // 去重：同一次食用可能被「Item.Consume 补丁」与「GlobalEvents.OnItemConsumed」两条通道各处理一次，
            // **所有玩家**（含用于【没吃的赶紧戒】计数的非榔王）都必须去重，否则连续次数会被双倍累计。
            if (!string.IsNullOrEmpty(id))
            {
                if (_langwangEatGuardUntil.TryGetValue(id, out float guardUntil) && Time.time < guardUntil) return;
                _langwangEatGuardUntil[id] = Time.time + LangwangEatDedupWindow;
            }

            if (cls == ClassType.Langwang)
            {
                if (isBerry)
                {
                    if (!string.IsNullOrEmpty(id)) _langwangLastBerryAt[id] = Time.time;

                    if (name == ModConfig.Langwang.GreenBerryItem.Value)
                        GrantSprint(character, "精品大果·青荔莓");
                    else if (name == ModConfig.Langwang.YellowBerryItem.Value)
                        GrantInfiniteStamina(character, "精品大果·黄荔莓");
                }
                else
                {
                    float drowsy = ModConfig.Langwang.AddictionDrowsyPerEat.Value;
                    if (drowsy > 0f)
                    {
                        HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
                        Plugin.Log.LogInfo(
                            $"[RollOfTheScout] 上瘾：{Nick(character)} 食用非荔莓食物（{name}），困倦+{drowsy * 100f:0.#}");
                    }
                }

                // ③【纤维化】每次食用任意物品 → +损伤
                //   黑名单（第 85 批新增）：诡异菇（转职触发物品）、万灵药、能量饮料、奶白金、大棒棒糖 → 豁免
                //   （效果描述里的括注按需求原文只列了 4 项：诡异菇、能量饮料、奶白金、大棒棒糖除外）
                float injury = ModConfig.Langwang.FibrosisInjuryPerEat.Value;
                if (injury > 0f
                    && !NameListContains(ModConfig.Langwang.FibrosisBlacklist.Value, ref _fibrosisBlacklistRaw, _fibrosisBlacklist, name))
                {
                    HostApi.ApplyStatus(character, CharacterAfflictions.STATUSTYPE.Injury, injury);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 纤维化：{Nick(character)} 食用物品（{name}），损伤+{injury * 100f:0.#}");
                }
                return;
            }

            // 其他玩家：连续食用荔莓计数（食用非荔莓食物则清零）
            if (string.IsNullOrEmpty(id)) return;
            if (isBerry)
            {
                _langwangBerryStreak.TryGetValue(id, out int streak);
                streak++;
                _langwangBerryStreak[id] = streak;
                TryLangwangConversion(character, id, streak);
            }
            else
            {
                _langwangBerryStreak[id] = 0;
            }
        }

        /// <summary>
        /// 【没吃的赶紧戒】：本局存在榔王时，其他玩家连续食用荔莓的次数**大于**
        /// 「没吃的赶紧戒-连续次数」（默认 3，即连续第 4 次起触发）即强制转职为榔王。
        /// 与待皓·模因污染 / 区·区块链同法：<c>ClassLedger.ForceAssign</c> 绕过「最大人数」限制
        /// （否则榔王默认最大 1 人时该效果永远无法生效），转职成功后由
        /// <see cref="ApplyClassChange"/> 清空负面状态并按新职业重发开局类效果。
        /// </summary>
        private void TryLangwangConversion(Character target, string id, int streak)
        {
            if (!ModConfig.Langwang.ConvertEnabled.Value) return;

            int threshold = ModConfig.Langwang.ConvertStreak.Value;
            if (threshold <= 0 || streak <= threshold) return;
            if (_ledger.GetClass(target) == ClassType.Langwang) return;

            // 只有本局确实已有榔王时才会传染（对应需求「本局有榔王时」）
            if (!_ledger.HasClassInRoom(ClassType.Langwang)) return;

            ClassType oldType = _ledger.GetClass(target);
            if (!_ledger.ForceAssign(target, ClassType.Langwang)) return;

            _langwangBerryStreak[id] = 0;
            ApplyClassChange(target, id, oldType, ClassType.Langwang);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 没吃的赶紧戒：{Nick(target)} 连续食用荔莓 {streak} 次（&gt;{threshold}），" +
                $"职业 {ClassLedger.Describe(oldType)} → 榔王。");
            ShowToast($"{Nick(target)} 没吃的赶紧戒：{ClassLedger.Describe(oldType)} → 榔王");
        }

        // ---------------------------------------------------------------
        // 程序员（Programmer）：【BUG】/【溢出】/【熬夜】/【开挂】
        // 【溢出】另见 Patches.ProgrammerOverflowPatch 与 HostApi.ExtraStaminaAdjust。
        // ---------------------------------------------------------------

        /// <summary>每帧推进程序员的四个效果（由 <see cref="TickTimers"/> 调用）。</summary>
        private void TickProgrammer(List<Character> characters)
        {
            float dt = Time.deltaTime;
            TickProgrammerBug(characters, dt);
            TickProgrammerNight(characters, dt);
            TickProgrammerClean(characters, dt);
            TickProgrammerCopy(characters, dt);
            TickProgrammerCopyRefresh();
            TickProgrammerCopyTimers();
        }

        // ---------------- 【BUG】：每 N 秒获得一个随机昆虫 ----------------

        private void TickProgrammerBug(List<Character> characters, float dt)
        {
            float interval = ModConfig.Programmer.BugIntervalSeconds.Value;
            if (interval <= 0f)
            {
                _programmerBugTimer = 0f;
                return;
            }
            _programmerBugTimer += dt;
            if (_programmerBugTimer < interval) return;
            _programmerBugTimer = 0f;

            foreach (var p in characters)
            {
                if (_ledger.GetClass(p) != ClassType.Programmer) continue;
                if (IsDead(p)) continue;
                GrantProgrammerBug(p);
            }
        }

        /// <summary>
        /// 【BUG】给指定程序员发放 1 个随机昆虫（默认：蜱虫 70% / 蝎子 20% / 早起虫儿 10%）：
        /// 优先放入 3 个物品栏空位，**物品栏已满则不发放**（不退化到主手 / 不落地）；
        /// 抽中蜱虫立刻 +「BUG-蜱虫中毒」，抽中蝎子立刻 +「BUG-蝎子中毒」。
        /// </summary>
        private void GrantProgrammerBug(Character programmer)
        {
            string tick = ModConfig.Programmer.BugTickItem.Value;
            string scorp = ModConfig.Programmer.BugScorpionItem.Value;
            string worm = ModConfig.Programmer.BugWormItem.Value;

            var pool = new List<WeightedItem>(3);
            if (!string.IsNullOrWhiteSpace(tick))
                pool.Add(new WeightedItem(tick, ModConfig.Programmer.BugTickWeight.Value));
            if (!string.IsNullOrWhiteSpace(scorp))
                pool.Add(new WeightedItem(scorp, ModConfig.Programmer.BugScorpionWeight.Value));
            if (!string.IsNullOrWhiteSpace(worm))
                pool.Add(new WeightedItem(worm, ModConfig.Programmer.BugWormWeight.Value));

            string picked = WeightedPicker.Pick(pool);
            if (string.IsNullOrEmpty(picked)) return;

            if (!HostApi.HasEmptyItemSlot(programmer))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] BUG：{Nick(programmer)} 物品栏已满，本次昆虫（{picked}）不发放。");
                return;
            }
            if (!HostApi.GiveItemToInventorySlots(programmer, picked, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] BUG：{Nick(programmer)} 发放 {picked} 失败。");
                return;
            }

            float poison = 0f;
            if (picked == tick) poison = ModConfig.Programmer.BugTickPoison.Value;
            else if (picked == scorp) poison = ModConfig.Programmer.BugScorpionPoison.Value;

            if (poison != 0f)
                HostApi.ApplyStatus(programmer, CharacterAfflictions.STATUSTYPE.Poison, poison);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] BUG：{Nick(programmer)} 获得 {picked}" +
                (poison != 0f ? $"，中毒+{poison * 100f:0.#}" : ""));
        }

        // ---------------- 【熬夜】：夜晚每 N 秒获得一次能量饮料效果 ----------------

        private void TickProgrammerNight(List<Character> characters, float dt)
        {
            if (!ModConfig.Programmer.NightOwlEnabled.Value)
            {
                _programmerNightTimer = 0f;
                return;
            }
            float interval = ModConfig.Programmer.NightOwlIntervalSeconds.Value;
            if (interval <= 0f)
            {
                _programmerNightTimer = 0f;
                return;
            }
            _programmerNightTimer += dt;
            if (_programmerNightTimer < interval) return;
            _programmerNightTimer = 0f;

            // 与原版「夜晚」判定一致：DayNightManager.isDay < 0.5（该值实际为 0/1）。
            var dayNight = DayNightManager.instance;
            if (dayNight == null || dayNight.isDay >= 0.5f) return;

            foreach (var p in characters)
            {
                if (_ledger.GetClass(p) != ClassType.Programmer) continue;
                if (IsDead(p)) continue;
                // clearDrowsyAfter 保持 false：加速结束后**不额外清除**因此产生的困倦
                GrantSprint(p, "熬夜");
            }
        }

        // ---------------- 【开挂·无队友】：随机削减 1 项负面状态 ----------------

        private void TickProgrammerClean(List<Character> characters, float dt)
        {
            if (!ModConfig.Programmer.CheatEnabled.Value)
            {
                _programmerCleanTimer = 0f;
                return;
            }
            float interval = ModConfig.Programmer.CheatCleanIntervalSeconds.Value;
            float amount = Mathf.Abs(ModConfig.Programmer.CheatCleanAmount.Value);
            if (interval <= 0f || amount <= 0f)
            {
                _programmerCleanTimer = 0f;
                return;
            }
            _programmerCleanTimer += dt;
            if (_programmerCleanTimer < interval) return;
            _programmerCleanTimer = 0f;

            float radius = Mathf.Max(0f, ModConfig.Programmer.CheatSoloRadius.Value);
            var types = GetProgrammerStatusTypes();
            if (types.Count == 0) return;

            foreach (var p in characters)
            {
                if (_ledger.GetClass(p) != ClassType.Programmer) continue;
                if (IsDead(p)) continue;
                if (HasOtherPlayerWithin(p, radius)) continue;

                var present = new List<CharacterAfflictions.STATUSTYPE>();
                foreach (var t in types)
                {
                    if (HostApi.GetStatus(p, t) > 0f) present.Add(t);
                }
                if (present.Count == 0) continue;

                var picked = present[Random.Range(0, present.Count)];
                HostApi.ApplyStatus(p, picked, -amount);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 开挂·无队友：{Nick(p)} 半径 {radius:0.#} 米内无其他玩家，随机削减「{picked}」{amount * 100f:0.#} 点。");
            }
        }

        // ---------------- 【开挂·复制】：复制在场随机玩家的随机职业效果 ----------------
        // 语义（第 84 批修订）：每「开挂-复制间隔」检测一次；
        //   ★ 检测**开始时先清除上一轮复制到的其他职业效果**（无论本次判定成功与否），使之无效化、不继承；
        //   随后同时满足「①自身没有任何负面状态」且「②物品栏中没有任意昆虫（蜱虫 / 蝎子 / 早起虫儿）」时，
        //   从在场随机一名其他存活玩家（排除自己、排除「程序员」职业）的当前职业里随机取 1 个**非负面**职业效果
        //   （负面效果已从候选表删除），并按该职业的「开局初始自带 / 代表效果」立即给予，持续到「下一次检测」（默认 3 分钟）。

        private void TickProgrammerCopy(List<Character> characters, float dt)
        {
            if (!ModConfig.Programmer.CheatEnabled.Value || !ModConfig.Programmer.CheatCopyEnabled.Value)
            {
                // 关闭时也要把上一轮已发放的复制效果清掉，避免残留（不继承）
                if (_programmerCopies.Count > 0) ClearProgrammerCopies("开挂·复制已关闭");
                _programmerCopyTimer = 0f;
                return;
            }
            float interval = ModConfig.Programmer.CheatCopyIntervalSeconds.Value;
            if (interval <= 0f)
            {
                if (_programmerCopies.Count > 0) ClearProgrammerCopies("开挂·复制已关闭");
                _programmerCopyTimer = 0f;
                return;
            }
            _programmerCopyTimer += dt;
            if (_programmerCopyTimer < interval) return;
            _programmerCopyTimer = 0f;

            // ★ 需求：下一次检测「无论是否判定成功」，都先清除掉之前获得的其他职业效果
            ClearProgrammerCopies("开挂·复制·下一次检测");

            var types = GetProgrammerStatusTypes();

            foreach (var p in characters)
            {
                if (_ledger.GetClass(p) != ClassType.Programmer) continue;
                if (IsDead(p)) continue;
                string id = _ledger.IdOf(p);
                if (string.IsNullOrEmpty(id)) continue;

                // 条件①：自身「没有任何负面状态」
                if (HasAnyNegativeStatus(p, types)) continue;

                // 条件②：自身「物品栏中没有任意昆虫」（蜱虫 / 蝎子 / 早起虫儿）——持有昆虫则本周期不复制
                if (HasProgrammerInsect(p))
                {
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·复制：{Nick(p)} 物品栏中仍持有昆虫，本周期不复制职业效果。");
                    continue;
                }

                // 在场随机一名「其他存活玩家」：除自己以外、职业非「程序员」、且该职业至少有一个**非负面**可复制效果
                var candidates = new List<Character>();
                foreach (var other in characters)
                {
                    if (other == null || other == p) continue;
                    if (other.photonView == null) continue;
                    if (IsDead(other)) continue;
                    ClassType otherClass = _ledger.GetClass(other);
                    if (otherClass == ClassType.None || otherClass == ClassType.Programmer) continue;
                    if (!HasObtainableProgrammerCopyEffect(otherClass)) continue;
                    candidates.Add(other);
                }
                if (candidates.Count == 0) continue;

                var src = candidates[Random.Range(0, candidates.Count)];
                ClassType srcClass = _ledger.GetClass(src);
                if (srcClass == ClassType.None) continue;

                string effect = PickProgrammerCopyEffect(srcClass);
                if (string.IsNullOrEmpty(effect)) continue;

                string kind = ProgrammerCopyRefreshKind(srcClass, effect);
                float duration = Mathf.Max(0f, ModConfig.Programmer.CheatCopyDurationSeconds.Value);
                float timerInterval = ProgrammerCopyTimerInterval(srcClass, effect);
                var rec = new ProgrammerCopy
                {
                    SourceClass = srcClass,
                    Effect = effect,
                    Target = p,
                    Interval = timerInterval,
                    NextTickAt = timerInterval > 0f ? Time.time + timerInterval : float.MaxValue,
                    RefreshUntil = duration > 0f ? Time.time + duration : -1f,
                    RefreshKind = kind,
                    NextReapplyAt = Time.time + ProgrammerCopyRefreshCadence(kind),
                };
                ApplyProgrammerCopyEffect(p, rec);   // 立即给予，并登记清理信息
                _programmerCopies[id] = rec;

                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 开挂·复制：{Nick(p)} 无任何负面状态，复制在场玩家 {Nick(src)} 的职业「{ClassLedger.Describe(srcClass)}」随机效果【{effect}】" +
                    $"，立即给予并持续到下一次检测。");
                if (ModConfig.Programmer.CheatCopyNotify.Value)
                    ShowToast($"【开挂】：程序员 {Nick(p)} 获得“{ClassLedger.Describe(srcClass)}”【{effect}】效果");
            }
        }

        /// <summary>推进「已复制效果」：窗口内对「加速 / 无限体力」自动续发；目标失效（死亡 / 转职 / 离开）即清理并移除。</summary>
        private void TickProgrammerCopyRefresh()
        {
            if (_programmerCopies.Count == 0) return;
            float now = Time.time;

            List<string> expired = null;
            foreach (var kv in _programmerCopies)
            {
                var rec = kv.Value;
                var p = rec.Target;
                bool valid = p != null && p.photonView != null
                    && _ledger.GetClass(p) == ClassType.Programmer && !IsDead(p);
                if (!valid)
                {
                    CleanupProgrammerCopy(rec, "目标失效");
                    if (expired == null) expired = new List<string>();
                    expired.Add(kv.Key);
                    continue;
                }
                if (rec.RefreshKind == null || rec.RefreshUntil <= 0f || now >= rec.RefreshUntil) continue;
                if (now < rec.NextReapplyAt) continue;
                rec.NextReapplyAt = now + ProgrammerCopyRefreshCadence(rec.RefreshKind);
                ApplyProgrammerCopyEffect(p, rec);   // 续发（同时刷新清理信息）
            }
            if (expired != null)
            {
                foreach (var id in expired) _programmerCopies.Remove(id);
            }
        }

        /// <summary>定时型效果：生效期内按 <see cref="ProgrammerCopy.Interval"/> 重复触发（与“一次性立即给予”相对）。</summary>
        private void TickProgrammerCopyTimers()
        {
            if (_programmerCopies.Count == 0) return;
            float now = Time.time;
            foreach (var kv in _programmerCopies)
            {
                var rec = kv.Value;
                if (rec.Interval <= 0f || rec.RefreshKind != null) continue;   // 续发型由 TickProgrammerCopyRefresh 处理
                var p = rec.Target;
                if (p == null || p.photonView == null) continue;
                if (now < rec.NextTickAt) continue;
                rec.NextTickAt = now + rec.Interval;
                ApplyProgrammerCopyEffect(p, rec);
            }
        }

        /// <summary>清除**所有**已复制的职业效果（下一次检测开始时 / 关闭功能时调用），保证「不继承」。</summary>
        private void ClearProgrammerCopies(string reason)
        {
            if (_programmerCopies.Count == 0) return;
            foreach (var kv in _programmerCopies)
            {
                CleanupProgrammerCopy(kv.Value, reason);
            }
            _programmerCopies.Clear();
        }

        /// <summary>
        /// 撤销一次「复制到的职业效果」：收回物品（物品栏 / 背包）、反向撤销状态、扣回额外体力、绝对恢复石化。
        /// ⚠ 加速 / 无限体力属原版 <c>Affliction</c>，主机**无法从远端移除**（引擎限制），此处仅停止续发，待其自然结束。
        /// </summary>
        private void CleanupProgrammerCopy(ProgrammerCopy rec, string reason)
        {
            if (rec == null) return;
            var p = rec.Target;
            if (p == null || p.photonView == null) return;

            if (!string.IsNullOrEmpty(rec.Item1)) HostApi.RemoveItemsFromInventorySlots(p, rec.Item1);
            if (!string.IsNullOrEmpty(rec.Item2)) HostApi.RemoveItemsFromInventorySlots(p, rec.Item2);
            if (rec.GroundItems != null)
            {
                // 物品栏满时「在脚下生成」的物品：若仍在地面则销毁（已被拾取的情形由上面的「按预制体名从物品栏收回」兜底）
                foreach (var go in rec.GroundItems)
                {
                    if (go != null) PhotonNetwork.Destroy(go);
                }
                rec.GroundItems.Clear();
            }
            if (rec.RemoveBackpack) HostApi.RemoveBackpack(p);
            if (rec.Status1.HasValue) HostApi.ApplyStatus(p, rec.Status1.Value, -rec.Status1Amount);
            if (rec.Status2.HasValue) HostApi.ApplyStatus(p, rec.Status2.Value, -rec.Status2Amount);
            if (rec.StaminaUndo != 0f) HostApi.BoostExtraStamina(p, -rec.StaminaUndo);
            if (rec.PetrifyRestore >= 0) HostApi.SetPetrify(p, rec.PetrifyRestore);

            Plugin.Log.LogInfo(
                $"[RollOfTheScout] 开挂·复制清理（{reason}）：{Nick(p)} 撤销【{ClassLedger.Describe(rec.SourceClass)}·{rec.Effect}】" +
                (rec.StopRefreshOnly ? "（加速 / 无限体力无法远端移除，已停止续发）" : "。"));
        }

        /// <summary>自身是否存在任意一项「开挂-负面状态」中列举的负面状态。</summary>
        private bool HasAnyNegativeStatus(Character p, List<CharacterAfflictions.STATUSTYPE> types)
        {
            foreach (var t in types)
            {
                if (HostApi.GetStatus(p, t) > 0f) return true;
            }
            return false;
        }

        /// <summary>自身 3 个物品栏中是否持有任意昆虫（蜱虫 / 蝎子 / 早起虫儿，取 [程序员] 配置的物品名）——持有则本周期不复制。</summary>
        private bool HasProgrammerInsect(Character p)
        {
            return CountItemInInventory(p, ModConfig.Programmer.BugTickItem.Value) > 0
                || CountItemInInventory(p, ModConfig.Programmer.BugScorpionItem.Value) > 0
                || CountItemInInventory(p, ModConfig.Programmer.BugWormItem.Value) > 0;
        }

        /// <summary>该职业是否至少有一个「非负面」可复制效果（用于把「无非负面效果」的职业排除出来源候选）。</summary>
        private static bool HasObtainableProgrammerCopyEffect(ClassType cls)
        {
            var pool = ProgrammerCopyEffectPool(cls);
            return pool != null && pool.Length > 0;
        }

        /// <summary>解析「开挂-负面状态」配置为状态类型列表（带缓存，配置变化时重建）。</summary>
        private List<CharacterAfflictions.STATUSTYPE> GetProgrammerStatusTypes()
        {
            string raw = ModConfig.Programmer.CheatCleanTypes.Value;
            if (raw == _programmerStatusTypesRaw && _programmerStatusTypes.Count > 0) return _programmerStatusTypes;

            _programmerStatusTypesRaw = raw;
            _programmerStatusTypes.Clear();
            foreach (var name in WeightedPicker.ParseList(raw))
            {
                if (!System.Enum.TryParse(name, out CharacterAfflictions.STATUSTYPE t)) continue;
                if (!System.Enum.IsDefined(typeof(CharacterAfflictions.STATUSTYPE), t)) continue;
                if (!_programmerStatusTypes.Contains(t)) _programmerStatusTypes.Add(t);
            }
            return _programmerStatusTypes;
        }

        /// <summary>
        /// 该职业「可被复制」的效果候选（**只含非负面效果**：不涉及对自身或他人的负面状态 / 削弱 / 强制倒地等）。
        /// ★=定时型（见 <see cref="ProgrammerCopyTimerInterval"/>）；该职业若无非负面效果则返回 null（不作为来源）。
        /// </summary>
        private static string[] ProgrammerCopyEffectPool(ClassType cls)
        {
            switch (cls)
            {
                case ClassType.Explorer:       return new[] { "开路", "高代谢" };
                case ClassType.Leader:         return new[] { "号召" };
                case ClassType.Logistician:    return new[] { "备战充分" };
                case ClassType.Doctor:         return new[] { "护理学", "毒理学" };
                case ClassType.Assassin:       return new[] { "刺杀" };
                case ClassType.Cook:           return new[] { "后勤保障" };
                case ClassType.M:              return new[] { "享受", "耐受" };
                case ClassType.Savage:         return new[] { "不拿拿", "自然韧性" };
                case ClassType.Scout:          return new[] { "集体" };
                case ClassType.StandardBearer: return new[] { "执旗" };
                case ClassType.Daihao:         return new[] { "生产", "我们全都是" };
                case ClassType.Saint:          return new[] { "辟谷养生", "完美主义" };
                case ClassType.Mage:           return new[] { "魔法卷轴" };
                case ClassType.Challenger:     return new[] { "顶级玩家" };
                case ClassType.MasterChef:     return new[] { "自带厨具", "破坏大师" };
                case ClassType.Hamster:        return new[] { "我全都要" };
                case ClassType.Firefighter:    return new[] { "火线救援" };
                case ClassType.Narcissist:     return new[] { "化妆盒" };
                case ClassType.Gunner:         return new[] { "开炮" };
                case ClassType.Band:           return new[] { "乐器" };
                case ClassType.MilesSoul:      return new[] { "封印解放" };
                case ClassType.Qu:             return new[] { "好多区", "区块链" };
                case ClassType.Astronaut:      return new[] { "NASA", "轻飘飘" };
                case ClassType.Langwang:       return new[] { "精品大果" };
                default:                       return null;   // 潘多拉 / 死灵法师 / 舞王 / 和平主义者 / 独狼 / 程序员：无非负面效果，不作来源
            }
        }

        private static string PickProgrammerCopyEffect(ClassType cls)
        {
            var pool = ProgrammerCopyEffectPool(cls);
            if (pool == null || pool.Length == 0) return null;
            return pool[Random.Range(0, pool.Length)];
        }

        /// <summary>
        /// 把「复制到的职业效果」**立即**施加到目标程序员身上（与玩家中途转职时重发开局效果同理），
        /// 同时在 <paramref name="rec"/> 上登记「清理信息」，供下一次检测时撤销（保证「不继承」）。
        /// 物品只放入 3 个物品栏空位（**不回退**到脚下生成），以保证清理时能精确收回。
        /// 加速 / 无限体力属原版 Affliction，主机无法从远端移除 → 仅登记续发/停止续发（<see cref="ProgrammerCopy.StopRefreshOnly"/>）。
        /// </summary>
        private void ApplyProgrammerCopyEffect(Character p, ProgrammerCopy rec)
        {
            // 定时型效果：登记「发放类」物品的清理信息，并执行本次触发（首次触发即等于「立即给予」）
            if (rec.Interval > 0f)
            {
                RegisterProgrammerCopyTimerItem(rec);
                ProgrammerCopyTimerTick(p, rec);
                return;
            }

            switch (rec.SourceClass)
            {
                // 【开路】：开局初始自带攀爬物（岩钉）
                case ClassType.Explorer:
                    rec.Item1 = ItemNames.ClimbingSpike;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·开路");
                    return;

                // 【宿命】：开局初始自带诅咒 / 石化 / 额外体力
                case ClassType.Leader:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Curse;
                    rec.Status1Amount = ModConfig.Leader.InitCurseAmount.Value;
                    rec.StaminaUndo = ModConfig.Leader.InitExtraStamina.Value;
                    rec.PetrifyRestore = HostApi.GetPetrify(p);
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    HostApi.SetPetrify(p, Mathf.Clamp(rec.PetrifyRestore + Mathf.RoundToInt(ModConfig.Leader.InitPetrifyAmount.Value * 100f), 0, 100));
                    HostApi.BoostExtraStamina(p, rec.StaminaUndo);
                    return;

                // 【备战充分】：开局初始自带背包（背包 / 喷气背包随机）
                // 第 86 批：已装备背包时「不放进已装备背包、也不覆盖它」，改按普通物品发放（空物品栏 → 满则脚下生成）
                case ClassType.Logistician:
                    rec.Item1 = Random.value < 0.5f ? ItemNames.Backpack : ItemNames.Jetpack;
                    GiveProgrammerCopyBackpack(p, rec.Item1, "开挂·备战充分");
                    return;

                // 【护理学】：清除自身损伤
                case ClassType.Doctor:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Injury;
                    rec.Status1Amount = -0.05f;
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    return;

                // 【刺杀】：开局初始自带仪式匕首 + 吹箭筒
                case ClassType.Assassin:
                    rec.Item1 = ItemNames.RitualDagger;
                    rec.Item2 = ItemNames.Blowgun;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·刺杀");
                    GiveProgrammerCopyItem(p, rec.Item2, "开挂·刺杀");
                    return;

                // 【潘多拉】：获得潘多拉魔盒
                case ClassType.Pandora:
                    rec.Item1 = ItemNames.PandorasBox;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·潘多拉");
                    return;

                // 【骸骨】：开局初始自带骸骨之书 + 初始诅咒
                case ClassType.Necromancer:
                    rec.Item1 = ItemNames.BookOfBones;
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Curse;
                    rec.Status1Amount = ModConfig.Necromancer.InitCurseAmount.Value;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·骸骨");
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    return;

                // 【享受】：额外体力
                case ClassType.M:
                    rec.StaminaUndo = 0.05f;
                    HostApi.BoostExtraStamina(p, rec.StaminaUndo);
                    return;

                // 【不拿拿】：获得粉香蕉
                case ClassType.Savage:
                    rec.Item1 = ItemNames.BerrynanaPink;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·不拿拿");
                    return;

                // 【集体】：清除自身损伤 + 饥饿
                case ClassType.Scout:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Injury;
                    rec.Status1Amount = -0.025f;
                    rec.Status2 = CharacterAfflictions.STATUSTYPE.Hunger;
                    rec.Status2Amount = -0.025f;
                    HostApi.ApplyStatusPair(p, rec.Status1.Value, rec.Status1Amount, rec.Status2.Value, rec.Status2Amount);
                    return;

                // 【执旗】：开局初始自带检查点旗标
                case ClassType.StandardBearer:
                    rec.Item1 = ItemNames.CheckpointFlag;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·执旗");
                    return;

                // 【舞动青春】：额外体力
                case ClassType.Dancer:
                    rec.StaminaUndo = 0.05f;
                    HostApi.BoostExtraStamina(p, rec.StaminaUndo);
                    return;

                // 【生产】：获得好大蛋
                case ClassType.Daihao:
                    rec.Item1 = ItemNames.NestEgg;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·生产");
                    return;

                // 【辟谷养生】：清除自身饥饿
                case ClassType.Saint:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Hunger;
                    rec.Status1Amount = -1f;
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    return;

                // 【魔法卷轴】：获得卷轴
                case ClassType.Mage:
                    rec.Item1 = ItemNames.GuidebookScroll;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·魔法卷轴");
                    return;

                // 【顶级玩家】：开局初始自带黄金抓钩 + 滑翔翼；【极限】：初始诅咒 + 初始石化
                case ClassType.Challenger:
                    if (rec.Effect == "顶级玩家")
                    {
                        rec.Item1 = ItemNames.GoldenHook;
                        rec.Item2 = ItemNames.Glider;
                        GiveProgrammerCopyItem(p, rec.Item1, "开挂·顶级玩家");
                        GiveProgrammerCopyItem(p, rec.Item2, "开挂·顶级玩家");
                        return;
                    }
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Curse;
                    rec.Status1Amount = ModConfig.Challenger.InitCurseAmount.Value;
                    rec.PetrifyRestore = HostApi.GetPetrify(p);
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    HostApi.SetPetrify(p, Mathf.Clamp(rec.PetrifyRestore + Mathf.RoundToInt(ModConfig.Challenger.InitPetrifyAmount.Value * 100f), 0, 100));
                    return;

                // 【自带厨具】：获得便携烤炉
                case ClassType.MasterChef:
                    rec.Item1 = ItemNames.PortableStove;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·自带厨具");
                    return;

                // 【后勤保障】：获得便携烤炉
                case ClassType.Cook:
                    rec.Item1 = ItemNames.PortableStove;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·后勤保障");
                    return;

                // 【和平？】：开局初始自带诅咒
                case ClassType.Pacifist:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Curse;
                    rec.Status1Amount = ModConfig.Pacifist.InitCurseAmount.Value;
                    HostApi.ApplyStatus(p, rec.Status1.Value, rec.Status1Amount);
                    return;

                // 【我全都要】：开局初始自带背包
                // 第 86 批：已装备背包时「不放进已装备背包、也不覆盖它」，改按普通物品发放（空物品栏 → 满则脚下生成）
                case ClassType.Hamster:
                    rec.Item1 = ItemNames.Backpack;
                    GiveProgrammerCopyBackpack(p, ItemNames.Backpack, "开挂·我全都要");
                    return;

                // 【火线救援】：加速（无法远端移除，仅停止续发）
                case ClassType.Firefighter:
                    rec.StopRefreshOnly = true;
                    GrantSprint(p, "开挂·火线救援", true);
                    return;

                // 【化妆盒】：获得防晒喷雾
                case ClassType.Narcissist:
                    rec.Item1 = ItemNames.Sunscreen;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·化妆盒");
                    return;

                // 【独行长路】：额外体力
                case ClassType.LoneWolf:
                    rec.StaminaUndo = 0.05f;
                    HostApi.BoostExtraStamina(p, rec.StaminaUndo);
                    return;

                // 【开炮】：获得童子军大炮
                case ClassType.Gunner:
                    rec.Item1 = ItemNames.ScoutCannonItem;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·开炮");
                    return;

                // 【乐器】：获得小号
                case ClassType.Band:
                    rec.Item1 = ItemNames.Bugle;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·乐器");
                    return;

                // 【封印解放】：无限体力（无法远端移除，仅停止续发）
                case ClassType.MilesSoul:
                    rec.StopRefreshOnly = true;
                    GrantInfiniteStamina(p, "开挂·封印解放");
                    return;

                // 【好多区】：获得早起虫儿
                case ClassType.Qu:
                    rec.Item1 = ItemNames.EarlyWorm;
                    GiveProgrammerCopyItem(p, rec.Item1, "开挂·好多区");
                    return;

                // 【NASA】：清除自身炎热 + 寒冷
                case ClassType.Astronaut:
                    rec.Status1 = CharacterAfflictions.STATUSTYPE.Hot;
                    rec.Status1Amount = -0.05f;
                    rec.Status2 = CharacterAfflictions.STATUSTYPE.Cold;
                    rec.Status2Amount = -0.05f;
                    HostApi.ApplyStatusPair(p, rec.Status1.Value, rec.Status1Amount, rec.Status2.Value, rec.Status2Amount);
                    return;

                // 【精品大果】：获得荔莓（绿 / 黄随机）
                case ClassType.Langwang:
                {
                    var pool = new List<WeightedItem>
                    {
                        new WeightedItem(ModConfig.Langwang.GreenBerryItem.Value, ModConfig.Langwang.GreenBerryWeight.Value),
                        new WeightedItem(ModConfig.Langwang.YellowBerryItem.Value, ModConfig.Langwang.YellowBerryWeight.Value),
                    };
                    pool.RemoveAll(w => string.IsNullOrWhiteSpace(w.PrefabName));
                    string berry = WeightedPicker.Pick(pool);
                    if (string.IsNullOrEmpty(berry)) return;
                    rec.Item1 = berry;
                    GiveProgrammerCopyItem(p, berry, "开挂·精品大果");
                    return;
                }

                case ClassType.Programmer:
                    // 不可达：候选来源已排除「程序员」职业（见 TickProgrammerCopy），此处仅作防御
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// 【开挂·复制】发放物品：**优先放入 3 个物品栏空位**；**物品栏已满则在脚下生成**（已生成的物品登记进
        /// <see cref="ProgrammerCopy.GroundItems"/>，清理时销毁，避免「继承」）。
        /// </summary>
        private void GiveProgrammerCopyItem(Character p, string prefabName, string reason)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            if (HostApi.GiveItemToInventorySlots(p, prefabName, out _))
            {
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(p)} 获得 1 个 {prefabName}（放入空物品栏）。");
                return;
            }

            var go = HostApi.SpawnItemAtFeet(p, prefabName);
            if (go == null)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：{Nick(p)} 物品栏已满且脚下生成失败，{prefabName} 未能发放。");
                return;
            }
            var rec = GetActiveProgrammerCopy(p);
            if (rec != null)
            {
                if (rec.GroundItems == null) rec.GroundItems = new List<GameObject>();
                rec.GroundItems.Add(go);
            }
            Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(p)} 物品栏已满，{prefabName} 已在脚下生成。");
        }

        /// <summary>
        /// 【开挂·复制】发放「背包类」效果（仓鼠【我全都要】/ 后勤【备战充分】）：
        /// **只有自身未装备任何背包时**才把新背包装备到背包槽（并登记 <see cref="ProgrammerCopy.RemoveBackpack"/> 以便清理）；
        /// **已装备背包时绝不放进已装备背包、也不覆盖它（及其中物品）**，改按普通物品处理（空物品栏 → 满则脚下生成）。
        /// </summary>
        private void GiveProgrammerCopyBackpack(Character p, string backpackPrefab, string reason)
        {
            if (string.IsNullOrEmpty(backpackPrefab)) return;

            if (HostApi.HasBackpack(p))
            {
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] {reason}：{Nick(p)} 已装备背包 → {backpackPrefab} 按普通物品发放（不放入 / 不覆盖已装备背包）。");
                GiveProgrammerCopyItem(p, backpackPrefab, reason);
                return;
            }

            if (HostApi.EquipBackpack(p, backpackPrefab, null))
            {
                var rec = GetActiveProgrammerCopy(p);
                if (rec != null) rec.RemoveBackpack = true;   // 仅当「本次确实装备了新背包」才在清理时清空背包槽
                Plugin.Log.LogInfo($"[RollOfTheScout] {reason}：{Nick(p)} 未装备背包 → 已装备 1 个 {backpackPrefab}（空背包）。");
                return;
            }
            Plugin.Log.LogWarning($"[RollOfTheScout] {reason}：{Nick(p)} 装备 {backpackPrefab} 失败（背包槽未就绪）。");
        }

        /// <summary>取该玩家当前生效的【开挂·复制】记录（未生效时返回 null），供发放物品时登记清理信息。</summary>
        private ProgrammerCopy GetActiveProgrammerCopy(Character p)
        {
            if (p == null) return null;
            string id = _ledger.IdOf(p);
            if (string.IsNullOrEmpty(id)) return null;
            _programmerCopies.TryGetValue(id, out var rec);
            return rec;
        }

        // ---------------- 定时型效果的「间隔」与「逐次触发」 ----------------

        /// <summary>
        /// 该「复制到的效果」是否属于**定时型**（生效期内按自身节奏重复触发）：返回间隔（秒）；一次性效果返回 0。
        /// 数值取各职业在文档中的默认量级（不读取该职业自己的 cfg）。
        /// </summary>
        private static float ProgrammerCopyTimerInterval(ClassType cls, string effect)
        {
            switch (cls)
            {
                case ClassType.Explorer:   return effect == "高代谢" ? 40f : 0f;
                case ClassType.Leader:     return effect == "号召" ? 180f : 0f;
                case ClassType.Doctor:     return effect == "护理学" ? 8f : (effect == "毒理学" ? 3f : 0f);
                case ClassType.Pandora:    return 5f;
                case ClassType.Necromancer:return effect == "亡灵" ? 5f : 0f;
                case ClassType.M:          return effect == "耐受" ? 10f : 0f;
                case ClassType.Savage:     return effect == "自然韧性" ? 5f : (effect == "不拿拿" ? 60f : 0f);
                case ClassType.Dancer:     return 60f;
                case ClassType.Daihao:     return effect == "脚滑" ? 150f : (effect == "我们全都是" ? 30f : (effect == "生产" ? 60f : 0f));
                case ClassType.Saint:      return effect == "教诲" ? 10f : (effect == "完美主义" ? 60f : 0f);
                case ClassType.MasterChef: return effect == "手暖炉" ? 1f : (effect == "破坏大师" ? 60f : 0f);
                case ClassType.Qu:         return effect == "区块链" ? 7f : (effect == "好多区" ? 60f : 0f);
                case ClassType.Astronaut:  return effect == "NASA" ? 5f : (effect == "轻飘飘" ? 120f : 0f);
                case ClassType.LoneWolf:   return 15f;
                case ClassType.Gunner:     return 120f;
                case ClassType.Cook:       return 180f;
                case ClassType.Mage:       return 180f;
                case ClassType.Langwang:   return 100f;
                default:                   return 0f;
            }
        }

        /// <summary>定时型效果中「会发放物品」的，预先在记录上登记需要清理的物品（清理时按预制体名从物品栏收回）。</summary>
        private void RegisterProgrammerCopyTimerItem(ProgrammerCopy rec)
        {
            switch (rec.SourceClass)
            {
                case ClassType.Savage: rec.Item1 = ItemNames.BerrynanaPink; return;   // 不拿拿
                case ClassType.Daihao: rec.Item1 = ItemNames.NestEgg; return;         // 生产
                case ClassType.Mage:   rec.Item1 = ItemNames.GuidebookScroll; return; // 魔法卷轴
                case ClassType.Cook:   rec.Item1 = ItemNames.PortableStove; return;   // 后勤保障
                case ClassType.Gunner: rec.Item1 = ItemNames.ScoutCannonItem; return; // 开炮
                case ClassType.Langwang:                                             // 精品大果（绿 / 黄两件都登记，清理时都收回）
                    rec.Item1 = ModConfig.Langwang.GreenBerryItem.Value;
                    rec.Item2 = ModConfig.Langwang.YellowBerryItem.Value;
                    return;
            }
        }

        /// <summary>
        /// 定时型「复制效果」的一次触发（首次由 <see cref="ApplyProgrammerCopyEffect"/> 调用，之后由
        /// <see cref="TickProgrammerCopies"/> 按各自间隔重复调用）。
        /// 数值取各职业在文档中的默认量级（不读取该职业自己的 cfg）；会影响他人的效果（号召 / 潘多拉 / 亡灵 / 教诲）按原样影响他人。
        /// </summary>
        private void ProgrammerCopyTimerTick(Character p, ProgrammerCopy rec)
        {
            switch (rec.SourceClass)
            {
                // 先驱·高代谢：定时获得一次加速
                case ClassType.Explorer:
                    GrantSprint(p, "开挂·高代谢", true);
                    return;

                // 领袖·号召：半径 30m 内所有人（含自己）+额外体力 / −饥饿 / −损伤
                case ClassType.Leader:
                {
                    const float radius = 30f;
                    int n = 0;
                    foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                    {
                        if (t == null || IsDead(t)) continue;
                        HostApi.BoostExtraStamina(t, 0.25f);
                        HostApi.ApplyStatusPair(t,
                            CharacterAfflictions.STATUSTYPE.Injury, -0.05f,
                            CharacterAfflictions.STATUSTYPE.Hunger, -0.1f);
                        n++;
                    }
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·号召：{Nick(p)} 对半径 {radius:0.#} 米内 {n} 名玩家 +额外体力 / −饥饿 / −损伤。");
                    return;
                }

                // 医生·护理学 / 毒理学：半径 5m 内所有人削减损伤（毒理学另削中毒）
                case ClassType.Doctor:
                {
                    const float radius = 5f;
                    bool toxin = rec.Effect == "毒理学";
                    int n = 0;
                    foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                    {
                        if (t == null || IsDead(t)) continue;
                        if (toxin)
                            HostApi.ApplyStatusPair(t,
                                CharacterAfflictions.STATUSTYPE.Injury, -0.025f,
                                CharacterAfflictions.STATUSTYPE.Poison, -0.025f);
                        else
                            HostApi.ApplyStatus(t, CharacterAfflictions.STATUSTYPE.Injury, -0.025f);
                        n++;
                    }
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·{rec.Effect}：{Nick(p)} 为半径 {radius:0.#} 米内 {n} 名玩家削减负面状态。");
                    return;
                }

                // 潘多拉之子·潘多拉：半径 5m 内其他玩家各「使用」一个潘多拉魔盒
                case ClassType.Pandora:
                {
                    const float radius = 5f;
                    int n = 0;
                    foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                    {
                        if (t == null || t == p) continue;      // 不含自己
                        if (IsDead(t)) continue;
                        if (IsTranscendent(t) || IsPandoraImmune(t) || IsMilesPandoraImmune(t)) continue;
                        GrantPandoraBox(t);
                        n++;
                    }
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·潘多拉：{Nick(p)} 对半径 {radius:0.#} 米内 {n} 名其他玩家施加潘多拉魔盒。");
                    return;
                }

                // 死灵法师·亡灵：半径 15m 内其他人类玩家 +中毒、骷髅玩家 +额外体力（不含自己）
                case ClassType.Necromancer:
                {
                    const float radius = 15f;
                    int n = 0;
                    foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                    {
                        if (t == null || t == p) continue;
                        if (IsDead(t)) continue;
                        if (t.data != null && t.data.isSkeleton)
                            HostApi.BoostExtraStamina(t, 0.025f);
                        else
                            HostApi.ApplyStatus(t, CharacterAfflictions.STATUSTYPE.Poison, 0.025f);
                        n++;
                    }
                    Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·亡灵：{Nick(p)} 光环影响半径 {radius:0.#} 米内 {n} 名玩家。");
                    return;
                }

                // M·耐受：自身削减 5 项负面状态（荆棘被原版状态数组通道排除，无法远端削减）
                case ClassType.M:
                    HostApi.ApplyStatusPair(p,
                        CharacterAfflictions.STATUSTYPE.Injury, -0.05f,
                        CharacterAfflictions.STATUSTYPE.Poison, -0.05f);
                    HostApi.ApplyStatusPair(p,
                        CharacterAfflictions.STATUSTYPE.Spores, -0.05f,
                        CharacterAfflictions.STATUSTYPE.Hot, -0.05f);
                    HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Cold, -0.05f);
                    return;

                // 野人·自然韧性 / 不拿拿
                case ClassType.Savage:
                    if (rec.Effect == "自然韧性")
                    {
                        HostApi.ApplyStatusPair(p,
                            CharacterAfflictions.STATUSTYPE.Poison, -0.025f,
                            CharacterAfflictions.STATUSTYPE.Spores, -0.025f);
                    }
                    else
                    {
                        GiveProgrammerCopyItem(p, ItemNames.BerrynanaPink, "开挂·不拿拿");
                    }
                    return;

                // 舞王·狂热舞者：定时强制自身后空翻
                case ClassType.Dancer:
                    ForceBackflip(p, "开挂·狂热舞者");
                    return;

                // 待皓·生产 / 我们全都是 / 脚滑
                case ClassType.Daihao:
                    if (rec.Effect == "脚滑")
                    {
                        ForceBackflip(p, "开挂·脚滑");
                        return;
                    }
                    if (rec.Effect == "生产")
                    {
                        GiveProgrammerCopyItem(p, ItemNames.NestEgg, "开挂·生产");
                        return;
                    }
                    {
                        const float radius = 50f;
                        int n = 0;
                        foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                        {
                            if (t == null || t == p) continue;
                            if (IsDead(t)) continue;
                            if (_ledger.GetClass(t) != ClassType.Daihao) continue;
                            n++;
                        }
                        if (n > 0)
                        {
                            HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Injury, -0.025f * n);
                            Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·我们全都是：{Nick(p)} 半径 {radius:0.#} 米内有 {n} 名待皓，损伤-{0.025f * n * 100f:0.#}");
                        }
                    }
                    return;

                // 圣人·完美主义（+额外体力）/ 教诲（半径 10m 内其他玩家 +额外体力、+困倦）
                case ClassType.Saint:
                    if (rec.Effect == "完美主义")
                    {
                        HostApi.BoostExtraStamina(p, 1f);
                        return;
                    }
                    {
                        const float radius = 10f;
                        int n = 0;
                        foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                        {
                            if (t == null || t == p) continue;
                            if (IsDead(t)) continue;
                            HostApi.BoostExtraStamina(t, 0.025f);
                            HostApi.ApplyStatus(t, CharacterAfflictions.STATUSTYPE.Drowsy, 0.10f);
                            n++;
                        }
                        Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·教诲：{Nick(p)} 对半径 {radius:0.#} 米内 {n} 名玩家 +额外体力 / +困倦。");
                    }
                    return;

                // 特级厨师·手暖炉（自身 +炎热）/ 破坏大师（自身 +额外体力）
                case ClassType.MasterChef:
                    if (rec.Effect == "手暖炉")
                        HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Hot, 0.025f);
                    else
                        HostApi.BoostExtraStamina(p, 0.10f);
                    return;

                // 区·区块链（自身清孢子）/ 好多区（按「虫儿数 + 附近区数」层数加成）
                case ClassType.Qu:
                    if (rec.Effect == "区块链")
                    {
                        HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Spores, -1f);
                        return;
                    }
                    {
                        int worms = CountItemInInventory(p, ItemNames.EarlyWorm);
                        int nearbyQu = 0;
                        const float radius = 30f;
                        foreach (var t in HostApi.GetCharactersInRadius(p.Center, radius))
                        {
                            if (t == null || t == p) continue;
                            if (IsDead(t)) continue;
                            if (_ledger.GetClass(t) == ClassType.Qu) nearbyQu++;
                        }
                        int stacks = Mathf.Min(4, worms + nearbyQu);
                        if (stacks <= 0) return;
                        HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Hunger, -0.025f * stacks);
                        HostApi.BoostExtraStamina(p, 0.025f * stacks);
                        Plugin.Log.LogInfo($"[RollOfTheScout] 开挂·好多区：{Nick(p)} 层数 {stacks}（虫儿 {worms} + 附近区 {nearbyQu}），饥饿-{0.025f * stacks * 100f:0.#}、额外体力+{0.025f * stacks * 100f:0.#}");
                    }
                    return;

                // 太空人·NASA（自身 −炎热 −寒冷）/ 轻飘飘（定时绑气球）
                case ClassType.Astronaut:
                    if (rec.Effect == "NASA")
                    {
                        HostApi.ApplyStatusPair(p,
                            CharacterAfflictions.STATUSTYPE.Hot, -0.05f,
                            CharacterAfflictions.STATUSTYPE.Cold, -0.05f);
                    }
                    else
                    {
                        TieBalloons(p, 2, "开挂·轻飘飘");
                    }
                    return;

                // 独狼·独行长路：附近无玩家 → +额外体力；否则 → +困倦
                case ClassType.LoneWolf:
                    if (HasOtherPlayerWithin(p, 15f))
                        HostApi.ApplyStatus(p, CharacterAfflictions.STATUSTYPE.Drowsy, 0.10f);
                    else
                        HostApi.BoostExtraStamina(p, 0.05f);
                    return;

                // 炮手·开炮（发放大炮）
                case ClassType.Gunner:
                    GiveProgrammerCopyItem(p, ItemNames.ScoutCannonItem, "开挂·开炮");
                    return;

                // 炊事员·后勤保障（发放便携烤炉）
                case ClassType.Cook:
                    GiveProgrammerCopyItem(p, ItemNames.PortableStove, "开挂·后勤保障");
                    return;

                // 法师·魔法卷轴（发放卷轴）
                case ClassType.Mage:
                    GiveProgrammerCopyItem(p, ItemNames.GuidebookScroll, "开挂·魔法卷轴");
                    return;

                // 榔王·精品大果（发放荔莓：绿 / 黄随机）
                case ClassType.Langwang:
                {
                    var pool = new List<WeightedItem>
                    {
                        new WeightedItem(ModConfig.Langwang.GreenBerryItem.Value, ModConfig.Langwang.GreenBerryWeight.Value),
                        new WeightedItem(ModConfig.Langwang.YellowBerryItem.Value, ModConfig.Langwang.YellowBerryWeight.Value),
                    };
                    pool.RemoveAll(w => string.IsNullOrWhiteSpace(w.PrefabName));
                    string berry = WeightedPicker.Pick(pool);
                    if (!string.IsNullOrEmpty(berry)) GiveProgrammerCopyItem(p, berry, "开挂·精品大果");
                    return;
                }
            }
        }

        /// <summary>该效果在「复制窗口」内是否需要续发（仅加速 / 无限体力）；其余为一次性，返回 null。</summary>
        private static string ProgrammerCopyRefreshKind(ClassType cls, string effect)
        {
            if (cls == ClassType.Firefighter && effect == "火线救援") return "Sprint";
            if (cls == ClassType.MilesSoul && effect == "封印解放") return "Infinite";
            if (cls == ClassType.Programmer && effect == "熬夜") return "Sprint";
            return null;
        }

        /// <summary>续发间隔（秒）：加速按能量饮料时长（约 10 秒）、无限体力按 20 秒。</summary>
        private static float ProgrammerCopyRefreshCadence(string kind)
        {
            return kind == "Sprint" ? 9f : 20f;
        }

        /// <summary>【溢出】主机侧钩子：见 <see cref="HostApi.ExtraStaminaAdjust"/>。</summary>
        private float AdjustProgrammerOverflow(Character target, float amount)
        {
            if (target == null || amount <= 0f) return amount;
            if (!ModConfig.Programmer.OverflowEnabled.Value) return amount;
            if (_ledger.GetClass(target) != ClassType.Programmer) return amount;

            float limit = ModConfig.Programmer.OverflowLimit.Value / 100f;   // 100 点 → 1.0
            if (limit <= 0f) return amount;

            float cur = target.data != null ? target.data.extraStamina : 0f;
            float sum = cur + amount;
            if (sum <= limit) return amount;
            return Mathf.Repeat(sum, limit) - cur;
        }

    }
}
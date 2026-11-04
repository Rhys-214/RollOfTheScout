using System;
using Photon.Pun;
using UnityEngine;

namespace RollOfTheScout
{
    /// <summary>
    /// 主模组对「外置玩法模式」的公开桥接层。
    ///
    /// 设计目标：
    ///   · 附属模组（如潘多拉大乱斗）不直接改写主模组的私有账本 / 配置项；
    ///   · 主模组在内部读取这里的运行时状态，从而让附属模组以「更高优先级」覆盖
    ///     职业分配、潘多拉数值、转职、显示面板等行为；
    ///   · 未安装附属模组时所有开关保持默认，主模组行为与以前完全一致。
    ///
    /// 该类型只存在于主机内存中；附属模组与主模组同装时由附属模组写入。
    /// 客机不需要安装附属模组，仍通过事件 197 接收显示数据。
    /// </summary>
    public static class PandoraModeBridge
    {
        /// <summary>模式是否处于「准备期 / 大逃杀」阶段（第 1~5 关）。</summary>
        public static bool Active;

        /// <summary>是否暂停主模组的随机职业分配（整局保持，直到回到机场）。</summary>
        public static bool SuppressAssignments;

        /// <summary>职业分配面板是否改为显示存活状态。</summary>
        public static bool DisplayActive;

        /// <summary>是否禁止一切转职。</summary>
        public static bool ReclassDisabled;

        /// <summary>是否启用潘多拉数值覆盖。</summary>
        public static bool OverridesActive;

        /// <summary>潘多拉之子最大人数覆盖（-1 = 不覆盖）。</summary>
        public static int PandoraMaxPlayers = -1;

        /// <summary>潘多拉作用半径覆盖（米，-1 = 不覆盖）。</summary>
        public static float PandoraBoxRadius = -1f;

        /// <summary>潘多拉作用间隔覆盖（秒，-1 = 不覆盖）。</summary>
        public static float PandoraBoxIntervalSeconds = -1f;

        /// <summary>
        /// 进入新关卡事件（参数为 <see cref="HostClassManager.LevelSerial"/>）。
        /// 附属模组用它启动准备期；主模组在清空旧职业之后触发。
        /// </summary>
        public static event Action<int> NewLevelStarted;

        /// <summary>由主模组在进入新关卡时调用。仅供主模组内部使用。</summary>
        internal static void RaiseNewLevelStarted(int serial)
        {
            try
            {
                NewLevelStarted?.Invoke(serial);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 潘多拉模式：新关卡事件处理异常：{e.Message}");
            }
        }

        /// <summary>主模组内部读取：潘多拉最大人数。</summary>
        internal static int EffectivePandoraMaxPlayers(int original)
        {
            return OverridesActive && PandoraMaxPlayers >= 0 ? PandoraMaxPlayers : original;
        }

        /// <summary>主模组内部读取：潘多拉作用半径。</summary>
        internal static float EffectivePandoraBoxRadius(float original)
        {
            return OverridesActive && PandoraBoxRadius >= 0f ? PandoraBoxRadius : original;
        }

        /// <summary>主模组内部读取：潘多拉作用间隔。</summary>
        internal static float EffectivePandoraBoxIntervalSeconds(float original)
        {
            return OverridesActive && PandoraBoxIntervalSeconds > 0f
                ? PandoraBoxIntervalSeconds : original;
        }

        /// <summary>面板 / 广播使用的存活状态：0 = 存活，1 = 死亡，2 = 异常。</summary>
        public static int StatusOf(Character character)
        {
            if (character == null || character.data == null) return 2;
            if (character.data.dead) return 1;
            if (character.data.passedOut || character.data.fullyPassedOut) return 2;
            return 0;
        }

        /// <summary>面板 / 广播使用的状态名（固定中英双语，与主模组其它面板措辞一致）。</summary>
        public static string StatusName(int status)
        {
            if (status == 0) return "Alive · 存活";
            if (status == 1) return "Dead · 死亡";
            return "Error · 异常";
        }

        /// <summary>强制清空全部职业（保留物品与状态）。</summary>
        public static void ClearAllClasses()
        {
            HostClassManager.Instance?.ModeClearClasses();
        }

        /// <summary>是否存在该角色的职业账本条目（用于区分「重连玩家」与「新加入者」）。</summary>
        public static bool HasClassEntry(Character character)
        {
            return HostClassManager.Instance != null && HostClassManager.Instance.ModeHasClassEntry(character);
        }

        /// <summary>稳定标识（复用主模组账本缓存，避免每帧分配候选列表）。</summary>
        public static string StableIdOf(Character character)
        {
            return HostClassManager.Instance != null ? HostClassManager.Instance.ModeStableId(character) : null;
        }

        /// <summary>静默强制分配职业（不产生职业分配提示；绕过名额与权重）。</summary>
        public static bool ForceAssign(Character character, ClassType type)
        {
            return HostClassManager.Instance != null && HostClassManager.Instance.ModeForceAssign(character, type);
        }

        /// <summary>给目标施加一次主模组既有的「无敌（奶白金）」效果。</summary>
        public static void GrantInvincible(Character character, string reason)
        {
            HostClassManager.Instance?.ModeGrantInvincible(character, reason);
        }

        /// <summary>主机屏幕提示 + 强制广播给客机（不受「职业显示-网络同步」开关影响）。</summary>
        public static void ShowToast(string text)
        {
            HostClassManager.Instance?.ModeShowToast(text);
        }

        /// <summary>请求主模组尽快重发一次显示快照（存活状态变化 / 模式开关）。</summary>
        public static void RequestDisplaySync()
        {
            HostClassManager.Instance?.ModeRequestDisplaySync();
        }

        /// <summary>重置潘多拉效果的周期计时（准备期结束时调用）。</summary>
        public static void ResetPandoraTimers()
        {
            HostClassManager.Instance?.ModeResetPandoraTimers();
        }

        /// <summary>
        /// 生成一名童军领队并强制追击指定玩家若干秒。
        /// 本模式**不**使用主模组【众魂阻挠】的 4 名全场上限；返回 PhotonView 供调用方随时让其逃离。
        /// </summary>
        public static PhotonView SpawnScoutmaster(Character target, float distance, float chaseSeconds)
        {
            return HostClassManager.Instance != null
                ? HostClassManager.Instance.ModeSpawnScoutmaster(target, distance, chaseSeconds)
                : null;
        }

        /// <summary>
        /// 一局结束 / 回机场时复位运行时覆盖（不清空事件订阅者）。
        ///
        /// 注意：**不重置 <see cref="SuppressAssignments"/>**。该开关由附属模组在机场按下一局配置设置；
        /// 主模组每帧的 <c>EndRun</c> 若把它清掉，可能在「离开机场进入关卡」的同一帧与附属模组写入产生竞争，
        /// 导致主模组先分配了一次随机职业。未安装附属模组时它自然是默认 false。
        /// </summary>
        public static void Reset()
        {
            Active = false;
            DisplayActive = false;
            ReclassDisabled = false;
            OverridesActive = false;
            PandoraMaxPlayers = -1;
            PandoraBoxRadius = -1f;
            PandoraBoxIntervalSeconds = -1f;
        }
    }
}

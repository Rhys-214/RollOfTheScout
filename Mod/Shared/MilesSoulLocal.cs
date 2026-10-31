using System;
using System.Globalization;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RollOfTheScout.Shared
{
    public enum MilesLocalGemUsage
    {
        Unknown = 0,
        SelfUse = 1,
        FeedNormal = 2,
        FeedDowned = 3,
    }

    /// <summary>
    /// 主模组客机模式与附属模组共用的残魂本机结算。
    /// 主机仍负责职业分配、首拾减诅咒和众魂阻挠计数；本机只处理自己的宝石使用，
    /// 因而状态修改直接发生在属主端，不再经过主机旧副本的绝对写回。
    /// </summary>
    public sealed class MilesSoulLocal : MonoBehaviour, IOnEventCallback
    {
        public static MilesSoulLocal Instance { get; private set; }

        public const string ModId = "com.peakmodding.RollOfTheScout";
        public const byte EventCode = 197;
        public const byte KindClientHello = 7;
        public const byte KindGameplayState = 8;
        public const byte KindUsageReport = 9;
        public const byte KindClientReady = 10;

        public const string AmuletHealing = "Amulet_Healing";
        public const string AmuletSuperJump = "Amulet_SuperJump";
        public const string AmuletClone = "Amulet_Clone";
        public const string AmuletInfiniteStamina = "Amulet_InfiniteStamina";

        private const float UseDedupWindow = 0.3f;
        private const float ReduceDelay = 0.35f;
        private const float ZeroCorrectionInterval = 0.25f;
        /// <summary>配置 ACK 的重发间隔（秒）：必须小于主机的客户端超时（12 秒），用于保持「本机结算」资格。</summary>
        private const float ReadyAckInterval = 8f;

        private Action<string> _info;
        private Action<string> _warning;
        private bool _hasState;
        private int _classCode;
        private float _nextHello;
        /// <summary>上一次成功解析的状态串：内容未变时跳过 Split / 逐项解析（稳态零分配）。</summary>
        private string _lastStateData;
        private float _nextAckTime;
        private float _nextSceneCheck;
        private bool _cachedAirport;

        private float _healingPetrify;
        private float _healingHunger;
        private float _superJumpPetrify;
        private float _superJumpHunger;
        private float _superJumpStamina;
        private float _clonePetrify;
        private float _cloneHunger;
        private float _cloneStamina;
        private float _ambitionPetrify;
        private float _ambitionHunger;
        private float _ambitionStamina;
        private bool _ruleZeroAssist;
        private float _ruleZeroStamina;

        private string _guardGem;
        private float _guardUntil;
        private int _preUsePetrify = -1;
        private float _preUseAt;

        private sealed class PendingReduce
        {
            public int Amount;
            public float FireAt;
        }

        private PendingReduce _reduce;

        private sealed class PendingZero
        {
            public int Target;
            public float FireAt;
            public float Until;
            public float NextWrite;
            public float Stamina;
            public bool StaminaApplied;
        }

        private PendingZero _zero;

        public void Configure(Action<string> info, Action<string> warning)
        {
            _info = info;
            _warning = warning;
        }

        private void Awake()
        {
            Instance = this;
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // 场景判定按 0.5 秒缓存：原版取当前场景名不需要每帧执行。
            if (Time.time >= _nextSceneCheck)
            {
                _nextSceneCheck = Time.time + 0.5f;
                _cachedAirport = IsInAirport();
            }

            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || _cachedAirport)
            {
                ResetRun();
                return;
            }

            bool inGameplay = GameHandler.IsInGameplayScene;
            if (inGameplay && Time.time >= _nextHello)
            {
                _nextHello = Time.time + 2f;
                SendToMaster(KindClientHello, "1");
            }

            if (!inGameplay || !IsActiveFor(Character.localCharacter)) return;
            TickReduce();
            TickZero();
        }

        private static bool IsInAirport()
        {
            return SceneManager.GetActiveScene().name.IndexOf(
                "Airport", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ResetRun()
        {
            _hasState = false;
            _classCode = 0;
            _nextHello = 0f;
            _lastStateData = null;
            _nextAckTime = 0f;
            _guardGem = null;
            _guardUntil = 0f;
            _preUsePetrify = -1;
            _reduce = null;
            _zero = null;
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != EventCode || PhotonNetwork.IsMasterClient) return;
            var master = PhotonNetwork.MasterClient;
            if (master == null || photonEvent.Sender != master.ActorNumber) return;

            try
            {
                if (!(photonEvent.CustomData is object[] arr) || arr.Length < 3) return;
                if (!(arr[0] is string id) || id != ModId) return;
                if (Convert.ToByte(arr[1]) != KindGameplayState) return;
                ApplyGameplayState(arr[2] as string);
            }
            catch (Exception e)
            {
                _warning?.Invoke($"残魂本机状态同步失败：{e.Message}");
            }
        }

        private void ApplyGameplayState(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // 内容未变：跳过 Split / 解析，只按需补发一次 ACK（保持主机侧「本机结算」资格）。
            if (string.Equals(data, _lastStateData, StringComparison.Ordinal))
            {
                if (_classCode == 26 && Time.time >= _nextAckTime) SendReady();
                return;
            }

            string[] parts = data.Split('|');
            if (parts.Length != 14) return;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classCode)) return;

            float[] values = new float[12];
            int[] valueIndices = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13 };
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.TryParse(parts[valueIndices[i]], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out values[i])) return;
            }
            if (!int.TryParse(parts[12], out int ruleZero)) return;

            if (_classCode != classCode)
            {
                _reduce = null;
                _zero = null;
                _guardGem = null;
                _preUsePetrify = -1;
            }
            _classCode = classCode;
            _healingPetrify = values[0];
            _healingHunger = values[1];
            _superJumpPetrify = values[2];
            _superJumpHunger = values[3];
            _superJumpStamina = values[4];
            _clonePetrify = values[5];
            _cloneHunger = values[6];
            _cloneStamina = values[7];
            _ambitionPetrify = values[8];
            _ambitionHunger = values[9];
            _ambitionStamina = values[10];
            _ruleZeroAssist = ruleZero != 0;
            _ruleZeroStamina = values[11];
            _hasState = true;
            _lastStateData = data;

            // 只有确认职业与配置均已到达后才让主机切换到本机结算；此前仍由主机兜底。
            if (_classCode == 26) SendReady();
        }

        /// <summary>补发配置 ACK（带节流；职业或配置发生变化时会立即发送）。</summary>
        private void SendReady()
        {
            _nextAckTime = Time.time + ReadyAckInterval;
            SendToMaster(KindClientReady, "26");
        }

        private static void SendToMaster(byte kind, string data)
        {
            var master = PhotonNetwork.MasterClient;
            if (master == null) return;
            try
            {
                PhotonNetwork.RaiseEvent(
                    EventCode,
                    new object[] { ModId, kind, data },
                    new RaiseEventOptions { TargetActors = new[] { master.ActorNumber } },
                    SendOptions.SendReliable);
            }
            catch
            {
                // 下一次周期握手 / 使用报告会自然重试。
            }
        }

        public static bool TryParseUsageReport(string data, out string gemName, out int usage)
        {
            gemName = null;
            usage = 0;
            if (string.IsNullOrEmpty(data)) return false;
            int split = data.LastIndexOf('|');
            if (split <= 0 || split + 1 >= data.Length) return false;
            if (!int.TryParse(data.Substring(split + 1), out usage)) return false;
            gemName = data.Substring(0, split);
            return IsGem(gemName) && usage >= 0 && usage <= 3;
        }

        public static bool IsGem(string name)
        {
            return name == AmuletHealing || name == AmuletSuperJump
                || name == AmuletClone || name == AmuletInfiniteStamina;
        }

        private bool IsActiveFor(Character character)
        {
            if (!_hasState || _classCode != 26 || PhotonNetwork.IsMasterClient) return false;
            if (character == null || character != Character.localCharacter) return false;
            return character.photonView != null && character.photonView.IsMine;
        }

        public void SnapshotPreUse(Item item)
        {
            if (item == null) return;
            var holder = item.holderCharacter;
            if (!IsActiveFor(holder)) return;
            if (!TryGemName(item, out _)) return;
            if (_preUsePetrify >= 0 && Time.time - _preUseAt < 0.5f) return;
            _preUsePetrify = GetPetrify(holder);
            _preUseAt = Time.time;
        }

        public void NotifyGemUsed(Item item, Character holder, MilesLocalGemUsage usage,
            bool cloneSuccess = false)
        {
            if (item == null || !IsActiveFor(holder)) return;
            if (!TryGemName(item, out string gemName)) return;
            OnGemUsed(holder, gemName, usage, cloneSuccess);
        }

        public void NotifyCloneUsed(Character holder, bool success)
        {
            if (!success || !IsActiveFor(holder)) return;
            OnGemUsed(holder, AmuletClone, MilesLocalGemUsage.SelfUse, cloneSuccess: true);
        }

        private static bool TryGemName(Item item, out string gemName)
        {
            gemName = null;
            if (item == null || !ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null)
                return false;
            gemName = prefab.gameObject.name;
            return IsGem(gemName);
        }

        private void OnGemUsed(Character character, string gemName, MilesLocalGemUsage usage,
            bool cloneSuccess)
        {
            if (gemName == AmuletClone && !cloneSuccess) return;
            if (_guardGem == gemName && Time.time < _guardUntil) return;
            _guardGem = gemName;
            _guardUntil = Time.time + UseDedupWindow;

            int preUse;
            bool hasPreUse = _preUsePetrify >= 0 && Time.time - _preUseAt <= 1.5f;
            preUse = hasPreUse ? _preUsePetrify : GetPetrify(character);
            _preUsePetrify = -1;

            if (gemName == AmuletHealing)
            {
                if (usage == MilesLocalGemUsage.FeedNormal)
                {
                    _info?.Invoke("童军的毅力：喂给未昏迷队友，无职业效果。");
                }
                else if (usage == MilesLocalGemUsage.FeedDowned && _ruleZeroAssist)
                {
                    if (!hasPreUse)
                    {
                        // 没有「使用前」基准就无法知道本次追加了多少石化：宁可不清，也不拿代价后的当前值当基准。
                        _warning?.Invoke("零号守则：未记录到使用前的石化值，本次无法等量清除（请检查宝石动作 / 喂食前缀补丁是否生效）。");
                    }
                    else
                    {
                        _zero = new PendingZero
                        {
                            Target = Mathf.Clamp(preUse, 0, 100),
                            FireAt = Time.time + ReduceDelay,
                            Until = Time.time + 3f,
                            Stamina = _ruleZeroStamina,
                        };
                    }
                }
                else
                {
                    AdjustStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -_healingHunger);
                    QueueReduce(_healingPetrify);
                }
            }
            else if (gemName == AmuletSuperJump)
            {
                AdjustStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -_superJumpHunger);
                QueueReduce(_superJumpPetrify);
                BoostStamina(character, _superJumpStamina);
            }
            else if (gemName == AmuletClone)
            {
                AdjustStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -_cloneHunger);
                QueueReduce(_clonePetrify);
                BoostStamina(character, _cloneStamina);
            }
            else if (gemName == AmuletInfiniteStamina)
            {
                AdjustStatus(character, CharacterAfflictions.STATUSTYPE.Hunger, -_ambitionHunger);
                QueueReduce(_ambitionPetrify);
                BoostStamina(character, _ambitionStamina);
            }

            SendToMaster(KindUsageReport, gemName + "|" + (int)usage);
        }

        private void QueueReduce(float amount)
        {
            int points = Mathf.RoundToInt(Mathf.Max(0f, amount) * 100f);
            if (points <= 0) return;
            if (_reduce == null) _reduce = new PendingReduce();
            _reduce.Amount += points;
            _reduce.FireAt = Time.time + ReduceDelay;
        }

        private void TickReduce()
        {
            if (_reduce == null || Time.time < _reduce.FireAt) return;
            var character = Character.localCharacter;
            if (!IsActiveFor(character)) { _reduce = null; return; }

            int current = GetPetrify(character);
            int target = Mathf.Clamp(current - _reduce.Amount, 0, 100);
            if (target != current) character.data.SetPetrify(target);
            _reduce = null;
        }

        private void TickZero()
        {
            if (_zero == null || Time.time < _zero.FireAt) return;
            var character = Character.localCharacter;
            if (!IsActiveFor(character)) { _zero = null; return; }

            int current = GetPetrify(character);
            if (current > _zero.Target && Time.time >= _zero.NextWrite)
            {
                character.data.SetPetrify(_zero.Target);
                _zero.NextWrite = Time.time + ZeroCorrectionInterval;
                current = _zero.Target;
            }

            // 必须先确认石化已回到使用前基准，再发放额外体力，避免被 ClampStamina 顶掉。
            if (current <= _zero.Target && !_zero.StaminaApplied)
            {
                BoostStamina(character, _zero.Stamina);
                _zero.StaminaApplied = true;
            }

            if (Time.time >= _zero.Until)
            {
                // 窗口到期仍高于基准（同步过晚 / 持续外部石化）：强制收尾，避免 _zero 永久残留后无限压制石化。
                if (!_zero.StaminaApplied)
                {
                    BoostStamina(character, _zero.Stamina);
                    _zero.StaminaApplied = true;
                    _warning?.Invoke(
                        $"零号守则：石化未在 3 秒窗口内回到使用前基准（当前 {current} > 基准 {_zero.Target}），" +
                        "已在窗口结束时兜底发放额外体力（可能受石化上限影响）。");
                }
                _zero = null;
            }
        }

        private static int GetPetrify(Character character)
        {
            return character == null || character.data == null
                ? 0
                : Mathf.Clamp((int)character.data.petrifyAmount, 0, 100);
        }

        private static void AdjustStatus(Character character,
            CharacterAfflictions.STATUSTYPE type, float amount)
        {
            if (character == null || character.refs == null || character.refs.afflictions == null) return;
            if (Mathf.Approximately(amount, 0f)) return;
            // 属主端直接走原版非 RPC 路径，让 PushStatuses 把结果同步回主机。
            character.refs.afflictions.AdjustStatus(type, amount);
        }

        private static void BoostStamina(Character character, float amount)
        {
            if (character == null || Mathf.Approximately(amount, 0f)) return;
            character.MoraleBoost(amount, 1);
        }

        private static bool IsDowned(Character character)
        {
            return character != null && character.data != null
                && character.data.fullyPassedOut && !character.data.dead;
        }

        internal static MilesLocalGemUsage UsageOfFeedTarget(Character target)
        {
            return IsDowned(target) ? MilesLocalGemUsage.FeedDowned : MilesLocalGemUsage.FeedNormal;
        }
    }

    public static class MilesLocalClonePatch
    {
        private static readonly FieldInfo ItemField = AccessTools.Field(typeof(ItemActionBase), "item");

        public static void Postfix(Peak.Action_CloneSelectedItem __instance, bool success)
        {
            try
            {
                if (!success || MilesSoulLocal.Instance == null) return;
                var item = ItemField?.GetValue(__instance) as Item;
                if (item == null) return;
                MilesSoulLocal.Instance.NotifyGemUsed(item, item.holderCharacter,
                    MilesLocalGemUsage.SelfUse, cloneSuccess: true);
            }
            catch
            {
                // 与其它补丁一致：单个效果失败不阻断原版动作。
            }
        }
    }

    public static class MilesLocalGemUsePatch
    {
        private static readonly FieldInfo ItemField = AccessTools.Field(typeof(ItemActionBase), "item");

        public static void ToggleGemPrefix(Peak.AmuletBase __instance)
        {
            MilesSoulLocal.Instance?.SnapshotPreUse(__instance.item);
        }

        public static void ToggleGemPostfix(Peak.AmuletBase __instance)
        {
            MilesSoulLocal.Instance?.NotifyGemUsed(__instance.item, __instance.item.holderCharacter,
                MilesLocalGemUsage.SelfUse);
        }

        public static void HealingGemPrefix(Peak.Action_HealingGem __instance) => FromActionPrefix(__instance);
        public static void SuperJumpPrefix(Peak.Action_SuperJumpAmulet __instance) => FromActionPrefix(__instance);
        public static void HealingGemPostfix(Peak.Action_HealingGem __instance) => FromAction(__instance);
        public static void SuperJumpPostfix(Peak.Action_SuperJumpAmulet __instance) => FromAction(__instance);

        private static void FromActionPrefix(ItemActionBase action)
        {
            var item = ItemField?.GetValue(action) as Item;
            MilesSoulLocal.Instance?.SnapshotPreUse(item);
        }

        private static void FromAction(ItemActionBase action)
        {
            var item = ItemField?.GetValue(action) as Item;
            if (item == null) return;
            MilesSoulLocal.Instance?.NotifyGemUsed(item, item.holderCharacter,
                MilesLocalGemUsage.SelfUse);
        }
    }

    public static class MilesLocalFeedPatch
    {
        private static readonly FieldInfo BestCharacterField = AccessTools.Field(typeof(Interaction), "bestCharacter");

        private static Character FeedTarget()
        {
            if (Interaction.instance == null || BestCharacterField == null) return null;
            var target = BestCharacterField.GetValue(Interaction.instance) as CharacterInteractible;
            return target != null ? target.character : null;
        }

        public static void Prefix(Item __instance, out MilesLocalGemUsage __state)
        {
            __state = MilesLocalGemUsage.Unknown;
            if (__instance == null || !__instance.canUseOnFriend) return;
            var target = FeedTarget();
            if (target == null) return;
            __state = MilesSoulLocal.UsageOfFeedTarget(target);
            MilesSoulLocal.Instance?.SnapshotPreUse(__instance);
        }

        public static void Postfix(Item __instance, MilesLocalGemUsage __state)
        {
            if (__state == MilesLocalGemUsage.Unknown || __instance == null) return;
            MilesSoulLocal.Instance?.NotifyGemUsed(__instance, __instance.holderCharacter, __state);
        }
    }
}

using System.Collections.Generic;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace RollOfTheScout
{
    /// <summary>
    /// 主机 → 客机「职业显示」网络广播。
    ///
    /// 背景：职业账本只存在主机内存、原版也没有任何能携带任意文本到远端客户端 UI 的
    /// 网络接口，因此「职业分配面板」「职业效果提示（Toast）」「头顶职业前缀」原本只能作用在
    /// 主机自己的屏幕上。现在为「安装了本模组或附属模组 <c>com.peakmodding.RollOfTheScoutClient</c> 的客机」
    /// 补上这条通道：主机用 Photon 自定义事件（<see cref="EventCode"/>）把数据广播给其它客户端，
    /// 由接收端在**自己屏幕上**本地显示。
    ///
    /// 硬约束（不得违背）：
    /// · 只有主机（MasterClient）发送；客机侧只接收、从不发送（客机不得影响主机）；
    /// · 客机**不装任何模组时本广播被直接忽略**，因此「客机零安装」的主线不受影响；
    /// · 广播默认不发给发送者（<c>ReceiverGroup.Others</c>），主机自身始终用本地账本显示。
    ///
    /// 数据格式：<c>object[]{ ModId(string), Kind(byte), Data(string) }</c>。
    /// 之所以把结构化数据压成 <c>string</c>，是为规避 Photon 对混合类型数组 / 数组字段序列化的
    /// 不确定性 —— <c>string</c> 一定可序列化。
    ///
    /// · <see cref="KindClassSnapshot"/>：<c>Data</c> = <c>"actor=class;actor=class;…"</c>
    ///   （ActorNumber=ClassType 数值），接收端自行解析成「昵称：职业」与「头顶前缀」；
    /// · <see cref="KindToast"/>：<c>Data</c> = 提示原文（与主机左上角提示完全一致）。
    /// </summary>
    public static class ClassDisplayNet
    {
        /// <summary>与附属模组约定的模组标识，用于把本事件与其它模组的同码事件区分开。</summary>
        public const string ModId = "com.peakmodding.RollOfTheScout";

        /// <summary>自定义事件码。刻意避开 Trapped Luggage 使用的 199，避免两模组同房时串扰。</summary>
        public const byte EventCode = 197;

        /// <summary>Kind：职业分配快照（<c>"actor=class;…"</c>）。</summary>
        public const byte KindClassSnapshot = 1;

        /// <summary>Kind：职业效果提示（Toast 文本）。</summary>
        public const byte KindToast = 2;

        /// <summary>
        /// Kind：职业名对照表（<c>"code=名称;…"</c>）。
        ///
        /// 随每次快照一起下发，让**客机端不必内置职业名表**：主模组将来新增职业时，
        /// 装了附属模组（或只装主模组）的客机无需任何更新即可正确显示新职业名，
        /// 而不会显示成「无职业」。旧版附属模组不认识本 Kind，会直接忽略（不产生任何回归）。
        /// </summary>
        public const byte KindClassNames = 3;

        /// <summary>职业快照的保底重发间隔（秒）：保证中途加入的客机也能尽快收到，且实时跟随职业变动。</summary>
        public const float SnapshotIntervalSeconds = 2f;

        // ---------------------------------------------------------------
        // 发送（仅主机）
        // ---------------------------------------------------------------

        /// <summary>广播职业分配快照。仅主机、且「职业显示-网络同步」开启时发送。</summary>
        public static void BroadcastClassSnapshot(IList<KeyValuePair<int, int>> actorClassPairs)
        {
            if (!ShouldBroadcast()) return;
            if (actorClassPairs == null || actorClassPairs.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < actorClassPairs.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(actorClassPairs[i].Key).Append('=').Append(actorClassPairs[i].Value);
            }
            Send(KindClassSnapshot, sb.ToString());
        }

        /// <summary>广播「职业数值 → 职业名」对照表（只发当前快照里出现过的职业即可）。</summary>
        public static void BroadcastClassNames(IList<KeyValuePair<int, string>> codeToName)
        {
            if (!ShouldBroadcast()) return;
            if (codeToName == null || codeToName.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < codeToName.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(codeToName[i].Key).Append('=').Append(Sanitize(codeToName[i].Value));
            }
            Send(KindClassNames, sb.ToString());
        }

        /// <summary>把分隔符替换掉，避免职业名里的 <c>;</c> / <c>=</c> 破坏解析（职业名均为固定常量，实际不会命中）。</summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Replace(';', '_').Replace('=', '_');
        }

        /// <summary>广播一条职业效果提示（与主机左上角 Toast 文本一致）。</summary>
        public static void BroadcastToast(string text)
        {
            if (!ShouldBroadcast()) return;
            if (string.IsNullOrEmpty(text)) return;
            Send(KindToast, text);
        }

        private static bool ShouldBroadcast()
        {
            if (!ModConfig.General.DisplaySync.Value) return false;
            if (!PhotonNetwork.InRoom) return false;
            return PhotonNetwork.IsMasterClient;
        }

        private static void Send(byte kind, string data)
        {
            try
            {
                object[] payload = { ModId, kind, data };
                PhotonNetwork.RaiseEvent(
                    EventCode, payload,
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 职业显示网络广播失败（kind={kind}）：{e.Message}");
            }
        }

        // ---------------------------------------------------------------
        // 解析（接收端：本模组客机模式 / 附属模组共用同一格式）
        // ---------------------------------------------------------------

        /// <summary>把 <c>"actor=class;…"</c> 解析为 (ActorNumber, ClassType 数值) 列表（保持主机侧顺序）。</summary>
        public static List<KeyValuePair<int, int>> ParseSnapshot(string data)
        {
            var list = new List<KeyValuePair<int, int>>();
            if (string.IsNullOrEmpty(data)) return list;

            foreach (var part in data.Split(';'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (int.TryParse(part.Substring(0, eq), out int actor) &&
                    int.TryParse(part.Substring(eq + 1), out int cls))
                {
                    list.Add(new KeyValuePair<int, int>(actor, cls));
                }
            }
            return list;
        }

        /// <summary>把 <c>"code=名称;…"</c> 解析为 (职业数值, 职业名) 列表。</summary>
        public static List<KeyValuePair<int, string>> ParseClassNames(string data)
        {
            var list = new List<KeyValuePair<int, string>>();
            if (string.IsNullOrEmpty(data)) return list;

            foreach (var part in data.Split(';'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (int.TryParse(part.Substring(0, eq), out int code) && eq + 1 < part.Length)
                {
                    list.Add(new KeyValuePair<int, string>(code, part.Substring(eq + 1)));
                }
            }
            return list;
        }
    }
}

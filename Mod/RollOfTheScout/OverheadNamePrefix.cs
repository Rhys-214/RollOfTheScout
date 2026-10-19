using System;
using UnityEngine;

namespace RollOfTheScout
{
    /// <summary>
    /// 「头顶职业前缀」的每 <see cref="PlayerName"/> 状态缓存（挂在对应 UI 对象上）。
    ///
    /// 性能 / 正确性要点：
    /// · 目标文本只在**输入变化**（职业名、昵称、皮肤色）时重建，之后每帧只做字符串比较；
    /// · 皮肤色按 <see cref="OverheadNameState.ColorViewId"/>（该名称当前对应的角色实例）缓存：**换人即重读**；
    ///   读取失败（装扮数据尚未同步）时**不缓存**，按 <see cref="ColorRetryAt"/> 限流重试，
    ///   避免「首次读到白色就永久是白色」以及「中途退出再加入后一直是白色」。
    /// </summary>
    internal sealed class OverheadNameState : MonoBehaviour
    {
        /// <summary>缓存的原始昵称（<c>Character.characterName</c>）；变化时重建目标文本。</summary>
        public string OriginalName;

        /// <summary>上次用于构建目标文本的职业名（null = 无职业）。</summary>
        public string LastClassName;

        /// <summary>上次用于构建目标文本的皮肤色十六进制。</summary>
        public string LastColorHex;

        /// <summary>目标显示文本（含富文本色标签）；与目标不一致时才写回 TMP。</summary>
        public string DesiredText;

        /// <summary>皮肤色当前对应的角色实例（PhotonView ViewID）。换人时需重新读取。</summary>
        public int ColorViewId;

        /// <summary>皮肤色是否已成功读取（读取失败则稍后重试）。</summary>
        public bool ColorReady;

        /// <summary>缓存下来的皮肤色 HTML 十六进制（形如 <c>"RRGGBB"</c>）。</summary>
        public string ColorHex = FallbackColor;

        /// <summary>下次允许重试读取皮肤色的时刻。</summary>
        public float ColorRetryAt;

        public const string FallbackColor = "FFFFFF";
    }

    /// <summary>
    /// 把头顶玩家名称改写成「【职业】昵称」：
    ///   · 「【职业】」用该玩家的**皮肤色**（<c>CharacterCustomization.PlayerColor</c>）；
    ///   · 「昵称」部分不带任何标签 → 维持原版字体颜色；
    ///   · 无职业 / 未拿到职业数据时，恢复为原版纯昵称。
    ///
    /// 主模组（主机与「客机模式」）与附属模组各自持有一份本类实现（两模组必须可独立安装）。
    /// 职业名由调用方给出（主机用本局账本、客机用主机广播的名称表），因此**主模组新增职业时
    /// 客机无需更新**即可正确显示。
    /// </summary>
    public static class OverheadNamePrefix
    {
        /// <summary>皮肤色读取失败后的重试间隔（秒）。</summary>
        private const float ColorRetrySeconds = 0.5f;

        /// <summary>
        /// 对某个头顶名称对象应用（或恢复）职业前缀。
        /// </summary>
        /// <param name="pn">头顶名称 UI 对象（<c>UIPlayerNames.playerNameText[index]</c>）。</param>
        /// <param name="ch">该名称对应的角色（各客户端本地均有副本）。</param>
        /// <param name="classNameLookup">职业名查询：返回空字符串表示「无职业 / 未启用」。</param>
        public static void Apply(PlayerName pn, Character ch, Func<Character, string> classNameLookup)
        {
            if (pn == null || ch == null) return;

            var st = pn.GetComponent<OverheadNameState>();
            if (st == null) st = pn.gameObject.AddComponent<OverheadNameState>();

            string original = ch.characterName;
            if (!string.Equals(st.OriginalName, original))
            {
                st.OriginalName = original;
                st.DesiredText = null;
            }

            RefreshColor(st, ch);

            string className = null;
            if (classNameLookup != null)
            {
                try { className = classNameLookup(ch); }
                catch { className = null; }
            }
            if (string.IsNullOrEmpty(className)) className = null;

            // 仅在「职业名 / 昵称 / 皮肤色」变化时重建目标文本（避免每帧字符串分配）
            if (st.DesiredText == null
                || !string.Equals(st.LastClassName, className)
                || !string.Equals(st.LastColorHex, st.ColorHex))
            {
                st.LastClassName = className;
                st.LastColorHex = st.ColorHex;
                st.DesiredText = (className == null || string.IsNullOrEmpty(original))
                    ? original
                    : $"<color=#{st.ColorHex}>【{className}】</color>{original}";
            }

            var tmp = pn.text;
            if (tmp == null) return;
            if (!tmp.richText) tmp.richText = true;   // 保证 <color> 标签生效（纯文本显示不受影响）
            if (st.DesiredText != null && tmp.text != st.DesiredText) tmp.text = st.DesiredText;
        }

        /// <summary>
        /// 皮肤色：按「当前对应的角色实例」（PhotonView ViewID）缓存。
        /// 换人（例如中途退出再加入会得到新的角色实例 / ViewID，即使 ActorNumber 被复用）→ 重新读取；
        /// 读取失败（装扮数据尚未同步）→ 不缓存、限流重试，避免一直显示白色。
        /// </summary>
        private static void RefreshColor(OverheadNameState st, Character ch)
        {
            int viewId = ViewIdOf(ch);

            if (st.ColorReady && st.ColorViewId == viewId) return;   // 已就绪且是同一角色实例
            if (st.ColorViewId != viewId)
            {
                st.ColorViewId = viewId;
                st.ColorReady = false;
                st.ColorRetryAt = 0f;
            }
            if (Time.time < st.ColorRetryAt) return;

            try
            {
                st.ColorHex = ColorUtility.ToHtmlStringRGB(ch.refs.customization.PlayerColor);
                st.ColorReady = true;
            }
            catch
            {
                st.ColorHex = OverheadNameState.FallbackColor;
                st.ColorRetryAt = Time.time + ColorRetrySeconds;   // 装扮数据未就绪：稍后重试
            }
        }

        private static int ViewIdOf(Character ch)
        {
            return ch.photonView != null ? ch.photonView.ViewID : 0;
        }
    }
}

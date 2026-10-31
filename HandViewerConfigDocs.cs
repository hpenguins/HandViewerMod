using CesiumLoader.SDK.Configuration;

namespace HandViewerMod
{
    /// <summary>config.json 里各字段的多语言说明。</summary>
    internal static class HandViewerConfigDocs
    {
        internal static bool Apply(ModConfig config)
        {
            if (config == null) return false;
            bool changed = false;
            changed |= config.Describe("Enabled", D(
                "总开关：关闭后不查询也不显示手牌列表。",
                "總開關：關閉後不查詢也不顯示手牌清單。",
                "Master switch: disables hand queries and the hand list when off.",
                "全体スイッチ：オフにすると手札の取得と一覧表示を行いません。"));
            changed |= config.Describe("AutoRefresh", D(
                "手牌展开可见时自动查询；关闭后只在手动刷新时查询。",
                "手牌展開可見時自動查詢；關閉後只在手動重新整理時查詢。",
                "Automatically query while the hand is expanded; when off, only manual refreshes query.",
                "手札を展開して表示している間だけ自動取得します。オフでは手動更新時のみ取得します。"));
            changed |= config.Describe("ServiceUrl", D(
                "手牌查询服务地址，默认 https://astralpartycards.hiynet.com/ 。",
                "手牌查詢服務位址，預設 https://astralpartycards.hiynet.com/ 。",
                "Hand query service URL (default: https://astralpartycards.hiynet.com/).",
                "手札検索サービスの URL（既定: https://astralpartycards.hiynet.com/）。"));
            changed |= config.Describe("TimeoutSeconds", D(
                "单次查询超时秒数，范围 5–60，默认 20。",
                "單次查詢逾時秒數，範圍 5–60，預設 20。",
                "Query timeout in seconds (5–60, default 20).",
                "1 回の取得タイムアウト秒数（5～60、既定 20）。"));
            changed |= config.Describe("DoubleRow", D(
                "手牌列表使用双排显示。",
                "手牌清單使用雙排顯示。",
                "Show the hand list in two rows.",
                "手札一覧を 2 段で表示します。"));
            changed |= config.Describe("AutoCollapseSeconds", D(
                "手牌展开后自动收回的秒数；0 表示不自动收回，默认 5。",
                "手牌展開後自動收回的秒數；0 表示不自動收回，預設 5。",
                "Seconds before the hand list collapses automatically; 0 disables it (default 5).",
                "手札一覧を自動で閉じるまでの秒数。0 で無効（既定 5）。"));
            return changed;
        }

        private static ConfigDoc D(string zhHans, string zhHant, string en, string ja)
            => ConfigDoc.Of(zhHans, zhHant, en, ja);
    }
}

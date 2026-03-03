namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// SortStatsSummary の表示サイズ種別。
/// カードの表示幅（単一/比較枚数）に応じて切り替える。
/// </summary>
public enum StatsCardSize
{
    /// <summary>フルサイズ（単一モード PC）: 全統計項目を表示</summary>
    Full,

    /// <summary>中サイズ（比較2〜3枚 PC）: 時間・Cmp・Swp・Progress%</summary>
    Medium,

    /// <summary>小サイズ（比較4枚以上 / タブレット / スマホ）: 省略形で表示</summary>
    Small,
}

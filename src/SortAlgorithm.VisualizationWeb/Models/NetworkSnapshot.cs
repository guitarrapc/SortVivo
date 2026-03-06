namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// ソーティングネットワーク中の1つのコンパレータ。
/// Bitonic sort などのデータ非依存ソートアルゴリズムで、
/// 固定された比較順序（ワイヤペア）を表現する。
/// </summary>
public record NetworkComparator
{
    /// <summary>上側ワイヤインデックス（0-based）</summary>
    public int Wire1 { get; init; }

    /// <summary>下側ワイヤインデックス（0-based）</summary>
    public int Wire2 { get; init; }

    /// <summary>ステージ番号（並列実行可能なコンパレータのグループ番号、0-based）</summary>
    public int Stage { get; init; }
}

/// <summary>
/// Sorting Network チュートリアル用のネットワークスナップショット。
/// 各 TutorialStep に付属し、そのステップ時点のネットワーク状態を保持する。
/// </summary>
public record NetworkSnapshot
{
    /// <summary>ネットワークのワイヤ数（= 配列サイズ）</summary>
    public int WireCount { get; init; }

    /// <summary>全コンパレータのリスト（事前計算済み、不変）</summary>
    public NetworkComparator[] Comparators { get; init; } = [];

    /// <summary>現在処理中のコンパレータインデックス（-1 = なし）</summary>
    public int ActiveComparatorIndex { get; init; } = -1;

    /// <summary>
    /// 各コンパレータの処理結果（true = Swap 発火、false = Swap なし、null = 未処理）。
    /// インデックスは Comparators 配列のインデックスと対応する。
    /// </summary>
    public bool?[] ComparatorResults { get; init; } = [];

    /// <summary>各ワイヤの現在の値</summary>
    public int[] WireValues { get; init; } = [];
}

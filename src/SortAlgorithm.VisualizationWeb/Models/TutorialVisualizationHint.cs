namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// チュートリアルで利用可能な追加ビジュアライゼーションのヒント。
/// アルゴリズムごとに設定し、木表示などの代替表現を有効化する。
/// </summary>
public enum TutorialVisualizationHint
{
    /// <summary>追加表示なし（マーブルのみ）</summary>
    None,

    /// <summary>ヒープ木表示（二分ヒープを SVG ツリーで描画）</summary>
    HeapTree,

    /// <summary>三分ヒープ木表示（三分ヒープを SVG ツリーで描画）</summary>
    TernaryHeapTree,

    /// <summary>弱ヒープ木表示（二分木レイアウト + reverse bit による辺の区別）</summary>
    WeakHeapTree,

    /// <summary>非平衡 BST 表示（in-order rank × 深さのレイアウトで二分探索木を描画）</summary>
    BstTree,

    /// <summary>AVL 木表示（回転後の平衡 BST を描画。balance factor 付き）</summary>
    AvlTree,

    /// <summary>
    /// 値ベースのバケット分配表示。
    /// Pigeonhole sort: 値と 1 対 1 対応する穴にマーブルが落ちる様子を描画する。
    /// Counting sort / Bucket sort も対応予定（Phase A-3）。
    /// </summary>
    ValueBucket,

    /// <summary>
    /// 桁ベースの LSD バケット分配表示。
    /// LSD Radix sort (b=10): 10 個の十進数桁バケットを描画（TwoDigitDecimal 配列で 2 パス）。
    /// LSD Radix sort (b=4): 4 個の 2-bit グループバケットを描画。
    /// どちらの基数かは <see cref="AlgorithmMetadata.TutorialLsdRadix"/> で区別する。
    /// </summary>
    DigitBucketLsd,

    /// <summary>
    /// 桁ベースの MSD バケット分配表示（再帰的）。
    /// MSD Radix sort (b=10): 10 個の十進数桁バケットを描画し、各バケット内で再帰的に下位桁で分割。
    /// MSD Radix sort (b=4): 4 個の 2-bit グループバケットを描画し、再帰的に分割。
    /// 再帰範囲（start, length）を境界線で可視化する。
    /// </summary>
    DigitBucketMsd,

    /// <summary>
    /// ソーティングネットワーク図（ワイヤ＋コンパレータ）。
    /// Bitonic sort: データ非依存の比較ネットワークを水平ワイヤ＋垂直コンパレータで描画。
    /// 水平軸は時間（ステージ）、垂直軸はワイヤ（配列インデックス）。
    /// </summary>
    SortingNetwork,

    /// <summary>
    /// 再帰ツリー表示（分割統治の構造を木で描画）。
    /// Merge Sort / Quicksort: 分割統治構造をツリーとして表示し、「今どの部分問題を解いているか」を示す。
    /// ノードには部分配列の内容を表示し、処理中のノードをハイライトする。
    /// </summary>
    RecursionTree,

    /// <summary>
    /// Shell sort の h-spaced 部分列を色分けして表示するマーブルアノテーション拡張。
    /// 独立した代替ビューではなく MarbleRenderer の拡張として機能し、
    /// TutorialStep.ShellGap が non-null のとき各マーブルの下に部分列カラードットを表示する。
    /// Shell sort (Knuth / Sedgewick / Tokuda / Ciura / Lee) に使用。
    /// </summary>
    ShellGap,
}

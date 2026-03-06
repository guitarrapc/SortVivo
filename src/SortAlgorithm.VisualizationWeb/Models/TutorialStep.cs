namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// チュートリアル用の論理ステップ。SortOperation 1件に対応し、
/// ナラティブテキスト・ハイライト情報・配列スナップショットを保持する。
/// </summary>
public record TutorialStep
{
    /// <summary>対応する SortOperation のインデックス</summary>
    public int OperationIndex { get; init; }

    /// <summary>この操作を適用した後のメイン配列スナップショット</summary>
    public int[] ArraySnapshot { get; init; } = [];

    /// <summary>この操作を適用した後のバッファー配列スナップショット（BufferId → 配列）</summary>
    public Dictionary<int, int[]> BufferSnapshots { get; init; } = new();

    /// <summary>メイン配列でハイライトするインデックス</summary>
    public int[] HighlightIndices { get; init; } = [];

    /// <summary>バッファー配列でハイライトするインデックス（BufferId → インデックス配列）</summary>
    public Dictionary<int, int[]> BufferHighlightIndices { get; init; } = new();

    /// <summary>ハイライトの種類（マーブルの色に影響）</summary>
    public OperationType HighlightType { get; init; }

    /// <summary>Compare 操作の比較結果（正: 左 > 右、負: 左 &lt; 右、0: 等しい、null: Compare 以外）</summary>
    public int? CompareResult { get; init; }

    /// <summary>IndexWrite 操作の移動元インデックス（値が直前にあった位置、見つからない場合 -1、Write 以外は null）</summary>
    public int? WriteSourceIndex { get; init; }

    /// <summary>IndexWrite 操作で上書きされる前の値（Write 以外は null）</summary>
    public int? WritePreviousValue { get; init; }

    /// <summary>
    /// ヒープ木表示用のヒープ境界。0 ≤ i &lt; HeapBoundary がヒープ内のノード。
    /// Heap Sort 以外では null。抽出フェーズで Swap のたびに 1 減少する。
    /// </summary>
    public int? HeapBoundary { get; init; }

    /// <summary>
    /// WeakHeap の reverse bit 配列スナップショット（インデックス i → r[i]）。
    /// r[i] = false のとき distinguished child は 2i、true のとき 2i+1。
    /// WeakHeapSort 以外では null。
    /// </summary>
    public bool[]? WeakHeapReverseBits { get; init; }

    /// <summary>
    /// BST スナップショット。BinaryTreeSort のみ使用（他は null）。
    /// ビルドフェーズは挿入経路、走査フェーズはアクティブノードを保持する。
    /// </summary>
    public BstSnapshot? Bst { get; init; }

    /// <summary>
    /// Distribution Sorts チュートリアル用のバケット分配スナップショット。
    /// TutorialVisualizationHint が ValueBucket / DigitBucketLsd / DigitBucketMsd のとき使用（他は null）。
    /// </summary>
    public DistributionSnapshot? Distribution { get; init; }

    /// <summary>
    /// Sorting Network チュートリアル用のネットワークスナップショット。
    /// TutorialVisualizationHint が SortingNetwork のとき使用（他は null）。
    /// </summary>
    public NetworkSnapshot? Network { get; init; }

    /// <summary>
    /// Recursion Tree チュートリアル用の再帰構造スナップショット。
    /// TutorialVisualizationHint が RecursionTree のとき使用（他は null）。
    /// Merge Sort / Quicksort などの分割統治アルゴリズムの再帰構造を木として可視化する。
    /// </summary>
    public RecursionSnapshot? Recursion { get; init; }

    /// <summary>
    /// Shell sort の h-spaced 部分列色分け用のギャップ値。
    /// TutorialVisualizationHint が ShellGap のとき使用（他は null）。
    /// 各マーブルのインデックス i に対して i % ShellGap で部分列グループを決定する。
    /// </summary>
    public int? ShellGap { get; init; }

    /// <summary>この操作を日本語で説明するナラティブテキスト</summary>
    public string Narrative { get; init; } = string.Empty;
}

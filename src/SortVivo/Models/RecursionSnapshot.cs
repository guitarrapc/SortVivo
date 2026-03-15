namespace SortVivo.Models;

/// <summary>
/// 再帰ツリー表示用のスナップショット。
/// Merge Sort / Quicksort などの分割統治アルゴリズムの再帰構造を木として可視化する。
/// </summary>
public record RecursionSnapshot
{
    /// <summary>全ノードの情報</summary>
    public RecursionNode[] Nodes { get; init; } = [];

    /// <summary>現在処理中のノード ID（-1 = なし）</summary>
    public int ActiveNodeId { get; init; } = -1;
}

/// <summary>
/// 再帰ツリーの1ノード。部分配列の範囲と状態を保持する。
/// </summary>
public record RecursionNode
{
    /// <summary>ノード ID（0 = ルート）</summary>
    public int Id { get; init; }

    /// <summary>親ノード ID（-1 = ルート）</summary>
    public int ParentId { get; init; } = -1;

    /// <summary>メイン配列上の開始インデックス</summary>
    public int Start { get; init; }

    /// <summary>メイン配列上の終了インデックス（排他）</summary>
    public int End { get; init; }

    /// <summary>ノードの状態</summary>
    public RecursionNodeState State { get; init; }

    /// <summary>ピボット値（Quicksort のみ、他は null）</summary>
    public int? PivotValue { get; init; }

    /// <summary>DNF ポインタ: lt 境界 – [left, lt-1] &lt; pivot (QuickSort 3-way のみ)</summary>
    public int? Lt { get; init; }

    /// <summary>DNF ポインタ: i – 現在のスキャン位置、[lt, i-1] == pivot (QuickSort 3-way のみ)</summary>
    public int? I { get; init; }

    /// <summary>DNF ポインタ: gt 境界 – [gt+1, right] &gt; pivot (QuickSort 3-way のみ)</summary>
    public int? Gt { get; init; }

    /// <summary>ノード内の配列スナップショット</summary>
    public int[] Values { get; init; } = [];

    /// <summary>再帰の深さ（0 = ルート）</summary>
    public int Depth { get; init; }
}

/// <summary>
/// 再帰ノードの処理状態。
/// </summary>
public enum RecursionNodeState
{
    /// <summary>未処理（まだ到達していない）</summary>
    Pending,

    /// <summary>分割中 / パーティション中（処理開始）</summary>
    Active,

    /// <summary>マージ中（子ノードから親へのマージ）</summary>
    Merging,

    /// <summary>完了（ソート済み）</summary>
    Completed,

    /// <summary>InsertionSort に委譲されたリーフ範囲（ハイブリッドソート用）</summary>
    InsertionSortLeaf,

    /// <summary>HeapSort に委譲されたリーフ範囲（ハイブリッドソートの深さ制限超過用）</summary>
    HeapSortLeaf,
}

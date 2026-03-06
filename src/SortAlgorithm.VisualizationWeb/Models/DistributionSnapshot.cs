namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// Distribution Sorts チュートリアル用のバケット分配スナップショット。
/// 各 TutorialStep に付属し、そのステップ時点の論理バケット状態を保持する。
/// Phase A-1: Pigeonhole sort（ValueBucket）対応。
/// </summary>
public record DistributionSnapshot
{
    /// <summary>バケット数</summary>
    public int BucketCount { get; init; }

    /// <summary>各バケットのラベル（Pigeonhole: 値そのもの、LSD: 桁値など）</summary>
    public string[] BucketLabels { get; init; } = [];

    /// <summary>
    /// 各バケットに現在入っている要素の値リスト（バケットインデックス → 値配列）。
    /// Buckets[b][0] が最初に追加された要素（底）、Buckets[b][^1] が最後に追加された要素（天辺）。
    /// </summary>
    public int[][] Buckets { get; init; } = [];

    /// <summary>現在のフェーズ</summary>
    public DistributionPhase Phase { get; init; }

    /// <summary>ハイライト中のバケットインデックス（-1 = なし）</summary>
    public int ActiveBucketIndex { get; init; } = -1;

    /// <summary>
    /// ハイライト中のバケット内要素インデックス（Buckets[ActiveBucketIndex] 内のインデックス、-1 = なし）。
    /// Scatter では最後に追加された要素、Gather では取り出し中の要素を指す。
    /// </summary>
    public int ActiveElementInBucket { get; init; } = -1;

    /// <summary>
    /// パスラベル（表示用）。
    /// Phase A-2 以降のマルチパスで "ones digit" / "tens digit" などを設定する。
    /// </summary>
    public string PassLabel { get; init; } = string.Empty;

    /// <summary>
    /// 現在のパス番号（0-based）。
    /// Phase A-2 以降のマルチパス LSD で使用する。
    /// </summary>
    public int PassIndex { get; init; }

    /// <summary>
    /// 各バケット（値）の出現回数配列（Counting sort 用）。
    /// Phase == Count または Place のときにヒストグラム表示する。
    /// null = Counting sort 以外のアルゴリズム。
    /// </summary>
    public int[]? Counts { get; init; }

    /// <summary>
    /// 現在の再帰範囲（MSD 用）。
    /// start = 処理中の配列範囲の開始インデックス、length = 範囲の長さ。
    /// null = MSD 以外のアルゴリズム。
    /// </summary>
    public (int start, int length)? ActiveRange { get; init; }

    /// <summary>
    /// 現在の桁インデックス（MSD 用）。
    /// 最上位桁 = digitCount - 1、最下位桁 = 0。
    /// -1 = MSD 以外のアルゴリズム。
    /// </summary>
    public int DigitIndex { get; init; } = -1;
}

/// <summary>
/// Distribution Sort のフェーズ。
/// </summary>
public enum DistributionPhase
{
    /// <summary>要素をバケットに分配中</summary>
    Scatter,

    /// <summary>バケットから要素を回収中</summary>
    Gather,

    /// <summary>要素の出現回数をカウント中（Counting sort 用）</summary>
    Count,

    /// <summary>累積和を計算中（Counting sort 用、SortOperation として不可視）</summary>
    PrefixSum,

    /// <summary>累積和に基づいて配置中（Counting sort 用）</summary>
    Place,
}

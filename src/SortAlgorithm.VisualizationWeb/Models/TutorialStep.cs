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

    /// <summary>この操作を日本語で説明するナラティブテキスト</summary>
    public string Narrative { get; init; } = string.Empty;
}

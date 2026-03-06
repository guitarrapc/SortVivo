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
}

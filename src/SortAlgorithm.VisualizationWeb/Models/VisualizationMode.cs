namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// 可視化の表示モード
/// </summary>
public enum VisualizationMode
{
    /// <summary>棒グラフ表示（デフォルト）</summary>
    BarChart,
    
    /// <summary>円形表示</summary>
    Circular,
    
    /// <summary>螺旋表示（将来実装）</summary>
    Spiral,
    
    /// <summary>ドットプロット表示（将来実装）</summary>
    DotPlot,

    /// <summary>画像行表示（アップロード画像を行ごとに分割してソート）</summary>
    PictureRow
}

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
    PictureRow,

    /// <summary>画像列表示（アップロード画像を列ごとに分割してソート）</summary>
    PictureColumn,

    /// <summary>画像ブロック表示（アップロード画像を 2D グリッドのブロックに分割してソート）</summary>
    PictureBlock,

    /// <summary>不均衡和音表示（各要素の現在位置と整列後の位置を弦で結び、位置ずれを可視化）</summary>
    DisparityChords
}

using SortAlgorithm.Contexts;

namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// アルゴリズムのメタデータ
/// </summary>
public record AlgorithmMetadata
{
    /// <summary>アルゴリズムの表示名</summary>
    public required string Name { get; init; }
    
    /// <summary>アルゴリズムのカテゴリ</summary>
    public required string Category { get; init; }
    
    /// <summary>時間計算量（平均）</summary>
    public required string TimeComplexity { get; init; }
    
    /// <summary>最大要素数</summary>
    public required int MaxElements { get; init; }
    
    /// <summary>推奨要素数</summary>
    public required int RecommendedSize { get; init; }
    
    /// <summary>ソート実行デリゲート</summary>
    public required Action<Span<int>, ISortContext> SortAction { get; init; }
    
    /// <summary>説明</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>チュートリアルでの説明文（日本語、2〜3文）</summary>
    public string TutorialDescription { get; init; } = string.Empty;

    /// <summary>
    /// チュートリアルで使用する初期配列の種類。
    /// アルゴリズムの特性上デフォルト配列では教育効果が得られない場合に変更する。
    /// 実際の配列は <see cref="TutorialArrayTypeExtensions.ToArray"/> で取得する。
    /// </summary>
    public TutorialArrayType TutorialArrayType { get; init; } = TutorialArrayType.Default;

    /// <summary>
    /// true の場合、チュートリアルの対象外とする。
    /// デフォルト配列でも専用配列でも教育的に表示できないアルゴリズムに設定する。
    /// </summary>
    public bool ExcludeFromTutorial { get; init; } = false;
}

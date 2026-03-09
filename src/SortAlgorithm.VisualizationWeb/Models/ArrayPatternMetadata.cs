using SortAlgorithm.VisualizationWeb.Services;

namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// 配列生成パターンのメタデータ
/// </summary>
public record ArrayPatternMetadata
{
    /// <summary>パターンの表示名</summary>
    public required string Name { get; init; }

    /// <summary>パターンのカテゴリ</summary>
    public required string Category { get; init; }

    /// <summary>配列生成デリゲート</summary>
    public required Func<int, Random, int[]> Generator { get; init; }

    /// <summary>ローカライズ用パターID (JSON キー)</summary>
    public string PatternId { get; init; } = string.Empty;

    /// <summary>説明</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>ローカライズされたパターン名を返す。JSONにキーがなければNameにフォールバック。</summary>
    public string GetLocalizedName(LocalizationService l)
    {
        if (string.IsNullOrEmpty(PatternId)) return Name;
        var key = $"arrayPatterns.{PatternId}.name";
        var result = l[key];
        return result == key ? Name : result;
    }

    /// <summary>ローカライズされた説明文を返す。JSONにキーがなければDescriptionにフォールバック。</summary>
    public string GetLocalizedDescription(LocalizationService l)
    {
        if (string.IsNullOrEmpty(PatternId)) return Description;
        var key = $"arrayPatterns.{PatternId}.description";
        var result = l[key];
        return result == key ? Description : result;
    }
}

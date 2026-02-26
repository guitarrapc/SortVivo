namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// チュートリアルで使用する初期配列の種類を表す列挙型。
/// アルゴリズムの特性に合わせた配列を選択することで、教育効果を最大化する。
/// </summary>
public enum TutorialArrayType
{
    /// <summary>
    /// デフォルト配列: [5, 3, 8, 1, 9, 2, 7, 4]
    /// 全値1桁・重複なし・8要素。比較・交換ベースのアルゴリズムに適する。
    /// </summary>
    Default,

    /// <summary>
    /// 2桁十進数配列: [53, 57, 31, 36, 82, 85, 61, 48]
    /// 同じ十の位を持つペアを意図的に含む。
    /// LSD: 一の位 → 十の位 の2パスが明確に見える。
    /// MSD: 十の位でバケット分割後、各バケット内で一の位の再帰が見える。
    /// </summary>
    TwoDigitDecimal,

    /// <summary>
    /// 4要素配列: [4, 1, 3, 2]
    /// 計算量が超多項式・階乗級のアルゴリズム向け。
    /// 要素数を絞ることで操作数を現実的な範囲に収め、再帰構造や無駄な処理を見せる。
    /// </summary>
    FourElement,
}

/// <summary>
/// <see cref="TutorialArrayType"/> の拡張メソッド。
/// 配列の定義をここに集約し、呼び出し側は種類だけ指定すればよい。
/// </summary>
public static class TutorialArrayTypeExtensions
{
    /// <summary>
    /// 列挙値に対応する初期配列を返す。
    /// </summary>
    public static int[] ToArray(this TutorialArrayType type) => type switch
    {
        TutorialArrayType.TwoDigitDecimal => [53, 57, 31, 36, 82, 85, 61, 48],
        TutorialArrayType.FourElement     => [4, 1, 3, 2],
        _ => [5, 3, 8, 1, 9, 2, 7, 4],
    };
}

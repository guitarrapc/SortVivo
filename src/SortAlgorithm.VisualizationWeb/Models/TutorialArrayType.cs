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

    /// <summary>
    /// 32要素・複数 run 配列: 昇順 run 2本と降順 run 2本を交互に並べた値 1〜32 の配列。
    /// MIN_MERGE=32 以上のサイズを確保し、run 検知・逆順化・run 拡張・バッファーを使ったマージを
    /// チュートリアルで視覚的に確認できるようにする。TimSort・PowerSort・ShiftSort 向け。
    /// </summary>
    MultiRun,

    /// <summary>
    /// 純粋 QuickSort 系配列: [3, 7, 1, 9, 5, 2, 8, 4, 6]（9要素、値 1〜9、重複なし）
    /// 中央インデックス (4) の値が 5 （中間値）なので、最初の partition で左4要素・右4要素の
    /// 均等分割が発生する。InsertionSort への切り替えしきい値を持たない純粋 QuickSort 向け。
    /// （QuickSort / Median3 / Median9 / DualPivot / Stable）
    /// </summary>
    PartitionSort,

    /// <summary>
    /// ハイブリッド QuickSort 系配列: 1〜31 の値を均等分割向けに配置した 31 要素配列。
    /// 最大しきい値 30（IntroSort）を超えるため、全アルゴリズムで最初の partition が
    /// QuickSort として実行される。IntroSort の四分位 median-of-3 でインデックス 7/15/23 の値が
    /// 8/16/24 となり median=16 → 15/15 の完全均等分割が発生する。
    /// （BlockQuickSort / IntroSort / IntroSortDotnet / PDQSort / C++ std::sort）
    /// </summary>
    PartitionSortHybrid,
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
        // 昇降混在・不均一 run (5,4,6,3,4,4,3,3) × 8 run、値 2〜37 全32要素
        // run 長が不揃いなため TimSort の stack invariant 調整が多く発生する
        TutorialArrayType.MultiRun        => [3, 8, 15, 22, 31, 28, 20, 13, 5, 7, 12, 18, 24, 30, 37, 34, 25, 11, 14, 19, 26, 35, 32, 23, 16, 6, 9, 17, 27, 21, 10, 2],
        // 9要素・値1〜9・重複なし。中央 index=4 の値=5（中間値）→ 最初の partition で左4/右4の均等分割
        TutorialArrayType.PartitionSort   => [3, 7, 1, 9, 5, 2, 8, 4, 6],
        // 31要素・値1〜31。IntroSort の四分位 median-of-3 が index7=8, index15=16, index23=24 → median=16
        // → 最初の partition で左15/右15の完全均等分割。全 Hybrid 系の最大しきい値 30 を超える要素数
        TutorialArrayType.PartitionSortHybrid => [5, 1, 7, 3, 6, 2, 4, 8, 13, 9, 15, 11, 14, 10, 12, 16, 21, 17, 23, 19, 22, 18, 20, 24, 29, 25, 31, 27, 30, 26, 28],
        _ => [5, 3, 8, 1, 9, 2, 7, 4],
    };
}

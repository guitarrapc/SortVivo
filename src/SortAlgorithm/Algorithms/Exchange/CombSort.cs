using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 最適化されたCombsort。
/// BubbleSortの改良版で、ギャップシーケンスを使用して遠く離れた要素を比較・交換することで、小さい値が配列の後方にある「タートル問題」を解決します。
/// ShellSortの交換ソート版とも言えます。ギャップを1.3で割りながら縮小し、最終的にギャップ1のBubbleSortで仕上げます。
/// Comb11最適化により、ギャップが9または10の場合は11に調整されます。
/// <br/>
/// Optimized Comb Sort.
/// An improved version of BubbleSort that uses a gap sequence to compare and swap distant elements,
/// solving the "turtle problem" where small values are located at the end of the array.
/// Can be seen as an exchange sort version of ShellSort. The gap is reduced by dividing by 1.3,
/// eventually finishing with a gap-1 BubbleSort pass.
/// The Comb11 optimization adjusts gaps of 9 or 10 to 11.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Comb Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence with Shrink Factor 1.3:</strong> The gap must be reduced by dividing by 1.3 (or equivalently, multiplying by 10/13).
/// This shrink factor has been empirically determined to provide optimal performance.
/// Initial gap = ⌊n × 10/13⌋, where n is the array length.</description></item>
/// <item><description><strong>Comb11 Optimization:</strong> When the calculated gap equals 9 or 10, it must be adjusted to 11.
/// This prevents inefficient gap sequences and improves average-case performance.</description></item>
/// <item><description><strong>Gap Reduction Until 1:</strong> The algorithm must continue reducing the gap until it reaches 1.
/// When gap &lt; 1 after reduction, it must be set to 1 (not terminate prematurely).</description></item>
/// <item><description><strong>Final BubbleSort Pass:</strong> When gap = 1, the algorithm performs standard BubbleSort passes
/// until no swaps occur in a complete pass, ensuring the array is fully sorted.</description></item>
/// <item><description><strong>Gap-based Comparison:</strong> For each gap h, compare and swap elements at positions i and i+h
/// for all valid i (0 ≤ i &lt; n-h), moving from left to right.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Exchange</description></item>
/// <item><description>Stable      : No (long-distance swaps do not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n log n) - Already sorted, but all gap passes must still be performed</description></item>
/// <item><description>Average case: Θ(n²/2^p) where p is the number of increments - Empirically O(n log n) with shrink factor 1.3</description></item>
/// <item><description>Worst case  : O(n²) - Certain input patterns can degrade to quadratic time</description></item>
/// <item><description>Comparisons : O(n log n) average - Each gap h performs (n-h) comparisons, summed over all gaps</description></item>
/// <item><description>Swaps       : O(n log n) average - Significantly fewer than BubbleSort due to gap sequence</description></item>
/// <item><description>Gap sequence: ⌊n × 10/13⌋, ⌊h × 10/13⌋, ..., 11, 8, 6, 4, 3, 2, 1 (with Comb11 optimization)</description></item>
/// </list>
/// <para><strong>Comparison with Related Algorithms:</strong></para>
/// <list type="bullet">
/// <item><description>vs BubbleSort: Much faster due to gap sequence eliminating turtles early</description></item>
/// <item><description>vs ShellSort: Similar concept but uses swaps instead of insertions; generally slightly slower</description></item>
/// <item><description>vs QuickSort: Simpler but slower on average; more predictable performance</description></item>
/// </list>
/// <para><strong>Implementation Optimizations:</strong></para>
/// <list type="number">
/// <item><description>Loop-Head Gap Update</description></item>
/// <item><description>Last Swap Index Optimization (Bubble Phase)</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Comb_sort</para>
/// </remarks>
public static class CombSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array

    /// <summary>
    /// Sorts the elements in the specified span in ascending order using the default comparer.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="span">The span of elements to sort in place.</param>
    public static void Sort<T>(Span<T> span) where T : IComparable<T>
        => Sort(span, new ComparableComparer<T>(), NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        var len = s.Length;
        var gap = len;
        var bubbleEnd = len;
        var swapped = true;

        while (gap != 1 || swapped)
        {
            gap = gap * 10 / 13;
            if (gap < 1) gap = 1;

            // Apply Comb11 optimization
            if (gap == 9 || gap == 10)
            {
                gap = 11;
            }

            swapped = false;

            if (gap == 1)
            {
                // Final bubble sort pass with last swap index optimization
                context.OnPhase(SortPhase.CombBubblePass, bubbleEnd);
                var newN = 0;
                for (var i = 0; i + 1 < bubbleEnd; i++)
                {
                    if (s.Compare(i, i + 1) > 0)
                    {
                        s.Swap(i, i + 1);
                        swapped = true;
                        newN = i + 1;
                    }
                }
                bubbleEnd = newN;
            }
            else
            {
                // Gap-based comb sort pass
                context.OnPhase(SortPhase.CombGapPass, gap, len);
                var end = len - gap;
                for (var i = 0; i < end; i++)
                {
                    if (s.Compare(i, i + gap) > 0)
                    {
                        s.Swap(i, i + gap);
                        swapped = true;
                    }
                }
            }
        }
    }
}

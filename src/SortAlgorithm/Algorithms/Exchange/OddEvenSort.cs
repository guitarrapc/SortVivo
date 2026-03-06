using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// BubbleSortの改良版で、奇数-偶数ペアと偶数-奇数ペアを交互に比較・交換することで、並列化可能なソートアルゴリズムを実現します。
/// 各ペアは独立しているため、並列実行が可能で、理論上は並列環境でO(n)時間で完了できます。安定な内部ソートです。
/// <br/>
/// An improvement over BubbleSort that alternately compares and swaps odd-even pairs and even-odd pairs,
/// enabling a parallelizable sorting algorithm. Since each pair is independent, parallel execution is possible,
/// and theoretically can complete in O(n) time in a parallel environment. This is a stable in-place sort.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Odd-Even Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Alternating Phase Structure:</strong> The algorithm must alternate between two distinct phases:
/// <list type="bullet">
/// <item><description>Odd-Even Phase: Compare and swap pairs (0,1), (2,3), (4,5), ... (indices i, i+1 where i is even)</description></item>
/// <item><description>Even-Odd Phase: Compare and swap pairs (1,2), (3,4), (5,6), ... (indices i, i+1 where i is odd)</description></item>
/// </list>
/// Each phase must process all applicable pairs before moving to the next phase.</description></item>
/// <item><description><strong>Termination Condition:</strong> The algorithm continues alternating phases until a complete pass
/// (both odd-even and even-odd phases) occurs with no swaps. This guarantees the array is fully sorted.
/// Setting sorted = true at the start of each iteration and only setting it to false when a swap occurs ensures correct termination.</description></item>
/// <item><description><strong>Comparison and Swap Logic:</strong> For each pair (i, i+1), if elements are out of order (i > i+1),
/// they must be swapped. This maintains stability since only strictly greater elements are moved.</description></item>
/// <item><description><strong>Parallel Execution (Optional):</strong> Within each phase, all pairs can be processed in parallel
/// since they operate on non-overlapping indices. In sequential execution, the algorithm still maintains correctness
/// by processing pairs left-to-right within each phase.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Exchange</description></item>
/// <item><description>Stable      : Yes (only swaps strictly out-of-order elements, preserving relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n) - Already sorted, one pass (both phases) with n-1 comparisons and no swaps</description></item>
/// <item><description>Average case: Θ(n²) - Approximately n/2 to n passes, each with ~n/2 comparisons, totaling ~n²/2 comparisons</description></item>
/// <item><description>Worst case  : O(n²) - Reversed array requires ~n passes, each with ~n/2 comparisons, totaling ~n²/2 comparisons</description></item>
/// <item><description>Comparisons : n-1 (best) to ~n²/2 (average/worst) - Each pass compares approximately n/2 pairs in each phase</description></item>
/// <item><description>Swaps       : 0 (best) to n(n-1)/2 (worst) - Reversed array swaps every compared out-of-order pair</description></item>
/// <item><description>Parallel time: O(n) - With unlimited processors, each phase takes O(1) time, and at most n phases are needed</description></item>
/// </list>
/// <para><strong>Comparison with Related Algorithms:</strong></para>
/// <list type="bullet">
/// <item><description>vs BubbleSort: Same worst-case complexity but better suited for parallel execution; sequential performance similar</description></item>
/// <item><description>vs Cocktail Sort: Cocktail Sort uses bidirectional passes; Odd-Even Sort uses alternating pair phases</description></item>
/// <item><description>vs Insertion Sort: Insertion Sort is typically faster in sequential execution for small arrays</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Odd%E2%80%93even_sort</para>
/// </remarks>
public static class OddEvenSort
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

        var sorted = false;
        int pass = 1;
        while (!sorted)
        {
            sorted = true;

            // Odd-even phase: compare pairs (0,1), (2,3), (4,5), ...
            context.OnPhase(SortPhase.OddEvenOddPhase, pass);
            for (var i = 0; i < s.Length - 1; i += 2)
            {
                if (s.Compare(i, i + 1) > 0)
                {
                    s.Swap(i, i + 1);
                    sorted = false;
                }
            }

            // Even-odd phase: compare pairs (1,2), (3,4), (5,6), ...
            context.OnPhase(SortPhase.OddEvenEvenPhase, pass);
            for (var i = 1; i < s.Length - 1; i += 2)
            {
                if (s.Compare(i, i + 1) > 0)
                {
                    s.Swap(i, i + 1);
                    sorted = false;
                }
            }
            pass++;
        }
    }
}

using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// ノームソートアルゴリズム、位置を記憶する最適化版。
/// 前回の位置を記憶することで、リストの先頭に戻る際の無駄な移動を削減します。この最適化により、挿入ソートと同程度の計算量を実現します。
/// ノームソートは「庭師のソート (Stupid sort)」とも呼ばれ、庭師が鉢植えを一つずつ並べ替えるように動作します。
/// <br/>
/// Gnome sort algorithm - optimized version with position memory.
/// By remembering the previous position, it reduces unnecessary movements when returning to the front. This optimization achieves computational complexity comparable to insertion sort.
/// Gnome sort, also known as "Stupid sort," works like a garden gnome arranging flower pots one by one.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Gnome Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Adjacent-only Comparison:</strong> The algorithm must only compare adjacent elements (i-1 and i).
/// This ensures the "step-by-step" nature of the gnome's movement and maintains stability.</description></item>
/// <item><description><strong>Sequential Swapping (Not Shifting):</strong> Elements must be moved via sequential swaps of adjacent pairs, not via shift operations.
/// This is the essential characteristic that distinguishes Gnome Sort from Insertion Sort. Using shift operations would transform it into Insertion Sort.</description></item>
/// <item><description><strong>Backward Movement on Disorder:</strong> When an out-of-order pair is found (arr[i-1] &gt; arr[i]), the algorithm must swap them and move backward (i--).
/// It continues moving backward until either reaching the beginning (i == 0) or finding a correctly ordered pair.</description></item>
/// <item><description><strong>Position Memory (Optimization):</strong> This optimized version remembers the previous position using an outer loop.
/// After backtracking to the correct position, it resumes from where it left off rather than restarting from the beginning.
/// This reduces redundant comparisons from O(n²) to the same level as Insertion Sort.</description></item>
/// <item><description><strong>Stability Preservation:</strong> Uses strict inequality (&gt;) rather than (≥) when comparing elements.
/// This ensures that equal elements maintain their original relative order, making it a stable sort.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion</description></item>
/// <item><description>Stable      : Yes (strict inequality preserves relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n) - Already sorted, only n-1 comparisons needed</description></item>
/// <item><description>Average case: Θ(n²) - Approximately n²/4 comparisons and swaps</description></item>
/// <item><description>Worst case  : O(n²) - Reverse sorted, exactly n(n-1)/2 swaps and 2×n(n-1)/2 = n(n-1) comparisons</description></item>
/// <item><description>Comparisons : Best: n-1 | Average: ~n²/2 | Worst: n(n-1) (each swap requires 2 compares: one before swap, one in while condition)</description></item>
/// <item><description>Swaps       : Best: 0 | Average: ~n²/4 | Worst: n(n-1)/2</description></item>
/// <item><description>Index Writes: Best: 0 | Average: ~n²/2 | Worst: n(n-1) (2 writes per swap)</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Gnome_sort</para>
/// </remarks>
public static class GnomeSort
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
    /// Sorts the elements in the specified span using a custom comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of the comparer. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer used to determine the order of elements.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        for (var i = 0; i < s.Length; i++)
        {
            context.OnPhase(SortPhase.GnomePass, i, s.Length - 1);
            while (i > 0 && s.Compare(i - 1, i) > 0)
            {
                s.Swap(i - 1, i);
                i--;
            }
        }
    }
}

/// <summary>
/// ノームソートアルゴリズム - 単純なwhileループ実装。
/// 最も基本的なノームソートの実装で、位置記憶などの最適化は行いません。実装は非常にシンプルですが、パフォーマンスは劣ります。
/// ノームソートは「庭師のソート (Stupid sort)」とも呼ばれ、庭師が鉢植えを一つずつ並べ替えるように動作します。
/// <br/>
/// Gnome sort algorithm - simple while loop implementation.
/// The most basic gnome sort implementation without optimizations like position memory. Very simple implementation but with inferior performance.
/// Gnome sort, also known as "Stupid sort," works like a garden gnome arranging flower pots one by one.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Gnome Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Adjacent-only Comparison:</strong> The algorithm must only compare adjacent elements (i-1 and i).
/// This ensures the "step-by-step" nature of the gnome's movement and maintains stability.</description></item>
/// <item><description><strong>Sequential Swapping (Not Shifting):</strong> Elements must be moved via sequential swaps of adjacent pairs, not via shift operations.
/// This is the essential characteristic that distinguishes Gnome Sort from Insertion Sort. Using shift operations would transform it into Insertion Sort.</description></item>
/// <item><description><strong>Backward Movement on Disorder:</strong> When an out-of-order pair is found (arr[i-1] &gt; arr[i]), the algorithm must swap them and move backward (i--).
/// It continues moving backward until either reaching the beginning (i == 0) or finding a correctly ordered pair.</description></item>
/// <item><description><strong>No Position Memory (Non-optimized):</strong> This basic version does not remember the previous position.
/// After backtracking to the beginning, it restarts from position 1, leading to redundant comparisons and reduced efficiency.</description></item>
/// <item><description><strong>Stability Preservation:</strong> Uses strict inequality (&gt;) rather than (≥) when comparing elements.
/// This ensures that equal elements maintain their original relative order, making it a stable sort.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion</description></item>
/// <item><description>Stable      : Yes (strict inequality preserves relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n) - Already sorted, only n-1 comparisons needed</description></item>
/// <item><description>Average case: Θ(n²) - Approximately n²/4 comparisons and swaps</description></item>
/// <item><description>Worst case  : O(n²) - Reverse sorted, exactly n(n-1)/2 swaps and 2×n(n-1)/2 = n(n-1) comparisons</description></item>
/// <item><description>Comparisons : Best: n-1 | Average: ~n²/2 | Worst: n(n-1) (each swap requires 2 compares: one before swap, one in while condition)</description></item>
/// <item><description>Swaps       : Best: 0 | Average: ~n²/4 | Worst: n(n-1)/2</description></item>
/// <item><description>Index Writes: Best: 0 | Average: ~n²/2 | Worst: n(n-1) (2 writes per swap)</description></item>
/// </list>
/// </remarks>
public static class GnomeSortNonOptimized
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

    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        var i = 0;
        while (i < s.Length)
        {
            context.OnPhase(SortPhase.GnomePass, i, s.Length - 1);
            if (i == 0 || s.Compare(i - 1, i) <= 0)
            {
                i++;
            }
            else
            {
                s.Swap(i, i - 1);
                --i;
            }
        }
    }
}

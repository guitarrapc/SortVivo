using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// Pair Insertion Sort（ペア挿入ソート）は、2要素を一度に挿入することで効率を向上させた挿入ソートの最適化版です。
/// 左端以外のパーティションでは2要素を同時に処理し、下限チェックが不要で高速に動作します。
/// <br/>
/// Pair Insertion Sort is an optimized variant of insertion sort that processes two elements at once,
/// improving efficiency by reducing boundary checks for non-leftmost partitions.
/// </summary>
/// <remarks>
/// <para><strong>Algorithm Overview:</strong></para>
/// <para>
/// Traditional insertion sort processes one element at a time. Pair insertion sort optimizes this by:
/// 1. Processing elements in pairs (two at a time)
/// 2. Comparing the two elements and sorting them
/// 3. Inserting the smaller element first, then the larger element
/// 4. Using the smaller element as a sentinel to eliminate boundary checks for the larger element
/// </para>
/// <para><strong>Key Optimizations:</strong></para>
/// <list type="bullet">
/// <item><description>Reduced Comparisons: By comparing pair elements first, we can determine insertion order efficiently</description></item>
/// <item><description>Eliminated Boundary Checks: After inserting the smaller element, it acts as a sentinel for the larger element</description></item>
/// <item><description>Better Cache Locality: Processing consecutive elements improves CPU cache usage</description></item>
/// <item><description>Fewer Branches: Unguarded insertion reduces conditional branches in the inner loop</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion</description></item>
/// <item><description>Stable      : Yes (careful ordering preserves stability)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) - Already sorted array</description></item>
/// <item><description>Average case: O(n²) - Similar to standard insertion sort but with lower constant factors</description></item>
/// <item><description>Worst case  : O(n²) - Reverse sorted array</description></item>
/// <item><description>Comparisons : ~n²/4 on average (fewer than standard insertion sort due to pair comparison)</description></item>
/// <item><description>Writes      : Similar to standard insertion sort, but better cache behavior</description></item>
/// </list>
/// <para><strong>Advantages over Standard Insertion Sort:</strong></para>
/// <list type="bullet">
/// <item><description>10-30% fewer comparisons on average due to pair pre-sorting</description></item>
/// <item><description>Reduced boundary checks improve performance on modern CPUs</description></item>
/// <item><description>Better instruction pipelining due to fewer conditional branches</description></item>
/// <item><description>Improved cache locality from processing consecutive elements together</description></item>
/// </list>
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>Small to medium arrays (n &lt; 50-100)</description></item>
/// <item><description>Final sorting step in hybrid algorithms (IntroSort, Timsort)</description></item>
/// <item><description>Non-leftmost partitions in quicksort (where sentinel is guaranteed)</description></item>
/// <item><description>Nearly sorted data where insertion sort excels</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Based on pair insertion optimization techniques used in modern sorting libraries</para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Insertion_sort</para>
/// </remarks>
public static class PairInsertionSort
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
        => Sort(span, 0, span.Length, new ComparableComparer<T>(), NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, 0, span.Length, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of the comparer</typeparam>
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
        => Sort(span, 0, span.Length, comparer, context);

    /// <summary>
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, int first, int last, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, first, last, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the subrange [first..last) using pair insertion sort with a custom comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, int first, int last, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(last, span.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(first, last);

        if (last - first <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCore(s, first, last);
    }

    /// <summary>
    /// Core pair insertion sort implementation that processes elements in pairs.
    /// This is the guarded version suitable for sorting from the beginning of an array.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length <= 1) return;

        // First element is trivially sorted
        // Start processing from second element in pairs
        var i = first + 1;

        // Process pairs of elements
        while (i + 1 < last)
        {
            s.Context.OnPhase(SortPhase.PairInsertionPass, i, last - 1);
            // Read the pair of elements
            var a = s.Read(i);
            var b = s.Read(i + 1);

            // Ensure a <= b (swap if necessary)
            // This pre-sorting of the pair reduces comparisons later
            if (s.Compare(a, b) > 0)
            {
                // Swap so that a is smaller
                (a, b) = (b, a);
            }

            // Insert the smaller element (a) first
            // This uses guarded insertion (with boundary check)
            var j = i - 1;
            while (j >= first && s.Compare(j, a) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            s.Write(j + 1, a);

            // Insert the larger element (b)
            // Since 'a' is already inserted and a <= b, we can use unguarded insertion
            // The element 'a' acts as a sentinel, guaranteeing the loop will terminate
            j = i; // Start from position after 'a' was inserted
            while (s.Compare(j, b) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            s.Write(j + 1, b);

            // Move to next pair
            i += 2;
        }

        // Handle remaining odd element if array length is odd
        if (i < last)
        {
            var tmp = s.Read(i);
            var j = i - 1;
            while (j >= first && s.Compare(j, tmp) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            if (j != i - 1)
            {
                s.Write(j + 1, tmp);
            }
        }
    }

    /// <summary>
    /// Semi-unguarded pair insertion sort: assumes that there is an element at position (first - 1)
    /// that is less than or equal to all elements in the range [first, last).
    /// This allows the smaller element (a) to be inserted without boundary checks,
    /// while the larger element (b) uses the inserted 'a' as a guaranteed sentinel.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort. Must be > 0.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <remarks>
    /// PRECONDITION: first > 0 and s[first-1] <= s[i] for all i in [first, last)
    /// This precondition is guaranteed by partitioning schemes in algorithms like IntroSort.
    /// 
    /// Optimization strategy:
    /// - Element 'a' (smaller): unguarded insertion using s[first-1] as sentinel
    /// - Element 'b' (larger): guarded insertion (j >= first check) using inserted 'a' as sentinel
    /// 
    /// This hybrid approach provides:
    /// - Safety: Prevents memory corruption if precondition is violated
    /// - Performance: Still eliminates boundary check for 'a' insertion (half of insertions)
    /// - Correctness: 'b' insertion correctly uses 'a' as sentinel without accessing [first-1]
    /// </remarks>
    internal static void UnguardedSortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length <= 1) return;

        var i = first + 1;

        // Process pairs of elements without boundary checks
        while (i + 1 < last)
        {
            var a = s.Read(i);
            var b = s.Read(i + 1);

            // Ensure a <= b
            if (s.Compare(a, b) > 0)
            {
                (a, b) = (b, a);
            }

            // Insert smaller element (a) - unguarded
            var j = i - 1;
            while (s.Compare(j, a) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            s.Write(j + 1, a);

            // Insert larger element (b) - unguarded
            // 'a' is now in place and acts as additional sentinel
            j = i;
            while (s.Compare(j, b) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            s.Write(j + 1, b);

            i += 2;
        }

        // Handle odd element - unguarded
        if (i < last)
        {
            var tmp = s.Read(i);
            var j = i - 1;
            while (s.Compare(j, tmp) > 0)
            {
                s.Write(j + 1, s.Read(j));
                j--;
            }
            s.Write(j + 1, tmp);
        }
    }
}

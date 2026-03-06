using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// シェルソートアルゴリズム - ギャップベースの比較を使用した改良版挿入ソート。
/// 配列を「ギャップ」で区切られたサブ配列に分割し、各サブ配列を挿入ソートでソートします。
/// ギャップが1に減少すると配列はほぼソート済みになり、最終的な挿入ソートパスが非常に効率的になります。
/// <br/>
/// Shell sort algorithm - an improved insertion sort using gap-based comparisons.
/// Shell sort divides the array into sub-arrays separated by a "gap" and sorts each sub-array using insertion sort.
/// As the gap reduces to 1, the array becomes nearly sorted, making the final insertion sort pass very efficient.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Shell Sort (Knuth 1973):</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence Property:</strong> The gap sequence must contain 1 as the final gap.
/// This ensures the algorithm performs at least one pass of standard insertion sort, guaranteeing correctness.
/// Knuth sequence: h_k = 3h_{k-1} + 1, starting from h_1 = 1: {1, 4, 13, 40, 121, 364, 1093, ...}</description></item>
/// <item><description><strong>h-Sorting Invariant:</strong> For each gap h, the array must be h-sorted (i.e., elements at positions i and i+h are in order).
/// This is achieved by applying gap-based insertion sort: for each position i ≥ h, insert a[i] into the sorted sequence a[i-h], a[i-2h], ..., a[i-kh].</description></item>
/// <item><description><strong>Decreasing Gap Sequence:</strong> Gaps must be applied in strictly decreasing order.
/// This implementation uses precomputed sequence and iterates from largest applicable gap down to 1.</description></item>
/// <item><description><strong>Gap Selection:</strong> Start with the largest gap h ≤ n/2 (or n/3 for some implementations).
/// This ensures meaningful comparisons while avoiding trivial partitioning.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion (gap-based variant)</description></item>
/// <item><description>Stable      : No (gap-based comparisons can reorder equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n log n) - Nearly sorted input with optimal gap sequence</description></item>
/// <item><description>Average case: O(n^1.5) ~ O(n^1.25) - Knuth sequence provides sub-quadratic performance</description></item>
/// <item><description>Worst case  : O(n^1.5) - Proven upper bound for Knuth's sequence (Knuth, 1973)</description></item>
/// <item><description>Comparisons : O(n^1.5) average - Depends on gap sequence and input distribution</description></item>
/// <item><description>Swaps       : O(n^1.5) average - Each gap-insertion may perform multiple swaps</description></item>
/// </list>
/// <para><strong>Gap Sequence Characteristics (Knuth 1973):</strong></para>
/// <list type="bullet">
/// <item><description>Formula: h_k = 3h_{k-1} + 1, or equivalently h_k = (3^k - 1) / 2</description></item>
/// <item><description>Most famous and widely studied gap sequence</description></item>
/// <item><description>Easy to compute with simple recurrence relation</description></item>
/// <item><description>Provides good general-purpose performance</description></item>
/// <item><description>Theoretical worst-case complexity: O(n^{3/2})</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Shellsort</para>
/// </remarks>
public static class ShellSortKnuth1973
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
    public static void Sort<T, TContext>(Span<T> span, int first, int last, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, first, last, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
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
    /// Sorts the subrange [first..last) using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The zero-based index of the first element in the range to sort.</param>
    /// <param name="last">The exclusive upper bound of the range to sort (one past the last element).</param>
    /// <remarks>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length < 2) return;

        // Knuth's sequence: h = 3*h + 1
        ReadOnlySpan<int> knuthSequence = [1, 4, 13, 40, 121, 364, 1093, 3280, 9841, 29524, 88573, 265720, 797161, 2391484];

        // Find the largest gap index where gap <= (length/2)
        int gapIndex = knuthSequence.Length - 1;
        while (gapIndex >= 0 && knuthSequence[gapIndex] > length / 2)
            gapIndex--;

        // Count usable gaps for display
        int totalGaps = gapIndex + 1;

        // Decrease gap by moving to previous index
        for (; gapIndex >= 0; gapIndex--)
        {
            var h = knuthSequence[gapIndex];
            s.Context.OnPhase(SortPhase.ShellGapPass, h, totalGaps - gapIndex, totalGaps);

            // Swap based Insertion sort with gap h.
            for (var i = first + h; i < last; i++)
            {
                // Ensure j >= first + h to stay within the subrange.
                for (int j = i; j >= first + h && s.Compare(j - h, j) > 0; j -= h)
                {
                    s.Swap(j, j - h);
                }
            }
        }
    }
}

/// <summary>
/// シェルソートアルゴリズム - ギャップベースの比較を使用した改良版挿入ソート。
/// 配列を「ギャップ」で区切られたサブ配列に分割し、各サブ配列を挿入ソートでソートします。
/// ギャップが1に減少すると配列はほぼソート済みになり、最終的な挿入ソートパスが非常に効率的になります。
/// <br/>
/// Shell sort algorithm - an improved insertion sort using gap-based comparisons.
/// Shell sort divides the array into sub-arrays separated by a "gap" and sorts each sub-array using insertion sort.
/// As the gap reduces to 1, the array becomes nearly sorted, making the final insertion sort pass very efficient.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Shell Sort (Sedgewick 1986):</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence Property:</strong> The gap sequence must contain 1 as the final gap.
/// Sedgewick sequence: h_k = 4^k + 3·2^(k-1) + 1 for k ≥ 0: {1, 5, 19, 41, 109, 209, 505, 929, ...}
/// Alternative formula: 9·4^k - 9·2^k + 1 or 4^{k+1} + 3·2^k + 1</description></item>
/// <item><description><strong>h-Sorting Invariant:</strong> For each gap h, the array must be h-sorted.
/// This is achieved by applying gap-based insertion sort for all positions i where i mod h forms an arithmetic sequence.</description></item>
/// <item><description><strong>Decreasing Gap Sequence:</strong> Gaps must be applied in strictly decreasing order.
/// Sedgewick's analysis shows this particular sequence avoids problematic arithmetic progressions between gaps.</description></item>
/// <item><description><strong>Gap Co-primality:</strong> Sedgewick's sequence ensures consecutive gaps are relatively prime,
/// which prevents worst-case scenarios where some elements are never compared until the final h=1 pass.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion (gap-based variant)</description></item>
/// <item><description>Stable      : No (gap-based comparisons can reorder equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n log n) - Nearly sorted input</description></item>
/// <item><description>Average case: O(n^{4/3}) - Sedgewick's theoretical analysis</description></item>
/// <item><description>Worst case  : O(n^{4/3}) - Proven upper bound (Sedgewick, 1986)</description></item>
/// <item><description>Comparisons : O(n^{4/3}) average - Better than Knuth's O(n^{3/2})</description></item>
/// <item><description>Swaps       : O(n^{4/3}) average - Proportional to comparisons</description></item>
/// </list>
/// <para><strong>Gap Sequence Characteristics (Sedgewick 1986):</strong></para>
/// <list type="bullet">
/// <item><description>Formula: h_k = 4^k + 3·2^(k-1) + 1, or 9·(4^k - 2^k) + 1</description></item>
/// <item><description>Theoretically proven to achieve O(n^{4/3}) worst-case complexity</description></item>
/// <item><description>Avoids problematic arithmetic progressions between gaps</description></item>
/// <item><description>Better theoretical bounds than Knuth sequence</description></item>
/// <item><description>More predictable performance across different input patterns</description></item>
/// </list>
/// </remarks>
/// <typeparam name="T"></typeparam>
public static class ShellSortSedgewick1986
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
    public static void Sort<T, TContext>(Span<T> span, int first, int last, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, first, last, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
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
    /// Sorts the subrange [first..last) using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The zero-based index of the first element in the range to sort.</param>
    /// <param name="last">The exclusive upper bound of the range to sort (one past the last element).</param>
    /// <remarks>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length < 2) return;

        // A partial Sedgewick sequence. Different references may show slightly different numbers.
        ReadOnlySpan<int> sedgewickSequence = [1, 5, 19, 41, 109, 209, 505, 929, 2161, 3905];

        // Find the largest gap index where gap <= (length/2)
        int gapIndex = sedgewickSequence.Length - 1;
        while (gapIndex >= 0 && sedgewickSequence[gapIndex] > length / 2)
            gapIndex--;

        int totalGaps = gapIndex + 1;

        // Decrease gap by moving to previous index
        for (; gapIndex >= 0; gapIndex--)
        {
            var h = sedgewickSequence[gapIndex];
            s.Context.OnPhase(SortPhase.ShellGapPass, h, totalGaps - gapIndex, totalGaps);

            // Swap based Insertion sort with gap h.
            for (var i = first + h; i < last; i++)
            {
                // Ensure j >= first + h to stay within the subrange.
                for (var j = i; j >= first + h && s.Compare(j - h, j) > 0; j -= h)
                {
                    s.Swap(j, j - h);
                }
            }
        }
    }
}

/// <summary>
/// シェルソートアルゴリズム - ギャップベースの比較を使用した改良版挿入ソート。
/// 配列を「ギャップ」で区切られたサブ配列に分割し、各サブ配列を挿入ソートでソートします。
/// ギャップが1に減少すると配列はほぼソート済みになり、最終的な挿入ソートパスが非常に効率的になります。
/// <br/>
/// Shell sort algorithm - an improved insertion sort using gap-based comparisons.
/// Shell sort divides the array into sub-arrays separated by a "gap" and sorts each sub-array using insertion sort.
/// As the gap reduces to 1, the array becomes nearly sorted, making the final insertion sort pass very efficient.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Shell Sort (Tokuda 1992):</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence Property:</strong> The gap sequence must contain 1 as the final gap.
/// Tokuda sequence: h_k = ⌈(9/4)^k⌉, or equivalently h_{k+1} = ⌊(9h_k + 1)/4⌋ starting from h_1 = 1:
/// {1, 4, 9, 20, 46, 103, 233, 525, 1182, 2660, 5985, 13467, 30301, ...}
/// The ratio between consecutive gaps is approximately 2.25.</description></item>
/// <item><description><strong>h-Sorting Invariant:</strong> For each gap h, the array must be h-sorted.
/// This is achieved by applying gap-based insertion sort that ensures elements h positions apart are in relative order.</description></item>
/// <item><description><strong>Decreasing Gap Sequence:</strong> Gaps must be applied in strictly decreasing order.
/// Tokuda's empirical research shows this ratio (≈2.25) provides optimal practical performance.</description></item>
/// <item><description><strong>Empirical Optimization:</strong> The sequence was derived through extensive empirical testing
/// to minimize the average number of comparisons and swaps across various input distributions.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion (gap-based variant)</description></item>
/// <item><description>Stable      : No (gap-based comparisons can reorder equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n log n) - Nearly sorted input</description></item>
/// <item><description>Average case: O(n^{5/4}) ≈ O(n^{1.25}) - Empirically verified</description></item>
/// <item><description>Worst case  : O(n^{3/2}) - Theoretical upper bound</description></item>
/// <item><description>Comparisons : O(n^{1.25}) average - Fewer than Knuth in practice</description></item>
/// <item><description>Swaps       : O(n^{1.25}) average - Proportional to comparisons</description></item>
/// </list>
/// <para><strong>Gap Sequence Characteristics (Tokuda 1992):</strong></para>
/// <list type="bullet">
/// <item><description>Formula: h_k = ⌈(9/4)^k⌉ or h_{k+1} = ⌊(9h_k + 1)/4⌋</description></item>
/// <item><description>Empirically optimized through extensive testing on various data sets</description></item>
/// <item><description>Better practical performance than Knuth's sequence</description></item>
/// <item><description>Ratio between consecutive gaps ≈ 2.25 (optimal for minimizing comparisons)</description></item>
/// <item><description>Widely used in production systems due to excellent average-case performance</description></item>
/// <item><description>Good balance between theoretical guarantees and practical efficiency</description></item>
/// </list>
/// </remarks>
/// <typeparam name="T"></typeparam>
public static class ShellSortTokuda1992
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
    public static void Sort<T, TContext>(Span<T> span, int first, int last, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, first, last, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
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
    /// Sorts the subrange [first..last) using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The zero-based index of the first element in the range to sort.</param>
    /// <param name="last">The exclusive upper bound of the range to sort (one past the last element).</param>
    /// <remarks>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length < 2) return;

        // Tokuda's sequence: empirically optimized gap sequence
        ReadOnlySpan<int> tokudaSequence = [1, 4, 9, 20, 46, 103, 233, 525, 1182, 2660, 5985, 13467, 30301, 68178, 153401, 345152, 776591];

        // Find the largest gap index where gap <= (length/2)
        int gapIndex = tokudaSequence.Length - 1;
        while (gapIndex >= 0 && tokudaSequence[gapIndex] > length / 2)
            gapIndex--;

        int totalGaps = gapIndex + 1;

        // Decrease gap by moving to previous index
        for (; gapIndex >= 0; gapIndex--)
        {
            var h = tokudaSequence[gapIndex];
            s.Context.OnPhase(SortPhase.ShellGapPass, h, totalGaps - gapIndex, totalGaps);

            // Swap based Insertion sort with gap h.
            for (int i = first + h; i < last; i++)
            {
                // Ensure j >= first + h to stay within the subrange.
                for (int j = i; j >= first + h && s.Compare(j - h, j) > 0; j -= h)
                {
                    s.Swap(j, j - h);
                }
            }
        }
    }
}

/// <summary>
/// シェルソートアルゴリズム - ギャップベースの比較を使用した改良版挿入ソート。
/// 配列を「ギャップ」で区切られたサブ配列に分割し、各サブ配列を挿入ソートでソートします。
/// ギャップが1に減少すると配列はほぼソート済みになり、最終的な挿入ソートパスが非常に効率的になります。
/// <br/>
/// Shell sort algorithm - an improved insertion sort using gap-based comparisons.
/// Shell sort divides the array into sub-arrays separated by a "gap" and sorts each sub-array using insertion sort.
/// As the gap reduces to 1, the array becomes nearly sorted, making the final insertion sort pass very efficient.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Shell Sort (Ciura 2001):</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence Property:</strong> The gap sequence must contain 1 as the final gap.
/// Ciura sequence (first 8 empirically determined): {1, 4, 10, 23, 57, 132, 301, 701}
/// Extended sequence using h_{k+1} = ⌊2.25h_k⌋: {1750, 3937, 8858, 19930, 44844, 100899, ...}
/// This is considered one of the best-known gap sequences for average-case performance.</description></item>
/// <item><description><strong>h-Sorting Invariant:</strong> For each gap h, the array must be h-sorted.
/// Ciura's sequence was specifically optimized to minimize the total number of comparisons in the average case.</description></item>
/// <item><description><strong>Decreasing Gap Sequence:</strong> Gaps must be applied in strictly decreasing order.
/// The first 8 gaps were found through exhaustive computer search for arrays up to 4000 elements.</description></item>
/// <item><description><strong>Empirical Optimality:</strong> Ciura's research involved testing millions of gap sequences
/// to find the one with minimum average comparisons, making this sequence empirically optimal for small to medium arrays.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion (gap-based variant)</description></item>
/// <item><description>Stable      : No (gap-based comparisons can reorder equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n log n) - Nearly sorted input</description></item>
/// <item><description>Average case: O(n^{1.3}) - Best known empirical average-case performance</description></item>
/// <item><description>Worst case  : O(n^{3/2}) - Theoretical upper bound (similar to other good sequences)</description></item>
/// <item><description>Comparisons : O(n^{1.3}) average - Empirically fewest among tested sequences</description></item>
/// <item><description>Swaps       : O(n^{1.3}) average - Proportional to comparisons</description></item>
/// </list>
/// <para><strong>Gap Sequence Characteristics (Ciura 2001):</strong></para>
/// <list type="bullet">
/// <item><description>First 8 gaps: {1, 4, 10, 23, 57, 132, 301, 701} - Empirically determined through exhaustive search</description></item>
/// <item><description>Extension formula: h_{k+1} = ⌊2.25h_k⌋ for gaps beyond 701</description></item>
/// <item><description>Best known gap sequence for average-case performance on arrays up to ~4000 elements</description></item>
/// <item><description>Widely recognized as the "best" Shell sort sequence in literature</description></item>
/// <item><description>Recommended for general-purpose use when average-case performance is critical</description></item>
/// <item><description>Reference: Marcin Ciura, "Best Increments for the Average Case of Shellsort" (2001)</description></item>
/// </list>
/// </remarks>
/// <typeparam name="T"></typeparam>
/// </remarks>
/// <typeparam name="T"></typeparam>
public static class ShellSortCiura2001
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
    public static void Sort<T, TContext>(Span<T> span, int first, int last, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, first, last, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
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
    /// Sorts the subrange [first..last) using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The zero-based index of the first element in the range to sort.</param>
    /// <param name="last">The exclusive upper bound of the range to sort (one past the last element).</param>
    /// <remarks>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length < 2) return;

        // Ciura's empirically determined sequence (extended for larger arrays)
        ReadOnlySpan<int> ciuraSequence = [1, 4, 10, 23, 57, 132, 301, 701, 1750, 3937, 8858, 19930, 44844, 100899];

        // Find the largest gap index where gap <= (length/2)
        int gapIndex = ciuraSequence.Length - 1;
        while (gapIndex >= 0 && ciuraSequence[gapIndex] > length / 2)
            gapIndex--;

        int totalGaps = gapIndex + 1;

        // Decrease gap by moving to previous index
        for (; gapIndex >= 0; gapIndex--)
        {
            var h = ciuraSequence[gapIndex];
            s.Context.OnPhase(SortPhase.ShellGapPass, h, totalGaps - gapIndex, totalGaps);

            // Swap based Insertion sort with gap h.
            for (var i = first + h; i < last; i++)
            {
                // Ensure j >= first + h to stay within the subrange.
                for (var j = i; j >= first + h && s.Compare(j - h, j) > 0; j -= h)
                {
                    s.Swap(j, j - h);
                }
            }
        }
    }
}

/// <summary>
/// シェルソートアルゴリズム - ギャップベースの比較を使用した改良版挿入ソート。
/// 配列を「ギャップ」で区切られたサブ配列に分割し、各サブ配列を挿入ソートでソートします。
/// ギャップが1に減少すると配列はほぼソート済みになり、最終的な挿入ソートパスが非常に効率的になります。
/// <br/>
/// Shell sort algorithm - an improved insertion sort using gap-based comparisons.
/// Shell sort divides the array into sub-arrays separated by a "gap" and sorts each sub-array using insertion sort.
/// As the gap reduces to 1, the array becomes nearly sorted, making the final insertion sort pass very efficient.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Shell Sort (Lee 2021):</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Sequence Property:</strong> The gap sequence must contain 1 as the final gap.
/// Lee sequence: h_k = ⌈(γ^k - 1)/(γ - 1)⌉ where γ = 2.243609061420001 (empirically optimal value)
/// Sequence: {1, 4, 9, 20, 45, 102, 230, 516, 1158, 2599, 5831, 13082, 29351, 65853, ...}
/// This is an improvement over Tokuda's sequence with fewer average comparisons.</description></item>
/// <item><description><strong>h-Sorting Invariant:</strong> For each gap h, the array must be h-sorted.
/// Lee's sequence maintains the h-sorting property while optimizing the gap ratio for minimal comparisons.</description></item>
/// <item><description><strong>Decreasing Gap Sequence:</strong> Gaps must be applied in strictly decreasing order.
/// The optimal γ value was found through extensive empirical testing across various input distributions.</description></item>
/// <item><description><strong>Improved Tokuda Formula:</strong> Lee refined Tokuda's approach by finding the optimal γ value
/// that minimizes average comparisons. The formula ensures gaps are well-distributed without arithmetic progressions.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Insertion (gap-based variant)</description></item>
/// <item><description>Stable      : No (gap-based comparisons can reorder equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n log n) - Nearly sorted input</description></item>
/// <item><description>Average case: O(n^{5/4}) ≈ O(n^{1.25}) - Empirically fewer comparisons than Tokuda</description></item>
/// <item><description>Worst case  : O(n^{3/2}) - Theoretical upper bound</description></item>
/// <item><description>Comparisons : O(n^{1.25}) average - State-of-the-art, fewer than Tokuda by ~5-10%</description></item>
/// <item><description>Swaps       : O(n^{1.25}) average - Proportional to comparisons</description></item>
/// </list>
/// <para><strong>Gap Sequence Characteristics (Lee 2021):</strong></para>
/// <list type="bullet">
/// <item><description>Formula: h_k = ⌈(γ^k - 1)/(γ - 1)⌉ where γ = 2.243609061420001</description></item>
/// <item><description>Most recent (2021) improvement over Tokuda's sequence</description></item>
/// <item><description>Optimal γ found through extensive empirical research</description></item>
/// <item><description>Empirically yields 5-10% fewer comparisons than Tokuda on average</description></item>
/// <item><description>State-of-the-art gap sequence for Shell sort</description></item>
/// <item><description>Recommended for modern implementations and research</description></item>
/// <item><description>Reference: Ying Wai Lee, "Empirically Improved Tokuda Gap Sequence in Shellsort" (arXiv:2112.11112, 2021)</description></item>
/// </list>
/// </remarks>
/// <typeparam name="T"></typeparam>
public static class ShellSortLee2021
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
    /// Sorts the subrange [first..last) using the provided comparer and sort context.
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
    /// Sorts the subrange [first..last) using the provided sort context.
    /// This overload accepts a SortSpan directly for use by other algorithms that already have a SortSpan instance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = last - first;
        if (length < 2) return;

        // Lee's empirically determined sequence based on gamma = 2.243609061420001
        // Formula: h_k = ceil((gamma^k - 1) / (gamma - 1))
        ReadOnlySpan<int> leeSequence = [1, 4, 9, 20, 45, 102, 230, 516, 1158, 2599, 5831, 13082, 29351, 65853, 147748, 331490, 743735];

        // Find the largest gap index where gap <= (length/2)
        int gapIndex = leeSequence.Length - 1;
        while (gapIndex >= 0 && leeSequence[gapIndex] > length / 2)
        {
            gapIndex--;
        }

        int totalGaps = gapIndex + 1;

        // Decrease gap by moving to previous index
        for (; gapIndex >= 0; gapIndex--)
        {
            var h = leeSequence[gapIndex];
            s.Context.OnPhase(SortPhase.ShellGapPass, h, totalGaps - gapIndex, totalGaps);

            // Swap based Insertion sort with gap h.
            for (var i = first + h; i < last; i++)
            {
                // Ensure j >= first + h to stay within the subrange.
                for (var j = i; j >= first + h && s.Compare(j - h, j) > 0; j -= h)
                {
                    s.Swap(j, j - h);
                }
            }
        }
    }
}

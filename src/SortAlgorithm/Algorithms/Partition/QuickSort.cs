using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// ピボット要素を基準に配列を2つの領域に分割し、各領域を再帰的にソートする分割統治法のソートアルゴリズムです。
/// 実用的なソートアルゴリズムの基礎として広く知られており、平均的には高速ですが最悪ケースでO(n²)の性能となります。
/// 本実装はDual-Pivotや3-way partitioningなどの高度な手法は使用せず、またInsertionSortなどの他アルゴリズムへの切り替えも行わない、ごく一般的なQuickSortです。
/// <br/>
/// A divide-and-conquer sorting algorithm that partitions the array into two regions based on a pivot element and recursively sorts each region.
/// Widely known as the foundation of practical sorting algorithms, it is fast on average but has O(n²) performance in the worst case.
/// This implementation is a basic QuickSort without advanced techniques such as dual-pivot or 3-way partitioning, and does not switch to other algorithms like InsertionSort.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct QuickSort:</strong></para>
/// <list type="number">
/// <item><description><strong>Pivot Selection:</strong> A pivot element is selected from the array.
/// This implementation uses the middle element of the range as the pivot.
/// While this is simple, it can lead to worst-case O(n²) performance on sorted or reverse-sorted arrays.
/// More sophisticated implementations use median-of-three, median-of-nine, or random pivot selection to reduce worst-case probability.</description></item>
/// <item><description><strong>Partitioning (Hoare Partition Scheme):</strong> The array is rearranged so that elements less than the pivot are on the left,
/// and elements greater than the pivot are on the right. This implementation uses Hoare's partitioning scheme:
/// <list type="bullet">
/// <item><description>Two pointers (i, j) start from opposite ends and move toward each other</description></item>
/// <item><description>Left pointer i advances while elements are less than pivot</description></item>
/// <item><description>Right pointer j retreats while elements are greater than pivot</description></item>
/// <item><description>When both pointers stop, elements at i and j are swapped</description></item>
/// <item><description>Process continues until pointers cross (i &gt; j)</description></item>
/// </list>
/// After partitioning, all elements in [left, j] are ≤ pivot, and all elements in [i, right] are ≥ pivot.
/// Note: Hoare's scheme does not guarantee the pivot ends up at its final position, unlike Lomuto's scheme.</description></item>
/// <item><description><strong>Recursive Division:</strong> The algorithm recursively sorts two independent subranges:
/// - Left region: [left, j]
/// - Right region: [i, right]
/// Base case: when right ≤ left, the range has ≤ 1 element and is trivially sorted.</description></item>
/// <item><description><strong>Termination:</strong> The algorithm terminates because:
/// - Each recursive call operates on a strictly smaller subarray (at least one element is partitioned out)
/// - The base case (right ≤ left) is eventually reached for all subarrays
/// - Maximum recursion depth: O(log n) on average, O(n) in worst case</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Partitioning (Divide and Conquer)</description></item>
/// <item><description>Partition   : Hoare partition scheme (bidirectional scan)</description></item>
/// <item><description>Stable      : No (partitioning does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack in average case, O(n) in worst case)</description></item>
/// <item><description>Best case   : Θ(n log n) - Balanced partitions (pivot divides array into two equal halves)</description></item>
/// <item><description>Average case: Θ(n log n) - Expected number of comparisons: 2n ln n ≈ 1.39n log₂ n</description></item>
/// <item><description>Worst case  : O(n²) - Occurs when pivot is always the smallest or largest element (e.g., sorted or reverse-sorted arrays with poor pivot selection)</description></item>
/// <item><description>Comparisons : 2n ln n (average) - Each partitioning pass compares elements with the pivot</description></item>
/// <item><description>Swaps       : n ln n / 3 (average) - Hoare's scheme performs fewer swaps than Lomuto's scheme</description></item>
/// </list>
/// <para><strong>Advantages of Hoare Partition Scheme:</strong></para>
/// <list type="bullet">
/// <item><description>Fewer swaps than Lomuto's scheme: approximately 3 times fewer on average</description></item>
/// <item><description>Better performance on arrays with many duplicate elements</description></item>
/// <item><description>Bidirectional scanning improves cache locality</description></item>
/// </list>
/// <para><strong>Disadvantages and Limitations:</strong></para>
/// <list type="bullet">
/// <item><description>Worst-case O(n²) performance on sorted or reverse-sorted arrays with poor pivot selection</description></item>
/// <item><description>Not stable: relative order of equal elements is not preserved</description></item>
/// <item><description>Recursion depth can be O(n) in worst case, risking stack overflow on large arrays</description></item>
/// <item><description>Poor performance on arrays with many duplicate elements (use 3-way partitioning instead)</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Quicksort</para>
/// </remarks>
public static class QuickSort
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
    /// Sorts the subrange [first..last) using the provided comparer and context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span containing elements to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context for tracking statistics and observations.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, int first, int last, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(last, span.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(first, last);

        if (last - first <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCore(s, first, last - 1);
    }

    /// <summary>
    /// Sorts the subrange [left..right] (inclusive) using the provided sort context.
    /// This overload accepts a SortSpan directly for use by other algorithms that already have a SortSpan instance.
    /// Uses Hoare's partitioning scheme with the middle element as pivot.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="left">The inclusive start index of the range to sort.</param>
    /// <param name="right">The inclusive end index of the range to sort.</param>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (right <= left) return;

        // Select pivot as the middle element
        var pivot = s.Read((left + right) / 2);

        // Hoare partition: two pointers moving from opposite ends
        var i = left;
        var j = right;

        while (i <= j)
        {
            // Move i forward while elements are less than pivot
            while (s.Compare(i, pivot) < 0)
            {
                i++;
            }

            // Move j backward while elements are greater than pivot
            while (s.Compare(pivot, j) < 0)
            {
                j--;
            }

            // Swap if pointers haven't crossed
            if (i <= j)
            {
                s.Swap(i, j);
                i++;
                j--;
            }
        }

        // Recursively sort left and right partitions
        // After partitioning, elements in [left..j] are not greater than the pivot region,
        // and elements in [i..right] are not less than the pivot region.
        if (left < j)
        {
            SortCore<T, TComparer, TContext>(s, left, j);
        }

        if (i < right)
        {
            SortCore<T, TComparer, TContext>(s, i, right);
        }
    }
}

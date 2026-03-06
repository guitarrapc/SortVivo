using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 2つのピボットを使用して配列を3つの領域に分割する分割統治法のソートアルゴリズムです。
/// 単一ピボットのQuickSortと比較して、より均等な分割により再帰の深さを浅くし、キャッシュ効率を高めることで高速化を実現します。
/// 本実装はVladimir Yaroslavskiy (2009)のDual-Pivot QuickSort論文に基づいています。
/// <br/>
/// A divide-and-conquer sorting algorithm that uses two pivots to partition the array into three regions.
/// Compared to single-pivot QuickSort, it achieves faster performance through more balanced partitioning, reducing recursion depth and improving cache efficiency.
/// This implementation is based on Vladimir Yaroslavskiy's (2009) Dual-Pivot QuickSort paper.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Dual-Pivot QuickSort:</strong></para>
/// <list type="number">
/// <item><description><strong>Pivot Selection and Ordering (Adaptive 5-Sample Method):</strong> Two pivots (p1, p2) are selected using an adaptive strategy:
/// <list type="bullet">
/// <item><description><strong>For arrays &lt; 47 elements:</strong> Use simple method (leftmost and rightmost elements as pivots, ensuring p1 ≤ p2)</description></item>
/// <item><description><strong>For arrays ≥47 elements:</strong> Use Java's proven 5-sample strategy:
/// <list type="bullet">
/// <item><description>Sample 5 elements at evenly distributed positions: left+length/7, left+2*length/7, middle, right-2*length/7, right-length/7</description></item>
/// <item><description>Sort these 5 elements using insertion sort (4-10 comparisons, same as Java's implementation)</description></item>
/// <item><description>Choose the 2nd smallest and 4th smallest as pivot1 and pivot2</description></item>
/// <item><description>This yields approximately 1/7, 3/7, 5/7 division ratios, close to the ideal 1/3, 2/3 for dual-pivot</description></item>
/// </list>
/// </description></item>
/// </list>
/// The pivots satisfy p1 ≤ p2 by construction. This method dramatically reduces worst-case probability and handles sorted/reverse-sorted data efficiently.</description></item>
/// <item><description><strong>Three-Way Partitioning (Neither Hoare nor Lomuto):</strong> This algorithm uses a specialized 3-way partitioning scheme designed for dual-pivot quicksort.
/// Unlike Hoare partition (bidirectional scan with two pointers) or Lomuto partition (single-direction scan),
/// this approach performs a left-to-right scan with three boundary pointers (l, k, g) to partition the array into three regions:
/// <list type="bullet">
/// <item><description>Left region: elements &lt; p1 (indices [left, l-1])</description></item>
/// <item><description>Middle region: elements where p1 ≤ element ≤ p2 (indices [l+1, g-1])</description></item>
/// <item><description>Right region: elements &gt; p2 (indices [g+1, right])</description></item>
/// </list>
/// The partitioning loop maintains these invariants:
/// - Elements in [left+1, l-1] are &lt; p1
/// - Elements in [l, k-1] are in [p1, p2]
/// - Elements in [g+1, right-1] are &gt; p2
/// - Element at index k is currently being examined
/// This is the standard dual-pivot partitioning method introduced by Yaroslavskiy.</description></item>
/// <item><description><strong>Pivot Placement:</strong> After partitioning, pivots are moved to their final positions:
/// - p1 is swapped with the element at position l (boundary of left region)
/// - p2 is swapped with the element at position g (boundary of right region)
/// This ensures pivots are correctly positioned between their respective regions.</description></item>
/// <item><description><strong>Recursive Division:</strong> The algorithm recursively sorts three independent regions:
/// - Left region: [left, l-1]
/// - Middle region: [l+1, g-1] (only if p1 &lt; p2, i.e., pivots are distinct)
/// - Right region: [g+1, right]
/// Base case: when right ≤ left, the region has ≤ 1 element and is trivially sorted.</description></item>
/// <item><description><strong>Termination:</strong> The algorithm terminates because:
/// - Each recursive call operates on a strictly smaller subarray (at least 2 elements are pivots)
/// - The base case (right ≤ left) is eventually reached for all subarrays
/// - Maximum recursion depth: O(log₃ n) on average, O(log n) worst case (tail recursion optimization: loop on largest partition, recurse on two smaller)</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Partitioning (Divide and Conquer)</description></item>
/// <item><description>Partition   : 3-way partition (Yaroslavskiy's method - neither Hoare nor Lomuto)</description></item>
/// <item><description>Stable      : No (partitioning does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack)</description></item>
/// <item><description>Best case   : Θ(n log₃ n) - Balanced partitions (each region ≈ n/3)</description></item>
/// <item><description>Average case: Θ(n log₃ n) - Expected number of comparisons: 1.9n ln n ≈ 1.37n log₂ n (vs 2n ln n for single-pivot)</description></item>
/// <item><description>Worst case  : O(n²) - Occurs when partitioning is highly unbalanced (rare with dual pivots)</description></item>
/// <item><description>Comparisons : 1.9n ln n (average) - Each element compared with both pivots during partitioning</description></item>
/// <item><description>Swaps       : 0.6n ln n (average) - Fewer swaps than single-pivot due to better partitioning</description></item>
/// </list>
/// <para><strong>Advantages over Single-Pivot QuickSort:</strong></para>
/// <list type="bullet">
/// <item><description>More balanced partitions: log₃ n vs log₂ n recursion depth (≈37% reduction)</description></item>
/// <item><description>Fewer comparisons on average: 1.9n ln n vs 2n ln n (≈5% reduction)</description></item>
/// <item><description>Better cache locality: three regions fit better in CPU cache than two</description></item>
/// <item><description>Lower probability of worst-case behavior: dual pivots provide better sampling</description></item>
/// </list>
/// <para><strong>Yaroslavskiy 2009 Optimizations Implemented:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Insertion Sort Fallback (TINY_SIZE = 17):</strong> Arrays smaller than 17 elements are sorted using insertion sort for better constant-factor performance.</description></item>
/// <item><description><strong>5-Sample Pivot Selection:</strong> For arrays ≥47 elements, uses 5-sample method to select pivots, reducing worst-case probability.</description></item>
/// <item><description><strong>Inner While Loop:</strong> Partitioning uses inner while loop to scan from right when element &gt; pivot2, matching Yaroslavskiy's specification.</description></item>
/// <item><description><strong>Equal Elements Optimization (DIST_SIZE = 13):</strong> When center region is large (&gt; length - 13) and pivots are different,
/// segregates elements equal to pivots from the center region before recursing. This improves performance on arrays with many duplicate values.</description></item>
/// <item><description><strong>Dual-Pivot Partitioning:</strong> Separate handling for equal pivots vs. different pivots cases.</description></item>
/// <item><description>Reference: https://web.archive.org/web/20151002230717/http://iaroslavski.narod.ru/quicksort/DualPivotQuicksort.pdf</description></item>
/// </list>
/// <para><strong>Differences from Java's DualPivotQuicksort (Java 7+):</strong></para>
/// <list type="bullet">
/// <item><description><strong>Core Algorithm:</strong> This implementation matches Yaroslavskiy's 2009 paper specification.</description></item>
/// <item><description><strong>Adaptive Algorithm Selection:</strong> Java's implementation adaptively selects from multiple algorithms:
/// <list type="bullet">
/// <item><description>Insertion Sort: Arrays ≤47 elements (we use ≤17)</description></item>
/// <item><description>Merge Sort: 47-286 elements with detected sorted runs (partial ordering)</description></item>
/// <item><description>Dual-Pivot QuickSort: ≥286 elements (general case)</description></item>
/// <item><description>Counting Sort: ≥3000 elements with small value range (e.g., byte arrays)</description></item>
/// </list>
/// This implementation uses Yaroslavskiy's original algorithm without additional adaptive selection.</description></item>
/// <item><description><strong>Duplicate Handling:</strong> Java uses 5-way partitioning to segregate elements equal to pivots.
/// This implementation uses Yaroslavskiy's 2009 approach with equal elements optimization.</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Quicksort</para>
/// <para>Paper: https://arxiv.org/abs/1310.7409 Average Case Analysis of Java 7's Dual Pivot Quicksort / Sebastian Wild, Markus E. Nebel</para>
/// </remarks>
public static class QuickSortDualPivot
{
    // Threshold for switching to 5-sample pivot selection
    // Below this size, simple pivot selection (left, right) is used
    // With length=47, seventh≈6, giving 5 sample points at approximately:
    // e1≈1/7, e2≈3/7, e3≈4/7(middle), e4≈5/7, e5≈6/7 of the array
    // This spacing ensures reliable pivot selection quality
    private const int PivotThreshold = 47;

    // Threshold for switching to insertion sort (Yaroslavskiy 2009)
    // Arrays smaller than this size are sorted using insertion sort
    private const int TINY_SIZE = 17;

    // Threshold for equal elements optimization (Yaroslavskiy 2009)
    // When center region is larger than (length - DIST_SIZE) and pivots are different,
    // segregate elements equal to pivots from the center region
    private const int DIST_SIZE = 13;

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
    /// <typeparam name="TComparer">The type of comparer to use. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span containing elements to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <param name="comparer">The comparer used to compare elements.</param>
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
    /// Sorts the subrange [left..right] (both inclusive) using the provided sort context.
    /// This overload accepts a SortSpan directly for use by other algorithms that already have a SortSpan instance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="left">The inclusive start index of the range to sort.</param>
    /// <param name="right">The inclusive end index of the range to sort.</param>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (right > left)
        {
            int length = right - left + 1;

            // For tiny arrays, use insertion sort (Yaroslavskiy 2009 optimization)
            if (length < TINY_SIZE)
            {
                InsertionSort.SortCore(s, left, right + 1);
                return;
            }

            // For small arrays, use simple pivot selection (left and right)
            if (length < PivotThreshold)
            {
                // Simple pivot selection: use left and right as pivots
                if (s.Compare(left, right) > 0)
                {
                    s.Swap(left, right);
                }
            }
            else
            {
                // Phase 0. Choose pivots using 5-sample method (Java's DualPivotQuicksort)
                int seventh = (length >> 3) + (length >> 6) + 1; // ≈ length/7

                // Sample 5 evenly distributed elements
                int e3 = (left + right) >> 1; // middle
                int e2 = e3 - seventh;
                int e1 = e2 - seventh;
                int e4 = e3 + seventh;
                int e5 = e4 + seventh;

                // Sort these 5 elements using insertion sort (4-10 comparisons)
                // This guarantees e1 <= e2 <= e3 <= e4 <= e5, ensuring pivot1 <= pivot2
                // Using the same proven approach as StdSort.Sort5
                Sort5(s, e1, e2, e3, e4, e5);

                // Now: e1 <= e2 <= e3 <= e4 <= e5 is GUARANTEED
                // Move pivots to the edges (will be swapped to final positions later)
                s.Swap(e2, left);
                s.Swap(e4, right);
            }

            // Check if pivots are different (right after pivot selection)
            // This is more efficient than checking after partitioning
            // At this point, left and right hold pivot values with left <= right guaranteed
            var diffPivots = s.Compare(left, right) != 0;
            s.Context.OnPhase(SortPhase.QuickSortPartition, left, right);
            s.Context.OnRole(left, BUFFER_MAIN, RoleType.Pivot);
            s.Context.OnRole(right, BUFFER_MAIN, RoleType.Pivot);

            // Phase 1. Partition array into three regions using dual pivots
            // Following Yaroslavskiy 2009 paper structure exactly
            var less = left + 1;
            var great = right - 1;

            if (diffPivots)
            {
                // Different pivots (p1 < p2): dual-comparison loop
                // Each element is compared against both pivot1 (left) and pivot2 (right)
                for (int k = less; k <= great; k++)
                {
                    if (s.Compare(k, left) < 0)
                    {
                        // Element < pivot1: move to left region
                        s.Swap(k, less);
                        less++;
                    }
                    else if (s.Compare(k, right) > 0)
                    {
                        // Element > pivot2: scan from right to find position
                        // Check k < great first to avoid unnecessary comparisons (short-circuit evaluation)
                        while (k < great && s.Compare(great, right) > 0)
                        {
                            great--;
                        }
                        s.Swap(k, great);
                        great--;

                        // Re-check swapped element (original comparison result no longer valid after swap)
                        if (s.Compare(k, left) < 0)
                        {
                            s.Swap(k, less);
                            less++;
                        }
                    }
                    // else: pivot1 <= element <= pivot2, stays in middle
                }
            }
            else
            {
                // Equal pivots (p1 == p2): single-comparison loop
                // All comparisons use left only, avoiding redundant cross-index reads against right.
                //
                // Loop invariant at start of each iteration k:
                //   [left+1, less-1] = all elements strictly < pivot
                //   [less,   k-1  ] = all elements == pivot
                //   [k,      great] = unexamined
                //   [great+1,right-1] = all elements strictly > pivot
                //   s[left] = pivot (never modified during this loop)
                //
                // This invariant is preserved in every case:
                //   s[k] < pivot : swap(k, less) puts old s[less](==pivot) at k, less++ extends left region.
                //   s[k] > pivot : inner while guarantees s[great] <= pivot before swap(k, great),
                //                  so s[k] after swap is <= pivot; re-check moves any < pivot to left.
                //                  s[k] > pivot after swap is structurally impossible.
                //   s[k] == pivot: no action, stays in middle.
                //
                // After the loop: [less, great] is PROVABLY all == pivot.
                // Sorting this region would be a no-op, so skipping it (midCount=0) is correct and
                // avoids O(n²) regression on all-equal inputs.
                for (int k = less; k <= great; k++)
                {
                    if (s.Compare(k, left) < 0)
                    {
                        // Element < pivot: move to left region
                        s.Swap(k, less);
                        less++;
                    }
                    else if (s.Compare(k, left) > 0)
                    {
                        // Element > pivot: scan from right to find position
                        while (k < great && s.Compare(great, left) > 0)
                        {
                            great--;
                        }
                        s.Swap(k, great);
                        great--;

                        // Re-check swapped element
                        if (s.Compare(k, left) < 0)
                        {
                            s.Swap(k, less);
                            less++;
                        }
                    }
                    // else: element == pivot, stays in middle
                }
            }

            // Swap pivots into their final positions
            s.Swap(left, less - 1);
            s.Swap(right, great + 1);

            // Store pivot positions (these indices remain fixed throughout subsequent phases)
            int pivot1 = less - 1;
            int pivot2 = great + 1;

            // Phase 3. Equal elements optimization (not included in paper, extended implementation)
            // When center region is large and pivots are different,
            // segregate elements equal to pivots before sorting center.
            // innerLeft/innerRight start equal to the Phase 1 center boundaries [less, great],
            // then narrow inward as equal-to-pivot elements are moved to the edges of the center.
            int innerLeft = less;
            int innerRight = great;
            int centerLen = great - less + 1;
            if (centerLen > length - DIST_SIZE && diffPivots)
            {
                for (int k = innerLeft; k <= innerRight; k++)
                {
                    if (s.Compare(k, pivot1) == 0) // equals pivot1
                    {
                        s.Swap(k, innerLeft);
                        innerLeft++;
                    }
                    else if (s.Compare(k, pivot2) == 0) // equals pivot2
                    {
                        // Advance innerRight past all pivot2-equal elements
                        // This ensures the swapped element is not equal to pivot2
                        while (k <= innerRight && s.Compare(innerRight, pivot2) == 0)
                        {
                            innerRight--;
                        }

                        // Only swap if k and innerRight haven't crossed
                        if (k <= innerRight)
                        {
                            s.Swap(k, innerRight);
                            innerRight--;

                            // Re-check swapped element (only need to check pivot1 now)
                            // Since we advanced innerRight past all pivot2-equals, k is guaranteed != pivot2
                            if (s.Compare(k, pivot1) == 0)
                            {
                                s.Swap(k, innerLeft);
                                innerLeft++;
                            }
                        }
                    }
                }
            }

            // Phases 2, 4, 5: tail recursion optimization
            // Recurse on the two smallest regions; loop (continue) on the largest.
            // This bounds recursion depth to O(log n): given sizes l+c+r = length-2,
            // the looped region is the largest, so each recursed size ≤ (length-2)/2.
            s.Context.OnRole(left, BUFFER_MAIN, RoleType.None);
            s.Context.OnRole(right, BUFFER_MAIN, RoleType.None);
            int leftCount = pivot1 - left;
            // When diffPivots == false, the equal-pivot partition loop invariant guarantees
            // [less..great] is all == pivot, so sorting it is a no-op. Setting midCount=0
            // skips the recursion and prevents O(n²) regression on all-equal inputs.
            int midCount = diffPivots ? innerRight - innerLeft + 1 : 0;
            int rightCount = right - pivot2;

            if (leftCount >= midCount && leftCount >= rightCount)
            {
                // Left is largest: loop left, recurse on center and right
                if (diffPivots) SortCore(s, innerLeft, innerRight);
                SortCore(s, pivot2 + 1, right);
                right = pivot1 - 1;
            }
            else if (rightCount >= leftCount && rightCount >= midCount)
            {
                // Right is largest: loop right, recurse on left and center
                SortCore(s, left, pivot1 - 1);
                if (diffPivots) SortCore(s, innerLeft, innerRight);
                left = pivot2 + 1;
            }
            else
            {
                // Center is largest: loop center, recurse on left and right
                SortCore(s, left, pivot1 - 1);
                SortCore(s, pivot2 + 1, right);
                left = innerLeft;
                right = innerRight;
            }
        }
    }

    /// <summary>
    /// Sorts 3 elements. 2-3 compares, 0-2 swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort3<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int x, int y, int z)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (s.Compare(y, x) < 0)
        {
            if (s.Compare(z, y) < 0)
            {
                s.Swap(x, z); // z < y < x -> swap x,z -> x < y < z
                return;
            }

            s.Swap(x, y); // x > y && y <= z -> x < y && x <= z
            if (s.Compare(z, y) < 0)  // if y > z
                s.Swap(y, z); // x <= y && y < z
        }
        else if (s.Compare(z, y) < 0)
        {
            s.Swap(y, z); // x >= y && y > z -> x >= z && y <= z
            if (s.Compare(y, x) < 0)  // if x > y
                s.Swap(x, y); // x <= y && y <= z
        }
    }

    /// <summary>
    /// Sorts 4 elements. 3-6 compares, 0-5 swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort4<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int x1, int x2, int x3, int x4)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        Sort3(s, x1, x2, x3);
        if (s.Compare(x4, x3) < 0)
        {
            s.Swap(x3, x4);
            if (s.Compare(x3, x2) < 0)
            {
                s.Swap(x2, x3);
                if (s.Compare(x2, x1) < 0)
                {
                    s.Swap(x1, x2);
                }
            }
        }
    }

    /// <summary>
    /// Sorts 5 elements. 4-10 compares, 0-9 swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort5<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int x1, int x2, int x3, int x4, int x5)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Sort first 4 elements
        Sort4(s, x1, x2, x3, x4);

        // Insert x5 into the sorted sequence
        if (s.Compare(x5, x4) < 0)
        {
            s.Swap(x4, x5);
            if (s.Compare(x4, x3) < 0)
            {
                s.Swap(x3, x4);
                if (s.Compare(x3, x2) < 0)
                {
                    s.Swap(x2, x3);
                    if (s.Compare(x2, x1) < 0)
                    {
                        s.Swap(x1, x2);
                    }
                }
            }
        }
    }
}

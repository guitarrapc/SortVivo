using SortAlgorithm.Contexts;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 配列から9個の要素をサンプリングして中央値の中央値を求め、それをピボットとする分割統治法のソートアルゴリズムです。
/// Median-of-3よりも優れたピボット選択により、偏ったデータや最悪ケースに対してより堅牢な性能を実現します。
/// <br/>
/// A divide-and-conquer sorting algorithm using median-of-medians (median-of-9) pivot selection for improved robustness against adversarial inputs.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct QuickSort with Median-of-9 Pivot Selection:</strong></para>
/// <list type="number">
/// <item><description><strong>Median-of-9 Pivot Selection (Median of Medians) with Adaptive Fallback:</strong> The pivot value is selected using a two-level median computation for large arrays:
/// <list type="bullet">
/// <item><description><strong>For arrays &gt;= 64 elements:</strong> Use full median-of-9 sampling</description></item>
/// <item><description>Sample 9 elements from evenly distributed positions across the array range [left, right]</description></item>
/// <item><description>Positions: left, left+m/8, left+m/4, left+m/2-m/8, left+m/2, left+m/2+m/8, right-m/4, right-m/8, right (where m = right - left)</description></item>
/// <item><description>Divide the 9 samples into 3 groups of 3 elements each</description></item>
/// <item><description>Compute the median of each group using 2-3 comparisons (9 comparisons total for all groups)</description></item>
/// <item><description>Compute the median of the three medians using 2-3 additional comparisons</description></item>
/// <item><description>Total overhead: 11-12 comparisons per partition call</description></item>
/// <item><description><strong>For arrays &lt; 64 elements:</strong> Fall back to median-of-3 to avoid sampling degeneration</description></item>
/// <item><description>Rationale: When m/8 becomes 0 (small arrays), median-of-9 samples duplicate indices, wasting comparisons without improving pivot quality</description></item>
/// <item><description>Example: For 32 elements, m8=2 is still useful; for 16 elements, m8=1 causes overlap; for 8 elements, m8=0 causes severe duplication</description></item>
/// <item><description>Threshold 64 ensures m8 ≥ 4, maintaining well-distributed sampling across all 9 positions</description></item>
/// </list>
/// This sampling strategy reduces worst-case probability from O(1/n³) (median-of-3) to approximately O(1/n⁹) for large arrays,
/// and provides improved pivot quality for challenging input patterns such as sorted, reverse-sorted, mountain-shaped, and adversarially-crafted arrays.
/// The median-of-medians approach increases the likelihood that the pivot is close to the true median, helping to produce more balanced partitions.</description></item>
/// <item><description><strong>Three-Way Partition (Dijkstra's Dutch National Flag):</strong> The array is partitioned into three regions in a single pass:
/// <list type="bullet">
/// <item><description>Initialize pointers: lt = left (boundary for &lt; pivot), gt = right - 1 (boundary for &gt; pivot), i = left (current element)</description></item>
/// <item><description>Scan and classify: compare array[i] with pivot and classify into three regions</description></item>
/// <item><description>If array[i] &lt; pivot: swap array[i] with array[lt], increment both lt and i</description></item>
/// <item><description>If array[i] &gt; pivot: swap array[i] with array[gt], decrement gt (don't increment i to re-examine swapped element)</description></item>
/// <item><description>If array[i] == pivot: increment i (keep element in middle region)</description></item>
/// <item><description>Termination: loop exits when i &gt; gt, ensuring all elements are classified</description></item>
/// </list>
/// This 3-way partitioning improves performance on arrays with many duplicate elements, reducing time complexity from O(n²) to O(n) for such cases.</description></item>
/// <item><description><strong>Partition Invariant:</strong> Upon completion of the partitioning phase (when i &gt; gt, i.e., i == gt + 1):
/// <list type="bullet">
/// <item><description>All elements in range [left, lt) satisfy: element &lt; pivot</description></item>
/// <item><description>All elements in range [lt, i) satisfy: element == pivot (before moving pivot from right)</description></item>
/// <item><description>All elements in range (gt, right) satisfy: element &gt; pivot (right holds the original pivot)</description></item>
/// <item><description>After moving pivot to position i: [lt, i] becomes the == pivot region</description></item>
/// <item><description>Partition boundaries satisfy: left ≤ lt ≤ i ≤ right</description></item>
/// </list>
/// This invariant ensures that after partitioning, the array is divided into three well-defined regions for recursive sorting.</description></item>
/// <item><description><strong>Recursive Subdivision:</strong> The algorithm recursively sorts two independent subranges, excluding the equal region:
/// <list type="bullet">
/// <item><description>Left subrange: [left, lt-1] contains all elements &lt; pivot and is sorted only if left &lt; lt-1</description></item>
/// <item><description>Middle region: [lt, eqRight] contains all elements == pivot (including the moved pivot) and needs no further sorting</description></item>
/// <item><description>Right subrange: [eqRight+1, right] contains all elements &gt; pivot and is sorted only if eqRight+1 &lt; right</description></item>
/// </list>
/// Base case: when right ≤ left, the range contains ≤ 1 element and is trivially sorted.
/// The 3-way partition ensures that elements equal to pivot are excluded from further recursion, improving performance on arrays with many duplicates.</description></item>
/// <item><description><strong>Termination Guarantee:</strong> The algorithm terminates for all inputs because:
/// <list type="bullet">
/// <item><description>Progress property: After each 3-way partition, both subranges [left, lt-1] and [eqRight+1, right] are strictly smaller than [left, right]</description></item>
/// <item><description>Minimum progress: Even when all elements equal the pivot, the entire array is classified as the equal region and recursion terminates immediately</description></item>
/// <item><description>Base case reached: The loop condition (left &lt; right) ensures termination when the range contains ≤ 1 element</description></item>
/// <item><description>Expected recursion depth: O(log n) with median-of-9 pivot selection (comparable to median-of-3)</description></item>
/// <item><description>Worst-case recursion depth: <strong>O(log n)</strong> with tail recursion optimization (always recurse on smaller partition)</description></item>
/// <item><description>Tail recursion optimization: The implementation recursively processes only the smaller partition and loops on the larger one, ensuring O(log n) stack depth even in adversarial cases</description></item>
/// </list>
/// The 3-way partition scheme ensures progress even on arrays with many duplicate elements.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Partitioning (Divide and Conquer)</description></item>
/// <item><description>Partition   : Three-way partition (Dijkstra's Dutch National Flag)</description></item>
/// <item><description>Stable      : No (partitioning does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack, O(1) for partitioning)</description></item>
/// <item><description>Best case   : Θ(n) - Occurs when all elements are equal (entire array becomes the equal region)</description></item>
/// <item><description>Average case: Θ(n log n) - Expected ~1.39n log₂ n comparisons with 3-way partition</description></item>
/// <item><description>Worst case  : O(n²) - Occurs when partitioning is maximally unbalanced (probability &lt; 1/n⁹, extremely unlikely)</description></item>
/// <item><description>Comparisons : ~1.39n log₂ n + 12n (average) - Additional ~12 comparisons per partition for median-of-9 selection</description></item>
/// <item><description>Swaps       : ~0.33n log₂ n (average) - 3-way partition performs similar swaps to Hoare partition</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Quicksort</para>
/// </remarks>
public static class QuickSortMedian9
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
    /// Sorts the subrange [left..right] (both inclusive) using the provided sort context.
    /// This overload accepts a SortSpan directly for use by other algorithms that already have a SortSpan instance.
    /// Uses tail recursion optimization to limit stack depth to O(log n) by recursing only on smaller partition.
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
        while (left < right)
        {
            // Phase 1. Select pivot using median-of-9 or median-of-3 strategy
            // For small ranges (< 64 elements), median-of-9 sampling degrades due to m8 becoming 0,
            // causing duplicate index sampling and wasted comparisons. Fall back to median-of-3.
            int pivotIndex;
            var rangeSize = right - left + 1;
            
            if (rangeSize < 64)
            {
                // Small range: use median-of-3 to avoid sampling degeneration
                var mid = left + (right - left) / 2;
                pivotIndex = MedianOf3Index(s, left, mid, right);
            }
            else
            {
                // Large range: use full median-of-9 for superior pivot quality
                pivotIndex = MedianOf9Index(s, left, right);
            }
            
            // Move pivot to right position to enable consistent index-based comparison
            // Avoid self-swap when pivot is already at right
            if (pivotIndex != right)
            {
                s.Swap(pivotIndex, right);
            }
            var pivotPos = right;
            s.Context.OnPhase(SortPhase.QuickSortPartition, left, right, pivotPos);
            s.Context.OnRole(pivotPos, BUFFER_MAIN, RoleType.Pivot);

            // Phase 2. Three-way partition (Dijkstra's Dutch National Flag)
            // This implementation stores pivot at [right] during partitioning, so gt starts at right-1.
            // 
            // Loop invariant (while i <= gt):
            //   [left, lt)       : < pivot
            //   [lt, i)          : == pivot
            //   [i, gt]          : unclassified
            //   [gt+1, right-1]  : > pivot
            //   [right]          : pivot (stored)
            var lt = left;      // Boundary: elements before lt are < pivot
            var gt = right - 1; // Boundary: elements after gt are > pivot
            var i = left;       // Current element being examined

            while (i <= gt)
            {
                var cmp = s.Compare(i, pivotPos);

                if (cmp < 0)
                {
                    // Element < pivot: swap to left region
                    // Avoid self-swap when lt == i (common at loop start and with sorted data)
                    if (lt != i)
                    {
                        s.Swap(lt, i);
                    }
                    lt++;
                    i++;
                }
                else if (cmp > 0)
                {
                    // Element > pivot: swap to right region
                    // Avoid self-swap when i == gt (common at loop end)
                    if (i != gt)
                    {
                        s.Swap(i, gt);
                    }
                    gt--;
                    // Don't increment i - need to examine swapped element
                }
                else
                {
                    // Element == pivot: keep in middle region
                    i++;
                }
            }

            // Loop termination: i == gt + 1
            // At this point:
            //   [left, lt)      : < pivot
            //   [lt, i)         : == pivot
            //   [i, right-1]    : > pivot
            //   [right]         : pivot (stored)
            // 
            // Move pivot from [right] to its final position [i]
            var pivotFinal = i;  // Final position of pivot (first element of > pivot region before swap)
            // Avoid self-swap when all elements are <= pivot (pivotFinal reaches right)
            if (pivotFinal != pivotPos)
            {
                s.Swap(pivotFinal, pivotPos);  // Swap pivot back to its final position
            }

            // After swap:
            //   [left, lt)         : < pivot
            //   [lt, pivotFinal]   : == pivot (pivot now at pivotFinal)
            //   [pivotFinal+1, right] : > pivot
            // Phase 3. Tail recursion optimization: recurse on smaller partition
            // Elements in [lt, pivotFinal] are equal to pivot and don't need further sorting
            s.Context.OnRole(pivotPos, BUFFER_MAIN, RoleType.None);

            // Calculate sizes of subranges to recurse on:
            // Left subrange: [left, lt-1] has size lt - left
            // Right subrange: [pivotFinal+1, right] has size right - pivotFinal
            var leftSize = lt - left;
            var rightSize = right - pivotFinal;

            if (leftSize < rightSize)
            {
                // Recurse on smaller left partition
                if (left < lt - 1)
                {
                    SortCore(s, left, lt - 1);
                }
                // Tail recursion: continue loop with right partition
                left = pivotFinal + 1;
            }
            else
            {
                // Recurse on smaller right partition
                if (pivotFinal < right - 1)
                {
                    SortCore(s, pivotFinal + 1, right);
                }
                // Tail recursion: continue loop with left partition
                right = lt - 1;
            }
        }
    }

    /// <summary>
    /// Returns the median index among three elements at specified indices.
    /// This method performs exactly 2-3 comparisons to determine the median index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MedianOf3Index<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int lowIdx, int midIdx, int highIdx)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Use SortSpan.Compare to track statistics
        var cmpLowMid = s.Compare(lowIdx, midIdx);

        if (cmpLowMid > 0) // low > mid
        {
            var cmpMidHigh = s.Compare(midIdx, highIdx);
            if (cmpMidHigh > 0) // low > mid > high
            {
                return midIdx; // mid is median
            }
            else // low > mid, mid <= high
            {
                var cmpLowHigh = s.Compare(lowIdx, highIdx);
                return cmpLowHigh > 0 ? highIdx : lowIdx;
            }
        }
        else // low <= mid
        {
            var cmpMidHigh = s.Compare(midIdx, highIdx);
            if (cmpMidHigh > 0) // low <= mid, mid > high
            {
                var cmpLowHigh = s.Compare(lowIdx, highIdx);
                return cmpLowHigh > 0 ? lowIdx : highIdx;
            }
            else // low <= mid <= high
            {
                return midIdx; // mid is median
            }
        }
    }

    /// <summary>
    /// Returns the median index among nine sampled elements from the array.
    /// This method samples elements at evenly distributed positions and computes
    /// the median of medians to select a high-quality pivot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MedianOf9Index<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int low, int high)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Sample 9 indices at evenly distributed positions (divide range into 8 equal steps)
        var step = (high - low) / 8;
        var i1 = low;
        var i2 = low + step;
        var i3 = low + step * 2;
        var i4 = low + step * 3;
        var i5 = low + step * 4;
        var i6 = low + step * 5;
        var i7 = low + step * 6;
        var i8 = low + step * 7;
        var i9 = high;

        // Compute median index of three groups
        var median1Idx = MedianOf3Index(s, i1, i2, i3);
        var median2Idx = MedianOf3Index(s, i4, i5, i6);
        var median3Idx = MedianOf3Index(s, i7, i8, i9);

        // Return median index of the three median indices
        return MedianOf3Index(s, median1Idx, median2Idx, median3Idx);
    }
}

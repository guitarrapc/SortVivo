using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 配列の左端・中央・右端の3点から中央値を求めてピボットとし、このピボットを基準に配列を左右に分割する分割統治法のソートアルゴリズムです。
/// Hoare partition schemeを使用し、Median-of-3法でピボットを選択することで様々なデータパターンに対して安定した性能を実現します。
/// <br/>
/// A divide-and-conquer sorting algorithm that selects the pivot as the median of three elements (left, middle, right) and partitions the array into left and right subarrays based on that pivot.
/// It uses the Hoare partition scheme and selects the pivot via median-of-three method to achieve stable performance across various data patterns.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct QuickSort with Median-of-3:</strong></para>
/// <list type="number">
/// <item><description><strong>Median-of-3 Pivot Selection:</strong> The pivot value is selected as the median of three sampled elements
/// at positions: array[left], array[mid], and array[right], where mid = (left + right) / 2.
/// This selection method is computed using 2-3 comparisons and ensures better pivot quality than random selection or always using a fixed position.
/// The median-of-3 strategy provides robust performance across various data patterns including sorted, reverse-sorted,
/// and partially sorted arrays, while maintaining the O(1/n³) probability of worst-case partitioning.</description></item>
/// <item><description><strong>Three-Way Partition (Dijkstra's Dutch National Flag):</strong> The array is partitioned into three regions in a single pass:
/// <list type="bullet">
/// <item><description>Initialize pointers: lt = left (boundary for &lt; pivot), gt = right - 1 (boundary for &gt; pivot), i = left (current element)</description></item>
/// <item><description>Scan and classify: compare array[i] with pivot and classify into three regions</description></item>
/// <item><description>If array[i] &lt; pivot: swap array[i] with array[lt], increment both lt and i</description></item>
/// <item><description>If array[i] &gt; pivot: swap array[i] with array[gt], decrement gt (don't increment i to re-examine swapped element)</description></item>
/// <item><description>If array[i] == pivot: increment i (keep element in middle region)</description></item>
/// <item><description>Termination: loop exits when i &gt; gt, ensuring all elements are classified</description></item>
/// </list>
/// This 3-way partitioning dramatically improves performance on arrays with many duplicate elements, reducing time complexity from O(n²) to O(n) for such cases.</description></item>
/// <item><description><strong>Partition Invariant:</strong> Upon completion of the partitioning phase (when i &gt; gt, i.e., i == gt + 1):
/// <list type="bullet">
/// <item><description>All elements in range [left, lt) satisfy: element &lt; pivot</description></item>
/// <item><description>All elements in range [lt, i) satisfy: element == pivot (before moving pivot from right)</description></item>
/// <item><description>All elements in range (gt, right) satisfy: element &gt; pivot (right holds the original pivot)</description></item>
/// <item><description>After moving pivot to position i: [lt, i] becomes the == pivot region</description></item>
/// <item><description>Partition boundaries satisfy: left ≤ lt ≤ i ≤ right</description></item>
/// </list>
/// This invariant guarantees that after partitioning, the array is divided into three well-defined regions for recursive sorting.</description></item>
/// <item><description><strong>Recursive Subdivision:</strong> The algorithm recursively sorts two independent subranges, excluding the equal region:
/// <list type="bullet">
/// <item><description>Left subrange: [left, lt-1] contains all elements &lt; pivot and is sorted only if left &lt; lt-1</description></item>
/// <item><description>Middle region: [lt, eqRight] contains all elements == pivot (including the moved pivot) and needs no further sorting</description></item>
/// <item><description>Right subrange: [eqRight+1, right] contains all elements &gt; pivot and is sorted only if eqRight+1 &lt; right</description></item>
/// </list>
/// Base case: when right ≤ left, the range contains ≤ 1 element and is trivially sorted.
/// The 3-way partition ensures that elements equal to pivot are excluded from further recursion, dramatically improving performance on arrays with many duplicates.</description></item>
/// <item><description><strong>Termination Guarantee:</strong> The algorithm terminates for all inputs because:
/// <list type="bullet">
/// <item><description>Progress property: After each 3-way partition, both subranges [left, lt-1] and [gt+1, right] are strictly smaller than [left, right]</description></item>
/// <item><description>Minimum progress: Even when all elements equal the pivot, the entire array is classified as the equal region and recursion terminates immediately</description></item>
/// <item><description>Base case reached: The recursion depth is bounded, and each recursive call eventually reaches the base case (right ≤ left)</description></item>
/// <item><description>Expected recursion depth: O(log n) with median-of-3 pivot selection</description></item>
/// <item><description>Worst-case recursion depth: O(log n) with tail recursion optimization (always recurse on smaller partition)</description></item>
/// <item><description>Tail recursion optimization: The implementation recursively processes only the smaller partition and loops on the larger one, guaranteeing O(log n) stack depth even in adversarial cases</description></item>
/// </list>
/// The Hoare partition scheme guarantees progress even on arrays with many duplicate elements.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Partitioning (Divide and Conquer)</description></item>
/// <item><description>Partition   : Three-way partition (Dijkstra's Dutch National Flag)</description></item>
/// <item><description>Stable      : No (partitioning does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack, O(1) for partitioning)</description></item>
/// <item><description>Best case   : Θ(n) - Occurs when all elements are equal (entire array becomes the equal region)</description></item>
/// <item><description>Average case: Θ(n log n) - Expected ~1.39n log₂ n comparisons with 3-way partition</description></item>
/// <item><description>Worst case  : O(n²) - Occurs when partitioning is maximally unbalanced (probability ~1/n³ with median-of-3)</description></item>
/// <item><description>Comparisons : ~1.39n log₂ n (average) - 3-way partition performs slightly more comparisons than Hoare but dramatically faster on duplicates</description></item>
/// <item><description>Swaps       : ~0.33n log₂ n (average) - 3-way partition performs similar swaps to Hoare partition</description></item>
/// </list>
/// <para><strong>Median-of-3 Pivot Selection Benefits:</strong></para>
/// <list type="bullet">
/// <item><description>Worst-case probability reduction: From O(1/n) with random pivot to O(1/n³) with median-of-3</description></item>
/// <item><description>Improved pivot quality: Median-of-3 tends to select pivots closer to the true median of the array</description></item>
/// <item><description>Minimal overhead: Requires only 2-3 additional comparisons per partitioning step</description></item>
/// <item><description>Robust data pattern handling: Efficiently handles sorted, reverse-sorted, and nearly-sorted arrays</description></item>
/// <item><description>Simple and widely adopted: The standard median-of-3 approach is well-understood and proven in practice</description></item>
/// </list>
/// <para><strong>Comparison with Other Sorting Algorithms:</strong></para>
/// <list type="bullet">
/// <item><description>vs. Random Pivot QuickSort: Median-of-3 provides more consistent performance with minimal overhead</description></item>
/// <item><description>vs. Hoare/Lomuto Partition QuickSort: 3-way partition dramatically outperforms on duplicate-heavy arrays (O(n) vs O(n²))</description></item>
/// <item><description>vs. Dual-Pivot QuickSort: Simpler implementation; dual-pivot can be faster on random data but 3-way is better for duplicates</description></item>
/// <item><description>vs. IntroSort: This is the core algorithm; IntroSort adds HeapSort fallback for worst-case protection</description></item>
/// <item><description>vs. Standard QuickSort: 3-way partition is essential for real-world data with many duplicate keys</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Quicksort</para>
/// </remarks>
public static class QuickSortMedian3
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
            // Phase 1. Select pivot using standard median-of-3 strategy
            // Sample left, mid, and right positions
            var mid = left + (right - left) / 2;
            var pivotIndex = MedianOf3Index(s, left, mid, right);

            // Move pivot to right position to enable index-based comparison
            // Avoid self-swap when pivot is already at right
            if (pivotIndex != right)
            {
                s.Swap(pivotIndex, right);
            }
            var pivotPos = right;
            s.Context.OnPhase(SortPhase.QuickSortPartition, left, right, pivotPos);
            s.Context.OnRole(pivotPos, BUFFER_MAIN, RoleType.Pivot);

            // Phase 2. Three-way partition
            // Partitions into: [left, lt) < pivot, [lt, eqRight] == pivot, (eqRight, right] > pivot
            var lt = left;      // Elements before lt are < pivot
            var gt = right - 1; // Elements after gt are > pivot
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

            // Loop invariant at termination: i == gt + 1
            // [left, lt) : < pivot (confirmed)
            // [lt, gt+1) = [lt, i) : == pivot (examined in loop)
            // [gt+1, right-1] : > pivot (swapped to the right of gt)
            // [right] : pivot's original position
            // 
            // Move pivot from [right] to position [i=gt+1]
            var eqRight = i;  // gt + 1
            // Avoid self-swap when all elements are <= pivot (eqRight reaches right)
            if (eqRight != pivotPos)
            {
                s.Swap(eqRight, pivotPos);  // Swap [gt+1] with [right]
            }

            // After swap:
            // [left, lt) : < pivot
            // [lt, gt] : == pivot
            // [gt+1] : pivot (moved from right)
            // [gt+2, right-1] : > pivot
            // [right] : == pivot (element originally at gt+1, no need to sort)
            // Phase 3. Tail recursion optimization: recurse on smaller partition
            // Elements in [lt, eqRight] are equal to pivot and don't need further sorting
            s.Context.OnRole(pivotPos, BUFFER_MAIN, RoleType.None);

            // Calculate sizes of subranges to recurse on:
            // Left subrange: [left, lt-1] has size (lt-1) - left + 1 = lt - left
            // Right subrange: [eqRight+1, right] has size right - (eqRight+1) + 1 = right - eqRight
            var leftSize = lt - left;
            var rightSize = right - eqRight;

            if (leftSize < rightSize)
            {
                // Recurse on smaller left partition
                if (left < lt - 1)
                {
                    SortCore(s, left, lt - 1);
                }
                // Tail recursion: continue loop with right partition
                left = eqRight + 1;
            }
            else
            {
                // Recurse on smaller right partition
                if (eqRight < right - 1)
                {
                    SortCore(s, eqRight + 1, right);
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
}

using SortAlgorithm.Contexts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// QuickSort、HeapSort、InsertionSortを組み合わせたハイブリッドソートアルゴリズム。
/// 通常はQuickSortを使用、小さい配列ではInsertionSort、再帰深度が深くなりすぎた場合はHeapSortに切り替えることで、
/// QuickSortの最悪ケースO(n²)を回避し、常にO(n log n)を保証します。
/// <br/>
/// A hybrid sorting algorithm that combines QuickSort, HeapSort, and InsertionSort.
/// It primarily uses QuickSort, but switches to InsertionSort for small arrays and HeapSort when recursion depth becomes too deep,
/// avoiding QuickSort's worst-case O(n²) and guaranteeing O(n log n) in all cases.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Introsort:</strong></para>
/// <list type="number">
/// <item><description><strong>Adaptive Algorithm Selection:</strong> The algorithm must correctly choose between three sub-algorithms:
/// <list type="bullet">
/// <item><description>InsertionSort when partition size ≤ 30 (optimized via benchmarking for best performance)</description></item>
/// <item><description>HeapSort when recursion depth exceeds depthLimit = 2⌊log₂(n)⌋</description></item>
/// <item><description>QuickSort (median-of-three + Hoare partition) for all other cases</description></item>
/// </list>
/// This adaptive selection ensures O(n log n) worst-case while maintaining QuickSort's average-case performance.</description></item>
/// <item><description><strong>Depth Limit Calculation:</strong> The depth limit must be set to 2⌊log₂(n)⌋ where n is the partition size.
/// This value is derived from the expected depth of a balanced binary tree (⌊log₂(n)⌋) multiplied by 2 to allow for some imbalance.
/// When this limit is exceeded, it indicates pathological QuickSort behavior (e.g., adversarial input patterns),
/// triggering a switch to HeapSort which guarantees O(n log n) regardless of input.</description></item>
/// <item><description><strong>QuickSort Phase - Median-of-Three Pivot Selection:</strong> To avoid worst-case QuickSort behavior on sorted/reverse-sorted inputs,
/// the pivot is selected adaptively based on array size:
/// <list type="bullet">
/// <item><description>For arrays &lt; 1000 elements: median of three quartile positions (q1 = left + n/4, mid = left + n/2, q3 = left + 3n/4)</description></item>
/// <item><description>For arrays ≥ 1000 elements: <strong>Ninther</strong> (median-of-5) using positions: left, left+delta/2, mid, mid+delta/2, right (where delta = n/2)</description></item>
/// </list>
/// The quartile-based sampling provides better pivot quality than simple left/mid/right sampling.
/// The Ninther (median-of-5) further improves pivot selection for large arrays, reducing the probability of unbalanced partitions.
/// This adaptive approach is similar to C++ std::introsort's __sort5 optimization.</description></item>
/// <item><description><strong>QuickSort Phase - Hoare Partition Scheme:</strong> Partitioning uses bidirectional scanning:
/// <list type="bullet">
/// <item><description>Left pointer l advances while s[l] &lt; pivot (with boundary check l &lt; right)</description></item>
/// <item><description>Right pointer r retreats while s[r] &gt; pivot (with boundary check r &gt; left)</description></item>
/// <item><description>When both pointers stop and l ≤ r, swap s[l] ↔ s[r] and advance both pointers</description></item>
/// <item><description>Loop terminates when l &gt; r, ensuring partitioning invariant: s[left..r] ≤ pivot ≤ s[l..right]</description></item>
/// </list>
/// The condition l ≤ r (not l &lt; r) ensures elements equal to pivot are swapped, preventing infinite loops on duplicate-heavy arrays.
/// Boundary checks prevent out-of-bounds access when all elements are smaller/larger than pivot.
/// <br/>
/// <strong>Duplicate Detection Optimization:</strong> After partitioning, if one partition is empty and the other contains all elements,
/// the algorithm checks if all elements equal the pivot value. This detects arrays with many duplicates (common in real-world data)
/// and terminates early, avoiding unnecessary recursion. This optimization is particularly effective for:
/// <list type="bullet">
/// <item><description>Boolean arrays (only two distinct values)</description></item>
/// <item><description>Categorical data with few distinct values (e.g., status codes, ratings)</description></item>
/// <item><description>Arrays with many repeated elements (e.g., sensor data with constant readings)</description></item>
/// </list></description></item>
/// <item><description><strong>Tail Recursion Optimization:</strong> After partitioning into [left, r] and [l, right],
/// the algorithm always recursively processes the smaller partition and loops on the larger partition.
/// This optimization guarantees the recursion stack depth is at most O(log n) (specifically ⌈log₂(n)⌉),
/// even in pathological cases before the depth limit triggers HeapSort.
/// This is identical to the strategy used in LLVM libcxx's std::sort implementation.</description></item>
/// <item><description><strong>InsertionSort Threshold:</strong> For partitions of size ≤ 30, InsertionSort is used instead of QuickSort.
/// This threshold was determined through empirical benchmarking:
/// <list type="bullet">
/// <item><description>InsertionSort has lower constant factors than QuickSort for small arrays</description></item>
/// <item><description>Reduces recursion overhead (30-element partition would require ~5 recursion levels)</description></item>
/// <item><description>Improves cache locality by processing small contiguous regions (fits in L1 cache)</description></item>
/// <item><description>Benchmark results showed 10-30% performance improvement for threshold 30 vs. 16 across primitive and reference types</description></item>
/// <item><description>Threshold of 30 matches C++ std::introsort for trivially copyable types</description></item>
/// </list>
/// This hybrid approach achieves better constant factors than pure QuickSort while maintaining O(n log n) worst-case.</description></item>
/// <item><description><strong>Nearly-Sorted Detection with Early Abort (SortIncomplete):</strong> When partitioning produces zero swaps,
/// the partition is likely nearly sorted. The algorithm uses InsertionSort.SortIncomplete to attempt sorting:
/// <list type="bullet">
/// <item><description>For very small partitions (2-5 elements): Uses sorting networks for optimal performance</description></item>
/// <item><description>For larger partitions: Tracks insertion count; aborts if more than 8 insertions are needed</description></item>
/// <item><description>If both partitions complete: Entire range is sorted, return early</description></item>
/// <item><description>If one partition completes: Continue with only the incomplete partition (tail recursion optimization)</description></item>
/// <item><description>If both partitions incomplete: Fall through to regular QuickSort/HeapSort (partition not nearly sorted)</description></item>
/// </list>
/// This optimization is based on LLVM libcxx's __insertion_sort_incomplete and significantly improves performance for nearly-sorted data,
/// common in real-world scenarios (append operations, partially sorted streams, time-series data).</description></item>
/// <item><description><strong>HeapSort Fallback Correctness:</strong> When depthLimit reaches 0, the current partition is sorted using HeapSort.
/// HeapSort guarantees O(n log n) time complexity regardless of input distribution, providing a safety net against adversarial inputs.
/// The depth limit ensures that HeapSort is invoked only when QuickSort exhibits pathological behavior,
/// preserving QuickSort's superior average-case performance for typical inputs.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Partition (base) + Heap + Insertion)</description></item>
/// <item><description>Stable      : No (QuickSort and HeapSort are unstable; element order is not preserved for equal values)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack, no additional arrays allocated)</description></item>
/// <item><description>Best case   : Θ(n log n) - Occurs when QuickSort consistently creates balanced partitions and InsertionSort handles small subarrays efficiently</description></item>
/// <item><description>Average case: Θ(n log n) - Expected ~1.386n log₂ n comparisons from QuickSort with Hoare partition, reduced by InsertionSort optimization</description></item>
/// <item><description>Worst case  : O(n log n) - Guaranteed by HeapSort fallback when recursion depth exceeds 2⌊log₂(n)⌋</description></item>
/// <item><description>Comparisons : ~1.2-1.4n log₂ n (average) - Lower than pure QuickSort due to InsertionSort handling small partitions</description></item>
/// <item><description>Swaps       : ~0.33n log₂ n (average) - Hoare partition performs significantly fewer swaps than Lomuto partition</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Introsort</para>
/// <para>Paper: David R Musser https://webpages.charlotte.edu/rbunescu/courses/ou/cs4040/introsort.pdf</para>
/// <para>LLVM implementation: https://github.com/llvm/llvm-project/blob/368faacac7525e538fa6680aea74e19a75e3458d/libcxx/include/__algorithm/sort.h#L272</para>
/// </remarks>
public static class IntroSort
{
    // Partitioning correctness: Hoare partition maintains invariant s[left..r] ≤ pivot ≤ s[l..right] with proper boundary checks
    // Recursion correctness: Both partitions [left, r] and [l, right] are strictly smaller than [left, right] due to pointer advance after swap
    // Termination guarantee: Combination of depth limit (triggers HeapSort) and tail recursion (limits stack depth) ensures termination
    // Algorithm correctness: InsertionSort, HeapSort, and QuickSort are all proven correct sorting algorithms
    // Complexity guarantee: Depth limit of 2⌊log₂(n)⌋ ensures HeapSort fallback before stack overflow or quadratic behavior

    // Follow to dotnet runtime https://github.com/dotnet/dotnet/blob/a6dd645cee9a1f6d40c3b151f80fb82bcbb87a4d/src/runtime/src/libraries/System.Private.CoreLib/src/System/Array.cs#L25
    private const int IntrosortSizeThreshold = 16;

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
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span containing elements to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that tracks statistics and provides sorting operations.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, int first, int last, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(last, span.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(first, last);

        if (last - first <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        // For floating-point types, move NaN values to the front
        // This optimization is based on dotnet/runtime's ArraySortHelper implementation
        // Reference: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/ArraySortHelper.cs
        int nanEnd = FloatingPointUtils.MoveNaNsToFront(s, first, last);

        if (nanEnd >= last)
        {
            // All values are NaN, already "sorted"
            return;
        }

        // Sort the non-NaN portion
        var depthLimit = 2 * (BitOperations.Log2((uint)(last - nanEnd)) + 1);
        IntroSortInternal(s, nanEnd, last - 1, depthLimit, true);
    }

    /// <summary>
    /// Internal IntroSort implementation that switches between QuickSort, HeapSort, and InsertionSort based on size and depth.
    /// </summary>
    private static void IntroSortInternal<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right, int depthLimit, bool leftmost)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (right > left)
        {
            var size = right - left + 1;

            // Small arrays: use InsertionSort
            // For leftmost partitions, use guarded version (needs boundary checks)
            // For non-leftmost partitions, use unguarded version (pivot acts as sentinel)
            if (size <= IntrosortSizeThreshold)
            {
                if (leftmost)
                {
                    InsertionSort.SortCore(s, left, right + 1);
                }
                else
                {
                    InsertionSort.UnguardedSortCore(s, left, right + 1);
                }
                return;
            }

            // Max depth reached: switch to HeapSort to guarantee O(n log n)
            if (depthLimit == 0)
            {
                HeapSort.SortCore(s, left, right + 1);
                return;
            }

            depthLimit--;

            // QuickSort with adaptive pivot selection:
            // - For large arrays (>= 1000): use Ninther (median-of-5) for better pivot quality
            // - For smaller arrays: use median-of-3 (quartile-based)
            var length = right - left + 1;
            T pivot;

            if (length >= 1000)
            {
                // Ninther: 5-point sampling for large arrays (similar to C++ std::introsort)
                var delta = length / 2;
                var mid = left + delta;
                var q1 = left + delta / 2;
                var q3 = mid + delta / 2;
                pivot = MedianOf5Value(s, left, q1, mid, q3, right);
            }
            else
            {
                // Standard quartile-based median-of-3 for smaller arrays
                var q1 = left + length / 4;
                var mid = left + length / 2;
                var q3 = left + (length * 3) / 4;
                pivot = MedianOf3Value(s, q1, mid, q3);
            }

            // Hoare partition scheme with swap counting for nearly-sorted detection
            s.Context.OnPhase(SortPhase.QuickSortPartition, left, right);
            var l = left;
            var r = right;
            var swapCount = 0;

            while (l <= r)
            {
                // Move l forward while elements are less than pivot
                while (l < right && s.Compare(l, pivot) < 0)
                {
                    l++;
                }

                // Move r backward while elements are greater than pivot
                while (r > left && s.Compare(r, pivot) > 0)
                {
                    r--;
                }

                // If pointers haven't crossed, swap and advance both
                if (l <= r)
                {
                    if (l != r) // Only count actual swaps (not self-swaps)
                    {
                        s.Swap(l, r);
                        swapCount++;
                    }
                    l++;
                    r--;
                }
            }

            // After partitioning: [left..r] <= pivot, [l..right] >= pivot
            // Detect all-equal-to-pivot case (common in arrays with many duplicates)
            // If one partition is empty and the other is the full range, all elements equal pivot
            var leftPartSize = r - left + 1;
            var rightPartSize = right - l + 1;
            var totalSize = right - left + 1;

            if (leftPartSize == 0 && rightPartSize == totalSize)
            {
                // All elements >= pivot; check if all are equal to pivot
                // This happens when all elements in range equal the pivot value
                var allEqual = true;
                for (var i = left; i <= right && allEqual; i++)
                {
                    if (s.Compare(i, pivot) != 0)
                    {
                        allEqual = false;
                    }
                }
                if (allEqual)
                {
                    // All elements equal - range is sorted, done
                    return;
                }
            }
            else if (rightPartSize == 0 && leftPartSize == totalSize)
            {
                // All elements <= pivot; check if all are equal to pivot
                var allEqual = true;
                for (var i = left; i <= right && allEqual; i++)
                {
                    if (s.Compare(i, pivot) != 0)
                    {
                        allEqual = false;
                    }
                }
                if (allEqual)
                {
                    // All elements equal - range is sorted, done
                    return;
                }
            }

            // Nearly-sorted detection: if no swaps occurred, try InsertionSort with early abort
            // This is similar to C++ std::introsort's __insertion_sort_incomplete optimization
            if (swapCount == 0)
            {
                // For nearly-sorted arrays, InsertionSort is very efficient
                // Try both partitions with SortIncomplete (can give up if not nearly sorted)
                var leftSorted = left >= r || InsertionSort.SortIncomplete(s, left, r + 1, leftmost);
                var rightSorted = l >= right || InsertionSort.SortIncomplete(s, l, right + 1, false);

                if (leftSorted)
                {
                    if (rightSorted)
                    {
                        // Both partitions completed successfully - done
                        return;
                    }
                    else
                    {
                        // Left done, right needs more work - continue with right partition
                        leftmost = false;
                        left = l;
                        continue;
                    }
                }
                else
                {
                    if (rightSorted)
                    {
                        // Right done, left needs more work - continue with left partition
                        right = r;
                        continue;
                    }
                    else
                    {
                        // Both partitions incomplete - fall through to regular tail recursion
                    }
                }
            }

            // Tail recursion optimization: always recurse on smaller partition, loop on larger
            // This guarantees O(log n) stack depth even for pathological inputs
            // (similar to C++ std::introsort and LLVM implementation)
            var leftSize = r - left + 1;
            var rightSize = right - l + 1;

            if (leftSize < rightSize)
            {
                // Recurse on smaller left partition (preserves leftmost flag)
                if (left < r)
                {
                    IntroSortInternal(s, left, r, depthLimit, leftmost);
                }
                // Tail recursion: continue loop with larger right partition
                // Right partition is never leftmost (element at position r acts as sentinel)
                leftmost = false;
                left = l;
            }
            else
            {
                // Recurse on smaller right partition (always non-leftmost)
                if (l < right)
                {
                    IntroSortInternal(s, l, right, depthLimit, leftmost);
                }
                // Tail recursion: continue loop with larger left partition
                // Preserve leftmost flag for left partition
                right = r;
            }
        }
    }

    /// <summary>
    /// Returns the median value among three elements at specified indices.
    /// This method performs exactly 2-3 comparisons to determine the median value.
    /// </summary>
    private static T MedianOf3Value<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int lowIdx, int midIdx, int highIdx)
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
                return s.Read(midIdx); // mid is median
            }
            else // low > mid, mid <= high
            {
                var cmpLowHigh = s.Compare(lowIdx, highIdx);
                return cmpLowHigh > 0 ? s.Read(highIdx) : s.Read(lowIdx);
            }
        }
        else // low <= mid
        {
            var cmpMidHigh = s.Compare(midIdx, highIdx);
            if (cmpMidHigh > 0) // low <= mid, mid > high
            {
                var cmpLowHigh = s.Compare(lowIdx, highIdx);
                return cmpLowHigh > 0 ? s.Read(lowIdx) : s.Read(highIdx);
            }
            else // low <= mid <= high
            {
                return s.Read(midIdx); // mid is median
            }
        }
    }

    /// <summary>
    /// Returns the median value among five elements at specified indices.
    /// This implements "Ninther" - median-of-medians using 5 samples for better pivot quality on large arrays.
    /// Performs 6-8 comparisons to determine the median value.
    /// </summary>
    /// <remarks>
    /// This is based on C++ std::introsort's __sort5 optimization for arrays >= 1000 elements.
    /// The five samples are: left, left+delta/2, mid, mid+delta/2, right (where delta = length/2).
    /// This provides better pivot selection than simple 3-point sampling, especially for large arrays
    /// with patterns like partially-sorted or mountain-shaped distributions.
    /// </remarks>
    private static T MedianOf5Value<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int i1, int i2, int i3, int i4, int i5)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Sort the 5 indices using a sorting network (6 comparisons minimum for 5 elements)
        // We'll use a simplified approach: sort pairs, then find median

        // First, sort pairs: (i1, i2), (i3, i4)
        if (s.Compare(i1, i2) > 0) (i1, i2) = (i2, i1);
        if (s.Compare(i3, i4) > 0) (i3, i4) = (i4, i3);

        // Now i1 <= i2 and i3 <= i4
        // Find median of i2, i3, i5 (this will be in the middle range)
        if (s.Compare(i2, i5) > 0) (i2, i5) = (i5, i2);
        if (s.Compare(i2, i3) > 0) (i2, i3) = (i3, i2);

        // Now we need the median of the remaining elements
        // We know: i1 <= i2, i3 <= i4, and i2 is constrained
        // The median is the 3rd element when sorted

        if (s.Compare(i1, i3) > 0) (i1, i3) = (i3, i1);
        // i1 is now the minimum of {i1, i3}

        if (s.Compare(i2, i3) > 0) (i2, i3) = (i3, i2);
        // i2 is now <= i3

        if (s.Compare(i2, i4) > 0)
        {
            if (s.Compare(i3, i4) > 0)
            {
                return s.Read(i4);
            }
            else
            {
                return s.Read(i3);
            }
        }

        return s.Read(i2);
    }

    /// <summary>
    /// Computes the floor of the base-2 logarithm of a positive integer.
    /// </summary>
    /// <param name="n">The positive integer to compute the logarithm for.</param>
    /// <returns>The floor of log2(n).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Log2(int n)
    {
        // var log = 0;
        // while (n > 1)
        // {
        //     n >>= 1;
        //     log++;
        // }
        // return log;
        return BitOperations.Log2((uint)n);
    }
}

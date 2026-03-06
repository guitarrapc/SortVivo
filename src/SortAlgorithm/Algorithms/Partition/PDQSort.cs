using System.Numerics;
using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// ランダム化quicksortの高速な平均ケースとheapsortの高速な最悪ケースを組み合わせ、特定のパターンを持つ入力に対して線形時間を実現する改良されたquicksort変種で、David MusserのIntrosortの拡張および改善版です。
/// <br/>
/// Improved quicksort variant and extension and improvement of David Musser's introsort, that combines the fast average case of randomized quicksort with the fast worst case of heapsort, while achieving linear time on
/// inputs with certain patterns.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct PDQSort:</strong></para>
/// <list type="number">
/// <item><description><strong>Adaptive Algorithm Selection:</strong> PDQSort must correctly choose between sub-algorithms:
/// <list type="bullet">
/// <item><description>InsertionSort when partition size &lt; 24 (InsertionSortThreshold)</description></item>
/// <item><description>HeapSort when bad partition count exceeds badAllowed = log₂(n)</description></item>
/// <item><description>PartialInsertionSort when already-partitioned pattern detected</description></item>
/// <item><description>QuickSort with adaptive pivot selection for all other cases</description></item>
/// </list>
/// This adaptive selection ensures O(n log n) worst-case while maintaining O(n) best-case for patterns.</description></item>
/// <item><description><strong>Bad Partition Detection and Limit:</strong> The bad partition limit must be set to log₂(n).
/// A partition is "bad" (highly unbalanced) when either side is &lt; n/8. When this limit is exceeded,
/// it indicates adversarial input patterns, triggering a switch to HeapSort which guarantees O(n log n).</description></item>
/// <item><description><strong>Pivot Selection - Ninther for Large Arrays:</strong> For arrays larger than 128 elements (NintherThreshold),
/// the pivot is selected as the median-of-medians (pseudomedian of 9 elements):
/// <list type="bullet">
/// <item><description>Three groups: {begin, mid, end-1}, {begin+1, mid-1, end-2}, {begin+2, mid+1, end-3}</description></item>
/// <item><description>Each group is sorted (Sort3), then the medians are sorted</description></item>
/// <item><description>The middle of the three medians becomes the pivot</description></item>
/// </list>
/// This ninther selection reduces the probability of worst-case partitioning to O(1/n⁹) from O(1/n³) (median-of-3).</description></item>
/// <item><description><strong>Partition Right Scheme:</strong> The primary partitioning scheme places equal elements in the right partition:
/// <list type="bullet">
/// <item><description>Bidirectional scan: left pointer advances while element &lt; pivot, right pointer retreats while element ≥ pivot</description></item>
/// <item><description>Boundary guards prevent out-of-bounds access when all elements are smaller/larger than pivot</description></item>
/// <item><description>Returns (pivotPos, alreadyPartitioned flag) for pattern detection</description></item>
/// <item><description>alreadyPartitioned flag is true when first ≥ last before any swaps, indicating pre-sorted partition</description></item>
/// </list>
/// This scheme enables detection of already-sorted sequences for O(n) behavior.</description></item>
/// <item><description><strong>Partition Left for Equal Elements:</strong> When pivot equals previous partition boundary (begin-1),
/// switch to PartitionLeft which places equal elements in the left partition:
/// <list type="bullet">
/// <item><description>This optimization handles inputs with many duplicate elements</description></item>
/// <item><description>Left partition becomes fully sorted (all equal to pivot), requiring no recursion</description></item>
/// <item><description>Achieves O(n) time for arrays with all equal elements</description></item>
/// </list>
/// This is triggered by comparing the pivot with *(begin-1), which is the largest element of the previous left partition.</description></item>
/// <item><description><strong>Partial Insertion Sort for Pattern Detection:</strong> When a partition appears already sorted (alreadyPartitioned flag),
/// attempt insertion sort with a limit of 8 element moves (PartialInsertionSortLimit):
/// <list type="bullet">
/// <item><description>If ≤ 8 elements are moved, the partition is nearly sorted → complete the insertion sort (return true)</description></item>
/// <item><description>If &gt; 8 elements are moved, the partition is not sorted → abort and use quicksort (return false)</description></item>
/// <item><description>Applied to both left and right partitions; if both succeed, entire range is sorted</description></item>
/// </list>
/// This achieves O(n) time for sorted and nearly-sorted inputs.</description></item>
/// <item><description><strong>Pattern-Defeating Shuffles:</strong> When a bad partition is detected, shuffle elements to break adversarial patterns:
/// <list type="bullet">
/// <item><description>Swap elements at positions: begin ↔ begin+n/4, pivotPos-1 ↔ pivotPos-n/4 (and similar for right partition)</description></item>
/// <item><description>For large partitions (&gt; 128), perform additional swaps at +1, +2 offsets</description></item>
/// <item><description>This randomization defeats carefully crafted adversarial inputs (e.g., anti-quicksort permutations)</description></item>
/// </list>
/// Without shuffling, adversarial inputs could force O(n²) behavior even with good pivot selection.</description></item>
/// <item><description><strong>Unguarded Insertion Sort Optimization:</strong> For non-leftmost partitions, use unguarded insertion sort:
/// <list type="bullet">
/// <item><description>*(begin-1) acts as a sentinel (guaranteed ≤ any element in [begin, end))</description></item>
/// <item><description>Eliminates boundary check (sift != begin) in the inner loop</description></item>
/// <item><description>Reduces comparisons and improves cache performance for small partitions</description></item>
/// </list>
/// This optimization is safe because quicksort's partitioning ensures *(begin-1) ≤ pivot ≤ all elements in right partition.</description></item>
/// <item><description><strong>Tail Recursion Elimination:</strong> After partitioning into [begin, pivotPos) and [pivotPos+1, end),
/// the algorithm always recursively processes the smaller partition and loops on the larger partition.
/// This optimization guarantees the recursion stack depth is at most O(log n) (specifically ⌈log₂(n)⌉),
/// even in pathological cases before the bad partition limit triggers HeapSort.
/// This is identical to the strategy used in LLVM libcxx's std::sort and our IntroSort implementation.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Partition (base) + Heap + Insertion)</description></item>
/// <item><description>Stable      : No (partitioning and heapsort are unstable)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack)</description></item>
/// <item><description>Best case   : O(n) - Sorted, reverse sorted, all equal elements (pattern detection + partial insertion sort)</description></item>
/// <item><description>Average case: Θ(n log n) - Expected ~1.2-1.4n log₂ n comparisons (better than basic quicksort due to optimizations)</description></item>
/// <item><description>Worst case  : O(n log n) - Guaranteed by HeapSort fallback when bad partition limit exceeded</description></item>
/// <item><description>Comparisons : ~1.2-1.4n log₂ n (average) - Ninther pivot selection and insertion sort reduce constant factors</description></item>
/// <item><description>Swaps       : ~0.33n log₂ n (average) - Partitioning performs fewer swaps than Lomuto scheme</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Paper: https://arxiv.org/abs/2106.05123</para>
/// <para>Other implementation: https://github.com/orlp/pdqsort</para>
/// </remarks>
public static class PDQSort
{
    // InsertionSortThreshold: 24 elements (empirically optimal, balances overhead vs. efficiency)
    // NintherThreshold: 128 elements (above this, use ninther; below this, use median-of-3)
    // PartialInsertionSortLimit: 8 element moves (threshold for detecting nearly-sorted partitions)
    // Bad partition limit: log₂(n) (allows some imbalance before triggering HeapSort)
    // Bad partition criterion: Either partition &lt; n/8 (highly unbalanced)

    // Constants
    private const int InsertionSortThreshold = 24;
    private const int NintherThreshold = 128;
    private const int PartialInsertionSortLimit = 8;

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
    /// <typeparam name="TComparer">The type of the comparer.</typeparam>
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
        // This improves performance and enhances PDQSort's pattern detection
        int nanEnd = FloatingPointUtils.MoveNaNsToFront(s, first, last);

        if (nanEnd >= last)
        {
            // All values are NaN, already "sorted"
            return;
        }

        // Sort the non-NaN portion
        var badAllowed = Log2(last - nanEnd);
        PDQSortLoop(s, nanEnd, last, badAllowed, true);
    }

    /// <summary>
    /// Main PDQSort loop with tail recursion elimination.
    /// </summary>
    private static void PDQSortLoop<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int begin, int end, int badAllowed, bool leftmost)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (true)
        {
            var size = end - begin;

            // Use insertion sort for small arrays
            if (size < InsertionSortThreshold)
            {
                if (leftmost)
                {
                    InsertionSort.SortCore(s, begin, end);
                }
                else
                {
                    InsertionSort.UnguardedSortCore(s, begin, end);
                }
                return;
            }

            // Choose pivot as median of 3 or pseudomedian of 9 (ninther)
            var s2 = size / 2;
            if (size > NintherThreshold)
            {
                // Ninther: median of medians for better pivot selection
                Sort3(s, begin, begin + s2, end - 1);
                Sort3(s, begin + 1, begin + (s2 - 1), end - 2);
                Sort3(s, begin + 2, begin + (s2 + 1), end - 3);
                Sort3(s, begin + (s2 - 1), begin + s2, begin + (s2 + 1));
                s.Swap(begin, begin + s2);
            }
            else
            {
                Sort3(s, begin + s2, begin, end - 1);
            }

            // If *(begin - 1) is the end of the right partition of a previous partition operation,
            // there is no element in [begin, end) that is smaller than *(begin - 1).
            // Then if our pivot compares equal to *(begin - 1) we change strategy.
            if (!leftmost && s.Compare(begin - 1, begin) >= 0)
            {
                begin = PartitionLeft(s, begin, end) + 1;
                continue;
            }

            // Partition and detect equal elements block
            s.Context.OnPhase(SortPhase.QuickSortPartition, begin, end - 1);
            var (equalLeft, equalRight, alreadyPartitioned) = PartitionRightSkipEquals(s, begin, end);

            // Calculate sizes excluding the equal block
            var lSize = equalLeft - begin;        // Elements < pivot
            var rSize = end - equalRight;          // Elements > pivot
            var eqSize = equalRight - equalLeft;         // Elements == pivot (to be excluded from recursion)
            var effective = size - eqSize;  // Effective size excluding equal elements

            // Check for highly unbalanced partition (using effective size)
            // If effective is small, the array is mostly equal elements and will finish soon
            var highlyUnbalanced = effective > 0 && (lSize < effective / 8 || rSize < effective / 8);

            // If we got a highly unbalanced partition we shuffle elements to break many patterns
            if (highlyUnbalanced)
            {
                // If we had too many bad partitions, switch to heapsort to guarantee O(n log n)
                if (--badAllowed == 0)
                {
                    HeapSort.SortCore(s, begin, end);
                    return;
                }

                if (lSize >= InsertionSortThreshold)
                {
                    s.Swap(begin, begin + lSize / 4);
                    s.Swap(equalLeft - 1, equalLeft - lSize / 4);

                    if (lSize > NintherThreshold)
                    {
                        s.Swap(begin + 1, begin + (lSize / 4 + 1));
                        s.Swap(begin + 2, begin + (lSize / 4 + 2));
                        s.Swap(equalLeft - 2, equalLeft - (lSize / 4 + 1));
                        s.Swap(equalLeft - 3, equalLeft - (lSize / 4 + 2));
                    }
                }

                if (rSize >= InsertionSortThreshold)
                {
                    s.Swap(equalRight, equalRight + rSize / 4);
                    s.Swap(end - 1, end - rSize / 4);

                    if (rSize > NintherThreshold)
                    {
                        s.Swap(equalRight + 1, equalRight + (1 + rSize / 4));
                        s.Swap(equalRight + 2, equalRight + (2 + rSize / 4));
                        s.Swap(end - 2, end - (1 + rSize / 4));
                        s.Swap(end - 3, end - (2 + rSize / 4));
                    }
                }
            }
            else
            {
                // If we were decently balanced and we tried to sort an already partitioned
                // sequence try to use insertion sort (excluding equal elements block)
                if (alreadyPartitioned &&
                    PartialInsertionSort(s, begin, equalLeft) &&
                    PartialInsertionSort(s, equalRight, end))
                {
                    return;
                }
            }

            // Tail recursion optimization: always recurse on smaller partition, loop on larger
            // This guarantees O(log n) stack depth even for pathological inputs
            // (similar to IntroSort and LLVM std::sort implementation)
            // Exclude equal elements block [eqL, eqR) from recursion
            var leftSize = equalLeft - begin;
            var rightSize = end - equalRight;

            if (leftSize < rightSize)
            {
                // Recurse on smaller left partition (preserves leftmost flag)
                PDQSortLoop(s, begin, equalLeft, badAllowed, leftmost);
                // Tail recursion: continue loop with larger right partition
                begin = equalRight;
                leftmost = false;
            }
            else
            {
                // Recurse on smaller right partition (always non-leftmost)
                PDQSortLoop(s, equalRight, end, badAllowed, false);
                // Tail recursion: continue loop with larger left partition
                end = equalLeft;
                // Preserve leftmost flag for left partition
            }
        }
    }

    /// <summary>
    /// Partitions using PartitionRight and then detects consecutive elements equal to the pivot.
    /// Returns the bounds of the equal block (eqL, eqR) to exclude from further recursion.
    /// This optimization is critical for handling inputs with many duplicate elements efficiently.
    /// </summary>
    /// <returns>
    /// eqL: Start of equal block (inclusive) - all elements in [eqL, eqR) equal pivot
    /// eqR: End of equal block (exclusive)
    /// alreadyPartitioned: Whether the input was already partitioned
    /// </returns>
    private static (int eqL, int eqR, bool alreadyPartitioned) PartitionRightSkipEquals<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int begin, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var (pivotPos, alreadyPartitioned) = PartitionRight(s, begin, end);

        // Read pivot value once to minimize SortSpan access
        var pivot = s.Read(pivotPos);

        // Expand left: find consecutive elements equal to pivot
        var eqL = pivotPos;
        while (eqL > begin && s.Compare(eqL - 1, pivot) == 0)
        {
            eqL--;
        }

        // Expand right: find consecutive elements equal to pivot
        var eqR = pivotPos + 1;
        while (eqR < end && s.Compare(eqR, pivot) == 0)
        {
            eqR++;
        }

        return (eqL, eqR, alreadyPartitioned);
    }

    /// <summary>
    /// Partitions [begin, end) around pivot *begin. Elements equal to the pivot are put in the right-hand partition.
    /// Returns the position of the pivot after partitioning and whether the passed sequence already was correctly partitioned.
    /// </summary>
    private static (int pivotPos, bool alreadyPartitioned) PartitionRight<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int begin, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Move pivot into local for speed
        var pivot = s.Read(begin);

        var first = begin;
        var last = end;

        // Find the first element greater than or equal to the pivot (first can reach end)
        do
        {
            first++;
            if (first == end) break;
        } while (s.Compare(first, pivot) < 0);

        // Find the first element strictly smaller than the pivot (last can reach begin)
        do
        {
            last--;
            if (last == begin) break;
        } while (first < last && s.Compare(last, pivot) >= 0);

        // If the first pair of elements that should be swapped to partition are the same element,
        // the passed in sequence already was correctly partitioned
        var alreadyPartitioned = first >= last;

        // Keep swapping pairs of elements that are on the wrong side of the pivot
        while (first < last)
        {
            s.Swap(first, last);
            do
            {
                first++;
            } while (first < last && s.Compare(first, pivot) < 0);
            do
            {
                last--;
            } while (first < last && s.Compare(last, pivot) >= 0);
        }

        // Put the pivot in the right place
        var pivotPos = first - 1;
        s.Write(begin, s.Read(pivotPos));
        s.Write(pivotPos, pivot);

        return (pivotPos, alreadyPartitioned);
    }

    /// <summary>
    /// Partitions [begin, end) around pivot *begin. Elements equal to the pivot are put to the left.
    /// Used when many equal elements are detected.
    /// </summary>
    private static int PartitionLeft<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int begin, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var pivot = s.Read(begin);
        var first = begin;
        var last = end;

        // Find the first element from the right that is greater than or equal to pivot (last can reach begin)
        do
        {
            last--;
            if (last == begin) break;
        } while (s.Compare(pivot, s.Read(last)) < 0);

        // Find the first element from the left that is less than pivot
        do
        {
            first++;
            if (first > last) break;
        } while (first < last && s.Compare(pivot, s.Read(first)) >= 0);

        while (first < last)
        {
            s.Swap(first, last);
            do
            {
                last--;
            } while (first < last && s.Compare(pivot, s.Read(last)) < 0);
            do
            {
                first++;
            } while (first < last && s.Compare(pivot, s.Read(first)) >= 0);
        }

        var pivotPos = last;
        s.Write(begin, s.Read(pivotPos));
        s.Write(pivotPos, pivot);

        return pivotPos;
    }

    /// <summary>
    /// Attempts to use insertion sort on [begin, end). Will return false if more than
    /// PartialInsertionSortLimit elements were moved, and abort sorting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PartialInsertionSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int begin, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (begin == end) return true;

        var limit = 0;
        for (var cur = begin + 1; cur < end; cur++)
        {
            if (limit > PartialInsertionSortLimit) return false;

            var sift = cur;
            var siftValue = s.Read(cur);

            if (s.Compare(sift, sift - 1) < 0)
            {
                do
                {
                    s.Write(sift, s.Read(sift - 1));
                    sift--;
                }
                while (sift != begin && s.Compare(siftValue, s.Read(sift - 1)) < 0);

                s.Write(sift, siftValue);
                limit += cur - sift;

                // Check limit immediately after increment to catch last iteration overflow
                if (limit > PartialInsertionSortLimit)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sorts 3 elements at positions a, b, c.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort3<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int a, int b, int c)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (s.Compare(b, a) < 0) s.Swap(a, b);
        if (s.Compare(c, b) < 0) s.Swap(b, c);
        if (s.Compare(b, a) < 0) s.Swap(a, b);
    }

    /// <summary>
    /// Returns floor(log2(n)), assumes n > 0.
    /// Uses hardware-accelerated bit operations for optimal performance.
    /// </summary>
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

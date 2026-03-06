using SortAlgorithm.Contexts;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 適応型の不安定なソートアルゴリズムで、ほぼソート済みのデータに特化した設計です。
/// 既にソート済みのリストに軽微な変更を加えた後の再ソートなどに最適です。
/// 特に、80%以上がソート済みで、順序が乱れた要素が均等に分散している場合、QuickSortの2-5倍高速になることがあります。
/// <br/>
/// An adaptive, unstable sorting algorithm designed for nearly-sorted data.
/// Ideal for re-sorting an already sorted list after minor modifications.
/// Can be 2-5x faster than QuickSort when >80% of data is sorted and out-of-order elements are evenly distributed.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Drop-Merge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Longest Nondecreasing Subsequence (LNS) Extraction:</strong> The algorithm heuristically identifies the LNS by scanning elements left-to-right.
/// Elements are "kept" if they maintain nondecreasing order (array[read] ≥ array[write-1]), forming a nondecreasing subsequence in-place.
/// Elements breaking this order are "dropped" into a temporary storage (dropped vector).
/// This process runs in O(N) time with a single pass through the array.</description></item>
/// <item><description><strong>RECENCY-based Backtracking:</strong> To handle incorrect LNS decisions, the algorithm uses RECENCY = 8 as a tolerance threshold.
/// When RECENCY consecutive elements are dropped, the algorithm assumes the last accepted element was a mistake (an outlier) and backtracks:
/// <list type="bullet">
/// <item><description>The last accepted element is moved to dropped vector</description></item>
/// <item><description>Previously dropped elements are restored for re-evaluation</description></item>
/// <item><description>This prevents local outliers from corrupting the LNS (e.g., [0,1,2,3,9,5,6,7] - the '9' gets dropped)</description></item>
/// </list>
/// RECENCY = 8 was chosen empirically to balance resilience against long out-of-order stretches and performance on mostly-sorted data.</description></item>
/// <item><description><strong>Double Comparison Optimization:</strong> When enabled (DOUBLE_COMPARISONS = true), the algorithm performs an additional check before dropping an element.
/// If array[read] ≥ array[write-2], the algorithm "quick undo" the last acceptance by dropping array[write-1] instead.
/// This catches single-element outliers immediately without backtracking (e.g., [0,1,2,3,9,5] - when comparing 5 with 9, check 5 vs 3 to drop 9 immediately).
/// This optimization significantly improves performance on well-ordered input with occasional outliers.</description></item>
/// <item><description><strong>Fast Backtracking:</strong> When enabled (FAST_BACKTRACKING = true), during backtracking the algorithm computes the maximum of recently dropped elements
/// and backtracks until finding a position where this maximum can be inserted. This handles clumps of out-of-order elements more efficiently
/// by backtracking multiple elements at once instead of one-by-one.</description></item>
/// <item><description><strong>Early-Out Heuristic:</strong> When enabled (EARLY_OUT = true), the algorithm monitors multiple disorder signals to detect worst-case scenarios:
/// <list type="bullet">
/// <item><description><strong>Dropped Ratio Check:</strong> After processing N/EARLY_OUT_TEST_AT elements (N/4), if dropped elements exceed EARLY_OUT_DISORDER_FRACTION (60%) of processed elements, abort and fallback to QuickSort.</description></item>
/// <item><description><strong>Undo Count Check:</strong> If the number of undo operations (backtracking due to RECENCY) exceeds MAX_UNDO_COUNT (16), abort. This catches pathological cases where "Recency reached → backtrack" happens repeatedly.</description></item>
/// <item><description><strong>Total Backtracked Check:</strong> If the cumulative number of backtracked elements exceeds N × MAX_BACKTRACKED_RATIO (N), abort. This detects adversarial inputs where backtracking occurs frequently even if dropped count is low.</description></item>
/// </list>
/// These combined checks prevent worst-case performance on heavily disordered data where Drop-Merge would be slower than general-purpose sorts.</description></item>
/// <item><description><strong>Dropped Elements Sorting:</strong> After LNS extraction, dropped elements are sorted using QuickSort (O(K log K) where K is the number of dropped elements).
/// Since K is expected to be small (K &lt; 0.2N for optimal performance), this step is efficient.</description></item>
/// <item><description><strong>Merge Phase:</strong> The algorithm merges the in-place LNS (in array[0..write]) and sorted dropped elements by:
/// <list type="bullet">
/// <item><description>Scanning from right to left in the final array</description></item>
/// <item><description>Comparing the rightmost LNS element with the rightmost dropped element</description></item>
/// <item><description>Placing the larger element at the current position</description></item>
/// </list>
/// This merge runs in O(N + K) time, reusing the original array space.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Aadaptive</description></item>
/// <item><description>Stable      : No (relative order of equal elements is not preserved)</description></item>
/// <item><description>In-place    : No (O(K) auxiliary space where K is the number of out-of-order elements)</description></item>
/// <item><description>Best case   : O(N) - When data is already sorted or nearly sorted (K ≈ 0), only LNS extraction and merge are needed</description></item>
/// <item><description>Average case: O(N + K log K) - Expected when K &lt; 0.2N with K elements randomly distributed</description></item>
/// <item><description>Worst case  : O(N log N) - Falls back to QuickSort when disorder exceeds 60% (early-out), or when K ≈ N</description></item>
/// <item><description>Comparisons : O(N + K log K) - N comparisons for LNS extraction, K log K for sorting dropped elements, N for merge</description></item>
/// <item><description>Swaps       : O(K log K) - Mainly in sorting the dropped elements; LNS extraction and merge use moves, not swaps</description></item>
/// <item><description>Space       : O(K) - Temporary storage for dropped elements</description></item>
/// </list>
/// <para><strong>Optimal Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>Long lists (&gt;10k elements, especially millions)</description></item>
/// <item><description>&gt;80% of data already in correct order</description></item>
/// <item><description>Out-of-order elements evenly distributed (not clumped)</description></item>
/// <item><description>Less than 20-30% elements out of order</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Paper: https://micsymposium.org/mics_2011_proceedings/mics2011_submission_13.pdf</para>
/// <para>Other implementation: https://github.com/emilk/drop-merge-sort</para>
/// </remarks>
public static class DropMergeSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_DROPPED = 1;    // Dropped elements buffer

    /// <summary>
    /// This speeds up well-ordered input by quite a lot.
    /// </summary>
    private const bool DoubleComparisons = true;

    /// <summary>
    /// The algorithm uses recency=8 which means it can handle no more than 8 outliers in a row.
    /// This number was chosen by experimentation, and could perhaps be adjusted dynamically for increased performance.
    /// Low RECENCY = faster when there is low disorder (a lot of order).
    /// High RECENCY = more resilient against long stretches of noise.
    /// If RECENCY is too small we are more dependent on nice data/luck.
    /// </summary>
    private const int Recency = 8;

    /// <summary>
    /// Back-track several elements at once. This is helpful when there are big clumps out-of-order.
    /// </summary>
    private const bool FastBackTracking = true;

    /// <summary>
    /// Break early if we notice that the input is not ordered enough.
    /// </summary>
    private const bool EarlyOut = true;

    /// <summary>
    /// Test for early-out when we have processed len / EARLY_OUT_TEST_AT elements.
    /// </summary>
    private const int EarlyOutTestAt = 4;

    /// <summary>
    /// If more than this percentage of elements have been dropped, we abort.
    /// </summary>
    private const double EarlyOutDisorderFraction = 0.6;

    /// <summary>
    /// Maximum number of undo operations (backtracking due to RECENCY) before falling back to QuickSort.
    /// Prevents pathological cases where backtracking happens repeatedly.
    /// </summary>
    private const int MaxUndoCount = 16;

    /// <summary>
    /// Maximum total number of backtracked elements before falling back to QuickSort.
    /// If totalBackTracked > n, the algorithm is likely in a worst-case scenario.
    /// </summary>
    private const int MaxBackTrackedRatio = 1; // totalBackTracked > n * MaxBackTrackedRatio

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
    /// Sorts the elements in the specified span using the provided comparer and context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        // Rent buffer from ArrayPool for dropped elements (O(K) auxiliary space where K is the number of out-of-order elements)
        var droppedBuffer = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            SortCore(span, droppedBuffer.AsSpan(0, span.Length), comparer, context);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(droppedBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>
    /// Core drop-merge sort implementation.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span to sort</param>
    /// <param name="droppedBuffer">Auxiliary buffer for dropped elements (same size as span)</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">Sort context for statistics tracking</param>
    private static void SortCore<T, TComparer, TContext>(Span<T> span, Span<T> droppedBuffer, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        var dropped = new SortSpan<T, TComparer, TContext>(droppedBuffer, context, comparer, BUFFER_DROPPED);

        // First step: heuristically find the Longest Nondecreasing Subsequence (LNS).
        // The LNS is kept in-place at span[0..write] while dropped elements go to the dropped buffer.
        context.OnPhase(SortPhase.DropMergeDetect);
        var droppedCount = 0;
        var droppedInRow = 0;
        var undoCount = 0;           // Track number of undo operations (backtracking due to RECENCY)
        var totalBackTracked = 0;    // Track total number of backtracked elements
        var write = 0;
        var read = 0;
        while (read < s.Length)
        {
            // Early-out: fallback to QuickSort if too much disorder detected
            if (EarlyOut)
            {
                // Check 1: Too many dropped elements (original check)
                if (read == s.Length / EarlyOutTestAt
                    && droppedCount > (int)(read * EarlyOutDisorderFraction))
                {
                    // Restore dropped elements back to array using CopyTo for efficiency
                    dropped.CopyTo(0, s, write, droppedCount);
                    QuickSortMedian3.SortCore(s, 0, s.Length - 1);
                    return;
                }

                // Check 2: Too many undo operations (backtracking due to RECENCY)
                if (undoCount > MaxUndoCount)
                {
                    // Restore dropped elements back to array using CopyTo for efficiency
                    dropped.CopyTo(0, s, write, droppedCount);
                    QuickSortMedian3.SortCore(s, 0, s.Length - 1);
                    return;
                }

                // Check 3: Total backtracked elements exceeds threshold
                if (totalBackTracked > s.Length * MaxBackTrackedRatio)
                {
                    // Restore dropped elements back to array using CopyTo for efficiency
                    dropped.CopyTo(0, s, write, droppedCount);
                    QuickSortMedian3.SortCore(s, 0, s.Length - 1);
                    return;
                }
            }

            if (write == 0 || s.Compare(read, write - 1) >= 0)
            {
                // The element is in order - keep it:
                if (read != write)
                {
                    s.Write(write, s.Read(read));
                }
                read++;
                write++;
                droppedInRow = 0;
            }
            else
            {
                // The next element is smaller than the last stored one.
                // The question is - should we drop the new element, or was accepting the previous element a mistake?

                /*
                    Check this situation:
                    0 1 2 3 9 5 6 7  (the 9 is a one-off)
                            | |
                            | read
                            write - 1
                    Checking this improves performance because we catch common problems earlier (without back-tracking).
                */

                if (DoubleComparisons
                    && droppedInRow == 0
                    && 2 <= write
                    && s.Compare(read, write - 2) >= 0)
                {
                    // Quick undo: drop previously accepted element, and overwrite with new one:
                    dropped.Write(droppedCount++, s.Read(write - 1));
                    s.Write(write - 1, s.Read(read));
                    read++;
                    continue;
                }

                if (droppedInRow < Recency)
                {
                    // Drop it
                    dropped.Write(droppedCount++, s.Read(read));
                    read++;
                    droppedInRow++;
                }
                else
                {
                    //We accepted something droppedInRow elements back that made us drop all RECENCY subsequent items.
                    //Accepting that element was obviously a mistake - so let's undo it!

                    //Example problem (RECENCY = 3):    0 1 12 3 4 5 6
                    //    0 1 12 is accepted. 3, 4, 5 will be rejected because they are larger than the last kept item (12).
                    //    When we get to 5 we reach droppedInRow == RECENCY.
                    //    This will trigger an undo where we drop the 12.
                    //    When we again go to 3, we will keep it because it is larger than the last kept item (1).

                    //Example worst-case (RECENCY = 3):   ...100 101 102 103 104 1 2 3 4 5 ....
                    //    100-104 is accepted. When we get to 3 we reach droppedInRow == RECENCY.
                    //    We drop 104 and reset the read by RECENCY. We restart, and then we drop again.
                    //    This can lead us to backtracking RECENCY number of elements
                    //    as many times as the leading non-decreasing subsequence is long.

                    // Track undo operation for early-out detection
                    undoCount++;

                    // Undo dropping the last droppedInRow elements:
                    droppedCount -= droppedInRow;
                    read -= droppedInRow;

                    var backTracked = 1;
                    write--;

                    if (FastBackTracking)
                    {
                        // Back-track until we can accept at least one of the recently dropped elements:
                        var maxOfDropped = MaxInRange(s, read, droppedInRow + 1);
                        while (1 <= write && s.Compare(maxOfDropped, write - 1) < 0)
                        {
                            backTracked++;
                            write--;
                        }
                    }

                    // Track total backtracked elements for early-out detection
                    totalBackTracked += backTracked;

                    // Drop the back-tracked elements:
                    for (var i = 0; i < backTracked; i++)
                    {
                        dropped.Write(droppedCount++, s.Read(write + i));
                    }
                    droppedInRow = 0;
                }
            }
        }

        // Second step: sort the dropped elements
        if (droppedCount > 0)
        {
            context.OnPhase(SortPhase.DropMergeSort, droppedCount);
            var droppedSpan = droppedBuffer.Slice(0, droppedCount);
            // Use SortSpan for dropped elements to track statistics correctly
            var droppedSortSpan = new SortSpan<T, TComparer, TContext>(droppedSpan, context, comparer, BUFFER_DROPPED);
            QuickSortMedian3.SortCore(droppedSortSpan, 0, droppedCount - 1);
        }

        // Third step: merge span[0..write] and droppedBuffer[0..droppedCount]
        context.OnPhase(SortPhase.DropMergeMerge, droppedCount, s.Length);
        var back = s.Length;
        var droppedIndex = droppedCount - 1;

        while (droppedIndex >= 0)
        {
            var lastDropped = dropped.Read(droppedIndex);

            while (0 < write && s.Compare(lastDropped, write - 1) < 0)
            {
                s.Write(back - 1, s.Read(write - 1));
                back--;
                write--;
            }
            s.Write(back - 1, lastDropped);
            back--;
            droppedIndex--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T MaxInRange<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int start, int count)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var max = s.Read(start);
        for (var i = 1; i < count; i++)
        {
            var current = s.Read(start + i);
            if (s.Compare(current, max) > 0)
            {
                max = current;
            }
        }
        return max;
    }
}

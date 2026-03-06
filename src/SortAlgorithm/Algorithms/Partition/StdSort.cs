using SortAlgorithm.Contexts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;


/// <summary>
/// LLVM libc++ std::sort implementation in C#.
/// Implements Introsort algorithm combining quicksort, heapsort, and insertion sort
/// with advanced optimizations including Tuckey's ninther, sorting networks, and
/// detection of already-partitioned data.
/// </summary>
/// <remarks>
/// <para><strong>Algorithm Overview:</strong></para>
/// <list type="bullet">
/// <item><description>Introsort: QuickSort with HeapSort fallback at depth limit</description></item>
/// <item><description>Sorting Networks: Optimized 2-5 element sorts</description></item>
/// <item><description>Insertion Sort: For small subarrays (< 24 elements)</description></item>
/// <item><description>Tuckey's Ninther: Advanced pivot selection for large arrays (>= 128)</description></item>
/// <item><description>Partition Optimizations: Equal element handling, already-partitioned detection</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Best case:    O(n) - Already sorted with partition detection</description></item>
/// <item><description>Average case:  O(n log n) - Typical random data</description></item>
/// <item><description>Worst case:    O(n log n) - Guaranteed by HeapSort fallback</description></item>
/// <item><description>Space:         O(log n) - Recursion stack depth</description></item>
/// <item><description>Stable:        No</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>LLVM libc++: https://github.com/llvm/llvm-project/blob/llvmorg-21.1.8/libcxx/include/__algorithm/sort.h</para>
/// <para>Danila Kutenin: Changing std::sort at Google’s Scale and Beyond https://danlark.org/2022/04/20/changing-stdsort-at-googles-scale-and-beyond/comment-page-1/</para>
/// </remarks>
public static class StdSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array

    // Upper bound for using insertion sort for sorting
    private const int INSERTION_SORT_LIMIT = 24;
    // Lower bound for using Tuckey's ninther technique for median computation
    private const int NINTHER_THRESHOLD = 128;

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
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="s">The SortSpan wrapping the span to sort.</param>
    /// <param name="first">The inclusive start index of the range to sort.</param>
    /// <param name="last">The exclusive end index of the range to sort.</param>
    /// <param name="context">The sort context for tracking statistics and observations.</param>
    internal static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (last - first <= 1) return;

        // Calculate depth limit: 2 * log2(n)
        var len = last - first;
        var depthLimit = 2 * Log2((uint)len);

        IntroSort(s, first, last, depthLimit, leftmost: true);
    }

    /// <summary>
    /// Computes log2 of an unsigned integer (bit width)
    /// </summary>
    private static int Log2(uint n)
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

    /// <summary>
    /// Sorts 3 elements. Stable, 2-3 compares, 0-2 swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort3<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int x, int y, int z)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // if x <= y
        if (s.Compare(y, x) >= 0)
        {
            // if y <= z: x <= y <= z (already sorted)
            if (s.Compare(z, y) >= 0)
                return;

            // x <= y && y > z
            s.Swap(y, z);   // x <= z && y < z
            if (s.Compare(y, x) < 0)  // if x > y
                s.Swap(x, y); // x < y && y <= z
            return;
        }

        // x > y
        if (s.Compare(z, y) < 0) // if y > z
        {
            s.Swap(x, z); // z < y < x -> swap x,z -> x < y < z
            return;
        }

        s.Swap(x, y); // x > y && y <= z -> x < y && x <= z
        if (s.Compare(z, y) < 0)  // if y > z
            s.Swap(y, z); // x <= y && y < z
    }

    /// <summary>
    /// Sorts 4 elements. Stable, 3-6 compares, 0-5 swaps.
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
    /// Sorts 5 elements. Stable, 4-10 compares, 0-9 swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort5<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int x1, int x2, int x3, int x4, int x5)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        Sort4(s, x1, x2, x3, x4);
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

    /// <summary>
    /// Main Introsort algorithm combining quicksort, heapsort, and insertion sort.
    /// </summary>
    /// <param name="leftmost">
    /// True if sorting the leftmost partition of the original range, false otherwise.
    /// When false, the element at (first - 1) is guaranteed to be a pivot from a previous
    /// partition, serving as a sentinel for unguarded insertion sort.
    /// </param>
    private static void IntroSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last, int depth, bool leftmost)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (true)
        {
            var len = last - first;

            // Handle small arrays with specialized sorting networks
            switch (len)
            {
                case 0:
                case 1:
                    return;
                case 2:
                    if (s.Compare(last - 1, first) < 0)
                    {
                        s.Swap(first, last - 1);
                    }
                    return;
                case 3:
                    Sort3(s, first, first + 1, last - 1);
                    return;
                case 4:
                    Sort4(s, first, first + 1, first + 2, last - 1);
                    return;
                case 5:
                    Sort5(s, first, first + 1, first + 2, first + 3, last - 1);
                    return;
            }

            // Use insertion sort for small arrays
            if (len < INSERTION_SORT_LIMIT)
            {
                if (leftmost)
                {
                    // First partition: use guarded insertion sort (with bounds checking)
                    InsertionSort.SortCore(s, first, last);
                }
                else
                {
                    // Right partition after a previous partition:
                    // (first - 1) contains the pivot, which is <= all elements in [first, last)
                    // This pivot serves as a sentinel, so we can use unguarded insertion sort
                    InsertionSortUnguarded(s, first, last);
                }
                return;
            }

            // Fallback to heapsort if recursion depth is too deep
            if (depth == 0)
            {
                HeapSort.SortCore(s, first, last);
                return;
            }
            depth--;

            // Pivot selection: Tuckey's ninther for large arrays, median-of-3 otherwise
            var halfLen = len / 2;
            if (len > NINTHER_THRESHOLD)
            {
                // Ninther: median of medians
                Sort3(s, first, first + halfLen, last - 1);
                Sort3(s, first + 1, first + (halfLen - 1), last - 2);
                Sort3(s, first + 2, first + (halfLen + 1), last - 3);
                Sort3(s, first + (halfLen - 1), first + halfLen, first + (halfLen + 1));
                s.Swap(first, first + halfLen);
            }
            else
            {
                // Median-of-3
                Sort3(s, first + halfLen, first, last - 1);
            }

            // Partition optimization: skip equal elements on left if not leftmost
            // From LLVM libc++:
            // The elements to the left of the current range are already sorted.
            // If the current range is not the leftmost part and the pivot is same as
            // the highest element in the range to the left (at first - 1), then we know
            // that all the elements in [first, pivot] *would be* equal to the pivot,
            // assuming the equal elements are put on the left side when partitioned.
            // This means we do not need to sort the left side of the partition.
            if (!leftmost && s.Compare(first - 1, first) >= 0)
            {
                first = PartitionWithEqualsOnLeft(s, first, last);
                continue;
            }

            // Partition
            s.Context.OnPhase(SortPhase.QuickSortPartition, first, last - 1);
            s.Context.OnRole(first, BUFFER_MAIN, RoleType.Pivot);
            var (pivotPos, alreadyPartitioned) = PartitionWithEqualsOnRight(s, first, last);
            s.Context.OnRole(first, BUFFER_MAIN, RoleType.None);

            // Check if already sorted using insertion sort heuristic
            if (alreadyPartitioned)
            {
                var leftSorted = InsertionSortIncomplete(s, first, pivotPos);
                var rightSorted = InsertionSortIncomplete(s, pivotPos + 1, last);

                if (leftSorted && rightSorted)
                    return;
                if (leftSorted)
                {
                    // Left partition is sorted, continue with right partition
                    // pivotPos will be at (first - 1), serving as sentinel
                    first = pivotPos + 1;
                    continue;
                }
                if (rightSorted)
                {
                    // Right partition is sorted, continue with left partition
                    last = pivotPos;
                    continue;
                }
            }

            // Recursively sort left partition, loop on right (tail recursion elimination)
            IntroSort(s, first, pivotPos, depth, leftmost);
            // After partitioning, pivot is at pivotPos. When we continue with the right partition,
            // pivotPos will be at (first - 1), serving as a sentinel for unguarded insertion sort.
            leftmost = false;
            first = pivotPos + 1;
        }
    }

    /// <summary>
    /// Insertion sort without bounds checking (unguarded).
    /// PRECONDITION: Assumes there is an element at position (first - 1) that is less than or equal
    /// to all elements in the range [first, last). This element acts as a sentinel.
    ///
    /// This precondition is satisfied in IntroSort because:
    /// - This method is only called when leftmost=false
    /// - leftmost=false means we are sorting a right partition after a previous partition
    /// - After partition, the pivot is placed at (first - 1)
    /// - By partition invariant, all elements in [first, last) are >= pivot
    /// - Therefore, pivot at (first - 1) serves as the sentinel
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InsertionSortUnguarded<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (first == last) return;

        for (var i = first + 1; i < last; i++)
        {
            var j = i - 1;
            if (s.Compare(i, j) < 0)
            {
                var tmp = s.Read(i);
                var k = j;
                j = i;
                do
                {
                    s.Write(j, s.Read(k));
                    j = k;
                } while (s.Compare(tmp, --k) < 0);
                s.Write(j, tmp);
            }
        }
    }

    /// <summary>
    /// Attempts insertion sort and returns true if array is already sorted or nearly sorted.
    /// Returns false if too many inversions are found (limit of 8 inversions).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InsertionSortIncomplete<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var len = last - first;
        switch (len)
        {
            case 0:
            case 1:
                return true;
            case 2:
                if (s.Compare(last - 1, first) < 0)
                {
                    s.Swap(first, last - 1);
                }
                return true;
            case 3:
                Sort3(s, first, first + 1, last - 1);
                return true;
            case 4:
                Sort4(s, first, first + 1, first + 2, last - 1);
                return true;
            case 5:
                Sort5(s, first, first + 1, first + 2, first + 3, last - 1);
                return true;
        }

        // Try insertion sort with inversion limit
        var j = first + 2;
        Sort3(s, first, first + 1, j);

        const int limit = 8;
        var count = 0;

        for (var i = j + 1; i < last; i++)
        {
            if (s.Compare(i, j) < 0)
            {
                var tmp = s.Read(i);
                var k = j;
                j = i;
                do
                {
                    s.Write(j, s.Read(k));
                    j = k;
                } while (j != first && s.Compare(tmp, --k) < 0);
                s.Write(j, tmp);

                if (++count == limit)
                {
                    return i + 1 == last;
                }
            }
            j = i;
        }
        return true;
    }

    /// <summary>
    /// Partitions range with equal elements kept to the right of pivot.
    /// Returns (pivot position, already partitioned flag).
    /// </summary>
    private static (int pivotPos, bool alreadyPartitioned) PartitionWithEqualsOnRight<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var begin = first;  // Save original position for bounds checking
        var pivot = s.Read(first);

        // Find first element >= pivot
        var i = first;
        do
        {
            i++;
        } while (i < last && s.Compare(i, pivot) < 0);

        // Find last element < pivot
        var j = last - 1;
        if (i < j)
        {
            // Optimization from LLVM libc++: if first only advanced by 1, we know last won't reach begin
            // because median-of-3 ensures begin is <= pivot, so unguarded scan is safe.
            if (begin == i - 1)
            {
                // Unguarded: first only advanced once, median-of-3 guarantees safety
                while (i < j && s.Compare(j, pivot) >= 0)
                {
                    j--;
                }
            }
            else
            {
                // Guarded: normal case with bounds check
                while (j > begin && s.Compare(j, pivot) >= 0)
                {
                    j--;
                }
            }
        }

        var alreadyPartitioned = i >= j;

        // Partition loop
        while (i < j)
        {
            s.Swap(i, j);

            // After swap, find next elements to swap
            // These are always guarded by the median-of-3 pivot selection
            do { i++; } while (s.Compare(i, pivot) < 0);
            do { j--; } while (s.Compare(j, pivot) >= 0);
        }

        // Place pivot in correct position
        // Note: Cannot use Swap here because pivot is already saved in local variable.
        // pivotPos contains the value that should be at 'first' position after partition.
        // This matches LLVM libc++ implementation: move(pivotPos to first), then move(pivot to pivotPos).
        var pivotPos = i - 1;
        if (first != pivotPos)
        {
            s.Write(first, s.Read(pivotPos));
        }
        s.Write(pivotPos, pivot);

        return (pivotPos, alreadyPartitioned);
    }

    /// <summary>
    /// Partitions range with equal elements kept to the left of pivot.
    /// Returns the first index of the right partition.
    /// </summary>
    private static int PartitionWithEqualsOnLeft<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var begin = first;  // Save original position for bounds checking
        var pivot = s.Read(first);

        // Find first element > pivot
        var i = first;
        // Optimization from LLVM libc++: check if pivot < last element to determine if guarded scan needed
        if (s.Compare(pivot, last - 1) < 0)
        {
            // Guarded: pivot < last element, so elements > pivot exist
            do
            {
                i++;
            } while (s.Compare(pivot, i) >= 0);
        }
        else
        {
            // Unguarded: pivot >= last element, no need for bounds check
            while (++i < last && s.Compare(pivot, i) >= 0)
            {
            }
        }

        // Find last element <= pivot
        var j = last - 1;
        if (i < j)
        {
            // Always guarded because median-of-3 ensures begin <= pivot
            while (j > begin && s.Compare(pivot, j) < 0)
            {
                j--;
            }
        }

        // Partition loop
        while (i < j)
        {
            s.Swap(i, j);

            // After swap, find next elements to swap
            // These are always guarded by the median-of-3 pivot selection
            do { i++; } while (s.Compare(pivot, i) >= 0);
            do { j--; } while (s.Compare(pivot, j) < 0);
        }

        // Place pivot in correct position
        // Note: Cannot use Swap here because pivot is already saved in local variable.
        // pivotPos contains the value that should be at 'first' position after partition.
        // This matches LLVM libc++ implementation: move(pivotPos to first), then move(pivot to pivotPos).
        var pivotPos = i - 1;
        if (first != pivotPos)
        {
            s.Write(first, s.Read(pivotPos));
        }
        s.Write(pivotPos, pivot);

        return i;
    }
}

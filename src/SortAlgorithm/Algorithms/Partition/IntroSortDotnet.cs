using SortAlgorithm.Contexts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// Dotnet runtime style IntroSort implementation.
/// This is a simplified version based on dotnet runtime's ArraySortHelper&lt;T&gt;,
/// optimized for JIT performance with minimal overhead.
/// <br/>
/// A hybrid sorting algorithm that combines QuickSort, HeapSort, and InsertionSort.
/// It primarily uses QuickSort, but switches to InsertionSort for small arrays and HeapSort when recursion depth becomes too deep,
/// avoiding QuickSort's worst-case O(n²) and guaranteeing O(n log n) in all cases.
/// </summary>
/// <remarks>
/// <para><strong>Key Differences from IntroSort:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Simpler pivot selection:</strong> Uses simple median-of-3 (lo, mid, hi) instead of quartile-based or Ninther</description></item>
/// <item><description><strong>Lomuto-style partitioning:</strong> Places pivot in final position, excludes it from recursion</description></item>
/// <item><description><strong>No extra optimizations:</strong> No swap counting, no nearly-sorted detection, no duplicate detection</description></item>
/// <item><description><strong>JIT-friendly:</strong> Smaller code size, marked with NoInlining for recursive method</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Partition (base) + Heap + Insertion)</description></item>
/// <item><description>Stable      : No (QuickSort and HeapSort are unstable; element order is not preserved for equal values)</description></item>
/// <item><description>In-place    : Yes (O(log n) auxiliary space for recursion stack, no additional arrays allocated)</description></item>
/// <item><description>Best case   : Θ(n log n) - Occurs when QuickSort consistently creates balanced partitions and InsertionSort handles small subarrays efficiently</description></item>
/// <item><description>Average case: Θ(n log n) - Expected ~1.386n log₂ n comparisons from QuickSort</description></item>
/// <item><description>Worst case  : O(n log n) - Guaranteed by HeapSort fallback when recursion depth exceeds 2⌊log₂(n)⌋</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>dotnet runtime: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/ArraySortHelper.cs</para>
/// </remarks>
public static class IntroSortDotnet
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array

    // Follow dotnet runtime threshold
    private const int IntrosortSizeThreshold = 16;

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
        var depthLimit = 2 * (BitOperations.Log2((uint)(last - first)) + 1);
        IntroSortInternal(s, first, last, depthLimit);
    }

    /// <summary>
    /// Internal IntroSort implementation. This is marked with NoInlining to prevent
    /// the JIT from inlining recursive calls into itself, which would hurt performance.
    /// </summary>
    /// <remarks>
    /// From dotnet runtime comments:
    /// "IntroSort is recursive; block it from being inlined into itself as this is currently not profitable."
    ///
    /// This implementation follows dotnet runtime's approach of using Slice to always work with
    /// 0-based spans, which improves performance especially for sorted/reversed data.
    /// </remarks>
    private static void IntroSortInternal<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right, int depthLimit)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        int partitionSize = right - left;
        while (partitionSize > 1)
        {
            // Small arrays: use InsertionSort
            if (partitionSize <= IntrosortSizeThreshold)
            {
                if (partitionSize == 2)
                {
                    SwapIfGreater(s, left, left + 1);
                    return;
                }

                if (partitionSize == 3)
                {
                    SwapIfGreater(s, left, left + 1);
                    SwapIfGreater(s, left, left + 2);
                    SwapIfGreater(s, left + 1, left + 2);
                    return;
                }

                InsertionSortInternal(s, left, partitionSize);
                return;
            }

            // Max depth reached: switch to HeapSort to guarantee O(n log n)
            if (depthLimit == 0)
            {
                HeapSortInternal(s, left, partitionSize);
                return;
            }
            depthLimit--;

            // Partition and get pivot position (returns position relative to left)
            s.Context.OnPhase(SortPhase.QuickSortPartition, left, left + partitionSize - 1);
            int p = PickPivotAndPartition(s, left, partitionSize);

            // Mark pivot position
            s.Context.OnRole(left + p, BUFFER_MAIN, RoleType.Pivot);

            // Recursively sort right partition, loop on left (tail recursion elimination)
            // Note: pivot at position (left + p) is already in final position, exclude from recursion
            s.Context.OnRole(left + p, BUFFER_MAIN, RoleType.None);
            IntroSortInternal(s, left + p + 1, left + partitionSize, depthLimit);
            partitionSize = p;
        }
    }

    /// <summary>
    /// Swaps elements at indices i and j if element at i is greater than element at j.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int i, int j)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (s.Compare(i, j) > 0)
        {
            s.Swap(i, j);
        }
    }

    /// <summary>
    /// Picks a pivot using median-of-3 and partitions the range.
    /// This follows dotnet runtime's implementation exactly:
    /// - Works with a partition of size 'partitionSize' starting at 'offset'
    /// - Sorts lo(0), mid, hi using SwapIfGreater (median-of-3)
    /// - Moves pivot (mid) to position hi-1
    /// - Partitions using two-pointer scan from both ends
    /// - Places pivot in final position
    /// - Returns the final pivot position RELATIVE to offset (0-based within partition)
    /// </summary>
    private static int PickPivotAndPartition<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int offset, int partitionSize)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // All operations are relative to offset, but we work with indices as if 0-based
        int hi = partitionSize - 1;
        int middle = hi >> 1;

        // Sort lo, mid and hi appropriately, then pick mid as the pivot.
        // This is the dotnet runtime's median-of-3 implementation.
        // Indices are adjusted by offset to access actual array positions
        SwapIfGreater(s, offset + 0, offset + middle);    // swap the low with the mid point
        SwapIfGreater(s, offset + 0, offset + hi);        // swap the low with the high
        SwapIfGreater(s, offset + middle, offset + hi);   // swap the middle with the high

        // Select the middle value as the pivot, and move it to be just before the last element.
        T pivot = s.Read(offset + middle);
        s.Swap(offset + middle, offset + hi - 1);

        // We already partitioned lo and hi and put the pivot in hi - 1.
        // And we pre-increment & decrement below.
        int left = 0;
        int right = hi - 1;

        // Walk the left and right pointers, swapping elements as necessary, until they cross.
        while (left < right)
        {
            // Move left pointer forward while elements are less than pivot
            // Pre-increment: ++left first, then compare
            while (left < hi - 1 && s.Compare(offset + (++left), pivot) < 0) ;

            // Move right pointer backward while elements are greater than pivot
            // Pre-decrement: --right first, then compare
            while (right > 0 && s.Compare(pivot, offset + (--right)) < 0) ;

            if (left >= right)
                break;

            s.Swap(offset + left, offset + right);
        }

        // Put pivot in the right location.
        if (left != hi - 1)
        {
            s.Swap(offset + left, offset + hi - 1);
        }

        // Return position relative to offset (0-based within partition)
        return left;
    }

    /// <summary>
    /// InsertionSort implementation following dotnet runtime's approach.
    /// Works with a partition starting at 'offset' with size 'partitionSize'.
    /// Uses 0-based indexing within the partition.
    /// </summary>
    /// <remarks>
    /// This is a direct port of dotnet runtime's InsertionSort but adapted for SortSpan.
    /// Unlike the shared InsertionSort.SortCore, this version:
    /// - Uses 0-based indexing within partition (matches dotnet runtime)
    /// - Always writes the final value (no j != i-1 optimization)
    /// - Simpler loop structure for better JIT optimization
    /// </remarks>
    private static void InsertionSortInternal<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int offset, int partitionSize)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        for (int i = 0; i < partitionSize - 1; i++)
        {
            T t = s.Read(offset + i + 1);

            int j = i;
            while (j >= 0 && s.Compare(offset + j, t) > 0)
            {
                s.Write(offset + j + 1, s.Read(offset + j));
                j--;
            }

            s.Write(offset + j + 1, t);
        }
    }

    /// <summary>
    /// HeapSort implementation following dotnet runtime's approach.
    /// Works with a partition starting at 'offset' with size 'partitionSize'.
    /// Uses 1-based heap indexing (parent: i/2, left child: 2*i, right child: 2*i+1).
    /// </summary>
    /// <remarks>
    /// This is a direct port of dotnet runtime's HeapSort but adapted for SortSpan.
    /// Key differences from shared HeapSort:
    /// - Uses 1-based heap indexing (simpler parent/child calculations)
    /// - No Floyd's algorithm (uses standard sift-down for both build and extract)
    /// - Simpler implementation optimized for JIT
    /// </remarks>
    private static void HeapSortInternal<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int offset, int partitionSize)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        int n = partitionSize;

        // Build heap (heapify from bottom to top)
        for (int i = n >> 1; i >= 1; i--)
        {
            DownHeap(s, offset, i, n);
        }

        // Extract elements from heap one by one
        for (int i = n; i > 1; i--)
        {
            s.Swap(offset + 0, offset + i - 1);
            DownHeap(s, offset, 1, i - 1);
        }
    }

    /// <summary>
    /// Restores the heap property for a subtree using sift-down operation.
    /// Uses 1-based heap indexing where:
    /// - Parent of node i is at i/2
    /// - Left child of node i is at 2*i
    /// - Right child of node i is at 2*i+1
    /// </summary>
    /// <param name="s">The SortSpan to operate on</param>
    /// <param name="offset">Starting position of the heap in the span</param>
    /// <param name="i">1-based index of the node to sift down</param>
    /// <param name="n">Size of the heap (1-based)</param>
    private static void DownHeap<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int offset, int i, int n)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Store the value to sift down
        T d = s.Read(offset + i - 1);

        // Continue while node has at least a left child
        while (i <= n >> 1)
        {
            int child = 2 * i;  // Left child

            // If right child exists and is greater than left child, use right child
            if (child < n && s.Compare(offset + child - 1, offset + child) < 0)
            {
                child++;
            }

            // If current value is greater than or equal to the larger child, we're done
            if (s.Compare(d, s.Read(offset + child - 1)) >= 0)
                break;

            // Move the larger child up
            s.Write(offset + i - 1, s.Read(offset + child - 1));
            i = child;
        }

        // Place the value in its final position
        s.Write(offset + i - 1, d);
    }
}

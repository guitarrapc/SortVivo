using SortAlgorithm.Contexts;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 未ソート部分から最大の要素を見つけ、それを先頭にフリップ（逆転）し、次に先頭から未ソート部分の最後の要素までフリップして配置します。
/// このプロセスを繰り返すことで、配列をソートします。
/// <br/>
/// Finds the maximum element in the unsorted portion, flips (reverses) the array to bring it to the front, and then flips the array up to the last unsorted element to place the maximum element in its correct position. This process is repeated until the array is sorted.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Pancake Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Maximum Finding:</strong> For each unsorted subarray [0..currentSize), find the index of the maximum element.
/// This requires comparing all elements in the range, performing (currentSize - 1) comparisons per iteration.</description></item>
/// <item><description><strong>Two-Flip Positioning:</strong> To move the maximum element to its correct position at (currentSize - 1):
/// <list type="bullet">
/// <item>First flip: Reverse [0..maxIndex] to bring the maximum to position 0</item>
/// <item>Second flip: Reverse [0..currentSize-1] to place the maximum at its final position</item>
/// </list>
/// If the maximum is already at (currentSize - 1), skip both flips.</description></item>
/// <item><description><strong>Range Reduction:</strong> After placing the maximum, reduce the unsorted range by 1 and repeat.
/// The algorithm terminates when currentSize reaches 1 (entire array sorted).</description></item>
/// <item><description><strong>Flip Correctness:</strong> Each flip reverses a subarray by swapping elements symmetrically:
/// swap(start, end), swap(start+1, end-1), ..., until start ≥ end. This preserves all elements while changing their order.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Selection (selects maximum and places it in correct position)</description></item>
/// <item><description>Stable      : No (flipping changes relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n²) - Already sorted, still must find maximum in each iteration (no flips needed)</description></item>
/// <item><description>Average case: Θ(n²) - Comparisons are always n(n-1)/2; average ~3n/2 flips</description></item>
/// <item><description>Worst case  : O(n²) - Maximum 2(n-1) flips when maximum is always at position 0</description></item>
/// <item><description>Comparisons : n(n-1)/2 - Always the same regardless of input (sum of 1 to n-1)</description></item>
/// <item><description>Swaps       : 0 to n(n-1)/2 - Each flip of length k performs k/2 swaps; worst case 2n flips of average length n/2</description></item>
/// <item><description>Index Reads : 2 × comparisons + 2 × swaps - Comparisons read 2 elements; swaps read and write 2 elements each</description></item>
/// <item><description>Index Writes: 2 × swaps - Each swap writes 2 elements</description></item>
/// </list>
/// <para><strong>Note:</strong> The Pancake Sorting Problem asks for the minimum number of flips to sort any array.
/// This implementation uses a simple greedy approach (not necessarily optimal) that guarantees at most 2(n-1) flips.
/// The optimal bound is between (15n/14) and (18n/11) flips for worst-case inputs.</para>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Pancake_sorting</para>
/// </remarks>
public static class PancakeSort
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
        for (var currentSize = last; currentSize > first; currentSize--)
        {
            // Find max in [first..currentSize-1] with CurrentMax role tracking
            s.Context.OnPhase(SortPhase.PancakeFindMax, first, currentSize - 1);
            var maxIndex = first;
            s.Context.OnRole(maxIndex, BUFFER_MAIN, RoleType.CurrentMax);
            for (var i = first + 1; i < currentSize; i++)
            {
                if (s.Compare(i, maxIndex) > 0)
                {
                    s.Context.OnRole(maxIndex, BUFFER_MAIN, RoleType.None);
                    maxIndex = i;
                    s.Context.OnRole(maxIndex, BUFFER_MAIN, RoleType.CurrentMax);
                }
            }
            s.Context.OnRole(maxIndex, BUFFER_MAIN, RoleType.None);

            // Max element is already at the end
            if (maxIndex == currentSize - 1)
                continue;

            // Move the maximum element to the front, then flip to the right position
            Flip(s, first, maxIndex);
            Flip(s, first, currentSize - 1);
        }
    }

    /// <summary>
    /// Finds the index of the maximum element within the first n elements of the span.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="first"></param>
    /// <param name="last"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MaxIndex<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int first, int last)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var maxIndex = first;
        for (var i = first + 1; i < last; i++)
        {
            if (s.Compare(i, maxIndex) > 0)
            {
                maxIndex = i;
            }
        }
        return maxIndex;
    }

    /// <summary>
    /// Reverses the elements in the span from start to end.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Flip<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int start, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (start < end)
        {
            s.Swap(start, end);
            start++;
            end--;
        }
    }
}

using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 配列の両端から中央に向かってソートする選択ソートの最適化版です。各イテレーションで未ソート領域から最小値と最大値を同時に見つけ、
/// それぞれ左端と右端に配置します。通常の選択ソートと比較してイテレーション回数が約半分になります。
/// <br/>
/// An optimized variant of selection sort that works from both ends toward the center. In each iteration, it finds both the minimum and maximum
/// elements in the unsorted region and places them at the left and right boundaries respectively. This reduces the number of iterations to approximately half compared to standard selection sort.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Double Selection Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Dual Partition Invariant:</strong> Maintain three regions in the array: a sorted prefix [0..left), an unsorted middle [left..right], and a sorted suffix (right..n).
/// After iteration k, the first k elements contain the k smallest elements in sorted order, and the last k elements contain the k largest elements in sorted order.
/// This invariant must hold at the start and end of each iteration.</description></item>
/// <item><description><strong>Simultaneous Min-Max Selection:</strong> For each pair of boundary positions (left, right), correctly identify both the minimum and maximum elements
/// in the unsorted region [left..right]. This requires comparing each element with both the current minimum and maximum candidates,
/// ensuring no smaller or larger element is overlooked in a single pass.</description></item>
/// <item><description><strong>Index-Aware Swap Operation:</strong> Exchange the minimum and maximum elements with the boundary positions while handling index overlap cases:
/// <list type="bullet">
/// <item><description>If min is at left and max is at right: No swaps needed (already in place)</description></item>
/// <item><description>If max is at left: Swap max to right first, then adjust min index if it was at right (now at left after first swap)</description></item>
/// <item><description>If min is at right: Swap min to left first, then adjust max index if it was at left (now at right after first swap)</description></item>
/// <item><description>Standard case: Swap min to left and max to right independently (no index adjustment needed)</description></item>
/// </list>
/// The critical requirement is to track how the first swap affects the position of the second element to be swapped.</description></item>
/// <item><description><strong>Dual Boundary Advancement:</strong> After each iteration, increment left by 1 and decrement right by 1.
/// This simultaneously shrinks the unsorted region from both ends until left >= right (0 or 1 element remains, which is automatically in place).</description></item>
/// <item><description><strong>Comparison Consistency:</strong> All element comparisons must use a total order relation (transitive, antisymmetric, total).
/// The IComparable&lt;T&gt;.CompareTo implementation must satisfy these properties for correctness.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Selection</description></item>
/// <item><description>Stable      : No (swapping non-adjacent elements can change relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Θ(n²) - Always performs approximately n²/2 comparisons regardless of input order</description></item>
/// <item><description>Average case: Θ(n²) - Same comparison count; swap count varies but doesn't dominate</description></item>
/// <item><description>Worst case  : Θ(n²) - Same comparison count; approximately n swaps when reverse sorted</description></item>
/// <item><description>Comparisons : Θ(n²) - Approximately n(n-1)/2 comparisons (two comparisons per element in unsorted region)</description></item>
/// <item><description>Swaps       : O(n) - At most n swaps (2 per iteration); best case 0 (already sorted), worst case ~n</description></item>
/// <item><description>Iterations  : ⌈n/2⌉ - Half the number of iterations compared to standard selection sort</description></item>
/// </list>
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>Situations where reducing iteration overhead is beneficial (even with same asymptotic complexity)</description></item>
/// <item><description>Educational purposes to demonstrate algorithm optimization techniques</description></item>
/// <item><description>When write operations are expensive and you want to minimize swap count compared to bubble sort</description></item>
/// <item><description>Small datasets where the constant factor improvement over selection sort matters</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Selection_sort</para>
/// </remarks>
public static class DoubleSelectionSort
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
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(first, last);

        if (span.Length <= 1) return;

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
        var left = first;
        var right = last - 1;

        while (left < right)
        {
            s.Context.OnPhase(SortPhase.DoubleSelectionFindMinMax, left, right);
            s.Context.OnRole(left, BUFFER_MAIN, RoleType.LeftPointer);
            s.Context.OnRole(right, BUFFER_MAIN, RoleType.RightPointer);

            var min = left;
            var max = left;
            s.Context.OnRole(min, BUFFER_MAIN, RoleType.CurrentMin);

            // Find both minimum and maximum in the unsorted region [left..right]
            for (var i = left + 1; i <= right; i++)
            {
                if (s.Compare(i, min) < 0)
                {
                    s.Context.OnRole(min, BUFFER_MAIN, RoleType.None);
                    min = i;
                    s.Context.OnRole(min, BUFFER_MAIN, RoleType.CurrentMin);
                }
                if (s.Compare(i, max) > 0)
                {
                    s.Context.OnRole(max, BUFFER_MAIN, RoleType.None);
                    max = i;
                    s.Context.OnRole(max, BUFFER_MAIN, RoleType.CurrentMax);
                }
            }

            // Clear all roles before swaps
            s.Context.OnRole(left, BUFFER_MAIN, RoleType.None);
            s.Context.OnRole(right, BUFFER_MAIN, RoleType.None);
            s.Context.OnRole(min, BUFFER_MAIN, RoleType.None);
            s.Context.OnRole(max, BUFFER_MAIN, RoleType.None);

            // Swap operations with careful index tracking
            // When max is at left boundary, swap it to right first
            if (max == left)
            {
                s.Swap(left, right);

                // After swap: what was at right is now at left
                // If min was at right, after the swap min is now at left
                // If min was at left (same as max), after swap it's at right
                // Adjust min index accordingly
                if (min == right)
                {
                    min = left;
                }
                else if (min == left)
                {
                    min = right;
                }

                // Now place min at left if it's not already there
                if (min != left)
                {
                    s.Swap(min, left);
                }
            }
            // When min is at right boundary (and max is not at left)
            else if (min == right)
            {
                s.Swap(right, left);

                // After swap: what was at left is now at right
                // If max was at left, it's now at right - adjust max index
                if (max == left)
                {
                    max = right;
                }

                // Now place max at right if it's not already there
                if (max != right)
                {
                    s.Swap(max, right);
                }
            }
            // Standard case: neither min nor max is at a boundary
            else
            {
                // Swap min to left first
                if (min != left)
                {
                    s.Swap(min, left);

                    // If max was at left, it's now at min's old position
                    if (max == left)
                    {
                        max = min;
                    }
                }

                // Then swap max to right
                if (max != right)
                {
                    s.Swap(max, right);
                }
            }

            left++;
            right--;
        }
    }
}

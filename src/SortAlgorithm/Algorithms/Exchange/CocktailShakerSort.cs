using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 配列を双方向に走査してソートを行う改良型バブルソートです。前方と後方のパスを交互に実行し、各パスで最大値と最小値を同時に確定位置に移動させます。
/// 部分的にソート済みのデータに対してバブルソートより高速に動作し、最後のスワップ位置を記録することで未ソート範囲を効率的に縮小します。
/// <br/>
/// An improved bidirectional variant of bubble sort that alternates between forward and backward passes. Each iteration simultaneously moves the largest value to the end and the smallest value to the beginning.
/// Performs better than bubble sort on partially sorted data by tracking the last swap position to efficiently shrink the unsorted range.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Cocktail Shaker Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Bidirectional Scanning:</strong> The algorithm alternates between forward passes (left to right) and backward passes (right to left).
/// Each complete iteration performs both passes, settling elements at both ends of the array.</description></item>
/// <item><description><strong>Adjacent Element Comparison:</strong> Only adjacent elements are compared in each pass.
/// Forward pass compares (i, i+1), backward pass compares (i, i-1), ensuring stability.</description></item>
/// <item><description><strong>Conditional Swap:</strong> Elements are swapped only when out of order (forward: i &gt; i+1, backward: i &lt; i-1).
/// This preserves the relative order of equal elements, maintaining stability.</description></item>
/// <item><description><strong>Range Optimization:</strong> The last swap position in each pass becomes the new boundary (max/min).
/// This optimization eliminates redundant comparisons in already-sorted regions.</description></item>
/// <item><description><strong>Early Termination:</strong> The algorithm terminates when min == max or when no swaps occur in a pass.
/// This allows O(n) performance for already-sorted or nearly-sorted input.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Exchange</description></item>
/// <item><description>Stable      : Yes (equal elements never swapped due to strict comparison)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) - Already sorted, only one forward pass with n-1 comparisons</description></item>
/// <item><description>Average case: O(n²) - Expected n(n-1)/4 swaps for random input</description></item>
/// <item><description>Worst case  : O(n²) - Reverse-sorted input requires n(n-1)/2 comparisons and swaps</description></item>
/// <item><description>Comparisons : Best O(n), Average/Worst O(n²)</description></item>
/// <item><description>Swaps       : Best 0, Average n(n-1)/4, Worst n(n-1)/2</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Cocktail_shaker_sort</para>
/// </remarks>
public static class CocktailShakerSort
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
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        var min = 0;
        var max = s.Length - 1;
        int pass = 1;

        while (min < max)
        {
            // Forward pass: bubble max to the right
            context.OnPhase(SortPhase.CocktailForwardPass, pass, min, max);
            context.OnRole(min, BUFFER_MAIN, RoleType.LeftPointer);
            context.OnRole(max, BUFFER_MAIN, RoleType.RightPointer);
            var lastSwapIndex = min;
            for (var i = min; i < max; i++)
            {
                if (s.Compare(i, i + 1) > 0)
                {
                    s.Swap(i, i + 1);
                    lastSwapIndex = i;
                }
            }

            context.OnRole(min, BUFFER_MAIN, RoleType.None);
            context.OnRole(max, BUFFER_MAIN, RoleType.None);
            max = lastSwapIndex;
            if (min >= max) break;

            // Backward pass: bubble min to the left
            context.OnPhase(SortPhase.CocktailBackwardPass, pass, min, max);
            context.OnRole(min, BUFFER_MAIN, RoleType.LeftPointer);
            context.OnRole(max, BUFFER_MAIN, RoleType.RightPointer);
            lastSwapIndex = max;
            for (var i = max; i > min; i--)
            {
                if (s.Compare(i - 1, i) > 0)
                {
                    s.Swap(i - 1, i);
                    lastSwapIndex = i;
                }
            }

            context.OnRole(min, BUFFER_MAIN, RoleType.None);
            context.OnRole(max, BUFFER_MAIN, RoleType.None);
            min = lastSwapIndex;
            if (min >= max) break;
            pass++;
        }
    }
}

/// <summary>
/// カクテルシェイカーソートの非最適化版です。固定された範囲で双方向スキャンを行い、最後のスワップ位置による範囲縮小を行いません。
/// 早期終了機能はありますが、各イテレーションで毎回全範囲をスキャンするため、最適化版より多くの比較を実行します。
/// <br/>
/// Non-optimized version of Cocktail Shaker Sort. Performs bidirectional scanning with fixed ranges without tracking the last swap position for range reduction.
/// Has early termination when no swaps occur, but scans the full range in each iteration, resulting in more comparisons than the optimized version.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Cocktail Shaker Sort (Non-Optimized):</strong></para>
/// <list type="number">
/// <item><description><strong>Bidirectional Scanning:</strong> The algorithm alternates between forward passes (left to right) and backward passes (right to left).
/// Unlike the optimized version, scan ranges are fixed based on iteration count (i), not adaptive based on swap positions.</description></item>
/// <item><description><strong>Adjacent Element Comparison:</strong> Only adjacent elements are compared in each pass.
/// Forward pass compares (j, j+1), backward pass compares (j, j-1), ensuring stability.</description></item>
/// <item><description><strong>Conditional Swap:</strong> Elements are swapped only when out of order (forward: j &gt; j+1, backward: j &lt; j-1).
/// This preserves the relative order of equal elements, maintaining stability.</description></item>
/// <item><description><strong>Fixed Range Iteration:</strong> Each iteration i processes ranges [i, n-i-1] for forward and [n-i-2, i] for backward.
/// The outer loop runs n/2 times, regardless of the actual sorted state within those ranges.</description></item>
/// <item><description><strong>Early Termination:</strong> The algorithm terminates early if no swaps occur in both passes of an iteration.
/// For sorted input, terminates after the first iteration (2n-3 comparisons).</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Exchange</description></item>
/// <item><description>Stable      : Yes (equal elements never swapped due to strict comparison)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) - Already sorted, first iteration only with 2n-3 comparisons</description></item>
/// <item><description>Average case: O(n²) - Expected n(n-1)/4 swaps for random input</description></item>
/// <item><description>Worst case  : O(n²) - Reverse-sorted input requires n(n-1)/2 comparisons and swaps</description></item>
/// <item><description>Comparisons : Best O(n), Average/Worst O(n²) - More comparisons than optimized version for partially sorted data</description></item>
/// <item><description>Swaps       : Best 0, Average n(n-1)/4, Worst n(n-1)/2</description></item>
/// </list>
/// </remarks>
public static class CocktailShakerSortNonOptimized
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
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        // half calculation
        for (int i = 0; i < s.Length / 2; i++)
        {
            var swapped = false;
            int pass = i + 1;
            int lo = i;
            int hi = s.Length - 1 - i;

            // Forward pass: bubble max to the right
            context.OnPhase(SortPhase.CocktailForwardPass, pass, lo, hi);
            context.OnRole(lo, BUFFER_MAIN, RoleType.LeftPointer);
            context.OnRole(hi, BUFFER_MAIN, RoleType.RightPointer);
            for (int j = i; j < s.Length - i - 1; j++)
            {
                if (s.Compare(j, j + 1) > 0)
                {
                    s.Swap(j, j + 1);
                    swapped = true;
                }
            }

            context.OnRole(lo, BUFFER_MAIN, RoleType.None);
            context.OnRole(hi, BUFFER_MAIN, RoleType.None);

            // Backward pass: bubble min to the left
            context.OnPhase(SortPhase.CocktailBackwardPass, pass, lo, hi);
            context.OnRole(lo, BUFFER_MAIN, RoleType.LeftPointer);
            context.OnRole(hi, BUFFER_MAIN, RoleType.RightPointer);
            for (int j = s.Length - 2 - i; j > i; j--)
            {
                if (s.Compare(j, j - 1) < 0)
                {
                    s.Swap(j, j - 1);
                    swapped = true;
                }
            }

            context.OnRole(lo, BUFFER_MAIN, RoleType.None);
            context.OnRole(hi, BUFFER_MAIN, RoleType.None);
            if (!swapped) break;
        }
    }
}

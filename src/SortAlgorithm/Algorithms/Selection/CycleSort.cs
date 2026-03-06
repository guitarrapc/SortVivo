using SortAlgorithm.Contexts;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 各要素を最終位置に直接配置するサイクル検出アルゴリズムです。書き込み回数を理論的に最小化（最大n回）し、メモリ書き込みコストが高い環境で有用です。
/// 要素の正しい位置を計算し、そこにある要素と交換しながらサイクルを辿ります。
/// <br/>
/// A cycle-detection algorithm that places each element directly in its final position. Minimizes writes to the theoretical minimum (at most n writes), useful in environments with expensive memory write operations.
/// Calculates the correct position of each element and follows cycles by swapping displaced elements.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Cycle Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Cycle Detection:</strong> The algorithm must identify permutation cycles in the array.
/// Each element belongs to exactly one cycle. By placing an element in its correct position and following the displacement chain,
/// the algorithm completes each cycle without revisiting positions.</description></item>
/// <item><description><strong>Position Calculation:</strong> For each element, count how many elements to its right are smaller.
/// This count determines the element's final sorted position. This requires O(n) comparisons per element.</description></item>
/// <item><description><strong>Minimal Writes:</strong> Each element is written to memory at most once after being read from its initial position.
/// For an array requiring k transpositions, exactly k writes occur (theoretically optimal for in-place sorting).</description></item>
/// <item><description><strong>Duplicate Handling:</strong> When placing an element at its calculated position, skip over duplicates
/// to find the next available slot. This ensures stability issues don't cause infinite loops.</description></item>
/// <item><description><strong>Cycle Completion:</strong> Continue rotating elements within a cycle until returning to the starting position.
/// Once a cycle is completed, move to the next unprocessed position to start a new cycle.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Selection (though often classified separately due to unique cycle-based approach)</description></item>
/// <item><description>Stable      : No (relative order of equal elements is not preserved)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space, only constant extra variables)</description></item>
/// <item><description>Best case   : Θ(n²) - Always performs n(n-1)/2 comparisons regardless of input</description></item>
/// <item><description>Average case: Θ(n²) - Comparisons dominate; writes are O(n) on average</description></item>
/// <item><description>Worst case  : Θ(n²) - Same comparison count; maximum n-1 writes when all elements displaced</description></item>
/// <item><description>Comparisons : Θ(n²) - Always n(n-1)/2 comparisons to calculate all positions</description></item>
/// <item><description>Writes      : O(n) - Theoretically optimal; at most n writes (each element placed once)</description></item>
/// <item><description>Reads       : Θ(n²) - Multiple reads during position calculation phase</description></item>
/// </list>
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>Flash memory or EEPROM with limited write cycles</description></item>
/// <item><description>Systems where memory writes are significantly more expensive than comparisons</description></item>
/// <item><description>Educational purposes to demonstrate cycle detection in permutations</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Quicksort</para>
/// </remarks>
public static class CycleSort
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

        for (var cycleStart = 0; cycleStart < span.Length - 1; cycleStart++)
        {
            context.OnPhase(SortPhase.CycleSortCycle, cycleStart, span.Length - 1);
            context.OnRole(cycleStart, BUFFER_MAIN, RoleType.LeftPointer);

            var item = s.Read(cycleStart);
            var pos = FindPosition(ref s, item, cycleStart);

            // If the item is already in the correct position, skip
            if (pos == cycleStart)
            {
                context.OnRole(cycleStart, BUFFER_MAIN, RoleType.None);
                continue;
            }

            // Skip duplicates
            pos = SkipDuplicates(ref s, item, pos);

            // Put the item at its correct position
            var temp = s.Read(pos);
            s.Write(pos, item);
            item = temp;

            // Rotate the rest of the cycle
            while (pos != cycleStart)
            {
                pos = FindPosition(ref s, item, cycleStart);
                pos = SkipDuplicates(ref s, item, pos);

                temp = s.Read(pos);
                s.Write(pos, item);
                item = temp;
            }

            context.OnRole(cycleStart, BUFFER_MAIN, RoleType.None);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindPosition<T, TComparer, TContext>(ref SortSpan<T, TComparer, TContext> s, T value, int start)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var pos = start;
        for (var i = start + 1; i < s.Length; i++)
        {
            if (s.Compare(i, value) < 0)
            {
                pos++;
            }
        }
        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SkipDuplicates<T, TComparer, TContext>(ref SortSpan<T, TComparer, TContext> s, T value, int pos)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (pos < s.Length && s.Compare(value, pos) == 0)
        {
            pos++;
        }
        return pos;
    }
}

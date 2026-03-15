using SortAlgorithm.Contexts;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 配列をランダムにシャッフルし、ソートされているかを確認することを繰り返す、非常に非効率なソートアルゴリズムです。Permutation Sortとも呼ばれます。
/// 10要素程度が現実的な時間で終了する限界です。
/// <br/>
/// Continuously shuffles the array randomly until it is sorted, checking after each shuffle. This approach is extremely inefficient and impractical for sorting.
/// 10 elements is about the limit for completing in a realistic time.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Bogosort:</strong></para>
/// <list type="number">
/// <item><description><strong>Uniform Permutation Generation:</strong> Each of the n! permutations must be generated with equal probability (1/n!).
/// This implementation uses the Fisher-Yates shuffle algorithm, which guarantees uniform distribution.</description></item>
/// <item><description><strong>Sortedness Verification:</strong> After each shuffle, the algorithm must verify if the array is sorted.
/// This is done by comparing adjacent elements in O(n) time.</description></item>
/// <item><description><strong>Probabilistic Termination:</strong> The algorithm terminates when a sorted permutation is found.
/// Expected number of shuffles: (e-1) × n! ≈ 1.718 × n!</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Joke / Exchange</description></item>
/// <item><description>Stable      : No (random shuffling does not preserve relative order)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Ω(n) - Already sorted, only one verification pass needed</description></item>
/// <item><description>Average case: Θ(n × n!) - Expected (e-1) × n! shuffles, each taking O(n) time and swaps</description></item>
/// <item><description>Worst case  : Unbounded - Theoretically could run forever (though probability diminishes exponentially)</description></item>
/// <item><description>Comparisons : (e-1) × n × (n-1) / 2 on average - Each verification performs n-1 comparisons</description></item>
/// <item><description>Swaps       : (n-1) × n! on average - Each shuffle performs n-1 swaps via Fisher-Yates</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Bogosort</para>
/// </remarks>
public static class BogoSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const uint XORSHIFT_SEED = 0x9E3779B9u; // Golden ratio derived; deterministic seed for reproducible shuffle sequence

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
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context, uint seed = XORSHIFT_SEED)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        var rngState = seed == 0 ? XORSHIFT_SEED : seed;

        var attempt = 0;
        while (!IsSorted(s))
        {
            attempt++;
            context.OnPhase(SortPhase.BogoShuffle, attempt);
            Shuffle(s, ref rngState);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Shuffle<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, ref uint rngState)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Fisher-Yates shuffle - すべての順列を均等な確率で生成
        var length = s.Length;
        for (var i = length - 1; i > 0; i--)
        {
            var j = (int)(NextRandom(ref rngState) % (uint)(i + 1));
            s.Swap(i, j);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSorted<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = s.Length;
        for (var i = 0; i < length - 1; i++)
        {
            if (s.Compare(i, i + 1) > 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Generates the next pseudo-random value using xorshift32.
    /// Deterministic: same seed always produces the same sequence, ensuring reproducible
    /// shuffle attempts for consistent visualization, statistics, and benchmarks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint NextRandom(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }
}

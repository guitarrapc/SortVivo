using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// バッチャー奇偶マージソート - 奇偶マージネットワークを使って2つの整列済み列をマージするソーティングネットワークアルゴリズムです。
/// 任意のサイズの入力に対応します。
/// <br/>
/// Batcher Odd-Even Merge Sort - A sorting network algorithm that uses odd-even merge networks to merge two sorted sequences.
/// Supports arbitrary input sizes.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Batcher Odd-Even Merge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Odd-Even Merge Definition:</strong> Given two sorted sequences A and B,
/// the odd-even merge recursively merges the odd-indexed elements of A and B, then merges the even-indexed elements,
/// then performs a sequence of compare-and-swap operations on adjacent interleaved pairs.
/// This produces a sorted merged sequence.</description></item>
/// <item><description><strong>Arbitrary Size Support:</strong> Unlike Bitonic Sort (which requires power-of-2 sizes),
/// this implementation uses Knuth's TAOCP formulation with an integer-division group check
/// <c>(j+i)/(p+p) == (j+i+k)/(p+p)</c> that correctly handles partial blocks at boundaries,
/// making the algorithm applicable to any input size.</description></item>
/// <item><description><strong>Iterative Network Construction:</strong> The algorithm uses the Knuth TAOCP Vol. 3
/// iterative formulation. The outer loop <c>p</c> controls the merge group size (doubling each time).
/// For each merge size, the inner loops <c>k</c> and <c>j</c> generate all compare-and-swap pairs
/// required by the odd-even merge network at that level.</description></item>
/// <item><description><strong>Comparison Network Property:</strong> Like Bitonic Sort, Batcher Odd-Even Merge Sort
/// is a comparison network (data-oblivious). The same comparisons are performed regardless of input values,
/// making it suitable for parallel implementations and providing worst-case guarantees.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Network Sort / Merge</description></item>
/// <item><description>Stable      : No (non-adjacent swaps can change relative order of equal elements)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : Θ(n log² n) - Data-independent comparison count (same as worst case)</description></item>
/// <item><description>Average case: Θ(n log² n) - Performs the same comparisons for all inputs</description></item>
/// <item><description>Worst case  : Θ(n log² n) - For n = 2^k: (1/4) × n × log n × (log n + 1) comparisons</description></item>
/// <item><description>Parallel depth: O(log² n) - Network depth allows O(log² n) parallel time</description></item>
/// </list>
/// <para><strong>Implementation Notes:</strong></para>
/// <list type="bullet">
/// <item><description>Supports arbitrary input sizes (not restricted to power of 2).</description></item>
/// <item><description>True in-place sorting with zero heap allocation.</description></item>
/// <item><description>Uses Knuth TAOCP Vol. 3 iterative formulation (four nested loops).</description></item>
/// <item><description>Inner group check <c>(j+i)/(p+p) == (j+i+k)/(p+p)</c> ensures comparisons stay within the same merge block.</description></item>
/// <item><description>Generates fewer total comparisons than Bitonic Sort for the same input size.</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Knuth, D.E., The Art of Computer Programming Vol. 3: Sorting and Searching, Algorithm 5.3.4N.</para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Batcher_odd%E2%80%93even_mergesort</para>
/// </remarks>
public static class BatcherOddEvenMergeSort
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
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of the sort context.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCoreIterative(s, span.Length);
    }

    /// <summary>
    /// Iterative Batcher odd-even merge sorting network.
    /// Repeatedly merges adjacent sorted blocks using odd-even compare-exchange stages.
    /// The comparison pattern is data-oblivious and determined solely by the network structure.
    /// </summary>
    /// <param name="s">The span to sort.</param>
    /// <param name="low">The starting index of the sequence.</param>
    /// <param name="count">The length of the sequence.</param>
    private static void SortCoreIterative<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int count)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Iterative Batcher odd-even merge sorting network. iterative formulation (Knuth TAOCP Vol. 3, Algorithm 5.3.4N).
        // Repeatedly merges adjacent sorted blocks using data-oblivious compare-exchange stages.
        for (var p = 1; p < count; p <<= 1)
        {
            s.Context.OnPhase(SortPhase.OddEvenMergeSortPass, p, count);

            // Inner loop: k is the comparison distance within the odd-even merge network.
            // Starts at p (full merge distance) and halves down to 1.
            for (var k = p; k >= 1; k >>= 1)
            {
                s.Context.OnPhase(SortPhase.OddEvenMergeSortStage, k, p, count);

                // j iterates over the starting positions of each comparison group.
                // k % p gives the correct starting offset for each stage.
                for (var j = k % p; j < count - k; j += k + k)
                {
                    // i iterates within each comparison group of size k.
                    for (var i = 0; i < k; i++)
                    {
                        // Bounds check: j+i+k must be a valid index.
                        // Group check: both elements must belong to the same merge block (size 2p).
                        // The bounds and block-membership checks avoid invalid compare-exchanges
                        // for incomplete blocks near the end of the array.
                        int left = j + i;
                        int right = left + k;
                        if (right < count && left / (p << 1) == right / (p << 1))
                        {
                            CompareAndSwap(s, left, right);
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CompareAndSwap<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int i, int j)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (s.Compare(i, j) > 0)
        {
            s.Swap(i, j);
        }
    }
}

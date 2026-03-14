using System.Buffers;
using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// StrandSortは入力から「ソート済み連（strand）」を繰り返し引き抜き、マージして整列済み列を構築するアルゴリズムです。
/// 各パスで先頭要素から始まる最長の非減少部分列（連）を左から貪欲に選び、残余要素を圧縮し直します。
/// 抽出した連を累積済み結果にマージすることを繰り返すことで、最終的にすべての要素が整列されます。
/// <br/>
/// Strand Sort repeatedly extracts sorted subsequences ("strands") from the remaining elements and merges
/// them into an accumulated result. Each pass greedily builds a strand from left to right: starting with
/// the first remaining element, it appends any subsequent element that is ≥ the current strand tail.
/// The strand is then merged with the accumulated sorted output, and the process repeats until no elements remain.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Strand Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Phase 1 - Strand Extraction:</strong> Scan remaining elements left to right.
/// Start the strand with the first element. For each subsequent element, append it to the strand if it is
/// ≥ the current strand tail; otherwise keep it in the remaining pool (compacted in place).
/// This guarantees each extracted strand is non-decreasing.</description></item>
/// <item><description><strong>Phase 2 - Merge:</strong> Perform a standard 2-way merge of the newly extracted strand
/// and the accumulated sorted result. The merged output is written directly to the main span, which serves
/// as scratch space (all original values have already been copied to the remaining buffer).
/// On non-final passes the merged data is copied to the result buffer for the next iteration;
/// on the final pass the main span already holds the sorted array.</description></item>
/// <item><description><strong>Remaining Compaction:</strong> Elements skipped during strand extraction are
/// shifted left in the remaining buffer in a single O(k) pass, where k is the current remaining count.
/// No additional allocation is required for compaction.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Adaptive merge</description></item>
/// <item><description>Stable      : No (equal elements from different strands may be reordered during merge)</description></item>
/// <item><description>In-place    : No (requires O(n) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) - Already sorted input produces one strand; merge is trivial</description></item>
/// <item><description>Average case: O(n√n) - Random input typically produces O(√n) strands</description></item>
/// <item><description>Worst case  : O(n²) - Reverse-sorted input produces n strands of length 1</description></item>
/// <item><description>Comparisons : O(n × passes) - one comparison per element per extraction pass plus merge work</description></item>
/// <item><description>Writes      : O(n × passes) - each merge writes all accumulated elements</description></item>
/// <item><description>Space       : O(n) - three flat ArrayPool buffers: remaining, strand, result</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Strand_sort</para>
/// </remarks>
public static class StrandSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;        // Main input array (merge scratch)
    private const int BUFFER_REMAINING = 1;   // Elements not yet placed in any strand
    private const int BUFFER_STRAND = 2;      // Current strand being extracted
    private const int BUFFER_RESULT = 3;      // Accumulated sorted result

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
    /// <typeparam name="TComparer">The type of the comparer. Must implement <see cref="IComparer{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer used to determine the order of elements.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCore(s, comparer, context);
    }

    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var n = s.Length;
        var remainingBuffer = ArrayPool<T>.Shared.Rent(n);
        var strandBuffer = ArrayPool<T>.Shared.Rent(n);
        var resultBuffer = ArrayPool<T>.Shared.Rent(n);
        try
        {
            // Copy all elements to the remaining buffer; the main span is used as merge scratch
            var remaining = new SortSpan<T, TComparer, TContext>(remainingBuffer.AsSpan(0, n), context, comparer, BUFFER_REMAINING);
            s.CopyTo(0, remaining, 0, n);
            var remainingCount = n;

            var strand = new SortSpan<T, TComparer, TContext>(strandBuffer.AsSpan(0, n), context, comparer, BUFFER_STRAND);
            var result = new SortSpan<T, TComparer, TContext>(resultBuffer.AsSpan(0, n), context, comparer, BUFFER_RESULT);
            var resultLen = 0;

            var strandPass = 0;

            while (remainingCount > 0)
            {
                strandPass++;

                // Phase 1: Extract a sorted strand from the remaining elements
                context.OnPhase(SortPhase.StrandSortExtract, strandPass, remainingCount);

                var strandLen = 0;
                var newRemainingCount = 0;

                // First element always starts the strand
                strand.Write(strandLen++, remaining.Read(0));

                for (var i = 1; i < remainingCount; i++)
                {
                    // Append to strand if >= current tail; otherwise keep in remaining
                    var elem = remaining.Read(i);
                    var tail = strand.Read(strandLen - 1);
                    if (strand.Compare(tail, elem) <= 0)
                        strand.Write(strandLen++, elem);
                    else
                        remaining.Write(newRemainingCount++, elem);
                }
                remainingCount = newRemainingCount;

                // Phase 2: Merge strand[0..strandLen) with result[0..resultLen) → s[0..mergedLen)
                context.OnPhase(SortPhase.StrandSortMerge, strandPass, strandLen, resultLen);

                var mergedLen = strandLen + resultLen;
                var si = 0;
                var ri = 0;
                var di = 0;

                while (si < strandLen && ri < resultLen)
                {
                    var sv = strand.Read(si);
                    var rv = result.Read(ri);
                    // Stable merge: when equal, prefer the existing result element
                    if (s.Compare(sv, rv) < 0)
                    {
                        s.Write(di++, sv);
                        si++;
                    }
                    else
                    {
                        s.Write(di++, rv);
                        ri++;
                    }
                }
                while (si < strandLen) s.Write(di++, strand.Read(si++));
                while (ri < resultLen) s.Write(di++, result.Read(ri++));

                if (remainingCount > 0)
                {
                    // Not the last pass: persist merged result for the next iteration
                    s.CopyTo(0, result, 0, mergedLen);
                    resultLen = mergedLen;
                }
                // Last pass: s[0..n) already contains the sorted array
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(remainingBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            ArrayPool<T>.Shared.Return(strandBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            ArrayPool<T>.Shared.Return(resultBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

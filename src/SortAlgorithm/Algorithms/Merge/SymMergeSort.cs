using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// SymMerge Sortは、SymMergeアルゴリズムを用いたイテレーティブな（ボトムアップ）安定インプレースソートです。
/// RotateMergeSortIterativeと同じボトムアップ構造ですが、マージステップにRotateではなくSymMergeを使用します。
/// SymMergeは各マージで対称二分探索により最適な分割点を1回見つけ、1回のローテーションと2つの再帰呼び出しでマージします。
/// これによりRotateMergeに比べて比較回数がO(n log² n)からO(n log n)に削減されます。
/// <br/>
/// SymMerge Sort is an iterative (bottom-up) stable in-place sort using the SymMerge algorithm.
/// It shares the same bottom-up structure as RotateMergeSortIterative, but replaces the rotation-based
/// merge with SymMerge: a single symmetric binary search finds the optimal split point, then one rotation
/// and two recursive calls complete the merge.
/// This reduces the comparison count from O(n log² n) to O(n log n) compared to RotateMerge.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct SymMerge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Phase 1 – Insertion Sort Seeding:</strong> Every contiguous block of size
/// InsertionSortThreshold is sorted independently with insertion sort.
/// The last block may be shorter; its size is clamped to the remaining element count.</description></item>
/// <item><description><strong>Phase 2 – Bottom-Up Merge Passes:</strong> Starting from
/// <c>width = InsertionSortThreshold</c>, each pass merges adjacent run pairs
/// [left..left+width-1] and [left+width..left+2*width-1], then doubles <c>width</c>.</description></item>
/// <item><description><strong>Already-Sorted Skip:</strong> Before each merge, if
/// <c>s[mid-1] ≤ s[mid]</c> the two runs are already in order and the merge is skipped.</description></item>
/// <item><description><strong>SymMerge Algorithm:</strong> Given sorted runs s[a..m) and s[m..b), computes
/// the midpoint <c>mid = (a+b)/2</c> and pivot sum <c>n = mid+m</c>, then binary-searches for split index
/// <c>start</c>. One rotation of s[start..end) (where end = n-start) brings elements into place,
/// followed by two recursive SymMerge calls on the resulting subproblems [a..start, mid) and [mid, end, b).</description></item>
/// <item><description><strong>Stability Preservation:</strong> The binary search uses ≥ comparison
/// (<c>s[p-c] ≥ s[c]</c> → advance lo), ensuring equal elements from the left run appear before those
/// from the right run in the merged result.</description></item>
/// <item><description><strong>Rotation Algorithm (3-Reversal with fast paths):</strong>
/// Left-rotates s[lo..hi) by (m-lo) positions: [left_part | right_block] → [right_block | left_part].
/// Fast path leftLen==1: move leftmost element to right end.
/// Fast path rightLen==1: move rightmost element to left end.
/// General case uses 3-reversal: Reverse(s[lo..m-1]), Reverse(s[m..hi-1]), Reverse(s[lo..hi-1]).</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Merge + Insertion), Iterative</description></item>
/// <item><description>Stable      : Yes (≥ comparison in binary search preserves relative order)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) – Sorted data: insertion sort is O(n), all phase-2 merges are skipped</description></item>
/// <item><description>Average case: O(n log² n) moves, O(n log n) comparisons</description></item>
/// <item><description>Worst case  : O(n log² n) moves, O(n log n) comparisons</description></item>
/// <item><description>Space       : O(log n) – Recursion stack depth within each SymMerge call</description></item>
/// </list>
/// <para><strong>SymMerge vs RotateMerge:</strong></para>
/// <list type="bullet">
/// <item><description>RotateMerge scans left-to-right and may perform many small rotations, costing
/// O(n log n) comparisons per merge (due to binary search per gallop step)</description></item>
/// <item><description>SymMerge performs exactly one O(log n) binary search per recursive call and one rotation,
/// achieving O(n) comparisons per merge via balanced recursion (T(n) = 2T(n/2) + O(log n) = O(n))</description></item>
/// <item><description>Total comparisons: O(n log n) for SymMergeSort vs O(n log² n) for RotateMergeSortIterative</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Pok-Son Kim and Arne Kutzner, "Stable minimum storage merging by symmetric comparisons" (2004)</para>
/// <para>Go standard library: sort.symMerge (src/sort/sort.go)</para>
/// </remarks>
public static class SymMergeSort
{
    // Threshold for using insertion sort instead of SymMerge
    private const int InsertionSortThreshold = 16;

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;

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
    /// <param name="span">The span of elements to sort in place.</param>
    /// <param name="context">The sort context for tracking operations. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TComparer and TContext type parameters.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCore(s, span.Length);
    }

    /// <summary>
    /// Bottom-up iterative sort core: Phase 1 seeds sorted runs with insertion sort,
    /// Phase 2 merges adjacent run pairs with doubling widths using SymMerge.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the span to sort</param>
    /// <param name="n">Total number of elements (span.Length)</param>
    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int n)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Phase 1: sort every block of size InsertionSortThreshold with insertion sort.
        for (var i = 0; i < n; i += InsertionSortThreshold)
            InsertionSort.SortCore(s, i, Math.Min(i + InsertionSortThreshold, n));

        // Phase 2: bottom-up merge passes using SymMerge.
        // Each pass merges adjacent pairs of runs of length `width`, then doubles width.
        for (var width = InsertionSortThreshold; width < n; width *= 2)
        {
            // left + width < n guarantees a non-empty right run exists.
            for (var left = 0; left + width < n; left += width * 2)
            {
                // mid: exclusive end of left run / start of right run (half-open convention).
                var mid = left + width;
                // right: exclusive end of right run — clamped to last valid index for the final pair.
                var right = Math.Min(left + width * 2, n);

                // Already-sorted skip: left run's max ≤ right run's min → no merge needed.
                if (s.Compare(mid - 1, mid) <= 0)
                    continue;

                SymMerge(s, left, mid, right);
            }
        }
    }

    /// <summary>
    /// Merges two sorted subarrays s[a..m) and s[m..b) in-place stably using the SymMerge algorithm.
    /// Performs a symmetric binary search to find the optimal split index, then one rotation,
    /// and recursively merges the two resulting subproblems.
    /// Based on the algorithm by Pok-Son Kim and Arne Kutzner (2004) as implemented in Go's sort.Stable.
    /// </summary>
    /// <param name="s">The SortSpan to operate on</param>
    /// <param name="a">Inclusive start of the left sorted run (half-open: left run is s[a..m))</param>
    /// <param name="m">Exclusive end of left run / inclusive start of right run (s[m..b))</param>
    /// <param name="b">Exclusive end of the right sorted run</param>
    private static void SymMerge<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int a, int m, int b)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Base cases: empty halves
        if (a >= m || m >= b) return;

        // For small ranges, fall back to insertion sort (adaptive: O(n) for nearly sorted)
        if (b - a <= InsertionSortThreshold)
        {
            InsertionSort.SortCore(s, a, b);
            return;
        }

        // mid: midpoint of the whole range [a..b); pivot sum n = mid + m
        var mid = (int)((uint)(a + b) >> 1);
        var n = mid + m;

        // Binary search bounds: search for split index 'start' such that
        // elements s[a..start) go to the first half and s[start..m) go to the second half.
        // The symmetric mirror of 'start' in the right run is 'end = n - start'.
        int lo, hi;
        if (m > mid)
        {
            // Right run is longer: search in the left portion of the right run
            lo = n - b;
            hi = mid;
        }
        else
        {
            // Left run is longer (or equal): search in the right portion of the left run
            lo = a;
            hi = m;
        }

        // p = n - 1: the index such that indices (c) and (p - c) are mirror positions.
        var p = n - 1;

        // Find the smallest 'lo' such that s[p - lo] < s[lo].
        // When s[p-c] >= s[c], s[c] belongs in the first half → advance lo.
        // The >= condition (not >) ensures stability: equal left-run elements stay before right-run elements.
        while (lo < hi)
        {
            var c = (int)((uint)(lo + hi) >> 1);
            if (s.Compare(p - c, c) >= 0)
                lo = c + 1;
            else
                hi = c;
        }

        var end = n - lo;

        // Rotate s[lo..end) to bring s[m..end) before s[lo..m):
        // [s[a..lo) | s[lo..m) | s[m..end) | s[end..b)]
        //            ^^^^^^^^^   ^^^^^^^^^^
        //            left part   right part  → after rotate: [s[m..end) | s[lo..m)]
        if (lo < m && m < end)
            Rotate(s, lo, m, end);

        // Recursively merge the two remaining subproblems on each half
        if (a < lo && lo < mid)
            SymMerge(s, a, lo, mid);
        if (mid < end && end < b)
            SymMerge(s, mid, end, b);
    }

    /// <summary>
    /// Left-rotates s[lo..hi) by (m - lo) positions: [s[lo..m) | s[m..hi)] → [s[m..hi) | s[lo..m)].
    /// Fast path leftLen==1: shift right and place the single left element at the end.
    /// Fast path rightLen==1: shift left and place the single right element at the start.
    /// General case uses 3-reversal: Reverse(s[lo..m-1]), Reverse(s[m..hi-1]), Reverse(s[lo..hi-1]).
    /// </summary>
    private static void Rotate<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int lo, int m, int hi)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var leftLen = m - lo;
        var rightLen = hi - m;

        // Fast path: single element from left moves to right end
        if (leftLen == 1)
        {
            var tmp = s.Read(lo);
            for (var i = lo; i < hi - 1; i++)
                s.Write(i, s.Read(i + 1));
            s.Write(hi - 1, tmp);
            return;
        }

        // Fast path: single element from right moves to left end
        if (rightLen == 1)
        {
            var tmp = s.Read(hi - 1);
            for (var i = hi - 1; i > lo; i--)
                s.Write(i, s.Read(i - 1));
            s.Write(lo, tmp);
            return;
        }

        // General case: 3-reversal [A|B] → Reverse(A), Reverse(B), Reverse(AB) → [B|A]
        Reverse(s, lo, m - 1);
        Reverse(s, m, hi - 1);
        Reverse(s, lo, hi - 1);
    }

    /// <summary>
    /// Reverses a subarray in-place using swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reverse<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        while (left < right)
        {
            s.Swap(left, right);
            left++;
            right--;
        }
    }
}

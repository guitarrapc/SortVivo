using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 最適化したイテレーティブな（ボトムアップ）Rotate Merge Sortです。
/// 再帰を使わず2フェーズで配列を処理します：フェーズ1は各ブロック（≤InsertionSortThreshold要素）をInsertionSortでソートし、フェーズ2はランの幅を毎パス倍増させながら隣接するランのペアをインプレースローテーションでマージします。
/// 安定ソートであり、追加メモリを使用せずにO(n log² n)の性能を保証します。
/// 再帰版と同等の最適化（InsertionSort・Galloping・3-reversal）を備えつつ、O(log n)のコールスタックを排除してスタックオーバーフローのリスクをなくします。
/// <br/>
/// Iterative (bottom-up) Rotate Merge Sort.
/// Eliminates recursion by processing the array in two phases: Phase 1 sorts each block of ≤InsertionSortThreshold elements with insertion sort, Phase 2 merges adjacent run pairs using in-place rotation while doubling the run width each pass.
/// This algorithm is stable and guarantees O(n log² n) performance without requiring auxiliary space.
/// Retains the same optimizations as the recursive variant (insertion sort, galloping, 3-reversal) while eliminating the O(log n) call-stack overhead and any risk of stack overflow on large inputs.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Iterative Rotate Merge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Phase 1 – Insertion Sort Seeding:</strong> Every contiguous block of size
/// InsertionSortThreshold is sorted independently with insertion sort.
/// The last block may be shorter; its size is clamped to the remaining element count.</description></item>
/// <item><description><strong>Phase 2 – Bottom-Up Merge Passes:</strong> Starting from
/// <c>width = InsertionSortThreshold</c>, each pass merges adjacent run pairs
/// [left..left+width-1] and [left+width..left+2*width-1], then doubles <c>width</c>.
/// The outer loop runs ⌈log₂(n/InsertionSortThreshold)⌉ times.</description></item>
/// <item><description><strong>End-of-Array Clamping:</strong> The right boundary of the last pair is
/// clamped: <c>right = Math.Min(left + 2*width - 1, n - 1)</c>.
/// The loop condition <c>left &lt; n - width</c> guarantees a non-empty right run exists before merging.</description></item>
/// <item><description><strong>Already-Sorted Skip:</strong> Before each merge, if
/// <c>s[mid] ≤ s[mid+1]</c> the two runs are already in order and the merge is skipped,
/// reducing work on nearly-sorted inputs.</description></item>
/// <item><description><strong>Galloping Optimization:</strong> Within each merge, exponential search
/// (1, 2, 4, 8, …) followed by binary search efficiently finds long runs of consecutive right-partition
/// elements, similar to TimSort's galloping mode.</description></item>
/// <item><description><strong>Rotation Algorithm (Left-Rotate by k, 3-Reversal with fast paths):</strong>
/// Left-rotates A[left..right] by k positions: [left_k_elems | rest] → [rest | left_k_elems].
/// Fast path k==1: move leftmost element to right end.
/// Fast path k==n-1: move rightmost element to left end (left rotate n-1 = right rotate 1).
/// General case uses 3-reversal: Reverse(A[left..left+k-1]), Reverse(A[left+k..right]), Reverse(A[left..right]).</description></item>
/// <item><description><strong>Stability Preservation:</strong> Binary search inside the merge uses ≤ comparison,
/// ensuring equal elements from the left run appear before those from the right run.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Merge + Insertion + Galloping), Iterative</description></item>
/// <item><description>Stable      : Yes (≤ comparison in merge preserves relative order)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space)</description></item>
/// <item><description>Best case   : O(n) – Sorted data: insertion sort is O(n), all phase-2 merges are skipped</description></item>
/// <item><description>Average case: O(n log² n) – Binary search (log n) + rotation (n) per merge × log n passes</description></item>
/// <item><description>Worst case  : O(n log² n)</description></item>
/// <item><description>Space       : O(1) – No recursion stack; only a constant number of loop variables</description></item>
/// </list>
/// <para><strong>Iterative vs Recursive:</strong></para>
/// <list type="bullet">
/// <item><description>Eliminates O(log n) call-stack depth; safe for arbitrarily large arrays</description></item>
/// <item><description>Merge order differs: bottom-up processes fixed-width blocks rather than balanced halves,
/// but total work and asymptotic complexity are identical</description></item>
/// <item><description>Slightly simpler control flow; easier to reason about run boundaries</description></item>
/// </list>
/// </remarks>
public static class RotateMergeSort
{
    // Threshold for using insertion sort instead of rotation-based merge
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
    /// Phase 2 merges adjacent run pairs with doubling widths until fully sorted.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the span to sort</param>
    /// <param name="n">Total number of elements (span.Length)</param>
    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int n)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Phase 1: sort every block of size InsertionSortThreshold with insertion sort.
        // InsertionSort.SortCore uses exclusive end [first, last), so pass Math.Min(i + threshold, n).
        for (var i = 0; i < n; i += InsertionSortThreshold)
            InsertionSort.SortCore(s, i, Math.Min(i + InsertionSortThreshold, n));

        // Phase 2: bottom-up merge passes.
        // Each pass merges adjacent pairs of runs of length `width`, then doubles width.
        for (var width = InsertionSortThreshold; width < n; width *= 2)
        {
            // left + width < n guarantees a non-empty right run ([mid+1..right]) exists.
            for (var left = 0; left < n - width; left += width * 2)
            {
                // mid: inclusive end of left run — always exactly `width` elements from `left`.
                var mid = left + width - 1;
                // right: inclusive end of right run — clamped to last valid index for the final pair.
                var right = Math.Min(left + width * 2 - 1, n - 1);

                // Already-sorted skip: left run's max ≤ right run's min → no merge needed.
                if (s.Compare(mid, mid + 1) <= 0)
                    continue;

                MergeInPlace(s, left, mid, right);
            }
        }
    }

    /// <summary>
    /// Merges two sorted subarrays [left..mid] and [mid+1..right] in-place using rotation.
    /// Uses galloping (exponential search + binary search) to efficiently find long consecutive blocks.
    /// </summary>
    private static void MergeInPlace<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int mid, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var start1 = left;
        var start2 = mid + 1;

        while (start1 <= mid && start2 <= right)
        {
            if (s.Compare(start1, start2) <= 0)
            {
                start1++;
            }
            else
            {
                // s[start1] > s[start2]: gallop to find how many right-partition elements
                // are all less than s[start1], then rotate the entire block at once.
                var start2End = GallopingSearchEnd(s, start1, start2, right);

                var blockSize = start2End - start2 + 1;
                var rotateDistance = start2 - start1;

                // Left-rotate [start1..start2End] by rotateDistance: [left_part | right_block] → [right_block | left_part]
                // right_block elements (all < s[start1]) are moved to the front; left_part shifts right.
                Rotate(s, start1, start2End, rotateDistance);

                start1 += blockSize;
                mid += blockSize;
                start2 = start2End + 1;
            }
        }
    }

    /// <summary>
    /// Finds the end position of consecutive right-partition elements that are all less than s[leftBoundary].
    /// Phase 1: exponential search (1, 2, 4, 8, …) to find a rough upper bound.
    /// Phase 2: binary search to pinpoint the exact boundary.
    /// </summary>
    /// <returns>The last index in [start..end] where s[i] &lt; s[leftBoundary]</returns>
    private static int GallopingSearchEnd<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int leftBoundary, int start, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var lastGood = start;
        var step = 1;

        while (start + step <= end && s.Compare(leftBoundary, start + step) > 0)
        {
            lastGood = start + step;
            step *= 2;
        }

        var low = lastGood;
        var high = Math.Min(start + step, end);

        while (low < high)
        {
            var mid = low + (high - low + 1) / 2;

            if (s.Compare(leftBoundary, mid) > 0)
                low = mid;
            else
                high = mid - 1;
        }

        return low;
    }

    /// <summary>
    /// Left-rotates A[left..right] by k positions: [left_k_elems | rest] → [rest | left_k_elems].
    /// Fast path k==1 (left rotate 1): move leftmost element to right end.
    /// Fast path k==n-1 (left rotate n-1 = right rotate 1): move rightmost element to left end.
    /// General case uses 3-reversal: Reverse[left..left+k-1], Reverse[left+k..right], Reverse[left..right].
    /// All paths are linear scans, enabling hardware prefetching without GCD or modulo overhead.
    /// </summary>
    /// <param name="k">The number of positions to rotate left</param>
    private static void Rotate<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right, int k)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (k == 0 || left >= right) return;

        var n = right - left + 1;
        k = k % n;
        if (k == 0) return;

        // Fast path: k==1 (left rotate 1) - move leftmost element to right end, shift rest left (sequential read/write, no swap)
        if (k == 1)
        {
            var tmp = s.Read(left);
            for (var i = left; i < right; i++)
                s.Write(i, s.Read(i + 1));
            s.Write(right, tmp);
            return;
        }

        // Fast path: k==n-1 (left rotate n-1 = right rotate 1) - move rightmost element to left end, shift rest right (sequential read/write, no swap)
        if (k == n - 1)
        {
            var tmp = s.Read(right);
            for (var i = right; i > left; i--)
                s.Write(i, s.Read(i - 1));
            s.Write(left, tmp);
            return;
        }

        // General case: left rotate by k via 3-reversal (linear scans, cache-friendly, no GCD overhead)
        // [A|B] → Reverse(A), Reverse(B), Reverse(AB) → [B|A]
        Reverse(s, left, left + k - 1);
        Reverse(s, left + k, right);
        Reverse(s, left, right);
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

/// <summary>
/// 最適化した再帰呼び出しなRotate Merge Sortです。
/// 配列を再帰的に半分に分割し、それぞれをソートした後、回転アルゴリズムを使用してインプレースでマージする分割統治アルゴリズムです。
/// 安定ソートであり、追加メモリを使用せずにO(n log² n)の性能を保証します。
/// 小さい配列（≤16要素）ではInsertionSortを使用、ローテートにGCD-cycle、連続ブロック検索にGallopingを用いる実用的な最適化を含みます。
/// <br/>
/// Optimized recursive Rotate Merge Sort.
/// Recursively divides the array in half, sorts each part, then merges sorted subarrays in-place using array rotation.
/// This divide-and-conquer algorithm is stable and guarantees O(n log² n) performance without requiring auxiliary space.
/// Includes practical optimizations: insertion sort for small subarrays (≤16 elements), GCD-cycle rotation, and galloping for finding consecutive blocks.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Rotate Merge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Hybrid Optimization:</strong> For subarrays with ≤16 elements, insertion sort is used instead of rotation-based merge.
/// This is a practical optimization similar to TimSort and IntroSort, reducing overhead for small sizes.</description></item>
/// <item><description><strong>Galloping Optimization:</strong> Uses exponential search (1, 2, 4, 8, ...) followed by binary search to efficiently find
/// long runs of consecutive elements from the right partition. This is similar to TimSort's galloping mode and reduces comparisons
/// when merging data with long consecutive blocks.</description></item>
/// <item><description><strong>Divide Step (Binary Partitioning):</strong> The array must be divided into two roughly equal halves at each recursion level.
/// The midpoint is calculated as mid = (left + right) / 2, ensuring balanced subdivision.
/// This guarantees a recursion depth of ⌈log₂(n)⌉.</description></item>
/// <item><description><strong>Base Case (Termination Condition):</strong> Recursion must terminate when a subarray has size ≤ 1.
/// An array of size 0 or 1 is trivially sorted and requires no further processing.</description></item>
/// <item><description><strong>Conquer Step (Recursive Sorting):</strong> Each half must be sorted independently via recursive calls.
/// The left subarray [left..mid] and right subarray [mid+1..right] are sorted before merging.</description></item>
/// <item><description><strong>In-Place Merge Step:</strong> Two sorted subarrays must be merged without using additional memory.
/// This is achieved using array rotation, which rearranges elements by shifting blocks of the array.</description></item>
/// <item><description><strong>Rotation Algorithm (Left-Rotate by k, 3-Reversal with fast paths):</strong> Left-rotates A[left..right] by k positions: [left_k_elems | rest] → [rest | left_k_elems].
/// Fast path k==1: move leftmost element to right end (sequential reads/writes, no swap).
/// Fast path k==n-1: move rightmost element to left end (left rotate n-1 = right rotate 1, sequential reads/writes, no swap).
/// General case uses 3-reversal: Reverse(A[left..left+k-1]), Reverse(A[left+k..right]), Reverse(A[left..right]).
/// All three phases are linear scans, enabling hardware prefetching and eliminating GCD/modulo overhead.</description></item>
/// <item><description><strong>Merge via Rotation:</strong> During merge, find the position where the first element of the right partition
/// should be inserted in the left partition (using binary search). Rotate elements to place it correctly, then recursively
/// merge the remaining elements. This maintains sorted order while being in-place.</description></item>
/// <item><description><strong>Stability Preservation:</strong> Binary search uses &lt;= comparison to find the insertion position,
/// ensuring equal elements from the left partition appear before equal elements from the right partition.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Merge + Insertion + Galloping)</description></item>
/// <item><description>Stable      : Yes (binary search with &lt;= comparison preserves relative order)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space, uses rotation instead of buffer)</description></item>
/// <item><description>Best case   : O(n) - Sorted data with insertion sort optimization for small partitions</description></item>
/// <item><description>Average case: O(n log² n) - Binary search (log n) + rotation (n) per merge level (log n levels)</description></item>
/// <item><description>Worst case  : O(n log² n) - Rotation adds O(n) factor to each merge operation</description></item>
/// <item><description>Comparisons : Best O(n), Average/Worst O(n log² n) - Galloping reduces comparisons for consecutive blocks</description></item>
/// <item><description>Writes      : Best O(n), Average/Worst O(n log² n) - k==1/k==n-1 fast paths use sequential writes; 3-reversal uses cache-friendly swaps</description></item>
/// <item><description>Swaps       : 0 for k==1/k==n-1 fast paths; O(n/2) per rotation in general case (3-reversal)</description></item>
/// <item><description>Space       : O(log n) - Only recursion stack space, no auxiliary buffer needed</description></item>
/// </list>
/// <para><strong>Advantages of Rotate Merge Sort:</strong></para>
/// <list type="bullet">
/// <item><description>True in-place sorting - O(1) auxiliary space (only recursion stack)</description></item>
/// <item><description>Stable - Preserves relative order of equal elements</description></item>
/// <item><description>Hybrid optimization - Insertion sort improves performance for small subarrays</description></item>
/// <item><description>Galloping search - Efficiently finds consecutive blocks (TimSort-style)</description></item>
/// <item><description>3-reversal rotation - cache-friendly linear scans, no GCD or modulo overhead</description></item>
/// <item><description>k==1 / k==n-1 fast paths - zero-swap sequential shift for the most common single-element merge case</description></item>
/// </list>
/// <para><strong>Disadvantages:</strong></para>
/// <list type="bullet">
/// <item><description>Slower than buffer-based merge sort - Additional log n factor from binary search and rotation overhead</description></item>
/// <item><description>More writes than standard merge sort - Rotation requires multiple element movements</description></item>
/// <item><description>Complexity - Multiple optimizations (insertion sort, galloping, 3-reversal fast paths) increase code complexity</description></item>
/// </list>
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>When memory is extremely constrained (embedded systems, real-time systems)</description></item>
/// <item><description>When stability is required but auxiliary memory is not available</description></item>
/// <item><description>Educational purposes - Understanding in-place merging techniques</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Merge_sort#Variants</para>
/// <para>Rotation-based in-place merge: Practical In-Place Merging (Geffert et al.)</para>
/// </remarks>
public static class RotateMergeSortRecursive
{
    // Threshold for using insertion sort instead of rotation-based merge
    // Small subarrays benefit from insertion sort's lower overhead
    private const int InsertionSortThreshold = 16;

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array (in-place operations only)

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
        SortCore(s, 0, span.Length - 1);
    }

    /// <summary>
    /// Core recursive merge sort implementation.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the span to sort</param>
    /// <param name="left">The inclusive start index of the range to sort</param>
    /// <param name="right">The inclusive end index of the range to sort</param>
    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (right <= left) return; // Base case: array of size 0 or 1 is sorted

        var length = right - left + 1;

        // Optimization: Use insertion sort for small subarrays
        // Rotation overhead is too high for small sizes, and insertion sort has better cache locality
        if (length <= InsertionSortThreshold)
        {
            // Reuse existing InsertionSort.SortCore
            // Note: SortCore uses exclusive end index [first, last), so we pass right + 1
            InsertionSort.SortCore(s, left, right + 1);
            return;
        }

        var mid = left + (right - left) / 2;

        // Conquer: Recursively sort left and right halves
        SortCore(s, left, mid);
        SortCore(s, mid + 1, right);

        // Optimization: Skip merge if already sorted (left[last] <= right[first])
        if (s.Compare(mid, mid + 1) <= 0)
        {
            return; // Already sorted, no merge needed
        }

        // Merge: Combine two sorted halves in-place using rotation
        MergeInPlace(s, left, mid, right);
    }

    /// <summary>
    /// Merges two sorted subarrays [left..mid] and [mid+1..right] in-place using rotation.
    /// When s[start1] &gt; s[start2], start1 is already the insertion point (no binary search needed).
    /// Optimization: Uses galloping (exponential search + binary search) to efficiently find
    /// long runs of consecutive elements from the right partition.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="left">The inclusive start index of the left subarray</param>
    /// <param name="mid">The inclusive end index of the left subarray</param>
    /// <param name="right">The inclusive end index of the right subarray</param>
    private static void MergeInPlace<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int mid, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var start1 = left;
        var start2 = mid + 1;

        // Main merge loop using rotation algorithm with galloping optimization
        while (start1 <= mid && start2 <= right)
        {
            // If element at start1 is in correct position
            if (s.Compare(start1, start2) <= 0)
            {
                start1++;
            }
            else
            {
                // s[start1] > s[start2] and [start1..mid] is sorted, so start1 is already the insertion point.
                // Galloping: find how many consecutive right-partition elements are all less than s[start1]
                var start2End = GallopingSearchEnd(s, start1, start2, right);

                var blockSize = start2End - start2 + 1;
                var rotateDistance = start2 - start1;

                // Left-rotate [start1..start2End] by rotateDistance: [left_part | right_block] → [right_block | left_part]
                // right_block elements (all < s[start1]) are moved to the front; left_part shifts right.
                Rotate(s, start1, start2End, rotateDistance);

                // Update pointers after moving the block
                start1 += blockSize;
                mid += blockSize;
                start2 = start2End + 1;
            }
        }
    }

    /// <summary>
    /// Finds the end position of consecutive elements from the right partition using galloping.
    /// Scans right partition for elements all less than s[leftBoundary], the left boundary element.
    /// Uses exponential search followed by binary search for efficiency.
    /// This is similar to TimSort's galloping mode.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="leftBoundary">The index of the left partition boundary element (start1) to compare against</param>
    /// <param name="start">The start position in the right partition</param>
    /// <param name="end">The end position in the right partition</param>
    /// <returns>The last index in the right partition where s[i] &lt; s[leftBoundary]</returns>
    private static int GallopingSearchEnd<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int leftBoundary, int start, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Phase 1: Exponential search (galloping) - find rough upper bound
        // Step size: 1, 2, 4, 8, 16, ... (exponentially increasing)
        var lastGood = start;
        var step = 1;

        while (start + step <= end && s.Compare(leftBoundary, start + step) > 0)
        {
            lastGood = start + step;
            step *= 2;  // Exponential growth
        }

        // Phase 2: Binary search for exact boundary in [lastGood..min(start+step, end)]
        var low = lastGood;
        var high = Math.Min(start + step, end);

        // Binary search to find the last element that is less than s[leftBoundary]
        while (low < high)
        {
            var mid = low + (high - low + 1) / 2;

            if (s.Compare(leftBoundary, mid) > 0)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// Rotates a subarray left by k positions.
    /// Fast paths for k==1 and k==n-1 shift a single element sequentially (no swaps, linear access).
    /// General case uses 3-reversal: Reverse[left..left+k-1], Reverse[left+k..right], Reverse[left..right].
    /// All paths are linear scans, enabling hardware prefetching without GCD or modulo overhead.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="left">The start index of the subarray to rotate</param>
    /// <param name="right">The end index of the subarray to rotate</param>
    /// <param name="k">The number of positions to rotate left</param>
    private static void Rotate<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right, int k)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (k == 0 || left >= right) return;

        var n = right - left + 1;
        k = k % n;
        if (k == 0) return;

        // Fast path: k==1 (left rotate 1) - move leftmost element to right end, shift rest left (sequential read/write, no swap)
        if (k == 1)
        {
            var tmp = s.Read(left);
            for (var i = left; i < right; i++)
                s.Write(i, s.Read(i + 1));
            s.Write(right, tmp);
            return;
        }

        // Fast path: k==n-1 (left rotate n-1 = right rotate 1) - move rightmost element to left end, shift rest right (sequential read/write, no swap)
        if (k == n - 1)
        {
            var tmp = s.Read(right);
            for (var i = right; i > left; i--)
                s.Write(i, s.Read(i - 1));
            s.Write(left, tmp);
            return;
        }

        // General case: left rotate by k via 3-reversal (linear scans, cache-friendly, no GCD overhead)
        // [A|B] → Reverse(A), Reverse(B), Reverse(AB) → [B|A]
        Reverse(s, left, left + k - 1);
        Reverse(s, left + k, right);
        Reverse(s, left, right);
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


/// <summary>
/// 最適化していないRotate Merge Sortです。
/// 配列を再帰的に半分に分割し、それぞれをソートした後、回転アルゴリズムを使用してインプレースでマージする分割統治アルゴリズムです。
/// 安定ソートであり、追加メモリを使用せずにO(n log n)の性能を保証します。
/// 回転をするため、要素の移動が多くなるため、標準のマージソートよりも遅くなります。
/// <br/>
/// Non-Optimized Rotate Merge Sort.
/// Recursively divides the array in half, sorts each part, then merges sorted subarrays in-place using array rotation.
/// This divide-and-conquer algorithm is stable and guarantees O(n log n) performance without requiring auxiliary space.
/// However, due to the rotations, it involves more element movements and is slower than standard merge sort.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Rotate Merge Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Hybrid Optimization:</strong> For subarrays with ≤16 elements, insertion sort is used instead of rotation-based merge.
/// This is a practical optimization similar to TimSort and IntroSort, reducing overhead for small sizes.</description></item>
/// <item><description><strong>Galloping Optimization:</strong> Uses exponential search (1, 2, 4, 8, ...) followed by binary search to efficiently find
/// long runs of consecutive elements from the right partition. This is similar to TimSort's galloping mode and reduces comparisons
/// when merging data with long consecutive blocks.</description></item>
/// <item><description><strong>Divide Step (Binary Partitioning):</strong> The array must be divided into two roughly equal halves at each recursion level.
/// The midpoint is calculated as mid = (left + right) / 2, ensuring balanced subdivision.
/// This guarantees a recursion depth of ⌈log₂(n)⌉.</description></item>
/// <item><description><strong>Base Case (Termination Condition):</strong> Recursion must terminate when a subarray has size ≤ 1.
/// An array of size 0 or 1 is trivially sorted and requires no further processing.</description></item>
/// <item><description><strong>Conquer Step (Recursive Sorting):</strong> Each half must be sorted independently via recursive calls.
/// The left subarray [left..mid] and right subarray [mid+1..right] are sorted before merging.</description></item>
/// <item><description><strong>In-Place Merge Step:</strong> Two sorted subarrays must be merged without using additional memory.
/// This is achieved using array rotation, which rearranges elements by shifting blocks of the array.</description></item>
/// <item><description><strong>Rotation Algorithm (Left-Rotate by k, GCD-Cycle / Juggling):</strong> Left-rotates A[left..right] by k positions: [left_k_elems | rest] → [rest | left_k_elems] using GCD-based cycle detection.
/// To left-rotate array A of length n by k positions: Find GCD(n, k) independent cycles, and for each cycle, move elements using assignments only.
/// This achieves O(n) time rotation with O(1) space using only writes (no swaps needed).
/// The algorithm divides rotation into GCD(n,k) independent cycles, rotating elements within each cycle.</description></item>
/// <item><description><strong>Merge via Rotation:</strong> During merge, find the position where the first element of the right partition
/// should be inserted in the left partition (using binary search). Rotate elements to place it correctly, then recursively
/// merge the remaining elements. This maintains sorted order while being in-place.</description></item>
/// <item><description><strong>Stability Preservation:</strong> Binary search uses &lt;= comparison to find the insertion position,
/// ensuring equal elements from the left partition appear before equal elements from the right partition.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Hybrid (Merge + Insertion + Galloping)</description></item>
/// <item><description>Stable      : Yes (binary search with &lt;= comparison preserves relative order)</description></item>
/// <item><description>In-place    : Yes (O(1) auxiliary space, uses rotation instead of buffer)</description></item>
/// <item><description>Best case   : O(n) - Sorted data with insertion sort optimization for small partitions</description></item>
/// <item><description>Average case: O(n log² n) - Binary search (log n) + rotation (n) per merge level (log n levels)</description></item>
/// <item><description>Worst case  : O(n log² n) - Rotation adds O(n) factor to each merge operation</description></item>
/// <item><description>Comparisons : Best O(n), Average/Worst O(n log² n) - Galloping reduces comparisons for consecutive blocks</description></item>
/// <item><description>Writes      : Best O(n), Average/Worst O(n² log n) - GCD-cycle rotation uses assignments only (no swaps)</description></item>
/// <item><description>Swaps       : 0 - GCD-cycle rotation uses only write operations, no swaps needed</description></item>
/// <item><description>Space       : O(log n) - Only recursion stack space, no auxiliary buffer needed</description></item>
/// </list>
/// <para><strong>Advantages of Rotate Merge Sort:</strong></para>
/// <list type="bullet">
/// <item><description>True in-place sorting - O(1) auxiliary space (only recursion stack)</description></item>
/// <item><description>Stable - Preserves relative order of equal elements</description></item>
/// <item><description>Hybrid optimization - Insertion sort improves performance for small subarrays</description></item>
/// <item><description>Galloping search - Efficiently finds consecutive blocks (TimSort-style)</description></item>
/// <item><description>GCD-cycle rotation - Efficient assignment-based rotation without swaps</description></item>
/// </list>
/// <para><strong>Disadvantages:</strong></para>
/// <list type="bullet">
/// <item><description>Slower than buffer-based merge sort - Additional log n factor from binary search and rotation overhead</description></item>
/// <item><description>More writes than standard merge sort - Rotation requires multiple element movements</description></item>
/// <item><description>Complexity - Multiple optimizations (insertion sort, galloping, GCD-cycle) increase code complexity</description></item>
/// </list>
/// <para><strong>Use Cases:</strong></para>
/// <list type="bullet">
/// <item><description>When memory is extremely constrained (embedded systems, real-time systems)</description></item>
/// <item><description>When stability is required but auxiliary memory is not available</description></item>
/// <item><description>Educational purposes - Understanding in-place merging techniques</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Merge_sort#Variants</para>
/// <para>Rotation-based in-place merge: Practical In-Place Merging (Geffert et al.)</para>
/// </remarks>
public static class RotateMergeSortNonOptimized
{
    // Threshold for using insertion sort instead of rotation-based merge
    // Small subarrays benefit from insertion sort's lower overhead
    private const int InsertionSortThreshold = 16;

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array (in-place operations only)

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
        SortCore(s, 0, span.Length - 1);
    }

    /// <summary>
    /// Core recursive merge sort implementation.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the span to sort</param>
    /// <param name="left">The inclusive start index of the range to sort</param>
    /// <param name="right">The inclusive end index of the range to sort</param>
    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (right <= left) return; // Base case: array of size 0 or 1 is sorted

        var length = right - left + 1;

        // Optimization: Use insertion sort for small subarrays
        // Rotation overhead is too high for small sizes, and insertion sort has better cache locality
        if (length <= InsertionSortThreshold)
        {
            // Reuse existing InsertionSort.SortCore
            // Note: SortCore uses exclusive end index [first, last), so we pass right + 1
            InsertionSort.SortCore(s, left, right + 1);
            return;
        }

        var mid = left + (right - left) / 2;

        // Conquer: Recursively sort left and right halves
        SortCore(s, left, mid);
        SortCore(s, mid + 1, right);

        // Optimization: Skip merge if already sorted (left[last] <= right[first])
        if (s.Compare(mid, mid + 1) <= 0)
        {
            return; // Already sorted, no merge needed
        }

        // Merge: Combine two sorted halves in-place using rotation
        MergeInPlace(s, left, mid, right);
    }

    /// <summary>
    /// Merges two sorted subarrays [left..mid] and [mid+1..right] in-place using rotation.
    /// When s[start1] &gt; s[start2], start1 is already the insertion point (no binary search needed).
    /// Optimization: Uses galloping (exponential search + binary search) to efficiently find
    /// long runs of consecutive elements from the right partition.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="left">The inclusive start index of the left subarray</param>
    /// <param name="mid">The inclusive end index of the left subarray</param>
    /// <param name="right">The inclusive end index of the right subarray</param>
    private static void MergeInPlace<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int mid, int right)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var start1 = left;
        var start2 = mid + 1;

        // Main merge loop using rotation algorithm with galloping optimization
        while (start1 <= mid && start2 <= right)
        {
            // If element at start1 is in correct position
            if (s.Compare(start1, start2) <= 0)
            {
                start1++;
            }
            else
            {
                // s[start1] > s[start2] and [start1..mid] is sorted, so start1 is already the insertion point.
                // Galloping: find how many consecutive right-partition elements are all less than s[start1]
                var start2End = GallopingSearchEnd(s, start1, start2, right);

                var blockSize = start2End - start2 + 1;
                var rotateDistance = start2 - start1;

                // Left-rotate [start1..start2End] by rotateDistance: [left_part | right_block] → [right_block | left_part]
                // right_block elements (all < s[start1]) are moved to the front; left_part shifts right.
                Rotate(s, start1, start2End, rotateDistance);

                // Update pointers after moving the block
                start1 += blockSize;
                mid += blockSize;
                start2 = start2End + 1;
            }
        }
    }

    /// <summary>
    /// Finds the end position of consecutive elements from the right partition using galloping.
    /// Scans right partition for elements all less than s[leftBoundary], the left boundary element.
    /// Uses exponential search followed by binary search for efficiency.
    /// This is similar to TimSort's galloping mode.
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="leftBoundary">The index of the left partition boundary element (start1) to compare against</param>
    /// <param name="start">The start position in the right partition</param>
    /// <param name="end">The end position in the right partition</param>
    /// <returns>The last index in the right partition where s[i] &lt; s[leftBoundary]</returns>
    private static int GallopingSearchEnd<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int leftBoundary, int start, int end)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Phase 1: Exponential search (galloping) - find rough upper bound
        // Step size: 1, 2, 4, 8, 16, ... (exponentially increasing)
        var lastGood = start;
        var step = 1;

        while (start + step <= end && s.Compare(leftBoundary, start + step) > 0)
        {
            lastGood = start + step;
            step *= 2;  // Exponential growth
        }

        // Phase 2: Binary search for exact boundary in [lastGood..min(start+step, end)]
        var low = lastGood;
        var high = Math.Min(start + step, end);

        // Binary search to find the last element that is less than s[leftBoundary]
        while (low < high)
        {
            var mid = low + (high - low + 1) / 2;

            if (s.Compare(leftBoundary, mid) > 0)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// Rotates a subarray by k positions to the left using the GCD-cycle (Juggling) algorithm.
    /// This algorithm divides the rotation into GCD(n, k) independent cycles and moves elements
    /// within each cycle using assignments only (no swaps needed).
    /// </summary>
    /// <param name="s">The SortSpan wrapping the array</param>
    /// <param name="left">The start index of the subarray to rotate</param>
    /// <param name="right">The end index of the subarray to rotate</param>
    /// <param name="k">The number of positions to rotate left</param>
    private static void Rotate<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int left, int right, int k)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (k == 0 || left >= right) return;

        var n = right - left + 1;
        k = k % n;
        if (k == 0) return;

        // Left rotation via GCD-cycle (Juggling algorithm): [left_k_elems | rest] → [rest | left_k_elems]
        // Divide rotation into gcd(n, k) independent cycles
        var cycles = GCD(n, k);

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            // Save the first element of this cycle
            var startIdx = left + cycle;
            var temp = s.Read(startIdx);
            var currentIdx = startIdx;

            // Move elements in this cycle
            while (true)
            {
                var nextIdx = currentIdx + k;
                if (nextIdx > right)
                    nextIdx = left + (nextIdx - right - 1);

                // If we've completed the cycle, break
                if (nextIdx == startIdx)
                    break;

                // Move element from nextIdx to currentIdx
                s.Write(currentIdx, s.Read(nextIdx));
                currentIdx = nextIdx;
            }

            // Place the saved element in its final position
            s.Write(currentIdx, temp);
        }
    }

    /// <summary>
    /// Calculates the greatest common divisor (GCD) of two numbers using Euclid's algorithm.
    /// </summary>
    /// <param name="a">First number</param>
    /// <param name="b">Second number</param>
    /// <returns>GCD of a and b</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}

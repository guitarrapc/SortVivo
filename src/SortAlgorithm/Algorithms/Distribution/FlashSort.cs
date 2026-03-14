using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// FlashSort distribution sorting algorithm.
/// <br/>
/// Classifies each element into one of m buckets via linear interpolation, permutes all elements
/// into their correct bucket region in O(n) using an in-place cycle technique, then applies
/// insertion sort locally within each bucket.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct FlashSort:</strong></para>
/// <list type="number">
/// <item><description><strong>Linear Classification:</strong> Each element is mapped to class k by
/// k = ⌊(m−1) × (key − minKey) / range⌋ using exact 128-bit arithmetic to avoid overflow.
/// The min element always maps to class 0; the max element always maps to class m−1.</description></item>
/// <item><description><strong>Max-to-Front Swap:</strong> Before the permutation phase the maximum
/// element is moved to index 0. This seeds class m−1 at the front and guarantees the cycle
/// starts with a correctly classified element, avoiding an unbounded outer-loop scan at the start.</description></item>
/// <item><description><strong>Permutation Phase (Cycle Technique):</strong> Each iteration picks up
/// the element at position j (flash), places it at the last unfilled slot of its class
/// (count[kClass]−1), picks up the displaced element, and repeats until the cycle closes
/// (j == count[kClass] after decrement). count[k] is decremented on each placement so it
/// always points to the next empty slot for class k.</description></item>
/// <item><description><strong>Advance Loop:</strong> After a cycle closes, j may point to a position
/// already filled. The outer loop advances j (and recomputes kClass) until it finds a position
/// that still belongs to an unfilled part of its class (j &lt; count[kClass]). A bounds guard
/// (j &lt; n) prevents out-of-range access if all remaining elements are already placed.</description></item>
/// <item><description><strong>Local Insertion Sort:</strong> After permutation every element lies
/// within its correct class region [boundary[k−1], boundary[k]). Insertion sort is applied
/// independently to each region using absolute span indices, so no cross-region movement
/// occurs and statistics are tracked accurately.</description></item>
/// <item><description><strong>Duplicate Handling:</strong> All duplicates map to the same class and
/// are permuted within that class's region. The final insertion sort orders them correctly.
/// The early-exit (minKey == maxKey) handles the all-equal case without entering the
/// permutation loop.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution (Non-comparison based)</description></item>
/// <item><description>Stable      : No (permutation phase does not preserve relative order)</description></item>
/// <item><description>In-place    : Nearly (O(m) auxiliary space for bucket counters)</description></item>
/// <item><description>Best case   : Θ(n) - Uniform distribution, permutation completes in O(n) moves</description></item>
/// <item><description>Average case: Θ(n + m) - Linear with n for uniform random data (m ≈ 0.43 n)</description></item>
/// <item><description>Worst case  : Θ(n²) - Highly skewed distribution concentrates elements in one bucket,
/// forcing the local insertion sort to handle O(n) elements</description></item>
/// <item><description>Comparisons : O(n log(n/m)) average (only in per-bucket insertion sort)</description></item>
/// <item><description>Swaps       : 0 explicit swaps beyond the initial max-to-front swap; permutation
/// uses read/write pairs rather than swap operations</description></item>
/// </list>
/// <para><strong>Supported Types:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Supported:</strong> byte, sbyte, short, ushort, int, uint, long, ulong, nint, nuint (up to 64-bit)</description></item>
/// <item><description><strong>Not Supported:</strong> Int128, UInt128 (&gt;64-bit types)</description></item>
/// </list>
/// <para><strong>Why 128-bit Types Are Not Supported:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Key Storage Limitation:</strong> This implementation uses <c>ulong</c> (64-bit) to store
/// unsigned keys. Supporting 128-bit would require <c>UInt128</c> keys for both storage and arithmetic.</description></item>
/// <item><description><strong>Practical Rarity:</strong> Sorting 128-bit integers is uncommon.
/// Comparison-based sorts remain practical for such cases.</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Neubert, K.-D. (1998). The Flashsort1 Algorithm. Dr. Dobb's Journal.</para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Flashsort</para>
/// </remarks>
public static class FlashSort
{
    private const int InsertionSortThreshold = 16;

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;           // Main input array

    /// <summary>
    /// Sorts the elements in the specified span using FlashSort.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort. Must be a supported binary integer type up to 64 bits.</typeparam>
    /// <param name="span">The span of elements to sort.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is a 128-bit type (<see cref="Int128"/> or <see cref="UInt128"/>).
    /// </exception>
    public static void Sort<T>(Span<T> span) where T : IBinaryInteger<T>
        => Sort(span, NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using FlashSort with context tracking.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort. Must be a supported binary integer type up to 64 bits.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort.</param>
    /// <param name="context">The sort context for tracking operations.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is a 128-bit type (<see cref="Int128"/> or <see cref="UInt128"/>).
    /// This implementation only supports integer types up to 64-bit due to key storage constraints.
    /// </exception>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IBinaryInteger<T>
        where TContext : ISortContext
    { 
        if (span.Length <= 1) return;

        var n = span.Length;
        var bitSize = GetBitSize<T>();

        // Small arrays go directly to insertion sort
        if (n <= InsertionSortThreshold)
        {
            InsertionSort.Sort(span, new ComparableComparer<T>(), context);
            return;
        }

        var s = new SortSpan<T, NullComparer<T>, TContext>(span, context, default, BUFFER_MAIN);

        // Pass 1: find min/max unsigned keys and the index of the maximum element
        context.OnPhase(SortPhase.DistributionCount);

        var minKey = ulong.MaxValue;
        var maxKey = ulong.MinValue;
        var maxIdx = 0;

        for (var i = 0; i < n; i++)
        {
            var key = GetUnsignedKey(s.Read(i), bitSize);
            if (key < minKey) minKey = key;
            if (key > maxKey) { maxKey = key; maxIdx = i; }
        }

        // All elements equal: already sorted
        if (minKey == maxKey) return;

        var range = maxKey - minKey;

        // m = number of buckets; empirically 0.43·n often works well on roughly uniform random data
        var m = Math.Max(2, (int)(0.43 * n));

        var countArr = ArrayPool<int>.Shared.Rent(m);
        var boundaryArr = ArrayPool<int>.Shared.Rent(m);

        try
        {
            var count = countArr.AsSpan(0, m);
            var boundary = boundaryArr.AsSpan(0, m);
            count.Clear();

            // Pass 2: count elements per class
            for (var i = 0; i < n; i++)
            {
                var key = GetUnsignedKey(s.Read(i), bitSize);
                count[ClassOf(key, minKey, range, m)]++;
            }

            // Pass 3: prefix sum → count[k] becomes exclusive upper bound of class k
            context.OnPhase(SortPhase.DistributionAccumulate);
            for (var k = 1; k < m; k++)
                count[k] += count[k - 1];

            // Save boundaries before permutation modifies count[]
            count.CopyTo(boundary);

            context.OnPhase(SortPhase.DistributionWrite);

            // Move the maximum element to index 0 so the permutation cycle starts cleanly
            // at class m-1 whose region ends at boundary[m-1]-1 = n-1
            s.Swap(maxIdx, 0);

            // Pass 4: permutation phase — place every element in its correct class region

            var move = 0;
            var j = 0;
            var kClass = m - 1;

            while (move < n - 1)
            {
                // Advance j to the next position whose element still belongs to an unfilled slot
                // j >= count[kClass] means position j is already in the "filled" suffix of its class
                while (j < n)
                {
                    kClass = ClassOf(GetUnsignedKey(s.Read(j), bitSize), minKey, range, m);
                    if (j < count[kClass]) break;
                    j++;
                }

                // All remaining elements are already correctly placed
                if (j >= n) break;

                // Pick up the element at j and cycle it into its correct class
                var flash = s.Read(j);

                // Cycle terminates when j equals the current count[kClass]:
                // that means the next empty slot for flash's class IS j itself → cycle closed
                while (j != count[kClass])
                {
                    kClass = ClassOf(GetUnsignedKey(flash, bitSize), minKey, range, m);
                    var dest = count[kClass] - 1;
                    var temp = s.Read(dest);
                    s.Write(dest, flash);
                    flash = temp;
                    count[kClass]--;
                    move++;
                }
            }

            // Pass 5: insertion sort within each class region [boundary[k-1], boundary[k])
            // Uses absolute indices and a real comparer so statistics and visualization are accurate
            var insertionSpan = new SortSpan<T, ComparableComparer<T>, TContext>(span, context, new ComparableComparer<T>(), BUFFER_MAIN);
            var lo = 0;
            for (var k = 0; k < m; k++)
            {
                var hi = boundary[k];
                if (hi - lo > 1)
                    InsertionSort.SortCore(insertionSpan, lo, hi);
                lo = hi;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(countArr, clearArray: false);
            ArrayPool<int>.Shared.Return(boundaryArr, clearArray: false);
        }
    }

    /// <summary>
    /// Maps an unsigned key to a class index in [0, m-1] using linear interpolation.
    /// Uses UInt128 arithmetic to guarantee exact results with no overflow for any 64-bit key range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ClassOf(ulong key, ulong minKey, ulong range, int m)
        => (int)((UInt128)(m - 1) * (key - minKey) / range);

    /// <summary>
    /// Get the bit size of the type T.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown for 128-bit types or unsupported types.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBitSize<T>() where T : IBinaryInteger<T>
    {
        if (typeof(T) == typeof(sbyte) || typeof(T) == typeof(byte))
            return 8;
        else if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            return 16;
        else if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            return 32;
        else if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
            return 64;
        else if (typeof(T) == typeof(nint) || typeof(T) == typeof(nuint))
            return IntPtr.Size * 8; // 32-bit or 64-bit depending on platform
        else if (typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128))
            throw new NotSupportedException($"Type {typeof(T).Name} with 128-bit size is not supported. Maximum supported bit size is 64.");
        else
            throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
    }

    /// <summary>
    /// Convert a signed or unsigned value to an unsigned key for classification.
    /// For signed types, flips the sign bit so that the natural unsigned ordering matches
    /// the signed ordering (negatives sort before positives with no overflow risk).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetUnsignedKey<T>(T value, int bitSize) where T : IBinaryInteger<T>
    {
        if (bitSize <= 8)
        {
            var byteValue = byte.CreateTruncating(value);
            if (typeof(T) == typeof(sbyte))
                return (byte)(byteValue ^ 0x80);
            return byteValue;
        }
        else if (bitSize <= 16)
        {
            var ushortValue = ushort.CreateTruncating(value);
            if (typeof(T) == typeof(short))
                return (ushort)(ushortValue ^ 0x8000);
            return ushortValue;
        }
        else if (bitSize <= 32)
        {
            var uintValue = uint.CreateTruncating(value);
            if (typeof(T) == typeof(int) || (typeof(T) == typeof(nint) && IntPtr.Size == 4))
                return uintValue ^ 0x8000_0000;
            return uintValue;
        }
        else
        {
            var ulongValue = ulong.CreateTruncating(value);
            if (typeof(T) == typeof(long) || (typeof(T) == typeof(nint) && IntPtr.Size == 8))
                return ulongValue ^ 0x8000_0000_0000_0000;
            return ulongValue;
        }
    }
}

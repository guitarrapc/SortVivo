using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 整数キーを使用したフラッシュソートのジェネリック版。値が均等に分布している場合に最適に動作します。
/// 値域を m 個のクラスに分割し、各要素をクラスに振り分けた後、置換サイクルでインプレースに並べ替えます。
/// 各クラス内を挿入ソートで仕上げることでソートが完了します。
/// <br/>
/// Flash sort (generic, integer key), a distribution-based sort that performs optimally when values are uniformly distributed.
/// Divides the value range into m classes, distributes elements into classes, then rearranges them in-place using permutation cycles.
/// A final insertion sort pass within each class completes the sort.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Flash Sort (Generic, Range-based):</strong></para>
/// <list type="number">
/// <item><description><strong>Key Extraction:</strong> Each element must have a deterministic unsigned 64-bit key derived from its binary representation.
/// Signed integers are converted to unsigned by bit-casting; the key is stable.</description></item>
/// <item><description><strong>Range Partitioning:</strong> The key range [minKey, maxKey] is divided into m equal classes.
/// Class index k = ⌊(key - minKey) × m / (range + 1)⌋.</description></item>
/// <item><description><strong>Class Count Heuristic:</strong> This implementation uses m = max(2, ⌊0.43 × n⌋) classes.
/// The 0.43n heuristic is empirically optimal for roughly uniform random data.</description></item>
/// <item><description><strong>Permutation Phase:</strong> Elements are placed into their correct class region using in-place permutation cycles.
/// The maximum element is first moved to index 0 to anchor the cycle correctly.</description></item>
/// <item><description><strong>Per-Class Sorting:</strong> Each class region is sorted using Insertion Sort (stable, O(m²) for m elements).
/// This ensures stability within each class.</description></item>
/// <item><description><strong>Uniform Distribution Assumption:</strong> Optimal performance (O(n)) requires uniform key distribution.
/// Worst case O(n²) occurs when all elements fall into a single class.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution</description></item>
/// <item><description>Stable      : No (permutation phase does not preserve relative order)</description></item>
/// <item><description>In-place    : Yes (O(m) auxiliary space for class boundaries only)</description></item>
/// <item><description>Best case   : Ω(n) - Uniform distribution, one pass over the array</description></item>
/// <item><description>Average case: Θ(n) - Assumes uniform distribution, permutation phase is O(n)</description></item>
/// <item><description>Worst case  : O(n²) - All elements in one class, degenerates to Insertion Sort</description></item>
/// <item><description>Comparisons : O(n log(n/m)) on average - Each class sorted independently by Insertion Sort</description></item>
/// <item><description>Memory      : O(m) - Two integer arrays of size m for counts and boundaries</description></item>
/// <item><description>Note        : クラス数は 0.43n (最小 2) に自動調整されます。キーの分布が偏るとパフォーマンスが低下します。128ビット整数型には対応していません。</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
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

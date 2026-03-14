using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 値の分布状況を数え上げることを利用してインデックスを導きソートします。
/// 各要素からキーを抽出し、その出現回数をカウントして累積和を計算し、正しい位置に配置する安定なソートアルゴリズムです。
/// キーの範囲が狭い場合に非常に高速ですが、範囲が広いとメモリを大量に消費します。
/// <br/>
/// Sorts elements by counting the distribution of extracted keys.
/// A stable sorting algorithm that extracts keys, counts occurrences, and uses cumulative sums to place elements.
/// Very fast when the key range is narrow, but consumes significant memory for wide ranges.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Counting Sort (Generic, Key-based):</strong></para>
/// <list type="number">
/// <item><description><strong>Key Extraction:</strong> Each element must have a deterministic integer key obtained via the key selector function.
/// The key must be stable (same element always produces the same key).</description></item>
/// <item><description><strong>Range Determination:</strong> The algorithm finds min and max keys to determine the range [min, max].
/// A count array of size (max - min + 1) is allocated to track occurrences.</description></item>
/// <item><description><strong>Index Normalization:</strong> Keys are normalized by subtracting min (<c>key - min</c>), mapping keys to array indices [0, range-1].
/// This is safe even when min == int.MinValue, because the validated range guarantees the difference fits in an int.</description></item>
/// <item><description><strong>Counting Phase:</strong> For each element, its key is extracted and <c>countArray[key - min]</c> is incremented.
/// This records how many times each key appears.</description></item>
/// <item><description><strong>Cumulative Sum:</strong> The count array is transformed into cumulative counts.
/// countArray[i] becomes the number of elements with keys ≤ i, indicating the final position.</description></item>
/// <item><description><strong>Placement Phase:</strong> Elements are placed in reverse order (for stability).
/// For each element with key k, it is placed at position <c>countArray[k - min] - 1</c>, then the count is decremented.</description></item>
/// <item><description><strong>Stability:</strong> Processing elements in reverse order ensures that elements with equal keys maintain their original relative order.</description></item>
/// <item><description><strong>Range Limitation:</strong> The key range must be reasonable (≤ {MaxCountArraySize}).
/// Excessive ranges cause memory allocation failures.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution</description></item>
/// <item><description>Stable      : Yes (reverse-order placement preserves relative order)</description></item>
/// <item><description>In-place    : No (O(n + k) where k = range of keys)</description></item>
/// <item><description>Comparisons : 0 (No comparison operations between keys)</description></item>
/// <item><description>Time        : O(n + k) where k is the range of keys</description></item>
/// <item><description>Memory      : O(n + k)</description></item>
/// <item><description>Note        : A large key range leads to excessive memory usage. The maximum range is {MaxCountArraySize}.</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Counting_sort</para>
/// </remarks>
public static class CountingSort
{
    private const int MaxCountArraySize = 10_000_000; // Maximum allowed count array size
    private const int StackAllocThreshold = 1024; // Use stackalloc for count arrays smaller than this

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TEMP = 1;       // Temporary buffer for sorted elements

    /// <summary>
    /// Sorts the elements in the specified span in ascending order using the key selector.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span of elements to sort in place.</param>
    public static void Sort<T>(Span<T> span, Func<T, int> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        SortCore(span, new FuncKeySelector<T>(keySelector), NullContext.Default);
    }

    /// <summary>
    /// Sorts the elements in the specified span using the key selector and sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, Func<T, int> keySelector, TContext context)
        where TContext : ISortContext
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        SortCore(span, new FuncKeySelector<T>(keySelector), context);
    }

    private static void SortCore<T, TKeySelector, TContext>(Span<T> span, TKeySelector keySelector, TContext context)
        where TKeySelector : struct, IKeySelector<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, NullComparer<T>, TContext>(span, context, default, BUFFER_MAIN);

        // Rent arrays from ArrayPool for temporary storage
        var keysArray = ArrayPool<int>.Shared.Rent(span.Length);
        var tempArray = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            // Create SortSpan for temp buffer to track operations
            var tempSpan = new SortSpan<T, NullComparer<T>, TContext>(tempArray.AsSpan(0, span.Length), context, default, BUFFER_TEMP);
            var keys = keysArray.AsSpan(0, span.Length);

            // Find min/max and cache keys in single pass
            var min = int.MaxValue;
            var max = int.MinValue;

            for (var i = 0; i < span.Length; i++)
            {
                var key = keySelector.GetKey(s.Read(i));
                keys[i] = key;
                if (key < min) min = key;
                if (key > max) max = key;
            }

            // If all keys are the same, no need to sort
            if (min == max) return;

            // Check for overflow and validate range
            long range = (long)max - (long)min + 1;
            if (range > int.MaxValue)
                throw new ArgumentException($"Key range is too large for CountingSort: {range}. Maximum supported range is {MaxCountArraySize}.");
            if (range > MaxCountArraySize)
                throw new ArgumentException($"Key range ({range}) exceeds maximum count array size ({MaxCountArraySize}). Consider using another comparison-based sort.");

            var size = (int)range;

            // Use stackalloc for small count arrays, ArrayPool for larger ones
            int[]? rentedCountArray = null;
            Span<int> countArray = size <= StackAllocThreshold
                ? stackalloc int[size]
                : (rentedCountArray = ArrayPool<int>.Shared.Rent(size)).AsSpan(0, size);
            countArray.Clear();
            try
            {
                CountSort(s, keys, tempSpan, countArray, min);
            }
            finally
            {
                if (rentedCountArray is not null)
                {
                    ArrayPool<int>.Shared.Return(rentedCountArray);
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(keysArray);
            ArrayPool<T>.Shared.Return(tempArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>
    /// Core counting sort implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, Span<int> keys, SortSpan<T, TComparer, TContext> tempSpan, Span<int> countArray, int min)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Count occurrences of each key
        s.Context.OnPhase(SortPhase.DistributionCount);
        for (var i = 0; i < s.Length; i++)
        {
            countArray[keys[i] - min]++;
        }

        // Calculate cumulative counts (for stable sort)
        s.Context.OnPhase(SortPhase.DistributionAccumulate);
        for (var i = 1; i < countArray.Length; i++)
        {
            countArray[i] += countArray[i - 1];
        }

        // Build result array in reverse order to maintain stability
        s.Context.OnPhase(SortPhase.DistributionWrite);
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var key = keys[i];
            var index = key - min;
            var pos = countArray[index] - 1;
            tempSpan.Write(pos, s.Read(i));
            countArray[index]--;
        }

        // Write sorted data back to original span using CopyTo for efficiency
        tempSpan.CopyTo(0, s, 0, s.Length);
    }
}

/// <summary>
/// 整数値を直接カウンティングソートでソートします。
/// 各値の出現回数をカウントし、累積和を計算して正しい位置に配置する安定なソートアルゴリズムです。
/// 値の範囲が狭い場合に非常に高速ですが、範囲が広いとメモリを大量に消費します。
/// <br/>
/// Directly sorts integer values using counting sort.
/// A stable sorting algorithm that counts occurrences and uses cumulative sums to place elements.
/// Very fast when the value range is narrow, but consumes significant memory for wide ranges.
/// </summary>
/// <remarks>
/// <para><strong>Supported Types:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Supported:</strong> byte, sbyte, short, ushort, int, uint, long, ulong, nint, nuint (up to 64-bit)</description></item>
/// <item><description><strong>Not Supported:</strong> Int128, UInt128, BigInteger (>64-bit types)</description></item>
/// </list>
/// <para><strong>Why Int128/UInt128 are not supported:</strong></para>
/// <para>The value range for 128-bit types can reach 2^128, making the count array impractically large.
/// If you need to sort Int128/UInt128, consider using a comparison-based sort.</para>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution</description></item>
/// <item><description>Stable      : Yes</description></item>
/// <item><description>In-place    : No (O(n + k) where k = range of values)</description></item>
/// <item><description>Comparisons : 0 (min/max scan uses direct numeric operators, not tracked as sort comparisons)</description></item>
/// <item><description>Swaps       : 0</description></item>
/// <item><description>Time        : O(n + k) where k is the range of values</description></item>
/// <item><description>Memory      : O(n + k)</description></item>
/// <item><description>Note        : 値の範囲が大きいとメモリ使用量が膨大になります。最大範囲は{MaxCountArraySize}、かつ range/n ≤ {MaxRangeFactor} の制約があります。</description></item>
/// </list>
/// </remarks>
public static class CountingSortInteger
{
    private const int MaxCountArraySize = 10_000_000; // Maximum allowed count array size
    private const int MaxRangeFactor = 32;            // Maximum allowed range/n ratio; range > MaxRangeFactor*n means O(range) dominates O(n)
    private const int StackAllocThreshold = 1024;     // Use stackalloc for count arrays smaller than this

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TEMP = 1;       // Temporary buffer for sorted elements

    /// <summary>
    /// Sorts the elements in the specified span in ascending order.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T"> The type of elements to sort. Must be a binary integer type with defined min/max values.</typeparam>
    /// <param name="span"> The span of elements to sort.</param>
    public static void Sort<T>(Span<T> span) where T : IBinaryInteger<T>, IMinMaxValue<T>
        => Sort(span, NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using the specified context.
    /// </summary>
    /// <typeparam name="T"> The type of elements to sort. Must be a binary integer type with defined min/max values.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span"> The span of elements to sort.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        EnsureSupportedType<T>();

        var comparer = new NullComparer<T>();
        var s = new SortSpan<T, NullComparer<T>, TContext>(span, context, comparer, BUFFER_MAIN);

        var tempArray = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            var tempSpan = new SortSpan<T, NullComparer<T>, TContext>(tempArray.AsSpan(0, span.Length), context, comparer, BUFFER_TEMP);
            // Find min and max to determine range
            var minValue = T.MaxValue;
            var maxValue = T.MinValue;

            // comparing with Min/Max should not track as statistic
            for (var i = 0; i < s.Length; i++)
            {
                var value = s.Read(i);
                if (value < minValue) minValue = value;
                if (value > maxValue) maxValue = value;
            }

            // If all elements are the same, no need to sort
            if (minValue == maxValue) return;

            // Use ulong arithmetic for range calculation to correctly handle all supported types
            // including ulong and nuint. ulong.CreateTruncating preserves 2's complement bit patterns
            // for signed types, so wrapping ulong subtraction gives the correct element count for both
            // signed and unsigned types.
            var umin = ulong.CreateTruncating(minValue);
            var umax = ulong.CreateTruncating(maxValue);

            // range == 0 means overflow (actual range is 2^64), which implies an enormous value range
            ulong range = umax - umin + 1;
            if (range == 0 || range > (ulong)MaxCountArraySize)
                throw new ArgumentException($"Value range ({range}) exceeds maximum count array size ({MaxCountArraySize}). Consider another comparison-based sort.");
            if (range > (ulong)s.Length * MaxRangeFactor)
                throw new ArgumentException($"Value range ({range}) is too large relative to array size ({s.Length}): range/n={range}/{(ulong)s.Length} exceeds limit of {MaxRangeFactor}. Consider another comparison-based sort.");

            var size = (int)range;

            // Use stackalloc for small count arrays, ArrayPool for larger ones
            int[]? rentedCountArray = null;
            Span<int> countArray = size <= StackAllocThreshold
                ? stackalloc int[size]
                : (rentedCountArray = ArrayPool<int>.Shared.Rent(size)).AsSpan(0, size);
            countArray.Clear();
            try
            {
                CountSort(s, tempSpan, countArray, umin);
            }
            finally
            {
                if (rentedCountArray is not null)
                {
                    ArrayPool<int>.Shared.Return(rentedCountArray);
                }
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountSort<T, TContext>(SortSpan<T, NullComparer<T>, TContext> s, SortSpan<T, NullComparer<T>, TContext> tempSpan, Span<int> countArray, ulong umin)
        where T : IBinaryInteger<T>
        where TContext : ISortContext
    {
        // Count occurrences
        s.Context.OnPhase(SortPhase.DistributionCount);
        for (var i = 0; i < s.Length; i++)
        {
            var value = s.Read(i);
            var index = (int)(ulong.CreateTruncating(value) - umin);
            countArray[index]++;
        }

        // Calculate cumulative counts (for stable sort)
        s.Context.OnPhase(SortPhase.DistributionAccumulate);
        for (var i = 1; i < countArray.Length; i++)
        {
            countArray[i] += countArray[i - 1];
        }

        // Build result array in reverse order to maintain stability
        s.Context.OnPhase(SortPhase.DistributionWrite);
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var value = s.Read(i);
            var index = (int)(ulong.CreateTruncating(value) - umin);
            var pos = countArray[index] - 1;
            tempSpan.Write(pos, value);
            countArray[index]--;
        }

        // Write sorted data back to original span using CopyTo for efficiency
        tempSpan.CopyTo(0, s, 0, s.Length);
    }

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> if <typeparamref name="T"/> is not supported.
    /// Supported types: sbyte, byte, short, ushort, int, uint, long, ulong, nint, nuint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureSupportedType<T>() where T : IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) ||
            typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
            typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
            typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
            typeof(T) == typeof(nint) || typeof(T) == typeof(nuint))
            return;
        if (typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128))
            throw new NotSupportedException($"Type {typeof(T).Name} with 128-bit size is not supported. Maximum supported bit size is 64.");
        throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
    }
}

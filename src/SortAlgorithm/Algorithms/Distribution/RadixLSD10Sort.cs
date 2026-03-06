using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 10進数基数のLSD（Least Significant Digit）基数ソート。
/// 値を10進数の桁として扱い、最下位桁から最上位桁まで順に安定なバケットソートを繰り返します。
/// 人間が理解しやすい10進数ベースのアルゴリズムで、デバッグや教育目的に適しています。
/// <br/>
/// Decimal-based LSD (Least Significant Digit) radix sort.
/// Treats values as decimal digits and performs stable bucket sorting repeatedly from the least significant digit to the most significant digit.
/// This decimal-based algorithm is easy for humans to understand and is suitable for debugging and educational purposes.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct LSD Radix Sort (Decimal Base):</strong></para>
/// <list type="number">
/// <item><description><strong>Stable Sorting per Digit:</strong> Each pass must be stable (preserve relative order of equal keys).
/// This implementation uses counting sort to maintain insertion order, ensuring stability.</description></item>
/// <item><description><strong>Digit Extraction Consistency:</strong> For a given position, the digit must be extracted consistently across all values.
/// This uses (value / divisor) % 10 where divisor = 10^d (d = 0, 1, 2, ...).</description></item>
/// <item><description><strong>LSD Processing Order:</strong> Process digits from least significant (ones place) to most significant (highest decimal digit).
/// This ensures that lower-order digits are already sorted when processing higher-order digits.</description></item>
/// <item><description><strong>Complete Pass Coverage:</strong> Must perform d passes where d = ⌈log₁₀(max)⌉ + 1 (number of decimal digits in the maximum value).
/// Incomplete passes result in partially sorted arrays.</description></item>
/// <item><description><strong>Negative Number Handling:</strong> For signed integers, uses sign-bit flipping to convert all values to unsigned representation.
/// This ensures negative values sort before positive values without requiring absolute value calculation (avoiding int.MinValue overflow).</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution (Non-comparison based)</description></item>
/// <item><description>Stable      : Yes (insertion order preserved in buckets)</description></item>
/// <item><description>In-place    : No (O(n + 10) auxiliary space for buckets)</description></item>
/// <item><description>Best case   : Θ(d × n) - d = number of decimal digits (d = ⌈log₁₀(max)⌉ + 1)</description></item>
/// <item><description>Average case: Θ(d × n) - Linear in input size, independent of value distribution</description></item>
/// <item><description>Worst case  : Θ(d × n) - Performance depends on digit count, not comparisons</description></item>
/// <item><description>Comparisons : 0 (Non-comparison sort; uses only arithmetic operations)</description></item>
/// <item><description>Swaps       : 0 (Elements moved via bucket redistribution, not swaps)</description></item>
/// <item><description>Writes      : d × n (Each element written once per digit pass)</description></item>
/// <item><description>Reads       : d × n (Each element read once per digit pass)</description></item>
/// </list>
/// <para><strong>Note:</strong> Uses decimal arithmetic (division and modulo), which may be slower than binary-based radix sorts (e.g., RadixLSD4Sort with bit shifts).
/// However, it is more intuitive for understanding and debugging.</para>
/// <para><strong>Supported Types:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Supported:</strong> byte, sbyte, short, ushort, int, uint, long, ulong, nint, nuint (up to 64-bit)</description></item>
/// <item><description><strong>Not Supported:</strong> Int128, UInt128, BigInteger (&gt;64-bit types)</description></item>
/// </list>
/// <para><strong>Why 128-bit Types Are Not Supported:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Key Storage Limitation:</strong> This implementation uses <c>ulong</c> (64-bit) to store radix keys.
/// Supporting 128-bit would require <c>UInt128</c> keys, significantly increasing memory usage and complexity.</description></item>
/// <item><description><strong>Stack Allocation Constraints:</strong> Larger keys increase stack pressure for bucket arrays,
/// potentially causing stack overflow in deep recursion scenarios.</description></item>
/// <item><description><strong>Performance Trade-offs:</strong> 128-bit operations are significantly slower than 64-bit on most architectures,
/// negating the performance benefits of radix sort.</description></item>
/// <item><description><strong>Practical Rarity:</strong> Sorting 128-bit integers is uncommon in typical applications.
/// For such cases, comparison-based sorts (e.g., QuickSort, MergeSort) remain practical alternatives.</description></item>
/// <item><description><strong>Implementation Complexity:</strong> Adding 128-bit support would require substantial code duplication
/// and conditional logic, reducing maintainability without significant real-world benefit.</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Radix_sort#Least_significant_digit</para>
/// </remarks>
public static class RadixLSD10Sort
{
    private const int RadixBase = 10;       // Decimal base

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;           // Main input array
    private const int BUFFER_TEMP = 1;           // Temporary buffer for digit redistribution

    /// <summary>
    /// Sorts the elements in the specified span.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T"> The type of elements to sort. Must be a binary integer type with defined min/max values.</typeparam>
    /// <param name="span"> The span of elements to sort.</param>
    public static void Sort<T>(Span<T> span) where T : IBinaryInteger<T>, IMinMaxValue<T>
        => Sort(span, NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span.
    /// </summary>
    /// <typeparam name="T"> The type of elements to sort. Must be a binary integer type with defined min/max values.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span"> The span of elements to sort.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation.
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is a 128-bit type (<see cref="Int128"/> or <see cref="UInt128"/>).
    /// This implementation only supports integer types up to 64-bit due to key storage and performance constraints.
    /// See class-level remarks for detailed explanation of this limitation.
    /// </exception>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        // Rent buffers from ArrayPool
        var tempArray = ArrayPool<T>.Shared.Rent(span.Length);

        try
        {
            var tempBuffer = tempArray.AsSpan(0, span.Length);

            // Use stackalloc for small fixed-size bucket counts (10 ints = 40 bytes)
            Span<int> bucketCounts = stackalloc int[RadixBase];

            var s = new SortSpan<T, NullComparer<T>, TContext>(span, context, default, BUFFER_MAIN);
            var temp = new SortSpan<T, NullComparer<T>, TContext>(tempBuffer, context, default, BUFFER_TEMP);

            // Determine bit size for sign-bit flipping
            var bitSize = GetBitSize<T>();

            // Find min and max unsigned keys to determine required digit count
            var minKey = ulong.MaxValue;
            var maxKey = ulong.MinValue;

            for (var i = 0; i < s.Length; i++)
            {
                var value = s.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                if (key < minKey) minKey = key;
                if (key > maxKey) maxKey = key;
            }

            // Early exit: if all elements are the same (range == 0), no sorting needed
            if (minKey == maxKey) return;

            // Pre-computed powers of 10 for O(1) divisor lookup
            // Pow10[d] = 10^d for d in [0..19], supporting up to 20 decimal digits (ulong max)
            // This eliminates O(digit) loop in divisor calculation for each recursive call
            ReadOnlySpan<ulong> pow10 = [
                1UL,                      // 10^0
                10UL,                     // 10^1
                100UL,                    // 10^2
                1_000UL,                  // 10^3
                10_000UL,                 // 10^4
                100_000UL,                // 10^5
                1_000_000UL,              // 10^6
                10_000_000UL,             // 10^7
                100_000_000UL,            // 10^8
                1_000_000_000UL,          // 10^9
                10_000_000_000UL,         // 10^10
                100_000_000_000UL,        // 10^11
                1_000_000_000_000UL,      // 10^12
                10_000_000_000_000UL,     // 10^13
                100_000_000_000_000UL,    // 10^14
                1_000_000_000_000_000UL,  // 10^15
                10_000_000_000_000_000UL, // 10^16
                100_000_000_000_000_000UL,// 10^17
                1_000_000_000_000_000_000UL,  // 10^18
                10_000_000_000_000_000_000UL  // 10^19 (max for 20-digit ulong: 18,446,744,073,709,551,615)
            ];

            // Calculate required number of decimal digits based on the range
            // For a narrow range (e.g., 9,000,000,000 to 9,000,000,100), we only need digits to represent the range (100 → 3 digits)
            // instead of maxKey (9,000,000,100 → 10 digits), dramatically reducing passes
            var range = maxKey - minKey;
            var digitCount = GetDigitCountFromUlong(range, pow10);

            // Start LSD radix sort from the least significant digit
            LSDSort(s, temp, digitCount, bitSize, minKey, bucketCounts, pow10);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LSDSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> source, SortSpan<T, TComparer, TContext> temp, int digitCount, int bitSize, ulong minKey, Span<int> bucketCounts, ReadOnlySpan<ulong> pow10)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        Span<int> bucketStarts = stackalloc int[RadixBase];

        // Perform LSD radix sort on unsigned keys
        for (int d = 0; d < digitCount; d++)
        {
            source.Context.OnPhase(SortPhase.RadixPass, d, digitCount);
            var divisor = pow10[d];

            // Clear bucket counts
            bucketCounts.Clear();

            // Count occurrences of each decimal digit
            // Use (key - minKey) to normalize the range, extracting only the necessary digits
            for (var i = 0; i < source.Length; i++)
            {
                var value = source.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                var normalizedKey = key - minKey;
                var digit = (int)((normalizedKey / divisor) % 10);
                bucketCounts[digit]++;
            }

            // Calculate cumulative bucket positions
            bucketStarts[0] = 0;
            for (var i = 1; i < RadixBase; i++)
            {
                bucketStarts[i] = bucketStarts[i - 1] + bucketCounts[i - 1];
            }

            // Distribute elements into temp buffer based on current digit
            for (var i = 0; i < source.Length; i++)
            {
                var value = source.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                var normalizedKey = key - minKey;
                var digit = (int)((normalizedKey / divisor) % 10);
                var pos = bucketStarts[digit]++;
                temp.Write(pos, value);
            }

            // Copy back from temp buffer
            temp.CopyTo(0, source, 0, source.Length);
        }
    }

    /// <summary>
    /// Get the bit size of the type T.
    /// </summary>
    /// <typeparam name="T">The binary integer type to check. Must be a standard .NET integer type.</typeparam>
    /// <returns>The bit size of the type (8, 16, 32, or 64).</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is a 128-bit type (<see cref="Int128"/> or <see cref="UInt128"/>),
    /// or any other non-standard integer type.
    /// <para>
    /// <strong>Rationale for 128-bit exclusion:</strong>
    /// This implementation uses <c>ulong</c> (64-bit) for radix key storage in <see cref="GetUnsignedKey{T}"/>.
    /// Supporting 128-bit types would require <c>UInt128</c> keys, doubling memory usage for bucket operations
    /// and degrading performance due to slower 128-bit arithmetic on most architectures.
    /// Additionally, 128-bit integer sorting is rare in practice; comparison-based sorts suffice for such cases.
    /// </para>
    /// </exception>
    /// <remarks>
    /// Supported types: byte, sbyte, short, ushort, int, uint, long, ulong, nint, nuint (up to 64-bit).
    /// </remarks>
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
    /// Convert a signed or unsigned value to an unsigned key for radix sorting.
    /// For signed types, flips the sign bit to ensure correct ordering (negative values sort before positive).
    /// For unsigned types, returns the value as-is.
    /// </summary>
    /// <remarks>
    /// Sign-bit flipping technique:
    /// - 32-bit signed: key = (uint)value ^ 0x8000_0000
    /// - 64-bit signed: key = (ulong)value ^ 0x8000_0000_0000_0000
    ///
    /// This ensures:
    /// - int.MinValue (-2147483648) → 0x0000_0000 (sorts first)
    /// - -1 → 0x7FFF_FFFF (sorts before 0)
    /// - 0 → 0x8000_0000 (sorts after negatives)
    /// - int.MaxValue (2147483647) → 0xFFFF_FFFF (sorts last)
    ///
    /// Advantages:
    /// - No Abs() needed, avoids MinValue overflow
    /// - Single unified pass for all values
    /// - Maintains stability
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetUnsignedKey<T>(T value, int bitSize) where T : IBinaryInteger<T>
    {
        if (bitSize <= 8)
        {
            var byteValue = byte.CreateTruncating(value);
            if (typeof(T) == typeof(sbyte))
                return (byte)(byteValue ^ 0x80); // Flip sign bit for signed byte
            return byteValue;
        }
        else if (bitSize <= 16)
        {
            var ushortValue = ushort.CreateTruncating(value);
            if (typeof(T) == typeof(short))
                return (ushort)(ushortValue ^ 0x8000); // Flip sign bit for signed short
            return ushortValue;
        }
        else if (bitSize <= 32)
        {
            var uintValue = uint.CreateTruncating(value);
            // Signed types (int, nint on 32-bit) need sign-bit flip
            if (typeof(T) == typeof(int) || (typeof(T) == typeof(nint) && IntPtr.Size == 4))
                return uintValue ^ 0x8000_0000;
            // Unsigned types (uint, nuint on 32-bit): no flip needed
            return uintValue;
        }
        else // 64-bit
        {
            var ulongValue = ulong.CreateTruncating(value);
            // Signed types (long, nint on 64-bit) need sign-bit flip
            if (typeof(T) == typeof(long) || (typeof(T) == typeof(nint) && IntPtr.Size == 8))
                return ulongValue ^ 0x8000_0000_0000_0000;
            // Unsigned types (ulong, nuint on 64-bit): no flip needed
            return ulongValue;
        }
    }

    /// <summary>
    /// Get the number of decimal digits needed to represent a ulong value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDigitCountFromUlong(ulong value, ReadOnlySpan<ulong> pow10)
    {
        if (value == 0) return 1;

        // value < 10^1 -> 1 digit, value < 10^2 -> 2 digits, ..., value < 10^d -> d digits
        // Pow10 is 10^0...10^19
        for (int d = 1; d < pow10.Length; d++)
            if (value < pow10[d]) return d;

        return 20; // max for ulong (10^20 > 2^64)
    }
}

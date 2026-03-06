using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 10進数基数のMSD（Most Significant Digit）基数ソート。
/// 値を10進数の桁として扱い、最上位桁から最下位桁まで再帰的にバケットソートを行います。
/// 人間が理解しやすい10進数ベースのアルゴリズムで、デバッグや教育目的に適しています。
/// <br/>
/// Decimal-based MSD (Most Significant Digit) radix sort.
/// Treats values as decimal digits and performs bucket sorting recursively from the most significant digit to the least significant digit.
/// This decimal-based algorithm is easy for humans to understand and is suitable for debugging and educational purposes.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct MSD Radix Sort (Decimal Base):</strong></para>
/// <list type="number">
/// <item><description><strong>Sign-Bit Flipping for Signed Integers:</strong> For signed types, the sign bit is flipped to convert signed values to unsigned keys.
/// This ensures negative values are ordered correctly before positive values without separate processing.
/// This technique avoids the MinValue overflow issue with Abs() and maintains stability.</description></item>
/// <item><description><strong>Dynamic Starting Digit (MSD Optimization):</strong> Before sorting, performs a single O(n) pass to find the maximum key value
/// and computes the actual required digit count. This eliminates empty high-order digit passes, which is critical for MSD performance
/// when values are small relative to the type's capacity (e.g., values ≤ 999 in a 64-bit type need only 3 digits, not 20).</description></item>
/// <item><description><strong>Digit Extraction Consistency:</strong> For a given position from most significant digit, extract the digit using (key / divisor) % 10
/// where divisor = 10^(digitCount - 1 - d) for digit position d.</description></item>
/// <item><description><strong>MSD Processing Order:</strong> Digits must be processed from most significant (d=digitCount-1) to least significant (d=0).
/// This top-down approach partitions the array into buckets recursively, processing each bucket independently for subsequent digits.</description></item>
/// <item><description><strong>Recursive Bucket Processing:</strong> After distributing elements based on the current digit, each bucket must be recursively sorted for the remaining digits.
/// Base cases: buckets with 0 or 1 elements are already sorted; buckets where all remaining digits are the same are also sorted.</description></item>
/// <item><description><strong>Cutoff to Insertion Sort:</strong> For small buckets (typically &lt; 16 elements), switching to insertion sort can improve performance due to lower overhead.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution (Radix Sort, MSD variant)</description></item>
/// <item><description>Stable      : Yes (maintains relative order of elements with equal keys)</description></item>
/// <item><description>In-place    : No (O(n) auxiliary space for temporary buffer)</description></item>
/// <item><description>Best case   : Θ(n) - When all elements fall into one bucket early, or when the initial digit scan shows all values are equal</description></item>
/// <item><description>Average case: Θ(n + d × n) - One O(n) pass for max digit computation + d passes where d = ⌈log₁₀(actualMax)⌉ (actual data digits, not type maximum)</description></item>
/// <item><description>Worst case  : Θ(n + d × n) - Same complexity regardless of input order</description></item>
/// <item><description>Comparisons : 0 (Non-comparison sort, uses arithmetic operations only)</description></item>
/// <item><description>Digit Passes: 1 initial pass for max computation + up to d = ⌈log₁₀(actualMax)⌉, but can terminate early per bucket</description></item>
/// <item><description>Memory      : O(n) for temporary buffer</description></item>
/// </list>
/// <para><strong>MSD vs LSD (Decimal):</strong></para>
/// <list type="bullet">
/// <item><description>MSD processes high-order digits first, enabling early termination when buckets are fully sorted</description></item>
/// <item><description>MSD dynamically computes starting digit from data, avoiding unnecessary passes for small values in large types</description></item>
/// <item><description>MSD is cache-friendlier for partially sorted data as it localizes accesses within buckets</description></item>
/// <item><description>MSD requires recursive processing of buckets, adding overhead compared to LSD's iterative approach</description></item>
/// <item><description>Both MSD and LSD can be implemented as stable sorts (this implementation maintains stability)</description></item>
/// </list>
/// <para><strong>Note:</strong> Uses decimal arithmetic (division and modulo), which may be slower than binary-based radix sorts (e.g., RadixMSD4Sort with bit shifts).
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
/// <para>Wiki: https://en.wikipedia.org/wiki/Radix_sort#Most_significant_digit</para>
/// </remarks>
public static class RadixMSD10Sort
{
    private const int RadixBase = 10;       // Decimal base
    private const int InsertionSortCutoff = 16; // Switch to insertion sort for small buckets

    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TEMP = 1;       // Temporary buffer for digit redistribution

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

        // Rent buffer from ArrayPool (only temp buffer needed now)
        var tempArray = ArrayPool<T>.Shared.Rent(span.Length);

        try
        {
            var tempBuffer = tempArray.AsSpan(0, span.Length);

            var comparer = new ComparableComparer<T>();
            var s = new SortSpan<T, ComparableComparer<T>, TContext>(span, context, comparer, BUFFER_MAIN);
            var temp = new SortSpan<T, ComparableComparer<T>, TContext>(tempBuffer, context, comparer, BUFFER_TEMP);

            // Determine the bit size for sign-bit flipping
            var bitSize = GetBitSize<T>();

            // Compute actual maximum digit count from the data (MSD optimization)
            // This is the key optimization: instead of using the type's maximum possible digits,
            // we scan the data once to find the actual maximum value and its digit count.
            // This eliminates empty high-order digit passes, which is crucial for MSD performance.
            var digitCount = ComputeMaxDigit(s, 0, s.Length, bitSize);

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

            // Start MSD radix sort from the most significant digit
            MSDSort(s, temp, 0, s.Length, digitCount - 1, bitSize, pow10);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MSDSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, SortSpan<T, TComparer, TContext> temp, int start, int length, int digit, int bitSize, ReadOnlySpan<ulong> pow10)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Base case: if length is small, use insertion sort
        if (length <= InsertionSortCutoff)
        {
            InsertionSort.SortCore(s, start, start + length);
            return;
        }

        // Base case: if we've processed all digits, we're done
        if (digit < 0)
        {
            return;
        }

        s.Context.OnPhase(SortPhase.RadixPass, digit, digit);
        var divisor = pow10[digit];

        // Separate counts and offsets for clarity
        // counts[d] = number of elements with digit value d
        // offsets[d] = write position for next element with digit value d
        Span<int> counts = stackalloc int[RadixBase];
        Span<int> offsets = stackalloc int[RadixBase];

        // Phase 1: Count occurrences of each digit value
        for (var i = 0; i < length; i++)
        {
            var value = s.Read(start + i);
            var key = GetUnsignedKey(value, bitSize);
            var digitValue = (int)((key / divisor) % 10);
            counts[digitValue]++;
        }

        // Phase 2: Calculate bucket offsets (prefix sum)
        // offsets[d] = starting position for bucket d
        offsets[0] = 0;
        for (var i = 1; i < RadixBase; i++)
        {
            offsets[i] = offsets[i - 1] + counts[i - 1];
        }

        // Phase 3: Distribute elements into temp buffer
        // Use a copy of offsets as write positions (will be incremented)
        Span<int> writePos = stackalloc int[RadixBase];
        offsets.CopyTo(writePos);

        for (var i = 0; i < length; i++)
        {
            var value = s.Read(start + i);
            var key = GetUnsignedKey(value, bitSize);
            var digitValue = (int)((key / divisor) % 10);
            var destIndex = writePos[digitValue]++;
            temp.Write(start + destIndex, value);
        }

        // Copy back from temp to source
        temp.CopyTo(start, s, start, length);

        // Phase 4: Recursively sort each bucket for the next digit
        for (var i = 0; i < RadixBase; i++)
        {
            if (counts[i] > 1)
            {
                MSDSort(s, temp, start + offsets[i], counts[i], digit - 1, bitSize, pow10);
            }
        }
    }

    /// <summary>
    /// Get bit size of the type T.
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
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            return 8;
        else if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            return 16;
        else if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            return 32;
        else if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
            return 64;
        else if (typeof(T) == typeof(nint) || typeof(T) == typeof(nuint))
            return IntPtr.Size * 8;
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
            // byte or sbyte
            if (typeof(T) == typeof(sbyte))
            {
                var sbyteValue = sbyte.CreateTruncating(value);
                return (ulong)((byte)sbyteValue ^ 0x80);
            }
            else
            {
                return byte.CreateTruncating(value);
            }
        }
        else if (bitSize <= 16)
        {
            // short or ushort
            if (typeof(T) == typeof(short))
            {
                var shortValue = short.CreateTruncating(value);
                return (ulong)((ushort)shortValue ^ 0x8000);
            }
            else
            {
                return ushort.CreateTruncating(value);
            }
        }
        else if (bitSize <= 32)
        {
            // int, uint, or nint/nuint on 32-bit platform
            if (typeof(T) == typeof(int))
            {
                var intValue = int.CreateTruncating(value);
                return (uint)intValue ^ 0x8000_0000;
            }
            else if (typeof(T) == typeof(nint))
            {
                // nint is signed, needs sign-bit flip
                var nintValue = nint.CreateTruncating(value);
                return (uint)nintValue ^ 0x8000_0000;
            }
            else
            {
                // uint or nuint (unsigned, no flip needed)
                return uint.CreateTruncating(value);
            }
        }
        else if (bitSize <= 64)
        {
            // long, ulong, or nint/nuint on 64-bit platform
            if (typeof(T) == typeof(long))
            {
                var longValue = long.CreateTruncating(value);
                return (ulong)longValue ^ 0x8000_0000_0000_0000;
            }
            else if (typeof(T) == typeof(nint))
            {
                // nint is signed, needs sign-bit flip (64-bit platform)
                var nintValue = nint.CreateTruncating(value);
                return (ulong)nintValue ^ 0x8000_0000_0000_0000;
            }
            else
            {
                // ulong or nuint (unsigned, no flip needed)
                return ulong.CreateTruncating(value);
            }
        }
        else
        {
            throw new NotSupportedException($"Bit size {bitSize} is not supported");
        }
    }

    /// <summary>
    /// Compute the actual maximum digit count needed for the given data.
    /// This performs a single pass through the data to find the maximum key value,
    /// then calculates the number of digits required.
    /// This optimization is crucial for MSD radix sort to avoid processing empty high-order digits.
    /// </summary>
    /// <remarks>
    /// MSD Optimization:
    /// - Without this: Always starts from maximum possible digits (e.g., 20 for 64-bit), causing empty recursion
    /// - With this: Starts from actual required digits (e.g., 3 for values &lt;= 999), avoiding unnecessary passes
    /// 
    /// Performance Impact:
    /// - One O(n) pass upfront to scan maxKey
    /// - Eliminates O(d × n) work for d unnecessary high-order digits
    /// - Critical for data with small values in large integer types (e.g., byte values in long arrays)
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeMaxDigit<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int start, int length, int bitSize)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (length == 0) return 0;

        // Find maximum key in the data
        var maxKey = 0UL;
        for (var i = 0; i < length; i++)
        {
            var value = s.Read(start + i);
            var key = GetUnsignedKey(value, bitSize);
            if (key > maxKey)
            {
                maxKey = key;
            }
        }

        // Calculate digit count from maxKey
        // log₁₀(maxKey) + 1, but using iterative division to avoid floating point
        if (maxKey == 0) return 1; // Special case: all zeros

        var digitCount = 0;
        var temp = maxKey;
        while (temp > 0)
        {
            temp /= 10;
            digitCount++;
        }

        return digitCount;
    }
}

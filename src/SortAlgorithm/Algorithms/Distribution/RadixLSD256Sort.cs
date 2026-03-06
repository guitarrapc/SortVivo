using SortAlgorithm.Contexts;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// 2^8 (256) 基数のLSD基数ソート。
/// 値をビット列として扱い、8ビットずつ（256種類）の桁に分けてバケットソートを行います。
/// 最下位桁（Least Significant Digit）から最上位桁へ向かって処理することで、安定なソートを実現します。
/// 符号付き整数は符号ビット反転により、負数も含めて正しくソートされます。
/// <br/>
/// LSD Radix Sort with radix 2^8 (256).
/// Treats values as bit sequences, dividing them into 8-bit digits (256 buckets) and performing bucket sort for each digit.
/// Processing from the Least Significant Digit to the most significant ensures a stable sort.
/// Signed integers are handled via sign-bit flipping to maintain correct ordering including negative values.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct LSD Radix Sort (Base-256):</strong></para>
/// <list type="number">
/// <item><description><strong>Sign-Bit Flipping for Signed Integers:</strong> For signed types, the sign bit is flipped to convert signed values to unsigned keys:
/// - 32-bit: key = (uint)value ^ 0x8000_0000
/// - 64-bit: key = (ulong)value ^ 0x8000_0000_0000_0000
/// This ensures negative values are ordered correctly before positive values without separate processing.
/// This technique avoids the MinValue overflow issue with Abs() and maintains stability.</description></item>
/// <item><description><strong>Digit Extraction Correctness:</strong> For each digit position d (from 0 to digitCount-1), extract the d-th 8-bit digit using bitwise operations:
/// digit = (key >> (d × 8)) &amp; 0xFF. This ensures each byte of the integer is processed independently.</description></item>
/// <item><description><strong>Stable Distribution (Counting Sort per Digit):</strong> Within each digit pass, elements are distributed into 256 buckets (0-255) based on the current digit value.
/// The distribution must preserve the relative order of elements with the same digit value (stable). This is achieved by processing elements in forward order and appending to buckets.</description></item>
/// <item><description><strong>LSD Processing Order:</strong> Digits must be processed from least significant (d=0) to most significant (d=digitCount-1).
/// This bottom-up approach ensures that after processing digit d, all digits 0 through d are correctly sorted, with stability maintained by previous passes.</description></item>
/// <item><description><strong>Digit Count Determination with Early Termination:</strong> The number of passes (digitCount) is determined by the actual range of values, not the full bit width.
/// digitCount = ⌈requiredBits / 8⌉ where requiredBits is calculated from (max XOR min) to find differing bits.
/// This optimization skips unnecessary high-order digit passes when the value range is small. When all elements are equal (range == 0), sorting is skipped entirely.</description></item>
/// <item><description><strong>Bucket Collection Order:</strong> After distributing elements for a digit, buckets must be collected in ascending order (bucket 0, 1, 2, ..., 255).
/// Due to sign-bit flipping, negative values naturally sort before positive values.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Distribution (Radix Sort, LSD variant)</description></item>
/// <item><description>Stable      : Yes (maintains relative order of elements with equal keys)</description></item>
/// <item><description>In-place    : No (O(n) auxiliary space for temporary buffer)</description></item>
/// <item><description>Best case   : Θ(n) - When all elements are identical (early termination on range == 0)</description></item>
/// <item><description>Average case: Θ(d × n) - Linear in input size, where d depends on actual value range</description></item>
/// <item><description>Worst case  : Θ(d × n) - Same complexity regardless of input order, d = ⌈bitSize/8⌉ for full range</description></item>
/// <item><description>Comparisons : 0 (Non-comparison sort, uses bitwise operations only)</description></item>
/// <item><description>Digit Passes: d = ⌈requiredBits/8⌉ (early termination based on actual value range, not full bit width)</description></item>
/// <item><description>Reads       : n (initial min/max scan) + d × n (one read per distribute pass) + optional final copy</description></item>
/// <item><description>Writes      : d × n (one write per distribute pass to temp) + optional final copy</description></item>
/// <item><description>Memory      : O(n) for temporary buffer</description></item>
/// </list>
/// <para><strong>Radix-256 Advantages:</strong></para>
/// <list type="bullet">
/// <item><description>Fewer passes than radix-10: 4 passes for 32-bit vs 10 passes for decimal</description></item>
/// <item><description>Efficient bit operations: Shift and mask are faster than division/modulo</description></item>
/// <item><description>Cache-friendly bucket size: 256 buckets fit well in L1/L2 cache</description></item>
/// <item><description>Sign-bit flip handles signed integers without separate negative/positive processing</description></item>
/// <item><description>Stable sort: Maintains relative order of equal elements</description></item>
/// </list>
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
public static class RadixLSD256Sort
{
    private const int RadixBits = 8;        // 8 bits per digit
    private const int RadixSize = 256;      // 2^8 = 256 buckets

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

        // Rent buffers from ArrayPool
        var tempArray = ArrayPool<T>.Shared.Rent(span.Length);
        var bucketOffsetsArray = ArrayPool<int>.Shared.Rent(RadixSize + 1);

        try
        {
            var tempBuffer = tempArray.AsSpan(0, span.Length);
            var bucketOffsets = bucketOffsetsArray.AsSpan(0, RadixSize + 1);

            // Use NullComparer since radix sort doesn't use comparisons
            var s = new SortSpan<T, NullComparer<T>, TContext>(span, context, default, BUFFER_MAIN);
            var temp = new SortSpan<T, NullComparer<T>, TContext>(tempBuffer, context, default, BUFFER_TEMP);

            // Determine the number of digits based on type size
            // GetBitSize throws NotSupportedException for unsupported types (>64-bit)
            var bitSize = GetBitSize<T>();

            // Find min and max to determine actual required passes
            // This optimization skips unnecessary high-order digit passes
            var minKey = ulong.MaxValue;
            var maxKey = ulong.MinValue;

            for (var i = 0; i < s.Length; i++)
            {
                var value = s.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                if (key < minKey) minKey = key;
                if (key > maxKey) maxKey = key;
            }

            // Calculate required number of passes based on the range
            // XOR to find differing bits, then count bits needed
            var range = maxKey ^ minKey;

            // Early return if all elements are equal (range == 0)
            if (range == 0) return;

            var requiredBits = 64 - System.Numerics.BitOperations.LeadingZeroCount(range);
            var digitCount = (requiredBits + RadixBits - 1) / RadixBits;

            // Start LSD radix sort from the least significant digit
            LSDSort(s, temp, digitCount, bitSize, bucketOffsets);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            ArrayPool<int>.Shared.Return(bucketOffsetsArray);
        }
    }

    private static void LSDSort<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, SortSpan<T, TComparer, TContext> temp, int digitCount, int bitSize, Span<int> bucketOffsets)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var src = s;
        var dst = temp;

        // Perform LSD radix sort with ping-pong buffers
        for (int d = 0; d < digitCount; d++)
        {
            src.Context.OnPhase(SortPhase.RadixPass, d, digitCount);
            var shift = d * RadixBits;

            // Clear bucket offsets
            // bucketOffsets[0..RadixSize] stores bucket boundaries:
            // - Initially: bucketOffsets[digit+1] = count of elements with 'digit'
            // - After prefix sum: bucketOffsets[digit] = start index for 'digit' bucket
            // - During distribution: bucketOffsets[digit]++ tracks next write position
            bucketOffsets.Clear();

            // Count occurrences of each digit (store count in digit+1 position)
            for (var i = 0; i < src.Length; i++)
            {
                var value = src.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                var digit = (int)((key >> shift) & 0xFF);
                bucketOffsets[digit + 1]++;
            }

            // Calculate cumulative offsets (prefix sum)
            // After this: bucketOffsets[digit] = start index for bucket 'digit'
            for (var i = 1; i <= RadixSize; i++)
            {
                bucketOffsets[i] += bucketOffsets[i - 1];
            }

            // Distribute elements from src to dst based on current digit
            for (var i = 0; i < src.Length; i++)
            {
                var value = src.Read(i);
                var key = GetUnsignedKey(value, bitSize);
                var digit = (int)((key >> shift) & 0xFF);
                var destIndex = bucketOffsets[digit]++;
                dst.Write(destIndex, value);
            }

            // Swap src/dst for next pass (ping-pong)
            var tempSortSpan = src;
            src = dst;
            dst = tempSortSpan;
        }

        // After digitCount swaps, if digitCount is odd, final data is in src (which points to temp buffer after odd swaps)
        // Pass 0: s→temp, swap (src=temp), Pass 1: temp→s, swap (src=s), ...
        if ((digitCount & 1) == 1)
        {
            src.CopyTo(0, s, 0, s.Length);
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
}

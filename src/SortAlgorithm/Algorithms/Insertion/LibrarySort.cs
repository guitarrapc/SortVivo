using System.Buffers;
using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// ギャップベースの挿入ソートで、理論上O(n log n)の期待計算量を持ちます。
/// 図書館の本棚のように、要素間に適度な隙間(ギャップ)を保持することで、挿入時のシフト量を大幅に削減します。
/// 定期的なリバランス操作により、ギャップを均等に再配置し、効率的な挿入を維持します。
/// <br/>
/// A gap-based insertion sort with O(n log n) expected time complexity.
/// Like library bookshelves, it maintains gaps between elements to reduce
/// the amount of shifting during insertions. Periodic rebalancing redistributes
/// gaps evenly to maintain efficient insertion performance.
/// </summary>
/// <remarks>
/// <para><strong>Core Principles of Library Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Gap Allocation:</strong> Uses (1 + ε)n space where ε is the gap ratio.
/// The extra εn positions serve as gaps, allowing insertions without shifting all subsequent elements.
/// Typical values: ε = 0.5 to 1.0, trading memory for performance.</description></item>
/// <item><description><strong>Binary Search for Position:</strong> Each new element's position is found
/// via binary search among existing elements in O(log n) time, ignoring gap positions.
/// This is significantly faster than linear search in standard insertion sort.</description></item>
/// <item><description><strong>Limited Shift Range:</strong> When inserting, shift elements right only
/// until the nearest gap is reached. With well-distributed gaps, average shift distance is O(log n)
/// rather than O(n), reducing insertion cost from O(n) to O(log n) per element.</description></item>
/// <item><description><strong>Periodic Rebalancing:</strong> When gaps become unevenly distributed,
/// rebalance the entire array to restore uniform gap distribution. Rebalancing occurs every 2^i or 4^i elements
/// (doubling strategy) so the amortized cost remains O(1) per insertion.</description></item>
/// <item><description><strong>Randomization (Theoretical):</strong> The O(n log n) guarantee assumes
/// random input order or shuffling. Without randomization, worst-case remains O(n²) when gaps cluster badly.
/// In practice, for general unsorted data, randomization is often unnecessary.</description></item>
/// </list>
/// <para><strong>Algorithm Overview:</strong></para>
/// <list type="number">
/// <item><description><strong>Initialization:</strong> Create auxiliary array of size (1+ε)n.
/// Mark gap positions (null or sentinel). Start with small sorted region using standard insertion sort.</description></item>
/// <item><description><strong>Insertion Loop:</strong> For each new element:
/// - Binary search among non-gap elements to find insertion position
/// - If position has gap, write directly; otherwise shift right until gap found
/// - Handle equal elements with randomization to maintain gap distribution</description></item>
/// <item><description><strong>Rebalancing:</strong> When element count reaches rebalance threshold (2x or 4x):
/// - Collect all non-gap elements
/// - Redistribute into auxiliary array with evenly spaced gaps
/// - Rebalance factor: spread elements across (2+2ε) times current size
/// - Reset counters and continue insertion</description></item>
/// <item><description><strong>Final Extraction:</strong> After all insertions, extract non-gap elements
/// back to original array in sorted order.</description></item>
/// </list>
/// <para><strong>Gap Management Strategy:</strong></para>
/// <list type="bullet">
/// <item><description>Gap Ratio (ε): 0.5 provides good balance (1.5n total space, 0.5n gaps)</description></item>
/// <item><description>Initial Size: Start small (e.g., 32 elements) with standard insertion sort</description></item>
/// <item><description>Growth Factor: Rebalance every 4x elements (more practical than 2x from paper)</description></item>
/// <item><description>Gap Representation: Use nullable wrapper or max value as sentinel for gaps</description></item>
/// <item><description>Spacing: After rebalancing, distribute elements uniformly with gap:element ratio = ε:1</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family        : Insertion (gap-based variant)</description></item>
/// <item><description>Stable        : Yes (equal elements maintain relative order during shifts)</description></item>
/// <item><description>In-place      : No (requires (1+ε)n auxiliary space for gaps)</description></item>
/// <item><description>Best case     : O(n) - Already sorted with uniformly distributed gaps</description></item>
/// <item><description>Average case  : O(n log n) - With random input and good gap distribution</description></item>
/// <item><description>Worst case    : O(n²) - Pathological gap clustering without randomization</description></item>
/// <item><description>Space         : O(n) - Auxiliary array of size (1+ε)n ≈ 1.5n to 2n</description></item>
/// <item><description>Binary Search : O(log n) per insertion to find position</description></item>
/// <item><description>Shift Cost    : O(log n) average per insertion with good gaps</description></item>
/// <item><description>Rebalance     : O(n) total across all rebalancing operations (amortized O(1))</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Paper: https://arxiv.org/abs/cs/0407003 "Insertion Sort is O(n log n)" by Michael A. Bender, Martín Farach-Colton, and Miguel Mosteiro</para>
/// <para>Conference: Proceedings of the Third International Conference on Fun With Algorithms (FUN 2004)</para>
/// </remarks>
public static class LibrarySort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;        // Main input array
    private const int BUFFER_AUX = 1;         // Auxiliary array with gaps
    // Note: positions buffer is not tracked for performance (uses fast memcpy instead)

    // Gap ratio: ε = 0.5 means (1+ε)n = 1.5n space
    private const double GapRatio = 0.5;

    // Rebalance every R times growth
    private const int RebalanceFactor = 4;

    // Small array threshold for fallback to InsertionSort
    private const int SmallSortThreshold = 32;

    // Maximum distance to search for a gap during insertion
    private const int MaxGapSearchDistance = 20;

    // Trigger early rebalance if shift distance exceeds this threshold
    private const int MaxShiftDistanceBeforeRebalance = 64;

    // Safety margin for auxiliary buffer size (1.05 = 5% extra space)
    private const double AuxSizeSafetyMargin = 1.05;

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
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context for tracking statistics and observations during sorting.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var length = span.Length;
        if (length <= 1) return;

        // For very small arrays, use standard insertion sort
        if (length <= SmallSortThreshold)
        {
            InsertionSort.Sort(span, 0, span.Length, comparer, context);
            return;
        }

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
        SortCore(s, length, comparer, context);
    }

    /// <summary>
    /// Core sorting logic with proper gap management and O(log n) search.
    /// </summary>
    private static void SortCore<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, int length, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Auxiliary array size: (1+ε)n with safety margin
        // With ε=0.5: (1.5 * 1.05)n ≈ 1.575n
        var auxSize = (int)Math.Ceiling(length * (1 + GapRatio) * AuxSizeSafetyMargin);

        var auxArray = ArrayPool<LibraryElement<T>>.Shared.Rent(auxSize);
        var positionsArray = ArrayPool<int>.Shared.Rent(length);
        var tempArray = ArrayPool<T>.Shared.Rent(length);

        try
        {
            var auxComparer = new LibraryElementComparer<T, TComparer>(comparer);
            var aux = new SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext>(auxArray.AsSpan(0, auxSize), context, auxComparer, BUFFER_AUX);
            // Note: positions uses Span<int> (not SortSpan) for O(1) memcpy performance
            var positions = positionsArray.AsSpan(0, length);

            // Initialize as gaps
            var gap = new LibraryElement<T>();
            for (var i = 0; i < auxSize; i++)
            {
                aux.Write(i, gap);
            }

            // Phase 1: Initial sort
            context.OnPhase(SortPhase.LibrarySortPhase, 1);
            var initSize = Math.Min(SmallSortThreshold, length);
            InsertionSort.SortCore(s, 0, initSize);

            // Place with gaps and build initial position buffer
            var auxEnd = PlaceWithGaps(aux, s, 0, initSize, 0, auxSize, positions, out var posCount);

            var sorted = initSize;
            var nextRebalance = initSize * RebalanceFactor;

            // Phase 2: Insert remaining
            context.OnPhase(SortPhase.LibrarySortPhase, 2);
            for (var i = initSize; i < length; i++)
            {
                if (sorted >= nextRebalance)
                {
                    auxEnd = Rebalance(aux, auxSize, positions, ref posCount, tempArray);
                    nextRebalance = sorted * RebalanceFactor;
                }

                var elem = s.Read(i);
                var insertIdx = BinarySearchPositions(aux, positions, posCount, elem, comparer);

                var needsRebalance = InsertAndUpdate(aux, ref auxEnd, auxSize, elem, positions, ref posCount, insertIdx);
                sorted++;

                // Early rebalance if large shift was detected (gaps are clustering)
                if (needsRebalance && sorted < nextRebalance)
                {
                    auxEnd = Rebalance(aux, auxSize, positions, ref posCount, tempArray);
                    nextRebalance = sorted * RebalanceFactor;
                }
            }

            // Phase 3: Extract
            context.OnPhase(SortPhase.LibrarySortPhase, 3);
            for (var i = 0; i < posCount && i < length; i++)
            {
                var pos = positions[i];
                s.Write(i, aux.Read(pos).Value);
            }
        }
        finally
        {
            ArrayPool<LibraryElement<T>>.Shared.Return(auxArray);
            ArrayPool<int>.Shared.Return(positionsArray);
            ArrayPool<T>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>
    /// Places elements with dynamic gap distribution and builds position buffer.
    /// </summary>
    private static int PlaceWithGaps<T, TComparer, TContext>(SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext> aux, SortSpan<T, TComparer, TContext> src,
        int srcStart, int count, int auxStart, int auxSize, Span<int> positions, out int posCount)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        posCount = 0;
        if (count == 0) return auxStart;

        // Range needed: (1+ε) * count
        var rangeNeeded = (int)Math.Ceiling(count * (1 + GapRatio));
        var rangeAvailable = auxSize - auxStart;

        // Strict validation: must have enough space for all elements
        if (rangeAvailable < count)
            throw new InvalidOperationException($"Insufficient auxiliary buffer space: need at least {count} positions, but only {rangeAvailable} available (auxStart={auxStart}, auxSize={auxSize})");

        // Use the minimum of needed and available, but ensure it's at least count
        var range = Math.Min(rangeNeeded, rangeAvailable);

        // Clear range
        var gap = new LibraryElement<T>();
        for (var i = 0; i < range; i++)
        {
            aux.Write(auxStart + i, gap);
        }

        // Distribute: pos[i] = floor(i * range / count)
        // This guarantees no collisions since range >= count
        for (var i = 0; i < count; i++)
        {
            var pos = auxStart + (int)((long)i * range / count);

            // Defensive check (should never happen with range >= count)
            if (pos >= auxSize)
                throw new InvalidOperationException($"Position overflow: calculated pos={pos}, but auxSize={auxSize} (i={i}, count={count}, range={range}, auxStart={auxStart})");

            aux.Write(pos, new LibraryElement<T>(src.Read(srcStart + i)));
            positions[posCount++] = pos;
        }

        // Verify all elements were placed
        if (posCount != count)
            throw new InvalidOperationException($"Data loss detected: expected {count} elements, but only placed {posCount}");

        return auxStart + range;
    }

    /// <summary>
    /// Binary search in position buffer (O(log n)).
    /// </summary>
    private static int BinarySearchPositions<T, TComparer, TContext>(SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext> aux,
        Span<int> positions, int count, T value, TComparer comparer)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var left = 0;
        var right = count;

        while (left < right)
        {
            var mid = left + (right - left) / 2;
            var cmp = comparer.Compare(value, aux.Read(positions[mid]).Value);

            if (cmp < 0)
            {
                right = mid;
            }
            else
            {
                left = mid + 1; // Stable: insert after equal elements
            }
        }

        return left;
    }

    /// <summary>
    /// Inserts element and updates position buffer incrementally.
    /// Returns true if a large shift occurred (suggesting rebalance is needed).
    /// </summary>
    private static bool InsertAndUpdate<T, TComparer, TContext>(SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext> aux, ref int auxEnd, int maxSize,
        T value, Span<int> positions, ref int posCount, int insertIdx)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // insertIdx is the index in positions[], not the position in aux[]
        // We need to find the actual insertion position in aux[] based on the range
        int targetPos;
        int searchStart, searchEnd;

        if (insertIdx >= posCount)
        {
            // Insert at end (after last element)
            // Use maxSize instead of auxEnd to utilize all available space
            searchStart = posCount > 0 ? positions[posCount - 1] + 1 : 0;
            searchEnd = maxSize;
        }
        else if (insertIdx == 0)
        {
            // Insert at beginning (before first element)
            searchStart = 0;
            searchEnd = positions[0];
        }
        else
        {
            // Insert between positions[insertIdx-1] and positions[insertIdx]
            searchStart = positions[insertIdx - 1] + 1;
            searchEnd = positions[insertIdx];
        }

        // LibrarySort principle: larger range = more gaps available
        // Choose target based on insertion position to balance gap consumption and prevent clustering:
        // - Front insertion: prefer left side (near searchStart)
        // - Back insertion: prefer right side, but be careful with maxSize range
        // - Middle insertion: use midpoint to balance gap usage
        var rangeSize = searchEnd - searchStart;
        
        int gapTarget;
        if (insertIdx == 0)
        {
            // Front insertion: search from left to avoid clustering on right
            gapTarget = searchStart;
        }
        else if (insertIdx >= posCount)
        {
            // Back insertion: start from just after last element
            // For back insertion with maxSize range, use auxEnd as reference point
            gapTarget = posCount > 0 ? positions[posCount - 1] + 1 : 0;
        }
        else
        {
            // Middle insertion: use midpoint to balance gap consumption
            gapTarget = searchStart + rangeSize / 2;
        }
        
        // Two-stage search to leverage large ranges:
        // Stage 1: Fast search with standard radius (O(1) expected for well-distributed gaps)
        // For back insertion with large range, cap the search radius to avoid excessive scanning
        // Protect against negative values when auxEnd < searchStart (can happen after rebalance with sparse tail)
        var effectiveRangeSize = insertIdx >= posCount 
            ? Math.Min(rangeSize, Math.Max(0, auxEnd - searchStart) + MaxGapSearchDistance) 
            : rangeSize;
        var searchRadius = Math.Min(effectiveRangeSize / 2, MaxGapSearchDistance);
        var gapPos = FindGapNear(aux, gapTarget, searchStart, searchEnd, searchRadius);
        
        // Stage 2: If no gap found and range is large, expand search radius
        // This exploits LibrarySort's strength: larger range = more gaps available
        if (gapPos == -1 && effectiveRangeSize > MaxGapSearchDistance * 2)
        {
            var expandedRadius = Math.Min(effectiveRangeSize / 2, MaxGapSearchDistance * 2);
            gapPos = FindGapNear(aux, gapTarget, searchStart, searchEnd, expandedRadius);
        }

        if (gapPos != -1)
        {
            // Gap found - use it directly
            aux.Write(gapPos, new LibraryElement<T>(value));
            InsertPosition(positions, ref posCount, insertIdx, gapPos);
            return false; // No large shift
        }

        // No gap in range - need to shift elements
        // Target position is determined by insertion index
        // For back insertion, use gapTarget (positions[posCount-1]+1) for consistency
        if (insertIdx >= posCount)
        {
            targetPos = gapTarget; // Consistent with gap search target
        }
        else
        {
            targetPos = positions[insertIdx];
        }

        // Find gap for shifting using local search from target position
        // LibrarySort principle: gaps should be nearby after proper rebalancing
        var shiftGap = FindGapNear(aux, targetPos, targetPos, maxSize, MaxGapSearchDistance);

        if (shiftGap == -1)
        {
            // No gap found in entire buffer - this should rarely happen after proper rebalancing
            if (auxEnd >= maxSize)
                throw new InvalidOperationException("No gap and buffer full");
            shiftGap = auxEnd++;
        }

        // Check if shift distance is too large
        var shiftDistance = shiftGap - targetPos;
        var largeShift = shiftDistance > MaxShiftDistanceBeforeRebalance;

        // Shift elements from targetPos to shiftGap
        for (var i = shiftGap; i > targetPos; i--)
        {
            aux.Write(i, aux.Read(i - 1));
        }

        // Update positions that were shifted
        // Optimization: positions is monotonically increasing, so only scan from insertIdx onwards
        // and break early when we pass shiftGap
        for (var i = insertIdx; i < posCount; i++)
        {
            var pos = positions[i];
            if (pos >= shiftGap)
                break; // Positions are sorted, no more updates needed

            if (pos >= targetPos)
            {
                positions[i] = pos + 1;
            }
        }

        // Write the new element
        aux.Write(targetPos, new LibraryElement<T>(value));
        InsertPosition(positions, ref posCount, insertIdx, targetPos);

        // Update auxEnd to include the shift gap position
        // Use Math.Max to handle the case where we extended the array (shiftGap = auxEnd++ above)
        auxEnd = Math.Max(auxEnd, shiftGap + 1);

        return largeShift; // Return true if rebalance is recommended
    }

    /// <summary>
    /// Finds a gap near the target position using local search (expanding left and right).
    /// This approach aligns with LibrarySort's assumption that gaps are nearby,
    /// and is effective for detecting clustering.
    /// Returns -1 if no gap found within the search radius.
    /// </summary>
    private static int FindGapNear<T, TComparer, TContext>(SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext> aux, int target, int start, int end, int maxRadius)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Check target position first
        if (target >= start && target < end && !aux.Read(target).HasValue)
            return target;

        // Expand search radius alternating left and right
        for (var radius = 1; radius <= maxRadius; radius++)
        {
            // Check right
            var right = target + radius;
            if (right >= start && right < end && !aux.Read(right).HasValue)
                return right;

            // Check left
            var left = target - radius;
            if (left >= start && left < end && !aux.Read(left).HasValue)
                return left;
        }

        return -1;
    }

    /// <summary>
    /// Inserts a position into the sorted position buffer.
    /// Uses Span.CopyTo for efficient bulk memory copy instead of element-by-element iteration.
    /// Note: Statistics tracking is skipped for performance.
    /// </summary>
    private static void InsertPosition(Span<int> positions, ref int count, int idx, int pos)
    {
        // Shift elements: use Span.CopyTo for efficient memory copy
        if (idx < count)
        {
            var source = positions.Slice(idx, count - idx);
            var dest = positions.Slice(idx + 1, count - idx);
            source.CopyTo(dest);
        }
        positions[idx] = pos;
        count++;
    }

    /// <summary>
    /// Rebalances with dynamic spacing to prevent data loss.
    /// Clears the range [0, range) where range = min((1+ε)*count, auxSize),
    /// then redistributes all elements with uniform gap distribution.
    /// Returns the maximum used position + 1 for auxEnd tracking.
    /// </summary>
    private static int Rebalance<T, TComparer, TContext>(SortSpan<LibraryElement<T>, LibraryElementComparer<T, TComparer>, TContext> aux, int auxSize,
        Span<int> positions, ref int posCount, Span<T> tempBuffer)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Collect elements
        var count = 0;
        for (var i = 0; i < posCount; i++)
        {
            var elem = aux.Read(positions[i]);
            if (elem.HasValue)
            {
                tempBuffer[count++] = elem.Value;
            }
        }

        // Calculate new range: (1+ε) * count
        var rangeNeeded = (int)Math.Ceiling(count * (1 + GapRatio));

        // Strict validation: must have enough space for all elements
        if (auxSize < count)
        {
            throw new InvalidOperationException(
                $"Insufficient auxiliary buffer space for rebalance: need at least {count} positions, " +
                $"but auxSize={auxSize}. This indicates the buffer was too small from the start.");
        }

        var range = Math.Min(rangeNeeded, auxSize);

        // Clear the range [0, range) to prepare for redistribution
        var gap = new LibraryElement<T>();
        for (var i = 0; i < range; i++)
        {
            aux.Write(i, gap);
        }

        // Redistribute: pos[i] = floor(i * range / count)
        // This guarantees no collisions since range >= count
        posCount = 0;
        var maxUsedPos = 0;
        for (var i = 0; i < count; i++)
        {
            var pos = (int)((long)i * range / count);

            // Defensive check (should never happen with range >= count)
            if (pos >= auxSize)
            {
                throw new InvalidOperationException(
                    $"Position overflow in rebalance: calculated pos={pos}, but auxSize={auxSize} " +
                    $"(i={i}, count={count}, range={range})");
            }

            aux.Write(pos, new LibraryElement<T>(tempBuffer[i]));
            positions[posCount++] = pos;
            maxUsedPos = Math.Max(maxUsedPos, pos);
        }

        // Verify all elements were placed
        if (posCount != count)
        {
            throw new InvalidOperationException(
                $"Data loss detected in rebalance: expected {count} elements, but only placed {posCount}");
        }

        // Return the maximum used position + 1
        // This represents the true auxEnd after rebalancing
        return maxUsedPos + 1;
    }

    /// <summary>
    /// Wrapper struct for gaps vs elements.
    /// </summary>
    private readonly struct LibraryElement<T>
    {
        public readonly T Value;
        public readonly bool HasValue;

        public LibraryElement(T value)
        {
            Value = value;
            HasValue = true;
        }
    }

    /// <summary>
    /// Comparer for <see cref="LibraryElement{T}"/> that delegates to the underlying <typeparamref name="TComparer"/>.
    /// Gap elements are ordered before non-gap elements.
    /// </summary>
    private readonly struct LibraryElementComparer<T, TComparer> : IComparer<LibraryElement<T>> where TComparer : IComparer<T>
    {
        private readonly TComparer _comparer;

        public LibraryElementComparer(TComparer comparer) => _comparer = comparer;

        public int Compare(LibraryElement<T> x, LibraryElement<T> y)
        {
            if (!x.HasValue && !y.HasValue) return 0;
            if (!x.HasValue) return -1;
            if (!y.HasValue) return 1;
            return _comparer.Compare(x.Value, y.Value);
        }
    }
}

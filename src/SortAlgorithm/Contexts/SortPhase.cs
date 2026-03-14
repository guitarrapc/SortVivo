namespace SortAlgorithm.Contexts;

/// <summary>
/// Enumeration representing the current phase of the algorithm.
/// Provides type information for the tutorial side to construct phase text.
/// </summary>
/// <remarks>
/// Parameter meanings for each phase:
/// <list type="table">
///   <listheader><term>Phase</term><description>param1 / param2 / param3</description></listheader>
///   <item><term>BubblePass</term><description>pass (current pass number) / totalPasses (total passes) / boundary (right boundary position)</description></item>
///   <item><term>SelectionFindMin</term><description>i (sorted boundary) / last (last index)</description></item>
///   <item><term>DoubleSelectionFindMinMax</term><description>left (left boundary) / right (right boundary)</description></item>
///   <item><term>CycleSortCycle</term><description>cycleStart (cycle start index) / last (last index)</description></item>
///   <item><term>PancakeFindMax</term><description>first (head index) / currentSize (current subarray last index, inclusive)</description></item>
///   <item><term>CocktailForwardPass</term><description>pass (current pass number) / min (left boundary) / max (right boundary)</description></item>
///   <item><term>CocktailBackwardPass</term><description>pass (current pass number) / min (left boundary) / max (right boundary)</description></item>
///   <item><term>CombGapPass</term><description>gap (current gap value) / len (array length)</description></item>
///   <item><term>CombBubblePass</term><description>bubbleEnd (right boundary position of bubble phase)</description></item>
///   <item><term>OddEvenOddPhase</term><description>pass (current pass number)</description></item>
///   <item><term>OddEvenEvenPhase</term><description>pass (current pass number)</description></item>
///   <item><term>QuickSortPartition</term><description>left / right / pivotIndex</description></item>
///   <item><term>HybridToInsertionSort</term><description>left (inclusive) / right (inclusive) / threshold</description></item>
///   <item><term>HybridToHeapSort</term><description>left (inclusive) / right (inclusive)</description></item>
///   <item><term>PDQPartialInsertionSort</term><description>begin (inclusive) / end-1 (inclusive)</description></item>
///   <item><term>PDQPatternShuffle</term><description>begin (inclusive) / end-1 (inclusive) / badAllowed remaining</description></item>
///   <item><term>OddEvenMergeSortPass</term><description>p (merge group size, power of 2) / count</description></item>
///   <item><term>OddEvenMergeSortStage</term><description>k (comparison distance within merge) / p (current merge group size) / count</description></item>
/// </list>
/// </remarks>
public enum SortPhase
{
    /// <summary>Phase not set (hides tutorial phase bar)</summary>
    None = 0,

    // Exchange family

    /// <summary>
    /// Bubble Sort pass.
    /// param1=pass (current pass number, 1-based), param2=totalPasses (total passes), param3=boundary (right boundary position)
    /// </summary>
    BubblePass,

    /// <summary>
    /// Cocktail Shaker Sort forward pass (left to right, moving maximum to the right).
    /// param1=pass (current pass number, 1-based), param2=min (left boundary), param3=max (right boundary)
    /// </summary>
    CocktailForwardPass,

    /// <summary>
    /// Cocktail Shaker Sort backward pass (right to left, moving minimum to the left).
    /// param1=pass (current pass number, 1-based), param2=min (left boundary), param3=max (right boundary)
    /// </summary>
    CocktailBackwardPass,

    /// <summary>
    /// Comb Sort gap pass.
    /// param1=gap (current gap value), param2=len (array length)
    /// </summary>
    CombGapPass,

    /// <summary>
    /// Comb Sort bubble phase (gap=1).
    /// param1=bubbleEnd (right boundary position of bubble phase)
    /// </summary>
    CombBubblePass,

    /// <summary>
    /// Odd-Even Sort odd-even phase (pairs starting at even indices: (0,1), (2,3), ...).
    /// param1=pass (current pass number, 1-based)
    /// </summary>
    OddEvenOddPhase,

    /// <summary>
    /// Odd-Even Sort even-odd phase (pairs starting at odd indices: (1,2), (3,4), ...).
    /// param1=pass (current pass number, 1-based)
    /// </summary>
    OddEvenEvenPhase,

    // Selection family

    /// <summary>
    /// Selection Sort minimum value search.
    /// param1=i (sorted boundary index), param2=last (search end index, inclusive)
    /// </summary>
    SelectionFindMin,

    /// <summary>
    /// Double Selection Sort simultaneous min/max search.
    /// param1=left (left boundary index), param2=right (right boundary index)
    /// </summary>
    DoubleSelectionFindMinMax,

    /// <summary>
    /// Cycle Sort cycle processing.
    /// param1=cycleStart (cycle start index), param2=last (array last index)
    /// </summary>
    CycleSortCycle,

    /// <summary>
    /// Pancake Sort max element search.
    /// param1=first (head index), param2=currentSize-1 (current subarray last index, inclusive)
    /// </summary>
    PancakeFindMax,

    // Insertion family

    /// <summary>
    /// Insertion Sort pass: inserting element at position i into sorted region.
    /// param1=i (element being inserted), param2=first, param3=last-1
    /// </summary>
    InsertionPass,

    /// <summary>
    /// Binary Insertion Sort pass: binary-search insertion of element at position i.
    /// param1=i (element being inserted), param2=first, param3=last-1
    /// </summary>
    BinaryInsertionPass,

    /// <summary>
    /// Gnome Sort current position check.
    /// param1=i (current position), param2=last-1
    /// </summary>
    GnomePass,

    /// <summary>
    /// Shell Sort gap pass (h-insertion sort with gap h).
    /// param1=gap (current gap value), param2=gapIndex+1 (1-based), param3=totalGaps
    /// </summary>
    ShellGapPass,

    /// <summary>
    /// Pair Insertion Sort pair processing.
    /// param1=i (first index of the pair), param2=last-1
    /// </summary>
    PairInsertionPass,

    /// <summary>
    /// Merge Insertion Sort (Ford-Johnson): pairing elements and comparing each pair.
    /// param1=0, param2=pairs-1 (number of pairs)
    /// </summary>
    MergeInsertionPairing,

    /// <summary>
    /// Merge Insertion Sort (Ford-Johnson): sorting the larger elements from each pair.
    /// param1=0, param2=pairs-1
    /// </summary>
    MergeInsertionSortLarger,

    /// <summary>
    /// Merge Insertion Sort (Ford-Johnson): inserting pended elements using Jacobsthal sequence.
    /// param1=0, param2=pendCount-1
    /// </summary>
    MergeInsertionInsertPend,

    /// <summary>
    /// Merge Insertion Sort (Ford-Johnson): rearranging the original array based on sorted indices.
    /// param1=0, param2=n-1
    /// </summary>
    MergeInsertionRearrange,

    /// <summary>
    /// Library Sort phase marker.
    /// param1=phase (1=initial sort, 2=insert remaining, 3=extract)
    /// </summary>
    LibrarySortPhase,

    /// <summary>
    /// Patience Sort pile-building (dealing) phase: distributing elements into sorted piles.
    /// No parameters.
    /// </summary>
    PatienceSortDeal,

    /// <summary>
    /// Patience Sort k-way merge phase: merging all piles using a min-heap.
    /// param1=pileCount (total number of piles to merge)
    /// </summary>
    PatienceSortMerge,

    /// <summary>
    /// Strand Sort extract phase: scanning remaining elements and pulling out a sorted strand.
    /// param1=strandPass (1-based pass number), param2=remainingCount (elements before extraction)
    /// </summary>
    StrandSortExtract,

    /// <summary>
    /// Strand Sort merge phase: merging the extracted strand into the accumulated sorted result.
    /// param1=strandPass (1-based pass number), param2=strandLen, param3=resultLen
    /// </summary>
    StrandSortMerge,

    // Tree family

    /// <summary>
    /// Tree Sort insertion phase: inserting element at index i into the BST.
    /// param1=i (index of element being inserted), param2=last (array last index)
    /// </summary>
    TreeSortInsert,

    /// <summary>
    /// Tree Sort extraction phase: in-order traversal writing sorted elements back.
    /// No parameters used.
    /// </summary>
    TreeSortExtract,

    // Heap family

    /// <summary>
    /// Heap Sort build phase: constructing the max-heap from the input array.
    /// param1=first (start index), param2=last-1 (end index)
    /// </summary>
    HeapBuild,

    /// <summary>
    /// Heap Sort extract phase: extracting the max element and shrinking the heap.
    /// param1=current extraction step (1-based), param2=total extractions (n-1)
    /// </summary>
    HeapExtract,

    // Merge family

    /// <summary>
    /// Merge Sort merge step: merging two sorted halves [left..mid] and [mid+1..right].
    /// param1=left (inclusive start), param2=mid (inclusive end of left half), param3=right (inclusive end)
    /// </summary>
    MergeSortMerge,

    /// <summary>
    /// Bottom-up merge pass: one sweep merging pairs of runs with a given width.
    /// param1=width (run size being merged), param2=pass number (1-based)
    /// </summary>
    MergePass,

    /// <summary>
    /// Initial insertion sort phase used by hybrid merge sorts (e.g., RotateMerge, SymMerge).
    /// param1=blockSize (insertion sort threshold)
    /// </summary>
    MergeInitSort,

    /// <summary>
    /// Natural run detection phase (ShiftSort, TimSort, PowerSort).
    /// No parameters.
    /// </summary>
    MergeRunDetect,

    /// <summary>
    /// Final run collapse/merge phase (TimSort MergeForceCollapse, PowerSort final flush).
    /// param1=remaining run count on the stack
    /// </summary>
    MergeRunCollapse,

    // Adaptive family

    /// <summary>
    /// Drop Merge Sort: LNS (Longest Nondecreasing Subsequence) detection scan.
    /// No parameters.
    /// </summary>
    DropMergeDetect,

    /// <summary>
    /// Drop Merge Sort: sorting the dropped elements.
    /// param1=droppedCount
    /// </summary>
    DropMergeSort,

    /// <summary>
    /// Drop Merge Sort: merging the LNS and sorted dropped elements back.
    /// param1=droppedCount, param2=total length
    /// </summary>
    DropMergeMerge,

    // Network family

    /// <summary>
    /// Bitonic Sort level: building a bitonic sequence of size k.
    /// param1=k (bitonic sequence size), param2=count
    /// </summary>
    BitonicLevel,

    /// <summary>
    /// Bitonic Sort stage: comparison-swap pass at distance j within level k.
    /// param1=j (comparison distance), param2=k (current level), param3=count
    /// </summary>
    BitonicStage,

    /// <summary>
    /// Batcher Odd-Even Merge Sort merge pass: merging blocks of size p.
    /// param1=p (merge group size, power of 2), param2=count
    /// </summary>
    OddEvenMergeSortPass,

    /// <summary>
    /// Batcher Odd-Even Merge Sort stage: comparison-swap pass at distance k within merge group p.
    /// param1=k (comparison distance), param2=p (current merge group size), param3=count
    /// </summary>
    OddEvenMergeSortStage,

    // Joke family

    /// <summary>
    /// Bogo Sort shuffle attempt.
    /// param1=attempt number (1-based)
    /// </summary>
    BogoShuffle,

    /// <summary>
    /// Slow Sort: settling the maximum of [start..end] at position end.
    /// param1=start (subrange start), param2=end (subrange end, receives the settled max)
    /// </summary>
    SlowSortSettle,

    /// <summary>
    /// Stooge Sort: one of the three recursive 2/3-passes.
    /// param1=start, param2=end (of the current sub-pass range), param3=pass (1=first 2/3, 2=last 2/3, 3=repeat first 2/3)
    /// </summary>
    StoogeSortPass,

    // Distribution family

    /// <summary>
    /// Radix Sort pass over one digit position.
    /// param1=digit (0-based, LSD: 0=least significant), param2=totalDigits
    /// </summary>
    RadixPass,

    /// <summary>
    /// Distribution Sort counting phase (count occurrences / build count array).
    /// No parameters.
    /// </summary>
    DistributionCount,

    /// <summary>
    /// Distribution Sort accumulate phase (prefix sum / compute offsets).
    /// No parameters.
    /// </summary>
    DistributionAccumulate,

    /// <summary>
    /// Distribution Sort write/collect phase (scatter to output / collect buckets).
    /// No parameters.
    /// </summary>
    DistributionWrite,

    // Partition family

    /// <summary>
    /// Quick Sort partition step: partitioning [left..right] around a pivot.
    /// param1=left, param2=right, param3=pivotIndex
    /// </summary>
    QuickSortPartition,

    /// <summary>
    /// Hybrid sort (IntroSort / PDQSort / StdSort / BlockQuickSort) switching to InsertionSort
    /// because the partition size is at or below the threshold.
    /// param1=left (inclusive), param2=right (inclusive), param3=threshold
    /// </summary>
    HybridToInsertionSort,

    /// <summary>
    /// Hybrid sort switching to HeapSort because the recursion depth limit (or bad-partition
    /// counter for PDQSort) has been exceeded, guaranteeing O(n log n) worst-case.
    /// param1=left (inclusive), param2=right (inclusive)
    /// </summary>
    HybridToHeapSort,

    /// <summary>
    /// PDQSort: the partition appears already sorted; attempting PartialInsertionSort
    /// (up to PartialInsertionSortLimit moves) before deciding whether to recurse.
    /// param1=begin (inclusive), param2=end-1 (inclusive)
    /// </summary>
    PDQPartialInsertionSort,

    /// <summary>
    /// PDQSort: a highly unbalanced partition was detected; shuffling elements to break
    /// adversarial patterns before re-partitioning.
    /// param1=begin (inclusive), param2=end-1 (inclusive), param3=badAllowed remaining
    /// </summary>
    PDQPatternShuffle,
}

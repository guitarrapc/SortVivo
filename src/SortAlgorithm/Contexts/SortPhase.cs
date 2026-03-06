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
/// </list>
/// </remarks>
public enum SortPhase
{
    /// <summary>Phase not set (hides tutorial phase bar)</summary>
    None = 0,

    /// <summary>
    /// Bubble Sort pass.
    /// param1=pass (current pass number, 1-based), param2=totalPasses (total passes), param3=boundary (right boundary position)
    /// </summary>
    BubblePass,

    /// <summary>
    /// Selection Sort minimum value search.
    /// param1=i (sorted boundary index), param2=last (search end index, inclusive)
    /// </summary>
    SelectionFindMin,

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

    // ── Insertion family ────────────────────────────────────────────────────

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
    /// Library Sort phase marker.
    /// param1=phase (1=initial sort, 2=insert remaining, 3=extract)
    /// </summary>
    LibrarySortPhase,
}

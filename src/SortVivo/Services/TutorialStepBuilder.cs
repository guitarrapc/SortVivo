using SortVivo.Models;

namespace SortVivo.Services;

/// <summary>
/// Builder that converts a list of SortOperations into a list of TutorialSteps.
/// Generates array snapshots, highlight info, and narrative text for each operation.
/// </summary>
public static class TutorialStepBuilder
{
    /// <summary>
    /// Builds a list of TutorialSteps from the initial array and a list of operations.
    /// Detects insertion patterns (Read + shift Writes + insert Write) and groups them
    /// into a single logical step for clarity.
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations)
        => Build(initialArray, operations, TutorialVisualizationHint.None);

    /// <summary>
    /// Builds a list of TutorialSteps with optional visualization hint support.
    /// When <paramref name="hint"/> is <see cref="TutorialVisualizationHint.HeapTree"/>,
    /// tracks the heap boundary for heap tree rendering.
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations, TutorialVisualizationHint hint)
        => Build(initialArray, operations, hint, lsdRadix: 0);

    /// <summary>
    /// Builds a list of TutorialSteps with optional visualization hint and LSD radix support.
    /// <paramref name="lsdRadix"/> is used when <paramref name="hint"/> is
    /// <see cref="TutorialVisualizationHint.DigitBucketLsd"/> to select bucket digit computation:
    /// 10 = decimal (b=10), 4 = 2-bit groups (b=4).
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations, TutorialVisualizationHint hint, int lsdRadix)
    {
        var steps = new List<TutorialStep>(operations.Count);
        var mainArray = (int[])initialArray.Clone();
        var bufferArrays = InitializeBufferArrays(initialArray.Length, operations);
        var tracker = CreateTracker(hint, lsdRadix, initialArray);

        // Phase / Role 伝播用の状態
        string currentPhase = string.Empty;
        var currentRoles = new Dictionary<int, SortAlgorithm.Contexts.RoleType>();

        int opIdx = 0;
        while (opIdx < operations.Count)
        {
            var op = operations[opIdx];

            // Phase / RoleAssign はステップを生成せず内部状態を更新して次へ進む
            if (op.Type == OperationType.Phase)
            {
                var phaseText = BuildPhaseText(op.PhaseKind, op.Index1, op.Index2, op.Length);
                // 空文字のフェーズテキストは currentPhase を上書きしない
                // (FlashSortClassBoundary 等、表示テキストを持たない内部通知フェーズ向け)
                if (!string.IsNullOrEmpty(phaseText))
                    currentPhase = phaseText;
                tracker.ProcessPhase(op.PhaseKind, op.Index1, op.Index2, op.Length, mainArray);
                opIdx++;
                continue;
            }
            if (op.Type == OperationType.RoleAssign && op.RoleValue.HasValue)
            {
                if (op.RoleValue.Value == SortAlgorithm.Contexts.RoleType.None)
                    currentRoles.Remove(op.Index1);
                else
                    currentRoles[op.Index1] = op.RoleValue.Value;
                opIdx++;
                continue;
            }

            int groupEnd = TryDetectInsertionGroup(operations, opIdx, mainArray);

            if (groupEnd > opIdx)
            {
                // Grouped insertion step: Read + shifts + insert → 1 logical step
                var step = BuildGroupedInsertionStep(operations, opIdx, groupEnd, mainArray, bufferArrays);
                var decorated = tracker.Decorate(step);
                steps.Add(decorated with { Phase = currentPhase, Roles = new Dictionary<int, SortAlgorithm.Contexts.RoleType>(currentRoles) });
                opIdx = groupEnd + 1;
            }
            else
            {
                // Individual step

                // 1. Process: tracker が内部状態を更新する (ApplyOperation 前)
                tracker.Process(op, mainArray, bufferArrays);

                // 2. Generate base step info
                var (highlights, bufferHighlights, highlightType, compareResult, writeSourceIndex, writePreviousValue, narrative) =
                    GenerateStepInfo(op, mainArray, bufferArrays);

                // 3. Apply operation to arrays
                ApplyOperation(op, mainArray, bufferArrays);

                // 4. Build base step
                var baseStep = new TutorialStep
                {
                    OperationIndex = opIdx,
                    ArraySnapshot = (int[])mainArray.Clone(),
                    BufferSnapshots = bufferArrays.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone()),
                    HighlightIndices = highlights,
                    BufferHighlightIndices = bufferHighlights,
                    HighlightType = highlightType,
                    CompareResult = compareResult,
                    WriteSourceIndex = writeSourceIndex,
                    WritePreviousValue = writePreviousValue,
                    Narrative = narrative,
                    Phase = currentPhase,
                    Roles = new Dictionary<int, SortAlgorithm.Contexts.RoleType>(currentRoles),
                };

                // 5. Decorate: tracker がビジュアライゼーション固有フィールドを追加
                steps.Add(tracker.Decorate(baseStep));

                // 6. PostStep: 後処理 (LSD バケットクリア等)
                tracker.PostStep();

                opIdx++;
            }
        }

        return steps;
    }

    // Tracker factory

    /// <summary>
    /// TutorialVisualizationHint に対応する IVisualizationTracker を生成する。
    /// </summary>
    private static IVisualizationTracker CreateTracker(
        TutorialVisualizationHint hint, int lsdRadix, int[] initialArray)
        => hint switch
        {
            TutorialVisualizationHint.HeapTree
            or TutorialVisualizationHint.TernaryHeapTree
            or TutorialVisualizationHint.WeakHeapTree => new HeapTracker(hint, initialArray.Length),
            TutorialVisualizationHint.BstTree => new BstTracker(initialArray.Length, avl: false),
            TutorialVisualizationHint.AvlTree => new BstTracker(initialArray.Length, avl: true),
            TutorialVisualizationHint.ValueBucket => new DistributionTracker(initialArray),
            TutorialVisualizationHint.FlashSortClasses => new FlashSortTracker(initialArray),
            TutorialVisualizationHint.MergeInsertionPairs => new MergeInsertionTracker(initialArray.Length),
            TutorialVisualizationHint.DigitBucketLsd => new LsdRadixTracker(initialArray, lsdRadix),
            TutorialVisualizationHint.DigitBucketMsd => new MsdRadixTracker(initialArray, lsdRadix),
            TutorialVisualizationHint.SortingNetwork => new NetworkTracker(initialArray.Length),
            TutorialVisualizationHint.RecursionTree => new RecursionTracker(initialArray.Length),
            TutorialVisualizationHint.ShellGap => new ShellGapTracker(),
            TutorialVisualizationHint.PatiencePiles => new PatiencePilesTracker(initialArray),
            _ => NullTracker.Instance,
        };

    // Insertion group detection

    /// <summary>
    /// Detects an insertion group pattern starting at <paramref name="startIdx"/>:
    /// IndexRead followed by Compare/IndexWrite operations, ending with an IndexWrite
    /// that writes the same value as the initial Read to a different position.
    /// Returns the last operation index of the group, or <paramref name="startIdx"/> if no group.
    /// </summary>
    private static int TryDetectInsertionGroup(List<SortOperation> operations, int startIdx, int[] mainArray)
    {
        var firstOp = operations[startIdx];

        // Must start with IndexRead on main array
        if (firstOp.Type != OperationType.IndexRead || firstOp.BufferId1 != 0
            || firstOp.Index1 < 0 || firstOp.Index1 >= mainArray.Length)
            return startIdx;

        int readValue = mainArray[firstOp.Index1];

        for (int i = startIdx + 1; i < operations.Count; i++)
        {
            var op = operations[i];

            // Found the final insertion write?
            if (op.Type == OperationType.IndexWrite && op.BufferId1 == 0
                && op.Value == readValue && op.Index1 != firstOp.Index1)
            {
                // Need at least: Read + 1 intermediate + Insert = 3 operations
                return i - startIdx >= 2 ? i : startIdx;
            }

            // Allow Compare and IndexWrite (shift) in between
            if (op.Type is not (OperationType.Compare or OperationType.IndexWrite))
                break;

            // Only main array writes
            if (op.Type == OperationType.IndexWrite && op.BufferId1 != 0)
                break;
        }

        return startIdx;
    }

    /// <summary>
    /// Builds a single TutorialStep for a grouped insertion operation.
    /// Applies all operations in [startIdx..endIdx] to mainArray and creates a snapshot.
    /// </summary>
    private static TutorialStep BuildGroupedInsertionStep(
        List<SortOperation> operations, int startIdx, int endIdx,
        int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        var readOp = operations[startIdx];
        var insertOp = operations[endIdx];
        int readValue = mainArray[readOp.Index1];
        int sourceIndex = readOp.Index1;
        int destIndex = insertOp.Index1;

        // Count intermediate shift writes
        int shiftCount = 0;
        for (int i = startIdx + 1; i < endIdx; i++)
        {
            if (operations[i].Type == OperationType.IndexWrite)
                shiftCount++;
        }

        string narrative = $"Insert value {readValue}: move from index {sourceIndex} to index {destIndex}";
        if (shiftCount > 0)
            narrative += $" (shifting {shiftCount} element{(shiftCount != 1 ? "s" : "")} right)";

        // Apply all operations in the group to advance main state
        for (int i = startIdx; i <= endIdx; i++)
            ApplyOperation(operations[i], mainArray, bufferArrays);

        return new TutorialStep
        {
            OperationIndex = endIdx,
            ArraySnapshot = (int[])mainArray.Clone(),
            BufferSnapshots = bufferArrays.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone()),
            HighlightIndices = [destIndex],
            BufferHighlightIndices = new Dictionary<int, int[]>(),
            HighlightType = OperationType.IndexWrite,
            WriteSourceIndex = sourceIndex,
            Narrative = narrative
        };
    }

    // Buffer initialization

    private static Dictionary<int, int[]> InitializeBufferArrays(int mainArrayLength, List<SortOperation> operations)
    {
        var maxSizes = new Dictionary<int, int>();

        foreach (var op in operations)
        {
            // Phase / RoleAssign はバッファーサイズ計算の対象外
            if (op.Type is OperationType.Phase or OperationType.RoleAssign)
                continue;

            if (op.BufferId1 > 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index1 + op.Length
                    : op.Index1 + 1;
                maxSizes[op.BufferId1] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId1), size);
            }

            if (op.BufferId2 > 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index2 + op.Length
                    : op.Index2 + 1;
                maxSizes[op.BufferId2] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId2), size);
            }
        }

        // Ensure buffers are at least as large as the main array to prevent index out-of-range
        return maxSizes.ToDictionary(
            kv => kv.Key,
            kv => new int[Math.Max(kv.Value, mainArrayLength)]);
    }

    // Phase text assembly

    /// <summary>
    /// SortPhase 種別と数値パラメータからチュートリアル表示用のフェーズテキストを組み立てる。
    /// 文字列アロケーションはここ（チュートリアル側）でのみ発生させる。
    /// </summary>
    private static string BuildPhaseText(SortAlgorithm.Contexts.SortPhase phase, int p1, int p2, int p3)
        => phase switch
        {
            SortAlgorithm.Contexts.SortPhase.BubblePass => $"Pass {p1}/{p2}: bubbling max to position {p3}",
            SortAlgorithm.Contexts.SortPhase.SelectionFindMin => $"Find minimum in [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.CocktailForwardPass => $"Pass {p1} forward →: bubbling max through [{p2}..{p3}]",
            SortAlgorithm.Contexts.SortPhase.CocktailBackwardPass => $"Pass {p1} backward ←: bubbling min through [{p2}..{p3}]",
            SortAlgorithm.Contexts.SortPhase.CombGapPass => $"Gap {p1}: comparing elements {p1} apart ({p2} total)",
            SortAlgorithm.Contexts.SortPhase.CombBubblePass => $"Bubble phase (gap=1): range [0..{p1}]",
            SortAlgorithm.Contexts.SortPhase.OddEvenOddPhase => $"Pass {p1} odd-even: pairs (0,1), (2,3), ...",
            SortAlgorithm.Contexts.SortPhase.OddEvenEvenPhase => $"Pass {p1} even-odd: pairs (1,2), (3,4), ...",
            SortAlgorithm.Contexts.SortPhase.DoubleSelectionFindMinMax => $"Find min & max in [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.CycleSortCycle => $"Cycle from index {p1} (range [0..{p2}])",
            SortAlgorithm.Contexts.SortPhase.PancakeFindMax => $"Find max in [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.InsertionPass => $"Inserting [{p1}] into sorted [{p2}..{p1 - 1}]",
            SortAlgorithm.Contexts.SortPhase.BinaryInsertionPass => $"Binary inserting [{p1}] into sorted [{p2}..{p1 - 1}]",
            SortAlgorithm.Contexts.SortPhase.GnomePass => $"Gnome at position {p1} / {p2}",
            SortAlgorithm.Contexts.SortPhase.ShellGapPass => $"Gap {p1}: pass {p2}/{p3} (h-insertion sort)",
            SortAlgorithm.Contexts.SortPhase.PairInsertionPass => $"Inserting pair at [{p1}], [{p1 + 1}] into sorted region",
            SortAlgorithm.Contexts.SortPhase.MergeInsertionPairing => $"Pairing: {p2 + 1} pair{(p2 != 0 ? "s" : "")} — compare each, mark larger/smaller",
            SortAlgorithm.Contexts.SortPhase.MergeInsertionSortLarger => $"Sort larger: recursively sorting {p2 + 1} larger element{(p2 != 0 ? "s" : "")}",
            SortAlgorithm.Contexts.SortPhase.MergeInsertionInsertPend => $"Insert pended: {p2 + 1} element{(p2 != 0 ? "s" : "")} in Jacobsthal order",
            SortAlgorithm.Contexts.SortPhase.MergeInsertionRearrange => $"Rearrange: writing {p2 + 1} element{(p2 != 0 ? "s" : "")} to final positions",
            SortAlgorithm.Contexts.SortPhase.LibrarySortPhase => p1 switch
            {
                1 => "Phase 1: initial sort (insertion sort on first block)",
                2 => "Phase 2: insert remaining elements with gaps",
                3 => "Phase 3: extract sorted elements from auxiliary array",
                _ => string.Empty,
            },
            SortAlgorithm.Contexts.SortPhase.TreeSortInsert => $"Insert [{p1}] into BST (element {p1 + 1}/{p2 + 1})",
            SortAlgorithm.Contexts.SortPhase.TreeSortExtract => "Extract: in-order traversal → write sorted array",
            SortAlgorithm.Contexts.SortPhase.HeapBuild => $"Build max-heap: [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.HeapExtract => $"Extract max ({p1}/{p2}): move root to sorted region",
            SortAlgorithm.Contexts.SortPhase.MergeSortMerge => $"Merge [{p1}..{p2}] + [{p2 + 1}..{p3}]",
            SortAlgorithm.Contexts.SortPhase.MergePass => $"Merge pass {p2}: width {p1} (merging pairs of {p1})",
            SortAlgorithm.Contexts.SortPhase.MergeInitSort => $"Initial sort: insertion sort in blocks of {p1}",
            SortAlgorithm.Contexts.SortPhase.MergeRunDetect => "Detecting natural runs",
            SortAlgorithm.Contexts.SortPhase.MergeRunCollapse => $"Collapsing {p1} run(s) on stack",
            SortAlgorithm.Contexts.SortPhase.DropMergeDetect => "Detecting LNS (Longest Nondecreasing Subsequence)",
            SortAlgorithm.Contexts.SortPhase.DropMergeSort => $"Sorting {p1} dropped element(s)",
            SortAlgorithm.Contexts.SortPhase.DropMergeMerge => $"Merging LNS with {p1} dropped element(s) into [{0}..{p2 - 1}]",
            SortAlgorithm.Contexts.SortPhase.StrandSortExtract => $"Pass {p1} extract: scanning {p2} remaining element(s)",
            SortAlgorithm.Contexts.SortPhase.StrandSortMerge => $"Pass {p1} merge: strand({p2}) + result({p3})",
            SortAlgorithm.Contexts.SortPhase.BitonicLevel => $"Level k={p1}: building bitonic sequence of size {p1} (count={p2})",
            SortAlgorithm.Contexts.SortPhase.BitonicStage => $"Stage j={p1}: compare-swap at distance {p1} within level {p2}",
            SortAlgorithm.Contexts.SortPhase.BogoShuffle => $"Shuffle attempt #{p1}",
            SortAlgorithm.Contexts.SortPhase.SlowSortSettle => $"Settle max of [{p1}..{p2}] at position {p2}",
            SortAlgorithm.Contexts.SortPhase.StoogeSortPass => p3 switch
            {
                1 => $"Pass 1/3: sort first 2/3 [{p1}..{p2}]",
                2 => $"Pass 2/3: sort last 2/3 [{p1}..{p2}]",
                3 => $"Pass 3/3: sort first 2/3 again [{p1}..{p2}]",
                _ => string.Empty,
            },
            SortAlgorithm.Contexts.SortPhase.RadixPass => $"Radix pass: digit {p1} (0=least significant)",
            SortAlgorithm.Contexts.SortPhase.DistributionCount => "Count: counting element occurrences",
            SortAlgorithm.Contexts.SortPhase.DistributionAccumulate => "Accumulate: computing bucket offsets (prefix sum)",
            SortAlgorithm.Contexts.SortPhase.DistributionWrite => "Write: scattering elements to sorted positions",
            SortAlgorithm.Contexts.SortPhase.QuickSortPartition => p3 >= 0
                ? $"Partition [{p1}..{p2}] — pivot at [{p3}]"
                : $"Partition [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.HybridToInsertionSort => $"Size {p2 - p1 + 1} ≤ {p3} → switch to InsertionSort [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.HybridToHeapSort => $"Depth limit exceeded → switch to HeapSort [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.PDQPartialInsertionSort => $"Already partitioned → try PartialInsertionSort [{p1}..{p2}]",
            SortAlgorithm.Contexts.SortPhase.PDQPatternShuffle => $"Unbalanced partition (bad={p3}) → shuffle to break pattern [{p1}..{p2}]",
            _ => string.Empty,
        };

    // Step info generation

    private static (int[] highlights, Dictionary<int, int[]> bufferHighlights, OperationType type, int? compareResult, int? writeSourceIndex, int? writePreviousValue, string narrative)
        GenerateStepInfo(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => op.Type switch
        {
            OperationType.Compare => BuildCompareInfo(op, mainArray, bufferArrays),
            OperationType.Swap => BuildSwapInfo(op, mainArray, bufferArrays),
            OperationType.IndexRead => BuildIndexReadInfo(op, mainArray, bufferArrays),
            OperationType.IndexWrite => BuildIndexWriteInfo(op, mainArray, bufferArrays),
            OperationType.RangeCopy => BuildRangeCopyInfo(op),
            _ => ([], new Dictionary<int, int[]>(), OperationType.Compare, null, null, null, string.Empty)
        };

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildCompareInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int vi = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        int vj = GetValue(op.BufferId2, op.Index2, mainArray, bufferArrays);
        string loc1 = FormatLocation(op.BufferId1, op.Index1);
        string loc2 = FormatLocation(op.BufferId2, op.Index2);

        string resultText = op.CompareResult > 0
            ? $"{vi} > {vj} → out of order, swap needed"
            : op.CompareResult < 0
                ? $"{vi} < {vj} → already in order"
                : $"{vi} = {vj} → equal, no swap needed";

        string narrative = $"Compare {loc1} ({vi}) and {loc2} ({vj}): {resultText}";

        int[] highlights = op.BufferId1 == 0 && op.BufferId2 == 0
            ? [op.Index1, op.Index2]
            : op.BufferId1 == 0 ? [op.Index1] : [];

        var bufHighlights = new Dictionary<int, int[]>();
        AddBufferHighlight(bufHighlights, op.BufferId1, op.Index1);
        AddBufferHighlight(bufHighlights, op.BufferId2, op.Index2);

        return (highlights, bufHighlights, OperationType.Compare, op.CompareResult, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildSwapInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int vi = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        int vj = GetValue(op.BufferId1, op.Index2, mainArray, bufferArrays);

        string narrative = $"Swap value {vi} at index {op.Index1} with value {vj} at index {op.Index2}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1, op.Index2] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1, op.Index2];

        return (highlights, bufHighlights, OperationType.Swap, null, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildIndexReadInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int v = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string narrative = $"Read value {v} from {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexRead, null, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildIndexWriteInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string valStr = op.Value.HasValue ? op.Value.Value.ToString() : "?";

        // Compute previous value at write destination
        int[] destArr = GetArray(op.BufferId1, mainArray, bufferArrays);
        int? previousValue = op.Index1 >= 0 && op.Index1 < destArr.Length ? destArr[op.Index1] : null;

        // Find source: where was the written value in the same array before this write?
        int? sourceIndex = null;
        if (op.Value.HasValue && op.BufferId1 == 0)
        {
            int writeVal = op.Value.Value;
            for (int k = 0; k < mainArray.Length; k++)
            {
                if (k != op.Index1 && mainArray[k] == writeVal)
                {
                    sourceIndex = k;
                    break;
                }
            }
        }

        string narrative = sourceIndex.HasValue
            ? $"Write value {valStr} from index {sourceIndex.Value} to {loc}"
            : $"Write value {valStr} to {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexWrite, null, sourceIndex, previousValue, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildRangeCopyInfo(SortOperation op)
    {
        string srcName = op.BufferId1 == 0 ? "main array" : $"buffer {op.BufferId1}";
        string dstName = op.BufferId2 == 0 ? "main array" : $"buffer {op.BufferId2}";
        int srcEnd = op.Index1 + op.Length - 1;

        string narrative = op.Length == 1
            ? $"Copy value at index {op.Index1} of {srcName} to index {op.Index2} of {dstName}"
            : $"Copy {op.Length} elements ({op.Index1}–{srcEnd}) from {srcName} to index {op.Index2} of {dstName}";

        // Highlight source side (read)
        int[] highlights = op.BufferId1 == 0
            ? Enumerable.Range(op.Index1, op.Length).ToArray()
            : [];

        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = Enumerable.Range(op.Index1, op.Length).ToArray();
        if (op.BufferId2 != 0)
        {
            var destRange = Enumerable.Range(op.Index2, op.Length).ToArray();
            if (bufHighlights.TryGetValue(op.BufferId2, out var existing))
                bufHighlights[op.BufferId2] = [.. existing, .. destRange];
            else
                bufHighlights[op.BufferId2] = destRange;
        }

        return (highlights, bufHighlights, OperationType.RangeCopy, null, null, null, narrative);
    }

    // Apply operation

    private static void ApplyOperation(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        switch (op.Type)
        {
            case OperationType.Compare:
            case OperationType.IndexRead:
                break;

            case OperationType.Swap:
                {
                    int[] arr = GetArray(op.BufferId1, mainArray, bufferArrays);
                    if (op.Index1 < arr.Length && op.Index2 < arr.Length)
                        (arr[op.Index1], arr[op.Index2]) = (arr[op.Index2], arr[op.Index1]);
                    break;
                }

            case OperationType.IndexWrite:
                {
                    if (op.Value.HasValue)
                    {
                        int[] arr = GetArray(op.BufferId1, mainArray, bufferArrays);
                        if (op.Index1 < arr.Length)
                            arr[op.Index1] = op.Value.Value;
                    }
                    break;
                }

            case OperationType.RangeCopy:
                {
                    int[] destArr = GetArray(op.BufferId2, mainArray, bufferArrays);

                    if (op.Values != null)
                    {
                        for (int k = 0; k < op.Length && k < op.Values.Length; k++)
                        {
                            int destIdx = op.Index2 + k;
                            if (destIdx < destArr.Length)
                                destArr[destIdx] = op.Values[k];
                        }
                    }
                    else
                    {
                        // Values is null: copy directly from the source array
                        int[] srcArr = GetArray(op.BufferId1, mainArray, bufferArrays);
                        for (int k = 0; k < op.Length; k++)
                        {
                            int srcIdx = op.Index1 + k;
                            int destIdx = op.Index2 + k;
                            if (srcIdx < srcArr.Length && destIdx < destArr.Length)
                                destArr[destIdx] = srcArr[srcIdx];
                        }
                    }
                    break;
                }
        }
    }

    // Helpers

    private static int[] GetArray(int bufferId, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => bufferId == 0 ? mainArray : bufferArrays.GetValueOrDefault(bufferId, mainArray);

    private static int GetValue(int bufferId, int index, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        if (index < 0) return 0;
        var arr = GetArray(bufferId, mainArray, bufferArrays);
        return index < arr.Length ? arr[index] : 0;
    }

    private static string FormatLocation(int bufferId, int index)
        => index < 0 ? "temp"
        : bufferId == 0 ? $"index {index}" : $"buffer index {index}";

    private static void AddBufferHighlight(Dictionary<int, int[]> dict, int bufferId, int index)
    {
        if (bufferId == 0) return;
        if (dict.TryGetValue(bufferId, out var existing))
            dict[bufferId] = [.. existing, index];
        else
            dict[bufferId] = [index];
    }
}

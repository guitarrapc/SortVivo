using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

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

        int opIdx = 0;
        while (opIdx < operations.Count)
        {
            int groupEnd = TryDetectInsertionGroup(operations, opIdx, mainArray);

            if (groupEnd > opIdx)
            {
                // Grouped insertion step: Read + shifts + insert → 1 logical step
                var step = BuildGroupedInsertionStep(operations, opIdx, groupEnd, mainArray, bufferArrays);
                steps.Add(tracker.Decorate(step));
                opIdx = groupEnd + 1;
            }
            else
            {
                // Individual step
                var op = operations[opIdx];

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

    // ─── Tracker factory ─────────────────────────────────────────

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
            TutorialVisualizationHint.BstTree         => new BstTracker(initialArray.Length, avl: false),
            TutorialVisualizationHint.AvlTree         => new BstTracker(initialArray.Length, avl: true),
            TutorialVisualizationHint.ValueBucket     => new DistributionTracker(initialArray),
            TutorialVisualizationHint.DigitBucketLsd  => new LsdRadixTracker(initialArray, lsdRadix),
            TutorialVisualizationHint.DigitBucketMsd  => new MsdRadixTracker(initialArray, lsdRadix),
            TutorialVisualizationHint.SortingNetwork  => new NetworkTracker(initialArray.Length),
            TutorialVisualizationHint.RecursionTree   => new RecursionTracker(initialArray.Length),
            TutorialVisualizationHint.ShellGap        => new ShellGapTracker(),
            _                                          => NullTracker.Instance,
        };

    // ─── Insertion group detection ──────────────────────────────────────────

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

    // ─── Buffer initialization ──────────────────────────────────────────────

    private static Dictionary<int, int[]> InitializeBufferArrays(int mainArrayLength, List<SortOperation> operations)
    {
        var maxSizes = new Dictionary<int, int>();

        foreach (var op in operations)
        {
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

    // ─── Step info generation ──────────────────────────────────────────────

    private static (int[] highlights, Dictionary<int, int[]> bufferHighlights, OperationType type, int? compareResult, int? writeSourceIndex, int? writePreviousValue, string narrative)
        GenerateStepInfo(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => op.Type switch
        {
            OperationType.Compare   => BuildCompareInfo(op, mainArray, bufferArrays),
            OperationType.Swap      => BuildSwapInfo(op, mainArray, bufferArrays),
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

    // ─── Apply operation ─────────────────────────────────────────────────

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

    // ─── Helpers ─────────────────────────────────────────────────────────

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

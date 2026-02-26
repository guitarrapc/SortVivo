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
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations)
    {
        var steps = new List<TutorialStep>(operations.Count);
        var mainArray = (int[])initialArray.Clone();
        var bufferArrays = InitializeBufferArrays(initialArray.Length, operations);

        for (int opIdx = 0; opIdx < operations.Count; opIdx++)
        {
            var op = operations[opIdx];

            // Generate narrative and highlights from values before applying the operation
            var (highlights, bufferHighlights, highlightType, narrative) =
                GenerateStepInfo(op, mainArray, bufferArrays);

            // Apply the operation to the array state
            ApplyOperation(op, mainArray, bufferArrays);

            // Save a snapshot after applying
            var snapshot = (int[])mainArray.Clone();
            var bufferSnapshots = bufferArrays.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone());

            steps.Add(new TutorialStep
            {
                OperationIndex = opIdx,
                ArraySnapshot = snapshot,
                BufferSnapshots = bufferSnapshots,
                HighlightIndices = highlights,
                BufferHighlightIndices = bufferHighlights,
                HighlightType = highlightType,
                Narrative = narrative
            });
        }

        return steps;
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

    private static (int[] highlights, Dictionary<int, int[]> bufferHighlights, OperationType type, string narrative)
        GenerateStepInfo(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => op.Type switch
        {
            OperationType.Compare   => BuildCompareInfo(op, mainArray, bufferArrays),
            OperationType.Swap      => BuildSwapInfo(op, mainArray, bufferArrays),
            OperationType.IndexRead => BuildIndexReadInfo(op, mainArray, bufferArrays),
            OperationType.IndexWrite => BuildIndexWriteInfo(op),
            OperationType.RangeCopy => BuildRangeCopyInfo(op),
            _ => ([], new Dictionary<int, int[]>(), OperationType.Compare, string.Empty)
        };

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildCompareInfo(
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

        return (highlights, bufHighlights, OperationType.Compare, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildSwapInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int vi = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        int vj = GetValue(op.BufferId1, op.Index2, mainArray, bufferArrays);

        string narrative = $"Swap value {vi} at index {op.Index1} with value {vj} at index {op.Index2}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1, op.Index2] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1, op.Index2];

        return (highlights, bufHighlights, OperationType.Swap, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildIndexReadInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int v = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string narrative = $"Read value {v} from {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexRead, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildIndexWriteInfo(SortOperation op)
    {
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string valStr = op.Value.HasValue ? op.Value.Value.ToString() : "?";
        string narrative = $"Write value {valStr} to {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexWrite, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildRangeCopyInfo(SortOperation op)
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

        return (highlights, bufHighlights, OperationType.RangeCopy, narrative);
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

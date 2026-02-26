using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// SortOperation のリストをチュートリアル用の TutorialStep リストへ変換するビルダー。
/// 操作ごとに配列スナップショット・ハイライト情報・日本語ナラティブを生成する。
/// </summary>
public static class TutorialStepBuilder
{
    /// <summary>
    /// 初期配列と操作リストから TutorialStep のリストを生成する。
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations)
    {
        var steps = new List<TutorialStep>(operations.Count);
        var mainArray = (int[])initialArray.Clone();
        var bufferArrays = InitializeBufferArrays(initialArray.Length, operations);

        for (int opIdx = 0; opIdx < operations.Count; opIdx++)
        {
            var op = operations[opIdx];

            // ナラティブとハイライトは操作適用前の値で生成する
            var (highlights, bufferHighlights, highlightType, narrative) =
                GenerateStepInfo(op, mainArray, bufferArrays);

            // 操作を配列状態に適用
            ApplyOperation(op, mainArray, bufferArrays);

            // 適用後のスナップショットを保存
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

    // ─── バッファー初期化 ──────────────────────────────────────────────────

    private static Dictionary<int, int[]> InitializeBufferArrays(int mainArrayLength, List<SortOperation> operations)
    {
        var maxSizes = new Dictionary<int, int>();

        foreach (var op in operations)
        {
            if (op.BufferId1 != 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index1 + op.Length
                    : op.Index1 + 1;
                maxSizes[op.BufferId1] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId1), size);
            }

            if (op.BufferId2 != 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index2 + op.Length
                    : op.Index2 + 1;
                maxSizes[op.BufferId2] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId2), size);
            }
        }

        // バッファーはメイン配列以上のサイズを確保してインデックス範囲外を防ぐ
        return maxSizes.ToDictionary(
            kv => kv.Key,
            kv => new int[Math.Max(kv.Value, mainArrayLength)]);
    }

    // ─── ステップ情報生成 ──────────────────────────────────────────────────

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
            ? $"{vi} > {vj} → 順序が逆なので入れ替えが必要"
            : op.CompareResult < 0
                ? $"{vi} < {vj} → 順序は正しい"
                : $"{vi} = {vj} → 同じ値なので順序は正しい";

        string narrative = $"{loc1} の値 {vi} と {loc2} の値 {vj} を比較: {resultText}";

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

        string narrative = $"位置 {op.Index1} の値 {vi} と 位置 {op.Index2} の値 {vj} を入れ替える";

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
        string narrative = $"{loc} の値 {v} を読み取る";

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
        string narrative = $"{loc} に値 {valStr} を書き込む";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexWrite, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, string) BuildRangeCopyInfo(SortOperation op)
    {
        string srcName = op.BufferId1 == 0 ? "メイン配列" : $"バッファ {op.BufferId1}";
        string dstName = op.BufferId2 == 0 ? "メイン配列" : $"バッファ {op.BufferId2}";
        int srcEnd = op.Index1 + op.Length - 1;

        string narrative = op.Length == 1
            ? $"{srcName} の位置 {op.Index1} の値を {dstName} の位置 {op.Index2} にコピーする"
            : $"{srcName} の位置 {op.Index1}〜{srcEnd} ({op.Length} 個) を {dstName} の位置 {op.Index2} からコピーする";

        // ソース側をハイライト（読み取り）
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

    // ─── 操作適用 ─────────────────────────────────────────────────────────

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
                    // Values が null の場合はソース配列から直接コピー
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

    // ─── ヘルパー ─────────────────────────────────────────────────────────

    private static int[] GetArray(int bufferId, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => bufferId == 0 ? mainArray : bufferArrays.GetValueOrDefault(bufferId, mainArray);

    private static int GetValue(int bufferId, int index, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        if (index < 0) return 0;
        var arr = GetArray(bufferId, mainArray, bufferArrays);
        return index < arr.Length ? arr[index] : 0;
    }

    private static string FormatLocation(int bufferId, int index)
        => index < 0 ? "一時値"
        : bufferId == 0 ? $"位置 {index}" : $"バッファ位置 {index}";

    private static void AddBufferHighlight(Dictionary<int, int[]> dict, int bufferId, int index)
    {
        if (bufferId == 0) return;
        if (dict.TryGetValue(bufferId, out var existing))
            dict[bufferId] = [.. existing, index];
        else
            dict[bufferId] = [index];
    }
}

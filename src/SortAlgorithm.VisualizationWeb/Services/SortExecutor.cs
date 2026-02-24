using SortAlgorithm.Contexts;
using SortAlgorithm.VisualizationWeb.Models;
using System.Buffers;
using System.Diagnostics;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// ソート実行と操作記録を行うサービス
/// </summary>
public class SortExecutor
{
    /// <summary>
    /// 適応的計測の目標合計時間（ms）。
    /// Blazor WASM では Stopwatch が performance.now() を使用し解像度が ~1ms に制限される。
    /// 合計計測時間がこの値を超えるまで繰り返し実行することで安定した平均実行時間を得る。
    /// 高速ソート: 自動的に多数回ループ → 安定、低速ソート: 1回で閾値超過 → 即終了。
    /// </summary>
    private const double MeasurementTargetMs = 50.0;

    /// <summary>
    /// 非同期版での UI スレッド解放間隔（ms）。
    /// 計測ループ内でこの時間が経過するたびに Task.Yield() を呼び、
    /// Blazor WASM のメインスレッドを一時的に解放して UI 更新を可能にする。
    /// </summary>
    private const double YieldIntervalMs = 16.0;

    /// <summary>
    /// ソートを実行し、すべての操作を記録する
    /// </summary>
    public (List<SortOperation> Operations, StatisticsContext Statistics, TimeSpan ActualExecutionTime) ExecuteAndRecord(ReadOnlySpan<int> sourceArray, AlgorithmMetadata algorithm)
    {
        var operations = new List<SortOperation>();

        // ArrayPoolから配列をレンタル（CompositeContext用作業配列）
        var workArray = ArrayPool<int>.Shared.Rent(sourceArray.Length);

        // 計測専用配列（ArrayPoolで確保し、ループ内で再利用してアロケーションを抑制）
        // Span<int>に変更済みのため、.AsSpan(0, sourceArray.Length)で正確な長さにスライスして渡せる
        var measureArray = ArrayPool<int>.Shared.Rent(sourceArray.Length);
        Span<int> measureSpan = measureArray.AsSpan(0, sourceArray.Length);

        try
        {
            // ウォームアップ（JIT最適化を促進、計測に含めない）
            sourceArray.CopyTo(measureSpan);
            algorithm.SortAction(measureSpan, NullContext.Default);

            // 適応的反復計測:
            // wallClock     → ループ終了判定用（CopyTo 込みの経過時間）
            // sortOnlyTicks → ソート処理のみの累積 tick（CopyTo を除外）
            // 合計計測時間が MeasurementTargetMs を超えるまで繰り返し、
            // 実行回数で割ることで安定した平均実行時間を得る。
            // - 高速ソート（例: 0.01ms/run）→ ~5,000回ループ → 安定した平均値
            // - 低速ソート（例: 100ms/run） → 1回で閾値超過 → 即終了、UX影響なし
            sourceArray.CopyTo(measureSpan);
            var wallClock = Stopwatch.StartNew();
            long sortOnlyTicks = 0L;
            int runs = 0;
            do
            {
                var before = Stopwatch.GetTimestamp();
                algorithm.SortAction(measureSpan, NullContext.Default);
                sortOnlyTicks += Stopwatch.GetTimestamp() - before;
                runs++;
                if (wallClock.Elapsed.TotalMilliseconds < MeasurementTargetMs)
                    sourceArray.CopyTo(measureSpan);
            } while (wallClock.Elapsed.TotalMilliseconds < MeasurementTargetMs);
            wallClock.Stop();

            // ソートのみの平均実行時間（CopyTo のオーバーヘッドを除外）
            var actualExecutionTime = TimeSpan.FromSeconds((double)sortOnlyTicks / Stopwatch.Frequency / runs);

            // ワーク配列を初期状態にリセット（CompositeContext実行用）
            sourceArray.CopyTo(workArray.AsSpan(0, sourceArray.Length));

            // StatisticsContextを作成（正確な統計情報を記録）
            var statisticsContext = new StatisticsContext();
            
            // VisualizationContextを使って操作を記録
            var visualizationContext = new VisualizationContext(
                onCompare: (i, j, result, bufferIdI, bufferIdJ) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.Compare,
                        Index1 = i,
                        Index2 = j,
                        BufferId1 = bufferIdI,
                        BufferId2 = bufferIdJ,
                        CompareResult = result
                    });
                },
                onSwap: (i, j, bufferId) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.Swap,
                        Index1 = i,
                        Index2 = j,
                        BufferId1 = bufferId
                    });
                },
                onIndexRead: (index, bufferId) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.IndexRead,
                        Index1 = index,
                        BufferId1 = bufferId
                    });
                },
                onIndexWrite: (index, bufferId, value) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.IndexWrite,
                        Index1 = index,
                        BufferId1 = bufferId,
                        Value = value as int?
                    });
                },
                onRangeCopy: (sourceIndex, destIndex, length, sourceBufferId, destBufferId, values) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.RangeCopy,
                        Index1 = sourceIndex,
                        Index2 = destIndex,
                        Length = length,
                        BufferId1 = sourceBufferId,
                        BufferId2 = destBufferId,
                        Values = values?.Length > 0
                            ? Array.ConvertAll(values, v => v is int intVal ? intVal : 0)
                            : null
                    });
                }
            );
            
            // CompositeContextを作成して両方のコンテキストを組み合わせる
            var compositeContext = new CompositeContext(statisticsContext, visualizationContext);
            
            // 2回目: CompositeContextで操作・統計を記録（NullContextで計測した実行時間を使用）
            algorithm.SortAction(workArray.AsSpan(0, sourceArray.Length), compositeContext);
            
            
            return (operations, statisticsContext, actualExecutionTime);
        }
        finally
        {
            // ArrayPoolに配列を返却
            ArrayPool<int>.Shared.Return(workArray, clearArray: true);
            ArrayPool<int>.Shared.Return(measureArray, clearArray: true);
        }
    }

    /// <summary>
    /// UI スレッドをブロックしないよう Task.Yield() を挟みながらソートを実行・記録する非同期版。
    /// Blazor WebAssembly 上で ComparisonMode の複数アルゴリズムを追加する際に使用する。
    /// </summary>
    /// <remarks>
    /// Span&lt;T&gt; は async メソッドのローカル変数として使えないため int[] を受け取る。
    /// Span はすべてインライン式として使用し、await をまたがない。
    /// Task.Yield() は計測ループで YieldIntervalMs (~16ms) おきに、
    /// 記録パス実行の前後にそれぞれ 1 回実行する。
    /// O(n²) アルゴリズムの記録パスは依然として同期ブロッキングだが、
    /// yield の挿入により O(n log n) では体感フリーズがほぼ解消される。
    /// </remarks>
    public async Task<(List<SortOperation> Operations, StatisticsContext Statistics, TimeSpan ActualExecutionTime)> ExecuteAndRecordAsync(int[] sourceArray, AlgorithmMetadata algorithm)
    {
        var n = sourceArray.Length;
        var workArray = ArrayPool<int>.Shared.Rent(n);
        var measureArray = ArrayPool<int>.Shared.Rent(n);

        try
        {
            // ウォームアップ（Span はインライン式として渡す。ローカル変数に格納しない）
            sourceArray.AsSpan(0, n).CopyTo(measureArray.AsSpan(0, n));
            algorithm.SortAction(measureArray.AsSpan(0, n), NullContext.Default);

            // 適応的反復計測
            sourceArray.AsSpan(0, n).CopyTo(measureArray.AsSpan(0, n));
            var wallClock = Stopwatch.StartNew();
            long sortOnlyTicks = 0L;
            int runs = 0;
            double lastYieldMs = 0.0;

            do
            {
                // イテレーション内では Span をインライン式として使用し、await をまたがない
                var before = Stopwatch.GetTimestamp();
                algorithm.SortAction(measureArray.AsSpan(0, n), NullContext.Default);
                sortOnlyTicks += Stopwatch.GetTimestamp() - before;
                runs++;

                var elapsed = wallClock.Elapsed.TotalMilliseconds;
                if (elapsed < MeasurementTargetMs)
                    sourceArray.AsSpan(0, n).CopyTo(measureArray.AsSpan(0, n));

                // YieldIntervalMs (~16ms / 約1フレーム) おきに UI スレッドへ制御を返す
                // ここで Span は一切スコープに存在しないため await 可能
                if (elapsed - lastYieldMs >= YieldIntervalMs)
                {
                    lastYieldMs = elapsed;
                    await Task.Yield();
                }
            } while (wallClock.Elapsed.TotalMilliseconds < MeasurementTargetMs);

            wallClock.Stop();

            var actualExecutionTime = TimeSpan.FromSeconds((double)sortOnlyTicks / Stopwatch.Frequency / runs);

            // 記録パス用のワーク配列を準備したあと yield して UI を解放
            sourceArray.AsSpan(0, n).CopyTo(workArray.AsSpan(0, n));
            await Task.Yield();

            // 操作記録
            var operations = new List<SortOperation>();
            var statisticsContext = new StatisticsContext();
            var visualizationContext = new VisualizationContext(
                onCompare: (i, j, result, bufferIdI, bufferIdJ) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.Compare,
                        Index1 = i,
                        Index2 = j,
                        BufferId1 = bufferIdI,
                        BufferId2 = bufferIdJ,
                        CompareResult = result
                    });
                },
                onSwap: (i, j, bufferId) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.Swap,
                        Index1 = i,
                        Index2 = j,
                        BufferId1 = bufferId
                    });
                },
                onIndexRead: (index, bufferId) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.IndexRead,
                        Index1 = index,
                        BufferId1 = bufferId
                    });
                },
                onIndexWrite: (index, bufferId, value) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.IndexWrite,
                        Index1 = index,
                        BufferId1 = bufferId,
                        Value = value as int?
                    });
                },
                onRangeCopy: (sourceIndex, destIndex, rangeLength, sourceBufferId, destBufferId, values) =>
                {
                    operations.Add(new SortOperation
                    {
                        Type = OperationType.RangeCopy,
                        Index1 = sourceIndex,
                        Index2 = destIndex,
                        Length = rangeLength,
                        BufferId1 = sourceBufferId,
                        BufferId2 = destBufferId,
                        Values = values?.Length > 0
                            ? Array.ConvertAll(values, v => v is int intVal ? intVal : 0)
                            : null
                    });
                }
            );

            var compositeContext = new CompositeContext(statisticsContext, visualizationContext);

            // 記録パス実行（Span はインライン式）
            algorithm.SortAction(workArray.AsSpan(0, n), compositeContext);

            // 記録完了後に yield して UI の応答性を即座に回復
            await Task.Yield();

            return (operations, statisticsContext, actualExecutionTime);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(workArray, clearArray: true);
            ArrayPool<int>.Shared.Return(measureArray, clearArray: true);
        }
    }
}

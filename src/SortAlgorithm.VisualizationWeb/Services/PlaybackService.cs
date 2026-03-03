using System.Buffers;
using Microsoft.JSInterop;
using SortAlgorithm.Contexts;
using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// 再生制御とシーク処理を行うサービス。
/// JS の requestAnimationFrame をドライバーとした rAF 駆動ループで表示を行う。
/// </summary>
public class PlaybackService : IDisposable
{
    private readonly IJSRuntime _js;

    // rAF ループ管理
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private DotNetObjectReference<PlaybackService>? _dotNetRef;
    private bool _isRegisteredWithLoop;

    // SpeedMultiplier に基づくフレーム累積（rAF 駆動用）
    // += SpeedMultiplier 毎フレーム。>= 1.0 になったら処理。
    private double _frameAccumulator;

    private List<SortOperation> _operations = [];

    // ArrayPoolで配列を再利用
    private int[] _pooledArray;
    private int _currentArraySize;
    private int[] _initialArray = [];
    private Dictionary<int, int[]> _initialBuffers = new();

    // 累積統計（各操作インデックスでの統計値）
    private CumulativeStats[] _cumulativeStats = [];

    private const int MAX_ARRAY_SIZE = 4096; // 最大配列サイズ

    // 完了ハイライト用のタイマー
    private CancellationTokenSource? _completionHighlightCts;
    private const int COMPLETION_HIGHLIGHT_DURATION_MS = 2000; // 2秒

    // シークのスロットリング用
    private DateTime _lastSeekTime = DateTime.MinValue;
    private const int SEEK_THROTTLE_MS = 16; // 60 FPS

    // デルタ追跡用（今フレームの配列変更）
    private readonly List<int> _mainDelta = [];
    private readonly Dictionary<int, List<int>> _bufferDeltas = new();
    private bool _trackDeltas = true; // InstantMode では false にして追跡をスキップ

    // 音: 今フレームの Read/Write 周波数バッファ（再利用してアロケーション抑制）
    private readonly List<float> _soundFreqBuffer = new(capacity: 16);
    
    /// <summary>現在の状態</summary>
    public VisualizationState State { get; private set; } = new();
    
    /// <summary>1フレームあたりの操作数（1-1000）</summary>
    public int OperationsPerFrame { get; set; } = 1;
    
    /// <summary>速度倍率（0.1x - 100x）</summary>
    public double SpeedMultiplier { get; set; } = 10.0;
    
    /// <summary>ソート完了時に自動的にリセットするか</summary>
    public bool AutoReset { get; set; } = false;
    
    /// <summary>描画なし超高速モード</summary>
    public bool InstantMode { get; set; } = false;

    /// <summary>音を再生するか（デフォルト OFF）</summary>
    public bool SoundEnabled { get; set; } = false;

    /// <summary>音量（0.0～1.0、デフォルト 0.5）</summary>
    public double SoundVolume { get; set; } = 0.5;

    /// <summary>状態が変更されたときのイベント</summary>
    public event Action? StateChanged;
    
    public PlaybackService(IJSRuntime js)
    {
        _js = js;
        // 最大サイズの配列をArrayPoolからレンタル
        _pooledArray = ArrayPool<int>.Shared.Rent(MAX_ARRAY_SIZE);
        _currentArraySize = 0;
    }

    /// <summary>
    /// AudioContext を初期化・再開する。Sound トグルを ON にした直後（ユーザー操作）に呼ぶ。
    /// </summary>
    public async ValueTask InitSoundAsync()
    {
        await _js.InvokeVoidAsync("soundEngine.initAudio");
    }
    
    /// <summary>
    /// ソート操作をロードする
    /// </summary>
    public void LoadOperations(ReadOnlySpan<int> initialArray, List<SortOperation> operations, StatisticsContext statistics, TimeSpan actualExecutionTime)
    {
        Stop();
        _operations = operations;
        _currentArraySize = initialArray.Length;

        // プールされた配列が不足している場合は返却して再レンタル
        if (_currentArraySize > _pooledArray.Length)
        {
            ArrayPool<int>.Shared.Return(_pooledArray, clearArray: true);
            _pooledArray = ArrayPool<int>.Shared.Rent(_currentArraySize);
        }

        // プールされた配列の必要な部分だけを使用
        initialArray.CopyTo(_pooledArray.AsSpan(0, _currentArraySize));
        _initialArray = _pooledArray.AsSpan(0, _currentArraySize).ToArray(); // 初期状態のコピーを保持
        _initialBuffers.Clear();
        
        // 累積統計を計算（StatisticsContextの計算ロジックを使用）
        _cumulativeStats = new CumulativeStats[operations.Count + 1]; // +1は初期状態用
        ulong cumulativeCompares = 0;
        ulong cumulativeSwaps = 0;
        ulong cumulativeReads = 0;
        ulong cumulativeWrites = 0;
        
        for (int i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            
            // StatisticsContextと同じロジックで累積統計を計算
            switch (op.Type)
            {
                case OperationType.Compare:
                    cumulativeCompares++;
                    break;
                    
                case OperationType.Swap:
                    if (op.BufferId1 >= 0) // StatisticsContextと同じ条件
                    {
                        cumulativeSwaps++;
                        cumulativeReads += 2;  // Swap = 2 reads
                        cumulativeWrites += 2; // Swap = 2 writes
                    }
                    break;
                    
                case OperationType.IndexRead:
                    if (op.BufferId1 >= 0)
                    {
                        cumulativeReads++;
                    }
                    break;
                    
                case OperationType.IndexWrite:
                    if (op.BufferId1 >= 0)
                    {
                        cumulativeWrites++;
                    }
                    break;
                    
                case OperationType.RangeCopy:
                    if (op.BufferId1 >= 0)
                    {
                        cumulativeReads += (ulong)op.Length;
                    }
                    if (op.BufferId2 >= 0)
                    {
                        cumulativeWrites += (ulong)op.Length;
                    }
                    break;
            }
            
            // この操作後の累積統計を保存（インデックスi+1に保存）
            _cumulativeStats[i + 1] = new CumulativeStats
            {
                CompareCount = cumulativeCompares,
                SwapCount = cumulativeSwaps,
                IndexReadCount = cumulativeReads,
                IndexWriteCount = cumulativeWrites
            };
        }
        
        // 現在のVisualizationModeを保持
        var currentMode = State.Mode;
        var nextSortVersion = State.SortVersion + 1;
        
        State = new VisualizationState
        {
            MainArray = _pooledArray.AsSpan(0, _currentArraySize).ToArray(), // 現在の状態用のコピー
            TotalOperations = operations.Count,
            CurrentOperationIndex = 0,
            PlaybackState = PlaybackState.Stopped,
            Mode = currentMode, // モードを引き継ぐ
            IsSortCompleted = false, // 明示的にfalseに設定
            ShowCompletionHighlight = false, // ハイライト表示もfalse
            Statistics = statistics, // StatisticsContextを設定（最終値として保持）
            CumulativeStats = _cumulativeStats, // 累積統計配列を設定
            ActualExecutionTime = actualExecutionTime, // 実測実行時間を設定
            SortVersion = nextSortVersion, // 新しいソートをしたことを JS 側に知らせる
            MainArrayDelta = null,         // 最初のレンダリングは全量更新
        };
        DiscardPendingDeltas();
        
        StateChanged?.Invoke();
    }
    
    /// <summary>
    /// 再生開始
    /// </summary>
    public void Play()
    {
        if (State.PlaybackState == PlaybackState.Playing) return;

        State.PlaybackState = PlaybackState.Playing;

        // 描画なしモードの場合は即座に完了
        if (InstantMode)
        {
            PlayInstant();
            return;
        }

        // rAF ループに登録（playbackHelper.js が vsync に合わせて OnAnimationFrame を呼び出す）
        _frameAccumulator = 0.0;
        _dotNetRef ??= DotNetObjectReference.Create(this);
        _ = _js.InvokeVoidAsync("playbackHelper.start", _instanceId, _dotNetRef);
        _isRegisteredWithLoop = true;

        StateChanged?.Invoke();
    }
    
    /// <summary>
    /// 描画なし超高速実行
    /// </summary>
    private async void PlayInstant()
    {
        // InstantMode は全操作を一気に処理するため、差分追跡をスキップして最終状態だけ送信する
        _trackDeltas = false;
        
        // UI更新を完全スキップして全操作を処理
        while (State.CurrentOperationIndex < _operations.Count)
        {
            var operation = _operations[State.CurrentOperationIndex];
            ApplyOperation(operation, applyToArray: true, updateStats: true);
            State.CurrentOperationIndex++;
        }
        
        _trackDeltas = true;
        DiscardPendingDeltas(); // 全量更新を指示（MainArrayDelta = null）
        
        // 完了
        ClearHighlights(); // ソート完了時にハイライトをクリア
        State.IsSortCompleted = true; // ソート完了フラグを設定
        State.ShowCompletionHighlight = true; // ハイライト表示を開始
        State.PlaybackState = PlaybackState.Paused;
        
        // 最終状態を描画（緑色ハイライト表示）
        StateChanged?.Invoke();
        
        // AutoResetがONの場合は、少し待ってからリセット
        if (AutoReset)
        {
            await Task.Delay(500); // 500ms緑色を表示
            Stop();
        }
        else
        {
            // 完了ハイライトを2秒後にクリア
            ScheduleCompletionHighlightClear();
        }
    }
    
    /// <summary>
    /// 一時停止
    /// </summary>
    public void Pause()
    {
        if (State.PlaybackState != PlaybackState.Playing) return;

        State.PlaybackState = PlaybackState.Paused;
        StopLoop();
        StateChanged?.Invoke();
    }
    
    /// <summary>
    /// 停止してリセット
    /// </summary>
    public void Stop()
    {
        StopLoop();
        _completionHighlightCts?.Cancel(); // 完了ハイライトタイマーもキャンセル
        State.CurrentOperationIndex = 0;

        // プールされた配列を再利用
        if (_currentArraySize > 0)
        {
            _initialArray.AsSpan().CopyTo(_pooledArray.AsSpan(0, _currentArraySize));
            State.MainArray = _pooledArray.AsSpan(0, _currentArraySize).ToArray();
        }

        State.BufferArrays.Clear();
        State.PlaybackState = PlaybackState.Stopped;
        State.IsSortCompleted = false; // リセット時に完了フラグをクリア
        State.ShowCompletionHighlight = false; // ハイライト表示もクリア
        ClearHighlights();
        DiscardPendingDeltas(); // リセット後は全量更新
        ResetStatistics();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// playbackHelper.js の rAF ループから登録解除する内部ヘルパー。
    /// </summary>
    private void StopLoop()
    {
        if (_isRegisteredWithLoop)
        {
            _ = _js.InvokeVoidAsync("playbackHelper.stop", _instanceId);
            _isRegisteredWithLoop = false;
        }
    }
    
    /// <summary>
    /// rAF ループから毎フレーム呼び出されるメソッド（playbackHelper.js が invokeMethod で同期呼出し）
    /// </summary>
    /// <remarks>
    /// <para>
    /// SpeedMultiplier の実現：
    ///   毎フレーム _frameAccumulator += SpeedMultiplier で蓄積。
    ///   &lt; 1.0 の間は処理をスキップ（SpeedMultiplier &lt; 1.0 時のスローモーション）。
    ///   &gt;= 1.0 で int 部分分のフレーム分の操作を一括処理（SpeedMultiplier &gt; 1.0 時の高速モード）。
    /// </para>
    /// </remarks>
    [JSInvokable]
    public bool OnAnimationFrame()
    {
        if (State.PlaybackState != PlaybackState.Playing || State.CurrentOperationIndex >= _operations.Count)
        {
            _isRegisteredWithLoop = false;
            return false;
        }

        // SpeedMultiplier に応じたフレーム蓄積
        _frameAccumulator += SpeedMultiplier;
        if (_frameAccumulator < 1.0)
        {
            return true; // まだ処理タイミングでない（スローモーション）
        }

        // 一括処理するフレーム数（SpeedMultiplier >= 1.0 の場合は複数フレーム分）
        var framesToProcess = (int)_frameAccumulator;
        _frameAccumulator -= framesToProcess;
        // バックグラウンド時にタブが非アクティブになった場合など、蓄積値が大きくなり遠すぎて進むのを防ぐ
        if (_frameAccumulator > 3.0) _frameAccumulator = 0.0;

        ClearHighlights();

        var effectiveOps = Math.Min(OperationsPerFrame * framesToProcess,
                                    _operations.Count - State.CurrentOperationIndex);

        // 音: 発音対象フレームかどうか判定（SpeedMultiplier > 50 は自動無効）
        var soundActive = SoundEnabled && SpeedMultiplier <= 50.0;
        if (soundActive) _soundFreqBuffer.Clear();

        for (int i = 0; i < effectiveOps && State.CurrentOperationIndex < _operations.Count; i++)
        {
            var operation = _operations[State.CurrentOperationIndex];

            // 音: ApplyOperation の前に周波数を収集（Read は配列変更前の値が正しい値）
            if (soundActive &&
                (operation.Type == OperationType.IndexRead || operation.Type == OperationType.IndexWrite))
            {
                var freq = GetFrequencyForOp(operation);
                if (freq > 0f) _soundFreqBuffer.Add(freq);
            }

            ApplyOperation(operation, applyToArray: true, updateStats: true);
            State.CurrentOperationIndex++;
        }

        // 音: サンプリングして発音（JS Interop 1回/フレーム）
        if (soundActive && _soundFreqBuffer.Count > 0)
        {
            var duration = GetSoundDuration(SpeedMultiplier);
            var sampled = SampleSoundFrequencies();
            _ = _js.InvokeVoidAsync("soundEngine.playNotes", sampled, duration, SoundVolume);
        }

        // ハイライト更新（最後の操作）
        if (State.CurrentOperationIndex > 0 && State.CurrentOperationIndex < _operations.Count)
        {
            var lastOperation = _operations[State.CurrentOperationIndex - 1];
            ApplyOperation(lastOperation, applyToArray: false, updateStats: false);
        }

        FinalizeDeltas();
        StateChanged?.Invoke();

        if (State.CurrentOperationIndex >= _operations.Count)
        {
            _isRegisteredWithLoop = false;
            _ = HandleCompletionAsync();
            return false; // rAF ループを停止
        }

        return true;
    }

    /// <summary>
    /// ソート完了時の非同期後続処理（ハイライト表示、リセット待機）
    /// </summary>
    private async Task HandleCompletionAsync()
    {
        ClearHighlights();
        State.BufferArrays.Clear();
        State.IsSortCompleted = true;
        State.ShowCompletionHighlight = true;
        State.PlaybackState = PlaybackState.Paused;

        FinalizeDeltas();
        StateChanged?.Invoke();

        if (AutoReset)
        {
            await Task.Delay(500);
            Stop();
        }
        else
        {
            ScheduleCompletionHighlightClear();
        }
    }
    
    /// <summary>
    /// 再生/一時停止を切り替え
    /// </summary>
    public void TogglePlayPause()
    {
        if (State.PlaybackState == PlaybackState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }
    
    /// <summary>
    /// 指定位置にシーク（インクリメンタル方式で高速化）
    /// </summary>
    public void SeekTo(int operationIndex, bool throttle = false)
    {
        if (operationIndex < 0 || operationIndex > _operations.Count)
            return;
        
        // スロットリング: 連続シーク時は一定間隔でのみ処理
        if (throttle)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSeekTime).TotalMilliseconds;
            if (elapsed < SEEK_THROTTLE_MS)
            {
                return; // スキップ
            }
            _lastSeekTime = now;
        }
        
        var currentIndex = State.CurrentOperationIndex;
        var targetIndex = operationIndex;
        
        // 現在位置と目的位置が近い場合は、インクリメンタルシーク
        var distance = Math.Abs(targetIndex - currentIndex);
        var replayThreshold = Math.Min(1000, _operations.Count / 4); // 閾値: 1000操作 or 全体の25%
        
        if (distance < replayThreshold && currentIndex <= targetIndex)
        {
            // 前方シーク: 現在位置から目的位置まで進める（高速）
            SeekForward(currentIndex, targetIndex);
        }
        else
        {
            // 後方シークまたは距離が遠い場合: 初期状態からリプレイ
            SeekFromBeginning(targetIndex);
        }
        
        State.CurrentOperationIndex = targetIndex;
        
        // ソート完了状態を更新
        State.IsSortCompleted = (operationIndex >= _operations.Count);
        State.ShowCompletionHighlight = State.IsSortCompleted;
        
        // 現在の操作をハイライト（完了時はハイライトなし）
        ClearHighlights();
        if (targetIndex < _operations.Count)
        {
            ApplyOperation(_operations[targetIndex], applyToArray: false, updateStats: false);
        }
        
        DiscardPendingDeltas(); // シーク後は差分ではなく全量更新
        StateChanged?.Invoke();
    }
    
    /// <summary>
    /// 前方シーク: 現在位置から目的位置まで進める
    /// </summary>
    private void SeekForward(int fromIndex, int toIndex)
    {
        for (int i = fromIndex; i < toIndex && i < _operations.Count; i++)
        {
            ApplyOperation(_operations[i], applyToArray: true, updateStats: true);
        }
    }
    
    /// <summary>
    /// 初期状態からリプレイ
    /// </summary>
    private void SeekFromBeginning(int targetIndex)
    {
        // 初期状態から指定位置まで操作を適用
        State.MainArray = [.. _initialArray];
        State.BufferArrays.Clear();
        ResetStatistics();
        
        for (int i = 0; i < targetIndex && i < _operations.Count; i++)
        {
            ApplyOperation(_operations[i], applyToArray: true, updateStats: true);
        }
    }
    
    private void ApplyOperation(SortOperation operation, bool applyToArray, bool updateStats)
    {
        switch (operation.Type)
        {
            case OperationType.Compare:
                State.CompareIndices.Add(operation.Index1);
                State.CompareIndices.Add(operation.Index2);
                break;
                
            case OperationType.Swap:
                State.SwapIndices.Add(operation.Index1);
                State.SwapIndices.Add(operation.Index2);
                if (applyToArray)
                {
                    var arr = GetArray(operation.BufferId1).AsSpan();
                    (arr[operation.Index1], arr[operation.Index2]) = (arr[operation.Index2], arr[operation.Index1]);
                    RecordDelta(operation.BufferId1, operation.Index1, arr[operation.Index1]);
                    RecordDelta(operation.BufferId1, operation.Index2, arr[operation.Index2]);
                }
                break;
                
            case OperationType.IndexRead:
                State.ReadIndices.Add(operation.Index1);
                break;
                
            case OperationType.IndexWrite:
                State.WriteIndices.Add(operation.Index1);
                if (applyToArray && operation.Value.HasValue)
                {
                    var arr = GetArray(operation.BufferId1).AsSpan();
                    if (operation.Index1 >= 0 && operation.Index1 < arr.Length)
                    {
                        arr[operation.Index1] = operation.Value.Value;
                        RecordDelta(operation.BufferId1, operation.Index1, operation.Value.Value);
                    }
                }
                break;
                
            case OperationType.RangeCopy:
                // ハイライト表示: sourceとdestinationの範囲をハイライト
                for (int i = 0; i < operation.Length; i++)
                {
                    if (operation.Index1 >= 0)
                    {
                        State.ReadIndices.Add(operation.Index1 + i);
                    }
                    if (operation.Index2 >= 0)
                    {
                        State.WriteIndices.Add(operation.Index2 + i);
                    }
                }
                
                if (applyToArray)
                {
                    if (operation.Values is { Length: > 0 })
                    {
                        // 記録された値を直接書き込む（バッファー状態に依存しない正確な再生）
                        var destArr = GetArray(operation.BufferId2);
                        var destSpan = destArr.AsSpan();
                        
                        for (int i = 0; i < operation.Values.Length && operation.Index2 + i < destSpan.Length; i++)
                        {
                            destSpan[operation.Index2 + i] = operation.Values[i];
                            RecordDelta(operation.BufferId2, operation.Index2 + i, operation.Values[i]);
                        }
                    }
                    else
                    {
                        // フォールバック: 値が記録されていない場合はソースからコピー
                        var sourceArr = GetArray(operation.BufferId1);
                        var destArr = GetArray(operation.BufferId2);
                        
                        var sourceSpan = sourceArr.AsSpan();
                        var destSpan = destArr.AsSpan();
                        
                        if (operation.Index1 >= 0 && operation.Index2 >= 0 && 
                            operation.Length > 0 &&
                            operation.Index1 + operation.Length <= sourceSpan.Length &&
                            operation.Index2 + operation.Length <= destSpan.Length)
                        {
                            sourceSpan.Slice(operation.Index1, operation.Length)
                                .CopyTo(destSpan.Slice(operation.Index2, operation.Length));
                            
                            for (int i = 0; i < operation.Length; i++)
                            {
                                RecordDelta(operation.BufferId2, operation.Index2 + i, destSpan[operation.Index2 + i]);
                            }
                        }
                    }
                }
                break;
        }
    }
    
    private int[] GetArray(int bufferId)
    {
        if (bufferId == 0) return State.MainArray;
        
        // バッファー配列が存在しない場合のみ作成
        if (!State.BufferArrays.ContainsKey(bufferId))
        {
            State.BufferArrays[bufferId] = new int[State.MainArray.Length];
        }
        return State.BufferArrays[bufferId];
    }
    
    
    /// <summary>
    /// フレームを進める（ComparisonModeService用の公開メソッド）
    /// </summary>
    public void AdvanceFrame(int opsToProcess)
    {
        if (State.CurrentOperationIndex >= _operations.Count)
            return;
        
        ClearHighlights();
        
        int actualOps = Math.Min(opsToProcess, _operations.Count - State.CurrentOperationIndex);
        for (int i = 0; i < actualOps && State.CurrentOperationIndex < _operations.Count; i++)
        {
            var operation = _operations[State.CurrentOperationIndex];
            ApplyOperation(operation, applyToArray: true, updateStats: true);
            State.CurrentOperationIndex++;
        }
        
        // ハイライト更新（最後の操作）
        if (State.CurrentOperationIndex > 0 && State.CurrentOperationIndex < _operations.Count)
        {
            var lastOperation = _operations[State.CurrentOperationIndex - 1];
            ApplyOperation(lastOperation, applyToArray: false, updateStats: false);
        }
        
        // StateChangedは呼ばない（ComparisonModeServiceが統一的に呼ぶ）
    }
    
    private void ClearHighlights()
    {
        State.CompareIndices.Clear();
        State.SwapIndices.Clear();
        State.ReadIndices.Clear();
        State.WriteIndices.Clear();
    }

    /// <summary>
    /// 操作から発音周波数を計算する（IndexRead: 配列の現在値、IndexWrite: 書き込む値）。
    /// </summary>
    private float GetFrequencyForOp(SortOperation op)
    {
        if (_currentArraySize <= 0) return -1f;

        int value;
        if (op.Type == OperationType.IndexWrite)
        {
            value = op.Value ?? 0;
        }
        else // IndexRead
        {
            var arr = GetArray(op.BufferId1);
            value = (op.Index1 >= 0 && op.Index1 < arr.Length) ? arr[op.Index1] : 0;
        }

        value = Math.Clamp(value, 0, _currentArraySize);
        return 200f + (value / (float)_currentArraySize) * 1000f;
    }

    /// <summary>
    /// 仕様 A4: 実際に収集した Read/Write 数に応じて発音する周波数をサンプリングする。
    /// OpsPerFrame=1 でも SpeedMultiplier が高い場合は effectiveOps が増えるため、
    /// 設定値でなく実際の収集数で決定することで防乱を防ぐ。
    /// </summary>
    private float[] SampleSoundFrequencies()
    {
        var count = _soundFreqBuffer.Count;
        if (count == 0) return [];

        if (count <= 3)
        {
            // 全音再生
            return [.. _soundFreqBuffer];
        }

        if (count <= 10)
        {
            // 等間隔3点サンプリング（先頭・中間・末尾）
            return [_soundFreqBuffer[0], _soundFreqBuffer[count / 2], _soundFreqBuffer[count - 1]];
        }

        // 末尾1音のみ
        return [_soundFreqBuffer[count - 1]];
    }

    /// <summary>
    /// 仕様 B4: SpeedMultiplier に応じた発音持続時間（ms）を返す。50x 超は 0（無効）。
    /// </summary>
    private static int GetSoundDuration(double speedMultiplier) => speedMultiplier switch
    {
        > 50 => 0,
        > 20 => 20,
        > 5  => 40,
        > 2  => 80,
        _    => 150,
    };

    /// <summary>
    /// 配列変更を差分リストに記録する（applyToArray=true のときのみ呼ぶ）
    /// </summary>
    private void RecordDelta(int bufferId, int index, int value)
    {
        if (!_trackDeltas) return;

        if (bufferId == 0)
        {
            _mainDelta.Add(index);
            _mainDelta.Add(value);
        }
        else
        {
            if (!_bufferDeltas.TryGetValue(bufferId, out var list))
            {
                list = [];
                _bufferDeltas[bufferId] = list;
            }
            list.Add(index);
            list.Add(value);
        }
    }

    /// <summary>
    /// 蓄積したデルタを State に書き込み、リストをクリアする。
    /// StateChanged?.Invoke() の直前に呼ぶ。
    /// </summary>
    private void FinalizeDeltas()
    {
        State.MainArrayDelta = _mainDelta.Count > 0 ? _mainDelta.ToArray() : [];

        if (_bufferDeltas.Count > 0)
        {
            var result = new Dictionary<int, int[]>(_bufferDeltas.Count);
            foreach (var (id, list) in _bufferDeltas)
            {
                if (list.Count > 0)
                    result[id] = list.ToArray();
            }
            State.BufferArrayDeltas = result.Count > 0 ? result : null;
        }
        else
        {
            State.BufferArrayDeltas = null;
        }

        _mainDelta.Clear();
        _bufferDeltas.Clear();
    }

    /// <summary>
    /// 未送信のデルタを破棄する（シーク・リセット時に全量更新するため）
    /// </summary>
    private void DiscardPendingDeltas()
    {
        _mainDelta.Clear();
        _bufferDeltas.Clear();
        State.MainArrayDelta = null;   // null = 全量更新フラグ
        State.BufferArrayDeltas = null;
    }
    
    private void ResetStatistics()
    {
        // StatisticsContextがある場合は何もしない（イミュータブル）
    }
    
    /// <summary>
    /// 完了ハイライトを指定時間後にクリア
    /// </summary>
    private async void ScheduleCompletionHighlightClear()
    {
        // 既存のタイマーをキャンセル
        _completionHighlightCts?.Cancel();
        _completionHighlightCts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(COMPLETION_HIGHLIGHT_DURATION_MS, _completionHighlightCts.Token);
            
            // ハイライト表示だけをクリア（IsSortCompletedは維持）
            State.ShowCompletionHighlight = false;
            StateChanged?.Invoke();
        }
        catch (TaskCanceledException)
        {
            // キャンセルされた場合は何もしない
        }
    }
    
    public void Dispose()
    {
        StopLoop();

        // AudioContext を解放
        _ = _js.InvokeVoidAsync("soundEngine.disposeAudio");

        // 完了ハイライトタイマーをキャンセル
        _completionHighlightCts?.Cancel();
        _completionHighlightCts?.Dispose();

        // DotNetObjectReference を解放（JS 側の参照を開放）
        _dotNetRef?.Dispose();

        // 累積統計配列をクリア（メモリリーク防止）
        _cumulativeStats = [];
        _operations.Clear();
        _initialBuffers.Clear();

        // ArrayPoolに配列を返却
        if (_pooledArray != null)
        {
            ArrayPool<int>.Shared.Return(_pooledArray, clearArray: true);
        }
    }
}

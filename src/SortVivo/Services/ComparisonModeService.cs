using Microsoft.JSInterop;
using SortVivo.Models;
namespace SortVivo.Services;

public class ComparisonModeService : IDisposable
{
    private readonly SortExecutor _executor;
    private readonly DebugSettings _debug;
    private readonly IJSRuntime _js;
    private readonly ComparisonState _state = new();
    private readonly List<PlaybackService> _playbackServices = new();
    private bool _completionLogged;

    private static readonly string[] AlgorithmColors =
    [
        "#60a5fa", // blue
        "#f87171", // red
        "#34d399", // green
        "#fbbf24", // amber
        "#a78bfa", // purple
        "#fb923c", // orange
    ];

    public ComparisonState State => _state;
    public event Action? OnStateChanged;

    /// <summary>
    /// 非同期処理（Add / Replace）の実行中は true。
    /// UI ボタンの disabled 制御やローディング表示に使用する。
    /// </summary>
    public bool IsAddingAlgorithm { get; private set; }

    /// <summary>インスタンスが 1 件以上あるときの先頭インスタンスのサウンド ON/OFF 状態。</summary>
    public bool SoundEnabled => _playbackServices.Count > 0 && _playbackServices[0].SoundEnabled;

    /// <summary>N>1 時のシークバー位置（最後に SeekAll で設定したグローバル位置）。</summary>
    public int GlobalSeekIndex { get; private set; }

    /// <summary>全インスタンス中の最大総 ops 数（シークバーの max 値）。</summary>
    public int MaxOps => _playbackServices.Count == 0 ? 0 : _playbackServices.Max(p => p.State.TotalOperations);

    public ComparisonModeService(SortExecutor executor, DebugSettings debug, IJSRuntime js)
    {
        _executor = executor;
        _debug = debug;
        _js = js;
    }

    //
    // Add & Generate（Upsert）
    //

    /// <summary>
    /// 新規配列を生成し、既存の全カードを再実行したうえで選択アルゴリズムを Upsert する。
    /// - 選択 Algo が比較中に存在しない → 追加 (N→N+1)
    /// - 選択 Algo が比較中にすでに存在する → 配列再生成のみ (N 不変)
    /// - N が上限かつ選択 Algo 未登録 → 呼び出し元で disabled 制御済みのため何もしない
    /// </summary>
    public async Task AddAndGenerateAsync(
        string algorithmName,
        AlgorithmMetadata metadata,
        int[] newArray,
        int arraySize,
        ArrayPatternMetadata pattern)
    {
        if (IsAddingAlgorithm) return;

        IsAddingAlgorithm = true;

        // 既存アルゴリズムのリストを保存してから全クリア
        var existingAlgos = _state.Instances
            .Select(i => new { i.AlgorithmName, i.Metadata })
            .ToList();

        ClearAllInstances();
        _state.InitialArray = newArray;
        _state.CurrentArraySize = arraySize;
        _state.CurrentPattern = pattern;
        NotifyStateChanged();

        try
        {
            // 既存アルゴリズムを新配列で再実行
            foreach (var algo in existingAlgos)
            {
                if (!ReferenceEquals(newArray, _state.InitialArray)) break;
                await AddAlgorithmInternalAsync(algo.AlgorithmName, algo.Metadata);
            }

            // 選択アルゴリズムが含まれていなければ追加
            bool alreadyPresent = _state.Instances.Any(i => i.AlgorithmName == algorithmName);
            if (!alreadyPresent && _state.Instances.Count < ComparisonState.MaxComparisons)
            {
                await AddAlgorithmInternalAsync(algorithmName, metadata);
            }

            _debug.Log($"[ComparisonMode] AddAndGenerate done. N={_state.Instances.Count}");
        }
        catch (Exception ex)
        {
            _debug.Log($"[ComparisonMode] ERROR AddAndGenerateAsync: {ex.Message}");
        }
        finally
        {
            IsAddingAlgorithm = false;
            NotifyStateChanged();
        }
    }

    //
    // 追加のみ（既存配列・既存カード維持、追加分だけ計測）
    //

    /// <summary>
    /// 既存配列と既存カードをそのまま維持し、選択アルゴリズムだけ追加計測する。
    /// 配列サイズ・パターンが変わっていない場合の最適化パス。
    /// 呼び出し元で「アルゴリズム未登録 かつ N &lt; N_max かつ配列条件不変」を確認済みであること。
    /// </summary>
    public async Task AddAlgorithmAsync(string algorithmName, AlgorithmMetadata metadata)
    {
        if (IsAddingAlgorithm) return;
        if (_state.InitialArray.Length == 0) return;

        // 新アルゴリズム追加前に全インスタンスを先頭に戻す（再生位置のリセット）。
        // ソートの記録データ（操作列・統計）は再計算せず再利用する。
        foreach (var p in _playbackServices) p.Stop();
        GlobalSeekIndex = 0;

        IsAddingAlgorithm = true;
        NotifyStateChanged();

        try
        {
            await AddAlgorithmInternalAsync(algorithmName, metadata);
            _debug.Log($"[ComparisonMode] AddAlgorithm (reuse array) done. N={_state.Instances.Count}");
        }
        catch (Exception ex)
        {
            _debug.Log($"[ComparisonMode] ERROR AddAlgorithmAsync: {ex.Message}");
        }
        finally
        {
            IsAddingAlgorithm = false;
            NotifyStateChanged();
        }
    }

    //
    // インライン切替（同じ配列、N 不変）
    //

    /// <summary>
    /// 指定インデックスのカードのアルゴリズムを差し替える。
    /// 配列は現在の共有配列をそのまま使い、N は変わらない。
    /// </summary>
    public async Task ReplaceAlgorithmAsync(int index, string newAlgoName, AlgorithmMetadata newMetadata)
    {
        if (index < 0 || index >= _state.Instances.Count) return;
        if (_state.InitialArray.Length == 0) return;
        if (IsAddingAlgorithm) return;

        // 差し替え前に全インスタンスを先頭に戻す（再生位置のリセット）。
        foreach (var p in _playbackServices) p.Stop();
        GlobalSeekIndex = 0;

        IsAddingAlgorithm = true;
        NotifyStateChanged();

        try
        {
            var capturedArray = _state.InitialArray;
            var (operations, statistics, actualExecutionTime) =
                await _executor.ExecuteAndRecordAsync(capturedArray, newMetadata);

            if (!ReferenceEquals(capturedArray, _state.InitialArray)) return;

            // 旧インスタンスを破棄して同じ位置に新インスタンスを挿入
            _playbackServices[index].StateChanged -= OnPlaybackStateChanged;
            _playbackServices[index].Dispose();

            var newPlayback = new PlaybackService(_js);
            newPlayback.LoadOperations(capturedArray, operations, statistics, actualExecutionTime);
            newPlayback.StateChanged += OnPlaybackStateChanged;

            _playbackServices[index] = newPlayback;
            _state.Instances[index] = new ComparisonInstance
            {
                AlgorithmName = newAlgoName,
                State = newPlayback.State,
                Metadata = newMetadata,
                Playback = newPlayback,
            };

            _debug.Log($"[ComparisonMode] Replaced index={index} with {newAlgoName}");
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _debug.Log($"[ComparisonMode] ERROR ReplaceAlgorithmAsync: {ex.Message}");
        }
        finally
        {
            IsAddingAlgorithm = false;
            NotifyStateChanged();
        }
    }

    //
    // カード削除
    //

    public void RemoveAlgorithm(int index)
    {
        if (index < 0 || index >= _state.Instances.Count) return;

        _debug.Log($"[ComparisonMode] RemoveAlgorithm index={index} ({_state.Instances[index].AlgorithmName})");

        _playbackServices[index].StateChanged -= OnPlaybackStateChanged;
        _playbackServices[index].Dispose();
        _playbackServices.RemoveAt(index);
        _state.Instances.RemoveAt(index);

        NotifyStateChanged();
    }

    public void ClearAll()
    {
        _debug.Log($"[ComparisonMode] ClearAll: removing {_state.Instances.Count} instance(s)");
        ClearAllInstances();
        NotifyStateChanged();
    }

    //
    // 再生制御
    //

    public void Play()
    {
        _completionLogged = false;
        foreach (var p in _playbackServices)
        {
            if (!p.State.IsSortCompleted)
                p.Play();
        }
        NotifyStateChanged();
    }

    public void Pause()
    {
        foreach (var p in _playbackServices) p.Pause();
        NotifyStateChanged();
    }

    public void Stop()
    {
        foreach (var p in _playbackServices) p.Stop();
        GlobalSeekIndex = 0;
        NotifyStateChanged();
    }

    public void Reset() => Stop();

    /// <summary>
    /// 全インスタンスを指定グローバル位置にシークする。
    /// 各インスタンスの総 ops を上限としてキャップする。
    /// </summary>
    public void SeekAll(int globalIndex)
    {
        GlobalSeekIndex = globalIndex;
        foreach (var p in _playbackServices)
        {
            var target = Math.Min(globalIndex, p.State.TotalOperations);
            p.SeekTo(target, throttle: true);
        }
        NotifyStateChanged();
    }

    public bool IsPlaying() => _playbackServices.Any(p => p.State.PlaybackState == PlaybackState.Playing);

    //
    // 設定同期
    //

    public void SetSpeedForAll(int ops, double speed)
    {
        foreach (var p in _playbackServices)
        {
            p.OperationsPerFrame = ops;
            p.SpeedMultiplier = speed;
        }
        NotifyStateChanged();
    }

    public void SetAutoResetForAll(bool auto)
    {
        foreach (var p in _playbackServices) p.AutoReset = auto;
        NotifyStateChanged();
    }

    /// <summary>全インスタンスのサウンドをトグルする。</summary>
    public void ToggleSound()
    {
        if (_playbackServices.Count == 0) return;
        var newState = !_playbackServices[0].SoundEnabled;
        foreach (var p in _playbackServices)
            p.SoundEnabled = newState;
        NotifyStateChanged();
    }

    /// <summary>すべてのPlaybackServiceのサウンドボリュームを設定する。</summary>
    public void SetSoundVolumeForAll(double volume)
    {
        foreach (var p in _playbackServices)
        {
            p.SoundVolume = volume;
        }
    }

    /// <summary>すべてのPlaybackServiceのサウンドタイプを設定する。</summary>
    public async Task SetSoundTypeForAllAsync(string soundType)
    {
        foreach (var p in _playbackServices)
        {
            p.SoundType = soundType;
        }
        await _js.InvokeVoidAsync("soundEngine.setSoundType", soundType);
    }

    //
    // プライベートヘルパー
    //

    private async Task AddAlgorithmInternalAsync(string algorithmName, AlgorithmMetadata metadata)
    {
        var capturedArray = _state.InitialArray;

        // プログレス表示用に処理中のアルゴリズム名を設定
        _state.ProcessingAlgorithmName = algorithmName;
        NotifyStateChanged();

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var (operations, statistics, actualExecutionTime) =
            await _executor.ExecuteAndRecordAsync(capturedArray, metadata);
        totalStopwatch.Stop();

        if (!ReferenceEquals(capturedArray, _state.InitialArray))
        {
            _state.ProcessingAlgorithmName = null;
            return;
        }

        var playback = new PlaybackService(_js);
        playback.LoadOperations(capturedArray, operations, statistics, actualExecutionTime);
        playback.StateChanged += OnPlaybackStateChanged;

        _playbackServices.Add(playback);
        _state.Instances.Add(new ComparisonInstance
        {
            AlgorithmName = algorithmName,
            State = playback.State,
            Metadata = metadata,
            Playback = playback,
        });

        _state.ProcessingAlgorithmName = null;

        _debug.Log(
            $"[ComparisonMode] Added {algorithmName}: {operations.Count:N0} operations " +
            $"(Comparisons: {statistics.CompareCount:N0}, Swaps: {statistics.SwapCount:N0}) " +
            $"recorded in {totalStopwatch.ElapsedMilliseconds:N0}ms " +
            $"(exec: {actualExecutionTime.TotalMilliseconds:F2}ms)");
    }

    private void ClearAllInstances()
    {
        Stop();
        GlobalSeekIndex = 0;
        _completionLogged = false;
        foreach (var p in _playbackServices)
        {
            p.StateChanged -= OnPlaybackStateChanged;
            p.Dispose();
        }
        _playbackServices.Clear();
        _state.Instances.Clear();
    }

    private void OnPlaybackStateChanged()
    {
        if (_playbackServices.Count > 0)
            GlobalSeekIndex = _playbackServices.Max(p => p.State.CurrentOperationIndex);
        CheckCompletionStatus();
        NotifyStateChanged();
    }

    private void CheckCompletionStatus()
    {
        if (_state.Instances.Count == 0) return;
        var completedCount = _state.Instances.Count(x => x.State.IsSortCompleted);
        if (completedCount == _state.Instances.Count && !_completionLogged)
        {
            _completionLogged = true;
            _debug.Log($"[ComparisonMode] All {_state.Instances.Count} algorithms completed.");
            foreach (var inst in _state.Instances.OrderBy(x => x.State.CompareCount))
                _debug.Log($"  - {inst.AlgorithmName}: Cmp={inst.State.CompareCount:N0}, SWP={inst.State.SwapCount:N0}, READ={inst.State.IndexReadCount}, WRITE={inst.State.IndexWriteCount}");
            Pause();
        }
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    /// <summary>
    /// N>1 時のシークバー完了マーカー一覧を返す。
    /// 各インスタンスの完了 ops 位置にマーカーを配置する（TotalOperations が確定済みのもの）。
    /// </summary>
    public IReadOnlyList<SeekBarMarker> GetCompletionMarkers()
    {
        var result = new List<SeekBarMarker>(_state.Instances.Count);
        for (var i = 0; i < _state.Instances.Count; i++)
        {
            var p = _playbackServices[i];
            if (p.State.TotalOperations > 0)
                result.Add(new SeekBarMarker(_state.Instances[i].AlgorithmName, p.State.TotalOperations, AlgorithmColors[i % AlgorithmColors.Length]));
        }
        return result;
    }

    public void Dispose()
    {
        ClearAllInstances();
    }
}

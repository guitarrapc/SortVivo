using Microsoft.JSInterop;
using SortAlgorithm.VisualizationWeb.Models;
namespace SortAlgorithm.VisualizationWeb.Services;

public class ComparisonModeService : IDisposable
{
    private readonly SortExecutor _executor;
    private readonly DebugSettings _debug;
    private readonly IJSRuntime _js;
    private readonly ComparisonState _state = new();
    private readonly List<PlaybackService> _playbackServices = new();
    private bool _completionLogged;

    public ComparisonState State => _state;
    public event Action? OnStateChanged;

    /// <summary>
    /// 非同期処理（Add / Replace）の実行中は true。
    /// UI ボタンの disabled 制御やローディング表示に使用する。
    /// </summary>
    public bool IsAddingAlgorithm { get; private set; }

    /// <summary>N=1 のときのみ有効なサウンドの ON/OFF 状態。</summary>
    public bool SoundEnabled => _playbackServices.Count == 1 && _playbackServices[0].SoundEnabled;

    public ComparisonModeService(SortExecutor executor, DebugSettings debug, IJSRuntime js)
    {
        _executor = executor;
        _debug = debug;
        _js = js;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add & Generate（Upsert）
    // ────────────────────────────────────────────────────────────────────────

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

    // ────────────────────────────────────────────────────────────────────────
    // 追加のみ（既存配列・既存カード維持、追加分だけ計測）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 既存配列と既存カードをそのまま維持し、選択アルゴリズムだけ追加計測する。
    /// 配列サイズ・パターンが変わっていない場合の最適化パス。
    /// 呼び出し元で「アルゴリズム未登録 かつ N &lt; N_max かつ配列条件不変」を確認済みであること。
    /// </summary>
    public async Task AddAlgorithmAsync(string algorithmName, AlgorithmMetadata metadata)
    {
        if (IsAddingAlgorithm) return;
        if (_state.InitialArray.Length == 0) return;

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

    // ────────────────────────────────────────────────────────────────────────
    // インライン切替（同じ配列、N 不変）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 指定インデックスのカードのアルゴリズムを差し替える。
    /// 配列は現在の共有配列をそのまま使い、N は変わらない。
    /// </summary>
    public async Task ReplaceAlgorithmAsync(int index, string newAlgoName, AlgorithmMetadata newMetadata)
    {
        if (index < 0 || index >= _state.Instances.Count) return;
        if (_state.InitialArray.Length == 0) return;
        if (IsAddingAlgorithm) return;

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

    // ────────────────────────────────────────────────────────────────────────
    // カード削除
    // ────────────────────────────────────────────────────────────────────────

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

    // ────────────────────────────────────────────────────────────────────────
    // 再生制御
    // ────────────────────────────────────────────────────────────────────────

    public void Play()
    {
        _completionLogged = false;
        foreach (var p in _playbackServices) p.Play();
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
        NotifyStateChanged();
    }

    public void Reset() => Stop();

    public void SeekAll(int i)
    {
        foreach (var p in _playbackServices) p.SeekTo(i, false);
        NotifyStateChanged();
    }

    public bool IsPlaying() => _playbackServices.Any(p => p.State.PlaybackState == PlaybackState.Playing);

    // ────────────────────────────────────────────────────────────────────────
    // 設定同期
    // ────────────────────────────────────────────────────────────────────────

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

    /// <summary>N=1 時のみ有効。サウンドをトグルする。</summary>
    public void ToggleSound()
    {
        if (_playbackServices.Count == 1)
        {
            _playbackServices[0].SoundEnabled = !_playbackServices[0].SoundEnabled;
            NotifyStateChanged();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // プライベートヘルパー
    // ────────────────────────────────────────────────────────────────────────

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

    public void Dispose()
    {
        ClearAllInstances();
    }
}


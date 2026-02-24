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

    public ComparisonState State => _state;
    public event Action? OnStateChanged;

    /// <summary>
    /// AddAlgorithmAsync での記録処理実行中は true。
    /// UI ボタンの disabled 制御やローディング表示に使用する。
    /// </summary>
    public bool IsAddingAlgorithm { get; private set; }

    public ComparisonModeService(SortExecutor executor, DebugSettings debug, IJSRuntime js)
    {
        _executor = executor;
        _debug = debug;
        _js = js;
    }

    public void Enable(int[] initialArray, int arraySize, ArrayPatternMetadata pattern)
    {
        Stop();
        
        // すべてのPlaybackServiceのイベント購読解除
        foreach (var playback in _playbackServices)
        {
            playback.StateChanged -= OnPlaybackStateChanged;
            playback.Dispose();
        }

        _playbackServices.Clear();
        _state.Instances.Clear();
        _state.IsEnabled = true;
        _state.InitialArray = initialArray.ToArray();
        _state.CurrentArraySize = arraySize;
        _state.CurrentPattern = pattern;
        
        _debug.Log($"[ComparisonModeService] Enabled with size={arraySize}, pattern={pattern.Name}");
        NotifyStateChanged();
    }
    public void Disable()
    {
        Stop();
        
        // すべてのPlaybackServiceのイベント購読解除
        foreach (var playback in _playbackServices)
        {
            playback.StateChanged -= OnPlaybackStateChanged;
            playback.Dispose();
        }

        _playbackServices.Clear();
        _state.Instances.Clear();
        _state.IsEnabled = false;
        _state.InitialArray = Array.Empty<int>();
        _state.CurrentArraySize = 0;
        _state.CurrentPattern = null;
        IsAddingAlgorithm = false;
        NotifyStateChanged();
    }
    public void AddAlgorithm(string algorithmName, AlgorithmMetadata metadata)
    {
        if (_state.Instances.Count >= ComparisonState.MaxComparisons || _state.InitialArray.Length == 0)
            return;

        try
        {
            var (operations, statistics, actualExecutionTime) = _executor.ExecuteAndRecord(_state.InitialArray, metadata);

            var playback = new PlaybackService(_js);
            playback.LoadOperations(_state.InitialArray, operations, statistics, actualExecutionTime);
            
            // PlaybackServiceのStateChangedイベントを購読
            playback.StateChanged += OnPlaybackStateChanged;
            
            _playbackServices.Add(playback);
            _state.Instances.Add(new ComparisonInstance
            {
                AlgorithmName = algorithmName,
                State = playback.State,
                Metadata = metadata,
                Playback = playback
            });

            _debug.Log($"[ComparisonMode] Added {algorithmName}: {operations.Count} operations");
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _debug.Log($"[ComparisonMode] ERROR adding {algorithmName}: {ex.Message}");
            // エラーが発生しても他のアルゴリズムに影響しないように続行
        }
    }

    /// <summary>
    /// ソートを非同期で実行・記録してアルゴリズムを比較リストに追加する。
    /// Blazor WASM メインスレッドのフリーズを抑制するために Task.Yield() を挟む。
    /// </summary>
    /// <remarks>
    /// IsAddingAlgorithm が true の間は重複呼び出しを無視する。
    /// 非同期処理中に Disable() が呼ばれた場合は結果を破棄する。
    /// </remarks>
    public async Task AddAlgorithmAsync(string algorithmName, AlgorithmMetadata metadata)
    {
        if (_state.Instances.Count >= ComparisonState.MaxComparisons || _state.InitialArray.Length == 0)
            return;

        if (IsAddingAlgorithm)
            return;

        IsAddingAlgorithm = true;
        NotifyStateChanged();

        try
        {
            // 配列参照を記録しておき、非同期処理中に Enable/Disable されたか検出する
            var capturedArray = _state.InitialArray;

            var (operations, statistics, actualExecutionTime) =
                await _executor.ExecuteAndRecordAsync(capturedArray, metadata);

            // 非同期処理中に ComparisonMode が無効化または配列が差し替えられた場合は破棄
            if (!_state.IsEnabled || !ReferenceEquals(capturedArray, _state.InitialArray))
            {
                _debug.Log($"[ComparisonMode] State changed during async add, discarding {algorithmName}");
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
                Playback = playback
            });

            _debug.Log($"[ComparisonMode] Added (async) {algorithmName}: {operations.Count} operations");
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _debug.Log($"[ComparisonMode] ERROR adding {algorithmName}: {ex.Message}");
        }
        finally
        {
            IsAddingAlgorithm = false;
            NotifyStateChanged();
        }
    }

    public void RemoveAlgorithm(int index)
    {
        _debug.Log($"[ComparisonModeService] RemoveAlgorithm called for index: {index}");
        _debug.Log($"[ComparisonModeService] Before removal, _playbackServices.Count: {_playbackServices.Count}, _state.Instances.Count: {_state.Instances.Count}");
        
        if (index >= 0 && index < _state.Instances.Count)
        {
            var algorithmName = _state.Instances[index].AlgorithmName;
            _debug.Log($"[ComparisonModeService] Removing algorithm: {algorithmName}");
            
            try
            {
                // イベント購読解除
                _playbackServices[index].StateChanged -= OnPlaybackStateChanged;
                
                _playbackServices[index].Dispose();
                _debug.Log($"[ComparisonModeService] PlaybackService disposed successfully");
            }
            catch (Exception ex)
            {
                _debug.Log($"[ComparisonModeService] ERROR disposing PlaybackService: {ex.Message}");
            }
            
            _playbackServices.RemoveAt(index);
            _state.Instances.RemoveAt(index);
            
            _debug.Log($"[ComparisonModeService] After removal, _playbackServices.Count: {_playbackServices.Count}, _state.Instances.Count: {_state.Instances.Count}");
            
            NotifyStateChanged();
        }
        else
        {
            _debug.Log($"[ComparisonModeService] ERROR: Invalid index {index} (valid range: 0-{_state.Instances.Count - 1})");
        }
    }
    public void Play()
    {
        foreach (var p in _playbackServices)
        {
            p.Play();
        }

        NotifyStateChanged();
    }
    public void Pause()
    {
        foreach (var p in _playbackServices)
        {
            p.Pause();
        }

        NotifyStateChanged();
    }
    public void Stop()
    {
        foreach (var p in _playbackServices)
        {
            p.Stop();
        }

        NotifyStateChanged();
    }
    public void Reset() => Stop();
    public void SeekAll(int i)
    {
        foreach (var p in _playbackServices)
        {
            p.SeekTo(i, false);
        }

        NotifyStateChanged();
    }
    public void SetSpeedForAll(int ops, double speed)
    {
        _debug.Log($"[ComparisonModeService] SetSpeedForAll called with ops={ops}, speed={speed}");
        _debug.Log($"[ComparisonModeService] Applying to {_playbackServices.Count} playback services");
        
        foreach (var p in _playbackServices)
        {
            p.OperationsPerFrame = ops;
            p.SpeedMultiplier = speed;
        }

        NotifyStateChanged();
    }
    public void SetAutoResetForAll(bool auto)
    {
        foreach (var p in _playbackServices)
        {
            p.AutoReset = auto;
        }

        NotifyStateChanged();
    }
    
    public bool IsPlaying() => _playbackServices.Any(p => p.State.PlaybackState == PlaybackState.Playing);
    
    /// <summary>
    /// PlaybackServiceの状態変更を受け取り、通知を伝播
    /// </summary>
    private void OnPlaybackStateChanged()
    {
        // 完了状態をチェック
        CheckCompletionStatus();
        
        // 個々のPlaybackServiceの状態変更をComparisonModeの状態変更として通知
        NotifyStateChanged();
    }
    
    /// <summary>
    /// すべてのアルゴリズムの完了状態をチェック
    /// </summary>
    private void CheckCompletionStatus()
    {
        var completedCount = _state.Instances.Count(x => x.State.IsSortCompleted);
        var totalCount = _state.Instances.Count;
        
        if (completedCount > 0 && completedCount == totalCount)
        {
            // すべて完了した
            _debug.Log($"[ComparisonMode] 🎉 All {totalCount} algorithms completed!");
            
            // 各アルゴリズムの統計を出力
            foreach (var instance in _state.Instances.OrderBy(x => x.State.CompareCount))
            {
                _debug.Log($"  - {instance.AlgorithmName}: Compares={instance.State.CompareCount:N0}, Swaps={instance.State.SwapCount:N0}");
            }
            
            // 全アルゴリズム完了時に自動的に一時停止
            Pause();
        }
    }
    
    private void NotifyStateChanged() => OnStateChanged?.Invoke();
    
    public void Dispose()
    {
        // すべてのPlaybackServiceのイベント購読解除
        foreach (var p in _playbackServices)
        {
            p.StateChanged -= OnPlaybackStateChanged;
            p.Dispose();
        }

        _playbackServices.Clear();
        _state.Instances.Clear();
    }
}

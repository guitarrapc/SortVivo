# 比較モード（Comparison Mode）実装計画

## 📋 概要

**目的:** 複数のソートアルゴリズムを同時実行し、パフォーマンスと動作を視覚的に比較できる機能

**主要機能:**
- 複数アルゴリズムの並列可視化（1-6個）
- 統一された配列条件（同じサイズ・パターン）
- 同期された再生制御（Play/Pause/Reset/Speed）
- グリッドレイアウト表示
- 統計情報の比較表
- 個別完了検出とハイライト

**推定総工数:** 3-4日

---

## 🎯 実装フェーズ

### Phase 1: データモデルと基本構造（0.5-1日）

#### 目的
比較モードのデータ構造と状態管理の基盤を構築

#### 実装内容

**1.1 比較モード用データモデル**

新規ファイル: `Models/ComparisonState.cs`

```csharp
namespace SortAlgorithm.VisualizationWeb.Models;

/// <summary>
/// 比較モードの状態を管理
/// </summary>
public class ComparisonState
{
    /// <summary>
    /// 比較中のアルゴリズムリスト（1-6個）
    /// </summary>
    public List<AlgorithmComparisonItem> Items { get; set; } = new();
    
    /// <summary>
    /// 共通の初期配列
    /// </summary>
    public int[] InitialArray { get; set; } = Array.Empty<int>();
    
    /// <summary>
    /// 比較モードが有効か
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 最大比較可能数
    /// </summary>
    public const int MaxComparisons = 6;
    
    /// <summary>
    /// グリッド列数を計算
    /// </summary>
    public int GetGridColumns()
    {
        return Items.Count switch
        {
            1 => 1,
            2 => 2,
            3 => 2,
            4 => 2,
            5 => 3,
            6 => 3,
            _ => 1
        };
    }
    
    /// <summary>
    /// すべてのアルゴリズムが完了したか
    /// </summary>
    public bool AllCompleted => Items.All(x => x.State.IsSortCompleted);
}

/// <summary>
/// 比較対象の個別アルゴリズム情報
/// </summary>
public class AlgorithmComparisonItem
{
    public required string AlgorithmName { get; init; }
    public required VisualizationState State { get; init; }
    public required AlgorithmMetadata Metadata { get; init; }
}
```

**1.2 比較モード用サービス**

新規ファイル: `Services/ComparisonPlaybackService.cs`

```csharp
namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// 複数アルゴリズムの同期再生を管理
/// </summary>
public class ComparisonPlaybackService : IDisposable
{
    private readonly List<PlaybackService> _playbackServices = new();
    private readonly ComparisonState _comparisonState = new();
    
    public ComparisonState State => _comparisonState;
    public event Action? OnStateChanged;
    
    /// <summary>
    /// アルゴリズムを追加
    /// </summary>
    public void AddAlgorithm(string algorithmName, VisualizationState state, AlgorithmMetadata metadata)
    {
        if (_comparisonState.Items.Count >= ComparisonState.MaxComparisons)
            return;
            
        var playback = new PlaybackService();
        playback.LoadState(state);
        playback.OnStateChanged += NotifyStateChanged;
        
        _playbackServices.Add(playback);
        _comparisonState.Items.Add(new AlgorithmComparisonItem
        {
            AlgorithmName = algorithmName,
            State = state,
            Metadata = metadata
        });
        
        NotifyStateChanged();
    }
    
    /// <summary>
    /// アルゴリズムを削除
    /// </summary>
    public void RemoveAlgorithm(int index)
    {
        if (index < 0 || index >= _comparisonState.Items.Count)
            return;
            
        _playbackServices[index].Dispose();
        _playbackServices.RemoveAt(index);
        _comparisonState.Items.RemoveAt(index);
        
        NotifyStateChanged();
    }
    
    /// <summary>
    /// すべて再生（同期）
    /// </summary>
    public void PlayAll()
    {
        foreach (var playback in _playbackServices)
            playback.Play();
    }
    
    /// <summary>
    /// すべて一時停止（同期）
    /// </summary>
    public void PauseAll()
    {
        foreach (var playback in _playbackServices)
            playback.Pause();
    }
    
    /// <summary>
    /// すべてリセット（同期）
    /// </summary>
    public void ResetAll()
    {
        foreach (var playback in _playbackServices)
            playback.Reset();
    }
    
    /// <summary>
    /// すべてシーク（同期）
    /// </summary>
    public void SeekAll(int targetIndex)
    {
        foreach (var playback in _playbackServices)
            playback.SeekTo(targetIndex, throttle: false);
    }
    
    /// <summary>
    /// 再生速度を設定（すべて同期）
    /// </summary>
    public void SetSpeedForAll(int opsPerFrame, double speedMultiplier)
    {
        foreach (var playback in _playbackServices)
            playback.SetSpeed(opsPerFrame, speedMultiplier);
    }
    
    private void NotifyStateChanged() => OnStateChanged?.Invoke();
    
    public void Dispose()
    {
        foreach (var playback in _playbackServices)
            playback.Dispose();
        _playbackServices.Clear();
    }
}
```

**1.3 Program.cs への登録**

`Program.cs` に以下を追加:

```csharp
builder.Services.AddScoped<ComparisonPlaybackService>();
```

#### 完了基準
- [ ] `ComparisonState.cs` 作成完了
- [ ] `ComparisonPlaybackService.cs` 作成完了
- [ ] `Program.cs` にサービス登録完了
- [ ] ビルドエラーなし

---

### Phase 2: UI - 比較モード切り替えとアルゴリズム管理（1日）

#### 目的
比較モードのON/OFF切り替えと、アルゴリズムの追加/削除UIを実装

#### 実装内容

**2.1 比較モード切り替えUI**

`Index.razor` に比較モードトグルを追加:

```razor
@* サイドバー内の設定セクションに追加 *@
<div class="stat-item">
    <span class="stat-label">
        Comparison Mode
        <span style="font-size: 0.8em; color: #888;">
            (@(_comparisonPlayback.State.Items.Count)/@(ComparisonState.MaxComparisons))
        </span>
    </span>
    <label class="toggle-switch">
        <input type="checkbox" @bind="_isComparisonMode" @bind:after="OnComparisonModeChanged">
        <span class="toggle-slider"></span>
    </label>
</div>

@if (_isComparisonMode)
{
    <div class="comparison-controls">
        <div class="stat-item">
            <span class="stat-label">Add Algorithm</span>
            <div style="display: flex; gap: 0.5rem;">
                <select @bind="SelectedAlgorithmToAdd" style="flex: 1;">
                    @foreach (var algo in GetAvailableAlgorithmsForComparison())
                    {
                        <option value="@algo.Name">@algo.Name</option>
                    }
                </select>
                <button class="btn-icon" 
                        @onclick="AddAlgorithmToComparison"
                        disabled="@(_comparisonPlayback.State.Items.Count >= ComparisonState.MaxComparisons)"
                        title="Add Algorithm">
                    ➕
                </button>
            </div>
        </div>
        
        <div class="stat-item">
            <span class="stat-label">Algorithms in Comparison</span>
            <div class="comparison-algorithm-list">
                @for (int i = 0; i < _comparisonPlayback.State.Items.Count; i++)
                {
                    var index = i;
                    var item = _comparisonPlayback.State.Items[i];
                    <div class="comparison-algorithm-item">
                        <span class="algorithm-name" title="@item.Metadata.Category">
                            @item.AlgorithmName
                        </span>
                        <button class="btn-remove" 
                                @onclick="() => RemoveAlgorithmFromComparison(index)"
                                title="Remove">
                            ✖
                        </button>
                    </div>
                }
            </div>
        </div>
    </div>
}
```

**2.2 Index.razor コードビハインド**

```csharp
@code {
    [Inject] private ComparisonPlaybackService _comparisonPlayback { get; set; } = null!;
    
    private bool _isComparisonMode = false;
    private string SelectedAlgorithmToAdd = "BubbleSort";
    
    private async Task OnComparisonModeChanged()
    {
        if (_isComparisonMode)
        {
            // 比較モードON: 現在のアルゴリズムを最初に追加
            await AddCurrentAlgorithmToComparison();
        }
        else
        {
            // 比較モードOFF: クリーンアップ
            _comparisonPlayback.Dispose();
        }
        StateHasChanged();
    }
    
    private async Task AddCurrentAlgorithmToComparison()
    {
        // 現在選択中のアルゴリズムを実行
        await ExecuteAlgorithm();
        
        // 比較リストに追加
        var metadata = Registry.GetAlgorithmMetadata(SelectedAlgorithm);
        _comparisonPlayback.AddAlgorithm(
            SelectedAlgorithm,
            Playback.State,
            metadata
        );
    }
    
    private async Task AddAlgorithmToComparison()
    {
        if (string.IsNullOrEmpty(SelectedAlgorithmToAdd))
            return;
            
        // 同じ初期配列でアルゴリズムを実行
        var state = await Executor.ExecuteAsync(
            SelectedAlgorithmToAdd,
            _comparisonPlayback.State.InitialArray.ToArray() // コピー
        );
        
        var metadata = Registry.GetAlgorithmMetadata(SelectedAlgorithmToAdd);
        _comparisonPlayback.AddAlgorithm(
            SelectedAlgorithmToAdd,
            state,
            metadata
        );
        
        StateHasChanged();
    }
    
    private void RemoveAlgorithmFromComparison(int index)
    {
        _comparisonPlayback.RemoveAlgorithm(index);
        StateHasChanged();
    }
    
    private IEnumerable<AlgorithmMetadata> GetAvailableAlgorithmsForComparison()
    {
        // すでに追加されているアルゴリズムを除外
        var existingAlgorithms = _comparisonPlayback.State.Items
            .Select(x => x.AlgorithmName)
            .ToHashSet();
            
        return Registry.GetAlgorithms()
            .Where(x => !existingAlgorithms.Contains(x.Name));
    }
}
```

**2.3 CSS追加 (`wwwroot/css/app.css`)**

```css
/* 比較モード用スタイル */
.comparison-controls {
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid #333;
}

.comparison-algorithm-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    max-height: 300px;
    overflow-y: auto;
}

.comparison-algorithm-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.5rem;
    background: #1e1e1e;
    border-radius: 4px;
    gap: 0.5rem;
}

.comparison-algorithm-item .algorithm-name {
    flex: 1;
    font-size: 0.9em;
    color: #e0e0e0;
}

.btn-icon {
    padding: 0.25rem 0.5rem;
    background: #3B82F6;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 1em;
}

.btn-icon:hover:not(:disabled) {
    background: #2563EB;
}

.btn-icon:disabled {
    background: #555;
    cursor: not-allowed;
    opacity: 0.5;
}

.btn-remove {
    padding: 0.25rem 0.5rem;
    background: #EF4444;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.8em;
}

.btn-remove:hover {
    background: #DC2626;
}
```

#### 完了基準
- [ ] 比較モードトグルスイッチ実装完了
- [ ] アルゴリズム追加UIとロジック実装完了
- [ ] アルゴリズム削除UIとロジック実装完了
- [ ] CSS追加完了
- [ ] 動作確認（アルゴリズムの追加/削除が正常に動作）

---

### Phase 3: グリッドレイアウト表示（1日）

#### 目的
複数のアルゴリズム可視化をグリッド形式で並べて表示

#### 実装内容

**3.1 グリッドレイアウトコンポーネント**

新規ファイル: `Components/ComparisonGrid.razor`

```razor
@using SortAlgorithm.VisualizationWeb.Models

<div class="comparison-grid" 
     style="grid-template-columns: repeat(@GetGridColumns(), 1fr);">
    @for (int i = 0; i < Items.Count; i++)
    {
        var item = Items[i];
        var index = i;
        
        <div class="comparison-grid-item @(item.State.IsSortCompleted ? "completed" : "")">
            <div class="comparison-header">
                <h4 class="algorithm-title">@item.AlgorithmName</h4>
                <span class="complexity-badge">@item.Metadata.TimeComplexity</span>
            </div>
            
            @if (VisualizationMode == VisualizationMode.BarChart)
            {
                <CanvasChartRenderer 
                    State="@item.State"
                    Width="@CalculateWidth()"
                    Height="@CalculateHeight()" />
            }
            else if (VisualizationMode == VisualizationMode.Circular)
            {
                <CircularRenderer 
                    State="@item.State"
                    Size="@CalculateSize()" />
            }
            
            <ComparisonStatsSummary State="@item.State" />
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public List<AlgorithmComparisonItem> Items { get; set; } = null!;
    
    [Parameter]
    public VisualizationMode VisualizationMode { get; set; } = VisualizationMode.BarChart;
    
    [Parameter]
    public int ContainerWidth { get; set; } = 1200;
    
    [Parameter]
    public int ContainerHeight { get; set; } = 800;
    
    private int GetGridColumns()
    {
        return Items.Count switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 2,
            5 => 3,
            6 => 3,
            7 => 3,
            8 => 3,
            9 => 3,
            _ => 1
        };
    }
    
    private int CalculateWidth()
    {
        var cols = GetGridColumns();
        var gap = 20; // グリッドのgap
        var itemWidth = (ContainerWidth - (gap * (cols - 1))) / cols;
        return itemWidth - 40; // パディング分を引く
    }
    
    private int CalculateHeight()
    {
        var rows = (int)Math.Ceiling(Items.Count / (double)GetGridColumns());
        var gap = 20;
        var headerHeight = 60;
        var statsHeight = 80;
        var itemHeight = (ContainerHeight - (gap * (rows - 1))) / rows;
        return itemHeight - headerHeight - statsHeight - 40; // ヘッダー・統計・パディング分を引く
    }
    
    private int CalculateSize()
    {
        return Math.Min(CalculateWidth(), CalculateHeight());
    }
}
```

**3.2 簡易統計表示コンポーネント**

新規ファイル: `Components/ComparisonStatsSummary.razor`

```razor
@using SortAlgorithm.VisualizationWeb.Models

<div class="comparison-stats-summary">
    <div class="stat-mini">
        <span class="label">Compares:</span>
        <span class="value">@State.Statistics.CompareCount.ToString("N0")</span>
    </div>
    <div class="stat-mini">
        <span class="label">Swaps:</span>
        <span class="value">@State.Statistics.SwapCount.ToString("N0")</span>
    </div>
    <div class="stat-mini">
        <span class="label">Progress:</span>
        <span class="value">@GetProgressPercentage()%</span>
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public VisualizationState State { get; set; } = null!;
    
    private int GetProgressPercentage()
    {
        if (State.TotalOperations == 0) return 0;
        return (int)((State.CurrentOperationIndex / (double)State.TotalOperations) * 100);
    }
}
```

**3.3 CSS追加**

```css
/* 比較グリッド */
.comparison-grid {
    display: grid;
    gap: 20px;
    width: 100%;
    height: 100%;
    padding: 1rem;
}

.comparison-grid-item {
    display: flex;
    flex-direction: column;
    background: #1a1a1a;
    border-radius: 8px;
    border: 2px solid #333;
    overflow: hidden;
    transition: border-color 0.3s;
}

.comparison-grid-item.completed {
    border-color: #10B981;
}

.comparison-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.75rem 1rem;
    background: #252525;
    border-bottom: 1px solid #333;
}

.algorithm-title {
    margin: 0;
    font-size: 1em;
    color: #e0e0e0;
}

.complexity-badge {
    font-size: 0.8em;
    color: #888;
    font-family: 'Courier New', monospace;
}

.comparison-stats-summary {
    display: flex;
    justify-content: space-around;
    padding: 0.75rem;
    background: #1e1e1e;
    border-top: 1px solid #333;
    font-size: 0.85em;
}

.stat-mini {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.25rem;
}

.stat-mini .label {
    color: #888;
    font-size: 0.9em;
}

.stat-mini .value {
    color: #e0e0e0;
    font-weight: bold;
}
```

**3.4 Index.razor に統合**

```razor
@* メイン可視化エリアを比較モードで切り替え *@
<div class="visualization-area">
    @if (_isComparisonMode && _comparisonPlayback.State.Items.Any())
    {
        <ComparisonGrid 
            Items="_comparisonPlayback.State.Items"
            VisualizationMode="SelectedVisualizationMode"
            ContainerWidth="@_visualizationWidth"
            ContainerHeight="@_visualizationHeight" />
    }
    else
    {
        @* 既存の単一アルゴリズム表示 *@
        @if (SelectedVisualizationMode == VisualizationMode.BarChart)
        {
            <CanvasChartRenderer State="@Playback.State" Width="800" Height="600" />
        }
        else if (SelectedVisualizationMode == VisualizationMode.Circular)
        {
            <CircularRenderer State="@Playback.State" Size="600" />
        }
    }
</div>

@code {
    private int _visualizationWidth = 1200;
    private int _visualizationHeight = 800;
    
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // ウィンドウサイズを取得してコンテナサイズを調整
            // （JavaScript interop で実装）
        }
    }
}
```

#### 完了基準
- [ ] `ComparisonGrid.razor` 実装完了
- [ ] `ComparisonStatsSummary.razor` 実装完了
- [ ] グリッドレイアウトCSS追加完了
- [ ] `Index.razor` に統合完了
- [ ] 動作確認（1-9個のアルゴリズムが正しくグリッド表示される）
- [ ] それぞれのグリッドを同じ大きさで表示する

---

### Phase 4: 同期再生制御と統計比較（1-1.5日）

#### 目的
すべてのアルゴリズムを同期して再生制御し、統計情報を比較表示

#### 実装内容

**4.1 同期再生制御UI**

`Index.razor` の制御ボタンを比較モード対応:

```razor
@* 比較モード時は ComparisonPlaybackService を使用 *@
<div class="playback-controls">
    <button class="control-button" 
            @onclick="@(_isComparisonMode ? (EventCallback)OnComparisonPlayPause : OnPlayPause)"
            title="@(_isPlayingComparison ? "Pause" : "Play")">
        @(_isPlayingComparison ? "⏸️" : "▶️")
    </button>
    
    <button class="control-button" 
            @onclick="@(_isComparisonMode ? (EventCallback)OnComparisonReset : OnReset)"
            title="Reset">
        🔄
    </button>
</div>

@code {
    private bool _isPlayingComparison = false;
    
    private void OnComparisonPlayPause()
    {
        if (_isPlayingComparison)
        {
            _comparisonPlayback.PauseAll();
            _isPlayingComparison = false;
        }
        else
        {
            _comparisonPlayback.PlayAll();
            _isPlayingComparison = true;
        }
        StateHasChanged();
    }
    
    private void OnComparisonReset()
    {
        _comparisonPlayback.ResetAll();
        _isPlayingComparison = false;
        StateHasChanged();
    }
    
    private void OnComparisonSpeedChanged()
    {
        _comparisonPlayback.SetSpeedForAll(_operationsPerFrame, _speedMultiplier);
    }
}
```

**4.2 統計比較表**

新規ファイル: `Components/ComparisonStatsTable.razor`

```razor
@using SortAlgorithm.VisualizationWeb.Models

<div class="comparison-stats-table-container">
    <h3>📊 Statistics Comparison</h3>
    
    <button class="copy-button" @onclick="CopyTableToClipboard" title="Copy to Clipboard">
        📋 Copy Table
    </button>
    
    <table class="comparison-stats-table">
        <thead>
            <tr>
                <th>Algorithm</th>
                <th>Complexity</th>
                <th>Compares</th>
                <th>Swaps</th>
                <th>Reads</th>
                <th>Writes</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var item in Items.OrderBy(x => x.State.Statistics.CompareCount))
            {
                <tr class="@(item.State.IsSortCompleted ? "completed" : "")">
                    <td class="algorithm-name">@item.AlgorithmName</td>
                    <td class="complexity">@item.Metadata.TimeComplexity</td>
                    <td class="stat-value">@item.State.Statistics.CompareCount.ToString("N0")</td>
                    <td class="stat-value">@item.State.Statistics.SwapCount.ToString("N0")</td>
                    <td class="stat-value">@item.State.Statistics.IndexReadCount.ToString("N0")</td>
                    <td class="stat-value">@item.State.Statistics.IndexWriteCount.ToString("N0")</td>
                    <td class="status">
                        @if (item.State.IsSortCompleted)
                        {
                            <span class="status-badge completed">✅ Completed</span>
                        }
                        else
                        {
                            <span class="status-badge running">⏳ Running</span>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>

@code {
    [Parameter, EditorRequired]
    public List<AlgorithmComparisonItem> Items { get; set; } = null!;
    
    [Inject] private IJSRuntime JS { get; set; } = null!;
    
    private async Task CopyTableToClipboard()
    {
        // TSV形式でクリップボードにコピー
        var header = "Algorithm\tComplexity\tCompares\tSwaps\tReads\tWrites\tStatus";
        var rows = Items.Select(item =>
            $"{item.AlgorithmName}\t" +
            $"{item.Metadata.TimeComplexity}\t" +
            $"{item.State.Statistics.CompareCount}\t" +
            $"{item.State.Statistics.SwapCount}\t" +
            $"{item.State.Statistics.IndexReadCount}\t" +
            $"{item.State.Statistics.IndexWriteCount}\t" +
            $"{(item.State.IsSortCompleted ? "Completed" : "Running")}"
        );
        
        var tsv = header + "\n" + string.Join("\n", rows);
        
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", tsv);
    }
}
```

**4.3 CSS追加**

```css
/* 統計比較表 */
.comparison-stats-table-container {
    margin-top: 1rem;
    padding: 1rem;
    background: #1a1a1a;
    border-radius: 8px;
}

.comparison-stats-table-container h3 {
    margin: 0 0 1rem 0;
    color: #e0e0e0;
}

.copy-button {
    margin-bottom: 1rem;
    padding: 0.5rem 1rem;
    background: #3B82F6;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
}

.copy-button:hover {
    background: #2563EB;
}

.comparison-stats-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9em;
}

.comparison-stats-table th {
    background: #252525;
    color: #888;
    padding: 0.75rem;
    text-align: left;
    border-bottom: 2px solid #333;
}

.comparison-stats-table td {
    padding: 0.75rem;
    border-bottom: 1px solid #2a2a2a;
    color: #e0e0e0;
}

.comparison-stats-table tr.completed {
    background: rgba(16, 185, 129, 0.1);
}

.comparison-stats-table .stat-value {
    text-align: right;
    font-family: 'Courier New', monospace;
}

.status-badge {
    padding: 0.25rem 0.5rem;
    border-radius: 4px;
    font-size: 0.85em;
}

.status-badge.completed {
    background: #10B981;
    color: white;
}

.status-badge.running {
    background: #F59E0B;
    color: white;
}
```

**4.4 Index.razor に統計表を追加**

```razor
@* 比較モード時のみ表示 *@
@if (_isComparisonMode && _comparisonPlayback.State.Items.Any())
{
    <ComparisonStatsTable Items="_comparisonPlayback.State.Items" />
}
```

#### 完了基準
- [ ] 同期再生制御実装完了（Play/Pause/Reset/Speed）
- [ ] `ComparisonStatsTable.razor` 実装完了
- [ ] クリップボードコピー機能実装完了
- [ ] CSS追加完了
- [ ] 動作確認（すべてのアルゴリズムが同期して動作）

---

### Phase 5: 個別完了検出と最終調整（0.5日）

#### 目的
各アルゴリズムの完了を個別に検出し、すべて完了するまでハイライト維持

#### 実装内容

**5.1 個別完了検出**

`ComparisonPlaybackService.cs` に完了監視を追加:

```csharp
private System.Timers.Timer? _completionCheckTimer;

public void StartCompletionMonitoring()
{
    _completionCheckTimer = new System.Timers.Timer(100); // 100ms間隔でチェック
    _completionCheckTimer.Elapsed += CheckCompletion;
    _completionCheckTimer.Start();
}

private void CheckCompletion(object? sender, ElapsedEventArgs e)
{
    for (int i = 0; i < _playbackServices.Count; i++)
    {
        var playback = _playbackServices[i];
        var item = _comparisonState.Items[i];
        
        // 完了検出
        if (!item.State.IsSortCompleted && playback.State.CurrentOperationIndex >= playback.State.TotalOperations - 1)
        {
            item.State.IsSortCompleted = true;
            NotifyStateChanged();
        }
    }
    
    // すべて完了したら監視停止
    if (_comparisonState.AllCompleted)
    {
        _completionCheckTimer?.Stop();
    }
}

public override void Dispose()
{
    _completionCheckTimer?.Stop();
    _completionCheckTimer?.Dispose();
    base.Dispose();
}
```

**5.2 ComparisonGrid での完了ハイライト**

`ComparisonGrid.razor` でクラス適用（既に実装済み）:

```razor
<div class="comparison-grid-item @(item.State.IsSortCompleted ? "completed" : "")">
```

CSS（既に追加済み）:

```css
.comparison-grid-item.completed {
    border-color: #10B981; /* 緑色のボーダー */
}
```

**5.3 最終調整**

- [ ] エラーハンドリング追加
- [ ] パフォーマンス最適化
  - 大量のアルゴリズム（9個）でも60 FPS維持
  - ArrayPoolの再利用
- [ ] レスポンシブ対応確認
- [ ] ドキュメント更新

#### 完了基準
- [ ] 個別完了検出実装完了
- [ ] 完了ハイライトが正しく動作
- [ ] すべて完了するまでハイライト維持
- [ ] パフォーマンステスト完了（9アルゴリズム同時実行で60 FPS）
- [ ] ドキュメント更新完了

---

## 📊 実装完了チェックリスト

### Phase 1: データモデルと基本構造
- [ ] `ComparisonState.cs` 作成
- [ ] `AlgorithmComparisonItem` 作成
- [ ] `ComparisonPlaybackService.cs` 作成
- [ ] `Program.cs` にサービス登録
- [ ] ビルドエラーなし

### Phase 2: UI - 比較モード切り替えとアルゴリズム管理
- [ ] 比較モードトグルスイッチ
- [ ] アルゴリズム追加UI
- [ ] アルゴリズム削除UI
- [ ] CSS追加
- [ ] 動作確認

### Phase 3: グリッドレイアウト表示
- [ ] `ComparisonGrid.razor` 実装
- [ ] `ComparisonStatsSummary.razor` 実装
- [ ] グリッドレイアウトCSS
- [ ] `Index.razor` 統合
- [ ] 1-9個のグリッド表示確認

### Phase 4: 同期再生制御と統計比較
- [ ] 同期Play/Pause/Reset
- [ ] 同期Speed制御
- [ ] `ComparisonStatsTable.razor` 実装
- [ ] クリップボードコピー機能
- [ ] 動作確認

### Phase 5: 個別完了検出と最終調整
- [ ] 個別完了検出実装
- [ ] 完了ハイライト動作確認
- [ ] パフォーマンステスト（9アルゴリズム）
- [ ] エラーハンドリング
- [ ] ドキュメント更新

---

## 🎯 成功基準

### 機能要件
- ✅ 1-9個のアルゴリズムを同時比較可能
- ✅ すべて同じ配列条件（サイズ・パターン）
- ✅ 同期再生制御（Play/Pause/Reset/Speed）
- ✅ グリッドレイアウト表示（偶数/奇数対応）
- ✅ 統計情報の比較表表示
- ✅ クリップボードコピー対応
- ✅ 個別完了検出とハイライト

### パフォーマンス要件
- ✅ 9アルゴリズム同時実行で60 FPS維持
- ✅ ArrayPoolによるメモリ効率化
- ✅ UI更新の適切な間引き

### UX要件
- ✅ 直感的なアルゴリズム追加/削除UI
- ✅ 完了したアルゴリズムの視覚的フィードバック
- ✅ 統計情報の見やすい比較表示
- ✅ レスポンシブ対応

---

## 📝 追加実装候補（オプション）

### 優先度: 低

1. **比較結果の保存・読み込み**
   - JSON形式でエクスポート
   - 過去の比較結果をインポート

2. **ランキング表示**
   - 最速アルゴリズムを自動ハイライト
   - 各統計値でソート可能

3. **比較モード専用のシークバー**
   - 全アルゴリズムの進捗を1つのバーで表示
   - 最も遅いアルゴリズムに合わせて同期

4. **フィルタリング機能**
   - カテゴリごとに比較（例: O(n²)のみ）
   - 配列サイズの推奨に基づく自動選択

---

## 🚀 実装開始

**推奨順序:**
1. Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5

**推定総工数:** 3-4日

準備ができたら Phase 1 から開始してください！ 🎉

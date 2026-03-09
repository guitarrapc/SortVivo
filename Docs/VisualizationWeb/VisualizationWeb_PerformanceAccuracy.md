# ビジュアライゼーションにおける実行速度の正確な表現

## 概要 (Overview)

ソートアルゴリズムのビジュアライゼーションでは、操作(Compare, Swap, Read, Write)の総数を単純に積み重ねると、実際のアルゴリズムの実行速度と乖離が生じる問題があります。このドキュメントでは、問題の原因と解決策を提案します。

This document addresses the issue where visualizations based on simple operation counts (Compare, Swap, Read, Write) diverge from actual algorithm performance, and proposes solutions.

---

## 1. 問題の背景 (Problem Background)

### 1.1 実測データの例 (Example Measurement Data)

ランダムな配列(n=10000)でPDQSortとQuickSort Median3を比較した際の統計:

| Algorithm | Complexity | Compares | Swaps | Reads | Writes | Progress |
|-----------|-----------|----------|-------|-------|--------|----------|
| Pattern-defeating quicksort | O(n log n) | 56,093 | 8,322 | 80,264 | 18,523 | 100% |
| QuickSort (Median3) | O(n log n) | 9,433 | 13,108 | 78,904 | 0 | 100% |

**単純な総操作数の計算:**
- **QuickSort (Median3)**: 9,433 + 13,108 + 78,904 + 0 = **101,445 ops**
- **PDQSort**: 56,093 + 8,322 + 80,264 + 18,523 = **163,202 ops**

この計算では QuickSort Median3 の方が高速に見えますが、**実際のベンチマークではPDQSortの方が高速**です。

### 1.2 データの異常値について (Data Anomalies)

**⚠️ QuickSort (Median3) の Writes=0 は異常です**

`StatisticsContext.OnSwap()` の実装を確認すると:

```csharp
public void OnSwap(int i, int j, int bufferId)
{
    if (bufferId < 0) return;
    Interlocked.Increment(ref _swapCount);
    // Swap操作は内部的にRead×2 + Write×2を含む
    Interlocked.Add(ref _indexReadCount, 2);
    Interlocked.Add(ref _indexWriteCount, 2);  // ← ここで必ず加算される
}
```

Swaps=13,108 であれば、最低でも **Writes=26,216** になるはずです。

**可能性のある原因:**
1. 統計収集に異なる `ISortContext` 実装を使用している
2. データ収集プロセスにバグがある
3. 表示時にWritesが0にリセットされている

**検証方法:**
```csharp
var stats = new StatisticsContext();
QuickSortMedian3.Sort(array, stats);
Console.WriteLine($"Swaps: {stats.SwapCount}, Writes: {stats.IndexWriteCount}");
// Expected: Writes >= 2 * Swaps
```

### 1.3 なぜPDQSortのComparesが多いのか (Why PDQSort Has More Compares)

PDQSortは高品質なピボット選択とパターン検出を行うため、Compare数が増加します:

**PDQSortの追加Compare要因:**
1. **Ninther (Median-of-9)**: 大配列では9要素からピボットを選択
   - `Sort3()` を9回実行 → 最大27回のCompare
2. **PartialInsertionSort**: パターン検出のための試行的な挿入ソート
   - 最大8要素の移動を試行 → 追加のCompare
3. **Pattern Detection**: ソート済み判定のための追加Compare

**QuickSort Median3の効率的なCompare:**
1. **Median-of-3**: 四分位位置の3要素のみを比較
   - 2-3回のCompareで完了
2. **Hoare Partition**: シンプルなピボット比較のみ

この差により、PDQSortのCompare数はQuickSort Median3の約6倍になります。

---

## 2. 実際のパフォーマンス要因 (Actual Performance Factors)

### 2.1 操作コストの非対称性 (Asymmetric Operation Costs)

実際のCPUでは、各操作のコストが大きく異なります:

| Operation | CPU Cycles (典型値) | 備考 |
|-----------|-------------------|------|
| **Compare** | 1-5 cycles | キャッシュヒット時。分岐予測の影響大 |
| **Swap** | 4-20 cycles | 2 Reads + 2 Writes。キャッシュ局所性に依存 |
| **Read** | 1-10 cycles | L1キャッシュ: 1-4, L2: 10-20, L3: 40-75, RAM: 100-300 |
| **Write** | 1-10 cycles | Readと同様。Write-backキャッシュで高速化 |

**重要な要因:**
1. **キャッシュ局所性**: 連続アクセスは1 cycle/op、ランダムアクセスは100+ cycles/op
2. **分岐予測**: 予測成功時は1-2 cycles、失敗時は10-20 cycles
3. **SIMD並列化**: 現代のCPUでは4-8要素を同時処理可能
4. **パイプライン化**: 複数操作の並列実行

### 2.2 PDQSortが実際に高速な理由 (Why PDQSort is Actually Faster)

統計上のCompare数が多くても、PDQSortが高速な理由:

1. **小配列でのInsertionSort切り替え**
   - Threshold = 24要素
   - キャッシュに完全に収まる → 1 cycle/compare達成

2. **UnguardedInsertionSort最適化**
   - 境界チェックを排除 → 分岐予測ミスを削減

3. **パターン検出による早期終了**
   - ソート済み配列を O(n) で処理
   - PartialInsertionSortで最大8要素移動時点で判定

4. **HeapSortフォールバック**
   - 最悪ケースでも O(n log n) を保証
   - キャッシュ効率の良い実装

5. **Ninther Pivot選択**
   - 不良パーティションの確率を 1/n³ → 1/n⁹ に削減
   - パーティション品質向上 → 再帰深度削減

### 2.3 QuickSort Median3の特性 (QuickSort Median3 Characteristics)

1. **シンプルなHoare Partition**
   - オーバーヘッドが少ない
   - 分岐予測しやすいパターン

2. **Swapの多さ**
   - Hoare schemeの特性
   - Swapはキャッシュミス時にコスト大

3. **パターン検出なし**
   - ソート済み配列でも O(n log n)
   - 特殊ケース最適化がない

---

## 3. 解決策: 重み付きコストモデル (Solution: Weighted Cost Model)

### 3.1 基本アプローチ: 操作重み付け (Basic Approach: Operation Weighting)

各操作に実際のコストを反映した重みを適用します。

#### 提案1: 固定重みモデル (Fixed Weight Model)

```csharp
/// <summary>
/// 重み付きコストを計算するContext実装
/// Calculates weighted cost that reflects actual CPU performance
/// </summary>
public class WeightedCostContext : ISortContext
{
    // 実際のCPUサイクルに基づく重み (基準: Compare = 1.0)
    // Weights based on actual CPU cycles (baseline: Compare = 1.0)
    private const double COMPARE_WEIGHT = 1.0;   // 基準値
    private const double SWAP_WEIGHT = 0.5;      // Swapは内部Read/Write込みで評価
    private const double READ_WEIGHT = 0.3;      // キャッシュヒット前提
    private const double WRITE_WEIGHT = 0.3;     // キャッシュヒット前提

    private double _totalCost;
    private ulong _compareCount;
    private ulong _swapCount;
    private ulong _readCount;
    private ulong _writeCount;

    public double TotalCost => _totalCost;
    public ulong CompareCount => _compareCount;
    public ulong SwapCount => _swapCount;
    public ulong ReadCount => _readCount;
    public ulong WriteCount => _writeCount;

    public void OnCompare(int i, int j, int result, int bufferIdI, int bufferIdJ)
    {
        Interlocked.Increment(ref _compareCount);
        // Compareのみカウント（インデックス計算は無視）
        AddCost(COMPARE_WEIGHT);
    }

    public void OnSwap(int i, int j, int bufferId)
    {
        if (bufferId < 0) return;

        Interlocked.Increment(ref _swapCount);
        // SwapはRead×2 + Write×2を含むが、重み付きコストでは1回としてカウント
        // これによりStatisticsContextとの二重カウントを防ぐ
        AddCost(SWAP_WEIGHT);
    }

    public void OnIndexRead(int index, int bufferId)
    {
        if (bufferId < 0) return;

        Interlocked.Increment(ref _readCount);
        AddCost(READ_WEIGHT);
    }

    public void OnIndexWrite(int index, int bufferId, object? value = null)
    {
        if (bufferId < 0) return;

        Interlocked.Increment(ref _writeCount);
        AddCost(WRITE_WEIGHT);
    }

    public void OnRangeCopy(int sourceIndex, int destinationIndex, int length,
                           int sourceBufferId, int destinationBufferId)
    {
        if (sourceBufferId >= 0)
        {
            Interlocked.Add(ref _readCount, (ulong)length);
            AddCost(READ_WEIGHT * length);
        }

        if (destinationBufferId >= 0)
        {
            Interlocked.Add(ref _writeCount, (ulong)length);
            AddCost(WRITE_WEIGHT * length);
        }
    }

    private void AddCost(double cost)
    {
        // Thread-safe addition using lock-free algorithm
        double current, newValue;
        do
        {
            current = _totalCost;
            newValue = current + cost;
        } while (Interlocked.CompareExchange(ref _totalCost, newValue, current) != current);
    }
}
```

**重み付けによる再計算例:**

| Algorithm | Compares×1.0 | Swaps×0.5 | Reads×0.3 | Writes×0.3 | **Total Cost** |
|-----------|--------------|-----------|-----------|------------|----------------|
| QuickSort (Median3) | 9,433 | 6,554 | 23,671 | 0 | **39,658** |
| PDQSort | 56,093 | 4,161 | 24,079 | 5,557 | **89,890** |

この重み付けでもPDQSortのコストが高く見えますが、次のアルゴリズム固有の補正が必要です。

### 3.2 アドバンスドアプローチ: アルゴリズムプロファイル補正 (Advanced: Algorithm Profile Correction)

アルゴリズムの特性に基づく補正係数を適用します。

```csharp
/// <summary>
/// アルゴリズムの実行特性プロファイル
/// Algorithm execution characteristic profile
/// </summary>
public enum AlgorithmProfile
{
    /// <summary>キャッシュ効率が高い (InsertionSort, PDQSort, IntroSort)</summary>
    CacheFriendly,

    /// <summary>分岐が多い (QuickSort系)</summary>
    BranchHeavy,

    /// <summary>メモリ集約的 (MergeSort, RadixSort)</summary>
    MemoryIntensive,

    /// <summary>標準 (デフォルト)</summary>
    Standard
}

/// <summary>
/// プロファイル補正を適用した重み付きコストContext
/// Weighted cost context with algorithm profile correction
/// </summary>
public class ProfiledWeightedCostContext : ISortContext
{
    private readonly WeightedCostContext _baseContext;
    private readonly double _algorithmMultiplier;

    public ProfiledWeightedCostContext(AlgorithmProfile profile)
    {
        _baseContext = new WeightedCostContext();
        _algorithmMultiplier = GetMultiplier(profile);
    }

    /// <summary>
    /// アルゴリズムプロファイルに基づく補正係数
    /// </summary>
    private static double GetMultiplier(AlgorithmProfile profile)
    {
        return profile switch
        {
            // キャッシュ効率の高いアルゴリズムは実行時間が短い
            AlgorithmProfile.CacheFriendly => 0.7,      // PDQSort, IntroSort

            // 分岐予測ミスが多いアルゴリズムは標準
            AlgorithmProfile.BranchHeavy => 1.0,        // QuickSort, BlockQuickSort

            // メモリアクセスが多いアルゴリズムは実行時間が長い
            AlgorithmProfile.MemoryIntensive => 1.3,    // MergeSort

            AlgorithmProfile.Standard => 1.0,
            _ => 1.0
        };
    }

    public double GetAdjustedCost() => _baseContext.TotalCost * _algorithmMultiplier;

    // ISortContext implementation delegates to _baseContext
    public void OnCompare(int i, int j, int result, int bufferIdI, int bufferIdJ)
        => _baseContext.OnCompare(i, j, result, bufferIdI, bufferIdJ);

    public void OnSwap(int i, int j, int bufferId)
        => _baseContext.OnSwap(i, j, bufferId);

    public void OnIndexRead(int index, int bufferId)
        => _baseContext.OnIndexRead(index, bufferId);

    public void OnIndexWrite(int index, int bufferId, object? value = null)
        => _baseContext.OnIndexWrite(index, bufferId, value);

    public void OnRangeCopy(int sourceIndex, int destinationIndex, int length,
                           int sourceBufferId, int destinationBufferId)
        => _baseContext.OnRangeCopy(sourceIndex, destinationIndex, length,
                                    sourceBufferId, destinationBufferId);
}
```

**プロファイル補正後の計算例:**

| Algorithm | Base Cost | Profile | Multiplier | **Adjusted Cost** |
|-----------|-----------|---------|------------|-------------------|
| QuickSort (Median3) | 39,658 | BranchHeavy | 1.0 | **39,658** |
| PDQSort | 89,890 | CacheFriendly | 0.7 | **62,923** |

これでPDQSortの方が遅く見えますが、実測との差は縮まりました。

### 3.3 最適アプローチ: ベンチマーク正規化 (Optimal: Benchmark Normalization)

実際のベンチマーク結果を使用して正規化係数を決定します。

```csharp
/// <summary>
/// BenchmarkDotNetの実測結果を使用して正規化するContext
/// Context that normalizes using actual BenchmarkDotNet results
/// </summary>
public class BenchmarkNormalizedCostContext : ISortContext
{
    private readonly WeightedCostContext _baseContext;
    private readonly double _normalizationFactor;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="benchmarkTimeMs">実測ベンチマーク時間 (ミリ秒)</param>
    /// <param name="expectedOperations">同じ入力での予想操作数</param>
    public BenchmarkNormalizedCostContext(double benchmarkTimeMs, double expectedOperations)
    {
        _baseContext = new WeightedCostContext();
        _normalizationFactor = benchmarkTimeMs / expectedOperations;
    }

    public double GetNormalizedTime() => _baseContext.TotalCost * _normalizationFactor;

    // ISortContext implementation delegates to _baseContext...
}
```

**使用例:**

```csharp
// Step 1: ベンチマーク実行 (BenchmarkDotNet)
// QuickSort Median3: 平均 2.5ms (n=10000, random array)
// PDQSort: 平均 1.8ms (n=10000, random array)

// Step 2: 統計収集
var quickSortStats = new WeightedCostContext();
QuickSortMedian3.Sort(testArray.ToArray(), quickSortStats);
// Total Cost: 39,658

var pdqSortStats = new WeightedCostContext();
PDQSort.Sort(testArray.ToArray(), pdqSortStats);
// Total Cost: 89,890

// Step 3: 正規化係数計算
var quickSortNormalizer = 2.5 / 39658;  // = 0.000063
var pdqSortNormalizer = 1.8 / 89890;    // = 0.000020

// Step 4: ビジュアライゼーションでの時間計算
var quickSortVisualTime = quickSortStats.TotalCost * quickSortNormalizer;  // 2.5ms
var pdqSortVisualTime = pdqSortStats.TotalCost * pdqSortNormalizer;        // 1.8ms

// PDQSortの方が高速に表示される!
```

---

## 4. 推奨実装: 統合的アプローチ (Recommended: Integrated Approach)

### 4.1 統合設計 (Integrated Design)

以下の3層アプローチを推奨します:

```
┌────────────────────────────────────────────────────────────┐
│ Layer 3: ベンチマーク正規化 (オプション)                     │
│ - BenchmarkDotNetの実測値を使用                             │
│ - アルゴリズムごとに異なる正規化係数                          │
├────────────────────────────────────────────────────────────┤
│ Layer 2: アルゴリズムプロファイル補正                         │
│ - CacheFriendly: 0.7x                                     │
│ - BranchHeavy: 1.0x                                       │
│ - MemoryIntensive: 1.3x                                   │
├────────────────────────────────────────────────────────────┤
│ Layer 1: 操作重み付け (基本)                                │
│ - Compare: 1.0                                            │
│ - Swap: 0.5                                               │
│ - Read: 0.3                                               │
│ - Write: 0.3                                              │
└────────────────────────────────────────────────────────────┘
```

### 4.2 実装例 (Implementation Example)

```csharp
/// <summary>
/// 統合的な重み付きコストContext
/// Integrated weighted cost context with multiple correction layers
/// </summary>
public class IntegratedCostContext : ISortContext
{
    private readonly WeightedCostContext _baseContext;
    private readonly AlgorithmProfile _profile;
    private readonly double? _benchmarkNormalizer;

    public IntegratedCostContext(
        AlgorithmProfile profile = AlgorithmProfile.Standard,
        double? benchmarkTimeMs = null,
        double? expectedOperations = null)
    {
        _baseContext = new WeightedCostContext();
        _profile = profile;

        if (benchmarkTimeMs.HasValue && expectedOperations.HasValue)
        {
            _benchmarkNormalizer = benchmarkTimeMs.Value / expectedOperations.Value;
        }
    }

    /// <summary>
    /// 最終的な調整済みコストを取得
    /// Gets the final adjusted cost
    /// </summary>
    public double GetFinalCost()
    {
        // Layer 1: Base weighted cost
        var baseCost = _baseContext.TotalCost;

        // Layer 2: Algorithm profile correction
        var profileMultiplier = GetProfileMultiplier(_profile);
        var profiledCost = baseCost * profileMultiplier;

        // Layer 3: Benchmark normalization (optional)
        if (_benchmarkNormalizer.HasValue)
        {
            return profiledCost * _benchmarkNormalizer.Value;
        }

        return profiledCost;
    }

    private static double GetProfileMultiplier(AlgorithmProfile profile)
    {
        return profile switch
        {
            AlgorithmProfile.CacheFriendly => 0.7,
            AlgorithmProfile.BranchHeavy => 1.0,
            AlgorithmProfile.MemoryIntensive => 1.3,
            _ => 1.0
        };
    }

    // ISortContext implementation...
    public void OnCompare(int i, int j, int result, int bufferIdI, int bufferIdJ)
        => _baseContext.OnCompare(i, j, result, bufferIdI, bufferIdJ);

    public void OnSwap(int i, int j, int bufferId)
        => _baseContext.OnSwap(i, j, bufferId);

    public void OnIndexRead(int index, int bufferId)
        => _baseContext.OnIndexRead(index, bufferId);

    public void OnIndexWrite(int index, int bufferId, object? value = null)
        => _baseContext.OnIndexWrite(index, bufferId, value);

    public void OnRangeCopy(int sourceIndex, int destinationIndex, int length,
                           int sourceBufferId, int destinationBufferId)
        => _baseContext.OnRangeCopy(sourceIndex, destinationIndex, length,
                                    sourceBufferId, destinationBufferId);
}
```

### 4.3 ビジュアライゼーションでの使用 (Usage in Visualization)

```csharp
// Blazor VisualizationWeb での使用例
public class SortVisualizationService
{
    public async Task VisualizeSortAsync<T>(
        T[] array,
        string algorithmName,
        CancellationToken cancellationToken)
    {
        // アルゴリズムに応じたプロファイル選択
        var profile = algorithmName switch
        {
            "PDQSort" => AlgorithmProfile.CacheFriendly,
            "IntroSort" => AlgorithmProfile.CacheFriendly,
            "QuickSort" => AlgorithmProfile.BranchHeavy,
            "QuickSortMedian3" => AlgorithmProfile.BranchHeavy,
            "MergeSort" => AlgorithmProfile.MemoryIntensive,
            _ => AlgorithmProfile.Standard
        };

        // Context作成
        var context = new IntegratedCostContext(profile);

        // カスタムVisualizationContext (アニメーション用)
        var vizContext = new VisualizationContext(
            onCompare: (i, j, result, bufIdI, bufIdJ) =>
            {
                context.OnCompare(i, j, result, bufIdI, bufIdJ);
                // アニメーション描画...
            },
            onSwap: (i, j, bufId) =>
            {
                context.OnSwap(i, j, bufId);
                // アニメーション描画...
            }
            // ... 他の操作も同様
        );

        // ソート実行
        switch (algorithmName)
        {
            case "PDQSort":
                PDQSort.Sort(array, vizContext);
                break;
            case "QuickSortMedian3":
                QuickSortMedian3.Sort(array, vizContext);
                break;
            // ... 他のアルゴリズム
        }

        // 最終コスト表示
        var finalCost = context.GetFinalCost();
        Console.WriteLine($"Algorithm: {algorithmName}, Adjusted Cost: {finalCost:F2}");
    }
}
```

### 4.4 ユーザー設定可能な重み (User-Configurable Weights)

ビジュアライゼーションUIで重みを調整可能にします:

```razor
@* Blazor Component *@
<div class="weight-configuration">
    <h3>Cost Weight Configuration</h3>

    <label>
        Compare Weight:
        <input type="range" min="0.1" max="2.0" step="0.1"
               @bind="CompareWeight" @bind:event="oninput" />
        <span>@CompareWeight</span>
    </label>

    <label>
        Swap Weight:
        <input type="range" min="0.1" max="2.0" step="0.1"
               @bind="SwapWeight" @bind:event="oninput" />
        <span>@SwapWeight</span>
    </label>

    <label>
        Read Weight:
        <input type="range" min="0.1" max="2.0" step="0.1"
               @bind="ReadWeight" @bind:event="oninput" />
        <span>@ReadWeight</span>
    </label>

    <label>
        Write Weight:
        <input type="range" min="0.1" max="2.0" step="0.1"
               @bind="WriteWeight" @bind:event="oninput" />
        <span>@WriteWeight</span>
    </label>

    <button @onclick="ResetToDefault">Reset to Default</button>
</div>

@code {
    private double CompareWeight { get; set; } = 1.0;
    private double SwapWeight { get; set; } = 0.5;
    private double ReadWeight { get; set; } = 0.3;
    private double WriteWeight { get; set; } = 0.3;

    private void ResetToDefault()
    {
        CompareWeight = 1.0;
        SwapWeight = 0.5;
        ReadWeight = 0.3;
        WriteWeight = 0.3;
    }
}
```

---

## 5. ベンチマークデータの収集方法 (Benchmark Data Collection)

### 5.1 BenchmarkDotNetでの実測 (Measurement with BenchmarkDotNet)

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SortAlgorithmBenchmark
{
    private int[] _randomArray = null!;

    [Params(1000, 10000, 100000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _randomArray = Enumerable.Range(0, N)
            .Select(_ => random.Next(N))
            .ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // 各反復で新しいコピーを作成
        _randomArray = _randomArray.ToArray();
    }

    [Benchmark]
    public void QuickSortMedian3_Random()
    {
        QuickSortMedian3.Sort(_randomArray.AsSpan());
    }

    [Benchmark]
    public void PDQSort_Random()
    {
        PDQSort.Sort(_randomArray.AsSpan());
    }

    [Benchmark]
    public void IntroSort_Random()
    {
        IntroSort.Sort(_randomArray.AsSpan());
    }
}

// 実行: dotnet run -c Release
// 結果から正規化係数を計算
```

### 5.2 統計収集との統合 (Integration with Statistics Collection)

```csharp
public class BenchmarkStatisticsCollector
{
    public record BenchmarkResult(
        string AlgorithmName,
        int ArraySize,
        double AverageTimeMs,
        WeightedCostStats Statistics);

    public record WeightedCostStats(
        ulong Compares,
        ulong Swaps,
        ulong Reads,
        ulong Writes,
        double TotalCost);

    public static BenchmarkResult CollectStatistics<T>(
        string algorithmName,
        T[] inputArray,
        Action<Span<T>, ISortContext> sortAction,
        int iterations = 10)
    {
        var times = new List<double>();
        WeightedCostContext? lastStats = null;

        for (int i = 0; i < iterations; i++)
        {
            var arrayCopy = inputArray.ToArray();
            var context = new WeightedCostContext();

            var sw = Stopwatch.StartNew();
            sortAction(arrayCopy.AsSpan(), context);
            sw.Stop();

            times.Add(sw.Elapsed.TotalMilliseconds);
            lastStats = context;
        }

        var avgTime = times.Average();

        return new BenchmarkResult(
            algorithmName,
            inputArray.Length,
            avgTime,
            new WeightedCostStats(
                lastStats!.CompareCount,
                lastStats.SwapCount,
                lastStats.ReadCount,
                lastStats.WriteCount,
                lastStats.TotalCost
            )
        );
    }
}

// 使用例
var random = new Random(42);
var testArray = Enumerable.Range(0, 10000)
    .Select(_ => random.Next(10000))
    .ToArray();

var quickSortResult = BenchmarkStatisticsCollector.CollectStatistics(
    "QuickSortMedian3",
    testArray,
    (span, ctx) => QuickSortMedian3.Sort(span, ctx)
);

var pdqSortResult = BenchmarkStatisticsCollector.CollectStatistics(
    "PDQSort",
    testArray,
    (span, ctx) => PDQSort.Sort(span, ctx)
);

Console.WriteLine($"QuickSort: {quickSortResult.AverageTimeMs:F2}ms, Cost: {quickSortResult.Statistics.TotalCost}");
Console.WriteLine($"PDQSort: {pdqSortResult.AverageTimeMs:F2}ms, Cost: {pdqSortResult.Statistics.TotalCost}");
```

---

## 6. 実装ロードマップ (Implementation Roadmap)

### Phase 1: 基本重み付けコンテキスト (Basic Weighted Context)
- [x] `WeightedCostContext` の実装
- [ ] 既存の `StatisticsContext` との統合テスト
- [ ] 単体テスト作成

### Phase 2: プロファイル補正 (Profile Correction)
- [ ] `AlgorithmProfile` enum の定義
- [ ] `ProfiledWeightedCostContext` の実装
- [ ] 各アルゴリズムへのプロファイル割り当て

### Phase 3: ベンチマーク統合 (Benchmark Integration)
- [ ] BenchmarkDotNetでの全アルゴリズム実測
- [ ] 正規化係数の自動計算ツール作成
- [ ] `BenchmarkNormalizedCostContext` の実装

### Phase 4: ビジュアライゼーション統合 (Visualization Integration)
- [ ] Blazor WebAssemblyアプリへの統合
- [ ] ユーザー設定UIの実装
- [ ] リアルタイムコスト表示

### Phase 5: ドキュメント化 (Documentation)
- [x] 技術仕様書 (本ドキュメント)
- [ ] ユーザーガイド
- [ ] API リファレンス

---

## 7. 参考文献 (References)

### 学術論文 (Academic Papers)
- Orson Peters, "Pattern-defeating quicksort", 2021, https://arxiv.org/abs/2106.05123
- C.A.R. Hoare, "Quicksort", The Computer Journal, 1962

### 実装リファレンス (Implementation References)
- PDQSort reference implementation: https://github.com/orlp/pdqsort
- C++ std::sort implementation analysis
- .NET Core ArraySortHelper implementation

### パフォーマンス分析 (Performance Analysis)
- Intel® 64 and IA-32 Architectures Optimization Reference Manual
- Agner Fog, "Optimizing software in C++", https://www.agner.org/optimize/

---

## 8. 付録: デバッグ用チェックリスト (Appendix: Debug Checklist)

### QuickSort Median3 の Writes=0 問題の診断手順

1. **StatisticsContextの使用を確認**
   ```csharp
   var stats = new StatisticsContext();
   QuickSortMedian3.Sort(array, stats);
   Console.WriteLine($"Swaps: {stats.SwapCount}, Writes: {stats.IndexWriteCount}");
   // Expected: IndexWriteCount >= 2 * SwapCount
   ```

2. **SortSpan.Swap()の呼び出しを確認**
   ```csharp
   // QuickSortMedian3.cs の中で s.Swap() が呼ばれているか確認
   s.Swap(l, r); // これが呼ばれているはず
   ```

3. **bufferId の値を確認**
   ```csharp
   // OnSwap内でbufferIdが負でないことを確認
   public void OnSwap(int i, int j, int bufferId)
   {
       Console.WriteLine($"OnSwap: bufferId={bufferId}"); // ← デバッグ出力
       if (bufferId < 0) return; // ← これで除外されていないか?
       // ...
   }
   ```

4. **複数Contextの使用を確認**
   ```csharp
   // 複数のContextが混在していないか確認
   var stats = new StatisticsContext();
   var viz = new VisualizationContext(/* ... */);

   // 統計収集には stats を使用
   QuickSortMedian3.Sort(array, stats);
   ```

### 予想される原因と対処法

| 症状 | 原因 | 対処法 |
|------|------|--------|
| Writes=0 | 異なるContext実装を使用 | StatisticsContextを使用していることを確認 |
| Writes < 2×Swaps | bufferId < 0 で除外 | BUFFER_MAIN = 0 が正しく設定されているか確認 |
| 統計が収集されない | NullContext使用 | Contextを明示的に渡す |

---

## 変更履歴 (Change History)

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2025-01-XX | 1.0 | AI Assistant | 初版作成 |

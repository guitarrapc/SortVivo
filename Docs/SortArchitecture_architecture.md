# Sort Architecture 再設計案

## 設計原則

### 核心的な考え方

1. **ソート関数は純粋関数** - 静的メソッドとしてステートレスに実装
2. **状態はContextに逃がす** - 統計・描画などの状態を持つものは外部から注入されるContextが担当
3. **標準ライブラリとの一貫性** - `Array.Sort()`, `MemoryExtensions.Sort()` と同様のAPI設計

```
┌──────────────────────────────────────────────────────────────────┐
│                        呼び出し側                                 │
│  ┌──────────────┐                        ┌───────────────────┐  │
│  │ Span<T>      │                        │ Context           │  │
│  │ (入力データ)  │                        │ (統計/描画の状態) │  │
│  └──────┬───────┘                        └─────────┬─────────┘  │
│         │                                          │            │
│         │              注入（オプション）            │            │
│         ▼                    ▼                     ▼            │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  BubbleSort.Sort<T>(span)                               │    │
│  │  BubbleSort.Sort<T, TContext>(span, ref context)        │    │
│  │  ──────────────────────────────────────────────────     │    │
│  │  • 静的メソッド（インスタンス不要）                       │    │
│  │  • 内部状態なし（純粋関数）                              │    │
│  │  • Context経由で統計/描画をフック                        │    │
│  └─────────────────────────────────────────────────────────┘    │
│         │                                          │            │
│         ▼                                          ▼            │
│  ┌──────────────┐                        ┌───────────────────┐  │
│  │ Span<T>      │                        │ Context           │  │
│  │ (ソート済み)  │                        │ (統計結果を保持)  │  │
│  └──────────────┘                        └───────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 責務の分離

| コンポーネント | 責務 | 状態 |
|--------------|------|------|
| ソート関数 | データの並べ替え | **なし（ステートレス）** |
| Context | 統計収集・描画コールバック | **あり（ミュータブル）** |
| 呼び出し側 | Contextの生成・管理 | Contextを所有 |

---

## 現状の課題

```csharp
// 現在の実装：インスタンスベース、状態が内部に密結合
var sort = new BubbleSort<int>();  // インスタンス化が必要
sort.Sort(array);
var stats = sort.Statistics;       // 状態がソートクラス内部に存在
```

**問題点：**
1. インスタンス化が必要（静的メソッドであるべき）
2. 統計情報がソートクラス内部に密結合
3. 拡張（ビジュアライズ等）が困難

---

## 採用設計：Class-based Context + SortSpan

classベースのContextパターンと `SortSpan<T>` ref struct を組み合わせた設計。
シンプルなAPIと拡張性、そして現行コードに近い書き心地を両立。

### 設計方針

- **Context は class** - `ref` 渡し不要でAPIがシンプル
- **NullContext.Default** - シングルトンで何もしない（パフォーマンス優先）
- **CompositeContext** - 複数のContextを組み合わせ可能（統計+描画など）
- **SortSpan<T>** - Span + Context をラップし、現行の `SortBase<T>` に近い書き心地を提供
- **IndexRead / IndexWrite の分離** - 読み取りと書き込みを分けて統計収集

### ISortContext インターフェース

```csharp
namespace SortAlgorithm.Contexts;

public interface ISortContext
{
    /// <summary>
    /// Handles the result of comparing two elements at the specified indices.
    /// </summary>
    /// <param name="i">Index of the compare from</param>
    /// <param name="j">Index of the compare to</param>
    /// <param name="result">The result of the comparison. negative(-) if the first element is less than the second,
    /// zero(0) if they are equal, and positive(+) if the first is greater than the second.</param>
    void OnCompare(int i, int j, int result);

    /// <summary>
    /// Handles the swapping of two elements at the specified indices.
    /// </summary>
    /// <param name="i">Index of the swap from</param>
    /// <param name="j">Index of the swap to</param>
    void OnSwap(int i, int j);

    /// <summary>
    /// Handles the event when an item at the specified index is read.
    /// </summary>
    /// <param name="index">The zero-based index of the item that was read.</param>
    void OnIndexRead(int index);

    /// <summary>
    /// Handles a write operation at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the write operation occurs.</param>
    void OnIndexWrite(int index);
}
```

### Context 実装

#### NullContext（No-op、シングルトン）

```csharp
namespace SortAlgorithm.Contexts;

public sealed class NullContext : ISortContext
{
    public static readonly NullContext Default = new();

    private NullContext() { }

    public void OnCompare(int i, int j, int result) { }
    public void OnSwap(int i, int j) { }
    public void OnIndexRead(int index) { }
    public void OnIndexWrite(int index) { }
}
```

#### StatisticsContext（統計収集）

```csharp
namespace SortAlgorithm.Contexts;

public sealed class StatisticsContext : ISortContext
{
    public ulong CompareCount => _compareCount;
    private ulong _compareCount;

    public ulong SwapCount => _swapCount;
    private ulong _swapCount;

    public ulong IndexReadCount => _indexReadCount;
    private ulong _indexReadCount;

    public ulong IndexWriteCount => _indexWriteCount;
    private ulong _indexWriteCount;

    public void OnCompare(int i, int j, int result) => Interlocked.Increment(ref _compareCount);
    public void OnSwap(int i, int j) => Interlocked.Increment(ref _swapCount);
    public void OnIndexRead(int index) => Interlocked.Increment(ref _indexReadCount);
    public void OnIndexWrite(int index) => Interlocked.Increment(ref _indexWriteCount);

    public void Reset()
    {
        _compareCount = 0;
        _swapCount = 0;
        _indexReadCount = 0;
        _indexWriteCount = 0;
    }
}
```

#### VisualizationContext（描画用）

```csharp
namespace SortAlgorithm.Contexts;

public sealed class VisualizationContext : ISortContext
{
    private readonly Action<int, int, int>? _onCompare;
    private readonly Action<int, int>? _onSwap;
    private readonly Action<int>? _onIndexRead;
    private readonly Action<int>? _onIndexWrite;

    public VisualizationContext(
        Action<int, int, int>? onCompare = null,
        Action<int, int>? onSwap = null,
        Action<int>? onIndexRead = null,
        Action<int>? onIndexWrite = null)
    {
        _onCompare = onCompare;
        _onSwap = onSwap;
        _onIndexRead = onIndexRead;
        _onIndexWrite = onIndexWrite;
    }

    public void OnCompare(int i, int j, int result) => _onCompare?.Invoke(i, j, result);
    public void OnSwap(int i, int j) => _onSwap?.Invoke(i, j);
    public void OnIndexRead(int index) => _onIndexRead?.Invoke(index);
    public void OnIndexWrite(int index) => _onIndexWrite?.Invoke(index);
}
```

#### CompositeContext（複合）

```csharp
namespace SortAlgorithm.Contexts;

public sealed class CompositeContext : ISortContext
{
    private readonly ISortContext[] _contexts;

    public CompositeContext(params ISortContext[] contexts)
    {
        _contexts = contexts;
    }

    public void OnCompare(int i, int j, int result)
    {
        foreach (var context in _contexts)
            context.OnCompare(i, j, result);
    }

    public void OnSwap(int i, int j)
    {
        foreach (var context in _contexts)
            context.OnSwap(i, j);
    }

    public void OnIndexRead(int index)
    {
        foreach (var context in _contexts)
            context.OnIndexRead(index);
    }

    public void OnIndexWrite(int index)
    {
        foreach (var context in _contexts)
            context.OnIndexWrite(index);
    }
}
```

### SortSpan<T>（Span + Context ラッパー）

現行の `SortBase<T>` に近い書き心地を提供する ref struct。

```csharp
namespace SortAlgorithm.Algorithms;

internal ref struct SortSpan<T, TComparer>(Span<T> span, ISortContext context, TComparer comparer, int bufferId)
    where TComparer : IComparer<T>
{
    private Span<T> _span = span;
    private readonly ISortContext _context = context;

    public int Length => _span.Length;

    /// <summary>
    /// Retrieves the element at the specified index. (Equivalent to span[i].)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read(int i)
    {
        _context.OnIndexRead(i);
        return _span[i];
    }

    /// <summary>
    /// Sets the element at the specified index. (Equivalent to span[i] = value.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int i, T value)
    {
        _context.OnIndexWrite(i);
        _span[i] = value;
    }

    /// <summary>
    /// Compares the elements at the specified indices. (Equivalent to span[i].CompareTo(span[j]).)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(int i, int j)
    {
        var a = Read(i);
        var b = Read(j);
        var result = a.CompareTo(b);
        _context.OnCompare(i, j, result);
        return result;
    }

    /// <summary>
    /// Exchanges the values at the specified indices. (Equivalent to swapping span[i] and span[j].)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(int i, int j)
    {
        var a = Read(i);
        var b = Read(j);
        _context.OnSwap(i, j);
        Write(i, b);
        Write(j, a);
    }
}
```

### ソートアルゴリズム実装例

```csharp
namespace SortAlgorithm.Algorithms;

public static class BubbleSort
{
    public static void Sort<T>(Span<T> span)
        => Sort(span, new ComparableComparer<T>(), NullContext.Default);

    public static void Sort<T>(Span<T> span, ISortContext context)
        => Sort(span, new ComparableComparer<T>(), context);

    public static void Sort<T, TComparer>(Span<T> span, TComparer comparer, ISortContext context)
        where TComparer : IComparer<T>
    {
        var s = new SortSpan<T, TComparer>(span, context, comparer, 0);

        for (var i = 0; i < s.Length; i++)
        {
            for (var j = s.Length - 1; j > i; j--)
            {
                // SortSpan経由で操作 - 現行のSortBase<T>に近い書き心地
                if (s.Compare(j, j - 1) < 0)
                {
                    s.Swap(j, j - 1);
                }
            }
        }
    }
}
```

### 使用例

```csharp
// 1. シンプルな使用（統計なし）
BubbleSort.Sort<int>(array);

// 2. 統計収集
var stats = new StatisticsContext();
BubbleSort.Sort(array.AsSpan(), stats);
Console.WriteLine($"Comparisons: {stats.CompareCount}, Swaps: {stats.SwapCount}");
Console.WriteLine($"Reads: {stats.IndexReadCount}, Writes: {stats.IndexWriteCount}");

// 3. ビジュアライズ（描画）
var viz = new VisualizationContext(
    onCompare: (i, j, result) => HighlightCompare(i, j),
    onSwap: (i, j) => AnimateSwap(i, j),
    onIndexRead: (index) => HighlightRead(index),
    onIndexWrite: (index) => HighlightWrite(index)
);
BubbleSort.Sort(array.AsSpan(), viz);

// 4. 統計 + 描画を同時に（CompositeContext）
var stats = new StatisticsContext();
var viz = new VisualizationContext(onSwap: (i, j) => Render(i, j));
var composite = new CompositeContext(stats, viz);
BubbleSort.Sort(array.AsSpan(), composite);
Console.WriteLine($"Swaps: {stats.SwapCount}");

// 5. 並行実行（各スレッドが独自のContextを持つ）
Parallel.ForEach(arrays, array =>
{
    var localStats = new StatisticsContext();
    BubbleSort.Sort(array.AsSpan(), localStats);
    // localStatsは各スレッドで独立、Interlocked使用でスレッドセーフ
});
```

### パフォーマンス特性

| Context | 仮想呼び出し | 実行時オーバーヘッド | 備考 |
|---------|------------|-------------------|------|
| `NullContext.Default` | あり | 最小 | 空メソッドのみ、シングルトン |
| `StatisticsContext` | あり | 小 | Interlocked.Increment |
| `VisualizationContext` | あり | 中 | コールバック実行 |
| `CompositeContext` | あり | 中〜大 | 複数Context呼び出し |

**Note：**
- 仮想呼び出し（vtable lookup）のコストは存在するが、ソートの比較・スワップ回数に比べれば微小
- パフォーマンスクリティカルな場合は `NullContext.Default` で統計なしにする
- JITのdevirtualization最適化により、特定条件下ではさらに最適化される可能性あり
- `StatisticsContext` は `Interlocked` を使用しスレッドセーフ

---

## ファイル構成

```
src/SortAlgorithm/
├── Contexts/
│   ├── ISortContext.cs             # インターフェース
│   ├── NullContext.cs              # No-op（シングルトン class）
│   ├── StatisticsContext.cs        # 統計収集（class）
│   ├── VisualizationContext.cs     # 描画用（class）
│   └── CompositeContext.cs         # 複合（class）
├── Algorithms/
│   ├── SortSpan.cs                 # Span + Context ラッパー（ref struct）
│   ├── Exchange/
│   │   ├── BubbleSort.cs           # static class
│   │   └── ...
│   ├── Partition/
│   │   ├── QuickSort.cs
│   │   └── ...
│   └── ...
└── SortMethod.cs
```

---

## 設計検討の経緯

### ヘルパーメソッド（Compare, Swap, Read, Write）の設計案

静的メソッド化に伴い、Contextを毎回引数で渡す必要がある問題に対して、以下の3案を検討しました。

**→ 案B（SortSpan）を採用**

#### 案A: 静的ヘルパークラス（毎回渡す）

最もシンプルな方法。Contextを毎回渡す。

```csharp
internal static class SortHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare<T, TComparer>(Span<T> span, int i, int j, TComparer comparer, ISortContext context)
        where TComparer : IComparer<T>
    {
        context.OnIndexRead(i);
        context.OnIndexRead(j);
        var result = comparer.Compare(span[i], span[j]);
        context.OnCompare(i, j, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(Span<T> span, int i, int j, ISortContext context)
    {
        context.OnIndexRead(i);
        context.OnIndexRead(j);
        context.OnSwap(i, j);
        context.OnIndexWrite(i);
        context.OnIndexWrite(j);
        (span[i], span[j]) = (span[j], span[i]);
    }
}

// 使用例
if (SortHelper.Compare(span, j, j - 1, context) < 0)
{
    SortHelper.Swap(span, j, j - 1, context);
}
```

**メリット：**
- ✅ 追加の型が不要
- ✅ シンプル

**デメリット：**
- ⚠️ 冗長（毎回contextを書く）

---

#### 案B: SortSpan（ref struct でラップ）【採用】

`Span<T>` と `ISortContext` をまとめたラッパーを作る。現在の `SortBase<T>` に近い書き心地。

```csharp
internal ref struct SortSpan<T, TComparer>(Span<T> span, ISortContext context, TComparer comparer, int bufferId)
    where TComparer : IComparer<T>
{
    private Span<T> _span = span;
    private readonly ISortContext _context = context;
    private readonly TComparer _comparer = comparer;
    private readonly int _bufferId = bufferId;

    public int Length => _span.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read(int i)
    {
        _context.OnIndexRead(i, _bufferId);
        return _span[i];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int i, T value)
    {
        _context.OnIndexWrite(i, _bufferId);
        _span[i] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(int i, int j)
    {
        var a = Read(i);
        var b = Read(j);
        var result = _comparer.Compare(a, b);
        _context.OnCompare(i, j, result, _bufferId, _bufferId);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(int i, int j)
    {
        var a = Read(i);
        var b = Read(j);
        _context.OnSwap(i, j);
        Write(i, b);
        Write(j, a);
    }
}

// 使用例
var s = new SortSpan<T>(span, context);
if (s.Compare(j, j - 1) < 0)
{
    s.Swap(j, j - 1);
}
```

**メリット：**
- ✅ 現在の `SortBase<T>` に近い書き心地
- ✅ `context` を毎回書かなくていい
- ✅ `ref struct` なのでヒープ割り当てなし
- ✅ 責務の分離が維持される（観察はContext、操作+通知はSortSpan）
- ✅ Read/Write の分離で詳細な統計収集が可能

**デメリット：**
- ⚠️ `ref struct` の制約（async/await不可、クロージャに入れられない等）
- ⚠️ 追加の型が増える

---

#### 案C: Contextに操作を持たせる（逆転の発想）

Contextが観察だけでなく操作も担当する設計。

```csharp
public interface ISortContext
{
    int Compare<T, TComparer>(Span<T> span, int i, int j, TComparer comparer) where TComparer : IComparer<T>;
    void Swap<T>(Span<T> span, int i, int j);
    T Read<T>(Span<T> span, int index);
    void Write<T>(Span<T> span, int index, T value);
}

public sealed class NullContext : ISortContext
{
    public static readonly NullContext Default = new();
    private NullContext() { }

    public int Compare<T, TComparer>(Span<T> span, int i, int j, TComparer comparer) where TComparer : IComparer<T>
        => comparer.Compare(span[i], span[j]);

    public void Swap<T>(Span<T> span, int i, int j)
        => (span[i], span[j]) = (span[j], span[i]);

    public T Read<T>(Span<T> span, int index) => span[index];
    public void Write<T>(Span<T> span, int index, T value) => span[index] = value;
}

// ビジュアライズ用（操作を委譲 + 観察、デコレータパターン）
public sealed class VisualizationContext : ISortContext
{
    readonly ISortContext _inner;
    readonly Action<int, int, int>? _onCompare;
    readonly Action<int, int>? _onSwap;

    public VisualizationContext(ISortContext? inner = null, ...)
    {
        _inner = inner ?? NullContext.Default;
        // ...
    }

    public int Compare<T, TComparer>(Span<T> span, int i, int j, TComparer comparer) where TComparer : IComparer<T>
    {
        var result = _inner.Compare(span, i, j, comparer);
        _onCompare?.Invoke(i, j, result);
        return result;
    }
    // ...
}

// 使用例
if (ctx.Compare(span, j, j - 1) < 0)
{
    ctx.Swap(span, j, j - 1);
}
```

**メリット：**
- ✅ 操作と観察が一体化（シンプル）
- ✅ 追加の型が少ない
- ✅ デコレータパターンで組み合わせ可能

**デメリット：**
- ⚠️ Contextの責務が大きくなる（観察だけでなく操作も担当）
- ⚠️ インターフェースが大きくなる

---

#### 比較表

| 観点 | 案A: 毎回渡す | 案B: SortSpan【採用】 | 案C: Context操作 |
|------|-------------|----------------------|-----------------|
| アルゴリズム実装のクリーンさ | △ 冗長 | ◎ 現行に近い | ○ |
| 追加の型 | なし | SortSpan | なし |
| 責務の分離 | ◎ | ◎ | △ 大きくなる |
| 組み合わせ（統計+描画） | CompositeContext | CompositeContext | デコレータ |
| 現行コードとの類似性 | △ | ◎ 最も近い | ○ |

---

### アルゴリズム情報（名前、SortMethod）

**解決策：** 属性でメタデータを付与

```csharp
[SortAlgorithm(SortMethod.Exchange, "BubbleSort")]
public static class BubbleSort
{
    // ...
}
```

### 既存テストとの互換性

**解決策：** 旧APIをラッパーとして残す（非推奨マーク付き）

```csharp
[Obsolete("Use static BubbleSort.Sort() instead")]
public class BubbleSortLegacy<T> : ISort<T>
{
    public void Sort(Span<T> span) => BubbleSort.Sort(span);
}
```

---

## 採用設計まとめ

| 項目 | 決定 |
|------|------|
| **Context の型** | class（シングルトン + 通常インスタンス） |
| **NullContext** | `NullContext.Default` シングルトン |
| **複合Context** | `CompositeContext` で複数組み合わせ可能 |
| **ソートAPI** | `Sort<T>(Span<T>)` と `Sort<T>(Span<T>, ISortContext)` |
| **ヘルパー** | `SortSpan<T>` ref struct（案B採用） |
| **インデックスアクセス** | `OnIndexRead` / `OnIndexWrite` に分離 |
| **ビジュアライズ対応** | 対応（`VisualizationContext` で位置情報取得可能） |
| **スレッドセーフ** | `StatisticsContext` は `Interlocked` 使用 |

---

## 次のステップ

1. `ISortContext` インターフェースと各Context実装を作成
2. `SortHelper` 静的クラスを作成
3. `BubbleSort` を新設計で実装
4. ベンチマークで現行版との比較
5. 他アルゴリズムへの展開
6. 旧API（`SortBase<T>`, `ISort<T>`）の非推奨化・削除

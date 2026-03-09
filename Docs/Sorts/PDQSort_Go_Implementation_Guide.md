# PDQSort Go準拠実装ガイド

## 実装時の注意点とトラブルシューティング

このドキュメントは、PDQSortをGo準拠に移行する際の具体的な実装手順、注意点、およびトラブルシューティング方法を記載します。

## 1. 実装の段階的アプローチ

### Phase 1: ChoosePivot関数の分離

#### 目的
- Pivot選択ロジックを独立した関数に分離
- インデックスベースの管理に移行
- Swap回数のカウントによるソート済み検出

#### 実装手順

**Step 1.1: SortedHint列挙型の追加**

```csharp
private enum SortedHint
{
    Unknown = 0,
    Increasing = 1,
    Decreasing = 2
}
```

**Step 1.2: Sort3WithSwapCount関数の追加**

```csharp
private static void Sort3WithSwapCount<T, TComparer>(
    SortSpan<T, TComparer> s, int a, int b, int c, ref int swaps)
    where TComparer : IComparer<T>
{
    if (s.Compare(b, a) < 0) { s.Swap(a, b); swaps++; }
    if (s.Compare(c, b) < 0) { s.Swap(b, c); swaps++; }
    if (s.Compare(b, a) < 0) { s.Swap(a, b); swaps++; }
}
```

**Step 1.3: MedianAdjacent関数の追加**

⚠️ **注意:** この関数は`pos - 1`と`pos + 1`にアクセスするため、呼び出し側で範囲チェックが必要

```csharp
private static int MedianAdjacent<T, TComparer>(
    SortSpan<T, TComparer> s, int pos, ref int swaps)
    where TComparer : IComparer<T>
{
    // 範囲チェック: pos-1 >= begin && pos+1 < end
    Sort3WithSwapCount(s, pos - 1, pos, pos + 1, ref swaps);
    return pos;  // 中央値はpos位置に入る
}
```

**Step 1.4: ChoosePivot関数の実装**

```csharp
private static (int pivot, SortedHint hint) ChoosePivot<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end)
    where TComparer : IComparer<T>
{
    const int MaxSwaps = 4 * 3;  // 3回のmedian x 各4 swap
    var size = end - begin;
    var swaps = 0;

    // 1/4, 1/2, 3/4の位置を計算
    int i = begin + size / 4;
    int j = begin + size / 2;
    int k = begin + (size / 4) * 3;

    if (size >= 8)
    {
        if (size > NintherThreshold)  // 128
        {
            // ⚠️ 範囲チェック: i, j, k が begin+1以上、end-2以下であることを確認
            // size > 128 なら、i >= begin + 32, k <= end - 32 なので安全
            i = MedianAdjacent(s, i, ref swaps);
            j = MedianAdjacent(s, j, ref swaps);
            k = MedianAdjacent(s, k, ref swaps);
        }
        // i, j, kの中央値をjに格納
        Sort3WithSwapCount(s, i, j, k, ref swaps);
    }

    // Swap回数からhintを判断
    var hint = swaps switch
    {
        0 => SortedHint.Increasing,  // 一度もSwapしていない = ソート済み
        MaxSwaps => SortedHint.Decreasing,  // 最大回数Swap = 逆順
        _ => SortedHint.Unknown
    };

    return (j, hint);  // pivotインデックスとhintを返す
}
```

**テスト:**

```csharp
// ソート済み配列
var sorted = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
var s = new SortSpan<int, Comparer<int>>(sorted, Comparer<int>.Default, context, 0);
var (pivot, hint) = ChoosePivot(s, 0, 10);
// Expected: hint == SortedHint.Increasing

// 逆順配列
var reversed = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
var s2 = new SortSpan<int, Comparer<int>>(reversed, Comparer<int>.Default, context, 0);
var (pivot2, hint2) = ChoosePivot(s2, 0, 10);
// Expected: hint2 == SortedHint.Decreasing
```

### Phase 2: 状態フラグの導入

#### 目的
- `wasBalanced`, `wasPartitioned`フラグを導入
- ループの状態を追跡し、最適化の判断材料にする

#### 実装手順

**Step 2.1: PDQSortLoop関数の修正**

```csharp
private static void PDQSortLoop<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int badAllowed,
    bool leftmost, ISortContext context)
    where TComparer : IComparer<T>
{
    // 状態フラグの初期化
    var wasBalanced = true;      // 前回のpartitionがバランスしていたか
    var wasPartitioned = true;   // 前回のpartitionが既にソート済みだったか

    while (true)
    {
        var size = end - begin;

        // Insertion sort threshold check
        if (size < InsertionSortThreshold) {
            // ...
            return;
        }

        // ... ChoosePivot呼び出し

        // Partition後に状態を更新
        var (pivotPos, alreadyPartitioned) = PartitionRight(s, begin, end, pivot);
        wasPartitioned = alreadyPartitioned;  // ← 状態を保存

        // Balance check
        var lSize = pivotPos - begin;
        var rSize = end - (pivotPos + 1);
        var balanceThreshold = size / 8;

        if (lSize < balanceThreshold || rSize < balanceThreshold) {
            wasBalanced = false;  // ← 状態を保存
            // ...
        } else {
            wasBalanced = true;
        }

        // ⚠️ 注意: 再帰呼び出し時、状態フラグをどう扱うか？
        // オプション1: 新しいフラグで再帰（Go実装）
        // オプション2: フラグをリセット
    }
}
```

**課題:**
- 再帰呼び出し時の状態フラグの扱い
- Goでは各再帰レベルで独立した状態フラグを持つ
- C#では関数引数として渡すか、ローカル変数で管理

### Phase 3: Partition前のソート済み検出

#### 目的
- ソート済み配列を早期に検出し、Swapを避ける
- Partial insertion sortをPartition前に実行

#### 実装手順

**Step 3.1: メインループの修正**

```csharp
while (true)
{
    // ... size check, pattern breaking

    // Pivot選択
    var (pivot, hint) = ChoosePivot(s, begin, end);

    // 逆順検出と反転
    if (hint == SortedHint.Decreasing) {
        ReverseRange(s, begin, end);
        // ⚠️ 注意: pivotインデックスの再計算が必要
        pivot = (end - 1) - (pivot - begin);
        hint = SortedHint.Increasing;
    }

    // ★ ソート済み検出（Partition前）★
    if (wasBalanced && wasPartitioned && hint == SortedHint.Increasing) {
        // Partial insertion sortを試行
        if (PartialInsertionSort(s, begin, end)) {
            return;  // ← Swapなしで完了！
        }
    }

    // 等値要素の最適化
    // ⚠️ 注意: pivotは値ではなくインデックス
    if (!leftmost && s.Compare(begin - 1, s.Read(pivot)) >= 0) {
        begin = PartitionLeft(s, begin, end, pivot) + 1;
        continue;
    }

    // Partitioning
    var (pivotPos, alreadyPartitioned) = PartitionRight(s, begin, end, pivot);
    // ...
}
```

**Step 3.2: ReverseRange関数の追加**

```csharp
private static void ReverseRange<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end)
    where TComparer : IComparer<T>
{
    var i = begin;
    var j = end - 1;
    while (i < j)
    {
        s.Swap(i, j);
        i++;
        j--;
    }
}
```

### Phase 4: PartitionRight/Left関数の修正

#### 目的
- Pivot インデックスを引数として受け取る
- 関数内でpivotをbeginにSwap

#### 実装手順

**Step 4.1: PartitionRight関数の修正**

```csharp
private static (int pivotPos, bool alreadyPartitioned) PartitionRight<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int pivotIndex)
    where TComparer : IComparer<T>
{
    // ⚠️ 重要: pivotIndexをbeginにSwap
    s.Swap(begin, pivotIndex);

    // Pivot値を読み取り
    var pivot = s.Read(begin);

    // ... 既存のpartitioning logic（変更なし）

    var first = begin;
    var last = end;

    while (s.Compare(++first, pivot) < 0) { }

    if (first - 1 == begin) {
        while (first < last && s.Compare(--last, pivot) >= 0) { }
    } else {
        while (s.Compare(--last, pivot) >= 0) { }
    }

    var alreadyPartitioned = first >= last;

    while (first < last) {
        s.Swap(first, last);
        while (s.Compare(++first, pivot) < 0) { }
        while (s.Compare(--last, pivot) >= 0) { }
    }

    var pivotPos = first - 1;
    s.Write(begin, s.Read(pivotPos));
    s.Write(pivotPos, pivot);

    return (pivotPos, alreadyPartitioned);
}
```

**Step 4.2: PartitionLeft関数の修正**

同様に、pivotIndexを受け取り、最初にSwapを実行:

```csharp
private static int PartitionLeft<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int pivotIndex)
    where TComparer : IComparer<T>
{
    s.Swap(begin, pivotIndex);
    var pivot = s.Read(begin);

    // ... 既存のpartitioning logic
}
```

### Phase 5: Pattern Breaking関数の追加

#### 目的
- Bad partition検出時にパターンを破壊
- Go実装では前回のpartitionがunbalancedだった場合、次のpartition前に実行

#### 実装手順

**Step 5.1: XorShift PRNGの実装**

```csharp
private struct XorShift
{
    private ulong _state;

    public XorShift(int seed)
    {
        _state = (ulong)seed + 1;  // ゼロを避ける
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;
        return _state;
    }
}
```

**Step 5.2: NextPowerOfTwo関数**

```csharp
private static int NextPowerOfTwo(int n)
{
    n--;
    n |= n >> 1;
    n |= n >> 2;
    n |= n >> 4;
    n |= n >> 8;
    n |= n >> 16;
    n++;
    return n;
}
```

**Step 5.3: BreakPatterns関数**

```csharp
private static void BreakPatterns<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end)
    where TComparer : IComparer<T>
{
    var length = end - begin;
    if (length >= 8)
    {
        var random = new XorShift(length);
        var modulus = (ulong)NextPowerOfTwo(length);

        // 中央付近の3要素をランダムな位置とSwap
        for (var idx = begin + (length / 4) * 2 - 1;
             idx <= begin + (length / 4) * 2 + 1;
             idx++)
        {
            var other = (int)(random.Next() & (modulus - 1));
            if (other >= length) {
                other -= length;
            }
            s.Swap(idx, begin + other);
        }
    }
}
```

**Step 5.4: メインループでの使用**

```csharp
while (true)
{
    // ... size check

    // ★ Pattern breaking（前回がunbalancedだった場合）★
    if (!wasBalanced) {
        BreakPatterns(s, begin, end);
        badAllowed--;
    }

    // ... pivot選択、partitioning
}
```

## 2. よくある実装エラーとその対処法

### エラー1: IndexOutOfRangeException in MedianAdjacent

**症状:**
```
System.IndexOutOfRangeException: Index was outside the bounds of the array.
  at MedianAdjacent(...) line: Sort3WithSwapCount(s, pos - 1, pos, pos + 1, ref swaps)
```

**原因:**
- `pos - 1` が `begin` より小さい
- `pos + 1` が `end` 以上

**対処法:**

```csharp
// ChoosePivot内で範囲を確認
if (size > NintherThreshold)
{
    // サイズチェック: size > 128なら安全
    // i = begin + size/4 >= begin + 32
    // k = begin + 3*size/4 <= end - 32
    Debug.Assert(i >= begin + 1 && i < end - 1);
    Debug.Assert(j >= begin + 1 && j < end - 1);
    Debug.Assert(k >= begin + 1 && k < end - 1);

    i = MedianAdjacent(s, i, ref swaps);
    j = MedianAdjacent(s, j, ref swaps);
    k = MedianAdjacent(s, k, ref swaps);
}
```

あるいは、`NintherThreshold`を調整:

```csharp
// 元: 128
// 新: 150（安全マージンを追加）
private const int NintherThreshold = 150;
```

### エラー2: Pivot値とインデックスの混同

**症状:**
```csharp
if (!leftmost && s.Compare(begin - 1, pivot) >= 0)  // ← pivot はインデックス！
```

**原因:**
- `pivot`はインデックスだが、値として比較している

**対処法:**

```csharp
// 正しい: pivotインデックスの値を読み取る
if (!leftmost && s.Compare(begin - 1, s.Read(pivot)) >= 0)
```

### エラー3: 状態フラグの不適切な更新

**症状:**
- 再帰呼び出し後、`wasBalanced`, `wasPartitioned`が親のスコープで上書きされる

**原因:**
- 状態フラグがループのローカル変数だが、再帰呼び出しで変更される

**対処法:**

Option 1: 各再帰レベルで独立した状態（Go実装）
```csharp
private static void PDQSortLoop<T>(...)
{
    var wasBalanced = true;  // ← このレベル専用
    var wasPartitioned = true;

    while (true) {
        // ...

        // 再帰呼び出しは新しいwasBalanced/wasPartitionedを持つ
        PDQSortLoop(s, begin, pivotPos, badAllowed, leftmost, context);
    }
}
```

Option 2: 状態を引数として渡す
```csharp
private static void PDQSortLoop<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int badAllowed,
    bool leftmost, bool wasBalanced, bool wasPartitioned,
    ISortContext context)
    where TComparer : IComparer<T>
{
    // ...
}
```

### エラー4: ReverseRange後のpivotインデックスの再計算忘れ

**症状:**
```csharp
if (hint == SortedHint.Decreasing) {
    ReverseRange(s, begin, end);
    // pivot インデックスを更新し忘れ！
}
var (pivotPos, _) = PartitionRight(s, begin, end, pivot);  // ← 間違ったpivot
```

**原因:**
- Reverse後、pivotインデックスが無効になる

**対処法:**

```csharp
if (hint == SortedHint.Decreasing) {
    ReverseRange(s, begin, end);

    // ★ pivotインデックスを再計算 ★
    // 元のpivotがbeginからの距離: (pivot - begin)
    // Reverse後は、endからの距離が同じ
    pivot = (end - 1) - (pivot - begin);

    hint = SortedHint.Increasing;
}
```

### エラー5: BreakPatternsでの型キャストエラー

**症状:**
```csharp
var other = (int)(random.Next() & (modulus - 1));
// CS0019: Operator '&' cannot be applied to operands of type 'ulong' and 'int'
```

**原因:**
- `random.Next()` は `ulong` を返すが、`modulus` は `int`

**対処法:**

```csharp
var modulus = (ulong)NextPowerOfTwo(length);  // ← ulong にキャスト
var other = (int)(random.Next() & (modulus - 1));
```

## 3. テスト戦略

### 単体テスト

各Phase毎にテストを作成:

**Phase 1: ChoosePivot**
```csharp
[Fact]
public void ChoosePivot_SortedArray_ReturnsIncreasingHint()
{
    var sorted = Enumerable.Range(1, 200).ToArray();
    var s = new SortSpan<int, Comparer<int>>(sorted, Comparer<int>.Default, NullContext.Default, 0);

    var (pivot, hint) = ChoosePivot(s, 0, 200);

    Assert.Equal(SortedHint.Increasing, hint);
}

[Fact]
public void ChoosePivot_ReversedArray_ReturnsDecreasingHint()
{
    var reversed = Enumerable.Range(1, 200).Reverse().ToArray();
    var s = new SortSpan<int, Comparer<int>>(reversed, Comparer<int>.Default, NullContext.Default, 0);

    var (pivot, hint) = ChoosePivot(s, 0, 200);

    Assert.Equal(SortedHint.Decreasing, hint);
}
```

**Phase 3: Early Detection**
```csharp
[Fact]
public void PDQSort_SortedArray_ZeroSwaps()
{
    var sorted = Enumerable.Range(1, 200).ToArray();
    var stats = new StatisticsContext();

    PDQSort.Sort(sorted.AsSpan(), stats);

    Assert.Equal(0UL, stats.SwapCount);  // ← Go準拠なら0
}
```

### 統合テスト

既存のテストスイートを実行:

```csharp
dotnet test --filter "FullyQualifiedName~PDQSortTests"
```

すべてのテストがパスすることを確認:
- EdgeCaseTests
- SortResultOrderTest（各種パターン）
- NearlySortedArrayTest
- etc.

### ベンチマーク

性能比較:

```csharp
[Benchmark]
public void PDQSort_Sorted_200()
{
    var array = Enumerable.Range(1, 200).ToArray();
    PDQSort.Sort(array.AsSpan());
}

[Benchmark]
public void PDQSort_Random_200()
{
    var array = _randomData.Clone() as int[];
    PDQSort.Sort(array.AsSpan());
}
```

## 4. デバッグ手法

### Swap回数の追跡

```csharp
// StatisticsContextを使用
var stats = new StatisticsContext();
PDQSort.Sort(array.AsSpan(), stats);

Console.WriteLine($"Swaps: {stats.SwapCount}");
Console.WriteLine($"Compares: {stats.CompareCount}");
Console.WriteLine($"Writes: {stats.IndexWriteCount}");
```

### ブレークポイントの設置

重要な箇所:
1. `ChoosePivot` 関数の先頭
2. `hint == SortedHint.Increasing` の条件分岐
3. `PartitionRight` 関数内の `s.Swap(begin, pivotIndex)`
4. `PartialInsertionSort` の return 文

### ログ出力

```csharp
private static void PDQSortLoop<T>(...)
{
    var iteration = 0;
    while (true)
    {
        iteration++;
        Console.WriteLine($"[Iteration {iteration}] begin={begin}, end={end}, size={end-begin}");

        // ...

        var (pivot, hint) = ChoosePivot(s, begin, end);
        Console.WriteLine($"  Pivot={pivot}, Hint={hint}");

        // ...
    }
}
```

## 5. パフォーマンスチューニング

### Aggressive Inlining

重要な関数に `[MethodImpl(MethodImplOptions.AggressiveInlining)]` を追加:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static (int pivot, SortedHint hint) ChoosePivot<T>(...)
{
    // ...
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void Sort3WithSwapCount<T>(...)
{
    // ...
}
```

### 条件分岐の最適化

```csharp
// Before: 複数の条件チェック
if (wasBalanced) {
    if (wasPartitioned) {
        if (hint == SortedHint.Increasing) {
            // ...
        }
    }
}

// After: 単一の条件式
if (wasBalanced && wasPartitioned && hint == SortedHint.Increasing) {
    // ...
}
```

## 6. まとめ

### 成功の鍵

1. **段階的な実装:** 一度にすべてを変更しない
2. **テストファースト:** 各Phaseで既存テストをパス
3. **デバッグ:** 問題が発生したら、1つ前のPhaseに戻る
4. **ベンチマーク:** 性能改善を測定

### 実装の優先順位

1. ✅ **Phase 1:** ChoosePivot関数の分離（低リスク）
2. ⚠️ **Phase 2:** 状態フラグの導入（中リスク）
3. ⚠️ **Phase 3:** Partition前のソート済み検出（中リスク）
4. ✅ **Phase 4:** PartitionRight/Left修正（低リスク）
5. ✅ **Phase 5:** BreakPatterns追加（低リスク）

### 予想される成果

- ソート済み配列: **Swap回数 1 → 0**
- ランダム配列: 性能ほぼ同等
- 逆順配列: わずかな改善
- コードの複雑さ: 増加（約30%）

---

**作成日:** 2026-01-02
**推奨実装期間:** 2-3週間（テストとデバッグ含む）

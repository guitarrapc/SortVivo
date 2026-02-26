# Tutorial Phase / Role 設計メモ（保留中）

## 背景・課題

チュートリアルでは `SortOperation`（Compare/Swap/Read/Write/RangeCopy）の低レベルな操作列を記録・表示しているが、
**アルゴリズムの意味論的なポイント**が伝わらない。

| アルゴリズム | 伝えたいポイント |
|---|---|
| Bubble Sort | 最大値を右端へバブルするパスの概念 |
| Selection Sort | 未ソート範囲の最小値候補を追跡している様子 |
| Quick Sort | ピボット要素を基準に左右へ分割している様子 |
| Radix LSD | 一の位 → 十の位 の順に処理するパス |
| Radix MSD | 十の位から再帰的にバケット分割する構造 |

マーブルの動きだけではこれらは見えない。

---

## 追加したい情報の分類

### A. フェーズ（局面）

複数ステップにまたがる「今何をしているか」の説明。

```
例:
  Bubble Sort   → "パス 1/7：最大値を右端へバブル中"
  Selection Sort → "位置 0〜7 の最小値を探索中"
  Quick Sort    → "ピボット 5 で左右に分割中"
  Radix LSD     → "一の位でソート中（パス 1/2）"
```

### B. 役割マーカー（Role）

特定インデックスに付与する「この要素は今何者か」の情報。

```
例:
  Pivot       → Quick Sort のピボット要素（金色など）
  CurrentMin  → Selection Sort の現在の最小値候補（黄色など）
  CurrentMax  → Bubble Sort で右端へ向かっている最大値
  LeftPointer / RightPointer → 2 ポインタ系アルゴリズム
```

---

## 実装案

### 案 A：`OnPhase` を `ISortContext` に追加

```csharp
// ISortContext に追加
void OnPhase(string description);

// TutorialStep に追加
public string Phase { get; init; } = string.Empty;

// アルゴリズム側（BubbleSort 例）
context.OnPhase($"パス {pass + 1}: 残り {n} 要素を右端へバブル");
```

UI では既存のナラティブパネルの上に「フェーズバー」として表示。

### 案 B：`OnRole` を `ISortContext` に追加

```csharp
// ISortContext に追加
void OnRole(int index, int bufferId, RoleType role);

public enum RoleType
{
    None,
    Pivot,
    CurrentMin,
    CurrentMax,
    LeftPointer,
    RightPointer,
}

// TutorialStep に追加
public Dictionary<int, RoleType> Roles { get; init; } = new();

// アルゴリズム側（QuickSort 例）
context.OnRole(pivotIndex, BUFFER_MAIN, RoleType.Pivot);
```

`MarbleRenderer` に `Roles` パラメータを追加し、役割に応じたアイコン・枠線で表示。

---

## 未解決の設計問題：呼び出し経路

`OnPhase` / `OnRole` を誰経由で呼ぶかで選択肢が 2 つある。

### 経路 1：`ISortContext` 直接呼び出し

```csharp
// アルゴリズム側
var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
context.OnPhase($"パス {pass}");   // context を直接参照
s.Compare(j + 1, j);
```

| | |
|---|---|
| ✅ | `SortSpan` がデータ操作に専念できる（責務明確） |
| ✅ | `OnPhase` の非データ的性質が明示的 |
| ⚠️ | アルゴリズムが `context` と `s` を両方保持する必要がある |
| ⚠️ | `NullContext` dead code elimination が自動で効かない（ただし呼び出しは O(n) 以下なので性能影響は軽微） |

`s.Context.OnPhase(...)` という書き方も可能だが、それなら経路 2 の方が自然という話になる。

### 経路 2：`SortSpan` 経由

```csharp
// SortSpan に追加
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void Phase(string description)
{
    if (typeof(TContext) != typeof(NullContext))
        _context.OnPhase(description);
}

// アルゴリズム側
s.Phase($"パス {pass}");   // s だけで完結
```

| | |
|---|---|
| ✅ | アルゴリズムが `s` だけ持てばよい（既存スタイルと一致） |
| ✅ | `NullContext` 最適化が自動で効く |
| ⚠️ | `SortSpan` が非データ操作を持つことになる |
| ⚠️ | `bufferId` が不要な `OnPhase` を `SortSpan` 経由にする不自然さ |

---

## 非対称性の問題

`OnPhase` と `OnRole` で自然な経路が異なる。

| 操作 | インデックス | bufferId | 自然な経路 |
|---|---|---|---|
| `OnPhase(string)` | なし | なし | `ISortContext` 直接 |
| `OnRole(index, bufferId, role)` | あり | あり | `SortSpan` 経由 |

2 つを別経路にすると設計が非一貫になる。
`SortSpan` に統一するか、`ISortContext` 直接に統一するか、あるいは性質で分けるかが未決定。

---

## 選択肢まとめ

| 方針 | `OnPhase` | `OnRole` | 特徴 |
|---|---|---|---|
| **SortSpan 統一** | `s.Phase()` | `s.SetRole()` | 一貫性重視。SortSpan の責務が広がる |
| **ISortContext 直接統一** | `context.OnPhase()` | `context.OnRole()` | 責務明確化重視。アルゴリズムが context を直接持つ |
| **性質で分離** | `context.OnPhase()` | `s.SetRole()` | 意味論的に正確。経路の非一貫性あり |

---

## ステータス

**保留中** — 設計方針が未決定。実装着手前に経路を確定する必要がある。

### 決定すべき事項

1. 呼び出し経路を `SortSpan` 統一 / `ISortContext` 直接統一 / 性質で分離のどれにするか
2. `OnRole` の `RoleType` に含める役割の種類と粒度
3. フェーズ文字列の生成をアルゴリズム側に任せるか、`TutorialStepBuilder` 側で補完するか
4. まず対応するアルゴリズムを絞るか（Bubble / Selection / Quick を先行実装など）

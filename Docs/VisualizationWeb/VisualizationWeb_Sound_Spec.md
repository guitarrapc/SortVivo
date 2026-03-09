# Phase 8.4 音（Sound）仕様書

## 📋 概要

**目的:** ソートアルゴリズム実行中に、配列への値アクセスを音で表現する。
値の大きさが音程に対応するため、ソートの進行が音でも感じ取れる。

**設計方針:**
- Web Audio API ネイティブのみ使用（外部ライブラリなし）
- デフォルト OFF（ページ訪問時に音が鳴らないように）
- 0アロケーション設計（再生中は AudioNode を生成しない → GC によるプツプツを防止）
- 描画と独立（`PlaybackService` の rAF ループに統合）

---

## 🎵 音の基本仕様

### 音源

| 項目 | 仕様 |
|------|------|
| API | Web Audio API（`AudioContext` + ボイスプール） |
| 波形 | サイン波（`sine`） |
| 周波数レンジ | 200 Hz〜1200 Hz |
| 周波数マッピング | 配列内の相対値（最小値→200 Hz、最大値→1200 Hz）に線形マッピング |
| デフォルト状態 | **OFF** |
| デフォルト音量 | **50%**（`SoundVolume = 0.5`） |
| UI | Sound トグル + Volume スライダー（Sound ON 時のみ表示） |

#### 周波数マッピング式

```
frequency = 200 + (value / arraySize) * 1000
```

> 値の範囲は `1〜N`（`N` = 配列サイズ）。最小値 200Hz、最大値 1200Hz。

---

## 🎹 操作タイプ別の発音仕様

### 鳴らす操作: **Read + Write のみ**（D2方式）

| 操作 | 発音 | 理由 |
|------|------|------|
| **IndexRead** | ✅ 鳴らす | 値を読んだ = アクセスした音 |
| **IndexWrite** | ✅ 鳴らす | 値を書いた = 変化した音 |
| Compare | ❌ 鳴らさない | 高頻度で騒音になりやすい |
| Swap | ❌ 直接は鳴らさない | 内部で IndexRead × 2 + IndexWrite × 2 が記録される |

> **Swap について:** Swap は内部的に 4回の Read/Write として `VisualizationContext` に記録されるため、自然に複数音が発音される。Swap 専用の処理は不要。

---

## ⚡ OpsPerFrame が多い時の発音制御（A4方式）

1フレームに複数の操作が含まれる場合、**実際に収集した Read/Write 数（`count`）** に応じて発音する操作を間引く。`OpsPerFrame` の設定値ではなく実際の収集数で判断することで、`SpeedMultiplier` が高い時に `effectiveOps` が増えても適切に制御できる。

| 収集数（count） | 発音数 | 選択方法 |
|----------------|--------|----------|
| **1〜3** | 全操作（1〜3音） | フィルタなし |
| **4〜10** | 最大3音 | 等間隔サンプリング（先頭・中間・末尾） |
| **11以上** | 1音 | 末尾の1操作のみ |

#### サンプリング方法（4〜10の場合）

```
indices = [0, floor(count / 2), count - 1]
```

---

## 🚀 高速再生時の発音制御（B4方式）

Speed Multiplier が高い場合、音の持続時間を短縮する。どの速度でも音は鳴り続ける。

### 持続時間テーブル

| Speed Multiplier | 持続時間 | 状態 |
|-----------------|----------|------|
| 0.1x 〜 2x | 150 ms | 通常 |
| 2x 〜 5x | 80 ms | 短縮 |
| 5x 〜 20x | 40 ms | 短縮 |
| 20x 〜 50x | 20 ms | 短縮 |
| 50x 超 | 10 ms | 最短 |

---

## 🔊 発音の実装仕様

### オーディオグラフ

```
[ボイスプール × 32]
  OscillatorNode → GainNode ─┐
  OscillatorNode → GainNode ─┤
  ...（32ボイス）             ├→ Limiter(DynamicsCompressor) → AudioContext.destination
  OscillatorNode → GainNode ─┘
```

**ボイスプール方式の採用理由:**
毎フレーム `OscillatorNode` / `GainNode` を新規生成すると、10x 速度・60fps・3音の場合に毎秒 360 個の AudioNode が生成・破棄されて GC が頻発し、プツプツノイズが発生する。
初期化時に 32 ボイス分のノードを生成して以後は再利用することで、**再生中の AudioNode 生成をゼロ**にする。

### ボイスプールサイズ（32ボイス）

| ディスプレイ | rAF 間隔 | 最大同時発音数 | 必要ボイス数 | 32で充足 |
|------------|---------|-------------|------------|---------|
| 60 Hz | 16.7 ms | 3音 × 3重なり = 9 | 9 | ✅ |
| 120 Hz | 8.3 ms | 3音 × 5重なり = 15 | 15 | ✅ |
| 144 Hz | 6.9 ms | 3音 × 6重なり = 18 | 18 | ✅ |
| 240 Hz | 4.2 ms | 3音 × 10重なり = 30 | 30 | ✅ |

### エンベロープ（音の形）

```
ゲイン
  peak |    /‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾\
       |   /                    \
   0.0 |  /                      \──── 時間
       | 5ms                  duration
       |  ↑アタック              ↑フェードアウト
```

- **アタック:** 5ms のランプアップ（0 → peak）。瞬時ジャンプを避けてクリックノイズを除去。
- **フェードアウト:** `duration` 終端に向けて線形フェードアウト（`linearRampToValueAtTime`）。
- **ゲイン:** 適応ゲイン方式（後述）。

### 適応ゲイン方式

速度に関わらず総ゲインを一定（≈ -16 dBFS）に保ち、DynamicsCompressor によるポンピングノイズを防止する。

```javascript
// 期待オーバーラップ数 ≈ duration × 60fps
expectedOverlap = max(1, durationSec × 60)

// 総ゲイン = expectedOverlap × notes × gainPerNote = 0.15 × vol（一定）
gainPerNote = (0.15 × vol) / (expectedOverlap × notes)
```

| 速度 | duration | expectedOverlap | gainPerNote（1音） | 総ゲイン |
|------|---------|-----------------|-------------------|---------|
| 1x | 150ms | 9 | 0.017 | **0.15** |
| 5x | 80ms | 4.8 | 0.031 | **0.15** |
| 10x | 40ms | 2.4 | 0.063 | **0.15** |
| 20x | 40ms | 2.4 | 0.063 | **0.15** |
| 100x | 10ms | 0.6 | 0.15 | **0.15** |

### 安全リミッター

通常動作では適応ゲインにより総ゲインが ≈ -16 dBFS となり、リミッターの閾値（-1 dBFS）に達しない。万が一クリッピングする場合の安全網として機能する。

| パラメーター | 値 | 説明 |
|------------|-----|------|
| threshold | -1 dBFS | 通常動作では触れない |
| knee | 0（ハードニー） | 純リミッター動作 |
| ratio | 20:1 | 強いリミッター |
| attack | 1 ms | 高速 |
| release | 100 ms | ポンピング抑制 |

### ボイス横取り（Voice Stealing）

全 32 ボイスが使用中の場合（超高リフレッシュレート環境等）、最も早く終了するボイスを横取りして再利用する。横取り時はクリックを防ぐため 2ms クロスフェードを挿入する。

```
通常割り当て:  gain 0 → 5ms → peak → fade → 0
横取り:        現在値 → 2ms → 0 → 5ms → peak → fade → 0
```

---

## 🖥️ UI 仕様

### Sound セクションのレイアウト

```
[🔊 Sound  ○──●]

（Sound ON 時のみ表示）
Volume              50%
[══════════════════════]  ← 0%〜100% スライダー
```

| 項目 | 仕様 |
|------|------|
| Sound トグル | デフォルト OFF、ON 時に `soundEngine.initAudio()` を呼ぶ |
| Volume スライダー | Sound ON 時のみ表示、範囲 0%〜100%、ステップ 5%、デフォルト 50% |
| 配置コンポーネント | `SettingsModal.razor`（Sound セクション） |

---

## 📁 実装ファイル構成

```
src/SortAlgorithm.VisualizationWeb/
├── wwwroot/
│   ├── index.html                  # 変更: soundEngine.js の <script> タグ追加
│   └── js/
│       └── soundEngine.js          # 新規: Web Audio API ラッパー（ボイスプール方式）
├── Services/
│   └── PlaybackService.cs          # 変更: SoundEnabled/SoundVolume プロパティ、発音ロジック統合
└── Components/
    └── NormalModeControls.razor    # 変更: Sound トグル + Volume スライダー UI
```

### soundEngine.js の公開 API

| 関数 | 引数 | 説明 |
|------|------|------|
| `initAudio()` | — | AudioContext 初期化・再開（ユーザー操作後に呼ぶ） |
| `playNotes(frequencies, duration, volume)` | `number[]`, `number`, `number` | 複数音を同時発音 |
| `disposeAudio()` | — | AudioContext を閉じてリソース解放 |

> `playNotes` は配列を受け取ることで Blazor→JS 間の JS Interop 回数を **1回/フレーム** に抑える。

### PlaybackService の音関連メンバー

| メンバー | 種別 | 説明 |
|---------|------|------|
| `SoundEnabled` | `bool` プロパティ | 音の有効/無効（デフォルト `false`） |
| `SoundVolume` | `double` プロパティ | 音量 0.0〜1.0（デフォルト `0.5`） |
| `InitSoundAsync()` | メソッド | `soundEngine.initAudio()` を呼ぶ |
| `_soundFreqBuffer` | `List<float>` | フレームごとの周波数バッファ（再利用、アロケーションなし） |
| `GetFrequencyForOp()` | private | 操作から周波数を計算（IndexRead: 配列現在値、IndexWrite: 書き込む値） |
| `SampleSoundFrequencies()` | private | A4方式でサンプリング |
| `GetSoundDuration()` | private static | B4方式で持続時間を返す |

---

## ✅ 受け入れ条件

| 条件 | 内容 |
|------|------|
| デフォルト OFF | ページロード時に音が鳴らない |
| Autoplay 対応 | Sound トグル ON（ユーザー操作）後に `AudioContext` を初期化する |
| 発音操作の限定 | IndexRead / IndexWrite のみ発音（Compare / Swap は直接鳴らさない） |
| OpsPerFrame 多い時 | 収集数に応じて最大 3音（4〜10）または 1音（11以上）に制限 |
| 全速度で発音 | SpeedMultiplier に関わらず音が鳴る（高速時は持続時間が短くなる） |
| 持続時間の段階制御 | 速度に応じて 150/80/40/20/10ms に変化する |
| クリックノイズなし | 5ms アタックでゲインをゼロから立ち上げる |
| ポンピングノイズなし | 適応ゲイン方式により総ゲインを一定化（DynamicsCompressor が作動しない） |
| プツプツノイズなし | ボイスプール方式により再生中の AudioNode 生成ゼロ化 |
| 音量調整 | Volume スライダーで 0%〜100% を調整可能 |
| JS 呼び出し最適化 | フレームあたり最大 1回のみ JS Interop を呼ぶ |
| リソース解放 | `PlaybackService.Dispose()` で `soundEngine.disposeAudio()` を呼ぶ |

---

## 🚫 対象外（スコープ外）

- 波形の変更（`square` / `sawtooth` 等をユーザーが選べる UI）
- 操作タイプ別の音色変化（Compare を鳴らす、Swap で特殊音など）
- MIDI 出力
- 音楽的スケール（平均律等）への補正

---

## 📊 実績工数

| 作業 | 工数 |
|------|------|
| `soundEngine.js` 実装（ボイスプール、リミッター、適応ゲイン） | 1日 |
| `PlaybackService.cs` への統合 | 0.5日 |
| `NormalModeControls.razor` UI 追加 | 0.25日 |
| ノイズ調査・改善（GC プツプツ、コンプレッサーポンピング） | 1日 |
| **合計** | **約 2.75日** |

---

## 📝 実装の変遷

当初設計から実際の実装で変更・追加された主な点。

| 項目 | 当初設計 | 実装後 | 変更理由 |
|------|---------|--------|---------|
| AudioNode 管理 | 毎フレーム生成・破棄 | **ボイスプール（32本）** | GC によるプツプツノイズ防止 |
| マスター出力 | なし | **安全リミッター** | クリッピング防止の安全網 |
| ゲイン計算 | 固定値 `0.3` | **適応ゲイン**（速度連動） | DynamicsCompressor ポンピング防止 |
| エンベロープ | 即時アタック | **5ms アタック** | クリックノイズ除去 |
| A4サンプリング閾値 | `OpsPerFrame` 設定値 | **実際の収集数 `count`** | 高速時の過発音防止 |
| 音量調整 | 対象外 | **Volume スライダー追加** | ユーザー要望 |
| UI 配置 | `Index.razor` | **`SettingsModal.razor`** | コンポーネント構造に合わせる |
| 高速時の自動無効化 | 50x 超で無音 | **全速度で発音（50x 超は 10ms）** | 高速時も音で進行を把握したい |


## 📋 概要

**目的:** ソートアルゴリズム実行中に、配列への値アクセスを音で表現する。
値の大きさが音程に対応するため、ソートの進行が音でも感じ取れる。

**設計方針:**
- Web Audio API ネイティブのみ使用（外部ライブラリなし）
- デフォルト OFF（ページ訪問時に音が鳴らないように）
- 0アロケーション設計（毎フレーム GC を引き起こさない）
- 描画と独立（`PlaybackService` の再生ループに統合）

---

## 🎵 音の基本仕様

### 音源

| 項目 | 仕様 |
|------|------|
| API | Web Audio API（`AudioContext` + `OscillatorNode`） |
| 波形 | サイン波（`sine`） |
| 周波数レンジ | 200 Hz〜1200 Hz |
| 周波数マッピング | 配列内の相対値（最小値→200 Hz、最大値→1200 Hz）に線形マッピング |
| デフォルト状態 | **OFF** |
| UI | トグルスイッチ（Auto Reset の隣に配置） |

#### 周波数マッピング式

```
frequency = 200 + (value / maxValue) * (1200 - 200)
           = 200 + (value / maxValue) * 1000
```

> `maxValue` は配列サイズ（要素数）と等値。値の範囲は `1〜N`（`N` = 配列サイズ）。

---

## 🎹 操作タイプ別の発音仕様

### 鳴らす操作: **Read + Write のみ**（D2方式）

| 操作 | 発音 | 理由 |
|------|------|------|
| **IndexRead** | ✅ 鳴らす | 値を読んだ = アクセスした音 |
| **IndexWrite** | ✅ 鳴らす | 値を書いた = 変化した音 |
| Compare | ❌ 鳴らさない | 高頻度で騒音になりやすい |
| Swap | ❌ 直接は鳴らさない | 内部で Read + Write が記録される |

> **Swap について:** Swap 操作は内部的に 2回の Read + 2回の Write として `VisualizationContext` に記録されるため、自然に2音が発音される。Swap 専用の処理は不要。

---

## ⚡ OpsPerFrame が多い時の発音制御（A4方式）

1フレームに複数の操作が含まれる場合、発音する操作を自動的に間引く。

| OpsPerFrame | 発音するop数 | 選択方法 |
|-------------|-------------|----------|
| **1〜3** | 全操作 | フィルタなし |
| **4〜10** | 最大3音 | 等間隔サンプリング（先頭・中間・末尾） |
| **11以上** | 最大1音 | 末尾の1操作のみ |

#### サンプリング方法（4〜10の場合）

```
// 等間隔3点サンプリング
indices = [0, floor(count / 2), count - 1]
```

> 先頭・中間・末尾を取ることで「フレームの全体的な変化」を音で表現する。

---

## 🚀 高速再生時の発音制御（B4方式）

Speed Multiplier が高い場合、音の持続時間を短縮する。どの速度でも音は鳴り続ける。

### 持続時間テーブル

| Speed Multiplier | 持続時間 | 状態 |
|-----------------|----------|------|
| 0.1x 〜 2x | 150 ms | 通常 |
| 2x 〜 5x | 80 ms | 短縮 |
| 5x 〜 20x | 40 ms | 短縮 |
| 20x 〜 50x | 20 ms | 短縮 |
| 50x 超 | 10 ms | 最短 |

#### 持続時間の計算ロジック

```javascript
function getSoundDuration(speedMultiplier) {
    if (speedMultiplier > 50)  return 10;   // 最短
    if (speedMultiplier > 20)  return 20;
    if (speedMultiplier > 5)   return 40;
    if (speedMultiplier > 2)   return 80;
    return 150;
}
```

---

## 🔊 発音の実装仕様

### ノードグラフ

```
OscillatorNode → GainNode → AudioContext.destination
  (freq, sine)   (fade out)
```

### エンベロープ（音の形）

```
音量
 1.0 |─────────────────┐
     |                 │ (線形フェードアウト)
 0.0 |─────────────────┘──── 時間
     0            duration
```

- **アタック:** なし（即座に最大音量）
- **ディケイ/リリース:** 持続時間の終わりに向けて線形フェードアウト（ `linearRampToValueAtTime`）
- **最大ゲイン:** `0.3`（複数音の重なりによるクリッピング防止）

### 発音の流れ

```javascript
function playNote(frequency, duration) {
    const ctx = getAudioContext();
    if (!ctx) return;

    const oscillator = ctx.createOscillator();
    const gainNode = ctx.createGain();

    oscillator.connect(gainNode);
    gainNode.connect(ctx.destination);

    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(frequency, ctx.currentTime);

    gainNode.gain.setValueAtTime(0.3, ctx.currentTime);
    gainNode.gain.linearRampToValueAtTime(0.0, ctx.currentTime + duration / 1000);

    oscillator.start(ctx.currentTime);
    oscillator.stop(ctx.currentTime + duration / 1000);
}
```

### AudioContext の初期化

ブラウザのAutoplay Policy制約により、`AudioContext` はユーザー操作（クリック等）の後に初期化する。

```javascript
// 最初のユーザーインタラクション（再生ボタン押下時等）で初期化
function ensureAudioContext() {
    if (!_audioContext) {
        _audioContext = new AudioContext();
    }
    if (_audioContext.state === 'suspended') {
        _audioContext.resume();
    }
    return _audioContext;
}
```

---

## 🖥️ UI 仕様

### トグルスイッチの配置

```
[⏩ Auto Reset  ●──○]  [🔊 Sound  ○──●]
```

- Auto Reset トグルの **右隣** に配置
- ラベル: `🔊 Sound`
- デフォルト: **OFF**（`false`）

---

## 📁 実装ファイル構成

```
src/SortAlgorithm.VisualizationWeb/
├── wwwroot/
│   └── js/
│       └── soundEngine.js          # 新規: Web Audio API ラッパー
├── Services/
│   └── PlaybackService.cs          # 変更: 発音タイミング制御を追加
└── Pages/
    └── Index.razor                 # 変更: Sound トグル UI 追加
```

### soundEngine.js の公開API

| 関数 | 引数 | 説明 |
|------|------|------|
| `initAudio()` | — | AudioContext 初期化（ユーザー操作後に呼ぶ） |
| `playNotes(frequencies, duration)` | `number[]`, `number` | 複数音を同時発音（サンプリング済みの周波数配列） |
| `disposeAudio()` | — | AudioContext を閉じてリソース解放 |

> `playNotes` は配列を受け取ることで、Blazor→JS 間のインターオペレーション回数を **1回/フレーム** に抑える。

### PlaybackService への統合ポイント

```csharp
// OnTimerElapsed / OnAnimationFrame 内の発音制御
if (SoundEnabled)
{
    var frequencies = SampleFrequencies(frameOps, OperationsPerFrame, _currentArraySize);
    var duration = GetSoundDuration(SpeedMultiplier);
    if (frequencies.Length > 0)
    {
        await _js.InvokeVoidAsync("soundEngine.playNotes", frequencies, duration);
    }
}
```

#### SampleFrequencies のロジック

```csharp
private float[] SampleFrequencies(IReadOnlyList<SortOperation> frameOps, int opsPerFrame, int arraySize)
{
    // Read/Write のみを対象にフィルタ
    var readWriteOps = frameOps
        .Where(op => op.Type is SortOperationType.IndexRead or SortOperationType.IndexWrite)
        .ToList(); // ← 実装時はアロケーション回避のためスタック割り当てや再利用バッファを使う

    if (readWriteOps.Count == 0) return [];

    // OpsPerFrame に応じてサンプリング
    var sampled = opsPerFrame <= 3
        ? readWriteOps                                            // 全部
        : opsPerFrame <= 10
            ? Sample3(readWriteOps)                               // 等間隔3点
            : [readWriteOps[^1]];                                 // 末尾1点

    return sampled
        .Select(op => 200f + (op.Value / (float)arraySize) * 1800f)
        .ToArray();
}
```

> **Note:** 実際の実装では `ArrayPool<float>` を使用してアロケーションを回避する。

---

## ✅ 受け入れ条件

| 条件 | 内容 |
|------|------|
| デフォルト OFF | ページロード時に音が鳴らない |
| Autoplay 対応 | ユーザー操作前に `AudioContext` を生成しない |
| OpsPerFrame 多い時 | 最大発音数が 3（4〜10） or 1（11以上）に制限される |
| 全速度で発音 | SpeedMultiplier に関わらず音が鳴る（高速時は持続時間が短くなる） |
| 持続時間の段階制御 | 速度に応じて 150/80/40/20/10ms に変化する |
| エンベロープ | 急激に音が切れない（フェードアウトあり） |
| 音量制限 | 複数音の重なりでクリッピングしない（gain ≤ 0.3） |
| JS 呼び出し最適化 | フレームあたり最大 1回のみ JS Interop を呼ぶ |
| リソース解放 | `PlaybackService.Dispose()` で `AudioContext` を閉じる |

---

## 🚫 対象外（スコープ外）

- 波形の変更（ユーザーが square/sawtooth 等を選べるUI）
- 操作タイプ別の音色変化（Compare を鳴らす、Swap で特殊音など）
- ボリューム調整スライダー
- MIDI 出力
- 音楽的スケール（平均律等）への補正

---

## 📊 推定工数

| 作業 | 工数 |
|------|------|
| `soundEngine.js` 実装 | 0.5日 |
| `PlaybackService.cs` への統合 | 0.5日 |
| `Index.razor` UI 追加 | 0.25日 |
| テスト・調整 | 0.25日 |
| **合計** | **約 1.5日** |

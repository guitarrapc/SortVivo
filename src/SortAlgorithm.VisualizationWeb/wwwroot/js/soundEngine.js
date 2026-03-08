'use strict';
// Sound Engine - Web Audio API ラッパー
//
// 設計方針:
//   - AudioContext はユーザー操作後に initAudio() で初期化する（ブラウザ Autoplay Policy 対応）
//   - playNotes(frequencies, duration, volume) で複数音を一括発音（JS Interop 1回/フレーム）
//   - 固定ボイスプール（16ボイス）を初期化時に生成し、再生中は AudioNode を生成しない
//     → 頻繁な AudioNode 生成・GC によるプツプツノイズを防止
//   - DynamicsCompressor をマスター出力に挟み、重なりによるクリッピングを防止

window.soundEngine = {
    _audioContext: null,
    _output: null,    // DynamicsCompressor → destination
    _voices: [],      // 固定ボイスプール: { osc, gain, freeAt }
    _voiceCount: 32,  // 最大ポリフォニー数（240Hz 表示でも横取りなし: 3音 × ceil(40ms/4ms) = 30）
    _soundType: 'sine',     // 現在のサウンドタイプ: 'sine' | 'poko'
    _pokoVoices: [],        // ポコ用固定ボイスプール: { mainOsc, mainGain, lowpass, clickOsc, clickGain, freeAt }
    _pokoVoiceCount: 24,    // ポコの最大ポリフォニー数（90ms × 60fps × 3音 ≈ 16 で余裕を持って 24）

    /**
     * サウンドタイプを設定する。'sine'（デフォルト）または 'poko'。
     * AudioContext 初期化前でも呼び出し可能。
     * @param {string} type - 'sine' | 'poko'
     */
    setSoundType: function (type) {
        this._soundType = (type === 'poko') ? 'poko' : 'sine';
    },

    /**
     * AudioContext を初期化・再開する。ユーザー操作（トグル押下など）後に呼ぶ。
     */
    initAudio: function () {
        if (!this._audioContext) {
            try {
                this._initAudioContext();
            } catch (e) {
                return;
            }
        }
        if (this._audioContext.state === 'suspended') {
            this._audioContext.resume();
        }
    },

    /**
     * AudioContext・Compressor・ボイスプールをまとめて初期化する。
     */
    _initAudioContext: function () {
        this._audioContext = new AudioContext();
        this._setupOutput();
        this._setupVoices();
        this._setupPokoVoices();
    },

    /**
     * 安全リミッターをマスター出力として設定する。
     * 適応ゲインにより通常動作では threshold に達しないためポンピングなし。
     * 万が一クリッピングする場合の最後の安全網。
     */
    _setupOutput: function () {
        const ctx = this._audioContext;
        const limiter = ctx.createDynamicsCompressor();
        // threshold -1dBFS: 実際の信号は ∼-16dB なのでほぼ触れない → ポンピングなし
        limiter.threshold.setValueAtTime(-1, ctx.currentTime);
        limiter.knee.setValueAtTime(0, ctx.currentTime);        // ハードニー
        limiter.ratio.setValueAtTime(20, ctx.currentTime);      // リミッター動作
        limiter.attack.setValueAtTime(0.001, ctx.currentTime);  // 高速アタック
        limiter.release.setValueAtTime(0.1, ctx.currentTime);   // リリース
        limiter.connect(ctx.destination);
        this._output = limiter;
    },

    /**
     * ボイスプールを初期化する。
     * 全オシレーターをゲイン 0 で起動し、playNotes から周波数・ゲインだけ上書きして再利用する。
     * AudioNode の生成ゼロ化 → GC によるプツプツノイズを防止。
     */
    _setupVoices: function () {
        const ctx = this._audioContext;
        const output = this._output || ctx.destination;
        this._voices = [];
        for (let i = 0; i < this._voiceCount; i++) {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(output);
            osc.type = 'sine';
            gain.gain.setValueAtTime(0.0, ctx.currentTime);
            osc.start();
            this._voices.push({ osc, gain, freeAt: 0 });
        }
    },

    /**
     * 空きボイスを取得する。空きがない場合は最も早く終わるボイスを横取りする。
     * @param {number} now - AudioContext の現在時刻（秒）
     */
    _acquireVoice: function (now) {
        let best = this._voices[0];
        for (let i = 0; i < this._voices.length; i++) {
            const v = this._voices[i];
            if (v.freeAt <= now) return v;     // 空きボイスが見つかった
            if (v.freeAt < best.freeAt) best = v;
        }
        return best;  // 最も早く終わるボイスを横取り
    },

    /**
     * 複数の周波数を同時に発音する。
     * @param {number[]} frequencies - 発音する周波数の配列（Hz）
     * @param {number} duration - 発音時間（ms）。0 の場合は何もしない。
     * @param {number} volume - 音量（0.0〜1.0）
     */
    playNotes: function (frequencies, duration, volume) {
        if (duration <= 0 || !frequencies || frequencies.length === 0) return;

        if (!this._audioContext) {
            try {
                this._initAudioContext();
            } catch (e) {
                return;
            }
        }
        if (this._audioContext.state === 'suspended') {
            this._audioContext.resume();
        }

        const ctx = this._audioContext;
        const now = ctx.currentTime;
        const durationSec = duration / 1000;

        const vol = Math.max(0, Math.min(1, volume ?? 1));
        // オーバーラップ適応ゲイン:速度に関わらず総ゲイン ≈ 0.15 で一定化しポンピングを防止する。
        // 同時発音数 expectedOverlap ≈ duration × 60fps。
        // 総ゲイン = expectedOverlap × notes × gainPerNote = 0.15 × vol（リミッターの -1dBFS 閾より常に小）
        const expectedOverlap = Math.max(1, durationSec * 60);
        const gainPerNote = (0.15 * vol) / (expectedOverlap * Math.max(1, frequencies.length));

        // poko は固定 140ms ディケイに基づいて独立計算（SpeedMultiplier によらず実効時間は常に 140ms）
        const gainPerPoko = (0.22 * vol) / (Math.max(1, 0.14 * 60) * Math.max(1, frequencies.length));

        for (let i = 0; i < frequencies.length; i++) {
            const freq = frequencies[i];
            if (freq <= 0) continue;

            if (this._soundType === 'poko') {
                this._pokoSound(now, freq, gainPerPoko);
            } else {
                const voice = this._acquireVoice(now);
                const stealing = voice.freeAt > now;
                const startAt = stealing ? now + 0.002 : now;

                // 既存スケジュールをキャンセル
                voice.gain.gain.cancelScheduledValues(now);
                voice.osc.frequency.cancelScheduledValues(now);

                if (stealing) {
                    // スムーズな横取り: 現在値から 2ms でフェードアウトして新音を開始
                    voice.gain.gain.setValueAtTime(voice.gain.gain.value, now);
                    voice.gain.gain.linearRampToValueAtTime(0.0, startAt);
                }

                this._sineSound(voice, freq, startAt, gainPerNote, durationSec);
            }
        }
    },

    /**
     * サイン波: ピッチ固定・5ms アタック・リニアディケイ。
     * @param {object} voice - ボイスプールエントリ { osc, gain, freeAt }
     * @param {number} freq - 周波数（Hz）
     * @param {number} startAt - 発音開始時刻（AudioContext 秒）
     * @param {number} gainPerNote - ノートあたりのゲイン
     * @param {number} durationSec - 発音時間（秒）
     */
    _sineSound: function (voice, freq, startAt, gainPerNote, durationSec) {
        const attackSec = 0.005;  // 5ms アタック: クリックノイズを除去

        voice.osc.frequency.setValueAtTime(freq, startAt);

        voice.gain.gain.setValueAtTime(0.0, startAt);
        voice.gain.gain.linearRampToValueAtTime(gainPerNote, startAt + attackSec);
        voice.gain.gain.linearRampToValueAtTime(0.0, startAt + durationSec);

        voice.freeAt = startAt + durationSec;
    },

    /**
     * ポコポコ: 専用ボイスプール（_pokoVoices）を使った完全ゼロアロケーション実装。
     * - メイン: sine 波 + lowpass + 控えめピッチドロップ（25ms）で優しい「ぽこっ」感
     * - クリック: 高調波 sine （10ms）でごく薄いアタック
     * @param {number} now - AudioContext の現在時刻（秒）
     * @param {number} freq - 周波数（Hz）
     * @param {number} gainPerNote - ノートあたりのゲイン
     */
    _pokoSound: function (now, freq, gainPerNote) {
        const voice = this._acquirePokoVoice(now);
        const stealing = voice.freeAt > now;
        const startAt = stealing ? now + 0.002 : now;

        // 既存スケジュールをキャンセル
        voice.mainOsc.frequency.cancelScheduledValues(now);
        voice.mainGain.gain.cancelScheduledValues(now);
        voice.lowpass.frequency.cancelScheduledValues(now);
        voice.clickOsc.frequency.cancelScheduledValues(now);
        voice.clickGain.gain.cancelScheduledValues(now);

        if (stealing) {
            // スムーズな横取り: 2ms でフェードアウト
            voice.mainGain.gain.setValueAtTime(voice.mainGain.gain.value, now);
            voice.mainGain.gain.linearRampToValueAtTime(0.0001, startAt);
            voice.clickGain.gain.setValueAtTime(voice.clickGain.gain.value, now);
            voice.clickGain.gain.linearRampToValueAtTime(0.0001, startAt);
        }

        // ── メイン（sine + lowpass）──
        // freq * 1.06 → freq へ 25ms exponential ramp で控えめな「ぽこ」感
        voice.mainOsc.frequency.setValueAtTime(freq * 1.06, startAt);
        voice.mainOsc.frequency.exponentialRampToValueAtTime(freq, startAt + 0.025);

        // 高域を軽く丸める（freq * 1.8、最大 2200Hz、Q = 0.5）
        voice.lowpass.frequency.setValueAtTime(Math.min(freq * 1.8, 2200), startAt);
        voice.lowpass.Q.setValueAtTime(0.5, startAt);

        const mainEndTime = startAt + 0.14;
        voice.mainGain.gain.setValueAtTime(0.0001, startAt);
        voice.mainGain.gain.linearRampToValueAtTime(gainPerNote * 0.73, startAt + 0.008); // gainPerNote は 0.22 ベースなので × 0.73 ≈ 0.16
        voice.mainGain.gain.exponentialRampToValueAtTime(0.0001, mainEndTime);
        voice.mainGain.gain.setValueAtTime(0.0, mainEndTime);  // 残響カット

        // ── アタッククリック（sine 高調波、10ms で消音、ごく薄い）──
        voice.clickOsc.frequency.setValueAtTime(freq * 1.8, startAt);

        const clickPeak = gainPerNote * (0.015 / 0.22);
        const clickEndTime = startAt + 0.010;
        voice.clickGain.gain.setValueAtTime(0.0001, startAt);
        voice.clickGain.gain.linearRampToValueAtTime(clickPeak, startAt + 0.002);
        voice.clickGain.gain.exponentialRampToValueAtTime(0.0001, clickEndTime);
      voice.clickGain.gain.setValueAtTime(0.0, clickEndTime);  // 残響カット

        voice.freeAt = startAt + 0.15;  // 140ms + 10ms マージン
    },

    /**
     * ポコ専用ボイスプールを初期化する。
     * 各ボイス: triangle mainOsc → mainGain → lowpass → output
     *           sine clickOsc → clickGain → output
     * 全ノードは起動済みで、ゲイン 0.0001 で待機する。
     */
    _setupPokoVoices: function () {
        const ctx = this._audioContext;
        const output = this._output || ctx.destination;
        this._pokoVoices = [];
        for (let i = 0; i < this._pokoVoiceCount; i++) {
            const mainOsc  = ctx.createOscillator();
            const mainGain = ctx.createGain();
            const lowpass  = ctx.createBiquadFilter();

            mainOsc.type = 'triangle';
            lowpass.type = 'lowpass';
            lowpass.Q.setValueAtTime(0.7, ctx.currentTime);
            mainGain.gain.setValueAtTime(0.0001, ctx.currentTime);

            mainOsc.connect(mainGain);
            mainGain.connect(lowpass);
            lowpass.connect(output);
            mainOsc.start();

            const clickOsc  = ctx.createOscillator();
            const clickGain = ctx.createGain();

            clickOsc.type = 'sine';
            clickGain.gain.setValueAtTime(0.0001, ctx.currentTime);

            clickOsc.connect(clickGain);
            clickGain.connect(output);
            clickOsc.start();

            this._pokoVoices.push({ mainOsc, mainGain, lowpass, clickOsc, clickGain, freeAt: 0 });
        }
    },

    /**
     * ポコ専用プールから空きボイスを取得する。
     * @param {number} now - AudioContext の現在時刻（秒）
     */
    _acquirePokoVoice: function (now) {
        let best = this._pokoVoices[0];
        for (let i = 0; i < this._pokoVoices.length; i++) {
            const v = this._pokoVoices[i];
            if (v.freeAt <= now) return v;
            if (v.freeAt < best.freeAt) best = v;
        }
        return best;
    },

    /**
     * AudioContext を閉じてリソースを解放する。
     */
    disposeAudio: function () {
        for (const v of this._voices) {
            try { v.osc.stop(); } catch (e) { /* already stopped */ }
        }
        this._voices = [];
        for (const v of this._pokoVoices) {
            try { v.mainOsc.stop(); } catch (e) { /* already stopped */ }
            try { v.clickOsc.stop(); } catch (e) { /* already stopped */ }
        }
        this._pokoVoices = [];
        if (this._output) {
            this._output.disconnect();
            this._output = null;
        }
        if (this._audioContext) {
            this._audioContext.close();
            this._audioContext = null;
        }
    }
};


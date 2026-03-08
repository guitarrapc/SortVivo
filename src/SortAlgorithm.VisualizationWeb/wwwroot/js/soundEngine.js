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
    _soundType: 'sine',          // 現在のサウンドタイプ: 'sine' | 'poko' | 'cutePop'
    _pokoVoices: [],             // ポコ用固定ボイスプール: { osc, gain, freeAt }
    _pokoVoiceCount: 16,         // ポコの最大ポリフォニー数（60ms × 60fps × 3音 ≈ 11 で余裕を持って 16）
    _cutePopVoices: [],          // CutePop 用固定ボイスプール: { osc1, gain1, osc2, gain2, freeAt }
    _cutePopVoiceCount: 16,      // CutePop の最大ポリフォニー数（70ms × 60fps × 3音 ≈ 13 で余裕を持って 16）

    /**
     * サウンドタイプを設定する。'sine'（デフォルト）、'poko'、または 'cutePop'。
     * AudioContext 初期化前でも呼び出し可能。
     * @param {string} type - 'sine' | 'poko' | 'cutePop'
     */
    setSoundType: function (type) {
        if (type === 'poko') this._soundType = 'poko';
        else if (type === 'cutePop') this._soundType = 'cutePop';
        else this._soundType = 'sine';
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
        this._setupCutePopVoices();
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

        // poko は固定 60ms ディケイに基づいて独立計算（SpeedMultiplier によらず実効時間は常に 60ms）
        const gainPerPoko = (0.3 * vol) / (Math.max(1, 0.06 * 60) * Math.max(1, frequencies.length));
        // cutePop は固定 70ms ディケイに基づいて独立計算
        const gainPerCutePop = (0.35 * vol) / (Math.max(1, 0.07 * 60) * Math.max(1, frequencies.length));

        for (let i = 0; i < frequencies.length; i++) {
            const freq = frequencies[i];
            if (freq <= 0) continue;

            if (this._soundType === 'poko') {
                this._pokoSound(now, freq, gainPerPoko);
            } else if (this._soundType === 'cutePop') {
                this._cutePopSound(now, freq, gainPerCutePop);
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
        voice.gain.gain.setValueAtTime(0.0, startAt + durationSec);  // 残響カット

        voice.freeAt = startAt + durationSec;
    },

    /**
     * ポコポコ: 専用ボイスプール（_pokoVoices）を使った完全ゼロアロケーション実装。
     * - triangle 波 + ピッチドロップ（freq*1.25 → freq）で「ぽこっ」感
     * @param {number} now - AudioContext の現在時刻（秒）
     * @param {number} freq - 周波数（Hz）
     * @param {number} gainPerNote - ノートあたりのゲイン
     */
    _pokoSound: function (now, freq, gainPerNote) {
        const duration = 0.06;
        const voice = this._acquirePokoVoice(now);
        const stealing = voice.freeAt > now;
        const startAt = stealing ? now + 0.002 : now;

        // 既存スケジュールをキャンセル
        voice.osc.frequency.cancelScheduledValues(now);
        voice.gain.gain.cancelScheduledValues(now);

        if (stealing) {
            // スムーズな横取り: 2ms でフェードアウト
            voice.gain.gain.setValueAtTime(voice.gain.gain.value, now);
            voice.gain.gain.linearRampToValueAtTime(0.0001, startAt);
        }

        voice.osc.frequency.setValueAtTime(freq * 1.25, startAt);
        voice.osc.frequency.exponentialRampToValueAtTime(freq, startAt + duration * 0.8);

        voice.gain.gain.setValueAtTime(0.0001, startAt);
        voice.gain.gain.exponentialRampToValueAtTime(gainPerNote, startAt + 0.005);
        voice.gain.gain.exponentialRampToValueAtTime(0.0001, startAt + duration);
        voice.gain.gain.setValueAtTime(0.0, startAt + duration);  // 残響カット

        voice.freeAt = startAt + duration;
    },

    /**
     * CutePop: 専用ボイスプール（_cutePopVoices）を使った完全ゼロアロケーション実装。
     * - osc1: sine、freq*1.35 → freq（duration*0.75）で丸いメイン音
     * - osc2: triangle、freq*2.0 → freq*1.3（duration*0.35）で薄いアタック層
     * - bandpass: 基音 freq、Q=4 で木琴特有の共鳴射をエミュレート
     * @param {number} now - AudioContext の現在時刻（秒）
     * @param {number} freq - 周波数（Hz）
     * @param {number} gainPerNote - ノートあたりのゲイン
     */
    _cutePopSound: function (now, freq, gainPerNote) {
        const duration = 0.07;
        const voice = this._acquireCutePopVoice(now);
        const stealing = voice.freeAt > now;
        const startAt = stealing ? now + 0.002 : now;

        // 既存スケジュールをキャンセル
        voice.osc1.frequency.cancelScheduledValues(now);
        voice.gain1.gain.cancelScheduledValues(now);
        voice.osc2.frequency.cancelScheduledValues(now);
        voice.gain2.gain.cancelScheduledValues(now);
        voice.bandpass.frequency.cancelScheduledValues(now);

        if (stealing) {
            voice.gain1.gain.setValueAtTime(voice.gain1.gain.value, now);
            voice.gain1.gain.linearRampToValueAtTime(0.0001, startAt);
            voice.gain2.gain.setValueAtTime(voice.gain2.gain.value, now);
            voice.gain2.gain.linearRampToValueAtTime(0.0001, startAt);
        }

        // bandpass: 基音にセンターを割り当て、木琴特有の共鳴射を強調
        voice.bandpass.frequency.setValueAtTime(freq, startAt);
        voice.bandpass.Q.setValueAtTime(4, startAt);

        // osc1: 丸いメイン音
        voice.osc1.frequency.setValueAtTime(freq * 1.35, startAt);
        voice.osc1.frequency.exponentialRampToValueAtTime(freq, startAt + duration * 0.75);
        voice.gain1.gain.setValueAtTime(0.0001, startAt);
        voice.gain1.gain.exponentialRampToValueAtTime(gainPerNote, startAt + 0.004);
        voice.gain1.gain.exponentialRampToValueAtTime(0.0001, startAt + duration);
        voice.gain1.gain.setValueAtTime(0.0, startAt + duration);          // 残響カット

        // osc2: 薄いアタック層
        voice.osc2.frequency.setValueAtTime(freq * 2.0, startAt);
        voice.osc2.frequency.exponentialRampToValueAtTime(freq * 1.3, startAt + duration * 0.35);
        voice.gain2.gain.setValueAtTime(0.0001, startAt);
        voice.gain2.gain.exponentialRampToValueAtTime(gainPerNote * 0.25, startAt + 0.002);
        voice.gain2.gain.exponentialRampToValueAtTime(0.0001, startAt + duration * 0.45);
        voice.gain2.gain.setValueAtTime(0.0, startAt + duration * 0.45);   // 残響カット

        voice.freeAt = startAt + duration;
    },

    /**
     * ポコ専用ボイスプールを初期化する。
     * 各ボイス: triangle osc → gain → output
     * 全ノードは起動済みで、ゲイン 0.0001 で待機する。
     */
    _setupPokoVoices: function () {
        const ctx = this._audioContext;
        const output = this._output || ctx.destination;
        this._pokoVoices = [];
        for (let i = 0; i < this._pokoVoiceCount; i++) {
            const osc  = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'triangle';
            gain.gain.setValueAtTime(0.0, ctx.currentTime);

            osc.connect(gain);
            gain.connect(output);
            osc.start();

            this._pokoVoices.push({ osc, gain, freeAt: 0 });
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
     * CutePop 専用ボイスプールを初期化する。
     * 各ボイス: sine osc1 → gain1 ⇘
     *           triangle osc2 → gain2 → bandpass → output
     * 全ノードは起動済みで、ゲイン 0.0 で待機する。
     */
    _setupCutePopVoices: function () {
        const ctx = this._audioContext;
        const output = this._output || ctx.destination;
        this._cutePopVoices = [];
        for (let i = 0; i < this._cutePopVoiceCount; i++) {
            const osc1     = ctx.createOscillator();
            const gain1    = ctx.createGain();
            const osc2     = ctx.createOscillator();
            const gain2    = ctx.createGain();
            const bandpass = ctx.createBiquadFilter();

            osc1.type = 'sine';
            gain1.gain.setValueAtTime(0.0, ctx.currentTime);
            osc2.type = 'triangle';
            gain2.gain.setValueAtTime(0.0, ctx.currentTime);
            bandpass.type = 'bandpass';
            bandpass.Q.setValueAtTime(4, ctx.currentTime);

            osc1.connect(gain1);
            osc2.connect(gain2);
            gain1.connect(bandpass);
            gain2.connect(bandpass);
            bandpass.connect(output);

            osc1.start();
            osc2.start();

            this._cutePopVoices.push({ osc1, gain1, osc2, gain2, bandpass, freeAt: 0 });
        }
    },

    /**
     * CutePop 専用プールから空きボイスを取得する。
     * @param {number} now - AudioContext の現在時刻（秒）
     */
    _acquireCutePopVoice: function (now) {
        let best = this._cutePopVoices[0];
        for (let i = 0; i < this._cutePopVoices.length; i++) {
            const v = this._cutePopVoices[i];
            if (v.freeAt <= now) return v;
            if (v.freeAt < best.freeAt) best = v;
        }
        return best;
    },

    /**
     * 全ボイスプールを即座にミュートする。一時停止・リセット時の残響音を防ぐ。
     * スケジュール済みの AudioParam イベントをキャンセルし、ゲインを 0 に即セットする。
     */
    silenceAll: function () {
        if (!this._audioContext) return;
        const now = this._audioContext.currentTime;
        for (const v of this._voices) {
            v.gain.gain.cancelScheduledValues(now);
            v.gain.gain.setValueAtTime(0.0, now);
            v.freeAt = 0;
        }
        for (const v of this._pokoVoices) {
            v.gain.gain.cancelScheduledValues(now);
            v.gain.gain.setValueAtTime(0.0, now);
            v.freeAt = 0;
        }
        for (const v of this._cutePopVoices) {
            v.gain1.gain.cancelScheduledValues(now);
            v.gain1.gain.setValueAtTime(0.0, now);
            v.gain2.gain.cancelScheduledValues(now);
            v.gain2.gain.setValueAtTime(0.0, now);
            v.freeAt = 0;
        }
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
            try { v.osc.stop(); } catch (e) { /* already stopped */ }
        }
        this._pokoVoices = [];
        for (const v of this._cutePopVoices) {
            try { v.osc1.stop(); } catch (e) { /* already stopped */ }
            try { v.osc2.stop(); } catch (e) { /* already stopped */ }
        }
        this._cutePopVoices = [];
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


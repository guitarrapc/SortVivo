using Microsoft.JSInterop;
using System.Text.Json;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// JSON ファイルベースのローカライゼーションサービス。
/// wwwroot/locales/{lang}.json から文字列を読み込み、キー解決・プレースホルダー展開を提供する。
/// </summary>
public sealed class LocalizationService
{
    private readonly HttpClient _http;
    private JsonElement _currentStrings;
    private JsonElement _fallbackStrings; // 常に en を保持
    private bool _initialized;

    public string CurrentLanguage { get; private set; } = "en";

    /// <summary>サポートする言語コード一覧。言語追加時はここに追加するだけ。</summary>
    public static readonly string[] SupportedLanguages = ["en", "ja"];

    /// <summary>各言語の自称表示名（翻訳しない）。</summary>
    public static string GetDisplayName(string lang) => lang switch
    {
        "en" => "English",
        "ja" => "日本語",
        _ => lang,
    };

    /// <summary>言語変更イベント。購読コンポーネントは StateHasChanged を呼ぶこと。</summary>
    public event Action? OnLanguageChanged;

    public LocalizationService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// アプリ起動時に一度呼ぶ。
    /// localStorage → navigator.language の順で言語を決定し JSON をロードする。
    /// </summary>
    public async Task InitializeAsync(IJSRuntime js)
    {
        if (_initialized) return;

        // 1. localStorage から保存済み言語を確認
        string? savedLang = null;
        try
        {
            var allValues = await js.InvokeAsync<Dictionary<string, string>>("stateStorage.loadAll");
            allValues.TryGetValue("sortvis.language", out savedLang);
        }
        catch { /* JS interop が使えない場合は無視 */ }

        string lang;
        if (!string.IsNullOrEmpty(savedLang) && SupportedLanguages.Contains(savedLang))
        {
            lang = savedLang;
        }
        else
        {
            // 2. ブラウザ言語を取得してマッピング
            string browserLang = "en";
            try
            {
                browserLang = await js.InvokeAsync<string>("stateStorage.getBrowserLanguage") ?? "en";
            }
            catch { /* フォールバック */ }
            lang = MapBrowserLanguage(browserLang);
        }

        // 3. 英語は常にフォールバックとして先にロード
        _fallbackStrings = await LoadJsonAsync("en");

        if (lang == "en")
        {
            _currentStrings = _fallbackStrings;
        }
        else
        {
            _currentStrings = await LoadJsonAsync(lang);
        }

        CurrentLanguage = lang;
        _initialized = true;
    }

    /// <summary>言語を切り替える。JSON をフェッチし localStorage に保存してイベントを発火する。</summary>
    public async Task SetLanguageAsync(string lang, IJSRuntime js)
    {
        if (!SupportedLanguages.Contains(lang)) return;
        if (lang == CurrentLanguage) return;

        if (lang == "en")
        {
            _currentStrings = _fallbackStrings;
        }
        else
        {
            _currentStrings = await LoadJsonAsync(lang);
        }

        CurrentLanguage = lang;

        // localStorage に保存
        try
        {
            await js.InvokeVoidAsync("stateStorage.saveAll", new Dictionary<string, string>
            {
                ["sortvis.language"] = lang,
            });
        }
        catch { /* 無視 */ }

        OnLanguageChanged?.Invoke();
    }

    /// <summary>キー引き。ドット区切りでネストをたどる。</summary>
    public string this[string key] => Resolve(key);

    /// <summary>プレースホルダー付きキー引き。{0}, {1}... ではなく {variableName} 形式で位置順に展開する。</summary>
    public string this[string key, params object[] args] => FormatResolved(key, args);

    private string Resolve(string key)
    {
        if (TryResolve(_currentStrings, key, out var value))
            return value;
        if (TryResolve(_fallbackStrings, key, out var fallback))
            return fallback;
        return key; // キーをそのまま返す（デバッグ用）
    }

    private string FormatResolved(string key, object[] args)
    {
        var template = Resolve(key);
        if (args.Length == 0) return template;

        // {variableName} を位置順に引数で順番に置換する
        // 例: "Step {current} / {total}" + [3, 10] → "Step 3 / 10"
        int argIndex = 0;
        var result = System.Text.RegularExpressions.Regex.Replace(
            template,
            @"\{[^}]+\}",
            _ => argIndex < args.Length ? args[argIndex++]?.ToString() ?? "" : "");
        return result;
    }

    private static bool TryResolve(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (root.ValueKind == JsonValueKind.Undefined) return false;

        var parts = key.Split('.');
        var current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object) return false;
            if (!current.TryGetProperty(part, out current)) return false;
        }

        if (current.ValueKind == JsonValueKind.String)
        {
            value = current.GetString() ?? string.Empty;
            return true;
        }
        return false;
    }

    private async Task<JsonElement> LoadJsonAsync(string lang)
    {
        try
        {
            var json = await _http.GetStringAsync($"locales/{lang}.json");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return default;
        }
    }

    private static string MapBrowserLanguage(string browserLang)
    {
        if (string.IsNullOrEmpty(browserLang)) return "en";
        // "ja", "ja-JP", "ja-*" はすべて日本語へ
        if (browserLang.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja";
        return "en";
    }
}

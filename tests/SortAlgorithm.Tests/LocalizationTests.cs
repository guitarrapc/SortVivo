using System.Text.Json;
using System.Text.RegularExpressions;

namespace SortAlgorithm.Tests;

/// <summary>
/// Phase 7: i18n ローカライゼーションのテスト。
/// キー網羅チェックと、LocalizationService が実装するキー解決ロジックを検証する。
/// </summary>
public class LocalizationTests
{
    // ===== Key Coverage Test =====

    /// <summary>
    /// en.json に存在する全キーが ja.json にも存在することをアサートする。
    /// 翻訳漏れを防止する。
    /// </summary>
    [Test]
    public async Task AllKeysInEnglishExistInJapanese()
    {
        var localesDir = GetLocalesDirectory();
        var enDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(localesDir, "en.json")));
        var jaDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(localesDir, "ja.json")));

        var enKeys = FlattenKeys(enDoc.RootElement).ToHashSet();
        var jaKeys = FlattenKeys(jaDoc.RootElement).ToHashSet();

        var missing = enKeys.Except(jaKeys).OrderBy(k => k).ToList();

        await Assert.That(missing).IsEmpty()
            .Because($"ja.json に以下のキーが欠落しています:\n{string.Join("\n", missing)}");
    }

    // ===== LocalizationService Resolution Logic Tests =====
    // LocalizationService の内部実装と同一のキー解決アルゴリズムを検証する。

    [Test]
    public async Task Resolve_ExistingTopLevelKey_ReturnsValue()
    {
        const string json = """{"greeting": "Hello"}""";
        using var doc = JsonDocument.Parse(json);
        var result = Resolve(doc.RootElement, default, "greeting");
        await Assert.That(result).IsEqualTo("Hello");
    }

    [Test]
    public async Task Resolve_NestedDotSeparatedKey_ReturnsValue()
    {
        const string json = """{"nav": {"home": "Home", "tutorial": "Tutorial"}}""";
        using var doc = JsonDocument.Parse(json);
        var result = Resolve(doc.RootElement, default, "nav.home");
        await Assert.That(result).IsEqualTo("Home");
    }

    [Test]
    public async Task Resolve_DeepNestedKey_ReturnsValue()
    {
        const string json = """{"a": {"b": {"c": "deep value"}}}""";
        using var doc = JsonDocument.Parse(json);
        var result = Resolve(doc.RootElement, default, "a.b.c");
        await Assert.That(result).IsEqualTo("deep value");
    }

    [Test]
    public async Task Resolve_MissingKeyInCurrent_FallsBackToFallback()
    {
        const string current = """{"nav": {"home": "ホーム"}}""";
        const string fallback = """{"nav": {"home": "Home"}, "nav2": {"other": "Other"}}""";
        using var curDoc = JsonDocument.Parse(current);
        using var fbDoc = JsonDocument.Parse(fallback);
        var result = Resolve(curDoc.RootElement, fbDoc.RootElement, "nav2.other");
        await Assert.That(result).IsEqualTo("Other");
    }

    [Test]
    public async Task Resolve_MissingKeyInBoth_ReturnsKeyString()
    {
        const string empty = """{}""";
        using var doc = JsonDocument.Parse(empty);
        var result = Resolve(doc.RootElement, doc.RootElement, "nonexistent.key");
        await Assert.That(result).IsEqualTo("nonexistent.key");
    }

    [Test]
    public async Task Resolve_NonStringValue_ReturnsKeyString()
    {
        const string json = """{"count": 42}""";
        using var doc = JsonDocument.Parse(json);
        var result = Resolve(doc.RootElement, default, "count");
        await Assert.That(result).IsEqualTo("count");
    }

    [Test]
    public async Task Format_SinglePlaceholder_ReplacedWithArg()
    {
        var result = Format("Step {current} of total", 3);
        await Assert.That(result).IsEqualTo("Step 3 of total");
    }

    [Test]
    public async Task Format_MultiplePlaceholders_ReplacedInOrder()
    {
        var result = Format("Step {current} / {total}", 3, 10);
        await Assert.That(result).IsEqualTo("Step 3 / 10");
    }

    [Test]
    public async Task Format_NoPlaceholders_ReturnsOriginalTemplate()
    {
        var result = Format("No placeholders here");
        await Assert.That(result).IsEqualTo("No placeholders here");
    }

    [Test]
    public async Task Format_Fewer_Args_Than_Placeholders_ReplacesAvailable()
    {
        var result = Format("{a} and {b}", "X");
        await Assert.That(result).IsEqualTo("X and ");
    }

    // ===== Key Value Quality Tests =====

    [Test]
    public async Task AllJapaneseStringValues_AreNonEmpty()
    {
        var localesDir = GetLocalesDirectory();
        var jaDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(localesDir, "ja.json")));

        var emptyKeys = new List<string>();
        foreach (var (key, value) in FlattenKeyValues(jaDoc.RootElement))
        {
            if (string.IsNullOrWhiteSpace(value))
                emptyKeys.Add(key);
        }

        await Assert.That(emptyKeys).IsEmpty()
            .Because($"ja.json の以下のキーが空文字列です:\n{string.Join("\n", emptyKeys)}");
    }

    // ===== Helper Methods =====
    // LocalizationService と同じキー解決アルゴリズムを実装する。

    private static string Resolve(JsonElement current, JsonElement fallback, string key)
    {
        if (TryResolve(current, key, out var value)) return value;
        if (TryResolve(fallback, key, out var fb)) return fb;
        return key;
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

    private static string Format(string template, params object[] args)
    {
        if (args.Length == 0) return template;
        int argIndex = 0;
        return Regex.Replace(template, @"\{[^}]+\}",
            _ => argIndex < args.Length ? args[argIndex++]?.ToString() ?? "" : "");
    }

    /// <summary>
    /// JsonElement を再帰的に走査してドット区切りのフラットキー一覧を返す。
    /// 末端の文字列値を持つキーのみ収集する（オブジェクト中間ノードは除く）。
    /// </summary>
    private static IEnumerable<string> FlattenKeys(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                foreach (var k in FlattenKeys(prop.Value, fullKey))
                    yield return k;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            yield return prefix;
        }
    }

    private static IEnumerable<(string Key, string Value)> FlattenKeyValues(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                foreach (var kv in FlattenKeyValues(prop.Value, fullKey))
                    yield return kv;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            yield return (prefix, element.GetString() ?? string.Empty);
        }
    }

    /// <summary>
    /// ソリューションルートを基準に locales ディレクトリパスを解決する。
    /// </summary>
    private static string GetLocalesDirectory()
    {
        // テスト実行ディレクトリからソリューションルートを探して .slnx で識別する
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.slnx").Any())
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException("ソリューションルートが見つかりません。");

        return Path.Combine(dir.FullName, "src", "SortAlgorithm.VisualizationWeb", "wwwroot", "locales");
    }
}

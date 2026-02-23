namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// Picture Row / Column / Block モード間で画像を共有し、最大 5 件の履歴を管理するサービス。
/// </summary>
public class PictureImageService
{
    public record ImageEntry(string DataUrl, string FileName);

    private const int MaxHistory = 5;

    private readonly List<ImageEntry> _history = [];

    /// <summary>履歴リスト（先頭が最新 = 現在の画像）</summary>
    public IReadOnlyList<ImageEntry> History => _history.AsReadOnly();

    /// <summary>現在の画像（履歴の先頭）</summary>
    public ImageEntry? Current => _history.Count > 0 ? _history[0] : null;

    /// <summary>変更通知のバージョン番号（ShouldRender の比較用）</summary>
    public int Version { get; private set; }

    /// <summary>画像が追加・切り替わったときのイベント</summary>
    public event Action? OnChanged;

    /// <summary>
    /// 画像を履歴に追加して現在の画像に設定する。
    /// 同名ファイルが既にある場合は削除してから先頭に追加する。
    /// 5 件を超えた場合は末尾を削除する。
    /// </summary>
    public void AddImage(string dataUrl, string fileName)
    {
        // 同名を重複させない
        _history.RemoveAll(e => e.FileName == fileName);

        _history.Insert(0, new ImageEntry(dataUrl, fileName));

        if (_history.Count > MaxHistory)
            _history.RemoveAt(_history.Count - 1);

        Version++;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 既存の履歴エントリを現在の画像に切り替える（先頭に移動）。
    /// </summary>
    public void SetCurrent(ImageEntry entry)
    {
        var idx = _history.IndexOf(entry);
        if (idx <= 0) return; // 既に先頭 or 見つからない

        _history.RemoveAt(idx);
        _history.Insert(0, entry);

        Version++;
        OnChanged?.Invoke();
    }
}

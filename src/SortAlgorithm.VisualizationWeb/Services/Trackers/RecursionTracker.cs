using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// RecursionTree ビジュアライゼーション用トラッカー。
/// Merge Sort / Quicksort の再帰構造を操作列のシグナルから推論する。
///
/// <para><strong>追跡ロジック：</strong></para>
/// <list type="bullet">
///   <item>Merge sort: <c>RangeCopy(srcBuf=0, dstBuf=1, src=S, len=L)</c> → ノード [S..S+L*2) をアクティブ化<br/>
///     L = leftLength = mid - left + 1 なので、等分割なら S+L*2 = right+1 が成立する。</item>
///   <item>Quicksort: <c>Swap(buf=0, i≠j)</c> → [min(i,j)..max(i,j)+1) を包含する最小ノードをアクティブ化</item>
///   <item><c>Compare(i,j) buf(0,0)</c> (i≥0, j≥0) → ノード候補の確認に使用</item>
/// </list>
/// </summary>
sealed class RecursionTracker : IVisualizationTracker
{
    private readonly int _arrayLength;
    private readonly List<RecursionNode> _nodes = [];
    private int _nextNodeId;
    private int _activeNodeId = -1;

    // ナラティブ生成用の状態追跡
    private string? _cachedNarrative;
    private OperationType _lastOpType;
    private int _lastOpStart = -1;
    private int _lastOpEnd = -1;

    // Decorate() 用キャッシュ
    private RecursionSnapshot? _cachedSnapshot;

    internal RecursionTracker(int arrayLength)
    {
        _arrayLength = arrayLength;

        // ルートノード作成（全体範囲、最初は Pending）
        var root = new RecursionNode
        {
            Id       = _nextNodeId++,
            ParentId = -1,
            Start    = 0,
            End      = arrayLength,
            State    = RecursionNodeState.Pending,
            Values   = [],
            Depth    = 0
        };
        _nodes.Add(root);
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        int newStart = -1, newEnd = -1;

        switch (op.Type)
        {
            // ── Merge sort: Merge フェーズ開始シグナル ──────────────────────────
            // RangeCopy(main→buffer): src=left, len=leftLength
            // 等分割なら end = src + len * 2
            case OperationType.RangeCopy when op.BufferId1 == 0 && op.BufferId2 == 1:
                newStart = op.Index1;
                newEnd   = Math.Min(op.Index1 + op.Length * 2, _arrayLength);
                _lastOpType = OperationType.RangeCopy;
                break;

            // ── Quicksort: パーティション中の Swap ───────────────────────────
            // Swap(i, j, buf=0): 現在のパーティション作業範囲 [min..max+1) を推定
            case OperationType.Swap when op.BufferId1 == 0 && op.Index1 != op.Index2:
                newStart = Math.Min(op.Index1, op.Index2);
                newEnd   = Math.Max(op.Index1, op.Index2) + 1;
                _lastOpType = OperationType.Swap;
                break;

            // ── Compare(i, j, buf(0,0)), i≥0, j≥0 ─────────────────────────
            // Merge sort の SortCore early-exit check / Quicksort のパーティションスキャン
            case OperationType.Compare
                when op.Index1 >= 0 && op.Index2 >= 0
                  && op.BufferId1 == 0 && op.BufferId2 == 0:
                newStart = Math.Min(op.Index1, op.Index2);
                newEnd   = Math.Max(op.Index1, op.Index2) + 1;
                _lastOpType = OperationType.Compare;
                break;
        }

        if (newStart < 0)
        {
            _cachedNarrative = null; // 追跡対象外の操作はナラティブ上書きしない
            return;
        }

        _lastOpStart = newStart;
        _lastOpEnd = newEnd;

        // ── ノードを特定してアクティブ化 ─────────────────────────────────────
        var node = FindOrCreateNode(newStart, newEnd, mainArray);
        if (node == null)
        {
            _cachedNarrative = null;
            return;
        }

        var prevNode = _activeNodeId >= 0 ? FindNodeById(_activeNodeId) : null;

        if (node.Id == _activeNodeId)
        {
            // 同じノードへの操作 → ナラティブだけ更新（値はDecorate()で更新）
            GenerateNarrative(node, prevNode, op);
            RebuildSnapshot();
            return;
        }

        // 前のノードの状態を遷移させる
        if (prevNode != null && prevNode.State is RecursionNodeState.Active or RecursionNodeState.Merging)
        {
            if (node.Depth <= prevNode.Depth)
            {
                // 同じ深さ（兄弟）または上位へ → 前のノードは完了
                MarkSubtreeCompleted(prevNode.Id);
                UpdateNodeState(node.Id, node.Depth < prevNode.Depth
                    ? RecursionNodeState.Merging
                    : RecursionNodeState.Active);
            }
            else
            {
                // より深いレベルへ → 現在ノードを Active に
                UpdateNodeState(node.Id, RecursionNodeState.Active);
            }
        }
        else
        {
            UpdateNodeState(node.Id, RecursionNodeState.Active);
        }

        _activeNodeId = node.Id;
        GenerateNarrative(node, prevNode, op);
        RebuildSnapshot();
    }

    public TutorialStep Decorate(TutorialStep step)
    {
        // 全てのノードの値を最新の配列状態で更新
        if (step.ArraySnapshot != null)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Start >= 0 && node.End <= step.ArraySnapshot.Length)
                {
                    _nodes[i] = node with { Values = step.ArraySnapshot[node.Start..node.End] };
                }
            }
            // スナップショットを再構築（更新された値を反映）
            RebuildSnapshot();
        }

        return step with
        {
            Recursion = _cachedSnapshot,
            Narrative = _cachedNarrative ?? step.Narrative,
        };
    }

    public void PostStep()
    {
        _cachedNarrative = null; // 次のステップのために消去
    }

    // ─── ノード検索 / 作成 ──────────────────────────────────────────────────

    /// <summary>
    /// [start..end) のノードを探す。存在しなければ最小包含ノードの子として作成する。
    /// </summary>
    private RecursionNode? FindOrCreateNode(int start, int end, int[] mainArray)
    {
        if (start < 0 || end > _arrayLength || start >= end) return null;

        // 完全一致するノードを探す
        var existing = _nodes.FirstOrDefault(n => n.Start == start && n.End == end);
        if (existing != null) return existing;

        // 完全包含する最小の親ノードを探す（Depth 降順 = より深い祖先を優先）
        var parent = _nodes
            .Where(n => n.Start <= start && n.End >= end && (n.End - n.Start) > (end - start))
            .OrderByDescending(n => n.Depth)
            .FirstOrDefault();

        if (parent == null)
            return _nodes.FirstOrDefault(n => n.ParentId == -1); // フォールバック: ルート

        var newNode = new RecursionNode
        {
            Id       = _nextNodeId++,
            ParentId = parent.Id,
            Start    = start,
            End      = end,
            State    = RecursionNodeState.Pending,
            Values   = mainArray[start..end].ToArray(),
            Depth    = parent.Depth + 1
        };
        _nodes.Add(newNode);
        return newNode;
    }

    private RecursionNode? FindNodeById(int id)
        => _nodes.FirstOrDefault(n => n.Id == id);

    // ─── ノード状態更新 ─────────────────────────────────────────────────────

    private void UpdateNodeState(int nodeId, RecursionNodeState newState)
    {
        var idx = _nodes.FindIndex(n => n.Id == nodeId);
        if (idx >= 0)
            _nodes[idx] = _nodes[idx] with { State = newState };
    }

    /// <summary>指定ノードとその子孫を全て Completed にする。</summary>
    private void MarkSubtreeCompleted(int nodeId)
    {
        var queue = new Queue<int>();
        queue.Enqueue(nodeId);
        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            UpdateNodeState(id, RecursionNodeState.Completed);
            foreach (var child in _nodes.Where(n => n.ParentId == id))
                queue.Enqueue(child.Id);
        }
    }

    // ─── ナラティブ生成 ─────────────────────────────────────────────────────

    /// <summary>現在の操作とノード状態から説明テキストを生成する。</summary>
    private void GenerateNarrative(RecursionNode node, RecursionNode? prevNode, SortOperation op)
    {
        string rangeDesc = $"[{node.Start}..{node.End})";
        int size = node.End - node.Start;

        // ルートノード初回アクティブ化
        if (node.ParentId == -1 && prevNode == null)
        {
            _cachedNarrative = $"Start sorting entire array {rangeDesc} with {size} elements";
            return;
        }

        // 状態に応じたフェーズ説明
        switch (node.State)
        {
            case RecursionNodeState.Active when _lastOpType == OperationType.RangeCopy:
                // Merge sort: マージフェーズ開始
                _cachedNarrative = $"Merging sorted subarrays into range {rangeDesc} ({size} elements)";
                break;

            case RecursionNodeState.Active when _lastOpType == OperationType.Swap:
                // Quicksort: パーティション中
                var pivotCandidate = FindPivotCandidate(node, op);
                if (pivotCandidate.HasValue)
                    _cachedNarrative = $"Partitioning range {rangeDesc} around pivot {pivotCandidate} ({size} elements)";
                else
                    _cachedNarrative = $"Partitioning range {rangeDesc} ({size} elements)";
                break;

            case RecursionNodeState.Active when _lastOpType == OperationType.Compare:
                // 比較による作業開始（分割前チェック等）
                if (prevNode != null && node.Depth > prevNode.Depth)
                    _cachedNarrative = $"Divide: recursively sort subrange {rangeDesc} ({size} elements)";
                else
                    _cachedNarrative = $"Working on range {rangeDesc} ({size} elements)";
                break;

            case RecursionNodeState.Merging:
                // マージ中
                _cachedNarrative = $"Conquer: merge completed subranges back into {rangeDesc}";
                break;

            case RecursionNodeState.Completed:
                // 完了
                _cachedNarrative = $"Range {rangeDesc} is now sorted";
                break;

            default:
                _cachedNarrative = $"Processing range {rangeDesc}";
                break;
        }
    }

    /// <summary>
    /// Quicksortのピボット値を推定する（中央要素をピボットとする実装を想定）。
    /// </summary>
    private int? FindPivotCandidate(RecursionNode node, SortOperation op)
    {
        if (node.Values.Length == 0) return null;

        // 中央要素をピボット候補として返す（一般的なQuicksortの実装）
        int midIdx = node.Values.Length / 2;
        if (midIdx >= 0 && midIdx < node.Values.Length)
            return node.Values[midIdx];

        return null;
    }

    // ─── スナップショット再構築 ──────────────────────────────────────────────

    private void RebuildSnapshot()
    {
        _cachedSnapshot = new RecursionSnapshot
        {
            Nodes        = _nodes.ToArray(),
            ActiveNodeId = _activeNodeId
        };
    }
}

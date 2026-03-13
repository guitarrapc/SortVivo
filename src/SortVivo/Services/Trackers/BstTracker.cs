using SortVivo.Models;

namespace SortVivo.Services;

/// <summary>
/// BstTree / AvlTree ビジュアライゼーション用トラッカー。
/// BinaryTreeSort の操作列から BST/AVL 構造をシャドウ再生し、各ステップへ付加する。
/// </summary>
sealed class BstTracker : IVisualizationTracker
{
    private readonly bool _isAvl;
    private readonly int[] _values;
    private readonly int[] _left;
    private readonly int[] _right;
    private int _size;
    private int _root;
    private int[] _insertionPath;
    private int _newNode;
    private bool _isTraversalPhase;
    private int _activeNode;
    private int[] _inorderList;

    // AVL 専用状態
    private readonly int[] _avlHeight;
    private readonly int[] _avlPathBuf;
    private int[] _avlRotatedNodes;
    private string? _avlRotationDesc;

    // Decorate() 用キャッシュ
    private BstSnapshot? _cachedSnapshot;
    private string? _cachedNarrative;

    internal BstTracker(int arrayLength, bool avl)
    {
        _isAvl = avl;
        _root = -1;
        _newNode = -1;
        _activeNode = -1;
        _values = new int[arrayLength];
        _left = Enumerable.Repeat(-1, arrayLength).ToArray();
        _right = Enumerable.Repeat(-1, arrayLength).ToArray();
        _insertionPath = [];
        _inorderList = [];
        _avlHeight = avl ? new int[arrayLength] : [];
        _avlPathBuf = avl ? new int[arrayLength] : [];
        _avlRotatedNodes = [];
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        _cachedNarrative = null;

        // BUFFER_TREE operations (BufferId1 = -1) are AVL internal node accesses.
        // Skip them but preserve the previous _cachedSnapshot so the tree stays visible.
        if (op.BufferId1 != 0) return;

        _cachedSnapshot = null;

        if (op.Type == OperationType.IndexRead)
        {
            int nodeId = _size++;
            int value = mainArray[op.Index1];
            _values[nodeId] = value;
            _left[nodeId] = _right[nodeId] = -1;
            if (_isAvl) { _avlHeight[nodeId] = 1; _avlRotatedNodes = []; _avlRotationDesc = null; }

            var path = new List<int>();
            if (_root == -1)
            {
                _root = nodeId;
            }
            else
            {
                int cur = _root;
                int avlPathTop = 0;
                while (true)
                {
                    path.Add(cur);
                    if (_isAvl) _avlPathBuf[avlPathTop++] = cur;
                    if (value < _values[cur])
                    {
                        if (_left[cur] == -1) { _left[cur] = nodeId; break; }
                        cur = _left[cur];
                    }
                    else
                    {
                        if (_right[cur] == -1) { _right[cur] = nodeId; break; }
                        cur = _right[cur];
                    }
                }

                if (_isAvl)
                {
                    var rotatedList = new List<int>();
                    var rotDescs = new List<string>();
                    int subtreeRoot = nodeId;
                    int subtreeFrom = nodeId;

                    while (avlPathTop > 0)
                    {
                        int ni = _avlPathBuf[--avlPathTop];
                        if (_left[ni] == subtreeFrom) _left[ni] = subtreeRoot;
                        else if (_right[ni] == subtreeFrom) _right[ni] = subtreeRoot;

                        AvlUpdateHeight(ni);
                        var (newRoot, rotType) = AvlBalance(ni);

                        if (rotType != null)
                        {
                            rotDescs.Add($"{rotType} at {_values[ni]}");
                            rotatedList.Add(ni);
                            if (newRoot != ni) rotatedList.Add(newRoot);
                        }
                        subtreeFrom = ni;
                        subtreeRoot = newRoot;
                    }

                    _root = subtreeRoot;
                    _avlRotatedNodes = [.. rotatedList.Distinct()];
                    _avlRotationDesc = rotDescs.Count > 0 ? string.Join("; ", rotDescs) : null;
                }
            }

            _insertionPath = [.. path];
            _newNode = nodeId;

            _cachedNarrative = _root == _newNode
                ? (_isAvl
                    ? $"Insert {_values[_newNode]} as AVL root"
                    : $"Insert {_values[_newNode]} as BST root")
                : _avlRotationDesc != null
                    ? $"Insert {_values[_newNode]} at depth {_insertionPath.Length} — {_avlRotationDesc}"
                    : _isAvl
                        ? $"Insert {_values[_newNode]} into AVL tree at depth {_insertionPath.Length}"
                        : $"Insert {_values[_newNode]} into BST at depth {_insertionPath.Length}";
        }
        else if (op.Type == OperationType.IndexWrite)
        {
            if (!_isTraversalPhase)
            {
                _isTraversalPhase = true;
                _inorderList = ComputeInorder();
                _insertionPath = [];
                _newNode = -1;
                if (_isAvl) { _avlRotatedNodes = []; _avlRotationDesc = null; }
            }
            _activeNode = op.Index1 < _inorderList.Length ? _inorderList[op.Index1] : -1;
            _cachedNarrative = $"In-order traversal: write {op.Value} to index {op.Index1}";
        }

        _cachedSnapshot = new BstSnapshot
        {
            Size = _size,
            Root = _root,
            Values = _values[.._size],
            Left = _left[.._size],
            Right = _right[.._size],
            InsertionPath = _insertionPath,
            NewNode = _newNode,
            ActiveNode = _activeNode,
            IsTraversalPhase = _isTraversalPhase,
            Heights = _isAvl ? _avlHeight[.._size] : null,
            RotatedNodes = _isAvl ? _avlRotatedNodes : [],
        };
    }

    public TutorialStep Decorate(TutorialStep step)
    {
        if (_cachedSnapshot == null) return step;
        return step with
        {
            Bst = _cachedSnapshot,
            Narrative = _cachedNarrative ?? step.Narrative,
        };
    }

    public void PostStep() { }

    // BST helpers

    /// <summary>
    /// 非再帰の中順走査で BST の全ノードを訪問順に列挙する。
    /// </summary>
    private int[] ComputeInorder()
    {
        var result = new List<int>(_size);
        var stack = new Stack<int>();
        int cur = _root;
        while (cur != -1 || stack.Count > 0)
        {
            while (cur != -1) { stack.Push(cur); cur = _left[cur]; }
            cur = stack.Pop();
            result.Add(cur);
            cur = _right[cur];
        }
        return [.. result];
    }

    // AVL helpers

    private void AvlUpdateHeight(int i)
    {
        int lh = _left[i] >= 0 ? _avlHeight[_left[i]] : 0;
        int rh = _right[i] >= 0 ? _avlHeight[_right[i]] : 0;
        _avlHeight[i] = 1 + Math.Max(lh, rh);
    }

    private int AvlGetBalance(int i)
    {
        int lh = _left[i] >= 0 ? _avlHeight[_left[i]] : 0;
        int rh = _right[i] >= 0 ? _avlHeight[_right[i]] : 0;
        return lh - rh;
    }

    /// <summary>y を中心に右回転。新しいサブツリー根 (x = y.Left) を返す。</summary>
    private int AvlRotateRight(int y)
    {
        int x = _left[y];
        int t2 = _right[x];
        _right[x] = y;
        _left[y] = t2;
        AvlUpdateHeight(y);
        AvlUpdateHeight(x);
        return x;
    }

    /// <summary>x を中心に左回転。新しいサブツリー根 (y = x.Right) を返す。</summary>
    private int AvlRotateLeft(int x)
    {
        int y = _right[x];
        int t2 = _left[y];
        _left[y] = x;
        _right[x] = t2;
        AvlUpdateHeight(x);
        AvlUpdateHeight(y);
        return y;
    }

    /// <summary>
    /// node を根とするサブツリーを再平衡化する。
    /// (newRoot, rotationType) を返す。rotationType は "LL"/"RR"/"LR"/"RL" または null（回転なし）。
    /// </summary>
    private (int newRoot, string? rotationType) AvlBalance(int node)
    {
        int bf = AvlGetBalance(node);

        if (bf > 1)
        {
            int li = _left[node];
            string rotType;
            if (AvlGetBalance(li) < 0)
            {
                _left[node] = AvlRotateLeft(li);
                rotType = "LR";
            }
            else
            {
                rotType = "LL";
            }
            return (AvlRotateRight(node), rotType);
        }

        if (bf < -1)
        {
            int ri = _right[node];
            string rotType;
            if (AvlGetBalance(ri) > 0)
            {
                _right[node] = AvlRotateRight(ri);
                rotType = "RL";
            }
            else
            {
                rotType = "RR";
            }
            return (AvlRotateLeft(node), rotType);
        }

        return (node, null);
    }
}

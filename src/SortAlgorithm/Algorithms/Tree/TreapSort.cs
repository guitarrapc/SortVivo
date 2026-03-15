using System.Buffers;
using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// Treap(Tree + Heap)を使用したランダム化ソートアルゴリズム。
/// Treapはランダム化された二分探索木で、各ノードにランダムな優先度を割り当て、
/// キーに対してBST順序、優先度に対して最大ヒープ順序を同時に満たす。
/// ランダム優先度により、入力パターンに関わらず期待計算量 O(n log n) を達成する。
/// <br/>
/// Treap (tree + heap) based randomized sorting algorithm. A treap is a randomized BST
/// where each node is assigned a random priority. The tree maintains BST order on keys
/// and max-heap order on priorities simultaneously. Random priorities ensure expected
/// O(n log n) performance regardless of input pattern.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Treap Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>BST Property:</strong> For every node, all values in the left subtree are less than the node's value,
/// and all values in the right subtree are greater than or equal to the node's value.
/// Maintained by comparing values during insertion (go left if value &lt; node, right otherwise).</description></item>
/// <item><description><strong>Heap Property:</strong> Each node's priority is greater than or equal to the priorities of its children.
/// After BST insertion at a leaf, the node is rotated upward until the heap property is restored.</description></item>
/// <item><description><strong>Rotation Correctness:</strong> All rotations preserve the BST in-order property while restoring
/// the heap property. Only single rotations (left or right) are needed, applied bottom-up after insertion.</description></item>
/// <item><description><strong>In-order Traversal:</strong> Iterative left→root→right traversal writes elements in sorted order.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Tree / Randomized</description></item>
/// <item><description>Stable      : No (random priorities determine tree structure; equal elements may be reordered by rotations)</description></item>
/// <item><description>In-place    : No (requires O(n) auxiliary space for tree nodes with parent pointers and priorities)</description></item>
/// <item><description>Best case   : Θ(n log n) expected</description></item>
/// <item><description>Average case: Θ(n log n) expected - guaranteed by random priority assignment</description></item>
/// <item><description>Worst case  : O(n²) with astronomically low probability (when random priorities produce degenerate tree)</description></item>
/// <item><description>Comparisons : O(n log n) expected</description></item>
/// <item><description>Index Reads : Θ(n) - each element read once from main array during insertion</description></item>
/// <item><description>Index Writes: Θ(n) - each element written once during in-order traversal</description></item>
/// <item><description>Swaps       : 0 - no swapping; elements are copied to tree nodes and written back during traversal</description></item>
/// <item><description>Space       : O(n) - one node per element; each node holds value, left/right/parent indices, and priority</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Treap</para>
/// <para>Original paper: Aragon, C. R.; Seidel, R. (1989). "Randomized Search Trees"</para>
/// </remarks>
public static class TreapSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TREE = -1;      // Tree nodes (virtual buffer, negative to exclude from main statistics)
    private const int NULL_INDEX = -1;       // Represents null reference in arena
    private const uint XORSHIFT_SEED = 0x9E3779B9u; // Golden ratio derived; deterministic seed for reproducible priority generation

    // Note: Arena (Node array) operations are not tracked via SortSpan because:
    // 1. Nodes are internal implementation details (tree structure metadata)
    // 2. Nodes cache values (T) directly for performance (avoiding indirection on every comparison)
    // 3. Only the initial Read and final Write on the original data array represent core data access

    /// <summary>
    /// Sorts the elements in the specified span in ascending order using the default comparer.
    /// Uses NullContext for zero-overhead fast path.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="span">The span of elements to sort in place.</param>
    public static void Sort<T>(Span<T> span) where T : IComparable<T>
        => Sort(span, new ComparableComparer<T>(), NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="context">The sort context for tracking operations. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TComparer and TContext type parameters.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context, uint seed = XORSHIFT_SEED)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var arena = ArrayPool<Node<T>>.Shared.Rent(span.Length);
        try
        {
            var arenaSpan = arena.AsSpan(0, span.Length);
            var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
            var rootIndex = NULL_INDEX;
            var nodeCount = 0;
            var rngState = seed == 0 ? XORSHIFT_SEED : seed;

            // Insert each element into the treap; after each insertion the node is rotated up to restore heap property
            for (var i = 0; i < s.Length; i++)
            {
                context.OnPhase(SortPhase.TreeSortInsert, i, s.Length - 1);
                context.OnRole(i, BUFFER_MAIN, RoleType.Inserting);
                rootIndex = Insert(arenaSpan, rootIndex, ref nodeCount, i, s, ref rngState);
                context.OnRole(i, BUFFER_MAIN, RoleType.None);
            }

            // Traverse the treap in-order and write sorted elements back into the span
            context.OnPhase(SortPhase.TreeSortExtract);
            var writeIndex = 0;
            Inorder(s, arenaSpan, rootIndex, ref writeIndex);
        }
        finally
        {
            ArrayPool<Node<T>>.Shared.Return(arena, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>
    /// Inserts the element at <paramref name="itemIndex"/> into the treap via standard BST insertion,
    /// assigns a pseudo-random priority, then rotates the node upward until the max-heap property is restored.
    /// Returns the (possibly new) root index.
    /// </summary>
    private static int Insert<T, TComparer, TContext>(
        Span<Node<T>> arena, int rootIndex, ref int nodeCount, int itemIndex,
        SortSpan<T, TComparer, TContext> s, ref uint rngState)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var value = s.Read(itemIndex);
        var priority = NextPriority(ref rngState);

        // Empty tree: create root directly
        if (rootIndex == NULL_INDEX)
            return CreateNode(arena, value, priority, ref nodeCount, s.Context);

        // Standard BST traversal to find the insertion point
        var current = rootIndex;
        while (true)
        {
            var cmp = CompareWithNode(arena, current, itemIndex, value, s.Comparer, s.Context);
            if (cmp < 0)
            {
                // value < node → go left
                if (arena[current].Left == NULL_INDEX)
                {
                    var newIndex = CreateNode(arena, value, priority, ref nodeCount, s.Context);
                    arena[current].Left = newIndex;
                    arena[newIndex].Parent = current;
                    current = newIndex;
                    break;
                }
                current = arena[current].Left;
            }
            else
            {
                // value >= node → go right
                if (arena[current].Right == NULL_INDEX)
                {
                    var newIndex = CreateNode(arena, value, priority, ref nodeCount, s.Context);
                    arena[current].Right = newIndex;
                    arena[newIndex].Parent = current;
                    current = newIndex;
                    break;
                }
                current = arena[current].Right;
            }
        }

        // Rotate the newly inserted node upward to restore max-heap property on priorities
        return HeapUp(arena, current, ref rootIndex);
    }

    /// <summary>
    /// Rotates node <paramref name="x"/> upward until its priority is no longer greater than its parent's priority,
    /// restoring the max-heap property. Returns the (possibly new) root index.
    /// </summary>
    private static int HeapUp<T>(Span<Node<T>> arena, int x, ref int rootIndex)
    {
        while (arena[x].Parent != NULL_INDEX)
        {
            var p = arena[x].Parent;
            if (arena[x].Priority <= arena[p].Priority)
                break;

            // x has higher priority than parent → rotate x up
            if (arena[p].Left == x)
                RotateRight(arena, p);
            else
                RotateLeft(arena, p);
        }

        // If x has no parent, it is the new root
        if (arena[x].Parent == NULL_INDEX)
            rootIndex = x;

        return rootIndex;
    }

    /// <summary>
    /// Left rotation: y = x.Right becomes the new subtree root; x becomes y.Left.
    /// Updates parent pointers for x, y, and y's former left child.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RotateLeft<T>(Span<Node<T>> arena, int x)
    {
        var y = arena[x].Right;

        // x.Right = y.Left
        arena[x].Right = arena[y].Left;
        if (arena[y].Left != NULL_INDEX)
            arena[arena[y].Left].Parent = x;

        // y inherits x's parent
        arena[y].Parent = arena[x].Parent;
        if (arena[x].Parent != NULL_INDEX)
        {
            if (arena[arena[x].Parent].Left == x)
                arena[arena[x].Parent].Left = y;
            else
                arena[arena[x].Parent].Right = y;
        }

        arena[y].Left = x;
        arena[x].Parent = y;
    }

    /// <summary>
    /// Right rotation: y = x.Left becomes the new subtree root; x becomes y.Right.
    /// Updates parent pointers for x, y, and y's former right child.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RotateRight<T>(Span<Node<T>> arena, int x)
    {
        var y = arena[x].Left;

        // x.Left = y.Right
        arena[x].Left = arena[y].Right;
        if (arena[y].Right != NULL_INDEX)
            arena[arena[y].Right].Parent = x;

        // y inherits x's parent
        arena[y].Parent = arena[x].Parent;
        if (arena[x].Parent != NULL_INDEX)
        {
            if (arena[arena[x].Parent].Right == x)
                arena[arena[x].Parent].Right = y;
            else
                arena[arena[x].Parent].Left = y;
        }

        arena[y].Right = x;
        arena[x].Parent = y;
    }

    /// <summary>
    /// Iterative in-order traversal (left → root → right) using an explicit stack.
    /// Writes sorted elements back into the original span via <paramref name="s"/>.
    /// </summary>
    private static void Inorder<T, TComparer, TContext>(
        SortSpan<T, TComparer, TContext> s, Span<Node<T>> arena, int rootIndex, ref int writeIndex)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (rootIndex == NULL_INDEX) return;

        // Stack depth bounded by tree height; use stackalloc for small trees, ArrayPool for large
        int[]? rented = null;
        Span<int> stack = s.Length <= 128
            ? stackalloc int[s.Length]
            : (rented = ArrayPool<int>.Shared.Rent(s.Length)).AsSpan(0, s.Length);
        try
        {
            var stackTop = 0;
            var current = rootIndex;

            while (stackTop > 0 || current != NULL_INDEX)
            {
                // Push all left descendants onto the stack
                while (current != NULL_INDEX)
                {
                    stack[stackTop++] = current;
                    current = arena[current].Left;
                }

                // Visit the node at the top of the stack
                current = stack[--stackTop];
                var value = ReadNodeValue(arena, current, s.Context);
                s.Write(writeIndex++, value);

                // Move to the right subtree
                current = arena[current].Right;
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Allocates a new arena node with the given value and priority, and records its creation for visualization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateNode<T, TContext>(Span<Node<T>> arena, T value, int priority, ref int nodeCount, TContext context)
        where TContext : ISortContext
    {
        var nodeIndex = nodeCount++;
        arena[nodeIndex] = new Node<T>(value, priority);
        context.OnIndexWrite(nodeIndex, BUFFER_TREE, value);
        return nodeIndex;
    }

    /// <summary>
    /// Reads a node's cached value and records the access for visualization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ReadNodeValue<T, TContext>(Span<Node<T>> arena, int nodeIndex, TContext context)
        where TContext : ISortContext
    {
        context.OnIndexRead(nodeIndex, BUFFER_TREE);
        return arena[nodeIndex].Value;
    }

    /// <summary>
    /// Compares <paramref name="value"/> against the cached value of the node at <paramref name="nodeIndex"/>.
    /// Records both the node access and the comparison for visualization and statistics.
    /// Returns negative if value &lt; node, zero if equal, positive if value &gt; node.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareWithNode<T, TComparer, TContext>(
        Span<Node<T>> arena, int nodeIndex, int itemIndex, T value, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        context.OnIndexRead(nodeIndex, BUFFER_TREE);
        var cmp = comparer.Compare(value, arena[nodeIndex].Value);
        context.OnCompare(itemIndex, nodeIndex, cmp, BUFFER_MAIN, BUFFER_TREE);
        return cmp;
    }

    /// <summary>
    /// Generates the next pseudo-random priority using xorshift32.
    /// Deterministic: same seed always produces the same sequence, ensuring reproducible
    /// tree shapes for consistent visualization, statistics, and benchmarks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextPriority(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (int)state;
    }

    /// <summary>
    /// Arena-based node structure with value caching, parent pointer, and random priority for treap operations.
    /// </summary>
    /// <remarks>
    /// Struct-based to eliminate GC pressure (allocated via ArrayPool).
    /// Left, Right, and Parent are indices into the arena array (-1 represents null).
    /// Value caches the T instance directly to avoid span[index] indirection on every comparison.
    /// Priority is a pseudo-random integer used to maintain max-heap order via rotations.
    /// Parent pointer enables bottom-up heap-up without a separate path stack.
    /// The node's identity is its position in the arena array, so no separate Id field is needed.
    /// </remarks>
    private struct Node<T>
    {
        public T Value;      // Cached value for direct comparison (avoids span indirection)
        public int Left;     // Index in arena, -1 = null
        public int Right;    // Index in arena, -1 = null
        public int Parent;   // Index in arena, -1 = no parent (root)
        public int Priority; // Random priority for max-heap property

        public Node(T value, int priority)
        {
            Value = value;
            Left = -1;
            Right = -1;
            Parent = -1;
            Priority = priority;
        }
    }
}

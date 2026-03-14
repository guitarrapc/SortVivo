using System.Buffers;
using System.Runtime.CompilerServices;
using SortAlgorithm.Contexts;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// バイナリ検索木(Binary Search Tree, BST)を使用したソートアルゴリズム、二分木ソートとも呼ばれる。
/// バイナリ検索木では、左の子ノードは親ノードより小さく、右の子ノードは親ノードより大きいことが保証される。
/// この特性により、木の中間順序走査 (in-order traversal) を行うことで配列がソートされる。
/// ただし、木が不均衡になると最悪ケースでO(n²)の時間がかかる可能性がある。
/// <br/>
/// A sorting algorithm that uses a binary search tree. In a binary search tree, the left child node is guaranteed to be smaller than the parent node, and the right child node is guaranteed to be larger.
/// This property ensures that performing an in-order traversal of the tree results in a sorted array.
/// However, an unbalanced tree can lead to O(n²) worst-case time complexity.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct Binary Tree Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Binary Search Tree Property:</strong> For every node, all values in the left subtree must be less than the node's value, and all values in the right subtree must be greater than or equal to the node's value.
/// This implementation maintains this invariant during insertion (value &lt; current goes left, value ≥ current goes right).</description></item>
/// <item><description><strong>Complete Tree Construction:</strong> All n elements must be inserted into the BST.
/// Each insertion reads one element from the array (n reads total).</description></item>
/// <item><description><strong>In-Order Traversal:</strong> The tree must be traversed in in-order (left → root → right) to produce sorted output.
/// This traversal visits each node exactly once, writing n elements back to the array.</description></item>
/// <item><description><strong>Comparison Consistency:</strong> The comparison operation must be consistent and transitive.
/// For all elements a, b, c: if a &lt; b and b &lt; c, then a &lt; c.</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Tree-based sorting</description></item>
/// <item><description>Stable      : No (equal elements may be reordered based on insertion order)</description></item>
/// <item><description>In-place    : No (Requires O(n) auxiliary space for tree nodes)</description></item>
/// <item><description>Best case   : Θ(n log n) - Balanced tree (e.g., random input or middle-out insertion)</description></item>
/// <item><description>Average case: Θ(n log n) - Tree height is O(log n), each insertion takes O(log n) comparisons</description></item>
/// <item><description>Worst case  : Θ(n²) - Completely unbalanced tree (e.g., sorted or reverse-sorted input forms a linear chain)</description></item>
/// <item><description>Comparisons : Best Θ(n log n), Average Θ(n log n), Worst Θ(n²)</description></item>
/// <item><description>  - Sorted input: n(n-1)/2 comparisons (each insertion compares with all previous elements)</description></item>
/// <item><description>  - Random input: ~1.39n log n comparisons (empirically, for balanced trees)</description></item>
/// <item><description>Index Reads : Θ(n) - Each element is read once during tree construction</description></item>
/// <item><description>Index Writes: Θ(n) - Each element is written once during in-order traversal</description></item>
/// <item><description>Swaps       : 0 (No swapping; elements are copied to tree nodes and then back to array)</description></item>
/// <item><description>Space       : O(n) - One struct node per element; allocated via ArrayPool (no per-node GC allocation)</description></item>
/// </list>
/// <para><strong>Implementation Notes:</strong></para>
/// <list type="bullet">
/// <item><description>Uses iterative insertion instead of recursive insertion to reduce call stack overhead</description></item>
/// <item><description>Tree nodes are struct-based and allocated via <see cref="System.Buffers.ArrayPool{T}"/> (arena); Left/Right are integer indices into the arena (-1 = null)</description></item>
/// <item><description>Equal elements are inserted to the right subtree (value ≥ current), making the sort unstable</description></item>
/// <item><description>No tree balancing is performed; for guaranteed O(n log n) performance, consider using AVL or Red-Black tree variants</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Tree_sort</para>
/// </remarks>
public static class BinaryTreeSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TREE = -1;      // Tree nodes (virtual buffer for visualization, negative to exclude from statistics)
    private const int NULL_INDEX = -1;       // Represents null reference in arena

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
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
        where T : IComparable<T>
        where TContext : ISortContext
        => Sort(span, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// This is the full-control version with explicit TContext type parameter.
    /// </summary>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
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

            for (var i = 0; i < s.Length; i++)
            {
                context.OnPhase(SortPhase.TreeSortInsert, i, s.Length - 1);
                context.OnRole(i, BUFFER_MAIN, RoleType.Inserting);
                var value = s.Read(i);
                rootIndex = InsertIterative(arenaSpan, rootIndex, ref nodeCount, value, comparer, context);
                context.OnRole(i, BUFFER_MAIN, RoleType.None);
            }

            // Traverse the tree in inorder and write elements back into the array.
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
    /// Iterative insertion. Instead of using recursion, it loops to find the child nodes.
    /// Returns the root index (unchanged unless the tree was empty).
    /// </summary>
    private static int InsertIterative<T, TComparer, TContext>(Span<Node<T>> arena, int rootIndex, ref int nodeCount, T value, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // If the tree is empty, create a new root and return.
        if (rootIndex == NULL_INDEX)
            return CreateNode(arena, value, ref nodeCount, context);

        // Iterate left & right node and insert.
        // If there's an existing tree, use 'current' to traverse down the children.
        var current = rootIndex;
        while (true)
        {
            // If the value is smaller than the current node, go left.
            if (comparer.Compare(value, arena[current].Value) < 0)
            {
                // If the left child is null, insert here.
                if (arena[current].Left == NULL_INDEX)
                {
                    arena[current].Left = CreateNode(arena, value, ref nodeCount, context);
                    break;
                }
                // Otherwise, move further down to the left child.
                current = arena[current].Left;
            }
            else
            {
                // If the value is greater or equal, go right.
                if (arena[current].Right == NULL_INDEX)
                {
                    arena[current].Right = CreateNode(arena, value, ref nodeCount, context);
                    break;
                }
                // Otherwise, move further down to the right child.
                current = arena[current].Right;
            }
        }
        return rootIndex;
    }

    /// <summary>
    /// Iterative in-order traversal (left → root → right) using an explicit stack.
    /// Writes sorted elements back into the original span via <paramref name="s"/>.
    /// </summary>
    private static void Inorder<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, Span<Node<T>> arena, int rootIndex, ref int writeIndex)
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
                s.Write(writeIndex++, arena[current].Value);

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

    // Helper methods for node operations (encapsulates visualization tracking)

    /// <summary>
    /// Allocates a new arena node, caches <paramref name="value"/>, and records its creation for visualization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateNode<T, TContext>(Span<Node<T>> arena, T value, ref int nodeCount, TContext context)
        where TContext : ISortContext
    {
        var nodeIndex = nodeCount++;
        arena[nodeIndex] = new Node<T>(value);
        context.OnIndexWrite(nodeIndex, BUFFER_TREE, value);
        return nodeIndex;
    }

    /// <summary>
    /// Arena-based node structure for binary tree sort.
    /// </summary>
    /// <remarks>
    /// Struct-based to eliminate GC pressure (allocated via ArrayPool).
    /// Left and Right are indices into the arena array (-1 represents null).
    /// Value caches the T instance directly to avoid span indirection on every comparison.
    /// The node's identity is its position in the arena array, so no separate Id field is needed.
    /// </remarks>
    private struct Node<T>
    {
        public T Value;     // Cached value for direct comparison
        public int Left;    // Index in arena, -1 = null
        public int Right;   // Index in arena, -1 = null

        public Node(T value)
        {
            Value = value;
            Left = NULL_INDEX;
            Right = NULL_INDEX;
        }
    }
}

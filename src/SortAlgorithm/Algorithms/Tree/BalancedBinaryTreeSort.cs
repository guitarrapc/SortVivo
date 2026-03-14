using System.Buffers;
using SortAlgorithm.Contexts;
using System.Runtime.CompilerServices;

namespace SortAlgorithm.Algorithms;

/// <summary>
/// Arena-based balanced binary tree sort (AVL tree) using struct nodes for optimal memory performance.
/// 平衡二分木（AVL木）を用いた二分木ソート。挿入時に回転を行って常に高さが O(log n) に保たれ、木を中順巡回することで配列に要素を昇順で再割り当てします。
/// 二分木ソートに比べて、平均および最悪ケースでも O(n log n) のソートが保証されます。
/// <br/>
/// Balanced binary tree sort using AVL tree. Rotations are performed during insertion to maintain a height of O(log n), and an in-order traversal reassigns array elements in ascending order.
/// Compared to binary tree sort, it guarantees O(n log n) sorting in both average and worst cases.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct AVL Tree Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Binary Search Tree Property:</strong> For every node, all values in the left subtree must be less than the node's value,
/// and all values in the right subtree must be greater than or equal to the node's value.
/// This property is maintained by comparing values during insertion.</description></item>
/// <item><description><strong>AVL Balance Property:</strong> For every node, the height difference between left and right subtrees (balance factor) must be at most 1.
/// Balance factor = height(left subtree) - height(right subtree) ∈ {-1, 0, 1}.
/// This is enforced by the Balance() method after every insertion.</description></item>
/// <item><description><strong>Height Maintenance:</strong> Each node stores its height, which is updated after any structural change.
/// Height = 1 + max(height(left), height(right)).
/// This is computed by UpdateHeight() after insertions and rotations.</description></item>
/// <item><description><strong>Rotation Correctness:</strong> When the balance factor violates the AVL property (|balance| > 1), rotations restore balance while preserving BST property:
/// <list type="bullet">
/// <item><description>Left-Left case (balance > 1, left child balanced): Single right rotation</description></item>
/// <item><description>Left-Right case (balance > 1, left child right-heavy): Left rotation on left child, then right rotation</description></item>
/// <item><description>Right-Right case (balance &lt; -1, right child balanced): Single left rotation</description></item>
/// <item><description>Right-Left case (balance &lt; -1, right child left-heavy): Right rotation on right child, then left rotation</description></item>
/// </list>
/// All rotations preserve the in-order traversal sequence.</description></item>
/// <item><description><strong>In-order Traversal Correctness:</strong> Visiting nodes in left-root-right order produces elements in sorted ascending order.
/// This is guaranteed by the BST property and implemented iteratively to avoid stack overflow.</description></item>
/// </list>
/// <para><strong>Mathematical Proof of O(log n) Height:</strong></para>
/// <list type="bullet">
/// <item><description>Let N(h) = minimum number of nodes in an AVL tree of height h</description></item>
/// <item><description>N(h) = N(h-1) + N(h-2) + 1 (Fibonacci-like recurrence)</description></item>
/// <item><description>N(h) ≥ F(h+2) - 1, where F is the Fibonacci sequence</description></item>
/// <item><description>Therefore, h ≤ 1.44 × log₂(n + 2) - 0.328 ≈ O(log n)</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Tree</description></item>
/// <item><description>Stable      : No (does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : No (requires O(n) auxiliary space for tree structure)</description></item>
/// <item><description>Best case   : Θ(n log n) - Already sorted or reversed, still requires building balanced tree</description></item>
/// <item><description>Average case: Θ(n log n) - Each of n insertions takes O(log n) comparisons</description></item>
/// <item><description>Worst case  : O(n log n) - Guaranteed by AVL balancing property</description></item>
/// <item><description>Comparisons : O(n log n) - Each insertion performs at most log₂(n) comparisons</description></item>
/// <item><description>Index Reads : Θ(n log n) - With ItemIndex implementation, each comparison reads both values (2 reads per comparison) plus n traversal reads</description></item>
/// <item><description>Index Writes: Θ(n) - Each element is written once during in-order traversal</description></item>
/// <item><description>Swaps       : 0 - No swaps performed; only tree node manipulations</description></item>
/// <item><description>Rotations   : O(n log n) worst case - At most 2 rotations per insertion (amortized O(1))</description></item>
/// </list>
/// <para><strong>Advantages over unbalanced BST:</strong></para>
/// <list type="bullet">
/// <item><description>Guaranteed O(n log n) time complexity even on sorted/reversed input (BST degrades to O(n²))</description></item>
/// <item><description>Predictable performance regardless of input distribution</description></item>
/// <item><description>Height always bounded by 1.44 × log₂(n)</description></item>
/// </list>
/// <para><strong>Reference:</strong></para>
/// <para>Wiki: https://en.wikipedia.org/wiki/Tree_sort</para>
/// </remarks>
public static class BalancedBinaryTreeSort
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array
    private const int BUFFER_TREE = -1;      // Tree nodes (virtual buffer for visualization, negative to exclude from statistics)
    private const int NULL_INDEX = -1;       // Represents null reference in arena

    // Note: Arena (Node array) operations are not tracked via SortSpan because:
    // 1. Nodes are internal implementation details (tree structure metadata)
    // 2. Nodes cache values (T) directly for performance (avoiding indirection overhead)
    // 3. Only the initial Read and final Write operations on the original data array
    //    represent the algorithm's core data access and are tracked via SortSpan
    // 4. Alternative design (storing only span indices in nodes) would require span[index] lookup on every
    //    comparison, increasing indirection and often reducing performance noticeably. (up to 3x slower than class-based approach in local benchmarks for this project)

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

        // Allocate path stack for iterative insertion
        // AVL tree theoretical height: h ≤ 1.44 * log₂(n+2) - 0.328
        // Add +2 margin to cover any rounding and the -0.328 offset
        var avlMaxHeight = span.Length <= 16
            ? span.Length
            : (int)Math.Ceiling(1.44 * Math.Log2(span.Length + 2)) + 2;

        // Use ArrayPool for arena allocation
        // Note: Cannot use stackalloc with Node<T> when T might be a reference type
        var arena = ArrayPool<Node<T>>.Shared.Rent(span.Length);
        int[]? rentedPathStack = null;
        Span<int> pathStack = avlMaxHeight <= 128
            ? stackalloc int[avlMaxHeight]
            : (rentedPathStack = ArrayPool<int>.Shared.Rent(avlMaxHeight)).AsSpan(0, avlMaxHeight);
        try
        {
            var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);
            var rootIndex = NULL_INDEX;
            var nodeCount = 0;

            // Insert each element into the AVL tree (iteratively with rebalancing)
            for (int i = 0; i < s.Length; i++)
            {
                context.OnPhase(SortPhase.TreeSortInsert, i, s.Length - 1);
                context.OnRole(i, BUFFER_MAIN, RoleType.RightPointer);
                rootIndex = InsertIterative(arena, rootIndex, ref nodeCount, i, s, pathStack);
                context.OnRole(i, BUFFER_MAIN, RoleType.None);
            }

            // Traverse in order and write back into the array (iterative to avoid stack overflow)
            context.OnPhase(SortPhase.TreeSortExtract);
            var writeIndex = 0;
            Inorder(s, arena, rootIndex, ref writeIndex, avlMaxHeight);
        }
        finally
        {
            ArrayPool<Node<T>>.Shared.Return(arena, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            if (rentedPathStack is not null)
            {
                ArrayPool<int>.Shared.Return(rentedPathStack);
            }
        }
    }

    /// <summary>
    /// Iteratively insert into the AVL tree using path stack, then rebalance.
    /// Completely recursion-free to avoid stack overhead.
    /// The value is read once from SortSpan (tracked) and then cached in the node.
    /// Performance optimization: Node.Value caches T directly to avoid span[index] indirection on every comparison.
    /// </summary>
    /// <param name="itemIndex">Index in the original span to read the value from.</param>
    /// <returns>Index of the new root of the tree.</returns>
    private static int InsertIterative<T, TComparer, TContext>(Span<Node<T>> arena, int rootIndex, ref int nodeCount, int itemIndex, SortSpan<T, TComparer, TContext> s, Span<int> pathStack)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Read the value to insert (tracked by SortSpan for statistics)
        var insertValue = s.Read(itemIndex);

        // If tree is empty, create root
        if (rootIndex == NULL_INDEX)
        {
            return CreateNode(arena, insertValue, ref nodeCount, s.Context);
        }

        // Phase 1: Navigate to insertion point and track path
        var stackTop = 0;
        var currentIndex = rootIndex;

        while (true)
        {
            pathStack[stackTop++] = currentIndex;
            // Compare against cached value (no span indirection needed)
            var cmp = CompareWithNode(arena, currentIndex, insertValue, s.Comparer, s.Context);

            if (cmp < 0)
            {
                // Go left
                var leftIndex = arena[currentIndex].Left;
                if (leftIndex == NULL_INDEX)
                {
                    // Insert here
                    var newIndex = CreateNode(arena, insertValue, ref nodeCount, s.Context);
                    arena[currentIndex].Left = newIndex;
                    currentIndex = newIndex;
                    break;
                }
                currentIndex = leftIndex;
            }
            else
            {
                // Go right
                var rightIndex = arena[currentIndex].Right;
                if (rightIndex == NULL_INDEX)
                {
                    // Insert here
                    var newIndex = CreateNode(arena, insertValue, ref nodeCount, s.Context);
                    arena[currentIndex].Right = newIndex;
                    currentIndex = newIndex;
                    break;
                }
                currentIndex = rightIndex;
            }
        }

        // Phase 2: Rebalance upward along the insertion path
        // subtreeRoot: the (possibly rotated) root of the subtree we just finished processing
        // subtreeFrom: the original node index before any rotation (for parent to identify which child)
        var subtreeRoot = currentIndex;
        int subtreeFrom = currentIndex;

        while (stackTop > 0)
        {
            var nodeIndex = pathStack[--stackTop];

            // Ensure nodeIndex points to the correct child subtree root (propagate up)
            if (arena[nodeIndex].Left == subtreeFrom)
            {
                arena[nodeIndex].Left = subtreeRoot;
            }
            else if (arena[nodeIndex].Right == subtreeFrom)
            {
                arena[nodeIndex].Right = subtreeRoot;
            }

            // Update height of current node
            UpdateHeight(arena, nodeIndex);

            // Balance this subtree - may rotate and return a new subtree root
            var newRoot = Balance(arena, nodeIndex);

            subtreeFrom = nodeIndex;
            subtreeRoot = newRoot;
        }

        // After processing all nodes up to the root, subtreeRoot is the new tree root
        // (which may have changed if rotations occurred at the top level)
        return subtreeRoot;
    }

    /// <summary>
    /// Iterative in-order traversal using explicit stack to avoid recursion overhead.
    /// </summary>
    /// <remarks>
    /// Uses an explicit stack to track node indices during traversal, avoiding recursion overhead.
    /// Uses ArrayPool to avoid GC pressure.
    /// Stack depth is bounded by the AVL tree height (O(log n)), not n.
    /// Reads cached values from tree nodes and writes them back to the original span.
    /// </remarks>
    private static void Inorder<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, Span<Node<T>> arena, int rootIndex, ref int writeIndex, int maxStackSize)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (rootIndex == NULL_INDEX) return;

        int[]? rented = null;
        Span<int> stack = maxStackSize <= 128
            ? stackalloc int[maxStackSize]
            : (rented = ArrayPool<int>.Shared.Rent(maxStackSize)).AsSpan(0, maxStackSize);
        try
        {
            var stackTop = 0;
            var currentIndex = rootIndex;

            // Iterative in-order traversal
            while (stackTop > 0 || currentIndex != NULL_INDEX)
            {
                // Traverse left subtree: push all left nodes onto stack
                while (currentIndex != NULL_INDEX)
                {
                    stack[stackTop++] = currentIndex;
                    currentIndex = arena[currentIndex].Left;
                }

                // Process the node at top of stack
                currentIndex = stack[--stackTop];
                // Read node value for visualization and write to array
                var value = ReadNodeValue(arena, currentIndex, s.Context);
                s.Write(writeIndex++, value);

                // Move to right subtree
                currentIndex = arena[currentIndex].Right;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    // Helper methods for node operations (encapsulates visualization tracking)

    /// <summary>
    /// Creates a new tree node in the arena and records its creation for visualization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateNode<T, TContext>(Span<Node<T>> arena, T value, ref int nodeCount, TContext context) where TContext : ISortContext
    {
        var nodeId = nodeCount++;
        arena[nodeId] = new Node<T>(value);
        // Record node creation in the tree buffer for visualization
        context.OnIndexWrite(nodeId, BUFFER_TREE, value);
        return nodeId;
    }

    /// <summary>
    /// Reads a node's value and records the access for visualization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ReadNodeValue<T, TContext>(Span<Node<T>> arena, int nodeIndex, TContext context) where TContext : ISortContext
    {
        // Visualize node access during traversal
        context.OnIndexRead(nodeIndex, BUFFER_TREE);
        return arena[nodeIndex].Value;
    }

    /// <summary>
    /// Compares a value with a node's value and records the comparison for statistics.
    /// Also records the node access for visualization.
    /// Returns: value.CompareTo(node.Value)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareWithNode<T, TComparer, TContext>(Span<Node<T>> arena, int nodeIndex, T value, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // Visualize node access during tree traversal
        context.OnIndexRead(nodeIndex, BUFFER_TREE);

        // Compare value with node's item
        // Note: This comparison is counted as a main array comparison (bufferId 0)
        // because the values originated from the main array
        var cmp = comparer.Compare(value, arena[nodeIndex].Value);
        context.OnCompare(-1, -1, cmp, 0, 0);

        return cmp;
    }

    // methods used to maintain AVL tree balance

    /// <summary>
    /// Update the node's height based on the heights of its children using arena indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateHeight<T>(Span<Node<T>> arena, int nodeIndex)
    {
        var leftIndex = arena[nodeIndex].Left;
        var rightIndex = arena[nodeIndex].Right;

        // Get the heights of the left and right children.
        int leftHeight = (leftIndex == NULL_INDEX) ? 0 : arena[leftIndex].Height;
        int rightHeight = (rightIndex == NULL_INDEX) ? 0 : arena[rightIndex].Height;

        // node's height = max child's height + 1
        arena[nodeIndex].Height = 1 + Math.Max(leftHeight, rightHeight);
    }

    /// <summary>
    /// Returns the balance factor (left height - right height) using arena indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBalance<T>(Span<Node<T>> arena, int nodeIndex)
    {
        var leftIndex = arena[nodeIndex].Left;
        var rightIndex = arena[nodeIndex].Right;

        int leftHeight = (leftIndex == NULL_INDEX) ? 0 : arena[leftIndex].Height;
        int rightHeight = (rightIndex == NULL_INDEX) ? 0 : arena[rightIndex].Height;
        return leftHeight - rightHeight;
    }

    /// <summary>
    /// Rebalance the node after insertion using AVL rotations (arena-based).
    /// </summary>
    /// <returns>Index of the new root after balancing.</returns>
    private static int Balance<T>(Span<Node<T>> arena, int nodeIndex)
    {
        int balance = GetBalance(arena, nodeIndex);

        // Left heavy (balance > 1)
        if (balance > 1)
        {
            var leftIndex = arena[nodeIndex].Left;
            // Left child is right heavy
            if (GetBalance(arena, leftIndex) < 0)
            {
                arena[nodeIndex].Left = RotateLeft(arena, leftIndex);
            }
            return RotateRight(arena, nodeIndex);
        }
        // Right heavy (balance < -1)
        else if (balance < -1)
        {
            var rightIndex = arena[nodeIndex].Right;
            // Right child is left heavy
            if (GetBalance(arena, rightIndex) > 0)
            {
                arena[nodeIndex].Right = RotateRight(arena, rightIndex);
            }
            return RotateLeft(arena, nodeIndex);
        }

        // Node is balanced, return as is.
        return nodeIndex;
    }

    /// <summary>
    /// Right rotation on the given node using arena indices.
    /// </summary>
    /// <returns>Index of the new root after rotation.</returns>
    private static int RotateRight<T>(Span<Node<T>> arena, int yIndex)
    {
        var xIndex = arena[yIndex].Left;
        var t2Index = arena[xIndex].Right;

        // Perform the rotation
        arena[xIndex].Right = yIndex;
        arena[yIndex].Left = t2Index;

        // Recompute heights
        UpdateHeight(arena, yIndex);
        UpdateHeight(arena, xIndex);

        // Return the new root
        return xIndex;
    }

    /// <summary>
    /// Left rotation on the given node using arena indices.
    /// </summary>
    /// <returns>Index of the new root after rotation.</returns>
    private static int RotateLeft<T>(Span<Node<T>> arena, int xIndex)
    {
        var yIndex = arena[xIndex].Right;
        var t2Index = arena[yIndex].Left;

        // Perform the rotation
        arena[yIndex].Left = xIndex;
        arena[xIndex].Right = t2Index;

        // Recompute heights
        UpdateHeight(arena, xIndex);
        UpdateHeight(arena, yIndex);

        // Return the new root
        return yIndex;
    }

    /// <summary>
    /// Arena-based node structure with value caching for optimal performance.
    /// </summary>
    /// <remarks>
    /// Struct-based to eliminate GC pressure (allocated via ArrayPool).
    /// Left and Right are indices into the arena array (-1 represents null).
    /// Value caches the T instance directly to avoid span indirection on every comparison.
    /// Height is maintained for AVL balancing.
    /// The node's identity is its position in the arena array, so no separate Id field is needed.
    /// </remarks>
    private struct Node<T>
    {
        public T Value;         // Cached value for direct comparison (avoids span indirection)
        public int Left;        // Index in arena, -1 = null
        public int Right;       // Index in arena, -1 = null
        public int Height;      // For AVL balancing

        public Node(T value)
        {
            Value = value;
            Left = -1;
            Right = -1;
            Height = 1;  // A new node starts with height = 1
        }
    }
}

/// <summary>
/// 平衡二分木（AVL木）を用いた二分木ソート。挿入時に回転を行って常に高さが O(log n) に保たれ、木を中順巡回することで配列に要素を昇順で再割り当てします。
/// 二分木ソートに比べて、平均および最悪ケースでも O(n log n) のソートが保証されます。
/// <br/>
/// Balanced binary tree sort using AVL tree. Rotations are performed during insertion to maintain a height of O(log n), and an in-order traversal reassigns array elements in ascending order.
/// Compared to binary tree sort, it guarantees O(n log n) sorting in both average and worst cases.
/// </summary>
/// <remarks>
/// <para><strong>Theoretical Conditions for Correct AVL Tree Sort:</strong></para>
/// <list type="number">
/// <item><description><strong>Binary Search Tree Property:</strong> For every node, all values in the left subtree must be less than the node's value,
/// and all values in the right subtree must be greater than or equal to the node's value.
/// This property is maintained by comparing values during insertion.</description></item>
/// <item><description><strong>AVL Balance Property:</strong> For every node, the height difference between left and right subtrees (balance factor) must be at most 1.
/// Balance factor = height(left subtree) - height(right subtree) ∈ {-1, 0, 1}.
/// This is enforced by the Balance() method after every insertion.</description></item>
/// <item><description><strong>Height Maintenance:</strong> Each node stores its height, which is updated after any structural change.
/// Height = 1 + max(height(left), height(right)).
/// This is computed by UpdateHeight() after insertions and rotations.</description></item>
/// <item><description><strong>Rotation Correctness:</strong> When the balance factor violates the AVL property (|balance| > 1), rotations restore balance while preserving BST property:
/// <list type="bullet">
/// <item><description>Left-Left case (balance > 1, left child balanced): Single right rotation</description></item>
/// <item><description>Left-Right case (balance > 1, left child right-heavy): Left rotation on left child, then right rotation</description></item>
/// <item><description>Right-Right case (balance &lt; -1, right child balanced): Single left rotation</description></item>
/// <item><description>Right-Left case (balance &lt; -1, right child left-heavy): Right rotation on right child, then left rotation</description></item>
/// </list>
/// All rotations preserve the in-order traversal sequence.</description></item>
/// <item><description><strong>In-order Traversal Correctness:</strong> Visiting nodes in left-root-right order produces elements in sorted ascending order.
/// This is guaranteed by the BST property and implemented recursively.</description></item>
/// </list>
/// <para><strong>Mathematical Proof of O(log n) Height:</strong></para>
/// <list type="bullet">
/// <item><description>Let N(h) = minimum number of nodes in an AVL tree of height h</description></item>
/// <item><description>N(h) = N(h-1) + N(h-2) + 1 (Fibonacci-like recurrence)</description></item>
/// <item><description>N(h) ≥ F(h+2) - 1, where F is the Fibonacci sequence</description></item>
/// <item><description>Therefore, h ≤ 1.44 × log₂(n + 2) - 0.328 ≈ O(log n)</description></item>
/// </list>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><description>Family      : Tree</description></item>
/// <item><description>Stable      : No (does not preserve relative order of equal elements)</description></item>
/// <item><description>In-place    : No (requires O(n) auxiliary space for tree structure)</description></item>
/// <item><description>Best case   : Θ(n log n) - Already sorted or reversed, still requires building balanced tree</description></item>
/// <item><description>Average case: Θ(n log n) - Each of n insertions takes O(log n) comparisons</description></item>
/// <item><description>Worst case  : O(n log n) - Guaranteed by AVL balancing property</description></item>
/// <item><description>Comparisons : O(n log n) - Each insertion performs at most log₂(n) comparisons</description></item>
/// <item><description>Index Reads : Θ(n) - Each element is read once during insertion</description></item>
/// <item><description>Index Writes: Θ(n) - Each element is written once during in-order traversal</description></item>
/// <item><description>Swaps       : 0 - No swaps performed; only tree node manipulations</description></item>
/// <item><description>Rotations   : O(n log n) worst case - At most 2 rotations per insertion (amortized O(1))</description></item>
/// </list>
/// <para><strong>Advantages over unbalanced BST:</strong></para>
/// <list type="bullet">
/// <item><description>Guaranteed O(n log n) time complexity even on sorted/reversed input (BST degrades to O(n²))</description></item>
/// <item><description>Predictable performance regardless of input distribution</description></item>
/// <item><description>Height always bounded by 1.44 × log₂(n)</description></item>
/// </list>
/// </remarks>
public static class BalancedBinaryTreeSortNonOptimized
{
    // Buffer identifiers for visualization
    private const int BUFFER_MAIN = 0;       // Main input array

    /// <summary>
    /// Sorts the elements in the specified span in ascending order using the default comparer.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="span">The span of elements to sort in place.</param>
    public static void Sort<T>(Span<T> span) where T : IComparable<T>
        => Sort(span, new ComparableComparer<T>(), NullContext.Default);

    /// <summary>
    /// Sorts the elements in the specified span using the provided sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TContext>(Span<T> span, TContext context)
    where T : IComparable<T>
    where TContext : ISortContext
        => Sort(span, new ComparableComparer<T>(), context);

    /// <summary>
    /// Sorts the elements in the specified span using the provided comparer and sort context.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TComparer">The type of comparer to use for element comparisons.</typeparam>
    /// <typeparam name="TContext">The type of context for tracking operations.</typeparam>
    /// <param name="span">The span of elements to sort. The elements within this span will be reordered in place.</param>
    /// <param name="comparer">The comparer to use for element comparisons.</param>
    /// <param name="context">The sort context that defines the sorting strategy or options to use during the operation. Cannot be null.</param>
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (span.Length <= 1) return;

        var s = new SortSpan<T, TComparer, TContext>(span, context, comparer, BUFFER_MAIN);

        Node<T>? root = null;

        // Insert each element in the array into the AVL tree.
        for (int i = 0; i < s.Length; i++)
        {
            var value = s.Read(i);
            // root = InsertIterative(root, value, s);
            root = InsertRecursive(root, value, s);
        }

        // Traverse in order and write back into the array.
        int n = 0;
        Inorder(s, root, ref n);
    }

    /// <summary>
    /// Insert a value into the AVL tree iteratively and rebalance if necessary.
    /// </summary>
    private static Node<T> InsertIterative<T, TComparer, TContext>(Node<T>? root, T value, SortSpan<T, TComparer, TContext> s)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // If the tree is empty, just create a new node.
        if (root is null)
        {
            return new Node<T>(value);
        }

        // Track the path: (node, wentLeft) where wentLeft indicates which child we followed
        var path = new Stack<(Node<T> node, bool wentLeft)>();
        Node<T> current = root;

        // Navigate to insertion point
        while (true)
        {
            bool goLeft = s.Compare(value, current.Item) < 0;

            if (goLeft)
            {
                if (current.Left is null)
                {
                    current.Left = new Node<T>(value);
                    break;
                }
                path.Push((current, true));
                current = current.Left;
            }
            else
            {
                if (current.Right is null)
                {
                    current.Right = new Node<T>(value);
                    break;
                }
                path.Push((current, false));
                current = current.Right;
            }
        }

        // Start rebalancing from the parent of the inserted node
        UpdateHeight(current);
        var balanced = Balance(current);

        // Rebalance upward along the insertion path
        while (path.Count > 0)
        {
            var (parent, wentLeft) = path.Pop();

            // Connect the balanced child to its parent
            if (wentLeft)
                parent.Left = balanced;
            else
                parent.Right = balanced;

            // Update and balance the parent
            UpdateHeight(parent);
            balanced = Balance(parent);
        }

        // Return the new root (which might have changed due to rotations)
        return balanced;
    }

    /// <summary>
    /// Insert into the AVL tree, then rebalance.
    /// </summary>
    private static Node<T> InsertRecursive<T, TComparer, TContext>(Node<T>? node, T value, SortSpan<T, TComparer, TContext> s)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        // If the tree is empty, return a new node.
        if (node is null)
        {
            return new Node<T>(value);
        }

        // Find the correct position to insert.
        if (s.Compare(value, node.Item) < 0)
        {
            node.Left = InsertRecursive(node.Left, value, s);
        }
        else
        {
            node.Right = InsertRecursive(node.Right, value, s);
        }

        // Update the height and rebalance if necessary.
        UpdateHeight(node);
        return Balance(node);
    }

    /// <summary>
    /// Traverse the tree in order to collect elements in ascending order.
    /// </summary>
    private static void Inorder<T, TComparer, TContext>(SortSpan<T, TComparer, TContext> s, Node<T>? node, ref int index)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        if (node is null) return;

        Inorder(s, node.Left, ref index);
        s.Write(index++, node.Item);
        Inorder(s, node.Right, ref index);
    }

    // methods used to maintain AVL tree balance

    /// <summary>
    /// Update the node's height based on the heights of its children.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateHeight<T>(Node<T> node)
    {
        // Get the heights of the left and right children.
        int leftHeight = (node.Left is null) ? 0 : node.Left.Height;
        int rightHeight = (node.Right is null) ? 0 : node.Right.Height;

        // node's height = max child's height + 1
        node.Height = 1 + Math.Max(leftHeight, rightHeight);
    }

    /// <summary>
    /// Returns the balance factor (left height - right height).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBalance<T>(Node<T> node)
    {
        int leftHeight = (node.Left is null) ? 0 : node.Left.Height;
        int rightHeight = (node.Right is null) ? 0 : node.Right.Height;
        return leftHeight - rightHeight;
    }

    /// <summary>
    /// Rebalance the node after insertion using AVL rotations.
    /// </summary>
    private static Node<T> Balance<T>(Node<T> node)
    {
        int balance = GetBalance(node);

        // Left heavy (balance > 1)
        if (balance > 1)
        {
            // Left child is right heavy
            if (GetBalance(node.Left!) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }
            return RotateRight(node);
        }
        // Right heavy (balance < -1)
        else if (balance < -1)
        {
            // Right child is left heavy
            if (GetBalance(node.Right!) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }
            return RotateLeft(node);
        }

        // Node is balanced, return as is.
        return node;
    }

    /// <summary>
    /// Right rotation on the given node.
    /// </summary>
    private static Node<T> RotateRight<T>(Node<T> y)
    {
        Node<T> x = y.Left!;
        Node<T>? t2 = x.Right;

        // Perform the rotation
        x.Right = y;
        y.Left = t2;

        // Recompute heights
        UpdateHeight(y);
        UpdateHeight(x);

        // Return the new root
        return x;
    }

    /// <summary>
    /// Left rotation on the given node.
    /// </summary>
    private static Node<T> RotateLeft<T>(Node<T> x)
    {
        Node<T> y = x.Right!;
        Node<T>? t2 = y.Left;

        // Perform the rotation
        y.Left = x;
        x.Right = t2;

        // Recompute heights
        UpdateHeight(x);
        UpdateHeight(y);

        // Return the new root
        return y;
    }

    private class Node<T>
    {
        public T Item;
        public Node<T>? Left;
        public Node<T>? Right;
        // For tracking the height in the AVL tree
        public int Height;

        public Node(T value)
        {
            Item = value;
            // A new node starts with height = 1
            Height = 1;
        }
    }
}

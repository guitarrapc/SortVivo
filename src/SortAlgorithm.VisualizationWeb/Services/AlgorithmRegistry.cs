using SortAlgorithm.VisualizationWeb.Models;
using SortAlgorithm.Contexts;
using SortAlgorithm.Algorithms;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// 全ソートアルゴリズムのメタデータを管理するレジストリ
/// </summary>
public class AlgorithmRegistry
{
    private readonly List<AlgorithmMetadata> _algorithms = [];

    public AlgorithmRegistry()
    {
        RegisterAlgorithms();
    }

    public IReadOnlyList<AlgorithmMetadata> GetAllAlgorithms() => _algorithms.AsReadOnly();

    public IEnumerable<AlgorithmMetadata> GetByCategory(string category)
        => _algorithms.Where(a => a.Category == category);

    public IEnumerable<string> GetCategories()
        => _algorithms.Select(a => a.Category).Distinct().OrderBy(c => c);

    private void RegisterAlgorithms()
    {
        // 最大サイズは全て4096、推奨サイズは計算量に応じて設定
        const int MAX_SIZE = 4096;

        // Exchange Sorts - O(n²) - 推奨256
        Add("Bubble sort", "Exchange Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => BubbleSort.Sort(arr, ctx),
            tutorialDescription: "Repeatedly compares adjacent pairs and swaps them if they are in the wrong order. Each pass bubbles the current largest unsorted value to its correct position at the end, so the sorted region grows by one element per pass.");
        Add("Cocktail shaker sort", "Exchange Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => CocktailShakerSort.Sort(arr, ctx),
            tutorialDescription: "A bidirectional variant of Bubble sort. It alternates between a left-to-right pass (moving the largest unsorted value to the right end) and a right-to-left pass (moving the smallest unsorted value to the left end), shrinking the unsorted region from both sides each round.");
        Add("Odd-even sort", "Exchange Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => OddEvenSort.Sort(arr, ctx),
            tutorialDescription: "Alternates between two phases each pass: the odd phase compares and swaps pairs at odd indices (0-1, 2-3, …) and the even phase compares pairs at even indices (1-2, 3-4, …). Repeating these two phases until no swaps occur guarantees a fully sorted array.");
        Add("Comb sort", "Exchange Sorts", "O(n²)", MAX_SIZE, 512, (arr, ctx) => CombSort.Sort(arr, ctx),
            tutorialDescription: "An improvement over Bubble sort that eliminates small values near the end of the array (\"turtles\") early. It starts by comparing elements far apart using a large gap, then shrinks the gap by a factor of roughly 1.3 each pass until the gap reaches 1, at which point it behaves like Bubble sort to finish.");

        // Selection Sorts - O(n²) - 推奨256
        Add("Selection sort", "Selection Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => SelectionSort.Sort(arr, ctx),
            tutorialDescription: "Scans the unsorted region to find its minimum value, then swaps that minimum with the first unsorted element. Each pass performs at most one swap, making it the algorithm with the fewest total swaps among simple O(n²) sorts.");
        Add("Double selection sort", "Selection Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => DoubleSelectionSort.Sort(arr, ctx),
            tutorialDescription: "An optimisation of Selection sort that finds both the minimum and the maximum of the unsorted region in a single scan, placing the minimum at the left end and the maximum at the right end simultaneously. This halves the number of passes needed compared to standard Selection sort.");
        Add("Cycle sort", "Selection Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => CycleSort.Sort(arr, ctx),
            tutorialDescription: "Decomposes the permutation into cycles and rotates each cycle to place every element directly into its final position. It performs the theoretical minimum number of writes to the array, making it ideal when memory writes are far more expensive than reads.");
        Add("Pancake sort", "Selection Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => PancakeSort.Sort(arr, ctx),
            tutorialDescription: "Sorts using only prefix reversals — like flipping a stack of pancakes with a spatula. Each pass finds the largest unsorted element, flips it to the front, then flips the entire unsorted region to move it into its final position at the end.");

        // Insertion Sorts - O(n²) ~ O(n log n) - 推奨256-2048
        Add("Insertion sort", "Insertion Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => InsertionSort.Sort(arr, ctx),
            tutorialDescription: "Takes each element from the unsorted region and shifts it leftward through the sorted region until it reaches its correct position, like sorting playing cards in hand. It runs in O(n) time on nearly-sorted data, which is why it serves as the finishing step in many advanced algorithms such as Timsort and Introsort.");
        Add("Pair insertion sort", "Insertion Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => PairInsertionSort.Sort(arr, ctx),
            tutorialDescription: "Picks two elements at a time — a smaller and a larger — and inserts both into the sorted region in a single backward scan. Because the smaller value terminates the scan earlier, the total number of comparisons is roughly halved compared to inserting each element independently.");
        Add("Binary insert sort", "Insertion Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => BinaryInsertionSort.Sort(arr, ctx),
            tutorialDescription: "Uses binary search instead of a linear scan to locate the correct insertion position in the sorted region, cutting comparisons from O(n) to O(log n) per element. The subsequent shift of elements to make room still costs O(n) per insert, so the overall complexity remains O(n²), but comparison count is significantly reduced.");
        Add("Library sort", "Insertion Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => LibrarySort.Sort(arr, ctx),
            tutorialDescription: "Also known as Gapped Insertion sort, it intentionally leaves empty gaps between sorted elements so future insertions require fewer shifts. Elements are placed with binary search, and gaps are periodically redistributed across the array. This rebalancing step enables O(n log n) expected time while retaining the simplicity of insertion-based logic.");
        Add("Shell sort (Knuth 1973)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE, 1024, (arr, ctx) => ShellSortKnuth1973.Sort(arr, ctx),
            tutorialDescription: "A generalisation of Insertion sort that first sorts elements spaced far apart, then progressively shrinks the gap to 1. Early passes with large gaps move elements close to their final positions quickly, so the last gap-1 pass has very little remaining work. This variant uses Knuth's 1973 sequence 1, 4, 13, 40, 121, … defined by h = 3h + 1.");
        Add("Shell sort (Sedgewick 1986)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE, 1024, (arr, ctx) => ShellSortSedgewick1986.Sort(arr, ctx),
            tutorialDescription: "A generalisation of Insertion sort that first sorts elements spaced far apart, then progressively shrinks the gap to 1. Early passes with large gaps move elements close to their final positions quickly, so the last gap-1 pass has very little remaining work. This variant uses Sedgewick's 1986 interleaved sequence 1, 5, 19, 41, 109, … formed by merging two geometric series, achieving O(n^4/3) worst-case complexity.");
        Add("Shell sort (Tokuda 1992)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE, 1024, (arr, ctx) => ShellSortTokuda1992.Sort(arr, ctx),
            tutorialDescription: "A generalisation of Insertion sort that first sorts elements spaced far apart, then progressively shrinks the gap to 1. Early passes with large gaps move elements close to their final positions quickly, so the last gap-1 pass has very little remaining work. This variant uses Tokuda's 1992 sequence 1, 4, 9, 20, 46, 103, … derived from the formula ceil((9(9/4)^k − 4) / 5), which performs consistently well in practice.");
        Add("Shell sort (Ciura 2001)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE, 1024, (arr, ctx) => ShellSortCiura2001.Sort(arr, ctx),
            tutorialDescription: "A generalisation of Insertion sort that first sorts elements spaced far apart, then progressively shrinks the gap to 1. Early passes with large gaps move elements close to their final positions quickly, so the last gap-1 pass has very little remaining work. This variant uses Ciura's empirically optimised sequence 1, 4, 10, 23, 57, 132, 301, 701, … discovered in 2001 and still regarded as one of the best known gap sequences.");
        Add("Shell sort (Lee 2021)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE, 1024, (arr, ctx) => ShellSortLee2021.Sort(arr, ctx),
            tutorialDescription: "A generalisation of Insertion sort that first sorts elements spaced far apart, then progressively shrinks the gap to 1. Early passes with large gaps move elements close to their final positions quickly, so the last gap-1 pass has very little remaining work. This variant uses Lee's 2021 sequence, a recently proposed empirically-tuned gap list designed to minimise the total comparison count across a wide range of array sizes.");
        Add("Gnome sort", "Insertion Sorts", "O(n²)", MAX_SIZE, 256, (arr, ctx) => GnomeSort.Sort(arr, ctx),
            tutorialDescription: "Moves through the array comparing adjacent pairs: it advances when a pair is already in order, or swaps them and steps back one position when they are not. This simple back-and-forth walk performs exactly the same element-shifting work as Insertion sort but requires no inner loop, making it one of the easiest sorting algorithms to implement from scratch.");

        // Merge Sorts - O(n log n) - 推奨2048
        Add("Merge sort", "Merge Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => MergeSort.Sort(arr, ctx),
            tutorialDescription: "Recursively splits the array in half until each sub-array holds a single element, then merges adjacent sub-arrays back together in sorted order. It is stable and guarantees O(n log n) in the worst case, but requires an auxiliary array of the same size as the input for the merge step.");
        Add("Bottom-up merge sort", "Merge Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => BottomupMergeSort.Sort(arr, ctx),
            tutorialDescription: "An iterative, non-recursive variant of Merge sort that treats every individual element as a sorted run of length 1, then repeatedly merges adjacent runs doubling the width each pass: 1 → 2 → 4 → 8 → … It delivers the same O(n log n) stability and worst-case guarantee as top-down Merge sort while eliminating the call-stack overhead of recursion.");
        Add("Rotate merge sort", "Merge Sorts", "O(n log² n)", MAX_SIZE, 1024, (arr, ctx) => RotateMergeSort.Sort(arr, ctx),
            tutorialDescription: "An in-place variant of Merge sort that merges two adjacent sorted runs by rotating blocks of elements rather than copying them to an auxiliary buffer. Rotation is costlier than a standard merge, raising the complexity to O(n log² n), but the algorithm requires no extra memory — making it valuable when auxiliary allocations must be avoided.");
        Add("Timsort", "Merge Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => TimSort.Sort(arr, ctx),
            tutorialDescription: "A hybrid of Merge sort and Insertion sort developed for CPython and later adopted by Java. It scans the input for naturally ordered runs, extends short runs with Insertion sort, and merges them using a stack-based strategy that exploits existing order to approach O(n) performance on partially sorted data.");
        Add("Powersort", "Merge Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => PowerSort.Sort(arr, ctx),
            tutorialDescription: "A natural merge sort that improves on Timsort's merge scheduling by computing a \"power\" value based on the sizes and positions of adjacent runs to determine the provably optimal merge order. This guarantees a comparison count close to the information-theoretic lower bound on inputs with existing structure, while matching Timsort on random data.");
        Add("ShiftSort", "Merge Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => ShiftSort.Sort(arr, ctx),
            tutorialDescription: "Detects both ascending and descending runs in the input, reversing any descending runs in-place to form ascending ones, then merges all runs using an adaptive strategy similar to Timsort. The symmetric run detection makes it especially effective on inputs that mix forward and backward sequences.");

        // Heap Sorts - O(n log n) - 推奨2048
        Add("Heapsort", "Heap Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => HeapSort.Sort(arr, ctx),
            tutorialDescription: "Transforms the array into a max-heap — a binary tree where every parent is larger than its children — so the largest element sits at index 0. It then repeatedly swaps that root with the last unsorted element, shrinks the heap boundary by one, and restores the heap property (sift-down), placing elements in sorted order with no extra memory required.");
        Add("Ternary heapsort", "Heap Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => TernaryHeapSort.Sort(arr, ctx),
            tutorialDescription: "A variant of Heapsort that uses a ternary heap — each node has up to three children instead of two. The shallower tree reduces the number of sift-down levels by a factor of log₂3 ≈ 1.58, which lowers the total number of comparisons during the extraction phase at the cost of comparing three children per node instead of two.");
        Add("Bottom-up heapSort", "Heap Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => BottomupHeapSort.Sort(arr, ctx),
            tutorialDescription: "Optimises the sift-down step of Heapsort by first descending to the leaf without any comparisons, then walking back up to find where the displaced root actually belongs. This halves the number of comparisons per extraction in the average case, making it faster than standard Heapsort in practice despite the same asymptotic complexity.");
        Add("Weak heapSort", "Heap Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => WeakHeapSort.Sort(arr, ctx),
            tutorialDescription: "Uses a weak heap — a relaxed binary tree where each node is only required to be larger than its right subtree, tracked with a single reverse-bit per node. This structure allows the merge of two weak heaps to cost exactly one comparison, reducing the total comparisons for n extractions to the theoretical minimum of n⌈log₂ n⌉.");
        Add("Smoothsort", "Heap Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => SmoothSort.Sort(arr, ctx),
            tutorialDescription: "An adaptive variant of Heapsort invented by Dijkstra that maintains a sequence of Leonardo heaps whose sizes follow the Leonardo number series. When the input is already sorted the algorithm degrades gracefully to O(n), unlike standard Heapsort which always runs in O(n log n) regardless of input order.");

        // Partition Sorts - O(n log n) - 推奨2048-4096
        Add("Quicksort", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => QuickSort.Sort(arr, ctx),
            tutorialDescription: "Selects a pivot element, partitions the array into elements ≤ pivot and elements > pivot, then recursively sorts each partition. With a good pivot choice the recursion depth stays at O(log n), giving O(n log n) average time, but a consistently poor pivot (e.g. always the smallest) degrades to O(n²).");
        Add("Quicksort (Median3)", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => QuickSortMedian3.Sort(arr, ctx),
            tutorialDescription: "Improves pivot selection by taking the median of the first, middle, and last elements instead of a fixed position. The median-of-three pivot is unlikely to be the extreme value, reducing the probability of the O(n²) worst case compared to naive Quicksort without significantly increasing per-partition cost.");
        Add("Quicksort (Median9)", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => QuickSortMedian9.Sort(arr, ctx),
            tutorialDescription: "Selects the pivot as the median of nine sampled elements — three medians each drawn from three equally spaced positions. The resulting pivot is a better approximation of the true median than median-of-three, producing more balanced partitions and fewer comparisons on large arrays at the cost of nine extra reads per partition.");
        Add("Quicksort (DualPivot)", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => QuickSortDualPivot.Sort(arr, ctx),
            tutorialDescription: "Uses two pivots, p1 ≤ p2, to divide the array into three partitions: elements < p1, elements between p1 and p2, and elements > p2. Sorting three regions instead of two reduces the average number of comparisons by roughly 5/9 compared to single-pivot Quicksort and is the algorithm behind Java's Arrays.sort for primitives.");
        Add("Quicksort (Stable)", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => StableQuickSort.Sort(arr, ctx),
            tutorialDescription: "A stable variant of Quicksort that preserves the relative order of equal elements. Stability requires an auxiliary buffer for the partition step, adding O(n) extra space. The trade-off makes it useful when sort keys are not unique and the original ordering of ties must be maintained.");
        Add("BlockQuickSort", "Partition Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => BlockQuickSort.Sort(arr, ctx),
            tutorialDescription: "Reorganises the partition step of Quicksort to process elements in small fixed-size blocks, separating the comparison phase from the swap phase. This pattern avoids branch mispredictions that slow down classic Quicksort on modern CPUs, yielding measurably faster wall-clock time even though the asymptotic complexity is unchanged.");
        Add("Introsort", "Partition Sorts", "O(n log n)", MAX_SIZE, 4096, (arr, ctx) => IntroSort.Sort(arr, ctx),
            tutorialDescription: "A hybrid that starts with Quicksort but monitors recursion depth; if depth exceeds 2⌊log₂ n⌋ it switches to Heapsort to guarantee O(n log n) worst-case, and it finishes small sub-arrays with Insertion sort. Combining the practical speed of Quicksort, the worst-case safety of Heapsort, and the low overhead of Insertion sort, Introsort is the basis for std::sort in most C++ standard libraries.");
        Add("IntrosortDotnet", "Partition Sorts", "O(n log n)", MAX_SIZE, 4096, (arr, ctx) => IntroSortDotnet.Sort(arr, ctx),
            tutorialDescription: "The Introsort variant used inside the .NET runtime (Array.Sort). It follows the same Quicksort → Heapsort → Insertion sort hybrid strategy as standard Introsort but incorporates .NET-specific tuning choices — such as the depth threshold and small-array cutoff — optimised for the managed runtime and its JIT compiler.");
        Add("Pattern-defeating quicksort", "Partition Sorts", "O(n log n)", MAX_SIZE, 4096, (arr, ctx) => PDQSort.Sort(arr, ctx),
            tutorialDescription: "An Introsort variant designed to detect and exploit common input patterns such as already-sorted, reverse-sorted, and many-equal-elements arrays. It adds a \"block partition\" like BlockQuickSort, pivot shuffling to break adversarial patterns, and a partial insertion sort for nearly-sorted data, making it the algorithm behind Rust's slice::sort_unstable.");
        Add("C++ std::sort", "Partition Sorts", "O(n log n)", MAX_SIZE, 4096, (arr, ctx) => StdSort.Sort(arr, ctx),
            tutorialDescription: "A faithful re-implementation of the Introsort algorithm as shipped in the GNU libstdc++ implementation of C++ std::sort. It uses median-of-three pivot selection, falls back to Heapsort after too many recursion levels, and switches to Insertion sort below a small-array threshold — the exact same hybrid strategy that has sorted data in C++ programs for decades.");

        // Adaptive Sorts - O(n log n) - 推奨2048
        Add("Drop-Merge sort", "Adaptive Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => DropMergeSort.Sort(arr, ctx),
            tutorialDescription: "Identifies the longest non-decreasing subsequence already present in the input, then collects the remaining out-of-order elements, sorts them separately, and merges the two sequences. On nearly-sorted data where only a small fraction k of elements are out of place it runs in O((k+1) log(k+1) + n) time, making it extremely fast when the input is close to sorted.");

        // Distribution Sorts - O(n) ~ O(nk) - 推奨4096
        Add("Counting sort", "Distribution Sorts", "O(n+k)", MAX_SIZE, 4096, (arr, ctx) => CountingSortInteger.Sort(arr, ctx),
            tutorialDescription: "Counts how many times each distinct value appears, then uses a running prefix-sum of those counts to calculate the exact output position for every element. It never compares values against each other, so it breaks the O(n log n) comparison-based lower bound, running in O(n + k) where k is the range of values.");
        Add("Pigeonhole sort", "Distribution Sorts", "O(n+k)", MAX_SIZE, 4096, (arr, ctx) => PigeonholeSortInteger.Sort(arr, ctx),
            tutorialDescription: "Allocates one \"pigeonhole\" bucket per possible value in the range, then drops each element into its corresponding bucket and finally reads the buckets back in order. Unlike Counting sort it physically moves elements into buckets rather than computing positions arithmetically, making it stable and conceptually the simplest distribution sort.");
        Add("Bucket sort", "Distribution Sorts", "O(n)", MAX_SIZE, 4096, (arr, ctx) => BucketSortInteger.Sort(arr, ctx),
            tutorialDescription: "Divides the value range into a fixed number of equal-width buckets, scatters each element into its bucket, sorts each bucket independently (typically with Insertion sort), then concatenates the buckets. When elements are uniformly distributed each bucket holds O(1) elements on average, making the overall expected time O(n).");
        Add("LSD Radix sort (b=4)", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => RadixLSD4Sort.Sort(arr, ctx),
            tutorialDescription: "Sorts integers digit by digit from the least significant to the most significant position using a stable counting sort at each pass. Using base 4 (2-bit digits) requires more passes than larger bases but keeps the per-pass counting table tiny, which can improve CPU cache utilisation on very large arrays.");
        Add("LSD Radix sort (b=10)", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => RadixLSD10Sort.Sort(arr, ctx),
            tutorialDescription: "Sorts integers digit by digit from the least significant to the most significant position using a stable counting sort at each pass. Base 10 is the most intuitive base to follow visually — each pass sorts by the ones, tens, hundreds, … digit — and is a natural choice for illustrating how LSD Radix sort works.",
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal);
        Add("LSD Radix sort (b=256)", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => RadixLSD256Sort.Sort(arr, ctx),
            tutorialDescription: "Sorts integers byte by byte from the least significant to the most significant byte using a stable counting sort at each pass. Base 256 (8-bit digits) needs only 4 passes for 32-bit integers and is the most common choice in high-performance implementations because each pass processes the maximum number of bits with a counting table that still fits comfortably in the L1 cache.",
            excludeFromTutorial: true);
        Add("MSD Radix sort (b=4)", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => RadixMSD4Sort.Sort(arr, ctx),
            tutorialDescription: "Sorts integers digit by digit from the most significant to the least significant position, recursively sorting each bucket before moving to the next digit. Starting from the most significant digit means sub-buckets that already differ at a higher digit need no further work, allowing early termination and giving it adaptive behaviour similar to a comparison sort.");
        Add("MSD Radix sort (b=10)", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => RadixMSD10Sort.Sort(arr, ctx),
            tutorialDescription: "A base-10 variant of MSD Radix sort that partitions by the leading decimal digit first, then recurses into each partition for the next digit. The decimal base makes the recursive bucketing intuitive to trace — elements are grouped first by hundreds, then tens, then ones — closely resembling how a human would sort numbered cards by hand.",
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal);
        Add("American flag sort", "Distribution Sorts", "O(nk)", MAX_SIZE, 4096, (arr, ctx) => AmericanFlagSort.Sort(arr, ctx),
            tutorialDescription: "An in-place variant of MSD Radix sort named by analogy with the Dutch national flag problem. It makes two passes per digit: the first counts elements to determine bucket boundaries, and the second cyclically permutes elements into their correct buckets without any auxiliary array, achieving MSD radix sorting with O(1) extra space.");

        // Network Sorts - O(log²n) - 推奨2048
        Add("Bitonic sort", "Network Sorts", "O(log²n)", MAX_SIZE, 2048, (arr, ctx) => BitonicSort.Sort(arr, ctx),
            tutorialDescription: "A sorting network that first builds a bitonic sequence — a sequence that first rises then falls — by repeatedly merging pairs of sub-sequences in alternating directions. It then applies a bitonic merge to convert the full sequence into sorted order. Every comparison-and-swap is data-independent, making it well-suited for parallel hardware such as GPUs.");
        Add("Bitonic sort (Recursive)", "Network Sorts", "O(log²n)", MAX_SIZE, 1024, (arr, ctx) => BitonicSortNonOptimized.Sort(arr, ctx),
            tutorialDescription: "A straightforward recursive implementation of Bitonic sort that expresses the build-then-merge structure directly as recursive calls. It is easier to read and verify than the iterative version but carries additional call-stack overhead, making it slower in practice while producing identical comparison-and-swap sequences on power-of-two sized arrays.");

        // Tree Sorts - O(n log n) - 推奨1024
        Add("Unbalanced binary tree sort", "Tree Sorts", "O(n log n)", MAX_SIZE, 1024, (arr, ctx) => BinaryTreeSort.Sort(arr, ctx),
            tutorialDescription: "Inserts every element into a plain binary search tree — smaller values go left, larger values go right — then recovers the sorted output with an in-order traversal. The average case is O(n log n), but an already-sorted input produces a degenerate tree of height n, degrading to O(n²) both in time and in stack depth.");
        Add("Balanced binary tree sort", "Tree Sorts", "O(n log n)", MAX_SIZE, 2048, (arr, ctx) => BalancedBinaryTreeSort.Sort(arr, ctx),
            tutorialDescription: "Inserts elements into a self-balancing binary search tree (such as a red-black tree or AVL tree) that automatically keeps its height at O(log n), then recovers sorted output with an in-order traversal. The balancing guarantee ensures O(n log n) worst-case even for sorted or reverse-sorted input, unlike a plain binary search tree.");

        // Joke Sorts - O(n!) ~ O(∞) - 推奨8（注意: 極めて遅い）
        // Bogo sort: ランダムシャッフルで非決定的のためチュートリアル対象外。
        // Slow / Stooge: 決定的な再帰アルゴリズムのため、4要素に絞ってチュートリアル可能。
        Add("Bogo sort", "Joke Sorts", "O(n!)", 8, 8, (arr, ctx) => BogoSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: "Repeatedly checks whether the array is sorted, and if not, shuffles it completely at random and tries again. With n elements there are n! possible orderings, so the expected number of shuffles before hitting the sorted one is n!, giving an expected time of O(n · n!). It has no practical use and exists purely as a humorous illustration of what \"not\" to do.",
            excludeFromTutorial: true);
        Add("Slow sort", "Joke Sorts", "O(n^(log n))", MAX_SIZE, 16, (arr, ctx) => SlowSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: "A deliberately inefficient recursive algorithm based on the tongue-in-cheek principle of \"multiply and surrender\". It finds the maximum of a subarray by recursively sorting both halves and comparing their last elements, then moves that maximum to the end and recurses on the remainder — doing far more work than necessary at every step, resulting in super-polynomial O(n^(log n)) time.",
            tutorialArrayType: TutorialArrayType.FourElement);
        Add("Stooge sort", "Joke Sorts", "O(n^2.7)", MAX_SIZE, 16, (arr, ctx) => StoogeSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: "Recursively sorts the first two-thirds of the array, then the last two-thirds, then the first two-thirds again. The triple-recursive structure is correct but wildly wasteful: the recurrence T(n) = 3T(2n/3) solves to O(n^2.71), worse than any practical sorting algorithm and introduced in CLRS as a cautionary example of how a correct algorithm can still be egregiously inefficient.",
            tutorialArrayType: TutorialArrayType.FourElement);
    }

    private void Add(string name, string category, string complexity, int maxElements, int recommendedSize,
        Action<Span<int>, ISortContext> sortAction, string description = "", string tutorialDescription = "",
        TutorialArrayType tutorialArrayType = TutorialArrayType.Default, bool excludeFromTutorial = false)
    {
        _algorithms.Add(new AlgorithmMetadata
        {
            Name = name,
            Category = category,
            TimeComplexity = complexity,
            MaxElements = maxElements,
            RecommendedSize = recommendedSize,
            SortAction = sortAction,
            Description = description,
            TutorialDescription = tutorialDescription,
            TutorialArrayType = tutorialArrayType,
            ExcludeFromTutorial = excludeFromTutorial
        });
    }
}

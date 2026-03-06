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
        // 最大サイズは全て16384、推奨サイズは計算量に応じて設定
        const int MAX_SIZE_N2 = 2048;
        const int MAX_SIZE_NLOGN15 = 8192;
        const int MAX_SIZE_NLOGN = 8192;
        const int MAX_SIZE_JOKE = 16;
        const int MAX_SIZE_JOKE_BOGO = 8;

        // Exchange Sorts - O(n²) - 推奨256
        Add("Bubble sort", "Exchange Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => BubbleSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Repeatedly compares adjacent pairs left-to-right and swaps them if they are in the wrong order, so each pass carries the largest unsorted value step by step to its correct position at the end.

                Key property: The simplest O(n²) sort with no lookahead or memory — its very simplicity means it performs more redundant comparisons than any other straightforward sort.

                Watch for:
                - Compare: each adjacent pair is tested in sequence; when already in order the scan advances without any swap
                - Swap: fires only when left > right, nudging the larger value exactly one step rightward
                - End of pass: the rightmost unsorted element settles into its final position, shrinking the active region by one
                """);
        Add("Cocktail shaker sort", "Exchange Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => CocktailShakerSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Alternates between a left-to-right pass that carries the largest unsorted value to the right end, and a right-to-left pass that carries the smallest unsorted value to the left end.

                Key property: A bidirectional extension of Bubble sort that shrinks the unsorted region from both sides each round, neutralising the "turtle" problem where small values near the end of the array slow the forward-only variant.

                Watch for:
                - Compare: direction reverses each pass — left-to-right on the forward pass, right-to-left on the backward pass
                - Swap: on the forward pass fires when left > right (pushes larger values rightward); on the backward pass fires when left > right in reverse (pushes smaller values leftward)
                - End of round: both the right and left boundaries close in by one, shrinking the unsorted region from both ends simultaneously
                """);
        Add("Odd-even sort", "Exchange Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => OddEvenSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Alternates between an odd phase that compares pairs at positions (0,1), (2,3), … and an even phase that compares pairs at positions (1,2), (3,4), …, repeating until a full round completes with no swaps.

                Key property: Within each phase all pair comparisons are independent of one another, making it directly parallelisable — each phase can be executed in a single parallel step on hardware with enough processors.

                Watch for:
                - Compare: odd phase tests every other pair starting at index 0; even phase shifts the pattern by one and tests the interleaved pairs
                - Swap: fires independently at any pair where left > right; multiple swaps can occur in the same phase without interfering with each other
                - End of round: one odd phase and one even phase together form one round; rounds repeat until no swap fires in either phase
                """);
        Add("Comb sort", "Exchange Sorts", "O(n²)", MAX_SIZE_N2, 512, (arr, ctx) => CombSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Compares elements separated by a large gap and swaps them if out of order, then shrinks the gap by a factor of ~1.3 each pass until it reaches 1, at which point the remaining passes behave identically to Bubble sort.

                Key property: The large initial gap moves "turtles" — small values stranded near the end of the array — to their correct region far more quickly than Bubble sort's adjacent-only swaps, dramatically reducing total work.

                Watch for:
                - Compare: highlighted pairs are separated by the current gap rather than being adjacent; the gap distance is clearly visible in the animation
                - Swap: fires when element at position i > element at position i+gap, moving the larger value rightward by a full gap width in one step
                - End of pass: watch the gap value shrink each pass until it reaches 1, after which the algorithm completes with standard Bubble sort passes
                """);

        // Selection Sorts - O(n²) - 推奨256
        Add("Selection sort", "Selection Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => SelectionSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the entire unsorted region to find the minimum element, then swaps it with the first unsorted element, repeating until the array is sorted.

                Key property: At most one swap per pass — the theoretical minimum among O(n²) sorts — making it the best choice when memory writes are far more expensive than reads.

                Watch for:
                - Compare: the scan pointer advances right through the unsorted region, silently updating the minimum candidate index without moving any elements
                - Swap: exactly one long-distance swap per pass, jumping the found minimum directly from its current position to the front of the unsorted region
                - End of pass: the left sorted boundary advances by exactly one element; total passes needed is n − 1
                """);
        Add("Double selection sort", "Selection Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => DoubleSelectionSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the unsorted region once to find both the minimum and the maximum simultaneously, then places the minimum at the left boundary and the maximum at the right boundary.

                Key property: Settles two elements per pass instead of one, halving the number of passes compared to Selection sort while still performing at most two swaps per pass.

                Watch for:
                - Compare: a single scan simultaneously tracks two candidates — the current minimum and the current maximum — updating both indices as it moves
                - Swap: up to two swaps per pass, one placing the minimum at the left boundary and one placing the maximum at the right boundary
                - End of pass: both the left and right sorted boundaries advance by one, shrinking the unsorted region from both ends at once
                """);
        Add("Cycle sort", "Selection Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => CycleSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Decomposes the array's permutation into cycles, then rotates each cycle in-place so that every element travels directly to its correct final position.

                Key property: Performs the absolute theoretical minimum number of writes to the array — each element is written at most once — making it uniquely suited to storage media where write operations are costly or have limited endurance.

                Watch for:
                - Compare: counts how many elements in the unsorted region are smaller than the current item to calculate its exact destination index
                - IndexWrite: each element is written directly to its final position; no element is ever moved to a temporary location unnecessarily
                - IndexRead: when an element already occupies the destination, it is picked up and rerouted to its own destination, continuing the cycle until it wraps back to the start
                """);
        Add("Pancake sort", "Selection Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => PancakeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Finds the largest unsorted element, flips the prefix up to that element to bring it to position 0, then flips the entire unsorted prefix to carry it to its final position at the end.

                Key property: Uses only prefix reversals as the sole sorting primitive — no arbitrary swaps — which models systems where the only permitted operation is reversing a contiguous prefix of the sequence.

                Watch for:
                - Compare: scans the unsorted region to locate the index of the largest remaining element
                - Swap: each reversal is a sequence of symmetric swaps around the midpoint of the flipped prefix; two separate reversals fire per pass
                - End of pass: two flips per pass settle exactly one element; the right sorted boundary grows by one
                """);

        // Insertion Sorts - O(n²) ~ O(n log n) - 推奨256-2048
        Add("Insertion sort", "Insertion Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => InsertionSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Picks up each element from the unsorted boundary and shifts it leftward through the sorted region one position at a time until it lands in its correct position.

                Key property: Degrades to O(n) on nearly-sorted input because very few shifts are needed per element, which is why it is used as the finishing step in advanced hybrids such as Timsort and Introsort once sub-arrays are small.

                Watch for:
                - IndexRead: the element at the unsorted boundary is picked up, leaving a vacancy
                - Compare: the picked-up element is tested against each sorted element from right to left; the scan stops as soon as a smaller-or-equal element is found
                - IndexWrite: each losing sorted element shifts one step right to extend the vacancy; the picked-up element drops into the final vacancy when the scan stops
                """);
        Add("Pair insertion sort", "Insertion Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => PairInsertionSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Picks up two elements at a time — a larger and a smaller — and inserts both into the sorted region in a single backward scan, placing the larger first and then continuing the scan to place the smaller.

                Key property: The smaller element always terminates the backward scan before or at the point where the larger stopped, so the combined scan is shorter than two independent insertions — roughly halving total comparisons.

                Watch for:
                - IndexRead: two elements are picked up from the unsorted boundary; the larger is queued for insertion first since it requires the longer scan
                - Compare: the backward scan finds the larger element's stopping point first; the smaller then continues from that point with a narrowed search range
                - IndexWrite: sorted elements shift right to open two adjacent vacancies; both picked-up elements drop into place in a single continuous backward pass
                """);
        Add("Binary insert sort", "Insertion Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => BinaryInsertionSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Uses binary search to locate the exact insertion position in the sorted region, then shifts all elements between that position and the unsorted boundary one step right to make room.

                Key property: Binary search cuts comparisons per insertion from O(n) to O(log n), but the rightward element shift still costs O(n), keeping overall complexity at O(n²) — comparisons are reduced but total work is not.

                Watch for:
                - IndexRead: the element to insert is picked up before the binary search begins
                - Compare: binary search jumps to the midpoint of the remaining sorted range each step; watch the left and right bounds converge toward a single insertion point
                - IndexWrite: once the position is found, all elements from that position to the gap shift one step right in a single continuous sweep; the element is then dropped into the freed slot
                """);
        Add("Gnome sort", "Insertion Sorts", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => GnomeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Moves a single pointer forward when the current adjacent pair is already in order, or swaps the pair and steps the pointer back one position when they are not, bouncing until the pointer reaches the end of the array.

                Key property: Performs exactly the same element-shifting work as Insertion sort — the back-and-forth pointer traces the same path as an insertion scan — but uses no inner loop, making it arguably the simplest correct sorting algorithm to implement from scratch.

                Watch for:
                - Compare: tests only the single pair at the current pointer position; the result alone determines whether the pointer advances or retreats
                - Swap: fires when left > right, then the pointer immediately steps back one position to re-check the newly formed adjacent pair
                - Forward advance: once the pointer recovers from a retreat and the current pair is in order, it resumes moving rightward; long ordered runs are traversed with no swaps at all
                """);
        Add("Library sort", "Insertion Sorts", "O(n log n)", MAX_SIZE_NLOGN15, 2048, (arr, ctx) => LibrarySort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Maintains intentional gaps between sorted elements so that insertions require only a short local shift; each new element is placed via binary search into a nearby gap, and the gaps are periodically redistributed evenly across the array.

                Key property: Gaps absorb insertion pressure locally, and periodic rebalancing keeps the average shift short enough to achieve O(n log n) expected time — faster than standard Insertion sort while retaining its insertion-based logic.

                Watch for:
                - Compare: binary search targets a gapped region rather than a tight sorted block, locating the gap nearest to the correct position
                - IndexWrite: the element drops into a nearby gap with only a short local shift, avoiding the long rightward sweeps that make standard Insertion sort slow
                - RangeCopy: during rebalancing, all current elements are redistributed across a freshly spaced-out array with new gaps re-inserted between them
                """);
        Add("Shell sort (Knuth 1973)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortKnuth1973.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Runs a series of Insertion sort passes over h-spaced subsequences of the array for a decreasing sequence of gap values h, finishing with h = 1 as a standard Insertion sort pass on a nearly-sorted array.

                Key property: Uses Knuth's 1973 sequence 1, 4, 13, 40, 121, … defined by h = 3h + 1 — one of the simplest analytically motivated sequences, giving O(n^1.5) practical performance with negligible precomputation.

                Watch for:
                - Compare: elements h positions apart are compared; with a large h, a badly misplaced element can leap close to its destination in a single step
                - IndexWrite: the current element shifts leftward through its h-spaced subsequence — skipping over the intervening positions — until it finds its insertion point
                - End of pass: h shrinks to the next sequence value; as h decreases the array grows progressively more ordered, leaving very little work for the final h = 1 pass
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);
        Add("Shell sort (Sedgewick 1986)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortSedgewick1986.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Runs a series of Insertion sort passes over h-spaced subsequences of the array for a decreasing sequence of gap values h, finishing with h = 1 as a standard Insertion sort pass on a nearly-sorted array.

                Key property: Uses Sedgewick's 1986 sequence 1, 5, 19, 41, 109, … formed by interleaving two geometric series, achieving a proven O(n^(4/3)) worst-case bound — a tighter theoretical guarantee than Knuth's sequence.

                Watch for:
                - Compare: elements h positions apart are compared; with a large h, a badly misplaced element can leap close to its destination in a single step
                - IndexWrite: the current element shifts leftward through its h-spaced subsequence — skipping over the intervening positions — until it finds its insertion point
                - End of pass: h shrinks to the next sequence value; as h decreases the array grows progressively more ordered, leaving very little work for the final h = 1 pass
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);
        Add("Shell sort (Tokuda 1992)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortTokuda1992.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Runs a series of Insertion sort passes over h-spaced subsequences of the array for a decreasing sequence of gap values h, finishing with h = 1 as a standard Insertion sort pass on a nearly-sorted array.

                Key property: Uses Tokuda's 1992 sequence 1, 4, 9, 20, 46, 103, … derived from the formula ceil((9(9/4)^k − 4) / 5), an empirically tuned sequence that consistently outperforms Knuth's and Sedgewick's on benchmarks across a wide range of input sizes.

                Watch for:
                - Compare: elements h positions apart are compared; with a large h, a badly misplaced element can leap close to its destination in a single step
                - IndexWrite: the current element shifts leftward through its h-spaced subsequence — skipping over the intervening positions — until it finds its insertion point
                - End of pass: h shrinks to the next sequence value; as h decreases the array grows progressively more ordered, leaving very little work for the final h = 1 pass
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);
        Add("Shell sort (Ciura 2001)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortCiura2001.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Runs a series of Insertion sort passes over h-spaced subsequences of the array for a decreasing sequence of gap values h, finishing with h = 1 as a standard Insertion sort pass on a nearly-sorted array.

                Key property: Uses Ciura's 2001 sequence 1, 4, 10, 23, 57, 132, 301, 701, … discovered empirically by minimising average comparison count on random arrays — widely regarded as the best-known gap sequence despite having no closed-form formula.

                Watch for:
                - Compare: elements h positions apart are compared; with a large h, a badly misplaced element can leap close to its destination in a single step
                - IndexWrite: the current element shifts leftward through its h-spaced subsequence — skipping over the intervening positions — until it finds its insertion point
                - End of pass: h shrinks to the next sequence value; as h decreases the array grows progressively more ordered, leaving very little work for the final h = 1 pass
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);
        Add("Shell sort (Lee 2021)", "Insertion Sorts", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortLee2021.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Runs a series of Insertion sort passes over h-spaced subsequences of the array for a decreasing sequence of gap values h, finishing with h = 1 as a standard Insertion sort pass on a nearly-sorted array.

                Key property: Uses Lee's 2021 sequence, a recently proposed empirically-tuned gap list that extends Ciura's search methodology over a larger space to minimise total comparison count across a broader range of array sizes than any previously published sequence.

                Watch for:
                - Compare: elements h positions apart are compared; with a large h, a badly misplaced element can leap close to its destination in a single step
                - IndexWrite: the current element shifts leftward through its h-spaced subsequence — skipping over the intervening positions — until it finds its insertion point
                - End of pass: h shrinks to the next sequence value; as h decreases the array grows progressively more ordered, leaving very little work for the final h = 1 pass
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);

        // Merge Sorts
        Add("Merge sort", "Merge Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => MergeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Recursively splits the array in half until each sub-array holds a single element, then merges adjacent sub-arrays back together in sorted order, building the fully sorted array bottom-up through the call stack.

                Key property: Stable and guarantees O(n log n) in all cases, but requires an auxiliary array of the same size as the input to perform each merge step — the classic space-vs-stability trade-off.

                Watch for:
                - RangeCopy: both halves of the current sub-array are copied into a temporary buffer before the merge begins
                - Compare: the merge reads from both buffer halves simultaneously, always picking the smaller front value to write back
                - IndexWrite: the chosen value is written back to the original array one position at a time until both buffer halves are exhausted
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Bottom-up merge sort", "Merge Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BottomupMergeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Treats every individual element as a sorted run of length 1, then iteratively merges adjacent runs and doubles the merge width each pass: 1 → 2 → 4 → 8 → … until a single sorted run spans the whole array.

                Key property: Delivers identical O(n log n) stability and worst-case guarantee to top-down Merge sort while being fully iterative — no recursion, no call-stack overhead, and no need to compute split points.

                Watch for:
                - RangeCopy: adjacent runs of the current width are copied into a buffer before each merge
                - Compare: the merge picks the smaller front value from the two buffer halves at each step
                - End of pass: the active run width doubles after each full sweep; watch the merge boundaries grow from 1 to 2 to 4 to … until they span the whole array
                """);
        Add("Rotate merge sort", "Merge Sorts", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => RotateMergeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Bottom-up variant of Rotate merge sort. Phase 1 seeds sorted runs by applying insertion sort to each fixed-size block. Phase 2 iteratively merges adjacent run pairs using in-place rotation, doubling the run width each pass until the whole array is sorted.

                Key property: Identical in-place guarantee and O(n log² n) complexity to the recursive variant, but with O(1) call-stack usage — no recursion overhead and no risk of stack overflow on very large arrays.

                Watch for:
                - Compare: phase 1 compares adjacent pairs within each block; phase 2 skip-checks whether s[mid] ≤ s[mid+1] before each merge, skipping it entirely when the boundary is already in order
                - Swap: rotation swaps fire only inside MergeInPlace when a block of right-run elements must be moved left past a block of left-run elements
                - End of pass: watch the merge width double each pass — 16 → 32 → 64 → … — until a single pass covers the whole array
                """);
        Add("Rotate merge sort (Recursive)", "Merge Sorts", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => RotateMergeSortRecursive.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Merges two adjacent sorted runs entirely in-place by using block rotations instead of copying to an auxiliary buffer, recursively solving the smaller sub-problems that each rotation leaves behind.

                Key property: Requires no extra memory at all — useful when auxiliary allocations must be avoided — but the cost of rotation raises the complexity to O(n log² n) compared to the O(n log n) of buffer-based Merge sort.

                Watch for:
                - Compare: binary search locates the pivot elements within each run to identify exactly which blocks need to be rotated
                - Swap: the rotation itself is a series of swaps that cyclically shift a block of elements into its target position
                - End of merge: each rotation resolves part of the overlap and leaves smaller in-place sub-problems, which are solved recursively
                """);
        Add("SymMerge sort", "Merge Sorts", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => SymMergeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Bottom-up in-place stable sort that replaces Rotate merge sort's repeated small rotations with SymMerge: one symmetric binary search finds the optimal split point, one rotation bridges the two runs, and two recursive calls finish each half.

                Key property: The symmetric binary search costs only O(log n) comparisons per recursive call, and by the recurrence T(n) = 2T(n/2) + O(log n) each merge requires only O(n) total comparisons — reducing the overall sort from O(n log² n) to O(n log n) comparisons, while element moves remain O(n log² n).

                Watch for:
                - Compare: the binary search tests s[p−c] ≥ s[c] at the symmetric mirror position; the split found is optimal, so exactly one rotation and no redundant scans are needed
                - Swap: a single rotation — implemented via 3-reversal — moves all misplaced elements into position at once, rather than the many small rotations that Rotate merge sort performs
                - End of merge: two recursive SymMerge calls handle the two resulting subproblems on each half of the range, halving the problem at each level until the base case is reached
                """);
        Add("Powersort", "Merge Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => PowerSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the input for natural runs like Timsort, but schedules merges by computing a "power" value from each run's position and size to determine the provably optimal merge order.

                Key property: The power-based scheduling guarantees a comparison count close to the information-theoretic lower bound on structured inputs — a proven improvement over Timsort's heuristic stack rules when runs have irregular lengths.

                Watch for:
                - Compare: adjacent pairs are tested during the run-detection scan; a power value is computed from each run's position in the array and its length relative to its neighbour
                - RangeCopy: when two runs are merged, the shorter is copied into a buffer before the merge begins
                - IndexWrite: values from the buffer are written back into the original array as the merge proceeds
                """,
            tutorialArrayType: TutorialArrayType.MultiRun);
        Add("ShiftSort", "Merge Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => ShiftSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the input to detect both ascending and descending runs, reverses any descending runs in-place to convert them to ascending, then merges all runs using an adaptive strategy similar to Timsort.

                Key property: Symmetric run detection — recognising both forward and backward sequences — doubles the chance of finding a long natural run at any position, making it especially effective on inputs that mix ascending and descending sections.

                Watch for:
                - Compare: adjacent pairs are tested to distinguish ascending runs from descending ones; descending run boundaries are recorded for reversal
                - Swap: detected descending runs are reversed in-place by swapping symmetric pairs around the midpoint, converting them to ascending at zero allocation cost
                - RangeCopy: after all runs are prepared, adjacent runs are merged using a buffer-based merge step identical to Timsort's
                """,
            tutorialArrayType: TutorialArrayType.MultiRun);
        Add("Timsort", "Merge Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => TimSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the input for naturally ordered runs, uses Insertion sort to extend any run shorter than a minimum length (minrun), then merges runs from a stack using a strategy that keeps stack heights balanced.

                Key property: Adaptive — the more existing order the input has, the fewer merges are needed, approaching O(n) on nearly-sorted data; this is why it was chosen as the standard library sort for CPython and Java.

                Watch for:
                - Compare: adjacent pairs are tested during the run-detection scan; each confirmed run is pushed onto the merge stack
                - IndexWrite: Insertion sort extends runs that fall below minrun, stitching short sequences into longer ones before any merging begins
                - RangeCopy: when the stack triggers a merge, the shorter of the two runs is copied into a buffer; sorted values are then written back
                """,
            tutorialArrayType: TutorialArrayType.MultiRun);

        // Heap Sorts - O(n log n) - 推奨2048
        Add("Heapsort", "Heap Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => HeapSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Builds a max-heap from the array so the root always holds the largest unsorted value, then repeatedly extracts the root by swapping it with the last unsorted element, shrinks the heap boundary by one, and restores the heap property via sift-down.

                Key property: In-place and O(n log n) in the worst case with no extra memory, but not stable and not adaptive — it performs the same amount of work on every input regardless of its existing order.

                Watch for:
                - Compare: during sift-down, the current node is compared against its two children; the larger child is promoted if it beats the parent
                - Swap: the root swaps with the last unsorted element to extract the maximum; sift-down then swaps the demoted root downward until the heap property is restored
                - End of phase: a heap-build phase first heapifies the entire array from the bottom up; the extraction phase then begins, growing the sorted region at the right one element per step
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.HeapTree);
        Add("Ternary heapsort", "Heap Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => TernaryHeapSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Same build-then-extract structure as Heapsort but uses a ternary heap where each node has up to three children; sift-down promotes the largest of the three children if it beats the current node.

                Key property: The ternary tree is shallower by a factor of log₂3 ≈ 1.58, reducing the number of sift-down levels per extraction — fewer levels means fewer swaps, even though each level requires comparing three children instead of two.

                Watch for:
                - Compare: each sift-down step compares the current node against all three children to find the largest; up to two comparisons are needed per level to identify the winner
                - Swap: the winning child is promoted if it beats the parent; sift-down continues from that child's position but reaches the leaf level in fewer steps than a binary heap
                - End of extraction: the sorted region at the right grows one element per extraction step, identical in structure to standard Heapsort
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.TernaryHeapTree);
        Add("Bottom-up heapSort", "Heap Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BottomupHeapSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Modifies Heapsort's sift-down by first descending all the way to the leaf without any comparisons — always following the larger child — then walking back up to find the displaced root's correct position.

                Key property: The "descend-then-ascend" strategy halves the average comparisons per sift-down, because the upward correction path is typically much shorter than a top-down search — making it measurably faster than standard Heapsort in practice despite the same asymptotic complexity.

                Watch for:
                - Compare: no comparisons are made during the initial blind descent to the leaf; comparisons only occur during the upward walk to locate the correct insertion point for the displaced root
                - Swap: once the correct level is found on the way up, the element is placed there with fewer total swaps than a standard top-down sift-down would require
                - End of extraction: the pattern is identical to Heapsort — the sorted right region grows one element per step, but each step completes with fewer comparisons
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.HeapTree);
        Add("Weak heapSort", "Heap Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => WeakHeapSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Builds a weak heap — a relaxed binary tree where each node only needs to dominate its right subtree — tracked with one reverse-bit per node, then merges sub-heaps to extract elements in sorted order.

                Key property: Every merge of two weak heaps costs exactly one comparison, reducing total comparisons for n extractions to the theoretical minimum of n⌈log₂ n⌉ — closer to the information-theoretic bound than any standard heap variant.

                Watch for:
                - Compare: each weak-heap merge requires exactly one comparison between a parent and the root of its distinguished child (determined by the reverse-bit)
                - Swap: when the merge comparison fails, the parent and distinguished child values are exchanged and the reverse-bit for that node is toggled, maintaining the weak heap invariant
                - End of extraction: the maximum is removed from the root; the left and right sub-heaps are re-merged one comparison at a time to restore the weak heap structure
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.WeakHeapTree);
        Add("Smoothsort", "Heap Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => SmoothSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Maintains a forest of Leonardo heaps — whose sizes follow the Leonardo number series 1, 1, 3, 5, 9, 15, 25, … — adding each element to extend the rightmost heap, then extracts elements by dismantling the forest in reverse order.

                Key property: Adaptive — when the input is already sorted no swaps are performed and the algorithm completes in O(n); unlike standard Heapsort it degrades gracefully rather than always running in full O(n log n) regardless of input order.

                Watch for:
                - Compare: during the build phase, adjacent heap roots are compared to keep them in order; during sift-down within a heap, each node is compared against its two children
                - Swap: when a heap root is smaller than the previous heap's root, the roots swap and sift-down propagates the change downward to restore the local heap property
                - End of phase: the extraction phase removes elements from the rightmost Leonardo heap, splitting it into two smaller Leonardo heaps that are relinked into the remaining forest
                """);

        // Partition Sorts - O(n log n) - 推奨2048-4096
        Add("Quicksort", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Selects a pivot element, partitions the array in-place into elements ≤ pivot on the left and elements > pivot on the right, then recursively sorts each side.

                Key property: O(n log n) average but O(n²) worst-case when the pivot is consistently the smallest or largest element — the simplest demonstration of why pivot selection is critical.

                Watch for:
                - Compare: each element in the unsorted region is tested against the pivot to determine which side it belongs to
                - Swap: elements found on the wrong side of the partition boundary are swapped across it; the pivot is placed at its final sorted position at the end
                - End of partition: the pivot's position is fixed, splitting the remaining unsorted work into two independent sub-problems
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (Median3)", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortMedian3.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Selects the pivot as the median of the first, middle, and last elements, then performs the same in-place partition and recursion as basic Quicksort.

                Key property: The median-of-three pivot is unlikely to be the extreme value, significantly reducing the probability of the O(n²) worst case on common patterns such as sorted or reverse-sorted input.

                Watch for:
                - IndexRead: three elements (first, middle, last) are read and compared to select the median; the chosen pivot is moved to a fixed position before partitioning begins
                - Compare: same left-right partition scan as basic Quicksort; elements are tested against the selected median pivot
                - Swap: same as basic Quicksort — elements on the wrong side are swapped across the partition boundary
                """);
        Add("Quicksort (Median9)", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortMedian9.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Samples nine elements from three equally spaced groups of three, takes the median of each group, then takes the median of those three medians as the pivot before partitioning.

                Key property: Nine samples approximate the true median far better than three, producing more balanced partitions and fewer total comparisons on large arrays — at the cost of nine extra reads per partition step.

                Watch for:
                - IndexRead: nine elements are sampled, three group medians are computed, and the median-of-medians is chosen as the pivot; watch the extra reads before the partition scan begins
                - Compare: same left-right partition scan as basic Quicksort; the better-chosen pivot typically produces more even splits
                - Swap: same as basic Quicksort — elements on the wrong side are swapped across the partition boundary
                """);
        Add("Quicksort (DualPivot)", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortDualPivot.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Picks two pivots p1 ≤ p2 and partitions the array into three regions in a single pass: elements < p1, elements between p1 and p2, and elements > p2; each region is then sorted recursively.

                Key property: Three partitions instead of two reduce average comparisons by roughly 5/9 compared to single-pivot Quicksort; this exact algorithm is used by Java's Arrays.sort for primitive types.

                Watch for:
                - Compare: each element is compared against both pivots to determine which of the three regions it belongs to; two comparisons per element rather than one
                - Swap: elements are moved to their correct region; at the end of each step both pivots are placed at their final sorted positions, fixing two positions at once
                - End of partition: two pivot positions are fixed simultaneously, splitting the remaining work into three independent sub-problems
                """);
        Add("Quicksort (Stable)", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => StableQuickSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Performs Quicksort's partition step using an auxiliary buffer to collect elements from each side in their original order, then writes them back — preserving the relative order of equal elements.

                Key property: Stability requires O(n) auxiliary space per partition — the direct cost of guaranteeing that equal elements keep their original ordering, which standard in-place Quicksort cannot provide.

                Watch for:
                - Compare: each element is compared against the pivot to determine its partition, same as standard Quicksort
                - RangeCopy: elements from each partition are copied into an auxiliary buffer in their original order to preserve stability
                - IndexWrite: elements are written back from the buffer to the main array; equal elements appear in the same relative order as before the partition
                """);
        Add("BlockQuickSort", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BlockQuickSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Reorganises Quicksort's partition step into two separate phases: a comparison phase that fills small fixed-size index blocks with positions of out-of-place elements, then a swap phase that exchanges those indexed pairs in sequence.

                Key property: Separating comparison from swapping eliminates the branch mispredictions that slow classic Quicksort on modern CPUs — the comparison phase accesses memory predictably and produces no conditional branches around swaps.

                Watch for:
                - Compare: elements are compared against the pivot in bulk to fill a block buffer with indices of out-of-place elements; no swaps occur during this phase
                - Swap: once full blocks are collected on both sides, the indexed pairs are swapped in sequence; the swap pattern is data-independent and branch-free
                - End of partition: leftover elements outside the filled blocks are handled with a short standard scan, then the pivot is placed at its final position
                """);
        Add("Introsort", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => IntroSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Starts with Quicksort but monitors recursion depth; switches to Heapsort when depth exceeds 2⌊log₂ n⌋ to guarantee O(n log n) worst-case, and finishes sub-arrays smaller than a fixed threshold with Insertion sort.

                Key property: Combines the practical speed of Quicksort, the worst-case safety of Heapsort, and the low overhead of Insertion sort for small inputs — the foundation of std::sort in most C++ standard libraries.

                Watch for:
                - Compare (Quicksort phase): left-right partition scan testing each element against the pivot; cross-boundary swaps follow
                - Swap (Heapsort fallback): when the depth limit is hit, the pattern shifts to sift-down swaps moving elements downward through heap levels
                - IndexWrite (Insertion sort finish): once sub-arrays shrink below the threshold, leftward element shifts replace partitioning
                """);
        Add("IntrosortDotnet", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => IntroSortDotnet.Sort(arr, ctx),
            tutorialDescription: """
                How it works: The Introsort variant used inside the .NET runtime (Array.Sort); follows the same Quicksort → Heapsort → Insertion sort hybrid strategy but with .NET-specific depth threshold and small-array cutoff values.

                Key property: Tuned for the managed runtime and JIT compiler — the threshold sizes differ from generic Introsort to maximise performance under .NET's memory model and compilation characteristics.

                Watch for:
                - Compare (Quicksort phase): left-right partition scan testing each element against the pivot; cross-boundary swaps follow
                - Swap (Heapsort fallback): when the .NET-specific depth limit is hit, the pattern shifts to sift-down swaps moving elements downward through heap levels
                - IndexWrite (Insertion sort finish): once sub-arrays shrink below the .NET-specific cutoff, leftward element shifts replace partitioning
                """);
        Add("Pattern-defeating quicksort", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => PDQSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: An Introsort variant that tests the input for common structural patterns before each partition — sorted, reverse-sorted, many equal elements — and takes a fast path when structure is detected, otherwise using block partitioning and pivot shuffling.

                Key property: Pattern detection lets it shortcut work on structured inputs that would cripple standard Quicksort; currently the algorithm behind Rust's slice::sort_unstable and a benchmark reference for unstable sorts.

                Watch for:
                - Compare (pattern detection): a scan tests the input for existing order before each partition; if structure is found a fast path replaces the full partition step
                - Swap (block partition): when a full partition is needed, compare and swap phases are separated like BlockQuickSort; pivot-shuffling swaps also fire when a bad partition is detected
                - End of phase: transitions between Quicksort, Insertion sort, and Heapsort are reactive to input structure — more frequent and earlier than in standard Introsort
                """);
        Add("C++ std::sort", "Partition Sorts", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => StdSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: A faithful re-implementation of the GNU libstdc++ Introsort: uses median-of-three pivot selection, falls back to Heapsort after too many recursion levels, and switches to Insertion sort for sub-arrays below a fixed threshold.

                Key property: Not a custom variant — this is the exact algorithm that has sorted data in C++ programs for decades, making it a direct historical baseline for comparing all other algorithms in this laboratory.

                Watch for:
                - Compare (Quicksort phase): median-of-three pivot selection precedes each partition; the standard left-right scan and cross-boundary swaps follow
                - Swap (Heapsort fallback): when the recursion depth limit is exceeded, the algorithm switches to Heapsort's sift-down swap pattern
                - IndexWrite (Insertion sort finish): once sub-arrays fall below the small-array threshold, Insertion sort takes over with leftward element shifts
                """);

        // Adaptive Sorts - O(n log n) - 推奨2048
        Add("Drop-Merge sort", "Adaptive Sorts", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => DropMergeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Scans the input once to identify the longest non-decreasing subsequence already in place, collects all out-of-order "dropped" elements into a separate buffer, sorts that buffer, then merges it back with the in-order portion.

                Key property: On nearly-sorted data with only k out-of-place elements the sort cost shrinks to O((k+1) log(k+1) + n) — the sparser the drops, the faster the algorithm, approaching O(n) as k approaches 0.

                Watch for:
                - Compare (drop detection): each element is tested against the last accepted value; elements that break the non-decreasing order are extracted and collected into a separate buffer
                - IndexRead: extracted "drop" elements are lifted out of the array into the buffer while the forward scan continues past them
                - Compare (merge): after the buffer is sorted, a standard merge compares the buffer's front element against the surviving in-order portion to determine write order
                - IndexWrite: the winning value from each merge step is written back, interleaving the sorted drops with the in-order run
                """);

        // Distribution Sorts - O(n) ~ O(nk) - 推奨4096
        Add("Counting sort", "Distribution Sorts", "O(n+k)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => CountingSortInteger.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Counts how many times each distinct value appears, computes a running prefix-sum of those counts to determine each value's output position, then writes every element directly to its computed position.

                Key property: Never compares values against each other — positions are computed arithmetically from counts — which is why it breaks the O(n log n) comparison-based lower bound and runs in O(n + k) where k is the value range.

                Watch for:
                - IndexRead: each element is read to increment its slot in the frequency table; no element moves during this phase
                - IndexWrite: the prefix-sum pass overwrites the frequency table with cumulative output positions; each element is then written to its computed position in a single placement pass
                - End of phase: three distinct phases are clearly visible — count, prefix-sum, and place — none of which involves any comparisons
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("Pigeonhole sort", "Distribution Sorts", "O(n+k)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => PigeonholeSortInteger.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Allocates one pigeonhole bucket per possible value in the range, drops each input element into its matching bucket, then reads the buckets back in value order to reconstruct a sorted output.

                Key property: Physically moves elements into individual value buckets rather than computing positions arithmetically — conceptually the simplest distribution sort, and inherently stable since elements are read back in the order they were deposited.

                Watch for:
                - IndexRead: each element's value is used directly as the bucket index; the element is deposited into that bucket slot
                - IndexWrite: elements are written into their bucket positions during the scatter phase
                - IndexRead + IndexWrite: during the gather phase, buckets are read in value order and each element is written back to the output array
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("Bucket sort", "Distribution Sorts", "O(n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => BucketSortInteger.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Divides the value range into a fixed number of equal-width buckets, scatters each element into its corresponding bucket, sorts each non-empty bucket independently, then concatenates all buckets in order.

                Key property: When elements are uniformly distributed each bucket holds O(1) elements on average, making the total sort O(n); degrades toward O(n²) if many elements cluster into the same bucket.

                Watch for:
                - IndexRead: each element's value is mapped to a bucket index arithmetically; the element is deposited into that bucket
                - Compare + IndexWrite: each non-empty bucket is sorted independently (typically with Insertion sort); watch per-bucket comparisons and shifts
                - IndexWrite (gather): sorted buckets are concatenated back into the original array in bucket order
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("LSD Radix sort (b=4)", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD4Sort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Sorts integers digit by digit from the least significant to the most significant 2-bit group, applying a stable counting sort at each pass so that the order established by previous passes is never disturbed.

                Key property: Base 4 (2-bit groups) keeps the per-pass counting table to just four slots, minimising the memory footprint per pass at the cost of requiring more passes than larger bases.

                Watch for:
                - IndexRead: each element's current 2-bit digit group is extracted by a bitwise operation; the digit value increments its slot in the four-entry counting table
                - IndexWrite: the prefix-sum pass converts counts to output positions; elements are then written to the output buffer in stable order by their current digit
                - End of pass: each pass produces an array sorted by all digit positions processed so far; watch the partial order grow pass by pass from the least significant bits upward
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd,
            tutorialLsdRadix: 4);
        Add("LSD Radix sort (b=10)", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD10Sort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Sorts integers digit by digit from the least significant to the most significant decimal digit, applying a stable counting sort at each pass so that the order established by previous passes is never disturbed.

                Key property: Base 10 is the most intuitive base to follow visually — each pass sorts by the ones, tens, hundreds, … digit in turn — making it the clearest choice for illustrating how LSD Radix sort progressively builds global order from local digit passes.

                Watch for:
                - IndexRead: each element's current decimal digit is extracted by a modulo/division operation; the digit value increments its slot in the ten-entry counting table
                - IndexWrite: the prefix-sum pass converts counts to output positions; elements are then written to the output buffer in stable order by their current digit
                - End of pass: after the ones pass elements are grouped by last digit; after the tens pass by last two digits; each pass visibly extends the sorted prefix
                """,
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd,
            tutorialLsdRadix: 10);
        Add("LSD Radix sort (b=256)", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD256Sort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Sorts integers byte by byte from the least significant to the most significant byte, applying a stable counting sort at each pass; for 32-bit integers only four passes are needed to fully sort the array.

                Key property: Base 256 (8-bit bytes) processes the maximum number of bits per pass while keeping the 256-entry counting table small enough to fit in L1 cache — the most common base in high-performance radix sort implementations.

                Watch for:
                - IndexRead: each element's current byte is extracted by a bitwise shift; the byte value increments its slot in the 256-entry counting table
                - IndexWrite: the prefix-sum pass converts counts to output positions; elements are written to the output buffer in stable byte order
                - End of pass: only four passes are needed for 32-bit integers; each pass is fast because the 256-entry counting table stays in L1 cache throughout
                """,
            excludeFromTutorial: true);
        Add("MSD Radix sort (b=4)", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixMSD4Sort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Sorts integers digit by digit from the most significant to the least significant 2-bit group, distributing elements into up to four buckets per pass and then recursively sorting each bucket for the next digit.

                Key property: Processing the most significant digit first allows early termination — sub-buckets whose elements already agree on all remaining digits need no further work — giving it adaptive behaviour similar to a comparison sort.

                Watch for:
                - IndexRead: each element's current most-significant remaining digit is extracted by a bitwise operation; the digit determines which of the four buckets the element enters
                - IndexWrite: elements are placed into their digit buckets; each bucket is then processed recursively for the next digit position
                - End of pass: sub-buckets of size 1 terminate immediately; watch small sub-buckets stop early while larger ones recurse deeper into finer digit positions
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketMsd,
            tutorialLsdRadix: 4);
        Add("MSD Radix sort (b=10)", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixMSD10Sort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Sorts integers digit by digit from the most significant decimal digit first, distributing elements into up to ten buckets per pass and then recursively sorting each bucket for the next digit.

                Key property: Partitioning by the leading digit first groups elements into coarse buckets immediately — closely resembling how a human sorts a pile of numbered cards by hundreds digit, then tens, then ones.

                Watch for:
                - IndexRead: each element's leading decimal digit is extracted by a division/modulo operation; the digit determines which of the ten buckets the element enters
                - IndexWrite: elements are placed into their digit buckets; each bucket is then processed recursively for the next digit
                - End of pass: elements that already differ at the current digit are permanently separated and never processed together again; watch the recursive depth grow only for elements sharing a long common prefix
                """,
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketMsd,
            tutorialLsdRadix: 10);
        Add("American flag sort", "Distribution Sorts", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => AmericanFlagSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: An in-place MSD Radix sort that makes two passes per digit: the first counts elements per bucket to compute bucket boundaries, the second cyclically permutes elements into their correct buckets without any auxiliary array.

                Key property: Achieves MSD radix sorting with O(1) extra space beyond the fixed-size counting table — the cyclic permutation replaces the auxiliary buffer that standard MSD Radix sort requires.

                Watch for:
                - IndexRead: the first pass reads each element's current digit to build the count table and compute bucket boundary positions
                - Swap (cyclic permutation): in the second pass, each out-of-place element is swapped directly into its correct bucket; the displaced element is immediately re-routed to its own destination, continuing the cycle until the chain closes
                - End of pass: once all elements sit in their correct bucket for the current digit, the algorithm recurses into each bucket for the next digit position
                """);

        // Network Sorts - O(log²n) - 推奨2048
        Add("Bitonic sort", "Network Sorts", "O(log²n)", MAX_SIZE_N2, 2048, (arr, ctx) => BitonicSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: First builds a bitonic sequence — one that rises then falls — by merging pairs of sub-sequences in alternating sort directions, then collapses the full bitonic sequence into sorted order with a final bitonic merge.

                Key property: Every comparison-and-swap fires at a fixed, data-independent position determined by the network structure — the same comparators apply regardless of element values — making it directly executable in parallel on GPUs and SIMD hardware.

                Watch for:
                - Compare: pairs at predetermined index positions are tested; the positions are dictated by the network topology, not by element values
                - Swap: fires when a comparator finds elements in the wrong order for the current direction (ascending or descending sub-sequence being built or merged)
                - End of phase: the build phase creates bitonic sub-sequences by alternating direction; the merge phase then collapses them into a single ascending sorted sequence
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork);
        Add("Bitonic sort (Recursive)", "Network Sorts", "O(log²n)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => BitonicSortNonOptimized.Sort(arr, ctx),
            tutorialDescription: """
                How it works: The same bitonic build-then-merge structure as iterative Bitonic sort, but expressed directly as recursive calls that mirror the network's divide-and-conquer shape.

                Key property: Easier to read and verify than the iterative version because the recursive structure maps one-to-one onto the network diagram, but carries additional call-stack overhead; produces identical comparison-and-swap sequences on power-of-two sized arrays.

                Watch for:
                - Compare: pairs at predetermined index positions are tested; identical comparator positions to the iterative version
                - Swap: fires when a comparator finds elements in the wrong order for the current direction
                - End of phase: the recursive call depth corresponds directly to the number of network stages; watch the build and merge phases emerge as distinct call-depth levels
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork);

        // Tree Sorts - O(n log n) - 推奨1024
        Add("Binary tree sort (BST)", "Tree Sorts", "O(n log n)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => BinaryTreeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Inserts every element into a plain binary search tree — smaller values go left, larger values go right — then recovers sorted output by reading all nodes in in-order traversal (left, root, right).

                Key property: O(n log n) average case but degrades to O(n²) time and call-stack depth on already-sorted input, because each new element always attaches as the rightmost leaf, producing a degenerate tree of height n.

                Watch for:
                - Compare: each insertion navigates the tree by comparing the new element against the current node, turning left or right until an empty slot is found
                - IndexWrite: the new element is placed at the found empty slot, extending the tree by one node
                - IndexRead (in-order traversal): after all insertions, nodes are visited left-root-right to reconstruct the sorted sequence without any further comparisons
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.BstTree);
        Add("Binary tree sort (AVL)", "Tree Sorts", "O(n log n)", MAX_SIZE_NLOGN15, 2048, (arr, ctx) => BalancedBinaryTreeSort.Sort(arr, ctx),
            tutorialDescription: """
                How it works: Inserts elements into a self-balancing binary search tree that automatically keeps its height at O(log n), then recovers sorted output via in-order traversal.

                Key property: The height guarantee ensures O(n log n) worst-case even for sorted or reverse-sorted input — the rebalancing after each insertion prevents the degenerate O(n²) behaviour of a plain binary search tree.

                Watch for:
                - Compare: each insertion navigates the tree by comparing the new element against the current node, turning left or right until an empty slot is found
                - Swap (rotation): when an insertion unbalances the tree, one or more rotations rearrange a small cluster of nodes to restore the height constraint
                - IndexRead (in-order traversal): after all insertions, nodes are visited left-root-right to reconstruct the sorted sequence
                """,
            tutorialVisualizationHint: TutorialVisualizationHint.AvlTree);

        // Joke Sorts - O(n!) ~ O(∞) - 推奨8（注意: 極めて遅い）
        // Bogo sort: ランダムシャッフルで非決定的のためチュートリアル対象外。
        // Slow / Stooge: 決定的な再帰アルゴリズムのため、4要素に絞ってチュートリアル可能。
        Add("Bogo sort", "Joke Sorts", "O(n!)", 8, MAX_SIZE_JOKE_BOGO, (arr, ctx) => BogoSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: """
                How it works: Repeatedly checks whether the array is sorted; if not, shuffles all elements completely at random and tries again, with no memory of previous attempts.

                Key property: Expected O(n · n!) time — there are n! possible orderings and each shuffle is equally likely to produce any of them — making it the worst-known sorting algorithm and a humorous illustration of what "not" to do.

                Watch for:
                - Compare: a sorted-check scan tests adjacent pairs left-to-right; the scan stops and triggers a reshuffle the moment any out-of-order pair is found
                - Swap (random shuffle): when the check fails, all elements are randomly permuted; each shuffle is completely independent of all previous attempts
                """,
            excludeFromTutorial: true);
        Add("Slow sort", "Joke Sorts", "O(n^(log n))", MAX_SIZE_JOKE, 16, (arr, ctx) => SlowSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: """
                How it works: Finds the maximum of a sub-array by recursively sorting both halves and comparing their last elements, moves that maximum to the end of the range, then recurses on the remainder — doing far more work than necessary at every step.

                Key property: Based on the tongue-in-cheek "multiply and surrender" principle, the redundant recursive calls produce super-polynomial O(n^(log n)) time — slower than any practical algorithm for all but trivially small inputs.

                Watch for:
                - Compare: the maximum of two recursively sorted halves is found by comparing their last elements; the larger is promoted to the end of the current range
                - Swap: the promoted maximum is moved to the final position of the current sub-array
                - End of phase: after placing the maximum the algorithm recurses on the remainder — but both halves were already recursively sorted, so the work already done is massively redundant
                """,
            tutorialArrayType: TutorialArrayType.FourElement);
        Add("Stooge sort", "Joke Sorts", "O(n^2.7)", MAX_SIZE_JOKE, 16, (arr, ctx) => StoogeSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            tutorialDescription: """
                How it works: Checks and corrects the first and last elements, then recursively sorts the first two-thirds of the array, then the last two-thirds, then the first two-thirds again — three overlapping recursive passes at every level.

                Key property: The triple-recursive structure on overlapping two-thirds regions is correct but wildly wasteful: T(n) = 3T(2n/3) solves to O(n^2.71), worse than any practical sorting algorithm and introduced in CLRS as a cautionary example of correctness without efficiency.

                Watch for:
                - Compare: the first action at each level checks only the first and last elements of the current range; all other ordering is delegated to the recursive calls
                - Swap: fires only when the first element is greater than the last; the three recursive calls then handle everything else
                - End of phase: observe the three-pass pattern — first-two-thirds, last-two-thirds, first-two-thirds — repeating recursively on shrinking sub-arrays
                """,
            tutorialArrayType: TutorialArrayType.FourElement);
    }

    private void Add(string name, string category, string complexity, int maxElements, int recommendedSize,
        Action<Span<int>, ISortContext> sortAction, string description = "", string tutorialDescription = "",
        TutorialArrayType tutorialArrayType = TutorialArrayType.Default, bool excludeFromTutorial = false,
        TutorialVisualizationHint tutorialVisualizationHint = TutorialVisualizationHint.None,
        int tutorialLsdRadix = 0)
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
            ExcludeFromTutorial = excludeFromTutorial,
            TutorialVisualizationHint = tutorialVisualizationHint,
            TutorialLsdRadix = tutorialLsdRadix,
        });
    }
}

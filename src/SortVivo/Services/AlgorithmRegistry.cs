using SortVivo.Models;
using SortAlgorithm.Contexts;
using SortAlgorithm.Algorithms;

namespace SortVivo.Services;

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

    private void RegisterAlgorithms()
    {
        // 最大サイズは全て16384、推奨サイズは計算量に応じて設定
        const int MAX_SIZE_N2 = 2048;
        const int MAX_SIZE_NLOGN15 = 8192;
        const int MAX_SIZE_NLOGN = 8192;
        const int MAX_SIZE_JOKE = 16;
        const int MAX_SIZE_JOKE_BOGO = 8;

        // Exchange Sorts - O(n²) - 推奨256
        Add("Bubble sort", "EXCHANGE", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => BubbleSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Exchange", "BubbleSort"));
        Add("Cocktail shaker sort", "EXCHANGE", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => CocktailShakerSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Exchange", "CocktailShakerSort"));
        Add("Odd-even sort", "EXCHANGE", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => OddEvenSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Exchange", "OddEvenSort"));
        Add("Comb sort", "EXCHANGE", "O(n²)", MAX_SIZE_N2, 512, (arr, ctx) => CombSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Exchange", "CombSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap);

        // Selection Sorts - O(n²) - 推奨256
        Add("Selection sort", "SELECTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => SelectionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Selection", "SelectionSort"));
        Add("Double selection sort", "SELECTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => DoubleSelectionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Selection", "DoubleSelectionSort"));
        Add("Cycle sort", "SELECTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => CycleSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Selection", "CycleSort"));
        Add("Pancake sort", "SELECTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => PancakeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Selection", "PancakeSort"));

        // Insertion Sorts
        Add("Insertion sort", "INSERTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => InsertionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "InsertionSort"));
        Add("Pair insertion sort", "INSERTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => PairInsertionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "PairInsertionSort"));
        Add("Binary insert sort", "INSERTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => BinaryInsertionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "BinaryInsertionSort"));
        Add("Gnome sort", "INSERTION", "O(n²)", MAX_SIZE_N2, 256, (arr, ctx) => GnomeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "GnomeSort"));
        Add("Library sort", "INSERTION", "O(n log n)", MAX_SIZE_NLOGN15, 2048, (arr, ctx) => LibrarySort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "LibrarySort"));
        Add("Merge insertion sort", "INSERTION", "O(n log n)", MAX_SIZE_N2, 256, (arr, ctx) => MergeInsertionSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "MergeInsertionSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.MergeInsertionPairs);
        Add("Shell sort (Knuth 1973)", "INSERTION", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortKnuth1973.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "ShellSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap,
            tutorialArrayType: TutorialArrayType.ShellSort);
        Add("Shell sort (Sedgewick 1986)", "INSERTION", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortSedgewick1986.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "ShellSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap,
            tutorialArrayType: TutorialArrayType.ShellSort);
        Add("Shell sort (Tokuda 1992)", "INSERTION", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortTokuda1992.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "ShellSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap,
            tutorialArrayType: TutorialArrayType.ShellSort);
        Add("Shell sort (Ciura 2001)", "INSERTION", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortCiura2001.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "ShellSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap,
            tutorialArrayType: TutorialArrayType.ShellSort);
        Add("Shell sort (Lee 2021)", "INSERTION", "O(n^1.5)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => ShellSortLee2021.Sort(arr, ctx),
            gitHubSourceUrl: Src("Insertion", "ShellSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ShellGap,
            tutorialArrayType: TutorialArrayType.ShellSort);

        // Merge Sorts
        Add("Merge sort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => MergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "MergeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Pingpong merge sort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => PingpongMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "PingpongMergeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Bottom-up merge sort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BottomupMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "BottomupMergeSort"));
        Add("Rotate merge sort", "MERGE", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => RotateMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "RotateMergeSort"));
        Add("Rotate merge sort (Recursive)", "MERGE", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => RotateMergeSortRecursive.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "RotateMergeSort"));
        Add("SymMerge sort", "MERGE", "O(n log² n)", MAX_SIZE_NLOGN, 1024, (arr, ctx) => SymMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "SymMergeSort"));
        Add("ShiftSort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => ShiftSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "ShiftSort"),
            tutorialArrayType: TutorialArrayType.MultiRun);
        Add("Natural merge sort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => NaturalMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "NaturalMergeSort"),
            tutorialArrayType: TutorialArrayType.MultiRun);
        Add("Timsort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => TimSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "TimSort"),
            tutorialArrayType: TutorialArrayType.MultiRun);
        Add("Powersort", "MERGE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => PowerSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Merge", "PowerSort"),
            tutorialArrayType: TutorialArrayType.MultiRun);

        // Heap Sorts - O(n log n) - 推奨2048
        Add("Heapsort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => HeapSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Heap", "HeapSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.HeapTree);
        Add("Ternary heapsort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => TernaryHeapSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Heap", "TernaryHeapSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.TernaryHeapTree);
        Add("Bottom-up heapSort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BottomupHeapSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Heap", "BottomupHeapSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.HeapTree);
        Add("Weak heapSort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => WeakHeapSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Heap", "WeakHeapSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.WeakHeapTree);
        Add("Smoothsort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => SmoothSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Heap", "SmoothSort"));
        Add("Tournament sort", "HEAP", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => TournamentSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Selection", "TournamentSort"));

        // Partition Sorts - O(n log n) - 推奨2048-4096
        Add("Quicksort", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "QuickSort"),
            tutorialArrayType: TutorialArrayType.PartitionSort,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (Median3)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortMedian3.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "QuickSortMedian3"),
            tutorialArrayType: TutorialArrayType.PartitionSort,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (Median9)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortMedian9.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "QuickSortMedian9"),
            tutorialArrayType: TutorialArrayType.PartitionSort,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (DualPivot)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSortDualPivot.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "QuickSortDualPivot"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (3-way)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => QuickSort3way.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "QuickSort3way"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Quicksort (Stable)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => StableQuickSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "StableQuickSort"),
            tutorialArrayType: TutorialArrayType.PartitionSort);
        Add("BlockQuickSort", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => BlockQuickSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "BlockQuickSort"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Introsort", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => IntroSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "IntroSort"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("IntrosortDotnet", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => IntroSortDotnet.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "IntroSortDotnet"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("Pattern-defeating quicksort", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => PDQSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "PDQSort"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);
        Add("std::sort (LLVM)", "PARTITION", "O(n log n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => StdSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Partition", "StdSort"),
            tutorialArrayType: TutorialArrayType.PartitionSortHybrid,
            tutorialVisualizationHint: TutorialVisualizationHint.RecursionTree);

        // Adaptive Sorts
        Add("Drop-Merge sort", "ADAPTIVE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => DropMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Adaptive", "DropMergeSort"));
        Add("Patience sort", "ADAPTIVE", "O(n log n)", MAX_SIZE_NLOGN, 2048, (arr, ctx) => PatienceSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Adaptive", "PatienceSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.PatiencePiles);
        Add("Strand sort", "ADAPTIVE", "O(n²)", MAX_SIZE_N2, 512, (arr, ctx) => StrandSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Adaptive", "StrandSort"));

        // Distribution Sorts - O(n) ~ O(nk) - 推奨4096
        Add("Counting sort", "DISTRIBUTION", "O(n+k)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => CountingSortInteger.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "CountingSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("Pigeonhole sort", "DISTRIBUTION", "O(n+k)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => PigeonholeSortInteger.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "PigeonholeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("Bucket sort", "DISTRIBUTION", "O(n)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => BucketSortInteger.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "BucketSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.ValueBucket);
        Add("Flash sort", "DISTRIBUTION", "O(n+m)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => FlashSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "FlashSort"),
            tutorialArrayType: TutorialArrayType.MultiRun,
            tutorialVisualizationHint: TutorialVisualizationHint.FlashSortClasses);
        Add("LSD Radix sort (b=4)", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD4Sort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "RadixLSD4Sort"),
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd,
            tutorialLsdRadix: 4);
        Add("LSD Radix sort (b=10)", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD10Sort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "RadixLSD10Sort"),
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketLsd,
            tutorialLsdRadix: 10);
        Add("LSD Radix sort (b=256)", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixLSD256Sort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "RadixLSD256Sort"));
        Add("MSD Radix sort (b=4)", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixMSD4Sort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "RadixMSD4Sort"),
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketMsd,
            tutorialLsdRadix: 4);
        Add("MSD Radix sort (b=10)", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => RadixMSD10Sort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "RadixMSD10Sort"),
            tutorialArrayType: TutorialArrayType.TwoDigitDecimal,
            tutorialVisualizationHint: TutorialVisualizationHint.DigitBucketMsd,
            tutorialLsdRadix: 10);
        Add("American flag sort", "DISTRIBUTION", "O(nk)", MAX_SIZE_NLOGN, 4096, (arr, ctx) => AmericanFlagSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Distribution", "AmericanFlagSort"));

        // Network Sorts
        Add("Bitonic sort", "NETWORK", "O(log²n)", MAX_SIZE_N2, 2048, (arr, ctx) => BitonicSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Network", "BitonicSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork);
        Add("Bitonic sort (Recursive)", "NETWORK", "O(log²n)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => BitonicSortNonOptimized.Sort(arr, ctx),
            gitHubSourceUrl: Src("Network", "BitonicSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork);
        Add("Batcher odd-even merge sort", "NETWORK", "O(n log²n)", MAX_SIZE_NLOGN15, 2048, (arr, ctx) => BatcherOddEvenMergeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Network", "BatcherOddEvenMergeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.SortingNetwork);

        // Tree Sorts - O(n log n) - 推奨1024
        Add("Binary tree sort (BST)", "TREE", "O(n log n)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => BinaryTreeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Tree", "BinaryTreeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.BstTree);
        Add("Binary tree sort (AVL)", "TREE", "O(n log n)", MAX_SIZE_NLOGN15, 2048, (arr, ctx) => BalancedBinaryTreeSort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Tree", "BalancedBinaryTreeSort"),
            tutorialVisualizationHint: TutorialVisualizationHint.AvlTree);
        Add("Splay sort", "TREE", "O(n log n)", MAX_SIZE_NLOGN15, 1024, (arr, ctx) => SplaySort.Sort(arr, ctx),
            gitHubSourceUrl: Src("Tree", "SplaySort"),
            tutorialVisualizationHint: TutorialVisualizationHint.BstTree);

        // Joke Sorts - O(n!) ~ O(∞) - 推奨8（注意: 極めて遅い）
        // Bogo sort: 4要素なら 4! = 24 通りなのでチュートリアルでランダム性を示せる。
        // Slow / Stooge: 決定的な再帰アルゴリズムのため、4要素に絞ってチュートリアル可能。
        Add("Bogo sort", "JOKE", "O(n!)", 8, MAX_SIZE_JOKE_BOGO, (arr, ctx) => BogoSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            gitHubSourceUrl: Src("Joke", "BogoSort"),
            tutorialArrayType: TutorialArrayType.FourElement);
        Add("Slow sort", "JOKE", "O(n^(log n))", MAX_SIZE_JOKE, 16, (arr, ctx) => SlowSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            gitHubSourceUrl: Src("Joke", "SlowSort"),
            tutorialArrayType: TutorialArrayType.FourElement);
        Add("Stooge sort", "JOKE", "O(n^2.7)", MAX_SIZE_JOKE, 16, (arr, ctx) => StoogeSort.Sort(arr, ctx), "⚠️ Extremely slow!",
            gitHubSourceUrl: Src("Joke", "StoogeSort"),
            tutorialArrayType: TutorialArrayType.FourElement);
    }

    private static string Src(string folder, string file)
        => $"https://github.com/guitarrapc/SortVivo/blob/main/src/SortAlgorithm/Algorithms/{folder}/{file}.cs";

    private void Add(string name, string category, string complexity, int maxElements, int recommendedSize,
        Action<Span<int>, ISortContext> sortAction, string description = "",
        TutorialArrayType tutorialArrayType = TutorialArrayType.Default, bool excludeFromTutorial = false,
        TutorialVisualizationHint tutorialVisualizationHint = TutorialVisualizationHint.None,
        int tutorialLsdRadix = 0, string gitHubSourceUrl = "")
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
            AlgorithmId = ToId(name),
            TutorialArrayType = tutorialArrayType,
            ExcludeFromTutorial = excludeFromTutorial,
            TutorialVisualizationHint = tutorialVisualizationHint,
            TutorialLsdRadix = tutorialLsdRadix,
            GitHubSourceUrl = gitHubSourceUrl,
        });
    }

    private static string ToId(string name)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(name, @"[^a-zA-Z0-9]+")
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();
        if (parts.Length == 0) return string.Empty;
        var first = char.ToLowerInvariant(parts[0][0]) + (parts[0].Length > 1 ? parts[0][1..] : "");
        var rest = parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : ""));
        return first + string.Concat(rest);
    }
}

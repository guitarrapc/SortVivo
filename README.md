# SortAlgorithm

This repository shows implementation for the Major Sort Algorithm.
Aim not to use LINQ or similar ease to use, but memory unefficient technique.

## Implemented Sort Algorithm

You can check various benchmark patterns at [GitHub Actions/Benchmark](https://github.com/guitarrapc/SortAlgorithms/actions/runs/22783774759).

### Adaptive
- [Drop Merge Sort](./src/SortAlgorithm/Algorithms/Adaptive/DropMergeSort.cs)

### Distribution
- [American Flag Sort](./src/SortAlgorithm/Algorithms/Distribution/AmericanFlagSort.cs)
- [Bucket Sort](./src/SortAlgorithm/Algorithms/Distribution/BucketSort.cs)
- [Counting Sort](./src/SortAlgorithm/Algorithms/Distribution/CountingSort.cs)
- [Pigeonhole Sort](./src/SortAlgorithm/Algorithms/Distribution/PigeonholeSort.cs)
- [Radix LSD Sort (Base 4)](./src/SortAlgorithm/Algorithms/Distribution/RadixLSD4Sort.cs)
- [Radix LSD Sort (Base 10)](./src/SortAlgorithm/Algorithms/Distribution/RadixLSD10Sort.cs)
- [Radix LSD Sort (Base 256)](./src/SortAlgorithm/Algorithms/Distribution/RadixLSD256Sort.cs)
- [Radix MSD Sort (Base 4)](./src/SortAlgorithm/Algorithms/Distribution/RadixMSD4Sort.cs)
- [Radix MSD Sort (Base 10)](./src/SortAlgorithm/Algorithms/Distribution/RadixMSD10Sort.cs)

### Exchange
- [Bubble Sort](./src/SortAlgorithm/Algorithms/Exchange/BubbleSort.cs)
- [Cocktail Shaker Sort](./src/SortAlgorithm/Algorithms/Exchange/CocktailShakerSort.cs)
- [Comb Sort](./src/SortAlgorithm/Algorithms/Exchange/CombSort.cs)
- [Odd-Even Sort](./src/SortAlgorithm/Algorithms/Exchange/OddEvenSort.cs)

### Heap
- [Bottom-Up Heap Sort](./src/SortAlgorithm/Algorithms/Heap/BottomupHeapSort.cs)
- [Heap Sort](./src/SortAlgorithm/Algorithms/Heap/HeapSort.cs)
- [Smooth Sort](./src/SortAlgorithm/Algorithms/Heap/SmoothSort.cs)
- [Ternary Heap Sort](./src/SortAlgorithm/Algorithms/Heap/TernaryHeapSort.cs)
- [Weak Heap Sort](./src/SortAlgorithm/Algorithms/Heap/WeakHeapSort.cs)

### Insertion
- [Binary Insertion Sort](./src/SortAlgorithm/Algorithms/Insertion/BinaryInsertionSort.cs)
- [Gnome Sort](./src/SortAlgorithm/Algorithms/Insertion/GnomeSort.cs)
- [Insertion Sort](./src/SortAlgorithm/Algorithms/Insertion/InsertionSort.cs)
- [Library Sort](./src/SortAlgorithm/Algorithms/Insertion/LibrarySort.cs)
- [Pair Insertion Sort](./src/SortAlgorithm/Algorithms/Insertion/PairInsertionSort.cs)
- [Shell Sort](./src/SortAlgorithm/Algorithms/Insertion/ShellSort.cs)
  - Knuth1973
  - Sedgewick1986
  - Tokuda1992
  - Ciura2001
  - Lee2021

### Joke
- [Bogo Sort](./src/SortAlgorithm/Algorithms/Joke/BogoSort.cs)
- [Slow Sort](./src/SortAlgorithm/Algorithms/Joke/SlowSort.cs)
- [Stooge Sort](./src/SortAlgorithm/Algorithms/Joke/StoogeSort.cs)

### Merge
- [Bottom-Up Merge Sort](./src/SortAlgorithm/Algorithms/Merge/BottomupMergeSort.cs)
- [Merge Sort](./src/SortAlgorithm/Algorithms/Merge/MergeSort.cs)
- [Power Sort](./src/SortAlgorithm/Algorithms/Merge/PowerSort.cs)
- [Rotate Merge Sort](./src/SortAlgorithm/Algorithms/Merge/RotateMergeSort.cs)
- [Shift Sort](./src/SortAlgorithm/Algorithms/Merge/ShiftSort.cs)
- [Tim Sort](./src/SortAlgorithm/Algorithms/Merge/TimSort.cs)

### Network
- [Bitonic Sort](./src/SortAlgorithm/Algorithms/Network/BitonicSort.cs)

### Partition
- [Block Quick Sort](./src/SortAlgorithm/Algorithms/Partition/BlockQuickSort.cs)
- [Intro Sort](./src/SortAlgorithm/Algorithms/Partition/IntroSort.cs)
- [Intro Sort (.NET)](./src/SortAlgorithm/Algorithms/Partition/IntroSort.cs)
- [Pattern-defeat Quick Sort](./src/SortAlgorithm/Algorithms/Partition/PDQSort.cs)
- [Quick Sort](./src/SortAlgorithm/Algorithms/Partition/QuickSort.cs)
- [Quick Sort (Dual Pivot)](./src/SortAlgorithm/Algorithms/Partition/QuickSortDualPivot.cs)
- [Quick Sort (Median of 3)](./src/SortAlgorithm/Algorithms/Partition/QuickSortMedian3.cs)
- [Quick Sort (Median of 9)](./src/SortAlgorithm/Algorithms/Partition/QuickSortMedian9.cs)
- [Stable Quick Sort](./src/SortAlgorithm/Algorithms/Partition/StableQuickSort.cs)
- [Std Sort](./src/SortAlgorithm/Algorithms/Partition/StdSort.cs)

### Selection
- [Cycle Sort](./src/SortAlgorithm/Algorithms/Selection/CycleSort.cs)
- [Double Selection Sort](./src/SortAlgorithm/Algorithms/Selection/DoubleSelectionSort.cs)
- [Pancake Sort](./src/SortAlgorithm/Algorithms/Selection/PancakeSort.cs)
- [Selection Sort](./src/SortAlgorithm/Algorithms/Selection/SelectionSort.cs)

### Tree
- [Balanced Binary Tree Sort](./src/SortAlgorithm/Algorithms/Tree/BalancedBinaryTreeSort.cs)
- [Binary Tree Sort](./src/SortAlgorithm/Algorithms/Tree/BinaryTreeSort.cs)

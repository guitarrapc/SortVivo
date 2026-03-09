using SortAlgorithm.Utils;
using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// 全配列生成パターンのメタデータを管理するレジストリ
/// </summary>
public class ArrayPatternRegistry
{
    private readonly List<ArrayPatternMetadata> _patterns = [];

    public ArrayPatternRegistry()
    {
        RegisterPatterns();
    }

    public IReadOnlyList<ArrayPatternMetadata> GetAllPatterns() => _patterns.AsReadOnly();

    public IEnumerable<ArrayPatternMetadata> GetByCategory(string category)
        => _patterns.Where(p => p.Category == category);

    public IEnumerable<string> GetCategories()
        => _patterns.Select(p => p.Category).Distinct().OrderBy(c => c);

    private void RegisterPatterns()
    {
        // Basic
        Add("🎲 Random", "Basic", ArrayPatterns.GenerateRandom, "Fully randomized array");
        Add("↗️ Sorted (Ascending)", "Basic", (size, _) => ArrayPatterns.GenerateSorted(size), "Already sorted in ascending order");
        Add("↘️ Reversed (Descending)", "Basic", (size, _) => ArrayPatterns.GenerateReversed(size), "Sorted in descending order");

        // Nearly Sorted
        Add("➡️ Single Element Moved", "Nearly Sorted", ArrayPatterns.GenerateSingleElementMoved, "One element moved from sorted array");
        Add("≈ Almost Sorted (5% Pair Swaps)", "Nearly Sorted", ArrayPatterns.GenerateAlmostSorted, "5% of pairs randomly swapped");
        Add("≈ Nearly Sorted (10% Random)", "Nearly Sorted", ArrayPatterns.GenerateNearlySorted, "10% of elements randomly swapped");
        Add("📍 Scrambled Tail (14% at End)", "Nearly Sorted", ArrayPatterns.GenerateScrambledTail, "Tail ~14% extracted and shuffled");
        Add("📍 Scrambled Head (14% at Start)", "Nearly Sorted", ArrayPatterns.GenerateScrambledHead, "Head ~14% extracted and shuffled");
        Add("🔊 Noisy (Block Shuffled)", "Nearly Sorted", ArrayPatterns.GenerateNoisy, "Small blocks shuffled");
        Add("🔢 Shuffled Odds Only", "Nearly Sorted", ArrayPatterns.GenerateShuffledOdds, "Only odd indices shuffled");
        Add("📊 Shuffled Half (Front Sorted)", "Nearly Sorted", ArrayPatterns.GenerateShuffledHalf, "Shuffled, then front half sorted");
        Add("🎲 Evens Reversed, Odds In-Order", "Nearly Sorted", (size, _) => ArrayPatterns.GenerateEvensReversedOddsInOrder(size), "Even values reversed, odd values in order");
        Add("🎲 Evens In-Order, Scrambled Odds", "Nearly Sorted", ArrayPatterns.GenerateEvensInOrderScrambledOdds, "Even values in order, odd values scrambled");
        Add("🔄 Double Layered (Symmetric Swap)", "Nearly Sorted", (size, _) => ArrayPatterns.GenerateDoubleLayered(size), "Even indices swapped symmetrically");

        // Merge Patterns
        Add("🔗 Final Merge (Even/Odd Sorted)", "Merge Patterns", (size, _) => ArrayPatterns.GenerateFinalMerge(size), "Even/odd indices sorted separately");
        Add("🔗 Shuffled Final Merge", "Merge Patterns", ArrayPatterns.GenerateShuffledFinalMerge, "Shuffled, then halves sorted separately");
        Add("⚙️ Sawtooth (4-way Interleaved)", "Merge Patterns", (size, _) => ArrayPatterns.GenerateSawtooth(size), "4-way interleaved sorted");

        // Partitioned
        Add("📐 Partitioned (Halves Shuffled)", "Partitioned", ArrayPatterns.GeneratePartitioned, "Sorted then halves shuffled");
        Add("📊 Half Sorted", "Partitioned", ArrayPatterns.GenerateHalfSorted, "Front half sorted, back half random");
        Add("↕️ Half Reversed", "Partitioned", (size, _) => ArrayPatterns.GenerateHalfReversed(size), "Back half reversed");

        // Shape
        Add("🎹 Pipe Organ", "Shape", (size, _) => ArrayPatterns.GeneratePipeOrgan(size), "Even values front, odd values back reversed");
        Add("🏞️ Valley Shape", "Shape", (size, _) => ArrayPatterns.GenerateValleyShape(size), "Minimum in center, maximum at edges");

        // Radix/Interleaved
        Add("🔢 Final Radix Pass", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateFinalRadix(size), "Even/odd values alternating");
        Add("🔢 Real Final Radix (Bitmask)", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateRealFinalRadix(size), "Bitmask-based radix pattern");
        Add("🔢 Recursive Final Radix", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateRecursiveFinalRadix(size), "Recursively applied radix pattern");
        Add("🔄 Final Bitonic Pass", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateFinalBitonicPass(size), "Reversed then pipe organ");
        Add("🔁 Bit Reversal (FFT)", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateBitReversal(size), "Bit-reversed order for FFT");
        Add("🧱 Block Randomly Shuffled", "Radix/Interleaved", ArrayPatterns.GenerateBlockRandomly, "Blocks shuffled randomly");
        Add("🧱 Block Reversed", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateBlockReverse(size), "Blocks reversed");
        Add("🔀 Interlaced", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateInterlaced(size), "Min at front, rest alternating from edges");
        Add("〰️ Zigzag Pattern", "Radix/Interleaved", (size, _) => ArrayPatterns.GenerateZigzag(size), "Alternating up and down");

        // Tree/Heap
        Add("🌳 BST In-Order Traversal", "Tree/Heap", ArrayPatterns.GenerateBstTraversal, "Random BST in-order traversal");
        Add("🌳 Inverted BST", "Tree/Heap", (size, _) => ArrayPatterns.GenerateInvertedBst(size), "Reverse level-order to in-order");
        Add("📈 Logarithmic Slopes", "Tree/Heap", (size, _) => ArrayPatterns.GenerateLogarithmicSlopes(size), "Powers of 2 based placement");
        Add("🔄 Half Rotation", "Tree/Heap", (size, _) => ArrayPatterns.GenerateHalfRotation(size), "Front and back halves swapped");
        Add("📚 Heapified (Max-Heap)", "Tree/Heap", (size, _) => ArrayPatterns.GenerateHeapified(size), "Max-heap structure");
        Add("📚 Poplar Heapified", "Tree/Heap", (size, _) => ArrayPatterns.GeneratePoplarHeapified(size), "Poplar heap structure");
        Add("📚 Triangular Heapified", "Tree/Heap", (size, _) => ArrayPatterns.GenerateTriangularHeapified(size), "Triangular heap structure");

        // Duplicates
        Add("🔢 Few Unique (16 Values)", "Duplicates", ArrayPatterns.GenerateFewUnique, "Only 16 unique values");
        Add("🔢 Many Duplicates (20%)", "Duplicates", ArrayPatterns.GenerateManyDuplicates, "Unique values ~20% of size");
        Add("🔢 Skewed Duplicates", "Duplicates", ArrayPatterns.GenerateSkewedDuplicates, "90% same value, rest unique");
        Add("⚪ All Equal", "Duplicates", (size, _) => ArrayPatterns.GenerateAllEqual(size), "All elements the same");

        // Distributions
        Add("📊 Quadratic (x²)", "Distributions", (size, _) => ArrayPatterns.GenerateQuadraticDistribution(size), "Quadratic curve distribution");
        Add("📊 Square Root (√x)", "Distributions", (size, _) => ArrayPatterns.GenerateSquareRootDistribution(size), "Square root distribution");
        Add("📊 Cubic (x³ Centered)", "Distributions", (size, _) => ArrayPatterns.GenerateCubicDistribution(size), "Cubic curve centered");
        Add("📊 Quintic (x⁵ Centered)", "Distributions", (size, _) => ArrayPatterns.GenerateQuinticDistribution(size), "Quintic curve centered");
        Add("📊 Cube Root (∛x)", "Distributions", (size, _) => ArrayPatterns.GenerateCubeRootDistribution(size), "Cube root distribution");
        Add("📊 Fifth Root (⁵√x)", "Distributions", (size, _) => ArrayPatterns.GenerateFifthRootDistribution(size), "Fifth root distribution");
        Add("〰️ Sine Wave", "Distributions", (size, _) => ArrayPatterns.GenerateSineWave(size), "Sine wave distribution");
        Add("〰️ Cosine Wave", "Distributions", (size, _) => ArrayPatterns.GenerateCosineWave(size), "Cosine wave distribution");
        Add("🔔 Bell Curve (Normal)", "Distributions", (size, _) => ArrayPatterns.GenerateBellCurve(size), "Normal distribution");
        Add("🌊 Perlin Noise Curve", "Distributions", ArrayPatterns.GeneratePerlinNoiseCurve, "Perlin noise distribution");
        Add("📐 Ruler Function", "Distributions", (size, _) => ArrayPatterns.GenerateRulerDistribution(size), "Ruler function distribution");
        Add("🍮 Blancmange Curve", "Distributions", (size, _) => ArrayPatterns.GenerateBlancmangeDistribution(size), "Blancmange curve distribution");
        Add("∞ Cantor Function", "Distributions", (size, _) => ArrayPatterns.GenerateCantorDistribution(size), "Cantor function distribution");
        Add("➗ Sum of Divisors", "Distributions", (size, _) => ArrayPatterns.GenerateDivisorsDistribution(size), "Sum of divisors distribution");
        Add("✈️ Fly Straight Dangit", "Distributions", (size, _) => ArrayPatterns.GenerateFsdDistribution(size), "FSD distribution (OEIS A133058)");
        Add("📉 Reverse Log", "Distributions", ArrayPatterns.GenerateReverseLogDistribution, "Reverse logarithmic distribution");
        Add("% Modulo Function", "Distributions", (size, _) => ArrayPatterns.GenerateModuloDistribution(size), "Modulo function distribution");
        Add("φ Euler Totient", "Distributions", (size, _) => ArrayPatterns.GenerateTotientDistribution(size), "Euler totient function distribution");

        // Advanced/Fractal
        Add("⭕ Circle Sort Pass", "Advanced/Fractal", ArrayPatterns.GenerateCirclePass, "One circle sort pass applied");
        Add("🔗 Pairwise Pass", "Advanced/Fractal", ArrayPatterns.GeneratePairwisePass, "Adjacent pairs sorted");
        Add("🔄 Recursive Reversal", "Advanced/Fractal", (size, _) => ArrayPatterns.GenerateRecursiveReversal(size), "Recursively reversed");
        Add("🔲 Gray Code Fractal", "Advanced/Fractal", (size, _) => ArrayPatterns.GenerateGrayCodeFractal(size), "Gray code pattern");
        Add("🔺 Sierpinski Triangle", "Advanced/Fractal", (size, _) => ArrayPatterns.GenerateSierpinskiTriangle(size), "Sierpinski triangle pattern");
        Add("🔻 Triangular", "Advanced/Fractal", (size, _) => ArrayPatterns.GenerateTriangular(size), "Triangular numbers");

        // Adversarial
        Add("⚔️ QuickSort Adversary", "Adversarial", (size, _) => ArrayPatterns.GenerateQuickSortAdversary(size), "Worst-case for QuickSort median-of-3");
        Add("⚔️ PDQ Adversary", "Adversarial", (size, _) => ArrayPatterns.GeneratePdqSortAdversary(size), "Worst-case for PDQSort");
        Add("⚔️ Grail Adversary", "Adversarial", ArrayPatterns.GenerateGrailSortAdversary, "Worst-case for GrailSort");
        Add("⚔️ ShuffleMerge Adversary", "Adversarial", (size, _) => ArrayPatterns.GenerateShuffleMergeAdversary(size), "Worst-case for ShuffleMerge");
        Add("⚔️ TimSortDrag Adversary", "Adversarial", (size, _) => ArrayPatterns.GenerateTimsortDragAdversary(size), "Worst-case for Timsort");
    }

    private void Add(string name, string category, Func<int, Random, int[]> generator, string description = "")
    {
        _patterns.Add(new ArrayPatternMetadata
        {
            Name = name,
            Category = category,
            Generator = generator,
            PatternId = ToId(name),
            Description = description
        });
    }

    private static string ToId(string name)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(name, @"[^a-zA-Z0-9]+")
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();
        if (parts.Length == 0) return string.Empty;
        return string.Concat(
            char.ToLowerInvariant(parts[0][0]) + (parts[0].Length > 1 ? parts[0][1..] : ""),
            parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : ""))
        );
    }
}

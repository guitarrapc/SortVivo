using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// Builder that converts a list of SortOperations into a list of TutorialSteps.
/// Generates array snapshots, highlight info, and narrative text for each operation.
/// </summary>
public static class TutorialStepBuilder
{
    /// <summary>
    /// Builds a list of TutorialSteps from the initial array and a list of operations.
    /// Detects insertion patterns (Read + shift Writes + insert Write) and groups them
    /// into a single logical step for clarity.
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations)
        => Build(initialArray, operations, TutorialVisualizationHint.None);

    /// <summary>
    /// Builds a list of TutorialSteps with optional visualization hint support.
    /// When <paramref name="hint"/> is <see cref="TutorialVisualizationHint.HeapTree"/>,
    /// tracks the heap boundary for heap tree rendering.
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations, TutorialVisualizationHint hint)
        => Build(initialArray, operations, hint, lsdRadix: 0);

    /// <summary>
    /// Builds a list of TutorialSteps with optional visualization hint and LSD radix support.
    /// <paramref name="lsdRadix"/> is used when <paramref name="hint"/> is
    /// <see cref="TutorialVisualizationHint.DigitBucketLsd"/> to select bucket digit computation:
    /// 10 = decimal (b=10), 4 = 2-bit groups (b=4).
    /// </summary>
    public static List<TutorialStep> Build(int[] initialArray, List<SortOperation> operations, TutorialVisualizationHint hint, int lsdRadix)
    {
        var steps = new List<TutorialStep>(operations.Count);
        var mainArray = (int[])initialArray.Clone();
        var bufferArrays = InitializeBufferArrays(initialArray.Length, operations);

        // Heap boundary tracking for HeapTree / TernaryHeapTree / WeakHeapTree visualization
        // HeapSort uses two extraction patterns:
        //   BottomupHeapSort/WeakHeapSort: Swap(0, i) — root swapped with last heap element
        //   HeapSort/TernaryHeapSort:      Read(0) + sift-down + Write(i, max) — root value written to end
        // WeakHeapSort additionally has reverse bits: every Merge swap flips r[max(i,j)].
        // Extraction swaps (root ↔ last) do NOT flip reverse bits.
        var trackHeap = hint is TutorialVisualizationHint.HeapTree
            or TutorialVisualizationHint.TernaryHeapTree
            or TutorialVisualizationHint.WeakHeapTree;
        var trackWeakHeap = hint == TutorialVisualizationHint.WeakHeapTree;
        int heapBoundary = trackHeap ? initialArray.Length : 0;
        bool heapBuildDone = false;
        // For HeapSort's Read+Write pattern: track the last Read(0) value
        int? pendingRootValue = null;
        // For WeakHeapSort: bit-parallel reverse bit array (r[i] = true → right child 2i+1 is distinguished)
        bool[] reverseBits = trackWeakHeap ? new bool[initialArray.Length] : [];

        // BST / AVL shadow-replay for BstTree / AvlTree visualization
        // BinaryTreeSort emits IndexRead × n (build) then IndexWrite × n (traversal).
        // We replay the BST/AVL insertion locally to reconstruct tree structure at each step.
        // AvlTree adds height tracking + rebalancing (RotateLeft/Right) after each insertion.
        var trackBst = hint == TutorialVisualizationHint.BstTree;
        var trackAvl = hint == TutorialVisualizationHint.AvlTree;
        var trackBstOrAvl = trackBst || trackAvl;
        int bstCap = initialArray.Length;
        int bstSize = 0;
        int bstRoot = -1;
        int[] bstValues = trackBstOrAvl ? new int[bstCap] : [];
        int[] bstLeft  = trackBstOrAvl ? Enumerable.Repeat(-1, bstCap).ToArray() : [];
        int[] bstRight = trackBstOrAvl ? Enumerable.Repeat(-1, bstCap).ToArray() : [];
        int[] bstInsertionPath = [];
        int   bstNewNode = -1;
        bool  bstIsTraversalPhase = false;
        int   bstActiveNode = -1;
        int[] bstInorderList = [];   // precomputed once all nodes are inserted
        // AVL-only state: height array, path stack for rebalancing, rotation info
        int[] avlHeight   = trackAvl ? new int[bstCap] : [];
        int[] avlPathBuf  = trackAvl ? new int[bstCap] : [];
        int[] avlRotatedNodes = [];
        string? avlRotationDesc = null;

        // Distribution bucket tracking for ValueBucket visualization
        // Pigeonhole sort emits: IndexRead(main,i) + IndexWrite(temp,i,v) × n (scatter)
        //                   then IndexRead(temp,j) + IndexWrite(main,pos,v) × n (gather).
        // We track logical buckets (value → bucket index) by deriving bucket assignments
        // from the value being written to temp and the minValue of the initial array.
        var trackDistribution = hint == TutorialVisualizationHint.ValueBucket;
        int distMinValue = 0;
        int distBucketCount = 0;
        string[] distBucketLabels = [];
        List<int>[] distBuckets = [];
        int[] distShadowTemp = [];       // shadow of temp buffer: distShadowTemp[pos] = value written there
        DistributionPhase distPhase = DistributionPhase.Scatter;
        int? distPendingGatherBucket = null;  // bucket index from most recent Read(temp), cleared by Write(main)
        if (trackDistribution)
        {
            distMinValue = initialArray.Length > 0 ? initialArray.Min() : 0;
            int distMaxValue = initialArray.Length > 0 ? initialArray.Max() : 0;
            distBucketCount = distMaxValue > distMinValue ? distMaxValue - distMinValue + 1 : 1;
            distBucketLabels = Enumerable.Range(distMinValue, distBucketCount).Select(v => v.ToString()).ToArray();
            distBuckets = Enumerable.Range(0, distBucketCount).Select(_ => new List<int>()).ToArray();
            distShadowTemp = new int[initialArray.Length];
        }

        // LSD Radix sort tracking for DigitBucketLsd visualization.
        // LSD b=10: each pass emits n × (Read(main,i)+Write(temp,pos,v)) then RangeCopy(temp→main).
        //           Pass boundary = RangeCopy(temp→main).
        //           Digit = (v - minValue) / 10^passIndex % 10.
        // LSD b=4:  uses ping-pong buffers. Pass 0: Read(main)+Write(temp), Pass 1: Read(temp)+Write(main), …
        //           Pass boundary = Read source buffer changes (main→temp or temp→main).
        //           Digit = ((uint)v ^ 0x8000_0000u) >> (passIndex * 2) & 0b11.
        //           Precedes passes with n key-building Reads from main (no Writes) — detected by !lsdPhaseReady.
        var trackLsd = hint == TutorialVisualizationHint.DigitBucketLsd;
        int lsdPassIndex = 0;
        int lsdPrevReadSourceId = -1;    // -1 = not yet seen any Read
        bool lsdPhaseReady = false;      // false until first Write is seen (skips key-building Reads in b=4)
        bool lsdClearBucketsAfterStep = false;  // set true for RangeCopy step in b=10; buckets cleared after adding step
        if (trackLsd)
        {
            int lsdBucketCount = lsdRadix > 0 ? lsdRadix : 10;
            distBucketCount = lsdBucketCount;
            distBucketLabels = Enumerable.Range(0, lsdBucketCount).Select(i => i.ToString()).ToArray();
            distMinValue = initialArray.Length > 0 ? initialArray.Min() : 0;
            distBuckets = Enumerable.Range(0, lsdBucketCount).Select(_ => new List<int>()).ToArray();
            distShadowTemp = new int[initialArray.Length];
            distPhase = DistributionPhase.Scatter;
        }

        int opIdx = 0;
        while (opIdx < operations.Count)
        {
            int groupEnd = TryDetectInsertionGroup(operations, opIdx, mainArray);

            if (groupEnd > opIdx)
            {
                // Grouped insertion step: Read + shifts + insert → 1 logical step
                var step = BuildGroupedInsertionStep(operations, opIdx, groupEnd, mainArray, bufferArrays);
                if (trackHeap) step = step with { HeapBoundary = heapBoundary };
                if (trackWeakHeap) step = step with { WeakHeapReverseBits = (bool[])reverseBits.Clone() };
                steps.Add(step);
                opIdx = groupEnd + 1;
            }
            else
            {
                // Individual step
                var op = operations[opIdx];

                // Track heap boundary: detect extraction events
                if (trackHeap && op.BufferId1 == 0)
                {
                    // Pattern 1: Swap(0, i) — BottomupHeapSort / WeakHeapSort extraction
                    if (op.Type == OperationType.Swap)
                    {
                        if (!heapBuildDone && (op.Index1 == 0 || op.Index2 == 0))
                            heapBuildDone = true;

                        int rootIdx = Math.Min(op.Index1, op.Index2);
                        int lastIdx = Math.Max(op.Index1, op.Index2);
                        bool isExtractionSwap = heapBuildDone && rootIdx == 0 && lastIdx == heapBoundary - 1;

                        if (isExtractionSwap)
                        {
                            heapBoundary--;
                            // Extraction swap: move root → sorted region, do NOT flip reverse bit
                        }
                        else if (trackWeakHeap)
                        {
                            // Merge swap: a[lastIdx] > a[rootIdx] was true → FlipBit(lastIdx)
                            reverseBits[lastIdx] = !reverseBits[lastIdx];
                        }
                    }
                    // Pattern 2: Read(0) then Write(heapBoundary-1, rootValue) — HeapSort extraction
                    else if (op.Type == OperationType.IndexRead && op.Index1 == 0)
                    {
                        if (!heapBuildDone)
                            heapBuildDone = true;
                        pendingRootValue = mainArray[0];
                    }
                    else if (op.Type == OperationType.IndexWrite && heapBuildDone
                        && pendingRootValue.HasValue && op.Value == pendingRootValue
                        && op.Index1 == heapBoundary - 1)
                    {
                        heapBoundary--;
                        pendingRootValue = null;
                    }
                }

                // Track BST / AVL state for BstTree / AvlTree visualization
                BstSnapshot? bstSnapshot = null;
                if (trackBstOrAvl && op.BufferId1 == 0)
                {
                    if (op.Type == OperationType.IndexRead)
                    {
                        // Create new node (common for BST and AVL)
                        int nodeId = bstSize++;
                        int value = mainArray[op.Index1];
                        bstValues[nodeId] = value;
                        bstLeft[nodeId] = bstRight[nodeId] = -1;
                        if (trackAvl) { avlHeight[nodeId] = 1; avlRotatedNodes = []; avlRotationDesc = null; }

                        // Navigate to insertion point (common path traversal for BST and AVL)
                        var path = new List<int>();
                        if (bstRoot == -1)
                        {
                            bstRoot = nodeId;
                        }
                        else
                        {
                            int cur = bstRoot;
                            int avlPathTop = 0;
                            while (true)
                            {
                                path.Add(cur);
                                if (trackAvl) avlPathBuf[avlPathTop++] = cur;
                                if (value < bstValues[cur])
                                {
                                    if (bstLeft[cur] == -1) { bstLeft[cur] = nodeId; break; }
                                    cur = bstLeft[cur];
                                }
                                else
                                {
                                    if (bstRight[cur] == -1) { bstRight[cur] = nodeId; break; }
                                    cur = bstRight[cur];
                                }
                            }

                            // AVL only: rebalance bottom-up along the insertion path
                            if (trackAvl)
                            {
                                var rotatedList = new List<int>();
                                var rotDescs = new List<string>();
                                int subtreeRoot = nodeId;
                                int subtreeFrom = nodeId;

                                while (avlPathTop > 0)
                                {
                                    int ni = avlPathBuf[--avlPathTop];
                                    if (bstLeft[ni] == subtreeFrom) bstLeft[ni] = subtreeRoot;
                                    else if (bstRight[ni] == subtreeFrom) bstRight[ni] = subtreeRoot;

                                    AvlUpdateHeight(ni, bstLeft, bstRight, avlHeight);
                                    var (newRoot, rotType) = AvlBalance(ni, bstLeft, bstRight, avlHeight);

                                    if (rotType != null)
                                    {
                                        rotDescs.Add($"{rotType} at {bstValues[ni]}");
                                        rotatedList.Add(ni);
                                        if (newRoot != ni) rotatedList.Add(newRoot);
                                    }
                                    subtreeFrom = ni;
                                    subtreeRoot = newRoot;
                                }

                                bstRoot = subtreeRoot;
                                avlRotatedNodes = [.. rotatedList.Distinct()];
                                avlRotationDesc = rotDescs.Count > 0 ? string.Join("; ", rotDescs) : null;
                            }
                        }
                        bstInsertionPath = [.. path];
                        bstNewNode = nodeId;
                    }
                    else if (op.Type == OperationType.IndexWrite)
                    {
                        if (!bstIsTraversalPhase)
                        {
                            bstIsTraversalPhase = true;
                            bstInorderList = BstComputeInorder(bstRoot, bstLeft, bstRight, bstSize);
                            bstInsertionPath = [];
                            bstNewNode = -1;
                            if (trackAvl) { avlRotatedNodes = []; avlRotationDesc = null; }
                        }
                        bstActiveNode = op.Index1 < bstInorderList.Length ? bstInorderList[op.Index1] : -1;
                    }

                    bstSnapshot = new BstSnapshot
                    {
                        Size = bstSize,
                        Root = bstRoot,
                        Values = bstValues[..bstSize],
                        Left = bstLeft[..bstSize],
                        Right = bstRight[..bstSize],
                        InsertionPath = bstInsertionPath,
                        NewNode = bstNewNode,
                        ActiveNode = bstActiveNode,
                        IsTraversalPhase = bstIsTraversalPhase,
                        Heights = trackAvl ? avlHeight[..bstSize] : null,
                        RotatedNodes = trackAvl ? avlRotatedNodes : []
                    };
                }

                // Track Distribution state for ValueBucket visualization (Pigeonhole sort)
                // Operations are processed BEFORE ApplyOperation so that bucket state matches the snapshot.
                DistributionSnapshot? distSnapshot = null;
                int distActiveBucket = -1;
                int distActiveElement = -1;
                if (trackDistribution)
                {
                    if (op.Type == OperationType.IndexWrite && op.BufferId1 == 1 && op.Value.HasValue)
                    {
                        // Scatter: Write(temp, pos, v) — element enters its logical bucket
                        int v = op.Value.Value;
                        int bIdx = v - distMinValue;
                        if ((uint)bIdx < (uint)distBucketCount)
                        {
                            distBuckets[bIdx].Add(v);
                            if (op.Index1 < distShadowTemp.Length)
                                distShadowTemp[op.Index1] = v;
                            distActiveBucket = bIdx;
                            distActiveElement = distBuckets[bIdx].Count - 1;
                            distPhase = DistributionPhase.Scatter;
                        }
                    }
                    else if (op.Type == OperationType.IndexRead && op.BufferId1 == 1)
                    {
                        // Gather: Read(temp, j) — identify which bucket this gather is from
                        if (op.Index1 >= 0 && op.Index1 < distShadowTemp.Length)
                        {
                            int v = distShadowTemp[op.Index1];
                            int bIdx = v - distMinValue;
                            if ((uint)bIdx < (uint)distBucketCount)
                            {
                                distActiveBucket = bIdx;
                                distActiveElement = distBuckets[bIdx].Count - 1;
                                distPendingGatherBucket = bIdx;
                                distPhase = DistributionPhase.Gather;
                            }
                        }
                    }
                    else if (op.Type == OperationType.IndexWrite && op.BufferId1 == 0 && distPendingGatherBucket.HasValue)
                    {
                        // Gather: Write(main, pos, v) — element leaves its bucket
                        int bIdx = distPendingGatherBucket.Value;
                        if (distBuckets[bIdx].Count > 0)
                            distBuckets[bIdx].RemoveAt(distBuckets[bIdx].Count - 1);
                        distActiveBucket = bIdx;
                        distActiveElement = -1;
                        distPendingGatherBucket = null;
                        distPhase = DistributionPhase.Gather;
                    }

                    distSnapshot = new DistributionSnapshot
                    {
                        BucketCount = distBucketCount,
                        BucketLabels = distBucketLabels,
                        Buckets = distBuckets.Select(b => b.ToArray()).ToArray(),
                        Phase = distPhase,
                        ActiveBucketIndex = distActiveBucket,
                        ActiveElementInBucket = distActiveElement,
                    };
                }

                // Track LSD Radix sort state for DigitBucketLsd visualization.
                if (trackLsd)
                {
                    if (op.Type == OperationType.IndexRead)
                    {
                        // Detect pass boundary in b=4: Read source changes between passes
                        if (lsdPhaseReady && lsdPrevReadSourceId >= 0 && op.BufferId1 != lsdPrevReadSourceId)
                        {
                            // Source changed → end of previous pass, clear buckets for new pass
                            foreach (var b in distBuckets) b.Clear();
                            lsdPassIndex++;
                        }
                        if (lsdPhaseReady)
                            lsdPrevReadSourceId = op.BufferId1;
                        else
                            lsdPrevReadSourceId = op.BufferId1;  // track even before first Write
                        distActiveBucket = -1;
                        distActiveElement = -1;
                    }
                    else if (op.Type == OperationType.IndexWrite && op.Value.HasValue)
                    {
                        // Scatter Write (to either main or temp, depending on pass)
                        lsdPhaseReady = true;
                        int v = op.Value.Value;
                        int digit = ComputeLsdDigit(v, lsdPassIndex, lsdRadix, distMinValue);
                        if ((uint)digit < (uint)distBucketCount)
                        {
                            distBuckets[digit].Add(v);
                            if (op.BufferId1 == 1 && op.Index1 < distShadowTemp.Length)
                                distShadowTemp[op.Index1] = v;
                            distActiveBucket = digit;
                            distActiveElement = distBuckets[digit].Count - 1;
                        }
                        distPhase = DistributionPhase.Scatter;
                    }
                    else if (op.Type == OperationType.RangeCopy && op.BufferId1 == 1 && op.BufferId2 == 0)
                    {
                        // b=10 pass end: RangeCopy(temp→main) = gather all buckets at once
                        distActiveBucket = -1;
                        distPhase = DistributionPhase.Gather;
                        lsdClearBucketsAfterStep = true;
                    }

                    distSnapshot = new DistributionSnapshot
                    {
                        BucketCount = distBucketCount,
                        BucketLabels = distBucketLabels,
                        Buckets = distBuckets.Select(b => b.ToArray()).ToArray(),
                        Phase = distPhase,
                        ActiveBucketIndex = distActiveBucket,
                        ActiveElementInBucket = distActiveElement,
                        PassIndex = lsdPassIndex,
                        PassLabel = GetLsdPassLabel(lsdPassIndex, lsdRadix),
                    };
                }

                var (highlights, bufferHighlights, highlightType, compareResult, writeSourceIndex, writePreviousValue, narrative) =
                    GenerateStepInfo(op, mainArray, bufferArrays);

                // Override narrative with BST / AVL-specific text
                if (bstSnapshot != null && op.BufferId1 == 0)
                {
                    bool isAvl = trackAvl;
                    narrative = op.Type switch
                    {
                        OperationType.IndexRead when bstSnapshot.Root == bstSnapshot.NewNode
                            => isAvl
                                ? $"Insert {bstSnapshot.Values[bstSnapshot.NewNode]} as AVL root"
                                : $"Insert {bstSnapshot.Values[bstSnapshot.NewNode]} as BST root",
                        OperationType.IndexRead when bstSnapshot.NewNode >= 0 && avlRotationDesc != null
                            => $"Insert {bstSnapshot.Values[bstSnapshot.NewNode]} at depth {bstInsertionPath.Length} — {avlRotationDesc}",
                        OperationType.IndexRead when bstSnapshot.NewNode >= 0
                            => isAvl
                                ? $"Insert {bstSnapshot.Values[bstSnapshot.NewNode]} into AVL tree at depth {bstInsertionPath.Length}"
                                : $"Insert {bstSnapshot.Values[bstSnapshot.NewNode]} into BST at depth {bstInsertionPath.Length}",
                        OperationType.IndexWrite
                            => $"In-order traversal: write {op.Value} to index {op.Index1}",
                        _ => narrative
                    };
                }

                // Override narrative with Distribution-specific text (Pigeonhole)
                if (distSnapshot != null && trackDistribution)
                {
                    narrative = (op.Type, op.BufferId1) switch
                    {
                        (OperationType.IndexRead, 0) when distPhase == DistributionPhase.Scatter
                            => $"Read value {GetValue(0, op.Index1, mainArray, bufferArrays)} from index {op.Index1}",
                        (OperationType.IndexWrite, 1) when op.Value.HasValue && distActiveBucket >= 0
                            => $"Scatter value {op.Value.Value} into bucket [{distBucketLabels[distActiveBucket]}]",
                        (OperationType.IndexRead, 1) when distActiveBucket >= 0 && op.Index1 < distShadowTemp.Length
                            => $"Pick up value {distShadowTemp[op.Index1]} from bucket [{distBucketLabels[distActiveBucket]}]",
                        (OperationType.IndexWrite, 0) when distPhase == DistributionPhase.Gather && distActiveBucket >= 0
                            => $"Place value {op.Value!.Value} from bucket [{distBucketLabels[distActiveBucket]}] to index {op.Index1}",
                        _ => narrative
                    };
                }

                // Override narrative with LSD-specific text
                if (distSnapshot != null && trackLsd)
                {
                    string passLabel = GetLsdPassLabel(lsdPassIndex, lsdRadix);
                    narrative = (op.Type, op.Value.HasValue) switch
                    {
                        (OperationType.IndexRead, _) when !lsdPhaseReady
                            => $"Pre-compute key for value {GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays)} at index {op.Index1}",
                        (OperationType.IndexRead, _)
                            => $"Read value {GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays)} from index {op.Index1} ({passLabel})",
                        (OperationType.IndexWrite, true) when distActiveBucket >= 0
                            => $"Scatter value {op.Value!.Value} into digit bucket [{distBucketLabels[distActiveBucket]}] ({passLabel})",
                        (OperationType.RangeCopy, _)
                            => $"Gather all buckets back to main array — pass {lsdPassIndex + 1} complete",
                        _ => narrative
                    };
                }

                ApplyOperation(op, mainArray, bufferArrays);

                steps.Add(new TutorialStep
                {
                    OperationIndex = opIdx,
                    ArraySnapshot = (int[])mainArray.Clone(),
                    BufferSnapshots = bufferArrays.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone()),
                    HighlightIndices = highlights,
                    BufferHighlightIndices = bufferHighlights,
                    HighlightType = highlightType,
                    CompareResult = compareResult,
                    WriteSourceIndex = writeSourceIndex,
                    WritePreviousValue = writePreviousValue,
                    Narrative = narrative,
                    HeapBoundary = trackHeap ? heapBoundary : null,
                    WeakHeapReverseBits = trackWeakHeap ? (bool[])reverseBits.Clone() : null,
                    Bst = bstSnapshot,
                    Distribution = distSnapshot,
                });

                // Post-step: clear LSD buckets after b=10 RangeCopy gather step
                if (lsdClearBucketsAfterStep)
                {
                    foreach (var b in distBuckets) b.Clear();
                    lsdPassIndex++;
                    lsdClearBucketsAfterStep = false;
                }

                opIdx++;
            }
        }

        return steps;
    }

    // ─── Insertion group detection ──────────────────────────────────────────

    /// <summary>
    /// Detects an insertion group pattern starting at <paramref name="startIdx"/>:
    /// IndexRead followed by Compare/IndexWrite operations, ending with an IndexWrite
    /// that writes the same value as the initial Read to a different position.
    /// Returns the last operation index of the group, or <paramref name="startIdx"/> if no group.
    /// </summary>
    private static int TryDetectInsertionGroup(List<SortOperation> operations, int startIdx, int[] mainArray)
    {
        var firstOp = operations[startIdx];

        // Must start with IndexRead on main array
        if (firstOp.Type != OperationType.IndexRead || firstOp.BufferId1 != 0
            || firstOp.Index1 < 0 || firstOp.Index1 >= mainArray.Length)
            return startIdx;

        int readValue = mainArray[firstOp.Index1];

        for (int i = startIdx + 1; i < operations.Count; i++)
        {
            var op = operations[i];

            // Found the final insertion write?
            if (op.Type == OperationType.IndexWrite && op.BufferId1 == 0
                && op.Value == readValue && op.Index1 != firstOp.Index1)
            {
                // Need at least: Read + 1 intermediate + Insert = 3 operations
                return i - startIdx >= 2 ? i : startIdx;
            }

            // Allow Compare and IndexWrite (shift) in between
            if (op.Type is not (OperationType.Compare or OperationType.IndexWrite))
                break;

            // Only main array writes
            if (op.Type == OperationType.IndexWrite && op.BufferId1 != 0)
                break;
        }

        return startIdx;
    }

    /// <summary>
    /// Builds a single TutorialStep for a grouped insertion operation.
    /// Applies all operations in [startIdx..endIdx] to mainArray and creates a snapshot.
    /// </summary>
    private static TutorialStep BuildGroupedInsertionStep(
        List<SortOperation> operations, int startIdx, int endIdx,
        int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        var readOp = operations[startIdx];
        var insertOp = operations[endIdx];
        int readValue = mainArray[readOp.Index1];
        int sourceIndex = readOp.Index1;
        int destIndex = insertOp.Index1;

        // Count intermediate shift writes
        int shiftCount = 0;
        for (int i = startIdx + 1; i < endIdx; i++)
        {
            if (operations[i].Type == OperationType.IndexWrite)
                shiftCount++;
        }

        string narrative = $"Insert value {readValue}: move from index {sourceIndex} to index {destIndex}";
        if (shiftCount > 0)
            narrative += $" (shifting {shiftCount} element{(shiftCount != 1 ? "s" : "")} right)";

        // Apply all operations in the group to advance main state
        for (int i = startIdx; i <= endIdx; i++)
            ApplyOperation(operations[i], mainArray, bufferArrays);

        return new TutorialStep
        {
            OperationIndex = endIdx,
            ArraySnapshot = (int[])mainArray.Clone(),
            BufferSnapshots = bufferArrays.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone()),
            HighlightIndices = [destIndex],
            BufferHighlightIndices = new Dictionary<int, int[]>(),
            HighlightType = OperationType.IndexWrite,
            WriteSourceIndex = sourceIndex,
            Narrative = narrative
        };
    }

    // ─── Buffer initialization ──────────────────────────────────────────────

    private static Dictionary<int, int[]> InitializeBufferArrays(int mainArrayLength, List<SortOperation> operations)
    {
        var maxSizes = new Dictionary<int, int>();

        foreach (var op in operations)
        {
            if (op.BufferId1 > 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index1 + op.Length
                    : op.Index1 + 1;
                maxSizes[op.BufferId1] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId1), size);
            }

            if (op.BufferId2 > 0)
            {
                int size = op.Type == OperationType.RangeCopy
                    ? op.Index2 + op.Length
                    : op.Index2 + 1;
                maxSizes[op.BufferId2] = Math.Max(maxSizes.GetValueOrDefault(op.BufferId2), size);
            }
        }

        // Ensure buffers are at least as large as the main array to prevent index out-of-range
        return maxSizes.ToDictionary(
            kv => kv.Key,
            kv => new int[Math.Max(kv.Value, mainArrayLength)]);
    }

    // ─── Step info generation ──────────────────────────────────────────────

    private static (int[] highlights, Dictionary<int, int[]> bufferHighlights, OperationType type, int? compareResult, int? writeSourceIndex, int? writePreviousValue, string narrative)
        GenerateStepInfo(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => op.Type switch
        {
            OperationType.Compare   => BuildCompareInfo(op, mainArray, bufferArrays),
            OperationType.Swap      => BuildSwapInfo(op, mainArray, bufferArrays),
            OperationType.IndexRead => BuildIndexReadInfo(op, mainArray, bufferArrays),
            OperationType.IndexWrite => BuildIndexWriteInfo(op, mainArray, bufferArrays),
            OperationType.RangeCopy => BuildRangeCopyInfo(op),
            _ => ([], new Dictionary<int, int[]>(), OperationType.Compare, null, null, null, string.Empty)
        };

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildCompareInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int vi = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        int vj = GetValue(op.BufferId2, op.Index2, mainArray, bufferArrays);
        string loc1 = FormatLocation(op.BufferId1, op.Index1);
        string loc2 = FormatLocation(op.BufferId2, op.Index2);

        string resultText = op.CompareResult > 0
            ? $"{vi} > {vj} → out of order, swap needed"
            : op.CompareResult < 0
                ? $"{vi} < {vj} → already in order"
                : $"{vi} = {vj} → equal, no swap needed";

        string narrative = $"Compare {loc1} ({vi}) and {loc2} ({vj}): {resultText}";

        int[] highlights = op.BufferId1 == 0 && op.BufferId2 == 0
            ? [op.Index1, op.Index2]
            : op.BufferId1 == 0 ? [op.Index1] : [];

        var bufHighlights = new Dictionary<int, int[]>();
        AddBufferHighlight(bufHighlights, op.BufferId1, op.Index1);
        AddBufferHighlight(bufHighlights, op.BufferId2, op.Index2);

        return (highlights, bufHighlights, OperationType.Compare, op.CompareResult, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildSwapInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int vi = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        int vj = GetValue(op.BufferId1, op.Index2, mainArray, bufferArrays);

        string narrative = $"Swap value {vi} at index {op.Index1} with value {vj} at index {op.Index2}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1, op.Index2] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1, op.Index2];

        return (highlights, bufHighlights, OperationType.Swap, null, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildIndexReadInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        int v = GetValue(op.BufferId1, op.Index1, mainArray, bufferArrays);
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string narrative = $"Read value {v} from {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexRead, null, null, null, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildIndexWriteInfo(
        SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        string loc = FormatLocation(op.BufferId1, op.Index1);
        string valStr = op.Value.HasValue ? op.Value.Value.ToString() : "?";

        // Compute previous value at write destination
        int[] destArr = GetArray(op.BufferId1, mainArray, bufferArrays);
        int? previousValue = op.Index1 >= 0 && op.Index1 < destArr.Length ? destArr[op.Index1] : null;

        // Find source: where was the written value in the same array before this write?
        int? sourceIndex = null;
        if (op.Value.HasValue && op.BufferId1 == 0)
        {
            int writeVal = op.Value.Value;
            for (int k = 0; k < mainArray.Length; k++)
            {
                if (k != op.Index1 && mainArray[k] == writeVal)
                {
                    sourceIndex = k;
                    break;
                }
            }
        }

        string narrative = sourceIndex.HasValue
            ? $"Write value {valStr} from index {sourceIndex.Value} to {loc}"
            : $"Write value {valStr} to {loc}";

        int[] highlights = op.BufferId1 == 0 ? [op.Index1] : [];
        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = [op.Index1];

        return (highlights, bufHighlights, OperationType.IndexWrite, null, sourceIndex, previousValue, narrative);
    }

    private static (int[], Dictionary<int, int[]>, OperationType, int?, int?, int?, string) BuildRangeCopyInfo(SortOperation op)
    {
        string srcName = op.BufferId1 == 0 ? "main array" : $"buffer {op.BufferId1}";
        string dstName = op.BufferId2 == 0 ? "main array" : $"buffer {op.BufferId2}";
        int srcEnd = op.Index1 + op.Length - 1;

        string narrative = op.Length == 1
            ? $"Copy value at index {op.Index1} of {srcName} to index {op.Index2} of {dstName}"
            : $"Copy {op.Length} elements ({op.Index1}–{srcEnd}) from {srcName} to index {op.Index2} of {dstName}";

        // Highlight source side (read)
        int[] highlights = op.BufferId1 == 0
            ? Enumerable.Range(op.Index1, op.Length).ToArray()
            : [];

        var bufHighlights = new Dictionary<int, int[]>();
        if (op.BufferId1 != 0)
            bufHighlights[op.BufferId1] = Enumerable.Range(op.Index1, op.Length).ToArray();
        if (op.BufferId2 != 0)
        {
            var destRange = Enumerable.Range(op.Index2, op.Length).ToArray();
            if (bufHighlights.TryGetValue(op.BufferId2, out var existing))
                bufHighlights[op.BufferId2] = [.. existing, .. destRange];
            else
                bufHighlights[op.BufferId2] = destRange;
        }

        return (highlights, bufHighlights, OperationType.RangeCopy, null, null, null, narrative);
    }

    // ─── Apply operation ─────────────────────────────────────────────────

    private static void ApplyOperation(SortOperation op, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        switch (op.Type)
        {
            case OperationType.Compare:
            case OperationType.IndexRead:
                break;

            case OperationType.Swap:
            {
                int[] arr = GetArray(op.BufferId1, mainArray, bufferArrays);
                if (op.Index1 < arr.Length && op.Index2 < arr.Length)
                    (arr[op.Index1], arr[op.Index2]) = (arr[op.Index2], arr[op.Index1]);
                break;
            }

            case OperationType.IndexWrite:
            {
                if (op.Value.HasValue)
                {
                    int[] arr = GetArray(op.BufferId1, mainArray, bufferArrays);
                    if (op.Index1 < arr.Length)
                        arr[op.Index1] = op.Value.Value;
                }
                break;
            }

            case OperationType.RangeCopy:
            {
                int[] destArr = GetArray(op.BufferId2, mainArray, bufferArrays);

                if (op.Values != null)
                {
                    for (int k = 0; k < op.Length && k < op.Values.Length; k++)
                    {
                        int destIdx = op.Index2 + k;
                        if (destIdx < destArr.Length)
                            destArr[destIdx] = op.Values[k];
                    }
                }
                else
                {
                    // Values is null: copy directly from the source array
                    int[] srcArr = GetArray(op.BufferId1, mainArray, bufferArrays);
                    for (int k = 0; k < op.Length; k++)
                    {
                        int srcIdx = op.Index1 + k;
                        int destIdx = op.Index2 + k;
                        if (srcIdx < srcArr.Length && destIdx < destArr.Length)
                            destArr[destIdx] = srcArr[srcIdx];
                    }
                }
                break;
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static int[] GetArray(int bufferId, int[] mainArray, Dictionary<int, int[]> bufferArrays)
        => bufferId == 0 ? mainArray : bufferArrays.GetValueOrDefault(bufferId, mainArray);

    private static int GetValue(int bufferId, int index, int[] mainArray, Dictionary<int, int[]> bufferArrays)
    {
        if (index < 0) return 0;
        var arr = GetArray(bufferId, mainArray, bufferArrays);
        return index < arr.Length ? arr[index] : 0;
    }

    private static string FormatLocation(int bufferId, int index)
        => index < 0 ? "temp"
        : bufferId == 0 ? $"index {index}" : $"buffer index {index}";

    private static void AddBufferHighlight(Dictionary<int, int[]> dict, int bufferId, int index)
    {
        if (bufferId == 0) return;
        if (dict.TryGetValue(bufferId, out var existing))
            dict[bufferId] = [.. existing, index];
        else
            dict[bufferId] = [index];
    }

    // ─── BST helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// 非再帰の中順走査で BST の全ノードを訪問順に列挙する。
    /// BinaryTreeSort の走査フェーズで「何番目の IndexWrite がどのノードか」を決定するために使用。
    /// </summary>
    private static int[] BstComputeInorder(int root, int[] left, int[] right, int size)
    {
        var result = new List<int>(size);
        var stack = new Stack<int>();
        int cur = root;
        while (cur != -1 || stack.Count > 0)
        {
            while (cur != -1) { stack.Push(cur); cur = left[cur]; }
            cur = stack.Pop();
            result.Add(cur);
            cur = right[cur];
        }
        return [.. result];
    }

    // ─── AVL helpers ─────────────────────────────────────────────────────
    // These mirror BalancedBinaryTreeSort's arena-based UpdateHeight / GetBalance / Rotate / Balance.
    // Arrays left[], right[], height[] are mutated in-place (same as the production code).

    private static void AvlUpdateHeight(int i, int[] left, int[] right, int[] height)
    {
        int lh = left[i]  >= 0 ? height[left[i]]  : 0;
        int rh = right[i] >= 0 ? height[right[i]] : 0;
        height[i] = 1 + Math.Max(lh, rh);
    }

    private static int AvlGetBalance(int i, int[] left, int[] right, int[] height)
    {
        int lh = left[i]  >= 0 ? height[left[i]]  : 0;
        int rh = right[i] >= 0 ? height[right[i]] : 0;
        return lh - rh;
    }

    /// <summary>Right rotation around y. Returns new subtree root (x = y.Left before rotation).</summary>
    private static int AvlRotateRight(int y, int[] left, int[] right, int[] height)
    {
        int x  = left[y];
        int t2 = right[x];
        right[x] = y;
        left[y]  = t2;
        AvlUpdateHeight(y, left, right, height);
        AvlUpdateHeight(x, left, right, height);
        return x;
    }

    // ─── LSD helpers ─────────────────────────────────────────────────────

    private static readonly uint[] Pow10 =
    [
        1u, 10u, 100u, 1_000u, 10_000u, 100_000u, 1_000_000u, 10_000_000u, 100_000_000u, 1_000_000_000u
    ];

    /// <summary>
    /// LSD ソート用の桁インデックスを計算する。
    /// radix=10: (v - minValue) / 10^passIndex % 10 （正整数に対して符号ビット反転後も同値）
    /// radix=4:  ((uint)v ^ 0x8000_0000) >> (passIndex * 2) &amp; 0b11 （符号ビット反転で符号なし比較）
    /// </summary>
    private static int ComputeLsdDigit(int v, int passIndex, int radix, int minValue)
    {
        if (radix == 4)
        {
            // LSD b=4 uses raw sign-bit-flipped key (no minValue normalization)
            uint key = (uint)v ^ 0x8000_0000u;
            int shift = passIndex * 2;
            return (int)((key >> shift) & 0b11u);
        }
        else // radix == 10 (default)
        {
            uint normalized = (uint)v - (uint)minValue;
            uint divisor = (uint)passIndex < (uint)Pow10.Length ? Pow10[passIndex] : 1u;
            return (int)((normalized / divisor) % 10u);
        }
    }

    /// <summary>
    /// パスインデックスと基数からパスラベル文字列を生成する。
    /// radix=10: "ones digit" / "tens digit" / ...
    /// radix=4:  "bits 0-1" / "bits 2-3" / ...
    /// </summary>
    private static string GetLsdPassLabel(int passIndex, int radix)
    {
        if (radix == 4)
        {
            int startBit = passIndex * 2;
            int endBit = startBit + 1;
            return $"bits {startBit}-{endBit}";
        }
        return passIndex switch
        {
            0 => "ones digit",
            1 => "tens digit",
            2 => "hundreds digit",
            3 => "thousands digit",
            _ => $"10^{passIndex} digit"
        };
    }

    /// <summary>Left rotation around x. Returns new subtree root (y = x.Right before rotation).</summary>
    private static int AvlRotateLeft(int x, int[] left, int[] right, int[] height)
    {
        int y  = right[x];
        int t2 = left[y];
        left[y]  = x;
        right[x] = t2;
        AvlUpdateHeight(x, left, right, height);
        AvlUpdateHeight(y, left, right, height);
        return y;
    }

    /// <summary>
    /// Rebalance the subtree rooted at node. Mirrors BalancedBinaryTreeSort.Balance().
    /// Returns (newRoot, rotationType) where rotationType is "LL", "RR", "LR", "RL" or null (no rotation).
    /// </summary>
    private static (int newRoot, string? rotationType) AvlBalance(int node, int[] left, int[] right, int[] height)
    {
        int bf = AvlGetBalance(node, left, right, height);

        if (bf > 1)
        {
            int li = left[node];
            string rotType;
            if (AvlGetBalance(li, left, right, height) < 0)
            {
                left[node] = AvlRotateLeft(li, left, right, height);
                rotType = "LR";
            }
            else
            {
                rotType = "LL";
            }
            return (AvlRotateRight(node, left, right, height), rotType);
        }

        if (bf < -1)
        {
            int ri = right[node];
            string rotType;
            if (AvlGetBalance(ri, left, right, height) > 0)
            {
                right[node] = AvlRotateRight(ri, left, right, height);
                rotType = "RL";
            }
            else
            {
                rotType = "RR";
            }
            return (AvlRotateLeft(node, left, right, height), rotType);
        }

        return (node, null);
    }
}

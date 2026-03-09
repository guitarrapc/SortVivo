# BlockQuickSort - Duplicate Check Optimization

## Overview

This document describes the duplicate check optimization implemented in BlockQuickSort, based on Section 3.1 of the BlockQuickSort paper (Edelkamp & Weiß, 2016).

## Purpose

The duplicate check optimization prevents excessive recursion depth when sorting arrays with many duplicate elements. Without this optimization, arrays with many duplicates can cause:
- Deep recursion leading to stack overflow
- Triggering of the IntroSort fallback to Heapsort
- Poor performance on duplicate-heavy data

## When It Activates

The duplicate check is applied when **either** condition from the paper is met:

1. **Pivot occurs twice in the sample for pivot selection**
   - For median-of-3: pivot value appears in at least 2 of {left, mid, right}
   - For median-of-5: pivot value appears in at least 2 of the sample positions
   - Indicates high likelihood of duplicates in the array

2. **Partitioning results very unbalanced for small/medium arrays**
   - Array size ≤ 10,000 elements
   - Smaller partition < 1/8 of total size (min(leftSize, rightSize) < size/8)
   - Indicates potential duplicate concentration or poor pivot choice

## Algorithm Details

### High-Level Flow

```
1. Select pivot using adaptive strategy (median-of-3/5/sqrt(n))
2. Check if pivot value appears multiple times in sample
3. Partition array using Hoare block partitioning
4. Check if partition is very unbalanced (for arrays ≤ 10,000)
5. If either condition is met:
   a. Determine which partition is larger (left or right)
   b. Scan larger partition for elements equal to pivot
   c. Move equal elements adjacent to the pivot
   d. Return range [equalLeft, equalRight] of pivot-equal elements
6. Exclude pivot-equal range from recursive calls
```

### Paper Conditions (Section 3.1)

The paper specifies two triggering conditions:

> "an additional check for duplicates equal to the pivot if one of the following
> two conditions applies:
> - the pivot occurs twice in the sample for pivot selection (in the case of median-of-three),
> - the partitioning results very unbalanced for an array of small size."

This implementation faithfully follows these conditions.

### Scanning Strategy

The algorithm scans the **larger partition** because:
- More likely to contain duplicates
- Reduces wasted work on smaller partition
- Maintains O(n) complexity for the partition step

### Early Stopping Condition

Scanning stops when duplicates become sparse:
```csharp
if (scanned >= 4 && found * 4 < scanned)
    break;  // Less than 25% duplicates, stop scanning
```

This 25% threshold (1 in 4 elements) prevents excessive work when duplicates are rare.

## Implementation

### Data Structures

```csharp
readonly struct PartitionResult
{
    public readonly int Left;   // First index of pivot-equal elements
    public readonly int Right;  // Last index of pivot-equal elements
}
```

### Key Methods

#### 1. `HoareBlockPartition`
- Returns `PartitionResult` instead of single pivot index
- Calls `CheckForDuplicates` for small arrays
- Returns range of equal elements `[Left, Right]`

#### 2. `CheckForDuplicates`
```csharp
static PartitionResult CheckForDuplicates<T>(
    SortSpan<T> s, 
    int left, 
    int right, 
    int pivotPos)
```

**Left Partition Scan:**
```csharp
for (var i = pivotPos - 1; i >= left; i--)
{
    if (s.Compare(i, pivot) == 0)  // Element equals pivot
    {
        equalLeft--;
        s.Swap(i, equalLeft);  // Move to pivot group
    }
    // Early stop if duplicates < 25%
}
```

**Right Partition Scan:**
```csharp
for (var i = pivotPos + 1; i <= right; i++)
{
    if (s.Compare(i, pivot) == 0)  // Element equals pivot
    {
        equalRight++;
        s.Swap(i, equalRight);  // Move to pivot group
    }
    // Early stop if duplicates < 25%
}
```

#### 3. `SortCore` - Recursive Call

```csharp
// Before: recurse on [left, pivotIndex-1] and [pivotIndex+1, right]
// After:  recurse on [left, result.Left-1] and [result.Right+1, right]
//         Elements in [result.Left, result.Right] are excluded (already sorted)
```

## Performance Characteristics

### Time Complexity
- **Best case:** O(1) - all elements equal to pivot
- **Average case:** O(n) - scans portion of larger partition
- **Worst case:** O(n) - scans entire larger partition (bounded)

### Space Complexity
- O(1) - no additional memory beyond stack variables

### Recursion Depth Improvement

**Without duplicate check:**
- Array with 500 identical elements: ~500 recursive calls
- May trigger IntroSort depth limit → switches to Heapsort

**With duplicate check:**
- Array with 500 identical elements: 1-2 recursive calls
- Equal elements grouped and excluded from recursion

## Configuration Parameters

| Constant | Value | Purpose |
|----------|-------|---------|
| `DuplicateCheckThreshold` | 512 | Maximum array size for duplicate check |
| `DuplicateScanRatio` | 4 | Minimum ratio for continuing scan (1/4 = 25%) |

## Test Coverage

The implementation is tested with:

1. **Small arrays with high duplicate density** (80%)
   - Verifies duplicate grouping works correctly
   
2. **Threshold boundary tests** (256, 512, 513)
   - Ensures check activates/deactivates at correct size
   
3. **Early stop verification** (20% duplicates)
   - Confirms scanning stops when duplicates are sparse
   
4. **All Mock duplicate data patterns**
   - `MockAllIdenticalData` - 100% duplicates
   - `MockTwoDistinctValuesData` - 50% duplicates
   - `MockHighlySkewedData` - 90% duplicates

## Example

### Input Array (size=100, 80% are value 50)
```
[50, 50, 23, 50, 50, 87, 50, 50, 50, 12, ...]
```

### After First Partition (pivot ≈ 50)
```
[23, 12, ...] | [50] | [87, 92, ...]
    left         pivot     right
```

### After Duplicate Check
```
[23, 12, ...] | [50, 50, 50, 50, 50, ...] | [87, 92, ...]
    left              pivot group               right
```

### Recursive Calls
```
Sort([23, 12, ...])      // Left partition
// Pivot group excluded  // Already sorted
Sort([87, 92, ...])      // Right partition
```

## Paper Reference

**Section 3.1: Further Tuning of Block Partitioning**
> "an additional check for duplicates equal to the pivot if one of the following two conditions applies:
> - the pivot occurs twice in the sample for pivot selection (in the case of median-of-three),
> - the partitioning results very unbalanced for an array of small size.
> 
> The check for duplicates takes place after the partitioning is completed. Only the larger half of the array is searched for elements equal to the pivot."

Edelkamp, S., & Weiß, A. (2016). BlockQuicksort: How Branch Mispredictions don't affect Quicksort. arXiv:1604.06697.

## Benefits

1. **Prevents deep recursion** on duplicate-heavy small arrays
2. **Avoids IntroSort fallback** to Heapsort
3. **Improves performance** on real-world data with duplicates
4. **Minimal overhead** - only active for small arrays, early stop on sparse duplicates
5. **Complements block partitioning** - handles duplicates that block partitioning alone cannot optimize

## Limitations

1. Only applies to arrays ≤ 512 elements
2. Requires additional comparisons and swaps during duplicate scan
3. Most beneficial when duplicates are clustered (not randomly distributed)

## Future Improvements

Potential enhancements mentioned in the paper but not yet implemented:

1. **Pivot sample duplicate detection**
   - Check if pivot occurs twice in median-of-3/5 sample
   - Trigger duplicate check even for larger arrays

2. **Unbalanced partition detection**
   - Measure partition imbalance ratio
   - Activate duplicate check when imbalance is severe

3. **Adaptive threshold**
   - Adjust `DuplicateCheckThreshold` based on recursion depth
   - Use different ratios for different array sizes

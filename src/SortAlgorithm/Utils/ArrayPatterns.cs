namespace SortAlgorithm.Utils;

public static class ArrayPatterns
{
    /// <summary>
    /// ランダム配列を生成
    /// <br/>
    /// Generate random array
    /// </summary>
    public static int[] GenerateRandom(int size, Random random)
    {
        return Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();
    }

    /// <summary>
    /// 負の値と正の値のランダム配列を生成
    /// <br/>
    /// Generate negative and positive random array
    /// </summary>
    public static int[] GenerateNegativePositiveRandom(int size, Random random)
    {
        return Enumerable.Range(-1 * (size / 2), size).OrderBy(_ => random.Next()).ToArray();
    }

    /// <summary>
    /// 負の値のランダム配列を生成
    /// <br/>
    /// Generate negative random array
    /// </summary>
    public static int[] GenerateNegativeeRandom(int size, Random random)
    {
        return Enumerable.Range(-1 * size, size).OrderBy(_ => random.Next()).ToArray();
    }

    /// <summary>
    /// ソート済み配列を生成（昇順）
    /// <br/>
    /// Generate sorted array (ascending order)
    /// </summary>
    public static int[] GenerateSorted(int size)
    {
        return Enumerable.Range(1, size).ToArray();
    }

    /// <summary>
    /// 逆順配列を生成（降順）
    /// <br/>
    /// Generate reversed array (descending order)
    /// </summary>
    public static int[] GenerateReversed(int size)
    {
        return Enumerable.Range(1, size).Reverse().ToArray();
    }

    /// <summary>
    /// 単一要素移動（ソート済みから1つの要素だけをランダム位置に移動）
    /// <br/>
    /// Generate single element moved (move one element from sorted array to random position)
    /// </summary>
    public static int[] GenerateSingleElementMoved(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();

        if (size < 2) return array;

        // Pick a random element to move
        var sourceIndex = random.Next(size);
        var destIndex = random.Next(size);

        if (sourceIndex == destIndex) return array;

        // Move element using rotation
        var element = array[sourceIndex];

        if (destIndex < sourceIndex)
        {
            // Shift elements right
            Array.Copy(array, destIndex, array, destIndex + 1, sourceIndex - destIndex);
            array[destIndex] = element;
        }
        else
        {
            // Shift elements left
            Array.Copy(array, sourceIndex + 1, array, sourceIndex, destIndex - sourceIndex);
            array[destIndex] = element;
        }

        return array;
    }

    /// <summary>
    /// ほぼソート済み配列を生成（要素の10%をランダムに入れ替え）
    /// <br/>
    /// Generate nearly sorted array (randomly swap 10% of elements)
    /// </summary>
    public static int[] GenerateNearlySorted(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // 要素の10%をランダムに入れ替え
        var swapCount = Math.Max(1, size / 10);
        for (int i = 0; i < swapCount; i++)
        {
            var index1 = random.Next(size);
            var index2 = random.Next(size);
            (array[index1], array[index2]) = (array[index2], array[index1]);
        }

        return array;
    }

    /// <summary>
    /// ジグザグパターンの配列を生成（交互に上下する）
    /// <br/>
    /// Generate zigzag pattern array (alternating up and down)
    /// </summary>
    public static int[] GenerateZigzag(int size)
    {
        var array = new int[size];

        // 小さい値と大きい値を交互に配置
        var lowValues = Enumerable.Range(1, size / 2).ToList();
        var highValues = Enumerable.Range(size / 2 + 1, size - size / 2).ToList();

        for (int i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                // 偶数インデックス: 小さい値
                var index = i / 2;
                array[i] = index < lowValues.Count ? lowValues[index] : highValues[i - lowValues.Count];
            }
            else
            {
                // 奇数インデックス: 大きい値
                var index = i / 2;
                array[i] = index < highValues.Count ? highValues[index] : lowValues[i - highValues.Count];
            }
        }

        return array;
    }

    /// <summary>
    /// 半分ソート済みの配列を生成（前半のみソート済み、後半はランダム）
    /// <br/>
    /// Generate half sorted array (first half sorted, second half random)
    /// </summary>
    public static int[] GenerateHalfSorted(int size, Random random)
    {
        var mid = size / 2;
        var firstHalf = Enumerable.Range(1, mid).ToArray();
        var secondHalf = Enumerable.Range(mid + 1, size - mid).OrderBy(_ => random.Next()).ToArray();
        return firstHalf.Concat(secondHalf).ToArray();
    }

    /// <summary>
    /// ほぼソート済み配列（5%のペアをランダムスワップ）
    /// <br/>
    /// Generate almost sorted array (randomly swap 5% of pairs)
    /// </summary>
    public static int[] GenerateAlmostSorted(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var swapCount = Math.Max(1, size / 20);

        for (var i = 0; i < swapCount; i++)
        {
            var idx1 = random.Next(size);
            var idx2 = random.Next(size);
            (array[idx1], array[idx2]) = (array[idx2], array[idx1]);
        }

        return array;
    }

    /// <summary>
    /// スクランブル末尾（約14%の要素を末尾に抽出してシャッフル）
    /// <br/>
    /// Generate scrambled tail (extract ~14% of elements to tail and shuffle)
    /// </summary>
    public static int[] GenerateScrambledTail(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var extracted = new List<int>();
        var kept = new List<int>();

        for (var i = 0; i < size; i++)
        {
            if (random.NextDouble() < 1.0 / 7.0)
            {
                extracted.Add(array[i]);
            }
            else
            {
                kept.Add(array[i]);
            }
        }

        // Shuffle extracted elements
        var shuffled = extracted.OrderBy(_ => random.Next()).ToArray();

        return [.. kept, .. shuffled];
    }

    /// <summary>
    /// スクランブル先頭（約14%の要素を先頭に抽出してシャッフル）
    /// <br/>
    /// Generate scrambled head (extract ~14% of elements to head and shuffle)
    /// </summary>
    public static int[] GenerateScrambledHead(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var extracted = new List<int>();
        var kept = new List<int>();

        for (var i = size - 1; i >= 0; i--)
        {
            if (random.NextDouble() < 1.0 / 7.0)
            {
                extracted.Add(array[i]);
            }
            else
            {
                kept.Insert(0, array[i]);
            }
        }

        // Shuffle extracted elements
        var shuffled = extracted.OrderBy(_ => random.Next()).ToArray();

        return [.. shuffled, .. kept];
    }

    /// <summary>
    /// ノイズ入り（小ブロックごとにシャッフル）
    /// <br/>
    /// Generate noisy array (shuffle each small block)
    /// </summary>
    public static int[] GenerateNoisy(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var blockSize = Math.Max(4, (int)(Math.Sqrt(size) / 2));

        for (var i = 0; i + blockSize <= size; i += random.Next(blockSize - 1) + 1)
        {
            var end = Math.Min(i + blockSize, size);
            var block = array[i..end].OrderBy(_ => random.Next()).ToArray();
            Array.Copy(block, 0, array, i, end - i);
        }

        return array;
    }

    /// <summary>
    /// 奇数インデックスのみシャッフル（偶数インデックスはソート済み）
    /// <br/>
    /// Generate shuffled odds (shuffle only odd indices, even indices are sorted)
    /// </summary>
    public static int[] GenerateShuffledOdds(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Fisher-Yates shuffle but only for odd indices
        for (var i = 1; i < size; i += 2)
        {
            // Random odd index from current position to end
            var randomOddIndex = (random.Next((size - i) / 2) * 2) + i;
            (array[i], array[randomOddIndex]) = (array[randomOddIndex], array[i]);
        }

        return array;
    }

    /// <summary>
    /// 半分シャッフル（全体をシャッフル後、前半のみソート）
    /// <br/>
    /// Generate shuffled half (shuffle entire array, then sort only first half)
    /// </summary>
    public static int[] GenerateShuffledHalf(int size, Random random)
    {
        // Shuffle entire array
        var array = Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();

        // Sort only the first half
        var mid = size / 2;
        Array.Sort(array, 0, mid);

        return array;
    }

    /// <summary>
    /// ダブルレイヤー（偶数インデックスを対称位置とスワップ）
    /// <br/>
    /// Generate double layered (swap even indices with their symmetric positions)
    /// </summary>
    public static int[] GenerateDoubleLayered(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Swap even indices with their symmetric positions
        for (var i = 0; i < size / 2; i += 2)
        {
            (array[i], array[size - i - 1]) = (array[size - i - 1], array[i]);
        }

        return array;
    }

    /// <summary>
    /// 偶数値逆順・奇数値順序（偶数の値を逆順に、奇数の値を順序通りに配置）
    /// <br/>
    /// Generate evens reversed odds in order (even values reversed, odd values in order)
    /// </summary>
    public static int[] GenerateEvensReversedOddsInOrder(int size)
    {
        var evens = new List<int>();
        var odds = new List<int>();

        // Separate even and odd values
        for (var i = 1; i <= size; i++)
        {
            if (i % 2 == 0)
            {
                evens.Add(i);
            }
            else
            {
                odds.Add(i);
            }
        }

        // Reverse even values
        evens.Reverse();

        // Interleave odds (in order) and evens (reversed)
        var array = new int[size];
        var evenIdx = 0;
        var oddIdx = 0;

        for (var i = 0; i < size; i++)
        {
            if ((i + 1) % 2 == 0 && evenIdx < evens.Count)
            {
                // Position for even value
                array[i] = evens[evenIdx++];
            }
            else if (oddIdx < odds.Count)
            {
                // Position for odd value
                array[i] = odds[oddIdx++];
            }
            else if (evenIdx < evens.Count)
            {
                // Fill remaining with evens
                array[i] = evens[evenIdx++];
            }
        }

        return array;
    }

    /// <summary>
    /// 偶数値順序・奇数値スクランブル（偶数の値を順序通りに、奇数の値をスクランブルして配置）
    /// <br/>
    /// Generate evens in order scrambled odds (even values in order, odd values scrambled)
    /// </summary>
    public static int[] GenerateEvensInOrderScrambledOdds(int size, Random random)
    {
        var evens = new List<int>();
        var odds = new List<int>();

        // Separate even and odd values
        for (var i = 1; i <= size; i++)
        {
            if (i % 2 == 0)
            {
                evens.Add(i);
            }
            else
            {
                odds.Add(i);
            }
        }

        // Shuffle odd values using Fisher-Yates
        for (var i = odds.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (odds[i], odds[j]) = (odds[j], odds[i]);
        }

        // Interleave evens (in order) and odds (scrambled)
        var array = new int[size];
        var evenIdx = 0;
        var oddIdx = 0;

        for (var i = 0; i < size; i++)
        {
            if ((i + 1) % 2 == 0 && evenIdx < evens.Count)
            {
                // Position for even value
                array[i] = evens[evenIdx++];
            }
            else if (oddIdx < odds.Count)
            {
                // Position for odd value
                array[i] = odds[oddIdx++];
            }
            else if (evenIdx < evens.Count)
            {
                // Fill remaining with evens
                array[i] = evens[evenIdx++];
            }
        }

        return array;
    }

    /// <summary>
    /// 最終マージ状態（偶数・奇数インデックスが別々にソート済み）
    /// <br/>
    /// Generate final merge (even and odd indices are sorted separately)
    /// </summary>
    public static int[] GenerateFinalMerge(int size)
    {
        var array = new int[size];
        var sorted = Enumerable.Range(1, size).ToArray();

        // Even indices get first half, odd indices get second half
        var evenIdx = 0;
        var oddIdx = 0;

        for (var i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                array[i] = sorted[evenIdx++];
            }
            else
            {
                array[i] = sorted[size / 2 + oddIdx++];
            }
        }

        return array;
    }

    /// <summary>
    /// シャッフル後最終マージ（全体をシャッフル後、前半と後半を別々にソート）
    /// <br/>
    /// Generate shuffled final merge (shuffle entire array, then sort first and second halves separately)
    /// </summary>
    public static int[] GenerateShuffledFinalMerge(int size, Random random)
    {
        var array = Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();
        var mid = size / 2;

        Array.Sort(array, 0, mid);
        Array.Sort(array, mid, size - mid);

        return array;
    }

    /// <summary>
    /// ソートギア状（4-wayインターリーブでソート済み）
    /// ArrayVのSawtoothパターン：4つの連続した上昇グループを生成
    /// <br/>
    /// Generate sawtooth (4-way interleaved sorted)
    /// <br/>
    /// ArrayV Sawtooth pattern: Generate 4 consecutive ascending groups
    /// </summary>
    public static int[] GenerateSawtooth(int size)
    {
        var sorted = Enumerable.Range(1, size).ToArray();
        var result = new int[size];
        const int count = 4;
        var k = 0;

        // 4-wayインターリーブ：各グループの要素を順番に収集
        for (var j = 0; j < count; j++)
        {
            for (var i = j; i < size; i += count)
            {
                result[k++] = sorted[i];
            }
        }

        return result;
    }

    /// <summary>
    /// パーティション済み（ソート後、前半と後半を別々にシャッフル）
    /// <br/>
    /// Generate partitioned (sort, then shuffle first and second halves separately)
    /// </summary>
    public static int[] GeneratePartitioned(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var mid = size / 2;

        var firstHalf = array[..mid].OrderBy(_ => random.Next()).ToArray();
        var secondHalf = array[mid..].OrderBy(_ => random.Next()).ToArray();

        return [.. firstHalf, .. secondHalf];
    }

    /// <summary>
    /// 半分反転（後半が逆順）
    /// <br/>
    /// Generate half reversed (second half is reversed)
    /// </summary>
    public static int[] GenerateHalfReversed(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var mid = size / 2;

        Array.Reverse(array, mid, size - mid);

        return array;
    }

    /// <summary>
    /// パイプオルガン型（偶数要素が前半、奇数要素が後半逆順）
    /// <br/>
    /// Generate pipe organ (even elements in first half, odd elements in reversed second half)
    /// </summary>
    public static int[] GeneratePipeOrgan(int size)
    {
        var array = new int[size];
        var sorted = Enumerable.Range(1, size).ToArray();
        var left = 0;
        var right = size - 1;

        for (var i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                array[left++] = sorted[i];
            }
            else
            {
                array[right--] = sorted[i];
            }
        }

        return array;
    }

    /// <summary>
    /// 谷型の配列を生成（中央が最小値）
    /// <br/>
    /// Generate valley shape array (center is minimum value)
    /// </summary>
    public static int[] GenerateValleyShape(int size)
    {
        var array = new int[size];
        var values = Enumerable.Range(1, size).Reverse().ToArray();

        // 大きい値から小さい値へ、そして小さい値から大きい値へ
        int left = 0;
        int right = size - 1;

        for (int i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                // 左側に大きい値を配置
                array[left++] = values[i];
            }
            else
            {
                // 右側に大きい値を配置
                array[right--] = values[i];
            }
        }

        return array;
    }

    /// <summary>
    /// 最終基数パス（偶数・奇数要素が交互配置）
    /// <br/>
    /// Generate final radix pass (even and odd elements alternately placed)
    /// </summary>
    public static int[] GenerateFinalRadix(int size)
    {
        var array = new int[size];
        var sorted = Enumerable.Range(1, size).ToArray();
        var mid = size / 2;

        for (var i = 0; i < mid; i++)
        {
            array[i * 2] = sorted[mid + i];
            if (i * 2 + 1 < size)
            {
                array[i * 2 + 1] = sorted[i];
            }
        }

        return array;
    }

    /// <summary>
    /// 真の最終基数パス（ビットマスクベースの基数ソート）
    /// <br/>
    /// Generate real final radix pass (bit mask based radix sort)
    /// </summary>
    public static int[] GenerateRealFinalRadix(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Calculate bit mask (highest bit position)
        var mask = 0;
        for (var i = 0; i < size; i++)
        {
            while (mask < array[i])
            {
                mask = (mask << 1) + 1;
            }
        }
        mask >>= 1;

        // Counting sort by masked bits
        var counts = new int[mask + 2];
        var temp = new int[size];
        Array.Copy(array, temp, size);

        for (var i = 0; i < size; i++)
        {
            counts[(array[i] & mask) + 1]++;
        }

        for (var i = 1; i < counts.Length; i++)
        {
            counts[i] += counts[i - 1];
        }

        var result = new int[size];
        for (var i = 0; i < size; i++)
        {
            result[counts[temp[i] & mask]++] = temp[i];
        }

        return result;
    }

    /// <summary>
    /// 再帰的最終基数パス（再帰的インターリーブ）
    /// <br/>
    /// Generate recursive final radix pass (recursive interleaving)
    /// </summary>
    public static int[] GenerateRecursiveFinalRadix(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        WeaveRecursive(array, 0, size, 1);
        return array;

        static void WeaveRecursive(int[] arr, int pos, int length, int gap)
        {
            if (length < 2) return;

            var mod2 = length % 2;
            length -= mod2;
            var mid = length / 2;
            var temp = new int[mid];

            // Extract first half
            for (int i = pos, j = 0; i < pos + gap * mid; i += gap, j++)
            {
                temp[j] = arr[i];
            }

            // Interleave
            for (int i = pos + gap * mid, j = pos, k = 0; i < pos + gap * length; i += gap, j += 2 * gap, k++)
            {
                arr[j] = arr[i];
                arr[j + gap] = temp[k];
            }

            WeaveRecursive(arr, pos, mid + mod2, 2 * gap);
            WeaveRecursive(arr, pos + gap, mid, 2 * gap);
        }
    }

    /// <summary>
    /// 最終バイトニックパス（配列を反転後にPipe Organ配置）
    /// <br/>
    /// Generate final bitonic pass (reverse array then arrange in Pipe Organ pattern)
    /// </summary>
    public static int[] GenerateFinalBitonicPass(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Reverse the array
        Array.Reverse(array);

        // Create pipe organ pattern (even indices go to front, odd indices go to back reversed)
        var temp = new int[size];
        var front = 0;
        var back = size;

        for (var i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                temp[front++] = array[i];
            }
            else
            {
                temp[--back] = array[i];
            }
        }

        return temp;
    }

    /// <summary>
    /// ビット反転順序（FFT用のビット反転配列）
    /// <br/>
    /// Generate bit reversal order (bit-reversed array for FFT)
    /// </summary>
    public static int[] GenerateBitReversal(int size)
    {
        var len = 1 << (int)(Math.Log(size) / Math.Log(2));
        var temp = Enumerable.Range(1, size).ToArray();
        var array = new int[size];

        // Initialize with indices
        for (var i = 0; i < len; i++)
        {
            array[i] = i;
        }

        // Bit reversal permutation
        var m = 0;
        var d1 = len >> 1;
        var d2 = d1 + (d1 >> 1);

        for (var i = 1; i < len - 1; i++)
        {
            var j = d1;

            for (int k = i, n = d2; (k & 1) == 0; j -= n, k >>= 1, n >>= 1)
            { }

            m += j;
            if (m > i)
            {
                (array[i], array[m]) = (array[m], array[i]);
            }
        }

        // Map back to values
        var result = new int[size];
        for (var i = 0; i < len && i < size; i++)
        {
            result[i] = temp[array[i] % size];
        }

        for (var i = len; i < size; i++)
        {
            result[i] = temp[i];
        }

        return result;
    }

    /// <summary>
    /// ブロックごとにランダムシャッフル
    /// <br/>
    /// Generate block randomly (randomly shuffle by blocks)
    /// </summary>
    public static int[] GenerateBlockRandomly(int size, Random random)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var blockSize = Pow2LessThanOrEqual((int)Math.Sqrt(size));
        var adjustedSize = size - (size % blockSize);

        // Fisher-Yates shuffle but on blocks
        for (var i = 0; i < adjustedSize; i += blockSize)
        {
            var randomBlock = random.Next((adjustedSize - i) / blockSize) * blockSize + i;
            BlockSwap(array, i, randomBlock, blockSize);
        }

        return array;

        static void BlockSwap(int[] arr, int a, int b, int length)
        {
            for (var i = 0; i < length; i++)
            {
                (arr[a + i], arr[b + i]) = (arr[b + i], arr[a + i]);
            }
        }

        static int Pow2LessThanOrEqual(int value)
        {
            var val = 1;
            while (val <= value)
            {
                val <<= 1;
            }
            return val >> 1;
        }
    }

    /// <summary>
    /// ブロックごとに反転（ブロック順序を反転）
    /// <br/>
    /// Generate block reverse (reverse block order)
    /// </summary>
    public static int[] GenerateBlockReverse(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var blockSize = Pow2LessThanOrEqual((int)Math.Sqrt(size));
        var adjustedSize = size - (size % blockSize);

        var i = 0;
        var j = adjustedSize - blockSize;

        while (i < j)
        {
            BlockSwap(array, i, j, blockSize);
            i += blockSize;
            j -= blockSize;
        }

        return array;

        static void BlockSwap(int[] arr, int a, int b, int length)
        {
            for (var k = 0; k < length; k++)
            {
                (arr[a + k], arr[b + k]) = (arr[b + k], arr[a + k]);
            }
        }

        static int Pow2LessThanOrEqual(int value)
        {
            var val = 1;
            while (val <= value) val <<= 1;
            return val >> 1;
        }
    }

    /// <summary>
    /// インターレース（最小値を先頭、残りを両端から交互配置）
    /// <br/>
    /// Generate interlaced (minimum value at head, rest alternately placed from both ends)
    /// </summary>
    public static int[] GenerateInterlaced(int size)
    {
        var array = new int[size];
        var sorted = Enumerable.Range(1, size).ToArray();

        array[0] = sorted[0];
        var left = 1;
        var right = size - 1;

        for (var i = 1; i < size; i++)
        {
            if (i % 2 == 1)
            {
                array[i] = sorted[right--];
            }
            else
            {
                array[i] = sorted[left++];
            }
        }

        return array;
    }

    /// <summary>
    /// 二分探索木レベル順走査（Level-Order Traversal / BFS）
    /// ソート済み配列からBSTを構築し、レベル順で再配置
    /// <br/>
    /// Generate BST traversal (Level-Order Traversal / BFS)
    /// <br/>
    /// Build BST from sorted array and rearrange in level order
    /// </summary>
    public static int[] GenerateBstTraversal(int size, Random random)
    {
        var temp = Enumerable.Range(1, size).ToArray();
        var array = new int[size];

        // BFS (Level-Order Traversal) using queue
        var queue = new Queue<(int start, int end)>();
        queue.Enqueue((0, size));
        var i = 0;

        while (queue.Count > 0)
        {
            var (start, end) = queue.Dequeue();
            if (start != end)
            {
                var mid = (start + end) / 2;
                array[i++] = temp[mid];
                queue.Enqueue((start, mid));
                queue.Enqueue((mid + 1, end));
            }
        }

        return array;
    }

    /// <summary>
    /// 逆BST（レベル順 → 中順変換の逆操作）
    /// BSTのレベル順走査インデックスを生成し、それを使って配列を再配置
    /// <br/>
    /// Generate inverted BST (reverse operation of level-order to in-order conversion)
    /// <br/>
    /// Generate BST level-order traversal indices and rearrange array
    /// </summary>
    public static int[] GenerateInvertedBst(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var levelOrderIndices = new int[size];

        // Generate level-order traversal indices using queue
        var queue = new Queue<(int start, int end)>();
        queue.Enqueue((0, size));
        var i = 0;

        while (queue.Count > 0)
        {
            var (start, end) = queue.Dequeue();
            if (start != end)
            {
                var mid = (start + end) / 2;
                levelOrderIndices[i++] = mid;
                queue.Enqueue((start, mid));
                queue.Enqueue((mid + 1, end));
            }
        }

        // Rearrange array using level-order indices
        var temp = new int[size];
        Array.Copy(array, temp, size);

        for (i = 0; i < size; i++)
        {
            array[levelOrderIndices[i]] = temp[i];
        }

        return array;
    }

    /// <summary>
    /// 対数スロープ（2のべき乗ベースの配置）
    /// 各インデックスiに対して、log2(i)に基づいた位置から値を取得
    /// <br/>
    /// Generate logarithmic slopes (power of 2 based placement)
    /// <br/>
    /// For each index i, get value from position based on log2(i)
    /// </summary>
    public static int[] GenerateLogarithmicSlopes(int size)
    {
        var temp = Enumerable.Range(1, size).ToArray();
        var array = new int[size];

        array[0] = temp[0];

        for (var i = 1; i < size; i++)
        {
            // Calculate log base 2
            var log = (int)(Math.Log(i) / Math.Log(2));
            var power = (int)Math.Pow(2, log);

            // Get value from position based on formula: 2 * (i - power) + 1
            var sourceIndex = 2 * (i - power) + 1;
            array[i] = sourceIndex < size ? temp[sourceIndex] : temp[i];
        }

        return array;
    }

    /// <summary>
    /// 半分回転（前半と後半を入れ替え）
    /// 配列を中央で分割し、各要素を対応する位置と入れ替え
    /// <br/>
    /// Generate half rotation (swap first half with second half)
    /// <br/>
    /// Split array at center and swap each element with corresponding position
    /// </summary>
    public static int[] GenerateHalfRotation(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var mid = (size + 1) / 2;

        if (size % 2 == 0)
        {
            // Even size: simple swap
            for (int a = 0, m = mid; m < size; a++, m++)
            {
                (array[a], array[m]) = (array[m], array[a]);
            }
        }
        else
        {
            // Odd size: cyclic rotation
            var temp = array[0];
            var a = 0;
            var m = mid;

            while (m < size)
            {
                array[a++] = array[m];
                array[m++] = array[a];
            }
            array[a] = temp;
        }

        return array;
    }

    /// <summary>
    /// ヒープ化済み（max-heap構造）
    /// <br/>
    /// Generate heapified (max-heap structure)
    /// </summary>
    public static int[] GenerateHeapified(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Build max-heap
        for (var i = size / 2 - 1; i >= 0; i--)
        {
            Heapify(array, size, i);
        }

        return array;

        static void Heapify(int[] arr, int n, int i)
        {
            var largest = i;
            var left = 2 * i + 1;
            var right = 2 * i + 2;

            if (left < n && arr[left] > arr[largest])
            {
                largest = left;
            }

            if (right < n && arr[right] > arr[largest])
            {
                largest = right;
            }

            if (largest != i)
            {
                (arr[i], arr[largest]) = (arr[largest], arr[i]);
                Heapify(arr, n, largest);
            }
        }
    }

    /// <summary>
    /// ポプラヒープ化済み（Poplar Heapソート用）
    /// 複数の完全二分木の森を形成
    /// <br/>
    /// Generate poplar heapified (for Poplar Heap sort)
    /// <br/>
    /// Form a forest of complete binary trees
    /// </summary>
    public static int[] GeneratePoplarHeapified(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Shuffle first to create a non-sorted starting point
        var random = new Random(42);
        for (var i = size - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        // Poplar heap: forest of complete binary trees
        // Each tree has size 2^k - 1 (1, 3, 7, 15, 31, ...)
        // Start with small poplars and merge them using binary carry sequence
        const int smallPoplarSize = 15;

        if (size <= smallPoplarSize)
        {
            Array.Sort(array, 0, size);
            return array;
        }

        var poplarLevel = 1;
        var it = 0;
        var next = it + smallPoplarSize;

        while (true)
        {
            // Make a small poplar (sorted)
            if (next <= size)
            {
                Array.Sort(array, it, Math.Min(next - it, size - it));
            }

            var poplarSize = smallPoplarSize;

            // Binary carry: merge poplars using bit tricks
            for (var i = (poplarLevel & -poplarLevel) >> 1; i != 0; i >>= 1)
            {
                it -= poplarSize;
                poplarSize = 2 * poplarSize + 1;
                if (it >= 0 && it + poplarSize <= size)
                {
                    Sift(array, it, poplarSize);
                }
                if (next < size)
                {
                    next++;
                }
            }

            if (size - next <= smallPoplarSize)
            {
                if (next < size)
                {
                    Array.Sort(array, next, size - next);
                }
                break;
            }

            it = next;
            next += smallPoplarSize;
            poplarLevel++;
        }

        return array;

        static void Sift(int[] arr, int first, int size)
        {
            if (size < 2) return;

            var root = first + (size - 1);
            var childRoot1 = root - 1;
            var childRoot2 = first + (size / 2 - 1);

            while (true)
            {
                var maxRoot = root;
                if (arr[maxRoot] < arr[childRoot1])
                {
                    maxRoot = childRoot1;
                }
                if (arr[maxRoot] < arr[childRoot2])
                {
                    maxRoot = childRoot2;
                }
                if (maxRoot == root) return;

                (arr[root], arr[maxRoot]) = (arr[maxRoot], arr[root]);

                size /= 2;
                if (size < 2) return;

                root = maxRoot;
                childRoot1 = root - 1;
                childRoot2 = maxRoot - (size - size / 2);
            }
        }
    }

    /// <summary>
    /// 三角ヒープ化済み（Triangular Heapソート用）
    /// 三角数ベースのヒープ構造（簡略版）
    /// <br/>
    /// Generate triangular heapified (for Triangular Heap sort)
    /// <br/>
    /// Triangular number based heap structure (simplified version)
    /// </summary>
    public static int[] GenerateTriangularHeapified(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();

        // Shuffle first to create a non-sorted starting point
        var random = new Random(43); // Use different seed from Smooth
        for (var i = size - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        // Triangular heap: each row has k elements (1, 2, 3, 4, ...)
        // Triangular numbers: 1, 3, 6, 10, 15, 21, 28, 36, 45, 55...
        // T(n) = n(n+1)/2

        // Build triangular heap structure
        // We'll create a simpler version: divide into triangular sections and heapify each
        var triangularSizes = new List<int>();
        var sum = 0;
        for (var i = 1; sum < size; i++)
        {
            var triangularSize = i; // Row size: 1, 2, 3, 4...
            if (sum + triangularSize > size)
                triangularSize = size - sum;

            triangularSizes.Add(triangularSize);
            sum += triangularSize;

            if (sum >= size) break;
        }

        // Heapify each triangular section
        var pos = 0;
        foreach (var sectionSize in triangularSizes)
        {
            if (pos >= size) break;

            var end = Math.Min(pos + sectionSize, size);

            // Build max-heap for this section
            for (var i = (end - pos) / 2 - 1; i >= 0; i--)
            {
                TriangularHeapify(array, pos, end, pos + i);
            }

            pos = end;
        }

        return array;

        static void TriangularHeapify(int[] arr, int start, int end, int i)
        {
            var largest = i;
            var left = start + 2 * (i - start) + 1;
            var right = start + 2 * (i - start) + 2;

            if (left < end && arr[left] > arr[largest])
                largest = left;

            if (right < end && arr[right] > arr[largest])
                largest = right;

            if (largest != i)
            {
                (arr[i], arr[largest]) = (arr[largest], arr[i]);
                TriangularHeapify(arr, start, end, largest);
            }
        }
    }

    /// <summary>
    /// 少数ユニーク値（16種類の値）
    /// <br/>
    /// Generate few unique values (3 distinct values)
    /// </summary>
    public static int[] GenerateFewUnique(int size, Random random)
    {
        return GenerateFewUnique(size, 16, random);
    }

    /// <summary>
    /// 少数ユニーク値（指定されたユニーク数）
    /// <br/>
    /// Generate few unique values (specified number of distinct values)
    /// </summary>
    public static int[] GenerateFewUnique(int size, int uniqueCount, Random random)
    {
        if (uniqueCount < 1)
            throw new ArgumentException("Unique count must be at least 1", nameof(uniqueCount));
        if (uniqueCount > size)
            uniqueCount = size;

        // Generate evenly distributed unique values across the range
        var values = new int[uniqueCount];
        for (var i = 0; i < uniqueCount; i++)
        {
            values[i] = (int)((i + 1) * (double)size / (uniqueCount + 1));
        }

        // Evenly distribute counts for each unique value
        var baseCount = size / uniqueCount;
        var remainder = size % uniqueCount;

        // Build result array
        var result = new int[size];
        var index = 0;
        for (var i = 0; i < uniqueCount; i++)
        {
            // Each unique value gets baseCount occurrences, plus 1 for the first 'remainder' values
            var count = baseCount + (i < remainder ? 1 : 0);
            for (var j = 0; j < count; j++)
            {
                result[index++] = values[i];
            }
        }

        // Shuffle using Fisher-Yates algorithm
        for (var i = size - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    /// <summary>
    /// 重複多数（ユニーク値は配列サイズの20%程度）
    /// <br/>
    /// Generate many duplicates (unique values are about 20% of array size)
    /// </summary>
    public static int[] GenerateManyDuplicates(int size, Random random)
    {
        var uniqueCount = Math.Max(10, Math.Min(40, size / 5));
        return Enumerable.Range(0, size)
            .Select(_ => random.Next(1, uniqueCount + 1))
            .ToArray();
    }

    /// <summary>
    /// 偏った重複（90%が同一値、残りがユニーク値）
    /// <br/>
    /// Generate skewed duplicates (90% same value, rest unique values)
    /// </summary>
    public static int[] GenerateSkewedDuplicates(int size, Random random)
    {
        var result = new int[size];

        // The dominant value (used for 90% of the array)
        var dominantValue = size / 2;

        // Calculate counts: 90% for dominant value, 10% for unique values
        var dominantCount = (int)(size * 0.9);
        var uniqueCount = size - dominantCount;

        // Fill with dominant value
        for (var i = 0; i < dominantCount; i++)
        {
            result[i] = dominantValue;
        }

        // Fill rest with unique values
        for (var i = dominantCount; i < size; i++)
        {
            // Generate unique values different from the dominant value
            var value = random.Next(1, size + 1);
            while (value == dominantValue)
            {
                value = random.Next(1, size + 1);
            }
            result[i] = value;
        }

        // Shuffle using Fisher-Yates algorithm
        for (var i = size - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    /// <summary>
    /// 全要素同一
    /// <br/>
    /// Generate all equal elements
    /// </summary>
    public static int[] GenerateAllEqual(int size)
    {
        return Enumerable.Repeat(size / 2, size).ToArray();
    }

    /// <summary>
    /// 正弦波分布
    /// <br/>
    /// Generate sine wave distribution
    /// </summary>
    public static int[] GenerateSineWave(int size)
    {
        var array = new int[size];
        var n = size - 1;
        var c = 2 * Math.PI / n;

        for (var i = 0; i < size; i++)
        {
            array[i] = (int)(n * (Math.Sin(c * i) + 1) / 2) + 1;
        }

        return array;
    }

    /// <summary>
    /// 余弦波分布
    /// <br/>
    /// Generate cosine wave distribution
    /// </summary>
    public static int[] GenerateCosineWave(int size)
    {
        var array = new int[size];
        var n = size - 1;
        var c = 2 * Math.PI / n;

        for (var i = 0; i < size; i++)
        {
            array[i] = (int)(n * (Math.Cos(c * i) + 1) / 2) + 1;
        }

        return array;
    }

    /// <summary>
    /// ベル曲線分布（正規分布）
    /// <br/>
    /// Generate bell curve distribution (normal distribution)
    /// </summary>
    public static int[] GenerateBellCurve(int size)
    {
        var array = new int[size];
        var step = 8.0 / size;
        var position = -4.0;
        var constant = 1264;
        var factor = size / 512.0;

        for (var i = 0; i < size; i++)
        {
            var square = Math.Pow(position, 2);
            var halfNegSquare = -square / 2.0;
            var numerator = constant * factor * Math.Pow(Math.E, halfNegSquare);
            var denominator = Math.Sqrt(2 * Math.PI);

            array[i] = Math.Max(1, (int)(numerator / denominator));
            position += step;
        }

        return array;
    }

    /// <summary>
    /// パーリンノイズ曲線
    /// <br/>
    /// Generate Perlin noise curve
    /// </summary>
    public static int[] GeneratePerlinNoiseCurve(int size, Random random)
    {
        var array = new int[size];

        for (var i = 0; i < size; i++)
        {
            var x = (double)i / size;
            var noise = PerlinNoise(x, random);
            array[i] = Math.Max(1, Math.Min(size, (int)(noise * size)));
        }

        return array;

        static double PerlinNoise(double x, Random rnd)
        {
            var xi = (int)Math.Floor(x) & 255;
            var xf = x - Math.Floor(x);
            var u = Fade(xf);

            var a = rnd.Next(256);
            var b = rnd.Next(256);

            return Lerp(u, Grad(a, xf), Grad(b, xf - 1));

            static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
            static double Lerp(double t, double a, double b) => a + t * (b - a);
            static double Grad(int hash, double x) => (hash & 1) == 0 ? x : -x;
        }
    }

    // Additional Mathematical Distributions

    /// <summary>
    /// 二次曲線分布（x²）
    /// <br/>
    /// Generate quadratic distribution (x²)
    /// </summary>
    public static int[] GenerateQuadraticDistribution(int size)
    {
        var array = new int[size];
        var n = size - 1;

        for (var i = 0; i < size; i++)
        {
            var x = (double)i / n;
            array[i] = (int)(n * x * x) + 1;
        }

        // Shuffle to randomize order while keeping value distribution
        return ShuffleArray(array);
    }

    /// <summary>
    /// 平方根曲線分布（√x）
    /// <br/>
    /// Generate square root distribution (√x)
    /// </summary>
    public static int[] GenerateSquareRootDistribution(int size)
    {
        var array = new int[size];
        var n = size - 1;

        for (var i = 0; i < size; i++)
        {
            var x = (double)i / n;
            array[i] = (int)(n * Math.Sqrt(x)) + 1;
        }

        return ShuffleArray(array);
    }

    /// <summary>
    /// 三次曲線分布（x³ 中心）
    /// <br/>
    /// Generate cubic distribution (x³ centered)
    /// </summary>
    public static int[] GenerateCubicDistribution(int size)
    {
        var array = new int[size];
        var h = size / 2.0;

        for (var i = 0; i < size; i++)
        {
            var val = i / h - 1;
            var cubic = val * val * val;
            array[i] = (int)(h * (cubic + 1));
        }

        return ShuffleArray(array);
    }

    /// <summary>
    /// 五次曲線分布（x⁵ 中心）
    /// <br/>
    /// Generate quintic distribution (x⁵ centered)
    /// </summary>
    public static int[] GenerateQuinticDistribution(int size)
    {
        var array = new int[size];
        var h = size / 2.0;

        for (var i = 0; i < size; i++)
        {
            var val = i / h - 1;
            var quintic = Math.Pow(val, 5);
            array[i] = (int)(h * (quintic + 1));
        }

        return ShuffleArray(array);
    }

    /// <summary>
    /// 立方根曲線分布（∛x）
    /// <br/>
    /// Generate cube root distribution (∛x)
    /// </summary>
    public static int[] GenerateCubeRootDistribution(int size)
    {
        var array = new int[size];
        var h = size / 2.0;

        for (var i = 0; i < size; i++)
        {
            var val = i / h - 1;
            var root = val < 0 ? -Math.Pow(-val, 1.0 / 3.0) : Math.Pow(val, 1.0 / 3.0);
            array[i] = (int)(h * (root + 1));
        }

        return ShuffleArray(array);
    }

    /// <summary>
    /// 五乗根曲線分布（⁵√x）
    /// <br/>
    /// Generate fifth root distribution (⁵√x)
    /// </summary>
    public static int[] GenerateFifthRootDistribution(int size)
    {
        var array = new int[size];
        var h = size / 2.0;

        for (var i = 0; i < size; i++)
        {
            var val = i / h - 1;
            var root = val < 0 ? -Math.Pow(-val, 1.0 / 5.0) : Math.Pow(val, 1.0 / 5.0);
            array[i] = (int)(h * (root + 1));
        }

        return ShuffleArray(array);
    }

    /// <summary>
    /// Fisher-Yatesシャッフル（配列をランダム化）
    /// <br/>
    /// Fisher-Yates shuffle (randomize array)
    /// </summary>
    public static int[] ShuffleArray(int[] array)
    {
        var random = new Random();
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
        return array;
    }

    /// <summary>
    /// ルーラー関数分布
    /// <br/>
    /// Generate ruler function distribution
    /// </summary>
    public static int[] GenerateRulerDistribution(int size)
    {
        var array = new int[size];
        var step = Math.Max(1, size / 256);
        var floorLog2 = (int)(Math.Log(size / (double)step) / Math.Log(2));
        var lowest = step;
        while (2 * lowest <= size / 4)
        {
            lowest *= 2;
        }

        var digits = new bool[floorLog2 + 2];
        int i, j;

        for (i = 0; i + step <= size; i += step)
        {
            for (j = 0; j < digits.Length && digits[j]; j++)
            { }

            digits[j] = true;

            for (var k = 0; k < step; k++)
            {
                var value = size / 2 - Math.Min((1 << j) * step, lowest);
                array[i + k] = value;
            }

            for (var k = 0; k < j; k++)
            {
                digits[k] = false;
            }
        }

        for (j = 0; j < digits.Length && digits[j]; j++)
        { }

        digits[j] = true;

        while (i < size)
        {
            var value = Math.Max(size / 2 - (1 << j) * step, size / 4);
            array[i++] = value;
        }

        return array;
    }

    /// <summary>
    /// ブランマンジェ曲線分布
    /// <br/>
    /// Generate blancmange curve distribution
    /// </summary>
    public static int[] GenerateBlancmangeDistribution(int size)
    {
        var array = new int[size];
        var floorLog2 = (int)(Math.Log(size) / Math.Log(2));

        for (var i = 0; i < size; i++)
        {
            var value = (int)(size * CurveSum(floorLog2, (double)i / size));
            array[i] = value;
        }

        return array;

        static double CurveSum(int n, double x)
        {
            var sum = 0.0;
            while (n >= 0)
            {
                sum += Curve(n--, x);
            }
            return sum;
        }

        static double Curve(int n, double x)
        {
            return TriangleWave((1 << n) * x) / (1 << n);
        }

        static double TriangleWave(double x)
        {
            return Math.Abs(x - (int)(x + 0.5));
        }
    }

    /// <summary>
    /// カントール関数分布
    /// <br/>
    /// Generate Cantor function distribution
    /// </summary>
    public static int[] GenerateCantorDistribution(int size)
    {
        var array = new int[size];
        CantorRecursive(array, 0, size, 0, size - 1);
        return ShuffleArray(array);

        static void CantorRecursive(int[] arr, int a, int b, int min, int max)
        {
            if (b - a < 1 || max == min) return;

            var mid = (min + max) / 2;
            if (b - a == 1)
            {
                arr[a] = mid;
                return;
            }

            var t1 = (a + a + b) / 3;
            var t2 = (a + b + b + 2) / 3;

            for (var i = t1; i < t2; i++)
            {
                arr[i] = mid;
            }

            CantorRecursive(arr, a, t1, min, mid);
            CantorRecursive(arr, t2, b, mid + 1, max);
        }
    }

    /// <summary>
    /// 約数の和関数分布
    /// <br/>
    /// Generate divisor sum function distribution
    /// </summary>
    public static int[] GenerateDivisorsDistribution(int size)
    {
        var n = new int[size];
        n[0] = 0;
        if (size > 1)
        {
            n[1] = 1;
        }

        var max = 1.0;
        for (var i = 2; i < size; i++)
        {
            n[i] = SumDivisors(i);
            if (n[i] > max)
            {
                max = n[i];
            }
        }

        var scale = Math.Min((size - 1) / max, 1);
        var array = new int[size];
        for (var i = 0; i < size; i++)
        {
            array[i] = (int)(n[i] * scale);
        }

        return array;

        static int SumDivisors(int num)
        {
            var sum = num + 1;
            for (var i = 2; i <= (int)Math.Sqrt(num); i++)
            {
                if (num % i == 0)
                {
                    if (i == num / i)
                    {
                        sum += i;
                    }
                    else
                    {
                        sum += i + num / i;
                    }
                }
            }
            return sum;
        }
    }

    /// <summary>
    /// FSD分布（Fly Straight Dangit - OEIS A133058）
    /// <br/>
    /// Generate FSD distribution (Fly Straight Dangit - OEIS A133058)
    /// </summary>
    public static int[] GenerateFsdDistribution(int size)
    {
        var fsd = new int[size];
        fsd[0] = 1;
        if (size > 1)
        {
            fsd[1] = 1;
        }

        var max = 1.0;
        for (var i = 2; i < size; i++)
        {
            var g = Gcd(fsd[i - 1], i);
            fsd[i] = fsd[i - 1] / g + (g == 1 ? i + 1 : 0);
            if (fsd[i] > max)
            {
                max = fsd[i];
            }
        }

        var scale = Math.Min((size - 1) / max, 1);
        var array = new int[size];
        for (var i = 0; i < size; i++)
        {
            array[i] = (int)(fsd[i] * scale);
        }

        return array;

        static int Gcd(int a, int b)
        {
            if (b == 0)
            {
                return a;
            }
            return Gcd(b, a % b);
        }
    }

    /// <summary>
    /// 逆対数分布（減少ランダム）
    /// <br/>
    /// Generate reverse logarithmic distribution (decreasing random)
    /// </summary>
    public static int[] GenerateReverseLogDistribution(int size, Random random)
    {
        var array = new int[size];

        for (var i = 0; i < size; i++)
        {
            var r = random.Next(size - i) + i;
            array[i] = r + 1;
        }

        return array;
    }

    /// <summary>
    /// モジュロ関数分布
    /// <br/>
    /// Generate modulo function distribution
    /// </summary>
    public static int[] GenerateModuloDistribution(int size)
    {
        var array = new int[size];

        for (var i = 0; i < size; i++)
        {
            array[i] = 2 * (size % (i + 1));
        }

        return array;
    }

    /// <summary>
    /// オイラーのトーシェント関数分布
    /// <br/>
    /// Generate Euler's totient function distribution
    /// </summary>
    public static int[] GenerateTotientDistribution(int size)
    {
        var array = new int[size];
        var minPrimeFactors = new int[size];
        var primes = new List<int>();

        array[0] = 0;
        if (size > 1)
        {
            array[1] = 1;
        }

        for (var i = 2; i < size; i++)
        {
            if (minPrimeFactors[i] == 0)
            {
                primes.Add(i);
                minPrimeFactors[i] = i;
                array[i] = i - 1;
            }

            foreach (var prime in primes)
            {
                if (i * prime >= size) break;

                var last = prime == minPrimeFactors[i];

                minPrimeFactors[i * prime] = prime;
                array[i * prime] = array[i] * (last ? prime : prime - 1);

                if (last) break;
            }
        }

        return array;
    }

    // Advanced/Fractal Patterns

    /// <summary>
    /// サークルソート初回パス（シャッフル後にサークルソート1パスを適用）
    /// <br/>
    /// Generate circle sort first pass (apply one pass of circle sort after shuffle)
    /// </summary>
    public static int[] GenerateCirclePass(int size, Random random)
    {
        var array = Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();

        // Calculate power of 2 >= size
        var n = 1;
        while (n < size)
        {
            n *= 2;
        }

        CircleSortRoutine(array, 0, n - 1, size);

        return array;

        static void CircleSortRoutine(int[] arr, int lo, int hi, int end)
        {
            if (lo == hi)
                return;

            var low = lo;
            var high = hi;
            var mid = (hi - lo) / 2;

            while (lo < hi)
            {
                if (hi < end && arr[lo] > arr[hi])
                {
                    (arr[lo], arr[hi]) = (arr[hi], arr[lo]);
                }
                lo++;
                hi--;
            }

            CircleSortRoutine(arr, low, low + mid, end);
            if (low + mid + 1 < end)
            {
                CircleSortRoutine(arr, low + mid + 1, high, end);
            }
        }
    }

    /// <summary>
    /// ペアワイズ最終パス（隣接ペアがソート済み、全体としてはランダム）
    /// <br/>
    /// Generate pairwise final pass (adjacent pairs are sorted, overall is random)
    /// </summary>
    public static int[] GeneratePairwisePass(int size, Random random)
    {
        var array = Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();

        // Sort adjacent pairs
        for (var i = 1; i < size; i += 2)
        {
            if (array[i - 1] > array[i])
            {
                (array[i - 1], array[i]) = (array[i], array[i - 1]);
            }
        }

        // Use pigeonhole sort on even/odd indices separately
        // Values are 1..size, so we need counts array of size+1
        for (var m = 0; m < 2; m++)
        {
            var counts = new int[size + 1];

            // Count occurrences
            for (var k = m; k < size; k += 2)
            {
                counts[array[k]]++;
            }

            // Place elements back
            var j = m;
            for (var i = 1; i <= size; i++)
            {
                while (counts[i] > 0 && j < size)
                {
                    array[j] = i;
                    j += 2;
                    counts[i]--;
                }
            }
        }

        return array;
    }

    /// <summary>
    /// 再帰的反転（配列全体を反転後、再帰的に半分ずつ反転）
    /// <br/>
    /// Generate recursive reversal (reverse entire array, then recursively reverse each half)
    /// </summary>
    public static int[] GenerateRecursiveReversal(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        ReversalRecursive(array, 0, size);
        return array;

        static void ReversalRecursive(int[] arr, int a, int b)
        {
            if (b - a < 2) return;

            Array.Reverse(arr, a, b - a);

            var m = (a + b) / 2;
            ReversalRecursive(arr, a, m);
            ReversalRecursive(arr, m, b);
        }
    }

    /// <summary>
    /// グレイコードフラクタル（グレイコードに基づく再帰的反転パターン）
    /// <br/>
    /// Generate Gray code fractal (recursive reversal pattern based on Gray code)
    /// </summary>
    public static int[] GenerateGrayCodeFractal(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        GrayCodeRecursive(array, 0, size, false);
        return array;

        static void GrayCodeRecursive(int[] arr, int a, int b, bool backward)
        {
            if (b - a < 3) return;

            var m = (a + b) / 2;

            if (backward)
            {
                Array.Reverse(arr, a, m - a);
            }
            else
            {
                Array.Reverse(arr, m, b - m);
            }

            GrayCodeRecursive(arr, a, m, false);
            GrayCodeRecursive(arr, m, b, true);
        }
    }

    /// <summary>
    /// シェルピンスキー三角形（フラクタルパターン）
    /// <br/>
    /// Generate Sierpinski triangle (fractal pattern)
    /// </summary>
    public static int[] GenerateSierpinskiTriangle(int size)
    {
        var triangle = new int[size];
        TriangleRecursive(triangle, 0, size);

        var sorted = Enumerable.Range(1, size).ToArray();
        var result = new int[size];

        for (var i = 0; i < size; i++)
        {
            result[i] = sorted[triangle[i]];
        }

        return result;

        static void TriangleRecursive(int[] arr, int a, int b)
        {
            if (b - a < 2)
                return;
            if (b - a == 2)
            {
                arr[a + 1]++;
                return;
            }

            var h = (b - a) / 3;
            var t1 = (a + a + b) / 3;
            var t2 = (a + b + b + 2) / 3;

            for (var i = a; i < t1; i++)
            {
                arr[i] += h;
            }
            for (var i = t1; i < t2; i++)
            {
                arr[i] += 2 * h;
            }

            TriangleRecursive(arr, a, t1);
            TriangleRecursive(arr, t1, t2);
            TriangleRecursive(arr, t2, b);
        }
    }

    /// <summary>
    /// 三角数配列（三角数の階層構造）
    /// <br/>
    /// Generate triangular array (hierarchical structure of triangular numbers)
    /// </summary>
    public static int[] GenerateTriangular(int size)
    {
        var triangle = new int[size];
        var j = 0;
        var k = 2;
        var max = 0;

        for (var i = 1; i < size; i++, j++)
        {
            if (i == k)
            {
                j = 0;
                k *= 2;
            }
            triangle[i] = triangle[j] + 1;
            if (triangle[i] > max) max = triangle[i];
        }

        // Counting sort to get indices
        var counts = new int[max + 1];
        for (var i = 0; i < size; i++)
        {
            counts[triangle[i]]++;
        }

        for (var i = 1; i < counts.Length; i++)
        {
            counts[i] += counts[i - 1];
        }

        for (var i = size - 1; i >= 0; i--)
        {
            triangle[i] = --counts[triangle[i]];
        }

        var sorted = Enumerable.Range(1, size).ToArray();
        var result = new int[size];

        for (var i = 0; i < size; i++)
        {
            result[i] = sorted[triangle[i]];
        }

        return result;
    }

    // Adversarial Patterns

    /// <summary>
    /// QuickSort最悪ケース（median-of-3 pivot選択用）
    /// <br/>
    /// Generate QuickSort adversary (worst case for median-of-3 pivot selection)
    /// </summary>
    public static int[] GenerateQuickSortAdversary(int size)
    {
        if (size <= 0)
            return Array.Empty<int>();

        var result = new int[size];
        int pos = 0;

        // First half: odd numbers, interleaving low/high
        int lowOdd = 1;
        int highOdd = (size % 2 == 0) ? size - 1 : size;

        while (pos < size / 2)
        {
            result[pos++] = lowOdd;
            lowOdd += 2;

            if (pos < size / 2)
            {
                result[pos++] = highOdd;
                highOdd -= 2;
            }
        }

        // Second half: all even numbers ascending
        for (int even = 2; pos < size; even += 2)
        {
            result[pos++] = even;
        }

        return result;
    }

    /// <summary>
    /// PDQソート対抗パターン（Pattern-defeating QuickSort用）
    /// 降順ベースに断層・昇順ラン・外れ値を戦略的配置し、パターン検出とpivot選択を困難にする
    /// <br/>
    /// Generate PDQ sort adversary pattern (for Pattern-defeating QuickSort)
    /// Strategic placement of faults, ascending runs, and outliers in descending base to defeat pattern detection and pivot selection
    /// </summary>
    public static int[] GeneratePdqSortAdversary(int size)
    {
        // Scale parameters based on array size
        var scaleFactor = Math.Max(1.0, size / 2048.0);

        return Generate(
            n: size,
            seed: 42,
            smallPercent: 10,
            preInsertPairs: Math.Max(3, (int)(24 * scaleFactor)),
            ascStride: 2,
            gap1AscLen: Math.Max(3, (int)(40 * scaleFactor)),
            gap2AscLen: Math.Max(3, (int)(28 * scaleFactor)),
            gap1Outliers: Math.Max(1, (int)(6 * scaleFactor)),
            gap3DenseLen: Math.Max(3, (int)(24 * scaleFactor)),
            gap3Outliers: Math.Max(1, (int)(4 * scaleFactor)),
            tailFromGap3: Math.Max(3, (int)(32 * scaleFactor))
        );

        static int[] Generate(
            int n,
            int seed = 1,
            int smallPercent = 10,     // Extract ~10% of smallest values
            int preInsertPairs = 24,   // Number of alternating pairs to insert at head (more = more vertical lines in visualization)
            int ascStride = 2,         // Stride for "every Nth" = 2 (skip one)
            int gap1AscLen = 40,       // Number of ascending values at 1/4 gap position
            int gap2AscLen = 28,       // Number of ascending values at 2/4 gap position
            int gap1Outliers = 6,      // Number of outliers at 1/4 gap
            int gap3DenseLen = 24,     // Length of dense narrow-range values at 3/4 gap
            int gap3Outliers = 4,      // Number of outliers at 3/4 gap
            int tailFromGap3 = 32      // Number of values from gap3 to place at tail
        )
        {
            if (n <= 0) return Array.Empty<int>();

            // Adjust to nearest multiple of 4 if not already
            int originalN = n;
            if (n % 4 != 0)
            {
                n = (n / 4 + 1) * 4;
            }

            var rng = new Random(seed);

            int q1 = n / 4;
            int q2 = n / 2;
            int q3 = (3 * n) / 4;

            // 1. Create descending array (rank permutation)
            // Values are 0..n-1 unique. Higher values appear taller in visualization.
            var baseDesc = new List<int>(n);
            for (int v = n - 1; v >= 0; v--) baseDesc.Add(v);

            // 2. Extract ~10% of smallest values (smallest = close to 0)
            int smallCount = Math.Max(1, (n * smallPercent) / 100);
            // baseDesc is descending, so tail has smallest values
            var smallPool = baseDesc.GetRange(baseDesc.Count - smallCount, smallCount);
            baseDesc.RemoveRange(baseDesc.Count - smallCount, smallCount);

            // smallPool has values in [smallCount-1 ... 0] order, so sort ascending for easier handling
            smallPool.Sort(); // 0..(smallCount-1)

            // 3. Create extraction pools for gaps at 1/4, 2/4, 3/4 positions
            // “ギャップから抜いた値” = その境界近辺の要素をある程度抜く
            // This creates visual "breaks" by extracting from boundaries and repositioning later
            var gap1Pool = TakeFromAroundIndex(baseDesc, q1, take: preInsertPairs * 2, rng);
            var gap2Pool = TakeFromAroundIndex(baseDesc, q2, take: preInsertPairs * 2, rng);
            var gap3Pool = TakeFromAroundIndex(baseDesc, q3, take: Math.Max(tailFromGap3 + gap3DenseLen, 64), rng);

            // 4. Alternately insert values from 1/4 and 2/4 gaps at the head
            // (Reserve some for outliers first)
            var outlierReserve = new List<int>();

            // Reserve some values from gap1Pool and gap3Pool for outliers
            ReserveForOutliers(gap1Pool, outlierReserve, Math.Min(gap1Outliers, gap1Pool.Count));
            ReserveForOutliers(gap3Pool, outlierReserve, Math.Min(gap3Outliers, gap3Pool.Count));

            var output = new List<int>(n);

            int pairs = Math.Min(preInsertPairs, Math.Min(gap1Pool.Count, gap2Pool.Count));
            for (int i = 0; i < pairs; i++)
            {
                output.Add(gap1Pool[i]);
                output.Add(gap2Pool[i]);
            }
            gap1Pool.RemoveRange(0, pairs);
            gap2Pool.RemoveRange(0, pairs);

            // 5. Fill rest with descending base, but special regions will be inserted at gap positions
            // Use base as-is for filling.
            // At this stage output is still short. Create array for index-based filling later.
            // Create Outliner pool by combining remaining baseDesc and gap pools
            var remaining = new List<int>(baseDesc.Count + gap1Pool.Count + gap2Pool.Count);
            remaining.AddRange(baseDesc);
            remaining.AddRange(gap1Pool);
            remaining.AddRange(gap2Pool);
            // remaining order is slightly broken, so re-sort descending (base form is descending)
            remaining.Sort((a, b) => b.CompareTo(a));

            // Generate final array
            var a = new int[n];
            int pos = 0;

            // First fill with output (head alternating insertion)
            for (; pos < output.Count && pos < n; pos++) a[pos] = output[pos];

            // Fill rest with descending (gap regions will be overwritten later)
            int remPos = 0;
            while (pos < n && remPos < remaining.Count)
            {
                // Just pack as-is, will overwrite gaps and tail later
                a[pos++] = remaining[remPos++];
            }

            // 6. At 1/4 gap position: ascending every-Nth small values + outliers
            // Extract from small values at ascStride intervals to create ascending list
            var gap1Asc = TakeEveryNth(smallPool, ascStride, take: gap1AscLen);
            // Mix in outliers from outlierReserve
            MixOutliersIntoRun(gap1Asc, outlierReserve, take: gap1Outliers, rng);

            WriteRun(a, start: q1, run: gap1Asc);

            // 7. At 2/4 gap position: arrange remaining small values in ascending order
            var gap2Asc = TakeHead(smallPool, take: gap2AscLen);
            // No outliers at 2/4
            WriteRun(a, start: q2, run: gap2Asc);

            // 8. At 3/4 gap area: similar values (narrow range) + outliers
            // From remaining smallPool, take close values together to create narrow range
            // smallPool is ascending, so taking from head gives close values
            var dense = TakeHead(smallPool, take: gap3DenseLen);
            MixOutliersIntoRun(dense, outlierReserve, take: gap3Outliers, rng);
            WriteRun(a, start: q3, run: dense);

            // 9. Place values from gap 3/4 (gap3Pool) at array tail
            // gap3Pool still remains. Packing at tail makes right-end characteristics more visible.
            // Note: gap3Pool itself may be empty
            int tailCount = Math.Min(tailFromGap3, gap3Pool.Count);
            for (int i = 0; i < tailCount; i++)
            {
                a[n - 1 - i] = gap3Pool[i];
            }

            // 10. Finally ensure permutation (repair duplicates/missing values)
            EnsurePermutationInPlace(a);

            // 11. Trim if different from original size, convert values to 1-based
            if (originalN != n)
            {
                Array.Resize(ref a, originalN);
                EnsurePermutationInPlace(a);
            }

            // Convert values from 0-based to 1-based (for compatibility with other patterns)
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = a[i] + 1;
            }

            return a;
        }

        static List<int> TakeFromAroundIndex(List<int> list, int centerIndex, int take, Random rng)
        {
            // list is a list of values (often in descending order). Take 'take' items from around centerIndex.
            // centerIndex is a conceptual "n position", so scale it according to list.Count.
            if (take <= 0 || list.Count == 0) return new List<int>();

            int n = list.Count;
            int center = Math.Clamp(centerIndex, 0, n - 1);
            int left = Math.Max(0, center - take);
            int right = Math.Min(n, center + take);

            // Extract nearby range as candidates, then randomly select 'take' items from there
            var window = list.GetRange(left, right - left);

            // Shuffle with Fisher-Yates and take first 'take' items
            for (int i = window.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (window[i], window[j]) = (window[j], window[i]);
            }

            int actual = Math.Min(take, window.Count);
            var picked = window.GetRange(0, actual);

            // Remove picked from list (remove by value: O(n*take) but fine for n=2048)
            foreach (var v in picked) list.Remove(v);

            return picked;
        }

        static void ReserveForOutliers(List<int> pool, List<int> reserve, int count)
        {
            count = Math.Min(count, pool.Count);
            for (int i = 0; i < count; i++)
                reserve.Add(pool[i]);
            pool.RemoveRange(0, count);
        }

        static List<int> TakeEveryNth(List<int> pool, int nth, int take)
        {
            var res = new List<int>(take);
            if (nth <= 0) nth = 1;

            for (int i = 0; i < pool.Count && res.Count < take; i += nth)
                res.Add(pool[i]);

            // Remove used items from pool (delete from back)
            // Recalculate taken indices and delete
            for (int i = Math.Min(pool.Count - 1, (res.Count - 1) * nth); i >= 0; i -= nth)
            {
                if ((i / nth) < res.Count) pool.RemoveAt(i);
            }

            return res;
        }

        static List<int> TakeHead(List<int> pool, int take)
        {
            take = Math.Min(take, pool.Count);
            var res = pool.GetRange(0, take);
            pool.RemoveRange(0, take);
            return res;
        }

        static void MixOutliersIntoRun(List<int> run, List<int> outliers, int take, Random rng)
        {
            if (take <= 0 || outliers.Count == 0 || run.Count == 0) return;
            take = Math.Min(take, outliers.Count);

            // Insert outliers into run at roughly equal intervals
            for (int i = 0; i < take; i++)
            {
                int idx = (i + 1) * run.Count / (take + 1);
                idx = Math.Clamp(idx, 0, run.Count);
                int v = outliers[0];
                outliers.RemoveAt(0);
                run.Insert(idx, v);
            }
        }

        static void WriteRun(int[] a, int start, List<int> run)
        {
            if (run.Count == 0) return;
            int n = a.Length;
            int pos = Math.Clamp(start, 0, n);

            int len = Math.Min(run.Count, n - pos);
            for (int i = 0; i < len; i++)
                a[pos + i] = run[i];
        }

        static void EnsurePermutationInPlace(int[] a)
        {
            int n = a.Length;
            var seen = new bool[n];
            var missing = new List<int>();

            for (int i = 0; i < n; i++)
            {
                int v = a[i];
                if ((uint)v >= (uint)n || seen[v])
                    a[i] = -1;
                else
                    seen[v] = true;
            }

            for (int v = 0; v < n; v++)
                if (!seen[v]) missing.Add(v);

            int k = 0;
            for (int i = 0; i < n && k < missing.Count; i++)
                if (a[i] == -1) a[i] = missing[k++];
        }
    }

    /// <summary>
    /// Grailソート最悪ケース
    /// <br/>
    /// Generate Grail sort adversary (worst case for Grail sort)
    /// </summary>
    public static int[] GenerateGrailSortAdversary(int size, Random random)
    {
        if (size <= 16)
        {
            return Enumerable.Range(1, size).Reverse().ToArray();
        }

        var blockLen = 1;
        while (blockLen * blockLen < size)
        {
            blockLen *= 2;
        }

        var numKeys = (size - 1) / blockLen + 1;
        var keys = blockLen + numKeys;

        var array = Enumerable.Range(1, size).OrderBy(_ => random.Next()).ToArray();

        // Sort and reverse the keys section
        Array.Sort(array, 0, keys);
        Array.Reverse(array, 0, keys);

        // Sort the remaining section
        Array.Sort(array, keys, size - keys);

        return array;
    }

    /// <summary>
    /// ShuffleMerge最悪ケース
    /// <br/>
    /// Generate ShuffleMerge adversary (worst case for ShuffleMerge)
    /// </summary>
    public static int[] GenerateShuffleMergeAdversary(int size)
    {
        var array = Enumerable.Range(1, size).ToArray();
        var temp = new int[size];
        var d = 2;
        var end = 1 << (int)(Math.Log(size - 1) / Math.Log(2) + 1);

        while (d <= end)
        {
            var i = 0;
            var dec = 0;

            while (i < size)
            {
                var j = i;
                dec += size;
                while (dec >= d)
                {
                    dec -= d;
                    j++;
                }

                var k = j;
                dec += size;
                while (dec >= d)
                {
                    dec -= d;
                    k++;
                }

                // Reverse merge the sections
                var mid = j;
                Array.Copy(array, i, temp, i, mid - i);
                Array.Copy(array, mid, temp, mid, k - mid);
                Array.Reverse(temp, i, mid - i);
                Array.Copy(temp, i, array, i, k - i);

                i = k;
            }
            d *= 2;
        }

        return array;
    }

    /// <summary>
    /// Timsortドラッグと呼ばれるrun長列が長くなりやすいパターン
    /// <br/>
    /// Generate Timsort drag adversary (pattern that creates long run lengths called Timsort drag)
    /// <br/>
    /// ref: Buss & Knop（Strategies for Stable Merge Sorting, arXiv:1801.04641 https://arxiv.org/abs/1801.04641
    /// </summary>
    public static int[] GenerateTimsortDragAdversary(int size)
    {
        var scale = 32;
        var n = size / scale;
        var runs = GenerateRtimScaled(n: n, scale: 32);
        var patternedArray = GenerateFromRunLengths(runs, size);
        var mod = size % runs[0];
        if (mod != 0)
        {
            for (var i = runs[0]; i < size; i++)
            {
                patternedArray[i] = patternedArray[i - 1] + 1;
            }
        }
        return patternedArray;

        // Munro & Wild multiply by 32 to avoid minrun effects in practical Timsort
        static int[] GenerateRtimScaled(int n, int scale = 32)
            => Rtim(n).Select(x => checked(x * scale)).ToArray();

        // Buss & Knop: Rtim(n) recursive definition (n >= 1)
        static List<int> Rtim(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (n <= 3) return [n];

            int nPrime = n / 2; // floor
            var left = Rtim(nPrime);
            var right = Rtim(nPrime - 1);

            var res = new List<int>(left.Count + right.Count + 1);
            res.AddRange(left);
            res.AddRange(right);
            res.Add((n % 2 == 0) ? 1 : 2);
            return res;
        }

        // Each run is strictly increasing,
        // but between runs the value drops so runs don't merge into a longer increasing run.
        static int[] GenerateFromRunLengths(int[] runLengths, int expectedSize)
        {
            int n = 0;
            foreach (var l in runLengths)
            {
                if (l <= 0) throw new ArgumentException("run length must be positive");
                n += l;
            }

            var a = new int[expectedSize];
            int idx = 0;

            // Make later runs have smaller value ranges so boundary is descending.
            int baseValue = n * 10; // big enough headroom

            foreach (var len in runLengths)
            {
                // Fill ascending within the run: (baseValue-len+1) ... baseValue
                int start = baseValue - len + 1;
                for (int i = 0; i < len; i++)
                    a[idx + i] = start + i;

                idx += len;
                baseValue -= len; // ensures next run's values are all smaller
            }

            return a;
        }
    }


    // Floating-Point with NaN Pattern Generators

    /// <summary>
    /// ランダムな float 配列を生成（NaN を含む）
    /// <br/>
    /// Generate random float array with NaN values
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <param name="random">乱数生成器</param>
    /// <param name="nanRatio">NaN の割合（0.0〜1.0）。デフォルトは 0.1（10%）</param>
    /// <returns>NaN を含むランダムな float 配列</returns>
    public static float[] GenerateRandomFloatWithNaN(int size, Random random, double nanRatio = 0.1)
    {
        var array = new float[size];
        var nanCount = (int)(size * nanRatio);

        // Generate random float values
        for (int i = 0; i < size; i++)
        {
            array[i] = (float)(random.NextDouble() * 1000.0 - 500.0); // Range: -500 to 500
        }

        // Replace random positions with NaN
        for (int i = 0; i < nanCount; i++)
        {
            var index = random.Next(size);
            array[index] = float.NaN;
        }

        return array;
    }

    /// <summary>
    /// ランダムな double 配列を生成（NaN を含む）
    /// <br/>
    /// Generate random double array with NaN values
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <param name="random">乱数生成器</param>
    /// <param name="nanRatio">NaN の割合（0.0〜1.0）。デフォルトは 0.1（10%）</param>
    /// <returns>NaN を含むランダムな double 配列</returns>
    public static double[] GenerateRandomDoubleWithNaN(int size, Random random, double nanRatio = 0.1)
    {
        var array = new double[size];
        var nanCount = (int)(size * nanRatio);

        // Generate random double values
        for (int i = 0; i < size; i++)
        {
            array[i] = random.NextDouble() * 1000.0 - 500.0; // Range: -500 to 500
        }

        // Replace random positions with NaN
        for (int i = 0; i < nanCount; i++)
        {
            var index = random.Next(size);
            array[index] = double.NaN;
        }

        return array;
    }

    /// <summary>
    /// ランダムな Half 配列を生成（NaN を含む）
    /// <br/>
    /// Generate random Half array with NaN values
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <param name="random">乱数生成器</param>
    /// <param name="nanRatio">NaN の割合（0.0〜1.0）。デフォルトは 0.1（10%）</param>
    /// <returns>NaN を含むランダムな Half 配列</returns>
    public static Half[] GenerateRandomHalfWithNaN(int size, Random random, double nanRatio = 0.1)
    {
        var array = new Half[size];
        var nanCount = (int)(size * nanRatio);

        // Generate random Half values
        for (int i = 0; i < size; i++)
        {
            array[i] = (Half)(random.NextDouble() * 100.0 - 50.0); // Range: -50 to 50 (Half has limited range)
        }

        // Replace random positions with NaN
        for (int i = 0; i < nanCount; i++)
        {
            var index = random.Next(size);
            array[index] = Half.NaN;
        }

        return array;
    }

    /// <summary>
    /// ソート済み float 配列を生成（先頭に NaN）
    /// <br/>
    /// Generate sorted float array with NaN at the beginning
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <param name="nanCount">NaN の個数</param>
    /// <returns>先頭に NaN、残りがソート済みの float 配列</returns>
    public static float[] GenerateSortedFloatWithNaN(int size, int nanCount = 3)
    {
        var array = new float[size];

        // NaN at the beginning
        for (int i = 0; i < Math.Min(nanCount, size); i++)
        {
            array[i] = float.NaN;
        }

        // Sorted values after NaN
        for (int i = nanCount; i < size; i++)
        {
            array[i] = i - nanCount + 1;
        }

        return array;
    }

    /// <summary>
    /// 全て NaN の float 配列を生成
    /// <br/>
    /// Generate float array with all NaN values
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <returns>全て NaN の float 配列</returns>
    public static float[] GenerateAllNaN(int size)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = float.NaN;
        }
        return array;
    }

    /// <summary>
    /// NaN なしの float 配列を生成（最適化の効果測定用）
    /// <br/>
    /// Generate float array without NaN (for optimization measurement)
    /// </summary>
    /// <param name="size">配列サイズ</param>
    /// <param name="random">乱数生成器</param>
    /// <returns>NaN を含まないランダムな float 配列</returns>
    public static float[] GenerateRandomFloatNoNaN(int size, Random random)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = (float)(random.NextDouble() * 1000.0 - 500.0);
        }
        return array;
    }

    // IntKey Pattern Generators (for JIT optimization verification)

    /// <summary>
    /// ランダム配列を生成（IntKey版）
    /// <br/>
    /// Generate random array (IntKey version)
    /// </summary>
    public static IntKey[] GenerateRandomIntKey(int size, Random random)
    {
        return Enumerable.Range(1, size)
            .OrderBy(_ => random.Next())
            .Select(x => new IntKey(x))
            .ToArray();
    }

    /// <summary>
    /// ソート済み配列を生成（IntKey版・昇順）
    /// <br/>
    /// Generate sorted array (IntKey version - ascending order)
    /// </summary>
    public static IntKey[] GenerateSortedIntKey(int size)
    {
        return Enumerable.Range(1, size)
            .Select(x => new IntKey(x))
            .ToArray();
    }

    /// <summary>
    /// 逆順配列を生成（IntKey版・降順）
    /// <br/>
    /// Generate reversed array (IntKey version - descending order)
    /// </summary>
    public static IntKey[] GenerateReversedIntKey(int size)
    {
        return Enumerable.Range(1, size)
            .Reverse()
            .Select(x => new IntKey(x))
            .ToArray();
    }

    /// <summary>
    /// 単一要素移動（ソート済みから1つの要素だけをランダム位置に移動）
    /// <br/>
    /// Generate single element moved (move one element from sorted array to random position)
    /// </summary>
    public static IntKey[] GenerateSingleElementMovedIntKey(int size, Random random)
    {
        var array = Enumerable.Range(1, size)
            .Select(x => new IntKey(x))
            .ToArray();

        if (size < 2) return array;

        // Pick a random element to move
        var sourceIndex = random.Next(size);
        var destIndex = random.Next(size);

        if (sourceIndex == destIndex) return array;

        // Move element using rotation
        var element = array[sourceIndex];

        if (destIndex < sourceIndex)
        {
            // Shift elements right
            Array.Copy(array, destIndex, array, destIndex + 1, sourceIndex - destIndex);
            array[destIndex] = element;
        }
        else
        {
            // Shift elements left
            Array.Copy(array, sourceIndex + 1, array, sourceIndex, destIndex - sourceIndex);
            array[destIndex] = element;
        }

        return array;
    }

    /// <summary>
    /// ほぼソート済み配列を生成（IntKey版・要素の10%をランダムに入れ替え）
    /// <br/>
    /// Generate nearly sorted array (IntKey version - randomly swap 10% of elements)
    /// </summary>
    public static IntKey[] GenerateNearlySortedIntKey(int size, Random random)
    {
        var array = Enumerable.Range(1, size)
            .Select(x => new IntKey(x))
            .ToArray();

        // 要素の10%をランダムに入れ替え
        var swapCount = Math.Max(1, size / 10);
        for (int i = 0; i < swapCount; i++)
        {
            var index1 = random.Next(size);
            var index2 = random.Next(size);
            (array[index1], array[index2]) = (array[index2], array[index1]);
        }

        return array;
    }

    /// <summary>
    /// ほぼソート済み配列（5%のペアをランダムスワップ）を生成 
    /// <br/>
    /// Generate almost sorted array (randomly swap 5% of pairs)
    /// </summary>
    public static IntKey[] GenerateAlmostSortedIntKey(int size, Random random)
    {
        var array = Enumerable.Range(1, size)
            .Select(x => new IntKey(x))
            .ToArray();
        var swapCount = Math.Max(1, size / 20);

        for (var i = 0; i < swapCount; i++)
        {
            var idx1 = random.Next(size);
            var idx2 = random.Next(size);
            (array[idx1], array[idx2]) = (array[idx2], array[idx1]);
        }

        return array;
    }

    /// <summary>
    /// 負の値と正の値のランダム配列を生成（IntKey版）
    /// <br/>
    /// Generate negative and positive random array (IntKey version)
    /// </summary>
    public static IntKey[] GenerateNegativePositiveRandomIntKey(int size, Random random)
    {
        return Enumerable.Range(-1 * (size / 2), size)
            .OrderBy(_ => random.Next())
            .Select(x => new IntKey(x))
            .ToArray();
    }

    /// <summary>
    /// パイプオルガン型（IntKey版・偶数要素が前半、奇数要素が後半逆順）
    /// <br/>
    /// Generate pipe organ (IntKey version - even elements in first half, odd elements in reversed second half)
    /// </summary>
    public static IntKey[] GeneratePipeOrganIntKey(int size)
    {
        var array = new IntKey[size];
        var sorted = Enumerable.Range(1, size).ToArray();
        var left = 0;
        var right = size - 1;

        for (var i = 0; i < size; i++)
        {
            if (i % 2 == 0)
            {
                array[left++] = new IntKey(sorted[i]);
            }
            else
            {
                array[right--] = new IntKey(sorted[i]);
            }
        }

        return array;
    }

    /// <summary>
    /// 少数ユニーク値（IntKey版・16種類の値）
    /// <br/>
    /// Generate few unique values (IntKey version - 16 distinct values)
    /// </summary>
    public static IntKey[] GenerateFewUniqueIntKey(int size, Random random)
    {
        return GenerateFewUniqueIntKey(size, 16, random);
    }

    /// <summary>
    /// 少数ユニーク値（IntKey版・指定されたユニーク数）
    /// <br/>
    /// Generate few unique values (IntKey version - specified number of distinct values)
    /// </summary>
    public static IntKey[] GenerateFewUniqueIntKey(int size, int uniqueCount, Random random)
    {
        if (uniqueCount < 1)
            throw new ArgumentException("Unique count must be at least 1", nameof(uniqueCount));
        if (uniqueCount > size)
            uniqueCount = size;

        // Generate evenly distributed unique values across the range
        var values = new IntKey[uniqueCount];
        for (var i = 0; i < uniqueCount; i++)
        {
            values[i] = new IntKey((int)((i + 1) * (double)size / (uniqueCount + 1)));
        }

        // Evenly distribute counts for each unique value
        var baseCount = size / uniqueCount;
        var remainder = size % uniqueCount;

        // Build result array
        var result = new IntKey[size];
        var index = 0;
        for (var i = 0; i < uniqueCount; i++)
        {
            // Each unique value gets baseCount occurrences, plus 1 for the first 'remainder' values
            var count = baseCount + (i < remainder ? 1 : 0);
            for (var j = 0; j < count; j++)
            {
                result[index++] = values[i];
            }
        }

        // Shuffle using Fisher-Yates algorithm
        for (var i = size - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    /// <summary>
    /// QuickSort最悪ケース（IntKey版・median-of-3 pivot選択用）
    /// <br/>
    /// Generate QuickSort adversary (IntKey version - worst case for median-of-3 pivot selection)
    /// </summary>
    public static IntKey[] GenerateQuickSortAdversaryIntKey(int size)
    {
        if (size <= 0)
            return Array.Empty<IntKey>();

        var result = new IntKey[size];
        int pos = 0;

        // First half: odd numbers, interleaving low/high
        int lowOdd = 1;
        int highOdd = (size % 2 == 0) ? size - 1 : size;

        while (pos < size / 2)
        {
            result[pos++] = new IntKey(lowOdd);
            lowOdd += 2;

            if (pos < size / 2)
            {
                result[pos++] = new IntKey(highOdd);
                highOdd -= 2;
            }
        }

        // Second half: all even numbers ascending
        for (int even = 2; pos < size; even += 2)
        {
            result[pos++] = new IntKey(even);
        }

        return result;
    }
}

using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// Shell sort の h-spaced 部分列ギャップを追跡し、TutorialStep.ShellGap を設定するトラッカー。
/// Compare(i, j) on main array で |i-j| > 0 のとき gap を更新し、
/// MarbleRenderer がドット色分けを行えるよう ShellGap を付加する。
/// </summary>
sealed class ShellGapTracker : IVisualizationTracker
{
    private int _currentGap = 1;

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        // Compare on main array (both bufferIds == 0) → update gap
        if (op.Type == OperationType.Compare && op.BufferId1 == 0 && op.BufferId2 == 0)
        {
            int gap = Math.Abs(op.Index1 - op.Index2);
            if (gap > 0)
                _currentGap = gap;
        }
    }

    public TutorialStep Decorate(TutorialStep step)
        => step with { ShellGap = _currentGap };

    public void PostStep() { }
}

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Breakpoints for responsive card grids (Query History, My Documents, peers):
/// two columns from <see cref="TwoColumnMinWidth"/>, three from
/// <see cref="ThreeColumnMinWidth"/>, four (or, for a max of six, six) from
/// <see cref="FourColumnMinWidth"/>, capped by the caller's max so tiles reflow
/// instead of clipping at the 800×600 minimum window size (022 FR-013).
/// </summary>
public static class ResponsiveGridLayout
{
    /// <summary>Width at or above which the grid can show two columns.</summary>
    public const double TwoColumnMinWidth = 560;

    /// <summary>Width at or above which the grid can show three columns.</summary>
    public const double ThreeColumnMinWidth = 900;

    /// <summary>Width at or above which the grid can show four columns.</summary>
    public const double FourColumnMinWidth = 1200;

    /// <summary>
    /// Column count for <paramref name="availableWidth"/>, clamped to
    /// <c>1..maxColumns</c>. An unknown width
    /// (<see cref="double.NaN"/>) defaults to <paramref name="maxColumns"/> so
    /// the first render matches the intended desktop layout.
    /// </summary>
    public static int ColumnsFor(double availableWidth, int maxColumns)
    {
        var cap = Math.Clamp(maxColumns, 1, 6);

        if (double.IsNaN(availableWidth))
        {
            return cap;
        }

        var columns = 1;
        if (availableWidth >= TwoColumnMinWidth)
        {
            columns = 2;
        }
        if (availableWidth >= ThreeColumnMinWidth)
        {
            columns = 3;
        }
        if (availableWidth >= FourColumnMinWidth)
        {
            // At desktop width a six-column grid (the Dashboard metrics) jumps
            // straight from three to six: an intermediate four would strand the
            // last two cards on an unbalanced 4 + 2 second row.
            columns = cap >= 6 ? 6 : 4;
        }

        return Math.Min(columns, cap);
    }

    /// <summary>
    /// Column count for <paramref name="availableWidth"/> capped at three
    /// columns (Query History).
    /// </summary>
    public static int ColumnsFor(double availableWidth) => ColumnsFor(availableWidth, 3);
}
